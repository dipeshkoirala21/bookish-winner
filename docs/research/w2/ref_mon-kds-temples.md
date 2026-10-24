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
| `Basantapur.cs` | T6 Narayan ruin, T10 Dabali, T28 Basantapur square (brick platform, curio market) |
| `KdsGround.cs` | the square's ground: one nearly level plaza, a level pad per structure, terrain eased round the zones (fix round) |
| `SquarePaving.cs` | `kds_square_paving`: the grey flag paving over the pedestrian square and the open zones (fix round) |
| `Kavindrapur.cs` | `kds_kavindrapur` (KAT2000, w906786393; fix round) |
| `NorthShrines.cs` | `kds_shiva_mandira_1`, `kds_shiva_mandira_2` (KAT1550/1560), `kds_kashinath` (KAT1521), `kds_nila_varahi` (KAT1522; fix round) |
| `Gorakhnath.cs` | `kds_gorakhnath_pipal`: the Gorakhnath stone shrine, the great pipal on its chautara, Pira Pati (KAT1551/1552; fix round) |

The six heroes that existed keep their ids (`her.ktm.kasthamandap`, `maju_dega`, `trailokya_mohan`, `jagannath`,
`shiva_parvati`, `kal_bhairav`); the 30 new structures (23 of the modeller stage, 7 of the fix round) are `her.ktm.kds_*` parts of `her.ktm.kasthamandap`
(`HeroRecipe.SiteOf`), added by the hero set with the curated record, each its own hero with its own LODs.

## 2. Placement

* A structure with an OSM outline in the anchor tile (10/516/161) is built in the outline's oriented box; the door
  bearing of the brief snaps onto the nearest box axis (45° steps for the octagonal Chyasin Dega). The ground is the
  structure's level pad (`KdsGround`, fix round): the plaza's level at its position (flat at 1301 m east of x -100,
  rising 1.5 m to Maru), the same in a raw tile and in the streamed world (`KdsGround.Prepared`).
* Unmapped structures use the brief's local frame (origin 27.7045 N, 85.3075 E): Ashok Binayak relative to
  Kasthamandap's outline, Pratap Malla's column relative to Jagannath's, Kotilingeshwar relative to Mahavishnu's, the
  Basantapur square relative to the Dabali's (the brief's positions are 0.5-2 m off the game's outlines; a relative
  offset keeps the neighbours right). The Sweta Bhairav screen finds the west wall of w1415152806 and hangs on it.
* Ashok Binayak stands 1.4 m from the corridor of path w1415316909 at the brief's point; it is nudged (up to 3 m) until
  its cage is clear.
* Free-standing pieces far from their temple's footprint stand on the ground at their own spot: Trailokya's Garuda
  pedestal sits on the levelled square 10 m out.

## 3. Roads (contract §1)

The drawn corridors (at least 4.8 m) of the square's mapped paths cross or touch 12 of the outlines (Kasthamandap,
Maju Dega, Trailokya, Laxmi Narayan Sattal, Silyan, Bhagwati, Narayan ruin, Kageshwor, Mahavishnu, both Shiva temples,
the bell). The temples keep their OSM plans; everything else gives way:

* eaves (fix round): a heritage tier keeps its measured eave height and is never raised. An eave that would hang under
  the corridor's clearance is shortened, the overhang pulled back toward the wall in 15 % steps, never below 60 % of
  the measured overhang; whatever still hangs into the corridor is recorded (`KdsCtx.HangResidualM`) and listed as a
  corridor-trim open issue (§7). The clearance follows the corridor's class: 4.5 m + margin over vehicle roads, 2.6 m
  + margin over footways, paths, steps and pedestrian ways (`KdsCtx.MinHangY`). Valances stop at the clearance;
* stairs shorten their run out of a corridor (Trailokya's west stair, Chyasin's stair, Saraswati); the platform at
  Maju Dega's stair foot is dropped if a corridor runs over it;
