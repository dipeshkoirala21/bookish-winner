import pytest

from ghumante_pipeline.binio import Reader, Writer, encode_varint, fnv1a32, fnv1a64, unzigzag, zigzag


@pytest.mark.parametrize("n", [0, 1, 127, 128, 300, 16383, 16384, 2**32 - 1, 2**63, 2**64 - 1])
def test_varint_round_trip(n):
    w = Writer().varint(n)
    assert Reader(w.bytes()).varint() == n


def test_varint_known_bytes():
    assert encode_varint(0) == b"\x00"
    assert encode_varint(300) == b"\xac\x02"


@pytest.mark.parametrize("n", [0, -1, 1, -2, 2**31, -(2**31), 2**62, -(2**63)])
def test_zigzag(n):
    assert unzigzag(zigzag(n)) == n
    w = Writer().svarint(n)
    assert Reader(w.bytes()).svarint() == n


def test_zigzag_known():
    assert [zigzag(v) for v in (0, -1, 1, -2, 2)] == [0, 1, 2, 3, 4]


def test_fixed_width_and_strings():
    w = Writer().u8(255).i8(-5).u16(65535).i16(-3).u32(4_000_000_000).i32(-7).u64(2**64 - 1).f32(1.5).f64(-2.25).str("काठमाडौं").str("")
    r = Reader(w.bytes())
    assert (r.u8(), r.i8(), r.u16(), r.i16(), r.u32(), r.i32(), r.u64(), r.f32(), r.f64(), r.str(), r.str()) == (
        255, -5, 65535, -3, 4_000_000_000, -7, 2**64 - 1, 1.5, -2.25, "काठमाडौं", "")
    assert r.remaining() == 0


def test_reader_eof():
    with pytest.raises(EOFError):
        Reader(b"\x01").u16()


def test_fnv_known_values():
    assert fnv1a32(b"") == 0x811C9DC5
    assert fnv1a32(b"a") == 0xE40C292C
    assert fnv1a64(b"a") == 0xAF63DC4C8601EC8C
