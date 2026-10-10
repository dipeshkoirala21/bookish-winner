"""Cameras: orbit placement by azimuth/elevation, auto-framing, projection and near-plane clipping.

World frame is the OBJ's: right-handed, +Y up. Files from ``core-tests/Support/ObjDump.cs`` map Unity's
(x east, y up, z north) to (x east, y up, z south), so **north is -Z** here. Azimuth is the compass bearing of the
camera as seen from the target, clockwise from north: ``az=0`` stands north of the object looking south (the front
of a Unity +Z-forward vehicle or character), ``az=90`` stands east, ``az=180`` south. Elevation is degrees above the
horizon (90 = straight down, screen-up = north).
"""

from __future__ import annotations

import math
from dataclasses import dataclass

import numpy as np

NORTH = np.array([0.0, 0.0, -1.0])
UP = np.array([0.0, 1.0, 0.0])


def direction(az_deg: float, el_deg: float) -> np.ndarray:
    """Unit vector from the target towards the camera."""
    az, el = math.radians(az_deg), math.radians(el_deg)
    return np.array([math.sin(az) * math.cos(el), math.sin(el), -math.cos(az) * math.cos(el)])


@dataclass
class Camera:
    eye: np.ndarray
    right: np.ndarray
    up: np.ndarray
    fwd: np.ndarray
    width: int
    height: int
    fov_deg: float = 30.0
    ortho: bool = False
    ortho_half_h: float = 1.0
    near: float = 0.01
    az: float = 0.0
    el: float = 0.0

    @property
    def focal(self) -> float:
        """Pixels per unit of x/z (perspective) at the image plane."""
        return 0.5 * self.height / math.tan(math.radians(self.fov_deg) * 0.5)

    @property
    def ortho_scale(self) -> float:
        """Pixels per metre (orthographic)."""
        return 0.5 * self.height / self.ortho_half_h

    def to_view(self, p: np.ndarray) -> np.ndarray:
        """World points (..., 3) -> view space (x right, y up, z forward)."""
        d = p - self.eye
        return np.stack([d @ self.right, d @ self.up, d @ self.fwd], axis=-1)

    def project(self, v: np.ndarray):
        """View-space points (..., 3) -> (sx, sy, q): pixels (y down) and the depth key (larger = nearer)."""
        cx, cy = 0.5 * self.width, 0.5 * self.height
        if self.ortho:
            s = self.ortho_scale
            return cx + v[..., 0] * s, cy - v[..., 1] * s, -v[..., 2]
        f = self.focal
        inv = 1.0 / v[..., 2]
        return cx + v[..., 0] * f * inv, cy - v[..., 1] * f * inv, inv

    def ray(self, px: np.ndarray, py: np.ndarray):
        """World ray origins and (unnormalised, fwd-component 1) directions through pixel positions."""
        cx, cy = 0.5 * self.width, 0.5 * self.height
        if self.ortho:
            s = self.ortho_scale
            o = self.eye + ((px - cx) / s)[..., None] * self.right - ((py - cy) / s)[..., None] * self.up
            d = np.broadcast_to(self.fwd, o.shape)
            return o, d
        f = self.focal
        d = self.fwd + ((px - cx) / f)[..., None] * self.right - ((py - cy) / f)[..., None] * self.up
        return np.broadcast_to(self.eye, d.shape), d

    def screen_dir(self, world_vec: np.ndarray, at: np.ndarray):
        """Screen-space (dx, dy) of a small world vector at ``at`` (for the compass)."""
        a = self.project(self.to_view(at[None, :]))
        b = self.project(self.to_view((at + world_vec)[None, :]))
        return float(b[0][0] - a[0][0]), float(b[1][0] - a[1][0])


def basis(az_deg: float, el_deg: float):
    """(right, up, fwd) of a camera looking at the target from ``direction(az, el)``."""
    fwd = -direction(az_deg, el_deg)
    hint = UP if abs(fwd[1]) < 0.9999 else NORTH
    right = np.cross(fwd, hint)
    right /= np.linalg.norm(right)
    up = np.cross(right, fwd)
    return right, up, fwd


