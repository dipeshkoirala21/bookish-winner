"""Geometry for tiling: polyline clipping with context points, seam
consistency between neighbouring tiles, polygon clipping and triangulation."""

from __future__ import annotations

import numpy as np
import pytest
import shapely
from shapely.geometry import MultiPolygon, Point, Polygon, box

from ghumante_pipeline.geom import (Piece, clip_polygon, clip_polyline, orient_ring, polyline_length, ring_centroid,
                                    signed_area, stitch_pieces, triangles_area, triangulate)

A = (0.0, 0.0, 100.0, 100.0)  # south-west tile of a 2x2 split
B = (100.0, 0.0, 200.0, 100.0)  # south-east
C = (0.0, 100.0, 100.0, 200.0)  # north-west
D = (100.0, 100.0, 200.0, 200.0)  # north-east
GRID = (A, B, C, D)


def P(*pts) -> np.ndarray:
    return np.array(pts, dtype=np.float64)


def _pieces(pts, b):
    return [(p.points.tolist(), p.has_prev_ctx, p.has_next_ctx) for p in clip_polyline(P(*pts), b)]


def _grid(n: int, size: float, ox: float = 0.0, oz: float = 0.0):
    return [(ox + i * size, oz + j * size, ox + (i + 1) * size, oz + (j + 1) * size)
            for j in range(n) for i in range(n)]


# ---------------------------------------------------------------------------
# Rings and polylines
# ---------------------------------------------------------------------------
def test_signed_area_and_orientation():
    sq = P((0, 0), (10, 0), (10, 10), (0, 10))
    assert signed_area(sq) == 100.0 and signed_area(sq[::-1]) == -100.0
    closed = np.vstack([sq, sq[:1]])
    assert signed_area(closed) == 100.0
    cw = orient_ring(sq, ccw=False)
    assert cw.tolist() == [[0, 0], [0, 10], [10, 10], [10, 0]]  # first vertex kept
    assert orient_ring(cw, ccw=True).tolist() == sq.tolist()
    assert orient_ring(sq, ccw=True) is not sq
    cw_closed = orient_ring(closed, ccw=False)
    assert cw_closed[0].tolist() == cw_closed[-1].tolist() and signed_area(cw_closed) == -100.0
    flat = P((0, 0), (1, 1), (2, 2))
    assert signed_area(flat) == 0.0 and orient_ring(flat, ccw=False).tolist() == flat.tolist()


def test_signed_area_at_game_magnitudes():
    sq = P((0, 0), (0.01, 0), (0.01, 0.01), (0, 0.01)) + (812345.0, 456789.0)
    assert signed_area(sq) == pytest.approx(1e-4, rel=1e-6)


def test_ring_centroid():
    assert ring_centroid(P((0, 0), (4, 0), (4, 2), (0, 2))) == pytest.approx((2.0, 1.0))
    tri = P((0, 0), (3, 0), (0, 3), (0, 0))
    assert ring_centroid(tri) == pytest.approx((1.0, 1.0))
    big = P((0, 0), (10, 0), (10, 10), (0, 10)) + (700000.0, 300000.0)
    assert ring_centroid(big) == pytest.approx((700005.0, 300005.0), abs=1e-9)
    assert ring_centroid(P((0, 0), (2, 2), (4, 4))) == pytest.approx((2.0, 2.0))  # degenerate: vertex mean
    # L-shape: centroid differs from the vertex mean
    L = P((0, 0), (2, 0), (2, 1), (1, 1), (1, 2), (0, 2))
    assert ring_centroid(L) == pytest.approx(Polygon(L).centroid.coords[0])


def test_polyline_length():
    assert polyline_length(P((0, 0), (3, 4), (3, 10))) == 11.0
    assert polyline_length(P((1, 1))) == 0.0


# ---------------------------------------------------------------------------
# Polyline clipping: basic cases
# ---------------------------------------------------------------------------
def test_fully_inside():
    pts = P((10, 10), (20, 50), (90, 90))
    (piece,) = clip_polyline(pts, A)
    assert piece.points.tolist() == pts.tolist() and not piece.has_prev_ctx and not piece.has_next_ctx
    assert piece.inner.tolist() == pts.tolist()


def test_fully_outside():
    assert clip_polyline(P((110, 10), (150, 90), (190, 10)), A) == []
    assert clip_polyline(P((-10, 150), (150, 150)), A) == []
    # bounding boxes overlap but the segment misses the box (passes the corner)
    assert clip_polyline(P((90, 120), (120, 90)), A) == []


