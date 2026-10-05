"""Routing graph (``.ghrg``): travel profiles, graph building, binary I/O and the reference router.

The binary layout is docs/DATA_FORMATS.md section 4. This module is the
reference implementation: ``Ghumante.Core`` ``Routing/TravelProfiles.cs``
mirrors the tables below, and the C# A* must return the same costs.
``travel_profiles_table()`` dumps the tables as JSON for a golden test.

Travel profiles
===============

Every single-bit ``model.Travel`` value is a profile. For profile ``p`` an edge
costs::

    speed  = SPEED_KMH[p][road_class] * SURFACE_FACTOR[p][SURFACE_GROUP[surface]]
    speed  = min(speed, ALPINE_SPEED_KMH[p])          when sac_scale >= T4 (any class)
    climb  = 1 + max(0, climb_m) / length_m * CLIMB_K[p]   (1 when length_m == 0)
    time_s = length_m * 3.6 / speed * climb

and is **unusable** (time ``inf``) when the profile's bit is missing from the
edge's access mask, when ``speed`` is 0, or when the edge is a trail
(``model.TRAIL_CLASSES``) whose ``sac_scale`` exceeds ``MAX_SAC[p]``. The
``sac_scale`` limit only applies to trails: ``trails.py`` also grades tracks
(T1, or T2..T5 when they are high or steep), and a jeep track at 4 600 m in
Mustang must stay drivable. Unknown class or surface values (newer enums)
get speed 0 and the DIRT group. Arithmetic is IEEE double in exactly the order
written above, so Python and C# agree to the last bit.

Base speed, km/h (0 = the profile physically cannot use the class)::

    class          FOOT  BICYCLE  MOTORBIKE  CAR  JEEP  BUS  HORSE
    UNKNOWN          0      0         0       0     0    0     0
    MOTORWAY         5     20        80      90    85   70     7
    TRUNK            5     20        80      80    75   60     7
    PRIMARY          5     18        70      70    65   50     7
    SECONDARY        5     18        60      60    55   45     7
    TERTIARY         5     18        50      50    50   40     7
    UNCLASSIFIED     5     16        40      40    40   30     7
    RESIDENTIAL      5     15        30      30    30   25     7
    LIVING_STREET    5     10        15      15    15   10     6
    SERVICE          5     14        20      20    20   15     7
    TRACK            5     12        25      20    30   15     7
    ROAD             5     16        35      35    35   30     7
    PEDESTRIAN       5      8        10       0     0    0     5
    FOOTWAY          5     10        10       0     0    0     6
    PATH             5     10        12       0     0    0     7
    STEPS            4      3         0       0     0    0     0
    CYCLEWAY         5     20        15       0     0    0     6
    BRIDLEWAY        5      8        10       0     0    0     8

Legality is the access mask's job (``tags.parse_access``: a motorbike is on a
path only with ``motorcycle=yes``); the table only says how fast a profile
goes once it is allowed there.

Surface factor by ``SurfaceGroup``::

    group   FOOT  BICYCLE  MOTORBIKE  CAR   JEEP  BUS   HORSE
    PAVED   1.0    1.0       1.0      1.0   1.0   1.0   1.0
    GRAVEL  1.0    0.75      0.75     0.75  0.9   0.75  1.0
    DIRT    1.0    0.6       0.6      0.6   0.85  0.6   1.0
    MUD     0.8    0.35      0.35     0.35  0.6   0.35  0.8

Per-profile constants::

    profile    MAX_SAC (trails)  CLIMB_K  ALPINE_SPEED_KMH (sac >= T4)
    FOOT         6 (T6)            6        3.5
    BICYCLE      2 (T2)            8        -
    MOTORBIKE    1 (T1)            1        -
    CAR          0                 1        -
    JEEP         0                 1        -
    BUS          0                 1        -
    HORSE        3 (T3)            4        -

``CLIMB_K`` 6 for walkers is Naismith-like: a 10 % climb costs 60 % more time.
Descents are free. The A* heuristic divides the straight-line distance by
``max_speed_kmh`` = max(SPEED_KMH[p]) * max(SURFACE_FACTOR[p]), which bounds
every edge's speed, so it is admissible (and consistent, see "Lengths" below).

Graph building (``build_graph``)
================================

* Roads with ``cls == UNKNOWN`` are skipped. The *usable mask* of a road is
  ``road.access`` restricted to the profiles that can use its class and
  ``sac_scale`` (table above); roads with an empty usable mask are skipped.
  The rest are the *routable ways*. ``oneway == -1`` ways are reversed first;
  consecutive duplicate node ids are dropped.
* **Graph nodes** are the OSM nodes that are an endpoint of a routable way, or
  that occur at least twice among all routable ways (shared by two ways, or
  visited twice by one way). Ways are split at graph nodes into pieces.
  A piece that would start and end at the same node (a closed way touching
  the network once, the loop of a lollipop) is split again at its middle
  vertex, so the ring is a real cycle and its far side is reachable. A piece
  longer than 65 535 points (``geom_count`` is a u16) is split every 65 534
  segments.
* Node ids ``<= 0`` mark synthetic vertices (for example points made by
  clipping a way to the region box). They are never shared with anything,
  and they only become graph nodes as way endpoints.
* **Node numbering** sorts by OSM node id (synthetic nodes last, in way
  order). ``RoutingGraph.node_osm_id`` keeps the ids in memory for debugging
  (0 for synthetic nodes); they are not serialised.
* Each piece yields a forward edge with the road's usable mask and a reverse
  edge with the same mask, or only ``FOOT`` (when present) for oneway roads.
  Edges with an empty mask are omitted. The stored access mask is therefore
  the *effective* mask: the runtime may trust it, and ``edge_time_s``
  re-checks the tables anyway. (``oneway:bicycle=no`` is not modelled because
  ``RoadFeature`` does not carry it.)
* Edges are sorted by (source, target, first vertex of the piece in input
  order, direction); roads are first sorted by OSM way id, so the file does
  not depend on the input order.
* **Elevation**: ``elev(x, z)`` is sampled once at every node and rounded to
  whole metres. Non-finite samples are stored as ``ELEV_UNKNOWN`` (-32768,
  i16 min), and edges touching such a node get ``climb_m = 0``. Otherwise
  ``climb_m = elev[target] - elev[source]`` of the stored node elevations, so a
  reverse edge has exactly the negated climb.
* **Names** are deduplicated on NFC ``(default, en, ne)`` and sorted, so the
  table does not depend on the order of the roads. If there are more than
  65 535, the ones past the u16 limit are dropped (the edge becomes unnamed)
  and counted in ``build_info["names_dropped"]``.
* **Flags**: bit0 bridge, bit1 tunnel, bit2 ford, bit3 link.

Lengths and geometry
--------------------

Positions are quantised to decimetres with round-half-even. Node coordinates
and the end points of every edge geometry are identical integers, so the
first geometry delta is always ``(0, 0)`` and the decoded last point equals the
target node exactly. ``length_dm = max(round(L * 10), ceil(chord_dm))`` where
``L`` is the polyline length in game metres before quantisation and
``chord_dm`` the straight-line distance between the quantised end nodes. The
second term (rarely more than 1 dm) guarantees that no edge is shorter than
its chord, which is what makes the straight-line A* heuristic admissible and
consistent on the quantised data the runtime sees.

Geometry blob: per directed edge (in edge order), ``geom_count`` x
``{svarint dx_dm, svarint dz_dm}``; the first point is relative to the source
node, every later point relative to the previous one. A reverse edge stores
the reversed polyline.
"""

from __future__ import annotations

import hashlib
import heapq
import math
import os
import struct
import unicodedata
from dataclasses import dataclass, field
from pathlib import Path
from typing import Callable, Iterable, Sequence

import numpy as np
from scipy.sparse import csr_matrix
from scipy.sparse.csgraph import breadth_first_order, connected_components
from scipy.spatial import cKDTree