* fences, railings and compound walls are trimmed at both ends where they enter a corridor (Kasthamandap's picket
  fence, the Narayan ruin's wire fence, Mahendreshwar's compound, Kotilingeshwar's rail);
* the Garuda keeps its real place on Trailokya's stair axis (fix round) and only steps along the axis off a drawn
  path's surface; path w1550144348's corridor runs over it (a corridor-trim open issue for roads, §7);
* the Basantapur platform is paved wall to wall (fix round: the footway corridors crossing it no longer cut holes);
  no stall stands within 1.6 m of a drawn road or a vehicle corridor.

## 4. Budgets

Per-structure ceilings by class (LOD0 / LOD1 / LOD2 / LOD3): major temples 64 k / 20 k / 6 k / 220, temples
34 k / 11 k / 3.2 k / 220, small structures 16 k / 5.2 k / 1.6 k / 220, the square 30 k / 9 k / 2.4 k / 220; the whole
site at LOD0 at most 150 k, LOD1 at most 40 % and LOD2 at most 12 % of LOD0 (`GeneratorsSacredKdsTemplesTests.SiteFitsItsBudget`).
Minor parts (Jagannath's corner shrines, the small shrine at Mahendreshwar, the side domes of Kal Bhairav, small
finials, the Nandis, Silyan's griffins and posts, the lions beyond LOD0) are drawn one LOD coarser than their structure.
Beyond LOD0 the kit drops relief that no longer reads: black windows become a flat frame and dark panel, posts plain
square shafts, picket rails three-faced and the stakes flat boards, Silyan's scalloped crest one continuous band,
Chyasin's columns a three-band lathe, Shiva-Parvati's arches four segments, stall mats flat.

Measured (sample tile 10/516/161, `KdsScratchProbe`/`SiteFitsItsBudget`, fix round):

| Structure | LOD0 | LOD1 | LOD2 | LOD3 |
|---|---|---|---|---|
| T1 Kasthamandap | 10 892 | 3 750 | 1 624 | 58 |
| T14 Chyasin Dega | 9 868 | 3 501 | 725 | 112 |
| T18 Jagannath + corner shrines | 9 840 | 3 127 | 1 607 | 42 |
| T17 Kal Bhairav | 8 893 | 4 193 | 772 | 24 |
| T4 Maju Dega | 8 366 | 2 462 | 868 | 58 |
| T7 Shiva-Parvati | 8 329 | 2 884 | 813 | 26 |
| T8 Trailokya Mohan + T9 Garuda | 8 180 | 3 008 | 870 | 58 |
| T2 Silyan Sattal | 6 742 | 2 267 | 463 | 26 |
| T28 Basantapur square + market | 6 490 | 1 728 | 622 | 38 |
| T25 Mahendreshwar + compound | 6 374 | 2 942 | 541 | 42 |
| T26 Bhagwati (range + temple) | 5 790 | 2 263 | 455 | 74 |
| T23 Kageshwor | 5 576 | 1 949 | 333 | 42 |
| Kavindrapur (fix round) | 5 210 | 1 454 | 505 | 48 |
| T24 Mahavishnu | 4 451 | 1 779 | 482 | 58 |
| T21 Gopinath | 4 182 | 1 461 | 487 | 58 |
| T11 Laxmi Narayan Sattal | 3 826 | 1 415 | 391 | 42 |
| T15 Big Drums | 3 580 | 1 202 | 408 | 26 |
| Gorakhnath shrine, pipal, Pira Pati (fix round) | 3 572 | 1 068 | 488 | 53 |
| T22 Kotilingeshwar | 2 738 | 1 774 | 604 | 44 |
| T3 Ashok Binayak | 2 614 | 1 529 | 344 | 26 |
| T20 Indrapur | 2 500 | 941 | 365 | 42 |
| T12 Taleju Bell | 2 058 | 1 608 | 376 | 26 |
| T16 Lakshmi Narayan | 2 052 | 1 062 | 331 | 42 |
| Kashinath (fix round) | 1 976 | 986 | 315 | 42 |
| T13 Saraswati phalcha | 1 812 | 809 | 223 | 26 |
| T6 Narayan ruin | 1 738 | 560 | 166 | 20 |
| Shiva Mandira 2 (fix round) | 1 686 | 970 | 315 | 42 |
| Shiva Mandira 1 (fix round) | 1 678 | 970 | 315 | 42 |
| T30 Shiva temple (N) | 1 434 | 771 | 291 | 42 |
| Square paving (fix round) | 1 322 | 526 | 138 | 70 |
| T5 white shikhara | 1 072 | 714 | 222 | 16 |
| T31 Shiva temple (W) | 948 | 717 | 261 | 42 |
| T19 Pratap Malla column | 856 | 504 | 254 | 28 |
| T27 Sweta Bhairav screen | 758 | 362 | 10 | 10 |
| Nila Varahi (fix round) | 720 | 315 | 121 | 26 |
| T10 Dabali | 416 | 148 | 52 | 52 |
| **site** | **148 539** | **57 719 (39 %)** | **17 157 (11.6 %)** | **1 523** |

The fix round added seven structures, the square's paving, Basantapur's market runs, the deity struts, the drums'
cradles, the bell pavilion's pillars and the eave bells (about 38 000 triangles at LOD0 over round 7's 149 380) and won
them back where nothing reads at the distance the LOD is drawn: Basantapur's goods 25 low-sided pieces a table (three-sided
figures, four-sided bowls) and a single cloth quad per run at LOD3; one quad per valance pleat (pitch 0.8 m, folds
alternating tone) and a pale band once every three pleats; tile courses only on roofs of 6.5 m and more; roofs, struts
and finials under 2.6 m half width drawn one LOD coarser; small finials a six-sided custom lathe; eave bells only on
eaves of 4.5 m and more, twice as far apart; black windows without bars under 0.3 m²; posts and pickets with fewer
mould rows; the deity struts' heads as boxes; Chyasin's columns one lathe; Kal Bhairav's and the Garuda's segment
counts; the side faces of the sanctums and Shiva-Parvati's side bays one LOD coarser; the paving on a 5 m grid.

