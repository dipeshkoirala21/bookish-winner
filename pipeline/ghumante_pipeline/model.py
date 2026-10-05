"""Shared enums and intermediate feature types: the pipeline's data contract.

The enums' integer values are written into the binary tile, search and routing
files (docs/DATA_FORMATS.md), and the C# runtime mirrors them. Never renumber
an existing value; append new ones and bump ``ENUMS_VERSION``. Running
``python -m ghumante_pipeline.model`` dumps every enum to
``shared/enums.json``, and the C# test suite checks its copy against that file.
"""

from __future__ import annotations

import json
import sys
from dataclasses import dataclass, field
from enum import IntEnum, IntFlag

import numpy as np

ENUMS_VERSION = 3  # 2: W2 F1 batch (docs/W2_DESIGN.md section 9.3, CONTENT_COVERAGE section 3.2); 3: EntryRule NO_LEATHER


class RoadClass(IntEnum):
    UNKNOWN = 0
    MOTORWAY = 1
    TRUNK = 2
    PRIMARY = 3
    SECONDARY = 4
    TERTIARY = 5
    UNCLASSIFIED = 6
    RESIDENTIAL = 7
    LIVING_STREET = 8
    SERVICE = 9
    TRACK = 10
    ROAD = 11  # highway=road: unknown classification
    PEDESTRIAN = 12
    FOOTWAY = 13
    PATH = 14
    STEPS = 15
    CYCLEWAY = 16
    BRIDLEWAY = 17


TRAIL_CLASSES = frozenset({RoadClass.PEDESTRIAN, RoadClass.FOOTWAY, RoadClass.PATH, RoadClass.STEPS,
                           RoadClass.CYCLEWAY, RoadClass.BRIDLEWAY})


class Surface(IntEnum):
    """Game surface. Visual identity per value; physics uses ``SURFACE_GROUP``."""

    UNKNOWN = 0
    ASPHALT = 1
    CONCRETE = 2
    BRICK = 3  # paving_stones / bricks: the Newar old-town streets
    COBBLE = 4  # sett, cobblestone, stone flags
    GRAVEL = 5
    COMPACTED = 6
    DIRT = 7  # unpaved, dirt, earth, ground
    MUD = 8
    SAND = 9
    GRASS = 10
    ROCK = 11
    SNOW_ICE = 12
    WOOD = 13
    METAL = 14


class SurfaceGroup(IntEnum):
    """The four physically distinct families the brief requires."""

    PAVED = 0
    GRAVEL = 1
    DIRT = 2
    MUD = 3


SURFACE_GROUP: dict[Surface, SurfaceGroup] = {
    Surface.UNKNOWN: SurfaceGroup.DIRT,
    Surface.ASPHALT: SurfaceGroup.PAVED,
    Surface.CONCRETE: SurfaceGroup.PAVED,
    Surface.BRICK: SurfaceGroup.PAVED,
    Surface.COBBLE: SurfaceGroup.PAVED,
    Surface.GRAVEL: SurfaceGroup.GRAVEL,
    Surface.COMPACTED: SurfaceGroup.GRAVEL,
    Surface.DIRT: SurfaceGroup.DIRT,
    Surface.MUD: SurfaceGroup.MUD,
    Surface.SAND: SurfaceGroup.DIRT,
    Surface.GRASS: SurfaceGroup.DIRT,
    Surface.ROCK: SurfaceGroup.GRAVEL,
    Surface.SNOW_ICE: SurfaceGroup.MUD,
    Surface.WOOD: SurfaceGroup.PAVED,
    Surface.METAL: SurfaceGroup.PAVED,
}


class SurfaceSource(IntEnum):
    TAGGED = 0  # surface=* present and understood
    DERIVED = 1  # derived from tracktype / smoothness
    INFERRED = 2  # statistical model (surface.py)
    DEFAULT = 3  # no evidence at all; class default


class SacScale(IntEnum):
    UNKNOWN = 0
    HIKING = 1  # T1
    MOUNTAIN_HIKING = 2  # T2
    DEMANDING_MOUNTAIN_HIKING = 3  # T3
    ALPINE_HIKING = 4  # T4
    DEMANDING_ALPINE_HIKING = 5  # T5
    DIFFICULT_ALPINE_HIKING = 6  # T6


