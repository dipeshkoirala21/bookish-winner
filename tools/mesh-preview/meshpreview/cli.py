"""Command line: ``python3 tools/mesh-preview/render.py in.obj -o out.png [options]`` (see README.md)."""

from __future__ import annotations

import argparse
import json
import os
import sys
import time

import numpy as np

from .compose import describe, render_sheet
from .objfile import load_obj
from .renderer import LAYOUTS, PRESETS, Options, Scene, View
from .stats import mesh_stats


def resolve_views(spec):
    """Views from an int layout (1-6), a comma list of preset names and ``az:el`` pairs, or View objects."""
    if isinstance(spec, int):
        return [PRESETS[n] for n in LAYOUTS[max(1, min(6, spec))]]
    if isinstance(spec, (list, tuple)) and spec and isinstance(spec[0], View):
        return list(spec)
    items = spec if isinstance(spec, (list, tuple)) else str(spec).split(",")
    if len(items) == 1 and str(items[0]).strip().isdigit():
        return resolve_views(int(items[0]))
    out = []
    for it in items:
        it = str(it).strip()
        key = it.lower().replace("/", "").replace("-", "").replace("three", "3").replace("quarter", "4")
        if key in PRESETS:
            out.append(PRESETS[key])
        elif ":" in it:
            a, e = it.split(":", 1)
            out.append(View(f"custom", float(a), float(e)))
        else:
            raise SystemExit(f"unknown view '{it}' (presets: {', '.join(PRESETS)}; or az:el)")
    return out


def _floats(s, n, what):
    try:
        v = [float(x) for x in s.split(",")]
    except ValueError:
        raise SystemExit(f"{what}: expected {n} comma-separated numbers, got '{s}'")
    if len(v) != n:
        raise SystemExit(f"{what}: expected {n} comma-separated numbers, got '{s}'")
    return v


def _directives(mesh):
    """``# meshpreview: key=value ...`` lines in the OBJ: render defaults the command line overrides."""
    d = {}
    for c in mesh.comments:
        if c.startswith("meshpreview:"):
            for kv in c[len("meshpreview:"):].split():
                if "=" in kv:
                    k, v = kv.split("=", 1)
                    d[k.strip()] = v.strip()
    return d


def build_parser():
    p = argparse.ArgumentParser(
        prog="render.py",
        description="Render OBJ meshes (optional per-vertex colours 'v x y z r g b') to PNG preview sheets.",
        formatter_class=argparse.ArgumentDefaultsHelpFormatter)
    p.add_argument("inputs", nargs="+", help="OBJ file(s)")
    p.add_argument("-o", "--out", help="output PNG (one input) or directory (several); default: next to each input")
    p.add_argument("--views", default=None,
                   help="layout 1-6 (1: 3/4; 2: front,3/4; 3: front,3/4,top; 4: +right; 5: +back; 6: +3/4 back) or a "
                        "comma list of presets (" + ", ".join(PRESETS) + ") and az:el pairs; default 3")
    p.add_argument("--az", type=float, help="camera bearing from the target, degrees clockwise from north (Unity +Z)")
    p.add_argument("--el", type=float, help="camera elevation above the horizon, degrees")
    p.add_argument("--dist", type=float, help="camera distance from the target in metres (default: auto-framed)")
    p.add_argument("--target", help="x,y,z look-at point in OBJ coordinates (default: bounding-box centre)")
    p.add_argument("--zoom", type=float, default=1.0, help="move in by this factor after auto-framing")
    p.add_argument("--size", type=int, default=800, help="panel width in pixels")
    p.add_argument("--height", type=int, help="panel height in pixels (default: square)")
    p.add_argument("--ssaa", type=int, default=2, help="supersampling factor per axis")
    p.add_argument("--fov", type=float, default=30.0, help="vertical field of view, degrees")
    p.add_argument("--ortho", action="store_true", help="orthographic projection")
    p.add_argument("--shading", choices=["toon", "lambert", "unlit", "normals"], help="default toon (as ToonLit)")
    p.add_argument("--flat", action="store_true", default=None, help="ignore file normals: faceted face normals")
    p.add_argument("--backfaces", choices=["cull", "show", "highlight"],
                   help="cull (default, like Unity's Cull Back), show (two-sided) or highlight in magenta")
    p.add_argument("--no-outline", action="store_true", help="no cartoon outlines")
    p.add_argument("--outline-width", type=float, default=1.0, help="outline width in output pixels")
    p.add_argument("--no-grid", action="store_true", help="no ground grid")
    p.add_argument("--no-shadows", action="store_true", help="no cast shadows")
    p.add_argument("--shadow-res", type=int, default=2048, help="shadow map resolution")
    p.add_argument("--sun", help="sun bearing,elevation in degrees (default 330,50)")
    p.add_argument("--wire", action="store_true", help="overlay the visible triangle edges")
    p.add_argument("--exploded", nargs="?", type=float, const=0.6, default=0.0,
                   help="push groups (or connected pieces) apart by this fraction of their offset from the centre")
    p.add_argument("--pull", type=float, default=None,
                   help="depth pull per group index (later groups win near-coplanar ties, like decals on roads)")
    p.add_argument("--group", action="append", help="only groups whose name contains this text (repeatable)")
    p.add_argument("--clip", help="x0,y0,z0,x1,y1,z1: only triangles whose centroid lies in this box")
    p.add_argument("--title", help="header title (default: file name and counts)")
    p.add_argument("--no-header", action="store_true", help="no header bar")
    p.add_argument("--stats", action="store_true", help="print mesh statistics as JSON")
    p.add_argument("-q", "--quiet", action="store_true")
    return p


