"""GHT1 tile container: record types, encoder and decoder (reference implementation).

The layout is normative in docs/DATA_FORMATS.md section 1; the C# reader in
game/Assets/Ghumante/Core/Data/ is written against that document and against
golden files produced by this module. Points this module pins down where the
document is terse:

* Every chunk payload is ``u32 raw_size`` followed by ``raw_size`` bytes of
  chunk body; ``raw_size`` excludes the u32 itself. With codec 1 the u32 is
  inside the DEFLATE stream.
* Chunk bodies follow the chunk table back to back, in table order (sorted by
  fourcc), without padding. ``payload_crc32`` covers them all.
* The codec is chosen deterministically: raw DEFLATE (level 9, wbits -15,
  memLevel 8, default strategy) when it makes the chunk strictly smaller,
  otherwise stored.
* Chunks with nothing in them are omitted; a decoder returns empty lists or
  ``None`` for them.
* ``AREA`` vertices are delta-coded across the whole vertex array (the first
  vertex relative to the tile origin), not per ring.
* A road's ``ref_ref`` points at a NAME record ``(ref, "", "")``.
* Local coordinates must fit in i32. Deltas between them can exceed i32, so
  readers decode svarints as i64 and accumulate in i64.

W2 chunks (F2, additive; old readers skip them): ``RATR`` (one road attribute
record per ``ROAD`` record, same order), ``BFNT`` (one building front record
per ``BLDG`` record, same order), ``JNCT`` (junctions) and ``PROP`` (real OSM
point objects). ``RATR`` and ``BFNT`` are parallel arrays: ``canonicalize``
sorts them together with their ``ROAD`` / ``BLDG`` records, and remaps a
``BFNT`` edge index when it re-orients an outer ring. Either parallel list is
empty (chunk omitted) or exactly as long as its base list.

W2 detail pass: ``RSTR`` (road structures, one record per ``ROAD`` record,
same order, sorted with them like ``RATR``): kind, effective layer, flags,
clearance, railing height, per-point deck heights (draped / deck / ramp) and
per-corridor-sample lateral shifts (as many as the ``RATR`` record's corridor
samples, or none).

``encode_tile`` canonicalises its input first (``canonicalize``): records are
sorted as the spec requires (with full-content tie-breaks), the name table is
rebuilt in first-reference order (deduplicated, NFC, unused names dropped),
building seeds are filled in, repeated closing points are stripped, building
rings are re-oriented (outer counter-clockwise, holes clockwise) and AREA
triangles re-wound counter-clockwise, all on the final integer centimetres.
So ``decode_tile(encode_tile(td)) == canonicalize(td)`` and encoding is
byte-deterministic regardless of record or name insertion order.
"""

from __future__ import annotations

import dataclasses
import json
import struct
import unicodedata
import zlib
from dataclasses import dataclass, field
from enum import IntFlag
from typing import Callable, Iterable

import numpy as np

from .binio import Reader, Writer, encode_varint, fnv1a32, fnv1a64
from .model import ALL_TRAVEL, BuildingArchetype, BuildingUse, PoiFlags, RoadClass, RoofMaterial, RoofShape, \
    Surface, SurfaceSource, WallMaterial
from .projection import TileId

TILE_MAGIC = b"GHT1"
TILE_VERSION = 1
HEADER_SIZE = 32
CHUNK_ENTRY_SIZE = 16
FLAG_HAS_DETAIL = 1 << 0

CODEC_STORED = 0
CODEC_DEFLATE = 1
DEFLATE_LEVEL = 9
MAX_CHUNK_RAW_SIZE = 64 << 20  # decompression-bomb guard

FOURCC_AREA = b"AREA"
FOURCC_BFNT = b"BFNT"
FOURCC_BIOM = b"BIOM"
FOURCC_BLDG = b"BLDG"
FOURCC_HGHT = b"HGHT"
FOURCC_JNCT = b"JNCT"
FOURCC_LINE = b"LINE"
FOURCC_META = b"META"
FOURCC_NAME = b"NAME"
FOURCC_POIS = b"POIS"
FOURCC_PROP = b"PROP"
FOURCC_RATR = b"RATR"
FOURCC_ROAD = b"ROAD"
FOURCC_RSTR = b"RSTR"
FOURCC_SEED = b"SEED"
KNOWN_FOURCCS = (FOURCC_AREA, FOURCC_BFNT, FOURCC_BIOM, FOURCC_BLDG, FOURCC_HGHT, FOURCC_JNCT, FOURCC_LINE,
                 FOURCC_META, FOURCC_NAME, FOURCC_POIS, FOURCC_PROP, FOURCC_RATR, FOURCC_ROAD, FOURCC_RSTR,
                 FOURCC_SEED)
EDGE_NONE = 255  # BFNT front_edge / second_edge: no edge
DECK_DRAPED = 0  # RSTR deck_role: the point lies on the terrain
DECK_DECK = 1  # RSTR deck_role: the point is on a structure deck (bridge, flyover, underpass trough)
DECK_RAMP = 2  # RSTR deck_role: the point is on an approach ramp (embankment or cutting), not a deck
SHOP_BAYS_FROM_POI = 0x80  # BFNT shop_bays bit7
YAW_CDEG_MAX = 35999

# Height quantisation (section 1.1): global, never per tile.
H_MIN_M = -100.0
H_STEP_M = 0.15
H_MAX_M = H_MIN_M + 65535 * H_STEP_M  # 9730.25
_H_MIN_F32 = struct.unpack("<f", struct.pack("<f", H_MIN_M))[0]
_H_STEP_F32 = struct.unpack("<f", struct.pack("<f", H_STEP_M))[0]

DEFAULT_SCATTER_RULESET = 1  # SEED ruleset id in M0

I32_MIN, I32_MAX = -(1 << 31), (1 << 31) - 1


class LineFlags(IntFlag):
    """``LINE`` record flags (section 1.5)."""

    HAS_PREV_CTX = 1 << 0
    HAS_NEXT_CTX = 1 << 1
    INTERMITTENT = 1 << 2
    TUNNEL = 1 << 3  # tunnel or culvert


class AreaFlags(IntFlag):
    """``AREA`` record flags (section 1.7)."""

    CLIPPED_BY_TILE = 1 << 0
    HERITAGE_ZONE = 1 << 1  # a heritage square or a curated heritage compound (CONTENT_COVERAGE F1)
    SACRED_NO_VEHICLE = 1 << 2  # no motor vehicle may enter (W2_DESIGN L16, D14); the routing graph agrees


ROAD_HAS_PREV_CTX = 1 << 3  # model.RoadFlags.HAS_PREV_CTX
ROAD_HAS_NEXT_CTX = 1 << 4  # model.RoadFlags.HAS_NEXT_CTX


# ---------------------------------------------------------------------------
# Records
# ---------------------------------------------------------------------------
def _field_eq(a, b) -> bool:
    if isinstance(a, np.ndarray) or isinstance(b, np.ndarray):
        if not (isinstance(a, np.ndarray) and isinstance(b, np.ndarray)):
            return False
        return a.shape == b.shape and bool(np.array_equal(a, b))
    if isinstance(a, (list, tuple)) and isinstance(b, (list, tuple)):
        return len(a) == len(b) and all(_field_eq(x, y) for x, y in zip(a, b))
    return a == b


class _Record:
    """Field-wise equality that understands numpy arrays (for tests and QA)."""

    __slots__ = ()

    def __eq__(self, other) -> bool:
        if type(self) is not type(other):
            return NotImplemented
        return all(_field_eq(getattr(self, f.name), getattr(other, f.name)) for f in dataclasses.fields(self))

    __hash__ = None  # mutable


def _empty_points() -> np.ndarray:
    return np.zeros((0, 2), dtype=np.int64)


@dataclass(frozen=True, slots=True)
class NameEntry:
    default: str = ""
    en: str = ""
    ne: str = ""


@dataclass(slots=True, eq=False)
class RoadRec(_Record):
    osm_way_id: int = 0
    road_class: int = RoadClass.UNKNOWN
    surface: int = Surface.UNKNOWN
    surface_source: int = SurfaceSource.DEFAULT
    flags: int = 0  # model.RoadFlags
    lanes: int = 0
    sac_scale: int = 0
    trail_visibility: int = 0
    layer: int = 0
    width_cm: int = 0
    access: int = int(ALL_TRAVEL)
    name_ref: int = 0
    ref_ref: int = 0
    points: np.ndarray = field(default_factory=_empty_points)  # (N, 2) int64 local cm, incl. context points


