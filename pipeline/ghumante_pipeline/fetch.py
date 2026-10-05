"""Reproducible source downloader (pipeline stage "Fetch", ARCHITECTURE.md 6.1).

Downloads what a region needs, as described by ``config/sources.yaml``:

* the Nepal OSM extract (mirrors tried in order: Geofabrik, then OSMToday),
* the Copernicus GLO-30 1-degree DEM tiles covering the region's
  ``horizon_bbox`` (a superset of ``bbox``),
* the ESA WorldCover 3-degree tiles covering it,

into ``data/raw/**`` and records exactly what was fetched (URL, mirror, size,
MD5, SHA-256, ETag, Last-Modified, time) in ``data/raw/SOURCES.lock.json``.

Behaviour, per file:

* **Up to date**: size and SHA-256 match the lock entry. Nothing is fetched.
* **Adopted**: the file exists but is not (or no longer) locked. If its MD5
  matches a checksum a mirror publishes (the ``.md5`` file, or an S3 ETag), it
  is locked as is, without re-downloading. CI restores cached rasters this way.
* **Downloaded** otherwise (or with ``force``). The body streams to
  ``<dest>.part`` with a ``<dest>.part.json`` sidecar naming the URL and its
  validators, so an interrupted transfer resumes with an HTTP ``Range`` request
  guarded by ``If-Range``. The result is checked against the mirror's MD5 file
  and/or the S3 ETag, hashed, and atomically renamed into place.

Mirrors are tried in order within each round; after a failed round the
downloader backs off 2, 4, 8, 16 s. Client errors (404, 403, ...) and a second
checksum mismatch drop a mirror for the rest of the run.

**Contributor metadata** (``strip_metadata: true`` on a mirror in
``sources.yaml``, set for the OSMToday mirror): OSM user names, uids and
changeset ids are personal data we do not need (docs/LICENSES.md 1.1). After a
download from such a mirror is verified against the published checksum, the PBF
is rewritten without them (``strip_osm_metadata``: object versions and
timestamps and the file header are kept) before it is moved into place. The
lock entry then describes the stripped file at rest (``bytes``, ``md5``,
``sha256``) and records the verified original under ``upstream`` plus
``postprocess: ["strip_metadata"]``. An existing unlocked file adopted from such
a mirror is stripped the same way.

Integrity sources: OSM mirrors publish ``<hex>  <name>`` MD5 files. Amazon S3
(GLO-30, WorldCover) returns an ETag that is the object's MD5 for single-part,
non-KMS uploads (all GLO-30 tiles). Multipart uploads (WorldCover) have an ETag
``md5(part md5s)-N``. The part size is not published, so it is guessed from
whole-MiB sizes that give ``N`` parts; a match counts as verified and a miss is
only a warning.

Usage:
    python fetch.py --region kathmandu_valley [--only osm,dem,landcover]
                    [--force] [--verify] [--raw-dir PATH] [--dry-run]
"""

from __future__ import annotations

import argparse
import hashlib
import http.client
import json
import math
import os
import re
import sys
import time
import urllib.error
import urllib.request
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path, PurePosixPath
from typing import Callable, Iterable, Mapping, Sequence
from urllib.parse import urlsplit

from . import __version__, config

KINDS = ("osm", "dem", "landcover")
LOCK_NAME = "SOURCES.lock.json"
LOCK_FORMAT = "ghumante-sources-lock"
LOCK_VERSION = 1
USER_AGENT = f"ghumante-pipeline-fetch/{__version__}"

MIB = 1 << 20
_CHUNK = MIB
_HASH_CHUNK = 4 * MIB
_PROGRESS_EVERY_S = 5.0
_MAX_BACKOFF_S = 64.0
_MAX_MULTIPART_GUESSES = 4
# 4xx codes worth retrying; any other 4xx drops the mirror for the run.
_RETRYABLE_4XX = frozenset({408, 425, 429})

_MD5_RE = re.compile(r"\b([0-9a-fA-F]{32})\b")
_ETAG_RE = re.compile(r'"?([0-9a-fA-F]{32})(?:-([0-9]+))?"?')
_CONTENT_RANGE_RE = re.compile(r"bytes\s+(\d+)-(\d+)/(\d+|\*)")
_CONTENT_RANGE_UNSATISFIED_RE = re.compile(r"bytes\s+\*/(\d+)")

Log = Callable[[str], None]


class FetchError(RuntimeError):
    """A download failed on every mirror (message lists the last errors)."""


class _Transient(Exception):
    """Retryable failure of one attempt."""


