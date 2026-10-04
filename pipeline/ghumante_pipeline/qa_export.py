"""QA export (docs/DATA_FORMATS.md section 5): decode a region pack into GeoJSON + PNGs.

Everything here is produced by **decoding the pack** (``tile_format.decode_tile``
on every blob that ``pack.PackReader`` returns, CRCs checked), never from
pipeline intermediates. Overlaying the output on OpenStreetMap in the QA viewer
(``tools/qa-viewer``) therefore checks the whole chain: extract, project, clip,
quantise, encode, decode and unproject.

Output (``out_dir``, normally ``build/regions/<region>/qa``)::

    index.json                         region bbox, tile list (corners in lon/lat), layers, palette, stats
    roads.geojson trails.geojson       ROAD records; trails are model.TRAIL_CLASSES
    buildings.geojson                  BLDG records (also per tile: buildings/<L>/<tx>_<ty>.geojson)
    areas.geojson lines.geojson pois.geojson
    hillshade/<L>/<tx>_<ty>.png        leaf tiles, from HGHT
    biome/<L>/<tx>_<ty>.png            leaf tiles, BIOM coloured with BIOME_PALETTE

* Coordinates are lon/lat (``projection.game_to_lonlat``) rounded to 7
  decimals (about 1 cm). Context points of road and line pieces are stripped;
  the flags still say whether a piece had them.
* Enum properties are member NAMES; flags are integer bit masks. Every feature
  has ``tile`` = ``"L/tx/ty"``. Roads carry both ``road_class`` and its alias
  ``class``, ``sac_scale`` and ``sac``, and a boolean ``oneway``.
* PNGs are ``n x n`` (the HGHT / BIOM grid), **row 0 north**, vertex-registered:
  pixel centres sit on the samples, so the outer pixel centres lie on the tile
  edges. ``index.json`` lists each tile's four corners ``[sw, se, ne, nw]`` in
  lon/lat, since a tile is a slightly rotated quad in lon/lat.
* Hillshade: grey levels ``255 * max(0, cos(zenith) cos(slope) + sin(zenith)
  sin(slope) cos(azimuth - aspect))`` with the sun at azimuth 315 deg
  (north-west), altitude 45 deg, from the dequantised heights at true spacing.
* Biome colours: ``BIOME_PALETTE`` below (the QA viewer sample's palette), one
  entry per ``model.Biome``; unknown values are magenta.
"""

from __future__ import annotations

import json
import math
import shutil
from collections import Counter
from pathlib import Path
from typing import Iterable

import numpy as np
from PIL import Image

from . import projection
from .model import (TRAIL_CLASSES, AreaKind, Biome, BuildingArchetype, BuildingUse, LineKind, PlaceKind, PoiKind,
                    RoadClass, RoofMaterial, RoofShape, SacScale, Surface, SURFACE_GROUP, SurfaceSource,
                    WallMaterial)
from .pack import PackReader
from .projection import TileId
from .tile_format import ROAD_HAS_NEXT_CTX, ROAD_HAS_PREV_CTX, LineFlags, TileData, decode_tile, dequantize_heights

QA_INDEX_FORMAT = "ghumante-qa-index"
QA_INDEX_VERSION = 1
COORD_DECIMALS = 7
PLACE_KIND_OFFSET = 1000
MAGENTA = "#ff00ff"

