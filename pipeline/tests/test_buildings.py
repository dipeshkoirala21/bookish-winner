"""Tests for buildings.py: archetype rules, levels/height/roof inference, zones."""

from __future__ import annotations

import copy

import numpy as np
import pytest

from ghumante_pipeline import buildings as B
from ghumante_pipeline.binio import encode_varint, fnv1a32
from ghumante_pipeline.buildings import (
    BuildingContext,
    building_seed,
    classify,
    footprint_area_m2,
    infer_buildings,
    load_archetype_zones,
    make_contexts,
)
from ghumante_pipeline.model import (
    BuildingArchetype as A,
    BuildingFeature,
    BuildingFlags,
    BuildingUse as U,
    RoofMaterial as RM,
    RoofShape as RS,
    WallMaterial as WM,
)
from ghumante_pipeline.tile_format import building_seed as tile_building_seed

# A ~10 m x 11 m square near Kathmandu (lon/lat degrees).
_SQ = np.array([[85.3, 27.7], [85.3001, 27.7], [85.3001, 27.7001], [85.3, 27.7001]])
_TINY = np.array([[85.3, 27.7], [85.30002, 27.7], [85.30002, 27.70002], [85.3, 27.70002]])  # ~2 m x 2 m


def bf(osm_id=1, use=U.UNKNOWN, raw="yes", religion=None, osm_type="w", outer=_SQ, **kw) -> BuildingFeature:
    return BuildingFeature(osm_type, osm_id, outer.copy(), use=use, building_raw=raw, religion=religion, **kw)


HILL = BuildingContext(0, 1400.0, "default")
CORE = BuildingContext(3, 1340.0, "default")
TOWN = BuildingContext(2, 1340.0, "default")
NEWAR = BuildingContext(3, 1340.0, "newar_core")
TERAI = BuildingContext(1, 90.0, "terai")
LOWLAND = BuildingContext(0, 200.0, "default")  # below 300 m but outside the terai polygon
SHERPA = BuildingContext(0, 3440.0, "sherpa")
TRANS = BuildingContext(0, 3800.0, "trans_himalaya")
HIGH = BuildingContext(0, 3200.0, "default")


# ---------------------------------------------------------------------------
# Zones
# ---------------------------------------------------------------------------
@pytest.fixture(scope="module")
def zones():
    return load_archetype_zones()


@pytest.mark.parametrize("name,lon,lat,zone", [
    ("Bhaktapur Durbar Square", 85.4281, 27.6722, "newar_core"),
    ("Kathmandu Durbar Square", 85.3073, 27.7045, "newar_core"),
    ("Patan Durbar Square", 85.3253, 27.6727, "newar_core"),
    ("Bandipur bazaar", 84.4086, 27.9378, "newar_core"),
    ("Namche Bazaar", 86.7140, 27.8050, "sherpa"),
    ("Lukla", 86.7314, 27.6869, "sherpa"),
    ("Lo Manthang", 83.9565, 29.1828, "trans_himalaya"),
    ("Manang", 84.0170, 28.6660, "trans_himalaya"),
    ("Thamel", 85.3100, 27.7154, "default"),
    ("Pokhara Lakeside", 83.9590, 28.2096, "default"),
    ("Birgunj", 84.8770, 27.0120, "terai"),
    ("Biratnagar", 87.2718, 26.4525, "terai"),
])
def test_zone_lookup(zones, name, lon, lat, zone):
    assert zones.zone_of(lon, lat) == zone, name


def test_zone_lookup_vectorised(zones):
    lon = np.array([85.4281, 86.7140, 83.9565, 85.3100, 84.8770])
    lat = np.array([27.6722, 27.8050, 29.1828, 27.7154, 27.0120])
    out = zones.zone_of(lon, lat)
    assert isinstance(out, np.ndarray) and out.shape == (5,)
    assert out.tolist() == ["newar_core", "sherpa", "trans_himalaya", "default", "terai"]
    assert isinstance(zones.zone_of(85.31, 27.715), str)


def test_zone_file_validation(tmp_path):
    bad = tmp_path / "z.yaml"
    bad.write_text("zones:\n  - id: atlantis\n    polygons: [[[0,0],[1,0],[1,1]]]\n")
    with pytest.raises(ValueError, match="unknown zone"):
        load_archetype_zones(bad)
    bad.write_text("zones:\n  - id: terai\n    polygons: [[[0,0],[1,1],[1,0],[0,1]]]\n")
    with pytest.raises(ValueError, match="invalid polygon"):
        load_archetype_zones(bad)