@dataclass(slots=True, eq=False)
class LineRec(_Record):
    osm_way_id: int = 0
    kind: int = 0  # model.LineKind
    flags: int = 0  # LineFlags
    width_cm: int = 0
    name_ref: int = 0
    points: np.ndarray = field(default_factory=_empty_points)


@dataclass(slots=True, eq=False)
class BuildingRec(_Record):
    osm_ref: int = 0  # (osm_id << 1) | is_relation
    archetype: int = BuildingArchetype.GENERIC
    use: int = BuildingUse.UNKNOWN
    levels: int = 1
    flags: int = 0  # model.BuildingFlags
    height_cm: int = 0
    min_height_cm: int = 0
    roof_shape: int = RoofShape.UNKNOWN
    roof_material: int = RoofMaterial.UNKNOWN
    wall_material: int = WallMaterial.UNKNOWN
    seed: int | None = None  # None: building_seed(osm_ref)
    name_ref: int = 0
    rings: list[np.ndarray] = field(default_factory=list)  # ring 0 outer; (N, 2) int64 local cm, not closed


@dataclass(slots=True, eq=False)
class AreaRec(_Record):
    osm_ref: int = 0  # (osm_id << 1) | is_relation
    kind: int = 0  # model.AreaKind
    flags: int = 0  # AreaFlags
    name_ref: int = 0
    vertices: np.ndarray = field(default_factory=_empty_points)  # (V, 2) int64 local cm
    indices: np.ndarray = field(default_factory=lambda: np.zeros(0, dtype=np.int64))  # (T*3,)
    rings: list[tuple[int, int]] = field(default_factory=list)  # (start, n) vertex ranges


@dataclass(slots=True, eq=False)
class PoiRec(_Record):
    osm_ref: int = 0  # (osm_id << 2) | type
    kind: int = 0  # model.PoiKind
    flags: int = 0  # model.PoiFlags
    importance: int = 0  # 0..255
    x_cm: int = 0
    z_cm: int = 0
    ele_dm: int = 0
    name_ref: int = 0
    search_id: int = 0


@dataclass(slots=True, eq=False)
class RoadAttrRec(_Record):
    """``RATR`` record (W2_DESIGN 9.3): attributes of the ``ROAD`` record at the same index."""

    area_type: int = 0  # model.AreaType
    sidewalk: int = 0  # model.Sidewalk
    lanes_fwd: int = 0
    lanes_bwd: int = 0
    maxspeed_kmh: int = 0
    flags: int = 0  # model.RoadAttrFlags
    partner_way_id: int = 0
    median_cm: int = 0
    corridor_dm: np.ndarray = field(default_factory=lambda: np.zeros(0, dtype=np.int64))


@dataclass(slots=True, eq=False)
class JunctionRec(_Record):
    """``JNCT`` record (W2_DESIGN 9.3)."""

    osm_node_id: int = 0
    kind: int = 0  # model.JunctionKind
    arms: int = 0
    flags: int = 0  # model.JunctionFlags
    x_cm: int = 0
    z_cm: int = 0
    ring_diameter_cm: int = 0
    island_diameter_cm: int = 0
    island_area_osm_ref: int = 0  # (osm_id << 1) | is_relation of the AREA island
    name_ref: int = 0


@dataclass(slots=True, eq=False)
class BuildingFrontRec(_Record):
    """``BFNT`` record (W2_DESIGN 9.3): the front of the ``BLDG`` record at the same index."""

    style_profile: int = 0  # model.StyleProfile
    area_type: int = 0  # model.AreaType
    front_edge: int = EDGE_NONE
    front_dist_dm: int = 0
    shop_bays: int = 0  # bits 0-3 count, bit7 FROM_POI
    flags: int = 0  # model.BuildingFrontFlags
    second_edge: int = EDGE_NONE


@dataclass(slots=True, eq=False)
class PropRec(_Record):
    """``PROP`` record: a real OSM point object (CONTENT_COVERAGE D3)."""

    osm_ref: int = 0  # (osm_id << 2) | type
    kind: int = 0  # model.ObjectKind
    subtype: int = 0  # TREE: model.TreeClass
    flags: int = 0  # model.PropFlags
    x_cm: int = 0
    z_cm: int = 0
    yaw_cdeg: int = 0  # bearing clockwise from +Z (north), 1/100 degree; valid with PropFlags.YAW
    height_dm: int = 0
    name_ref: int = 0
    ref_ref: int = 0


@dataclass(slots=True, eq=False)
class RoadStructureRec(_Record):
    """``RSTR`` record (W2 detail pass): the structure of the ``ROAD`` record at the same index.

    ``deck_role`` (uint8 per ROAD point, context points included, or empty = draped everywhere) says what each
    point stands on (``DECK_DRAPED`` / ``DECK_DECK`` / ``DECK_RAMP``); ``deck_cm`` (int64, same length) holds the
    absolute surface height in game centimetres where the role is not draped (ignored where it is).
    ``shift_cm`` (int64 per RATR corridor sample, or empty) is the lateral corridor shift, + = left of the point
    order."""

    kind: int = 0  # model.RoadStructureKind
    layer: int = 0  # effective layer
    flags: int = 0  # model.RoadStructureFlags
    clearance_cm: int = 0  # free height above this road, 0 = unlimited / unknown
    railing_dm: int = 0  # railing height, 0 = none
    deck_role: np.ndarray = field(default_factory=lambda: np.zeros(0, dtype=np.uint8))
    deck_cm: np.ndarray = field(default_factory=lambda: np.zeros(0, dtype=np.int64))
    shift_cm: np.ndarray = field(default_factory=lambda: np.zeros(0, dtype=np.int64))


@dataclass(slots=True, eq=False)
class TileData(_Record):
    tile: TileId
    data_version: int
    heights_q: np.ndarray | None = None  # (n, n) uint16, row 0 south
    biomes: np.ndarray | None = None  # (n, n) uint8
    names: list[NameEntry] = field(default_factory=list)
    roads: list[RoadRec] = field(default_factory=list)
    lines: list[LineRec] = field(default_factory=list)
    buildings: list[BuildingRec] = field(default_factory=list)
    areas: list[AreaRec] = field(default_factory=list)
    pois: list[PoiRec] = field(default_factory=list)
    seed: tuple[int, int] | None = None  # (tile_seed, ruleset)
    meta: dict | None = None
    has_detail: bool = False
    road_attrs: list[RoadAttrRec] = field(default_factory=list)  # RATR: empty or one per road
    junctions: list[JunctionRec] = field(default_factory=list)  # JNCT
    building_fronts: list[BuildingFrontRec] = field(default_factory=list)  # BFNT: empty or one per building
    props: list[PropRec] = field(default_factory=list)  # PROP
    road_structures: list[RoadStructureRec] = field(default_factory=list)  # RSTR: empty or one per road

    def name(self, ref: int) -> NameEntry | None:
        """Resolve a ``name_ref`` (0 = no name)."""
        return None if ref == 0 else self.names[ref - 1]


@dataclass(frozen=True, slots=True)
class ChunkInfo:
    fourcc: bytes
    codec: int
    offset: int
    stored_size: int


@dataclass(frozen=True, slots=True)
class TileHeader:
    version: int
    flags: int
    tile: TileId
    data_version: int
    payload_crc32: int
    chunks: tuple[ChunkInfo, ...]

    @property
    def has_detail(self) -> bool:
        return bool(self.flags & FLAG_HAS_DETAIL)


# ---------------------------------------------------------------------------
# Names
# ---------------------------------------------------------------------------
def _nfc(s: str | None) -> str:
    return unicodedata.normalize("NFC", s or "")


class NameTable:
    """Builds a tile's NAME chunk: ``ref`` returns a 1-based ``name_ref``,
    0 for no name, and reuses the record for identical (default, en, ne)."""

    __slots__ = ("_index", "_entries")

    def __init__(self) -> None:
        self._index: dict[NameEntry, int] = {}
        self._entries: list[NameEntry] = []

    def __len__(self) -> int:
        return len(self._entries)

    def ref(self, name) -> int:
        """``name`` is a ``model.NameRec``, a ``NameEntry`` or ``None``."""
        if name is None:
            return 0
        e = NameEntry(_nfc(name.default), _nfc(name.en), _nfc(name.ne))
        if not (e.default or e.en or e.ne):
            return 0
        k = self._index.get(e)
        if k is None:
            self._entries.append(e)
            k = self._index[e] = len(self._entries)
        return k

    def ref_str(self, text: str | None) -> int:
        """Reference for a bare string such as a road ``ref`` ("NH04")."""
        return self.ref(NameEntry(text or "", "", ""))

    def entries(self) -> list[NameEntry]:
        return list(self._entries)


