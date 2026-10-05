"""Tests for ghumante_pipeline.tags: OSM tag parsing, including real Nepal quirks."""

from __future__ import annotations

import random

import pytest

from ghumante_pipeline import tags as T
from ghumante_pipeline.model import (
    ALL_TRAVEL,
    MOTOR_TRAVEL,
    AreaKind,
    BuildingUse,
    LineKind,
    NameRec,
    PlaceKind,
    PoiKind,
    RoadClass,
    RoofMaterial,
    RoofShape,
    SacScale,
    Surface,
    Travel,
    WallMaterial,
)

F, B, M, C, J, BUS, H = (Travel.FOOT, Travel.BICYCLE, Travel.MOTORBIKE, Travel.CAR, Travel.JEEP, Travel.BUS,
                         Travel.HORSE)


@pytest.fixture(autouse=True)
def _clean_unknown():
    T.reset_unknown()
    yield
    T.reset_unknown()


# ---------------------------------------------------------------------------
# Road class
# ---------------------------------------------------------------------------
@pytest.mark.parametrize("value, expected", [
    ("motorway", (RoadClass.MOTORWAY, False)),
    ("trunk", (RoadClass.TRUNK, False)),
    ("primary", (RoadClass.PRIMARY, False)),
    ("secondary", (RoadClass.SECONDARY, False)),
    ("tertiary", (RoadClass.TERTIARY, False)),
    ("unclassified", (RoadClass.UNCLASSIFIED, False)),
    ("residential", (RoadClass.RESIDENTIAL, False)),
    ("living_street", (RoadClass.LIVING_STREET, False)),
    ("service", (RoadClass.SERVICE, False)),
    ("track", (RoadClass.TRACK, False)),
    ("road", (RoadClass.ROAD, False)),
    ("pedestrian", (RoadClass.PEDESTRIAN, False)),
    ("footway", (RoadClass.FOOTWAY, False)),
    ("path", (RoadClass.PATH, False)),
    ("steps", (RoadClass.STEPS, False)),
    ("cycleway", (RoadClass.CYCLEWAY, False)),
    ("bridleway", (RoadClass.BRIDLEWAY, False)),
    ("motorway_link", (RoadClass.MOTORWAY, True)),
    ("trunk_link", (RoadClass.TRUNK, True)),
    ("primary_link", (RoadClass.PRIMARY, True)),
    ("secondary_link", (RoadClass.SECONDARY, True)),
    ("tertiary_link", (RoadClass.TERTIARY, True)),
    ("Primary", (RoadClass.PRIMARY, False)),
    (" track ", (RoadClass.TRACK, False)),
    ("goreto", (RoadClass.PATH, False)),
    ("residential_link", (RoadClass.UNKNOWN, False)),
    ("bus_stop", (RoadClass.UNKNOWN, False)),
    ("construction", (RoadClass.UNKNOWN, False)),
    ("proposed", (RoadClass.UNKNOWN, False)),
    ("platform", (RoadClass.UNKNOWN, False)),
    ("raceway", (RoadClass.UNKNOWN, False)),
    ("street_lamp", (RoadClass.UNKNOWN, False)),
    ("crossing;bus_stop", (RoadClass.UNKNOWN, False)),
    ("Sindhuli BP Highway", (RoadClass.UNKNOWN, False)),
    ("", (RoadClass.UNKNOWN, False)),
])
def test_parse_road_class(value, expected):
    assert T.parse_road_class({"highway": value}) == expected


def test_parse_road_class_missing_and_unknown_recording():
    assert T.parse_road_class({}) == (RoadClass.UNKNOWN, False)
    T.parse_road_class({"highway": "bus_stop"})  # valid, not routable: not recorded
    T.parse_road_class({"highway": "Sindhuli BP Highway"})
    assert dict(T.unknown_values["highway"]) == {"Sindhuli BP Highway": 1}


# ---------------------------------------------------------------------------
# Surface
# ---------------------------------------------------------------------------
SURFACE_CASES = {
    Surface.ASPHALT: ["asphalt", "paved", "Blacktopped", "black_topped", "black topped", "blacktop", "bitumin",
                      "bitumen", "premix", "ottaseal", "chipseal", "tarmac", "metalled", "pitch", "asphalt:lanes",
                      "  ASPHALT  ", "कालोपत्रे"],
    Surface.CONCRETE: ["concrete", "concrete:plates", "concrete:lanes", "cement", "RCC", "cemented"],
    Surface.BRICK: ["paving_stones", "bricks", "brick", "paving_stones:30", "interlock", "Concrete_Block_paved"],
    Surface.COBBLE: ["sett", "cobblestone", "unhewn_cobblestone", "stone", "flagstones", "uneven_flagstones",
                     "cobblestone:flattened", "stone_steps"],
    Surface.GRAVEL: ["gravel", "fine_gravel", "fine-gravel", "pebblestone", "shingle", "gravelled"],
    Surface.COMPACTED: ["compacted", "partially_paved"],
    Surface.DIRT: ["unpaved", "dirt", "earth", "ground", "clay", "soil", "dryriver", "בלתי_סלול", "בלתי סלול",
                   "EarthenTE228", "possibly_unpaved", "looks_unpaved", "kachchi", "woodchips"],
    Surface.MUD: ["mud", "Muddy"],
    Surface.SAND: ["sand", "sandy"],
    Surface.GRASS: ["grass", "grass_paver", "artificial_turf"],
    Surface.ROCK: ["rock", "rocky", "rokcs", "moraine", "bare_rock"],
    Surface.SNOW_ICE: ["snow", "ice"],
    Surface.WOOD: ["wood", "wooden"],
    Surface.METAL: ["metal", "metal_grid", "steel", "Steel", "iron"],
}


@pytest.mark.parametrize("raw, expected", [(v, s) for s, vals in SURFACE_CASES.items() for v in vals])
def test_normalize_surface_synonyms(raw, expected):
    assert T.normalize_surface(raw) == expected
    assert "surface" not in T.unknown_values or raw not in T.unknown_values["surface"]


@pytest.mark.parametrize("raw, expected", [
    ("concrete;unpaved", Surface.CONCRETE),  # real multi-value: first recognised wins
    ("junk;gravel", Surface.GRAVEL),
    ("dirt/sand", Surface.DIRT),
    ("steps_and_unpaved", Surface.DIRT),
    ("ground; grass", Surface.DIRT),
])
def test_normalize_surface_multi_values(raw, expected):
    assert T.normalize_surface(raw) == expected


@pytest.mark.parametrize("raw", ["G", "g", "3.5", "500 m", "g\\", "gro'", "fg", "ghattekulo", "Cemantary",
                                 "track", "road", "stairs", "sdfghjkl;", "yes", "DigitalGlobe, 2015-05-03"])
def test_normalize_surface_junk_is_none_and_recorded(raw):
    assert T.normalize_surface(raw) is None
    assert T.unknown_values["surface"][raw.strip()] == 1


@pytest.mark.parametrize("raw", [None, "", "   ", ";"])
def test_normalize_surface_missing(raw):
    assert T.normalize_surface(raw) is None
    assert sum(T.unknown_values["surface"].values()) == 0


def test_unknown_report_is_sorted_and_reset_clears():
    for v in ["G", "3.5", "G", "500 m", "3.5", "G"]:
        T.normalize_surface(v)
    T.parse_place_kind({"place": "toukhel 3"})
    rep = T.unknown_report()
    assert list(rep) == ["place", "surface"]
    assert rep["surface"] == [("G", 3), ("3.5", 2), ("500 m", 1)]
    T.reset_unknown()
    assert T.unknown_report() == {}


def test_unknown_values_truncates_long_text():
    T.normalize_surface("x" * 500)
    (value,) = T.unknown_values["surface"]
    assert len(value) == 64


