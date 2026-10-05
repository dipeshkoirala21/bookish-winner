"""GHT1 tile container: round trips, determinism, corruption and layout."""

from __future__ import annotations

import copy
import dataclasses
import struct
import zlib
from pathlib import Path

import numpy as np
import pytest
import shapely
from shapely.geometry import Point, Polygon

from ghumante_pipeline import geom
from ghumante_pipeline.binio import Reader, Writer, encode_varint, fnv1a32, fnv1a64
from ghumante_pipeline.model import (AreaKind, Biome, BuildingArchetype, BuildingFlags, BuildingUse, LineKind, NameRec,
                                     PoiFlags, PoiKind, RoadClass, RoadFlags, RoofMaterial, RoofShape, SacScale,
                                     Surface, SurfaceSource, Travel, WallMaterial)
from ghumante_pipeline.projection import TileId
from ghumante_pipeline.tile_format import (CHUNK_ENTRY_SIZE, CODEC_DEFLATE, CODEC_STORED, FOURCC_HGHT, FOURCC_NAME,
                                           FOURCC_POIS, FOURCC_SEED, H_MAX_M, HEADER_SIZE, I32_MAX, I32_MIN,
                                           KNOWN_FOURCCS, AreaFlags, AreaRec, BuildingRec, LineFlags, LineRec,
                                           NameEntry, NameTable, PoiRec, RoadRec, TileData, _read_points,
                                           _read_svarints, _read_varints, _svarints_bytes, _varints_bytes,
                                           _write_points, building_seed,
                                           canonical_ring, canonicalize, chunk_body, decode_tile, dequantize_heights,
                                           encode_chunks, encode_tile, from_local_cm, make_seed, osm_ref_nwr,
                                           osm_ref_wr, points_to_local_cm, quantize_heights, read_header, tile_seed,
                                           to_local_cm)

TILE = TileId(10, 500, 300)  # a 1024 m leaf tile in the Kathmandu Valley area
S_CM = int(TILE.size * 100)


# ---------------------------------------------------------------------------
# Synthetic tile builder
# ---------------------------------------------------------------------------
def _walk(rng, n, start=None, step=3000) -> np.ndarray:
    p0 = rng.integers(0, S_CM, 2) if start is None else np.asarray(start)
    return np.cumsum(np.vstack([p0, rng.integers(-step, step, (n - 1, 2))]), axis=0).astype(np.int64)


def _ring(rng, n, cx, cz, r) -> np.ndarray:
    ang = np.sort(rng.uniform(0, 2 * np.pi, n))
    rad = rng.uniform(0.6 * r, r, n)
    return np.stack([cx + rad * np.cos(ang), cz + rad * np.sin(ang)], axis=1).round().astype(np.int64)


NAMES = [NameRec("Durbar Marg", "Durbar Marg", "दरबार मार्ग"), NameRec("Bagmati", "Bagmati River", "बागमती नदी"),
         NameRec("बौद्धनाथ", "Boudhanath", "बौद्धनाथ"), NameRec("Thamel", "", ""), NameRec("", "", "")]