def test_crossing_once():
    assert _pieces([(50, 50), (150, 70)], A) == [([[50, 50], [100, 60], [150, 70]], False, True)]
    assert _pieces([(50, 50), (150, 70)], B) == [([[50, 50], [100, 60], [150, 70]], True, False)]


def test_crossing_through_whole_tile():
    assert _pieces([(-50, 50), (150, 50)], A) == [([[-50, 50], [0, 50], [100, 50], [150, 50]], True, True)]


def test_leaving_and_reentering():
    pts = [(10, 10), (50, 150), (90, 10), (150, 50), (90, 90)]
    got = _pieces(pts, A)
    assert got == [
        ([[10, 10], [(100 - 10) / 140 * 40 + 10, 100], [50, 150]], False, True),
        ([[50, 150], [50 + (100 - 150) / (10 - 150) * (90 - 50), 100], [90, 10], [100, 10 + 10 / 60 * 40], [150, 50]],
         True, True),
        ([[150, 50], [100, 50 + 50 / 60 * 40], [90, 90]], True, False),
    ]


def test_passing_exactly_through_corner():
    pts = [(50, 50), (150, 150)]
    assert _pieces(pts, A) == [([[50, 50], [100, 100], [150, 150]], False, True)]
    assert _pieces(pts, D) == [([[50, 50], [100, 100], [150, 150]], True, False)]
    assert _pieces(pts, B) == [] and _pieces(pts, C) == []  # touches only at the corner: zero length


def test_corner_with_ugly_coordinates():
    # The line through (0.1, 0.3) and the corner (100, 100) continued: exact rational collinearity
    # does not hold in floats, so the exact decision path decides which border is crossed first.
    a, b = (0.1, 0.3), (199.9, 199.7)
    pieces = {i: clip_polyline(P(a, b), t) for i, t in enumerate(GRID)}
    (chain,) = stitch_pieces([p for ps in pieces.values() for p in ps])
    assert chain[0].tolist() == list(a) and chain[-1].tolist() == list(b)
    for pt in chain[1:-1]:
        assert pt[0] == 100.0 or pt[1] == 100.0


def test_vertex_exactly_on_border():
    pts = [(50, 50), (100, 60), (150, 40)]
    assert _pieces(pts, A) == [([[50, 50], [100, 60], [150, 40]], False, True)]
    assert _pieces(pts, B) == [([[50, 50], [100, 60], [150, 40]], True, False)]
    # touching the border from inside and turning back: B sees only a point -> dropped
    back = [(50, 50), (100, 60), (50, 70)]
    assert _pieces(back, A) == [([[50, 50], [100, 60], [50, 70]], False, False)]
    assert _pieces(back, B) == []


def test_polyline_starting_or_ending_on_border():
    assert _pieces([(100, 50), (150, 50)], A) == []
    assert _pieces([(100, 50), (150, 50)], B) == [([[100, 50], [150, 50]], False, False)]
    assert _pieces([(150, 50), (100, 50)], B) == [([[150, 50], [100, 50]], False, False)]


def test_segment_along_border_appears_in_both_tiles():
    pts = [(100, 10), (100, 90)]
    assert _pieces(pts, A) == [([[100, 10], [100, 90]], False, False)]
    assert _pieces(pts, B) == [([[100, 10], [100, 90]], False, False)]
    longer = [(100, -50), (100, 50)]
    assert _pieces(longer, A) == [([[100, -50], [100, 0], [100, 50]], True, False)]


def test_degenerate_inputs():
    assert clip_polyline(P((10, 10)), A) == []
    assert clip_polyline(np.zeros((0, 2)), A) == []
    assert clip_polyline(P((10, 10), (10, 10)), A) == []
    (piece,) = clip_polyline(P((10, 10), (10, 10), (20, 20), (20, 20)), A)
    assert piece.points.tolist() == [[10, 10], [20, 20]]
    with pytest.raises(ValueError):
        clip_polyline(P((10, 10), (np.nan, 20)), A)
    with pytest.raises(ValueError):
        clip_polyline(P((10, 10), (20, 20)), (0, 0, 0, 10))


def test_closed_ring_is_rotated_not_split_at_its_start():
    ring = [(50, 50), (150, 50), (150, 90), (50, 90), (50, 50)]  # starts inside A
    got = _pieces(ring, A)
    assert got == [([[150, 90], [100, 90], [50, 90], [50, 50], [100, 50], [150, 50]], True, True)]
    inside = [(10, 10), (20, 10), (20, 20), (10, 10)]
    assert _pieces(inside, A) == [([[10, 10], [20, 10], [20, 20], [10, 10]], False, False)]


