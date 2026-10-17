# Reference brief: riding Thamel - Asan - New Road without stutter or pop-in (package `perf-roads`)

> Owner playtest #4 (docs/W2_PLAYTEST_1.md): "Buildings constantly flickers and stutters... Make them stable and
> solid." This package owns the streaming side of it: the main thread's work per frame while tiles stream in
> (`StreamingScheduler`, `TileBuild` upload chunks, the tile-shown handlers) and the road blocks that switch between
> the full road kit and the cheap far copy around the rider (`RoadBlocks`, `WorldStreamer.UpdateBands`). It models no
> new object; what it must get right is that the streets a local rides through look continuous and complete, and the
> frame never hitches when a new tile arrives. Road surfaces, widths and markings are the roads package's
> ([ref_roads.md](ref_roads.md)); houses are the buildings and perf-cells packages'.

## 1. The route (what is really there)

The ride harness (`core-tests/StreamingRideTests.cs`) rides a scooter at 50 km/h along the motorbike route of the
sample's routing graph through five places of the old core, 2 145 m in all:

| Waypoint | Lat, lon | What a local sees (2023-2026) | Sight line from a scooter |
|---|---|---|---|
| Thamel Chowk | 27.71434, 85.31138 | the tourist quarter's crossroads: 4-6 m of worn asphalt and KMC brick or stone re-paving between shopfronts, no footpaths, no paint (ref_roads.md §0) | 60-150 m down a lane between 4-6 storey houses |
| Asan Tole | 27.70755, 85.31252 | the six-way bazaar square: grey stone flags wall to wall (`#9A968E`), no kerbs, no paint, the Annapurna temple in the middle | 30-120 m: the lanes leave the square at angles (photos 01, 05 below) |
| Indra Chowk | 27.70602, 85.30961 | the next bazaar square west: the same stone flags, Akash Bhairav's temple front | 50-150 m |
| New Road, Peepal Bot | 27.70344, 85.31014 | Juddha Sadak, laid out after the 1934 earthquake as the city's first planned commercial avenue [S: Wikipedia, Nepali Times]; asphalt with a centre line, the heritage lantern lamps KMC put up in 2026 (ref_props.md) | the longest straight of the route: 400-500 m to the gate |
| New Road Gate | 27.70327, 85.31309 | the gate arch at the Tundikhel end, a footbridge beside it (bridges_flyovers.md) | across Kanti Path to Tundikhel |

What follows for the streaming:

* In the lanes of Thamel and Asan the rider sees at most 100-150 m of street. Every street within the B1 radius
  (120 / 200 / 250 m on Low / Mid / High) must be there with its full kit, or a local sees a bare patch of ground
  where the street they are riding into should be (the late tile of the first Unity playtest).
* On New Road the view runs 400-500 m straight ahead. Beyond the B1 radius the street must still be visible as a
  ribbon (the cheap copy, out to 250 m on Low and the B2 radius, 500 / 700 m, on Mid and High) and must never vanish
  or appear while the rider can see it.
* A detail tile of the old core (1 km, about 140 k building triangles) takes 3-4.5 s to build on this desktop and
  4 times that on a Low or Mid phone (ref_quality.md); at 50 km/h the rider covers 170-250 m meanwhile, so Low's
  detail radius had to grow from 350 to 450 m (section 3).

## 2. Budgets the ride is held to

W2_DESIGN 10.4: streaming upload 2 ms per frame on Low and Mid (30 fps), 1.5 ms on High (60 fps); a frame copies at
most 1 MiB of mesh data (`StreamingScheduler.UploadBudgetBytes`), shared with the B0 building cells
(`Tick(x, z, reservedBytes)`). The harness measures the CPU time of the main thread (not wall time, so a busy shared
machine does not count) and scales it to the tier's phones by 4 / 3 / 2 (ref_quality.md: Helio G85-G99 and Snapdragon
680 on Low, Snapdragon 7 Gen 1 on Mid, 7 Gen 3 on High). Each ride runs twice and a frame counts at the faster of its
two identical runs.

## 3. Before and after (sample pack `kathmandu_core`, this desktop, October 2026)

"Before" is the build the owner played (main 64155f3): one road layer and one decal layer per tile at the full kit,
upload chunks cut by vertex count only, the tick's first chunk always uploaded, the time budget counted from the first
upload, the fauna ground, the audio index, the static colliders and the paved-area index of a newly shown tile built on
the main thread, and Low's detail radius at 100 m. Each row names the test that reproduces it.

