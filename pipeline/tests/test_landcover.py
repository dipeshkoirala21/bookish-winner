"""Tests for landcover.py: windowed WorldCover reads, nearest, mode and built-up fraction."""

from __future__ import annotations

from pathlib import Path

import numpy as np
import pytest
import rasterio
from rasterio.transform import Affine

from ghumante_pipeline import projection as P
from ghumante_pipeline.landcover import CLASS_CODES, LandcoverSampler

PPD = 12000  # WorldCover: 1/12000 degree
RAW_WC = (Path(__file__).resolve().parents[1] / "data" / "raw" / "worldcover"
          / "ESA_WorldCover_10m_2021_v200_N27E084_Map.tif")


def write_wc(path: Path, left: int, top: int, data: np.ndarray) -> None:
    """WorldCover-like GeoTIFF (pixel-area convention, nodata 0) whose top-left
    pixel corner is at (left, top) / 12000 degrees."""
    h, w = data.shape
    with rasterio.open(path, "w", driver="GTiff", width=w, height=h, count=1, dtype="uint8", crs="EPSG:4326",
                       transform=Affine(1 / PPD, 0, left / PPD, 0, -1 / PPD, top / PPD), nodata=0,
                       tiled=True, blockxsize=256, blockysize=256) as ds:
        ds.write(data.astype(np.uint8), 1)


def idx(deg: float) -> int:
    return round(deg * PPD)


def centre(left: int, top: int, row, col):
    """lon/lat of the centre of pixel (row, col) of a tile written with write_wc."""
    return (left + np.asarray(col) + 0.5) / PPD, (top - np.asarray(row) - 0.5) / PPD


def tile_bbox(tile: P.TileId, pad_deg: float) -> tuple[float, float, float, float]:
    lon, lat = P.game_to_lonlat([tile.x0, tile.x0 + tile.size, tile.x0, tile.x0 + tile.size],
                                [tile.z0, tile.z0, tile.z0 + tile.size, tile.z0 + tile.size])
    return (min(lon) - pad_deg, min(lat) - pad_deg, max(lon) + pad_deg, max(lat) + pad_deg)


def kathmandu_tile(level: int) -> P.TileId:
    x, z = P.lonlat_to_game(85.36, 27.69)
    return P.tile_at(level, float(x), float(z))


# A 0.1 x 0.1 degree patchwork around the level-8 test tile: 40 x 40-pixel
# patches of random classes with salt-and-pepper noise.
PATCH_LEFT, PATCH_TOP, PATCH_N = idx(85.32), idx(27.72), 1200


