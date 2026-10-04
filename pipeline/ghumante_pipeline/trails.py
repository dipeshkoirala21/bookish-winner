"""Trail difficulty (``sac_scale``) inference from terrain.

Only 2.2% of Nepal's 110 000 km of trails carry ``sac_scale``
(docs/reports/TAG_COVERAGE_FINDINGS.md). For the rest, the difficulty is
inferred (ARCHITECTURE.md section 6.2, ADR-005) from three things:

* the 90th percentile of the along-track slope, from DEM heights sampled every
  ~30 m along the trail;
* the highest elevation the trail reaches;
* whether the trail runs near a glacier or moraine.

Tagged values are always kept. Inferred ones set ``RoadFeature.sac_inferred``,
which becomes ``RoadFlags.SAC_INFERRED`` in the tiles.

The module also holds the small geodesic helpers (haversine segment lengths,
uniform resampling) that ``surface.py`` uses for road lengths.
"""

from __future__ import annotations

import math
from typing import Callable, Sequence

import numpy as np

from .model import TRAIL_CLASSES, RoadClass, RoadFeature, SacScale

EARTH_RADIUS_M = 6_371_008.8  # IUGG mean radius

# Rule thresholds (documented in infer_sac_scale).
HIGH_ELEV_M = 3000.0
ALPINE_ELEV_M = 4500.0
SLOPE_T2_DEG = 15.0
SLOPE_T3_DEG = 25.0
SLOPE_T5_DEG = 35.0
NODATA_BELOW_M = -1000.0  # sampler values below this are treated as nodata

# Classes that get a difficulty: all trails, plus tracks (jeep tracks are walked too).
DIFFICULTY_CLASSES = TRAIL_CLASSES | {RoadClass.TRACK}
# Built, gentle ways: T1 unless they are high or steep.
EASY_CLASSES = frozenset({RoadClass.STEPS, RoadClass.FOOTWAY, RoadClass.PEDESTRIAN, RoadClass.CYCLEWAY,
                          RoadClass.TRACK})


# ---------------------------------------------------------------------------
# Geodesy helpers
# ---------------------------------------------------------------------------
def segment_lengths_m(lonlat: np.ndarray) -> np.ndarray:
    """Great-circle (haversine) length of each segment of an (N, 2) lon/lat polyline."""
    a = np.asarray(lonlat, dtype=np.float64)
    if a.ndim != 2 or a.shape[0] < 2:
        return np.zeros(0, dtype=np.float64)
    lon = np.radians(a[:, 0])
    lat = np.radians(a[:, 1])
    h = (np.sin(np.diff(lat) / 2.0) ** 2
         + np.cos(lat[:-1]) * np.cos(lat[1:]) * np.sin(np.diff(lon) / 2.0) ** 2)
    return 2.0 * EARTH_RADIUS_M * np.arcsin(np.sqrt(np.clip(h, 0.0, 1.0)))


def polyline_length_m(lonlat: np.ndarray) -> float:
    """Great-circle length of an (N, 2) lon/lat polyline in metres."""
    return float(segment_lengths_m(lonlat).sum())


def polyline_lengths_m(lines: Sequence[np.ndarray]) -> np.ndarray:
    """Lengths in metres of many (N, 2) lon/lat polylines, computed in one vectorised pass."""
    arrs = [a if a.ndim == 2 and a.shape[1:] == (2,) else np.zeros((0, 2))
            for a in (np.asarray(x, dtype=np.float64) for x in lines)]
    n = len(arrs)
    sizes = np.array([len(a) for a in arrs], dtype=np.int64)
    if n == 0 or sizes.sum() < 2:
        return np.zeros(n, dtype=np.float64)
    seg = segment_lengths_m(np.concatenate(arrs))
    owner = np.repeat(np.arange(n), sizes)
    same = owner[:-1] == owner[1:]  # drop the joins between consecutive polylines
    return np.bincount(owner[:-1][same], weights=seg[same], minlength=n)


