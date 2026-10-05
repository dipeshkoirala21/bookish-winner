"""Curated DB: heritage (hero) records, hide zones and the ``.ghcd`` region file (D5 + D12).

``load_heritage`` reads ``config/curated/heritage_sites.yaml``. ``resolve``
binds every record to the region's extract:

* **anchor**: the OSM object (``osm``) or the curated ``manual`` lon/lat. A
  way or relation anchor that is a building footprint is the hero's plan
  (``footprint_ref``). A node anchor inside a building footprint takes that
  footprint as its plan when the footprint is no larger than
  ``NODE_PLAN_FACTOR`` x the curated ``plan_m`` area (or ``NODE_PLAN_MAX_M2``
  without a plan), so a gate node in a palace block never hides the palace.
* **hide zone** (D5): the plan footprint plus every ``hide`` ref footprint.
  A node or manual anchor without a footprint uses its curated ``plan_m``
  rectangle (centred on the anchor, turned to ``yaw_deg``) instead.
  Every building or part whose centroid lies inside the zone, or that
  overlaps it by at least ``HIDE_OVERLAP_SHARE`` of its own area, gets the
  ``LANDMARK`` flag (hidden: the hero replica stands there). A building that
  only clips a plan rectangle has the rectangle cut out of its footprint
  (``cut_buildings``: a tower standing in a palace wing). Overlaps that
  remain are counted (``unhidden_overlaps``, the G7 check), and records with
  no hide zone at all are listed (``no_hide_zone``).
* **archetype override**: the plan footprint gets the archetype of the
  record's kind (stupa STUPA, pagoda TEMPLE_PAGODA, shikhara TEMPLE_SHIKHARA,
  palace RANA_PALACE ...).

Records whose anchor lies outside ``bbox_lonlat`` (the region box) are skipped (``outside``), and so
are records whose OSM anchor is not in the extract (``not_in_extract``: another region's heroes).
``encode_ghcd`` / ``decode_ghcd`` implement the binary file
(DATA_FORMATS.md section 7); ``hero_recipes_json`` writes the same records as
JSON for the temporary Track C bridge (W2_DESIGN 9.4).
"""

from __future__ import annotations

import json
import math
import struct
import unicodedata
import zlib
from dataclasses import dataclass, field
from pathlib import Path
from typing import Sequence

import numpy as np
import shapely
import yaml
from shapely.geometry import Polygon
from shapely.geometry.polygon import orient

from . import projection
from .binio import Reader, Writer
from .config import CONFIG_DIR
from .model import (BuildingArchetype, BuildingFeature, BuildingFlags, EntryRule, Extract, HeritageFinish,
                    HeritageFlags, HeritageKind, KoraDirection, NameRec)
from .projection import TileId

HERITAGE_PATH = CONFIG_DIR / "curated" / "heritage_sites.yaml"
GHCD_MAGIC = b"GHCD"
GHCD_VERSION = 1
GHCD_HEADER = struct.Struct("<4sHHIII12s")
assert GHCD_HEADER.size == 32
YAW_UNKNOWN = 0xFFFF
NODE_PLAN_FACTOR = 4.0
NODE_PLAN_MAX_M2 = 900.0
HIDE_OVERLAP_SHARE = 0.3
HIDE_BUFFER_M = 0.3

KIND_ARCHETYPE = {
    HeritageKind.PAGODA: BuildingArchetype.TEMPLE_PAGODA, HeritageKind.MANDAPA: BuildingArchetype.TEMPLE_PAGODA,
    HeritageKind.SHIKHARA_STONE: BuildingArchetype.TEMPLE_SHIKHARA,
    HeritageKind.SHIKHARA_PLASTER: BuildingArchetype.TEMPLE_SHIKHARA, HeritageKind.STUPA: BuildingArchetype.STUPA,
    HeritageKind.GOMPA: BuildingArchetype.GOMPA, HeritageKind.PALACE: BuildingArchetype.RANA_PALACE,
    HeritageKind.HOUSE_TEMPLE: BuildingArchetype.NEWAR, HeritageKind.BAHAL: BuildingArchetype.NEWAR,
}