def test_cut_point_is_snapped_to_border():
    rng = np.random.default_rng(0)
    for _ in range(500):
        a = (rng.uniform(0, 100), rng.uniform(0, 100))
        b = (rng.uniform(100.0000001, 300), rng.uniform(-200, 300))
        for p in clip_polyline(P(a, b), A):
            cut = p.inner[-1]
            assert cut[0] == 100.0 or cut[1] in (0.0, 100.0)
            assert 0.0 <= cut[0] <= 100.0 and 0.0 <= cut[1] <= 100.0


# ---------------------------------------------------------------------------
# Neighbour consistency
# ---------------------------------------------------------------------------
def test_neighbour_cut_points_identical_bit_for_bit():
    rng = np.random.default_rng(1)
    west, east = (500.0, 300.0, 1524.0, 1324.0), (1524.0, 300.0, 2548.0, 1324.0)
    for _ in range(1000):
        a = (rng.uniform(west[0], west[2]), rng.uniform(301, 1323))
        b = (rng.uniform(east[0] + 1e-9, east[2]), rng.uniform(301, 1323))
        if rng.random() < 0.5:
            a, b = b, a
        (pw,), (pe,) = clip_polyline(P(a, b), west), clip_polyline(P(a, b), east)
        cw = pw.points[-2] if pw.has_next_ctx else pw.points[1]
        ce = pe.points[1] if pe.has_prev_ctx else pe.points[-2]
        assert cw.tobytes() == ce.tobytes() and cw[0] == 1524.0
        # context points are the original vertices on the other side
        if pw.has_next_ctx:
            assert pw.points[-1].tolist() == list(b) and pe.points[0].tolist() == list(a)
        else:
            assert pw.points[0].tolist() == list(a) and pe.points[-1].tolist() == list(b)


@pytest.mark.parametrize("seed", range(6))
def test_grid_split_reconstructs_polyline(seed):
    rng = np.random.default_rng(seed)
    size = 1024.0
    origin = (512000.0, 307200.0)
    grid = _grid(3, size, *origin)
    for _ in range(60):
        n = int(rng.integers(2, 25))
        pts = np.column_stack([rng.uniform(origin[0] + 1, origin[0] + 3 * size - 1, n),
                               rng.uniform(origin[1] + 1, origin[1] + 3 * size - 1, n)])
        # put some vertices exactly on grid lines and corners
        for k in rng.choice(n, size=min(n, 3), replace=False):
            if rng.random() < 0.5:
                pts[k, 0] = origin[0] + size * int(rng.integers(1, 3))
            if rng.random() < 0.5:
                pts[k, 1] = origin[1] + size * int(rng.integers(1, 3))
        if len(set(map(tuple, pts.tolist()))) < n:
            continue  # revisits a snapped vertex: stitching would be ambiguous
        on_x = np.isin(pts[:, 0], origin[0] + size * np.arange(4))
        on_z = np.isin(pts[:, 1], origin[1] + size * np.arange(4))
        # skip polylines with a segment lying along a grid line (appears in two tiles by design)
        if np.any((on_x[:-1] & on_x[1:] & (pts[:-1, 0] == pts[1:, 0]))
                  | (on_z[:-1] & on_z[1:] & (pts[:-1, 1] == pts[1:, 1]))):
            continue
        pieces = [p for t in grid for p in clip_polyline(pts, t)]
        chains = stitch_pieces(pieces)
        assert len(chains) == 1
        chain = chains[0]
        assert chain[0].tolist() == pts[0].tolist() and chain[-1].tolist() == pts[-1].tolist()
        # original vertices appear in order; every extra point is a cut on a grid line, on its segment
        orig = [tuple(p) for p in pts.tolist()]
        k = 0
        for q in map(tuple, chain.tolist()):
            if k < len(orig) and q == orig[k]:
                k += 1
                continue
            assert q[0] in origin[0] + size * np.arange(4) or q[1] in origin[1] + size * np.arange(4)
            a, b = np.array(orig[k - 1]), np.array(orig[k])
            d = b - a
            cross = d[0] * (q[1] - a[1]) - d[1] * (q[0] - a[0])
            assert abs(cross) <= 1e-6 * np.hypot(*d) * 3 * size
        assert k == len(orig)
        assert polyline_length(chain) == pytest.approx(polyline_length(pts), rel=1e-12)
        # every piece's context points are original vertices
        for p in pieces:
            if p.has_prev_ctx:
                assert tuple(p.points[0].tolist()) in set(orig)
            if p.has_next_ctx:
                assert tuple(p.points[-1].tolist()) in set(orig)