class _ChecksumMismatch(_Transient):
    """Downloaded bytes do not match a published checksum."""


@dataclass
class Download:
    """One file to fetch. ``urls`` are mirrors in preference order and
    ``md5_urls[i]`` is the checksum file published next to ``urls[i]`` (or None).
    ``dest`` is relative to the raw data directory."""

    name: str
    kind: str
    urls: list[str]
    md5_urls: list[str | None]
    dest: Path
    source_id: str = ""
    mirror_names: list[str | None] = field(default_factory=list)
    strip_metadata: list[bool] = field(default_factory=list)  # per mirror; empty = never

    def __post_init__(self) -> None:
        self.dest = Path(self.dest)
        if self.kind not in KINDS:
            raise ValueError(f"{self.name}: unknown kind {self.kind!r}")
        if not self.urls:
            raise ValueError(f"{self.name}: no URLs")
        if len(self.md5_urls) != len(self.urls):
            raise ValueError(f"{self.name}: md5_urls must parallel urls")
        if self.mirror_names and len(self.mirror_names) != len(self.urls):
            raise ValueError(f"{self.name}: mirror_names must parallel urls")
        if self.strip_metadata and len(self.strip_metadata) != len(self.urls):
            raise ValueError(f"{self.name}: strip_metadata must parallel urls")
        if any(self.strip_metadata) and self.kind != "osm":
            raise ValueError(f"{self.name}: strip_metadata only applies to OSM files")
        if not _is_safe_relpath(self.dest.as_posix()):
            raise ValueError(f"{self.name}: dest must be a relative path inside the raw dir: {self.dest}")

    def strips(self, i: int) -> bool:
        """True when downloads from mirror ``i`` get their contributor metadata stripped."""
        return bool(self.strip_metadata and self.strip_metadata[i])


# --------------------------------------------------------------------------
# Planning
# --------------------------------------------------------------------------
def tile_corners(bbox_lonlat: Sequence[float], tile_deg: int) -> list[tuple[int, int]]:
    """South-west corners ``(lat, lon)`` of the ``tile_deg`` grid tiles covering
    the box, sorted south to north, then west to east.

    Corners are the box edges floored to a multiple of ``tile_deg``. When the
    south edge lies exactly on a tile boundary, the tile to the south is added
    too: both GLO-30 (pixel-is-point, the tile's south row is dropped) and
    WorldCover (as read by ``landcover.py``) keep that boundary latitude in the
    southern tile. So the plan is a superset of what ``DemSampler`` and
    ``LandcoverSampler`` look for.
    """
    lon_min, lat_min, lon_max, lat_max = (float(v) for v in bbox_lonlat)
    if lon_min > lon_max or lat_min > lat_max:
        raise ValueError(f"degenerate bbox {tuple(bbox_lonlat)}")
    d = int(tile_deg)
    if d < 1:
        raise ValueError(f"tile_deg must be a positive integer, got {tile_deg!r}")
    lat_lo = d * math.ceil(lat_min / d) - d
    lat_hi = d * math.floor(lat_max / d)
    lon_lo = d * math.floor(lon_min / d)
    lon_hi = d * math.floor(lon_max / d)
    return [(lat, lon) for lat in range(lat_lo, lat_hi + 1, d) for lon in range(lon_lo, lon_hi + 1, d)]


def _tile_url(template: str, lat: int, lon: int) -> str:
    return template.format(ns="N" if lat >= 0 else "S", lat=abs(lat),
                           ew="E" if lon >= 0 else "W", lon=abs(lon))


def _raster_downloads(kind: str, src: Mapping, bbox: Sequence[float]) -> list[Download]:
    out = []
    for lat, lon in tile_corners(bbox, int(src.get("tile_deg", 1))):
        url = _tile_url(src["url_template"], lat, lon)
        filename = PurePosixPath(urlsplit(url).path).name
        out.append(Download(name=PurePosixPath(filename).stem, kind=kind, urls=[url], md5_urls=[None],
                            dest=Path(src["dest_dir"]) / filename, source_id=src.get("id", kind),
                            mirror_names=[None]))
    return out


def _union_bbox(a: Sequence[float], b: Sequence[float]) -> tuple[float, float, float, float]:
    return (min(a[0], b[0]), min(a[1], b[1]), max(a[2], b[2]), max(a[3], b[3]))


