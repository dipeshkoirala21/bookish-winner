"""Tests for search_index: entry selection, keys, the GHSI format and lookup."""

from __future__ import annotations

import json
import random
import struct
from pathlib import Path

import pytest
from shapely.geometry import box

from ghumante_pipeline import projection
from ghumante_pipeline.model import AdminArea, NameRec, PlaceFeature, PlaceKind, PoiFeature, PoiFlags, PoiKind
from ghumante_pipeline.search_index import (
    ENTRY_SIZE,
    FLAG_DISCOVERABLE,
    FLAG_LANDMARK,
    FLAG_TRANSPORT_HUB,
    HEADER_SIZE,
    PLACE_KIND_OFFSET,
    SCORE_SCALE,
    SearchEntry,
    SearchIndex,
    build_entries,
    decode_index,
    encode_index,
    folded_primary,
    keys_for,
    kind_group,
    kind_priority,
    match_score_prefix,
    osa_distance,
    peak_weight,
    place_importance,
    poi_importance,
    population_bonus,
    read_index,
    search_ids,
    sort_entries,
    write_index,
)
from ghumante_pipeline.translit import fold

PIPELINE_ROOT = Path(__file__).resolve().parents[1]
P = PLACE_KIND_OFFSET


# ---------------------------------------------------------------------------
# Synthetic world
# ---------------------------------------------------------------------------
def _place(oid, kind, lon, lat, default, en="", ne="", alt=(), population=None, osm_type="n"):
    return PlaceFeature(osm_type, oid, kind, lon, lat, NameRec(default, en, ne, tuple(alt)), population=population)


def _poi(oid, kind, lon, lat, default, en="", ne="", alt=(), osm_type="n", ele=None, tags=None, flags=0):
    return PoiFeature(osm_type, oid, kind, lon, lat, NameRec(default, en, ne, tuple(alt)), ele_m=ele,
                      tags=dict(tags or {}), flags=PoiFlags(flags))


def _admin(oid, level, name, poly, admin_level=6):
    return AdminArea(oid, level, admin_level, name, poly)


KTM = (85.3205817, 27.708317)
BAGMATI = _admin(4583010, PlaceKind.PROVINCE, NameRec("Bagmati Province", "Bagmati Province", "बागमती प्रदेश"),
                 box(84.5, 27.2, 86.5, 28.5), 4)
KTM_DISTRICT = _admin(4583246, PlaceKind.DISTRICT, NameRec("Kathmandu", "Kathmandu", "काठमाडौं"),
                      box(85.18, 27.68, 85.58, 27.83))
LTP_DISTRICT = _admin(4583244, PlaceKind.DISTRICT, NameRec("Lalitpur", "Lalitpur", "ललितपुर"),
                      box(85.18, 27.40, 85.58, 27.68))
KTM_METRO = _admin(6115366, PlaceKind.LOCAL_LEVEL,
                   NameRec("काठमाडौं महानगरपालिका", "Kathmandu Metropolitan City", "काठमाडौं महानगरपालिका"),
                   box(85.28, 27.68, 85.36, 27.75), 7)
WARD = _admin(7000001, PlaceKind.WARD, NameRec("Ward 3", "Ward 3", ""), box(85.30, 27.70, 85.31, 27.71), 9)
ADMIN = [BAGMATI, KTM_DISTRICT, LTP_DISTRICT, KTM_METRO, WARD]

PLACES = [
    _place(67157058, PlaceKind.CITY, *KTM, "काठमाडौं", "Kathmandu", "काठमाडौं", alt=("काठमांडौ",),
           population=975000),
    _place(11, PlaceKind.HAMLET, 84.6, 28.3, "Kathmandu", "Kathmandu"),  # far away, same name
    _place(1349697740, PlaceKind.NEIGHBOURHOOD, 85.3127015, 27.7166578, "Thamel", "Thamel"),
    _place(3318253390, PlaceKind.SUBURB, 85.3617188, 27.7205626, "बौद्ध", "Baudha", "बौद्ध"),
    _place(723880699, PlaceKind.CITY, 85.3166069, 27.6765635, "ललितपुर", "Lalitpur", "ललितपुर",
           alt=("Patan",), population=254000),
    _place(21, PlaceKind.CITY, 83.9856, 28.2096, "Pokhara", "Pokhara", "पोखरा", population=518000),
    _place(22, PlaceKind.NONE, 85.31, 27.70, "toukhel 3"),  # free-text place value
    _place(23, PlaceKind.HAMLET, 85.40, 27.75, "", "", ""),  # unnamed
]
POIS = [
    _poi(56688296, PoiKind.STUPA, 85.362039, 27.721493, "Boudhanāth Stupa", ne="बौद्धनाथ स्तूप", osm_type="w",
         tags={"wikidata": "Q1046836"}),
    _poi(201223707, PoiKind.STUPA, 85.290396, 27.714929, "Swayambhunath", ne="स्वयंभुनाथ", osm_type="w"),
    _poi(913170315, PoiKind.TEMPLE_HINDU, 85.3485, 27.7105, "पशुपतीनाथ", "Pashupatinath Temple", osm_type="w"),
    _poi(118505122, PoiKind.AIRPORT, 85.3591, 27.6966, "त्रिभुवन अन्तर्राष्ट्रिय विमानस्थल",
         "Tribhuvan International Airport", "त्रिभुवन अन्तर्राष्ट्रिय विमानस्थल", osm_type="w"),
    _poi(568545483, PoiKind.RESTAURANT, 85.3164, 27.7093, "काठमाडौं भान्सा", "Kathmandu Kitchen"),
    _poi(943246322, PoiKind.HOTEL, 85.3102, 27.7173, "Kathmandu Grand Hotel"),
    _poi(1783313633, PoiKind.BANK, 85.3151, 27.6749, "bank of kathmandu"),
    _poi(31, PoiKind.VIEWPOINT, 85.30, 27.70, ""),  # unnamed
    _poi(32, PoiKind.PEAK, 86.925, 27.988, "Mount Everest", ne="सगरमाथा", ele=8849.0),
]
LANDMARKS = {56688296: "boudhanath", 201223707: "swayambhunath", 1349697740: "thamel"}


