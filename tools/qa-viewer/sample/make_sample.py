#!/usr/bin/env python3
"""Generate the synthetic QA sample dataset in ``tools/qa-viewer/sample/qa/``.

The sample follows docs/DATA_FORMATS.md section 5 (``index.json``,
``<layer>.geojson``, ``hillshade/<L>/<tx>_<ty>.png``, ``biome/<L>/<tx>_<ty>.png``)
so the QA viewer can be demoed and tested without a pipeline build. It covers
two leaf tiles (level 10, 1 024 m) around Thamel, Kathmandu (85.31 E, 27.715 N),
plus their level-9 parent.

Everything is SYNTHETIC: the streets, buildings and POIs are invented to
exercise every enum value and colour in the viewer. They are not OpenStreetMap
data, and the OSM ids are fake. Features are built in game metres
(NPL-TM84 minus the world origin, identity warp), clipped per tile the way the
pack writer clips them (roads, lines and areas clipped; buildings assigned to
the tile containing their centroid), then unprojected to lon/lat, so the files
look like what ``qa_export.py`` decodes from a real pack.

Output is deterministic (fixed seed, no timestamps). Requires pyproj and
shapely (both pipeline dependencies); PNGs are written with the stdlib only.

    python3 tools/qa-viewer/sample/make_sample.py
"""

from __future__ import annotations

import argparse
import json
import math
import random
import shutil
import struct
import zlib
from pathlib import Path

from pyproj import Transformer
from shapely.geometry import LineString, MultiLineString, MultiPolygon, Point, Polygon, box
from shapely.geometry.polygon import orient

HERE = Path(__file__).resolve().parent
OUT = HERE / "qa"  # overridden by --out

# --- frames (mirror pipeline/ghumante_pipeline/projection.py) ---------------
TM84 = "+proj=tmerc +lat_0=0 +lon_0=84 +k=0.9996 +x_0=500000 +y_0=0 +ellps=WGS84 +units=m +no_defs"
ORIGIN_E, ORIGIN_N = 100_000.0, 2_900_000.0
_INV = Transformer.from_crs(TM84, "EPSG:4326", always_xy=True)


def g2ll(x: float, z: float) -> list[float]:
    lon, lat = _INV.transform(x + ORIGIN_E, z + ORIGIN_N)
    return [round(lon, 7), round(lat, 7)]


def tile_size(level: int) -> int:
    return 1 << (20 - level)


def morton2(tx: int, ty: int) -> int:
    out = 0
    for i in range(29):
        out |= ((tx >> i) & 1) << (2 * i)
        out |= ((ty >> i) & 1) << (2 * i + 1)
    return out


def tile_key(level: int, tx: int, ty: int) -> int:
    return (level << 58) | morton2(tx, ty)


# --- region -------------------------------------------------------------------
REGION = "sample_thamel"
LEAF = 10
S = tile_size(LEAF)  # 1024 m
LEAF_TILES = [(516, 162), (517, 162)]
PARENT = (9, 258, 81)
X0, Z0 = 516 * S, 162 * S  # region south-west corner in game metres
X1, Z1 = X0 + 2 * S, Z0 + S
REGION_BOX = box(X0, Z0, X1, Z1)
HEIGHT_N, BIOME_N = 129, 65

rng = random.Random(20261004)

# enum values needed for flags (shared/enums.json)
RF = {"ONEWAY": 1, "BRIDGE": 2, "TUNNEL": 4, "HAS_PREV_CTX": 8, "HAS_NEXT_CTX": 16, "LINK": 32, "FORD": 64,
      "SAC_INFERRED": 128}
BF = {"LEVELS_INFERRED": 1, "HEIGHT_TAGGED": 2, "ROOF_TAGGED": 4, "LANDMARK": 8, "PART": 16}
PF = {"DISCOVERABLE": 1, "LANDMARK": 2, "HAS_ELE": 4, "SACRED": 8}
TRAVEL_ALL, TRAVEL_FOOT, TRAVEL_NO_MOTOR = 127, 1, 1 | 2 | 64
TRAIL_CLASSES = {"PEDESTRIAN", "FOOTWAY", "PATH", "STEPS", "CYCLEWAY", "BRIDLEWAY"}
SURFACE_GROUP = {"ASPHALT": "PAVED", "CONCRETE": "PAVED", "BRICK": "PAVED", "COBBLE": "PAVED", "GRAVEL": "GRAVEL",
                 "COMPACTED": "GRAVEL", "DIRT": "DIRT", "MUD": "MUD", "SAND": "DIRT", "GRASS": "DIRT",
                 "ROCK": "GRAVEL", "SNOW_ICE": "MUD", "WOOD": "PAVED", "METAL": "PAVED", "UNKNOWN": "DIRT"}

BIOME_PALETTE = {
    "NONE": "#000000", "WATER": "#4a90d9", "URBAN_DENSE": "#b0a397", "URBAN_GREEN": "#a9c79a",
    "TERAI_PADDY": "#c8e07a", "TERAI_CROPLAND": "#e2d886", "TERAI_SAL_FOREST": "#3f7f3a",
    "TERAI_GRASSLAND": "#cfe39a", "RIVERBED_GRAVEL": "#d8cfc0", "CHURE_FOREST": "#5b8f4a",
    "HILL_TERRACES": "#d9c46a", "HILL_FOREST": "#2f6b3a", "HILL_SCRUB": "#8fae6b", "HILL_GRASSLAND": "#b9d48a",
    "SUBALPINE_FOREST": "#2c5a4a", "ALPINE_MEADOW": "#a7c98f", "ALPINE_SCRUB": "#8a9f75",
    "SCREE_ROCK": "#9a9590", "MORAINE": "#b7aea2", "GLACIER": "#d6eef8", "SNOW": "#ffffff",
    "TRANS_HIMALAYAN_STEPPE": "#d9b98c", "TRANS_HIMALAYAN_CROPLAND": "#c9a86a", "ORCHARD": "#9cc56b",
    "TEA_GARDEN": "#4f9a4f", "WETLAND": "#7fc6a4", "VALLEY_CROPLAND": "#e6d77a", "BARE_SOIL": "#c9a27e",
}


