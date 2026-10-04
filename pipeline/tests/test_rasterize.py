"""Tests for rasterize.py: sample-point burning semantics and area burn order."""

from __future__ import annotations

import numpy as np
import pytest
import shapely
from shapely.geometry import LineString, MultiPolygon, Point, Polygon, box

from ghumante_pipeline import projection as P
from ghumante_pipeline.model import AreaKind
from ghumante_pipeline.rasterize import area_priority, burn_key, rasterize_at_samples

TILE = P.TileId(10, 520, 160)
N = 65
SPACING = TILE.size / (N - 1)  # 16 m


def grid():
    return P.grid_coords(TILE, N)


def test_row_zero_is_south_and_column_zero_is_west():
    sw = box(TILE.x0 - 5, TILE.z0 - 5, TILE.x0 + 40, TILE.z0 + 40)  # covers samples 0..2
    out = rasterize_at_samples([(sw, 9)], TILE.x0, TILE.z0, SPACING, N)
    assert out.shape == (N, N) and out.dtype == np.uint8
    assert out[:3, :3].tolist() == [[9] * 3] * 3
    assert out.sum() == 9 * 9
    assert out[-1, -1] == 0


def test_burns_exactly_the_samples_inside():
    # Edges between sample rows/columns: x in (x0+10, x0+100) holds columns 1..6.
    r = box(TILE.x0 + 10, TILE.z0 + 20, TILE.x0 + 100, TILE.z0 + 70)
    out = rasterize_at_samples([(r, 1)], TILE.x0, TILE.z0, SPACING, N)
    rows, cols = np.nonzero(out)
    assert sorted(set(cols.tolist())) == [1, 2, 3, 4, 5, 6]
    assert sorted(set(rows.tolist())) == [2, 3, 4]
    # A thin sliver between two sample columns burns nothing (no all_touched).
    sliver = box(TILE.x0 + 17, TILE.z0, TILE.x0 + 31, TILE.z0 + 1024)
    assert rasterize_at_samples([(sliver, 1)], TILE.x0, TILE.z0, SPACING, N).sum() == 0


def test_matches_exact_point_in_polygon_for_random_polygons():
    rng = np.random.default_rng(0)
    x, z = grid()
    for _ in range(60):
        cx, cz = TILE.x0 + rng.uniform(0, 1024), TILE.z0 + rng.uniform(0, 1024)
        m = int(rng.integers(3, 12))
        ang = np.sort(rng.uniform(0, 2 * np.pi, m))
        rad = rng.uniform(20, 700, m)
        poly = Polygon(np.c_[cx + rad * np.cos(ang), cz + rad * np.sin(ang)])
        if not poly.is_valid:
            continue
        out = rasterize_at_samples([(poly, 3)], TILE.x0, TILE.z0, SPACING, N)
        np.testing.assert_array_equal(out, np.where(shapely.contains_xy(poly, x, z), 3, 0))


def test_later_shapes_overwrite_earlier_ones_and_holes_are_respected():
    outer = box(TILE.x0, TILE.z0, TILE.x0 + 500, TILE.z0 + 500)
    with_hole = Polygon(outer.exterior.coords, [box(TILE.x0 + 100, TILE.z0 + 100,
                                                    TILE.x0 + 200, TILE.z0 + 200).exterior.coords])
    pond = box(TILE.x0 + 300, TILE.z0 + 300, TILE.x0 + 400, TILE.z0 + 400)
    out = rasterize_at_samples([(with_hole, 7), (pond, 2)], TILE.x0, TILE.z0, SPACING, N)
    x, z = grid()
    assert out[int(150 / SPACING), int(150 / SPACING)] == 0  # in the hole
    assert out[int(350 / SPACING), int(350 / SPACING)] == 2  # later shape wins
    assert out[int(50 / SPACING), int(50 / SPACING)] == 7
    swapped = rasterize_at_samples([(pond, 2), (with_hole, 7)], TILE.x0, TILE.z0, SPACING, N)
    assert swapped[int(350 / SPACING), int(350 / SPACING)] == 7
    assert np.array_equal(out != 0, shapely.contains_xy(shapely.union_all([with_hole, pond]), x, z))


