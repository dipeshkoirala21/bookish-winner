# Reference brief: roads of the Kathmandu Valley today (W2 detail pass, package `roads`)

> What the road mesher (`Core/Meshing/RoadMesher.cs`, `Core/Meshing/Roads/**`) models, and why. Facts and numbers come
> from [roads.md](roads.md) (R) and [W2_DESIGN](../../W2_DESIGN.md) §4, checked against open-licensed photos
> (Openverse, reference only, never copied into the repo; credits at the end) and recent press. Owner feedback that
> drives this pass: "Roads have sharp corners", "Some of the roads are too narrow", "Make roads smoother. Make it wide",
> "Even on the narrower streets of Kathmandu at least 3 bikes can pass", "Make roundabouts more detailed".
> Cartoon rule: surfaces are simplified and proportions slightly bolder; silhouettes, details and colours stay faithful.

## 0. What a Kathmandu local must recognise

| Place | What is there now (2023-2026) | Must read in the game |
|---|---|---|
| **Ring Road south** (Kalanki-Koteshwor) | China-aided 8-lane upgrade: per side 2 main lanes + 1-2 service lanes + cycle track + footpath, 62 m right of way [S: Kathmandu Post, ShareSansar]; main carriageways about 6.5-7 m, kerbed median about 1 m (OSM centrelines 7.4 m apart) [O, R §6.1]; dark, fairly fresh asphalt with white lane lines and a yellow line at the median | a wide dual carriageway: dark asphalt, white dashed lane lines, yellow edge lines, a narrow raised median with painted kerbs, raised red/grey paver footpaths behind 150 mm kerbs |
| **Ring Road north**, Lazimpat, Kanti Path | undivided 4-lane arterials, 14 m; Kanti Path and Durbar Marg have 3-4 m footpaths and tree medians on divided stretches [O, R §6] | wide grey-blue asphalt, centre and lane lines, kerbed footpaths both sides |
| **Thamel lanes** (Jyatha, Thamel Marg, Chaksibari) | 4-6 m of road between shopfronts (corridor p25/p50/p75 5.8 / 8.8 / 15 m); shared surface, no footpath; worn asphalt and concrete, KMC stone and brick re-paving in the heritage walk (Thamel-Basantapur) [S: Kathmandu Post 2018; eKantipur 2025] | narrow but rideable (>= 4.8 m drawn: three motorbikes abreast), no centre line, no kerbs, dusty worn surface |
| **Asan, Indra Chowk, Kel Tole** | flat grey stone slabs wall to wall (KMC laid stone on 33,000 m2 of heritage lanes and squares, 2025) [S: eKantipur], brick drain channels, no kerbs, no paint | grey **stone-flag** paving flush to the house plinths |
| **Bhaktapur, Patan, Thimi, Kirtipur cores** | red brick laid herringbone, stone kerb edging and drain channels, brick squares (Taumadhi, Potter's Square) [V: photos] | red **brick** paving, no paint |
| **Roundabouts** (Jawalakhel, Thapathali, Maitighar Mandala, Balaju, Tripureshwor) | true circles, ring 7-10 m wide, raised island with a painted (yellow-black) kerb, often a planted edge, a statue or monument in the middle, traffic police on a podium at the busy ones [O, R §7, W2_DESIGN 4.7] | a round ring of asphalt, a rounded raised island with a striped kerb and a planting strip, splitter islands on wide arms, white give-way dashes |
| **Hill roads** (Nagarkot, Chandragiri, Pharping, Godawari, valley rim) | 5.5-7 m asphalt with 0.75-1 m earth shoulders, tight hairpins; an uphill open drain [R §8.2] | smooth curves (no polygon kinks), shoulders, the road benched into the slope (a skirt on the downhill side) |

## 1. Cross-section (game metres; real widths × 1.25 for motor roads, decision 1 floor 4.8 m)

* **Carriageway.** Width model `RoadWidthModel` (W2_DESIGN 4.1-4.4) inside the RATR corridor; every street is drawn
  at least `RoadClearance.MinCorridorM` = 4.8 m wide, footways, paths and steps at least 2.5 m (their clear corridor is
  still 4.8 m). The footpaths and shoulders the class and area give are **reserved first**: the carriageway widens
  beyond its real width only into what the corridor leaves beside them (Kanti Path and Durbar Marg keep their kerbed
  pavements; the real width itself is never given up). Lanes, access masks and the stage-one driving index follow the
  §4.2 width (`RoadWidthProfile.Width`); the surface as drawn (`Drawn`, about `ShiftAt`, with caps and rings) is
  exported for physics and traffic as `Roads.RoadSurfaceQuery` (height, normal and kind of surface at any point, the
  lowered underpass stretches to cut; §8). Gentle crown: the centre vertex carries the surface colour 12 % lighter
  (worn wheel tracks, not paint), on ribbons and junction caps alike.
* **Barrier kerb** next to footpaths: 150 mm high (NRS 2070 §13.6), 150 mm top, rounded nose (cartoon), concrete
  `#BDB8AE`; gutter AO 0.62-0.72.
* **Footpath**: +150 mm, 2.5 % cross-fall to the road, red `#B5655A` or grey `#9C9A94` interlocking pavers (one per way),
  channel Flagstone; 2.5-3.0 m on trunk, 2.0-3.5 m on primary, 1.5-2.0 m on secondary/tertiary in urban areas
  (60 % both sides, 20 % one side, 20 % none), none in old cores (shared surface) [R §8.2]. Rounded outer edge, which
  follows the terrain where the ground beside the road rises or falls. A footpath changes width along a 1 : 4 kerb
  flare (not the carriageway's 1 : 20 taper), so one narrow spot does not take the pavement off 20 m either side; on
  the sample 95 % of urban arterial length with room for a footpath has one. **Mapped sidewalks** (OSM footways and
  paths along a street) become that street's footpath, widened to cover them up to 3.5 m, instead of a second ribbon
  across its asphalt; a footway crossing the carriageway is the carriageway there (both stay drawn within 10 m of a
  tile-border cut so the two tiles agree).
* **Side by side**: carriageways of unconnected parallel streets (the one-way pairs of Durbar Marg and Jamal, the Ring
  Road's main and service carriageways) stop at their shared gap, each side in proportion to what both want. A side
  narrows to its real width first and keeps its footpath (two footpaths back to back read as the kerbed separator),
  then drops the footpath; where the mapped centrelines are too close even for the real widths the carriageway moves
  away by up to 1 m and narrows for the rest (never under the 4.8 m floor, a trunk-to-tertiary road never under a 6 m
  bus width). The two carriageways of a dual road mapped closer than their real widths and the median allow move apart
  by up to 1 m each. Everything tapers at 1 : 20 and tile-border cut ends keep the width and position both tiles agree
  on. Building **passages** (RSTR `Passage`, a dhoka under a house that stays whole) are drawn as the gateway (the real
  width within 1.5-3.5 m, no footpaths, outside the clear corridors). An RSTR **corridor shift** (the corridor moved
  off a protected footprint) moves the carriageway with it: ribbon, grade, caps, markings, corridor index and surface
  query all offset it by the same `RoadWidthProfile.ShiftAt`.
* **Median** (dual carriageways, `RATR.partner_way`): mountable 100 mm kerb with a 45° face, painted in 0.5 m
  **yellow `#E8C547` / black `#2B2C2F`** bands near its noses (Kathmandu practice; IRC:35 says black-and-white) [V],
  grass `#6FAE4A` top when >= 1.6 m wide (Kanti Path, Durbar Marg trees), concrete `#A9A59C` otherwise (Ring Road).
* **Shoulder** (rural trunk 1.5 m, hill trunk/primary 1.0, hill secondary 0.75): unpaved `#BBAF96`, channel Dirt.
* **Skirt** everywhere else: a rounded 0.28 m drop from the road edge to just under the terrain, so the slab never
  floats and reads as a thick, soft-edged cartoon road.
* **Heritage paving** (old cores, minor classes, surface not tagged as asphalt or concrete): Kathmandu core grey stone
  flags `#9A968E` (channel Flagstone, 60 %) or brick (40 %, by way); Bhaktapur, Thimi, Patan and Kirtipur red brick
  `#A4553A` herringbone (channel Brick). The look package's procedural textures draw the bond and joints.

## 2. Plan geometry (owner: "sharp corners", "not smooth")

* Every mapped corner gets a **circular fillet** whose radius comes from the class and design speed: trunk 60 m,
  primary 40, secondary 30, tertiary 22, residential/unclassified 14, service/living street/track 10, pedestrian 8,
  footway/path 6, steps 3; halved in old cores (>= 4 m); never under half the width + 1.5 m (NRS/NURS curve
  practice; hairpins on hill roads 12-20 m at the apex [O: F24 3.6 m apex]). The fillet is shared in proportion with
  the neighbouring corner when segments are short and may leave the mapped corner by at most 2.5 m (old core), 4 m
  (built-up) or 8 m (open country), half that on footways, so corner houses are not cut away.
* A bend mapped as several close vertices (or with a jog of mapping noise inside it) is **merged** into one corner at
  the intersection of its outer legs (within half the deviation limit), so the whole bend gets the class radius
  instead of a run of tight arcs; a vertex where the way doubles back on a leg under 10 m is dropped as a **spike**.
  On the sample 78 % of all corners get their full class radius; the rest are limited by short legs, the deviation
  limit (corner houses), a tile cut or a junction cap (which draws the turn itself).
* Arcs are densified by a sagitta rule at the outer road edge (<= 5 cm chord error, at most 15° a step), so a curve
  never shows polygon kinks; two OSM ways that continue each other at a node (exactly two ends, nothing else) share
  one fillet ("knee") and one width.
* **Tile borders**: the context points give both tiles the same direction across the border, bit for bit (from the
  vertex before the cut to the vertex after it, which both tiles hold); the curve is straight with zero curvature
  through every cut, so both sides agree on position, tangent and curvature (curvature-continuous). The cross-section
  at a cut is tile-independent too: the border width, footpaths and shoulders of the valley-floor (URBAN) or RURAL
  column by where the cut lies, kerbs and columns by the way's class and width tag; synthetic bridge decks meet level
  at the lifted centre. On the sample 99.7 % of the 18,600 border vertices meet their partner within 2 mm, all but
  one within 1 cm (the rest are pieces cut right next to a tile corner).
* **Vertical profile**: the terrain drape along the smoothed centreline is low-passed (Gaussian, 6-12 m by class),
  grade changes capped at 1 % per metre (a 100 m vertical curve) and held within ±0.12 m of the banked plane through
  the lifted terrain at its highest point across the road (at most 0.20 m above the centre: along a gully the low
  columns are lifted instead of the whole road floating). The cross-fall follows the terrain's cross slope (smoothed
  over 6 m) up to the comfortable 25 %, and further, up to 45°, wherever holding 25 % would leave an edge more than
  0.12 m off the terrain, so a hill road **hugs the hillside** (the Pasang Lhamu Highway along an 80 % slope no longer
  stands on a 9 m causeway; on the sample no centre is more than 0.5 m above the lifted terrain). A true bench, cut
  uphill and filled downhill, needs the terrain mesher (§8). Seams (tile cuts, junction caps, ring entries, knees) fade
  to the exact terrain plus the lift both sides agree on.
* **Knees** (two pieces continuing each other at a node, ground pieces on different OSM layers included) have one
  width: the wider end tapers down to at most 3 m above the narrower at 1 : 6, and after the side-by-side clamp the
  other end follows it, so a two-piece node has no width step (a street meeting a deck end tapers to the deck).

## 3. Junctions and roundabouts

* **Junction caps**: one surface per junction from the game widths of every arm, kerb-return arcs between arms
  (6 m when both arms are >= 6.5 m, 3 m otherwise, 1.5 m in old cores); footpaths and kerbs run round the corner
  when both arms have one on that side, a skirt otherwise; ribbons stop at the cap edge (no overlapping slabs). The cap
  takes the ribbon's end row vertex for vertex: the same cut, the same columns, the lighter crown and the AO of each
  column, so there is neither a hairline gap nor a colour seam where they meet; kerbed outlines (islands, medians)
  are ear-clipped so concave ones never fold.
* **Roundabouts** (mapped rings, JNCT): a true circle; ring width = widest approach + 1 m, at least 7 m; island
  diameter from the mapping (Jawalakhel ring centreline 43.8 m → island about 35 m, measured 36 m [O]); a rounded
  mountable island kerb in yellow-black bands, a 1 m cobbled apron (`#9A8F84`) for buses, a planting strip of soil
  `#6E4E36` with low flowers `#D9714E` inside the inner kerb, grass inside it (the ornaments package places the statue,
  garden or monument in the free interior, `RoadIsland.InteriorRadiusM`). Entries flare onto the circle with kerb
  returns; wide two-way approaches (>= 7 m) get a raised teardrop **splitter island**; white give-way dashes across
  each entry, a yellow edge line round the island and a dashed lane circle on rings >= 9 m wide.
* **Outer kerbs**: on built-up rings a kerb and a paver footpath run round the outer circle between the entries
  (the approach's own footpath, or 2.3 m where it brings none: Tripureshwor, Thapathali and Maitighar have kerbed
  footpaths all round), with end faces where they stop at an entry. Two neighbouring one-way approaches, or the two
  carriageways of a dual road, whose facing cut edges are at most 8 m apart get a kerbed **nose island** in the wedge
  between them (yellow-black kerb, grass when large enough) with a tail of up to 14 m running back between the
  carriageways, instead of bare ground.
* **Police chowks** (curated list, W2_DESIGN 4.7): white podium Ø 1.4 m, 0.35 m high, red umbrella. **Off by default**
  (`RoadOptions.PolicePodiums = false`): the ornaments package (RoundaboutDecorator, `Centrepiece.PolicePodium`) draws
  the podium, drum or umbrella of every police island and chowk, and two generators at one island would overlap; the
  islands themselves (`RoadIsland.PolicePodium`) are laid out either way.

## 4. Markings (RSN5 Table 1, IRC:35; R §10)

| Marking | Game value |
|---|---|
| Centre line | white `#F2F0E8`, 125 mm (100 mm × 1.25), broken 1.5 / 4.5 m urban, 2 / 7 m rural; 4 / 2 m warning in the last 30 m before a junction; continuous on bridges; only where the **real** width >= 5.5 m on asphalt or concrete, never in old cores |
| Lane lines | as centre lines, on dual carriageways and roads with >= 3 lanes |
| Edge line | yellow `#F2C230`, 150 mm, 100 mm in from the edge, trunk to secondary outside old cores |
| Zebra | 0.5 m white bars with 0.5 m gaps across the carriageway, 2-4 m deep; at mapped marked crossings, every 200 m on urban trunk/primary/secondary, at controlled chowks; stop lines 200 mm, 2.5 m before |
| Wear | crisp in URBAN and OLD_CORE, blended 30-60 % toward the asphalt elsewhere |

All markings follow the drawn ribbon exactly (same rows, same heights + 12 mm), material channel `Marking`.

## 5. Structures (decisions 3-4)

* Bridges and flyovers with deck heights (`RoadStructureRecord.IsElevated`): the road surface is drawn at `DeckY`
  + 3 cm (the bridges package draws slab, kerbs, railings and piers); deck heights are interpolated (Catmull-Rom)
  between the mapped points; at-grade pieces ramp onto deck ends at <= 8 %.
* Underpasses whose clearance is under 5.5 m are lowered with 8 % ramps; retaining walls line the cutting.
  Tunnels are not drawn. OSM `bridge=yes` pieces without deck data run straight between their lifted ends.

## 6. Previews next to the photos

Street-level renders of the sample (tools/mesh-preview, `MeshingRoadPreviews`, buildings that stand in a corridor
left out as the buildings package trims them) were put side by side with the photos above (Pillow composites, kept
outside the repository):

* **Thamel** (pedestrian lane, shared surface): brick-red heritage paving in a 4.8 m lane between house fronts, no
  paint, no kerbs, as in the Thamel photos; the real lanes are as narrow but busier (shop fronts, wires: other packages).
* **Asan** (old-core lane): grey stone flags (`#9A968E`, channel Flagstone) without paint or footpaths, the road
  following the chowk's open shape; the photo's flags read the same grey.
* **Kanti Path** (old-core arterial): asphalt with a dashed white centre line, yellow edge lines and junction caps
  with kerb returns; where the corridor has no room the footpath is left out (Durbar Marg next door has a planted
  median and wide pavements, drawn where RATR gives the room).
* **Roundabouts** (Balaju, Tripureshwor): a true circle with a yellow-black banded island kerb, apron and planting
  strip, give-way dashes and a lane circle; red paver footpaths with kerbs round the corners; the Tripureshwor photo's
  green-and-yellow island kerb matches the banded kerb's role (the ornaments package draws the hedge and statue).
* **Hill switchback** (synthetic rim road: the sample has no hill roads): two hairpins mapped with five vertices each
  are drawn as smooth bends, every corner at the tertiary class radius (22 m) with short straights between; the
  road leans with the 16 % slope and stays on the terrain, as on the Zhangmu road in the photo.

Review fix pass (renders of the final code next to the photos, `previews/roads/compare_fix_*.png` outside the
repository):

* **Hill road** (Pasang Lhamu Highway on the sample's valley rim, an 80 % cross slope): the carriageway leans with the
  hillside and both edges stay on the drawn terrain; no causeway stands above the downhill side (it stood up to 9 m
  before). The photos (Zhangmu road, a switchback cut across a slope) show the real thing benched level into the hill
  with a cut face uphill: that needs the terrain mesh cut and filled to the road (§8).
* **Roundabouts** (Tripureshwor, Jamal): kerbed paver footpaths run round the outer circle between the entries, as round
  Maitighar Mandala in the photo; the one-way pairs entering Jamal and the dual carriageways at Tripureshwor get a
  kerbed grass nose island (yellow-black kerb) with a tail between the carriageways instead of bare ground; the island
  top rises over a low ridge of ground between the two carriageways so the terrain never shows through it.
* **Kanti Path junction cap**: lit and unlit (vertex colours) views show no colour seam or gap where the cap meets the
  ribbons (same crown column, same AO); the kerbed footpaths stay on both sides as on Durbar Marg in the photo (the
  footpath colour still changes at the way boundary, §8).

## 7. Photo credits (Openverse; reference only)

| Topic | Photo | Creator, licence |
|---|---|---|
| City traffic, worn asphalt, yellow-black chevron barrier | "Street traffic in Kathmandu" flickr.com/photos/10816734@N03/3426991359 | World Bank Photo Collection, CC BY-NC-ND 2.0 |
| Arterial with buses, lane paint | "Street traffic in Kathmandu" flickr.com/photos/10816734@N03/3427798280 | World Bank Photo Collection, CC BY-NC-ND 2.0 |
| Ring Road (pre-upgrade, single carriageway) | "Kathmandu Ring Road" flickr.com/photos/47335827@N00/5241601374 | Doug Letterman, CC BY 2.0 |
| Thamel lane, shared surface | "Nepal - Kathmandu - 001 - streets of Thamel" flickr.com/photos/56796376@N00/492181084 | mckaysavage, CC BY 2.0 |
| Thamel lane at dusk | "Thamel, Kathmandu" flickr.com/photos/57277045@N07/12407774444 | esmar.abdulhamid, CC BY 2.0 |
| Asan stone flags | "Asan Tole, Kathmandu" flickr.com/photos/97189192@N00/30111400897 | isapisa, CC BY-SA 2.0 |
| Kathmandu core stone paving | "Kathmandu Street" flickr.com/photos/23303789@N05/8137432489 | clee130, CC BY-NC-SA 2.0 |
| Bhaktapur brick lanes | "Bhaktapur street" flickr.com/photos/24172274@N00/30368358088 | scottgunn, CC BY-NC 2.0 |
| Bhaktapur herringbone square | "Potter's Square in Bhaktapur" flickr.com/photos/39267804@N05/32860442130 | eriktorner, CC BY-NC-SA 2.0 |
| Patan stone and brick lanes | "Streets of Patan" flickr.com/photos/86419644@N00/18186290763 | René Clausen Nielsen, CC BY-SA 2.0 |
| Raised footpath with kerb | "Typical street scene in Kathmandu" flickr.com/photos/71401718@N00/3161594756 | Wonderlane, CC BY 2.0 |
| Switchback road | "Road to nowhere" flickr.com/photos/56278354@N00/49195564813 | ePi.Longo, CC BY-SA 2.0 |
| Hill road on a slope (comparison) | "Zhangmu traffic" flickr.com/photos/18728817@N00/6098137882 | Easternblot, CC BY-ND 2.0 |
| Arterial with median, Durbar Marg (comparison) | "Durbar Marg, Kathmandu, Nepal" flickr.com/photos/40679851@N05/3745567016 | bklorfine, CC BY-NC-ND 2.0 |
| Roundabout island, Tripureshwor (comparison) | "Tripureshwor Bus Stop, Tripura Marg" flickr.com/photos/41231665@N07/17748741420 | KatjaUlbert, CC BY-SA 2.0 |
| Maitighar Mandala from above (fix-pass comparison) | "Maitighar Mandala" commons.wikimedia.org/w/index.php?curid=36916328 | Ratopati.com, CC BY-SA 4.0 |

Press: [Kalanki-Koteshwar to turn into an eight-lane road](https://www.sharesansar.com/newsdetail/kalanki-koteshwar-to-turn-into-an-eight-lane-road),
[Kathmandu Post editorial on the median, 2018](https://kathmandupost.com/editorial/2018/05/29/about-time),
[Heritage flair for Thamel-Basantapur road](https://kathmandupost.com/valley/2018/11/17/heritage-flair-for-thamel-basantapur-road),
[KMC laying stones on 33,000 m2 (2025)](https://ekantipur.com/national/2025/02/21/en/kathmandu-metropolis-laying-stones-on-about-33-thousand-square-meters-52-35.html).

## 8. Open issues (outside this package)

* **Physics and traffic on the drawn surface** (collide, traffic). `RoadSpatialIndex`, `TileGroundQuery`, `LaneGraph`,
  `PedestrianSim` and `ParkedPlacement` still read the §4.2 width (`WidthAt`, `MaxWidth`, `CarriagewayM`) about the
  raw polyline, `LaneGraph` and `RoadSpatialIndex` with their own dual-carriageway shift (`−0.5 (w − real)`), so they
  miss what is drawn: gallis drawn 4.8 m wide where the index is 3-4 m, the drawn edges of unconnected parallel pieces
  stopping at their shared gap (the §4.2 width is wider than the drawn one at about 7 % of the width samples on the
  busy sample tiles, two thirds of them by more than 1 m: mostly the one-way pairs and the Ring Road service
  carriageways, whose lanes can therefore reach past the drawn edge onto the kerbed strip), the 1 m pushes and
  RSTR corridor shifts (`RoadWidthProfile.ShiftAt`), junction caps, rings and the smoothed, banked vertical profile.
  Stand bodies on `Roads.RoadSurfaceQuery.For(tile, sampler, options).TrySample` (height, normal, carriageway /
  footpath / median / island / shoulder / junction / deck), build the driving index from `DrawnAt` about `ShiftAt`,
  and lay lanes across the drawn width about `ShiftAt` (the access masks stay on the §4.2 width). Before lanes move,
  `TrafficSim.Room` must also check the successor and predecessor lanes: it only looks at the spawn lane and its
  twin, so a vehicle can be spawned onto a lane end whose last body has just moved onto the next connector (found
  while trying lanes on the drawn width: `TrafficTests.RingRoadRunsHalfAnHourWithoutContact` then counts that pair
  for a minute; with the check it fails on a junction conflict elsewhere instead, so the 30-minute run is sensitive to
  any lane geometry change).
* **Terrain under the roads** (nature). `TerrainMesher` must cut the lowered underpass stretches
  (`RoadSurfaceQuery.HasLowered` / `TryCut`), and bench hill roads: cut and fill the terrain mesh to the drawn surface
  along them so the road can lie level across (the comfortable 25 % `RoadGrade.MaxBank`) instead of leaning with the
  hillside up to 45° (`MaxSteepBank`, which keeps it from floating on the 30 m DEM today).
* **Corridor shift and passages from the record** (integration / data). Wire `RoadStructureRecord.CorridorShiftCm`
  (DATA_FORMATS 1.15) into `RoadLayout.CorridorShiftOf` (until then only `RoadLayout.WithCorridorShifts`, used by the
  tests, sets shifts), and replace `RoadLayout.PassageKind` with `RoadStructureKind.Passage` when the data package's
  enum lands (ENUMS_VERSION 4; the numeric value is the same).
* **Police podiums** (integration). `RoadOptions.PolicePodiums` stays off wherever the ornaments package's
  `RoundaboutDecorator` runs (it draws the podium, drum or umbrella of every police island); turn it on only for a
  build without that decorator.
* **Residual overlaps** (roads, next pass). Unconnected parallel carriageways still overlap on about 760 m of edge
  samples over the whole sample (0.04 %), at bends where the two pieces take different fillets, and about 12 m of
  footway centreline lies inside a carriageway (stretches too short or too close to a tile cut to absorb).
* **Footpath colour by way** (roads / look). Paver colour (red or grey) is picked per OSM way, so a footpath changes
  colour where a street changes way id, at a junction cap between two ways and where a mapped sidewalk is absorbed;
  a per-street (name or corridor) choice would keep one colour along a street.
