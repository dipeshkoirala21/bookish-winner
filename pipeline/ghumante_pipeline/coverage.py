"""OSM tag coverage report.

Scans an OSM PBF extract once and measures how well the tags the game depends
on are populated (road surfaces, building levels, Nepali names, trail difficulty,
...). The numbers decide how much the pipeline has to *infer* instead of read.

Usage:
    python -m ghumante_pipeline.coverage --pbf data/raw/osm/nepal.osm.pbf \
        --out ../docs/reports

Writes ``tag_coverage.json`` (machine readable, used by CI to detect regressions
in upstream data) and ``tag_coverage.md`` (human readable tables).
"""

from __future__ import annotations

import argparse
import json
import math
import time
from collections import Counter, defaultdict
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path

import osmium

# --------------------------------------------------------------------------
# Regions of interest (lon_min, lat_min, lon_max, lat_max). Rough bounding
# boxes, only used to break coverage down by area; a feature is attributed to
# the region containing its first node.
# --------------------------------------------------------------------------
REGIONS: dict[str, tuple[float, float, float, float]] = {
    "kathmandu_valley": (85.18, 27.56, 85.56, 27.82),
    "pokhara": (83.88, 28.14, 84.08, 28.30),
    "prithvi_corridor": (84.08, 27.70, 85.18, 28.00),
    "khumbu": (86.55, 27.65, 86.95, 28.05),
    "annapurna": (83.70, 28.30, 84.20, 28.85),
    "chitwan": (84.10, 27.40, 84.70, 27.75),
    "mustang": (83.55, 28.75, 84.20, 29.35),
}

ROAD_CLASSES = [
    "motorway", "motorway_link", "trunk", "trunk_link", "primary", "primary_link",
    "secondary", "secondary_link", "tertiary", "tertiary_link", "unclassified",
    "residential", "living_street", "service", "road", "track",
]
TRAIL_CLASSES = ["path", "footway", "steps", "bridleway", "cycleway", "pedestrian"]
ROAD_TAGS = [
    "surface", "tracktype", "smoothness", "width", "lanes", "name", "name:ne",
    "name:en", "ref", "maxspeed", "oneway", "bridge", "tunnel", "layer",
]
TRAIL_TAGS = ["surface", "sac_scale", "trail_visibility", "name", "name:ne", "width", "incline"]
BUILDING_TAGS = [
    "building:levels", "height", "roof:shape", "roof:material", "roof:colour",
    "building:material", "building:colour", "name", "name:ne", "addr:street",
    "amenity", "shop",
]
PLACE_TAGS = ["name", "name:ne", "name:en", "population", "ele", "wikidata", "is_in"]
NAMED_TAGS = ["name", "name:ne", "name:en", "int_name", "alt_name", "old_name", "wikidata", "wikipedia"]


def _is_devanagari(s: str) -> bool:
    return any("ऀ" <= ch <= "ॿ" for ch in s)


def _is_latin(s: str) -> bool:
    return any(("a" <= ch.lower() <= "z") for ch in s)


def _region_of(lon: float, lat: float) -> str | None:
    for key, (x0, y0, x1, y1) in REGIONS.items():
        if x0 <= lon <= x1 and y0 <= lat <= y1:
            return key
    return None


@dataclass
class TagStats:
    """Counts objects and (optionally) length, and tag presence on each."""

    count: int = 0
    length_km: float = 0.0
    tag_count: Counter = field(default_factory=Counter)
    tag_len: Counter = field(default_factory=Counter)
    values: dict = field(default_factory=lambda: defaultdict(Counter))

    def add(self, tags, keys, length_km: float = 0.0, value_keys=()) -> None:
        self.count += 1
        self.length_km += length_km
        for k in keys:
            if k in tags:
                self.tag_count[k] += 1
                self.tag_len[k] += length_km
        for k in value_keys:
            v = tags.get(k)
            if v is not None:
                self.values[k][v] += 1 if length_km == 0 else length_km

    def to_json(self, keys) -> dict:
        out = {"count": self.count}
        if self.length_km:
            out["length_km"] = round(self.length_km, 1)
        out["tag_pct_by_count"] = {
            k: _pct(self.tag_count[k], self.count) for k in keys
        }
        if self.length_km:
            out["tag_pct_by_length"] = {
                k: _pct(self.tag_len[k], self.length_km) for k in keys
            }
        if self.values:
            out["values"] = {
                k: {v: round(n, 1) for v, n in c.most_common(40)}
                for k, c in self.values.items()
            }
        return out


