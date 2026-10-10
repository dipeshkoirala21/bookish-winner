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

## 2026-10-10: W2 detail pass, gameplay integration (package integ-play)

### Done
* **Camera against the real world:** the chase camera's obstacle query is the open world's `TileGroundQuery`, typed and read every frame. On this branch it holds the streamed tiles' building footprints and the terrain; statue and pier colliders and bridge decks join it once the world integration wires them (see open issues). A Core test (`TheChaseBoomStopsShortOfThamelHousesAcrossRebasesAndAReopenedRegion`) sweeps the boom round a scooter on Thamel Marg over the committed sample: it never ends in or passes through a house, gives the same camera with the origin 7 km and 14 km away, and a closed then reopened region stops it again.
* **One shape per vehicle:** `Core/Driving/VehicleSeeds` is the single source of the fleet seed. A parked fleet vehicle, its borrowed copy and its plate share a model type, and the player's vehicle draws its own model type's wheels (`VehicleMeshCache.Wheel(in WheelSocket)`), as traffic does.
* **The player's face:** `Core/Characters/FaceMood`. Calm in sacred zones, joy on a first-time place (with a rate-limited "Discovered: …" toast; places are keyed by the named feature's position, so two temples with one name are two discoveries, and the place you spawn or teleport to is not one) and on arrival, a grin on a landing of at least 2.5 m/s (on foot too, the haptic's bar), a wince-laugh on hitting a wall (a calm face in temples). The poser is rebuilt when a recipe change rebuilds the skeleton. HitWall also plays a knock, a body bounce and a haptic (Medium on a vehicle, Light on foot), and the occluder fade targets the explorer's head.
* **Sadhus at Pashupati:** the pedestrian sim has a Pashupati row (sadhu weight 6 of 91) inside the `StreetStyle.Pashupati` circle, and nowhere else. Other rows draw the same random numbers as before, so determinism holds. The recipe's stopgap that dressed 12% of the men there as sadhus on top of the sim's is gone, so the drawn crowd has the researched share (198 of 3000, 6.6%; `TheDrawnCrowdAtPashupatiHasTheResearchedSadhuShare`).
* **Traffic spawns (this unblocks roads adopting the drawn surface):** routed buses share the moving-vehicle cap. The ambient spawner leaves the slots of the departures the bus runner still has to place (`TrafficSim.RoutedPending`), and a routed spawn is refused at `MaxVehicles` (37 against a cap of 30 before; never above 30 now, with 10 buses at Ratna Park). `TrafficSim.Room` also checks the lane's successors and predecessors, so nothing spawns 0.4 m behind a vehicle that just moved onto the next connector (the Chabahil contact). `SeveralBusesAreAlwaysNearThePlayerInTheCity` and `RingRoadRunsHalfAnHourWithoutContact` pass.
* **Player's vehicle wheels:** left wheels are the right wheel's mirror image (pivot scale −1 on X, same spin), as on a real vehicle, so hub faces, the outer tyre of dual rear wheels and the tractor rim's dish face outward on both sides, and the tractor's chevron lugs point the same way on both (a half turn about Y would reverse them). Previews: `/home/user/wt/previews/integ-play/wheels_*.png`.
* **HUD:** the horn button's hover name follows its label ("Bell" on a bicycle, EN and NE).
* **Car routes on the rebuilt GHRG car mask:** verified on the sample. A car route from Thamel Chowk to Boudha (7.8 km, about 8 min) never enters any of the 92 two-wheeler-only galli edges round Thamel; the scooter route (7.0 km) may use them.
* **HUD strings:** the route chip's stop button and the touch controls' tooltips are now localised (EN and NE, three keys appended).
* `game/README.md` has a new section, "The whole detail pass on a Mac", that walks through the detail pass in ten steps. Steps that need world layers not wired on this branch (bridge decks, roundabout statues and gates, statue colliders for the camera) are marked "(needs integ-world)"; `ReadmeMarksTheWorldLayersThatAreNotWiredYet` fails once they are wired until the marks come off, so the steps get re-checked then. Step 9 says how to get the hatchback (hop off, G twice); step 8 says a borrowed vehicle keeps its model type (parked ones have no plates).

### Open issues (not owned here)
* Vehicles: a `TrafficPresenter` hook to hide the bus the player rides in (needed for a real seat view), mirrored left wheels in `TrafficPresenter` (a mirror, as the driven view does now, not a half turn that reverses tractor chevrons), a one-sided-detail wheel mesh to halve wheel triangles, and an interior LOD to replace the stand-in cockpit.
* Tractor trolley (vehicles, collide, integration): no trolley is drawn. A tractor with its trolley needs a combined sim length of about 7.3 m (3.45 m today) and an articulated hitch in `Core/Traffic` and `Core/Driving`, in `TrafficPresenter`, and in `DrivenVehicleView` for the player's own tractor.
* Integration (world): build the bridge layer (`BridgeMesher`, `BridgeDeckIndex` as `TileGroundQuery.DeckFactory`) and the roundabout decorator in `TileBuild`, register generator cylinders and walls (statues, piers) in `WorldStreamer.RegisterColliders` (it copies only boxes and ramps), and set `CorridorFactory` and `HiddenBuildingRefs`, so the camera and vehicles collide with everything drawn. Then check README steps 2, 6 and 7 on a Mac and remove their "(needs integ-world)" marks (`ReadmeMarksTheWorldLayersThatAreNotWiredYet` asks for it).
* Characters: there is no wardrobe UI for the new recipe fields yet.
* Animals: pigeon feeding at Basantapur is not implemented.
* Already failing on main (be7eba6) without this package, 11 core tests owned by roads, bridges, buildings and streaming: RibbonsNeverCoverBuildingsOnTheSamplePack, EveryDrawnRoadIsAtLeastRideableOnEveryLeafTile, JunctionCapsShareTheirCutEdgesWithTheRibbons, NoRoadFloatsAboveTheTerrainOnTheSample, UrbanArterialsWithRoomKeepTheirFootpaths, SampleBridgesBuildWithRailingsBothSides, SampleRoadsUnderAndBesideBridgesStayClear, ThapathaliBridgeIsFoundAndBuilt, BandTotalsAtAsanFitTheBudgetTable, WholeSelectionsWithDetailFitTheTriangleAndMeshBudgets (646k against 630k on tier 0) and RidesThroughRealKathmandu. The twelfth, SeveralBusesAreAlwaysNearThePlayerInTheCity, is fixed here.

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
