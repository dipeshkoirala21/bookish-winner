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
         "golden.ghsi", "golden_search.json", "golden.ghrg", "golden_routes.json", "golden_profiles.json",
         # W2 (F2 chunks and region files)
         "golden_w2.ght", "golden_w2.json", "golden.ghrt", "golden_transit.json", "golden.ghcd", "golden_curated.json",
         "golden_aviation.json")


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
    assert sum((a / n).stat().st_size for n in FILES) < 280 * 1024


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

    w2 = json.loads((a / "golden_w2.json").read_text(encoding="utf-8"))
    assert {c["fourcc"] for c in w2["chunks"]} == {"AREA", "BFNT", "BIOM", "BLDG", "HGHT", "JNCT", "LINE", "META",
                                                   "NAME", "POIS", "PROP", "RATR", "ROAD", "RSTR", "SEED"}
    rs = w2["road_structures"]
    assert len(rs) == len(w2["roads"]) and {r["kind"] for r in rs} == {0, 1, 3}
    assert any(r["shift_cm"] for r in rs) and any(v is not None and v < 0 for r in rs for v in r["deck_cm"])
    assert all(len(r["deck_role"]) in (0, len(rd["points"]) // 2) for r, rd in zip(rs, w2["roads"]))
    assert len(w2["road_attrs"]) == len(w2["roads"]) and len(w2["building_fronts"]) == len(w2["buildings"])
    # The clockwise golden building is re-oriented: its front edge 1 becomes 2 and the second edge 0 becomes 3.
    assert [f["front_edge"] for f in w2["building_fronts"]] == [255, 2]
    assert [f["second_edge"] for f in w2["building_fronts"]] == [255, 3]
    assert any(len(r["corridor_dm"]) for r in w2["road_attrs"])
    transit_ = json.loads((a / "golden_transit.json").read_text(encoding="utf-8"))
    assert {r["mode_name"] for r in transit_["routes"]} == {"BUS", "TEMPO", "HIKING"}
    assert len(transit_["restrictions"]) == 2
    cur = json.loads((a / "golden_curated.json").read_text(encoding="utf-8"))
    assert [r["id"] for r in cur["records"]] == sorted(r["id"] for r in cur["records"])
    av = json.loads((a / "golden_aviation.json").read_text(encoding="utf-8"))
    assert set(av["procedures"]) >= {"A1", "D1", "D2", "H-E"}

    routes = json.loads((a / "golden_routes.json").read_text(encoding="utf-8"))
    assert 30 <= routes["node_count"] <= 50
    assert routes["no_car_ways"] == [503]
    found = [r for r in routes["routes"] if r["found"]]
    assert len(found) > 50 and any(not r["found"] for r in routes["routes"])
    assert len({r["profile"] for r in found}) == 7
