"""Way-level profiles along road polylines (W2 detail pass): arc lengths, stations and piece look-ups.

``corridors.py`` and ``structures.py`` compute their values once per OSM way, along the way's whole game-space
polyline, so that the pieces a way is cut into at tile borders read the same values on both sides of a border.
This module holds the shared geometry:

* ``dedupe``: a way's game polyline without consecutive duplicate vertices (what ``geom.clip_polyline`` clips,
  so vertex indices agree), with the matching OSM node ids.
* ``cumulative``: arc length at every vertex.
* ``piece_arcs``: the arc position along the way of every point of a clipped piece (vertices and cut points),
  for open and closed ways.
* ``Profile``: values sampled at increasing arc positions, read back by linear interpolation, as windowed minima
  or as maxima over intervals (closed ways wrap).
"""

from __future__ import annotations

from dataclasses import dataclass

import numpy as np


def dedupe(pts: np.ndarray, node_ids: np.ndarray | None = None) -> tuple[np.ndarray, np.ndarray | None]:
    """Drop consecutive duplicate vertices (keeps the first of a run, and its node id)."""
    p = np.asarray(pts, dtype=np.float64).reshape(-1, 2)
    if len(p) < 2:
        return p.copy(), None if node_ids is None else np.asarray(node_ids, dtype=np.int64).copy()
    keep = np.ones(len(p), dtype=bool)
    keep[1:] = np.any(p[1:] != p[:-1], axis=1)
    ids = None if node_ids is None else np.asarray(node_ids, dtype=np.int64).reshape(-1)[keep]
    return p[keep], ids


def cumulative(pts: np.ndarray) -> np.ndarray:
    """Arc length (metres) at every vertex of a polyline."""
    p = np.asarray(pts, dtype=np.float64)
    if len(p) < 2:
        return np.zeros(len(p))
    seg = np.hypot(*np.diff(p, axis=0).T)
    return np.concatenate([[0.0], np.cumsum(seg)])


def is_closed(pts: np.ndarray) -> bool:
    p = np.asarray(pts)
    return len(p) > 2 and bool(np.array_equal(p[0], p[-1]))


def at_arcs(pts: np.ndarray, cum: np.ndarray, arcs) -> tuple[np.ndarray, np.ndarray]:
    """Positions and unit tangents at arc positions (clamped to the polyline; closed ways wrap)."""
    p = np.asarray(pts, dtype=np.float64)
    a = np.asarray(arcs, dtype=np.float64).reshape(-1)
    total = float(cum[-1]) if len(cum) else 0.0
    if len(p) < 2 or total <= 0:
        return np.repeat(p[:1], len(a), axis=0), np.tile([1.0, 0.0], (len(a), 1))
    if is_closed(p):
        a = np.mod(a, total)
    a = np.clip(a, 0.0, total)
    seg = np.diff(p, axis=0)
    ln = np.diff(cum)
    k = np.clip(np.searchsorted(cum, a, side="right") - 1, 0, len(seg) - 1)
    # Skip zero-length segments for the tangent (dedupe() leaves none; be safe anyway).
    t = (a - cum[k]) / np.where(ln[k] > 0, ln[k], 1.0)
    pos = p[k] + seg[k] * t[:, None]
    tan = seg[k] / np.where(ln[k] > 0, ln[k], 1.0)[:, None]
    return pos, tan


def piece_arcs(way: np.ndarray, cum: np.ndarray, piece: np.ndarray, tol: float = 1e-6) -> np.ndarray | None:
    """Arc position along ``way`` (deduped, as clipped) of every point of a ``geom.clip_polyline`` piece.

    A piece is a run of the way's vertices with cut points on the segments between them; its first point is
    always a vertex (the context vertex, or the way's start). Closed ways may wrap: arcs then keep increasing past
    the way's length (read them modulo the length). Returns None if the piece does not follow the way."""
    P = np.asarray(way, dtype=np.float64)
    Q = np.asarray(piece, dtype=np.float64)
    n = len(P)
    if n < 2 or len(Q) == 0:
        return None
    closed = is_closed(P)
    total = float(cum[-1])
    starts = np.flatnonzero((P[:, 0] == Q[0, 0]) & (P[:, 1] == Q[0, 1]))
    for k0 in starts.tolist():
        if closed and k0 == n - 1:
            k0 = 0
        out = np.empty(len(Q))
        out[0] = cum[k0]
        k, off, ok = k0, 0.0, True
        for j in range(1, len(Q)):
            q = Q[j]
            nk = k + 1
            if nk >= n:
                ok = False
                break
            if q[0] == P[nk, 0] and q[1] == P[nk, 1]:
                k = nk
                if closed and k == n - 1:
                    k, off = 0, off + total
                out[j] = cum[k] + off
                continue
            # A cut point on segment (k, k + 1).
            a, b = P[k], P[nk]
            d = b - a
            L = float(np.hypot(d[0], d[1]))
            if L <= 0:
                ok = False
                break
            t = float(np.dot(q - a, d)) / (L * L)
            dist = abs(float(d[0] * (q[1] - a[1]) - d[1] * (q[0] - a[0]))) / L
            if dist > max(tol, 1e-9 * L) * 1e3 or t < -1e-9 or t > 1 + 1e-9:
                ok = False
                break
            out[j] = cum[k] + off + t * L
        if ok:
            return out
    return None


@dataclass
class Profile:
    """Values at increasing arc positions along a way (``closed`` ways wrap at ``total``)."""

    arcs: np.ndarray
    values: np.ndarray
    total: float
    closed: bool = False

    def _wrap(self, a: np.ndarray) -> np.ndarray:
        a = np.asarray(a, dtype=np.float64)
        if self.closed and self.total > 0:
            return np.mod(a, self.total)
        return np.clip(a, self.arcs[0], self.arcs[-1])

    def at(self, a) -> np.ndarray:
        """Linear interpolation (clamped at the ends)."""
        return np.interp(self._wrap(a), self.arcs, self.values)

    def window_min(self, a, half: float) -> np.ndarray:
        """Minimum of the piecewise-linear profile over ``[a - half, a + half]`` (per position)."""
        return self._window(a, half, np.minimum, np.inf)

    def window_max(self, a, half: float) -> np.ndarray:
        return self._window(a, half, np.maximum, -np.inf)

    def _window(self, a, half: float, op, init: float) -> np.ndarray:
        a = np.asarray(a, dtype=np.float64).reshape(-1)
        out = np.full(len(a), init)
        for i, c in enumerate(a.tolist()):
            lo, hi = c - half, c + half
            vals = [self.at(lo).item(), self.at(hi).item()]
            if self.closed and self.total > 0 and hi - lo >= self.total:
                vals.append(float(op.reduce(self.values)))
            else:
                if self.closed and self.total > 0:
                    lo_w, hi_w = np.mod(lo, self.total), np.mod(hi, self.total)
                    if lo_w <= hi_w:
                        m = (self.arcs >= lo_w) & (self.arcs <= hi_w)
                    else:
                        m = (self.arcs >= lo_w) | (self.arcs <= hi_w)
                else:
                    m = (self.arcs >= lo) & (self.arcs <= hi)
                if m.any():
                    vals.append(float(op.reduce(self.values[m])))
            out[i] = op.reduce(np.array(vals))
        return out
