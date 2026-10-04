"""Extract one region's OSM features into a ``model.Extract``.

Reads the Nepal PBF (or any ``.osm``/``.osm.pbf`` file) with pyosmium and
produces typed features in lon/lat: roads, buildings, POIs, places, areas,
lines and admin boundaries. All tag parsing goes through ``tags.py``.

Two passes over the file:

* **Pass A** reads relations only (a few seconds for Nepal). It keeps the
  multipolygon (and protected-area boundary) relations that will become a
  building, area, POI or place, and the ``boundary=administrative`` relations
  at the requested ``admin_levels``, and collects their member way ids.
  Relations come after ways in a PBF, so this has to happen first.
* **Pass B** reads nodes and ways with a node-location cache. Every way is
  seen once in Python: member ways of the relations from pass A have their
  geometry captured wherever they are (admin boundaries extend far outside the
  region), and tagged ways are turned into features when they touch the
  region. Building ways (8.3 M of the 9.5 M ways in Nepal) are rejected on
  their first node before anything else is computed.

Relations are then assembled in Python with shapely: member ways are chained
into rings by shared end-node ids, outer rings are unioned, inner rings
subtracted (outer rings nested in an inner ring are added back), and the result
is made valid. Relations whose member bounding box misses the region are never
assembled.

Spatial filter (``box`` = region bbox grown by ``buffer_m``):

* roads and lines: kept whole when any node is inside ``box`` (the tiler clips);
* buildings and areas: kept when their bounding box intersects ``box``;
* POIs and places: kept when their point is inside ``box``;
* admin areas: kept when the assembled polygon intersects the *unbuffered*
  bbox.

An object with a ``building`` tag becomes a building, never also an area, so a
temple footprint tagged ``amenity=place_of_worship`` does not also turn into a
RELIGIOUS compound. Ways and areas with a ``poi_kind`` also give a POI at their
centroid (or a point on the surface when the centroid falls outside, or the
midpoint of an open way). A multipolygon building with several outer parts
keeps its largest part only, so every building has one record per OSM object
(``stats["buildings_relation_parts_dropped"]`` counts the rest).

Outputs are sorted (roads and lines by way id, buildings by ``osm_ref``, the
others by type then id), so the same input gives the same ``Extract``.
"""

from __future__ import annotations

import gzip
import io
import math
import pickle
import time
from collections import Counter
from dataclasses import dataclass, field
from pathlib import Path

import numpy as np
import osmium
import shapely
from shapely.geometry import LineString, MultiPolygon, Polygon, box as shapely_box

from . import tags as T
from .model import (
    AdminArea,
    AreaFeature,
    AreaKind,
    BuildingFeature,
    BuildingFlags,
    Extract,
    LineFeature,
    LineKind,
    NameRec,
    PlaceFeature,
    PlaceKind,
    PoiFeature,
    PoiFlags,
    PoiKind,
    RoadClass,
    RoadFeature,
    RoofMaterial,
    RoofShape,
    WallMaterial,
)

COORD_SCALE = 10_000_000  # osmium fixed-point: 1e-7 degree
M_PER_DEG_LAT = 111_320.0
# Building ways are rejected when their first node is further than this outside
# the box. Footprints larger than this (about 1 km) are not real buildings.
BUILDING_FIRST_NODE_SLACK_DEG = 0.01

ADMIN_LEVEL_KIND: dict[int, PlaceKind] = {
    2: PlaceKind.COUNTRY,
    4: PlaceKind.PROVINCE,
    6: PlaceKind.DISTRICT,
    7: PlaceKind.LOCAL_LEVEL,
    9: PlaceKind.WARD,
}

