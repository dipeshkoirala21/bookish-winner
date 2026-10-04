"""End-to-end build of the synthetic fixture (tests/fixtures/synth/make_synth.py).

OSM XML + synthetic DEM and WorldCover GeoTIFFs -> ``build.build_region`` ->
pack, search index, routing graph, manifest, QA export. Checks the outputs
decode, are deterministic, are searchable and routable, and that the QA
GeoJSON (decoded from the pack) lands within 2 cm of the source coordinates.
"""

from __future__ import annotations

import json
import math
from pathlib import Path

import numpy as np
import pytest
import yaml
from PIL import Image

from fixtures.synth.make_synth import PLACE_A, PLACE_B, TEMPLE_NAME, TEMPLE_NAME_NE, make_synth, synth_build
from ghumante_pipeline import build, projection, routing, search_index
from ghumante_pipeline.model import BuildingFlags, PoiFlags, RoadFlags, Travel
from ghumante_pipeline.pack import PackReader, read_manifest, sha256_file
from ghumante_pipeline.tile_format import decode_tile

TOL_M = 0.02


@pytest.fixture(scope="session")
def synth(tmp_path_factory):
    return synth_build(tmp_path_factory.getbasetemp() / "synth_e2e")


@pytest.fixture(scope="session")
def synth_again(tmp_path_factory):
    # Different directory, serial encoding and no extract cache: must be byte-identical.
    return synth_build(tmp_path_factory.getbasetemp() / "synth_e2e_again", workers=1, qa=False, use_cache=False)


def _metres(lonlat_a: np.ndarray, lonlat_b: np.ndarray) -> np.ndarray:
    a, b = np.asarray(lonlat_a, dtype=float), np.asarray(lonlat_b, dtype=float)
    lat = np.radians(0.5 * (a[..., 1] + b[..., 1]))
    dx = (a[..., 0] - b[..., 0]) * 111_320.0 * np.cos(lat)
    dy = (a[..., 1] - b[..., 1]) * 110_574.0
    return np.hypot(dx, dy)


def _geojson(path: Path) -> list[dict]:
    return json.loads(path.read_text(encoding="utf-8"))["features"]


# ---------------------------------------------------------------------------
# Outputs and manifest
# ---------------------------------------------------------------------------
def test_outputs_exist(synth) -> None:
    for p in (synth.pack, synth.index, synth.graph, synth.reg_dir / "synth_test.manifest.json",
              synth.reg_dir / "BUILD_REPORT.md", synth.qa_dir / "index.json"):
        assert p.exists(), p


def test_manifest(synth) -> None:
    m = read_manifest(synth.reg_dir / "synth_test.manifest.json")
    assert m["region"] == "synth_test" and m["scale_model"] == "identity"
    assert m["detail_levels"] == [9, 10] and m["horizon_levels"] == [7, 8]
    files = {f["path"]: f for f in m["files"]}
    assert set(files) == {"synth_test.ghpk", "synth_test.search.ghsi", "synth_test.route.ghrg"}
    for name, f in files.items():
        p = synth.reg_dir / name
        assert f["bytes"] == p.stat().st_size and f["sha256"] == sha256_file(p)
    with PackReader(synth.pack) as pr:
        levels = {}
        for k in pr.keys():
            levels[str(k >> 58)] = levels.get(str(k >> 58), 0) + 1
    assert m["tile_counts"] == levels
    assert any("OpenStreetMap" in a for a in m["attribution"]) and len(m["attribution"]) == 3
    s = m["stats"]
    for k in ("surface_tagged_pct", "surface_derived_pct", "surface_inferred_pct", "surface_default_pct",
              "building_levels_inferred_pct", "graph_nodes", "graph_edges", "search_entries", "timings_s"):
        assert k in s
    assert s["buildings"] == len(synth.synth.buildings) + 1
    assert abs(sum(s[f"surface_{k}_pct"] for k in ("tagged", "derived", "inferred", "default")) - 100.0) < 0.1
    assert 0 < s["surface_tagged_pct"] < 100 and s["surface_derived_pct"] > 0  # the grade2 track
    assert 0 < s["building_levels_inferred_pct"] < 100
    assert m["sources"]["osm"]["md5"] and len(m["sources"]["dem"]) == 1
    report = (synth.reg_dir / "BUILD_REPORT.md").read_text(encoding="utf-8")
    assert "surface_tagged_pct" in report and "synth_test.ghpk" in report


