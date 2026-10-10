"""Road corridors and building trimming (W2 detail pass; docs/W2_DETAIL_CONTRACT.md decision 1).

Every drawn road gets a **clear corridor**: a band centred on its centreline (shifted sideways only next to
protected footprints) that no building or building part enters. ``RATR.corridor_dm`` stores its final game width
every 20 m, ``RSTR.shift_cm`` its shift, and the pipeline trims every building back to the band so the runtime,
the meshers and the routing all agree. The rule, per OSM way along its whole polyline (``wayprofile``; stations
every ``STATION_M``):

```text
S      = 2 x d                          d: the distance from the centreline within +-STATION_M/2 of the station
                                       to the nearest building outline (parts excluded), BEFORE trimming; inf when
                                       none lies within NEAR_M; 0 when the centreline runs through a footprint
R      = W2_DESIGN 4.1 real width       the plausible width tag (0.5 x class floor .. 40 m), else class x area
N      = min(max(R x Scale, Min), R + 6) W2_DESIGN 4.2-4.3 nominal game carriageway (Scale 1.25 motor, 1.15
                                       pedestrian street, 1.0 footway/path/steps; Min by class, oneway, dual)
E      = 2 x shoulder + footpath room   the widest footpaths the runtime may draw (roads.md 8.2) plus kerbs
lim    = S - 1.0                        0.5 m to the buildings on each side (the runtime smooths the samples
                                       with its own 30 m moving minimum, so the pipeline does not add another)
F      = max(MinCorridorM 4.8, R if tagged else the class floor)
C      = max(F, min(N + E, lim))        then a 1:20 taper (lower envelope), so it never jumps
```

So ``C`` is the real width widened by W2_DESIGN 4, never below 4.8 m (three motorbikes side by side, old-core
gallis included) and never below a surveyed width; where buildings stand closer than that, they are trimmed.

**Protected footprints** (``LANDMARK`` heroes and the temple, shikhara, stupa, chorten and shrine archetypes) are
never trimmed. Where one would intrude beside a way, the corridor shifts away from it (``SHIFT_MARGIN_M`` clear),
tapered 1:20 along the way (``_fit_between``); with protected footprints on both sides it narrows to the space
between them (``SQUEEZED``). ``RSTR.shift_cm`` samples the shift every ``SHIFT_SAMPLE_M`` with the largest shift of
each spacing, so a reader that interpolates never shifts less. A way that runs **into or through** a protected
footprint is clipped before anything else (``clip_at_protected``): it stops ``CLIP_MARGIN_M`` outside the outline
and is pulled back until its flat corridor end clears it; a way cut in two continues as an extra road with the same
OSM id. Decks passing over keep their middle, tunnels and passages pass under. The build fails when a corridor
still enters a protected footprint by more than ``PROTECTED_INTRUSION_MAX_M2`` (curated exceptions in
``config/curated/protected_intrusions.yaml``).

**Passages** (``passages``): ways under a building that stays intact (``tunnel=building_passage``, ``covered``,
``indoor``, short layer >= 0 tunnels; the dhoka passages into Newar bahal courtyards) never trim the buildings they
pass under; RSTR marks them PASSAGE with the gateway's free height.

**Trimming** (``trim_buildings``): every drawn road except TUNNEL and PASSAGE ways builds its band from the stations (each
sub-segment buffered by the larger of its two half-widths, round joins, ``TRIM_EPS_M`` extra; +0.5 m where the
corridor is shifted); each other building and part loses what lies inside the bands. Every remaining polygon of at
least ``SLIVER_M2`` is kept (a building a road cuts in two becomes two records with the same ``osm_ref``, the larger
first), holes under ``HOLE_MIN_M2`` are filled, and a footprint with nothing that large left is removed. The kept ones
get ``BuildingFrontFlags.TRIMMED_FOR_ROAD``.

The pieces' ``RATR`` samples are **window minima** of ``C`` over +-20 m (rounded down to the decimetre), so a
runtime that interpolates between samples, holds the nearest one or takes the smallest one near a point
(``RoadWidthModel.LimitAt``) always stays inside the trimmed band.

**Gallis** (``galli``): an untagged way of a minor car class (unclassified, residential, living street, service,
track, road; trunk to tertiary roads are car roads whatever the mapping says) whose space between buildings, measured with rays
perpendicular to the centreline (``RAY_M`` each side, both sides bounded; a station inside a footprint repeats
its neighbour), stays under ``structures.CAR_MIN_REAL_M`` + 1 m for at least ``structures.GALLI_MIN_M`` is a
galli: cars and buses keep out (decision 5), even though the corridor is widened for two-wheelers.

**Bands** for trimming are flat at a way's two ends (a dead end does not cut the house it stops at) and round at
every other vertex and station.
"""

from __future__ import annotations

import math
from collections import defaultdict
from dataclasses import dataclass, field
from typing import Callable, Sequence

import numpy as np
import shapely

from . import wayprofile as wp
from .structures import CAR_CLASSES, CAR_MIN_REAL_M, GALLI_MIN_M
from .model import AreaType, BuildingArchetype, BuildingFlags, RoadClass, RoadFeature, RoadStructureKind as SK, \
    Sidewalk
from . import tags as T

MIN_CORRIDOR_M = 4.8  # RoadClearance.MinCorridorM
CLEARANCE_M = 1.0
STATION_M = 10.0
RAY_M = 30.0
NEAR_M = 20.0
TAPER = 20.0
SAMPLE_M = 20.0
SHIFT_SAMPLE_M = 5.0  # RSTR shift_cm spacing (DATA_FORMATS 1.15)
SHIFT_MARGIN_M = 0.5  # covers the 1 : 20 taper between the 10 m stations (a band never comes closer than ~0.2 m)
TRIM_EPS_M = 0.05
SIMPLIFY_M = 0.03
OPEN_M = 0.05  # opening radius against hair-thin spikes after the difference
SLIVER_M2 = 4.0
HOLE_MIN_M2 = 1.0
MIN_INTRUSION_M2 = 0.01
PROTECTED_ARCHETYPES = frozenset(int(a) for a in (BuildingArchetype.TEMPLE_PAGODA, BuildingArchetype.TEMPLE_SHIKHARA,
                                                   BuildingArchetype.STUPA, BuildingArchetype.CHORTEN,
                                                   BuildingArchetype.SHRINE))
FOOT = frozenset(int(c) for c in (RoadClass.FOOTWAY, RoadClass.PATH, RoadClass.STEPS, RoadClass.CYCLEWAY,
                                   RoadClass.BRIDLEWAY))
MAJOR = frozenset(int(c) for c in (RoadClass.MOTORWAY, RoadClass.TRUNK, RoadClass.PRIMARY, RoadClass.SECONDARY,
                                    RoadClass.TERTIARY))
