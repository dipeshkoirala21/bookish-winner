"""Region search index (``.ghsi``): entry selection, key generation, binary I/O and lookup.

The binary layout is docs/DATA_FORMATS.md section 3. This module is the
reference implementation; the C# port in ``Ghumante.Core`` mirrors
``SearchIndex.search`` exactly, and golden tests compare ranked results.

Entry selection (``build_entries``)
===================================

* **Places** with ``kind != PlaceKind.NONE`` and a display name; stored kind
  ``PlaceKind + 1000``.
* **Admin areas** of level PROVINCE, DISTRICT and LOCAL_LEVEL (not wards),
  placed at ``polygon.point_on_surface()``; stored kind ``level + 1000``.
* **POIs** with a display name whose kind is not in ``EXCLUDED_POI_KINDS``
  (ADR-010: no business names; lodging is left out in M0 too).

With ``bounds_game`` (the region's leaf-tile coverage, game metres), place and
POI candidates outside it are dropped first: the extract's buffer zone has no
detail tiles to travel to. Admin areas are kept.

``aliases`` (raw OSM id -> names; the curated ``aliases`` of
``config/landmarks.yaml``) are appended to the entry's ``alt`` names, so they
become search keys ("Kathmandu Airport", "TIA").

"Display name" means at least one of ``default``/``en``/``ne`` is non-empty.
A candidate is also dropped when none of those three folds to a non-empty
string (for example a name only in Tibetan script).

Positions come from ``projection.lonlat_to_game``.

**Importance** (0..255, integer) = clamp(kind weight + bonuses, 0, 255):

* kind weight: ``PLACE_WEIGHTS`` / ``POI_WEIGHTS`` (city 250, province 240,
  district 220, town 200, ...; unlisted kinds 50). Peaks: ``70 + round(150 *
  clamp((ele - 1000) / 7849, 0, 1))``, so a 1 000 m hill gets 70 and Everest
  (8 849 m) 220; a peak without ``ele`` gets 70.
* population (places): ``clamp(round(10 * log10(population)) - 30, 0, 40)``,
  i.e. +0 at 1 000 people, +10 at 10 000, +30 at a million.
* landmark: +40 (the OSM id is in ``landmark_ids``, or the POI has
  ``PoiFlags.LANDMARK``).
* Wikidata: +20 when the feature's ``tags`` has ``wikidata`` (only
  ``PoiFeature`` carries tags; ``PlaceFeature`` and ``AdminArea`` do not).
* A BUS_STATION that is not a landmark and whose folded name says where the
  bus goes ("Bus to Nagarkot", "bus for Kathmandu"; ``is_bus_to_stop``) gets
  at most ``DEFAULT_WEIGHT`` and no transport-hub flag.

The ``importance`` floats already on the features are not used.

**Flags:** bit0 discoverable (POI kinds 100-299: religious, heritage,
nature; or ``PoiFlags.DISCOVERABLE``), bit1 landmark, bit2 transport hub
(AIRPORT, BUS_STATION, CABLE_CAR_STATION, RAILWAY_STATION).

**Dedupe.** The *folded primary name* of an entry is the first non-empty
``fold`` of ``default``, ``en``, ``ne``; its *folded variants* are all the
distinct non-empty folds of the three. Candidates are visited in order of
(importance descending, osm_ref, kind, folded primary name). A candidate is
dropped when an already kept candidate shares any folded variant, has the
same ``kind_group`` and lies within 300 m (game metres), or, when either one
is an admin area, the admin polygon contains the other's point. So
"DUBAR SQUARE PATAN", "पाटन दरवार क्षेत्र" and "Patan Durbar Square" (all with
``en`` "Patan Durbar Square", 40 m apart) are one entry. The dropped
candidate's names that fold differently are appended to the kept entry's
``alt``, so they stay searchable. Kind groups:
settlement places (city, town, village, hamlet, isolated dwelling, suburb,
neighbourhood, quarter, locality, farm) share one group; ``place=square``
joins the religious/heritage POIs; each other place or admin kind is its own
group (a place=district node merges with the district relation); POIs group
by ``kind // 100`` (religious and heritage, nature, transport, tourism,
activities, civic).

**District and province** come from point-in-polygon tests (shapely STRtree,
boundary inclusive) of the entry's lon/lat against the DISTRICT and PROVINCE
admin polygons; when several match, the smallest OSM relation id wins. A
district entry gets no district; province and country entries get neither.

Keys (``keys_for``)
===================

For every name variant (``default``, ``en``, ``ne`` and each ``alt``):

* ``fold(variant)``;
* ``fold(romanize(variant))`` for Devanagari variants. ``fold`` already
  romanises Devanagari, so this equals the previous key; it is kept so the
  code reads like the spec;
* every word suffix of the folded variant, ``" ".join(words[i:])`` for
  ``i >= 1``, unless its first word is shorter than 3 characters or is in
  the folded stop-list ``SUFFIX_STOP_WORDS`` (generic words such as temple,
  mandir, lake, tal, the; see the constant). "Patan Durbar Square" gets
  "durbar square" and "square"; "Pashupatinath Temple" gets no "temple".

Keys are de-duplicated per entry and sorted.

File layout and determinism
===========================

* Entries are sorted by (kind, folded primary name, full osm_ref).
* The names section interns ``(default, en, ne)`` triples (NFC): first every
  district and province name in order of first use while walking the sorted
  entries (district before province), so the u16 indices stay small; then
  every entry name in entry order. Records are shared by identical triples.
* Keys are sorted by (key bytes, entry index).
* Coordinates are rounded to the nearest decimetre / 1e-7 degree;
  ``osm_ref`` is ``(osm_id << 2) | type`` (node 0, way 1, relation 2),
  truncated to 32 bits in the file.

Lookup (``SearchIndex.search``), exact and mirrored in C#
=========================================================

All arithmetic is integer; ``m`` is a match score in thousandths.

1. ``q = fold(query)``; if the query contains Devanagari, ``fold(romanize(query))``
   is also tried (it is the same string by construction). An empty ``q`` or a
   ``limit <= 0`` gives ``[]``. For each distinct folded query ``q`` with
   ``L = len(q)``:
2. **Exact and prefix.** Binary-search the first key ``>= q`` (ordinal byte
   order) and walk forward while the key starts with ``q``. With
   ``extra = len(key) - L``: ``m = 1000`` when ``extra == 0``, else
   ``m = max(100, 900 - 10 * extra)``.
3. **Token match.** The distinct words of ``q`` that are *significant*
   (``significant``: at least 3 characters and not a folded stop word) are
   collected. With two or more, every entry that has, for **each** of them, a
   key starting with it gets ``m = TOKEN_MATCH_SCORE`` (600). Keys start at
   every significant word of a name, so this means every significant query
   word is a prefix of some word of the entry: "tribhuvan airport" finds
   "Tribhuvan International Airport".
4. **Fuzzy**, only when ``L > 3``: ``max_d = max(1, L // 4)``. Every key whose
   first character equals ``q[0]`` (a contiguous range of the sorted keys) and
   whose length differs from ``L`` by at most ``max_d`` is compared with the
   optimal-string-alignment distance (restricted Damerau-Levenshtein: insert,
   delete, substitute, swap two adjacent characters; all cost 1; no substring
   edited twice). A distance ``d <= max_d`` gives ``m = 750 - 100 * d``.
5. Each entry keeps the maximum ``m`` over all its matching keys and queries.
6. **Rank bonus** (``rank_bonus``): landmarks (flag bit1) +150, other
   heritage POIs (kinds 120..129) +50; ``m' = min(1000, m + bonus)``.
   "Boudha" ranks Boudhanath Stupa (prefix match, landmark) above the Baudha
   neighbourhood (exact match).
7. ``score = 1785 * m' + 3000 * importance``, which is
   ``2 550 000 * (m' / 1000 * 0.7 + importance / 255 * 0.3)``. Results are
   sorted by (score descending, ``kind_priority(kind)`` ascending, entry
   index ascending) and the first ``limit`` are returned as
   ``(score / 2 550 000, entry)``. ``kind_priority`` puts places before POIs:
   ``kind - 1000`` for places, ``kind + 1000`` for POIs. Within one kind the
   entry index already orders by folded name, then osm_ref.
"""

