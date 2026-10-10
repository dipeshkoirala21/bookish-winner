"""Road structures: bridges, flyovers, underpasses, tunnels and fords with absolute deck heights (W2 detail pass).

docs/W2_DETAIL_CONTRACT.md decisions 3-5 and docs/DATA_FORMATS.md section 1.15 (``RSTR``). Everything is computed
once per OSM way along its whole game-space polyline (``wayprofile``), so the pieces a way is cut into at tile
borders agree; ``Structures.piece_record`` then reads the values back for one clipped piece.

Classification (``analyse``)
----------------------------
* **Effective layer**: the OSM ``layer``; else +1 for a bridge and -1 for a tunnel (``tunnel=yes``; a
  ``building_passage`` and any tunnel of at most ``PASSAGE_MAX_M`` on layer >= 0 is a passage, i.e. an ordinary
  ground road).
* **Water crossings**: a road crosses a waterway line (river, stream, canal; ditches and drains only for tagged
  bridges; lines tagged ``tunnel``/``culvert`` never) or a water area (riverbank, lake, pond). A tagged bridge that
  crosses water is a BRIDGE with WATER_CROSSING. An untagged road crossing a waterway line gets an **inferred
  bridge span** there: the water area's inside interval when the crossing lies in one, else the line's width
  (tagged, or ``LINE_WIDTH_M`` by kind) plus ``SPAN_MARGIN_M`` on each side, divided by the sine of the crossing
  angle. Lakes and ponds also make spans where a road runs at least ``POND_MIN_M`` over them. A crossing within
  ``BRIDGE_SNAP_M`` of a tagged bridge belongs to that bridge (mapping offsets), and fords never get spans.
* **Grade separations**: two ways whose lines cross at a point that is not a shared OSM node, on different
  effective layers. The higher one is the upper: a tagged bridge over it, or an untagged way on layer > 0 (a
  flyover mapped without ``bridge``), becomes a deck; it is a FLYOVER when it crosses no water (FOOT_OVERBRIDGE for
  footways, paths, steps, cycleways, bridleways and pedestrian streets), and gets OVER_ROAD. The lower one is an
  UNDERPASS with the clearance measured under the deck. A tunnel-tagged lower way under a ground road is lowered (LOWERED); a layer < 0
  way meeting a ground road without a shared node and without a tunnel tag is an at-grade crossing (ignored).
* TUNNEL: a tunnel that passes under no road (not drawn in W2, kept out of car routes); FORD: ``ford=yes``.

Deck heights (``_solve``)
-------------------------
Every way that carries a deck or a lowered profile, and the ways up to two junctions away, are cut into
**stations** every ``STATION_M`` (plus their vertices and every interval end). Stations at the same OSM node are
one graph vertex. Terrain is the runtime's: the leaf tiles' quantised ``HGHT`` samples, bilinear (``LeafTerrain``).

1. **Base deck line**: each connected run of deck stations is interpolated harmonically (linearly along a chain)
   between its **abutments**, the deck stations that touch a ground road or end a way, which sit on the terrain;
   a deck never runs below the terrain under it.
2. **Requirements**: over water the deck is at least the water surface + the kind's clearance (rivers 3 m: the
   lowest terrain along the waterway within ``SURFACE_SEARCH_M`` minus the channel depth); over a road it is at
   least that road's surface + ``MIN_UNDERPASS_CLEARANCE_M`` + the deck depth (``DECK_DEPTH_M``, foot decks
   ``FOOT_DECK_DEPTH_M``) along the lower road's corridor.
3. **Ramps**: requirements spread outwards as cones with the class's maximum grade (``GRADE``), through the deck
   and on into the approach roads until they meet the terrain (those stations are RAMP; a way that is not a
   structure itself but carries such a ramp gets APPROACH). The final height is the maximum of the base line and
   every cone.
4. **Lowered underpasses**: under a ground road a tunnel-tagged lower way must stay at least the clearance + deck
   depth below the upper road's surface; the requirement spreads as an inverted cone with the same grades until it
   meets the terrain.
5. Requirements on stacked structures depend on the lower heights, so steps 1-4 repeat until nothing changes.

Car access (decision 5, ``car_accessible``): a motor class (trunk to service, track, road), an access mask with
CAR or JEEP, a real width of at least ``CAR_MIN_REAL_M`` (the plausible ``width`` tag, else the W2_DESIGN 4.1
default), no galli stretch (``corridors``: untagged ways whose measured space between buildings stays under
``CAR_MIN_REAL_M`` + 1 m for ``GALLI_MIN_M``) and not a TUNNEL. The routing graph drops CAR, JEEP and BUS from
every way that fails it (``routing.build_graph(no_car=...)``).
"""

from __future__ import annotations

import heapq
import math
from collections import defaultdict
from dataclasses import dataclass, field
from typing import Callable, Sequence

import numpy as np
import shapely

from . import wayprofile as wp
from .model import AreaKind, LineKind, RoadClass, RoadFeature, RoadStructureFlags as SF, RoadStructureKind as SK, \
    Travel
from .tile_format import DECK_DECK, DECK_DRAPED, DECK_RAMP, RoadStructureRec

MIN_UNDERPASS_CLEARANCE_M = 5.5  # RoadClearance.MinUnderpassClearanceM
DECK_DEPTH_M = 1.2  # vehicle deck: surface to underside (the bridges package keeps its structure within it)
FOOT_DECK_DEPTH_M = 0.6
STATION_M = 5.0
EPS_M = 0.02
MAX_RAMP_M = 400.0  # an approach ramp never runs further than this from its structure
PASSAGE_MAX_M = 60.0
BRIDGE_SNAP_M = 20.0
SPAN_MARGIN_M = 2.0
POND_MIN_M = 3.0
SURFACE_SEARCH_M = {int(LineKind.RIVER): 40.0, int(LineKind.CANAL): 20.0, int(LineKind.STREAM): 20.0,
                    int(LineKind.DITCH): 10.0}
LINE_WIDTH_M = {int(LineKind.RIVER): 15.0, int(LineKind.CANAL): 5.0, int(LineKind.STREAM): 3.0,
                int(LineKind.DITCH): 1.5}
# (channel depth below the lowest terrain, deck clearance above the water surface), metres
LINE_WATER = {int(LineKind.RIVER): (1.5, 3.0), int(LineKind.CANAL): (1.0, 2.0), int(LineKind.STREAM): (0.75, 1.5),
              int(LineKind.DITCH): (0.5, 1.0)}
AREA_WATER = {int(AreaKind.WATER_RIVER): (1.5, 3.0), int(AreaKind.WATER_LAKE): (0.5, 1.0),
              int(AreaKind.WATER_POND): (0.5, 1.0)}
SPAN_LINES = frozenset((int(LineKind.RIVER), int(LineKind.CANAL), int(LineKind.STREAM)))
WATER_LINES = SPAN_LINES | {int(LineKind.DITCH)}
POND_AREAS = frozenset((int(AreaKind.WATER_LAKE), int(AreaKind.WATER_POND)))
RAILING_M = 1.1
FOOT_RAILING_M = 1.3
CAR_MIN_REAL_M = 3.0
GALLI_MIN_M = 20.0
FOOT_CLASSES = frozenset(int(c) for c in (RoadClass.FOOTWAY, RoadClass.PATH, RoadClass.STEPS, RoadClass.CYCLEWAY,
                                           RoadClass.BRIDLEWAY))
