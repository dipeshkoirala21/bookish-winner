"""Aviation sidecar ``<region>.aviation.json`` (docs/W2_DESIGN.md section 8, work item D20).

``build_aviation`` compiles ``config/aviation/<airport>.yaml`` (the AV 4-5
tables, copied unchanged) with the runway geometry read from OSM:

* **thresholds**: the end nodes of the main runway way (``main_way``); the
  southern end is the runway-02 threshold, the northern the runway-20 one
  (D20 test: equal to the OSM nodes to the centimetre).
* **pavement ends**: the far end nodes of the displaced-threshold ways.
* every lon/lat point (thresholds, VOR, NDB, holds, procedure points) also
  gets game ``x``/``z`` (metres, 3 decimals); procedure altitudes stay metres
  MSL (the game's Y is true elevation, ADR-004).

Output is deterministic JSON (sorted keys, fixed rounding). ODbL-derived
thresholds plus our own procedure tables; listed in the manifest ``files``.
"""

from __future__ import annotations

import json
import math
from pathlib import Path
from typing import Sequence

import numpy as np
import yaml

from . import projection
from .config import CONFIG_DIR
from .model import Extract, LineKind

AVIATION_DIR = CONFIG_DIR / "aviation"
FORMAT = "ghumante-aviation"
VERSION = 1


def _xz(lon: float, lat: float) -> tuple[float, float]:
    x, z = projection.lonlat_to_game(lon, lat)
    return round(float(x), 3), round(float(z), 3)


def _pt(lon: float, lat: float, **extra) -> dict:
    x, z = _xz(lon, lat)
    return {"lon": round(float(lon), 7), "lat": round(float(lat), 7), "x": x, "z": z, **extra}


def _wid(ref: str) -> int:
    if not ref or ref[0] != "w" or not ref[1:].isdigit():
        raise ValueError(f"expected a way ref, got {ref!r}")
    return int(ref[1:])


def load_config(airport: str) -> dict:
    return yaml.safe_load((AVIATION_DIR / f"{airport}.yaml").read_text(encoding="utf-8"))


def build_aviation(extract: Extract, airport: str = "tia", cfg: dict | None = None,
                   elevation=None) -> tuple[dict | None, dict]:
    """The sidecar dict, or ``None`` when the region does not hold the runway. ``elevation(x, z)``
    (optional) adds DSM ground heights at the thresholds."""
    cfg = cfg if cfg is not None else load_config(airport)
    ap = cfg["airport"]
    rw = ap["runway"]
    lines = {int(ln.osm_id): ln for ln in extract.lines if ln.kind == LineKind.RUNWAY}
    main = lines.get(_wid(rw["main_way"]))
    if main is None:
        return None, {"runway": "missing"}
    ll = np.asarray(main.lonlat, dtype=np.float64)
    south, north = (ll[0], ll[-1]) if ll[0, 1] <= ll[-1, 1] else (ll[-1], ll[0])
    thr = {"02": _pt(*south), "20": _pt(*north)}
    for k in thr:
        thr[k]["elev_m"] = float(rw["threshold_elev_m"][k])
        if elevation is not None:
            g = float(np.asarray(elevation(np.array([thr[k]["x"]]), np.array([thr[k]["z"]]))).reshape(-1)[0])
            thr[k]["dsm_m"] = round(g, 2) if math.isfinite(g) else None
    ends = {}
    for ref in rw.get("displaced_ways", []):
        d = lines.get(_wid(ref))
        if d is None:
            continue
        p = np.asarray(d.lonlat, dtype=np.float64)
        far = max((p[0], p[-1]), key=lambda q: math.hypot(*(_xz(*q)[i] - _xz(*south)[i] for i in (0, 1)))
                  + math.hypot(*(_xz(*q)[i] - _xz(*north)[i] for i in (0, 1))))
        ends["south" if far[1] < south[1] else "north"] = _pt(*far)
    tx = (thr["20"]["x"] - thr["02"]["x"], thr["20"]["z"] - thr["02"]["z"])
    heading = math.degrees(math.atan2(tx[0], tx[1])) % 360.0
    runway = {"designators": list(rw["designators"]), "main_way": rw["main_way"],
              "displaced_ways": list(rw.get("displaced_ways", [])), "width_m": float(rw["width_m"]),
              "heading_true_deg": [float(h) for h in rw["heading_true_deg"]],
              "heading_game_deg": round(heading, 3), "length_between_thresholds_m": round(math.hypot(*tx), 2),
              "thresholds": thr, "pavement_ends": ends, "hials_02_m": rw.get("hials_02_m"),
              "papi_deg": rw.get("papi_deg")}

    procs = {}
    for pid, p in sorted(cfg.get("procedures", {}).items()):
        procs[pid] = {"title": p["title"],
                      "points": [_pt(q["lon"], q["lat"], alt_m=float(q["alt_m"]), ground_m=float(q["ground_m"]),
                                     note=str(q.get("note", ""))) for q in p["points"]]}
    holds = [{**h, **_pt(h["lon"], h["lat"])} for h in cfg.get("holds", [])]
    vor, ndb = ap.get("vor"), ap.get("ndb")
    out = {
        "format": FORMAT, "version": VERSION, "note": cfg.get("note", ""),
        "airport": {"icao": ap["icao"], "iata": ap["iata"], "name_en": ap["name_en"], "aerodrome": ap["aerodrome"],
                    "elevation_m": float(ap["elevation_m"]), "open_hours": list(ap.get("open_hours", [6, 24])),
                    "aprons": dict(ap.get("aprons", {})), "buildings": dict(ap.get("buildings", {})),
                    "vor": {**vor, **_pt(vor["lon"], vor["lat"])} if vor else None,
                    "ndb": {**ndb, **_pt(ndb["lon"], ndb["lat"])} if ndb else None,
                    "runway": runway},
        "schedule": cfg.get("schedule", {}), "procedure_weights": cfg.get("procedure_weights", {}),
        "holds": holds, "procedures": procs, "liveries": cfg.get("liveries", {}), "classes": cfg.get("classes", {}),
        "sources": {"runway": "OpenStreetMap " + rw["main_way"], "procedures": "docs/research/w2/aviation.md"},
    }
    return out, {"runway": rw["main_way"], "procedures": len(procs),
                 "threshold_distance_m": runway["length_between_thresholds_m"]}


def dumps(av: dict) -> str:
    return json.dumps(av, sort_keys=True, ensure_ascii=False, indent=1, allow_nan=False) + "\n"


def write_aviation(path: Path, av: dict) -> dict:
    text = dumps(av)
    Path(path).write_text(text, encoding="utf-8")
    return {"bytes": len(text.encode("utf-8"))}