def test_multipolygons_dtype_and_degenerate_inputs():
    a = box(TILE.x0 + 50, TILE.z0 + 50, TILE.x0 + 150, TILE.z0 + 150)
    b = box(TILE.x0 + 600, TILE.z0 + 600, TILE.x0 + 700, TILE.z0 + 700)
    out = rasterize_at_samples([(MultiPolygon([a, b]), 300)], TILE.x0, TILE.z0, SPACING, N, dtype=np.uint16)
    assert out.dtype == np.uint16 and set(np.unique(out)) == {0, 300}
    assert out[int(100 / SPACING), int(100 / SPACING)] == 300 and out[int(650 / SPACING), int(650 / SPACING)] == 300
    assert not rasterize_at_samples([], TILE.x0, TILE.z0, SPACING, N).any()
    junk = [(Polygon(), 1), (Point(TILE.x0 + 16, TILE.z0 + 16), 1),
            (LineString([(TILE.x0, TILE.z0), (TILE.x0 + 1024, TILE.z0 + 1024)]), 1)]
    assert not rasterize_at_samples(junk, TILE.x0, TILE.z0, SPACING, N).any()
    with pytest.raises(ValueError):
        rasterize_at_samples([(a, 1)], TILE.x0, TILE.z0, 0.0, N)


def test_neighbouring_tiles_agree_on_shared_edges():
    # Polygon edges deliberately running exactly through shared samples.
    east = TILE.neighbor(1, 0)
    north = TILE.neighbor(0, 1)
    xe = TILE.x0 + TILE.size  # the shared column
    shapes = [
        (Polygon([(xe - 300, TILE.z0 + 100), (xe + 300, TILE.z0 + 132), (xe + 200, TILE.z0 + 900),
                  (xe - 100, TILE.z0 + 1100)]), 4),
        (box(xe - 64, TILE.z0 + 512, xe, TILE.z0 + 2048), 6),  # edge exactly on the shared column
        (box(TILE.x0 + 200, TILE.z0 + 1000, TILE.x0 + 300, TILE.z0 + 1024 + 48), 5),  # crosses north edge
    ]
    a = rasterize_at_samples(shapes, TILE.x0, TILE.z0, SPACING, N)
    e = rasterize_at_samples(shapes, east.x0, east.z0, SPACING, N)
    n = rasterize_at_samples(shapes, north.x0, north.z0, SPACING, N)
    assert np.array_equal(a[:, -1], e[:, 0])
    assert np.array_equal(a[-1, :], n[0, :])
    assert a[:, -1].any() and a[-1, :].any()


def test_area_priority_puts_water_and_glacier_last():
    water = {AreaKind.WATER_LAKE, AreaKind.WATER_RIVER, AreaKind.WATER_POND}
    others = [k for k in AreaKind if k not in water | {AreaKind.GLACIER, AreaKind.SAND_SHINGLE}]
    for k in others:
        assert area_priority(k) < area_priority(AreaKind.GLACIER) < area_priority(AreaKind.WATER_LAKE)
    assert all(area_priority(k) == area_priority(AreaKind.WATER_RIVER) for k in water)
    assert area_priority(AreaKind.FARMLAND) < area_priority(AreaKind.RESIDENTIAL) < area_priority(AreaKind.PARK)
    assert area_priority(AreaKind.FOREST) < area_priority(AreaKind.FARMLAND)
    assert area_priority(AreaKind.NONE) == 0
    assert isinstance(area_priority(250), int)  # unknown future kinds still get a tier


def test_burn_key_orders_by_tier_then_larger_first():
    items = [(AreaKind.WATER_POND, 10.0, 3), (AreaKind.FOREST, 1e6, 1), (AreaKind.SAND_SHINGLE, 5e4, 7),
             (AreaKind.WATER_RIVER, 2e5, 2), (AreaKind.RESIDENTIAL, 3e3, 4), (AreaKind.WATER_LAKE, 10.0, 0)]
    order = sorted(items, key=lambda it: burn_key(*it))
    assert [it[0] for it in order] == [AreaKind.FOREST, AreaKind.RESIDENTIAL, AreaKind.WATER_RIVER,
                                       AreaKind.SAND_SHINGLE, AreaKind.WATER_LAKE, AreaKind.WATER_POND]
    # A sand bar mapped inside a river polygon survives; so does a channel in a shingle bed.
    river = box(TILE.x0, TILE.z0, TILE.x0 + 1024, TILE.z0 + 300)
    bar = box(TILE.x0 + 400, TILE.z0 + 100, TILE.x0 + 600, TILE.z0 + 200)
    shapes = sorted([(bar, AreaKind.SAND_SHINGLE), (river, AreaKind.WATER_RIVER)],
                    key=lambda s: burn_key(s[1], s[0].area))
    out = rasterize_at_samples([(g, int(k)) for g, k in shapes], TILE.x0, TILE.z0, SPACING, N)
    assert out[int(150 / SPACING), int(500 / SPACING)] == AreaKind.SAND_SHINGLE
    assert out[int(250 / SPACING), int(500 / SPACING)] == AreaKind.WATER_RIVER
