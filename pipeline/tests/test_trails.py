"""Tests for ghumante_pipeline.trails: sac_scale inference from terrain."""

from __future__ import annotations

import copy
import math

import numpy as np
import pytest

from ghumante_pipeline.model import RoadClass, RoadFeature, SacScale
from ghumante_pipeline.trails import (
    DIFFICULTY_CLASSES,
    EARTH_RADIUS_M,
    assign_trail_difficulty,
    infer_sac_scale,
    polyline_length_m,
    polyline_lengths_m,
    resample_polyline,
    segment_lengths_m,
    trail_profile,
)

DEG_M = 2 * math.pi * EARTH_RADIUS_M / 360.0  # metres per degree of latitude
LAT0, LON0 = 27.7, 85.3
LON_M = DEG_M * math.cos(math.radians(LAT0))  # metres per degree of longitude at LAT0


# ---------------------------------------------------------------------------
# Geodesy helpers
# ---------------------------------------------------------------------------
def test_segment_lengths_known_distances():
    d = segment_lengths_m(np.array([[0.0, 0.0], [0.0, 1.0], [1.0, 1.0], [1.0, 1.0]]))
    assert d[0] == pytest.approx(DEG_M, rel=1e-12)
    assert d[1] == pytest.approx(DEG_M * math.cos(math.radians(1.0)), rel=1e-4)
    assert d[2] == 0.0


@pytest.mark.parametrize("pts", [np.zeros((0, 2)), np.array([[85.0, 27.0]]), np.zeros(4)])
def test_segment_lengths_degenerate(pts):
    assert segment_lengths_m(pts).size == 0
    assert polyline_length_m(pts) == 0.0


def test_resample_polyline_uniform_steps_and_endpoints():
    line = np.array([[LON0, LAT0], [LON0 + 1000 / LON_M, LAT0], [LON0 + 1000 / LON_M, LAT0 + 45 / DEG_M]])
    pts, step = resample_polyline(line, 30.0)
    total = polyline_length_m(line)
    assert total == pytest.approx(1045.0, rel=1e-3)
    assert step <= 30.0 and step == pytest.approx(total / (len(pts) - 1))
    assert len(pts) - 1 == math.ceil(total / 30.0)
    np.testing.assert_allclose(pts[0], line[0])
    np.testing.assert_allclose(pts[-1], line[-1])
    chords = segment_lengths_m(pts)
    assert np.all(chords <= step * (1 + 2e-3))  # a chord across the corner is shorter than the arc
    assert np.sum(np.abs(chords - step) > step * 2e-3) == 1


def test_resample_polyline_skips_duplicate_vertices():
    line = np.array([[LON0, LAT0], [LON0, LAT0], [LON0 + 100 / LON_M, LAT0], [LON0 + 100 / LON_M, LAT0]])
    pts, step = resample_polyline(line, 30.0)
    assert len(pts) == 5 and step == pytest.approx(25.0, rel=1e-3)
    assert np.all(np.isfinite(pts))


def test_resample_polyline_short_and_degenerate():
    pts, step = resample_polyline(np.array([[LON0, LAT0], [LON0 + 10 / LON_M, LAT0]]), 30.0)
    assert len(pts) == 2 and step == pytest.approx(10.0, rel=1e-3)
    pts, step = resample_polyline(np.array([[LON0, LAT0], [LON0, LAT0]]), 30.0)
    assert len(pts) == 1 and step == 0.0
    pts, step = resample_polyline(np.zeros((0, 2)), 30.0)
    assert pts.shape == (0, 2) and step == 0.0


# ---------------------------------------------------------------------------
# Rule table
# ---------------------------------------------------------------------------
P, FW, ST, PED, CYC, BR, TR = (RoadClass.PATH, RoadClass.FOOTWAY, RoadClass.STEPS, RoadClass.PEDESTRIAN,
                               RoadClass.CYCLEWAY, RoadClass.BRIDLEWAY, RoadClass.TRACK)
T1, T2, T3, T4, T5 = (SacScale.HIKING, SacScale.MOUNTAIN_HIKING, SacScale.DEMANDING_MOUNTAIN_HIKING,
                      SacScale.ALPINE_HIKING, SacScale.DEMANDING_ALPINE_HIKING)


