# Reference brief: city buildings of the Kathmandu Valley as they stand today (detail pass, package "buildings")

> Status: written 2026-10-10 for the detail pass (docs/W2_DETAIL_CONTRACT.md). It turns photographs of each place into
> modelling rules for the B0 house grammar (`Core/Meshing/Buildings/HouseBuilder*.cs`) and the roof and facade kit
> (`Core/Meshing/Kit/{FacadeKit,PropKit,KitRound,KitPaint}.cs`). The numbers and rules of `newar_houses.md` (storey
> stacks, bays, palettes, profile shares) still hold; this file adds what the photos show that the stage-1 grammar
> missed, with the dimensions and colours used.
>
> Photos: Openverse (Flickr and Wikimedia Commons, open licences), collected with `refimg.py` under
> `/home/user/wt/refs/buildings/<topic>/`. **Reference only: nothing from them is copied into the repository or
> shipped.** Every mesh is procedural; colours are the cartoon palette of `newar_houses.md` §9, checked against the
> photos. Credits are in §9.
>
> Labels as in `newar_houses.md`: **[photo]** seen in the cited photographs, **[src]** a cited source, **[est]** an
> estimate.

## 0. What the owner asked for, and what was wrong

"Everything looks so boxy … cities … some houses have way too low balcony … it blocks the way … some of the houses
are in the roads." The stage-1 B0 grammar drew each house as one box with flat panels for windows, one colour per
footprint and nothing on the side walls. The photos show the opposite: **every wall that looks onto a lane is a
facade** (windows, shops, signs), **openings are deep holes** (0.3 m reveals in Newar walls, 0.16 m in RCC), and
**rows are made of separate houses** with their own heights, colours and roofs. Two rules from the contract are
binding: nothing below 4.5 m over a road corridor, and no footprint in a corridor.

## 1. What stands in each place now (2023-2026)

| Place | Mix today [photo; newar_houses.md §3] | What reads first from the street |
|---|---|---|
| **Bhaktapur** (Taumadhi, Tachupal, the lanes) | Newar row houses of 3-5 storeys in dark red fair-face brick, many rebuilt after 2015 in brick-veneered RCC that keep tile gables (heritage by-law); some plastered or painted modern houses at the edges | Big dark diagonal **struts** under deep (1.2 m) eaves; carved black-brown window frames with **lintel and sill ears**; lattice tikijhya; the projecting **sanjhya**; carved timber floor bands; stone-capped plinths (pikha); prayer-flag bunting; shop signs at the squares [bhaktapur_street ref_03, ref_09] |
| **Patan** (Mangal Bazaar lanes, bahals) | Brick Newar houses and long brick rows with **painted (often green) window frames**, ground-floor shops, **tiled pent roofs over the ground floor**; plastered and painted hybrids with big shop boards; bahal courtyards [patan_lane ref_02, ref_04, ref_05, ref_08] | Continuous brick fronts with rows of small windows; timber or painted jali balconies; low tiled hoods; lime-washed or red-painted ground floors |
| **Asan, Indra Chowk** | The most altered core: 5-7 storey hybrids and RCC houses, plastered and painted, a few old carved fronts (painted black) [asan ref_02, ref_03, ref_06] | Shops on every ground floor with **goods hanging over the fronts** (clothes, bags, garlands), tin awnings, balconies on the upper floors, dense wires (street dressing, not this package) |
| **Thamel** | 4-7 storey guest houses and hotels: exposed brick or pastel paint, **balconies with white concrete balusters** or steel rails and plants, **stacks of fascia boards and vertical blade signs** on every lower floor, rooftop restaurants with umbrellas, prayer flags [thamel ref_00, ref_02, ref_06; balconies ref_04; thamel_rooftop ref_00, ref_02] | Signboards in red, yellow, green, blue, white on almost every floor up to the 3rd; hanging pashminas and bags at the shops |
| **Baneshwor, Koteshwor, Kalanki** (the metro) | RCC frame houses of 3-6 storeys, the street face painted (pastels), **side and back walls raw brick**, roller-shutter shops on main roads, a few new commercial blocks with glass and aluminium-composite (ACP) cladding [baneshwor ref_00, ref_05; balconies ref_06; kirtipur ref_00] | Black and blue **rooftop tanks** (often on the stair cabin), **rebar stubs** with bottles on top on growing houses, evacuated-tube **solar heaters**, flat terraces with parapets, cantilevered upper floors, concrete sunshades (chhajja) over every window, steel window grilles |
| **Kirtipur** | Brick and plastered houses stepping up the ridge; colourful RCC stacks on the slopes [kirtipur ref_00, ref_01] | Stone plinths, stepped lanes; mixed Newar and modern |
| **Bungamati, Khokana** | Brick and **mud- or ochre-plastered** village houses with tile roofs, **red-painted doors and windows**, pot plants on the plinths, laundry, **haystacks and grain on the roofs** [bungamati ref_00, ref_01, ref_03] | Low (3-4 storey) houses with deep tile eaves over a brick-paved square |