def resample_polyline(lonlat: np.ndarray, spacing_m: float) -> tuple[np.ndarray, float]:
    """Resample a lon/lat polyline at uniform arc-length steps of at most ``spacing_m``.

    Returns ``(points, step_m)``. ``points`` is (M, 2) and includes both
    endpoints; ``step_m`` is the true along-track distance between consecutive
    points. Positions are linearly interpolated in lon/lat within each segment,
    which is accurate for segments far shorter than the Earth's radius. A
    zero-length polyline gives one point and ``step_m = 0``.
    """
    a = np.asarray(lonlat, dtype=np.float64)
    if a.ndim != 2 or a.shape[0] == 0:
        return np.zeros((0, 2), dtype=np.float64), 0.0
    d = segment_lengths_m(a)
    if d.size:
        keep = np.concatenate(([True], d > 0.0))  # np.interp needs increasing x
        a, d = a[keep], d[d > 0.0]
    total = float(d.sum())
    if total <= 0.0:
        return a[:1].copy(), 0.0
    n = max(1, int(math.ceil(total / spacing_m)))
    s = np.linspace(0.0, total, n + 1)
    cum = np.concatenate(([0.0], np.cumsum(d)))
    pts = np.column_stack((np.interp(s, cum, a[:, 0]), np.interp(s, cum, a[:, 1])))
    return pts, total / n


# ---------------------------------------------------------------------------
# Rules
# ---------------------------------------------------------------------------
def infer_sac_scale(cls: RoadClass, max_elev_m: float, slope_p90_deg: float, near_glacier: bool) -> SacScale:
    """Difficulty grade from terrain. The rule table:

    1. ``slope_p90`` < 15 deg gives T1 HIKING, < 25 deg T2 MOUNTAIN_HIKING,
       otherwise T3 DEMANDING_MOUNTAIN_HIKING.
    2. Between 3 000 m and 4 500 m the grade goes up by one (altitude, weather,
       remoteness).
    3. At 4 500 m and above, or near a glacier, the trail is at least T4
       ALPINE_HIKING, and T5 DEMANDING_ALPINE_HIKING when ``slope_p90`` >= 35 deg.
    4. STEPS, FOOTWAY, PEDESTRIAN, CYCLEWAY and TRACK are built ways: they are T1
       unless they are high (>= 3 000 m or near a glacier) or steep
       (``slope_p90`` >= 25 deg). In that case rules 1-3 apply.
    5. T6 DIFFICULT_ALPINE_HIKING is never inferred. Only a mapper who has been
       there can say a route needs climbing.

    Non-trail classes, and non-finite elevation or slope, give ``UNKNOWN``.
    """
    if cls not in DIFFICULTY_CLASSES:
        return SacScale.UNKNOWN
    if not (math.isfinite(max_elev_m) and math.isfinite(slope_p90_deg)):
        return SacScale.UNKNOWN
    high = near_glacier or max_elev_m >= HIGH_ELEV_M
    if cls in EASY_CLASSES and not high and slope_p90_deg < SLOPE_T3_DEG:
        return SacScale.HIKING
    if near_glacier or max_elev_m >= ALPINE_ELEV_M:
        return SacScale.DEMANDING_ALPINE_HIKING if slope_p90_deg >= SLOPE_T5_DEG else SacScale.ALPINE_HIKING
    if slope_p90_deg < SLOPE_T2_DEG:
        grade = 1
    elif slope_p90_deg < SLOPE_T3_DEG:
        grade = 2
    else:
        grade = 3
    if max_elev_m >= HIGH_ELEV_M:
        grade += 1
    return SacScale(grade)


def trail_profile(elev_m: np.ndarray, step_m: float) -> tuple[float, float]:
    """(max elevation, 90th-percentile slope in degrees) of a uniformly sampled profile.

    NaN samples are ignored. The slope is NaN when fewer than two consecutive
    samples are valid or ``step_m`` is 0.
    """
    z = np.asarray(elev_m, dtype=np.float64)
    finite = np.isfinite(z)
    if not finite.any():
        return math.nan, math.nan
    max_e = float(z[finite].max())
    if step_m <= 0.0 or z.size < 2:
        return max_e, math.nan
    dz = np.abs(np.diff(z))
    dz = dz[np.isfinite(dz)]
    if dz.size == 0:
        return max_e, math.nan
    slopes = np.degrees(np.arctan(dz / step_m))
    return max_e, float(np.percentile(slopes, 90.0))