# Small whitelists of raw tags carried on features for later stages.
POI_TAG_KEYS = (
    "amenity", "tourism", "historic", "natural", "man_made", "religion", "denomination", "place_of_worship",
    "heritage", "wikidata", "wikipedia", "building", "leisure", "shop", "sport", "aeroway", "iata", "icao",
    "opening_hours", "website", "cuisine", "stars", "waterway",
)
AREA_TAG_KEYS = (
    "natural", "landuse", "leisure", "amenity", "water", "wetland", "religion", "place", "boundary",
    "protect_class", "aeroway", "crop", "trees", "intermittent", "wikidata",
)
LINE_TAG_KEYS = (
    "waterway", "railway", "aerialway", "aeroway", "barrier", "historic", "man_made", "intermittent",
    "tunnel", "bridge", "layer", "usage", "wikidata",
)
_RELIGIOUS_POI = frozenset(int(k) for k in PoiKind if 100 <= int(k) < 120)


def _subset(tags: dict[str, str], keys: tuple[str, ...]) -> dict[str, str]:
    return {k: tags[k] for k in keys if k in tags}


def _is_yes_not_no(v: str | None) -> bool:
    """True for any present value except ``no``/``false``/``0`` (``bridge=viaduct``)."""
    if v is None:
        return False
    s = v.strip().lower()
    return bool(s) and s not in ("no", "false", "0")


def _parse_layer(v: str | None) -> int:
    if v is None:
        return 0
    try:
        return max(-5, min(5, int(round(float(v.split(";")[0].strip())))))
    except ValueError:
        return 0


def _parse_population(v: str | None) -> int | None:
    if not v:
        return None
    digits = "".join(c for c in v.split(";")[0] if c.isdigit())
    return int(digits) if digits and len(digits) < 12 else None


def _has_building(tags) -> bool:
    v = tags.get("building")
    return v is not None and v.strip().lower() not in ("no", "")


# ---------------------------------------------------------------------------
# Geometry helpers (lon/lat)
# ---------------------------------------------------------------------------
def signed_area(ring: np.ndarray) -> float:
    """Shoelace signed area of an (N, 2) ring (closed or not); > 0 is counter-clockwise."""
    x, y = ring[:, 0], ring[:, 1]
    return 0.5 * float(np.dot(x, np.roll(y, -1)) - np.dot(np.roll(x, -1), y))


def _open_ring(coords: np.ndarray) -> np.ndarray:
    """Drop the closing point and consecutive duplicates."""
    c = np.asarray(coords, dtype=np.float64)
    if len(c) > 1 and np.array_equal(c[0], c[-1]):
        c = c[:-1]
    if len(c) > 1:
        keep = np.ones(len(c), dtype=bool)
        keep[1:] = np.any(c[1:] != c[:-1], axis=1)
        c = c[keep]
    return c


def _orient(ring: np.ndarray, ccw: bool) -> np.ndarray:
    a = signed_area(ring)
    if (a > 0) != ccw:
        ring = ring[::-1].copy()
    return ring


def _polygonal(geom) -> Polygon | MultiPolygon | None:
    """Valid polygonal part of ``geom`` or None."""
    if geom is None or geom.is_empty:
        return None
    if not geom.is_valid:
        geom = shapely.make_valid(geom)
    if isinstance(geom, (Polygon, MultiPolygon)):
        return None if geom.area <= 0 else geom
    polys = [g for g in getattr(geom, "geoms", []) if isinstance(g, (Polygon, MultiPolygon)) and not g.is_empty]
    if not polys:
        return None
    out = shapely.union_all(polys)
    return out if not out.is_empty and out.area > 0 else None


def _ring_polygon(coords: np.ndarray) -> Polygon | MultiPolygon | None:
    c = _open_ring(coords)
    if len(c) < 3:
        return None
    return _polygonal(Polygon(c))


def _label_point(geom) -> tuple[float, float]:
    """Centroid when it lies on the geometry, else a point on its surface."""
    if isinstance(geom, LineString):
        p = geom.interpolate(0.5, normalized=True)
        return float(p.x), float(p.y)
    c = geom.centroid
    if not geom.contains(c):
        c = geom.representative_point()
    return float(c.x), float(c.y)


