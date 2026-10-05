"""Tests for the source downloader (ghumante_pipeline.fetch).

Network behaviour is exercised against a local threaded HTTP server on
127.0.0.1 that supports Range / If-Range, can fail on demand, cut a transfer
short, and emit S3-style ETag headers. Nothing here touches the internet.
"""

from __future__ import annotations

import hashlib
import json
import random
import re
import socket
import subprocess
import sys
import threading
from dataclasses import dataclass, field
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

import pytest

from ghumante_pipeline import config
from ghumante_pipeline import fetch as F

PIPELINE_ROOT = Path(__file__).resolve().parents[1]


# --------------------------------------------------------------------------
# Local test server
# --------------------------------------------------------------------------
@dataclass
class Route:
    data: bytes
    etag: str | None = None
    last_modified: str = "Sun, 04 Oct 2026 07:33:52 GMT"
    fail: list[int] = field(default_factory=list)  # status codes for the next GETs
    cut_after: int | None = None                   # first full GET sends only this many bytes
    ignore_range: bool = False
    s3: bool = False                                # emit x-amz-request-id
    sequence: list[bytes] = field(default_factory=list)  # successive bodies (for md5 files)

    def __post_init__(self) -> None:
        if self.etag is None:
            self.etag = '"' + hashlib.md5(self.data).hexdigest() + '"'


@dataclass
class Hit:
    method: str
    path: str
    range: str | None
    if_range: str | None


class _Handler(BaseHTTPRequestHandler):
    server_version = "TestServer/1.0"

    def log_message(self, *args) -> None:  # keep pytest output clean
        pass

    def do_HEAD(self) -> None:
        self._serve(head=True)

    def do_GET(self) -> None:
        self._serve(head=False)

    def _serve(self, head: bool) -> None:
        srv: TestServer = self.server.owner  # type: ignore[attr-defined]
        rng, if_range = self.headers.get("Range"), self.headers.get("If-Range")
        with srv.lock:
            srv.hits.append(Hit(self.command, self.path, rng, if_range))
            route = srv.routes.get(self.path)
            if route is None:
                self.send_error(404)
                return
            if route.fail and not head:
                self.send_error(route.fail.pop(0))
                return
            if route.sequence and not head:
                route.data = route.sequence.pop(0) if len(route.sequence) > 1 else route.sequence[0]
            data, cut = route.data, None
            if not head and route.cut_after is not None:
                cut, route.cut_after = route.cut_after, None
        start = 0
        m = re.fullmatch(r"bytes=(\d+)-", rng or "")
        use_range = (m is not None and not route.ignore_range
                     and (if_range is None or if_range in (route.etag, route.last_modified)))
        if use_range:
            start = int(m.group(1))
            if start >= len(data):
                self.send_response(416)
                self.send_header("Content-Range", f"bytes */{len(data)}")
                self.send_header("Content-Length", "0")
                self.end_headers()
                return
            self.send_response(206)
            self.send_header("Content-Range", f"bytes {start}-{len(data) - 1}/{len(data)}")
        else:
            self.send_response(200)
        body = data[start:]
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Accept-Ranges", "bytes")
        self.send_header("ETag", route.etag)
        self.send_header("Last-Modified", route.last_modified)
        if route.s3:
            self.send_header("x-amz-request-id", "TEST")
        self.send_header("Connection", "close")
        self.end_headers()
        if head:
            return
        self.wfile.write(body if cut is None else body[:cut])
        self.wfile.flush()
        self.close_connection = True


class TestServer:
    __test__ = False  # not a pytest class

    def __init__(self) -> None:
        self.routes: dict[str, Route] = {}
        self.hits: list[Hit] = []
        self.lock = threading.Lock()
        self.httpd = ThreadingHTTPServer(("127.0.0.1", 0), _Handler)
        self.httpd.daemon_threads = True
        self.httpd.owner = self  # type: ignore[attr-defined]
        self.base = f"http://127.0.0.1:{self.httpd.server_address[1]}"
        self.thread = threading.Thread(target=self.httpd.serve_forever, daemon=True)
        self.thread.start()

    def add(self, path: str, data: bytes, **kw) -> str:
        self.routes[path] = Route(data, **kw)
        return self.base + path

    def add_md5(self, path: str, data_or_hex: bytes | str, name: str = "file") -> str:
        hexd = data_or_hex if isinstance(data_or_hex, str) else hashlib.md5(data_or_hex).hexdigest()
        return self.add(path, f"{hexd}  /var/www/{name}\n".encode())

    def gets(self, path: str) -> list[Hit]:
        return [h for h in self.hits if h.path == path and h.method == "GET"]

    def close(self) -> None:
        self.httpd.shutdown()
        self.httpd.server_close()


@pytest.fixture
def server():
    s = TestServer()
    yield s
    s.close()


@pytest.fixture
def raw(tmp_path) -> Path:
    d = tmp_path / "raw"
    d.mkdir()
    return d


class Sleeps(list):
    def __call__(self, s: float) -> None:
        self.append(s)


def _blob(n: int, seed: int = 0) -> bytes:
    return random.Random(seed).randbytes(n)


