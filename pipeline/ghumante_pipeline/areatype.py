"""Area-type grid: the 250 m cells of docs/research/w2/street_life.md 1.1 and roads.md 1.2.

Every 250 m game-space cell (aligned to multiples of 250 m, so the grid is the
same in every region and on both sides of a tile border) gets one
``model.AreaType``, first match wins:

1. **OLD_CORE**: the cell centre lies in a curated core (``style_zones.yaml``
   zones with ``old_core: true``), or building coverage >= 0.45 with >= 180
   buildings in the cell.
2. **URBAN**: coverage >= 0.22.
3. **FOREST**: tree share >= 0.5 and coverage < 0.05 (tree share is the larger
   of the OSM FOREST area share and the WorldCover tree-cover share, sampled
   on a 5 x 5 point lattice per cell).
4. **PERI_URBAN**: coverage >= 0.06.
5. **RURAL**: everything else.
6. **HILL** (roads.md 1.2) replaces PERI_URBAN, RURAL and FOREST where the DSM
   at the cell centre is above 1,650 m or the steepest gradient to one of the
   8 neighbouring cell centres exceeds 12 %.

Coverage is the summed footprint area of the buildings whose outer-ring
centroid falls in the cell, divided by the cell area (62,500 m2); building
parts do not count. The grid feeds ``RATR.area_type`` (road pieces: the cell
under the piece's length midpoint) and ``BFNT.area_type`` (buildings: the
cell under the centroid), and through them the runtime ``AreaTypeGrid``
(W2_DESIGN 10.3), which takes a majority vote per cell.
"""

from __future__ import annotations

import math
from dataclasses import dataclass
from typing import Callable, Sequence

import numpy as np
import shapely

from .model import AreaType

CELL_M = 250.0
OLD_CORE_COVERAGE = 0.45
OLD_CORE_MIN_BUILDINGS = 180
URBAN_COVERAGE = 0.22
PERI_URBAN_COVERAGE = 0.06
FOREST_SHARE = 0.5
FOREST_MAX_COVERAGE = 0.05
HILL_MIN_ELEV_M = 1650.0
HILL_MIN_GRADIENT = 0.12
SAMPLES_PER_AXIS = 5


@dataclass
class AreaTypeGrid:
    """Cells ``(i, j)`` cover ``[x0 + i*cell, x0 + (i+1)*cell) x [z0 + j*cell, ...)`` in game metres."""

    x0: float
    z0: float
    nx: int
    nz: int
    types: np.ndarray  # (nz, nx) uint8 model.AreaType
    coverage: np.ndarray  # (nz, nx) float64
    counts: np.ndarray  # (nz, nx) int64
    cell: float = CELL_M

    def index(self, x, z) -> tuple[np.ndarray, np.ndarray]:
        i = np.floor((np.asarray(x, dtype=np.float64) - self.x0) / self.cell).astype(np.int64)
        j = np.floor((np.asarray(z, dtype=np.float64) - self.z0) / self.cell).astype(np.int64)
        return i, j

    def at(self, x, z):
        """Area type at game ``(x, z)`` (``UNKNOWN`` outside the grid); scalar or array."""
        i, j = self.index(x, z)
        ok = (i >= 0) & (i < self.nx) & (j >= 0) & (j < self.nz)
        out = np.zeros(np.shape(i), dtype=np.uint8)
        out[ok] = self.types[j[ok], i[ok]]
        return int(out) if np.ndim(out) == 0 else out

    def counts_by_type(self) -> dict[str, int]:
        vals, cnt = np.unique(self.types, return_counts=True)
        return {AreaType(int(v)).name: int(c) for v, c in zip(vals, cnt)}


def ring_area_centroid(ring: np.ndarray) -> tuple[float, float, float]:
    """Absolute area and centroid of an open (N, 2) ring in game metres."""
    r = np.asarray(ring, dtype=np.float64)
    if len(r) < 3:
        return 0.0, float(r[:, 0].mean()) if len(r) else 0.0, float(r[:, 1].mean()) if len(r) else 0.0
    o = r[0]
    p = r - o
    q = np.roll(p, -1, axis=0)
    cross = p[:, 0] * q[:, 1] - q[:, 0] * p[:, 1]
    a2 = float(cross.sum())
    if a2 == 0.0:
        return 0.0, float(r[:, 0].mean()), float(r[:, 1].mean())
    cx = float(((p[:, 0] + q[:, 0]) * cross).sum()) / (3.0 * a2) + o[0]
    cz = float(((p[:, 1] + q[:, 1]) * cross).sum()) / (3.0 * a2) + o[1]
    return abs(a2) * 0.5, cx, cz


def grid_bounds(box: Sequence[float], margin_cells: int = 1, cell: float = CELL_M) -> tuple[float, float, int, int]:
    """Cell-aligned origin and size covering a game box plus a margin."""
    x0 = math.floor(box[0] / cell) * cell - margin_cells * cell
    z0 = math.floor(box[1] / cell) * cell - margin_cells * cell
    nx = int(math.ceil((box[2] - x0) / cell)) + margin_cells
    nz = int(math.ceil((box[3] - z0) / cell)) + margin_cells
    return x0, z0, max(nx, 1), max(nz, 1)


