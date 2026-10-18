# Reference brief: Kathmandu Durbar Square, the temples and the square (bespoke models)

Package **mon-kds-temples** (W2 monuments, modeller stage). The research brief is
`docs/research/heritage/kds-temples.md` (structure inventory T1-T31, positions, DANAM measurements, photos); this note
records how each structure is modelled from it, what had to give way to the road corridors and the budgets, and what
the render-vs-photo rounds found. Photos are reference only (DANAM, Wikimedia Commons, Openverse; never copied into the
repository): `/home/user/wt/refs/mon-kds-temples/`. Renders and side-by-side sheets:
`/home/user/wt/previews/mon-kds-temples/`.

## 1. Where the code is

`game/Assets/Ghumante/Core/Generators/Sacred/Heroes/KdsTemples/`:

| File | Structures |
|---|---|
| `KdsTemplesSite.cs` | the site table (id, OSM way, brief position, door bearing, top, class), catalogue patch (`Apply`), placement (`Locate`), dispatch |
| `KdsKit.cs` | the shared kit: looks (brief hex colours), stages and stairs, posts, walls, windows, doors, roofs on measured eaves with the crimson valance, eave fitting to corridors, struts, bells, octagonal roofs, finials (gilt gajur, white bell dome, dark bronze, white stepped pinnacle, flame-arch pinnacle), lions, fences, flame arches, bells |
| `KdsPagoda.cs` | measured roof tiers, window bands, ambulatories, stage stacks |
| `Kasthamandap.cs` | T1 |
| `MajuDega.cs` | T4 and the 0.5 m stone platform w908861401 |
| `TrailokyaMohan.cs` | T8 and the Garuda T9 |
| `Jagannath.cs` | T18 with its four corner shrines, T19 Pratap Malla's column |
| `ChyasinDega.cs` | T14 (octagonal) |
| `ShivaParvati.cs` | T7 |
| `ForecourtTemples.cs` | T16 Lakshmi Narayan, T20 Indrapur, T21 Gopinath, T23 Kageshwor, T24 Mahavishnu |
| `StoneShrines.cs` | T5 white shikhara, T22 Kotilingeshwar, T30/T31 the two Shiva temples by Kasthamandap |
| `KalBhairav.cs` | T17 Kal Bhairav (moved to w908861416, facing north) and T27 the Sweta Bhairav screen |
| `Pavilions.cs` | T2 Silyan Sattal, T3 Ashok Binayak, T11 Laxmi Narayan Sattal, T12 Taleju Bell, T13 Saraswati phalcha, T15 Big Drums |
| `Mahendreshwar.cs` | T25 with its compound, gate and the small shrine w907048005 |
| `Bhagwati.cs` | T26 |
| `Basantapur.cs` | T6 Narayan ruin, T10 Dabali, T28 Basantapur square |

The six heroes that existed keep their ids (`her.ktm.kasthamandap`, `maju_dega`, `trailokya_mohan`, `jagannath`,
`shiva_parvati`, `kal_bhairav`); the 23 new structures are `her.ktm.kds_*` parts of `her.ktm.kasthamandap`
(`HeroRecipe.SiteOf`), added by the hero set with the curated record, each its own hero with its own LODs.

## 2. Placement

* A structure with an OSM outline in the anchor tile (10/516/161) is built in the outline's oriented box; the door
  bearing of the brief snaps onto the nearest box axis (45° steps for the octagonal Chyasin Dega). The ground is the
  lowest terrain under the footprint.
* Unmapped structures use the brief's local frame (origin 27.7045 N, 85.3075 E): Ashok Binayak relative to
  Kasthamandap's outline, Pratap Malla's column relative to Jagannath's, Kotilingeshwar relative to Mahavishnu's, the
  Basantapur square relative to the Dabali's (the brief's positions are 0.5-2 m off the game's outlines; a relative
  offset keeps the neighbours right). The Sweta Bhairav screen finds the west wall of w1415152806 and hangs on it.
* Ashok Binayak stands 1.4 m from the corridor of path w1415316909 at the brief's point; it is nudged (up to 3 m) until
  its cage is clear.
* Free-standing pieces far from their temple's footprint stand on the ground at their own spot (the frame's ground is
  the lowest terrain under the temple): Trailokya's Garuda pedestal sits on the square 10 m out, where the flags lie
  higher, instead of being half buried.

## 3. Roads (contract §1)

