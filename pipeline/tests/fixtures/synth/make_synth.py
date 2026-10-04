"""Synthetic end-to-end fixture: a 2 km x 2 km invented neighbourhood near Thamel.

``make_synth(dest)`` writes, under ``dest``::

    osm/synth.osm                     OSM XML: street grid, trails, ~380 buildings (one
                                      multipolygon with a courtyard), a lake, a forest
                                      multipolygon with a hole, a park, a river, POIs,
                                      two places and a district boundary
    dem/synth_dem.tif                 float32 lon/lat DEM, 2 arc-second pixels, smooth hills
    worldcover/synth_worldcover.tif   uint8 WorldCover classes, 1/3000 degree pixels
    landmarks.resolved.json           the temple as a hero landmark

and returns a ``Synth`` with the region definition and the ground truth the
tests check against (source coordinates, ids). Everything is deterministic: no
randomness, fixed ids starting at ``ID_BASE``. Some features are placed on
purpose across leaf-tile borders (computed from the projection): one building,
the lake (on a tile corner) and every street.

Run ``python make_synth.py OUT_DIR`` to inspect the files.
"""

from __future__ import annotations

import json
import math
import sys
from dataclasses import dataclass, field
from pathlib import Path
from xml.sax.saxutils import quoteattr

import numpy as np
import rasterio
from rasterio.transform import Affine

PIPELINE_ROOT = Path(__file__).resolve().parents[3]
if str(PIPELINE_ROOT) not in sys.path:
    sys.path.insert(0, str(PIPELINE_ROOT))

from ghumante_pipeline import projection  # noqa: E402
from ghumante_pipeline.config import Region  # noqa: E402

ID_BASE = 900_000_000
BBOX = (85.300, 27.700, 85.320, 27.718)
HORIZON_BBOX = (85.290, 27.690, 85.330, 27.728)
LEAF = 10

DEM_PPD = 1800  # 2 arc-seconds
DEM_BOX = (85.20, 27.60, 85.42, 27.82)
WC_PPD = 3000
WC_BOX = (85.22, 27.62, 85.40, 27.80)

TEMPLE_NAME = "Kalo Bhairab Mandir"
TEMPLE_NAME_NE = "काल भैरव मन्दिर"
PLACE_A = ("Synthtol", 85.3018, 27.7022)
PLACE_B = ("Bagh Synth", 85.3172, 27.7148)


def synth_region(region_id: str = "synth_test") -> Region:
    return Region(id=region_id, name_en="Synth (test)", name_ne="सिन्थ (परीक्षण)", bbox=BBOX,
                  horizon_bbox=HORIZON_BBOX, detail_levels=(9, 10), horizon_levels=(7, 8), height_grid=65,
                  biome_grid=33, bbox_buffer_m=300.0, milestone="test")


def dem_height(lon, lat):
    """Smooth synthetic terrain (metres): a valley floor rising to the north-east with hills."""
    lon = np.asarray(lon, dtype=np.float64)
    lat = np.asarray(lat, dtype=np.float64)
    return (1300.0 + 900.0 * (lat - 27.70) + 500.0 * (lon - 85.30)
            + 40.0 * np.sin((lon - 85.30) * 260.0) * np.cos((lat - 27.70) * 190.0)
            + 200.0 * np.exp(-(((lon - 85.36) / 0.03) ** 2 + ((lat - 27.76) / 0.03) ** 2)))


@dataclass
class Synth:
    root: Path
    osm: Path
    region: Region
    landmarks: Path
    nodes: dict[int, tuple[float, float]] = field(default_factory=dict)
    buildings: dict[int, list[int]] = field(default_factory=dict)  # way id -> node ids (open ring)
    building_relation: int = 0
    temple_way: int = 0
    roads: dict[int, list[int]] = field(default_factory=dict)  # way id -> node ids
    poi_nodes: dict[int, str] = field(default_factory=dict)  # node id -> name
    places: dict[str, int] = field(default_factory=dict)  # name -> node id
    lake_way: int = 0
    border_building: int = 0

    def lonlat(self, node_ids) -> np.ndarray:
        return np.array([self.nodes[n] for n in node_ids], dtype=np.float64)