def make_tile(seed: int = 0, n_roads=40, n_lines=10, n_bldg=200, n_areas=12, n_pois=30, w2: bool = True) -> TileData:
    rng = np.random.default_rng(seed)
    names = NameTable()

    def nref():
        k = int(rng.integers(0, len(NAMES) + 2))
        return names.ref(NAMES[k]) if k < len(NAMES) else 0

    td = TileData(TILE, 1, has_detail=True)
    td.heights_q = quantize_heights(1300 + 50 * rng.standard_normal((129, 129)).cumsum(axis=1) / 10)
    td.biomes = rng.choice([int(b) for b in Biome], size=(65, 65)).astype(np.uint8)
    for i in range(n_roads):
        n = int(rng.integers(2, 60))
        flags = int(rng.integers(0, 256))
        pts = _walk(rng, n + bool(flags & RoadFlags.HAS_PREV_CTX) + bool(flags & RoadFlags.HAS_NEXT_CTX))
        td.roads.append(RoadRec(
            osm_way_id=int(rng.integers(1, 1 << 40)), road_class=RoadClass(int(rng.integers(0, 18))),
            surface=Surface(int(rng.integers(0, 15))), surface_source=SurfaceSource(int(rng.integers(0, 4))),
            flags=flags, lanes=int(rng.integers(0, 6)), sac_scale=SacScale(int(rng.integers(0, 7))),
            trail_visibility=int(rng.integers(0, 7)), layer=int(rng.integers(-5, 6)),
            width_cm=int(rng.integers(0, 3000)), access=int(rng.integers(0, 128)), name_ref=nref(),
            ref_ref=names.ref_str(["NH04", "F21", None][i % 3]), points=pts))
    for _ in range(n_lines):
        td.lines.append(LineRec(osm_way_id=int(rng.integers(1, 1 << 40)), kind=LineKind(int(rng.integers(0, 14))),
                                flags=int(rng.integers(0, 16)), width_cm=int(rng.integers(0, 5000)),
                                name_ref=nref(), points=_walk(rng, int(rng.integers(4, 200)), step=800)))
    for _ in range(n_bldg):
        c = rng.integers(0, S_CM, 2)
        rings = [_ring(rng, int(rng.integers(4, 12)), c[0], c[1], 800)]
        if rng.random() < 0.1:
            rings.append(_ring(rng, 4, c[0], c[1], 200)[::-1])
        td.buildings.append(BuildingRec(
            osm_ref=osm_ref_wr("w" if rng.random() < 0.95 else "r", int(rng.integers(1, 1 << 40))),
            archetype=BuildingArchetype(int(rng.integers(0, 20))), use=BuildingUse(int(rng.integers(0, 18))),
            levels=int(rng.integers(1, 9)), flags=int(rng.integers(0, 32)), height_cm=int(rng.integers(0, 4000)),
            min_height_cm=int(rng.integers(0, 2)) * 300, roof_shape=RoofShape(int(rng.integers(0, 15))),
            roof_material=RoofMaterial(int(rng.integers(0, 10))), wall_material=WallMaterial(int(rng.integers(0, 9))),
            name_ref=nref(), rings=rings))
    for _ in range(n_areas):
        c = rng.uniform(0, TILE.size, 2) + (TILE.x0, TILE.z0)
        shell = _ring(rng, int(rng.integers(5, 40)), c[0], c[1], 150).astype(np.float64)
        poly = shapely.make_valid(Polygon(shell))
        if rng.random() < 0.5:
            poly = poly.difference(Point(c).buffer(20, quad_segs=3))
        for part in geom.clip_polygon(poly, TILE.bounds):
            v, idx, rings = geom.triangulate(part)
            td.areas.append(AreaRec(osm_ref=osm_ref_wr("r", int(rng.integers(1, 1 << 30))),
                                    kind=AreaKind(int(rng.integers(0, 27))), flags=int(rng.integers(0, 2)),
                                    name_ref=nref(), vertices=points_to_local_cm(TILE, v), indices=idx, rings=rings))
    for _ in range(n_pois):
        has_ele = rng.random() < 0.3
        td.pois.append(PoiRec(osm_ref=osm_ref_nwr("nwr"[int(rng.integers(0, 3))], int(rng.integers(1, 1 << 40))),
                              kind=int(rng.choice([int(k) for k in PoiKind])),
                              flags=(int(rng.integers(0, 16)) & ~PoiFlags.HAS_ELE)
                              | (PoiFlags.HAS_ELE if has_ele else 0),
                              importance=int(rng.integers(0, 256)), x_cm=int(rng.integers(0, S_CM)),
                              z_cm=int(rng.integers(0, S_CM)), ele_dm=int(rng.integers(-1000, 88490)) if has_ele else 0,
                              name_ref=nref(), search_id=int(rng.integers(0, 100000))))
    if w2:
        from ghumante_pipeline.tile_format import BuildingFrontRec, JunctionRec, PropRec, RoadAttrRec

        for _ in td.roads:
            td.road_attrs.append(RoadAttrRec(
                area_type=int(rng.integers(0, 7)), sidewalk=int(rng.integers(0, 6)), lanes_fwd=int(rng.integers(0, 4)),
                lanes_bwd=int(rng.integers(0, 4)), maxspeed_kmh=int(rng.choice([0, 30, 40, 50])),
                flags=int(rng.integers(0, 256)), partner_way_id=int(rng.integers(0, 1 << 40)) * int(rng.random() < 0.2),
                median_cm=int(rng.integers(0, 400)),
                corridor_dm=rng.integers(0, 900, int(rng.integers(0, 30))).astype(np.int64)))
        for b in td.buildings:
            n = len(b.rings[0])
            fe = int(rng.integers(0, n)) if rng.random() < 0.8 else 255
            td.building_fronts.append(BuildingFrontRec(
                int(rng.integers(0, 13)), int(rng.integers(0, 7)), fe, int(rng.integers(0, 256)),
                int(rng.integers(0, 16)) | (0x80 if rng.random() < 0.3 else 0), int(rng.integers(0, 128)),
                int(rng.integers(0, n)) if rng.random() < 0.2 else 255))
        for _ in range(12):
            td.junctions.append(JunctionRec(
                osm_node_id=int(rng.integers(0, 1 << 40)), kind=int(rng.integers(0, 7)), arms=int(rng.integers(0, 7)),
                flags=int(rng.integers(0, 64)), x_cm=int(rng.integers(-500, S_CM + 500)),
                z_cm=int(rng.integers(-500, S_CM + 500)), ring_diameter_cm=int(rng.integers(0, 6000)),
                island_diameter_cm=int(rng.integers(0, 4000)), island_area_osm_ref=int(rng.integers(0, 1 << 30)),
                name_ref=nref()))
        for _ in range(40):
            yaw = rng.random() < 0.5
            td.props.append(PropRec(
                osm_ref=osm_ref_nwr("nw"[int(rng.integers(0, 2))], int(rng.integers(1, 1 << 40))),
                kind=int(rng.integers(0, 25)), subtype=int(rng.integers(0, 6)),
                flags=int(rng.integers(0, 32)) & ~1 | int(yaw), x_cm=int(rng.integers(0, S_CM)),
                z_cm=int(rng.integers(0, S_CM)), yaw_cdeg=int(rng.integers(0, 36000)) if yaw else 0,
                height_dm=int(rng.integers(0, 400)), name_ref=nref(), ref_ref=names.ref_str(["D7", "11", None][_ % 3])))
    td.names = names.entries()
    td.seed = make_seed(TILE, 1)
    td.meta = {"region": "kathmandu_valley", "sources": {"osm": "abc123", "dem": ["N27E085"], "landcover": "wc"},
               "name": "काठमाडौं"}
    return td


def _resolved(td: TileData, ref: int):
    e = td.name(ref)
    return None if e is None else (e.default, e.en, e.ne)


# ---------------------------------------------------------------------------
# Round trips
# ---------------------------------------------------------------------------
@pytest.mark.parametrize("seed", [0, 1, 2])
def test_round_trip_every_chunk(seed):
    td = make_tile(seed)
    blob = encode_tile(td)
    out = decode_tile(blob)
    assert out == canonicalize(td)
    assert {c.fourcc for c in read_header(blob).chunks} == set(KNOWN_FOURCCS)
    assert out.tile == TILE and out.data_version == 1 and out.has_detail
    assert np.array_equal(out.heights_q, td.heights_q) and out.heights_q.dtype == np.uint16
    assert np.array_equal(out.biomes, td.biomes) and out.biomes.dtype == np.uint8
    assert out.seed == (tile_seed(TILE.key, 1), 1)
    assert out.meta == td.meta