def test_later_zone_wins(tmp_path):
    p = tmp_path / "z.yaml"
    p.write_text("zones:\n"
                 "  - id: terai\n    polygons: [[[0,0],[10,0],[10,10],[0,10]]]\n"
                 "  - id: newar_core\n    polygons: [[[4,4],[6,4],[6,6],[4,6]]]\n")
    z = load_archetype_zones(p)
    assert z.zone_of(5, 5) == "newar_core" and z.zone_of(1, 1) == "terai" and z.zone_of(20, 20) == "default"


def test_make_contexts(zones):
    b1 = bf(1, outer=_SQ + np.array([85.4281 - 85.3, 27.6722 - 27.7]))
    b2 = bf(2)
    ctx = make_contexts([b1, b2], zones, [1400.0, 1300.0], [3, 1])
    assert ctx == [BuildingContext(3, 1400.0, "newar_core"), BuildingContext(1, 1300.0, "default")]
    with pytest.raises(ValueError):
        make_contexts([b1], zones, [1.0, 2.0], [0, 0])


# ---------------------------------------------------------------------------
# Archetype rules
# ---------------------------------------------------------------------------
@pytest.mark.parametrize("b,ctx,arch", [
    (bf(raw="temple", use=U.RELIGIOUS, religion="hindu"), NEWAR, A.TEMPLE_PAGODA),
    (bf(raw="temple", use=U.RELIGIOUS, religion="hindu"), HILL, A.TEMPLE_PAGODA),
    (bf(raw="temple", use=U.RELIGIOUS, religion="hindu"), TERAI, A.TEMPLE_SHIKHARA),
    (bf(raw="temple", use=U.RELIGIOUS), LOWLAND, A.TEMPLE_SHIKHARA),  # elevation < 300 m is Terai
    (bf(raw="stupa", use=U.RELIGIOUS, religion="buddhist"), NEWAR, A.STUPA),
    (bf(raw="chorten", use=U.RELIGIOUS), SHERPA, A.CHORTEN),
    (bf(raw="monastery", use=U.RELIGIOUS), SHERPA, A.GOMPA),
    (bf(raw="gompa", use=U.RELIGIOUS), HILL, A.GOMPA),
    (bf(use=U.RELIGIOUS, religion="buddhist"), SHERPA, A.GOMPA),
    (bf(use=U.RELIGIOUS, religion="Buddhist"), NEWAR, A.TEMPLE_PAGODA),
    (bf(raw="temple", use=U.RELIGIOUS, religion="buddhist"), HILL, A.GOMPA),
    (bf(raw="shrine", use=U.RELIGIOUS, religion="hindu"), HILL, A.SHRINE),
    (bf(raw="mosque", use=U.RELIGIOUS), TERAI, A.MOSQUE),
    (bf(use=U.RELIGIOUS, religion="muslim"), TERAI, A.MOSQUE),
    (bf(raw="church", use=U.RELIGIOUS), HILL, A.CHURCH),
    (bf(raw="chapel", use=U.RELIGIOUS), HILL, A.CHURCH),
    (bf(use=U.RELIGIOUS, religion="christian"), CORE, A.CHURCH),
    (bf(use=U.RELIGIOUS, religion="hindu"), HILL, A.TEMPLE_PAGODA),
    (bf(use=U.RELIGIOUS), HILL, A.SHRINE),  # no usable religion
    (bf(use=U.RELIGIOUS, religion="Baun"), HILL, A.SHRINE),  # junk religion (real data)
    (bf(use=U.INDUSTRIAL), NEWAR, A.INDUSTRIAL),
    (bf(use=U.EDUCATION), NEWAR, A.INSTITUTIONAL),
    (bf(use=U.HEALTH), HILL, A.INSTITUTIONAL),
    (bf(use=U.PUBLIC), TERAI, A.INSTITUTIONAL),
    (bf(use=U.OFFICE), CORE, A.INSTITUTIONAL),
    (bf(use=U.HUT), SHERPA, A.HUT),
    (bf(use=U.GREENHOUSE), HILL, A.GREENHOUSE),
    (bf(use=U.HOTEL), SHERPA, A.TEAHOUSE),
    (bf(use=U.HOTEL), HIGH, A.TEAHOUSE),
    (bf(use=U.HOTEL), CORE, A.MODERN_URBAN),
    (bf(use=U.HOTEL), NEWAR, A.NEWAR),
    (bf(), NEWAR, A.NEWAR),
    (bf(use=U.HOUSE), SHERPA, A.SHERPA_HIMALAYAN),
    (bf(), TRANS, A.TRANS_HIMALAYAN),
    (bf(), TERAI, A.TERAI),
    (bf(), BuildingContext(2, 80.0, "terai"), A.MODERN_URBAN),
    (bf(), BuildingContext(3, 1300.0, "terai"), A.MODERN_URBAN),
    (bf(), LOWLAND, A.TERAI),
    (bf(), CORE, A.MODERN_URBAN),
    (bf(), TOWN, A.MODERN_URBAN),
    (bf(), HILL, A.HILL_VILLAGE),
    (bf(), BuildingContext(1, 2999.0, "default"), A.HILL_VILLAGE),
    (bf(), HIGH, A.GENERIC),
    (bf(), BuildingContext(0, float("nan"), "default"), A.GENERIC),
])
def test_classify(b, ctx, arch):
    assert classify(b, ctx) == arch


