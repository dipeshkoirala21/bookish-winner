"""Road attributes: the ``RATR`` chunk (docs/W2_DESIGN.md 4 and 9.3, work item D17).

One ``RoadAttrRec`` per ``ROAD`` record (same order):

* ``area_type``: the 250 m area-type cell (``areatype.py``) under the piece's
  length midpoint (rendered points only, context points excluded).
* ``sidewalk`` (``tags.parse_sidewalk``), ``lanes_fwd`` / ``lanes_bwd``
  (``lanes:forward`` / ``lanes:backward``), ``maxspeed_kmh``. Left/right and
  forward/backward are relative to the ROAD point order, which is reversed
  for ``oneway=-1`` ways, so those values are swapped for them.
* ``flags`` (``model.RoadAttrFlags``): DUAL, SERVICE_ROAD, HERITAGE_PEDESTRIAN
  (the piece's length midpoint lies in a SACRED_NO_VEHICLE zone), NO_MOTOR
  (the access mask has no motor mode, or ``motor_vehicle``/``vehicle=no``), LIT,
  BUS_ROUTE (member of a bus, microbus, tempo or share-taxi route relation),
  RING_MEMBER (``junction=roundabout|circular``), PAINTABLE (real width
  >= 5.5 m and asphalt or concrete; ``real_width_m``).
* ``partner_way_id`` and ``median_cm`` from **dual-carriageway pairing**
  (``pair_dual_carriageways``): two one-way motor ways (trunk to tertiary, not
  links) with the same ``ref`` or the same name, running in opposite
  directions with centrelines under ``DUAL_MAX_SPACING_M`` apart on at least
  half of the shorter way's 10 m samples. ``median_cm`` = median centreline
  spacing - the two real half-widths, at least 1.0 m. A one-way primary or
  secondary that runs alongside a dual trunk carriageway in the SAME
  direction, 6-15 m away, is a SERVICE_ROAD (Ring Road south).
* ``corridor_dm``: **corridor samples** (roads.md 1.3) every 20 m along the
  piece from its first rendered point: two rays perpendicular to the
  centreline, ``CORRIDOR_RAY_M`` each side, to the first building outline
  (building parts excluded); the sample is ``2 x min(dLeft, dRight)`` in
  decimetres, or 0 when a side is open. Samples whose point lies inside a
  footprint (mapping offset) take the previous valid sample (else the next,
  else 0). **Mapping-offset guard** (W2_DESIGN 9.5): on a way with a tagged
  ``width`` a bounded sample is never narrower than that width (building
  outlines and centrelines are often offset by a few metres in OSM; the
  surveyed width wins). Only for OLD_CORE and URBAN pieces of motor classes and pedestrian
  streets that are not bridges, tunnels or on a non-zero layer; every other
  piece stores no samples ("unknown").
"""

from __future__ import annotations

import math
from dataclasses import dataclass, field
from typing import Sequence

import numpy as np
import shapely

from .model import (MOTOR_TRAVEL, AreaType, RoadAttrFlags, RoadClass, RoadFeature, Sidewalk, Surface,
                    TransitMode)
from . import tags as T
from .tile_format import RoadAttrRec

SAMPLE_M = 20.0
CORRIDOR_RAY_M = 40.0
DUAL_MAX_SPACING_M = 15.0
DUAL_SAMPLE_M = 10.0
DUAL_MIN_SHARE = 0.5
SERVICE_SPACING_M = (6.0, 15.0)
MEDIAN_MIN_M = 1.0
PAINT_MIN_REAL_M = 5.5
DUAL_CLASSES = frozenset(int(c) for c in (RoadClass.MOTORWAY, RoadClass.TRUNK, RoadClass.PRIMARY,
                                           RoadClass.SECONDARY, RoadClass.TERTIARY))
TRAIL = frozenset(int(c) for c in (RoadClass.FOOTWAY, RoadClass.PATH, RoadClass.STEPS, RoadClass.CYCLEWAY,
                                    RoadClass.BRIDLEWAY))
CORRIDOR_AREAS = frozenset((int(AreaType.OLD_CORE), int(AreaType.URBAN)))
BUS_MODES = frozenset((TransitMode.BUS, TransitMode.MICROBUS, TransitMode.TEMPO, TransitMode.SHARE_TAXI))
_YES = frozenset({"yes", "24/7", "automatic", "limited", "interval", "sunset-sunrise", "night"})