Rounds 6 and 7 added the Garuda figure, Chyasin's struts, cornice and niches, Jagannath's portal surrounds and beast
heads, Silyan's crest caps and knobs, Shiva-Parvati's couple, Kasthamandap's bronze finial and Basantapur's goods (about
4 000 triangles) and won them back where nothing reads: balusters and railing bars draw only their front and back
faces (the gallery has a dark backing panel behind them), Silyan's knobs are four-sided cones, Chyasin's niches are
arch panels and its 24 columns turn on five sides, Kasthamandap's eave bells are gone (the 2021 photos show valances
only, KAT1970 04, 13), each stall carries three goods.

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

Round 6 (sheets in `/home/user/wt/previews/mon-kds-temples/r6`, the starting state in `r6a`, steps `r6b`-`r6d`;
Garuda close-ups `r6e_garuda.jpg`, `r6f_garuda.jpg`):

| Structure | What the photos showed against r5 | Change |
|---|---|---|
| T14 Chyasin Dega | KAT1810 04, 05, 13: the octagonal roofs run straight to the eave with no upturn; carved struts under every roof; a moulded cornice over the colonnade; blind niches with carved frames on the upper walls | Roofs straight (`PolyRoof` lift 0.012 of the eave), struts on all three tiers (`PolyStruts` 3/2/1 a side), cornice mould ring, niches with two arch panels and a sill on each face of tiers 2 and 3; first valance 0.34 m |
| T4 Maju Dega | KAT1910 10, 12: the stair runs between thick white parapets with half-round copings and a newel at the foot | Stair 2.1 m wide between 0.44 m parapets; `KdsKit.StairCoping` sweeps the half-round white coping down each parapet to a domed newel |
| T2 Silyan Sattal | KAT1990 02, 06, 10: the white crest is a row of merlons with caps and small knobs over half-round scoops, a dark beam band behind it, piers at the corners carrying gilt lions | Storey heights re-measured (gallery 5.2 m, eave 7.5 m, ridge 10 m); merlons every 1.2 m with cap slabs and knobs; beam band at the back; piers to the gallery; 1.15 m gilt lions |
| T7 Shiva-Parvati | hires c02, c_shivaparvati 06: the couple at the centre window are white figures with tall white crowns banded in gilt, in a carved arched window; the lions on the stage are dark grey stone | Couple and window rebuilt; lions recoloured #6A665F stone |
| T3 Ashok Binayak | KAT1930 03, 05: the base, walls and door are dull aged brass; only pilasters and lattice keep a little gilt | Aged brass #8E763C (Metal) on base, walls and door; aged gilt on pilasters and lattice |
| T18 Jagannath | KAT1740 elevation east, 05: brick walls 2.3-4.75 m, a carved cornice with a row of small beast heads to 5.45 m, the strut zone with three carved windows a side; each portal a dark carved surround holding door and two blind arches, lanterns on brackets; corner shrines with a corner block, a pedestal with a cornice, a small cella and a first roof broader than the pedestal | Wall, cornice and strut zone at the elevation's heights; beast heads (LOD0); portal surrounds with stepped frames, door 1.1 × 1.85 m, blind arches 1.9 m, lanterns; struts 4 a side; corner shrines rebuilt to the elevation (top 6.3 m) |
| T1 Kasthamandap | KAT1970 04, 06: the gallery railing is a close row of thin balusters against the dark gallery | Balusters every 0.32 m against a dark backing panel (front and back faces only) |
| T9 Garuda | KAT2020 03, 18, 19: a man-bird kneeling on the left knee, right knee raised, hands joined before the chest, a crown with a front medallion, earrings, a serpent necklace; two wings of long feathers fanning out behind the arms and down to the statue's block; a man-high brick pedestal under a thick dark stone cap | `KneelingGaruda` replaces the generic figure: block, two-segment legs, torso, arms with joined hands, head, crown, medallion, earrings, necklace, three tiers of scalloped feather outlines per wing (two at LOD1, one at LOD2) |