class BuildingUse(IntEnum):
    UNKNOWN = 0  # building=yes
    HOUSE = 1
    APARTMENTS = 2
    COMMERCIAL = 3
    MIXED_USE = 4
    EDUCATION = 5
    HEALTH = 6
    PUBLIC = 7
    INDUSTRIAL = 8
    RELIGIOUS = 9
    HOTEL = 10
    HUT = 11  # hut, shed, cowshed, farm_auxiliary
    GREENHOUSE = 12
    CONSTRUCTION = 13
    ROOF = 14  # open-sided roof
    GARAGE = 15
    OFFICE = 16
    FARM = 17


class BuildingArchetype(IntEnum):
    GENERIC = 0
    NEWAR = 1
    MODERN_URBAN = 2
    HILL_VILLAGE = 3
    TERAI = 4
    SHERPA_HIMALAYAN = 5
    TRANS_HIMALAYAN = 6  # Mustang / Dolpo flat-roofed
    TEMPLE_PAGODA = 7
    TEMPLE_SHIKHARA = 8
    STUPA = 9
    GOMPA = 10
    CHORTEN = 11
    SHRINE = 12
    MOSQUE = 13
    CHURCH = 14
    INDUSTRIAL = 15
    INSTITUTIONAL = 16  # schools, hospitals, offices
    HUT = 17
    GREENHOUSE = 18
    TEAHOUSE = 19
    RANA_PALACE = 20  # Rana-era neoclassical palaces (W2_DESIGN 2.3)
    NEWAR_HYBRID = 21  # Newar ground floors with concrete storeys on top (W2_DESIGN 2.3)


class RoofShape(IntEnum):
    UNKNOWN = 0
    FLAT = 1
    GABLED = 2
    HIPPED = 3
    PYRAMIDAL = 4
    SKILLION = 5
    DOME = 6
    ROUND = 7
    PAGODA = 8  # multi-tier
    SHIKHARA = 9
    GAMBREL = 10
    MANSARD = 11
    HALF_HIPPED = 12
    ONION = 13
    CONE = 14


class RoofMaterial(IntEnum):
    UNKNOWN = 0
    CONCRETE = 1
    METAL = 2  # corrugated iron sheets: very common
    TILES = 3
    SLATE = 4
    THATCH = 5
    WOOD = 6
    GLASS = 7
    MUD = 8
    STONE = 9


class WallMaterial(IntEnum):
    UNKNOWN = 0
    PLASTER = 1  # concrete / cement rendered
    BRICK = 2
    STONE = 3
    MUD = 4
    WOOD = 5
    BAMBOO = 6
    METAL = 7
    GLASS = 8


class PlaceKind(IntEnum):
    NONE = 0
    COUNTRY = 1
    PROVINCE = 2
    CITY = 3
    TOWN = 4
    VILLAGE = 5
    HAMLET = 6
    ISOLATED_DWELLING = 7
    SUBURB = 8
    NEIGHBOURHOOD = 9
    QUARTER = 10
    LOCALITY = 11
    FARM = 12
    SQUARE = 13
    ISLAND = 14
    DISTRICT = 15
    LOCAL_LEVEL = 16  # municipality / rural municipality
    WARD = 17


