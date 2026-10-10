"""Scene preparation and per-view rendering: z-buffered triangles, toon or Lambert shading that mirrors
``Ghumante/ToonLit`` (3-band ramp, cool shadow tint, trilight ambient, rim), cast shadows from a shadow map, a ground
grid at the mesh's lowest point, cartoon outlines from depth/normal edges, an optional hidden-line wireframe and an
exploded layout. Everything is supersampled ``ssaa`` times and box-filtered down."""

from __future__ import annotations

import math
from dataclasses import dataclass, field

import numpy as np
from PIL import Image

from . import camera as cam_mod
from .objfile import Mesh, connected_components
from .raster import Target, rasterize


@dataclass
class View:
    """A named camera placement (see :mod:`meshpreview.camera` for the angle conventions)."""

    name: str
    az: float
    el: float


PRESETS = {
    "front": View("front", 0.0, 12.0),
    "back": View("back", 180.0, 12.0),
    "right": View("right", 90.0, 12.0),
    "left": View("left", 270.0, 12.0),
    "34": View("3/4", 35.0, 28.0),
    "34back": View("3/4 back", 215.0, 28.0),
    "34left": View("3/4 left", 325.0, 28.0),
    "top": View("top", 0.0, 90.0),
    "street": View("street", 35.0, 6.0),
    "bird": View("bird", 35.0, 55.0),
}

LAYOUTS = {
    1: ["34"],
    2: ["front", "34"],
    3: ["front", "34", "top"],
    4: ["front", "right", "34", "top"],
    5: ["front", "right", "back", "34", "top"],
    6: ["front", "right", "back", "34", "34back", "top"],
}


@dataclass
class Options:
    size: int = 800
    height: int | None = None
    ssaa: int = 2
    fov: float = 30.0
    ortho: bool = False
    shading: str = "toon"  # toon | lambert | unlit | normals
    flat: bool = False
    backfaces: str = "cull"  # cull (as Unity's Cull Back) | show (two-sided) | highlight (magenta)
    outline: bool = True
    outline_width: float = 1.0
    grid: bool = True
    shadows: bool = True
    shadow_res: int = 2048
    sun_az: float = 330.0
    sun_el: float = 50.0
    wire: bool = False
    exploded: float = 0.0
    pull: float = 0.0
    margin: float = 0.9
    zoom: float = 1.0
    target: tuple | None = None
    dist: float | None = None
    bg: tuple = ((0.70, 0.79, 0.89), (0.90, 0.92, 0.95))
    extra: dict = field(default_factory=dict)


# Daytime light of the game's sky (World/Sky/SkyPalette.cs, sun at 35 degrees): sRGB hex values.
def _hex(h):
    return np.array([(h >> 16) & 255, (h >> 8) & 255, h & 255], dtype=np.float32) / 255.0


def srgb_to_linear(c):
    c = np.asarray(c, dtype=np.float32)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def linear_to_srgb(c):
    c = np.clip(c, 0.0, 1.0)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1.0 / 2.4) - 0.055)


LIGHT = srgb_to_linear(_hex(0xFFF4E2)) * 1.05
AMB_SKY = srgb_to_linear(_hex(0x92B6E6))
AMB_EQUATOR = srgb_to_linear(_hex(0xC4D2DE))
AMB_GROUND = srgb_to_linear(_hex(0x6E6A5C))
AMBIENT_STRENGTH = 0.85
SHADOW_TINT = np.array([0.66, 0.72, 0.92], dtype=np.float32)
RIM_COLOR = np.array([1.0, 0.93, 0.82], dtype=np.float32)
RAMP_T = (0.02, 0.42, 0.05)
RAMP_L = (0.32, 0.70, 1.0)
OUTLINE_COLOR = np.array([0.09, 0.07, 0.07], dtype=np.float32)
WIRE_COLOR = np.array([0.05, 0.16, 0.42], dtype=np.float32)
BACK_COLOR = np.array([1.0, 0.0, 0.85], dtype=np.float32)


