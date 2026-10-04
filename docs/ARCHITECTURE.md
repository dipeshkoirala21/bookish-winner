# Ghumante: architecture

> Status: **M0 draft, 2026-10-04.** This is a living document. Decisions are tagged with an ADR number (§13) so code comments can refer to them.
> Companion docs: [ROADMAP](ROADMAP.md) · [PROGRESS](PROGRESS.md) · [DATA_FORMATS](DATA_FORMATS.md) · [LICENSES](LICENSES.md) · [ASSET_MANIFEST](ASSET_MANIFEST.md) · [OSM tag coverage](reports/tag_coverage.md) ([findings](reports/TAG_COVERAGE_FINDINGS.md)) · [Landmark check](reports/landmarks.md)

---

## 1. Summary

Ghumante is a mobile open-world explorer of the real Nepal. Three parts make it up:

1. **An offline data pipeline** (`/pipeline`, Python). It turns OpenStreetMap, Copernicus GLO-30 elevation and ESA WorldCover land cover into deterministic, quadtree-tiled **region packs**, plus a **search index** and a **routing graph** per region. It runs on a dev machine or in CI, never on a phone.
2. **An engine-agnostic C# core** (`game/Assets/Ghumante/Core`, no `UnityEngine` references). It holds coordinates, data readers, search, routing, save schema and gameplay rules. This is the part we can unit-test anywhere with plain `dotnet test`.
3. **A Unity 6 LTS + URP game** (`/game`). It streams the region packs into a cartoon world around the player: terrain, roads, procedural buildings, scattered vegetation, water, traffic, wildlife. It also provides the vehicles, activities, the map and search, saves, and the UI from the reference image.

Everything rests on **canonical real-world coordinates**: the game world is derived from them by a scale model, so no persistent data ever depends on how we squash Nepal.

---

## 2. Where this brief needs pushback

The brief is strong. Twelve points need a change, a decision from you, or a caveat before we commit. The ones marked **Decision** need your answer (collected in §14).

| # | Topic | What the brief says | Concern | Recommendation |
|---|---|---|---|---|
| P1 | **World scale** | Evaluate 1:1 vs uniform compression | At 1:1, Kathmandu to Pokhara is about 200 km of road, roughly 2 h of real-time riding, and Lukla to EBC is about 65 km of trail. Uniform compression (1:4 to 1:10) breaks towns: real building footprints and 5 m roads cannot shrink, so Thamel stops fitting. | **Variable scale, "Euro Truck Simulator style"** (§5): towns stay at 1:1, open country is compressed about 1:6, and slopes and view angles are preserved. M0/M1 build the valley at 1:1 (identity warp). The warp is a separate pipeline stage, with a go/no-go gate before M2. **Decision**: target travel times. |
| P2 | Memory budget | "~1.5 GB on low-end (3–4 GB RAM)" | Android's low-memory killer and iOS jetsam end apps well before total RAM is used. 1.5 GB on a 3 GB phone is unsafe (§10). | Tiered budgets: **Low ≤ 1.0 GB, Mid ≤ 1.5 GB, High ≤ 2.0 GB** resident, with streaming radius and density scaled per tier. |
| P3 | Nepali text | "Full UI localization in English and Nepali from day one" | Devanagari needs OpenType shaping (conjuncts, reordered vowel signs, reph). Unity's TextMeshPro/uGUI does not shape Indic scripts. Shaping exists only in UI Toolkit's Advanced Text Generator (§7.9). | Build all UI in **UI Toolkit with the Advanced Text Generator**. A week-1 spike checks Devanagari on real devices. If the spike fails, fall back to a HarfBuzz-based shaping plugin. |
| P4 | OSM reality | Real surfaces, real buildings | The [coverage report](reports/tag_coverage.md) shows that **85% of road length has no `surface`**, **99.6% of 8.3 M buildings have no `building:levels`**, 97.5% are plain `building=yes`, `sac_scale` covers 2% of trails, and `name:ne` is on only 14–29% of villages and hamlets. | Treat **inference as the main path**: surface, levels, archetype, trail difficulty and Nepali names are all modelled, and every inferred value is flagged. "Accurate" becomes "true where OSM knows, plausible and consistent where it doesn't". The QA viewer shows which is which. |
| P5 | Install size and delivery | PAD + On-Demand Resources | On-Demand Resources is legacy on Apple's side, and Apple-hosted Background Assets need a newer iOS than our iOS 16 floor (§9). Most of our region data is our own binary format, not Unity assets. | **Region packs are plain files behind one `IRegionPackSource` interface**, with backends for Play Asset Delivery (Android), our CDN (iOS 16+, and the fallback everywhere), and Apple-hosted Background Assets later. The base install ships the Kathmandu Valley pack. |
| P6 | Licensing (ODbL) | Attribution in credits and on the map | Our tiles are a *Derivative Database* that we distribute inside the app. ODbL 4.4/4.6 then requires us to offer the derived database **or the method used to make it**. | Publish the pipeline (at least the transformation method) under an open licence, keep hand-curated content (fun facts, curated gems) in a separate database, and attribute on every map screen and in the credits. **Lawyer review needed** (see LICENSES.md). **Decision**. |
| P7 | Real business names | Real POIs, generic shopfronts | OSM `shop`/`amenity` names are real businesses, some with trademarks. Painting them on signs or listing them in search invites trademark and endorsement problems. | Shopfronts use generic Nepali-style signage. Search covers public places (temples, peaks, lakes, squares, transport hubs, viewpoints) and hotels and teahouses only where they are trek landmarks. |
| P8 | M3 scope | Annapurna + Everest, trekking, Lukla flight, helicopter, glaciers, yaks and horses, bungee | That is three milestones of work: two new biomes, two new movement systems, an aircraft system, and a mini-game. | Split it into **M3a** (Annapurna, trekking, stamina, horses) and **M3b** (Everest, the Lukla flight, helicopter, glaciers, bungee). See ROADMAP. |
| P9 | "Every milestone installable on a phone" | — | Unity cannot run in this cloud dev environment, and CI builds need a **Unity licence seat**, an **Apple Developer account** with signing assets, and a **Play Console** account. | The CI is wired up with GameCI and fastlane. You add the secrets listed in `docs/CI_SECRETS.md`. Until then, milestone builds come from your machine. **Decision**: Unity tier. |
| P10 | Cultural accuracy | Respectful depiction | Some sites have real access rules: non-Hindus may not enter Pashupatinath's main temple, and photography rules vary. | Model the real access rules (the inner sanctum is not enterable; the player watches aarti from the east bank) and add them to the cultural review list. |
| P11 | Kid-appropriate + analytics | Opt-in where required | A game that appeals to children brings COPPA, GDPR-K and the Play Families policy. Ad and analytics SDKs are the usual problem. | Collect no analytics by default, leave out ad SDKs, use a privacy-first crash reporter, and target a 9+ / PEGI 7 rating. |
| P12 | Engine | Unity 6 LTS default | Godot 4 has better text shaping and no licence cost, but Unity is stronger where this project is hardest: mobile open-world rendering, asset delivery, profiling and the asset ecosystem. | **Stay on Unity 6 LTS** (§3), with the engine-agnostic core kept portable. |

