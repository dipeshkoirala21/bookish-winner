"""Wavefront OBJ loading for the mesh previewer.

Reads positions with optional per-vertex colours (``v x y z r g b``, 0..1 or 0..255), normals (``vn``), faces of any
size (fan-triangulated; ``a``, ``a/b``, ``a//c`` and ``a/b/c`` corners, negative indices), groups (``o``/``g``) and
``usemtl`` diffuse colours from an ``mtllib`` when the file has no vertex colours. Texture coordinates (``vt u v``)
carry the Ghumante material contract (docs/W2_DETAIL_CONTRACT.md §5): u = material channel, v = baked AO.

Files written by ``core-tests/Support/ObjDump.cs`` take a vectorised fast path (uniform triangle corners), so a
200k-triangle dump loads in about a second.
"""

from __future__ import annotations

import os
from dataclasses import dataclass, field

import numpy as np


@dataclass
class Mesh:
    """A triangle mesh with per-corner normal indices and per-triangle groups.

    ``positions`` (V, 3) float64; ``colors`` (V, 3) float32 in 0..1 or None; ``normals`` (N, 3) float32 or None;
    ``tri`` (T, 3) int64 position indices; ``tri_n`` (T, 3) int64 normal indices (-1 = none) or None;
    ``tri_group`` (T,) int32 into ``groups``; ``tri_color`` (T, 3) float32 material colours or None;
    ``uvs`` (K, 2) float32 and ``tri_t`` (T, 3) int64 uv indices (-1 = none) or None.
    """

    positions: np.ndarray
    tri: np.ndarray
    colors: np.ndarray | None = None
    normals: np.ndarray | None = None
    tri_n: np.ndarray | None = None
    tri_group: np.ndarray | None = None
    groups: list = field(default_factory=lambda: ["default"])
    tri_color: np.ndarray | None = None
    comments: list = field(default_factory=list)
    uvs: np.ndarray | None = None
    tri_t: np.ndarray | None = None

    @property
    def triangle_count(self) -> int:
        return int(self.tri.shape[0])

    @property
    def vertex_count(self) -> int:
        return int(self.positions.shape[0])

    def subset(self, keep: np.ndarray) -> "Mesh":
        """The mesh restricted to the triangles where ``keep`` is True (vertex arrays are shared)."""
        return Mesh(
            positions=self.positions,
            tri=self.tri[keep],
            colors=self.colors,
            normals=self.normals,
            tri_n=None if self.tri_n is None else self.tri_n[keep],
            tri_group=None if self.tri_group is None else self.tri_group[keep],
            groups=self.groups,
            tri_color=None if self.tri_color is None else self.tri_color[keep],
            comments=self.comments,
            uvs=self.uvs,
            tri_t=None if self.tri_t is None else self.tri_t[keep],
        )


def _parse_floats(lines: list, prefix_len: int) -> np.ndarray:
    """Rows of floats from ``lines`` (prefix stripped); rows may differ in length (padded with NaN)."""
    if not lines:
        return np.zeros((0, 3))
    counts = [len(s.split()) - 1 for s in lines]
    width = max(counts)
    if min(counts) == width:
        flat = np.array(" ".join(s[prefix_len:] for s in lines).split(), dtype=np.float64)
        return flat.reshape(len(lines), width)
    out = np.full((len(lines), width), np.nan)
    for i, s in enumerate(lines):
        vals = s.split()[1:]
        out[i, : len(vals)] = [float(x) for x in vals]
    return out


def _read_mtl(path: str) -> dict:
    """``newmtl`` name -> Kd colour (0..1)."""
    colours = {}
    name = None
    try:
        with open(path, "r", encoding="utf-8", errors="replace") as f:
            for line in f:
                parts = line.split()
                if not parts:
                    continue
                if parts[0] == "newmtl" and len(parts) > 1:
                    name = " ".join(parts[1:])
                elif parts[0] == "Kd" and name is not None and len(parts) >= 4:
                    colours[name] = [float(parts[1]), float(parts[2]), float(parts[3])]
    except OSError:
        pass
    return colours