def load_heritage(path: Path | None = None, stage_max: int = 1) -> list[dict]:
    """Records of ``heritage_sites.yaml`` up to ``stage_max``, defaults applied, sorted by id."""
    path = Path(path) if path is not None else HERITAGE_PATH
    if not path.exists():
        return []
    data = yaml.safe_load(path.read_text(encoding="utf-8")) or {}
    defaults = data.get("defaults") or {}
    out, seen = [], set()
    for e in data.get("sites", []) or []:
        r = {**defaults, **e}
        rid = str(r["id"])
        if rid in seen:
            raise ValueError(f"{path}: duplicate heritage id {rid!r}")
        seen.add(rid)
        HeritageKind[str(r["kind"])]
        if not (r.get("osm") or r.get("manual")):
            raise ValueError(f"{path}: {rid} needs osm or manual")
        if int(r.get("stage", 1)) <= stage_max:
            out.append(r)
    return sorted(out, key=lambda r: r["id"])


def _ref(s: str | None) -> tuple[str, int] | None:
    if not s:
        return None
    s = str(s)
    if len(s) < 2 or s[0] not in "nwr" or not s[1:].isdigit():
        raise ValueError(f"bad OSM ref {s!r}")
    return s[0], int(s[1:])


def anchor_node_ids(records: Sequence[dict]) -> set[int]:
    """Node ids the extract must locate (node anchors)."""
    out = set()
    for r in records:
        a = _ref(r.get("osm"))
        if a and a[0] == "n":
            out.add(a[1])
    return out


@dataclass
class Resolved:
    rec: dict
    x: float = 0.0  # anchor, game metres
    z: float = 0.0
    lon: float = 0.0
    lat: float = 0.0
    anchor_ref: int = 0  # (id << 2) | type
    footprint: int = -1  # building index of the plan
    compound_ref: int = 0  # (id << 1) | is_relation
    hidden: list[int] = field(default_factory=list)  # building indices hidden by this hero
    name_ne: str = ""
    flags: int = 0


def _osm_ref_nwr(t: str, i: int) -> int:
    return (i << 2) | {"n": 0, "w": 1, "r": 2}[t]


def _wr(t: str, i: int) -> int:
    return (i << 1) | (1 if t == "r" else 0)


def _poly(b: BuildingFeature) -> Polygon:
    return Polygon(b.outer, [h for h in b.holes if len(h) >= 3])


def _area_m2(poly) -> float:
    if poly.is_empty:
        return 0.0
    lat = poly.representative_point().y
    return float(poly.area) * (111_320.0 ** 2) * math.cos(math.radians(lat))


def _plan_rect(lon: float, lat: float, r: dict):
    """The curated ``plan_m`` [w, d] rectangle centred on (lon, lat), its depth along the door bearing
    ``yaw_deg`` (180 when unknown, as HeroBuilder), in lon/lat; None without a plan."""
    plan = [float(v) for v in str((r.get("attrs") or {}).get("plan_m", "")).split(",") if v.strip()]
    if len(plan) != 2 or plan[0] <= 0 or plan[1] <= 0:
        return None
    yaw = math.radians(float(r["yaw_deg"]) if r.get("yaw_deg") is not None else 180.0)
    fx, fz = math.sin(yaw), math.cos(yaw)  # facing (east, north)
    rx, rz = fz, -fx
    kx = math.cos(math.radians(lat)) * 111_320.0
    hw, hd = plan[0] / 2.0, plan[1] / 2.0
    pts = []
    for sw, sd in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        e = sw * hw * rx + sd * hd * fx
        n = sw * hw * rz + sd * hd * fz
        pts.append((lon + e / kx, lat + n / 111_320.0))
    return orient(Polygon(pts), 1.0)