## 2. Newar house details (Bhaktapur, Patan, Kirtipur, the cores)

| Element | Real size and look [photo / src] | Modelled as (B0) |
|---|---|---|
| Struts (*tundal*) | 1.3-1.6 m long, 12-16 cm square, dark sal, rising at 45-60° from a wall plate on the top storey to the eave; one every 1.2-1.5 m; plain or lightly carved on houses [bhaktapur_street ref_03] | `Strut`: bevelled beam 0.14 m with a carved bulge two-thirds up, bracket block at the foot, on a moulded wall plate; spacing 1.15-1.45 m; rising about 50° (`HouseBuilder.Roofs.NewarRoof`) |
| Eaves | Overhang 1.0 m (1.2 m Bhaktapur), timber soffit with rafters, fascia board, jhingati courses, rounded ridge tiles with raised ends [ref_03; S2] | Swept slab 0.18 m: soffit, fascia, tile courses every 0.34 m with a 3 cm lip; rafters every 0.5 m; ridge rod with end knobs; barge boards at row ends |
| Tikijhya | 0.6-0.8 × 0.8-1.0 m lattice in a carved frame; lintel and sill run 0.2-0.35 m past the frame ("ears"); set into a deep reveal; diagonal or square lattice [bhaktapur_window ref_08] | Real opening with 0.3 m reveal and a dark room; frame jambs 0.13 m; lintel and sill ledges with chamfered fronts; diagonal (70%) or square (30%) lattice of 3.4 cm strips at 0.16 m |
| Sanjhya | Projecting bay of 3-5 units, 2.4-3.6 m wide, projects 0.3-0.6 m on carved brackets, lattice over a carved apron, small hood [patan_lane ref_04; S8] | Moulded sill on 2-4 brackets, posts per unit, lattice screens over carved aprons, cheeks, cornice and a tiled hood; projection clipped out of the road below 4.5 m but never dropped |
| Door | Low (1.45-1.7 m) carved door, frame with ears, threshold stone, a painted panel and vermilion above [bhaktapur_street ref_03, ref_04] | Recessed leaves with raised panels, frame, double lintel, threshold stone, ochre panel with sindoor, marigold garland on 25% |
| Floor bands | Brick corbels (2 courses) or carved timber bands with joist ends (Bhaktapur) | `FacadeKit.Corbel` / timber `Ledge` with joist-end blocks every 0.55 m |
| Pent roof | A tiled single-slope hood above the ground floor, 0.6-0.85 m, on small struts (Patan 45%, Bhaktapur and the KTM core 30%) [patan_lane ref_08] | `Hood` at the G/1 line; clipped (or dropped) where the lane is too narrow below 4.5 m |
| Plinth | Stone-capped brick apron 0.3-0.45 m high, up to 0.6 m deep [bhaktapur_street ref_01] | `Pikha`, clipped out of the road; walkable collider |
| Brick | Fair-face, dark red; glossy dachi apa on the best fronts | Channel `BrickGlazed` on Bhaktapur and Patan fronts, `Brick` elsewhere; colour per profile (`newar_houses.md` §9.1) |
| Wood colour | Natural dark sal `#4A2C1C`; painted black `#2B221D` (30% KTM core); painted green `#2F6E4E` (Patan, KTM, 22%) [patan_lane ref_02]; red `#9E2B25` (Bungamati) | Per plot |
| Roof tiles (jhingati) | Old, weathered and never new-orange: Bhaktapur roofs average `#6a5040`-`#735543` in morning light and `#533421`-`#8f5438` in shade, `#ab7750`-`#b87237` in full sun [bhaktapur_houses ref_07, bhaktapur_street ref_09]; Patan's dusty `#9e827e`-`#bc9995` in full sun and `#7e6a6a` in shade [patan_lane ref_08]; darker, mossy or sooty along the eaves; no two courses alike | Heritage profiles (Bhaktapur, Patan, Kirtipur, the towns, the KTM core): `#8A5A44` `#9C6450` `#6E4A3A` `#A57A60` `#A8603E` `#5E4A3A` (28/24/20/12/10/6%); elsewhere fired red-brown `#A8473A` `#B4583F` `#9A5A44` `#7A5040` `#6F5A3C`; every course its own shade (`ShapeColor.JitterFaces`) and the lowest third of each slope shaded toward soot brown `#4F4436` (up to 45% in the heritage towns, `HouseBuilder.Roofs.Weather`); ridge `#7E4A36` |

