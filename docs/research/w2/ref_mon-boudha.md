# Reference brief: Boudhanath (`mon-boudha`)

The modeller's per-object brief for the bespoke Boudhanath site (W2 monuments round). The research behind it, with every
source, OSM id and confidence tag, is `docs/research/heritage/boudha.md`; this file lists, per modelled object, what a
Kathmandu local recognises it by (silhouette, proportions, colours, details) and the photos each part was checked
against. Photos are reference only, never copied into the repository; local copies are under
`/home/user/wt/refs/mon-boudha/` with their credits in each folder's `credits.txt` (and the table in the heritage brief
§9). Renders, render|photo comparisons and the scripts that made them are under `/home/user/wt/previews/mon-boudha/`
(rounds `r1`..`r4`).

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
| Stupa | terraces, ring, dome, harmika, spire, crown, flags, terrace chortens and statues, stairs | 48,000 / 14,500 / 3,840 / 240 | 47,790 / 14,361 / 3,518 / 98 |
| North | wall N, Ajima + gate pavilions + NE house, Guru Lhakhang + bell, 11 houses, lamp houses, garden | 23,000 / 6,900 / 1,850 / 360 | 22,552 / 6,794 / 1,823 / 253 |
| East | wall E, 14 houses (Cafe Terrace roof), trees, kiln | 18,000 / 5,400 / 1,500 / 360 | 16,017 / 4,141 / 1,444 / 352 |
| South | wall S + south gate pavilion and kiln, 10 houses, lamp houses | 18,000 / 5,400 / 1,500 / 360 | 15,706 / 3,953 / 1,307 / 315 |
| West | wall W, Jamchen, Dhabzang, 11 houses, benches, kiln | 22,500 / 6,750 / 1,800 / 360 | 21,858 / 5,386 / 1,690 / 332 |
| Gate | main gate on Boudha Main Road | 8,500 / 2,550 / 680 / 120 | 7,850 / 1,586 / 600 / 36 |

The stupa's LOD0 is kept under 48,000 (not the 60,000 a major temple may take) because the streamer's High hero slice
is 60,000 triangles for everything in view: the allocator steps the smallest-on-screen heroes down first, so with a
60,000-triangle stupa every quarter fell to LOD3 whenever the stupa was in view. With 48,000 the aerial and rooftop views
keep the stupa at LOD0 and the quarters at LOD1-2; from the kora the near quarter keeps LOD0 beside a LOD1 stupa (the
far quarters still drop to LOD3; see the open issue on the hero slice).

Silhouettes kept per LOD: LOD1 keeps every outline (flat niche panels, ramps for the stair flights, windows without
hoods, merged cornices); LOD2 keeps the stupa's profile, the gilt spire and canopy, the chortens as lathes of 5-6 sides,
the wall as faces with dark niche panels, houses as moulded prisms with storey bands on the kora fronts; LOD3 is a
far stand-in (stupa lathe, house boxes). Colliders (LOD0 only): walkable terrace boxes and stair ramps, the kora slab
in boxes, walls, house and gompa walls, statue and kiln cylinders. Vertex UV0 carries the material channel (u) and the
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
  the ring niches hold flat gilt figures; the harmika's four faces carry the eyes; the canopy's four-leg lantern frame
  meets over the finial; the flag lines hang in a catenary (three lines a bundle with five-colour flags nearly edge to
  edge at LOD0, two lines with every other flag at LOD1, one ribbon a bundle at LOD2).
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
  mini-Boudha 6.8 tall, 3.5 square base (at (±29.4, 26.4) and (±29.4, -32.6)); elephants 1.55 on a 0.35 plinth at
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
  tall on a 0.9 sill; 8 darchor poles 9.0 + 0.45 tip at the intermediate bearings; north opening 15.2 wide (Ajima and
  pavilions), south opening 4.2 (south pavilion); flag path 3.6 wide; brick out to the first house front (max r 79, 4 m
  into lane mouths); slab top 0.34 over the lifted road ribbons.
* **Colours:** wall lime white `#EEEAE0`, base course `#D9D4C8`, coping `#F7F4EE`; niche recess `#2B2620`, wheels
  copper `#B0743A` / brass `#C9A24A` in shade; valance `#E8B530` with `#C8202A` and `#F4F2EC` stripes; flags
  `#8C8A86`, kerb `#6E6A64`, brick `#9C5A40`; compound ground `#9A968E`.
* **Details:** lamp houses with ochre pent roofs and stepped brass-lamp tables (NE and S), juniper kilns (white
  chorten-shaped) east, west and at the south pavilion, the potted garden of the north-west compound, rounded trees,
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
* **Proportions:** opening 3.3 wide, springing 2.9, apex 4.35; piers 1.4 wide; frieze 5.6, eave 6.4, roof 6.95, crest
  to 8.6; lion columns 2.7 with 1.15 lions. Placed on the lane (OSM w4847306) 8.6 m back from the road edge, facing the
  road.
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
* **Jamchen Lhakhang (west):** a marble plinth and green steps, a portico of maroon columns with white lattice screens
  and yellow curtains in the door, a band of window panels, a gilt roof with dragons and three gilt banners. Top 11.0.
  Colours maroon `#9E2430`, curtains `#F2C230`, screens `#F0F0EC`. Photo: commons_late/c01.
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
