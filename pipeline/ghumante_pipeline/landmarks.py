"""Resolve hero landmarks (config/landmarks.yaml) to concrete OSM objects.

The brief asks us to place hero landmarks exactly where they are, so before an
artist models one we check that OSM has it, where, and with which tags.

Usage:
    python -m ghumante_pipeline.landmarks --pbf data/raw/osm/nepal.osm.pbf \
        --config config/landmarks.yaml --json config/landmarks.resolved.json \
        --md ../docs/reports/landmarks.md
"""

from __future__ import annotations

import argparse
import json
import math
import re
from pathlib import Path

import osmium
import yaml

NAME_KEYS = ("name", "name:en", "name:ne", "alt_name", "int_name", "old_name", "official_name", "alt_name:en")


def _haversine_km(lon1, lat1, lon2, lat2) -> float:
    r = 6371.0088
    p1, p2 = math.radians(lat1), math.radians(lat2)
    dp, dl = p2 - p1, math.radians(lon2 - lon1)
    a = math.sin(dp / 2) ** 2 + math.cos(p1) * math.cos(p2) * math.sin(dl / 2) ** 2
    return 2 * r * math.asin(math.sqrt(a))


def _tag_match(tags: dict, flt: str) -> bool:
    k, _, v = flt.partition("=")
    if k not in tags:
        return False
    return v in ("*", "") or tags[k] == v


def load_config(path: Path) -> list[dict]:
    data = yaml.safe_load(path.read_text(encoding="utf-8"))
    out = []
    for lm in data["landmarks"]:
        lm = dict(lm)
        lm["_re"] = re.compile(lm["match"], re.IGNORECASE)
        out.append(lm)
    return out


def scan(pbf: Path, landmarks: list[dict]) -> dict[str, list[dict]]:
    combined = re.compile("|".join(f"(?:{lm['match']})" for lm in landmarks), re.IGNORECASE)
    candidates: list[dict] = []
    rel_members: dict[int, list[int]] = {}

    fp = osmium.FileProcessor(str(pbf)).with_locations().with_filter(osmium.filter.KeyFilter(*NAME_KEYS))
    for obj in fp:
        tags = dict(obj.tags)
        names = [tags[k] for k in NAME_KEYS if k in tags]
        if not any(combined.search(n) for n in names):
            continue
        cand = {"type": "n" if obj.is_node() else "w" if obj.is_way() else "r", "id": obj.id, "tags": tags, "lon": None, "lat": None}
        if obj.is_node():
            cand["lon"], cand["lat"] = obj.location.lon, obj.location.lat
        elif obj.is_way():
            pts = [(n.location.lon, n.location.lat) for n in obj.nodes if n.location.valid()]
            if pts:
                cand["lon"] = sum(p[0] for p in pts) / len(pts)
                cand["lat"] = sum(p[1] for p in pts) / len(pts)
        else:
            rel_members[obj.id] = [m.ref for m in obj.members if m.type == "w"][:200]
        candidates.append(cand)

    # Second pass: approximate relation centroids from member way nodes.
    if rel_members:
        want = {w for ws in rel_members.values() for w in ws}
        sums: dict[int, tuple[float, float, int]] = {}
        fp2 = osmium.FileProcessor(str(pbf), osmium.osm.NODE | osmium.osm.WAY).with_locations()
        for obj in fp2:
            if obj.is_way() and obj.id in want:
                pts = [(n.location.lon, n.location.lat) for n in obj.nodes if n.location.valid()]
                if pts:
                    sums[obj.id] = (sum(p[0] for p in pts), sum(p[1] for p in pts), len(pts))
        for c in candidates:
            if c["type"] == "r":
                acc = [sums[w] for w in rel_members.get(c["id"], []) if w in sums]
                n = sum(a[2] for a in acc)
                if n:
                    c["lon"] = sum(a[0] for a in acc) / n
                    c["lat"] = sum(a[1] for a in acc) / n

    by_lm: dict[str, list[dict]] = {}
    for lm in landmarks:
        lon0, lat0, rad = lm["near"]
        hits = []
        for c in candidates:
            names = [c["tags"][k] for k in NAME_KEYS if k in c["tags"]]
            if not any(lm["_re"].search(n) for n in names):
                continue
            if c["lon"] is None:
                continue
            d = _haversine_km(lon0, lat0, c["lon"], c["lat"])
            if d > rad:
                continue
            pref_rank = next((i for i, f in enumerate(lm["prefer"]) if _tag_match(c["tags"], f)), len(lm["prefer"]))
            score = (len(lm["prefer"]) - pref_rank) * 10 + (3 if "wikidata" in c["tags"] else 0) + (2 if c["type"] != "n" else 0) - d / max(rad, 1e-6)
            hits.append({**c, "dist_km": round(d, 2), "score": round(score, 2), "pref_rank": pref_rank})
        hits.sort(key=lambda h: -h["score"])
        by_lm[lm["id"]] = hits
    return by_lm