def load_obj(path: str) -> Mesh:
    """Load ``path``; raises ValueError when it holds no triangles."""
    with open(path, "r", encoding="utf-8", errors="replace") as f:
        text = f.read()

    v_lines, vn_lines, vt_lines, f_lines = [], [], [], []
    f_group, f_vcount, f_ncount, f_mat = [], [], [], []
    groups = ["default"]
    group_index = {"default": 0}
    cur_group = 0
    materials = {}
    mat_names = [None]
    mat_index = {None: 0}
    cur_mat = 0
    comments = []
    any_negative = False

    for line in text.splitlines():
        if not line:
            continue
        c = line[0]
        if c == "v":
            c1 = line[1:2]
            if c1 == " " or c1 == "\t":
                v_lines.append(line)
            elif c1 == "n":
                vn_lines.append(line)
            elif c1 == "t":
                vt_lines.append(line)
        elif c == "f":
            if line[1:2] not in (" ", "\t"):
                continue
            f_lines.append(line)
            f_group.append(cur_group)
            f_mat.append(cur_mat)
            if "-" in line:
                any_negative = True
                f_vcount.append(len(v_lines))
                f_ncount.append((len(vn_lines), len(vt_lines)))
        elif c == "o" or c == "g":
            name = line[1:].strip() or "default"
            if name not in group_index:
                group_index[name] = len(groups)
                groups.append(name)
            cur_group = group_index[name]
        elif c == "#":
            if len(comments) < 40:
                comments.append(line[1:].strip())
        elif line.startswith("usemtl"):
            name = line[6:].strip()
            if name not in mat_index:
                mat_index[name] = len(mat_names)
                mat_names.append(name)
            cur_mat = mat_index[name]
        elif line.startswith("mtllib"):
            mtl = os.path.join(os.path.dirname(os.path.abspath(path)), line[6:].strip())
            materials.update(_read_mtl(mtl))

    if not f_lines:
        raise ValueError(f"{path}: no faces")

    vrows = _parse_floats(v_lines, 2)
    if vrows.shape[1] < 3:
        raise ValueError(f"{path}: vertex rows need x y z")
    positions = vrows[:, :3].astype(np.float64)
    colors = None
    if vrows.shape[1] >= 6 and not np.isnan(vrows[:, 3:6]).all():
        colors = np.nan_to_num(vrows[:, 3:6], nan=0.8).astype(np.float32)
        if colors.max() > 1.0 + 1e-6:
            colors = colors / 255.0
        colors = np.clip(colors, 0.0, 1.0)
    normals = None
    if vn_lines:
        normals = _parse_floats(vn_lines, 3)[:, :3].astype(np.float32)
        lens = np.linalg.norm(normals, axis=1, keepdims=True)
        normals = normals / np.maximum(lens, 1e-12)

    uvs = None
    if vt_lines:
        uvs = np.nan_to_num(_parse_floats(vt_lines, 3)[:, :2], nan=0.0).astype(np.float32)
        if uvs.shape[1] < 2:
            uvs = np.concatenate([uvs, np.ones((uvs.shape[0], 1), np.float32)], axis=1)

    tri, tri_n, tri_t, tri_face = _parse_faces(f_lines, any_negative, f_vcount, f_ncount)
    nv = positions.shape[0]
    bad = (tri < 0) | (tri >= nv)
    if bad.any():
        keep = ~bad.any(axis=1)
        tri, tri_face = tri[keep], tri_face[keep]
        if tri_n is not None:
            tri_n = tri_n[keep]
        if tri_t is not None:
            tri_t = tri_t[keep]
    if tri_n is not None:
        nn = 0 if normals is None else normals.shape[0]
        tri_n = np.where((tri_n >= 0) & (tri_n < nn), tri_n, -1)
        if (tri_n < 0).all():
            tri_n = None
    if tri_t is not None:
        nt = 0 if uvs is None else uvs.shape[0]
        tri_t = np.where((tri_t >= 0) & (tri_t < nt), tri_t, -1)
        if (tri_t < 0).all():
            tri_t = None
    if tri_t is None:
        uvs = None
    if tri.shape[0] == 0:
        raise ValueError(f"{path}: no valid triangles")

    tri_group = np.asarray(f_group, dtype=np.int32)[tri_face]
    tri_color = None
    if colors is None and materials:
        lut = np.array([materials.get(n, [0.8, 0.8, 0.8]) for n in mat_names], dtype=np.float32)
        tri_color = lut[np.asarray(f_mat, dtype=np.int32)[tri_face]]

    return Mesh(positions=positions, tri=tri, colors=colors, normals=normals, tri_n=tri_n, tri_group=tri_group,
                groups=groups, tri_color=tri_color, comments=comments, uvs=uvs, tri_t=tri_t)


def _corner_kind(token: str) -> int:
    """Integers per corner once '//' becomes ' 0 ' and '/' a space: a=1, a/b=2, a//c and a/b/c=3."""
    return token.count("/") + 1


