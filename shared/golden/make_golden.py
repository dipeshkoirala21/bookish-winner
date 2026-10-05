#!/usr/bin/env python3
"""Generate the golden files that keep the C# core (game/Assets/Ghumante/Core) in sync with the pipeline.

Run from the repository root::

    python3 shared/golden/make_golden.py

Every file is a pure function of this script and the pipeline package (no clocks, no unseeded
randomness, sorted JSON keys), so CI regenerates them and fails when the committed copies differ
(.github/workflows/core.yml). core-tests/ reads them; see core-tests/GoldenFiles.cs.

Outputs (all in shared/golden/):

* ``tm84_points.json``: lon/lat -> canonical and game X/Z from pyproj (projection.py) across Nepal.
* ``golden.ght`` + ``golden_tile.json``: a synthetic tile with every chunk type, and its decoded
  contents. ``golden_unknown.ght`` is the same tile plus an unknown ``XTRA`` chunk readers must skip.
* ``golden.ghpk`` + ``golden_pack.json``: a small region pack and its directory.
* ``golden.ghsi`` + ``golden_search.json``: a search index over ~30 synthetic places and POIs, decoded
  entries, ``fold``/``romanize`` cases, and ranked results for a query list.
* ``golden.ghrg`` + ``golden_routes.json``: a ~40-node synthetic road network, its decoded arrays,
  A* routes per profile and nearest-node queries. ``golden_profiles.json`` dumps the travel tables.

``--out DIR`` writes somewhere else (tests). ``--enums-cs`` additionally rewrites
game/Assets/Ghumante/Core/Data/Enums.cs from shared/enums.json (append-only enums, ARCHITECTURE.md).
"""

from __future__ import annotations

import json
import math
import struct
import sys
import zlib
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "pipeline"))

from shapely.geometry import box  # noqa: E402

from ghumante_pipeline import projection, routing, search_index, tile_format, translit  # noqa: E402
from ghumante_pipeline.model import (AdminArea, AreaKind, Biome, BuildingArchetype, BuildingFlags,  # noqa: E402
                                     BuildingUse, LineKind, NameRec, PlaceFeature, PlaceKind, PoiFeature, PoiFlags,
                                     PoiKind, RoadClass, RoadFeature, RoadFlags, RoofMaterial, RoofShape, SacScale,
                                     Surface, SurfaceSource, Travel, WallMaterial)
from ghumante_pipeline.pack import PackReader, write_pack  # noqa: E402
from ghumante_pipeline.projection import TileId  # noqa: E402
from ghumante_pipeline.tile_format import (AreaRec, BuildingRec, LineRec, NameEntry, NameTable, PoiRec,  # noqa: E402
                                           RoadRec, TileData)

DATA_VERSION = 1


def write_json(name: str, obj) -> None:
    text = json.dumps(obj, sort_keys=True, ensure_ascii=False, indent=1, allow_nan=False) + "\n"
    (OUT / name).write_text(text, encoding="utf-8")


def write_bytes(name: str, data: bytes) -> None:
    (OUT / name).write_bytes(data)


# ---------------------------------------------------------------------------
# Projection
# ---------------------------------------------------------------------------
def make_tm84() -> None:
    pts: list[tuple[float, float]] = []
    # A 7 x 6 grid over Nepal and its horizon buffer, plus named places and the 84°E meridian itself.
    for i in range(7):
        for j in range(6):
            pts.append((round(80.0 + i * 1.4, 4), round(26.3 + j * 0.85, 4)))
    pts += [(85.3240, 27.7172), (83.9856, 28.2096), (86.9250, 27.9881), (84.0, 28.0), (84.0, 26.5),
            (88.2, 27.0), (80.06, 28.9), (85.362039, 27.721493), (84.4167, 27.5833)]
    lon = np.array([p[0] for p in pts])
    lat = np.array([p[1] for p in pts])
    e, n = projection.lonlat_to_tm84(lon, lat)
    x, z = projection.tm84_to_game(e, n)
    rows = [{"lon": float(lo), "lat": float(la), "e": round(float(ee), 6), "n": round(float(nn), 6),
             "x": round(float(xx), 6), "z": round(float(zz), 6)}
            for lo, la, ee, nn, xx, zz in zip(lon, lat, e, n, x, z)]
    write_json("tm84_points.json", {"format": "ghumante-golden-tm84", "version": 1,
                                    "origin": [projection.WORLD_ORIGIN_E, projection.WORLD_ORIGIN_N],
                                    "points": rows})


# ---------------------------------------------------------------------------
# Tiles and pack
# ---------------------------------------------------------------------------
GOLDEN_TILE = TileId(10, 266, 200)


def _arr(pts) -> np.ndarray:
    return np.asarray(pts, dtype=np.int64).reshape(-1, 2)