def plan_downloads(region: config.Region, sources: Mapping) -> list[Download]:
    """Everything ``region`` needs, in a fixed order: the OSM extract, then DEM
    tiles, then WorldCover tiles (each south-west to north-east)."""
    osm = sources["osm"]
    mirrors = osm["mirrors"]
    plan = [Download(name=osm["id"], kind="osm",
                     urls=[m["url"] for m in mirrors],
                     md5_urls=[m.get("md5_url") for m in mirrors],
                     dest=Path(osm["dest"]), source_id=osm["id"],
                     mirror_names=[m.get("name") for m in mirrors],
                     strip_metadata=[bool(m.get("strip_metadata", False)) for m in mirrors])]
    # horizon_bbox is documented as a superset of bbox; the union guards a config slip.
    bbox = _union_bbox(region.horizon_bbox, region.bbox)
    plan += _raster_downloads("dem", sources["dem"], bbox)
    plan += _raster_downloads("landcover", sources["landcover"], bbox)
    names = [d.name for d in plan]
    if len(set(names)) != len(names):
        raise ValueError(f"duplicate download names in plan: {names}")
    return plan


# --------------------------------------------------------------------------
# Lock file
# --------------------------------------------------------------------------
def lock_path(raw_dir: Path) -> Path:
    return Path(raw_dir) / LOCK_NAME


def read_lock(raw_dir: Path) -> dict[str, dict]:
    """Lock entries keyed by download name ({} when there is no lock file)."""
    p = lock_path(raw_dir)
    if not p.exists():
        return {}
    data = json.loads(p.read_text(encoding="utf-8"))
    if not isinstance(data, dict) or data.get("format") != LOCK_FORMAT:
        raise ValueError(f"{p}: not a {LOCK_FORMAT} file")
    if data.get("version") != LOCK_VERSION:
        raise ValueError(f"{p}: unsupported lock version {data.get('version')!r}")
    entries = data.get("entries", {})
    for name, e in entries.items():
        if not isinstance(e, dict) or e.get("name") != name:
            raise ValueError(f"{p}: malformed entry {name!r}")
    return {name: dict(entries[name]) for name in sorted(entries)}


def write_lock(raw_dir: Path, entries: Iterable[dict] | Mapping[str, dict]) -> Path:
    """Write exactly ``entries`` to the lock file (stable: sorted keys, fixed
    indentation, trailing newline) via an atomic rename."""
    if isinstance(entries, Mapping):
        entries = entries.values()
    by_name: dict[str, dict] = {}
    for e in entries:
        name = e["name"]
        if name in by_name:
            raise ValueError(f"duplicate lock entry {name!r}")
        by_name[name] = e
    doc = {"format": LOCK_FORMAT, "version": LOCK_VERSION, "entries": by_name}
    p = lock_path(raw_dir)
    p.parent.mkdir(parents=True, exist_ok=True)
    _atomic_write_text(p, json.dumps(doc, indent=2, sort_keys=True, ensure_ascii=False) + "\n")
    return p


def verify(raw_dir: Path, names: Iterable[str] | None = None) -> list[str]:
    """Problems with the locked files (all entries, or just ``names``): missing
    lock or file, size or SHA-256 mismatch. Empty when everything checks out.
    Works offline."""
    raw_dir = Path(raw_dir)
    try:
        lock = read_lock(raw_dir)
    except (OSError, ValueError) as e:
        return [f"unreadable lock file: {e}"]
    if not lock and not lock_path(raw_dir).exists():
        return [f"no lock file at {lock_path(raw_dir)}"]
    problems = []
    for name in sorted(lock if names is None else set(names)):
        e = lock.get(name)
        if e is None:
            problems.append(f"{name}: not in lock file")
            continue
        rel = str(e.get("path", ""))
        if not _is_safe_relpath(rel):
            problems.append(f"{name}: unsafe path {rel!r} in lock file")
            continue
        p = raw_dir / rel
        if not p.is_file():
            problems.append(f"{name}: missing file {rel}")
            continue
        size = p.stat().st_size
        if size != e.get("bytes"):
            problems.append(f"{name}: {rel} is {size} bytes, lock says {e.get('bytes')}")
            continue
        sha = _hash_file(p)[2]
        if sha != e.get("sha256"):
            problems.append(f"{name}: {rel} SHA-256 {sha} != locked {e.get('sha256')}")
    return problems


# --------------------------------------------------------------------------
# Download
# --------------------------------------------------------------------------
@dataclass
class _Remote:
    """What we know about the remote object (also persisted in the .part sidecar)."""

    url: str
    etag: str | None = None
    last_modified: str | None = None
    total: int | None = None
    s3: bool = False  # S3 response whose ETag is MD5-derived (no KMS / SSE-C)

    @classmethod
    def from_headers(cls, url: str, headers, total: int | None) -> "_Remote":
        return cls(url, headers.get("ETag"), headers.get("Last-Modified"), total, _s3_md5_etag(headers))