from __future__ import annotations

import math
import re
import unicodedata
from bisect import bisect_left
from dataclasses import dataclass, field
from pathlib import Path
from typing import Iterable, Sequence

import numpy as np
import shapely
from shapely import STRtree

from . import projection
from .binio import Reader, Writer
from .model import AdminArea, NameRec, PlaceFeature, PlaceKind, PoiFeature, PoiFlags, PoiKind
from .translit import fold, has_devanagari, romanize

MAGIC = b"GHSI"
VERSION = 1
HEADER_SIZE = 32
ENTRY_SIZE = 32
PLACE_KIND_OFFSET = 1000

FLAG_DISCOVERABLE = 1 << 0
FLAG_LANDMARK = 1 << 1
FLAG_TRANSPORT_HUB = 1 << 2

DEDUPE_RADIUS_M = 300.0
SCORE_SCALE = 2_550_000  # 1785 * 1000 + 3000 * 255

EXCLUDED_POI_KINDS = frozenset({
    PoiKind.NONE, PoiKind.SHOP, PoiKind.RESTAURANT, PoiKind.CAFE, PoiKind.BANK, PoiKind.SCHOOL,
    PoiKind.HOSPITAL, PoiKind.GOVERNMENT, PoiKind.PARKING, PoiKind.FUEL, PoiKind.TAXI_STAND,
    PoiKind.HOTEL, PoiKind.GUEST_HOUSE, PoiKind.HOSTEL,
})
TRANSPORT_HUB_KINDS = frozenset({PoiKind.AIRPORT, PoiKind.BUS_STATION, PoiKind.CABLE_CAR_STATION,
                                 PoiKind.RAILWAY_STATION})
