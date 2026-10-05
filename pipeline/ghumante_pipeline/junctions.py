"""Junctions: the ``JNCT`` chunk (docs/W2_DESIGN.md 4.7 and 9.3, work item D18).

``find_junctions`` returns region-wide ``Junction``s; the tiler writes each to
the leaf tile containing its centre. Records are emitted only for junctions
that need something the runtime cannot infer from ``ROAD`` alone:

* **Rings**: connected ``junction=roundabout|circular`` ways form one ring
  (kind ROUNDABOUT, or CIRCULAR when any member is ``circular``). Centre = the
  mean of the ring's distinct vertices; ``ring_diameter_cm`` = perimeter / pi
  for a closed ring, else the largest chord between ring vertices (split arcs,
  vehicles_traffic 5.1); ``osm_node_id`` = the ring's smallest OSM node id.
  ``arms`` = distinct motor ways touching the ring that are not ring members.
  Island: the largest TRAFFIC_ISLAND, park, meadow or grassland AREA whose
  representative point lies inside the ring circle (``HAS_ISLAND_AREA``,
  ``island_area_osm_ref``, ``island_diameter_cm`` = equal-area diameter).
* **Mini roundabouts**: ``highway=mini_roundabout`` nodes (MINI_ROUNDABOUT).
* **Signals**: ``highway=traffic_signals`` nodes on a motor road (SIGNALS,
  ``HAS_SIGNALS``).
* **Police chowks** (``config/curated/chowks.yaml``): snapped to a ring whose
  centre is within ``snap_m`` (flags added to the ring record), else to the
  motor junction node within ``snap_m`` with the most arms (ties: higher road
  class, then nearer, then lower id). A snapped signal node keeps kind
  SIGNALS; otherwise the kind is SYNTHETIC_ISLAND when the node has >= 4 arms,
  one of them trunk or primary, and no mapped island within
  ``ISLAND_SEARCH_M`` (the runtime sizes the island, W2_DESIGN 4.7), else
  POLICE. Police chowks get ``HAS_POLICE`` and, for ``officers: "2-4"``,
  ``OFFICERS_2_4``; their name is the curated English name with the node's OSM
  ``name:ne`` (never typed Devanagari, ADR-005).

Every record gets ``HERITAGE_NO_MOTOR`` when its centre lies in a
SACRED_NO_VEHICLE zone and ``CROSSINGS_MARKED`` when a marked crossing prop
lies within ``CROSSING_NEAR_M``. Arms count a way twice when the node is in
its interior (two arms) and once at its end.
"""

from __future__ import annotations

import math
from collections import defaultdict
from dataclasses import dataclass, field
from pathlib import Path
from typing import Sequence

import numpy as np
import shapely
import yaml

from . import projection
from .config import CONFIG_DIR
from .model import AreaKind, JunctionFlags, JunctionKind, NameRec, ObjectKind, RoadClass

CHOWKS_PATH = CONFIG_DIR / "curated" / "chowks.yaml"
ISLAND_SEARCH_M = 45.0
CROSSING_NEAR_M = 30.0
MOTOR_CLASSES = frozenset(int(c) for c in (RoadClass.MOTORWAY, RoadClass.TRUNK, RoadClass.PRIMARY,
                                            RoadClass.SECONDARY, RoadClass.TERTIARY, RoadClass.UNCLASSIFIED,
                                            RoadClass.RESIDENTIAL, RoadClass.LIVING_STREET, RoadClass.SERVICE,
                                            RoadClass.ROAD, RoadClass.TRACK))
ISLAND_KINDS = frozenset(int(k) for k in (AreaKind.TRAFFIC_ISLAND, AreaKind.PARK, AreaKind.MEADOW,
                                           AreaKind.GRASSLAND))
# Lower is more important (for tie-breaks): trunk 0 ... track 9.
_RANK = {int(c): i for i, c in enumerate((RoadClass.MOTORWAY, RoadClass.TRUNK, RoadClass.PRIMARY,
                                          RoadClass.SECONDARY, RoadClass.TERTIARY, RoadClass.UNCLASSIFIED,
                                          RoadClass.RESIDENTIAL, RoadClass.LIVING_STREET, RoadClass.SERVICE,
                                          RoadClass.ROAD, RoadClass.TRACK))}


@dataclass
class Junction:
    osm_node_id: int
    kind: JunctionKind
    x: float  # game metres
    z: float
    arms: int = 0
    flags: int = 0
    ring_diameter_m: float = 0.0
    island_diameter_m: float = 0.0
    island_area_osm_ref: int = 0
    name: NameRec | None = None