Round 7 (sheets in `r7c` and `r7f`, Garuda `r7f/garuda.jpg` and `r7g/garuda.jpg`; in situ `situ_r7`):

| Structure | What the photos showed against r6 | Change |
|---|---|---|
| T1 Kasthamandap | KAT1970 13 (close of the top): a big dark bronze finial on the apex, bell, collar, onion and rings with a gilt drop, about 2.8 m tall; the 2021 eaves carry only the valances; the new fence is pale, unweathered timber | `BronzeFinial` rebuilt to that profile (apex 17.7 m, top 20.3 m); eave bells removed; picket fence #C7B59A |
| T5 White shikhara | c_shivaparvati 14, KAT1910 10: a small porch slab on two timber posts before the niche door; a white kalasha with a gilt tip on the spire | Porch slab and posts; white kalasha lathe and gilt cone; top 7.0 m |
| T28 Basantapur square | c_basantapur_chowk 00, c_dabli 05: every mat and table carries goods (statues, singing bowls, boxes), the platform's brick is uneven in tone, a grey stone kerb edges it | Three goods on every stall (cones, low bronze boxes, upright coloured boxes); every brick cell jittered in tone; a grey flag kerb band on the platform's open edges |
| T9 Garuda | r6f in situ: the pedestal and the swept wing tips touched the margin of path w1550144348 at the true spot; KAT2020 03: the crown is a broad tiara about as tall as the face, the chest broad | The pedestal back to 1.24 × 0.96 m under a 1.4 × 1.12 m cap; placement checks the whole group (pedestal corners, wing tips, wing middles) against the corridor margin; wings less swept (0.34 rad); head and chest broader, crown 0.29 m |
| T25 Mahendreshwar, T28 mats | budget | Railing bars and mat goods trimmed to stay inside 150 k (see §4) |

Photo credits: DANAM (Digital Archive of Nepalese Arts and Monuments, Heidelberg Academy) images CC BY-SA 4.0, photos
Thomas Schrom 2023 and others as captioned in `danam/<KAT>/captions.txt`; KVPT drawings courtesy of the Kathmandu
Valley Preservation Trust; Wikimedia Commons files listed with author and licence in each `c_*/credits.txt`.

## 6. In situ (TileBuild path)