ADMIN_ENTRY_LEVELS = (PlaceKind.PROVINCE, PlaceKind.DISTRICT, PlaceKind.LOCAL_LEVEL)
_ABOVE_PROVINCE = frozenset({PlaceKind.COUNTRY, PlaceKind.PROVINCE})
_ABOVE_DISTRICT = _ABOVE_PROVINCE | {PlaceKind.DISTRICT}
_SETTLEMENT_KINDS = frozenset({
    PlaceKind.CITY, PlaceKind.TOWN, PlaceKind.VILLAGE, PlaceKind.HAMLET, PlaceKind.ISOLATED_DWELLING,
    PlaceKind.SUBURB, PlaceKind.NEIGHBOURHOOD, PlaceKind.QUARTER, PlaceKind.LOCALITY, PlaceKind.FARM,
})

PLACE_WEIGHTS: dict[PlaceKind, int] = {
    PlaceKind.COUNTRY: 255, PlaceKind.CITY: 250, PlaceKind.PROVINCE: 240, PlaceKind.DISTRICT: 220,
    PlaceKind.TOWN: 200, PlaceKind.LOCAL_LEVEL: 180, PlaceKind.SUBURB: 150, PlaceKind.SQUARE: 140,
    PlaceKind.QUARTER: 130, PlaceKind.NEIGHBOURHOOD: 120, PlaceKind.VILLAGE: 110, PlaceKind.ISLAND: 100,
    PlaceKind.LOCALITY: 70, PlaceKind.HAMLET: 60, PlaceKind.WARD: 40, PlaceKind.ISOLATED_DWELLING: 30,
    PlaceKind.FARM: 20,
}
POI_WEIGHTS: dict[PoiKind, int] = {
    # religious
    PoiKind.TEMPLE_HINDU: 110, PoiKind.STUPA: 150, PoiKind.GOMPA: 120, PoiKind.SHRINE: 70,
    PoiKind.MOSQUE: 100, PoiKind.CHURCH: 90, PoiKind.PLACE_OF_WORSHIP: 80, PoiKind.CHORTEN: 80,
    PoiKind.MANI_WALL: 60,
    # heritage
    PoiKind.HERITAGE_SQUARE: 190, PoiKind.PALACE: 170, PoiKind.MONUMENT: 110, PoiKind.RUINS: 100,
    PoiKind.STONE_TAP: 90, PoiKind.CITY_GATE: 90, PoiKind.MUSEUM: 130,
    # nature (PEAK is computed from elevation)
    PoiKind.VIEWPOINT: 110, PoiKind.WATERFALL: 130, PoiKind.CAVE: 120, PoiKind.LAKE: 140,
    PoiKind.HOT_SPRING: 130, PoiKind.GLACIER: 130, PoiKind.PASS: 150, PoiKind.SPRING: 60,
    PoiKind.RIVER: 120, PoiKind.PARK: 100, PoiKind.PROTECTED_AREA: 180, PoiKind.NOTABLE_TREE: 70,
    PoiKind.RIDGE: 90,
    # transport
    PoiKind.AIRPORT: 200, PoiKind.HELIPAD: 60, PoiKind.BUS_STATION: 140, PoiKind.CABLE_CAR_STATION: 140,
    PoiKind.RAILWAY_STATION: 140, PoiKind.BRIDGE: 70,
    # tourism
    PoiKind.CAMP_SITE: 90, PoiKind.TEAHOUSE: 100, PoiKind.ATTRACTION: 120, PoiKind.MARKETPLACE: 110,
    PoiKind.INFORMATION: 40, PoiKind.PICNIC_SITE: 80, PoiKind.THEME_PARK: 120, PoiKind.ZOO: 130,
    # activities
    PoiKind.BUNGEE: 140, PoiKind.PARAGLIDING: 130, PoiKind.ZIPLINE: 130, PoiKind.RAFTING: 120,
    PoiKind.BOATING: 110, PoiKind.SAFARI: 130,
}
DEFAULT_WEIGHT = 50
PEAK_BASE, PEAK_RANGE, PEAK_ELE_MIN, PEAK_ELE_MAX = 70, 150, 1000.0, 8849.0
LANDMARK_BONUS = 40
WIKIDATA_BONUS = 20

# Ranking bonus, in thousandths of the match score (``rank``): famous landmarks and
# heritage sites beat a same-named neighbourhood or bus stop.
RANK_BONUS_LANDMARK = 150
RANK_BONUS_HERITAGE = 50
HERITAGE_KIND_MIN, HERITAGE_KIND_MAX = 120, 129  # HERITAGE_SQUARE .. (heritage POI kinds)
# Every significant query word is a prefix of some word of the entry (``match_scores``).
TOKEN_MATCH_SCORE = 600

# A bus stop named after its destination ("Bus to Nagarkot") is not a hub of that place.
_BUS_TO_RE = re.compile(r"^(bus|buses|busses|micro|microbus|micro bus|jeep|tempo)( stop| station| park)? (to|por) ")

# Generic words that may not start a suffix key (compared after folding).
SUFFIX_STOP_WORDS: tuple[str, ...] = (
    "the", "and", "temple", "mandir", "mandira", "lake", "tal", "taal", "pokhari", "pond", "kunda", "kund",
    "river", "khola", "nadi", "road", "marg", "sadak", "street", "chowk", "tole", "tol", "bazar", "bazaar",
    "school", "college", "hospital", "ward", "village", "city", "metropolitan", "sub", "municipality",
    "rural", "palika", "nagarpalika", "gaunpalika", "mahanagarpalika", "upamahanagarpalika", "district",
    "province", "area", "park",
)
_STOP_FOLDED = frozenset(fold(w) for w in SUFFIX_STOP_WORDS)