def _smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


class Scene:
    """Per-corner arrays of a mesh, prepared once for every view: world positions ``P`` (T, 3, 3), shading normals
    ``N``, sRGB colours ``C``, face normals ``FN`` (T, 3), group ids ``G`` and the shadow map."""

    def __init__(self, mesh: Mesh, opts: Options):
        self.mesh = mesh
        self.opts = opts
        pos = mesh.positions.astype(np.float64)
        P = pos[mesh.tri]
        if opts.exploded > 0:
            P = P + self._explode_offsets(mesh, opts.exploded)[:, None, :]
        self.P = P
        e1, e2 = P[:, 1] - P[:, 0], P[:, 2] - P[:, 0]
        fn = np.cross(e1, e2)
        ln = np.linalg.norm(fn, axis=1, keepdims=True)
        self.area = 0.5 * ln[:, 0]
        self.FN = (fn / np.maximum(ln, 1e-20)).astype(np.float32)
        T = mesh.tri.shape[0]
        if mesh.normals is not None and mesh.tri_n is not None and not opts.flat:
            tn = mesh.tri_n
            N = mesh.normals[np.maximum(tn, 0)]
            missing = tn < 0
            if missing.any():
                N[missing] = np.repeat(self.FN[:, None, :], 3, axis=1)[missing]
        else:
            N = np.repeat(self.FN[:, None, :], 3, axis=1)
        self.N = N.astype(np.float32)
        if mesh.colors is not None:
            C = mesh.colors[mesh.tri]
        elif mesh.tri_color is not None:
            C = np.repeat(mesh.tri_color[:, None, :], 3, axis=1)
        else:
            C = np.full((T, 3, 3), 0.78, dtype=np.float32)
        self.C = C.astype(np.float32)
        self.G = mesh.tri_group if mesh.tri_group is not None else np.zeros(T, dtype=np.int32)
        flat = P.reshape(-1, 3)
        self.lo, self.hi = flat.min(axis=0), flat.max(axis=0)
        self.points = flat
        self.ground_y = float(self.lo[1])
        ext = self.hi - self.lo
        self.extent = ext
        span = max(float(ext[0]), float(ext[2]), 1e-3)
        steps = [0.05, 0.1, 0.25, 0.5, 1, 2, 5, 10, 25, 50, 100, 250, 500, 1000]
        self.grid_step = next((s for s in steps if span / s <= 24), steps[-1])
        self.ground_centre = 0.5 * (self.lo + self.hi)
        self.ground_radius = max(0.8 * 0.5 * math.hypot(ext[0], ext[2]), 0.45 * float(ext[1])) + 2.5 * self.grid_step
        self.shadow = None
        if opts.shadows:
            self._build_shadow_map()

    @staticmethod
    def _explode_offsets(mesh: Mesh, factor: float) -> np.ndarray:
        """Per-triangle offsets pushing each piece (group, else connected component) away from the centre."""
        if len(mesh.groups) > 1 and mesh.tri_group is not None and np.unique(mesh.tri_group).size > 1:
            labels = np.unique(mesh.tri_group, return_inverse=True)[1].reshape(-1)
        else:
            labels = connected_components(mesh)
        cent = mesh.positions[mesh.tri].mean(axis=1)
        k = int(labels.max()) + 1
        cnt = np.bincount(labels, minlength=k).astype(np.float64)
        comp = np.stack([np.bincount(labels, weights=cent[:, i], minlength=k) for i in range(3)], axis=1) / cnt[:, None]
        centre = 0.5 * (mesh.positions.min(axis=0) + mesh.positions.max(axis=0))
        return (comp - centre)[labels] * factor

    def _build_shadow_map(self):
        o = self.opts
        res = int(o.shadow_res)
        sun = cam_mod.fit(self.points, o.sun_az, o.sun_el, res, res, ortho=True, margin=0.98)
        tgt = Target(res, res, ids=False)
        v = sun.to_view(self.P)
        sx, sy, q = sun.project(v)
        rasterize(tgt, sx, sy, q)
        self.shadow = (sun, tgt.q.reshape(res, res))
        self.sun_dir = cam_mod.direction(o.sun_az, o.sun_el).astype(np.float32)

    def shadow_factor(self, pts: np.ndarray, nrm: np.ndarray) -> np.ndarray:
        """Fraction (0..1) of light reaching world points, 2x2 PCF; points off the map are lit."""
        if self.shadow is None:
            return np.ones(pts.shape[0], dtype=np.float32)
        sun, qmap = self.shadow
        res = qmap.shape[0]
        texel = 1.0 / sun.ortho_scale
        towards = np.sign(nrm @ self.sun_dir)[:, None]
        p = pts + nrm * towards * (1.5 * texel)
        v = sun.to_view(p)
        sx, sy, q = sun.project(v)
        fx, fy = sx - 0.5, sy - 0.5
        ix, iy = np.floor(fx).astype(np.int64), np.floor(fy).astype(np.int64)
        wx, wy = (fx - ix).astype(np.float32), (fy - iy).astype(np.float32)
        bias = 2.0 * texel
        lit = np.zeros(pts.shape[0], dtype=np.float32)
        for dx, dy, w in ((0, 0, (1 - wx) * (1 - wy)), (1, 0, wx * (1 - wy)), (0, 1, (1 - wx) * wy), (1, 1, wx * wy)):
            cx, cy = ix + dx, iy + dy
            ok = (cx >= 0) & (cx < res) & (cy >= 0) & (cy < res)
            m = np.full(pts.shape[0], -np.inf, dtype=np.float32)
            m[ok] = qmap[cy[ok], cx[ok]]
            lit += w * ((q + bias >= m) | ~ok)
        return lit