def _cut(b: BuildingFeature, poly, zone):
    """``b`` minus ``zone`` when that leaves one valid polygon with no new courtyard (written back into ``b``),
    else None: a plan wholly inside a block is a data error to report, not a hole to punch."""
    rest = shapely.make_valid(shapely.difference(poly, zone))
    if rest.geom_type != "Polygon" or rest.is_empty or _area_m2(rest) < 1.0 \
            or len(rest.interiors) > len(poly.interiors):
        return None
    rest = orient(rest, 1.0)
    b.outer = np.asarray(rest.exterior.coords[:-1], dtype=np.float64)
    b.holes = [np.asarray(i.coords[:-1], dtype=np.float64) for i in rest.interiors if len(i.coords) >= 4]
    return rest


def resolve(records: Sequence[dict], extract: Extract, node_pos: dict[int, tuple[float, float, NameRec | None]],
            bbox_lonlat: Sequence[float] | None = None) -> tuple[list[Resolved], dict[int, BuildingArchetype], dict]:
    """Bind records to the extract (module docstring). Sets LANDMARK on hidden buildings (in place)
    and returns ``(resolved, archetype overrides by building index, stats)``."""
    stats = {"records": len(records), "resolved": 0, "not_in_extract": [], "outside": [], "hidden_buildings": 0,
             "unhidden_overlaps": 0, "cut_buildings": 0, "no_hide_zone": [], "compound_missing": []}
    bidx = {(b.osm_type, int(b.osm_id)): i for i, b in enumerate(extract.buildings)}
    areas = {(a.osm_type, int(a.osm_id)): a for a in extract.areas}
    polys = np.empty(len(extract.buildings), dtype=object)
    polys[:] = [_poly(b) if len(b.outer) >= 3 else Polygon() for b in extract.buildings]
    ok = shapely.is_valid(polys)
    if not ok.all():
        polys[~ok] = shapely.make_valid(polys[~ok])
    tree = shapely.STRtree(polys) if len(polys) else None
    poi_pos = {(p.osm_type, int(p.osm_id)): (p.lon, p.lat, p.name) for p in extract.pois}
    out: list[Resolved] = []
    overrides: dict[int, BuildingArchetype] = {}
    for r in records:
        res = Resolved(rec=r, flags=int(HeritageFlags.SANCTUM_CLOSED))
        a = _ref(r.get("osm"))
        name = None
        if a is None:
            m = r["manual"]
            res.lon, res.lat = float(m["lon"]), float(m["lat"])
            res.flags |= HeritageFlags.MANUAL_POSITION
        elif a[0] == "n":
            hit = node_pos.get(a[1]) or poi_pos.get(a)
            if hit is None:
                stats["not_in_extract"].append(r["id"])
                continue
            res.lon, res.lat, name = float(hit[0]), float(hit[1]), hit[2]
            res.anchor_ref = _osm_ref_nwr(*a)
        else:
            res.anchor_ref = _osm_ref_nwr(*a)
            if a in bidx:
                b = extract.buildings[bidx[a]]
                res.footprint = bidx[a]
                pt = polys[res.footprint].representative_point() if not polys[res.footprint].is_empty \
                    else shapely.Point(b.outer[0])
                res.lon, res.lat, name = float(pt.x), float(pt.y), b.name
            elif a in areas:
                pt = areas[a].polygon.representative_point()
                res.lon, res.lat, name = float(pt.x), float(pt.y), areas[a].name
            elif a in poi_pos:
                res.lon, res.lat, name = poi_pos[a][0], poi_pos[a][1], poi_pos[a][2]
            else:
                stats["not_in_extract"].append(r["id"])
                continue
        if bbox_lonlat is not None and not (bbox_lonlat[0] <= res.lon <= bbox_lonlat[2]
                                            and bbox_lonlat[1] <= res.lat <= bbox_lonlat[3]):
            stats["outside"].append(r["id"])
            continue
        x, z = projection.lonlat_to_game(res.lon, res.lat)
        res.x, res.z = float(x), float(z)
        if name is not None and name.ne:
            res.name_ne = unicodedata.normalize("NFC", name.ne)
        elif name is not None and name.default and any("ऀ" <= ch <= "ॿ" for ch in name.default):
            res.name_ne = unicodedata.normalize("NFC", name.default)

        # Node anchor inside a footprint: that footprint is the plan (when it is not a whole palace block).
        if res.footprint < 0 and a is not None and a[0] == "n" and tree is not None:
            cand = tree.query(shapely.Point(res.lon, res.lat), predicate="within")
            cand = [int(c) for c in cand if not (extract.buildings[int(c)].flags & BuildingFlags.PART)]
            if cand:
                c = min(cand, key=lambda k: (_area_m2(polys[k]), k))
                plan = [float(v) for v in str((r.get("attrs") or {}).get("plan_m", "")).split(",") if v.strip()]
                limit = NODE_PLAN_FACTOR * plan[0] * plan[1] if len(plan) == 2 else NODE_PLAN_MAX_M2
                if _area_m2(polys[c]) <= limit:
                    res.footprint = c

        # Compound.
        c = _ref(r.get("compound"))
        if c is not None:
            res.compound_ref = _wr(*c) if c[0] in "wr" else 0
            res.flags |= HeritageFlags.HAS_COMPOUND | HeritageFlags.WALKABLE_COMPOUND | HeritageFlags.NO_VEHICLES
            if c not in areas:
                stats["compound_missing"].append(r["id"])
        if r.get("verify"):
            res.flags |= HeritageFlags.VERIFY

        # Hide zone and overrides.
        zone_parts = []
        if res.footprint >= 0:
            zone_parts.append(polys[res.footprint])
            arch = KIND_ARCHETYPE.get(HeritageKind[str(r["kind"])])
            if arch is not None:
                overrides[res.footprint] = arch
        rect = None
        if res.footprint < 0 and (a is None or a[0] == "n"):
            rect = _plan_rect(res.lon, res.lat, r)
            if rect is not None:
                zone_parts.append(rect)
        for h in r.get("hide") or []:
            hr = _ref(h)
            if hr in bidx:
                zone_parts.append(polys[bidx[hr]])
        if not zone_parts:
            stats["no_hide_zone"].append(r["id"])
        if zone_parts and tree is not None:
            k = math.cos(math.radians(res.lat)) * 111_320.0
            zone = shapely.buffer(shapely.union_all(zone_parts), HIDE_BUFFER_M / k)
            cand = sorted(int(v) for v in tree.query(zone, predicate="intersects"))
            for ci in cand:
                p = polys[ci]
                # With a plan rectangle, a block far bigger than the plan is never hidden (a tower in a palace
                # wing), and a ring-shaped wing (a chowk) whose centroid lies in its courtyard is not "inside".
                big = rect is not None and _area_m2(p) > NODE_PLAN_FACTOR * _area_m2(rect)
                inside = not big and (zone.contains(p.representative_point()) or (
                    zone.contains(p.centroid) and (rect is None or p.contains(p.centroid))))
                share = float(shapely.area(shapely.intersection(p, zone))) / max(float(p.area), 1e-18)
                if inside or share >= HIDE_OVERLAP_SHARE:
                    res.hidden.append(ci)
                elif share > 0.0 and _area_m2(shapely.intersection(p, zone)) > 0.5:
                    cut = _cut(extract.buildings[ci], p, zone) if rect is not None else None
                    if cut is not None:
                        polys[ci] = cut
                        stats["cut_buildings"] += 1
                    else:
                        stats["unhidden_overlaps"] += 1
            for ci in res.hidden:
                b = extract.buildings[ci]
                b.flags = BuildingFlags(b.flags) | BuildingFlags.LANDMARK
            stats["hidden_buildings"] += len(res.hidden)
        out.append(res)
        stats["resolved"] += 1
    return out, overrides, stats


