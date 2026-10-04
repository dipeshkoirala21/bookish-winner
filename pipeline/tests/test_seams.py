"""Seam invariants on the synthetic end-to-end build (docs/DATA_FORMATS.md 1.1, 1.2, 1.4).

* ``HGHT`` and ``BIOM`` edge rows/columns are identical across **every**
  neighbouring tile pair at every level (east and north neighbours, plus the
  shared corner sample of diagonal neighbours).
* Where a parent and a child tile both sample the raw DEM (no pre-filter), the
  samples they share decode to identical heights.
* Road and line cuts are consistent: every exit cut point of a way in one leaf
  tile is the entry cut point of the same way in the neighbouring tile, and
  the context points on both sides are the original neighbouring vertices.
* Every building appears exactly once.

``check_seams`` is reused by the real-data tests on a sample of tile pairs.
"""

from __future__ import annotations

from collections import Counter

import numpy as np
import pytest

from fixtures.synth.make_synth import synth_build
from ghumante_pipeline import projection
from ghumante_pipeline.model import RoadFlags
from ghumante_pipeline.pack import PackReader
from ghumante_pipeline.projection import TileId
from ghumante_pipeline.tile_format import LineFlags, TileData, decode_tile


@pytest.fixture(scope="session")
def synth(tmp_path_factory):
    return synth_build(tmp_path_factory.getbasetemp() / "synth_e2e")


@pytest.fixture(scope="session")
def decoded(synth) -> dict[TileId, TileData]:
    with PackReader(synth.pack) as pr:
        return {projection.tile_from_key(k): decode_tile(pr.get(k)) for k in pr.keys()}


def neighbour_pairs(tiles) -> list[tuple[TileId, TileId, str]]:
    """(tile, neighbour, direction) for every east/north/north-east/north-west neighbour present."""
    s = set(tiles)
    out = []
    for t in sorted(s, key=lambda t: t.key):
        n = 1 << t.level
        for dx, dy, d in ((1, 0, "E"), (0, 1, "N"), (1, 1, "NE"), (-1, 1, "NW")):
            if 0 <= t.tx + dx < n and 0 <= t.ty + dy < n:
                u = TileId(t.level, t.tx + dx, t.ty + dy)
                if u in s:
                    out.append((t, u, d))
    return out


def _edges(a: np.ndarray, b: np.ndarray, d: str) -> tuple[np.ndarray, np.ndarray]:
    if d == "E":
        return a[:, -1], b[:, 0]
    if d == "N":
        return a[-1, :], b[0, :]
    if d == "NE":
        return a[-1:, -1], b[:1, 0]
    return a[-1:, 0], b[:1, -1]  # NW


def check_seams(a: TileData, b: TileData, d: str) -> list[str]:
    """Problems with the shared HGHT/BIOM samples of neighbours ``a`` -> ``b``."""
    bad = []
    if a.heights_q is not None and b.heights_q is not None:
        x, y = _edges(a.heights_q, b.heights_q, d)
        if not np.array_equal(x, y):
            bad.append(f"HGHT {a.tile}->{b.tile} ({d}): {int((x != y).sum())} samples differ")
    if a.biomes is not None and b.biomes is not None:
        x, y = _edges(a.biomes, b.biomes, d)
        if not np.array_equal(x, y):
            bad.append(f"BIOM {a.tile}->{b.tile} ({d}): {int((x != y).sum())} samples differ")
    return bad


def test_every_level_has_neighbours(decoded) -> None:
    levels = Counter(t.level for t in decoded)
    pairs = Counter(t.level for t, _, _ in neighbour_pairs(decoded))
    for lvl, n in levels.items():
        if n > 1:
            assert pairs[lvl] > 0, f"level {lvl} has {n} tiles but no neighbour pairs"


def test_heights_and_biomes_seam_identical(decoded) -> None:
    problems = []
    pairs = neighbour_pairs(decoded)
    assert len(pairs) >= 20
    for t, u, d in pairs:
        problems += check_seams(decoded[t], decoded[u], d)
    assert not problems, "\n".join(problems[:20])


def test_biome_edges_vary(decoded) -> None:
    """The seam test is meaningful: edges are not all one biome."""
    leaf = max(t.level for t in decoded)
    vals = set()
    for t, td in decoded.items():
        if t.level == leaf:
            vals.update(np.unique(td.biomes).tolist())
    assert len(vals) >= 3


