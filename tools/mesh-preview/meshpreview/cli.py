"""Command line: ``python3 tools/mesh-preview/render.py in.obj -o out.png [options]`` (see README.md).

Several inputs render one sheet each, or one combined scene (``--combine``), or one contact sheet (``--sheet``)."""

from __future__ import annotations

import argparse
import json
import os
import sys
import time

import numpy as np

from .compose import describe, render_contact, render_sheet
from .objfile import load_obj, merge_meshes
from .renderer import LAYOUTS, PRESETS, Options, Scene, View
from .stats import mesh_stats


def resolve_views(spec):
    """Views from an int layout (1-6), a comma list of preset names and ``az:el`` pairs, or View objects."""
    if isinstance(spec, int):
        return [PRESETS[n] for n in LAYOUTS[max(1, min(6, spec))]]
    if isinstance(spec, (list, tuple)) and spec and isinstance(spec[0], View):
        return list(spec)
    items = spec if isinstance(spec, (list, tuple)) else str(spec).split(",")
    if len(items) == 1 and str(items[0]).strip() in ("1", "2", "3", "4", "5", "6"):
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


def _point(vals, what):
    """x y z from ['x', 'y', 'z'] or ['x,y,z']."""
    if vals is None:
        return None
    if len(vals) == 1:
        return _floats(vals[0], 3, what)
    if len(vals) == 3:
        try:
            return [float(v) for v in vals]
        except ValueError:
            pass
    raise SystemExit(f"{what}: expected x y z or x,y,z")


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
    p.add_argument("inputs", nargs="*", help="OBJ file(s)")
    p.add_argument("--swatch", metavar="OUT.obj",
                   help="write the material swatch board (one corner per channel with AO) to OUT.obj and render it")
    p.add_argument("-o", "--out", help="output PNG (one input, --combine, --sheet) or directory (several inputs); "
                                        "default: next to each input")
    p.add_argument("--sheet", action="store_true",
                   help="contact sheet: one labelled cell per input (one view; default 3/4) or one row per input "
                        "(several --views); default output contact.png next to the first input")
    p.add_argument("--combine", action="store_true",
                   help="render all inputs as one scene (one group per file), e.g. the terrain, roads and buildings "
                        "of a dumped tile")
    p.add_argument("--eye", nargs="+", metavar="X",
                   help="free camera position 'x y z' or 'x,y,z' in OBJ coordinates (street-level views); OBJ files "
                        "can carry one as a '# meshpreview: eye=x,y,z look=x,y,z' directive")
    p.add_argument("--look", nargs="+", metavar="X", help="point the --eye camera looks at (default: the mesh centre)")
    p.add_argument("--no-eye", action="store_true", help="ignore eye/look directives in the files (orbit views)")
    p.add_argument("--unity", action="store_true",
                   help="--eye, --look, --target and --clip are Unity coordinates (z north); ObjDump files store -z")
    p.add_argument("--radius", type=float,
                   help="with an eye point: only triangles whose centroid lies within this many metres (plan)")
    p.add_argument("--fog", type=float, help="distance fog: metres at which the fog reaches 63 %% (default off)")
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
    p.add_argument("--shading", choices=["toon", "lambert", "unlit", "normals", "channels", "ao"],
                   help="default toon (as ToonLit); channels: false colour per material channel with a legend; "
                        "ao: the baked AO (UV0 v) in grey")
    p.add_argument("--no-materials", action="store_true",
                   help="ignore UV0: no channel patterns, no AO (the plain vertex-colour look)")
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


def _unity(pt, args):
    return None if pt is None else ([pt[0], pt[1], -pt[2]] if args.unity else list(pt))


def options_from(args, mesh) -> tuple:
    d = _directives(mesh)
    o = Options()
    o.materials = not args.no_materials
    o.fog = args.fog if args.fog is not None else float(d.get("fog", 0.0))
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
    o.target = tuple(_unity(_floats(args.target, 3, "--target"), args)) if args.target else None
    eye = _unity(_point(args.eye, "--eye"), args)
    look = _unity(_point(args.look, "--look"), args)
    if eye is None and not args.no_eye and "eye" in d:
        eye = _floats(d["eye"], 3, "eye directive")
        if look is None and "look" in d:
            look = _floats(d["look"], 3, "look directive")
    if eye is not None:
        ev = View("eye", 0.0, 0.0, eye=tuple(eye), look=None if look is None else tuple(look))
        views = [ev] + (resolve_views(args.views) if args.views is not None else [])
        if args.az is not None or args.el is not None:
            views.append(View("custom", args.az if args.az is not None else 35.0, args.el if args.el is not None else 25.0))
        return o, views
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
        if args.unity:
            b[2], b[5] = -b[5], -b[2]
        c = mesh.positions[mesh.tri].mean(axis=1)
        keep &= ((c >= b[:3]) & (c <= b[3:])).all(axis=1)
    if keep.all():
        return mesh
    if not keep.any():
        raise SystemExit("--group/--clip left no triangles")
    return mesh.subset(keep)


