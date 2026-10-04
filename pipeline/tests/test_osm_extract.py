"""Tests for osm_extract: the hand-made fixtures/mini.osm and, when present, the real Nepal PBF."""

from __future__ import annotations

import time

import numpy as np
import pytest
from shapely.geometry import Point, box

from ghumante_pipeline import tags as T
from ghumante_pipeline.config import load_regions
from ghumante_pipeline.model import (
    ALL_TRAVEL,
    AreaKind,
    BuildingFlags,
    BuildingUse,
    Extract,
    LineKind,
    PlaceKind,
    PoiFlags,
    PoiKind,
    RoadClass,
    RoofMaterial,
    RoofShape,
    SacScale,
    Travel,
    WallMaterial,
)
from ghumante_pipeline.osm_extract import (
    assemble_multipolygon,
    build_rings,
    buffered_bbox,
    extract_region,
    load_extract,
    save_extract,
    signed_area,
)

BBOX = (85.300, 27.700, 85.320, 27.720)


@pytest.fixture(scope="module")
def mini_path(fixtures_dir):
    return fixtures_dir / "mini.osm"


@pytest.fixture(scope="module")
def ex(mini_path) -> Extract:
    return extract_region(mini_path, BBOX, region_id="mini")


def _by_id(features, osm_id, osm_type=None):
    hits = [f for f in features if f.osm_id == osm_id and (osm_type is None or f.osm_type == osm_type)]
    assert len(hits) == 1, f"expected one feature {osm_type}{osm_id}, got {len(hits)}"
    return hits[0]


def _ids(features):
    return {(getattr(f, "osm_type", "w"), f.osm_id) for f in features}


# ---------------------------------------------------------------------------
# Roads
# ---------------------------------------------------------------------------
def test_road_set(ex):
    # 150 is far outside; 151 starts in the buffer and is kept whole; 153 lost a node.
    assert [r.osm_id for r in ex.roads] == [100, 101, 102, 103, 104, 105, 151, 153]


def test_tagged_residential(ex):
    r = _by_id(ex.roads, 100)
    assert r.cls == RoadClass.RESIDENTIAL and not r.is_link
    assert r.node_ids.dtype == np.int64 and r.node_ids.tolist() == [1, 2, 3]
    assert r.lonlat.shape == (3, 2) and r.lonlat.dtype == np.float64
    np.testing.assert_allclose(r.lonlat[0], (85.302, 27.702), atol=1e-9)
    assert r.surface_raw == "paving_stones"
    assert r.width_m == 5.0 and r.lanes == 2 and r.oneway == 0
    assert r.name.default == "Thamel Marg" and r.name.ne == "ठमेल मार्ग"
    assert r.access == ALL_TRAVEL and r.ref is None
    assert not (r.bridge or r.tunnel or r.ford) and r.layer == 0


def test_untagged_residential_shares_node(ex):
    r = _by_id(ex.roads, 101)
    assert r.cls == RoadClass.RESIDENTIAL
    assert r.surface_raw is None and r.width_m is None and r.lanes == 0 and r.name is None
    assert r.node_ids[0] == _by_id(ex.roads, 100).node_ids[-1] == 3


def test_oneway_primary(ex):
    r = _by_id(ex.roads, 102)
    assert r.cls == RoadClass.PRIMARY and r.oneway == 1 and r.ref == "H02"
    assert r.surface_raw == "asphalt" and r.lanes == 4


def test_bridge(ex):
    r = _by_id(ex.roads, 103)
    assert r.cls == RoadClass.SECONDARY and r.bridge and r.layer == 1 and not r.tunnel


def test_path_with_sac_scale(ex):
    r = _by_id(ex.roads, 104)
    assert r.cls == RoadClass.PATH
    assert r.sac_scale == SacScale.MOUNTAIN_HIKING and not r.sac_inferred
    assert r.trail_visibility == 2
    assert r.access == Travel.FOOT | Travel.BICYCLE | Travel.HORSE


def test_track(ex):
    r = _by_id(ex.roads, 105)
    assert r.cls == RoadClass.TRACK and r.tracktype == 3 and r.smoothness == "bad" and r.ford
    assert not r.access & Travel.CAR


def test_road_from_buffer_kept_whole(ex):
    r = _by_id(ex.roads, 151)
    assert len(r.lonlat) == 2 and r.lonlat[1, 0] == pytest.approx(85.45)


def test_way_with_missing_node_keeps_aligned_ids(ex):
    r = _by_id(ex.roads, 153)
    assert r.node_ids.tolist() == [2, 4]
    assert len(r.lonlat) == 2
    assert ex.stats["ways_missing_node_locations"] == 1