@pytest.mark.parametrize("raw, expected", [
    ("grade1", Surface.CONCRETE), ("grade2", Surface.GRAVEL), ("grade3", Surface.COMPACTED),
    ("grade4", Surface.DIRT), ("grade5", Surface.DIRT), ("grade3-5", Surface.COMPACTED), ("5", Surface.DIRT),
    ("Grade 2", Surface.GRAVEL), (2, Surface.GRAVEL), (5, Surface.DIRT), ("grade6", None), ("yes", None),
    (None, None), ("", None), (0, None), (7, None),
])
def test_surface_from_tracktype(raw, expected):
    assert T.surface_from_tracktype(raw) == expected


@pytest.mark.parametrize("raw, expected", [
    ("grade1", 1), ("grade3-5", 3), ("5", 5), ("grade 4", 4), (3, 3), ("grade10", 0), ("yes", 0), (None, 0),
])
def test_parse_tracktype(raw, expected):
    assert T.parse_tracktype(raw) == expected


def test_parse_tracktype_records_junk_only():
    T.parse_tracktype("yes")
    T.parse_tracktype("grade2")
    T.parse_tracktype(None)
    assert dict(T.unknown_values["tracktype"]) == {"yes": 1}


@pytest.mark.parametrize("raw, expected", [
    ("excellent", Surface.ASPHALT), ("good", Surface.ASPHALT), ("Smooth", Surface.ASPHALT),
    ("intermediate", Surface.COMPACTED), ("bad", Surface.GRAVEL), ("rough", Surface.GRAVEL),
    ("very_bad", Surface.DIRT), ("horrible", Surface.DIRT), ("off_road_wheels", Surface.DIRT),
    ("very_horrible", Surface.MUD), ("impassable", Surface.ROCK), ("very bad", Surface.DIRT),
    ("low", None), (None, None),
])
def test_surface_from_smoothness(raw, expected):
    assert T.surface_from_smoothness(raw) == expected


# ---------------------------------------------------------------------------
# Lengths, levels, elevation, lanes
# ---------------------------------------------------------------------------
@pytest.mark.parametrize("raw, expected", [
    ("5", 5.0), ("5m", 5.0), ("5 m", 5.0), ("5.5 meters", 5.5), ("5.5 metres", 5.5), ("3,5", 3.5),
    ("~4", 4.0), ("approx 4", 4.0), ("4-6", 5.0), ("4 - 6 m", 5.0), ("4m-6m", 5.0),
    ("12'", 12 * 0.3048), ("12 ft", 12 * 0.3048), ("12 feet", 12 * 0.3048), ("20 feets", 20 * 0.3048),
    ("12'6\"", 12 * 0.3048 + 6 * 0.0254), ("12’6”", 12 * 0.3048 + 6 * 0.0254), ("12 ft 6 in", 3.81),
    ("350 cm", 3.5), ("3.5 meters aprox", 3.5), ("3 approx", 3.0), ("7-8 meters (approx)", 7.5),
    ("4;5", 4.0), ("200", 200.0), ("०.५", 0.5), ("5 m.", 5.0), (".5", 0.5),
])
def test_parse_length_m(raw, expected):
    assert T.parse_length_m(raw) == pytest.approx(expected)


@pytest.mark.parametrize("raw", ["-2", "-2.5", "0", "201", "600m", "0.5 km", "1435", "unknown", "bato",
                                 "5 m wide", "34567890-", "4700 m hight"])
def test_parse_length_m_rejects(raw):
    assert T.parse_length_m(raw, key="width") is None
    assert T.unknown_values["width"][raw] == 1


def test_parse_length_m_missing_not_recorded():
    assert T.parse_length_m(None) is None
    assert T.parse_length_m("  ") is None
    assert sum(T.unknown_values["width"].values()) == 0


@pytest.mark.parametrize("raw, expected", [
    ("2", 2.0), ("2.5", 2.5), ("2;3", 3.0), ("G+2", 3.0), ("g + 2", 3.0), ("B+G+2", 3.0), ("B1+G+3", 4.0),
    ("G+2.5", 3.5), ("G", 1.0), ("3 floors", 3.0), ("3 floor", 3.0), ("3 storey", 3.0), ("3 storeys", 3.0),
    ("3 stories", 3.0), ("1 story", 1.0), ("2 levels", 2.0), ("2-3", 3.0), ("३", 3.0), ("३ तल्ला", 3.0),
    ("4 and half", 4.5), ("2 and a half", 2.5), ("upto 8", 8.0), ("2?", 2.0), ("80", 80.0), ("1", 1.0),
])
def test_parse_levels(raw, expected):
    assert T.parse_levels(raw) == pytest.approx(expected)


@pytest.mark.parametrize("raw", ["0", "-1", "81", "0.5", "commercial", "yes", "Nursery to 12", "7m", "B", "-"])
def test_parse_levels_rejects(raw):
    assert T.parse_levels(raw) is None
    assert T.unknown_values["building:levels"][raw] == 1


@pytest.mark.parametrize("raw, expected", [
    ("8848.86", 8848.86), ("8,848 m", 8848.0), ("8848m", 8848.0), ("8848 m", 8848.0), ("29032 ft", 8848.9536),
    ("29032'", 8848.9536), ("8848 masl", 8848.0), ("8848 m.a.s.l.", 8848.0), ("1350 amsl", 1350.0),
    ("~1350", 1350.0), ("-20", -20.0), ("1,350.5", 1350.5), ("१३५०", 1350.0),
])
def test_parse_ele(raw, expected):
    assert T.parse_ele(raw) == pytest.approx(expected)


@pytest.mark.parametrize("raw", ["0", "9500", "-600", "high", "29032 ft;x;", "1.234.5"])
def test_parse_ele_rejects(raw):
    if raw == "29032 ft;x;":  # first ';' part is fine
        assert T.parse_ele(raw) == pytest.approx(8848.9536)
        return
    assert T.parse_ele(raw) is None
    assert T.unknown_values["ele"][raw] == 1


@pytest.mark.parametrize("raw, expected", [
    ("2", 2), ("1", 1), ("1.5", 1), ("2;3", 2), ("1-2", 1), ("4 lanes", 4), ("20", 20), ("0", 0), ("21", 0),
    ("two", 0), (None, 0), ("", 0),
])
def test_parse_lanes(raw, expected):
    assert T.parse_lanes(raw) == expected


@pytest.mark.parametrize("tags, cls, expected", [
    ({"oneway": "yes"}, RoadClass.RESIDENTIAL, 1),
    ({"oneway": "true"}, RoadClass.RESIDENTIAL, 1),
    ({"oneway": "1"}, RoadClass.RESIDENTIAL, 1),
    ({"oneway": "-1"}, RoadClass.RESIDENTIAL, -1),
    ({"oneway": "reverse"}, RoadClass.RESIDENTIAL, -1),
    ({"oneway": "no"}, RoadClass.RESIDENTIAL, 0),
    ({"oneway": "alternating"}, RoadClass.PRIMARY, 0),
    ({}, RoadClass.RESIDENTIAL, 0),
    ({}, RoadClass.MOTORWAY, 1),
    ({"highway": "motorway_link"}, RoadClass.MOTORWAY, 1),
    ({"oneway": "no"}, RoadClass.MOTORWAY, 0),
    ({"junction": "roundabout"}, RoadClass.PRIMARY, 1),
    ({"junction": "circular"}, RoadClass.TERTIARY, 1),
    ({"junction": "roundabout", "oneway": "no"}, RoadClass.PRIMARY, 0),
    ({"junction": "roundabout", "oneway": "-1"}, RoadClass.PRIMARY, -1),
    ({"junction": "yes"}, RoadClass.PRIMARY, 0),
    ({"oneway": "Yes"}, RoadClass.SERVICE, 1),
    ({"oneway": "maybe"}, RoadClass.SERVICE, 0),
])
def test_parse_oneway(tags, cls, expected):
    assert T.parse_oneway(tags, cls) == expected