def fit(points: np.ndarray, az: float, el: float, width: int, height: int, fov_deg: float = 30.0,
        ortho: bool = False, margin: float = 0.92, target=None, dist=None, zoom: float = 1.0) -> Camera:
    """A camera at (az, el) that frames ``points`` (N, 3) tightly (perspective: the smallest distance at which an
    off-centre frustum holds every point; orthographic: the extents), or exactly at ``dist`` from ``target`` when
    given. ``zoom`` > 1 moves in (a fraction of the object leaves the frame)."""
    right, up, fwd = basis(az, el)
    pts = np.asarray(points, dtype=np.float64)
    lo, hi = pts.min(axis=0), pts.max(axis=0)
    centre = 0.5 * (lo + hi) if target is None else np.asarray(target, dtype=np.float64)
    radius = max(float(np.linalg.norm(hi - lo)) * 0.5, 1e-3)
    rel = pts - centre
    X, Y, D = rel @ right, rel @ up, rel @ fwd
    aspect = width / float(height)
    cam = Camera(eye=centre.copy(), right=right, up=up, fwd=fwd, width=width, height=height, fov_deg=fov_deg,
                 ortho=ortho, az=az, el=el)
    if ortho:
        if target is None:
            cx, cy = 0.5 * (X.max() + X.min()), 0.5 * (Y.max() + Y.min())
            half = max(0.5 * (Y.max() - Y.min()), 0.5 * (X.max() - X.min()) / aspect, 1e-3)
        else:
            cx = cy = 0.0
            half = max(np.abs(Y).max(), np.abs(X).max() / aspect, 1e-3)
        cam.ortho_half_h = half / margin / zoom
        back = (dist if dist is not None else (D.max() - D.min()) + radius) - D.min()
        cam.eye = centre + right * cx + up * cy - fwd * back
        cam.near = 1e-4
        return cam
    ty = math.tan(math.radians(fov_deg) * 0.5) * margin
    tx = ty * aspect
    if dist is not None:
        cam.eye = centre - fwd * float(dist)
        cam.near = max(1e-4, min(0.05, 0.01 * float(dist)))
        return cam
    if target is not None:
        # Centred on the target: the smallest distance at which every point lies inside the symmetric frustum.
        need = np.maximum(np.abs(X) / tx, np.abs(Y) / ty) - D
        d = max(float(need.max()), float(-D.min()) + radius * 0.05, 1e-3)
        d /= zoom
        cam.eye = centre - fwd * d
        cam.near = max(1e-4, min(0.05, 0.01 * d))
        return cam

    def feasible(d):
        z = D + d
        if (z <= 0).any():
            return False
        return (X - tx * z).max() <= (X + tx * z).min() and (Y - ty * z).max() <= (Y + ty * z).min()

    lo_d = float(-D.min()) + 1e-6
    hi_d = lo_d + 4.0 * radius / ty + 1.0
    while not feasible(hi_d):
        hi_d *= 2.0
    for _ in range(48):
        mid = 0.5 * (lo_d + hi_d)
        if feasible(mid):
            hi_d = mid
        else:
            lo_d = mid
    d = hi_d
    z = D + d
    cx = 0.5 * ((X - tx * z).max() + (X + tx * z).min())
    cy = 0.5 * ((Y - ty * z).max() + (Y + ty * z).min())
    eye = centre + right * cx + up * cy - fwd * d
    if zoom != 1.0:
        aim = centre + right * cx + up * cy  # the point the framed view centres on, at depth d
        eye = aim - fwd * (d / zoom)
        d = d / zoom
    cam.eye = eye
    cam.near = max(1e-4, min(0.05, 0.01 * d))
    return cam


def clip_near(view: np.ndarray, attrs: np.ndarray, src: np.ndarray, near: float):
    """Clip view-space triangles (T, 3, 3) and their per-corner attributes (T, 3, K) against ``z = near``.

    Returns (view, attrs, src) for the kept and split triangles; ``src`` maps each output triangle to its input row.
    Winding is preserved (corners are rotated, never swapped)."""
    z = view[:, :, 2]
    inside = z > near
    n_in = inside.sum(axis=1)
    if (n_in == 3).all():
        return view, attrs, src
    full = n_in == 3
    out_v, out_a, out_s = [view[full]], [attrs[full]], [src[full]]
    va = np.concatenate([view, attrs], axis=2)

    def rotate(rows, first):
        """Rotate corners of ``rows`` so corner ``first`` (per row) comes first."""
        r = va[rows]
        k = np.arange(3)[None, :]
        order = (first[:, None] + k) % 3
        return np.take_along_axis(r, order[:, :, None], axis=1)

    def lerp(a, b):
        t = (near - a[:, 2]) / (b[:, 2] - a[:, 2])
        return a + (b - a) * t[:, None]

    one = np.flatnonzero(n_in == 1)
    if one.size:
        first = np.argmax(inside[one], axis=1)
        r = rotate(one, first)
        a, b, c = r[:, 0], r[:, 1], r[:, 2]
        tri = np.stack([a, lerp(a, b), lerp(a, c)], axis=1)
        out_v.append(tri[:, :, :3])
        out_a.append(tri[:, :, 3:])
        out_s.append(src[one])
    two = np.flatnonzero(n_in == 2)
    if two.size:
        first = np.argmin(inside[two], axis=1)  # the outside corner first: c, then a, b inside
        r = rotate(two, first)
        c, a, b = r[:, 0], r[:, 1], r[:, 2]
        pca = lerp(a, c)  # where edge c-a crosses the plane
        pbc = lerp(b, c)  # where edge b-c crosses the plane
        # The clipped quad (pca, a, b, pbc) keeps the cyclic order c -> a -> b.
        for t in (np.stack([pca, a, b], axis=1), np.stack([pca, b, pbc], axis=1)):
            out_v.append(t[:, :, :3])
            out_a.append(t[:, :, 3:])
            out_s.append(src[two])
    return np.concatenate(out_v), np.concatenate(out_a), np.concatenate(out_s)


def look_at(eye, look, width: int, height: int, fov_deg: float = 30.0, near: float = 0.05) -> Camera:
    """A perspective camera at ``eye`` looking at ``look`` (OBJ coordinates), screen-up towards +Y; its ``az``/``el``
    report the bearing and elevation of the eye as seen from ``look`` (the orbit convention)."""
    eye = np.asarray(eye, dtype=np.float64)
    look = np.asarray(look, dtype=np.float64)
    fwd = look - eye
    ln = float(np.linalg.norm(fwd))
    if ln < 1e-9:
        fwd = -NORTH.copy()
    else:
        fwd = fwd / ln
    hint = UP if abs(fwd[1]) < 0.9999 else NORTH
    right = np.cross(fwd, hint)
    right /= np.linalg.norm(right)
    up = np.cross(right, fwd)
    d = -fwd
    az = math.degrees(math.atan2(d[0], -d[2])) % 360.0
    el = math.degrees(math.asin(max(-1.0, min(1.0, d[1]))))
    return Camera(eye=eye, right=right, up=up, fwd=fwd, width=width, height=height, fov_deg=fov_deg, ortho=False,
                  near=near, az=az, el=el)