def _parse_faces(f_lines, any_negative, f_vcount, f_ncount):
    """(tri (T,3) 0-based position indices, tri_n (T,3) or None, tri_t (T,3) or None, tri_face (T,) source face
    index)."""
    split = [s.split() for s in f_lines]
    ncorner = np.fromiter((len(p) - 1 for p in split), dtype=np.int64, count=len(split))
    first = split[0][1]
    kind = _corner_kind(first)
    has_normals = kind == 3

    if not any_negative and (ncorner == 3).all():
        joined = " ".join(" ".join(p[1:]) for p in split)
        if kind > 1:
            joined = joined.replace("//", " 0 ").replace("/", " ")
        flat = np.array(joined.split(), dtype=np.int64)
        if flat.size == len(split) * 3 * kind:
            corners = flat.reshape(len(split), 3, kind)
            tri = corners[:, :, 0] - 1
            tri_n = corners[:, :, 2] - 1 if has_normals else None
            tri_t = corners[:, :, 1] - 1 if kind >= 2 else None  # 'a//c' wrote 0 here: -1 = none
            return tri, tri_n, tri_t, np.arange(len(split))

    # General path: any polygon size, mixed corner formats, negative (relative) indices.
    neg_v = iter(f_vcount)
    neg_n = iter(f_ncount)
    tris, tris_n, tris_t, faces = [], [], [], []
    for fi, parts in enumerate(split):
        vcount = ncount = tcount = 0
        if any_negative and "-" in f_lines[fi]:
            vcount, (ncount, tcount) = next(neg_v), next(neg_n)
        vi, ni, ti = [], [], []
        for tok in parts[1:]:
            fields = tok.split("/")
            a = int(fields[0])
            vi.append(a - 1 if a > 0 else vcount + a)
            if len(fields) >= 2 and fields[1]:
                t = int(fields[1])
                ti.append(t - 1 if t > 0 else tcount + t)
            else:
                ti.append(-1)
            if len(fields) >= 3 and fields[2]:
                n = int(fields[2])
                ni.append(n - 1 if n > 0 else ncount + n)
            else:
                ni.append(-1)
        for k in range(1, len(vi) - 1):
            tris.append((vi[0], vi[k], vi[k + 1]))
            tris_n.append((ni[0], ni[k], ni[k + 1]))
            tris_t.append((ti[0], ti[k], ti[k + 1]))
            faces.append(fi)
    tri = np.asarray(tris, dtype=np.int64).reshape(-1, 3)
    tri_n = np.asarray(tris_n, dtype=np.int64).reshape(-1, 3)
    tri_t = np.asarray(tris_t, dtype=np.int64).reshape(-1, 3)
    if (tri_n < 0).all():
        tri_n = None
    if (tri_t < 0).all():
        tri_t = None
    return tri, tri_n, tri_t, np.asarray(faces, dtype=np.int64)


def connected_components(mesh: Mesh, weld: float | None = None) -> np.ndarray:
    """Per-triangle component labels (0..K-1) with vertices welded by position (``weld`` metres; default 1e-5 of
    the bounding-box diagonal), so hard-edged shells with split normals still count as one piece."""
    pos = mesh.positions
    diag = float(np.linalg.norm(pos.max(axis=0) - pos.min(axis=0))) or 1.0
    q = weld if weld is not None else diag * 1e-5
    keys = np.round((pos - pos.min(axis=0)) / q).astype(np.int64)
    _, welded = np.unique(keys, axis=0, return_inverse=True)
    welded = welded.reshape(-1)
    t = welded[mesh.tri]
    labels = np.arange(int(welded.max()) + 1)
    for _ in range(500):
        lm = np.minimum(np.minimum(labels[t[:, 0]], labels[t[:, 1]]), labels[t[:, 2]])
        before = labels.copy()
        for k in range(3):
            np.minimum.at(labels, t[:, k], lm)
        while True:  # pointer jumping
            nxt = labels[labels]
            if np.array_equal(nxt, labels):
                break
            labels = nxt
        if np.array_equal(before, labels):
            break
    tri_label = labels[t[:, 0]]
    _, compact = np.unique(tri_label, return_inverse=True)
    return compact.reshape(-1).astype(np.int32)