def L(x: float, z: float) -> tuple[float, float]:
    """Region-local metres -> game metres."""
    return (X0 + x, Z0 + z)


def tile_of(x: float, z: float) -> tuple[int, int]:
    return int(math.floor(x / S)), int(math.floor(z / S))


def tile_box(tx: int, ty: int):
    return box(tx * S, ty * S, (tx + 1) * S, (ty + 1) * S)


def tile_str(tx: int, ty: int) -> str:
    return f"{LEAF}/{tx}/{ty}"


def names(default: str | None = None, ne: str | None = None, en: str | None = None) -> dict:
    if not default and not ne:
        return {}
    out = {"name": default or ne}
    out["name_en"] = en or (default if default and default.isascii() else "")
    out["name_ne"] = ne or ""
    return out


# --- terrain (synthetic DEM used for hillshade + biomes) --------------------------
MOUND = (1700.0, 780.0)  # local metres


def height(x: float, z: float) -> float:
    lx, lz = x - X0, z - Z0
    h = 1328.0 + 0.004 * lx  # gentle eastward rise
    h += 4.0 * math.sin(lx / 170.0) * math.cos(lz / 210.0)
    d2 = (lx - MOUND[0]) ** 2 + (lz - MOUND[1]) ** 2
    h += 55.0 * math.exp(-d2 / (2 * 230.0 ** 2))  # a forested hillock in the east tile
    # the stream channel along the west edge
    cx = 60.0 + 25.0 * math.sin(lz / 120.0)
    h -= 6.0 * math.exp(-((lx - cx) ** 2) / (2 * 25.0 ** 2))
    return h


def hillshade_value(x: float, z: float, step: float) -> int:
    dzdx = (height(x + step, z) - height(x - step, z)) / (2 * step)
    dzdy = (height(x, z + step) - height(x, z - step)) / (2 * step)
    zf = 3.0  # vertical exaggeration so the gentle valley floor shows up
    nx, ny, nz = -zf * dzdx, -zf * dzdy, 1.0  # surface normal (x east, y north, z up)
    norm = math.sqrt(nx * nx + ny * ny + nz * nz)
    az, alt = math.radians(315.0), math.radians(45.0)  # light from the north-west, 45 deg up
    lx, ly, lz = math.sin(az) * math.cos(alt), math.cos(az) * math.cos(alt), math.sin(alt)
    v = (nx * lx + ny * ly + nz * lz) / norm
    return max(0, min(255, int(round(255 * v))))


def biome_at(x: float, z: float) -> str:
    lx, lz = x - X0, z - Z0
    d = math.hypot(lx - MOUND[0], lz - MOUND[1])
    cx = 60.0 + 25.0 * math.sin(lz / 120.0)
    if abs(lx - cx) < 18:
        return "WATER"
    if abs(lx - cx) < 40:
        return "RIVERBED_GRAVEL"
    if 1150 <= lx <= 1270 and 300 <= lz <= 380:
        return "WATER"
    if d < 150:
        return "HILL_FOREST"
    if d < 260:
        return "HILL_TERRACES" if (lx + lz) % 120 < 80 else "HILL_SCRUB"
    if 820 <= lx <= 960 and 380 <= lz <= 470:
        return "URBAN_GREEN"
    if lx < 1000:
        return "URBAN_DENSE"
    if lz < 220:
        return "VALLEY_CROPLAND"
    if lx > 1980:
        return "ORCHARD"
    return "URBAN_GREEN" if lz > 900 else "URBAN_DENSE"


# --- PNG (stdlib) -------------------------------------------------------------
def write_png(path: Path, width: int, height_px: int, rows: list[bytes], color_type: int) -> None:
    def chunk(tag: bytes, data: bytes) -> bytes:
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    raw = b"".join(b"\x00" + r for r in rows)
    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", width, height_px, 8, color_type, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw, 9))
    png += chunk(b"IEND", b"")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(png)


def hex_rgb(h: str) -> bytes:
    return bytes(int(h[i:i + 2], 16) for i in (1, 3, 5))


def write_rasters(tx: int, ty: int) -> None:
    """Vertex-aligned grids (sample (j, i) sits ON the tile edge for i, j = 0 or n-1),
    written north-up: PNG row 0 is the northern edge."""
    x0, z0 = tx * S, ty * S
    step = S / (HEIGHT_N - 1)
    rows = []
    for r in range(HEIGHT_N):
        j = HEIGHT_N - 1 - r
        rows.append(bytes(hillshade_value(x0 + i * step, z0 + j * step, step) for i in range(HEIGHT_N)))
    write_png(OUT / "hillshade" / str(LEAF) / f"{tx}_{ty}.png", HEIGHT_N, HEIGHT_N, rows, 0)

    step = S / (BIOME_N - 1)
    rows = []
    for r in range(BIOME_N):
        j = BIOME_N - 1 - r
        rows.append(b"".join(hex_rgb(BIOME_PALETTE[biome_at(x0 + i * step, z0 + j * step)])
                             for i in range(BIOME_N)))
    write_png(OUT / "biome" / str(LEAF) / f"{tx}_{ty}.png", BIOME_N, BIOME_N, rows, 2)


# --- feature builders -----------------------------------------------------------
roads: list[dict] = []
trails: list[dict] = []
buildings: list[dict] = []
areas: list[dict] = []
lines: list[dict] = []
pois: list[dict] = []
places: list[dict] = []

_next_id = [1_000_001]


def fake_id() -> int:
    _next_id[0] += 1
    return _next_id[0]


def line_geom(g) -> dict:
    if isinstance(g, LineString):
        return {"type": "LineString", "coordinates": [g2ll(x, z) for x, z in g.coords]}
    raise TypeError(g)


def poly_geom(g) -> dict:
    def rings(p: Polygon):
        p = orient(p, 1.0)  # outer CCW, holes CW (in X-east/Z-north, as in BLDG/AREA)
        out = [[g2ll(x, z) for x, z in p.exterior.coords]]
        out += [[g2ll(x, z) for x, z in r.coords] for r in p.interiors]
        return out

    if isinstance(g, Polygon):
        return {"type": "Polygon", "coordinates": rings(g)}
    if isinstance(g, MultiPolygon):
        return {"type": "MultiPolygon", "coordinates": [rings(p) for p in g.geoms]}
    raise TypeError(g)


