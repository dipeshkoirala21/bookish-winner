"""Per-road and per-building context: settlement density, elevation, slope, zone.

The surface model (``surface.py``) conditions on a ``RoadContext`` and the
building inference (``buildings.py``) on a ``BuildingContext``. Both are
computed here, vectorised, so the 515 k buildings and ~100 k roads of the
Kathmandu Valley take seconds:

* **Settlement density** (buildings per hectare) comes from a ``DensityGrid``:
  building centroids are counted on a ``cell_m`` grid anchored to multiples of
  the cell size in game space, and the count is box-summed over the
  ``(2r+1) x (2r+1)`` cells around the query cell (``r = round(radius_m /
  cell_m)``) with an integral image. The default 50 m cells and 300 m radius
  give a 650 m square window (42.25 ha). Integer counts make it exact and
  independent of the order of the buildings.
* **Roads** are characterised at their middle: the point at half the
  polyline's game-space length. Elevation is the bilinear DEM height there and
  slope the DEM central-difference slope over ``ROAD_SLOPE_SPACING_M``.
* **Buildings** use the mean vertex of the outer ring (as
  ``buildings.building_centroids``), the DEM height there and the archetype
  zone from ``config/archetype_zones.yaml``.
"""

from __future__ import annotations

from typing import Sequence

import numpy as np

from . import projection
from .buildings import ArchetypeZones, BuildingContext
from .dem import DemSampler
from .model import BuildingFeature, RoadFeature
from .surface import DENSITY_EDGES, ELEV_EDGES, SLOPE_EDGES, RoadContext

DENSITY_CELL_M = 50.0
DENSITY_RADIUS_M = 300.0
ROAD_SLOPE_SPACING_M = 30.0  # about one GLO-30 pixel


# ---------------------------------------------------------------------------
# Vectorised helpers
# ---------------------------------------------------------------------------
def concat_points(arrays: Sequence[np.ndarray]) -> tuple[np.ndarray, np.ndarray]:
    """Concatenate (N_i, 2) arrays: returns ``(points, offsets)`` with
    ``offsets`` of length ``len(arrays) + 1`` (CSR style)."""
    counts = np.fromiter((len(a) for a in arrays), dtype=np.int64, count=len(arrays))
    offsets = np.zeros(len(arrays) + 1, dtype=np.int64)
    np.cumsum(counts, out=offsets[1:])
    if not len(arrays) or offsets[-1] == 0:
        return np.zeros((0, 2), dtype=np.float64), offsets
    pts = np.concatenate([np.asarray(a, dtype=np.float64).reshape(-1, 2) for a in arrays])
    return pts, offsets


def project_lonlat_arrays(arrays: Sequence[np.ndarray]) -> list[np.ndarray]:
    """Project many (N, 2) lon/lat arrays to game metres in one pyproj call."""
    pts, off = concat_points(arrays)
    if len(pts) == 0:
        return [np.zeros((0, 2), dtype=np.float64) for _ in arrays]
    x, z = projection.lonlat_to_game(pts[:, 0], pts[:, 1])
    g = np.stack([np.asarray(x, dtype=np.float64), np.asarray(z, dtype=np.float64)], axis=1)
    return [g[off[i]:off[i + 1]] for i in range(len(arrays))]


def polyline_midpoints(lines: Sequence[np.ndarray]) -> np.ndarray:
    """(N, 2) point at half the planar length of each polyline (vectorised).

    A single-vertex or zero-length polyline gives its first vertex; an empty
    one gives NaN."""
    n = len(lines)
    out = np.full((n, 2), np.nan, dtype=np.float64)
    if n == 0:
        return out
    pts, off = concat_points(lines)
    counts = np.diff(off)
    has = counts > 0
    out[has] = pts[off[:-1][has]]
    if len(pts) < 2:
        return out
    seg = np.hypot(*(pts[1:] - pts[:-1]).T)
    # Zero the "segments" that join the last vertex of one line to the next line.
    valid_seg = np.ones(len(seg), dtype=bool)
    ends = off[1:-1] - 1
    valid_seg[ends[(ends >= 0) & (ends < len(seg))]] = False
    seg = np.where(valid_seg, seg, 0.0)
    cum = np.concatenate([[0.0], np.cumsum(seg)])  # cum[k] = length up to vertex k
    multi = counts >= 2
    idx = np.flatnonzero(multi)
    if not len(idx):
        return out
    start, end = off[idx], off[idx + 1] - 1  # first and last vertex
    total = cum[end] - cum[start]
    target = cum[start] + 0.5 * total
    k = np.searchsorted(cum, target, side="right") - 1  # vertex at or before the target
    k = np.clip(k, start, np.maximum(start, end - 1))
    seg_len = cum[k + 1] - cum[k]
    t = np.where(seg_len > 0, (target - cum[k]) / np.where(seg_len > 0, seg_len, 1.0), 0.0)
    t = np.clip(t, 0.0, 1.0)
    mid = pts[k] + (pts[k + 1] - pts[k]) * t[:, None]
    out[idx] = np.where((total > 0)[:, None], mid, pts[start])
    return out


