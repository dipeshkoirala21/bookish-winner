"""Multi-view sheets: panels in a grid under a header (file, triangle/vertex counts, size), each panel labelled with
its view, the grid step and a north arrow."""

from __future__ import annotations

import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFont

from .renderer import Options, Scene, View, render_view

_FONTS = {}


def font(size: int):
    if size not in _FONTS:
        try:
            _FONTS[size] = ImageFont.load_default(size=size)
        except TypeError:  # Pillow < 10.1
            _FONTS[size] = ImageFont.load_default()
    return _FONTS[size]


def _grid_shape(n: int):
    if n <= 3:
        return 1, n
    if n == 4:
        return 2, 2
    return 2, 3


def _fmt_m(v: float) -> str:
    if v >= 100:
        return f"{v:.0f}"
    if v >= 10:
        return f"{v:.1f}"
    return f"{v:.2f}"


def _compass(draw: ImageDraw.ImageDraw, cam, scene, x0: int, y0: int, h: int):
    """North arrow (OBJ -Z = Unity +Z) in the panel's lower-left corner."""
    r = max(14, h // 30)
    cx, cy = x0 + r + 10, y0 + h - r - 10
    draw.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(255, 255, 255), outline=(60, 60, 70), width=2)
    at = 0.5 * (scene.lo + scene.hi)
    L = max(float(np.linalg.norm(scene.extent)) * 0.1, 1e-3)
    dx, dy = cam.screen_dir(np.array([0.0, 0.0, -L]), at)
    if cam.ortho:
        ppm = cam.ortho_scale
    else:
        z = float((at - cam.eye) @ cam.fwd)
        ppm = cam.focal / max(z, 1e-6)
    ratio = math.hypot(dx, dy) / max(L * ppm, 1e-9)
    f = font(max(11, r))
    if ratio < 0.3:
        toward = float(np.array([0.0, 0.0, -1.0]) @ (-cam.fwd)) > 0
        if toward:  # north points at the viewer
            draw.ellipse([cx - 4, cy - 4, cx + 4, cy + 4], fill=(200, 40, 40))
        else:
            draw.line([cx - 6, cy - 6, cx + 6, cy + 6], fill=(200, 40, 40), width=3)
            draw.line([cx - 6, cy + 6, cx + 6, cy - 6], fill=(200, 40, 40), width=3)
        draw.text((cx + r + 4, cy - r), "N", fill=(200, 40, 40), font=f)
        return
    ln = math.hypot(dx, dy)
    ux, uy = dx / ln, dy / ln
    tip = (cx + ux * (r - 3), cy + uy * (r - 3))
    tail = (cx - ux * (r - 6), cy - uy * (r - 6))
    px, py = -uy, ux
    draw.polygon([tip, (cx + px * 6, cy + py * 6), (cx - px * 6, cy - py * 6)], fill=(200, 40, 40))
    draw.line([tail, (cx, cy)], fill=(60, 60, 70), width=3)
    draw.text((tip[0] + ux * 10 - 5, tip[1] + uy * 10 - 8), "N", fill=(200, 40, 40), font=f)


def render_sheet(scene: Scene, views, opts: Options, title: str | None = None, subtitle: str | None = None) -> Image.Image:
    """Render ``views`` and lay them out under a header; returns the sheet."""
    W = int(opts.size)
    H = int(opts.height or opts.size)
    rows, cols = _grid_shape(len(views))
    hf = font(max(14, min(22, W // 40)))
    header_h = 0 if title is None and subtitle is None else (hf.size + 10) * (2 if subtitle else 1) + 8
    gap = 4
    sheet = Image.new("RGB", (cols * W + (cols - 1) * gap, header_h + rows * H + (rows - 1) * gap), (40, 42, 48))
    draw = ImageDraw.Draw(sheet)
    if title:
        draw.text((10, 6), title, fill=(255, 255, 255), font=hf)
    if subtitle:
        draw.text((10, 6 + hf.size + 8), subtitle, fill=(200, 205, 215), font=hf)
    lf = font(max(12, min(20, W // 45)))
    for i, v in enumerate(views):
        img, cam = render_view(scene, v, opts)
        r, c = divmod(i, cols)
        x0, y0 = c * (W + gap), header_h + r * (H + gap)
        sheet.paste(img, (x0, y0))
        label = f"{v.name}  az {v.az:.0f}  el {v.el:.0f}" + ("  ortho" if opts.ortho else "")
        draw.rectangle([x0, y0, x0 + int(draw.textlength(label, font=lf)) + 14, y0 + lf.size + 10], fill=(255, 255, 255))
        draw.text((x0 + 7, y0 + 4), label, fill=(30, 30, 40), font=lf)
        if opts.grid:
            g = f"grid {_fmt_m(scene.grid_step).rstrip('0').rstrip('.')} m"
            tw = int(draw.textlength(g, font=lf))
            draw.rectangle([x0 + W - tw - 16, y0 + H - lf.size - 12, x0 + W, y0 + H], fill=(255, 255, 255))
            draw.text((x0 + W - tw - 8, y0 + H - lf.size - 8), g, fill=(30, 30, 40), font=lf)
        _compass(draw, cam, scene, x0, y0, H)
    return sheet


def describe(scene: Scene, path: str) -> tuple:
    """(title, subtitle) for a sheet."""
    m = scene.mesh
    e = scene.extent
    title = f"{os.path.basename(path)}   {m.triangle_count:,} tris   {m.vertex_count:,} verts"
    sub = f"size {_fmt_m(e[0])} x {_fmt_m(e[2])} m plan, {_fmt_m(e[1])} m tall"
    names = [g for g in m.groups if g != "default"]
    if names:
        sub += f"   groups: {len(names)}"
    return title, sub
