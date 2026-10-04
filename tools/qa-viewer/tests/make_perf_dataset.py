#!/usr/bin/env python3
"""Write a Kathmandu-valley-sized synthetic QA export for performance checks.

    python3 tools/qa-viewer/tests/make_perf_dataset.py [--buildings 500000] [--out tools/qa-viewer/perf-data/qa]

The output (~200 MB, gitignored) mimics the real region's volume: ~500k
buildings clustered around the valley's towns, ~120k road pieces, ~20k POIs,
~3k areas and the full leaf-tile list (~1 300 level-10 tiles). Geometry is
random; only sizes and property shapes matter. Used by tests/perf.cjs.
"""

from __future__ import annotations

import argparse
import json
import math
import random
from pathlib import Path

try:
    from pyproj import Transformer
except ImportError:  # tile corners are optional for the perf test
    Transformer = None

BBOX = (85.18, 27.55, 85.58, 27.83)  # kathmandu_valley in pipeline/config/regions.yaml
CENTRES = [(85.315, 27.705, 0.025, 0.45), (85.325, 27.672, 0.018, 0.2), (85.428, 27.672, 0.012, 0.12),
           (85.280, 27.680, 0.010, 0.08), (85.360, 27.720, 0.030, 0.15)]  # lon, lat, sigma, weight
ARCHES = ["GENERIC"] * 6 + ["MODERN_URBAN"] * 6 + ["NEWAR"] * 2 + ["HILL_VILLAGE", "INSTITUTIONAL", "INDUSTRIAL"]
CLASSES = (["RESIDENTIAL"] * 50 + ["SERVICE"] * 15 + ["TRACK"] * 10 + ["UNCLASSIFIED"] * 8 + ["TERTIARY"] * 6
           + ["SECONDARY"] * 4 + ["PRIMARY"] * 3 + ["TRUNK"] * 1 + ["LIVING_STREET"] * 3)
TRAILS = ["FOOTWAY", "PATH", "STEPS", "PEDESTRIAN"]
SURFACES = ["ASPHALT", "CONCRETE", "BRICK", "COBBLE", "GRAVEL", "COMPACTED", "DIRT", "MUD"]
SOURCES = ["TAGGED"] * 15 + ["DERIVED"] * 3 + ["INFERRED"] * 70 + ["DEFAULT"] * 12
POI_KINDS = ["TEMPLE_HINDU", "STUPA", "SHRINE", "HOTEL", "GUEST_HOUSE", "RESTAURANT", "CAFE", "SHOP", "SCHOOL",
             "BANK", "BUS_STATION", "VIEWPOINT", "PARKING", "MUSEUM"]
AREA_KINDS = ["RESIDENTIAL", "FARMLAND", "FOREST", "PARK", "WATER_POND", "RELIGIOUS", "COMMERCIAL"]
M_LAT = 1 / 111_320.0


def rnd_point(rng: random.Random) -> tuple[float, float]:
    r = rng.random() * sum(c[3] for c in CENTRES)
    for lon, lat, sig, w in CENTRES:
        r -= w
        if r <= 0:
            break
    while True:
        x, y = rng.gauss(lon, sig), rng.gauss(lat, sig * 0.8)
        if BBOX[0] < x < BBOX[2] and BBOX[1] < y < BBOX[3]:
            return x, y


def rect(lon, lat, w, d, a):
    m_lon = M_LAT / math.cos(math.radians(lat))
    c, s = math.cos(a), math.sin(a)
    pts = [(-w / 2, -d / 2), (w / 2, -d / 2), (w / 2, d / 2), (-w / 2, d / 2), (-w / 2, -d / 2)]
    return [[round(lon + (x * c - y * s) * m_lon, 7), round(lat + (x * s + y * c) * M_LAT, 7)] for x, y in pts]


def polyline(rng, lon, lat, n, step_m):
    m_lon = M_LAT / math.cos(math.radians(lat))
    a = rng.uniform(0, 2 * math.pi)
    pts = [[round(lon, 7), round(lat, 7)]]
    for _ in range(n - 1):
        a += rng.uniform(-0.5, 0.5)
        lon += math.cos(a) * step_m * m_lon
        lat += math.sin(a) * step_m * M_LAT
        pts.append([round(lon, 7), round(lat, 7)])
    return pts