def test_pack_decodes_with_crc(synth) -> None:
    with PackReader(synth.pack) as pr:
        n = 0
        for key in pr.keys():
            td = decode_tile(pr.get(key, verify=True))
            assert td.tile.key == key and td.data_version == 1
            assert td.heights_q is not None and td.biomes is not None
            n += 1
        assert n == 19
    meta = None
    with PackReader(synth.pack) as pr:
        for key in pr.keys():
            td = decode_tile(pr.get(key))
            if td.meta:
                meta = td.meta
    assert meta["region"] == "synth_test" and set(meta["sources"]) == {"osm", "dem", "landcover"}


def test_determinism(synth, synth_again) -> None:
    for a, b in ((synth.pack, synth_again.pack), (synth.index, synth_again.index), (synth.graph, synth_again.graph)):
        assert sha256_file(a) == sha256_file(b), a.name


# ---------------------------------------------------------------------------
# Content
# ---------------------------------------------------------------------------
def _all_tiles(synth):
    with PackReader(synth.pack) as pr:
        return [decode_tile(pr.get(k)) for k in pr.keys()]


def test_landmark_and_poi_search_ids(synth) -> None:
    tiles = _all_tiles(synth)
    temple_ref = synth.synth.temple_way << 1
    b = [b for td in tiles for b in td.buildings if b.osm_ref == temple_ref]
    assert len(b) == 1 and b[0].flags & BuildingFlags.LANDMARK
    idx = search_index.read_index(synth.index)
    pois = [(td, p) for td in tiles for p in td.pois if p.osm_ref == (synth.synth.temple_way << 2) | 1]
    assert len(pois) == 1
    td, p = pois[0]
    assert p.flags & PoiFlags.LANDMARK and p.search_id > 0
    assert idx.entries[p.search_id - 1].name.default == TEMPLE_NAME
    assert td.name(p.name_ref).ne == TEMPLE_NAME_NE


def test_oneway_reverse_stored_reversed(synth) -> None:
    # Way "Ulto Galli" (oneway=-1) runs east in OSM; stored points must run west.
    tiles = _all_tiles(synth)
    pieces = [(td, r) for td in tiles for r in td.roads if td.name(r.name_ref) and
              td.name(r.name_ref).default == "Ulto Galli"]
    assert pieces
    for td, r in pieces:
        assert r.flags & RoadFlags.ONEWAY
        g = r.points[:, 0]
        assert g[0] > g[-1]


def test_search(synth) -> None:
    idx = search_index.read_index(synth.index)
    top = idx.search("bhairab")
    assert top and top[0][1].name.default == TEMPLE_NAME
    top = idx.search(TEMPLE_NAME_NE)
    assert top and top[0][1].name.default == TEMPLE_NAME
    top = idx.search("synthtol")
    assert top and top[0][1].name.default == PLACE_A[0]
    assert idx.search("synth gompa")[0][1].name.default == "Synth Gompa"


def test_route_between_places(synth) -> None:
    g = routing.read_graph(synth.graph)
    ax, az = projection.lonlat_to_game(PLACE_A[1], PLACE_A[2])
    bx, bz = projection.lonlat_to_game(PLACE_B[1], PLACE_B[2])
    straight = math.hypot(float(bx - ax), float(bz - az))
    for prof in (Travel.FOOT, Travel.MOTORBIKE):
        s = routing.nearest_node(g, float(ax), float(az), prof, max_dist_m=300)
        d = routing.nearest_node(g, float(bx), float(bz), prof, incoming=True, max_dist_m=300)
        assert s is not None and d is not None
        r = routing.route(g, s, d, prof)
        assert r is not None, prof
        assert straight * 0.8 < r.length_m < straight * 2.0
        assert r.time_s > 0


