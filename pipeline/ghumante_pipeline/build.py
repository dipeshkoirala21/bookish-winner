"""Region build orchestrator: every pipeline stage, from the PBF to a shippable region.

    python build.py --region thamel_test|kathmandu_valley [--pbf PATH] [--raw-dir PATH]
                    [--out build/] [--qa] [--no-cache] [--stages tiles,search,routing,qa]
                    [--workers N]

Stages (ARCHITECTURE.md section 6.1):

1. **config**: region from ``config/regions.yaml``, sources from ``sources.yaml``.
2. **extract**: ``osm_extract.extract_region``, cached under
   ``<out>/cache/<region>/extract-<hash>.pkl.gz``. The hash covers the PBF's
   name, size and mtime, the bbox, buffer and admin levels, and the source of
   the extract code (``osm_extract.py``, ``tags.py``, ``model.py``).
3. **rasters**: one ``DemSampler`` over every tile's lon/lat extent, and
   ``LandcoverSampler`` mosaics for the detail and the horizon tile extents.
4. **surface**: ``contexts.road_contexts`` -> ``surface.assign_surfaces``.
5. **trails**: ``trails.assign_trail_difficulty`` (glacier proximity from OSM
   glacier areas).
6. **buildings**: ``contexts.building_contexts`` -> ``buildings.infer_buildings``.
7. **search**: ``search_index.build_entries`` (landmark ids from
   ``config/landmarks.resolved.json``) -> ``<region>.search.ghsi``.
8. **tiles**: ``tiling.build_tiles`` -> **pack** ``<region>.ghpk``.
9. **routing**: ``routing.build_graph`` -> ``<region>.route.ghrg``.
10. **manifest**: ``<region>.manifest.json`` (DATA_FORMATS.md section 2) with
    SHA-256 of each file, stats, timings and attribution.
11. **qa** (``--qa``): ``qa_export.export_qa`` decodes the pack into ``qa/``.
12. **report**: ``BUILD_REPORT.md``.

Outputs go to ``<out>/regions/<region>/``. The pack, search index and routing
graph are byte-deterministic for the same inputs; only the manifest's
``built_at`` and the timings differ between runs.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import logging
import sys
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Sequence

import numpy as np
import shapely

from . import __version__, config, contexts, osm_extract, projection, routing, search_index, tiling
from .biomes import BiomeZones, load_zones
from .buildings import infer_buildings, load_archetype_zones
from .dem import DemSampler
from .landcover import LandcoverSampler
from .model import AreaKind, Extract, PoiKind
from .pack import file_entry, write_manifest, write_pack
from .surface import assign_surfaces
from .trails import assign_trail_difficulty

log = logging.getLogger("ghumante.build")

ALL_STAGES = ("tiles", "search", "routing", "qa")
DEFAULT_ADMIN_LEVELS = (4, 6, 7)
GLACIER_NEAR_DEG = 0.005  # about 500 m
_EXTRACT_CODE = ("osm_extract.py", "tags.py", "model.py")


# ---------------------------------------------------------------------------
# Source discovery and fingerprints
# ---------------------------------------------------------------------------
def _md5_file(path: Path) -> str:
    h = hashlib.md5()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 22), b""):
            h.update(chunk)
    return h.hexdigest()


def _lock_entries(raw_dir: Path) -> dict[str, dict]:
    p = raw_dir / "SOURCES.lock.json"
    if not p.exists():
        return {}
    try:
        data = json.loads(p.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return {}
    return {str(e.get("path")): e for e in (data.get("entries") or {}).values() if isinstance(e, dict)}


def source_record(path: Path, raw_dir: Path, lock: dict[str, dict]) -> dict:
    """``{name, bytes, md5, sha256?}`` for a source file: from the lock file when
    its size matches, else from an ``.md5`` sidecar, else hashed here."""
    path = Path(path)
    size = path.stat().st_size
    rel = None
    for cand in (path, path.resolve()):
        try:
            rel = cand.relative_to(raw_dir).as_posix()
        except ValueError:
            try:
                rel = cand.relative_to(raw_dir.resolve()).as_posix()
            except ValueError:
                continue
        if rel in lock:
            break
    e = lock.get(rel or "")
    if e and int(e.get("bytes", -1)) == size and e.get("md5"):
        return {"name": path.name, "bytes": size, "md5": e["md5"], "sha256": e.get("sha256")}
    side = path.with_name(path.name + ".md5")
    if side.exists():
        tok = side.read_text(encoding="utf-8", errors="replace").split()
        if tok and len(tok[0]) == 32:
            return {"name": path.name, "bytes": size, "md5": tok[0].lower()}
    return {"name": path.name, "bytes": size, "md5": _md5_file(path)}


def _combined_md5(records: Sequence[dict]) -> str:
    h = hashlib.md5()
    for r in sorted(records, key=lambda r: r["name"]):
        h.update(f"{r['name']}={r['md5']}\n".encode())
    return h.hexdigest()


def _unique_files(paths) -> list[Path]:
    seen, out = set(), []
    for p in paths:
        rp = p.resolve()
        if p.is_file() and rp not in seen:
            seen.add(rp)
            out.append(p)
    return out


def find_dem_files(raw_dir: Path, bbox_lonlat) -> tuple[list[Path], list[str]]:
    """GLO-30 tiles for the box under ``raw/dem`` (or ``raw/dem/copernicus``);
    falls back to every ``*.tif`` under ``raw/dem``. Returns (files, missing names)."""
    names = DemSampler.copernicus_tile_names(bbox_lonlat)
    found, missing = [], []
    for n in names:
        cands = [raw_dir / "dem" / "copernicus" / f"{n}.tif", raw_dir / "dem" / f"{n}.tif"]
        hit = next((c for c in cands if c.is_file()), None)
        (found.append(hit) if hit else missing.append(n))
    found = _unique_files(found)
    if not found:
        found = _unique_files(sorted((raw_dir / "dem").rglob("*.tif")))
        missing = []
    return found, missing


def find_landcover_files(raw_dir: Path, bbox_lonlat) -> tuple[list[Path], list[str]]:
    names = LandcoverSampler.worldcover_tile_names(bbox_lonlat)
    found, missing = [], []
    for n in names:
        c = raw_dir / "worldcover" / f"{n}.tif"
        (found.append(c) if c.is_file() else missing.append(n))
    found = _unique_files(found)
    if not found:
        found = _unique_files(sorted((raw_dir / "worldcover").rglob("*.tif")))
        missing = []
    return found, missing


# Curated landmark kinds (config/landmarks.yaml) that override the kind tags.poi_kind
# derived for the same OSM object. OSM often tags a stupa only as a Buddhist
# place of worship (Boudhanath: amenity=place_of_worship + religion=buddhist ->
# GOMPA); the hand-checked landmark kind is better evidence.
LANDMARK_POI_KINDS: dict[str, PoiKind] = {
    "stupa": PoiKind.STUPA, "monastery": PoiKind.GOMPA, "heritage_square": PoiKind.HERITAGE_SQUARE, "palace": PoiKind.PALACE, "waterfall": PoiKind.WATERFALL,
    "cave": PoiKind.CAVE, "viewpoint": PoiKind.VIEWPOINT, "airport": PoiKind.AIRPORT,
    "cable_car": PoiKind.CABLE_CAR_STATION, "pass": PoiKind.PASS,
}


def _landmark_entries(path: Path | None) -> list[dict]:
    if not path or not Path(path).exists():
        return []
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    return list(data if isinstance(data, list) else data.get("landmarks", []))


def _landmark_osm(e: dict) -> tuple[str, int] | None:
    osm = str(e.get("osm") or "")
    if len(osm) < 2 or osm[0] not in "nwr" or not osm[1:].isdigit():
        return None
    return osm[0], int(osm[1:])


def load_landmarks(path: Path) -> tuple[dict[int, str], set[tuple[str, int]]]:
    """``landmarks.resolved.json`` -> (raw osm id -> landmark id, {(osm_type, osm_id)})."""
    ids: dict[int, str] = {}
    refs: set[tuple[str, int]] = set()
    for e in _landmark_entries(path):
        ref = _landmark_osm(e)
        if ref is None:
            continue
        ids.setdefault(ref[1], str(e.get("id", "")))
        refs.add(ref)
    return ids, refs


def apply_landmark_kinds(extract: Extract, path: Path | None) -> int:
    """Set ``kind`` of POIs that are curated landmarks to ``LANDMARK_POI_KINDS``
    of the landmark's kind (in place). Returns the number of POIs changed."""
    kinds: dict[tuple[str, int], PoiKind] = {}
    for e in _landmark_entries(path):
        ref = _landmark_osm(e)
        k = LANDMARK_POI_KINDS.get(str(e.get("kind") or ""))
        if ref is not None and k is not None:
            kinds.setdefault(ref, k)
    changed = 0
    for p in extract.pois:
        k = kinds.get((p.osm_type, int(p.osm_id)))
        if k is not None and p.kind != k:
            p.kind = k
            changed += 1
    return changed