def _closed_port_url() -> str:
    with socket.socket() as s:
        s.bind(("127.0.0.1", 0))
        port = s.getsockname()[1]
    return f"http://127.0.0.1:{port}/nothing"


def _dl(urls, md5_urls=None, dest="osm/test.bin", kind="osm", name="test") -> F.Download:
    urls = list(urls)
    return F.Download(name=name, kind=kind, urls=urls, md5_urls=list(md5_urls or [None] * len(urls)), dest=Path(dest))


def _sha(b: bytes) -> str:
    return hashlib.sha256(b).hexdigest()


# --------------------------------------------------------------------------
# Planning
# --------------------------------------------------------------------------
@pytest.fixture(scope="module")
def real_cfg():
    return config.load_regions(), config.load_sources()


def test_plan_kathmandu_valley_tiles(real_cfg):
    regions, sources = real_cfg
    plan = F.plan_downloads(regions["kathmandu_valley"], sources)
    assert [d.kind for d in plan] == ["osm"] + ["dem"] * 6 + ["landcover"]
    osm = plan[0]
    assert osm.name == "osm_nepal" and osm.dest == Path("osm/nepal.osm.pbf")
    assert osm.urls == ["https://download.geofabrik.de/asia/nepal-latest.osm.pbf", "https://geo2day.com/asia/nepal.pbf"]
    assert osm.md5_urls == [u + ".md5" for u in osm.urls]
    assert osm.mirror_names == ["geofabrik", "osmtoday"]
    assert osm.strip_metadata == [False, True]  # OSMToday ships user/uid/changeset
    dem_names = [d.name for d in plan if d.kind == "dem"]
    assert dem_names == [f"Copernicus_DSM_COG_10_N{lat}_00_E{lon:03d}_00_DEM" for lat in (27, 28) for lon in (84, 85, 86)]
    n27e085 = next(d for d in plan if d.name == "Copernicus_DSM_COG_10_N27_00_E085_00_DEM")
    assert n27e085.urls == ["https://copernicus-dem-30m.s3.amazonaws.com/Copernicus_DSM_COG_10_N27_00_E085_00_DEM/"
                            "Copernicus_DSM_COG_10_N27_00_E085_00_DEM.tif"]
    assert n27e085.md5_urls == [None]
    assert n27e085.dest == Path("dem/copernicus/Copernicus_DSM_COG_10_N27_00_E085_00_DEM.tif")
    assert n27e085.source_id == "copernicus_glo30"
    wc = plan[-1]
    assert wc.name == "ESA_WorldCover_10m_2021_v200_N27E084_Map"
    assert wc.urls == ["https://esa-worldcover.s3.eu-central-1.amazonaws.com/v200/2021/map/"
                       "ESA_WorldCover_10m_2021_v200_N27E084_Map.tif"]
    assert wc.dest == Path("worldcover/ESA_WorldCover_10m_2021_v200_N27E084_Map.tif")
    # Deterministic.
    assert F.plan_downloads(regions["kathmandu_valley"], sources) == plan


def test_plan_thamel_test(real_cfg):
    regions, sources = real_cfg
    names = [d.name for d in F.plan_downloads(regions["thamel_test"], sources)]
    assert names == ["osm_nepal", "Copernicus_DSM_COG_10_N27_00_E085_00_DEM", "ESA_WorldCover_10m_2021_v200_N27E084_Map"]


@pytest.mark.parametrize("rid", ["kathmandu_valley", "thamel_test", "prithvi_corridor", "pokhara"])
def test_plan_covers_what_the_samplers_need(real_cfg, rid):
    from ghumante_pipeline.dem import DemSampler
    from ghumante_pipeline.landcover import LandcoverSampler

    regions, sources = real_cfg
    r = regions[rid]
    names = {d.name for d in F.plan_downloads(r, sources)}
    for bbox in (r.horizon_bbox, r.bbox):
        assert set(DemSampler.copernicus_tile_names(bbox)) <= names
        assert set(LandcoverSampler.worldcover_tile_names(bbox)) <= names


def test_tile_corners_edges_and_hemispheres():
    assert F.tile_corners((84.6, 27.2, 86.4, 28.6), 1) == [(27, 84), (27, 85), (27, 86), (28, 84), (28, 85), (28, 86)]
    assert F.tile_corners((84.6, 27.2, 86.4, 28.6), 3) == [(27, 84)]
    # South edge exactly on a boundary also takes the tile to the south (pixel-is-point row).
    assert F.tile_corners((85.0, 27.0, 85.5, 27.5), 1) == [(26, 85), (27, 85)]
    assert F.tile_corners((85.0, 27.0, 85.5, 27.5), 3) == [(24, 84), (27, 84)]
    # North / east edges on a boundary are floored (harmless superset).
    assert F.tile_corners((85.2, 27.2, 86.0, 28.0), 1) == [(27, 85), (27, 86), (28, 85), (28, 86)]
    assert F.tile_corners((-0.5, -0.5, -0.1, -0.1), 1) == [(-1, -1)]
    assert F.tile_corners((-0.5, -0.5, -0.1, -0.1), 3) == [(-3, -3)]
    with pytest.raises(ValueError):
        F.tile_corners((1, 1, 0, 0), 1)


