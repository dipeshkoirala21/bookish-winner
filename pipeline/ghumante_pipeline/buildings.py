"""Building inference: archetype, levels, height, roof and materials.

97.5% of Nepal's buildings are plain ``building=yes`` and 99.6% have no
``building:levels`` (docs/reports/TAG_COVERAGE_FINDINGS.md), so almost every
attribute the runtime needs is inferred here (ARCHITECTURE.md sections 6.2 and
7.5). Tagged values always win and keep their provenance flags
(``HEIGHT_TAGGED``, ``ROOF_TAGGED``); inferred levels set ``LEVELS_INFERRED``.

Inputs per building are a ``BuildingContext``: the settlement density bin (as
in ``surface.density_bin``: 0 rural, 1 village, 2 town, 3 urban core), the
ground elevation and the archetype zone from ``config/archetype_zones.yaml``.

**Archetype rules** (first match wins; a curated hero override from
``heritage_sites.yaml`` beats them all):

1. Religious (use RELIGIOUS, a ``religion`` tag, a temple-like ``building`` or
   ``tower:type=stupa``): ``tower:type``/``man_made`` stupa and ``building``
   stupa/chaitya STUPA; chorten CHORTEN; shrine SHRINE; mosque MOSQUE;
   church/chapel/cathedral CHURCH; monastery/gompa/gumba GOMPA. Then names
   (D1): a stupa name (stupa, chaitya, Boudha, Swayambhu) STUPA; a shikhara
   name (Krishna Mandir, Vatsala, Siddhi Lakshmi, Pratappur, Anantapur,
   Mahabouddha, "shikhara") or a tagged shikhara roof TEMPLE_SHIKHARA; a Newar
   bahal/bahi/vihar name TEMPLE_PAGODA (never a GOMPA, L6); a gompa name
   (gompa, gumba, monastery, ling) GOMPA. Then temple/mandir/pagoda/dewal
   ``building`` TEMPLE_SHIKHARA in the Terai, else TEMPLE_PAGODA. Otherwise by
   religion: muslim MOSQUE, christian CHURCH, buddhist GOMPA (TEMPLE_PAGODA in
   a Newar core, where Buddhist shrines are pagodas), hindu a temple as above.
   A religious building with no usable religion is a SHRINE.
2. Use INDUSTRIAL -> INDUSTRIAL; EDUCATION, HEALTH, PUBLIC, OFFICE ->
   INSTITUTIONAL; HUT -> HUT; GREENHOUSE -> GREENHOUSE.
3. HOTEL in the sherpa zone or at >= 2 500 m -> TEAHOUSE.
4. Zone: newar_core NEWAR, sherpa SHERPA_HIMALAYAN, trans_himalaya
   TRANS_HIMALAYAN.
5. Terai (zone terai or elevation < 300 m): density >= 2 MODERN_URBAN, else TERAI.
6. Density >= 2 -> MODERN_URBAN.
7. 300 <= elevation < 3 000 m -> HILL_VILLAGE; otherwise GENERIC.

"Terai" for rules 1 and 5 means zone ``terai`` or elevation below 300 m.

**Levels** (when not tagged), sampled from (archetype, density bin):

=================  ===============================================
MODERN_URBAN       core (3): 3-6, mode 4; town (2): 2-4, mode 3;
                   below: 1-3, mode 2
NEWAR              3-5, mode 4
HILL_VILLAGE       1-2 (55% 2)
TERAI              1-2 (60% 1)
SHERPA_HIMALAYAN   2
TRANS_HIMALAYAN    1-2 (60% 2)
TEAHOUSE           2-3 (60% 2)
INSTITUTIONAL      1-3 below town density, 2-4 at town and core
INDUSTRIAL         1 (20% 2)
GOMPA              2-3; TEMPLE_PAGODA 1-3 (the tiers live in the roof);
                   MOSQUE, CHURCH 1-2; other religious, HUT, GREENHOUSE 1
GENERIC            rural 1-2, village 1-2, town 2-3, core 3-4
=================  ===============================================

Use ROOF, GARAGE and CONSTRUCTION get 1 level, and so does any footprint under
12 m2. Use ROOF also sets ``OPEN_CANOPY``.

**Plausibility gate** (D4): a tagged height under 2.5 m on a building without
parts, a tagged level count above 20, or a tagged height per level outside
1.8-6 m sets ``TAG_SUSPECT``. Religious archetypes keep their tagged values
(the runtime reads a temple height under 3 m as the plinth, W2_DESIGN 3.1);
every other building drops the suspect tag(s) and infers them instead.
``building:part`` records (``PART``) take their host's archetype when the
host is religious (``link_parts``). Levels inferred from a tagged height are ``round((height - roof
allowance) / 3)``, at least 1. Untagged height is ``min_height + levels * 3.0 m
+ roof allowance`` (flat 0.5 m parapet, skillion 1.0, gabled/hipped 1.5,
pyramidal/cone/round 2.0, dome/onion 3.0, pagoda 4.0, shikhara 6.0, other 1.5).

**Roofs and walls** (when not tagged) are drawn from the archetype's table in
``ROOF_SHAPES``, ``ROOF_MATERIALS`` and ``WALL_MATERIALS`` below. A flat roof
never gets a sloped-roof material (tin, tiles, slate, thatch): it becomes
concrete, or mud in the trans-Himalaya.

All sampling is deterministic, from the per-building seed shared with the tile
format: ``fnv1a32(varint((osm_id << 1) | is_relation))``. Each attribute
hashes the seed with its own salt, so changing one table does not reshuffle the
others.
"""