def _shade(opts, albedo_srgb, n, view_dir, shadow, sun_dir):
    """Colour (linear) of surface samples: the ToonLit ramp, Lambert, unlit or normals."""
    if opts.shading == "unlit":
        return srgb_to_linear(albedo_srgb)
    if opts.shading == "normals":
        return srgb_to_linear(0.5 + 0.5 * n)
    albedo = srgb_to_linear(albedo_srgb)
    ndl = n @ sun_dir
    if opts.shading == "lambert":
        ramp = np.clip(ndl, 0.0, 1.0) * shadow
    else:
        t0, t1, soft = RAMP_T
        b1 = _smoothstep(t0 - soft, t0 + soft, ndl)
        b2 = _smoothstep(t1 - soft, t1 + soft, ndl)
        ramp = RAMP_L[0] + (RAMP_L[1] + (RAMP_L[2] - RAMP_L[1]) * b2 - RAMP_L[0]) * b1
        ramp = np.minimum(ramp, RAMP_L[0] + (1.0 - RAMP_L[0]) * shadow)
    ny = n[:, 1:2]
    ambient = (AMB_SKY * np.clip(ny, 0, 1) + AMB_EQUATOR * (1 - np.abs(ny)) + AMB_GROUND * np.clip(-ny, 0, 1)) * AMBIENT_STRENGTH
    cool = SHADOW_TINT + (1.0 - SHADOW_TINT) * np.clip(ramp, 0, 1)[:, None]
    col = albedo * (ambient * cool + LIGHT * ramp[:, None])
    if opts.shading == "toon":
        rim = np.power(np.clip(1.0 - np.clip(np.sum(n * view_dir, axis=1), 0, 1), 0, 1), 3.5) * 0.3
        rim *= np.clip(ndl + 0.35, 0, 1) * shadow
        col = col + rim[:, None] * RIM_COLOR * LIGHT
    return col