# ---------------------------------------------------------------------------
# Small helpers used by the tiler
# ---------------------------------------------------------------------------
def quantize_heights(h_m) -> np.ndarray:
    """Metres -> global u16 height code: ``clip(rint((h + 100) / 0.15), 0, 65535)``."""
    h = np.asarray(h_m, dtype=np.float64)
    if np.isnan(h).any():
        raise ValueError("NaN height")
    return np.clip(np.rint((h - H_MIN_M) / H_STEP_M), 0, 65535).astype(np.uint16)


def dequantize_heights(q) -> np.ndarray:
    """u16 height code -> metres, ``float32(-100.0 + q * 0.15)`` evaluated in float64."""
    return (H_MIN_M + np.asarray(q, dtype=np.float64) * H_STEP_M).astype(np.float32)


def to_local_cm(tile: TileId, x, z) -> tuple[np.ndarray, np.ndarray]:
    """Game metres -> integer centimetres relative to the tile's south-west corner.

    Tile origins are exact multiples of a power of two, so a point on a shared
    border gets ``S*100`` in one tile and ``0`` in the other, and its other
    coordinate is identical in both."""
    xc = np.rint((np.asarray(x, dtype=np.float64) - tile.x0) * 100.0).astype(np.int64)
    zc = np.rint((np.asarray(z, dtype=np.float64) - tile.z0) * 100.0).astype(np.int64)
    return xc, zc


def points_to_local_cm(tile: TileId, pts) -> np.ndarray:
    """(N, 2) game metres -> (N, 2) int64 local centimetres."""
    p = np.asarray(pts, dtype=np.float64).reshape(-1, 2)
    xc, zc = to_local_cm(tile, p[:, 0], p[:, 1])
    return np.stack([xc, zc], axis=1)


def from_local_cm(tile: TileId, pts_cm) -> np.ndarray:
    """(N, 2) local centimetres -> (N, 2) float64 game metres."""
    p = np.asarray(pts_cm, dtype=np.float64).reshape(-1, 2)
    return np.stack([tile.x0 + p[:, 0] / 100.0, tile.z0 + p[:, 1] / 100.0], axis=1)


def tile_seed(tile_key: int, ruleset: int = DEFAULT_SCATTER_RULESET) -> int:
    """SEED chunk value (D10, stable seed): FNV-1a 64 over ``(u64 tile_key, u32 ruleset)``.

    It no longer hashes ``data_version``, so trees and props stay where they are
    when the pipeline is rebuilt; only a new scatter ruleset re-rolls them. The
    byte layout is the one M0 used with ``data_version`` in the u32 slot, so a
    ruleset-1 seed equals the old seed of a data_version-1 tile."""
    return fnv1a64(struct.pack("<QI", tile_key, ruleset))


def building_seed(osm_ref: int) -> int:
    """Per-building variation seed: FNV-1a 32 over the osm_ref varint bytes."""
    return fnv1a32(encode_varint(osm_ref))


def make_seed(tile: TileId, ruleset: int = DEFAULT_SCATTER_RULESET) -> tuple[int, int]:
    """``(tile_seed, ruleset)`` for the SEED chunk (independent of ``data_version``, D10)."""
    return tile_seed(tile.key, ruleset), ruleset


def osm_ref_wr(osm_type: str, osm_id: int) -> int:
    """Building/area ``osm_ref``: ``(osm_id << 1) | is_relation``."""
    if osm_type not in ("w", "r"):
        raise ValueError(f"buildings and areas come from ways or relations, not {osm_type!r}")
    return (osm_id << 1) | (osm_type == "r")


def osm_ref_nwr(osm_type: str, osm_id: int) -> int:
    """POI ``osm_ref``: ``(osm_id << 2) | type`` with node 0, way 1, relation 2."""
    return (osm_id << 2) | {"n": 0, "w": 1, "r": 2}[osm_type]


# ---------------------------------------------------------------------------
# Vectorised varints
# ---------------------------------------------------------------------------
_U64_7 = [np.uint64(7 * k) for k in range(10)]
_SMALL = 48  # below this many values a plain Python loop beats numpy


def _svarints_bytes(values: np.ndarray) -> bytes:
    """Concatenated zigzag LEB128 encodings of an int64 array."""
    v = np.ascontiguousarray(values, dtype=np.int64).reshape(-1)
    n = len(v)
    if n == 0:
        return b""
    if n < _SMALL:
        return _svarints_list_bytes(v.tolist())
    z = (v.view(np.uint64) << np.uint64(1)) ^ (v >> np.int64(63)).view(np.uint64)
    return _varints_bytes(z)


def _svarints_list_bytes(vals: list[int]) -> bytes:
    out = bytearray()
    for s in vals:
        zz = ((s << 1) ^ (s >> 63)) & 0xFFFFFFFFFFFFFFFF
        while zz >= 0x80:
            out.append((zz & 0x7F) | 0x80)
            zz >>= 7
        out.append(zz)
    return bytes(out)


def _varints_bytes(z: np.ndarray) -> bytes:
    z = np.ascontiguousarray(z, dtype=np.uint64).reshape(-1)
    if len(z) == 0:
        return b""
    nb = np.ones(len(z), dtype=np.int64)
    for k in range(1, 10):
        nb += z >= (np.uint64(1) << _U64_7[k])
    starts = np.cumsum(nb) - nb
    out = np.empty(int(nb.sum()), dtype=np.uint8)
    for k in range(int(nb.max())):
        m = nb > k
        byte = ((z[m] >> _U64_7[k]) & np.uint64(0x7F)).astype(np.uint8)
        byte |= np.where(nb[m] > k + 1, 0x80, 0).astype(np.uint8)
        out[starts[m] + k] = byte
    return out.tobytes()


def _read_varints(r: Reader, count: int) -> np.ndarray:
    """Read ``count`` LEB128 varints as uint64 (vectorised)."""
    if count == 0:
        return np.zeros(0, dtype=np.uint64)
    if count > r.remaining():
        raise ValueError(f"{count} varints cannot fit in {r.remaining()} bytes")
    if count < _SMALL:
        data, pos, end = r.data, r.pos, r.end
        vals = []
        for _ in range(count):
            v = shift = 0
            while True:
                if pos >= end:
                    raise ValueError("truncated varint array")
                b = data[pos]
                pos += 1
                v |= (b & 0x7F) << shift
                if b < 0x80:
                    break
                shift += 7
                if shift >= 70:
                    raise ValueError("varint longer than 10 bytes")
            if v >> 64:
                raise ValueError("varint overflow")
            vals.append(v)
        r.pos = pos
        return np.array(vals, dtype=np.uint64)
    hi = min(r.end, r.pos + 10 * count)
    buf = np.frombuffer(r.data[r.pos:hi], dtype=np.uint8)
    ends = np.flatnonzero(buf < 0x80)[:count]
    if len(ends) < count:
        raise ValueError("truncated varint array")
    starts = np.empty(count, dtype=np.int64)
    starts[0] = 0
    starts[1:] = ends[:-1] + 1
    lens = ends - starts + 1
    if int(lens.max()) > 10:
        raise ValueError("varint longer than 10 bytes")
    tens = lens == 10
    if tens.any() and (buf[ends[tens]] > 1).any():
        raise ValueError("varint overflow")
    vals = np.zeros(count, dtype=np.uint64)
    for k in range(int(lens.max())):
        m = lens > k
        vals[m] |= (buf[starts[m] + k].astype(np.uint64) & np.uint64(0x7F)) << _U64_7[k]
    r.pos += int(ends[-1]) + 1
    return vals


def _read_svarints(r: Reader, count: int) -> np.ndarray:
    z = _read_varints(r, count)
    return (z >> np.uint64(1)).view(np.int64) ^ -(z & np.uint64(1)).view(np.int64)


def _write_points(w: Writer, pts: np.ndarray) -> None:
    """``{svarint dx, svarint dz}`` per point; the first relative to the tile origin."""
    if 2 * len(pts) < _SMALL:
        flat = pts.reshape(-1).tolist()
        w.raw(_svarints_list_bytes([flat[0], flat[1]] + [flat[i] - flat[i - 2] for i in range(2, len(flat))]))
        return
    d = np.diff(pts, axis=0, prepend=np.zeros((1, 2), dtype=np.int64))
    w.raw(_svarints_bytes(d))


def _read_points(r: Reader, n: int) -> np.ndarray:
    d = _read_svarints(r, 2 * n).reshape(n, 2)
    return np.cumsum(d, axis=0, dtype=np.int64)


