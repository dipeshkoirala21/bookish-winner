"""Checks on the real Kathmandu Valley build (``python build.py --region kathmandu_valley``).

These read the outputs in ``build/regions/kathmandu_valley/`` and are skipped
when that build has not been run (or the source data is missing).
"""

from __future__ import annotations

import math

import numpy as np
import pytest

from ghumante_pipeline import projection, routing, search_index
from ghumante_pipeline.config import BUILD_DIR
from ghumante_pipeline.model import Travel
from ghumante_pipeline.pack import PackReader, read_manifest
from ghumante_pipeline.tile_format import decode_tile
from test_seams import check_seams, neighbour_pairs

pytestmark = pytest.mark.realdata

REG = BUILD_DIR / "regions" / "kathmandu_valley"
PACK = REG / "kathmandu_valley.ghpk"
INDEX = REG / "kathmandu_valley.search.ghsi"
GRAPH = REG / "kathmandu_valley.route.ghrg"
MANIFEST = REG / "kathmandu_valley.manifest.json"

BOUDHA = (85.3620, 27.7215)
THAMEL = (85.3127, 27.7167)
PACK_BUDGET_BYTES = 80 * 1000 * 1000


def _need(*paths):
    missing = [p for p in paths if not p.exists()]
    if missing:
        pytest.skip(f"kathmandu_valley build outputs missing ({missing[0].name}); run build.py first")


def _dist_m(lon1, lat1, lon2, lat2) -> float:
    x1, z1 = projection.lonlat_to_game(lon1, lat1)
    x2, z2 = projection.lonlat_to_game(lon2, lat2)
    return math.hypot(float(x1 - x2), float(z1 - z2))


@pytest.fixture(scope="module")
def index():
    _need(INDEX)
    return search_index.read_index(INDEX)


@pytest.fixture(scope="module")
def graph():
    _need(GRAPH)
    return routing.read_graph(GRAPH)


def test_search_boudha(index) -> None:
    res = index.search("Boudha")
    assert res, "no results"
    score, e = res[0]
    name = " ".join([e.name.default, e.name.en, e.name.ne]).lower()
    assert "boudha" in name or "baudha" in name or "बौद्ध" in name
    assert e.kind == 101  # PoiKind.STUPA
    assert _dist_m(e.lon, e.lat, *BOUDHA) < 300.0


def test_search_kathmandu_devanagari(index) -> None:
    res = index.search("काठमाडौं")
    assert res
    e = res[0][1]
    assert "kathmandu" in (e.name.en or e.name.default).lower()


def test_motorbike_route_thamel_boudha(graph) -> None:
    src = routing.nearest_node(graph, *map(float, projection.lonlat_to_game(*THAMEL)), Travel.MOTORBIKE,
                               max_dist_m=300)
    dst = routing.nearest_node(graph, *map(float, projection.lonlat_to_game(*BOUDHA)), Travel.MOTORBIKE,
                               incoming=True, max_dist_m=300)
    assert src is not None and dst is not None
    r = routing.route(graph, src, dst, Travel.MOTORBIKE)
    assert r is not None
    assert 4_500.0 <= r.length_m <= 9_000.0, r.length_m
    assert r.time_s < 20 * 60, r.time_s


def test_seams_sample() -> None:
    _need(PACK)
    with PackReader(PACK) as pr:
        tiles = [projection.tile_from_key(k) for k in pr.keys()]
        pairs = neighbour_pairs(tiles)
        rng = np.random.default_rng(20261004)
        pick = rng.choice(len(pairs), size=min(200, len(pairs)), replace=False)
        problems = []
        cache = {}

        def get(t):
            if t not in cache:
                cache[t] = decode_tile(pr.get(t.key))
            return cache[t]

        for i in sorted(pick.tolist()):
            a, b, d = pairs[i]
            problems += check_seams(get(a), get(b), d)
    assert not problems, "\n".join(problems[:20])


def test_pack_size_and_manifest() -> None:
    _need(PACK, MANIFEST)
    size = PACK.stat().st_size
    print(f"kathmandu_valley.ghpk: {size / 1e6:.2f} MB")
    assert size < PACK_BUDGET_BYTES
    m = read_manifest(MANIFEST)
    assert m["region"] == "kathmandu_valley"
    assert m["tile_counts"]["10"] > 1000
    assert m["stats"]["buildings_in_tiles"] > 400_000