@pytest.mark.parametrize("raw, expected", [
    ("hiking", SacScale.HIKING), ("mountain_hiking", SacScale.MOUNTAIN_HIKING),
    ("demanding_mountain_hiking", SacScale.DEMANDING_MOUNTAIN_HIKING), ("alpine_hiking", SacScale.ALPINE_HIKING),
    ("demanding_alpine_hiking", SacScale.DEMANDING_ALPINE_HIKING),
    ("difficult_alpine_hiking", SacScale.DIFFICULT_ALPINE_HIKING), ("T3", SacScale.DEMANDING_MOUNTAIN_HIKING),
    ("Mountain Hiking", SacScale.MOUNTAIN_HIKING), ("mountain_hiking;hiking", SacScale.MOUNTAIN_HIKING),
    ("easy", SacScale.UNKNOWN), (None, SacScale.UNKNOWN),
])
def test_parse_sac_scale(raw, expected):
    assert T.parse_sac_scale(raw) == expected


@pytest.mark.parametrize("raw, expected", [
    ("excellent", 1), ("good", 2), ("intermediate", 3), ("medium", 3), ("bad", 4), ("poor", 4), ("horrible", 5),
    ("no", 6), ("residential", 0), (None, 0), ("", 0),
])
def test_parse_trail_visibility(raw, expected):
    assert T.parse_trail_visibility(raw) == expected


# ---------------------------------------------------------------------------
# Access
# ---------------------------------------------------------------------------
@pytest.mark.parametrize("cls, expected", [
    (RoadClass.MOTORWAY, MOTOR_TRAVEL),
    (RoadClass.TRUNK, ALL_TRAVEL),
    (RoadClass.PRIMARY, ALL_TRAVEL),
    (RoadClass.SECONDARY, ALL_TRAVEL),
    (RoadClass.TERTIARY, ALL_TRAVEL),
    (RoadClass.UNCLASSIFIED, ALL_TRAVEL),
    (RoadClass.RESIDENTIAL, ALL_TRAVEL),
    (RoadClass.LIVING_STREET, ALL_TRAVEL),
    (RoadClass.ROAD, ALL_TRAVEL),
    (RoadClass.SERVICE, ALL_TRAVEL & ~BUS),
    (RoadClass.TRACK, F | B | M | J | H),
    (RoadClass.PATH, F | B | H),
    (RoadClass.FOOTWAY, F),
    (RoadClass.PEDESTRIAN, F),
    (RoadClass.STEPS, F),
    (RoadClass.CYCLEWAY, B | F),
    (RoadClass.BRIDLEWAY, H | F),
    (RoadClass.UNKNOWN, Travel(0)),
])
def test_parse_access_defaults(cls, expected):
    assert T.parse_access({}, cls) == expected


@pytest.mark.parametrize("tags, cls, expected", [
    # tracks: cars only on good tracks
    ({"tracktype": "grade1"}, RoadClass.TRACK, F | B | M | J | H | C),
    ({"tracktype": "grade2"}, RoadClass.TRACK, F | B | M | J | H | C),
    ({"tracktype": "grade3"}, RoadClass.TRACK, F | B | M | J | H),
    ({"motorcar": "yes"}, RoadClass.TRACK, F | B | M | J | H | C),
    ({"motor_vehicle": "yes"}, RoadClass.TRACK, F | B | M | J | H | C),  # generic yes: no bus on a track
    ({"bus": "yes"}, RoadClass.TRACK, F | B | M | J | H | BUS),
    # access=no keeps walking unless foot=no
    ({"access": "no"}, RoadClass.RESIDENTIAL, F),
    ({"access": "agricultural"}, RoadClass.UNCLASSIFIED, F),
    ({"access": "no", "foot": "no"}, RoadClass.RESIDENTIAL, Travel(0)),
    ({"access": "no", "foot": "yes"}, RoadClass.RESIDENTIAL, F),
    ({"access": "no", "motor_vehicle": "yes"}, RoadClass.RESIDENTIAL, F | MOTOR_TRAVEL),
    ({"access": "no", "bicycle": "designated"}, RoadClass.RESIDENTIAL, F | B),
    ({"access": "private"}, RoadClass.RESIDENTIAL, ALL_TRAVEL & ~BUS),
    ({"access": "permit"}, RoadClass.PRIMARY, ALL_TRAVEL & ~BUS),
    ({"access": "yes"}, RoadClass.FOOTWAY, F | B | H | M),
    ({"access": "destination"}, RoadClass.RESIDENTIAL, ALL_TRAVEL),
    ({"access": "customers"}, RoadClass.SERVICE, ALL_TRAVEL & ~BUS),
    # vehicle / motor_vehicle
    ({"vehicle": "no"}, RoadClass.RESIDENTIAL, F | H),
    ({"vehicle": "no", "bicycle": "yes"}, RoadClass.RESIDENTIAL, F | H | B),
    ({"motor_vehicle": "no"}, RoadClass.RESIDENTIAL, F | B | H),
    ({"motor_vehicle": "no", "motorcycle": "yes"}, RoadClass.RESIDENTIAL, F | B | H | M),
    ({"motor_vehicle": "private"}, RoadClass.RESIDENTIAL, ALL_TRAVEL & ~BUS),
    # mode specific
    ({"motorcycle": "no"}, RoadClass.RESIDENTIAL, ALL_TRAVEL & ~M),
    ({"motorcar": "no"}, RoadClass.RESIDENTIAL, ALL_TRAVEL & ~(C | J)),
    ({"hgv": "no"}, RoadClass.PRIMARY, ALL_TRAVEL & ~BUS),
    ({"bus": "no"}, RoadClass.PRIMARY, ALL_TRAVEL & ~BUS),
    ({"psv": "no"}, RoadClass.PRIMARY, ALL_TRAVEL & ~BUS),
    ({"hgv": "no", "bus": "yes"}, RoadClass.PRIMARY, ALL_TRAVEL & ~BUS),
    ({"bus": "yes"}, RoadClass.SERVICE, ALL_TRAVEL),
    ({"bicycle": "no"}, RoadClass.PATH, F | H),
    ({"horse": "no"}, RoadClass.PATH, F | B),
    ({"foot": "no"}, RoadClass.PRIMARY, ALL_TRAVEL & ~F),
    ({"foot": "yes"}, RoadClass.MOTORWAY, MOTOR_TRAVEL | F),
    # trails: motorbikes only when allowed, and never cars
    ({"motorcycle": "yes"}, RoadClass.PATH, F | B | H | M),
    ({"motor_vehicle": "yes"}, RoadClass.PATH, F | B | H | M),
    ({"motorcar": "yes"}, RoadClass.PATH, F | B | H),
    ({"bicycle": "yes"}, RoadClass.FOOTWAY, F | B),
    ({"bicycle": "yes"}, RoadClass.PEDESTRIAN, F | B),
    ({"bicycle": "yes"}, RoadClass.STEPS, F | B),
    ({"motorcycle": "yes"}, RoadClass.STEPS, F),
    ({"vehicle": "yes"}, RoadClass.STEPS, F),
    # neutral and junk values change nothing
    ({"foot": "use_sidepath"}, RoadClass.PRIMARY, ALL_TRAVEL),
    ({"bicycle": "dismount"}, RoadClass.FOOTWAY, F),
    ({"horse": "maybe"}, RoadClass.PATH, F | B | H),
    ({"access": "no;yes"}, RoadClass.RESIDENTIAL, F),
    # width
    ({"width": "2.5"}, RoadClass.RESIDENTIAL, ALL_TRAVEL & ~(BUS | C)),
    ({"width": "1.5 m"}, RoadClass.RESIDENTIAL, ALL_TRAVEL & ~(BUS | C | J)),
    ({"width": "3"}, RoadClass.RESIDENTIAL, ALL_TRAVEL),
    ({"width": "3,5"}, RoadClass.RESIDENTIAL, ALL_TRAVEL),
    ({"est_width": "2"}, RoadClass.UNCLASSIFIED, ALL_TRAVEL & ~(BUS | C)),
    ({"width": "bato"}, RoadClass.UNCLASSIFIED, ALL_TRAVEL),
    ({"width": "1.8", "bus": "yes"}, RoadClass.RESIDENTIAL, ALL_TRAVEL & ~(BUS | C | J)),
])
def test_parse_access_overrides(tags, cls, expected):
    assert T.parse_access(tags, cls) == expected


