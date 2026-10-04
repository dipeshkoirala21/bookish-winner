"""Tests for dem.py: Copernicus mosaicking, pixel-centre convention, seams, LOD filtering."""

from __future__ import annotations

import logging
import math
from pathlib import Path

import numpy as np
import pytest
import rasterio
from rasterio.transform import Affine
from scipy import ndimage

from ghumante_pipeline import projection as P
from ghumante_pipeline.dem import DemSampler, GeoGrid, metres_per_degree, quantise_spacing

PPD = 3600  # GLO-30: 1 arc-second
RAW_DEM = Path(__file__).resolve().parents[1] / "data" / "raw" / "dem" / "Copernicus_DSM_COG_10_N27_00_E085_00_DEM.tif"


def write_dem(path: Path, a0: int, b0: int, w: int, h: int, func, nodata=None, point: bool = True) -> np.ndarray:
    """Copernicus-like GeoTIFF: pixel [0, 0] centred on (a0, b0) arc-seconds
    (lon, lat), ``AREA_OR_POINT=Point``, values ``func(lon, lat)``."""
    lon = (a0 + np.arange(w)) / PPD
    lat = (b0 - np.arange(h)) / PPD
    data = np.asarray(func(*np.meshgrid(lon, lat)), dtype=np.float32)
    transform = Affine(1 / PPD, 0, (a0 - 0.5) / PPD, 0, -1 / PPD, (b0 + 0.5) / PPD)
    with rasterio.Env(GTIFF_POINT_GEO_IGNORE=False), \
            rasterio.open(path, "w", driver="GTiff", width=w, height=h, count=1, dtype="float32",
                          crs="EPSG:4326", transform=transform, nodata=nodata, tiled=True,
                          blockxsize=256, blockysize=256) as ds:
        ds.write(data, 1)
        if point:
            ds.update_tags(AREA_OR_POINT="Point")
    return data


def arcsec(deg: float) -> int:
    return round(deg * PPD)


def plane(lon, lat):
    return 1000.0 + 3000.0 * (lon - 85.3) + 2000.0 * (lat - 27.7)


# A synthetic DEM around Kathmandu big enough for a few level-8 tiles: smooth
# relief plus high-frequency noise, so seams and filtering are non-trivial.
SYN_LON0, SYN_LAT0, SYN_W, SYN_H = 85.25, 27.80, 900, 800


def _rough_terrain(lon, lat):
    rng = np.random.default_rng(42)
    noise = ndimage.gaussian_filter(rng.normal(size=lon.shape), 1.0) * 40.0
    return 1500.0 + 400.0 * np.sin(lon * 90.0) * np.cos(lat * 70.0) + 300.0 * (lat - 27.7) * 10 + noise


@pytest.fixture(scope="module")
def rough_dem(tmp_path_factory) -> Path:
    p = tmp_path_factory.mktemp("dem") / "rough.tif"
    write_dem(p, arcsec(SYN_LON0), arcsec(SYN_LAT0), SYN_W, SYN_H, _rough_terrain)
    return p


ROUGH_BBOX = (85.27, 27.60, 85.48, 27.78)


@pytest.fixture(scope="module")
def rough(rough_dem) -> DemSampler:
    return DemSampler.from_files([rough_dem], ROUGH_BBOX, margin_deg=0.01)


def kathmandu_tile(level: int) -> P.TileId:
    x, z = P.lonlat_to_game(85.36, 27.69)
    return P.tile_at(level, float(x), float(z))


def tile_bbox(tile: P.TileId, pad_deg: float) -> tuple[float, float, float, float]:
    lon, lat = P.game_to_lonlat([tile.x0, tile.x0 + tile.size, tile.x0, tile.x0 + tile.size],
                                [tile.z0, tile.z0, tile.z0 + tile.size, tile.z0 + tile.size])
    return (min(lon) - pad_deg, min(lat) - pad_deg, max(lon) + pad_deg, max(lat) + pad_deg)


# --- grid convention ----------------------------------------------------------------
def test_synthetic_tile_mimics_copernicus_point_convention(tmp_path):
    p = tmp_path / "t.tif"
    write_dem(p, arcsec(85.3), arcsec(27.7), 10, 10, plane)
    with rasterio.Env(GTIFF_POINT_GEO_IGNORE=True):
        with rasterio.open(p) as ds:  # raw tie point = centre of pixel [0, 0]
            assert ds.transform.c == pytest.approx(85.3, abs=1e-12)
            assert ds.transform.f == pytest.approx(27.7, abs=1e-12)
    with rasterio.open(p) as ds:  # GDAL's corner-based view
        assert ds.tags()["AREA_OR_POINT"] == "Point"
        assert ds.transform.c == pytest.approx(85.3 - 0.5 / PPD, abs=1e-12)