# Decks of these classes are foot decks: FOOT_OVERBRIDGE over roads, the thinner deck, the taller railing.
FOOT_DECK_CLASSES = FOOT_CLASSES | {int(RoadClass.PEDESTRIAN)}
CAR_CLASSES = frozenset(int(c) for c in (
    RoadClass.MOTORWAY, RoadClass.TRUNK, RoadClass.PRIMARY, RoadClass.SECONDARY, RoadClass.TERTIARY,
    RoadClass.UNCLASSIFIED, RoadClass.RESIDENTIAL, RoadClass.LIVING_STREET, RoadClass.SERVICE, RoadClass.TRACK,
    RoadClass.ROAD))
# Maximum ramp grade by class (rise / run). Foot classes climb by stairs at the structure ends.
GRADE = {int(RoadClass.MOTORWAY): 0.05, int(RoadClass.TRUNK): 0.05, int(RoadClass.PRIMARY): 0.05,
         int(RoadClass.SECONDARY): 0.06, int(RoadClass.TERTIARY): 0.06, int(RoadClass.TRACK): 0.10,
         int(RoadClass.PEDESTRIAN): 0.08}
FOOT_GRADE = 0.5
# Classes a foot structure's stairs may continue into (its cones never enter motor roads).
STAIR_CLASSES = frozenset(int(c) for c in (RoadClass.FOOTWAY, RoadClass.PATH, RoadClass.STEPS, RoadClass.CYCLEWAY,
                                           RoadClass.BRIDLEWAY, RoadClass.PEDESTRIAN))
DEFAULT_GRADE = 0.08
KIND_RANK = {int(SK.BRIDGE): 5, int(SK.FLYOVER): 4, int(SK.UNDERPASS): 3, int(SK.TUNNEL): 2, int(SK.FORD): 1,
             int(SK.NONE): 0}
_YES_LINE_TUNNEL = frozenset({"yes", "culvert", "true", "1", "flooded"})


def grade(cls: int) -> float:
    return FOOT_GRADE if int(cls) in FOOT_CLASSES else GRADE.get(int(cls), DEFAULT_GRADE)


def deck_depth(cls: int) -> float:
    return FOOT_DECK_DEPTH_M if int(cls) in FOOT_DECK_CLASSES else DECK_DEPTH_M


def tunnel_type(r: RoadFeature, length_m: float) -> str | None:
    """``"tunnel"``, ``"passage"`` (an ordinary ground road: building passages, short layer >= 0 tunnels) or None."""
    if not r.tunnel:
        return None
    v = (r.extra.get("tunnel") or "yes").strip().lower()
    if v in ("building_passage", "avalanche_protector", "no"):
        return "passage"
    if int(r.layer) >= 0 and length_m <= PASSAGE_MAX_M:
        return "passage"
    return "tunnel"


def effective_layer(r: RoadFeature, ttype: str | None) -> int:
    if int(r.layer) != 0:
        return int(r.layer)
    if r.bridge:
        return 1
    if ttype == "tunnel":
        return -1
    return 0


def car_accessible(r: RoadFeature, real_m: float, galli: bool, kind: int) -> bool:
    """Decision 5: may a car use this way (module docstring)."""
    if int(r.cls) not in CAR_CLASSES:
        return False
    if not int(r.access) & int(Travel.CAR | Travel.JEEP):
        return False
    if real_m < CAR_MIN_REAL_M or galli:
        return False
    return int(kind) != int(SK.TUNNEL)


class LeafTerrain:
    """Terrain exactly as the runtime reads it: the leaf tiles' quantised ``HGHT`` samples, interpolated
    bilinearly inside the grid cell (DATA_FORMATS 1.1)."""

    def __init__(self, sample_game: Callable, tile_size: float, n: int) -> None:
        self.sample_game = sample_game
        self.size = float(tile_size)
        self.n = int(n)
        self.step = self.size / (self.n - 1)
        self._cache: dict[tuple[int, int, int, int], float] = {}

    def _corner(self, tx: np.ndarray, ty: np.ndarray, i: np.ndarray, j: np.ndarray) -> np.ndarray:
        from .tile_format import dequantize_heights, quantize_heights

        keys = list(zip(tx.tolist(), ty.tolist(), i.tolist(), j.tolist()))
        out = np.empty(len(keys))
        miss = [k for k, key in enumerate(keys) if key not in self._cache]
        if miss:
            mt, my, mi, mj = (np.array([keys[k][c] for k in miss]) for c in range(4))
            x = mt.astype(np.float64) * self.size + mi.astype(np.float64) * self.step
            z = my.astype(np.float64) * self.size + mj.astype(np.float64) * self.step
            h = np.asarray(self.sample_game(x, z, spacing_m=self.step), dtype=np.float64).reshape(-1)
            h = np.where(np.isfinite(h), h, 0.0)
            hq = dequantize_heights(quantize_heights(h)).astype(np.float64)
            for k, v in zip(miss, hq.tolist()):
                self._cache[keys[k]] = v
        for k, key in enumerate(keys):
            out[k] = self._cache[key]
        return out

    def __call__(self, x, z) -> np.ndarray:
        x = np.asarray(x, dtype=np.float64).reshape(-1)
        z = np.asarray(z, dtype=np.float64).reshape(-1)
        if not len(x):
            return np.zeros(0)
        tx = np.floor(x / self.size).astype(np.int64)
        ty = np.floor(z / self.size).astype(np.int64)
        fi = (x - tx * self.size) / self.step
        fj = (z - ty * self.size) / self.step
        i0 = np.clip(np.floor(fi).astype(np.int64), 0, self.n - 2)
        j0 = np.clip(np.floor(fj).astype(np.int64), 0, self.n - 2)
        u = np.clip(fi - i0, 0.0, 1.0)
        v = np.clip(fj - j0, 0.0, 1.0)
        h00 = self._corner(tx, ty, i0, j0)
        h10 = self._corner(tx, ty, i0 + 1, j0)
        h01 = self._corner(tx, ty, i0, j0 + 1)
        h11 = self._corner(tx, ty, i0 + 1, j0 + 1)
        return (h00 * (1 - u) + h10 * u) * (1 - v) + (h01 * (1 - u) + h11 * u) * v


@dataclass
class Feature:
    """A structure interval along a way (arc metres)."""

    a0: float
    a1: float
    kind: int
    flags: int = 0
    clearance_m: float = 0.0


@dataclass
class WayStructure:
    """What one way is built as. ``pts``/``role``/``h`` are set when the way carries heights (its densified
    polyline, the role per vertex and the absolute height per vertex, terrain where draped)."""

    layer: int = 0
    features: list[Feature] = field(default_factory=list)
    railing_m: float = 0.0
    pts: np.ndarray | None = None
    node_ids: np.ndarray | None = None
    cum: np.ndarray | None = None
    role: np.ndarray | None = None
    h: np.ndarray | None = None


@dataclass
class Crossing:
    upper: int
    lower: int
    x: float
    z: float
    a_upper: float
    a_lower: float
    sin: float


