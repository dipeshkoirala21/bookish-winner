"""Tiling: everything a region needs -> ``GHT1`` tile blobs (docs/DATA_FORMATS.md section 1).

``build_tiles`` produces one blob per tile key for

* every **detail level** (``region.detail_levels``) over the game-space bounding
  box of ``region.bbox``, and
* every **horizon level** (``region.horizon_levels``) over that of
  ``region.horizon_bbox``.

Every tile carries ``HGHT`` (``region.height_grid`` samples per edge) and
``BIOM`` (``region.biome_grid``). Detail-level tiles also carry ``SEED`` and
``META``. Vector content (``ROAD``, ``LINE``, ``AREA``, ``BLDG``, ``POIS``,
``NAME``) is written **only at the leaf level** (``max(detail_levels)``) in M0;
those tiles set the ``has_detail`` flag. Horizon tiles carry only ``HGHT`` and
``BIOM``.

Seams
-----
* Heights are ``dem.sample_game`` at ``projection.grid_coords`` with
  ``spacing_m = S / (n - 1)``, quantised globally. Both inputs are pure
  functions of the sample position, so shared edges are bit-identical.
* Biomes use ``biomes.compute_biome_grid`` on the biome grid, with the OSM
  area kinds burned by ``rasterize.rasterize_at_samples`` (exact border test).
  Area polygons are passed whole, or clipped to the tile box grown by two
  sample spacings, so a shared border sample sees the same geometry from both
  sides.
* Roads and lines are cut with ``geom.clip_polyline`` against each tile's
  closed square from the same game-space float64 vertices (projected once),
  so neighbouring tiles compute bit-identical cut points, and ``to_local_cm``
  maps them to ``S*100`` / ``0`` on the two sides.

Feature assignment (leaf level)
-------------------------------
* Roads and lines: every leaf tile whose closed square a segment's bounding
  box touches gets a ``clip_polyline`` call (the clip drops misses). Ways with
  ``oneway=-1`` are reversed first so ``ONEWAY`` means point order.
* Areas: ``geom.clip_polygon`` per tile from an STRtree query, then
  ``geom.triangulate`` per clipped part (one ``AREA`` record each, flag
  ``CLIPPED_BY_TILE`` when the source polygon was not wholly inside the tile).
* Buildings: stored once, unclipped, in the leaf tile containing the area
  centroid of the outer ring (half-open tile squares).
* POIs: in the leaf tile containing the point. **Places are written to
  ``POIS`` too, with ``kind = PlaceKind + 1000``**, the convention of the
  search index, so map labels can come from tiles. Their ``osm_ref`` uses the
  POI form ``(osm_id << 2) | type``.

Parallelism: tiles are encoded in a ``fork`` process pool. The prepared state
(samplers with warmed caches, projected features, buckets) is a module global
inherited by the workers, which return ``(key, blob, stats)``; results are
merged by key, so the output never depends on scheduling.
"""

from __future__ import annotations

import logging
import math
import multiprocessing as mp
import os
import time
from collections import Counter, defaultdict
from concurrent.futures import ProcessPoolExecutor
from dataclasses import dataclass, field
from typing import Iterable, Mapping, Sequence

import numpy as np
import shapely
from shapely.geometry import box as shapely_box

from . import geom, projection
from .biomes import BiomeZones, compute_biome_grid
from .config import PIPELINE_DATA_VERSION, Region
from .contexts import concat_points, project_lonlat_arrays
from .dem import DemSampler
from .landcover import LandcoverSampler
from .model import AreaKind, BuildingFlags, Extract, LineKind, PlaceKind, PoiFlags, RoadFlags
from .projection import TileId
from .rasterize import burn_key, rasterize_at_samples
from .tile_format import (
    AreaFlags, AreaRec, BuildingRec, LineFlags, LineRec, NameTable, PoiRec, RoadRec, TileData, encode_tile,
    make_seed, osm_ref_nwr, osm_ref_wr, points_to_local_cm, quantize_heights, read_header, to_local_cm,
)

log = logging.getLogger(__name__)

PLACE_KIND_OFFSET = 1000  # search_index.PLACE_KIND_OFFSET
RASTER_CLIP_MARGIN_SAMPLES = 2.0
_YES = frozenset({"yes", "true", "1", "culvert", "building_passage", "flooded"})


# ---------------------------------------------------------------------------
# Tile sets
# ---------------------------------------------------------------------------
def region_game_bbox(bbox_lonlat: Sequence[float]) -> tuple[float, float, float, float]:
    return projection.bbox_lonlat_to_game(*(float(v) for v in bbox_lonlat))