def render_view(scene: Scene, view: View, opts: Options):
    """Render one view; returns (PIL image at the output size, camera)."""
    W = int(opts.size)
    H = int(opts.height or opts.size)
    ss = max(1, int(opts.ssaa))
    Ws, Hs = W * ss, H * ss
    cam = cam_mod.fit(scene.points, view.az, view.el, Ws, Hs, fov_deg=opts.fov, ortho=opts.ortho, margin=opts.margin,
                      target=opts.target, dist=opts.dist, zoom=opts.zoom)
    P = scene.P
    if cam.ortho:
        facing = (scene.FN @ (-cam.fwd)) > 0
    else:
        facing = np.einsum("ij,ij->i", scene.FN, (cam.eye[None, :] - P[:, 0]).astype(np.float32)) > 0
    sel = np.flatnonzero(facing) if opts.backfaces == "cull" else np.arange(P.shape[0])
    V = cam.to_view(P[sel])
    attrs = np.concatenate([P[sel], scene.N[sel], scene.C[sel]], axis=2)
    V, attrs, src = cam_mod.clip_near(V, attrs, sel, cam.near)
    sx, sy, q = cam.project(V)
    if opts.pull and not cam.ortho:
        q = q * (1.0 + opts.pull * scene.G[src].astype(np.float64))[:, None]
    elif opts.pull:
        q = q + (opts.pull * float(np.linalg.norm(scene.extent)) * scene.G[src].astype(np.float64))[:, None]
    tgt = Target(Ws, Hs, ids=True)
    rasterize(tgt, sx, sy, q)

    out = np.zeros((Hs * Ws, 3), dtype=np.float32)
    nbuf = np.zeros((Hs * Ws, 3), dtype=np.float32)
    qbuf = np.zeros(Hs * Ws, dtype=np.float64)
    hit = np.flatnonzero(tgt.tri >= 0)
    inv_vz = None if cam.ortho else 1.0 / V[:, :, 2]
    sun_dir = cam_mod.direction(opts.sun_az, opts.sun_el).astype(np.float32)
    for c0 in range(0, hit.size, 400_000):
        pix = hit[c0:c0 + 400_000]
        t = tgt.tri[pix]
        b = np.empty((pix.size, 3), dtype=np.float64)
        b[:, 0] = tgt.b0[pix]
        b[:, 1] = tgt.b1[pix]
        b[:, 2] = 1.0 - b[:, 0] - b[:, 1]
        if inv_vz is not None:
            b *= inv_vz[t]
            b /= b.sum(axis=1, keepdims=True)
        a = attrs[t]
        val = np.einsum("pk,pkc->pc", b, a)
        wpos, nrm, col = val[:, 0:3], val[:, 3:6].astype(np.float32), val[:, 6:9].astype(np.float32)
        nrm /= np.maximum(np.linalg.norm(nrm, axis=1, keepdims=True), 1e-12)
        s = src[t]
        front = facing[s]
        nrm[~front] *= -1.0
        if cam.ortho:
            vdir = np.broadcast_to(-cam.fwd.astype(np.float32), nrm.shape)
        else:
            vdir = (cam.eye[None, :] - wpos).astype(np.float32)
            vdir /= np.maximum(np.linalg.norm(vdir, axis=1, keepdims=True), 1e-12)
        fn = scene.FN[s] * np.where(front, 1.0, -1.0)[:, None].astype(np.float32)
        sh = scene.shadow_factor(wpos, fn) if opts.shading in ("toon", "lambert") else np.ones(pix.size, np.float32)
        lin = _shade(opts, col, nrm, vdir, sh, sun_dir)
        if opts.backfaces == "highlight" and (~front).any():
            lin[~front] = lin[~front] * 0.35 + srgb_to_linear(BACK_COLOR) * 0.65
        out[pix] = lin
        nbuf[pix] = nrm
        qbuf[pix] = tgt.q[pix]

    mesh_mask = tgt.tri >= 0
    bg = _background(Hs, Ws, opts)
    ground_mask = np.zeros(Hs * Ws, dtype=bool)
    if opts.grid:
        ground_mask = _ground(scene, cam, opts, out, nbuf, qbuf, mesh_mask, Ws, Hs, sun_dir)
    covered = mesh_mask | ground_mask
    img = np.where(covered[:, None], linear_to_srgb(out), bg.reshape(-1, 3))
    img = img.reshape(Hs, Ws, 3)

    if opts.wire and hit.size:
        _wire(img, tgt, sx, sy, ss)
    if opts.outline:
        _outline(img, qbuf.reshape(Hs, Ws), nbuf.reshape(Hs, Ws, 3), mesh_mask.reshape(Hs, Ws), ss, opts.outline_width)
    pil = Image.fromarray((np.clip(img, 0, 1) * 255.0 + 0.5).astype(np.uint8), "RGB")
    if ss > 1:
        pil = pil.reduce(ss)
    cam.width, cam.height = W, H  # report the camera at output resolution (focal scales with it)
    return pil, cam


