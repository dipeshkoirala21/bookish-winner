"""Biome classification: WorldCover + OSM land use + elevation + slope + zone -> ``model.Biome``.

The ``BIOM`` chunk (docs/DATA_FORMATS.md section 1.2) stores one ``Biome`` per
vertex-aligned sample. ``compute_biome_grid`` samples every input at the
sample points: heights, slope, WorldCover class, zone and noise. Every input
is a pure function of position (see ``dem.py``), so the shared edges of
neighbouring tiles classify identically. The ``area_kind`` grid comes from
``rasterize.rasterize_at_samples``, which has the same property.

Rules (``classify``)
--------------------
``e`` is the elevation dithered by value noise, ``elev + (noise - 0.5) * 200 m``,
so band edges wander naturally instead of following contour lines. ``s`` is
the slope in degrees. ``WC`` is the WorldCover code and ``A`` the OSM
``AreaKind``. Zones come from ``config/biome_zones.yaml``: T = terai,
TH = trans_himalaya, KV = kathmandu_valley_floor, KS = khumbu_sherpa. The
first matching row wins.

==  =========================================================  ====================================
#   Condition                                                  Biome
==  =========================================================  ====================================
1   A is WATER_LAKE / WATER_RIVER / WATER_POND                 WATER
2   A is GLACIER                                               GLACIER
3   A is SAND_SHINGLE (mapped riverbed bars)                   RIVERBED_GRAVEL
4   WC 80 (permanent water)                                    WATER
5   WC 70 (snow/ice) and e >= 4800                             SNOW
6   WC 70 and 4000 <= e < 4800                                 GLACIER (ice tongues, avalanche cones)
    (WC 70 below 4000 m is treated as unknown cover: seasonal snow or cloud)
7   A is PARK / PITCH / CEMETERY                               URBAN_GREEN
8   A is COMMERCIAL / INDUSTRIAL / PEDESTRIAN                  URBAN_DENSE
9   A is RELIGIOUS: WC 10/20/30 -> URBAN_GREEN (sacred groves), else URBAN_DENSE
10  A is AERODROME: WC 50 -> URBAN_DENSE, else URBAN_GREEN (grass strips)
11  A is RESIDENTIAL: WC 10/20/30 -> URBAN_GREEN; WC 40 -> fall through to the
    cropland rules (fields inside loosely drawn village polygons); else URBAN_DENSE
12  WC 50 (built-up)                                           URBAN_DENSE
13  A is ORCHARD / TEA_GARDEN                                  ORCHARD / TEA_GARDEN
14  A is WETLAND, or WC 90 / 95                                WETLAND
==  =========================================================  ====================================

Remaining samples are classified by *cover*: the WorldCover class when it is
10/20/30/40/60/100. Otherwise (no data, or low "snow") the OSM area decides:
FOREST -> 10, SCRUB -> 20, MEADOW/GRASSLAND -> 30, FARMLAND -> 40,
BARE_ROCK/SCREE -> 60. OSM FARMLAND over WC 20/30/60 counts as cropland too,
since fallow fields look like grass or bare soil to the classifier.

==========  ========================================================  ===========================
Cover       Condition (first match)                                   Biome
==========  ========================================================  ===========================
40 crop     zone TH                                                   TRANS_HIMALAYAN_CROPLAND
            zone T or e < 300: noise < 0.6 / otherwise                TERAI_PADDY / TERAI_CROPLAND
            zone KV                                                   VALLEY_CROPLAND
            zone KS (stone-walled potato terraces)                    HILL_TERRACES
            s >= 8 and 500 <= e < 4000                                HILL_TERRACES
            otherwise (valley-bottom tars, flat high fields)          VALLEY_CROPLAND
10 trees    zone T or e < 300                                         TERAI_SAL_FOREST
            e < 1000 (Siwalik / low-valley sal and chir pine)         CHURE_FOREST
            e < 3000 (Schima-Castanopsis, oak, pine)                  HILL_FOREST
            e < 3900 (fir, birch, rhododendron)                       SUBALPINE_FOREST
            otherwise (above tree line)                               ALPINE_SCRUB
20 shrub    zone TH                                                   TRANS_HIMALAYAN_STEPPE
            zone T or e < 300 (tall-grass/scrub floodplain)           TERAI_GRASSLAND
            e < 3000                                                  HILL_SCRUB
            otherwise (juniper, dwarf rhododendron)                   ALPINE_SCRUB
30 grass    zone TH                                                   TRANS_HIMALAYAN_STEPPE
            zone T or e < 300                                         TERAI_GRASSLAND
            e < 3000                                                  HILL_GRASSLAND
            otherwise                                                 ALPINE_MEADOW
100 moss    always                                                    ALPINE_MEADOW
60 bare     not OSM rock, e < 1500, s < 6, zone not KV                RIVERBED_GRAVEL
            zone TH and e < 5000                                      TRANS_HIMALAYAN_STEPPE
            e >= 4300 and s < 20                                      MORAINE
            not OSM rock, e < 3000, s < 30 (landslides, kilns)        BARE_SOIL
            otherwise                                                 SCREE_ROCK
none        zone TH and e < 5000                                      TRANS_HIMALAYAN_STEPPE
            zone T or e < 300                                         TERAI_CROPLAND
            e < 1000 / 3000 / 3900                                    CHURE_FOREST / HILL_FOREST /
                                                                      SUBALPINE_FOREST
            e < 4600 / 5500 / above                                   ALPINE_MEADOW / SCREE_ROCK / SNOW
==========  ========================================================  ===========================

Thresholds follow the usual vegetation belts of Nepal: tropical below
1000 m, subtropical to 2000 m, temperate to 3000 m, subalpine to the tree
line at 3800-4000 m, alpine to the snow line at 4800-5200 m. In the real
N27E085 tile, WorldCover cropland at 1000-2000 m has a median slope of 13.6
degrees, so the 8 degree terrace threshold marks about 70 % of mid-hill fields
as terraces.
"""

