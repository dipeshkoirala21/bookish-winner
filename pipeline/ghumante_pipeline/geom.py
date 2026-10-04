"""Pure 2-D geometry for tiling: polyline/polygon clipping and triangulation.

Everything works in **game coordinates** (metres, X east, Z north; see
projection.py). Nothing here knows about tiles or file formats; ``tiling.py``
feeds tile boxes in and ``tile_format.py`` quantises the results.

Polyline clipping (``clip_polyline``) is the seam-critical part. A road that
crosses a tile border is clipped in *both* tiles, and the two meshes must meet
exactly (docs/DATA_FORMATS.md section 1.4). Three rules make that hold:

* **Closed boxes.** A vertex lying exactly on a border is inside both tiles.
* **Shared decisions.** The parameter at which segment ``a -> b`` crosses a
  border line ``x = L`` is always computed as ``(L - ax) / (bx - ax)`` (and
  likewise for z), so every tile that has ``L`` as a border gets the same
  float. Liang-Barsky's entry/exit choices are min/max over these shared
  values, so two tiles never disagree on which border a segment crosses
  first, even for a segment passing within an ulp of a tile corner (a float
  tie between an x and a z border is a corner hit). ``t == 0`` exactly when
  ``a`` is inside the closed box and ``t == 1`` exactly when ``b`` is, so the
  run bookkeeping agrees with a plain point-in-box test.
* **Canonical cut points.** The cut point on a vertical border ``x = X`` is
  ``(X, az + (X - ax) / (bx - ax) * (bz - az))``, evaluated in float64 from the
  original segment ``a -> b`` (never from a previous cut), with the border
  coordinate snapped to the border value and the other coordinate clamped to
  the border's extent. Horizontal borders are symmetric. Both tiles that share
  the border evaluate the very same expression, so the cut point is
  bit-identical on both sides. A corner hit (the x and z crossing parameters
  tie) yields the corner itself.

A segment that runs exactly along a border line lies in both closed boxes, so
it appears in the pieces of both tiles. The runtime renders such a road twice
on top of itself, which is harmless; ``stitch_pieces`` does not try to merge
such overlaps.
"""

from __future__ import annotations

from collections import defaultdict
from dataclasses import dataclass

import numpy as np
import shapely
from shapely.geometry import MultiPolygon, Polygon
from shapely.geometry.polygon import orient as _shapely_orient

Box = tuple[float, float, float, float]  # (x0, z0, x1, z1), closed

MIN_POLYGON_AREA_M2 = 0.5


# ---------------------------------------------------------------------------
# Rings and polylines
# ---------------------------------------------------------------------------
def _as_points(points) -> np.ndarray:
    pts = np.asarray(points, dtype=np.float64)
    if pts.ndim != 2 or pts.shape[1] != 2:
        raise ValueError(f"expected (N, 2) points, got shape {pts.shape}")
    return pts


def signed_area(points) -> float:
    """Shoelace area of a ring (open or closed): positive when counter-clockwise
    in the X-east/Z-north plane. Computed relative to the first vertex, which
    keeps precision at game-coordinate magnitudes."""
    pts = _as_points(points)
    if len(pts) < 3:
        return 0.0
    rel = pts - pts[0]
    x, z = rel[:, 0], rel[:, 1]
    return 0.5 * float(np.dot(x, np.roll(z, -1)) - np.dot(np.roll(x, -1), z))


def orient_ring(points, ccw: bool = True) -> np.ndarray:
    """Copy of a ring with the requested winding. The first vertex stays first
    (for a closed ring the closing vertex stays last). Degenerate rings with
    zero area are returned unchanged."""
    pts = _as_points(points).copy()
    a = signed_area(pts)
    if a == 0.0 or (a > 0.0) == ccw:
        return pts
    if len(pts) > 1 and np.array_equal(pts[0], pts[-1]):
        return pts[::-1].copy()
    return np.concatenate([pts[:1], pts[:0:-1]])