MOTOR = MAJOR | frozenset(int(c) for c in (RoadClass.UNCLASSIFIED, RoadClass.RESIDENTIAL, RoadClass.LIVING_STREET,
                                           RoadClass.SERVICE, RoadClass.TRACK, RoadClass.ROAD, RoadClass.UNKNOWN))

# W2_DESIGN 4.1 real widths (columns OLD_CORE, URBAN, PERI_URBAN, RURAL, HILL), mirroring RoadWidthModel.
_COL = {int(AreaType.OLD_CORE): 0, int(AreaType.URBAN): 1, int(AreaType.PERI_URBAN): 2, int(AreaType.RURAL): 3,
        int(AreaType.FOREST): 3, int(AreaType.HILL): 4, int(AreaType.UNKNOWN): 1}
_W = {int(RoadClass.SECONDARY): (6.0, 7.0, 7.0, 5.5, 5.75), int(RoadClass.TERTIARY): (5.0, 7.0, 6.0, 4.5, 4.5),
      int(RoadClass.UNCLASSIFIED): (3.5, 4.0, 5.0, 3.5, 3.5), int(RoadClass.ROAD): (3.5, 4.0, 5.0, 3.5, 3.5),
      int(RoadClass.UNKNOWN): (3.5, 4.0, 5.0, 3.5, 3.5), int(RoadClass.RESIDENTIAL): (4.0, 5.0, 5.0, 3.5, 3.5),
      int(RoadClass.LIVING_STREET): (3.0, 3.0, 3.5, 3.0, 3.0), int(RoadClass.SERVICE): (3.5, 4.0, 4.5, 3.5, 3.0),
      int(RoadClass.TRACK): (3.0,) * 5, int(RoadClass.PEDESTRIAN): (4.0, 4.0, 4.0, 4.0, 3.0),
      int(RoadClass.FOOTWAY): (1.75, 2.0, 2.0, 1.5, 1.2), int(RoadClass.CYCLEWAY): (1.75, 2.0, 2.0, 1.5, 1.2),
      int(RoadClass.PATH): (1.5, 1.5, 1.5, 1.2, 1.0), int(RoadClass.BRIDLEWAY): (1.5, 1.5, 1.5, 1.2, 1.0),
      int(RoadClass.STEPS): (1.5, 2.0, 1.5, 1.2, 1.2)}
_FLOOR = {int(RoadClass.MOTORWAY): 6.0, int(RoadClass.TRUNK): 6.0, int(RoadClass.PRIMARY): 5.0,
          int(RoadClass.SECONDARY): 4.0, int(RoadClass.TERTIARY): 3.0, int(RoadClass.RESIDENTIAL): 2.5,
          int(RoadClass.UNCLASSIFIED): 2.5, int(RoadClass.SERVICE): 2.5, int(RoadClass.LIVING_STREET): 2.5,
          int(RoadClass.ROAD): 2.5, int(RoadClass.UNKNOWN): 2.5, int(RoadClass.TRACK): 2.0,
          int(RoadClass.PEDESTRIAN): 2.0, int(RoadClass.FOOTWAY): 0.9, int(RoadClass.STEPS): 0.9,
          int(RoadClass.CYCLEWAY): 0.9}


def floor_m(cls: int) -> float:
    return _FLOOR.get(int(cls), 0.6)


def tagged_width(r: RoadFeature) -> float | None:
    """The plausible ``width`` tag (RoadWidthModel.TagPlausible: 0.5 x floor .. 40 m), else None."""
    w = r.width_m
    if w is None or not w > 0:
        return None
    w = float(w)
    return w if 0.5 * floor_m(int(r.cls)) <= w <= 40.0 else None


def car_width(r: RoadFeature, real: float) -> float:
    """The width decision 5 tests (``structures.car_accessible``): the plausible ``width`` tag as surveyed, else the
    W2_DESIGN 4.1 default ``real`` (never the class floor a narrow tag is clamped to for drawing)."""
    t = tagged_width(r)
    return float(t) if t is not None else float(real)


_NO = frozenset({"", "no", "false", "0"})


def is_passage(r: RoadFeature, length_m: float | None = None) -> bool:
    """A way under a building that stays intact: ``tunnel=building_passage``, ``covered`` or ``indoor`` (any value
    but no), or a tunnel of at most 60 m on layer >= 0 (``structures.tunnel_type`` "passage")."""
    ex = r.extra or {}
    t = (ex.get("tunnel") or "").strip().lower()
    if t == "building_passage":
        return True
    if (ex.get("covered") or "").strip().lower() not in _NO or (ex.get("indoor") or "").strip().lower() not in _NO:
        return True
    return bool(r.tunnel) and t not in ("avalanche_protector", "no") and int(r.layer) >= 0 \
        and length_m is not None and length_m <= 60.0


def passages(roads: Sequence[RoadFeature], roads_game: Sequence[np.ndarray], outlines: Sequence[np.ndarray],
             min_heights: Sequence[float | None]) -> dict[int, float]:
    """Passage ways (``is_passage``) -> the free height their gateway keeps: ``RoadClearance.MinOverheadClearanceM``
    4.5 m, or the tagged ``building:min_height`` of a footprint they run under when that is higher. They never trim
    the buildings they pass under (decision 1 keeps the house; the buildings package meshes the gateway)."""
    from .structures import MIN_OVERHEAD_CLEARANCE_M

    cand = []
    for i, r in enumerate(roads):
        p = roads_game[i]
        if len(p) < 2:
            continue
        if is_passage(r, float(wp.cumulative(p)[-1])):
            cand.append(i)
    out: dict[int, float] = {}
    if not cand:
        return out
    polys = np.empty(len(outlines), dtype=object)
    polys[:] = [shapely.polygons(np.asarray(o, dtype=np.float64)) if len(o) >= 3 else shapely.Polygon()
                for o in outlines]
    tree = shapely.STRtree(polys)
    for i in cand:
        ln = shapely.linestrings(np.asarray(roads_game[i], dtype=np.float64))
        hits = tree.query(ln, predicate="intersects")
        mh = [float(min_heights[k]) for k in hits.tolist() if min_heights[k]]
        out[i] = max([MIN_OVERHEAD_CLEARANCE_M] + mh)
    return out


def real_width(r: RoadFeature, area: int) -> float:
    """W2_DESIGN 4.1 real width with RoadWidthModel.RealWidthM's tag plausibility."""
    cls = int(r.cls)
    t = tagged_width(r)
    if t is not None:
        w = t
    else:
        col = _COL.get(int(area), 1)
        oneway = bool(r.oneway)
        if cls in (int(RoadClass.MOTORWAY), int(RoadClass.TRUNK)):
            w = 7.0 if oneway else (14.0, 14.0, 14.0, 9.0, 7.5)[col]
        elif cls == int(RoadClass.PRIMARY):
            w = 14.0 if (not oneway and int(r.lanes) >= 4 and col <= 2) else (7.0, 7.0, 7.0, 6.5, 7.0)[col]
        else:
            w = _W.get(cls, (4.0,) * 5)[col]
    return max(floor_m(cls), min(40.0, w))