def clip_lines(coords_local, props: dict, out: list, ctx_flags: tuple[int, int] = (8, 16)) -> None:
    """Clip a polyline per leaf tile (one feature per piece), like the ROAD/LINE writer: a piece cut
    at its start (end) gets the original vertex just outside the tile as a context point and the
    HAS_PREV_CTX (HAS_NEXT_CTX) flag. The viewer must not draw context points."""
    pts = [L(x, z) for x, z in coords_local]
    ls = LineString(pts)
    cum = [0.0]
    for a, b in zip(pts, pts[1:]):
        cum.append(cum[-1] + math.dist(a, b))
    for tx, ty in LEAF_TILES:
        piece = ls.intersection(tile_box(tx, ty))
        if piece.is_empty:
            continue
        parts = list(piece.geoms) if isinstance(piece, MultiLineString) else [piece]
        for p in parts:
            if not isinstance(p, LineString) or p.length < 0.5:
                continue
            coords = list(p.coords)
            flags = props.get("flags", 0)
            d0, d1 = ls.project(Point(coords[0])), ls.project(Point(coords[-1]))
            if d0 > 1e-6:  # cut at its start: previous original vertex is the context point
                k = max(i for i, c in enumerate(cum) if c < d0 - 1e-6)
                coords.insert(0, pts[k])
                flags |= ctx_flags[0]
            if d1 < ls.length - 1e-6:  # cut at its end
                k = min(i for i, c in enumerate(cum) if c > d1 + 1e-6)
                coords.append(pts[k])
                flags |= ctx_flags[1]
            out.append({"type": "Feature", "properties": {**props, "flags": flags, "tile": tile_str(tx, ty)},
                        "geometry": line_geom(LineString(coords))})


def road(cls: str, pts, surface: str, source: str, name=None, ne=None, ref=None, flags=0, lanes=0,
         sac="UNKNOWN", width_cm=0, access=TRAVEL_ALL, layer=0, visibility=0, way_id=None):
    props = {
        "osm_way_id": way_id or fake_id(), "road_class": cls, "surface": surface,
        "surface_group": SURFACE_GROUP[surface], "surface_source": source, "flags": flags, "lanes": lanes,
        "sac_scale": sac, "trail_visibility": visibility, "layer": layer, "width_cm": width_cm,
        "access": access, **names(name, ne), "ref": ref or "",
    }
    clip_lines(pts, props, trails if cls in TRAIL_CLASSES else roads)


def jitter(pts, amp=3.0):
    return [(x + rng.uniform(-amp, amp), z + rng.uniform(-amp, amp)) for x, z in pts]


def build_roads() -> None:
    road("PRIMARY", [(-80, 520), (700, 540), (1400, 515), (2140, 498)], "ASPHALT", "TAGGED",
         "Madhya Marg", "मध्य मार्ग", ref="H-S1", lanes=4, width_cm=1400)
    road("TRUNK", [(1900, -60), (1880, 400), (1925, 1100)], "ASPHALT", "TAGGED", "Chakra Path (sample ring)",
         "चक्र पथ", ref="NH-S2", lanes=4, width_cm=1800)
    road("SECONDARY", [(1000, -60), (1050, 300), (1020, 700), (1100, 1100)], "CONCRETE", "INFERRED",
         "Uttar Marg", "उत्तर मार्ग", lanes=2)
    road("TERTIARY", [(100, -40), (300, 200), (420, 380), (480, 600), (600, 900), (650, 1100)], "BRICK", "TAGGED",
         "Purano Sadak", "पुरानो सडक")

    # Thamel-style lane grid in the west tile; every surface and source appears.
    surfaces = ["ASPHALT", "BRICK", "COBBLE", "CONCRETE", "GRAVEL", "COMPACTED", "DIRT", "MUD", "SAND", "GRASS"]
    sources = ["INFERRED", "INFERRED", "DEFAULT", "TAGGED", "INFERRED", "DERIVED", "DEFAULT", "INFERRED"]
    k = 0
    for x in (150, 270, 390, 630, 750, 870):
        road("RESIDENTIAL", jitter([(x, 565), (x + 8, 700), (x - 4, 860), (x + 5, 1000)]),
             surfaces[k % len(surfaces)], sources[k % len(sources)], f"Gali {k + 1}" if k % 2 == 0 else None)
        k += 1
    for z in (640, 760, 880):
        road("RESIDENTIAL", jitter([(120, z), (400, z + 6), (700, z - 5), (905, z + 3)]),
             surfaces[k % len(surfaces)], sources[k % len(sources)], f"Tole Marg {k}" if k % 3 == 0 else None)
        k += 1
    # south-west old town: irregular brick and cobble lanes
    road("RESIDENTIAL", jitter([(120, 120), (220, 160), (260, 300), (200, 420)]), "BRICK", "INFERRED")
    road("LIVING_STREET", jitter([(140, 300), (300, 330), (380, 470)]), "COBBLE", "TAGGED", "Hiti Gali", "हिटी गल्ली")
    road("SERVICE", [(520, 120), (640, 140), (700, 230)], "CONCRETE", "INFERRED")
    road("RESIDENTIAL", [(560, 300), (760, 320), (960, 330)], "ASPHALT", "INFERRED", flags=RF["ONEWAY"],
         name="Ekmarga Gali")

    # East tile: periphery, tracks and test lanes.
    road("PRIMARY", [(1400, 515), (1460, 460), (1560, 430)], "ASPHALT", "TAGGED", flags=RF["LINK"] | RF["ONEWAY"])
    road("TRACK", [(1100, 120), (1300, 160), (1500, 120), (1700, 180)], "DIRT", "DEFAULT")
    road("TRACK", [(1300, 160), (1350, 300), (1330, 420)], "GRAVEL", "DERIVED")
    road("UNCLASSIFIED", [(1560, 430), (1600, 560), (1580, 680)], "ROCK", "INFERRED")
    road("SERVICE", [(1950, 300), (2040, 320)], "CONCRETE", "INFERRED")
    road("ROAD", [(1100, 900), (1300, 960), (1500, 940)], "ASPHALT", "DEFAULT")
    road("RESIDENTIAL", [(1100, 700), (1220, 720)], "ASPHALT", "INFERRED")
    road("RESIDENTIAL", [(1220, 720), (1280, 722)], "WOOD", "TAGGED", "Kath Pul", "काठ पुल",
         flags=RF["BRIDGE"], layer=1)
    road("RESIDENTIAL", [(1280, 722), (1420, 760)], "ASPHALT", "INFERRED")
    road("UNCLASSIFIED", [(1150, 1000), (1400, 1010), (1600, 1005)], "SNOW_ICE", "DEFAULT", "Test lane (snow)")
    road("TRACK", [(1180, 220), (1240, 200), (1290, 230)], "MUD", "INFERRED", flags=RF["FORD"])
    road("TERTIARY", [(1925, 640), (2060, 660)], "ASPHALT", "DERIVED")