# W2_DESIGN 4.1 real widths when OSM has no `width` (metres), class x area type.
# Columns: OLD_CORE, URBAN, PERI_URBAN, RURAL, HILL (FOREST reads as HILL, UNKNOWN as URBAN).
_W = {
    "trunk2": (14.0, 14.0, 14.0, 9.0, 7.5), "trunk1": (7.0, 7.0, 7.0, 7.0, 7.0),
    RoadClass.PRIMARY: (7.0, 7.0, 7.0, 6.5, 7.0), RoadClass.SECONDARY: (6.0, 7.0, 7.0, 5.5, 5.75),
    RoadClass.TERTIARY: (5.0, 7.0, 6.0, 4.5, 4.5), RoadClass.UNCLASSIFIED: (3.5, 4.0, 5.0, 3.5, 3.5),
    RoadClass.RESIDENTIAL: (4.0, 5.0, 5.0, 3.5, 3.5), RoadClass.LIVING_STREET: (3.0, 3.0, 3.5, 3.0, 3.0),
    RoadClass.SERVICE: (3.5, 4.0, 4.5, 3.5, 3.0), RoadClass.TRACK: (3.0, 3.0, 3.0, 3.0, 3.0),
    RoadClass.PEDESTRIAN: (4.0, 4.0, 4.0, 4.0, 3.0), RoadClass.FOOTWAY: (1.75, 2.0, 2.0, 1.5, 1.2),
    RoadClass.PATH: (1.5, 1.5, 1.5, 1.2, 1.0), RoadClass.STEPS: (1.5, 2.0, 1.5, 1.2, 1.2),
}
_FLOOR = {RoadClass.TRUNK: 6.0, RoadClass.MOTORWAY: 6.0, RoadClass.PRIMARY: 5.0, RoadClass.SECONDARY: 4.0,
          RoadClass.TERTIARY: 3.0, RoadClass.RESIDENTIAL: 2.5, RoadClass.UNCLASSIFIED: 2.5, RoadClass.SERVICE: 2.5,
          RoadClass.TRACK: 2.0, RoadClass.FOOTWAY: 0.9, RoadClass.PATH: 0.6}
_COL = {int(AreaType.OLD_CORE): 0, int(AreaType.URBAN): 1, int(AreaType.PERI_URBAN): 2, int(AreaType.RURAL): 3,
        int(AreaType.HILL): 4, int(AreaType.FOREST): 4, int(AreaType.UNKNOWN): 1}


def real_width_m(cls: int, area_type: int, width_m: float | None, lanes: int, oneway: bool) -> float:
    """W2_DESIGN 4.1: the tagged width when plausible, else the class x area default; clamped to
    ``[class floor, 40]``. Mirrors ``RoadWidthModel.RealWidthM`` (Track A) for the pipeline's own flags."""
    c = RoadClass(cls) if cls in RoadClass._value2member_map_ else RoadClass.UNKNOWN
    floor = _FLOOR.get(c, 2.0)
    if width_m is not None and width_m > 0:
        return max(floor, min(40.0, float(width_m)))
    col = _COL.get(int(area_type), 1)
    if c in (RoadClass.TRUNK, RoadClass.MOTORWAY):
        w = _W["trunk1" if oneway else "trunk2"][col]
    elif c == RoadClass.PRIMARY and lanes == 4 and not oneway and col == 1:
        w = 14.0
    elif c in (RoadClass.ROAD,):
        w = _W[RoadClass.UNCLASSIFIED][col]
    elif c in (RoadClass.CYCLEWAY, RoadClass.BRIDLEWAY):
        w = _W[RoadClass.PATH][col]
    else:
        w = _W.get(c, (3.5,) * 5)[col]
    return max(floor, min(40.0, w))


# ---------------------------------------------------------------------------
# Dual carriageways
# ---------------------------------------------------------------------------
@dataclass
class DualInfo:
    partner: dict[int, int] = field(default_factory=dict)  # way id -> partner way id
    spacing_m: dict[int, float] = field(default_factory=dict)  # way id -> median centreline spacing
    service: set[int] = field(default_factory=set)  # way ids flagged SERVICE_ROAD

    def stats(self) -> dict:
        return {"dual_ways": len(self.partner), "service_ways": len(self.service)}


