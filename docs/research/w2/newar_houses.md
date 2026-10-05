# W2 research: houses of the Kathmandu Valley, per city

> Status: research input for Wave 2 "Kathmandu comes alive", written 2026-10-05. It covers vernacular Newar houses, courtyard blocks, Rana-era palaces, modern RCC houses, Thamel and post-2015 rebuilding, with a facade and roof grammar a procedural generator can implement, and bright cartoon colour palettes in hex.
> Scope: buildings that are not temples. Temple and stupa geometry is in other W2 research files. This file only says how a house frames or abuts a temple square.
> Evidence: (a) published sources, each cited with its URL; (b) an OSM census written for this file over `pipeline/data/raw/osm/nepal.osm.pbf` (2026-10-02 snapshot) and the built pack `pipeline/build/regions/kathmandu_valley/qa/buildings.geojson`. The method is in Appendix A.
> Labels: **[src]** means the number comes from a cited source. **[osm]** means it was measured from OSM or the pack for this file. **[est]** means an estimate (from photographs, reasoning, or rounding several sources together). An estimate is a design default to check against references, not a fact.
> Licences: photos and drawings in the cited pages are reference only. Nothing from them ships. Every shipped mesh and texture is procedural (ASSET_MANIFEST §1).

---

## 0. TL;DR for the generator

1. **The traditional Newar house is a small brick box with a rigid storey grammar.** It is 4–8 m wide [src], about 6 m deep (two bays of 2.2–2.8 m on each side of a spine wall) [src], and 3–4 storeys at a floor-to-floor height of **2.2–2.5 m** [src]. It has a gabled jhingati-tile roof pitched at **about 35°** with an overhang of **about 1.2 m** on carved struts [src]. Every storey has its own window type: a shop *dalan* or low door on the ground floor, *tikijhya* lattice windows on the 1st floor, a central projecting *sanjhya* on the 2nd floor, and a *gajhya* dormer in the roof.
2. **Use real footprints as the frontage measure.** The median short side of the footprint is **4.9 m in Bhaktapur's core**, 5.7 m in Kathmandu's core, 7.3 m in Patan's core and 7.5 m in Thamel [osm]. Narrow plots mean 1–3 window bays per storey, not 6.
3. **The cores are already hybrid.** Where OSM tags roof shape in the cores, **73–77% of the tagged roofs are flat** (Patan, Kathmandu core) [osm]. Where OSM tags structure across the valley, **47.4% of the tagged buildings are RCC** and 44% are brick in cement mortar [osm]. Both tags are biased toward newer buildings, but a "NEWAR = gabled tile" rule is wrong for most core buildings. The generator needs a **NEWAR_HYBRID** variant: a brick-veneered RCC frame with 4–6 storeys, a flat roof terrace, a token tiled eave or *kausi* hood, and a water tank.
4. **Heights differ by city.** Tagged levels (mean) are 4.56 in Kathmandu's core, 3.91 in Thamel, 3.08 in Patan's core, 2.84 in Kirtipur and 2.53 in Thimi [osm]. Heritage by-laws cap new core houses at **35 ft (10.5 m) and 4 storeys** in Kirtipur and around Bhaktapur's three squares [src], and at 10 m in the Khokana and Bungamati rebuild guidance, which is widely exceeded (4–6 storeys) [src].
5. **The palettes are in §9.** Brick, wood and tile follow each city. Modern RCC uses a pastel set. Rana buildings are white and cream stucco. Thamel adds sign and prayer-flag colours.

---

## 1. Sources at a glance

