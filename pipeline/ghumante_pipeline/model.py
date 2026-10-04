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

ENUMS_VERSION = 1


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
    LANDMARK = 1 << 3  # replaced by a hand-made hero asset at runtime
    PART = 1 << 4  # building:part


class PoiFlags(IntFlag):
    DISCOVERABLE = 1 << 0
    LANDMARK = 1 << 1
    HAS_ELE = 1 << 2
    SACRED = 1 << 3  # no vehicles, walk clockwise etc.


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


ALL_ENUMS = (
    RoadClass, Surface, SurfaceGroup, SurfaceSource, SacScale, BuildingUse, BuildingArchetype,
    RoofShape, RoofMaterial, WallMaterial, PlaceKind, PoiKind, AreaKind, LineKind, Biome, WorldCover,
    Travel, RoadFlags, BuildingFlags, PoiFlags,
)


def enums_json() -> dict:
    return {
        "version": ENUMS_VERSION,
        "enums": {e.__name__: {m.name: int(m.value) for m in e} for e in ALL_ENUMS},
        "surface_group": {s.name: SURFACE_GROUP[s].name for s in Surface},
    }


if __name__ == "__main__":
    json.dump(enums_json(), sys.stdout, indent=1)
    sys.stdout.write("\n")
