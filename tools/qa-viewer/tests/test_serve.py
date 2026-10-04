"""serve.py: dataset discovery, the /__qa/datasets.json endpoint and MIME types."""

from __future__ import annotations

import importlib.util
import json
import threading
import urllib.request
from functools import partial
from pathlib import Path

VIEWER = Path(__file__).resolve().parents[1]
ROOT = VIEWER.parents[1]

spec = importlib.util.spec_from_file_location("qa_serve", VIEWER / "serve.py")
serve = importlib.util.module_from_spec(spec)
spec.loader.exec_module(serve)


def test_find_datasets_lists_the_sample_with_a_viewer_relative_path():
    ds = serve.find_datasets(ROOT)
    sample = [d for d in ds if d["sample"]]
    assert len(sample) == 1
    assert sample[0]["data"] == "sample/qa"
    assert sample[0]["name"] == "Thamel (synthetic sample)"


def test_find_datasets_finds_pipeline_builds(tmp_path):
    qa = tmp_path / "pipeline" / "build" / "regions" / "thamel_test" / "qa"
    qa.mkdir(parents=True)
    (qa / "index.json").write_text(json.dumps({"region": "thamel_test", "name": {"en": "Thamel (test)"}}))
    ds = serve.find_datasets(tmp_path)
    assert ds[0]["id"] == "thamel_test" and ds[0]["name"] == "Thamel (test)"
    assert ds[0]["path"] == "/pipeline/build/regions/thamel_test/qa"
    assert not ds[0]["sample"]


def test_http_endpoint_and_types():
    serve.Handler.root = ROOT
    serve.Handler.quiet = True
    httpd = serve.Server(("127.0.0.1", 0), partial(serve.Handler, directory=str(ROOT)))
    port = httpd.server_address[1]
    t = threading.Thread(target=httpd.serve_forever, daemon=True)
    t.start()
    try:
        base = f"http://127.0.0.1:{port}"
        with urllib.request.urlopen(base + "/__qa/datasets.json") as r:
            body = json.loads(r.read())
        assert body["enums"] == "/shared/enums.json"
        assert body["viewer"] == "/tools/qa-viewer/"
        assert any(d["data"] == "sample/qa" for d in body["datasets"])
        with urllib.request.urlopen(base + "/tools/qa-viewer/sample/qa/roads.geojson") as r:
            assert r.headers["Content-Type"] == "application/geo+json"
            assert r.headers["Cache-Control"] == "no-cache"
        with urllib.request.urlopen(base + "/tools/qa-viewer/sample/qa/hillshade/10/516_162.png") as r:
            assert r.headers["Content-Type"] == "image/png"
        with urllib.request.urlopen(base + "/") as r:  # redirects to the viewer
            assert r.url.endswith("/tools/qa-viewer/")
    finally:
        httpd.shutdown()
        httpd.server_close()
