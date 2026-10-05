"""The attribution shipped in every manifest (from config/sources.yaml) must be the
licence-mandated wording recorded in docs/LICENSES.md, verbatim."""

from __future__ import annotations

import re
from pathlib import Path

from ghumante_pipeline import config

LICENSES = Path(__file__).resolve().parents[2] / "docs" / "LICENSES.md"


def _quote_after(text: str, marker: str) -> str:
    """The first ``> ...`` blockquote line after the line containing ``marker``."""
    lines = text.splitlines()
    start = next(i for i, ln in enumerate(lines) if marker in ln)
    for ln in lines[start + 1:]:
        m = re.match(r"\s*>\s?(.*)$", ln)
        if m:
            return m.group(1).strip()
    raise AssertionError(f"no blockquote after {marker!r}")


def test_dem_attribution_is_art_6b_notice() -> None:
    text = LICENSES.read_text(encoding="utf-8")
    notice = _quote_after(text, "Required notice (Art. 6b")
    assert notice.startswith("produced using Copernicus WorldDEM-30")
    assert config.load_sources()["dem"]["attribution"] == notice


def test_worldcover_attribution_has_licence_and_modification_note() -> None:
    text = LICENSES.read_text(encoding="utf-8")
    notice = _quote_after(text, "### 1.3 ESA WorldCover")
    assert "Licensed under CC BY 4.0" in notice and "Resampled and stylised" in notice
    assert config.load_sources()["landcover"]["attribution"] == notice


def test_osm_attribution() -> None:
    assert config.load_sources()["osm"]["attribution"] == "© OpenStreetMap contributors"