| # | Source | What it gives | URL |
|---|---|---|---|
| S1 | World Housing Encyclopedia report 99, "Traditional Nawari house in Kathmandu Valley" (GEM wiki mirror) | Inter-storey 2.20–2.50 m including the floor; typical storey 2.3 m; depth about 6 m; frontage 4–8 m; joists 100×70 mm at 150–200 mm; brick 210×105×50 mm; *dalan* timber ground floor for shops; *San Jhya* spanning most of the facade at 3rd-storey level; later openings about 800 mm wide; vertical RCC extensions on top | https://wheki-dev.globalquakemodel.org/reports/report_99 · https://world-housing.net/report-99-traditional-nawari-house-in-kathmandu-valley/ |
| S2 | FAO, "Bhaktapur Development Project" (chapter on the traditional house) | 2–4 storey row houses; two bays separated by a spine wall; bay span 2.20–2.80 m; joists 10×7.5 cm at 10–15 cm; 10–15 cm clay over the floors; **ceilings 1.80–2.20 m**; **roof pitch about 35°**; **overhang about 1.20 m on struts**; jhingati pressed into wet clay | https://www.fao.org/4/l2680e/l2680e01.htm |
| S3 | Kirtipur Municipality Building, Planning and Heritage By-laws (final draft, Magh 2064 BS / 2007) | Core zones: ≤ 35 ft (10.5 m) including tanks and ≤ 4 storeys; fair-face brick, no plaster; wooden windows "3′×5′"; jhingati roofs kept; roof slope ≥ 25°; RCC cantilevers only above the 3rd floor; cornice band ≤ 9 in (0.23 m) at each floor; balcony at the 3rd floor; front apron 1–1.5 ft high × 2 ft wide; minimum plot frontage 6 m; minimum ceiling 2.30 m (core) | https://kirtipurmun.gov.np/storage/01K56DSTRMWM9WHMTXDTYYQJ8M.pdf |
| S4 | D. Gautam, "Building features acquired from indigenous technology… Bhaktapur" (JScE vol. 2) | More than 90% of Bhaktapur municipality's buildings are traditional URM; 3-storey norm; height ≤ 10 m; floor names *chhendi / mantan / chota / buigal*; kitchen on the top floor, so no chimneys; plinths; struts; wooden lintel bands; L < 2B | https://elibrary.khec.edu.np/bitstream/123456789/134/1/6_Dipendra_Gautam.pdf |
| S5 | Kathmandu Post, "The history of dachi appa and ma appa bricks" (2018) | Dachi apa 8×4×2 in, high-fired, darker, glossy, tapered; ma apa 8×5×1¾ in; plinth brick 18×10×5 in; made in Thimi and Satungal | https://kathmandupost.com/news/2018-02-18/the-history-of-dachi-appa-and-ma-appa-bricks.html |
| S6 | Nepali Times, "Protecting Kathmandu's historic roofscape" | Jhingati 200×105 mm, an 8 mm groove locks the tiles, laid on fresh clay; colours "carmine, scarlet-red, vermillion and tangerine orange" | https://nepalitimes.com/protecting-kathmandu-s-historic-roofscape |
| S7 | Nepal Traveller, "The architecture of Patan" | Three-leaf wall: dachi apa face, ma apa structure, rubble core; *tundal* struts; *ankhijhya*; *san jhya* "spanning a whole floor's width" | https://nepaltraveller.com/sidetrack/3397 |
| S8 | Wikipedia, "Newar window" | Sanjhya = projecting 3-unit bay, centred, 3rd floor; tikijhya = lattice, most common, 2nd floor; gajhya = projecting window under the roof; pasukha jhya = 5-unit window on shrines (Pancha Buddha); peacock window *mhaykhā jhyā* | https://en.wikipedia.org/wiki/Newar_window |
| S9 | Wikipedia, "Newar architecture" | Joists *dhalin*; a 10 cm yellow-clay floor; tundal, ankhijhyal, bahal and chowk vocabulary | https://en.wikipedia.org/wiki/Newar_architecture |
| S10 | Bhaktapur.com, "A closer insight into typical traditional Newari house" | Floors *chidi / matang / chuata / buigal* (spellings vary); low doors on purpose ("a house is a temple"); exposed terracotta brick; tiles *aypa* | https://www.bhaktapur.com/a-closer-insight-into-typical-traditional-newari-house/ |
| S11 | Kathmandu Post, "Bhaktapur offers free design approval" (2016) | Houses around Bhaktapur Durbar, Taumadhi and Dattatreya squares ≤ 35 ft; carved wooden windows and doors required; permit fees waived for traditional-style rebuilds | https://kathmandupost.com/valley/2016/02/01/bhaktapur-offers-free-design-approval |
| S12 | Nepali Times, "Khokana and Bungamati strive to save heritage" (2020) | Rebuild guidance limits homes to 10 m and recommends 2–3 storeys; people built 4–5, some 5–6; Rs 300,000 grant per household; a restoration reused 70% of the old material | https://nepalitimes.com/khokana-and-bungamati-strive-to-save-heritage |
| S13 | Yamamoto et al., "Exterior design of townhouses… eaves types… Nepali city" (Japan Architectural Review 6(1), 2023) and the Japanese report PDF | Survey of 104 townhouses around Tachupal, Bhaktapur, inside and outside the monument zone; eaves types used to date upper-floor extensions | https://onlinelibrary.wiley.com/doi/full/10.1002/2475-8876.12395 · https://r-dmuch.jp/jp/researchnews/contents/18.10.13.Disaster_Mitigation_Design_for_Historic_Cultural_Cities.pdf |
| S14 | Nepal Traveller, "Tundal in Nepalese architecture" | Struts of sal and sissoo; their structural role | https://nepaltraveller.com/sidetrack/tundal-in-nepalese-architecture |
| S15 | ECS Nepal, "Hidden Worlds" / Patan bahal pages | Patan has 166 bahal-type courtyards; 18 main bahals | https://ecs.com.np/features/hidden-worlds · https://bajracharya.org/18-bahals-of-patan-unveiling-ancient-mahavihars/ |
| S16 | Lonely Planet, Pujari Math; South Asia Commons (Tachupal) | Peacock window at Pujari Math, Tachupal Tole, Bhaktapur; building 15th c. (Yaksha Malla), rebuilt 1763; now the Woodcarving Museum | https://www.lonelyplanet.com/points-of-interest/pujari-math/1432551 · https://southasiacommons.net/artifacts/4803632/ |
| S17 | Wikipedia, "Singha Durbar"; Onlinekhabar, "8 grand architectural marvels of the Rana era"; Hyperallergic and Kathmandu Post (2025) | Singha Durbar 1903 (Wikipedia; Onlinekhabar says 1908), designed by Kumar Narsingh and Kishor Narsingh Rana; Palladian, Corinthian and Neoclassical; 8 courtyards and 1,700 rooms before the 1973 fire; western wing burned on **9 Sep 2025** (Gen Z protests) | https://en.wikipedia.org/wiki/Singha_Durbar · https://english.onlinekhabar.com/8-grand-architectural-marvels-of-the-rana-era.html · https://hyperallergic.com/1040874/historic-singha-durbar-palace-destroyed-as-nepal-protests-turn-deadly/ · https://kathmandupost.com/national/2025/09/14/prime-minister-karki-takes-charge-some-offices-resume-services |
| S18 | Wikipedia, "Garden of Dreams" and "Kaiser Mahal" | Garden of Dreams 1920, Kishore Narsingh; 6,895 m²; originally six pavilions (one per season), three remain; Edwardian and neoclassical; restored with Austrian aid (Götz Hagmüller) | https://en.wikipedia.org/wiki/Garden_of_Dreams · https://www.wikipedia.com/wiki/Keshar_Mahal |
| S19 | ECS Nepal, "Rendezvous with the Ranas"; Shanker Hotel blog | Rana durbars "generally white-plastered with arresting French windows", Grecian columns, porticos | https://ecs.com.np/features/rendezvous-with-the-ranas · https://www.shankerhotel.com.np/blog/2014/10/19/the-historic-durbars-of-kathmandu |
| S20 | Kathmandu Post (2017), "94pc of houses in Kathmandu unsafe"; Nepali Times, "Buildings not strong enough" | 48% of Kathmandu houses are non-engineered RCC; columns of 9×9 or 9×12 in, against a 12×12 in code minimum | https://kathmandupost.com/money/2017/04/28/94pc-of-houses-in-kathmandu-unsafe · https://nepalitimes.com/buildings-not-strong-enough |
| S21 | PreventionWeb, Gorkha earthquake 2015 | 604,930 houses fully damaged (602,257 private), 288,856 partly damaged; Jan 2020: 62% rebuilt, 25% under construction | https://www.preventionweb.net/quick/65633 |
| S22 | J-STAGE URPR 13, Khokana reconstruction (2015–2025 fieldwork) | Many houses rebuilt in RCC, but strong vernacular continuity; Khokana on the UNESCO Tentative List since 1996 | https://www.jstage.jst.go.jp/article/urpr/13/0/13_189/_article/-char/en |
| S23 | Panauti travel and heritage pages (Cathay Pacific, Onlinekhabar) | Narrow lanes of two-storey red brick houses; jhingati roofs; Indreshwar (1294) is the tallest building | https://www.cathaypacific.com/cx/en_KH/inspiration/travel/exploring-traditional-nepali-town-of-panauti.html · https://english.onlinekhabar.com/panauti-unesco-world-heritage-site.html |
| S24 | Thimi pages (bhaktapur.com; Nepali Times, "Thimi's urban tissue") | Potters' town (Prajapati), pottery squares, *nani* clan-cluster courtyards, Balkumari (Biska Jatra) | https://www.bhaktapur.com/discover/thimi/ · https://nepalitimes.com/thimi-s-urban-tissue |
| S25 | Indian tank vendors (Vectus, Sintex, Duraplast product sheets via lntsufin.com) | A 1,000 L rooftop tank is about Ø1.06–1.10 m × 1.10–1.29 m | https://lntsufin.com/product/vectus-smart-plastic-water-tanks-1000-l-white/17240-2561 |

Sources disagree in places, and this file says which value it uses:
* **Storey height.** S1 gives 2.20–2.50 m floor-to-floor and S2 gives ceilings of 1.80–2.20 m. S4 says 2.5–3 m, but that is for all URM, not only traditional houses. The default here is **2.25 m floor-to-floor for traditional storeys** (ASSET_MANIFEST §4 already says 2.1–2.4 m).
* **Floor names.** These are transliterations of the same Newar words: ground *chhẽdī / chidi / chheli*; 1st *mātã / matan / mantan*; 2nd *chwata / chota / chuata*; attic *baĩgaḥ / buigal / baiga*. UI text should use one romanisation, chosen by the localisation reviewer.
* **Which floor has which window.** S8 puts tikijhya on the "second floor" and sanjhya on the "third floor". That uses ground = 1st floor (the British-Indian count would say 1st and 2nd floor). This file numbers floors as **G, 1, 2, 3** (ground = 0).

---

## 2. The traditional Newar row house (*chhẽ*)

### 2.1 Massing and plan