def build_trails() -> None:
    mx, mz = MOUND
    road("PEDESTRIAN", jitter([(300, 820), (450, 815), (600, 822)]), "BRICK", "TAGGED", "Pasal Gali", "पसल गल्ली",
         access=TRAVEL_FOOT)
    road("FOOTWAY", [(450, 700), (470, 760), (452, 815)], "BRICK", "INFERRED", access=TRAVEL_FOOT)
    road("FOOTWAY", [(900, 400), (960, 430)], "CONCRETE", "INFERRED", access=TRAVEL_FOOT)
    sacs = ["HIKING", "MOUNTAIN_HIKING", "DEMANDING_MOUNTAIN_HIKING", "ALPINE_HIKING", "DEMANDING_ALPINE_HIKING",
            "DIFFICULT_ALPINE_HIKING", "UNKNOWN"]
    for i, sac in enumerate(sacs):
        a0 = math.radians(20 + i * 48)
        pts = []
        for t in range(7):
            r = 250 - t * 30
            a = a0 + t * 0.12
            pts.append((mx + r * math.cos(a), mz + r * math.sin(a)))
        inferred = i in (1, 2, 4)
        road("PATH", pts, ["DIRT", "GRASS", "ROCK", "ROCK", "ROCK", "ROCK", "DIRT"][i], "INFERRED" if i else "DEFAULT",
             f"Trail T{i + 1}" if sac != "UNKNOWN" else "Trail (no sac_scale)",
             flags=RF["SAC_INFERRED"] if inferred else 0, sac=sac, access=TRAVEL_NO_MOTOR,
             visibility=min(6, i + 1) if sac != "UNKNOWN" else 0)
    road("STEPS", [(mx - 40, mz - 160), (mx - 20, mz - 90), (mx, mz - 20)], "ROCK", "TAGGED", "Dhunga Sinki",
         sac="MOUNTAIN_HIKING", access=TRAVEL_FOOT)
    road("CYCLEWAY", [(1100, 470), (1300, 480), (1500, 470)], "ASPHALT", "TAGGED", access=2)
    road("BRIDLEWAY", [(1950, 900), (2040, 980)], "GRASS", "DEFAULT", sac="HIKING", access=TRAVEL_NO_MOTOR)
    road("FOOTWAY", [(1235, 600), (1265, 600)], "METAL", "TAGGED", "Jholunge Pul (sample)", "झोलुङ्गे पुल",
         flags=RF["BRIDGE"], access=TRAVEL_FOOT)


def rect(cx, cz, w, d, ang=0.0) -> Polygon:
    c, s = math.cos(ang), math.sin(ang)
    pts = [(-w / 2, -d / 2), (w / 2, -d / 2), (w / 2, d / 2), (-w / 2, d / 2)]
    return Polygon([(cx + x * c - z * s, cz + x * s + z * c) for x, z in pts])


def circle(cx, cz, r, n=20) -> Polygon:
    return Polygon([(cx + r * math.cos(2 * math.pi * i / n), cz + r * math.sin(2 * math.pi * i / n)) for i in range(n)])


def building(poly_local: Polygon, archetype: str, use: str, levels: int, *, inferred=True, name=None, ne=None,
             roof="UNKNOWN", roof_mat="UNKNOWN", wall="UNKNOWN", height_cm=0, extra_flags=0):
    g = Polygon([L(x, z) for x, z in poly_local.exterior.coords],
                [[L(x, z) for x, z in r.coords] for r in poly_local.interiors])
    c = g.centroid
    if not REGION_BOX.contains(c):
        return
    tx, ty = tile_of(c.x, c.y)
    osm_id = fake_id()
    flags = (BF["LEVELS_INFERRED"] if inferred else 0) | (BF["HEIGHT_TAGGED"] if height_cm else 0) | extra_flags
    seed = zlib.crc32(str(osm_id).encode())  # stand-in for FNV-1a; synthetic anyway
    buildings.append({"type": "Feature", "properties": {
        "osm_ref": osm_id << 1, "archetype": archetype, "use": use, "levels": levels, "flags": flags,
        "height_cm": height_cm, "min_height_cm": 0, "roof_shape": roof, "roof_material": roof_mat,
        "wall_material": wall, "seed": seed, **names(name, ne), "tile": tile_str(tx, ty)},
        "geometry": poly_geom(g)})