The drawn corridors (at least 4.8 m) of the square's mapped paths cross or touch 12 of the outlines (Kasthamandap,
Maju Dega, Trailokya, Laxmi Narayan Sattal, Silyan, Bhagwati, Narayan ruin, Kageshwor, Mahavishnu, both Shiva temples,
the bell). The temples keep their OSM plans; everything else gives way:

* eaves: an eave that would hang under the overhead clearance (4.5 m + margin) over a corridor is first shortened (the
  overhang pulled back toward the wall in 15 % steps); only when even a short overhang would hang into the corridor
  (the wall itself stands at it) is the tier raised as a whole; valances stop at the clearance;
* stairs shorten their run out of a corridor (Trailokya's west stair, Chyasin's stair, Saraswati); the platform at
  Maju Dega's stair foot is dropped if a corridor runs over it;
* fences, railings and compound walls are trimmed at both ends where they enter a corridor (Kasthamandap's picket
  fence, the Narayan ruin's wire fence, Mahendreshwar's compound, Kotilingeshwar's rail);
* the Garuda moves along Trailokya's stair axis (or up to 0.5 m beside it) to the nearest spot where the whole group,
  pedestal corners and wing tips, keeps out of the corridor margin (path w1550144348 runs over its real place);
* the Basantapur paving drops every 2 m cell that touches a corridor (kerbs are drawn where it ends) and no stall
  stands within 1.6 m of one.

## 4. Budgets

Per-structure ceilings by class (LOD0 / LOD1 / LOD2 / LOD3): major temples 64 k / 20 k / 6 k / 220, temples
34 k / 11 k / 3.2 k / 220, small structures 16 k / 5.2 k / 1.6 k / 220, the square 30 k / 9 k / 2.4 k / 220; the whole
site at LOD0 at most 150 k, LOD1 at most 40 % and LOD2 at most 12 % of LOD0 (`GeneratorsSacredKdsTemplesTests.SiteFitsItsBudget`).
Minor parts (Jagannath's corner shrines, the small shrine at Mahendreshwar, the side domes of Kal Bhairav, small
finials, the Nandis, Silyan's griffins and posts, the lions beyond LOD0) are drawn one LOD coarser than their structure.
Beyond LOD0 the kit drops relief that no longer reads: black windows become a flat frame and dark panel, posts plain
square shafts, picket rails three-faced and the stakes flat boards, Silyan's scalloped crest one continuous band,
Chyasin's columns a three-band lathe, Shiva-Parvati's arches four segments, stall mats flat.

Measured (sample tile 10/516/161, `DumpStructures`, after round 5):

| Structure | LOD0 | LOD1 | LOD2 | LOD3 |
|---|---|---|---|---|
| T1 Kasthamandap | 13 708 | 4 152 | 1 551 | 58 |
| T18 Jagannath + corner shrines | 10 456 | 3 275 | 1 511 | 42 |
| T14 Chyasin Dega | 9 940 | 3 315 | 635 | 112 |
| T4 Maju Dega | 9 830 | 2 552 | 824 | 58 |
| T17 Kal Bhairav | 9 568 | 4 386 | 738 | 24 |
| T8 Trailokya Mohan + T9 Garuda | 8 852 | 2 956 | 774 | 58 |
| T2 Silyan Sattal | 8 710 | 2 477 | 455 | 26 |
| T7 Shiva-Parvati | 8 614 | 2 873 | 820 | 26 |
| T25 Mahendreshwar + compound | 6 994 | 3 110 | 541 | 42 |
| T26 Bhagwati (range + temple) | 6 882 | 2 263 | 455 | 74 |
| T23 Kageshwor | 6 428 | 2 179 | 325 | 42 |
| T24 Mahavishnu | 5 747 | 1 863 | 482 | 58 |
| T21 Gopinath | 5 688 | 1 723 | 487 | 58 |
| T11 Laxmi Narayan Sattal | 4 874 | 1 461 | 393 | 42 |
| T28 Basantapur square | 4 564 | 1 728 | 1 300 | 48 |
| T20 Indrapur | 3 648 | 1 001 | 365 | 42 |
| T3 Ashok Binayak | 3 230 | 1 537 | 344 | 26 |
| T22 Kotilingeshwar | 2 826 | 1 790 | 604 | 44 |
| T16 Lakshmi Narayan | 2 766 | 1 086 | 331 | 42 |
| T15 Big Drums | 2 756 | 1 182 | 408 | 26 |
| T12 Taleju Bell | 2 148 | 1 358 | 298 | 26 |
| T13 Saraswati phalcha | 2 140 | 825 | 223 | 26 |
| T30 Shiva temple (N) | 2 042 | 787 | 291 | 42 |
| T31 Shiva temple (W) | 1 940 | 725 | 261 | 42 |
| T6 Narayan ruin | 1 738 | 560 | 166 | 20 |
| T5 white shikhara | 1 208 | 623 | 173 | 16 |
| T19 Pratap Malla column | 856 | 504 | 254 | 28 |
| T27 Sweta Bhairav screen | 758 | 362 | 10 | 10 |
| T10 Dabali | 416 | 148 | 52 | 52 |
| **site** | **149 327** | **52 801 (35 %)** | **15 071 (10 %)** | **1 210** |

Round 5 spent about 2 700 more triangles on the Kal Bhairav relief (the figure is the shrine) and won them back where
they did not read: the cage bells of Ashok Binayak became the one bell rack the photos show, hand bells turn on ten
sides instead of sixteen, Kasthamandap's pickets stand 0.40 m apart instead of 0.32 m, and the small gilt family on
Pratap Malla's column (7.5 m up) uses six-sided figures and five serpent heads.

## 5. Structure briefs and render rounds

Each structure was rendered from four sides at LOD0 (`tools/mesh-preview/render.py`) above its reference photos in
rounds r1-r5 (sheets in `/home/user/wt/previews/mon-kds-temples/r1` .. `r5`, one JPG per structure). Colours are the
kit's (`KdsLooks`): old tile #82533F, new tile #8F5641, valance crimson #A9232C, carved timber #3E2B20, black window
frames #2A1E18, brick #A4553D (plinths #A35A3C, glazed #B5502F, ochre wash #C4532E), white lime plaster #EEEBE3, grey
stone #8C877D, gilt #D4A93A, tread flags #9A7F6C.

| Structure | Silhouette and proportions (source) | Rounds: what was found and changed |
|---|---|---|
| T1 Kasthamandap | Three roofs 22.7 / 17.4 / 10.1 m at 4.5 / 10.6 / 15.5 m; open post hall on a 21 m plinth; picket fence (DANAM KAT1970 04, 06, 12; KVPT drawings) | r2: first-roof struts removed (none in KAT1970 04, 11), Gorakhnath shrine horns off; r3: matches the photos in tier spacing and the long valances; LOD1 halved (posts, pickets, windows) |
| T2 Silyan Sattal | Shuttered shop storey under a pent roof, white scalloped parapet with corner piers and gilt griffins, open veranda, broad hip roof (KAT1990 02, 06, 10) | r2: scallops rebuilt as flat merlons between half-round scoops, piers to 5.75 m, griffins 1.3 m; r3: parapet reads at the right depth; LOD1 crest as one band |
| T3 Ashok Binayak | Brass-clad shrine under one gilt roof inside a 2.8 m brass cage with bells, big bell east (KAT1930 02, 04, 07) | r2: cage aged brass #8C7A4C, bells every 0.55 m; nudged 3.3 m clear of path w1415316909 |
| T4 Maju Dega | Nine brick stages to 6.6 m, white-plaster storeys with black windows and dark struts, white bell dome finial (KAT1910 03, 07, 12) | r2: Nandi onto the second tread inside the outline; r3: white walls confirmed by close crops of 07 and 12 |
| T5 White shikhara | White cella with niche door, curvilinear spire, amalaka and gilt tip (c_shivaparvati 14, KAT1910 10) | r4: horizontal tier mouldings added on the spire (the photos' stacked courses) |
| T6 Narayan ruin | Bare four-stage brick plinth overgrown with grass, rubble, wire fence, water tank (KAT1890 02, 03, 07) | r4: grass mats on the treads and top instead of cones; brick dulled and greyed (weathered since 2015) |
| T7 Shiva-Parvati | Long brick house-temple: five carved bays with semicircular toranas, beast-head band, red-brown upper frontage with rosettes, the couple at the centre window, one broad hip roof, five gilt finials, lions on the lower stage (c_shivaparvati 02, 06; hi-res c02) | r3: ground storey rebuilt with bays and toranas, frontage #8A4530, lions raised onto the stage; r4: beast-head bosses as blocks, rosettes lighter |
| T8 Trailokya Mohan + T9 Garuda | Three roofs over white-plaster storeys with black keyhole niches, five brick stages, kneeling Garuda to the west (KAT2020 03, 11, 14) | r3: tier spacing and plinth match; Garuda moved along the stair axis out of path w1550144348 |
| T10 Dabali | Low brick platform with stone edge (c_dabli 01-05) | r1-r3: unchanged |
| T11 Laxmi Narayan Sattal | Post arcade round a brick core of shops, broad low pent roof, brick upper storey with a bay window, hip roof and lantern (c_laxmi_sattal 00, 03) | r3: rectangle fitted out of the Maru lane's corridor and the pent roof pulled back per side (no raised roof); r4: the fit includes the corner posts' bracket capitals |
| T12 Taleju Bell | White 3.5 m block with a stair along its SSW side, two buff pillars with notched capitals, bronze bell, small hip roof (KAT1840 02, 03, 09) | r4: block and stair drawn inside the OSM outline (the bell lane's corridor runs along it) |
| T13 Saraswati phalcha | Rest platform on a stone plinth, carved posts, brick back wall with sanctum, broad hip roof (KAT1820 05, 07) | r3: matches |
| T14 Chyasin Dega | Octagonal three-roof temple, 24 columns with cusped arches on a six-step plinth (KAT1810 04, 05, 13) | r2: cusped arch heads and lintel; r3: matches; LOD1 columns three-band |
| T15 Big Drums | Ochre brick house, small door, open upper bay with two huge drums lying on their sides, painted skins aslant (c_bigdrum 01, 05, 07, 08) | r4: skins turned in (57°) and tilted up; skins toned to buff #D9CBAA with smaller painted rings |
| T16 Lakshmi Narayan | KVPT elevation KAT1780 03: plinth 0.08 H, brick wall to 0.43 H, beast-head cornice and pent ledge, first eave at 0.55 H about the plinth's width, upper storey, second eave 0.79 H | r4: rebuilt to the elevation's proportions (the first roof had sat at 0.38 H over a short wall) |
| T17 Kal Bhairav | Grey stone shrine wall with the painted six-armed relief in a black flame arch, sky-blue field, orange aureole; tiled roof with valance, a cornice slab with its own valance, three ribbed domes (KAT1770 05, 07, 08) | r2: domes pale stone, niche rim black, sky-blue field; r4: the second valance under the cornice slab; lions kept clear of the walkway |
| T18 Jagannath + T19 column | Two roofs over a brick sanctum with three doors and pink blind arches per side, four corner shrines, Pratap Malla's column (KAT1740 04, 05, 07) | r3: matches |
| T20 Indrapur | Very broad low first roof over a post ambulatory, small upper storey (c_indrapur 04, 14) | r4: first roof flattened to about 30° |
| T21 Gopinath, T24 Mahavishnu | Three roofs on stepped brick plinths (c_gopinath 00, 05; KAT1520 05, 09, 14) | r3: match |
| T22 Kotilingeshwar | Weathered sandstone cube with a dome, corner domelets, red valance at the cornice, stepped plinth, rail (KAT1530 02, 03, 06) | r4: stone warmed to buff-grey #9C9080 / #857A6C |
| T23 Kageshwor | Broad tile roof over a brick arcade, white terrace storey, white ribbed shikhara with corner turrets (c_kageshwor 00, 05, 10) | r2: terrace cube and turrets; r4: tier mouldings on the spire |
| T25 Mahendreshwar | White walls with red/yellow bands, two metal roofs with gilt edges, blue painted struts, gilt pinnacle with a gilt banner, compound with brick gate (KAT1510 02, 07, 08) | r4: the gilt banner (pataka) from the finial over both roofs |
| T26 Bhagwati | Long brick range with shops and carved windows, three-roof temple on its roof (c_bhagwati 00, 03) | r4: the range's front recedes from the street's corridor along the outline's slanting edge |
| T27 Sweta Bhairav screen | Carved lattice screen on the palace wall (c_sweta 08, c_sweta_facade 06) | r1-r3: unchanged |
| T28 Basantapur square | Raised buff-pink brick platform, rows of curio stalls on mats and low tables, few umbrellas, lamp posts (c_basantapur_chowk 00, 04; c_dabli 05) | r2: stalls in rows; r4: umbrellas rarer and plain |
| T30/T31 Shiva temples | Small two-roof brick temples by Kasthamandap (c_kastha 14) | r3: match |

Round 5 (this pass; sheets in `/home/user/wt/previews/mon-kds-temples/r5`, the starting state in `r5a`):

| Structure | What the photos showed against r4 | Change |
|---|---|---|
| T17 Kal Bhairav | KAT1770 03 (KVPT north elevation), 05, 14: the figure fills the niche: a broad, squat warrior stance on the reclining red figure, a big square blue-black face with round red-rimmed eyes, a fan crown of gilt leaves over a band of small skulls, six thick arms with gilt armlets holding a raised sword, a disc, a staff, a drum and a cup; the arch's apex and monster face sit under the valance at about 4.8 m, the sun and moon discs beside it. r4 had a small mascot-like figure with stick arms and the arch running up through the eave. | Figure rebuilt at the elevation's proportions (`BhairavFigure`: two-segment limbs with elbows and knees, hands, bands, garland, necklace, face parts, fan crown); niche height 5.2 → 4.35 m so the apex is under the eave; discs moved to 4.4 m. New test `KalBhairavFillsItsNicheUnderTheEave`. |
| T3 Ashok Binayak | KAT1930 03, 05, 07: the gilt copper roof is darker with age, moderately pitched, its corners curled up into big horns; the back wall is plain dark brick; the small offering bells hang in one iron rack on the east side inside the cage, not round the whole cage. | Roof eave 2.85 m, apex 4.0 m (was 4.4), aged gilt #A8873A, corner horns (`KdsKit.Roof(..., horns: true)`); brick back wall; `BellRack` (two rows of seven) replaces the bells on all four cage sides. |
| T15 Big Drums | c_bigdrum 03, 06: worn buff skins with a faded red ring and red rims, laced shells; no bright yellow and green rings. | Skins buff with a faded red ring; leather lacing zig-zags over the shells (LOD0). |
| T9 Garuda | KAT2020 03: the Garuda kneels on a man-high brick pedestal with a stone cap; in situ (K5) the pedestal was buried because the frame's ground is the temple's lowest corner. | The pedestal stands on its own ground; search beside the axis keeps the whole group out of the corridor margin. New test `TrailokyaKeepsItsGarudaOnTheWestAxisStandingOnTheSquare`. |
| T6 Narayan ruin | KAT1890 02, 03: weeds hang down the broken risers, not just on the treads. | Green curtains of weeds over parts of every riser. |
| T7 Shiva-Parvati | hires c02: all five ridge finials carry gilt leaf sprays. | Sprays on the outer four too (LOD0). |
| T19 Pratap Malla column | KAT1791 00: the king sits larger than his queens and sons under the serpent hood. | King 1.0 m (was 0.85), lighter family figures. |

Photo credits: DANAM (Digital Archive of Nepalese Arts and Monuments, Heidelberg Academy) images CC BY-SA 4.0, photos
Thomas Schrom 2023 and others as captioned in `danam/<KAT>/captions.txt`; KVPT drawings courtesy of the Kathmandu
Valley Preservation Trust; Wikimedia Commons files listed with author and licence in each `c_*/credits.txt`.

## 6. In situ (TileBuild path)

`GeneratorsSacredKdsTemplesPreview.DumpInSitu` writes the whole tile through the streaming build (terrain, roads, areas,
buildings with the hidden generic houses removed, heroes at their LODs); the brief's viewpoints K1-K10 are rendered
beside their photos in `/home/user/wt/previews/mon-kds-temples/situ_r5/` (round 5: the eye now stands 1.6 m over the
walkable ground under it, as §6 of the brief asks; r4 used fixed heights and K2's eye sat in the Maru lane's road
surface). The streamed tile's KDS heroes add up to the same 149 327 triangles as the direct build, so the game draws
these models at the site. The site reads as in the photos from each:
Kasthamandap's three roofs over the hall (K1, K2), Maju Dega's stages with the white shikhara at the stair foot (K3),
Basantapur's stalls with the Gaddi Baithak beyond (K4), Kal Bhairav with its domes and relief facing north (K6),
Shiva-Parvati's five toranas and lions (K9). The generic houses round the square and the palace (other packages) are
what differs most from the photos.

## 7. Open issues

* The temple field's grey flag paving (OSM r21291455, `surface=sett`) is drawn by the areas layer, not here.
* Ashok Binayak's gilt reads brighter in the preview than in the photos (the game's gilt channel shades it).
* The drum skins' painted designs vary between the photos (plain to rainbow arcs); the worn buff skin with a faded red
  ring of the 2015-2024 photos is used.
* In situ the square's ground is the areas layer's sandy beige with the mapped footways drawn as red-brown road ribbons
  across the temple field (K1, K6, K9); in the photos it is grey stone flags throughout (OSM r21291455, `surface=sett`).
  That surface belongs to the areas/roads packages; the heroes here pave only their own plinths and Basantapur's stall
  platform.
* The generic houses round the square (K1's grey block, K6's white blocks behind Kal Bhairav) are the buildings and
  palace packages' and differ most from the photos.
* Taleju Bell: the bell lane's corridor runs over the outline's stair side; the stair stays inside the outline.