| Parameter | Value | Label | Notes |
|---|---|---|---|
| Frontage (facade width) | 4–8 m typical. Medians from footprint short sides: Bhaktapur 4.9, Khokana 4.9, KTM core 5.7, Panauti 6.2, Bungamati 6.6, Kirtipur 6.7, Thimi 6.8, Patan 7.3 m | [src] S1; [osm] | Use the real footprint. The short side of a row-house footprint is usually the street frontage, but not always (corners, deep plots). Snap facades to the edge that faces the road (`BFNT` frontage edge, CONTENT_COVERAGE D4). |
| Depth | About 6 m: two bays of 2.20–2.80 m plus 3 walls | [src] S1, S2 | Real footprints are often deeper: a house may have a back wing or a second row toward the courtyard. |
| Spine wall | One longitudinal wall parallel to the street, at mid-depth | [src] S2 | Drives the roof: two slopes, one to the street and one to the courtyard, with the ridge over the spine. |
| Storeys | 3 is the norm (S4); 2–4 (S2); 3–4 (S1). Later extensions go to 5–6 | [src] | Per-city priors are in §6. |
| Floor-to-floor | G 2.40 m, 1st 2.25 m, 2nd 2.25 m, 3rd/attic 2.10 m to the eave plate | [est] from S1, S2 | Clear ceiling 1.8–2.2 m (S2). A NEWAR building uses these heights; do not apply the 3 m default. |
| Plinth | 0.15–0.45 m above street; front apron (*pikhā*) 0.3–0.45 m high × 0.6 m deep | [src] S3 (1–1.5 ft × 2 ft); [est] height range | Stone-capped. Doorstep stone (*pikhā lukhā* / door-stone *mandala*) in front of the door. The plinth is what keeps the monsoon off the walls. |
| Wall thickness | Ground 450–750 mm, thinner going up | [src] Nepal Traveller and Uni Idaho pages (three-leaf walls 45–75 cm). S4 quotes 9″/4.5″, which reads like modern infill | Shows only as deep window reveals: use **0.45 m reveals on G and 1, 0.35 m above** [est]. |
| Wall build-up | Outer leaf dachi apa (glazed, tapered), middle ma apa, rubble or mud core | [src] S5, S7 | Facade texture = dachi apa stretcher bond with hair-thin joints. |
| Brick module | Dachi apa 8×4×2 in (203×102×51 mm); ordinary 210×105×50 mm | [src] S5, S1 | One course is about 52–55 mm, so a 2.25 m storey has about 42 courses. In the detail atlas, draw brick **at 2× real size for legibility at mobile distance** [est]. |
| Roof form | Gabled, ridge parallel to the street | [src] S3 ("sloped on two sides") | Corner houses and some Bhaktapur houses use a hipped return [est]. |
| Roof pitch | **35°** (S2); by-law minimum 25° (S3); Kirtipur's height rule names 25–30° | [src] | Default **32° ± 3°**, seeded [est]. |
| Eave overhang | **About 1.20 m** on struts | [src] S2 | Houses 0.8–1.2 m, monasteries deeper, temples deepest (S7). Default 1.0 m, Bhaktapur 1.2 m [est]. |
| Struts (*tundal / tunāḥ*) | One at each structural post or window jamb, about every 1.2–1.8 m along the eave; 1.0–1.4 m long; 35–45° | [est] from photographs; S14 for role and wood | On houses they are plain or lightly carved. Never put figurative or erotic carving on house struts (ASSET_MANIFEST cultural note). |
| Roof build-up | Rafters, planks or bamboo mat, 10–15 cm clay, jhingati | [src] S2, S1 | Thickness of the eave edge = 0.18 m with a timber fascia board [est]. |
| Jhingati tile | 200×105 mm, overlapping like scales; ridge cover tiles | [src] S6 | Toon roof: draw the scale pattern in the detail atlas, one tile = 2 texels at LOD0 [est]. |
| Ridge ornament | Plain ridge tiles; occasionally a small finial over a shrine room | [est] | Never a temple *gajur* on a house. |
| Kitchen and smoke | Kitchen on the top floor; no chimney. Smoke escapes through roof tiles and the gajhya | [src] S4 | No chimneys on Newar houses. Optional smoke wisp at meal times [est]. |
| Roof terrace (*kausi*) | A cut-out terrace in one roof slope, about 2–3 m wide, with a low brick parapet | [est] (common on houses; also allowed by S3 annex 2.1) | 20–30% of traditional houses [est]. |

### 2.2 Facade grammar per storey

Floors are counted G, 1, 2, 3 (attic). Each storey is a row of **bays**. The bay count is `n = clamp(round(frontage / bayW), 1, 7)`, where `bayW` is 1.6 m [est], and the result is forced **odd** when frontage ≥ 4.5 m so that there is a centre bay.

| Storey | Use [src S4, S10] | Elements | Dimensions | Rules |
|---|---|---|---|---|
| Plinth | — | Stone cap, apron (*pikhā*), doorstep | Height 0.15–0.45 m; apron 0.6 m deep | Continuous along the row. It steps with terrain in Kirtipur. |
| **G (chhẽdī)** | Storage, workshop or **shop**; stairs (one flight, ladder-like) | **Shop variant**: a *dalan* timber frame of 2–4 posts, 0.10–0.15 m square [src S1], with wooden plank shutters or (modern) a steel roller shutter. **House variant**: a central carved door with 0–2 small windows | Door 0.75–0.90 m wide × **1.45–1.70 m high** (deliberately low) [est; low door: S10]. Carved frame 0.12 m wide; lintel and sill run **0.25–0.35 m past each jamb** ("ears") [est]. Small window 0.45×0.60 m | Shop share comes from real shop POIs inside the footprint, then the city rate (§6). Above the door: a painted deity panel or torana-like board, vermilion-smeared frame [est]. A Ganesh image or a dyo-twāḥ niche may sit beside the door [est]. |
| Floor band G/1 | — | Projecting brick cornice or protruding joist ends, or a moulded "dachi" band | Projection ≤ 0.23 m [src S3]; height 0.20–0.30 m [est] | One band at every floor line. In Bhaktapur, a carved timber band [est]. |
| **1 (mātã)** | Bedrooms | **Tikijhya** (*ankhijhya*, lattice) windows, one per bay, symmetric | 0.60–0.80 m wide × 0.80–1.00 m high, sill **0.35–0.45 m** above the floor (people sit on the floor) [est]. Double frame: the outer carved frame adds 0.15 m on each side and its lintel and sill extend 0.20 m past it [src S1: double frames, lintels well into the masonry] | Lattice: a diagonal or square grid of 20–30 mm members at 40–60 mm spacing, drawn as an opaque inset in the detail atlas (ASSET_MANIFEST `tikijhya`). |
| Floor band 1/2 | — | As G/1 | — | — |
| **2 (chwata)** | Living room and reception: the best room | **Sanjhya** (projecting bay window), centred: **3 units** (S8) on a 5–7 m facade; a long "san jhya" spanning almost the full facade on wide or rich houses (S1, S7). Side bays: tikijhya | Sanjhya 2.4–3.6 m wide × 1.1–1.4 m high, **projects 0.30–0.60 m** on carved brackets; seat (*jhya-lāḥ*) inside [src S1: "with seating framed within it"; dimensions est]. Full-width variant: frontage − 0.6 m | When frontage < 4.5 m, use a single-unit projecting window. Sanjhya shutters open **upward** (S8). In the cartoon style, draw the lattice and show a pot of marigolds on 10% of sills [est]. |
| Floor band 2/3 | — | Wall plate, strut feet | — | Strut feet sit on a timber wall plate about 0.3 m below the eave line [est]. |
| **3 / attic (baĩgaḥ)** | Kitchen, shrine room (*āgã*) | **Gajhya**: a projecting window under the eave, centred. Sometimes the *kausi* terrace instead | 0.9–1.4 m wide × 0.6–0.8 m high, projects 0.3–0.5 m [est] | Knee wall 0.6–1.2 m below the eave [est]. A 4th storey above an attic only comes from later extensions (§4). |
| Roof | — | Gable, struts, jhingati | 32° ± 3°, overhang 1.0–1.2 m | The gable end only shows at a block end or a gap. Party walls hide it inside a row. |

**Symmetry.** The facade is mirrored about the centre bay on G (if a house door), 1 and 2 [src S1/S8 "centre of a facade"; est for G]. Shops break symmetry on G.

**Repetition along a row.** Each plot gets its own floor heights jittered ±0.1 m and its own eave height. Rows show **steps of 0.2–0.6 m in the eave line** between neighbours [est]. S4 notes that neighbours often align floor levels to prevent pounding, so jitter is small (±0.1 m) inside a group and the larger step falls between groups [src S4 + est].

**Openings ratio.** The wall is solid: openings are about 15–25% of the facade area on 1–2 and 30–60% on a shop G [est; S1 gives wall density of 15–20% of plan area, which matches small openings].

### 2.3 Windows, doors and the peacock window