def make_tile_data() -> TileData:
    names = NameTable()
    durbar = names.ref(NameRec("Durbar Marg", "Durbar Marg", "दरबार मार्ग"))
    bagmati = names.ref(NameRec("बागमती", "Bagmati River", "बागमती नदी"))
    boudha = names.ref(NameRec("बौद्धनाथ", "Boudhanath", "बौद्धनाथ"))
    nh = names.ref_str("NH04")
    thamel = names.ref(NameRec("Thamel", "", ""))

    n = 17
    jj, ii = np.mgrid[0:n, 0:n]
    heights = (9000 + 37 * ii * ii - 11 * jj * ii + 400 * jj).astype(np.int64)
    heights[0, 0] = 0
    heights[0, 1] = 65535  # wraps the residual
    heights[3, 5] = 1
    heights[16, 16] = 65535
    biomes = ((ii * 7 + jj * 3) % 28).astype(np.uint8)

    s = int(GOLDEN_TILE.size * 100)
    roads = [
        RoadRec(osm_way_id=4000000123, road_class=RoadClass.PRIMARY, surface=Surface.ASPHALT,
                surface_source=SurfaceSource.TAGGED, flags=RoadFlags.ONEWAY | RoadFlags.HAS_PREV_CTX, lanes=2,
                layer=0, width_cm=850, access=int(Travel.FOOT | Travel.CAR | Travel.BUS | Travel.MOTORBIKE),
                name_ref=durbar, ref_ref=nh, points=_arr([(-1500, 2000), (0, 2100), (25000, 26000), (51000, 52500)])),
        RoadRec(osm_way_id=17, road_class=RoadClass.PATH, surface=Surface.DIRT, surface_source=SurfaceSource.INFERRED,
                flags=RoadFlags.SAC_INFERRED | RoadFlags.BRIDGE | RoadFlags.HAS_NEXT_CTX, sac_scale=SacScale.ALPINE_HIKING,
                trail_visibility=4, layer=-2, width_cm=0, access=int(Travel.FOOT), name_ref=thamel,
                points=_arr([(100, 100), (2000, 900), (s, 1500), (s + 2000, 1700)])),
        RoadRec(osm_way_id=17, road_class=RoadClass.RESIDENTIAL, surface=Surface.BRICK,
                surface_source=SurfaceSource.DEFAULT, flags=RoadFlags.TUNNEL | RoadFlags.LINK | RoadFlags.FORD,
                lanes=255, trail_visibility=0, layer=5, width_cm=300000, access=0x7F,
                points=_arr([(50, 60), (s, s)])),
    ]
    lines = [
        LineRec(osm_way_id=99, kind=LineKind.RIVER, flags=tile_format.LineFlags.HAS_PREV_CTX
                | tile_format.LineFlags.INTERMITTENT, width_cm=2500, name_ref=bagmati,
                points=_arr([(-300, 50000), (0, 50010), (40000, 61000)])),
        LineRec(osm_way_id=5, kind=LineKind.CABLE_CAR, flags=tile_format.LineFlags.TUNNEL, width_cm=0,
                points=_arr([(1000, 1000), (1000, 99000)])),
    ]
    buildings = [
        BuildingRec(osm_ref=(123456789 << 1), archetype=BuildingArchetype.NEWAR, use=BuildingUse.HOUSE, levels=4,
                    flags=BuildingFlags.LEVELS_INFERRED, height_cm=1250, min_height_cm=0,
                    roof_shape=RoofShape.HIPPED, roof_material=RoofMaterial.TILES, wall_material=WallMaterial.BRICK,
                    # clockwise on purpose: canonicalize re-orients it
                    rings=[_arr([(10000, 10000), (10000, 11000), (11500, 11000), (11500, 10000)])]),
        BuildingRec(osm_ref=(77 << 1) | 1, archetype=BuildingArchetype.STUPA, use=BuildingUse.RELIGIOUS, levels=1,
                    flags=BuildingFlags.LANDMARK | BuildingFlags.HEIGHT_TAGGED | BuildingFlags.ROOF_TAGGED,
                    height_cm=3600, min_height_cm=200, roof_shape=RoofShape.DOME, roof_material=RoofMaterial.STONE,
                    wall_material=WallMaterial.PLASTER, name_ref=boudha,
                    rings=[_arr([(30000, 30000), (36000, 30000), (36000, 36000), (30000, 36000), (30000, 30000)]),
                           _arr([(32000, 32000), (34000, 32000), (34000, 34000), (32000, 34000)])]),
    ]
    areas = [
        AreaRec(osm_ref=(555 << 1), kind=AreaKind.WATER_POND, flags=0, name_ref=0,
                vertices=_arr([(70000, 70000), (80000, 70000), (80000, 80000), (70000, 80000)]),
                indices=np.array([0, 2, 1, 0, 3, 2], dtype=np.int64), rings=[(0, 4)]),
        AreaRec(osm_ref=(9 << 1) | 1, kind=AreaKind.FOREST, flags=tile_format.AreaFlags.CLIPPED_BY_TILE,
                name_ref=bagmati,
                vertices=_arr([(0, 0), (s, 0), (s, 20000), (0, 20000), (5000, 5000), (6000, 5000), (6000, 6000)]),
                indices=np.array([0, 1, 2, 0, 2, 3], dtype=np.int64), rings=[(0, 4), (4, 3)]),
    ]
    pois = [
        PoiRec(osm_ref=(56688296 << 2) | 1, kind=PoiKind.STUPA, flags=PoiFlags.DISCOVERABLE | PoiFlags.LANDMARK
               | PoiFlags.SACRED, importance=230, x_cm=33000, z_cm=33000, name_ref=boudha, search_id=7),
        PoiRec(osm_ref=(42 << 2), kind=PoiKind.PEAK, flags=PoiFlags.HAS_ELE, importance=150, x_cm=-5, z_cm=s + 7,
               ele_dm=88488, search_id=0),
        PoiRec(osm_ref=(43 << 2) | 2, kind=PoiKind.BANK, flags=0, importance=0, x_cm=1, z_cm=2),
    ]
    return TileData(tile=GOLDEN_TILE, data_version=DATA_VERSION, heights_q=heights.astype(np.uint16),
                    biomes=biomes, names=names.entries(), roads=roads, lines=lines, buildings=buildings,
                    areas=areas, pois=pois, seed=tile_format.make_seed(GOLDEN_TILE, DATA_VERSION),
                    meta={"region": "golden_region", "sources": {"osm": "d41d8cd98f00b204e9800998ecf8427e",
                                                                   "dem": "glo30", "landcover": "worldcover"},
                          "note": "नमस्ते \"quoted\""},
                    has_detail=True)