def test_round_trip_preserves_content_against_input():
    """Compare decoded records with the *input* records (not just canonicalize)."""
    td = make_tile(5)
    out = decode_tile(encode_tile(td))
    by_id = {(r.osm_way_id, r.points.tobytes()): r for r in td.roads}
    assert len(out.roads) == len(td.roads)
    for r in out.roads:
        src = by_id[(r.osm_way_id, r.points.tobytes())]
        for f in ("road_class", "surface", "surface_source", "flags", "lanes", "sac_scale", "trail_visibility",
                  "layer", "width_cm", "access"):
            assert getattr(r, f) == getattr(src, f), f
        assert _resolved(out, r.name_ref) == _resolved(td, src.name_ref)
        assert _resolved(out, r.ref_ref) == _resolved(td, src.ref_ref)
    src_b = {b.osm_ref: b for b in td.buildings}
    for b in out.buildings:
        s = src_b[b.osm_ref]
        assert b.seed == building_seed(b.osm_ref)
        assert (b.levels, b.use, b.archetype, b.height_cm, b.roof_shape) == (s.levels, s.use, s.archetype,
                                                                            s.height_cm, s.roof_shape)
        assert sorted(map(tuple, b.rings[0].tolist())) == sorted(map(tuple, s.rings[0].tolist()))
        assert _resolved(out, b.name_ref) == _resolved(td, s.name_ref)
    src_p = {(p.osm_ref, p.kind): p for p in td.pois}
    for p in out.pois:
        s = src_p[(p.osm_ref, p.kind)]
        assert (p.x_cm, p.z_cm, p.ele_dm, p.flags, p.importance, p.search_id) == (s.x_cm, s.z_cm, s.ele_dm, s.flags,
                                                                                 s.importance, s.search_id)


def test_records_sorted_as_spec_requires():
    out = decode_tile(encode_tile(make_tile(3)))
    assert [(r.osm_way_id, *r.points[0].tolist()) for r in out.roads] == sorted(
        (r.osm_way_id, *r.points[0].tolist()) for r in out.roads)
    assert [(r.osm_way_id, *r.points[0].tolist()) for r in out.lines] == sorted(
        (r.osm_way_id, *r.points[0].tolist()) for r in out.lines)
    assert [b.osm_ref for b in out.buildings] == sorted(b.osm_ref for b in out.buildings)
    assert [(a.osm_ref, *a.vertices[0].tolist()) for a in out.areas] == sorted(
        (a.osm_ref, *a.vertices[0].tolist()) for a in out.areas)
    assert [(p.osm_ref, p.kind) for p in out.pois] == sorted((p.osm_ref, p.kind) for p in out.pois)


def test_canonicalize_is_idempotent_and_pure():
    td = make_tile(4)
    before = copy.deepcopy(td)
    c1 = canonicalize(td)
    assert canonicalize(c1) == c1
    assert td == before  # input untouched
    assert encode_tile(c1) == encode_tile(td)


def test_horizon_tile_has_only_hght_and_biom():
    rng = np.random.default_rng(9)
    td = TileData(TileId(6, 20, 10), 1, heights_q=quantize_heights(rng.uniform(100, 8000, (129, 129))),
                  biomes=rng.integers(0, 28, (65, 65)).astype(np.uint8))
    blob = encode_tile(td)
    h = read_header(blob)
    assert [c.fourcc for c in h.chunks] == [b"BIOM", b"HGHT"] and not h.has_detail
    out = decode_tile(blob)
    assert out == canonicalize(td)
    assert out.roads == [] and out.names == [] and out.seed is None and out.meta is None


def test_empty_tile():
    td = TileData(TileId(0, 0, 0), 7)
    blob = encode_tile(td)
    assert len(blob) == HEADER_SIZE
    assert decode_tile(blob) == td


# ---------------------------------------------------------------------------
# Determinism
# ---------------------------------------------------------------------------
def test_encoding_is_byte_deterministic():
    td = make_tile(11)
    assert encode_tile(td) == encode_tile(td)
    assert encode_tile(make_tile(11)) == encode_tile(td)


def test_encoding_independent_of_record_and_name_order():
    td = make_tile(12)
    rng = np.random.default_rng(1)
    # Rebuild the name table in a different insertion order and shuffle every record list.
    perm = rng.permutation(len(td.names))
    new_names = [td.names[i] for i in perm]
    new_ref = {int(old) + 1: new + 1 for new, old in enumerate(perm)}
    new_ref[0] = 0

    def remap(rec, *fields):
        return dataclasses.replace(rec, **{f: new_ref[getattr(rec, f)] for f in fields})

    rp = rng.permutation(len(td.roads))
    bp = rng.permutation(len(td.buildings))
    shuffled = dataclasses.replace(
        td, names=new_names + [NameEntry("unused", "", "")],
        roads=[remap(td.roads[i], "name_ref", "ref_ref") for i in rp],
        road_attrs=[td.road_attrs[i] for i in rp],  # parallel lists move with their records
        lines=[remap(td.lines[i], "name_ref") for i in rng.permutation(len(td.lines))],
        buildings=[remap(td.buildings[i], "name_ref") for i in bp],
        building_fronts=[td.building_fronts[i] for i in bp],
        areas=[remap(td.areas[i], "name_ref") for i in rng.permutation(len(td.areas))],
        pois=[remap(td.pois[i], "name_ref") for i in rng.permutation(len(td.pois))],
        junctions=[remap(td.junctions[i], "name_ref") for i in rng.permutation(len(td.junctions))],
        props=[remap(td.props[i], "name_ref", "ref_ref") for i in rng.permutation(len(td.props))],
        meta=dict(reversed(list(td.meta.items()))))
    assert encode_tile(shuffled) == encode_tile(td)


def test_sort_ties_broken_by_content():
    pts = np.array([[0, 0], [100, 100]])
    a = RoadRec(osm_way_id=7, road_class=RoadClass.PRIMARY, points=pts)
    b = RoadRec(osm_way_id=7, road_class=RoadClass.SECONDARY, points=pts)
    t1 = TileData(TILE, 1, roads=[a, b])
    t2 = TileData(TILE, 1, roads=[b, a])
    assert encode_tile(t1) == encode_tile(t2)