| Item | Newar name | Where | Generator piece | Size [est unless noted] | Cultural note |
|---|---|---|---|---|---|
| Lattice window | tikijhyā / ankhijhyā | 1st floor (also 2nd floor side bays) | `ghm_bld_newar_window_tikijhya_{a,b}` | 0.6–0.8 × 0.8–1.0 m + 0.15 m frame | "Most common" (S8) |
| Projecting bay | sãjhyā / sanjhya / san jhya | 2nd floor centre | `…_sanjhya_{a,b}` (3-unit), plus a new long variant `…_sanjhya_long` | 2.4–3.6 × 1.1–1.4 m, projects 0.3–0.6 m | Classic Newar window (S8) |
| Under-roof window | gājhyā | Attic, centre | new `…_gajhya_a` | 0.9–1.4 × 0.6–0.8 m | (S8) |
| Plain carved window | — | Any floor; later houses | `…_plain_{a,b}` | 0.8 × 1.4–1.8 m (S1: "about 800 mm", "almost floor to floor") | The by-law module is 3′×5′ (0.91 × 1.52 m) [src S3] |
| Five-unit window | pāsukhā jhyā | Bahal shrine facades only | new `ghm_bld_bahal_window_pasukha` | 2.5–3.5 × 1.0 m | Pancha Buddha symbolism (S8). Never on ordinary houses |
| Door | dhwākhā | Ground floor | `ghm_bld_newar_door_{a,b}` | 0.75–0.9 × 1.45–1.7 m | Low on purpose (S10) |
| Peacock window | mhaykhā jhyā | **Only** Pujari Math, Tachupal Tole, Bhaktapur (S16) | Hero piece at its real location | Hero; measure on site or from a licensed survey | A unique landmark. Never generic or scattered. |

### 2.4 Courtyard blocks: chowk, bahal (baha), bahi

| Type | What it is | Grammar | Sizes |
|---|---|---|---|
| Residential chowk | A block of row houses around a private or semi-public courtyard, reached by a low passage (*dhwākā*) through one house | Houses face the street **and** the courtyard. The courtyard face is the same grammar with fewer shops. Courtyard furniture: a well or tap, a small chaitya or shrine, grain drying, laundry lines | Courtyard 8×8 to 25×30 m [est]. Thimi's *nani* clan clusters are this type (S24) |
| **Bahal / baha** (*bahāḥ*, vihara) | A Buddhist monastic quadrangle, now mostly lived in by Shakya and Bajracharya families | 2-storey ranges on all four sides. The **shrine range (*kwāpā dyaḥ*) is opposite the entrance**, with a low wide door, a **torana**, a *pāsukhā* window above it, and often a small tiered "lantern" roof on top. One or more **chaityas** stand in the centre. Two stone lions flank the entrance passage. Stone or brick paving | OSM areas [osm]: Jana Baha 3,716 m²; Te Bahal 3,518; Machhindranath Bahal, Bungamati 2,574 (54×52 m); Yetkha Bahal 1,614; Bu Bahal 1,569; Guji Bahal 1,218; Karuna Bahi 1,158; Ta Bahal 641; Konti Baha 510; Kuthu Bahi 499; Chusya Bahal 405. **Median ≈ 500–650 m²** (about 22×25 m). Patan has **166** bahal-type spaces and 18 main bahals [src S15] |
| **Bahi** (*bahī*) | The older, more monastic type, often at the town edge | Raised plinth (0.6–1.0 m [est]) with an open colonnaded ground-floor gallery around the court, fewer houses attached, more brick and timber than plaster [est] | Karuna Bahi (I Bahi) 1,158 m²; Kuthu Bahi 499 m² [osm] |
| Palace chowk (durbar) | Courtyards of the Durbar Square palaces | Hero content, not generic | Lam Chok 205 m², Nucha Chok 184 m², Dahk Chok 421 m², Nyasal Chowk 464 m² [osm] |

Gameplay note (the owner allowed temple compounds to be entered): bahals and chowks are **public courtyards today**, so they can be walkable. The shrine interior (*kwāpā dyaḥ*) stays closed: show a doorway with a curtain and an offering platform, not an interior. Shoes-off and no-entry rules belong in curated `attrs` (CONTENT_COVERAGE §3), not in the generator.

OSM gap: building multipolygons with holes become roofed-over courtyards in W1 (163 cases, CONTENT_COVERAGE B1). Bahals mapped as `landuse=religious`, `leisure=common` or `place=square` areas should **suppress roof generation** over their area and hand it to a courtyard decorator.

---

## 3. City by city

Footprint medians and percentiles come from the pack (rotated bounding box of each footprint). Levels come from OSM `building:levels` where tagged. Bounding boxes are in Appendix A: they are rough historic-core rectangles **[est]** and include some modern fringe.

### 3.1 Numbers per area [osm]

| Area | Buildings | Footprint short side p25 / **p50** / p75 (m) | Footprint area p50 (m²) | Built cover of box | Tagged levels: % tagged, mean | Tagged levels 1/2/3/4/5/6/7+ (%) | Shop + food + lodging POIs per km² | Typical street width (OSM `width`, median) |
|---|---|---|---|---|---|---|---|---|
| Kathmandu core (Asan, Indra Chowk, Durbar Sq) | 11,556 | 4.4 / **5.7** / 7.5 | 48 | 37% | 4.4%, **4.56** | 7/6/12/17/**31**/15/11 | ~494 | tertiary 4 m, residential 4 m, secondary 7 m, primary 14 m |
| Thamel | 2,533 | 5.8 / **7.5** / 9.9 | 85 | 40% | 8.3%, **3.91** | 10/12/17/21/**23**/12/5 | **~2,064** | residential 4–7 m, secondary (Thamel Marg) 7 m |
| Patan core | 9,257 | 5.5 / **7.3** / 9.6 | 78 | 31% | 4.5%, **3.08** | 21/17/**23**/20/14/4/2 | ~233 | residential **5 m** (n = 110), tertiary 6 m, secondary 6 m |
| Bhaktapur core | 10,669 | 3.7 / **4.9** / 6.6 | **38** | 24% | 0.3% (too few) | — | ~133 | secondary **4 m** (n = 26), primary 7 m |
| Kirtipur core | 4,097 | 5.0 / **6.7** / 8.3 | 66 | 20% | 1.2%, 2.84 | 19/25/23/21/12/0/0 | ~309 | residential 4–6 m; **1.8 km of steps** in the box |
| Thimi core | 1,579 | 5.0 / **6.8** / 9.1 | 70 | 12% | 2.8%, 2.53 | 39/18/14/14/14/2/0 | ~235 | residential 3 m, secondary 8 m |
| Bungamati | 654 | 5.0 / **6.6** / 8.5 | 59 | 6% | 1.7% | (n = 11) | ~9 | mostly track and path |
| Khokana | 491 | 3.6 / **4.9** / 6.9 | **35** | 4% | 1.4% | (n = 7) | — | track 5 m (the main street) |
| Panauti | 1,607 | 4.8 / **6.2** / 7.9 | 55 | 8% | 0.5% | (n = 8) | ~57 | residential, living_street |
| Metro outside cores (Ring Road box) | 135,490 | 5.7 / **7.7** / 9.6 | 82 | — | 7.4%, 3.0 | 15/18/**38**/18/7/3/2 | — | — |
| Rest of the valley (rim, villages, suburbs) | 364,328 | 5.4 / **7.0** / 8.8 | 71 | — | 2.0%, 2.15 | **44**/18/23/11/3/1/0 | — | — |

How to read this:
* **Bhaktapur and Khokana are mapped house by house** (footprint median 35–38 m², short side 4.9 m). Other cores often merge several houses into one footprint. In Kathmandu and Patan the generator should **split long footprints into plots** of 4–8 m along the frontage, seeded [est], so the facade rhythm reads correctly.
* OSM levels are tagged on only 0.3–8% of core buildings, mostly by post-2015 survey campaigns that favoured newer or institutional buildings. Treat them as **upper-biased** priors.
* **Building structure** (valley, n = 10,706 tagged): brick in cement mortar 44%, non-engineered RCC 37.5%, engineered RCC 10%, brick in mud mortar 4.4%, other 4%. In cores: Patan (n = 226) brick-cement 46%, RCC 38%, brick-mud 7%; KTM core (n = 105) brick-cement 30%, RCC 50%, brick-mud 10%; Thimi (n = 40) RCC 52%; Kirtipur (n = 40) RCC 40%. S20: 48% of Kathmandu houses are non-engineered RCC.
* **Roof shape where tagged**: valley 17,592 (3.3%): flat 67%, mixed 19%, gabled 9%, double_pitch 1.2%, hipped 0.3%. Patan core: flat 73%, gabled 15%. KTM core: flat 77%, gabled 6%. Thamel: flat 62%, complex 16%.
* **Roof material where tagged** (valley, 17,526): concrete 55% (including the 807 "concerte" misspellings), tin/metal/CGI 40%, clay or roof tiles 0.9%. Jhingati is almost never tagged. Do not take tile share from OSM. Use the per-city estimates in §6.
* **`building:part` per core**: KTM 249, Patan 368, Bhaktapur 44 (temples). See the temple research.
* **Rooftop tanks and solar**: OSM nodes in the cores are ~0 (the 5,979 valley `storage_tank` tags are a Banepa campaign, where they cover 35% of mapped buildings; rooftop solar 8%; CONTENT_COVERAGE B25). Use the rates in §5.