from __future__ import annotations

import math
import re
import struct
from collections import Counter
from dataclasses import dataclass
from pathlib import Path
from typing import Sequence

import numpy as np
import shapely
import yaml
from shapely.geometry import Polygon

from .binio import encode_varint, fnv1a32
from .config import CONFIG_DIR
from .model import (
    BuildingArchetype as A,
    BuildingFeature,
    BuildingFlags,
    BuildingUse as U,
    RoofMaterial as RM,
    RoofShape as RS,
    WallMaterial as WM,
)

DEFAULT_ZONES_PATH = CONFIG_DIR / "archetype_zones.yaml"
ZONES = ("newar_core", "sherpa", "trans_himalaya", "terai", "default")
LEVEL_HEIGHT_M = 3.0
TINY_FOOTPRINT_M2 = 12.0
TERAI_MAX_ELEV_M = 300.0
TEAHOUSE_MIN_ELEV_M = 2500.0
HILL_MAX_ELEV_M = 3000.0
_M_PER_DEG = 111_320.0

# Seed salts, one per sampled attribute.
_SALT_LEVELS, _SALT_ROOF_SHAPE, _SALT_ROOF_MAT, _SALT_WALL = 1, 2, 3, 4


# ---------------------------------------------------------------------------
# Zones
# ---------------------------------------------------------------------------
@dataclass(frozen=True)
class BuildingContext:
    density_bin: int  # 0 rural, 1 village, 2 town, 3 urban core (surface.density_bin)
    elev_m: float
    zone: str  # one of ZONES


@dataclass(frozen=True)
class ArchetypeZones:
    """Zone polygons in lon/lat; later entries win where they overlap."""

    zones: tuple[tuple[str, object], ...]  # (zone id, prepared shapely geometry)

    def zone_of(self, lon, lat):
        """Zone id at a point (``str``), or an array of ids for array input."""
        scalar = np.ndim(lon) == 0 and np.ndim(lat) == 0
        lon_a, lat_a = np.broadcast_arrays(np.asarray(lon, dtype=np.float64), np.asarray(lat, dtype=np.float64))
        out = np.full(lon_a.shape, "default", dtype=f"<U{max(len(z) for z in ZONES)}")
        for zid, geom in self.zones:
            out[shapely.intersects_xy(geom, lon_a, lat_a)] = zid
        return str(out[()]) if scalar else out


def load_archetype_zones(path: Path | None = None) -> ArchetypeZones:
    """Load ``config/archetype_zones.yaml`` (or ``path``)."""
    path = Path(path) if path is not None else DEFAULT_ZONES_PATH
    data = yaml.safe_load(path.read_text(encoding="utf-8"))
    zones = []
    for entry in data.get("zones", []):
        zid = str(entry["id"])
        if zid not in ZONES or zid == "default":
            raise ValueError(f"{path}: unknown zone id {zid!r}; expected one of {ZONES[:-1]}")
        polys = []
        for ring in entry.get("polygons", []):
            poly = Polygon([(float(lon), float(lat)) for lon, lat in ring])
            if not poly.is_valid or poly.area <= 0:
                raise ValueError(f"{path}: zone {zid!r} has an invalid polygon starting at {ring[0]}")
            polys.append(poly)
        if not polys:
            raise ValueError(f"{path}: zone {zid!r} has no polygons")
        geom = shapely.union_all(polys)
        shapely.prepare(geom)
        zones.append((zid, geom))
    return ArchetypeZones(tuple(zones))