from __future__ import annotations

from dataclasses import dataclass
from enum import IntEnum
from functools import lru_cache
from pathlib import Path
from typing import Iterable

import numpy as np
import shapely
import yaml
from shapely.geometry import Polygon

from . import projection
from .config import CONFIG_DIR
from .dem import DemSampler
from .landcover import LandcoverSampler
from .model import AreaKind, Biome
from .model import WorldCover as WC
from .projection import TileId, grid_coords
from .rasterize import burn_key, rasterize_at_samples


class Zone(IntEnum):
    """Biome zone codes (config/biome_zones.yaml)."""

    DEFAULT = 0
    TERAI = 1
    TRANS_HIMALAYA = 2
    KATHMANDU_VALLEY_FLOOR = 3
    KHUMBU_SHERPA = 4


# --- thresholds (metres / degrees); see the module docstring ---------------------
ELEV_DITHER_M = 100.0
TERAI_MAX_M = 300.0
CHURE_MAX_M = 1000.0
HILL_MAX_M = 3000.0
TREELINE_M = 3900.0
ICE_MIN_M = 4000.0
SNOWLINE_M = 4800.0
STEPPE_MAX_M = 5000.0
MORAINE_MIN_M = 4300.0
MORAINE_MAX_SLOPE = 20.0
RIVERBED_MAX_M = 1500.0
RIVERBED_MAX_SLOPE = 6.0
TERRACE_MIN_SLOPE = 8.0
TERRACE_MIN_M = 500.0
TERRACE_MAX_M = 4000.0
BARE_SOIL_MAX_M = 3000.0
BARE_SOIL_MAX_SLOPE = 30.0
FALLBACK_MEADOW_MAX_M = 4600.0
FALLBACK_ROCK_MAX_M = 5500.0
PADDY_NOISE = 0.6

NOISE_CELL_M = 200.0
NOISE_SEED = 0

DEFAULT_ZONES_PATH = CONFIG_DIR / "biome_zones.yaml"

_WATER_AREAS = (AreaKind.WATER_LAKE, AreaKind.WATER_RIVER, AreaKind.WATER_POND)
_COVER_WC = (WC.TREE_COVER, WC.SHRUBLAND, WC.GRASSLAND, WC.CROPLAND, WC.BARE_SPARSE, WC.MOSS_LICHEN)