@dataclass
class Structures:
    """Result of ``analyse``: per road index (extract order) its structure; ``report`` lists every structure for
    the build report and docs/research/w2/data_detail_pass.md."""

    ways: dict[int, WayStructure] = field(default_factory=dict)
    kinds: np.ndarray = field(default_factory=lambda: np.zeros(0, dtype=np.int64))  # way-level dominant kind
    layers: np.ndarray = field(default_factory=lambda: np.zeros(0, dtype=np.int64))
    crossings: list[Crossing] = field(default_factory=list)
    report: list[dict] = field(default_factory=list)
    stats: dict = field(default_factory=dict)

    def polyline(self, i: int) -> np.ndarray | None:
        ws = self.ways.get(i)
        return None if ws is None or ws.pts is None else ws.pts

    def piece_record(self, i: int, piece_pts: np.ndarray, way_pts: np.ndarray, way_cum: np.ndarray,
                     has_prev: bool, has_next: bool, car_ok: bool) -> RoadStructureRec:
        """The ``RSTR`` record of one clipped piece of road ``i`` (``piece_pts``: the float piece with its context
        points, cut from ``way_pts``); ``shift_cm`` is left empty (``corridors`` fills it)."""
        ws = self.ways.get(i)
        flags = int(SF.CAR_ACCESSIBLE) if car_ok else 0
        layer = int(self.layers[i]) if len(self.layers) else 0
        if ws is None:
            return RoadStructureRec(kind=int(SK.NONE), layer=max(-128, min(127, layer)), flags=flags)
        arcs = wp.piece_arcs(way_pts, way_cum, piece_pts)
        lo = 1 if has_prev else 0
        hi = len(piece_pts) - (1 if has_next else 0)
        if arcs is None:
            a0, a1 = 0.0, float(way_cum[-1])
        else:
            a0, a1 = float(arcs[lo]), float(arcs[hi - 1])
        total = float(way_cum[-1])
        kind, clear = int(SK.NONE), 0.0
        for f in ws.features:
            if not _overlaps(f.a0, f.a1, a0, a1, total, wp.is_closed(way_pts)):
                continue
            flags |= int(f.flags)
            if KIND_RANK.get(int(f.kind), 0) > KIND_RANK.get(kind, 0):
                kind = int(f.kind)
            if f.clearance_m > 0:
                clear = f.clearance_m if clear <= 0 else min(clear, f.clearance_m)
        role = np.zeros(0, dtype=np.uint8)
        deck = np.zeros(0, dtype=np.int64)
        if ws.role is not None and arcs is not None:
            prof_h = wp.Profile(ws.cum, ws.h, float(ws.cum[-1]), wp.is_closed(ws.pts))
            r = _roles_at(ws.cum, ws.role, arcs, float(ws.cum[-1]), wp.is_closed(ws.pts))
            if (r[lo:hi] != DECK_DRAPED).any() or (r != DECK_DRAPED).any():
                role = r.astype(np.uint8)
                deck = np.rint(prof_h.at(arcs) * 100.0).astype(np.int64)
        has_deck = bool(len(role) and (role[lo:hi] == DECK_DECK).any())
        if kind in (int(SK.BRIDGE), int(SK.FLYOVER)) and not has_deck:
            # The piece only carries ramps or touches the structure's interval: report it as an approach.
            kind, flags = int(SK.NONE), (flags & ~int(SF.WATER_CROSSING | SF.OVER_ROAD | SF.FOOT_OVERBRIDGE)) \
                | (int(SF.APPROACH) if len(role) else 0)
        railing = ws.railing_m if kind in (int(SK.BRIDGE), int(SK.FLYOVER)) else 0.0
        return RoadStructureRec(kind=kind, layer=max(-128, min(127, layer)), flags=flags & 0xFF,
                                clearance_cm=int(round(clear * 100.0)), railing_dm=int(round(railing * 10.0)),
                                deck_role=role, deck_cm=deck)


def _overlaps(f0: float, f1: float, a0: float, a1: float, total: float, closed: bool) -> bool:
    if a1 < a0:
        a0, a1 = a1, a0
    if not closed or total <= 0:
        return f1 >= a0 and f0 <= a1
    for k in (-1, 0, 1, 2):
        if f1 + k * total >= a0 and f0 + k * total <= a1:
            return True
    return False


def _roles_at(cum: np.ndarray, role: np.ndarray, arcs: np.ndarray, total: float, closed: bool) -> np.ndarray:
    """Role at arbitrary arcs: a vertex keeps its role; a point between two vertices is draped only when both
    are, else DECK when either is a deck, else RAMP."""
    a = np.mod(arcs, total) if closed and total > 0 else np.clip(arcs, 0.0, total)
    k = np.clip(np.searchsorted(cum, a, side="right") - 1, 0, len(cum) - 1)
    out = np.empty(len(a), dtype=np.int64)
    for t, (kk, aa) in enumerate(zip(k.tolist(), a.tolist())):
        if abs(aa - cum[kk]) <= 1e-6 or kk == len(cum) - 1:
            out[t] = role[kk]
            continue
        if kk + 1 < len(cum) and abs(aa - cum[kk + 1]) <= 1e-6:
            out[t] = role[kk + 1]
            continue
        r0, r1 = int(role[kk]), int(role[min(kk + 1, len(role) - 1)])
        if r0 == DECK_DRAPED and r1 == DECK_DRAPED:
            out[t] = DECK_DRAPED
        elif DECK_DECK in (r0, r1):
            out[t] = DECK_DECK
        else:
            out[t] = DECK_RAMP
    return out


# ---------------------------------------------------------------------------------------------------------------
# Analysis
# ---------------------------------------------------------------------------------------------------------------
def _line_flag_tunnel(tags: dict) -> bool:
    return (tags.get("tunnel") or "").strip().lower() in _YES_LINE_TUNNEL


def _intervals_union(iv: list[tuple[float, float]]) -> list[tuple[float, float]]:
    out: list[tuple[float, float]] = []
    for a, b in sorted(iv):
        if out and a <= out[-1][1] + 1e-6:
            out[-1] = (out[-1][0], max(out[-1][1], b))
        else:
            out.append((a, b))
    return out


def _sin_angle(t1: np.ndarray, t2: np.ndarray) -> float:
    return abs(float(t1[0] * t2[1] - t1[1] * t2[0]))


def _point_parts(g) -> list[tuple[float, float]]:
    if g is None or g.is_empty:
        return []
    if g.geom_type == "Point":
        return [(g.x, g.y)]
    if hasattr(g, "geoms"):
        out = []
        for p in g.geoms:
            out += _point_parts(p)
        return out
    return []


def _line_parts(g) -> list:
    if g is None or g.is_empty:
        return []
    if g.geom_type == "LineString":
        return [g]
    if hasattr(g, "geoms"):
        out = []
        for p in g.geoms:
            out += _line_parts(p)
        return out
    return []