# ---------------------------------------------------------------------------
# Geometry and seeds
# ---------------------------------------------------------------------------
def building_seed(b: BuildingFeature) -> int:
    """Same as ``tile_format.building_seed(osm_ref)``."""
    return fnv1a32(encode_varint((b.osm_id << 1) | (1 if b.osm_type == "r" else 0)))


def _unit(seed: int, salt: int) -> float:
    """Deterministic uniform [0, 1) from a seed and an attribute salt."""
    return fnv1a32(struct.pack("<II", seed & 0xFFFFFFFF, salt)) / 4294967296.0


def _pick(table: tuple[tuple[object, float], ...], u: float):
    total = sum(w for _, w in table)
    acc = 0.0
    for value, w in table:
        acc += w / total
        if u < acc:
            return value
    return table[-1][0]


def footprint_area_m2(b: BuildingFeature) -> float:
    """Approximate footprint area (outer minus holes) in m2, local equirectangular."""
    def ring_area(r: np.ndarray) -> float:
        if len(r) < 3:
            return 0.0
        x, y = r[:, 0], r[:, 1]
        return 0.5 * abs(float(np.dot(x, np.roll(y, -1)) - np.dot(np.roll(x, -1), y)))
    outer = np.asarray(b.outer, dtype=np.float64)
    if len(outer) < 3:
        return 0.0
    k = math.cos(math.radians(float(outer[:, 1].mean()))) * _M_PER_DEG * _M_PER_DEG
    return max(0.0, ring_area(outer) - sum(ring_area(np.asarray(h, dtype=np.float64)) for h in b.holes)) * k


def building_centroids(buildings: Sequence[BuildingFeature]) -> tuple[np.ndarray, np.ndarray]:
    """Mean vertex of each outer ring (lon, lat); cheap and good enough for context lookups."""
    lon = np.array([float(np.mean(b.outer[:, 0])) for b in buildings], dtype=np.float64)
    lat = np.array([float(np.mean(b.outer[:, 1])) for b in buildings], dtype=np.float64)
    return lon, lat


def make_contexts(buildings: Sequence[BuildingFeature], zones: ArchetypeZones, elev_m, density_bins
                  ) -> list[BuildingContext]:
    """Contexts from per-building elevation and density-bin arrays (same order as ``buildings``)."""
    lon, lat = building_centroids(buildings)
    zone = zones.zone_of(lon, lat) if len(buildings) else np.array([], dtype=str)
    elev = np.asarray(elev_m, dtype=np.float64).reshape(-1)
    dens = np.asarray(density_bins).reshape(-1)
    if not (len(elev) == len(dens) == len(buildings)):
        raise ValueError("elev_m and density_bins must have one value per building")
    return [BuildingContext(int(d), float(e), str(z)) for d, e, z in zip(dens, elev, zone)]


# ---------------------------------------------------------------------------
# Archetype
# ---------------------------------------------------------------------------
def _first_token(raw: str | None) -> str:
    if not raw:
        return ""
    return raw.split(";", 1)[0].strip().lower().replace(" ", "_").replace("-", "_")


_STUPA = frozenset({"stupa", "chaitya"})
_CHORTEN = frozenset({"chorten"})
_SHRINE = frozenset({"shrine", "wayside_shrine"})
_MOSQUE = frozenset({"mosque", "eidgah"})
_CHURCH = frozenset({"church", "chapel", "cathedral"})
_GOMPA = frozenset({"monastery", "gompa", "gumba", "gumpa", "vihar", "vihara", "bihar"})
_TEMPLE = frozenset({"temple", "mandir", "pagoda", "dewal", "deval"})
_STUPA_NAME_RE = re.compile(r"stupa|chaitya|chaity|स्तूप|स्तुप|चैत्य|\bbou?dd?h?a(?:nath)?\b|baudha|bodnath|"
                            r"swayam|swoyam|स्वयम्भू")
_SHIKHARA_NAME_RE = re.compile(r"krishna\s*mandir|vatsala|siddhi\s*lakshmi|pratappur|anantapur|mahabou?dd?ha|"
                               r"shikhar|शिखर|कृष्ण\s*मन्दिर")