_OSM_COVER = np.zeros(256, dtype=np.uint8)
for _kind, _wc in ((AreaKind.FOREST, WC.TREE_COVER), (AreaKind.SCRUB, WC.SHRUBLAND),
                   (AreaKind.MEADOW, WC.GRASSLAND), (AreaKind.GRASSLAND, WC.GRASSLAND),
                   (AreaKind.FARMLAND, WC.CROPLAND), (AreaKind.BARE_ROCK, WC.BARE_SPARSE),
                   (AreaKind.SCREE, WC.BARE_SPARSE)):
    _OSM_COVER[int(_kind)] = int(_wc)


# ---------------------------------------------------------------------------
# Classification
# ---------------------------------------------------------------------------
def classify(worldcover, elev_m, slope_deg, area_kind, zone, noise) -> np.ndarray:
    """Biome per sample from the rule tables in the module docstring.

    All inputs broadcast together: ``worldcover`` (uint8 WorldCover codes),
    ``elev_m``, ``slope_deg``, ``area_kind`` (``AreaKind``, 0 = none),
    ``zone`` (``Zone``) and ``noise`` in [0, 1). Returns a uint8 ``Biome``
    array; every sample gets a biome (never ``NONE``).
    """
    wc, elev, slope, area, zn, nz = np.broadcast_arrays(
        np.asarray(worldcover, dtype=np.int16), np.asarray(elev_m, dtype=np.float64),
        np.asarray(slope_deg, dtype=np.float64), np.asarray(area_kind, dtype=np.int16),
        np.asarray(zone, dtype=np.int16), np.asarray(noise, dtype=np.float64))
    e = elev + (nz - 0.5) * (2.0 * ELEV_DITHER_M)
    out = np.zeros(wc.shape, dtype=np.uint8)
    todo = np.ones(wc.shape, dtype=bool)

    def put(mask: np.ndarray, biome: Biome) -> None:
        m = todo & mask
        out[m] = biome
        np.logical_and(todo, ~m, out=todo)

    terai = (zn == Zone.TERAI) | (e < TERAI_MAX_M)
    trans = zn == Zone.TRANS_HIMALAYA
    valley = zn == Zone.KATHMANDU_VALLEY_FLOOR
    khumbu = zn == Zone.KHUMBU_SHERPA
    wc_veg = np.isin(wc, (WC.TREE_COVER, WC.SHRUBLAND, WC.GRASSLAND))

    # 1-6: water, ice and snow.
    put(np.isin(area, _WATER_AREAS), Biome.WATER)
    put(area == AreaKind.GLACIER, Biome.GLACIER)
    put(area == AreaKind.SAND_SHINGLE, Biome.RIVERBED_GRAVEL)
    put(wc == WC.PERMANENT_WATER, Biome.WATER)
    snow_ice = wc == WC.SNOW_ICE
    put(snow_ice & (e >= SNOWLINE_M), Biome.SNOW)
    put(snow_ice & (e >= ICE_MIN_M), Biome.GLACIER)

    # 7-12: settlements.
    put(np.isin(area, (AreaKind.PARK, AreaKind.PITCH, AreaKind.CEMETERY)), Biome.URBAN_GREEN)
    put(np.isin(area, (AreaKind.COMMERCIAL, AreaKind.INDUSTRIAL, AreaKind.PEDESTRIAN)), Biome.URBAN_DENSE)
    religious = area == AreaKind.RELIGIOUS
    put(religious & wc_veg, Biome.URBAN_GREEN)
    put(religious, Biome.URBAN_DENSE)
    aerodrome = area == AreaKind.AERODROME
    put(aerodrome & (wc == WC.BUILT_UP), Biome.URBAN_DENSE)
    put(aerodrome, Biome.URBAN_GREEN)
    residential = area == AreaKind.RESIDENTIAL
    put(residential & wc_veg, Biome.URBAN_GREEN)
    put(residential & (wc != WC.CROPLAND), Biome.URBAN_DENSE)
    put(wc == WC.BUILT_UP, Biome.URBAN_DENSE)

    # 13-14: specific OSM land use and wetlands.
    put(area == AreaKind.ORCHARD, Biome.ORCHARD)
    put(area == AreaKind.TEA_GARDEN, Biome.TEA_GARDEN)
    put((area == AreaKind.WETLAND) | np.isin(wc, (WC.HERBACEOUS_WETLAND, WC.MANGROVES)), Biome.WETLAND)

    # Effective cover class.
    cover = np.where(np.isin(wc, _COVER_WC), wc, 0)
    cover = np.where((area == AreaKind.FARMLAND) & np.isin(wc, (WC.SHRUBLAND, WC.GRASSLAND, WC.BARE_SPARSE)),
                     WC.CROPLAND, cover)
    cover = np.where(cover == 0, _OSM_COVER[np.clip(area, 0, 255)], cover)
    osm_rock = np.isin(area, (AreaKind.BARE_ROCK, AreaKind.SCREE))

    crop = cover == WC.CROPLAND
    put(crop & trans, Biome.TRANS_HIMALAYAN_CROPLAND)
    put(crop & terai & (nz < PADDY_NOISE), Biome.TERAI_PADDY)
    put(crop & terai, Biome.TERAI_CROPLAND)
    put(crop & valley, Biome.VALLEY_CROPLAND)
    put(crop & khumbu, Biome.HILL_TERRACES)
    put(crop & (slope >= TERRACE_MIN_SLOPE) & (e >= TERRACE_MIN_M) & (e < TERRACE_MAX_M), Biome.HILL_TERRACES)
    put(crop, Biome.VALLEY_CROPLAND)

    trees = cover == WC.TREE_COVER
    put(trees & terai, Biome.TERAI_SAL_FOREST)
    put(trees & (e < CHURE_MAX_M), Biome.CHURE_FOREST)
    put(trees & (e < HILL_MAX_M), Biome.HILL_FOREST)
    put(trees & (e < TREELINE_M), Biome.SUBALPINE_FOREST)
    put(trees, Biome.ALPINE_SCRUB)

    shrub = cover == WC.SHRUBLAND
    put(shrub & trans, Biome.TRANS_HIMALAYAN_STEPPE)
    put(shrub & terai, Biome.TERAI_GRASSLAND)
    put(shrub & (e < HILL_MAX_M), Biome.HILL_SCRUB)
    put(shrub, Biome.ALPINE_SCRUB)

    grass = cover == WC.GRASSLAND
    put(grass & trans, Biome.TRANS_HIMALAYAN_STEPPE)
    put(grass & terai, Biome.TERAI_GRASSLAND)
    put(grass & (e < HILL_MAX_M), Biome.HILL_GRASSLAND)
    put(grass, Biome.ALPINE_MEADOW)

    put(cover == WC.MOSS_LICHEN, Biome.ALPINE_MEADOW)

    bare = cover == WC.BARE_SPARSE
    put(bare & ~osm_rock & ~valley & (e < RIVERBED_MAX_M) & (slope < RIVERBED_MAX_SLOPE), Biome.RIVERBED_GRAVEL)
    put(bare & trans & (e < STEPPE_MAX_M), Biome.TRANS_HIMALAYAN_STEPPE)
    put(bare & (e >= MORAINE_MIN_M) & (slope < MORAINE_MAX_SLOPE), Biome.MORAINE)
    put(bare & ~osm_rock & (e < BARE_SOIL_MAX_M) & (slope < BARE_SOIL_MAX_SLOPE), Biome.BARE_SOIL)
    put(bare, Biome.SCREE_ROCK)

    # No cover information: elevation bands.
    put(trans & (e < STEPPE_MAX_M), Biome.TRANS_HIMALAYAN_STEPPE)
    put(terai, Biome.TERAI_CROPLAND)
    put(e < CHURE_MAX_M, Biome.CHURE_FOREST)
    put(e < HILL_MAX_M, Biome.HILL_FOREST)
    put(e < TREELINE_M, Biome.SUBALPINE_FOREST)
    put(e < FALLBACK_MEADOW_MAX_M, Biome.ALPINE_MEADOW)
    put(e < FALLBACK_ROCK_MAX_M, Biome.SCREE_ROCK)
    put(todo, Biome.SNOW)
    return out