def scale_for(cls: int) -> float:
    if int(cls) == int(RoadClass.PEDESTRIAN):
        return 1.15
    return 1.0 if int(cls) in FOOT else 1.25


def min_game_m(cls: int, oneway: bool, dual: bool, area: int) -> float:
    """W2_DESIGN 4.3 (RoadWidthModel.MinGameWidthM)."""
    cls = int(cls)
    if cls in FOOT:
        if cls == int(RoadClass.STEPS):
            return 1.2
        return 1.2 if int(area) == int(AreaType.OLD_CORE) else 1.5
    if cls in (int(RoadClass.PEDESTRIAN), int(RoadClass.TRACK), int(RoadClass.LIVING_STREET)):
        return 3.0
    if dual:
        return 7.0
    if oneway:
        return 4.0
    return 6.5 if cls in MAJOR else 4.0


def shoulder_m(cls: int, area: int) -> float:
    col = _COL.get(int(area), 1)
    cls = int(cls)
    if cls in (int(RoadClass.MOTORWAY), int(RoadClass.TRUNK)):
        return 1.5 if col == 3 else 1.0 if col == 4 else 0.0
    if cls == int(RoadClass.PRIMARY):
        return 1.0 if col == 4 else 0.0
    if cls == int(RoadClass.SECONDARY):
        return 0.75 if col == 4 else 0.0
    return 0.0


def footpath_room(r: RoadFeature, area: int, dual: bool, heritage: bool) -> tuple[float, float]:
    """The widest footpath (+ kerb) the runtime may draw on each side (RoadWidthModel.NominalFootpaths, maxima)."""
    cls = int(r.cls)
    if cls not in MOTOR or cls == int(RoadClass.TRACK) or heritage:
        return 0.0, 0.0
    w = {int(RoadClass.MOTORWAY): 3.0, int(RoadClass.TRUNK): 3.0, int(RoadClass.PRIMARY): 3.5,
         int(RoadClass.SECONDARY): 2.0, int(RoadClass.TERTIARY): 2.0}.get(cls, 1.5) * 1.15 + 0.15
    sw = T.parse_sidewalk(r.extra or {})
    if int(r.oneway) < 0:
        sw = {Sidewalk.LEFT: Sidewalk.RIGHT, Sidewalk.RIGHT: Sidewalk.LEFT}.get(sw, sw)
    left = right = 0.0
    if sw in (Sidewalk.NONE, Sidewalk.SEPARATE):
        return 0.0, 0.0
    if sw == Sidewalk.LEFT:
        left = w
    elif sw == Sidewalk.RIGHT:
        right = w
    elif sw == Sidewalk.BOTH:
        left = right = w
    elif int(area) == int(AreaType.URBAN) and cls in MAJOR:
        left = right = w
    if dual:
        right = 0.0
    return left, right


def is_protected(b) -> bool:
    return bool(int(b.flags) & int(BuildingFlags.LANDMARK)) or int(b.archetype) in PROTECTED_ARCHETYPES


@dataclass
class WayCorridor:
    arcs: np.ndarray  # station arcs (m)
    c: np.ndarray  # corridor width (m)
    shift: np.ndarray  # lateral shift (m), + = left of the point order
    space: np.ndarray  # measured S (m), inf = open
    total: float
    closed: bool
    squeezed: bool = False
    galli: bool = False

    def samples(self, a0: float, length: float) -> tuple[np.ndarray, np.ndarray]:
        """RATR ``corridor_dm`` and ``shift_cm`` for a piece whose first rendered point lies at arc ``a0`` and whose
        rendered length is ``length``: samples every 20 m, window minima of the corridor over +-20 m."""
        k = int(math.floor(max(0.0, length) / SAMPLE_M + 1e-9)) + 1
        arcs = a0 + SAMPLE_M * np.arange(k)
        pc = wp.Profile(self.arcs, self.c, self.total, self.closed)
        c = pc.window_min(arcs, SAMPLE_M)
        dm = np.maximum(1, np.floor(c * 10.0 + 1e-6)).astype(np.int64)
        if np.any(self.shift != 0):
            # Shifts every SHIFT_SAMPLE_M, each the largest shift (by side) within one spacing: a reader that
            # interpolates linearly between them never shifts less than the profile does.
            ks = int(math.floor(max(0.0, length) / SHIFT_SAMPLE_M + 1e-9)) + 1
            sa = a0 + SHIFT_SAMPLE_M * np.arange(ks)
            pos = wp.Profile(self.arcs, np.maximum(self.shift, 0.0), self.total, self.closed)
            neg = wp.Profile(self.arcs, np.maximum(-self.shift, 0.0), self.total, self.closed)
            sp, sn = pos.window_max(sa, SHIFT_SAMPLE_M), neg.window_max(sa, SHIFT_SAMPLE_M)
            sh = np.rint(np.where(sp >= sn, sp, -sn) * 100.0).astype(np.int64)
        else:
            sh = np.zeros(0, dtype=np.int64)
        return dm, sh


@dataclass
class Corridors:
    ways: dict[int, WayCorridor] = field(default_factory=dict)
    stats: dict = field(default_factory=dict)

    def galli(self, i: int) -> bool:
        w = self.ways.get(i)
        return bool(w is not None and w.galli)

    def width(self, i: int, a0: float, a1: float) -> float | None:
        """The widest corridor of road ``i`` between arcs ``a0`` and ``a1`` (None for a road without one)."""
        w = self.ways.get(i)
        if w is None:
            return None
        prof = wp.Profile(w.arcs, w.c, w.total, w.closed)
        return float(prof.window_max(np.array([0.5 * (a0 + a1)]), max(0.0, 0.5 * (a1 - a0)))[0])


def _rays(pos: np.ndarray, tan: np.ndarray, polys: np.ndarray, tree, ray_m: float) -> tuple[np.ndarray, np.ndarray]:
    """Distances to the first polygon left and right of each station (inf when none within ``ray_m``), and whether
    the station lies inside a polygon."""
    n = len(pos)
    hit = np.full((2, n), np.inf)
    inside = np.zeros(n, dtype=bool)
    if n == 0 or tree is None:
        return hit, inside
    normal = np.stack([-tan[:, 1], tan[:, 0]], axis=1)
    origins = shapely.points(pos)
    for side, sign in ((0, 1.0), (1, -1.0)):
        ends = pos + sign * ray_m * normal
        rays = shapely.linestrings(np.stack([pos, ends], axis=1))
        pairs = tree.query(rays, predicate="intersects")
        if pairs.shape[1]:
            inter = shapely.intersection(rays[pairs[0]], polys[pairs[1]])
            d = shapely.distance(origins[pairs[0]], inter)
            d = np.where(np.isfinite(d), d, np.inf)
            np.minimum.at(hit[side], pairs[0], d)
    pin = tree.query(origins, predicate="within")
    if pin.shape[1]:
        inside[np.unique(pin[0])] = True
    return hit, inside