_BAHAL_NAME_RE = re.compile(r"bah(?:a|al|il|i)\b|baha\b|bahal|vihar|बहाल|बही|विहार")
_GOMPA_NAME_RE = re.compile(r"gompa|gumba|gonpa|monastery|\bling\b|choling|shedrub|गुम्बा|गोम्पा")
RELIGIOUS_ARCHETYPES = frozenset({A.TEMPLE_PAGODA, A.TEMPLE_SHIKHARA, A.STUPA, A.GOMPA, A.CHORTEN, A.SHRINE,
                                  A.MOSQUE, A.CHURCH})
SUSPECT_MIN_HEIGHT_M = 2.5
SUSPECT_MAX_LEVELS = 20
SUSPECT_STOREY_M = (1.8, 6.0)
_RELIGIONS = {
    "hindu": "hindu", "hinduism": "hindu",
    "buddhist": "buddhist", "buddhism": "buddhist", "budhhist": "buddhist", "buddist": "buddhist",
    "budhist": "buddhist", "bon": "buddhist",
    "muslim": "muslim", "islam": "muslim", "islamic": "muslim",
    "christian": "christian", "christianity": "christian",
}


def _religion_family(raw: str | None) -> str:
    # tags.py keeps its religion normaliser private; this mirrors its aliases.
    for part in (raw or "").split(";"):
        fam = _RELIGIONS.get(part.strip().lower())
        if fam:
            return fam
    return ""


def _is_terai(ctx: BuildingContext) -> bool:
    return ctx.zone == "terai" or (math.isfinite(ctx.elev_m) and ctx.elev_m < TERAI_MAX_ELEV_M)


def _temple(ctx: BuildingContext) -> A:
    return A.TEMPLE_SHIKHARA if _is_terai(ctx) else A.TEMPLE_PAGODA


def _names(b: BuildingFeature) -> str:
    if b.name is None:
        return ""
    return " | ".join(s for s in (b.name.default, b.name.en, b.name.ne) if s).casefold()


def classify(b: BuildingFeature, ctx: BuildingContext) -> A:
    """Archetype for one building (rules in the module docstring)."""
    btype = _first_token(b.building_raw)
    religion = _religion_family(b.religion)
    extra = b.extra or {}
    stupa_tag = "stupa" in ((extra.get("tower:type") or "").lower(), (extra.get("man_made") or "").lower())
    if b.use == U.RELIGIOUS or religion or btype in _TEMPLE | _STUPA | _GOMPA or stupa_tag:
        if btype in _STUPA or stupa_tag:
            return A.STUPA
        if btype in _CHORTEN:
            return A.CHORTEN
        if btype in _SHRINE:
            return A.SHRINE
        if btype in _MOSQUE:
            return A.MOSQUE
        if btype in _CHURCH:
            return A.CHURCH
        if btype in _GOMPA:
            return A.GOMPA
        names = _names(b)
        if names and _STUPA_NAME_RE.search(names):
            return A.STUPA
        if b.roof_shape == RS.SHIKHARA or (names and _SHIKHARA_NAME_RE.search(names)):
            return A.TEMPLE_SHIKHARA
        if names and _BAHAL_NAME_RE.search(names):
            return A.TEMPLE_PAGODA
        if names and _GOMPA_NAME_RE.search(names):
            return A.GOMPA
        if btype in _TEMPLE:
            if religion == "buddhist" and ctx.zone != "newar_core":
                return A.GOMPA
            return _temple(ctx)
        if religion == "muslim":
            return A.MOSQUE
        if religion == "christian":
            return A.CHURCH
        if religion == "buddhist":
            return A.TEMPLE_PAGODA if ctx.zone == "newar_core" else A.GOMPA
        if religion == "hindu":
            return _temple(ctx)
        if b.use == U.RELIGIOUS:
            return A.SHRINE
        # religion tag that names no known faith on a non-religious building: fall through
    use = b.use
    if use == U.INDUSTRIAL:
        return A.INDUSTRIAL
    if use in (U.EDUCATION, U.HEALTH, U.PUBLIC, U.OFFICE):
        return A.INSTITUTIONAL
    if use == U.HUT:
        return A.HUT
    if use == U.GREENHOUSE:
        return A.GREENHOUSE
    elev_ok = math.isfinite(ctx.elev_m)
    if use == U.HOTEL and (ctx.zone == "sherpa" or (elev_ok and ctx.elev_m >= TEAHOUSE_MIN_ELEV_M)):
        return A.TEAHOUSE
    if ctx.zone == "newar_core":
        return A.NEWAR
    if ctx.zone == "sherpa":
        return A.SHERPA_HIMALAYAN
    if ctx.zone == "trans_himalaya":
        return A.TRANS_HIMALAYAN
    if _is_terai(ctx):
        return A.MODERN_URBAN if ctx.density_bin >= 2 else A.TERAI
    if ctx.density_bin >= 2:
        return A.MODERN_URBAN
    if elev_ok and TERAI_MAX_ELEV_M <= ctx.elev_m < HILL_MAX_ELEV_M:
        return A.HILL_VILLAGE
    return A.GENERIC