def write_fc(path: Path, feats) -> int:
    n = 0
    with path.open("w", encoding="utf-8") as fh:
        fh.write('{"type":"FeatureCollection","features":[\n')
        for f in feats:
            if n:
                fh.write(",\n")
            fh.write(json.dumps(f, separators=(",", ":")))
            n += 1
        fh.write("\n]}\n")
    return n


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--buildings", type=int, default=500_000)
    ap.add_argument("--roads", type=int, default=120_000)
    ap.add_argument("--out", type=Path, default=Path(__file__).resolve().parents[1] / "perf-data" / "qa")
    args = ap.parse_args()
    out = args.out
    out.mkdir(parents=True, exist_ok=True)
    rng = random.Random(7)

    def buildings():
        for i in range(args.buildings):
            lon, lat = rnd_point(rng)
            arche = rng.choice(ARCHES)
            yield {"type": "Feature", "properties": {
                "osm_ref": (2_000_000_000 + i) << 1, "archetype": arche, "use": "HOUSE", "levels": rng.randint(1, 6),
                "flags": 1 if rng.random() < 0.996 else 0, "height_cm": 0, "min_height_cm": 0, "roof_shape": "FLAT",
                "roof_material": "CONCRETE", "wall_material": "PLASTER", "seed": rng.getrandbits(32), "tile": "10/0/0"},
                "geometry": {"type": "Polygon", "coordinates": [rect(lon, lat, rng.uniform(7, 14), rng.uniform(7, 12),
                                                                     rng.uniform(0, 1.5))]}}

    def roads(trails: bool):
        n = args.roads // (6 if trails else 1)
        for i in range(n):
            lon, lat = rnd_point(rng)
            cls = rng.choice(TRAILS if trails else CLASSES)
            yield {"type": "Feature", "properties": {
                "osm_way_id": 900_000_000 + i + (10_000_000 if trails else 0), "road_class": cls,
                "surface": rng.choice(SURFACES), "surface_source": rng.choice(SOURCES), "flags": 0, "lanes": 0,
                "sac_scale": "HIKING" if trails and rng.random() < 0.3 else "UNKNOWN", "width_cm": 0, "access": 127,
                "name": f"Marg {i}" if i % 7 == 0 else None, "tile": "10/0/0"},
                "geometry": {"type": "LineString", "coordinates": polyline(rng, lon, lat, rng.randint(2, 6), rng.uniform(20, 80))}}

    def pois():
        for i in range(20_000):
            lon, lat = rnd_point(rng)
            yield {"type": "Feature", "properties": {"osm_ref": (3_000_000_000 + i) << 2, "kind": rng.choice(POI_KINDS),
                                                     "flags": 1, "importance": rng.randint(0, 255), "name": f"Poi {i}"},
                   "geometry": {"type": "Point", "coordinates": [round(lon, 7), round(lat, 7)]}}

    def areas():
        for i in range(3_000):
            lon, lat = rnd_point(rng)
            ring = polyline(rng, lon, lat, 8, rng.uniform(30, 150))
            ring.append(ring[0])
            yield {"type": "Feature", "properties": {"osm_ref": (4_000_000_000 + i) << 1, "kind": rng.choice(AREA_KINDS), "flags": 0},
                   "geometry": {"type": "Polygon", "coordinates": [ring]}}

    counts = {
        "buildings": write_fc(out / "buildings.geojson", buildings()),
        "roads": write_fc(out / "roads.geojson", roads(False)),
        "trails": write_fc(out / "trails.geojson", roads(True)),
        "pois": write_fc(out / "pois.geojson", pois()),
        "areas": write_fc(out / "areas.geojson", areas()),
        "lines": write_fc(out / "lines.geojson", []),
    }
    tiles = []
    if Transformer is not None:
        tm = "+proj=tmerc +lat_0=0 +lon_0=84 +k=0.9996 +x_0=500000 +y_0=0 +ellps=WGS84 +units=m +no_defs"
        fwd = Transformer.from_crs("EPSG:4326", tm, always_xy=True)
        xs, zs = zip(*[fwd.transform(lo, la) for lo in (BBOX[0], BBOX[2]) for la in (BBOX[1], BBOX[3])])
        s = 1024
        for tx in range(int((min(xs) - 100_000) // s), int((max(xs) - 100_000) // s) + 1):
            for ty in range(int((min(zs) - 2_900_000) // s), int((max(zs) - 2_900_000) // s) + 1):
                tiles.append({"level": 10, "tx": tx, "ty": ty})  # corners: computed by the viewer
    index = {"format": "ghumante-qa-index", "version": 1, "region": "perf_synthetic", "synthetic": True,
             "scale_model": "identity", "bbox_lonlat": list(BBOX), "leaf_level": 10, "tiles": tiles,
             "layers": {k: {"path": f"{k}.geojson", "count": v} for k, v in counts.items()},
             "stats": {"note": "random geometry for performance tests"}}
    (out / "index.json").write_text(json.dumps(index), encoding="utf-8")
    sizes = {p.name: round(p.stat().st_size / 1e6, 1) for p in out.glob("*.geojson")}
    print(json.dumps({"out": str(out), "counts": counts, "tiles": len(tiles), "MB": sizes}))


if __name__ == "__main__":
    main()