# ---------------------------------------------------------------------------
# Buildings
# ---------------------------------------------------------------------------
def test_building_set(ex):
    # 250 is outside, 252 is a building:part only; 251 is in the buffer.
    assert [(b.osm_type, b.osm_id) for b in ex.buildings] == [("w", 200), ("w", 201), ("w", 230), ("w", 251),
                                                              ("r", 300)]


def test_buildings_sorted_by_osm_ref(ex):
    refs = [(b.osm_id << 1) | (b.osm_type == "r") for b in ex.buildings]
    assert refs == sorted(refs)


def test_tagged_building(ex):
    b = _by_id(ex.buildings, 200)
    assert b.use == BuildingUse.HOUSE and b.building_raw == "house"
    assert b.levels == 3.0  # G+2
    assert b.height_m == 9.0
    assert b.roof_shape == RoofShape.HIPPED and b.roof_material == RoofMaterial.METAL
    assert b.wall_material == WallMaterial.BRICK
    assert b.flags == BuildingFlags.HEIGHT_TAGGED | BuildingFlags.ROOF_TAGGED
    # Drawn clockwise in the file; must come out counter-clockwise and unclosed.
    assert b.outer.shape == (4, 2) and signed_area(b.outer) > 0
    assert not np.array_equal(b.outer[0], b.outer[-1])
    assert b.holes == []


def test_untagged_building(ex):
    b = _by_id(ex.buildings, 201)
    assert b.use == BuildingUse.UNKNOWN and b.building_raw == "yes"
    assert b.levels is None and b.height_m is None and b.min_height_m is None
    assert b.roof_shape == RoofShape.UNKNOWN and b.flags == BuildingFlags(0)
    assert b.name is None and b.religion is None


def test_multipolygon_building_with_hole(ex):
    b = _by_id(ex.buildings, 300, "r")
    assert b.use == BuildingUse.COMMERCIAL and b.levels == 4.0
    assert b.name.default == "Courtyard Mall"
    assert signed_area(b.outer) > 0
    assert len(b.holes) == 1 and signed_area(b.holes[0]) < 0
    assert abs(signed_area(b.outer)) == pytest.approx(0.0005 ** 2, rel=1e-6)
    assert abs(signed_area(b.holes[0])) == pytest.approx(0.0001 ** 2, rel=1e-6)


def test_temple_building_and_poi(ex):
    b = _by_id(ex.buildings, 230)
    assert b.use == BuildingUse.RELIGIOUS and b.building_raw == "temple" and b.religion == "hindu"
    assert b.name.ne == "काष्ठमण्डप" and b.levels == 3.0
    p = _by_id(ex.pois, 230, "w")
    assert p.kind == PoiKind.TEMPLE_HINDU and p.flags & PoiFlags.SACRED
    assert (p.lon, p.lat) == pytest.approx((85.3051, 27.7046))
    assert p.tags["religion"] == "hindu"
    # A building never doubles as an area (no RELIGIOUS compound from the footprint).
    assert ("w", 230) not in _ids(ex.areas)


# ---------------------------------------------------------------------------
# POIs and places
# ---------------------------------------------------------------------------
def test_poi_set(ex):
    assert [(p.osm_type, p.osm_id, p.kind) for p in ex.pois] == [
        ("n", 60, PoiKind.STUPA), ("n", 61, PoiKind.PEAK), ("w", 230, PoiKind.TEMPLE_HINDU),
        ("w", 260, PoiKind.PARK), ("r", 301, PoiKind.LAKE),
    ]


def test_stupa_node(ex):
    p = _by_id(ex.pois, 60, "n")
    assert p.name.default == "Kathesimbhu Stupa" and p.flags == PoiFlags.SACRED
    assert (p.lon, p.lat) == (85.309, 27.712) and p.importance == 0.0


def test_peak_elevation(ex):
    p = _by_id(ex.pois, 61, "n")
    assert p.ele_m == 8848.0 and p.flags & PoiFlags.HAS_ELE


def test_lake_poi_avoids_island(ex):
    p = _by_id(ex.pois, 301, "r")
    lake = _by_id(ex.areas, 301, "r").polygon
    assert lake.contains(Point(p.lon, p.lat))


def test_places(ex):
    assert [(p.osm_type, p.osm_id) for p in ex.places] == [("n", 62), ("n", 63)]
    city = _by_id(ex.places, 62)
    assert city.kind == PlaceKind.CITY and city.name.ne == "काठमाडौं" and city.population == 1003285
    village = _by_id(ex.places, 63)
    assert village.kind == PlaceKind.VILLAGE and village.name.en == "Thamel Gaun" and village.name.ne == ""
    assert village.population is None


