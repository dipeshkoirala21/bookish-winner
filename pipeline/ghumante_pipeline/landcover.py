"""ESA WorldCover 2021 v200 land cover: windowed mosaic and game-space sampling.

WorldCover ships 3 x 3 degree tiles of uint8 class codes (``model.WorldCover``)
at 1/12000 degree (about 8.2 x 9.2 m in Nepal), pixel-area convention, nodata 0.
One tile is 36000 x 36000 pixels (1.3 GB in memory), so ``from_files`` only
reads the window covering the bounding box plus a margin, using the shared
``dem.read_geo_mosaic`` / ``dem.GeoGrid`` machinery.

Queries are pure functions of position, so tiles that share an edge agree:

* ``sample_lonlat`` / ``sample_game`` without a spacing (or with one up to 3x
  the native spacing) return the class of the pixel containing the point.
* Coarser spacings return a deterministic **mode** instead of a point sample.
  The mosaic is cut into blocks of about ``q/2`` metres (``q`` = spacing rounded
  to a power of two). Blocks are anchored to global pixel indices, not to the
  mosaic origin. Per-class pixel counts are box-summed over 3 x 3 blocks, which
  gives a window of about 1.5 ``q`` centred on the block holding the point.
  The class with the most pixels wins, ties go to the lowest class code, and
  nodata pixels do not vote.
* ``built_up_fraction_game`` returns the share of built-up (class 50) pixels
  among valid pixels in a square window of half-width ``radius_m`` (an
  approximation of the disk). It is computed on blocks of about ``radius/8`` and
  interpolated bilinearly between block centres.

Block and window sizes use pixel sizes at ``dem.REFERENCE_LAT_DEG``, so results
do not depend on the mosaic extent either.
"""

from __future__ import annotations

import math
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Sequence

import numpy as np
from scipy import ndimage

from . import projection
from .dem import REFERENCE_LAT_DEG, GeoGrid, quantise_spacing, read_geo_mosaic
from .model import WorldCover

# Valid class codes in ascending order; argmax over this axis breaks ties
# towards the lowest code.
CLASS_CODES = np.array([int(c) for c in WorldCover if c != WorldCover.NODATA], dtype=np.uint8)
BUILT_UP = int(WorldCover.BUILT_UP)

# Use the windowed mode when the quantised spacing exceeds this multiple of the
# native spacing (32 m and coarser in Nepal; 8 and 16 m sample the pixel).
MODE_THRESHOLD = 3.0

_STRIP_PIXELS = 1 << 23  # bound on temporary arrays while counting blocks


_VALID_LUT = np.zeros(256, dtype=bool)
_VALID_LUT[CLASS_CODES] = True


def _wc_valid(block: np.ndarray, nodata: float | None) -> np.ndarray:
    return _VALID_LUT[block]  # a lookup table avoids np.isin's large temporaries


@dataclass(frozen=True)
class _BlockRaster:
    """A raster of per-block values; block ``(by, bx)`` covers global pixel rows
    ``[by*fy, (by+1)*fy)`` and columns ``[bx*fx, (bx+1)*fx)``."""

    values: np.ndarray
    by0: int
    bx0: int
    fy: int
    fx: int


def _box_sum(a: np.ndarray, ry: int, rx: int) -> np.ndarray:
    """Integer sum over a ``(2ry+1) x (2rx+1)`` window, zero outside.

    Separable; ndimage accumulates in float64, which is exact for pixel counts
    (any mosaic has far fewer than 2^31 pixels)."""
    out = np.asarray(a, dtype=np.int32)
    if ry:
        out = ndimage.correlate1d(out, np.ones(2 * ry + 1), axis=0, output=np.int32, mode="constant")
    if rx:
        out = ndimage.correlate1d(out, np.ones(2 * rx + 1), axis=1, output=np.int32, mode="constant")
    return out


