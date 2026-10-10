"""Ghumante mesh previewer: a numpy + Pillow software renderer for OBJ meshes (tools/mesh-preview/README.md)."""

from .objfile import Mesh, connected_components, load_obj, write_obj
from .renderer import LAYOUTS, PRESETS, Options, Scene, View, render_view
from .compose import describe, render_sheet
from .stats import mesh_stats

__all__ = [
    "Mesh", "connected_components", "load_obj", "write_obj", "LAYOUTS", "PRESETS", "Options", "Scene", "View",
    "render_view", "describe", "render_sheet", "mesh_stats", "render_file",
]


def render_file(path: str, out: str, views=None, opts: Options | None = None, title: str | None = None):
    """Load ``path``, render ``views`` (names, View objects or an int layout; default 3) and save the sheet to
    ``out``; returns the PIL image."""
    from .cli import resolve_views

    opts = opts or Options()
    mesh = load_obj(path)
    scene = Scene(mesh, opts)
    vs = resolve_views(views if views is not None else 3)
    t, sub = describe(scene, path)
    img = render_sheet(scene, vs, opts, title or t, sub)
    img.save(out)
    return img