def _key_names(r: RoadFeature) -> set[str]:
    out = set()
    if r.ref:
        out.add("ref:" + r.ref.strip().casefold())
    if r.name is not None:
        for s in (r.name.default, r.name.en):
            if s:
                out.add("name:" + s.strip().casefold())
    return out


def _samples(line, step: float) -> tuple[np.ndarray, np.ndarray]:
    """Points every ``step`` metres along a LineString and the unit tangents there."""
    length = line.length
    if length <= 0:
        return np.zeros((0, 2)), np.zeros((0, 2))
    d = np.arange(0.5 * min(step, length), length, step)
    p = shapely.get_coordinates(shapely.line_interpolate_point(line, d))
    a = shapely.get_coordinates(shapely.line_interpolate_point(line, np.maximum(d - 1.0, 0.0)))
    b = shapely.get_coordinates(shapely.line_interpolate_point(line, np.minimum(d + 1.0, length)))
    t = b - a
    n = np.hypot(t[:, 0], t[:, 1])
    return p, t / np.where(n > 0, n, 1.0)[:, None]


def pair_dual_carriageways(roads: Sequence[RoadFeature], roads_game: Sequence[np.ndarray]) -> DualInfo:
    """Partner ways and Ring-Road-style service roads (module docstring)."""
    info = DualInfo()
    cand = [i for i, r in enumerate(roads)
            if r.oneway and not r.is_link and int(r.cls) in DUAL_CLASSES and len(roads_game[i]) >= 2]
    if len(cand) < 2:
        return info
    lines = np.empty(len(cand), dtype=object)
    lines[:] = [shapely.linestrings(np.asarray(roads_game[i], dtype=np.float64)) for i in cand]
    tree = shapely.STRtree(lines)
    keys = [_key_names(roads[i]) for i in cand]
    best: dict[int, tuple[float, float, int]] = {}  # cand pos -> (share, spacing, other pos)
    same_dir: dict[int, list[tuple[int, float]]] = {}
    for k, line in enumerate(lines):
        pts, tan = _samples(line, DUAL_SAMPLE_M)
        if not len(pts):
            continue
        near = tree.query(line, predicate="dwithin", distance=DUAL_MAX_SPACING_M)
        for o in sorted(int(v) for v in near):
            if o == k:
                continue
            other = lines[o]
            loc = shapely.line_locate_point(other, shapely.points(pts))
            q = shapely.get_coordinates(shapely.line_interpolate_point(other, loc))
            q2 = shapely.get_coordinates(shapely.line_interpolate_point(other, np.minimum(loc + 1.0, other.length)))
            q1 = shapely.get_coordinates(shapely.line_interpolate_point(other, np.maximum(loc - 1.0, 0.0)))
            to = q2 - q1
            tn = np.hypot(to[:, 0], to[:, 1])
            to = to / np.where(tn > 0, tn, 1.0)[:, None]
            dist = np.hypot(*(q - pts).T)
            dot = np.einsum("ij,ij->i", tan, to)
            close = dist <= DUAL_MAX_SPACING_M
            opp = close & (dot < -0.7)
            same = close & (dot > 0.7)
            if keys[k] & keys[o] and opp.sum() >= DUAL_MIN_SHARE * len(pts):
                share = float(opp.mean())
                spacing = float(np.median(dist[opp]))
                cur = best.get(k)
                if cur is None or (share, -spacing, -cand[o]) > (cur[0], -cur[1], -cand[cur[2]]):
                    best[k] = (share, spacing, o)
            if same.sum() >= DUAL_MIN_SHARE * len(pts):
                sp = float(np.median(dist[same]))
                if SERVICE_SPACING_M[0] <= sp <= SERVICE_SPACING_M[1]:
                    same_dir.setdefault(k, []).append((o, sp))
    for k, (share, spacing, o) in best.items():
        info.partner[int(roads[cand[k]].osm_id)] = int(roads[cand[o]].osm_id)
        info.spacing_m[int(roads[cand[k]].osm_id)] = spacing
    for k, lst in same_dir.items():
        r = roads[cand[k]]
        if int(r.cls) not in (int(RoadClass.PRIMARY), int(RoadClass.SECONDARY)) or int(r.osm_id) in info.partner:
            continue
        for o, _sp in lst:
            ro = roads[cand[o]]
            if int(ro.cls) in (int(RoadClass.TRUNK), int(RoadClass.MOTORWAY)) and int(ro.osm_id) in info.partner:
                info.service.add(int(r.osm_id))
                break
    return info


