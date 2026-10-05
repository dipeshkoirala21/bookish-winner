#!/usr/bin/env python3
"""Ghumante OSM data census: one run over pipeline/data/raw/osm/nepal.osm.pbf.

Reads relations first (relations-only read, needed to know which ways are
route / multipolygon members), then ONE nodes+ways pass with a node-location
index. Counts every category for Nepal, the Kathmandu Valley bbox, the
kathmandu_core bbox and the kathmandu_valley pack bbox, plus road curvature,
bridges, routes, waterfalls, adventure spots, name keywords and what the
pipeline's own classifiers (tags.poi_kind / area_kind / line_kind) would make
of all of Nepal. Then reads the built packs (QA GeoJSON decoded from the pack,
and the kathmandu_core pack decoded directly) to record what reached them.

Usage:
  python3 census.py                # full run (scan, pickle, post-process)
  python3 census.py --test 300000  # debug: stop after N ways, no relations
  python3 census.py --post         # post-process again from the pickle
"""
from __future__ import annotations

import argparse
import gc
import heapq
import json
import math
import os
import pickle
import re
import sys
import time
from collections import Counter, defaultdict

REPO = os.environ.get("GHUMANTE_REPO", os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..")))
sys.path.insert(0, f"{REPO}/pipeline")

import numpy as np  # noqa: E402
import osmium  # noqa: E402

from ghumante_pipeline import tags as T  # noqa: E402
from ghumante_pipeline.model import AreaKind, LineKind, PlaceKind, PoiKind  # noqa: E402
from ghumante_pipeline.osm_extract import build_rings  # noqa: E402

OUT = os.environ.get("GHUMANTE_CENSUS_OUT", os.path.join(REPO, "pipeline", "build", "content_census"))
PBF = f"{REPO}/pipeline/data/raw/osm/nepal.osm.pbf"
PICKLE = f"{OUT}/census_state.pkl"

SCOPES = ("nepal", "valley", "core", "pack")
BOXES = {
    "valley": [85.18, 27.57, 85.56, 27.82],
    "core": [85.283, 27.690, 85.375, 27.735],
    "pack": [85.18, 27.55, 85.58, 27.83],  # kathmandu_valley pack bbox, for pack-vs-OSM comparison
}
S_N, S_P, S_PV, S_PVC = (0,), (0, 3), (0, 3, 1), (0, 3, 1, 2)


def scopes_of(lon: float, lat: float) -> tuple:
    if 85.18 <= lon <= 85.58 and 27.55 <= lat <= 27.83:
        if lon <= 85.56 and 27.57 <= lat <= 27.82:
            if 85.283 <= lon <= 85.375 and 27.690 <= lat <= 27.735:
                return S_PVC
            return S_PV
        return S_P
    return S_N


def near_pack(lon: float, lat: float) -> bool:
    return abs(lon - 85.38) < 1.2 and abs(lat - 27.69) < 1.2


KX, KY = 111.320, 110.574  # km per degree (lon at equator, lat)
D2R = math.pi / 180.0
R2D = 180.0 / math.pi

# ----------------------------------------------------------------------------
# Category definitions
# ----------------------------------------------------------------------------
CAT_KEYS = (
    "place", "tourism", "historic", "natural", "waterway", "landuse", "leisure", "boundary", "aerialway",
    "railway", "aeroway", "power", "man_made", "barrier", "sport", "attraction", "amenity", "shop", "office",
    "craft", "emergency", "healthcare", "heritage", "whitewater", "mountain_pass", "highway", "building",
    "place_of_worship", "public_transport", "military", "geological", "hazard", "ford", "club", "climbing",
    "route", "piste:type", "cave", "bridge:structure", "artwork_type", "memorial", "denomination", "advertising",
)
COUNT_ONLY_KEYS = {"shop", "office", "craft", "denomination"}
LINEAR_KEYS = {"highway", "waterway", "railway", "power", "aerialway", "barrier", "man_made", "natural", "aeroway",
               "historic", "route", "piste:type"}
QUALIFIED = {
    ("amenity", "place_of_worship"): "religion",
    ("amenity", "shelter"): "shelter_type",
    ("man_made", "tower"): "tower:type",
    ("man_made", "mast"): "tower:type",
    ("tourism", "information"): "information",
    ("natural", "water"): "water",
    ("natural", "wetland"): "wetland",
    ("boundary", "administrative"): "admin_level",
    ("boundary", "protected_area"): "protect_class",
    ("aeroway", "aerodrome"): "aerodrome:type",
    ("leisure", "pitch"): "sport",
    ("historic", "memorial"): "memorial",
    ("tourism", "artwork"): "artwork_type",
}
FEATURE_KEYS = (set(CAT_KEYS) - {"building"}) | {"name", "name:en", "name:ne", "wikidata", "int_name"}
BUILDING_SPECIAL = {
    "temple", "stupa", "shrine", "monastery", "church", "mosque", "chapel", "cathedral", "gompa", "palace",
    "chorten", "pagoda", "castle", "fort", "ruins", "tower", "religious", "synagogue", "mandir", "gumba",
    "gurdwara", "bridge", "minaret", "bell_tower",
}
AREA_KEYS = {"landuse", "leisure", "amenity", "tourism", "place", "boundary", "aeroway", "building", "historic",
             "military", "shop", "office", "craft", "healthcare", "emergency", "sport", "public_transport",
             "area:highway", "man_made", "power", "waterway", "natural", "attraction", "heritage", "glacier:type"}
LINEAR_NATURAL = {"cliff", "ridge", "tree_row", "coastline", "arete", "valley", "earth_bank", "gully", "gorge",
                  "strait"}
LINEAR_MANMADE = {"embankment", "pipeline", "dyke", "breakwater", "groyne", "cutline", "goods_conveyor",
                  "mani_wall", "power_line", "cable"}
WATERWAY_AREAS = {"riverbank", "dock", "boatyard", "dam", "weir", "reservoir", "pond", "basin", "waterfall",
                  "rapids", "fish_pass", "lock"}
HIST_LINEAR = {"city_wall", "citywalls", "wall", "mani_wall", "aqueduct"}

# Full item lists (named items unless the category is in RARE_KEEP_UNNAMED)
FULL_NEPAL = {
    "natural=waterfall", "waterway=waterfall", "natural=hot_spring", "natural=cave_entrance", "natural=cave",
    "natural=volcano", "natural=glacier", "natural=saddle", "mountain_pass=yes", "natural=peak", "natural=ridge",
    "natural=arete", "natural=cliff", "natural=gorge", "natural=valley", "natural=rock", "natural=stone",
    "waterway=rapids", "tourism=viewpoint", "tourism=theme_park", "tourism=zoo", "tourism=museum",
    "tourism=alpine_hut", "tourism=wilderness_hut", "historic=castle", "historic=palace", "historic=city_gate",
    "historic=fort", "historic=archaeological_site", "historic=monument", "historic=ruins",
    "aeroway=aerodrome", "aeroway=heliport", "sport=climbing", "sport=paragliding", "sport=free_flying",
    "sport=rafting", "sport=canoe", "sport=kayak", "sport=bungee", "sport=mountain_biking", "sport=golf",
    "sport=motocross", "sport=horse_riding", "sport=skiing", "leisure=nature_reserve", "boundary=national_park",
    "boundary=protected_area", "railway=station", "railway=halt", "natural=water|water=lake", "heritage=1",
    "heritage=2", "heritage=3", "heritage=4", "man_made=stupa", "building=stupa", "building=palace",
    "building=castle", "building=fort", "leisure=water_park", "leisure=golf_course", "tourism=camp_site",
    "attraction=*", "whitewater=*", "geological=*", "man_made=tower|tower:type=observation", "historic=memorial",
    "historic=temple", "natural=tree", "climbing=*", "cave=*", "hazard=*", "historic=fortification",
    "tourism=attraction",
}
FULL_VALLEY_PREFIX = ("tourism=", "historic=", "natural=", "heritage=", "aerialway=", "aeroway=", "sport=",
                      "attraction=", "man_made=stupa", "man_made=tower", "man_made=water_tap", "building=",
                      "amenity=place_of_worship|", "amenity=fountain", "amenity=drinking_water", "place=square",
                      "leisure=park", "leisure=garden", "leisure=nature_reserve", "leisure=stadium",
                      "waterway=waterfall", "waterway=dam", "whitewater=", "mountain_pass=", "railway=",
                      "barrier=city_wall", "amenity=theatre", "amenity=arts_centre", "amenity=monastery",
                      "leisure=water_park", "leisure=amusement_arcade", "amenity=marketplace",
                      "amenity=bus_station", "landuse=religious", "place_of_worship=")
VALLEY_EXCLUDE = ("building=yes", "building=house", "natural=tree", "tourism=information", "natural=water|",
                  "tourism=hotel", "tourism=guest_house", "tourism=hostel", "tourism=apartment", "tourism=motel",
                  "tourism=yes", "building=residential", "building=commercial", "building=school")
RARE_KEEP_UNNAMED = {"natural=waterfall", "waterway=waterfall", "natural=hot_spring", "natural=cave_entrance",
                     "natural=cave", "natural=volcano", "waterway=rapids", "aerialway=*", "whitewater=*",
                     "sport=paragliding", "sport=free_flying", "sport=rafting", "sport=bungee",
                     "tourism=theme_park", "tourism=zoo", "heritage=1"}
FULL_CAP = 700

CURV_CLASSES = {"trunk", "primary", "secondary", "tertiary", "unclassified", "track"}
TRAIL_CLASSES = {"path", "footway", "steps", "bridleway", "track", "pedestrian", "cycleway", "via_ferrata"}
FOOT_CLASSES = {"footway", "path", "steps", "bridleway", "pedestrian", "cycleway"}

ADV_SPORTS = {"climbing", "paragliding", "free_flying", "bungee", "bungee_jumping", "canoe", "kayak", "rafting",
              "whitewater", "mountain_biking", "motocross", "golf", "horse_riding", "skiing", "karting",
              "zipline", "zip_line", "canyoning", "hang_gliding", "parachuting", "skydiving", "rowing",
              "ice_climbing", "bouldering", "via_ferrata", "hiking", "trekking", "orienteering", "archery",
              "fishing", "motor", "cycling", "free_climbing", "climbing_adventure", "mountaineering", "boating",
              "swimming", "water_ski", "ultralight", "safari", "sailing", "scuba_diving"}
ADV_LEISURE = {"water_park", "golf_course", "miniature_golf", "horse_riding", "fishing", "bird_hide",
               "summer_camp", "ice_rink", "amusement_arcade", "climbing", "high_ropes_course", "adventure_park",
               "trampoline_park", "sports_centre_adventure"}
ADV_RX = re.compile(
    r"bunge+|bungy|zip\s*-?\s*lin|zip\s*-?\s*flyer|zipflyer|paraglid|para\s*-?\s*motor|rafting|\braft\b|kayak|"
    r"canyon|rock\s*climb|climbing|\bclimb\b|ultra\s*-?\s*light|sky\s*-?\s*div|mountain\s*bik|\bmtb\b|"
    r"giant\s*swing|cable\s*car|gondola|ropeway|rope\s*way|safari|boating|horse\s*rid|camping|adventure|"
    r"go\s*-?\s*kart|karting|hot\s*air\s*balloon|mountain\s*flight|sky\s*walk|skywalk|tower\s*jump|\batv\b|"
    r"off\s*-?\s*road|high\s*rope|canopy|via\s*ferrata|abseil|rappel|\bswing\b|tandem")

DB = r"(?<![ऀ-ॿ])"  # Devanagari "word start"
KEYWORDS = {
    "waterfall": r"jhar[a]?n[a]?\b|jharna|jharana|chhahar|chahar[a]?\b|waterfall|\bfalls?\b|झरना|छहरा|छाँगो|छाङ्गो",
    "cave": r"\bcaves?\b|\bgufa\b|\bgupha\b|\bguffa\b|गुफा",
    "hot_spring": r"tatopani|tato\s*pani|hot\s*spring|तातोपानी",
    "lake_pond": rf"\blake\b|\btaal?\b|\bdaha\b|pokhari|\bkunda?\b|{DB}ताल|{DB}दह\b|पोखरी|{DB}कुण्ड",
    "river_khola": r"\bkhola\b|\bnadi\b|\briver\b|खोला|नदी",
    "hill_danda": rf"\bdand[ae]\b|\bdada\b|thumk[ao]|\bdhuri\b|\blekh\b|डाँडा|{DB}डाडा|थुम्को|{DB}लेक\b",
    "pass_bhanjyang": r"bhanjy[a]?ng|bhanjang|deurali|\bpass\b|\bla\b|भञ्ज्याङ|भन्ज्याङ|देउराली",
    "himal_peak": r"\bhimal\b|\bpeak\b|\bchuli\b|\bhill\b|हिमाल|चुली",
    "glacier": r"glacier|हिमनदी",
    "viewpoint": r"view\s*point|view\s*tower|view\s*deck|भ्यू|भ्यु",
    "fort_gadhi_kot": rf"\bgadhi\b|\bgarhi\b|\bkot\b|\bfort\b|गढी|{DB}कोट\b",
    "durbar_palace": r"durbar|darbar|palace|दरबार",
    "temple_mandir": r"mandir|\btemple\b|मन्दिर|मंदिर",
    "stupa_chaitya": r"stupa|chaitya|\bchaity|स्तूप|स्तुप|चैत्य",
    "gompa_monastery": r"\bgomp?a\b|gumba|gonpa|monastery|गुम्बा",
    "ghat": r"\bghat\b|घाट",
    "bahal_baha_courtyard": r"\bbahal\b|\bbaha\b|\bbahi\b|बहाल",
    "hiti_dhunge_dhara": rf"\bhiti\b|dhunge\s*dhara|\bdhara\b|हिटी|{DB}धारा",
    "pati_sattal_chautara": rf"\bpati\b|\bpaati\b|sattal|chautar[ai]|{DB}पाटी|सत्तल|चौतारा|चौतारी",
    "chowk_square": r"\bchowk\b|\bchok\b|\bsquare\b|चोक",
    "bridge_pul": r"jholung|\bpul\b|bridge|पुल\b|झोलुङ्गे",
    "forest_ban": rf"forest|jungle|\bban\b|{DB}वन\b|जङ्गल",
    "park_garden": r"\bpark\b|garden|उद्यान|बगैंचा|पार्क",
    "trek_trail": r"\btrek|\btrail\b|circuit|base\s*camp",
    "tower_stambha": r"\btower\b|स्तम्भ|धरहरा",
    "adventure": ADV_RX.pattern,
}
KW_RX = {k: re.compile(v) for k, v in KEYWORDS.items()}
KW_ANY = re.compile("|".join(f"(?:{v})" for v in KEYWORDS.values()))
WATERFALL_RX = KW_RX["waterfall"]

FAMOUS_TREKS = {
    "Annapurna Circuit": r"annapurna\s*circuit|thorong|thorung",
    "Annapurna Base Camp / Sanctuary": r"annapurna\s*base\s*camp|annapurna\s*sanctuary|\babc\b|machh?apuchh?re\s*base",
    "Poon Hill / Ghorepani": r"poon\s*hill|poonhill|ghorepani",
    "Mardi Himal": r"\bmardi\b",
    "Khopra Danda": r"khopra",
    "Everest Base Camp / Khumbu": r"everest\s*base\s*camp|\bebc\b|kala\s*patt?h?ar|gorak\s*shep|khumbu",
    "Gokyo / Three Passes": r"gokyo|three\s*pass|\bcho\s*la\b|renjo|kongma",
    "Langtang Valley": r"langtang|kyanjin",
    "Gosaikunda / Helambu": r"gosai\s*kunda?|gosain\s*kunda?|helambu|laurebina",
    "Manaslu Circuit": r"manaslu|larkya|larke",
    "Tsum Valley": r"\btsum\b",
    "Upper Mustang": r"upper\s*mustang|lo\s*-?\s*manthang",
    "Nar Phu": r"\bnar\s*-?\s*phu|narphu",
    "Kanchenjunga": r"kan?ch[ae]n\s*d?z?jun?ga|kangchenjunga|khangchendzonga|pang\s*pema|yalung|oktang",
    "Makalu Base Camp": r"makalu",
    "Rara Lake": r"\brara\b",
    "Dolpo / Phoksundo": r"dolpo|phoksundo|shey\s*gompa",
    "Great Himalaya Trail": r"great\s*himalaya",
    "Tamang Heritage Trail": r"tamang\s*heritage",
    "Chisapani / Nagarkot / Valley Rim": r"chisapani|nagarkot|valley\s*rim|shivapuri",
    "Pikey Peak": r"pikey",
    "Dhorpatan": r"dhorpatan",
    "Api / Saipal": r"\bapi\s*(?:base|himal)|saipal",
    "Ruby Valley / Ganesh Himal": r"ruby\s*valley|ganesh\s*himal",
    "Jiri": r"\bjiri\b",
    "Royal Trek": r"royal\s*trek",
    "Panch Pokhari": r"panch\s*pokhari",
    "Kalinchowk": r"kalinchowk|kalinchok",
    "Limi Valley": r"\blimi\b",
    "Mohare Danda": r"mohare",
    "Champadevi / Chandragiri": r"champa\s*devi|chandragiri",
}
FAMOUS_RX = {k: re.compile(v) for k, v in FAMOUS_TREKS.items()}
FAMOUS_ANY = re.compile("|".join(f"(?:{v})" for v in FAMOUS_TREKS.values()))

EXN = 15
_NUM_RX = re.compile(r"\s*(-?\d+(?:[.,]\d+)?)")


def normv(v: str) -> str:
    v = v.strip().lower()
    return v[:48] if len(v) > 48 else v


def num(v) -> float:
    if not v:
        return 0.0
    m = _NUM_RX.match(v)
    if not m:
        return 0.0
    try:
        return float(m.group(1).replace(",", "."))
    except ValueError:
        return 0.0


def osm_ref(gi: int, oid: int) -> str:
    return "nwr"[gi] + str(oid)


# ----------------------------------------------------------------------------
# Stat accumulator
# ----------------------------------------------------------------------------
F_NODE, F_WAY, F_REL, F_NAMED, F_NE, F_EN, F_WD, F_ELE = range(8)


class Stat:
    __slots__ = ("c", "km", "km2", "ex")

    def __init__(self):
        self.c = [[0] * 8 for _ in range(4)]
        self.km = [0.0] * 4
        self.km2 = [0.0] * 4
        self.ex = [[], []]

    def add(self, gi, scopes, fl, lens, area, ekey, item):
        c = self.c
        for s in scopes:
            cs = c[s]
            cs[gi] += 1
            cs[3] += fl[0]
            cs[4] += fl[1]
            cs[5] += fl[2]
            cs[6] += fl[3]
            cs[7] += fl[4]
            if area:
                self.km2[s] += area
        if lens is not None:
            km = self.km
            km[0] += lens[0]
            km[1] += lens[1]
            km[2] += lens[2]
            km[3] += lens[3]
        if item is not None:
            _push(self.ex[0], ekey, item)
            if len(scopes) > 2:
                _push(self.ex[1], ekey, item)

    def to_json(self, examples=15):
        if examples is True:
            examples = OUT_EX[0]
        examples = int(examples or 0)
        out = {}
        for si, s in enumerate(SCOPES):
            cs = self.c[si]
            tot = cs[0] + cs[1] + cs[2]
            d = {"total": tot}
            if tot and si < 2:
                d.update({"n": cs[0], "w": cs[1], "r": cs[2], "named": cs[3], "name_ne": cs[4], "name_en": cs[5],
                          "wikidata": cs[6], "ele": cs[7]})
            elif tot:
                d["named"] = cs[3]
            if self.km[si]:
                d["km"] = round(self.km[si], 1)
            if self.km2[si]:
                d["km2"] = round(self.km2[si], 2)
            out[s] = d
        if examples > 0:
            for i, s in ((0, "examples_nepal"), (1, "examples_valley")):
                if self.ex[i]:
                    out[s] = [it for _k, it in sorted(self.ex[i], reverse=True)][:examples]
        return out


def _push(h, key, item):
    if len(h) < EXN:
        heapq.heappush(h, (key, item))
    elif key > h[0][0]:
        heapq.heapreplace(h, (key, item))


class HwStat:
    __slots__ = ("ways", "km", "named", "named_km", "ne", "ref", "bridge", "bridge_km", "tunnel", "tunnel_km",
                 "surface_km", "sac", "ford")

    def __init__(self):
        for f in self.__slots__:
            setattr(self, f, [0.0] * 4)


class CurvStat:
    __slots__ = ("ways", "km", "turn", "hp")

    def __init__(self):
        self.ways = [0] * 4
        self.km = [0.0] * 4
        self.turn = [0.0] * 4
        self.hp = [0] * 4


# ----------------------------------------------------------------------------
# Global state (pickled after the scan)
# ----------------------------------------------------------------------------
class State:
    def __init__(self):
        self.stats: dict[str, Stat] = {}
        self.counters = Counter()
        self.bld_n = [0] * 4
        self.bld_named = [0] * 4
        self.bld_values = [Counter() for _ in range(4)]
        self.full = [defaultdict(list), defaultdict(list)]  # nepal, valley
        self.named_total = [0] * 4
        self.ne_total = [0] * 4
        self.wd_total = [0] * 4
        self.tagged = Counter()
        # pipeline classifiers
        self.pipe_poi = [Counter() for _ in range(4)]
        self.pipe_poi_named = [Counter() for _ in range(4)]
        self.pipe_place = [Counter() for _ in range(4)]
        self.pipe_line = [Counter() for _ in range(4)]
        self.pipe_line_km = [Counter() for _ in range(4)]
        self.pipe_area = [Counter() for _ in range(4)]
        self.pipe_area_km2 = [Counter() for _ in range(4)]
        # highways
        self.hw: dict[str, HwStat] = {}
        self.hw_names = [set(), set(), set(), set()]
        self.curv: dict[str, CurvStat] = {}
        self.road_by_name: dict[str, list] = {}
        self.road_by_name_valley: dict[str, list] = {}
        self.road_by_ref: dict[str, list] = {}
        self.top_ways_curv = []  # heap (deg_per_km, ...)
        self.top_ways_hp = []
        self.hairpin_grid = Counter()
        self.hairpins_valley = []
        self.trails_by_name: dict[str, list] = {}
        # bridges
        self.br_class: dict[str, list] = {}
        self.br_value = Counter()
        self.br_struct = Counter()
        self.br_foot = [[0] * 4, [0.0] * 4]
        self.br_susp = [[0] * 4, [0.0] * 4]
        self.br_named = [0] * 4
        self.top_footbridges = []
        self.top_roadbridges = []
        self.top_suspension = []
        self.br_valley_named = []
        # water
        self.rivers: dict[str, list] = {}
        self.waterfalls: list[dict] = []
        self.wf_nodes: dict[int, int] = {}
        self.wf_name_candidates = []
        # adventure, keywords, famous treks
        self.adventure = []
        self.kw = {k: Stat() for k in KEYWORDS}
        self.kw_primary = {k: [Counter(), Counter()] for k in KEYWORDS}
        self.famous = {k: {"count": 0, "by_tag": Counter(), "examples": []} for k in FAMOUS_TREKS}
        # relation support
        self.rels = []
        self.needed_ways: set = set()
        self.area_member_ways: set = set()
        self.needed_nodes: set = set()
        self.way_info: dict[int, tuple] = {}
        self.way_coords: dict[int, np.ndarray] = {}
        self.node_loc: dict[int, tuple] = {}
        self.timings = {}
        self.ele_bands: dict[str, dict] = {}


ST = State()


def stat(key: str) -> Stat:
    s = ST.stats.get(key)
    if s is None:
        s = ST.stats[key] = Stat()
    return s


def full_wanted(key: str) -> tuple[bool, bool]:
    gen = key.split("=")[0] + "=*"
    nep = key in FULL_NEPAL or gen in FULL_NEPAL
    val = key.startswith(FULL_VALLEY_PREFIX) and not key.startswith(VALLEY_EXCLUDE)
    if key.startswith("sport=") and not any(p in ADV_SPORTS for p in key[6:].split("|")[0].split(";")):
        val = False
    if key.startswith("natural=water|water=lake"):
        val = True
    return nep, val


_FULL_CACHE: dict[str, tuple[bool, bool]] = {}


def primary_tag(td: dict) -> str:
    for k in CAT_KEYS:
        v = td.get(k)
        if v is not None:
            return f"{k}={normv(v)}"
    return "-"


# ----------------------------------------------------------------------------
# Geometry helpers
# ----------------------------------------------------------------------------
def get_coords(nodes) -> list:
    try:
        return [(n.lon, n.lat) for n in nodes]
    except osmium.InvalidLocationError:
        out = []
        for n in nodes:
            loc = n.location
            if loc.valid():
                out.append((loc.lon, loc.lat))
        return out


def line_len_scoped(coords) -> tuple:
    tot = v = c = p = 0.0
    if len(coords) < 2:
        return (0.0, 0.0, 0.0, 0.0)
    lon0, lat0 = coords[0]
    kx = KX * math.cos(lat0 * D2R)
    for lon, lat in coords[1:]:
        dx = (lon - lon0) * kx
        dy = (lat - lat0) * KY
        d = math.sqrt(dx * dx + dy * dy)
        tot += d
        mx = (lon + lon0) * 0.5
        if 85.18 <= mx <= 85.58:
            my = (lat + lat0) * 0.5
            if 27.55 <= my <= 27.83:
                p += d
                if mx <= 85.56 and 27.57 <= my <= 27.82:
                    v += d
                    if 85.283 <= mx <= 85.375 and 27.690 <= my <= 27.735:
                        c += d
        lon0, lat0 = lon, lat
    return (tot, v, c, p)


def line_len_plain(coords) -> float:
    tot = 0.0
    if len(coords) < 2:
        return 0.0
    lon0, lat0 = coords[0]
    kx = KX * math.cos(lat0 * D2R)
    for lon, lat in coords[1:]:
        dx = (lon - lon0) * kx
        dy = (lat - lat0) * KY
        tot += math.sqrt(dx * dx + dy * dy)
        lon0, lat0 = lon, lat
    return tot


def ring_area_km2(coords) -> float:
    n = len(coords)
    if n < 4:
        return 0.0
    lonr, latr = coords[0]
    kx = KX * math.cos(latr * D2R)
    s = 0.0
    px, py = 0.0, 0.0
    for lon, lat in coords[1:]:
        x = (lon - lonr) * kx
        y = (lat - latr) * KY
        s += px * y - x * py
        px, py = x, y
    return abs(s) * 0.5


def ring_area_np(a: np.ndarray) -> float:
    if len(a) < 4:
        return 0.0
    lat0 = float(a[0, 1])
    x = (a[:, 0] - a[0, 0]) * KX * math.cos(lat0 * D2R)
    y = (a[:, 1] - a[0, 1]) * KY
    return abs(float(np.dot(x[:-1], y[1:]) - np.dot(x[1:], y[:-1]))) * 0.5


DP_TOL_M = 5.0


def dp_keep(xs, ys, tol):
    """Douglas-Peucker: indices of kept vertices (iterative)."""
    n = len(xs)
    if n <= 2:
        return list(range(n))
    keep = [False] * n
    keep[0] = keep[-1] = True
    stack = [(0, n - 1)]
    tol2 = tol * tol
    while stack:
        a, b = stack.pop()
        if b <= a + 1:
            continue
        ax, ay = xs[a], ys[a]
        dx, dy = xs[b] - ax, ys[b] - ay
        L2 = dx * dx + dy * dy
        best = -1.0
        bi = -1
        if L2 == 0.0:
            for i in range(a + 1, b):
                ex, ey = xs[i] - ax, ys[i] - ay
                d2 = ex * ex + ey * ey
                if d2 > best:
                    best, bi = d2, i
        else:
            for i in range(a + 1, b):
                cr = dx * (ys[i] - ay) - dy * (xs[i] - ax)
                d2 = cr * cr / L2
                if d2 > best:
                    best, bi = d2, i
        if best > tol2:
            keep[bi] = True
            stack.append((a, bi))
            stack.append((bi, b))
    return [i for i in range(n) if keep[i]]


def curvature(coords, near):
    """Total |turning| in degrees after Douglas-Peucker simplification (5 m), hairpins
    (>= 120 deg same-sense turning within 60 m of path) and per-scope turning."""
    lon0, lat0 = coords[0]
    kx = 111320.0 * math.cos(lat0 * D2R)
    ky = 110574.0
    px = [(lon - lon0) * kx for lon, _ in coords]
    py = [(lat - lat0) * ky for _, lat in coords]
    keep = dp_keep(px, py, DP_TOL_M)
    xs = [px[i] for i in keep]
    ys = [py[i] for i in keep]
    ll = [coords[i] for i in keep]
    n = len(xs)
    if n < 3:
        return 0.0, [], None
    hd, sl = [], []
    for i in range(n - 1):
        dx = xs[i + 1] - xs[i]
        dy = ys[i + 1] - ys[i]
        hd.append(math.atan2(dy, dx))
        sl.append(math.sqrt(dx * dx + dy * dy))
    turns = [0.0] * n
    tot = 0.0
    twopi = 2 * math.pi
    for i in range(1, n - 1):
        t = hd[i] - hd[i - 1]
        if t > math.pi:
            t -= twopi
        elif t < -math.pi:
            t += twopi
        t *= R2D
        turns[i] = t
        tot += t if t >= 0 else -t
    hps = []
    i = 1
    while i < n - 1:
        ti = turns[i]
        if -2.0 < ti < 2.0:
            i += 1
            continue
        cum = 0.0
        dist = 0.0
        j = i
        hit = False
        while j < n - 1:
            cum += turns[j]
            if cum >= 120.0 or cum <= -120.0:
                hit = True
                break
            dist += sl[j]
            if dist > 60.0:
                break
            j += 1
        if hit:
            hps.append(ll[(i + j) // 2])
            i = j + 1
        else:
            i += 1
    scoped = None
    if near:
        scoped = [tot, 0.0, 0.0, 0.0]
        for k in range(1, n - 1):
            sc = scopes_of(*ll[k])
            if len(sc) > 1:
                a = abs(turns[k])
                for s in sc[1:]:
                    scoped[s] += a
    return tot, hps, scoped


def area_semantics(td: dict, closed: bool) -> bool:
    if not closed:
        return False
    a = td.get("area")
    if a == "no":
        return False
    if a == "yes":
        return True
    if ("highway" in td or "barrier" in td or "railway" in td or "aerialway" in td) and not any(
            k in td for k in ("landuse", "natural", "leisure", "amenity", "building", "place", "tourism")):
        return False
    for k, v in td.items():
        if k in AREA_KEYS:
            if k == "natural" and v in LINEAR_NATURAL:
                continue
            if k == "man_made" and v in LINEAR_MANMADE:
                continue
            if k == "waterway" and v not in WATERWAY_AREAS:
                continue
            if k == "power" and v in ("line", "minor_line", "cable"):
                continue
            if k == "historic" and v in HIST_LINEAR:
                continue
            return True
    return False


# ----------------------------------------------------------------------------
# Generic per-object processing
# ----------------------------------------------------------------------------
def count_building(bv: str, scopes, named: bool):
    for s in scopes:
        ST.bld_n[s] += 1
        ST.bld_values[s][bv] += 1
        if named:
            ST.bld_named[s] += 1


def process(td: dict, gi: int, oid: int, lon: float, lat: float, scopes, lens, area: float, is_area: bool,
            closed: bool = False, rel_kind_ok: bool = True):
    name = td.get("name") or td.get("name:en") or td.get("int_name") or ""
    name = name.strip()
    fl = (1 if name else 0, 1 if "name:ne" in td else 0, 1 if "name:en" in td else 0,
          1 if "wikidata" in td else 0, 1 if "ele" in td else 0)
    for s in scopes:
        if name:
            ST.named_total[s] += 1
        ST.ne_total[s] += fl[1]
        ST.wd_total[s] += fl[3]
    ref = osm_ref(gi, oid)
    item = ekey = None
    wd = td.get("wikidata", "")
    if name:
        pop = num(td.get("population"))
        ele = num(td.get("ele"))
        metric = pop or ele or (area * 1000.0 if area else 0.0) or (lens[0] if lens else 0.0)
        tb = (oid * 2654435761) & 0xFFFFFFFF
        ekey = (1 if wd else 0, metric, tb, gi, oid)
        extra = ""
        if pop:
            extra = f"pop={int(pop)}"
        elif ele:
            extra = f"ele={ele:g}"
        elif area:
            extra = f"{area:.3g}km2"
        elif lens and lens[0]:
            extra = f"{lens[0]:.3g}km"
        disp = (td.get("name:en") or td.get("int_name") or name).strip()
        item = [disp, td.get("name:ne", ""), ref, round(lon, 5), round(lat, 5), extra, wd]
    building = td.get("building")
    if building is not None:
        count_building(normv(building), scopes, bool(name))
    for k in CAT_KEYS:
        v = td.get(k)
        if v is None:
            continue
        v = normv(v)
        if k == "building" and v not in BUILDING_SPECIAL:
            continue
        key = f"{k}={v}"
        use_len = lens if (k in LINEAR_KEYS and not is_area) else None
        use_area = area if is_area else 0.0
        st = stat(key)
        if k in COUNT_ONLY_KEYS:
            st.add(gi, scopes, fl, None, 0.0, None, None)
            continue
        st.add(gi, scopes, fl, use_len, use_area, ekey, item)
        keys = [key]
        q = QUALIFIED.get((k, v))
        if q:
            qv = td.get(q)
            qkey = f"{key}|{q}={normv(qv) if qv else '(none)'}"
            stat(qkey).add(gi, scopes, fl, use_len, use_area, ekey, item)
            keys.append(qkey)
        for kk in keys:
            fw = _FULL_CACHE.get(kk)
            if fw is None:
                fw = _FULL_CACHE[kk] = full_wanted(kk)
            if fw[0] or fw[1]:
                keep = bool(name) or kk in RARE_KEEP_UNNAMED or (kk.split("=")[0] + "=*") in RARE_KEEP_UNNAMED
                if not keep:
                    continue
                it = item or ["", td.get("name:ne", ""), ref, round(lon, 5), round(lat, 5), "", wd]
                if fw[0] and len(ST.full[0][kk]) < 4000:
                    ST.full[0][kk].append(it)
                if fw[1] and len(scopes) > 2 and len(ST.full[1][kk]) < 4000:
                    ST.full[1][kk].append(it)

    # pipeline classifiers (what the pipeline would make of this object)
    if rel_kind_ok:
        pk = T.poi_kind(td)
        if pk != PoiKind.NONE:
            pn = pk.name
            for s in scopes:
                ST.pipe_poi[s][pn] += 1
                if name:
                    ST.pipe_poi_named[s][pn] += 1
    if "place" in td:
        pl = T.parse_place_kind(td)
        for s in scopes:
            ST.pipe_place[s][pl.name] += 1
    if gi == 1:
        lk = T.line_kind(td)
        if lk != LineKind.NONE:
            for s in scopes:
                ST.pipe_line[s][lk.name] += 1
            if lens:
                for s in range(4):
                    if lens[s]:
                        ST.pipe_line_km[s][lk.name] += lens[s]
    if (gi == 1 and closed and building is None and td.get("area") != "no") or (gi == 2 and is_area and rel_kind_ok):
        ak = T.area_kind(td)
        if ak != AreaKind.NONE:
            for s in scopes:
                ST.pipe_area[s][ak.name] += 1
                ST.pipe_area_km2[s][ak.name] += area

    # peak elevation bands
    nat0 = td.get("natural")
    if nat0 in ("peak", "volcano", "saddle"):
        e = num(td.get("ele"))
        band = "no_ele" if not e else (">=8000" if e >= 8000 else ">=7000" if e >= 7000 else ">=6000" if e >= 6000
                                       else ">=5000" if e >= 5000 else ">=4000" if e >= 4000 else ">=3000"
                                       if e >= 3000 else "<3000")
        b = ST.ele_bands.setdefault(nat0, {})
        r = b.setdefault(band, [0, 0, 0])
        r[0] += 1
        r[1] += 1 if name else 0
        r[2] += 1 if len(scopes) > 2 else 0
    # waterfalls
    ww = td.get("waterway")
    nat = td.get("natural")
    if ww == "waterfall" or nat == "waterfall":
        if True:
            rec = {"name": name, "name_ne": td.get("name:ne", ""), "osm": ref, "lon": round(lon, 5),
                   "lat": round(lat, 5), "tag": "waterway=waterfall" if ww == "waterfall" else "natural=waterfall",
                   "height": td.get("height", ""), "ele": td.get("ele", ""), "wikidata": wd, "on": [],
                   "scopes": [SCOPES[s] for s in scopes]}
            if gi == 0:
                ST.wf_nodes[oid] = len(ST.waterfalls)
            ST.waterfalls.append(rec)

    # names: keyword census, famous treks, adventure
    is_road_line = gi == 1 and "highway" in td and not is_area
    if name:
        text = " | ".join(td[k] for k in ("name", "name:en", "int_name", "name:ne", "alt_name") if k in td).casefold()
        ptag = None
        if not is_road_line and KW_ANY.search(text):
            ptag = primary_tag(td)
            for kw, rx in KW_RX.items():
                if rx.search(text):
                    ST.kw[kw].add(gi, scopes, fl, None, 0.0, ekey, item + [ptag])
                    ST.kw_primary[kw][0][ptag] += 1
                    if len(scopes) > 2:
                        ST.kw_primary[kw][1][ptag] += 1
            if WATERFALL_RX.search(text) and ww != "waterfall" and nat != "waterfall" and \
                    len(ST.wf_name_candidates) < 1500 and not td.get("shop") and td.get("amenity") not in (
                    "restaurant", "cafe", "school", "college", "bank", "fuel") and not td.get("tourism") in (
                    "hotel", "guest_house", "hostel"):
                ST.wf_name_candidates.append(item + [ptag])
        if FAMOUS_ANY.search(text):
            ptag = ptag or primary_tag(td)
            for label, rx in FAMOUS_RX.items():
                if rx.search(text):
                    f = ST.famous[label]
                    f["count"] += 1
                    f["by_tag"][ptag] += 1
                    if len(f["examples"]) < 40 and not ptag.startswith(("shop=", "amenity=restaurant", "amenity=cafe",
                                                                        "office=")):
                        f["examples"].append(item + [ptag])
    # adventure
    sp = td.get("sport")
    adv = None
    if sp and any(p.strip() in ADV_SPORTS for p in sp.lower().split(";")):
        adv = f"sport={sp}"
    elif td.get("leisure") in ADV_LEISURE:
        adv = f"leisure={td['leisure']}"
    elif td.get("aerialway") in ("zip_line", "cable_car", "gondola", "chair_lift", "mixed_lift"):
        adv = f"aerialway={td['aerialway']}"
    elif td.get("attraction") or td.get("whitewater") or td.get("climbing") or td.get("free_flying:site"):
        adv = "attraction/whitewater/climbing"
    elif td.get("tourism") in ("theme_park", "zoo"):
        adv = f"tourism={td['tourism']}"
    elif name and not is_road_line and ADV_RX.search(name.casefold()) and not td.get("shop") and \
            td.get("amenity") not in ("school", "college", "bank", "restaurant", "cafe", "fuel", "pharmacy"):
        adv = "name:" + primary_tag(td)
    if adv:
        ST.counters["adventure_total"] += 1
        if len(scopes) > 2:
            ST.counters["adventure_valley"] += 1
        if len(ST.adventure) < 3000:
            ST.adventure.append([name, ref, round(lon, 5), round(lat, 5), adv,
                                 ";".join(f"{k}={td[k]}" for k in ("tourism", "leisure", "sport", "attraction",
                                                                    "amenity", "natural", "aerialway") if k in td),
                                 1 if len(scopes) > 2 else 0])


# ----------------------------------------------------------------------------
# Node / way handlers
# ----------------------------------------------------------------------------
def on_node(n):
    td = {t.k: t.v for t in n.tags}
    loc = n.location
    if not loc.valid():
        ST.counters["nodes_invalid_location"] += 1
        return
    lon, lat = loc.lon, loc.lat
    oid = n.id
    ST.tagged["nodes"] += 1
    if oid in ST.needed_nodes:
        ST.node_loc[oid] = (lon, lat)
    scopes = scopes_of(lon, lat)
    process(td, 0, oid, lon, lat, scopes, None, 0.0, False)
    if td.get("highway") == "street_lamp" or td.get("ford"):
        pass


def _hw_way(td, hwv, name, scopes, lens, coords, near, clon, clat, wid):
    H = ST.hw.get(hwv)
    if H is None:
        H = ST.hw[hwv] = HwStat()
    for s in scopes:
        H.ways[s] += 1
        if name:
            H.named[s] += 1
        if "name:ne" in td:
            H.ne[s] += 1
        if "ref" in td:
            H.ref[s] += 1
        if "sac_scale" in td:
            H.sac[s] += 1
        if "ford" in td and td["ford"] != "no":
            H.ford[s] += 1
    for s in range(4):
        H.km[s] += lens[s]
        if name:
            H.named_km[s] += lens[s]
        if "surface" in td:
            H.surface_km[s] += lens[s]
    if name:
        ST.hw_names[0].add(name)
        if len(scopes) > 2:
            ST.hw_names[1].add(name)
            if len(scopes) > 3:
                ST.hw_names[2].add(name)
    bridge = td.get("bridge")
    if bridge and bridge != "no":
        bv = normv(bridge)
        ST.br_value[bv] += 1
        struct = normv(td.get("bridge:structure", "(none)"))
        ST.br_struct[struct] += 1
        bc = ST.br_class.get(hwv)
        if bc is None:
            bc = ST.br_class[hwv] = [[0] * 4, [0.0] * 4]
        for s in scopes:
            H.bridge[s] += 1
            bc[0][s] += 1
            if name:
                ST.br_named[s] += 1
        for s in range(4):
            H.bridge_km[s] += lens[s]
            bc[1][s] += lens[s]
        lname = name.casefold()
        susp = struct == "suspension" or bv == "suspension" or "suspension" in lname or "jholung" in lname or \
            "झोलुङ्गे" in lname
        foot = hwv in FOOT_CLASSES
        it = [name, td.get("bridge:name", ""), osm_ref(1, wid), round(clon, 5), round(clat, 5),
              round(lens[0] * 1000.0, 1), hwv, struct, bv]
        key = (lens[0], wid)
        if foot:
            for s in scopes:
                ST.br_foot[0][s] += 1
            for s in range(4):
                ST.br_foot[1][s] += lens[s]
            _pushn(ST.top_footbridges, key, it, 40)
        else:
            _pushn(ST.top_roadbridges, key, it, 40)
        if susp:
            for s in scopes:
                ST.br_susp[0][s] += 1
            for s in range(4):
                ST.br_susp[1][s] += lens[s]
            _pushn(ST.top_suspension, key, it, 40)
        if len(scopes) > 2 and name and len(ST.br_valley_named) < 400:
            ST.br_valley_named.append(it)
    tunnel = td.get("tunnel")
    if tunnel and tunnel != "no":
        for s in scopes:
            H.tunnel[s] += 1
        for s in range(4):
            H.tunnel_km[s] += lens[s]
    # curvature
    if hwv in CURV_CLASSES and coords and len(coords) >= 3:
        tot, hps, scoped = curvature(coords, near)
        C = ST.curv.get(hwv)
        if C is None:
            C = ST.curv[hwv] = CurvStat()
        for s in scopes:
            C.ways[s] += 1
        for s in range(4):
            C.km[s] += lens[s]
        if scoped is None:
            C.turn[0] += tot
            C.hp[0] += len(hps)
        else:
            for s in range(4):
                C.turn[s] += scoped[s]
        for (hlon, hlat) in hps:
            if scoped is not None:
                for s in scopes_of(hlon, hlat):
                    C.hp[s] += 1
                if 85.18 <= hlon <= 85.56 and 27.57 <= hlat <= 27.82 and len(ST.hairpins_valley) < 3000:
                    ST.hairpins_valley.append([round(hlon, 5), round(hlat, 5), hwv, name, osm_ref(1, wid)])
            ST.hairpin_grid[(round(hlon, 1), round(hlat, 1))] += 1
        km = lens[0]
        refv = td.get("ref", "")
        if name:
            _agg_road(ST.road_by_name, name, km, tot, len(hps), clon, clat, hwv, refv)
            if len(scopes) > 2:
                _agg_road(ST.road_by_name_valley, name, km, tot, len(hps), clon, clat, hwv, refv)
        if refv and hwv != "track":
            for r in refv.split(";"):
                _agg_road(ST.road_by_ref, r.strip().upper(), km, tot, len(hps), clon, clat, hwv, name)
        if km >= 2.0:
            it = [name, refv, osm_ref(1, wid), hwv, round(km, 2), round(tot / km, 1), len(hps), round(clon, 4),
                  round(clat, 4)]
            _pushn(ST.top_ways_curv, (tot / km, wid), it, 60)
            if hps:
                _pushn(ST.top_ways_hp, (len(hps), wid), it, 60)
    if name and hwv in TRAIL_CLASSES:
        r = ST.trails_by_name.get(name)
        if r is None:
            r = ST.trails_by_name[name] = [0.0, 0.0, 0, Counter(), clon, clat]
        r[0] += lens[0]
        r[1] += lens[1]
        r[2] += 1
        r[3][hwv] += 1


def _pushn(h, key, item, n):
    if len(h) < n:
        heapq.heappush(h, (key, item))
    elif key > h[0][0]:
        heapq.heapreplace(h, (key, item))


def _agg_road(d, name, km, turn, hp, clon, clat, hwv, ref):
    r = d.get(name)
    if r is None:
        r = d[name] = [0.0, 0.0, 0, 0, 0.0, 0.0, 999.0, 999.0, -999.0, -999.0, Counter(), ref]
    r[0] += km
    r[1] += turn
    r[2] += hp
    r[3] += 1
    r[4] += clon * km
    r[5] += clat * km
    r[6] = min(r[6], clon)
    r[7] = min(r[7], clat)
    r[8] = max(r[8], clon)
    r[9] = max(r[9], clat)
    r[10][hwv] += 1
    if ref and not r[11]:
        r[11] = ref


def record_member(wid, nodes, coords, lens, lon0, lat0):
    try:
        fr, lr = nodes[0].ref, nodes[-1].ref
    except Exception:
        fr = lr = 0
    ST.way_info[wid] = (lon0, lat0, lens[0], lens[1], lens[2], lens[3], fr, lr)
    if wid in ST.area_member_ways and coords:
        ST.way_coords[wid] = np.asarray(coords, dtype=np.float64)


def first_loc(nodes):
    for n in nodes:
        loc = n.location
        if loc.valid():
            return loc.lon, loc.lat
    return None


def on_way(w):
    tags = w.tags
    wid = w.id
    need = wid in ST.needed_ways
    ntags = len(tags)
    if ntags == 0:
        if need:
            nodes = w.nodes
            fl0 = first_loc(nodes)
            if fl0 is None:
                return
            near = near_pack(*fl0)
            coords = get_coords(nodes) if (near or wid in ST.area_member_ways) else None
            if near:
                lens = line_len_scoped(coords)
            else:
                try:
                    lens = (osmium.geom.haversine_distance(nodes) / 1000.0, 0.0, 0.0, 0.0)
                except osmium.InvalidLocationError:
                    lens = (line_len_plain(coords or get_coords(nodes)), 0.0, 0.0, 0.0)
            record_member(wid, nodes, coords, lens, fl0[0], fl0[1])
        return
    ST.tagged["ways"] += 1
    b = tags.get("building")
    if b is not None and not need:
        bv = normv(b)
        if bv not in BUILDING_SPECIAL:
            fast = True
            if ntags > 1:
                for t in tags:
                    if t.k in FEATURE_KEYS:
                        fast = False
                        break
            if fast:
                try:
                    loc = w.nodes[0].location
                    lon, lat = loc.lon, loc.lat
                except (osmium.InvalidLocationError, IndexError):
                    ST.counters["buildings_no_location"] += 1
                    return
                count_building(bv, scopes_of(lon, lat), False)
                return
    td = {t.k: t.v for t in tags}
    nodes = w.nodes
    fl0 = first_loc(nodes)
    if fl0 is None:
        ST.counters["ways_no_location"] += 1
        return
    lon0, lat0 = fl0
    closed = w.is_closed()
    near = near_pack(lon0, lat0)
    is_area = area_semantics(td, closed)
    hw = td.get("highway")
    hwv = normv(hw) if hw is not None else None
    want = near or is_area or (hwv in CURV_CLASSES) or (wid in ST.area_member_ways)
    coords = get_coords(nodes) if want else None
    if coords is not None and len(coords) == 0:
        ST.counters["ways_no_location"] += 1
        return
    if near:
        lens = line_len_scoped(coords)
        n = len(coords)
        clon = sum(c[0] for c in coords) / n
        clat = sum(c[1] for c in coords) / n
        scopes = scopes_of(clon, clat)
    else:
        try:
            lens = (osmium.geom.haversine_distance(nodes) / 1000.0, 0.0, 0.0, 0.0)
        except osmium.InvalidLocationError:
            if coords is None:
                coords = get_coords(nodes)
            lens = (line_len_plain(coords), 0.0, 0.0, 0.0)
        if coords:
            n = len(coords)
            clon = sum(c[0] for c in coords) / n
            clat = sum(c[1] for c in coords) / n
        else:
            clon, clat = lon0, lat0
        scopes = S_N
    area = ring_area_km2(coords) if is_area else 0.0
    process(td, 1, wid, clon, clat, scopes, lens, area, is_area, closed)
    if hwv is not None and not is_area:
        name = (td.get("name") or td.get("name:en") or "").strip()
        _hw_way(td, hwv, name, scopes, lens, coords, near, clon, clat, wid)
    ww = td.get("waterway")
    if ww is not None and not is_area:
        name = (td.get("name") or td.get("name:en") or "").strip()
        wwv = normv(ww)
        if name and wwv in ("river", "stream", "canal"):
            r = ST.rivers.get(name)
            if r is None:
                r = ST.rivers[name] = [0.0, 0.0, 0, clon, clat, wwv, td.get("name:ne", "")]
            r[0] += lens[0]
            r[1] += lens[1]
            r[2] += 1
        if ST.wf_nodes:
            wfn = ST.wf_nodes
            for nd in nodes:
                ix = wfn.get(nd.ref)
                if ix is not None:
                    on = ST.waterfalls[ix]["on"]
                    lab = f"{wwv}:{name}" if name else wwv
                    if lab not in on and len(on) < 4:
                        on.append(lab)
    if need:
        record_member(wid, nodes, coords, lens, lon0, lat0)


# ----------------------------------------------------------------------------
# Passes
# ----------------------------------------------------------------------------
AREA_REL_TYPES = ("multipolygon", "boundary")


def is_area_relation(td: dict) -> bool:
    t = td.get("type")
    if t not in AREA_REL_TYPES:
        return False
    if td.get("boundary") == "administrative":
        return False
    return True


def prepass():
    t0 = time.time()
    n = 0
    for r in osmium.FileProcessor(PBF, osmium.osm.RELATION):
        n += 1
        if not len(r.tags):
            continue
        td = {t.k: t.v for t in r.tags}
        mem = [(m.type, m.ref, m.role) for m in r.members]
        ST.rels.append((r.id, td, mem))
        area_rel = is_area_relation(td)
        for (mt, ref, role) in mem:
            if mt == "w":
                ST.needed_ways.add(ref)
                if area_rel:
                    ST.area_member_ways.add(ref)
            elif mt == "n":
                ST.needed_nodes.add(ref)
    ST.timings["prepass_relations_s"] = round(time.time() - t0, 1)
    ST.counters["relations_all"] = n
    log(f"prepass: {n} relations, {len(ST.rels)} tagged, needed ways {len(ST.needed_ways)}, "
        f"area member ways {len(ST.area_member_ways)}, needed nodes {len(ST.needed_nodes)} in {time.time() - t0:.0f}s")


def main_pass(limit_ways: int | None = None):
    t0 = time.time()
    flt = osmium.filter.EmptyTagFilter()
    flt.enable_for(osmium.osm.NODE)
    fp = osmium.FileProcessor(PBF, osmium.osm.NODE | osmium.osm.WAY).with_locations().with_filter(flt)
    nw = 0
    nn = 0
    tn = None
    for o in fp:
        if o.is_way():
            if tn is None:
                tn = time.time()
                ST.timings["nodes_s"] = round(tn - t0, 1)
                log(f"nodes done: {nn} tagged nodes in {tn - t0:.0f}s, waterfalls so far {len(ST.waterfalls)}")
            nw += 1
            on_way(o)
            if nw % 1_000_000 == 0:
                log(f"ways {nw / 1e6:.0f}M  {time.time() - t0:.0f}s  stats={len(ST.stats)} "
                    f"way_info={len(ST.way_info)} coords={len(ST.way_coords)}")
            if limit_ways and nw >= limit_ways:
                break
        else:
            nn += 1
            on_node(o)
    ST.counters["ways_seen"] = nw
    ST.timings["main_pass_s"] = round(time.time() - t0, 1)
    log(f"main pass done: {nw} ways in {time.time() - t0:.0f}s")


def log(msg):
    print(time.strftime("%H:%M:%S"), msg, flush=True)


# ----------------------------------------------------------------------------
# Relations (after the scan)
# ----------------------------------------------------------------------------
def process_relations():
    t0 = time.time()
    rel_by_id = {rid: (td, mem) for rid, td, mem in ST.rels}
    routes = defaultdict(list)
    route_union = defaultdict(set)
    route_union_valley = defaultdict(set)
    protected = []
    waterway_rels = []
    admin = defaultdict(lambda: [0, 0, 0, 0])
    missing_total = 0
    unassembled = 0

    def route_way_ids(rid, depth, seen):
        out = []
        td, mem = rel_by_id.get(rid, (None, []))
        for mt, ref, role in mem:
            if mt == "w":
                out.append(ref)
            elif mt == "r" and depth < 4 and ref in rel_by_id and ref not in seen:
                seen.add(ref)
                out.extend(route_way_ids(ref, depth + 1, seen))
        return out

    for rid, td, mem in ST.rels:
        rtype = td.get("type", "")
        area_rel = is_area_relation(td)
        lens = None
        area = 0.0
        lon = lat = None
        if area_rel:
            segs_outer, segs_inner = [], []
            for mt, ref, role in mem:
                if mt != "w":
                    continue
                c = ST.way_coords.get(ref)
                info = ST.way_info.get(ref)
                if c is None or info is None or len(c) < 2:
                    missing_total += 1
                    continue
                (segs_inner if role == "inner" else segs_outer).append((info[6], info[7], c))
            if segs_outer:
                rings_o, _u1 = build_rings(segs_outer)
                rings_i, _u2 = build_rings(segs_inner) if segs_inner else ([], 0)
                area = max(0.0, sum(ring_area_np(r) for r in rings_o) - sum(ring_area_np(r) for r in rings_i))
                allc = np.concatenate([s[2] for s in segs_outer])
                lon = float((allc[:, 0].min() + allc[:, 0].max()) / 2)
                lat = float((allc[:, 1].min() + allc[:, 1].max()) / 2)
                if not rings_o:
                    unassembled += 1
        if rtype in ("route", "superroute", "waterway", "route_master") or lon is None:
            wids = route_way_ids(rid, 0, {rid}) if rtype in ("route", "superroute", "route_master", "waterway") \
                else [ref for mt, ref, role in mem if mt == "w"]
            uniq = list(dict.fromkeys(wids))
            tot = [0.0, 0.0, 0.0, 0.0]
            xs, ys = [], []
            for w in uniq:
                info = ST.way_info.get(w)
                if info is None:
                    missing_total += 1
                    continue
                tot[0] += info[2]
                tot[1] += info[3]
                tot[2] += info[4]
                tot[3] += info[5]
                xs.append(info[0])
                ys.append(info[1])
            for mt, ref, role in mem:
                if mt == "n" and ref in ST.node_loc:
                    xs.append(ST.node_loc[ref][0])
                    ys.append(ST.node_loc[ref][1])
            if rtype in ("route", "superroute", "waterway", "route_master"):
                lens = tuple(tot)
            if lon is None and xs:
                lon = sum(xs) / len(xs)
                lat = sum(ys) / len(ys)
        if lon is None:
            ST.counters["relations_no_location"] += 1
            continue
        scopes = scopes_of(lon, lat)
        if rtype in ("route", "superroute") and lens is not None:
            # route counts by any length in scope, not by centroid
            sc = [0]
            if lens[3] > 0:
                sc.append(3)
            if lens[1] > 0:
                sc.append(1)
            if lens[2] > 0:
                sc.append(2)
            scopes = tuple(sc)
        ST.tagged["relations"] += 1
        process(td, 2, rid, lon, lat, scopes, None, area, area_rel, False, rel_kind_ok=area_rel)
        name = (td.get("name") or td.get("name:en") or "").strip()
        if rtype in ("route", "superroute"):
            rv = normv(td.get("route", td.get("superroute", "?")))
            wids = route_way_ids(rid, 0, {rid})
            route_union[rv].update(wids)
            if lens and lens[1] > 0:
                route_union_valley[rv].update(w for w in wids if ST.way_info.get(w, (0, 0, 0, 0))[3] > 0)
            routes[rv].append({
                "name": name, "name_ne": td.get("name:ne", ""), "ref": td.get("ref", ""), "osm": f"r{rid}",
                "type": rtype, "km": round(lens[0], 1) if lens else 0, "km_valley": round(lens[1], 1) if lens else 0,
                "ways": len(set(wids)), "from": td.get("from", ""), "to": td.get("to", ""),
                "network": td.get("network", ""), "operator": td.get("operator", ""), "wikidata": td.get("wikidata", ""),
                "lon": round(lon, 4), "lat": round(lat, 4)})
        if td.get("boundary") in ("protected_area", "national_park") or td.get("leisure") == "nature_reserve":
            protected.append([name, td.get("name:ne", ""), f"r{rid}", td.get("boundary") or td.get("leisure"),
                              td.get("protect_class", ""), td.get("protection_title", ""), round(area, 1),
                              round(lon, 4), round(lat, 4), td.get("wikidata", "")])
        if rtype == "waterway" and lens:
            waterway_rels.append([name, td.get("name:ne", ""), f"r{rid}", td.get("waterway", ""), round(lens[0], 1),
                                  round(lens[1], 1), td.get("wikidata", "")])
        if td.get("boundary") == "administrative":
            lvl = td.get("admin_level", "?")
            for s in scopes:
                admin[lvl][s] += 1
    ST.counters["relation_member_ways_missing"] = missing_total
    ST.counters["relations_area_unassembled"] = unassembled
    ST.timings["relations_post_s"] = round(time.time() - t0, 1)

    def union_km(ids, idx):
        return round(sum(ST.way_info[w][idx] for w in ids if w in ST.way_info), 1)

    route_out = {}
    for rv, lst in sorted(routes.items(), key=lambda kv: -len(kv[1])):
        lst.sort(key=lambda d: -d["km"])
        route_out[rv] = {
            "count": len(lst), "named": sum(1 for d in lst if d["name"]),
            "with_wikidata": sum(1 for d in lst if d["wikidata"]),
            "sum_km": round(sum(d["km"] for d in lst), 1),
            "union_km": union_km(route_union[rv], 2),
            "valley_count": sum(1 for d in lst if d["km_valley"] > 0),
            "valley_sum_km": round(sum(d["km_valley"] for d in lst), 1),
            "valley_union_km": union_km(route_union_valley[rv], 3),
            "items": [{k: v for k, v in d.items() if v not in ("", None)} for d in lst] if rv in ("hiking", "foot", "trekking", "mtb", "bicycle", "horse", "running", "canoe",
                                   "piste", "ski", "inline_skates", "fitness_trail", "walking", "pilgrimage")
            else [{k: v for k, v in d.items() if v not in ("", None)} for d in lst[:40]],
        }
    protected.sort(key=lambda r: -r[6])
    waterway_rels.sort(key=lambda r: -r[4])
    return route_out, protected, waterway_rels, {k: dict(zip(SCOPES, v)) for k, v in sorted(admin.items())}


# ----------------------------------------------------------------------------
# Pack contents
# ----------------------------------------------------------------------------
def _geo_len_km(coords) -> float:
    return line_len_plain(coords)


def pack_counts_from_features(feats: dict[str, list]) -> dict:
    out = {}
    pois = feats.get("pois", [])
    seen = {}
    for f in pois:
        p = f["properties"]
        key = (p.get("osm_type"), p.get("osm_id"), p.get("kind"))
        if key not in seen:
            seen[key] = p
    kinds = Counter(p["kind"] for p in seen.values())
    named = Counter(p["kind"] for p in seen.values() if p.get("name"))
    out["pois_unique"] = len(seen)
    out["poi_pieces"] = len(pois)
    out["pois_by_kind"] = {k: {"count": v, "named": named.get(k, 0)} for k, v in kinds.most_common()}
    lines = feats.get("lines", [])
    lk = defaultdict(lambda: [set(), 0.0, set()])
    for f in lines:
        p = f["properties"]
        r = lk[p["kind"]]
        r[0].add(p.get("osm_way_id"))
        r[1] += _geo_len_km(f["geometry"]["coordinates"])
        if p.get("name"):
            r[2].add(p["name"])
    out["lines_by_kind"] = {k: {"ways": len(v[0]), "km": round(v[1], 1), "distinct_names": len(v[2])}
                            for k, v in sorted(lk.items(), key=lambda kv: -kv[1][1])}
    areas = feats.get("areas", [])
    ak = defaultdict(lambda: [set(), 0.0])
    for f in areas:
        p = f["properties"]
        r = ak[p["kind"]]
        r[0].add(p.get("osm_ref"))
        g = f["geometry"]
        polys = [g["coordinates"]] if g["type"] == "Polygon" else g["coordinates"]
        for poly in polys:
            if poly:
                a = ring_area_km2(poly[0]) - sum(ring_area_km2(h) for h in poly[1:])
                r[1] += max(a, 0.0)
    out["areas_by_kind"] = {k: {"objects": len(v[0]), "km2": round(v[1], 2)}
                            for k, v in sorted(ak.items(), key=lambda kv: -kv[1][1])}
    for layer in ("roads", "trails"):
        rk = defaultdict(lambda: [set(), 0.0, 0])
        for f in feats.get(layer, []):
            p = f["properties"]
            r = rk[p.get("road_class") or p.get("class")]
            r[0].add(p.get("osm_way_id"))
            r[1] += _geo_len_km(f["geometry"]["coordinates"])
            r[2] += 1 if p.get("name") else 0
        out[f"{layer}_by_class"] = {k: {"ways": len(v[0]), "km": round(v[1], 1), "named_pieces": v[2]}
                                    for k, v in sorted(rk.items(), key=lambda kv: -kv[1][1])}
    return out


def pack_stats() -> dict:
    t0 = time.time()
    out = {}
    base = f"{REPO}/pipeline/build/regions"
    for region in ("kathmandu_valley", "kathmandu_core", "thamel_test"):
        mp = f"{base}/{region}/{region}.manifest.json"
        if not os.path.exists(mp):
            continue
        m = json.load(open(mp))
        st = m.get("stats", {})
        out[region] = {
            "manifest": {k: st.get(k) for k in ("roads", "road_km", "road_km_extract", "buildings", "buildings_in_tiles",
                                                 "pois", "places", "areas", "lines", "admin_areas", "search_entries",
                                                 "graph_nodes", "graph_edges", "tiles", "leaf_tiles", "pack_bytes",
                                                 "tile_features", "building_archetypes", "surface_mix_pct",
                                                 "trails_sac_tagged", "trails_sac_inferred")},
            "bbox_lonlat": m.get("bbox_lonlat"), "built_at": m.get("built_at"),
            "tile_counts": m.get("tile_counts"),
            "files": {f["path"]: f["bytes"] for f in m.get("files", [])},
        }
    # valley: decoded QA GeoJSON (qa_export decodes the pack)
    qa = f"{base}/kathmandu_valley/qa"
    feats = {}
    for layer in ("pois", "lines", "areas", "roads", "trails"):
        p = f"{qa}/{layer}.geojson"
        if os.path.exists(p):
            feats[layer] = json.load(open(p))["features"]
    if feats:
        out["kathmandu_valley"]["decoded_leaf_tiles"] = pack_counts_from_features(feats)
        out["kathmandu_valley"]["decoded_leaf_tiles"]["source"] = "pipeline/build/regions/kathmandu_valley/qa/*.geojson " \
                                                                    "(qa_export decodes every leaf tile of the .ghpk)"
    del feats
    gc.collect()
    # core: decode the shipped sample pack directly
    try:
        from ghumante_pipeline import projection
        from ghumante_pipeline.pack import PackReader
        from ghumante_pipeline.qa_export import _tile_features
        from ghumante_pipeline.tile_format import decode_tile
        core_pack = f"{REPO}/shared/sample-regions/kathmandu_core/kathmandu_core.ghpk"
        feats = defaultdict(list)
        with PackReader(core_pack) as pr:
            keys = pr.keys()
            lvls = [projection.tile_from_key(k).level for k in keys]
            leaf = max(lvls)
            for k, lv in zip(keys, lvls):
                if lv != leaf:
                    continue
                td = decode_tile(pr.get(k))
                if td.has_detail:
                    for layer, fs in _tile_features(td).items():
                        if layer in ("pois", "lines", "areas", "roads", "trails"):
                            feats[layer].extend(fs)
        d = pack_counts_from_features(feats)
        d["source"] = "shared/sample-regions/kathmandu_core/kathmandu_core.ghpk decoded with tile_format.decode_tile"
        out.setdefault("kathmandu_core", {})["decoded_leaf_tiles"] = d
    except Exception as e:  # noqa: BLE001
        out.setdefault("kathmandu_core", {})["decode_error"] = repr(e)
    out["seconds"] = round(time.time() - t0, 1)
    return out


# ----------------------------------------------------------------------------
# Output
# ----------------------------------------------------------------------------
def scoped(arr, nd=1):
    return {s: (round(arr[i], nd) if isinstance(arr[i], float) else arr[i]) for i, s in enumerate(SCOPES)}


def stats_section(prefix_keys, examples_for=None, top_n=None):
    """Group stats by key prefix. examples_for: function(key)->bool."""
    groups = defaultdict(dict)
    for key, st in ST.stats.items():
        k = key.split("=", 1)[0]
        if k not in prefix_keys:
            continue
        groups[k][key] = st
    out = {}
    for k in prefix_keys:
        items = sorted(groups.get(k, {}).items(), key=lambda kv: -(kv[1].c[0][0] + kv[1].c[0][1] + kv[1].c[0][2]))
        d = {}
        for i, (key, st) in enumerate(items):
            tot0 = st.c[0][0] + st.c[0][1] + st.c[0][2]
            if top_n and i >= top_n and not (examples_for and examples_for(key) and tot0 >= 3):
                continue
            ex = examples_for(key) if examples_for else True
            n_ex = 0
            if ex and k not in COUNT_ONLY_KEYS:
                n_ex = OUT_EX[0] if TIER_A.match(key) else min(4, OUT_EX[0])
            d[key] = st.to_json(examples=n_ex)
        out[k] = {"distinct_values": len(items), "values": d}
    return out


INTEREST = re.compile(
    r"^(place=|tourism=|historic=|natural=|waterway=|leisure=(park|nature_reserve|garden|stadium|water_park|golf_course)|"
    r"boundary=(protected_area|national_park)|aerialway=|railway=(station|halt|rail|narrow_gauge|abandoned)|"
    r"aeroway=(aerodrome|helipad|heliport|runway)|man_made=(tower|water_tower|mast|flagpole|chimney|bridge|dam|stupa|"
    r"water_tap|water_well|mani_wall|embankment|observation)|barrier=(city_wall|wall|gate)|sport=|attraction=|"
    r"amenity=(place_of_worship|fountain|drinking_water|shelter|monastery|theatre|arts_centre|marketplace|bus_station)|"
    r"heritage=|whitewater=|mountain_pass=|building=|place_of_worship=|geological=|climbing=|cave=|hazard=|"
    r"highway=(bus_stop|trailhead|street_lamp)|landuse=(forest|religious|orchard|meadow|reservoir|recreation_ground))")


TIER_A = re.compile(
    r"^(place=(city|town|village|hamlet|suburb|neighbourhood|locality|square|island|islet)$|"
    r"tourism=(attraction|viewpoint|museum|hotel|guest_house|hostel|camp_site|alpine_hut|wilderness_hut|picnic_site|"
    r"theme_park|zoo|artwork|information)$|"
    r"historic=(monument|memorial|ruins|castle|palace|city_gate|archaeological_site|temple|building|wayside_shrine|fort|"
    r"stone_tap|yes)$|"
    r"natural=(peak|volcano|saddle|ridge|cliff|cave_entrance|cave|spring|hot_spring|glacier|tree|tree_row|wood|scrub|"
    r"water|wetland|bare_rock|scree|sand|beach|valley|rock|stone|arete|grassland|shingle)$|natural=water\|water=lake$|"
    r"waterway=(river|stream|canal|ditch|waterfall|rapids|dam|weir)$|"
    r"landuse=(forest|farmland|orchard|meadow|grass|religious|reservoir|recreation_ground)$|"
    r"leisure=(park|nature_reserve|garden|stadium|water_park|golf_course)$|boundary=(protected_area|national_park)$|"
    r"aerialway=|railway=(rail|station|halt)$|aeroway=(aerodrome|helipad|runway)$|"
    r"man_made=(tower|water_tower|mast|flagpole|chimney|bridge|embankment|stupa|water_tap)$|"
    r"man_made=tower\|tower:type=(observation|communication)$|barrier=(wall|city_wall|gate)$|"
    r"sport=(climbing|paragliding|free_flying|bungee|canoe|rafting|kayak|mountain_biking|golf|motocross|horse_riding)|"
    r"attraction=|heritage=|mountain_pass=|whitewater=|amenity=place_of_worship$|amenity=fountain$|"
    r"highway=(bus_stop|street_lamp)$)")

OUT_FULL_NEPAL = re.compile(
    r"^(natural=(peak|saddle|cave_entrance|cave|hot_spring|glacier|ridge|cliff|valley|rock|stone|arete|volcano|tree)$|"
    r"mountain_pass=yes$|natural=water\|water=lake$|tourism=(viewpoint|attraction|museum|theme_park|zoo|alpine_hut|"
    r"wilderness_hut|camp_site)$|historic=(castle|fort|city_gate|archaeological_site|monument|memorial|ruins|temple)$|"
    r"aeroway=aerodrome$|railway=(station|halt)$|boundary=(national_park|protected_area)$|leisure=(nature_reserve|"
    r"golf_course|water_park)$|heritage=|man_made=stupa$|building=stupa$|man_made=tower\|tower:type=observation$|"
    r"sport=|attraction=|whitewater=|geological=|hazard=|waterway=rapids$|climbing=|cave=)")
OUT_FULL_VALLEY = re.compile(
    r"^(tourism=(attraction|viewpoint|museum|artwork|theme_park|zoo|picnic_site|camp_site|gallery|aquarium|"
    r"alpine_hut|wilderness_hut)$|historic=[^|]*$|heritage=|place=square$|place_of_worship=|"
    r"amenity=place_of_worship\|religion=|amenity=(monastery|fountain|theatre|arts_centre)$|man_made=water_tap$|"
    r"man_made=tower$|man_made=tower\|tower:type=observation$|aerialway=(gondola|cable_car|zip_line|station)$|"
    r"aeroway=(aerodrome|helipad)$|leisure=(park|stadium|water_park|nature_reserve|garden)$|"
    r"natural=(peak|saddle|cave_entrance|valley|water)$|mountain_pass=|building=(temple|stupa|church|monastery|tower|"
    r"ruins|religious|pagoda|mosque)$|landuse=religious$|waterway=(dam|waterfall)$|sport=(climbing|golf)$|attraction=)")
FULL_CAPS_FIRST = {"nepal:natural=tree": 100, "valley:leisure=garden": 60, "valley:amenity=place_of_worship|religion=hindu": 500}


def build_json(route_out, protected, waterway_rels, admin, packs, test: bool) -> dict:
    cj = {}
    cj["meta"] = {
        "generated_at": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "script": f"{OUT}/census.py",
        "source": {"pbf": "pipeline/data/raw/osm/nepal.osm.pbf", "bytes": os.path.getsize(PBF),
                   "md5": open(PBF + ".md5").read().split()[0] if os.path.exists(PBF + ".md5") else "",
                   "data_date": "2026-10-02 (newest tagged object 2026-10-02T19:34Z per tag_coverage.md)"},
        "test_mode": test,
        "scopes": {"nepal": "every object in the extract",
                   "valley": {"bbox": BOXES["valley"], "note": "Kathmandu Valley census bbox"},
                   "core": {"bbox": BOXES["core"], "note": "kathmandu_core sample region bbox"},
                   "pack": {"bbox": BOXES["pack"], "note": "kathmandu_valley pack bbox (for OSM-vs-pack comparison)"}},
        "membership": "nodes by location; ways by vertex mean (only within ~1.2 deg of the valley; elsewhere "
                      "everything is 'nepal'); way lengths per scope are clipped by segment midpoint; relations "
                      "by assembled outer-ring bbox centre (areas) or member mean; routes count in a scope if any "
                      "member length lies inside it",
        "fields": {"n/w/r": "geometry type counts", "named": "has name (or name:en / int_name)",
                   "name_ne": "has name:ne", "wikidata": "has wikidata", "ele": "has ele",
                   "km": "line length (linear keys only, not for closed areas)",
                   "km2": "area of closed ways / assembled multipolygons",
                   "examples": "[name, name:ne, osm id, lon, lat, extra, wikidata]; ranked wikidata first, then "
                               "population/ele/area/length, then pseudo-random"},
        "timings_s": ST.timings,
        "counters": dict(ST.counters),
        "tagged_objects_seen": dict(ST.tagged),
        "known_limitations": [
            "bridges.suspension(...) excludes bridge:structure=simple-suspension (counted only Nepal-wide in "
            "bridge_structure_values); longest_foot_bridges carries the structure of each bridge",
            "trails_named and famous_treks.*.named_trail_ways aggregate by the raw `name` tag, so a trail whose name is "
            "Devanagari (with the Latin form only in name:en) is missed by Latin patterns; famous_treks.*.object_* "
            "search name|name:en|int_name|name:ne|alt_name",
            "famous_treks.*.objects_matching_name was counted during the scan with an earlier Kanchenjunga pattern "
            "(missed the 'Kanchendjunga' spelling); route relations and trail ways were re-matched afterwards",
            "scope membership of ways uses the vertex mean only within ~1.2 deg of the valley (everything farther is "
            "'nepal' only); building-only ways use their first node",
            "hairpins also fire on U-shaped streets in town grids; curvature is per way (turns at way joins are lost)",
            "relation members outside the extract are missing (relation_member_ways_missing); a few area relations "
            "could not be assembled (relations_area_unassembled), their km2 is partial",
            "adventure.* is heuristic (tags + name words); review before use",
            "full_lists are capped (meta.output_caps); full_lists.truncated_from gives the uncapped size",
        ],
    }
    cj["name_coverage"] = {"named": scoped(ST.named_total), "with_name_ne": scoped(ST.ne_total),
                           "with_wikidata": scoped(ST.wd_total)}
    # categories
    keys_main = ["place", "tourism", "historic", "natural", "waterway", "landuse", "leisure", "boundary",
                 "aerialway", "railway", "aeroway", "power", "man_made", "barrier", "sport", "attraction",
                 "heritage", "whitewater", "mountain_pass", "place_of_worship", "geological", "hazard", "ford",
                 "climbing", "cave", "piste:type", "military", "public_transport", "club", "route",
                 "bridge:structure", "memorial", "artwork_type", "emergency", "healthcare"]
    cj["categories"] = stats_section(keys_main, examples_for=lambda k: bool(INTEREST.match(k)), top_n=80)
    # qualified (religion etc.) are already inside via "key=value|q=..." entries
    cj["amenity"] = stats_section(["amenity"], examples_for=lambda k: bool(INTEREST.match(k)), top_n=120)["amenity"]
    cj["highway_features"] = stats_section(["highway"], examples_for=lambda k: bool(INTEREST.match(k)),
                                           top_n=60)["highway"]
    cj["shops_offices_crafts"] = {}
    for k in ("shop", "office", "craft"):
        items = [(key, st) for key, st in ST.stats.items() if key.startswith(k + "=")]
        tot = [sum(st.c[s][0] + st.c[s][1] + st.c[s][2] for _, st in items) for s in range(4)]
        items.sort(key=lambda kv: -(kv[1].c[0][0] + kv[1].c[0][1] + kv[1].c[0][2]))
        cj["shops_offices_crafts"][k] = {
            "total": scoped(tot), "distinct_values": len(items),
            "top": {key: {s: st.c[i][0] + st.c[i][1] + st.c[i][2] for i, s in enumerate(SCOPES)}
                    for key, st in items[:30]}}
    # buildings
    special = {key: st.to_json(OUT_EX[0]) for key, st in ST.stats.items() if key.startswith("building=")}
    cj["buildings"] = {
        "total": scoped(ST.bld_n), "named": scoped(ST.bld_named),
        "top_values": {s: dict(ST.bld_values[i].most_common(40)) for i, s in enumerate(SCOPES)},
        "religious_heritage_values": special,
    }
    # places of worship
    pow_keys = sorted([k for k in ST.stats if k.startswith("amenity=place_of_worship|religion=")],
                      key=lambda k: -ST.stats[k].c[0][0] - ST.stats[k].c[0][1] - ST.stats[k].c[0][2])
    cj["places_of_worship"] = {
        "amenity=place_of_worship": ST.stats["amenity=place_of_worship"].to_json(OUT_EX[0])
        if "amenity=place_of_worship" in ST.stats else {},
        "by_religion": {k.split("religion=")[1]: ST.stats[k].to_json(examples=OUT_EX[0] if i < 6 else 0)
                        for i, k in enumerate(pow_keys)},
        "man_made=stupa": ST.stats["man_made=stupa"].to_json(OUT_EX[0]) if "man_made=stupa" in ST.stats else {},
        "note": "building=temple/stupa/... are in buildings.religious_heritage_values; place_of_worship=* in categories",
    }
    # highways
    hw = {}
    for k, H in sorted(ST.hw.items(), key=lambda kv: -kv[1].km[0]):
        if H.ways[0] < 1:
            continue
        hw[k] = {f: scoped(getattr(H, f), 1 if f.endswith("km") else 0) for f in HwStat.__slots__}
        for f in hw[k]:
            if not f.endswith("km"):
                hw[k][f] = {s: int(v) for s, v in hw[k][f].items()}
    cj["highways"] = {"by_class": hw,
                      "distinct_road_names": {"nepal": len(ST.hw_names[0]), "valley": len(ST.hw_names[1]),
                                              "core": len(ST.hw_names[2])},
                      "note": "ways counted by vertex-mean scope; km clipped by segment midpoint; "
                              "highway=* areas (area=yes) excluded"}
    # curvature
    curv = {}
    for k, C in ST.curv.items():
        curv[k] = {"ways": scoped(C.ways), "km": scoped(C.km), "turn_deg": scoped(C.turn, 0),
                   "deg_per_km": {s: round(C.turn[i] / C.km[i], 1) if C.km[i] else 0 for i, s in enumerate(SCOPES)},
                   "hairpins": scoped(C.hp),
                   "hairpins_per_100km": {s: round(100 * C.hp[i] / C.km[i], 2) if C.km[i] else 0
                                          for i, s in enumerate(SCOPES)}}

    def road_rows(d, min_km, sort, n):
        rows = []
        for name, r in d.items():
            if r[0] < min_km:
                continue
            km = r[0]
            rows.append({"name": name, "ref": r[11], "km": round(km, 1), "deg_per_km": round(r[1] / km, 1),
                         "hairpins": r[2], "hairpins_per_10km": round(10 * r[2] / km, 2), "ways": r[3],
                         "classes": dict(r[10].most_common(3)),
                         "centroid": [round(r[4] / km, 4), round(r[5] / km, 4)] if km else None,
                         "extent_deg": [round(r[8] - r[6], 2), round(r[9] - r[7], 2)]})
        rows.sort(key=sort)
        return rows[:n]

    cj["road_curvature"] = {
        "method": "per way: Douglas-Peucker simplification at 5 m removes digitising jitter; turning = sum |heading change| "
                  "at vertices; hairpin = same-sense cumulative turn >= 120 deg within 60 m of path (non-overlapping; in "
                  "town grids a U-shaped street also counts). "
                  "Named roads are aggregated by exact name across ways (a generic name may merge separate roads: "
                  "check extent_deg). Turning between consecutive ways is not counted.",
        "by_class": curv,
        "top_winding_named_roads_nepal": road_rows(ST.road_by_name, 2.0, lambda r: -r["deg_per_km"], 20),
        "top_winding_named_roads_nepal_min10km": road_rows(ST.road_by_name, 10.0, lambda r: -r["deg_per_km"], 20),
        "top_hairpin_named_roads_nepal": road_rows(ST.road_by_name, 2.0, lambda r: -r["hairpins"], 20),
        "top_winding_named_roads_valley": road_rows(ST.road_by_name_valley, 2.0, lambda r: -r["deg_per_km"], 20),
        "top_hairpin_named_roads_valley": road_rows(ST.road_by_name_valley, 1.0, lambda r: -r["hairpins"], 15),
        "top_winding_refs_nepal": road_rows(ST.road_by_ref, 10.0, lambda r: -r["deg_per_km"], 20),
        "top_hairpin_refs_nepal": road_rows(ST.road_by_ref, 5.0, lambda r: -r["hairpins"], 20),
        "top_winding_single_ways_ge2km": [dict(zip(["name", "ref", "osm", "class", "km", "deg_per_km", "hairpins",
                                                    "lon", "lat"], it))
                                          for _k, it in sorted(ST.top_ways_curv, reverse=True)[:30]],
        "top_hairpin_single_ways_ge2km": [dict(zip(["name", "ref", "osm", "class", "km", "deg_per_km", "hairpins",
                                                    "lon", "lat"], it))
                                          for _k, it in sorted(ST.top_ways_hp, reverse=True)[:30]],
        "hairpin_hotspots_0p1deg": [{"lon": k[0], "lat": k[1], "hairpins": v}
                                    for k, v in ST.hairpin_grid.most_common(25)],
        "hairpins_valley": ST.hairpins_valley[:800],
        "hairpins_valley_total_listed": len(ST.hairpins_valley),
    }
    # bridges
    cj["bridges"] = {
        "by_highway_class": {k: {"count": scoped(v[0]), "km": scoped(v[1], 2)}
                             for k, v in sorted(ST.br_class.items(), key=lambda kv: -kv[1][0][0])},
        "bridge_values": dict(ST.br_value.most_common(20)),
        "bridge_structure_values": dict(ST.br_struct.most_common(20)),
        "foot_bridges(footway/path/steps/bridleway/pedestrian/cycleway)": {"count": scoped(ST.br_foot[0]),
                                                                          "km": scoped(ST.br_foot[1], 2)},
        "suspension(bridge:structure=suspension|bridge=suspension|name suspension/jholunge; EXCLUDES "
        "bridge:structure=simple-suspension, see bridge_structure_values)": {
            "count": scoped(ST.br_susp[0]), "km": scoped(ST.br_susp[1], 2)},
        "named_bridges": scoped(ST.br_named),
        "longest_foot_bridges": [it for _k, it in sorted(ST.top_footbridges, reverse=True)][:30],
        "longest_road_bridges": [it for _k, it in sorted(ST.top_roadbridges, reverse=True)][:30],
        "longest_suspension": [it for _k, it in sorted(ST.top_suspension, reverse=True)][:30],
        "item_fields": ["name", "bridge:name", "osm", "lon", "lat", "length_m", "highway", "bridge:structure",
                        "bridge"],
        "valley_named_bridges": ST.br_valley_named[:200],
        "man_made=bridge": ST.stats["man_made=bridge"].to_json(OUT_EX[0]) if "man_made=bridge" in ST.stats else {},
    }
    # routes
    cj["routes"] = {"by_route": route_out}
    # trails by name
    tr = sorted(ST.trails_by_name.items(), key=lambda kv: -kv[1][0])
    cj["trails_named"] = {
        "distinct_names": len(tr),
        "longest_nepal": [{"name": n, "km": round(r[0], 1), "km_valley": round(r[1], 1), "ways": r[2],
                           "classes": dict(r[3].most_common(3)), "lon": round(r[4], 4), "lat": round(r[5], 4)}
                          for n, r in tr[:40]],
        "longest_valley": [{"name": n, "km_valley": round(r[1], 1), "ways": r[2], "classes": dict(r[3].most_common(3))}
                           for n, r in sorted(tr, key=lambda kv: -kv[1][1])[:25] if r[1] > 0],
    }
    # famous treks
    fam = {}
    trek_routes = [d for rv in ("hiking", "foot", "trekking", "walking", "mtb", "bicycle", "horse", "pilgrimage")
                   for d in route_out.get(rv, {}).get("items", [])]
    for label, rx in FAMOUS_RX.items():
        rel_hits = [{"name": d["name"], "osm": d["osm"], "km": d.get("km", 0), "type": d["type"], "route": rv}
                    for rv, ro in route_out.items() for d in ro["items"]
                    if d.get("name") and rx.search(d["name"].casefold())]
        way_hits = [{"name": n, "km": round(r[0], 1), "ways": r[2], "classes": dict(r[3].most_common(2))}
                    for n, r in tr if rx.search(n.casefold())]
        f = ST.famous[label]
        fam[label] = {"route_relations": rel_hits[:15], "named_trail_ways": way_hits[:15],
                      "objects_matching_name": f["count"], "objects_by_tag": dict(f["by_tag"].most_common(8)),
                      "object_examples": f["examples"][:12]}
    cj["famous_treks"] = fam
    cj["famous_treks_note"] = f"{len(trek_routes)} hiking/foot/trekking/mtb/bicycle/horse route relations in total"
    # water
    rivers = sorted(ST.rivers.items(), key=lambda kv: -kv[1][0])
    cj["water"] = {
        "rivers_by_name_way_sum_top": [{"name": n, "name_ne": r[6], "km": round(r[0], 1), "km_valley": round(r[1], 1),
                                        "ways": r[2], "waterway": r[5]} for n, r in rivers[:40]],
        "rivers_valley_top": [{"name": n, "name_ne": r[6], "km_valley": round(r[1], 1), "waterway": r[5]}
                              for n, r in sorted(rivers, key=lambda kv: -kv[1][1])[:25] if r[1] > 0],
        "distinct_named_waterways": len(rivers),
        "waterway_relations_longest": waterway_rels[:40],
        "waterway_relations_count": len(waterway_rels),
    }
    wfs = ST.waterfalls
    cj["waterfalls"] = {
        "count": len(wfs), "named": sum(1 for w in wfs if w["name"]),
        "by_tag": dict(Counter(w["tag"] for w in wfs)),
        "by_geometry": dict(Counter(w["osm"][0] for w in wfs)),
        "on_a_waterway_line": sum(1 for w in wfs if w["on"]),
        "valley": sum(1 for w in wfs if "valley" in w["scopes"]),
        "with_height": sum(1 for w in wfs if w["height"]),
        "with_wikidata": sum(1 for w in wfs if w["wikidata"]),
        "items": [{k: v for k, v in w.items() if v not in ("", [], None) and k != "scopes"} for w in
                  sorted(wfs, key=lambda w: (not w["name"], w["name"]))],
        "name_only_candidates(named like a waterfall but not tagged waterfall)": ST.wf_name_candidates[:400],
        "name_only_candidates_count": len(ST.wf_name_candidates),
    }
    # protected
    cj["protected_areas"] = {"relations": protected[:200], "relations_count": len(protected),
                             "fields": ["name", "name:ne", "osm", "type", "protect_class", "protection_title",
                                        "area_km2", "lon", "lat", "wikidata"]}
    cj["admin_boundaries_by_level"] = admin
    cj["elevation_bands"] = {"fields": "[count, named, in_valley]", **ST.ele_bands}
    # adventure
    adv = ST.adventure
    cj["adventure"] = {
        "total": ST.counters["adventure_total"], "valley": ST.counters["adventure_valley"],
        "by_reason": dict(Counter(a[4].split(":")[0] if a[4].startswith("name:") else a[4] for a in adv).most_common(40)),
        "note": "reason 'attraction/whitewater/climbing' = has attraction=*, whitewater=*, climbing=* or free_flying:site; "
                "'name' = adventure word in the name (bungee, zip line, paraglid, rafting, kayak, canyon, climb, "
                "safari, cable car, camping, adventure, go-kart, ...) and not a shop/school/bank/restaurant",
        "fields": ["name", "osm", "lon", "lat", "reason", "tags", "in_valley"],
        "items_tagged": [a for a in adv if not a[4].startswith("name:")][:700],
        "items_by_name_only": [a for a in adv if a[4].startswith("name:")][:500],
        "sport_values": {k: v.to_json(0) for k, v in ST.stats.items() if k.startswith("sport=")
                         and any(p in ADV_SPORTS for p in k[6:].split(";"))},
    }
    # keywords
    cj["name_keywords"] = {
        "note": "casefolded name|name:en|int_name|name:ne|alt_name of named non-road objects; highway lines excluded",
        "patterns": KEYWORDS,
        "by_keyword": {k: {**ST.kw[k].to_json(OUT_EX[1]),
                           "by_primary_tag_nepal": dict(ST.kw_primary[k][0].most_common(10)),
                           "by_primary_tag_valley": dict(ST.kw_primary[k][1].most_common(10))} for k in KEYWORDS},
    }
    # pipeline classifier census
    def cdict(cs, nd=None):
        return {s: (dict(sorted(((k, round(v, nd) if nd is not None else v) for k, v in cs[i].items()),
                                key=lambda kv: -kv[1]))) for i, s in enumerate(SCOPES)}
    cj["pipeline_classification"] = {
        "note": "tags.poi_kind / parse_place_kind / line_kind / area_kind applied to every object the census saw "
                "(nodes, ways; multipolygon/boundary relations), i.e. what the pipeline WOULD produce for each scope. "
                "The pipeline's buffer/clip rules differ slightly; compare with packs.*.decoded_leaf_tiles.",
        "poi_kinds": cdict(ST.pipe_poi), "poi_kinds_named": cdict(ST.pipe_poi_named),
        "place_kinds": cdict(ST.pipe_place),
        "line_kinds": cdict(ST.pipe_line), "line_kinds_km": cdict(ST.pipe_line_km, 1),
        "area_kinds": cdict(ST.pipe_area), "area_kinds_km2": cdict(ST.pipe_area_km2, 2),
        "poi_kinds_with_zero_in_nepal": [k.name for k in PoiKind if k != PoiKind.NONE and not ST.pipe_poi[0].get(k.name)],
        "area_kinds_with_zero_in_nepal": [k.name for k in AreaKind if k != AreaKind.NONE
                                          and not ST.pipe_area[0].get(k.name)],
        "line_kinds_with_zero_in_nepal": [k.name for k in LineKind if k != LineKind.NONE
                                          and not ST.pipe_line[0].get(k.name)],
    }
    cj["packs"] = packs
    # full lists last (trimmed to the size budget)
    fl_n = {k: _sort_items(v)[:FULL_CAPS_FIRST.get("nepal:" + k, 10 ** 6)]
            for k, v in sorted(ST.full[0].items()) if OUT_FULL_NEPAL.match(k)}
    fl_v = {k: _sort_items(v)[:FULL_CAPS_FIRST.get("valley:" + k, 10 ** 6)]
            for k, v in sorted(ST.full[1].items()) if OUT_FULL_VALLEY.match(k)}
    cj["full_lists"] = {"fields": "[name (name:en preferred), name:ne, osm, lon, lat, extra, wikidata]; named items "
                                  "only except caves/hot springs/volcanoes/rapids/aerialways/heritage/adventure sports; "
                                  "sorted wikidata first, then ele/population/area/length",
                        "nepal": fl_n, "valley": fl_v}
    return cj


def _sort_items(v):
    def key(it):
        try:
            m = float(it[5].split("=")[-1].rstrip("km2")) if it[5] else 0.0
        except ValueError:
            m = 0.0
        return (0 if it[6] else 1, -m, it[0])
    return sorted(v, key=key)


def _is_scalar(v):
    return v is None or isinstance(v, (str, int, float, bool))


def dumps_pretty(o, ind=0) -> str:
    """Pretty JSON where any list/dict holding only scalars (or only scalar lists) stays on one line."""
    pad = " " * ind
    if isinstance(o, dict):
        if not o:
            return "{}"
        if all(_is_scalar(v) for v in o.values()) and len(o) <= 40:
            return json.dumps(o, ensure_ascii=False)
        parts = [f'{pad} {json.dumps(str(k), ensure_ascii=False)}: {dumps_pretty(v, ind + 1)}' for k, v in o.items()]
        return "{\n" + ",\n".join(parts) + "\n" + pad + "}"
    if isinstance(o, list):
        if not o:
            return "[]"
        if all(_is_scalar(v) for v in o):
            return json.dumps(o, ensure_ascii=False)
        if all(isinstance(v, list) and all(_is_scalar(x) for x in v) for v in o) or \
                all(isinstance(v, dict) and all(_is_scalar(x) for x in v.values()) for v in o):
            return "[\n" + ",\n".join(pad + "  " + json.dumps(v, ensure_ascii=False) for v in o) + "\n" + pad + "]"
        return "[\n" + ",\n".join(pad + "  " + dumps_pretty(v, ind + 1) for v in o) + "\n" + pad + "]"
    return json.dumps(o, ensure_ascii=False)


LADDER = [(15, 10, 700), (15, 10, 400), (12, 8, 300), (10, 8, 220), (10, 6, 160), (8, 5, 120), (6, 4, 90),
          (5, 3, 60), (4, 3, 40), (3, 2, 25)]
OUT_EX = [15, 10]


def fit_size(build, limit=1_950_000):
    """build() -> cj using OUT_EX; full lists are capped here. Steps down the ladder until it fits."""
    for ex_main, ex_kw, cap in LADDER:
        OUT_EX[0], OUT_EX[1] = ex_main, ex_kw
        cj = build()
        trunc = {}
        for scope in ("nepal", "valley"):
            for k, v in cj["full_lists"][scope].items():
                if len(v) > cap:
                    cj["full_lists"][scope][k] = v[:cap]
                    trunc[f"{scope}:{k}"] = len(v)
        cj["full_lists"]["truncated_from"] = trunc
        cj["meta"]["output_caps"] = {"examples_per_category_scope": ex_main, "examples_per_keyword_scope": ex_kw,
                                     "full_list_cap": cap}
        s = dumps_pretty(cj)
        if len(s.encode()) <= limit:
            return cj, s
    return cj, s


# ----------------------------------------------------------------------------
# Markdown summary
# ----------------------------------------------------------------------------
def _c(cj_cat, key, scope="nepal", f="total"):
    try:
        return cj_cat[key][scope].get(f, 0)
    except (KeyError, AttributeError):
        return 0


def write_md(cj, path):
    L = []
    A = L.append
    cats = cj["categories"]

    def cat(key):
        k = key.split("=", 1)[0]
        if k == "amenity":
            return cj["amenity"]["values"].get(key)
        if k == "highway":
            return cj["highway_features"]["values"].get(key)
        if k == "building":
            return cj["buildings"]["religious_heritage_values"].get(key)
        return cats.get(k, {}).get("values", {}).get(key)

    def row(key, label=None, extra=""):
        d = cat(key)
        if not d:
            return f"| {label or key} | 0 | 0 | 0 | | | | {extra} |"
        n, v, c = d["nepal"], d["valley"], d["core"]
        parts = []
        if n.get("km2"):
            parts.append(f"{n['km2']:,} km² (valley {v.get('km2', 0):,})")
        if n.get("km") and (not n.get("km2") or n["km"] > 5):
            parts.append(f"{n['km']:,} km (valley {v.get('km', 0):,})")
        km = "; ".join(parts)
        ex = d.get("examples_nepal", [])[:3]
        exs = "; ".join(e[0] for e in ex)
        return (f"| {label or key} | {n['total']:,} | {v['total']:,} | {c['total']:,} | "
                f"{n.get('named', 0):,} / {n.get('name_ne', 0):,} / {n.get('wikidata', 0):,} | "
                f"n{n.get('n', 0)} w{n.get('w', 0)} r{n.get('r', 0)} | {km} | {exs}{extra} |")

    hdr = ("| category | Nepal | Valley | Core | named / name:ne / wikidata (Nepal) | geometry (Nepal) | length/area | "
           "examples |\n|---|---:|---:|---:|---|---|---|---|")
    meta = cj["meta"]
    A("# Ghumante OSM data census (Nepal extract 2026-10-02)")
    A("")
    A(f"Generated {meta['generated_at']} by `census.py` (one relations read + one nodes/ways pass over the 468 MB PBF; "
      f"timings {meta['timings_s']}). Machine-readable numbers: `census.json` (same folder). Scopes: **Nepal** = whole "
      f"extract; **Valley** = bbox {BOXES['valley']}; **Core** = kathmandu_core bbox {BOXES['core']}; "
      f"**Pack** = kathmandu_valley pack bbox {BOXES['pack']} (only in the JSON). Ways are placed by vertex mean, lengths "
      f"clipped by segment midpoint, routes by member length.")
    A("")
    fpath = f"{OUT}/census_findings.md"
    if os.path.exists(fpath):
        A(open(fpath).read().rstrip())
        A("")
        A("# Detailed tables (auto-generated)")
        A("")
    nc = cj["name_coverage"]
    A(f"Tagged objects seen: {meta['tagged_objects_seen']}. Named features: Nepal {nc['named']['nepal']:,}, "
      f"valley {nc['named']['valley']:,}; with name:ne {nc['with_name_ne']['nepal']:,} / {nc['with_name_ne']['valley']:,}; "
      f"with wikidata {nc['with_wikidata']['nepal']:,} / {nc['with_wikidata']['valley']:,}.")
    A("")
    sections = [
        ("Places", ["place=city", "place=town", "place=village", "place=hamlet", "place=isolated_dwelling",
                    "place=suburb", "place=neighbourhood", "place=quarter", "place=locality", "place=square",
                    "place=island", "place=islet", "place=farm"]),
        ("Landmarks, monuments, heritage", ["tourism=attraction", "historic=monument", "historic=memorial",
                                            "historic=ruins", "historic=castle", "historic=palace", "historic=fort",
                                            "historic=city_gate", "historic=archaeological_site", "historic=temple",
                                            "historic=building", "historic=wayside_shrine", "historic=yes",
                                            "historic=heritage", "heritage=1", "heritage=2", "heritage=3",
                                            "man_made=stupa", "building=stupa", "building=temple", "building=pagoda",
                                            "building=monastery", "building=gompa", "building=palace",
                                            "building=church", "building=mosque", "building=shrine",
                                            "amenity=place_of_worship", "amenity=monastery", "tourism=museum",
                                            "tourism=artwork", "man_made=tower", "place=square"]),
        ("Tourism & lodging", ["tourism=viewpoint", "tourism=hotel", "tourism=guest_house", "tourism=hostel",
                               "tourism=camp_site", "tourism=alpine_hut", "tourism=wilderness_hut",
                               "tourism=picnic_site", "tourism=theme_park", "tourism=zoo", "tourism=information"]),
        ("Nature & scenery", ["natural=peak", "natural=volcano", "natural=saddle", "mountain_pass=yes",
                              "natural=ridge", "natural=cliff", "natural=cave_entrance", "natural=cave",
                              "natural=spring", "natural=hot_spring", "natural=glacier", "natural=tree",
                              "natural=tree_row", "natural=wood", "natural=scrub", "natural=grassland",
                              "natural=heath", "natural=water", "natural=water|water=lake", "natural=water|water=river",
                              "natural=water|water=pond", "natural=wetland", "natural=bare_rock", "natural=scree",
                              "natural=sand", "natural=shingle", "natural=beach", "natural=rock", "natural=stone",
                              "natural=waterfall", "natural=valley", "natural=gorge"]),
        ("Waterways", ["waterway=river", "waterway=stream", "waterway=canal", "waterway=ditch", "waterway=drain",
                       "waterway=waterfall", "waterway=rapids", "waterway=dam", "waterway=weir", "waterway=riverbank",
                       "whitewater=*"]),
        ("Land use & green", ["landuse=forest", "landuse=farmland", "landuse=orchard", "landuse=meadow",
                              "landuse=grass", "landuse=residential", "landuse=commercial", "landuse=industrial",
                              "landuse=religious", "landuse=cemetery", "landuse=military", "landuse=reservoir",
                              "landuse=quarry", "landuse=construction", "landuse=farmyard", "landuse=plant_nursery",
                              "leisure=park", "leisure=garden", "leisure=nature_reserve", "leisure=pitch",
                              "leisure=stadium", "leisure=playground", "leisure=sports_centre",
                              "leisure=swimming_pool", "leisure=golf_course", "leisure=water_park",
                              "boundary=protected_area", "boundary=national_park"]),
        ("Transport infrastructure", ["aerialway=cable_car", "aerialway=gondola", "aerialway=chair_lift",
                                      "aerialway=zip_line", "aerialway=goods", "aerialway=station",
                                      "railway=rail", "railway=narrow_gauge", "railway=abandoned",
                                      "railway=construction", "railway=station", "railway=halt",
                                      "aeroway=aerodrome", "aeroway=runway", "aeroway=helipad", "aeroway=heliport",
                                      "aeroway=taxiway", "aeroway=apron", "aeroway=terminal", "highway=bus_stop",
                                      "amenity=bus_station", "amenity=fuel", "amenity=parking", "amenity=taxi",
                                      "highway=traffic_signals", "highway=crossing", "highway=mini_roundabout",
                                      "highway=turning_circle", "highway=motorway_junction", "highway=trailhead",
                                      "ford=yes"]),
        ("Power & man-made", ["power=line", "power=minor_line", "power=tower", "power=pole", "power=substation",
                              "power=plant", "power=generator", "man_made=water_tower", "man_made=mast",
                              "man_made=tower|tower:type=communication", "man_made=tower|tower:type=observation",
                              "man_made=flagpole", "man_made=chimney", "man_made=bridge", "man_made=embankment",
                              "man_made=dam", "man_made=water_tap", "man_made=water_well", "man_made=mani_wall",
                              "man_made=works", "man_made=storage_tank", "barrier=wall", "barrier=city_wall",
                              "barrier=fence", "barrier=gate", "barrier=retaining_wall", "barrier=hedge"]),
        ("Street furniture", ["highway=street_lamp", "amenity=bench", "amenity=shelter",
                              "amenity=shelter|shelter_type=public_transport", "amenity=shelter|shelter_type=(none)",
                              "highway=bus_stop", "amenity=drinking_water", "man_made=water_tap", "amenity=fountain",
                              "amenity=waste_basket", "amenity=toilets", "amenity=post_box", "amenity=telephone",
                              "amenity=water_point", "amenity=atm", "natural=tree", "tourism=artwork",
                              "amenity=clock", "amenity=vending_machine", "advertising=billboard"]),
        ("Adventure & sport", ["sport=climbing", "sport=paragliding", "sport=free_flying", "sport=bungee",
                               "sport=canoe", "sport=rafting", "sport=kayak", "sport=mountain_biking",
                               "sport=motocross", "sport=golf", "sport=horse_riding", "sport=skiing",
                               "sport=soccer", "sport=cricket", "sport=swimming", "sport=multi",
                               "attraction=amusement_ride", "attraction=animal", "attraction=water_slide",
                               "attraction=summer_toboggan", "climbing=crag", "leisure=water_park"]),
    ]
    for title, keys in sections:
        A(f"## {title}")
        A("")
        A(hdr)
        for k in keys:
            A(row(k))
        A("")
    # religion
    A("## Places of worship by religion")
    A("")
    A("| religion | Nepal | Valley | Core | named | wikidata |")
    A("|---|---:|---:|---:|---:|---:|")
    for r, d in cj["places_of_worship"]["by_religion"].items():
        A(f"| {r} | {d['nepal']['total']:,} | {d['valley']['total']:,} | {d['core']['total']:,} | "
          f"{d['nepal'].get('named', 0):,} | {d['nepal'].get('wikidata', 0):,} |")
    A("")
    b = cj["buildings"]
    A(f"Buildings: Nepal {b['total']['nepal']:,}, valley {b['total']['valley']:,}, core {b['total']['core']:,} "
      f"(named {b['named']['nepal']:,} / {b['named']['valley']:,}). Top values Nepal: "
      + ", ".join(f"{k} {v:,}" for k, v in list(b['top_values']['nepal'].items())[:12]))
    A("")
    # highways
    A("## Roads and trails (km)")
    A("")
    A("| highway | ways Nepal | km Nepal | km Valley | km Core | named ways % | named km Nepal | name:ne ways | "
      "bridges (Nepal / Valley) | tunnels | surface-tagged km % |")
    A("|---|---:|---:|---:|---:|---:|---:|---:|---|---:|---:|")
    for k, h in cj["highways"]["by_class"].items():
        if h["ways"]["nepal"] < 20:
            continue
        kmn = h["km"]["nepal"]
        A(f"| {k} | {h['ways']['nepal']:,} | {kmn:,.0f} | {h['km']['valley']:,.0f} | {h['km']['core']:,.0f} | "
          f"{100 * h['named']['nepal'] / max(1, h['ways']['nepal']):.1f} | {h['named_km']['nepal']:,.0f} | "
          f"{h['ne']['nepal']:,} | {h['bridge']['nepal']:,} / {h['bridge']['valley']:,} | {h['tunnel']['nepal']:,} | "
          f"{100 * h['surface_km']['nepal'] / max(1e-9, kmn):.1f} |")
    A("")
    dn = cj["highways"]["distinct_road_names"]
    A(f"Distinct highway names: Nepal {dn['nepal']:,}, valley {dn['valley']:,}, core {dn['core']:,}.")
    A("")
    rc = cj["road_curvature"]
    A("## Winding roads (curvature)")
    A("")
    A(rc["method"])
    A("")
    A("| class | km Nepal | deg/km Nepal | hairpins Nepal | hairpins/100 km | deg/km Valley | hairpins Valley |")
    A("|---|---:|---:|---:|---:|---:|---:|")
    for k in ("trunk", "primary", "secondary", "tertiary", "unclassified", "track"):
        c = rc["by_class"].get(k)
        if not c:
            continue
        A(f"| {k} | {c['km']['nepal']:,.0f} | {c['deg_per_km']['nepal']} | {c['hairpins']['nepal']:,} | "
          f"{c['hairpins_per_100km']['nepal']} | {c['deg_per_km']['valley']} | {c['hairpins']['valley']:,} |")
    A("")
    for title, key in (("Top 20 most winding named roads in Nepal (>= 2 km)", "top_winding_named_roads_nepal"),
                       ("Most winding named roads >= 10 km", "top_winding_named_roads_nepal_min10km"),
                       ("Most hairpins (named roads)", "top_hairpin_named_roads_nepal"),
                       ("Most winding named roads in the valley", "top_winding_named_roads_valley")):
        A(f"**{title}**")
        A("")
        A("| road | ref | km | deg/km | hairpins | classes | centroid | extent (deg) |")
        A("|---|---|---:|---:|---:|---|---|---|")
        for r in rc[key][:20]:
            A(f"| {r['name']} | {r['ref']} | {r['km']} | {r['deg_per_km']} | {r['hairpins']} | "
              f"{', '.join(r['classes'])} | {r['centroid']} | {r['extent_deg']} |")
        A("")
    A("**Hairpin hotspots (0.1° cells):** " + ", ".join(f"({h['lon']}, {h['lat']}) {h['hairpins']}"
                                                         for h in rc["hairpin_hotspots_0p1deg"][:12]))
    A("")
    br = cj["bridges"]
    fb = br["foot_bridges(footway/path/steps/bridleway/pedestrian/cycleway)"]
    sp = br["suspension(bridge:structure=suspension|bridge=suspension|name suspension/jholunge; EXCLUDES "
            "bridge:structure=simple-suspension, see bridge_structure_values)"]
    A("## Bridges")
    A("")
    A(f"Bridge ways by class (Nepal count / valley count): " + ", ".join(
        f"{k} {v['count']['nepal']:,}/{v['count']['valley']:,}" for k, v in list(br["by_highway_class"].items())[:14]))
    A("")
    A(f"Foot bridges (footway/path/steps/...): Nepal {fb['count']['nepal']:,} ({fb['km']['nepal']} km), valley "
      f"{fb['count']['valley']:,}. Suspension (tag or name): Nepal {sp['count']['nepal']:,} ({sp['km']['nepal']} km), "
      f"valley {sp['count']['valley']:,}. bridge=* values: {br['bridge_values']}. bridge:structure: "
      f"{br['bridge_structure_values']}.")
    A("")
    A("Longest suspension bridges: " + "; ".join(f"{b[0] or '(unnamed)'} {b[5]} m ({b[6]}, {b[3]},{b[4]})"
                                                 for b in br["longest_suspension"][:10]))
    A("")
    A("Longest foot bridges: " + "; ".join(f"{b[0] or '(unnamed)'} {b[5]} m ({b[7]})" for b in br["longest_foot_bridges"][:10]))
    A("")
    # routes
    A("## Route relations")
    A("")
    A("| route | count | named | sum km | union km | valley count | valley union km |")
    A("|---|---:|---:|---:|---:|---:|---:|")
    for k, r in cj["routes"]["by_route"].items():
        A(f"| {k} | {r['count']} | {r['named']} | {r['sum_km']:,} | {r['union_km']:,} | {r['valley_count']} | "
          f"{r['valley_union_km']:,} |")
    A("")
    for rv in ("hiking", "foot", "trekking", "mtb", "bicycle"):
        r = cj["routes"]["by_route"].get(rv)
        if r:
            A(f"**{rv}** (longest): " + "; ".join(f"{d.get('name') or d.get('ref') or d['osm']} {d.get('km', 0)} km"
                                                  for d in r["items"][:25]))
            A("")
    A("## Famous treks: what OSM has")
    A("")
    A("| trek | route relations (km) | named trail ways (km) | objects named like it |")
    A("|---|---|---|---|")
    for label, f in cj["famous_treks"].items():
        rr = "; ".join(f"{d['name']} ({d['km']})" for d in f["route_relations"][:4])
        ww = "; ".join(f"{d['name']} ({d['km']})" for d in f["named_trail_ways"][:3])
        A(f"| {label} | {rr or '**none**'} | {ww or '-'} | {f['objects_matching_name']} "
          f"({', '.join(list(f['objects_by_tag'])[:3])}) |")
    A("")
    tn = cj["trails_named"]
    A(f"Named trail ways: {tn['distinct_names']:,} distinct names. Longest: " + "; ".join(
        f"{d['name']} {d['km']} km" for d in tn["longest_nepal"][:15]))
    A("")
    # water
    wf = cj["waterfalls"]
    A("## Waterfalls, rivers")
    A("")
    A(f"Waterfalls: {wf['count']} ({wf['named']} named; by tag {wf['by_tag']}; geometry {wf['by_geometry']}; "
      f"{wf['on_a_waterway_line']} sit on a waterway line; {wf['valley']} in the valley bbox; {wf['with_height']} have "
      f"height; {wf['with_wikidata']} wikidata). Name-only candidates (named like a waterfall/jharana/chhahara but "
      f"not tagged): {wf['name_only_candidates_count']}.")
    A("")
    A("Named waterfalls (first 40): " + "; ".join(w["name"] for w in wf["items"] if w.get("name"))[:2500])
    A("")
    wt = cj["water"]
    A("Longest rivers (relations): " + "; ".join(f"{r[0]} {r[4]} km" for r in wt["waterway_relations_longest"][:15]))
    A("")
    A("Valley rivers (way sums): " + "; ".join(f"{r['name']} {r['km_valley']} km" for r in wt["rivers_valley_top"][:15]))
    A("")
    pa = cj["protected_areas"]
    A(f"Protected-area relations: {pa['relations_count']}. Largest: " + "; ".join(
        f"{p[0]} ({p[3]}, {p[6]:,} km²)" for p in pa["relations"][:20]))
    A("")
    A("## Adventure")
    A("")
    ad = cj["adventure"]
    A(f"Adventure-flavoured objects: {ad['total']:,} Nepal, {ad['valley']:,} valley. By reason: {ad['by_reason']}.")
    A("")
    A("Tagged adventure items (sample): " + "; ".join(f"{a[0] or '(unnamed)'} [{a[4]}]" for a in ad["items_tagged"][:40]))
    A("")
    A("Name-only adventure items (sample): " + "; ".join(f"{a[0]} [{a[4][5:]}]" for a in ad["items_by_name_only"][:40]))
    A("")
    A("## Name keywords (Nepali geography & heritage words)")
    A("")
    A("| keyword | Nepal | Valley | Core | top primary tags (Nepal) |")
    A("|---|---:|---:|---:|---|")
    for k, d in cj["name_keywords"]["by_keyword"].items():
        A(f"| {k} | {d['nepal']['total']:,} | {d['valley']['total']:,} | {d['core']['total']:,} | "
          f"{', '.join(f'{t} {n}' for t, n in list(d['by_primary_tag_nepal'].items())[:5])} |")
    A("")
    # pipeline
    pc = cj["pipeline_classification"]
    A("## Pipeline classification of all Nepal (tags.poi_kind / area_kind / line_kind)")
    A("")
    A("| PoiKind | Nepal | Valley | Core | Pack bbox | in valley pack (decoded) |")
    A("|---|---:|---:|---:|---:|---:|")
    packpois = cj["packs"].get("kathmandu_valley", {}).get("decoded_leaf_tiles", {}).get("pois_by_kind", {})
    for k in [p.name for p in PoiKind if p != PoiKind.NONE]:
        A(f"| {k} | {pc['poi_kinds']['nepal'].get(k, 0):,} | {pc['poi_kinds']['valley'].get(k, 0):,} | "
          f"{pc['poi_kinds']['core'].get(k, 0):,} | {pc['poi_kinds']['pack'].get(k, 0):,} | "
          f"{packpois.get(k, {}).get('count', 0):,} |")
    A("")
    A(f"PoiKinds with zero objects in all Nepal: {pc['poi_kinds_with_zero_in_nepal']}. AreaKinds zero: "
      f"{pc['area_kinds_with_zero_in_nepal']}. LineKinds zero: {pc['line_kinds_with_zero_in_nepal']}.")
    A("")
    A("| AreaKind | Nepal count | Nepal km² | Valley count | Valley km² |")
    A("|---|---:|---:|---:|---:|")
    for k, v in pc["area_kinds"]["nepal"].items():
        A(f"| {k} | {v:,} | {pc['area_kinds_km2']['nepal'].get(k, 0):,} | {pc['area_kinds']['valley'].get(k, 0):,} | "
          f"{pc['area_kinds_km2']['valley'].get(k, 0):,} |")
    A("")
    A("| LineKind | Nepal ways | Nepal km | Valley ways | Valley km |")
    A("|---|---:|---:|---:|---:|")
    for k, v in pc["line_kinds"]["nepal"].items():
        A(f"| {k} | {v:,} | {pc['line_kinds_km']['nepal'].get(k, 0):,} | {pc['line_kinds']['valley'].get(k, 0):,} | "
          f"{pc['line_kinds_km']['valley'].get(k, 0):,} |")
    A("")
    # packs
    A("## What reached the built packs")
    A("")
    for region, p in cj["packs"].items():
        if not isinstance(p, dict):
            continue
        m = p.get("manifest", {})
        A(f"**{region}** (built {p.get('built_at')}, bbox {p.get('bbox_lonlat')}): tiles {m.get('tiles')}, "
          f"roads {m.get('roads')}, road_km {m.get('road_km')}, buildings {m.get('buildings')} "
          f"(in tiles {m.get('buildings_in_tiles')}), pois {m.get('pois')}, places {m.get('places')}, "
          f"areas {m.get('areas')}, lines {m.get('lines')}, search entries {m.get('search_entries')}.")
        dl = p.get("decoded_leaf_tiles")
        if dl:
            A("")
            A(f"Decoded leaf tiles ({dl['source']}): {dl['pois_unique']:,} unique POIs; by kind: " + ", ".join(
                f"{k} {v['count']}" for k, v in dl["pois_by_kind"].items()))
            A("")
            A("Lines by kind: " + ", ".join(f"{k} {v['ways']} ways/{v['km']} km" for k, v in dl["lines_by_kind"].items()))
            A("")
            A("Areas by kind: " + ", ".join(f"{k} {v['objects']}/{v['km2']} km²" for k, v in dl["areas_by_kind"].items()))
            A("")
            A("Roads by class: " + ", ".join(f"{k} {v['km']} km" for k, v in dl.get("roads_by_class", {}).items())
              + " | trails: " + ", ".join(f"{k} {v['km']} km" for k, v in dl.get("trails_by_class", {}).items()))
            A("")
    open(path, "w").write("\n".join(L) + "\n")


# ----------------------------------------------------------------------------
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--test", type=int, default=0)
    ap.add_argument("--post", action="store_true")
    args = ap.parse_args()
    global ST
    os.makedirs(OUT, exist_ok=True)
    suffix = "_test" if args.test else ""
    if args.post:
        t0 = time.time()
        with open(PICKLE, "rb") as fh:
            ST = pickle.load(fh)
        log(f"loaded pickle in {time.time() - t0:.0f}s")
    else:
        t0 = time.time()
        if not args.test:
            prepass()
        main_pass(args.test or None)
        ST.timings["scan_total_s"] = round(time.time() - t0, 1)
        if not args.test:
            t1 = time.time()
            with open(PICKLE, "wb") as fh:
                pickle.dump(ST, fh, protocol=pickle.HIGHEST_PROTOCOL)
            log(f"pickled state in {time.time() - t1:.0f}s ({os.path.getsize(PICKLE) / 1e6:.0f} MB)")
    route_out, protected, waterway_rels, admin = process_relations()
    log("relations processed")
    packs = pack_stats()
    log("packs read")
    cj, s = fit_size(lambda: build_json(route_out, protected, waterway_rels, admin, packs, bool(args.test)))
    jp = f"{OUT}/census{suffix}.json"
    open(jp, "w").write(s)
    log(f"wrote {jp} ({len(s.encode()) / 1e6:.2f} MB, caps {cj['meta']['output_caps']})")
    write_md(cj, f"{OUT}/census{suffix}.md")
    log("wrote md")


if __name__ == "__main__":
    main()
