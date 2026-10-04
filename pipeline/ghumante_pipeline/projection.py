"""Coordinate systems and quadtree tile math.

Three coordinate frames are used throughout the project (see
docs/ARCHITECTURE.md section "Coordinates"):

1. **Geographic**: WGS84 longitude/latitude in degrees (EPSG:4326). Used for
   source data, the map UI and anything shown to a human.
2. **Canonical (NPL-TM84)**: a Transverse Mercator projection on the 84 deg E
   meridian (the boundary between UTM zones 44N and 45N), so all of Nepal is in
   one seamless plane with less than 0.2 % scale error. Units are true metres.
   Saves and the search index store positions in this frame (or in lon/lat)
   so they survive changes to the game's scale model.
3. **Game world**: metres in the Unity scene, ``X`` east, ``Z`` north, ``Y`` up.
   ``game = canonical - WORLD_ORIGIN``. The world is true 1:1 (ARCHITECTURE.md
   ADR-004), so there is no warp between the canonical frame and the game.

Quadtree: the root tile covers game ``[0, 2^20) x [0, 2^20)`` metres. A tile at
level ``L`` is ``2^(20-L)`` metres on a side. Tile ``(L, tx, ty)`` covers
``X in [tx*S, (tx+1)*S)`` and ``Z in [ty*S, (ty+1)*S)``. ``ty`` grows northwards.
"""

from __future__ import annotations

from dataclasses import dataclass
from functools import lru_cache
from typing import Iterator

import numpy as np
from pyproj import CRS, Transformer

# --- canonical CRS --------------------------------------------------------
TM84_PROJ4 = (
    "+proj=tmerc +lat_0=0 +lon_0=84 +k=0.9996 +x_0=500000 +y_0=0 "
    "+ellps=WGS84 +towgs84=0,0,0,0,0,0,0 +units=m +no_defs +type=crs"
)
TM84_CENTRAL_MERIDIAN = 84.0
TM84_SCALE_FACTOR = 0.9996
TM84_FALSE_EASTING = 500_000.0
TM84_FALSE_NORTHING = 0.0

# --- game world frame -------------------------------------------------------
# Subtracted from canonical coordinates so that all of Nepal has
# positive game coordinates well inside the quadtree root.
WORLD_ORIGIN_E = 100_000.0
WORLD_ORIGIN_N = 2_900_000.0

# --- quadtree ---------------------------------------------------------------
ROOT_LEVEL_BITS = 20
ROOT_SIZE_M = float(1 << ROOT_LEVEL_BITS)  # 1,048,576 m
MAX_LEVEL = 16  # 16 m tiles; far finer than anything we generate


@lru_cache(maxsize=1)
def tm84_crs() -> CRS:
    return CRS.from_proj4(TM84_PROJ4)


@lru_cache(maxsize=1)
def _fwd() -> Transformer:
    return Transformer.from_crs("EPSG:4326", tm84_crs(), always_xy=True)


@lru_cache(maxsize=1)
def _inv() -> Transformer:
    return Transformer.from_crs(tm84_crs(), "EPSG:4326", always_xy=True)


def lonlat_to_tm84(lon, lat):
    """Geographic degrees -> canonical metres (easting, northing). Vectorised."""
    return _fwd().transform(lon, lat)


def tm84_to_lonlat(e, n):
    """Canonical metres -> geographic degrees (lon, lat). Vectorised."""
    return _inv().transform(e, n)


def tm84_to_game(e, n):
    """Canonical -> game XZ (a pure translation; the world is 1:1)."""
    return np.asarray(e) - WORLD_ORIGIN_E, np.asarray(n) - WORLD_ORIGIN_N


def game_to_tm84(x, z):
    return np.asarray(x) + WORLD_ORIGIN_E, np.asarray(z) + WORLD_ORIGIN_N


def lonlat_to_game(lon, lat):
    e, n = lonlat_to_tm84(lon, lat)
    return tm84_to_game(e, n)


def game_to_lonlat(x, z):
    e, n = game_to_tm84(x, z)
    return tm84_to_lonlat(e, n)


# --- tiles --------------------------------------------------------------------
def tile_size(level: int) -> float:
    if not 0 <= level <= MAX_LEVEL:
        raise ValueError(f"level {level} out of range")
    return ROOT_SIZE_M / (1 << level)