def _polygon_parts(geom) -> list[Polygon]:
    if geom is None:
        return []
    if isinstance(geom, Polygon):
        return [geom]
    return [g for g in geom.geoms if isinstance(g, Polygon)]


def build_rings(segments: list[tuple[int, int, np.ndarray]]) -> tuple[list[np.ndarray], int]:
    """Chain ways into closed rings by their end-node ids.

    ``segments`` are ``(first_node_id, last_node_id, coords)``. Returns the
    closed rings (first point repeated at the end) and the number of segments
    that could not be closed into a ring. Deterministic: segments are used in
    the order given.
    """
    rings: list[np.ndarray] = []
    ends: dict[int, list[int]] = {}
    for i, (a, b, _c) in enumerate(segments):
        if a != b:
            ends.setdefault(a, []).append(i)
            ends.setdefault(b, []).append(i)
    used = [False] * len(segments)
    unclosed = 0
    for i, (a, b, c) in enumerate(segments):
        if used[i]:
            continue
        used[i] = True
        if a == b:
            if len(c) >= 4:
                rings.append(c)
            else:
                unclosed += 1
            continue
        parts = [c]
        start, end = a, b
        n_used = 1
        while end != start:
            nxt = next((j for j in ends.get(end, ()) if not used[j]), None)
            if nxt is None:
                break
            used[nxt] = True
            n_used += 1
            ja, jb, jc = segments[nxt]
            if ja == end:
                parts.append(jc[1:])
                end = jb
            else:
                parts.append(jc[::-1][1:])
                end = ja
        if end == start:
            ring = np.concatenate(parts)
            if len(ring) >= 4:
                rings.append(ring)
                continue
        unclosed += n_used
    return rings, unclosed


def assemble_multipolygon(members: list[tuple[str, int, int, np.ndarray]]) -> tuple[object | None, int]:
    """Polygon from multipolygon member ways ``(role, first_id, last_id, lonlat)``.

    Roles ``outer`` and empty are outer rings, ``inner`` holes; other roles are
    ignored. Outer rings are unioned and inner rings subtracted. Outer rings
    lying inside an inner ring (an island in a lake in an island) are added back.
    Returns ``(geometry or None, unclosed segment count)``.
    """
    outer_segs = [(a, b, c) for role, a, b, c in members if role in ("outer", "")]
    inner_segs = [(a, b, c) for role, a, b, c in members if role == "inner"]
    outer_rings, u1 = build_rings(outer_segs)
    inner_rings, u2 = build_rings(inner_segs)
    outers = [p for p in (_ring_polygon(r) for r in outer_rings) if p is not None]
    inners = [p for p in (_ring_polygon(r) for r in inner_rings) if p is not None]
    if not outers:
        return None, u1 + u2
    geom = shapely.union_all(outers)
    if inners:
        inner_u = shapely.union_all(inners)
        geom = geom.difference(inner_u)
        nested = [o for o in outers if inner_u.covers(o)]
        if nested:
            geom = shapely.union_all([geom, *nested])
    geom = _polygonal(geom)
    if geom is not None:
        geom = shapely.orient_polygons(geom, exterior_cw=False)
    return geom, u1 + u2


# ---------------------------------------------------------------------------
# Extraction
# ---------------------------------------------------------------------------
@dataclass
class _Box:
    lon0: float
    lat0: float
    lon1: float
    lat1: float

    def ints(self) -> tuple[int, int, int, int]:
        return (math.floor(self.lon0 * COORD_SCALE), math.floor(self.lat0 * COORD_SCALE),
                math.ceil(self.lon1 * COORD_SCALE), math.ceil(self.lat1 * COORD_SCALE))

    def contains(self, lon: float, lat: float) -> bool:
        return self.lon0 <= lon <= self.lon1 and self.lat0 <= lat <= self.lat1

    def intersects(self, b: tuple[float, float, float, float]) -> bool:
        return not (b[2] < self.lon0 or b[0] > self.lon1 or b[3] < self.lat0 or b[1] > self.lat1)

    def grown(self, d: float) -> "_Box":
        return _Box(self.lon0 - d, self.lat0 - d, self.lon1 + d, self.lat1 + d)