# ---------------------------------------------------------------------------
# .ghcd
# ---------------------------------------------------------------------------
def _yaw_cdeg(r: dict) -> int:
    y = r.get("yaw_deg")
    if y is None:
        return YAW_UNKNOWN
    return int(round(float(y) % 360.0 * 100.0)) % 36000


def _nfc(s) -> str:
    return unicodedata.normalize("NFC", str(s or ""))


@dataclass
class HeritageRec:
    """One decoded ``.ghcd`` heritage record (DATA_FORMATS.md 7)."""

    id: str
    kind: int
    stage: int
    flags: int
    finish: int
    tiers: int
    plinth_levels: int
    doors: int
    entry_rule: int
    kora: int
    yaw_cdeg: int
    height_cm: int
    name_en: str
    name_ne: str
    deity: str
    anchor_ref: int
    footprint_ref: int
    compound_ref: int
    anchor_tile: int
    x_cm: int
    z_cm: int
    hidden: list[int]
    attrs: list[tuple[str, str]]
    provenance: str
    review: str


def to_records(resolved: Sequence[Resolved], extract: Extract, leaf_level: int) -> list[HeritageRec]:
    out = []
    for res in resolved:
        r = res.rec
        attrs = {str(k): _nfc(v) for k, v in (r.get("attrs") or {}).items()}
        if r.get("verify"):
            attrs["verify"] = ",".join(str(v) for v in r["verify"])
        b = extract.buildings[res.footprint] if res.footprint >= 0 else None
        tile = projection.tile_at(leaf_level, res.x, res.z)
        out.append(HeritageRec(
            id=_nfc(r["id"]), kind=int(HeritageKind[str(r["kind"])]), stage=int(r.get("stage", 1)), flags=res.flags,
            finish=int(HeritageFinish[str(r.get("finish", "UNKNOWN"))]), tiers=int(r.get("tiers", 0) or 0),
            plinth_levels=int(r.get("plinth_levels", 0) or 0), doors=int(r.get("doors", 0) or 0),
            entry_rule=int(EntryRule[str(r.get("entry_rule", "NONE"))]),
            kora=int(KoraDirection[str(r.get("kora", "NONE"))]), yaw_cdeg=_yaw_cdeg(r),
            height_cm=int(round(float(r.get("height_m", 0) or 0) * 100.0)), name_en=_nfc(r.get("name_en")),
            name_ne=res.name_ne, deity=_nfc(r.get("deity")), anchor_ref=res.anchor_ref,
            footprint_ref=_wr(b.osm_type, int(b.osm_id)) if b is not None else 0, compound_ref=res.compound_ref,
            anchor_tile=tile.key, x_cm=int(round(res.x * 100.0)), z_cm=int(round(res.z * 100.0)),
            hidden=sorted({_wr(extract.buildings[i].osm_type, int(extract.buildings[i].osm_id)) for i in res.hidden}),
            attrs=sorted(attrs.items()), provenance=_nfc(r.get("provenance")), review=_nfc(r.get("review"))))
    return sorted(out, key=lambda h: h.id)