def build_buildings() -> None:
    # Blocks between the lane grid (west tile, Thamel-like): dense 3-6 storey.
    xs = [150, 270, 390, 630, 750, 870]
    zs = [565, 640, 760, 880, 1000]
    for bx0, bx1 in zip(xs, xs[1:]):
        if bx0 == 390:
            continue  # the pedestrian bazaar / temple square sits here
        for bz0, bz1 in zip(zs, zs[1:]):
            x, z = bx0 + 9, bz0 + 9
            while x < bx1 - 14:
                w = rng.uniform(12, 17)
                zz = z
                while zz < bz1 - 13:
                    d = rng.uniform(11, 16)
                    if rng.random() > 0.12:
                        r = rng.random()
                        arche = "MODERN_URBAN" if r < 0.6 else ("GENERIC" if r < 0.85 else "NEWAR")
                        use = rng.choice(["MIXED_USE", "HOTEL", "COMMERCIAL", "APARTMENTS", "HOUSE", "UNKNOWN"])
                        tagged = rng.random() < 0.06
                        lv = rng.randint(3, 6) if arche != "NEWAR" else rng.randint(3, 4)
                        building(rect(x + w / 2, zz + d / 2, w - 1.2, d - 1.2, rng.uniform(-0.03, 0.03)), arche, use, lv,
                                 inferred=not tagged, roof="FLAT" if arche == "MODERN_URBAN" else "UNKNOWN",
                                 wall="PLASTER" if arche == "MODERN_URBAN" else ("BRICK" if arche == "NEWAR" else "UNKNOWN"))
                    zz += d
                x += w
    # South-west old town: Newar rows around courtyards.
    for i in range(7):
        for j in range(5):
            cx, cz = 150 + i * 30, 170 + j * 32
            if rng.random() < 0.15:
                continue
            building(rect(cx, cz, 14, 9, 0.12), "NEWAR", rng.choice(["HOUSE", "MIXED_USE"]), rng.randint(3, 5),
                     inferred=rng.random() > 0.1, roof="GABLED", roof_mat="TILES", wall="BRICK")
    # A Newar courtyard house (chowk) with a hole.
    outer = Polygon([(420, 120), (470, 120), (470, 170), (420, 170)],
                    [[(432, 132), (432, 158), (458, 158), (458, 132)]])
    building(outer, "NEWAR", "HOUSE", 4, inferred=False, name="Chowk Ghar", ne="चोक घर", roof="GABLED",
             roof_mat="TILES", wall="BRICK", extra_flags=BF["ROOF_TAGGED"])
    # The bazaar square with temples.
    building(rect(455, 760, 12, 12), "TEMPLE_PAGODA", "RELIGIOUS", 3, inferred=False, name="Ganesh Mandir",
             ne="गणेश मन्दिर", roof="PAGODA", roof_mat="TILES", wall="BRICK",
             extra_flags=BF["LANDMARK"] | BF["ROOF_TAGGED"])
    building(rect(500, 735, 7, 7), "TEMPLE_SHIKHARA", "RELIGIOUS", 2, name="Shiva Mandir", ne="शिव मन्दिर",
             roof="SHIKHARA", wall="STONE")
    building(circle(420, 700, 3, 10), "SHRINE", "RELIGIOUS", 1, name="Sano Devalaya")
    building(circle(560, 690, 11, 24), "STUPA", "RELIGIOUS", 2, inferred=False, name="Kathe Chaitya",
             ne="काठे चैत्य", roof="DOME", extra_flags=BF["LANDMARK"])
    building(rect(500, 880, 22, 12, 0.05), "INSTITUTIONAL", "EDUCATION", 3, name="Sample School",
             roof="FLAT", wall="PLASTER")
    building(rect(330, 960, 30, 16), "MODERN_URBAN", "HOTEL", 7, inferred=False, height_cm=2350,
             name="Hotel (sample)", roof="FLAT", wall="GLASS")
    # East tile: periphery and the archetype test row.
    building(rect(1450, 200, 60, 30, 0.02), "INDUSTRIAL", "INDUSTRIAL", 1, roof="GABLED", roof_mat="METAL",
             wall="METAL")
    building(rect(1520, 260, 40, 8, 0.02), "GREENHOUSE", "GREENHOUSE", 1, roof="ROUND", roof_mat="GLASS")
    building(rect(1590, 250, 5, 4), "HUT", "HUT", 1, roof="SKILLION", roof_mat="METAL")
    for i in range(14):
        a = 2 * math.pi * i / 14
        r = 205 + 15 * (i % 2)
        building(rect(MOUND[0] + r * math.cos(a), MOUND[1] + r * math.sin(a), 9, 6, a), "HILL_VILLAGE", "HOUSE",
                 rng.randint(1, 2), roof="HIPPED", roof_mat=rng.choice(["METAL", "SLATE", "THATCH"]), wall="MUD")
    building(rect(MOUND[0] + 40, MOUND[1] + 10, 18, 14, 0.2), "GOMPA", "RELIGIOUS", 2, name="Sample Gompa",
             ne="गुम्बा", roof="HIPPED")
    building(circle(MOUND[0] + 70, MOUND[1] - 30, 2.5, 8), "CHORTEN", "RELIGIOUS", 1)
    building(rect(MOUND[0] - 30, MOUND[1] + 60, 12, 9), "TEAHOUSE", "HOTEL", 2, name="Bhatti (sample)",
             roof="GABLED", roof_mat="METAL")
    test_row = ["GENERIC", "TERAI", "SHERPA_HIMALAYAN", "TRANS_HIMALAYAN", "MOSQUE", "CHURCH", "INSTITUTIONAL",
                "HILL_VILLAGE", "NEWAR", "MODERN_URBAN"]
    for i, arche in enumerate(test_row):
        building(rect(1110 + i * 26, 830, 16, 14), arche, "RELIGIOUS" if arche in ("MOSQUE", "CHURCH") else "HOUSE",
                 1 + i % 4, name=f"Test: {arche}")
    # Scattered peri-urban houses in the east tile.
    for i in range(70):
        cx, cz = rng.uniform(1080, 2020), rng.uniform(560, 990)
        if math.hypot(cx - MOUND[0], cz - MOUND[1]) < 260 or 800 < cz < 860 or abs(cx - 1250) < 30:
            continue
        building(rect(cx, cz, rng.uniform(8, 13), rng.uniform(7, 11), rng.uniform(0, 1.5)),
                 rng.choice(["MODERN_URBAN", "GENERIC", "GENERIC"]), "HOUSE", rng.randint(2, 4),
                 roof="FLAT", wall="PLASTER")


def area(kind: str, geom_local, name=None, ne=None, osm_id=None):
    if isinstance(geom_local, MultiPolygon):
        g = MultiPolygon([Polygon([L(x, z) for x, z in p.exterior.coords]) for p in geom_local.geoms])
    else:
        g = Polygon([L(x, z) for x, z in geom_local.exterior.coords],
                    [[L(x, z) for x, z in r.coords] for r in geom_local.interiors])
    osm_id = osm_id or fake_id()
    for tx, ty in LEAF_TILES:
        tb = tile_box(tx, ty)
        piece = g.intersection(tb)
        if piece.is_empty or piece.area < 1:
            continue
        clipped = not g.within(tb)
        areas.append({"type": "Feature", "properties": {
            "osm_ref": osm_id << 1, "kind": kind, "flags": 1 if clipped else 0, **names(name, ne),
            "tile": tile_str(tx, ty)}, "geometry": poly_geom(piece)})