@dataclass(frozen=True, order=True)
class TileId:
    level: int
    tx: int
    ty: int

    def __post_init__(self) -> None:
        n = 1 << self.level
        if not (0 <= self.tx < n and 0 <= self.ty < n):
            raise ValueError(f"tile {self} outside quadtree")

    @property
    def size(self) -> float:
        return tile_size(self.level)

    @property
    def x0(self) -> float:
        return self.tx * self.size

    @property
    def z0(self) -> float:
        return self.ty * self.size

    @property
    def bounds(self) -> tuple[float, float, float, float]:
        """(x_min, z_min, x_max, z_max) in game metres."""
        s = self.size
        return (self.tx * s, self.ty * s, (self.tx + 1) * s, (self.ty + 1) * s)

    @property
    def key(self) -> int:
        return tile_key(self.level, self.tx, self.ty)

    def parent(self) -> "TileId":
        if self.level == 0:
            raise ValueError("root has no parent")
        return TileId(self.level - 1, self.tx >> 1, self.ty >> 1)

    def children(self) -> list["TileId"]:
        return [
            TileId(self.level + 1, self.tx * 2 + dx, self.ty * 2 + dy)
            for dy in (0, 1)
            for dx in (0, 1)
        ]

    def neighbor(self, dx: int, dy: int) -> "TileId":
        return TileId(self.level, self.tx + dx, self.ty + dy)

    def __str__(self) -> str:
        return f"{self.level}/{self.tx}/{self.ty}"


def _part1by1(v: int) -> int:
    """Spread the low 29 bits of v so there is a zero bit between each."""
    v &= (1 << 29) - 1
    out = 0
    for i in range(29):
        out |= ((v >> i) & 1) << (2 * i)
    return out


def _compact1by1(v: int) -> int:
    out = 0
    for i in range(29):
        out |= ((v >> (2 * i)) & 1) << i
    return out


def morton2(tx: int, ty: int) -> int:
    """Interleave bits: tx on even bits, ty on odd bits."""
    return _part1by1(tx) | (_part1by1(ty) << 1)


def tile_key(level: int, tx: int, ty: int) -> int:
    """64-bit tile key: level in the top 6 bits, Morton code below.

    Sorting by key groups tiles by level and keeps spatial neighbours close,
    which is what the region pack directory relies on.
    """
    return (level << 58) | morton2(tx, ty)


def tile_from_key(key: int) -> TileId:
    level = key >> 58
    m = key & ((1 << 58) - 1)
    return TileId(level, _compact1by1(m), _compact1by1(m >> 1))


def tile_at(level: int, x: float, z: float) -> TileId:
    s = tile_size(level)
    return TileId(level, int(np.floor(x / s)), int(np.floor(z / s)))


def tiles_covering(level: int, x_min: float, z_min: float, x_max: float, z_max: float) -> Iterator[TileId]:
    """All tiles at ``level`` intersecting the half-open game-space box."""
    s = tile_size(level)
    n = 1 << level
    tx0 = max(0, int(np.floor(x_min / s)))
    ty0 = max(0, int(np.floor(z_min / s)))
    tx1 = min(n - 1, int(np.ceil(x_max / s)) - 1)
    ty1 = min(n - 1, int(np.ceil(z_max / s)) - 1)
    for ty in range(ty0, ty1 + 1):
        for tx in range(tx0, tx1 + 1):
            yield TileId(level, tx, ty)


def grid_coords(tile: TileId, n: int) -> tuple[np.ndarray, np.ndarray]:
    """Game-space X and Z of an ``n x n`` vertex grid on ``tile``.

    Sample ``(j, i)`` (row ``j`` from the south edge, column ``i`` from the west)
    sits at ``x0 + i*S/(n-1)``, ``z0 + j*S/(n-1)``, so the first and last rows and
    columns lie exactly on the tile edges and are shared with neighbours. ``n``
    must be ``2^k + 1``. Coordinates are computed from integers in a way that
    is bit-identical for both tiles that share an edge.
    """
    if n < 2 or (n - 1) & (n - 2):
        raise ValueError("grid size must be 2^k + 1")
    s = tile.size
    step = s / (n - 1)  # exact: power-of-two divided by power-of-two
    idx = np.arange(n, dtype=np.float64)
    xs = tile.x0 + idx * step
    zs = tile.z0 + idx * step
    return np.meshgrid(xs, zs)  # (n, n) arrays: [j, i]


def bbox_lonlat_to_game(lon_min: float, lat_min: float, lon_max: float, lat_max: float, densify: int = 16):
    """Game-space bounding box enclosing a lon/lat box (edges densified,
    because straight lon/lat edges are curves in TM84)."""
    t = np.linspace(0.0, 1.0, densify)
    lons = np.concatenate([lon_min + (lon_max - lon_min) * t, np.full(densify, lon_max),
                           lon_max - (lon_max - lon_min) * t, np.full(densify, lon_min)])
    lats = np.concatenate([np.full(densify, lat_min), lat_min + (lat_max - lat_min) * t,
                           np.full(densify, lat_max), lat_max - (lat_max - lat_min) * t])
    x, z = lonlat_to_game(lons, lats)
    return float(np.min(x)), float(np.min(z)), float(np.max(x)), float(np.max(z))


def scale_factor_at(lon: float, lat: float) -> float:
    """Point scale factor k of NPL-TM84 at a location (for docs and tests)."""
    f = tm84_crs()
    from pyproj import Proj

    return float(Proj(f).get_factors(lon, lat).meridional_scale)