# ---------------------------------------------------------------------------
# Layout
# ---------------------------------------------------------------------------
def test_header_and_chunk_table_layout():
    td = make_tile(6)
    blob = encode_tile(td)
    magic, version, flags, level, tx, ty, dv, count, res2, crc = struct.unpack_from("<4sHHB3xIIIHHI", blob, 0)
    assert (magic, version, flags, level, tx, ty, dv, res2) == (b"GHT1", 1, 1, 10, 500, 300, 1, 0)
    assert blob[9:12] == b"\0\0\0"
    payload_start = HEADER_SIZE + CHUNK_ENTRY_SIZE * count
    assert crc == zlib.crc32(blob[payload_start:])
    entries = [struct.unpack_from("<4sB3sII", blob, HEADER_SIZE + CHUNK_ENTRY_SIZE * k) for k in range(count)]
    assert [e[0] for e in entries] == sorted(e[0] for e in entries)
    pos = payload_start
    for fourcc, codec, reserved, offset, size in entries:
        assert reserved == b"\0\0\0" and offset == pos and codec in (CODEC_STORED, CODEC_DEFLATE)
        raw = blob[offset:offset + size]
        if codec == CODEC_DEFLATE:
            raw = zlib.decompress(raw, -15)
        (raw_size,) = struct.unpack_from("<I", raw)
        assert raw_size == len(raw) - 4
        pos += size
    assert pos == len(blob)


def test_codec_choice_is_size_based():
    td = make_tile(7)
    h = read_header(encode_tile(td))
    codecs = {c.fourcc: c.codec for c in h.chunks}
    assert codecs[FOURCC_SEED] == CODEC_STORED  # 14 bytes never shrink
    assert codecs[FOURCC_HGHT] == CODEC_DEFLATE


def test_golden_tile_bytes():
    """A tiny tile pinned byte for byte (also a fixture for the C# reader)."""
    names = NameTable()
    td = TileData(TILE, 1, seed=make_seed(TILE, 1), has_detail=True, pois=[PoiRec(
        osm_ref=osm_ref_nwr("n", 42), kind=PoiKind.PEAK, flags=int(PoiFlags.HAS_ELE | PoiFlags.DISCOVERABLE),
        importance=200, x_cm=51200, z_cm=-3, ele_dm=88488,
        name_ref=names.ref(NameEntry("सगरमाथा", "Everest", "सगरमाथा")), search_id=7)])
    td.names = names.entries()
    golden = bytes.fromhex(
        "47485431010001000a000000f40100002c0100000100000003000000884bdd184e414d45010000005000000028000000"
        "504f495300000000780000001400000053454544000000008c0000000e0000003365606060147db064c78325d31f2cd9"
        "f060c9ba074bf63d58b21448b2bb96a516a51697609705001000000001a801c80005c880a00605d0e60a01070a000000"
        "94fc6917439f82ae0100")
    assert encode_tile(td) == golden
    out = decode_tile(golden)
    assert out.pois[0].ele_dm == 88488 and out.names == [NameEntry("सगरमाथा", "Everest", "सगरमाथा")]


def test_poi_record_bytes():
    td = TileData(TILE, 1, pois=[PoiRec(osm_ref=5, kind=300, flags=1, importance=9, x_cm=-1, z_cm=64, search_id=3)])
    blob = encode_tile(td)
    info = next(c for c in read_header(blob).chunks if c.fourcc == FOURCC_POIS)
    body = chunk_body(blob, info)
    assert body == bytes([1, 5]) + struct.pack("<HBB", 300, 1, 9) + bytes([1, 0x80, 0x01, 0, 0, 3])


# ---------------------------------------------------------------------------
# Corruption and compatibility
# ---------------------------------------------------------------------------
def test_payload_crc_corruption_detected():
    blob = bytearray(encode_tile(make_tile(8)))
    blob[-5] ^= 0x40
    with pytest.raises(ValueError, match="CRC"):
        decode_tile(bytes(blob))


@pytest.mark.parametrize("cut", [0, 10, HEADER_SIZE + 3, -1])
def test_truncated_tile_rejected(cut):
    blob = encode_tile(make_tile(8, n_bldg=10))
    with pytest.raises(ValueError):
        decode_tile(blob[:cut])


def test_bad_magic_rejected():
    blob = bytearray(encode_tile(make_tile(8, n_bldg=5)))
    blob[0:4] = b"GHT2"
    with pytest.raises(ValueError, match="magic"):
        decode_tile(bytes(blob))


def test_version_mismatch_rejected():
    blob = bytearray(encode_tile(make_tile(8, n_bldg=5)))
    struct.pack_into("<H", blob, 4, 2)
    with pytest.raises(ValueError, match="version"):
        decode_tile(bytes(blob))


def test_chunk_offset_out_of_range_rejected():
    blob = bytearray(encode_tile(make_tile(8, n_bldg=5)))
    struct.pack_into("<I", blob, HEADER_SIZE + 8, len(blob))
    with pytest.raises(ValueError, match="outside"):
        decode_tile(bytes(blob))


def _crafted(chunks, has_detail=False):
    return encode_chunks(chunks, TILE, 1, has_detail)


def test_unknown_chunks_are_skipped():
    td = make_tile(13, n_bldg=20)
    c = canonicalize(td)
    blob = encode_tile(td)
    h = read_header(blob)
    chunks = [(i.fourcc, chunk_body(blob, i)) for i in h.chunks]
    extra = [(b"AAAA", b"\x01\x02\x03" * 50), (b"ZZZZ", b"future chunk"), (b"Hght", b"\xff")]
    crafted = _crafted(chunks + extra, has_detail=True)
    assert len(read_header(crafted).chunks) == len(chunks) + 3
    assert decode_tile(crafted) == c