def horizon_tile(tile: TileId, base: int) -> TileData:
    n = 9
    jj, ii = np.mgrid[0:n, 0:n]
    return TileData(tile=tile, data_version=DATA_VERSION,
                    heights_q=(base + 100 * ii + 50 * jj).astype(np.uint16),
                    biomes=np.full((n, n), int(Biome.HILL_FOREST), dtype=np.uint8),
                    seed=tile_format.make_seed(tile, DATA_VERSION))


def _pts(a: np.ndarray) -> list[int]:
    return [int(v) for v in np.asarray(a, dtype=np.int64).reshape(-1)]


def tile_json(blob: bytes) -> dict:
    hdr = tile_format.read_header(blob)
    td = tile_format.decode_tile(blob)
    hq = td.heights_q
    return {
        "level": hdr.tile.level, "tx": hdr.tile.tx, "ty": hdr.tile.ty, "key": hdr.tile.key,
        "version": hdr.version, "flags": hdr.flags, "data_version": hdr.data_version,
        "payload_crc32": hdr.payload_crc32, "bytes": len(blob),
        "chunks": [{"fourcc": c.fourcc.decode(), "codec": c.codec, "offset": c.offset, "stored_size": c.stored_size}
                   for c in hdr.chunks],
        "heights_n": None if hq is None else int(hq.shape[0]),
        "heights_q": None if hq is None else [int(v) for v in hq.reshape(-1)],
        # float32 metres, exactly representable in JSON via repr of the float32 value
        "heights_m": None if hq is None else [float(v) for v in tile_format.dequantize_heights(hq).reshape(-1)],
        "biomes_n": None if td.biomes is None else int(td.biomes.shape[0]),
        "biomes": None if td.biomes is None else [int(v) for v in td.biomes.reshape(-1)],
        "names": [[e.default, e.en, e.ne] for e in td.names],
        "roads": [{"osm_way_id": r.osm_way_id, "road_class": r.road_class, "surface": r.surface,
                   "surface_source": r.surface_source, "flags": r.flags, "lanes": r.lanes, "sac_scale": r.sac_scale,
                   "trail_visibility": r.trail_visibility, "layer": r.layer, "width_cm": r.width_cm,
                   "access": r.access, "name_ref": r.name_ref, "ref_ref": r.ref_ref, "points": _pts(r.points)}
                  for r in td.roads],
        "lines": [{"osm_way_id": ln.osm_way_id, "kind": ln.kind, "flags": ln.flags, "width_cm": ln.width_cm,
                   "name_ref": ln.name_ref, "points": _pts(ln.points)} for ln in td.lines],
        "buildings": [{"osm_ref": b.osm_ref, "archetype": b.archetype, "use": b.use, "levels": b.levels,
                       "flags": b.flags, "height_cm": b.height_cm, "min_height_cm": b.min_height_cm,
                       "roof_shape": b.roof_shape, "roof_material": b.roof_material,
                       "wall_material": b.wall_material, "seed": b.seed, "name_ref": b.name_ref,
                       "rings": [_pts(rg) for rg in b.rings]} for b in td.buildings],
        "areas": [{"osm_ref": a.osm_ref, "kind": a.kind, "flags": a.flags, "name_ref": a.name_ref,
                   "vertices": _pts(a.vertices), "indices": [int(i) for i in a.indices],
                   "rings": [[int(s), int(n)] for s, n in a.rings]} for a in td.areas],
        "pois": [{"osm_ref": p.osm_ref, "kind": p.kind, "flags": p.flags, "importance": p.importance,
                  "x_cm": p.x_cm, "z_cm": p.z_cm, "ele_dm": p.ele_dm, "name_ref": p.name_ref,
                  "search_id": p.search_id} for p in td.pois],
        "seed": None if td.seed is None else {"tile_seed": td.seed[0], "ruleset": td.seed[1]},
        "meta_json": None if td.meta is None else tile_format._meta_json(td.meta),
        "meta": td.meta,
        "has_detail": td.has_detail,
    }