# ---------------------------------------------------------------------------
# Canonicalisation and validation
# ---------------------------------------------------------------------------
def _int_points(a, what: str, shape2: bool = True) -> np.ndarray:
    arr = np.asarray(a)
    if arr.size == 0:
        return np.zeros((0, 2) if shape2 else (0,), dtype=np.int64)
    if not np.issubdtype(arr.dtype, np.integer):
        raise TypeError(f"{what} must be integer centimetres, got dtype {arr.dtype}")
    arr = np.ascontiguousarray(arr, dtype=np.int64)
    if shape2 and (arr.ndim != 2 or arr.shape[1] != 2):
        raise ValueError(f"{what} must have shape (N, 2), got {arr.shape}")
    if arr.min() < I32_MIN or arr.max() > I32_MAX:
        raise ValueError(f"{what} outside the i32 range")
    return arr


def _strip_closing(ring: np.ndarray) -> np.ndarray:
    if len(ring) > 1 and np.array_equal(ring[0], ring[-1]):
        return ring[:-1]
    return ring


def _area2(ring: np.ndarray) -> int:
    """Twice the signed area of an integer ring, exactly (Python ints)."""
    xs = ring[:, 0].tolist()
    zs = ring[:, 1].tolist()
    n = len(xs)
    return sum(xs[i] * zs[(i + 1) % n] - xs[(i + 1) % n] * zs[i] for i in range(n))


def canonical_ring(ring_cm, outer: bool) -> np.ndarray:
    """A building ring as stored: integer cm, closing point stripped, outer
    rings counter-clockwise and holes clockwise (first vertex kept first).
    Zero-area rings are left as they are."""
    r = _strip_closing(_int_points(ring_cm, "building ring"))
    a2 = _area2(r) if len(r) >= 3 else 0
    if a2 != 0 and (a2 > 0) != outer:
        r = np.concatenate([r[:1], r[:0:-1]])
    return r


def _ccw_triangles(vertices: np.ndarray, indices: np.ndarray) -> np.ndarray:
    """Flip clockwise triangles (exact integer orientation test)."""
    if len(indices) == 0:
        return indices
    tri = indices.reshape(-1, 3).copy()
    p = vertices[tri]  # (T, 3, 2)
    e1 = p[:, 1] - p[:, 0]
    e2 = p[:, 2] - p[:, 0]
    if max(int(np.abs(e1).max()), int(np.abs(e2).max())) < (1 << 30):
        cross_neg = (e1[:, 0] * e2[:, 1] - e1[:, 1] * e2[:, 0]) < 0
    else:  # products could overflow int64
        cross_neg = np.array([a * d - b * c < 0 for (a, b), (c, d) in zip(e1.tolist(), e2.tolist())], dtype=bool)
    tri[cross_neg] = tri[cross_neg][:, [0, 2, 1]]
    return tri.reshape(-1)


def _tiebreak(rec, name_fields: tuple[str, ...], resolve: Callable[[int], tuple]) -> tuple:
    """Total-order key over every field of a record (names resolved to text)."""
    out = []
    for f in dataclasses.fields(rec):
        v = getattr(rec, f.name)
        if f.name in name_fields:
            out.append(resolve(v))
        elif isinstance(v, np.ndarray):
            out.append((v.shape, v.astype(np.int64).tobytes()))
        elif isinstance(v, list):
            out.append(tuple((x.shape, x.astype(np.int64).tobytes()) if isinstance(x, np.ndarray) else tuple(x)
                             for x in v))
        else:
            out.append(-1 if v is None else int(v))
    return tuple(out)


def _sorted_idx(recs: list, primary: Callable, name_fields: tuple[str, ...], resolve: Callable,
                extra: list | None = None, extra2: list | None = None) -> list[int]:
    """Indices of ``recs`` in the spec's order: by the primary key; records
    sharing a primary key are ordered by their full content (computed only for
    those groups), then by the content of their ``extra`` record (a parallel
    list such as ``RATR`` for ``ROAD``) and of their ``extra2`` record (``RSTR``)
    when given."""
    keyed = sorted(((primary(r), i) for i, r in enumerate(recs)), key=lambda t: t[0])
    out: list[int] = []
    i = 0
    while i < len(keyed):
        j = i + 1
        while j < len(keyed) and keyed[j][0] == keyed[i][0]:
            j += 1
        group = [k for _, k in keyed[i:j]]
        if len(group) > 1:
            group.sort(key=lambda k: (_tiebreak(recs[k], name_fields, resolve),
                                      _tiebreak(extra[k], (), resolve) if extra else (),
                                      _tiebreak(extra2[k], (), resolve) if extra2 else ()))
        out.extend(group)
        i = j
    return out


def _sorted(recs: list, primary: Callable, name_fields: tuple[str, ...], resolve: Callable) -> list:
    """``recs`` in the spec's order (see ``_sorted_idx``)."""
    return [recs[k] for k in _sorted_idx(recs, primary, name_fields, resolve)]


def _ring_flips(ring_cm, outer: bool) -> bool:
    """True when ``canonical_ring`` reverses this ring."""
    r = _strip_closing(_int_points(ring_cm, "building ring"))
    a2 = _area2(r) if len(r) >= 3 else 0
    return a2 != 0 and (a2 > 0) != outer


def _first_point(pts: np.ndarray) -> tuple[int, int]:
    return (int(pts[0, 0]), int(pts[0, 1])) if len(pts) else (I32_MIN - 1, I32_MIN - 1)