def test_unknown_chunk_with_unknown_codec_is_skipped():
    blob = bytearray(_crafted([(b"XTRA", b"x" * 100), (FOURCC_SEED, Writer().u64(5).u16(1).bytes())]))
    h = read_header(bytes(blob))
    k = [c.fourcc for c in h.chunks].index(b"XTRA")
    blob[HEADER_SIZE + CHUNK_ENTRY_SIZE * k + 4] = 9  # codec 9 (table is outside the CRC)
    assert decode_tile(bytes(blob)).seed == (5, 1)


def test_known_chunk_with_unknown_codec_rejected():
    blob = bytearray(_crafted([(FOURCC_SEED, Writer().u64(5).u16(1).bytes())]))
    blob[HEADER_SIZE + 4] = 9
    with pytest.raises(ValueError, match="codec"):
        decode_tile(bytes(blob))


def test_raw_size_mismatch_rejected():
    body = Writer().u64(0x9E3779B97F4A7C15).u16(0xBEEF).bytes()  # incompressible: stored
    blob = bytearray(_crafted([(FOURCC_SEED, body)]))
    info = read_header(bytes(blob)).chunks[0]
    assert info.codec == CODEC_STORED
    off = info.offset
    struct.pack_into("<I", blob, off, len(body) + 1)
    struct.pack_into("<I", blob, 28, zlib.crc32(bytes(blob[HEADER_SIZE + CHUNK_ENTRY_SIZE:])))
    with pytest.raises(ValueError, match="raw_size"):
        decode_tile(bytes(blob))


def test_trailing_bytes_in_chunk_rejected():
    with pytest.raises(ValueError, match="trailing"):
        decode_tile(_crafted([(FOURCC_SEED, Writer().u64(5).u16(1).u8(0).bytes())]))


def test_truncated_record_chunk_rejected():
    td = make_tile(14, n_bldg=3)
    blob = encode_tile(td)
    h = read_header(blob)
    chunks = [(i.fourcc, chunk_body(blob, i)) for i in h.chunks]
    chunks = [(f, b[:-3] if f == b"ROAD" else b) for f, b in chunks]
    with pytest.raises(ValueError, match="ROAD"):
        decode_tile(_crafted(chunks))


def test_name_ref_beyond_table_rejected_on_decode():
    names = Writer().varint(1).str("a").str("").str("").bytes()
    pois = Writer().varint(1).varint(4).u16(200).u8(0).u8(0).svarint(0).svarint(0).svarint(0).varint(2).varint(0)
    with pytest.raises(ValueError, match="name_ref"):
        decode_tile(_crafted([(FOURCC_NAME, names), (FOURCC_POIS, pois.bytes())]))


@pytest.mark.parametrize("body", [Writer().u16(0).u16(0).f32(-100.0).f32(0.15).bytes(),
                                  Writer().u16(4).u16(0).f32(-100.0).f32(0.15).raw(b"\0" * 32).bytes(),
                                  Writer().u16(3).u16(0).f32(-50.0).f32(0.15).raw(b"\0" * 18).bytes()])
def test_bad_hght_header_rejected(body):
    with pytest.raises(ValueError, match="HGHT"):
        decode_tile(_crafted([(FOURCC_HGHT, body)]))


def test_meta_must_be_an_object():
    with pytest.raises(ValueError, match="META"):
        decode_tile(_crafted([(b"META", Writer().str("[1, 2]").bytes())]))
    with pytest.raises(TypeError):
        encode_tile(TileData(TILE, 1, meta=[1, 2]))


def test_mutated_chunk_bodies_only_raise_value_error():
    """Decoder robustness: corrupt chunk bodies with a valid CRC either decode
    or raise ValueError, never anything else (no IndexError, MemoryError...)."""
    td = make_tile(1, n_roads=8, n_lines=4, n_bldg=15, n_areas=3, n_pois=6)
    td.heights_q, td.biomes = td.heights_q[:9, :9], td.biomes[:5, :5]  # keep re-deflating cheap
    blob = encode_tile(td)
    h = read_header(blob)
    bodies = [(c.fourcc, chunk_body(blob, c)) for c in h.chunks]
    rng = np.random.default_rng(0)
    for _ in range(800):
        k = int(rng.integers(len(bodies)))
        f, b = bodies[k]
        b = bytearray(b)
        mode = int(rng.integers(3))
        if mode == 0:
            for _ in range(int(rng.integers(1, 4))):
                b[int(rng.integers(len(b)))] = int(rng.integers(256))
        elif mode == 1:
            b = b[:int(rng.integers(len(b)))]
        else:
            pos = int(rng.integers(len(b)))
            b[pos:pos] = rng.bytes(int(rng.integers(1, 6)))
        chunks = list(bodies)
        chunks[k] = (f, bytes(b))
        try:
            decode_tile(encode_chunks(chunks, h.tile, 1, True))
        except ValueError:
            pass


def test_bad_deflate_stream_rejected():
    blob = bytearray(encode_tile(make_tile(15, n_bldg=0)))
    info = next(c for c in read_header(bytes(blob)).chunks if c.fourcc == FOURCC_HGHT)
    blob[info.offset:info.offset + 8] = b"\xff" * 8
    struct.pack_into("<I", blob, 28, zlib.crc32(bytes(blob[HEADER_SIZE + CHUNK_ENTRY_SIZE * len(
        read_header(bytes(blob)).chunks):])))
    with pytest.raises(ValueError):
        decode_tile(bytes(blob))


# ---------------------------------------------------------------------------
# Heights
# ---------------------------------------------------------------------------
def test_height_quantisation_edges():
    h = np.array([-100.0, 9730.25, -100.08, -1e9, 1e9, np.inf, -np.inf, 0.0, 8848.86, 9730.4, 9730.3])
    q = quantize_heights(h)
    assert q.dtype == np.uint16
    assert q.tolist() == [0, 65535, 0, 0, 65535, 65535, 0, 667, 59659, 65535, 65535]
    d = dequantize_heights(q)
    assert d.dtype == np.float32
    assert d[0] == np.float32(-100.0) and d[1] == np.float32(9730.25) and H_MAX_M == 9730.25
    assert abs(float(d[8]) - 8848.86) <= 0.075