def region_tiles(region: Region) -> dict[int, list[TileId]]:
    """Level -> sorted tiles: detail levels cover ``bbox``, horizon levels
    ``horizon_bbox`` (a level in both gets the union)."""
    out: dict[int, set[TileId]] = defaultdict(set)
    gb = region_game_bbox(region.bbox)
    hb = region_game_bbox(region.horizon_bbox)
    for lvl in region.detail_levels:
        out[lvl].update(projection.tiles_covering(lvl, *gb))
    for lvl in region.horizon_levels:
        out[lvl].update(projection.tiles_covering(lvl, *hb))
    return {lvl: sorted(ts, key=lambda t: t.key) for lvl, ts in sorted(out.items())}


def tiles_lonlat_bbox(tiles: Iterable[TileId], margin_deg: float = 0.0) -> tuple[float, float, float, float]:
    """Lon/lat box enclosing every tile's corners (for sizing raster mosaics)."""
    xs, zs = [], []
    for t in tiles:
        x0, z0, x1, z1 = t.bounds
        xs += [x0, x1, x1, x0, 0.5 * (x0 + x1), 0.5 * (x0 + x1)]
        zs += [z0, z0, z1, z1, z0, z1]
    if not xs:
        raise ValueError("no tiles")
    lon, lat = projection.game_to_lonlat(np.array(xs), np.array(zs))
    return (float(np.min(lon)) - margin_deg, float(np.min(lat)) - margin_deg,
            float(np.max(lon)) + margin_deg, float(np.max(lat)) + margin_deg)


# ---------------------------------------------------------------------------
# Prepared state
# ---------------------------------------------------------------------------
@dataclass
class _State:
    region: Region
    data_version: int
    leaf: int
    meta: dict | None
    dem: DemSampler
    lc_by_level: dict[int, LandcoverSampler]
    zones: BiomeZones | None
    detail_levels: frozenset[int]
    extract: Extract
    roads_game: list[np.ndarray] = field(default_factory=list)
    lines_game: list[np.ndarray] = field(default_factory=list)
    area_geoms: np.ndarray = field(default_factory=lambda: np.zeros(0, dtype=object))
    area_order: np.ndarray = field(default_factory=lambda: np.zeros(0, dtype=np.int64))  # burn rank
    area_tree: object = None
    bld_rings: list[list[np.ndarray]] = field(default_factory=list)
    poi_xz: np.ndarray = field(default_factory=lambda: np.zeros((0, 2)))
    place_xz: np.ndarray = field(default_factory=lambda: np.zeros((0, 2)))
    road_bucket: dict[TileId, list[int]] = field(default_factory=dict)
    line_bucket: dict[TileId, list[int]] = field(default_factory=dict)
    bld_bucket: dict[TileId, list[int]] = field(default_factory=dict)
    poi_bucket: dict[TileId, list[int]] = field(default_factory=dict)
    place_bucket: dict[TileId, list[int]] = field(default_factory=dict)
    search_ids: Mapping[tuple[int, int], int] = field(default_factory=dict)  # (osm_ref, entry kind) ->
    search_importance: Mapping[tuple[int, int], int] = field(default_factory=dict)
    landmark_refs: frozenset[tuple[str, int]] = frozenset()


_STATE: _State | None = None


def _segment_cells(lines: Sequence[np.ndarray], size: float, tx_lo: int, tx_hi: int, ty_lo: int, ty_hi: int
                   ) -> dict[tuple[int, int], list[int]]:
    """(tx, ty) -> sorted feature indices whose segments' bounding boxes touch
    the closed tile square, restricted to the given tile index range."""
    pts, off = concat_points(lines)
    if len(pts) < 2:
        return {}
    a, b = pts[:-1], pts[1:]
    fid = np.repeat(np.arange(len(lines)), np.diff(off))[:-1]
    valid = np.ones(len(a), dtype=bool)
    ends = off[1:-1] - 1
    valid[ends[(ends >= 0) & (ends < len(a))]] = False
    # Single-vertex lines contribute no segment; a segment starting at a line's last vertex is a join.
    a, b, fid = a[valid], b[valid], fid[valid]
    lo = np.minimum(a, b)
    hi = np.maximum(a, b)
    # Closed squares: a coordinate exactly on k*S touches tiles k-1 and k.
    lx = np.ceil(lo[:, 0] / size).astype(np.int64) - 1
    lz = np.ceil(lo[:, 1] / size).astype(np.int64) - 1
    hx = np.floor(hi[:, 0] / size).astype(np.int64)
    hz = np.floor(hi[:, 1] / size).astype(np.int64)
    lx, hx = np.maximum(lx, tx_lo), np.minimum(hx, tx_hi)
    lz, hz = np.maximum(lz, ty_lo), np.minimum(hz, ty_hi)
    keep = (lx <= hx) & (lz <= hz)
    lx, hx, lz, hz, fid = lx[keep], hx[keep], lz[keep], hz[keep], fid[keep]
    if not len(fid):
        return {}
    wx = hx - lx + 1
    cnt = wx * (hz - lz + 1)
    rep = np.repeat(np.arange(len(fid)), cnt)
    k = np.arange(int(cnt.sum())) - np.repeat(np.cumsum(cnt) - cnt, cnt)
    tx = lx[rep] + k % wx[rep]
    ty = lz[rep] + k // wx[rep]
    f = fid[rep]
    w = tx_hi - tx_lo + 1
    h = ty_hi - ty_lo + 1
    code = np.unique((f * h + (ty - ty_lo)) * w + (tx - tx_lo))
    f = code // (w * h)
    rest = code % (w * h)
    ty = rest // w + ty_lo
    tx = rest % w + tx_lo
    out: dict[tuple[int, int], list[int]] = defaultdict(list)
    for fi, x, y in zip(f.tolist(), tx.tolist(), ty.tolist()):
        out[(x, y)].append(fi)
    return out