def _near(pos: np.ndarray, tan: np.ndarray, polys: np.ndarray, tree) -> np.ndarray:
    """Distance from each station's centreline stretch (+-STATION_M/2 along the tangent) to the nearest polygon
    (inf beyond NEAR_M)."""
    n = len(pos)
    out = np.full(n, np.inf)
    if n == 0 or tree is None:
        return out
    h = 0.5 * STATION_M
    segs = shapely.linestrings(np.stack([pos - h * tan, pos, pos + h * tan], axis=1))
    pairs = tree.query(segs, predicate="dwithin", distance=NEAR_M)
    if pairs.shape[1]:
        d = shapely.distance(segs[pairs[0]], polys[pairs[1]])
        np.minimum.at(out, pairs[0], d)
    return out


def _point_segment_distance(p: np.ndarray, a: np.ndarray, b: np.ndarray) -> np.ndarray:
    ab = b - a
    L2 = np.sum(ab * ab, axis=1)
    t = np.clip(np.sum((p - a) * ab, axis=1) / np.maximum(L2, 1e-18), 0.0, 1.0)
    return np.hypot(*(a + ab * t[:, None] - p).T)


def _side_near(pos: np.ndarray, tan: np.ndarray, back: np.ndarray, fwd: np.ndarray, polys: np.ndarray,
               tree) -> tuple[np.ndarray, np.ndarray]:
    """Nearest distance from each station's centreline stretch (from ``back`` to ``fwd``, the way's own points
    STATION_M / 2 either side, clipped at its ends) to a polygon on its left and on its right (by the side of the
    closest point; inf beyond NEAR_M), and whether the stretch touches a polygon. Polygons that lie only ahead of
    or behind the stretch (beyond a way's end) are not beside it and do not count."""
    n = len(pos)
    hit = np.full((2, n), np.inf)
    touch = np.zeros(n, dtype=bool)
    if n == 0 or tree is None:
        return hit, touch
    segs = shapely.linestrings(np.stack([back, pos, fwd], axis=1))
    pairs = tree.query(segs, predicate="dwithin", distance=NEAR_M)
    if not pairs.shape[1]:
        return hit, touch
    si, pi = pairs[0], pairs[1]
    d = shapely.distance(segs[si], polys[pi])
    sl = shapely.shortest_line(segs[si], polys[pi])
    c = shapely.get_coordinates(sl).reshape(-1, 2, 2)
    # The side is taken at the closest point of the stretch, with the tangent of the segment it lies on.
    on = c[:, 0]
    q = c[:, 1] - on
    zero = d <= 1e-9
    if zero.any():  # the stretch touches the footprint: its side is the side of the footprint's centroid
        cen = shapely.get_coordinates(shapely.centroid(polys[pi[zero]]))
        q[zero] = cen - on[zero]
    t0 = pos[si] - back[si]
    t1 = fwd[si] - pos[si]
    d0 = _point_segment_distance(on, back[si], pos[si])
    d1 = _point_segment_distance(on, pos[si], fwd[si])
    t = np.where((d0 <= d1)[:, None] & (np.hypot(*t0.T) > 1e-9)[:, None], t0, t1)
    t = np.where((np.hypot(*t.T) > 1e-9)[:, None], t, tan[si])
    t = t / np.maximum(np.hypot(*t.T), 1e-12)[:, None]
    # Not beside the stretch: the closest point is an end of a stretch that ends there (a way's end).
    at_end = ((np.hypot(*(on - back[si]).T) < 1e-6) & (np.hypot(*t0.T) < 1e-9)) | \
             ((np.hypot(*(on - fwd[si]).T) < 1e-6) & (np.hypot(*t1.T) < 1e-9))
    ahead = np.abs(t[:, 0] * q[:, 0] + t[:, 1] * q[:, 1]) > np.abs(t[:, 0] * q[:, 1] - t[:, 1] * q[:, 0])
    beside = zero | ~(at_end & ahead)
    si, d, q, t = si[beside], d[beside], q[beside], t[beside]
    cross = t[:, 0] * q[:, 1] - t[:, 1] * q[:, 0]
    left = cross >= 0
    np.minimum.at(hit[0], si[left], d[left])
    np.minimum.at(hit[1], si[~left], d[~left])
    touch[si[d <= 1e-9]] = True
    return hit, touch


def _fill_inside(vals: np.ndarray, inside: np.ndarray) -> np.ndarray:
    """Stations inside a footprint repeat the previous valid station (else the next; else open)."""
    if not inside.any():
        return vals
    out = vals.copy()
    good = np.flatnonzero(~inside)
    if not len(good):
        return np.full_like(vals, np.inf)
    for i in np.flatnonzero(inside):
        prev = good[good < i]
        out[i] = vals[prev[-1]] if len(prev) else vals[good[good > i][0]]
    return out


def _envelope(arcs: np.ndarray, v: np.ndarray, slope: float) -> np.ndarray:
    """Lower envelope with a maximum slope: v[i] = min_j (v[j] + slope * |a_i - a_j|)."""
    out = v.astype(np.float64).copy()
    for i in range(1, len(out)):
        out[i] = min(out[i], out[i - 1] + slope * (arcs[i] - arcs[i - 1]))
    for i in range(len(out) - 2, -1, -1):
        out[i] = min(out[i], out[i + 1] + slope * (arcs[i + 1] - arcs[i]))
    return out


def _upper_envelope(arcs: np.ndarray, v: np.ndarray, slope: float) -> np.ndarray:
    return -_envelope(arcs, -v, slope)


def _fit_between(arcs: np.ndarray, C: np.ndarray, pl: np.ndarray, pr: np.ndarray) -> tuple[np.ndarray, np.ndarray,
                                                                                            bool]:
    """Corridor widths and lateral shifts (+ = left) that keep the band SHIFT_MARGIN_M clear of the protected
    outlines at ``pl`` (left) and ``pr`` (right) of every station, with the shift changing by at most 1 : 20 and
    as close to 0 as that allows; the corridor narrows only where both sides leave no room for it."""
    m = SHIFT_MARGIN_M
    C = C.astype(np.float64).copy()
    both = np.isfinite(pl) & np.isfinite(pr)
    room = np.where(both, np.maximum(pl + pr - 2.0 * m, 0.1), np.inf)
    squeezed = bool((C > room + 1e-9).any())
    for _ in range(8):
        C = np.minimum(C, room)
        C = np.minimum(C, _envelope(arcs, C, 1.0 / TAPER))
        hi = np.where(np.isfinite(pl), pl - m - 0.5 * C, np.inf)
        lo = np.where(np.isfinite(pr), -(pr - m - 0.5 * C), -np.inf)
        L = _upper_envelope(arcs, lo, 1.0 / TAPER)
        U = _envelope(arcs, hi, 1.0 / TAPER)
        conflict = L > U + 1e-6
        if not conflict.any():
            break
        # Tapers from both sides meet: narrow the corridor there until the band fits.
        room = np.where(conflict, np.minimum(room, C - (L - U)), room)
        room = np.maximum(room, 0.1)
        squeezed = True
    shift = np.minimum(np.maximum(0.0, L), U)
    clash = (L > U) & np.isfinite(L) & np.isfinite(U)
    shift[clash] = 0.5 * (L[clash] + U[clash])
    shift = np.where(np.isfinite(shift), shift, 0.0)
    return C, shift, squeezed