def build_grid(box: Sequence[float], building_areas: np.ndarray, building_xz: np.ndarray, *,
               core_geoms: Sequence[object] = (), forest_geom: object | None = None,
               tree_share: Callable[[np.ndarray, np.ndarray], np.ndarray] | None = None,
               elevation: Callable[[np.ndarray, np.ndarray], np.ndarray] | None = None,
               cell: float = CELL_M) -> AreaTypeGrid:
    """Classify every cell over ``box`` (game ``x0, z0, x1, z1``).

    ``building_areas`` (N,) footprint areas and ``building_xz`` (N, 2)
    centroids, in game metres. ``core_geoms`` are game-space shapely polygons
    of the curated cores; ``forest_geom`` one (multi)polygon of OSM forest;
    ``tree_share(x, z)`` returns 1.0 where land cover is tree cover (else 0);
    ``elevation(x, z)`` the DSM in metres. Missing inputs are skipped.
    """
    x0, z0, nx, nz = grid_bounds(box, cell=cell)
    counts = np.zeros((nz, nx), dtype=np.int64)
    area = np.zeros((nz, nx), dtype=np.float64)
    if len(building_xz):
        xz = np.asarray(building_xz, dtype=np.float64).reshape(-1, 2)
        i = np.floor((xz[:, 0] - x0) / cell).astype(np.int64)
        j = np.floor((xz[:, 1] - z0) / cell).astype(np.int64)
        ok = (i >= 0) & (i < nx) & (j >= 0) & (j < nz)
        np.add.at(counts, (j[ok], i[ok]), 1)
        np.add.at(area, (j[ok], i[ok]), np.asarray(building_areas, dtype=np.float64)[ok])
    coverage = area / (cell * cell)

    cx = x0 + (np.arange(nx) + 0.5) * cell
    cz = z0 + (np.arange(nz) + 0.5) * cell
    gx, gz = np.meshgrid(cx, cz)

    core = np.zeros((nz, nx), dtype=bool)
    for g in core_geoms:
        core |= shapely.contains_xy(g, gx, gz)

    # Tree share on a 5 x 5 lattice per cell.
    off = (np.arange(SAMPLES_PER_AXIS) + 0.5) / SAMPLES_PER_AXIS * cell - 0.5 * cell
    ox, oz = np.meshgrid(off, off)
    sx = (gx[:, :, None] + ox.reshape(1, 1, -1)).reshape(-1)
    sz = (gz[:, :, None] + oz.reshape(1, 1, -1)).reshape(-1)
    share = np.zeros(sx.shape[0], dtype=np.float64)
    if forest_geom is not None and not forest_geom.is_empty:
        shapely.prepare(forest_geom)
        share = np.maximum(share, shapely.contains_xy(forest_geom, sx, sz).astype(np.float64))
    if tree_share is not None:
        share = np.maximum(share, np.asarray(tree_share(sx, sz), dtype=np.float64))
    share = share.reshape(nz, nx, -1).mean(axis=2)

    types = np.full((nz, nx), int(AreaType.RURAL), dtype=np.uint8)
    peri = coverage >= PERI_URBAN_COVERAGE
    types[peri] = AreaType.PERI_URBAN
    forest = (share >= FOREST_SHARE) & (coverage < FOREST_MAX_COVERAGE)
    types[forest] = AreaType.FOREST
    types[coverage >= URBAN_COVERAGE] = AreaType.URBAN
    old = core | ((coverage >= OLD_CORE_COVERAGE) & (counts >= OLD_CORE_MIN_BUILDINGS))
    types[old] = AreaType.OLD_CORE

    if elevation is not None:
        h = np.asarray(elevation(gx.reshape(-1), gz.reshape(-1)), dtype=np.float64).reshape(nz, nx)
        h = np.where(np.isfinite(h), h, np.nan)
        grad = np.zeros((nz, nx), dtype=np.float64)
        pad = np.pad(h, 1, mode="edge")
        for dj in (-1, 0, 1):
            for di in (-1, 0, 1):
                if di == 0 and dj == 0:
                    continue
                nb = pad[1 + dj:1 + dj + nz, 1 + di:1 + di + nx]
                d = cell * math.hypot(di, dj)
                g = np.abs(nb - h) / d
                grad = np.fmax(grad, np.where(np.isfinite(g), g, 0.0))
        hill = ((h > HILL_MIN_ELEV_M) | (grad > HILL_MIN_GRADIENT)) & np.isin(
            types, (int(AreaType.PERI_URBAN), int(AreaType.RURAL), int(AreaType.FOREST)))
        types[hill] = AreaType.HILL
    return AreaTypeGrid(x0=x0, z0=z0, nx=nx, nz=nz, types=types, coverage=coverage, counts=counts, cell=cell)
