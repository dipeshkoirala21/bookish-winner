"""Material channels (docs/W2_DETAIL_CONTRACT.md §5): a simple procedural pattern per channel so brick, wood, tiles,
stone and foliage read in previews, a false-colour palette for ``--shading channels`` and the channel names.

A mesh with UV0 stores ``u = MaterialChannel`` and ``v = baked AO`` (1 open, 0 occluded). The patterns here are a
stand-in for the look package's runtime textures: they multiply or blend the vertex albedo in world space (box
projection, metres), and fade to their average where a texel would be smaller than a pixel, so far surfaces do not
shimmer.
"""

from __future__ import annotations

import numpy as np

# Core/Meshing/MaterialChannel.cs, in order.
NAMES = [
    "Plain", "Plaster", "Brick", "BrickGlazed", "Wood", "WoodCarved", "RoofTile", "Metal", "Gilt", "Stone",
    "Asphalt", "Concrete", "Grass", "Foliage", "Bark", "Fabric", "Skin", "Glass", "Water", "Paint", "Rubber", "Dirt",
    "Flagstone", "Hair", "Leather", "Marking",
]

# --shading channels: distinct sRGB colours per channel (unknown channels are grey).
PALETTE = np.array([
    (0.80, 0.80, 0.80), (0.96, 0.90, 0.72), (0.78, 0.25, 0.18), (0.55, 0.10, 0.22), (0.70, 0.48, 0.25),
    (0.45, 0.25, 0.10), (0.85, 0.42, 0.30), (0.55, 0.62, 0.70), (1.00, 0.80, 0.10), (0.50, 0.50, 0.46),
    (0.20, 0.20, 0.24), (0.72, 0.74, 0.70), (0.35, 0.75, 0.25), (0.10, 0.50, 0.20), (0.40, 0.28, 0.18),
    (0.80, 0.30, 0.80), (1.00, 0.72, 0.58), (0.55, 0.85, 1.00), (0.15, 0.40, 0.90), (0.95, 0.35, 0.60),
    (0.08, 0.08, 0.08), (0.60, 0.45, 0.30), (0.62, 0.58, 0.48), (0.30, 0.18, 0.10), (0.50, 0.30, 0.20),
    (1.00, 1.00, 1.00),
], dtype=np.float32)


def name(ch: int) -> str:
    return NAMES[ch] if 0 <= ch < len(NAMES) else f"#{ch}"


def palette(ch: np.ndarray) -> np.ndarray:
    """False colour (N, 3) per channel id."""
    ch = np.asarray(ch)
    out = np.full((ch.size, 3), 0.5, dtype=np.float32)
    ok = (ch >= 0) & (ch < len(PALETTE))
    out[ok] = PALETTE[ch[ok]]
    return out


# ---- hashing and noise (vectorised, deterministic) -------------------------------------------------------------

def _hash(ix: np.ndarray, iy: np.ndarray, seed: int = 0) -> np.ndarray:
    """Uniform 0..1 per integer lattice point."""
    h = (ix.astype(np.int64) * 73856093) ^ (iy.astype(np.int64) * 19349663) ^ (seed * 83492791 + 0x9E3779B9)
    h = h & 0xFFFFFFFF
    h = (h ^ (h >> 16)) * 0x45D9F3B & 0xFFFFFFFF
    h = (h ^ (h >> 16)) * 0x45D9F3B & 0xFFFFFFFF
    h = h ^ (h >> 16)
    return (h & 0xFFFFFF).astype(np.float32) / float(0x1000000)


def _noise(x: np.ndarray, y: np.ndarray, scale: float, seed: int = 0) -> np.ndarray:
    """Smooth value noise in 0..1 with features of ``scale`` metres."""
    u, v = x / scale, y / scale
    iu, iv = np.floor(u), np.floor(v)
    fu, fv = (u - iu).astype(np.float32), (v - iv).astype(np.float32)
    fu = fu * fu * (3 - 2 * fu)
    fv = fv * fv * (3 - 2 * fv)
    iu, iv = iu.astype(np.int64), iv.astype(np.int64)
    a, b = _hash(iu, iv, seed), _hash(iu + 1, iv, seed)
    c, d = _hash(iu, iv + 1, seed), _hash(iu + 1, iv + 1, seed)
    return (a + (b - a) * fu) + ((c + (d - c) * fu) - (a + (b - a) * fu)) * fv