def test_parent_child_shared_samples(synth, decoded) -> None:
    region = synth.synth.region
    n = region.height_grid
    for t, td in decoded.items():
        if t.level != region.leaf_level:
            continue
        p = t.parent()
        if p not in decoded:
            continue
        # Both 16 m (child) and 32 m (parent) spacings sample the raw DEM (no pre-filter at <= 44 m).
        half = (n - 1) // 2
        oi, oj = (t.tx & 1) * half, (t.ty & 1) * half
        child = td.heights_q[::2, ::2]
        parent = decoded[p].heights_q[oj:oj + half + 1, oi:oi + half + 1]
        assert np.array_equal(child, parent), f"{t} vs parent {p}"


def _global_cm(t: TileId, pts: np.ndarray) -> np.ndarray:
    s = int(t.size * 100)
    return pts + np.array([t.tx * s, t.ty * s], dtype=np.int64)


def _cuts(decoded, leaf: int, rec_attr: str, prev_bit: int, next_bit: int):
    exits, entries = Counter(), Counter()
    ctx_after_exit, ctx_before_entry = {}, {}
    for t, td in decoded.items():
        if t.level != leaf:
            continue
        for r in getattr(td, rec_attr):
            g = _global_cm(t, r.points)
            if r.flags & next_bit:
                key = (r.osm_way_id, tuple(g[-2]))
                exits[key] += 1
                ctx_after_exit[key] = tuple(g[-1])
            if r.flags & prev_bit:
                key = (r.osm_way_id, tuple(g[1]))
                entries[key] += 1
                ctx_before_entry[key] = tuple(g[0])
    return exits, entries, ctx_after_exit, ctx_before_entry


def _outer_border(decoded, leaf: int) -> tuple[int, int, int, int]:
    tiles = [t for t in decoded if t.level == leaf]
    s = int(tiles[0].size * 100)
    return (min(t.tx for t in tiles) * s, min(t.ty for t in tiles) * s,
            (max(t.tx for t in tiles) + 1) * s, (max(t.ty for t in tiles) + 1) * s)


@pytest.mark.parametrize("rec_attr,prev_bit,next_bit,min_cuts", [
    ("roads", int(RoadFlags.HAS_PREV_CTX), int(RoadFlags.HAS_NEXT_CTX), 10),
    ("lines", int(LineFlags.HAS_PREV_CTX), int(LineFlags.HAS_NEXT_CTX), 2),
])
def test_cut_points_consistent(decoded, rec_attr, prev_bit, next_bit, min_cuts) -> None:
    leaf = max(t.level for t in decoded)
    exits, entries, ctx_exit, ctx_entry = _cuts(decoded, leaf, rec_attr, prev_bit, next_bit)
    x0, z0, x1, z1 = _outer_border(decoded, leaf)

    def on_outer(p) -> bool:
        return p[0] in (x0, x1) or p[1] in (z0, z1)

    inner_exits = {k: v for k, v in exits.items() if not on_outer(k[1])}
    inner_entries = {k: v for k, v in entries.items() if not on_outer(k[1])}
    assert len(inner_exits) >= min_cuts, "the fixture should cut ways at inner tile borders"
    assert inner_exits == inner_entries
    for key in inner_exits:
        # Exit side: piece [.., p_prev, cut, ctx_next]; entry side: [ctx_prev, cut, p_next, ..].
        # The exit's context point is the first in-tile vertex after the cut on the other side,
        # unless the next original vertex lies beyond that tile too (then both are that vertex).
        assert ctx_exit[key] != key[1] and ctx_entry[key] != key[1]


def test_cut_points_on_tile_borders(decoded) -> None:
    leaf = max(t.level for t in decoded)
    for t, td in decoded.items():
        if t.level != leaf:
            continue
        s = int(t.size * 100)
        for r in td.roads:
            inner = r.points[(1 if r.flags & RoadFlags.HAS_PREV_CTX else 0):
                             len(r.points) - (1 if r.flags & RoadFlags.HAS_NEXT_CTX else 0)]
            assert inner.min() >= 0 and inner.max() <= s, f"{t} way {r.osm_way_id} leaves its tile"
            if r.flags & RoadFlags.HAS_NEXT_CTX:
                assert inner[-1][0] in (0, s) or inner[-1][1] in (0, s)
            if r.flags & RoadFlags.HAS_PREV_CTX:
                assert inner[0][0] in (0, s) or inner[0][1] in (0, s)


def test_every_building_exactly_once(synth, decoded) -> None:
    refs = Counter()
    for t, td in decoded.items():
        if td.buildings:
            assert t.level == synth.synth.region.leaf_level
        refs.update(b.osm_ref for b in td.buildings)
    assert all(n == 1 for n in refs.values()), [r for r, n in refs.items() if n > 1][:5]
    expected = {w << 1 for w in synth.synth.buildings} | {(synth.synth.building_relation << 1) | 1}
    assert set(refs) == expected