def _ring_centroids(rings: Sequence[np.ndarray]) -> np.ndarray:
    """Vectorised area centroid of open rings (vertex mean for degenerate ones)."""
    pts, off = concat_points(rings)
    if len(pts) == 0:
        return np.zeros((0, 2))
    starts = off[:-1]
    counts = np.diff(off)
    origin = np.repeat(pts[starts], counts, axis=0)
    rel = pts - origin
    # Next vertex within the same ring (wrap to the ring's first vertex).
    nxt_idx = np.arange(len(pts)) + 1
    nxt_idx[off[1:] - 1] = starts
    rn = rel[nxt_idx]
    cross = rel[:, 0] * rn[:, 1] - rn[:, 0] * rel[:, 1]
    a2 = np.add.reduceat(cross, starts)
    cx = np.add.reduceat((rel[:, 0] + rn[:, 0]) * cross, starts)
    cz = np.add.reduceat((rel[:, 1] + rn[:, 1]) * cross, starts)
    mean_x = np.add.reduceat(rel[:, 0], starts) / counts
    mean_z = np.add.reduceat(rel[:, 1], starts) / counts
    ok = a2 != 0.0
    safe = np.where(ok, 3.0 * a2, 1.0)
    out_x = np.where(ok, cx / safe, mean_x) + pts[starts, 0]
    out_z = np.where(ok, cz / safe, mean_z) + pts[starts, 1]
    return np.stack([out_x, out_z], axis=1)


def _to_game_geoms(geoms: Sequence[object]) -> np.ndarray:
    """Lon/lat shapely geometries -> game metres (one pyproj call), made valid."""
    arr = np.empty(len(geoms), dtype=object)
    arr[:] = list(geoms)
    if not len(arr):
        return arr

    def fn(c: np.ndarray) -> np.ndarray:
        x, z = projection.lonlat_to_game(c[:, 0], c[:, 1])
        return np.stack([np.asarray(x, dtype=np.float64), np.asarray(z, dtype=np.float64)], axis=1)

    out = shapely.transform(arr, fn)
    bad = ~shapely.is_valid(out)
    if bad.any():
        out[bad] = shapely.make_valid(out[bad])
    return out


