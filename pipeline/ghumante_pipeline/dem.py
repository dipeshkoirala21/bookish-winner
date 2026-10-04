"""Copernicus GLO-30 DEM: mosaic the COG tiles and sample heights in game space.

``DemSampler`` reads the 1 x 1 degree Copernicus GLO-30 tiles that cover a
bounding box into a single float32 array and answers height and slope queries
for game-space positions (``projection.game_to_lonlat`` then interpolation).

Grid convention
---------------
Copernicus tiles are ``AREA_OR_POINT=Point``: the GeoTIFF tie point is the
*centre* of the first pixel, which sits on a whole arc-second (pixel ``[0, 0]``
of ``N27_00_E085_00`` is centred on exactly 85 E, 28 N). GDAL shifts the
geotransform by half a pixel when it reads such a file, so the transform rasterio
reports is the usual pixel-*corner* one (left edge 84.999861). We therefore take
pixel centres as ``corner + 0.5 px`` for every file, Point or Area. We force
``GTIFF_POINT_GEO_IGNORE=FALSE`` while opening, so an environment that sets it
cannot silently move the whole DEM by 15 m.

All rasters live on a ``GeoGrid``: a north-up lon/lat pixel lattice addressed by
*global* integer pixel indices (``round(lon * 3600)`` for GLO-30). Sampling
coordinates are computed as ``lon * ppd - phase - col0``. Because ``col0`` is
an integer, the fractional pixel position (and so the interpolation weights)
depends only on ``lon``, not on where the mosaic starts. Two samplers built
over different boxes therefore return the same bilinear heights wherever both
hold the same source pixels.

Seam invariant
--------------
Every query here is a pure function of the queried position (and, for coarse
LODs, the quantised spacing). There is no dependence on which other points are
in the batch. ``scipy.ndimage.map_coordinates`` evaluates each point on its own,
and the pre-filtered copies are whole-mosaic arrays cached by spacing. This is
what makes the shared edge rows and columns of neighbouring ``HGHT`` tiles
bit-identical (docs/DATA_FORMATS.md section 1.1).

Coarse LODs
-----------
Sampling a 30 m DEM every 256 m point-samples (aliases) ridges and gullies.
When ``spacing_m`` quantised to a power of two, ``q``, exceeds 1.5 x the native
spacing, we sample a Gaussian pre-filtered copy of the mosaic instead, with
``sigma = 0.5 * q / pixel_size`` per axis, so coarse heights are area averages.
Kernel sizes use the pixel size at ``REFERENCE_LAT_DEG``, not the mosaic's own
mid-latitude, so the filtered heights do not depend on the mosaic extent either.
"""

from __future__ import annotations

import logging
import math
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Sequence

import numpy as np
import rasterio
from rasterio.windows import Window
from scipy import ndimage

from . import projection

log = logging.getLogger(__name__)

# Middle of Nepal (26.3-30.5 N). Pixel sizes at this latitude size the filter
# kernels and mode windows; cos(lat) varies by only +-2.5 % across the country.
REFERENCE_LAT_DEG = 28.0

# Pre-filter when the quantised spacing exceeds this multiple of the native
# spacing (64 m and coarser for GLO-30; 8, 16 and 32 m sample the raw DEM).
PREFILTER_THRESHOLD = 1.5

# Copernicus GLO-30 marks voids with large negative values in some releases.
DEM_MIN_VALID_M = -500.0
DEM_MAX_VALID_M = 9000.0

_VALID_ORDERS = (0, 1, 3)


# ---------------------------------------------------------------------------
# Geodesy helpers
# ---------------------------------------------------------------------------
def metres_per_degree(lat_deg: float) -> tuple[float, float]:
    """(metres per degree of longitude, of latitude) on WGS84 at ``lat_deg``."""
    p = math.radians(lat_deg)
    m_lat = 111132.954 - 559.822 * math.cos(2 * p) + 1.175 * math.cos(4 * p)
    m_lon = 111412.84 * math.cos(p) - 93.5 * math.cos(3 * p) + 0.118 * math.cos(5 * p)
    return m_lon, m_lat


def quantise_spacing(spacing_m: float) -> float:
    """Nearest power of two (in log space) to ``spacing_m``: 8, 16, 32, ... m."""
    if not spacing_m > 0:
        raise ValueError(f"spacing must be positive, got {spacing_m}")
    return float(2.0 ** math.floor(math.log2(spacing_m) + 0.5))