@pytest.fixture(scope="module")
def entries() -> list[SearchEntry]:
    return build_entries(PLACES, POIS, ADMIN, LANDMARKS)


@pytest.fixture(scope="module")
def index(entries) -> SearchIndex:
    return SearchIndex.from_entries(entries)


def _by_name(entries, name) -> SearchEntry:
    found = [e for e in entries if name in (e.name.default, e.name.en, e.name.ne)]
    assert len(found) == 1, (name, found)
    return found[0]


def _top(index, q, limit=10):
    res = index.search(q, limit)
    assert res, q
    return res[0][1]


# ---------------------------------------------------------------------------
# Importance, flags and groups
# ---------------------------------------------------------------------------
def test_population_bonus():
    assert [population_bonus(p) for p in (None, 0, -5, 999, 1000, 10_000, 100_000, 1_000_000, 10**9)] == [
        0, 0, 0, 0, 0, 10, 20, 30, 40]


def test_peak_weight_scales_with_elevation():
    assert peak_weight(None) == 70
    assert peak_weight(float("nan")) == 70
    assert peak_weight(500.0) == 70
    assert peak_weight(8849.0) == 220
    assert peak_weight(9500.0) == 220
    assert peak_weight(2000.0) < peak_weight(5000.0) < peak_weight(8000.0)


def test_place_and_poi_importance():
    assert place_importance(PlaceKind.CITY) == 250
    assert place_importance(PlaceKind.TOWN) == 200
    assert place_importance(PlaceKind.PROVINCE) == 240
    assert place_importance(PlaceKind.DISTRICT) == 220
    assert place_importance(PlaceKind.CITY, 975000) == 255  # clamped
    assert place_importance(PlaceKind.VILLAGE, 10_000) == 120
    assert place_importance(PlaceKind.HAMLET, landmark=True) == 100
    assert poi_importance(PoiKind.STUPA) == 150
    assert poi_importance(PoiKind.STUPA, landmark=True, wikidata=True) == 210
    assert poi_importance(PoiKind.PEAK, 8849.0, wikidata=True) == 240
    assert poi_importance(PoiKind.SHOP) == 50  # unlisted -> default weight


def test_kind_groups_and_priority():
    assert kind_group(P + PlaceKind.CITY) == kind_group(P + PlaceKind.HAMLET) == kind_group(P + PlaceKind.SUBURB)
    assert kind_group(P + PlaceKind.SQUARE) == kind_group(PoiKind.HERITAGE_SQUARE) == kind_group(PoiKind.STUPA)
    assert kind_group(P + PlaceKind.DISTRICT) != kind_group(P + PlaceKind.LOCAL_LEVEL)
    assert kind_group(PoiKind.PEAK) == kind_group(PoiKind.LAKE) != kind_group(PoiKind.AIRPORT)
    assert kind_priority(P + PlaceKind.COUNTRY) < kind_priority(P + PlaceKind.WARD) < kind_priority(PoiKind.NONE)
    assert kind_priority(PoiKind.TEMPLE_HINDU) < kind_priority(PoiKind.BANK)


# ---------------------------------------------------------------------------
# build_entries
# ---------------------------------------------------------------------------
def test_build_entries_selection(entries):
    names = {e.display_name for e in entries}
    # excluded: business POIs (ADR-010), lodging, unnamed, place=NONE, wards
    for gone in ("Kathmandu Kitchen", "Kathmandu Grand Hotel", "bank of kathmandu", "toukhel 3", "Ward 3"):
        assert gone not in names
    assert {"Kathmandu", "Thamel", "Baudha", "Lalitpur", "Pokhara", "Boudhanāth Stupa", "Swayambhunath",
            "Pashupatinath Temple", "Tribhuvan International Airport", "Mount Everest", "Bagmati Province",
            "Kathmandu Metropolitan City"} <= names
    kinds = sorted(e.kind for e in entries)
    assert P + PlaceKind.PROVINCE in kinds and P + PlaceKind.DISTRICT in kinds and P + PlaceKind.LOCAL_LEVEL in kinds
    assert P + PlaceKind.WARD not in kinds
    assert entries == sort_entries(entries)  # returned in file order