@pytest.mark.parametrize("cls, elev, p90, glacier, expected", [
    # path below 3000 m: slope decides
    (P, 1400, 0, False, T1), (P, 1400, 14.9, False, T1), (P, 1400, 15, False, T2), (P, 2999, 24.9, False, T2),
    (P, 2000, 25, False, T3), (P, 2000, 60, False, T3),
    # 3000-4500 m: one grade up
    (P, 3000, 5, False, T2), (P, 3800, 20, False, T3), (P, 4499, 30, False, T4),
    # >= 4500 m or glacier: at least T4, T5 when very steep
    (P, 4500, 0, False, T4), (P, 5364, 20, False, T4), (P, 5364, 34.9, False, T4), (P, 5364, 35, False, T5),
    (P, 1500, 5, True, T4), (P, 1500, 40, True, T5),
    # bridleway follows the path rules
    (BR, 1000, 20, False, T2),
    # built ways: T1 unless high or steep
    (ST, 1300, 24, False, T1), (FW, 2800, 10, False, T1), (PED, 1300, 0, False, T1), (CYC, 100, 3, False, T1),
    (TR, 2000, 20, False, T1),
    (ST, 1300, 30, False, T3), (FW, 3200, 5, False, T2), (TR, 3900, 10, False, T2), (TR, 4800, 10, False, T4),
    (FW, 1000, 5, True, T4),
    # never T6, whatever the input
    (P, 8800, 89, True, T5),
])
def test_infer_sac_scale(cls, elev, p90, glacier, expected):
    assert infer_sac_scale(cls, elev, p90, glacier) == expected


@pytest.mark.parametrize("cls", [RoadClass.PRIMARY, RoadClass.RESIDENTIAL, RoadClass.UNKNOWN, RoadClass.SERVICE])
def test_infer_sac_scale_non_trail(cls):
    assert infer_sac_scale(cls, 5000, 40, True) == SacScale.UNKNOWN


@pytest.mark.parametrize("elev, p90", [(math.nan, 10.0), (3000.0, math.nan), (math.inf, 5.0)])
def test_infer_sac_scale_non_finite(elev, p90):
    assert infer_sac_scale(RoadClass.PATH, elev, p90, False) == SacScale.UNKNOWN


def test_infer_sac_scale_grid_is_monotonic_and_never_t6():
    for cls in DIFFICULTY_CLASSES:
        for glacier in (False, True):
            prev_by_slope = None
            for elev in np.linspace(0, 8800, 45):
                row = [infer_sac_scale(cls, float(elev), float(s), glacier) for s in np.linspace(0, 80, 41)]
                assert all(SacScale.HIKING <= g <= SacScale.DEMANDING_ALPINE_HIKING for g in row)
                assert row == sorted(row)  # steeper is never easier
                if prev_by_slope is not None:
                    assert all(a <= b for a, b in zip(prev_by_slope, row))  # higher is never easier
                prev_by_slope = row


# ---------------------------------------------------------------------------
# Profiles
# ---------------------------------------------------------------------------
def test_trail_profile():
    step = 30.0
    rises = np.concatenate([np.full(90, math.tan(math.radians(10.0)) * step),
                            np.full(10, math.tan(math.radians(40.0)) * step)])
    z = 1000.0 + np.concatenate([[0.0], np.cumsum(rises)])
    max_e, p90 = trail_profile(z, step)
    assert max_e == pytest.approx(z[-1])
    assert 10.0 <= p90 <= 40.0
    _, p90_desc = trail_profile(z[::-1], step)  # descending counts the same
    assert p90_desc == pytest.approx(p90)


def test_trail_profile_nan_and_degenerate():
    max_e, p90 = trail_profile(np.array([100.0, np.nan, 130.0, 160.0]), 30.0)
    assert max_e == 160.0 and p90 == pytest.approx(45.0)
    assert all(math.isnan(v) for v in trail_profile(np.array([np.nan, np.nan]), 30.0))
    max_e, p90 = trail_profile(np.array([500.0]), 0.0)
    assert max_e == 500.0 and math.isnan(p90)


# ---------------------------------------------------------------------------
# Batch assignment with a synthetic DEM
# ---------------------------------------------------------------------------
class PlaneDem:
    """Elevation rising eastward at ``slope_deg`` from ``base_m`` at LON0, flat north-south."""

    def __init__(self, base_m: float, slope_deg: float) -> None:
        self.base_m = base_m
        self.grad = math.tan(math.radians(slope_deg))
        self.calls: list[int] = []

    def __call__(self, lonlat: np.ndarray) -> np.ndarray:
        assert lonlat.ndim == 2 and lonlat.shape[1] == 2
        self.calls.append(len(lonlat))
        return self.base_m + self.grad * (lonlat[:, 0] - LON0) * LON_M