def test_near_corner_segments_are_consistent():
    """Segments passing within float noise of a 4-tile corner: the pieces of
    all four tiles must still stitch into one continuous chain."""
    rng = np.random.default_rng(7)
    corner = np.array([100.0, 100.0])
    for _ in range(2000):
        ang = rng.uniform(0, 2 * np.pi)
        d = np.array([np.cos(ang), np.sin(ang)])
        off = rng.choice([0.0, 1e-13, -1e-13, 1e-9, -1e-9]) * np.array([-d[1], d[0]])
        a = corner + off - d * rng.uniform(1, 90)
        b = corner + off + d * rng.uniform(1, 90)
        pieces = [p for t in GRID for p in clip_polyline(P(a, b), t)]
        chains = stitch_pieces(pieces)
        assert len(chains) == 1, (a.tolist(), b.tolist())
        assert chains[0][0].tolist() == a.tolist() and chains[0][-1].tolist() == b.tolist()


def test_stitch_pieces_handles_disjoint_runs():
    pts = P((10, 10), (150, 10), (150, 90), (10, 90))
    pieces = clip_polyline(pts, A)
    assert len(pieces) == 2
    assert len(stitch_pieces(pieces)) == 2
    assert len(stitch_pieces(pieces + clip_polyline(pts, B))) == 1
    assert isinstance(pieces[0], Piece)


# ---------------------------------------------------------------------------
# Polygons
# ---------------------------------------------------------------------------
def _total_area(polys):
    return sum(p.area for p in polys)


def test_clip_polygon_conserves_area_with_holes():
    poly = Polygon([(20, 20), (180, 30), (170, 180), (30, 170)],
                   [[(90, 90), (110, 90), (110, 110), (90, 110)], [(40, 40), (60, 40), (50, 60)]])
    parts = [clip_polygon(poly, t) for t in GRID]
    assert sum(_total_area(ps) for ps in parts) == pytest.approx(poly.area, rel=1e-12)
    for t, ps in zip(GRID, parts):
        for p in ps:
            assert p.is_valid and shapely.is_ccw(p.exterior) and all(not shapely.is_ccw(r) for r in p.interiors)
            x0, z0, x1, z1 = p.bounds
            assert x0 >= t[0] and z0 >= t[1] and x1 <= t[2] and z1 <= t[3]
    assert len(parts[0][0].interiors) == 1  # the triangle hole is wholly in A


def test_clip_polygon_border_vertices_exact():
    poly = Polygon([(10.123, 20.456), (190.789, 33.3), (150.1, 170.7)])
    for t in GRID:
        for p in clip_polygon(poly, t):
            for x, z in p.exterior.coords:
                if abs(x - 100.0) < 1e-6:
                    assert x == 100.0
                if abs(z - 100.0) < 1e-6:
                    assert z == 100.0


def test_clip_polygon_cases():
    inside = Polygon([(10, 10), (20, 10), (20, 20)])
    (p,) = clip_polygon(inside, A)
    assert p.equals(inside) and shapely.is_ccw(p.exterior)
    assert clip_polygon(Polygon([(110, 10), (120, 10), (120, 20)]), A) == []
    assert clip_polygon(Polygon([(100, 10), (150, 10), (150, 50), (100, 50)]), A) == []  # shares an edge only
    tiny = Polygon([(99.6, 10), (110, 10), (110, 11), (99.6, 11)])  # 0.4 x 1 m in A
    assert clip_polygon(tiny, A) == [] and len(clip_polygon(tiny, A, min_area=0.1)) == 1
    bowtie = Polygon([(10, 10), (90, 90), (90, 10), (10, 90)])  # self-intersecting
    parts = clip_polygon(bowtie, A)
    assert len(parts) == 2 and _total_area(parts) == pytest.approx(3200.0)
    assert clip_polygon(Polygon(), A) == []
    with pytest.raises(TypeError):
        clip_polygon(shapely.LineString([(0, 0), (1, 1)]), A)


def test_clip_polygon_deterministic_order():
    a = Point(20, 20).buffer(10)
    b = Point(70, 60).buffer(15)
    c = Point(90, 15).buffer(12)
    r1 = clip_polygon(MultiPolygon([a, b, c]), A)
    r2 = clip_polygon(MultiPolygon([c, b, a]), A)
    assert [p.wkb for p in r1] == [p.wkb for p in r2]
    assert len(r1) == 3