def _code_hash(names: Sequence[str]) -> str:
    h = hashlib.sha256()
    base = Path(__file__).resolve().parent
    for n in names:
        h.update(n.encode())
        h.update((base / n).read_bytes())
    return h.hexdigest()[:16]


def extract_cache_key(pbf: Path, region: config.Region, admin_levels: Sequence[int]) -> str:
    st = pbf.stat()
    payload = json.dumps({
        "pbf": pbf.name, "size": st.st_size, "mtime_ns": st.st_mtime_ns, "bbox": list(region.bbox),
        "buffer_m": region.bbox_buffer_m, "admin_levels": list(admin_levels), "code": _code_hash(_EXTRACT_CODE),
        "data_version": config.PIPELINE_DATA_VERSION,
    }, sort_keys=True)
    return hashlib.sha256(payload.encode()).hexdigest()[:20]


def _osm_timestamp(pbf: Path) -> str | None:
    try:
        import osmium

        r = osmium.io.Reader(str(pbf), osmium.osm.osm_entity_bits.NOTHING)
        h = r.header()
        r.close()
        ts = h.get("osmosis_replication_timestamp") or h.get("timestamp")
        return ts or None
    except Exception:  # noqa: BLE001 - optional metadata
        return None


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
class _Timer:
    def __init__(self) -> None:
        self.t: dict[str, float] = {}

    def run(self, name: str, fn, *a, **kw):
        log.info("[%s] ...", name)
        t0 = time.perf_counter()
        out = fn(*a, **kw)
        self.t[name] = round(time.perf_counter() - t0, 2)
        log.info("[%s] done in %.1f s", name, self.t[name])
        return out


