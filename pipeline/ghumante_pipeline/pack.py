"""GHPK region packs and region manifests (docs/DATA_FORMATS.md section 2).

A pack is a 64-byte header, the GHT1 tile blobs back to back in ascending
tile-key order starting at offset 64, then the directory (24 bytes per tile,
sorted by key, binary-searchable) which ends the file. Everything is a pure
function of ``(region_id, data_version, tiles)``, so packs are byte-identical
across builds; the CI determinism test compares their SHA-256.

``PackReader`` memory-maps a pack, validates the header and directory
(including ``directory_crc32``) on open, and checks each tile's CRC-32 on
``get``. The region manifest is a sibling JSON file written with sorted keys.
"""

from __future__ import annotations

import hashlib
import json
import mmap
import os
import struct
import zlib
from pathlib import Path
from typing import Iterator, Mapping

import numpy as np

from .projection import tile_from_key
from .tile_format import read_header

PACK_MAGIC = b"GHPK"
PACK_VERSION = 1
PACK_HEADER_SIZE = 64
DIR_ENTRY_SIZE = 24
REGION_ID_SIZE = 16

MANIFEST_FORMAT = "ghumante-region-manifest"
MANIFEST_VERSION = 1

_DIR_DTYPE = np.dtype([("key", "<u8"), ("offset", "<u8"), ("size", "<u4"), ("crc", "<u4")])
_HEADER = struct.Struct("<4sHHIIQQ16sI12s")
assert _HEADER.size == PACK_HEADER_SIZE


def _region_id_bytes(region_id: str) -> bytes:
    try:
        b = region_id.encode("ascii")
    except UnicodeEncodeError:
        raise ValueError(f"region id {region_id!r} is not ASCII") from None
    if not b or b"\0" in b:
        raise ValueError(f"bad region id {region_id!r}")
    return b[:REGION_ID_SIZE]


def _atomic_write(path: Path, chunks) -> str:
    """Write byte chunks to ``path`` via a temporary file; returns the SHA-256."""
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_name(path.name + ".tmp")
    h = hashlib.sha256()
    try:
        with open(tmp, "wb") as f:
            for c in chunks:
                h.update(c)
                f.write(c)
        os.replace(tmp, path)
    except BaseException:
        tmp.unlink(missing_ok=True)
        raise
    return h.hexdigest()


def write_pack(path, region_id: str, data_version: int, tiles: Mapping[int, bytes], validate: bool = True) -> dict:
    """Write a region pack. ``tiles`` maps tile key -> GHT1 blob.

    With ``validate`` each blob's GHT1 header must parse and its level/tx/ty
    must match the key. Returns JSON-friendly stats: path, region_id,
    data_version, tile_count, bytes, sha256, tile_bytes, max_tile_bytes and
    tile_counts per level (string keys, as in the manifest).
    """
    rid = _region_id_bytes(region_id)
    keys = sorted(int(k) for k in tiles)
    if len(set(keys)) != len(keys):
        raise ValueError("duplicate tile keys")
    if keys and (keys[0] < 0 or keys[-1] >= 1 << 64):
        raise ValueError("tile key outside u64")
    blobs = []
    for k in keys:
        blob = bytes(tiles[k])
        if validate:
            hdr = read_header(blob)
            if hdr.tile.key != k:
                raise ValueError(f"tile blob for key {k:#x} holds tile {hdr.tile}")
            if hdr.data_version != data_version:
                raise ValueError(f"tile {hdr.tile} has data_version {hdr.data_version}, pack {data_version}")
        if len(blob) >= 1 << 32:
            raise ValueError("tile larger than 4 GiB")
        blobs.append(blob)

    directory = np.zeros(len(keys), dtype=_DIR_DTYPE)
    offset = PACK_HEADER_SIZE
    for i, (k, blob) in enumerate(zip(keys, blobs)):
        directory[i] = (k, offset, len(blob), zlib.crc32(blob) & 0xFFFFFFFF)
        offset += len(blob)
    dir_bytes = directory.tobytes()
    header = _HEADER.pack(PACK_MAGIC, PACK_VERSION, 0, len(keys), data_version, offset, len(dir_bytes),
                          rid.ljust(REGION_ID_SIZE, b"\0"), zlib.crc32(dir_bytes) & 0xFFFFFFFF, b"\0" * 12)
    sha = _atomic_write(Path(path), [header, *blobs, dir_bytes])

    counts: dict[int, int] = {}
    for k in keys:
        lvl = k >> 58
        counts[lvl] = counts.get(lvl, 0) + 1
    tile_bytes = offset - PACK_HEADER_SIZE
    return {
        "path": str(path),
        "region_id": rid.decode("ascii"),
        "data_version": data_version,
        "tile_count": len(keys),
        "bytes": offset + len(dir_bytes),
        "sha256": sha,
        "tile_bytes": tile_bytes,
        "max_tile_bytes": max((len(b) for b in blobs), default=0),
        "tile_counts": {str(lvl): counts[lvl] for lvl in sorted(counts)},
    }