BIOME_PALETTE: dict[str, str] = {
    "NONE": "#000000", "WATER": "#4a90d9", "URBAN_DENSE": "#b0a397", "URBAN_GREEN": "#a9c79a",
    "TERAI_PADDY": "#c8e07a", "TERAI_CROPLAND": "#e2d886", "TERAI_SAL_FOREST": "#3f7f3a",
    "TERAI_GRASSLAND": "#cfe39a", "RIVERBED_GRAVEL": "#d8cfc0", "CHURE_FOREST": "#5b8f4a",
    "HILL_TERRACES": "#d9c46a", "HILL_FOREST": "#2f6b3a", "HILL_SCRUB": "#8fae6b", "HILL_GRASSLAND": "#b9d48a",
    "SUBALPINE_FOREST": "#2c5a4a", "ALPINE_MEADOW": "#a7c98f", "ALPINE_SCRUB": "#8a9f75", "SCREE_ROCK": "#9a9590",
    "MORAINE": "#b7aea2", "GLACIER": "#d6eef8", "SNOW": "#ffffff", "TRANS_HIMALAYAN_STEPPE": "#d9b98c",
    "TRANS_HIMALAYAN_CROPLAND": "#c9a86a", "ORCHARD": "#9cc56b", "TEA_GARDEN": "#4f9a4f", "WETLAND": "#7fc6a4",
    "VALLEY_CROPLAND": "#e6d77a", "BARE_SOIL": "#c9a27e",
}

SUN_AZIMUTH_DEG = 315.0
SUN_ALTITUDE_DEG = 45.0

LAYERS = ("roads", "trails", "buildings", "areas", "lines", "pois")


def _enum_name(enum, v: int) -> str | int:
    try:
        return enum(int(v)).name
    except ValueError:
        return int(v)


def _poi_kind_name(v: int) -> str | int:
    if v >= PLACE_KIND_OFFSET:
        try:
            return "PLACE_" + PlaceKind(v - PLACE_KIND_OFFSET).name
        except ValueError:
            return int(v)
    return _enum_name(PoiKind, v)


def _hex_rgb(h: str) -> tuple[int, int, int]:
    h = h.lstrip("#")
    return int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16)


def biome_lut() -> np.ndarray:
    """256 x 3 uint8 colour table indexed by biome value (magenta for unknown)."""
    lut = np.tile(np.array(_hex_rgb(MAGENTA), dtype=np.uint8), (256, 1))
    for b in Biome:
        lut[int(b)] = _hex_rgb(BIOME_PALETTE.get(b.name, MAGENTA))
    return lut


def hillshade(heights_m: np.ndarray, spacing_m: float, azimuth_deg: float = SUN_AZIMUTH_DEG,
              altitude_deg: float = SUN_ALTITUDE_DEG) -> np.ndarray:
    """uint8 hillshade of a south-up grid (row 0 south), returned row 0 south."""
    h = np.asarray(heights_m, dtype=np.float64)
    dz_dy, dz_dx = np.gradient(h, spacing_m)  # rows go north, columns east
    slope = np.arctan(np.hypot(dz_dx, dz_dy))
    aspect = np.arctan2(-dz_dx, -dz_dy)  # direction of steepest descent, clockwise from north
    zen = math.radians(90.0 - altitude_deg)
    az = math.radians(azimuth_deg)
    shade = math.cos(zen) * np.cos(slope) + math.sin(zen) * np.sin(slope) * np.cos(az - aspect)
    return np.clip(np.rint(255.0 * np.clip(shade, 0.0, 1.0)), 0, 255).astype(np.uint8)


def _png(path: Path, img: Image.Image) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path, format="PNG", optimize=False)


def tile_corners_lonlat(tile: TileId) -> list[list[float]]:
    """``[sw, se, ne, nw]`` corners of a tile in lon/lat (7 decimals)."""
    x0, z0, x1, z1 = tile.bounds
    lon, lat = projection.game_to_lonlat(np.array([x0, x1, x1, x0]), np.array([z0, z0, z1, z1]))
    return [[round(float(a), COORD_DECIMALS), round(float(b), COORD_DECIMALS)] for a, b in zip(lon, lat)]


