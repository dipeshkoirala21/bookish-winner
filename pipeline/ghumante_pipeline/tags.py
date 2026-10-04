"""Robust OSM tag parsing into the typed fields of ``model.py``.

Every parser here takes raw OSM strings (or a tag mapping) and returns a
``model`` enum, a number, or ``None``. None of them raises on bad data: Nepal's
OSM data has plenty of it (docs/reports/TAG_COVERAGE_FINDINGS.md). Examples are
``Blacktopped``, ``bitumin`` and the Hebrew ``בלתי_סלול`` for surfaces, ``G+2``
for levels, ``5 m`` and ``3,5`` for widths, and free text in ``place``.

Values a parser does not understand are counted in ``unknown_values`` (per OSM
key) so the build report can list them. They are never dropped silently. Call
each parser once per object and key, or the counts are inflated.
``reset_unknown()`` clears the counters between builds.

Normalisation applied to enum-like values: Unicode NFC, trimmed, lower-cased,
runs of whitespace (and, for most keys, hyphens) turned into ``_``. For a
multi-value ``a;b`` the first recognised value wins, unless a parser says
otherwise.
"""

from __future__ import annotations

import math
import re
import unicodedata
from collections import Counter, defaultdict
from typing import Mapping

from .model import (
    ALL_TRAVEL,
    MOTOR_TRAVEL,
    TRAIL_CLASSES,
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

# ---------------------------------------------------------------------------
# Unknown-value bookkeeping
# ---------------------------------------------------------------------------
unknown_values: dict[str, Counter] = defaultdict(Counter)
_UNKNOWN_MAX_LEN = 64  # long free text is truncated in the report


def reset_unknown() -> None:
    """Forget every recorded unknown value."""
    unknown_values.clear()


def unknown_report(top: int = 40) -> dict[str, list[tuple[str, int]]]:
    """Deterministic summary of ``unknown_values``.

    Keys are sorted. Each key holds up to ``top`` ``(value, count)`` pairs,
    most frequent first, with ties broken by value.
    """
    out: dict[str, list[tuple[str, int]]] = {}
    for key in sorted(unknown_values):
        items = sorted(unknown_values[key].items(), key=lambda kv: (-kv[1], kv[0]))
        if items:
            out[key] = items[:top]
    return out


def _record(key: str, raw: object) -> None:
    if raw is None:
        return
    v = str(raw).strip()
    if v:
        unknown_values[key][v[:_UNKNOWN_MAX_LEN]] += 1


# ---------------------------------------------------------------------------
# String helpers
# ---------------------------------------------------------------------------
_SPACES = re.compile(r"\s+")
_SPACES_HYPHENS = re.compile(r"[\s\-]+")
_DEVANAGARI_DIGITS = str.maketrans("०१२३४५६७८९", "0123456789")
_PRIMES = str.maketrans({"′": "'", "’": "'", "‘": "'", "`": "'", "´": "'", "″": '"', "”": '"', "“": '"'})


def _norm(raw: object, hyphen: bool = True) -> str:
    """NFC, trimmed, lower case, whitespace (and hyphens) -> ``_``."""
    if raw is None:
        return ""
    s = unicodedata.normalize("NFC", str(raw)).strip().lower()
    s = (_SPACES_HYPHENS if hyphen else _SPACES).sub("_", s)
    return s.strip("_")


def _first(raw: object, hyphen: bool = True) -> str:
    """Normalised first value of a ``;``-separated multi-value."""
    if raw is None:
        return ""
    head = str(raw).split(";", 1)[0]
    return _norm(head, hyphen)


def _parts(raw: object, hyphen: bool = True) -> list[str]:
    """All normalised non-empty values of a ``;``-separated multi-value."""
    if raw is None:
        return []
    return [p for p in (_norm(x, hyphen) for x in str(raw).split(";")) if p]


def _tv(tags: Mapping[str, str], key: str) -> str:
    """Normalised first value of ``tags[key]`` ("" when absent)."""
    return _first(tags.get(key), hyphen=False)


def _clean_text(raw: object) -> str:
    """Names: NFC, trimmed, inner whitespace collapsed to one space."""
    if raw is None:
        return ""
    return _SPACES.sub(" ", unicodedata.normalize("NFC", str(raw))).strip()


# ---------------------------------------------------------------------------
# Script detection
# ---------------------------------------------------------------------------
def _script_counts(s: str) -> tuple[int, int, int]:
    """(latin, devanagari, other) letter counts. Digits and punctuation are ignored;
    Devanagari vowel signs and virama count as Devanagari."""
    if s.isascii():
        return sum(1 for ch in s if ch.isalpha()), 0, 0
    lat = dev = oth = 0
    for ch in s:
        o = ord(ch)
        if 0x0900 <= o <= 0x097F or 0xA8E0 <= o <= 0xA8FF:
            dev += 1
        elif ch.isalpha():
            if o < 0x0250 or 0x1E00 <= o <= 0x1EFF or 0x2C60 <= o <= 0x2C7F or 0xA720 <= o <= 0xA7FF:
                lat += 1
            else:
                oth += 1
    return lat, dev, oth


def is_devanagari(s: str | None) -> bool:
    """True when ``s`` has Devanagari letters and no Latin letters."""
    if not s:
        return False
    lat, dev, _ = _script_counts(s)
    return dev > 0 and lat == 0


def is_latin(s: str | None) -> bool:
    """True when ``s`` has Latin letters and letters of no other script."""
    if not s:
        return False
    lat, dev, oth = _script_counts(s)
    return lat > 0 and dev == 0 and oth == 0


def _has_devanagari(s: str) -> bool:
    return any(0x0900 <= ord(ch) <= 0x097F for ch in s)


# ---------------------------------------------------------------------------
# Road class
# ---------------------------------------------------------------------------
_ROAD_CLASS: dict[str, RoadClass] = {
    "motorway": RoadClass.MOTORWAY,
    "trunk": RoadClass.TRUNK,
    "primary": RoadClass.PRIMARY,
    "secondary": RoadClass.SECONDARY,
    "tertiary": RoadClass.TERTIARY,
    "unclassified": RoadClass.UNCLASSIFIED,
    "residential": RoadClass.RESIDENTIAL,
    "living_street": RoadClass.LIVING_STREET,
    "service": RoadClass.SERVICE,
    "track": RoadClass.TRACK,
    "road": RoadClass.ROAD,
    "pedestrian": RoadClass.PEDESTRIAN,
    "footway": RoadClass.FOOTWAY,
    "path": RoadClass.PATH,
    "steps": RoadClass.STEPS,
    "cycleway": RoadClass.CYCLEWAY,
    "bridleway": RoadClass.BRIDLEWAY,
    # non-standard values seen in the wild
    "footpath": RoadClass.FOOTWAY,
    "sidewalk": RoadClass.FOOTWAY,
    "trail": RoadClass.PATH,
    "goreto": RoadClass.PATH,  # Nepali for footpath
    "stairs": RoadClass.STEPS,
}
_LINK_BASES = frozenset({"motorway", "trunk", "primary", "secondary", "tertiary"})
# Valid highway=* values that are not a routable way. Not recorded as unknown.
_HIGHWAY_NOT_ROUTABLE = frozenset({
    "bus_stop", "construction", "proposed", "planned", "platform", "raceway", "rest_area", "services",
    "turning_circle", "turning_loop", "mini_roundabout", "crossing", "street_lamp", "traffic_signals",
    "stop", "give_way", "milestone", "speed_camera", "speed_display", "elevator", "corridor", "abandoned",
    "disused", "razed", "demolished", "no", "emergency_access_point", "motorway_junction",
    "passing_place", "traffic_mirror", "trailhead", "emergency_bay", "bus_guideway", "busway", "escape",
    "via_ferrata", "ladder", "toll_gantry", "traffic_calming", "noexit", "ford", "viewpoint", "yes",
    "access_ramp", "incline", "steps_ramp", "virtual", "dummy",
})


def parse_road_class(tags: Mapping[str, str]) -> tuple[RoadClass, bool]:
    """``highway=*`` -> (class, is_link).

    ``*_link`` maps to its base class with ``is_link=True``. ``highway=road``
    is ``ROAD``. Anything that is not a routable way (``bus_stop``,
    ``construction``, ``proposed``, ``platform``, ``raceway`` ...) is
    ``UNKNOWN``.
    """
    v = _first(tags.get("highway"), hyphen=False)
    if not v:
        return RoadClass.UNKNOWN, False
    cls = _ROAD_CLASS.get(v)
    if cls is not None:
        return cls, False
    if v.endswith("_link"):
        base = v[:-5]
        if base in _LINK_BASES:
            return _ROAD_CLASS[base], True
    if v not in _HIGHWAY_NOT_ROUTABLE:
        _record("highway", tags.get("highway"))
    return RoadClass.UNKNOWN, False


# ---------------------------------------------------------------------------
# Surface
# ---------------------------------------------------------------------------
def _table(groups: dict[object, tuple[str, ...]]) -> dict[str, object]:
    out: dict[str, object] = {}
    for value, keys in groups.items():
        for k in keys:
            out[k] = value
    return out


_SURFACE: dict[str, Surface] = _table({
    Surface.ASPHALT: (
        "asphalt", "paved", "blacktopped", "black_topped", "blacktop", "black_top", "bitumin", "bitumen",
        "bituminous", "premix", "pre_mix", "ottaseal", "otta_seal", "chipseal", "chip_seal", "tarmac",
        "tarmacadam", "tar", "tarred", "pitch", "pitched", "metalled", "metaled", "tartan", "sealed",
        "kalopatre", "kalo_patre", "pakki", "pakka", "pucca", "pukka", "कालोपत्रे", "पक्की", "סלול",
    ),
    Surface.CONCRETE: (
        "concrete", "concrete:plates", "concrete:lanes", "cement", "cemented", "cement_concrete", "rcc",
        "pcc", "reinforced_concrete",
    ),
    Surface.BRICK: (
        "paving_stones", "paving_stone", "pavers", "paver", "bricks", "brick", "clinker", "interlock",
        "interlocking", "interlocking_tiles", "concrete_block_paved", "block_paved", "tiles",
    ),
    Surface.COBBLE: (
        "sett", "setts", "cobblestone", "cobblestones", "unhewn_cobblestone", "stone", "stones",
        "flagstones", "flagstone", "uneven_flagstones", "stone_steps", "stone_paved", "slabs",
    ),
    Surface.GRAVEL: (
        "gravel", "fine_gravel", "pebblestone", "pebbles", "shingle", "crushed_stone", "crushed_rock",
        "chippings", "grit", "graveled", "gravelled", "ग्राभेल",
    ),
    Surface.COMPACTED: ("compacted", "partially_paved", "murram", "laterite"),
    Surface.DIRT: (
        "unpaved", "dirt", "earth", "earthen", "ground", "clay", "soil", "dryriver", "dry_river", "woodchips",
        "possibly_unpaved", "looks_unpaved", "unsealed", "kachha", "kachcha", "kaccha", "kacha",
        "kachchi", "kacchi", "kachhi", "dhule", "कच्ची", "धुले", "בלתי_סלול",
    ),
    Surface.MUD: ("mud", "muddy"),
    Surface.SAND: ("sand", "sandy"),
    Surface.GRASS: ("grass", "grass_paver", "grass_pavers", "turf", "artificial_turf", "artificial_grass",
                    "plastic_grass", "decoturf"),
    Surface.ROCK: ("rock", "rocks", "rocky", "rokcs", "bare_rock", "boulders", "moraine", "scree"),
    Surface.SNOW_ICE: ("snow", "ice", "glacier"),
    Surface.WOOD: ("wood", "wooden", "timber"),
    Surface.METAL: ("metal", "metal_grid", "steel", "iron"),
})
# Prefix fallback ("EarthenTE228" -> earthen) only for keys this long, longest first.
_SURFACE_PREFIXES = sorted((k for k in _SURFACE if len(k) >= 5 and ":" not in k), key=lambda k: (-len(k), k))
_SURFACE_SPLIT = re.compile(r"[;/,|+&]|_and_|_or_")


def _surface_one(part: str) -> Surface | None:
    part = part.strip("_")
    if not part:
        return None
    s = _SURFACE.get(part)
    if s is not None:
        return s
    if ":" in part:  # asphalt:lanes, paving_stones:30, cobblestone:flattened
        s = _SURFACE.get(part.split(":", 1)[0])
        if s is not None:
            return s
    for k in _SURFACE_PREFIXES:
        if part.startswith(k):
            return _SURFACE[k]
    return None


def normalize_surface(raw: str | None) -> Surface | None:
    """``surface=*`` -> ``Surface``, or ``None`` when missing or unrecognised.

    Case-, whitespace- and hyphen-insensitive. Multi-values (``a;b``, ``a/b``,
    ``a_and_b``) give the first recognised part. Unrecognised values (``G``,
    ``3.5``, ``500 m``) are recorded under ``unknown_values["surface"]``.
    """
    if raw is None:
        return None
    parts = [p for p in (x.strip("_") for x in _SURFACE_SPLIT.split(_norm(raw))) if p]
    if not parts:
        return None
    for part in parts:
        hit = _surface_one(part)
        if hit is not None:
            return hit
    _record("surface", raw)
    return None


_TRACKTYPE_RE = re.compile(r"^(?:grade)?_?([1-5])(?![0-9])")
_TRACKTYPE_SURFACE = {
    1: Surface.CONCRETE, 2: Surface.GRAVEL, 3: Surface.COMPACTED, 4: Surface.DIRT, 5: Surface.DIRT,
}


def _tracktype(raw: object) -> int:
    if raw is None or isinstance(raw, bool):
        return 0
    if isinstance(raw, int):
        return raw if 1 <= raw <= 5 else 0
    m = _TRACKTYPE_RE.match(_norm(raw))
    return int(m.group(1)) if m else 0


def parse_tracktype(raw: str | int | None) -> int:
    """``tracktype=*`` -> 1..5 (``grade3``, ``3``, ``grade3-5`` -> 3), 0 when unknown."""
    v = _tracktype(raw)
    if v == 0 and raw is not None and not isinstance(raw, int) and _norm(raw):
        _record("tracktype", raw)
    return v


def surface_from_tracktype(raw: str | int | None) -> Surface | None:
    """grade1 CONCRETE, grade2 GRAVEL, grade3 COMPACTED, grade4/5 DIRT.

    Accepts the raw tag or the already parsed 1..5 integer. Ranges such as
    ``grade3-5`` use the first grade.
    """
    return _TRACKTYPE_SURFACE.get(parse_tracktype(raw))


_SMOOTHNESS: dict[str, Surface] = _table({
    Surface.ASPHALT: ("excellent", "good", "smooth"),
    Surface.COMPACTED: ("intermediate", "medium"),
    Surface.GRAVEL: ("bad", "rough"),
    Surface.DIRT: ("very_bad", "horrible", "off_road_wheels"),
    Surface.MUD: ("very_horrible",),
    Surface.ROCK: ("impassable",),
})


def surface_from_smoothness(raw: str | None) -> Surface | None:
    """excellent/good ASPHALT, intermediate COMPACTED, bad GRAVEL,
    very_bad/horrible DIRT, very_horrible MUD, impassable ROCK."""
    parts = _parts(raw)
    for p in parts:
        s = _SMOOTHNESS.get(p)
        if s is not None:
            return s
    if parts:
        _record("smoothness", raw)
    return None


# ---------------------------------------------------------------------------
# Numbers with units
# ---------------------------------------------------------------------------
_NUM = r"(\d+(?:[.,]\d+)*|[.,]\d+)"
_LEN_UNITS: dict[str, float] = {
    "": 1.0, "m": 1.0, "mt": 1.0, "mts": 1.0, "mtr": 1.0, "mtrs": 1.0, "meter": 1.0, "meters": 1.0,
    "metre": 1.0, "metres": 1.0, "cm": 0.01, "mm": 0.001, "km": 1000.0,
    "ft": 0.3048, "feet": 0.3048, "feets": 0.3048, "foot": 0.3048, "'": 0.3048,
    "in": 0.0254, "inch": 0.0254, "inches": 0.0254, '"': 0.0254, "''": 0.0254,
}
_UNIT = "(" + "|".join(re.escape(u) for u in sorted(_LEN_UNITS, key=len, reverse=True) if u) + ")?"
_FEET_INCHES_RE = re.compile(rf"{_NUM}\s*(?:'|ft|feet|foot)\s*{_NUM}\s*(?:\"|''|in|inch|inches)?")
_RANGE_RE = re.compile(rf"{_NUM}\s*{_UNIT}\s*(?:-|–|—|to)\s*{_NUM}\s*{_UNIT}")
_SINGLE_RE = re.compile(rf"([-+]?)\s*{_NUM}\s*{_UNIT}")
_APPROX_RE = re.compile(r"^(?:~|≈|±|\+/-|<=|>=|<|>|approx\.?|approximately|about|around|ca\.?|circa|c\.)\s*")
# "3.5 meters aprox", "7-8 meters (approx)"
_APPROX_WORD_RE = re.compile(r"\(?\s*\b(?:approx(?:imately|imate)?|aprox|apprx|approx)\b\.?\s*\)?")
_ASL_RE = re.compile(r"\s*(?:m\.?\s*a\.?\s*s\.?\s*l\.?|m\.?\s*s\.?\s*l\.?|amsl|masl|asl)$")


def _to_float(num: str) -> float | None:
    """'3,5' -> 3.5 (decimal comma); '8,848' and '1,234.5' -> thousands separators."""
    if "," in num:
        if "." in num:
            num = num.replace(",", "")
        else:
            head, *tail = num.split(",")
            if head not in ("", "0") and all(len(t) == 3 for t in tail):
                num = head + "".join(tail)
            elif len(tail) == 1:
                num = f"{head or '0'}.{tail[0]}"
            else:
                return None
    if num.count(".") > 1:
        return None
    try:
        v = float(num)
    except ValueError:
        return None
    return v if math.isfinite(v) else None


def _measure_text(raw: object) -> str:
    s = unicodedata.normalize("NFKC", str(raw)).translate(_DEVANAGARI_DIGITS).translate(_PRIMES)
    s = s.split(";", 1)[0].strip().lower()
    s = _APPROX_WORD_RE.sub(" ", s).strip()
    s = _APPROX_RE.sub("", s).rstrip(". ")
    return s


def _measure(raw: object, allow_negative: bool = False) -> float | None:
    """Parse a length in metres from free text; ``None`` when unparseable."""
    if raw is None:
        return None
    s = _measure_text(raw)
    if not s:
        return None
    m = _FEET_INCHES_RE.fullmatch(s)
    if m:
        ft, inch = _to_float(m.group(1)), _to_float(m.group(2))
        if ft is None or inch is None:
            return None
        return ft * 0.3048 + inch * 0.0254
    m = _RANGE_RE.fullmatch(s)
    if m:
        a, ua, b, ub = _to_float(m.group(1)), m.group(2) or "", _to_float(m.group(3)), m.group(4) or ""
        if a is None or b is None:
            return None
        fb = _LEN_UNITS[ub]
        fa = _LEN_UNITS[ua] if ua else fb
        return (a * fa + b * fb) / 2.0
    m = _SINGLE_RE.fullmatch(s)
    if m:
        v = _to_float(m.group(2))
        if v is None:
            return None
        if m.group(1) == "-":
            if not allow_negative:
                return None
            v = -v
        return v * _LEN_UNITS[m.group(3) or ""]
    return None


MAX_LENGTH_M = 200.0


def _length_value(raw: object) -> float | None:
    v = _measure(raw)
    return v if v is not None and 0.0 < v <= MAX_LENGTH_M else None


def parse_length_m(raw: str | None, key: str = "width") -> float | None:
    """Width or height in metres.

    Handles ``5``, ``5m``, ``5 m``, ``5.5 meters``, ``3,5``, ``~4``, ``4-6``
    (the mean, 5.0), ``12'``, ``12 ft``, ``12'6"`` and ``cm``/``mm``. Returns
    ``None`` for missing, unparseable, zero, negative or absurd (> 200 m)
    values. Values that are present but rejected are recorded under ``key``.
    """
    if raw is None or not str(raw).strip():
        return None
    v = _length_value(raw)
    if v is None:
        _record(key, raw)
    return v


MAX_LEVELS = 80
_LEVEL_WORDS = re.compile(
    r"\s*(?:floors?|stor(?:e)?ys?|stories|levels?|tallas?|तल्ला|मञ्जिल)\.?$")
_LEVEL_TOKENS: dict[str, float] = {
    "g": 1.0, "gf": 1.0, "ground": 1.0, "ground_floor": 1.0,
    # below ground or not a full storey: counted as 0 above-ground levels
    "b": 0.0, "lb": 0.0, "ub": 0.0, "sb": 0.0, "basement": 0.0, "lg": 0.0, "lgf": 0.0,
    "r": 0.0, "roof": 0.0, "t": 0.0, "terrace": 0.0, "rt": 0.0, "ph": 0.0, "mumty": 0.0,
}


def _levels_one(p: str) -> float | None:
    p = _LEVEL_WORDS.sub("", p.strip().rstrip("?")).strip()
    p = re.sub(r"^(?:up\s*to|upto)\s*", "", p)
    if not p:
        return None
    m = re.fullmatch(rf"{_NUM}\s*(?:and\s*(?:a\s*)?half|½|1/2)", p)  # "4 and half" (storeys)
    if m:
        v = _to_float(m.group(1))
        return None if v is None else v + 0.5
    if "+" in p:
        total = 0.0
        for tok in (t.strip() for t in p.split("+")):
            tok = _SPACES.sub("_", tok)
            if tok in _LEVEL_TOKENS:
                total += _LEVEL_TOKENS[tok]
            elif re.fullmatch(r"b\d", tok):  # B1, B2: basements
                continue
            else:
                v = _to_float(tok) if re.fullmatch(_NUM, tok) else None
                if v is None:
                    return None
                total += v
        return total
    if p in _LEVEL_TOKENS:
        return _LEVEL_TOKENS[p] or None
    m = re.fullmatch(rf"{_NUM}\s*(?:-|–|to)\s*{_NUM}", p)
    if m:
        a, b = _to_float(m.group(1)), _to_float(m.group(2))
        return None if a is None or b is None else max(a, b)
    if re.fullmatch(_NUM, p):
        return _to_float(p)
    return None


def _levels_value(raw: object) -> float | None:
    s = unicodedata.normalize("NFKC", str(raw)).translate(_DEVANAGARI_DIGITS).strip().lower()
    vals = [v for v in (_levels_one(p) for p in s.split(";") if p.strip()) if v is not None]
    if not vals:
        return None
    v = max(vals)
    return v if 1.0 <= v <= MAX_LEVELS else None


def parse_levels(raw: str | None, key: str = "building:levels") -> float | None:
    """Above-ground storeys.

    ``2``, ``2.5``; ``2;3`` -> 3 (the tallest part); ``G+2`` -> 3 (ground plus
    two); ``B+G+2`` -> 3 (basements do not count); ``3 floors``, ``3 storey``,
    ``३ तल्ला`` -> 3; ranges ``2-3`` -> 3. Values outside 1..80 give ``None``.
    """
    if raw is None or not str(raw).strip():
        return None
    v = _levels_value(raw)
    if v is None:
        _record(key, raw)
    return v


MIN_ELE_M, MAX_ELE_M = -500.0, 9000.0


def parse_ele(raw: str | None, key: str = "ele") -> float | None:
    """Elevation in metres: ``8848.86``, ``8,848 m``, ``8848m``, ``8848 masl``,
    ``29032 ft`` and ``29032'`` (converted to metres).

    ``None`` outside -500..9000 m. An exact ``0`` is treated as a placeholder
    and also gives ``None``: nowhere in or near Nepal is at sea level.
    """
    if raw is None or not str(raw).strip():
        return None
    s = _ASL_RE.sub(" m", _measure_text(raw))
    v = _measure(s, allow_negative=True)
    if v is None or v == 0.0 or not MIN_ELE_M <= v <= MAX_ELE_M:
        _record(key, raw)
        return None
    return v


def parse_lanes(raw: str | None) -> int:
    """``lanes=*`` -> 1..20, 0 when unknown. ``1.5`` -> 1, ``2;3`` and ``2-3`` -> 2."""
    if raw is None:
        return 0
    s = unicodedata.normalize("NFKC", str(raw)).translate(_DEVANAGARI_DIGITS)
    s = s.split(";", 1)[0].strip().lower()
    if not s:
        return 0
    m = re.fullmatch(r"(\d+(?:[.,]\d+)?)\s*(?:(?:-|to)\s*\d+(?:[.,]\d+)?)?\s*(?:lanes?)?", s)
    v = _to_float(m.group(1)) if m else None
    if v is None or not 1 <= v <= 20:
        _record("lanes", raw)
        return 0
    return int(math.floor(v))


_ONEWAY_FWD = frozenset({"yes", "true", "1"})
_ONEWAY_REV = frozenset({"-1", "reverse"})
_ONEWAY_BOTH = frozenset({"no", "false", "0", "alternating", "reversible"})


def parse_oneway(tags: Mapping[str, str], cls: RoadClass) -> int:
    """1 forward, -1 reverse, 0 both ways.

    Explicit ``oneway`` wins. Otherwise ``oneway=yes`` is implied on
    motorways, motorway links and ``junction=roundabout|circular``.
    """
    v = _first(tags.get("oneway"), hyphen=False)
    if v in _ONEWAY_FWD:
        return 1
    if v in _ONEWAY_REV:
        return -1
    if v in _ONEWAY_BOTH:
        return 0
    if v:
        _record("oneway", tags.get("oneway"))
    if cls == RoadClass.MOTORWAY or _tv(tags, "highway") == "motorway_link":
        return 1
    if _tv(tags, "junction") in ("roundabout", "circular"):
        return 1
    return 0


_SAC: dict[str, SacScale] = _table({
    SacScale.HIKING: ("hiking", "t1", "strolling"),
    SacScale.MOUNTAIN_HIKING: ("mountain_hiking", "t2"),
    SacScale.DEMANDING_MOUNTAIN_HIKING: ("demanding_mountain_hiking", "t3"),
    SacScale.ALPINE_HIKING: ("alpine_hiking", "t4"),
    SacScale.DEMANDING_ALPINE_HIKING: ("demanding_alpine_hiking", "t5"),
    SacScale.DIFFICULT_ALPINE_HIKING: ("difficult_alpine_hiking", "t6"),
})


def parse_sac_scale(raw: str | None) -> SacScale:
    """``sac_scale=*`` (or ``T1``..``T6``) -> ``SacScale``; ``UNKNOWN`` otherwise."""
    parts = _parts(raw)
    for p in parts:
        s = _SAC.get(p)
        if s is not None:
            return s
    if parts:
        _record("sac_scale", raw)
    return SacScale.UNKNOWN


_TRAIL_VIS = _table({
    1: ("excellent",), 2: ("good",), 3: ("intermediate", "medium"), 4: ("bad", "poor"),
    5: ("horrible",), 6: ("no", "none"),
})


def parse_trail_visibility(raw: str | None) -> int:
    """excellent 1, good 2, intermediate 3, bad 4, horrible 5, no 6, else 0."""
    parts = _parts(raw)
    for p in parts:
        v = _TRAIL_VIS.get(p)
        if v is not None:
            return int(v)
    if parts:
        _record("trail_visibility", raw)
    return 0


# ---------------------------------------------------------------------------
# Access
# ---------------------------------------------------------------------------
_F, _B, _M, _C, _J, _BUS, _H = (Travel.FOOT, Travel.BICYCLE, Travel.MOTORBIKE, Travel.CAR, Travel.JEEP,
                                Travel.BUS, Travel.HORSE)
_NONE = Travel(0)
_TRAIL_MODES = _F | _B | _H | _M

_DEFAULT_ACCESS: dict[RoadClass, Travel] = {
    RoadClass.UNKNOWN: _NONE,
    RoadClass.MOTORWAY: MOTOR_TRAVEL,
    RoadClass.TRUNK: ALL_TRAVEL,
    RoadClass.PRIMARY: ALL_TRAVEL,
    RoadClass.SECONDARY: ALL_TRAVEL,
    RoadClass.TERTIARY: ALL_TRAVEL,
    RoadClass.UNCLASSIFIED: ALL_TRAVEL,
    RoadClass.RESIDENTIAL: ALL_TRAVEL,
    RoadClass.LIVING_STREET: ALL_TRAVEL,
    RoadClass.ROAD: ALL_TRAVEL,
    RoadClass.SERVICE: ALL_TRAVEL & ~_BUS,
    RoadClass.TRACK: _F | _B | _M | _J | _H,
    RoadClass.PATH: _F | _B | _H,
    RoadClass.FOOTWAY: _F,
    RoadClass.PEDESTRIAN: _F,
    RoadClass.STEPS: _F,
    RoadClass.CYCLEWAY: _B | _F,
    RoadClass.BRIDLEWAY: _H | _F,
}
# The most a mode-specific "yes" (motorcycle=yes, bus=yes, foot=yes ...) can grant.
_CAP_SPECIFIC: dict[RoadClass, Travel] = {c: ALL_TRAVEL for c in RoadClass}
_CAP_SPECIFIC.update({c: _TRAIL_MODES for c in TRAIL_CLASSES})
_CAP_SPECIFIC[RoadClass.STEPS] = _F | _B
_CAP_SPECIFIC[RoadClass.UNKNOWN] = _NONE
# The most a generic "yes" (access / vehicle / motor_vehicle) can grant.
_CAP_GENERIC = dict(_CAP_SPECIFIC)
_CAP_GENERIC[RoadClass.TRACK] = ALL_TRAVEL & ~_BUS
_CAP_GENERIC[RoadClass.SERVICE] = ALL_TRAVEL & ~_BUS
_CAP_GENERIC[RoadClass.STEPS] = _F

_ACCESS_NO = frozenset({"no", "agricultural", "forestry", "military", "emergency", "restricted", "prohibited",
                        "none", "closed"})
_ACCESS_PRIVATE = frozenset({"private", "permit"})
_ACCESS_YES = frozenset({"yes", "designated", "permissive", "destination", "customers", "delivery", "official",
                         "public", "allowed", "true", "1"})
_ACCESS_NEUTRAL = frozenset({"unknown", "discouraged", "use_sidepath", "separate", "dismount", "optional_sidepath",
                             "limited", "variable"})
# (key, modes it governs, generic?) in increasing specificity; later layers win.
_ACCESS_LAYERS: tuple[tuple[str, Travel, bool], ...] = (
    ("access", ALL_TRAVEL, True),
    ("vehicle", _B | MOTOR_TRAVEL, True),
    ("motor_vehicle", MOTOR_TRAVEL, True),
    ("motorcar", _C | _J, False),
    ("motorcycle", _M, False),
    ("bicycle", _B, False),
    ("horse", _H, False),
    ("foot", _F, False),
)
_BUS_KEYS = ("hgv", "psv", "bus")


def _access_class(tags: Mapping[str, str], key: str) -> str:
    """'no' | 'private' | 'yes' | '' for one access key."""
    v = _tv(tags, key)
    if not v or v in _ACCESS_NEUTRAL:
        return ""
    if v in _ACCESS_NO:
        return "no"
    if v in _ACCESS_PRIVATE:
        return "private"
    if v in _ACCESS_YES:
        return "yes"
    _record(key, tags.get(key))
    return ""


def parse_access(tags: Mapping[str, str], cls: RoadClass) -> Travel:
    """Travel-profile mask for a way: class default, then tag overrides, then width.

    Class defaults:

    ======================================  =====================================
    MOTORWAY                                MOTORBIKE CAR JEEP BUS (no foot)
    TRUNK, PRIMARY..TERTIARY, UNCLASSIFIED,  all
    RESIDENTIAL, LIVING_STREET, ROAD
    SERVICE                                 all but BUS
    TRACK                                   FOOT BICYCLE MOTORBIKE JEEP HORSE,
                                            + CAR when tracktype is grade1/grade2
    PATH                                    FOOT BICYCLE HORSE
    FOOTWAY, PEDESTRIAN, STEPS              FOOT
    CYCLEWAY                                BICYCLE FOOT
    BRIDLEWAY                               HORSE FOOT
    UNKNOWN                                 nothing
    ======================================  =====================================

    Overrides, from general to specific (later wins). Values ``no``,
    ``agricultural``, ``forestry`` and ``military`` count as *no*. ``private``
    and ``permit`` count as *private*. ``yes``, ``designated``, ``permissive``,
    ``destination``, ``customers`` and ``delivery`` count as *yes*:

    * ``access``: *no* removes every mode except FOOT (people still walk there
      unless ``foot=no``); *private* removes BUS; *yes* restores modes.
    * ``vehicle``: covers BICYCLE and the motor modes (FOOT and HORSE stay).
    * ``motor_vehicle``: covers MOTORBIKE, CAR, JEEP and BUS.
    * ``motorcar`` covers CAR and JEEP; ``motorcycle`` MOTORBIKE; ``bicycle``,
      ``horse`` and ``foot`` their own mode.
    * ``hgv``, ``psv``, ``bus``: any *no* removes BUS; otherwise any *yes* adds it.

    A *yes* never grants more than the class can physically carry. Trails
    (PATH, FOOTWAY, PEDESTRIAN, CYCLEWAY, BRIDLEWAY) cap at FOOT, BICYCLE,
    HORSE and MOTORBIKE, so ``motorcycle=yes`` or ``motor_vehicle=yes`` on a path
    adds MOTORBIKE only. STEPS cap at FOOT and BICYCLE. On a TRACK or SERVICE
    road a generic *yes* never adds BUS (a track gains CAR); only ``bus=yes``,
    ``psv=yes`` or ``hgv=yes`` does.

    Finally a tagged ``width`` (or ``est_width``) below 3.0 m removes BUS and
    CAR, and below 2.0 m it also removes JEEP.
    """
    mask = _DEFAULT_ACCESS.get(cls, _NONE)
    if cls == RoadClass.TRACK and _tracktype(tags.get("tracktype")) in (1, 2):
        mask |= _C
    cap_generic, cap_specific = _CAP_GENERIC.get(cls, _NONE), _CAP_SPECIFIC.get(cls, _NONE)
    for key, scope, generic in _ACCESS_LAYERS:
        verdict = _access_class(tags, key)
        if not verdict:
            continue
        if verdict == "no":
            mask &= ~(scope & ~_F) if key == "access" else ~scope
        elif verdict == "private":
            mask &= ~(scope & _BUS)
        else:
            mask |= scope & (cap_generic if generic else cap_specific)
    bus = [_access_class(tags, k) for k in _BUS_KEYS]
    if "no" in bus:
        mask &= ~_BUS
    elif "private" in bus:
        mask &= ~_BUS
    elif "yes" in bus:
        mask |= _BUS & cap_specific
    width = _length_value(tags.get("width"))
    if width is None:
        width = _length_value(tags.get("est_width"))
    if width is not None:
        if width < 3.0:
            mask &= ~(_BUS | _C)
        if width < 2.0:
            mask &= ~_J
    return Travel(mask)


# ---------------------------------------------------------------------------
# Names
# ---------------------------------------------------------------------------
_ALT_NAME_KEYS = (
    "alt_name", "old_name", "official_name", "short_name", "loc_name",
    "alt_name:en", "alt_name:ne", "official_name:en", "official_name:ne",
    "old_name:en", "old_name:ne", "short_name:en", "short_name:ne", "int_name",
)


def parse_name(tags: Mapping[str, str]) -> NameRec | None:
    """Name record from ``name``, ``name:en``, ``name:ne``, ``int_name`` and alternates.

    * ``default`` = ``name``.
    * ``en`` = ``name:en``, else ``int_name``, else ``name`` when it is Latin
      script. A ``name:en`` written in Devanagari is moved to ``alt``.
    * ``ne`` = ``name:ne``, else ``name`` when it is Devanagari. A ``name:ne``
      with no Devanagari in it is moved to ``alt``. Nothing is transliterated
      (ADR-005).
    * ``alt`` = alt_name, old_name, official_name, short_name, loc_name, their
      ``:en``/``:ne`` variants and int_name, split on ``;``, de-duplicated
      case-insensitively and excluding anything equal to default, en or ne.

    Returns ``None`` when every field is empty.
    """
    default = _clean_text(tags.get("name"))
    demoted: list[str] = []
    en = _clean_text(tags.get("name:en"))
    if en and is_devanagari(en):
        demoted.append(en)
        en = ""
    if not en:
        int_name = _clean_text(tags.get("int_name"))
        if int_name and not is_devanagari(int_name):
            en = int_name
    if not en and is_latin(default):
        en = default
    ne = _clean_text(tags.get("name:ne"))
    if ne and not _has_devanagari(ne):
        demoted.append(ne)
        ne = ""
    if not ne and is_devanagari(default):
        ne = default

    seen = {x.casefold() for x in (default, en, ne) if x}
    alt: list[str] = []
    candidates = demoted + [p for k in _ALT_NAME_KEYS for p in str(tags.get(k) or "").split(";")]
    for raw in candidates:
        v = _clean_text(raw)
        if v and v.casefold() not in seen:
            seen.add(v.casefold())
            alt.append(v)
    if not (default or en or ne or alt):
        return None
    return NameRec(default=default, en=en, ne=ne, alt=tuple(alt))


# ---------------------------------------------------------------------------
# Buildings
# ---------------------------------------------------------------------------
_TOKEN_SPLIT = re.compile(r"[:,/&+_.]+")


def _lookup(table: Mapping[str, object], raw: object, key: str, ignore: frozenset[str] = frozenset()):
    """Look a free-text value up in ``table``; ``None`` when nothing matches.

    Tries each ``;`` part whole, then each word of it ("stone & wood" -> stone,
    "sheet_metal" -> metal, "Mrityunjay Mandir" -> mandir). Values in
    ``ignore`` (valid but meaningless, such as ``mixed``) are not recorded as
    unknown.
    """
    parts = _parts(raw)
    for p in parts:
        hit = table.get(p)
        if hit is not None:
            return hit
    for p in parts:
        if p in ignore:
            continue
        for tok in _TOKEN_SPLIT.split(p):
            if tok in ignore:
                break
            hit = table.get(tok)
            if hit is not None:
                return hit
    if any(p not in ignore for p in parts):
        _record(key, raw)
    return None


_BUILDING_USE: dict[str, BuildingUse] = _table({
    BuildingUse.HOUSE: (
        "house", "detached", "semidetached_house", "semi_detached", "semi", "terrace", "terraced_house",
        "bungalow", "residential", "home", "residence", "residental", "residencial", "villa", "houseboat",
        "dwelling_house", "farmhouse_residential", "niwas", "ghar",
    ),
    BuildingUse.APARTMENTS: ("apartments", "apartment", "flats", "flat", "dormitory", "hostel_dormitory"),
    BuildingUse.COMMERCIAL: (
        "commercial", "retail", "shop", "shops", "supermarket", "kiosk", "mall", "market", "marketplace",
        "store", "restaurant", "cafe", "bank", "pharmacy", "bakery", "bakehouse", "business", "showroom",
        "grocery", "fuel", "kirana", "pasal", "canteen", "cafeteria", "mart", "petrol", "gas_station",
    ),
    BuildingUse.MIXED_USE: ("mixed_use", "mixed", "multipurpose"),
    BuildingUse.EDUCATION: (
        "school", "college", "university", "kindergarten", "educational", "education", "school_building",
        "library", "vidyalaya", "coaching", "coaching_centre",
    ),
    BuildingUse.HEALTH: (
        "hospital", "clinic", "health_post", "healthpost", "health", "health_centre", "health_center",
        "dispensary", "health_facility", "healthcare",
    ),
    BuildingUse.PUBLIC: (
        "government", "public", "civic", "townhall", "police", "fire_station", "community_centre",
        "community_hall", "hall", "post_office", "museum", "military", "barracks", "prison", "toilets",
        "train_station", "transportation", "stadium", "sports_hall", "sports_centre", "grandstand",
        "administrative", "institutional", "government_office", "airport_terminal", "community",
    ),
    BuildingUse.INDUSTRIAL: (
        "industrial", "warehouse", "factory", "manufacture", "kiln", "brick_kiln", "workshop", "storage",
        "service", "storage_tank", "water_tower", "transformer_tower", "power", "substation", "silo",
        "digester", "hangar",
    ),
    BuildingUse.RELIGIOUS: (
        "temple", "church", "mosque", "monastery", "stupa", "shrine", "religious", "chapel", "cathedral",
        "gompa", "gumba", "gumpa", "pagoda", "chorten", "mandir", "gurudwara", "synagogue",
        "place_of_worship", "wayside_shrine", "vihar", "vihara", "bihar", "chaitya", "dewal", "deval", "eidgah",
        "meditation", "meditation_center", "meditation_centre",
    ),
    BuildingUse.HOTEL: (
        "hotel", "hostel", "guest_house", "guesthouse", "motel", "lodge", "resort", "homestay", "teahouse",
        "tea_house",
    ),
    BuildingUse.HUT: (
        "hut", "shed", "cowshed", "barn", "stable", "sty", "farm_auxiliary", "cabin", "goat_shed",
        "poultry_house", "chicken_coop", "henhouse", "boathouse", "allotment_house", "static_caravan",
        "tent", "kitchen", "outbuilding", "gatehouse", "guardhouse", "romney", "livestock",
    ),
    BuildingUse.GREENHOUSE: ("greenhouse", "greenhouse_horticulture", "glasshouse"),
    BuildingUse.CONSTRUCTION: ("construction", "under_construction"),
    BuildingUse.ROOF: ("roof", "carport", "canopy", "pavilion", "shelter", "bus_stop"),
    BuildingUse.GARAGE: ("garage", "garages", "parking"),
    BuildingUse.OFFICE: ("office", "offices", "laboratory"),
    BuildingUse.FARM: ("farm", "farmhouse", "agriculture", "agricultural"),
})
# Valid building=* values that say nothing about use. Not recorded as unknown.
_BUILDING_GENERIC = frozenset({
    "yes", "true", "1", "unknown", "building", "ruins", "ruin", "abandoned", "collapsed", "damaged",
    "destroyed", "demolished", "disused", "tower", "bunker", "container", "bridge", "gate", "wall", "part",
    "no", "no_roof", "historical", "historic", "small_building", "new_building", "uncertain",
})
_AMENITY_USE: dict[str, BuildingUse] = _table({
    BuildingUse.RELIGIOUS: ("place_of_worship", "monastery", "temple"),
    BuildingUse.EDUCATION: ("school", "college", "university", "kindergarten", "library", "language_school",
                            "music_school", "training", "research_institute"),
    BuildingUse.HEALTH: ("hospital", "clinic", "health_post", "doctors", "dentist", "nursing_home", "veterinary"),
    BuildingUse.PUBLIC: ("townhall", "police", "fire_station", "courthouse", "post_office", "community_centre",
                         "public_building", "government", "prison", "social_facility", "public_office",
                         "toilets", "events_venue", "arts_centre", "theatre", "cinema"),
    BuildingUse.COMMERCIAL: ("restaurant", "cafe", "fast_food", "food_court", "bank", "bar", "pub", "marketplace",
                             "fuel", "pharmacy", "money_transfer", "bureau_de_change", "nightclub",
                             "ice_cream", "car_wash", "internet_cafe", "driving_school"),
    BuildingUse.GARAGE: ("parking",),
    BuildingUse.ROOF: ("shelter",),
})
_TOURISM_USE: dict[str, BuildingUse] = _table({
    BuildingUse.HOTEL: ("hotel", "guest_house", "hostel", "motel", "alpine_hut", "wilderness_hut", "apartment",
                        "chalet"),
    BuildingUse.PUBLIC: ("museum",),
})


def _use_from_other_tags(tags: Mapping[str, str]) -> BuildingUse:
    use = _BUILDING_USE.get(_tv(tags, "building:use"))
    if use is not None:
        return use
    use = _AMENITY_USE.get(_tv(tags, "amenity"))
    if use is not None:
        return use
    use = _TOURISM_USE.get(_tv(tags, "tourism"))
    if use is not None:
        return use
    office = _tv(tags, "office")
    if office:
        return BuildingUse.PUBLIC if office == "government" else BuildingUse.OFFICE
    if _tv(tags, "shop") not in ("", "no", "vacant"):
        return BuildingUse.COMMERCIAL
    if _tv(tags, "man_made") in ("stupa", "chorten") or _tv(tags, "historic") in ("temple", "monastery"):
        return BuildingUse.RELIGIOUS
    return BuildingUse.UNKNOWN


def parse_building_use(tags: Mapping[str, str]) -> BuildingUse:
    """``building=*`` -> ``BuildingUse``.

    Plain ``building=yes`` (97.5% of Nepal) and other use-less values are
    refined from ``building:use``, ``amenity``, ``tourism``, ``office`` and
    ``shop`` on the same object, so a ``building=yes`` + ``amenity=school`` is
    ``EDUCATION``.
    """
    use = _lookup(_BUILDING_USE, tags.get("building"), "building", _BUILDING_GENERIC)
    if use is None or use == BuildingUse.UNKNOWN:
        use = _use_from_other_tags(tags)
    return use


_ROOF_SHAPE: dict[str, RoofShape] = _table({
    RoofShape.FLAT: ("flat",),
    RoofShape.GABLED: ("gabled", "gable", "saltbox", "double_saltbox", "crosspitched", "cross_gabled",
                       "side_gabled", "double_pitch", "pitched", "pitch"),
    RoofShape.HIPPED: ("hipped", "hip", "side_hipped", "quadruple_saltbox", "hipped_and_gabled"),
    RoofShape.PYRAMIDAL: ("pyramidal", "pyramid"),
    RoofShape.SKILLION: ("skillion", "lean_to", "monopitch", "shed", "butterfly", "sawtooth", "slope"),
    RoofShape.DOME: ("dome", "domed"),
    RoofShape.ROUND: ("round", "barrel", "arched", "vaulted", "curved", "circular"),
    RoofShape.PAGODA: ("pagoda", "tiered", "multi_tier", "multi_tiered"),
    RoofShape.SHIKHARA: ("shikhara", "sikhara", "shikara"),
    RoofShape.GAMBREL: ("gambrel",),
    RoofShape.MANSARD: ("mansard",),
    RoofShape.HALF_HIPPED: ("half_hipped", "half_hip", "jerkinhead"),
    RoofShape.ONION: ("onion",),
    RoofShape.CONE: ("cone", "conical", "spire"),
})


# "mixed" is on ~3 000 Kathmandu roofs from one import; valid but shapeless.
_MIXED = frozenset({"mixed", "mix", "mixeded", "complex", "complex_regular", "complex_irregular", "other",
                    "unknown", "yes", "many", "multiple", "various"})


def parse_roof_shape(raw: str | None) -> RoofShape:
    """``roof:shape=*`` -> ``RoofShape`` (``half-hipped`` and ``half_hipped`` alike;
    ``double_pitch``/``pitched`` GABLED; ``mixed`` and ``complex_*`` UNKNOWN)."""
    hit = _lookup(_ROOF_SHAPE, raw, "roof:shape", _MIXED)
    return RoofShape.UNKNOWN if hit is None else hit


_ROOF_MATERIAL: dict[str, RoofMaterial] = _table({
    RoofMaterial.METAL: ("metal", "metal_sheet", "metal_sheets", "tin", "tin_sheet", "corrugated_iron",
                         "corrugated_metal", "corrugated", "cgi", "zinc", "steel", "aluminium", "aluminum",
                         "iron", "copper", "sheet_metal", "jasta"),
    RoofMaterial.TILES: ("roof_tiles", "tile", "tiles", "clay", "clay_tiles", "terracotta", "khapada", "jhingati"),
    RoofMaterial.SLATE: ("slate", "slates", "stone_tile", "stone_tiles"),
    RoofMaterial.STONE: ("stone", "stone_slabs"),
    RoofMaterial.THATCH: ("thatch", "grass", "straw", "reed", "khar", "hay"),
    RoofMaterial.CONCRETE: ("concrete", "concerte", "concreate", "concret", "cement", "rcc", "rbc", "slab",
                            "plaster", "reinforced_concrete", "eternit", "tar_paper", "asphalt", "bitumen",
                            "asbestos"),
    RoofMaterial.WOOD: ("wood", "timber", "wooden", "wood_shingles", "shingles", "bamboo"),
    RoofMaterial.GLASS: ("glass", "plastic", "polycarbonate", "acrylic_glass", "upvc"),
    RoofMaterial.MUD: ("mud", "earth", "adobe"),
})


def parse_roof_material(raw: str | None) -> RoofMaterial:
    """``roof:material=*`` -> ``RoofMaterial``.

    Corrugated iron (``tin``, ``cgi``, ``metal_sheet``) is by far the most common
    roof in rural Nepal. Real typos are accepted (``concerte`` is on 800+
    roofs; ``rbc`` is reinforced brick concrete). A shape in the material key
    (``flat``) is ignored.
    """
    hit = _lookup(_ROOF_MATERIAL, raw, "roof:material", _MIXED | {"flat"})
    return RoofMaterial.UNKNOWN if hit is None else hit


_WALL_MATERIAL: dict[str, WallMaterial] = _table({
    WallMaterial.BRICK: ("brick", "bricks", "brics", "brickwork", "brick_masonry"),
    WallMaterial.STONE: ("stone", "stones", "stone_masonry", "sandstone", "limestone", "granite", "marble"),
    WallMaterial.PLASTER: ("concrete", "concreate", "concerte", "cement", "plaster", "plastered", "rcc", "rbc",
                           "cement_block", "cement_blocks",
                           "concrete_block", "concrete_blocks", "block", "blocks", "reinforced_concrete",
                           "render", "rendered", "stucco"),
    WallMaterial.MUD: ("mud", "adobe", "earth", "clay", "rammed_earth", "cob", "mud_brick"),
    WallMaterial.WOOD: ("wood", "timber", "wooden", "log", "logs"),
    WallMaterial.BAMBOO: ("bamboo",),
    WallMaterial.METAL: ("metal", "steel", "tin", "corrugated_iron", "iron", "metal_sheet", "cgi"),
    WallMaterial.GLASS: ("glass",),
})


def parse_wall_material(raw: str | None) -> WallMaterial:
    """``building:material=*`` -> ``WallMaterial`` (concrete and cement render are
    PLASTER). Mixed values give the first material named: ``stone & wood`` STONE."""
    hit = _lookup(_WALL_MATERIAL, raw, "building:material", _MIXED | {"demolished", "not_constructed"})
    return WallMaterial.UNKNOWN if hit is None else hit


# ---------------------------------------------------------------------------
# Places
# ---------------------------------------------------------------------------
_PLACE: dict[str, PlaceKind] = {
    "country": PlaceKind.COUNTRY,
    "state": PlaceKind.PROVINCE,
    "province": PlaceKind.PROVINCE,
    "city": PlaceKind.CITY,
    "town": PlaceKind.TOWN,
    "village": PlaceKind.VILLAGE,
    "hamlet": PlaceKind.HAMLET,
    "isolated_dwelling": PlaceKind.ISOLATED_DWELLING,
    "suburb": PlaceKind.SUBURB,
    "borough": PlaceKind.SUBURB,
    "neighbourhood": PlaceKind.NEIGHBOURHOOD,
    "neighborhood": PlaceKind.NEIGHBOURHOOD,
    "city_block": PlaceKind.NEIGHBOURHOOD,
    "quarter": PlaceKind.QUARTER,
    "locality": PlaceKind.LOCALITY,
    "farm": PlaceKind.FARM,
    "square": PlaceKind.SQUARE,
    "island": PlaceKind.ISLAND,
    "islet": PlaceKind.ISLAND,
    "district": PlaceKind.DISTRICT,
    "county": PlaceKind.DISTRICT,
    "municipality": PlaceKind.LOCAL_LEVEL,
    "ward": PlaceKind.WARD,
}
# Valid place=* values with no PlaceKind. Not recorded as unknown.
_PLACE_IGNORED = frozenset({"region", "plot", "sea", "ocean", "continent", "archipelago", "allotments"})


def parse_place_kind(tags: Mapping[str, str]) -> PlaceKind:
    """``place=*`` -> ``PlaceKind``; ``NONE`` for free text (``toukhel 3``, phone numbers)."""
    raw = tags.get("place")
    v = _first(raw, hyphen=False)
    if not v:
        return PlaceKind.NONE
    kind = _PLACE.get(v)
    if kind is not None:
        return kind
    if v not in _PLACE_IGNORED:
        _record("place", raw)
    return PlaceKind.NONE


# ---------------------------------------------------------------------------
# POIs
# ---------------------------------------------------------------------------
_RELIGION_ALIASES = {
    "hindu": "hindu", "hinduism": "hindu",
    "buddhist": "buddhist", "buddhism": "buddhist", "budhhist": "buddhist", "buddist": "buddhist",
    "budhist": "buddhist", "budhdhist": "buddhist", "bon": "buddhist",
    "muslim": "muslim", "islam": "muslim", "islamic": "muslim",
    "christian": "christian", "christianity": "christian",
}
_LODGING = frozenset({"hotel", "motel", "guest_house", "hostel", "camp_site", "caravan_site", "alpine_hut",
                      "wilderness_hut", "apartment", "chalet"})
_HERITAGE_SQUARE_RE = re.compile(r"durbar\s*square|दरबार\s*स्क्वायर")
_STONE_TAP_RE = re.compile(r"\bhiti\b|dhunge\s*dhara|dhungedhara|ढुङ्गे\s*धारा|हिटी")
_HOT_SPRING_RE = re.compile(r"tatopani|tato\s*pani|hot\s*spring|तातोपानी")
_ACTIVITY_NAME_RULES: tuple[tuple[re.Pattern, PoiKind], ...] = (
    (re.compile(r"bunge+|bungy"), PoiKind.BUNGEE),
    (re.compile(r"zip\s*-?\s*lin(?:e|ing)|zip\s*-?\s*flyer"), PoiKind.ZIPLINE),
    (re.compile(r"paraglid"), PoiKind.PARAGLIDING),
    (re.compile(r"rafting"), PoiKind.RAFTING),
    (re.compile(r"safari"), PoiKind.SAFARI),
)


def _religion(tags: Mapping[str, str]) -> str:
    for p in _parts(tags.get("religion"), hyphen=False):
        r = _RELIGION_ALIASES.get(p)
        if r:
            return r
    return ""


def _names_text(tags: Mapping[str, str]) -> str:
    return " | ".join(str(tags[k]) for k in ("name", "name:en", "int_name", "alt_name", "name:ne") if tags.get(k)
                      ).casefold()


def poi_kind(tags: Mapping[str, str]) -> PoiKind:
    """Classify a point of interest. Rules are tried in this order; the first match wins:

    1. **Religious.** Stupas (``man_made``/``building``/``historic``/
       ``place_of_worship=stupa``) and chortens come before generic places of
       worship. ``man_made=mani_wall`` gives MANI_WALL. For
       ``amenity=place_of_worship`` (or the non-standard ``amenity=temple``),
       ``place_of_worship=shrine`` gives SHRINE; otherwise ``religion``
       decides: hindu TEMPLE_HINDU, buddhist/bon GOMPA, muslim MOSQUE,
       christian CHURCH. With no usable religion, ``building=mosque|church|
       monastery`` decides, and anything else is PLACE_OF_WORSHIP.
       ``amenity=monastery`` gives GOMPA; ``historic=wayside_shrine`` SHRINE.
    2. **Heritage.** ``place=square``, or a non-commercial object named
       "... Durbar Square", is HERITAGE_SQUARE. Then historic castle/palace
       PALACE, city_gate CITY_GATE, stone tap (``historic=stone_tap``, or a
       water tap / drinking water that is historic or named *hiti*/*dhunge
       dhara*) STONE_TAP, museum MUSEUM, monument/memorial MONUMENT,
       ruins/archaeological_site RUINS.
    3. **Nature.** peak/volcano, waterfall, cave, hot spring (``natural=hot_spring``,
       a hot ``public_bath``, or a spring or bath named *Tatopani*) before plain
       spring, glacier, pass (``mountain_pass=yes``, ``natural=saddle``), spring,
       a named ``natural=water`` lake or reservoir LAKE, viewpoint, a named tree
       NOTABLE_TREE, ridge.
    4. **Transport.** aerodrome, helipad/heliport, bus station, aerial-way
       station, railway station/halt, fuel, taxi.
    5. **Activities by tag.** ``sport=free_flying`` PARAGLIDING,
       ``aerialway=zip_line`` ZIPLINE, ``sport=bungee`` BUNGEE,
       ``sport=rafting|canoe`` RAFTING, ``amenity=boat_rental`` BOATING.
    6. **Lodging.** hotel/motel, guest_house/apartment/chalet, hostel,
       camp/caravan site, alpine/wilderness hut TEAHOUSE.
    7. **Food.** restaurant/fast_food/food_court RESTAURANT, cafe CAFE.
    8. **Markets and shops.** ``amenity=marketplace``, then any ``shop=*``.
    9. **Activities by name.** bungee/bungy, zip line/zip flyer, paragliding,
       rafting, safari. These come after lodging and food so that
       "Jungle Safari Lodge" stays a hotel.
    10. **Other tourism.** theme park, zoo, picnic site, attraction, information.
    11. **Green space.** park/garden PARK, national park / protected area /
        nature reserve PROTECTED_AREA.
    12. **Civic.** school/college/university/kindergarten SCHOOL,
        hospital/clinic/health_post HOSPITAL, townhall/courthouse/
        ``office=government`` GOVERNMENT, bank BANK.
    13. **Infrastructure.** parking PARKING, ``man_made=bridge`` BRIDGE.
    """
    g = lambda k: _tv(tags, k)  # noqa: E731
    amenity, tourism, historic = g("amenity"), g("tourism"), g("historic")
    man_made, building = g("man_made"), g("building")
    natural, leisure, shop = g("natural"), g("leisure"), g("shop")
    pow_kind = g("place_of_worship")
    named = bool(_clean_text(tags.get("name")) or _clean_text(tags.get("name:en")))

    # 1. religious
    structures = (man_made, building, historic, pow_kind)
    if "stupa" in structures:
        return PoiKind.STUPA
    if "chorten" in structures:
        return PoiKind.CHORTEN
    if man_made == "mani_wall" or historic == "mani_wall" or pow_kind == "mani_wall":
        return PoiKind.MANI_WALL
    if amenity in ("place_of_worship", "temple"):
        if pow_kind in ("shrine", "wayside_shrine"):
            return PoiKind.SHRINE
        rel = _religion(tags)
        if rel == "hindu":
            return PoiKind.TEMPLE_HINDU
        if rel == "buddhist":
            return PoiKind.GOMPA
        if rel == "muslim":
            return PoiKind.MOSQUE
        if rel == "christian":
            return PoiKind.CHURCH
        if building == "mosque":
            return PoiKind.MOSQUE
        if building in ("church", "chapel", "cathedral"):
            return PoiKind.CHURCH
        if building in ("monastery", "gompa"):
            return PoiKind.GOMPA
        return PoiKind.PLACE_OF_WORSHIP
    if amenity == "monastery":
        return PoiKind.GOMPA
    if historic in ("wayside_shrine", "shrine"):
        return PoiKind.SHRINE

    # 2. heritage
    commercial = bool(amenity or shop or g("office") or tourism in _LODGING)
    if g("place") == "square" or (not commercial and _HERITAGE_SQUARE_RE.search(_names_text(tags))):
        return PoiKind.HERITAGE_SQUARE
    if historic in ("castle", "palace") or building == "palace" or g("castle_type") == "palace":
        return PoiKind.PALACE
    if historic == "city_gate" or g("barrier") == "city_gate":
        return PoiKind.CITY_GATE
    if historic == "stone_tap":
        return PoiKind.STONE_TAP
    if man_made == "water_tap" or amenity in ("drinking_water", "water_point", "fountain"):
        if historic or _STONE_TAP_RE.search(_names_text(tags)):
            return PoiKind.STONE_TAP
    if tourism == "museum" or amenity == "museum":
        return PoiKind.MUSEUM
    if historic in ("monument", "memorial"):
        return PoiKind.MONUMENT
    if historic in ("ruins", "archaeological_site"):
        return PoiKind.RUINS

    # 3. nature
    if natural in ("peak", "volcano"):
        return PoiKind.PEAK
    if g("waterway") == "waterfall" or natural == "waterfall":
        return PoiKind.WATERFALL
    if natural in ("cave_entrance", "cave"):
        return PoiKind.CAVE
    if natural == "hot_spring" or (amenity == "public_bath" and g("bath:type") == "hot_spring"):
        return PoiKind.HOT_SPRING
    if (natural == "spring" or amenity == "public_bath") and _HOT_SPRING_RE.search(_names_text(tags)):
        return PoiKind.HOT_SPRING
    if natural == "glacier":
        return PoiKind.GLACIER
    if g("mountain_pass") == "yes" or natural == "saddle":
        return PoiKind.PASS
    if natural == "spring":
        return PoiKind.SPRING
    if natural == "water" and named and g("water") in ("", "lake", "oxbow", "lagoon", "reservoir"):
        return PoiKind.LAKE
    if tourism == "viewpoint":
        return PoiKind.VIEWPOINT
    if natural == "tree" and named:
        return PoiKind.NOTABLE_TREE
    if natural == "ridge":
        return PoiKind.RIDGE

    # 4. transport
    aeroway = g("aeroway")
    if aeroway in ("aerodrome", "airport"):
        return PoiKind.AIRPORT
    if aeroway in ("helipad", "heliport"):
        return PoiKind.HELIPAD
    if amenity == "bus_station":
        return PoiKind.BUS_STATION
    if g("aerialway") == "station":
        return PoiKind.CABLE_CAR_STATION
    if g("railway") in ("station", "halt"):
        return PoiKind.RAILWAY_STATION
    if amenity == "fuel":
        return PoiKind.FUEL
    if amenity == "taxi":
        return PoiKind.TAXI_STAND

    # 5. activities by tag
    sport = g("sport")
    if sport == "free_flying":
        return PoiKind.PARAGLIDING
    if g("aerialway") == "zip_line":
        return PoiKind.ZIPLINE
    if sport in ("bungee", "bungee_jumping"):
        return PoiKind.BUNGEE
    if sport in ("rafting", "canoe", "whitewater"):
        return PoiKind.RAFTING
    if amenity == "boat_rental":
        return PoiKind.BOATING

    # 6. lodging
    if tourism in ("hotel", "motel"):
        return PoiKind.HOTEL
    if tourism in ("guest_house", "apartment", "chalet"):
        return PoiKind.GUEST_HOUSE
    if tourism == "hostel":
        return PoiKind.HOSTEL
    if tourism in ("camp_site", "caravan_site"):
        return PoiKind.CAMP_SITE
    if tourism in ("alpine_hut", "wilderness_hut"):
        return PoiKind.TEAHOUSE

    # 7. food
    if amenity in ("restaurant", "fast_food", "food_court"):
        return PoiKind.RESTAURANT
    if amenity == "cafe":
        return PoiKind.CAFE

    # 8. markets and shops
    if amenity == "marketplace":
        return PoiKind.MARKETPLACE
    if shop and shop not in ("no", "vacant"):
        return PoiKind.SHOP

    # 9. activities by name
    if named:
        text = _names_text(tags)
        for rx, kind in _ACTIVITY_NAME_RULES:
            if rx.search(text):
                return kind

    # 10. other tourism
    if tourism == "theme_park":
        return PoiKind.THEME_PARK
    if tourism == "zoo":
        return PoiKind.ZOO
    if tourism == "picnic_site":
        return PoiKind.PICNIC_SITE
    if tourism == "attraction":
        return PoiKind.ATTRACTION
    if tourism == "information":
        return PoiKind.INFORMATION

    # 11. green space
    if leisure in ("park", "garden"):
        return PoiKind.PARK
    if g("boundary") in ("national_park", "protected_area") or leisure == "nature_reserve":
        return PoiKind.PROTECTED_AREA

    # 12. civic
    if amenity in ("school", "college", "university", "kindergarten"):
        return PoiKind.SCHOOL
    if amenity in ("hospital", "clinic", "health_post") or g("healthcare") in ("hospital", "clinic"):
        return PoiKind.HOSPITAL
    if amenity in ("townhall", "courthouse") or g("office") == "government":
        return PoiKind.GOVERNMENT
    if amenity == "bank":
        return PoiKind.BANK

    # 13. infrastructure
    if amenity == "parking":
        return PoiKind.PARKING
    if man_made == "bridge":
        return PoiKind.BRIDGE
    return PoiKind.NONE


# ---------------------------------------------------------------------------
# Areas and lines
# ---------------------------------------------------------------------------
_WATER_RIVER = frozenset({"river", "canal", "stream", "rapids", "ditch", "drain", "riverbank"})
_WATER_POND = frozenset({"pond", "reservoir", "basin", "fishpond", "wastewater", "reflecting_pool", "lock",
                         "moat", "stream_pool"})
_TEA_RE = re.compile(r"\btea")


def _is_tea(tags: Mapping[str, str]) -> bool:
    return any(_TEA_RE.search(_norm(tags.get(k))) for k in ("crop", "trees", "produce"))


def area_kind(tags: Mapping[str, str]) -> AreaKind:
    """Classify a polygon. First match wins, in this order:

    1. Water: ``waterway=riverbank`` and ``natural=water`` with
       ``water=river|canal|stream`` WATER_RIVER; ``water=pond|reservoir|basin|...``,
       ``landuse=reservoir|basin|aquaculture|salt_pond`` WATER_POND; other
       ``natural=water`` WATER_LAKE.
    2. ``natural=glacier`` GLACIER; ``natural=wetland|mud`` WETLAND.
    3. Uses that restrict access: ``landuse=religious`` or
       ``amenity=place_of_worship`` RELIGIOUS; ``place=square``, a pedestrian
       highway *area* or ``area:highway=pedestrian`` PEDESTRIAN;
       ``aeroway=aerodrome`` AERODROME; cemetery/grave_yard CEMETERY;
       ``landuse=military`` (or any ``military=*``) MILITARY;
       ``leisure=pitch|stadium|sports_centre|track`` PITCH;
       ``leisure=park|garden|playground``, ``landuse=recreation_ground`` PARK.
    4. Vegetation: ``landuse=forest``/``natural=wood`` FOREST; farmland, orchard
       or plant nursery growing tea (``crop``/``trees``/``produce``) TEA_GARDEN;
       ``landuse=farmland|farmyard|allotments|greenhouse_horticulture`` FARMLAND;
       ``landuse=orchard|plant_nursery|vineyard`` ORCHARD;
       ``natural=grassland|fell`` GRASSLAND; ``landuse=meadow|grass|village_green``,
       ``natural=meadow``, ``leisure=common`` MEADOW; ``natural=scrub|heath`` SCRUB.
    5. Built-up: residential RESIDENTIAL, commercial/retail COMMERCIAL,
       industrial/quarry/landfill INDUSTRIAL (``brownfield``, ``construction``
       and ``greenfield`` are NONE).
    6. Bare ground: ``natural=sand|shingle|beach`` SAND_SHINGLE,
       ``bare_rock|rock`` BARE_ROCK, ``scree|moraine`` SCREE.
    7. ``boundary=protected_area|national_park``, ``leisure=nature_reserve``
       PROTECTED. This comes last because such polygons are huge and often
       also carry a land-cover tag.
    """
    g = lambda k: _tv(tags, k)  # noqa: E731
    natural, landuse, leisure, amenity = g("natural"), g("landuse"), g("leisure"), g("amenity")
    water = g("water")

    if g("waterway") == "riverbank":
        return AreaKind.WATER_RIVER
    if natural == "water":
        if water in _WATER_RIVER:
            return AreaKind.WATER_RIVER
        if water in _WATER_POND:
            return AreaKind.WATER_POND
        return AreaKind.WATER_LAKE
    if landuse in ("reservoir", "basin", "aquaculture", "salt_pond"):
        return AreaKind.WATER_POND
    if natural == "glacier":
        return AreaKind.GLACIER
    if natural in ("wetland", "mud"):
        return AreaKind.WETLAND

    if landuse == "religious" or amenity == "place_of_worship":
        return AreaKind.RELIGIOUS
    is_area = g("area") == "yes" or g("type") == "multipolygon"
    if g("place") == "square" or (g("highway") == "pedestrian" and is_area) or g("area:highway") == "pedestrian":
        return AreaKind.PEDESTRIAN
    if g("aeroway") == "aerodrome":
        return AreaKind.AERODROME
    if landuse == "cemetery" or amenity == "grave_yard":
        return AreaKind.CEMETERY
    if landuse == "military" or g("military") not in ("", "no"):
        return AreaKind.MILITARY
    if leisure in ("pitch", "stadium", "sports_centre", "track"):
        return AreaKind.PITCH
    if leisure in ("park", "garden", "playground") or landuse == "recreation_ground":
        return AreaKind.PARK

    if landuse == "forest" or natural == "wood":
        return AreaKind.FOREST
    if landuse in ("farmland", "orchard", "plant_nursery") and _is_tea(tags):
        return AreaKind.TEA_GARDEN
    if landuse in ("farmland", "farmyard", "allotments", "greenhouse_horticulture"):
        return AreaKind.FARMLAND
    if landuse in ("orchard", "plant_nursery", "vineyard"):
        return AreaKind.ORCHARD
    if natural in ("grassland", "fell"):
        return AreaKind.GRASSLAND
    if landuse in ("meadow", "grass", "village_green") or natural == "meadow" or leisure == "common":
        return AreaKind.MEADOW
    if natural in ("scrub", "heath"):
        return AreaKind.SCRUB

    if landuse == "residential":
        return AreaKind.RESIDENTIAL
    if landuse in ("commercial", "retail"):
        return AreaKind.COMMERCIAL
    if landuse in ("industrial", "quarry", "landfill"):
        return AreaKind.INDUSTRIAL

    if natural in ("sand", "shingle", "beach"):
        return AreaKind.SAND_SHINGLE
    if natural in ("bare_rock", "rock"):
        return AreaKind.BARE_ROCK
    if natural in ("scree", "moraine"):
        return AreaKind.SCREE

    if g("boundary") in ("protected_area", "national_park") or leisure == "nature_reserve":
        return AreaKind.PROTECTED
    return AreaKind.NONE


_MANI_RE = re.compile(r"\bmani\b|माने")


def line_kind(tags: Mapping[str, str]) -> LineKind:
    """Classify a non-road linear feature.

    waterway river/stream/canal/ditch/drain/waterfall; railway
    rail/narrow_gauge/light_rail/monorail/funicular (``abandoned``,
    ``disused``, ``construction`` and ``proposed`` are NONE); aerialway
    cable_car/gondola/mixed_lift CABLE_CAR, chair_lift CHAIR_LIFT, zip_line
    ZIP_LINE; aeroway runway/taxiway; city walls; mani walls (``man_made`` or
    ``historic=mani_wall``, or a wall whose name contains "mani").
    """
    g = lambda k: _tv(tags, k)  # noqa: E731
    waterway = g("waterway")
    if waterway == "river":
        return LineKind.RIVER
    if waterway in ("stream", "brook", "tidal_channel"):
        return LineKind.STREAM
    if waterway == "canal":
        return LineKind.CANAL
    if waterway in ("ditch", "drain"):
        return LineKind.DITCH
    if waterway == "waterfall":
        return LineKind.WATERFALL
    if g("railway") in ("rail", "narrow_gauge", "light_rail", "monorail", "funicular"):
        return LineKind.RAILWAY
    aerialway = g("aerialway")
    if aerialway in ("cable_car", "gondola", "mixed_lift"):
        return LineKind.CABLE_CAR
    if aerialway == "chair_lift":
        return LineKind.CHAIR_LIFT
    if aerialway == "zip_line":
        return LineKind.ZIP_LINE
    aeroway = g("aeroway")
    if aeroway == "runway":
        return LineKind.RUNWAY
    if aeroway in ("taxiway", "taxilane"):
        return LineKind.TAXIWAY
    historic, barrier, man_made = g("historic"), g("barrier"), g("man_made")
    if barrier == "city_wall" or historic in ("city_wall", "citywalls"):
        return LineKind.CITY_WALL
    if man_made == "mani_wall" or historic == "mani_wall":
        return LineKind.MANI_WALL
    if (historic == "wall" or barrier == "wall") and _MANI_RE.search(_names_text(tags)):
        return LineKind.MANI_WALL
    return LineKind.NONE