### 3.2 Character per city

| City | Brick and colour | Wood | Roofs | Heights and skyline | Streets and squares | Distinctive cues to generate | Sources |
|---|---|---|---|---|---|---|---|
| **Bhaktapur (Khwopa)** | The best-kept fair-face **dachi apa**: deep red-brown, glossy, hair-thin joints. More than 90% of buildings are traditional URM (S4, pre-2015). Lime plaster is rare | Natural dark sal, unpainted; richest tikijhya and sanjhya; long carved timber floor bands | Highest tile share in the valley; 35° pitch, 1.2 m eaves (S2) | 3–5 storeys: about 32% 3, 51% 4, 13% 5, 3% 6 in the Tachupal sample (estimate read from the S13 figure, n ≈ 104); by-law ≤ 35 ft around the three squares (S11) | Narrow brick lanes 3–5 m (OSM secondary median 4 m); brick herringbone paving; **squares**: Durbar, Taumadhi (Nyatapola), Tachupal (Dattatreya); pottery squares (Talako, Bolachhen) with pots drying | Pots and grain drying on plinths, ground-floor pottery wheels near the pottery squares, dyochhen (god houses), *phalcha* resting pavilions at corners, hiti and pond edges (Siddha Pokhari) | S2, S4, S11, S13 |
| **Patan (Lalitpur, Yala)** | Dachi apa red with more mixed and rebuilt facades; some lime-washed ground floors; many brick-veneer RCC | Dark carved wood; metalwork (copper and brass) shops in the Buddhist quarters | Mixed: tiles on old houses, flat terraces on rebuilt ones (73% of tagged roofs are flat) | 3–5 storeys; modern tower blocks at the edge | Residential streets 5 m median (n = 110); the **densest web of bahals** (166, S15) and hitis (Manga Hiti, Sundhara); Mangal Bazaar | Bahal entrances with stone lions every 60–120 m along main lanes [est]; metal-shop fronts with hanging pots; Buddhist prayer wheels in bahals | S7, S15; [osm] |
| **Kathmandu old core (Yẽ)** | The most altered: rebuilt in RCC, plastered and painted, or clad in glazed tile. Traditional brick fronts survive in clusters (Itum Bahal, Kel Tole, Jana Baha) | Carved windows survive here and there, often painted **dark brown or black** [est] | **Flat roofs dominate** (77% of tagged); tiles mainly on temples and bahals | The **tallest core**: tagged mean 4.56; 5 storeys is the mode (31%); 7+ is 11% | Bazaar lanes 3–5 m (Asan, Indra Chowk, Makhan Tole); 6-way tole crossings with temple islands; **very dense shopfronts**, cloth and spice stalls | Glass-front and shutter shops with hanging goods; overhead wire tangles; a **hybrid** house above an old timber ground floor; balconies on the rebuilt upper floors | [osm]; S20 |
| **Kirtipur** | Dusty red brick with some stone in the plinths; sits on a ridge | Medium carving | Mixed; small hipped and gabled roofs on the slope | 2–4 storeys (tagged mean 2.84); the skyline steps up the hill | Ridge-top town: **stepped lanes** (1.8 km of steps in the box), terraces, gates (Samal, Palifa, Dhwakasi), ponds. By-law: ≤ 35 ft, ≤ 4 storeys, fair-face brick, no plaster (S3) | Steps and ramps instead of flat lanes; city gates; views out to the valley; university campus (modern) at the foot | S3; [osm] |
| **Madhyapur Thimi** | Brick, weathered and lighter, with more plastered and rebuilt houses | Plainer | Mixed | 2–4 storeys (tagged mean 2.53) | Clan courtyards (*nani*), **Pottery Square (Digu Tole)**, Balkumari temple, Bode at the edge | Pots and kilns: rows of drying pots on plinths and in courtyards; terracotta masks and figurines on stalls | S24 |
| **Bungamati** | Brick and mud plaster in ochre and brown; heavy 2015 damage, much RCC rebuilding | **Woodcarving village**: carving workshops on ground floors [est, widely reported] | Tile and flat | 3–5 storeys after rebuilding (guidance said 10 m and 2–3 storeys; people built 4–6, S12) | Town on a spur; **Machhindranath Bahal** courtyard 54×52 m (2,574 m²) with the Rato Machhindranath shikhara | Carvers' stalls; the 12-year chariot festival route [festival content elsewhere] | S12; [osm] |
| **Khokana** | Brick and mud, the least plastered; fields up to the houses | Plain | Tile and flat | Mapped houses are small (35 m² median, 4.9 m frontage) | One **wide brick-paved main street** with continuous house fronts and *phalcha*; on the UNESCO Tentative List since 1996 | **Mustard-oil presses** (traditional oil-mill houses); drying mustard and grain on the street; a 2015 rebuild in brick-veneered RCC (S22) | S12, S22; [osm] |
| **Panauti** | Red brick, well kept | Fine woodwork on old houses and temples | Jhingati | **Two-storey lanes** (S23) with some 3–4 | Narrow, twisting lanes at the confluence of the Roshi and Punyamati; **Indreshwar** (1294) the tallest building | A compact old town; riverside ghats and patis | S23 |

Other Newar towns in the valley, currently outside the pipeline's Newar zones (CONTENT_COVERAGE B4): **Bode, Sankhu, Chapagaun, Pharping, Thecho, Sunakothi, Harisiddhi, Lubhu, Siddhipur, Tokha, Banepa, Dhulikhel**. Default them to the **Kirtipur profile**, using the Khokana profile for small farm villages [est].

---

## 4. Hybrid and post-2015 houses (the real majority in cores)

The usual sequence, from S1 ("vertical extension… with concrete frames above original masonry structure"), S13 (eaves type marks the extension period) and S12:

1. **Original**: G + 2 + attic, tiled gable (§2).
2. **Top-up**: the tiled roof is removed and 1–2 RCC or brick floors are added with a flat terrace. A **tiled eave hood** is often re-applied at the old eave line, or at the new top floor (S13 "eaves types"; Kirtipur by-law requires a sloped jhingati hood on any 3rd-floor or higher cantilever, S3).
3. **Rebuild (2015 onward)**: a full RCC frame, **4–6 storeys**, faced on the street with dachi-style brick tiles (veneer) in heritage zones (Khokana guideline: brick facades, tile roofs, traditional elements, "rarely followed", S22), or plastered and painted outside them.

**Generator variant `NEWAR_HYBRID`** [est, derived from the above]:

| Part | Rule |
|---|---|
| Lower storeys (G–2) | §2 grammar, with storey heights of 2.4 / 2.4 / 2.4 m (rebuilds use slightly taller storeys) |
| Upper storeys (3–5) | Brick veneer (heritage zones) or plaster paint (§5 palette). Plain windows 0.8 × 1.4 m, aluminium or timber; a balcony with a steel railing on the top floor in 30% of cases |
| Eave hood | A single-slope jhingati or corrugated hood, 0.6–0.9 m deep at 25–30°, at the floor line where the old eave was or at the top floor, on 2–4 struts |
| Roof | Flat terrace with a 0.9 m parapet; stair cabin (*mumty*) 2.4 m high; water tank (§5); laundry lines; potted plants |
| Seams | A visible change of brick tone or a plaster line where old and new meet (20% of cases) |

Damage context: the 2015 Gorkha earthquake fully damaged **604,930** houses nationwide (602,257 private) and partly damaged 288,856. By January 2020, 62% had been rebuilt and 25% were under construction (S21). **Render the world in its present, rebuilt state.** Do not show earthquake ruins (tone: 9+/PEGI 7, respectful). Kathmandu Durbar Square heroes after 2021 (Kasthamandap rebuilt) belong in the temple research.

