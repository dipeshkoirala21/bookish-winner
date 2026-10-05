"""Building style profiles and fronts (docs/W2_DESIGN.md 2.1 and 9.3; work items D4 + D19).

``StyleZones`` loads ``config/style_zones.yaml`` and assigns every building a
``model.StyleProfile`` by the first matching rule (the file's header lists
them: curated zones, Newar-town circles, municipality relations by id, the
area-type grid). ``front_records`` computes the per-tile ``BFNT`` records:

* ``front_edge``: the outer-ring edge (index into the canonical ring 0, whose
  edge ``i`` runs from vertex ``i`` to ``i + 1``) that faces the nearest motor
  road or pedestrian street. An edge *faces* a road when the nearest road
  segment lies on the outer side of the edge (outward normal) within
  ``FRONT_MAX_M``; among facing edges the nearest wins (ties: lower index).
  ``255`` when no edge faces a road.
* ``front_dist_dm``: front-edge midpoint to that road's centreline, decimetres,
  clamped to 255.
* ``second_edge``: the nearest facing edge whose road is a *different* way,
  within ``CORNER_MAX_M`` (corner houses; ``CORNER`` flag), else 255.
* ``shop_bays``: shop-like POIs inside the footprint (bits 0-3, at most 15)
  with bit 7 ``FROM_POI`` set when any (``poi_hints.apply_poi_footprints``).
* ``flags``: ``COURTYARD_HOST`` (the footprint has holes), ``CORNER``,
  ``FACES_HERITAGE_SQUARE`` (front midpoint within ``HERITAGE_FRONT_M`` of a
  heritage-zone AREA), ``RANA_HINT`` (``start_date`` before 1960, or a
  non-religious building named or tagged durbar/palace/mahal),
  ``STRUCTURE_RCC`` / ``STRUCTURE_MUD`` from ``building:structure``,
  ``ROOF_FLAT_TAGGED``.

Building parts get the host's profile and area type and no front (255).
"""

from __future__ import annotations

import math
import re
from dataclasses import dataclass, field
from pathlib import Path
from typing import Sequence

import numpy as np
import shapely
import yaml
from shapely.geometry import Point, Polygon

from . import projection
from .config import CONFIG_DIR
from .model import AreaType, BuildingFlags, BuildingFrontFlags, PlaceKind, RoadClass, RoofShape, StyleProfile

DEFAULT_PATH = CONFIG_DIR / "style_zones.yaml"
FRONT_MAX_M = 25.0
CORNER_MAX_M = 15.0
HERITAGE_FRONT_M = 8.0
EDGE_NONE = 255
FROM_POI = 0x80
RANA_YEAR = 1960
# Roads a building can front: motor classes and pedestrian streets (not footways, paths or steps).
FRONT_CLASSES = frozenset(int(c) for c in (RoadClass.MOTORWAY, RoadClass.TRUNK, RoadClass.PRIMARY,
                                            RoadClass.SECONDARY, RoadClass.TERTIARY, RoadClass.UNCLASSIFIED,
                                            RoadClass.RESIDENTIAL, RoadClass.LIVING_STREET, RoadClass.SERVICE,
                                            RoadClass.ROAD, RoadClass.PEDESTRIAN, RoadClass.TRACK))
_YEAR_RE = re.compile(r"(1[5-9]\d\d|20\d\d)")
_RANA_NAME_RE = re.compile(r"durbar|darbar|palace|\bmahal\b|दरबार|महल")


@dataclass
class Zone:
    id: str
    profile: StyleProfile
    geom_lonlat: object
    old_core: bool = False


