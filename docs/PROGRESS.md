# Ghumante: progress log

> The running log: what's done, what's next, known issues, and decisions with the reasons for them. The newest entry is at the top.

## Current status (2026-10-05)

**Milestone:** M0 Foundations done; M1 Kathmandu vertical slice started (see [M1_PLAN](M1_PLAN.md)).

| Area | State |
|---|---|
| Planning docs | ARCHITECTURE, ROADMAP, LICENSES, DATA_FORMATS, ASSET_MANIFEST written; open questions in ARCHITECTURE §14 |
| Source data | Nepal OSM (2026-10-02), Copernicus GLO-30 tiles, WorldCover N27E084 downloaded and verified |
| Tag coverage | [report](reports/tag_coverage.md) + [findings](reports/TAG_COVERAGE_FINDINGS.md) |
| Landmarks | [60/66 resolved](reports/landmarks.md) against OSM |
| Pipeline | ✅ M0: `fetch.py` + `build.py` build the Kathmandu Valley pack, search index and routing graph; ~1.8 k fixture tests + 44 real-data tests pass (stats below) |
| C# core | ✅ readers for pack, tiles, search and routing, plus geo, save, motion and haptics pacing; 174 `dotnet test` cases against the Python golden files; also compiles as netstandard2.1 / C# 9 |
| Unity project | ✅ runs in the owner's Mac editor (Unity 6000.3.25f1): animated main menu, haptics, Settings, Devanagari TextSpike; compile-checked and API-audited against Unity 6000.3 |
| CI | ✅ pipeline, core, localisation, Unity (compile check always; GameCI jobs need secrets) and nightly data (incl. real-data tests) |

---

## 2026-10-05: session 2 (first run on the Mac, fun menu, M1 start)

### Done
* **The project ran in the owner's Mac editor:** Project Setup applied, the main menu at 60 fps, and Devanagari shaping correct (the live rows match the HarfBuzz references). Fixed from that first run: the invisible toast, the debug HUD covering the OSM credit, and the TextSpike layout cut-off (UI Toolkit's default `flex-shrink: 1`). Project Setup now also turns on frame-timing stats and selects the new Input System only, so the two start-up dialogs no longer appear.
* **Animated main menu with haptics** (owner request).
  * **Motion:** a living Himalaya backdrop (clouds, sun, birds, lungta prayer flags, parallax from tilt or the mouse); an entrance sequence; springy press feel; particle bursts; playful reactions on every button; a Settings sheet (bottom sheet in portrait, side panel in landscape; drag or back to close).
  * **Reduce motion** follows the OS until the player chooses.
  * **Settings persist** atomically (`LocalSaveStore`).
  * **The app now starts on the main menu.**
  * Details and what to try are in `game/Assets/Ghumante/UI/README.md` ("Motion").
* **Haptics on phones:**
  * iOS uses UIFeedbackGenerator through `Plugins/iOS/GhumanteHaptics.mm`, and only on iPhones (not iPads or Macs).
  * Android uses VibrationEffect predefined effects and View haptic feedback over JNI. A build hook declares VIBRATE.
  * A rate limiter is followed by a pacer, so a reward haptic still plays right after a press tick.
  * In the editor the debug HUD shows each haptic ("editor - not felt").
* **The compile check covers more:** Input System stubs, so the tilt-sensor path is compiled and audited, and two editor stubs that the Project Setup change needed.
* **M1 plan** ([M1_PLAN.md](M1_PLAN.md)): three waves, track ownership and code contracts. The `kathmandu_core` sample region (11.3 MB) is committed so Explore works out of the box.

### Verification
`dotnet test core-tests` 174/174; `run.sh --audit` PASS (Android + iOS); `check_ui.py` PASS; localisation OK; TextSpike up to date. An adversarial review found 14 issues and rejected 3 more; all 14 were fixed. Nothing has run on a phone yet.

### Vision and content coverage
* **Product-owner vision restated:** *"a GTA style open world explore game in cartoonish theme ... every place, object, landmark, monument, scenery, winding road, waterfall, building, trekking route, forest and adventure spot in their actual places."* "GTA style" is read as the open-world format (seamless roaming, hop on and off vehicles, living streets, minimap, discovery, side activities), still non-violent at 9+ / PEGI 7.
* **Content coverage audit** → [CONTENT_COVERAGE.md](CONTENT_COVERAGE.md); evidence in [reports/content_census/](reports/content_census/README.md).
  * **What's already right:** positions. Nothing is lost between OSM and the packs: buildings to ≤ 1 cm and every road vertex kept, so hairpins survive.
  * **Gaps found:**
    * Rivers, cable cars and runways are in the data but never drawn.
    * About 1,250 point-only temples, shrines, hitis and monuments in the valley have nothing drawn.
    * Forests are only a tint.
    * About 15 k real street objects are dropped.
    * Route relations are dropped, so there are no named treks and no real bus lines.
    * Plinth heights are read as building heights, and `building:part` is dropped (Nyatapola 2 m, Dharahara a floating disc).
    * Some landmark anchors point at the wrong object (Boudha, Patan, Muktinath).
    * Rivers run uphill on the 30 m DSM.
    * Roads are draped on the DSM.
    * Places can't be discovered.
    * Some search results land off the map.
    * The far peaks (Everest from Nagarkot) are beyond the far clip.
  * **Gaps in OSM itself:** about 335 waterfalls tagged, 0 mani walls, and adventure spots barely mapped. These get fixed in OSM with the local community, or added from a curated, licence-clean DB, never invented.
* **Plan changes** (ROADMAP, M1_PLAN):
  * M1 grows by 1.16–1.23: water, forests and trees, POI-anchored structures, props, place presence, aerialways and airfields, named routes, and a coverage gate. That is about 11–12 weeks instead of 8.
  * A data track (D1–D14, format batches F1–F3) starts when Wave 1 lands.
  * Later milestones gain the content items listed in CONTENT_COVERAGE §3.4.
  * Errata applied to ARCHITECTURE, DATA_FORMATS and TAG_COVERAGE_FINDINGS.
* **Owner decisions O1–O15** (CONTENT_COVERAGE §6): defaults adopted so work proceeds; the product owner can override. Key ones:
  * "Every" means OSM plus curated, never invented.
  * Procedural decoration is generic only: never temples, shrines or named places.
  * Sacred-access rules apply.
  * Keep the 60 MB valley pack by dropping L10 heights.
  * Accept the longer M1.

### Next
* M1 Wave 1, "Explore works" (in progress).
* Then W2 with the data track: F1 classifier, landmark and search fixes first, then the new chunks.
* A native Nepali speaker should review the new menu and settings strings (listed in UI/README.md).

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