# ---------------------------------------------------------------------------
# Areas, lines, admin
# ---------------------------------------------------------------------------
def test_lake_multipolygon(ex):
    a = _by_id(ex.areas, 301, "r")
    assert a.kind == AreaKind.WATER_LAKE and a.name.default == "Rani Pokhari"
    g = a.polygon
    assert g.is_valid and g.geom_type == "Polygon" and len(g.interiors) == 1
    assert g.area == pytest.approx(0.004 ** 2 - 0.001 ** 2, rel=1e-6)
    assert not g.contains(Point(85.315, 27.705))  # the island
    assert a.tags == {"natural": "water", "water": "lake"}


def test_area_set(ex):
    assert [(a.osm_type, a.osm_id, a.kind) for a in ex.areas] == [("w", 260, AreaKind.PARK),
                                                                 ("r", 301, AreaKind.WATER_LAKE)]
    park = _by_id(ex.areas, 260)
    assert park.polygon.is_valid and park.polygon.area > 0


def test_river_line_kept_whole(ex):
    assert [ln.osm_id for ln in ex.lines] == [240]
    ln = ex.lines[0]
    assert ln.kind == LineKind.RIVER and ln.name.default == "Bishnumati" and len(ln.lonlat) == 3
    assert ln.tags == {"waterway": "river"}


def test_admin(ex):
    assert [(a.admin_level, a.osm_id) for a in ex.admin] == [(4, 403), (6, 400)]
    d = _by_id(ex.admin, 400)
    assert d.level == PlaceKind.DISTRICT and d.name.default == "Kathmandu" and d.name.ne == "काठमाडौं"
    # Assembled from two member ways, extending well beyond the region.
    assert d.polygon.is_valid and d.polygon.bounds == pytest.approx((85.25, 27.65, 85.40, 27.78))
    assert _by_id(ex.admin, 403).level == PlaceKind.PROVINCE


def test_admin_levels_parameter(mini_path):
    ex9 = extract_region(mini_path, BBOX, admin_levels=(9,))
    assert [(a.admin_level, a.osm_id, a.level) for a in ex9.admin] == [(9, 402, PlaceKind.WARD)]


def test_excluded_features(ex):
    every = set()
    for lst in (ex.roads, ex.buildings, ex.pois, ex.places, ex.areas, ex.lines):
        every |= _ids(lst)
    # Far-away road, building, peak, village and lake; a building:part; an untagged node.
    for far in (("w", 150), ("w", 250), ("w", 252), ("n", 72), ("n", 73), ("r", 302), ("w", 223), ("n", 104)):
        assert far not in every


def test_zero_buffer_drops_buffer_zone(mini_path):
    ex0 = extract_region(mini_path, BBOX, buffer_m=0.0)
    assert 151 not in {r.osm_id for r in ex0.roads}
    assert 251 not in {b.osm_id for b in ex0.buildings}
    assert 100 in {r.osm_id for r in ex0.roads}


def test_buffered_bbox():
    b = buffered_bbox(BBOX, 1500.0)
    assert b[1] == pytest.approx(27.700 - 1500 / 111320.0)
    assert b[0] < 85.300 - 1500 / 111320.0  # longitude degrees are shorter


# ---------------------------------------------------------------------------
# Stats, determinism, cache
# ---------------------------------------------------------------------------
def test_stats(ex):
    s = ex.stats
    assert s["counts"] == {"roads": 8, "buildings": 5, "pois": 5, "places": 2, "areas": 2, "lines": 1, "admin": 2}
    assert s["region"] == "mini" and s["road_classes"]["RESIDENTIAL"] == 2
    assert set(s["timing_s"]) == {"pass_relations", "pass_nodes_ways", "assemble_relations", "total"}
    assert s["unknown_values"] == {}


def test_unknown_values_recorded_per_extract(tmp_path):
    osm = tmp_path / "junk.osm"
    osm.write_text(
        '<osm version="0.6">'
        '<node id="1" lat="27.705" lon="85.305"/><node id="2" lat="27.706" lon="85.306"/>'
        '<way id="1"><nd ref="1"/><nd ref="2"/><tag k="highway" v="residential"/>'
        '<tag k="lanes" v="lots"/></way></osm>', encoding="utf-8")
    ex1 = extract_region(osm, BBOX)
    assert ex1.stats["unknown_values"] == {"lanes": [("lots", 1)]}
    # Counted per extract, not accumulated across runs.
    ex2 = extract_region(osm, BBOX)
    assert ex2.stats["unknown_values"] == {"lanes": [("lots", 1)]}
    assert T.unknown_values["lanes"]["lots"] >= 2


def test_deterministic_and_round_trip(mini_path, ex, tmp_path):
    p1, p2 = tmp_path / "a.pkl.gz", tmp_path / "b.pkl.gz"
    ex2 = extract_region(mini_path, BBOX, region_id="mini")
    ex2.stats["timing_s"] = ex.stats["timing_s"]
    save_extract(ex, p1)
    save_extract(ex2, p2)
    assert p1.read_bytes() == p2.read_bytes()
    back = load_extract(p1)
    assert isinstance(back, Extract) and back.region == "mini"
    assert [r.osm_id for r in back.roads] == [r.osm_id for r in ex.roads]
    np.testing.assert_array_equal(back.buildings[-1].holes[0], ex.buildings[-1].holes[0])
    assert back.areas[1].polygon.equals(ex.areas[1].polygon)
    assert back.stats == ex.stats