---

## 5. Modern RCC houses (`MODERN_URBAN`)

| Parameter | Value | Label |
|---|---|---|
| Storey height | 2.9–3.0 m floor-to-floor (ceiling ≥ 2.5 m outside cores, Kirtipur general zones) | [src] S3; ASSET_MANIFEST §4 |
| Commercial ground floor | 3.0–3.3 m, with a roller shutter 2.4–2.7 m high and 2.5–3.5 m wide per shop, 1–3 shops per frontage | [est] |
| Frame | Columns 9×9 to 12×12 in (0.23–0.30 m), at 3–4.5 m spacing. Beams show as a slab edge 0.15 m on the facade | [src] S20; [est] spacing |
| Infill | Brick in cement mortar, plastered and painted (most); exposed brick on the side and back walls (very common: the street side is painted, the sides are raw) | [est], consistent with the OSM structure tags |
| Storeys | Metro: 3 (38%) > 2 ≈ 4 (18%) > 1 (15%) > 5 (7%) > 6 (3%) > 7+ (2%); rest of the valley: 1 (44%) > 3 (23%) > 2 (18%) > 4 (11%) (tagged OSM, §3.1) | [osm] |
| Floor plan | Footprint median 82 m² in the metro (short side 7.7 m) | [osm] |
| Facade | Street facade: a central bay of balconies or a stair window, flanked by windows 1.2 × 1.35 m; 2–3 windows per storey on 7–8 m | [est] |
| Balconies | Cantilever 0.9–1.2 m with a steel railing or brick jali, on 40–60% of houses; usually on upper floors | [est] |
| Window shades (*chhajja*) | A concrete sunshade 0.3–0.45 m deep over each window | [est] |
| Glazed-tile fronts | Glossy ceramic tile cladding (white, maroon, blue) on 10–15% of street facades | [est] |
| Rooftop | Flat terrace, 0.9–1.0 m parapet or railing; a stair cabin covering 8–15% of the roof area; **rebar stubs** sticking 0.6–1.0 m out of the roof columns (left for the next floor) on 30–50% of houses on growing streets | [est] |
| Water tanks | A black or blue polyethylene tank, 500–2,000 L; a 1,000 L tank is about Ø1.1 × 1.2 m (S25), on a steel or brick stand 0.5–1.5 m high. **Rate: 60–80% of RCC roofs, 1–3 tanks each** [est]. The only measured rate in OSM is 35% of mapped buildings in the Banepa campaign, which under-counts | [src] S25; [est] rate; CONTENT_COVERAGE B25 |
| Solar water heater | An evacuated-tube rack (about 2 × 1.5 m, 15–20 tubes) on 15–25% of roofs [est]. OSM Banepa campaign: rooftop solar 8% (CONTENT_COVERAGE B25) | [est] |
| Other roof props | TV dishes 10%, corrugated (CGI) sheds (SKILLION) 20%, potted plants, laundry lines, prayer flags (Buddhist households, 5–10%) | [est] |
| Plot setbacks | Detached houses on the rim: about 1.5 m sides and back, about 1 m to the road (S3 general zones). Row-like in cores | [src] S3 |

---

## 6. Per-city generator profiles

These profiles are **seeded priors** used when a building has no tag. Real tags win, after the plausibility gate (CONTENT_COVERAGE D4). Percentages are **[est]** unless they cite §3.1.

| Profile | Applies to | Levels prior (2/3/4/5/6+) % | Archetype mix (NEWAR / NEWAR_HYBRID / MODERN) % | Shop ground floor % (when no POI) | Tile-roof share of NEWAR + HYBRID | Brick tone | Notes |
|---|---|---|---|---|---|---|---|
| `bhaktapur` | Bhaktapur core | 2 / 32 / 51 / 13 / 2 (from S13 figure, rounded) | 60 / 30 / 10 | 25 on squares and main lanes, 10 elsewhere | 70 | `brick.bkt` | Eave 1.2 m; carved timber floor bands; pottery props near the pottery squares |
| `patan` | Patan core | 22 / 29 / 25 / 17 / 7 ([osm], excluding 1-storey tags) | 35 / 40 / 25 | 30 main lanes, 10 elsewhere | 45 | `brick.ptn` | Bahal entrances; metal-shop props |
| `kathmandu_core` | Asan, Indra Chowk, Durbar Sq, Kel Tole, Jana Baha | 6 / 13 / 18 / 34 / 29 ([osm], excluding 1-storey tags) | 20 / 35 / 45 | 70 on bazaar lanes, 40 elsewhere | 25 | `brick.ktm` | Painted wood; wires; glazed-tile fronts |
| `thamel` | Thamel, Jyatha, Chhetrapati | 10 / 17 / 25 / 28 / 20 | 5 / 15 / 80 | 85 | 10 | — | Sign rules (§7) |
| `kirtipur` | Kirtipur core and other Newar towns by default | 20 / 30 / 30 / 18 / 2 | 40 / 35 / 25 | 20 | 50 | `brick.ktp` | Stepped lanes; stone plinths |
| `thimi` | Thimi, Bode | 20 / 30 / 30 / 18 / 2 | 45 / 30 / 25 | 20 | 50 | `brick.thm` | Pots and kilns |
| `bungamati` | Bungamati | 10 / 30 / 35 / 20 / 5 (post-2015 rebuild, S12) | 35 / 40 / 25 | 15 (carving workshops) | 45 | `brick.vil` | Mud-plaster patches; carving props |
| `khokana` | Khokana | 15 / 40 / 30 / 13 / 2 | 50 / 35 / 15 | 10 (oil-mill houses) | 55 | `brick.vil` | Wide main street; mustard drying in season |
| `panauti` | Panauti core | 30 / 45 / 20 / 5 / 0 | 60 / 25 / 15 | 15 | 70 | `brick.pnt` | Two-storey lanes (S23) |
| `metro` | KTM–Lalitpur outside cores | 18 / 38 / 18 / 7 / 5 ([osm]; the 15% 1-storey share goes to sheds) | 0 / 0 / 100 | Main roads 80, side streets 20 | — | — | §5 |
| `rim` | Rest of the valley | 18 / 23 / 11 / 3 / 1 ([osm]; 44% 1-storey) | 0 / 0 / 100, with HILL_VILLAGE beyond the built-up edge | 10 | — | — | CGI roofs common (tin/metal is 40% of tagged roof material) |

Profile choice: polygons traced on the real historic cores (D4 already plans "Newar zones traced from real historic cores"). Until those exist, the Appendix A rectangles work as stand-ins.

---

## 7. Thamel and other tourist streets

| Parameter | Value | Label |
|---|---|---|
| Commercial POI density | About **2,064 shops, restaurants, cafés, bars and lodgings per km²** in the Thamel box, against about 494 in the KTM core and about 233 in Patan | [osm] |
| Building height | Tagged mean 3.91 levels; 5 storeys most common (23%). Many 5–7-storey guest houses | [osm] |
| Footprints | Larger (median 85 m², short side 7.5 m); hotels up to several hundred m² | [osm] |
| Street width | Lanes 3–5 m wall to wall, Thamel Marg 7 m; much of the core is pedestrianised in the evening [est] | [osm] + [est] |
| Signage | **3–8 signboards per frontage** [est]: flat fascia boards over each shop (0.6–1.0 m tall); **vertical blade signs** 0.4–0.6 m × 1.5–3 m projecting 0.5–1.0 m from upper floors; banners. Generic names only, no real brands (ASSET_MANIFEST signage policy) | [est] |
| Rooftop restaurants and terraces | On **30–40% of buildings** [est]: umbrellas, plastic chairs, potted plants, string lights, a small corrugated or bamboo canopy | [est] |
| Overhead | Dense wire bundles crossing every 15–25 m; **prayer-flag strings** across lanes every 20–40 m [est] in the order blue, white, red, green, yellow | [est] |
| Ground floor | Almost 100% shops: trekking gear, pashmina, singing bowls, bookshops, money changers. Goods hang outside up to 2.5 m high | [est] |
| Night | Neon and LED signs as emissive (night windows already use an emissive mask) | — |