def _vis(fp: np.ndarray, size: float) -> np.ndarray:
    """1 where a feature of ``size`` metres spans several pixels, 0 where it is sub-pixel (fade to average)."""
    return np.clip(1.5 - fp / (size * 0.25), 0.0, 1.0).astype(np.float32)


def _edge(d: np.ndarray, half_width: float, fp: np.ndarray) -> np.ndarray:
    """Antialiased line mask from the distance ``d`` (metres) to a line of half width ``half_width``."""
    soft = np.maximum(fp * 0.75, 1e-5)
    return np.clip((half_width + soft - d) / (2 * soft), 0.0, 1.0).astype(np.float32)


def _bond(u, v, w, h, mortar, fp, offset=0.5, seed=0):
    """Running-bond units of w x h metres: (mortar mask 0..1, per-unit random 0..1, unit row, unit column)."""
    row = np.floor(v / h)
    uu = u / w + offset * (row % 2)
    col = np.floor(uu)
    du = (uu - col) * w
    dv = (v / h - row) * h
    d = np.minimum(np.minimum(du, w - du), np.minimum(dv, h - dv))
    m = _edge(d, mortar * 0.5, fp)
    return m, _hash(col.astype(np.int64), row.astype(np.int64), seed), row, col


def _planar(p: np.ndarray, n: np.ndarray):
    """Box projection: (u, v) metres on the plane most facing the normal; v is height on walls."""
    a = np.abs(n)
    top = (a[:, 1] >= a[:, 0]) & (a[:, 1] >= a[:, 2])
    xside = ~top & (a[:, 0] >= a[:, 2])
    u = np.where(top, p[:, 0], np.where(xside, p[:, 2], p[:, 0]))
    v = np.where(top, p[:, 2], p[:, 1])
    return u.astype(np.float64), v.astype(np.float64), top


def _slope(p: np.ndarray, n: np.ndarray):
    """Coordinates on a sloped plane: u across the slope, v down the slope (metres); flat faces use x, z."""
    up = np.array([0.0, 1.0, 0.0])
    d = -up[None, :] + n * n[:, 1:2]
    ln = np.linalg.norm(d, axis=1, keepdims=True)
    flat = ln[:, 0] < 0.05
    d = np.where(flat[:, None], np.array([[0.0, 0.0, 1.0]]), d / np.maximum(ln, 1e-9))
    across = np.cross(n, d)
    across /= np.maximum(np.linalg.norm(across, axis=1, keepdims=True), 1e-9)
    return np.einsum("ij,ij->i", p, across), np.einsum("ij,ij->i", p, d)


def _mix(a, b, t):
    t = np.asarray(t, dtype=np.float32)
    if t.ndim == 1:
        t = t[:, None]
    return a + (b - a) * t


def _var(alb, r, amount, vis):
    """Scale albedo by 1 + amount * (2r - 1), faded by visibility."""
    return alb * (1.0 + amount * (2.0 * r - 1.0) * vis)[:, None]