class PackReader:
    """Random-access reader for a ``.ghpk`` file (memory-mapped)."""

    def __init__(self, path) -> None:
        self.path = Path(path)
        self._file = open(self.path, "rb")
        try:
            size = os.fstat(self._file.fileno()).st_size
            if size < PACK_HEADER_SIZE:
                raise ValueError("file shorter than a pack header")
            self._mm = mmap.mmap(self._file.fileno(), 0, access=mmap.ACCESS_READ)
            self._open(size)
        except BaseException:
            self.close()
            raise

    def _open(self, size: int) -> None:
        (magic, version, _flags, count, data_version, dir_off, dir_size, rid, dir_crc,
         _reserved) = _HEADER.unpack(self._mm[:PACK_HEADER_SIZE])
        if magic != PACK_MAGIC:
            raise ValueError(f"bad pack magic {magic!r}")
        if version != PACK_VERSION:
            raise ValueError(f"unsupported GHPK version {version}")
        if dir_size != count * DIR_ENTRY_SIZE:
            raise ValueError("directory size does not match tile count")
        if dir_off < PACK_HEADER_SIZE or dir_off + dir_size > size:
            raise ValueError("directory outside the file")
        dir_bytes = self._mm[dir_off:dir_off + dir_size]  # a copy: keeps the mmap closable
        if zlib.crc32(dir_bytes) & 0xFFFFFFFF != dir_crc:
            raise ValueError("pack directory CRC mismatch")
        d = np.frombuffer(dir_bytes, dtype=_DIR_DTYPE)
        if count > 1 and not np.all(d["key"][1:] > d["key"][:-1]):
            raise ValueError("pack directory keys not strictly ascending")
        ends = d["offset"] + d["size"].astype(np.uint64)
        if count and (d["offset"].min() < PACK_HEADER_SIZE or ends.max() > dir_off):
            raise ValueError("tile data outside the tile area")
        try:
            self.region_id = rid.rstrip(b"\0").decode("ascii")
        except UnicodeDecodeError:
            raise ValueError("region id is not ASCII") from None
        self.data_version = data_version
        self.tile_count = count
        self._keys = d["key"].copy()
        self._offsets = d["offset"].copy()
        self._sizes = d["size"].copy()
        self._crcs = d["crc"].copy()

    # -- container protocol ------------------------------------------------
    def __enter__(self) -> "PackReader":
        return self

    def __exit__(self, *exc) -> None:
        self.close()

    def close(self) -> None:
        mm = getattr(self, "_mm", None)
        if mm is not None:
            mm.close()
            self._mm = None
        if getattr(self, "_file", None) is not None:
            self._file.close()
            self._file = None

    def __len__(self) -> int:
        return self.tile_count

    def __iter__(self) -> Iterator[int]:
        return iter(self.keys())

    def __contains__(self, key) -> bool:
        return self._index(key) is not None

    def keys(self) -> list[int]:
        return [int(k) for k in self._keys]

    def tiles(self):
        """TileIds of every tile, in key order."""
        return [tile_from_key(k) for k in self.keys()]

    def _index(self, key) -> int | None:
        try:
            k = int(key)
        except (TypeError, ValueError):
            return None
        if not 0 <= k < 1 << 64 or self.tile_count == 0:
            return None
        i = int(np.searchsorted(self._keys, np.uint64(k)))
        return i if i < self.tile_count and int(self._keys[i]) == k else None

    def entry(self, key: int) -> tuple[int, int, int]:
        """``(offset, size, crc32)`` of a tile; ``KeyError`` when absent."""
        i = self._index(key)
        if i is None:
            raise KeyError(key)
        return int(self._offsets[i]), int(self._sizes[i]), int(self._crcs[i])

    def get(self, key: int, verify: bool = True) -> bytes:
        """The tile blob for ``key``. ``KeyError`` when absent, ``ValueError``
        when ``verify`` and its CRC-32 does not match the directory."""
        if self._mm is None:
            raise ValueError("pack is closed")
        off, size, crc = self.entry(key)
        blob = self._mm[off:off + size]
        if verify and zlib.crc32(blob) & 0xFFFFFFFF != crc:
            raise ValueError(f"tile {key:#x} CRC mismatch")
        return blob

    def items(self, verify: bool = True) -> Iterator[tuple[int, bytes]]:
        for k in self.keys():
            yield k, self.get(k, verify)


# ---------------------------------------------------------------------------
# Manifest
# ---------------------------------------------------------------------------
def sha256_file(path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def file_entry(path, name: str | None = None) -> dict:
    """A manifest ``files`` entry: ``{"path", "bytes", "sha256"}`` (path is the
    file name, since manifests sit next to the files they list)."""
    p = Path(path)
    return {"path": name or p.name, "bytes": p.stat().st_size, "sha256": sha256_file(p)}


def manifest_json(manifest: dict) -> str:
    m = dict(manifest)
    if m.setdefault("format", MANIFEST_FORMAT) != MANIFEST_FORMAT:
        raise ValueError(f"manifest format must be {MANIFEST_FORMAT!r}")
    if m.setdefault("version", MANIFEST_VERSION) != MANIFEST_VERSION:
        raise ValueError(f"manifest version must be {MANIFEST_VERSION}")
    return json.dumps(m, sort_keys=True, ensure_ascii=False, indent=1, allow_nan=False) + "\n"


def write_manifest(path, manifest: dict) -> None:
    """Write ``<region>.manifest.json``: UTF-8, sorted keys, ``indent=1``.
    ``format`` and ``version`` are filled in when missing."""
    _atomic_write(Path(path), [manifest_json(manifest).encode("utf-8")])


def read_manifest(path) -> dict:
    m = json.loads(Path(path).read_text(encoding="utf-8"))
    if not isinstance(m, dict) or m.get("format") != MANIFEST_FORMAT:
        raise ValueError(f"{path} is not a Ghumante region manifest")
    if m.get("version") != MANIFEST_VERSION:
        raise ValueError(f"unsupported manifest version {m.get('version')}")
    return m