def buffered_bbox(bbox_lonlat: tuple[float, float, float, float], buffer_m: float) -> tuple[float, float, float, float]:
    """``bbox`` grown by ``buffer_m`` metres (degrees at the bbox's mid latitude)."""
    lon0, lat0, lon1, lat1 = (float(v) for v in bbox_lonlat)
    dlat = buffer_m / M_PER_DEG_LAT
    dlon = buffer_m / (M_PER_DEG_LAT * max(0.01, math.cos(math.radians(0.5 * (lat0 + lat1)))))
    return lon0 - dlon, lat0 - dlat, lon1 + dlon, lat1 + dlat


@dataclass
class _Rel:
    id: int
    tags: dict[str, str]
    members: list[tuple[int, str]]  # (way id, role)
    admin_level: int = 0  # > 0 for admin boundaries


@dataclass
class _State:
    box: _Box
    core: _Box
    ex: Extract
    stats: Counter = field(default_factory=Counter)


def _scan_relations(path: str, admin_levels: tuple[int, ...]) -> list[_Rel]:
    """Pass A: relations that can produce features, with their way members."""
    rels: list[_Rel] = []
    for r in osmium.FileProcessor(path, osmium.osm.RELATION):
        rtype = r.tags.get("type")
        if rtype not in ("multipolygon", "boundary"):
            continue
        tags = dict(r.tags)
        boundary = tags.get("boundary")
        admin_level = 0
        if boundary == "administrative":
            try:
                lvl = int(str(tags.get("admin_level", "")).strip())
            except ValueError:
                lvl = -1
            if lvl not in admin_levels:
                continue
            admin_level = lvl
        elif rtype == "boundary" and boundary not in ("protected_area", "national_park"):
            continue
        elif not (_has_building(tags) or "place" in tags or T.area_kind(tags) != AreaKind.NONE
                  or T.poi_kind(tags) != PoiKind.NONE):
            continue
        members = [(int(m.ref), m.role) for m in r.members if m.type == "w"]
        if members:
            rels.append(_Rel(int(r.id), tags, members, admin_level))
    return rels


def _name_or_empty(tags: dict[str, str]) -> NameRec:
    return T.parse_name(tags) or NameRec()


def _add_poi(st: _State, osm_type: str, osm_id: int, kind: PoiKind, lon: float, lat: float,
             tags: dict[str, str]) -> None:
    if not st.box.contains(lon, lat):
        return
    ele = T.parse_ele(tags.get("ele")) if tags.get("ele") else None
    flags = PoiFlags(0)
    if ele is not None:
        flags |= PoiFlags.HAS_ELE
    if int(kind) in _RELIGIOUS_POI:
        flags |= PoiFlags.SACRED
    st.ex.pois.append(PoiFeature(osm_type, osm_id, kind, lon, lat, name=T.parse_name(tags), ele_m=ele,
                                 flags=flags, tags=_subset(tags, POI_TAG_KEYS)))


def _add_place(st: _State, osm_type: str, osm_id: int, lon: float, lat: float, tags: dict[str, str]) -> None:
    if not st.box.contains(lon, lat):
        return
    kind = T.parse_place_kind(tags)
    if kind == PlaceKind.NONE:
        return
    name = T.parse_name(tags)
    if name is None:
        st.stats["places_unnamed_skipped"] += 1
        return
    st.ex.places.append(PlaceFeature(osm_type, osm_id, kind, lon, lat, name,
                                     population=_parse_population(tags.get("population"))))


