"""Checks the committed synthetic sample against docs/DATA_FORMATS.md section 5.

    python3 -m pytest tools/qa-viewer/tests -q
"""

from __future__ import annotations

import filecmp
import json
import struct
import subprocess
import sys
from pathlib import Path

import pytest

VIEWER = Path(__file__).resolve().parents[1]
SAMPLE = VIEWER / "sample" / "qa"
SPEC_LAYERS = ("roads", "trails", "buildings", "areas", "lines", "pois")
GEOM = {"roads": {"LineString", "MultiLineString"}, "trails": {"LineString", "MultiLineString"},
        "lines": {"LineString", "MultiLineString"}, "buildings": {"Polygon", "MultiPolygon"},
        "areas": {"Polygon", "MultiPolygon"}, "pois": {"Point"}, "places": {"Point"}}


def load(name: str) -> dict:
    return json.loads((SAMPLE / name).read_text(encoding="utf-8"))


def png_size(path: Path) -> tuple[int, int]:
    head = path.read_bytes()[:24]
    assert head[:8] == b"\x89PNG\r\n\x1a\n", path
    return struct.unpack(">II", head[16:24])


def walk(c):
    if isinstance(c[0], (int, float)):
        yield c
    else:
        for cc in c:
            yield from walk(cc)


def test_index_has_the_spec_fields():
    idx = load("index.json")
    assert idx["region"] == "sample_thamel"
    w, s, e, n = idx["bbox_lonlat"]
    assert w < 85.31 < e and s < 27.715 < n  # Thamel
    assert set(SPEC_LAYERS) <= set(idx["layers"])
    assert idx["stats"]["roads"] == idx["layers"]["roads"]["count"]
    assert idx["biome_palette"]["WATER"].startswith("#")
    levels = {t["level"] for t in idx["tiles"]}
    assert levels == {9, 10}
    for t in idx["tiles"]:
        assert len(t["corners_lonlat"]) == 4 and int(t["key"]) >> 58 == t["level"]


@pytest.mark.parametrize("layer", SPEC_LAYERS + ("places",))
def test_layer_files_are_lonlat_feature_collections(layer):
    idx = load("index.json")
    fc = load(idx["layers"][layer]["path"])
    assert fc["type"] == "FeatureCollection"
    assert len(fc["features"]) == idx["layers"][layer]["count"] > 0
    w, s, e, n = idx["bbox_lonlat"]
    for f in fc["features"]:
        assert f["geometry"]["type"] in GEOM[layer]
        for lon, lat in walk(f["geometry"]["coordinates"]):
            assert w - 1e-3 <= lon <= e + 1e-3 and s - 1e-3 <= lat <= n + 1e-3


def test_rasters_exist_for_every_leaf_tile_with_vertex_aligned_sizes():
    idx = load("index.json")
    leaf = [t for t in idx["tiles"] if t["level"] == idx["leaf_level"]]
    assert len(leaf) == 2
    for t in leaf:
        assert png_size(SAMPLE / t["hillshade"]) == (129, 129)
        assert png_size(SAMPLE / t["biome"]) == (65, 65)


def test_tiled_variant_splits_buildings_by_tile():
    idx = load("index.tiled.json")
    tpl = idx["layers"]["buildings"]["tiles"]
    total = 0
    for t in (t for t in idx["tiles"] if t["level"] == 10):
        fc = load(tpl.format(L=t["level"], tx=t["tx"], ty=t["ty"]))
        assert all(f["properties"]["tile"] == f"10/{t['tx']}/{t['ty']}" for f in fc["features"])
        total += len(fc["features"])
    assert total == idx["layers"]["buildings"]["count"]


def test_generator_is_deterministic(tmp_path):
    pytest.importorskip("pyproj")
    pytest.importorskip("shapely")
    out = tmp_path / "qa"
    subprocess.run([sys.executable, str(VIEWER / "sample" / "make_sample.py"), "--out", str(out)], check=True,
                   capture_output=True)
    cmp = filecmp.dircmp(SAMPLE, out)

    def diffs(c):
        bad = c.diff_files + c.left_only + c.right_only
        for sub in c.subdirs.values():
            bad += diffs(sub)
        return bad

    assert diffs(cmp) == [], "sample/qa is stale: rerun tools/qa-viewer/sample/make_sample.py"