def _prepare(region: Region, extract: Extract, dem: DemSampler, lc_by_level: dict[int, LandcoverSampler],
             zones: BiomeZones | None, tiles_by_level: dict[int, list[TileId]], data_version: int,
             meta: dict | None, search_ids: Mapping[tuple[int, int], int],
             search_importance: Mapping[tuple[int, int], int],
             landmark_refs: Iterable[tuple[str, int]]) -> _State:
    leaf = region.leaf_level
    st = _State(region=region, data_version=data_version, leaf=leaf, meta=meta, dem=dem, lc_by_level=lc_by_level,
                zones=zones, detail_levels=frozenset(region.detail_levels), extract=extract,
                search_ids=dict(search_ids), search_importance=dict(search_importance),
                landmark_refs=frozenset(landmark_refs))
    ex = extract

    # Roads (oneway=-1 reversed so that point order is the travel direction) and lines.
    st.roads_game = project_lonlat_arrays([r.lonlat[::-1] if r.oneway == -1 else r.lonlat for r in ex.roads])
    st.lines_game = project_lonlat_arrays([ln.lonlat for ln in ex.lines])

    # Areas in game space, with a global burn rank (tier, larger first, osm_ref).
    st.area_geoms = _to_game_geoms([a.polygon for a in ex.areas])
    if len(st.area_geoms):
        areas_m2 = shapely.area(st.area_geoms)
        keys = [burn_key(a.kind, float(areas_m2[i]), osm_ref_wr(a.osm_type, a.osm_id) if a.osm_type in "wr"
                         else a.osm_id) for i, a in enumerate(ex.areas)]
        order = sorted(range(len(keys)), key=lambda i: (keys[i], i))
        rank = np.empty(len(keys), dtype=np.int64)
        rank[order] = np.arange(len(keys))
        st.area_order = rank
        st.area_tree = shapely.STRtree(st.area_geoms)

    # Buildings: rings in game space, bucketed by outer-ring centroid.
    leaf_tiles = tiles_by_level.get(leaf, [])
    leaf_set = set(leaf_tiles)
    rings_flat: list[np.ndarray] = []
    for b in ex.buildings:
        rings_flat.append(b.outer)
        rings_flat.extend(b.holes)
    proj = project_lonlat_arrays(rings_flat)
    k = 0
    st.bld_rings = []
    for b in ex.buildings:
        n = 1 + len(b.holes)
        st.bld_rings.append(proj[k:k + n])
        k += n
    size = projection.tile_size(leaf)
    if ex.buildings:
        cent = _ring_centroids([r[0] for r in st.bld_rings])
        st.bld_bucket = _point_bucket(cent, leaf, leaf_set)

    # POIs and places.
    if ex.pois:
        x, z = projection.lonlat_to_game(np.array([p.lon for p in ex.pois]), np.array([p.lat for p in ex.pois]))
        st.poi_xz = np.stack([np.asarray(x), np.asarray(z)], axis=1)
        st.poi_bucket = _point_bucket(st.poi_xz, leaf, leaf_set)
    if ex.places:
        x, z = projection.lonlat_to_game(np.array([p.lon for p in ex.places]), np.array([p.lat for p in ex.places]))
        st.place_xz = np.stack([np.asarray(x), np.asarray(z)], axis=1)
        st.place_bucket = _point_bucket(st.place_xz, leaf, leaf_set)

    # Polyline buckets.
    if leaf_tiles:
        txs = [t.tx for t in leaf_tiles]
        tys = [t.ty for t in leaf_tiles]
        rng = (min(txs), max(txs), min(tys), max(tys))
        for attr, lines in (("road_bucket", st.roads_game), ("line_bucket", st.lines_game)):
            cells = _segment_cells(lines, size, *rng)
            setattr(st, attr, {TileId(leaf, tx, ty): idx for (tx, ty), idx in cells.items()
                               if TileId(leaf, tx, ty) in leaf_set})
    return st


def _point_bucket(xz: np.ndarray, level: int, tile_set: set[TileId]) -> dict[TileId, list[int]]:
    size = projection.tile_size(level)
    tx = np.floor(xz[:, 0] / size).astype(np.int64)
    ty = np.floor(xz[:, 1] / size).astype(np.int64)
    out: dict[TileId, list[int]] = defaultdict(list)
    n = 1 << level
    for i, (x, y) in enumerate(zip(tx.tolist(), ty.tolist())):
        if 0 <= x < n and 0 <= y < n:
            t = TileId(level, x, y)
            if t in tile_set:
                out[t].append(i)
    return dict(out)


def _warm_caches(st: _State, tiles_by_level: dict[int, list[TileId]]) -> None:
    """Build the samplers' per-spacing caches once, before the pool forks."""
    for lvl, tiles in tiles_by_level.items():
        if not tiles:
            continue
        t = tiles[0]
        hs = t.size / (st.region.height_grid - 1)
        bs = t.size / (st.region.biome_grid - 1)
        x, z = np.array([t.x0]), np.array([t.z0])
        st.dem.sample_game(x, z, spacing_m=hs)
        st.dem.sample_game(x, z, spacing_m=bs)
        lon, lat = projection.game_to_lonlat(x, z)
        st.lc_by_level[lvl].sample_lonlat(np.asarray(lon), np.asarray(lat), spacing_m=bs)


# ---------------------------------------------------------------------------
# Per-tile work
# ---------------------------------------------------------------------------
def _flag(tags: Mapping[str, str], key: str) -> bool:
    return (tags.get(key) or "").strip().lower() in _YES


def _area_kind_grid(st: _State, tile: TileId, n: int) -> np.ndarray:
    spacing = tile.size / (n - 1)
    if st.area_tree is None:
        return np.zeros((n, n), dtype=np.uint8)
    m = RASTER_CLIP_MARGIN_SAMPLES * spacing
    x0, z0, x1, z1 = tile.bounds
    idx = st.area_tree.query(shapely_box(x0 - m, z0 - m, x1 + m, z1 + m))
    if not len(idx):
        return np.zeros((n, n), dtype=np.uint8)
    idx = sorted(idx.tolist(), key=lambda i: st.area_order[i])
    shapes = []
    for i in idx:
        g = st.area_geoms[i]
        gx0, gz0, gx1, gz1 = g.bounds
        if gx0 < x0 - m or gz0 < z0 - m or gx1 > x1 + m or gz1 > z1 + m:
            g = shapely.clip_by_rect(g, x0 - m, z0 - m, x1 + m, z1 + m)
        if g.is_empty:
            continue
        shapes.append((g, int(st.extract.areas[i].kind)))
    return rasterize_at_samples(shapes, tile.x0, tile.z0, spacing, n)


