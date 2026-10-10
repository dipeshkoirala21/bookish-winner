"""Multi-view sheets (panels in a grid under a header: file, triangle/vertex counts, size; each panel labelled with
its view, the grid step and a north arrow), a channel legend for ``--shading channels`` and contact sheets (one
labelled cell per file)."""

from __future__ import annotations

import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFont

from . import materials
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
    if abs(float(cam.fwd[1])) < 0.6 and not cam.ortho:
        # Near-horizontal views (street level): a map-style arrow, screen-up = the way the camera faces.
        heading = math.atan2(float(cam.fwd[0]), -float(cam.fwd[2]))
        dx, dy = -math.sin(heading), -math.cos(heading)
        _arrow(draw, cx, cy, r, dx, dy, font(max(11, r)))
        return
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
    _arrow(draw, cx, cy, r, dx, dy, f)


def _arrow(draw, cx, cy, r, dx, dy, f):
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
    legend = _legend_items(scene, opts)
    if legend:
        lh = _legend(None, legend, sheet.width, lf)
        grown = Image.new("RGB", (sheet.width, sheet.height + lh), (40, 42, 48))
        grown.paste(sheet, (0, 0))
        sheet = grown
        draw = ImageDraw.Draw(sheet)
        _legend(draw, legend, sheet.width, lf, y0=sheet.height - lh)
    for i, v in enumerate(views):
        img, cam = render_view(scene, v, opts)
        r, c = divmod(i, cols)
        x0, y0 = c * (W + gap), header_h + r * (H + gap)
        sheet.paste(img, (x0, y0))
        label = _view_label(v, cam, opts)
        draw.rectangle([x0, y0, x0 + int(draw.textlength(label, font=lf)) + 14, y0 + lf.size + 10], fill=(255, 255, 255))
        draw.text((x0 + 7, y0 + 4), label, fill=(30, 30, 40), font=lf)
        if opts.grid:
            g = f"grid {_fmt_m(scene.grid_step).rstrip('0').rstrip('.')} m"
            tw = int(draw.textlength(g, font=lf))
            draw.rectangle([x0 + W - tw - 16, y0 + H - lf.size - 12, x0 + W, y0 + H], fill=(255, 255, 255))
            draw.text((x0 + W - tw - 8, y0 + H - lf.size - 8), g, fill=(30, 30, 40), font=lf)
        _compass(draw, cam, scene, x0, y0, H)
    return sheet


def _view_label(v: View, cam, opts: Options) -> str:
    if v.eye is not None:
        e = cam.eye
        heading = math.degrees(math.atan2(float(cam.fwd[0]), -float(cam.fwd[2]))) % 360
        return f"{v.name} {e[0]:.1f},{e[1]:.1f},{e[2]:.1f}  heading {heading:.0f}"
    return f"{v.name}  az {v.az:.0f}  el {v.el:.0f}" + ("  ortho" if opts.ortho else "")


def _legend_items(scene: Scene, opts: Options):
    """(name, colour) of the channels present, for ``--shading channels``."""
    if opts.shading != "channels" or scene.CH is None:
        return []
    present = np.unique(scene.CH)
    cols = materials.palette(present)
    return [(materials.name(int(c)), tuple(int(round(x * 255)) for x in col)) for c, col in zip(present, cols)]


def _legend(draw, items, width: int, f, y0: int = 0) -> int:
    """Draw swatches and names (flowing rows) when ``draw`` is given; returns the height needed."""
    x, y = 10, y0 + 6
    row_h = f.size + 10
    for nm, col in items:
        w = 18 + 6 + int(f.getlength(nm)) + 16
        if x + w > width - 10:
            x, y = 10, y + row_h
        if draw is not None:
            draw.rectangle([x, y + 2, x + 18, y + 2 + f.size], fill=col, outline=(255, 255, 255))
            draw.text((x + 24, y), nm, fill=(235, 235, 240), font=f)
        x += w
    return y + row_h - y0 + 2


def render_contact(items, views, opts: Options, title: str | None = None) -> Image.Image:
    """Contact sheet of ``items`` (label, Scene): one view -> a grid of cells, one per file; several views -> one row
    per file. Each cell has a caption strip under the picture: file, triangle count, size (and the view)."""
    W = int(opts.size)
    H = int(opts.height or opts.size)
    n = len(items)
    if len(views) == 1:
        cols = int(math.ceil(math.sqrt(n))) if n > 3 else n
        rows = int(math.ceil(n / cols))
    else:
        cols, rows = len(views), n
    hf = font(max(14, min(22, (W * cols) // 60)))
    header_h = 0 if title is None else hf.size + 18
    lf = font(max(11, min(16, W // 26)))
    lines_per = 3 if len(views) > 1 else 2
    cap = lines_per * (lf.size + 4) + 8
    gap = 4
    ch = H + cap
    sheet = Image.new("RGB", (cols * W + (cols - 1) * gap, header_h + rows * ch + (rows - 1) * gap), (40, 42, 48))
    draw = ImageDraw.Draw(sheet)
    if title:
        draw.text((10, 8), title, fill=(255, 255, 255), font=hf)
    cell = 0
    for label, scene in items:
        for v in views:
            img, cam = render_view(scene, v, opts)
            r, c = divmod(cell, cols)
            x0, y0 = c * (W + gap), header_h + r * (ch + gap)
            sheet.paste(img, (x0, y0))
            _compass(draw, cam, scene, x0, y0, H)
            e = scene.extent
            lines = [label, f"{scene.mesh.triangle_count:,} tris   {_fmt_m(e[0])} x {_fmt_m(e[2])} m, {_fmt_m(e[1])} m tall"]
            if len(views) > 1:
                lines.append(_view_label(v, cam, opts))
            for k, t in enumerate(lines):
                draw.text((x0 + 6, y0 + H + 4 + k * (lf.size + 4)), t, fill=(255, 255, 255) if k == 0 else (190, 196, 210),
                          font=lf)
            cell += 1
    return sheet


def describe(scene: Scene, path: str) -> tuple:
    """(title, subtitle) for a sheet."""
    m = scene.mesh
    e = scene.extent
    used = int(np.unique(m.tri).size) if m.triangle_count < m.vertex_count * 4 else m.vertex_count
    title = f"{os.path.basename(path)}   {m.triangle_count:,} tris   {used:,} verts"
    sub = f"size {_fmt_m(e[0])} x {_fmt_m(e[2])} m plan, {_fmt_m(e[1])} m tall"
    names = [g for g in m.groups if g != "default"]
    if names:
        sub += f"   groups: {len(names)}"
    if scene.CH is not None:
        present = np.unique(scene.CH)
        sub += f"   channels: {', '.join(materials.name(int(c)) for c in present[:8])}" + (" ..." if present.size > 8 else "")
        sub += f"   AO {float(scene.AO.min()):.2f}..{float(scene.AO.max()):.2f}"
    else:
        sub += "   no UV0 (Plain, no AO)"
    return title, sub