# ---------------------------------------------------------------------------
# QA export
# ---------------------------------------------------------------------------
def test_qa_index(synth) -> None:
    idx = json.loads((synth.qa_dir / "index.json").read_text(encoding="utf-8"))
    assert idx["format"] == "ghumante-qa-index" and idx["leaf_level"] == 10
    assert len(idx["tiles"]) == 19
    leaf = [t for t in idx["tiles"] if t["level"] == 10]
    assert len(leaf) == 9
    for t in leaf:
        assert len(t["corners_lonlat"]) == 4
        for kind, size in (("hillshade", 65), ("biome", 33)):
            p = synth.qa_dir / t[kind]
            assert p.exists()
            with Image.open(p) as im:
                assert im.size == (size, size)
        # Corners agree with the projection of the tile square.
        tid = projection.TileId(10, t["tx"], t["ty"])
        lon, lat = projection.game_to_lonlat(tid.x0, tid.z0)
        assert _metres(np.array(t["corners_lonlat"][0]), np.array([lon, lat])) < 0.02
        assert t["key"] == str(tid.key)
    assert set(idx["layers"]) >= {"roads", "trails", "buildings", "areas", "lines", "pois"}
    assert idx["layers"]["buildings"]["count"] == len(synth.synth.buildings) + 1
    assert "WATER" in idx["biome_palette"] and idx["raster"]["biome"]["registration"] == "vertex"


def test_qa_buildings_roundtrip(synth) -> None:
    syn = synth.synth
    feats = _geojson(synth.qa_dir / "buildings.geojson")
    assert len(feats) == len(syn.buildings) + 1
    worst = 0.0
    for f in feats:
        if f["properties"]["osm_type"] != "w":
            continue
        src = syn.lonlat(syn.buildings[f["properties"]["osm_id"]])
        ring = np.array(f["geometry"]["coordinates"][0][:-1])
        assert len(ring) == len(src)
        d = _metres(ring[:, None, :], src[None, :, :]).min(axis=1)
        worst = max(worst, float(d.max()))
    assert worst < TOL_M, worst
    # Per-tile files hold the same buildings.
    per_tile = sum(len(_geojson(p)) for p in (synth.qa_dir / "buildings").rglob("*.geojson"))
    assert per_tile == len(feats)
    border = [f for f in feats if f["properties"]["osm_id"] == syn.border_building]
    assert len(border) == 1


def test_qa_pois_roundtrip(synth) -> None:
    syn = synth.synth
    feats = _geojson(synth.qa_dir / "pois.geojson")
    by_id = {f["properties"]["osm_id"]: f for f in feats if f["properties"]["osm_type"] == "n"}
    for nid, name in list(syn.poi_nodes.items()) + [(v, k) for k, v in syn.places.items()]:
        f = by_id[nid]
        assert f["properties"]["name"] == name
        assert _metres(np.array(f["geometry"]["coordinates"]), np.array(syn.nodes[nid])) < TOL_M
    kinds = {f["properties"]["kind"] for f in feats}
    assert "PLACE_NEIGHBOURHOOD" in kinds and "TEMPLE_HINDU" in kinds and "PEAK" in kinds


def test_qa_roads_roundtrip(synth) -> None:
    syn = synth.synth
    feats = _geojson(synth.qa_dir / "roads.geojson") + _geojson(synth.qa_dir / "trails.geojson")
    assert {"road_class", "class", "surface", "surface_source", "sac_scale", "oneway"} <= set(feats[0]["properties"])
    assert any(f["properties"]["road_class"] == "PATH" for f in _geojson(synth.qa_dir / "trails.geojson"))
    matched = 0
    for f in feats:
        src = syn.lonlat(syn.roads[f["properties"]["osm_way_id"]])
        pts = np.array(f["geometry"]["coordinates"])
        d = _metres(pts[:, None, :], src[None, :, :]).min(axis=1)
        # Interior vertices are source vertices; the two ends may be cut points on a tile border.
        inner = d[1:-1]
        assert (inner < TOL_M).all() or len(inner) == 0
        matched += int((d < TOL_M).sum())
    assert matched > 100