def _glacier_test(extract: Extract):
    polys = [a.polygon for a in extract.areas if a.kind == AreaKind.GLACIER and a.polygon is not None]
    if not polys:
        return None
    geom = shapely.buffer(shapely.union_all(polys), GLACIER_NEAR_DEG)
    shapely.prepare(geom)

    def near(lonlat: np.ndarray) -> bool:
        ll = np.asarray(lonlat, dtype=np.float64)
        return bool(shapely.intersects_xy(geom, ll[:, 0], ll[:, 1]).any())

    return near


def _pct(part: float, whole: float) -> float:
    return round(100.0 * part / whole, 2) if whole else 0.0


# ---------------------------------------------------------------------------
# Build
# ---------------------------------------------------------------------------
def build_region(region: config.Region | str, *, pbf: Path | None = None, raw_dir: Path | None = None,
                 out_dir: Path | None = None, qa: bool = False, use_cache: bool = True,
                 stages: Sequence[str] | None = None, workers: int | None = None,
                 landmarks_path: Path | None = None, archetype_zones_path: Path | None = None,
                 biome_zones_path: Path | None = None, admin_levels: Sequence[int] = DEFAULT_ADMIN_LEVELS) -> dict:
    """Run the build for one region; returns the manifest dict (with ``stats``)."""
    t_start = time.perf_counter()
    if isinstance(region, str):
        regions = config.load_regions()
        if region not in regions:
            raise SystemExit(f"unknown region {region!r}; known: {', '.join(sorted(regions))}")
        region = regions[region]
    raw_dir = Path(raw_dir) if raw_dir else config.RAW_DIR
    pbf = Path(pbf) if pbf else raw_dir / "osm" / "nepal.osm.pbf"
    out_dir = Path(out_dir) if out_dir else config.BUILD_DIR
    landmarks_path = Path(landmarks_path) if landmarks_path else config.CONFIG_DIR / "landmarks.resolved.json"
    stages = tuple(stages) if stages else (ALL_STAGES if qa else tuple(s for s in ALL_STAGES if s != "qa"))
    bad = [s for s in stages if s not in ALL_STAGES]
    if bad:
        raise ValueError(f"unknown stages {bad}; choose from {ALL_STAGES}")
    if not pbf.exists():
        raise FileNotFoundError(f"OSM extract {pbf} not found (run fetch.py)")
    rid = region.id
    reg_dir = out_dir / "regions" / rid
    reg_dir.mkdir(parents=True, exist_ok=True)
    pack_path = reg_dir / f"{rid}.ghpk"
    index_path = reg_dir / f"{rid}.search.ghsi"
    graph_path = reg_dir / f"{rid}.route.ghrg"
    manifest_path = reg_dir / f"{rid}.manifest.json"
    timer = _Timer()
    stats: dict = {}
    warnings: list[str] = []
    need_data = any(s in stages for s in ("tiles", "search", "routing"))

    sources = config.load_sources()
    lock = _lock_entries(raw_dir)
    manifest_sources: dict = {}
    tile_meta: dict = {"region": rid, "sources": {}}

    if need_data:
        # --- extract (cached) ---------------------------------------------------
        cache_dir = out_dir / "cache" / rid
        key = extract_cache_key(pbf, region, admin_levels)
        cache_path = cache_dir / f"extract-{key}.pkl.gz"
        if use_cache and cache_path.exists():
            extract = timer.run("extract (cached)", osm_extract.load_extract, cache_path)
            timer.t["extract"] = timer.t.pop("extract (cached)")
            stats["extract_cached"] = True
        else:
            extract = timer.run("extract", osm_extract.extract_region, pbf, region.bbox,
                                buffer_m=region.bbox_buffer_m, admin_levels=admin_levels, region_id=rid)
            stats["extract_cached"] = False
            if use_cache:
                osm_extract.save_extract(extract, cache_path)
                for old in cache_dir.glob("extract-*.pkl.gz"):
                    if old != cache_path:
                        old.unlink(missing_ok=True)
        stats["extract"] = {k: v for k, v in extract.stats.items() if k not in ("unknown_values",)}
        stats["unknown_tag_values"] = extract.stats.get("unknown_values", {})

        osm_rec = source_record(pbf, raw_dir, lock)
        manifest_sources["osm"] = {**osm_rec, "timestamp": _osm_timestamp(pbf)}
        tile_meta["sources"]["osm"] = osm_rec["md5"]

        # --- rasters ---------------------------------------------------------------
        tiles_by_level = tiling.region_tiles(region)
        all_tiles = [t for ts in tiles_by_level.values() for t in ts]
        dem_box = tiling.tiles_lonlat_bbox(all_tiles, 0.01)
        dem_files, dem_missing = find_dem_files(raw_dir, dem_box)
        if not dem_files:
            raise FileNotFoundError(f"no DEM tiles under {raw_dir / 'dem'} (run fetch.py --region {rid})")
        if dem_missing:
            warnings.append(f"DEM tiles missing (edges filled by nearest value): {', '.join(dem_missing)}")
        dem = timer.run("dem", DemSampler.from_files, dem_files, dem_box)
        detail_tiles = [t for lvl in region.detail_levels for t in tiles_by_level.get(lvl, [])]
        horizon_tiles = [t for lvl in region.horizon_levels for t in tiles_by_level.get(lvl, [])]
        lc_box_d = tiling.tiles_lonlat_bbox(detail_tiles, 0.005)
        lc_files, lc_missing = find_landcover_files(raw_dir, tiling.tiles_lonlat_bbox(all_tiles, 0.005))
        if not lc_files:
            raise FileNotFoundError(f"no WorldCover tiles under {raw_dir / 'worldcover'}")
        if lc_missing:
            warnings.append(f"WorldCover tiles missing (no land cover there): {', '.join(lc_missing)}")
        lc_detail = timer.run("landcover_detail", LandcoverSampler.from_files, lc_files, lc_box_d)
        lc_by_level = {lvl: lc_detail for lvl in region.detail_levels}
        if horizon_tiles:
            lc_box_h = tiling.tiles_lonlat_bbox(horizon_tiles, 0.005)
            lc_h = timer.run("landcover_horizon", LandcoverSampler.from_files, lc_files, lc_box_h)
            for lvl in region.horizon_levels:
                lc_by_level.setdefault(lvl, lc_h)
        dem_recs = [source_record(p, raw_dir, lock) for p in dem_files]
        lc_recs = [source_record(p, raw_dir, lock) for p in lc_files]
        manifest_sources["dem"] = dem_recs
        manifest_sources["landcover"] = lc_recs
        tile_meta["sources"]["dem"] = _combined_md5(dem_recs)
        tile_meta["sources"]["landcover"] = _combined_md5(lc_recs)
        stats["rasters"] = {"dem_bbox": [round(v, 4) for v in dem_box], "dem_valid_fraction":
                            round(dem.valid_fraction, 5), "dem_files": [r["name"] for r in dem_recs],
                            "landcover_files": [r["name"] for r in lc_recs]}

        # --- surface, trails, buildings ----------------------------------------------
        def surface_stage():
            density = contexts.density_grid_for(extract.buildings)
            ctxs = contexts.road_contexts(extract.roads, dem, density)
            return density, assign_surfaces(extract.roads, ctxs)

        density, surf = timer.run("surface", surface_stage)
        stats["surface"] = surf
        stats["trails"] = timer.run("trails", assign_trail_difficulty, extract.roads, dem.heights_at_lonlat,
                                    _glacier_test(extract))
        azones = load_archetype_zones(archetype_zones_path)

        def buildings_stage():
            bctx = contexts.building_contexts(extract.buildings, dem, azones, density)
            return infer_buildings(extract.buildings, bctx)

        stats["buildings"] = timer.run("buildings", buildings_stage)

        # --- search ------------------------------------------------------------------
        landmark_ids, landmark_refs = load_landmarks(landmarks_path)
        stats["landmark_kinds_applied"] = apply_landmark_kinds(extract, landmarks_path)
        entries = timer.run("search_entries", search_index.build_entries, extract.places, extract.pois,
                            extract.admin, landmark_ids)
        sids = search_index.search_ids(entries)
        simp: dict[int, int] = {}
        for e in entries:
            simp.setdefault(e.osm_ref, int(e.importance))
        if "search" in stages:
            stats["search"] = timer.run("search", search_index.write_index, index_path, entries)

        # --- tiles + pack --------------------------------------------------------------
        if "tiles" in stages:
            bzones: BiomeZones = load_zones(biome_zones_path)
            tstats: dict = {}
            tiles = timer.run("tiles", tiling.build_tiles, region, extract, dem, lc_by_level, bzones,
                              data_version=config.PIPELINE_DATA_VERSION, meta=tile_meta, search_ids=sids,
                              search_importance=simp, landmark_refs=landmark_refs, workers=workers, stats=tstats)
            stats["tiles"] = tstats
            stats["pack"] = timer.run("pack", write_pack, pack_path, rid, config.PIPELINE_DATA_VERSION, tiles)
            del tiles

        # --- routing ----------------------------------------------------------------------
        if "routing" in stages:
            def routing_stage():
                g = routing.build_graph(extract.roads, elev=lambda x, z: dem.sample_game(x, z))
                return g, routing.write_graph(graph_path, g)

            graph, stats["routing"] = timer.run("routing", routing_stage)
            del graph

    # --- manifest -------------------------------------------------------------------------
    manifest = _manifest(region, reg_dir, pack_path, index_path, graph_path, stats, manifest_sources, sources,
                         timer, t_start, warnings)
    if pack_path.exists():
        if not need_data and manifest_path.exists():
            # Partial run (e.g. QA only): keep what the last full build measured.
            prev = json.loads(manifest_path.read_text(encoding="utf-8"))
            for k in ("sources", "tile_counts"):
                manifest[k] = prev.get(k, manifest[k])
            manifest["stats"] = {**prev.get("stats", {}), "timings_s": manifest["stats"]["timings_s"],
                                 "warnings": manifest["stats"]["warnings"]}
        write_manifest(manifest_path, manifest)

    # --- QA -----------------------------------------------------------------------------------
    if "qa" in stages:
        if not pack_path.exists():
            raise FileNotFoundError(f"{pack_path} missing; run the tiles stage first")
        from . import qa_export  # needs pillow (a dev dependency); only for --qa
        qa_sum = timer.run("qa", qa_export.export_qa, pack_path, reg_dir / "qa",
                           leaf_level=region.leaf_level, region_meta=manifest)
        stats["qa"] = qa_sum
        manifest["stats"]["timings_s"] = dict(timer.t)
        manifest["stats"]["qa"] = qa_sum
        if pack_path.exists():
            write_manifest(manifest_path, manifest)

    manifest["stats"]["timings_s"]["total"] = round(time.perf_counter() - t_start, 2)
    if pack_path.exists():
        write_manifest(manifest_path, manifest)
    (reg_dir / "BUILD_REPORT.md").write_text(build_report(manifest, stats, warnings), encoding="utf-8")
    log.info("built %s in %.1f s -> %s", rid, time.perf_counter() - t_start, reg_dir)
    return manifest