# ---------------------------------------------------------------------------
# Entries
# ---------------------------------------------------------------------------
@dataclass(slots=True)
class SearchEntry:
    name: NameRec
    kind: int  # PoiKind value, or PlaceKind value + 1000
    importance: int  # 0..255
    flags: int  # FLAG_*
    x: float  # game metres
    z: float
    lon: float
    lat: float
    osm_ref: int  # (osm_id << 2) | type; truncated to 32 bits after a read
    district: NameRec | None = None
    province: NameRec | None = None

    @property
    def display_name(self) -> str:
        return self.name.en or self.name.default or self.name.ne


def osm_ref(osm_type: str, osm_id: int) -> int:
    return (int(osm_id) << 2) | {"n": 0, "w": 1, "r": 2}[osm_type]


def folded_primary(name: NameRec) -> str:
    """First non-empty fold of ``default``, ``en``, ``ne``: the dedupe and sort name."""
    for v in (name.default, name.en, name.ne):
        f = fold(v)
        if f:
            return f
    return ""


def kind_group(kind: int) -> int:
    """Dedupe group: entries of one group with the same name close together are duplicates."""
    if kind >= PLACE_KIND_OFFSET:
        pk = kind - PLACE_KIND_OFFSET
        if pk in _SETTLEMENT_KINDS:
            return 10
        if pk == PlaceKind.SQUARE:
            return PoiKind.HERITAGE_SQUARE // 100
        return kind
    return kind // 100


def kind_priority(kind: int) -> int:
    """Tie-break rank (lower first): places before POIs, then ascending kind value."""
    return kind - PLACE_KIND_OFFSET if kind >= PLACE_KIND_OFFSET else kind + PLACE_KIND_OFFSET


def _clamp_u8(v: float) -> int:
    return int(min(255, max(0, v)))


def population_bonus(population: int | None) -> int:
    if not population or population <= 0:
        return 0
    return int(min(40, max(0, round(10 * math.log10(population)) - 30)))


def peak_weight(ele_m: float | None) -> int:
    if ele_m is None or not math.isfinite(ele_m):
        return PEAK_BASE
    t = min(1.0, max(0.0, (ele_m - PEAK_ELE_MIN) / (PEAK_ELE_MAX - PEAK_ELE_MIN)))
    return PEAK_BASE + int(round(PEAK_RANGE * t))


def place_importance(kind: PlaceKind, population: int | None = None, *, landmark: bool = False,
                     wikidata: bool = False) -> int:
    w = PLACE_WEIGHTS.get(PlaceKind(kind), DEFAULT_WEIGHT) + population_bonus(population)
    return _clamp_u8(w + (LANDMARK_BONUS if landmark else 0) + (WIKIDATA_BONUS if wikidata else 0))


def poi_importance(kind: PoiKind, ele_m: float | None = None, *, landmark: bool = False,
                   wikidata: bool = False) -> int:
    w = peak_weight(ele_m) if kind == PoiKind.PEAK else POI_WEIGHTS.get(PoiKind(kind), DEFAULT_WEIGHT)
    return _clamp_u8(w + (LANDMARK_BONUS if landmark else 0) + (WIKIDATA_BONUS if wikidata else 0))


def poi_flags(kind: PoiKind, landmark: bool = False, poi_flags_in: int = 0) -> int:
    f = 0
    if 100 <= int(kind) < 300 or poi_flags_in & PoiFlags.DISCOVERABLE:
        f |= FLAG_DISCOVERABLE
    if landmark:
        f |= FLAG_LANDMARK
    if kind in TRANSPORT_HUB_KINDS:
        f |= FLAG_TRANSPORT_HUB
    return f


def is_bus_to_stop(name: NameRec | None) -> bool:
    """True for bus stops named after where the bus goes ("Bus to Nagarkot", "bus for Kathmandu")."""
    if name is None:
        return False
    return any(_BUS_TO_RE.match(f) for f in folded_variants(name))


def rank_bonus(entry: SearchEntry) -> int:
    """Match-score bonus (thousandths) used by ``rank``: landmarks, then heritage POIs."""
    if entry.flags & FLAG_LANDMARK:
        return RANK_BONUS_LANDMARK
    if HERITAGE_KIND_MIN <= entry.kind <= HERITAGE_KIND_MAX:
        return RANK_BONUS_HERITAGE
    return 0


def _has_wikidata(feature: object) -> bool:
    tags = getattr(feature, "tags", None)
    return bool(tags and tags.get("wikidata"))


def significant(word: str) -> bool:
    """A folded word that may start a suffix key and counts in token matching:
    at least 3 characters and not in ``SUFFIX_STOP_WORDS``."""
    return len(word) >= 3 and word not in _STOP_FOLDED


def keys_for(entry: SearchEntry) -> list[str]:
    """Sorted unique folded keys of an entry (see the module docstring)."""
    n = entry.name
    keys: set[str] = set()
    for v in (n.default, n.en, n.ne, *n.alt):
        if not v:
            continue
        folded = [fold(v)]
        if has_devanagari(v):
            folded.append(fold(romanize(v)))
        for f in folded:
            if not f:
                continue
            keys.add(f)
            words = f.split(" ")
            for i in range(1, len(words)):
                if significant(words[i]):
                    keys.add(" ".join(words[i:]))
    return sorted(keys)