def test_pixel_centres_sample_stored_values_exactly(tmp_path):
    rng = np.random.default_rng(1)
    vals = rng.uniform(0, 5000, size=(40, 50)).astype(np.float32)
    p = tmp_path / "rand.tif"
    a0, b0 = arcsec(85.30), arcsec(27.70)
    write_dem(p, a0, b0, 50, 40, lambda lon, lat: vals)
    dem = DemSampler.from_files([p], (85.3005, 27.6895, 85.3132, 27.6995), margin_deg=0.0)
    rows, cols = np.mgrid[2:38, 2:48]
    lon, lat = (a0 + cols) / PPD, (b0 - rows) / PPD
    for order in (0, 1, 3):
        got = dem.sample_lonlat(lon, lat, order=order)
        if order == 3:  # interpolating spline: exact at knots up to float32 coefficients
            np.testing.assert_allclose(got, vals[rows, cols], rtol=0, atol=2e-3)
        else:
            np.testing.assert_array_equal(got, vals[rows, cols].astype(np.float64))
    # Half a pixel east of a centre is the mean of the two neighbours.
    mid = dem.sample_lonlat((a0 + 10.5) / PPD, (b0 - 7) / PPD)
    assert mid == pytest.approx(0.5 * (float(vals[7, 10]) + float(vals[7, 11])), abs=1e-3)


def test_gtiff_point_geo_ignore_option_cannot_shift_dem(tmp_path):
    p = tmp_path / "t.tif"
    write_dem(p, arcsec(85.3), arcsec(27.7), 20, 20, plane)
    with rasterio.Env(GTIFF_POINT_GEO_IGNORE=True):
        dem = DemSampler.from_files([p], (85.301, 27.697, 85.304, 27.699), margin_deg=0.0)
    lon, lat = (arcsec(85.3) + 5) / PPD, (arcsec(27.7) - 5) / PPD
    assert dem.sample_lonlat(lon, lat) == pytest.approx(float(np.float32(plane(lon, lat))), abs=1e-4)


def test_bilinear_and_cubic_reproduce_a_tilted_plane(tmp_path):
    p = tmp_path / "plane.tif"
    write_dem(p, arcsec(85.25), arcsec(27.75), 400, 400, plane)
    dem = DemSampler.from_files([p], (85.27, 27.66, 85.33, 27.73), margin_deg=0.01)
    rng = np.random.default_rng(3)
    lon = rng.uniform(85.27, 85.33, 500)
    lat = rng.uniform(27.66, 27.73, 500)
    np.testing.assert_allclose(dem.sample_lonlat(lon, lat, order=1), plane(lon, lat), atol=2e-3)
    np.testing.assert_allclose(dem.sample_lonlat(lon, lat, order=3), plane(lon, lat), atol=5e-3)
    # (N, 2) adapter used as trails.assign_trail_difficulty's sample_elev callable.
    assert np.array_equal(dem.heights_at_lonlat(np.c_[lon, lat]), dem.sample_lonlat(lon, lat))
    with pytest.raises(ValueError):
        dem.heights_at_lonlat(np.zeros((3, 3)))


def test_mosaic_of_adjacent_tiles_is_continuous(tmp_path):
    # Four 0.05-degree tiles meeting at (85.30, 27.70); the grids continue
    # without gap or overlap, like neighbouring Copernicus tiles.
    n = arcsec(0.05)
    paths = []
    for i, (da, db) in enumerate([(0, 0), (n, 0), (0, -n), (n, -n)]):
        p = tmp_path / f"t{i}.tif"
        write_dem(p, arcsec(85.25) + da, arcsec(27.75) + db, n, n, plane)
        paths.append(p)
    dem = DemSampler.from_files(paths, (85.26, 27.66, 85.34, 27.74), margin_deg=0.0)
    assert dem.valid_fraction == 1.0
    # Points straddling both tile boundaries (lon 85.30 - 0.5 px .. lat 27.70 ...).
    t = np.linspace(-3, 3, 241) / PPD
    np.testing.assert_allclose(dem.sample_lonlat(85.30 + t, np.full_like(t, 27.71)), plane(85.30 + t, 27.71), atol=2e-3)
    np.testing.assert_allclose(dem.sample_lonlat(np.full_like(t, 85.29), 27.70 + t), plane(85.29, 27.70 + t), atol=2e-3)
    # Input order does not matter.
    other = DemSampler.from_files(paths[::-1], (85.26, 27.66, 85.34, 27.74), margin_deg=0.0)
    np.testing.assert_array_equal(dem.data, other.data)
    assert dem.grid == other.grid