from .binio import Reader, Writer
from .model import SURFACE_GROUP, TRAIL_CLASSES, NameRec, RoadClass, RoadFeature, SacScale, SurfaceGroup, Travel
from .projection import lonlat_to_game

MAGIC = b"GHRG"
VERSION = 1
HEADER_SIZE = 32
NODE_SIZE = 12
EDGE_SIZE = 24

ELEV_UNKNOWN = -32768
MAX_GEOM_POINTS = 0xFFFF
MAX_NAMES = 0xFFFF

EDGE_BRIDGE = 1 << 0
EDGE_TUNNEL = 1 << 1
EDGE_FORD = 1 << 2
EDGE_LINK = 1 << 3

ALPINE_SAC = int(SacScale.ALPINE_HIKING)

_HEADER = struct.Struct("<4sHHIIIIQ")
NODE_DTYPE = np.dtype([("x_dm", "<i4"), ("z_dm", "<i4"), ("elev_m", "<i2"), ("reserved", "<u2")])
EDGE_DTYPE = np.dtype([
    ("target", "<u4"), ("length_dm", "<u4"), ("road_class", "u1"), ("surface", "u1"), ("access", "u1"),
    ("flags", "u1"), ("sac_scale", "u1"), ("reserved", "u1"), ("climb_m", "<i2"), ("geom_offset", "<u4"),
    ("name_index", "<u2"), ("geom_count", "<u2"),
])
assert _HEADER.size == HEADER_SIZE and NODE_DTYPE.itemsize == NODE_SIZE and EDGE_DTYPE.itemsize == EDGE_SIZE

# ---------------------------------------------------------------------------
# Travel profiles (tables documented in the module docstring)
# ---------------------------------------------------------------------------
PROFILES: tuple[Travel, ...] = (Travel.FOOT, Travel.BICYCLE, Travel.MOTORBIKE, Travel.CAR, Travel.JEEP,
                                Travel.BUS, Travel.HORSE)

_INF = math.inf
_SPEED_ROWS: dict[RoadClass, tuple[float, ...]] = {
    #                         FOOT  BIKE  MBIKE  CAR  JEEP   BUS  HORSE
    RoadClass.UNKNOWN:       (0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0),
    RoadClass.MOTORWAY:      (5.0, 20.0, 80.0, 90.0, 85.0, 70.0, 7.0),
    RoadClass.TRUNK:         (5.0, 20.0, 80.0, 80.0, 75.0, 60.0, 7.0),
    RoadClass.PRIMARY:       (5.0, 18.0, 70.0, 70.0, 65.0, 50.0, 7.0),
    RoadClass.SECONDARY:     (5.0, 18.0, 60.0, 60.0, 55.0, 45.0, 7.0),
    RoadClass.TERTIARY:      (5.0, 18.0, 50.0, 50.0, 50.0, 40.0, 7.0),
    RoadClass.UNCLASSIFIED:  (5.0, 16.0, 40.0, 40.0, 40.0, 30.0, 7.0),
    RoadClass.RESIDENTIAL:   (5.0, 15.0, 30.0, 30.0, 30.0, 25.0, 7.0),
    RoadClass.LIVING_STREET: (5.0, 10.0, 15.0, 15.0, 15.0, 10.0, 6.0),
    RoadClass.SERVICE:       (5.0, 14.0, 20.0, 20.0, 20.0, 15.0, 7.0),
    RoadClass.TRACK:         (5.0, 12.0, 25.0, 20.0, 30.0, 15.0, 7.0),
    RoadClass.ROAD:          (5.0, 16.0, 35.0, 35.0, 35.0, 30.0, 7.0),
    RoadClass.PEDESTRIAN:    (5.0, 8.0, 10.0, 0.0, 0.0, 0.0, 5.0),
    RoadClass.FOOTWAY:       (5.0, 10.0, 10.0, 0.0, 0.0, 0.0, 6.0),
    RoadClass.PATH:          (5.0, 10.0, 12.0, 0.0, 0.0, 0.0, 7.0),
    RoadClass.STEPS:         (4.0, 3.0, 0.0, 0.0, 0.0, 0.0, 0.0),
    RoadClass.CYCLEWAY:      (5.0, 20.0, 15.0, 0.0, 0.0, 0.0, 6.0),
    RoadClass.BRIDLEWAY:     (5.0, 8.0, 10.0, 0.0, 0.0, 0.0, 8.0),
}
_SURFACE_ROWS: dict[SurfaceGroup, tuple[float, ...]] = {
    #                     FOOT  BIKE  MBIKE  CAR  JEEP   BUS  HORSE
    SurfaceGroup.PAVED:  (1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0),
    SurfaceGroup.GRAVEL: (1.0, 0.75, 0.75, 0.75, 0.9, 0.75, 1.0),
    SurfaceGroup.DIRT:   (1.0, 0.6, 0.6, 0.6, 0.85, 0.6, 1.0),
    SurfaceGroup.MUD:    (0.8, 0.35, 0.35, 0.35, 0.6, 0.35, 0.8),
}
_MAX_SAC = (6, 2, 1, 0, 0, 0, 3)
_CLIMB_K = (6.0, 8.0, 1.0, 1.0, 1.0, 1.0, 4.0)
_ALPINE_SPEED = (3.5, _INF, _INF, _INF, _INF, _INF, _INF)

_TRAIL_VALUES = frozenset(int(c) for c in TRAIL_CLASSES)
_SURFACE_GROUP_LUT = np.full(256, int(SurfaceGroup.DIRT), dtype=np.uint8)
for _s, _g in SURFACE_GROUP.items():
    _SURFACE_GROUP_LUT[int(_s)] = int(_g)
_TRAIL_LUT = np.zeros(256, dtype=bool)
_TRAIL_LUT[sorted(_TRAIL_VALUES)] = True


@dataclass(frozen=True)
class TravelProfile:
    """Speed and capability table of one travel mode."""

    travel: Travel
    speed_kmh: tuple[float, ...]  # by RoadClass value
    surface_factor: tuple[float, ...]  # by SurfaceGroup value
    max_sac: int  # highest sac_scale allowed on TRAIL_CLASSES edges
    climb_k: float
    alpine_speed_kmh: float = _INF  # speed cap when sac_scale >= T4

    @property
    def name(self) -> str:
        return str(self.travel.name)

    @property
    def max_speed_kmh(self) -> float:
        """Upper bound of the effective speed on any edge (the A* heuristic's divisor)."""
        return max(self.speed_kmh) * max(self.surface_factor)

    def allows(self, road_class: int, sac_scale: int) -> bool:
        """Whether the class/sac_scale combination is physically usable (ignoring access tags)."""
        rc = int(road_class)
        if not 0 <= rc < len(self.speed_kmh) or self.speed_kmh[rc] <= 0.0:
            return False
        return not (rc in _TRAIL_VALUES and int(sac_scale) > self.max_sac)

    def speed(self, road_class: int, surface: int, sac_scale: int) -> float:
        """Effective speed in km/h; 0.0 when the profile cannot use such an edge."""
        if not self.allows(road_class, sac_scale):
            return 0.0
        v = self.speed_kmh[int(road_class)] * self.surface_factor[int(_SURFACE_GROUP_LUT[int(surface) & 0xFF])]
        if int(sac_scale) >= ALPINE_SAC:
            v = min(v, self.alpine_speed_kmh)
        return v


def _make_profiles() -> dict[Travel, TravelProfile]:
    out: dict[Travel, TravelProfile] = {}
    for i, t in enumerate(PROFILES):
        out[t] = TravelProfile(
            travel=t,
            speed_kmh=tuple(_SPEED_ROWS[c][i] for c in RoadClass),
            surface_factor=tuple(_SURFACE_ROWS[g][i] for g in SurfaceGroup),
            max_sac=_MAX_SAC[i],
            climb_k=_CLIMB_K[i],
            alpine_speed_kmh=_ALPINE_SPEED[i],
        )
    return out