def test_religious_beats_use_and_zone():
    # A temple tagged as a public building in a Newar core is still a temple.
    assert classify(bf(raw="temple", use=U.PUBLIC, religion="hindu"), NEWAR) == A.TEMPLE_PAGODA


# ---------------------------------------------------------------------------
# Inference
# ---------------------------------------------------------------------------
def test_seed_matches_tile_format():
    for t, i in (("w", 1), ("w", 123456789), ("r", 42)):
        b = bf(i, osm_type=t)
        ref = (i << 1) | (t == "r")
        assert building_seed(b) == tile_building_seed(ref) == fnv1a32(encode_varint(ref))


def test_tagged_values_kept():
    b = bf(levels=5.0, height_m=17.0, roof_shape=RS.GABLED, roof_material=RM.SLATE, wall_material=WM.STONE)
    stats = infer_buildings([b], [HILL])
    assert (b.levels, b.height_m) == (5.0, 17.0)
    assert (b.roof_shape, b.roof_material, b.wall_material) == (RS.GABLED, RM.SLATE, WM.STONE)
    assert b.flags == BuildingFlags.HEIGHT_TAGGED | BuildingFlags.ROOF_TAGGED
    assert stats["levels_tagged"] == 1 and stats["levels_inferred"] == 0 and stats["height_tagged"] == 1


def test_levels_tagged_height_derived():
    b = bf(levels=4.0)
    infer_buildings([b], [CORE])
    assert b.levels == 4.0 and not b.flags & BuildingFlags.LEVELS_INFERRED
    assert b.height_m == pytest.approx(4 * 3.0 + B.roof_allowance(b.roof_shape))
    assert not b.flags & BuildingFlags.HEIGHT_TAGGED


def test_levels_from_tagged_height():
    b = bf(height_m=12.5, roof_shape=RS.FLAT)
    infer_buildings([b], [CORE])
    assert b.levels == 4.0 and b.flags & BuildingFlags.LEVELS_INFERRED and b.flags & BuildingFlags.HEIGHT_TAGGED
    assert b.height_m == 12.5


def test_min_height_added():
    b = bf(levels=2.0, min_height_m=3.0, roof_shape=RS.FLAT)
    infer_buildings([b], [CORE])
    assert b.height_m == pytest.approx(3.0 + 6.0 + 0.5)


def test_tiny_footprint_one_level():
    b = bf(outer=_TINY)
    assert footprint_area_m2(b) < 12.0
    stats = infer_buildings([b], [NEWAR])
    assert b.levels == 1.0 and b.flags & BuildingFlags.LEVELS_INFERRED and stats["tiny_footprints"] == 1


def test_footprint_area():
    a = footprint_area_m2(bf())
    assert 100 < a < 125  # about 9.9 m x 11.1 m
    hole = np.array([[85.30004, 27.70004], [85.30004, 27.70006], [85.30006, 27.70006],
                                     [85.30006, 27.70004]])
    assert footprint_area_m2(bf(holes=[hole])) < a


@pytest.mark.parametrize("ctx,arch,allowed", [
    (CORE, A.MODERN_URBAN, {3, 4, 5, 6}),
    (TOWN, A.MODERN_URBAN, {2, 3, 4}),
    (NEWAR, A.NEWAR, {3, 4, 5}),
    (HILL, A.HILL_VILLAGE, {1, 2}),
    (TERAI, A.TERAI, {1, 2}),
    (SHERPA, A.SHERPA_HIMALAYAN, {2}),
    (TRANS, A.TRANS_HIMALAYAN, {1, 2}),
])
def test_levels_distributions(ctx, arch, allowed):
    bs = [bf(i) for i in range(1, 401)]
    infer_buildings(bs, [ctx] * len(bs))
    levels = [b.levels for b in bs]
    assert all(b.archetype == arch for b in bs)
    assert set(levels) <= allowed
    assert len(set(levels)) == len(allowed)  # every allowed value is used
    assert all(b.flags & BuildingFlags.LEVELS_INFERRED for b in bs)


def test_modern_urban_core_mode_is_four():
    bs = [bf(i) for i in range(1, 2001)]
    infer_buildings(bs, [CORE] * len(bs))
    vals, counts = np.unique([b.levels for b in bs], return_counts=True)
    assert vals[np.argmax(counts)] == 4