| Check (test) | Before (64155f3) | After |
|---|---|---|
| Main-thread work when a detail tile shows (the ride's handlers, worst per shown tile, desktop CPU ms; x 2-4 on phones) | 4.4-7.3 ms: static colliders 2.4-3.6, fauna ground 1.5-2.6, audio index 0.8-1.3, paving 0.1 | 0.25-0.34 ms left: sacred zones 0.2-0.3 (LifeHost's, one tile per tick), audio join 0.01, colliders link 0.05; the rest runs on workers (`PreparedStaticCollidersAnswerLikeTheRawSet`, `PerTileFaunaGroundAnswersLikeTheSharedIndex`, `AudioTilesIndexedOnAWorkerAnswerLikeTheMainThreadIndex`, `PavingIndexedOnTheWorkerLeavesTheMainThreadOnlyALookup`) |
| Largest upload chunk of a dense UV0 layer (`SplitLayerEndsChunksAtTheByteCap`, EditMode) | 32 768 vertices, about 1.4 MB | at most 1 MiB (`TileBuild.UploadChunkBytes`) |
| B0 cells first, then tiles, in one frame (`B0CellsAndTileChunksTogetherStayWithinTheFrameCap`) | up to 1.14 MB (the tick's first chunk always went) | at most 1 MiB, cells and tiles both finish |
| A tick whose own work (selection, collecting builds) had already spent the time budget (`TheTicksOwnWorkCountsAgainstTheUploadTime`) | still uploaded 21 chunks | its first chunk only; with 0.4 ms left 6 chunks, with the whole budget 18 |
| Low, every detail build 18 s (a slow phone): nearest street of the route drawn as bare ground (`TheOldLowDetailRadiiLetStreetsPopInAroundTheRider`) | 0 m, for 1 337 frames (100 m radius); 85 m with the 350 m this package first tried | none within the B1 radius on any tier (Low 450 m; `EvenSlowBuildsNeverLetARoadBlockPopInNearTheRider`) |
| Same ride: nearest road block appearing late | 0 m (100 m radius); 79 m (350 m) | 196 m Low, 242 m Mid, 656 m High, all outside B1 (120 / 200 / 250 m) |
| Road blocks vanishing within the far radius; blocks within B1 without their full kit | (no per-block roads: a tile's whole road layer came and went with the tile) | 0; 0 |
| Road and marking triangles around Asan / Chabahil chowk on Low (`RoadsDecalsAndBridgesInViewFitTheTierSliceAtAsanAndChabahil`) | one full-kit layer per detail tile, culled only per 1 km tile: 164 k / 176 k over the 4 detail tiles | per 128 m block, the cheap copy beyond B1 (out to 250 m on Low): 5.3 k / 8.3 k in the worst of 16 view directions (slice 8 k, with the band headroom) |
| Worst frame of streaming work on the tier's phones (`RideThroughThamelAsanAndNewRoadStaysWithinTheFrameBudget`) | (no ride harness) | within the budget, faster of two identical runs (0 unpaired frames): Low 1.2-1.5 ms, Mid 1.1-1.4 ms, High 0.7-0.9 ms (budget 2 / 2 / 1.5) |
| Most bytes in a frame / build objects created while riding | (no ride harness) | 1.00 MiB (cap 1 MiB) / 0 |

## 4. Previews (render beside photo)

`/home/user/wt/previews/perf-roads/ride2/`:

* `late_tile_before_after_top.png`: Low, slow builds, 280 m from Thamel Chowk; left the 350 m detail radius (the
  Asan tile's streets missing inside the B1 ring), right 450 m (every street drawn).
* `{low,mid,high}_ride_sheet.png`: the roads exactly as the game draws them at 5 points of the ride, chase camera
  14 m behind the rider: grey or brick-red the full kit within B1, **blue** the cheap far copy beyond (tinted for the
  check only; in the game it has the road's colours).
* `frames_chart.png`: streaming work and bytes per frame along the ride, per tier, against the budgets.
* `photo_vs_render_asan.png`, `photo_vs_render_newroad.png`: an Asan lane and New Road (photos below) beside the ride
  at the same kind of spot. The renders show roads and terrain only (houses are other packages'); what to compare is
  the street layout and surface around the rider and the sight line: grey stone flags without paint in Asan, a long
  straight asphalt avenue with a centre line on New Road whose far end stays drawn as a ribbon.

## 5. Photo credits (Openverse, reference only, never copied into the repo)

| Photo | Source | Licence |
|---|---|---|
| Asan Tole, Kathmandu (lane with stone flags), isapisa | flickr.com/photos/97189192@N00/30111400897 | CC BY-SA 2.0 |
| Asan Tole, Kathmandu (square), isapisa | flickr.com/photos/97189192@N00/31174356298 | CC BY-SA 2.0 |
| Market Street, Asan Tole, eriktorner | flickr.com/photos/39267804@N05/13255232605 | CC BY-NC-SA 2.0 |
| New Road, Kathmandu, Rick McCharles | flickr.com/photos/71035721@N00/4084871320 | CC BY 2.0 |

Sources: [New Road of Kathmandu](https://en.wikipedia.org/wiki/New_Road_of_Kathmandu);
[Rebuilding Kathmandu after the 1934 quake, Nepali Times](https://nepalitimes.com/rebuilding-kathmandu-after-the-1934-quake);
ref_roads.md, ref_props.md, ref_quality.md, bridges_flyovers.md and W2_DESIGN 10.4 of this repository.