def merge_meshes(named: list) -> Mesh:
    """One mesh from several ``(name, Mesh)`` pairs (``--combine``): groups become ``name/group`` (or ``name``),
    missing colours read grey, missing normals face normals, missing UVs Plain and open; comments are kept."""
    pos, tri, cols, nrm, tri_n, uvs, tri_t, tgroup, tcol = [], [], [], [], [], [], [], [], []
    groups, comments = [], []
    any_col = any(m.colors is not None for _, m in named)
    any_n = any(m.normals is not None and m.tri_n is not None for _, m in named)
    any_t = any(m.uvs is not None and m.tri_t is not None for _, m in named)
    any_tc = any(m.tri_color is not None for _, m in named)
    vo = no = to = 0
    for name, m in named:
        pos.append(m.positions)
        tri.append(m.tri + vo)
        if any_col:
            cols.append(m.colors if m.colors is not None else np.full((m.vertex_count, 3), 0.78, np.float32))
        if any_tc:
            tcol.append(m.tri_color if m.tri_color is not None else
                        (m.colors[m.tri].mean(axis=1) if m.colors is not None else np.full((m.triangle_count, 3), 0.78, np.float32)))
        if any_n:
            if m.normals is not None and m.tri_n is not None:
                nrm.append(m.normals)
                tri_n.append(np.where(m.tri_n >= 0, m.tri_n + no, -1))
                no += m.normals.shape[0]
            else:
                tri_n.append(np.full(m.tri.shape, -1, np.int64))
        if any_t:
            if m.uvs is not None and m.tri_t is not None:
                uvs.append(m.uvs)
                tri_t.append(np.where(m.tri_t >= 0, m.tri_t + to, -1))
                to += m.uvs.shape[0]
            else:
                tri_t.append(np.full(m.tri.shape, -1, np.int64))
        local = m.tri_group if m.tri_group is not None else np.zeros(m.triangle_count, np.int32)
        used = np.unique(local)
        remap = np.zeros(max(len(m.groups), int(local.max(initial=0)) + 1), np.int32)
        real = [g for g in used if m.groups[g] != "default"]
        for g in used:
            label = name if (m.groups[g] == "default" or len(real) <= 1) else f"{name}/{m.groups[g]}"
            if label not in groups:
                groups.append(label)
            remap[g] = groups.index(label)
        tgroup.append(remap[local])
        comments.extend(m.comments)
        vo += m.vertex_count
    return Mesh(
        positions=np.concatenate(pos), tri=np.concatenate(tri),
        colors=np.concatenate(cols) if any_col else None,
        normals=np.concatenate(nrm) if any_n and nrm else None,
        tri_n=np.concatenate(tri_n) if any_n and nrm else None,
        tri_group=np.concatenate(tgroup).astype(np.int32), groups=groups,
        tri_color=np.concatenate(tcol) if any_tc and not any_col else None,
        comments=comments,
        uvs=np.concatenate(uvs) if any_t and uvs else None,
        tri_t=np.concatenate(tri_t) if any_t and uvs else None,
    )


def write_obj(path: str, positions, tri, colors=None, normals=None, groups=None, comments=None, uvs=None) -> None:
    """Write an OBJ in the format :func:`load_obj` reads fastest (and ObjDump.cs writes).

    ``positions`` (V, 3); ``tri`` (T, 3) 0-based, counter-clockwise front faces; ``colors`` (V, 3) in 0..1;
    ``normals`` (V, 3) per vertex (faces then use ``a//a``); ``groups`` a list of (name, first_triangle, count) in
    triangle order; ``comments`` lines written first (``meshpreview: key=value`` lines set render defaults); ``uvs``
    (V, 2) per vertex (u = material channel, v = AO; faces then use ``a/a/a``)."""
    positions = np.asarray(positions, dtype=np.float64)
    tri = np.asarray(tri, dtype=np.int64)
    lines = [f"# {c}" for c in (comments or [])]
    if colors is not None:
        colors = np.clip(np.asarray(colors, dtype=np.float64), 0.0, 1.0)
        for p, c in zip(positions, colors):
            lines.append(f"v {p[0]:.5f} {p[1]:.5f} {p[2]:.5f} {c[0]:.4f} {c[1]:.4f} {c[2]:.4f}")
    else:
        lines.extend(f"v {p[0]:.5f} {p[1]:.5f} {p[2]:.5f}" for p in positions)
    if normals is not None:
        lines.extend(f"vn {n[0]:.4f} {n[1]:.4f} {n[2]:.4f}" for n in np.asarray(normals, dtype=np.float64))
    if uvs is not None:
        lines.extend(f"vt {t[0]:.4f} {t[1]:.4f}" for t in np.asarray(uvs, dtype=np.float64))
    spans = groups or [("mesh", 0, tri.shape[0])]
    for name, first, count in spans:
        lines.append(f"o {name}")
        for a, b, c in tri[first:first + count] + 1:
            if uvs is not None and normals is not None:
                lines.append(f"f {a}/{a}/{a} {b}/{b}/{b} {c}/{c}/{c}")
            elif uvs is not None:
                lines.append(f"f {a}/{a} {b}/{b} {c}/{c}")
            elif normals is not None:
                lines.append(f"f {a}//{a} {b}//{b} {c}//{c}")
            else:
                lines.append(f"f {a} {b} {c}")
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