def analyse(roads: Sequence[RoadFeature], roads_game: Sequence[np.ndarray], node_ids: Sequence[np.ndarray],
            lines: Sequence, lines_game: Sequence[np.ndarray], areas: Sequence, area_geoms: Sequence,
            terrain: Callable[[np.ndarray, np.ndarray], np.ndarray],
            real_width: Callable[[int], float] | None = None,
            galli: Callable[[int], bool] | None = None) -> Structures:
    """Classify every way and solve deck heights (module docstring). ``roads_game``/``node_ids`` are the deduped
    game polylines (``oneway=-1`` reversed) and their node ids; ``real_width(i)`` gives road ``i``'s W2_DESIGN 4.1
    real width (for car access, widths in the report and lower-road corridors) and ``galli(i)`` whether a stretch
    of it is a galli (``corridors``)."""
    n = len(roads)
    res = Structures()
    res.kinds = np.zeros(n, dtype=np.int64)
    res.layers = np.zeros(n, dtype=np.int64)
    stats: dict = defaultdict(int)
    cums = [wp.cumulative(p) for p in roads_game]
    totals = np.array([float(c[-1]) if len(c) else 0.0 for c in cums])
    ttypes = [tunnel_type(r, totals[i]) for i, r in enumerate(roads)]
    layers = np.array([effective_layer(r, ttypes[i]) for i, r in enumerate(roads)], dtype=np.int64)
    res.layers = layers
    rw = real_width or (lambda i: 4.0)
    valid = np.array([len(p) >= 2 and totals[i] > 0 for i, p in enumerate(roads_game)], dtype=bool)
    road_lines = np.empty(n, dtype=object)
    road_lines[:] = [shapely.linestrings(p) if valid[i] else shapely.LineString() for i, p in enumerate(roads_game)]
    tree = shapely.STRtree(road_lines)

    deck_iv: dict[int, list[tuple[float, float]]] = defaultdict(list)
    deck_flags: dict[int, int] = defaultdict(int)
    span_iv: dict[int, list[tuple[float, float]]] = defaultdict(list)  # inferred water spans
    deck_req: list[tuple[int, float, float, float]] = []  # (way, a0, a1, h_min)
    water_names: dict[int, set] = defaultdict(set)
    bridges = [i for i in range(n) if valid[i] and roads[i].bridge and not roads[i].ford and ttypes[i] != "tunnel"]
    bridge_set = set(bridges)
    for i in bridges:
        deck_iv[i].append((0.0, float(totals[i])))
        deck_flags[i] |= int(SF.DECK_FROM_TAGS)

    # --- water ---------------------------------------------------------------------------------------------
    wl_idx = [k for k, ln in enumerate(lines) if int(ln.kind) in WATER_LINES and len(lines_game[k]) >= 2
              and not _line_flag_tunnel(ln.tags or {})]
    wa_idx = [k for k, a in enumerate(areas) if int(a.kind) in AREA_WATER and area_geoms[k] is not None
              and not area_geoms[k].is_empty]
    w_lines = np.empty(len(wl_idx), dtype=object)
    w_lines[:] = [shapely.linestrings(lines_game[k]) for k in wl_idx]
    w_cums = [wp.cumulative(lines_game[k]) for k in wl_idx]
    w_areas = np.empty(len(wa_idx), dtype=object)
    w_areas[:] = [area_geoms[k] for k in wa_idx]
    bridge_lines = np.empty(len(bridges), dtype=object)
    bridge_lines[:] = [road_lines[i] for i in bridges]
    btree = shapely.STRtree(bridge_lines) if len(bridges) else None

    def water_surface_line(m: int, x: float, z: float) -> tuple[float, float]:
        k = wl_idx[m]
        kind = int(lines[k].kind)
        depth, clear = LINE_WATER[kind]
        a = float(shapely.line_locate_point(w_lines[m], shapely.Point(x, z)))
        R = SURFACE_SEARCH_M[kind]
        arcs = np.arange(a - R, a + R + 1e-9, 4.0)
        pos, _ = wp.at_arcs(lines_game[k], w_cums[m], arcs)
        return float(np.min(terrain(pos[:, 0], pos[:, 1]))) - depth, clear

    if len(wl_idx):
        pairs = shapely.STRtree(w_lines).query(road_lines[valid], predicate="intersects")
        vidx = np.flatnonzero(valid)
        by_road: dict[int, list[int]] = defaultdict(list)
        for ri, li in zip(vidx[pairs[0]].tolist(), pairs[1].tolist()):
            by_road[ri].append(li)
    else:
        by_road = {}
    if len(wa_idx):
        apairs = shapely.STRtree(w_areas).query(road_lines[valid], predicate="intersects")
        vidx = np.flatnonzero(valid)
        by_road_a: dict[int, list[int]] = defaultdict(list)
        for ri, ai in zip(vidx[apairs[0]].tolist(), apairs[1].tolist()):
            by_road_a[ri].append(ai)
    else:
        by_road_a = {}

    for i in sorted(set(by_road) | set(by_road_a)):
        r = roads[i]
        if r.ford or ttypes[i] == "tunnel" or layers[i] < 0:
            continue
        line = road_lines[i]
        total = float(totals[i])
        tagged = i in bridge_set
        # Water-area inside intervals.
        area_iv: list[tuple[float, float, float, float, int]] = []  # a0, a1, surface, clear, area kind
        for ai in sorted(by_road_a.get(i, ())):
            k = wa_idx[ai]
            kind = int(areas[k].kind)
            for part in _line_parts(shapely.intersection(line, w_areas[ai])):
                c = shapely.get_coordinates(part)
                if len(c) < 2:
                    continue
                pa = shapely.line_locate_point(line, shapely.points(c[[0, -1]]))
                a0, a1 = float(min(pa)), float(max(pa))
                if a1 - a0 < 0.5:
                    continue
                st = np.arange(a0, a1 + 1e-9, 2.0)
                pos, _ = wp.at_arcs(roads_game[i], cums[i], st)
                depth, clear = AREA_WATER[kind]
                surf = float(np.min(terrain(pos[:, 0], pos[:, 1]))) - depth
                area_iv.append((a0, a1, surf, clear, kind))
                if areas[k].name is not None and (areas[k].name.en or areas[k].name.default):
                    water_names[i].add(areas[k].name.en or areas[k].name.default)
        spans: list[tuple[float, float, float]] = []  # a0, a1, h_min
        crossed = False
        for li in sorted(by_road.get(i, ())):
            k = wl_idx[li]
            kind = int(lines[k].kind)
            for (x, z) in _point_parts(shapely.intersection(line, w_lines[li])):
                if not tagged and kind not in SPAN_LINES:
                    continue
                if not tagged and btree is not None:
                    near = btree.query(shapely.Point(x, z), predicate="dwithin", distance=BRIDGE_SNAP_M)
                    if len(near):
                        # A mapping offset: the river belongs to the tagged bridge next to it.
                        b = bridges[int(sorted(near.tolist())[0])]
                        surf, clear = water_surface_line(li, x, z)
                        deck_req.append((b, 0.0, float(totals[b]), surf + clear))
                        deck_flags[b] |= int(SF.WATER_CROSSING)
                        stats["water_snapped_to_bridge"] += 1
                        continue
                crossed = True
                a = float(shapely.line_locate_point(line, shapely.Point(x, z)))
                surf, clear = water_surface_line(li, x, z)
                _, tan = wp.at_arcs(roads_game[i], cums[i], [a])
                _, ltan = wp.at_arcs(lines_game[k], w_cums[li], [float(shapely.line_locate_point(
                    w_lines[li], shapely.Point(x, z)))])
                s = max(_sin_angle(tan[0], ltan[0]), 0.33)
                w = float(lines[k].width_m) if lines[k].width_m and lines[k].width_m > 0 else LINE_WIDTH_M[kind]
                half = (0.5 * w + SPAN_MARGIN_M) / s
                a0, a1 = a - half, a + half
                for (b0, b1, asurf, aclear, _ak) in area_iv:
                    if b0 - SPAN_MARGIN_M <= a <= b1 + SPAN_MARGIN_M:
                        a0, a1 = min(a0, b0 - SPAN_MARGIN_M), max(a1, b1 + SPAN_MARGIN_M)
                        surf = min(surf, asurf)
                        clear = max(clear, aclear)
                spans.append((max(0.0, a0), min(total, a1), surf + clear))
                nm = lines[k].name
                if nm is not None and (nm.en or nm.default):
                    water_names[i].add(nm.en or nm.default)
        for (b0, b1, asurf, aclear, ak) in area_iv:
            covered = any(s0 <= b0 and b1 <= s1 for s0, s1, _ in spans)
            if covered:
                continue
            if tagged or (ak in POND_AREAS and b1 - b0 >= POND_MIN_M):
                crossed = True
                spans.append((max(0.0, b0 - SPAN_MARGIN_M), min(total, b1 + SPAN_MARGIN_M), asurf + aclear))
        if not spans:
            continue
        for a0, a1, h in spans:
            deck_req.append((i, a0, a1, h))
        if tagged:
            deck_flags[i] |= int(SF.WATER_CROSSING)
            stats["bridges_tagged_over_water"] += 1
        elif crossed:
            for a0, a1, _h in spans:
                span_iv[i].append((a0, a1))
            stats["bridges_inferred"] += 1
    for i, iv in span_iv.items():
        merged = _intervals_union(iv)
        span_iv[i] = merged
        deck_iv[i].extend(merged)
        deck_flags[i] |= int(SF.WATER_CROSSING)

    # --- grade separations -----------------------------------------------------------------------------------
    special = np.flatnonzero(valid & ((layers != 0) | np.array([r.bridge for r in roads], dtype=bool)))
    crossings: list[Crossing] = []
    if len(special):
        pairs = tree.query(road_lines[special], predicate="intersects")
        seen = set()
        for si, o in zip(special[pairs[0]].tolist(), pairs[1].tolist()):
            if si == o or not valid[o]:
                continue
            key = (min(si, o), max(si, o))
            if key in seen:
                continue
            seen.add(key)
            la, lb = int(layers[si]), int(layers[o])
            if la == lb:
                continue
            up, lo = (si, o) if la > lb else (o, si)
            # A grade separation needs a deck above (a bridge, or a way on layer > 0) or a tunnel below; a layer -1
            # road meeting a ground road without a node is a sloppy at-grade crossing, not an underpass.
            if not (roads[up].bridge or layers[up] > 0 or ttypes[lo] == "tunnel"):
                continue
            shared = set(np.asarray(node_ids[up]).tolist()) & set(np.asarray(node_ids[lo]).tolist())
            shared_pts = [roads_game[up][np.flatnonzero(np.asarray(node_ids[up]) == nid)[0]] for nid in shared
                          if nid > 0]
            for (x, z) in _point_parts(shapely.intersection(road_lines[up], road_lines[lo])):
                if any(math.hypot(x - p[0], z - p[1]) < 0.05 for p in shared_pts):
                    continue
                au = float(shapely.line_locate_point(road_lines[up], shapely.Point(x, z)))
                al = float(shapely.line_locate_point(road_lines[lo], shapely.Point(x, z)))
                _, tu = wp.at_arcs(roads_game[up], cums[up], [au])
                _, tl = wp.at_arcs(roads_game[lo], cums[lo], [al])
                crossings.append(Crossing(up, lo, float(x), float(z), au, al, max(_sin_angle(tu[0], tl[0]), 0.25)))
    crossings.sort(key=lambda c: (int(roads[c.upper].osm_id), int(roads[c.lower].osm_id), c.a_upper))
    res.crossings = crossings
    uppers = {c.upper for c in crossings}
    for i in sorted(uppers):
        if i in bridge_set or i in span_iv:
            deck_flags[i] |= int(SF.OVER_ROAD)
            continue
        if layers[i] > 0 and ttypes[i] is None:
            deck_iv[i].append((0.0, float(totals[i])))
            deck_flags[i] |= int(SF.DECK_FROM_TAGS | SF.OVER_ROAD)
            stats["flyovers_from_layer"] += 1
    for i in list(deck_iv):
        deck_iv[i] = _intervals_union(deck_iv[i])

    # --- kinds --------------------------------------------------------------------------------------------------
    kinds = np.zeros(n, dtype=np.int64)
    lower_of: dict[int, list[Crossing]] = defaultdict(list)
    for c in crossings:
        lower_of[c.lower].append(c)
    # Tunnel-tagged ways chained to an underpass (its approach cuttings, e.g. the Ring Road at Kalanki) are part of
    # that underpass, not tunnels.
    tunnel_ways = [i for i in range(n) if ttypes[i] == "tunnel" and valid[i]]
    tunnel_lowered: set[int] = set()
    if tunnel_ways:
        parent = {i: i for i in tunnel_ways}

        def find(a: int) -> int:
            while parent[a] != a:
                parent[a] = parent[parent[a]]
                a = parent[a]
            return a

        first: dict[int, int] = {}
        for i in tunnel_ways:
            for nid in set(np.asarray(node_ids[i]).tolist()):
                if nid <= 0:
                    continue
                if nid in first:
                    ra, rb = find(first[nid]), find(i)
                    if ra != rb:
                        parent[max(ra, rb)] = min(ra, rb)
                else:
                    first[nid] = i
        under_roots = {find(i) for i in tunnel_ways if i in lower_of}
        tunnel_lowered = {i for i in tunnel_ways if find(i) in under_roots}
    for i in range(n):
        if roads[i].ford:
            kinds[i] = int(SK.FORD)
        elif i in deck_iv:
            water = bool(deck_flags[i] & int(SF.WATER_CROSSING))
            over = bool(deck_flags[i] & int(SF.OVER_ROAD))
            kinds[i] = int(SK.BRIDGE) if water or not over else int(SK.FLYOVER)
            if over and not water and int(roads[i].cls) in FOOT_DECK_CLASSES:
                deck_flags[i] |= int(SF.FOOT_OVERBRIDGE)
        elif i in lower_of or i in tunnel_lowered:
            kinds[i] = int(SK.UNDERPASS)
        elif ttypes[i] == "tunnel":
            kinds[i] = int(SK.TUNNEL)
    res.kinds = kinds

    # --- heights -------------------------------------------------------------------------------------------------
    res._tunnel_lowered = tunnel_lowered  # type: ignore[attr-defined]
    _solve(res, roads, roads_game, node_ids, cums, totals, layers, ttypes, deck_iv, span_iv, deck_flags, deck_req,
           crossings, terrain, rw, stats)
    for k in ("tunnels", "fords"):
        stats.setdefault(k, 0)
    stats["tunnels"] = int((kinds == int(SK.TUNNEL)).sum())
    stats["fords"] = int((kinds == int(SK.FORD)).sum())
    stats["crossings"] = len(crossings)
    res.stats = dict(stats)
    res.report = _report(res, roads, totals, rw, water_names, deck_flags)
    return res