class _Proj:
    """Collects game-space point arrays, then unprojects them all in one call."""

    def __init__(self) -> None:
        self.parts: list[np.ndarray] = []

    def add(self, tile: TileId, pts_cm: np.ndarray) -> int:
        p = np.asarray(pts_cm, dtype=np.float64).reshape(-1, 2)
        self.parts.append(np.stack([tile.x0 + p[:, 0] / 100.0, tile.z0 + p[:, 1] / 100.0], axis=1))
        return len(self.parts) - 1

    def resolve(self) -> list[list[list[float]]]:
        if not self.parts:
            return []
        allp = np.concatenate(self.parts)
        lon, lat = projection.game_to_lonlat(allp[:, 0], allp[:, 1])
        ll = np.round(np.stack([np.asarray(lon), np.asarray(lat)], axis=1), COORD_DECIMALS).tolist()
        out, k = [], 0
        for p in self.parts:
            out.append(ll[k:k + len(p)])
            k += len(p)
        return out


def _name_props(td: TileData, ref: int) -> dict:
    n = td.name(ref)
    if n is None:
        return {}
    props = {"name": n.default or n.en or n.ne}
    if n.en:
        props["name_en"] = n.en
    if n.ne:
        props["name_ne"] = n.ne
    return props


def _strip_ctx(pts: np.ndarray, prev: bool, nxt: bool) -> np.ndarray:
    return pts[(1 if prev else 0):len(pts) - (1 if nxt else 0)]


def _tile_features(td: TileData) -> dict[str, list[dict]]:
    """GeoJSON features of one decoded leaf tile, per layer."""
    t = td.tile
    tid = str(t)
    proj = _Proj()
    pending: list[tuple[str, dict, str, list[int]]] = []  # layer, props, geometry type, part ids

    for r in td.roads:
        pts = _strip_ctx(r.points, bool(r.flags & ROAD_HAS_PREV_CTX), bool(r.flags & ROAD_HAS_NEXT_CTX))
        cls = _enum_name(RoadClass, r.road_class)
        try:
            group = SURFACE_GROUP[Surface(r.surface)].name
        except ValueError:
            group = "DIRT"
        props = {
            "osm_way_id": r.osm_way_id, "road_class": cls, "class": cls,
            "surface": _enum_name(Surface, r.surface), "surface_group": group,
            "surface_source": _enum_name(SurfaceSource, r.surface_source), "flags": r.flags,
            "oneway": bool(r.flags & 1), "lanes": r.lanes, "sac_scale": _enum_name(SacScale, r.sac_scale),
            "sac": r.sac_scale, "trail_visibility": r.trail_visibility, "layer": r.layer, "width_cm": r.width_cm,
            "access": r.access, **_name_props(td, r.name_ref),
        }
        ref = td.name(r.ref_ref)
        props["ref"] = ref.default if ref else ""
        props["tile"] = tid
        layer = "trails" if r.road_class in {int(c) for c in TRAIL_CLASSES} else "roads"
        pending.append((layer, props, "LineString", [proj.add(t, pts)]))

    for ln in td.lines:
        pts = _strip_ctx(ln.points, bool(ln.flags & LineFlags.HAS_PREV_CTX), bool(ln.flags & LineFlags.HAS_NEXT_CTX))
        props = {"osm_way_id": ln.osm_way_id, "kind": _enum_name(LineKind, ln.kind), "flags": ln.flags,
                 "width_cm": ln.width_cm, **_name_props(td, ln.name_ref), "tile": tid}
        pending.append(("lines", props, "LineString", [proj.add(t, pts)]))

    for b in td.buildings:
        props = {"osm_ref": b.osm_ref, "osm_type": "r" if b.osm_ref & 1 else "w", "osm_id": b.osm_ref >> 1,
                 "archetype": _enum_name(BuildingArchetype, b.archetype), "use": _enum_name(BuildingUse, b.use),
                 "levels": b.levels, "flags": b.flags, "height_cm": b.height_cm, "min_height_cm": b.min_height_cm,
                 "roof_shape": _enum_name(RoofShape, b.roof_shape),
                 "roof_material": _enum_name(RoofMaterial, b.roof_material),
                 "wall_material": _enum_name(WallMaterial, b.wall_material), "seed": b.seed,
                 **_name_props(td, b.name_ref), "tile": tid}
        pending.append(("buildings", props, "Polygon", [proj.add(t, rg) for rg in b.rings]))

    for a in td.areas:
        props = {"osm_ref": a.osm_ref, "osm_type": "r" if a.osm_ref & 1 else "w", "osm_id": a.osm_ref >> 1,
                 "kind": _enum_name(AreaKind, a.kind), "flags": a.flags, "triangles": len(a.indices) // 3,
                 **_name_props(td, a.name_ref), "tile": tid}
        pending.append(("areas", props, "Polygon", [proj.add(t, a.vertices[s:s + n]) for s, n in a.rings]))

    for p in td.pois:
        props = {"osm_ref": p.osm_ref, "osm_type": "nwr"[p.osm_ref & 3] if (p.osm_ref & 3) < 3 else "?",
                 "osm_id": p.osm_ref >> 2, "kind": _poi_kind_name(p.kind), "kind_id": p.kind, "flags": p.flags,
                 "importance": p.importance, "ele_dm": p.ele_dm, "search_id": p.search_id,
                 **_name_props(td, p.name_ref), "tile": tid}
        pending.append(("pois", props, "Point", [proj.add(t, np.array([[p.x_cm, p.z_cm]]))]))

    coords = proj.resolve()
    out: dict[str, list[dict]] = {k: [] for k in LAYERS}
    for layer, props, gtype, ids in pending:
        if gtype == "Point":
            g = {"type": "Point", "coordinates": coords[ids[0]][0]}
        elif gtype == "LineString":
            g = {"type": "LineString", "coordinates": coords[ids[0]]}
        else:
            rings = [c + [c[0]] for c in (coords[i] for i in ids) if len(c) >= 3]
            if not rings:
                continue
            g = {"type": "Polygon", "coordinates": rings}
        out[layer].append({"type": "Feature", "properties": props, "geometry": g})
    return out