def trail(osm_id: int, cls: RoadClass = RoadClass.PATH, east_m: float = 600.0, north_m: float = 0.0,
          lon0: float = LON0, sac: SacScale = SacScale.UNKNOWN, inferred: bool = False) -> RoadFeature:
    pts = np.array([[lon0, LAT0], [lon0 + east_m / LON_M, LAT0 + north_m / DEG_M]])
    return RoadFeature(osm_id=osm_id, cls=cls, lonlat=pts, node_ids=np.array([2 * osm_id, 2 * osm_id + 1]),
                       sac_scale=sac, sac_inferred=inferred)


@pytest.mark.parametrize("base, slope, expected", [
    (1300.0, 5.0, T1), (1300.0, 20.0, T2), (1300.0, 30.0, T3), (3500.0, 5.0, T2), (3500.0, 20.0, T3),
    (5000.0, 10.0, T4), (5000.0, 40.0, T5),
])
def test_assign_on_planes(base, slope, expected):
    roads = [trail(1)]
    stats = assign_trail_difficulty(roads, PlaneDem(base, slope))
    assert roads[0].sac_scale == expected and roads[0].sac_inferred
    assert stats["inferred"] == 1 and stats["inferred_km_by_grade"][expected.name] == pytest.approx(0.6, abs=1e-3)


def test_assign_contour_trail_is_flat():
    roads = [trail(1, east_m=0.0, north_m=800.0)]  # runs north along the contour of a 40-degree slope
    assign_trail_difficulty(roads, PlaneDem(1300.0, 40.0))
    assert roads[0].sac_scale == T1


def test_assign_keeps_tagged_and_recomputes_inferred():
    roads = [
        trail(1, sac=SacScale.DIFFICULT_ALPINE_HIKING),  # tagged: kept
        trail(2, sac=SacScale.ALPINE_HIKING, inferred=True),  # stale inference: recomputed
        trail(3, cls=RoadClass.RESIDENTIAL),  # not a trail: untouched
        trail(4, cls=RoadClass.TRACK),
    ]
    stats = assign_trail_difficulty(roads, PlaneDem(1300.0, 3.0))
    assert (roads[0].sac_scale, roads[0].sac_inferred) == (SacScale.DIFFICULT_ALPINE_HIKING, False)
    assert (roads[1].sac_scale, roads[1].sac_inferred) == (T1, True)
    assert (roads[2].sac_scale, roads[2].sac_inferred) == (SacScale.UNKNOWN, False)
    assert (roads[3].sac_scale, roads[3].sac_inferred) == (T1, True)
    assert stats["candidates"] == 3 and stats["tagged"] == 1 and stats["inferred"] == 2
    assert stats["tagged_km_by_grade"]["DIFFICULT_ALPINE_HIKING"] == pytest.approx(0.6, abs=1e-3)
    assert stats["tagged_km"] + stats["inferred_km"] == pytest.approx(1.8, abs=1e-3)


def test_assign_near_glacier_gets_road_vertices():
    seen = []

    def near(lonlat: np.ndarray) -> bool:
        seen.append(lonlat.copy())
        return lonlat[0, 0] > LON0 + 0.5

    roads = [trail(1), trail(2, lon0=LON0 + 1.0)]
    dem = lambda ll: np.full(len(ll), 2000.0)  # noqa: E731
    assign_trail_difficulty(roads, dem, near)
    assert [r.sac_scale for r in roads] == [T1, T4]
    assert len(seen) == 2
    np.testing.assert_array_equal(seen[0], roads[0].lonlat)


def test_assign_batches_sampler_calls_without_changing_results():
    roads = [trail(i, east_m=200.0 + 37.0 * i, lon0=LON0 + 0.01 * i) for i in range(40)]
    dem_one = PlaneDem(2900.0, 18.0)
    r1 = copy.deepcopy(roads)
    s1 = assign_trail_difficulty(r1, dem_one)
    dem_many = PlaneDem(2900.0, 18.0)
    r2 = copy.deepcopy(roads)
    s2 = assign_trail_difficulty(r2, dem_many, batch_points=50)
    assert len(dem_one.calls) == 1 and len(dem_many.calls) > 5
    assert sum(dem_many.calls) == sum(dem_one.calls)
    assert [(r.sac_scale, r.sac_inferred) for r in r1] == [(r.sac_scale, r.sac_inferred) for r in r2]
    assert s1 == s2