def _snap_ppd(ppd: float) -> float:
    r = round(ppd)
    return float(r) if abs(ppd - r) < 1e-6 * max(1.0, ppd) else ppd


def _snap_phase(x: float) -> float:
    """Fractional part of ``x`` with 0 / 0.5 snapped exactly (pixel-grid phase)."""
    ph = x - math.floor(x)
    half = round(ph * 2.0)
    if abs(ph * 2.0 - half) < 1e-6:
        ph = half / 2.0
    return ph % 1.0


# ---------------------------------------------------------------------------
# Global lon/lat pixel grid and windowed mosaicking (shared with landcover.py)
# ---------------------------------------------------------------------------
@dataclass(frozen=True)
class GeoGrid:
    """A north-up lon/lat pixel lattice anchored to global integer pixel indices.

    Continuous pixel coordinates are ``u = lon * ppd_x - phase_x`` (growing
    east) and ``v = -lat * ppd_y - phase_y`` (growing south). Global pixel ``k``
    covers ``u in [k, k + 1)`` and is centred on ``u = k + 0.5``. Array element
    ``[r, c]`` of a mosaic on this grid is global pixel ``(row0 + r, col0 + c)``.
    """

    ppd_x: float  # pixels per degree of longitude
    ppd_y: float  # pixels per degree of latitude
    phase_x: float  # pixel-corner phase in [0, 1)
    phase_y: float
    col0: int
    row0: int
    width: int
    height: int

    def uv(self, lon, lat) -> tuple[np.ndarray, np.ndarray]:
        lon = np.asarray(lon, dtype=np.float64)
        lat = np.asarray(lat, dtype=np.float64)
        return lon * self.ppd_x - self.phase_x, -lat * self.ppd_y - self.phase_y

    def centre_coords(self, lon, lat) -> tuple[np.ndarray, np.ndarray]:
        """Fractional (row, col) array coordinates, integers at pixel centres
        (the convention of ``scipy.ndimage.map_coordinates``)."""
        u, v = self.uv(lon, lat)
        return v - (self.row0 + 0.5), u - (self.col0 + 0.5)

    def pixel_index(self, lon, lat) -> tuple[np.ndarray, np.ndarray]:
        """Integer (row, col) array index of the pixel containing each point.
        Not clipped: points outside the mosaic give out-of-range indices."""
        u, v = self.uv(lon, lat)
        return (np.floor(v).astype(np.int64) - self.row0,
                np.floor(u).astype(np.int64) - self.col0)

    @property
    def bounds(self) -> tuple[float, float, float, float]:
        """Outer pixel edges (lon_min, lat_min, lon_max, lat_max)."""
        lon_min = (self.col0 + self.phase_x) / self.ppd_x
        lon_max = (self.col0 + self.width + self.phase_x) / self.ppd_x
        lat_max = -(self.row0 + self.phase_y) / self.ppd_y
        lat_min = -(self.row0 + self.height + self.phase_y) / self.ppd_y
        return lon_min, lat_min, lon_max, lat_max

    @property
    def mid_lat(self) -> float:
        b = self.bounds
        return 0.5 * (b[1] + b[3])

    def pixel_size_m(self, lat_deg: float | None = None) -> tuple[float, float]:
        """(east-west, north-south) pixel size in metres at ``lat_deg``
        (default: the mosaic's mid-latitude)."""
        m_lon, m_lat = metres_per_degree(self.mid_lat if lat_deg is None else lat_deg)
        return m_lon / self.ppd_x, m_lat / self.ppd_y


@dataclass(frozen=True)
class _SourceInfo:
    path: Path
    col0: int
    row0: int
    width: int
    height: int
    nodata: float | None