def test_height_quantisation_error_bound():
    h = np.random.default_rng(0).uniform(-100, 9730.25, 100000)
    assert np.abs(dequantize_heights(quantize_heights(h)) - h).max() <= 0.075 + 1e-3


def test_height_nan_rejected():
    with pytest.raises(ValueError):
        quantize_heights(np.array([1.0, np.nan]))


def test_hght_prediction_filter_matches_spec():
    rng = np.random.default_rng(3)
    q = rng.integers(0, 65536, (17, 17)).astype(np.uint16)
    q[0, :3] = [65535, 0, 65535]  # wrap-around residuals
    blob = encode_tile(TileData(TILE, 1, heights_q=q))
    body = chunk_body(blob, read_header(blob).chunks[0])
    r = Reader(body)
    n, res, h_min, h_step = r.u16(), r.u16(), r.f32(), r.f32()
    assert (n, res, h_min, h_step) == (17, 0, np.float32(-100.0), np.float32(0.15))
    resid = [r.u16() for _ in range(n * n)]
    assert r.remaining() == 0
    for j in range(n):
        for i in range(n):
            pred = int(q[j, i - 1]) if i > 0 else (int(q[j - 1, 0]) if j > 0 else 0)
            assert resid[j * n + i] == (int(q[j, i]) - pred) % 65536
    assert np.array_equal(decode_tile(blob).heights_q, q)


@pytest.mark.parametrize("bad", [np.zeros((128, 128), np.uint16), np.zeros((129, 65), np.uint16),
                                 np.full((3, 3), 70000), np.zeros((3, 3), np.float32)])
def test_bad_height_grid_rejected(bad):
    with pytest.raises((ValueError, TypeError)):
        encode_tile(TileData(TILE, 1, heights_q=bad))


# ---------------------------------------------------------------------------
# Coordinates and seeds
# ---------------------------------------------------------------------------
def test_to_local_cm_border_points():
    east = TILE.neighbor(1, 0)
    north = TILE.neighbor(0, 1)
    x_border, z = east.x0, TILE.z0 + 123.456789
    xa, za = to_local_cm(TILE, np.array([x_border]), np.array([z]))
    xb, zb = to_local_cm(east, np.array([x_border]), np.array([z]))
    assert (int(xa[0]), int(xb[0])) == (S_CM, 0) and int(za[0]) == int(zb[0]) == 12346
    _, za = to_local_cm(TILE, np.array([TILE.x0]), np.array([north.z0]))
    _, zb = to_local_cm(north, np.array([TILE.x0]), np.array([north.z0]))
    assert (int(za[0]), int(zb[0])) == (S_CM, 0)
    pts = np.array([[TILE.x0 + 1.004, TILE.z0 - 2.5]])
    cm = points_to_local_cm(TILE, pts)
    assert cm.dtype == np.int64 and cm.tolist() == [[100, -250]]
    assert np.allclose(from_local_cm(TILE, cm), [[TILE.x0 + 1.0, TILE.z0 - 2.5]])


def test_shared_cut_point_identical_in_both_tiles():
    """geom + tile_format: a road crossing a border encodes the same cut point
    on both sides (S*100 / 0 and an identical z)."""
    east = TILE.neighbor(1, 0)
    rng = np.random.default_rng(21)
    for _ in range(200):
        a = np.array([TILE.x0 + rng.uniform(0, TILE.size), TILE.z0 + rng.uniform(1, TILE.size - 1)])
        b = np.array([east.x0 + rng.uniform(0, east.size), east.z0 + rng.uniform(1, east.size - 1)])
        (pa,), (pb,) = geom.clip_polyline([a, b], TILE.bounds), geom.clip_polyline([a, b], east.bounds)
        ca, cb = points_to_local_cm(TILE, pa.points), points_to_local_cm(east, pb.points)
        assert ca[-2, 0] == S_CM and cb[1, 0] == 0 and ca[-2, 1] == cb[1, 1]
        roads = []
        for tile, piece, cm in ((TILE, pa, ca), (east, pb, cb)):
            flags = (RoadFlags.HAS_PREV_CTX if piece.has_prev_ctx else 0) | (
                RoadFlags.HAS_NEXT_CTX if piece.has_next_ctx else 0)
            out = decode_tile(encode_tile(TileData(tile, 1, roads=[RoadRec(osm_way_id=1, flags=int(flags),
                                                                           points=cm)])))
            roads.append(out.roads[0].points)
        assert roads[0][-2, 1] == roads[1][1, 1]


def test_coordinate_extremes_round_trip():
    ext = np.array([[I32_MAX, I32_MIN], [I32_MIN, I32_MAX], [0, 0], [-1, 1], [I32_MAX, I32_MAX], [I32_MIN, I32_MIN],
                    [63, -64], [64, -65], [8191, -8192]], dtype=np.int64)
    td = TileData(TILE, 1, roads=[RoadRec(osm_way_id=(1 << 64) - 1, points=ext)],
                  lines=[LineRec(osm_way_id=0, points=ext[::-1].copy())],
                  buildings=[BuildingRec(osm_ref=(1 << 63) + 1, rings=[ext[:3]])],
                  areas=[AreaRec(osm_ref=1, vertices=ext, indices=np.array([0, 1, 2, 3, 4, 5]), rings=[(0, 9)])],
                  pois=[PoiRec(osm_ref=(1 << 64) - 1, kind=65535, x_cm=I32_MIN, z_cm=I32_MAX, ele_dm=0)])
    out = decode_tile(encode_tile(td))
    assert out == canonicalize(td)
    assert np.array_equal(out.roads[0].points, ext) and out.roads[0].osm_way_id == (1 << 64) - 1
    assert out.pois[0].x_cm == I32_MIN and out.pois[0].z_cm == I32_MAX


