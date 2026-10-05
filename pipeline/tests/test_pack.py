"""GHPK region packs and region manifests."""

from __future__ import annotations

import hashlib
import json
import struct
import zlib

import numpy as np
import pytest

from ghumante_pipeline.pack import (DIR_ENTRY_SIZE, MANIFEST_FORMAT, PACK_HEADER_SIZE, PackReader, file_entry,
                                    read_manifest, sha256_file, write_manifest, write_pack)
from ghumante_pipeline.projection import TileId, tile_from_key
from ghumante_pipeline.tile_format import PoiRec, TileData, decode_tile, encode_tile, make_seed, quantize_heights

DV = 1


def _tile_blob(tile: TileId, rng: np.random.Generator, dv: int = DV) -> bytes:
    td = TileData(tile, dv, seed=make_seed(tile, dv), has_detail=tile.level >= 9,
                  pois=[PoiRec(osm_ref=int(rng.integers(1, 1 << 40)), kind=200, x_cm=int(rng.integers(0, 1000)))])
    if rng.random() < 0.2:
        td.heights_q = quantize_heights(rng.uniform(1200, 1400, (9, 9)))
    return encode_tile(td)


def _tiles(n: int, seed: int = 0) -> dict[int, bytes]:
    rng = np.random.default_rng(seed)
    out: dict[int, bytes] = {}
    levels = [5, 6, 7, 8, 9, 10]
    while len(out) < n:
        lvl = int(rng.choice(levels))
        side = 1 << lvl
        t = TileId(lvl, int(rng.integers(side // 4, side // 2)), int(rng.integers(side // 8, side // 4)))
        out.setdefault(t.key, _tile_blob(t, rng))
    return out


@pytest.fixture(scope="module")
def tiles1000() -> dict[int, bytes]:
    return _tiles(1000)


@pytest.fixture()
def pack1000(tmp_path, tiles1000):
    path = tmp_path / "kathmandu_valley.ghpk"
    stats = write_pack(path, "kathmandu_valley", DV, tiles1000)
    return path, stats


def test_round_trip_1000_tiles(pack1000, tiles1000):
    path, stats = pack1000
    with PackReader(path) as pk:
        assert pk.region_id == "kathmandu_valley" and pk.data_version == DV
        assert pk.tile_count == len(pk) == 1000
        assert pk.keys() == sorted(tiles1000) and list(pk) == sorted(tiles1000)
        rng = np.random.default_rng(42)
        keys = list(tiles1000)
        for k in rng.choice(keys, 300):
            k = int(k)
            assert k in pk
            assert pk.get(k) == tiles1000[k]
        for k in keys[:20]:
            td = decode_tile(pk.get(k))
            assert td.tile == tile_from_key(k)
        assert dict(pk.items()) == tiles1000
        assert pk.tiles()[0].key == min(tiles1000)
    assert stats["tile_count"] == 1000 and stats["bytes"] == path.stat().st_size
    assert stats["sha256"] == sha256_file(path) == hashlib.sha256(path.read_bytes()).hexdigest()
    assert stats["tile_bytes"] == sum(len(b) for b in tiles1000.values())
    assert stats["max_tile_bytes"] == max(len(b) for b in tiles1000.values())
    levels = [k >> 58 for k in tiles1000]
    assert stats["tile_counts"] == {str(lvl): levels.count(lvl) for lvl in sorted(set(levels))}
    assert list(stats["tile_counts"]) == sorted(stats["tile_counts"], key=int)


def test_layout_matches_spec(pack1000, tiles1000):
    path, _ = pack1000
    data = path.read_bytes()
    magic, version, flags, count, dv, dir_off, dir_size, rid, dir_crc, reserved = struct.unpack_from(
        "<4sHHIIQQ16sI12s", data, 0)
    assert (magic, version, flags, count, dv) == (b"GHPK", 1, 0, 1000, DV)
    assert rid == b"kathmandu_valley" and reserved == b"\0" * 12
    assert dir_size == count * DIR_ENTRY_SIZE and dir_off + dir_size == len(data)
    assert dir_crc == zlib.crc32(data[dir_off:])
    pos = PACK_HEADER_SIZE
    prev = -1
    for i in range(count):
        key, off, size, crc = struct.unpack_from("<QQII", data, dir_off + DIR_ENTRY_SIZE * i)
        assert key > prev and off == pos  # sorted, back to back in key order
        assert data[off:off + size] == tiles1000[key] and crc == zlib.crc32(tiles1000[key])
        prev, pos = key, off + size
    assert pos == dir_off


def test_byte_deterministic(tmp_path, tiles1000):
    a, b = tmp_path / "a.ghpk", tmp_path / "b.ghpk"
    sa = write_pack(a, "kathmandu_valley", DV, tiles1000)
    shuffled = dict(reversed(list(tiles1000.items())))
    sb = write_pack(b, "kathmandu_valley", DV, shuffled)
    assert a.read_bytes() == b.read_bytes() and sa["sha256"] == sb["sha256"]


def test_missing_key(pack1000, tiles1000):
    path, _ = pack1000
    absent = TileId(10, 1, 1).key
    assert absent not in tiles1000
    with PackReader(path) as pk:
        assert absent not in pk and -1 not in pk and (1 << 64) not in pk and "x" not in pk
        assert max(tiles1000) + 1 not in pk and 0 not in pk
        with pytest.raises(KeyError):
            pk.get(absent)
        with pytest.raises(KeyError):
            pk.entry(max(tiles1000) + 1)


def test_corrupt_tile_crc_detected(pack1000, tiles1000):
    path, _ = pack1000
    key = sorted(tiles1000)[500]
    with PackReader(path) as pk:
        off, size, _ = pk.entry(key)
    data = bytearray(path.read_bytes())
    data[off + size // 2] ^= 0xFF
    path.write_bytes(bytes(data))
    with PackReader(path) as pk:  # the directory is intact, so opening works
        with pytest.raises(ValueError, match="CRC"):
            pk.get(key)
        assert pk.get(key, verify=False) != tiles1000[key]
        other = sorted(tiles1000)[0]
        assert pk.get(other) == tiles1000[other]


def test_corrupt_directory_detected(pack1000):
    path, _ = pack1000
    data = bytearray(path.read_bytes())
    data[-3] ^= 0x01
    path.write_bytes(bytes(data))
    with pytest.raises(ValueError, match="directory CRC"):
        PackReader(path)


@pytest.mark.parametrize("patch, match", [
    (lambda d: d.__setitem__(slice(0, 4), b"GHPX"), "magic"),
    (lambda d: struct.pack_into("<H", d, 4, 2), "version"),
    (lambda d: struct.pack_into("<I", d, 8, 999), "tile count"),
    (lambda d: struct.pack_into("<Q", d, 16, 1 << 40), "outside"),
])
def test_bad_header_rejected(tmp_path, patch, match):
    path = tmp_path / "t.ghpk"
    write_pack(path, "thamel_test", DV, _tiles(5))
    data = bytearray(path.read_bytes())
    patch(data)
    path.write_bytes(bytes(data))
    with pytest.raises(ValueError, match=match):
        PackReader(path)


def test_unsorted_directory_rejected(tmp_path):
    path = tmp_path / "t.ghpk"
    write_pack(path, "thamel_test", DV, _tiles(3))
    data = bytearray(path.read_bytes())
    dir_off = struct.unpack_from("<Q", data, 16)[0]
    e0 = bytes(data[dir_off:dir_off + 24])
    data[dir_off:dir_off + 24] = data[dir_off + 24:dir_off + 48]
    data[dir_off + 24:dir_off + 48] = e0
    struct.pack_into("<I", data, 48, zlib.crc32(bytes(data[dir_off:])))
    path.write_bytes(bytes(data))
    with pytest.raises(ValueError, match="ascending"):
        PackReader(path)


def test_truncated_file_rejected(tmp_path):
    path = tmp_path / "t.ghpk"
    write_pack(path, "thamel_test", DV, _tiles(3))
    data = path.read_bytes()
    path.write_bytes(data[:-10])
    with pytest.raises(ValueError):
        PackReader(path)
    path.write_bytes(data[:20])
    with pytest.raises(ValueError, match="shorter"):
        PackReader(path)


def test_empty_pack(tmp_path):
    path = tmp_path / "empty.ghpk"
    stats = write_pack(path, "x", 3, {})
    assert stats["bytes"] == PACK_HEADER_SIZE and stats["tile_count"] == 0 and stats["tile_counts"] == {}
    with PackReader(path) as pk:
        assert pk.keys() == [] and len(pk) == 0 and pk.data_version == 3 and 5 not in pk


def test_region_id_rules(tmp_path):
    tiles = _tiles(2)
    stats = write_pack(tmp_path / "a.ghpk", "kathmandu_valley_extended", DV, tiles)
    assert stats["region_id"] == "kathmandu_valley"
    with PackReader(tmp_path / "a.ghpk") as pk:
        assert pk.region_id == "kathmandu_valley"
    for bad in ("काठमाडौं", "", "a\0b"):
        with pytest.raises(ValueError):
            write_pack(tmp_path / "b.ghpk", bad, DV, tiles)


def test_write_validates_tiles(tmp_path):
    rng = np.random.default_rng(0)
    t = TileId(10, 500, 300)
    with pytest.raises(ValueError, match="holds tile"):
        write_pack(tmp_path / "a.ghpk", "r", DV, {t.neighbor(1, 0).key: _tile_blob(t, rng)})
    with pytest.raises(ValueError, match="data_version"):
        write_pack(tmp_path / "a.ghpk", "r", DV, {t.key: _tile_blob(t, rng, dv=2)})
    with pytest.raises(ValueError, match="magic"):
        write_pack(tmp_path / "a.ghpk", "r", DV, {t.key: b"not a tile at all, just some bytes" * 2})
    assert not (tmp_path / "a.ghpk").exists() and not (tmp_path / "a.ghpk.tmp").exists()
    stats = write_pack(tmp_path / "a.ghpk", "r", DV, {t.key: b"raw"}, validate=False)
    assert stats["tile_count"] == 1


def test_closed_reader(pack1000, tiles1000):
    path, _ = pack1000
    pk = PackReader(path)
    k = next(iter(tiles1000))
    assert pk.get(k) == tiles1000[k]
    pk.close()
    pk.close()  # idempotent
    with pytest.raises(ValueError, match="closed"):
        pk.get(k)


def test_overwrite_is_atomic_replacement(tmp_path):
    path = tmp_path / "r.ghpk"
    write_pack(path, "r", DV, _tiles(3, seed=1))
    s2 = write_pack(path, "r", DV, _tiles(4, seed=2))
    with PackReader(path) as pk:
        assert pk.tile_count == 4
    assert sha256_file(path) == s2["sha256"]
    assert [p.name for p in tmp_path.iterdir()] == ["r.ghpk"]


# ---------------------------------------------------------------------------
# Manifest
# ---------------------------------------------------------------------------
def _manifest(stats: dict) -> dict:
    return {
        "region": "kathmandu_valley", "name": {"en": "Kathmandu Valley", "ne": "काठमाडौं उपत्यका"},
        "data_version": DV, "pipeline_version": "0.1.0", "built_at": "2026-10-04T19:00:00Z",
        "bbox_lonlat": [85.18, 27.55, 85.58, 27.83], "bbox_game": [418000.5, 147000.25, 457000.0, 178000.0],
        "detail_levels": [8, 9, 10], "horizon_levels": [5, 6, 7], "scale_model": "identity",
        "files": [{"path": "kathmandu_valley.ghpk", "bytes": stats["bytes"], "sha256": stats["sha256"]}],
        "tile_counts": stats["tile_counts"],
        "sources": {"osm": {"md5": "abc", "timestamp": "2026-10-02T19:34:00Z"}, "dem": [], "landcover": []},
        "attribution": ["© OpenStreetMap contributors"],
        "stats": {"roads": 0, "buildings": 0, "surface_tagged_pct": 15.2},
    }


def test_manifest_round_trip(tmp_path, pack1000):
    path, stats = pack1000
    m = _manifest(stats)
    mpath = tmp_path / "kathmandu_valley.manifest.json"
    write_manifest(mpath, m)
    back = read_manifest(mpath)
    assert back == {**m, "format": MANIFEST_FORMAT, "version": 1}
    assert "format" not in m  # input not mutated
    text = mpath.read_text(encoding="utf-8")
    assert "काठमाडौं उपत्यका" in text and "©" in text  # ensure_ascii=False
    assert text == json.dumps(back, ensure_ascii=False, indent=1) + "\n"  # key order as written
    assert text.splitlines()[1].startswith(' "attribution"')  # sorted keys, indent=1
    write_manifest(tmp_path / "again.json", dict(reversed(list(m.items()))))
    assert (tmp_path / "again.json").read_bytes() == mpath.read_bytes()
    assert back["files"][0] == file_entry(path)


def test_manifest_tile_counts_numeric_order(tmp_path):
    """Level keys sort numerically ("5" < "10"), every other dict lexicographically."""
    m = {"region": "r", "tile_counts": {"10": 1248, "5": 42, "9": 300, "6": 132},
         "stats": {"b": 1, "a": {"z": 0, "y": 1}, "chunk_bytes": {"ROAD": 1, "AREA": 2}}}
    write_manifest(tmp_path / "m.json", m)
    back = json.loads((tmp_path / "m.json").read_text(encoding="utf-8"))
    assert list(back["tile_counts"]) == ["5", "6", "9", "10"]
    assert list(back) == ["format", "region", "stats", "tile_counts", "version"]
    assert list(back["stats"]) == ["a", "b", "chunk_bytes"] and list(back["stats"]["a"]) == ["y", "z"]
    assert list(back["stats"]["chunk_bytes"]) == ["AREA", "ROAD"]


def test_manifest_validation(tmp_path):
    with pytest.raises(ValueError):
        write_manifest(tmp_path / "m.json", {"format": "something-else"})
    with pytest.raises(ValueError):
        write_manifest(tmp_path / "m.json", {"version": 2})
    with pytest.raises(ValueError):
        write_manifest(tmp_path / "m.json", {"x": float("nan")})
    (tmp_path / "bad.json").write_text('{"format": "other", "version": 1}', encoding="utf-8")
    with pytest.raises(ValueError, match="manifest"):
        read_manifest(tmp_path / "bad.json")
    (tmp_path / "v2.json").write_text(json.dumps({"format": MANIFEST_FORMAT, "version": 2}), encoding="utf-8")
    with pytest.raises(ValueError, match="version"):
        read_manifest(tmp_path / "v2.json")


def test_sha256_file_large(tmp_path):
    data = np.random.default_rng(0).bytes(3 * (1 << 20) + 17)
    p = tmp_path / "blob.bin"
    p.write_bytes(data)
    assert sha256_file(p) == hashlib.sha256(data).hexdigest()
    want = {"path": "x.bin", "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()}
    assert file_entry(p, name="x.bin") == want
