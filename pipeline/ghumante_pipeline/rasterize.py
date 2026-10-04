"""Burn polygons into vertex-aligned sample grids (AREA kinds for BIOM etc.).

``rasterize_at_samples`` answers "which polygon covers this *sample point*",
for the same vertex grids as ``HGHT``/``BIOM`` (docs/DATA_FORMATS.md section
1.1): ``out[j, i]`` belongs to game point ``(x0 + i*spacing, z0 + j*spacing)``.
Row ``j = 0`` is the south edge and column ``i = 0`` the west edge. A sample is
burned when it lies inside the geometry. This is ``all_touched=False`` with a
transform whose pixel centres are the sample points, so nothing is smeared by
half a cell.

The bulk of the grid uses ``rasterio.features.rasterize`` (GDAL's scanline
filler). The four border rows and columns are then re-evaluated with exact
point-in-polygon tests (``shapely.contains_xy``) on the very same float64
coordinates. Neighbouring tiles share those samples, and the exact test makes
them agree bit for bit, even for a sample lying within floating-point noise of
a polygon edge.

``area_priority`` gives the burn order for ``model.AreaKind``: broad land use
first, then finer land use, then wetland, glacier and water, so water and
glacier win. ``burn_key`` adds "larger polygons first" inside a tier, so
islands, sand bars and ponds drawn on top of bigger polygons survive.
"""

from __future__ import annotations

from typing import Iterable, Sequence

import numpy as np
import shapely
from rasterio.transform import Affine
from rasterio import features

from .model import AreaKind

# Burn tiers (low burns first, high last and wins).
_AREA_PRIORITY: dict[AreaKind, int] = {
    AreaKind.NONE: 0,
    AreaKind.PROTECTED: 10,  # boundaries, not land cover
    AreaKind.MILITARY: 12,
    AreaKind.FOREST: 20,
    AreaKind.SCRUB: 21,
    AreaKind.GRASSLAND: 22,
    AreaKind.MEADOW: 23,
    AreaKind.BARE_ROCK: 25,
    AreaKind.SCREE: 26,
    AreaKind.FARMLAND: 30,
    AreaKind.ORCHARD: 32,
    AreaKind.TEA_GARDEN: 33,
    AreaKind.RESIDENTIAL: 40,
    AreaKind.COMMERCIAL: 41,
    AreaKind.INDUSTRIAL: 42,
    AreaKind.AERODROME: 43,
    AreaKind.CEMETERY: 50,
    AreaKind.PARK: 51,
    AreaKind.PITCH: 52,
    AreaKind.RELIGIOUS: 53,
    AreaKind.PEDESTRIAN: 54,
    AreaKind.WETLAND: 60,
    AreaKind.GLACIER: 70,
    # Water and the sand/shingle bars mapped inside rivers share the last tier;
    # inside it, larger polygons burn first (burn_key), so a bar inside a
    # riverbank polygon and a channel inside a shingle polygon both survive.
    AreaKind.WATER_RIVER: 80,
    AreaKind.SAND_SHINGLE: 80,
    AreaKind.WATER_LAKE: 80,
    AreaKind.WATER_POND: 80,
}


def area_priority(kind: AreaKind | int) -> int:
    """Burn tier of an area kind; higher tiers are burned later and win.
    Unknown (future) kinds burn with the land-use tier."""
    try:
        return _AREA_PRIORITY[AreaKind(kind)]
    except (ValueError, KeyError):
        return 35


def burn_key(kind: AreaKind | int, area: float, tiebreak: int = 0) -> tuple[int, float, int]:
    """Sort key for burn order: by tier, then larger area first, then a stable
    caller-supplied id (e.g. the OSM id) so the order never depends on input
    order."""
    return (area_priority(kind), -float(area), int(tiebreak))


def _sample_axes(x0: float, z0: float, spacing: float, n: int) -> tuple[np.ndarray, np.ndarray]:
    idx = np.arange(n, dtype=np.float64)
    return x0 + idx * spacing, z0 + idx * spacing  # same formula as projection.grid_coords


def rasterize_at_samples(
    shapes: Sequence[tuple[object, int]] | Iterable[tuple[object, int]],
    x0: float,
    z0: float,
    spacing: float,
    n: int,
    dtype=np.uint8,
) -> np.ndarray:
    """``(n, n)`` grid of burned values; 0 where no geometry covers a sample.

    ``shapes`` holds ``(shapely geometry in game metres, value)`` pairs. Later
    pairs overwrite earlier ones (sort with ``burn_key`` first). Polygon holes
    are respected. Points and lines burn nothing: they contain no sample.
    """
    if n < 1 or not spacing > 0:
        raise ValueError(f"need n >= 1 and spacing > 0, got n={n}, spacing={spacing}")
    dtype = np.dtype(dtype)
    shapes = [(g, v) for g, v in shapes if g is not None and not g.is_empty
              and g.geom_type in ("Polygon", "MultiPolygon", "GeometryCollection")]
    out = np.zeros((n, n), dtype=dtype)
    if not shapes:
        return out

    # North-up transform whose pixel centres are the sample points; flip after.
    transform = Affine(spacing, 0.0, x0 - 0.5 * spacing, 0.0, -spacing, z0 + (n - 0.5) * spacing)
    burned = features.rasterize(
        [(g, int(v)) for g, v in shapes], out_shape=(n, n), transform=transform, fill=0,
        all_touched=False, dtype=dtype, merge_alg=features.MergeAlg.replace,
    )
    out[:] = burned[::-1]

    # Exact re-evaluation of the border samples shared with neighbouring tiles.
    xs, zs = _sample_axes(x0, z0, spacing, n)
    border = [
        (np.s_[0, :], xs, np.full(n, zs[0])),
        (np.s_[n - 1, :], xs, np.full(n, zs[n - 1])),
        (np.s_[:, 0], np.full(n, xs[0]), zs),
        (np.s_[:, n - 1], np.full(n, xs[n - 1]), zs),
    ]
    for sl, bx, bz in border:
        vals = np.zeros(n, dtype=dtype)
        for g, v in shapes:
            vals[shapely.contains_xy(g, bx, bz)] = v
        out[sl] = vals
    return out