def ring_centroid(points) -> tuple[float, float]:
    """Area centroid of a ring (open or closed). Falls back to the vertex mean
    for degenerate rings."""
    pts = _as_points(points)
    if len(pts) > 1 and np.array_equal(pts[0], pts[-1]):
        pts = pts[:-1]
    if len(pts) == 0:
        raise ValueError("empty ring")
    origin = pts[0]
    rel = pts - origin
    x, z = rel[:, 0], rel[:, 1]
    xn, zn = np.roll(x, -1), np.roll(z, -1)
    cross = x * zn - xn * z
    a2 = float(cross.sum())
    if a2 == 0.0:
        m = rel.mean(axis=0)
        return float(origin[0] + m[0]), float(origin[1] + m[1])
    cx = float(((x + xn) * cross).sum()) / (3.0 * a2)
    cz = float(((z + zn) * cross).sum()) / (3.0 * a2)
    return float(origin[0] + cx), float(origin[1] + cz)


def polyline_length(points) -> float:
    pts = _as_points(points)
    if len(pts) < 2:
        return 0.0
    d = np.diff(pts, axis=0)
    return float(np.hypot(d[:, 0], d[:, 1]).sum())


def _dedupe_consecutive(pts: np.ndarray) -> np.ndarray:
    if len(pts) < 2:
        return pts
    keep = np.ones(len(pts), dtype=bool)
    keep[1:] = np.any(pts[1:] != pts[:-1], axis=1)
    return pts[keep]


# ---------------------------------------------------------------------------
# Polyline clipping
# ---------------------------------------------------------------------------
@dataclass
class Piece:
    """One maximal in-box run of a clipped polyline.

    ``points`` includes the context points: when ``has_prev_ctx`` the first
    point is the original vertex just before the entry cut (outside the box),
    and when ``has_next_ctx`` the last point is the original vertex just after
    the exit cut. Context points are never rendered.
    """

    points: np.ndarray  # (N, 2) float64
    has_prev_ctx: bool
    has_next_ctx: bool

    @property
    def inner(self) -> np.ndarray:
        """The in-box points (context points stripped)."""
        lo = 1 if self.has_prev_ctx else 0
        hi = len(self.points) - (1 if self.has_next_ctx else 0)
        return self.points[lo:hi]


def _cut_x(ax: float, az: float, bx: float, bz: float, x_line: float, z_lo: float, z_hi: float) -> tuple[float, float]:
    """Canonical intersection of segment a->b with the vertical line x = x_line."""
    z = az + (x_line - ax) / (bx - ax) * (bz - az)
    return x_line, min(max(z, z_lo), z_hi)


def _cut_z(ax: float, az: float, bx: float, bz: float, z_line: float, x_lo: float, x_hi: float) -> tuple[float, float]:
    """Canonical intersection of segment a->b with the horizontal line z = z_line."""
    x = ax + (z_line - az) / (bz - az) * (bx - ax)
    return min(max(x, x_lo), x_hi), z_line