def test_parse_access_records_unknown_values_once_per_key():
    T.parse_access({"horse": "maybe", "access": "service"}, RoadClass.PATH)
    assert dict(T.unknown_values["horse"]) == {"maybe": 1}
    assert dict(T.unknown_values["access"]) == {"service": 1}
    T.parse_access({"width": "bato"}, RoadClass.PATH)
    assert "width" not in T.unknown_values  # width is recorded by parse_length_m, not here


# ---------------------------------------------------------------------------
# Names and scripts
# ---------------------------------------------------------------------------
@pytest.mark.parametrize("s, deva, latin", [
    ("काठमाडौं", True, False), ("सगरमाथा चुचुरो", True, False), ("Kathmandu", False, True),
    ("Kathmandu Durbar Square", False, True), ("Pashupatinath पशुपतिनाथ", False, False),
    ("Bhaktapur-1", False, True), ("Nāgārjun", False, True), ("ལྷ་ས", False, False), ("中尼铁路", False, False),
    ("1234", False, False), ("", False, False), (None, False, False), ("श्री ५", True, False),
])
def test_scripts(s, deva, latin):
    assert T.is_devanagari(s) is deva
    assert T.is_latin(s) is latin


def test_parse_name_latin_default():
    rec = T.parse_name({"name": "Phewa Lake", "name:ne": "फेवा ताल", "alt_name": "Phewa Tal;Fewa Lake"})
    assert rec == NameRec(default="Phewa Lake", en="Phewa Lake", ne="फेवा ताल", alt=("Phewa Tal", "Fewa Lake"))


def test_parse_name_devanagari_default():
    rec = T.parse_name({"name": "काठमाडौं", "name:en": "Kathmandu"})
    assert rec == NameRec(default="काठमाडौं", en="Kathmandu", ne="काठमाडौं", alt=())


def test_parse_name_int_name_fallback_and_alt_dedup():
    rec = T.parse_name({"name": "सगरमाथा", "int_name": "Everest", "official_name": "Mount Everest",
                        "alt_name": "everest; Chomolungma ;Chomolungma", "old_name": "सगरमाथा",
                        "alt_name:en": "Peak XV", "short_name": "", "loc_name": "Qomolangma"})
    assert rec.default == "सगरमाथा" and rec.en == "Everest" and rec.ne == "सगरमाथा"
    assert rec.alt == ("Chomolungma", "Mount Everest", "Qomolangma", "Peak XV")


def test_parse_name_mixed_script_default_is_neither():
    rec = T.parse_name({"name": "Pashupatinath पशुपतिनाथ"})
    assert rec == NameRec(default="Pashupatinath पशुपतिनाथ", en="", ne="", alt=())


def test_parse_name_wrong_script_tags_demoted_to_alt():
    rec = T.parse_name({"name": "Lukla", "name:ne": "Lukla Bazaar", "name:en": "लुक्ला"})
    assert rec.en == "Lukla" and rec.ne == ""
    assert rec.alt == ("लुक्ला", "Lukla Bazaar")


def test_parse_name_whitespace_and_nfc():
    decomposed = "Kāthmandu"  # 'a' + combining macron
    rec = T.parse_name({"name": f"  {decomposed}   Valley "})
    assert rec.default == "Kāthmandu Valley"


@pytest.mark.parametrize("tags", [{}, {"name": ""}, {"name": "   "}, {"highway": "path"}])
def test_parse_name_empty(tags):
    assert T.parse_name(tags) is None


def test_parse_name_only_alt():
    rec = T.parse_name({"alt_name": "Gosaikunda"})
    assert rec == NameRec(alt=("Gosaikunda",))


# ---------------------------------------------------------------------------
# Buildings
# ---------------------------------------------------------------------------
@pytest.mark.parametrize("tags, expected", [
    ({"building": "house"}, BuildingUse.HOUSE),
    ({"building": "detached"}, BuildingUse.HOUSE),
    ({"building": "semidetached_house"}, BuildingUse.HOUSE),
    ({"building": "terrace"}, BuildingUse.HOUSE),
    ({"building": "bungalow"}, BuildingUse.HOUSE),
    ({"building": "residential"}, BuildingUse.HOUSE),
    ({"building": "apartments"}, BuildingUse.APARTMENTS),
    ({"building": "dormitory"}, BuildingUse.APARTMENTS),
    ({"building": "commercial"}, BuildingUse.COMMERCIAL),
    ({"building": "retail"}, BuildingUse.COMMERCIAL),
    ({"building": "kiosk"}, BuildingUse.COMMERCIAL),
    ({"building": "supermarket"}, BuildingUse.COMMERCIAL),
    ({"building": "mixed_use"}, BuildingUse.MIXED_USE),
    ({"building": "school"}, BuildingUse.EDUCATION),
    ({"building": "college"}, BuildingUse.EDUCATION),
    ({"building": "university"}, BuildingUse.EDUCATION),
    ({"building": "kindergarten"}, BuildingUse.EDUCATION),
    ({"building": "educational"}, BuildingUse.EDUCATION),
    ({"building": "hospital"}, BuildingUse.HEALTH),
    ({"building": "clinic"}, BuildingUse.HEALTH),
    ({"building": "health_post"}, BuildingUse.HEALTH),
    ({"building": "government"}, BuildingUse.PUBLIC),
    ({"building": "public"}, BuildingUse.PUBLIC),
    ({"building": "civic"}, BuildingUse.PUBLIC),
    ({"building": "townhall"}, BuildingUse.PUBLIC),
    ({"building": "police"}, BuildingUse.PUBLIC),
    ({"building": "fire_station"}, BuildingUse.PUBLIC),
    ({"building": "industrial"}, BuildingUse.INDUSTRIAL),
    ({"building": "warehouse"}, BuildingUse.INDUSTRIAL),
    ({"building": "factory"}, BuildingUse.INDUSTRIAL),
    ({"building": "manufacture"}, BuildingUse.INDUSTRIAL),
    ({"building": "kiln"}, BuildingUse.INDUSTRIAL),
    ({"building": "temple"}, BuildingUse.RELIGIOUS),
    ({"building": "church"}, BuildingUse.RELIGIOUS),
    ({"building": "mosque"}, BuildingUse.RELIGIOUS),
    ({"building": "monastery"}, BuildingUse.RELIGIOUS),
    ({"building": "stupa"}, BuildingUse.RELIGIOUS),
    ({"building": "shrine"}, BuildingUse.RELIGIOUS),
    ({"building": "gompa"}, BuildingUse.RELIGIOUS),
    ({"building": "pagoda"}, BuildingUse.RELIGIOUS),
    ({"building": "Mrityunjay Mandir"}, BuildingUse.RELIGIOUS),  # real free text, word match
    ({"building": "hotel"}, BuildingUse.HOTEL),
    ({"building": "hostel"}, BuildingUse.HOTEL),
    ({"building": "guest_house"}, BuildingUse.HOTEL),
    ({"building": "hut"}, BuildingUse.HUT),
    ({"building": "shed"}, BuildingUse.HUT),
    ({"building": "cowshed"}, BuildingUse.HUT),
    ({"building": "barn"}, BuildingUse.HUT),
    ({"building": "stable"}, BuildingUse.HUT),
    ({"building": "sty"}, BuildingUse.HUT),
    ({"building": "farm_auxiliary"}, BuildingUse.HUT),
    ({"building": "cabin"}, BuildingUse.HUT),
    ({"building": "greenhouse"}, BuildingUse.GREENHOUSE),
    ({"building": "greenhouse_horticulture"}, BuildingUse.GREENHOUSE),
    ({"building": "construction"}, BuildingUse.CONSTRUCTION),
    ({"building": "roof"}, BuildingUse.ROOF),
    ({"building": "carport"}, BuildingUse.ROOF),
    ({"building": "garage"}, BuildingUse.GARAGE),
    ({"building": "garages"}, BuildingUse.GARAGE),
    ({"building": "office"}, BuildingUse.OFFICE),
    ({"building": "farm"}, BuildingUse.FARM),
    ({"building": "House"}, BuildingUse.HOUSE),
    ({"building": "yes"}, BuildingUse.UNKNOWN),
    ({"building": "ruins"}, BuildingUse.UNKNOWN),
    ({}, BuildingUse.UNKNOWN),
    # refinement of building=yes from tags on the same object
    ({"building": "yes", "amenity": "school"}, BuildingUse.EDUCATION),
    ({"building": "yes", "amenity": "place_of_worship"}, BuildingUse.RELIGIOUS),
    ({"building": "yes", "amenity": "hospital"}, BuildingUse.HEALTH),
    ({"building": "yes", "amenity": "police"}, BuildingUse.PUBLIC),
    ({"building": "yes", "amenity": "restaurant"}, BuildingUse.COMMERCIAL),
    ({"building": "yes", "shop": "convenience"}, BuildingUse.COMMERCIAL),
    ({"building": "yes", "shop": "vacant"}, BuildingUse.UNKNOWN),
    ({"building": "yes", "office": "ngo"}, BuildingUse.OFFICE),
    ({"building": "yes", "office": "government"}, BuildingUse.PUBLIC),
    ({"building": "yes", "tourism": "guest_house"}, BuildingUse.HOTEL),
    ({"building": "yes", "building:use": "residential"}, BuildingUse.HOUSE),
    ({"building": "yes", "man_made": "stupa"}, BuildingUse.RELIGIOUS),
    ({"building": "weird_value", "amenity": "school"}, BuildingUse.EDUCATION),
    # an explicit use is not overridden by other tags
    ({"building": "house", "shop": "convenience"}, BuildingUse.HOUSE),
])
def test_parse_building_use(tags, expected):
    assert T.parse_building_use(tags) == expected