def _radius(mesh, views, r):
    """Triangles within ``r`` metres (plan) of the first eye point."""
    eye = next((v.eye for v in views if v.eye is not None), None)
    if r is None or eye is None:
        return mesh
    c = mesh.positions[mesh.tri].mean(axis=1)
    keep = (c[:, 0] - eye[0]) ** 2 + (c[:, 2] - eye[2]) ** 2 <= r * r
    if not keep.any():
        raise SystemExit("--radius left no triangles")
    return mesh if keep.all() else mesh.subset(keep)


def _render_one(mesh, path, args, out, label=None):
    t0 = time.time()
    mesh = _filter(mesh, args)
    opts, views = options_from(args, mesh)
    mesh = _radius(mesh, views, args.radius)
    if args.stats:
        print(json.dumps({"file": path, **mesh_stats(mesh)}, indent=1))
    if os.path.dirname(os.path.abspath(out)):
        os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    scene = Scene(mesh, opts)
    title, sub = describe(scene, path)
    if label:
        title = label + title[len(os.path.basename(path)):]
    if args.title:
        title = args.title
    img = render_sheet(scene, views, opts, None if args.no_header else title, None if args.no_header else sub)
    img.save(out)
    if not args.quiet:
        print(f"{out}: {mesh.triangle_count:,} triangles, {len(views)} view(s), {img.width}x{img.height}, "
              f"{time.time() - t0:.1f} s")


def main(argv=None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    if args.swatch:
        from .swatch import make_swatch
        make_swatch(args.swatch)
        args.inputs = [args.swatch] + list(args.inputs)
    if not args.inputs:
        parser.error("no input OBJ files")
    many = len(args.inputs) > 1
    if args.sheet:
        t0 = time.time()
        out = args.out or os.path.join(os.path.dirname(os.path.abspath(args.inputs[0])), "contact.png")
        if os.path.isdir(out) or not out.lower().endswith(".png"):
            os.makedirs(out, exist_ok=True)
            out = os.path.join(out, "contact.png")
        items, views, opts = [], None, None
        for path in args.inputs:
            mesh = _filter(load_obj(path), args)
            o, vs = options_from(args, mesh)
            if args.views is None and args.az is None and args.el is None and args.eye is None:
                vs = [v for v in vs if v.eye is not None][:1] or resolve_views(1)
            mesh = _radius(mesh, vs, args.radius)
            if opts is None:
                opts, views = o, vs
            items.append((os.path.basename(path), Scene(mesh, opts)))
        img = render_contact(items, views, opts, None if args.no_header else args.title)
        os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
        img.save(out)
        if not args.quiet:
            print(f"{out}: {len(items)} file(s), {img.width}x{img.height}, {time.time() - t0:.1f} s")
        return 0
    if args.combine:
        named = [(os.path.splitext(os.path.basename(p))[0], load_obj(p)) for p in args.inputs]
        mesh = merge_meshes(named)
        out = args.out or os.path.join(os.path.dirname(os.path.abspath(args.inputs[0])), "combined.png")
        if os.path.isdir(out) or not out.lower().endswith(".png"):
            os.makedirs(out, exist_ok=True)
            out = os.path.join(out, "combined.png")
        _render_one(mesh, args.inputs[0], args, out, label=" + ".join(n for n, _ in named))
        return 0
    if many and args.out and args.out.lower().endswith(".png"):
        raise SystemExit("several inputs: -o must be a directory (or use --combine / --sheet)")
    for path in args.inputs:
        if args.out and (many or os.path.isdir(args.out) or not args.out.lower().endswith(".png")):
            os.makedirs(args.out, exist_ok=True)
            out = os.path.join(args.out, os.path.splitext(os.path.basename(path))[0] + ".png")
        else:
            out = args.out or os.path.splitext(path)[0] + ".png"
        _render_one(load_obj(path), path, args, out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