def make_tiles_and_pack() -> None:
    blob = tile_format.encode_tile(make_tile_data())
    write_bytes("golden.ght", blob)
    expect = tile_json(blob)

    # Same tile plus an unknown chunk (sorted between the others) that readers must skip.
    hdr = tile_format.read_header(blob)
    chunks = [(c.fourcc, tile_format.chunk_body(blob, c)) for c in hdr.chunks]
    chunks.append((b"XTRA", b"future chunk payload, ignore me" * 3))
    unknown = tile_format.encode_chunks(chunks, GOLDEN_TILE, DATA_VERSION, True)
    write_bytes("golden_unknown.ght", unknown)
    expect["unknown_variant"] = {
        "bytes": len(unknown), "payload_crc32": tile_format.read_header(unknown).payload_crc32,
        "chunks": [c.fourcc.decode() for c in tile_format.read_header(unknown).chunks]}
    write_json("golden_tile.json", expect)

    tiles = {GOLDEN_TILE.key: blob}
    for t, base in ((TileId(5, 8, 6), 12000), (TileId(6, 16, 12), 20000), (TileId(6, 17, 12), 30000)):
        tiles[t.key] = tile_format.encode_tile(horizon_tile(t, base))
    pack_path = OUT / "golden.ghpk"
    write_pack(pack_path, "golden_region_long_id", DATA_VERSION, tiles)
    with PackReader(pack_path) as pr:
        entries = []
        for k in pr.keys():
            off, size, crc = pr.entry(k)
            t = projection.tile_from_key(k)
            entries.append({"key": k, "level": t.level, "tx": t.tx, "ty": t.ty, "offset": off, "size": size,
                            "crc32": crc, "sha_crc32": zlib.crc32(pr.get(k)) & 0xFFFFFFFF})
        pack = {"region_id": pr.region_id, "data_version": pr.data_version, "tile_count": pr.tile_count,
                "bytes": pack_path.stat().st_size, "entries": entries,
                "absent_keys": [TileId(10, 266, 201).key, TileId(5, 8, 7).key, 0],
                "horizon_tile": tile_json(pr.get(TileId(6, 17, 12).key))}
    write_json("golden_pack.json", pack)


# ---------------------------------------------------------------------------
# Search
# ---------------------------------------------------------------------------
def _place(oid, kind, lon, lat, default, en="", ne="", alt=(), population=None):
    return PlaceFeature("n", oid, kind, lon, lat, NameRec(default, en, ne, tuple(alt)), population=population)


def _poi(oid, kind, lon, lat, default, en="", ne="", alt=(), osm_type="n", ele=None, tags=None, flags=0):
    return PoiFeature(osm_type, oid, kind, lon, lat, NameRec(default, en, ne, tuple(alt)), ele_m=ele,
                      tags=dict(tags or {}), flags=PoiFlags(flags))


