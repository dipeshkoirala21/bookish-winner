"""A material swatch board (``render.py --swatch out.obj``): one wall-and-floor corner per material channel, tinted
with a typical albedo, its baked AO darkening into the corner and a sloped roof strip, laid out west to east in rows
of seven. It shows what each channel's preview pattern looks like and that AO (UV0 v) reaches the renderer."""

from __future__ import annotations

import numpy as np

from . import materials
from .objfile import write_obj

ALBEDO = {
    0: (0.80, 0.80, 0.80), 1: (0.93, 0.88, 0.78), 2: (0.66, 0.30, 0.22), 3: (0.55, 0.18, 0.16), 4: (0.55, 0.36, 0.20),
    5: (0.45, 0.28, 0.15), 6: (0.62, 0.30, 0.22), 7: (0.62, 0.64, 0.68), 8: (0.90, 0.70, 0.25), 9: (0.62, 0.60, 0.55),
    10: (0.30, 0.30, 0.32), 11: (0.70, 0.70, 0.67), 12: (0.40, 0.62, 0.28), 13: (0.25, 0.50, 0.22),
    14: (0.42, 0.32, 0.24), 15: (0.70, 0.25, 0.30), 16: (0.85, 0.62, 0.48), 17: (0.40, 0.55, 0.62),
    18: (0.30, 0.50, 0.62), 19: (0.85, 0.55, 0.20), 20: (0.15, 0.15, 0.16), 21: (0.55, 0.42, 0.30),
    22: (0.60, 0.56, 0.50), 23: (0.18, 0.12, 0.08), 24: (0.45, 0.28, 0.18), 25: (0.95, 0.95, 0.92),
}


def make_swatch(path: str, size: float = 2.0) -> int:
    """Write the board to ``path``; returns the number of channels."""
    pos, nrm, col, uv, tri, groups = [], [], [], [], [], []
    n_ch = len(materials.NAMES)
    seg = 8

    def quad_grid(origin, du, dv, normal, ch, ao_fn):
        base = len(pos)
        flip = float(np.dot(np.cross(du, dv), normal)) < 0  # keep faces counter-clockwise seen from the normal
        for j in range(seg + 1):
            for i in range(seg + 1):
                s, t = i / seg, j / seg
                pos.append(origin + du * s + dv * t)
                nrm.append(normal)
                col.append(ALBEDO.get(ch, (0.8, 0.8, 0.8)))
                uv.append((float(ch), ao_fn(s, t)))
        for j in range(seg):
            for i in range(seg):
                a = base + j * (seg + 1) + i
                b, c, d = a + 1, a + seg + 2, a + seg + 1
                if flip:
                    b, d = d, b
                tri.append((a, b, c))
                tri.append((a, c, d))

    for ch in range(n_ch):
        first = len(tri)
        gx, gz = ch % 7, ch // 7
        o = np.array([gx * (size * 1.5), 0.0, gz * (size * 1.9)])
        # Wall facing north (-Z in OBJ): its plane at the south edge, AO darkest at the floor.
        quad_grid(o + np.array([0.0, 0.0, 0.0]), np.array([size, 0.0, 0.0]), np.array([0.0, size, 0.0]),
                  (0.0, 0.0, -1.0), ch, lambda s, t: 0.35 + 0.65 * min(1.0, t * 3.0))
        # Floor north of the wall, AO darkest along the wall.
        quad_grid(o + np.array([size, 0.0, 0.0]), np.array([-size, 0.0, 0.0]), np.array([0.0, 0.0, -size]),
                  (0.0, 1.0, 0.0), ch, lambda s, t: 0.35 + 0.65 * min(1.0, t * 3.0))
        # A 35-degree roof strip on top of the wall, sloping down to the north.
        h = size * 0.35
        quad_grid(o + np.array([size, size + h, 0.0]), np.array([-size, 0.0, 0.0]), np.array([0.0, -h, -size * 0.5]),
                  tuple(np.array([0.0, 0.5, -h]) / np.linalg.norm([0.0, 0.5, -h])), ch, lambda s, t: 1.0)
        groups.append((materials.name(ch), first, len(tri) - first))
    write_obj(path, np.array(pos), np.array(tri), np.array(col), np.array(nrm), groups,
              comments=["Ghumante material swatch: one corner per MaterialChannel (u), AO in v",
                        "meshpreview: views=front sun=160,55"], uvs=np.array(uv))
    return n_ch
