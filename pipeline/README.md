# Ghumante data pipeline

This is the offline pipeline that turns OpenStreetMap, the Copernicus GLO-30 DEM and ESA WorldCover into the files the game streams:

* `GHT1` tiles in a `GHPK` region pack
* a `GHSI` search index
* a `GHRG` routing graph
* a manifest

For the design, see [docs/ARCHITECTURE.md](../docs/ARCHITECTURE.md) §5–6. The binary formats are defined in [docs/DATA_FORMATS.md](../docs/DATA_FORMATS.md), which is normative. Real-data quirks are in [docs/reports/TAG_COVERAGE_FINDINGS.md](../docs/reports/TAG_COVERAGE_FINDINGS.md).

## Install

You need Python 3.11+ and GDAL-free wheels only.

```sh
cd pipeline
python3 -m pip install -e '.[dev]'     # numpy scipy pyproj shapely>=2.1 rasterio osmium pyyaml (+ pytest pillow)
```

`pillow` is needed only for `--qa` and for the tests.

## Fetch source data

```sh
python3 fetch.py --region kathmandu_valley          # OSM Nepal extract + GLO-30 tiles + WorldCover for the horizon bbox
python3 fetch.py --region kathmandu_valley --dry-run   # show the plan
python3 fetch.py --verify                           # re-hash what is on disk against SOURCES.lock.json
```

Downloads go to `data/raw/`:

| Path | Contents |
|---|---|
| `osm/nepal.osm.pbf` | about 468 MB. Geofabrik is tried first, then the OSMToday mirror, MD5 checked. |
| `dem/copernicus/*.tif` | GLO-30 COGs from AWS |
| `worldcover/*.tif` | ESA WorldCover 2021 v200 |
| `SOURCES.lock.json` | sizes, MD5 and SHA-256 of every file. The build reads the hashes from here. |

## Build a region

```sh
python3 build.py --region thamel_test --qa          # 2 km test region, ~15 s
python3 build.py --region kathmandu_valley --qa     # the M0 valley, see timings below
```

| Option | Meaning |
|---|---|
| `--region ID` | A region from `config/regions.yaml`: bbox, horizon bbox, detail and horizon levels, grid sizes. |
| `--pbf PATH`, `--raw-dir PATH` | Alternative inputs. The defaults are `data/raw/osm/nepal.osm.pbf` and `data/raw/`. |
| `--out DIR` | The build directory (default `build/`). |
| `--qa` | Also write the QA export (decoded from the pack). |
| `--no-cache` | Re-read the PBF and do not write the extract cache. |
| `--stages tiles,search,routing,qa` | Run a subset. `--stages qa` re-exports QA from an existing pack without reading any source. |
| `--workers N` | Tile-encoding processes (default `min(4, cpus)`). The output is identical for any N. |
| `--regions-file PATH` | An alternative `regions.yaml` (used by the tests). |

The stages run in this order. Each one is timed in the manifest and in `BUILD_REPORT.md`.

