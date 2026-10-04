"""Tests for biomes.py: rule table, zones, value noise and seam-safe biome grids."""

from __future__ import annotations

import struct
from pathlib import Path

import numpy as np
import pytest
import rasterio
from rasterio.transform import Affine
from scipy import ndimage
from shapely.geometry import box

from ghumante_pipeline import binio
from ghumante_pipeline import projection as P
from ghumante_pipeline.biomes import (
    BiomeZones,
    Zone,
    _fnv_lattice,
    biome_grid_for_tile,
    classify,
    compute_biome_grid,
    default_zones,
    load_zones,
    value_noise,
    zone_at_game,
)
from ghumante_pipeline.dem import DemSampler
from ghumante_pipeline.landcover import CLASS_CODES, LandcoverSampler
from ghumante_pipeline.model import AreaKind as A
from ghumante_pipeline.model import Biome as B

RAW = Path(__file__).resolve().parents[1] / "data" / "raw"
RAW_DEM = RAW / "dem" / "Copernicus_DSM_COG_10_N27_00_E085_00_DEM.tif"
RAW_WC = RAW / "worldcover" / "ESA_WorldCover_10m_2021_v200_N27E084_Map.tif"

T, TH, KV, KS = Zone.TERAI, Zone.TRANS_HIMALAYA, Zone.KATHMANDU_VALLEY_FLOOR, Zone.KHUMBU_SHERPA