def _background(Hs, Ws, opts):
    top, bottom = np.array(opts.bg[0], dtype=np.float32), np.array(opts.bg[1], dtype=np.float32)
    t = np.linspace(0.0, 1.0, Hs, dtype=np.float32)[:, None, None]
    return np.broadcast_to(top + (bottom - top) * t, (Hs, Ws, 3))


def _ground(scene, cam, opts, out, nbuf, qbuf, mesh_mask, Ws, Hs, sun_dir):
    """Shade the ground plane (grid, shadows, radial fade) where it is nearer than the mesh; returns its mask."""
    gy = scene.ground_y - 1e-4 * max(float(np.linalg.norm(scene.extent)), 1e-3)
    ys, xs = np.mgrid[0:Hs, 0:Ws]
    px = xs.reshape(-1).astype(np.float64) + 0.5
    py = ys.reshape(-1).astype(np.float64) + 0.5
    o, d = cam.ray(px, py)
    dy = d[:, 1]
    with np.errstate(divide="ignore", invalid="ignore"):
        t = (gy - o[:, 1]) / dy
    ok = (dy < -1e-9) & (t > 0)
    hitp = o + d * t[:, None]
    with np.errstate(divide="ignore"):
        q = -t if cam.ortho else 1.0 / t  # the view depth is t: rays start on the camera plane (ortho) or have fwd = 1
    dx = hitp[:, 0] - scene.ground_centre[0]
    dz = hitp[:, 2] - scene.ground_centre[2]
    r = np.sqrt(dx * dx + dz * dz)
    R = scene.ground_radius
    ok &= r < R
    ok &= ~mesh_mask | (q > qbuf)
    idx = np.flatnonzero(ok)
    if idx.size == 0:
        return ok
    hp = hitp[idx]
    step = scene.grid_step
    xw = hp[:, 0].reshape(-1)
    zw = hp[:, 2].reshape(-1)
    # Pixel footprint (world metres per pixel) for antialiased constant-width lines.
    if cam.ortho:
        fp = np.full(idx.size, 1.0 / cam.ortho_scale)
    else:
        fp = t[idx] / cam.focal / np.maximum(np.abs(d[idx, 1]) / np.linalg.norm(d[idx], axis=1), 0.08)
        fp = np.minimum(fp, t[idx] / cam.focal * 6)
    def line(stepm, width):
        fx = np.abs(xw / stepm - np.round(xw / stepm)) * stepm
        fz = np.abs(zw / stepm - np.round(zw / stepm)) * stepm
        dmin = np.minimum(fx, fz)
        return np.clip(1.0 - dmin / (fp * width), 0.0, 1.0)
    base = np.array([0.86, 0.86, 0.84], dtype=np.float32)
    minor = line(step, 0.9)[:, None]
    major = line(step * 5, 1.4)[:, None]
    col = base * (1 - 0.16 * minor) * (1 - 0.22 * major)
    # Axis lines through the mesh centre: x (east) reddish, z (north-south) bluish.
    cx = np.clip(1.0 - np.abs(zw - scene.ground_centre[2]) / (fp * 1.6), 0, 1)[:, None]
    cz = np.clip(1.0 - np.abs(xw - scene.ground_centre[0]) / (fp * 1.6), 0, 1)[:, None]
    col = col * (1 - 0.5 * cx) + np.array([0.80, 0.35, 0.30], dtype=np.float32) * 0.5 * cx
    col = col * (1 - 0.5 * cz) + np.array([0.30, 0.45, 0.80], dtype=np.float32) * 0.5 * cz
    n = np.zeros((idx.size, 3), dtype=np.float32)
    n[:, 1] = 1.0
    sh = scene.shadow_factor(hp, n) if opts.shadows else np.ones(idx.size, np.float32)
    lin = srgb_to_linear(col) * (0.62 + 0.38 * sh[:, None]) * (SHADOW_TINT + (1 - SHADOW_TINT) * sh[:, None])
    fade = _smoothstep(R, 0.72 * R, r[idx])[:, None]
    bgc = srgb_to_linear(_background(Hs, Ws, opts).reshape(-1, 3)[idx])
    out[idx] = lin * fade + bgc * (1 - fade)
    nbuf[idx] = n
    qbuf[idx] = q[idx]
    return ok