def test_parse_building_use_records_only_unknown_values():
    T.parse_building_use({"building": "yes"})
    T.parse_building_use({"building": "ruins"})
    T.parse_building_use({"building": "house"})
    T.parse_building_use({"building": "aeroplane"})
    assert dict(T.unknown_values["building"]) == {"aeroplane": 1}


@pytest.mark.parametrize("raw, expected", [
    ("flat", RoofShape.FLAT), ("gabled", RoofShape.GABLED), ("hipped", RoofShape.HIPPED),
    ("pyramidal", RoofShape.PYRAMIDAL), ("skillion", RoofShape.SKILLION), ("dome", RoofShape.DOME),
    ("round", RoofShape.ROUND), ("pagoda", RoofShape.PAGODA), ("shikhara", RoofShape.SHIKHARA),
    ("gambrel", RoofShape.GAMBREL), ("mansard", RoofShape.MANSARD), ("half-hipped", RoofShape.HALF_HIPPED),
    ("half_hipped", RoofShape.HALF_HIPPED), ("onion", RoofShape.ONION), ("cone", RoofShape.CONE),
    ("double_pitch", RoofShape.GABLED), ("pitched", RoofShape.GABLED), ("saltbox", RoofShape.GABLED),
    ("side_hipped", RoofShape.HIPPED), ("curved", RoofShape.ROUND), ("hemispherical_dome", RoofShape.DOME),
    ("Flat", RoofShape.FLAT), ("mixed", RoofShape.UNKNOWN), ("Mix", RoofShape.UNKNOWN),
    ("complex_regular", RoofShape.UNKNOWN), (None, RoofShape.UNKNOWN), ("tin", RoofShape.UNKNOWN),
])
def test_parse_roof_shape(raw, expected):
    assert T.parse_roof_shape(raw) == expected


def test_roof_shape_mixed_not_recorded():
    T.parse_roof_shape("mixed")
    T.parse_roof_shape("complex_irregular")
    T.parse_roof_shape("tin")
    assert dict(T.unknown_values["roof:shape"]) == {"tin": 1}


@pytest.mark.parametrize("raw, expected", [
    ("metal", RoofMaterial.METAL), ("metal_sheet", RoofMaterial.METAL), ("tin", RoofMaterial.METAL),
    ("corrugated_iron", RoofMaterial.METAL), ("CGI", RoofMaterial.METAL), ("sheet_metal", RoofMaterial.METAL),
    ("roof_tiles", RoofMaterial.TILES), ("tile", RoofMaterial.TILES), ("clay", RoofMaterial.TILES),
    ("slate", RoofMaterial.SLATE), ("stone_tile", RoofMaterial.SLATE), ("stone", RoofMaterial.STONE),
    ("thatch", RoofMaterial.THATCH), ("grass", RoofMaterial.THATCH), ("straw", RoofMaterial.THATCH),
    ("concrete", RoofMaterial.CONCRETE), ("cement", RoofMaterial.CONCRETE), ("RCC", RoofMaterial.CONCRETE),
    ("concerte", RoofMaterial.CONCRETE), ("rbc", RoofMaterial.CONCRETE), ("concerte:tin", RoofMaterial.CONCRETE),
    ("wood", RoofMaterial.WOOD), ("timber", RoofMaterial.WOOD), ("glass", RoofMaterial.GLASS),
    ("mud", RoofMaterial.MUD), ("mixed", RoofMaterial.UNKNOWN), ("flat", RoofMaterial.UNKNOWN),
    (None, RoofMaterial.UNKNOWN),
])
def test_parse_roof_material(raw, expected):
    assert T.parse_roof_material(raw) == expected


@pytest.mark.parametrize("raw, expected", [
    ("brick", WallMaterial.BRICK), ("bricks", WallMaterial.BRICK), ("stone", WallMaterial.STONE),
    ("concrete", WallMaterial.PLASTER), ("cement", WallMaterial.PLASTER), ("plaster", WallMaterial.PLASTER),
    ("rcc", WallMaterial.PLASTER), ("mud", WallMaterial.MUD), ("adobe", WallMaterial.MUD),
    ("earth", WallMaterial.MUD), ("wood", WallMaterial.WOOD), ("timber", WallMaterial.WOOD),
    ("bamboo", WallMaterial.BAMBOO), ("metal", WallMaterial.METAL), ("glass", WallMaterial.GLASS),
    ("stone & wood", WallMaterial.STONE), ("stone concrete", WallMaterial.STONE),
    ("mud and stone", WallMaterial.MUD), ("bamboo_sheet", WallMaterial.BAMBOO), ("CGI sheet", WallMaterial.METAL),
    ("cement:brick", WallMaterial.PLASTER), ("brics", WallMaterial.BRICK), ("mixed", WallMaterial.UNKNOWN),
    ("gold", WallMaterial.UNKNOWN), (None, WallMaterial.UNKNOWN),
])
def test_parse_wall_material(raw, expected):
    assert T.parse_wall_material(raw) == expected


# ---------------------------------------------------------------------------
# Places
# ---------------------------------------------------------------------------
@pytest.mark.parametrize("value, expected", [
    ("country", PlaceKind.COUNTRY), ("state", PlaceKind.PROVINCE), ("province", PlaceKind.PROVINCE),
    ("city", PlaceKind.CITY), ("town", PlaceKind.TOWN), ("village", PlaceKind.VILLAGE),
    ("hamlet", PlaceKind.HAMLET), ("isolated_dwelling", PlaceKind.ISOLATED_DWELLING),
    ("suburb", PlaceKind.SUBURB), ("neighbourhood", PlaceKind.NEIGHBOURHOOD),
    ("neighborhood", PlaceKind.NEIGHBOURHOOD), ("quarter", PlaceKind.QUARTER), ("locality", PlaceKind.LOCALITY),
    ("farm", PlaceKind.FARM), ("square", PlaceKind.SQUARE), ("island", PlaceKind.ISLAND),
    ("islet", PlaceKind.ISLAND), ("district", PlaceKind.DISTRICT), ("municipality", PlaceKind.LOCAL_LEVEL),
    ("Village", PlaceKind.VILLAGE), ("toukhel 3", PlaceKind.NONE), ("motichaur 3", PlaceKind.NONE),
    ("9847896219", PlaceKind.NONE), ("region", PlaceKind.NONE), ("plot", PlaceKind.NONE), ("", PlaceKind.NONE),
])
def test_parse_place_kind(value, expected):
    assert T.parse_place_kind({"place": value}) == expected