---

## 3. Engine decision (ADR-001)

**Decision: Unity 6 LTS with URP**, for the reasons below. The facts and their sources are in the [research appendix](#appendix-a-verified-platform-facts).

| Criterion | Unity 6 LTS | Godot 4.x | Weight |
|---|---|---|---|
| Large-world mobile rendering (instancing, GPU-driven, occlusion, LOD tooling) | Mature: SRP Batcher, BatchRendererGroup, GPU Resident Drawer, LOD Group, Burst-compiled jobs | Workable (MultiMesh, visibility ranges), but fewer shipped open-world mobile titles and no Burst equivalent: C++ GDExtension is needed for heavy procedural generation | High |
| Procedural generation throughput | Burst + Jobs + Collections on a worker pool, with near-native speed from C# | GDScript/C# are slower; C++ GDExtension adds a second toolchain | High |
| Delivery (PAD/ODR/CDN) | First-party Play Asset Delivery support; Addressables with remote catalogs | Community plugins only | Medium |
| Devanagari shaping | UI Toolkit Advanced Text Generator only; TextMeshPro cannot shape it | HarfBuzz text server built in | Medium. Mitigated by building the UI in UI Toolkit (P3) |
| Profiling and device tooling | Profiler, Memory Profiler, Frame Debugger, Adaptive Performance | Profiler, plus RenderDoc and Xcode/AGI externally | Medium |
| Cost and licence | Personal is free below the revenue/funding threshold; Pro seat above it | MIT, free | Medium. **Your decision on tier** |
| CI headless builds | Needs licence activation (GameCI), and macOS for iOS | No licence; runs anywhere | Low |
| Asset ecosystem (toon shaders, vehicles, animals) | Very large | Small | Medium |

**Mitigations that keep the door open:** the pure-C# core (§7.1) has no engine dependency. All data is in our own documented formats. Rendering code goes through a small set of interfaces (`ITerrainRenderer`, `IInstancedRenderer`, `IRoadMesher`). A port would replace the `Ghumante.World` and UI assemblies, not the game.

---

## 4. System overview

```mermaid
flowchart LR
  subgraph Offline["Offline pipeline (Python, CI)"]
    F[fetch.py<br/>OSM, DEM, WorldCover<br/>+ SOURCES.lock.json] --> X[OSM extract<br/>pyosmium]
    X --> I[Inference<br/>surface, levels, archetype,<br/>trail difficulty, names]
    F --> R[Rasters<br/>DEM mosaic, WorldCover]
    I --> T[Tiler<br/>quadtree, clip, encode]
    R --> T
    I --> S[Search index]
    I --> G[Routing graph]
    T --> P[Region pack .ghpk<br/>+ manifest]
    P --> Q[QA export → QA viewer]
  end
  subgraph Delivery
    P --> CDN[(CDN)] & PAD[(Play Asset Delivery)] & BASE[(Base install:<br/>Kathmandu Valley)]
    S --> CDN
    G --> CDN
  end
  subgraph Runtime["Game (Unity 6, C#)"]
    D[Ghumante.Platform<br/>IRegionPackSource] --> C[Ghumante.Core<br/>readers, search, routing, save]
    C --> W[Ghumante.World<br/>streaming, terrain, roads,<br/>buildings, scatter, water]
    C --> M[Ghumante.Map<br/>map, search UI, navigation]
    W --> V[Vehicles · Characters · Traffic · Wildlife · Activities]
    M --> UI[Ghumante.UI<br/>UI Toolkit]
  end
  CDN --> D
  PAD --> D
  BASE --> D
```

---

## 5. Coordinates, projection and world scale

### 5.1 Coordinate frames (ADR-002)

| Frame | Definition | Used for |
|---|---|---|
| Geographic | WGS84 lon/lat | Source data, map UI, debug teleports, fun facts |
| **Canonical (NPL-TM84)** | `+proj=tmerc +lat_0=0 +lon_0=84 +k=0.9996 +x_0=500000 +y_0=0 +ellps=WGS84`: a UTM-style Transverse Mercator on the 84°E meridian, which is exactly the boundary between UTM zones 44N and 45N | All persistent positions (saves, discoveries, pins, search entries). True metres. |
| **Game** | `warp(canonical) − (100 000, 2 900 000)`; X east, Z north, Y up | Everything in the scene |

* Scale error of NPL-TM84 over Nepal: **0.9996 on 84°E to 1.0018 at the eastern border** (measured with PROJ in `projection.scale_factor_at`). That is at most 1.8 m per km, which is invisible in play.
* After the origin shift, all of Nepal has positive game coordinates: X ≈ 18–818 km, Z ≈ 16–470 km (identity warp). That fits inside the 2^20 m quadtree root.

### 5.2 Floating origin (ADR-003)

`float32` has about 6 cm resolution at 800 km, which causes visible jitter. The runtime therefore uses:

* `WorldPos` (`double` X/Z, `float` Y) in the core for every persistent and simulation position.
* A **floating origin**: the scene is rebased when the player is more than 2 km from the current origin. All root transforms, particle systems, the physics world and the streaming caches are shifted together in one frame, and listeners (`IOriginShiftListener`) get the delta. Physics runs in local floats near the origin.
* Tile content is built relative to the tile origin and placed at `tileOrigin − floatingOrigin`, so meshes are never rebuilt when the origin moves.

### 5.3 World scale (ADR-004)

Some numbers to frame the decision (real-world figures are in Appendix A):

| Journey | Real distance | Time at 1:1 (arcade speeds) | Time with recommended model |
|---|---|---|---|
| Thamel → Boudhanath | ~6 km by road | ~5 min by motorbike | same (urban, 1:1) |
| Kathmandu → Pokhara (Prithvi Hwy) | ~200 km | ~2 h | **~15–20 min** |
| Lukla → Everest Base Camp | ~65 km trail | ~6–8 h walking | **~60–80 min** in stages |
| Kathmandu → Sauraha (Chitwan) | ~160 km | ~1.5 h | ~15 min |

**Options considered**

1. **1:1 everywhere.** This is the simplest and the most faithful, and the map is exact. But long journeys are tedious, and the game would lean almost entirely on fast travel. That works against the vision of going anywhere, any way. It also gives the largest world to fill and the biggest download.
2. **Uniform compression (1:4–1:10).** Rejected. Real footprints, 5 m roads and human-sized props cannot shrink, so towns become unreadable and roads overlap.
3. **Variable scale (recommended).** Settlement cores stay at 1:1, peri-urban areas and valley floors run at about 1:2, and open country at **about 1:6** (tunable per region between 1:4 and 1:8). Smooth transitions connect them. This is how Euro Truck Simulator 2 keeps cities recognisable while making country-length drives fun.

**How the variable scale works (pipeline stage `warp.py`, M2)**

* **Scale field.** `s(x)` ∈ [1/8, 1] is computed on a 500 m canonical grid from settlement density (OSM buildings plus the WorldCover built-up class), then heavily smoothed. Gradients are limited so no building is noticeably sheared.
* **Horizontal warp.** We solve for an *as-similar-as-possible* map φ, in which each grid cell becomes a scaled and rotated copy of itself with local scale `s(x)`. This is a sparse linear least-squares problem, solved once for all of Nepal (about 0.5 M unknowns at 1 km resolution). It is stored as a displacement grid plus its inverse, both shipped to the device so the runtime can convert lon/lat ↔ game.
* **Vertical model.** Elevation is split as `z = B + r`: `B` is a heavily low-passed base (σ ≈ 15 km) and `r` is local relief. Game height is `z' = k_b·B + s(x)·e(z)·r`. Here `k_b` is the base compression (about 1/6), so the valley floor (~1 350 m) and the Terai (~100 m) keep their relative order without fake tilts. `s(x)` makes slopes in compressed areas a *similarity*, so **slopes and the angular size of distant peaks are preserved**: the Himalaya look exactly as tall from the hills as in reality. `e(z)` is an optional "drama" exaggeration of 1.0–1.4 above 3 000 m. Everest stands about 800 m above Base Camp in game, which is enormous next to a 1.7 m character.
* **Road generalisation.** Compression would turn hairpin stacks into knots. A cartographic generalisation pass simplifies them, enforces a minimum turning radius (12 m), cuts the number of bends in a stack, checks gradients, and carves the terrain corridor. This is the single biggest risk of the scale model, so it is a **go/no-go gate before M2** (see ROADMAP).
* **Settlement typification.** Dispersed hill-village houses in compressed zones are thinned and re-placed (keeping one house in *k*), using the same building data.
* **Fallback** if the gate fails: stay 1:1, and add *journey mode* (auto-drive along the real route with points of interest to stop at) plus generous fast travel.

Saves, discoveries and the search index store canonical coordinates, so changing the scale model never invalidates player progress (ADR-002).

---

## 6. Offline data pipeline

### 6.1 Stages

| Stage | Module | Input → output | Notes |
|---|---|---|---|
| Fetch | `fetch.py` | Mirrors → `data/raw/**`, `SOURCES.lock.json` | Geofabrik first, then the OSMToday mirror. MD5 checked for OSM; SHA-256 recorded for everything; resumable. |
| Coverage | `coverage.py` | PBF → `docs/reports/tag_coverage.{md,json}` | Run nightly; CI warns when coverage drops. |
| Landmarks | `landmarks.py` | PBF + `config/landmarks.yaml` → `landmarks.resolved.json` | Hero placement source of truth. |
| Extract | `osm_extract.py` | PBF + region bbox → `Extract` (`model.py`) | pyosmium with node locations; multipolygons via the area assembler; clipped to bbox + buffer. |
| Tag parsing | `tags.py` | raw tags → typed fields | Robust to Nepali data quirks: `G+2` levels, `5m`, `5 m`, `bitumin`, `Blacktopped`, Hebrew `בלתי_סלול` and so on. |
| Surface inference | `surface.py` | Roads → surface + source | Empirical model trained on the 15% of roads that are tagged (§6.2). |
| Building inference | `buildings.py` | Buildings → levels, height, roof, materials, archetype | Rules from region, density, use and elevation. Configurable. |
| Trail difficulty | `trails.py` | Paths + DEM → `sac_scale` (inferred where missing) | Slope, altitude, glacier proximity. |
| Rasters | `dem.py`, `landcover.py` | COG tiles → seamless samplers in game space | Bilinear for 30 m+ spacing; pre-filtered (area-averaged) for coarse LODs. |
| Biomes | `biomes.py` | WorldCover + OSM land use + elevation + slope + region → `Biome` | Terraces = cropland on slopes of 8–35° in the middle hills. |
| Tiling | `tiling.py` | Everything → `GHT1` tiles | Clipping with context points; buildings by centroid; areas triangulated. |
| Packing | `pack.py` | Tiles → `.ghpk` + manifest | Deterministic, CRC per tile. |
| Search | `translit.py`, `search_index.py` | Places, POIs, landmarks → `.ghsi` | Latin, Devanagari, romanised Nepali, fuzzy. |
| Routing | `routing.py` | Roads and trails → `.ghrg` | Per-profile access and speeds. |
| QA export | `qa_export.py` | Pack → GeoJSON + PNGs | Decodes the pack, so it tests the whole chain. |

`build.py --region kathmandu_valley` runs every stage. Each stage caches its output under `build/cache/<region>/<stage>-<hash>.pkl.gz`. The hash covers stage inputs, config and code version, so editing the biome rules does not re-read the 468 MB PBF.

### 6.2 Inference models (ADR-005)

Coverage numbers come from the [tag coverage report](reports/tag_coverage.md).

* **Road surface** (85% of length untagged). This is an empirical conditional distribution P(surface group | highway class, settlement-density bin, elevation bin, terrain-slope bin), fitted on the tagged roads of the same build with Laplace smoothing. It backs off to coarser conditioning when counts are thin, and finally to hand priors (trunk/primary → asphalt; track → dirt). `tracktype` and `smoothness` are used first when present (`DERIVED`). The model picks the arg-max when its probability is ≥ 0.6. Otherwise it samples deterministically, using a hash of the *road chain* (the same name+ref, or connected ways of the same class), so one road does not change surface every 100 m. Tagged vs inferred percentages go into the manifest and the coverage report.
* **Building levels and height** (99.6% unknown). Levels are drawn from a distribution chosen by archetype and urban density: Kathmandu core 3–5 levels, peri-urban 2–4, hill villages 1–2, Terai 1–2. The deterministic per-building seed makes the result stable.
* **Archetype.** In order: religious tags first; then hand-drawn polygons (`config/archetype_zones.yaml`: Newar old towns, Sherpa Khumbu, the trans-Himalaya); then elevation and region rules; then density.
* **Trail difficulty** (`sac_scale` on 2% of trails). Inferred from slope percentile along the path, maximum altitude, and glacier or moraine proximity.
* **Nepali names** (missing on most villages). The order is `name:ne`, then `name` when it is Devanagari, then *no* machine transliteration in the UI (we show the Latin name). We do not show machine-made Devanagari to Nepali readers. The romaniser is used only for search keys.

### 6.3 Terrain and road conformance

DEM heights are shipped at 8 m spacing on level-10 tiles (129² samples per 1 024 m). The runtime builds 2 m near-field terrain chunks by bicubic upsampling and conforms them to roads: it flattens the road corridor along the road's smoothed elevation profile with a falloff, and adds embankments and cuts. This happens in Burst jobs, so the pack stays small and roads always sit on the terrain.

### 6.4 Determinism and reproducibility

* For the same inputs (pinned by `SOURCES.lock.json`), config and code, the pipeline produces byte-identical packs. A CI test builds the fixture region twice and compares SHA-256 sums.
* `data_version` is written into every tile, pack and manifest. The runtime refuses to mix packs with different `data_version` values.
* The nightly job re-fetches data, rebuilds every region, publishes the coverage report and a diff, and uploads packs to the staging CDN. Production promotion is a manual step.

### 6.5 QA viewer

`tools/qa-viewer/` is a static Leaflet page. It shows OpenStreetMap tiles underneath, and the decoded pack layers (roads coloured by surface and by `surface_source`, buildings by archetype, areas, POIs, hillshade and biome PNGs) on top, plus a tile-grid overlay and click-to-inspect attributes. It is developer-only. It respects the OSM tile usage policy (low volume, attribution shown), and it can switch to a self-hosted basemap.

---

## 7. Runtime architecture (Unity)

### 7.1 Assemblies

| Assembly | Engine refs | Responsibility |
|---|---|---|
| `Ghumante.Core` | **none** (`noEngineReferences`) | `WorldPos`, NPL-TM84 projection, tile keys, `GHT1`/`GHPK`/`GHSI`/`GHRG` readers, search, routing (A*), travel profiles, save schema and migrations, localisation keys, service interfaces (`IAnalytics`, `ICrashReporter`, `ICloudSave`, `IQuestService`, `IDialogueService`), event bus |
| `Ghumante.World` | Unity, Burst, Collections, Mathematics | Streaming, floating origin, terrain, roads, buildings, scatter, water, sky, weather |
| `Ghumante.Vehicles` | Unity | Arcade vehicle controller, surface grip table, vehicle definitions (ScriptableObjects) |
| `Ghumante.Characters` | Unity | Player controller (walk, run, climb, stamina), NPCs, crowds |
| `Ghumante.Traffic` | Unity | Lane graph built from `ROAD`, traffic agents, cows, horn logic |
| `Ghumante.Wildlife` | Unity | Biome spawn tables, behaviour state machines, photo targets |
| `Ghumante.Activities` | Unity | `IActivity` framework, one sub-folder per activity |
| `Ghumante.Map` | Unity | Vector map renderer, fog of discovery, search UI, route display, minimap |
| `Ghumante.UI` | Unity (UI Toolkit) | Menus and HUD in the reference style, localisation binding |
| `Ghumante.Save` | Unity (I/O only) | Local save storage, cloud-save adapters |
| `Ghumante.Audio` | Unity | Adaptive music layers, biome ambience |
| `Ghumante.Platform` | Unity | Region delivery (`IRegionPackSource`: PAD, CDN, built-in), Game Center / Play Games, device tier detection |
| `Ghumante.DebugTools` | Unity | Teleport, overlays, OSM tag inspector, FPS/memory HUD, weather/time override (development builds only) |
| `Ghumante.App` | all | Bootstrap, scene flow, dependency wiring |

Dependencies only point downward toward `Core`. Gameplay assemblies talk to each other through `Core` interfaces and the event bus, never directly.

### 7.2 World streaming

* **LOD rings** sit around the camera's position in tile space, with radii in metres per tier:

  | Ring | Tile level | Content | Low | Mid | High |
  |---|---|---|---|---|---|
  | Near | 10 (1 km) | full: terrain 2 m, roads, buildings kit LOD0–1, scatter, traffic | 1 km | 1.5 km | 2 km |
  | Mid | 9 (2 km) | terrain 8 m, roads simplified, buildings as merged extrusions, impostor trees | 3 km | 5 km | 7 km |
  | Far | 8 (4 km) | terrain 32 m, major roads, building blocks | 8 km | 12 km | 16 km |
  | Horizon | 5–7 | terrain only, skyline impostor ring beyond | to 60 km | to 100 km | to 150 km |

* **Pipeline:** `TileRequest → IO thread (read pack range) → decode job (Core readers) → mesh-build jobs (Burst) → main-thread upload (budgeted)`. Main-thread work is capped at **2 ms per frame** on Mid, spread over a queue ordered by distance and view direction.
* **Caches:** decoded tiles go in an LRU sized per tier, and meshes are pooled and reused.
* **Determinism:** all procedural variation comes from `SEED` and per-building `seed`, so the world is identical on every visit and on every device.

### 7.3 Terrain

Terrain uses chunked LOD in the CDLOD style. Each tile builds a regular grid mesh, and vertices **morph** toward the parent LOD in the transition band, so there is no popping and no cracks. Skirts cover any remaining T-junction gaps. Shading is a toon ramp: biome colours come from a palette texture indexed by `BIOM`, with triplanar noise for detail and slope-based rock blending.

### 7.4 Roads

Road meshes are ribbons generated from `ROAD` polylines. Width comes from the tag or a class default, and the cross-section from surface and class. Junctions are found from shared endpoints and given simple polygon caps. Bridges get deck meshes. Surface sets the material (asphalt, brick and so on), the physics material (§7.6), the decals (potholes, puddles in monsoon) and the particles (dust, mud). Trails are narrow ribbons with stone steps on `steps`.

### 7.5 Buildings

* **Archetype grammars.** Each archetype in `BuildingArchetype` is a ScriptableObject: a facade grammar (ground floor, upper floors, trims), a roof generator per `RoofShape`, a palette, and modular kit pieces (walls, windows, doors, trims), with the toon material per archetype. A footprint is split into facades; facades are filled from the kit, chosen by seed, levels and use.
* **LOD.** LOD0 is the kit, built in the near ring and GPU-instanced per piece. LOD1 is one merged extruded mesh per tile, with roof shapes. LOD2 is per-block merged boxes with the facade texture atlas.
* **Landmarks.** OSM buildings flagged `LANDMARK` are hidden, and the hand-made hero prefab is placed at the resolved OSM location (`landmarks.resolved.json`) and orientation.

### 7.6 Vehicles and physics

The vehicle model is an arcade raycast-wheel controller. Grip, top speed, drag and camera shake come from `SurfaceGroup` × weather: the monsoon makes `DIRT` act like `MUD`. Stuck vehicles recover automatically after 3 seconds (a bounce and a nudge), so mud is never a soft-lock. Vehicle access follows the travel profile masks (`Travel`). A bus physically cannot enter a footpath: the road is too narrow, and a friendly "the bus can't go there" prompt appears.

### 7.7 Traffic, pedestrians, animals

* **Traffic.** A lane graph is built per tile from `ROAD`. Density comes from road class and time of day, scaled by tier. Agents give way to cows: a cow is a static obstacle with a soft avoidance radius, and vehicles never collide with animals (there is a damage-free bump plus a stop).
* **Pedestrians.** Crowds use GPU-skinned or vertex-animation-texture instancing in dense areas. Archetypes include porters with doko baskets, monks, sadhus and kids flying kites (Dashain).
* **Wildlife.** Spawn tables are keyed by `Biome` and region (rhino only in Chitwan and Bardiya). Behaviour is a state machine (wander, graze, flee, flock, perch). Interaction is observation only: the photo system scores framing and distance.

### 7.8 Map, search and navigation

* **Map.** The map renders vectors from `ROAD`/`AREA`/`LINE`/`POIS` at the zooms that need detail. For country and province zoom it uses a precomputed generalised map layer (M2). The style is cartoon: thick outlines and a paper texture. Undiscovered areas use a sketch style through a fog-of-discovery mask texture, which is updated as the player explores and saved as a compressed bitmap.
* **Search.** The C# search port matches the Python reference (`search_index.py`). Golden tests run the same query list in both languages and compare the ranked results.
* **Routing.** A* over `GHRG` per travel profile gives an ETA per mode. *Go there yourself* draws a route ribbon in the world and on the minimap, with turn hints from the edge geometry. *Fast travel* is allowed to discovered places and transport hubs, with a cartoon transition.

### 7.9 UI and localisation

* **UI Toolkit for every screen.** The reason is Devanagari shaping (P3): UI Toolkit's Advanced Text Generator uses HarfBuzz and ICU. uGUI and TextMeshPro are not used for text.
* **Style.** The reference style is built as USS: cream panels, ribbon headers, glossy pill buttons with a darker bottom edge, round icon buttons, and counter pills with a "+".
* **Fonts.** Baloo 2 for display (Latin + Devanagari) and Mukta for body text. Both are SIL OFL 1.1. See LICENSES.md.
* **Localisation.** The Unity Localization package provides EN and NE string tables. Nepali numerals (०-९) are a per-locale option. Every string has a key, and CI fails on missing keys in either table.

### 7.10 Save system

* Local JSON saves, compressed, with `schemaVersion` and ordered migrations (`ISaveMigration`). Writes are atomic: write to a temp file, then rename. Two rotating backups are kept.
* Sections: `player` (canonical position, vehicle), `discovery` (fog bitmaps per region, found POIs, passport stamps), `collections`, `progress` (explorer level, coins, stars), `settings`, `story` (reserved, empty), `quests` (reserved, empty).
* `ICloudSave` gets Game Center and Play Games Services implementations later. Conflicts resolve by merging collections and taking the maximum of progress.

### 7.11 Story Mode hooks

`IQuestService`, `IDialogueService`, `IWorldStateFlags`, `INpcRegistry`, and save sections `story` and `quests` exist in M1, each with a no-op implementation. Activities and discoveries publish events (`DiscoveryMade`, `ActivityCompleted`, `PlaceEntered`) that a quest system can subscribe to later.

### 7.12 Analytics, crash reporting, privacy

`IAnalytics` and `ICrashReporter` are interfaces. The default `NullAnalytics` collects nothing. Analytics is enabled only after explicit opt-in, and never for players who declare an age under 13 (or 16 in the EU). There are no ad SDKs. Choosing a crash reporter is left to M1 (candidates: Unity Cloud Diagnostics, Sentry, Crashlytics). It must be configurable to collect no personal data.

### 7.13 Debug tools

Debug tools ship in development builds only. They include: teleport to coordinates, a place name or a tile; a tile and LOD overlay; a "nearest OSM feature" inspector that shows the original tags and the inferred fields with their sources; an FPS, frame-time and memory HUD; time-of-day and weather override; streaming statistics; and a fast-forward travel mode.

---

## 8. Rendering style

* **Toon lighting.** A URP custom lit shader (Shader Graph plus a custom function) with a ramp texture, rim light and optional inverted-hull outlines (characters, vehicles and landmarks only, for cost reasons).
* **Atmosphere.** The sky gradient and stylised height fog make distant ranges fade into a soft blue. Snow peaks keep a warm rim at sunrise and sunset; this is the hero moment.
* **Skyline.** Beyond the horizon tiles, a 360° skyline impostor ring is precomputed per region from the coarse DEM, so the Himalaya are always on the horizon.
* **Animation.** Vertex-shader wind (trees, prayer flags), squash-and-stretch on characters and vehicles, and bouncy UI tweens.
* **Weather.** Clear, valley fog, monsoon rain with wet-surface shader parameters and puddles, and snow at altitude. A wetness value drives road physics.

---

## 9. Delivery and install size

* **Base install:** the app, shared art kits, and the `kathmandu_valley` pack (target ≤ 60 MB for the pack). The whole base download must stay **under the Google Play base-module and iOS cellular limits** (Appendix A).
* **Region packs** are plain binary files plus per-region Addressables bundles for hero assets. One `IRegionPackSource` interface has these backends:
  * `BuiltInSource`: StreamingAssets, for the base region.
  * `PlayAssetDeliverySource`: Android on-demand asset packs, one per region (within PAD per-pack and total limits).
  * `CdnSource`: HTTPS download with resume, SHA-256 verification against the manifest, storage in the app's persistent data path (excluded from iCloud backup). This is used on iOS 16+, and it is the fallback everywhere.
  * `AppleBackgroundAssetsSource`: Apple-hosted asset packs, once the iOS minimum allows (see Appendix A).
* Downloaded regions **work fully offline**. Manifests and packs are validated on launch, and a corrupt pack is re-downloaded.
* Store policy: region packs are data, not executable code, so downloading them is permitted under App Store Review Guideline 2.5.2 and Google Play's policies (Appendix A).

---

## 10. Performance and memory budgets

Device tiers are detected at first launch from RAM, GPU family and a 3-second benchmark scene. The player can override the tier. Adaptive Performance adjusts it at runtime when the device runs hot.

| Budget | Low (3–4 GB Android, e.g. Helio G85/G99) | Mid (iPhone 12, Snapdragon 7-series) | High |
|---|---|---|---|
| Target frame rate | 30 fps (33.3 ms) | 60 fps (16.6 ms) | 60 fps |
| CPU main thread | ≤ 20 ms | ≤ 10 ms | ≤ 10 ms |
| Draw calls (SRP batches) | ≤ 250 | ≤ 500 | ≤ 800 |
| Triangles on screen | ≤ 300 k | ≤ 800 k | ≤ 1.5 M |
| Resident memory (total) | **≤ 1.0 GB** | ≤ 1.5 GB | ≤ 2.0 GB |
| — textures | 250 MB | 450 MB | 700 MB |
| — meshes | 120 MB | 220 MB | 350 MB |
| — tile data cache | 40 MB | 80 MB | 120 MB |
| — managed heap | 120 MB | 160 MB | 200 MB |
| — audio | 30 MB | 50 MB | 60 MB |
| Near ring radius | 1.0 km | 1.5 km | 2.0 km |
| NPCs / vehicles nearby | 30 / 15 | 80 / 35 | 140 / 60 |
| Shadows | blob + 1 cascade, 512 | 2 cascades, 1024 | 2 cascades, 2048 |
| Outlines | landmarks only | characters, vehicles, landmarks | + props |

Texture compression is ASTC everywhere (6×6 for terrain and props, 4×4 for UI). Graphics APIs are Vulkan with a GLES3 fallback on Android, and Metal on iOS.

---

## 11. Build, CI/CD and environments

| Workflow | Trigger | What it does |
|---|---|---|
| `pipeline.yml` | push, PR | Lint, run `pytest`, build the `thamel_test` fixture region twice, and check determinism. |
| `core.yml` | push, PR | `dotnet test` for `Ghumante.Core`, including golden files written by the pipeline. |
| `unity-build.yml` | push to main, manual | GameCI: EditMode tests, Android AAB (IL2CPP, ARM64), iOS Xcode project, then macOS + fastlane → TestFlight. Needs secrets (`docs/CI_SECRETS.md`). |
| `nightly-data.yml` | cron 02:15 UTC | Fetch, coverage report, landmark check, build all active regions, upload artifacts and the staging CDN, and post a diff summary. |

`ProjectSettings` are applied by an editor script (`Ghumante.EditorTools.ProjectSetup`), not edited by hand. That covers the bundle id, API levels, IL2CPP, ARM64, graphics APIs and the URP asset. The script is idempotent, so CI and every developer get identical settings.

---

## 12. Repository layout

```
/docs                 architecture, roadmap, progress, formats, licences, reports
/pipeline             Python data pipeline (pyproject.toml)
  /config             regions, sources, landmarks, archetype zones, inference priors
  /ghumante_pipeline  package
  /tests              pytest (unit, fixture end-to-end, real-data marked)
  fetch.py build.py   CLI entry points
/shared               cross-language contracts (enums.json, golden files)
/core-tests           dotnet test project compiling Ghumante.Core sources outside Unity
/game                 Unity 6 project
  /Assets/Ghumante/{Core,World,Vehicles,...}
/tools/qa-viewer      static web QA viewer
/.github/workflows    CI
```

---

## 13. Decision log (ADRs)

| ADR | Decision | Status |
|---|---|---|
| 001 | Unity 6 LTS + URP, engine-agnostic C# core | Accepted (§3) |
| 002 | Canonical CRS NPL-TM84; persistent data in canonical/lon-lat only | Accepted |
| 003 | Floating origin, rebase at 2 km; doubles in core | Accepted |
| 004 | Variable-scale world (towns 1:1, country ~1:6) via an ASAP warp; identity in M0/M1; go/no-go before M2 | **Proposed: needs your sign-off on target travel times** |
| 005 | Inference-first data model with per-field provenance flags | Accepted |
| 006 | Own binary formats (GHT1/GHPK/GHSI/GHRG), DEFLATE, deterministic builds | Accepted |
| 007 | UI Toolkit for all UI (Devanagari shaping); TMP not used for text | Accepted, pending the device spike |
| 008 | Region packs via `IRegionPackSource` (PAD / CDN / built-in / Apple-hosted later) | Accepted |
| 009 | No analytics by default; no ad SDKs | Proposed |
| 010 | OSM shop/brand names not rendered in-world, not searchable | Proposed |
| 011 | Quadtree root 2^20 m, leaf level 10 (1 024 m), heights 129² at 15 cm quantisation | Accepted |
| 012 | Overpass/Geofabrik independence: mirrors list in `sources.yaml` | Accepted |

---

## 14. Open questions for you

1. **Travel-time targets** (ADR-004): is "Kathmandu → Pokhara in about 15–20 minutes of riding" the right feel? Shorter means more compression and more road generalisation.
2. **Unity licence tier and accounts**: Personal or Pro? Who owns the Apple Developer and Play Console accounts that CI will sign with?
3. **ODbL approach** (P6): are you OK publishing the pipeline source (or at least the transformation method) and offering the derived database? Do you have a lawyer for the review?
4. **Economy mapping**: Coins (rentals, tickets, cosmetics), Stars (discoveries and medals; unlock badges and regions?), Hearts/Energy as in-world stamina only. Should Stars gate regions at all, or only badges?
5. **Monetisation direction** (later): cosmetics and/or region packs? This affects whether region downloads are free.
6. **Art direction**: should landmark outlines be on everywhere or only on hero objects? Is the palette tuned toward the reference image or something more pastel?
7. **Cultural consultant**: do you have someone in mind? We need them before M1 hero landmarks are final.

---

## Appendix A: verified platform facts

*Filled from the research pass of 2026-10-04 (sources inline). See §15 of PROGRESS.md for anything still unverified.*

(see below)