def _wire(img, tgt, sx, sy, ss):
    """Darken pixels within ~0.7 output px of an edge of their (visible) triangle."""
    pix = np.flatnonzero(tgt.tri >= 0)
    t = tgt.tri[pix]
    b0 = tgt.b0[pix]
    b1 = tgt.b1[pix]
    b2 = 1.0 - b0 - b1
    X, Y = sx[t], sy[t]
    area2 = np.abs((X[:, 1] - X[:, 0]) * (Y[:, 2] - Y[:, 0]) - (X[:, 2] - X[:, 0]) * (Y[:, 1] - Y[:, 0]))
    l0 = np.hypot(X[:, 2] - X[:, 1], Y[:, 2] - Y[:, 1])
    l1 = np.hypot(X[:, 0] - X[:, 2], Y[:, 0] - Y[:, 2])
    l2 = np.hypot(X[:, 1] - X[:, 0], Y[:, 1] - Y[:, 0])
    d = np.minimum(np.minimum(b0 * area2 / np.maximum(l0, 1e-9), b1 * area2 / np.maximum(l1, 1e-9)),
                   b2 * area2 / np.maximum(l2, 1e-9))
    w = np.clip(1.0 - d / (0.7 * ss), 0.0, 1.0)[:, None] * 0.6
    flat = img.reshape(-1, 3)
    flat[pix] = flat[pix] * (1 - w) + WIRE_COLOR * w


def _outline(img, q, n, mesh, ss, width):
    """Cartoon outlines: silhouettes (mesh vs. not), depth creases (relative Laplacian of the affine depth key)
    and normal creases above ~40 degrees."""
    H, W = q.shape
    edge = np.zeros((H, W), dtype=bool)
    qa = np.abs(q) + 1e-12
    for axis in (0, 1):
        a = np.roll(q, 1, axis=axis)
        b = np.roll(q, -1, axis=axis)
        lap = np.abs(a + b - 2 * q) / qa
        edge |= lap > 0.012
        m2 = np.roll(mesh, -1, axis=axis)
        edge |= mesh != m2
        n2 = np.roll(n, -1, axis=axis)
        both = mesh & m2
        edge |= both & (np.sum(n * n2, axis=2) < 0.77)
    edge[0, :] = edge[-1, :] = False
    edge[:, 0] = edge[:, -1] = False
    edge &= mesh | np.roll(mesh, -1, 0) | np.roll(mesh, -1, 1) | np.roll(mesh, 1, 0) | np.roll(mesh, 1, 1)
    grow = int(round(ss * width)) - 1
    for _ in range(max(0, grow)):
        edge = edge | np.roll(edge, 1, 0) | np.roll(edge, 1, 1)
    img[edge] = img[edge] * 0.18 + OUTLINE_COLOR * 0.82