def _manifest(region: config.Region, reg_dir: Path, pack_path: Path, index_path: Path, graph_path: Path,
              stats: dict, msources: dict, sources: dict, timer: _Timer, t_start: float, warnings: list[str]) -> dict:
    files = [file_entry(p) for p in (pack_path, index_path, graph_path) if p.exists()]
    surf = stats.get("surface", {})
    pct = surf.get("pct_by_source", {})
    b = stats.get("buildings", {})
    counts = (stats.get("extract") or {}).get("counts", {})
    tiles = stats.get("tiles", {})
    rt = stats.get("routing", {})
    srch = stats.get("search", {})
    pack = stats.get("pack", {})
    summary = {
        "roads": counts.get("roads", 0), "buildings": counts.get("buildings", 0), "pois": counts.get("pois", 0),
        "places": counts.get("places", 0), "areas": counts.get("areas", 0), "lines": counts.get("lines", 0),
        "admin_areas": counts.get("admin", 0),
        "road_km": surf.get("total_km", 0.0),
        "surface_tagged_pct": pct.get("TAGGED", 0.0), "surface_derived_pct": pct.get("DERIVED", 0.0),
        "surface_inferred_pct": pct.get("INFERRED", 0.0), "surface_default_pct": pct.get("DEFAULT", 0.0),
        "surface_mix_pct": surf.get("surface_pct", {}),
        "building_levels_inferred_pct": _pct(b.get("levels_inferred", 0), b.get("buildings", 0)),
        "building_levels_tagged_pct": b.get("levels_tagged_pct", 0.0),
        "building_archetypes": b.get("archetypes", {}),
        "trails_sac_tagged": (stats.get("trails") or {}).get("tagged", 0),
        "trails_sac_inferred": (stats.get("trails") or {}).get("inferred", 0),
        "tiles": tiles.get("tiles", 0), "leaf_tiles": tiles.get("leaf_tiles", 0),
        "buildings_in_tiles": tiles.get("buildings_assigned", 0),
        "tile_features": tiles.get("features", {}), "chunk_bytes": tiles.get("chunk_bytes", {}),
        "pack_bytes": pack.get("bytes", 0), "max_tile_bytes": pack.get("max_tile_bytes", 0),
        "search_entries": srch.get("entries", 0), "search_keys": srch.get("keys", 0),
        "graph_nodes": rt.get("nodes", rt.get("node_count", 0)), "graph_edges": rt.get("edges", rt.get("edge_count", 0)),
        "timings_s": dict(timer.t),
        "warnings": list(warnings),
    }
    attribution = [sources[k]["attribution"] for k in ("osm", "dem", "landcover") if k in sources]
    return {
        "region": region.id, "name": {"en": region.name_en, "ne": region.name_ne},
        "data_version": config.PIPELINE_DATA_VERSION, "pipeline_version": __version__,
        "built_at": datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
        "bbox_lonlat": list(region.bbox), "horizon_bbox_lonlat": list(region.horizon_bbox),
        "bbox_game": [round(v, 3) for v in projection.bbox_lonlat_to_game(*region.bbox)],
        "detail_levels": list(region.detail_levels), "horizon_levels": list(region.horizon_levels),
        "leaf_level": region.leaf_level, "height_grid": region.height_grid, "biome_grid": region.biome_grid,
        "scale_model": "identity",
        "files": files,
        "tile_counts": pack.get("tile_counts", {}),
        "sources": msources,
        "attribution": attribution,
        "stats": summary,
    }


