"""Routes and turn restrictions: the region route file ``.ghrt`` (D2 + D22; DATA_FORMATS.md section 6).

``build_routes`` turns the extract's ``type=route`` relations into ordered,
oriented way chains with stops:

* **Ways**: the relation's way members with an empty, ``forward`` or
  ``backward`` role, in member order, that are roads of the extract (others
  set ``MISSING_WAYS``). Each way's direction comes from its connection to the
  next (or, for the last, the previous) way through shared end nodes; a member
  that does not connect starts a new run (``HAS_GAPS``). ``backward`` flips
  the result.
* **Length**: summed game-metre length of the chained ways.
* **Stops**: node members with a ``stop``/``platform`` role when the relation
  has any (``STOPS_FROM_MEMBERS``), else every ``BUS_STOP`` prop within
  ``stop_snap_m`` of the route (``STOPS_INFERRED``), ordered by their
  distance along the route; stops closer than ``min_stop_spacing_m`` along the
  route to the previous kept stop are dropped. First and last are TERMINAL.
* **Livery**: ``CITY_GREEN`` when the OSM operator contains a
  ``operator_city_green`` substring (the operator name is never written),
  ``COACH`` for buses that run more than ``coach_outside_km`` past the region
  box, else the mode default. Headways come from ``transit.yaml``.

Restrictions (``type=restriction``) whose ``from`` or ``to`` way is a road of
the region are stored as they are. The file is a pure function of the extract
and the config.
"""

from __future__ import annotations

import math
import struct
import unicodedata
import zlib
from dataclasses import dataclass, field
from pathlib import Path
from typing import Sequence

import numpy as np
import yaml

from . import projection
from . import tags as T
from .binio import Reader, Writer
from .config import CONFIG_DIR
from .model import (Extract, LiveryClass, NameRec, ObjectKind, RouteFlags, StopFlags, TransitMode,
                    TurnRestriction)

TRANSIT_PATH = CONFIG_DIR / "curated" / "transit.yaml"
GHRT_MAGIC = b"GHRT"
GHRT_VERSION = 1
GHRT_HEADER = struct.Struct("<4sHHIIIII4s")
assert GHRT_HEADER.size == 32
WAY_ROLES = frozenset({"", "forward", "backward", "route", "main"})
STOP_ROLES = frozenset({"stop", "stop_entry_only", "stop_exit_only", "platform", "platform_entry_only",
                        "platform_exit_only"})
STOP_DEDUP_M = 15.0


@dataclass
class TransitConfig:
    stop_snap_m: float = 25.0
    min_stop_spacing_m: float = 60.0
    headway_s: dict[int, tuple[int, int]] = field(default_factory=dict)
    city_green_ops: tuple[str, ...] = ("sajha",)
    mode_livery: dict[int, int] = field(default_factory=dict)
    coach_outside_km: float = 15.0


def load_transit(path: Path | None = None) -> TransitConfig:
    path = Path(path) if path is not None else TRANSIT_PATH
    if not path.exists():
        return TransitConfig()
    d = yaml.safe_load(path.read_text(encoding="utf-8")) or {}
    cfg = TransitConfig(stop_snap_m=float(d.get("stop_snap_m", 25)),
                        min_stop_spacing_m=float(d.get("min_stop_spacing_m", 60)))
    for k, v in (d.get("headway_s") or {}).items():
        cfg.headway_s[int(TransitMode[str(k)])] = (int(v[0]), int(v[1]))
    lv = d.get("livery") or {}
    cfg.city_green_ops = tuple(str(s).casefold() for s in lv.get("operator_city_green", ["sajha"]))
    for k, v in (lv.get("mode_default") or {}).items():
        cfg.mode_livery[int(TransitMode[str(k)])] = int(LiveryClass[str(v)])
    cfg.coach_outside_km = float(lv.get("coach_outside_km", 15))
    return cfg


@dataclass
class Stop:
    osm_ref: int  # (id << 2) | type; 0 = none
    flags: int
    x_dm: int
    z_dm: int
    along_dm: int
    name: NameRec | None = None