def encode_ghcd(records: Sequence[HeritageRec]) -> bytes:
    w = Writer()
    for h in sorted(records, key=lambda h: h.id):
        (w.str(h.id).u8(h.kind).u8(h.stage).u8(h.flags).u8(h.finish).u8(h.tiers).u8(h.plinth_levels).u8(h.doors)
         .u8(h.entry_rule).u8(h.kora).u16(h.yaw_cdeg).varint(h.height_cm).str(h.name_en).str(h.name_ne)
         .str(h.deity).varint(h.anchor_ref).varint(h.footprint_ref).varint(h.compound_ref).u64(h.anchor_tile)
         .svarint(h.x_cm).svarint(h.z_cm).varint(len(h.hidden)))
        for ref in h.hidden:
            w.varint(ref)
        w.varint(len(h.attrs))
        for k, v in h.attrs:
            w.str(k).str(v)
        w.str(h.provenance).str(h.review)
    payload = w.bytes()
    head = GHCD_HEADER.pack(GHCD_MAGIC, GHCD_VERSION, 0, len(records), len(payload),
                            zlib.crc32(payload) & 0xFFFFFFFF, b"\0" * 12)
    return head + payload


def decode_ghcd(data: bytes) -> list[HeritageRec]:
    if len(data) < GHCD_HEADER.size:
        raise ValueError("ghcd shorter than its header")
    magic, version, _flags, count, nbytes, crc, _ = GHCD_HEADER.unpack_from(data, 0)
    if magic != GHCD_MAGIC:
        raise ValueError(f"bad ghcd magic {magic!r}")
    if version != GHCD_VERSION:
        raise ValueError(f"unsupported ghcd version {version}")
    payload = memoryview(data)[GHCD_HEADER.size:]
    if len(payload) != nbytes:
        raise ValueError("ghcd payload size mismatch")
    if zlib.crc32(payload) & 0xFFFFFFFF != crc:
        raise ValueError("ghcd payload CRC mismatch")
    r = Reader(bytes(payload))
    out = []
    for _ in range(count):
        h = HeritageRec(id=r.str(), kind=r.u8(), stage=r.u8(), flags=r.u8(), finish=r.u8(), tiers=r.u8(),
                        plinth_levels=r.u8(), doors=r.u8(), entry_rule=r.u8(), kora=r.u8(), yaw_cdeg=r.u16(),
                        height_cm=r.varint(), name_en=r.str(), name_ne=r.str(), deity=r.str(), anchor_ref=r.varint(),
                        footprint_ref=r.varint(), compound_ref=r.varint(), anchor_tile=r.u64(), x_cm=r.svarint(),
                        z_cm=r.svarint(), hidden=[], attrs=[], provenance="", review="")
        h.hidden = [r.varint() for _ in range(r.varint())]
        h.attrs = [(r.str(), r.str()) for _ in range(r.varint())]
        h.provenance, h.review = r.str(), r.str()
        out.append(h)
    if r.remaining():
        raise ValueError("trailing bytes in ghcd")
    return out