def _check_triangulation(poly: Polygon):
    v, idx, rings = triangulate(poly)
    assert v.dtype == np.float64 and idx.dtype == np.int64 and len(idx) % 3 == 0
    tri = idx.reshape(-1, 3)
    p = v[tri]
    cross = ((p[:, 1, 0] - p[:, 0, 0]) * (p[:, 2, 1] - p[:, 0, 1])
             - (p[:, 1, 1] - p[:, 0, 1]) * (p[:, 2, 0] - p[:, 0, 0]))
    assert np.all(cross > 0)  # counter-clockwise, no degenerate triangles
    assert triangles_area(v, idx) == pytest.approx(poly.area, rel=1e-9)
    assert rings[0][0] == 0 and sum(n for _, n in rings) == len(v)
    for k, (s, n) in enumerate(rings):
        ring = v[s:s + n]
        assert (signed_area(ring) > 0) == (k == 0)
    assert rings[1:] == [] or len(rings) - 1 == len(poly.interiors)
    cents = p.mean(axis=1)
    assert np.all(shapely.contains_xy(poly.buffer(1e-9 * max(1.0, np.abs(v).max())), cents[:, 0], cents[:, 1]))
    return v, idx, rings


def test_triangulate_square_with_holes():
    poly = Polygon([(0, 0), (10, 0), (10, 10), (0, 10)],
                   [[(2, 2), (2, 4), (4, 4), (4, 2)], [(6, 6), (8, 6), (8, 8), (6, 8)]])
    v, idx, rings = _check_triangulation(poly)
    assert rings == [(0, 4), (4, 4), (8, 4)]
    assert v[:4].tolist() == [[0, 0], [10, 0], [10, 10], [0, 10]]
    assert triangles_area(v, idx) == 92.0
    hole_pts = shapely.points(v[idx.reshape(-1, 3)].mean(axis=1))
    assert not np.any(shapely.contains(Polygon([(2, 2), (2, 4), (4, 4), (4, 2)]), hole_pts))


def test_triangulate_reorients_rings_and_strips_duplicates():
    cw_shell = [(0, 0), (0, 10), (10, 10), (10, 10), (10, 0)]
    poly = Polygon(cw_shell, [[(2, 2), (4, 2), (4, 4), (2, 4)]])  # hole CCW
    v, idx, rings = _check_triangulation(poly)
    assert rings == [(0, 4), (4, 4)]
    assert v[:4].tolist() == [[0, 0], [10, 0], [10, 10], [0, 10]]
    assert v[4:].tolist() == [[2, 2], [2, 4], [4, 4], [4, 2]]


def test_triangulate_hole_touching_shell():
    poly = Polygon([(0, 0), (10, 0), (10, 10), (0, 10)], [[(0, 5), (3, 6), (3, 4)]])
    assert poly.is_valid
    _check_triangulation(poly)


def test_triangulate_rejects_non_polygons_and_handles_empty():
    with pytest.raises(TypeError):
        triangulate(MultiPolygon([box(0, 0, 1, 1), box(2, 2, 3, 3)]))
    v, idx, rings = triangulate(Polygon())
    assert v.shape == (0, 2) and len(idx) == 0 and rings == []


def test_triangulate_fuzz_at_game_magnitudes():
    rng = np.random.default_rng(3)
    for _ in range(60):
        c = rng.uniform(0, 1000, 2) + (512345.678, 298765.4321)
        poly = Point(c).buffer(rng.uniform(50, 200), quad_segs=int(rng.integers(2, 10)))
        for _ in range(int(rng.integers(0, 5))):
            poly = poly.difference(Point(c + rng.uniform(-150, 150, 2)).buffer(rng.uniform(5, 60), quad_segs=3))
        for part in clip_polygon(poly, (c[0] - 100, c[1] - 120, c[0] + 300, c[1] + 300)):
            _check_triangulation(part)