def _clip_segment(a: tuple[float, float], b: tuple[float, float], box: Box):
    """Liang-Barsky against the closed box.

    Returns ``None`` when the segment misses the box, else the entry and exit
    points as float tuples (``a``/``b`` themselves when the segment starts or
    ends inside). The crossing parameter of border line ``L`` is always
    ``(L - ax) / dx`` (or the z analogue), so every tile sharing that line
    computes the same float and makes the same min/max decisions; ``t == 0``
    happens exactly when ``a`` is inside and ``t == 1`` when ``b`` is.
    """
    ax, az = a
    bx, bz = b
    x0, z0, x1, z1 = box
    dx = bx - ax
    dz = bz - az
    inf = float("inf")
    if dx > 0.0:
        lx_in, lx_out = x0, x1
    elif dx < 0.0:
        lx_in, lx_out = x1, x0
    elif x0 <= ax <= x1:
        lx_in = lx_out = ax  # parallel and inside: never binding (t = +-inf)
    else:
        return None
    if dz > 0.0:
        lz_in, lz_out = z0, z1
    elif dz < 0.0:
        lz_in, lz_out = z1, z0
    elif z0 <= az <= z1:
        lz_in = lz_out = az
    else:
        return None
    tx_in, tx_out = ((lx_in - ax) / dx, (lx_out - ax) / dx) if dx != 0.0 else (-inf, inf)
    tz_in, tz_out = ((lz_in - az) / dz, (lz_out - az) / dz) if dz != 0.0 else (-inf, inf)
    t0 = max(0.0, tx_in, tz_in)
    t1 = min(1.0, tx_out, tz_out)
    if t0 > t1:
        return None

    def point(t: float, tx: float, tz: float, lx: float, lz: float) -> tuple[float, float]:
        if t == 0.0:
            return ax, az
        if t == 1.0:
            return bx, bz
        if tx == t and tz == t:  # corner hit
            return lx, lz
        if tx == t:
            return _cut_x(ax, az, bx, bz, lx, z0, z1)
        return _cut_z(ax, az, bx, bz, lz, x0, x1)

    return point(t0, tx_in, tz_in, lx_in, lz_in), point(t1, tx_out, tz_out, lx_out, lz_out)


def _make_piece(run: list[tuple[float, float]], prev_ctx: bool, next_ctx: bool) -> Piece | None:
    lo = 1 if prev_ctx else 0
    hi = len(run) - (1 if next_ctx else 0)
    inner: list[tuple[float, float]] = []
    for p in run[lo:hi]:
        if not inner or p != inner[-1]:
            inner.append(p)
    if len(inner) < 2:  # zero length inside the box
        return None
    pts = ([run[0]] if prev_ctx else []) + inner + ([run[-1]] if next_ctx else [])
    return Piece(np.array(pts, dtype=np.float64), prev_ctx, next_ctx)


def clip_polyline(points, box: Box) -> list[Piece]:
    """Clip a polyline to the closed box ``(x0, z0, x1, z1)``.

    Returns the maximal in-box runs in polyline order. A run cut at its start
    gets the previous original vertex prepended as a context point
    (``has_prev_ctx``), and likewise at its end. Consecutive duplicate vertices
    are removed first, and pieces with zero in-box length are dropped.

    A closed polyline (first vertex == last vertex) that leaves the box is
    rotated to start at its first outside vertex, so that the run passing
    through the closing vertex is not split there.
    """
    pts = _dedupe_consecutive(_as_points(points))
    if len(pts) < 2:
        return []
    if not np.all(np.isfinite(pts)):
        raise ValueError("non-finite polyline coordinates")
    x0, z0, x1, z1 = (float(v) for v in box)
    if not (x0 < x1 and z0 < z1):
        raise ValueError(f"bad box {box}")
    box = (x0, z0, x1, z1)
    inside = (pts[:, 0] >= x0) & (pts[:, 0] <= x1) & (pts[:, 1] >= z0) & (pts[:, 1] <= z1)
    if inside.all():
        return [Piece(pts.copy(), False, False)]
    if len(pts) > 2 and np.array_equal(pts[0], pts[-1]):
        k = int(np.flatnonzero(~inside)[0])
        pts = np.concatenate([pts[k:-1], pts[:k + 1]])
        inside = np.concatenate([inside[k:-1], inside[:k + 1]])

    a_pts, b_pts = pts[:-1], pts[1:]
    touch = ~((np.maximum(a_pts[:, 0], b_pts[:, 0]) < x0) | (np.minimum(a_pts[:, 0], b_pts[:, 0]) > x1)
              | (np.maximum(a_pts[:, 1], b_pts[:, 1]) < z0) | (np.minimum(a_pts[:, 1], b_pts[:, 1]) > z1))
    pieces: list[Piece] = []
    run: list[tuple[float, float]] | None = None
    prev_ctx = False

    def close(next_ctx: bool) -> None:
        nonlocal run
        piece = _make_piece(run, prev_ctx, next_ctx)
        if piece is not None:
            pieces.append(piece)
        run = None

    pl = pts.tolist()
    for i in np.flatnonzero(touch).tolist():
        a, b = (pl[i][0], pl[i][1]), (pl[i + 1][0], pl[i + 1][1])
        ia, ib = bool(inside[i]), bool(inside[i + 1])
        if ia and ib:
            if run is None:  # only possible for the very first segment
                run, prev_ctx = [a], False
            run.append(b)
            continue
        res = _clip_segment(a, b, box)
        if res is None:
            continue  # misses the box; ``a`` is outside so no run is open
        e, x = res
        if ia:
            if run is None:
                run, prev_ctx = [a], False
            run.append(x)
            run.append(b)
            close(True)
        else:
            run, prev_ctx = [a, e], True
            if ib:
                run.append(b)
            else:
                run.append(x)
                run.append(b)
                close(True)
    if run is not None:
        close(False)
    return pieces