def summarize(landmarks: list[dict], by_lm: dict[str, list[dict]]) -> list[dict]:
    rows = []
    for lm in landmarks:
        hits = by_lm.get(lm["id"], [])
        best = hits[0] if hits else None
        status = "missing"
        if best:
            status = "found" if best["pref_rank"] < len(lm["prefer"]) else "weak"
        rows.append({
            "id": lm["id"], "region": lm["region"], "kind": lm["kind"], "status": status,
            "osm": f"{best['type']}{best['id']}" if best else None,
            "lon": round(best["lon"], 6) if best else None, "lat": round(best["lat"], 6) if best else None,
            "name": best["tags"].get("name") if best else None,
            "name_en": best["tags"].get("name:en") if best else None,
            "name_ne": best["tags"].get("name:ne") if best else None,
            "key_tags": {k: v for k, v in (best["tags"].items() if best else []) if k not in NAME_KEYS and not k.startswith(("name:", "source", "addr:"))},
            "alternatives": [f"{h['type']}{h['id']} {h['tags'].get('name')} ({h['dist_km']} km)" for h in hits[1:5]],
        })
    return rows


def render_md(rows: list[dict]) -> str:
    out = ["# Hero landmark verification against OSM\n",
           "> Auto-generated by `pipeline/ghumante_pipeline/landmarks.py` from `pipeline/config/landmarks.yaml`.\n",
           "Status: **found** = an object with the expected tags exists near the expected location; "
           "**weak** = only a name match without the expected tags (verify by hand); **missing** = nothing in OSM within the search radius.\n"]
    counts = {s: sum(1 for r in rows if r["status"] == s) for s in ("found", "weak", "missing")}
    out.append(f"Summary: {counts['found']} found, {counts['weak']} weak, {counts['missing']} missing (of {len(rows)}).\n")
    out.append("| landmark | region | status | OSM | name | name:ne | lon, lat | key tags |")
    out.append("|---|---|---|---|---|---|---|---|")
    for r in rows:
        osm = r["osm"]
        link = f"[{osm}](https://www.openstreetmap.org/{ {'n':'node','w':'way','r':'relation'}[osm[0]] }/{osm[1:]})" if osm else "-"
        kt = ", ".join(f"{k}={v}" for k, v in list(r["key_tags"].items())[:5]).replace("|", "/")
        ll = f"{r['lon']}, {r['lat']}" if r["lon"] is not None else "-"
        out.append(f"| {r['id']} | {r['region']} | {r['status']} | {link} | {r['name_en'] or r['name'] or '-'} | {r['name_ne'] or '-'} | {ll} | {kt} |")
    out.append("")
    return "\n".join(out)


def main(argv=None) -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--pbf", type=Path, required=True)
    ap.add_argument("--config", type=Path, default=Path(__file__).resolve().parents[1] / "config" / "landmarks.yaml")
    ap.add_argument("--json", type=Path, required=True)
    ap.add_argument("--md", type=Path, required=True)
    args = ap.parse_args(argv)
    lms = load_config(args.config)
    by_lm = scan(args.pbf, lms)
    rows = summarize(lms, by_lm)
    args.json.write_text(json.dumps(rows, ensure_ascii=False, indent=1), encoding="utf-8")
    args.md.parent.mkdir(parents=True, exist_ok=True)
    args.md.write_text(render_md(rows), encoding="utf-8")
    print(render_md(rows))


if __name__ == "__main__":
    main()