@dataclass(slots=True)
class _Cand:
    entry: SearchEntry
    folded: str
    group: int
    polygon: object = None  # admin areas only (lon/lat)
    variants: tuple[str, ...] = ()  # distinct non-empty folds of default, en, ne


def folded_variants(name: NameRec) -> tuple[str, ...]:
    """Distinct non-empty folds of ``default``, ``en``, ``ne`` (in that order)."""
    return tuple(dict.fromkeys(f for f in (fold(name.default), fold(name.en), fold(name.ne)) if f))


def _merge_alt(kept: SearchEntry, dropped: SearchEntry) -> None:
    """Keep the dropped duplicate's names searchable: they become alternates of the kept entry."""
    have = {fold(v) for v in (kept.name.default, kept.name.en, kept.name.ne, *kept.name.alt) if v}
    extra = []
    for v in (dropped.name.default, dropped.name.en, dropped.name.ne, *dropped.name.alt):
        f = fold(v) if v else ""
        if f and f not in have:
            have.add(f)
            extra.append(v)
    if extra:
        n = kept.name
        kept.name = NameRec(n.default, n.en, n.ne, tuple(n.alt) + tuple(extra))


def _dedupe(cands: list[_Cand]) -> list[_Cand]:
    order = sorted(cands, key=lambda c: (-c.entry.importance, c.entry.osm_ref, c.entry.kind, c.folded))
    kept: dict[tuple[str, int], list[_Cand]] = {}
    out: list[_Cand] = []
    r2 = DEDUPE_RADIUS_M * DEDUPE_RADIUS_M
    for c in order:
        e = c.entry
        dup_of: _Cand | None = None
        seen: set[int] = set()
        for v in c.variants:
            for k in kept.get((v, c.group), ()):
                if id(k) in seen:
                    continue
                seen.add(id(k))
                ke = k.entry
                if ((ke.x - e.x) ** 2 + (ke.z - e.z) ** 2 <= r2
                        or (k.polygon is not None and shapely.intersects_xy(k.polygon, e.lon, e.lat))
                        or (c.polygon is not None and shapely.intersects_xy(c.polygon, ke.lon, ke.lat))):
                    dup_of = k
                    break
            if dup_of is not None:
                break
        if dup_of is not None:
            _merge_alt(dup_of.entry, e)
            continue
        for v in c.variants:
            kept.setdefault((v, c.group), []).append(c)
        out.append(c)
    return out


def _valid(geom: object) -> object:
    return geom if shapely.is_valid(geom) else shapely.make_valid(geom)


def _assign_admin(entries: list[SearchEntry], admin: list[AdminArea]) -> None:
    if not entries:
        return
    pts = shapely.points(np.array([e.lon for e in entries]), np.array([e.lat for e in entries]))
    for level in (PlaceKind.DISTRICT, PlaceKind.PROVINCE):
        areas = [a for a in admin if a.level == level and a.polygon is not None and not a.polygon.is_empty]
        if not areas:
            continue
        tree = STRtree([_valid(a.polygon) for a in areas])
        src, idx = tree.query(pts, predicate="intersects")
        best: dict[int, int] = {}
        for s, t in zip(src.tolist(), idx.tolist()):
            if s not in best or t < best[s]:
                best[s] = t
        for s, t in best.items():
            e = entries[s]
            pk = e.kind - PLACE_KIND_OFFSET
            if level == PlaceKind.DISTRICT and pk not in _ABOVE_DISTRICT:
                e.district = areas[t].name
            elif level == PlaceKind.PROVINCE and pk not in _ABOVE_PROVINCE:
                e.province = areas[t].name


def _with_aliases(name: NameRec, aliases: Sequence[str]) -> NameRec:
    extra = tuple(a for a in aliases if a and a not in name.alt)
    return NameRec(name.default, name.en, name.ne, tuple(name.alt) + extra) if extra else name