def test_entries_use_game_coordinates(entries):
    ktm = [e for e in entries if e.kind == P + PlaceKind.CITY and e.name.en == "Kathmandu"][0]
    x, z = projection.lonlat_to_game(*KTM)
    assert ktm.x == pytest.approx(float(x)) and ktm.z == pytest.approx(float(z))
    assert (ktm.lon, ktm.lat) == KTM
    assert ktm.osm_ref == (67157058 << 2) | 0
    stupa = _by_name(entries, "Swayambhunath")
    assert stupa.osm_ref == (201223707 << 2) | 1


def test_admin_entries_at_point_on_surface(entries):
    metro = _by_name(entries, "Kathmandu Metropolitan City")
    assert metro.kind == P + PlaceKind.LOCAL_LEVEL
    assert KTM_METRO.polygon.contains(KTM_METRO.polygon.point_on_surface())
    pt = KTM_METRO.polygon.point_on_surface()
    assert (metro.lon, metro.lat) == pytest.approx((pt.x, pt.y))
    assert metro.osm_ref == (6115366 << 2) | 2
    assert metro.importance == 180


def test_district_and_province_assignment(entries):
    ktm = [e for e in entries if e.kind == P + PlaceKind.CITY and e.name.en == "Kathmandu"][0]
    assert ktm.district.en == "Kathmandu" and ktm.province.en == "Bagmati Province"
    lalitpur = [e for e in entries if e.kind == P + PlaceKind.CITY and e.name.en == "Lalitpur"][0]
    assert lalitpur.district.en == "Lalitpur" and lalitpur.province.en == "Bagmati Province"
    district = [e for e in entries if e.kind == P + PlaceKind.DISTRICT and e.name.en == "Kathmandu"][0]
    assert district.district is None and district.province.en == "Bagmati Province"
    province = _by_name(entries, "Bagmati Province")
    assert province.district is None and province.province is None
    everest = _by_name(entries, "Mount Everest")  # outside every admin polygon
    assert everest.district is None and everest.province is None
    hamlet = [e for e in entries if e.kind == P + PlaceKind.HAMLET][0]  # 84.6 E: in the province box only
    assert hamlet.district is None and hamlet.province.en == "Bagmati Province"


def test_overlapping_districts_pick_smallest_relation_id():
    a = _admin(200, PlaceKind.DISTRICT, NameRec("B-district", "B-district"), box(0, 0, 2, 2))
    b = _admin(100, PlaceKind.DISTRICT, NameRec("A-district", "A-district"), box(1, 1, 3, 3))
    p = _place(1, PlaceKind.VILLAGE, 1.5, 1.5, "Overlap")
    edge = _place(2, PlaceKind.VILLAGE, 2.0, 0.5, "Edge")  # on a's boundary: inclusive
    es = build_entries([p, edge], [], [a, b])
    by = {e.name.default: e for e in es}
    assert by["Overlap"].district.default == "A-district"
    assert by["Edge"].district.default == "B-district"


def test_flags_and_importance_in_entries(entries):
    bouda = _by_name(entries, "Boudhanāth Stupa")
    assert bouda.flags == FLAG_DISCOVERABLE | FLAG_LANDMARK
    assert bouda.importance == 150 + 40 + 20  # landmark + wikidata
    swy = _by_name(entries, "Swayambhunath")
    assert swy.flags == FLAG_DISCOVERABLE | FLAG_LANDMARK and swy.importance == 190
    airport = _by_name(entries, "Tribhuvan International Airport")
    assert airport.flags == FLAG_TRANSPORT_HUB and airport.importance == 200
    thamel = _by_name(entries, "Thamel")
    assert thamel.flags == FLAG_LANDMARK and thamel.importance == 160
    everest = _by_name(entries, "Mount Everest")
    assert everest.flags == FLAG_DISCOVERABLE and everest.importance == 220


def test_poi_landmark_flag_and_discoverable_flag_from_extract():
    q = _poi(5, PoiKind.MARKETPLACE, 85.3, 27.7, "Asan Bazaar", flags=PoiFlags.LANDMARK | PoiFlags.DISCOVERABLE)
    (e,) = build_entries([], [q])
    assert e.flags == FLAG_LANDMARK | FLAG_DISCOVERABLE
    assert e.importance == 110 + 40


