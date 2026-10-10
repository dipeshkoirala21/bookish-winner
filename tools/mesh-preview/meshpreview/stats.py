"""Mesh statistics for self-checks: counts, size, groups, how much of the surface is smooth-shaded, how many
distinct facet directions it has (a box world has a handful), inverted normals and degenerate triangles."""

from __future__ import annotations

import numpy as np

from .objfile import Mesh, connected_components


def mesh_stats(mesh: Mesh, components: bool = True) -> dict:
    pos = mesh.positions
    P = pos[mesh.tri]
    fn = np.cross(P[:, 1] - P[:, 0], P[:, 2] - P[:, 0])
    ln = np.linalg.norm(fn, axis=1)
    area = 0.5 * ln
    total = float(area.sum()) or 1.0
    unit = fn / np.maximum(ln, 1e-20)[:, None]
    out = {
        "triangles": mesh.triangle_count,
        "vertices": mesh.vertex_count,
        "size_m": [round(float(v), 3) for v in (pos.max(axis=0) - pos.min(axis=0))],
        "min": [round(float(v), 3) for v in pos.min(axis=0)],
        "max": [round(float(v), 3) for v in pos.max(axis=0)],
        "surface_m2": round(float(area.sum()), 2),
        "degenerate_triangles": int((area < 1e-8).sum()),
    }
    groups = {}
    if mesh.tri_group is not None:
        counts = np.bincount(mesh.tri_group, minlength=len(mesh.groups))
        groups = {mesh.groups[i]: int(c) for i, c in enumerate(counts) if c > 0}
    out["groups"] = groups
    if mesh.normals is not None and mesh.tri_n is not None:
        N = mesh.normals[np.maximum(mesh.tri_n, 0)]
        d = np.einsum("tkc,tc->tk", N, unit)
        smooth = (d.min(axis=1) < np.cos(np.radians(8.0))) & (mesh.tri_n >= 0).all(axis=1)
        out["smooth_shaded_area_pct"] = round(100.0 * float(area[smooth].sum()) / total, 1)
        inverted = d.mean(axis=1) < 0
        out["inverted_normals_area_pct"] = round(100.0 * float(area[inverted].sum()) / total, 2)
    if mesh.uvs is not None and mesh.tri_t is not None:
        from .materials import name
        u = mesh.uvs[np.maximum(mesh.tri_t, 0)]
        u[mesh.tri_t < 0] = (0.0, 1.0)
        ch = np.clip(np.rint(np.median(u[:, :, 0], axis=1)), 0, 255).astype(np.int64)
        tri_area = np.bincount(ch, weights=area)
        out["channels_area_pct"] = {name(int(c)): round(100.0 * float(a) / total, 1) for c, a in enumerate(tri_area) if a > 0}
        ao = u[:, :, 1]
        out["ao"] = {"min": round(float(ao.min()), 3), "mean": round(float(ao.mean()), 3),
                     "occluded_area_pct": round(100.0 * float(area[ao.mean(axis=1) < 0.7].sum()) / total, 1)}
    else:
        out["channels_area_pct"] = None
    theta = np.degrees(np.arccos(np.clip(unit[:, 1], -1, 1)))
    phi = np.degrees(np.arctan2(unit[:, 0], -unit[:, 2])) % 360.0
    bins = (np.minimum(theta // 5, 35) * 72 + np.minimum(phi // 5, 71)).astype(np.int64)
    hist = np.bincount(bins, weights=area, minlength=36 * 72)
    out["facet_directions_5deg"] = int((hist >= 0.001 * total).sum())
    if components and mesh.triangle_count <= 600_000:
        out["components"] = int(connected_components(mesh).max()) + 1
    return out