@dataclass
class Chowk:
    id: str
    name_en: str
    lon: float
    lat: float
    officers: str = "1"
    control: str = "P"


@dataclass
class ChowkConfig:
    snap_m: float = 120.0
    chowks: list[Chowk] = field(default_factory=list)


def load_chowks(path: Path | None = None) -> ChowkConfig:
    path = Path(path) if path is not None else CHOWKS_PATH
    if not path.exists():
        return ChowkConfig()
    data = yaml.safe_load(path.read_text(encoding="utf-8")) or {}
    out = ChowkConfig(snap_m=float(data.get("snap_m", 120.0)))
    for e in data.get("chowks", []) or []:
        lon, lat = (float(v) for v in e["at"])
        out.chowks.append(Chowk(str(e["id"]), str(e.get("name_en", "")), lon, lat, str(e.get("officers", "1")),
                                str(e.get("control", "P"))))
    return out


@dataclass
class _NodeInfo:
    arms: int = 0
    classes: list[int] = field(default_factory=list)
    ways: set[int] = field(default_factory=set)


def _node_table(roads) -> tuple[dict[int, _NodeInfo], dict[int, tuple[float, float]]]:
    """Motor-road arms per OSM node id, and lon/lat per node id."""
    info: dict[int, _NodeInfo] = defaultdict(_NodeInfo)
    pos: dict[int, tuple[float, float]] = {}
    for r in roads:
        if int(r.cls) not in MOTOR_CLASSES:
            continue
        ids = np.asarray(r.node_ids, dtype=np.int64)
        ll = np.asarray(r.lonlat, dtype=np.float64)
        n = len(ids)
        closed = n > 2 and ids[0] == ids[-1]
        for k, nid in enumerate(ids.tolist()):
            if nid <= 0:
                continue
            end = (k == 0 or k == n - 1) and not closed
            if closed and k == n - 1:
                continue
            ni = info[nid]
            ni.arms += 1 if end else 2
            ni.classes.append(int(r.cls))
            ni.ways.add(int(r.osm_id))
            pos.setdefault(nid, (float(ll[k, 0]), float(ll[k, 1])))
    return info, pos


def _rings(roads) -> list[tuple[list, bool]]:
    """Connected ring-member ways: ``[(roads, is_circular)]`` sorted by smallest node id."""
    members = [r for r in roads if (r.extra or {}).get("junction", "").strip().lower() in ("roundabout", "circular")]
    if not members:
        return []
    parent = list(range(len(members)))

    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i

    by_node: dict[int, int] = {}
    for i, r in enumerate(members):
        for nid in np.asarray(r.node_ids).tolist():
            if nid <= 0:
                continue
            if nid in by_node:
                a, b = find(i), find(by_node[nid])
                if a != b:
                    parent[max(a, b)] = min(a, b)
            else:
                by_node[nid] = i
    groups: dict[int, list] = defaultdict(list)
    for i, r in enumerate(members):
        groups[find(i)].append(r)
    out = []
    for g in groups.values():
        circ = any((r.extra or {}).get("junction", "").strip().lower() == "circular" for r in g)
        out.append((sorted(g, key=lambda r: int(r.osm_id)), circ))
    out.sort(key=lambda t: min(int(n) for r in t[0] for n in np.asarray(r.node_ids).tolist() if n > 0))
    return out


