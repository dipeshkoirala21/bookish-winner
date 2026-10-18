# Reference brief: rideability (package `ride`, playtest round 1)

What a rider meets on the real streets of the Kathmandu valley, the rules the game follows to match it, and the
whole-sample evidence that it now does. It covers owner playtest items #8 (feet sinking into roads), #9 (the widened road
is not wider to drive on), #14 (cannot ride between houses where there is space) and #1 for non-sacred structures
(a road blocked by some structure), plus two places the owner named in earlier feedback: the Kalanki underpass and the
dhoka passages of Asan (docs/W2_PLAYTEST_1.md, docs/W2_DETAIL_CONTRACT.md §0), and the three spots the camera package's
rides ran into (docs/research/w2/ref_camera.md §6, §7.1): a bus blocked on Kantipath at Ratna Park, a bus blocked turning
into Bhaktithapa Road, and an "unidentified overhead solid" north of Thamel. The sacred plinths of #1 are the heritage
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
| **Ratna Park junction** (Kantipath at Rastriya Sabha Griha, 85.3162, 27.7062) | An at-grade crossing of four main roads. Pedestrians cross overhead on steel foot bridges (Ratnapark-Bhotahiti, Ratnapark-Bagbazar, Jamal). The RSTR data instead holds the four foot decks 2.0-2.7 m over the terrain and lowers the junction node and its four arms 4.0 m (kind Underpass, Lowered), so the decks clear 5.5 m (§4). The game draws what the data says, consistently. | [Himalayan Times: overhead bridges](https://thehimalayantimes.com/kathmandu/21-locations-valley-need-overhead-bridges), [ShareSansar: KMC overhead bridges](https://www.sharesansar.com/newsdetail/kathmandu-metropolitan-city-building-two-overhead-bridges-in-first-phase); RSTR ways 171603746, 171215507, 171203194, 220025931 |
| **Junction corners on bus roads** | A bus (Sajha, Mahanagar: 10-11 m long, 2.5 m wide) swings its front overhang across the outside of a turn. Kerb returns are about 4.6 m (15 ft) on residential streets and 7.6 m (25 ft) on arterials with turning buses; urban guides keep 3-4.6 m. Corner houses in Kathmandu stand at the kerb line, often chamfered at the corner. | [PEDSAFE: curb radius](https://www.walkinginfo.org/engineering/crossings-curb/), [NACTO: corner radii](https://nacto.org/publication/urban-street-design-guide/intersection-design-elements/corner-radii/), [NAP: bus turning radii](https://nap.nationalacademies.org/read/23171/chapter/5) |
| **Lainchaur foot overbridge** (85.3130, 27.7178) | A pedestrian overbridge with railings across the Primary road north of Thamel (OSM Footway 1268595053, RSTR Flyover, FootOverbridge, layer 1). Its deck stands 5.4 m over the drawn road. | RSTR; camera package §7.1 |

## 2. Rules the game follows (the rideable street)

1. **Feet and wheels stand on the drawn surface.** The ground query answers the very triangles of the roads layer the
   player sees, within 3 cm: carriageways on their smoothed profile with crown and camber, kerb tops, footpaths, medians,
   junction caps with their kerb returns, rings and islands, and a lowered underpass in its cutting
   (`Meshing/Roads/DrawnRoadSurface`, attached to `RoadSpatialIndex.DrawnSurface` on the streaming worker from the uploaded
   roads layer). Kerb faces and road-edge skirts are steps, never ramps: up-facing triangles only, normal y ≥ 0.75.
   Where two drawn pieces overlap a step apart (at most `DrawnRoadSurface.CoveredM` = 0.45 m, the ground query's step-up:
   a mapped sidewalk drawn at road level inside a raised paver footpath, a path ribbon over a lane, two tiles' caps at a
   seam), the lower one is hidden under the upper one, and the feet stand on the upper one, the surface the player sees.
   Before, a walker coming along the lower piece stayed on it, up to 0.45 m inside the visible surface (the walk harness,
   §3). Pieces farther apart (a street over a lowered underpass) keep the level nearest the feet.
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
   over trimmed houses (except bus corners, rule 8). The first segment at a tile-border cut takes its footpaths from the
   cut row alone (`RibbonMesher.SegmentKinds`), the border pin both tiles share, so the kerb runs on across the seam even
   where the border crosses the road at an angle and one tile's corridor ends the footpath within the first profile step
   (that tile dropped the footpath from the whole segment, the other kept it: a kerb step at 43 seam vertices, e.g. Secondary
   199430771 at 85.30214, 27.70385, `seam_cmp.png`).
7. **Junction caps meet their arms level** (`Meshing/Roads/CapLevels`). A cap is drawn a lift above the terrain, where
   the pinned ribbon ends of draped arms meet it. An arm whose ribbon follows structure heights at its cut (a lowered
   underpass, a ramp onto a deck) carries the cap with it: exactly the ribbon's end row on that arm's cut section, and
   inside the cap the arms' offsets blended by inverse squared distance to their cuts. Islands and police podiums on the
   cap sink or rise with it, and the analytic surface query (`RoadSurfaceQuery`) answers the same height. At the Ratna Park
   junction the cap now sinks with its four arms instead of standing 4.3 m above them.
8. **Bus corners keep a 4.5 m kerb return** (`RoadLayout.BusCornerRadiusM`). At a corner between two car roads both drawn
   at least 6.5 m wide, at least 60° apart and outside the old cores, the corner-room clamp of rule 6 never shrinks the
   kerb return below 4.5 m (the residential norm; the design radius there is 6 m). The corner house is trimmed back to the
   cap, drawn and solid alike (decision 1: rideability beats exact footprints). 518 caps of the sample have such a corner;
   together they cut 43 m² of pipeline-trimmed houses.

## 3. Evidence: the whole kathmandu_core sample, before and after

The harness lives in /home/user/wt/previews/ride/harness (`dotnet build -p:SrcRoot=<tree>`, then `Ride <tree> all`,
`Ride <tree> walk 40`, `Ride <tree> offroad`). It streams all 60 leaf tiles with `TileBuild.Execute` at High tier and
wires the ground query as `WorldRoot.Build` does (deck and corridor factories). Before means main 64155f3; after means
w2d/ride. Feet are measured against the level the player sees (rule 1: a drawn level covered by another at most 0.45 m
above it counts as that one), on both sides of the table.

The walk harness is the explorer on foot (`ArcadeVehicle` with the game's walker spec, running at full stick, the game's
1/60 s fixed step, no road assist): 45 000 walks of up to 40 m along the middle of every drawn footpath, the centre of
every lane, galli, footway, path, steps and dhoka passage, and across every junction cap from each arm's cut to the next.

| Check (owner item) | Before (main 64155f3) | After (w2d/ride) |
|---|---|---|
| Feet: drawn road surface samples (1 m grid) whose ground is 3 cm or more off (#8) | 5 075 273 of 7.72 M (66.9 %), 3.13 M of them sunk, worst > 1 m at Indra Chowk caps | 47 (0.0006 %), 3 sunk; most within about 10 m of a tile seam, where the feet stand on the neighbouring tile's cap up to 0.6 m over this tile's piece |
| Walk harness: walker steps on the drawn surface with the feet 3 cm or more off it (#8) | 5 036 531 of 10.24 M (49.2 %), 3.15 M of them sunk; 78 769 steps 0.3-1 m off | 548 of 8.92 M (0.006 %), 361 sunk, in 13 places; the worst a footbridge deck 0.47 m under its drawn path (Path 762800882, the bridges package's deck, §4) |
| Walk harness: walks that get through (#9, #14) | 98.5 % of 45 065, 656 stuck (218 against a solid) | 99.6 % of 45 240, 170 stuck (7 against a solid; most others at lowered roads and their junction caps beside uncarved terrain, §4) |
| Wheels "off road" on a drawn carriageway, cap or shoulder (#9) | 74 885 m² of 7.32 M (1.02 %): the widened edges and caps at the biome's dirt top speed | 0 of 7.28 M |
| Drivable width lost to a solid or an unclimbable step (#9) | 2.91 km of half-width, 578 cross-sections ≥ 0.3 m | 0.78 km, 248 (mostly the sample's outer edge and the riverside cuttings) |
| Invisible walls on open ground ≥ 0.45 m from every drawn house (#14) | 30 409 m² (27 031 m² in trimmed houses, 2 497 m² in dropped ones) | 312 m² (300 m² are the retaining walls of cuttings, which are drawn; 3 m² in trimmed houses) |
| Solids on a drawn road clear of the drawn houses (#1, non-sacred) | 817 m² (722 m² house walls, 76 m² railings and retaining walls) | 25 m² (22 m² bridge railings on bridge decks, the bridges2 package's) |
| Motorbike rides every drawn motor road at its centre | 98.5 % of 11 147 finished, 167 stuck | 99.9 % of 11 154, 15 stuck |
| ... and 0.9 m inside the drawn left edge (#9) | 98.1 % of 11 109, 212 stuck | 99.5 % of 11 114, 53 stuck (most at lowered riverside roads and underpasses whose terrain is not carved yet, §4) |

Core tests (they fail on main for the reasons above):

* `RideFeetTests.FeetStandOnTheDrawnRoadSurface` and `WalkersFeetStayOnTheDrawnSurface` (#8), measured against the
  level the player sees; `FeetStepOntoTheDrawnPieceThatCoversAnother` (the mapped sidewalk Footway 802516424 inside the
  raised footpath of Primary 169442477: feet 0.27 m inside the visible footpath before) and
  `WalkersOnLanesAndFootwaysStandOnTheVisibleSurface` (654 walks along the lanes and foot ways of Asan and Indra Chowk:
  827 steps sunk up to 0.45 m before, none after).
* `RideCameraSpotsTests` for the camera package's spots: the bus through the Ratna Park junction on Kantipath (centre
  and 1.5 m left), the bus turning into Bhaktithapa Road on the centre line, and the Lainchaur foot overbridge north of
  Thamel identified as the overhead solid.
* `MeshingRoadDetailTests.RibbonsAreContinuousAcrossEveryTileSeam` (43 border vertices without a partner across the seam
  with the corridor-clamped footpaths before the cut-segment rule, 1 after, within the 3 allowed).
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
* `seam_cmp.png`: the border of tiles 515/161 and 516/161 at Secondary 199430771 (85.30214, 27.70385), before and
  after the cut-segment rule: the footpath now runs on to the seam in both tiles.

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
* **bridges2 (small foot decks):** the deck of the footbridge Path 762800882 (85.35720, 27.71187) stands 0.47 m under
  the path ribbon the roads layer draws over it (deck query 1304.47 m, drawn 1304.94 m): a walker on it is 0.4 m inside
  the drawn path, the worst of the walk harness's 13 remaining places (§3).
* **data / roads (RSTR heights):** a few lowered pieces end metres below the junction they join (Residential 328701965
  at Balkhu ends 4.4 m under the at-grade junction). The drawn surface has a step there that no carve fixes.
* **roads:** where a junction cap reaches over a tile border onto the neighbour's ribbon, the two tiles' surfaces overlap
  by up to 2 m at a 4-6 cm height difference. The ground stands on the upper one, which is the visible one.
* **camera + bridges (Lainchaur foot overbridge, 85.3130, 27.7178):** the camera package's "unidentified overhead solid"
  north of Thamel is the foot overbridge at Lainchaur (OSM Footway 1268595053, RSTR Flyover, FootOverbridge, layer 1) over
  Primary 176144636, drawn by the bridges package with railings: a real structure, nothing stray
  (`RideCameraSpotsTests.TheOverheadSolidNorthOfThamelIsTheLainchaurFootOverbridge`). The road index stands every deck's
  slab `RoadSpatialIndex.DeckThicknessM` = 1.2 m deep (the vehicle deck depth), so under this foot deck, drawn 0.6 m deep
  (`RoadStructureRecord.FootDeckDepthM`), an undrawn 0.6 m of slab hangs 4.40 m over the carriageway where the drawn deck
  clears 4.99 m. Taking the foot deck depth there (tried: `RoadSpatialIndex.DeckDepthOf`) makes the camera package's car
  chase boom snap 1.75 m (portrait) on its Thamel-Ratna Park ride under this bridge
  (`CharactersCameraRideTests.TheCameraStaysSteadyOnRealStreets`), so the change waits until the chase boom passes under
  a thinner slab smoothly. No vehicle is affected (a bus is 3.2 m tall).
* **roads / camera (bus turning from Tanka Prasad Ghumti Sadak into Bhaktithapa Road, 85.32753, 27.69324):** the camera
  package's bus on the centre line now drives on (bus corners keep a 4.5 m kerb return, rule 8;
  `RideCameraSpotsTests.TheBusTurnsFromGhumtiSadakIntoBhaktithapaRoad`). Its bus 1.5 m left of the centre line still
  stops there: it keeps to the outside of the 46° bend Bhaktithapa Road takes at the node, its front overhang swings out
  over the 14 m arm of Ghumti Sadak, which bends 4.5 m from the node, so the cap has no room for a kerb return on that
  side, and it reaches 0.65 m into the corner house (with the house hidden). Drawing the apex of a 4.5 m return there
  (tried) cut that to 0.54 m, still not clear, and moved the centre-line bus's chase boom by 0.5 m in a frame
  (`CharactersCameraRideTests`, bus Maitighar-Baneshwor). It needs a flared junction (the arm widened before its bend) or a
  bus autopilot that keeps to the inside of the turn.
* **cutfill / data / bridges2 (the walk harness's stuck walks, §3):** most of the 170 walks that stop are at the lowered
  riverside roads and their junction caps (Teku, Kalimati, Balkhu, Thapathali, Ratna Park), where the uncarved terrain
  stands over the drawn edge or the RSTR heights step at a junction. A few are the stair approaches of the Ratna Park foot
  overbridges (OSM Steps 112116413, 112116415, 112449579, 112449580, RSTR Approach): the walker climbs the drawn stair
  ramp and stops where it meets the deck (owner #15, the bridges2 package's).

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