def stitch_pieces(pieces: list[Piece]) -> list[np.ndarray]:
    """Chain clipped pieces (e.g. from all tiles a polyline touches) back into
    polylines by matching a piece's last in-box point to another piece's first
    in-box point, bit for bit. Context points are dropped and shared junction
    points kept once. Used by tests and QA to prove seam consistency; pieces
    duplicated along a border line are not de-overlapped."""
    inner = [p.inner for p in pieces]
    key = lambda pt: (float(pt[0]), float(pt[1]))  # noqa: E731
    by_first: dict[tuple[float, float], list[int]] = defaultdict(list)
    lasts = set()
    for i, pts in enumerate(inner):
        by_first[key(pts[0])].append(i)
        lasts.add(key(pts[-1]))
    order = [i for i in range(len(inner)) if key(inner[i][0]) not in lasts]
    order += [i for i in range(len(inner)) if i not in set(order)]
    used: set[int] = set()
    chains: list[np.ndarray] = []
    for head in order:
        if head in used:
            continue
        used.add(head)
        parts = [inner[head]]
        cur = key(inner[head][-1])
        while True:
            nxt = next((j for j in by_first.get(cur, ()) if j not in used), None)
            if nxt is None:
                break
            used.add(nxt)
            parts.append(inner[nxt][1:])
            cur = key(inner[nxt][-1])
        chains.append(np.concatenate(parts))
    return chains


# ---------------------------------------------------------------------------
# Polygons
# ---------------------------------------------------------------------------
def _polygon_parts(geom) -> list[Polygon]:
    if geom is None or geom.is_empty:
        return []
    if isinstance(geom, Polygon):
        return [geom]
    if hasattr(geom, "geoms"):
        out: list[Polygon] = []
        for g in geom.geoms:
            out.extend(_polygon_parts(g))
        return out
    return []  # points and lines left over from a clip


def _poly_sort_key(p: Polygon) -> tuple:
    return (*p.bounds, p.area, shapely.to_wkb(p, output_dimension=2, byte_order=1))


def clip_polygon(geom, box: Box, min_area: float = MIN_POLYGON_AREA_M2) -> list[Polygon]:
    """Clip a shapely Polygon or MultiPolygon to the box.

    The input is made valid first. The result is a list of valid polygons with
    counter-clockwise shells and clockwise holes, parts smaller than
    ``min_area`` dropped, sorted deterministically (by bounds, then area, then
    WKB). Vertices created by the clip lie exactly on the box border.
    """
    if geom is None or geom.is_empty:
        return []
    if not isinstance(geom, (Polygon, MultiPolygon)) and not hasattr(geom, "geoms"):
        raise TypeError(f"expected a polygonal geometry, got {geom.geom_type}")
    x0, z0, x1, z1 = (float(v) for v in box)
    if not geom.is_valid:
        geom = shapely.make_valid(geom)
    gx0, gz0, gx1, gz1 = geom.bounds
    if gx0 >= x0 and gz0 >= z0 and gx1 <= x1 and gz1 <= z1:
        clipped = geom
    else:
        clipped = shapely.clip_by_rect(geom, x0, z0, x1, z1)
        if not clipped.is_valid:
            clipped = shapely.make_valid(clipped)
    out = [_shapely_orient(p, 1.0) for p in _polygon_parts(clipped) if p.area >= min_area]
    out.sort(key=_poly_sort_key)
    return out