# --- classify: the rule table --------------------------------------------------------
# (worldcover, elevation m, slope deg, area kind, zone, noise) -> biome
CASES = [
    # water, ice, snow
    ((40, 1500, 10, A.WATER_RIVER, 0, .5), B.WATER),
    ((10, 300, 0, A.WATER_POND, 0, .5), B.WATER),
    ((10, 3000, 5, A.GLACIER, 0, .5), B.GLACIER),
    ((80, 800, 1, A.SAND_SHINGLE, 0, .5), B.RIVERBED_GRAVEL),
    ((80, 2000, 3, A.FOREST, 0, .5), B.WATER),
    ((80, 1300, 0, A.PARK, KV, .5), B.WATER),
    ((70, 5200, 30, 0, 0, .5), B.SNOW),
    ((70, 4400, 30, 0, 0, .5), B.GLACIER),
    ((70, 2500, 30, 0, 0, .5), B.HILL_FOREST),  # low "snow": unknown cover -> fallback
    ((70, 2500, 30, A.MEADOW, 0, .5), B.HILL_GRASSLAND),
    # settlements
    ((50, 1300, 2, A.PARK, KV, .5), B.URBAN_GREEN),
    ((10, 1300, 2, A.CEMETERY, 0, .5), B.URBAN_GREEN),
    ((30, 1300, 2, A.PITCH, 0, .5), B.URBAN_GREEN),
    ((10, 1300, 2, A.COMMERCIAL, 0, .5), B.URBAN_DENSE),
    ((40, 900, 2, A.INDUSTRIAL, 0, .5), B.URBAN_DENSE),
    ((60, 1300, 0, A.PEDESTRIAN, KV, .5), B.URBAN_DENSE),
    ((10, 1300, 5, A.RELIGIOUS, 0, .5), B.URBAN_GREEN),
    ((50, 1300, 5, A.RELIGIOUS, 0, .5), B.URBAN_DENSE),
    ((30, 1330, 1, A.AERODROME, KV, .5), B.URBAN_GREEN),
    ((50, 1330, 1, A.AERODROME, KV, .5), B.URBAN_DENSE),
    ((10, 1400, 5, A.RESIDENTIAL, 0, .5), B.URBAN_GREEN),
    ((40, 1300, 2, A.RESIDENTIAL, KV, .5), B.VALLEY_CROPLAND),
    ((40, 1500, 15, A.RESIDENTIAL, 0, .5), B.HILL_TERRACES),
    ((60, 1300, 2, A.RESIDENTIAL, 0, .5), B.URBAN_DENSE),
    ((0, 1300, 2, A.RESIDENTIAL, 0, .5), B.URBAN_DENSE),
    ((50, 1300, 2, 0, 0, .5), B.URBAN_DENSE),
    ((50, 2000, 20, A.FOREST, 0, .5), B.URBAN_DENSE),
    # specific land use, wetlands
    ((10, 900, 5, A.ORCHARD, 0, .5), B.ORCHARD),
    ((20, 1200, 15, A.TEA_GARDEN, 0, .5), B.TEA_GARDEN),
    ((90, 100, 0, 0, T, .5), B.WETLAND),
    ((95, 100, 0, 0, T, .5), B.WETLAND),
    ((40, 150, 0, A.WETLAND, 0, .5), B.WETLAND),
    # cropland
    ((40, 3200, 5, 0, TH, .5), B.TRANS_HIMALAYAN_CROPLAND),
    ((40, 150, 0, 0, 0, .3), B.TERAI_PADDY),
    ((40, 150, 0, 0, 0, .9), B.TERAI_CROPLAND),
    ((40, 450, 1, 0, T, .2), B.TERAI_PADDY),
    ((40, 1300, 2, 0, KV, .5), B.VALLEY_CROPLAND),
    ((40, 1350, 15, 0, KV, .5), B.VALLEY_CROPLAND),
    ((40, 3500, 2, 0, KS, .5), B.HILL_TERRACES),
    ((40, 1500, 12, 0, 0, .5), B.HILL_TERRACES),
    ((40, 3700, 12, 0, 0, .5), B.HILL_TERRACES),
    ((40, 1500, 3, 0, 0, .5), B.VALLEY_CROPLAND),
    ((40, 400, 12, 0, 0, .5), B.VALLEY_CROPLAND),
    ((40, 4200, 12, 0, 0, .5), B.VALLEY_CROPLAND),
    ((30, 1500, 12, A.FARMLAND, 0, .5), B.HILL_TERRACES),  # fallow field seen as grass
    ((0, 150, 0, A.FARMLAND, 0, .3), B.TERAI_PADDY),
    # trees
    ((10, 200, 5, 0, 0, .5), B.TERAI_SAL_FOREST),
    ((10, 600, 5, 0, T, .5), B.TERAI_SAL_FOREST),
    ((10, 600, 25, 0, 0, .5), B.CHURE_FOREST),
    ((10, 2000, 30, 0, 0, .5), B.HILL_FOREST),
    ((10, 1500, 20, A.FARMLAND, 0, .5), B.HILL_FOREST),  # trees inside a coarse farmland polygon
    ((10, 3500, 30, 0, 0, .5), B.SUBALPINE_FOREST),
    ((10, 4100, 30, 0, 0, .5), B.ALPINE_SCRUB),
    ((0, 2000, 30, A.FOREST, 0, .5), B.HILL_FOREST),
    # shrub
    ((20, 3500, 10, 0, TH, .5), B.TRANS_HIMALAYAN_STEPPE),
    ((20, 200, 1, 0, 0, .5), B.TERAI_GRASSLAND),
    ((20, 1500, 20, 0, 0, .5), B.HILL_SCRUB),
    ((20, 3500, 20, 0, 0, .5), B.ALPINE_SCRUB),
    ((0, 1500, 20, A.SCRUB, 0, .5), B.HILL_SCRUB),
    # grass and moss
    ((30, 4000, 10, 0, TH, .5), B.TRANS_HIMALAYAN_STEPPE),
    ((30, 200, 1, 0, 0, .5), B.TERAI_GRASSLAND),
    ((30, 1500, 25, 0, 0, .5), B.HILL_GRASSLAND),
    ((30, 4200, 25, 0, 0, .5), B.ALPINE_MEADOW),
    ((0, 4200, 25, A.MEADOW, 0, .5), B.ALPINE_MEADOW),
    ((0, 1500, 25, A.GRASSLAND, 0, .5), B.HILL_GRASSLAND),
    ((100, 4500, 25, 0, 0, .5), B.ALPINE_MEADOW),
    # bare
    ((60, 800, 2, 0, 0, .5), B.RIVERBED_GRAVEL),
    ((60, 150, 1, 0, T, .5), B.RIVERBED_GRAVEL),
    ((60, 1300, 2, 0, KV, .5), B.BARE_SOIL),  # brick kilns, building sites
    ((60, 800, 2, A.BARE_ROCK, 0, .5), B.SCREE_ROCK),
    ((60, 4000, 10, 0, TH, .5), B.TRANS_HIMALAYAN_STEPPE),
    ((60, 5200, 10, 0, TH, .5), B.MORAINE),
    ((60, 4600, 10, 0, 0, .5), B.MORAINE),
    ((60, 4600, 35, 0, 0, .5), B.SCREE_ROCK),
    ((60, 2000, 20, 0, 0, .5), B.BARE_SOIL),
    ((60, 2000, 40, 0, 0, .5), B.SCREE_ROCK),
    ((60, 3500, 10, 0, 0, .5), B.SCREE_ROCK),
    ((0, 4600, 10, A.SCREE, 0, .5), B.MORAINE),
    # no cover information: elevation bands
    ((0, 200, 0, 0, 0, .5), B.TERAI_CROPLAND),
    ((0, 700, 10, 0, 0, .5), B.CHURE_FOREST),
    ((0, 2000, 10, 0, 0, .5), B.HILL_FOREST),
    ((0, 3500, 10, 0, 0, .5), B.SUBALPINE_FOREST),
    ((0, 4300, 10, 0, 0, .5), B.ALPINE_MEADOW),
    ((0, 5000, 10, 0, 0, .5), B.SCREE_ROCK),
    ((0, 6000, 10, 0, 0, .5), B.SNOW),
    ((0, 4000, 10, 0, TH, .5), B.TRANS_HIMALAYAN_STEPPE),
    ((0, 5600, 10, 0, TH, .5), B.SNOW),
    ((0, 400, 10, A.PROTECTED, T, .5), B.TERAI_CROPLAND),
]


