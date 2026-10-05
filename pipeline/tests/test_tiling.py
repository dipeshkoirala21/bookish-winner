"""Unit tests for tiling.py and contexts.py on small hand-made inputs."""

from __future__ import annotations

import numpy as np
import pytest
from shapely.geometry import Polygon

from ghumante_pipeline import contexts, geom, projection, tiling
from ghumante_pipeline.buildings import ArchetypeZones
from ghumante_pipeline.config import Region
from ghumante_pipeline.dem import DemSampler, GeoGrid
from ghumante_pipeline.landcover import LandcoverSampler
from ghumante_pipeline.model import (AreaFeature, AreaKind, BuildingFeature, BuildingFlags, Extract, PlaceFeature,
                                     PlaceKind, PoiFeature, PoiFlags, PoiKind, RoadClass, RoadFeature, RoadFlags,
                                     NameRec, LineFeature, LineKind)
from ghumante_pipeline.pack import write_pack
from ghumante_pipeline.projection import TileId
from ghumante_pipeline.surface import density_bin, elev_bin, slope_bin
from ghumante_pipeline.tile_format import AreaFlags, decode_tile, points_to_local_cm

LEAF = 10
S = projection.tile_size(LEAF)
TX, TY = 516, 162  # a leaf tile near Thamel


# ---------------------------------------------------------------------------
# Small synthetic samplers
# ---------------------------------------------------------------------------
def _grid(ppd: int, box=(85.25, 27.65, 85.36, 27.76)) -> GeoGrid:
    lon0, lat0, lon1, lat1 = box
    c0, r0 = int(np.floor(lon0 * ppd)), int(np.floor(-lat1 * ppd))
    w, h = int(round((lon1 - lon0) * ppd)) + 2, int(round((lat1 - lat0) * ppd)) + 2
    return GeoGrid(float(ppd), float(ppd), 0.0, 0.0, c0, r0, w, h)


def _dem(ppd: int = 1200) -> DemSampler:
    g = _grid(ppd)
    rows, cols = np.mgrid[0:g.height, 0:g.width]
    lon = (g.col0 + cols + 0.5) / ppd
    lat = -(g.row0 + rows + 0.5) / ppd
    data = 1300.0 + 2000.0 * (lat - 27.70) + 30.0 * np.sin(lon * 900.0)
    return DemSampler(data.astype(np.float32), g)