def _terrain(st: _State, tile: TileId) -> tuple[np.ndarray, np.ndarray]:
    n = st.region.height_grid
    xs, zs = projection.grid_coords(tile, n)
    h = st.dem.sample_game(xs, zs, spacing_m=tile.size / (n - 1))
    heights_q = quantize_heights(h)
    nb = st.region.biome_grid
    bx, bz = projection.grid_coords(tile, nb)
    akind = _area_kind_grid(st, tile, nb)
    biomes = compute_biome_grid(bx, bz, tile.size / (nb - 1), st.dem, st.lc_by_level[tile.level], akind, st.zones)
    return heights_q, biomes.astype(np.uint8)


def _beyond_ctx(tile: TileId, full: np.ndarray, ctx: np.ndarray, cut_cm: np.ndarray, step: int) -> np.ndarray | None:
    """The first original vertex from ``ctx`` on, walking away from the tile
    (``step`` -1 before a start cut, +1 after an end cut), whose centimetre
    position differs from the cut point. None when there is none, or when that
    vertex lies strictly inside the tile (the way only grazed the border by
    less than half a centimetre and comes back)."""
    pts = np.asarray(full, dtype=np.float64)
    hit = np.flatnonzero((pts[:, 0] == ctx[0]) & (pts[:, 1] == ctx[1]))
    if not hit.size:
        return None
    closed = len(pts) > 2 and np.array_equal(pts[0], pts[-1])
    n = len(pts) - 1 if closed else len(pts)
    j = int(hit[0]) % n if closed else int(hit[0])
    for _ in range(n):
        if not closed and not 0 <= j < n:
            return None
        c = points_to_local_cm(tile, pts[j:j + 1])[0]
        if not np.array_equal(c, cut_cm):
            sc = int(round(tile.size * 100))
            if 0 < c[0] < sc and 0 < c[1] < sc:
                return None
            return c
        j = (j + step) % n if closed else j + step
    return None


def _piece_cm(tile: TileId, piece: geom.Piece, full: np.ndarray) -> tuple[np.ndarray, bool, bool] | None:
    """A clipped piece in local centimetres, fixed up for rounding (DATA_FORMATS 1.4).

    Rounding to whole centimetres can merge an original vertex lying within
    0.5 cm of the border with the cut point. Consecutive duplicate in-tile
    points are dropped (a piece left with fewer than 2 is dropped), and a
    context point that rounds onto its cut point is replaced by the next
    original vertex further out that does not, which is the first in-tile
    point of the neighbour's piece, so both sides still see the same tangent
    at the cut. Without such a vertex the context flag is cleared.
    """
    pc = points_to_local_cm(tile, piece.points)
    prev, nxt = bool(piece.has_prev_ctx), bool(piece.has_next_ctx)
    raw = pc[(1 if prev else 0):len(pc) - (1 if nxt else 0)]
    keep = np.ones(len(raw), dtype=bool)
    keep[1:] = np.any(raw[1:] != raw[:-1], axis=1)
    inner = raw[keep]
    if len(inner) < 2:
        return None
    parts = []
    if prev:
        c = pc[0]
        if np.array_equal(c, inner[0]):
            c = _beyond_ctx(tile, full, piece.points[0], inner[0], -1)
        if c is None:
            prev = False
        else:
            parts.append(np.asarray(c, dtype=np.int64).reshape(1, 2))
    parts.append(inner)
    if nxt:
        c = pc[-1]
        if np.array_equal(c, inner[-1]):
            c = _beyond_ctx(tile, full, piece.points[-1], inner[-1], +1)
        if c is None:
            nxt = False
        else:
            parts.append(np.asarray(c, dtype=np.int64).reshape(1, 2))
    return np.concatenate(parts).astype(np.int64), prev, nxt