class PoiKind(IntEnum):
    NONE = 0
    # religious
    TEMPLE_HINDU = 100
    STUPA = 101
    GOMPA = 102
    SHRINE = 103
    MOSQUE = 104
    CHURCH = 105
    PLACE_OF_WORSHIP = 106
    CHORTEN = 107
    MANI_WALL = 108
    # heritage
    HERITAGE_SQUARE = 120
    PALACE = 121
    MONUMENT = 122
    RUINS = 123
    STONE_TAP = 124
    CITY_GATE = 125
    MUSEUM = 126
    # nature
    PEAK = 200
    VIEWPOINT = 201
    WATERFALL = 202
    CAVE = 203
    LAKE = 204
    HOT_SPRING = 205
    GLACIER = 206
    PASS = 207
    SPRING = 208
    RIVER = 209
    PARK = 210
    PROTECTED_AREA = 211
    NOTABLE_TREE = 212
    RIDGE = 213
    # transport
    AIRPORT = 300
    HELIPAD = 301
    BUS_STATION = 302
    FUEL = 303
    CABLE_CAR_STATION = 304
    RAILWAY_STATION = 305
    TAXI_STAND = 306
    BRIDGE = 307
    PARKING = 308
    # tourism and services
    HOTEL = 400
    GUEST_HOUSE = 401
    HOSTEL = 402
    CAMP_SITE = 403
    TEAHOUSE = 404  # alpine_hut, trekking lodge
    RESTAURANT = 405
    CAFE = 406
    ATTRACTION = 407
    SHOP = 408
    MARKETPLACE = 409
    INFORMATION = 410
    PICNIC_SITE = 411
    THEME_PARK = 412
    ZOO = 413
    # activities
    BUNGEE = 500
    PARAGLIDING = 501
    ZIPLINE = 502
    RAFTING = 503
    BOATING = 504
    SAFARI = 505
    # civic (ambient life, not search-prominent)
    SCHOOL = 600
    HOSPITAL = 601
    GOVERNMENT = 602
    BANK = 603

    # appended in ENUMS_VERSION 2 (values grouped with their families, assigned once)
    STATUE = 127  # tourism=artwork statues, historic=memorial statues
    GHAT = 128  # river ghats (steps to the water)
    PARAGLIDING_LANDING = 506


class AreaKind(IntEnum):
    NONE = 0
    WATER_LAKE = 1
    WATER_RIVER = 2  # natural=water + water=river, waterway=riverbank
    WATER_POND = 3  # pond, reservoir, basin
    GLACIER = 4
    WETLAND = 5
    FOREST = 6
    FARMLAND = 7
    ORCHARD = 8
    MEADOW = 9
    SCRUB = 10
    PARK = 11
    RESIDENTIAL = 12
    COMMERCIAL = 13
    INDUSTRIAL = 14
    RELIGIOUS = 15  # temple compounds: no vehicles
    PEDESTRIAN = 16  # squares, pedestrian areas
    SAND_SHINGLE = 17  # braided riverbeds
    BARE_ROCK = 18
    AERODROME = 19
    CEMETERY = 20
    MILITARY = 21
    PITCH = 22
    PROTECTED = 23
    TEA_GARDEN = 24
    GRASSLAND = 25
    SCREE = 26
    # appended in ENUMS_VERSION 2
    MARKETPLACE = 27
    PARKING = 28
    BUS_PARK = 29
    CAMPUS = 30
    KILN = 31
    QUARRY = 32
    STADIUM = 33
    GOLF = 34
    POOL = 35
    APRON = 36  # aeroway=apron (never a building)
    POWER_PLANT = 37
    COURTYARD = 38  # named bahal / bahi / chowk courtyards (W2_DESIGN D19)
    TRAFFIC_ISLAND = 39  # area:highway=traffic_island


class LineKind(IntEnum):
    NONE = 0
    RIVER = 1
    STREAM = 2
    CANAL = 3
    DITCH = 4
    RAILWAY = 5
    CABLE_CAR = 6
    CHAIR_LIFT = 7
    ZIP_LINE = 8
    RUNWAY = 9
    TAXIWAY = 10
    CITY_WALL = 11
    MANI_WALL = 12
    WATERFALL = 13  # waterway=waterfall drawn as a way
    # appended in ENUMS_VERSION 2 (reserved for D3; not emitted yet)
    TREE_ROW = 14
    CLIFF = 15
    GHAT_EDGE = 16
    POWER_LINE = 17
    WALL = 18
    FENCE = 19
    HEDGE = 20
    RETAINING_WALL = 21
    KERB = 22
    FERRY = 23
    DAM = 24
    WEIR = 25
    PENSTOCK = 26