---

## 8. Rana-era neoclassical palaces (new archetype `RANA_PALACE`, value 20 in F1)

General style (S17, S19): **white or cream lime-stucco** symmetrical blocks built 1850–1950; **Corinthian or Grecian columns**, porticos, pediments, **balustraded parapets**, **French windows** with fanlights, often around 1–8 courtyards; Nepali carved wood inside; gardens with urns, pergolas and fountains.

| Parameter | Value | Label |
|---|---|---|
| Storeys | 2–4, mostly 3 | [est] (Gaddi Baithak tagged 3 levels [osm]) |
| Floor-to-floor | G 4.5 m (rusticated base), piano nobile 5.0 m, 3rd 4.0 m | [est] |
| Bay rhythm | 3.0–3.6 m; a central portico of 3–5 bays carrying a pediment; projecting end pavilions | [est] |
| Columns | Corinthian, about 0.5–0.7 m diameter on the portico; pilasters between bays elsewhere | [est] |
| Windows | French windows 1.2 × 3.0 m with semicircular fanlights and white trim; louvred shutters | [est] |
| Cornice and parapet | Cornice projecting 0.4–0.6 m; balustrade 1.0 m with urns at the piers | [est] |
| Roof | Flat or low-pitched behind the balustrade; some CGI hips in the back wings | [est] |
| Colour | White to cream stucco ("white-plastered", S19); trim brighter white | [src] S19; [est] hex |

Heroes measured in the pack (footprints, rotated bounding box) [osm]:

| Building | OSM | Footprint | Notes |
|---|---|---|---|
| Singha Durbar main block | r1574200 | 6,583 m², **104.8 × 85.1 m**, inside a 40 ha compound (w677032175, 426,439 m²) | 8 courtyards and 1,700 rooms before the 1973 fire (S17). **The western wing burned on 9 Sep 2025** (S17). Decide with the PO whether to show it intact (pre-2025) or under reconstruction. This file recommends **intact, with scaffolding on the west wing**, and never fire or damage. |
| Kaiser (Keshar) Mahal | r1650246 | 3,529 m², **81.6 × 53.9 m** | Garden of Dreams next to it: w85650618, 7,881 m² in OSM (6,895 m² per S18); 3 of the original 6 pavilions survive |
| Gaddi Baithak (KTM Durbar Sq, 1908) | w247501389 | 848 m², **46.4 × 36.3 m** | Tagged `height=1`, which is a plinth error: needs a height override (CONTENT_COVERAGE L9). A white neoclassical front facing the square |
| Babar Mahal | w512547750 (largest) | 1,301 m², 50.5 × 33.6 m | Restaurant and shopping complex "Babar Mahal Revisited"; Department of Roads |
| Narayanhiti | — | Main block 1960s modernist (Benjamin Polk), **not Rana style** [est from the general record] | Museum compound w24758025 (385,854 m²). Use INSTITUTIONAL with a tower roof hero, not RANA_PALACE |

Other Rana palaces for the archetype (S17): Sheetal Niwas (1923), Harihar Bhawan (1910s, "white plaster exterior"), Jawalakhel Durbar (1897), Thapathali Durbar (1854, 4 courtyards), Bagh Durbar. Smaller **"Rana-style" houses** (2–3 storeys, stucco, pilasters, balustrade, green or brown shutters) are common in Dillibazar, Naxal, Thamel and Lazimpat [est]. Add them as a 5% minority in `metro` with `start_date` before 1960 when tagged.

---

## 9. Colour palettes (bright cartoon interpretation)

Rules: keep each family's hue, then raise saturation by about 15–25% and value by about 10% compared with the real material, so the colours read on a phone in sunlight. Every value is an **[est]** design choice anchored on the cited descriptions: dachi apa is "darker red… glossy" (S5); jhingati comes in "carmine, scarlet-red, vermillion and tangerine orange" (S6); Rana buildings are "white-plastered" (S19). The base swatches already in ASSET_MANIFEST §3 (`w.brick.newar` #B4543A, `w.tile.roof` #9E4630, `w.paint.*`) are kept as the "neutral" entries.

### 9.1 Newar brick per city (face / mortar-shadow / highlight)

| Token | City | Face | Joint and shadow | Highlight (wet sheen) |
|---|---|---|---|---|
| `brick.bkt` | Bhaktapur, deepest and glossiest | `#B4432F` | `#6E2219` | `#D4664A` |
| `brick.ptn` | Patan | `#BF5236` | `#74291D` | `#DE7352` |
| `brick.ktm` | Kathmandu core (patchier) | `#C25A3C` | `#7A3022` | `#E07A57` |
| `brick.ktp` | Kirtipur, dusty | `#B85A40` | `#713526` | `#D47A5C` |
| `brick.thm` | Thimi, lighter and orange | `#C9663F` | `#7E3A22` | `#E8885E` |
| `brick.vil` | Bungamati and Khokana (ma apa and mud) | `#B86A4A` | `#76402C` | `#D48A68` |
| `brick.pnt` | Panauti | `#BC553B` | `#742C1F` | `#DA7556` |
| `brick.newar` | Neutral (existing) | `#B4543A` | — | — |

### 9.2 Roof tiles (jhingati), seeded 4-way mix per roof

| Token | Hex | Share |
|---|---|---|
| `tile.carmine` | `#B8322B` | 25% |
| `tile.scarlet` | `#D2452E` | 35% |
| `tile.vermilion` | `#E2603A` | 25% |
| `tile.tangerine` | `#EC8A3E` | 10% |
| `tile.aged` (moss and soot patches) | `#6F5A3C` | 5% (as patches) |
| `tile.ridge` | `#9E3A26` | ridge row |

### 9.3 Wood, metal and ritual colours

| Token | Hex | Use |
|---|---|---|
| `wood.sal.dark` | `#4A2C1C` | Window and door frames, struts (Bhaktapur, Patan) |
| `wood.sal.mid` | `#6B4129` | Lattice infill, floor bands |
| `wood.sal.light` | `#8E5B37` | Freshly restored or new carving (Bungamati workshops) |
| `wood.painted.black` | `#2B221D` | KTM core painted windows (about 30% there) |
| `wood.painted.brown` | `#5A3222` | KTM core, Thimi |
| `metal.brass` | `#D9A93A` | Door knockers, Patan shop pots, bells |
| `metal.copper` | `#C46A3A` | Patan metal shops |
| `sindoor.red` | `#E23B2A` | Vermilion on door frames and shrine stones |
| `marigold` | `#F6A21B` | Garlands, sill flowers |
| `lime.white` | `#F4EFE0` | Lime-washed plinths and the lower walls of some houses |
| `ochre.wash` | `#E0A845` | Ochre-washed walls (Bungamati, Khokana patches) |
| `mud.plaster` | `#C9A27A` | Mud plaster patches (villages) |
| `stone.plinth` | `#9A948A` | Plinths, doorsteps, paving edges |
| `brick.paving` | `#A8553E` | Herringbone street brick (matches `ghm_rd_brick`) |

### 9.4 Modern RCC (pastel paint, seeded share per house)

| Token | Hex | Share [est] | Note |
|---|---|---|---|
| `paint.white` | `#F7F6F0` | 16% | |
| `paint.cream` | `#F6E6BE` | 14% | |
| `paint.yellow` | `#FFD95A` | 9% | existing `w.paint` |
| `paint.peach` | `#F8B48A` | 9% | |
| `paint.pink` | `#F49AC1` | 8% | existing |
| `paint.sky` | `#7FC8F8` | 8% | existing |
| `paint.mint` | `#9FE2B8` | 7% | |
| `paint.lime` | `#B6E05A` | 5% | existing |
| `paint.turquoise` | `#3CC9C0` | 5% | existing |
| `paint.lavender` | `#B79CE8` | 4% | existing |
| `paint.orange` | `#F59A3B` | 4% | |
| `raw.concrete` | `#ABA79F` | 6% | unfinished upper floors |
| `raw.brick` | `#B8654A` | 5% (street facade); **side and back walls of 60% of houses** | |
| Trim (slab edges, chhajja) | white `#FFFFFF` or a darker tint of the body colour (−20% value) | — | 50/50 |
| Railing | `#2E3440` (black steel), `#3D7CC9` (blue), `#C9A13A` (golden) | 60 / 20 / 20 | |
| Shutter | `#8C949C` (galvanised), `#3D7CC9`, `#C9433A`, `#3FA35C` | 55 / 15 / 15 / 15 | |
| Tanks | `#2A2A2E` black, `#2F6FD6` blue, `#E9E4D4` cream, `#F2C230` yellow | 55 / 25 / 15 / 5 | the first two exist as `w.tank.*` |
| CGI roof sheds | `#3D7CC9`, `#3FA35C`, `#C9433A`, `#A2603A`, `#B9BEC3` (bare) | 20 each | the first four exist as `w.metal.roof.*` |