def compute(roads: Sequence[RoadFeature], roads_game: Sequence[np.ndarray], area_at: Callable,
            outlines: Sequence[np.ndarray], protected: np.ndarray, dual_partner: dict | None = None,
            heritage_at: Callable | None = None, drawn: np.ndarray | None = None) -> Corridors:
    """Way-level corridors (module docstring). ``outlines``: every building's outer ring (game metres, parts
    excluded from the rays by passing an empty ring) and ``protected`` the matching mask. ``area_at(x, z)`` returns
    AreaType arrays; ``heritage_at(x, z)`` booleans (inside SACRED_NO_VEHICLE: no footpaths)."""
    res = Corridors()
    st: dict = defaultdict(int)
    dual_partner = dual_partner or {}
    polys = np.empty(len(outlines), dtype=object)
    polys[:] = [shapely.polygons(np.asarray(r, dtype=np.float64)) if len(r) >= 3 else shapely.Polygon()
                for r in outlines]
    ok = shapely.is_valid(polys) | shapely.is_empty(polys)
    if not ok.all():
        polys[~ok] = shapely.make_valid(polys[~ok])
    nonempty = ~shapely.is_empty(polys)
    all_idx = np.flatnonzero(nonempty)
    prot_idx = np.flatnonzero(nonempty & np.asarray(protected, dtype=bool))
    tree_all = shapely.STRtree(polys[all_idx]) if len(all_idx) else None
    tree_prot = shapely.STRtree(polys[prot_idx]) if len(prot_idx) else None
    # Stations of every way, in one batch.
    w_of, arcs_l, pos_l, tan_l, back_l, fwd_l = [], [], [], [], [], []
    n_st = 0  # stations so far: the batch offset of the next way
    meta = {}
    for i, r in enumerate(roads):
        pts = roads_game[i]
        if len(pts) < 2 or int(r.cls) == int(RoadClass.UNKNOWN) or (drawn is not None and not drawn[i]):
            continue
        cum = wp.cumulative(pts)
        total = float(cum[-1])
        if total <= 0:
            continue
        k = max(2, int(math.ceil(total / STATION_M)) + 1)
        arcs = np.linspace(0.0, total, k)
        pos, tan = wp.at_arcs(pts, cum, arcs)
        bk, _ = wp.at_arcs(pts, cum, np.clip(arcs - 0.5 * STATION_M, 0.0, total))
        fw, _ = wp.at_arcs(pts, cum, np.clip(arcs + 0.5 * STATION_M, 0.0, total))
        meta[i] = (n_st, k, total, wp.is_closed(pts))
        n_st += k
        w_of.append(np.full(k, i))
        arcs_l.append(arcs)
        pos_l.append(pos)
        tan_l.append(tan)
        back_l.append(bk)
        fwd_l.append(fw)
    if not meta:
        res.stats = dict(st)
        return res
    P = np.concatenate(pos_l)
    Tn = np.concatenate(tan_l)
    hit, inside = _rays(P, Tn, polys[all_idx], tree_all, RAY_M)
    near = _near(P, Tn, polys[all_idx], tree_all)
    phit, pinside = _side_near(P, Tn, np.concatenate(back_l), np.concatenate(fwd_l), polys[prot_idx], tree_prot)
    areas_all = np.asarray(area_at(P[:, 0], P[:, 1]), dtype=np.int64).reshape(-1)
    herit = np.asarray(heritage_at(P[:, 0], P[:, 1]), dtype=bool).reshape(-1) if heritage_at is not None \
        else np.zeros(len(P), dtype=bool)
    order = sorted(meta)  # stations are contiguous per way in the batch, in road index order
    for idx, i in enumerate(order):
        r = roads[i]
        start, k, total, closed = meta[i]
        sl = slice(start, start + k)
        arcs = arcs_l[idx]
        dl, dr = _fill_inside(hit[0, sl], inside[sl]), _fill_inside(hit[1, sl], inside[sl])
        S_rays = np.where(np.isfinite(dl) & np.isfinite(dr), 2.0 * np.minimum(dl, dr), np.inf)
        S = 2.0 * near[sl]
        cls = int(r.cls)
        area = areas_all[sl]
        tagged = tagged_width(r)
        dual = int(r.osm_id) in dual_partner
        R = np.array([real_width(r, a) for a in area.tolist()])
        N = np.array([min(max(Rk * scale_for(cls), min_game_m(cls, bool(r.oneway), dual, a)), Rk + 6.0)
                      for Rk, a in zip(R.tolist(), area.tolist())])
        E = np.array([2.0 * shoulder_m(cls, a) + sum(footpath_room(r, a, dual, h))
                      for a, h in zip(area.tolist(), herit[sl].tolist())])
        F = max(MIN_CORRIDOR_M, tagged if tagged is not None else floor_m(cls))
        lim = S - CLEARANCE_M
        C = np.maximum(F, np.minimum(N + E, lim))
        C = np.maximum(_envelope(arcs, C, 1.0 / TAPER), F)
        # Protected footprints: the band [shift - C/2, shift + C/2] (lateral, + = left) keeps SHIFT_MARGIN_M from
        # the nearest protected outline on each side; it narrows only where both sides leave no room (SQUEEZED).
        pl, pr = phit[0, sl].copy(), phit[1, sl].copy()
        st["stations_touching_protected"] += int(pinside[sl].sum())
        C, shift, squeezed = _fit_between(arcs, C, pl, pr)
        if squeezed:
            st["ways_squeezed"] += 1
        if np.any(shift != 0):
            st["ways_shifted"] += 1
        # Galli: an untagged car-class way that stays narrow on both sides for GALLI_MIN_M.
        galli = False
        if cls in CAR_CLASSES and cls not in MAJOR and tagged is None:
            narrow = np.isfinite(S_rays) & (S_rays - CLEARANCE_M < CAR_MIN_REAL_M)
            if narrow.any():
                t = 0
                while t < k and not galli:
                    if narrow[t]:
                        u = t
                        while u + 1 < k and narrow[u + 1]:
                            u += 1
                        if arcs[u] - arcs[t] >= GALLI_MIN_M - 1e-6:
                            galli = True
                        t = u + 1
                    else:
                        t += 1
        if galli:
            st["galli_ways"] += 1
        st["stations"] += k
        st["stations_below_min_space"] += int((S - CLEARANCE_M < MIN_CORRIDOR_M).sum())
        res.ways[i] = WayCorridor(arcs=arcs, c=C, shift=shift, space=S, total=total, closed=closed,
                                  squeezed=squeezed, galli=galli)
    res.stats = dict(st)
    return res