def test_tile_url_naming_southern_western():
    src = {"id": "x", "tile_deg": 1, "dest_dir": "dem",
           "url_template": "https://h/{ns}{lat:02d}_{ew}{lon:03d}/{ns}{lat:02d}_{ew}{lon:03d}.tif"}
    dls = F._raster_downloads("dem", src, (-0.5, -0.5, 0.5, 0.5))
    assert [d.name for d in dls] == ["S01_W001", "S01_E000", "N00_W001", "N00_E000"]
    assert dls[0].urls == ["https://h/S01_W001/S01_W001.tif"] and dls[0].dest == Path("dem/S01_W001.tif")


def test_download_validation():
    with pytest.raises(ValueError):
        F.Download("x", "osm", ["http://a"], [None, None], Path("a"))
    with pytest.raises(ValueError):
        F.Download("x", "video", ["http://a"], [None], Path("a"))
    with pytest.raises(ValueError):
        F.Download("x", "osm", ["http://a"], [None], Path("../escape"))
    with pytest.raises(ValueError):
        F.Download("x", "osm", [], [], Path("a"))


def test_parse_md5_file():
    assert F.parse_md5_file("F94BA0C6CEF6CB9F5EEAD0A53EC33C59  /var/www/maps/nepal.pbf\n") == "f94ba0c6cef6cb9f5eead0a53ec33c59"
    assert F.parse_md5_file("MD5 (nepal.pbf) = f94ba0c6cef6cb9f5eead0a53ec33c59") == "f94ba0c6cef6cb9f5eead0a53ec33c59"
    with pytest.raises(ValueError):
        F.parse_md5_file("<html>not found</html>")


def test_multipart_part_size_guess(tmp_path):
    # WorldCover N27E084: 104225123 bytes in 13 parts -> 8 MiB parts.
    assert F._multipart_part_sizes(104_225_123, 13) == [8 * F.MIB]
    assert F._multipart_part_sizes(10, 1) == [10]
    p = tmp_path / "f"
    data = _blob(int(2.5 * F.MIB), 3)
    p.write_bytes(data)
    md5s = b"".join(hashlib.md5(data[i:i + F.MIB]).digest() for i in range(0, len(data), F.MIB))
    assert F.multipart_etag(p, F.MIB) == hashlib.md5(md5s).hexdigest() + "-3"


# --------------------------------------------------------------------------
# Downloading
# --------------------------------------------------------------------------
def test_download_basic_and_lock_entry(server, raw):
    data = _blob(300_000)
    url = server.add("/a.pbf", data)
    md5_url = server.add_md5("/a.pbf.md5", data)
    dl = F.Download("osm_test", "osm", [url], [md5_url], Path("osm/a.pbf"), source_id="osm_x", mirror_names=["local"])
    sleeps = Sleeps()
    e = F.download(dl, raw, sleep=sleeps, timeout=5)
    assert (raw / "osm/a.pbf").read_bytes() == data
    assert not (raw / "osm/a.pbf.part").exists() and not (raw / "osm/a.pbf.part.json").exists()
    assert sleeps == []
    assert e["name"] == "osm_test" and e["kind"] == "osm" and e["source_id"] == "osm_x"
    assert e["path"] == "osm/a.pbf" and e["url"] == url and e["mirror"] == 0 and e["mirror_name"] == "local"
    assert e["md5_url"] == md5_url
    assert e["bytes"] == len(data)
    assert e["md5"] == hashlib.md5(data).hexdigest() and e["sha256"] == _sha(data)
    assert e["checks"] == ["md5_file"]
    assert e["last_modified"] == "Sun, 04 Oct 2026 07:33:52 GMT" and e["etag"]
    assert e["method"] == "downloaded"
    assert re.fullmatch(r"\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ", e["fetched_at"])


_OSM_WITH_METADATA = """<?xml version='1.0' encoding='UTF-8'?>
<osm version="0.6" generator="test">
<node id="1" version="3" timestamp="2020-01-02T03:04:05Z" changeset="77" uid="42" user="alice" lat="27.7" lon="85.3">
 <tag k="name" v="A"/></node>
<node id="2" version="1" timestamp="2021-01-01T00:00:00Z" changeset="78" uid="43" user="bob" lat="27.71" lon="85.31"/>
<way id="10" version="2" timestamp="2022-05-06T07:08:09Z" changeset="79" uid="42" user="alice">
 <nd ref="1"/><nd ref="2"/><tag k="highway" v="path"/></way>
</osm>
"""


def _pbf_with_metadata(tmp_path: Path) -> bytes:
    import osmium

    xml = tmp_path / "m.osm"
    xml.write_text(_OSM_WITH_METADATA, encoding="utf-8")
    out = tmp_path / "m.osm.pbf"
    w = osmium.SimpleWriter(osmium.io.File(str(out), "pbf,add_metadata=true"), overwrite=True)
    for o in osmium.FileProcessor(str(xml)):
        w.add(o)
    w.close()
    return out.read_bytes()


def _objects(path: Path) -> list[tuple]:
    import osmium

    return [(o.id, o.user, o.uid, o.changeset, o.version, o.timestamp.isoformat(), dict(o.tags))
            for o in osmium.FileProcessor(osmium.io.File(str(path), "pbf"))]