def test_voids_are_filled_with_nearest_valid_height(tmp_path):
    def with_voids(lon, lat):
        h = plane(lon, lat)
        h[10:20, 10:20] = -32767.0
        h[30, 30] = np.nan
        return h

    p = tmp_path / "voids.tif"
    a0, b0 = arcsec(85.30), arcsec(27.70)
    data = write_dem(p, a0, b0, 60, 60, with_voids, nodata=-32767.0)
    dem = DemSampler.from_files([p], (85.302, 27.688, 85.314, 27.698), margin_deg=0.0)
    assert dem.valid_fraction < 1.0
    assert np.isfinite(dem.data).all() and dem.data.min() > 0
    # A void pixel next to the hole's west edge takes the value just outside.
    assert dem.sample_lonlat((a0 + 10) / PPD, (b0 - 15) / PPD) == float(data[15, 9])
    assert float(dem.sample_lonlat((a0 + 30) / PPD, (b0 - 30) / PPD)) in {
        float(data[29, 30]), float(data[31, 30]), float(data[30, 29]), float(data[30, 31])}


def test_partial_coverage_warns_and_no_coverage_raises(tmp_path, caplog):
    p = tmp_path / "small.tif"
    write_dem(p, arcsec(85.30), arcsec(27.70), 36, 36, plane)
    with caplog.at_level(logging.WARNING, logger="ghumante_pipeline.dem"):
        dem = DemSampler.from_files([p], (85.29, 27.68, 85.32, 27.71), margin_deg=0.0)
    assert 0 < dem.valid_fraction < 1
    assert any("filling by nearest" in r.message for r in caplog.records)
    assert np.isfinite(dem.sample_lonlat(85.28, 27.72))
    with pytest.raises(ValueError, match="no valid DEM data"):
        DemSampler.from_files([p], (86.0, 27.0, 86.1, 27.1))


def test_mismatched_grids_are_rejected(tmp_path):
    a, b = tmp_path / "a.tif", tmp_path / "b.tif"
    write_dem(a, arcsec(85.30), arcsec(27.70), 20, 20, plane)
    # Same pixel size, but corners (not centres) on whole arc-seconds.
    with rasterio.open(b, "w", driver="GTiff", width=20, height=20, count=1, dtype="float32", crs="EPSG:4326",
                       transform=Affine(1 / PPD, 0, 85.305, 0, -1 / PPD, 27.70)) as ds:
        ds.write(np.zeros((1, 20, 20), np.float32))
    with pytest.raises(ValueError, match="does not match"):
        DemSampler.from_files([a, b], (85.301, 27.696, 85.304, 27.699))


# --- names, metrics -------------------------------------------------------------------
def test_copernicus_tile_names():
    names = DemSampler.copernicus_tile_names
    assert names((85.18, 27.55, 85.58, 27.83)) == ["Copernicus_DSM_COG_10_N27_00_E085_00_DEM"]
    # A box ending exactly on 28 N / starting on 85 E needs no extra tiles.
    assert names((85.0, 27.2, 85.9, 28.0)) == ["Copernicus_DSM_COG_10_N27_00_E085_00_DEM"]
    assert names((84.6, 27.2, 86.4, 28.6)) == [
        f"Copernicus_DSM_COG_10_N{lat}_00_E0{lon}_00_DEM" for lat in (27, 28) for lon in (84, 85, 86)]
    assert names((85.5, 27.5, 85.6, 27.6), margin_deg=0.6)[0] == "Copernicus_DSM_COG_10_N26_00_E084_00_DEM"
    assert names((-0.5, -0.5, -0.1, -0.1)) == ["Copernicus_DSM_COG_10_S01_00_W001_00_DEM"]