class LandcoverSampler:
    """Nearest / mode / built-up-fraction sampler over a WorldCover mosaic.

    ``data`` is uint8, north-up, on ``grid`` (``dem.GeoGrid``); 0 means no data.
    Derived block rasters are cached per quantised spacing or radius;
    ``clear_cache()`` frees them.
    """

    def __init__(self, data: np.ndarray, grid: GeoGrid) -> None:
        if data.shape != (grid.height, grid.width):
            raise ValueError(f"data shape {data.shape} does not match grid {grid.height}x{grid.width}")
        self.data = np.ascontiguousarray(data, dtype=np.uint8)
        self.grid = grid
        self.valid_fraction = float(np.count_nonzero(self.data)) / max(1, self.data.size)
        self._modes: dict[float, _BlockRaster] = {}
        self._built: dict[float, _BlockRaster] = {}

    # --- construction ---------------------------------------------------------
    @classmethod
    def from_files(cls, paths: Sequence[Path], bbox_lonlat: tuple[float, float, float, float],
                   margin_deg: float = 0.02) -> "LandcoverSampler":
        """Read the window of each tile overlapping ``bbox_lonlat`` + margin.
        Uncovered areas and unknown codes become 0 (no data)."""
        data, _valid, grid = read_geo_mosaic(paths, bbox_lonlat, margin_deg, np.uint8, 0, _wc_valid)
        return cls(data, grid)

    @staticmethod
    def worldcover_tile_names(bbox_lonlat: tuple[float, float, float, float],
                              margin_deg: float = 0.0) -> list[str]:
        """Names of the 3 x 3 degree tiles covering the box, e.g.
        ``ESA_WorldCover_10m_2021_v200_N27E084_Map``. A tile named ``N27``
        holds latitudes in ``(27, 30]`` (pixel-area convention: the top row ends
        exactly on 30 N), longitudes ``[84, 87)``. Sorted south-west first."""
        lon_min, lat_min, lon_max, lat_max = bbox_lonlat
        lon_min, lat_min = lon_min - margin_deg, lat_min - margin_deg
        lon_max, lat_max = lon_max + margin_deg, lat_max + margin_deg
        lat_lo = 3 * math.ceil(lat_min / 3) - 3
        lat_hi = 3 * math.ceil(lat_max / 3) - 3
        lon_lo = 3 * math.floor(lon_min / 3)
        lon_hi = 3 * math.floor(lon_max / 3)
        names = []
        for lat in range(lat_lo, lat_hi + 1, 3):
            for lon in range(lon_lo, lon_hi + 1, 3):
                ns = f"{'N' if lat >= 0 else 'S'}{abs(lat):02d}"
                ew = f"{'E' if lon >= 0 else 'W'}{abs(lon):03d}"
                names.append(f"ESA_WorldCover_10m_2021_v200_{ns}{ew}_Map")
        return names

    # --- metrics ----------------------------------------------------------------
    def native_spacing_m(self) -> float:
        """Geometric-mean pixel size in metres at the mosaic's mid-latitude."""
        dx, dy = self.grid.pixel_size_m()
        return math.sqrt(dx * dy)

    def _reference_pixel_m(self) -> tuple[float, float]:
        return self.grid.pixel_size_m(REFERENCE_LAT_DEG)

    def mode_scale(self, spacing_m: float | None) -> float | None:
        """Quantised spacing whose mode raster ``spacing_m`` samples, or None for
        the nearest pixel."""
        if spacing_m is None:
            return None
        q = quantise_spacing(spacing_m)
        dx, dy = self._reference_pixel_m()
        return q if q > MODE_THRESHOLD * math.sqrt(dx * dy) else None

    def mode_block_px(self, q: float) -> tuple[int, int]:
        """(rows, cols) of native pixels per mode block for quantised spacing ``q``."""
        dx, dy = self._reference_pixel_m()
        return max(1, round(q / (2.0 * dy))), max(1, round(q / (2.0 * dx)))

    # --- block machinery ----------------------------------------------------------
    def _block_layout(self, fy: int, fx: int) -> tuple[int, int, int, int]:
        g = self.grid
        by0, bx0 = g.row0 // fy, g.col0 // fx
        nby = (g.row0 + g.height - 1) // fy - by0 + 1
        nbx = (g.col0 + g.width - 1) // fx - bx0 + 1
        return by0, bx0, nby, nbx

    def _block_count(self, fy: int, fx: int, pred: Callable[[np.ndarray], np.ndarray]) -> np.ndarray:
        """int32 (nby, nbx) counts of pixels where ``pred`` holds, per block."""
        g = self.grid
        by0, bx0, nby, nbx = self._block_layout(fy, fx)
        row_pad, col_pad = g.row0 - by0 * fy, g.col0 - bx0 * fx
        out = np.zeros((nby, nbx), dtype=np.int32)
        step = max(1, _STRIP_PIXELS // max(1, nbx * fx * fy))
        for a in range(0, nby, step):
            b = min(nby, a + step)
            pr0, pr1 = a * fy, b * fy  # padded row range of this strip
            dr0, dr1 = max(0, pr0 - row_pad), min(g.height, pr1 - row_pad)
            strip = np.zeros((pr1 - pr0, nbx * fx), dtype=bool)
            if dr0 < dr1:
                strip[dr0 + row_pad - pr0:dr1 + row_pad - pr0, col_pad:col_pad + g.width] = pred(self.data[dr0:dr1])
            out[a:b] = strip.reshape(b - a, fy, nbx, fx).sum(axis=(1, 3), dtype=np.int32)
        return out

    def _present_codes(self) -> list[int]:
        hist = np.zeros(256, dtype=np.int64)
        step = max(1, _STRIP_PIXELS // max(1, self.grid.width))
        for r in range(0, self.grid.height, step):
            hist += np.bincount(self.data[r:r + step].ravel(), minlength=256)
        return [int(c) for c in CLASS_CODES if hist[c]]

    def _mode_raster(self, q: float) -> _BlockRaster:
        br = self._modes.get(q)
        if br is None:
            fy, fx = self.mode_block_px(q)
            by0, bx0, nby, nbx = self._block_layout(fy, fx)
            best = np.zeros((nby, nbx), dtype=np.int64)
            mode = np.zeros((nby, nbx), dtype=np.uint8)
            # Ascending codes with a strict ">" keep the lowest code on ties.
            for code in self._present_codes():
                win = _box_sum(self._block_count(fy, fx, lambda d, c=code: d == c), 1, 1)
                better = win > best
                best[better] = win[better]
                mode[better] = code
            br = _BlockRaster(mode, by0, bx0, fy, fx)
            self._modes[q] = br
        return br

    def _built_raster(self, radius_m: float) -> _BlockRaster:
        br = self._built.get(radius_m)
        if br is None:
            dx, dy = self._reference_pixel_m()
            fy, fx = max(1, int(radius_m / (8.0 * dy))), max(1, int(radius_m / (8.0 * dx)))
            ry = max(0, round(radius_m / (fy * dy) - 0.5))
            rx = max(0, round(radius_m / (fx * dx) - 0.5))
            by0, bx0, _, _ = self._block_layout(fy, fx)
            built = _box_sum(self._block_count(fy, fx, lambda d: d == BUILT_UP), ry, rx)
            valid = _box_sum(self._block_count(fy, fx, lambda d: d != 0), ry, rx)
            frac = np.zeros(built.shape, dtype=np.float32)
            np.divide(built, valid, out=frac, where=valid > 0, casting="unsafe")
            br = _BlockRaster(frac, by0, bx0, fy, fx)
            self._built[radius_m] = br
        return br

    def clear_cache(self) -> None:
        self._modes.clear()
        self._built.clear()

    # --- sampling -----------------------------------------------------------------
    def sample_lonlat(self, lon, lat, spacing_m: float | None = None) -> np.ndarray:
        """uint8 class codes at lon/lat points; 0 outside the mosaic or for
        non-finite input. ``spacing_m`` selects the mode as in ``sample_game``."""
        lon, lat = np.broadcast_arrays(np.asarray(lon, dtype=np.float64), np.asarray(lat, dtype=np.float64))
        shape = lon.shape
        lon, lat = lon.ravel(), lat.ravel()
        finite = np.isfinite(lon) & np.isfinite(lat)
        out = np.zeros(lon.shape, dtype=np.uint8)
        if not finite.any():
            return out.reshape(shape)
        q = self.mode_scale(spacing_m)
        u, v = self.grid.uv(lon[finite], lat[finite])
        gr, gc = np.floor(v).astype(np.int64), np.floor(u).astype(np.int64)
        if q is None:
            values, r, c = self.data, gr - self.grid.row0, gc - self.grid.col0
        else:
            br = self._mode_raster(q)
            values, r, c = br.values, gr // br.fy - br.by0, gc // br.fx - br.bx0
        inside = (r >= 0) & (r < values.shape[0]) & (c >= 0) & (c < values.shape[1])
        res = np.zeros(r.shape, dtype=np.uint8)
        res[inside] = values[r[inside], c[inside]]
        out[finite] = res
        return out.reshape(shape)

    def sample_game(self, x, z, spacing_m: float | None = None) -> np.ndarray:
        """uint8 class codes at game-space points: the pixel's class, or the
        3 x 3-block mode for spacings above 3x native. Pure function of position."""
        x, z = np.broadcast_arrays(np.asarray(x, dtype=np.float64), np.asarray(z, dtype=np.float64))
        lon, lat = projection.game_to_lonlat(x, z)
        return self.sample_lonlat(np.asarray(lon), np.asarray(lat), spacing_m=spacing_m)

    def built_up_fraction_lonlat(self, lon, lat, radius_m: float) -> np.ndarray:
        """Fraction in [0, 1] of valid pixels that are built-up (class 50) in a
        square window of half-width ``radius_m`` around each point."""
        if not radius_m > 0:
            raise ValueError(f"radius must be positive, got {radius_m}")
        lon, lat = np.broadcast_arrays(np.asarray(lon, dtype=np.float64), np.asarray(lat, dtype=np.float64))
        shape = lon.shape
        br = self._built_raster(float(radius_m))
        u, v = self.grid.uv(lon.ravel(), lat.ravel())
        row = v / br.fy - 0.5 - br.by0
        col = u / br.fx - 0.5 - br.bx0
        finite = np.isfinite(row) & np.isfinite(col)
        out = np.full(row.shape, np.nan, dtype=np.float64)
        if finite.any():
            out[finite] = ndimage.map_coordinates(br.values, np.stack([row[finite], col[finite]]), order=1,
                                                  mode="nearest", prefilter=False, output=np.float64)
        return np.clip(out, 0.0, 1.0).reshape(shape)

    def built_up_fraction_game(self, x, z, radius_m: float) -> np.ndarray:
        x, z = np.broadcast_arrays(np.asarray(x, dtype=np.float64), np.asarray(z, dtype=np.float64))
        lon, lat = projection.game_to_lonlat(x, z)
        return self.built_up_fraction_lonlat(np.asarray(lon), np.asarray(lat), radius_m)
