# Reference brief: the near building band as a rider sees it in the old core (detail pass, package "perf-cells")

> Status: written 2026-10-17 for the playtest-fix round (docs/W2_PLAYTEST_1.md #4: "Buildings constantly flickers and
> stutters... Make then stable and solid"). This package draws no new object: it decides **which** houses the B0 band
> (`World/Buildings/B0CellSet.cs`, `DetailCells.cs`) draws, at which level and when, so the brief is about what a
> rider sees of the houses along real streets, and what must therefore never change while riding or standing.
>
> Photos: Openverse (Flickr, open licences), the buildings package's set under `/home/user/wt/refs/buildings/asan/`
> and `/home/user/wt/refs/buildings/thamel/` (an Openverse search for Thahiti and Asan failed on 2026-10-17, so these
> were reused). **Reference only: nothing from them is copied into the repository.** Credits in §5. Labels: **[photo]**
> seen in the cited photographs, **[src]** a cited source, **[est]** an estimate, **[measured]** from the core tests on
> the `kathmandu_core` sample.

## 1. The streets of the playtest

| Street | What stands there now [photo] | What the rider sees | B0 consequence |
|---|---|---|---|
| **Asan Tole and its lanes** (Thahity Jyatha Marg, Chittadhar Marg, the Indra Chowk road) | Unbroken walls of 4-6 storey houses on both sides, Newar brick with carved windows among plastered RCC fronts; shop shutters and hanging goods on the ground floor, signboards and cables above; lanes 4-8 m wide [asan ref_00, ref_02, ref_05, ref_06] | A canyon: the walls fill the view to the next bend, 30-60 m ahead; nothing beyond is visible except the sky strip [photo; est] | Every house in view is a B0 house (35 / 60 / 80 m radius on Low / Mid / High). Any house switching to a B1 box, or a block of them, is seen at once, a few metres from the rider. |
| **Thamel to Asan** (Dabali Marga, Amrit Marg, Thabahi Sadak, Narsingh Chowk) | 4-6 storey houses and hotels with shop fronts, stacks of signboards, awnings and balconies on both sides [thamel ref_00, ref_02]; the large hotel footprints of Dabali Marga [measured: the heaviest Low frame of the ride] | Sightlines of 50-120 m along the straighter streets [thamel ref_02]; the B0/B1 hand-over at the band radius is in view | The 4 m dithered cross-fade at the band radius is the only transition allowed, and it must stay put while the chase camera sways (the band follows the rider). |
| **Kantipath and Durbar Marg** (Lainchaur to Maitighar by car at 50 km/h) | Wide divided roads, 4-10 storey commercial blocks, long straight views [src: route on the sample graph, 3.6 km] | Houses enter the band quickly at speed, and on the outside of every turn | Builds must be on screen before the house reaches the band: a lead along the velocity, and finished builds shown the next frame. |

## 2. What "stable and solid" means here (rules for the planner)

1. **The band centre is the rider, not the camera** (`BandAnchor`): a chase camera swinging round a stopped rider moved
   every band edge across the houses in front of it (3 m sideways at 4.5 m behind the rider); the centre now trails the
   rider by at most 1 m and does not move at all while the rider stands [measured].
2. **Each house once, at one level**, in one 32 m cell: no near and lite rings drawn over each other (the old rings drew
   the house at the near radius twice, dithered together) [measured: doubles in every frame before].
3. **A B1 box never replaces a house B0 should draw** while riding: the B1 block stands in only for a house B0 truly
   lacks (first frames on a tile, a teleport), never because some other cell of the block is rebuilding.
4. **A rebuilt cell keeps drawing its old version** until every chunk of the new one is on the GPU, and the two swap in
   one frame; a level changes once on the way in and once on the way out (4 m hysteresis), never back and forth.
5. **Colliders are what is drawn**: a shown cell registers exactly its version's set, a hidden one none.
6. **The budget holds while riding**, without lowering a level near the rider: levels are raised only while the raise
   leaves 10 k triangles of the tier's B0 budget free for the houses riding brings in (`B0CellSet.RaiseCapTris`).

## 3. Measured before and after (core-tests `MeshingBuildingB0StabilityTests`, 30 fps, worker cost model)

Before: the old ring cells as the owner played them (main at 5a7668a, emulated with the same cost model). After: the
B0 cells of this package, centred by `BandAnchor`.

| Ride | Tier | Before: gap frames / double frames / block flips / quick re-shows | After |
|---|---|---|---|
| Thamel Chowk to Asan, scooter at 10 m/s, 3 stops, swaying chase camera (3,821 frames) | Low | 593 / 3,692 / 220 / 336 | 0 / 0 / 0 / 0 |
| | Mid | 958 / 3,796 / 266 / 593 | 0 / 0 / 0 / 0 |
| | High | 1,093 / 3,819 / 314 / 763 | 0 / 0 / 0 / 0 |
| Lainchaur to Maitighar by car at 50 km/h (8,185 frames) | Low | 2,049 / 5,009 / 754 / 1,662 | 0 / 0 / 0 / 0 (8 block flips on the turns before the build harvest, see §4) |
| | Mid | 2,640 / 6,759 / 934 / 2,307 | 0 / 0 / 0 / 0 |
| | High | 3,264 / 7,415 / 1,092 / 3,151 | 0 / 0 / 0 / 0 |
| Rolling to and fro on Thahity Jyatha Marg, 30 m legs at 5 m/s (1,356 frames) | Low | 34 / 1,356 / 6 / 18 | 0 / 0 / 0 / 0 |
| | Mid | 82 / 1,356 / 26 / 57 | 0 / 0 / 0 / 0 (B0 peak 127 k over the 105.75 k budget before the raise gate, 100.7 k after) |
| | High | 38 / 1,356 / 18 / 55 | 0 / 0 / 0 / 0 |

Also 0 level reversals, 0 rebuilds while the rider stands and 0 collider mismatches on every ride and tier; the B0
main-thread work per frame (selection, stand-in fixed point, show and hide, colliders, upload bookkeeping; the builds
on a worker) has a median of 0.03 / 0.07 / 0.09 ms on Low / Mid / High (desktop Debug build).

## 4. What the renders and the rides showed that the numbers alone did not

* **Old rings, 11.00 s to 11.03 s on Amrit Marg (High)**: the house closing the street view turns into a flat B1 box
  for a frame while a ring cell of its block rebuilds (render `flicker_before_after.png`, top row); this is the
  flicker the owner saw. The B0 cells keep it a house in both frames (bottom row).
* **Low at 50 km/h on a turn** (first run of the car ride): a house on the outside of the turn reached the band edge
  2 frames after its build was dispatched, the 0.5 m lead covering only 1.5 frames at 9.6 m/s; its 128 m block stood in
  for 2 frames (8 flips). Finished builds are now harvested at the next upload (`B0CellSet.Harvest`), so a build
  dispatched in one frame is drawn in the next: 0 flips.
* **Rolling round Asan on Mid**: near-level houses gathered on both sides of the rider's path (26 houses, 68 k
  triangles); raises are now gated by the band's budget, so the band stays within it. The price: standing at Asan on
  Mid, 11 of the 229 B0 houses are drawn one level poorer than their distance asks (8 near, 11 lite, 210 flat; 95.7 k
  triangles); High keeps every level (36 near, 55 lite, 270 flat; 207.6 k of 225 k).

## 5. Credits (reference only)

| File | Title | Creator | Licence | Source |
|---|---|---|---|---|
| asan/ref_00 | Market Street, Asan Tole, Kathmandu Old Town, Nepal | eriktorner | CC BY-NC-SA 2.0 | https://www.flickr.com/photos/39267804@N05/13255232605 |
| asan/ref_02 | Asan Tole, Kathmandu | isapisa | CC BY-SA 2.0 | https://www.flickr.com/photos/97189192@N00/30111399277 |
| asan/ref_05 | Asan Tole, Kathmandu | isapisa | CC BY-SA 2.0 | https://www.flickr.com/photos/97189192@N00/30111400897 |
| asan/ref_06 | Asan Tole, Kathmandu | isapisa | CC BY-SA 2.0 | https://www.flickr.com/photos/97189192@N00/31174356298 |
| thamel/ref_00 | Thamel street, Kathmandu | andreweland | CC BY-NC-ND 2.0 | https://www.flickr.com/photos/46267632@N00/2003052300 |
| thamel/ref_02 | Nepal - Kathmandu - 001 - streets of Thamel | mckaysavage | CC BY 2.0 | https://www.flickr.com/photos/56796376@N00/492181084 |