## 3. Modern RCC house details (the metro, Thamel, Asan)

| Element | Real size and look [photo / src] | Modelled as (B0) |
|---|---|---|
| Frame and paint | Columns 0.23-0.3 m at 3-4.5 m; slab edges as bands; street face painted pastel (white, cream, yellow, peach, pink, sky, mint, lime, turquoise, lavender, orange), sides raw brick on 60% [baneshwor ref_05; balconies ref_06] | Pilasters at plot ends (rounded corner), rounded slab band per floor in the trim colour, raw-brick side walls with a concrete plinth |
| Windows | 1.1-1.25 × 1.35 m aluminium or timber frames with a mullion and a transom, tinted glass (blue, teal, brown), a concrete sill, a **chhajja** sunshade 0.3-0.45 m over each window, steel grilles on many | Real opening 0.16 m deep; frame strips; glass `Glass` channel; ledge sill; chhajja ledge (clipped below 4.5 m); grille bars on 60% |
| Balconies | Cantilever 0.9-1.2 m on 40-60% of houses: stainless pipes, painted bars, white concrete bottle balusters (guest houses), solid parapets, or painted timber jali on older houses; plants and airing clothes [balconies ref_04, ref_05; colourful ref_06] | Slab with a rounded edge; five railing styles; pot plants; a sari over the rail; **clipped to the free depth before a road below 4.5 m, a French balcony when nothing is left** |
| Cantilevered upper floors | Floors 1+ overhang the street by 0.5-0.9 m on many houses | Only where the corridor leaves room (or 4.5 m up): soffit, cheeks and roof strip |
| Shops | Roller shutters 2.4-2.7 m high, 2.5-3.5 m wide, 1-3 per front; most open in the day, showing stocked shelves and a counter; drum box at the head; tin or canvas awnings; fascia boards 0.6-1.0 m [asan ref_06; patan_lane ref_05] | Open shops with shelves, boxes and a counter, or corrugated shutters (half open on some) with a bottom bar and lock; drum box; awnings clipped or dropped; boards with abstract lettering (no names or brands) |
| Signs | Thamel: 3-8 boards per front plus **vertical blade signs** 0.45 × 1.5-2.4 m projecting 0.55-0.9 m on two arms, on most floors up to the 3rd [thamel ref_00, ref_02] | Stacked fascia boards; 1-2 blade signs per lower floor in Thamel (80%), 30% in the KTM core; below 4.5 m over a road they lie flat on the wall |
| Hanging goods | Clothes, bags and garlands hung over the shop fronts up to 3 m [asan ref_03] | Garment panels on a rod across the shop head; plump bags in the doorway |
| New commercial blocks | Glass bands between ACP cladding (silver, blue, red), big sign band, glass shop fronts (main roads in the metro) | `CommercialUpper`, `GlassShopfront` (35% of 4+ storey houses on main lanes in the metro) |
| Rana-style | White stucco, pilasters, French windows with fanlights and green louvred shutters, cornices, balustrade with urns [balconies ref_08: Gaddi Baithak] | `RanaFront` and the roof balustrade |

## 4. Roofs and roof props (the skyline)

