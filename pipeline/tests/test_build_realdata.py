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
from ghumante_pipeline.model import PoiKind, Travel
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
    """"Boudha" lands on Boudhanath Stupa first: the landmark rank bonus (DATA_FORMATS
    section 3) puts it above the exact-match Baudha neighbourhood ~100 m away."""
    res = index.search("Boudha")
    assert res, "no results"
    top = res[0][1]
    assert top.kind == PoiKind.STUPA and top.flags & search_index.FLAG_LANDMARK, [e.display_name for _, e in res[:3]]
    assert "boudha" in top.name.default.lower() and _dist_m(top.lon, top.lat, *BOUDHA) < 300.0
    best = index.search("Boudhanath")[0][1]
    assert best.kind == PoiKind.STUPA and _dist_m(best.lon, best.lat, *BOUDHA) < 300.0


# Common ways people name the valley's hero landmarks -> the landmark id they must find first.
COMMON_QUERIES = {
    "Boudha": "boudhanath", "Boudhanath Stupa": "boudhanath", "बौद्धनाथ": "boudhanath",
    "Swayambhu": "swayambhunath", "Swayambhu Stupa": "swayambhunath", "Monkey Temple": "swayambhunath",
    "Pashupatinath": "pashupatinath", "Pashupati": "pashupatinath",
    "Kathmandu Durbar Square": "kathmandu_durbar_square", "Kathmandu Durbar": "kathmandu_durbar_square",
    "Basantapur": "kathmandu_durbar_square", "Patan Durbar Square": "patan_durbar_square",
    "Bhaktapur Durbar Square": "bhaktapur_durbar_square", "Nyatapola": "nyatapola",
    "Changu Narayan": "changu_narayan", "Dharahara": "dharahara", "धरहरा": "dharahara",
    "Garden of Dreams": "garden_of_dreams", "Thamel": "thamel", "Chandragiri Cable Car": "chandragiri_cable_car",
    "Nagarkot": "nagarkot_viewpoint", "Tribhuvan airport": "tribhuvan_airport",
    "Kathmandu airport": "tribhuvan_airport", "TIA": "tribhuvan_airport", "Tribhuvan Intl": "tribhuvan_airport",
}


def _valley_landmarks() -> dict[str, dict]:
    import json

    from ghumante_pipeline.config import CONFIG_DIR

    rows = json.loads((CONFIG_DIR / "landmarks.resolved.json").read_text(encoding="utf-8"))
    return {r["id"]: r for r in rows if r.get("region") == "kathmandu_valley" and r.get("osm")}


def _ref32(osm: str) -> int:
    return ((int(osm[1:]) << 2) | {"n": 0, "w": 1, "r": 2}[osm[0]]) & 0xFFFFFFFF


def test_every_valley_landmark_is_searchable(index) -> None:
    """Each resolved landmark has its own search entry with the landmark flag (Dharahara is a
    man_made=tower and the cable car a LINE: both get synthesised POIs)."""
    by_ref = {}
    for e in index.entries:
        by_ref.setdefault(e.osm_ref, []).append(e)
    missing = []
    for lid, lm in _valley_landmarks().items():
        es = [e for e in by_ref.get(_ref32(lm["osm"]), []) if e.flags & search_index.FLAG_LANDMARK]
        if not es:
            missing.append(lid)
    assert not missing, missing
    pashu = [e for e in by_ref[_ref32(_valley_landmarks()["pashupatinath"]["osm"])]]
    assert pashu[0].kind == PoiKind.TEMPLE_HINDU


@pytest.mark.parametrize("query,landmark", sorted(COMMON_QUERIES.items()))
def test_common_landmark_queries(index, query, landmark) -> None:
    lm = _valley_landmarks()[landmark]
    res = index.search(query, 3)
    assert res, f"{query!r}: no results"
    top = res[0][1]
    assert top.flags & search_index.FLAG_LANDMARK, (query, [e.display_name for _, e in res])
    assert top.osm_ref == _ref32(lm["osm"]) or _dist_m(top.lon, top.lat, lm["lon"], lm["lat"]) < 300.0, \
        (query, top.display_name, lm["osm"])


def test_patan_durbar_square_not_duplicated(index) -> None:
    res = index.search("Patan Durbar Square", 10)
    near = [e for _, e in res if e.kind == PoiKind.HERITAGE_SQUARE
            and _dist_m(e.lon, e.lat, 85.3253, 27.6727) < 300.0
            and (e.name.en or e.name.default).lower() == "patan durbar square"]
    assert len(near) == 1, [(e.display_name, e.osm_ref) for e in near]


def test_search_entries_inside_leaf_tiles(index) -> None:
    """No place or POI entry points into the extract's buffer zone (admin areas may)."""
    _need(PACK)
    with PackReader(PACK) as pr:
        leaf = {projection.tile_from_key(k) for k in pr.keys()}
    lvl = max(t.level for t in leaf)
    size = projection.tile_size(lvl)
    keys = {(t.tx, t.ty) for t in leaf if t.level == lvl}
    from ghumante_pipeline.model import PlaceKind
    admin = {1000 + int(k) for k in (PlaceKind.PROVINCE, PlaceKind.DISTRICT, PlaceKind.LOCAL_LEVEL)}
    out = [e for e in index.entries if e.kind not in admin
           and (math.floor(e.x / size), math.floor(e.z / size)) not in keys]
    assert not out, [(e.display_name, e.kind) for e in out[:10]]