def test_parse_place_kind_records_free_text_only():
    for v in ("toukhel 3", "region", "town", "9847896219"):
        T.parse_place_kind({"place": v})
    assert dict(T.unknown_values["place"]) == {"toukhel 3": 1, "9847896219": 1}


# ---------------------------------------------------------------------------
# POIs
# ---------------------------------------------------------------------------
POW = {"amenity": "place_of_worship"}


@pytest.mark.parametrize("tags, expected", [
    # religious
    ({**POW, "religion": "hindu"}, PoiKind.TEMPLE_HINDU),
    ({**POW, "religion": "Hindu"}, PoiKind.TEMPLE_HINDU),
    ({**POW, "religion": "hindu;buddhist"}, PoiKind.TEMPLE_HINDU),
    ({**POW, "religion": "hindu;budhhist"}, PoiKind.TEMPLE_HINDU),
    ({**POW, "religion": "buddhist"}, PoiKind.GOMPA),
    ({**POW, "religion": "buddhist;hindu"}, PoiKind.GOMPA),
    ({**POW, "religion": "bon"}, PoiKind.GOMPA),
    ({**POW, "religion": "muslim"}, PoiKind.MOSQUE),
    ({**POW, "religion": "christian"}, PoiKind.CHURCH),
    ({**POW, "religion": "Kirat"}, PoiKind.PLACE_OF_WORSHIP),
    ({**POW, "religion": "Baun"}, PoiKind.PLACE_OF_WORSHIP),
    ({**POW}, PoiKind.PLACE_OF_WORSHIP),
    ({**POW, "building": "mosque"}, PoiKind.MOSQUE),
    ({**POW, "building": "church"}, PoiKind.CHURCH),
    ({**POW, "religion": "hindu", "building": "stupa"}, PoiKind.STUPA),
    ({**POW, "religion": "buddhist", "man_made": "stupa"}, PoiKind.STUPA),
    ({**POW, "religion": "buddhist", "place_of_worship": "chorten"}, PoiKind.CHORTEN),
    ({**POW, "religion": "hindu", "place_of_worship": "shrine"}, PoiKind.SHRINE),
    ({"amenity": "temple", "religion": "hindu"}, PoiKind.TEMPLE_HINDU),
    ({"man_made": "stupa"}, PoiKind.STUPA),
    ({"building": "stupa"}, PoiKind.STUPA),
    ({"man_made": "chorten"}, PoiKind.CHORTEN),
    ({"man_made": "mani_wall"}, PoiKind.MANI_WALL),
    ({"historic": "wayside_shrine"}, PoiKind.SHRINE),
    ({"amenity": "monastery"}, PoiKind.GOMPA),
    # heritage
    ({"place": "square", "name": "Patan Durbar Square"}, PoiKind.HERITAGE_SQUARE),
    ({"place": "square", "name": "Asan"}, PoiKind.NONE),  # D1: no heritage, durbar name or wikidata
    ({"place": "square", "name": "Asan", "heritage": "1"}, PoiKind.HERITAGE_SQUARE),
    ({"place": "square", "name": "Koteshwar Chowk", "wikidata": "Q1"}, PoiKind.HERITAGE_SQUARE),
    ({"tourism": "attraction", "name": "Bhaktapur Durbar Square"}, PoiKind.HERITAGE_SQUARE),
    ({"highway": "pedestrian", "name": "Kathmandu Durbar Square"}, PoiKind.HERITAGE_SQUARE),
    ({"amenity": "restaurant", "name": "Durbar Square Cafe"}, PoiKind.RESTAURANT),
    ({"tourism": "hotel", "name": "Durbar Square Hotel"}, PoiKind.HOTEL),
    ({"historic": "palace", "tourism": "museum"}, PoiKind.PALACE),
    ({"historic": "castle"}, PoiKind.NONE),  # D1: valley "castles" are houses
    ({"historic": "castle", "name": "Rishen's House"}, PoiKind.NONE),
    ({"historic": "castle", "name": "Kaiser Mahal"}, PoiKind.PALACE),
    ({"tourism": "artwork"}, PoiKind.STATUE),
    ({"historic": "memorial", "memorial": "statue"}, PoiKind.STATUE),
    ({"amenity": "crematorium"}, PoiKind.GHAT),
    ({"name": "Arya Ghat", "landuse": "religious"}, PoiKind.GHAT),
    ({"name": "Ghat Cafe", "amenity": "cafe"}, PoiKind.CAFE),
    ({"historic": "monument"}, PoiKind.MONUMENT),
    ({"historic": "memorial"}, PoiKind.MONUMENT),
    ({"historic": "ruins"}, PoiKind.RUINS),
    ({"historic": "archaeological_site"}, PoiKind.RUINS),
    ({"historic": "stone_tap"}, PoiKind.STONE_TAP),
    ({"man_made": "water_tap", "historic": "yes"}, PoiKind.STONE_TAP),
    ({"amenity": "drinking_water", "name": "Manga Hiti"}, PoiKind.STONE_TAP),
    ({"man_made": "water_tap"}, PoiKind.NONE),
    ({"historic": "city_gate"}, PoiKind.CITY_GATE),
    ({"tourism": "museum"}, PoiKind.MUSEUM),
    # nature
    ({"natural": "peak", "tourism": "viewpoint"}, PoiKind.PEAK),
    ({"natural": "volcano"}, PoiKind.PEAK),
    ({"tourism": "viewpoint"}, PoiKind.VIEWPOINT),
    ({"waterway": "waterfall"}, PoiKind.WATERFALL),
    ({"natural": "waterfall"}, PoiKind.WATERFALL),
    ({"natural": "cave_entrance"}, PoiKind.CAVE),
    ({"natural": "cave"}, PoiKind.CAVE),
    ({"natural": "water", "water": "lake", "name": "Phewa Lake"}, PoiKind.LAKE),
    ({"natural": "water", "name": "Rara Lake"}, PoiKind.LAKE),
    ({"natural": "water", "water": "lake"}, PoiKind.NONE),
    ({"natural": "water", "water": "pond", "name": "Rani Pokhari"}, PoiKind.LAKE),
    ({"natural": "water", "water": "pond", "name": "Fish Pond"}, PoiKind.NONE),
    ({"natural": "hot_spring"}, PoiKind.HOT_SPRING),
    ({"amenity": "public_bath", "bath:type": "hot_spring"}, PoiKind.HOT_SPRING),
    ({"natural": "spring", "name": "Tatopani"}, PoiKind.SPRING),  # D1: hot spring only with the tag
    ({"natural": "spring"}, PoiKind.SPRING),
    ({"natural": "glacier"}, PoiKind.NONE),  # D1: unnamed glaciers are not POIs
    ({"natural": "glacier", "name": "Khumbu Glacier"}, PoiKind.GLACIER),
    ({"mountain_pass": "yes", "natural": "saddle"}, PoiKind.PASS),
    ({"natural": "saddle"}, PoiKind.PASS),
    ({"mountain_pass": "yes"}, PoiKind.PASS),
    ({"leisure": "park"}, PoiKind.PARK),
    ({"leisure": "garden"}, PoiKind.PARK),
    ({"boundary": "national_park"}, PoiKind.PROTECTED_AREA),
    ({"boundary": "protected_area"}, PoiKind.PROTECTED_AREA),
    ({"leisure": "nature_reserve"}, PoiKind.PROTECTED_AREA),
    ({"natural": "tree", "name": "Pipal Bot"}, PoiKind.NOTABLE_TREE),
    ({"natural": "tree"}, PoiKind.NONE),
    ({"natural": "ridge"}, PoiKind.NONE),
    ({"natural": "ridge", "name": "Shivapuri Danda"}, PoiKind.RIDGE),
    # transport
    ({"aeroway": "aerodrome"}, PoiKind.NONE),  # D1: a travel agency tagged aerodrome
    ({"aeroway": "aerodrome", "iata": "KTM"}, PoiKind.AIRPORT),
    ({"aeroway": "aerodrome", "wikidata": "Q1"}, PoiKind.AIRPORT),
    ({"free_flying:site": "landing"}, PoiKind.PARAGLIDING_LANDING),
    ({"free_flying:site": "takeoff"}, PoiKind.PARAGLIDING),
    ({"attraction": "bungee_jumping"}, PoiKind.BUNGEE),
    ({"office": "travel_agent", "name": "Bungee Nepal"}, PoiKind.NONE),
    ({"amenity": "school", "name": "Rafting School"}, PoiKind.SCHOOL),
    ({"aeroway": "helipad"}, PoiKind.HELIPAD),
    ({"aeroway": "heliport"}, PoiKind.HELIPAD),
    ({"amenity": "bus_station"}, PoiKind.BUS_STATION),
    ({"amenity": "fuel"}, PoiKind.FUEL),
    ({"aerialway": "station"}, PoiKind.CABLE_CAR_STATION),
    ({"railway": "station"}, PoiKind.RAILWAY_STATION),
    ({"railway": "halt"}, PoiKind.RAILWAY_STATION),
    ({"amenity": "taxi"}, PoiKind.TAXI_STAND),
    # lodging, food, shops
    ({"tourism": "hotel"}, PoiKind.HOTEL),
    ({"tourism": "motel"}, PoiKind.HOTEL),
    ({"tourism": "guest_house"}, PoiKind.GUEST_HOUSE),
    ({"tourism": "hostel"}, PoiKind.HOSTEL),
    ({"tourism": "camp_site"}, PoiKind.CAMP_SITE),
    ({"tourism": "alpine_hut"}, PoiKind.TEAHOUSE),
    ({"tourism": "wilderness_hut"}, PoiKind.TEAHOUSE),
    ({"amenity": "restaurant"}, PoiKind.RESTAURANT),
    ({"amenity": "fast_food"}, PoiKind.RESTAURANT),
    ({"amenity": "food_court"}, PoiKind.RESTAURANT),
    ({"amenity": "cafe"}, PoiKind.CAFE),
    ({"shop": "convenience"}, PoiKind.SHOP),
    ({"shop": "vacant"}, PoiKind.NONE),
    ({"amenity": "marketplace"}, PoiKind.MARKETPLACE),
    # tourism misc
    ({"tourism": "attraction"}, PoiKind.ATTRACTION),
    ({"tourism": "information"}, PoiKind.INFORMATION),
    ({"tourism": "picnic_site"}, PoiKind.PICNIC_SITE),
    ({"tourism": "theme_park"}, PoiKind.THEME_PARK),
    ({"tourism": "zoo"}, PoiKind.ZOO),
    # activities
    ({"sport": "free_flying"}, PoiKind.PARAGLIDING),
    ({"aerialway": "zip_line"}, PoiKind.ZIPLINE),
    ({"tourism": "attraction", "name": "Pokhara Zip Line"}, PoiKind.ZIPLINE),
    ({"tourism": "attraction", "name": "Zip-Flyer Nepal"}, PoiKind.ZIPLINE),
    ({"sport": "bungee"}, PoiKind.BUNGEE),
    ({"tourism": "attraction", "name": "The Last Resort Bungy"}, PoiKind.BUNGEE),
    ({"name": "Bhote Koshi Bungee Jump"}, PoiKind.BUNGEE),
    ({"tourism": "hotel", "name": "Jungle Safari Lodge"}, PoiKind.HOTEL),
    ({"tourism": "attraction", "name": "Jungle Safari"}, PoiKind.SAFARI),
    ({"sport": "rafting"}, PoiKind.RAFTING),
    ({"amenity": "boat_rental"}, PoiKind.BOATING),
    # civic
    ({"amenity": "school"}, PoiKind.SCHOOL),
    ({"amenity": "college"}, PoiKind.SCHOOL),
    ({"amenity": "university"}, PoiKind.SCHOOL),
    ({"amenity": "kindergarten"}, PoiKind.SCHOOL),
    ({"amenity": "hospital"}, PoiKind.HOSPITAL),
    ({"amenity": "clinic"}, PoiKind.HOSPITAL),
    ({"amenity": "townhall"}, PoiKind.GOVERNMENT),
    ({"office": "government"}, PoiKind.GOVERNMENT),
    ({"amenity": "bank"}, PoiKind.BANK),
    ({"amenity": "parking"}, PoiKind.PARKING),
    ({"man_made": "bridge"}, PoiKind.BRIDGE),
    # nothing
    ({}, PoiKind.NONE),
    ({"amenity": "bench"}, PoiKind.NONE),
    ({"highway": "residential"}, PoiKind.NONE),
])
def test_poi_kind(tags, expected):
    assert T.poi_kind(tags) == expected