def build_areas() -> None:
    area("WATER_POND", Polygon([(1150, 300), (1270, 300), (1270, 380), (1150, 380)],
                               [[(1195, 330), (1195, 350), (1225, 350), (1225, 330)]]), "Pokhari (sample)", "पोखरी")
    area("PARK", Polygon([(820, 380), (960, 385), (955, 470), (825, 468)]), "Sapana Bagaincha (sample)", "सपना बगैंचा")
    area("PEDESTRIAN", Polygon([(395, 670), (620, 670), (620, 800), (395, 800)]), "Bazaar Chowk", "बजार चोक")
    area("RELIGIOUS", Polygon([(435, 740), (520, 740), (520, 790), (435, 790)]))
    area("RESIDENTIAL", Polygon([(100, 40), (600, 40), (600, 500), (100, 500)]))
    area("COMMERCIAL", Polygon([(120, 560), (910, 560), (910, 1010), (120, 1010)]), "Thamel bazaar area")
    area("RESIDENTIAL", Polygon([(900, 560), (1200, 560), (1200, 1000), (900, 1000)]))  # crosses the tile edge
    area("FOREST", Polygon([(MOUND[0] + 150 * math.cos(a) * (1 + 0.15 * math.sin(3 * a)),
                             MOUND[1] + 150 * math.sin(a) * (1 + 0.15 * math.sin(3 * a)))
                            for a in [2 * math.pi * i / 36 for i in range(36)]]), "Ban (sample forest)", "वन")
    area("FARMLAND", MultiPolygon([Polygon([(1300, 30), (1450, 30), (1450, 110), (1300, 110)]),
                                   Polygon([(1520, 30), (1700, 40), (1690, 140), (1520, 120)])]))
    area("PITCH", Polygon([(1960, 380), (2030, 380), (2030, 470), (1960, 470)]), "Khel Maidan", "खेल मैदान")
    area("SAND_SHINGLE", Polygon([(30, 0), (100, 0), (100, 1024), (30, 1024)]))
    area("ORCHARD", Polygon([(1985, 520), (2045, 520), (2045, 620), (1985, 620)]))


def line(kind: str, pts, name=None, ne=None, width_cm=0, flags=0):
    props = {"osm_way_id": fake_id(), "kind": kind, "flags": flags, "width_cm": width_cm, **names(name, ne)}
    clip_lines(pts, props, lines, ctx_flags=(1, 2))  # LINE flags: bit0 HAS_PREV_CTX, bit1 HAS_NEXT_CTX


def build_lines() -> None:
    line("STREAM", [(60 + 25 * math.sin(z / 120.0), z) for z in range(-20, 1060, 40)], "Khola (sample)", "खोला",
         width_cm=600)
    line("CANAL", [(1250, -20), (1252, 400), (1250, 1050)], "Kulo (sample canal)", "कुलो", width_cm=300)
    line("DITCH", [(1300, 110), (1450, 112)], flags=4)
    line("CITY_WALL", [(110, 60), (110, 330), (330, 330)], "Purano Parkhal", "पुरानो पर्खाल")
    line("MANI_WALL", [(MOUND[0] + 20, MOUND[1] - 50), (MOUND[0] + 60, MOUND[1] - 55)], "Mani wall (sample)")
    line("CABLE_CAR", [(1960, 520), (MOUND[0] + 10, MOUND[1] + 5)], "Test cable car")


def poi(kind: str, x, z, name=None, ne=None, importance=60, flags=PF["DISCOVERABLE"], ele_m=None):
    gx, gz = L(x, z)
    tx, ty = tile_of(gx, gz)
    osm_id = fake_id()
    f = flags | (PF["HAS_ELE"] if ele_m is not None else 0)
    pois.append({"type": "Feature", "properties": {
        "osm_ref": osm_id << 2, "kind": kind, "flags": f, "importance": importance,
        "ele_dm": int(round(ele_m * 10)) if ele_m is not None else 0, **names(name, ne),
        "search_id": len(pois) + 1, "tile": tile_str(tx, ty)}, "geometry": {"type": "Point", "coordinates": g2ll(gx, gz)}})