class _GeoJsonWriter:
    """Streams a FeatureCollection, one feature per line (deterministic, compact)."""

    def __init__(self, path: Path, name: str) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        self.f = open(path, "w", encoding="utf-8", newline="\n")
        self.f.write('{"type":"FeatureCollection","name":%s,"features":[\n' % json.dumps(name))
        self.n = 0

    def write(self, features: Iterable[dict]) -> None:
        for feat in features:
            if self.n:
                self.f.write(",\n")
            self.f.write(json.dumps(feat, ensure_ascii=False, separators=(",", ":")))
            self.n += 1

    def close(self) -> None:
        self.f.write("\n]}\n")
        self.f.close()


def export_qa(pack_path: Path, out_dir: Path, *, leaf_level: int | None = None, region_meta: dict | None = None,
              per_tile_buildings: bool = True) -> dict:
    """Write the QA export of ``pack_path`` into ``out_dir`` (replaced). Returns
    the summary that also goes into ``index.json`` (layer counts).

    ``region_meta`` (usually the region manifest) supplies ``region``, ``name``,
    ``bbox_lonlat``, ``detail_levels``, ``horizon_levels``, ``attribution`` and
    ``stats``. ``leaf_level`` defaults to ``max(detail_levels)`` from it, else
    the highest level holding detail tiles.
    """
    pack_path, out_dir = Path(pack_path), Path(out_dir)
    meta = dict(region_meta or {})
    if out_dir.exists():
        shutil.rmtree(out_dir)
    out_dir.mkdir(parents=True)
    lut = biome_lut()

    with PackReader(pack_path) as pr:
        keys = pr.keys()
        tiles = [projection.tile_from_key(k) for k in keys]
        if leaf_level is None:
            if meta.get("detail_levels"):
                leaf_level = max(int(v) for v in meta["detail_levels"])
            else:
                leaf_level = max((t.level for t in tiles), default=0)
        writers = {name: _GeoJsonWriter(out_dir / f"{name}.geojson", name) for name in LAYERS}
        tile_entries = []
        counts_total: Counter = Counter()
        raster_sizes = {}
        try:
            for key, tile in zip(keys, tiles):
                td = decode_tile(pr.get(key))
                entry = {"level": tile.level, "tx": tile.tx, "ty": tile.ty, "key": str(key),
                         "corners_lonlat": tile_corners_lonlat(tile), "detail": bool(td.has_detail)}
                c = entry["corners_lonlat"]
                entry["bounds_lonlat"] = [min(p[0] for p in c), min(p[1] for p in c),
                                          max(p[0] for p in c), max(p[1] for p in c)]
                if tile.level == leaf_level:
                    sub = f"{tile.level}/{tile.tx}_{tile.ty}.png"
                    if td.heights_q is not None:
                        n = td.heights_q.shape[0]
                        hs = hillshade(dequantize_heights(td.heights_q), tile.size / (n - 1))
                        _png(out_dir / "hillshade" / sub, Image.fromarray(np.ascontiguousarray(hs[::-1])))
                        entry["hillshade"] = "hillshade/" + sub
                        raster_sizes["hillshade"] = n
                        h = dequantize_heights(td.heights_q)
                        entry["height_range_m"] = [round(float(h.min()), 2), round(float(h.max()), 2)]
                    if td.biomes is not None:
                        rgb = lut[td.biomes[::-1]]
                        _png(out_dir / "biome" / sub, Image.fromarray(np.ascontiguousarray(rgb)))
                        entry["biome"] = "biome/" + sub
                        raster_sizes["biome"] = td.biomes.shape[0]
                if td.has_detail:
                    feats = _tile_features(td)
                    counts = {k: len(v) for k, v in feats.items() if v}
                    entry["counts"] = counts
                    counts_total.update(counts)
                    for name in LAYERS:
                        writers[name].write(feats[name])
                    if per_tile_buildings and feats["buildings"]:
                        w = _GeoJsonWriter(out_dir / "buildings" / str(tile.level) / f"{tile.tx}_{tile.ty}.geojson",
                                           "buildings")
                        w.write(feats["buildings"])
                        w.close()
                tile_entries.append(entry)
        finally:
            for w in writers.values():
                w.close()

    layers = {name: {"path": f"{name}.geojson", "count": int(counts_total.get(name, 0))} for name in LAYERS}
    if per_tile_buildings:
        layers["buildings"]["tiles"] = "buildings/{L}/{tx}_{ty}.geojson"
        layers["buildings"]["level"] = leaf_level
    index = {
        "format": QA_INDEX_FORMAT, "version": QA_INDEX_VERSION,
        "region": meta.get("region", ""), "name": meta.get("name", meta.get("region", "")),
        "data_version": meta.get("data_version"), "pipeline_version": meta.get("pipeline_version"),
        "scale_model": meta.get("scale_model", "identity"),
        "bbox_lonlat": meta.get("bbox_lonlat"),
        "detail_levels": meta.get("detail_levels", []), "horizon_levels": meta.get("horizon_levels", []),
        "leaf_level": leaf_level, "tiles": tile_entries, "layers": layers,
        "raster": {k: {"size": v, "registration": "vertex", "row0": "north"} for k, v in sorted(raster_sizes.items())},
        "biome_palette": BIOME_PALETTE,
        "hillshade": {"azimuth_deg": SUN_AZIMUTH_DEG, "altitude_deg": SUN_ALTITUDE_DEG},
        "stats": {**(meta.get("stats") or {}), "qa_layer_counts": dict(sorted(counts_total.items()))},
        "attribution": meta.get("attribution", []),
        "source_pack": pack_path.name,
    }
    (out_dir / "index.json").write_text(json.dumps(index, ensure_ascii=False, indent=1, sort_keys=False) + "\n",
                                        encoding="utf-8")
    return {"out_dir": str(out_dir), "tiles": len(tile_entries), "leaf_level": leaf_level,
            "layers": {k: v["count"] for k, v in layers.items()}}