@dataclass
class StyleZones:
    zones: list[Zone] = field(default_factory=list)
    towns: list[dict] = field(default_factory=list)
    town_radius_m: float = 400.0
    village_radius_m: float = 250.0
    village_small_buildings: int = 1500
    municipalities: dict[int, tuple[StyleProfile, StyleProfile]] = field(default_factory=dict)
    area_types: dict[int, StyleProfile] = field(default_factory=dict)

    def core_geoms_game(self) -> list[object]:
        """Game-space polygons of the ``old_core`` zones (areatype.build_grid)."""
        return [to_game(z.geom_lonlat) for z in self.zones if z.old_core]


def to_game(geom_lonlat):
    def fn(c: np.ndarray) -> np.ndarray:
        x, z = projection.lonlat_to_game(c[:, 0], c[:, 1])
        return np.stack([np.asarray(x, dtype=np.float64), np.asarray(z, dtype=np.float64)], axis=1)

    return shapely.transform(geom_lonlat, fn)


def _circle_lonlat(lon: float, lat: float, radius_m: float):
    """A circle of ``radius_m`` metres in lon/lat (64-gon, local equirectangular)."""
    k = 111_320.0
    t = np.linspace(0.0, 2.0 * math.pi, 64, endpoint=False)
    return Polygon(np.stack([lon + radius_m * np.cos(t) / (k * math.cos(math.radians(lat))),
                             lat + radius_m * np.sin(t) / k], axis=1))


def _shape(entry: dict, where: str):
    if "rect" in entry:
        a, b, c, d = (float(v) for v in entry["rect"])
        return shapely.box(a, b, c, d)
    if "circle" in entry:
        lon, lat, r = (float(v) for v in entry["circle"])
        return _circle_lonlat(lon, lat, r)
    if "polygon" in entry:
        return Polygon([(float(a), float(b)) for a, b in entry["polygon"]])
    raise ValueError(f"{where}: zone needs rect, circle or polygon")


def load_style_zones(path: Path | None = None) -> StyleZones:
    path = Path(path) if path is not None else DEFAULT_PATH
    data = yaml.safe_load(path.read_text(encoding="utf-8"))
    out = StyleZones()
    for e in data.get("zones", []) or []:
        g = _shape(e, f"{path}: {e.get('id')}")
        if not g.is_valid or g.area <= 0:
            raise ValueError(f"{path}: zone {e.get('id')!r} is not a valid polygon")
        out.zones.append(Zone(str(e["id"]), StyleProfile[str(e["profile"])], g, bool(e.get("old_core", False))))
    t = data.get("newar_towns") or {}
    out.towns = list(t.get("places", []) or [])
    out.town_radius_m = float(t.get("town_radius_m", 400))
    out.village_radius_m = float(t.get("village_radius_m", 250))
    out.village_small_buildings = int(t.get("village_small_buildings", 1500))
    for e in data.get("municipalities", []) or []:
        p = StyleProfile[str(e["profile"])]
        out.municipalities[int(e["relation"])] = (p, StyleProfile[str(e.get("urban_profile", e["profile"]))])
    for k, v in (data.get("area_types") or {}).items():
        out.area_types[int(AreaType[str(k)])] = StyleProfile[str(v)]
    return out