def _landcover(ppd: int = 3000) -> LandcoverSampler:
    g = _grid(ppd)
    data = np.full((g.height, g.width), 50, dtype=np.uint8)
    data[:, : g.width // 3] = 40
    return LandcoverSampler(data, g)


def _region(**kw) -> Region:
    x0, z0 = TX * S, TY * S
    lon0, lat0 = projection.game_to_lonlat(x0 + 100.0, z0 + 100.0)
    lon1, lat1 = projection.game_to_lonlat(x0 + 2 * S - 100.0, z0 + 2 * S - 100.0)
    base = dict(id="unit", name_en="Unit", name_ne="", bbox=(float(lon0), float(lat0), float(lon1), float(lat1)),
                horizon_bbox=(float(lon0), float(lat0), float(lon1), float(lat1)), detail_levels=(9, 10),
                horizon_levels=(8,), height_grid=17, biome_grid=9, bbox_buffer_m=0.0)
    base.update(kw)
    return Region(**base)


def _ll(x, z) -> list[tuple[float, float]]:
    lon, lat = projection.game_to_lonlat(np.asarray(x, dtype=float), np.asarray(z, dtype=float))
    return list(zip(np.atleast_1d(lon).tolist(), np.atleast_1d(lat).tolist()))


def _road(osm_id: int, xz, cls=RoadClass.RESIDENTIAL, **kw) -> RoadFeature:
    xz = np.asarray(xz, dtype=float)
    ll = np.array(_ll(xz[:, 0], xz[:, 1]))
    return RoadFeature(osm_id=osm_id, cls=cls, lonlat=ll, node_ids=np.arange(len(ll), dtype=np.int64) + osm_id * 100,
                       **kw)


def _square(cx: float, cz: float, half: float) -> np.ndarray:
    return np.array(_ll([cx - half, cx + half, cx + half, cx - half], [cz - half, cz - half, cz + half, cz + half]))


# ---------------------------------------------------------------------------
# contexts.py
# ---------------------------------------------------------------------------
def test_polyline_midpoints() -> None:
    lines = [np.array([[0.0, 0.0], [10.0, 0.0], [10.0, 10.0]]),  # length 20: midpoint at the corner
             np.array([[5.0, 5.0]]),  # single vertex
             np.zeros((0, 2)),
             np.array([[0.0, 0.0], [0.0, 0.0]]),  # zero length
             np.array([[0.0, 0.0], [4.0, 0.0]])]
    m = contexts.polyline_midpoints(lines)
    np.testing.assert_allclose(m[0], [10.0, 0.0])
    np.testing.assert_allclose(m[1], [5.0, 5.0])
    assert np.isnan(m[2]).all()
    np.testing.assert_allclose(m[3], [0.0, 0.0])
    np.testing.assert_allclose(m[4], [2.0, 0.0])


def test_density_grid_counts_window() -> None:
    # 10 buildings in one 50 m cell, 5 in a cell 6 cells away (still in the window), 7 far away.
    x = np.r_[np.full(10, 1010.0), np.full(5, 1010.0 + 300.0), np.full(7, 5000.0)]
    z = np.r_[np.full(10, 1010.0), np.full(5, 1010.0), np.full(7, 5000.0)]
    g = contexts.DensityGrid(x, z, cell_m=50.0, radius_m=300.0)
    assert g.window_ha == pytest.approx(42.25)
    assert g.count_at(1010.0, 1010.0) == 15
    assert g.count_at(1010.0 + 350.0, 1010.0) == 5  # 7 cells from the first group
    assert g.count_at(5000.0, 5000.0) == 7
    assert g.count_at(-1e6, 0.0) == 0
    assert g.count_at(np.nan, 0.0) == 0
    assert g.per_ha(1010.0, 1010.0) == pytest.approx(15 / 42.25)
    # Order of the input never matters.
    perm = np.random.default_rng(1).permutation(len(x))
    g2 = contexts.DensityGrid(x[perm], z[perm])
    assert np.array_equal(g.sums, g2.sums)


def test_bins_match_surface_scalars() -> None:
    vals = np.array([-5.0, 0.0, 0.5, 2.99, 3.0, 12.0, 400.0, 499.9, 500.0, 1500.0, 3600.0, np.nan, 5.0, 15.0])
    assert contexts.density_bins(vals).tolist() == [density_bin(v) for v in vals]
    assert contexts.elev_bins(vals).tolist() == [elev_bin(v) for v in vals]
    assert contexts.slope_bins(vals).tolist() == [slope_bin(v) for v in vals]


def test_road_and_building_contexts() -> None:
    dem = _dem()
    x0, z0 = TX * S, TY * S
    buildings = []
    for k in range(60):  # a dense cluster
        cx, cz = x0 + 300.0 + (k % 8) * 15.0, z0 + 300.0 + (k // 8) * 15.0
        buildings.append(BuildingFeature("w", 1000 + k, _square(cx, cz, 4.0)))
    density = contexts.density_grid_for(buildings)
    roads = [_road(1, [[x0 + 250.0, z0 + 350.0], [x0 + 450.0, z0 + 350.0]]),
             _road(2, [[x0 + 900.0, z0 + 900.0], [x0 + 1000.0, z0 + 1000.0]])]
    arr = contexts.road_context_arrays(roads, dem, density)
    assert arr["per_ha"][0] == pytest.approx(60 / 42.25)
    assert arr["per_ha"][1] == 0.0
    np.testing.assert_allclose(arr["elev_m"], dem.sample_game(arr["mid_x"], arr["mid_z"]))
    ctx = contexts.road_contexts(roads, dem, density)
    assert ctx[0].density_bin == density_bin(60 / 42.25) == 1
    assert ctx[0].elev_bin == elev_bin(float(arr["elev_m"][0]))
    zones = ArchetypeZones(())
    bctx = contexts.building_contexts(buildings, dem, zones, density)
    assert len(bctx) == 60 and all(c.zone == "default" and c.density_bin == 1 for c in bctx)
    lon, lat, _, _ = contexts.building_centroids_game(buildings)
    np.testing.assert_allclose([c.elev_m for c in bctx], dem.sample_lonlat(lon, lat))
    assert contexts.building_contexts([], dem, zones) == []


# ---------------------------------------------------------------------------
# tiling helpers
# ---------------------------------------------------------------------------
def test_region_tiles_cover_bbox() -> None:
    reg = _region()
    tl = tiling.region_tiles(reg)
    assert sorted(tl) == [8, 9, 10]
    gb = tiling.region_game_bbox(reg.bbox)
    for lvl, tiles in tl.items():
        assert [t.key for t in tiles] == sorted(t.key for t in tiles)
        x0 = min(t.x0 for t in tiles)
        z0 = min(t.z0 for t in tiles)
        x1 = max(t.x0 + t.size for t in tiles)
        z1 = max(t.z0 + t.size for t in tiles)
        assert x0 <= gb[0] and z0 <= gb[1] and x1 >= gb[2] and z1 >= gb[3]
    assert len(tl[10]) == 4 and len(tl[9]) == 1


def test_segment_cells_closed_squares() -> None:
    # A segment running exactly along the border x = 2*S touches tiles 1 and 2.
    lines = [np.array([[2 * S, 0.5 * S], [2 * S, 0.7 * S]]),
             np.array([[0.5 * S, 0.5 * S], [0.6 * S, 0.6 * S]]),  # inside tile (0, 0)
             np.array([[0.5 * S, 0.5 * S], [2.5 * S, 0.5 * S]]),  # crosses three tiles
             np.array([[9.0 * S, 9.0 * S]])]  # single vertex: no segment
    cells = tiling._segment_cells(lines, S, 0, 3, 0, 3)
    assert sorted(t for t, ids in cells.items() if 0 in ids) == [(1, 0), (2, 0)]
    assert sorted(t for t, ids in cells.items() if 1 in ids) == [(0, 0)]
    assert sorted(t for t, ids in cells.items() if 2 in ids) == [(0, 0), (1, 0), (2, 0)]
    assert not any(3 in ids for ids in cells.values())
    # Restricting the range drops tiles outside it.
    cells = tiling._segment_cells(lines, S, 1, 1, 0, 0)
    assert set(cells) == {(1, 0)}


def test_ring_centroids_match_geom() -> None:
    rng = np.random.default_rng(3)
    rings = []
    for _ in range(20):
        n = int(rng.integers(3, 9))
        ang = np.sort(rng.uniform(0, 2 * np.pi, n))
        r = rng.uniform(5, 30, n)
        rings.append(np.stack([528000.0 + r * np.cos(ang), 165000.0 + r * np.sin(ang)], axis=1))
    rings.append(np.array([[1.0, 1.0], [2.0, 2.0], [3.0, 3.0]]))  # degenerate: vertex mean
    got = tiling._ring_centroids(rings)
    for ring, c in zip(rings, got):
        np.testing.assert_allclose(c, geom.ring_centroid(ring), rtol=0, atol=1e-6)


# ---------------------------------------------------------------------------
# build_tiles on a hand-made extract
# ---------------------------------------------------------------------------
@pytest.fixture(scope="module")
def unit_build():
    reg = _region()
    x0, z0 = TX * S, TY * S
    ex = Extract(region="unit")
    # Road 10 crosses the vertical border x = x0 + S; road 11 is oneway=-1 inside tile (TX, TY).
    ex.roads.append(_road(10, [[x0 + 200.0, z0 + 300.0], [x0 + 900.0, z0 + 320.0], [x0 + S + 400.0, z0 + 340.0]],
                          name=NameRec("Unit Marg", "Unit Marg", ""), ref="H01", width_m=7.5, lanes=2))
    ex.roads.append(_road(11, [[x0 + 100.0, z0 + 600.0], [x0 + 300.0, z0 + 600.0]], oneway=-1))
    ex.roads.append(_road(12, [[x0 + 100.0, z0 + 800.0], [x0 + 300.0, z0 + 800.0]], cls=RoadClass.PATH,
                          sac_inferred=True))
    ex.lines.append(LineFeature(20, LineKind.STREAM, np.array(_ll([x0 + 500.0, x0 + 500.0], [z0 + 900.0, z0 + S + 50.0])),
                                tags={"intermittent": "yes"}))
    # Building 30 straddles the border; its centroid is just east of it.
    ex.buildings.append(BuildingFeature("w", 30, _square(x0 + S + 2.0, z0 + 500.0, 6.0), levels=3.0,
                                        height_m=9.5, flags=BuildingFlags.LEVELS_INFERRED))
    ex.buildings.append(BuildingFeature("w", 31, _square(x0 + 400.0, z0 + 400.0, 5.0), levels=2.0, height_m=7.0))
    # Lake across the border and corner.
    lake = Polygon(_ll(x0 + S + 120.0 * np.cos(np.linspace(0, 2 * np.pi, 33)[:-1]),
                       z0 + S + 90.0 * np.sin(np.linspace(0, 2 * np.pi, 33)[:-1])))
    ex.areas.append(AreaFeature("w", 40, AreaKind.WATER_LAKE, lake, name=NameRec("Unit Pokhari", "", "")))
    plon, plat = projection.game_to_lonlat(x0 + 100.0, z0 + 100.0)
    ex.pois.append(PoiFeature("n", 50, PoiKind.TEMPLE_HINDU, float(plon), float(plat), NameRec("Unit Mandir", "", ""),
                              ele_m=1310.0, flags=PoiFlags.DISCOVERABLE, importance=0.5))
    ex.places.append(PlaceFeature("n", 60, PlaceKind.NEIGHBOURHOOD, float(plon), float(plat), NameRec("Unittol", "", "")))
    dem, lc = _dem(), _landcover()
    st: dict = {}
    tiles1 = tiling.build_tiles(reg, ex, dem, lc, None, meta={"region": "unit", "sources": {}}, workers=1,
                                search_ids={((50 << 2), int(PoiKind.TEMPLE_HINDU)): 7}, landmark_refs={("w", 31)}, stats=st)
    tiles2 = tiling.build_tiles(reg, ex, dem, lc, None, meta={"region": "unit", "sources": {}}, workers=2,
                                search_ids={((50 << 2), int(PoiKind.TEMPLE_HINDU)): 7}, landmark_refs={("w", 31)})
    return reg, ex, tiles1, tiles2, st


def test_parallel_equals_serial(unit_build) -> None:
    _, _, t1, t2, _ = unit_build
    assert t1 == t2


def test_tile_set_and_chunks(unit_build) -> None:
    reg, _, tiles, _, st = unit_build
    decoded = {projection.tile_from_key(k): decode_tile(b) for k, b in tiles.items()}
    assert st["tile_counts"] == {"8": 1, "9": 1, "10": 4}
    for t, td in decoded.items():
        assert td.heights_q.shape == (reg.height_grid, reg.height_grid)
        assert td.biomes.shape == (reg.biome_grid, reg.biome_grid)
        if t.level == 8:  # horizon: terrain only
            assert td.seed is None and td.meta is None and not td.has_detail
        else:
            assert td.seed is not None and td.meta == {"region": "unit", "sources": {}}
            assert td.has_detail == (t.level == LEAF)
        if t.level != LEAF:
            assert not (td.roads or td.buildings or td.areas or td.pois or td.lines)


def test_roads_clipped_with_context(unit_build) -> None:
    _, ex, tiles, _, _ = unit_build
    a = decode_tile(tiles[TileId(LEAF, TX, TY).key])
    b = decode_tile(tiles[TileId(LEAF, TX + 1, TY).key])
    ra = [r for r in a.roads if r.osm_way_id == 10]
    rb = [r for r in b.roads if r.osm_way_id == 10]
    assert len(ra) == 1 and len(rb) == 1
    assert ra[0].flags & RoadFlags.HAS_NEXT_CTX and not ra[0].flags & RoadFlags.HAS_PREV_CTX
    assert rb[0].flags & RoadFlags.HAS_PREV_CTX and not rb[0].flags & RoadFlags.HAS_NEXT_CTX
    cut_a = ra[0].points[-2]
    cut_b = rb[0].points[1]
    assert cut_a[0] == int(S * 100) and cut_b[0] == 0 and cut_a[1] == cut_b[1]
    # Context points are the original neighbouring vertices.
    x0, z0 = TX * S, TY * S
    assert np.abs(ra[0].points[-1] - points_to_local_cm(a.tile, [[x0 + S + 400.0, z0 + 340.0]])[0]).max() <= 1
    assert np.abs(rb[0].points[0] - points_to_local_cm(b.tile, [[x0 + 900.0, z0 + 320.0]])[0]).max() <= 1
    assert a.name(ra[0].name_ref).default == "Unit Marg" and a.name(ra[0].ref_ref).default == "H01"
    assert ra[0].width_cm == 750 and ra[0].lanes == 2
    # oneway=-1 is stored reversed and flagged ONEWAY.
    r11 = next(r for r in a.roads if r.osm_way_id == 11)
    assert r11.flags & RoadFlags.ONEWAY
    assert r11.points[0][0] > r11.points[-1][0]
    r12 = next(r for r in a.roads if r.osm_way_id == 12)
    assert r12.flags & RoadFlags.SAC_INFERRED


def test_buildings_once_by_centroid(unit_build) -> None:
    _, _, tiles, _, st = unit_build
    found = {}
    for k, blob in tiles.items():
        for b in decode_tile(blob).buildings:
            found.setdefault(b.osm_ref, []).append(projection.tile_from_key(k))
    assert sorted(found) == [30 << 1, 31 << 1]
    assert all(len(v) == 1 for v in found.values())
    assert found[30 << 1][0] == TileId(LEAF, TX + 1, TY)  # centroid east of the border
    td = decode_tile(tiles[TileId(LEAF, TX + 1, TY).key])
    b30 = td.buildings[0]
    assert b30.rings[0][:, 0].min() < 0  # footprint not clipped
    assert b30.levels == 3 and b30.height_cm == 950 and b30.flags & BuildingFlags.LEVELS_INFERRED
    td0 = decode_tile(tiles[TileId(LEAF, TX, TY).key])
    assert td0.buildings[0].flags & BuildingFlags.LANDMARK
    assert st["buildings_assigned"] == 2


def test_area_clipped_into_four_tiles(unit_build) -> None:
    _, ex, tiles, _, _ = unit_build
    parts = []
    for k, blob in tiles.items():
        td = decode_tile(blob)
        for a in td.areas:
            assert a.kind == AreaKind.WATER_LAKE and a.flags & AreaFlags.CLIPPED_BY_TILE
            assert td.name(a.name_ref).default == "Unit Pokhari"
            p = a.vertices.astype(float)[a.indices.reshape(-1, 3)]
            cross = (p[:, 1, 0] - p[:, 0, 0]) * (p[:, 2, 1] - p[:, 0, 1]) - (p[:, 1, 1] - p[:, 0, 1]) * (p[:, 2, 0] - p[:, 0, 0])
            assert (cross > 0).all()  # degenerate triangles are dropped after quantisation
            parts.append(0.5 * cross.sum() / 1e4)
    assert len(parts) == 4
    lake = tiling._to_game_geoms([ex.areas[0].polygon])[0]
    assert sum(parts) == pytest.approx(lake.area, rel=1e-3)


def test_pois_places_and_lines(unit_build) -> None:
    _, _, tiles, _, _ = unit_build
    td = decode_tile(tiles[TileId(LEAF, TX, TY).key])
    poi = next(p for p in td.pois if p.kind == PoiKind.TEMPLE_HINDU)
    assert poi.search_id == 7 and poi.flags & PoiFlags.HAS_ELE and poi.ele_dm == 13100 and poi.importance == 128
    assert (poi.x_cm, poi.z_cm) == (10000, 10000)
    place = next(p for p in td.pois if p.kind == PlaceKind.NEIGHBOURHOOD + tiling.PLACE_KIND_OFFSET)
    assert td.name(place.name_ref).default == "Unittol"
    ln = next(ln for ln in td.lines if ln.osm_way_id == 20)
    assert ln.flags & 4 and ln.flags & 2 and not ln.flags & 1 and not ln.flags & 8  # intermittent, next context
    tdn = decode_tile(tiles[TileId(LEAF, TX, TY + 1).key])
    assert any(ln.osm_way_id == 20 and ln.flags & 0b0001 for ln in tdn.lines)


def test_pack_roundtrip(unit_build, tmp_path) -> None:
    _, _, tiles, _, _ = unit_build
    info = write_pack(tmp_path / "unit.ghpk", "unit", 1, tiles)
    assert info["tile_count"] == len(tiles)


def test_piece_cm_rounding_onto_cut_point() -> None:
    """A vertex within 0.5 cm of a tile border must not leave a zero-length segment or a
    context point equal to its cut point (DATA_FORMATS 1.4); both sides keep one tangent."""
    a, b = TileId(10, 0, 0), TileId(10, 1, 0)
    s = a.size
    full = np.array([(s - 5.0, 5.0), (s + 0.004, 5.0), (s + 5.0, 5.0), (s + 10.0, 5.0)])
    (pa,) = geom.clip_polyline(full, a.bounds)
    pts_a, prev_a, next_a = tiling._piece_cm(a, pa, full)
    sc = int(round(s * 100))
    assert not prev_a and next_a
    assert pts_a.tolist() == [[sc - 500, 500], [sc, 500], [sc + 500, 500]]
    (pb,) = geom.clip_polyline(full, b.bounds)
    pts_b, prev_b, next_b = tiling._piece_cm(b, pb, full)
    assert prev_b and not next_b
    assert pts_b.tolist() == [[-500, 500], [0, 500], [500, 500], [1000, 500]]
    # A's context point is B's first point after the cut; B's context is A's last point before it.
    assert pts_a[-1].tolist() == [pts_b[2][0] + sc, pts_b[2][1]]
    assert pts_b[0].tolist() == [pts_a[0][0] - sc, pts_a[0][1]]
    # a piece that rounds to a single point is dropped
    tiny = np.array([(s - 0.002, 5.0), (s + 5.0, 5.0)])
    (pt,) = geom.clip_polyline(tiny, a.bounds)
    assert tiling._piece_cm(a, pt, tiny) is None
    # no further vertex that differs: the context flag is cleared
    short = np.array([(s - 5.0, 5.0), (s + 0.004, 5.0)])
    (ps,) = geom.clip_polyline(short, a.bounds)
    pts_s, _, next_s = tiling._piece_cm(a, ps, short)
    assert not next_s and pts_s.tolist() == [[sc - 500, 500], [sc, 500]]


def test_piece_cm_grazing_the_border_clears_context() -> None:
    """A way that dips < 0.5 cm outside the tile and comes back: no context point inside the tile."""
    a = TileId(10, 0, 0)
    full = np.array([(5.0, 5.0), (-0.004, 6.0), (5.0, 7.0)])
    pieces = geom.clip_polyline(full, a.bounds)
    assert len(pieces) == 2
    out = [tiling._piece_cm(a, p, full) for p in pieces]
    for pts, prev, nxt in out:
        for has, ctx in ((prev, pts[0]), (nxt, pts[-1])):
            if has:
                assert not (0 < ctx[0] < a.size * 100 and 0 < ctx[1] < a.size * 100), pts