def canonicalize(td: TileData) -> TileData:
    """The exact TileData that ``encode_tile`` writes (see module docstring).
    Does not modify ``td``; raises ``ValueError``/``TypeError`` on invalid input."""
    names = td.names

    def resolve(ref: int) -> tuple[str, str, str]:
        if ref == 0:
            return ("", "", "")
        if not 0 < ref <= len(names):
            raise ValueError(f"name_ref {ref} outside the name table ({len(names)} entries)")
        e = names[ref - 1]
        return (_nfc(e.default), _nfc(e.en), _nfc(e.ne))

    if td.road_attrs and len(td.road_attrs) != len(td.roads):
        raise ValueError(f"{len(td.road_attrs)} RATR records for {len(td.roads)} roads")
    if td.building_fronts and len(td.building_fronts) != len(td.buildings):
        raise ValueError(f"{len(td.building_fronts)} BFNT records for {len(td.buildings)} buildings")
    if td.road_structures and len(td.road_structures) != len(td.roads):
        raise ValueError(f"{len(td.road_structures)} RSTR records for {len(td.roads)} roads")
    roads = []
    for r in td.roads:
        pts = _int_points(r.points, f"road {r.osm_way_id} points")
        roads.append(dataclasses.replace(r, points=pts))
    road_attrs = []
    for a in td.road_attrs:
        cor = np.asarray(a.corridor_dm)
        if cor.size and not np.issubdtype(cor.dtype, np.integer):
            raise TypeError("RATR corridor_dm must be integer decimetres")
        cor = np.ascontiguousarray(cor, dtype=np.int64).reshape(-1)
        if cor.size and cor.min() < 0:
            raise ValueError("RATR corridor_dm must not be negative")
        road_attrs.append(dataclasses.replace(a, corridor_dm=cor))
    road_structures = [_canonical_structure(s, len(roads[k].points),
                                            len(road_attrs[k].corridor_dm) if road_attrs else 0, k)
                       for k, s in enumerate(td.road_structures)]
    lines = []
    for ln in td.lines:
        pts = _int_points(ln.points, f"line {ln.osm_way_id} points")
        lines.append(dataclasses.replace(ln, points=pts))
    buildings = []
    fronts = [dataclasses.replace(f) for f in td.building_fronts]
    for k, b in enumerate(td.buildings):
        if fronts and b.rings and _ring_flips(b.rings[0], outer=True):
            n = len(_strip_closing(_int_points(b.rings[0], "building ring")))
            f = fronts[k]
            for name in ("front_edge", "second_edge"):
                e = getattr(f, name)
                if e != EDGE_NONE and 0 <= e < n:
                    setattr(f, name, n - 1 - e)
        rings = [canonical_ring(rg, outer=(i == 0)) for i, rg in enumerate(b.rings)]
        seed = building_seed(b.osm_ref) if b.seed is None else int(b.seed)
        buildings.append(dataclasses.replace(b, rings=rings, seed=seed))
    areas = []
    for a in td.areas:
        verts = _int_points(a.vertices, f"area {a.osm_ref} vertices")
        idx = np.asarray(a.indices)
        if idx.size and not np.issubdtype(idx.dtype, np.integer):
            raise TypeError(f"area {a.osm_ref} indices must be integers")
        idx = np.ascontiguousarray(idx, dtype=np.int64).reshape(-1)
        if len(idx) % 3:
            raise ValueError(f"area {a.osm_ref}: index count {len(idx)} is not a multiple of 3")
        if len(idx) and (idx.min() < 0 or idx.max() >= len(verts)):
            raise ValueError(f"area {a.osm_ref}: triangle index out of range")
        rings = [(int(s), int(n)) for s, n in a.rings]
        areas.append(dataclasses.replace(a, vertices=verts, indices=_ccw_triangles(verts, idx), rings=rings))
    pois = [dataclasses.replace(p) for p in td.pois]

    junctions = [dataclasses.replace(j) for j in td.junctions]
    props = [dataclasses.replace(p) for p in td.props]
    for p in props:
        if p.flags & 1 and not 0 <= p.yaw_cdeg <= YAW_CDEG_MAX:  # PropFlags.YAW
            raise ValueError(f"prop {p.osm_ref}: yaw_cdeg {p.yaw_cdeg} outside 0..{YAW_CDEG_MAX}")
        if not p.flags & 1 and p.yaw_cdeg:
            raise ValueError(f"prop {p.osm_ref}: yaw_cdeg set without the YAW flag")

    order = _sorted_idx(roads, lambda r: (r.osm_way_id, *_first_point(r.points)), ("name_ref", "ref_ref"), resolve,
                        road_attrs, road_structures)
    roads = [roads[k] for k in order]
    road_attrs = [road_attrs[k] for k in order] if road_attrs else []
    road_structures = [road_structures[k] for k in order] if road_structures else []
    lines = _sorted(lines, lambda r: (r.osm_way_id, *_first_point(r.points)), ("name_ref",), resolve)
    order = _sorted_idx(buildings, lambda r: (r.osm_ref,), ("name_ref",), resolve, fronts)
    buildings = [buildings[k] for k in order]
    fronts = [fronts[k] for k in order] if fronts else []
    areas = _sorted(areas, lambda r: (r.osm_ref, *_first_point(r.vertices)), ("name_ref",), resolve)
    pois = _sorted(pois, lambda r: (r.osm_ref, r.kind), ("name_ref",), resolve)
    junctions = _sorted(junctions, lambda r: (r.osm_node_id, r.kind, r.x_cm, r.z_cm), ("name_ref",), resolve)
    props = _sorted(props, lambda r: (r.osm_ref, r.kind), ("name_ref", "ref_ref"), resolve)

    table = NameTable()

    def remap(ref: int) -> int:
        return table.ref(NameEntry(*resolve(ref)))

    # Fixed walk order (fourcc order of the referencing chunks) for the new table.
    for a in areas:
        a.name_ref = remap(a.name_ref)
    for b in buildings:
        b.name_ref = remap(b.name_ref)
    for j in junctions:
        j.name_ref = remap(j.name_ref)
    for ln in lines:
        ln.name_ref = remap(ln.name_ref)
    for p in pois:
        p.name_ref = remap(p.name_ref)
    for p in props:
        p.name_ref = remap(p.name_ref)
        p.ref_ref = remap(p.ref_ref)
    for r in roads:
        r.name_ref = remap(r.name_ref)
        r.ref_ref = remap(r.ref_ref)

    heights = None if td.heights_q is None else _check_grid(td.heights_q, np.uint16, 65535, "heights_q")
    biomes = None if td.biomes is None else _check_grid(td.biomes, np.uint8, 255, "biomes")
    seed = None if td.seed is None else (int(td.seed[0]), int(td.seed[1]))
    if td.meta is not None and not isinstance(td.meta, dict):
        raise TypeError("meta must be a dict")
    meta = None if td.meta is None else json.loads(_nfc(_meta_json(td.meta)))
    return TileData(tile=td.tile, data_version=int(td.data_version), heights_q=heights, biomes=biomes,
                    names=table.entries(), roads=roads, lines=lines, buildings=buildings, areas=areas, pois=pois,
                    seed=seed, meta=meta, has_detail=bool(td.has_detail), road_attrs=road_attrs,
                    junctions=junctions, building_fronts=fronts, props=props, road_structures=road_structures)


def _canonical_structure(s: RoadStructureRec, n_points: int, n_corridor: int, k: int) -> RoadStructureRec:
    """Validated copy of an ``RSTR`` record (integer arrays, lengths matching the road and its RATR record;
    deck heights zeroed where draped)."""
    role = np.ascontiguousarray(np.asarray(s.deck_role), dtype=np.int64).reshape(-1)
    deck = np.asarray(s.deck_cm)
    if deck.size and not np.issubdtype(deck.dtype, np.integer):
        raise TypeError(f"RSTR {k}: deck_cm must be integer centimetres")
    deck = np.ascontiguousarray(deck, dtype=np.int64).reshape(-1)
    if len(role) != len(deck):
        raise ValueError(f"RSTR {k}: {len(role)} deck roles for {len(deck)} deck heights")
    if len(role) and (role.min() < 0 or role.max() > DECK_RAMP):
        raise ValueError(f"RSTR {k}: deck_role outside 0..{DECK_RAMP}")
    if len(role) and not (role != DECK_DRAPED).any():
        role, deck = role[:0], deck[:0]  # draped everywhere: no deck heights at all
    if len(role) not in (0, n_points):
        raise ValueError(f"RSTR {k}: {len(role)} deck points for a road of {n_points} points")
    deck = np.where(role != DECK_DRAPED, deck, 0)
    shift = np.asarray(s.shift_cm)
    if shift.size and not np.issubdtype(shift.dtype, np.integer):
        raise TypeError(f"RSTR {k}: shift_cm must be integer centimetres")
    shift = np.ascontiguousarray(shift, dtype=np.int64).reshape(-1)
    if len(shift) and not shift.any():
        shift = shift[:0]
    if len(shift) not in (0, n_corridor):
        raise ValueError(f"RSTR {k}: {len(shift)} shifts for {n_corridor} RATR corridor samples")
    for v, what, lo, hi in ((s.kind, "kind", 0, 255), (s.layer, "layer", -128, 127), (s.flags, "flags", 0, 255),
                            (s.railing_dm, "railing_dm", 0, 255), (s.clearance_cm, "clearance_cm", 0, 1 << 32)):
        if not lo <= int(v) <= hi:
            raise ValueError(f"RSTR {k}: {what} {v} outside {lo}..{hi}")
    return RoadStructureRec(kind=int(s.kind), layer=int(s.layer), flags=int(s.flags), clearance_cm=int(s.clearance_cm),
                            railing_dm=int(s.railing_dm), deck_role=role.astype(np.uint8), deck_cm=deck,
                            shift_cm=shift)


def _check_grid(a, dtype, vmax: int, what: str) -> np.ndarray:
    arr = np.asarray(a)
    if arr.ndim != 2 or arr.shape[0] != arr.shape[1]:
        raise ValueError(f"{what} must be square, got {arr.shape}")
    n = arr.shape[0]
    if n < 2 or (n - 1) & (n - 2) or n > 65535:
        raise ValueError(f"{what} size {n} is not 2^k + 1")
    if not np.issubdtype(arr.dtype, np.integer):
        raise TypeError(f"{what} must be an integer array, got {arr.dtype}")
    if arr.size and (arr.min() < 0 or arr.max() > vmax):
        raise ValueError(f"{what} values outside 0..{vmax}")
    return np.ascontiguousarray(arr, dtype=dtype)


def _meta_json(meta: dict) -> str:
    return json.dumps(meta, sort_keys=True, ensure_ascii=False, separators=(",", ":"), allow_nan=False)


# ---------------------------------------------------------------------------
# Chunk bodies
# ---------------------------------------------------------------------------
def _enc_hght(q: np.ndarray) -> bytes:
    n = q.shape[0]
    r = q.copy()
    r[:, 1:] = q[:, 1:] - q[:, :-1]  # uint16 arithmetic wraps mod 65536
    r[1:, 0] = q[1:, 0] - q[:-1, 0]
    w = Writer().u16(n).u16(0).f32(H_MIN_M).f32(H_STEP_M)
    return w.raw(r.astype("<u2").tobytes()).bytes()


def _grid_size(r: Reader) -> int:
    n = r.u16()
    r.u16()
    if n < 2 or (n - 1) & (n - 2):
        raise ValueError(f"grid size {n} is not 2^k + 1")
    return n