@pytest.mark.parametrize("adopt", [False, True])
def test_strip_metadata_from_mirror(server, raw, tmp_path, adopt):
    """A mirror flagged strip_metadata: the verified PBF loses user/uid/changeset, keeps the rest."""
    data = _pbf_with_metadata(tmp_path)
    src = tmp_path / "src.osm.pbf"
    src.write_bytes(data)
    before = _objects(src)
    assert before[0][1:4] == ("alice", 42, 77)
    url = server.add("/nepal.pbf", data)
    md5_url = server.add_md5("/nepal.pbf.md5", data)
    dl = F.Download("osm_nepal", "osm", [url], [md5_url], Path("osm/nepal.osm.pbf"), mirror_names=["osmtoday"],
                    strip_metadata=[True])
    if adopt:  # present but unlocked: adopted by checksum, then stripped
        (raw / "osm").mkdir()
        (raw / "osm/nepal.osm.pbf").write_bytes(data)
    e = F.download(dl, raw, sleep=Sleeps(), timeout=5, lock={})
    path = raw / "osm/nepal.osm.pbf"
    after = _objects(path)
    assert [o[1:4] for o in after] == [("", 0, 0)] * 3
    assert [(o[0], o[4], o[5], o[6]) for o in after] == [(o[0], o[4], o[5], o[6]) for o in before]
    assert e["method"] == ("adopted" if adopt else "downloaded")
    assert e["postprocess"] == ["strip_metadata"]
    assert e["upstream"] == {"bytes": len(data), "md5": hashlib.md5(data).hexdigest(), "sha256": _sha(data)}
    stripped = path.read_bytes()
    assert e["bytes"] == len(stripped) and e["sha256"] == _sha(stripped) and stripped != data
    F.write_lock(raw, [e])
    assert F.verify(raw) == []
    # up to date on the next run (no re-download, no second strip)
    hits = len(server.gets("/nepal.pbf"))
    assert F.download(dl, raw, sleep=Sleeps(), timeout=5) == e
    assert len(server.gets("/nepal.pbf")) == hits


def test_no_strip_without_flag(server, raw, tmp_path):
    data = _pbf_with_metadata(tmp_path)
    url = server.add("/g.pbf", data)
    dl = F.Download("osm_nepal", "osm", [url], [server.add_md5("/g.pbf.md5", data)], Path("osm/g.pbf"),
                    strip_metadata=[False])
    e = F.download(dl, raw, sleep=Sleeps(), timeout=5)
    assert (raw / "osm/g.pbf").read_bytes() == data and "upstream" not in e and "postprocess" not in e
    with pytest.raises(ValueError):
        F.Download("x", "dem", ["http://a"], [None], Path("a"), strip_metadata=[True])
    with pytest.raises(ValueError):
        F.Download("x", "osm", ["http://a"], [None], Path("a"), strip_metadata=[True, False])


@pytest.mark.parametrize("failure", ["http500", "refused", "http404"])
def test_mirror_fallback(server, raw, failure):
    data = _blob(50_000, 1)
    if failure == "refused":
        bad, bad_md5 = _closed_port_url(), _closed_port_url()
    else:
        code = 500 if failure == "http500" else 404
        bad = server.add("/bad", b"x", fail=[code] * 10)
        bad_md5 = server.add("/bad.md5", b"y", fail=[code] * 10)
    good = server.add("/good", data)
    good_md5 = server.add_md5("/good.md5", data)
    sleeps = Sleeps()
    e = F.download(_dl([bad, good], [bad_md5, good_md5]), raw, sleep=sleeps, timeout=5)
    assert e["mirror"] == 1 and e["url"] == good
    assert (raw / "osm/test.bin").read_bytes() == data
    assert sleeps == []  # the second mirror succeeded within the first round


def test_retry_with_backoff_then_success(server, raw):
    data = _blob(10_000, 2)
    url = server.add("/flaky", data, fail=[500, 503])
    sleeps = Sleeps()
    e = F.download(_dl([url]), raw, sleep=sleeps, timeout=5)
    assert sleeps == [2, 4]
    assert e["bytes"] == len(data) and len(server.gets("/flaky")) == 3


def test_retries_exhausted(server, raw):
    url = server.add("/down", b"zzz", fail=[500] * 20)
    sleeps = Sleeps()
    with pytest.raises(F.FetchError, match="HTTP 500"):
        F.download(_dl([url]), raw, sleep=sleeps, timeout=5)
    assert sleeps == [2, 4, 8, 16]
    assert len(server.gets("/down")) == 5
    assert not (raw / "osm/test.bin").exists()


def test_permanent_client_error_does_not_back_off(server, raw):
    sleeps = Sleeps()
    with pytest.raises(F.FetchError, match="404"):
        F.download(_dl([server.base + "/missing"]), raw, sleep=sleeps, timeout=5)
    assert sleeps == []


def test_resume_after_interrupted_transfer(server, raw):
    data = _blob(200_000, 4)
    url = server.add("/big", data, cut_after=70_000)
    sleeps = Sleeps()
    e = F.download(_dl([url]), raw, sleep=sleeps, timeout=5)
    assert (raw / "osm/test.bin").read_bytes() == data and e["sha256"] == _sha(data)
    hits = server.gets("/big")
    assert [h.range for h in hits] == [None, "bytes=70000-"]
    assert hits[1].if_range == server.routes["/big"].etag
    assert sleeps == [2]