def _inspect_sources(paths: Sequence[Path]) -> tuple[list[_SourceInfo], float, float, float, float]:
    infos: list[_SourceInfo] = []
    ref: tuple[float, float, float, float] | None = None
    for p in sorted({Path(p) for p in paths}, key=str):
        with rasterio.open(p) as ds:
            if ds.crs is None or not ds.crs.is_geographic:
                raise ValueError(f"{p}: expected a geographic (EPSG:4326) raster, got {ds.crs}")
            t = ds.transform
            if t.b != 0 or t.d != 0 or t.a <= 0 or t.e >= 0:
                raise ValueError(f"{p}: only north-up, unrotated rasters are supported ({t})")
            ppd_x, ppd_y = _snap_ppd(1.0 / t.a), _snap_ppd(-1.0 / t.e)
            phase_x, phase_y = _snap_phase(t.c * ppd_x), _snap_phase(-t.f * ppd_y)
            cur = (ppd_x, ppd_y, phase_x, phase_y)
            if ref is None:
                ref = cur
            elif (abs(cur[0] - ref[0]) > 1e-6 * ref[0] or abs(cur[1] - ref[1]) > 1e-6 * ref[1]
                  or abs(cur[2] - ref[2]) > 1e-3 or abs(cur[3] - ref[3]) > 1e-3):
                raise ValueError(f"{p}: pixel grid {cur} does not match the other tiles {ref}")
            col0 = round(t.c * ref[0] - ref[2])
            row0 = round(-t.f * ref[1] - ref[3])
            infos.append(_SourceInfo(Path(p), col0, row0, ds.width, ds.height, ds.nodata))
    if ref is None:
        raise ValueError("no raster files given")
    infos.sort(key=lambda s: (s.row0, s.col0, str(s.path)))
    return infos, *ref


def read_geo_mosaic(
    paths: Sequence[Path],
    bbox_lonlat: tuple[float, float, float, float],
    margin_deg: float,
    dtype: np.dtype,
    fill_value: float,
    is_valid: Callable[[np.ndarray, float | None], np.ndarray],
) -> tuple[np.ndarray, np.ndarray, GeoGrid]:
    """Mosaic single-band lon/lat rasters over ``bbox_lonlat`` + ``margin_deg``.

    Only the window overlapping the box is read from each file (a 3 x 3 degree
    WorldCover tile is 1.3 GB in memory; we never load one whole). Files must
    share one pixel grid. Where files overlap the first one in (row, col, path)
    order wins, so the result does not depend on the order of ``paths``.

    Returns ``(array, valid_mask, grid)``; invalid pixels hold ``fill_value``.
    """
    lon_min, lat_min, lon_max, lat_max = bbox_lonlat
    if not (lon_min < lon_max and lat_min < lat_max):
        raise ValueError(f"degenerate bbox {bbox_lonlat}")
    with rasterio.Env(GTIFF_POINT_GEO_IGNORE=False):
        infos, ppd_x, ppd_y, phase_x, phase_y = _inspect_sources(paths)
        # One extra pixel on every side so bilinear interpolation anywhere in
        # the (margin-expanded) box has both neighbours.
        c0 = math.floor((lon_min - margin_deg) * ppd_x - phase_x) - 1
        c1 = math.floor((lon_max + margin_deg) * ppd_x - phase_x) + 1
        r0 = math.floor(-(lat_max + margin_deg) * ppd_y - phase_y) - 1
        r1 = math.floor(-(lat_min - margin_deg) * ppd_y - phase_y) + 1
        grid = GeoGrid(ppd_x, ppd_y, phase_x, phase_y, c0, r0, c1 - c0 + 1, r1 - r0 + 1)
        data = np.full((grid.height, grid.width), fill_value, dtype=dtype)
        valid = np.zeros((grid.height, grid.width), dtype=bool)
        for s in infos:
            a0, a1 = max(c0, s.col0), min(c1, s.col0 + s.width - 1)
            b0, b1 = max(r0, s.row0), min(r1, s.row0 + s.height - 1)
            if a0 > a1 or b0 > b1:
                continue
            win = Window(a0 - s.col0, b0 - s.row0, a1 - a0 + 1, b1 - b0 + 1)
            with rasterio.open(s.path) as ds:
                block = ds.read(1, window=win)
            ok = np.asarray(is_valid(block, s.nodata), dtype=bool)
            dst = data[b0 - r0:b1 - r0 + 1, a0 - c0:a1 - c0 + 1]
            dst_valid = valid[b0 - r0:b1 - r0 + 1, a0 - c0:a1 - c0 + 1]
            put = ok & ~dst_valid
            dst[put] = block[put].astype(dtype, copy=False)
            dst_valid |= ok
    return data, valid, grid


def _dem_valid(block: np.ndarray, nodata: float | None) -> np.ndarray:
    ok = np.isfinite(block) & (block > DEM_MIN_VALID_M) & (block < DEM_MAX_VALID_M)
    if nodata is not None and np.isfinite(nodata):
        ok &= block != nodata
    return ok