| Prop | Real [photo / src] | Rate | Modelled as |
|---|---|---|---|
| Water tank | Black (and blue, cream) polyethylene, 1,000 L about Ø1.1 m × 1.0-1.2 m with two raised hoops, domed shoulder, screw lid; on a steel angle stand or brick piers, often **on top of the stair cabin** [roof_tanks ref_00; `newar_houses.md` S25; vendor sheets] | 60-80% of flat roofs, 1-3 | `PropKit.Tank` (lathe, 8-10 sides), `PropKit.Stand`; 45% on the cabin |
| Solar water heater | 12-15 evacuated tubes Ø58 mm × 1.8 m under a horizontal drum Ø0.47 m × 1.5 m, on a galvanised frame tilted 30-45° to the south; installation area about 2.2 × 1.5 m [vendor sheets] | 15-25% | `PropKit.SolarHeater` facing south |
| Rebar stubs | Column stubs 0.1-0.4 m with 4 bars rising 0.6-1.0 m, a plastic bottle on some bars [modern_houses ref_00] | 42% of growing RCC houses of 3+ storeys | `PropKit.RebarColumn` at the column heads |
| Stair cabin (*mumty*) | 2-3 m square, 2.4 m high, a slab with an overhang, a steel door | All flat roofs of 3+ storeys | Rounded box, door, slab |
| Laundry, plants, dishes | Lines between posts, terracotta pots with marigolds and geraniums, dishes [colourful ref_06] | 45% / 32% / 15% | `PropKit.Laundry`, `PottedPlant`, `Dish` |
| Rooftop restaurant | Umbrellas, plastic chairs, plants, prayer flags (Thamel, Boudha) [thamel_rooftop ref_02] | Thamel 35% | `PropKit.Umbrella`, `Chair`, `PrayerFlags` |
| Haystacks | Hay and maize stacks on village roofs [bungamati ref_01] | 25% (Bungamati, Khokana, rim) | Two ellipsoids |
| Jhingati gable | Newar houses; hybrids in the heritage zones keep tile gables (Bhaktapur 70%, Patan 45%) | — | `NewarRoof` on near-rectangular plots |

## 5. Rows, plots and street-facing walls

* **Plots.** Merged OSM footprints in Kathmandu, Patan, Thamel and Kirtipur are split into 4-8 m plots; each is its own
  house: archetype (40% redraw from the profile mix), storeys (45% one or two fewer than the plan, never taller, so B1
  stays the envelope), palette, roof (flat or gable) and parapet. Partition walls show only where a plot rises above its
  neighbour.
