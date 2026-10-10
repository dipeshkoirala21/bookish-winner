"""Road structures: bridges, flyovers, underpasses, tunnels and fords with absolute deck heights (W2 detail pass).

docs/W2_DETAIL_CONTRACT.md decisions 3-5 and docs/DATA_FORMATS.md section 1.15 (``RSTR``). Everything is computed
once per OSM way along its whole game-space polyline (``wayprofile``), so the pieces a way is cut into at tile
borders agree; ``Structures.piece_record`` then reads the values back for one clipped piece.

Classification (``analyse``)
----------------------------
* **Effective layer**: the OSM ``layer``; else +1 for a bridge and -1 for a tunnel (``tunnel=yes``; a
  ``building_passage`` and any tunnel of at most ``PASSAGE_MAX_M`` on layer >= 0 is a passage, i.e. a ground road).
* **Water crossings**: a road crosses a waterway line (river, stream, canal; ditches and drains only for tagged
  bridges; lines tagged ``tunnel``/``culvert`` never) or a water area (riverbank, lake, pond). A tagged bridge that
  crosses water is a BRIDGE with WATER_CROSSING. An untagged road crossing a waterway line gets an **inferred
  bridge span** there: the water area's inside interval when the crossing lies in one, else the line's width
  (tagged, or ``LINE_WIDTH_M`` by kind) plus ``SPAN_MARGIN_M`` on each side, divided by the sine of the crossing
  angle. Lakes and ponds also make spans where a road runs at least ``POND_MIN_M`` over them. A crossing within
  ``BRIDGE_SNAP_M`` of a tagged bridge belongs to that bridge only when the bridge is the same road (it shares a
  node with the crossing way, or runs parallel to it with the same class and name), is not a foot bridge next to a
  motor road, and does not cross that water line itself (``_snap_bridge``); fords never get spans.
* **Water surface**: the lowest terrain along the waterway within ``SURFACE_SEARCH_M`` of the crossing (or along the
  road inside a water area) minus the channel depth (``LINE_WATER``, ``AREA_WATER``); rivers and streams crossing
  URBAN or OLD_CORE ground are incised deeper (``INCISED_LINE_M``, ``INCISED_AREA_M``: the 30 m DEM smooths
  their sand-mined channels away, and the valley's bridges stand at street level).
* **Grade separations**: two ways whose lines cross at a point that is not a shared OSM node, on different
  effective layers. The higher one is the upper: a tagged bridge over it, or an untagged way on layer > 0 (a
  flyover mapped without ``bridge``), becomes a deck; it is a FLYOVER when it crosses no water (FOOT_OVERBRIDGE for
  footways, paths, steps, cycleways, bridleways and pedestrian streets), and gets OVER_ROAD. The lower one is an
  UNDERPASS with the clearance measured under the deck. A tunnel-tagged lower way under a ground road is lowered
  (LOWERED); a layer < 0 way meeting a ground road without a shared node and without a tunnel tag is an at-grade
  crossing (ignored), and so is a crossing within ``ABUTMENT_M`` (across the lower road) of a deck end that no
  other deck way continues: the bridge lands on that road there (``Structures.at_abutment``).
* TUNNEL: a tunnel that passes under no road (not drawn in W2, kept out of car routes); FORD: ``ford=yes``;
  PASSAGE: a way under a building that stays intact (``corridors.passages``), with the gateway's free height.

Deck heights (``_solve``)
-------------------------
Every way that carries a deck or a lowered profile, and every way within ``NEIGH_M`` of path from one, is cut into
**stations** every ``STATION_M`` (plus their vertices and every interval end). Stations at the same OSM node are
one graph vertex. Terrain is the runtime's: the leaf tiles' quantised ``HGHT`` samples, bilinear (``LeafTerrain``).

1. **Base deck line**: each connected run of deck stations is interpolated harmonically (linearly along a chain)
   between its **abutments**, the deck stations that touch a ground road or end a way, which sit on the terrain;
   a deck never runs below it or below the terrain under it.
2. **Bounds**: over water a deck is at least the water surface + the kind's clearance (rivers 3 m). Every station
   may sink at most ``MAX_CUT_M``; a riverside road under a river bridge (inside the river corridor, within
   ``RIVER_ZONE_M`` of the crossing) at most to the water surface + ``RIVER_FLOOR_M``.
3. **Crossings**: the upper road's stretch over the lower road's corridor (``Crossing.iv_u``) and the lower road's
   stretch under the upper road's corridor (``iv_l``, both at most ``MAX_FLAT_M`` either side) are coupled: every
   upper station is at least ``MIN_UNDERPASS_CLEARANCE_M`` + the deck depth above **every** lower station, so the
   clearance holds under the deck's whole width, not only at the centreline.
4. **One linear programme per connected neighbourhood** (``_solve_component``) finds the offsets from the terrain:
   along every ground edge the offset changes by at most the class grade per metre (``GRADE``; foot ways
   ``FOOT_GRADE``, stairs), along every deck edge the height itself does, so a road never steps and stations at a
   shared node are one value. It minimises raising (1 per metre of offset and metre of road), sinking (cheap for
   tunnel-tagged underpasses, the roads their cuttings continue as and riverside roads; else ``W_LOWER``),
   URBAN / OLD_CORE embankments over ``EMBANK_CAP_M`` (``W_EMBANK``), stations where the solved neighbourhood meets
   unsolved ways leaving the terrain (``W_PIN``), motor roads lifted by a foot structure (``W_PIN_FOOT``: its
   stairs end at the road instead) and, where the class grade cannot close a loop between a deck and the road
   under it, ramps steepened up to ``STEEP_GRADE`` (``W_STEEP``). Missing clearance costs ``W_CLEAR`` (in effect
   never). The active set grows (``INFLUENCE_*``) until no solution presses against its boundary.
5. Stations off the terrain become RAMP (embankments, cuttings) or keep DECK; ways that are not structures but
   carry such a ramp get APPROACH, lowered stretches LOWERED.

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
from .model import AreaKind, AreaType, LineKind, RoadClass, RoadFeature, RoadStructureFlags as SF, \
    RoadStructureKind as SK, Travel
from .tile_format import DECK_DECK, DECK_DRAPED, DECK_RAMP, RoadStructureRec

MIN_UNDERPASS_CLEARANCE_M = 5.5  # RoadClearance.MinUnderpassClearanceM
DECK_DEPTH_M = 1.2  # vehicle deck: surface to underside (the bridges package keeps its structure within it)
FOOT_DECK_DEPTH_M = 0.6
STATION_M = 5.0
EPS_M = 0.02
PASSAGE_MAX_M = 60.0
BRIDGE_SNAP_M = 20.0
ABUTMENT_M = 2.0  # a road whose centreline crosses a deck this close to its abutment meets the bridge there
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
# Channel depth below the lowest terrain where a river or stream crosses URBAN or OLD_CORE ground: the valley's
# rivers run in channels incised by decades of sand mining (the Bagmati at Thapathali "down a canyon"), which the
# 30 m DEM smooths away, so the street-level bridges stand several metres above the water.
INCISED_LINE_M = {int(LineKind.RIVER): 4.5, int(LineKind.STREAM): 2.5}  # by LineKind
INCISED_AREA_M = {int(AreaKind.WATER_RIVER): 4.5}  # by AreaKind (riverbanks)
INCISED_AREAS = frozenset((int(AreaType.URBAN), int(AreaType.OLD_CORE)))
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
KIND_RANK = {int(SK.BRIDGE): 6, int(SK.FLYOVER): 5, int(SK.UNDERPASS): 4, int(SK.TUNNEL): 3, int(SK.FORD): 2,
             int(SK.PASSAGE): 1, int(SK.NONE): 0}
MIN_CORRIDOR_M = 4.8  # RoadClearance.MinCorridorM
MIN_OVERHEAD_CLEARANCE_M = 4.5  # RoadClearance.MinOverheadClearanceM: the free height a PASSAGE gateway keeps
MAX_FLAT_M = 60.0  # a crossing's stretch over / under the other corridor never runs further than this either side
# Height solver (``_solve``): costs per metre of offset from the terrain and metre of road (a linear programme).
W_RAISE = 1.0
W_LOWER_CHEAP = 0.01  # tunnel-tagged underpasses and riverside roads under a river bridge sink first
W_LOWER = 1.0e4  # any other road sinks only when no deck can clear it (a ramp looping back under its own deck)
W_EMBANK = 50.0  # per metre an URBAN or OLD_CORE embankment rises above EMBANK_CAP_M
EMBANK_CAP_M = 3.0
W_PIN = 1.0e4  # leaving the terrain where the solved neighbourhood meets an unsolved way
W_PIN_FOOT = 1.0e5  # a motor road lifted by a foot structure (its stairs end at the road instead, steeper if need be)
W_STAIR = 1.0e4  # per metre a foot way steps beyond its stair grade
W_CLEAR = 1.0e7  # per metre of missing underpass clearance (decision 3: in effect never)
W_STEEP = 2.0e4  # per metre of rise beyond the class grade (per metre of road), up to STEEP_GRADE
STEEP_GRADE = 0.15
MAX_CUT_M = 12.0
NEIGH_M = 600.0  # solved neighbourhood: every way within this path distance of a structure
INFLUENCE_MARGIN_M = 4.0
INFLUENCE_GRADE = 0.05
# A road within this distance of a river's (stream's, canal's) bank line lies in its corridor (the floodplain a
# river bridge spans: the Bagmati and Bishnumati bank roads run up to ~70 m from the mapped centreline).
RIVERSIDE_M = {int(LineKind.RIVER): 80.0, int(LineKind.CANAL): 30.0, int(LineKind.STREAM): 30.0}
RIVER_ZONE_M = 300.0  # how far along the bank roads a riverside cutting may run
CUT_ZONE_M = 300.0  # how far a tunnel-tagged underpass's cutting may run on into the roads it continues as
RIVER_FLOOR_M = 1.0  # a lowered riverside road stays this far above the water surface
DEBUG = False  # keep the solver's arrays on Structures._dbg (tests and tools)
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
    iv_u: tuple[float, float] | None = None  # the upper road's arcs over the lower road's corridor
    iv_l: tuple[float, float] | None = None  # the lower road's arcs under the upper road's corridor
    need_m: float = 0.0  # clearance + upper deck depth
    clearance_m: float = 0.0  # solved: lowest upper surface - deck depth - highest lower surface over the overlap
    riverside: bool = False  # the lower road runs along the river the upper road bridges


@dataclass
class Structures:
    """Result of ``analyse``: per road index (extract order) its structure; ``report`` lists every structure for
    the build report and docs/research/w2/data_detail_pass.md."""

    ways: dict[int, WayStructure] = field(default_factory=dict)
    kinds: np.ndarray = field(default_factory=lambda: np.zeros(0, dtype=np.int64))  # way-level dominant kind
    layers: np.ndarray = field(default_factory=lambda: np.zeros(0, dtype=np.int64))
    crossings: list[Crossing] = field(default_factory=list)
    at_abutment: list[Crossing] = field(default_factory=list)  # crossings dropped where a deck lands on the road
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
            galli: Callable[[int], bool] | None = None,
            corridor_width: Callable[[int, float, float], float | None] | None = None,
            area_type: Callable[[np.ndarray, np.ndarray], np.ndarray] | None = None,
            passages: dict[int, float] | None = None) -> Structures:
    """Classify every way and solve deck heights (module docstring). ``roads_game``/``node_ids`` are the deduped
    game polylines (``oneway=-1`` reversed) and their node ids; ``real_width(i)`` gives road ``i``'s W2_DESIGN 4.1
    real width (for car access, widths in the report and lower-road corridors) and ``galli(i)`` whether a stretch
    of it is a galli (``corridors``). ``corridor_width(i, a0, a1)`` is the widest final corridor of road ``i``
    between arcs ``a0`` and ``a1`` (``corridors.Corridors.width``; else 1.25 x real + 4 m, at least 4.8 m),
    ``area_type(x, z)`` the AreaType grid (the URBAN / OLD_CORE embankment cap) and ``passages`` the ways that
    pass under a building that stays intact, with the free height their gateway keeps (kind PASSAGE)."""
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

    def incised(table: dict, kind: int, depth: float, x: float, z: float) -> float:
        if area_type is None or kind not in table:
            return depth
        at = int(np.asarray(area_type(np.array([x]), np.array([z]))).reshape(-1)[0])
        return max(depth, table[kind]) if at in INCISED_AREAS else depth

    def water_surface_line(m: int, x: float, z: float) -> tuple[float, float]:
        k = wl_idx[m]
        kind = int(lines[k].kind)
        depth, clear = LINE_WATER[kind]
        depth = incised(INCISED_LINE_M, kind, depth, x, z)
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
                mid = pos[len(pos) // 2]
                depth = incised(INCISED_AREA_M, kind, depth, float(mid[0]), float(mid[1]))
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
                    b = _snap_bridge(i, li, near, bridges, roads, road_lines, node_ids, w_lines, roads_game, cums,
                                     x, z) if len(near) else None
                    if b is not None:
                        # A mapping offset: the river belongs to the tagged bridge this road continues into.
                        surf, clear = water_surface_line(li, x, z)
                        deck_req.append((b, 0.0, float(totals[b]), surf + clear))
                        deck_flags[b] |= int(SF.WATER_CROSSING)
                        stats["water_snapped_to_bridge"] += 1
                        continue
                    if len(near):
                        stats["water_not_snapped"] += 1
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
    # A deck spans a road only when the road lies between its abutments: a crossing within ABUTMENT_M (across the
    # road, so along the deck / sin) of a deck end that no other deck continues is where the bridge lands on that
    # road (a junction mapped without a shared node, the bank road at a bridge head), not an underpass.
    deck_way = valid & np.array([(r.bridge or int(layers[i]) > 0) and ttypes[i] != "tunnel" for i, r in enumerate(roads)],
                                dtype=bool)
    node_deck: dict[int, int] = defaultdict(int)
    for i in np.flatnonzero(deck_way).tolist():
        for nid in set(int(v) for v in np.asarray(node_ids[i]).tolist() if v > 0):
            node_deck[nid] += 1

    def abutment_dist(u: int, a: float) -> float:
        if not deck_way[u] or wp.is_closed(roads_game[u]):
            return math.inf
        ids = np.asarray(node_ids[u])
        out = math.inf
        for nid, d in ((int(ids[0]), a), (int(ids[-1]), float(totals[u]) - a)):
            if nid > 0 and node_deck.get(nid, 0) > 1:
                continue  # another deck way continues the structure here
            out = min(out, d)
        return out

    kept = []
    for c in crossings:
        if abutment_dist(c.upper, c.a_upper) * c.sin < ABUTMENT_M:
            stats["crossings_at_abutment"] += 1
            res.at_abutment.append(c)
            continue
        kept.append(c)
    crossings = kept
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
    water = _water_zones(lines, wl_idx, w_lines, areas, wa_idx, w_areas, water_surface_line)
    _solve(res, roads, roads_game, node_ids, cums, totals, layers, ttypes, deck_iv, span_iv, deck_flags, deck_req,
           crossings, terrain, rw, stats, corridor_width, water, area_type, tunnel_lowered)
    # Passages under buildings that stay intact (decision 1 keeps them; the gateway keeps decision 2's height).
    for i, clear in sorted((passages or {}).items()):
        if not valid[i] or int(res.kinds[i]) != int(SK.NONE):
            continue
        res.kinds[i] = int(SK.PASSAGE)
        ws = res.ways.setdefault(i, WayStructure(layer=int(layers[i])))
        ws.features.append(Feature(0.0, float(totals[i]), int(SK.PASSAGE), int(SF.DECK_FROM_TAGS), float(clear)))
        ws.features.sort(key=lambda f: (f.a0, f.a1, f.kind, f.flags, f.clearance_m))
        stats["passages"] += 1
    for k in ("tunnels", "fords"):
        stats.setdefault(k, 0)
    stats["tunnels"] = int((kinds == int(SK.TUNNEL)).sum())
    stats["fords"] = int((kinds == int(SK.FORD)).sum())
    stats["crossings"] = len(crossings)
    res.stats = dict(stats)
    res.report = _report(res, roads, totals, rw, water_names, deck_flags)
    return res


def _snap_bridge(i: int, li: int, near, bridges, roads, road_lines, node_ids, w_lines, roads_game, cums,
                 x: float, z: float) -> int | None:
    """The tagged bridge an untagged road's water crossing belongs to (a mapping offset), or None: the bridge must be
    the same road (it shares a node with the crossing way, or runs parallel to it with the same class and name),
    must not be a foot bridge next to a motor road, and must not cross that water line itself (then the road's own
    crossing is a second one and gets its own span)."""
    ids_i = {int(v) for v in np.asarray(node_ids[i]).tolist() if v > 0}
    motor_i = int(roads[i].cls) not in FOOT_DECK_CLASSES
    name_i = (roads[i].name.default or roads[i].name.en) if roads[i].name is not None else ""
    for t in sorted(int(v) for v in np.asarray(near).tolist()):
        b = bridges[t]
        if b == i:
            continue
        if motor_i and int(roads[b].cls) in FOOT_DECK_CLASSES:
            continue
        if shapely.intersects(road_lines[b], w_lines[li]):
            continue
        same = bool(ids_i & {int(v) for v in np.asarray(node_ids[b]).tolist() if v > 0})
        if not same and name_i and int(roads[b].cls) == int(roads[i].cls):
            name_b = (roads[b].name.default or roads[b].name.en) if roads[b].name is not None else ""
            if name_b == name_i:
                a_i = float(shapely.line_locate_point(road_lines[i], shapely.Point(x, z)))
                a_b = float(shapely.line_locate_point(road_lines[b], shapely.Point(x, z)))
                _, ti = wp.at_arcs(roads_game[i], cums[i], [a_i])
                _, tb = wp.at_arcs(roads_game[b], cums[b], [a_b])
                same = abs(float(ti[0][0] * tb[0][0] + ti[0][1] * tb[0][1])) >= 0.9
        if same:
            return b
    return None


def _water_zones(lines, wl_idx, w_lines, areas, wa_idx, w_areas, water_surface_line) -> _Water | None:
    """River corridors: every river, stream and canal line grown by half its width + RIVERSIDE_M, and every
    riverbank area grown by 30 m."""
    span = [m for m, k in enumerate(wl_idx) if int(lines[k].kind) in SPAN_LINES]
    parts = []
    for m in span:
        ln = lines[wl_idx[m]]
        w = float(ln.width_m) if ln.width_m and ln.width_m > 0 else LINE_WIDTH_M[int(ln.kind)]
        parts.append(shapely.buffer(w_lines[m], 0.5 * w + RIVERSIDE_M.get(int(ln.kind), 30.0), quad_segs=4))
    for a, k in enumerate(wa_idx):
        if int(areas[k].kind) == int(AreaKind.WATER_RIVER):
            parts.append(shapely.buffer(w_areas[a], 30.0, quad_segs=4))
    if not span:
        return None
    zone = shapely.union_all(np.asarray(parts, dtype=object)) if parts else None
    geoms = np.empty(len(span), dtype=object)
    geoms[:] = [w_lines[m] for m in span]
    return _Water(zone, geoms, lambda k, x, z: water_surface_line(span[k], x, z)[0])


def _neighbourhood(core: set, roads_game, node_ids, cums, radius: float) -> list[int]:
    """Ways with a vertex within ``radius`` metres of path along the road network from any ``core`` way."""
    from scipy.sparse import coo_matrix
    from scipy.sparse.csgraph import dijkstra

    key: dict[int, int] = {}
    rows: list[int] = []
    cols: list[int] = []
    wts: list[float] = []
    way_nodes: dict[int, np.ndarray] = {}
    for i, p in enumerate(roads_game):
        if len(p) < 2:
            continue
        ids = np.asarray(node_ids[i]).tolist()
        idx = []
        for k, nid in enumerate(ids):
            kk = int(nid) if nid > 0 else -(i * 1_000_003 + k + 1)
            j = key.get(kk)
            if j is None:
                j = key[kk] = len(key)
            idx.append(j)
        L = np.maximum(np.diff(cums[i]), 1e-6)
        rows += idx[:-1]
        cols += idx[1:]
        wts += L.tolist()
        way_nodes[i] = np.asarray(idx, dtype=np.int64)
    if not key:
        return sorted(core)
    G = coo_matrix((wts, (rows, cols)), shape=(len(key), len(key))).tocsr()
    src = np.unique(np.concatenate([way_nodes[i] for i in core if i in way_nodes] or [np.zeros(0, np.int64)]))
    if not len(src):
        return sorted(core)
    d = dijkstra(G, directed=False, indices=src, limit=radius, min_only=True)
    reach = np.isfinite(d)
    return sorted(i for i, nodes in way_nodes.items() if i in core or reach[nodes].any())


def _within(src: list[int], adj, e_u, e_v, e_len, e_ok, radius: float, keep=None) -> list[int]:
    """Vertices within ``radius`` metres of path from ``src`` over the ``e_ok`` edges (and through ``keep``
    vertices only, when given), sources included."""
    dist = {int(v): 0.0 for v in src}
    heap = [(0.0, v) for v in sorted(dist)]
    while heap:
        d, u = heapq.heappop(heap)
        if d > dist.get(u, np.inf) + 1e-9:
            continue
        for k in adj[u]:
            if not e_ok[k]:
                continue
            v = int(e_v[k] if e_u[k] == u else e_u[k])
            nd = d + float(e_len[k])
            if nd <= radius and (keep is None or keep[v]) and nd < dist.get(v, np.inf) - 1e-9:
                dist[v] = nd
                heapq.heappush(heap, (nd, v))
    return sorted(dist)


def _maxplus(src: np.ndarray, adj, e_u, e_v, e_w) -> np.ndarray:
    """Max-plus propagation: out[v] = max_s (src[s] - sum of e_w along a path s -> v); -inf where unreached
    (stops at 0)."""
    lab = np.array(src, dtype=np.float64, copy=True)
    heap = [(-lab[v], v) for v in np.flatnonzero(lab > 0).tolist()]
    heapq.heapify(heap)
    while heap:
        negh, u = heapq.heappop(heap)
        h = -negh
        if h < lab[u] - 1e-12:
            continue
        for k in adj[u]:
            v = e_v[k] if e_u[k] == u else e_u[k]
            cand = h - e_w[k]
            if cand > lab[v] + 1e-9 and cand > 0:
                lab[v] = cand
                heapq.heappush(heap, (-cand, v))
    return lab


class _Water:
    """The river corridors (bank zones) and their water surfaces, for lowering riverside roads under a bridge."""

    def __init__(self, zone, line_geoms, surface_at) -> None:
        self.zone = zone
        self.lines = line_geoms
        self.tree = shapely.STRtree(line_geoms) if len(line_geoms) else None
        self.surface_at = surface_at  # (line index, x, z) -> water surface height
        if zone is not None and not zone.is_empty:
            shapely.prepare(zone)

    def mask(self, x: np.ndarray, z: np.ndarray) -> np.ndarray:
        if self.zone is None or self.zone.is_empty or not len(x):
            return np.zeros(len(x), dtype=bool)
        return np.asarray(shapely.contains_xy(self.zone, x, z), dtype=bool)

    def surface(self, x: float, z: float) -> float | None:
        """Water surface of the nearest river, stream or canal when (x, z) lies in a river corridor."""
        if self.tree is None or not self.mask(np.array([x]), np.array([z]))[0]:
            return None
        k = int(self.tree.nearest(shapely.Point(x, z)))
        return float(self.surface_at(k, x, z))


def _solve(res: Structures, roads, roads_game, node_ids, cums, totals, layers, ttypes, deck_iv, span_iv, deck_flags,
           deck_req, crossings, terrain, rw, stats, cw, water: _Water | None, area_type, tunnel_lowered: set) -> None:
    n = len(roads)
    lowered_ways = {c.lower for c in crossings if c.upper not in deck_iv and c.lower not in deck_iv
                    and ttypes[c.lower] == "tunnel"} | set(tunnel_lowered)
    joins = list(res.at_abutment)  # a deck landing on a road: the two meet there, at one height
    core = set(deck_iv) | lowered_ways | {c.lower for c in crossings} | {c.upper for c in crossings} \
        | {c.lower for c in joins}
    if not core:
        _features(res, roads, totals, layers, ttypes, deck_iv, span_iv, deck_flags, crossings, False)
        return
    ways = _neighbourhood(core, roads_game, node_ids, cums, NEIGH_M)
    ways = [w for w in ways if len(roads_game[w]) >= 2 and totals[w] > 0]
    way_set = set(ways)

    def corridor_w(i: int, a: float) -> float:
        # The widest corridor within one RATR sample spacing (the runtime draws the window minima of it).
        w = cw(i, a - 20.0, a + 20.0) if cw is not None else None
        return float(w) if w is not None and w > 0 else max(MIN_CORRIDOR_M, 1.25 * rw(i) + 4.0)

    # --- crossing intervals: the stretches of each road that lie over / under the other road's corridor -------
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
    for c in crossings:
        wu, wl = corridor_w(c.upper, c.a_upper), corridor_w(c.lower, c.a_lower)
        s = max(c.sin, 0.25)
        cot = math.sqrt(max(0.0, 1.0 - s * s)) / s
        fu = min(MAX_FLAT_M, (0.5 * wl + 1.0) / s + 0.5 * wu * cot)
        fl = min(MAX_FLAT_M, (0.5 * wu + 1.0) / s + 0.5 * wl * cot)
        c.iv_u = (max(0.0, c.a_upper - fu), min(float(totals[c.upper]), c.a_upper + fu))
        c.iv_l = (max(0.0, c.a_lower - fl), min(float(totals[c.lower]), c.a_lower + fl))
        c.need_m = MIN_UNDERPASS_CLEARANCE_M + deck_depth(int(roads[c.upper].cls))
        extra_arcs[c.upper] += [c.iv_u[0], c.iv_u[1], c.a_upper]
        extra_arcs[c.lower] += [c.iv_l[0], c.iv_l[1], c.a_lower]
    for c in joins:
        extra_arcs[c.upper].append(c.a_upper)
        extra_arcs[c.lower].append(c.a_lower)

    # --- stations ------------------------------------------------------------------------------------------------
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
    st_node = np.zeros(ns, dtype=np.int64)
    for s in range(ns):
        v = st_vertex_a[s]
        if v < 0:
            continue
        nid = int(np.asarray(node_ids[st_way_a[s]])[v])
        if nid <= 0:
            continue
        st_node[s] = nid
        if nid in first_at:
            ra, rb = find(first_at[nid]), find(s)
            if ra != rb:
                parent[max(ra, rb)] = min(ra, rb)
        else:
            first_at[nid] = s
    # A deck that lands on a road meets it there: the two stations are one vertex, like a shared node.
    for c in joins:
        if c.upper not in way_st or c.lower not in way_st:
            continue
        su = way_st[c.upper][int(np.argmin(np.abs(st_arc_a[way_st[c.upper]] - c.a_upper)))]
        sl = way_st[c.lower][int(np.argmin(np.abs(st_arc_a[way_st[c.lower]] - c.a_lower)))]
        ra, rb = find(int(su)), find(int(sl))
        if ra != rb:
            parent[max(ra, rb)] = min(ra, rb)
            stats["abutment_joins"] += 1
    gv = np.array([find(s) for s in range(ns)])
    uniq, gidx = np.unique(gv, return_inverse=True)
    nv = len(uniq)
    vpos = np.zeros((nv, 2))
    vpos[gidx] = pos
    terr = np.asarray(terrain(vpos[:, 0], vpos[:, 1]), dtype=np.float64)

    # Edges.
    e_u, e_v, e_len, e_g, e_deck, e_way = [], [], [], [], [], []
    st_deck = np.zeros(ns, dtype=bool)
    for w in ways:
        s = way_st[w]
        a = st_arc_a[s]
        iv = deck_iv.get(w, [])
        for a0, a1 in iv:
            st_deck[s[(a >= a0 - 1e-6) & (a <= a1 + 1e-6)]] = True
        g = grade(int(roads[w].cls))
        for t in range(len(s) - 1):
            mid = 0.5 * (a[t] + a[t + 1])
            e_u.append(int(gidx[s[t]]))
            e_v.append(int(gidx[s[t + 1]]))
            e_len.append(float(a[t + 1] - a[t]))
            e_g.append(g)
            e_deck.append(any(a0 - 1e-6 <= mid <= a1 + 1e-6 for a0, a1 in iv))
            e_way.append(w)
    ne = len(e_u)
    e_u_a, e_v_a = np.array(e_u, dtype=np.int64), np.array(e_v, dtype=np.int64)
    e_len_a, e_g_a, e_deck_a = np.maximum(np.array(e_len), 1e-6), np.array(e_g), np.array(e_deck, dtype=bool)
    e_cls = np.array([int(roads[w].cls) for w in e_way], dtype=np.int64)
    e_foot = np.isin(e_cls, list(FOOT_CLASSES))
    e_stair = np.isin(e_cls, list(STAIR_CLASSES))
    adj: list[list[int]] = [[] for _ in range(nv)]
    for k in range(ne):
        adj[e_u[k]].append(k)
        adj[e_v[k]].append(k)
    v_deck = np.zeros(nv, dtype=bool)
    v_ground = np.zeros(nv, dtype=bool)
    v_len = np.zeros(nv)
    np.add.at(v_len, e_u_a, 0.5 * e_len_a)
    np.add.at(v_len, e_v_a, 0.5 * e_len_a)
    v_len = np.maximum(v_len, 0.5)
    v_deck[e_u_a[e_deck_a]] = True
    v_deck[e_v_a[e_deck_a]] = True
    v_ground[e_u_a[~e_deck_a]] = True
    v_ground[e_v_a[~e_deck_a]] = True
    deg = np.zeros(nv, dtype=np.int64)
    np.add.at(deg, e_u_a, 1)
    np.add.at(deg, e_v_a, 1)
    abut = v_deck & (v_ground | (deg <= 1))
    v_motor = np.zeros(nv, dtype=bool)
    v_motor[e_u_a[~e_stair]] = True
    v_motor[e_v_a[~e_stair]] = True
    v_stair = np.zeros(nv, dtype=bool)
    v_stair[e_u_a[e_stair]] = True
    v_stair[e_v_a[e_stair]] = True

    # 1. Base deck line: harmonic interpolation per deck component (Dirichlet at the abutments).
    base = terr.copy()
    _harmonic(base, v_deck, abut, e_u_a, e_v_a, e_len_a, e_deck_a, terr)
    base = np.where(v_deck, np.maximum(base, terr), terr)

    # 2. Bounds on the offset from the terrain (delta = H - terrain).
    lo = np.full(nv, -MAX_CUT_M)
    lo[v_deck] = np.maximum(base[v_deck] - terr[v_deck], 0.0)
    v_foot_src = np.zeros(nv, dtype=bool)
    for w, ivs in req_iv.items():
        if w not in way_st:
            continue
        s = way_st[w]
        a = st_arc_a[s]
        for a0, a1, h in ivs:
            g = gidx[s[(a >= a0 - 1e-6) & (a <= a1 + 1e-6)]]
            lo[g] = np.maximum(lo[g], h - terr[g])
            if int(roads[w].cls) in FOOT_DECK_CLASSES:
                v_foot_src[g] = True
    wl = np.full(nv, W_LOWER)
    # Tunnel-tagged underpasses sink cheaply, and so do the ground roads their cuttings run on into.
    cut_src = [int(v) for w in sorted(lowered_ways) if w in way_st for v in np.unique(gidx[way_st[w]]).tolist()]
    for u in _within(cut_src, adj, e_u_a, e_v_a, e_len_a, ~e_deck_a, CUT_ZONE_M):
        wl[u] = W_LOWER_CHEAP

    def iv_vertices(w: int, a0: float, a1: float) -> np.ndarray:
        """The stations bracketing [a0, a1] (linear interpolation between them covers the whole interval)."""
        s = way_st.get(w)
        if s is None:
            return np.zeros(0, dtype=np.int64)
        a = st_arc_a[s]
        k0 = max(0, int(np.searchsorted(a, a0 + 1e-6, side="right")) - 1)
        k1 = min(len(a) - 1, int(np.searchsorted(a, a1 - 1e-6, side="left")))
        return np.unique(gidx[s[k0:k1 + 1]])

    cross_u: list[np.ndarray] = []
    cross_l: list[np.ndarray] = []
    for c in crossings:
        vu = iv_vertices(c.upper, *c.iv_u)
        vl = iv_vertices(c.lower, *c.iv_l)
        both = np.intersect1d(vu, vl)
        if len(both):  # a junction of the two ways inside the stretch: it carries the upper road
            vl = np.setdiff1d(vl, both)
            stats["crossings_sharing_a_junction"] += 1
        cross_u.append(vu)
        cross_l.append(vl)

    # 3. Riverside roads under a river bridge may sink toward the water (a cutting) before the deck rises.
    c_river = [False] * len(crossings)
    if water is not None:
        river_v = water.mask(vpos[:, 0], vpos[:, 1]) & ~v_deck
        for ci, c in enumerate(crossings):
            if any(a0 - 1e-6 <= c.a_lower <= a1 + 1e-6 for a0, a1 in deck_iv.get(c.lower, ())) \
                    or not (deck_flags.get(c.upper, 0) & int(SF.WATER_CROSSING)):
                continue
            if c.upper in span_iv and not roads[c.upper].bridge and not any(
                    a0 - 1e-6 <= c.a_upper <= a1 + 1e-6 for a0, a1 in span_iv[c.upper]):
                continue
            surf = water.surface(c.x, c.z)
            if surf is None:
                continue
            c_river[ci] = True
            floor = surf + RIVER_FLOOR_M
            # The bank roads reachable from the stretch under the deck without leaving the river corridor.
            for u in _within(cross_l[ci].tolist(), adj, e_u_a, e_v_a, e_len_a, ~e_deck_a, RIVER_ZONE_M, river_v):
                wl[u] = W_LOWER_CHEAP
                lo[u] = max(lo[u], min(floor - terr[u], 0.0))
        stats["riverside_crossings"] = int(sum(c_river))

    # 4. Pins: vertices shared with ways outside the solved neighbourhood stay on the terrain.
    pin = np.zeros(nv)
    node_ways: dict[int, int] = defaultdict(int)
    for i in range(n):
        if len(roads_game[i]) < 2:
            continue
        for nid in set(np.asarray(node_ids[i]).tolist()):
            if nid > 0 and i not in way_set:
                node_ways[nid] += 1
    for s in np.flatnonzero(st_node > 0).tolist():
        if node_ways.get(int(st_node[s]), 0):
            pin[gidx[s]] = W_PIN
    v_boundary = pin > 0

    if area_type is not None:
        at = np.asarray(area_type(vpos[:, 0], vpos[:, 1]), dtype=np.int64).reshape(-1)
        # Motor-road embankments only: a foot overbridge's stairs must climb the full clearance anyway.
        v_urban = np.isin(at, [int(AreaType.URBAN), int(AreaType.OLD_CORE)]) & ~v_deck & v_motor
    else:
        v_urban = np.zeros(nv, dtype=bool)

    # 5. Active vertices: everything a structure may move (influence cones with a margin), in components.
    e_w_inf = INFLUENCE_GRADE * e_len_a
    foot_cross = [int(roads[c.upper].cls) in FOOT_DECK_CLASSES for c in crossings]

    e_footdeck = e_deck_a & np.isin(e_cls, list(FOOT_DECK_CLASSES))
    v_vehdeck = np.zeros(nv, dtype=bool)
    v_vehdeck[e_u_a[e_deck_a & ~e_footdeck]] = True
    v_vehdeck[e_v_a[e_deck_a & ~e_footdeck]] = True

    def sources(margin: float, vehicle_only: bool) -> np.ndarray:
        dk = v_vehdeck if vehicle_only else v_deck
        src = np.where(dk | ((lo > 0) & ~(v_foot_src & vehicle_only)), np.maximum(lo, 0.0) + margin, -np.inf)
        for ci, c in enumerate(crossings):
            if vehicle_only and foot_cross[ci] and not c_river[ci] and c.lower not in lowered_ways:
                continue
            vu, vl = cross_u[ci], cross_l[ci]
            top = float(np.max(terr[vl] + np.maximum(lo[vl], 0.0))) if len(vl) else float(terr[vu].max())
            if len(vu):
                src[vu] = np.maximum(src[vu], top + c.need_m - terr[vu] + margin)
            if len(vl):
                src[vl] = np.maximum(src[vl], c.need_m + margin)
        return src

    v_cross = np.zeros(nv, dtype=bool)
    for ci in range(len(crossings)):
        v_cross[cross_u[ci]] = True
        v_cross[cross_l[ci]] = True
    margin = INFLUENCE_MARGIN_M
    veh = _maxplus(sources(margin, True), adj, e_u_a, e_v_a, e_w_inf) > 0
    pin_foot = v_motor & v_stair & ~veh & ~v_vehdeck & ~v_cross
    pin = np.where(pin_foot & (pin == 0), W_PIN_FOOT, pin)
    delta = np.zeros(nv)
    lp_stats: dict = defaultdict(int)
    for attempt in range(3):
        inf = _maxplus(sources(margin, False), adj, e_u_a, e_v_a, e_w_inf)
        active = (inf > 0) | v_deck | v_cross | (lo > 0)
        delta = np.zeros(nv)
        lp_stats = defaultdict(int)
        comps = _components(active, e_u_a, e_v_a, cross_u, cross_l)
        tight = False
        for comp in comps:
            d, info = _solve_component(comp, terr, base, lo, wl, pin, v_deck, v_urban, v_len, adj, e_u_a, e_v_a,
                                       e_len_a, e_g_a, e_deck_a, e_foot, active, crossings, cross_u, cross_l)
            delta[comp] = d
            for k, v in info.items():
                lp_stats[k] += v
            tight |= bool(info.get("tight_boundary", 0))
        if not tight:
            break
        margin *= 2.5
        stats["influence_retries"] += 1
    H = terr + delta
    lowered = delta < -EPS_M
    if DEBUG:
        res._dbg = dict(terr=terr, H=H, lo=lo, wl=wl, pin=pin, base=base, cross_u=cross_u, cross_l=cross_l,  # type: ignore[attr-defined]
                        gidx=gidx, way_st=way_st, arc=st_arc_a, vpos=vpos, v_deck=v_deck, v_urban=v_urban, adj=adj,
                        e_u=e_u_a, e_v=e_v_a, e_len=e_len_a, e_g=e_g_a, e_deck=e_deck_a, e_way=e_way)
    stats["stations"] = int(ns)
    for k, v in lp_stats.items():
        if k != "tight_boundary":
            stats[k] += v
    stats["pins_violated"] = int((np.abs(delta[v_boundary]) > 1e-3).sum())

    # Clearance at every crossing, over the whole overlap of the two corridors.
    for ci, c in enumerate(crossings):
        vu, vl = cross_u[ci], cross_l[ci]
        if len(vu) and len(vl):
            cl = float(H[vu].min()) - deck_depth(int(roads[c.upper].cls)) - float(H[vl].max())
        else:
            cl = 0.0
        c.clearance_m = max(0.0, cl)
        c.riverside = c_river[ci]
        if cl < MIN_UNDERPASS_CLEARANCE_M - 0.05:
            stats["crossings_short_of_clearance"] += 1
    # Motor-road ramps steeper than their class grade (connectors the class grade cannot close), per way (reported).
    e_way_a = np.asarray(e_way, dtype=np.int64)
    rise = np.abs(delta[e_u_a] - delta[e_v_a])
    steep = ~e_deck_a & ~e_foot & (rise > e_g_a * e_len_a + 0.01)
    res.steep_ramps = {}  # type: ignore[attr-defined]
    for k in np.flatnonzero(steep).tolist():
        w = int(e_way_a[k])
        res.steep_ramps[w] = round(max(res.steep_ramps.get(w, 0.0), float(rise[k] / e_len_a[k])), 3)  # type: ignore[attr-defined]
    # Embankments over the URBAN / OLD_CORE cap, per way (reported).
    over = v_urban & (delta > EMBANK_CAP_M + 0.05)
    res.embankments = {}  # type: ignore[attr-defined]
    for w in ways:
        g = gidx[way_st[w]]
        if over[g].any() and int(roads[w].cls) not in FOOT_DECK_CLASSES:
            res.embankments[w] = round(float(delta[g].max()), 2)  # type: ignore[attr-defined]

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
        k_h = np.where(k_role != DECK_DRAPED, H[g[keep]], terr[g[keep]])
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
        ws._lowered_way = w in lowered_ways  # type: ignore[attr-defined]
    _features(res, roads, totals, layers, ttypes, deck_iv, span_iv, deck_flags, crossings, True)
    stats["ways_with_heights"] = sum(1 for ws in res.ways.values() if ws.pts is not None)


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


def _components(active: np.ndarray, e_u, e_v, cross_u, cross_l) -> list[np.ndarray]:
    """Connected components of the active vertices (edges between active vertices, plus the two stretches of every
    crossing, which one clearance constraint couples)."""
    from scipy.sparse import coo_matrix
    from scipy.sparse.csgraph import connected_components

    nv = len(active)
    m = active[e_u] & active[e_v]
    rows = [e_u[m]]
    cols = [e_v[m]]
    for vu, vl in zip(cross_u, cross_l):
        allv = np.concatenate([vu, vl])
        if len(allv) > 1:
            rows.append(np.full(len(allv) - 1, allv[0]))
            cols.append(allv[1:])
    r = np.concatenate(rows) if rows else np.zeros(0, dtype=np.int64)
    c = np.concatenate(cols) if cols else np.zeros(0, dtype=np.int64)
    A = coo_matrix((np.ones(len(r)), (r, c)), shape=(nv, nv))
    _ncomp, lab = connected_components(A, directed=False)
    idx = np.flatnonzero(active)
    order = np.argsort(lab[idx], kind="stable")
    idx = idx[order]
    labs = lab[idx]
    cuts = np.flatnonzero(np.diff(labs)) + 1
    return [p for p in np.split(idx, cuts) if len(p)]


def _solve_component(comp, terr, base, lo, wl, pin, v_deck, v_urban, v_len, adj, e_u, e_v, e_len, e_g, e_deck,
                     e_foot, active, crossings, cross_u, cross_l) -> tuple[np.ndarray, dict]:
    """One linear programme: the offsets from the terrain of the component's vertices that keep every bound
    (decks, water, floors), every grade (decks: absolute heights; ground: the offset changes by at most the grade
    per metre, so a road never steps) and every clearance, at the least cost (raising 1 per metre of offset and
    metre of road, cheap lowering for tunnel-tagged underpasses and riverside roads, penalties for the rest).
    Vertices outside the component stay on the terrain."""
    from scipy.optimize import linprog
    from scipy.sparse import coo_matrix

    K = len(comp)
    loc = {int(v): t for t, v in enumerate(comp.tolist())}
    info: dict = defaultdict(int)
    info["lp_components"] = 1
    info["lp_vertices"] = K
    # Variables: p[K], n[K] (delta = p - n), then extras appended.
    cost: list[float] = []
    bounds: list[tuple[float, float | None]] = []
    for v in comp.tolist():
        cost.append(float(v_len[v] * W_RAISE + pin[v]))
        bounds.append((max(lo[v], 0.0), None))
    for v in comp.tolist():
        cost.append(float(v_len[v] * wl[v] + pin[v]))
        bounds.append((0.0, max(-lo[v], 0.0)))
    nvar = 2 * K
    rows: list[int] = []
    cols: list[int] = []
    vals: list[float] = []
    rhs: list[float] = []

    def add_row(entries: list[tuple[int, float]], b: float) -> None:
        r = len(rhs)
        for col, val in entries:
            rows.append(r)
            cols.append(col)
            vals.append(val)
        rhs.append(b)

    def dvar(v: int) -> list[tuple[int, float]]:
        t = loc[v]
        return [(t, 1.0), (K + t, -1.0)]

    def neg(e: list[tuple[int, float]]) -> list[tuple[int, float]]:
        return [(c, -x) for c, x in e]

    stair_cols: list[int] = []
    steep_cols: list[int] = []
    clear_cols: list[int] = []
    emb_cols: list[int] = []
    seen = set()
    boundary_edges: list[tuple[int, int, float]] = []  # (inside vertex, edge, limit)
    for v in comp.tolist():
        for k in adj[v]:
            if k in seen:
                continue
            seen.add(k)
            a, b = int(e_u[k]), int(e_v[k])
            L = float(e_len[k])
            g = float(e_g[k])
            if e_deck[k]:
                G = max(g, abs(base[a] - base[b]) / L)
                off = terr[a] - terr[b]  # deltas: (Ta + da) - (Tb + db) <= G L
                lim_ab, lim_ba = G * L - off, G * L + off
            else:
                lim_ab = lim_ba = g * L
            slack: list[tuple[int, float]] = []
            if e_foot[k]:
                cost.append(W_STAIR)
                bounds.append((0.0, None))
                slack = [(nvar, -1.0)]
                stair_cols.append(nvar)
                nvar += 1
            elif not e_deck[k] and g < STEEP_GRADE:
                # A ramp may steepen (to STEEP_GRADE at most) where the class grade cannot close a loop between a
                # deck and the road under it; it never steps.
                cost.append(W_STEEP * L)
                bounds.append((0.0, (STEEP_GRADE - g) * L))
                slack = [(nvar, -1.0)]
                steep_cols.append(nvar)
                nvar += 1
            ia, ib = a in loc, b in loc
            if ia and ib:
                add_row(dvar(a) + neg(dvar(b)) + slack, lim_ab)
                add_row(dvar(b) + neg(dvar(a)) + slack, lim_ba)
            elif ia:
                add_row(dvar(a) + slack, lim_ab)
                add_row(neg(dvar(a)) + slack, lim_ba)
                boundary_edges.append((a, k, min(lim_ab, lim_ba)))
            else:
                add_row(neg(dvar(b)) + slack, lim_ab)
                add_row(dvar(b) + slack, lim_ba)
                boundary_edges.append((b, k, min(lim_ab, lim_ba)))
    # URBAN / OLD_CORE embankments above the cap.
    for v in comp.tolist():
        if v_urban[v]:
            cost.append(W_EMBANK * float(v_len[v]))
            bounds.append((0.0, None))
            add_row(dvar(v) + [(nvar, -1.0)], EMBANK_CAP_M)
            emb_cols.append(nvar)
            nvar += 1
    # Clearances: M >= every lower height; every upper height >= M + need (soft).
    cis = [ci for ci in range(len(crossings)) if len(cross_u[ci]) and len(cross_l[ci])
           and int(cross_u[ci][0]) in loc]
    for ci in cis:
        m_col = nvar
        cost.append(0.0)
        bounds.append((None, None))
        s_col = nvar + 1
        clear_cols.append(s_col)
        cost.append(W_CLEAR)
        bounds.append((0.0, None))
        nvar += 2
        for y in cross_l[ci].tolist():
            add_row(dvar(y) + [(m_col, -1.0)], -float(terr[y]))
        for x in cross_u[ci].tolist():
            add_row(neg(dvar(x)) + [(m_col, 1.0), (s_col, -1.0)], float(terr[x]) - crossings[ci].need_m)
    A = coo_matrix((vals, (rows, cols)), shape=(len(rhs), nvar)).tocsr() if rhs else None
    r = linprog(np.asarray(cost), A_ub=A, b_ub=np.asarray(rhs) if rhs else None, bounds=bounds, method="highs")
    if r.status != 0 or r.x is None:
        info["lp_failed"] += 1
        return np.maximum(lo[comp], 0.0), info
    x = r.x
    d = x[:K] - x[K:2 * K]
    d = np.where(np.abs(d) < 1e-7, 0.0, d)
    for v, k, lim in boundary_edges:
        if abs(d[loc[v]]) > 1e-3 and abs(d[loc[v]]) >= lim - 1e-4:
            info["tight_boundary"] += 1
    info["stair_steps"] += int((x[stair_cols] > 1e-3).sum()) if stair_cols else 0
    info["steep_ramp_edges"] += int((x[steep_cols] > 1e-3).sum()) if steep_cols else 0
    info["clearance_slack"] += int((x[clear_cols] > 1e-3).sum()) if clear_cols else 0
    info["embankment_over_cap_vertices"] += int((x[emb_cols] > 0.05).sum()) if emb_cols else 0
    return d, info


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


def _features(res: Structures, roads, totals, layers, ttypes, deck_iv, span_iv, deck_flags, crossings,
              solved: bool) -> None:
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
    # Underpasses: the lower road's stretch under the upper road's corridor, with the clearance over all of it.
    for c in crossings:
        ws = res.ways.setdefault(c.lower, WayStructure(layer=int(layers[c.lower])))
        clear = float(c.clearance_m) if solved else 0.0
        a0, a1 = c.iv_l if c.iv_l is not None else (c.a_lower, c.a_lower)
        ws.features.append(Feature(a0, a1, int(SK.UNDERPASS), 0, clear))
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