def assign_profiles(zones: StyleZones, lon: np.ndarray, lat: np.ndarray, area_type: np.ndarray, places=(),
                    admin=()) -> tuple[np.ndarray, dict]:
    """StyleProfile per building centroid (lon/lat arrays), first match of the four rules."""
    n = len(lon)
    out = np.zeros(n, dtype=np.uint8)
    done = np.zeros(n, dtype=bool)
    stats: dict = {"by_rule": {}, "towns_found": [], "towns_missing": []}

    def apply(mask: np.ndarray, value, rule: str) -> None:
        m = mask & ~done
        if np.isscalar(value) or np.ndim(value) == 0:
            out[m] = int(value)
        else:
            out[m] = np.asarray(value)[m]
        done[m] = True
        stats["by_rule"][rule] = stats["by_rule"].get(rule, 0) + int(m.sum())

    for z in zones.zones:
        apply(shapely.contains_xy(z.geom_lonlat, lon, lat), z.profile, "zone")

    for t in zones.towns:
        name = str(t["name"]).casefold()
        nlon, nlat = (float(v) for v in t.get("near", (0.0, 0.0)))
        best = None
        for p in places:
            names = {s.casefold() for s in (p.name.default, p.name.en) if s}
            if name not in names:
                continue
            d = _metres(nlon, nlat, p.lon, p.lat)
            if d <= 3000.0 and (best is None or d < best[0]):
                best = (d, p)
        if best is None:
            stats["towns_missing"].append(t["name"])
            continue
        p = best[1]
        town = p.kind in (PlaceKind.CITY, PlaceKind.TOWN)
        r = zones.town_radius_m if town else zones.village_radius_m
        inside = (_metres_arr(p.lon, p.lat, lon, lat) <= r)
        prof = StyleProfile.KIRTIPUR
        if not town and int(inside.sum()) < zones.village_small_buildings:
            prof = StyleProfile.KHOKANA
        stats["towns_found"].append(t["name"])
        apply(inside, prof, "newar_town")

    for a in admin:
        rule = zones.municipalities.get(int(a.osm_id))
        if rule is None or a.polygon is None:
            continue
        base, urban = rule
        inside = shapely.contains_xy(a.polygon, lon, lat)
        urban_cell = np.isin(area_type, (int(AreaType.URBAN), int(AreaType.OLD_CORE)))
        apply(inside, np.where(urban_cell, int(urban), int(base)), "municipality")

    fallback = np.array([int(zones.area_types.get(int(t), StyleProfile.RIM)) for t in range(8)], dtype=np.uint8)
    apply(np.ones(n, dtype=bool), fallback[np.clip(area_type.astype(np.int64), 0, 7)], "area_type")
    return out, stats


def _metres(lon0, lat0, lon1, lat1) -> float:
    k = 111_320.0
    return math.hypot((lon1 - lon0) * k * math.cos(math.radians(0.5 * (lat0 + lat1))), (lat1 - lat0) * k)


def _metres_arr(lon0, lat0, lon, lat) -> np.ndarray:
    k = 111_320.0
    return np.hypot((np.asarray(lon) - lon0) * k * math.cos(math.radians(lat0)), (np.asarray(lat) - lat0) * k)


# ---------------------------------------------------------------------------
# Fronts
# ---------------------------------------------------------------------------
@dataclass
class RoadSegments:
    """Every segment of the front-able roads, in game metres, with an STRtree."""

    a: np.ndarray  # (S, 2)
    b: np.ndarray  # (S, 2)
    way: np.ndarray  # (S,) OSM way id
    tree: object

    @classmethod
    def build(cls, roads_game: Sequence[np.ndarray], way_ids: Sequence[int], classes: Sequence[int]) -> "RoadSegments":
        a_parts, b_parts, w_parts = [], [], []
        for pts, wid, cls_ in zip(roads_game, way_ids, classes):
            if int(cls_) not in FRONT_CLASSES or len(pts) < 2:
                continue
            p = np.asarray(pts, dtype=np.float64)
            a_parts.append(p[:-1])
            b_parts.append(p[1:])
            w_parts.append(np.full(len(p) - 1, int(wid), dtype=np.int64))
        if not a_parts:
            z = np.zeros((0, 2))
            return cls(z, z, np.zeros(0, dtype=np.int64), None)
        a = np.concatenate(a_parts)
        b = np.concatenate(b_parts)
        keep = np.any(a != b, axis=1)
        a, b, w = a[keep], b[keep], np.concatenate(w_parts)[keep]
        lines = shapely.linestrings(np.stack([a, b], axis=1))
        return cls(a, b, w, shapely.STRtree(lines))

    def nearest(self, pts: np.ndarray, max_dist: float) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
        """(segment index or -1, distance, closest point (N, 2)) for each point."""
        n = len(pts)
        seg = np.full(n, -1, dtype=np.int64)
        dist = np.full(n, np.inf)
        close = np.zeros((n, 2))
        if self.tree is None or n == 0:
            return seg, dist, close
        idx, d = self.tree.query_nearest(shapely.points(pts), max_distance=max_dist, return_distance=True,
                                         all_matches=False)
        seg[idx[0]] = idx[1]
        dist[idx[0]] = d
        ok = seg >= 0
        if ok.any():
            a, b = self.a[seg[ok]], self.b[seg[ok]]
            ab = b - a
            t = np.clip(np.einsum("ij,ij->i", pts[ok] - a, ab) / np.einsum("ij,ij->i", ab, ab), 0.0, 1.0)
            close[ok] = a + ab * t[:, None]
        return seg, dist, close


