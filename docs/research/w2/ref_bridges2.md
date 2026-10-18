# Reference brief: bridges2 (playtest round, October 2026)

The owner's first Unity playtest (docs/W2_PLAYTEST_1.md) reported three things this package answers:

* **#2 (decks):** "Bridges is not walkable or drivable at all. Characters gets sucked in. Character basically disappears
  when trying to cross the bridge."
* **#2 (underpasses):** "the same bridge acts as a overpass in areas like ratopul, kalopul, setopul, pahenlo pul.. but
  none of those underpass works.. The bridge wont let us pass through."
* **#15 (foot overbridges):** "Overhead Sidewalks do not work. I tried crossing the road using that, and i could not even
  reach the top."

This round changes how two structures are built (their look follows the photographs below) and makes every structure
walkable and rideable where it is drawn. The general look of the valley's bridges, flyovers and overbridges is in
`bridges_flyovers.md` §4 and §9; this brief only covers what this round adds.

## 1. Dhobi Khola corridor under Ratopul, Setopul, Kalopul and Bhatkeko Pul

**What is there.** The Dhobi Khola (Rudramati) corridor roads run along both banks of the river from Bijulibazar to
Chabahil (Dhobikhola Corridor Improvement Project, Kathmandu Valley Development Authority, started 2002 and still
being finished in pieces [S1]). The street bridges over the river (Ratopul, Setopul, Kalopul, Bhatkeko Pul, Gaushala)
stand at street level; the riverside roads stay down near the river and pass under the bridges' end spans, so at every
bridge the corridor road dips into a cutting a few metres below the street, held on the city side by a **retaining
wall** (stone masonry or plain concrete, whitewashed or bare grey, with a low parapet or pipe railing on top) and on the
river side by the channel wall. Side streets come down to the corridor road on ramps through gaps in the wall. The
sample's data (RSTR, `docs/DATA_FORMATS.md` §1.15) carries this exactly: the corridor pieces under each bridge are
`UNDERPASS` records with heights about 3.5 m under the street, and the pieces leading down to them are `LOWERED` +
`APPROACH` cuttings (riverside roads sinking for a river bridge, step 2 of the height solve).

**Proportions used.** Corridor carriageway 6–7 m (tertiary), cutting 3–4 m deep at the bridges, wall top 1 m over the
street-level ground behind it (parapet), wall face 0.3 m thick in concrete grey `#A8A39A` (`BridgeStyle.Concrete`),
gaps in the wall wherever another road joins at the cutting's level.

**What changed in the game.** The retaining walls are one system for the whole cutting: the underpass pieces and the
lowered approach pieces chained to them (`BridgeLayout.Trenches`). They are drawn (`BridgeMesher` trench walls) and solid
(`BridgeColliders`) along exactly the same runs, and both open where another road's carriageway meets the cutting at its
level, so a side street ramping down to the corridor joins it through a gap, as in the photographs, instead of a wall
standing across the corridor road (the playtest's "the bridge won't let us pass through").

## 2. Foot overbridges: stairs and handrails

**What is there.** Kathmandu's overhead pedestrian bridges (Kanti Path at Jamal, Rani Pokhari, Bir Hospital and
Bhadrakali; Durbar Marg and Ratna Park; Putalisadak; New Baneshwor; Chabahil; Ring Road at Satdobato, Ekantakuna, Gwarko
and Koteshwor) are steel: a girder or truss deck about 2 m wide and 20–40 m long (Chabahil: 23 m by 2 m, KMC portable
steel bridge, 2020 [S2]) on steel columns, 5.5–6.5 m over the road, with **steel stairs at both ends**, usually two or
three flights with flat landings, often turning at a landing (a dog-leg or an L down to the pavement). Handrails run on
both sides of every flight and along the landings; where one flight turns into the next, the rail turns with the walking
line and is **open across the way on**. Treads are narrow steel plates (riders complain they are small and slippery
[S3]); risers about 16–18 cm.

**Proportions used.** Riser 0.165 m, tread 0.30 m on generated flights (`BridgeStyle`), mapped flights fitted to the
mapped steps (risers 0.15–0.20 m, treads 0.20–0.32 m), a flat landing every 14 steps, flights 1.2–2.4 m wide, handrail
0.95 m over the nosing line, 35 mm tube, balusters at both ends of each rail run and about every 2.4 m, steel paint of the
structure.