class _Osm:
    def __init__(self) -> None:
        self.next_id = ID_BASE
        self.nodes: dict[int, tuple[float, float, dict]] = {}
        self.ways: list[tuple[int, list[int], dict]] = []
        self.rels: list[tuple[int, list[tuple[str, int, str]], dict]] = []

    def nid(self) -> int:
        self.next_id += 1
        return self.next_id

    def node(self, lon: float, lat: float, tags: dict | None = None) -> int:
        i = self.nid()
        self.nodes[i] = (round(lon, 7), round(lat, 7), tags or {})
        return i

    def way(self, nodes: list[int], tags: dict | None = None) -> int:
        i = self.nid()
        self.ways.append((i, nodes, tags or {}))
        return i

    def ring(self, pts: list[tuple[float, float]], tags: dict | None = None) -> tuple[int, list[int]]:
        ids = [self.node(x, y) for x, y in pts]
        return self.way(ids + [ids[0]], tags), ids

    def rel(self, members: list[tuple[str, int, str]], tags: dict) -> int:
        i = self.nid()
        self.rels.append((i, members, tags))
        return i

    def xml(self) -> str:
        out = ['<?xml version="1.0" encoding="UTF-8"?>', '<osm version="0.6" generator="ghumante-synth">']

        def tag_lines(tags: dict, ind: str) -> list[str]:
            return [f"{ind}<tag k={quoteattr(k)} v={quoteattr(str(v))}/>" for k, v in tags.items()]

        for i in sorted(self.nodes):
            lon, lat, tags = self.nodes[i]
            head = f'  <node id="{i}" version="1" lat="{lat:.7f}" lon="{lon:.7f}"'
            if tags:
                out.append(head + ">")
                out += tag_lines(tags, "    ")
                out.append("  </node>")
            else:
                out.append(head + "/>")
        for i, nds, tags in self.ways:
            out.append(f'  <way id="{i}" version="1">')
            out += [f'    <nd ref="{n}"/>' for n in nds]
            out += tag_lines(tags, "    ")
            out.append("  </way>")
        for i, members, tags in self.rels:
            out.append(f'  <relation id="{i}" version="1">')
            out += [f'    <member type="{t}" ref="{r}" role="{role}"/>' for t, r, role in members]
            out += tag_lines(tags, "    ")
            out.append("  </relation>")
        out.append("</osm>")
        return "\n".join(out) + "\n"


def _rect(cx: float, cy: float, w: float, h: float, rot: float = 0.0) -> list[tuple[float, float]]:
    """Counter-clockwise rectangle in lon/lat (w, h in degrees)."""
    c, s = math.cos(rot), math.sin(rot)
    pts = []
    for dx, dy in ((-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2)):
        pts.append((cx + dx * c - dy * s, cy + dx * s + dy * c))
    return pts


def _ellipse(cx: float, cy: float, rx: float, ry: float, n: int = 24) -> list[tuple[float, float]]:
    return [(cx + rx * math.cos(2 * math.pi * k / n), cy + ry * math.sin(2 * math.pi * k / n)) for k in range(n)]


def _leaf_border_lonlat(tx: int, ty: int) -> tuple[float, float]:
    """Lon/lat of the south-west corner of leaf tile (tx, ty)."""
    s = projection.tile_size(LEAF)
    lon, lat = projection.game_to_lonlat(tx * s, ty * s)
    return float(lon), float(lat)