def _bins(values: np.ndarray, edges: tuple[float, ...]) -> np.ndarray:
    """Vectorised ``surface._bin``: a value equal to an edge is in the upper bin; NaN -> 0."""
    v = np.asarray(values, dtype=np.float64)
    b = np.searchsorted(np.asarray(edges, dtype=np.float64), v, side="right")
    return np.where(np.isfinite(v), b, 0).astype(np.int64)


def density_bins(per_ha) -> np.ndarray:
    return _bins(per_ha, DENSITY_EDGES)


def elev_bins(elev_m) -> np.ndarray:
    return _bins(elev_m, ELEV_EDGES)


def slope_bins(slope_deg) -> np.ndarray:
    return _bins(slope_deg, SLOPE_EDGES)


# ---------------------------------------------------------------------------
# Density
# ---------------------------------------------------------------------------
class DensityGrid:
    """Buildings per hectare in a square window around a point (see module docstring)."""

    def __init__(self, x, z, cell_m: float = DENSITY_CELL_M, radius_m: float = DENSITY_RADIUS_M) -> None:
        if not cell_m > 0 or radius_m < 0:
            raise ValueError("cell_m must be positive and radius_m non-negative")
        self.cell_m = float(cell_m)
        self.r = int(round(radius_m / cell_m))
        side = (2 * self.r + 1) * self.cell_m
        self.window_ha = side * side / 10_000.0
        x = np.asarray(x, dtype=np.float64).reshape(-1)
        z = np.asarray(z, dtype=np.float64).reshape(-1)
        ok = np.isfinite(x) & np.isfinite(z)
        ix = np.floor(x[ok] / self.cell_m).astype(np.int64)
        iz = np.floor(z[ok] / self.cell_m).astype(np.int64)
        if len(ix) == 0:
            self.ix0 = self.iz0 = 0
            self.sums = np.zeros((1, 1), dtype=np.int64)
            return
        r = self.r
        self.ix0, self.iz0 = int(ix.min()) - r, int(iz.min()) - r
        nx, nz = int(ix.max()) + r - self.ix0 + 1, int(iz.max()) + r - self.iz0 + 1
        counts = np.zeros((nz, nx), dtype=np.int64)
        np.add.at(counts, (iz - self.iz0, ix - self.ix0), 1)
        # Integral image with a zero row/column, then the (2r+1)^2 box sum per cell.
        ii = np.zeros((nz + 1, nx + 1), dtype=np.int64)
        ii[1:, 1:] = counts.cumsum(0).cumsum(1)
        jz = np.arange(nz)
        jx = np.arange(nx)
        z0, z1 = np.clip(jz - r, 0, nz), np.clip(jz + r + 1, 0, nz)
        x0, x1 = np.clip(jx - r, 0, nx), np.clip(jx + r + 1, 0, nx)
        self.sums = (ii[z1][:, x1] - ii[z0][:, x1] - ii[z1][:, x0] + ii[z0][:, x0])

    def count_at(self, x, z) -> np.ndarray:
        """Buildings in the window around each point (0 outside the grid; NaN input -> 0)."""
        x = np.asarray(x, dtype=np.float64)
        z = np.asarray(z, dtype=np.float64)
        x, z = np.broadcast_arrays(x, z)
        out = np.zeros(x.shape, dtype=np.int64)
        ok = np.isfinite(x) & np.isfinite(z)
        ix = np.floor(x[ok] / self.cell_m).astype(np.int64) - self.ix0
        iz = np.floor(z[ok] / self.cell_m).astype(np.int64) - self.iz0
        nz, nx = self.sums.shape
        inside = (ix >= 0) & (ix < nx) & (iz >= 0) & (iz < nz)
        vals = np.zeros(len(ix), dtype=np.int64)
        vals[inside] = self.sums[iz[inside], ix[inside]]
        out[ok] = vals
        return out

    def per_ha(self, x, z) -> np.ndarray:
        return self.count_at(x, z) / self.window_ha