class Biome(IntEnum):
    NONE = 0
    WATER = 1
    URBAN_DENSE = 2
    URBAN_GREEN = 3
    TERAI_PADDY = 4
    TERAI_CROPLAND = 5
    TERAI_SAL_FOREST = 6
    TERAI_GRASSLAND = 7
    RIVERBED_GRAVEL = 8
    CHURE_FOREST = 9
    HILL_TERRACES = 10
    HILL_FOREST = 11
    HILL_SCRUB = 12
    HILL_GRASSLAND = 13
    SUBALPINE_FOREST = 14
    ALPINE_MEADOW = 15
    ALPINE_SCRUB = 16
    SCREE_ROCK = 17
    MORAINE = 18
    GLACIER = 19
    SNOW = 20
    TRANS_HIMALAYAN_STEPPE = 21
    TRANS_HIMALAYAN_CROPLAND = 22
    ORCHARD = 23
    TEA_GARDEN = 24
    WETLAND = 25
    VALLEY_CROPLAND = 26
    BARE_SOIL = 27


class WorldCover(IntEnum):
    """ESA WorldCover 2021 v200 class codes."""

    NODATA = 0
    TREE_COVER = 10
    SHRUBLAND = 20
    GRASSLAND = 30
    CROPLAND = 40
    BUILT_UP = 50
    BARE_SPARSE = 60
    SNOW_ICE = 70
    PERMANENT_WATER = 80
    HERBACEOUS_WETLAND = 90
    MANGROVES = 95
    MOSS_LICHEN = 100


class StyleProfile(IntEnum):
    """Regional building style (W2_DESIGN 2.1); one per building in ``BFNT``."""

    NONE = 0
    KATHMANDU_CORE = 1
    THAMEL = 2
    PATAN = 3
    BHAKTAPUR = 4
    KIRTIPUR = 5
    THIMI = 6
    BUNGAMATI = 7
    KHOKANA = 8
    PANAUTI = 9
    METRO = 10
    RIM = 11
    BOUDHA_KORA = 12


class AreaType(IntEnum):
    """250 m area-type grid (street_life 1.1 + roads 1.2): ``RATR`` and ``BFNT``."""

    UNKNOWN = 0
    OLD_CORE = 1
    URBAN = 2
    PERI_URBAN = 3
    RURAL = 4
    HILL = 5
    FOREST = 6


class JunctionKind(IntEnum):
    """``JNCT`` record kind (W2_DESIGN 4.7)."""

    PLAIN = 0
    ROUNDABOUT = 1
    CIRCULAR = 2
    MINI_ROUNDABOUT = 3
    SIGNALS = 4
    POLICE = 5
    SYNTHETIC_ISLAND = 6


class Sidewalk(IntEnum):
    """``RATR.sidewalk``: OSM ``sidewalk*`` (left/right relative to the point order)."""

    UNKNOWN = 0
    NONE = 1
    LEFT = 2
    RIGHT = 3
    BOTH = 4
    SEPARATE = 5


class ObjectKind(IntEnum):
    """``PROP`` record kind: real OSM point objects (CONTENT_COVERAGE D3, W2_DESIGN 9.3)."""

    NONE = 0
    TREE = 1
    POWER_TOWER = 2
    POWER_POLE = 3
    STREET_LAMP = 4
    BUS_STOP = 5
    SHELTER = 6
    BENCH = 7
    WATER_TAP = 8
    WELL = 9
    GATE = 10
    CHIMNEY = 11
    MAST = 12
    TOWER = 13
    STORAGE_TANK = 14
    SOLAR_PANEL = 15
    ARTWORK = 16
    TRAFFIC_SIGNALS = 17
    CROSSING_MARKED = 18
    CROSSING_UNMARKED = 19
    AEROWAY_GATE = 20
    PARKING_POSITION = 21
    WINDSOCK = 22
    HELIPAD = 23
    TAXI_STAND = 24


class TreeClass(IntEnum):
    """``PROP.subtype`` of a TREE (species class from species/genus/leaf_type/name)."""

    UNKNOWN = 0
    PIPAL = 1  # Ficus religiosa
    BAR = 2  # Ficus benghalensis
    BROADLEAF = 3
    CONIFER = 4
    PALM = 5


class TransitMode(IntEnum):
    """Route mode in ``.ghrt`` (D2 + D22)."""

    NONE = 0
    BUS = 1
    MICROBUS = 2
    TEMPO = 3
    SHARE_TAXI = 4
    HIKING = 5
    FOOT = 6
    BICYCLE = 7
    MTB = 8