def download(dl: Download, raw_dir: Path, *, force: bool = False, retries: int = 4, timeout: float = 60,
             lock: Mapping[str, dict] | None = None, sleep: Callable[[float], None] = time.sleep,
             log: Log | None = None) -> dict:
    """Make ``raw_dir/dl.dest`` present and verified; return its lock entry.

    ``lock`` defaults to the lock file in ``raw_dir``. Does not write the lock
    file (callers merge the entry and call :func:`write_lock`). Raises
    :class:`FetchError` when every mirror fails; an existing ``dest`` is never
    removed on failure.
    """
    raw_dir = Path(raw_dir)
    log = log or (lambda _msg: None)
    dest = raw_dir / dl.dest
    if lock is None:
        lock = read_lock(raw_dir)
    if not force and dest.is_file():
        entry = lock.get(dl.name)
        if entry is not None and _matches_entry(dest, dl, entry):
            log(f"{dl.dest.as_posix()}: up to date")
            return dict(entry)
        log(f"{dl.dest.as_posix()}: present but not locked; checking against mirror checksums")
        adopted = _adopt(dl, raw_dir, timeout, log)
        if adopted is not None:
            return adopted
        log(f"{dl.dest.as_posix()}: no mirror checksum matches; downloading")
    return _fetch(dl, raw_dir, retries, timeout, sleep, log)


def _matches_entry(dest: Path, dl: Download, entry: Mapping) -> bool:
    if entry.get("path") != dl.dest.as_posix() or dest.stat().st_size != entry.get("bytes"):
        return False
    return _hash_file(dest)[2] == entry.get("sha256")


def _fetch(dl: Download, raw_dir: Path, retries: int, timeout: float,
           sleep: Callable[[float], None], log: Log) -> dict:
    errors: list[str] = []
    dead: set[int] = set()
    mismatches = [0] * len(dl.urls)
    for attempt in range(retries + 1):
        for i, url in enumerate(dl.urls):
            if i in dead:
                continue
            try:
                return _fetch_from(dl, raw_dir, i, timeout, log)
            except urllib.error.HTTPError as e:
                errors.append(f"mirror {i} HTTP {e.code} {e.reason} ({e.url or url})")
                if 400 <= e.code < 500 and e.code not in _RETRYABLE_4XX:
                    dead.add(i)
            except _ChecksumMismatch as e:
                errors.append(f"mirror {i}: {e}")
                mismatches[i] += 1
                if mismatches[i] >= 2:
                    dead.add(i)
            except (_Transient, OSError, http.client.HTTPException) as e:
                # URLError (refused, reset, DNS), timeouts, truncated bodies.
                errors.append(f"mirror {i}: {type(e).__name__}: {e}")
            log(f"{dl.dest.as_posix()}: {errors[-1]}")
        if len(dead) == len(dl.urls) or attempt == retries:
            break
        delay = min(2.0 ** (attempt + 1), _MAX_BACKOFF_S)
        log(f"{dl.dest.as_posix()}: all mirrors failed; retrying in {delay:.0f} s")
        sleep(delay)
    raise FetchError(f"{dl.name}: download failed: " + "; ".join(errors[-2 * len(dl.urls):]))