# ---------------------------------------------------------------------------------------------------------------
# Protected footprints: roads never run inside them
# ---------------------------------------------------------------------------------------------------------------
CLIP_MARGIN_M = 0.3  # a road clipped at a protected footprint stops this far outside its outline
CLIP_MIN_PIECE_M = 1.0
CLIP_STEP_M = 0.5
PROTECTED_INTRUSION_MAX_M2 = 1.0  # the build fails when a corridor still enters a protected footprint by more
PROTECTED_INTRUSIONS_PATH = "curated/protected_intrusions.yaml"  # curated exceptions (config/), by building ref


@dataclass
class ProtectedClip:
    """Roads with the stretches inside protected footprints removed (``clip_at_protected``)."""

    roads: list
    game: list
    ids: list
    src: list  # output road index -> input road index (a road a footprint cuts in two continues as an extra road)
    clipped: dict = field(default_factory=dict)  # input road index -> metres removed
    stats: dict = field(default_factory=dict)


def clip_at_protected(roads: Sequence[RoadFeature], roads_game: Sequence[np.ndarray], ids: Sequence[np.ndarray],
                      rings: Sequence[Sequence[np.ndarray]], protected: np.ndarray, half_m: Callable[[int], float],
                      skip: set, ends_only: set = frozenset()) -> ProtectedClip:
    """Remove every stretch of a road centreline inside a protected footprint (heroes and temples are never
    trimmed, so no road may run through them): the road stops ``CLIP_MARGIN_M`` outside the outline and is pulled
    back further until its flat corridor end (half width ``half_m(i)``) clears the outline; a road a footprint
    cuts in two becomes two roads with the same OSM way (the extra one appended, ``src``). ``skip``: ways that pass
    under (tunnels) or through a gateway (passages); ``ends_only``: ways that pass over (bridges, layer > 0), which
    only lose the stretches at their ends (a bridge landing at a temple, e.g. Rani Pokhari's)."""
    import copy

    out = ProtectedClip(roads=list(roads), game=list(roads_game), ids=list(ids), src=list(range(len(roads))))
    st: dict = defaultdict(int)
    idx = np.flatnonzero(np.asarray(protected, dtype=bool))
    if not len(idx):
        return out
    polys = np.empty(len(idx), dtype=object)
    polys[:] = [shapely.make_valid(shapely.Polygon(np.asarray(rings[k][0], dtype=np.float64)))
                if len(rings[k]) and len(rings[k][0]) >= 3 else shapely.Polygon() for k in idx]
    tree = shapely.STRtree(polys)
    for i, r in enumerate(roads):
        p = np.asarray(roads_game[i], dtype=np.float64)
        if i in skip or len(p) < 2 or int(r.cls) == int(RoadClass.UNKNOWN):
            continue
        line = shapely.LineString(p)
        hits = tree.query(line, predicate="intersects")
        if not len(hits):
            continue
        U = shapely.union_all(polys[hits])
        rest = shapely.difference(line, shapely.buffer(U, CLIP_MARGIN_M, quad_segs=4))
        parts = [g for g in _line_parts(rest) if g.length >= CLIP_MIN_PIECE_M]
        parts.sort(key=lambda g: float(line.project(shapely.Point(g.coords[0]))))
        if i in ends_only and len(parts) > 1:
            # A deck passes over: keep it whole between its first and last stretch outside the footprints.
            a0 = min(float(line.project(shapely.Point(g.coords[0]))) for g in parts)
            a1 = max(float(line.project(shapely.Point(g.coords[-1]))) for g in parts)
            parts = [shapely.LineString(_sub_polyline(p, wp.cumulative(p), a0, a1))]
        h = max(0.5 * MIN_CORRIDOR_M, float(half_m(i)))
        kept = []
        for g in parts:
            c = np.asarray(g.coords, dtype=np.float64)
            if len(c) >= 2 and line.project(shapely.Point(c[0])) > line.project(shapely.Point(c[-1])):
                c = c[::-1]
            cut0 = float(np.hypot(*(c[0] - p[0]))) > 1e-6
            cut1 = float(np.hypot(*(c[-1] - p[-1]))) > 1e-6
            c = _pull_back(c, U, h, cut0, cut1)
            if c is not None:
                kept.append(c)
        removed = float(line.length - sum(float(wp.cumulative(c)[-1]) for c in kept))
        out.clipped[i] = round(removed, 2)
        st["roads_clipped"] += 1
        st["metres_removed"] += removed
        vid = {(float(x), float(z)): int(n) for (x, z), n in zip(p.tolist(), np.asarray(ids[i]).tolist())}
        def ids_of(c: np.ndarray) -> np.ndarray:
            return np.array([vid.get((float(x), float(z)), 0) for x, z in c.tolist()], dtype=np.int64)
        if not kept:
            out.game[i] = np.zeros((0, 2))
            out.ids[i] = np.zeros(0, dtype=np.int64)
            st["roads_removed"] += 1
            continue
        out.game[i] = kept[0]
        out.ids[i] = ids_of(kept[0])
        if len(kept) > 1:
            st["roads_split"] += 1
            if int(r.cls) not in FOOT and int(r.cls) != int(RoadClass.PEDESTRIAN):
                st["motor_roads_split"] += 1
        for c in kept[1:]:
            out.roads.append(copy.copy(r))
            out.game.append(c)
            out.ids.append(ids_of(c))
            out.src.append(i)
    st["metres_removed"] = round(float(st.get("metres_removed", 0.0)), 1)
    out.stats = dict(st)
    return out


def _pull_back(c: np.ndarray, U, h: float, cut0: bool, cut1: bool) -> np.ndarray | None:
    """Shorten a clipped piece at its cut ends until its flat corridor end (half width ``h``) clears ``U``."""
    for _ in range(int(4 * h / CLIP_STEP_M) + 2):
        if len(c) < 2:
            return None
        cum = wp.cumulative(c)
        if cum[-1] < CLIP_MIN_PIECE_M:
            return None
        bad0 = cut0 and _cap_hits(c, cum, U, h, start=True)
        bad1 = cut1 and _cap_hits(c, cum, U, h, start=False)
        if not (bad0 or bad1):
            return c
        a0 = CLIP_STEP_M if bad0 else 0.0
        a1 = cum[-1] - (CLIP_STEP_M if bad1 else 0.0)
        if a1 - a0 < CLIP_MIN_PIECE_M:
            return None
        c = _sub_polyline(c, cum, a0, a1)
    return None