def find_junctions(roads, junction_nodes, areas, props, chowks: ChowkConfig, *, sacred=None,
                   to_game=None, area_geoms_game=None) -> tuple[list[Junction], dict]:
    """Region-wide junction list (module docstring). ``sacred`` is a game-space prepared geometry (or None);
    ``area_geoms_game`` the areas projected to game metres (same order as ``areas``)."""
    if to_game is None:
        def to_game(lon, lat):
            x, z = projection.lonlat_to_game(np.asarray(lon, dtype=np.float64), np.asarray(lat, dtype=np.float64))
            return np.asarray(x, dtype=np.float64), np.asarray(z, dtype=np.float64)
    stats: dict = {"rings": 0, "mini": 0, "signals": 0, "police": 0, "police_on_rings": 0, "synthetic_islands": 0,
                   "chowks_unmatched": []}
    info, pos = _node_table(roads)
    out: list[Junction] = []

    # Islands: candidate areas in game space.
    island_idx = [i for i, a in enumerate(areas) if int(a.kind) in ISLAND_KINDS]
    if area_geoms_game is None:
        geoms = []
        for i in island_idx:
            g = areas[i].polygon
            geoms.append(shapely.transform(g, lambda c: np.stack(to_game(c[:, 0], c[:, 1]), axis=1)))
    else:
        geoms = [area_geoms_game[i] for i in island_idx]
    isl = np.empty(len(geoms), dtype=object)
    isl[:] = geoms
    isl_tree = shapely.STRtree(isl) if len(isl) else None
    isl_pt = shapely.point_on_surface(isl) if len(isl) else np.zeros(0, dtype=object)
    isl_area = shapely.area(isl) if len(isl) else np.zeros(0)

    def island_near(x: float, z: float, radius: float) -> tuple[int, float, int]:
        if isl_tree is None:
            return 0, 0.0, -1
        cand = isl_tree.query(shapely.Point(x, z), predicate="dwithin", distance=radius)
        best = None
        for c in sorted(int(v) for v in cand):
            px, pz = shapely.get_x(isl_pt[c]), shapely.get_y(isl_pt[c])
            if math.hypot(px - x, pz - z) > radius:
                continue
            if best is None or isl_area[c] > isl_area[best]:
                best = c
        if best is None:
            return 0, 0.0, -1
        a = areas[island_idx[best]]
        ref = (int(a.osm_id) << 1) | (1 if a.osm_type == "r" else 0)
        return ref, 2.0 * math.sqrt(float(isl_area[best]) / math.pi), best

    # Rings.
    ring_recs: list[Junction] = []
    for members, circ in _rings(roads):
        ids, lls = [], []
        for r in members:
            ids.extend(np.asarray(r.node_ids).tolist())
            lls.extend(np.asarray(r.lonlat, dtype=np.float64).tolist())
        seen, uniq = set(), []
        for nid, ll in zip(ids, lls):
            if nid in seen:
                continue
            seen.add(nid)
            uniq.append(ll)
        u = np.asarray(uniq, dtype=np.float64)
        gx, gz = to_game(u[:, 0], u[:, 1])
        gx, gz = np.asarray(gx, dtype=np.float64), np.asarray(gz, dtype=np.float64)
        cx, cz = float(gx.mean()), float(gz.mean())
        closed_loop = all(len(np.asarray(r.node_ids)) > 0 for r in members) and _is_closed(members)
        if closed_loop:
            perim = 0.0
            for r in members:
                p = np.asarray(r.lonlat, dtype=np.float64)
                x, z = to_game(p[:, 0], p[:, 1])
                perim += float(np.hypot(np.diff(np.asarray(x)), np.diff(np.asarray(z))).sum())
            diam = perim / math.pi
        else:
            pts = np.stack([gx, gz], axis=1)
            d = np.hypot(pts[:, None, 0] - pts[None, :, 0], pts[:, None, 1] - pts[None, :, 1])
            diam = float(d.max()) if len(pts) > 1 else 0.0
        ring_ways = {int(r.osm_id) for r in members}
        touching = set()
        for nid in seen:
            if nid > 0 and nid in info:
                touching |= info[nid].ways - ring_ways
        j = Junction(min(n for n in seen if n > 0) if any(n > 0 for n in seen) else 0,
                     JunctionKind.CIRCULAR if circ else JunctionKind.ROUNDABOUT, cx, cz, arms=min(255, len(touching)),
                     ring_diameter_m=diam)
        ref, idiam, _ = island_near(cx, cz, 0.5 * diam)
        if ref:
            j.flags |= JunctionFlags.HAS_ISLAND_AREA
            j.island_area_osm_ref, j.island_diameter_m = ref, idiam
        name = next((r.name for r in members if r.name is not None and not r.name.is_empty()), None)
        j.name = name
        ring_recs.append(j)
    stats["rings"] = len(ring_recs)
    out.extend(ring_recs)

    # Mini roundabouts and signals.
    node_rec: dict[int, Junction] = {}
    for jn in junction_nodes:
        if jn.osm_id not in info:
            continue  # not on a motor road of the extract
        x, z = to_game(jn.lon, jn.lat)
        x, z = float(np.asarray(x)), float(np.asarray(z))
        if jn.kind == "mini_roundabout":
            j = Junction(jn.osm_id, JunctionKind.MINI_ROUNDABOUT, x, z, arms=min(255, info[jn.osm_id].arms))
            stats["mini"] += 1
        else:
            j = Junction(jn.osm_id, JunctionKind.SIGNALS, x, z, arms=min(255, info[jn.osm_id].arms),
                         flags=int(JunctionFlags.HAS_SIGNALS))
            stats["signals"] += 1
        node_rec[jn.osm_id] = j
        out.append(j)

    # Police chowks.
    nids = np.array(sorted(n for n, i in info.items() if i.arms >= 3), dtype=np.int64)
    if len(nids):
        nll = np.array([pos[int(n)] for n in nids], dtype=np.float64)
        nx_, nz_ = to_game(nll[:, 0], nll[:, 1])
        nx_, nz_ = np.asarray(nx_, dtype=np.float64), np.asarray(nz_, dtype=np.float64)
    for c in chowks.chowks:
        cx, cz = to_game(c.lon, c.lat)
        cx, cz = float(np.asarray(cx)), float(np.asarray(cz))
        flags = int(JunctionFlags.HAS_POLICE) | (int(JunctionFlags.OFFICERS_2_4) if c.officers == "2-4" else 0)
        ring = min(ring_recs, key=lambda j: math.hypot(j.x - cx, j.z - cz), default=None)
        if ring is not None and math.hypot(ring.x - cx, ring.z - cz) <= chowks.snap_m:
            ring.flags |= flags
            ring.name = NameRec(c.name_en, c.name_en, ring.name.ne if ring.name else "")
            stats["police_on_rings"] += 1
            continue
        if not len(nids):
            stats["chowks_unmatched"].append(c.id)
            continue
        d = np.hypot(nx_ - cx, nz_ - cz)
        near = np.flatnonzero(d <= chowks.snap_m)
        if not len(near):
            stats["chowks_unmatched"].append(c.id)
            continue
        best = min(near.tolist(), key=lambda k: (-info[int(nids[k])].arms,
                                                 min(_RANK.get(cl, 99) for cl in info[int(nids[k])].classes),
                                                 float(d[k]), int(nids[k])))
        nid = int(nids[best])
        ni = info[nid]
        x, z = float(nx_[best]), float(nz_[best])
        name = NameRec(c.name_en, c.name_en, "")
        if nid in node_rec:
            j = node_rec[nid]
            j.flags |= flags
            j.name = name
        else:
            major = any(cl in (int(RoadClass.TRUNK), int(RoadClass.PRIMARY), int(RoadClass.MOTORWAY))
                        for cl in ni.classes)
            ref, _d, _ = island_near(x, z, ISLAND_SEARCH_M)
            if ni.arms >= 4 and major and not ref:
                kind = JunctionKind.SYNTHETIC_ISLAND
                stats["synthetic_islands"] += 1
            else:
                kind = JunctionKind.POLICE
            j = Junction(nid, kind, x, z, arms=min(255, ni.arms), flags=flags, name=name)
            out.append(j)
            node_rec[nid] = j
        stats["police"] += 1

    # Zone and crossing flags.
    xs = np.array([j.x for j in out])
    zs = np.array([j.z for j in out])
    if len(out) and sacred is not None:
        inside = shapely.contains_xy(sacred, xs, zs)
        for j, ins in zip(out, inside.tolist()):
            if ins:
                j.flags |= JunctionFlags.HERITAGE_NO_MOTOR
    marked = [p for p in props if p.kind == ObjectKind.CROSSING_MARKED]
    if len(out) and marked:
        mx, mz = to_game(np.array([p.lon for p in marked]), np.array([p.lat for p in marked]))
        mx, mz = np.asarray(mx, dtype=np.float64), np.asarray(mz, dtype=np.float64)
        for j in out:
            reach = max(CROSSING_NEAR_M, 0.5 * j.ring_diameter_m + 15.0)
            if np.any(np.hypot(mx - j.x, mz - j.z) <= reach):
                j.flags |= JunctionFlags.CROSSINGS_MARKED
    stats["records"] = len(out)
    out.sort(key=lambda j: (j.osm_node_id, int(j.kind), j.x, j.z))
    return out, stats


def _is_closed(members) -> bool:
    """True when the ring members chain into one closed loop (every node id used an even number of times)."""
    cnt: dict[int, int] = defaultdict(int)
    for r in members:
        ids = np.asarray(r.node_ids).tolist()
        if len(ids) >= 2:
            cnt[ids[0]] += 1
            cnt[ids[-1]] += 1
    return bool(cnt) and all(v % 2 == 0 for v in cnt.values())