# ---------------------------------------------------------------------------
# Zones
# ---------------------------------------------------------------------------
@dataclass(frozen=True)
class BiomeZones:
    """Zone polygons in lon/lat; later entries win where they overlap."""

    zones: tuple[tuple[Zone, object], ...]  # (zone, prepared shapely geometry)

    def at_lonlat(self, lon, lat) -> np.ndarray:
        lon, lat = np.broadcast_arrays(np.asarray(lon, dtype=np.float64), np.asarray(lat, dtype=np.float64))
        out = np.zeros(lon.shape, dtype=np.uint8)
        for code, geom in self.zones:
            out[shapely.contains_xy(geom, lon, lat)] = code
        return out

    def at_game(self, x, z) -> np.ndarray:
        x, z = np.broadcast_arrays(np.asarray(x, dtype=np.float64), np.asarray(z, dtype=np.float64))
        lon, lat = projection.game_to_lonlat(x, z)
        return self.at_lonlat(np.asarray(lon), np.asarray(lat))


def load_zones(path: Path | None = None) -> BiomeZones:
    """Load ``config/biome_zones.yaml`` (or ``path``)."""
    path = Path(path) if path is not None else DEFAULT_ZONES_PATH
    data = yaml.safe_load(path.read_text(encoding="utf-8"))
    zones = []
    for entry in data.get("zones", []):
        zid = str(entry["id"])
        try:
            code = Zone[zid.upper()]
        except KeyError:
            raise ValueError(f"{path}: unknown zone id {zid!r}; expected one of "
                             f"{[z.name.lower() for z in Zone if z]}") from None
        if code == Zone.DEFAULT:
            raise ValueError(f"{path}: the default zone has no polygons")
        if "code" in entry and int(entry["code"]) != int(code):
            raise ValueError(f"{path}: zone {zid!r} has code {entry['code']}, expected {int(code)}")
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
        zones.append((code, geom))
    return BiomeZones(tuple(zones))


