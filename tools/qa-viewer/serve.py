#!/usr/bin/env python3
"""Local static server for the Ghumante QA viewer (developer-only).

Serves the repository root so the viewer (``tools/qa-viewer/``) can read QA
exports anywhere in the tree, e.g. ``pipeline/build/regions/<region>/qa/``.

    python3 tools/qa-viewer/serve.py [--root .] [--port 8765] [--host 127.0.0.1] [--open]

On start it prints one viewer URL per QA dataset it finds, for example

    http://localhost:8765/tools/qa-viewer/?data=../../pipeline/build/regions/kathmandu_valley/qa

The ``data`` parameter is resolved relative to the viewer page, so
``../../pipeline/build/regions/<region>/qa`` is the default pipeline location
(``?region=<region>`` is a shorthand for it), and ``sample/qa`` is the bundled
synthetic sample.

Besides plain files it answers one JSON endpoint, ``/__qa/datasets.json``,
listing the QA datasets under the root. The viewer uses it to fill its dataset
picker and to choose a default when the URL names none. Any other static
server works too (the viewer then falls back to the sample unless
``?data=`` or ``?region=`` is given).

Standard library only. It binds to 127.0.0.1 by default; it is not meant to be
exposed on a network.
"""

from __future__ import annotations

import argparse
import functools
import http.server
import json
import socketserver
import sys
import webbrowser
from pathlib import Path
from urllib.parse import urlsplit

VIEWER_DIR = Path(__file__).resolve().parent
DEFAULT_ROOT = VIEWER_DIR.parents[1]  # repo root (tools/qa-viewer/serve.py -> ../..)

# Where QA exports are looked for, relative to the served root. The first one
# is the pipeline's default output (pipeline/build/regions/<region>/qa/); the
# second matches the generic `qa/<region>/` layout of DATA_FORMATS.md section 5.
DATASET_GLOBS = (
    "pipeline/build/regions/*/qa/index.json",
    "pipeline/build/qa/*/index.json",
    "qa/*/index.json",
)
SAMPLE_INDEX = "tools/qa-viewer/sample/qa/index.json"
ENUMS_JSON = "shared/enums.json"

EXTRA_TYPES = {
    ".geojson": "application/geo+json",
    ".json": "application/json",
    ".js": "text/javascript",
    ".mjs": "text/javascript",
    ".css": "text/css",
    ".png": "image/png",
    ".md": "text/markdown; charset=utf-8",
}
NO_CACHE_SUFFIXES = (".json", ".geojson", ".png", ".js", ".css", ".html")


def find_datasets(root: Path) -> list[dict]:
    """QA datasets under `root`, real builds first (sorted by region id), the sample last."""
    found: list[dict] = []
    seen: set[Path] = set()
    viewer_rel = VIEWER_DIR.relative_to(root) if VIEWER_DIR.is_relative_to(root) else None
    for pattern in DATASET_GLOBS:
        for idx in sorted(root.glob(pattern)):
            qa_dir = idx.parent.resolve()
            if qa_dir in seen:
                continue
            seen.add(qa_dir)
            region = qa_dir.parent.name if qa_dir.name == "qa" else qa_dir.name
            found.append(_entry(root, viewer_rel, qa_dir, region, sample=False))
    sample = root / SAMPLE_INDEX
    if sample.is_file():
        found.append(_entry(root, viewer_rel, sample.parent.resolve(), "sample", sample=True))
    return found


def _entry(root: Path, viewer_rel: Path | None, qa_dir: Path, region: str, sample: bool) -> dict:
    rel_root = qa_dir.relative_to(root.resolve()).as_posix()
    if viewer_rel is not None:
        depth = len(viewer_rel.parts)
        rel_viewer = "/".join([".."] * depth + [rel_root]) if not rel_root.startswith(viewer_rel.as_posix() + "/") \
            else rel_root[len(viewer_rel.as_posix()) + 1:]
    else:
        rel_viewer = "/" + rel_root
    info = {"id": region, "data": rel_viewer, "path": "/" + rel_root, "sample": sample}
    try:
        index = json.loads((qa_dir / "index.json").read_text(encoding="utf-8"))
        name = index.get("name")
        if isinstance(name, dict):
            name = name.get("en") or next(iter(name.values()), None)
        info["name"] = name or index.get("region") or region
        info["mtime"] = int((qa_dir / "index.json").stat().st_mtime)
    except (OSError, ValueError):
        info["name"] = region
    return info