def _building_fields(tags: dict[str, str]) -> dict:
    height = T.parse_length_m(tags.get("height"), "height") if tags.get("height") else None
    roof = T.parse_roof_shape(tags.get("roof:shape")) if tags.get("roof:shape") else RoofShape.UNKNOWN
    flags = BuildingFlags(0)
    if height is not None:
        flags |= BuildingFlags.HEIGHT_TAGGED
    if roof != RoofShape.UNKNOWN:
        flags |= BuildingFlags.ROOF_TAGGED
    rm = tags.get("roof:material")
    wm = tags.get("building:material")
    religion = tags.get("religion")
    return dict(
        use=T.parse_building_use(tags),
        building_raw=tags.get("building", "yes"),
        levels=T.parse_levels(tags.get("building:levels")) if tags.get("building:levels") else None,
        height_m=height,
        min_height_m=T.parse_length_m(tags.get("min_height"), "min_height") if tags.get("min_height") else None,
        roof_shape=roof,
        roof_material=T.parse_roof_material(rm) if rm else RoofMaterial.UNKNOWN,
        wall_material=T.parse_wall_material(wm) if wm else WallMaterial.UNKNOWN,
        flags=flags,
        religion=religion.strip() if religion and religion.strip() else None,
        name=T.parse_name(tags),
    )


def _road(way_id: int, tags: dict[str, str], lonlat: np.ndarray, node_ids: np.ndarray) -> RoadFeature | None:
    cls, is_link = T.parse_road_class(tags)
    if cls == RoadClass.UNKNOWN:
        return None
    return RoadFeature(
        osm_id=way_id, cls=cls, lonlat=lonlat, node_ids=node_ids, is_link=is_link,
        surface_raw=tags.get("surface"),
        tracktype=T.parse_tracktype(tags.get("tracktype")) if tags.get("tracktype") else 0,
        smoothness=tags.get("smoothness"),
        width_m=T.parse_length_m(tags.get("width")) if tags.get("width") else None,
        lanes=T.parse_lanes(tags.get("lanes")) if tags.get("lanes") else 0,
        oneway=T.parse_oneway(tags, cls),
        bridge=_is_yes_not_no(tags.get("bridge")),
        tunnel=_is_yes_not_no(tags.get("tunnel")),
        ford=_is_yes_not_no(tags.get("ford")),
        layer=_parse_layer(tags.get("layer")),
        sac_scale=T.parse_sac_scale(tags.get("sac_scale")),
        trail_visibility=T.parse_trail_visibility(tags.get("trail_visibility")),
        access=T.parse_access(tags, cls),
        name=T.parse_name(tags),
        ref=tags.get("ref"),
    )


def _process_way(st: _State, way_id: int, tags: dict[str, str], lonlat: np.ndarray, node_ids: np.ndarray,
                 closed: bool) -> None:
    box = st.box
    lon, lat = lonlat[:, 0], lonlat[:, 1]
    any_inside = bool(np.any((lon >= box.lon0) & (lon <= box.lon1) & (lat >= box.lat0) & (lat <= box.lat1)))
    bb = (float(lon.min()), float(lat.min()), float(lon.max()), float(lat.max()))
    if not box.intersects(bb):
        return
    is_building = _has_building(tags)
    area_tag = (tags.get("area") or "").strip().lower()

    if any_inside and "highway" in tags and area_tag != "yes":
        road = _road(way_id, tags, lonlat, node_ids)
        if road is not None:
            st.ex.roads.append(road)

    lk = T.line_kind(tags)
    if any_inside and lk != LineKind.NONE:
        st.ex.lines.append(LineFeature(way_id, lk, lonlat,
                                       width_m=T.parse_length_m(tags.get("width")) if tags.get("width") else None,
                                       name=T.parse_name(tags), tags=_subset(tags, LINE_TAG_KEYS)))

    poly = None
    if closed and is_building:
        ring = _open_ring(lonlat)
        if len(ring) >= 3 and abs(signed_area(ring)) > 0:
            st.ex.buildings.append(BuildingFeature("w", way_id, _orient(ring, True), **_building_fields(tags)))
        else:
            st.stats["buildings_degenerate_skipped"] += 1
    elif is_building:
        st.stats["buildings_open_way_skipped"] += 1
    elif closed and area_tag != "no":
        ak = T.area_kind(tags)
        if ak != AreaKind.NONE:
            poly = _ring_polygon(lonlat)
            if poly is not None:
                st.ex.areas.append(AreaFeature("w", way_id, ak, poly, name=T.parse_name(tags),
                                               tags=_subset(tags, AREA_TAG_KEYS)))
            else:
                st.stats["areas_invalid_skipped"] += 1

    pk = T.poi_kind(tags)
    has_place = "place" in tags
    if pk != PoiKind.NONE or has_place:
        if closed:
            geom = poly if poly is not None else _ring_polygon(lonlat)
        else:
            geom = LineString(lonlat) if len(lonlat) >= 2 else None
        if geom is None:
            return
        plon, plat = _label_point(geom)
        if pk != PoiKind.NONE:
            _add_poi(st, "w", way_id, pk, plon, plat, tags)
        if has_place:
            _add_place(st, "w", way_id, plon, plat, tags)