def _solve(res: Structures, roads, roads_game, node_ids, cums, totals, layers, ttypes, deck_iv, span_iv, deck_flags,
           deck_req, crossings, terrain, rw, stats) -> None:
    n = len(roads)
    lowered_ways = {c.lower for c in crossings if c.upper not in deck_iv and c.lower not in deck_iv
                    and ttypes[c.lower] == "tunnel"}
    core = set(deck_iv) | lowered_ways | {c.lower for c in crossings} | {c.upper for c in crossings}
    # Neighbourhood: ways sharing a node with the core, two hops out.
    by_node: dict[int, list[int]] = defaultdict(list)
    for i in range(n):
        if len(roads_game[i]) < 2:
            continue
        for nid in set(np.asarray(node_ids[i]).tolist()):
            if nid > 0:
                by_node[nid].append(i)
    ways = set(core)
    frontier = set(core)
    for _hop in range(2):
        nxt = set()
        for i in frontier:
            for nid in set(np.asarray(node_ids[i]).tolist()):
                if nid > 0:
                    nxt.update(by_node.get(nid, ()))
        nxt -= ways
        ways |= nxt
        frontier = nxt
    ways = sorted(w for w in ways if len(roads_game[w]) >= 2 and totals[w] > 0)
    if not core:
        _features(res, roads, totals, layers, ttypes, deck_iv, span_iv, deck_flags, crossings, {}, rw)
        return

    # --- stations ------------------------------------------------------------------------------------------------
    req_iv: dict[int, list[tuple[float, float, float]]] = defaultdict(list)
    for (i, a0, a1, h) in deck_req:
        req_iv[i].append((a0, a1, h))
    extra_arcs: dict[int, list[float]] = defaultdict(list)
    for i, iv in deck_iv.items():
        for a0, a1 in iv:
            extra_arcs[i] += [a0, a1]
    for i, iv in req_iv.items():
        for a0, a1, _h in iv:
            extra_arcs[i] += [a0, a1]
    flat: dict[tuple[int, int], tuple[float, float]] = {}
    for c in crossings:
        lw = max(4.8, 1.25 * rw(c.lower) + 4.0)
        f = min(60.0, (0.5 * lw + 1.0) / c.sin)
        flat[(id(c), 0)] = (c.a_upper - f, c.a_upper + f)
        uw = max(4.8, 1.25 * rw(c.upper) + 2.0)
        fl = min(60.0, (0.5 * uw + 1.0) / c.sin)
        flat[(id(c), 1)] = (c.a_lower - fl, c.a_lower + fl)
        extra_arcs[c.upper] += [c.a_upper - f, c.a_upper + f, c.a_upper]
        extra_arcs[c.lower] += [c.a_lower - fl, c.a_lower + fl, c.a_lower]

    st_way: list[int] = []
    st_arc: list[float] = []
    st_vertex: list[int] = []  # vertex index in the way, -1 for an inserted station
    way_st: dict[int, np.ndarray] = {}
    for w in ways:
        cum = cums[w]
        total = float(totals[w])
        grid = np.arange(0.0, total, STATION_M)
        arcs = np.concatenate([cum, grid, np.clip(np.array(extra_arcs.get(w, []), dtype=np.float64), 0.0, total)])
        is_v = np.concatenate([np.arange(len(cum)), np.full(len(arcs) - len(cum), -1)])
        o = np.lexsort((-(is_v >= 0).astype(np.int64), arcs))
        arcs, is_v = arcs[o], is_v[o]
        keep = np.ones(len(arcs), dtype=bool)
        last = -1e18
        last_v = False
        for t in range(len(arcs)):
            if arcs[t] - last < 0.05:
                if is_v[t] >= 0 and not last_v:
                    keep[t - 1] = False  # prefer the vertex
                    last, last_v = arcs[t], True
                    continue
                keep[t] = False
                continue
            last, last_v = arcs[t], is_v[t] >= 0
        arcs, is_v = arcs[keep], is_v[keep]
        start = len(st_arc)
        st_way += [w] * len(arcs)
        st_arc += arcs.tolist()
        st_vertex += is_v.tolist()
        way_st[w] = np.arange(start, start + len(arcs))
    ns = len(st_arc)
    st_way_a = np.array(st_way)
    st_arc_a = np.array(st_arc)
    st_vertex_a = np.array(st_vertex)
    pos = np.zeros((ns, 2))
    for w in ways:
        s = way_st[w]
        pos[s], _ = wp.at_arcs(roads_game[w], cums[w], st_arc_a[s])
        v = st_vertex_a[s]
        pos[s[v >= 0]] = np.asarray(roads_game[w])[v[v >= 0]]
    # Graph vertices: stations merged at shared OSM nodes.
    parent = np.arange(ns)

    def find(a: int) -> int:
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a

    first_at: dict[int, int] = {}
    for s in range(ns):
        v = st_vertex_a[s]
        if v < 0:
            continue
        nid = int(np.asarray(node_ids[st_way_a[s]])[v])
        if nid <= 0:
            continue
        if nid in first_at:
            ra, rb = find(first_at[nid]), find(s)
            if ra != rb:
                parent[max(ra, rb)] = min(ra, rb)
        else:
            first_at[nid] = s
    gv = np.array([find(s) for s in range(ns)])
    uniq, gidx = np.unique(gv, return_inverse=True)
    nv = len(uniq)
    vpos = np.zeros((nv, 2))
    vpos[gidx] = pos
    terr = terrain(vpos[:, 0], vpos[:, 1])

    # Edges and deck membership.
    e_u, e_v, e_len, e_g, e_deck, e_way = [], [], [], [], [], []
    st_deck = np.zeros(ns, dtype=bool)
    for w in ways:
        s = way_st[w]
        a = st_arc_a[s]
        iv = deck_iv.get(w, [])
        if iv:
            for a0, a1 in iv:
                st_deck[s[(a >= a0 - 1e-6) & (a <= a1 + 1e-6)]] = True
        g = grade(int(roads[w].cls))
        for t in range(len(s) - 1):
            L = float(a[t + 1] - a[t])
            mid = 0.5 * (a[t] + a[t + 1])
            dk = any(a0 - 1e-6 <= mid <= a1 + 1e-6 for a0, a1 in iv)
            e_u.append(int(gidx[s[t]]))
            e_v.append(int(gidx[s[t + 1]]))
            e_len.append(L)
            e_g.append(g)
            e_deck.append(dk)
            e_way.append(int(s[t]))
    e_u_a, e_v_a = np.array(e_u, dtype=np.int64), np.array(e_v, dtype=np.int64)
    e_len_a, e_g_a, e_deck_a = np.array(e_len), np.array(e_g), np.array(e_deck, dtype=bool)
    adj: list[list[int]] = [[] for _ in range(nv)]
    for k in range(len(e_u)):
        adj[e_u[k]].append(k)
        adj[e_v[k]].append(k)
    v_deck = np.zeros(nv, dtype=bool)
    v_ground = np.zeros(nv, dtype=bool)
    for k in range(len(e_u)):
        if e_deck_a[k]:
            v_deck[e_u[k]] = v_deck[e_v[k]] = True
        else:
            v_ground[e_u[k]] = v_ground[e_v[k]] = True
    deg = np.zeros(nv, dtype=np.int64)
    np.add.at(deg, e_u_a, 1)
    np.add.at(deg, e_v_a, 1)
    abut = v_deck & (v_ground | (deg <= 1))

    # 1. Base deck line: harmonic interpolation per deck component (Dirichlet at the abutments).
    base = terr.copy()
    _harmonic(base, v_deck, abut, e_u_a, e_v_a, e_len_a, e_deck_a, terr)
    base = np.where(v_deck, np.maximum(base, terr), terr)

    # Per-station requirement intervals (deck minima); crossing constraints depend on heights (iterated).
    # Stations under a deck never rise (the deck clears them), stations over a lowered underpass never sink.
    blocked_up = np.zeros(nv, dtype=bool)
    blocked_down = np.zeros(nv, dtype=bool)
    for c in crossings:
        for w, (f0, f1), blk in ((c.lower, flat[(id(c), 1)], blocked_up), (c.upper, flat[(id(c), 0)], blocked_down)):
            s = way_st.get(w)
            if s is None:
                continue
            a = st_arc_a[s]
            blk[gidx[s[(a >= f0 - 1e-6) & (a <= f1 + 1e-6)]]] = True
    foot_way = {w: int(roads[w].cls) in FOOT_DECK_CLASSES for w in ways}

    def station_req(H: np.ndarray) -> tuple[np.ndarray, np.ndarray, np.ndarray, np.ndarray]:
        req = [np.full(nv, -np.inf), np.full(nv, -np.inf)]  # vehicle, foot sources
        cap = [np.full(nv, np.inf), np.full(nv, np.inf)]
        for w, ivs in req_iv.items():
            if w not in way_st:
                continue
            s = way_st[w]
            a = st_arc_a[s]
            for a0, a1, h in ivs:
                m = (a >= a0 - 1e-6) & (a <= a1 + 1e-6)
                np.maximum.at(req[foot_way[w]], gidx[s[m]], h)
        for c in crossings:
            hl = _height_at(H, gidx, way_st, st_arc_a, c.lower, c.a_lower)
            hu = _height_at(H, gidx, way_st, st_arc_a, c.upper, c.a_upper)
            if not (math.isfinite(hl) and math.isfinite(hu)):
                continue
            if c.upper in deck_iv:
                f0, f1 = flat[(id(c), 0)]
                s = way_st[c.upper]
                a = st_arc_a[s]
                m = (a >= f0 - 1e-6) & (a <= f1 + 1e-6)
                np.maximum.at(req[foot_way[c.upper]], gidx[s[m]],
                              hl + MIN_UNDERPASS_CLEARANCE_M + deck_depth(int(roads[c.upper].cls)))
            elif c.lower in lowered_ways:
                f0, f1 = flat[(id(c), 1)]
                s = way_st[c.lower]
                a = st_arc_a[s]
                m = (a >= f0 - 1e-6) & (a <= f1 + 1e-6)
                np.minimum.at(cap[foot_way[c.lower]], gidx[s[m]],
                              hu - MIN_UNDERPASS_CLEARANCE_M - deck_depth(int(roads[c.upper].cls)))
        return req[0], req[1], cap[0], cap[1]

    e_stair = np.array([int(roads[st_way_a[s0]].cls) in STAIR_CLASSES for s0 in e_way], dtype=bool)
    H = base.copy()
    lowered = np.zeros(nv, dtype=bool)
    for _it in range(5):
        req_v, req_f, cap_v, cap_f = station_req(H)
        up = np.maximum(
            _cones_up(req_v, base, terr, v_deck, adj, e_u_a, e_v_a, e_len_a, e_g_a, None, blocked_up),
            _cones_up(req_f, base, terr, v_deck, adj, e_u_a, e_v_a, e_len_a, e_g_a, e_stair, blocked_up))
        Hn = np.where(v_deck, np.maximum(base, up), np.where(up > terr + EPS_M, up, terr))
        down = np.minimum(
            _cones_down(cap_v, terr, v_deck, adj, e_u_a, e_v_a, e_len_a, e_g_a, None, blocked_down),
            _cones_down(cap_f, terr, v_deck, adj, e_u_a, e_v_a, e_len_a, e_g_a, e_stair, blocked_down))
        low = (down < Hn - EPS_M) & ~v_deck
        Hn = np.where(low, down, Hn)
        if np.allclose(Hn, H, atol=1e-4) and _it > 0:
            H = Hn
            lowered = low
            break
        H = Hn
        lowered = low
    stats["stations"] = int(ns)

    # --- per-way roles, densified polylines ---------------------------------------------------------------------
    for w in ways:
        s = way_st[w]
        g = gidx[s]
        a = st_arc_a[s]
        role = np.where(st_deck[s], DECK_DECK,
                        np.where((H[g] > terr[g] + EPS_M) | (H[g] < terr[g] - EPS_M), DECK_RAMP, DECK_DRAPED))
        if not (role != DECK_DRAPED).any():
            continue
        # Keep every original vertex, plus the stations in and next to the non-draped stretches.
        near = role != DECK_DRAPED
        grow = near.copy()
        grow[1:] |= near[:-1]
        grow[:-1] |= near[1:]
        keep = grow | (st_vertex_a[s] >= 0)
        pts = pos[s[keep]]
        ids = np.where(st_vertex_a[s[keep]] >= 0,
                       np.asarray(node_ids[w])[np.maximum(st_vertex_a[s[keep]], 0)], 0)
        pts, ids_d = wp.dedupe(pts, ids)
        k_role = role[keep]
        k_h = H[g[keep]]
        dkeep = np.ones(int(keep.sum()), dtype=bool)
        pk = pos[s[keep]]
        dkeep[1:] = np.any(pk[1:] != pk[:-1], axis=1)
        ws = res.ways.setdefault(w, WayStructure(layer=int(layers[w])))
        ws.pts = pts
        ws.node_ids = ids_d
        ws.cum = wp.cumulative(pts)
        ws.role = k_role[dkeep].astype(np.uint8)
        ws.h = k_h[dkeep].astype(np.float64)
        ws._arcs = a[keep][dkeep]  # type: ignore[attr-defined]
        ws._lowered = lowered[g[keep]][dkeep]  # type: ignore[attr-defined]
        ws._terr = terr[g[keep]][dkeep]  # type: ignore[attr-defined]
        ws._lowered_way = w in lowered_ways or w in getattr(res, "_tunnel_lowered", ())  # type: ignore[attr-defined]
    _features(res, roads, totals, layers, ttypes, deck_iv, span_iv, deck_flags, crossings,
              {"H": H, "gidx": gidx, "way_st": way_st, "arc": st_arc_a, "terr": terr}, rw)
    stats["ways_with_heights"] = sum(1 for ws in res.ways.values() if ws.pts is not None)