def test_resume_existing_part_from_previous_run(server, raw):
    data = _blob(120_000, 5)
    url = server.add("/big", data, cut_after=50_000)
    md5_url = server.add_md5("/big.md5", data)
    with pytest.raises(F.FetchError):
        F.download(_dl([url], [md5_url]), raw, retries=0, timeout=5)
    part = raw / "osm/test.bin.part"
    assert part.stat().st_size == 50_000 and (raw / "osm/test.bin.part.json").exists()
    assert not (raw / "osm/test.bin").exists()
    e = F.download(_dl([url], [md5_url]), raw, retries=0, timeout=5)
    assert server.gets("/big")[-1].range == "bytes=50000-"
    assert (raw / "osm/test.bin").read_bytes() == data and e["checks"] == ["md5_file"]
    assert not part.exists()


def test_resume_restarts_when_remote_changed(server, raw):
    old, new = _blob(80_000, 6), _blob(90_000, 7)
    url = server.add("/v", old, cut_after=30_000)
    with pytest.raises(F.FetchError):
        F.download(_dl([url]), raw, retries=0, timeout=5)
    server.add("/v", new, last_modified="Mon, 05 Oct 2026 07:00:00 GMT")  # new ETag too
    F.download(_dl([url]), raw, retries=0, timeout=5)
    assert server.gets("/v")[-1].range == "bytes=30000-"  # asked, but If-Range failed -> 200
    assert (raw / "osm/test.bin").read_bytes() == new


def test_resume_when_server_ignores_range(server, raw):
    data = _blob(60_000, 8)
    url = server.add("/norange", data, cut_after=20_000, ignore_range=True)
    F.download(_dl([url]), raw, sleep=Sleeps(), timeout=5)
    assert (raw / "osm/test.bin").read_bytes() == data


def test_part_from_another_mirror_is_not_spliced(server, raw):
    a, b = _blob(40_000, 9), _blob(40_000, 10)
    url_a = server.add("/a", a, cut_after=10_000)
    url_b = server.add("/b", b)
    with pytest.raises(F.FetchError):
        F.download(_dl([url_a]), raw, retries=0, timeout=5)
    F.download(_dl([url_b]), raw, retries=0, timeout=5)
    assert server.gets("/b")[-1].range is None
    assert (raw / "osm/test.bin").read_bytes() == b


def test_complete_part_answered_416(server, raw):
    data = _blob(30_000, 11)
    url = server.add("/c", data)
    md5_url = server.add("/c.md5", b"", sequence=[b"0" * 32 + b"  x\n"] * 2 + [f"{hashlib.md5(data).hexdigest()}  x\n".encode()])
    # First attempt: body complete but md5 wrong twice -> .part discarded, mirror dropped.
    with pytest.raises(F.FetchError, match="MD5"):
        F.download(_dl([url], [md5_url]), raw, retries=0, timeout=5)
    # Simulate an interrupted rename: a complete .part plus sidecar.
    part = raw / "osm/test.bin.part"
    part.write_bytes(data)
    F._write_sidecar(raw / "osm/test.bin.part.json", F._Remote(url, server.routes["/c"].etag, None, len(data)))
    e = F.download(_dl([url], [md5_url]), raw, retries=0, timeout=5)
    assert server.gets("/c")[-1].range == f"bytes={len(data)}-"
    assert (raw / "osm/test.bin").read_bytes() == data and e["checks"] == ["md5_file"]


def test_md5_mismatch_rejected(server, raw):
    data = _blob(40_000, 12)
    url = server.add("/m", data)
    md5_url = server.add_md5("/m.md5", "0123456789abcdef0123456789abcdef")
    sleeps = Sleeps()
    with pytest.raises(F.FetchError, match="MD5"):
        F.download(_dl([url], [md5_url]), raw, sleep=sleeps, timeout=5)
    d = raw / "osm"
    assert not (d / "test.bin").exists() and not (d / "test.bin.part").exists()
    assert not (d / "test.bin.part.json").exists()
    assert sleeps == [2]  # one retry, then the mirror is dropped after a second mismatch
    assert len(server.gets("/m")) == 2


def test_md5_mismatch_keeps_existing_file(server, raw):
    old = b"existing pinned version"
    (raw / "osm").mkdir()
    (raw / "osm/test.bin").write_bytes(old)
    url = server.add("/m", _blob(1000, 13))
    md5_url = server.add_md5("/m.md5", "0123456789abcdef0123456789abcdef")
    with pytest.raises(F.FetchError):
        F.download(_dl([url], [md5_url]), raw, force=True, sleep=Sleeps(), timeout=5)
    assert (raw / "osm/test.bin").read_bytes() == old