def test_clip_and_triangulate_area_conservation_across_tiles():
    rng = np.random.default_rng(4)
    size = 1024.0
    grid = _grid(2, size, 512000.0, 307200.0)
    for _ in range(10):
        c = np.array([512000.0 + size, 307200.0 + size]) + rng.uniform(-300, 300, 2)
        poly = Point(c).buffer(rng.uniform(300, 700), quad_segs=6)
        for _ in range(3):
            poly = poly.difference(Point(c + rng.uniform(-400, 400, 2)).buffer(rng.uniform(20, 120), quad_segs=4))
        clipped_total = sum(poly.intersection(box(*t)).area for t in grid)
        tri_total = 0.0
        for t in grid:
            for part in clip_polygon(poly, t):
                v, idx, _ = triangulate(part)
                tri_total += triangles_area(v, idx)
                assert v[:, 0].min() >= t[0] and v[:, 0].max() <= t[2]
        assert tri_total == pytest.approx(clipped_total, abs=2.0)  # slivers < 0.5 m2 are dropped
        assert tri_total == pytest.approx(poly.area, abs=2.0)


# ---------------------------------------------------------------------------
# Real data
# ---------------------------------------------------------------------------
@pytest.mark.realdata
@pytest.mark.slow
def test_real_thamel_roads_clip_encode_and_stitch(nepal_pbf):
    """Thamel highways from the real extract: clip into level-10 tiles, encode
    and decode each tile, and check that every way stitches back together and
    that cut points match across tile borders after the cm round trip."""
    osmium = pytest.importorskip("osmium")
    from collections import defaultdict

    from ghumante_pipeline.projection import bbox_lonlat_to_game, lonlat_to_game, tiles_covering
    from ghumante_pipeline.tile_format import (ROAD_HAS_NEXT_CTX, ROAD_HAS_PREV_CTX, RoadRec, TileData, decode_tile,
                                               encode_tile, points_to_local_cm)

    bbox = (85.300, 27.700, 85.320, 27.720)
    ways: dict[int, np.ndarray] = {}
    fp = (osmium.FileProcessor(str(nepal_pbf)).with_locations()
          .with_filter(osmium.filter.KeyFilter("highway")).with_filter(osmium.filter.EntityFilter(osmium.osm.WAY)))
    for w in fp:
        try:
            ll = np.array([(n.lon, n.lat) for n in w.nodes])
        except osmium.InvalidLocationError:
            continue
        if ll[:, 0].max() < bbox[0] or ll[:, 0].min() > bbox[2] or ll[:, 1].max() < bbox[1] or ll[:, 1].min() > bbox[3]:
            continue
        x, z = lonlat_to_game(ll[:, 0], ll[:, 1])
        ways[w.id] = np.column_stack([x, z])
    assert len(ways) > 500
    tiles = list(tiles_covering(10, *bbox_lonlat_to_game(*bbox)))
    pieces = defaultdict(list)
    per_tile = defaultdict(list)
    for wid in sorted(ways):
        for t in tiles:
            for p in clip_polyline(ways[wid], t.bounds):
                pieces[wid].append(p)
                flags = (ROAD_HAS_PREV_CTX if p.has_prev_ctx else 0) | (ROAD_HAS_NEXT_CTX if p.has_next_ctx else 0)
                per_tile[t].append(RoadRec(osm_way_id=wid, flags=flags, points=points_to_local_cm(t, p.points)))
    cuts = defaultdict(list)  # game-cm cut point -> tiles that have it
    for t, roads in per_tile.items():
        blob = encode_tile(TileData(t, 1, roads=roads, has_detail=True))
        assert encode_tile(TileData(t, 1, roads=roads[::-1], has_detail=True)) == blob
        for r in decode_tile(blob).roads:
            base = np.array([round(t.x0 * 100), round(t.z0 * 100)], dtype=np.int64)
            if r.flags & ROAD_HAS_PREV_CTX:
                cuts[(r.osm_way_id, *(r.points[1] + base).tolist())].append(t)
            if r.flags & ROAD_HAS_NEXT_CTX:
                cuts[(r.osm_way_id, *(r.points[-2] + base).tolist())].append(t)
    x0 = min(t.bounds[0] for t in tiles)
    z0 = min(t.bounds[1] for t in tiles)
    x1 = max(t.bounds[2] for t in tiles)
    z1 = max(t.bounds[3] for t in tiles)
    for wid, pts in ways.items():
        if pts[:, 0].min() >= x0 and pts[:, 0].max() <= x1 and pts[:, 1].min() >= z0 and pts[:, 1].max() <= z1:
            assert len(stitch_pieces(pieces[wid])) == 1, wid
    interior = [(k, v) for k, v in cuts.items()
                if x0 * 100 < k[1] < x1 * 100 and z0 * 100 < k[2] < z1 * 100]
    assert interior and all(len(v) == 2 for _, v in interior)