def test_native_spacing_and_spacing_quantisation(rough):
    assert 28.5 < rough.native_spacing_m() < 29.5  # 27.4 m E-W x 30.8 m N-S at 27.7 N
    m_lon, m_lat = metres_per_degree(27.7)
    assert m_lat == pytest.approx(110_800, rel=2e-3) and m_lon == pytest.approx(98_600, rel=3e-3)
    assert [quantise_spacing(s) for s in (8, 11, 12, 30, 64, 90, 256)] == [8, 8, 16, 32, 64, 64, 256]
    assert rough.filter_scale(None) is None
    assert rough.filter_scale(8) is None and rough.filter_scale(32) is None
    assert rough.filter_scale(64) == 64 and rough.filter_scale(70) == 64 and rough.filter_scale(256) == 256
    with pytest.raises(ValueError):
        quantise_spacing(0)


# --- the seam invariant -----------------------------------------------------------------
@pytest.mark.parametrize("level,n,spacing,order", [
    (10, 129, None, 1), (10, 129, 8.0, 1), (10, 129, 8.0, 3), (8, 65, 64.0, 1), (8, 129, 32.0, 3), (8, 33, 128.0, 1),
])
def test_adjacent_tiles_share_bit_identical_edges(rough, level, n, spacing, order):
    a = kathmandu_tile(level)
    east, north = a.neighbor(1, 0), a.neighbor(0, 1)
    ha = rough.sample_game(*P.grid_coords(a, n), spacing_m=spacing, order=order)
    he = rough.sample_game(*P.grid_coords(east, n), spacing_m=spacing, order=order)
    hn = rough.sample_game(*P.grid_coords(north, n), spacing_m=spacing, order=order)
    assert np.array_equal(ha[:, -1], he[:, 0])
    assert np.array_equal(ha[-1, :], hn[0, :])
    assert np.ptp(ha) > 10  # the terrain is not trivially flat


def test_results_do_not_depend_on_the_batch(rough):
    tile = kathmandu_tile(9)
    x, z = P.grid_coords(tile, 65)
    for spacing, order in ((None, 1), (32.0, 1), (128.0, 3)):
        full = rough.sample_game(x, z, spacing_m=spacing, order=order)
        for sl in (np.s_[0, :], np.s_[:, 17], np.s_[40:43, 5:9]):
            assert np.array_equal(rough.sample_game(x[sl], z[sl], spacing_m=spacing, order=order), full[sl])
        single = rough.sample_game(float(x[3, 4]), float(z[3, 4]), spacing_m=spacing, order=order)
        assert single.shape == () and single == full[3, 4]
    slope = rough.slope_deg_game(x, z, 32.0)
    assert np.array_equal(rough.slope_deg_game(x[5], z[5], 32.0), slope[5])


def test_samplers_over_different_boxes_agree(rough_dem, rough):
    other = DemSampler.from_files([rough_dem], (85.30, 27.62, 85.46, 27.76), margin_deg=0.05)
    assert other.grid != rough.grid
    x, z = P.grid_coords(kathmandu_tile(9), 65)
    for spacing in (None, 64.0, 128.0):
        assert np.array_equal(other.sample_game(x, z, spacing_m=spacing), rough.sample_game(x, z, spacing_m=spacing))


def test_slope_edges_agree_across_tiles(rough):
    a = kathmandu_tile(10)
    e = a.neighbor(1, 0)
    sa = rough.slope_deg_game(*P.grid_coords(a, 129), 8.0)
    se = rough.slope_deg_game(*P.grid_coords(e, 129), 8.0)
    assert np.array_equal(sa[:, -1], se[:, 0])
    assert (sa >= 0).all() and (sa < 90).all()


# --- LOD pre-filtering -------------------------------------------------------------------
def test_prefilter_area_averages_high_frequency_relief(tmp_path):
    # A 2-pixel checkerboard of +-100 m on a flat 2000 m plateau.
    def checker(lon, lat):
        c = np.rint(lon * PPD).astype(np.int64) + np.rint(lat * PPD).astype(np.int64)
        return 2000.0 + np.where(c % 2 == 0, 100.0, -100.0)

    p = tmp_path / "checker.tif"
    write_dem(p, arcsec(85.25), arcsec(27.78), 600, 600, checker)
    tile = kathmandu_tile(8)
    bbox = tile_bbox(tile, 0.01)
    dem = DemSampler.from_files([p], bbox, margin_deg=0.0)
    assert dem.valid_fraction == 1.0
    x, z = P.grid_coords(tile, 33)
    sel = np.s_[4:-4, 4:-4]
    raw = dem.sample_game(x, z)[sel]
    coarse = dem.sample_game(x, z, spacing_m=128.0)[sel]
    assert raw.std() > 30  # point samples alias the checkerboard
    assert coarse.std() < 1.0 and abs(coarse.mean() - 2000.0) < 1.0
    # The filtered copies are cached per quantised spacing and deterministic.
    assert np.array_equal(dem.sample_game(x, z, spacing_m=120.0)[sel], coarse)
    dem.clear_cache()
    again = DemSampler.from_files([p], bbox, margin_deg=0.0)
    assert np.array_equal(again.sample_game(x, z, spacing_m=128.0)[sel], coarse)