def build_entries(places: Sequence[PlaceFeature], pois: Sequence[PoiFeature],
                  admin: Sequence[AdminArea] | None = None,
                  landmark_ids: dict[int, str] | None = None, *,
                  aliases: dict[int, Sequence[str]] | None = None,
                  bounds_game: tuple[float, float, float, float] | None = None) -> list[SearchEntry]:
    """Select, score, dedupe and annotate search entries (see the module docstring).

    ``landmark_ids`` maps raw OSM ids (``feature.osm_id``, any element type)
    to landmark ids; ``aliases`` maps raw OSM ids to extra search names (the
    curated ``aliases`` of ``config/landmarks.yaml``), indexed like ``alt``.
    With ``bounds_game`` ``(x0, z0, x1, z1)`` (the region's leaf-tile coverage),
    place and POI candidates outside it are dropped before dedupe: the extract's
    buffer zone has no detail tiles. Admin areas are kept. The result is in file
    order (``sort_entries``).
    """
    landmark_ids = landmark_ids or {}
    aliases = aliases or {}
    admin_sorted = sorted(admin or [], key=lambda a: (a.osm_id, int(a.level)))
    raw: list[tuple[SearchEntry, object]] = []  # entry without x/z, admin polygon

    for p in places:
        if p.kind == PlaceKind.NONE or p.name is None or p.name.is_empty():
            continue
        lm = p.osm_id in landmark_ids
        imp = place_importance(p.kind, p.population, landmark=lm, wikidata=_has_wikidata(p))
        e = SearchEntry(_with_aliases(p.name, aliases.get(p.osm_id, ())), int(p.kind) + PLACE_KIND_OFFSET, imp,
                        FLAG_LANDMARK if lm else 0, 0.0, 0.0, float(p.lon), float(p.lat),
                        osm_ref(p.osm_type, p.osm_id))
        raw.append((e, None))

    for a in admin_sorted:
        if a.level not in ADMIN_ENTRY_LEVELS or a.name is None or a.name.is_empty():
            continue
        if a.polygon is None or a.polygon.is_empty:
            continue
        poly = _valid(a.polygon)
        shapely.prepare(poly)
        pt = poly.point_on_surface()
        if pt.is_empty:
            continue
        lm = a.osm_id in landmark_ids
        imp = place_importance(a.level, None, landmark=lm)
        e = SearchEntry(_with_aliases(a.name, aliases.get(a.osm_id, ())), int(a.level) + PLACE_KIND_OFFSET, imp,
                        FLAG_LANDMARK if lm else 0, 0.0, 0.0, float(pt.x), float(pt.y), osm_ref("r", a.osm_id))
        raw.append((e, poly))

    for q in pois:
        if q.kind in EXCLUDED_POI_KINDS or q.name is None or q.name.is_empty():
            continue
        lm = q.osm_id in landmark_ids or bool(q.flags & PoiFlags.LANDMARK)
        imp = poi_importance(q.kind, q.ele_m, landmark=lm, wikidata=_has_wikidata(q))
        fl = poi_flags(q.kind, lm, int(q.flags))
        if q.kind == PoiKind.BUS_STATION and not lm and is_bus_to_stop(q.name):
            imp, fl = min(imp, DEFAULT_WEIGHT), fl & ~FLAG_TRANSPORT_HUB
        e = SearchEntry(_with_aliases(q.name, aliases.get(q.osm_id, ())), int(q.kind), imp, fl, 0.0, 0.0,
                        float(q.lon), float(q.lat), osm_ref(q.osm_type, q.osm_id))
        raw.append((e, None))

    if not raw:
        return []
    xs, zs = projection.lonlat_to_game(np.array([e.lon for e, _ in raw]), np.array([e.lat for e, _ in raw]))
    cands: list[_Cand] = []
    for (e, poly), x, z in zip(raw, np.asarray(xs).tolist(), np.asarray(zs).tolist()):
        e.x, e.z = float(x), float(z)
        if bounds_game is not None and poly is None and not (
                bounds_game[0] <= e.x <= bounds_game[2] and bounds_game[1] <= e.z <= bounds_game[3]):
            continue
        folded = folded_primary(e.name)
        if not folded:
            continue
        cands.append(_Cand(e, folded, kind_group(e.kind), poly, folded_variants(e.name)))

    entries = [c.entry for c in _dedupe(cands)]
    _assign_admin(entries, admin_sorted)
    return sort_entries(entries)


def sort_entries(entries: Iterable[SearchEntry]) -> list[SearchEntry]:
    """File order: (kind, folded primary name, osm_ref)."""
    return sorted(entries, key=lambda e: (e.kind, folded_primary(e.name), e.osm_ref))


def search_ids(entries: Iterable[SearchEntry]) -> dict[tuple[int, int], int]:
    """``(full osm_ref, kind)`` -> ``search_id`` (file entry index + 1) for the tile
    ``POIS`` chunk. ``kind`` is the entry kind (``PlaceKind + 1000`` for places
    and admin areas), so one OSM object that yields both a POI and a place entry
    maps each tile record to its own entry."""
    out: dict[tuple[int, int], int] = {}
    for i, e in enumerate(sort_entries(entries)):
        out.setdefault((e.osm_ref, int(e.kind)), i + 1)
    return out


def search_importances(entries: Iterable[SearchEntry]) -> dict[tuple[int, int], int]:
    """``(full osm_ref, kind)`` -> entry importance (0..255), keyed like ``search_ids``."""
    out: dict[tuple[int, int], int] = {}
    for e in entries:
        out.setdefault((e.osm_ref, int(e.kind)), int(e.importance))
    return out


# ---------------------------------------------------------------------------
# Binary format
# ---------------------------------------------------------------------------
def _name_key(n: NameRec) -> tuple[str, str, str]:
    return tuple(unicodedata.normalize("NFC", s or "") for s in (n.default, n.en, n.ne))  # type: ignore[return-value]


def _round_i32(v: float) -> int:
    r = int(round(v))
    if not -(1 << 31) <= r < (1 << 31):
        raise OverflowError(f"{v} does not fit an i32")
    return r