@pytest.fixture(scope="module")
def patch_file(tmp_path_factory) -> Path:
    rng = np.random.default_rng(7)
    coarse = rng.choice(CLASS_CODES, size=(PATCH_N // 40, PATCH_N // 40))
    data = np.kron(coarse, np.ones((40, 40), dtype=np.uint8))
    salt = rng.random(data.shape) < 0.2
    data[salt] = rng.choice(CLASS_CODES, size=int(salt.sum()))
    p = tmp_path_factory.mktemp("wc") / "patch.tif"
    write_wc(p, PATCH_LEFT, PATCH_TOP, data)
    return p


@pytest.fixture(scope="module")
def patch(patch_file) -> LandcoverSampler:
    return LandcoverSampler.from_files([patch_file], tile_bbox(kathmandu_tile(8), 0.005))


# --- names and windows --------------------------------------------------------------
def test_worldcover_tile_names():
    names = LandcoverSampler.worldcover_tile_names
    assert names((85.18, 27.55, 85.58, 27.83)) == ["ESA_WorldCover_10m_2021_v200_N27E084_Map"]
    # 30 N is the top edge of the N27 row; 87 E starts the next column.
    assert names((84.0, 27.5, 86.9, 30.0)) == ["ESA_WorldCover_10m_2021_v200_N27E084_Map"]
    nepal = names((80.06, 26.35, 88.2, 30.45))
    assert len(nepal) == 12 and nepal[0] == "ESA_WorldCover_10m_2021_v200_N24E078_Map"
    assert "ESA_WorldCover_10m_2021_v200_N30E087_Map" in nepal
    assert names((-1.0, -1.0, -0.5, -0.5)) == ["ESA_WorldCover_10m_2021_v200_S03W003_Map"]


def test_reads_only_the_window_it_needs(tmp_path):
    big = np.full((3000, 3000), 30, dtype=np.uint8)  # 0.25 x 0.25 degrees
    big[1500:, :] = 40
    p = tmp_path / "big.tif"
    write_wc(p, idx(85.0), idx(27.9), big)
    lc = LandcoverSampler.from_files([p], (85.10, 27.77, 85.11, 27.78), margin_deg=0.002)
    assert lc.data.size < 0.01 * big.size
    assert lc.sample_lonlat(85.105, 27.776) == 30 and lc.sample_lonlat(85.105, 27.771) == 40


# --- nearest -------------------------------------------------------------------------
def test_nearest_returns_the_pixel_containing_the_point(tmp_path):
    rng = np.random.default_rng(1)
    data = rng.choice(CLASS_CODES, size=(60, 80)).astype(np.uint8)
    p = tmp_path / "rand.tif"
    left, top = idx(85.30), idx(27.70)
    write_wc(p, left, top, data)
    lc = LandcoverSampler.from_files([p], (85.3001, 27.6951, 85.3065, 27.6999), margin_deg=0.0)
    rows, cols = np.mgrid[0:60, 0:80]
    lon, lat = centre(left, top, rows, cols)
    np.testing.assert_array_equal(lc.sample_lonlat(lon, lat), data)
    # Anywhere inside a pixel, not just at its centre.
    q = 0.45 / PPD
    np.testing.assert_array_equal(lc.sample_lonlat(lon + q, lat - q), data)
    np.testing.assert_array_equal(lc.sample_lonlat(lon - q, lat + q), data)
    assert lc.sample_lonlat(lon[5, 5], lat[5, 5]).dtype == np.uint8
    # Spacings up to 3x native still sample the pixel.
    np.testing.assert_array_equal(lc.sample_lonlat(lon, lat, spacing_m=16.0), data)


def test_two_tiles_mosaic_and_outside_is_nodata(tmp_path):
    a = np.full((100, 100), 10, np.uint8)
    b = np.full((100, 100), 40, np.uint8)
    pa, pb = tmp_path / "a.tif", tmp_path / "b.tif"
    write_wc(pa, idx(85.30), idx(27.70), a)
    write_wc(pb, idx(85.30) + 100, idx(27.70), b)  # directly east
    lc = LandcoverSampler.from_files([pb, pa], (85.301, 27.692, 85.315, 27.699), margin_deg=0.0)
    lon_edge = (idx(85.30) + 100) / PPD
    assert lc.sample_lonlat(lon_edge - 1e-7, 27.695) == 10
    assert lc.sample_lonlat(lon_edge + 1e-7, 27.695) == 40
    assert lc.sample_lonlat(85.31, 27.65) == 0  # outside the mosaic
    out = lc.sample_lonlat(np.array([np.nan, 85.305]), np.array([27.695, 27.695]))
    assert out.tolist() == [0, 10]


# --- mode --------------------------------------------------------------------------------
def test_mode_scale_threshold(patch):
    assert 8.0 < patch.native_spacing_m() < 9.5
    assert patch.mode_scale(None) is None and patch.mode_scale(16.0) is None
    assert patch.mode_scale(32.0) == 32.0 and patch.mode_scale(100.0) == 128.0
    fy, fx = patch.mode_block_px(64.0)
    assert (fy, fx) == (3, 4)  # about 32 m blocks: 9.24 m x 8.20 m pixels at 28 N


def test_mode_picks_the_majority_class(tmp_path):
    rng = np.random.default_rng(5)
    data = np.where(rng.random((600, 600)) < 0.7, 30, 10).astype(np.uint8)
    p = tmp_path / "maj.tif"
    left, top = idx(85.30), idx(27.70)
    write_wc(p, left, top, data)
    lc = LandcoverSampler.from_files([p], (85.305, 27.655, 85.345, 27.695), margin_deg=0.0)
    rows, cols = np.mgrid[100:500:37, 100:500:41]
    lon, lat = centre(left, top, rows, cols)
    near = lc.sample_lonlat(lon, lat)
    assert set(np.unique(near)) == {10, 30}
    # 64 m windows hold ~100 pixels, so an occasional sample may flip.
    assert (lc.sample_lonlat(lon, lat, spacing_m=64.0) == 30).mean() > 0.95
    for spacing in (128.0, 256.0):
        assert (lc.sample_lonlat(lon, lat, spacing_m=spacing) == 30).all()


def test_mode_ties_go_to_the_lowest_code_and_nodata_does_not_vote(tmp_path):
    # Alternate 1-pixel columns of 40 and 20: every window with an even number
    # of columns is an exact tie.
    data = np.where(np.arange(400) % 2 == 0, 40, 20).astype(np.uint8)[None, :].repeat(400, axis=0)
    p = tmp_path / "tie.tif"
    left, top = idx(85.30), idx(27.70)
    write_wc(p, left, top, data)
    lc = LandcoverSampler.from_files([p], (85.302, 27.668, 85.332, 27.698), margin_deg=0.0)
    fy, fx = lc.mode_block_px(64.0)
    assert (3 * fx) % 2 == 0  # the window really is a tie
    lon, lat = centre(left, top, np.arange(50, 350, 13), np.arange(50, 350, 13))
    assert (lc.sample_lonlat(lon, lat, spacing_m=64.0) == 20).all()

    # Mostly nodata with a little shrubland: the shrubland wins.
    sparse = np.zeros((400, 400), np.uint8)
    sparse[::7, ::5] = 20
    p2 = tmp_path / "sparse.tif"
    write_wc(p2, left, top, sparse)
    lc2 = LandcoverSampler.from_files([p2], (85.302, 27.668, 85.332, 27.698), margin_deg=0.0)
    assert (lc2.sample_lonlat(lon, lat, spacing_m=128.0) == 20).all()
    assert lc2.sample_lonlat(lon[0], lat[0]) in (0, 20)


def test_mode_is_a_pure_function_of_position(patch_file, patch):
    tile = kathmandu_tile(8)
    x, z = P.grid_coords(tile, 65)
    full = patch.sample_game(x, z, spacing_m=64.0)
    assert len(np.unique(full)) > 3
    assert np.array_equal(patch.sample_game(x[10], z[10], spacing_m=64.0), full[10])
    assert patch.sample_game(float(x[3, 7]), float(z[3, 7]), spacing_m=64.0) == full[3, 7]
    # A sampler over a different box anchors its blocks to the same global grid.
    other = LandcoverSampler.from_files([patch_file], tile_bbox(tile, 0.012), margin_deg=0.01)
    assert other.grid != patch.grid
    assert np.array_equal(other.sample_game(x, z, spacing_m=64.0), full)
    assert np.array_equal(other.sample_game(x, z), patch.sample_game(x, z))


@pytest.mark.parametrize("spacing", [None, 16.0, 64.0, 128.0])
def test_adjacent_game_tiles_share_edges(patch_file, spacing):
    a = kathmandu_tile(9)
    east, north = a.neighbor(1, 0), a.neighbor(0, 1)
    bbox = tile_bbox(P.TileId(8, a.tx >> 1, a.ty >> 1), 0.005)
    lc = LandcoverSampler.from_files([patch_file], bbox)
    ga = lc.sample_game(*P.grid_coords(a, 65), spacing_m=spacing)
    ge = lc.sample_game(*P.grid_coords(east, 65), spacing_m=spacing)
    gn = lc.sample_game(*P.grid_coords(north, 65), spacing_m=spacing)
    assert np.array_equal(ga[:, -1], ge[:, 0])
    assert np.array_equal(ga[-1, :], gn[0, :])


# --- built-up fraction ----------------------------------------------------------------------
def test_built_up_fraction(tmp_path):
    data = np.full((1200, 1200), 40, np.uint8)
    data[:, :600] = 50  # west half built-up
    data[:100, 600:] = 0  # nodata in the north-east corner does not count
    p = tmp_path / "bu.tif"
    left, top = idx(85.30), idx(27.70)
    write_wc(p, left, top, data)
    lc = LandcoverSampler.from_files([p], (85.305, 27.605, 85.395, 27.695), margin_deg=0.0)
    lat = centre(left, top, 600, 0)[1]
    lon_mid = (left + 600) / PPD
    f = lc.built_up_fraction_lonlat(np.array([lon_mid - 0.02, lon_mid, lon_mid + 0.02]), np.full(3, lat), 300.0)
    assert f[0] == pytest.approx(1.0) and f[2] == pytest.approx(0.0)
    assert f[1] == pytest.approx(0.5, abs=0.06)
    # Nodata excluded: next to the nodata block the built-up share stays 1 on the west side.
    lat_n = centre(left, top, 60, 0)[1]
    assert lc.built_up_fraction_lonlat(lon_mid - 0.01, lat_n, 200.0) == pytest.approx(1.0)
    x, z = P.lonlat_to_game(lon_mid, lat)
    g = lc.built_up_fraction_game(np.array([x, x]), np.array([z, z]), 300.0)
    assert g.shape == (2,) and g[0] == g[1] == pytest.approx(f[1], abs=0.02)
    with pytest.raises(ValueError):
        lc.built_up_fraction_lonlat(85.3, 27.7, 0.0)


def test_built_up_fraction_of_random_cover(tmp_path):
    rng = np.random.default_rng(11)
    data = np.where(rng.random((900, 900)) < 0.25, 50, 10).astype(np.uint8)
    p = tmp_path / "r.tif"
    left, top = idx(85.30), idx(27.70)
    write_wc(p, left, top, data)
    lc = LandcoverSampler.from_files([p], (85.305, 27.630, 85.370, 27.695), margin_deg=0.0)
    lon, lat = centre(left, top, np.arange(300, 600, 50), np.arange(300, 600, 50))
    f = lc.built_up_fraction_lonlat(lon, lat, 250.0)
    np.testing.assert_allclose(f, 0.25, atol=0.03)
    assert np.array_equal(lc.built_up_fraction_lonlat(lon[:2], lat[:2], 250.0), f[:2])


@pytest.mark.realdata
@pytest.mark.skipif(not RAW_WC.exists(), reason="WorldCover tile N27E084 not downloaded")
def test_real_worldcover_kathmandu():
    lc = LandcoverSampler.from_files([RAW_WC], (85.28, 27.68, 85.36, 27.74))
    assert lc.data.shape[0] * lc.data.shape[1] < 2_000_000  # a window, not the 36000^2 tile
    assert lc.sample_lonlat(85.3125, 27.7125) == 50  # Thamel
    assert lc.sample_lonlat(85.307, 27.704) == 50  # Kathmandu Durbar Square
    # (85.324, 27.717) itself is a ~150 m garden plot (class 40) in the 10 m
    # data; at coarse LOD the area is built-up.
    x, z = P.lonlat_to_game(85.324, 27.717)
    assert lc.sample_game(x, z, spacing_m=256.0) == 50
    assert lc.built_up_fraction_game(x, z, 500.0) > 0.6