def build_report(manifest: dict, stats: dict, warnings: list[str]) -> str:
    """Human-readable ``BUILD_REPORT.md``."""
    s = manifest.get("stats", {})
    lines = [f"# Build report: {manifest['region']}", "",
             f"* Built at {manifest.get('built_at')} by pipeline {manifest.get('pipeline_version')} "
             f"(data_version {manifest.get('data_version')}).",
             f"* bbox {manifest.get('bbox_lonlat')}, horizon {manifest.get('horizon_bbox_lonlat')}; "
             f"detail levels {manifest.get('detail_levels')}, horizon levels {manifest.get('horizon_levels')}.",
             f"* Extract {'from cache' if stats.get('extract_cached') else 'read from the PBF'}; "
             f"{stats.get('landmark_kinds_applied', 0)} POI kinds set from curated landmarks.", ""]
    lines += ["## Files", "", "| File | Bytes | MB | SHA-256 |", "|---|---:|---:|---|"]
    for f in manifest.get("files", []):
        lines.append(f"| {f['path']} | {f['bytes']:,} | {f['bytes'] / 1e6:.2f} | `{f['sha256'][:16]}…` |")
    lines += ["", "## Tiles", "", "| Level | Tile size | Tiles |", "|---:|---:|---:|"]
    for lvl, n in sorted(manifest.get("tile_counts", {}).items(), key=lambda kv: int(kv[0])):
        lines.append(f"| {lvl} | {projection.tile_size(int(lvl)):,.0f} m | {n:,} |")
    tb = stats.get("tiles", {})
    if tb:
        total = max(1, tb.get("tile_bytes", 0))
        lines += ["", "Bytes per chunk type (stored, after DEFLATE):", "", "| Chunk | Bytes | Share |",
                  "|---|---:|---:|"]
        for k, v in sorted(tb.get("chunk_bytes", {}).items(), key=lambda kv: -kv[1]):
            lines.append(f"| {k} | {v:,} | {100.0 * v / total:.1f}% |")
        lines += ["", f"Leaf tiles: {tb.get('leaf_tiles', 0):,}; buildings placed {tb.get('buildings_assigned', 0):,} "
                  f"of {tb.get('buildings_in_extract', 0):,} extracted (the rest lie in the bbox buffer).",
                  f"Feature records: {json.dumps(tb.get('features', {}))}"]
    lines += ["", "## Content", "", "| What | Value |", "|---|---:|"]
    for k in ("roads", "road_km", "buildings", "pois", "places", "areas", "lines", "admin_areas",
              "search_entries", "search_keys", "graph_nodes", "graph_edges"):
        v = s.get(k, 0)
        lines.append(f"| {k} | {v:,} |" if isinstance(v, int) else f"| {k} | {v} |")
    lines += ["", "## Inference provenance", "", "| What | % |", "|---|---:|"]
    for k in ("surface_tagged_pct", "surface_derived_pct", "surface_inferred_pct", "surface_default_pct",
              "building_levels_tagged_pct", "building_levels_inferred_pct"):
        lines.append(f"| {k} | {s.get(k, 0.0)} |")
    lines += ["", f"Trails: sac_scale tagged on {s.get('trails_sac_tagged', 0):,}, inferred on "
              f"{s.get('trails_sac_inferred', 0):,}.", "",
              f"Surface mix (% of length): {json.dumps(s.get('surface_mix_pct', {}))}", "",
              f"Building archetypes: {json.dumps(s.get('building_archetypes', {}))}"]
    surf = stats.get("surface", {})
    if surf.get("by_class"):
        lines += ["", "Surface provenance by class (% of length):", "",
                  "| Class | km | tagged | derived | inferred | default |", "|---|---:|---:|---:|---:|---:|"]
        for cls, row in surf["by_class"].items():
            p = row["pct_by_source"]
            lines.append(f"| {cls} | {row['km']:.1f} | {p.get('TAGGED', 0)} | {p.get('DERIVED', 0)} | "
                         f"{p.get('INFERRED', 0)} | {p.get('DEFAULT', 0)} |")
    lines += ["", "## Timings (s)", "", "| Stage | s |", "|---|---:|"]
    for k, v in s.get("timings_s", {}).items():
        lines.append(f"| {k} | {v} |")
    unk = stats.get("unknown_tag_values") or {}
    if unk:
        lines += ["", "## Unrecognised tag values (top)", ""]
        for key, vals in sorted(unk.items()):
            shown = ", ".join(f"`{v}` ({n})" for v, n in list(vals)[:10])
            lines.append(f"* **{key}**: {shown}")
    if warnings:
        lines += ["", "## Warnings", ""] + [f"* {w}" for w in warnings]
    lines += ["", "## Attribution", ""] + [f"* {a}" for a in manifest.get("attribution", [])]
    return "\n".join(lines) + "\n"


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------
def main(argv: Sequence[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description="Build a Ghumante region (tiles, pack, search, routing, QA).")
    ap.add_argument("--region", required=True, help="region id from config/regions.yaml")
    ap.add_argument("--pbf", type=Path, help="OSM extract (default: <raw-dir>/osm/nepal.osm.pbf)")
    ap.add_argument("--raw-dir", type=Path, help=f"source data directory (default: {config.RAW_DIR})")
    ap.add_argument("--out", type=Path, help=f"build directory (default: {config.BUILD_DIR})")
    ap.add_argument("--qa", action="store_true", help="also write the QA export (qa/ next to the pack)")
    ap.add_argument("--no-cache", action="store_true", help="re-extract from the PBF and do not write a cache")
    ap.add_argument("--stages", help=f"comma-separated subset of {','.join(ALL_STAGES)}")
    ap.add_argument("--workers", type=int, help="tile-encoding processes (default: min(4, cpus))")
    ap.add_argument("--regions-file", type=Path, help="alternative regions.yaml")
    ap.add_argument("-q", "--quiet", action="store_true")
    args = ap.parse_args(argv)
    logging.basicConfig(level=logging.WARNING if args.quiet else logging.INFO,
                        format="%(asctime)s %(levelname)s %(message)s", datefmt="%H:%M:%S")
    regions = config.load_regions(args.regions_file)
    if args.region not in regions:
        ap.error(f"unknown region {args.region!r}; known: {', '.join(sorted(regions))}")
    stages = [s.strip() for s in args.stages.split(",") if s.strip()] if args.stages else None
    m = build_region(regions[args.region], pbf=args.pbf, raw_dir=args.raw_dir, out_dir=args.out, qa=args.qa,
                     use_cache=not args.no_cache, stages=stages, workers=args.workers)
    s = m["stats"]
    print(json.dumps({"region": m["region"], "tile_counts": m["tile_counts"], "pack_mb": round(s["pack_bytes"] / 1e6, 2),
                      "search_entries": s["search_entries"], "graph_nodes": s["graph_nodes"],
                      "graph_edges": s["graph_edges"], "surface_tagged_pct": s["surface_tagged_pct"],
                      "building_levels_inferred_pct": s["building_levels_inferred_pct"],
                      "timings_s": s["timings_s"]}, ensure_ascii=False, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