def test_names_that_fold_to_nothing_are_dropped():
    tib = _poi(6, PoiKind.GOMPA, 85.3, 27.7, "ཀ་ཏ་མན་ཏུ")
    alt_only = _poi(7, PoiKind.GOMPA, 85.3, 27.7, "", alt=("Something",))
    assert build_entries([], [tib, alt_only]) == []


def test_dedupe_same_name_same_group_within_300m():
    lon, lat = 85.33, 27.70
    near = 0.002  # ~200 m of longitude at 27.7 N
    far = 0.004  # ~395 m
    pois = [
        _poi(100, PoiKind.TEMPLE_HINDU, lon, lat, "Bhimsen Mandir"),
        _poi(101, PoiKind.PLACE_OF_WORSHIP, lon + near, lat, "Bhimsen Mandir"),  # same group, near: dropped
        _poi(102, PoiKind.TEMPLE_HINDU, lon - far, lat, "Bhimsen Mandir"),  # far: kept
        _poi(103, PoiKind.BUS_STATION, lon, lat, "Bhimsen Mandir"),  # other group: kept
        _poi(104, PoiKind.TEMPLE_HINDU, lon, lat, "Bhimsen Mandir", osm_type="w"),  # same importance: higher ref
    ]
    es = build_entries([], pois)
    refs = sorted(e.osm_ref >> 2 for e in es)
    assert refs == [100, 102, 103]


def test_dedupe_keeps_highest_importance():
    lon, lat = 85.33, 27.70
    places = [
        _place(1, PlaceKind.HAMLET, lon, lat, "Sundarijal"),
        _place(2, PlaceKind.VILLAGE, lon + 0.001, lat, "Sundarijal"),
    ]
    (e,) = build_entries(places, [])
    assert e.kind == P + PlaceKind.VILLAGE and e.osm_ref >> 2 == 2


def test_dedupe_place_node_inside_admin_polygon():
    node = _place(9, PlaceKind.DISTRICT, 85.20, 27.80, "Kathmandu", "Kathmandu")  # far from point_on_surface
    es = build_entries([node], [], [KTM_DISTRICT])
    assert len([e for e in es if e.kind == P + PlaceKind.DISTRICT]) == 1


def test_build_entries_is_deterministic_under_input_order(entries):
    rng = random.Random(3)
    for _ in range(5):
        places, pois, admin = list(PLACES), list(POIS), list(ADMIN)
        rng.shuffle(places)
        rng.shuffle(pois)
        rng.shuffle(admin)
        again = build_entries(places, pois, admin, LANDMARKS)
        assert encode_index(again) == encode_index(entries)


def test_build_entries_empty():
    assert build_entries([], [], [], {}) == []
    assert build_entries([], [], None, None) == []


# ---------------------------------------------------------------------------
# keys_for
# ---------------------------------------------------------------------------
def _entry(default="", en="", ne="", alt=(), kind=PoiKind.ATTRACTION, imp=100, ref=1, x=0.0, z=0.0, flags=0):
    return SearchEntry(NameRec(default, en, ne, tuple(alt)), int(kind), imp, flags, x, z, 85.3, 27.7, ref)


def test_keys_for_variants_and_romanisation():
    e = _entry("काठमाडौं", "Kathmandu", "काठमाडौं", alt=("Kantipur",))
    assert keys_for(e) == ["kantipur", "katamadaun", "katmandu"]


def test_keys_for_word_suffixes_and_stop_list():
    assert keys_for(_entry("Patan Durbar Square")) == ["durbar square", "patan durbar square", "square"]
    # "temple" may not start a suffix; full names are always kept
    assert keys_for(_entry("Shree Pashupatinath Temple")) == ["pasupatinat temple", "sri pasupatinat temple"]
    assert keys_for(_entry("Temple")) == ["temple"]
    # short words (< 3 characters after folding) do not start suffixes
    assert keys_for(_entry("Ward No 3")) == ["bard no 3"]
    # stop words are matched after folding: मन्दिर -> "mandir", ताल -> "tal", "The" -> "te"
    assert keys_for(_entry("राम मन्दिर")) == ["ram mandir"]
    assert keys_for(_entry("Phewa Taal")) == ["peba tal"]
    assert keys_for(_entry("Garden of The Dreams")) == ["dreams", "garden op te dreams"]


def test_keys_for_devanagari_multiword():
    e = _entry("बौद्धनाथ स्तूप")
    assert keys_for(e) == ["baudanat stup", "stup"]


def test_keys_for_is_sorted_unique():
    e = _entry("Thamel", "Thamel", "ठमेल", alt=("THAMEL", "Thaamel"))
    assert keys_for(e) == ["tamel"]


def test_folded_primary():
    assert folded_primary(NameRec("", "Kathmandu", "काठमाडौं")) == "katmandu"
    assert folded_primary(NameRec("ཀ་ཏ", "", "काठमाडौं")) == "katamadaun"
    assert folded_primary(NameRec()) == ""