def front_edges(ring: np.ndarray, roads: RoadSegments) -> tuple[int, int, int]:
    """``(front_edge, front_dist_dm, second_edge)`` of an open counter-clockwise outer ring (game metres)."""
    r = np.asarray(ring, dtype=np.float64)
    n = len(r)
    if n < 3:
        return EDGE_NONE, 0, EDGE_NONE
    a = r
    b = np.roll(r, -1, axis=0)
    mid = 0.5 * (a + b)
    d = b - a
    length = np.hypot(d[:, 0], d[:, 1])
    normal = np.stack([d[:, 1], -d[:, 0]], axis=1) / np.where(length > 0, length, 1.0)[:, None]
    seg, dist, close = roads.nearest(mid, FRONT_MAX_M)
    facing = (seg >= 0) & (length > 0.5) & (np.einsum("ij,ij->i", normal, close - mid) > 0.0)
    if not facing.any():
        return EDGE_NONE, 0, EDGE_NONE
    cand = np.flatnonzero(facing)
    order = cand[np.lexsort((cand, dist[cand]))]
    front = int(order[0])
    fdm = int(min(255, max(0, round(float(dist[front]) * 10.0))))
    front_way = roads.way[seg[front]]
    second = EDGE_NONE
    for e in order[1:]:
        if roads.way[seg[e]] != front_way and dist[e] <= CORNER_MAX_M:
            second = int(e)
            break
    if front > 254 or (second != EDGE_NONE and second > 254):
        return (EDGE_NONE, 0, EDGE_NONE) if front > 254 else (front, fdm, EDGE_NONE)
    return front, fdm, second


def start_year(raw: str | None) -> int | None:
    m = _YEAR_RE.search(raw or "")
    return int(m.group(1)) if m else None


def hint_flags(b) -> int:
    """BFNT flags that come from a building's own tags (``BuildingFeature``)."""
    f = 0
    if b.holes:
        f |= BuildingFrontFlags.COURTYARD_HOST
    extra = getattr(b, "extra", {}) or {}
    st = (extra.get("building:structure") or "").lower()
    if "reinforced_concrete" in st or "rcc" in st or "concrete_frame" in st:
        f |= BuildingFrontFlags.STRUCTURE_RCC
    if "mud" in st:
        f |= BuildingFrontFlags.STRUCTURE_MUD
    if (b.flags & BuildingFlags.ROOF_TAGGED) and b.roof_shape == RoofShape.FLAT:
        f |= BuildingFrontFlags.ROOF_FLAT_TAGGED
    year = start_year(extra.get("start_date") or extra.get("construction_date"))
    names = " ".join(s for s in ((b.name.default, b.name.en) if b.name else ()) if s).casefold()
    religious = bool(b.religion) or b.building_raw in ("temple", "stupa", "monastery", "church", "mosque")
    if not religious and ((year is not None and year < RANA_YEAR)
                          or (extra.get("historic") or "").lower() in ("durbar", "palace")
                          or _RANA_NAME_RE.search(names)):
        f |= BuildingFrontFlags.RANA_HINT
    return int(f)