@lru_cache(maxsize=1)
def default_zones() -> BiomeZones:
    return load_zones(DEFAULT_ZONES_PATH)


def zone_at_game(x, z, zones: BiomeZones | None = None) -> np.ndarray:
    """uint8 ``Zone`` codes at game-space points (default: the shipped zones)."""
    return (zones or default_zones()).at_game(x, z)


# ---------------------------------------------------------------------------
# Value noise
# ---------------------------------------------------------------------------
_M32 = np.uint64(0xFFFFFFFF)


def _fnv_lattice(ix: np.ndarray, iz: np.ndarray, seed: int) -> np.ndarray:
    """Vectorised ``binio.fnv1a32`` of the 12 little-endian bytes of
    (int32 ix, int32 iz, uint32 seed), as uint64 holding 32-bit values."""
    words = (np.asarray(ix, dtype=np.int64).astype(np.uint32).astype(np.uint64),
             np.asarray(iz, dtype=np.int64).astype(np.uint32).astype(np.uint64),
             np.uint64(seed & 0xFFFFFFFF))
    h = np.full(np.broadcast(*words).shape, 0x811C9DC5, dtype=np.uint64)
    prime = np.uint64(0x01000193)
    for w in words:
        for shift in (0, 8, 16, 24):
            h = ((h ^ ((w >> np.uint64(shift)) & np.uint64(0xFF))) * prime) & _M32
    return h


def _lattice_hash(ix: np.ndarray, iz: np.ndarray, seed: int) -> np.ndarray:
    """32-bit hash of integer lattice points: FNV-1a (``_fnv_lattice``), then
    the murmur3 32-bit finaliser so that neighbouring cells decorrelate."""
    h = _fnv_lattice(ix, iz, seed)
    h ^= h >> np.uint64(16)
    h = (h * np.uint64(0x85EBCA6B)) & _M32
    h ^= h >> np.uint64(13)
    h = (h * np.uint64(0xC2B2AE35)) & _M32
    h ^= h >> np.uint64(16)
    return h