def build_pois() -> None:
    sac = PF["DISCOVERABLE"] | PF["SACRED"]
    poi("TEMPLE_HINDU", 455, 760, "Ganesh Mandir", "गणेश मन्दिर", 220, sac | PF["LANDMARK"])
    poi("TEMPLE_HINDU", 500, 735, "Shiva Mandir", "शिव मन्दिर", 120, sac)
    poi("STUPA", 560, 690, "Kathe Chaitya", "काठे चैत्य", 200, sac | PF["LANDMARK"])
    poi("SHRINE", 420, 700, "Sano Devalaya", "सानो देवालय", 40, sac)
    poi("GOMPA", MOUND[0] + 40, MOUND[1] + 10, "Sample Gompa", "गुम्बा", 140, sac)
    poi("CHORTEN", MOUND[0] + 70, MOUND[1] - 30, None, None, 20, sac)
    poi("MANI_WALL", MOUND[0] + 40, MOUND[1] - 52, "Mani wall (sample)", None, 20, sac)
    poi("MOSQUE", 1110 + 4 * 26, 830, "Test mosque", None, 40, sac)
    poi("CHURCH", 1110 + 5 * 26, 830, "Test church", None, 40, sac)
    poi("PLACE_OF_WORSHIP", 230, 260, None, None, 10, PF["SACRED"])
    poi("HERITAGE_SQUARE", 505, 735, "Bazaar Chowk", "बजार चोक", 210, PF["DISCOVERABLE"] | PF["LANDMARK"])
    poi("STONE_TAP", 200, 330, "Hiti (stone spout)", "हिटी", 90)
    poi("CITY_GATE", 110, 200, "Dhoka (sample gate)", "ढोका", 60)
    poi("MUSEUM", 445, 145, "Chowk Ghar museum", "संग्रहालय", 110)
    poi("PALACE", 600, 160, "Sample Durbar", "दरबार", 150, PF["DISCOVERABLE"] | PF["LANDMARK"])
    poi("MONUMENT", 540, 610, "Sahid Stambha (sample)", "शहीद स्तम्भ", 70)
    poi("RUINS", 1180, 160, "Old wall ruins", None, 15)
    poi("VIEWPOINT", MOUND[0], MOUND[1], "Danda viewpoint", "डाँडा", 160, ele_m=1384.0)
    poi("PEAK", MOUND[0] + 5, MOUND[1] + 3, "Sano Danda (sample)", "सानो डाँडा", 120, ele_m=1386.0)
    poi("SPRING", MOUND[0] - 120, MOUND[1] - 140, "Mul (spring)", "मूल", 30)
    poi("NOTABLE_TREE", 890, 425, "Pipal Bot", "पिपल बोट", 50)
    poi("PARK", 890, 430, "Sapana Bagaincha (sample)", "सपना बगैंचा", 130)
    poi("LAKE", 1210, 340, "Pokhari (sample)", "पोखरी", 100)
    poi("WATERFALL", 1252, 600, "Kulo jharna (test)", None, 15)
    poi("BUS_STATION", 1880, 470, "Bus park (sample)", "बसपार्क", 140)
    poi("TAXI_STAND", 700, 545, None, None, 10, 0)
    poi("FUEL", 1905, 300, None, None, 15, 0)
    poi("PARKING", 980, 520, None, None, 5, 0)
    poi("BRIDGE", 1250, 722, "Kath Pul", "काठ पुल", 30)
    poi("HELIPAD", 2010, 420, "Helipad (test)", None, 30)
    poi("HOTEL", 330, 960, "Hotel (sample)", None, 30, 0)
    poi("GUEST_HOUSE", 210, 700, "Guest house (sample)", None, 20, 0)
    poi("HOSTEL", 660, 930, "Hostel (sample)", None, 15, 0)
    poi("TEAHOUSE", MOUND[0] - 30, MOUND[1] + 60, "Bhatti (sample)", "भट्टी", 60)
    poi("RESTAURANT", 300, 650, "Khaja Ghar", "खाजा घर", 20, 0)
    poi("CAFE", 680, 770, "Chiya Pasal", "चिया पसल", 20, 0)
    poi("SHOP", 520, 820, None, None, 5, 0)
    poi("MARKETPLACE", 510, 805, "Asan-style market (sample)", "बजार", 120)
    poi("INFORMATION", 610, 790, "Tourist info", None, 30, 0)
    poi("ATTRACTION", 470, 790, "Kasthamandap-style pavilion (sample)", "मण्डप", 90)
    poi("PICNIC_SITE", MOUND[0] + 110, MOUND[1] + 120, "Picnic spot", "वनभोज स्थल", 40)
    poi("PARAGLIDING", MOUND[0] - 10, MOUND[1] + 30, "Paragliding launch (test)", None, 50)
    poi("ZIPLINE", 1960, 520, "Zipline (test)", None, 40)
    poi("BUNGEE", 1252, 650, "Bungee (test)", None, 40)
    poi("SCHOOL", 500, 880, "Sample School", "विद्यालय", 30, 0)
    poi("HOSPITAL", 980, 900, "Swasthya Chauki (sample)", "स्वास्थ्य चौकी", 60, 0)
    poi("GOVERNMENT", 600, 130, "Ward office (sample)", "वडा कार्यालय", 40, 0)
    poi("BANK", 760, 545, None, None, 10, 0)
    # Places (optional layer; not part of the POIS chunk). Real neighbourhood names, approximate positions.
    for name, ne, kind, lon, lat, imp in [
        ("Thamel", "ठमेल", "NEIGHBOURHOOD", 85.3101, 27.7153, 200),
        ("Jyatha", "ज्याठा", "NEIGHBOURHOOD", 85.3112, 27.7122, 120),
        ("Lainchaur", "लैनचौर", "NEIGHBOURHOOD", 85.3150, 27.7172, 110),
    ]:
        places.append({"type": "Feature", "properties": {"osm_ref": fake_id() << 2, "kind": kind,
                                                         "importance": imp, **names(name, ne, en=name)},
                       "geometry": {"type": "Point", "coordinates": [lon, lat]}})


# --- writers ------------------------------------------------------------------------
def write_geojson(path: Path, features: list[dict], layer: str) -> None:
    """One feature per line (diff-friendly), compact separators, sorted by tile."""
    head = json.dumps({"type": "FeatureCollection", "name": layer}, separators=(",", ":"))[:-1]
    body = ",\n".join(json.dumps(f, ensure_ascii=False, separators=(",", ":")) for f in features)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(head + ',"features":[\n' + body + "\n]}\n", encoding="utf-8")


def length_m(feature: dict) -> float:
    # haversine over the lon/lat line (good enough for stats), context points excluded
    c = feature["geometry"]["coordinates"]
    fl = feature["properties"].get("flags", 0)
    c = c[(1 if fl & 8 else 0):(len(c) - 1 if fl & 16 else len(c))]
    tot = 0.0
    for (lo1, la1), (lo2, la2) in zip(c, c[1:]):
        p1, p2 = math.radians(la1), math.radians(la2)
        dp, dl = p2 - p1, math.radians(lo2 - lo1)
        a = math.sin(dp / 2) ** 2 + math.cos(p1) * math.cos(p2) * math.sin(dl / 2) ** 2
        tot += 2 * 6371008.8 * math.asin(math.sqrt(a))
    return tot


