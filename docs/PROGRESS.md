# Ghumante: progress log

> The running log: what's done, what's next, known issues, and decisions with the reasons for them. The newest entry is at the top.

## Current status (2026-10-04)

**Milestone:** M0 Foundations, in progress (see the [ROADMAP](ROADMAP.md#m0-foundations-planning-and-pipeline-proof)).

| Area | State |
|---|---|
| Planning docs | ARCHITECTURE, ROADMAP, LICENSES, DATA_FORMATS, ASSET_MANIFEST written; open questions in ARCHITECTURE §14 |
| Source data | Nepal OSM (2026-10-02), Copernicus GLO-30 tiles, WorldCover N27E084 downloaded and verified |
| Tag coverage | [report](reports/tag_coverage.md) + [findings](reports/TAG_COVERAGE_FINDINGS.md) |
| Landmarks | [60/66 resolved](reports/landmarks.md) against OSM |
| Pipeline | ✅ M0: `fetch.py` + `build.py` build the Kathmandu Valley pack, search index and routing graph; ~1.8 k fixture tests + 44 real-data tests pass (stats below) |
| C# core | ✅ readers for pack, tiles, search and routing, plus geo and save; 111 `dotnet test` cases against the Python golden files; also compiles as netstandard2.1 / C# 9 |
| Unity project | ✅ skeleton, unverified in Unity (can't be opened or run in this cloud environment, which has no Unity licence); compile-checked against reference assemblies |
| CI | ✅ pipeline, core, localisation, Unity (compile check always; GameCI jobs need secrets) and nightly data (incl. real-data tests) |

---

## 2026-10-04: session 1 (M0 kickoff)

### Done
* Read the brief and wrote down the pushback: [ARCHITECTURE §2](ARCHITECTURE.md#2-where-this-brief-needs-pushback), 15 items.
* Ran a parallel research pass with adversarial fact-checking over engine, delivery, licensing, performance, scale precedents and data tooling. Results are in [ARCHITECTURE Appendix A](ARCHITECTURE.md#appendix-a-verified-platform-facts-research-pass-2026-10-04) and [reports/research_2026-10-04.md](reports/research_2026-10-04.md).
* Downloaded the sources. **Geofabrik is unreachable from this cloud environment** (TLS connection reset at the proxy, consistent with Geofabrik blocking cloud egress), so the same daily Nepal extract came from the OSMToday/geo2day mirror, MD5-verified. `sources.yaml` lists Geofabrik first and the mirror as fallback.
* **Tag coverage report.** It scans 9.5 M tagged ways in about 5 min. Headline numbers: 85% of road length has no surface tag, 99.6% of buildings have no levels, sac_scale is on 2% of trails, and name:ne is on 14–29% of villages. The consequences became ADR-005 (inference-first).
* **Landmark verification.** 60 of the 66 brief landmarks resolved to OSM objects with the expected tags. Weak or missing: Gorkha Durbar, the Bhote Koshi bungee, the Kalinchowk temple, the Pokhara zipline, and the Muktinath temple (it matched "Muktinath Pond", which needs a manual check).
* Wrote the data contract: `model.py` enums (exported to `shared/enums.json`), [DATA_FORMATS.md](DATA_FORMATS.md) (GHT1 tiles, GHPK packs, GHSI search, GHRG routing), and the NPL-TM84 projection plus quadtree with tests.

### Decisions (see ARCHITECTURE §13)
* **ADR-004, decided by the product owner: the whole playable world is true 1:1**, horizontally and vertically. 2–4 h journeys are fine. Consequences: no warp or road generalisation; earth curvature is rendered for correct skylines; long trips get ride-along transport, cruise assist and save-anywhere; data size becomes the main engineering cost (sized from the valley build). An earlier recommendation of variable scale (towns 1:1, country ~1:6) was superseded the same day.
* **ADR-017, product-owner requirement: playable in portrait mode.** Both portrait and landscape are fully supported, with live rotation. Layouts are driven by aspect ratio. In portrait: a one-thumb control scheme, a minimum horizontal FOV per camera rig, and map bottom sheets. `ProjectSetup` applies it: auto-rotation with portrait and both landscape orientations allowed (not upside-down portrait), and an orientation-neutral UI Toolkit panel scale (reference 1500×1500, match 0.5).
* ADR-001 **Unity 6.3 LTS**, not 6.0 LTS. 6.0 support ends this month; 6.3 is supported to Dec 2027.
* ADR-007 all UI in UI Toolkit, for Devanagari shaping. A device spike is the first task in M1.
* ADR-013 / 014 / 015: layer separation for ODbL; no HydroSHEDS; base install ≤ 150 MB with the CDN as the baseline (ODR is deprecated in iOS 27).

### Known issues / risks
* Unity can't run in this cloud environment, so engine code is compile-checked against reference assemblies only. The first real Unity open happens on your machine or in CI once secrets are added (`docs/CI_SECRETS.md`).
* The mirror's OSM file contains contributor metadata. Our outputs never include it, and `fetch.py` now strips user names, uids and changesets from files fetched from that mirror (LICENSES §1.1). The local file fetched before that change still has them until it is re-fetched.
* With a 1:1 world, **all-Nepal data size** (~141 k leaf tiles) and **content density between landmarks** are the main risks. Both are sized from the M0 valley build.

### End-of-session summary

**Kathmandu Valley build** (`python3 build.py --region kathmandu_valley --qa`, extract cached, 4 tile processes): 3.5 min wall (tiles 93 s, QA 54 s, buildings 28 s, WorldCover horizon mosaic 17 s, extract from cache 10 s). From scratch the PBF extract alone takes ~200 s. Details: `pipeline/build/regions/kathmandu_valley/BUILD_REPORT.md` and `pipeline/README.md`.

| Output | Value |
|---|---:|
| `kathmandu_valley.ghpk` | 60.95 MB (HGHT 60%, BLDG 24%) |
| `kathmandu_valley.search.ghsi` | 0.69 MB, 5 956 entries |
| `kathmandu_valley.route.ghrg` | 10.97 MB, 85 488 nodes, 205 912 edges |
| Tiles | 2 282 (L5 42, L6 132, L7 460, L8 80, L9 320, L10 1 248) |
| Roads | 53 523 (10 364 km in the leaf tiles); surface tagged 13.1%, inferred 84.3% |
| Buildings | 556 960 (levels inferred on 96.6%) |
| POIs / places | 42 989 / 1 118 |

The manifest records the OSM snapshot time (the mirror's PBF header is empty, so the lock file's `Last-Modified`, 2026-10-04T07:33:52Z) and the licence-mandated attribution from LICENSES §1.2/§1.3.

**CI.** `pipeline.yml` (fixture tests), `core.yml` (golden files, netstandard2.1 compile, `dotnet test`), `localization.yml`, `unity.yml` (compile check + API audit without secrets; GameCI builds and TestFlight with secrets) and `nightly-data.yml` (fetch, coverage, landmarks, one-region build, `pytest -m realdata`, artifacts). `global.json` pins the .NET 8 SDK. Not done yet: CDN upload, all-region nightly builds, coverage-drop warnings (ARCHITECTURE §6.4, §11).

### Next
* M1 device spike (ROADMAP 1.1): open the project in Unity 6.3, run `ProjectSetup`, and check the TextSpike screen on iOS and Android.
* Add the CI secrets (`docs/CI_SECRETS.md`) so the Unity build jobs run.
* Pack loading in Unity through the Core readers (ROADMAP 1.2).