def apply(ch: int, alb: np.ndarray, p: np.ndarray, n: np.ndarray, fp: np.ndarray) -> np.ndarray:
    """Patterned albedo (sRGB, (N, 3)) for samples of one channel at world points ``p`` with normals ``n`` and a
    pixel footprint ``fp`` in metres."""
    alb = alb.astype(np.float32)
    u, v, top = _planar(p, n)
    if ch == 1:  # plaster: soft blotches and grain
        out = _var(alb, _noise(u, v, 0.6, 1), 0.07, _vis(fp, 0.6))
        return _var(out, _noise(u, v, 0.07, 2), 0.04, _vis(fp, 0.07))
    if ch in (2, 3):  # brick (Newari dachi apa): 24 x 7.5 cm, running bond, thin mortar
        glazed = ch == 3
        m, r, _, _ = _bond(u, v, 0.24, 0.075, 0.010 if glazed else 0.014, fp, seed=3)
        vis = _vis(fp, 0.075)
        brick = _var(alb, r, 0.06 if glazed else 0.13, vis)
        if glazed:
            brick = brick * 1.06
        mortar = alb * 0.45 + np.array([0.40, 0.38, 0.34], dtype=np.float32) * 0.55
        out = _mix(brick, mortar, m * vis)
        avg = _mix(alb, mortar, 0.18)
        return _mix(avg, out, vis)
    if ch in (4, 5):  # wood: grain along the height on walls, along x on tops; planks; carved rosettes
        g = np.where(top, v, u)
        a = np.where(top, u, v)
        warp = _noise(g * 0.2, a, 0.5, 4) * 0.08
        grain = 0.5 + 0.5 * np.sin((g + warp) / 0.012)
        out = alb * (0.90 + 0.12 * grain * _vis(fp, 0.02))[:, None]
        plank = (np.abs(((g / 0.16) % 1.0) - 0.5) * 0.16)
        seam = _edge(0.08 - plank, 0.004, fp) * _vis(fp, 0.16)
        out = out * (1.0 - 0.35 * seam)[:, None]
        if ch == 5:
            cu, cv = (u / 0.14) % 1.0 - 0.5, (v / 0.14) % 1.0 - 0.5
            rr = np.sqrt(cu * cu + cv * cv) * 0.14
            ring = _edge(np.abs(rr - 0.045), 0.006, fp) + _edge(rr, 0.015, fp) * 0.6
            petals = 0.5 + 0.5 * np.cos(np.arctan2(cv, cu) * 8)
            vis = _vis(fp, 0.05)
            out = out * (1.0 - (0.45 * np.clip(ring, 0, 1) + 0.15 * petals * (rr < 0.04)) * vis)[:, None]
        return out
    if ch == 6:  # jhingati roof tiles: rows down the slope with curved lower edges
        su, sv = _slope(p, n)
        row = np.floor(sv / 0.12)
        uu = su / 0.18 + 0.5 * (row % 2)
        col = np.floor(uu)
        fu = uu - col
        fv = sv / 0.12 - row
        edge_at = 1.0 - 0.28 * (1.0 - (2.0 * fu - 1.0) ** 2)  # scalloped lower edge
        d = np.abs(fv - edge_at) * 0.12
        side = np.minimum(fu, 1 - fu) * 0.18
        vis = _vis(fp, 0.12)
        line = np.clip(_edge(d, 0.008, fp) + 0.6 * _edge(side, 0.004, fp), 0, 1)
        r = _hash(col.astype(np.int64), row.astype(np.int64), 6)
        out = _var(alb, r, 0.10, vis)
        out = out * (1.0 - 0.10 * np.clip((fv - edge_at) * -2, 0, 1) * vis)[:, None]  # shade towards the overlap
        return out * (1.0 - 0.55 * line * vis)[:, None]
    if ch == 7:  # metal: brushed streaks
        s = _noise(u * 40.0, v, 0.6, 7)
        return _var(alb, s, 0.06, _vis(fp, 0.03)) * 1.03
    if ch == 8:  # gilt: warm sparkle and hammered dents
        out = _var(alb, _noise(u, v, 0.05, 8), 0.12, _vis(fp, 0.05))
        return np.clip(out * 1.08 + np.array([0.04, 0.03, 0.0], np.float32), 0, 1)
    if ch == 9:  # stone: ashlar blocks
        m, r, _, _ = _bond(u, v, 0.55, 0.28, 0.02, fp, seed=9)
        vis = _vis(fp, 0.28)
        out = _var(alb, r, 0.10, vis)
        out = _var(out, _noise(u, v, 0.08, 10), 0.08, _vis(fp, 0.08))
        return out * (1.0 - 0.45 * m * vis)[:, None]
    if ch == 10:  # asphalt: speckle and worn patches
        out = _var(alb, _noise(u, v, 2.5, 11), 0.08, 1.0)
        return _var(out, _hash(np.floor(u / 0.035).astype(np.int64), np.floor(v / 0.035).astype(np.int64), 12), 0.10, _vis(fp, 0.035))
    if ch == 11:  # concrete: blotches and expansion joints every 3 m
        out = _var(alb, _noise(u, v, 0.9, 13), 0.06, 1.0)
        d = np.minimum(np.abs(((u / 3.0) % 1.0) - 0.5), np.abs(((v / 3.0) % 1.0) - 0.5)) * 3.0
        return out * (1.0 - 0.3 * _edge(1.5 - d, 0.008, fp) * _vis(fp, 0.5))[:, None]
    if ch == 12:  # grass: blades and patches
        out = _var(alb, _noise(u, v, 1.8, 14), 0.12, 1.0)
        blade = _hash(np.floor(u / 0.04).astype(np.int64), np.floor(v / 0.04).astype(np.int64), 15)
        return _var(out, blade, 0.14, _vis(fp, 0.04))
    if ch == 13:  # foliage: leaf clumps
        out = _var(alb, _noise(u + p[:, 1], v, 0.22, 16), 0.20, _vis(fp, 0.2))
        return _var(out, _noise(u, v + p[:, 1], 0.06, 17), 0.10, _vis(fp, 0.06))
    if ch == 14:  # bark: vertical furrows
        a = np.where(top, u, u + p[:, 2] * 0.0)
        f = 0.5 + 0.5 * np.sin(a / 0.025 + _noise(a, v * 0.15, 0.4, 18) * 6.0)
        return alb * (0.80 + 0.25 * f * _vis(fp, 0.03))[:, None]
    if ch == 15:  # fabric: weave
        w = (np.floor(u / 0.012) + np.floor(v / 0.012)) % 2
        return _var(alb, w.astype(np.float32), 0.05, _vis(fp, 0.012))
    if ch == 17:  # glass: sky reflection gradient and a diagonal streak
        sky = np.array([0.72, 0.84, 0.95], dtype=np.float32)
        streak = (np.sin((u + v) / 0.35) > 0.85).astype(np.float32) * 0.25
        return _mix(alb * 0.75, sky, 0.30 + streak)
    if ch == 18:  # water: ripples
        r = 0.5 + 0.5 * np.sin(u / 0.18 + _noise(u, v, 0.8, 19) * 6) * np.sin(v / 0.23)
        return _var(alb, r, 0.10, _vis(fp, 0.2))
    if ch == 19:  # paint: very slight unevenness
        return _var(alb, _noise(u, v, 0.4, 20), 0.03, 1.0)
    if ch == 20:  # rubber
        return _var(alb * 0.92, _noise(u, v, 0.03, 21), 0.04, _vis(fp, 0.03))
    if ch == 21:  # dirt: clods and pebbles
        out = _var(alb, _noise(u, v, 0.5, 22), 0.12, 1.0)
        return _var(out, _hash(np.floor(u / 0.05).astype(np.int64), np.floor(v / 0.05).astype(np.int64), 23), 0.12, _vis(fp, 0.05))
    if ch == 22:  # flagstone paving: offset slabs, dark joints
        m, r, _, _ = _bond(u, v, 0.6, 0.45, 0.025, fp, offset=0.37, seed=24)
        vis = _vis(fp, 0.45)
        out = _var(alb, r, 0.12, vis)
        return out * (1.0 - 0.5 * m * vis)[:, None]
    if ch == 23:  # hair: strands
        s = _noise(u * 30.0, v, 0.3, 25)
        return _var(alb, s, 0.10, _vis(fp, 0.01))
    if ch == 24:  # leather
        return _var(alb, _noise(u, v, 0.02, 26), 0.07, _vis(fp, 0.02))
    if ch == 25:  # road marking: worn paint
        return _var(alb, _noise(u, v, 0.15, 27), 0.08, _vis(fp, 0.15))
    return alb  # Plain, Skin and unknown channels
