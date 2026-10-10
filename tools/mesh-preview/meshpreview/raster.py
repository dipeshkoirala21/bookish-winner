"""Vectorised triangle rasteriser (numpy only).

Triangles arrive already projected: screen x and y in pixels (y down, pixel centres at +0.5) and a depth key ``q``
that is affine in screen space and larger for nearer points (``1/z`` for a perspective camera, ``-z`` for an
orthographic one). The z-buffer keeps the largest ``q``; with ``ids`` on it also records, per pixel, the winning
triangle and its screen-space barycentrics, from which :mod:`meshpreview.shading` interpolates the attributes.

Speed comes from bucketing triangles by the size of their pixel bounding box: every triangle of a bucket is tested
against the same grid of pixel offsets with numpy broadcasting (no per-candidate gathers), exact sizes up to 16 px and
powers of two above (padding is masked). Hidden-surface resolution is ``np.maximum.at`` on the flat depth buffer.
"""

from __future__ import annotations

import numpy as np

_EXACT = 16  # bounding boxes up to this many pixels per side are bucketed by their exact size
_EPS = 1e-6  # inside test tolerance on barycentrics (pixel centres on a shared edge go to both triangles)


class Target:
    """A W x H render target: depth keys ``q`` (-inf = empty) and, with ``ids``, the triangle index and the
    screen-space barycentrics ``b0``/``b1`` of vertices 0 and 1 of the nearest triangle per pixel (flat arrays,
    row-major, row 0 at the top)."""

    def __init__(self, width: int, height: int, ids: bool = True):
        self.width = int(width)
        self.height = int(height)
        n = self.width * self.height
        self.q = np.full(n, -np.inf, dtype=np.float32)
        self.ids = ids
        if ids:
            self.tri = np.full(n, -1, dtype=np.int32)
            self.b0 = np.zeros(n, dtype=np.float32)
            self.b1 = np.zeros(n, dtype=np.float32)
            self._slot = np.zeros(n, dtype=np.int64)


def _class_size(n: np.ndarray) -> np.ndarray:
    """Bucket size per side: exact up to ``_EXACT``, else the next power of two."""
    out = n.astype(np.int64).copy()
    big = out > _EXACT
    if big.any():
        out[big] = np.left_shift(1, np.ceil(np.log2(out[big])).astype(np.int64))
    return out