def _cap_hits(c: np.ndarray, cum: np.ndarray, U, h: float, start: bool) -> bool:
    L = min(float(cum[-1]), 2.0 * h)
    seg = _sub_polyline(c, cum, 0.0, L) if start else _sub_polyline(c, cum, float(cum[-1]) - L, float(cum[-1]))
    band = shapely.buffer(shapely.LineString(seg), h, cap_style="flat", quad_segs=4)
    return float(shapely.area(shapely.intersection(band, U))) > 0.05


def _sub_polyline(c: np.ndarray, cum: np.ndarray, a0: float, a1: float) -> np.ndarray:
    inner = (cum > a0 + 1e-9) & (cum < a1 - 1e-9)
    p0, _ = wp.at_arcs(c, cum, [a0])
    p1, _ = wp.at_arcs(c, cum, [a1])
    return np.concatenate([p0, c[inner], p1])


def _line_parts(g) -> list:
    if g is None or g.is_empty:
        return []
    if g.geom_type == "LineString":
        return [g]
    if hasattr(g, "geoms"):
        out = []
        for q in g.geoms:
            out += _line_parts(q)
        return out
    return []


def load_protected_intrusions_ok() -> frozenset:
    """Curated building refs (``w123``) whose protected footprint a corridor may still enter (config/curated)."""
    from .config import CONFIG_DIR

    path = CONFIG_DIR / PROTECTED_INTRUSIONS_PATH
    if not path.exists():
        return frozenset()
    import yaml

    data = yaml.safe_load(path.read_text(encoding="utf-8")) or {}
    return frozenset(str(e["ref"]) for e in (data.get("allowed") or []) if e and e.get("ref"))


# ---------------------------------------------------------------------------------------------------------------
# Trimming
# ---------------------------------------------------------------------------------------------------------------
@dataclass
class TrimResult:
    rings: dict[int, list[np.ndarray]] = field(default_factory=dict)  # building index -> new rings (game metres)
    parts: dict[int, list[list[np.ndarray]]] = field(default_factory=dict)  # building index -> further pieces' rings
    trimmed: set = field(default_factory=set)
    removed: set = field(default_factory=set)
    protected_hits: dict[int, list[int]] = field(default_factory=dict)  # protected building -> road indices
    stats: dict = field(default_factory=dict)


def _point_normals(pos: np.ndarray, closed: bool) -> np.ndarray:
    """Left normals at the points of a polyline: the segment normal at open ends, else the mitred bisector of the
    two segment normals (scaled by 1 / cos(half the turn), at most 2), so an offset polyline keeps its distance."""
    d = np.diff(pos, axis=0)
    L = np.hypot(d[:, 0], d[:, 1])
    nrm = np.zeros_like(d)
    good = L > 1e-9
    nrm[good] = np.stack([-d[good, 1], d[good, 0]], axis=1) / L[good, None]
    for k in range(1, len(nrm)):  # zero-length segments carry the previous normal
        if not good[k]:
            nrm[k] = nrm[k - 1]
    for k in range(len(nrm) - 2, -1, -1):
        if not good[k] and good[k + 1]:
            nrm[k] = nrm[k + 1]
    out = np.zeros_like(pos)
    if not len(nrm):
        return out
    out[0], out[-1] = nrm[0], nrm[-1]
    if closed:
        out[0] = out[-1] = nrm[-1] + nrm[0]
    out[1:-1] = nrm[:-1] + nrm[1:]
    n = np.hypot(out[:, 0], out[:, 1])
    ok = n > 1e-9
    out[ok] /= n[ok, None]
    ref = np.concatenate([nrm[:1], nrm[1:], nrm[-1:]]) if len(nrm) > 1 else np.repeat(nrm, 2, axis=0)
    cosh = np.clip(np.sum(out * ref, axis=1), 0.5, 1.0)
    return out / cosh[:, None]


def corridor_bands(cor: Corridors, roads_game: Sequence[np.ndarray], skip: set,
                   exact: bool = False) -> tuple[np.ndarray, np.ndarray]:
    """Band polygons (one per sub-segment between stations and vertices) and their road index. ``exact`` drops
    the trimming margins (``TRIM_EPS_M`` and the +0.5 m next to shifts): the corridor itself, for reports."""
    segs_a, segs_b, rad, owner, joints, jrad, jown = [], [], [], [], [], [], []
    for i, w in sorted(cor.ways.items()):
        if i in skip:
            continue
        pts = np.asarray(roads_game[i], dtype=np.float64)
        cum = wp.cumulative(pts)
        arcs = np.unique(np.concatenate([cum, w.arcs]))
        arcs = arcs[np.concatenate([[True], np.diff(arcs) > 1e-6])]  # no zero-length pieces (no joint at an end)
        pos, _ = wp.at_arcs(pts, cum, arcs)
        pc = wp.Profile(w.arcs, w.c, w.total, w.closed)
        cc = pc.at(arcs)
        if np.any(w.shift != 0):
            sh = wp.Profile(w.arcs, w.shift, w.total, w.closed).at(arcs)
        else:
            sh = np.zeros(len(arcs))
        off = pos + _point_normals(pos, w.closed) * sh[:, None] if np.any(sh != 0) else pos
        a2, b2 = off[:-1], off[1:]
        r = 0.5 * np.maximum(cc[:-1], cc[1:])
        if not exact:
            r = r + TRIM_EPS_M + np.where((sh[:-1] != 0) | (sh[1:] != 0), 0.5, 0.0)
        segs_a.append(a2)
        segs_b.append(b2)
        rad.append(r)
        owner.append(np.full(len(r), i))
        # Round joints at every inner point (and all points of a closed way): discs of the larger radius.
        if len(r) > 1:
            joints.append(off[1:-1])
            jrad.append(np.maximum(r[:-1], r[1:]))
            jown.append(np.full(len(r) - 1, i))
        if w.closed and len(r) > 1:
            joints.append(off[:1])
            jrad.append(np.array([max(r[0], r[-1])]))
            jown.append(np.array([i]))
    if not segs_a:
        return np.zeros(0, dtype=object), np.zeros(0, dtype=np.int64)
    A = np.concatenate(segs_a)
    B = np.concatenate(segs_b)
    Rr = np.concatenate(rad)
    lines = shapely.linestrings(np.stack([A, B], axis=1))
    bands = shapely.buffer(lines, Rr, quad_segs=4, cap_style="flat")
    own = np.concatenate(owner)
    if joints:
        J = np.concatenate(joints)
        discs = shapely.buffer(shapely.points(J), np.concatenate(jrad), quad_segs=4)
        bands = np.concatenate([bands, discs])
        own = np.concatenate([own, np.concatenate(jown)])
    return bands, own