# ---------------------------------------------------------------------------
# Areas and lines
# ---------------------------------------------------------------------------
@pytest.mark.parametrize("tags, expected", [
    ({"natural": "water"}, AreaKind.WATER_LAKE),
    ({"natural": "water", "water": "lake"}, AreaKind.WATER_LAKE),
    ({"natural": "water", "water": "river"}, AreaKind.WATER_RIVER),
    ({"natural": "water", "water": "canal"}, AreaKind.WATER_RIVER),
    ({"natural": "water", "water": "stream"}, AreaKind.WATER_RIVER),
    ({"natural": "water", "water": "pond"}, AreaKind.WATER_POND),
    ({"natural": "water", "water": "reservoir"}, AreaKind.WATER_POND),
    ({"natural": "water", "water": "basin"}, AreaKind.WATER_POND),
    ({"waterway": "riverbank"}, AreaKind.WATER_RIVER),
    ({"landuse": "reservoir"}, AreaKind.WATER_POND),
    ({"landuse": "basin"}, AreaKind.WATER_POND),
    ({"natural": "glacier"}, AreaKind.GLACIER),
    ({"natural": "wetland"}, AreaKind.WETLAND),
    ({"landuse": "forest"}, AreaKind.FOREST),
    ({"natural": "wood"}, AreaKind.FOREST),
    ({"landuse": "farmland"}, AreaKind.FARMLAND),
    ({"landuse": "farmland", "crop": "tea"}, AreaKind.TEA_GARDEN),
    ({"landuse": "farmyard"}, AreaKind.FARMLAND),
    ({"landuse": "orchard"}, AreaKind.ORCHARD),
    ({"landuse": "plant_nursery"}, AreaKind.ORCHARD),
    ({"landuse": "orchard", "trees": "tea_plants"}, AreaKind.TEA_GARDEN),
    ({"landuse": "meadow"}, AreaKind.MEADOW),
    ({"landuse": "grass"}, AreaKind.MEADOW),
    ({"landuse": "village_green"}, AreaKind.MEADOW),
    ({"natural": "grassland"}, AreaKind.GRASSLAND),
    ({"natural": "scrub"}, AreaKind.SCRUB),
    ({"natural": "heath"}, AreaKind.SCRUB),
    ({"leisure": "park"}, AreaKind.PARK),
    ({"leisure": "garden"}, AreaKind.PARK),
    ({"landuse": "residential"}, AreaKind.RESIDENTIAL),
    ({"landuse": "commercial"}, AreaKind.COMMERCIAL),
    ({"landuse": "retail"}, AreaKind.COMMERCIAL),
    ({"landuse": "industrial"}, AreaKind.INDUSTRIAL),
    ({"landuse": "quarry"}, AreaKind.INDUSTRIAL),
    ({"landuse": "brownfield"}, AreaKind.NONE),
    ({"landuse": "religious"}, AreaKind.RELIGIOUS),
    ({"amenity": "place_of_worship", "landuse": "residential"}, AreaKind.RELIGIOUS),
    ({"place": "square"}, AreaKind.PEDESTRIAN),
    ({"highway": "pedestrian", "area": "yes"}, AreaKind.PEDESTRIAN),
    ({"highway": "pedestrian", "type": "multipolygon"}, AreaKind.PEDESTRIAN),
    ({"highway": "pedestrian"}, AreaKind.NONE),
    ({"area:highway": "pedestrian"}, AreaKind.PEDESTRIAN),
    ({"natural": "sand"}, AreaKind.SAND_SHINGLE),
    ({"natural": "shingle"}, AreaKind.SAND_SHINGLE),
    ({"natural": "beach"}, AreaKind.SAND_SHINGLE),
    ({"natural": "bare_rock"}, AreaKind.BARE_ROCK),
    ({"natural": "rock"}, AreaKind.BARE_ROCK),
    ({"natural": "scree"}, AreaKind.SCREE),
    ({"aeroway": "aerodrome"}, AreaKind.AERODROME),
    ({"landuse": "cemetery"}, AreaKind.CEMETERY),
    ({"amenity": "grave_yard"}, AreaKind.CEMETERY),
    ({"landuse": "military"}, AreaKind.MILITARY),
    ({"leisure": "pitch"}, AreaKind.PITCH),
    ({"leisure": "stadium"}, AreaKind.PITCH),
    ({"leisure": "sports_centre"}, AreaKind.PITCH),
    ({"boundary": "protected_area"}, AreaKind.PROTECTED),
    ({"boundary": "national_park"}, AreaKind.PROTECTED),
    ({"leisure": "nature_reserve"}, AreaKind.PROTECTED),
    ({"boundary": "national_park", "natural": "wood"}, AreaKind.FOREST),
    ({}, AreaKind.NONE),
    ({"building": "yes"}, AreaKind.NONE),
])
def test_area_kind(tags, expected):
    assert T.area_kind(tags) == expected