def records_json(records: Sequence[HeritageRec]) -> list[dict]:
    return [{
        "id": h.id, "kind": HeritageKind(h.kind).name, "kind_value": h.kind, "stage": h.stage, "flags": h.flags,
        "finish": HeritageFinish(h.finish).name, "tiers": h.tiers, "plinth_levels": h.plinth_levels,
        "doors": h.doors, "entry_rule": EntryRule(h.entry_rule).name, "kora": KoraDirection(h.kora).name,
        "yaw_cdeg": h.yaw_cdeg, "yaw_deg": None if h.yaw_cdeg == YAW_UNKNOWN else h.yaw_cdeg / 100.0,
        "height_m": h.height_cm / 100.0, "name_en": h.name_en, "name_ne": h.name_ne, "deity": h.deity,
        "anchor_ref": h.anchor_ref, "footprint_ref": h.footprint_ref, "compound_ref": h.compound_ref,
        "anchor_tile": h.anchor_tile, "x": h.x_cm / 100.0, "z": h.z_cm / 100.0, "hidden": h.hidden,
        "attrs": dict(h.attrs), "provenance": h.provenance, "review": h.review,
    } for h in records]


def write_ghcd(path: Path, records: Sequence[HeritageRec]) -> dict:
    data = encode_ghcd(records)
    Path(path).write_bytes(data)
    return {"records": len(records), "bytes": len(data)}


def hero_recipes_json(records: Sequence[HeritageRec], region_id: str) -> str:
    """The Track C bridge file (W2_DESIGN 9.4), deterministic."""
    return json.dumps({"format": "ghumante-hero-recipes", "version": 1, "region": region_id,
                       "records": records_json(records)}, ensure_ascii=False, sort_keys=True, indent=1) + "\n"