@pytest.mark.parametrize("inputs,expected", CASES, ids=[f"{c[0]}->{c[1].name}" for c in CASES])
def test_classify_rule(inputs, expected):
    got = classify(*inputs)
    assert got.dtype == np.uint8 and got.shape == ()
    assert B(int(got)) == expected


def test_classify_vectorised_matches_scalar_rules():
    cols = [np.array(c) for c in zip(*(c[0] for c in CASES))]
    got = classify(*cols)
    assert got.tolist() == [int(c[1]) for c in CASES]
    # Broadcasting: a whole grid against scalar zone/noise.
    grid = classify(np.full((4, 5), 10), np.linspace(100, 4500, 20).reshape(4, 5), 10.0, 0, Zone.DEFAULT, 0.5)
    assert grid.shape == (4, 5) and grid[0, 0] == B.TERAI_SAL_FOREST and grid[-1, -1] == B.ALPINE_SCRUB


def test_noise_dithers_elevation_bands():
    # 2950 m forest: 2850 m "effective" with noise 0, 3049 m with noise 0.99.
    assert classify(10, 2950, 20, 0, 0, 0.0) == B.HILL_FOREST
    assert classify(10, 2950, 20, 0, 0, 0.99) == B.SUBALPINE_FOREST
    assert classify(70, 4750, 20, 0, 0, 0.0) == B.GLACIER
    assert classify(70, 4750, 20, 0, 0, 0.99) == B.SNOW


def test_classify_always_assigns_a_valid_biome():
    rng = np.random.default_rng(0)
    n = 20000
    wc = rng.choice(np.r_[0, CLASS_CODES, 255], n)
    out = classify(wc, rng.uniform(-50, 8800, n), rng.uniform(0, 80, n), rng.integers(0, 27, n),
                   rng.integers(0, 5, n), rng.random(n))
    assert (out != B.NONE).all()
    assert set(np.unique(out)) <= {int(b) for b in B}


# --- zones -------------------------------------------------------------------------------
KNOWN_PLACES = {
    "kathmandu_durbar_square": ((85.307, 27.704), KV),
    "bhaktapur": ((85.428, 27.672), KV),
    "patan": ((85.325, 27.673), KV),
    "lo_manthang": ((83.955, 29.18), TH),
    "muktinath": ((83.871, 28.817), TH),
    "manang": ((84.017, 28.666), TH),
    "namche_bazaar": ((86.714, 27.805), KS),
    "lukla": ((86.729, 27.687), KS),
    "everest_base_camp": ((86.853, 28.004), KS),
    "birgunj": ((84.88, 27.01), T),
    "janakpur": ((85.92, 26.73), T),
    "nepalgunj": ((81.62, 28.05), T),
    "biratnagar": ((87.27, 26.45), T),
    "pokhara": ((83.985, 28.21), Zone.DEFAULT),
    "hetauda": ((85.03, 27.43), Zone.DEFAULT),
    "jomsom": ((83.723, 28.781), Zone.DEFAULT),
    "simikot": ((81.82, 29.97), Zone.DEFAULT),
    "nagarkot": ((85.52, 27.715), Zone.DEFAULT),
    "budhanilkantha": ((85.362, 27.778), Zone.DEFAULT),
}


@pytest.mark.parametrize("name", sorted(KNOWN_PLACES))
def test_zone_polygons_contain_known_places(name):
    (lon, lat), zone = KNOWN_PLACES[name]
    zones = load_zones()
    assert Zone(int(zones.at_lonlat(lon, lat))) == zone
    x, z = P.lonlat_to_game(lon, lat)
    assert Zone(int(zone_at_game(x, z))) == zone
    assert Zone(int(zone_at_game(x, z, zones))) == zone