@pytest.mark.parametrize("tags, expected", [
    ({"waterway": "river"}, LineKind.RIVER),
    ({"waterway": "stream"}, LineKind.STREAM),
    ({"waterway": "canal"}, LineKind.CANAL),
    ({"waterway": "ditch"}, LineKind.DITCH),
    ({"waterway": "drain"}, LineKind.DITCH),
    ({"waterway": "waterfall"}, LineKind.WATERFALL),
    ({"railway": "rail"}, LineKind.RAILWAY),
    ({"railway": "narrow_gauge"}, LineKind.RAILWAY),
    ({"railway": "light_rail"}, LineKind.RAILWAY),
    ({"railway": "abandoned"}, LineKind.NONE),
    ({"railway": "disused"}, LineKind.NONE),
    ({"railway": "construction"}, LineKind.NONE),
    ({"aerialway": "cable_car"}, LineKind.CABLE_CAR),
    ({"aerialway": "gondola"}, LineKind.CABLE_CAR),
    ({"aerialway": "mixed_lift"}, LineKind.CABLE_CAR),
    ({"aerialway": "chair_lift"}, LineKind.CHAIR_LIFT),
    ({"aerialway": "zip_line"}, LineKind.ZIP_LINE),
    ({"aeroway": "runway"}, LineKind.RUNWAY),
    ({"aeroway": "taxiway"}, LineKind.TAXIWAY),
    ({"barrier": "city_wall"}, LineKind.CITY_WALL),
    ({"historic": "city_wall"}, LineKind.CITY_WALL),
    ({"man_made": "mani_wall"}, LineKind.MANI_WALL),
    ({"historic": "wall", "name": "Mani wall"}, LineKind.MANI_WALL),
    ({"barrier": "wall", "name": "Long Mani Wall"}, LineKind.MANI_WALL),
    ({"barrier": "wall", "name": "Manipur Wall"}, LineKind.NONE),
    ({"barrier": "wall"}, LineKind.NONE),
    ({}, LineKind.NONE),
])
def test_line_kind(tags, expected):
    assert T.line_kind(tags) == expected


# ---------------------------------------------------------------------------
# Robustness: nothing raises on garbage
# ---------------------------------------------------------------------------
_KEYS = ["highway", "surface", "tracktype", "smoothness", "width", "est_width", "lanes", "oneway", "junction",
         "sac_scale", "trail_visibility", "access", "vehicle", "motor_vehicle", "motorcar", "motorcycle", "hgv",
         "bus", "psv", "bicycle", "foot", "horse", "name", "name:en", "name:ne", "int_name", "alt_name",
         "building", "building:use", "amenity", "shop", "office", "tourism", "religion", "man_made", "historic",
         "natural", "water", "waterway", "landuse", "leisure", "place", "boundary", "aeroway", "aerialway",
         "railway", "sport", "barrier", "crop", "area", "type"]
_ALPHABET = "aAzZ09 ;:,./-_+'\"~()?&|\\गडौंाब्ड़ॐ०५בלת中́​\t\n"


def _garbage(rng: random.Random) -> str:
    return "".join(rng.choice(_ALPHABET) for _ in range(rng.randint(0, 12)))


def test_parsers_never_raise_on_garbage():
    rng = random.Random(20261004)
    raw_parsers = [T.normalize_surface, T.surface_from_tracktype, T.surface_from_smoothness, T.parse_length_m,
                   T.parse_levels, T.parse_ele, T.parse_lanes, T.parse_sac_scale, T.parse_trail_visibility,
                   T.parse_roof_shape, T.parse_roof_material, T.parse_wall_material, T.parse_tracktype,
                   T.is_devanagari, T.is_latin]
    tag_parsers = [T.parse_road_class, T.parse_name, T.parse_building_use, T.parse_place_kind, T.poi_kind,
                   T.area_kind, T.line_kind]
    for _ in range(3000):
        s = _garbage(rng)
        for fn in raw_parsers:
            fn(s)
        tags = {k: _garbage(rng) for k in rng.sample(_KEYS, rng.randint(0, 8))}
        for fn in tag_parsers:
            fn(tags)
        cls = rng.choice(list(RoadClass))
        assert T.parse_access(tags, cls) & ~ALL_TRAVEL == 0
        assert T.parse_oneway(tags, cls) in (-1, 0, 1)
    for fn in raw_parsers:
        fn(None)


def test_parse_access_never_exceeds_physical_caps():
    rng = random.Random(7)
    values = ["yes", "no", "private", "designated", "permissive", "destination", "agricultural", ""]
    keys = ["access", "vehicle", "motor_vehicle", "motorcar", "motorcycle", "hgv", "psv", "bus", "bicycle",
            "foot", "horse"]
    for _ in range(2000):
        tags = {k: rng.choice(values) for k in keys if rng.random() < 0.4}
        for cls in (RoadClass.PATH, RoadClass.FOOTWAY, RoadClass.PEDESTRIAN, RoadClass.CYCLEWAY,
                    RoadClass.BRIDLEWAY):
            assert T.parse_access(tags, cls) & (C | J | BUS) == 0
        assert T.parse_access(tags, RoadClass.STEPS) & ~(F | B) == 0
        assert T.parse_access(tags, RoadClass.UNKNOWN) == Travel(0)


# ---------------------------------------------------------------------------
# Real data
# ---------------------------------------------------------------------------
@pytest.mark.realdata
@pytest.mark.slow
def test_real_surface_values_are_recognised(nepal_pbf):
    """Nearly every surface-tagged way in the Nepal extract parses (junk aside)."""
    import osmium

    total = recognised = 0
    fp = osmium.FileProcessor(str(nepal_pbf), osmium.osm.WAY).with_filter(osmium.filter.KeyFilter("surface"))
    for way in fp:
        tags = dict(way.tags)
        total += 1
        recognised += T.normalize_surface(tags["surface"]) is not None
        T.parse_road_class(tags)
        T.parse_access(tags, T.parse_road_class(tags)[0])
        T.parse_name(tags)
    assert total > 50_000
    assert recognised / total > 0.998