def _fetch_from(dl: Download, raw_dir: Path, i: int, timeout: float, log: Log) -> dict:
    url, md5_url = dl.urls[i], dl.md5_urls[i]
    dest = raw_dir / dl.dest
    part = dest.with_name(dest.name + ".part")
    sidecar = dest.with_name(dest.name + ".part.json")
    expected_md5 = _fetch_md5(md5_url, timeout) if md5_url else None
    dest.parent.mkdir(parents=True, exist_ok=True)

    prev = _read_sidecar(sidecar)
    offset = part.stat().st_size if part.is_file() and prev is not None and prev.url == url else 0
    headers = {"User-Agent": USER_AGENT}
    if offset:
        headers["Range"] = f"bytes={offset}-"
        validator = prev.etag if prev.etag and not prev.etag.startswith("W/") else prev.last_modified
        if validator:
            headers["If-Range"] = validator
    try:
        resp = urllib.request.urlopen(urllib.request.Request(url, headers=headers), timeout=timeout)
    except urllib.error.HTTPError as e:
        if e.code != 416 or not offset:
            raise
        m = _CONTENT_RANGE_UNSATISFIED_RE.search(e.headers.get("Content-Range", ""))
        e.close()
        if m and int(m.group(1)) == offset and prev.total == offset:
            log(f"{dl.dest.as_posix()}: .part already complete")
            return _finish(dl, raw_dir, i, prev, expected_md5, md5_url, timeout, log)
        _discard(part, sidecar)
        raise _Transient(f"range {offset}- not satisfiable; restarting") from None

    with resp:
        if resp.status == 206:
            m = _CONTENT_RANGE_RE.search(resp.headers.get("Content-Range", ""))
            if m is None or int(m.group(1)) != offset:
                _discard(part, sidecar)
                raise _Transient(f"bad Content-Range {resp.headers.get('Content-Range')!r} for offset {offset}")
            total = int(m.group(3)) if m.group(3) != "*" else None
            remote = _Remote.from_headers(url, resp.headers, total)
            if offset and not _same_object(prev, remote):
                _discard(part, sidecar)
                raise _Transient("remote object changed since the partial download; restarting")
            mode = "ab" if offset else "wb"
            if offset:
                log(f"{dl.dest.as_posix()}: resuming at {offset / MIB:.1f} MB")
        elif resp.status == 200:
            length = resp.headers.get("Content-Length")
            remote = _Remote.from_headers(url, resp.headers, int(length) if length is not None else None)
            offset, mode = 0, "wb"
        else:
            raise _Transient(f"unexpected HTTP {resp.status}")
        _write_sidecar(sidecar, remote)
        written = _stream(resp, part, mode, offset, remote.total, dl.dest.as_posix(), log)
    if remote.total is not None and written != remote.total:
        raise _Transient(f"connection closed after {written} of {remote.total} bytes (kept .part for resume)")
    return _finish(dl, raw_dir, i, remote, expected_md5, md5_url, timeout, log)


def _stream(resp, part: Path, mode: str, offset: int, total: int | None, label: str, log: Log) -> int:
    n = offset
    last = time.monotonic()
    with open(part, mode) as f:
        while True:
            chunk = resp.read(_CHUNK)
            if not chunk:
                break
            f.write(chunk)
            n += len(chunk)
            now = time.monotonic()
            if now - last >= _PROGRESS_EVERY_S:
                last = now
                pct = f" ({100.0 * n / total:.0f}%)" if total else ""
                log(f"{label}: {n / MIB:.1f}{f' / {total / MIB:.1f}' if total else ''} MB{pct}")
        f.flush()
        os.fsync(f.fileno())
    return n


def _finish(dl: Download, raw_dir: Path, i: int, remote: _Remote, expected_md5: str | None,
            md5_url: str | None, timeout: float, log: Log) -> dict:
    """Verify the complete .part, move it into place and build the lock entry."""
    dest = raw_dir / dl.dest
    part = dest.with_name(dest.name + ".part")
    sidecar = dest.with_name(dest.name + ".part.json")
    size, md5, sha = _hash_file(part)
    checks = []
    try:
        if expected_md5 is not None:
            if md5 != expected_md5 and md5_url:
                # The extract may have been regenerated while we downloaded.
                expected_md5 = _fetch_md5(md5_url, timeout)
            if md5 != expected_md5:
                raise _ChecksumMismatch(f"MD5 {md5} != published {expected_md5}")
            checks.append("md5_file")
        checks += _etag_checks(part, size, md5, remote, log, dl.dest.as_posix())
    except _ChecksumMismatch:
        _discard(part, sidecar)
        raise
    upstream = None
    if dl.strips(i):
        upstream = {"bytes": size, "md5": md5, "sha256": sha}
        size, md5, sha = _strip_in_place(part, log, dl.dest.as_posix())
    os.replace(part, dest)
    sidecar.unlink(missing_ok=True)
    log(f"{dl.dest.as_posix()}: {size / MIB:.1f} MB, checks: {', '.join(checks) or 'none'}")
    return _make_entry(dl, i, remote, size, md5, sha, checks, "downloaded", upstream)