def _pct(n: float, d: float) -> float:
    return round(100.0 * n / d, 2) if d else 0.0


class CoverageScanner:
    def __init__(self) -> None:
        self.totals = Counter()
        self.max_timestamp: datetime | None = None
        # scope -> key -> TagStats  (scope = "nepal" or a region key)
        self.roads: dict[str, dict[str, TagStats]] = defaultdict(lambda: defaultdict(TagStats))
        self.trails: dict[str, dict[str, TagStats]] = defaultdict(lambda: defaultdict(TagStats))
        self.buildings: dict[str, TagStats] = defaultdict(TagStats)
        self.places: dict[str, dict[str, TagStats]] = defaultdict(lambda: defaultdict(TagStats))
        self.named: dict[str, TagStats] = defaultdict(TagStats)
        self.name_script: dict[str, Counter] = defaultdict(Counter)
        self.building_values: dict[str, Counter] = defaultdict(Counter)
        self.poi: dict[str, Counter] = defaultdict(Counter)  # "key=value" counts per scope
        self.religion = Counter()
        self.peaks = TagStats()
        self.peak_names: list[tuple[str, str, str]] = []
        self.landuse_area_proxy = Counter()
        self.admin_levels: dict[str, list[str]] = defaultdict(list)
        self.route_relations: dict[str, list[str]] = defaultdict(list)
        self.protected: list[str] = []
        self.inferable_surface: dict[str, Counter] = defaultdict(Counter)

    # -- helpers ---------------------------------------------------------
    def _scopes(self, lon: float | None, lat: float | None) -> list[str]:
        scopes = ["nepal"]
        if lon is not None:
            r = _region_of(lon, lat)
            if r:
                scopes.append(r)
        return scopes

    def _stamp(self, obj) -> None:
        ts = obj.timestamp
        if ts and (self.max_timestamp is None or ts > self.max_timestamp):
            self.max_timestamp = ts

    def _common(self, tags, scopes, kind: str) -> None:
        if "name" in tags or "name:ne" in tags or "name:en" in tags:
            for s in scopes:
                self.named[s].add(tags, NAMED_TAGS)
                n = tags.get("name", "")
                if n:
                    script = (
                        "devanagari+latin" if _is_devanagari(n) and _is_latin(n)
                        else "devanagari" if _is_devanagari(n)
                        else "latin" if _is_latin(n) else "other"
                    )
                    self.name_script[s][script] += 1
        for key in (
            "amenity", "tourism", "historic", "heritage", "aeroway", "aerialway",
            "railway", "natural", "waterway", "landuse", "leisure", "man_made",
            "sport", "shop", "boundary", "office", "craft", "emergency",
        ):
            v = tags.get(key)
            if v is None:
                continue
            if key in ("shop", "office", "craft"):
                v = "*"
            for s in scopes:
                self.poi[s][f"{key}={v}"] += 1
        if tags.get("amenity") == "place_of_worship" or tags.get("building") in (
            "temple", "monastery", "stupa", "shrine", "mosque", "church", "chapel"
        ):
            self.religion[tags.get("religion", "(none)")] += 1
        place = tags.get("place")
        if place:
            for s in scopes:
                self.places[s][place].add(tags, PLACE_TAGS)

    # -- osmium callbacks -----------------------------------------------
    def node(self, n) -> None:
        self.totals["tagged_nodes"] += 1
        self._stamp(n)
        tags = n.tags
        loc = n.location
        lon, lat = (loc.lon, loc.lat) if loc.valid() else (None, None)
        scopes = self._scopes(lon, lat)
        self._common(tags, scopes, "node")
        if tags.get("natural") == "peak":
            self.peaks.add(tags, ["ele", "name", "name:ne", "name:en", "wikidata", "prominence"])
            if "name" in tags or "name:en" in tags:
                self.peak_names.append((tags.get("name:en") or tags.get("name"), tags.get("ele", ""), tags.get("name:ne", "")))
        if "building" in tags:
            for s in scopes:
                self.buildings[s].add(tags, BUILDING_TAGS)
                self.building_values[s][tags["building"]] += 1
            self.totals["building_nodes"] += 1

    def way(self, w) -> None:
        self.totals["tagged_ways"] += 1
        self._stamp(w)
        tags = w.tags
        first = None
        try:
            if len(w.nodes):
                loc = w.nodes[0].location
                if loc.valid():
                    first = (loc.lon, loc.lat)
        except osmium.InvalidLocationError:
            first = None
        scopes = self._scopes(*(first or (None, None)))
        self._common(tags, scopes, "way")

        hw = tags.get("highway")
        if hw:
            try:
                km = osmium.geom.haversine_distance(w.nodes) / 1000.0
            except osmium.InvalidLocationError:
                km = 0.0
            if hw in ROAD_CLASSES:
                for s in scopes:
                    self.roads[s][hw].add(tags, ROAD_TAGS, km, value_keys=("surface", "tracktype", "smoothness"))
                    self.roads[s]["_all"].add(tags, ROAD_TAGS, km, value_keys=("surface",))
                    if "surface" not in tags:
                        self.inferable_surface[s][hw] += km
            elif hw in TRAIL_CLASSES:
                for s in scopes:
                    self.trails[s][hw].add(tags, TRAIL_TAGS, km, value_keys=("sac_scale", "trail_visibility", "surface"))
                    self.trails[s]["_all"].add(tags, TRAIL_TAGS, km)
            else:
                for s in scopes:
                    self.poi[s][f"highway={hw}"] += 1
        if "building" in tags:
            for s in scopes:
                self.buildings[s].add(tags, BUILDING_TAGS)
                self.building_values[s][tags["building"]] += 1
        lu = tags.get("landuse") or tags.get("natural")
        if lu and w.is_closed():
            self.landuse_area_proxy[lu] += 1

    def relation(self, r) -> None:
        self.totals["tagged_relations"] += 1
        self._stamp(r)
        tags = r.tags
        scopes = ["nepal"]
        self._common(tags, scopes, "relation")
        t = tags.get("type")
        if "building" in tags:
            self.buildings["nepal"].add(tags, BUILDING_TAGS)
            self.building_values["nepal"][tags["building"]] += 1
        if t == "route":
            route = tags.get("route", "?")
            self.route_relations[route].append(tags.get("name:en") or tags.get("name") or tags.get("ref") or f"r{r.id}")
        if tags.get("boundary") == "administrative":
            lvl = tags.get("admin_level", "?")
            self.admin_levels[lvl].append(tags.get("name:en") or tags.get("name") or f"r{r.id}")
        if tags.get("boundary") in ("protected_area", "national_park") or tags.get("leisure") == "nature_reserve":
            self.protected.append(f"{tags.get('name:en') or tags.get('name') or r.id} ({tags.get('boundary') or tags.get('leisure')}, protect_class={tags.get('protect_class', '-')})")

    # -- output ------------------------------------------------------------
    def to_json(self, source: str, elapsed: float) -> dict:
        scopes = ["nepal"] + list(REGIONS)
        out: dict = {
            "generated_at": datetime.now(timezone.utc).isoformat(timespec="seconds"),
            "source": source,
            "data_max_timestamp": self.max_timestamp.isoformat() if self.max_timestamp else None,
            "scan_seconds": round(elapsed, 1),
            "totals": dict(self.totals),
            "regions": {k: list(v) for k, v in REGIONS.items()},
            "scopes": {},
        }
        for s in scopes:
            out["scopes"][s] = {
                "roads": {hw: st.to_json(ROAD_TAGS) for hw, st in sorted(self.roads[s].items(), key=lambda kv: -kv[1].length_km)},
                "trails": {hw: st.to_json(TRAIL_TAGS) for hw, st in sorted(self.trails[s].items(), key=lambda kv: -kv[1].length_km)},
                "buildings": self.buildings[s].to_json(BUILDING_TAGS),
                "building_values": dict(self.building_values[s].most_common(40)),
                "places": {p: st.to_json(PLACE_TAGS) for p, st in sorted(self.places[s].items(), key=lambda kv: -kv[1].count)},
                "named_features": self.named[s].to_json(NAMED_TAGS),
                "name_script": dict(self.name_script[s]),
                "poi_top": dict(self.poi[s].most_common(250)),
                "unsurfaced_road_km_by_class": {k: round(v, 1) for k, v in self.inferable_surface[s].most_common()},
            }
        out["religion"] = dict(self.religion.most_common())
        out["peaks"] = self.peaks.to_json(["ele", "name", "name:ne", "name:en", "wikidata", "prominence"])
        out["peaks_sample"] = sorted(
            ((n, e, ne) for n, e, ne in self.peak_names if e),
            key=lambda t: -_to_float(t[1]),
        )[:40]
        out["closed_landcover_ways"] = dict(self.landuse_area_proxy.most_common(40))
        out["admin_levels"] = {k: {"count": len(v), "examples": sorted(v)[:12]} for k, v in sorted(self.admin_levels.items())}
        out["route_relations"] = {k: {"count": len(v), "examples": sorted(set(v))[:40]} for k, v in sorted(self.route_relations.items(), key=lambda kv: -len(kv[1]))}
        out["protected_areas"] = sorted(set(self.protected))
        return out