`GeneratorsSacredKdsTemplesPreview.DumpInSitu` writes the whole tile through the streaming build (terrain, roads, areas,
buildings with the hidden generic houses removed, heroes at their LODs); the brief's viewpoints K1-K10 are rendered
beside their photos in `/home/user/wt/previews/mon-kds-temples/situ_r7/` (rounds 5-7: the eye stands 1.6 m over the
walkable ground under it, as §6 of the brief asks; r4 used fixed heights and K2's eye sat in the Maru lane's road
surface; earlier rounds in `situ_r5`, `situ_r6a`, `situ_r6d`). The streamed tile's KDS heroes add up to the same
149 380 triangles as the direct build (`DumpInSitu`: tile 10/516/161, 36 heroes, 243 709 triangles at LOD0, the KDS
temples site 149 380), so the game draws these models at the site. The site reads as in the photos from each:
Kasthamandap's three roofs over the hall (K1, K2), Maju Dega's stages with the white shikhara at the stair foot (K3),
Basantapur's stalls with the Gaddi Baithak beyond (K4), Kal Bhairav with its domes and relief facing north (K6),
Shiva-Parvati's five toranas and lions (K9), the kneeling Garuda's fanned wings before Trailokya Mohan from behind
(K5), Jagannath between its corner shrines (K7). The generic houses round the square and the palace (other packages) are
what differs most from the photos.

Fix round: the judge's viewpoints K1-K9, KS, KW (`judge-mon-kds-temples-0`) and V1-V7, W1-W5
(`judge-mon-kds-temples-1`) re-rendered through the same path with the same eyes (`fix/tools/situ.py`, eye 1.6 m over
the walkable ground) beside their photos: `/home/user/wt/previews/mon-kds-temples/fix/situ4/` (overview
`sheet_fix.jpg`; tops `top_north.png`; earlier steps `fix/situ`, `fix/situ3`, part renders `fix/r1`). The square now
reads as one level grey plaza with the plinths standing on it (K1, K3, KS, NE), Basantapur as a brick platform carrying
runs of draped tables (K4, V2), Kavindrapur stands on Maru (K7b, V7), and the NE corner holds the two Shiva Mandiras,
the Gorakhnath pipal and Pira Pati (NE, W1, W4).

## 7. Open issues

* **roads/areas:** inside `HeritageZone|SacredNoVehicle` areas draw footways, paths and pedestrian ways as flush flags
  (no ribbon), and only the two vehicle lanes (the NNE bell lane, Maru to Jhochhen) as darker sett. Until then
  `SquarePaving` rises over the raised footway ribbons so the plaza reads as one surface, and the vehicle lanes stay
  brick-red ribbons across the temple field (K6, K8, W2).
* **roads (corridor trims):** the heritage tiers keep their measured eaves; where a mapped footway's corridor runs
  under an eave the overhang is pulled back at most to 60 % and the residue is left: Trailokya Mohan's NW first-tier
  eave over path w1550144348, the Garuda (which stands at its real place over the same path; its statue is exempt in
  the corridor test), Chyasin Dega's south pillar (its collider is dropped where it stands in a corridor) and
  Kavindrapur's lower-roof valance over Maru. The roads package should trim these paths at the monument outlines.
* **nature:** the OSM tree node c1b2a59fc by the Gorakhnath shrine is placed as a eucalyptus; it is the great pipal
  (*Ficus religiosa*, KAT1551 04-09), which `Gorakhnath.cs` now draws itself. Map that node to Pipal (or skip it
  inside `her.ktm.kds_gorakhnath_pipal`).
* **look:** a herringbone brick channel for Basantapur's platform (#9C4E3A, joints #6E5A4E); the platform uses the
  brick channel with per-cell tone jitter and draws the dark joint bands between herringbone fields as geometry at LOD0.
* **integration:** `HeroBuilder.PrepareGround` and its one call in `TileBuild` (before the terrain is meshed) are the
  same hook the Boudha, Pashupati and palace packages add; merge them into one `PrepareGround` that calls every
  site's `*Ground.Apply`. `KdsGround` and the palace's `KpGround` meet at 1301 m east of x -100; the north field's
  zone stops short of the Taleju plinth (the palace's ground).
* **OSM ref:** the judge named `w907048008` for the generic block on Shiva Mandira 2; that way does not exist in the
  extract (`nepal.osm.pbf`), the block is `w907048009` (`building=yes`, 4 corners at (7.1, 45.3)), which the hero now
  hides.
* Ashok Binayak's gilt reads brighter in the preview than in the photos (the game's gilt channel shades it).
* The drum skins' painted designs vary between the photos (plain to rainbow arcs); the worn buff skin with a faded red
  ring of the 2015-2024 photos is used.
* The generic houses round the square (K1's grey block, K6's white blocks behind Kal Bhairav) are the buildings and
  palace packages' and differ most from the photos.