def encode_index(entries: Iterable[SearchEntry]) -> bytes:
    """Serialise entries to a GHSI blob (deterministic; entries are re-sorted)."""
    ordered = sort_entries(entries)

    names: list[tuple[str, str, str]] = []
    name_ids: dict[tuple[str, str, str], int] = {}

    def intern(n: NameRec) -> int:
        k = _name_key(n)
        i = name_ids.get(k)
        if i is None:
            i = name_ids[k] = len(names)
            names.append(k)
        return i

    for e in ordered:
        for n in (e.district, e.province):
            if n is not None:
                intern(n)
    if len(names) > 0xFFFF:
        raise ValueError(f"{len(names)} district/province names do not fit a u16 index")
    entry_names = [intern(e.name) for e in ordered]

    w = Writer()
    w.varint(len(names))
    for d, en, ne in names:
        w.str(d).str(en).str(ne)
    names_bytes = w.bytes()

    w = Writer()
    for e, ni in zip(ordered, entry_names):
        if not 0 <= e.kind <= 0xFFFF:
            raise ValueError(f"kind {e.kind} out of u16 range")
        w.u32(ni).u16(e.kind).u8(_clamp_u8(e.importance)).u8(e.flags & 0xFF)
        w.i32(_round_i32(e.x * 10.0)).i32(_round_i32(e.z * 10.0))
        w.i32(_round_i32(e.lon * 1e7)).i32(_round_i32(e.lat * 1e7))
        w.u32(e.osm_ref & 0xFFFFFFFF)
        w.u16(0 if e.district is None else name_ids[_name_key(e.district)] + 1)
        w.u16(0 if e.province is None else name_ids[_name_key(e.province)] + 1)
    entries_bytes = w.bytes()

    pairs = sorted((k, i) for i, e in enumerate(ordered) for k in keys_for(e))
    w = Writer()
    for k, i in pairs:
        w.str(k).varint(i)
    keys_bytes = w.bytes()

    names_off = HEADER_SIZE
    entries_off = names_off + len(names_bytes)
    keys_off = entries_off + len(entries_bytes)
    h = Writer()
    h.raw(MAGIC).u16(VERSION).u16(0).u32(len(ordered)).u32(len(pairs))
    h.u32(names_off).u32(entries_off).u32(keys_off).u32(0)
    assert len(h) == HEADER_SIZE
    return h.bytes() + names_bytes + entries_bytes + keys_bytes


def write_index(path: str | Path, entries: Iterable[SearchEntry]) -> dict:
    """Write a ``.ghsi`` file. Returns stats for the build report."""
    entries = list(entries)
    data = encode_index(entries)
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)
    r = Reader(data)
    r.pos = 8
    entry_count, key_count = r.u32(), r.u32()
    return {
        "entries": entry_count,
        "keys": key_count,
        "names": Reader(data, HEADER_SIZE).varint(),
        "bytes": len(data),
        "landmarks": sum(1 for e in entries if e.flags & FLAG_LANDMARK),
        "discoverable": sum(1 for e in entries if e.flags & FLAG_DISCOVERABLE),
        "transport_hubs": sum(1 for e in entries if e.flags & FLAG_TRANSPORT_HUB),
    }


def decode_index(data: bytes) -> "SearchIndex":
    if len(data) < HEADER_SIZE:
        raise ValueError("GHSI blob shorter than its header")
    r = Reader(data)
    if r.raw(4) != MAGIC:
        raise ValueError("not a GHSI file (bad magic)")
    version = r.u16()
    if version != VERSION:
        raise ValueError(f"unsupported GHSI version {version}")
    flags = r.u16()
    entry_count, key_count = r.u32(), r.u32()
    names_off, entries_off, keys_off = r.u32(), r.u32(), r.u32()
    r.u32()
    if not (HEADER_SIZE <= names_off <= entries_off <= keys_off <= len(data)) \
            or keys_off - entries_off != entry_count * ENTRY_SIZE:
        raise ValueError("GHSI section offsets are inconsistent")

    r = Reader(data, names_off, entries_off)
    names = [NameRec(r.str(), r.str(), r.str()) for _ in range(r.varint())]
    if r.remaining():
        raise ValueError("GHSI names section has trailing bytes")

    def ref(i: int) -> NameRec | None:
        if i == 0:
            return None
        if i > len(names):
            raise ValueError(f"name reference {i} out of range")
        return names[i - 1]

    r = Reader(data, entries_off, keys_off)
    entries: list[SearchEntry] = []
    for _ in range(entry_count):
        ni, kind, imp, fl = r.u32(), r.u16(), r.u8(), r.u8()
        x_dm, z_dm, lon_e7, lat_e7, oref = r.i32(), r.i32(), r.i32(), r.i32(), r.u32()
        di, pi = r.u16(), r.u16()
        if ni >= len(names):
            raise ValueError(f"entry name index {ni} out of range")
        entries.append(SearchEntry(names[ni], kind, imp, fl, x_dm / 10.0, z_dm / 10.0, lon_e7 / 1e7,
                                   lat_e7 / 1e7, oref, ref(di), ref(pi)))

    r = Reader(data, keys_off)
    keys: list[str] = []
    key_entries: list[int] = []
    prev: tuple[str, int] | None = None
    for _ in range(key_count):
        k, i = r.str(), r.varint()
        if i >= entry_count:
            raise ValueError(f"key entry index {i} out of range")
        if prev is not None and (k.encode("utf-8"), i) < (prev[0].encode("utf-8"), prev[1]):
            raise ValueError("GHSI keys are not sorted")
        prev = (k, i)
        keys.append(k)
        key_entries.append(i)
    if r.remaining():
        raise ValueError("GHSI has trailing bytes after the keys section")
    return SearchIndex(entries, names, keys, key_entries, flags)