def test_zones_are_vectorised():
    lonlat = np.array([v[0] for v in KNOWN_PLACES.values()])
    expected = [int(v[1]) for v in KNOWN_PLACES.values()]
    assert default_zones().at_lonlat(lonlat[:, 0], lonlat[:, 1]).tolist() == expected
    x, z = P.lonlat_to_game(lonlat[:, 0], lonlat[:, 1])
    assert zone_at_game(np.asarray(x).reshape(1, -1), np.asarray(z).reshape(1, -1)).shape == (1, len(expected))


def test_zone_file_validation(tmp_path):
    ring = [[85.0, 27.0], [85.1, 27.0], [85.1, 27.1]]

    def write(text: str) -> Path:
        p = tmp_path / "z.yaml"
        p.write_text(text, encoding="utf-8")
        return p

    ok = load_zones(write(f"zones:\n  - {{id: terai, code: 1, polygons: [{ring}]}}\n"
                          f"  - {{id: khumbu_sherpa, polygons: [{ring}]}}\n"))
    assert isinstance(ok, BiomeZones) and ok.at_lonlat(85.08, 27.02) == KS  # later entry wins
    with pytest.raises(ValueError, match="unknown zone"):
        load_zones(write(f"zones:\n  - {{id: himalaya, polygons: [{ring}]}}\n"))
    with pytest.raises(ValueError, match="expected 2"):
        load_zones(write(f"zones:\n  - {{id: trans_himalaya, code: 3, polygons: [{ring}]}}\n"))
    with pytest.raises(ValueError, match="invalid polygon"):
        load_zones(write("zones:\n  - {id: terai, polygons: [[[85, 27], [85.1, 27.1], [85.1, 27], [85, 27.1]]]}\n"))
    with pytest.raises(ValueError, match="no polygons"):
        load_zones(write("zones:\n  - {id: terai, polygons: []}\n"))


# --- value noise -------------------------------------------------------------------------
def test_value_noise_range_determinism_and_purity():
    rng = np.random.default_rng(2)
    x = rng.uniform(0, 800_000, 5000)
    z = rng.uniform(0, 450_000, 5000)
    v = value_noise(x, z)
    assert v.dtype == np.float64 and (v >= 0).all() and (v < 1).all()
    assert 0.4 < v.mean() < 0.6 and v.std() > 0.15
    assert np.array_equal(value_noise(x, z), v)
    assert np.array_equal(value_noise(x[7:9], z[7:9]), v[7:9])
    assert value_noise(float(x[3]), float(z[3])) == v[3]
    assert not np.array_equal(value_noise(x, z, seed=1), v)
    # Smooth at the cell scale: neighbours 1 m apart differ little.
    assert np.abs(value_noise(x + 1.0, z) - v).max() < 0.03
    # Lattice values: exactly the hashed values at cell corners, independent of cell size.
    assert value_noise(400.0, 600.0, cell_m=200.0) == value_noise(2.0, 3.0, cell_m=1.0)


def test_lattice_hash_is_fnv1a_of_the_documented_bytes():
    for ix, iz, seed in [(0, 0, 0), (1, 2, 3), (-5, 70000, 99), (2**31 - 1, -(2**31), 2**32 - 1)]:
        ref = binio.fnv1a32(struct.pack("<iiI", ix, iz, seed))
        assert int(_fnv_lattice(np.array(ix), np.array(iz), seed)) == ref


# --- biome grids (synthetic rasters) --------------------------------------------------------
DEM_PPD, WC_PPD = 3600, 12000


def write_dem(path: Path, lon0: float, lat0: float, w: int, h: int, data: np.ndarray) -> None:
    a0, b0 = round(lon0 * DEM_PPD), round(lat0 * DEM_PPD)
    with rasterio.Env(GTIFF_POINT_GEO_IGNORE=False), \
            rasterio.open(path, "w", driver="GTiff", width=w, height=h, count=1, dtype="float32", crs="EPSG:4326",
                          transform=Affine(1 / DEM_PPD, 0, (a0 - .5) / DEM_PPD, 0, -1 / DEM_PPD,
                                           (b0 + .5) / DEM_PPD)) as ds:
        ds.write(data.astype(np.float32), 1)
        ds.update_tags(AREA_OR_POINT="Point")