def _dec_hght(r: Reader) -> np.ndarray:
    n = _grid_size(r)
    h_min, h_step = r.f32(), r.f32()
    if h_min != _H_MIN_F32 or h_step != _H_STEP_F32:
        raise ValueError(f"unsupported height quantisation ({h_min}, {h_step})")
    res = np.frombuffer(r.raw(2 * n * n), dtype="<u2").reshape(n, n).astype(np.uint64)
    res[:, 0] = np.cumsum(res[:, 0]) & np.uint64(0xFFFF)
    return (np.cumsum(res, axis=1) & np.uint64(0xFFFF)).astype(np.uint16)


def _enc_biom(b: np.ndarray) -> bytes:
    return Writer().u16(b.shape[0]).u16(0).raw(b.astype(np.uint8).tobytes()).bytes()


def _dec_biom(r: Reader) -> np.ndarray:
    n = _grid_size(r)
    return np.frombuffer(r.raw(n * n), dtype=np.uint8).reshape(n, n).copy()


def _enc_name(names: list[NameEntry]) -> bytes:
    w = Writer().varint(len(names))
    for e in names:
        w.str(e.default).str(e.en).str(e.ne)
    return w.bytes()


def _dec_name(r: Reader) -> list[NameEntry]:
    n = _count(r)
    return [NameEntry(r.str(), r.str(), r.str()) for _ in range(n)]


def _check_ctx_points(n: int, flags: int, prev_bit: int, next_bit: int, what: str) -> None:
    need = 2 + bool(flags & prev_bit) + bool(flags & next_bit)
    if n < need:
        raise ValueError(f"{what}: {n} points, need at least {need}")


def _enc_road(roads: list[RoadRec]) -> bytes:
    w = Writer().varint(len(roads))
    for rd in roads:
        _check_ctx_points(len(rd.points), rd.flags, ROAD_HAS_PREV_CTX, ROAD_HAS_NEXT_CTX, f"road {rd.osm_way_id}")
        (w.varint(rd.osm_way_id).u8(rd.road_class).u8(rd.surface).u8(rd.surface_source).u8(rd.flags)
         .u8(rd.lanes).u8(rd.sac_scale).u8(rd.trail_visibility).i8(rd.layer).varint(rd.width_cm).u8(rd.access)
         .varint(rd.name_ref).varint(rd.ref_ref).varint(len(rd.points)))
        _write_points(w, rd.points)
    return w.bytes()


def _dec_road(r: Reader, n_names: int) -> list[RoadRec]:
    out = []
    for _ in range(_count(r)):
        rd = RoadRec(osm_way_id=r.varint(), road_class=r.u8(), surface=r.u8(), surface_source=r.u8(), flags=r.u8(),
                     lanes=r.u8(), sac_scale=r.u8(), trail_visibility=r.u8(), layer=r.i8(), width_cm=r.varint(),
                     access=r.u8(), name_ref=_name_ref(r, n_names), ref_ref=_name_ref(r, n_names))
        n = r.varint()
        _check_ctx_points(n, rd.flags, ROAD_HAS_PREV_CTX, ROAD_HAS_NEXT_CTX, "road")
        rd.points = _read_points(r, n)
        out.append(rd)
    return out


def _enc_line(lines: list[LineRec]) -> bytes:
    w = Writer().varint(len(lines))
    for ln in lines:
        _check_ctx_points(len(ln.points), ln.flags, LineFlags.HAS_PREV_CTX, LineFlags.HAS_NEXT_CTX,
                          f"line {ln.osm_way_id}")
        w.varint(ln.osm_way_id).u8(ln.kind).u8(ln.flags).varint(ln.width_cm).varint(ln.name_ref)
        w.varint(len(ln.points))
        _write_points(w, ln.points)
    return w.bytes()


def _dec_line(r: Reader, n_names: int) -> list[LineRec]:
    out = []
    for _ in range(_count(r)):
        ln = LineRec(osm_way_id=r.varint(), kind=r.u8(), flags=r.u8(), width_cm=r.varint(),
                     name_ref=_name_ref(r, n_names))
        n = r.varint()
        _check_ctx_points(n, ln.flags, LineFlags.HAS_PREV_CTX, LineFlags.HAS_NEXT_CTX, "line")
        ln.points = _read_points(r, n)
        out.append(ln)
    return out


def _enc_bldg(buildings: list[BuildingRec]) -> bytes:
    w = Writer().varint(len(buildings))
    for b in buildings:
        if not 1 <= b.levels <= 255:
            raise ValueError(f"building {b.osm_ref}: levels {b.levels} outside 1..255")
        if not b.rings:
            raise ValueError(f"building {b.osm_ref} has no rings")
        (w.varint(b.osm_ref).u8(b.archetype).u8(b.use).u8(b.levels).u8(b.flags).varint(b.height_cm)
         .varint(b.min_height_cm).u8(b.roof_shape).u8(b.roof_material).u8(b.wall_material).u32(b.seed)
         .varint(b.name_ref).varint(len(b.rings)))
        for ring in b.rings:
            if len(ring) < 3:
                raise ValueError(f"building {b.osm_ref}: ring with {len(ring)} points")
            w.varint(len(ring))
            _write_points(w, ring)
    return w.bytes()


def _dec_bldg(r: Reader, n_names: int) -> list[BuildingRec]:
    out = []
    for _ in range(_count(r)):
        b = BuildingRec(osm_ref=r.varint(), archetype=r.u8(), use=r.u8(), levels=r.u8(), flags=r.u8(),
                        height_cm=r.varint(), min_height_cm=r.varint(), roof_shape=r.u8(), roof_material=r.u8(),
                        wall_material=r.u8(), seed=r.u32(), name_ref=_name_ref(r, n_names))
        n_rings = _count(r)
        if n_rings < 1:
            raise ValueError("building without rings")
        for _ in range(n_rings):
            n = r.varint()
            if n < 3:
                raise ValueError(f"building ring with {n} points")
            b.rings.append(_read_points(r, n))
        out.append(b)
    return out


def _enc_area(areas: list[AreaRec]) -> bytes:
    w = Writer().varint(len(areas))
    for a in areas:
        nv = len(a.vertices)
        for s, n in a.rings:
            if s < 0 or n < 1 or s + n > nv:
                raise ValueError(f"area {a.osm_ref}: ring ({s}, {n}) outside {nv} vertices")
        w.varint(a.osm_ref).u8(a.kind).u8(a.flags).varint(a.name_ref).varint(nv)
        _write_points(w, a.vertices)
        w.varint(len(a.indices)).raw(_varints_bytes(a.indices.astype(np.uint64)))
        w.varint(len(a.rings))
        for s, n in a.rings:
            w.varint(s).varint(n)
    return w.bytes()


def _dec_area(r: Reader, n_names: int) -> list[AreaRec]:
    out = []
    for _ in range(_count(r)):
        a = AreaRec(osm_ref=r.varint(), kind=r.u8(), flags=r.u8(), name_ref=_name_ref(r, n_names))
        nv = _count(r)
        a.vertices = _read_points(r, nv)
        ni = _count(r)
        if ni % 3:
            raise ValueError(f"area index count {ni} is not a multiple of 3")
        idx = _read_varints(r, ni)
        if ni and int(idx.max()) >= nv:
            raise ValueError("area triangle index out of range")
        a.indices = idx.astype(np.int64)
        for _ in range(_count(r)):
            s, n = r.varint(), r.varint()
            if n < 1 or s + n > nv:
                raise ValueError(f"area ring ({s}, {n}) outside {nv} vertices")
            a.rings.append((s, n))
        out.append(a)
    return out


def _enc_pois(pois: list[PoiRec]) -> bytes:
    w = Writer().varint(len(pois))
    for p in pois:
        for v, what in ((p.x_cm, "x_cm"), (p.z_cm, "z_cm")):
            if not I32_MIN <= v <= I32_MAX:
                raise ValueError(f"poi {p.osm_ref}: {what} outside the i32 range")
        if p.ele_dm and not p.flags & PoiFlags.HAS_ELE:
            raise ValueError(f"poi {p.osm_ref}: ele_dm set without the HAS_ELE flag")
        (w.varint(p.osm_ref).u16(p.kind).u8(p.flags).u8(p.importance).svarint(p.x_cm).svarint(p.z_cm)
         .svarint(p.ele_dm).varint(p.name_ref).varint(p.search_id))
    return w.bytes()


def _dec_pois(r: Reader, n_names: int) -> list[PoiRec]:
    return [PoiRec(osm_ref=r.varint(), kind=r.u16(), flags=r.u8(), importance=r.u8(), x_cm=r.svarint(),
                   z_cm=r.svarint(), ele_dm=r.svarint(), name_ref=_name_ref(r, n_names), search_id=r.varint())
            for _ in range(_count(r))]