ADMIN = [
    AdminArea(4583010, PlaceKind.PROVINCE, 4, NameRec("Bagmati Province", "Bagmati Province", "बागमती प्रदेश"),
              box(84.5, 27.2, 86.5, 28.5)),
    AdminArea(4583011, PlaceKind.PROVINCE, 4, NameRec("Gandaki Province", "Gandaki Province", "गण्डकी प्रदेश"),
              box(82.8, 27.4, 84.5, 29.4)),
    AdminArea(4583246, PlaceKind.DISTRICT, 6, NameRec("Kathmandu", "Kathmandu", "काठमाडौं"),
              box(85.18, 27.68, 85.58, 27.83)),
    AdminArea(4583300, PlaceKind.DISTRICT, 6, NameRec("Kaski", "Kaski", "कास्की"), box(83.7, 28.0, 84.2, 28.6)),
]
PLACES = [
    _place(67157058, PlaceKind.CITY, 85.3205817, 27.708317, "काठमाडौं", "Kathmandu", "काठमाडौं",
           alt=("काठमांडौ",), population=975000),
    _place(21, PlaceKind.CITY, 83.9856, 28.2096, "Pokhara", "Pokhara", "पोखरा", population=518000),
    _place(723880699, PlaceKind.CITY, 85.3166069, 27.6765635, "ललितपुर", "Lalitpur", "ललितपुर", alt=("Patan",),
           population=254000),
    _place(24, PlaceKind.CITY, 85.4298, 27.6710, "भक्तपुर", "Bhaktapur", "भक्तपुर", population=81000),
    _place(1349697740, PlaceKind.NEIGHBOURHOOD, 85.3127015, 27.7166578, "Thamel", "Thamel", "ठमेल"),
    _place(1349697741, PlaceKind.QUARTER, 85.3130, 27.7170, "Thamel"),  # duplicate within 300 m: deduped
    _place(3318253390, PlaceKind.SUBURB, 85.3617188, 27.7205626, "बौद्ध", "Baudha", "बौद्ध"),
    _place(25, PlaceKind.VILLAGE, 85.5200, 27.7150, "Nagarkot", "Nagarkot", "नगरकोट"),
    _place(26, PlaceKind.TOWN, 85.2775, 27.6787, "Kirtipur", "Kirtipur", "कीर्तिपुर", population=65000),
    _place(27, PlaceKind.VILLAGE, 86.7140, 27.8050, "नाम्चे बजार", "Namche Bazaar", "नाम्चे बजार"),
    _place(28, PlaceKind.VILLAGE, 86.7290, 27.6870, "Lukla", "Lukla", "लुक्ला"),
    _place(29, PlaceKind.VILLAGE, 84.4920, 27.5770, "Sauraha", "Sauraha", "सौराहा"),
    _place(30, PlaceKind.HAMLET, 84.6, 28.3, "Kathmandu", "Kathmandu"),  # far away, same name: kept
]
POIS = [
    _poi(56688296, PoiKind.STUPA, 85.362039, 27.721493, "Boudhanāth Stupa", "Boudhanath", "बौद्धनाथ स्तूप",
         alt=("Boudha Stupa",), osm_type="w", tags={"wikidata": "Q1046836"}),
    _poi(201223707, PoiKind.STUPA, 85.290396, 27.714929, "Swayambhunath", ne="स्वयम्भू", osm_type="w"),
    _poi(913170315, PoiKind.TEMPLE_HINDU, 85.3485, 27.7105, "पशुपतिनाथ", "Pashupatinath Temple", osm_type="w"),
    _poi(31, PoiKind.HERITAGE_SQUARE, 85.3070, 27.7045, "Kathmandu Durbar Square", "Kathmandu Durbar Square",
         "काठमाडौं दरबार क्षेत्र", alt=("Basantapur Durbar Square",), osm_type="w"),
    _poi(32, PoiKind.HERITAGE_SQUARE, 85.3250, 27.6730, "Patan Durbar Square", osm_type="w"),
    _poi(33, PoiKind.HERITAGE_SQUARE, 85.4280, 27.6720, "Bhaktapur Durbar Square", osm_type="w"),
    _poi(34, PoiKind.LAKE, 83.9560, 28.2150, "फेवा ताल", "Phewa Lake", "फेवा ताल", osm_type="w"),
    _poi(35, PoiKind.VIEWPOINT, 83.9470, 28.2440, "Sarangkot"),
    _poi(36, PoiKind.PEAK, 86.925, 27.988, "Mount Everest", ne="सगरमाथा", alt=("Sagarmatha", "Chomolungma"),
         ele=8849.0),
    _poi(37, PoiKind.PEAK, 83.8200, 28.5960, "Annapurna I", ne="अन्नपूर्ण", ele=8091.0),
    _poi(38, PoiKind.PEAK, 85.5, 28.2, "Langtang Lirung", ele=7234.0),
    _poi(118505122, PoiKind.AIRPORT, 85.3591, 27.6966, "त्रिभुवन अन्तर्राष्ट्रिय विमानस्थल",
         "Tribhuvan International Airport", "त्रिभुवन अन्तर्राष्ट्रिय विमानस्थल", osm_type="w"),
    _poi(39, PoiKind.BUS_STATION, 83.9890, 28.2180, "Pokhara Bus Park"),
    _poi(40, PoiKind.PARK, 85.3150, 27.7140, "Garden of Dreams", "Garden of Dreams", "सपनाको बगैंचा"),
    _poi(41, PoiKind.MUSEUM, 85.3180, 27.7150, "Narayanhiti Palace Museum"),
    _poi(42, PoiKind.LAKE, 85.3155, 27.7080, "रानी पोखरी", "Rani Pokhari", "रानी पोखरी"),
    _poi(43, PoiKind.GOMPA, 85.3630, 27.7430, "Kopan Monastery"),
    _poi(44, PoiKind.CABLE_CAR_STATION, 85.2100, 27.6670, "Chandragiri Cable Car"),
    _poi(45, PoiKind.WATERFALL, 83.9600, 28.1900, "Devi's Falls", "Devi's Falls", flags=PoiFlags.LANDMARK),
    _poi(46, PoiKind.SHOP, 85.3120, 27.7160, "Thamel Shop"),  # excluded kind
    _poi(47, PoiKind.HOT_SPRING, 83.6800, 28.4800, "Tatopani"),
    _poi(48, PoiKind.TEMPLE_HINDU, 85.3080, 27.7050, "Kasthamandap", "Kasthamandap", "काष्ठमण्डप"),
]
LANDMARKS = {56688296: "boudhanath", 201223707: "swayambhunath", 1349697740: "thamel", 31: "ktm_durbar"}
ALIASES = {118505122: ["Kathmandu Airport", "TIA"], 31: ["Basantapur Durbar Square", "Hanuman Dhoka"]}

QUERIES: list[tuple[str, int]] = [
    ("kathmandu", 10), ("Kathmandoo", 10), ("काठमाडौं", 10), ("boudha", 10), ("Bouddha", 10), ("बौद्ध", 10),
    ("Boudhanath", 5), ("thamel", 10), ("ठमेल", 10), ("pokhara", 10), ("पोखरा", 10), ("pokara", 10),
    ("durbar", 10), ("durbar square", 10), ("square", 10), ("swayambhu", 10), ("swoyambhu", 10),
    ("स्वयम्भू", 10), ("everest", 10), ("evrest", 10), ("sagarmatha", 10), ("phewa", 10), ("fewa tal", 10),
    ("lake", 10), ("airport", 10), ("pashupati", 10), ("pasupatinath", 10), ("nam", 10), ("k", 10),
    ("k", 3), ("", 10), ("   ", 10), ("xyz", 10), ("annapurna", 10), ("anapurna", 10), ("bhaktapur", 10),
    ("bhaktpur", 10), ("tribhuvan", 10), ("kat", 10), ("katmandu", 0), ("devis falls", 10), ("Devi’s", 10),
    ("lalitpur", 10), ("patan", 10), ("kirtipur", 10), ("namche", 10), ("garden", 10), ("pokhari", 10),
    ("kasthamandap", 10), ("काष्ठमण्डप", 10), ("tatopani", 10), ("chandragiri", 10), ("ZZZZZZZZ", 10),
    ("Ṭhamel", 10), ("THAMEL!!", 10), ("lukla", 1), ("sauraha", 10), ("bagmati", 10),
    # token matches (every significant word starts a key), aliases and the rank bonus
    ("tribhuvan airport", 10), ("Kathmandu Airport", 10), ("TIA", 10), ("durbar kathmandu", 10),
    ("swayambhu stupa", 10), ("tribhu airp", 10), ("the lake", 10), ("phewa lake pokhara", 10),
    ("patan square", 10), ("kathmandu durbar square", 10), ("square durbar", 10), ("boudha stupa", 10),
]