def options_from(args, mesh) -> tuple:
    d = _directives(mesh)
    o = Options()
    o.size = args.size
    o.height = args.height
    o.ssaa = args.ssaa
    o.fov = args.fov
    o.ortho = args.ortho or d.get("ortho") == "1"
    o.shading = args.shading or d.get("shading", "toon")
    o.flat = bool(args.flat) if args.flat is not None else d.get("flat") == "1"
    o.backfaces = args.backfaces or d.get("backfaces", "cull")
    o.outline = not args.no_outline
    o.outline_width = args.outline_width
    o.grid = not args.no_grid and d.get("grid", "1") != "0"
    o.shadows = not args.no_shadows
    o.shadow_res = args.shadow_res
    sun = args.sun or d.get("sun")
    if sun:
        o.sun_az, o.sun_el = _floats(sun, 2, "--sun")
    o.wire = args.wire
    o.exploded = args.exploded or 0.0
    o.pull = args.pull if args.pull is not None else float(d.get("pull", 0.0))
    o.zoom = args.zoom
    o.dist = args.dist
    o.target = tuple(_floats(args.target, 3, "--target")) if args.target else None
    if args.views is not None:
        views = resolve_views(args.views)
        if args.az is not None or args.el is not None:
            views.append(View("custom", args.az if args.az is not None else 35.0, args.el if args.el is not None else 25.0))
    elif args.az is not None or args.el is not None:
        views = [View("custom", args.az if args.az is not None else 35.0, args.el if args.el is not None else 25.0)]
    else:
        views = resolve_views(d.get("views", "3"))
    return o, views


def _filter(mesh, args):
    keep = np.ones(mesh.triangle_count, dtype=bool)
    if args.group:
        names = [g for g in mesh.groups if any(s in g for s in args.group)]
        ids = [mesh.groups.index(g) for g in names]
        keep &= np.isin(mesh.tri_group, ids)
    if args.clip:
        b = _floats(args.clip, 6, "--clip")
        c = mesh.positions[mesh.tri].mean(axis=1)
        keep &= ((c >= b[:3]) & (c <= b[3:])).all(axis=1)
    if keep.all():
        return mesh
    if not keep.any():
        raise SystemExit("--group/--clip left no triangles")
    return mesh.subset(keep)


def main(argv=None) -> int:
    args = build_parser().parse_args(argv)
    many = len(args.inputs) > 1
    if many and args.out and args.out.lower().endswith(".png"):
        raise SystemExit("several inputs: -o must be a directory")
    for path in args.inputs:
        t0 = time.time()
        mesh = _filter(load_obj(path), args)
        opts, views = options_from(args, mesh)
        if args.stats:
            print(json.dumps({"file": path, **mesh_stats(mesh)}, indent=1))
        if args.out and (many or os.path.isdir(args.out) or not args.out.lower().endswith(".png")):
            os.makedirs(args.out, exist_ok=True)
            out = os.path.join(args.out, os.path.splitext(os.path.basename(path))[0] + ".png")
        else:
            out = args.out or os.path.splitext(path)[0] + ".png"
        if os.path.dirname(os.path.abspath(out)):
            os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
        scene = Scene(mesh, opts)
        title, sub = describe(scene, path)
        if args.title:
            title = args.title
        img = render_sheet(scene, views, opts, None if args.no_header else title, None if args.no_header else sub)
        img.save(out)
        if not args.quiet:
            print(f"{out}: {mesh.triangle_count:,} triangles, {len(views)} view(s), {img.width}x{img.height}, "
                  f"{time.time() - t0:.1f} s")
    return 0


if __name__ == "__main__":
    sys.exit(main())