def _enc_ratr(attrs: list[RoadAttrRec]) -> bytes:
    w = Writer().varint(len(attrs))
    for a in attrs:
        (w.u8(a.area_type).u8(a.sidewalk).u8(a.lanes_fwd).u8(a.lanes_bwd).u8(a.maxspeed_kmh).u8(a.flags)
         .varint(a.partner_way_id).varint(a.median_cm).varint(len(a.corridor_dm)))
        w.raw(_varints_bytes(a.corridor_dm.astype(np.uint64)))
    return w.bytes()


def _dec_ratr(r: Reader) -> list[RoadAttrRec]:
    out = []
    for _ in range(_count(r)):
        a = RoadAttrRec(area_type=r.u8(), sidewalk=r.u8(), lanes_fwd=r.u8(), lanes_bwd=r.u8(), maxspeed_kmh=r.u8(),
                        flags=r.u8(), partner_way_id=r.varint(), median_cm=r.varint())
        a.corridor_dm = _read_varints(r, _count(r)).astype(np.int64)
        out.append(a)
    return out


def _enc_jnct(junctions: list[JunctionRec]) -> bytes:
    w = Writer().varint(len(junctions))
    for j in junctions:
        (w.varint(j.osm_node_id).u8(j.kind).u8(j.arms).u8(j.flags).svarint(j.x_cm).svarint(j.z_cm)
         .varint(j.ring_diameter_cm).varint(j.island_diameter_cm).varint(j.island_area_osm_ref).varint(j.name_ref))
    return w.bytes()


def _enc_rstr(structs: list[RoadStructureRec]) -> bytes:
    w = Writer().varint(len(structs))
    for s in structs:
        w.u8(s.kind).i8(s.layer).u8(s.flags).varint(s.clearance_cm).u8(s.railing_dm).varint(len(s.deck_role))
        if len(s.deck_role):
            role = s.deck_role.astype(np.int64)
            up = role != DECK_DRAPED
            y = s.deck_cm[up]
            dy = np.diff(y, prepend=np.int64(0))
            zz = ((dy.view(np.uint64) << np.uint64(1)) ^ (dy >> np.int64(63)).view(np.uint64))
            code = np.zeros(len(role), dtype=np.uint64)
            code[up] = ((zz << np.uint64(1)) | (role[up] == DECK_RAMP).astype(np.uint64)) + np.uint64(1)
            w.raw(_varints_bytes(code))
        w.varint(len(s.shift_cm))
        w.raw(_svarints_bytes(s.shift_cm))
    return w.bytes()


def _dec_rstr(r: Reader) -> list[RoadStructureRec]:
    out = []
    for _ in range(_count(r)):
        s = RoadStructureRec(kind=r.u8(), layer=r.i8(), flags=r.u8(), clearance_cm=r.varint(), railing_dm=r.u8())
        n = _count(r)
        if n:
            code = _read_varints(r, n)
            up = code != 0
            c = code[up] - np.uint64(1)
            zz = c >> np.uint64(1)
            dy = (zz >> np.uint64(1)).view(np.int64) ^ -(zz & np.uint64(1)).view(np.int64)
            role = np.zeros(n, dtype=np.uint8)
            role[up] = np.where((c & np.uint64(1)) != 0, DECK_RAMP, DECK_DECK)
            deck = np.zeros(n, dtype=np.int64)
            deck[up] = np.cumsum(dy)
            s.deck_role, s.deck_cm = role, deck
        s.shift_cm = _read_svarints(r, _count(r)).astype(np.int64)
        out.append(s)
    return out


def _dec_jnct(r: Reader, n_names: int) -> list[JunctionRec]:
    return [JunctionRec(osm_node_id=r.varint(), kind=r.u8(), arms=r.u8(), flags=r.u8(), x_cm=r.svarint(),
                        z_cm=r.svarint(), ring_diameter_cm=r.varint(), island_diameter_cm=r.varint(),
                        island_area_osm_ref=r.varint(), name_ref=_name_ref(r, n_names))
            for _ in range(_count(r))]


def _enc_bfnt(fronts: list[BuildingFrontRec]) -> bytes:
    w = Writer().varint(len(fronts))
    for f in fronts:
        w.u8(f.style_profile).u8(f.area_type).u8(f.front_edge).u8(f.front_dist_dm).u8(f.shop_bays).u8(f.flags)
        w.u8(f.second_edge)
    return w.bytes()


def _dec_bfnt(r: Reader) -> list[BuildingFrontRec]:
    n = r.varint()
    if 7 * n > r.remaining():
        raise ValueError(f"{n} BFNT records cannot fit in {r.remaining()} bytes")
    return [BuildingFrontRec(*r.raw(7)) for _ in range(n)]


def _enc_prop(props: list[PropRec]) -> bytes:
    w = Writer().varint(len(props))
    for p in props:
        for v, what in ((p.x_cm, "x_cm"), (p.z_cm, "z_cm")):
            if not I32_MIN <= v <= I32_MAX:
                raise ValueError(f"prop {p.osm_ref}: {what} outside the i32 range")
        (w.varint(p.osm_ref).u8(p.kind).u8(p.subtype).u8(p.flags).svarint(p.x_cm).svarint(p.z_cm).u16(p.yaw_cdeg)
         .varint(p.height_dm).varint(p.name_ref).varint(p.ref_ref))
    return w.bytes()


def _dec_prop(r: Reader, n_names: int) -> list[PropRec]:
    return [PropRec(osm_ref=r.varint(), kind=r.u8(), subtype=r.u8(), flags=r.u8(), x_cm=r.svarint(), z_cm=r.svarint(),
                    yaw_cdeg=r.u16(), height_dm=r.varint(), name_ref=_name_ref(r, n_names),
                    ref_ref=_name_ref(r, n_names))
            for _ in range(_count(r))]


def _dec_meta(r: Reader) -> dict:
    meta = json.loads(r.str())
    if not isinstance(meta, dict):
        raise ValueError("META is not a JSON object")
    return meta


def _enc_seed(seed: tuple[int, int]) -> bytes:
    return Writer().u64(seed[0]).u16(seed[1]).bytes()


def _dec_seed(r: Reader) -> tuple[int, int]:
    return r.u64(), r.u16()


def _count(r: Reader) -> int:
    n = r.varint()
    if n > r.remaining():
        raise ValueError(f"count {n} exceeds the {r.remaining()} bytes left")
    return n


def _name_ref(r: Reader, n_names: int) -> int:
    ref = r.varint()
    if ref > n_names:
        raise ValueError(f"name_ref {ref} outside the name table ({n_names} entries)")
    return ref


# ---------------------------------------------------------------------------
# Container
# ---------------------------------------------------------------------------
def _deflate(raw: bytes) -> bytes:
    c = zlib.compressobj(DEFLATE_LEVEL, zlib.DEFLATED, -15, 8, zlib.Z_DEFAULT_STRATEGY)
    return c.compress(raw) + c.flush()


def _inflate(stored: bytes) -> bytes:
    d = zlib.decompressobj(-15)
    out = d.decompress(stored, MAX_CHUNK_RAW_SIZE + 4)
    if d.unconsumed_tail:
        raise ValueError("chunk exceeds the maximum raw size")
    if not d.eof or d.unused_data:
        raise ValueError("bad DEFLATE stream")
    return out


def encode_chunks(chunks: Iterable[tuple[bytes, bytes]], tile: TileId, data_version: int, has_detail: bool) -> bytes:
    """Assemble a GHT1 blob from ``(fourcc, body)`` pairs (sorted here).
    Exposed so tests and tools can build tiles with extra chunk types."""
    items = sorted(chunks, key=lambda c: c[0])
    fourccs = [f for f, _ in items]
    if len(set(fourccs)) != len(fourccs):
        raise ValueError("duplicate fourcc")
    if any(len(f) != 4 for f in fourccs):
        raise ValueError("fourcc must be 4 bytes")
    stored: list[tuple[bytes, int, bytes]] = []
    for f, body in items:
        raw = struct.pack("<I", len(body)) + body
        comp = _deflate(raw)
        stored.append((f, CODEC_DEFLATE, comp) if len(comp) < len(raw) else (f, CODEC_STORED, raw))
    offset = HEADER_SIZE + CHUNK_ENTRY_SIZE * len(stored)
    table = Writer()
    payload = bytearray()
    for f, codec, data in stored:
        table.raw(f).u8(codec).raw(b"\0\0\0").u32(offset + len(payload)).u32(len(data))
        payload += data
    head = (Writer().raw(TILE_MAGIC).u16(TILE_VERSION).u16(FLAG_HAS_DETAIL if has_detail else 0).u8(tile.level)
            .raw(b"\0\0\0").u32(tile.tx).u32(tile.ty).u32(data_version).u16(len(stored)).u16(0)
            .u32(zlib.crc32(payload) & 0xFFFFFFFF))
    return head.bytes() + table.bytes() + bytes(payload)