def _road_records(st: _State, tile: TileId, names: NameTable, stats: Counter) -> list[RoadRec]:
    out = []
    box = tile.bounds
    for i in st.road_bucket.get(tile, ()):
        r = st.extract.roads[i]
        pieces = geom.clip_polyline(st.roads_game[i], box)
        if not pieces:
            continue
        base = 0
        if r.oneway:
            base |= RoadFlags.ONEWAY
        if r.bridge:
            base |= RoadFlags.BRIDGE
        if r.tunnel:
            base |= RoadFlags.TUNNEL
        if r.is_link:
            base |= RoadFlags.LINK
        if r.ford:
            base |= RoadFlags.FORD
        if r.sac_inferred:
            base |= RoadFlags.SAC_INFERRED
        name_ref = names.ref(r.name)
        ref_ref = names.ref_str(r.ref)
        width_cm = int(round(r.width_m * 100)) if r.width_m and r.width_m > 0 else 0
        for p in pieces:
            fixed = _piece_cm(tile, p, st.roads_game[i])
            if fixed is None:
                stats["road_pieces_dropped_rounding"] += 1
                continue
            pts_cm, has_prev, has_next = fixed
            flags = int(base)
            if has_prev:
                flags |= RoadFlags.HAS_PREV_CTX
            if has_next:
                flags |= RoadFlags.HAS_NEXT_CTX
            out.append(RoadRec(
                osm_way_id=int(r.osm_id), road_class=int(r.cls), surface=int(r.surface),
                surface_source=int(r.surface_source), flags=flags, lanes=min(max(int(r.lanes), 0), 255),
                sac_scale=int(r.sac_scale), trail_visibility=min(max(int(r.trail_visibility), 0), 255),
                layer=min(max(int(r.layer), -128), 127), width_cm=width_cm, access=int(r.access) & 0xFF,
                name_ref=name_ref, ref_ref=ref_ref, points=pts_cm))
            stats["road_pieces"] += 1
    return out


def _line_records(st: _State, tile: TileId, names: NameTable, stats: Counter) -> list[LineRec]:
    out = []
    box = tile.bounds
    for i in st.line_bucket.get(tile, ()):
        ln = st.extract.lines[i]
        if ln.kind == LineKind.NONE:
            continue
        pieces = geom.clip_polyline(st.lines_game[i], box)
        if not pieces:
            continue
        base = 0
        if _flag(ln.tags, "intermittent"):
            base |= LineFlags.INTERMITTENT
        if _flag(ln.tags, "tunnel"):
            base |= LineFlags.TUNNEL
        name_ref = names.ref(ln.name)
        width_cm = int(round(ln.width_m * 100)) if ln.width_m and ln.width_m > 0 else 0
        for p in pieces:
            fixed = _piece_cm(tile, p, st.lines_game[i])
            if fixed is None:
                stats["line_pieces_dropped_rounding"] += 1
                continue
            pts_cm, has_prev, has_next = fixed
            flags = int(base)
            if has_prev:
                flags |= LineFlags.HAS_PREV_CTX
            if has_next:
                flags |= LineFlags.HAS_NEXT_CTX
            out.append(LineRec(osm_way_id=int(ln.osm_id), kind=int(ln.kind), flags=flags, width_cm=width_cm,
                               name_ref=name_ref, points=pts_cm))
            stats["line_pieces"] += 1
    return out


def _area_records(st: _State, tile: TileId, names: NameTable, stats: Counter) -> list[AreaRec]:
    if st.area_tree is None:
        return []
    out = []
    x0, z0, x1, z1 = box = tile.bounds
    idx = sorted(st.area_tree.query(shapely_box(x0, z0, x1, z1)).tolist())
    for i in idx:
        a = st.extract.areas[i]
        if a.kind == AreaKind.NONE or a.osm_type not in ("w", "r"):
            continue
        g = st.area_geoms[i]
        gx0, gz0, gx1, gz1 = g.bounds
        clipped = not (gx0 >= x0 and gz0 >= z0 and gx1 <= x1 and gz1 <= z1)
        try:
            parts = geom.clip_polygon(g, box)
        except Exception as e:  # noqa: BLE001 - GEOS topology errors on broken source polygons
            stats["area_clip_errors"] += 1
            log.debug("area %s%s clip failed in %s: %s", a.osm_type, a.osm_id, tile, e)
            continue
        name_ref = names.ref(a.name)
        osm_ref = osm_ref_wr(a.osm_type, a.osm_id)
        for poly in parts:
            try:
                verts, tris, rings = geom.triangulate(poly)
            except ValueError as e:
                stats["area_triangulation_errors"] += 1
                log.debug("area %s%s triangulation failed in %s: %s", a.osm_type, a.osm_id, tile, e)
                continue
            vcm = points_to_local_cm(tile, verts)
            if len(tris):
                # Drop triangles that collapse to zero area on the centimetre grid.
                p = vcm[tris.reshape(-1, 3)]
                cross = ((p[:, 1, 0] - p[:, 0, 0]) * (p[:, 2, 1] - p[:, 0, 1])
                         - (p[:, 1, 1] - p[:, 0, 1]) * (p[:, 2, 0] - p[:, 0, 0]))
                tris = tris.reshape(-1, 3)[cross != 0].reshape(-1)
            if len(tris) == 0:
                stats["area_parts_empty"] += 1
                continue
            out.append(AreaRec(osm_ref=osm_ref, kind=int(a.kind),
                               flags=int(AreaFlags.CLIPPED_BY_TILE) if clipped else 0, name_ref=name_ref,
                               vertices=vcm, indices=tris, rings=list(rings)))
            stats["area_parts"] += 1
    return out