@pytest.mark.parametrize("pts", [np.array([[I32_MAX + 1, 0], [0, 0]]), np.array([[0, I32_MIN - 1], [0, 0]])])
def test_coordinates_outside_i32_rejected(pts):
    with pytest.raises(ValueError, match="i32"):
        encode_tile(TileData(TILE, 1, roads=[RoadRec(osm_way_id=1, points=pts)]))


def test_float_coordinates_rejected():
    with pytest.raises(TypeError):
        encode_tile(TileData(TILE, 1, roads=[RoadRec(osm_way_id=1, points=np.array([[0.5, 1.0], [2.0, 3.0]]))]))


def test_vectorised_varints_match_binio():
    rng = np.random.default_rng(0)
    vals = np.concatenate([rng.integers(-(1 << 63), (1 << 63) - 1, 3000, dtype=np.int64),
                           np.array([0, -1, 1, 63, -64, 64, -65, -(1 << 63), (1 << 63) - 1], dtype=np.int64)])
    for chunk in (vals, vals[:5], vals[-9:]):  # numpy path and small-array path
        ref = b"".join(Writer().svarint(int(v)).bytes() for v in chunk)
        assert _svarints_bytes(chunk) == ref
        r = Reader(ref + b"\x07")
        assert np.array_equal(_read_svarints(r, len(chunk)), chunk) and r.remaining() == 1
    u = np.array([0, 127, 128, 16383, 16384, (1 << 64) - 1, 1 << 63], dtype=np.uint64)
    assert _varints_bytes(u) == b"".join(encode_varint(int(x)) for x in u)
    assert np.array_equal(_read_varints(Reader(_varints_bytes(u)), len(u)), u)


@pytest.mark.parametrize("n", [1, 2, 23, 24, 25, 60, 500])
def test_point_coding_matches_spec(n):
    """First point absolute, then deltas, as svarint pairs (both code paths)."""
    pts = np.random.default_rng(n).integers(-(1 << 31), 1 << 31, (n, 2))
    ref = Writer()
    prev = (0, 0)
    for x, z in pts.tolist():
        ref.svarint(x - prev[0]).svarint(z - prev[1])
        prev = (x, z)
    w = Writer()
    _write_points(w, pts)
    assert w.bytes() == ref.bytes()
    r = Reader(w.bytes())
    assert np.array_equal(_read_points(r, n), pts) and r.remaining() == 0


@pytest.mark.parametrize("data", [b"\x80" * 11, b"\xff" * 9 + b"\x02", b"\x80\x80"])
def test_vectorised_varint_errors(data):
    with pytest.raises(ValueError):
        _read_varints(Reader(data), 1)


def test_seed_values():
    assert TILE.key == 0x2800000000035DB0
    assert tile_seed(TILE.key, 1) == fnv1a64(struct.pack("<QI", TILE.key, 1)) == 0xAE829F431769FC94
    ref = osm_ref_wr("w", 123456789)
    assert ref == 246913578
    assert building_seed(ref) == fnv1a32(encode_varint(ref)) == 0x4B778F2E
    assert osm_ref_wr("r", 5) == 11 and osm_ref_nwr("n", 5) == 20 and osm_ref_nwr("w", 5) == 21
    assert osm_ref_nwr("r", 5) == 22
    assert make_seed(TILE, 1) == (tile_seed(TILE.key, 1), 1)


# ---------------------------------------------------------------------------
# Names and canonical geometry
# ---------------------------------------------------------------------------
def test_name_table():
    t = NameTable()
    assert t.ref(None) == 0 and t.ref(NameRec()) == 0 and t.ref_str(None) == 0 and t.ref_str("") == 0
    a = t.ref(NameRec("Patan", "Lalitpur", "पाटन", alt=("Yala",)))
    assert a == 1 and t.ref(NameEntry("Patan", "Lalitpur", "पाटन")) == 1
    assert t.ref_str("NH04") == 2 and t.ref_str("NH04") == 2
    decomposed = "Café"
    assert t.ref(NameEntry(decomposed)) == t.ref(NameEntry("Café")) == 3
    assert t.entries() == [NameEntry("Patan", "Lalitpur", "पाटन"), NameEntry("NH04"), NameEntry("Café")]
    assert len(t) == 3


def test_names_remapped_and_unused_dropped():
    names = [NameEntry("unused"), NameEntry("B"), NameEntry("A"), NameEntry("B")]
    td = TileData(TILE, 1, names=names, pois=[PoiRec(osm_ref=8, name_ref=4), PoiRec(osm_ref=4, name_ref=3),
                                              PoiRec(osm_ref=12, name_ref=2)])
    out = decode_tile(encode_tile(td))
    assert out.names == [NameEntry("A"), NameEntry("B")]
    assert [(p.osm_ref, p.name_ref) for p in out.pois] == [(4, 1), (8, 2), (12, 2)]


def test_name_ref_out_of_range_rejected_on_encode():
    with pytest.raises(ValueError, match="name_ref"):
        encode_tile(TileData(TILE, 1, pois=[PoiRec(osm_ref=1, name_ref=1)]))


def test_building_rings_canonicalised():
    cw_closed = np.array([[0, 0], [0, 1000], [1000, 1000], [1000, 0], [0, 0]])
    hole_ccw = np.array([[200, 200], [400, 200], [400, 400], [200, 400]])
    td = TileData(TILE, 1, buildings=[BuildingRec(osm_ref=10, rings=[cw_closed, hole_ccw])])
    b = decode_tile(encode_tile(td)).buildings[0]
    assert b.rings[0].tolist() == [[0, 0], [1000, 0], [1000, 1000], [0, 1000]]
    assert b.rings[1].tolist() == [[200, 200], [200, 400], [400, 400], [400, 200]]
    assert geom.signed_area(b.rings[0]) > 0 > geom.signed_area(b.rings[1])
    assert b.seed == building_seed(10)
    assert canonical_ring(np.array([[0, 0], [5, 0], [10, 0]]), outer=True).tolist() == [[0, 0], [5, 0], [10, 0]]


