"""shared/golden/make_golden.py: the C# core's golden files are deterministic, small and up to date."""

from __future__ import annotations

import filecmp
import importlib.util
import json
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[2]
GOLDEN = ROOT / "shared" / "golden"
FILES = ("tm84_points.json", "golden.ght", "golden_unknown.ght", "golden_tile.json", "golden.ghpk", "golden_pack.json",
         "golden.ghsi", "golden_search.json", "golden.ghrg", "golden_routes.json", "golden_profiles.json")


@pytest.fixture(scope="module")
def make_golden():
    spec = importlib.util.spec_from_file_location("make_golden", GOLDEN / "make_golden.py")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


@pytest.fixture(scope="module")
def generated(make_golden, tmp_path_factory) -> tuple[Path, Path]:
    a = tmp_path_factory.mktemp("golden_a")
    b = tmp_path_factory.mktemp("golden_b")
    make_golden.main(["--out", str(a)])
    make_golden.main(["--out", str(b)])
    return a, b


def test_generation_is_deterministic(generated):
    a, b = generated
    for name in FILES:
        assert filecmp.cmp(a / name, b / name, shallow=False), name


def test_committed_golden_files_are_up_to_date(generated):
    a, _ = generated
    for name in FILES:
        assert (GOLDEN / name).exists(), f"{name} missing: run python3 shared/golden/make_golden.py"
        assert filecmp.cmp(a / name, GOLDEN / name, shallow=False), \
            f"{name} is stale: run python3 shared/golden/make_golden.py"


def test_total_size_is_commit_friendly(generated):
    a, _ = generated
    assert sum((a / n).stat().st_size for n in FILES) < 200 * 1024


def test_golden_content_covers_the_formats(generated):
    a, _ = generated
    tile = json.loads((a / "golden_tile.json").read_text(encoding="utf-8"))
    fourccs = {c["fourcc"] for c in tile["chunks"]}
    assert fourccs == {"AREA", "BIOM", "BLDG", "HGHT", "LINE", "META", "NAME", "POIS", "ROAD", "SEED"}
    assert {c["codec"] for c in tile["chunks"]} == {0, 1}
    assert "XTRA" in tile["unknown_variant"]["chunks"]

    tm = json.loads((a / "tm84_points.json").read_text(encoding="utf-8"))
    assert len(tm["points"]) >= 50

    search = json.loads((a / "golden_search.json").read_text(encoding="utf-8"))
    assert 25 <= search["entry_count"] <= 40
    assert sum(1 for q in search["queries"] if q["results"]) > 30
    names = {n[1] for n in search["names"]} | {n[0] for n in search["names"]}
    assert {"Kathmandu", "Boudhanath", "Thamel", "Pokhara", "काठमाडौं"} <= names

    routes = json.loads((a / "golden_routes.json").read_text(encoding="utf-8"))
    assert 30 <= routes["node_count"] <= 50
    found = [r for r in routes["routes"] if r["found"]]
    assert len(found) > 50 and any(not r["found"] for r in routes["routes"])
    assert len({r["profile"] for r in found}) == 7