def _ring_points(ring) -> np.ndarray:
    pts = np.asarray(ring.coords, dtype=np.float64)[:, :2]
    if len(pts) > 1 and np.array_equal(pts[0], pts[-1]):
        pts = pts[:-1]
    pts = _dedupe_consecutive(pts)
    if len(pts) > 1 and np.array_equal(pts[0], pts[-1]):
        pts = pts[:-1]
    return pts


def triangulate(poly: Polygon) -> tuple[np.ndarray, np.ndarray, list[tuple[int, int]]]:
    """Constrained Delaunay triangulation of a polygon with holes.

    Returns ``(vertices, indices, rings)``: ``vertices`` (V, 2) float64 are the
    ring vertices in ring order (shell first, counter-clockwise, then each hole,
    clockwise; closing vertices not repeated), ``indices`` (T*3,) int64 are
    counter-clockwise triangles referencing those vertices, and ``rings`` are
    ``(start, n)`` ranges into ``vertices``. GEOS's CDT respects holes and adds
    no Steiner points; a Steiner point would raise ``ValueError``.
    """
    if not isinstance(poly, Polygon):
        raise TypeError(f"triangulate expects a Polygon, got {type(poly).__name__}")
    empty = (np.zeros((0, 2), np.float64), np.zeros(0, np.int64), [])
    if poly.is_empty:
        return empty
    shell = orient_ring(_ring_points(poly.exterior), ccw=True)
    if len(shell) < 3:
        return empty
    holes = [orient_ring(h, ccw=False) for h in (_ring_points(r) for r in poly.interiors) if len(h) >= 3]
    rings_pts = [shell, *holes]
    verts = np.concatenate(rings_pts)
    rings: list[tuple[int, int]] = []
    start = 0
    for r in rings_pts:
        rings.append((start, len(r)))
        start += len(r)

    tris = shapely.get_parts(shapely.constrained_delaunay_triangles(Polygon(shell, holes)))
    if len(tris) == 0:
        return verts, np.zeros(0, np.int64), rings
    coords = shapely.get_coordinates(shapely.get_exterior_ring(tris))
    if len(coords) != 4 * len(tris):
        raise ValueError("unexpected triangle ring layout from GEOS")
    coords = coords.reshape(-1, 4, 2)[:, :3, :]
    lookup: dict[tuple[float, float], int] = {}
    for i, (x, z) in enumerate(verts.tolist()):
        lookup.setdefault((x, z), i)
    try:
        idx = np.array([[lookup[(x, z)] for x, z in tri] for tri in coords.tolist()], dtype=np.int64)
    except KeyError as e:
        raise ValueError(f"triangulation introduced a Steiner point {e}") from None
    p = verts[idx]  # (T, 3, 2)
    cross = ((p[:, 1, 0] - p[:, 0, 0]) * (p[:, 2, 1] - p[:, 0, 1])
             - (p[:, 1, 1] - p[:, 0, 1]) * (p[:, 2, 0] - p[:, 0, 0]))
    idx = idx[cross != 0.0]
    flip = cross[cross != 0.0] < 0.0
    idx[flip] = idx[flip][:, [0, 2, 1]]
    return verts, idx.reshape(-1), rings


def triangles_area(vertices: np.ndarray, indices: np.ndarray) -> float:
    """Sum of signed triangle areas (positive for counter-clockwise)."""
    if len(indices) == 0:
        return 0.0
    p = np.asarray(vertices, dtype=np.float64)[np.asarray(indices).reshape(-1, 3)]
    o = p[:, 0, :]
    cross = ((p[:, 1, 0] - o[:, 0]) * (p[:, 2, 1] - o[:, 1]) - (p[:, 1, 1] - o[:, 1]) * (p[:, 2, 0] - o[:, 0]))
    return 0.5 * float(cross.sum())