class LiveryClass(IntEnum):
    """Generic livery a route's vehicles use (W2_DESIGN 5.3; never an operator's livery)."""

    NONE = 0
    CITY_GREEN = 1
    MINIBUS = 2
    MICROBUS = 3
    SAFA_TEMPO = 4
    COACH = 5


class TurnRestriction(IntEnum):
    """``type=restriction`` relations in ``.ghrt``."""

    NONE = 0
    NO_LEFT_TURN = 1
    NO_RIGHT_TURN = 2
    NO_STRAIGHT_ON = 3
    NO_U_TURN = 4
    ONLY_LEFT_TURN = 5
    ONLY_RIGHT_TURN = 6
    ONLY_STRAIGHT_ON = 7
    NO_ENTRY = 8
    NO_EXIT = 9


class HeritageKind(IntEnum):
    """Curated hero record kind (``.ghcd``; W2_DESIGN 9.4)."""

    NONE = 0
    PAGODA = 1
    SHIKHARA_STONE = 2
    SHIKHARA_PLASTER = 3
    STUPA = 4
    HOUSE_TEMPLE = 5
    MANDAPA = 6
    RELIEF = 7
    COLUMN = 8
    GATE = 9
    PALACE = 10
    TOWER = 11
    HITI = 12
    POKHARI = 13
    GOMPA = 14
    BAHAL = 15


class HeritageFinish(IntEnum):
    """Main finish of a hero (``.ghcd``)."""

    UNKNOWN = 0
    TILE = 1
    GILT_TOP = 2
    GILT_ALL = 3
    WHITEWASH = 4
    STONE = 5
    BRICK = 6
    STUCCO = 7
    TERRACOTTA = 8


class EntryRule(IntEnum):
    """Non-blocking info card shown at a compound entrance (W2-O1; never blocks the player)."""

    NONE = 0
    SHOES_OFF = 1
    QUIET_WORSHIP = 2
    REAL_COMPOUND_CLOSED_TO_NON_HINDUS = 3
    INTERIOR_NO_PHOTO = 4
    KUMARI_NOT_SHOWN = 5
    NO_LEATHER = 6  # ENUMS_VERSION 3: the Golden Temple's "no leather in the courtyard" (temples.md section 9)


class KoraDirection(IntEnum):
    NONE = 0
    CLOCKWISE = 1
    ANTICLOCKWISE = 2  # Bon sites


class SacredZoneKind(IntEnum):
    """Kind of a sacred or heritage zone (``SacredZoneIndex``, D14 routing)."""

    NONE = 0
    COMPOUND = 1
    COURTYARD = 2
    HERITAGE_SQUARE = 3
    STUPA_KORA = 4
    GHAT = 5


class Travel(IntFlag):
    """Travel profiles; bit set in a routing edge's access mask."""

    FOOT = 1 << 0
    BICYCLE = 1 << 1
    MOTORBIKE = 1 << 2
    CAR = 1 << 3  # taxi, hatchback
    JEEP = 1 << 4  # 4x4, shared jeep, SUV
    BUS = 1 << 5  # bus, truck, tanker
    HORSE = 1 << 6  # horse, mule, yak


ALL_TRAVEL = Travel(0x7F)
MOTOR_TRAVEL = Travel.MOTORBIKE | Travel.CAR | Travel.JEEP | Travel.BUS


class RoadFlags(IntFlag):
    ONEWAY = 1 << 0  # traffic flows in digitised direction only
    BRIDGE = 1 << 1
    TUNNEL = 1 << 2
    HAS_PREV_CTX = 1 << 3  # first point is context only (outside tile)
    HAS_NEXT_CTX = 1 << 4  # last point is context only
    LINK = 1 << 5  # *_link ramp
    FORD = 1 << 6
    SAC_INFERRED = 1 << 7  # sac_scale value was inferred, not tagged