def test_md5_regenerated_during_download(server, raw):
    data = _blob(20_000, 14)
    url = server.add("/r", data)
    stale = b"ffffffffffffffffffffffffffffffff  nepal.pbf\n"
    fresh = f"{hashlib.md5(data).hexdigest()}  nepal.pbf\n".encode()
    md5_url = server.add("/r.md5", b"", sequence=[stale, fresh])
    e = F.download(_dl([url], [md5_url]), raw, sleep=Sleeps(), timeout=5)
    assert e["checks"] == ["md5_file"] and len(server.gets("/r")) == 1


def test_s3_single_part_etag(server, raw):
    data = _blob(25_000, 15)
    url = server.add("/s3.tif", data, s3=True)
    e = F.download(_dl([url], kind="dem", dest="dem/s3.tif"), raw, timeout=5)
    assert e["checks"] == ["s3_etag"] and e["md5_url"] is None


def test_s3_etag_mismatch_rejected(server, raw):
    url = server.add("/s3.tif", _blob(25_000, 16), s3=True, etag='"0123456789abcdef0123456789abcdef"')
    with pytest.raises(F.FetchError, match="ETag"):
        F.download(_dl([url], kind="dem", dest="dem/s3.tif"), raw, sleep=Sleeps(), timeout=5)
    assert not (raw / "dem/s3.tif").exists() and not (raw / "dem/s3.tif.part").exists()


def test_non_s3_etag_is_not_treated_as_md5(server, raw):
    url = server.add("/plain.tif", _blob(5000, 17), etag='"0123456789abcdef0123456789abcdef"')
    e = F.download(_dl([url], kind="dem", dest="dem/plain.tif"), raw, timeout=5)
    assert e["checks"] == []


def test_s3_multipart_etag(server, raw, tmp_path):
    data = _blob(int(2.5 * F.MIB), 18)
    src = tmp_path / "src"
    src.write_bytes(data)
    etag = '"' + F.multipart_etag(src, F.MIB) + '"'
    url = server.add("/wc.tif", data, s3=True, etag=etag)
    e = F.download(_dl([url], kind="landcover", dest="worldcover/wc.tif"), raw, timeout=5)
    assert e["checks"] == ["s3_etag_multipart"]
    # An unreproducible multipart ETag is only a warning.
    url2 = server.add("/wc2.tif", data, s3=True, etag='"0123456789abcdef0123456789abcdef-3"')
    msgs: list[str] = []
    e2 = F.download(_dl([url2], kind="landcover", dest="worldcover/wc2.tif", name="wc2"), raw, timeout=5, log=msgs.append)
    assert e2["checks"] == [] and any("multipart ETag" in m for m in msgs)


# --------------------------------------------------------------------------
# Skip / adopt / force
# --------------------------------------------------------------------------
def test_skip_when_up_to_date(server, raw):
    data = _blob(30_000, 19)
    url = server.add("/u", data)
    dl = _dl([url])
    e1 = F.download(dl, raw, timeout=5)
    F.write_lock(raw, [e1])
    n = len(server.hits)
    e2 = F.download(dl, raw, timeout=5)
    assert e2 == e1 and len(server.hits) == n  # no network at all
    # force re-downloads
    e3 = F.download(dl, raw, force=True, timeout=5)
    assert len(server.gets("/u")) == 2 and e3["sha256"] == e1["sha256"] and e3["method"] == "downloaded"


def test_adopt_existing_file_by_md5(server, raw):
    data = _blob(70_000, 20)
    (raw / "osm").mkdir()
    (raw / "osm/test.bin").write_bytes(data)
    # Mirror 0 publishes a different snapshot; mirror 1 matches the file on disk.
    m0 = server.add("/m0", _blob(70_000, 21))
    m0_md5 = server.add_md5("/m0.md5", server.routes["/m0"].data)
    m1 = server.add("/m1", data, last_modified="Fri, 02 Oct 2026 19:34:00 GMT")
    m1_md5 = server.add_md5("/m1.md5", data)
    e = F.download(_dl([m0, m1], [m0_md5, m1_md5]), raw, timeout=5)
    assert e["method"] == "adopted" and e["mirror"] == 1 and e["url"] == m1
    assert e["checks"] == ["md5_file"] and e["sha256"] == _sha(data)
    assert e["last_modified"] == "Fri, 02 Oct 2026 19:34:00 GMT"
    assert server.gets("/m0") == [] and server.gets("/m1") == []  # no body downloads


def test_adopt_with_unreachable_primary(server, raw):
    data = _blob(10_000, 22)
    (raw / "osm").mkdir()
    (raw / "osm/test.bin").write_bytes(data)
    m1 = server.add("/m1", data)
    m1_md5 = server.add_md5("/m1.md5", data)
    e = F.download(_dl([_closed_port_url(), m1], [_closed_port_url(), m1_md5]), raw, timeout=5)
    assert e["method"] == "adopted" and e["mirror"] == 1


def test_adopt_by_s3_etag(server, raw):
    data = _blob(10_000, 23)
    (raw / "dem").mkdir()
    (raw / "dem/t.tif").write_bytes(data)
    url = server.add("/t.tif", data, s3=True)
    e = F.download(_dl([url], kind="dem", dest="dem/t.tif"), raw, timeout=5)
    assert e["method"] == "adopted" and e["checks"] == ["s3_etag"]
    assert server.gets("/t.tif") == [] and [h.method for h in server.hits] == ["HEAD"]