* **Every wall on a lane is a facade; walls on open ground get windows; the rest stay blank.** A body wall is judged at
  three points along its square-cornered extent (so every drop level dresses the same walls): a road corridor within
  4 m *in front of it* (stepping 3 m out brings the road 1 m nearer) makes it a **street** wall: windows on every floor
  (tikijhya on Newar floors), shops or small windows on the ground floor, bands and signs; nothing within 3 m in front
  and a road within 14 m in front makes it an **open** wall (a small square, a lane's bend): windows only, wider apart;
  anything else (against a neighbour, a back wall on a courtyard or an open plot away from the roads, the party wall at
  the end of a row) stays **blank** raw brick or paint, as in the photos. The corridor source reports real distances
  (out to 64 m), so "a road within 4 m" means a real road.
* **Corners.** Exposed convex corners are rounded (0.1 m Newar, 0.16 m RCC; smooth normals); corners against a
  neighbour stay square so a row has no notches.

## 6. Clearance (decisions 1 and 2) as modelled

* Footprints in a corridor are cut back along the corridor edge (half-plane cuts at the deepest intrusion, the larger
  side kept where a road runs through a building), in every band; a house that stands in the road is dropped
  (`BuildingFootprints`). Measured on the valley pack with the stand-in corridors: Asan 10/516/161 trims 2,158 and drops
  29 houses; Bhaktapur 10/528/157 trims 765 and drops 6.
* Projections ask `Clearance.Depth` for the free depth before building: balconies, sanjhya, gajhya, pent hoods, eaves,
  chhajjas, sills, signs, blade signs, awnings, steps, canopies, cantilevers and aprons are clipped, laid flat or left out
  below 4.5 m over a corridor; above it they keep their full depth. Each element is clipped by its own free depth
  (the sanjhya projects only as far as the road leaves room and is set flush into the wall when nothing is free, its
  brackets, pot and hood only where they fit; hanging goods, garlands, struts and copings likewise).
  `Clearance.Clamp` then moves any remaining vertex below the clearance out to the corridor edge: only frames and boards
  a few centimetres proud of a wall on the edge (Asan with the stand-in corridors: 8,052 of 11.6 M vertices moved, 29 by
  more than 5 cm).

## 7. LOD bands and budgets (as built)

B0 keeps the W2 radii and is drawn in two rings; nothing inside B0 is ever the bare B1 box. Band table in
`Core/Meshing/Buildings/BuildingBandTable.cs` (the one source of truth), read by `World/Buildings/BandConfig.cs` and
`DetailCells`:

| Tier | B0 near ring (full grammar, 32 m cells) | Cap per plot | Lite ring (64 m cells) | B0 outer | B1 outer | B2 outer | B3 outer |
|---|---|---|---|---|---|---|---|
| Low | 0-10 m, from drop level 1 | 2,200 | 10-35 m at the **flat** level | 35 m | 120 m | 350 m | 750 m |
| Mid | 0-22 m | 3,600 | 22-60 m at the **lite** level | 60 m | 200 m | 500 m | 1,250 m |
| High | 0-36 m | 4,500 | 36-80 m at the **lite** level | 80 m | 250 m | 700 m | 1,750 m |

* **Drop levels.** 0 everything; 1 no small relief and props; 2 no struts, tile courses or grilles; 3 no floor bands,
  railings or chhajjas; 4 no roof props; 5 **lite** (about a quarter of level 0): every opening still cut with its
  reveal over a dark room, a plain frame (lintel and sill with ears), a coarse lattice, the sanjhya as a bay, eaves, hoods,
  balconies, shutters and boards, the door canopy and the first water tank; 6 **flat** (about a seventh): the lite house
  with its openings laid on the wall (a dark or glazed fill under a lintel and a sill board, the coarse lattice on it).
* **Per plot.** The cap holds per plot (one house of a merged row): each plot is built at the level its size predicts
  and only a plot that still overflows is rebuilt lighter, once (rarely twice); the lightest fallback is the lite level.
  Retries at Asan: 17% of plots at the Low cap, 9% Mid, 4% High; 1.31x / 1.19x / 1.10x the kept triangles built, the
  Low tier doing no more work than High. The heaviest Asan cell builds in about 15-45 ms on a desktop worker (Release).
* **Same house at every level.** The grammar draws structure (archetype, storeys, balconies, cantilever, hoods, shops,
  signs, awnings, sanjhya, tanks) from per-element forks of its random sequence and lays out bays, windows and side
  walls on the square-cornered plot, so a house never grows or loses a balcony, a shop or a dressed side wall when it
  crosses from the near ring into the lite ring (`EveryDropLevelKeepsTheHouseStructure`,
  `EveryDropLevelDressesTheSameSideWalls`).
* **B1** is the styled extrusion with its front detail (front paint, floor band, window rows; `BuildingBandTable.B1Options`).

Budget check (`MeshingBuildingGrammarTests.BandTotalsAtAsanFitTheBudgetTable`), measured honestly: Asan lies 36 m from
the east edge of its tile, so every band is measured on the 3 × 3 tiles around it (B0 per house by distance, B1-B3
triangle by triangle with the band shader's cross-fade), 40% in view:

| Tier | B0 (share) | B1 | B2 | B3 | Total | W2 slice × 1.15 |
|---|---|---|---|---|---|---|
| Low | 5.6 k (5.8 k) | 5.1 k | 8.3 k | 6.0 k | 25.0 k | 25.3 k |
| Mid | 38.3 k (41 k) | 10.9 k | 12.2 k | 16.5 k | 77.9 k | 82.8 k |
| High | 85.4 k (90 k) | 14.8 k | 21.3 k | 26.2 k | 147.8 k | 155.3 k |

The EditMode `WorldW2BandTests` reads the same table (radii, caps, slices, the B0 share) and checks TileBuild's far
bands around Asan the same way. On Low the far bands alone take 88% of the slice (B3 measures about three times the
design's estimate), which is why the Low lite ring is flat (see §11).

## 8. Colours used (hex)

`newar_houses.md` §9 plus: painted green `#2F6E4E`, painted red `#9E2B25`, teal jali `#3FA39A`, hay `#C9A24E`, glass
tints `#3E5F7A` `#2F6B6E` `#5A4A3A` `#34495E` `#4A7A9A`, ACP `#C4C9CE` `#2F6FB0` `#C23B33` `#D8C8A0` `#5B6B7A`, awnings
`#3D7CC9` `#2FA84F` `#E8483A` `#F2C230` `#B9BEC3` `#F07A2A`, cloth `#D93A2B` `#F2C230` `#2E6FD8` `#FFFFFF` `#E85D9E`
`#2E9E4F` `#7A1F1F` `#F59A3B`, aluminium `#C9CED3`, steel `#2E3440`, galvanised `#B9BEC3`, terracotta pots `#B5582F`,
jhingati (heritage) `#8A5A44` `#9C6450` `#6E4A3A` `#A57A60` `#A8603E` `#5E4A3A`, jhingati (metro) `#A8473A` `#B4583F`
`#9A5A44` `#7A5040` `#6F5A3C`, ridge `#7E4A36`, soot and moss on old tiles `#4F4436`.

## 9. Preview comparisons

Street-level renders of the valley pack next to the photos are under `/home/user/wt/previews/buildings/` (not in the
repository): `bhaktapur_lane_*`, `patan_lane_*`, `asan_*`, `thamel_*`, `baneshwor_*`, `kirtipur_*`, the archetype lineup
`lineup_sheet.png` and the side-by-side sheets `cmp_*.png` (photo left, render right). The renders of the band
configuration per tier (B0 near and lite rings, B1 beyond) are under `fix2/obj/places/<place>/tier{0,2}/street.png`,
the full / lite / flat levels of each archetype in `fix2/levels_*.png`, and the weathered roofs against the photos in
`fix2/cmp_bhaktapur_roofs.png`, `fix2/cmp_taumadhi_roofs.png` and `fix2/cmp_patan_roofs.png`. Regenerate with
`GHUMANTE_PREVIEW_DIR=... dotnet test core-tests --filter MeshingBuildingPreviewTests` and `tools/mesh-preview/render.py`.

## 10. Photo credits (reference only, not shipped)

All from Flickr unless noted; licence as published.

* Bhaktapur: "Maison traditionnelle de Bhaktapur", dalbera, CC BY 2.0, https://www.flickr.com/photos/72746018@N00/8554519689 ·
  "Old Man Relaxing in Front of His House, Bhaktapur", terbeck, CC BY-NC-SA 2.0, https://www.flickr.com/photos/39832617@N07/14332863327 ·
  "Taumadhi Tole - with Bhairabnath Temples", eriktorner, CC BY-NC-SA 2.0 · "Tachupal Tole in Bhaktapur", eriktorner, CC BY-NC-SA 2.0,
  https://www.flickr.com/photos/39267804@N05/32428971343 · "Potter's Square in Bhaktapur", eriktorner, CC BY-NC-SA 2.0 ·
  "Pujari Math, old Hindu priests house", eriktorner, CC BY-NC-SA 2.0, https://www.flickr.com/photos/39267804@N05/32903895570 ·
  "Sculpture de fenêtre newar (Bhaktapur)", dalbera, CC BY 2.0, https://www.flickr.com/photos/72746018@N00/8555635976.
* Patan: "Streets of Patan", René Clausen Nielsen, CC BY-SA 2.0, https://www.flickr.com/photos/86419644@N00/18186290763 ·
  "One of the ancient buildings bordering the Lalitpur/ Patan Durbar Square", shankar s., CC BY 2.0,
  https://www.flickr.com/photos/77742560@N06/49629587922 · "2015-03-30 Nepal 916 Kathmandu, Patan, Lalitpur", Allie_Caulfield, CC BY 2.0,
  https://www.flickr.com/photos/28577026@N02/16769370063 · "Another look at some of the historic buildings bordering the Lalitpur/ Patan
  Durbar Square", shankar s., CC BY 2.0, https://www.flickr.com/photos/77742560@N06/49626817681 · "Bahal à Patan", jfgornet, CC BY-SA 2.0.
* Asan: "Asan Tole, Kathmandu" (series), isapisa, CC BY-SA 2.0, https://www.flickr.com/photos/97189192@N00/31174349528 ·
  "Market Street, Asan Tole", eriktorner, CC BY-NC-SA 2.0 · "Kathmandu, Nepal", jafsegal, CC BY 2.0.
* Thamel: "Thamel street, Kathmandu", andreweland, CC BY-NC-ND 2.0 · "Nepal - Kathmandu - 001 - streets of Thamel" and "002 - the
  interesting 3D rooftops of Thamel", mckaysavage, CC BY 2.0, https://www.flickr.com/photos/56796376@N00/492181084 ·
  "Rooftop Restaurants, Thamel", eriktorner, CC BY-NC-SA 2.0, https://www.flickr.com/photos/39267804@N05/13256215845 ·
  "Kathmandu Guest House, Nepal", Rick McCharles, CC BY 2.0, https://www.flickr.com/photos/71035721@N00/10606670136 ·
  "Legal hashish shop in Kathmandu, Nepal in 1973", Roger McLassus, CC BY-SA 3.0 (Wikimedia Commons).
* Metro and houses: "New Baneshwor Kathmandu" and "baneshwor chock", thapa.laxman, CC BY-SA 2.0,
  https://www.flickr.com/photos/33926277@N07/4145170324 · "City of Kathmandu", margretemborsky, CC BY 2.0,
  https://www.flickr.com/photos/127756652@N07/15252959686 · "Myriad of electricy lines in Kathmandu", World Bank Photo Collection,
  CC BY-NC-ND 2.0 · "Kathmandu hanging laundry", yumievriwan, CC BY-NC-ND 2.0 · "Kathmandu rooftop water tanks", tvancort,
  CC BY-NC-SA 2.0, https://www.flickr.com/photos/44518317@N00/1936721912 · "Nepalese graffitti, new construction, concrete and re-bar",
  Wonderlane, CC BY 2.0, https://www.flickr.com/photos/71401718@N00/5210360399 · "construction in Nepal new storefront", Wonderlane,
  CC BY-NC 2.0 · "Totally out of sync with the surroundings … the Gaddi Durbar", shankar s., CC BY 2.0.
* Kirtipur and villages: "Kirtipur miracle.", anjetika, CC BY-NC-SA 2.0 · "2015-03-30 Nepal 443 Kirtipur", Allie_Caulfield, CC BY 2.0 ·
  "Bungamati, village newar (Népal)" and "Sur la place du temple (Bungamati)", dalbera, CC BY 2.0,
  https://www.flickr.com/photos/72746018@N00/8629058868.
* Specifications: Indian tank vendor listings (Sintex 1,000 L: Ø1.10 m × 1.01 m; lntsufin.com), evacuated-tube heater listings
  (15 tubes Ø58 × 1,800 mm, 2.2 × 1.5 m installation area; storeking.in, lntsufin.com).

## 11. Open issues for the lead and the integration package

* **B1 front detail in the streamer.** `TileBuild` builds the B1 layer from `MeshingSettings.Buildings` (default
  options, `FrontDetail` off), so the window rows and floor bands of B1 are not drawn yet. Build it from
  `BuildingBandTable.B1Options(settings.Buildings)`; the band budget above already counts it. The W1 whole-selection
  bound in `core-tests/StreamingConfigTests.WholeSelectionsWithDetailFitTheTriangleAndMeshBudgets` builds every
  building of the selection as B1 and will then need rebasing (B1 costs about 28 instead of 18 triangles per building;
  with the default on, tier 0 measured 711 k against its 630 k allowance).
* **Road corridors.** Buildings use the package's stand-in corridor source (`RoadCorridorStandIn`, real distances out
  to 64 m) until `BuildingOptions.Corridors` is set to the roads package's `RoadCorridorIndex.ForTile` in
  `MeshingSettings` (all four building option sets: the guard is cached per tile and corridor source).
* **Low far bands.** Measured on the 3 × 3 tiles around Asan, B1-B3 take 19.4 k of Low's 22 k slice (B3 about
  three times the W2_DESIGN 2.4 estimate, B2 a third more), leaving B0 5.8 k. A coarser B3 on Low (or a smaller Low
  B3 radius) would give B0 room for a lite instead of a flat outer ring.
* **Enum golden test.** `core-tests/EnumsTests.EveryEnumIsCovered` fails on the base commit already (the stub
  `RoadStructureKind` and `RoadStructureFlags` of the data package are not in the golden list).