class BuildingFlags(IntFlag):
    LEVELS_INFERRED = 1 << 0
    HEIGHT_TAGGED = 1 << 1
    ROOF_TAGGED = 1 << 2
    LANDMARK = 1 << 3  # hidden: a hero replica stands here (inside a D5 hide zone)
    PART = 1 << 4  # building:part
    HAS_PARTS = 1 << 5  # a building whose building:part records describe its shape
    TAG_SUSPECT = 1 << 6  # tagged height or levels failed the plausibility gate (height kept, see buildings.py)
    OPEN_CANOPY = 1 << 7  # open-sided roof (building=roof)


class PoiFlags(IntFlag):
    DISCOVERABLE = 1 << 0
    LANDMARK = 1 << 1
    HAS_ELE = 1 << 2
    SACRED = 1 << 3  # no vehicles, walk clockwise etc.
    HAS_FOOTPRINT = 1 << 4  # a building footprint contains this POI (it passed its use to it)
    LANDMARK_LITE = 1 << 5
    INFERRED = 1 << 6  # kind or position inferred, not tagged


class RoadAttrFlags(IntFlag):
    """``RATR.flags`` (W2_DESIGN 9.3)."""

    DUAL = 1 << 0  # paired with an opposite one-way carriageway (partner_way_id)
    SERVICE_ROAD = 1 << 1  # Ring Road service carriageway
    HERITAGE_PEDESTRIAN = 1 << 2  # inside a heritage square or sacred compound: walk and cycle only
    NO_MOTOR = 1 << 3  # access/motor_vehicle=no
    LIT = 1 << 4
    BUS_ROUTE = 1 << 5  # member of a bus/microbus/tempo route relation
    RING_MEMBER = 1 << 6  # junction=roundabout|circular
    PAINTABLE = 1 << 7  # real width >= 5.5 m and sealed: centre line allowed


class JunctionFlags(IntFlag):
    """``JNCT.flags`` (W2_DESIGN 9.3; bits 4-5 added by the pipeline)."""

    HAS_ISLAND_AREA = 1 << 0
    OFFICERS_2_4 = 1 << 1
    HERITAGE_NO_MOTOR = 1 << 2
    CROSSINGS_MARKED = 1 << 3
    HAS_POLICE = 1 << 4  # a curated police chowk (chowks.yaml), whatever the kind
    HAS_SIGNALS = 1 << 5  # an OSM traffic_signals node at the junction


class BuildingFrontFlags(IntFlag):
    """``BFNT.flags`` (W2_DESIGN 9.3)."""

    COURTYARD_HOST = 1 << 0
    CORNER = 1 << 1
    FACES_HERITAGE_SQUARE = 1 << 2
    RANA_HINT = 1 << 3
    STRUCTURE_RCC = 1 << 4
    STRUCTURE_MUD = 1 << 5
    ROOF_FLAT_TAGGED = 1 << 6


class PropFlags(IntFlag):
    """``PROP.flags``."""

    YAW = 1 << 0  # yaw_cdeg is valid
    HEIGHT_TAGGED = 1 << 1
    CHAUTARI = 1 << 2  # a tree on a chautari platform
    FROM_WAY = 1 << 3  # position derived from a way (centroid or stand end)
    ON_ROAD = 1 << 4  # a node of a ROAD way (signals, crossings)


class RouteFlags(IntFlag):
    """``.ghrt`` route flags."""

    ROUNDTRIP = 1 << 0
    STOPS_FROM_MEMBERS = 1 << 1
    STOPS_INFERRED = 1 << 2
    HAS_GAPS = 1 << 3  # the way chain is broken somewhere
    MISSING_WAYS = 1 << 4  # member ways absent from the region's roads


class StopFlags(IntFlag):
    """``.ghrt`` stop flags."""

    FROM_MEMBER = 1 << 0
    INFERRED = 1 << 1
    TERMINAL = 1 << 2


class HeritageFlags(IntFlag):
    """``.ghcd`` heritage record flags."""

    MANUAL_POSITION = 1 << 0  # no OSM anchor: curated lon/lat
    HAS_COMPOUND = 1 << 1
    WALKABLE_COMPOUND = 1 << 2  # W2-O1
    NO_VEHICLES = 1 << 3
    SANCTUM_CLOSED = 1 << 4  # always set: sanctums are never entered or modelled
    VERIFY = 1 << 5  # some attribute still needs a human check [V]