TRAVEL_PROFILES: dict[Travel, TravelProfile] = _make_profiles()

# Per-profile lookup tables indexed by the raw u8 class / surface value.
_SPEED_LUT: dict[Travel, np.ndarray] = {}
_SURFACE_LUT: dict[Travel, np.ndarray] = {}
for _t, _p in TRAVEL_PROFILES.items():
    _lut = np.zeros(256, dtype=np.float64)
    _lut[:len(_p.speed_kmh)] = _p.speed_kmh
    _SPEED_LUT[_t] = _lut
    _SURFACE_LUT[_t] = np.asarray(_p.surface_factor, dtype=np.float64)[_SURFACE_GROUP_LUT]


def get_profile(profile: Travel | int | str) -> TravelProfile:
    """The profile for a single-bit ``Travel`` value (or its name)."""
    try:
        key = Travel[profile.upper()] if isinstance(profile, str) else Travel(int(profile))
    except (KeyError, ValueError):
        raise ValueError(f"unknown travel profile {profile!r}") from None
    p = TRAVEL_PROFILES.get(key)
    if p is None:
        raise ValueError(f"{profile!r} is not a single travel profile")
    return p


def usable_mask(road_class: int, sac_scale: int) -> int:
    """Travel bits whose profile can physically use an edge of this class and ``sac_scale``."""
    m = 0
    for t, p in TRAVEL_PROFILES.items():
        if p.allows(road_class, sac_scale):
            m |= int(t)
    return m


def climb_factor(profile: Travel | int | str, climb_m: int, length_m: float) -> float:
    """``1 + max(0, climb) / length * k`` (1 for zero-length edges)."""
    p = get_profile(profile)
    if length_m <= 0.0:
        return 1.0
    return 1.0 + max(0, int(climb_m)) / length_m * p.climb_k


def travel_profiles_table() -> dict:
    """JSON-friendly dump of every table, for the C# golden test."""
    return {
        "format": "ghumante-travel-profiles", "version": 1,
        "alpine_sac": ALPINE_SAC,
        "trail_classes": sorted(RoadClass(c).name for c in _TRAIL_VALUES),
        "profiles": {
            p.name: {
                "bit": int(t),
                "speed_kmh": {c.name: p.speed_kmh[int(c)] for c in RoadClass},
                "surface_factor": {g.name: p.surface_factor[int(g)] for g in SurfaceGroup},
                "max_sac": p.max_sac,
                "climb_k": p.climb_k,
                "alpine_speed_kmh": None if math.isinf(p.alpine_speed_kmh) else p.alpine_speed_kmh,
                "max_speed_kmh": p.max_speed_kmh,
            }
            for t, p in TRAVEL_PROFILES.items()
        },
    }


# ---------------------------------------------------------------------------
# Graph
# ---------------------------------------------------------------------------
_NODE_FIELDS = (("node_x_dm", np.int32), ("node_z_dm", np.int32), ("node_elev", np.int16))
_EDGE_FIELDS = (
    ("edge_target", np.uint32, "target"), ("edge_length_dm", np.uint32, "length_dm"),
    ("edge_class", np.uint8, "road_class"), ("edge_surface", np.uint8, "surface"),
    ("edge_access", np.uint8, "access"), ("edge_flags", np.uint8, "flags"), ("edge_sac", np.uint8, "sac_scale"),
    ("edge_climb", np.int16, "climb_m"), ("edge_geom_offset", np.uint32, "geom_offset"),
    ("edge_name", np.uint16, "name_index"), ("edge_geom_count", np.uint16, "geom_count"),
)


@dataclass(eq=False)
class RoutingGraph:
    """A CSR routing graph: everything that is serialised, plus lazily built per-profile caches.

    Edges of node ``v`` are ``offsets[v] .. offsets[v + 1]``. ``edge_name`` is
    ``0`` for unnamed edges, otherwise ``names[edge_name - 1]``. Treat the arrays
    as immutable: the caches are not invalidated.
    """

    node_x_dm: np.ndarray
    node_z_dm: np.ndarray
    node_elev: np.ndarray  # whole metres, ELEV_UNKNOWN = no data
    offsets: np.ndarray
    edge_target: np.ndarray
    edge_length_dm: np.ndarray
    edge_class: np.ndarray
    edge_surface: np.ndarray
    edge_access: np.ndarray
    edge_flags: np.ndarray
    edge_sac: np.ndarray
    edge_climb: np.ndarray
    edge_geom_offset: np.ndarray
    edge_name: np.ndarray
    edge_geom_count: np.ndarray
    geometry: bytes = b""
    names: list[NameRec] = field(default_factory=list)
    flags: int = 0
    node_osm_id: np.ndarray | None = None  # build-time only, not serialised (0 = synthetic)
    build_info: dict = field(default_factory=dict)
    _cache: dict = field(default_factory=dict, repr=False)

    def __post_init__(self) -> None:
        for name, dt in _NODE_FIELDS:
            setattr(self, name, np.ascontiguousarray(getattr(self, name), dtype=dt))
        self.offsets = np.ascontiguousarray(self.offsets, dtype=np.uint32)
        for name, dt, _ in _EDGE_FIELDS:
            setattr(self, name, np.ascontiguousarray(getattr(self, name), dtype=dt))
        self.geometry = bytes(self.geometry)
        self.names = [NameRec(n.default, n.en, n.ne) for n in self.names]

    # --- basic accessors -------------------------------------------------
    @property
    def node_count(self) -> int:
        return int(self.node_x_dm.shape[0])

    @property
    def edge_count(self) -> int:
        return int(self.edge_target.shape[0])

    @property
    def edge_source(self) -> np.ndarray:
        """(E,) source node of every edge, derived from ``offsets``."""
        src = self._cache.get("edge_source")
        if src is None:
            src = np.repeat(np.arange(self.node_count, dtype=np.int64), np.diff(self.offsets.astype(np.int64)))
            src.setflags(write=False)
            self._cache["edge_source"] = src
        return src

    def edges_from(self, v: int) -> range:
        return range(int(self.offsets[v]), int(self.offsets[v + 1]))

    def node_xz(self, v: int) -> tuple[float, float]:
        """Game metres of node ``v``."""
        return int(self.node_x_dm[v]) / 10.0, int(self.node_z_dm[v]) / 10.0

    def node_xz_array(self) -> np.ndarray:
        """(N, 2) game metres of every node."""
        return np.column_stack((self.node_x_dm, self.node_z_dm)).astype(np.float64) / 10.0

    def edge_geometry_dm(self, e: int) -> np.ndarray:
        """(geom_count, 2) int64 decimetre polyline of edge ``e``, from its source to its target."""
        e = int(e)
        n = int(self.edge_geom_count[e])
        r = Reader(self.geometry, int(self.edge_geom_offset[e]))
        v = int(self.edge_source[e])
        x, z = int(self.node_x_dm[v]), int(self.node_z_dm[v])
        out = np.empty((n, 2), dtype=np.int64)
        for i in range(n):
            x += r.svarint()
            z += r.svarint()
            out[i, 0] = x
            out[i, 1] = z
        return out

    def edge_geometry(self, e: int) -> np.ndarray:
        """(geom_count, 2) polyline of edge ``e`` in game metres."""
        return self.edge_geometry_dm(e).astype(np.float64) / 10.0

    def edge_name_rec(self, e: int) -> NameRec | None:
        k = int(self.edge_name[e])
        return self.names[k - 1] if k else None

    # --- validation and comparison -----------------------------------------
    def validate(self) -> None:
        """Raise ``ValueError`` when the arrays are inconsistent or do not fit the format."""
        n, e = self.node_count, self.edge_count
        for name, _ in _NODE_FIELDS:
            if getattr(self, name).shape != (n,):
                raise ValueError(f"{name} has shape {getattr(self, name).shape}, expected ({n},)")
        for name, _, _ in _EDGE_FIELDS:
            if getattr(self, name).shape != (e,):
                raise ValueError(f"{name} has shape {getattr(self, name).shape}, expected ({e},)")
        if self.offsets.shape != (n + 1,):
            raise ValueError("offsets must have node_count + 1 entries")
        if self.offsets[0] != 0 or self.offsets[-1] != e or np.any(np.diff(self.offsets.astype(np.int64)) < 0):
            raise ValueError("offsets are not a valid CSR index")
        if e and int(self.edge_target.max()) >= n:
            raise ValueError("edge target out of range")
        if e and int(self.edge_name.max()) > len(self.names):
            raise ValueError("edge name index out of range")
        if len(self.names) > MAX_NAMES:
            raise ValueError(f"{len(self.names)} names do not fit a u16 index")
        if len(self.geometry) >= 1 << 32:
            raise ValueError("geometry blob larger than 4 GiB")
        if e and (int(self.edge_geom_count.min()) < 2 or int(self.edge_geom_offset.max()) >= len(self.geometry)):
            raise ValueError("edge geometry out of range")

    def __eq__(self, other: object) -> bool:
        """Equality of everything that is serialised (caches and build info are ignored)."""
        if not isinstance(other, RoutingGraph):
            return NotImplemented
        arrays = [name for name, _ in _NODE_FIELDS] + ["offsets"] + [name for name, _, _ in _EDGE_FIELDS]
        return (all(getattr(self, a).dtype == getattr(other, a).dtype
                    and np.array_equal(getattr(self, a), getattr(other, a)) for a in arrays)
                and self.geometry == other.geometry and self.names == other.names and self.flags == other.flags)

    __hash__ = None  # type: ignore[assignment]

    # --- statistics ---------------------------------------------------------
    def stats(self) -> dict:
        """Sizes, network length and, per profile, usable km and connected components.

        ``total_km`` counts each road piece once (a two-way piece has two
        directed edges). Components are weakly connected components of the
        profile's usable edges, counting only nodes that touch such an edge.
        """
        n, e = self.node_count, self.edge_count
        src = self.edge_source
        tgt = self.edge_target.astype(np.int64)
        length = self.edge_length_dm.astype(np.int64)
        if e:
            rows = np.column_stack((np.minimum(src, tgt), np.maximum(src, tgt), length, self.edge_class,
                                    self.edge_surface, self.edge_flags, self.edge_sac, self.edge_name,
                                    self.edge_geom_count)).astype(np.int64)
            pieces, inv = np.unique(rows, axis=0, return_inverse=True)
            inv = inv.reshape(-1)
            piece_len = pieces[:, 2]
        else:
            inv = np.zeros(0, dtype=np.int64)
            piece_len = np.zeros(0, dtype=np.int64)
        profiles: dict[str, dict] = {}
        for t in PROFILES:
            use = np.isfinite(edge_times(self, t))
            piece_use = np.zeros(len(piece_len), dtype=bool)
            piece_use[inv[use]] = True
            touched = np.unique(np.concatenate((src[use], tgt[use])))
            comps, largest = 0, 0.0
            if touched.size:
                m = csr_matrix((np.ones(int(use.sum()), dtype=np.int32), (src[use], tgt[use])), shape=(n, n))
                _, labels = connected_components(m, directed=True, connection="weak")
                sizes = np.bincount(labels[touched])
                sizes = sizes[sizes > 0]
                comps, largest = int(sizes.size), float(sizes.max()) / float(touched.size)
            profiles[t.name] = {
                "edges": int(use.sum()),
                "km": round(float(piece_len[piece_use].sum()) / 10_000.0, 3),
                "nodes": int(touched.size),
                "components": comps,
                "largest_component_share": round(largest, 4),
            }
        return {
            "nodes": n,
            "edges": e,
            "names": len(self.names),
            "geom_bytes": len(self.geometry),
            "total_km": round(float(piece_len.sum()) / 10_000.0, 3),
            "directed_km": round(float(length.sum()) / 10_000.0, 3),
            "profiles": profiles,
        }