def _building_records(st: _State, tile: TileId, names: NameTable, stats: Counter) -> list[BuildingRec]:
    out = []
    for i in st.bld_bucket.get(tile, ()):
        b = st.extract.buildings[i]
        flags = int(b.flags)
        if (b.osm_type, int(b.osm_id)) in st.landmark_refs:
            flags |= BuildingFlags.LANDMARK
        levels = int(round(b.levels)) if b.levels is not None and math.isfinite(b.levels) else 1
        rings = [points_to_local_cm(tile, r) for r in st.bld_rings[i]]
        if len(rings[0]) < 3:
            stats["buildings_degenerate"] += 1
            continue
        rings = [rings[0]] + [h for h in rings[1:] if len(h) >= 3]
        out.append(BuildingRec(
            osm_ref=osm_ref_wr(b.osm_type, b.osm_id), archetype=int(b.archetype), use=int(b.use),
            levels=min(max(levels, 1), 255), flags=flags & 0xFF,
            height_cm=max(0, int(round(b.height_m * 100))) if b.height_m is not None else 0,
            min_height_cm=max(0, int(round(b.min_height_m * 100))) if b.min_height_m else 0,
            roof_shape=int(b.roof_shape), roof_material=int(b.roof_material), wall_material=int(b.wall_material),
            name_ref=names.ref(b.name), rings=rings))
    stats["buildings"] += len(out)
    return out


def _poi_records(st: _State, tile: TileId, names: NameTable, stats: Counter) -> list[PoiRec]:
    out = []
    ex = st.extract
    for i in st.poi_bucket.get(tile, ()):
        p = ex.pois[i]
        ref = osm_ref_nwr(p.osm_type, p.osm_id)
        flags = int(p.flags)
        if (p.osm_type, int(p.osm_id)) in st.landmark_refs:
            flags |= PoiFlags.LANDMARK
        ele_dm = 0
        if p.ele_m is not None and math.isfinite(p.ele_m):
            flags |= PoiFlags.HAS_ELE
            ele_dm = int(round(p.ele_m * 10))
        skey = (ref, int(p.kind))
        imp = st.search_importance.get(skey)
        if imp is None:
            imp = int(round(min(max(p.importance, 0.0), 1.0) * 255))
        xc, zc = to_local_cm(tile, st.poi_xz[i, 0], st.poi_xz[i, 1])
        out.append(PoiRec(osm_ref=ref, kind=int(p.kind), flags=flags & 0xFF, importance=int(imp), x_cm=int(xc),
                          z_cm=int(zc), ele_dm=ele_dm, name_ref=names.ref(p.name),
                          search_id=int(st.search_ids.get(skey, 0))))
    for i in st.place_bucket.get(tile, ()):
        p = ex.places[i]
        if p.kind == PlaceKind.NONE:
            continue
        ref = osm_ref_nwr(p.osm_type, p.osm_id)
        skey = (ref, int(p.kind) + PLACE_KIND_OFFSET)
        imp = st.search_importance.get(skey)
        if imp is None:
            imp = int(round(min(max(p.importance, 0.0), 1.0) * 255))
        flags = int(PoiFlags.LANDMARK) if (p.osm_type, int(p.osm_id)) in st.landmark_refs else 0
        xc, zc = to_local_cm(tile, st.place_xz[i, 0], st.place_xz[i, 1])
        out.append(PoiRec(osm_ref=ref, kind=int(p.kind) + PLACE_KIND_OFFSET, flags=flags, importance=int(imp),
                          x_cm=int(xc), z_cm=int(zc), ele_dm=0, name_ref=names.ref(p.name),
                          search_id=int(st.search_ids.get(skey, 0))))
        stats["places"] += 1
    stats["pois"] += len(out)
    return out


def make_tile(st: _State, tile: TileId) -> tuple[bytes, Counter]:
    stats: Counter = Counter()
    heights_q, biomes = _terrain(st, tile)
    td = TileData(tile=tile, data_version=st.data_version, heights_q=heights_q, biomes=biomes)
    if tile.level in st.detail_levels:
        td.seed = make_seed(tile, st.data_version)
        td.meta = st.meta
    if tile.level == st.leaf and tile.level in st.detail_levels:
        names = NameTable()
        td.areas = _area_records(st, tile, names, stats)
        td.buildings = _building_records(st, tile, names, stats)
        td.lines = _line_records(st, tile, names, stats)
        td.pois = _poi_records(st, tile, names, stats)
        td.roads = _road_records(st, tile, names, stats)
        td.names = names.entries()
        td.has_detail = True
    blob = encode_tile(td)
    for c in read_header(blob).chunks:
        stats["bytes_" + c.fourcc.decode()] += c.stored_size
    stats["bytes"] += len(blob)
    return blob, stats