def test_load_rejects_other_pickles(tmp_path):
    import gzip
    import pickle
    p = tmp_path / "x.pkl.gz"
    with gzip.open(p, "wb") as f:
        pickle.dump({"not": "an extract"}, f)
    with pytest.raises(TypeError):
        load_extract(p)


# ---------------------------------------------------------------------------
# Ring building and multipolygon assembly
# ---------------------------------------------------------------------------
def _seg(a, b, pts):
    return (a, b, np.asarray(pts, dtype=np.float64))


def test_build_rings_chains_and_reverses():
    sq = [(0, 0), (1, 0), (1, 1), (0, 1)]
    segs = [_seg(1, 2, [sq[0], sq[1]]), _seg(3, 2, [sq[2], sq[1]]),  # reversed piece
            _seg(3, 4, [sq[2], sq[3]]), _seg(4, 1, [sq[3], sq[0]])]
    rings, unclosed = build_rings(segs)
    assert unclosed == 0 and len(rings) == 1
    r = rings[0]
    assert len(r) == 5 and np.array_equal(r[0], r[-1])
    assert abs(signed_area(r[:-1])) == pytest.approx(1.0)


def test_build_rings_reports_open_chain():
    rings, unclosed = build_rings([_seg(1, 2, [(0, 0), (1, 0)]), _seg(2, 3, [(1, 0), (1, 1)])])
    assert rings == [] and unclosed == 2


def test_assemble_nested_island():
    outer = [(0, 0), (10, 0), (10, 10), (0, 10), (0, 0)]
    inner = [(2, 2), (8, 2), (8, 8), (2, 8), (2, 2)]
    island = [(4, 4), (6, 4), (6, 6), (4, 6), (4, 4)]
    g, unclosed = assemble_multipolygon([("outer", 1, 1, np.array(outer, float)),
                                         ("inner", 2, 2, np.array(inner, float)),
                                         ("outer", 3, 3, np.array(island, float))])
    assert unclosed == 0 and g.is_valid
    assert g.area == pytest.approx(100 - 36 + 4)
    assert g.contains(Point(5, 5)) and not g.contains(Point(3, 3))


def test_assemble_without_outer_is_none():
    g, _ = assemble_multipolygon([("inner", 1, 1, np.array([(0, 0), (1, 0), (1, 1), (0, 0)], float))])
    assert g is None


def test_assemble_makes_bowtie_valid():
    bow = [(0, 0), (2, 2), (2, 0), (0, 2), (0, 0)]
    g, _ = assemble_multipolygon([("outer", 1, 1, np.array(bow, float))])
    assert g is not None and g.is_valid and g.area == pytest.approx(2.0)


# ---------------------------------------------------------------------------
# Real data
# ---------------------------------------------------------------------------
@pytest.mark.realdata
@pytest.mark.slow
def test_thamel_real_extract(nepal_pbf, capsys):
    region = load_regions()["thamel_test"]
    t0 = time.perf_counter()
    ex = extract_region(nepal_pbf, region.bbox, buffer_m=region.bbox_buffer_m, region_id=region.id)
    elapsed = time.perf_counter() - t0
    with capsys.disabled():
        print(f"\nthamel_test extract: {elapsed:.1f} s, counts {ex.stats['counts']}, "
              f"timing {ex.stats['timing_s']}")
    c = ex.stats["counts"]
    assert c["roads"] > 200 and c["buildings"] > 2000 and c["pois"] > 100 and c["places"] > 10
    # Kathmandu Durbar Square: a heritage-square POI or the Kasthamandap temple near it.
    durbar = Point(85.3073, 27.7045).buffer(0.004)
    near = [p for p in ex.pois if durbar.contains(Point(p.lon, p.lat))
            and (p.kind == PoiKind.HERITAGE_SQUARE or (p.name and "Kasthamandap" in p.name.default))]
    assert near
    names = " | ".join(f"{a.name.default} {a.name.en}" for a in ex.admin if a.admin_level == 6)
    assert "Kathmandu" in names or "काठमाडौं" in names
    core = box(*region.bbox)
    assert all(a.polygon.intersects(core) for a in ex.admin)
    assert {a.admin_level for a in ex.admin} >= {4, 6, 7}
    for b in ex.buildings[:500]:
        assert signed_area(b.outer) > 0 and all(signed_area(h) < 0 for h in b.holes)
    assert all(a.polygon.is_valid for a in ex.areas)