def _process_node(st: _State, node_id: int, lon: float, lat: float, tags: dict[str, str]) -> None:
    pk = T.poi_kind(tags)
    if pk != PoiKind.NONE:
        _add_poi(st, "n", node_id, pk, lon, lat, tags)
    if "place" in tags:
        _add_place(st, "n", node_id, lon, lat, tags)


def _process_relation(st: _State, rel: _Rel, geoms: dict[int, tuple[int, int, np.ndarray]]) -> None:
    members = []
    missing = 0
    for wid, role in rel.members:
        g = geoms.get(wid)
        if g is None:
            missing += 1
            continue
        members.append((role, g[0], g[1], g[2]))
    if missing:
        st.stats["relation_member_ways_missing"] += missing
    if not members:
        return
    lo = np.min([m[3].min(axis=0) for m in members], axis=0)
    hi = np.max([m[3].max(axis=0) for m in members], axis=0)
    bb = (float(lo[0]), float(lo[1]), float(hi[0]), float(hi[1]))
    target = st.core if rel.admin_level else st.box
    if not target.intersects(bb):
        return
    geom, unclosed = assemble_multipolygon(members)
    if unclosed:
        st.stats["relation_segments_unclosed"] += unclosed
    if geom is None:
        st.stats["relations_unassembled"] += 1
        return
    tags = rel.tags

    if rel.admin_level:
        if geom.intersects(shapely_box(st.core.lon0, st.core.lat0, st.core.lon1, st.core.lat1)):
            st.ex.admin.append(AdminArea(rel.id, ADMIN_LEVEL_KIND.get(rel.admin_level, PlaceKind.NONE),
                                         rel.admin_level, _name_or_empty(tags), geom))
        return

    if not st.box.intersects(geom.bounds):
        return
    if _has_building(tags):
        parts = sorted(_polygon_parts(geom), key=lambda p: -p.area)
        if parts:
            if len(parts) > 1:
                st.stats["buildings_relation_parts_dropped"] += len(parts) - 1
            p = parts[0]
            outer = _orient(_open_ring(np.asarray(p.exterior.coords)), True)
            holes = [_orient(_open_ring(np.asarray(h.coords)), False) for h in p.interiors]
            st.ex.buildings.append(BuildingFeature("r", rel.id, outer, holes=holes, **_building_fields(tags)))
    else:
        ak = T.area_kind(tags)
        if ak != AreaKind.NONE:
            st.ex.areas.append(AreaFeature("r", rel.id, ak, geom, name=T.parse_name(tags),
                                           tags=_subset(tags, AREA_TAG_KEYS)))
    pk = T.poi_kind(tags)
    if pk != PoiKind.NONE or "place" in tags:
        plon, plat = _label_point(geom)
        if pk != PoiKind.NONE:
            _add_poi(st, "r", rel.id, pk, plon, plat, tags)
        if "place" in tags:
            _add_place(st, "r", rel.id, plon, plat, tags)


