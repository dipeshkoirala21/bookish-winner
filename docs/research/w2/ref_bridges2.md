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

**Stair feet on the ground (second session of this round).** Every stair now ends on the pavement it serves, as the
photographs show (the last tread a riser over the footpath, never in the air, never sunk into the bank):

* A generated flight (a deck end with nothing mapped) and the return flight of a dog-leg take the smallest whole number
  of standard risers whose foot, a flight's length on, lies that many risers below the top (`StandardToGround`): a few
  rounds of "drop to the ground under the last foot" never settled on a slope, and the west stair of the Durbar Marg
  overbridge 112449587 ended 0.45 m under the terrain.
* A mapped flight whose free foot is still in the air runs on along its grade until the line meets the ground, searched
  along it (`FootOnGround`; the steps 1268595050 of the Ring Road overbridge 1268595049 ended 0.9 m over the pavement),
  and one whose foot lies under the ground (a stair moved with an extended deck end onto higher ground) ends where its
  grade line enters the bank (`LandingAlong`; the stair 1433527430 of the overbridge 1433527431 ended 0.56 m under it).
* A foot way joining a stair at its end node and carrying straight on under a flight run on past the mapped foot is
  covered there too (`JoinedCover`): its ribbon, a brick ramp up to the stair's mapped foot 1.5 m over the street, stood
  through the lower treads of the Ring Road steps 1268595061 and gave walkers coming down a step up.
* A flight clipped off a deck starts at the first point off it, up to 0.1 m past the deck edge; the deck index carries
  its top 0.15 m back at its top height (`BridgeDeckIndex.TopLipM`), so walkers reaching the top of the sidewalk stairs
  of the Tripureshwor overbridge 268851407 no longer drop through that sliver to the street.
* Steep stretches inside a foot deck are split where the mapped heights turn (a dip or a crest), so each flight runs one
  way, and a single mapped point of a foot deck that dips (or peaks) more than 0.3 m between two level neighbours is
  evened out (`FootDips`): on the deck 112116412 of the Jamal overbridge a 0.7 m dip 1.5 m before the joint with the
  deck 112116410 became one chord flight 0.7 m under that joint, so neither deck reached the other.
* The stair generated at an overbridge end is laid out before the deck's own flights, so a flight of the deck that the
  generated stair leaves from runs its treads out to the deck edge at the railing gap (the west end of 112116410 left a
  0.4 m strip of nothing between the two).

## 3. Rideability before and after (headless, the real walker and motorbike)

`core-tests/BridgeSampleRideTests.cs` builds the ground query the streamer builds on High (the same deck index, solids
and terrain step) over the committed `kathmandu_core` sample and drives `ArcadeVehicle` along the lines a player
follows. The same tests run on main (`64155f3`) and on this branch:

| Test (owner complaint) | main 64155f3 | session start (e9482ef) | this round |
|---|---|---|---|
| `RiversideRoadsPassUnderTheDhobiKholaBridges` (#2: Kalopul, Setopul, Ratopul, Bhatkeko Pul; 22 runs) | 19 runs stopped | 0 | 0 |
| `EveryStairFlightIsClimbedAndDescended` (#15: every flight up and down) | 44 of 135 flights (60 of 270 runs) stopped | 5 of 123 flights (8 runs), data quirks | 5 of 125 flights (8 runs), the same data quirks |
| `RiverBridgesAndFlyoversAreWalkedAndRiddenAcross` (#2: walk and ride; now with the owner's Prithvi Path bridge) | 21 of 114 runs stopped | 0 of 114 | 0 of 118 (59 decks) |
| `EveryFootOverbridgeIsCrossedFootToFoot` (new, gating, #15: foot to foot over each of the 15 overbridges) | - | 10 crossed | 15 crossed (one, 1174960075, on its second route: the data quirk below) |
| `BridgeOverbridgeClimbTests.EveryOverbridgeCanBeClimbedAndCrossed` (explicit, #15, the same on the drawn world) | 3 crossed | 10 crossed | 15 crossed |
| `BridgeCrossingTests.ProbeEveryBridge` (explicit, #2, drawn world: 227 decks walked, walked back, ridden, walkway, and every road under them; 915 runs) | - | 57 bad (22 sunk into what is drawn), unders 9 of 68 bad | 50 bad (10 sunk), unders 10 of 68 bad |

(The flight count falls because flights lying wholly on a deck, or on a deck's own end flight, are left to the deck.)
The crossing test's rider slows to 18 km/h for a bend of more than 50 degrees in the next 12 m: at the half-throttle
cruise of the straight it ran off the 70-degree turn at the head of the Bishnumati bridge.

The drawn-world probes (`BridgeCrossingTests`, explicit: every span walked, ridden, walked along its walkway and under
it; `BridgeOverbridgeClimbTests`, explicit: foot to foot over every overbridge) measure the feet against the drawn
meshes as well (a sink into a drawn surface is the owner's "sucked in").

**The owner's bridge.** The screenshot's bridge ("shree tripura sundari grill udhyog", a wide divided road with
railings) is Prithvi Path over the Bagmati channel at Tripureshwor (ways 225426574 and 225426579, decks 23 m long, only
0.1 m over the bank, so the rise filter of the crossing test left it out). It is now gated by name: walked and ridden
across both carriageways, feet never more than 0.03 m inside the drawn deck (drawn world: walk, walk back, ride and
walkway all clear).

**Walked on what is drawn.** Two causes of the remaining sinks were the roads layer drawing a bridge way where the
ground query did not look: the approach stretches of a bridge or flyover way beyond its deck (the way is elevated, so
no ground road, and the bridges package draws no deck there) are now walked on the surface the roads layer draws there
(`TileGroundQuery.ApproachOfStructure`, through the roads package's own `RoadSurfaceQuery`), and over a deck the
asphalt ribbon the roads layer lays on it answers where it lies above the deck within reach (`OverDeckDrawn`). On the
Ring Road culvert bridge 756183449 the walker had stood 0.33 m and 0.46 m inside the drawn carriageway on its two
approaches; now within 0.04 m. A structure way's record heights count where the bridges package does not draw that
stretch (the check is per segment, as the drawing is).

**Known data quirks (open issues for the data package).** The flights still failing all come from structure heights
that the data's derivation gives ways which only touch a stair or a deck end: a sidewalk sharing a node with the foot of
a stair, or with the end of a deck, gets that node's height and climbs over it in a hump of flights that runs under the
deck or the stair beside it. The four ways are listed in the test (`StairDataQuirks`): the sidewalk 1429946088 through
the stair joint of overbridge 268851407 (and the 1.2 m way 1429946087 from that joint to the deck), the sidewalk
301779943 through the end of overbridge 112449571, the sidewalk 1174960072 at the foot of the steps 1174960074 (whose
foot node is mapped 1.5 m over the street), and the footway 1368711450 across the end of the road bridge 1540981400, its
steps mapped to tops 1.3 m under the deck and 2.5 m inside its edge. The route over 1174960075 that starts up the
sidewalk hump 1174960072 still stops under the steps 1174960074; the next route over that overbridge crosses it. The
other routes the first session left open (1433527429, 112449580, 112116409, 1268595049, 268851407) are all crossed now
(stair feet on the ground, the flight top lip, the deck zones split and evened out).

**Trench walls under a bridge approach.** The retaining wall of a lowered riverside road stood 0.2 m out of the
approach road of the Dallu bridge where that road crosses the wall line on the bank; the wall now opens wherever a road
(its footpaths too) on the bank crosses the wall line below the wall's top.

**Open issues for other packages (found by these probes).**

* Roads: at the east end of the Ring Road culvert bridge 756183449 the drawn roads mesh lies 0.2-0.3 m over what the
  roads package's own `RoadSurfaceQuery` answers there (the query says 1296.68, the mesh 1296.9 at (527566.7,
  167203.1)); walkers stand on the query's surface, so they are up to 0.3 m inside the drawn ribbon for 3 m.
* Props: `PropPlacement.DeckSpot` stands a generated lamp (3 m pole, r 0.08 m) on foot overbridge decks wherever the deck
  has a walkway, also in a railing gap where a stair or another deck joins: on the Jamal overbridge one stands in the gap
  of the deck 112116412 where the deck 112116410 joins (529608.5, 165631.0), and the drawn-world walker turning there
  stops at it. Lamps on a deck should skip `BridgeSpan.InGap`.
* Data: the four stair quirks above (sidewalk humps through stair joints and deck ends) and the dip in the derived heights
  of 112116412 (evened out here).
* Roads/ride (minimal edits made here, to be taken over): `RibbonMesher` and `RoadSurfaceQuery` skip the stretches a
  stair of the bridges package covers (`BridgeLayout.CoversAlong`), `RoadSpatialIndex.TryNearestExcept` serves the
  layered road choice, and `MeshingRoadDetailTests` skips covered stretches.

## 4. Photographs (reference only, not copied into the repository)

New Openverse searches for this round failed (rate-limited, as in the data package's round; again in the second
session for "Kathmandu pedestrian overhead bridge stairs", "overhead bridge Kathmandu", "Kathmandu footbridge" and
"pedestrian bridge Nepal"); the comparisons use the photographs already collected in `/home/user/wt/refs/bridges/` for
the bridges package. The second session's changes are to where stairs end and meet, not to how they look: each stair
now lands on the footpath with its last tread one riser over it, as in `overbridge2/ref_00.jpg`. Before and after renders
from the drawn world: `/home/user/wt/previews/bridges2/s3_*_before_after.png`.

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