def _to_float(s: str) -> float:
    try:
        return float(str(s).replace("m", "").replace(",", "").strip())
    except ValueError:
        return -math.inf


def scan(pbf: Path) -> tuple[CoverageScanner, float]:
    sc = CoverageScanner()
    t0 = time.time()
    fp = (
        osmium.FileProcessor(str(pbf))
        .with_locations()
        .with_filter(osmium.filter.EmptyTagFilter())
    )
    for obj in fp:
        if obj.is_node():
            sc.node(obj)
        elif obj.is_way():
            sc.way(obj)
        elif obj.is_relation():
            sc.relation(obj)
    return sc, time.time() - t0


# --------------------------------------------------------------------------
# Markdown rendering
# --------------------------------------------------------------------------
def _table(headers: list[str], rows: list[list]) -> str:
    lines = ["| " + " | ".join(headers) + " |", "|" + "|".join("---" for _ in headers) + "|"]
    for r in rows:
        lines.append("| " + " | ".join(str(c) for c in r) + " |")
    return "\n".join(lines)


def render_markdown(d: dict) -> str:
    md: list[str] = []
    md.append("# OSM tag coverage report: Nepal\n")
    md.append(
        "> Auto-generated by `pipeline/ghumante_pipeline/coverage.py`. Do not hand-edit; "
        "see `TAG_COVERAGE_FINDINGS.md` for interpretation.\n"
    )
    md.append(f"- Source: `{d['source']}`")
    md.append(f"- Newest object timestamp in extract: `{d['data_max_timestamp']}`")
    md.append(f"- Generated: `{d['generated_at']}` (scan took {d['scan_seconds']} s)")
    md.append(f"- Tagged objects scanned: {d['totals']}\n")
    md.append("Percentages are of objects in that row; *by length* weights roads by kilometres. "
              "Regional rows attribute a way to the region containing its first node "
              "(bounding boxes in the JSON).\n")

    nepal = d["scopes"]["nepal"]
    # Roads
    md.append("## 1. Roads (`highway=*` drivable classes)\n")
    rows = []
    for hw, st in nepal["roads"].items():
        p = st.get("tag_pct_by_length", {})
        rows.append([hw, st["count"], st.get("length_km", 0), p.get("surface", 0), p.get("tracktype", 0),
                     p.get("smoothness", 0), p.get("width", 0), p.get("lanes", 0), p.get("name", 0),
                     p.get("name:ne", 0), p.get("ref", 0), p.get("bridge", 0)])
    md.append(_table(["highway", "ways", "km", "surface %len", "tracktype", "smoothness", "width", "lanes",
                      "name", "name:ne", "ref", "bridge"], rows))
    md.append("\n### Surface values (Nepal, km)\n")
    sv = nepal["roads"].get("_all", {}).get("values", {}).get("surface", {})
    md.append(_table(["surface", "km"], [[k, v] for k, v in sv.items()]))
    md.append("\n### Road surface coverage by region (by length)\n")
    rows = []
    for s in d["scopes"]:
        a = d["scopes"][s]["roads"].get("_all")
        if not a:
            continue
        p = a.get("tag_pct_by_length", {})
        rows.append([s, a["count"], a.get("length_km", 0), p.get("surface", 0), p.get("smoothness", 0),
                     p.get("width", 0), p.get("lanes", 0), p.get("name", 0), p.get("name:ne", 0)])
    md.append(_table(["scope", "ways", "km", "surface", "smoothness", "width", "lanes", "name", "name:ne"], rows))
    md.append("\n### Untagged-surface road km by class (Nepal) - what inference must cover\n")
    md.append(_table(["highway", "km without surface"], [[k, v] for k, v in nepal["unsurfaced_road_km_by_class"].items()]))

    # Trails
    md.append("\n## 2. Trails\n")
    rows = []
    for hw, st in nepal["trails"].items():
        p = st.get("tag_pct_by_length", {})
        rows.append([hw, st["count"], st.get("length_km", 0), p.get("surface", 0), p.get("sac_scale", 0),
                     p.get("trail_visibility", 0), p.get("name", 0)])
    md.append(_table(["highway", "ways", "km", "surface", "sac_scale", "trail_visibility", "name"], rows))
    for hw in ("path", "footway"):
        v = nepal["trails"].get(hw, {}).get("values", {}).get("sac_scale")
        if v:
            md.append(f"\n`sac_scale` on `{hw}` (km): " + ", ".join(f"{k}: {n}" for k, n in v.items()))
    md.append("\n### Route relations\n")
    rows = [[k, v["count"], "; ".join(v["examples"][:15])] for k, v in d["route_relations"].items()]
    md.append(_table(["route", "count", "examples"], rows))

    # Buildings
    md.append("\n## 3. Buildings\n")
    rows = []
    for s in d["scopes"]:
        b = d["scopes"][s]["buildings"]
        p = b["tag_pct_by_count"]
        rows.append([s, b["count"], p.get("building:levels", 0), p.get("height", 0), p.get("roof:shape", 0),
                     p.get("roof:material", 0), p.get("building:material", 0), p.get("name", 0)])
    md.append(_table(["scope", "buildings", "levels %", "height %", "roof:shape %", "roof:material %",
                      "building:material %", "name %"], rows))
    md.append("\n### `building=*` values (Nepal)\n")
    md.append(_table(["value", "count"], [[k, v] for k, v in nepal["building_values"].items()]))

    # Places
    md.append("\n## 4. Places\n")
    rows = []
    for p_, st in nepal["places"].items():
        p = st["tag_pct_by_count"]
        rows.append([p_, st["count"], p.get("name", 0), p.get("name:ne", 0), p.get("name:en", 0),
                     p.get("population", 0), p.get("ele", 0), p.get("wikidata", 0)])
    md.append(_table(["place", "count", "name %", "name:ne %", "name:en %", "population %", "ele %", "wikidata %"], rows))
    md.append("\n### Script of the primary `name` tag\n")
    rows = []
    for s in d["scopes"]:
        ns = d["scopes"][s]["name_script"]
        tot = sum(ns.values()) or 1
        rows.append([s, tot] + [round(100 * ns.get(k, 0) / tot, 1) for k in ("latin", "devanagari", "devanagari+latin", "other")])
    md.append(_table(["scope", "named features", "latin %", "devanagari %", "mixed %", "other %"], rows))
    md.append("\n### Named features: alternate-name coverage\n")
    rows = []
    for s in d["scopes"]:
        nf = d["scopes"][s]["named_features"]
        p = nf["tag_pct_by_count"]
        rows.append([s, nf["count"], p.get("name", 0), p.get("name:ne", 0), p.get("name:en", 0), p.get("int_name", 0),
                     p.get("alt_name", 0), p.get("wikidata", 0)])
    md.append(_table(["scope", "features", "name", "name:ne", "name:en", "int_name", "alt_name", "wikidata"], rows))

    # Religion / heritage / nature / transport / tourism
    md.append("\n## 5. Places of worship by `religion`\n")
    md.append(_table(["religion", "count"], [[k, v] for k, v in d["religion"].items()]))
    md.append("\n## 6. Peaks\n")
    pk = d["peaks"]
    md.append(f"{pk['count']} `natural=peak` nodes; tag coverage: " + ", ".join(f"{k} {v}%" for k, v in pk["tag_pct_by_count"].items()))
    md.append("\nHighest named peaks in the extract:\n")
    md.append(_table(["name", "ele", "name:ne"], d["peaks_sample"][:25]))

    def poi_rows(prefixes):
        rows = []
        for k, v in nepal["poi_top"].items():
            if any(k.startswith(pf) for pf in prefixes):
                kv = d["scopes"]["kathmandu_valley"]["poi_top"].get(k, 0)
                rows.append([f"`{k}`", v, kv])
        return rows

    md.append("\n## 7. Nature, water and land cover features\n")
    md.append(_table(["tag", "Nepal", "Kathmandu Valley"], poi_rows(("natural=", "waterway=", "landuse=", "leisure=", "boundary="))))
    md.append("\n## 8. Transport\n")
    md.append(_table(["tag", "Nepal", "Kathmandu Valley"], poi_rows(("aeroway=", "aerialway=", "railway=", "highway=", "amenity=fuel", "amenity=bus_station"))))
    md.append("\n## 9. Heritage, tourism and amenities\n")
    md.append(_table(["tag", "Nepal", "Kathmandu Valley"], poi_rows(("historic=", "heritage=", "tourism=", "man_made=", "amenity=", "sport=", "shop=", "office="))))

    md.append("\n## 10. Administrative boundaries (relations)\n")
    rows = [[k, v["count"], "; ".join(v["examples"])] for k, v in d["admin_levels"].items()]
    md.append(_table(["admin_level", "relations", "examples"], rows))
    md.append("\n## 11. Protected areas\n")
    md.append("\n".join(f"- {p}" for p in d["protected_areas"][:80]))
    md.append("")
    return "\n".join(md)


def main(argv=None) -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--pbf", type=Path, required=True)
    ap.add_argument("--out", type=Path, required=True, help="output directory")
    ap.add_argument("--source-label", default=None, help="label recorded in the report (defaults to file name)")
    args = ap.parse_args(argv)
    sc, elapsed = scan(args.pbf)
    data = sc.to_json(args.source_label or args.pbf.name, elapsed)
    args.out.mkdir(parents=True, exist_ok=True)
    (args.out / "tag_coverage.json").write_text(json.dumps(data, ensure_ascii=False, indent=1), encoding="utf-8")
    (args.out / "tag_coverage.md").write_text(render_markdown(data), encoding="utf-8")
    print(f"scanned in {elapsed:.0f}s -> {args.out}")


if __name__ == "__main__":
    main()