# ---------------------------------------------------------------------------
# Intermediate feature types (pipeline-internal; positions in lon/lat degrees)
# ---------------------------------------------------------------------------
@dataclass(slots=True)
class NameRec:
    default: str = ""  # OSM name=*
    en: str = ""  # name:en, else int_name, else romanised default
    ne: str = ""  # name:ne, else default if it is Devanagari
    alt: tuple[str, ...] = ()  # alt_name, old_name, ... (search only)

    def is_empty(self) -> bool:
        return not (self.default or self.en or self.ne)


@dataclass(slots=True)
class RoadFeature:
    osm_id: int
    cls: RoadClass
    lonlat: np.ndarray  # (N, 2) float64
    node_ids: np.ndarray  # (N,) int64, for routing topology
    is_link: bool = False
    surface: Surface = Surface.UNKNOWN
    surface_source: SurfaceSource = SurfaceSource.DEFAULT
    surface_raw: str | None = None
    tracktype: int = 0  # 1..5, 0 unknown
    smoothness: str | None = None
    width_m: float | None = None
    lanes: int = 0
    oneway: int = 0  # 0 both, 1 forward, -1 reverse
    bridge: bool = False
    tunnel: bool = False
    ford: bool = False
    layer: int = 0
    sac_scale: SacScale = SacScale.UNKNOWN
    sac_inferred: bool = False
    trail_visibility: int = 0  # 0 unknown, 1 excellent .. 6 no
    access: Travel = ALL_TRAVEL
    name: NameRec | None = None
    ref: str | None = None
    extra: dict[str, str] = field(default_factory=dict)  # osm_extract.ROAD_TAG_KEYS subset (RATR, D17)


@dataclass(slots=True)
class BuildingFeature:
    osm_type: str  # "w" or "r"
    osm_id: int
    outer: np.ndarray  # (N, 2) lon/lat, counter-clockwise, not closed
    holes: list[np.ndarray] = field(default_factory=list)  # clockwise
    use: BuildingUse = BuildingUse.UNKNOWN
    building_raw: str = "yes"
    levels: float | None = None
    height_m: float | None = None
    min_height_m: float | None = None
    roof_shape: RoofShape = RoofShape.UNKNOWN
    roof_material: RoofMaterial = RoofMaterial.UNKNOWN
    wall_material: WallMaterial = WallMaterial.UNKNOWN
    archetype: BuildingArchetype = BuildingArchetype.GENERIC
    flags: BuildingFlags = BuildingFlags(0)
    religion: str | None = None
    name: NameRec | None = None
    extra: dict[str, str] = field(default_factory=dict)  # osm_extract.BUILDING_TAG_KEYS subset (D4, D19)


@dataclass(slots=True)
class PoiFeature:
    osm_type: str  # "n", "w" or "r"
    osm_id: int
    kind: PoiKind
    lon: float
    lat: float
    name: NameRec | None = None
    ele_m: float | None = None
    flags: PoiFlags = PoiFlags(0)
    importance: float = 0.0  # 0..1, used for search ranking and map labels
    tags: dict[str, str] = field(default_factory=dict)  # small whitelisted subset


@dataclass(slots=True)
class PlaceFeature:
    osm_type: str
    osm_id: int
    kind: PlaceKind
    lon: float
    lat: float
    name: NameRec
    population: int | None = None
    importance: float = 0.0


@dataclass(slots=True)
class AreaFeature:
    osm_type: str
    osm_id: int
    kind: AreaKind
    polygon: object  # shapely Polygon or MultiPolygon in lon/lat
    name: NameRec | None = None
    tags: dict[str, str] = field(default_factory=dict)


@dataclass(slots=True)
class LineFeature:
    osm_id: int
    kind: LineKind
    lonlat: np.ndarray  # (N, 2)
    width_m: float | None = None
    name: NameRec | None = None
    tags: dict[str, str] = field(default_factory=dict)


@dataclass(slots=True)
class AdminArea:
    osm_id: int  # relation id
    level: PlaceKind  # PROVINCE, DISTRICT, LOCAL_LEVEL, WARD
    admin_level: int  # raw OSM admin_level
    name: NameRec
    polygon: object  # shapely (Multi)Polygon in lon/lat