def make_synth(dest: Path) -> Synth:
    dest = Path(dest)
    for sub in ("osm", "dem", "worldcover"):
        (dest / sub).mkdir(parents=True, exist_ok=True)
    o = _Osm()
    syn = Synth(root=dest, osm=dest / "osm" / "synth.osm", region=synth_region(),
                landmarks=dest / "landmarks.resolved.json")

    # --- street grid --------------------------------------------------------------
    lons = [85.3008 + 0.0026 * i for i in range(8)]  # 85.3008 .. 85.3190
    lats = [27.7012 + 0.0026 * j for j in range(7)]  # 27.7012 .. 27.7168
    inter = {(i, j): o.node(lons[i], lats[j]) for i in range(len(lons)) for j in range(len(lats))}

    def street(nodes: list[int], tags: dict) -> int:
        w = o.way(nodes, tags)
        syn.roads[w] = nodes
        return w

    for j, lat in enumerate(lats):  # east-west streets, with a wiggling midpoint per block
        seq = [o.node(85.2985, lat)]
        for i in range(len(lons)):
            seq.append(inter[(i, j)])
            if i + 1 < len(lons):
                seq.append(o.node(0.5 * (lons[i] + lons[i + 1]), lat + 0.00012 * math.sin(i + j)))
        seq.append(o.node(85.3215, lat))
        if j == 3:
            tags = {"highway": "primary", "name": "Synth Marg", "name:ne": "सिन्थ मार्ग", "ref": "H01",
                    "surface": "asphalt", "lanes": "2"}
        elif j == 0:
            tags = {"highway": "track", "tracktype": "grade2"}
        elif j == 5:
            tags = {"highway": "residential", "oneway": "-1", "name": "Ulto Galli"}
        else:
            tags = {"highway": "residential"}
        street(seq, tags)
    for i, lon in enumerate(lons):  # north-south streets
        seq = [o.node(lon, 27.6990)]
        for j in range(len(lats)):
            seq.append(inter[(i, j)])
            if j + 1 < len(lats):
                seq.append(o.node(lon + 0.0001 * math.cos(i * 2 + j), 0.5 * (lats[j] + lats[j + 1])))
        seq.append(o.node(lon, 27.7195))
        if i == 4:
            tags = {"highway": "tertiary", "name": "Bhairab Marg", "surface": "paving_stones"}
        elif i == 1:
            tags = {"highway": "residential", "oneway": "yes", "surface": "concrete"}
        else:
            tags = {"highway": "residential"}
        street(seq, tags)
    # Trails: a path up to a viewpoint and a flight of steps.
    path_nodes = [inter[(7, 6)]] + [o.node(85.3190 + 0.0004 * k, 27.7168 + 0.0003 * k + 0.0001 * (k % 2))
                                    for k in range(1, 6)]
    street(path_nodes, {"highway": "path", "name": "Synth Danda Trail"})
    street([inter[(2, 4)], o.node(85.3060, 27.7124), o.node(85.3063, 27.7130)], {"highway": "steps"})

    # --- buildings ---------------------------------------------------------------------
    bw, bh = 0.00012, 0.00010  # about 12 m x 11 m
    k = 0
    for i in range(len(lons) - 1):
        for j in range(len(lats) - 1):
            if (i, j) in ((5, 1), (2, 2)):
                continue  # courtyard house and temple go here
            for a in range(3):
                for b in range(3):
                    cx = lons[i] + 0.0006 + 0.0007 * a
                    cy = lats[j] + 0.0006 + 0.0007 * b
                    tags = {"building": "yes"}
                    if k % 7 == 0:
                        tags["building:levels"] = str(2 + k % 4)
                    if k % 11 == 0:
                        tags["building"] = "house"
                    if k % 13 == 0:
                        tags["roof:shape"] = "flat"
                    w, ids = o.ring(_rect(cx, cy, bw, bh, 0.1 * ((a + b) % 3)), tags)
                    syn.buildings[w] = ids
                    k += 1
    # The temple (a building that is also a POI) and a multipolygon courtyard house.
    tw, tids = o.ring(_rect(lons[2] + 0.0013, lats[2] + 0.0013, 0.00016, 0.00016),
                      {"building": "temple", "amenity": "place_of_worship", "religion": "hindu",
                       "name": TEMPLE_NAME, "name:ne": TEMPLE_NAME_NE})
    syn.buildings[tw] = tids
    syn.temple_way = tw
    cx, cy = lons[5] + 0.0013, lats[1] + 0.0013
    ow, oids = o.ring(_rect(cx, cy, 0.0004, 0.00036))
    iw, iids = o.ring(_rect(cx, cy, 0.00014, 0.00012)[::-1])
    syn.building_relation = o.rel([("way", ow, "outer"), ("way", iw, "inner")],
                                  {"type": "multipolygon", "building": "yes", "name": "Synth Bahal"})
    for n in oids + iids:
        syn.nodes[n] = o.nodes[n][:2]
    # A building centred on a leaf-tile border (x = 517 * 1024 m), so its footprint crosses it.
    blon, _ = _leaf_border_lonlat(517, 162)
    bw_id, bids = o.ring(_rect(blon, 27.7130, 0.00020, 0.00012), {"building": "yes", "name": "Border Ghar"})
    syn.buildings[bw_id] = bids
    syn.border_building = bw_id

    # --- areas -----------------------------------------------------------------------
    clon, clat = _leaf_border_lonlat(517, 162)  # a leaf-tile corner inside the bbox
    lw, _ = o.ring(_ellipse(clon, clat, 0.0011, 0.0008),
                   {"natural": "water", "water": "lake", "name": "Synth Pokhari"})
    syn.lake_way = lw
    fo, _ = o.ring(_rect(85.3060, 27.7150, 0.0040, 0.0030))
    fi, _ = o.ring(_rect(85.3060, 27.7150, 0.0012, 0.0010)[::-1])
    o.rel([("way", fo, "outer"), ("way", fi, "inner")], {"type": "multipolygon", "landuse": "forest"})
    o.ring(_rect(85.3155, 27.7035, 0.0016, 0.0012), {"leisure": "park", "name": "Synth Bagaincha"})
    o.ring([(85.2990, 27.6995), (85.3210, 27.6995), (85.3210, 27.7185), (85.2990, 27.7185)],
           {"landuse": "residential"})

    # --- river -----------------------------------------------------------------------
    river = [o.node(85.2980 + 0.0012 * t, 27.7088 + 0.0004 * math.sin(t * 0.7)) for t in range(21)]
    o.way(river, {"waterway": "river", "name": "Synth Khola"})

    # --- POIs and places -----------------------------------------------------------------
    for lon, lat, tags in (
        (85.3105, 27.7062, {"amenity": "place_of_worship", "religion": "buddhist", "name": "Synth Gompa"}),
        (85.3209, 27.7181, {"tourism": "viewpoint", "name": "Synth Danda Viewpoint"}),
        (85.3125, 27.7110, {"amenity": "cafe", "name": "Cafe Synth"}),
        (85.3195, 27.7175, {"natural": "peak", "ele": "1405", "name": "Synth Danda"}),
    ):
        n = o.node(lon, lat, tags)
        syn.poi_nodes[n] = tags["name"]
    for name, lon, lat in (PLACE_A, PLACE_B):
        n = o.node(lon, lat, {"place": "neighbourhood" if name == PLACE_A[0] else "suburb", "name": name})
        syn.places[name] = n

    # --- district boundary -----------------------------------------------------------------
    bnodes = [o.node(x, y) for x, y in ((85.290, 27.690), (85.330, 27.690), (85.330, 27.728), (85.290, 27.728))]
    bway = o.way(bnodes + [bnodes[0]], {})
    o.rel([("way", bway, "outer")], {"type": "boundary", "boundary": "administrative", "admin_level": "6",
                                     "name": "Synth District", "name:ne": "सिन्थ जिल्ला"})

    syn.nodes.update({i: (v[0], v[1]) for i, v in o.nodes.items()})
    syn.osm.write_text(o.xml(), encoding="utf-8")
    syn.landmarks.write_text(json.dumps([{"id": "synth_temple", "region": "synth_test", "status": "found",
                                          "osm": f"w{syn.temple_way}", "name": TEMPLE_NAME}], indent=1),
                             encoding="utf-8")
    _write_dem(dest / "dem" / "synth_dem.tif")
    _write_worldcover(dest / "worldcover" / "synth_worldcover.tif")
    return syn