def _unknown_delta(before: dict[str, Counter]) -> dict[str, list[tuple[str, int]]]:
    out: dict[str, list[tuple[str, int]]] = {}
    for key in sorted(T.unknown_values):
        delta = T.unknown_values[key] - before.get(key, Counter())
        if delta:
            out[key] = sorted(delta.items(), key=lambda kv: (-kv[1], kv[0]))[:40]
    return out


def _way_lonlat(way, nds, wkb) -> tuple[np.ndarray | None, np.ndarray | None]:
    """(N, 2) lon/lat of every way node, built in C++ via WKB.

    When some node locations are missing (a clipped extract) the points are
    built in Python without them and the matching node ids are returned as
    the second value (``None`` otherwise). ``(None, None)`` when fewer than
    two points remain.
    """
    try:
        hexwkb = wkb.create_linestring(way, use_nodes=osmium.geom.use_nodes.ALL)
        # 1 byte order + 4 type + 4 count, then little-endian (lon, lat) doubles.
        return np.frombuffer(bytes.fromhex(hexwkb), dtype="<f8", offset=9).reshape(-1, 2).copy(), None
    except (osmium.InvalidLocationError, RuntimeError):
        pass
    pts, refs = [], []
    for n in nds:
        loc = n.location
        if loc.valid():
            pts.append((loc.x, loc.y))
            refs.append(n.ref)
    if len(pts) < 2:
        return None, None
    return np.asarray(pts, dtype=np.float64) / COORD_SCALE, np.asarray(refs, dtype=np.int64)


_TYPE_ORDER = {"n": 0, "w": 1, "r": 2}