def encode_tile(td: TileData) -> bytes:
    """Encode a tile (deterministic; see module docstring)."""
    c = canonicalize(td)
    chunks: list[tuple[bytes, bytes]] = []
    try:
        if c.heights_q is not None:
            chunks.append((FOURCC_HGHT, _enc_hght(c.heights_q)))
        if c.biomes is not None:
            chunks.append((FOURCC_BIOM, _enc_biom(c.biomes)))
        if c.names:
            chunks.append((FOURCC_NAME, _enc_name(c.names)))
        if c.roads:
            chunks.append((FOURCC_ROAD, _enc_road(c.roads)))
        if c.lines:
            chunks.append((FOURCC_LINE, _enc_line(c.lines)))
        if c.buildings:
            chunks.append((FOURCC_BLDG, _enc_bldg(c.buildings)))
        if c.areas:
            chunks.append((FOURCC_AREA, _enc_area(c.areas)))
        if c.pois:
            chunks.append((FOURCC_POIS, _enc_pois(c.pois)))
        if c.road_attrs:
            chunks.append((FOURCC_RATR, _enc_ratr(c.road_attrs)))
        if c.junctions:
            chunks.append((FOURCC_JNCT, _enc_jnct(c.junctions)))
        if c.building_fronts:
            chunks.append((FOURCC_BFNT, _enc_bfnt(c.building_fronts)))
        if c.props:
            chunks.append((FOURCC_PROP, _enc_prop(c.props)))
        if c.road_structures:
            chunks.append((FOURCC_RSTR, _enc_rstr(c.road_structures)))
        if c.seed is not None:
            chunks.append((FOURCC_SEED, _enc_seed(c.seed)))
        if c.meta is not None:
            chunks.append((FOURCC_META, Writer().str(_meta_json(c.meta)).bytes()))
    except (struct.error, OverflowError) as e:
        raise ValueError(f"field out of range: {e}") from e
    return encode_chunks(chunks, c.tile, c.data_version, c.has_detail)


def read_header(blob: bytes) -> TileHeader:
    """Parse and bounds-check the header and chunk table (no CRC check)."""
    mv = memoryview(blob)
    if len(mv) < HEADER_SIZE:
        raise ValueError("tile shorter than its header")
    if bytes(mv[:4]) != TILE_MAGIC:
        raise ValueError(f"bad tile magic {bytes(mv[:4])!r}")
    version, flags, level = struct.unpack_from("<HHB", mv, 4)
    if version != TILE_VERSION:
        raise ValueError(f"unsupported GHT1 version {version}")
    tx, ty, data_version, count = struct.unpack_from("<IIIH", mv, 12)
    (crc,) = struct.unpack_from("<I", mv, 28)
    payload_start = HEADER_SIZE + CHUNK_ENTRY_SIZE * count
    if payload_start > len(mv):
        raise ValueError("chunk table runs past the end of the tile")
    chunks = []
    for k in range(count):
        base = HEADER_SIZE + CHUNK_ENTRY_SIZE * k
        fourcc = bytes(mv[base:base + 4])
        codec = mv[base + 4]
        offset, size = struct.unpack_from("<II", mv, base + 8)
        if offset < payload_start or offset + size > len(mv):
            raise ValueError(f"chunk {fourcc!r} outside the tile")
        chunks.append(ChunkInfo(fourcc, codec, offset, size))
    try:
        tile = TileId(level, tx, ty)
    except ValueError as e:
        raise ValueError(f"bad tile address: {e}") from e
    return TileHeader(version, flags, tile, data_version, crc, tuple(chunks))


def chunk_body(blob: bytes, info: ChunkInfo) -> bytes:
    """Decompressed chunk body (without the raw_size prefix)."""
    stored = bytes(memoryview(blob)[info.offset:info.offset + info.stored_size])
    if info.codec == CODEC_STORED:
        raw = stored
    elif info.codec == CODEC_DEFLATE:
        try:
            raw = _inflate(stored)
        except zlib.error as e:
            raise ValueError(f"chunk {info.fourcc!r}: {e}") from e
    else:
        raise ValueError(f"chunk {info.fourcc!r}: unknown codec {info.codec}")
    if len(raw) < 4:
        raise ValueError(f"chunk {info.fourcc!r} too short")
    (raw_size,) = struct.unpack_from("<I", raw, 0)
    if raw_size != len(raw) - 4:
        raise ValueError(f"chunk {info.fourcc!r}: raw_size {raw_size} != {len(raw) - 4}")
    return raw[4:]


def decode_tile(blob: bytes) -> TileData:
    """Decode and verify a GHT1 blob. Unknown fourccs are skipped; any
    corruption raises ``ValueError``."""
    h = read_header(blob)
    payload_start = HEADER_SIZE + CHUNK_ENTRY_SIZE * len(h.chunks)
    if zlib.crc32(memoryview(blob)[payload_start:]) & 0xFFFFFFFF != h.payload_crc32:
        raise ValueError("tile payload CRC mismatch")
    seen = [c.fourcc for c in h.chunks]
    if len(set(seen)) != len(seen):
        raise ValueError("duplicate chunk fourcc")
    bodies = {c.fourcc: c for c in h.chunks if c.fourcc in KNOWN_FOURCCS}
    td = TileData(tile=h.tile, data_version=h.data_version, has_detail=h.has_detail)

    def parse(fourcc: bytes, fn):
        info = bodies.get(fourcc)
        if info is None:
            return None
        body = chunk_body(blob, info)
        r = Reader(body)
        try:
            value = fn(r)
        except (EOFError, struct.error, UnicodeDecodeError, OverflowError) as e:
            raise ValueError(f"corrupt {fourcc.decode()} chunk: {e}") from e
        except ValueError as e:
            raise ValueError(f"corrupt {fourcc.decode()} chunk: {e}") from e
        if r.remaining():
            raise ValueError(f"{r.remaining()} trailing bytes in {fourcc.decode()} chunk")
        return value

    td.names = parse(FOURCC_NAME, _dec_name) or []
    nn = len(td.names)
    td.heights_q = parse(FOURCC_HGHT, _dec_hght)
    td.biomes = parse(FOURCC_BIOM, _dec_biom)
    td.roads = parse(FOURCC_ROAD, lambda r: _dec_road(r, nn)) or []
    td.lines = parse(FOURCC_LINE, lambda r: _dec_line(r, nn)) or []
    td.buildings = parse(FOURCC_BLDG, lambda r: _dec_bldg(r, nn)) or []
    td.areas = parse(FOURCC_AREA, lambda r: _dec_area(r, nn)) or []
    td.pois = parse(FOURCC_POIS, lambda r: _dec_pois(r, nn)) or []
    td.seed = parse(FOURCC_SEED, _dec_seed)
    td.meta = parse(FOURCC_META, _dec_meta)
    td.road_attrs = parse(FOURCC_RATR, _dec_ratr) or []
    td.junctions = parse(FOURCC_JNCT, lambda r: _dec_jnct(r, nn)) or []
    td.building_fronts = parse(FOURCC_BFNT, _dec_bfnt) or []
    td.props = parse(FOURCC_PROP, lambda r: _dec_prop(r, nn)) or []
    td.road_structures = parse(FOURCC_RSTR, _dec_rstr) or []
    if td.road_attrs and len(td.road_attrs) != len(td.roads):
        raise ValueError(f"{len(td.road_attrs)} RATR records for {len(td.roads)} ROAD records")
    if td.building_fronts and len(td.building_fronts) != len(td.buildings):
        raise ValueError(f"{len(td.building_fronts)} BFNT records for {len(td.buildings)} BLDG records")
    if td.road_structures:
        if len(td.road_structures) != len(td.roads):
            raise ValueError(f"{len(td.road_structures)} RSTR records for {len(td.roads)} ROAD records")
        for k, (s, rd) in enumerate(zip(td.road_structures, td.roads)):
            if len(s.deck_role) not in (0, len(rd.points)):
                raise ValueError(f"RSTR {k}: {len(s.deck_role)} deck points for a road of {len(rd.points)} points")
            nc = len(td.road_attrs[k].corridor_dm) if td.road_attrs else 0
            if len(s.shift_cm) not in (0, nc):
                raise ValueError(f"RSTR {k}: {len(s.shift_cm)} shifts for {nc} RATR corridor samples")
    return td