* Kasthamandap's top is 20.3 m (the bronze finial of KAT1970 13 over the 17.7 m apex), 1.5 m above the brief's 18.8 m;
  the brief's figure is the roof apex plus a small finial, the 2021 photos show the large one.

## 8. Fix round (judge round 1, scores 6 and 5)

| # | Finding | Change (and test) |
|---|---|---|
| 1 | Tiers lifted over corridors | `KdsKit.Roof`/`FitEave` never raise a tier; pull-back in 15 % steps floored at 60 %; clearance by corridor class (2.6 m foot, 4.5 m vehicle); one fit per roof (no double pull-back). Test `HeritageTiersKeepTheirMeasuredEaves`: every roof's built eave equals its measured eave, Kasthamandap's first eave 4.5 ± 0.1 m |
| 2 | No level pads; monuments on the lowest terrain; slopes beside plinths | `KdsGround`: plaza at 1301 m rising 1.5 m to Maru, a pad per structure, 3 m apron, terrain eased round the zones (slope 0.4 within 18 m); applied to the decoded tile before meshing (`HeroBuilder.PrepareGround`, `TileBuild`). No skirt is needed: the levelled ground meets every plinth. Tests `SquareIsLevelledUnderThePlinths`, `LevellingIsIdempotentAndKeepsTheTileOutsideTheSquare` |
| 3 | Basantapur market sparse, peach paving, green mats, umbrellas | Tables 2.4 × 0.9 × 0.5 m in runs of 3-5 along the platform with 4 m aisles, cloth skirts blue #2D4FB0 / red #B3262B (60/40), 25 goods a table (bronze, brass, copper, dark), no mats, one umbrella in some runs, no legs beyond LOD0; platform brick #9C4E3A |
| 4, 13 | Kavindrapur missing | `Kavindrapur.cs` from KAT2000 (plan 15.35 m, section 14.88 m to the tip): arcade with lattice screens under the lean-to roof (eave 2.7 m), window storey, lattice balcony, main roof (eave 8.45 m), top storey and roof (eave 11.6 m, ridge 13.35 m), three small pinnacles; hides w906786393. Test `NewMonumentsStandWhereTheBriefPutsThem` |
| 5 | Kal Bhairav | Relief colours and figure (round 5) kept; segment counts trimmed for the budget |
| 6 | Shiva-Parvati crest, plinth, lions | Gilt crest sprays along the ridge, lions on the stage, side bays one LOD coarser |
| 7 | Garuda | Wings below the shoulder to the block, swept back with scalloped edges; face and three-lobed crown; brick pedestal with a stone cap; stays at its real place (open issue) |
| 8 | Big Drums | Drums 1.3 m across lying in cradles along the bay, laced shells, fringe valance |
| 9 | Jagannath struts | Carved deity struts on the first tier (figure, halo, griffin base) at LOD0 |
| 10 | Taleju Bell | Battered white block with red dado, two white pillars with stepped capitals, ridge cap and gajur |
| 11 | Kasthamandap corner bays | Corner bays of the hall drawn |
| 12 | Chyasin pillars | Both stone pillars before the stair (kneeling figure on one, cup on the other) |
| 14 | NE corner | `NorthShrines.cs` Shiva Mandira 1 and 2 (KAT1550/1560, 7.6/7.75 m), `Gorakhnath.cs` stone shrine with six niche reliefs and the yellow cloth, the 19.5 m pipal on its chautara, Pira Pati; hides w907048009 |
| 15 | Kashinath, Nila Varahi | `NorthShrines.cs` (KAT1521 9.6 m two-roof temple on its plinth, KAT1522 Varahi shrine 4.8 m) |
| 16 | Square paving | `SquarePaving.cs` (`kds_square_paving`): grey flags over r21291455 and the open zones (temple field, Maru, the lane round Silyan, the bell lane, Basantapur's south street, the north field), cells inside building outlines left out; roads/areas open issue above |
| 17 | Holes in the Basantapur platform | Platform paved wall to wall |

Evidence-based deviations: Kavindrapur's north neighbour w1416684423 stays (a modern white house in every photo; the
temple's plan length matches w906786393 alone); the judge's w907048008 is w907048009 (above).