# ---------------------------------------------------------------------------
# Batch assignment
# ---------------------------------------------------------------------------
def assign_trail_difficulty(
    roads: Sequence[RoadFeature],
    sample_elev: Callable[[np.ndarray], np.ndarray],
    near_glacier: Callable[[np.ndarray], bool] | None = None,
    spacing_m: float = 30.0,
    *,
    batch_points: int = 1_000_000,
) -> dict:
    """Infer ``sac_scale`` in place for trails (``TRAIL_CLASSES``) and tracks.

    * Roads with a tagged ``sac_scale`` (not UNKNOWN, ``sac_inferred`` False)
      keep it. Earlier inferred values are recomputed.
    * Each remaining polyline is resampled every ~``spacing_m`` metres. The
      points of many roads are batched into one ``sample_elev`` call, which takes
      an (N, 2) lon/lat array and returns (N,) metres (NaN, or anything below
      -1 000 m, is nodata).
    * ``near_glacier`` gets the road's own (N, 2) lon/lat vertices.
    * The result goes through ``infer_sac_scale``, and ``sac_inferred`` is set
      to True. Roads with no valid elevation, or of zero length, stay UNKNOWN.

    Returns statistics: counts and kilometres for tagged and inferred trails,
    and inferred kilometres per grade.
    """
    if spacing_m <= 0:
        raise ValueError("spacing_m must be positive")
    stats = {
        "candidates": 0, "tagged": 0, "inferred": 0, "no_elevation": 0, "degenerate": 0,
        "tagged_km": 0.0, "inferred_km": 0.0, "spacing_m": float(spacing_m),
        "tagged_km_by_grade": {s.name: 0.0 for s in SacScale if s != SacScale.UNKNOWN},
        "inferred_km_by_grade": {s.name: 0.0 for s in SacScale if s != SacScale.UNKNOWN},
    }

    pending: list[tuple[int, np.ndarray, float, float]] = []  # (road index, points, step, length)
    pending_pts = 0

    def flush() -> None:
        nonlocal pending, pending_pts
        if not pending:
            return
        allpts = np.concatenate([p for _, p, _, _ in pending])
        z = np.asarray(sample_elev(allpts), dtype=np.float64).reshape(-1)
        if z.shape[0] != allpts.shape[0]:
            raise ValueError(f"sample_elev returned {z.shape[0]} values for {allpts.shape[0]} points")
        z = np.where(z < NODATA_BELOW_M, np.nan, z)
        off = 0
        for idx, pts, step, length_m in pending:
            road = roads[idx]
            zi = z[off:off + len(pts)]
            off += len(pts)
            max_e, p90 = trail_profile(zi, step)
            glacier = bool(near_glacier(road.lonlat)) if near_glacier is not None else False
            sac = infer_sac_scale(road.cls, max_e, p90, glacier)
            if sac == SacScale.UNKNOWN:
                stats["no_elevation"] += 1
                continue
            road.sac_scale = sac
            road.sac_inferred = True
            stats["inferred"] += 1
            stats["inferred_km"] += length_m / 1000.0
            stats["inferred_km_by_grade"][sac.name] += length_m / 1000.0
        pending = []
        pending_pts = 0

    for idx, road in enumerate(roads):
        if road.cls not in DIFFICULTY_CLASSES:
            continue
        stats["candidates"] += 1
        if road.sac_scale != SacScale.UNKNOWN and not road.sac_inferred:
            km = polyline_length_m(road.lonlat) / 1000.0
            stats["tagged"] += 1
            stats["tagged_km"] += km
            stats["tagged_km_by_grade"][SacScale(road.sac_scale).name] += km
            continue
        road.sac_scale = SacScale.UNKNOWN
        road.sac_inferred = False
        pts, step = resample_polyline(road.lonlat, spacing_m)
        if step <= 0.0:
            stats["degenerate"] += 1
            continue
        pending.append((idx, pts, step, step * (len(pts) - 1)))
        pending_pts += len(pts)
        if pending_pts >= batch_points:
            flush()
    flush()

    for k in ("tagged_km", "inferred_km"):
        stats[k] = round(stats[k], 3)
    for k in ("tagged_km_by_grade", "inferred_km_by_grade"):
        stats[k] = {name: round(v, 3) for name, v in stats[k].items()}
    return stats