def _work(keys: list[int]) -> list[tuple[int, bytes, dict]]:
    st = _STATE
    assert st is not None
    out = []
    for k in keys:
        blob, stats = make_tile(st, projection.tile_from_key(k))
        out.append((k, blob, dict(stats)))
    return out


def _worker_init() -> None:
    # PROJ contexts are not shared safely across fork; build fresh transformers.
    projection._fwd.cache_clear()
    projection._inv.cache_clear()


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------
def build_tiles(region: Region, extract: Extract, dem: DemSampler, landcover: LandcoverSampler | Mapping[int, LandcoverSampler],
                zones: BiomeZones | None = None, *, data_version: int = PIPELINE_DATA_VERSION, meta: dict | None = None,
                search_ids: Mapping[tuple[int, int], int] | None = None,
                search_importance: Mapping[tuple[int, int], int] | None = None,
                landmark_refs: Iterable[tuple[str, int]] = (), workers: int | None = None,
                stats: dict | None = None) -> dict[int, bytes]:
    """Encode every tile of ``region``: returns ``{tile key: GHT1 blob}``.

    ``landcover`` is one sampler, or ``{level: sampler}`` (every level needed).
    ``search_ids`` maps ``(osm_ref, kind)`` -> search id (``search_index.search_ids``;
    ``kind`` is the POIS record kind, ``PlaceKind + 1000`` for places) and
    ``search_importance`` the same key -> 0..255 importance (else the feature's
    own 0..1 importance is scaled). ``landmark_refs`` holds
    ``(osm_type, osm_id)`` of hero landmarks (``LANDMARK`` flags). ``workers``
    defaults to ``min(4, cpu_count)``; 1 runs in-process. When ``stats`` is
    given it is filled with tile counts, feature counts, chunk bytes and timings.
    """
    global _STATE
    t0 = time.perf_counter()
    tiles_by_level = region_tiles(region)
    lc_by_level = dict(landcover) if isinstance(landcover, Mapping) else {lvl: landcover for lvl in tiles_by_level}
    missing = [lvl for lvl in tiles_by_level if lvl not in lc_by_level]
    if missing:
        raise ValueError(f"no landcover sampler for levels {missing}")
    st = _prepare(region, extract, dem, lc_by_level, zones, tiles_by_level, data_version, meta,
                  search_ids or {}, search_importance or {}, landmark_refs)
    t_prep = time.perf_counter()
    _warm_caches(st, tiles_by_level)
    t_warm = time.perf_counter()

    keys = [t.key for lvl in sorted(tiles_by_level) for t in tiles_by_level[lvl]]
    workers = workers or min(4, os.cpu_count() or 1)
    results: dict[int, bytes] = {}
    agg: Counter = Counter()
    per_key_stats: list[tuple[int, dict]] = []
    _STATE = st
    try:
        if workers <= 1 or len(keys) < 8:
            batches = [_work(keys)]
        else:
            # Interleave keys so every chunk mixes cheap horizon tiles and dense leaf tiles.
            n_chunks = max(workers * 8, 1)
            chunks = [keys[i::n_chunks] for i in range(n_chunks)]
            chunks = [c for c in chunks if c]
            ctx = mp.get_context("fork")
            with ProcessPoolExecutor(max_workers=workers, mp_context=ctx, initializer=_worker_init) as pool:
                batches = list(pool.map(_work, chunks))
    finally:
        _STATE = None
    for batch in batches:
        for k, blob, s in batch:
            results[k] = blob
            per_key_stats.append((k, s))
    for k, s in sorted(per_key_stats, key=lambda t: t[0]):
        agg.update(s)
    t_end = time.perf_counter()

    if stats is not None:
        leaf_tiles = tiles_by_level.get(region.leaf_level, [])
        assigned = sum(len(v) for v in st.bld_bucket.values())
        stats.update({
            "tile_counts": {str(lvl): len(ts) for lvl, ts in tiles_by_level.items()},
            "tiles": len(results),
            "leaf_level": region.leaf_level,
            "leaf_tiles": len(leaf_tiles),
            "leaf_tiles_with_content": sum(1 for t in leaf_tiles if t in st.bld_bucket or t in st.road_bucket),
            "buildings_in_extract": len(extract.buildings),
            "buildings_assigned": assigned,
            "buildings_outside_tiles": len(extract.buildings) - assigned,
            "features": {k: int(v) for k, v in sorted(agg.items()) if not k.startswith("bytes")},
            "chunk_bytes": {k[6:]: int(v) for k, v in sorted(agg.items()) if k.startswith("bytes_")},
            "tile_bytes": int(agg["bytes"]),
            "workers": workers,
            "timing_s": {"prepare": round(t_prep - t0, 3), "warm_caches": round(t_warm - t_prep, 3),
                         "encode": round(t_end - t_warm, 3), "total": round(t_end - t0, 3)},
        })
    return results