def empty_graph() -> RoutingGraph:
    z = np.zeros(0)
    return RoutingGraph(z, z, z, np.zeros(1), *([z] * len(_EDGE_FIELDS)))


# ---------------------------------------------------------------------------
# Building
# ---------------------------------------------------------------------------
def lonlat_to_game_xy(lonlat: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    """Default ``to_game``: (N, 2) lon/lat degrees -> game X, Z metres (``projection``)."""
    a = np.asarray(lonlat, dtype=np.float64).reshape(-1, 2)
    x, z = lonlat_to_game(a[:, 0], a[:, 1])
    return np.asarray(x, dtype=np.float64), np.asarray(z, dtype=np.float64)


def _project_all(roads: Sequence[RoadFeature], to_game) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    """Game x, z of every vertex of every road (one projection call) and the split offsets."""
    lens = np.array([np.asarray(r.lonlat).reshape(-1, 2).shape[0] for r in roads], dtype=np.int64)
    off = np.zeros(len(roads) + 1, dtype=np.int64)
    off[1:] = np.cumsum(lens)
    if not len(roads) or off[-1] == 0:
        return np.zeros(0), np.zeros(0), off
    ll = np.concatenate([np.asarray(r.lonlat, dtype=np.float64).reshape(-1, 2) for r in roads])
    x, z = to_game(ll)
    return np.asarray(x, dtype=np.float64), np.asarray(z, dtype=np.float64), off


def _clip_segment(xa: float, za: float, xb: float, zb: float,
                  box: tuple[float, float, float, float]) -> tuple[float, float] | None:
    """Liang-Barsky: the parameter range [t0, t1] of segment a->b inside the closed box, or None."""
    dx, dz = xb - xa, zb - za
    t0, t1 = 0.0, 1.0
    for p, q in ((-dx, xa - box[0]), (dx, box[2] - xa), (-dz, za - box[1]), (dz, box[3] - za)):
        if p == 0.0:
            if q < 0.0:
                return None
            continue
        t = q / p
        if p < 0.0:
            if t > t1:
                return None
            t0 = max(t0, t)
        else:
            if t < t0:
                return None
            t1 = min(t1, t)
    return t0, t1


def clip_roads(roads: Sequence[RoadFeature], box: tuple[float, float, float, float], *,
               to_game: Callable[[np.ndarray], tuple[np.ndarray, np.ndarray]] = lonlat_to_game_xy,
               to_lonlat: Callable | None = None) -> list[RoadFeature]:
    """Clip roads to the closed game-metre box ``(x0, z0, x1, z1)`` (the region's leaf tiles).

    Roads entirely inside are returned as they are; roads entirely outside are
    dropped; the others become one road per inside run (same attributes and
    OSM id) whose cut points are synthetic vertices (node id 0, see
    ``build_graph``) placed exactly on the box edge. Original vertices keep
    their lon/lat and node ids, so the topology inside the box is unchanged.
    ``to_lonlat`` maps game ``(x, z)`` arrays back to lon/lat (default
    ``projection.game_to_lonlat``).
    """
    import dataclasses

    if to_lonlat is None:
        from .projection import game_to_lonlat as to_lonlat
    box = tuple(float(v) for v in box)
    roads = list(roads)
    gx, gz, off = _project_all(roads, to_game)
    out: list[RoadFeature] = []
    for k, r in enumerate(roads):
        x, z = gx[off[k]:off[k + 1]], gz[off[k]:off[k + 1]]
        if x.shape[0] == 0:
            continue
        inside = (x >= box[0]) & (x <= box[2]) & (z >= box[1]) & (z <= box[3])
        if inside.all():
            out.append(r)
            continue
        if x.shape[0] < 2 or (x.max() < box[0] or x.min() > box[2] or z.max() < box[1] or z.min() > box[3]):
            continue
        ll = np.asarray(r.lonlat, dtype=np.float64).reshape(-1, 2)
        ids = np.asarray(r.node_ids, dtype=np.int64).reshape(-1)
        runs: list[list[tuple[float, float, int, float, float]]] = []  # (lon, lat, id, x, z); lon nan = cut
        run: list | None = None
        for i in range(x.shape[0] - 1):
            c = _clip_segment(float(x[i]), float(z[i]), float(x[i + 1]), float(z[i + 1]), box)
            if c is None:
                if run is not None:
                    runs.append(run)
                    run = None
                continue
            t0, t1 = c
            dx, dz = float(x[i + 1] - x[i]), float(z[i + 1] - z[i])
            if run is None:
                if t0 == 0.0:
                    run = [(float(ll[i, 0]), float(ll[i, 1]), int(ids[i]), float(x[i]), float(z[i]))]
                else:
                    run = [(math.nan, math.nan, 0, float(x[i]) + t0 * dx, float(z[i]) + t0 * dz)]
            if t1 == 1.0:
                run.append((float(ll[i + 1, 0]), float(ll[i + 1, 1]), int(ids[i + 1]), float(x[i + 1]),
                            float(z[i + 1])))
            else:
                run.append((math.nan, math.nan, 0, float(x[i]) + t1 * dx, float(z[i]) + t1 * dz))
                runs.append(run)
                run = None
        if run is not None:
            runs.append(run)
        for run in runs:
            pts = [run[0]]
            for p in run[1:]:
                if (p[3], p[4]) != (pts[-1][3], pts[-1][4]):
                    pts.append(p)
            if len(pts) < 2:
                continue
            arr = np.array([(p[0], p[1]) for p in pts], dtype=np.float64)
            cut = np.isnan(arr[:, 0])
            if cut.any():
                lon, lat = to_lonlat(np.array([p[3] for p, c in zip(pts, cut) if c]),
                                     np.array([p[4] for p, c in zip(pts, cut) if c]))
                arr[cut, 0], arr[cut, 1] = np.asarray(lon, dtype=np.float64), np.asarray(lat, dtype=np.float64)
            out.append(dataclasses.replace(r, lonlat=arr,
                                           node_ids=np.array([p[2] for p in pts], dtype=np.int64)))
    return out


def roads_length_m(roads: Sequence[RoadFeature], *,
                   to_game: Callable[[np.ndarray], tuple[np.ndarray, np.ndarray]] = lonlat_to_game_xy) -> float:
    """Total polyline length of the roads in game metres."""
    roads = list(roads)
    x, z, off = _project_all(roads, to_game)
    if x.size < 2:
        return 0.0
    seg = np.hypot(np.diff(x), np.diff(z))
    same = np.ones(seg.shape[0], dtype=bool)
    same[off[1:-1] - 1] = False  # no segment between the last vertex of one road and the next road
    return float(seg[same].sum())


def _name_key(n: NameRec | None) -> tuple[str, str, str] | None:
    if n is None or n.is_empty():
        return None
    return tuple(unicodedata.normalize("NFC", s or "") for s in (n.default, n.en, n.ne))  # type: ignore[return-value]


def _road_flags(r: RoadFeature) -> int:
    return ((EDGE_BRIDGE if r.bridge else 0) | (EDGE_TUNNEL if r.tunnel else 0) | (EDGE_FORD if r.ford else 0)
            | (EDGE_LINK if r.is_link else 0))


def encode_svarints(values: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    """Vectorised zigzag + LEB128 (identical to ``binio.Writer.svarint``).

    Returns the encoded bytes (uint8) and the byte count of each value.
    """
    v = np.ascontiguousarray(values, dtype=np.int64)
    zz = ((v << 1) ^ (v >> 63)).view(np.uint64)
    nb = np.ones(v.shape[0], dtype=np.int64)
    for k in range(1, 10):
        nb += zz >= np.uint64(1 << (7 * k))
    ends = np.cumsum(nb)
    starts = ends - nb
    out = np.zeros(int(ends[-1]) if v.size else 0, dtype=np.uint8)
    for k in range(int(nb.max()) if v.size else 0):
        m = nb > k
        byte = ((zz[m] >> np.uint64(7 * k)) & np.uint64(0x7F)).astype(np.uint8)
        out[starts[m] + k] = byte | ((nb[m] - 1 > k).astype(np.uint8) << 7)
    return out, nb


def build_graph(
    roads: Sequence[RoadFeature],
    to_game: Callable[[np.ndarray], tuple[np.ndarray, np.ndarray]] = lonlat_to_game_xy,
    elev: Callable[[np.ndarray, np.ndarray], np.ndarray] | None = None,
) -> RoutingGraph:
    """Build the routing graph of a region's roads and trails (rules in the module docstring).

    ``to_game`` maps an (N, 2) lon/lat array to game ``(x, z)``; ``elev`` maps
    game ``(x, z)`` arrays to metres (None: every elevation and climb is 0).
    """
    roads = list(roads)
    info = {"roads_in": len(roads), "roads_used": 0, "skipped_unknown_class": 0, "skipped_no_access": 0,
            "skipped_degenerate": 0, "loops_split": 0, "long_split": 0, "oneway_reverse_omitted": 0,
            "names_dropped": 0}

    # 1. Select routable ways (sorted by way id so the output ignores input order).
    order = sorted(range(len(roads)), key=lambda i: int(roads[i].osm_id))
    ll_parts: list[np.ndarray] = []
    id_parts: list[np.ndarray] = []
    attrs: list[tuple[int, int, int, int, int, int, int]] = []  # cls, surface, sac, flags, mask, oneway, osm_id
    name_keys: list[tuple[str, str, str] | None] = []
    for i in order:
        r = roads[i]
        cls = int(r.cls)
        if cls == RoadClass.UNKNOWN:
            info["skipped_unknown_class"] += 1
            continue
        mask = int(r.access) & usable_mask(cls, int(r.sac_scale))
        if not mask:
            info["skipped_no_access"] += 1
            continue
        ll = np.asarray(r.lonlat, dtype=np.float64)
        ids = np.asarray(r.node_ids, dtype=np.int64).reshape(-1)
        if ll.ndim != 2 or ll.shape[1] != 2 or ll.shape[0] != ids.shape[0]:
            raise ValueError(f"way {r.osm_id}: lonlat {ll.shape} and node_ids {ids.shape} do not match")
        if not np.isfinite(ll).all():
            raise ValueError(f"way {r.osm_id}: non-finite coordinates")
        if ids.shape[0] < 2:
            info["skipped_degenerate"] += 1
            continue
        if int(r.oneway) < 0:
            ll, ids = ll[::-1], ids[::-1]
        ll_parts.append(ll)
        id_parts.append(ids)
        attrs.append((cls, int(r.surface) & 0xFF, int(r.sac_scale) & 0xFF, _road_flags(r), mask,
                      1 if int(r.oneway) != 0 else 0, int(r.osm_id)))
        name_keys.append(_name_key(r.name))
    if not id_parts:
        g = empty_graph()
        g.build_info = info
        return g

    lens = np.array([p.shape[0] for p in id_parts], dtype=np.int64)
    road_of = np.repeat(np.arange(len(id_parts), dtype=np.int64), lens)
    ids = np.concatenate(id_parts)
    ll = np.concatenate(ll_parts)
    attr = np.array(attrs, dtype=np.int64)

    # Drop consecutive duplicate (real) node ids, then ways left with < 2 vertices.
    dup = np.zeros(ids.shape[0], dtype=bool)
    dup[1:] = (ids[1:] == ids[:-1]) & (road_of[1:] == road_of[:-1]) & (ids[1:] > 0)
    if dup.any():
        ids, ll, road_of = ids[~dup], ll[~dup], road_of[~dup]
        lens = np.bincount(road_of, minlength=len(attr))
        good = lens >= 2
        if not good.all():
            info["skipped_degenerate"] += int((~good).sum())
            keep_v = good[road_of]
            remap = np.cumsum(good) - 1
            ids, ll, road_of = ids[keep_v], ll[keep_v], remap[road_of[keep_v]]
            attr, lens = attr[good], lens[good]
            name_keys = [k for k, ok in zip(name_keys, good) if ok]
        if not len(attr):
            g = empty_graph()
            g.build_info = info
            return g
    info["roads_used"] = int(len(attr))
    nv = ids.shape[0]
    last = np.cumsum(lens) - 1
    first = last - lens + 1

    # 2. Graph nodes. Synthetic vertices (id <= 0) get unique keys above every real id.
    syn = ids <= 0
    key = ids.copy()
    if syn.any():
        key[syn] = max(int(ids.max()), 0) + 1 + np.arange(int(syn.sum()), dtype=np.int64)
    _, inv, cnt = np.unique(key, return_inverse=True, return_counts=True)
    split = cnt[inv.reshape(-1)] >= 2
    split[first] = True
    split[last] = True

    def pieces() -> tuple[np.ndarray, np.ndarray]:
        sp = np.flatnonzero(split)
        a, b = sp[:-1], sp[1:]
        same = road_of[a] == road_of[b]
        return a[same], b[same]

    a, b = pieces()
    loop = key[a] == key[b]
    if loop.any():
        info["loops_split"] = int(loop.sum())
        split[(a[loop] + b[loop]) // 2] = True
        a, b = pieces()
    too_long = (b - a + 1) > MAX_GEOM_POINTS
    if too_long.any():
        info["long_split"] = int(too_long.sum())
        for aa, bb in zip(a[too_long].tolist(), b[too_long].tolist()):
            split[aa + MAX_GEOM_POINTS - 1:bb:MAX_GEOM_POINTS - 1] = True
        a, b = pieces()

    sp = np.flatnonzero(split)
    node_keys, first_idx = np.unique(key[sp], return_index=True)
    n_nodes = node_keys.shape[0]
    vertex_node = np.full(nv, -1, dtype=np.int64)
    vertex_node[sp] = np.searchsorted(node_keys, key[sp])
    node_vertex = sp[first_idx]

    # 3. Positions (dm), elevations.
    x, z = to_game(ll)
    x = np.asarray(x, dtype=np.float64).reshape(-1)
    z = np.asarray(z, dtype=np.float64).reshape(-1)
    if x.shape[0] != nv or z.shape[0] != nv:
        raise ValueError("to_game returned the wrong number of points")
    if not (np.isfinite(x).all() and np.isfinite(z).all()):
        raise ValueError("to_game returned non-finite coordinates")
    qx = np.rint(x * 10.0).astype(np.int64)
    qz = np.rint(z * 10.0).astype(np.int64)
    node_qx, node_qz = qx[node_vertex], qz[node_vertex]
    lim = (1 << 31) - 1
    if np.abs(qx).max() > lim or np.abs(qz).max() > lim:
        raise ValueError("game coordinates do not fit i32 decimetres")
    qx[sp] = node_qx[vertex_node[sp]]
    qz[sp] = node_qz[vertex_node[sp]]

    node_elev = np.zeros(n_nodes, dtype=np.int16)
    if elev is not None:
        ev = np.asarray(elev(x[node_vertex], z[node_vertex]), dtype=np.float64).reshape(-1)
        if ev.shape[0] != n_nodes:
            raise ValueError(f"elev returned {ev.shape[0]} values for {n_nodes} nodes")
        ok = np.isfinite(ev)
        node_elev = np.where(ok, np.clip(np.rint(np.where(ok, ev, 0.0)), -32767, 32767), ELEV_UNKNOWN)
        node_elev = node_elev.astype(np.int16)

    # 4. Pieces: lengths and attributes.
    seg = np.hypot(np.diff(x), np.diff(z))
    seg[road_of[1:] != road_of[:-1]] = 0.0
    piece_len = np.add.reduceat(seg, a)
    chord = np.hypot((qx[b] - qx[a]).astype(np.float64), (qz[b] - qz[a]).astype(np.float64))
    length_dm = np.maximum(np.rint(piece_len * 10.0), np.ceil(chord)).astype(np.int64)
    if length_dm.size and int(length_dm.max()) >= 1 << 32:
        raise ValueError("edge longer than the u32 length field")
    pr = road_of[a]
    p_src, p_tgt = vertex_node[a], vertex_node[b]
    p_mask = attr[pr, 4]
    p_rev_mask = np.where(attr[pr, 5] != 0, p_mask & int(Travel.FOOT), p_mask)
    info["oneway_reverse_omitted"] = int(((attr[pr, 5] != 0) & (p_rev_mask == 0)).sum())

    # 5. Directed edges in canonical order.
    n_p = a.shape[0]
    has_rev = np.flatnonzero(p_rev_mask != 0)
    pidx = np.concatenate((np.arange(n_p, dtype=np.int64), has_rev))
    rev = np.concatenate((np.zeros(n_p, dtype=bool), np.ones(has_rev.shape[0], dtype=bool)))
    src = np.where(rev, p_tgt[pidx], p_src[pidx])
    tgt = np.where(rev, p_src[pidx], p_tgt[pidx])
    o = np.lexsort((rev, a[pidx], tgt, src))
    pidx, rev, src, tgt = pidx[o], rev[o], src[o], tgt[o]
    n_e = pidx.shape[0]
    er = pr[pidx]
    access = np.where(rev, p_rev_mask[pidx], p_mask[pidx])

    es, et = node_elev[src].astype(np.int64), node_elev[tgt].astype(np.int64)
    unknown = (es == ELEV_UNKNOWN) | (et == ELEV_UNKNOWN)
    climb = np.where(unknown, 0, np.clip(et - es, -32768, 32767))

    # 6. Geometry blob (vectorised delta + svarint encoding).
    counts = (b - a + 1)[pidx]
    start_v = np.where(rev, b[pidx], a[pidx])
    step = np.where(rev, -1, 1)
    pt_first = np.cumsum(counts) - counts
    k = np.arange(int(counts.sum()), dtype=np.int64) - np.repeat(pt_first, counts)
    vidx = np.repeat(start_v, counts) + np.repeat(step, counts) * k
    gx, gz = qx[vidx], qz[vidx]
    dx = np.empty_like(gx)
    dz = np.empty_like(gz)
    dx[1:] = gx[1:] - gx[:-1]
    dz[1:] = gz[1:] - gz[:-1]
    dx[pt_first] = gx[pt_first] - node_qx[src]
    dz[pt_first] = gz[pt_first] - node_qz[src]
    blob, nb = encode_svarints(np.column_stack((dx, dz)).reshape(-1))
    edge_bytes = np.add.reduceat(nb.reshape(-1, 2).sum(axis=1), pt_first)
    geom_offset = np.cumsum(edge_bytes) - edge_bytes
    if blob.shape[0] >= 1 << 32:
        raise ValueError("geometry blob larger than 4 GiB")

    # 7. Names.
    uniq_names = sorted({k_ for k_ in name_keys if k_ is not None})
    if len(uniq_names) > MAX_NAMES:
        info["names_dropped"] = len(uniq_names) - MAX_NAMES
        uniq_names = uniq_names[:MAX_NAMES]
    name_id = {k_: i + 1 for i, k_ in enumerate(uniq_names)}
    road_name = np.array([name_id.get(k_, 0) if k_ is not None else 0 for k_ in name_keys], dtype=np.int64)

    offsets = np.zeros(n_nodes + 1, dtype=np.int64)
    offsets[1:] = np.cumsum(np.bincount(src, minlength=n_nodes))
    node_osm = np.where(syn[node_vertex], 0, node_keys)

    g = RoutingGraph(
        node_x_dm=node_qx, node_z_dm=node_qz, node_elev=node_elev, offsets=offsets,
        edge_target=tgt, edge_length_dm=length_dm[pidx], edge_class=attr[er, 0], edge_surface=attr[er, 1],
        edge_access=access, edge_flags=attr[er, 3], edge_sac=attr[er, 2], edge_climb=climb,
        edge_geom_offset=geom_offset, edge_name=road_name[er], edge_geom_count=counts,
        geometry=blob.tobytes(), names=[NameRec(*k_) for k_ in uniq_names],
        node_osm_id=node_osm.astype(np.int64), build_info=info,
    )
    g.validate()
    return g


# ---------------------------------------------------------------------------
# Binary I/O
# ---------------------------------------------------------------------------
def encode_graph(g: RoutingGraph) -> bytes:
    """Serialise to a GHRG blob (deterministic)."""
    g.validate()
    n, e = g.node_count, g.edge_count
    nodes = np.zeros(n, dtype=NODE_DTYPE)
    nodes["x_dm"], nodes["z_dm"], nodes["elev_m"] = g.node_x_dm, g.node_z_dm, g.node_elev
    edges = np.zeros(e, dtype=EDGE_DTYPE)
    for name, _, col in _EDGE_FIELDS:
        edges[col] = getattr(g, name)
    w = Writer()
    for nm in g.names:
        w.str(nm.default).str(nm.en).str(nm.ne)
    header = _HEADER.pack(MAGIC, VERSION, g.flags & 0xFFFF, n, e, len(g.geometry), len(g.names), 0)
    return b"".join((header, nodes.tobytes(), g.offsets.astype("<u4").tobytes(), edges.tobytes(), g.geometry,
                     w.bytes()))


def decode_graph(data: bytes | memoryview) -> RoutingGraph:
    """Parse a GHRG blob; rejects unknown versions, truncation and trailing bytes."""
    data = bytes(data)
    if len(data) < HEADER_SIZE:
        raise ValueError("GHRG blob shorter than its header")
    magic, version, flags, n, e, gb, nc, _ = _HEADER.unpack_from(data, 0)
    if magic != MAGIC:
        raise ValueError(f"bad magic {magic!r}")
    if version != VERSION:
        raise ValueError(f"unsupported GHRG version {version}")
    pos = HEADER_SIZE
    need = pos + n * NODE_SIZE + (n + 1) * 4 + e * EDGE_SIZE + gb
    if len(data) < need:
        raise ValueError(f"GHRG blob truncated: {len(data)} bytes, sections need {need}")
    nodes = np.frombuffer(data, dtype=NODE_DTYPE, count=n, offset=pos)
    pos += n * NODE_SIZE
    offsets = np.frombuffer(data, dtype="<u4", count=n + 1, offset=pos)
    pos += (n + 1) * 4
    edges = np.frombuffer(data, dtype=EDGE_DTYPE, count=e, offset=pos)
    pos += e * EDGE_SIZE
    geometry = data[pos:pos + gb]
    r = Reader(data, pos + gb)
    try:
        names = [NameRec(r.str(), r.str(), r.str()) for _ in range(nc)]
    except (EOFError, UnicodeDecodeError) as exc:
        raise ValueError(f"bad GHRG names section: {exc}") from None
    if r.remaining():
        raise ValueError(f"{r.remaining()} trailing bytes after the names section")
    g = RoutingGraph(
        node_x_dm=nodes["x_dm"], node_z_dm=nodes["z_dm"], node_elev=nodes["elev_m"], offsets=offsets,
        **{name: edges[col] for name, _, col in _EDGE_FIELDS},
        geometry=geometry, names=names, flags=flags,
    )
    g.validate()
    return g


def write_graph(path: str | Path, g: RoutingGraph) -> dict:
    """Write a ``.ghrg`` file atomically. Returns path, bytes, sha256 and ``g.stats()``."""
    data = encode_graph(g)
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_name(path.name + ".tmp")
    try:
        tmp.write_bytes(data)
        os.replace(tmp, path)
    except BaseException:
        tmp.unlink(missing_ok=True)
        raise
    return {"path": str(path), "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest(), **g.stats()}


def read_graph(path: str | Path) -> RoutingGraph:
    return decode_graph(Path(path).read_bytes())


# ---------------------------------------------------------------------------
# Costs and queries
# ---------------------------------------------------------------------------
def edge_time_s(g: RoutingGraph, e: int, profile: Travel | int | str) -> float:
    """Seconds to traverse edge ``e`` with ``profile``; ``inf`` when not allowed."""
    p = get_profile(profile)
    e = int(e)
    if not int(g.edge_access[e]) & int(p.travel):
        return math.inf
    speed = p.speed(int(g.edge_class[e]), int(g.edge_surface[e]), int(g.edge_sac[e]))
    if speed <= 0.0:
        return math.inf
    length_m = int(g.edge_length_dm[e]) / 10.0
    cf = 1.0 + max(0, int(g.edge_climb[e])) / length_m * p.climb_k if length_m > 0.0 else 1.0
    return length_m * 3.6 / speed * cf


def edge_times(g: RoutingGraph, profile: Travel | int | str) -> np.ndarray:
    """Vectorised ``edge_time_s`` for every edge (read-only, cached per profile)."""
    p = get_profile(profile)
    key = ("times", p.travel)
    t = g._cache.get(key)
    if t is not None:
        return t
    cls = g.edge_class.astype(np.intp)
    sac = g.edge_sac.astype(np.int64)
    speed = _SPEED_LUT[p.travel][cls] * _SURFACE_LUT[p.travel][g.edge_surface.astype(np.intp)]
    speed = np.where(sac >= ALPINE_SAC, np.minimum(speed, p.alpine_speed_kmh), speed)
    ok = ((g.edge_access & int(p.travel)) != 0) & (speed > 0.0) & ~(_TRAIL_LUT[cls] & (sac > p.max_sac))
    length_m = g.edge_length_dm.astype(np.float64) / 10.0
    climb = np.maximum(g.edge_climb.astype(np.float64), 0.0)
    with np.errstate(divide="ignore", invalid="ignore"):
        cf = np.where(length_m > 0.0, 1.0 + climb / length_m * p.climb_k, 1.0)
        t = np.where(ok, length_m * 3.6 / speed * cf, np.inf)
    t.setflags(write=False)
    g._cache[key] = t
    return t


def _usable_csr(g: RoutingGraph, p: TravelProfile) -> tuple[list[int], list[int], list[float], list[int]]:
    """Per-profile CSR of usable edges as Python lists (offsets, targets, times, edge ids)."""
    key = ("csr", p.travel)
    c = g._cache.get(key)
    if c is None:
        t = edge_times(g, p.travel)
        eid = np.flatnonzero(np.isfinite(t))
        off = np.zeros(g.node_count + 1, dtype=np.int64)
        off[1:] = np.cumsum(np.bincount(g.edge_source[eid], minlength=g.node_count))
        c = (off.tolist(), g.edge_target[eid].tolist(), t[eid].tolist(), eid.tolist())
        g._cache[key] = c
    return c


def _node_lists(g: RoutingGraph) -> tuple[list[int], list[int]]:
    c = g._cache.get("node_lists")
    if c is None:
        c = g._cache["node_lists"] = (g.node_x_dm.tolist(), g.node_z_dm.tolist())
    return c


@dataclass
class Route:
    nodes: list[int]  # node indices from source to destination
    edges: list[int]  # edge indices, len(nodes) - 1
    length_m: float
    time_s: float


def route(g: RoutingGraph, src: int, dst: int, profile: Travel | int | str, *,
          heuristic: bool = True) -> Route | None:
    """Fastest route from node ``src`` to node ``dst``, or None when unreachable.

    A* with ``h(v) = |v - dst| / max_speed`` (admissible and consistent, see the
    module docstring); nodes may be re-opened, so the result is optimal even if
    rounding makes ``h`` inconsistent by an ulp. ``heuristic=False`` runs
    plain Dijkstra.
    """
    p = get_profile(profile)
    n = g.node_count
    src, dst = int(src), int(dst)
    if not (0 <= src < n and 0 <= dst < n):
        raise IndexError(f"node index out of range (node_count {n})")
    if src == dst:
        return Route([src], [], 0.0, 0.0)
    off, tgt, times, eid = _usable_csr(g, p)
    xs, zs = _node_lists(g)
    xd, zd = xs[dst], zs[dst]
    vmax = p.max_speed_kmh

    if heuristic:
        def h(v: int) -> float:
            return math.hypot(xs[v] - xd, zs[v] - zd) / 10.0 * 3.6 / vmax
    else:
        def h(v: int) -> float:
            return 0.0

    best: dict[int, float] = {src: 0.0}
    prev: dict[int, int] = {}  # node -> index into the usable-edge lists
    hc: dict[int, float] = {}
    heap: list[tuple[float, float, int]] = [(h(src), 0.0, src)]
    while heap:
        _, gv, v = heapq.heappop(heap)
        if gv > best[v]:
            continue
        if v == dst:
            break
        for j in range(off[v], off[v + 1]):
            w = tgt[j]
            ng = gv + times[j]
            old = best.get(w)
            if old is None or ng < old:
                best[w] = ng
                prev[w] = j
                hw = hc.get(w)
                if hw is None:
                    hw = hc[w] = h(w)
                heapq.heappush(heap, (ng + hw, ng, w))
    else:
        return None

    src_of = g.edge_source
    edges: list[int] = []
    nodes = [dst]
    v = dst
    while v != src:
        e = eid[prev[v]]
        edges.append(e)
        v = int(src_of[e])
        nodes.append(v)
    edges.reverse()
    nodes.reverse()
    length_dm = int(g.edge_length_dm[np.asarray(edges, dtype=np.int64)].astype(np.int64).sum())
    return Route(nodes, edges, length_dm / 10.0, best[dst])


def route_geometry(g: RoutingGraph, r: Route) -> np.ndarray:
    """(N, 2) game-metre polyline of a route (shared joints appear once)."""
    if not r.edges:
        x, z = g.node_xz(r.nodes[0])
        return np.array([[x, z]], dtype=np.float64)
    parts = [g.edge_geometry(e) for e in r.edges]
    return np.concatenate([parts[0]] + [q[1:] for q in parts[1:]])


def main_component(g: RoutingGraph, profile: Travel | int | str) -> np.ndarray:
    """Sorted node indices of the profile's *main component*: the largest strongly
    connected component of the usable-edge graph (ties: the component holding
    the smallest node index). Empty when no component has two or more nodes.
    Mirrored by ``NearestNode.MainComponent`` in C#."""
    p = get_profile(profile)
    key = ("main", p.travel)
    c = g._cache.get(key)
    if c is None:
        c = g._cache[key] = _main_component(g, p)
    return c


def _usable_matrix(g: RoutingGraph, p: TravelProfile) -> csr_matrix:
    use = np.isfinite(edge_times(g, p.travel))
    n = g.node_count
    src = g.edge_source[use]
    dst = g.edge_target[use].astype(np.int64)
    return csr_matrix((np.ones(src.size, dtype=np.int8), (src, dst)), shape=(n, n))


def _main_component(g: RoutingGraph, p: TravelProfile) -> np.ndarray:
    n = g.node_count
    if n == 0:
        return np.zeros(0, dtype=np.int64)
    _, labels = connected_components(_usable_matrix(g, p), directed=True, connection="strong")
    sizes = np.bincount(labels)
    best = int(sizes.max())
    if best < 2:
        return np.zeros(0, dtype=np.int64)
    first = int(np.flatnonzero(sizes[labels] == best)[0])  # smallest node of a largest component
    return np.flatnonzero(labels == labels[first]).astype(np.int64)


def snap_nodes(g: RoutingGraph, profile: Travel | int | str, incoming: bool = False,
               main_network: bool = True) -> np.ndarray:
    """Sorted node indices that ``nearest_node`` may return.

    Always nodes with a usable outgoing edge for the profile (``incoming``: a
    usable incoming edge). With ``main_network`` (the default), and when the
    profile has a main component (``main_component``), only the nodes that can
    reach it (``incoming=False``: route starts) or that can be reached from it
    (``incoming=True``: destinations). Any start can then reach any destination,
    so a landmark never snaps into a small disconnected island (a two-node
    oneway fragment, a footway ring in a compound) and returns no route.
    """
    p = get_profile(profile)
    key = ("snap", p.travel, bool(incoming), bool(main_network))
    c = g._cache.get(key)
    if c is not None:
        return c
    use = np.isfinite(edge_times(g, p.travel))
    ends = g.edge_target[use].astype(np.int64) if incoming else g.edge_source[use]
    ids = np.unique(ends)
    if main_network:
        main = main_component(g, p.travel)
        if main.size:
            m = _usable_matrix(g, p)
            reach = breadth_first_order(m if incoming else m.T.tocsr(), int(main[0]), directed=True,
                                        return_predecessors=False)
            ids = np.intersect1d(ids, reach.astype(np.int64))
    ids = ids.astype(np.int64)
    ids.setflags(write=False)
    g._cache[key] = ids
    return ids


def _kdtree(g: RoutingGraph, p: TravelProfile, incoming: bool,
            main_network: bool) -> tuple[cKDTree | None, np.ndarray]:
    key = ("kdtree", p.travel, incoming, main_network)
    c = g._cache.get(key)
    if c is None:
        ids = snap_nodes(g, p.travel, incoming, main_network)
        tree = cKDTree(g.node_xz_array()[ids]) if ids.size else None
        c = g._cache[key] = (tree, ids)
    return c


def nearest_node(g: RoutingGraph, x: float, z: float, profile: Travel | int | str, *,
                 incoming: bool = False, max_dist_m: float | None = None,
                 main_network: bool = True) -> int | None:
    """Closest node (game metres) among ``snap_nodes(g, profile, incoming, main_network)``.

    By default that is a node with a usable outgoing edge for ``profile`` that
    can reach the profile's main component; with ``incoming=True`` a node with
    a usable incoming edge reachable from it (a destination). Exact distance
    ties (``dx * dx + dz * dz`` in doubles of the decimetre-quantised node
    position) go to the lowest node index, like the C# ``NearestNode``. None
    when no node qualifies or the nearest is farther than ``max_dist_m``.
    """
    tree, ids = _kdtree(g, get_profile(profile), bool(incoming), bool(main_network))
    if tree is None:
        return None
    x, z = float(x), float(z)
    d, _ = tree.query([x, z])
    # Every node at (numerically) the same distance, then the exact C# comparison.
    cand = tree.query_ball_point([x, z], r=float(d) * (1.0 + 1e-9) + 1e-9)
    best_v, best_d2 = -1, math.inf
    for i in cand:
        v = int(ids[int(i)])
        dx = int(g.node_x_dm[v]) / 10.0 - x
        dz = int(g.node_z_dm[v]) / 10.0 - z
        d2 = dx * dx + dz * dz
        if d2 < best_d2 or (d2 == best_d2 and v < best_v):
            best_v, best_d2 = v, d2
    if best_v < 0 or (max_dist_m is not None and math.sqrt(best_d2) > max_dist_m):
        return None
    return best_v


def eta_by_profile(g: RoutingGraph, src_xz: tuple[float, float], dst_xz: tuple[float, float], *,
                   max_snap_m: float | None = None) -> dict[str, tuple[float, float] | None]:
    """``{profile name: (length_m, time_s) or None}`` for the map's "how to get there" panel.

    Both points snap with ``nearest_node`` (main network on: the start must
    reach the profile's main component, the destination be reachable from it),
    so a route exists for every profile that has a main component. Only the network part is
    timed; the walk to and from the network is left to the caller.
    """
    out: dict[str, tuple[float, float] | None] = {}
    for t in PROFILES:
        s = nearest_node(g, src_xz[0], src_xz[1], t, max_dist_m=max_snap_m)
        d = nearest_node(g, dst_xz[0], dst_xz[1], t, incoming=True, max_dist_m=max_snap_m)
        r = route(g, s, d, t) if s is not None and d is not None else None
        out[t.name] = None if r is None else (r.length_m, r.time_s)
    return out