# ---------------------------------------------------------------------------
# Tables
# ---------------------------------------------------------------------------
Dist = tuple[tuple[object, float], ...]

# (archetype) -> {density bin or None for "any": levels distribution}
LEVELS: dict[A, dict[int | None, Dist]] = {
    A.MODERN_URBAN: {3: ((3, 0.2), (4, 0.4), (5, 0.25), (6, 0.15)),
                     2: ((2, 0.3), (3, 0.45), (4, 0.25)),
                     None: ((1, 0.3), (2, 0.5), (3, 0.2))},
    A.NEWAR: {None: ((3, 0.3), (4, 0.5), (5, 0.2))},
    A.HILL_VILLAGE: {None: ((1, 0.45), (2, 0.55))},
    A.TERAI: {None: ((1, 0.6), (2, 0.4))},
    A.SHERPA_HIMALAYAN: {None: ((2, 1.0),)},
    A.TRANS_HIMALAYAN: {None: ((1, 0.4), (2, 0.6))},
    A.TEAHOUSE: {None: ((2, 0.6), (3, 0.4))},
    A.INSTITUTIONAL: {3: ((2, 0.3), (3, 0.4), (4, 0.3)), 2: ((2, 0.3), (3, 0.4), (4, 0.3)),
                      None: ((1, 0.3), (2, 0.45), (3, 0.25))},
    A.INDUSTRIAL: {None: ((1, 0.8), (2, 0.2))},
    A.TEMPLE_PAGODA: {None: ((1, 0.4), (2, 0.4), (3, 0.2))},
    A.TEMPLE_SHIKHARA: {None: ((1, 1.0),)},
    A.STUPA: {None: ((1, 1.0),)},
    A.GOMPA: {None: ((2, 0.5), (3, 0.5))},
    A.CHORTEN: {None: ((1, 1.0),)},
    A.SHRINE: {None: ((1, 1.0),)},
    A.MOSQUE: {None: ((1, 0.5), (2, 0.5))},
    A.CHURCH: {None: ((1, 0.6), (2, 0.4))},
    A.HUT: {None: ((1, 1.0),)},
    A.GREENHOUSE: {None: ((1, 1.0),)},
    A.GENERIC: {3: ((3, 0.6), (4, 0.4)), 2: ((2, 0.5), (3, 0.5)), 1: ((1, 0.5), (2, 0.5)),
                None: ((1, 0.6), (2, 0.4))},
}

ROOF_SHAPES: dict[A, Dist] = {
    A.GENERIC: ((RS.FLAT, 0.6), (RS.GABLED, 0.4)),
    A.NEWAR: ((RS.GABLED, 0.6), (RS.FLAT, 0.3), (RS.HIPPED, 0.1)),
    A.MODERN_URBAN: ((RS.FLAT, 0.9), (RS.SKILLION, 0.05), (RS.GABLED, 0.05)),
    A.HILL_VILLAGE: ((RS.GABLED, 0.5), (RS.HIPPED, 0.3), (RS.FLAT, 0.2)),
    A.TERAI: ((RS.FLAT, 0.4), (RS.GABLED, 0.4), (RS.HIPPED, 0.2)),
    A.SHERPA_HIMALAYAN: ((RS.GABLED, 0.8), (RS.HIPPED, 0.2)),
    A.TRANS_HIMALAYAN: ((RS.FLAT, 1.0),),
    A.TEMPLE_PAGODA: ((RS.PAGODA, 1.0),),
    A.TEMPLE_SHIKHARA: ((RS.SHIKHARA, 1.0),),
    A.STUPA: ((RS.DOME, 1.0),),
    A.GOMPA: ((RS.HIPPED, 0.5), (RS.FLAT, 0.3), (RS.GABLED, 0.2)),
    A.CHORTEN: ((RS.CONE, 1.0),),
    A.SHRINE: ((RS.PYRAMIDAL, 0.5), (RS.FLAT, 0.3), (RS.GABLED, 0.2)),
    A.MOSQUE: ((RS.DOME, 0.6), (RS.FLAT, 0.4)),
    A.CHURCH: ((RS.GABLED, 0.8), (RS.FLAT, 0.2)),
    A.INDUSTRIAL: ((RS.GABLED, 0.4), (RS.FLAT, 0.3), (RS.SKILLION, 0.3)),
    A.INSTITUTIONAL: ((RS.FLAT, 0.6), (RS.GABLED, 0.25), (RS.HIPPED, 0.15)),
    A.HUT: ((RS.GABLED, 0.6), (RS.SKILLION, 0.4)),
    A.GREENHOUSE: ((RS.ROUND, 0.7), (RS.GABLED, 0.3)),
    A.TEAHOUSE: ((RS.GABLED, 0.7), (RS.HIPPED, 0.3)),
}