# ---------------------------------------------------------------------------
# DemSampler
# ---------------------------------------------------------------------------
class DemSampler:
    """Seamless height sampler over a Copernicus GLO-30 mosaic.

    ``data`` is float32, north-up (row 0 = northernmost), on ``grid``. Voids
    are already filled with the nearest valid height. Pre-filtered copies and
    spline coefficients are cached per quantised spacing and order; call
    ``clear_cache()`` to free them.
    """

    def __init__(self, data: np.ndarray, grid: GeoGrid, valid_fraction: float = 1.0) -> None:
        if data.shape != (grid.height, grid.width):
            raise ValueError(f"data shape {data.shape} does not match grid {grid.height}x{grid.width}")
        self.data = np.ascontiguousarray(data, dtype=np.float32)
        self.grid = grid
        self.valid_fraction = float(valid_fraction)
        self._filtered: dict[float, np.ndarray] = {}
        self._coeffs: dict[tuple[float | None, int], np.ndarray] = {}

    # --- construction -----------------------------------------------------
    @classmethod
    def from_files(cls, paths: Sequence[Path], bbox_lonlat: tuple[float, float, float, float],
                   margin_deg: float = 0.05) -> "DemSampler":
        """Mosaic the tiles in ``paths`` that overlap ``bbox_lonlat`` + margin.

        Voids (NaN, nodata, out-of-range values) and any part of the box no
        file covers are filled with the nearest valid height. A warning is
        logged when anything had to be filled.
        """
        data, valid, grid = read_geo_mosaic(paths, bbox_lonlat, margin_deg, np.float32, np.nan, _dem_valid)
        n_valid = int(valid.sum())
        if n_valid == 0:
            raise ValueError(f"no valid DEM data covers {bbox_lonlat} in {[str(p) for p in paths]}")
        frac = n_valid / valid.size
        if n_valid < valid.size:
            log.warning("DEM mosaic %s: %.3f%% of pixels are voids or uncovered; filling by nearest",
                        bbox_lonlat, 100.0 * (1.0 - frac))
            idx = ndimage.distance_transform_edt(~valid, return_distances=False, return_indices=True)
            data = data[idx[0], idx[1]]
        return cls(data, grid, frac)

    @staticmethod
    def copernicus_tile_names(bbox_lonlat: tuple[float, float, float, float],
                              margin_deg: float = 0.0) -> list[str]:
        """Names of the GLO-30 tiles whose pixel centres cover the box.

        Tile ``N{k}`` holds pixel centres with latitude in ``(k, k+1]`` and
        tile ``E{m}`` holds longitudes in ``[m, m+1)``, so a box ending exactly
        on 28 N needs no ``N28`` tile. Sorted south-west to north-east.
        """
        lon_min, lat_min, lon_max, lat_max = bbox_lonlat
        lon_min, lat_min = lon_min - margin_deg, lat_min - margin_deg
        lon_max, lat_max = lon_max + margin_deg, lat_max + margin_deg
        names = []
        for lat in range(math.ceil(lat_min) - 1, math.ceil(lat_max)):
            for lon in range(math.floor(lon_min), math.floor(lon_max) + 1):
                ns = f"{'N' if lat >= 0 else 'S'}{abs(lat):02d}"
                ew = f"{'E' if lon >= 0 else 'W'}{abs(lon):03d}"
                names.append(f"Copernicus_DSM_COG_10_{ns}_00_{ew}_00_DEM")
        return names

    # --- metrics --------------------------------------------------------------
    def native_spacing_m(self) -> float:
        """Geometric-mean pixel size in metres at the mosaic's mid-latitude
        (about 29 m for GLO-30 in Nepal: 27.3 m east-west, 30.8 m north-south)."""
        dx, dy = self.grid.pixel_size_m()
        return math.sqrt(dx * dy)

    def _reference_pixel_m(self) -> tuple[float, float]:
        return self.grid.pixel_size_m(REFERENCE_LAT_DEG)

    def filter_scale(self, spacing_m: float | None) -> float | None:
        """The quantised spacing whose pre-filtered copy ``spacing_m`` samples,
        or None when it samples the raw mosaic."""
        if spacing_m is None:
            return None
        q = quantise_spacing(spacing_m)
        dx, dy = self._reference_pixel_m()
        return q if q > PREFILTER_THRESHOLD * math.sqrt(dx * dy) else None

    # --- cached derived rasters ----------------------------------------------
    def _filtered_copy(self, q: float) -> np.ndarray:
        arr = self._filtered.get(q)
        if arr is None:
            dx, dy = self._reference_pixel_m()
            sigma = (0.5 * q / dy, 0.5 * q / dx)  # (rows, cols)
            arr = ndimage.gaussian_filter(self.data, sigma=sigma, mode="nearest", truncate=4.0,
                                          output=np.float32)
            self._filtered[q] = arr
        return arr

    def _source(self, order: int, q: float | None) -> np.ndarray:
        base = self.data if q is None else self._filtered_copy(q)
        if order <= 1:
            return base
        key = (q, order)
        coeffs = self._coeffs.get(key)
        if coeffs is None:
            coeffs = ndimage.spline_filter(base, order=order, output=np.float32, mode="nearest")
            self._coeffs[key] = coeffs
        return coeffs

    def clear_cache(self) -> None:
        self._filtered.clear()
        self._coeffs.clear()

    # --- sampling -----------------------------------------------------------------
    def sample_lonlat(self, lon, lat, order: int = 1, spacing_m: float | None = None) -> np.ndarray:
        """Heights (float64 metres) at lon/lat points of any (broadcastable) shape.

        ``order`` 1 is bilinear and 3 cubic B-spline (0 nearest). Points outside
        the mosaic take the nearest edge value. ``spacing_m`` selects the
        pre-filtered copy exactly as ``sample_game`` does. Non-finite inputs
        give NaN.
        """
        if order not in _VALID_ORDERS:
            raise ValueError(f"order must be one of {_VALID_ORDERS}, got {order}")
        lon, lat = np.broadcast_arrays(np.asarray(lon, dtype=np.float64), np.asarray(lat, dtype=np.float64))
        shape = lon.shape
        src = self._source(order, self.filter_scale(spacing_m))
        row, col = self.grid.centre_coords(lon.ravel(), lat.ravel())
        finite = np.isfinite(row) & np.isfinite(col)
        out = np.full(row.shape, np.nan, dtype=np.float64)
        if finite.any():
            coords = np.stack([row[finite], col[finite]])
            out[finite] = ndimage.map_coordinates(src, coords, order=order, mode="nearest",
                                                  prefilter=False, output=np.float64)
        return out.reshape(shape)

    def heights_at_lonlat(self, lonlat: np.ndarray, order: int = 1) -> np.ndarray:
        """Heights for an ``(..., 2)`` array of (lon, lat) pairs. This is the
        ``sample_elev`` callable that ``trails.assign_trail_difficulty`` takes."""
        ll = np.asarray(lonlat, dtype=np.float64)
        if ll.shape[-1:] != (2,):
            raise ValueError(f"expected an (..., 2) lon/lat array, got shape {ll.shape}")
        return self.sample_lonlat(ll[..., 0], ll[..., 1], order=order)

    def sample_game(self, x, z, spacing_m: float | None = None, order: int = 1) -> np.ndarray:
        """Heights at game-space points. With ``spacing_m`` (the grid spacing the
        heights are for) coarse LODs sample the area-averaged pre-filtered copy.

        The result for a given ``(x, z, spacing_m, order)`` never depends on the
        other points in the call, so tiles sharing an edge agree bit for bit.
        """
        x, z = np.broadcast_arrays(np.asarray(x, dtype=np.float64), np.asarray(z, dtype=np.float64))
        lon, lat = projection.game_to_lonlat(x, z)
        return self.sample_lonlat(np.asarray(lon), np.asarray(lat), order=order, spacing_m=spacing_m)

    def slope_deg_game(self, x, z, spacing_m: float, order: int = 1) -> np.ndarray:
        """Terrain slope in degrees from central differences over +-``spacing_m``.

        Each slope is computed from four fresh samples around the point (never
        from a tile's own height grid with one-sided edges), so it is a pure
        function of position and agrees across tile edges.
        """
        d = float(spacing_m)
        if not d > 0:
            raise ValueError(f"spacing must be positive, got {spacing_m}")
        x, z = np.broadcast_arrays(np.asarray(x, dtype=np.float64), np.asarray(z, dtype=np.float64))
        xs = np.stack([x + d, x - d, x, x])
        zs = np.stack([z, z, z + d, z - d])
        h = self.sample_game(xs, zs, spacing_m=d, order=order)
        gx = (h[0] - h[1]) / (2.0 * d)
        gz = (h[2] - h[3]) / (2.0 * d)
        return np.degrees(np.arctan(np.hypot(gx, gz)))