def rasterize(target: Target, x: np.ndarray, y: np.ndarray, q: np.ndarray, budget: int = 1 << 21) -> int:
    """Draw triangles (``x``, ``y``, ``q`` are (T, 3) arrays) into ``target``; returns the number of covered
    candidate samples. The triangle index written to ``target.tri`` is the row index in these arrays. Both
    windings are drawn (cull before calling)."""
    W, H = target.width, target.height
    if x.shape[0] == 0:
        return 0
    x = np.asarray(x, dtype=np.float64)
    y = np.asarray(y, dtype=np.float64)
    q = np.asarray(q, dtype=np.float64)
    x0, x1, x2 = x[:, 0], x[:, 1], x[:, 2]
    y0, y1, y2 = y[:, 0], y[:, 1], y[:, 2]
    area2 = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0)
    with np.errstate(invalid="ignore"):
        xmin = np.maximum(np.ceil(x.min(axis=1) - 0.5), 0.0)
        xmax = np.minimum(np.floor(x.max(axis=1) - 0.5), W - 1.0)
        ymin = np.maximum(np.ceil(y.min(axis=1) - 0.5), 0.0)
        ymax = np.minimum(np.floor(y.max(axis=1) - 0.5), H - 1.0)
        keep = (np.abs(area2) > 1e-12) & (xmax >= xmin) & (ymax >= ymin) & np.isfinite(area2) & np.isfinite(q).all(axis=1)
    idx = np.flatnonzero(keep)
    if idx.size == 0:
        return 0
    x0, x1, x2, y0, y1, y2 = x0[idx], x1[idx], x2[idx], y0[idx], y1[idx], y2[idx]
    q0, q1, q2 = q[idx, 0], q[idx, 1], q[idx, 2]
    inv = 1.0 / area2[idx]
    xmin, xmax, ymin, ymax = xmin[idx], xmax[idx], ymin[idx], ymax[idx]
    ox, oy = xmin + 0.5, ymin + 0.5  # centre of the bounding box's first pixel
    # Barycentric planes in the box's local pixel coordinates (X, Y integers): b = A X + B Y + C.
    a0 = -(y2 - y1) * inv
    bb0 = (x2 - x1) * inv
    c0 = ((x2 - x1) * (oy - y1) - (y2 - y1) * (ox - x1)) * inv
    a1 = -(y0 - y2) * inv
    bb1 = (x0 - x2) * inv
    c1 = ((x0 - x2) * (oy - y2) - (y0 - y2) * (ox - x2)) * inv
    aq = (q0 - q2) * a0 + (q1 - q2) * a1
    bq = (q0 - q2) * bb0 + (q1 - q2) * bb1
    cq = q2 + (q0 - q2) * c0 + (q1 - q2) * c1
    bw = (xmax - xmin).astype(np.int64) + 1
    bh = (ymax - ymin).astype(np.int64) + 1
    base = ymin.astype(np.int64) * W + xmin.astype(np.int64)
    cw, ch = _class_size(bw), _class_size(bh)
    key = cw * 65536 + ch
    order = np.argsort(key, kind="stable")
    skey = key[order]
    bounds = np.flatnonzero(np.diff(skey)) + 1
    starts = np.concatenate(([0], bounds))
    ends = np.concatenate((bounds, [skey.size]))

    coef = np.stack([a0, bb0, c0, a1, bb1, c1, aq, bq, cq], axis=1).astype(np.float32)
    covered = 0
    for s, e in zip(starts, ends):
        members = order[s:e]
        k = int(skey[s])
        kw, kh = k // 65536, k % 65536
        npx = kw * kh
        lx = np.tile(np.arange(kw, dtype=np.float32), kh)[None, :]
        ly = np.repeat(np.arange(kh, dtype=np.float32), kw)[None, :]
        off = (np.repeat(np.arange(kh, dtype=np.int64), kw) * W + np.tile(np.arange(kw, dtype=np.int64), kh))
        padded = kw > _EXACT or kh > _EXACT
        if padded:
            lxi = np.tile(np.arange(kw, dtype=np.int64), kh)[None, :]
            lyi = np.repeat(np.arange(kh, dtype=np.int64), kw)[None, :]
        step = max(1, budget // npx)
        for m0 in range(0, members.size, step):
            mem = members[m0:m0 + step]
            c = coef[mem]
            b0 = c[:, 0:1] * lx + c[:, 1:2] * ly + c[:, 2:3]
            b1 = c[:, 3:4] * lx + c[:, 4:5] * ly + c[:, 5:6]
            inside = (b0 >= -_EPS) & (b1 >= -_EPS) & (b0 + b1 <= 1.0 + _EPS)
            if padded:
                inside &= (lxi < bw[mem][:, None]) & (lyi < bh[mem][:, None])
            ii, kk = np.nonzero(inside)
            if ii.size == 0:
                continue
            qq = (c[:, 6:7] * lx + c[:, 7:8] * ly + c[:, 8:9])[inside]
            pix = base[mem][ii] + off[kk]
            covered += ii.size
            np.maximum.at(target.q, pix, qq)
            if not target.ids:
                continue
            sel = np.flatnonzero(qq >= target.q[pix])
            p = pix[sel]
            target._slot[p] = sel
            chosen = target._slot[p]  # one candidate per pixel even when depths tie
            target.tri[p] = idx[mem[ii[chosen]]]
            target.b0[p] = b0[inside][chosen]
            target.b1[p] = b1[inside][chosen]
    return covered