def _adopt(dl: Download, raw_dir: Path, timeout: float, log: Log) -> dict | None:
    """Lock an existing file without downloading it when its MD5 matches a
    checksum published by one of the mirrors (one attempt per mirror)."""
    dest = raw_dir / dl.dest
    digests = None
    for i, url in enumerate(dl.urls):
        md5_url = dl.md5_urls[i]
        try:
            expected = _fetch_md5(md5_url, timeout) if md5_url else None
        except (_Transient, OSError, http.client.HTTPException) as e:
            log(f"{dl.dest.as_posix()}: mirror {i} checksum unavailable ({type(e).__name__}: {e})")
            continue
        try:
            remote = _head(url, timeout)
        except (OSError, http.client.HTTPException) as e:
            if expected is None:
                log(f"{dl.dest.as_posix()}: mirror {i} unavailable ({type(e).__name__}: {e})")
                continue
            remote = _Remote(url)
        if digests is None:
            digests = _hash_file(dest)
        size, md5, sha = digests
        if remote.total is not None and remote.total != size:
            continue
        checks = []
        if expected is not None:
            if expected != md5:
                continue
            checks.append("md5_file")
        try:
            checks += _etag_checks(dest, size, md5, remote, log, dl.dest.as_posix())
        except _ChecksumMismatch:
            continue
        if checks:
            log(f"{dl.dest.as_posix()}: adopted (mirror {i}, {', '.join(checks)})")
            upstream = None
            if dl.strips(i):
                upstream = {"bytes": size, "md5": md5, "sha256": sha}
                size, md5, sha = _strip_in_place(dest, log, dl.dest.as_posix())
            return _make_entry(dl, i, remote, size, md5, sha, checks, "adopted", upstream)
    return None


def _make_entry(dl: Download, i: int, remote: _Remote, size: int, md5: str, sha256: str,
                checks: list[str], method: str, upstream: dict | None = None) -> dict:
    entry = {
        "name": dl.name,
        "kind": dl.kind,
        "source_id": dl.source_id,
        "path": dl.dest.as_posix(),
        "url": dl.urls[i],
        "mirror": i,
        "mirror_name": dl.mirror_names[i] if dl.mirror_names else None,
        "md5_url": dl.md5_urls[i],
        "bytes": size,
        "md5": md5,
        "sha256": sha256,
        "checks": sorted(checks),
        "etag": remote.etag,
        "last_modified": remote.last_modified,
        "fetched_at": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "method": method,
    }
    if upstream is not None:
        entry["upstream"] = dict(upstream)
        entry["postprocess"] = ["strip_metadata"]
    return entry


# --------------------------------------------------------------------------
# Contributor metadata
# --------------------------------------------------------------------------
STRIP_FORMAT = "pbf,add_metadata=version+timestamp"


def strip_osm_metadata(src: Path, dst: Path) -> None:
    """Rewrite the OSM PBF ``src`` to ``dst`` without user names, uids and
    changeset ids. Tags, ids, geometry, object versions and timestamps, and the
    file header (bbox, replication timestamp) are kept."""
    import osmium

    src_file = osmium.io.File(str(src), "pbf")
    reader = osmium.io.Reader(src_file, osmium.osm.osm_entity_bits.NOTHING)
    header = reader.header()
    reader.close()
    writer = osmium.SimpleWriter(osmium.io.File(str(dst), STRIP_FORMAT), header=header, overwrite=True)
    try:
        for obj in osmium.FileProcessor(osmium.io.File(str(src), "pbf")):
            writer.add(obj)
    finally:
        writer.close()


def _strip_in_place(path: Path, log: Log, label: str) -> tuple[int, str, str]:
    """Strip contributor metadata from ``path`` (atomically); returns the new (size, md5, sha256)."""
    tmp = path.with_name(path.name + ".strip")
    try:
        strip_osm_metadata(path, tmp)
        os.replace(tmp, path)
    except BaseException:
        tmp.unlink(missing_ok=True)
        raise
    size, md5, sha = _hash_file(path)
    log(f"{label}: contributor metadata stripped ({size / MIB:.1f} MB)")
    return size, md5, sha


# --------------------------------------------------------------------------
# Checksums
# --------------------------------------------------------------------------
def _hash_file(path: Path) -> tuple[int, str, str]:
    """(size, md5 hex, sha256 hex) in one pass."""
    md5, sha = hashlib.md5(), hashlib.sha256()
    size = 0
    with open(path, "rb") as f:
        while chunk := f.read(_HASH_CHUNK):
            md5.update(chunk)
            sha.update(chunk)
            size += len(chunk)
    return size, md5.hexdigest(), sha.hexdigest()


def parse_md5_file(text: str) -> str:
    """The MD5 in a checksum file (``<hex>  <name>`` or BSD ``MD5 (name) = <hex>``)."""
    m = _MD5_RE.search(text)
    if m is None:
        raise ValueError(f"no MD5 in checksum file: {text[:80]!r}")
    return m.group(1).lower()


def _fetch_md5(url: str, timeout: float) -> str:
    req = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        text = resp.read(64 * 1024).decode("utf-8", "replace")
    try:
        return parse_md5_file(text)
    except ValueError as e:
        raise _Transient(f"{url}: {e}") from None