# ---------------------------------------------------------------------------
# Binary format
# ---------------------------------------------------------------------------
def test_header_and_layout(entries):
    data = encode_index(entries)
    magic, version, flags, n_entries, n_keys, names_off, entries_off, keys_off, reserved = struct.unpack_from(
        "<4sHHIIIIII", data, 0)
    assert (magic, version, flags, reserved) == (b"GHSI", 1, 0, 0)
    assert n_entries == len(entries)
    assert n_keys == sum(len(keys_for(e)) for e in entries)
    assert names_off == HEADER_SIZE
    assert keys_off - entries_off == n_entries * ENTRY_SIZE
    assert names_off < entries_off <= keys_off < len(data)


def test_round_trip(entries, tmp_path):
    path = tmp_path / "region.search.ghsi"
    stats = write_index(path, entries)
    data = path.read_bytes()
    assert data == encode_index(entries)
    idx = read_index(path)
    assert stats["entries"] == len(idx.entries) == len(entries)
    assert stats["keys"] == len(idx.keys)
    assert stats["names"] == len(idx.names)
    assert stats["bytes"] == len(data)
    assert stats["landmarks"] == 3 and stats["transport_hubs"] == 1
    for a, b in zip(entries, idx.entries):
        assert (b.name.default, b.name.en, b.name.ne) == (a.name.default, a.name.en, a.name.ne)
        assert b.name.alt == ()  # alt names live only in the keys
        assert (b.kind, b.importance, b.flags) == (a.kind, a.importance, a.flags)
        assert b.x == pytest.approx(a.x, abs=0.05) and b.z == pytest.approx(a.z, abs=0.05)
        assert b.lon == pytest.approx(a.lon, abs=1e-7) and b.lat == pytest.approx(a.lat, abs=1e-7)
        assert b.osm_ref == a.osm_ref & 0xFFFFFFFF
        for na, nb in ((a.district, b.district), (a.province, b.province)):
            assert (na is None) == (nb is None)
            if na is not None:
                assert (nb.default, nb.en, nb.ne) == (na.default, na.en, na.ne)
    # keys sorted by (key bytes, entry index) and pointing at the right entries
    pairs = list(zip(idx.keys, idx.key_entries))
    assert pairs == sorted(pairs)
    for k, i in pairs:
        assert k in keys_for(entries[i])
    # decoding and re-encoding the decoded entries reproduces the entry section
    assert SearchIndex.from_entries(entries).entries == idx.entries


def test_names_section_interns_admin_names_first(entries):
    idx = SearchIndex.from_entries(entries)
    admin_names = {(n.default, n.en, n.ne) for e in entries for n in (e.district, e.province) if n is not None}
    first = [(n.default, n.en, n.ne) for n in idx.names[:len(admin_names)]]
    assert set(first) == admin_names
    # identical triples are stored once
    triples = [(n.default, n.en, n.ne) for n in idx.names]
    assert len(triples) == len(set(triples))
    # the district entry "Kathmandu" shares its record with the district references
    district = [e for e in idx.entries if e.kind == P + PlaceKind.DISTRICT and e.name.en == "Kathmandu"][0]
    city = [e for e in idx.entries if e.kind == P + PlaceKind.CITY and e.name.en == "Kathmandu"][0]
    assert city.district is district.name


def test_entries_sorted_by_kind_then_folded_name(entries):
    idx = SearchIndex.from_entries(entries)
    keys = [(e.kind, folded_primary(e.name)) for e in idx.entries]
    assert keys == sorted(keys)


def test_encoding_is_deterministic(entries):
    rng = random.Random(11)
    ref = encode_index(entries)
    for _ in range(5):
        shuffled = list(entries)
        rng.shuffle(shuffled)
        assert encode_index(shuffled) == ref


def test_encode_empty_index():
    idx = decode_index(encode_index([]))
    assert idx.entries == [] and idx.keys == [] and idx.names == []
    assert idx.search("kathmandu") == []


def test_decode_rejects_bad_input(entries):
    data = encode_index(entries)
    with pytest.raises(ValueError, match="magic"):
        decode_index(b"XXXX" + data[4:])
    with pytest.raises(ValueError, match="version"):
        decode_index(data[:4] + struct.pack("<H", 2) + data[6:])
    with pytest.raises(ValueError):
        decode_index(data[:20])
    with pytest.raises((ValueError, EOFError)):
        decode_index(data[:-3])
    with pytest.raises(ValueError, match="trailing"):
        decode_index(data + b"\x00")
    # swap the first two keys' order by rebuilding the keys section unsorted
    idx = decode_index(data)
    keys_off = struct.unpack_from("<I", data, 24)[0]
    from ghumante_pipeline.binio import Writer
    w = Writer()
    pairs = list(zip(idx.keys, idx.key_entries))
    pairs[0], pairs[-1] = pairs[-1], pairs[0]
    for k, i in pairs:
        w.str(k).varint(i)
    with pytest.raises(ValueError, match="sorted"):
        decode_index(data[:keys_off] + w.bytes())