def test_unverifiable_or_stale_file_is_replaced(server, raw):
    data = _blob(10_000, 24)
    (raw / "dem").mkdir()
    (raw / "dem/t.tif").write_bytes(b"stale content of another version")
    url = server.add("/t.tif", data, s3=True)
    e = F.download(_dl([url], kind="dem", dest="dem/t.tif"), raw, timeout=5)
    assert e["method"] == "downloaded" and (raw / "dem/t.tif").read_bytes() == data
    # No checksum published at all: an unlocked file is re-downloaded, not trusted.
    url2 = server.add("/n.bin", data)
    (raw / "dem/n.bin").write_bytes(data)
    e2 = F.download(_dl([url2], kind="dem", dest="dem/n.bin", name="n"), raw, timeout=5)
    assert e2["method"] == "downloaded" and len(server.gets("/n.bin")) == 1


def test_tampered_locked_file_is_repaired(server, raw):
    data = _blob(10_000, 25)
    url = server.add("/x", data)
    md5_url = server.add_md5("/x.md5", data)
    dl = _dl([url], [md5_url])
    lock = {"test": F.download(dl, raw, timeout=5)}
    (raw / "osm/test.bin").write_bytes(b"\0" * len(data))
    e = F.download(dl, raw, lock=lock, timeout=5)
    assert e["method"] == "downloaded" and (raw / "osm/test.bin").read_bytes() == data


# --------------------------------------------------------------------------
# Lock file and verify
# --------------------------------------------------------------------------
def test_lock_round_trip_and_stability(server, raw):
    a, b = _blob(1000, 26), _blob(2000, 27)
    ea = F.download(_dl([server.add("/a", a)], name="zeta", dest="osm/a"), raw, timeout=5)
    eb = F.download(_dl([server.add("/b", b)], name="alpha", dest="dem/b", kind="dem"), raw, timeout=5)
    p = F.write_lock(raw, [ea, eb])
    assert p == raw / "SOURCES.lock.json"
    text1 = p.read_text(encoding="utf-8")
    F.write_lock(raw, {"alpha": eb, "zeta": ea})
    assert p.read_text(encoding="utf-8") == text1 and text1.endswith("\n")
    doc = json.loads(text1)
    assert doc["format"] == "ghumante-sources-lock" and doc["version"] == 1
    assert list(doc["entries"]) == ["alpha", "zeta"]
    assert list(doc["entries"]["alpha"]) == sorted(doc["entries"]["alpha"])
    assert F.read_lock(raw) == {"alpha": eb, "zeta": ea}
    with pytest.raises(ValueError):
        F.write_lock(raw, [ea, ea])


def test_read_lock_missing_and_corrupt(raw):
    assert F.read_lock(raw) == {}
    (raw / "SOURCES.lock.json").write_text('{"format": "something-else"}')
    with pytest.raises(ValueError):
        F.read_lock(raw)


def test_verify_detects_tampering(server, raw):
    a, b = _blob(5000, 28), _blob(6000, 29)
    ea = F.download(_dl([server.add("/a", a)], name="a", dest="osm/a"), raw, timeout=5)
    eb = F.download(_dl([server.add("/b", b)], name="b", dest="dem/b", kind="dem"), raw, timeout=5)
    assert F.verify(raw) == [f"no lock file at {raw / 'SOURCES.lock.json'}"]
    F.write_lock(raw, [ea, eb])
    assert F.verify(raw) == []
    # Same size, one byte flipped.
    flipped = bytearray(a)
    flipped[100] ^= 0xFF
    (raw / "osm/a").write_bytes(bytes(flipped))
    problems = F.verify(raw)
    assert len(problems) == 1 and "a:" in problems[0] and "SHA-256" in problems[0]
    (raw / "osm/a").write_bytes(a[:-1])
    assert "bytes" in F.verify(raw)[0]
    (raw / "dem/b").unlink()
    problems = F.verify(raw)
    assert len(problems) == 2 and "missing file dem/b" in problems[1]
    assert F.verify(raw, ["nope"]) == ["nope: not in lock file"]
    assert len(F.verify(raw, ["b"])) == 1


def test_verify_rejects_unsafe_lock_path(raw):
    F.write_lock(raw, [{"name": "evil", "path": "../outside", "bytes": 1, "sha256": "x"}])
    assert "unsafe path" in F.verify(raw)[0]


