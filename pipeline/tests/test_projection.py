"""Projection round trips, known values and quadtree math."""

import math

import numpy as np
import pytest

from ghumante_pipeline import projection as p


def test_central_meridian_maps_to_false_easting():
    e, n = p.lonlat_to_tm84(84.0, 28.0)
    assert e == pytest.approx(500_000.0, abs=1e-6)
    # Northing at 28N on the central meridian: k0 * meridian arc length.
    assert n == pytest.approx(0.9996 * 3_098_143.0, rel=1e-4)


def test_matches_utm_formula_shape():
    # NPL-TM84 is UTM with lon_0=84; compare to UTM zone 45N shifted by 3 degrees:
    # a point 3 deg east of 84E must equal the UTM-45N easting of a point on 87E... i.e.
    # easting(84+d) - 500000 == -(easting(84-d) - 500000) by symmetry.
    e1, n1 = p.lonlat_to_tm84(86.5, 27.5)
    e2, n2 = p.lonlat_to_tm84(81.5, 27.5)
    assert (e1 - 500_000) == pytest.approx(-(e2 - 500_000), abs=1e-6)
    assert n1 == pytest.approx(n2, abs=1e-6)


@pytest.mark.parametrize("lon,lat", [(80.06, 29.5), (85.3240, 27.7172), (88.2, 26.6), (86.925, 27.988), (82.09, 29.53)])
def test_round_trip_lonlat_game(lon, lat):
    x, z = p.lonlat_to_game(lon, lat)
    lon2, lat2 = p.game_to_lonlat(x, z)
    assert lon2 == pytest.approx(lon, abs=1e-9)
    assert lat2 == pytest.approx(lat, abs=1e-9)


def test_round_trip_vectorised():
    rng = np.random.default_rng(1)
    lon = rng.uniform(80.0, 88.3, 1000)
    lat = rng.uniform(26.3, 30.5, 1000)
    x, z = p.lonlat_to_game(lon, lat)
    lon2, lat2 = p.game_to_lonlat(x, z)
    assert np.max(np.abs(lon2 - lon)) < 1e-9
    assert np.max(np.abs(lat2 - lat)) < 1e-9


def test_all_of_nepal_inside_quadtree_and_positive():
    x0, z0, x1, z1 = p.bbox_lonlat_to_game(80.0, 26.3, 88.3, 30.5)
    assert x0 > 0 and z0 > 0
    assert x1 < p.ROOT_SIZE_M and z1 < p.ROOT_SIZE_M


def test_scale_error_small_over_nepal():
    for lon, lat in [(80.06, 29.5), (84.0, 28.0), (88.2, 26.6)]:
        k = p.scale_factor_at(lon, lat)
        assert 0.9995 < k < 1.0020


def test_distance_preserved_locally():
    # 1 km east of Kathmandu measured on the ellipsoid vs projected.
    from pyproj import Geod
    g = Geod(ellps="WGS84")
    lon2, lat2, _ = g.fwd(85.324, 27.717, 90.0, 1000.0)
    x1, z1 = p.lonlat_to_game(85.324, 27.717)
    x2, z2 = p.lonlat_to_game(lon2, lat2)
    d = math.hypot(float(x2 - x1), float(z2 - z1))
    assert d == pytest.approx(1000.0, rel=4e-4)


def test_tile_key_round_trip_and_ordering():
    for level in (0, 1, 5, 10, 16):
        n = 1 << level
        for tx, ty in [(0, 0), (n - 1, 0), (0, n - 1), (n - 1, n - 1), (n // 3, n // 7)]:
            t = p.TileId(level, tx, ty)
            assert p.tile_from_key(t.key) == t
    assert p.TileId(5, 31, 31).key < p.TileId(6, 0, 0).key


def test_tile_hierarchy():
    t = p.TileId(10, 518, 162)
    assert t.size == 1024.0
    assert all(c.parent() == t for c in t.children())
    assert t.neighbor(1, 0).x0 == t.bounds[2]
    x0, z0, x1, z1 = t.bounds
    assert p.tile_at(10, x0, z0) == t
    assert p.tile_at(10, x1, z0) == t.neighbor(1, 0)


def test_tiles_covering_counts():
    tiles = list(p.tiles_covering(10, 1000.0, 1000.0, 3000.0, 2048.0))
    assert {(t.tx, t.ty) for t in tiles} == {(0, 0), (1, 0), (2, 0), (0, 1), (1, 1), (2, 1)}


def test_grid_coords_shared_edges_bit_identical():
    t = p.TileId(10, 518, 162)
    right = t.neighbor(1, 0)
    up = t.neighbor(0, 1)
    gx, gz = p.grid_coords(t, 129)
    rx, rz = p.grid_coords(right, 129)
    ux, uz = p.grid_coords(up, 129)
    assert np.array_equal(gx[:, -1], rx[:, 0]) and np.array_equal(gz[:, -1], rz[:, 0])
    assert np.array_equal(gx[-1, :], ux[0, :]) and np.array_equal(gz[-1, :], uz[0, :])
    # parent grid points coincide with every other child grid point
    parent = t.parent()
    px, pz = p.grid_coords(parent, 129)
    child = parent.children()[0]
    cx, cz = p.grid_coords(child, 129)
    assert np.array_equal(cx[::2, ::2], px[:65, :65]) and np.array_equal(cz[::2, ::2], pz[:65, :65])


def test_grid_size_validation():
    with pytest.raises(ValueError):
        p.grid_coords(p.TileId(10, 0, 0), 128)