def test_encode_rejects_out_of_range_coordinates():
    e = _entry("Far", x=3e8)
    with pytest.raises(OverflowError):
        encode_index([e])


def test_search_ids(entries):
    ids = search_ids(entries)
    idx = SearchIndex.from_entries(entries)
    for ref, sid in ids.items():
        assert idx.entries[sid - 1].osm_ref == ref & 0xFFFFFFFF
    assert ids[(56688296 << 2) | 1] >= 1


# ---------------------------------------------------------------------------
# Lookup
# ---------------------------------------------------------------------------
@pytest.mark.parametrize("a,b,d", [
    ("", "", 0), ("abc", "abc", 0), ("abc", "abd", 1), ("abc", "ab", 1), ("ab", "abc", 1),
    ("pokra", "pokara", 1), ("tamel", "tmael", 1), ("ca", "abc", 3), ("abcd", "badc", 2), ("katmandu", "kxtmxndu", 2),
])
def test_osa_distance(a, b, d):
    assert osa_distance(a, b, 10) == d
    assert osa_distance(b, a, 10) == d


def test_osa_distance_cutoff():
    assert osa_distance("abcdefgh", "zzzzzzzz", 2) == 3
    assert osa_distance("abc", "abcdef", 2) == 3  # length difference alone
    assert osa_distance("pokra", "pokara", 1) == 1


def test_match_score_prefix():
    assert match_score_prefix(0) == 1000
    assert match_score_prefix(1) == 890
    assert match_score_prefix(30) == 600
    assert match_score_prefix(80) == 100
    assert match_score_prefix(500) == 100


@pytest.mark.parametrize("q", ["kathmandu", "kathmandoo", "Kathmandu", "KATHMANDU", "काठमाडौं", " kathmandu! "])
def test_kathmandu_city_first(index, q):
    top = _top(index, q)
    assert top.kind == P + PlaceKind.CITY and top.name.en == "Kathmandu"


def test_kathmandu_city_outranks_district_metro_and_hamlet(index):
    res = index.search("kathmandu", 10)
    kinds = [e.kind for _, e in res]
    assert kinds[0] == P + PlaceKind.CITY
    assert P + PlaceKind.DISTRICT in kinds and P + PlaceKind.HAMLET in kinds and P + PlaceKind.LOCAL_LEVEL in kinds
    assert kinds.index(P + PlaceKind.DISTRICT) < kinds.index(P + PlaceKind.HAMLET)
    # business names never show up
    assert all("Kitchen" not in e.display_name and "Hotel" not in e.display_name for _, e in res)


def test_boudha_finds_boudhanath(index):
    res = index.search("boudha", 5)
    names = [e.display_name for _, e in res]
    assert "Boudhanāth Stupa" in names
    assert names[0] == "Baudha"  # the exact-match suburb comes first
    assert _top(index, "boudhanath").display_name == "Boudhanāth Stupa"
    assert _top(index, "बौद्धनाथ").display_name == "Boudhanāth Stupa"
    assert _top(index, "Bouddhanath stupa").display_name == "Boudhanāth Stupa"


def test_swaya_prefix_finds_swayambhunath(index):
    assert _top(index, "swaya").display_name == "Swayambhunath"
    assert _top(index, "swoyambhu").display_name == "Swayambhunath"
    assert _top(index, "स्वयम्भू").display_name == "Swayambhunath"


def test_devanagari_query_finds_latin_only_entry(index):
    top = _top(index, "ठमेल")
    assert top.name.default == "Thamel" and top.name.ne == ""
    score, _ = index.search("ठमेल", 1)[0]
    assert score == pytest.approx(0.7 + 0.3 * 160 / 255)


def test_fuzzy_typo_finds_pokhara(index):
    res = index.search("pokhra", 3)
    assert res[0][1].display_name == "Pokhara"
    assert res[0][0] == pytest.approx(0.65 * 0.7 + 0.3 * 255 / 255)


def test_alt_name_and_suffix_matches(index):
    assert _top(index, "patan").display_name == "Lalitpur"  # alt_name=Patan
    assert _top(index, "pashupatinath").display_name == "Pashupatinath Temple"
    assert _top(index, "international airport").display_name == "Tribhuvan International Airport"
    assert _top(index, "sagarmatha").display_name == "Mount Everest"
    assert _top(index, "everest").display_name == "Mount Everest"  # suffix "everest" of "Mount Everest"


def test_importance_breaks_ties():
    es = [
        _entry("Bhimsen", kind=P + PlaceKind.HAMLET, imp=60, ref=4),
        _entry("Bhimsen", kind=P + PlaceKind.CITY, imp=250, ref=8),
    ]
    idx = SearchIndex.from_entries(es)
    res = idx.search("bhimsen")
    assert [e.kind for _, e in res] == [P + PlaceKind.CITY, P + PlaceKind.HAMLET]
    assert res[0][0] == pytest.approx(0.7 + 0.3 * 250 / 255)
    assert res[1][0] == pytest.approx(0.7 + 0.3 * 60 / 255)