# --------------------------------------------------------------------------
# CLI
# --------------------------------------------------------------------------
@pytest.fixture
def cli_env(server, tmp_path):
    """A sources.yaml / regions.yaml pair pointing at the local server."""
    osm = _blob(40_000, 30)
    server.add("/osm/nepal.pbf", osm)
    server.add_md5("/osm/nepal.pbf.md5", osm)
    tiles = {}
    for lat in (27, 28):
        for lon in (85, 86):
            tiles[(lat, lon)] = _blob(3000 + lat + lon, lat * 1000 + lon)
            server.add(f"/dem/N{lat:02d}_E{lon:03d}.tif", tiles[(lat, lon)], s3=True)
    server.add("/wc/N27E084.tif", _blob(5000, 31), s3=True)
    base = server.base
    sources = f"""
osm:
  id: osm_nepal
  dest: osm/nepal.osm.pbf
  mirrors:
    - name: dead
      url: {_closed_port_url()}
      md5_url: {_closed_port_url()}
    - name: local
      url: {base}/osm/nepal.pbf
      md5_url: {base}/osm/nepal.pbf.md5
dem:
  id: dem_test
  tile_deg: 1
  url_template: "{base}/dem/{{ns}}{{lat:02d}}_{{ew}}{{lon:03d}}.tif"
  dest_dir: dem/copernicus
landcover:
  id: wc_test
  tile_deg: 3
  url_template: "{base}/wc/{{ns}}{{lat:02d}}{{ew}}{{lon:03d}}.tif"
  dest_dir: worldcover
"""
    regions = """
defaults: {detail_levels: [10], horizon_levels: [7], height_grid: 129, biome_grid: 65, bbox_buffer_m: 0}
regions:
  tiny:
    name_en: Tiny
    bbox: [85.3, 27.7, 85.4, 27.8]
    horizon_bbox: [85.5, 27.5, 86.5, 28.5]
"""
    (tmp_path / "sources.yaml").write_text(sources)
    (tmp_path / "regions.yaml").write_text(regions)
    raw = tmp_path / "raw"
    args = ["--region", "tiny", "--raw-dir", str(raw), "--sources-config", str(tmp_path / "sources.yaml"),
            "--regions-config", str(tmp_path / "regions.yaml"), "--timeout", "5"]
    return args, raw


def test_cli_end_to_end(server, cli_env, capsys):
    args, raw = cli_env
    assert F.main(args + ["--dry-run"]) == 0
    out = capsys.readouterr().out
    assert "6 file(s)" in out and out.count("missing") == 6 and not raw.exists()
    assert F.main(args) == 0
    lock = F.read_lock(raw)
    assert sorted(lock) == sorted(["osm_nepal", "N27_E085", "N27_E086", "N28_E085", "N28_E086", "N27E084"])
    assert lock["osm_nepal"]["mirror"] == 1 and lock["osm_nepal"]["mirror_name"] == "local"
    assert lock["N27_E085"]["path"] == "dem/copernicus/N27_E085.tif" and lock["N27_E085"]["checks"] == ["s3_etag"]
    lock_text = (raw / "SOURCES.lock.json").read_text()
    n = len(server.hits)
    # Second run: everything up to date, no network, lock unchanged byte for byte.
    assert F.main(args) == 0
    assert len(server.hits) == n and (raw / "SOURCES.lock.json").read_text() == lock_text
    assert "up to date" in capsys.readouterr().out
    assert F.main(args + ["--verify"]) == 0
    assert F.main(["--verify", "--raw-dir", str(raw)]) == 0
    (raw / "worldcover/N27E084.tif").write_bytes(b"tampered")
    assert F.main(args + ["--verify"]) == 1
    assert "PROBLEM" in capsys.readouterr().out
    assert F.main(args + ["--verify", "--only", "dem"]) == 0


def test_cli_only_and_failure_exit_code(server, cli_env, capsys):
    args, raw = cli_env
    assert F.main(args + ["--only", "dem"]) == 0
    assert sorted(F.read_lock(raw)) == ["N27_E085", "N27_E086", "N28_E085", "N28_E086"]
    server.routes["/wc/N27E084.tif"].fail = [404]
    assert F.main(args + ["--only", "landcover,dem", "--retries", "0"]) == 1
    err = capsys.readouterr().err
    assert "FAILED" in err and "N27E084" in err
    assert "N27E084" not in F.read_lock(raw)


def test_cli_usage_errors(cli_env):
    args, _raw = cli_env
    with pytest.raises(SystemExit) as ei:
        F.main(args + ["--only", "osm,video"])
    assert ei.value.code == 2
    with pytest.raises(SystemExit):
        F.main(["--dry-run"])  # no region
    with pytest.raises(SystemExit):
        F.main(args[:1] + ["nowhere"] + args[2:])


def test_wrapper_script_dry_run(tmp_path):
    r = subprocess.run([sys.executable, str(PIPELINE_ROOT / "fetch.py"), "--region", "thamel_test", "--dry-run",
                        "--raw-dir", str(tmp_path)], capture_output=True, text=True, timeout=60)
    assert r.returncode == 0, r.stderr
    assert "osm/nepal.osm.pbf" in r.stdout and "Copernicus_DSM_COG_10_N27_00_E085_00_DEM.tif" in r.stdout


# --------------------------------------------------------------------------
# Real data
# --------------------------------------------------------------------------
@pytest.mark.realdata
@pytest.mark.slow
def test_real_lock_verifies():
    raw = config.RAW_DIR
    if not F.lock_path(raw).exists():
        pytest.skip("no SOURCES.lock.json (run fetch.py)")
    regions, sources = config.load_regions(), config.load_sources()
    names = [d.name for d in F.plan_downloads(regions["kathmandu_valley"], sources)]
    assert F.verify(raw, names) == []
    lock = F.read_lock(raw)
    assert lock["osm_nepal"]["checks"] == ["md5_file"]
    assert all("s3_etag" in lock[n]["checks"] for n in names if n.startswith("Copernicus_"))
