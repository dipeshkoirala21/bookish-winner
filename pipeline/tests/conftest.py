"""Shared pytest fixtures for the pipeline test-suite."""

from __future__ import annotations

import sys
from pathlib import Path

import pytest

PIPELINE_ROOT = Path(__file__).resolve().parents[1]
if str(PIPELINE_ROOT) not in sys.path:
    sys.path.insert(0, str(PIPELINE_ROOT))

RAW = PIPELINE_ROOT / "data" / "raw"
NEPAL_PBF = RAW / "osm" / "nepal.osm.pbf"
FIXTURES = Path(__file__).resolve().parent / "fixtures"


def pytest_collection_modifyitems(config, items):
    if NEPAL_PBF.exists():
        return
    skip = pytest.mark.skip(reason="real source data not downloaded (run fetch.py)")
    for item in items:
        if "realdata" in item.keywords:
            item.add_marker(skip)


@pytest.fixture(scope="session")
def fixtures_dir() -> Path:
    return FIXTURES


@pytest.fixture(scope="session")
def nepal_pbf() -> Path:
    return NEPAL_PBF