def value_noise(x, z, cell_m: float = NOISE_CELL_M, seed: int = NOISE_SEED) -> np.ndarray:
    """Smooth deterministic noise in [0, 1) as a pure function of game position.

    Lattice values (hashed, ``_lattice_hash / 2^32``) sit on a ``cell_m`` grid
    and are interpolated bilinearly with smoothstep weights.
    """
    if not cell_m > 0:
        raise ValueError(f"cell size must be positive, got {cell_m}")
    x, z = np.broadcast_arrays(np.asarray(x, dtype=np.float64), np.asarray(z, dtype=np.float64))
    gx, gz = x / cell_m, z / cell_m
    fx0, fz0 = np.floor(gx), np.floor(gz)
    tx, tz = gx - fx0, gz - fz0
    ix, iz = fx0.astype(np.int64), fz0.astype(np.int64)
    scale = 1.0 / 4294967296.0
    v00 = _lattice_hash(ix, iz, seed) * scale
    v10 = _lattice_hash(ix + 1, iz, seed) * scale
    v01 = _lattice_hash(ix, iz + 1, seed) * scale
    v11 = _lattice_hash(ix + 1, iz + 1, seed) * scale
    ux = tx * tx * (3.0 - 2.0 * tx)
    uz = tz * tz * (3.0 - 2.0 * tz)
    v = (v00 * (1.0 - ux) + v10 * ux) * (1.0 - uz) + (v01 * (1.0 - ux) + v11 * ux) * uz
    return np.clip(v, 0.0, np.nextafter(1.0, 0.0))


# ---------------------------------------------------------------------------
# Grids
# ---------------------------------------------------------------------------
def compute_biome_grid(xs, zs, spacing_m: float, dem: DemSampler, lc: LandcoverSampler,
                       area_kind: np.ndarray | None = None, zones: BiomeZones | None = None) -> np.ndarray:
    """Biomes at game-space sample points ``(xs, zs)`` for a grid of ``spacing_m``.

    Heights and slope use ``dem`` at that spacing (pre-filtered for coarse
    LODs), land cover uses ``lc`` (windowed mode for coarse LODs), and zones
    and noise are point lookups. ``area_kind`` (same shape, from
    ``rasterize_at_samples``) defaults to none. Each input is a pure function of
    position, so tiles that share an edge get identical edge biomes.
    """
    xs, zs = np.broadcast_arrays(np.asarray(xs, dtype=np.float64), np.asarray(zs, dtype=np.float64))
    lon, lat = projection.game_to_lonlat(xs, zs)
    lon, lat = np.asarray(lon), np.asarray(lat)
    elev = dem.sample_lonlat(lon, lat, order=1, spacing_m=spacing_m)
    slope = dem.slope_deg_game(xs, zs, spacing_m)
    wc = lc.sample_lonlat(lon, lat, spacing_m=spacing_m)
    zone = (zones or default_zones()).at_lonlat(lon, lat)
    noise = value_noise(xs, zs)
    area = np.zeros(xs.shape, dtype=np.uint8) if area_kind is None else np.broadcast_to(area_kind, xs.shape)
    return classify(wc, elev, slope, area, zone, noise)


def biome_grid_for_tile(tile: TileId, n: int, dem: DemSampler, lc: LandcoverSampler,
                        areas: Iterable[tuple[object, AreaKind | int]] = (),
                        zones: BiomeZones | None = None) -> np.ndarray:
    """Convenience: the ``n x n`` ``BIOM`` grid of ``tile`` (row 0 = south).

    ``areas`` are ``(shapely geometry in game metres, AreaKind)``; they are
    burned in ``burn_key`` order (tier, larger first, then input position).
    """
    xs, zs = grid_coords(tile, n)
    spacing = tile.size / (n - 1)
    items = list(areas)
    order = sorted(range(len(items)), key=lambda k: burn_key(items[k][1], items[k][0].area, k))
    shapes = [(items[k][0], int(items[k][1])) for k in order]
    area_kind = rasterize_at_samples(shapes, tile.x0, tile.z0, spacing, n)
    return compute_biome_grid(xs, zs, spacing, dem, lc, area_kind, zones)