@dataclass
class Route:
    osm_id: int
    id: str
    mode: int
    livery: int
    flags: int
    name: NameRec | None
    ref: str
    from_name: str
    to_name: str
    headway_peak_s: int
    headway_off_s: int
    length_m: int
    ways: list[tuple[int, bool]]  # (way id, forward)
    stops: list[Stop]


@dataclass
class Restriction:
    osm_id: int
    kind: int
    from_way: int
    via_node: int
    via_way: int
    to_way: int


@dataclass
class RouteSet:
    routes: list[Route] = field(default_factory=list)
    restrictions: list[Restriction] = field(default_factory=list)


def _game(ll: np.ndarray) -> np.ndarray:
    x, z = projection.lonlat_to_game(ll[:, 0], ll[:, 1])
    return np.stack([np.asarray(x, dtype=np.float64), np.asarray(z, dtype=np.float64)], axis=1)


def _chain(ways: list[tuple[int, str]], roads: dict) -> tuple[list[tuple[int, bool]], bool]:
    """Oriented way chain and whether it has gaps."""
    out: list[tuple[int, bool]] = []
    gaps = False
    n = len(ways)
    for k, (wid, role) in enumerate(ways):
        r = roads[wid]
        a, b = int(r.node_ids[0]), int(r.node_ids[-1])
        fwd = True
        if k + 1 < n:
            nr = roads[ways[k + 1][0]]
            na, nb = int(nr.node_ids[0]), int(nr.node_ids[-1])
            if b in (na, nb):
                fwd = True
            elif a in (na, nb):
                fwd = False
            elif k > 0:
                fwd = _prev_fwd(out, roads, a, b)
            else:
                gaps = True
        elif k > 0:
            fwd = _prev_fwd(out, roads, a, b)
        if k > 0:
            pw, pf = out[-1]
            pr = roads[pw]
            pend = int(pr.node_ids[-1]) if pf else int(pr.node_ids[0])
            if (a if fwd else b) != pend:
                gaps = True
        if role == "backward":
            fwd = not fwd
        out.append((wid, fwd))
    return out, gaps


def _prev_fwd(out, roads, a: int, b: int) -> bool:
    pw, pf = out[-1]
    pr = roads[pw]
    pend = int(pr.node_ids[-1]) if pf else int(pr.node_ids[0])
    return a == pend or b != pend


def _polyline(chain: list[tuple[int, bool]], roads: dict) -> np.ndarray:
    parts = []
    for wid, fwd in chain:
        ll = np.asarray(roads[wid].lonlat, dtype=np.float64)
        if int(roads[wid].oneway) < 0:
            pass  # lonlat is in OSM order; chain orientation is relative to OSM order
        parts.append(ll if fwd else ll[::-1])
    return _game(np.concatenate(parts)) if parts else np.zeros((0, 2))