ROOF_MATERIALS: dict[A, Dist] = {
    A.GENERIC: ((RM.CONCRETE, 0.5), (RM.METAL, 0.5)),
    A.NEWAR: ((RM.TILES, 0.6), (RM.CONCRETE, 0.3), (RM.METAL, 0.1)),
    A.MODERN_URBAN: ((RM.CONCRETE, 0.9), (RM.METAL, 0.1)),
    A.HILL_VILLAGE: ((RM.METAL, 0.55), (RM.CONCRETE, 0.15), (RM.SLATE, 0.15), (RM.THATCH, 0.1),
                     (RM.TILES, 0.05)),
    A.TERAI: ((RM.CONCRETE, 0.3), (RM.TILES, 0.3), (RM.METAL, 0.3), (RM.THATCH, 0.1)),
    A.SHERPA_HIMALAYAN: ((RM.METAL, 0.6), (RM.STONE, 0.2), (RM.WOOD, 0.2)),
    A.TRANS_HIMALAYAN: ((RM.MUD, 0.9), (RM.WOOD, 0.1)),
    A.TEMPLE_PAGODA: ((RM.TILES, 0.85), (RM.METAL, 0.15)),
    A.TEMPLE_SHIKHARA: ((RM.CONCRETE, 0.6), (RM.STONE, 0.4)),
    A.STUPA: ((RM.CONCRETE, 1.0),),
    A.GOMPA: ((RM.METAL, 0.6), (RM.TILES, 0.2), (RM.CONCRETE, 0.2)),
    A.CHORTEN: ((RM.STONE, 1.0),),
    A.SHRINE: ((RM.METAL, 0.4), (RM.TILES, 0.3), (RM.CONCRETE, 0.3)),
    A.MOSQUE: ((RM.CONCRETE, 1.0),),
    A.CHURCH: ((RM.METAL, 0.5), (RM.CONCRETE, 0.5)),
    A.INDUSTRIAL: ((RM.METAL, 0.8), (RM.CONCRETE, 0.2)),
    A.INSTITUTIONAL: ((RM.CONCRETE, 0.6), (RM.METAL, 0.4)),
    A.HUT: ((RM.METAL, 0.6), (RM.THATCH, 0.3), (RM.WOOD, 0.1)),
    A.GREENHOUSE: ((RM.GLASS, 1.0),),
    A.TEAHOUSE: ((RM.METAL, 0.8), (RM.STONE, 0.2)),
}

