"""Smoke and format tests of the mesh previewer: python3 -m pytest -q tools/mesh-preview/tests"""

import os
import sys

import numpy as np
import pytest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))

from meshpreview import Options, Scene, View, load_obj, mesh_stats, render_view  # noqa: E402
from meshpreview.cli import main, resolve_views  # noqa: E402
from meshpreview.objfile import merge_meshes, write_obj  # noqa: E402

BG = np.array([0.70, 0.79, 0.89])


def cube(path, channel=2, ao=0.5, colour=(0.8, 0.3, 0.2), with_uv=True):
    """A unit cube on the ground, counter-clockwise faces, per-face normals."""
    pos, nrm, tri, uv = [], [], [], []
    for axis in range(3):
        for sign in (-1.0, 1.0):
            n = np.zeros(3)
            n[axis] = sign
            u = np.roll(np.array([0.0, 1.0, 0.0]), axis)
            v = np.cross(n, u)
            c = np.array([0.0, 0.5, 0.0]) + 0.5 * n
            b = len(pos)
            for du, dv in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                pos.append(c + 0.5 * du * u + 0.5 * dv * v)
                nrm.append(n)
                uv.append((channel, ao if dv < 0 else 1.0))
            t = [(b, b + 1, b + 2), (b, b + 2, b + 3)]
            if np.dot(np.cross(u, v), n) < 0:
                t = [(a, c2, b2) for a, b2, c2 in t]
            tri.extend(t)
    write_obj(path, np.array(pos), np.array(tri), np.tile(colour, (len(pos), 1)), np.array(nrm),
              uvs=np.array(uv) if with_uv else None)
    return path


def test_obj_round_trip_reads_uv_channel_and_ao(tmp_path):
    m = load_obj(cube(str(tmp_path / "c.obj"), channel=9, ao=0.25))
    assert m.triangle_count == 12
    assert m.uvs is not None and m.tri_t is not None
    s = Scene(m, Options(shadows=False))
    assert set(np.unique(s.CH)) == {9}
    assert s.AO.min() == pytest.approx(0.25)
    st = mesh_stats(m)
    assert st["channels_area_pct"] == {"Stone": 100.0}
    assert st["inverted_normals_area_pct"] == 0.0


def test_render_draws_the_mesh_and_ao_darkens(tmp_path):
    o = Options(size=96, shadows=False, grid=False, outline=False)
    lit = Scene(load_obj(cube(str(tmp_path / "a.obj"), channel=0, ao=1.0)), o)
    dark = Scene(load_obj(cube(str(tmp_path / "b.obj"), channel=0, ao=0.0)), o)
    a = np.asarray(render_view(lit, View("34", 35, 28), o)[0], dtype=float) / 255
    b = np.asarray(render_view(dark, View("34", 35, 28), o)[0], dtype=float) / 255
    covered = np.abs(a - BG).sum(axis=2) > 0.1
    assert covered.mean() > 0.2
    assert b[covered].mean() < a[covered].mean() - 0.03


def test_eye_camera_sees_the_cube_from_street_level(tmp_path):
    o = Options(size=80, shadows=False, grid=False)
    s = Scene(load_obj(cube(str(tmp_path / "c.obj"))), o)
    img, cam = render_view(s, View("eye", 0, 0, eye=(0.0, 0.5, -4.0), look=(0.0, 0.5, 0.0)), o)
    a = np.asarray(img, dtype=float) / 255
    assert (np.abs(a - BG).sum(axis=2) > 0.1)[30:50, 30:50].all()
    assert cam.az == pytest.approx(0.0) and cam.el == pytest.approx(0.0, abs=1e-6)


def test_merge_keeps_groups_and_fills_missing_uvs(tmp_path):
    a = load_obj(cube(str(tmp_path / "a.obj")))
    b = load_obj(cube(str(tmp_path / "b.obj"), with_uv=False))
    m = merge_meshes([("roads", a), ("buildings", b)])
    assert m.groups == ["roads", "buildings"]
    assert m.triangle_count == 24
    assert (m.tri_t[12:] < 0).all() and (m.tri_t[:12] >= 0).all()


def test_views_resolve():
    assert [v.name for v in resolve_views(3)] == ["front", "3/4", "top"]
    assert [v.name for v in resolve_views("34")] == ["3/4"]
    assert [v.name for v in resolve_views("front,10:20")] == ["front", "custom"]


def test_cli_sheet_combine_and_channels(tmp_path):
    a = cube(str(tmp_path / "a.obj"), channel=2)
    b = cube(str(tmp_path / "b.obj"), channel=6)
    assert main([a, b, "--sheet", "-o", str(tmp_path / "sheet.png"), "--size", "64", "-q"]) == 0
    assert main([a, b, "--combine", "-o", str(tmp_path / "comb.png"), "--size", "64", "-q", "--shading", "channels",
                 "--eye", "3,2,-3", "--look", "0,0.5,0", "--fog", "50"]) == 0
    assert main([a, "-o", str(tmp_path / "one.png"), "--size", "64", "-q", "--views", "2", "--unity", "--target",
                 "0,0.5,0"]) == 0
    for f in ("sheet.png", "comb.png", "one.png"):
        assert os.path.getsize(tmp_path / f) > 1000