def test_assign_spacing_controls_sample_count():
    dem = PlaneDem(1000.0, 10.0)
    assign_trail_difficulty([trail(1, east_m=900.0)], dem, spacing_m=30.0)
    assign_trail_difficulty([trail(1, east_m=900.0)], dem, spacing_m=100.0)
    assert dem.calls == [31, 10]
    with pytest.raises(ValueError):
        assign_trail_difficulty([trail(1)], dem, spacing_m=0.0)


def test_assign_nodata_and_degenerate():
    roads = [trail(1), trail(2, east_m=0.0), trail(3)]
    roads[2].lonlat = roads[2].lonlat[:1]

    def dem(lonlat):
        return np.full(len(lonlat), -32767.0)  # Copernicus-style nodata

    stats = assign_trail_difficulty(roads, dem)
    assert all(r.sac_scale == SacScale.UNKNOWN and not r.sac_inferred for r in roads)
    assert stats["no_elevation"] == 1 and stats["degenerate"] == 2 and stats["inferred"] == 0


def test_assign_rejects_bad_sampler_shape():
    with pytest.raises(ValueError):
        assign_trail_difficulty([trail(1)], lambda ll: np.zeros(3))


def test_assign_partial_nodata_still_infers():
    def dem(lonlat):
        z = PlaneDem(1300.0, 20.0)(lonlat)
        z[::3] = np.nan
        return z

    roads = [trail(1)]
    assign_trail_difficulty(roads, dem)
    assert roads[0].sac_scale == T2


# ---------------------------------------------------------------------------
# Real DEM
# ---------------------------------------------------------------------------
@pytest.mark.realdata
def test_real_dem_shivapuri_climb():
    """Kathmandu valley floor (~1 300 m) up toward Shivapuri: a real hill trail."""
    from pathlib import Path

    rasterio = pytest.importorskip("rasterio")
    dem_path = Path(__file__).resolve().parents[1] / "data/raw/dem/Copernicus_DSM_COG_10_N27_00_E085_00_DEM.tif"
    if not dem_path.exists():
        pytest.skip("Copernicus DEM tile not downloaded")
    with rasterio.open(dem_path) as ds:
        band = ds.read(1)
        tr = ds.transform  # north-up: lon = c + col * a, lat = f + row * e

        def sample(lonlat: np.ndarray) -> np.ndarray:
            c = np.clip(np.floor((lonlat[:, 0] - tr.c) / tr.a).astype(int), 0, band.shape[1] - 1)
            r = np.clip(np.floor((lonlat[:, 1] - tr.f) / tr.e).astype(int), 0, band.shape[0] - 1)
            return band[r, c].astype(np.float64)

        road = RoadFeature(osm_id=1, cls=RoadClass.PATH, lonlat=np.array([[85.33, 27.70], [85.37, 27.81]]),
                           node_ids=np.array([1, 2]))
        flat = RoadFeature(osm_id=2, cls=RoadClass.FOOTWAY, lonlat=np.array([[85.31, 27.70], [85.33, 27.69]]),
                           node_ids=np.array([3, 4]))
        stats = assign_trail_difficulty([road, flat], sample)
    assert stats["inferred"] == 2
    assert road.sac_scale in (T2, T3) and flat.sac_scale == T1


def test_polyline_lengths_vectorised_matches_scalar():
    rng = np.random.default_rng(5)
    lines = [np.column_stack((LON0 + np.cumsum(rng.normal(0, 1e-3, k)), LAT0 + np.cumsum(rng.normal(0, 1e-3, k))))
             for k in (0, 1, 2, 7, 1, 30, 0)]
    lines.append(np.zeros(4))  # malformed: treated as empty
    got = polyline_lengths_m(lines)
    assert got.shape == (len(lines),)
    np.testing.assert_allclose(got, [polyline_length_m(x) for x in lines], rtol=1e-12, atol=1e-9)
    assert polyline_lengths_m([]).shape == (0,)
    assert polyline_lengths_m([np.zeros((1, 2))]).tolist() == [0.0]