@dataclass(slots=True)
class PropFeature:
    """A real OSM point object (``PROP`` chunk; CONTENT_COVERAGE D3, W2 PROP subset)."""

    osm_type: str  # "n" or "w"
    osm_id: int
    kind: ObjectKind
    lon: float
    lat: float
    subtype: int = 0  # TREE: TreeClass
    flags: PropFlags = PropFlags(0)
    yaw_deg: float | None = None  # bearing clockwise from north
    height_m: float | None = None
    name: NameRec | None = None
    ref: str | None = None


@dataclass(slots=True)
class JunctionNode:
    """A tagged node that shapes a junction: ``highway=traffic_signals`` or ``mini_roundabout``."""

    osm_id: int
    lon: float
    lat: float
    kind: str  # "traffic_signals" | "mini_roundabout"


@dataclass(slots=True)
class RouteMember:
    type: str  # "n", "w" or "r"
    ref: int
    role: str
    lon: float | None = None  # node members: position when the extract saw the node
    lat: float | None = None
    name: NameRec | None = None


@dataclass(slots=True)
class RouteFeature:
    """A ``type=route`` relation (D2): bus, microbus, tempo, share taxi, hiking, foot, bicycle, mtb."""

    osm_id: int
    route: str  # the raw route=* value
    tags: dict[str, str] = field(default_factory=dict)
    members: list[RouteMember] = field(default_factory=list)


@dataclass(slots=True)
class RestrictionFeature:
    """A ``type=restriction`` relation (D2): ``from`` way, ``via`` node or way, ``to`` way."""

    osm_id: int
    kind: TurnRestriction
    from_way: int
    to_way: int
    via_node: int = 0
    via_way: int = 0


@dataclass(slots=True)
class Extract:
    """Everything the OSM stage produces for one region."""

    region: str
    roads: list[RoadFeature] = field(default_factory=list)
    buildings: list[BuildingFeature] = field(default_factory=list)
    pois: list[PoiFeature] = field(default_factory=list)
    places: list[PlaceFeature] = field(default_factory=list)
    areas: list[AreaFeature] = field(default_factory=list)
    lines: list[LineFeature] = field(default_factory=list)
    admin: list[AdminArea] = field(default_factory=list)
    stats: dict = field(default_factory=dict)
    # W2 (ENUMS_VERSION 2)
    props: list[PropFeature] = field(default_factory=list)
    junction_nodes: list[JunctionNode] = field(default_factory=list)
    routes: list[RouteFeature] = field(default_factory=list)
    restrictions: list[RestrictionFeature] = field(default_factory=list)
    anchor_nodes: dict[int, tuple[float, float, NameRec | None]] = field(default_factory=dict)  # curated anchors


ALL_ENUMS = (
    RoadClass, Surface, SurfaceGroup, SurfaceSource, SacScale, BuildingUse, BuildingArchetype,
    RoofShape, RoofMaterial, WallMaterial, PlaceKind, PoiKind, AreaKind, LineKind, Biome, WorldCover,
    Travel, RoadFlags, BuildingFlags, PoiFlags,
    # ENUMS_VERSION 2
    StyleProfile, AreaType, JunctionKind, Sidewalk, ObjectKind, TreeClass, TransitMode, LiveryClass, TurnRestriction,
    HeritageKind, HeritageFinish, EntryRule, KoraDirection, SacredZoneKind,
    RoadAttrFlags, JunctionFlags, BuildingFrontFlags, PropFlags, RouteFlags, StopFlags, HeritageFlags,
)


def enums_json() -> dict:
    return {
        "version": ENUMS_VERSION,
        "enums": {e.__name__: {m.name: int(m.value) for m in e} for e in ALL_ENUMS},
        "flag_enums": [e.__name__ for e in ALL_ENUMS if issubclass(e, IntFlag)],
        "surface_group": {s.name: SURFACE_GROUP[s].name for s in Surface},
    }


if __name__ == "__main__":
    json.dump(enums_json(), sys.stdout, indent=1)
    sys.stdout.write("\n")