def _write_dem(path: Path) -> None:
    lon0, lat0, lon1, lat1 = DEM_BOX
    w, h = round((lon1 - lon0) * DEM_PPD), round((lat1 - lat0) * DEM_PPD)
    lon = lon0 + (np.arange(w) + 0.5) / DEM_PPD
    lat = lat1 - (np.arange(h) + 0.5) / DEM_PPD
    data = dem_height(lon[None, :], lat[:, None]).astype(np.float32)
    with rasterio.open(path, "w", driver="GTiff", width=w, height=h, count=1, dtype="float32", crs="EPSG:4326",
                       transform=Affine(1.0 / DEM_PPD, 0.0, lon0, 0.0, -1.0 / DEM_PPD, lat1), nodata=-32767.0,
                       compress="deflate") as ds:
        ds.write(data, 1)


def _write_worldcover(path: Path) -> None:
    lon0, lat0, lon1, lat1 = WC_BOX
    w, h = round((lon1 - lon0) * WC_PPD), round((lat1 - lat0) * WC_PPD)
    lon = (lon0 + (np.arange(w) + 0.5) / WC_PPD)[None, :]
    lat = (lat1 - (np.arange(h) + 0.5) / WC_PPD)[:, None]
    wc = np.full((h, w), 30, dtype=np.uint8)  # grassland
    wc[np.broadcast_to(lon < 85.300, wc.shape)] = 40  # cropland to the west
    wc[np.broadcast_to(lat > 27.722, wc.shape)] = 10  # forest to the north
    town = (lon >= 85.300) & (lon <= 85.322) & (lat >= 27.699) & (lat <= 27.719)
    wc[town] = 50
    clon, clat = _leaf_border_lonlat(517, 162)
    lake = ((lon - clon) / 0.0011) ** 2 + ((lat - clat) / 0.0008) ** 2 <= 1.0
    wc[lake] = 80
    with rasterio.open(path, "w", driver="GTiff", width=w, height=h, count=1, dtype="uint8", crs="EPSG:4326",
                       transform=Affine(1.0 / WC_PPD, 0.0, lon0, 0.0, -1.0 / WC_PPD, lat1), nodata=0,
                       compress="deflate") as ds:
        ds.write(wc, 1)