def write_wc(path: Path, lon0: float, lat0: float, data: np.ndarray) -> None:
    h, w = data.shape
    with rasterio.open(path, "w", driver="GTiff", width=w, height=h, count=1, dtype="uint8", crs="EPSG:4326",
                       transform=Affine(1 / WC_PPD, 0, round(lon0 * WC_PPD) / WC_PPD, 0, -1 / WC_PPD,
                                        round(lat0 * WC_PPD) / WC_PPD), nodata=0) as ds:
        ds.write(data.astype(np.uint8), 1)


def kathmandu_tile(level: int) -> P.TileId:
    x, z = P.lonlat_to_game(85.36, 27.69)
    return P.tile_at(level, float(x), float(z))


@pytest.fixture(scope="module")
def samplers(tmp_path_factory):
    d = tmp_path_factory.mktemp("biome")
    rng = np.random.default_rng(9)
    # Relief from 200 m to ~4600 m, rough enough to cross every elevation band.
    yy, xx = np.mgrid[0:600, 0:600]
    relief = 200.0 + 4400.0 * (xx / 600.0) + 200.0 * np.sin(yy / 9.0) + 60.0 * ndimage.gaussian_filter(
        rng.normal(size=(600, 600)), 1.5)
    write_dem(d / "dem.tif", 85.28, 27.76, 600, 600, relief)
    coarse = rng.choice(CLASS_CODES, size=(30, 30))
    wc = np.kron(coarse, np.ones((60, 60), dtype=np.uint8))
    salt = rng.random(wc.shape) < 0.15
    wc[salt] = rng.choice(CLASS_CODES, size=int(salt.sum()))
    write_wc(d / "wc.tif", 85.28, 27.76, wc)
    bbox = (85.30, 27.62, 85.44, 27.74)
    return DemSampler.from_files([d / "dem.tif"], bbox, 0.01), LandcoverSampler.from_files([d / "wc.tif"], bbox, 0.01)


@pytest.mark.parametrize("level,n", [(10, 65), (9, 65), (8, 65), (8, 33)])
def test_biome_grids_of_neighbouring_tiles_share_edges(samplers, level, n):
    dem, lc = samplers
    a = kathmandu_tile(level)
    east, north = a.neighbor(1, 0), a.neighbor(0, 1)
    xe = east.x0
    areas = [  # one polygon crossing both shared edges, one ending exactly on the east edge
        (box(xe - 700, a.z0 + 0.3 * a.size, xe + 300, a.z0 + 1.2 * a.size), A.FARMLAND),
        (box(xe - 0.25 * a.size, a.z0, xe, a.z0 + 0.5 * a.size), A.WATER_LAKE),
    ]
    ga = biome_grid_for_tile(a, n, dem, lc, areas)
    ge = biome_grid_for_tile(east, n, dem, lc, areas)
    gn = biome_grid_for_tile(north, n, dem, lc, areas)
    assert ga.shape == (n, n) and ga.dtype == np.uint8
    assert np.array_equal(ga[:, -1], ge[:, 0])
    assert np.array_equal(ga[-1, :], gn[0, :])
    assert len(np.unique(ga)) >= 4
    assert (ga == B.WATER).any()


def test_compute_biome_grid_matches_classify_on_sampled_inputs(samplers):
    dem, lc = samplers
    tile = kathmandu_tile(9)
    x, z = P.grid_coords(tile, 33)
    spacing = tile.size / 32
    expected = classify(lc.sample_game(x, z, spacing), dem.sample_game(x, z, spacing),
                        dem.slope_deg_game(x, z, spacing), 0, zone_at_game(x, z), value_noise(x, z))
    got = compute_biome_grid(x, z, spacing, dem, lc)
    assert np.array_equal(got, expected)
    assert np.array_equal(compute_biome_grid(x[4], z[4], spacing, dem, lc), got[4])


@pytest.mark.realdata
@pytest.mark.skipif(not (RAW_DEM.exists() and RAW_WC.exists()), reason="raster tiles not downloaded")
def test_real_biomes_kathmandu():
    bbox = (85.28, 27.68, 85.36, 27.74)
    dem = DemSampler.from_files([RAW_DEM], bbox)
    lc = LandcoverSampler.from_files([RAW_WC], bbox)
    x, z = P.lonlat_to_game(85.3125, 27.7125)  # Thamel
    tile = P.tile_at(10, float(x), float(z))
    g = biome_grid_for_tile(tile, 65, dem, lc)
    assert (g == B.URBAN_DENSE).mean() > 0.7
    assert (zone_at_game(*P.grid_coords(tile, 65)) == KV).all()