def _height_at(H, gidx, way_st, st_arc, w: int, a: float) -> float:
    s = way_st.get(w)
    if s is None:
        return float("nan")
    return float(np.interp(a, st_arc[s], H[gidx[s]]))


def _harmonic(base, v_deck, abut, e_u, e_v, e_len, e_deck, terr) -> None:
    """Interior deck vertices: the harmonic interpolation (edge weights 1/length) of their abutments, in place."""
    from scipy.sparse import coo_matrix
    from scipy.sparse.csgraph import connected_components
    from scipy.sparse.linalg import spsolve

    nv = len(base)
    m = e_deck
    if not m.any():
        return
    u, v, L = e_u[m], e_v[m], np.maximum(e_len[m], 0.05)
    A = coo_matrix((np.ones(len(u)), (u, v)), shape=(nv, nv))
    ncomp, lab = connected_components(A, directed=False)
    for c in np.unique(lab[v_deck]).tolist():
        verts = np.flatnonzero((lab == c) & v_deck)
        fixed = abut[verts]
        if fixed.all():
            continue
        if not fixed.any():
            base[verts] = float(np.mean(terr[verts]))
            continue
        loc = -np.ones(nv, dtype=np.int64)
        free = verts[~fixed]
        loc[free] = np.arange(len(free))
        em = np.isin(u, verts)
        uu, vv, ww = u[em], v[em], 1.0 / L[em]
        rows, cols, vals = [], [], []
        b = np.zeros(len(free))
        diag = np.zeros(len(free))
        for a_, b_, w_ in zip(uu.tolist(), vv.tolist(), ww.tolist()):
            for p, q in ((a_, b_), (b_, a_)):
                if loc[p] < 0:
                    continue
                diag[loc[p]] += w_
                if loc[q] >= 0:
                    rows.append(loc[p])
                    cols.append(loc[q])
                    vals.append(-w_)
                else:
                    b[loc[p]] += w_ * base[q]
        rows += list(range(len(free)))
        cols += list(range(len(free)))
        vals += diag.tolist()
        M = coo_matrix((vals, (rows, cols)), shape=(len(free), len(free))).tocsr()
        x = spsolve(M, b)
        base[free] = np.asarray(x).reshape(-1)


