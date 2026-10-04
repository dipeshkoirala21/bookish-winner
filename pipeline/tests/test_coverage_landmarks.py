"""Tests for the coverage scanner and the landmark resolver (small OSM XML)."""

import json
import textwrap

import yaml

from ghumante_pipeline import coverage, landmarks

OSM_XML = textwrap.dedent("""\
    <?xml version='1.0' encoding='UTF-8'?>
    <osm version="0.6" generator="test">
      <node id="1" version="1" timestamp="2026-10-01T00:00:00Z" lat="27.7000" lon="85.3000"/>
      <node id="2" version="1" timestamp="2026-10-01T00:00:00Z" lat="27.7010" lon="85.3010"/>
      <node id="3" version="1" timestamp="2026-10-02T00:00:00Z" lat="27.7020" lon="85.3000"/>
      <node id="4" version="1" timestamp="2026-10-01T00:00:00Z" lat="27.7020" lon="85.3010"/>
      <node id="10" version="1" timestamp="2026-10-01T00:00:00Z" lat="27.7215" lon="85.3620">
        <tag k="name" v="Boudhanāth Stupa"/><tag k="name:ne" v="बौद्धनाथ स्तूप"/>
        <tag k="man_made" v="stupa"/><tag k="amenity" v="place_of_worship"/><tag k="religion" v="buddhist"/>
      </node>
      <node id="11" version="1" timestamp="2026-10-01T00:00:00Z" lat="27.7167" lon="85.3127">
        <tag k="place" v="neighbourhood"/><tag k="name" v="Thamel"/><tag k="name:ne" v="ठमेल"/>
      </node>
      <node id="12" version="1" timestamp="2026-10-01T00:00:00Z" lat="27.9881" lon="86.9252">
        <tag k="natural" v="peak"/><tag k="name" v="Mount Everest"/><tag k="ele" v="8848.86"/>
      </node>
      <node id="13" version="1" timestamp="2026-10-01T00:00:00Z" lat="28.5000" lon="83.0000">
        <tag k="name" v="Some Other Stupa"/><tag k="man_made" v="stupa"/>
      </node>
      <way id="100" version="1" timestamp="2026-10-01T00:00:00Z">
        <nd ref="1"/><nd ref="2"/><tag k="highway" v="primary"/><tag k="surface" v="asphalt"/>
      </way>
      <way id="101" version="1" timestamp="2026-10-01T00:00:00Z">
        <nd ref="2"/><nd ref="4"/><tag k="highway" v="track"/>
      </way>
      <way id="102" version="1" timestamp="2026-10-01T00:00:00Z">
        <nd ref="1"/><nd ref="2"/><nd ref="4"/><nd ref="3"/><nd ref="1"/>
        <tag k="building" v="yes"/><tag k="building:levels" v="3"/>
      </way>
    </osm>
    """)


def test_coverage_scan_counts(tmp_path):
    f = tmp_path / "t.osm"
    f.write_text(OSM_XML, encoding="utf-8")
    sc, _ = coverage.scan(f)
    data = sc.to_json("t.osm", 0.0)
    nepal = data["scopes"]["nepal"]
    assert nepal["roads"]["_all"]["count"] == 2
    assert nepal["roads"]["_all"]["tag_pct_by_count"]["surface"] == 50.0
    assert nepal["roads"]["primary"]["length_km"] >= 0.1  # ~0.15 km, rounded to 0.1
    assert nepal["buildings"]["count"] == 1
    assert nepal["buildings"]["tag_pct_by_count"]["building:levels"] == 100.0
    assert nepal["places"]["neighbourhood"]["tag_pct_by_count"]["name:ne"] == 100.0
    assert data["peaks"]["count"] == 1
    assert data["data_max_timestamp"].startswith("2026-10-01")  # untagged nodes are filtered out
    md = coverage.render_markdown(data)
    assert "# OSM tag coverage report" in md and "| primary |" in md


def test_landmark_resolution(tmp_path):
    f = tmp_path / "t.osm"
    f.write_text(OSM_XML, encoding="utf-8")
    cfg = tmp_path / "lm.yaml"
    cfg.write_text(yaml.safe_dump({"landmarks": [
        {"id": "boudhanath", "region": "kathmandu_valley", "kind": "stupa", "match": "bou?d+h?a(nath)?|बौद्ध",
         "prefer": ["man_made=stupa"], "near": [85.362, 27.721, 3]},
        {"id": "everest", "region": "khumbu", "kind": "peak", "match": "^mount everest$", "prefer": ["natural=peak"],
         "near": [86.925, 27.988, 3]},
        {"id": "far_stupa", "region": "x", "kind": "stupa", "match": "other stupa", "prefer": ["man_made=stupa"],
         "near": [85.3, 27.7, 5]},
    ]}, allow_unicode=True), encoding="utf-8")
    lms = landmarks.load_config(cfg)
    rows = landmarks.summarize(lms, landmarks.scan(f, lms))
    by = {r["id"]: r for r in rows}
    assert by["boudhanath"]["status"] == "found" and by["boudhanath"]["osm"] == "n10"
    assert by["boudhanath"]["name_ne"] == "बौद्धनाथ स्तूप"
    assert by["everest"]["osm"] == "n12"
    assert by["far_stupa"]["status"] == "missing"  # exists, but outside the search radius
    md = landmarks.render_md(rows)
    assert "1 missing" in md
    json.dumps(rows, ensure_ascii=False)
