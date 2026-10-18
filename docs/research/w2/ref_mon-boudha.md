# Reference brief: Boudhanath (`mon-boudha`)

The modeller's per-object brief for the bespoke Boudhanath site (W2 monuments round). The research behind it, with every
source, OSM id and confidence tag, is `docs/research/heritage/boudha.md`; this file lists, per modelled object, what a
Kathmandu local recognises it by (silhouette, proportions, colours, details) and the photos each part was checked
against. Photos are reference only, never copied into the repository; local copies are under
`/home/user/wt/refs/mon-boudha/` with their credits in each folder's `credits.txt` (and the table in the heritage brief
§9). Renders, render|photo comparisons and the scripts that made them are under `/home/user/wt/previews/mon-boudha/`
(rounds `r1`..`r10`; `r9/streamed` is the in-situ scene as the streamer builds it; see the round log at the end).

Code: `game/Assets/Ghumante/Core/Generators/Sacred/Heroes/Boudha/` (one file per structure group). Site frame: `u`
toward 99° (east, turned 9° clockwise with the stupa's axes), `v` toward 9° (north), origin the stupa axis (the T1
centroid of OSM w56688295 + (-0.17, 1.85) m), heights above the kora (the flattened plaza level, `BoudhaGround`).

## Parts, LODs and budgets