def _cones_up(req, base, terr, v_deck, adj, e_u, e_v, e_len, e_g, e_ok, blocked) -> np.ndarray:
    """Max-plus propagation of minimum heights with the edge grades. On ground it stops where it meets the terrain,
    at ``blocked`` stations (under a deck) and ``MAX_RAMP_M`` from the structure; ``e_ok`` limits the edges (foot
    structures continue only into stair classes)."""
    lab = np.full(len(req), -np.inf)
    dist = np.zeros(len(req))
    heap = []
    for v in np.flatnonzero(np.isfinite(req)).tolist():
        if req[v] > base[v] + 1e-9 or v_deck[v]:
            lab[v] = req[v]
            heapq.heappush(heap, (-req[v], v))
    while heap:
        negh, u = heapq.heappop(heap)
        h = -negh
        if h < lab[u] - 1e-12:
            continue
        for k in adj[u]:
            if e_ok is not None and not e_ok[k]:
                continue
            v = e_v[k] if e_u[k] == u else e_u[k]
            cand = h - e_g[k] * e_len[k]
            if cand <= lab[v] + 1e-9:
                continue
            if not v_deck[v]:
                d = (0.0 if v_deck[u] else dist[u]) + e_len[k]
                if cand <= terr[v] + EPS_M or blocked[v] or d > MAX_RAMP_M:
                    continue
                dist[v] = d
            lab[v] = cand
            heapq.heappush(heap, (-cand, v))
    return lab