WALL_MATERIALS: dict[A, Dist] = {
    A.GENERIC: ((WM.PLASTER, 0.6), (WM.BRICK, 0.4)),
    A.NEWAR: ((WM.BRICK, 0.85), (WM.PLASTER, 0.15)),
    A.MODERN_URBAN: ((WM.PLASTER, 0.8), (WM.BRICK, 0.2)),
    A.HILL_VILLAGE: ((WM.STONE, 0.35), (WM.PLASTER, 0.25), (WM.MUD, 0.25), (WM.BRICK, 0.15)),
    A.TERAI: ((WM.MUD, 0.35), (WM.BRICK, 0.3), (WM.PLASTER, 0.25), (WM.BAMBOO, 0.1)),
    A.SHERPA_HIMALAYAN: ((WM.STONE, 0.9), (WM.WOOD, 0.1)),
    A.TRANS_HIMALAYAN: ((WM.MUD, 0.5), (WM.STONE, 0.5)),
    A.TEMPLE_PAGODA: ((WM.BRICK, 1.0),),
    A.TEMPLE_SHIKHARA: ((WM.PLASTER, 0.6), (WM.STONE, 0.2), (WM.BRICK, 0.2)),
    A.STUPA: ((WM.PLASTER, 1.0),),
    A.GOMPA: ((WM.STONE, 0.5), (WM.PLASTER, 0.5)),
    A.CHORTEN: ((WM.PLASTER, 0.6), (WM.STONE, 0.4)),
    A.SHRINE: ((WM.BRICK, 0.5), (WM.PLASTER, 0.3), (WM.STONE, 0.2)),
    A.MOSQUE: ((WM.PLASTER, 1.0),),
    A.CHURCH: ((WM.PLASTER, 0.7), (WM.BRICK, 0.3)),
    A.INDUSTRIAL: ((WM.BRICK, 0.4), (WM.METAL, 0.3), (WM.PLASTER, 0.3)),
    A.INSTITUTIONAL: ((WM.PLASTER, 0.7), (WM.BRICK, 0.3)),
    A.HUT: ((WM.WOOD, 0.3), (WM.STONE, 0.3), (WM.MUD, 0.2), (WM.BAMBOO, 0.2)),
    A.GREENHOUSE: ((WM.GLASS, 1.0),),
    A.TEAHOUSE: ((WM.STONE, 0.6), (WM.PLASTER, 0.2), (WM.WOOD, 0.2)),
}

ROOF_ALLOWANCE_M: dict[RS, float] = {
    RS.FLAT: 0.5, RS.SKILLION: 1.0, RS.GABLED: 1.5, RS.HIPPED: 1.5, RS.HALF_HIPPED: 1.5, RS.GAMBREL: 1.5,
    RS.MANSARD: 1.5, RS.PYRAMIDAL: 2.0, RS.CONE: 2.0, RS.ROUND: 2.0, RS.DOME: 3.0, RS.ONION: 3.0,
    RS.PAGODA: 4.0, RS.SHIKHARA: 6.0,
}
_SLOPED_MATERIALS = frozenset({RM.METAL, RM.TILES, RM.SLATE, RM.THATCH})
_ONE_LEVEL_USES = frozenset({U.ROOF, U.GARAGE, U.CONSTRUCTION})


def _levels_dist(arch: A, density_bin: int) -> Dist:
    table = LEVELS.get(arch, LEVELS[A.GENERIC])
    return table.get(int(density_bin), table[None])


def roof_allowance(shape: RS) -> float:
    return ROOF_ALLOWANCE_M.get(shape, 1.5)


# ---------------------------------------------------------------------------
# Inference
# ---------------------------------------------------------------------------
def plausibility(b: BuildingFeature) -> tuple[bool, bool]:
    """``(height_suspect, levels_suspect)`` of the tagged values (D4 gate)."""
    has_parts = bool(b.flags & BuildingFlags.HAS_PARTS) or bool(b.flags & BuildingFlags.PART)
    h_bad = b.height_m is not None and b.height_m < SUSPECT_MIN_HEIGHT_M and not has_parts
    l_bad = b.levels is not None and b.levels > SUSPECT_MAX_LEVELS
    if b.height_m is not None and b.levels is not None and b.levels >= 1 and not (h_bad or l_bad):
        per = (b.height_m - (b.min_height_m or 0.0)) / b.levels
        if not SUSPECT_STOREY_M[0] <= per <= SUSPECT_STOREY_M[1]:
            h_bad = l_bad = True
    return h_bad, l_bad


