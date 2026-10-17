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

## 3. Photographs (reference only, not copied into the repository)

New Openverse searches for this round failed (rate-limited, as in the data package's round); the comparisons use the
photographs already collected in `/home/user/wt/refs/bridges/` for the bridges package:

| File | What it shows | Credit |
|---|---|---|
| `refs/bridges/overbridge2/ref_00.jpg` | Steel pedestrian bridge in downtown Kathmandu, stairs with landing | Francisco Anzola, CC BY 2.0, flickr.com/photos/10345599@N03/12653554815 |
| `refs/bridges/overbridge2/ref_03.jpg` | Pedestrian bridge in Kathmandu, steel deck and rails | The Advocacy Project, CC BY-NC-SA 2.0, flickr.com/photos/42487558@N00/8984541407 |
| `refs/bridges/overbridge3/ref_00.jpg` | Kathmandu traffic under an overhead bridge | jwalanta, CC BY-NC-SA 2.0, flickr.com/photos/20492363@N00/4496289019 |
| `refs/bridges/river/ref_00.jpg`, `ref_01.jpg` | Thapathali bridge, street-level river bridge | bmaharjan, CC BY-SA 2.0, flickr.com/photos/21539384@N02 |

## 4. Sources

* [S1] The Himalayan Times, "Dhobikhola Corridor Project incomplete after a decade" (2016):
  https://thehimalayantimes.com/ampArticle/166026
* [S2] ShareSansar, "Construction of portable overhead bridge started at Chabahil chowk" (2020):
  https://www.sharesansar.com/newsdetail/construction-of-portable-overhead-bridge-started-at-chabahil-chowk-expected-to-be-completed-before-july-11-2020
* [S3] The Himalayan Times, "Repair all overhead bridges: DM Dangol":
  https://thehimalayantimes.com/nepal/repair-all-overhead-bridges-dm-dangol
* Kathmandu Post, "Pedestrian overpass takes shape in Babarmahal" (September 2026):
  https://kathmandupost.com/visual-stories/2026/09/22/pedestrian-overpass-takes-shape-in-babarmahal