# ---------------------------------------------------------------------------
# Corridors
# ---------------------------------------------------------------------------
@dataclass
class BuildingIndex:
    """Building outlines (no parts) in game metres, for corridor rays."""

    polys: np.ndarray
    tree: object

    @classmethod
    def build(cls, rings: Sequence[np.ndarray]) -> "BuildingIndex":
        arr = np.empty(len(rings), dtype=object)
        arr[:] = [shapely.polygons(np.asarray(r, dtype=np.float64)) if len(r) >= 3 else shapely.Polygon()
                  for r in rings]
        ok = shapely.is_valid(arr) | shapely.is_empty(arr)
        if not ok.all():
            arr[~ok] = shapely.make_valid(arr[~ok])
        return cls(arr, shapely.STRtree(arr) if len(arr) else None)


def sample_positions(pts: np.ndarray, step: float = SAMPLE_M) -> tuple[np.ndarray, np.ndarray]:
    """Points every ``step`` m from the first point along a polyline, and unit tangents."""
    p = np.asarray(pts, dtype=np.float64)
    seg = np.diff(p, axis=0)
    ln = np.hypot(seg[:, 0], seg[:, 1])
    cum = np.concatenate([[0.0], np.cumsum(ln)])
    total = float(cum[-1])
    if total <= 0:
        return np.zeros((0, 2)), np.zeros((0, 2))
    d = np.arange(0.0, total + 1e-9, step)
    k = np.clip(np.searchsorted(cum, d, side="right") - 1, 0, len(seg) - 1)
    # Skip zero-length segments for tangents.
    t = (d - cum[k]) / np.where(ln[k] > 0, ln[k], 1.0)
    pos = p[k] + seg[k] * t[:, None]
    tan = seg[k] / np.where(ln[k] > 0, ln[k], 1.0)[:, None]
    return pos, tan


def corridor_samples(pts: np.ndarray, bidx: BuildingIndex, step: float = SAMPLE_M,
                     ray_m: float = CORRIDOR_RAY_M, min_dm: int = 0) -> np.ndarray:
    """``corridor_dm`` (int64) for a rendered polyline (game metres); see the module docstring.
    ``min_dm`` (the tagged width) is the mapping-offset guard: a bounded sample is never narrower."""
    pos, tan = sample_positions(pts, step)
    n = len(pos)
    if n == 0 or bidx.tree is None:
        return np.zeros(n, dtype=np.int64)
    normal = np.stack([-tan[:, 1], tan[:, 0]], axis=1)  # left of travel
    origins = shapely.points(pos)
    hit = np.full((2, n), np.inf)
    for side, sign in ((0, 1.0), (1, -1.0)):
        ends = pos + sign * ray_m * normal
        rays = shapely.linestrings(np.stack([pos, ends], axis=1))
        pairs = bidx.tree.query(rays, predicate="intersects")
        if pairs.shape[1]:
            inter = shapely.intersection(rays[pairs[0]], bidx.polys[pairs[1]])
            d = shapely.distance(origins[pairs[0]], inter)
            d = np.where(np.isfinite(d), d, np.inf)
            np.minimum.at(hit[side], pairs[0], d)
    inside = np.zeros(n, dtype=bool)
    pin = bidx.tree.query(origins, predicate="within")
    if pin.shape[1]:
        inside[np.unique(pin[0])] = True
    both = np.isfinite(hit[0]) & np.isfinite(hit[1])
    val = np.where(both, np.rint(2.0 * np.minimum(hit[0], hit[1]) * 10.0), 0.0).astype(np.int64)
    if inside.any():
        good = np.flatnonzero(~inside)
        if not len(good):
            return np.zeros(n, dtype=np.int64)
        for i in np.flatnonzero(inside):
            prev = good[good < i]
            val[i] = val[prev[-1]] if len(prev) else val[good[good > i][0]]
    if min_dm > 0:
        val = np.where(val > 0, np.maximum(val, int(min_dm)), 0).astype(np.int64)
    return val


# ---------------------------------------------------------------------------
# Records
# ---------------------------------------------------------------------------
def _lanes(raw: str | None) -> int:
    return min(255, T.parse_lanes(raw)) if raw else 0