def test_equal_scores_break_by_kind_priority_then_entry_index():
    es = [
        _entry("Bhimsen", kind=PoiKind.TEMPLE_HINDU, imp=110, ref=1 << 2),
        _entry("Bhimsen", kind=P + PlaceKind.VILLAGE, imp=110, ref=9 << 2),
        _entry("Bhimsen", kind=P + PlaceKind.VILLAGE, imp=110, ref=3 << 2),
    ]
    idx = SearchIndex.from_entries(es)
    res = idx.rank("bhimsen")
    assert len({s for s, _ in res}) == 1
    got = [(idx.entries[i].kind, idx.entries[i].osm_ref) for _, i in res]
    assert got == [(P + PlaceKind.VILLAGE, 3 << 2), (P + PlaceKind.VILLAGE, 9 << 2), (PoiKind.TEMPLE_HINDU, 1 << 2)]


def test_exact_beats_prefix_beats_fuzzy():
    es = [
        _entry("Tamel", imp=0, ref=1),
        _entry("Tamelko", imp=0, ref=2),  # prefix, extra 2
        _entry("Tsmel", imp=0, ref=3),  # fuzzy, d = 1
    ]
    idx = SearchIndex.from_entries(es)
    res = idx.search("tamel")
    assert [(round(s, 6), e.name.default) for s, e in res] == [
        (0.7, "Tamel"), (round(0.88 * 0.7, 6), "Tamelko"), (round(0.65 * 0.7, 6), "Tsmel")]


def test_fuzzy_rules():
    idx = SearchIndex.from_entries([_entry("Tamel", ref=1), _entry("Katmandu", ref=2)])
    assert idx.search("tmael")[0][1].name.default == "Tamel"  # transposition
    assert idx.search("ramel") == []  # first character must match
    assert idx.search("tsm") == []  # 3 characters: no fuzzy, no prefix
    assert [e.name.default for _, e in idx.search("tam")] == ["Tamel"]  # prefix still works
    # 8 characters allow distance 2 but not 3
    assert idx.search("kxtmxndu")[0][1].name.default == "Katmandu"
    assert idx.search("kxxmxndu") == []


def test_limit_and_empty_queries(index):
    assert len(index.search("k", 3)) == 3
    assert len(index.search("k", 1)) == 1
    assert index.search("k", 0) == []
    assert index.search("k", -1) == []
    assert index.search("") == []
    assert index.search("   ") == []
    assert index.search("!!!") == []
    assert index.search("zzzzqqqq") == []


def test_scores_are_in_unit_range_and_sorted(index):
    for q in ("k", "kathmandu", "boudha", "pa", "stupa"):
        res = index.search(q, 50)
        scores = [s for s, _ in res]
        assert scores == sorted(scores, reverse=True)
        assert all(0.0 < s <= 1.0 for s in scores)
        assert len({id(e) for _, e in res}) == len(res)  # one result per entry


def test_rank_integer_scores(index):
    ranked = index.rank("kathmandu", 3)
    s, i = ranked[0]
    assert s == 1785 * 1000 + 3000 * index.entries[i].importance
    assert index.search("kathmandu", 3)[0][0] == s / SCORE_SCALE


def test_search_from_file_equals_in_memory(entries, tmp_path):
    path = tmp_path / "x.ghsi"
    write_index(path, entries)
    a, b = read_index(path), SearchIndex.from_entries(entries)
    for q in ("kathmandu", "boudha", "swaya", "ठमेल", "pokhra", "p"):
        assert a.rank(q, 20) == b.rank(q, 20)


# ---------------------------------------------------------------------------
# Real data: Kathmandu Valley straight from the PBF (independent of osm_extract)
# ---------------------------------------------------------------------------
_VALLEY = (85.18, 27.55, 85.58, 27.83)
_PLACE_VALUES = {
    "city": PlaceKind.CITY, "town": PlaceKind.TOWN, "village": PlaceKind.VILLAGE, "hamlet": PlaceKind.HAMLET,
    "suburb": PlaceKind.SUBURB, "neighbourhood": PlaceKind.NEIGHBOURHOOD, "quarter": PlaceKind.QUARTER,
    "locality": PlaceKind.LOCALITY, "square": PlaceKind.SQUARE, "isolated_dwelling": PlaceKind.ISOLATED_DWELLING,
    "farm": PlaceKind.FARM, "state": PlaceKind.PROVINCE,
}
_LANDMARK_KINDS = {
    "stupa": PoiKind.STUPA, "temple_pagoda": PoiKind.TEMPLE_HINDU, "heritage_square": PoiKind.HERITAGE_SQUARE,
    "tower": PoiKind.MONUMENT, "garden": PoiKind.PARK, "cable_car": PoiKind.CABLE_CAR_STATION,
    "viewpoint": PoiKind.VIEWPOINT, "airport": PoiKind.AIRPORT,
}