def _project(poly: np.ndarray, pts: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    """(distance along, distance off) of points on a polyline (game metres)."""
    if len(poly) < 2 or not len(pts):
        return np.zeros(len(pts)), np.full(len(pts), np.inf)
    a, b = poly[:-1], poly[1:]
    ab = b - a
    ln = np.hypot(ab[:, 0], ab[:, 1])
    cum = np.concatenate([[0.0], np.cumsum(ln)])[:-1]
    along = np.zeros(len(pts))
    off = np.full(len(pts), np.inf)
    for i, p in enumerate(pts):
        t = np.clip(np.einsum("ij,ij->i", p - a, ab) / np.where(ln > 0, ln * ln, 1.0), 0.0, 1.0)
        q = a + ab * t[:, None]
        d = np.hypot(*(q - p).T)
        k = int(np.argmin(d))
        along[i] = cum[k] + t[k] * ln[k]
        off[i] = d[k]
    return along, off


def build_routes(extract: Extract, cfg: TransitConfig, bbox_lonlat: Sequence[float] | None = None
                 ) -> tuple[RouteSet, dict]:
    roads = {int(r.osm_id): r for r in extract.roads}
    stats: dict = {"routes": 0, "by_mode": {}, "stops_from_members": 0, "stops_inferred": 0, "with_gaps": 0,
                   "restrictions": 0}
    stops_all = [p for p in extract.props if p.kind == ObjectKind.BUS_STOP]
    stop_xy = _game(np.array([[p.lon, p.lat] for p in stops_all], dtype=np.float64)) if stops_all \
        else np.zeros((0, 2))
    out = RouteSet()
    for rf in extract.routes:
        mode = T.route_mode({**rf.tags, "route": rf.route})
        if mode == TransitMode.NONE:
            continue
        flags = 0
        ways = [(m.ref, m.role.strip().lower()) for m in rf.members if m.type == "w"
                and m.role.strip().lower() in WAY_ROLES]
        have = [(w, role) for w, role in ways if w in roads and len(roads[w].node_ids) >= 2]
        if len(have) < len(ways):
            flags |= RouteFlags.MISSING_WAYS
        if not have:
            continue
        chain, gaps = _chain(have, roads)
        if gaps:
            flags |= RouteFlags.HAS_GAPS
            stats["with_gaps"] += 1
        if (rf.tags.get("roundtrip") or "").lower() == "yes":
            flags |= RouteFlags.ROUNDTRIP
        poly = _polyline(chain, roads)
        seg = np.hypot(*np.diff(poly, axis=0).T) if len(poly) > 1 else np.zeros(0)
        length = float(seg.sum())

        stops: list[Stop] = []
        member_stops = [m for m in rf.members if m.type == "n" and m.role.strip().lower() in STOP_ROLES
                        and m.lon is not None]
        if member_stops:
            flags |= RouteFlags.STOPS_FROM_MEMBERS
            xy = _game(np.array([[m.lon, m.lat] for m in member_stops], dtype=np.float64))
            along, _off = _project(poly, xy)
            cands = [(float(along[i]), xy[i], (m.ref << 2), int(StopFlags.FROM_MEMBER), m.name)
                     for i, m in enumerate(member_stops)]
            stats["stops_from_members"] += len(cands)
            cands.sort(key=lambda c: (c[0], c[2]))
            min_gap = STOP_DEDUP_M
        elif mode in (TransitMode.BUS, TransitMode.MICROBUS, TransitMode.TEMPO, TransitMode.SHARE_TAXI) \
                and len(stop_xy):
            along, off = _project(poly, stop_xy)
            near = np.flatnonzero(off <= cfg.stop_snap_m)
            cands = [(float(along[i]), stop_xy[i], (stops_all[i].osm_id << 2) | (1 if stops_all[i].osm_type == "w"
                                                                                 else 0),
                      int(StopFlags.INFERRED), stops_all[i].name) for i in near.tolist()]
            if cands:
                flags |= RouteFlags.STOPS_INFERRED
            stats["stops_inferred"] += len(cands)
            cands.sort(key=lambda c: (c[0], c[2]))
            min_gap = cfg.min_stop_spacing_m
        else:
            cands, min_gap = [], STOP_DEDUP_M
        last = -math.inf
        for along_m, xy, ref, sflags, name in cands:
            if along_m - last < min_gap:
                continue
            stops.append(Stop(ref, sflags, int(round(xy[0] * 10.0)), int(round(xy[1] * 10.0)),
                              int(round(along_m * 10.0)), name))
            last = along_m
        if stops:
            stops[0].flags |= StopFlags.TERMINAL
            stops[-1].flags |= StopFlags.TERMINAL

        livery = cfg.mode_livery.get(int(mode), int(LiveryClass.NONE))
        op = (rf.tags.get("operator") or "").casefold()
        if mode == TransitMode.BUS and any(s in op for s in cfg.city_green_ops):
            livery = int(LiveryClass.CITY_GREEN)
        elif mode == TransitMode.BUS and bbox_lonlat is not None and _outside_km(chain, roads, bbox_lonlat) \
                > cfg.coach_outside_km:
            livery = int(LiveryClass.COACH)
        hw = cfg.headway_s.get(int(mode), (0, 0))
        name = T.parse_name(rf.tags)
        out.routes.append(Route(
            osm_id=int(rf.osm_id), id=f"{TransitMode(mode).name.lower()}.r{rf.osm_id}", mode=int(mode),
            livery=int(livery), flags=int(flags), name=name, ref=_nfc(rf.tags.get("ref")),
            from_name=_nfc(rf.tags.get("from")), to_name=_nfc(rf.tags.get("to")), headway_peak_s=hw[0],
            headway_off_s=hw[1], length_m=int(round(length)), ways=chain, stops=stops))
        stats["routes"] += 1
        stats["by_mode"][TransitMode(mode).name] = stats["by_mode"].get(TransitMode(mode).name, 0) + 1
    for rs in extract.restrictions:
        if rs.kind == TurnRestriction.NONE:
            continue
        out.restrictions.append(Restriction(int(rs.osm_id), int(rs.kind), int(rs.from_way), int(rs.via_node),
                                            int(rs.via_way), int(rs.to_way)))
    stats["restrictions"] = len(out.restrictions)
    out.routes.sort(key=lambda r: r.osm_id)
    out.restrictions.sort(key=lambda r: r.osm_id)
    return out, stats


def bus_way_ids(rs: RouteSet) -> set[int]:
    """Way ids used by bus, microbus, tempo and share-taxi routes (RATR BUS_ROUTE)."""
    modes = {int(TransitMode.BUS), int(TransitMode.MICROBUS), int(TransitMode.TEMPO), int(TransitMode.SHARE_TAXI)}
    return {w for r in rs.routes if r.mode in modes for w, _ in r.ways}


def _outside_km(chain, roads, bbox) -> float:
    worst = 0.0
    for wid, _ in chain:
        ll = np.asarray(roads[wid].lonlat, dtype=np.float64)
        lat = float(ll[:, 1].mean())
        k = 111.32
        dx = np.maximum(np.maximum(bbox[0] - ll[:, 0], ll[:, 0] - bbox[2]), 0.0) * k * math.cos(math.radians(lat))
        dy = np.maximum(np.maximum(bbox[1] - ll[:, 1], ll[:, 1] - bbox[3]), 0.0) * k
        worst = max(worst, float(np.hypot(dx, dy).max()))
    return worst


def _nfc(s) -> str:
    return unicodedata.normalize("NFC", str(s or ""))


# ---------------------------------------------------------------------------
# Binary
# ---------------------------------------------------------------------------
class _Names:
    def __init__(self) -> None:
        self.index: dict[tuple[str, str, str], int] = {}
        self.entries: list[tuple[str, str, str]] = []

    def ref(self, n: NameRec | None) -> int:
        if n is None:
            return 0
        e = (_nfc(n.default), _nfc(n.en), _nfc(n.ne))
        if not any(e):
            return 0
        k = self.index.get(e)
        if k is None:
            self.entries.append(e)
            k = self.index[e] = len(self.entries)
        return k

    def ref_str(self, s: str) -> int:
        return self.ref(NameRec(s, "", "")) if s else 0


def encode_ghrt(rs: RouteSet) -> bytes:
    names = _Names()
    body = Writer()
    for r in rs.routes:
        nref, rref, fref, tref = (names.ref(r.name), names.ref_str(r.ref), names.ref_str(r.from_name),
                                  names.ref_str(r.to_name))
        (body.varint(r.osm_id).str(r.id).u8(r.mode).u8(r.livery).u8(r.flags).varint(nref).varint(rref)
         .varint(fref).varint(tref).u16(min(65535, r.headway_peak_s)).u16(min(65535, r.headway_off_s))
         .varint(r.length_m).varint(len(r.ways)))
        for wid, fwd in r.ways:
            body.varint((wid << 1) | (1 if fwd else 0))
        body.varint(len(r.stops))
        for s in r.stops:
            body.varint(s.osm_ref).u8(s.flags).svarint(s.x_dm).svarint(s.z_dm).varint(s.along_dm)
            body.varint(names.ref(s.name))
    for x in rs.restrictions:
        body.varint(x.osm_id).u8(x.kind).varint(x.from_way).varint(x.via_node).varint(x.via_way).varint(x.to_way)
    nw = Writer().varint(len(names.entries))
    for e in names.entries:
        nw.str(e[0]).str(e[1]).str(e[2])
    payload = nw.bytes() + body.bytes()
    head = GHRT_HEADER.pack(GHRT_MAGIC, GHRT_VERSION, 0, len(rs.routes), len(rs.restrictions), len(names.entries),
                            len(payload), zlib.crc32(payload) & 0xFFFFFFFF, b"\0" * 4)
    return head + payload


def decode_ghrt(data: bytes) -> tuple[RouteSet, list[tuple[str, str, str]]]:
    if len(data) < GHRT_HEADER.size:
        raise ValueError("ghrt shorter than its header")
    magic, version, _f, n_routes, n_restr, n_names, nbytes, crc, _ = GHRT_HEADER.unpack_from(data, 0)
    if magic != GHRT_MAGIC:
        raise ValueError(f"bad ghrt magic {magic!r}")
    if version != GHRT_VERSION:
        raise ValueError(f"unsupported ghrt version {version}")
    payload = bytes(memoryview(data)[GHRT_HEADER.size:])
    if len(payload) != nbytes or zlib.crc32(payload) & 0xFFFFFFFF != crc:
        raise ValueError("ghrt payload size or CRC mismatch")
    r = Reader(payload)
    if r.varint() != n_names:
        raise ValueError("ghrt name count mismatch")
    names = [(r.str(), r.str(), r.str()) for _ in range(n_names)]

    def nm(k: int) -> NameRec | None:
        if k == 0:
            return None
        if k > len(names):
            raise ValueError("ghrt name ref out of range")
        return NameRec(*names[k - 1])

    rs = RouteSet()
    for _ in range(n_routes):
        osm_id, rid = r.varint(), r.str()
        mode, livery, flags = r.u8(), r.u8(), r.u8()
        nref, rref, fref, tref = r.varint(), r.varint(), r.varint(), r.varint()
        hp, ho, length = r.u16(), r.u16(), r.varint()
        ways = []
        for _ in range(r.varint()):
            v = r.varint()
            ways.append((v >> 1, bool(v & 1)))
        stops = []
        for _ in range(r.varint()):
            s = Stop(r.varint(), r.u8(), r.svarint(), r.svarint(), r.varint())
            s.name = nm(r.varint())
            stops.append(s)
        rs.routes.append(Route(osm_id, rid, mode, livery, flags, nm(nref), (nm(rref) or NameRec()).default,
                               (nm(fref) or NameRec()).default, (nm(tref) or NameRec()).default, hp, ho, length,
                               ways, stops))
    for _ in range(n_restr):
        rs.restrictions.append(Restriction(r.varint(), r.u8(), r.varint(), r.varint(), r.varint(), r.varint()))
    if r.remaining():
        raise ValueError("trailing bytes in ghrt")
    return rs, names


def write_ghrt(path: Path, rs: RouteSet) -> dict:
    data = encode_ghrt(rs)
    Path(path).write_bytes(data)
    return {"routes": len(rs.routes), "restrictions": len(rs.restrictions), "bytes": len(data)}


def routes_json(rs: RouteSet) -> dict:
    def n(x: NameRec | None):
        return None if x is None else [x.default, x.en, x.ne]

    return {"routes": [{
        "osm_id": r.osm_id, "id": r.id, "mode": r.mode, "mode_name": TransitMode(r.mode).name, "livery": r.livery,
        "livery_name": LiveryClass(r.livery).name, "flags": r.flags, "name": n(r.name), "ref": r.ref,
        "from": r.from_name, "to": r.to_name, "headway_peak_s": r.headway_peak_s, "headway_off_s": r.headway_off_s,
        "length_m": r.length_m, "way_ids": [w for w, _ in r.ways], "way_forward": [f for _, f in r.ways],
        "stops": [{"osm_ref": s.osm_ref, "flags": s.flags, "x_dm": s.x_dm, "z_dm": s.z_dm, "along_dm": s.along_dm,
                   "name": n(s.name)} for s in r.stops]} for r in rs.routes],
        "restrictions": [{"osm_id": x.osm_id, "kind": x.kind, "from_way": x.from_way, "via_node": x.via_node,
                          "via_way": x.via_way, "to_way": x.to_way} for x in rs.restrictions]}
