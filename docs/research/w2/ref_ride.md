# Reference brief: rideability (package `ride`, playtest round 1)

What a rider meets on the real streets of the Kathmandu valley, the rules the game follows to match it, and the
whole-sample evidence that it now does. It covers owner playtest items #8 (feet sinking into roads), #9 (the widened road
is not wider to drive on), #14 (cannot ride between houses where there is space) and #1 for non-sacred structures
(a road blocked by some structure), plus two places the owner named in earlier feedback: the Kalanki underpass and the
dhoka passages of Asan (docs/W2_PLAYTEST_1.md, docs/W2_DETAIL_CONTRACT.md §0). The sacred plinths of #1 are the heritage
package's; bridge decks, their railings and foot overbridges (#2, #15) are the bridges2 package's; the cross-slope of
hill roads and the terrain carve (#16) are the cutfill package's.

Photos are references only (Openverse, open licences, listed in §5). None is copied into the repository.

## 1. What is really there

| Place | What the rider meets | Source |
|---|---|---|
| **Thamel lanes** (Jyatha, Thamel Marg, Chaksibari) | 4-6 m of flat, worn asphalt or concrete from shop front to shop front. There is no kerb and no footpath, and motorbikes ride the whole width and squeeze past parked bikes at the walls. | photos thamel/ref_06, ref_07, galli/ref_00, ref_04; ref_roads.md |
| **Asan, Indra Chowk** | Grey stone flags run wall to wall, flush with the house plinths, with no kerb. The lanes open into small squares (chowks) and, through low gateways under the houses (dhoka), into bahal courtyards. | asan_tole/ref_00, ref_05, ref_07; data_detail_pass.md §10 |
| **Dhoka passages** | A short covered way under a house that stays whole, into a courtyard. In reality the door is low (Newar doorways make you bow; often under 2 m) and 1.2-2.5 m wide. In OSM: `tunnel=building_passage`, `covered`; 103 in the valley, about 44 in Asan and Indra Chowk. The game opens the gateway to at least 4.5 m (decision 2, rideability beats exact dimensions), and the storeys above stay solid. | dhoka_passage/ref_00 (Kumari Bahal entrance); [voicemap: Chusya Bahal](https://voicemap.me/tour/kathmandu/old-kathmandu/sites/chusya-bahal), [bhaktapur.com: Newari house](https://www.bhaktapur.com/a-closer-insight-into-typical-traditional-newari-house/) |
| **Kalanki underpass** | The Ring Road through lanes dip under Kalanki chowk: 800 m between Khasi Bazaar and Bafal, four lanes, with service lanes on the upper deck. It opened informally on 17-18 Aug 2018. In OSM the four pieces are trunk `tunnel=yes`, layer -1 (ways 225457469, 653848641, 1090820447, 1090820451); RSTR classes them as kind Underpass, car accessible, 4-7 m below the terrain. | [Kathmandu Post 2018-08-18](https://kathmandupost.com/valley/2018/08/18/kalanki-underpass-opens-for-public), [Ratopati](https://english.ratopati.com/story/5570), bridges_flyovers.md §3.3 |
| **Arterials** (Ring Road south, Kanti Path) | Raised paver footpaths sit behind 150 mm barrier kerbs (NRS 2070 §13.6). A motorbike can hop a kerb (it is lower than a stair step). The Ring Road keeps dusty dirt shoulders outside the asphalt. | ring_road/ref_00, ref_04-06; ref_roads.md |
| **Riverside cuttings** (Teku, Kalimati, Balkhu, Shankhamul) | Streets drop into cuttings under the river bridges, with masonry retaining walls. Side streets join them at their own level. | data_detail_pass.md §2, bridges_flyovers.md §3.1 |
| **Between houses** | There are open plots, chowks, courtyards and gaps of a few metres between houses. A rider uses all of them. Nothing invisible stands where nothing is built. | alley/ref_00, asan_tole/ref_04 |

## 2. Rules the game follows (the rideable street)

1. **Feet and wheels stand on the drawn surface.** The ground query answers the very triangles of the roads layer the
   player sees, within 3 cm: carriageways on their smoothed profile with crown and camber, kerb tops, footpaths, medians,
   junction caps with their kerb returns, rings and islands, and a lowered underpass in its cutting
   (`Meshing/Roads/DrawnRoadSurface`, attached to `RoadSpatialIndex.DrawnSurface` on the streaming worker from the uploaded
   roads layer). Kerb faces and road-edge skirts are steps, never ramps: up-facing triangles only, normal y ≥ 0.75.
2. **Every drawn carriageway is road for the wheels.** On the drawn surface the sample reports `OnRoad` with the road's
   own surface and class, so the widened edges and the junction caps have asphalt grip and top speed, not the biome's
   dirt. The drawn material picks the foot surface (stone flags in Asan, pavers on footpaths, grass on islands).
3. **The drivable width is the drawn width.** Nothing invisible stands inside a drawn road. A motorbike body moved
   outward from the drawn centre reaches the drawn edge, a drawn house wall or a climbable kerb (≤ 0.5 m for vehicles,
   0.55 m on foot).
4. **Solids only where something is drawn.** House walls stand at the outlines the building meshers draw: the same
   corridor guard (`BuildingFootprints`) trims both, and a house it drops is neither drawn nor solid. A courtyard that the
   trimmed outline cuts through is filled (no ring left outside the house). A dhoka passage opens the ground floor along
   its gateway. A tunnel-tagged underpass or passage (RSTR kind Underpass or Passage) is drawn, indexed, laned and
   corridored like any road; only RSTR kind Tunnel stays out (`RoadMesher.IsTunnel`).
5. **Retaining walls of a cutting open where a street is drawn.** At every wall station, a drawn road level at most 1.5 m
   (`TileSolids.AtGradeM`) above the cutting's surface opens the wall: a street joining the cutting, a junction cap, the
   corner of a kerb return. A higher one, a street over the cutting's edge, keeps the wall 0.3 m under it. A wall
   that stops at an open station ends halfway to it, so it never reaches into that road.
6. **Footpaths and caps stay inside the final corridor** the houses were trimmed to: tile-border footpath pins are
   clamped one profile step in, and kerb-return fillets use only the corridor room at their corner, so they never pave
   over trimmed houses.

## 3. Evidence: the whole kathmandu_core sample, before and after

The harness lives in /home/user/wt/previews/ride/harness (`dotnet build -p:SrcRoot=<tree>`, then `Ride <tree> all`). It
streams all 60 leaf tiles with `TileBuild.Execute` at High tier and wires the ground query as `WorldRoot.Build` does
(deck and corridor factories). Before means main 97012ab; after means w2d/ride.

| Check (owner item) | Before (main 97012ab) | After (w2d/ride) |
|---|---|---|
| Feet: drawn road surface samples (1 m grid) whose ground is 3 cm or more off (#8) | 5 074 287 of 7.58 M (66.9 %), 3.12 M of them sunk, worst > 1 m at Indra Chowk caps | 170 (0.002 %): most within about 2 m of a tile seam, where two tiles' junction caps overlap; a few where two drawn pieces overlap within 0.2 m (for example a path ribbon drawn 0.14 m over a secondary road, where the ground stands on the road) |
| Wheels "off road" on a drawn carriageway, cap or shoulder (#9) | 74 884 m² (1.02 %): the widened edges and caps at the biome's dirt top speed | 0 |
| Drivable width lost to a solid or an unclimbable step (#9) | 2.92 km of half-width, 583 cross-sections ≥ 0.3 m | 0.86 km, 268 (mostly the sample's outer edge and the riverside cuttings) |
| Invisible walls on open ground ≥ 0.45 m from every drawn house (#14) | 30 433 m² (27 031 m² in trimmed houses, 2 497 m² in dropped ones) | 352 m² (315 m² are the retaining walls of cuttings, which are drawn; 3 m² in trimmed houses) |
| Solids on a drawn road clear of the drawn houses (#1, non-sacred) | 817 m² (722 m² house walls, 76 m² railings and retaining walls) | 25 m² (21 m² bridge railings on bridge decks, the bridges2 package's) |
| Motorbike rides every drawn motor road at its centre | 98.5 % of 11 147 finished, 167 stuck | 99.8 % of 11 154, 19 stuck |
| ... and 0.9 m inside the drawn left edge (#9) | 98.1 % of 11 109, 212 stuck | 99.1 % of 11 114, 97 stuck (74 at lowered riverside roads whose terrain is not carved yet, §4) |

Core tests (they fail on main for the reasons above):

* `RideFeetTests.FeetStandOnTheDrawnRoadSurface` and `WalkersFeetStayOnTheDrawnSurface` (#8).
* `DrivableTests.DrawnCarriagewaysAreRoadForTheWheels` and `TheDrivableWidthIsTheDrawnWidth` (#9).
* `DrivableTests.NoSolidStandsWhereNothingIsDrawn` (#14 and #1).
* `DrivableTests.RidesEveryDrawnRoadToItsEdge`.
* `RideTests` for Kalanki (drawn, laned, ridden end to end 5-7 m under the terrain) and the Asan passages.
* `MeshingRoadRideTests` for caps, footpaths, joints and retaining walls.

All of them run on the feet tiles: Asan, Indra Chowk, Thamel's edge and Kalanki.

Previews (/home/user/wt/previews/ride/):

* `map_asan.png`, `map_thamel.png`, `map_teku.png`, `map_balkhu.png`, `map_kalanki.png` and `map_institutional.png`:
  top-down before and after, with the drawn road surface, drawn houses, invisible walls, solids on roads, off-road
  wheels and feet off the surface. Rendered by `tools/mapviz.py` from `Ride <tree> map lat lon half step`.
* `kalanki_roads_cmp.png` and `kalanki_side_cmp.png`: the underpass is drawn now. `kalanki_after_street*.png` is the
  view from inside it, with and without the terrain.
* `cmp_thamel_lane.png`: a Thamel lane photo against the game at eye height.

## 4. Open issues for other packages

* **cutfill (TerrainCarve):** carve the terrain over RSTR-lowered roads (the Kalanki trench, the riverside cuttings at
  Teku, Kalimati, Balkhu and Shankhamul). The drawn underpass and its ground are right, but the uncarved terrain surface
  stands over them (`kalanki_after_street.png`). At 74 lowered road edges the terrain beside a lowered junction is
  1.4-5 m above the road where the drawn surface stops: edge rides stop there.
* **bridges2:** bridge railings (TileSolids `Railings`, elevated branch) stand at `half + DeckKerbM` while the bridges
  package draws the railing at `HalfL + WalkL` (beyond the footpath). At Teku (Bishnumati bridges 248967188 and
  1123582689) and Balkhu (Ring Road decks) the solid railing stands on the drawn deck footpath (`map_teku.png`, magenta).
  Move the solid railing to the drawn railing line (BridgeLayout `InnerL`/`InnerR`), or take the railings from
  BridgeColliders, as the bridges2 WIP does for covered roads.
* **data / roads (RSTR heights):** a few lowered pieces end metres below the junction they join (Residential 328701965
  at Balkhu ends 4.4 m under the at-grade junction). The drawn surface has a step there that no carve fixes.
* **roads:** where a junction cap reaches over a tile border onto the neighbour's ribbon, the two tiles' surfaces overlap
  by up to 2 m at a 4-6 cm height difference. The ground stands on the upper one, which is the visible one.

## 5. Photo credits (Openverse; reference only)

| Set | File | Title | Author | Licence |
|---|---|---|---|---|
| thamel | ref_06 | Thamel street | stewie811 | CC BY-NC-SA 2.0 |
| thamel | ref_07 | Thamel streets | enderzero | CC BY-NC-SA 2.0 |
| galli | ref_00 | Nepal - Kathmandu - 001 - streets of Thamel | mckaysavage | CC BY 2.0 |
| galli | ref_04 | Tourists walking, signs, and motorcycles in downtown Kathmandu, Thamel | Wonderlane | CC BY 2.0 |
| asan_tole | ref_00 | Asan Tole | Jorge Lascar | CC BY 2.0 |
| asan_tole | ref_05 | Market Street, Asan Tole, Kathmandu Old Town | eriktorner | CC BY-NC-SA 2.0 |
| asan_tole | ref_07 | Apple seller outside Ganesh Shrine, Asan Tole | markhorrell | CC BY-NC-SA 2.0 |
| dhoka_passage | ref_00 | Entrance to Kumari Bahal | kasiat | CC BY-NC-SA 2.0 |
| ring_road | ref_00 | Kathmandu Ring Road | Doug Letterman | CC BY 2.0 |
| ring_road | ref_04-06 | Valley Strike | anantal | CC BY 2.0 |
| alley | ref_00 | Kathmandu alley | niksin | CC BY-NC-SA 2.0 |

The Flickr source URLs are in /home/user/wt/refs/ride/*/credits.txt.