The site is six heroes so each part picks its own LOD by its own screen height (0.35 / 0.12 / 0.04): the Great Stupa
(`her.ktm.boudhanath`, the curated record) and five parts that hang off it through `HeroRecipe.SiteOf`
(`.north`, `.east`, `.south`, `.west` kora quarters and `.gate`). Ceilings (LOD0 / 1 / 2 / 3 triangles; LOD1 about
30 %, LOD2 about 8 % of LOD0; the LOD0 ceilings stay within the site's 150,000 on High):

| Part | Holds | Ceiling | Built (sample tile) |
|---|---|---|---|
| Stupa | terraces, ring, dome, harmika, spire, crown, flags, terrace chortens and statues, stairs | 51,000 / 14,500 / 3,840 / 240 | 50,494 / 14,473 / 3,422 / 116 |
| North | wall N, Ajima + gate pavilions + NE house, Guru Lhakhang + bell, 11 houses, lamp houses, garden | 24,000 / 7,200 / 1,960 / 360 | 23,986 / 7,086 / 1,923 / 253 |
| East | wall E, 13 houses (Cafe Terrace roof), trees, kiln | 17,500 / 5,250 / 1,460 / 360 | 15,211 / 4,085 / 1,427 / 319 |
| South | wall S + south gate pavilion and kiln, 10 houses, lamp houses | 17,000 / 5,100 / 1,400 / 360 | 15,599 / 4,018 / 1,359 / 297 |
| West | wall W, Jamchen, Dhabzang, 11 houses, benches, kiln | 23,500 / 7,050 / 1,880 / 360 | 23,076 / 5,703 / 1,788 / 332 |
| Gate | main gate on Boudha Main Road | 9,500 / 2,850 / 760 / 120 | 9,260 / 1,596 / 610 / 36 |

The LOD0 ceilings add up to 142,500 (built 137,626).

The stupa's LOD0 is kept at 51,000 (not the 60,000 a major temple may take) because the streamer's High hero slice
is 60,000 triangles for everything in view: the allocator steps the smallest-on-screen heroes down first, so with a
60,000-triangle stupa every quarter fell to LOD3 whenever the stupa was in view. With 51,000 the stupa and all five parts
at LOD2 still fit (58,380); the aerial view keeps the stupa at LOD0 with the two nearest quarters at LOD1, and from the
kora the near quarter keeps LOD0 beside a LOD1 stupa (the far quarters still drop to LOD3; see the open issue on the hero
slice). The 3,000 over the earlier 48,000 carry the 64 prayer-flag lines (four a bundle).

Silhouettes kept per LOD: LOD1 keeps every outline (flat niche panels, ramps for the stair flights, windows without
hoods, merged cornices); LOD2 keeps the stupa's profile, the gilt spire and canopy, the chortens as lathes of 5-6 sides,
the wall as faces with dark niche panels, houses as moulded prisms with storey bands on the kora fronts; LOD3 is a
far stand-in (stupa lathe, house boxes). Colliders (LOD0 only): walkable terrace boxes (per terrace the body as a true square and the
two arms end to end) and stair ramps, the kora slab in boxes, walls, house and gompa walls, statue and kiln cylinders;
the ring as two cylinders (Ø 38.0 to 11.25, Ø 35.5 to 12.0), the dome as a third. Vertex UV0 carries the material channel (u) and the
baked AO (v) like the rest of the detail pass.

## B-01 Great Stupa (`BoudhaStupa.cs`)

* **Recognised by:** a white hemispherical dome on three white mandala terraces with stepped (20-cornered) outlines, a
  gilt square tower of 13 steps on a white harmika painted with the blue-and-white eyes, the "१" nose and the third eye,
  a gilt canopy with a striped cloth band and a saffron skirt, and sixteen bundles of five-colour prayer flags fanning
  from the canopy to the terrace corners and the compound wall.
* **Proportions (m):** T1 81.5 × 79.4 to 4.0, T2 62.5 × 61.8 to 8.0, T3 49.8 × 50.1 to 10.5 (OSM outlines and
  photo-measured heights); base ring Ø 38.0 stepping to Ø 35.5 at 11.25, top 12.0, with 108 niches; dome Ø 33.5, apex
  23.7; harmika 7.0 wide, 23.0 → 26.1, cornice 7.7 wide to 27.6 with its valance; spire 13 steps from 6.6 to 2.4 wide,
  27.6 → 36.7; neck to 37.1; canopy drum Ø 4.4 to 38.7; parasol cap to 40.6; finial to the tip at 44.8. Stairs: north
  3.9 wide (with yellow handrails and clay flower pots), east, south and west 3.0, at the arm ends of each terrace.
* **Colours:** dome lime white `#F4F0E6`, weathered base `#D9D4C8`; the twelve saffron arcs `#E3A73A` over a yellow
  wash `#F2D27A` round the top; terrace walls white `#EEEAE0`, terrace floors `#C39A86`; stair treads `#B89484`; gilt
  `#E2B54A` (bright), `#E0BC63` (lit), `#A87C2E` (shade); eyes blue `#1F4E9A` / `#73B9E6`, brows `#1B2140`, nose and
  third eye `#D8402A`; cloth band white `#F4F2EC`, blue `#2E5DA8`, red `#C8202A`, yellow `#F2C230`; skirt saffron
  `#F0A01E`; flags in the five colours (blue, white, red, green, yellow).
* **Details:** each terrace parapet carries small white chorten finials about every 3.2 m, with gaps at the stairs;
  the ring niches hold small gilt figures in white frames with five-colour pennants between; the dome carries the
  saffron arcade (twelve arches springing at 0.23 and cresting at 0.44 of the dome's rise, on thin legs to its foot)
  under the yellow cap wash and its scalloped drips; the harmika's four faces carry the eyes; the crown is a barrel drum
  (37.1 → 40.0) banded white, blue, white, red over a folded saffron skirt, with 28 pendant diamonds, under the parasol
  cap; the four-leg gilt lantern frame (from Ø 3.4 at the drum top to 44.2) meets over the bulb finial; the flag lines
  hang in a catenary (four lines a bundle, 64 in all, with five-colour flags 0.6 wide every 1.05 m at LOD0, two lines
  with flags every 2.2 m at LOD1, one ribbon a bundle at LOD2).
* **Photos:** general3/ref_00 (spire, Prof. Mortel), tamang/ref_06 and eyes/ref_00 (eyes), spire/ref_05-06 (canopy
  frame, mini chorten and arcs), panorama/ref_02 (telephoto silhouette, Cheryl Marland), around/ref_03 (north view,
  Jorge Lascar).

## B-02 Terrace dressing (`BoudhaTerraces.cs`)

* **Recognised by:** big white Tibetan bell chortens at the terrace corners (three-step base, lotus collar, bell, small
  harmika, ringed spire, gilt parasol), four mini-Boudha chortens with eyes and a gilt 13-step spire on T1, white
  elephants with riders guarding the north and south stairs, two white Dhyani Buddhas on T2 north, a grey carved stone
  shrine on T3 south, and the two compound stairs to T1 (north through the west gate pavilion, south by the lamp
  houses).
* **Proportions:** bell chortens 5.6 tall (8: at (±18.1, 34.6), (±18.2, -34.2), (±36, 10.5 / 10), (±36, -16.5 / -16));
  mini-Boudha 7.3 tall on a 4.4 square base, its dome Ø 3.9 rising 1.85, harmika 1.5 wide and 0.95 tall with the eyes
  at 1.5 times the old size, the gilt spire 1.32 → 0.72 wide over 2.3 m (at (±29.4, 26.4) and (±29.4, -32.6), round
  solids Ø 4.1); elephants 1.55 on a 0.35 plinth at
  (±3.6, ±37.5); Buddhas 2.4 on their pedestals; stone shrine 2.4.
* **Colours:** white `#EEEAE0` with shade `#D9D4C8`; elephant saddle cloth red, harness dots red / blue / yellow / green;
  rider robe `#E07A2A`; Buddha robe `#F6F2EA`, ushnisha `#3A5BA0`; stone `#8E8A82`; compound stair treads `#B0A79C`.
* **Photos:** corner/ref_06 (T1 bell chorten and statues), tamang/ref_08 (elephant), earthquake/ref_06-07 (stair with
  pots, stone shrine), spire/ref_06 (mini chorten).

## B-03 Compound wall and kora (`BoudhaWall.cs`, `BoudhaQuarter.cs`)

* **Recognised by:** a low white wall round the compound, its outer face opened by a continuous run of dark niches with
  copper prayer wheels under yellow valances with thin red and white stripes, small white chortens on the piers, tall
  darchor poles with vertical five-colour banners, and the kora outside: a grey flagstone path along the wall, a kerb,
  and red brick paving out to the house fronts.
* **Proportions:** wall line r 45-55 from the axis (OSM), 312 m long, 2.6 high, 0.7 thick; 147 niches 1.3 wide, 0.55
  tall on a 0.9 sill (each quarter part draws and counts its stretch); 8 darchor poles 9.0 + 0.45 tip at the intermediate
  bearings; north opening 15.2 wide (Ajima and pavilions), south pavilion 4.0 wide and 2.4 deep set into the compound
  with a closed carved door, a white fan over it and two lamp niches (corner/02); flag path 3.6 wide; brick out to the
  first house front (max r 79, 4 m into lane mouths); slab top 0.30 over the lifted road ribbons.
* **On the drawn kora:** the road package draws the kora street up to 8 m wide (r 54-62 in the east), over the OSM wall
  line in the east, north-east and south. Nothing solid may stand on a drawn road, so the wall line is fitted back:
  each 7 m piece moves toward the axis until its centre line keeps 0.55 m (half the wall plus 0.2) off every drawn edge
  (up to 2.8 m in the east bulge; `BoudhaWall.Fit`). The slab colliders cover only what lies off the drawn road; the
  juniper kilns, the west benches and the kora objects standing on the drawn street are drawn but walk-through; the NE
  lamp houses step in toward the stupa until their plans clear it.
* **Colours:** wall lime white `#EEEAE0`, base course `#D9D4C8`, coping `#F7F4EE`; niche recess `#2B2620`, wheels
  copper `#B0743A` / brass `#C9A24A` in shade; valance `#E8B530` with `#C8202A` and `#F4F2EC` stripes; flags
  `#8C8A86`, kerb `#6E6A64`, brick `#9C5A40`; compound ground `#9A968E`.
* **Details:** lamp houses with ochre pent roofs and stepped brass-lamp tables (NE and S), juniper kilns (corner/02:
  a two-step white base, a firebox with a small sooty arched mouth under an overhanging hipped eave, a lotus collar, the
  white bulb, a harmika and a spike) east, west and at the south pavilion, the potted garden of the north-west compound, rounded trees,
  three kora benches facing the stupa on the west.
* **Photos:** commons_late/c03 (kora today), corner/ref_02 (south pavilion and kiln), spire/ref_00-01 (niches),
  kora/ref_08 (paving, benches), circle/ref_02-09 (wall and pilgrims).

## B-04 North entrance (`BoudhaNorth.cs`)

* **Recognised by:** the little Ajima (Hariti) shrine in the wall's north opening: a brick plinth with two steps, dark
  lattice screens, a gilt door frame and torana, a broad shallow gilt hip roof with standing seams, a red band and a
  yellow frill under the eaves, a gilt pendant, and a double-vajra finial; two brass prayer-wheel lanterns at the steps
  and a butter-lamp table; the two white gate pavilions either side with arched passages, gilt toranas and striped
  valances; the white NE house with a maroon frieze and a little red pagoda shrine on its roof.
* **Proportions:** plinth 0.9, plan 5.6 × 5.2, eave 3.9, roof top 5.5, vajra tip 7.1; pavilions 4.2 wide, 2.4 deep,
  3.6 tall, centred 5.5 m either side of the axis; NE house 9 × 3.8.
* **Colours:** brick `#A64B32` / `#B5603E`; lattice `#2A2018`; roof gilt `#C9A24A`, bright gilt `#E2B54A`; band
  `#C8202A`, frill `#E8B530`; pavilions white `#EEEAE0`; NE house white with maroon `#7A1F1F`, pagoda shrine red
  `#B3261E`.
* **Photos:** around/ref_03 (from the north), commons_late/c04 and c05 (Ajima from above, 2016), y2019/ref_06-08 (Ajima
  exterior), commons_ajima/c01-c02 (interior).

## B-05 Main gate (`BoudhaGate.cs`)

* **Recognised by:** a broad painted gateway over the lane from Boudha Main Road: teal piers with gold-trimmed panels of
  the eight auspicious symbols, a cusped ogee arch with a gilt edge and a band of colourful carved relief round it, blue
  spandrels with gold and teal clouds, a dark sign board on a red-and-blue frieze, a yellow roof with kicked corners, a
  white chorten between two gilt dragons on the ridge and kalash finials; white snow lions with turquoise manes on blue
  lotus caps atop white columns outside the piers; steel bollards in the opening.
* **Proportions:** opening 4.7 wide (the photo's piers are a third of the opening; the lane is drawn 4.8 wide and the
  piers stand off it), a round head springing at 2.7 to the apex at 4.95; piers 1.4 wide; frieze 5.9, eave 6.7, roof 7.25,
  crest to 8.9; lion columns 2.7 with 1.15 lions. Placed on the lane (OSM w4847306) 8.6 m back from the road edge, facing
  the road, and centred across the lane as the road mesher draws it.
* **Spandrels:** a teal dragon either side with gilt scales writhing along the archivolt's carved band from a red
  flaming jewel over the apex, cloud scrolls in the corners; the frieze a yellow strip, a teal band with gilt and white
  discs, a blue band and the dark (blank) sign board on yellow corbels.
* **Colours:** piers teal `#2E4A4E` with gold `#E2B54A` trim; spandrel blue `#2B5FA0`; relief gold, teal `#3E9C94`, red
  `#C8382E`, green `#2F8F4E`, sky `#5FA8D8`; frieze red `#B3261E` and blue `#2E5DA8`; roof yellow `#EAD35A` / `#D9B53A`;
  lotus caps `#3A7FC0`; bollards `#B8BCC2`.
* **Details:** the sign board carries no lettering (no names or logos in the game); the street's shop signs and bank
  boards are not reproduced.
* **Photos:** commons/Boudhanath_entrance_gate*.jpg (Wikimedia Commons), gate2/ref_04 (the older gate, Wonderlane).

## B-06 Gompas on the kora (`BoudhaGompas.cs`)

* **Guru Lhakhang (north, on its OSM footprint):** an ochre two-storey front over a deep portico of four red columns
  with blue-green capitals and red cloud brackets under a banded beam (red, gilt, blue, green); the portico's back wall
  painted with the Four Guardian Kings either side of a gilt-framed door; Tibetan windows with black trapezoid
  surrounds, red frames and frilled awnings; a maroon frieze with gilt mirror discs under a dark cornice; on the roof a
  white pavilion under a gilt Chinese hip roof with dragon corners, a gilt victory banner, and the gilt Dharmachakra with
  two kneeling deer on the front parapet. Top 15.05 m. **The Big Bell** in front: a silver-bronze bell in a gilt
  horseshoe arch on two gilt posts on a stone base (3.5 m). Colours ochre `#E3B84E`, maroon `#7A1F1F`, columns
  `#B3261E`, gilt `#E2B54A`, bell `#A9A39A`. Photos: bell/ref_09 (Cheryl Marland), gompa2/ref_00-01 (petmyrhino),
  commons_more/c01 (gilt dragon roof), commons_more/c04 (roof ornaments from the north).
* **Jamchen Lhakhang (west):** a marble plinth (0.6) and green steps; a tall portico (beam top 5.2) of maroon columns
  and corner pilasters with white lattice screens and yellow curtains in the door, a red valance hanging across each
  bay with gilt cusps and medallions, a row of painted block ends, and a deep dark eave sloping 1.3 m out over it with a
  navy soffit and a gilt edge; the upper storey set back 0.45 behind a gallery of white lattice panels with blue tracery
  in red frames over a maroon rail under a gilt band; a gilt roof with dragons and three gilt banners. Top 12.2.
  Colours maroon `#9E2430`, valance `#A3221C`, eave `#343A44` / soffit `#1F2C46`, curtains `#F2C230`, screens `#F0F0EC`,
  lattice `#EDEFF2` / `#3F6FA8`. Photo: commons_late/c01.
* **Dhabzang (north-west):** white with a maroon frieze and mirrors, Tibetan windows, a small gilt-roofed pavilion on the
  roof. Top 13.1.

## B-07 Ring houses (`BoudhaRing.cs`)

* **Recognised by:** an unbroken ring of 3-6 storey houses on their real OSM footprints (46 houses; 49 front-row ways
  including the three gompas), shops on every kora front (rolled-up shutters, goods hanging inside, a counter of brass,
  a signboard and a striped awning), painted plaster in ochre, salmon, cream, pale green, sky blue, pink and white or
  exposed brick with carved dark windows, white Rana pediments over green-shuttered windows on the ochre fronts, a few
  red clay-tile hip roofs, roof terraces with cafe umbrellas, black water tanks, solar heaters, stair huts and banner
  poles.
* **Proportions:** plinth 0.15, shop storey 3.4, upper storeys 3.0, parapet 1.0, hip roofs rising 2.0 with 0.45 eaves;
  bays about 3.1 m; windows 1.1 × 1.4; awnings 0.95 deep.
* **Colours:** ochre `#E8C35A`, deep ochre `#E3B84E`, brick red `#B5442E`, exposed brick `#A64B32`, salmon `#E39A7A`,
  cream `#F1E6CC`, pale green `#BFD8B0`, sky blue `#A9C9DE`, pink `#E7A9A0`, white `#F2EFE8`, orange `#E58A3A`; trim
  white `#F4F1EA` or dark wood `#5A3422`; roof tiles `#A04A34`; slab `#A9A49A`.
* **Details:** every front is classified from the footprint (a face toward the stupa is a shop front, a face on a lane
  a side front, a party wall against a neighbour stays plain); signboards are coloured panels with a plain text bar
  (no lettering or brands).
* **Off the drawn kora:** where the drawn kora street or a lane overlaps an OSM outline (up to 3.8 m in the east row),
  the house's corners slide back along its party walls until its front keeps 0.3 m off the drawn edge
  (`BoudhaRing.OffRoads`; most houses keep 75-99 % of their plan). A house left with less than 8 m² or 35 % of its plan
  is not built (w912264741, a 17 m² sliver at the east lane mouth).
* **Photos:** commons_late/c03, gate2/ref_00 (ring houses from the stupa), bell/ref_09 (the ochre house east of the
  Guru Lhakhang with green windows and white pediments).

## Photo-matched viewpoints (render | photo)

Unity coordinates in tile 10/521/163 (kora level 1331.3; script `round.py` in the previews' scripts folder renders
every view with each part at LOD0, or at the LOD the streamer picks on High):

| View | Eye | Look at | fov | Photo |
|---|---|---|---|---|
| North rooftop (Guru Lhakhang front parapet, on the axis) | 770.8, 1344.5, 237.7 | 761, 1336, 175.5 | 80 | around/ref_03 |
| SW rooftop | 706, 1356, 126 | 742, 1338, 190 | 65 | corner/ref_04 |
| Main gate from the road | 737.5, 1332.6, 84.0 | 741.0, 1335.5, 97.0 | 60 | commons/Boudhanath_entrance_gate |
| Kora at the south wall | 753, 1333.2, 119.5 | 761, 1347, 175.5 | 68 | corner/ref_02 |
| Guru Lhakhang and bell (from the east gate pavilion) | 774, 1336.5, 222 | 771, 1336, 240 | 55 | bell/ref_09 |
| Aerial 45° from the SE | 820, 1420, 110 | 761.8, 1340, 176.4 | 50 | corner/ref_05 |
| Dome and spire from a south rooftop | 748, 1354, 102 | 761, 1352, 175.5 | 38 | dome/ref_00-07 |

## Branding and sensitivity

No names, logos or brand marks anywhere: shop signs, the gate's board and the notice board are blank coloured panels.
The eyes, the Buddhas and the gompa ornaments follow the photos without invented iconography.

## Round log (render | photo, `/home/user/wt/previews/mon-boudha/`)

| Round | Views | Fixed after looking |
|---|---|---|
| r1-r5 | all photo views | first build of every part; plaza level, terraces, ring houses, gate, gompas |
| r6 | dome, kiln, south pavilion, bell, kora, gate | dome arcs read as petals: rebuilt as an arcade; kiln and closed south pavilion to corner/02; bell arch closed into a horseshoe |
| r7 | dome, crown, gate, kora | arcade lowered; barrel crown with banded drum, skirt and lantern frame; gate frieze and round arch |
| r8 | mini chorten, Jamchen, kora, north stair, NE roof, tele | wider flags, lighter stair treads, Guru Lhakhang balcony; mini chorten and Jamchen proportions noted |
| r9 (+ `r9/streamed`) | mini chorten, Jamchen, gate, dome, tele, kora, north stair, NE roof, east kora; streamed north, SW and NE roofs, aerial, gate road, Guru Lhakhang | mini chorten re-proportioned; Jamchen portico, eave and gallery; gate opening and dragons; 64 flag lines; road clearance (wall fit, house cuts, gate centring) seen in the streamed scene |
| r10 | Jamchen, gate | Jamchen valance across the bays; gate dragons as teal relief |