class Handler(http.server.SimpleHTTPRequestHandler):
    root: Path = DEFAULT_ROOT
    quiet = False
    extensions_map = {**http.server.SimpleHTTPRequestHandler.extensions_map, **EXTRA_TYPES}

    def do_GET(self) -> None:  # noqa: N802 (http.server API)
        path = urlsplit(self.path).path
        if path == "/__qa/datasets.json":
            return self._send_json({
                "datasets": find_datasets(self.root),
                "enums": ("/" + ENUMS_JSON) if (self.root / ENUMS_JSON).is_file() else None,
                "viewer": "/" + VIEWER_DIR.relative_to(self.root).as_posix() + "/"
                if VIEWER_DIR.is_relative_to(self.root) else None,
            })
        if path == "/":
            # Convenience: the root redirects to the viewer when it is inside the root.
            if VIEWER_DIR.is_relative_to(self.root):
                self.send_response(302)
                self.send_header("Location", "/" + VIEWER_DIR.relative_to(self.root).as_posix() + "/")
                self.end_headers()
                return None
        return super().do_GET()

    def end_headers(self) -> None:
        if urlsplit(self.path).path.endswith(NO_CACHE_SUFFIXES) or self.path.endswith("/"):
            # QA data changes on every pipeline build: always revalidate (cheap 304s via If-Modified-Since).
            self.send_header("Cache-Control", "no-cache")
        super().end_headers()

    def _send_json(self, obj) -> None:
        body = json.dumps(obj, indent=1).encode("utf-8")
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, fmt: str, *args) -> None:
        if not self.quiet:
            super().log_message(fmt, *args)


class Server(socketserver.ThreadingMixIn, http.server.HTTPServer):
    daemon_threads = True
    allow_reuse_address = True


def viewer_urls(base: str, datasets: list[dict]) -> list[str]:
    return [f"{base}?data={ds['data']}" + ("   (synthetic sample)" if ds["sample"] else f"   ({ds['id']})")
            for ds in datasets]


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--root", type=Path, default=DEFAULT_ROOT, help="directory to serve (default: repo root)")
    ap.add_argument("--port", type=int, default=8765)
    ap.add_argument("--host", default="127.0.0.1", help="bind address (default 127.0.0.1)")
    ap.add_argument("--region", help="also print the URL for pipeline/build/regions/<REGION>/qa")
    ap.add_argument("--open", action="store_true", help="open the viewer in a browser")
    ap.add_argument("--quiet", action="store_true", help="do not log requests")
    args = ap.parse_args(argv)

    root = args.root.resolve()
    if not root.is_dir():
        print(f"error: root {root} is not a directory", file=sys.stderr)
        return 2
    Handler.root = root
    Handler.quiet = args.quiet
    handler = functools.partial(Handler, directory=str(root))
    try:
        httpd = Server((args.host, args.port), handler)
    except OSError as e:
        print(f"error: cannot listen on {args.host}:{args.port}: {e} (try --port)", file=sys.stderr)
        return 1

    port = httpd.server_address[1]
    host = "localhost" if args.host in ("127.0.0.1", "0.0.0.0", "::", "::1") else args.host
    if VIEWER_DIR.is_relative_to(root):
        base = f"http://{host}:{port}/{VIEWER_DIR.relative_to(root).as_posix()}/"
    else:
        base = f"http://{host}:{port}/  (warning: the viewer is outside --root; serve the repo root)"
    print(f"Serving {root} at http://{host}:{port}/", flush=True)
    print(f"QA viewer: {base}", flush=True)
    datasets = find_datasets(root)
    urls = viewer_urls(base, datasets)
    if args.region:
        urls.insert(0, f"{base}?data=../../pipeline/build/regions/{args.region}/qa   ({args.region})")
    for u in urls:
        print("  " + u, flush=True)
    if not any(not ds["sample"] for ds in datasets):
        print("  (no pipeline QA export found yet: run `build.py --qa`, output goes to "
              "pipeline/build/regions/<region>/qa/)", flush=True)
    if args.open:
        webbrowser.open(urls[0].split()[0] if urls else base)
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        httpd.server_close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