def test_qa_area_lake(synth) -> None:
    feats = [f for f in _geojson(synth.qa_dir / "areas.geojson") if f["properties"]["osm_id"] == synth.synth.lake_way]
    assert len(feats) == 4  # the lake sits on a leaf-tile corner
    assert all(f["properties"]["kind"] == "WATER_LAKE" and f["properties"]["flags"] & 1 for f in feats)


# ---------------------------------------------------------------------------
# CLI and partial stages
# ---------------------------------------------------------------------------
def test_cli_and_qa_only_stage(tmp_path) -> None:
    syn = make_synth(tmp_path / "src")
    regions = {"regions": {"synth_test": {
        "name_en": "Synth", "name_ne": "सिन्थ", "bbox": list(syn.region.bbox),
        "horizon_bbox": list(syn.region.horizon_bbox), "detail_levels": [9, 10], "horizon_levels": [7, 8],
        "height_grid": 33, "biome_grid": 17, "bbox_buffer_m": 300}}}
    rf = tmp_path / "regions.yaml"
    rf.write_text(yaml.safe_dump(regions, allow_unicode=True), encoding="utf-8")
    out = tmp_path / "out"
    rc = build.main(["--region", "synth_test", "--regions-file", str(rf), "--pbf", str(syn.osm), "--raw-dir",
                     str(syn.root), "--out", str(out), "--workers", "1", "-q"])
    assert rc == 0
    reg = out / "regions" / "synth_test"
    assert (reg / "synth_test.ghpk").exists() and not (reg / "qa").exists()
    assert list((out / "cache" / "synth_test").glob("extract-*.pkl.gz"))
    pack_sha = sha256_file(reg / "synth_test.ghpk")
    # QA only, from the existing pack (no extract needed).
    region = build.config.load_regions(rf)["synth_test"]
    m = build.build_region(region, pbf=syn.osm, raw_dir=syn.root, out_dir=out, stages=["qa"])
    assert (reg / "qa" / "index.json").exists()
    assert sha256_file(reg / "synth_test.ghpk") == pack_sha
    assert m["stats"]["qa"]["layers"]["buildings"] == len(syn.buildings) + 1
    with pytest.raises(ValueError):
        build.build_region(region, pbf=syn.osm, raw_dir=syn.root, out_dir=out, stages=["bogus"])


def test_extract_cache_key_changes(tmp_path) -> None:
    syn = make_synth(tmp_path / "src")
    k1 = build.extract_cache_key(syn.osm, syn.region, (4, 6, 7))
    k2 = build.extract_cache_key(syn.osm, syn.region, (4, 6))
    from dataclasses import replace
    k3 = build.extract_cache_key(syn.osm, replace(syn.region, bbox=(85.30, 27.70, 85.31, 27.71)), (4, 6, 7))
    assert len({k1, k2, k3}) == 3
    assert k1 == build.extract_cache_key(syn.osm, syn.region, (4, 6, 7))


def test_load_landmarks(tmp_path) -> None:
    p = tmp_path / "lm.json"
    p.write_text(json.dumps([{"id": "a", "osm": "w12"}, {"id": "b", "osm": "n7"}, {"id": "c", "osm": None},
                             {"id": "d", "osm": "x1"}]), encoding="utf-8")
    ids, refs = build.load_landmarks(p)
    assert ids == {12: "a", 7: "b"} and refs == {("w", 12), ("n", 7)}
    assert build.load_landmarks(tmp_path / "missing.json") == ({}, set())