def protected_intrusions(rings: Sequence[Sequence[np.ndarray]], protected: np.ndarray, bands: np.ndarray,
                         owners: np.ndarray, min_m2: float = 0.05) -> dict[int, tuple[float, list[int]]]:
    """Protected footprints the exact corridor still enters by at least ``min_m2``: building index ->
    (intruding area m2, road indices)."""
    out: dict[int, tuple[float, list[int]]] = {}
    idx = np.flatnonzero(np.asarray(protected, dtype=bool))
    if not len(idx) or not len(bands):
        return out
    polys = np.empty(len(idx), dtype=object)
    polys[:] = [shapely.make_valid(shapely.Polygon(np.asarray(rings[k][0]))) if len(rings[k][0]) >= 3
                else shapely.Polygon() for k in idx]
    tree = shapely.STRtree(bands)
    pairs = tree.query(polys, predicate="intersects")
    by: dict[int, list[int]] = defaultdict(list)
    for a, b in zip(pairs[0].tolist(), pairs[1].tolist()):
        by[a].append(b)
    for a in sorted(by):
        area = float(shapely.area(shapely.intersection(polys[a], shapely.union_all(bands[by[a]]))))
        if area >= min_m2:
            out[int(idx[a])] = (round(area, 2), sorted({int(owners[s]) for s in by[a]}))
    return out


def trim_buildings(rings: Sequence[Sequence[np.ndarray]], protected: np.ndarray, bands: np.ndarray,
                   owners: np.ndarray) -> TrimResult:
    """Clip every unprotected building (outer ring + holes, game metres) out of the corridor bands."""
    res = TrimResult()
    st: dict = defaultdict(int)
    n = len(rings)
    if not len(bands) or not n:
        res.stats = dict(st)
        return res
    polys = np.empty(n, dtype=object)
    polys[:] = [shapely.Polygon(np.asarray(rg[0]), [np.asarray(h) for h in rg[1:] if len(h) >= 3])
                if len(rg) and len(rg[0]) >= 3 else shapely.Polygon() for rg in rings]
    bad = ~(shapely.is_valid(polys) | shapely.is_empty(polys))
    if bad.any():
        polys[bad] = shapely.make_valid(polys[bad])
    tree = shapely.STRtree(bands)
    pairs = tree.query(polys, predicate="intersects")
    by_b: dict[int, list[int]] = defaultdict(list)
    for bi, si in zip(pairs[0].tolist(), pairs[1].tolist()):
        by_b[bi].append(si)
    for bi in sorted(by_b):
        segs = by_b[bi]
        if protected[bi]:
            res.protected_hits[bi] = sorted({int(owners[s]) for s in segs})
            st["protected_overlaps"] += 1
            continue
        poly = polys[bi]
        cut = shapely.union_all(bands[segs])
        inter_a = float(shapely.area(shapely.intersection(poly, cut)))
        if inter_a < MIN_INTRUSION_M2:
            continue
        diff = shapely.difference(poly, cut)
        # An opening removes the hair-thin spikes and slivers where the band pieces meet.
        diff = shapely.buffer(shapely.buffer(diff, -OPEN_M, join_style="mitre"), OPEN_M, join_style="mitre")
        kept = []
        for g in sorted((g for g in _polys(diff) if g.area > 0), key=lambda g: (-g.area, g.bounds)):
            if g.area < SLIVER_M2:
                st["slivers_dropped"] += 1
                continue
            rings_ = _clean_rings(shapely.simplify(g, SIMPLIFY_M, preserve_topology=True), st)
            if rings_ is None:
                st["slivers_dropped"] += 1
                continue
            kept.append(rings_)
        if not kept:
            res.removed.add(bi)
            st["removed"] += 1
            st["area_removed_m2"] += float(poly.area)
            continue
        res.rings[bi] = kept[0][0]
        if len(kept) > 1:
            res.parts[bi] = [k[0] for k in kept[1:]]
            st["split_into_parts"] += 1
            st["extra_parts"] += len(kept) - 1
        res.trimmed.add(bi)
        st["trimmed"] += 1
        st["area_removed_m2"] += float(poly.area - sum(k[1] for k in kept))
    st["area_removed_m2"] = round(float(st.get("area_removed_m2", 0.0)), 1)
    res.stats = dict(st)
    return res


def _ring_cm(ring: np.ndarray) -> np.ndarray:
    """A ring rounded to the centimetre grid the tiles store, without repeated points, spikes (a vertex whose
    neighbours coincide) or collinear vertices."""
    r = np.round(np.asarray(ring, dtype=np.float64) * 100.0) / 100.0
    for _ in range(len(r)):
        n = len(r)
        if n < 3:
            break
        nxt = np.roll(r, -1, axis=0)
        prv = np.roll(r, 1, axis=0)
        dup = np.all(r == nxt, axis=1)
        spike = np.all(prv == nxt, axis=1)
        cross = (r[:, 0] - prv[:, 0]) * (nxt[:, 1] - prv[:, 1]) - (r[:, 1] - prv[:, 1]) * (nxt[:, 0] - prv[:, 0])
        flat = np.abs(cross) < 1e-12
        bad = dup | spike | flat
        if not bad.any():
            break
        k = int(np.flatnonzero(bad)[0])
        r = np.delete(r, k, axis=0)
    return r


def _clean_rings(g, st) -> tuple[list[np.ndarray], float] | None:
    """Valid, centimetre-rounded rings (outer counter-clockwise, holes clockwise) of the largest polygon of ``g``,
    or None when nothing of at least SLIVER_M2 survives."""
    for attempt in range(3):
        g = _largest(g)
        if g is None or g.area < SLIVER_M2:
            return None
        outer = _ring_cm(np.asarray(g.exterior.coords)[:-1])
        holes = []
        for h in g.interiors:
            if shapely.Polygon(h).area < HOLE_MIN_M2:
                st["holes_filled"] += 1
                continue
            hr = _ring_cm(np.asarray(h.coords)[:-1])
            if len(hr) >= 3:
                holes.append(hr)
        if len(outer) < 3:
            return None
        cand = shapely.Polygon(outer, holes)
        if cand.is_valid and cand.area >= SLIVER_M2:
            return [_orient(outer, True)] + [_orient(h, False) for h in holes], float(cand.area)
        st["rings_repaired"] += 1
        g = shapely.make_valid(cand)
        g = shapely.buffer(shapely.buffer(g, -OPEN_M, join_style="mitre"), OPEN_M, join_style="mitre")
    return None


def _polys(g) -> list:
    if g is None or g.is_empty:
        return []
    if g.geom_type == "Polygon":
        return [g]
    if hasattr(g, "geoms"):
        out = []
        for p in g.geoms:
            out += _polys(p)
        return out
    return []


def _largest(g):
    ps = _polys(g)
    if not ps:
        return None
    ps.sort(key=lambda p: (-p.area, p.bounds))
    return ps[0]


def _orient(ring: np.ndarray, ccw: bool) -> np.ndarray:
    r = np.asarray(ring, dtype=np.float64)
    x, z = r[:, 0], r[:, 1]
    a2 = float(np.sum(x * np.roll(z, -1) - np.roll(x, -1) * z))
    if (a2 > 0) != ccw:
        r = r[::-1]
    return r.copy()