def tile_entry(level: int, tx: int, ty: int, leaf: bool) -> dict:
    s = tile_size(level)
    x0, z0 = tx * s, ty * s
    sw, se, ne, nw = g2ll(x0, z0), g2ll(x0 + s, z0), g2ll(x0 + s, z0 + s), g2ll(x0, z0 + s)
    lons, lats = [p[0] for p in (sw, se, ne, nw)], [p[1] for p in (sw, se, ne, nw)]
    e = {"level": level, "tx": tx, "ty": ty, "key": str(tile_key(level, tx, ty)),
         "bounds_lonlat": [min(lons), min(lats), max(lons), max(lats)],
         "corners_lonlat": [sw, se, ne, nw], "detail": True}
    if leaf:
        t = tile_str(tx, ty)
        e["hillshade"] = f"hillshade/{level}/{tx}_{ty}.png"
        e["biome"] = f"biome/{level}/{tx}_{ty}.png"
        e["counts"] = {name: sum(1 for f in feats if f["properties"].get("tile") == t)
                       for name, feats in (("roads", roads), ("trails", trails), ("buildings", buildings),
                                           ("areas", areas), ("lines", lines), ("pois", pois))}
    return e


def main(argv: list[str] | None = None) -> None:
    global OUT
    ap = argparse.ArgumentParser(description="Write the synthetic QA sample (see module docstring).")
    ap.add_argument("--out", type=Path, default=OUT, help=f"output directory (default {OUT})")
    OUT = ap.parse_args(argv).out.resolve()
    if OUT.exists():
        if any(OUT.iterdir()) and not (OUT / "index.json").is_file():
            raise SystemExit(f"refusing to replace {OUT}: not empty and not a QA export")
        shutil.rmtree(OUT)
    build_roads()
    build_trails()
    build_buildings()
    build_areas()
    build_lines()
    build_pois()
    for tx, ty in LEAF_TILES:
        write_rasters(tx, ty)

    tiles = [tile_entry(PARENT[0], PARENT[1], PARENT[2], False)] + [tile_entry(LEAF, tx, ty, True)
                                                                    for tx, ty in LEAF_TILES]
    w = min(t["bounds_lonlat"][0] for t in tiles[1:])
    s = min(t["bounds_lonlat"][1] for t in tiles[1:])
    e = max(t["bounds_lonlat"][2] for t in tiles[1:])
    n = max(t["bounds_lonlat"][3] for t in tiles[1:])

    layers = {"roads": roads, "trails": trails, "buildings": buildings, "areas": areas, "lines": lines,
              "pois": pois, "places": places}
    for name, feats in layers.items():
        feats.sort(key=lambda f: (f["properties"].get("tile", ""), f["properties"].get("osm_way_id")
                                  or f["properties"].get("osm_ref", 0)))
        write_geojson(OUT / f"{name}.geojson", feats, name)

    # Per-tile building files for the optional tiled index (index.tiled.json).
    for tx, ty in LEAF_TILES:
        t = tile_str(tx, ty)
        write_geojson(OUT / "buildings" / str(LEAF) / f"{tx}_{ty}.geojson",
                      [f for f in buildings if f["properties"]["tile"] == t], "buildings")

    road_len = {k: 0.0 for k in ("TAGGED", "DERIVED", "INFERRED", "DEFAULT")}
    for f in roads + trails:
        road_len[f["properties"]["surface_source"]] += length_m(f)
    total = sum(road_len.values())
    lv_inf = sum(1 for f in buildings if f["properties"]["flags"] & BF["LEVELS_INFERRED"])
    arche: dict[str, int] = {}
    for f in buildings:
        arche[f["properties"]["archetype"]] = arche.get(f["properties"]["archetype"], 0) + 1

    index = {
        "format": "ghumante-qa-index", "version": 1,
        "region": REGION, "name": {"en": "Thamel (synthetic sample)", "ne": "ठमेल (नमूना)"},
        "synthetic": True,
        "note": "Synthetic sample for the QA viewer: invented features near Thamel, fake OSM ids.",
        "data_version": 1, "pipeline_version": "sample", "scale_model": "identity",
        "bbox_lonlat": [w, s, e, n],
        "detail_levels": [9, 10], "horizon_levels": [], "leaf_level": LEAF,
        "tiles": tiles,
        "layers": {name: {"path": f"{name}.geojson", "count": len(feats)} for name, feats in layers.items()},
        "raster": {"hillshade": {"size": HEIGHT_N, "registration": "vertex", "row0": "north"},
                   "biome": {"size": BIOME_N, "registration": "vertex", "row0": "north"}},
        "biome_palette": BIOME_PALETTE,
        "stats": {
            "roads": len(roads), "trails": len(trails), "buildings": len(buildings), "areas": len(areas),
            "lines": len(lines), "pois": len(pois),
            "road_km": round(total / 1000, 2),
            "surface_tagged_pct": round(100 * road_len["TAGGED"] / total, 1),
            "surface_source_pct": {k: round(100 * v / total, 1) for k, v in road_len.items()},
            "levels_inferred_pct": round(100 * lv_inf / len(buildings), 1),
            "archetypes": dict(sorted(arche.items())),
        },
        "attribution": ["Synthetic sample data (not OpenStreetMap)"],
    }
    (OUT / "index.json").write_text(json.dumps(index, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")

    tiled = json.loads(json.dumps(index))
    tiled["layers"]["buildings"] = {"tiles": "buildings/{L}/{tx}_{ty}.geojson", "level": LEAF,
                                    "count": len(buildings)}
    tiled["note"] += " Variant: buildings are listed per tile (optional per-tile layer files)."
    (OUT / "index.tiled.json").write_text(json.dumps(tiled, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")

    # Minimal variant: only what a terse exporter might write. Exercises the viewer's fallbacks:
    # string tile ids without corners (computed from NPL-TM84), a plain layer list, trails mixed
    # into the roads file (split client-side), no biome palette, no raster block (default PNG paths).
    write_geojson(OUT / "roads_with_trails.geojson", roads + trails, "roads")
    minimal = {
        "region": REGION + "_minimal",
        "bbox": {"west": w, "south": s, "east": e, "north": n},
        "leaf_level": LEAF,
        "tiles": [tile_str(tx, ty) for tx, ty in LEAF_TILES],
        "layers": ["pois", {"name": "roads", "file": "roads_with_trails.geojson"}],
    }
    (OUT / "index.minimal.json").write_text(json.dumps(minimal, indent=1) + "\n", encoding="utf-8")

    print(f"wrote {OUT}: " + ", ".join(f"{k}={len(v)}" for k, v in layers.items()))


if __name__ == "__main__":
    main()