def test_teahouse_levels():
    bs = [bf(i, use=U.HOTEL) for i in range(1, 200)]
    infer_buildings(bs, [SHERPA] * len(bs))
    assert {b.levels for b in bs} == {2.0, 3.0}


def test_temples_and_stupas_get_their_roofs():
    t = bf(1, raw="temple", use=U.RELIGIOUS, religion="hindu")
    s = bf(2, raw="temple", use=U.RELIGIOUS, religion="hindu")
    st = bf(3, raw="stupa", use=U.RELIGIOUS)
    infer_buildings([t, s, st], [NEWAR, TERAI, NEWAR])
    assert (t.archetype, t.roof_shape) == (A.TEMPLE_PAGODA, RS.PAGODA)
    assert (s.archetype, s.roof_shape) == (A.TEMPLE_SHIKHARA, RS.SHIKHARA)
    assert (st.archetype, st.roof_shape) == (A.STUPA, RS.DOME)
    assert t.height_m == pytest.approx(t.levels * 3.0 + 4.0)


def test_roof_variety_and_flat_roof_materials():
    bs = [bf(i) for i in range(1, 600)]
    infer_buildings(bs, [HILL] * len(bs))
    shapes = {b.roof_shape for b in bs}
    assert shapes == {RS.GABLED, RS.HIPPED, RS.FLAT}
    for b in bs:
        assert b.roof_material != RM.UNKNOWN and b.wall_material != WM.UNKNOWN
        if b.roof_shape == RS.FLAT:
            assert b.roof_material not in (RM.METAL, RM.TILES, RM.SLATE, RM.THATCH)
    trans = [bf(i) for i in range(1, 50)]
    infer_buildings(trans, [TRANS] * len(trans))
    assert {b.roof_shape for b in trans} == {RS.FLAT} and {b.roof_material for b in trans} <= {RM.MUD, RM.WOOD}


def test_one_level_uses():
    bs = [bf(1, use=U.ROOF), bf(2, use=U.GARAGE), bf(3, use=U.CONSTRUCTION)]
    infer_buildings(bs, [CORE] * 3)
    assert [b.levels for b in bs] == [1.0, 1.0, 1.0]


def test_determinism_and_order_independence():
    bs = [bf(i, osm_type="r" if i % 7 == 0 else "w") for i in range(1, 300)]
    ctxs = [(CORE, NEWAR, HILL, TERAI)[i % 4] for i in range(len(bs))]
    a = copy.deepcopy(bs)
    b = copy.deepcopy(bs)
    sa = infer_buildings(a, ctxs)
    perm = list(range(len(bs)))[::-1]
    b_perm = [b[i] for i in perm]
    sb = infer_buildings(b_perm, [ctxs[i] for i in perm])
    assert sa == sb
    for x, y in zip(a, b):
        assert (x.archetype, x.levels, x.height_m, x.roof_shape, x.roof_material, x.wall_material, x.flags) == \
               (y.archetype, y.levels, y.height_m, y.roof_shape, y.roof_material, y.wall_material, y.flags)


def test_seed_depends_on_relation_bit():
    w = bf(10)
    r = bf(10, osm_type="r")
    assert building_seed(w) != building_seed(r)


def test_stats_shape():
    bs = [bf(1), bf(2, levels=3.0), bf(3, use=U.RELIGIOUS, raw="temple")]
    stats = infer_buildings(bs, [HILL, HILL, NEWAR])
    assert stats["buildings"] == 3
    assert stats["archetypes"] == {"HILL_VILLAGE": 2, "TEMPLE_PAGODA": 1}
    assert stats["zones"] == {"default": 2, "newar_core": 1}
    assert stats["levels_tagged"] == 1 and stats["levels_inferred"] == 2


def test_length_mismatch():
    with pytest.raises(ValueError):
        infer_buildings([bf()], [])


def test_extract_then_infer(fixtures_dir):
    from ghumante_pipeline.osm_extract import extract_region
    ex = extract_region(fixtures_dir / "mini.osm", (85.300, 27.700, 85.320, 27.720))
    zones = load_archetype_zones()
    ctx = make_contexts(ex.buildings, zones, [1340.0] * len(ex.buildings), [3] * len(ex.buildings))
    infer_buildings(ex.buildings, ctx)
    by_id = {b.osm_id: b for b in ex.buildings}
    # Kasthamandap sits in the Kathmandu Newar core polygon.
    assert by_id[230].archetype == A.TEMPLE_PAGODA and by_id[230].levels == 3.0
    assert by_id[200].levels == 3.0 and by_id[200].height_m == 9.0
    assert by_id[300].archetype == A.NEWAR  # commercial building inside the core
    assert all(b.height_m and b.height_m > 0 and b.levels >= 1 for b in ex.buildings)