FOLD_CASES = [
    "Kathmandu", "Kathmandoo", "काठमाडौं", "Boudha", "Bouddha", "Baudha", "बौद्ध", "Swayambhu", "Swoyambhu",
    "स्वयम्भू", "Thamel", "ठमेल", "Pokhara", "पोखरा", "नेपाल", "र", "क्षेत्र", "ललितपुर", "ज्ञान", "Chowk",
    "Chowka", "Fewa", "Vishnu", "विष्णु", "Zoo", "Ward 11", "ward 1", "Devi's Falls", "it’s", "ʼok",
    "Boudhanāth Stupa", "Ṭhamel", "ॐ मणि पद्मे हुं", "संसार", "सिंह", "कँ", "दुःख", "०१२३४५६७८९", "क।ख॥ग॰घ",
    "क़ ख़ ग़ ज़ ड़ ढ़ फ़ य़", "ऋषि", "ऐ औ आ ई ऊ", "क्‍ष", "क्‌ष", "abc DEF ghi", "  multiple   spaces  ",
    "", "ññ", "Ærø", "ﬁsh", "Straße", "日本", "kkkhhh", "shh", "wwoo", "ooww", "owl", "bowa", "owo", "zzf",
    "Mixed काठमाडौं City", "ऽ", "ॲ", "ॽ", "᳐क", "क꣠ा",
]


def make_search() -> None:
    entries = search_index.build_entries(PLACES, POIS, ADMIN, LANDMARKS, aliases=ALIASES)
    data = search_index.encode_index(entries)
    write_bytes("golden.ghsi", data)
    idx = search_index.decode_index(data)

    def name(n):
        return None if n is None else [n.default, n.en, n.ne]

    out_entries = [{"name": name(e.name), "kind": e.kind, "importance": e.importance, "flags": e.flags,
                    "x": e.x, "z": e.z, "lon": e.lon, "lat": e.lat, "osm_ref": e.osm_ref,
                    "district": name(e.district), "province": name(e.province)} for e in idx.entries]
    results = []
    for q, limit in QUERIES:
        ranked = idx.rank(q, limit)
        results.append({"query": q, "limit": limit, "folded": translit.fold(q),
                        "results": [{"entry": i, "score_int": s, "score": s / search_index.SCORE_SCALE,
                                     "osm_ref": idx.entries[i].osm_ref} for s, i in ranked]})
    folds = [{"text": t, "fold": translit.fold(t), "romanize": translit.romanize(t), "clean": translit.clean(t)}
             for t in FOLD_CASES]
    osa = []
    for a, b, m in (("katmandu", "katamandu", 2), ("tamel", "tamle", 1), ("abcdef", "badcfe", 3),
                    ("ca", "abc", 3), ("pokara", "pokra", 1), ("everest", "evrest", 1), ("a", "", 1),
                    ("abcdefgh", "hgfedcba", 2), ("sbayambu", "sbayambunat", 3)):
        osa.append({"a": a, "b": b, "max_d": m, "d": search_index.osa_distance(a, b, m)})
    write_json("golden_search.json", {
        "format": "ghumante-golden-search", "version": 1, "score_scale": search_index.SCORE_SCALE,
        "entry_count": len(idx.entries), "key_count": len(idx.keys), "names": [name(n) for n in idx.names],
        "keys": [[k, i] for k, i in zip(idx.keys, idx.key_entries)], "entries": out_entries,
        "queries": results, "fold_cases": folds, "osa_cases": osa,
        "stop_words": sorted(search_index._STOP_FOLDED),
        "rank_bonus": {"landmark": search_index.RANK_BONUS_LANDMARK, "heritage": search_index.RANK_BONUS_HERITAGE,
                       "heritage_kinds": [search_index.HERITAGE_KIND_MIN, search_index.HERITAGE_KIND_MAX],
                       "token_match": search_index.TOKEN_MATCH_SCORE}})


# ---------------------------------------------------------------------------
# Routing
# ---------------------------------------------------------------------------
def _planar(lonlat: np.ndarray):
    a = np.asarray(lonlat, dtype=np.float64)
    return a[:, 0].copy(), a[:, 1].copy()


def _elev(x: np.ndarray, z: np.ndarray) -> np.ndarray:
    out = 1300.0 + 0.04 * (x - 200000.0) + 0.02 * (z - 300000.0) + 15.0 * np.sin(x / 90.0)
    out = np.where((np.abs(x - 200600.0) < 1.0) & (np.abs(z - 300600.0) < 1.0), np.nan, out)  # one unknown node
    return out