The OSM `building:colour` tags (141 in the valley: white 40, red 28, brown 23, yellow 16, blue 6, grey 6) agree with this ranking. The sample is very small.

### 9.5 Rana palaces

| Token | Hex |
|---|---|
| `rana.stucco.white` | `#F8F5EC` |
| `rana.stucco.cream` | `#F1E3BE` (Kaiser Mahal and the Garden of Dreams pavilions lean to cream [est]) |
| `rana.trim` | `#FFFFFF` |
| `rana.shadow` | `#D9CDB0` |
| `rana.shutter.green` | `#4E7D52` [est] |
| `rana.roof.cgi` | `#7F8A93` |
| `rana.gilt` (lion crowns, finials) | `#E0B13E` |

### 9.6 Thamel and street colour

| Token | Hex | Use |
|---|---|---|
| `sign.red` | `#E8483A` | Sign boards (matches `ui.ribbon`) |
| `sign.yellow` | `#FFD23F` | |
| `sign.blue` | `#2F7DE1` | |
| `sign.green` | `#2FA84F` | |
| `sign.white` / `sign.ink` | `#FFFFFF` / `#1E1E1E` | |
| Prayer flags (fixed order) | `#2E6FD8` blue, `#FFFFFF` white, `#D93A2B` red, `#2E9E4F` green, `#F2C230` yellow | Order matters: blue, white, red, green, yellow |

---

## 10. Implementation checklist (data → generator)

1. **Archetype choice.** Inside a city profile polygon: NEWAR, NEWAR_HYBRID or MODERN by the profile mix, seeded per building, and overridden by tags. `building:structure=*reinforced_concrete*` → HYBRID or MODERN; `load_bearing_brick_wall_in_mud_mortar` → NEWAR. `roof:shape=flat` → HYBRID or MODERN; `gabled` or `hipped` → NEWAR. Shops inside the footprint (8,712 valley shops are inside footprints, CONTENT_COVERAGE B19) → shop ground floor.
2. **Plot split.** Split frontages longer than 9 m (KTM, Patan) into plots of 4–8 m, so each plot gets its own storey heights, eave step and palette draw.
3. **Storey stack.** NEWAR: 2.40 / 2.25 / 2.25 / 2.10 m. HYBRID: 2.40 ×3, then 2.8 m for the RCC floors. MODERN: 3.0 m (3.2 m on a shop G). RANA: 4.5 / 5.0 / 4.0 m.
4. **Bays.** `n = clamp(round(frontage / 1.6), 1, 7)`, forced odd when frontage ≥ 4.5 m; the centre bay on floor 2 gets the sanjhya when frontage ≥ 4.5 m.
5. **Roof.** NEWAR: a gable with its ridge parallel to the frontage edge, 32° ± 3°, overhang 1.0–1.2 m, struts every 1.2–1.8 m, jhingati mix from §9.2. HYBRID: a flat terrace plus a tiled eave hood at 0.6–0.9 m. MODERN: flat plus props (§5).
6. **Courtyards.** Do not roof bahal or chowk areas, or the holes of building multipolygons. Decorate them with a chaitya, a well or tap, paving and, for bahals, the shrine range (§2.4).
7. **Height sanity.** A NEWAR house with 4 storeys and a 6 m depth comes to about 9.0 m at the eave and 11.1 m at the ridge (rise = 3 m × tan 35°). That fits the 35 ft (10.5 m) cap measured to mid-roof (S3). Flag anything taller than 6 storeys inside the Bhaktapur, Kirtipur or Panauti profiles as TAG_SUSPECT unless tagged.
8. **Triangle budget.** The NEWAR cap is 5,000 tris (ASSET_MANIFEST §4). Order of dropping ornament: lattice relief, then struts (fall back to a fascia texture), then floor bands. Never drop the sanjhya, because it carries the identity.
9. **Never generate** the peacock window, palace windows, temple struts with imagery, or temple *gajur* on houses. Those are hero-only.

---

## 11. Open questions for the product owner and cultural review

1. **Singha Durbar**: show it intact (pre-September 2025), or with reconstruction scaffolding? This file recommends intact with light scaffolding on the west wing. Never show fire.
2. **Floor and window names in UI text**: pick one romanisation (*sanjhya* vs *sãjhyā*; *tikijhya* vs *ankhijhya*). This needs a Newar-speaking reviewer (Nepal Bhasa).
3. **Bahal interiors**: courtyards walkable, shrine rooms closed (recommended). Confirm with the curated `entry_rule` design.
4. **Per-city share estimates** in §6 should be checked against a 1-hour street-view or photo count per city, for example 50 facades each in Bhaktapur, Patan, KTM core and Kirtipur. That is cheap and would replace the largest [est] values.

---

## Appendix A. OSM and pack method (reproducible)

* **Inputs**: `pipeline/data/raw/osm/nepal.osm.pbf` (2026-10-02) with pyosmium (`locations=True`, `flex_mem`); and `pipeline/build/regions/kathmandu_valley/qa/buildings.geojson` (pack decode, 542 k buildings), read line by line with shapely 2.1.
* **Valley box**: 85.18–85.58 E, 27.55–27.83 N. A building is assigned by the mean of its ring's vertices.
* **Area rectangles** (lon min, lat min, lon max, lat max) **[est]**: Thamel 85.306, 27.712, 85.316, 27.719 · KTM core 85.302, 27.698, 85.316, 27.712 · Patan core 85.316, 27.664, 85.332, 27.682 · Bhaktapur core 85.418, 27.667, 85.442, 27.677 · Kirtipur core 85.271, 27.672, 85.284, 27.684 · Thimi core 85.380, 27.676, 85.392, 27.686 · Bungamati 85.295, 27.624, 85.305, 27.633 · Khokana 85.290, 27.632, 85.299, 27.640 · Panauti 85.506, 27.579, 85.518, 27.590 · Metro (Ring Road box) 85.29, 27.67, 85.36, 27.74, excluding the earlier boxes in the pack table. In the OSM level table, "ktm_ringroad" includes the KTM core, Thamel and northern Patan.
* **Footprint dimensions**: the minimum rotated rectangle in a local equirectangular metre frame; the short side is the frontage proxy.
* **Densities**: POIs = nodes with `shop=*`, `amenity` ∈ {restaurant, cafe, bar, fast_food} or `tourism` ∈ {hotel, guest_house, hostel}, divided by the rectangle area (Thamel 0.763 km², KTM core 2.135, Patan 3.138, Bhaktapur 2.615, Kirtipur 1.700, Thimi 1.308, Panauti 1.438). Built cover = the sum of footprint areas divided by the rectangle area.
* **Street widths**: the `width` tag on `highway=*` ways whose vertex mean falls in a rectangle (n is small; shown per class).
* **Named courtyards**: closed ways whose name matches `bah(a|al|il|i)|chowk|chok|square|dabali|vihar`; the area is the shoelace area.
* **Palaces**: closed ways whose name matches the palace list; hero footprints are the largest pack footprints inside hand-drawn boxes around each compound.
* **Caveat**: `start_date` values in OSM mix BS and AD years (`196x`, `199x`, `200x` prefixes), so they were not used.
* The scripts were throwaway, written to the session scratchpad and not committed. They are short enough to rebuild from this appendix. Port them into D13 (coverage report) if the numbers need refreshing.
