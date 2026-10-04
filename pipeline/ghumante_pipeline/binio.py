"""Little-endian binary writer/reader with LEB128 varints, zigzag and hashes.

Shared by tile_format, pack, search_index and routing. The encodings are
specified in docs/DATA_FORMATS.md section 0 and mirrored by
game/Assets/Ghumante/Core/Data/BinReader.cs.
"""

from __future__ import annotations

import struct
import unicodedata

FNV32_OFFSET = 0x811C9DC5
FNV32_PRIME = 0x01000193
FNV64_OFFSET = 0xCBF29CE484222325
FNV64_PRIME = 0x100000001B3


def fnv1a32(data: bytes) -> int:
    h = FNV32_OFFSET
    for b in data:
        h ^= b
        h = (h * FNV32_PRIME) & 0xFFFFFFFF
    return h


def fnv1a64(data: bytes) -> int:
    h = FNV64_OFFSET
    for b in data:
        h ^= b
        h = (h * FNV64_PRIME) & 0xFFFFFFFFFFFFFFFF
    return h


def zigzag(n: int) -> int:
    if not -(1 << 63) <= n < (1 << 63):
        raise OverflowError(n)
    return ((n << 1) ^ (n >> 63)) & 0xFFFFFFFFFFFFFFFF


def unzigzag(z: int) -> int:
    return (z >> 1) ^ -(z & 1)


def encode_varint(n: int) -> bytes:
    if n < 0:
        raise ValueError("varint must be non-negative; use svarint")
    if n >= 1 << 64:
        raise OverflowError(n)
    out = bytearray()
    while True:
        b = n & 0x7F
        n >>= 7
        if n:
            out.append(b | 0x80)
        else:
            out.append(b)
            return bytes(out)


class Writer:
    """Append-only little-endian byte builder."""

    __slots__ = ("buf",)

    def __init__(self) -> None:
        self.buf = bytearray()

    def __len__(self) -> int:
        return len(self.buf)

    def bytes(self) -> bytes:
        return bytes(self.buf)

    def raw(self, b: bytes) -> "Writer":
        self.buf += b
        return self

    def u8(self, v: int) -> "Writer":
        self.buf += struct.pack("<B", v)
        return self

    def i8(self, v: int) -> "Writer":
        self.buf += struct.pack("<b", v)
        return self

    def u16(self, v: int) -> "Writer":
        self.buf += struct.pack("<H", v)
        return self

    def i16(self, v: int) -> "Writer":
        self.buf += struct.pack("<h", v)
        return self

    def u32(self, v: int) -> "Writer":
        self.buf += struct.pack("<I", v)
        return self

    def i32(self, v: int) -> "Writer":
        self.buf += struct.pack("<i", v)
        return self

    def u64(self, v: int) -> "Writer":
        self.buf += struct.pack("<Q", v)
        return self

    def f32(self, v: float) -> "Writer":
        self.buf += struct.pack("<f", v)
        return self

    def f64(self, v: float) -> "Writer":
        self.buf += struct.pack("<d", v)
        return self

    def varint(self, v: int) -> "Writer":
        self.buf += encode_varint(v)
        return self

    def svarint(self, v: int) -> "Writer":
        self.buf += encode_varint(zigzag(v))
        return self

    def str(self, s: str) -> "Writer":
        b = unicodedata.normalize("NFC", s or "").encode("utf-8")
        self.varint(len(b))
        self.buf += b
        return self


class Reader:
    """Sequential little-endian reader over a bytes-like object."""

    __slots__ = ("data", "pos", "end")

    def __init__(self, data: bytes | memoryview, pos: int = 0, end: int | None = None) -> None:
        self.data = memoryview(data)
        self.pos = pos
        self.end = len(self.data) if end is None else end

    def remaining(self) -> int:
        return self.end - self.pos

    def _take(self, n: int) -> memoryview:
        if self.pos + n > self.end:
            raise EOFError(f"need {n} bytes at {self.pos}, have {self.end - self.pos}")
        mv = self.data[self.pos:self.pos + n]
        self.pos += n
        return mv

    def raw(self, n: int) -> bytes:
        return bytes(self._take(n))

    def u8(self) -> int:
        return self._take(1)[0]

    def i8(self) -> int:
        return struct.unpack("<b", self._take(1))[0]

    def u16(self) -> int:
        return struct.unpack("<H", self._take(2))[0]

    def i16(self) -> int:
        return struct.unpack("<h", self._take(2))[0]

    def u32(self) -> int:
        return struct.unpack("<I", self._take(4))[0]

    def i32(self) -> int:
        return struct.unpack("<i", self._take(4))[0]

    def u64(self) -> int:
        return struct.unpack("<Q", self._take(8))[0]

    def f32(self) -> float:
        return struct.unpack("<f", self._take(4))[0]

    def f64(self) -> float:
        return struct.unpack("<d", self._take(8))[0]

    def varint(self) -> int:
        result = 0
        shift = 0
        for _ in range(10):
            b = self.u8()
            result |= (b & 0x7F) << shift
            if not b & 0x80:
                if result >= 1 << 64:
                    raise ValueError("varint overflow")
                return result
            shift += 7
        raise ValueError("varint longer than 10 bytes")

    def svarint(self) -> int:
        return unzigzag(self.varint())

    def str(self) -> str:
        n = self.varint()
        return bytes(self._take(n)).decode("utf-8")