def make_network() -> list[RoadFeature]:
    """A 7 x 6 grid (spacing 300 m) of mixed roads, trails and one-ways in planar game metres."""
    x0, z0, step = 200000.0, 300000.0, 300.0
    nx, nz = 7, 6

    def nid(i, j):
        return 1000 + j * nx + i

    def pt(i, j):
        return (x0 + i * step, z0 + j * step)

    roads: list[RoadFeature] = []
    way = [500]

    def add(cls, cells, mid=None, **kw):
        pts, ids = [], []
        for k, (i, j) in enumerate(cells):
            if k and mid:
                pa, pb = pt(*cells[k - 1]), pt(i, j)
                pts.append(((pa[0] + pb[0]) / 2 + mid, (pa[1] + pb[1]) / 2 - mid))
                ids.append(90000 + way[0] * 10 + k)
            pts.append(pt(i, j))
            ids.append(nid(i, j))
        roads.append(RoadFeature(osm_id=way[0], cls=cls, lonlat=np.asarray(pts, dtype=np.float64),
                                 node_ids=np.asarray(ids, dtype=np.int64), **kw))
        way[0] += 1

    nm = NameRec("Ring Road", "Ring Road", "चक्रपथ")
    add(RoadClass.TRUNK, [(i, 0) for i in range(nx)], surface=Surface.ASPHALT, name=nm, ref="NH01")
    add(RoadClass.PRIMARY, [(0, j) for j in range(nz)], surface=Surface.ASPHALT, mid=7.3)
    add(RoadClass.SECONDARY, [(nx - 1, j) for j in range(nz)], surface=Surface.CONCRETE, bridge=True)
    add(RoadClass.RESIDENTIAL, [(i, nz - 1) for i in range(nx)], surface=Surface.BRICK,
        name=NameRec("Durbar Marg", "Durbar Marg", "दरबार मार्ग"))
    add(RoadClass.TERTIARY, [(i, 2) for i in range(nx)], surface=Surface.GRAVEL, oneway=1)
    add(RoadClass.UNCLASSIFIED, [(i, 3) for i in range(nx - 1, -1, -1)], surface=Surface.DIRT, oneway=-1)
    add(RoadClass.TRACK, [(3, j) for j in range(nz)], surface=Surface.MUD, ford=True, sac_scale=SacScale.HIKING)
    add(RoadClass.PATH, [(1, j) for j in range(nz)], surface=Surface.DIRT, sac_scale=SacScale.MOUNTAIN_HIKING,
        access=Travel.FOOT | Travel.BICYCLE | Travel.HORSE, mid=-11.0)
    add(RoadClass.FOOTWAY, [(5, j) for j in range(nz)], surface=Surface.ASPHALT,
        access=Travel.FOOT | Travel.BICYCLE)
    add(RoadClass.STEPS, [(2, 1), (2, 2), (2, 3), (2, 4)], surface=Surface.ROCK, access=Travel.FOOT)
    add(RoadClass.PATH, [(4, 3), (4, 4), (4, 5)], surface=Surface.ROCK, sac_scale=SacScale.DEMANDING_ALPINE_HIKING,
        access=Travel.FOOT)
    add(RoadClass.SERVICE, [(1, 1), (2, 1), (3, 1)], surface=Surface.COMPACTED, is_link=True, tunnel=True)
    add(RoadClass.LIVING_STREET, [(4, 1), (5, 1), (6, 1)], surface=Surface.COBBLE)
    add(RoadClass.CYCLEWAY, [(4, 4), (5, 4), (6, 4)], surface=Surface.ASPHALT, access=Travel.BICYCLE | Travel.FOOT)
    add(RoadClass.BRIDLEWAY, [(2, 4), (2, 5)], surface=Surface.GRASS, access=Travel.HORSE | Travel.FOOT)
    # an isolated piece (unreachable from the grid) and a lollipop loop
    roads.append(RoadFeature(osm_id=900, cls=RoadClass.RESIDENTIAL,
                             lonlat=np.array([[x0 + 3000.0, z0 + 3000.0], [x0 + 3300.0, z0 + 3000.0]]),
                             node_ids=np.array([7001, 7002], dtype=np.int64)))
    roads.append(RoadFeature(osm_id=901, cls=RoadClass.SERVICE,
                             lonlat=np.array([pt(6, 5), (x0 + 1900.0, z0 + 1700.0), (x0 + 2000.0, z0 + 1600.0),
                                              (x0 + 2100.0, z0 + 1700.0), pt(6, 5)]),
                             node_ids=np.array([nid(6, 5), 8001, 8002, 8003, nid(6, 5)], dtype=np.int64)))
    return roads