def static_attrs(r: RoadFeature, dual: DualInfo, bus_ways: set[int]) -> tuple[int, int, int, int, int]:
    """Way-level ``(sidewalk, lanes_fwd, lanes_bwd, maxspeed, flags)`` (no area type, corridor or zone flags)."""
    ex = r.extra or {}
    sw = T.parse_sidewalk(ex)
    fwd, bwd = _lanes(ex.get("lanes:forward")), _lanes(ex.get("lanes:backward"))
    if int(r.oneway) < 0:  # the ROAD record is reversed: swap sides and directions
        sw = {Sidewalk.LEFT: Sidewalk.RIGHT, Sidewalk.RIGHT: Sidewalk.LEFT}.get(sw, sw)
        fwd, bwd = bwd, fwd
    flags = 0
    wid = int(r.osm_id)
    if wid in dual.partner:
        flags |= RoadAttrFlags.DUAL
    if wid in dual.service:
        flags |= RoadAttrFlags.SERVICE_ROAD
    mv = (ex.get("motor_vehicle") or ex.get("vehicle") or "").strip().lower()
    if not (int(r.access) & int(MOTOR_TRAVEL)) or mv == "no":
        flags |= RoadAttrFlags.NO_MOTOR
    if (ex.get("lit") or "").strip().lower() in _YES:
        flags |= RoadAttrFlags.LIT
    if wid in bus_ways:
        flags |= RoadAttrFlags.BUS_ROUTE
    if (ex.get("junction") or "").strip().lower() in ("roundabout", "circular"):
        flags |= RoadAttrFlags.RING_MEMBER
    return int(sw), fwd, bwd, T.parse_maxspeed(ex.get("maxspeed")), int(flags)


def road_attr(r: RoadFeature, rendered_game: np.ndarray, area_type: int, in_sacred: bool, dual: DualInfo,
              bus_ways: set[int], partner: RoadFeature | None, bidx: BuildingIndex | None) -> RoadAttrRec:
    """The RATR record of one ROAD piece (``rendered_game``: the piece's rendered points, game metres)."""
    sw, fwd, bwd, ms, flags = static_attrs(r, dual, bus_ways)
    if in_sacred:
        flags |= RoadAttrFlags.HERITAGE_PEDESTRIAN
    real = real_width_m(int(r.cls), area_type, r.width_m, int(r.lanes), bool(r.oneway))
    if real >= PAINT_MIN_REAL_M and r.surface in (Surface.ASPHALT, Surface.CONCRETE):
        flags |= RoadAttrFlags.PAINTABLE
    partner_id = dual.partner.get(int(r.osm_id), 0)
    median_cm = 0
    if partner_id:
        pw = real_width_m(int(partner.cls), area_type, partner.width_m, int(partner.lanes), True) \
            if partner is not None else real
        median = max(MEDIAN_MIN_M, dual.spacing_m[int(r.osm_id)] - 0.5 * (real + pw))
        median_cm = int(round(median * 100.0))
    cor = np.zeros(0, dtype=np.int64)
    if (bidx is not None and area_type in CORRIDOR_AREAS and int(r.cls) not in TRAIL
            and int(r.cls) != int(RoadClass.UNKNOWN) and not r.bridge and not r.tunnel and int(r.layer) == 0):
        tagged_dm = int(math.ceil(float(r.width_m) * 10.0 - 1e-9)) if r.width_m and r.width_m > 0 else 0
        cor = corridor_samples(rendered_game, bidx, min_dm=tagged_dm)
    return RoadAttrRec(area_type=int(area_type), sidewalk=sw, lanes_fwd=fwd, lanes_bwd=bwd, maxspeed_kmh=ms,
                       flags=int(flags), partner_way_id=int(partner_id), median_cm=median_cm, corridor_dm=cor)


def length_midpoint(pts: np.ndarray) -> tuple[float, float]:
    p = np.asarray(pts, dtype=np.float64)
    if len(p) == 1:
        return float(p[0, 0]), float(p[0, 1])
    seg = np.diff(p, axis=0)
    ln = np.hypot(seg[:, 0], seg[:, 1])
    cum = np.concatenate([[0.0], np.cumsum(ln)])
    half = 0.5 * cum[-1]
    k = int(np.clip(np.searchsorted(cum, half, side="right") - 1, 0, len(seg) - 1))
    t = (half - cum[k]) / ln[k] if ln[k] > 0 else 0.0
    q = p[k] + seg[k] * t
    return float(q[0]), float(q[1])