def extract_region(pbf_or_osm: Path, bbox_lonlat: tuple, *, buffer_m: float = 1500.0,
                   admin_levels=(4, 6, 7), region_id: str = "") -> Extract:
    """Extract every feature of interest around ``bbox_lonlat`` (lon_min, lat_min, lon_max, lat_max)."""
    t0 = time.perf_counter()
    path = str(pbf_or_osm)
    admin_levels = tuple(int(a) for a in admin_levels)
    core = _Box(*(float(v) for v in bbox_lonlat))
    box = _Box(*buffered_bbox(bbox_lonlat, buffer_m))
    st = _State(box=box, core=core, ex=Extract(region=region_id))
    unknown_before = {k: Counter(v) for k, v in T.unknown_values.items()}

    # Pass A: relations.
    rels = _scan_relations(path, admin_levels)
    member_ids = {wid for r in rels for wid, _ in r.members}
    t_a = time.perf_counter()

    # Pass B: nodes and ways with locations.
    X0, Y0, X1, Y1 = box.ints()
    wkb = osmium.geom.WKBFactory()
    BX0, BY0, BX1, BY1 = box.grown(BUILDING_FIRST_NODE_SLACK_DEG).ints()
    member_geoms: dict[int, tuple[int, int, np.ndarray]] = {}
    fp = (osmium.FileProcessor(path, osmium.osm.NODE | osmium.osm.WAY)
          .with_locations()
          .with_filter(osmium.filter.EmptyTagFilter().enable_for(osmium.osm.NODE)))
    n_ways = 0
    for o in fp:
        if o.is_node():
            loc = o.location
            if not loc.valid():
                continue
            x, y = loc.x, loc.y
            if X0 <= x <= X1 and Y0 <= y <= Y1:
                _process_node(st, int(o.id), x / COORD_SCALE, y / COORD_SCALE, dict(o.tags))
            continue
        n_ways += 1
        wid = o.id
        nds = o.nodes
        if len(nds) < 2:
            continue
        is_member = wid in member_ids
        tags = o.tags
        if not is_member:
            if not len(tags):
                continue
            if "building" in tags:
                loc0 = nds[0].location
                if not (BX0 <= loc0.x <= BX1 and BY0 <= loc0.y <= BY1):
                    continue
        lonlat, refs = _way_lonlat(o, nds, wkb)
        if lonlat is None:
            continue
        if refs is not None:
            st.stats["ways_missing_node_locations"] += 1
        if is_member:
            ends = (int(refs[0]), int(refs[-1])) if refs is not None else (int(nds[0].ref), int(nds[-1].ref))
            member_geoms[wid] = (ends[0], ends[1], lonlat)
            if not len(tags):
                continue
        lo, hi = lonlat.min(axis=0), lonlat.max(axis=0)
        if hi[0] < box.lon0 or lo[0] > box.lon1 or hi[1] < box.lat0 or lo[1] > box.lat1:
            continue
        if refs is None:
            refs = np.array([n.ref for n in nds], dtype=np.int64)
        closed = len(nds) >= 4 and nds[0].ref == nds[-1].ref
        _process_way(st, int(wid), dict(tags), lonlat, refs, closed)
    t_b = time.perf_counter()

    # Relations.
    for rel in sorted(rels, key=lambda r: r.id):
        _process_relation(st, rel, member_geoms)
    t_c = time.perf_counter()

    ex = st.ex
    ex.roads.sort(key=lambda f: f.osm_id)
    ex.lines.sort(key=lambda f: f.osm_id)
    ex.buildings.sort(key=lambda f: (f.osm_id << 1) | (f.osm_type == "r"))
    for lst in (ex.pois, ex.places, ex.areas):
        lst.sort(key=lambda f: (_TYPE_ORDER[f.osm_type], f.osm_id, int(getattr(f, "kind", 0))))
    ex.admin.sort(key=lambda a: (a.admin_level, a.osm_id))

    ex.stats = {
        "region": region_id,
        "bbox": [float(v) for v in bbox_lonlat],
        "buffered_bbox": [box.lon0, box.lat0, box.lon1, box.lat1],
        "buffer_m": float(buffer_m),
        "admin_levels": list(admin_levels),
        "counts": {
            "roads": len(ex.roads), "buildings": len(ex.buildings), "pois": len(ex.pois),
            "places": len(ex.places), "areas": len(ex.areas), "lines": len(ex.lines), "admin": len(ex.admin),
        },
        "road_classes": {k.name: v for k, v in sorted(Counter(r.cls for r in ex.roads).items())},
        "poi_kinds": {k.name: v for k, v in sorted(Counter(p.kind for p in ex.pois).items())},
        "area_kinds": {k.name: v for k, v in sorted(Counter(a.kind for a in ex.areas).items())},
        "line_kinds": {k.name: v for k, v in sorted(Counter(ln.kind for ln in ex.lines).items())},
        "relations_considered": len(rels),
        "relation_member_ways": len(member_ids),
        "ways_read": n_ways,
        **{k: int(v) for k, v in sorted(st.stats.items())},
        "unknown_values": _unknown_delta(unknown_before),
        "timing_s": {
            "pass_relations": round(t_a - t0, 3), "pass_nodes_ways": round(t_b - t_a, 3),
            "assemble_relations": round(t_c - t_b, 3), "total": round(time.perf_counter() - t0, 3),
        },
    }
    return ex


# ---------------------------------------------------------------------------
# Cache files
# ---------------------------------------------------------------------------
def save_extract(ex: Extract, path: Path) -> None:
    """Write ``ex`` as gzip-compressed pickle (protocol 5). The gzip header has
    no timestamp, so equal extracts give equal bytes."""
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    payload = pickle.dumps(ex, protocol=5)
    buf = io.BytesIO()
    with gzip.GzipFile(filename="", mode="wb", fileobj=buf, mtime=0, compresslevel=6) as gz:
        gz.write(payload)
    tmp = path.with_suffix(path.suffix + ".tmp")
    tmp.write_bytes(buf.getvalue())
    tmp.replace(path)


def load_extract(path: Path) -> Extract:
    """Read an ``Extract`` written by ``save_extract``."""
    with gzip.open(Path(path), "rb") as f:
        ex = pickle.load(f)
    if not isinstance(ex, Extract):
        raise TypeError(f"{path} does not hold an Extract")
    return ex