1. **extract** (`osm_extract.py`): the PBF and region bbox plus a buffer become an `Extract` of roads, buildings, areas, lines, POIs, places and admin areas. The result is cached in `build/cache/<region>/extract-<hash>.pkl.gz`. The hash covers the PBF name, size and mtime, the bbox, the buffer, the admin levels, the source of `osm_extract.py`, `tags.py` and `model.py`, and `data_version`.
2. **rasters** (`dem.py`, `landcover.py`): one DEM mosaic over every tile's lon/lat extent, and WorldCover mosaics for the detail and horizon extents.
3. **surface** (`contexts.py`, `surface.py`): per-road context (building density within 300 m from a 50 m count grid, plus DEM elevation and slope at the road's middle) feeds an empirical surface model fitted on the tagged roads.
4. **trails** (`trails.py`): infers `sac_scale` from slope, altitude and glacier proximity.
5. **buildings** (`contexts.py`, `buildings.py`): infers levels, height, roof, materials and archetype from density, elevation and archetype zones.
6. **search** (`search_index.py`): writes `<region>.search.ghsi`, with landmark ids from `config/landmarks.resolved.json`.
7. **tiles** (`tiling.py`) and **pack** (`pack.py`): write `<region>.ghpk`. Heights and biomes cover every detail and horizon level. Vector content goes only on leaf tiles (M0). Before the tiles are encoded, the W2 detail pass (docs/W2_DETAIL_CONTRACT.md) runs once per way: `structures.py` finds bridges, flyovers, underpasses, tunnels and fords and solves absolute deck heights (the `RSTR` chunk), `corridors.py` derives every road's clear corridor (`RATR.corridor_dm`, never under 4.8 m) and trims the buildings that stand in it (`BFNT` `TRIMMED_FOR_ROAD`), and the car rule feeds routing. DATA_FORMATS.md 1.11 and 1.15 hold the rules; `BUILD_REPORT.md` counts trims per tile.
8. **routing** (`routing.py`): writes `<region>.route.ghrg`.
9. **manifest**: writes `<region>.manifest.json` (DATA_FORMATS §2) with the SHA-256 of each file, tile counts, sources, attribution, stats and timings.
10. **qa** (`qa_export.py`, with `--qa`): decodes the pack into `qa/`.
11. **report**: writes `BUILD_REPORT.md`, which includes per-chunk bytes, surface provenance by road class, building archetypes, unrecognised tag values and warnings.

### Outputs

```
build/
  cache/<region>/extract-<hash>.pkl.gz
  regions/<region>/
    <region>.ghpk                  tiles (GHPK of GHT1)
    <region>.search.ghsi           search index
    <region>.route.ghrg            routing graph
    <region>.manifest.json         manifest (DATA_FORMATS §2)
    BUILD_REPORT.md                human-readable build report
    qa/                            with --qa (DATA_FORMATS §5)
      index.json                   bbox, tiles (corners in lon/lat), layers, biome palette, stats
      roads.geojson trails.geojson buildings.geojson areas.geojson lines.geojson pois.geojson
      buildings/<L>/<tx>_<ty>.geojson   per-tile buildings (lazy loading in the viewer)
      hillshade/<L>/<tx>_<ty>.png  leaf tiles, row 0 north, vertex-registered
      biome/<L>/<tx>_<ty>.png      leaf tiles, colours = qa_export.BIOME_PALETTE
```

The pack, search index and routing graph are **byte-deterministic**. Building the same inputs twice gives identical SHA-256 sums; only `built_at` and the timings in the manifest change. `build/` is not checked in.

## View the QA export

```sh
cd ..                                   # repository root
python3 tools/qa-viewer/serve.py        # serves the repo root; prints one URL per export found
# open http://localhost:8765/tools/qa-viewer/?region=kathmandu_valley
```

The viewer overlays the decoded layers on OpenStreetMap: roads coloured by surface or by `surface_source`, buildings by archetype, areas, POIs, hillshade and biome rasters, and the tile grid, with click-to-inspect. Because the layers are decoded from the pack, a good overlay validates the whole chain: extract, projection, clipping, encoding, decoding and unprojection. See `tools/qa-viewer/README.md`.

## Tests

```sh
cd pipeline
python3 -m pytest -q                       # unit + synthetic end-to-end (realdata tests skip without data)
python3 -m pytest -q -m "not realdata"     # never touch the 468 MB extract
python3 -m pytest -q tests/test_tiling.py tests/test_seams.py tests/test_build_e2e.py
python3 -m pytest -q tests/test_build_realdata.py   # checks the built kathmandu_valley outputs
```

* `tests/fixtures/synth/make_synth.py` generates the synthetic end-to-end fixture. It is a 2 km × 2 km invented neighbourhood near Thamel: OSM XML (street grid, trails, about 380 buildings including a multipolygon, a lake on a tile corner, a forest with a hole, a river, POIs, places and a district boundary) plus small DEM and WorldCover GeoTIFFs. Run `python3 tests/fixtures/synth/make_synth.py OUT_DIR` to look at it.
* `test_build_e2e.py` runs the whole build on that fixture and checks:
  * the manifest and per-tile CRCs
  * determinism, across two builds in different directories with different worker counts
  * that search finds the temple (Latin and Devanagari)
  * that routes exist between the two places
  * that QA GeoJSON lands within 2 cm of the source lon/lat
* `test_seams.py` checks, on the same build:
  * `HGHT` and `BIOM` edges are bit-identical for every neighbouring tile pair at every level
  * parent and child heights agree
  * road and line cut points match across tile borders, with context points
  * every building is stored exactly once
* `test_build_realdata.py` is marked `realdata`. It is skipped when `build/regions/kathmandu_valley` is missing, and checks:
  * "Boudha" finds Boudhanath
  * "काठमाडौं" finds Kathmandu
  * a motorbike route from Thamel to Boudhanath exists
  * seams hold on 200 random neighbour pairs
  * the pack is under 80 MB

## Real builds (2026-10-04, 4 cores, 15 GB RAM)

Both builds ran with 4 tile workers. The full numbers are in each region's `BUILD_REPORT.md` and manifest.

| | `thamel_test` | `kathmandu_valley` |
|---|---:|---:|
| Tiles per level | L7 12, L8 42, L9 4, L10 9 | L5 42, L6 132, L7 460, L8 80, L9 320, L10 1 248 (2 282 in all) |
| Pack (`.ghpk`) | 2.53 MB | **60.95 MB** (budget 80 MB). HGHT is 60%, BLDG 24%, ROAD 6%, AREA 5%. The largest tile is 251 kB. |
| Search index (`.ghsi`) | 587 entries, 75 kB | 6 373 entries, 14 650 keys, 0.73 MB |
| Routing graph (`.ghrg`) | 8 824 nodes, 22 081 edges, 0.97 MB | 87 940 nodes, 211 696 edges, 11.6 MB |
| Roads in extract | 5 624 (631 km) | 53 523 (11 302 km) |
| Buildings in tiles / extracted | 34 693 / 87 147 | 542 261 / 556 960 (the rest are in the bbox buffer) |
| Surface by length: tagged / derived / inferred / default | 23.8 / 0.0 / 75.1 / 1.1 % | 13.1 / 2.4 / 84.3 / 0.3 % |
| Building levels inferred | 94.5 % | 96.6 % |
| Trails with sac_scale tagged / inferred | 0 / 1 259 | 40 / 17 482 |
| QA export | 36 MB | 586 MB (GeoJSON + 2 × 1 248 PNGs) |

Valley stage timings in seconds:

| Stage | Time (s) |
|---|---:|
| extract from the PBF, not cached | 202 |
| extract from cache | 10–12 |
| DEM mosaic | 2–12 |
| WorldCover horizon mosaic | 14 |
| surface | 2 |
| trails | 2 |
| buildings | 28 |
| search | 1.5 |
| **tiles (4 processes)** | 87 |
| pack | 0.2 |
| routing | 1.6 |
| QA | 55 |

Totals:

* with a cached extract and `--qa`: **3.4 min** wall
* from scratch (`--no-cache`, no QA): **5.7 min** wall

A from-scratch build and a cached build produced byte-identical pack, index and graph. Checks on the valley build (`tests/test_build_realdata.py`):

* "Boudha" finds the Baudha neighbourhood first, about 100 m from the stupa, with Boudhanāth Stupa (STUPA, landmark) next.
* "Boudhanath" finds the stupa first.
* "काठमाडौं" finds Kathmandu.
* The motorbike route from Thamel to Boudhanath is 7.5 km and takes 8.0 min.
* Seams hold on 200 random neighbour pairs.