def test_graph_inside_leaf_tiles(graph) -> None:
    _need(MANIFEST)
    m = read_manifest(MANIFEST)
    with PackReader(PACK) as pr:
        leaf = [projection.tile_from_key(k) for k in pr.keys()]
    lvl = m["leaf_level"]
    b = np.array([t.bounds for t in leaf if t.level == lvl])
    x0, z0, x1, z1 = b[:, 0].min(), b[:, 1].min(), b[:, 2].max(), b[:, 3].max()
    xz = graph.node_xz_array()
    assert xz[:, 0].min() >= x0 - 0.05 and xz[:, 0].max() <= x1 + 0.05
    assert xz[:, 1].min() >= z0 - 0.05 and xz[:, 1].max() <= z1 + 0.05


LANDMARK_ROUTES = [("kathmandu_durbar_square", "bhaktapur_durbar_square"), ("thamel", "boudhanath"),
                   ("swayambhunath", "pashupatinath"), ("swayambhunath", "bhaktapur_durbar_square"),
                   ("boudhanath", "nagarkot_viewpoint"), ("patan_durbar_square", "tribhuvan_airport")]


@pytest.mark.parametrize("a,b", LANDMARK_ROUTES)
def test_landmark_routes_every_profile(graph, a, b) -> None:
    """Snapping to the profile's main network: every profile finds a route between hero landmarks."""
    lms = _valley_landmarks()
    src = tuple(map(float, projection.lonlat_to_game(lms[a]["lon"], lms[a]["lat"])))
    dst = tuple(map(float, projection.lonlat_to_game(lms[b]["lon"], lms[b]["lat"])))
    eta = routing.eta_by_profile(graph, src, dst)
    assert all(v is not None and v[0] > 1000.0 for v in eta.values()), eta


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
    print(f"motorbike Thamel -> Boudhanath: {r.length_m / 1000:.2f} km, {r.time_s / 60:.1f} min")
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
    assert list(m["tile_counts"]) == sorted(m["tile_counts"], key=int)
    assert m["stats"]["buildings_in_tiles"] > 400_000
    assert m["stats"]["road_km"] < m["stats"]["road_km_extract"]


@pytest.fixture(scope="module")
def leaf_tiles():
    _need(PACK, INDEX)
    with PackReader(PACK) as pr:
        keys = list(pr.keys())
        lvl = max(projection.tile_from_key(k).level for k in keys)
        return [decode_tile(pr.get(k)) for k in keys if projection.tile_from_key(k).level == lvl]


def test_pois_search_id_is_own_entry(index, leaf_tiles) -> None:
    """DATA_FORMATS 1.8: entries[search_id - 1] is the record's own OSM object and kind."""
    bad, linked = [], 0
    for td in leaf_tiles:
        for p in td.pois:
            if p.search_id:
                linked += 1
                e = index.entries[p.search_id - 1]
                if e.kind != p.kind or e.osm_ref != p.osm_ref & 0xFFFFFFFF:
                    bad.append((str(td.tile), p.kind, e.kind, e.display_name))
    assert linked > 1000 and not bad, bad[:10]


def test_road_pieces_have_no_rounding_artifacts(leaf_tiles) -> None:
    from test_seams import polyline_problems

    problems = [m for td in leaf_tiles for m in polyline_problems(td)]
    assert not problems, problems[:10]


def test_no_inferred_metal_off_bridges(leaf_tiles) -> None:
    from ghumante_pipeline.model import RoadFlags, Surface, SurfaceSource

    bad = {r.osm_way_id for td in leaf_tiles for r in td.roads
           if r.surface in (Surface.METAL, Surface.WOOD) and r.surface_source == SurfaceSource.INFERRED
           and not r.flags & RoadFlags.BRIDGE}
    assert not bad, sorted(bad)[:10]


def test_manifest_provenance_and_attribution() -> None:
    """The OSM snapshot date is recorded (lock-file Last-Modified when the PBF header has
    none), no build-machine paths ship in the manifest, and the attribution is the
    licence-mandated wording from sources.yaml / docs/LICENSES.md."""
    _need(MANIFEST)
    from ghumante_pipeline.config import load_sources

    m = read_manifest(MANIFEST)
    assert m["sources"]["osm"].get("timestamp"), "manifest has no OSM snapshot timestamp"
    assert str(REG.resolve()) not in MANIFEST.read_text(encoding="utf-8")
    if "qa" in m.get("stats", {}):
        assert m["stats"]["qa"]["out_dir"] == "qa"
    srcs = load_sources()
    assert m["attribution"] == [srcs[k]["attribution"] for k in ("osm", "dem", "landcover")]