def make_routing() -> None:
    write_json("golden_profiles.json", routing.travel_profiles_table())
    g = routing.build_graph(make_network(), to_game=_planar, elev=_elev)
    data = routing.encode_graph(g)
    write_bytes("golden.ghrg", data)
    g = routing.decode_graph(data)
    n = g.node_count

    rng = np.random.default_rng(20261004)
    pairs = [(0, n - 1), (n - 1, 0), (3, 30), (30, 3), (5, 5)]
    pairs += [tuple(int(v) for v in rng.integers(0, n, 2)) for _ in range(14)]
    routes = []
    for t in routing.PROFILES:
        for s, d in pairs:
            r = routing.route(g, s, d, t)
            routes.append({"profile": t.name, "src": s, "dst": d, "found": r is not None,
                           "nodes": None if r is None else r.nodes, "edges": None if r is None else r.edges,
                           "time_s": None if r is None else r.time_s,
                           "length_m": None if r is None else r.length_m})

    nearest = []
    for k in range(12):
        x = 199900.0 + float(rng.integers(0, 2400)) + 0.25
        z = 299900.0 + float(rng.integers(0, 1900)) + 0.75
        for t in (Travel.FOOT, Travel.CAR, Travel.BICYCLE):
            for incoming in (False, True):
                for main in (True, False):
                    nearest.append({"x": x, "z": z, "profile": t.name, "incoming": incoming, "main_network": main,
                                    "node": routing.nearest_node(g, x, z, t, incoming=incoming, main_network=main)})
    # Exact ties (midpoints between nodes) and points near the isolated piece.
    for x, z in ((200150.0, 300000.0), (200000.0, 300150.0), (203150.0, 302990.0), (203000.0, 303000.0)):
        for t in (Travel.FOOT, Travel.CAR):
            for incoming in (False, True):
                for main in (True, False):
                    nearest.append({"x": x, "z": z, "profile": t.name, "incoming": incoming, "main_network": main,
                                    "node": routing.nearest_node(g, x, z, t, incoming=incoming, main_network=main)})
    main_component = {t.name: routing.main_component(g, t).tolist() for t in routing.PROFILES}
    snap = {f"{t.name}/{d}": routing.snap_nodes(g, t, d == "in").tolist() for t in routing.PROFILES
            for d in ("out", "in")}
    times = {t.name: [None if math.isinf(v) else float(v) for v in routing.edge_times(g, t)]
             for t in routing.PROFILES}

    write_json("golden_routes.json", {
        "format": "ghumante-golden-routes", "version": 1,
        "node_count": n, "edge_count": g.edge_count, "geom_bytes": len(g.geometry),
        "node_x_dm": g.node_x_dm.tolist(), "node_z_dm": g.node_z_dm.tolist(), "node_elev": g.node_elev.tolist(),
        "offsets": g.offsets.tolist(),
        "edges": {name: getattr(g, name).tolist() for name, _, _ in routing._EDGE_FIELDS},
        "edge_geometry_dm": [g.edge_geometry_dm(e).reshape(-1).tolist() for e in range(g.edge_count)],
        "names": [[nm.default, nm.en, nm.ne] for nm in g.names],
        "edge_times": times, "routes": routes, "nearest": nearest, "main_component": main_component,
        "snap_nodes": snap})


# ---------------------------------------------------------------------------
# Enums.cs
# ---------------------------------------------------------------------------
ENUM_TYPES = {"PoiKind": "ushort"}
FLAG_ENUMS = {"Travel", "RoadFlags", "BuildingFlags", "PoiFlags"}


def pascal(name: str) -> str:
    return "".join(w[:1].upper() + w[1:].lower() for w in name.split("_"))


def write_enums_cs() -> None:
    spec = json.loads((ROOT / "shared" / "enums.json").read_text(encoding="utf-8"))
    lines = [
        "// <auto-generated>",
        "// Generated by shared/golden/make_golden.py --enums-cs from shared/enums.json (itself exported from",
        "// pipeline/ghumante_pipeline/model.py). Do not edit by hand: enums are append-only, and",
        "// core-tests/EnumsTests.cs checks every name and value against shared/enums.json.",
        "// </auto-generated>",
        "using System;",
        "",
        "namespace Ghumante.Core.Data",
        "{",
    ]
    for ename, members in spec["enums"].items():
        if ename in FLAG_ENUMS:
            lines.append("    [Flags]")
        lines.append(f"    public enum {ename} : {ENUM_TYPES.get(ename, 'byte')}")
        lines.append("    {")
        if ename in FLAG_ENUMS:
            lines.append("        None = 0,")
        for m, v in members.items():
            lines.append(f"        {pascal(m)} = {v},")
        lines.append("    }")
        lines.append("")
    lines += [
        "    /// <summary>Physics family of each surface (model.SURFACE_GROUP).</summary>",
        "    public static class SurfaceGroups",
        "    {",
        "        private static readonly SurfaceGroup[] Table =",
        "        {",
    ]
    for s in spec["enums"]["Surface"]:
        lines.append(f"            SurfaceGroup.{pascal(spec['surface_group'][s])}, // {pascal(s)}")
    lines += [
        "        };",
        "",
        "        /// <summary>The group of a surface; unknown (newer) values count as DIRT, as in routing.py.</summary>",
        "        public static SurfaceGroup Of(Surface s)",
        "        {",
        "            int i = (int)s;",
        "            return i < Table.Length ? Table[i] : SurfaceGroup.Dirt;",
        "        }",
        "    }",
        "",
        "    public static class TravelMasks",
        "    {",
        "        /// <summary>Every travel mode (model.ALL_TRAVEL).</summary>",
        "        public const Travel All = (Travel)0x7F;",
        "    }",
        "}",
    ]
    (ROOT / "game/Assets/Ghumante/Core/Data/Enums.cs").write_text("\n".join(lines) + "\n", encoding="utf-8")


def main(argv: list[str] | None = None) -> None:
    global OUT
    args = sys.argv[1:] if argv is None else argv
    if "--out" in args:  # used by pipeline/tests/test_golden.py to check determinism
        OUT = Path(args[args.index("--out") + 1]).resolve()
        OUT.mkdir(parents=True, exist_ok=True)
    if "--enums-cs" in args:
        write_enums_cs()
    make_tm84()
    make_tiles_and_pack()
    make_search()
    make_routing()
    total = sum(p.stat().st_size for p in OUT.iterdir() if p.is_file() and p.name != Path(__file__).name)
    print(f"golden files written to {OUT} ({total / 1024:.1f} KiB)")


if __name__ == "__main__":
    main()