def infer_buildings(buildings: list[BuildingFeature], contexts: list[BuildingContext],
                    overrides: dict[int, A] | None = None, part_hosts: dict[int, int] | None = None) -> dict:
    """Fill archetype, levels, height, roof and materials in place; return stats.

    Expects buildings as ``osm_extract`` produces them: a value present on entry
    is treated as tagged (``levels``, ``height_m``, ``roof_shape``,
    ``roof_material``, ``wall_material``), so call this once per extract.
    ``overrides`` maps a building index to a curated archetype (hero records);
    ``part_hosts`` maps a ``building:part`` index to its host index
    (``poi_hints.link_parts``): parts of religious hosts take the host's archetype.
    """
    if len(buildings) != len(contexts):
        raise ValueError(f"{len(buildings)} buildings but {len(contexts)} contexts")
    overrides = overrides or {}
    part_hosts = part_hosts or {}
    archetypes: Counter = Counter()
    zones: Counter = Counter()
    n = Counter()
    # Hosts first, so parts can copy a host's archetype.
    order = sorted(range(len(buildings)), key=lambda i: (i in part_hosts, i))
    for i in order:
        b, ctx = buildings[i], contexts[i]
        seed = building_seed(b)
        if i in overrides:
            arch = overrides[i]
            n["archetype_overrides"] += 1
        elif i in part_hosts and buildings[part_hosts[i]].archetype in RELIGIOUS_ARCHETYPES:
            arch = A(buildings[part_hosts[i]].archetype)
        else:
            arch = classify(b, ctx)
        b.archetype = arch
        archetypes[arch.name] += 1
        zones[ctx.zone] += 1
        flags = BuildingFlags(b.flags)
        if b.use == U.ROOF:
            flags |= BuildingFlags.OPEN_CANOPY
        h_bad, l_bad = plausibility(b)
        if h_bad or l_bad:
            flags |= BuildingFlags.TAG_SUSPECT
            n["tag_suspect"] += 1
            if arch not in RELIGIOUS_ARCHETYPES:
                if h_bad:
                    b.height_m = None
                if l_bad:
                    b.levels = None
                n["tag_suspect_reinferred"] += 1

        # Roof.
        if b.roof_shape != RS.UNKNOWN:
            flags |= BuildingFlags.ROOF_TAGGED
            n["roof_tagged"] += 1
        else:
            b.roof_shape = _pick(ROOF_SHAPES.get(arch, ROOF_SHAPES[A.GENERIC]), _unit(seed, _SALT_ROOF_SHAPE))
        if b.roof_material == RM.UNKNOWN:
            mat = _pick(ROOF_MATERIALS.get(arch, ROOF_MATERIALS[A.GENERIC]), _unit(seed, _SALT_ROOF_MAT))
            if b.roof_shape == RS.FLAT and mat in _SLOPED_MATERIALS:
                mat = RM.MUD if arch == A.TRANS_HIMALAYAN else RM.CONCRETE
            b.roof_material = mat
        if b.wall_material == WM.UNKNOWN:
            b.wall_material = _pick(WALL_MATERIALS.get(arch, WALL_MATERIALS[A.GENERIC]), _unit(seed, _SALT_WALL))
        allowance = roof_allowance(b.roof_shape)

        # Levels.
        height_tagged = b.height_m is not None
        if b.levels is not None:
            n["levels_tagged"] += 1
        else:
            flags |= BuildingFlags.LEVELS_INFERRED
            if height_tagged:
                base = (b.min_height_m or 0.0)
                b.levels = float(max(1, round((b.height_m - base - allowance) / LEVEL_HEIGHT_M)))
                n["levels_from_height"] += 1
            elif b.use in _ONE_LEVEL_USES:
                b.levels = 1.0
            elif footprint_area_m2(b) < TINY_FOOTPRINT_M2:
                b.levels = 1.0
                n["tiny_footprints"] += 1
            else:
                b.levels = float(_pick(_levels_dist(arch, ctx.density_bin), _unit(seed, _SALT_LEVELS)))
            n["levels_inferred"] += 1

        # Height.
        if height_tagged:
            flags |= BuildingFlags.HEIGHT_TAGGED
            n["height_tagged"] += 1
        else:
            b.height_m = round((b.min_height_m or 0.0) + b.levels * LEVEL_HEIGHT_M + allowance, 2)
        b.flags = flags

    total = len(buildings)
    return {
        "buildings": total,
        "archetypes": dict(sorted(archetypes.items())),
        "zones": dict(sorted(zones.items())),
        "levels_tagged": n["levels_tagged"],
        "levels_inferred": n["levels_inferred"],
        "levels_from_height": n["levels_from_height"],
        "tiny_footprints": n["tiny_footprints"],
        "height_tagged": n["height_tagged"],
        "roof_tagged": n["roof_tagged"],
        "tag_suspect": n["tag_suspect"],
        "tag_suspect_reinferred": n["tag_suspect_reinferred"],
        "archetype_overrides": n["archetype_overrides"],
        "levels_tagged_pct": round(100.0 * n["levels_tagged"] / total, 2) if total else 0.0,
    }