**What changed in the game.** A flight's handrail now stops where another flight or a landing pad meets its side (the
turn of a dog-leg, a chain of mapped steps), on the drawn mesh and on the collider alike (`BridgeSpan.HandrailRuns`). The
playtest found the earlier collider ran straight across the top of the next flight, so the walker hit a rail at every
turn and never reached the deck.

**Mapped stairs as the walking surface.** OSM maps the stairs of an overbridge as `highway=steps` ways and, often, as
the sidewalk itself climbing over the deck; the data's structure heights give each of them a straight line between the
mapped points. The bridges package draws the flights and landings fitted to those points (and down to the pavement past
a mapped end left in the air), and where it does, the road neither draws its own ribbon nor answers the ground query
(`BridgeLayout.CoversAlong`, used by `RibbonMesher`, `RoadSurfaceQuery` and `TileGroundQuery`). Before, a brick or
asphalt ramp at the structure heights lay over the flights (the photographs show bare steel treads), and past the foot
of a fitted flight the ground followed that ramp half a metre above the pavement. Other fixes in the same place:

* A landing pad at the turn of a dog-leg is cut back off the flight below it (a pad centred on the joint reached a metre
  over the top of the flight coming up to it); a flight passing just over another flight's handrail opens that rail.
* A deck end moved out of a widened road corridor (room for the landing) stops 1.4 m short of the first house straight
  on (`BridgeLayout.HouseRoom`); a house wall that still crosses a flight, a landing or a deck walkway is left out of the
  solids (`TileSolids.OpenFootprints`); the house is drawn by the buildings package as before.
* The ground query measures what is overhead from the deck, flight or landing the body steps onto, and a piece's slab is
  its real depth (`BridgeDeckIndex.SlabOverlaps`: a flight is 0.35 m of steel, not a 1.2 m road deck), so the next
  tread or the landing above the last tread no longer reads as a ceiling.
* A railing no longer runs on across a street at grade that meets a bridge end at an angle (a T or an L at the bridge
  head): only the approach carried straight on, or a way that climbs with the structure, is the structure's own.
* A flight inside a foot deck (a steep stretch of the mapped deck) runs along the chord of that stretch, not along the
  tangent at its top: at a bend there it ran 40 degrees off its steps, its top under the next deck (way 112448812).
* A deck ending in a flight carries the steps way that joins it there: a mapped point on that flight is the deck's, so
  the joining stair starts at the deck edge instead of turning back over its own first flight (way 1258620879).
* Where a deck is not built (a junction trim, an opening), its walking surface still carries on but nothing of it stands
  overhead as a slab.
* A flight of the deck's own has no handrail across a railing gap of the deck (the way leaving through the gap crosses
  the flight's side, overbridge 195516756), and where two mapped steps ways meet at a corner without a landing, the rail
  of the upper flight opens over the first treads of the next one (overbridge 112449582).
* A landing is cut back off a flight more than 0.3 m below it (0.45 m, the step-up exactly, still stopped walkers), and
  the cut samples the flight's walking line as well as its edges.

## 3. Rideability before and after (headless, the real walker and motorbike)

`core-tests/BridgeSampleRideTests.cs` builds the ground query the streamer builds on High (the same deck index, solids
and terrain step) over the committed `kathmandu_core` sample and drives `ArcadeVehicle` along the lines a player
follows. The same tests run on main (`64155f3`) and on this branch:

| Test (owner complaint) | main 64155f3 | this round |
|---|---|---|
| `RiversideRoadsPassUnderTheDhobiKholaBridges` (#2: Kalopul, Setopul, Ratopul, Bhatkeko Pul; 22 runs) | 19 runs stopped | 0 |
| `EveryStairFlightIsClimbedAndDescended` (#15: every flight up and down) | 44 of 135 flights (60 of 270 runs) stopped | 5 of 123 flights (8 of 246 runs), all on the data quirks below |
| `RiverBridgesAndFlyoversAreWalkedAndRiddenAcross` (#2: 57 bridges and flyovers, walk and ride) | 21 of 114 runs stopped | 0 |
| `BridgeOverbridgeClimbTests.EveryOverbridgeCanBeClimbedAndCrossed` (explicit, #15: foot to foot over each of the 15 overbridges on the drawn world) | 3 crossed | 10 crossed |

(The flight count falls because flights lying wholly on a deck, or on a deck's own end flight, are left to the deck.)
The crossing test's rider slows to 18 km/h for a bend of more than 50 degrees in the next 12 m: at the half-throttle
cruise of the straight it ran off the 70-degree turn at the head of the Bishnumati bridge.

The drawn-world probes (`BridgeCrossingTests`, explicit: every span walked, ridden, walked along its walkway and under
it; `BridgeOverbridgeClimbTests`, explicit: foot to foot over every overbridge) measure the feet against the drawn
meshes as well (a sink into a drawn surface is the owner's "sucked in").

**Known data quirks (open issues for the data package).** The flights still failing all come from structure heights
that the data's derivation gives ways which only touch a stair or a deck end: a sidewalk sharing a node with the foot of
a stair, or with the end of a deck, gets that node's height and climbs over it in a hump of flights that runs under the
deck or the stair beside it. The four ways are listed in the test (`StairDataQuirks`): the sidewalk 1429946088 through
the stair joint of overbridge 268851407 (and the 1.2 m way 1429946087 from that joint to the deck), the sidewalk
301779943 through the end of overbridge 112449571, the sidewalk 1174960072 at the foot of the steps 1174960074 (whose
foot node is mapped 1.5 m over the street), and the footway 1368711450 across the end of the road bridge 1540981400, its
steps mapped to tops 1.3 m under the deck and 2.5 m inside its edge. The explicit climb probe still finds no foot-to-foot route
over 1433527429, 112449580, 112116409 and 1268595049 (its walk graph splits into pieces at the joints of several mapped
ways; one stair foot of 1433527429 lies 0.56 m under the terrain), and stops on 268851407 at the joint above; these stay
open issues, although every flight of them outside the quirks passes the gating test.

**Trench walls under a bridge approach.** The retaining wall of a lowered riverside road stood 0.2 m out of the
approach road of the Dallu bridge where that road crosses the wall line on the bank; the wall now opens wherever a road
(its footpaths too) on the bank crosses the wall line below the wall's top.

## 4. Photographs (reference only, not copied into the repository)

New Openverse searches for this round failed (rate-limited, as in the data package's round); the comparisons use the
photographs already collected in `/home/user/wt/refs/bridges/` for the bridges package:

| File | What it shows | Credit |
|---|---|---|
| `refs/bridges/overbridge2/ref_00.jpg` | Steel pedestrian bridge in downtown Kathmandu, stairs with landing | Francisco Anzola, CC BY 2.0, flickr.com/photos/10345599@N03/12653554815 |
| `refs/bridges/overbridge2/ref_03.jpg` | Pedestrian bridge in Kathmandu, steel deck and rails | The Advocacy Project, CC BY-NC-SA 2.0, flickr.com/photos/42487558@N00/8984541407 |
| `refs/bridges/overbridge3/ref_00.jpg` | Kathmandu traffic under an overhead bridge | jwalanta, CC BY-NC-SA 2.0, flickr.com/photos/20492363@N00/4496289019 |
| `refs/bridges/river/ref_00.jpg`, `ref_01.jpg` | Thapathali bridge, street-level river bridge | bmaharjan, CC BY-SA 2.0, flickr.com/photos/21539384@N02 |

## 5. Sources

* [S1] The Himalayan Times, "Dhobikhola Corridor Project incomplete after a decade" (2016):
  https://thehimalayantimes.com/ampArticle/166026
* [S2] ShareSansar, "Construction of portable overhead bridge started at Chabahil chowk" (2020):
  https://www.sharesansar.com/newsdetail/construction-of-portable-overhead-bridge-started-at-chabahil-chowk-expected-to-be-completed-before-july-11-2020
* [S3] The Himalayan Times, "Repair all overhead bridges: DM Dangol":
  https://thehimalayantimes.com/nepal/repair-all-overhead-bridges-dm-dangol
* Kathmandu Post, "Pedestrian overpass takes shape in Babarmahal" (September 2026):
  https://kathmandupost.com/visual-stories/2026/09/22/pedestrian-overpass-takes-shape-in-babarmahal