def test_area_triangles_rewound_ccw():
    v = np.array([[0, 0], [100, 0], [100, 100], [0, 100]])
    td = TileData(TILE, 1, areas=[AreaRec(osm_ref=2, kind=AreaKind.WATER_LAKE, flags=AreaFlags.CLIPPED_BY_TILE,
                                          vertices=v, indices=np.array([0, 2, 1, 0, 3, 2]), rings=[(0, 4)])])
    a = decode_tile(encode_tile(td)).areas[0]
    assert a.indices.tolist() == [0, 1, 2, 0, 2, 3] and a.rings == [(0, 4)]
    assert geom.triangles_area(a.vertices, a.indices) == 100 * 100


@pytest.mark.parametrize("bad", [
    TileData(TILE, 1, buildings=[BuildingRec(osm_ref=1, levels=0, rings=[np.array([[0, 0], [1, 0], [0, 1]])])]),
    TileData(TILE, 1, buildings=[BuildingRec(osm_ref=1, rings=[np.array([[0, 0], [1, 0]])])]),
    TileData(TILE, 1, buildings=[BuildingRec(osm_ref=1, rings=[])]),
    TileData(TILE, 1, roads=[RoadRec(osm_way_id=1, points=np.array([[0, 0]]))]),
    TileData(TILE, 1, roads=[RoadRec(osm_way_id=1, flags=int(RoadFlags.HAS_PREV_CTX),
                                     points=np.array([[0, 0], [1, 1]]))]),
    TileData(TILE, 1, lines=[LineRec(osm_way_id=1, flags=int(LineFlags.HAS_NEXT_CTX | LineFlags.HAS_PREV_CTX),
                                     points=np.array([[0, 0], [1, 1], [2, 2]]))]),
    TileData(TILE, 1, roads=[RoadRec(osm_way_id=1, road_class=300, points=np.array([[0, 0], [1, 1]]))]),
    TileData(TILE, 1, roads=[RoadRec(osm_way_id=-1, points=np.array([[0, 0], [1, 1]]))]),
    TileData(TILE, 1, roads=[RoadRec(osm_way_id=1, layer=200, points=np.array([[0, 0], [1, 1]]))]),
    TileData(TILE, 1, pois=[PoiRec(osm_ref=1, ele_dm=50)]),
    TileData(TILE, 1, pois=[PoiRec(osm_ref=1, x_cm=1 << 40)]),
    TileData(TILE, 1, areas=[AreaRec(osm_ref=1, vertices=np.array([[0, 0], [1, 0], [0, 1]]),
                                     indices=np.array([0, 1]))]),
    TileData(TILE, 1, areas=[AreaRec(osm_ref=1, vertices=np.array([[0, 0], [1, 0], [0, 1]]),
                                     indices=np.array([0, 1, 3]))]),
    TileData(TILE, 1, areas=[AreaRec(osm_ref=1, vertices=np.array([[0, 0], [1, 0], [0, 1]]),
                                     indices=np.array([0, 1, 2]), rings=[(1, 3)])]),
    TileData(TILE, 1, seed=(1 << 64, 1)),
])
def test_invalid_records_rejected(bad):
    with pytest.raises((ValueError, TypeError)):
        encode_tile(bad)


def test_poi_elevation_with_flag():
    p = PoiRec(osm_ref=osm_ref_nwr("n", 1), kind=PoiKind.PEAK, flags=int(PoiFlags.HAS_ELE), ele_dm=-35)
    assert decode_tile(encode_tile(TileData(TILE, 1, pois=[p]))).pois == [p]


def test_road_flags_and_enums_survive():
    p = RoadRec(osm_way_id=99, road_class=RoadClass.TRACK, surface=Surface.MUD, surface_source=SurfaceSource.INFERRED,
                flags=int(RoadFlags.ONEWAY | RoadFlags.SAC_INFERRED | RoadFlags.FORD), lanes=255,
                sac_scale=SacScale.DIFFICULT_ALPINE_HIKING, trail_visibility=6, layer=-128, width_cm=(1 << 64) - 1,
                access=int(Travel.FOOT | Travel.HORSE), points=np.array([[5, 5], [6, 6]]))
    assert decode_tile(encode_tile(TileData(TILE, 1, roads=[p]))).roads == [p]


def test_building_extremes():
    b = BuildingRec(osm_ref=1, archetype=BuildingArchetype.TEAHOUSE, levels=255, flags=int(BuildingFlags.LANDMARK),
                    height_cm=(1 << 40), min_height_cm=0, seed=0xFFFFFFFF,
                    rings=[np.array([[0, 0], [10, 0], [0, 10]])])
    assert decode_tile(encode_tile(TileData(TILE, 1, buildings=[b]))).buildings == [b]


# ---------------------------------------------------------------------------
# Real data
# ---------------------------------------------------------------------------
@pytest.mark.realdata
def test_real_dem_window_round_trip():
    rasterio = pytest.importorskip("rasterio")
    from rasterio.windows import Window
    path = Path(__file__).resolve().parents[1] / "data/raw/dem/Copernicus_DSM_COG_10_N27_00_E085_00_DEM.tif"
    if not path.exists():
        pytest.skip("DEM tile not downloaded")
    with rasterio.open(path) as src:
        h = src.read(1, window=Window(1500, 1000, 129, 129)).astype(np.float64)
    q = quantize_heights(h)
    blob = encode_tile(TileData(TILE, 1, heights_q=q))
    out = decode_tile(blob)
    assert np.array_equal(out.heights_q, q)
    assert np.abs(dequantize_heights(out.heights_q) - h).max() <= 0.0751
    assert len(blob) < 0.75 * q.nbytes  # the prediction filter + DEFLATE actually compress real terrain