def _head(url: str, timeout: float) -> _Remote:
    req = urllib.request.Request(url, method="HEAD", headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        length = resp.headers.get("Content-Length")
        return _Remote.from_headers(url, resp.headers, int(length) if length is not None else None)


def _s3_md5_etag(headers) -> bool:
    """True for Amazon S3 responses whose ETag is derived from MD5 (plain or
    SSE-S3 objects; KMS and customer-key encryption make it opaque)."""
    if headers.get("Server") != "AmazonS3" and headers.get("x-amz-request-id") is None:
        return False
    sse = (headers.get("x-amz-server-side-encryption") or "").lower()
    return not sse.startswith("aws:kms") and headers.get("x-amz-server-side-encryption-customer-algorithm") is None


def _parse_etag(etag: str | None) -> tuple[str, int] | None:
    """(hex, parts) of an MD5-style ETag; parts is 0 for a single-part upload."""
    if not etag or etag.startswith("W/"):
        return None
    m = _ETAG_RE.fullmatch(etag.strip())
    if m is None:
        return None
    return m.group(1).lower(), int(m.group(2)) if m.group(2) else 0


def multipart_etag(path: Path, part_size: int) -> str:
    """S3 multipart ETag of a file uploaded in ``part_size`` parts."""
    digests = []
    with open(path, "rb") as f:
        while chunk := f.read(part_size):
            digests.append(hashlib.md5(chunk).digest())
    return f"{hashlib.md5(b''.join(digests)).hexdigest()}-{len(digests)}"


def _multipart_part_sizes(size: int, parts: int) -> list[int]:
    """Whole-MiB part sizes that split ``size`` bytes into exactly ``parts`` parts."""
    if parts == 1:
        return [max(size, 1)]
    lo = -(-size // parts)                 # smallest part size giving <= parts parts
    hi = -(-size // (parts - 1)) - 1        # largest part size giving > parts - 1 parts
    return [m * MIB for m in range(-(-lo // MIB), hi // MIB + 1)][:_MAX_MULTIPART_GUESSES]


def _etag_checks(path: Path, size: int, md5: str, remote: _Remote, log: Log, label: str) -> list[str]:
    if not remote.s3:
        return []
    parsed = _parse_etag(remote.etag)
    if parsed is None:
        return []
    hexd, parts = parsed
    if parts == 0:
        if hexd != md5:
            raise _ChecksumMismatch(f"MD5 {md5} != S3 ETag {hexd}")
        return ["s3_etag"]
    for ps in _multipart_part_sizes(size, parts):
        if multipart_etag(path, ps) == f"{hexd}-{parts}":
            return ["s3_etag_multipart"]
    log(f"{label}: warning: could not reproduce multipart ETag {remote.etag} (unknown part size)")
    return []


# --------------------------------------------------------------------------
# Small file helpers
# --------------------------------------------------------------------------
def _same_object(prev: _Remote, now: _Remote) -> bool:
    if prev.etag and now.etag:
        return prev.etag == now.etag
    if prev.last_modified and now.last_modified:
        return prev.last_modified == now.last_modified
    return prev.total is None or now.total is None or prev.total == now.total


def _read_sidecar(path: Path) -> _Remote | None:
    try:
        d = json.loads(path.read_text(encoding="utf-8"))
        return _Remote(url=d["url"], etag=d.get("etag"), last_modified=d.get("last_modified"),
                       total=d.get("total"), s3=bool(d.get("s3", False)))
    except (OSError, ValueError, KeyError, TypeError):
        return None


def _write_sidecar(path: Path, remote: _Remote) -> None:
    doc = {"url": remote.url, "etag": remote.etag, "last_modified": remote.last_modified,
           "total": remote.total, "s3": remote.s3}
    _atomic_write_text(path, json.dumps(doc, indent=1, sort_keys=True) + "\n")


def _discard(*paths: Path) -> None:
    for p in paths:
        p.unlink(missing_ok=True)


def _atomic_write_text(path: Path, text: str) -> None:
    tmp = path.with_name(path.name + ".tmp")
    tmp.write_text(text, encoding="utf-8")
    os.replace(tmp, path)


def _is_safe_relpath(rel: str) -> bool:
    p = PurePosixPath(rel)
    return bool(rel) and not p.is_absolute() and ".." not in p.parts and "\\" not in rel


# --------------------------------------------------------------------------
# CLI
# --------------------------------------------------------------------------
def _plan_status(dl: Download, raw_dir: Path, lock: Mapping[str, dict]) -> str:
    """Cheap (no hashing) status for the plan listing."""
    dest = raw_dir / dl.dest
    if dest.is_file():
        e = lock.get(dl.name)
        if e is not None and e.get("path") == dl.dest.as_posix() and e.get("bytes") == dest.stat().st_size:
            return "locked"
        return "present, unlocked"
    if dest.with_name(dest.name + ".part").is_file():
        return "partial"
    return "missing"


def _parse_kinds(only: str | None) -> tuple[str, ...]:
    if not only:
        return KINDS
    kinds = tuple(k.strip() for k in only.split(",") if k.strip())
    bad = [k for k in kinds if k not in KINDS]
    if bad or not kinds:
        raise ValueError(f"--only takes a comma-separated subset of {','.join(KINDS)}; got {only!r}")
    return kinds


def main(argv: Sequence[str] | None = None) -> int:
    ap = argparse.ArgumentParser(prog="fetch.py", description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--region", help="region id from config/regions.yaml")
    ap.add_argument("--only", help="comma-separated subset of: " + ",".join(KINDS))
    ap.add_argument("--force", action="store_true", help="re-download even if the lock says up to date")
    ap.add_argument("--verify", action="store_true",
                    help="check files against the lock (size + SHA-256) and exit; no network")
    ap.add_argument("--dry-run", action="store_true", help="print the plan and exit")
    ap.add_argument("--raw-dir", type=Path, default=config.RAW_DIR, help=f"default: {config.RAW_DIR}")
    ap.add_argument("--regions-config", type=Path, default=None, help="default: config/regions.yaml")
    ap.add_argument("--sources-config", type=Path, default=None, help="default: config/sources.yaml")
    ap.add_argument("--retries", type=int, default=4, help="extra rounds over the mirrors (default 4)")
    ap.add_argument("--timeout", type=float, default=60.0, help="socket timeout in seconds (default 60)")
    args = ap.parse_args(argv)
    try:
        kinds = _parse_kinds(args.only)
    except ValueError as e:
        ap.error(str(e))
    if not args.region and not args.verify:
        ap.error("--region is required (except with --verify)")
    if args.retries < 0:
        ap.error("--retries must be >= 0")

    raw_dir: Path = args.raw_dir
    plan: list[Download] = []
    if args.region:
        regions = config.load_regions(args.regions_config)
        if args.region not in regions:
            ap.error(f"unknown region {args.region!r}; known: {', '.join(sorted(regions))}")
        region = regions[args.region]
        plan = [d for d in plan_downloads(region, config.load_sources(args.sources_config)) if d.kind in kinds]

    if args.verify:
        names = [d.name for d in plan] if args.region else None
        problems = verify(raw_dir, names)
        if names is None:
            try:
                names = list(read_lock(raw_dir))
            except (OSError, ValueError):
                names = []
        for p in problems:
            print(f"  PROBLEM {p}")
        print(f"verify: {len(names)} file(s) checked against {lock_path(raw_dir)}, {len(problems)} problem(s)")
        return 1 if problems else 0

    try:
        lock = read_lock(raw_dir)
    except (OSError, ValueError) as e:
        print(f"error: cannot read lock file: {e}", file=sys.stderr)
        return 1
    hb = region.horizon_bbox
    print(f"fetch {region.id}: {len(plan)} file(s) for horizon bbox "
          f"[{hb[0]}, {hb[1]}, {hb[2]}, {hb[3]}] -> {raw_dir}")
    for dl in plan:
        print(f"  {dl.kind:<9} {dl.dest.as_posix():<62} {_plan_status(dl, raw_dir, lock)}")
    if args.dry_run:
        return 0

    def log(msg: str) -> None:
        print(f"    {msg}", flush=True)

    failed = []
    for dl in plan:
        before = lock.get(dl.name)
        try:
            entry = download(dl, raw_dir, force=args.force, retries=args.retries, timeout=args.timeout,
                             lock=lock, log=log)
        except FetchError as e:
            failed.append(dl.name)
            print(f"  FAILED  {dl.dest.as_posix()}: {e}", file=sys.stderr, flush=True)
            continue
        status = "up to date" if entry == before else entry["method"]
        if entry != before:
            lock[dl.name] = entry
            write_lock(raw_dir, lock)
        print(f"  ok      {dl.dest.as_posix()}: {status}, {entry['bytes'] / MIB:.1f} MB, "
              f"mirror {entry['mirror']}, md5 {entry['md5']}", flush=True)
    print(f"lock: {lock_path(raw_dir)} ({len(lock)} entries)")
    if failed:
        print(f"{len(failed)} of {len(plan)} download(s) failed: {', '.join(failed)}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