def _cones_down(cap, terr, v_deck, adj, e_u, e_v, e_len, e_g, e_ok, blocked) -> np.ndarray:
    """Min-plus propagation of maximum heights (lowered underpasses) until they meet the terrain (same limits as
    ``_cones_up``; decks are never lowered)."""
    lab = np.full(len(cap), np.inf)
    dist = np.zeros(len(cap))
    heap = []
    for v in np.flatnonzero(np.isfinite(cap)).tolist():
        lab[v] = cap[v]
        heapq.heappush(heap, (cap[v], v))
    while heap:
        h, u = heapq.heappop(heap)
        if h > lab[u] + 1e-12:
            continue
        for k in adj[u]:
            if e_ok is not None and not e_ok[k]:
                continue
            v = e_v[k] if e_u[k] == u else e_u[k]
            cand = h + e_g[k] * e_len[k]
            d = dist[u] + e_len[k]
            if cand >= lab[v] - 1e-9 or cand >= terr[v] - EPS_M or v_deck[v] or blocked[v] or d > MAX_RAMP_M:
                continue
            dist[v] = d
            lab[v] = cand
            heapq.heappush(heap, (cand, v))
    return lab


def _runs(mask: np.ndarray) -> list[tuple[int, int]]:
    out = []
    t = 0
    n = len(mask)
    while t < n:
        if mask[t]:
            u = t
            while u + 1 < n and mask[u + 1]:
                u += 1
            out.append((t, u))
            t = u + 1
        else:
            t += 1
    return out


def _features(res: Structures, roads, totals, layers, ttypes, deck_iv, span_iv, deck_flags, crossings, sol, rw) -> None:
    kinds = res.kinds
    for i in range(len(roads)):
        k = int(kinds[i])
        ws = res.ways.get(i)
        if k == int(SK.NONE) and ws is None and i not in deck_iv:
            continue
        ws = res.ways.setdefault(i, WayStructure(layer=int(layers[i])))
        total = float(totals[i])
        foot = int(roads[i].cls) in FOOT_DECK_CLASSES
        if k == int(SK.FORD):
            ws.features.append(Feature(0.0, total, k, 0))
        elif k == int(SK.TUNNEL):
            ws.features.append(Feature(0.0, total, k, int(SF.DECK_FROM_TAGS)))
        if i in deck_iv:
            fl = int(deck_flags.get(i, 0))
            dk = int(SK.BRIDGE) if (fl & int(SF.WATER_CROSSING)) or not (fl & int(SF.OVER_ROAD)) else int(SK.FLYOVER)
            ws.railing_m = FOOT_RAILING_M if foot else RAILING_M
            if i in span_iv and not roads[i].bridge:
                # Inferred spans: the feature covers the deck and its ramps in this way.
                for a0, a1 in span_iv[i]:
                    b0, b1 = a0, a1
                    if ws.role is not None:
                        arcs = getattr(ws, "_arcs")
                        nd = ws.role != DECK_DRAPED
                        for r0, r1 in _runs(nd):
                            if arcs[r0] <= a1 + 1e-6 and arcs[r1] >= a0 - 1e-6:
                                b0, b1 = min(b0, arcs[r0]), max(b1, arcs[r1])
                    ws.features.append(Feature(b0, b1, int(SK.BRIDGE), fl & ~int(SF.DECK_FROM_TAGS)))
            else:
                ws.features.append(Feature(0.0, total, dk, fl))
        if ws.role is not None and i not in deck_iv:
            arcs = getattr(ws, "_arcs")
            low = getattr(ws, "_lowered")
            for r0, r1 in _runs((ws.role == DECK_RAMP) & ~low):
                ws.features.append(Feature(float(arcs[r0]), float(arcs[r1]), int(SK.NONE), int(SF.APPROACH)))
            own = bool(getattr(ws, "_lowered_way", False))
            for r0, r1 in _runs((ws.role == DECK_RAMP) & low):
                if own:
                    ws.features.append(Feature(float(arcs[r0]), float(arcs[r1]), int(SK.UNDERPASS),
                                               int(SF.LOWERED | SF.DECK_FROM_TAGS)))
                else:
                    ws.features.append(Feature(float(arcs[r0]), float(arcs[r1]), int(SK.NONE),
                                               int(SF.LOWERED | SF.APPROACH)))
    # Underpass clearances.
    for c in crossings:
        ws = res.ways.setdefault(c.lower, WayStructure(layer=int(layers[c.lower])))
        if sol:
            hu = _height_at(sol["H"], sol["gidx"], sol["way_st"], sol["arc"], c.upper, c.a_upper)
            hl = _height_at(sol["H"], sol["gidx"], sol["way_st"], sol["arc"], c.lower, c.a_lower)
            clear = max(0.0, hu - deck_depth(int(roads[c.upper].cls)) - hl)
        else:
            clear = 0.0
        uw = max(4.8, 1.25 * rw(c.upper) + 2.0)
        f = min(60.0, (0.5 * uw + 1.0) / c.sin)
        ws.features.append(Feature(c.a_lower - f, c.a_lower + f, int(SK.UNDERPASS), 0, clear))
        c.clearance_m = clear  # type: ignore[attr-defined]
    for ws in res.ways.values():
        ws.features.sort(key=lambda f: (f.a0, f.a1, f.kind, f.flags, f.clearance_m))


def _report(res: Structures, roads, totals, rw, water_names, deck_flags) -> list[dict]:
    out = []
    lower_names: dict[int, list] = defaultdict(list)
    upper_names: dict[int, list] = defaultdict(list)
    for c in res.crossings:
        lower_names[c.upper].append(c)
        upper_names[c.lower].append(c)
    for i, ws in sorted(res.ways.items()):
        k = int(res.kinds[i])
        fl = 0
        clear = 0.0
        for f in ws.features:
            fl |= int(f.flags)
            if f.clearance_m > 0:
                clear = f.clearance_m if clear <= 0 else min(clear, f.clearance_m)
        approach = k == int(SK.NONE)
        if approach and not (fl & int(SF.APPROACH)):
            continue
        r = roads[i]
        name = ""
        if r.name is not None:
            name = r.name.en or r.name.default or ""
        rise = 0.0
        if ws.h is not None and ws.pts is not None:
            rise = float(np.max(ws.h - _terr_of(ws)))
        out.append({
            "osm_id": int(r.osm_id), "name": name, "name_ne": (r.name.ne if r.name is not None else "") or "",
            "class": RoadClass(int(r.cls)).name, "kind": SK(k).name, "flags": int(fl),
            "flag_names": [m.name for m in SF if fl & int(m)], "length_m": round(float(totals[i]), 1),
            "width_m": round(float(r.width_m), 2) if r.width_m else None, "real_width_m": round(float(rw(i)), 2),
            "layer": int(res.layers[i]), "lanes": int(r.lanes), "bridge_tag": (r.extra or {}).get("bridge"),
            "tunnel_tag": (r.extra or {}).get("tunnel"), "structure": (r.extra or {}).get("bridge:structure"),
            "water": sorted(water_names.get(i, ())), "clearance_m": round(clear, 2), "max_rise_m": round(rise, 2),
            "over": sorted({int(roads[c.lower].osm_id) for c in lower_names.get(i, ())}),
            "under": sorted({int(roads[c.upper].osm_id) for c in upper_names.get(i, ())}),
        })
    return out


def _terr_of(ws: WayStructure) -> np.ndarray:
    t = getattr(ws, "_terr", None)
    return t if t is not None else np.zeros(len(ws.h))