def _is_deva(s: str) -> bool:
    return any(0x0900 <= ord(c) <= 0x097F for c in s)


def _simple_name(tags: dict) -> NameRec | None:
    default = tags.get("name", "").strip()
    en = tags.get("name:en", "").strip() or tags.get("int_name", "").strip()
    if not en and default and not _is_deva(default):
        en = default
    ne = tags.get("name:ne", "").strip() or (default if _is_deva(default) else "")
    alt = tuple(v.strip() for k in ("alt_name", "old_name") for v in tags.get(k, "").split(";") if v.strip())
    n = NameRec(default, en, ne, alt)
    return None if n.is_empty() else n


def _religious_kind(tags: dict) -> PoiKind | None:
    if "stupa" in (tags.get("building"), tags.get("man_made"), tags.get("place_of_worship")):
        return PoiKind.STUPA
    if tags.get("amenity") != "place_of_worship":
        return None
    return {"hindu": PoiKind.TEMPLE_HINDU, "buddhist": PoiKind.GOMPA}.get(tags.get("religion", ""),
                                                                           PoiKind.PLACE_OF_WORSHIP)


def _scan_valley(pbf: Path):
    import osmium

    places: list[PlaceFeature] = []
    pois: list[PoiFeature] = []
    fp = osmium.FileProcessor(str(pbf), osmium.osm.NODE).with_filter(
        osmium.filter.KeyFilter("place", "amenity", "building", "man_made"))
    for o in fp:
        lon, lat = o.location.lon, o.location.lat
        if not (_VALLEY[0] <= lon <= _VALLEY[2] and _VALLEY[1] <= lat <= _VALLEY[3]):
            continue
        tags = dict(o.tags)
        name = _simple_name(tags)
        if name is None:
            continue
        pk = _PLACE_VALUES.get(tags.get("place", ""))
        if pk is not None:
            pop = tags.get("population", "")
            places.append(PlaceFeature("n", o.id, pk, lon, lat, name,
                                       population=int(pop) if pop.isdigit() else None))
            continue
        kind = _religious_kind(tags)
        if kind is not None:
            wd = {"wikidata": tags["wikidata"]} if "wikidata" in tags else {}
            pois.append(PoiFeature("n", o.id, kind, lon, lat, name, tags=wd))
    return places, pois


@pytest.mark.realdata
@pytest.mark.slow
def test_realdata_kathmandu_valley(nepal_pbf, tmp_path):
    places, pois = _scan_valley(nepal_pbf)
    assert len(places) > 500 and len(pois) > 500

    landmarks = json.loads((PIPELINE_ROOT / "config" / "landmarks.resolved.json").read_text(encoding="utf-8"))
    landmark_ids: dict[int, str] = {}
    seen = {p.osm_id for p in places} | {p.osm_id for p in pois}
    for lm in landmarks:
        if lm["region"] != "kathmandu_valley" or lm["status"] != "found":
            continue
        osm_type, osm_id = lm["osm"][0], int(lm["osm"][1:])
        landmark_ids[osm_id] = lm["id"]
        kind = _LANDMARK_KINDS.get(lm["kind"])
        if kind is None or osm_id in seen:
            continue
        pois.append(PoiFeature(osm_type, osm_id, kind, lm["lon"], lm["lat"],
                               NameRec(lm["name"] or "", lm.get("name_en") or "", lm.get("name_ne") or "")))

    entries = build_entries(places, pois, None, landmark_ids)
    stats = write_index(tmp_path / "kathmandu_valley.search.ghsi", entries)
    assert stats["entries"] > 1000 and stats["landmarks"] >= 10
    idx = read_index(tmp_path / "kathmandu_valley.search.ghsi")

    def top(q: str) -> SearchEntry:
        res = idx.search(q, 5)
        assert res, q
        return res[0][1]

    def folded_names(e: SearchEntry) -> list[str]:
        return [fold(v) for v in (e.name.default, e.name.en, e.name.ne) if v]

    boudha = top("Boudha")
    assert any(f.startswith("bauda") for f in folded_names(boudha)), boudha
    bn = top("boudhanath")
    assert bn.kind == PoiKind.STUPA and bn.flags & FLAG_LANDMARK, bn
    for q in ("Thamel", "ठमेल"):
        t = top(q)
        assert t.name.en == "Thamel" and t.kind == P + PlaceKind.NEIGHBOURHOOD, (q, t)
    patan = top("Patan")
    assert patan.name.en == "Lalitpur" or any("patan" in f for f in folded_names(patan)), patan
    ktm = top("Kathmandu")
    assert ktm.name.en == "Kathmandu" and ktm.kind == P + PlaceKind.CITY, ktm
    assert top("काठमाडौं").name.en == "Kathmandu"
    swy = top("swayambhunath")
    assert swy.flags & FLAG_LANDMARK and swy.kind == PoiKind.STUPA, swy