@dataclass
class SynthBuild:
    synth: Synth
    out_dir: Path
    reg_dir: Path
    manifest: dict

    @property
    def pack(self) -> Path:
        return self.reg_dir / f"{self.synth.region.id}.ghpk"

    @property
    def index(self) -> Path:
        return self.reg_dir / f"{self.synth.region.id}.search.ghsi"

    @property
    def graph(self) -> Path:
        return self.reg_dir / f"{self.synth.region.id}.route.ghrg"

    @property
    def qa_dir(self) -> Path:
        return self.reg_dir / "qa"


_BUILDS: dict[str, SynthBuild] = {}


def synth_build(base: Path, *, workers: int = 2, qa: bool = True, use_cache: bool = True) -> SynthBuild:
    """Generate the fixture under ``base/src`` and build it into ``base/out``
    (once per ``base`` per process)."""
    from ghumante_pipeline.build import build_region

    base = Path(base)
    key = str(base.resolve())
    if key not in _BUILDS:
        syn = make_synth(base / "src")
        out = base / "out"
        m = build_region(syn.region, pbf=syn.osm, raw_dir=syn.root, out_dir=out, qa=qa, use_cache=use_cache,
                         workers=workers, landmarks_path=syn.landmarks)
        _BUILDS[key] = SynthBuild(syn, out, out / "regions" / syn.region.id, m)
    return _BUILDS[key]


if __name__ == "__main__":
    out = Path(sys.argv[1] if len(sys.argv) > 1 else "synth_out")
    s = make_synth(out)
    print(f"wrote {s.osm} ({len(s.buildings)} buildings, {len(s.roads)} roads)")