def building_centroids_game(buildings: Sequence[BuildingFeature]) -> tuple[np.ndarray, np.ndarray, np.ndarray, np.ndarray]:
    """Mean outer-ring vertex of each building: ``(lon, lat, x, z)`` (vectorised)."""
    if not buildings:
        e = np.zeros(0, dtype=np.float64)
        return e, e, e, e
    pts, off = concat_points([b.outer for b in buildings])
    counts = np.diff(off)
    lon = np.add.reduceat(pts[:, 0], off[:-1]) / counts
    lat = np.add.reduceat(pts[:, 1], off[:-1]) / counts
    x, z = projection.lonlat_to_game(lon, lat)
    return lon, lat, np.asarray(x, dtype=np.float64), np.asarray(z, dtype=np.float64)


def density_grid_for(buildings: Sequence[BuildingFeature], cell_m: float = DENSITY_CELL_M,
                     radius_m: float = DENSITY_RADIUS_M) -> DensityGrid:
    _, _, x, z = building_centroids_game(buildings)
    return DensityGrid(x, z, cell_m, radius_m)


# ---------------------------------------------------------------------------
# Contexts
# ---------------------------------------------------------------------------
def road_context_arrays(roads: Sequence[RoadFeature], dem: DemSampler | None, density: DensityGrid,
                        roads_game: Sequence[np.ndarray] | None = None) -> dict[str, np.ndarray]:
    """Midpoint, buildings/ha, elevation and slope per road (arrays, same order)."""
    game = list(roads_game) if roads_game is not None else project_lonlat_arrays([r.lonlat for r in roads])
    mid = polyline_midpoints(game)
    dens = density.per_ha(mid[:, 0], mid[:, 1])
    if dem is not None and len(roads):
        elev = dem.sample_game(mid[:, 0], mid[:, 1])
        slope = dem.slope_deg_game(mid[:, 0], mid[:, 1], ROAD_SLOPE_SPACING_M)
    else:
        elev = np.full(len(roads), np.nan)
        slope = np.full(len(roads), np.nan)
    return {"mid_x": mid[:, 0], "mid_z": mid[:, 1], "per_ha": dens, "elev_m": elev, "slope_deg": slope}


def road_contexts(roads: Sequence[RoadFeature], dem: DemSampler | None, density: DensityGrid,
                  roads_game: Sequence[np.ndarray] | None = None) -> list[RoadContext]:
    """``surface.RoadContext`` for every road (density, elevation and slope bins at its middle)."""
    a = road_context_arrays(roads, dem, density, roads_game)
    db, eb, sb = density_bins(a["per_ha"]), elev_bins(a["elev_m"]), slope_bins(a["slope_deg"])
    return [RoadContext(int(d), int(e), int(s)) for d, e, s in zip(db.tolist(), eb.tolist(), sb.tolist())]


def building_contexts(buildings: Sequence[BuildingFeature], dem: DemSampler | None, zones: ArchetypeZones,
                      density: DensityGrid | None = None) -> list[BuildingContext]:
    """``buildings.BuildingContext`` for every building: density bin around its
    centroid, DEM elevation there and archetype zone. ``density`` defaults to a
    grid built from these buildings."""
    if not buildings:
        return []
    lon, lat, x, z = building_centroids_game(buildings)
    if density is None:
        density = DensityGrid(x, z)
    dbin = density_bins(density.per_ha(x, z))
    elev = dem.sample_lonlat(lon, lat) if dem is not None else np.full(len(buildings), np.nan)
    zone = zones.zone_of(lon, lat)
    return [BuildingContext(int(d), float(e), str(zn)) for d, e, zn in zip(dbin.tolist(), elev.tolist(), zone.tolist())]