def test_prefilter_preserves_smooth_terrain(tmp_path):
    p = tmp_path / "plane.tif"
    write_dem(p, arcsec(85.25), arcsec(27.75), 400, 400, plane)
    dem = DemSampler.from_files([p], (85.27, 27.66, 85.33, 27.73), margin_deg=0.01)
    lon = np.linspace(85.28, 85.32, 50)
    lat = np.linspace(27.67, 27.72, 50)
    np.testing.assert_allclose(dem.sample_lonlat(lon, lat, spacing_m=256.0), plane(lon, lat), atol=0.05)


# --- slope ------------------------------------------------------------------------------
def test_slope_of_a_plane_in_game_space(tmp_path):
    # Heights rising 0.2 m per game metre eastwards: an 11.31 degree slope.
    x_ref = float(P.lonlat_to_game(85.36, 27.69)[0])

    def east_ramp(lon, lat):
        x, _ = P.lonlat_to_game(lon, lat)
        return 0.2 * (np.asarray(x) - x_ref) + 3000.0

    p = tmp_path / "ramp.tif"
    write_dem(p, arcsec(85.30), arcsec(27.75), 300, 300, east_ramp)
    tile = kathmandu_tile(10)
    dem = DemSampler.from_files([p], tile_bbox(tile, 0.01), margin_deg=0.0)
    assert dem.valid_fraction == 1.0
    x, z = P.grid_coords(tile, 17)
    for spacing in (8.0, 64.0):
        np.testing.assert_allclose(dem.slope_deg_game(x, z, spacing), math.degrees(math.atan(0.2)), atol=0.05)
    with pytest.raises(ValueError):
        dem.slope_deg_game(x, z, 0.0)


# --- input handling ------------------------------------------------------------------------
def test_order_validation_nonfinite_and_outside_points(rough):
    with pytest.raises(ValueError, match="order"):
        rough.sample_lonlat(85.3, 27.7, order=2)
    out = rough.sample_lonlat(np.array([85.3, np.nan]), np.array([27.7, 27.7]))
    assert np.isfinite(out[0]) and np.isnan(out[1])
    # Outside the mosaic: the nearest edge value (mode='nearest').
    lon_min, lat_min, lon_max, lat_max = rough.grid.bounds
    assert rough.sample_lonlat(lon_max + 1.0, 27.7) == rough.sample_lonlat(lon_max + 2.0, 27.7)
    with pytest.raises(ValueError):
        DemSampler(np.zeros((2, 2), np.float32), GeoGrid(3600.0, 3600.0, 0.5, 0.5, 0, 0, 3, 3))


@pytest.mark.realdata
@pytest.mark.skipif(not RAW_DEM.exists(), reason="Copernicus tile N27E085 not downloaded")
def test_real_dem_kathmandu():
    dem = DemSampler.from_files([RAW_DEM], (85.18, 27.55, 85.58, 27.83))
    assert dem.valid_fraction == 1.0
    h = float(dem.sample_lonlat(85.324, 27.717))
    assert 1280.0 < h < 1380.0  # DSM at Naxal, Kathmandu: about 1300 m
    # Pixel [0, 0] of the file is centred on exactly 85 E, 28 N.
    with rasterio.open(RAW_DEM) as ds:
        v00 = float(ds.read(1, window=rasterio.windows.Window(0, 0, 1, 1))[0, 0])
    corner = DemSampler.from_files([RAW_DEM], (85.0, 27.99, 85.01, 28.0), margin_deg=0.0)
    assert float(corner.sample_lonlat(85.0, 28.0)) == v00
    x, z = P.lonlat_to_game(85.324, 27.717)
    assert abs(float(dem.sample_game(x, z, spacing_m=256.0)) - h) < 30.0
    assert 0 <= float(dem.slope_deg_game(x, z, 30.0)) < 20.0