def read_index(path: str | Path) -> "SearchIndex":
    return decode_index(Path(path).read_bytes())


# ---------------------------------------------------------------------------
# Lookup
# ---------------------------------------------------------------------------
def osa_distance(a: str, b: str, max_d: int) -> int:
    """Optimal-string-alignment distance, or ``max_d + 1`` once it is known to exceed ``max_d``."""
    la, lb = len(a), len(b)
    if abs(la - lb) > max_d:
        return max_d + 1
    prev2: list[int] = []
    prev = list(range(lb + 1))
    for i in range(1, la + 1):
        cur = [i] + [0] * lb
        ai = a[i - 1]
        row_min = i
        for j in range(1, lb + 1):
            cost = 0 if ai == b[j - 1] else 1
            v = min(prev[j] + 1, cur[j - 1] + 1, prev[j - 1] + cost)
            if i > 1 and j > 1 and ai == b[j - 2] and a[i - 2] == b[j - 1]:
                v = min(v, prev2[j - 2] + 1)
            cur[j] = v
            if v < row_min:
                row_min = v
        if row_min > max_d:  # row minima never decrease
            return max_d + 1
        prev2, prev = prev, cur
    return prev[lb] if prev[lb] <= max_d else max_d + 1


def match_score_prefix(extra: int) -> int:
    return 1000 if extra == 0 else max(100, 900 - 10 * extra)


@dataclass
class SearchIndex:
    entries: list[SearchEntry]
    names: list[NameRec]
    keys: list[str]  # sorted (ASCII, so code-point order equals byte order)
    key_entries: list[int]
    flags: int = 0
    _bounds: dict = field(default_factory=dict, repr=False)

    @classmethod
    def from_entries(cls, entries: Iterable[SearchEntry]) -> "SearchIndex":
        """In-memory index that behaves exactly like the file (encode + decode)."""
        return decode_index(encode_index(entries))

    def _first_char_range(self, c: str) -> tuple[int, int]:
        r = self._bounds.get(c)
        if r is None:
            r = self._bounds[c] = (bisect_left(self.keys, c), bisect_left(self.keys, chr(ord(c) + 1)))
        return r

    def match_scores(self, query: str) -> dict[int, int]:
        """Entry index -> best match score in thousandths (steps 1-4 of the lookup)."""
        best: dict[int, int] = {}

        def hit(i: int, m: int) -> None:
            e = self.key_entries[i]
            if m > best.get(e, -1):
                best[e] = m

        queries = [fold(query)]
        if has_devanagari(query):
            queries.append(fold(romanize(query)))
        keys = self.keys
        for q in dict.fromkeys(queries):
            if not q:
                continue
            n = len(q)
            i = bisect_left(keys, q)
            while i < len(keys) and keys[i].startswith(q):
                hit(i, match_score_prefix(len(keys[i]) - n))
                i += 1
            words = [w for w in dict.fromkeys(q.split(" ")) if significant(w)]
            if len(words) >= 2:
                common: set[int] | None = None
                for w in words:
                    hits: set[int] = set()
                    i = bisect_left(keys, w)
                    while i < len(keys) and keys[i].startswith(w):
                        hits.add(self.key_entries[i])
                        i += 1
                    common = hits if common is None else common & hits
                    if not common:
                        break
                for e in sorted(common or ()):
                    if TOKEN_MATCH_SCORE > best.get(e, -1):
                        best[e] = TOKEN_MATCH_SCORE
            if n > 3:
                max_d = max(1, n // 4)
                lo, hi = self._first_char_range(q[0])
                last_k, d = "", max_d + 1
                for i in range(lo, hi):
                    k = keys[i]
                    if abs(len(k) - n) > max_d:
                        continue
                    if k != last_k:  # identical keys are adjacent
                        last_k, d = k, osa_distance(q, k, max_d)
                    if d <= max_d:
                        hit(i, 750 - 100 * d)
        return best

    def rank(self, query: str, limit: int = 10) -> list[tuple[int, int]]:
        """Ranked ``(integer score, entry index)`` pairs; the form golden tests compare."""
        if limit <= 0:
            return []
        scored = [(1785 * min(1000, m + rank_bonus(self.entries[i])) + 3000 * self.entries[i].importance, i)
                  for i, m in self.match_scores(query).items()]
        scored.sort(key=lambda t: (-t[0], kind_priority(self.entries[t[1]].kind), t[1]))
        return scored[:limit]

    def search(self, query: str, limit: int = 10) -> list[tuple[float, SearchEntry]]:
        """Ranked ``(score in [0, 1], entry)`` results; see the module docstring."""
        return [(s / SCORE_SCALE, self.entries[i]) for s, i in self.rank(query, limit)]
