# Ghumante OSM data census (Nepal extract 2026-10-02)

Generated 2026-10-05T03:30:44Z by `census.py` (one relations read + one nodes/ways pass over the 468 MB PBF; timings {'prepass_relations_s': 3.3, 'nodes_s': 18.4, 'main_pass_s': 215.8, 'scan_total_s': 219.1, 'relations_post_s': 1.3}). Machine-readable numbers: `census.json` (same folder). Scopes: **Nepal** = whole extract; **Valley** = bbox [85.18, 27.57, 85.56, 27.82]; **Core** = kathmandu_core bbox [85.283, 27.69, 85.375, 27.735]; **Pack** = kathmandu_valley pack bbox [85.18, 27.55, 85.58, 27.83] (only in the JSON). Ways are placed by vertex mean, lengths clipped by segment midpoint, routes by member length.

## Key findings

This section was written by hand from the numbers below. The vision is that every place, object, landmark, monument, scenic spot, winding road, waterfall, building, trekking route, forest and adventure spot sits in its actual place. "Valley" means the census bbox 85.18–85.56 E, 27.57–27.82 N. "Core" means the kathmandu_core sample bbox.

### What OSM gives us well (we can place it 1:1 directly)

* **Roads and trails: 291,000 km of `highway=*`** in Nepal; 9,044 km in the valley; 1,031 km in the core. Trails dominate: path 107,451 km, unclassified 76,329 km, track 43,953 km. Only 2.1% of unclassified ways and 1.2% of path ways have a name. There are 9,297 distinct road names in Nepal (3,253 in the valley).
* **Winding roads are real and measurable.** After Douglas-Peucker (5 m), turning per km is: trunk 152 °/km, primary 389, secondary 425, tertiary 525, unclassified 605, track 614. There are 153,000 hairpins (≥120° within 60 m) in Nepal and about 4,800 in the valley. Some of the valley ones are U-shaped streets in town grids.
  * Signature roads by hairpin count: Tribhuvan Highway / NH41 (Rajpath: 122 km, 709 °/km, 182 hairpins), Mahakali Highway NH03 (264), Mechi Rajmarg NH04 (155), Rapti Rajmarg NH55 (118), Siddhartha Highway (90), BP Highway (76), Pasang Lhamu Highway (61), the Beni–Lo Manthang jeep road (64).
  * Valley rim showpieces: Godavari–Phulchoki Marg (33 hairpins in 12.6 km), the Sundarijal/Helambu road (28), Trishuli Highway via Kakani (22), Kulekhani–Pharping road (18), the roads to Nagarkot (F28/F98, about 1,100 °/km), Kakani Park Marg and the road to Dakshinkali.
  * Hairpin hotspots (0.1° cells): NW valley rim (85.2, 27.8) has 970, Nagarkot/Sankhu (85.5, 27.8) has 919, and Ilam (88.1, 26.9) has 912.
* **Bridges: 17,679 bridge ways (815 in the valley).** 6,419 of them are foot bridges (footway/path/steps…), totalling 370 km.
  * `bridge:structure`: simple-suspension 1,285, suspension 562, beam 195, truss 27.
  * Longest: Dodhara–Chandani 1,483 m, an unnamed झोलुङ्गे पुल 881 m, Kushma–Balewa "Bungee Footbridge" 493 m, Triveni–Balmiki 383 m, Kushma–Gyadi 342 m.
  * The valley has 212 foot bridges.
* **Mountains: 3,955 peaks.** 754 are named and 3,345 have `ele`. Bands: 20 at ≥8,000 m (all named), 101 at 7,000 m+, 587 at 6,000 m+, 1,367 at 5,000 m+.
  * There are also 1,224 `mountain_pass=yes` (1,169 named), 488 saddles, 2,784 ridge ways (7,493 km) and 1,436 cliffs.
  * Glaciers: 3,781 polygons covering 4,756 km², 152 of them named.
  * Lakes: 1,545 `water=lake` (100.8 km², 198 named).
  * The valley has 11 peaks: Phulchoki 2,782 m, Shivapuri 2,732, Chandragiri 2,551, Champa Devi 2,241, Nagarkot 2,150, Jamacho 2,128, and others.
* **Forests and land cover.** Pipeline FOREST areas cover **48,853 km² in Nepal**: `natural=wood` 45,300 km² (66,481 objects) plus `landuse=forest` 4,355 km². The valley has 357 km². Farmland covers 11,383 km² and residential 1,894 km². Scrub 1,489 km², grassland 761 km², bare rock / scree 286 / 361 km², sand / shingle 652 km² (braided Terai riverbeds), riverbank water 888 km². No AreaKind is empty Nepal-wide.
* **Protected areas: 24 assembled boundary relations.** Annapurna CA 7,496 km², Shey-Phoksundo NP 3,553, Gaurishankar CA 2,204, Langtang NP 1,804, Kanchenjunga CA 1,779, Makalu-Barun NP 1,721, Manaslu CA 1,629, Chitwan NP 1,203, Sagarmatha NP 1,132, and others. Shivapuri-Nagarjun NP (103 km²) is in the valley.
* **Rivers: 26,776 km of `waterway=river` and 66,457 km of streams.**
  * Waterway relations give whole river lengths: Karnali 542 km, Ghaghara 516, Kali Gandaki 391, Trishuli system 312, Sun Kosi 280, Narayani 268, Bagmati 207, Arun 202, Marsyangdi 192, Tamor 183.
  * In the valley the Bagmati has 49 km, Bishnumati 16, Manohara 10, plus the Godawari, Kodku, Balkhu, Nakhu and others.
* **Trekking routes.** 127 `route=hiking` relations (4,016 km of unique ways) and 11 `route=foot` (258 km). Relations exist for:
  * the Great Himalaya Trail (1,404 km)
  * Annapurna Circuit (239 km), Upper Dolpo Circuit (210), Manaslu Circuit (147), Three Passes (99), Jiri–Lukla (86)
  * Kanchenjunga (Suketar–BC 100, Taplejung–BC 84, superroute 157)
  * Everest BC (54), Makalu BC (53), Helambu (43), Gosaikunda (39), Tsum (38), Langtang (31), Mardi Himal (30), Annapurna BC (24), Dhorpatan (78)
  * valley-rim hikes: Chisapani–Nagarkot 21, Sundarijal–Chisapani 9, Shivapuri 9.5
* **Religious and heritage sites.**
  * 9,770 `amenity=place_of_worship` (2,119 in the valley, 513 in the core): hindu 6,826, buddhist 1,068, christian 336, muslim 331, no religion 1,167.
  * Also 110 `man_made=stupa`, 354 `building=temple`, 72 `amenity=monastery`, 855 wayside shrines (chortens in the hills).
  * 14 `heritage=1` objects cover the UNESCO sites: the Kathmandu Valley monuments plus the Chitwan and Sagarmatha NPs.
  * The 66 hero landmarks are tracked in docs/reports/landmarks.md.
* **Places.** 28 cities, 304 towns, 5,248 villages, 14,049 hamlets, 1,172 neighbourhoods, 1,185 localities and 49 squares. Admin boundaries: 7 provinces + 77 districts (levels 4 and 6, with neighbours' units in the counts), 749 level-7 units and 6,741 wards.
* **Name coverage.** 201,175 named features in Nepal (53,726 in the valley). Only 12.6% have `name:ne` (25,290) and 0.8% have `wikidata` (1,623). Place names are much better: cities 26/28 have `name:ne`, towns 233/304.

### Surprising zeros and thin spots (decide: hand-place, curate, or generate)

* **Waterfalls.** All 335 are `waterway=waterfall` (332 nodes, 3 ways); `natural=waterfall` = 0. Only 69 are named, 3 have `height`, 2 have `wikidata`, and only 132 sit on a mapped waterway line.
  * The valley has **5** (Basmati, Fung Funge Jharana, Nagarkot Waterfall, 2 unnamed). The core has **0**.
  * Another 106 objects are *named* like waterfalls (jharana / chhahara / falls) but are not tagged as one. Example: Devi's Falls also exists as `tourism=attraction` "Patale Chhango". These are in `waterfalls.name_only_candidates…`.
* **Adventure is barely tagged.**
  * `sport=rafting|canoe|kayak|bungee|mountain_biking|horse_riding|skiing|motocross` = **0** each. `whitewater=*` is 4 nodes. `attraction=river_rafting` is 5 and `attraction=bungee_jumping` is 2 (Kushma Bungee Jump, "The Cliff").
  * Paragliding: `sport=free_flying` 12 (Sarangkot take-offs and landings) plus 1 `sport=paragliding`. `sport=climbing` 6.
  * Aerialways: `aerialway=zip_line` 10 (9 unnamed; likely river-crossing wires), gondola 6 (Chandragiri, Manakamana, Lumbini, …), cable_car 2, chair_lift **0**, goods **0**.
  * The pipeline yields only BUNGEE 5, PARAGLIDING 23, ZIPLINE 13, RAFTING 3, BOATING 4 and SAFARI 4 POIs for all of Nepal. **Adventure spots need a curated list** (rafting rivers: Trishuli, Bhote Koshi, Seti, Kali Gandaki; the Last Resort bungee; Pokhara zip-flyer; Sarangkot; Chitwan safari).
* **Heritage tag mismatches.**
  * `historic=palace` and `building=palace` are **0**. Durbars are tagged `historic=castle` (130). But in the valley, most of the 16 `historic=castle` are houses mistagged by novices ("…aunty ko ghar", "Rishen's House"), and they become PALACE POIs in the pack.
  * `man_made=mani_wall` is 0 (PoiKind MANI_WALL 0; LineKind MANI_WALL 4 ways).
  * `heritage=2/3` are 0. `historic=fort` is 9 ("gadhi"/"kot" mostly appear as names, not tags).
* **Street furniture is too sparse to place 1:1; generate it procedurally.** Nepal-wide (valley in brackets):
  * traffic_signals 60 (50), street_lamp 1,475 (327), bench 778 (113), waste_basket 212, toilets 878, `advertising=*` 0.
  * shelter 932 (220). Chautari (resting platforms) appear as named `natural=tree`: 368 objects match pati/sattal/chautara.
  * bus_stop 1,351 (379). `highway=crossing` 1,370 (616).
* **Valley mapping-campaign artefacts that bloat the pack's POI and area layers.**
  * `leisure=garden` 3,069 Nepal-wide, of which 2,946 are in the valley (only 59 named). They become most of the **3,292 PARK POIs** in the valley pack.
  * `natural=spring` 1,206 in the valley: household water sources, which become **1,932 SPRING POIs** in the pack.
  * `man_made=storage_tank` 5,979 in the valley (of 6,773) and `power=generator` 1,156 (of 1,304), probably rooftop tanks and solar panels.
  * Valley `tourism=viewpoint` (119) and `historic=castle` include businesses and homes. A quality filter (named + not inside a residential building + not shop-like) is needed before these become discoverables.
* **Others.**
  * Railway: 155 km of `railway=rail` (Janakpur–Jaynagar) and 80 km under construction. Nothing in the valley.
  * `natural=volcano`, `natural=gorge` and `waterway=riverbank` are 0 (riverbanks are `natural=water` + `water=river`).
  * `highway=trailhead`: 2. `tourism=wilderness_hut`: 17.
  * Treks with **no route relation**: Poon Hill/Ghorepani (only trail ways and the Poon Hill peak/tower), Upper Mustang (only the Beni–Lo Manthang jeep road), Nar Phu, Tamang Heritage Trail, Royal Trek, Panch Pokhari, Kalinchowk, Limi Valley, Champadevi/Chandragiri. These need hand-drawn routes along the existing path ways.

### OSM vs the built packs (did it reach the game?)

* **kathmandu_valley pack** (bbox 85.18–85.58, 27.55–27.83):
  * Decoded leaf tiles hold **43,524 unique POIs**. Per kind they match the census's own run of `tags.poi_kind` on that bbox to within the 1.5 km extract buffer: TEMPLE_HINDU 1,384 vs 1,377, VIEWPOINT 135 vs 133, WATERFALL 7 vs 6, MONUMENT 121 vs 120, STONE_TAP 126 vs 126. **Nothing is lost between OSM and the pack.** The gaps are in OSM itself.
  * Roads in tiles: 8,404 km of road classes plus 1,947 km of trails = 10,351 km (manifest road_km 10,364; OSM `highway=*` in that bbox is 9,992 km).
  * Lines: RIVER 331 km, STREAM 354 km, CABLE_CAR 2.3 km (Chandragiri), ZIP_LINE 1.7 km, CITY_WALL 0.1 km.
  * Areas: FOREST 481 km², PROTECTED 103 km², FARMLAND 99 km², RESIDENTIAL 32 km².
  * Buildings: 542,261 in tiles (OSM 533,834 in the bbox by first node). Archetypes: NEWAR 34,607, TEMPLE_PAGODA 322, STUPA 4, GOMPA 58.
* **kathmandu_core sample pack:** 15,186 unique POIs (SHOP 7,070, RESTAURANT 1,936, TEMPLE_HINDU 435, GOMPA 120, MONUMENT 46, STONE_TAP 26, VIEWPOINT 21, HERITAGE_SQUARE 5, WATERFALL 1), 1,271 km of roads plus 170 km of trails, and 167,576 buildings.

# Detailed tables (auto-generated)

Tagged objects seen: {'nodes': 231064, 'ways': 9479212, 'relations': 20417}. Named features: Nepal 201,175, valley 53,726; with name:ne 25,290 / 5,566; with wikidata 1,623 / 179.

## Places

| category | Nepal | Valley | Core | named / name:ne / wikidata (Nepal) | geometry (Nepal) | length/area | examples |
|---|---:|---:|---:|---|---|---|---|
| place=city | 28 | 4 | 1 | 28 / 26 / 24 | n28 w0 r0 |  | Kathmandu; Pokhara; Bharatpur |
| place=town | 304 | 18 | 0 | 276 / 233 / 83 | n283 w18 r3 | 34.53 km² (valley 0) | Kamalamai; Triyuga; Kirtipur |
| place=village | 5,248 | 29 | 0 | 3,443 / 749 / 155 | n3890 w1355 r3 | 26.56 km² (valley 0.39) | Barpak; Manahari; Jagatradevi |
| place=hamlet | 14,049 | 165 | 2 | 12,633 / 4,016 / 155 | n13854 w194 r1 | 7.25 km² (valley 0.05) | Darchha; Kalikakot; Gorak Shep |
| place=isolated_dwelling | 601 | 1 | 0 | 469 / 22 / 1 | n574 w27 r0 | 0.1 km² (valley 0) | Ghatiabagarh; Tora; Fangla |
| place=suburb | 463 | 95 | 8 | 450 / 407 / 118 | n462 w1 r0 | 0.03 km² (valley 0) | Wana; Langtang; Tauthali |
| place=neighbourhood | 1,172 | 307 | 76 | 1,136 / 501 / 16 | n1140 w29 r3 | 4.1 km² (valley 0.04) | Lazimpat; Maru; Sunkuda |
| place=quarter | 6 | 0 | 0 | 6 / 3 / 0 | n4 w2 r0 | 0.31 km² (valley 0) | बरुवाटार टोल; सकेला टोल; Kshetrapur |
| place=locality | 1,185 | 377 | 88 | 1,180 / 341 / 9 | n1170 w15 r0 | 1.62 km² (valley 1.01) | 希拉里台阶; Khumbu Icefall; Lumbini |
| place=square | 49 | 11 | 3 | 41 / 12 / 1 | n36 w12 r1 | 0.02 km² (valley 0.01) | Basantapur Durbar Square; Bhaktapur Durbar Square; Tashiling Tibetan Craft Market |
| place=island | 8 | 0 | 0 | 0 / 0 / 0 | n0 w8 r0 | 18.88 km² (valley 0) |  |
| place=islet | 80 | 1 | 0 | 1 / 1 / 1 | n0 w80 r0 | 14.56 km² (valley 0.0) | Taal Barahi Mandir |
| place=farm | 200 | 14 | 0 | 151 / 51 / 0 | n158 w42 r0 | 0.16 km² (valley 0.01) | Poultry Farm; mixed field; mixed farm |

## Landmarks, monuments, heritage

| category | Nepal | Valley | Core | named / name:ne / wikidata (Nepal) | geometry (Nepal) | length/area | examples |
|---|---:|---:|---:|---|---|---|---|
| tourism=attraction | 1,115 | 296 | 71 | 1,059 / 40 / 8 | n994 w118 r3 | 1.5 km² (valley 0.06) | Basantapur Durbar Square; Garden of Dreams; Changu Narayan Temple |
| historic=monument | 200 | 98 | 33 | 152 / 17 / 5 | n160 w39 r1 | 0.01 km² (valley 0.01) | Nyatapola Temple; Maitighar Mandala; Changu Narayan Temple |
| historic=memorial | 160 | 28 | 12 | 91 / 6 / 1 | n109 w51 r0 | 0.02 km² (valley 0.0) | Shahid Gate; Memorials for Alpinists & Mountaineers; Hillary Memorial Viewpoint |
| historic=ruins | 127 | 1 | 0 | 31 / 1 / 1 | n36 w91 r0 | 0.08 km² (valley 0.0) | Liglig; Ghale Dzong; Kyityang Gumba |
| historic=castle | 130 | 16 | 2 | 122 / 4 / 0 | n118 w12 r0 | 0.01 km² (valley 0) | Kalaiya Darbar; Fort; Dailekh gadi |
| historic=palace | 0 | 0 | 0 | | | |  |
| historic=fort | 9 | 0 | 0 | 6 / 0 / 1 | n3 w6 r0 | 0.02 km² (valley 0) | Tintale; Amargadhi KIlla, Amar singh Dhami; Bayalkada Fort |
| historic=city_gate | 53 | 10 | 0 | 30 / 2 / 0 | n44 w9 r0 |  | Sahid Dashrath Chanda Gate; Biratnagar Gate; Bandipur Gate |
| historic=archaeological_site | 40 | 11 | 1 | 33 / 1 / 2 | n21 w18 r1 | 0.42 km² (valley 0.02) | Kapilavastu (I) Tilaurakot; Nigali Sagar; Kapilavastu (II) Piprahava |
| historic=temple | 4 | 1 | 0 | 3 / 0 / 1 | n3 w1 r0 |  | Kalinchok Bhagwati Temple; Panchase Temple; Laxmi Narsing Mandir |
| historic=building | 13 | 10 | 0 | 9 / 0 / 0 | n7 w6 r0 | 0.01 km² (valley 0.01) | Lamjung Darbar; Basanti Bagh Durbar; Kulpuja Pati |
| historic=wayside_shrine | 855 | 25 | 18 | 52 / 4 / 0 | n561 w294 r0 | 0.01 km² (valley 0.0) | Keke La; Ghungru La; Chorten |
| historic=yes | 31 | 20 | 11 | 16 / 4 / 1 | n6 w18 r7 | 0.33 km² (valley 0.33); 25.2 km (valley 0) | Amar Narayan Temple; Swayambhunath; Swayambhunath |
| historic=heritage | 2 | 1 | 1 | 2 / 0 / 0 | n1 w0 r1 |  | Unikot Durbar; Basantapur Dabali |
| heritage=1 | 14 | 9 | 7 | 14 / 8 / 6 | n0 w7 r7 | 2,343.23 km² (valley 0.36) | Chitwan National Park; Sagarmatha National Park; Basantapur Durbar Square |
| heritage=2 | 0 | 0 | 0 | | | |  |
| heritage=3 | 0 | 0 | 0 | | | |  |
| man_made=stupa | 110 | 10 | 7 | 4 / 1 / 0 | n44 w66 r0 |  | Shechen Stupa For Universal Peace; Chorten de Braga; Kani Gate |
| building=stupa | 8 | 4 | 1 | 1 / 0 / 0 | n1 w7 r0 |  | Nama Buddha |
| building=temple | 354 | 103 | 39 | 133 / 14 / 9 | n34 w316 r4 | 0.04 km² (valley 0.02) | Royal Thai Monastery; Nyatapola Temple; Hiranya Varna Mahavihar |
| building=pagoda | 1 | 0 | 0 | 1 / 1 / 0 | n0 w1 r0 |  | World Peace Pagoda |
| building=monastery | 15 | 1 | 1 | 6 / 0 / 0 | n1 w14 r0 |  | Lukla Gompa; Kesh chandra parabrat mahabihar; Pal Saghon Dhungkar Choezom Monastrery |
| building=gompa | 4 | 0 | 0 | 0 / 0 / 0 | n0 w4 r0 |  |  |
| building=palace | 0 | 0 | 0 | | | |  |
| building=church | 43 | 16 | 3 | 24 / 0 / 0 | n4 w39 r0 | 0.01 km² (valley 0.0) | Assumption Church; New Testament Church Nepal; Immanuel Bible Missionary Church |
| building=mosque | 21 | 0 | 0 | 15 / 0 / 0 | n3 w17 r1 | 0.01 km² (valley 0) | Madariya Pahad Nepal; ईदगाह; Idgaha Karbala |
| building=shrine | 3 | 3 | 2 | 0 / 0 / 0 | n0 w3 r0 |  |  |
| amenity=place_of_worship | 9,770 | 2,119 | 513 | 8,271 / 2,317 / 30 | n7681 w2075 r14 | 1.19 km² (valley 0.36) | Gadhimai Temple; Bajrayogini temple; chandeshwori temple |
| amenity=monastery | 72 | 24 | 19 | 41 / 0 / 3 | n22 w48 r2 | 0.04 km² (valley 0.0) | Royal Thai Monastery; Tengboche Monastery; Thrangu Tashi Yangtse Monastery |
| tourism=museum | 83 | 31 | 15 | 76 / 10 / 4 | n52 w30 r1 | 0.5 km² (valley 0.42) | International Mountain Museum; Chhauni Museum; Annapurna Butterfly Museum |
| tourism=artwork | 312 | 149 | 51 | 240 / 3 / 0 | n295 w17 r0 |  | Lord Buddha; Buddha; buddha statue |
| man_made=tower | 413 | 40 | 13 | 122 / 5 / 0 | n319 w94 r0 | 0.02 km² (valley 0.0) | Poonhill tower; Tikapur View Tower; Kohalpur View Tower |
| place=square | 49 | 11 | 3 | 41 / 12 / 1 | n36 w12 r1 | 0.02 km² (valley 0.01) | Basantapur Durbar Square; Bhaktapur Durbar Square; Tashiling Tibetan Craft Market |

## Tourism & lodging

| category | Nepal | Valley | Core | named / name:ne / wikidata (Nepal) | geometry (Nepal) | length/area | examples |
|---|---:|---:|---:|---|---|---|---|
| tourism=viewpoint | 1,397 | 119 | 16 | 526 / 21 / 6 | n1384 w13 r0 | 0.01 km² (valley 0.0) | Kangchenjunga; Mount Makalu; Dhaulagiri |
| tourism=hotel | 4,222 | 949 | 569 | 3,994 / 273 / 4 | n3644 w574 r4 | 1.22 km² (valley 0.48) | Dwarika's Hotel; Grand Hotel; Hyatt Regency Kathmandu |
| tourism=guest_house | 2,996 | 386 | 155 | 2,715 / 92 / 3 | n2280 w687 r29 | 0.11 km² (valley 0.0) | Machermo Lodge & Bakery; Hotel Everest View; Panorama Lodge |
| tourism=hostel | 652 | 354 | 159 | 572 / 20 / 0 | n508 w142 r2 | 0.13 km² (valley 0.03) | Dolma Guesthouse; Jiri Hospital; Hotel Sneha |
| tourism=camp_site | 583 | 37 | 6 | 300 / 2 / 2 | n527 w56 r0 | 0.16 km² (valley 0.0) | Everest Base Camp; Thorong-ra High Camp; Camp 3N North ridge |
| tourism=alpine_hut | 163 | 2 | 0 | 141 / 0 / 0 | n122 w40 r1 | 0.12 km² (valley 0) | Thorong High Camp Hotel; Hotel Tilicho Peak and Restaurant; Tonglu |
| tourism=wilderness_hut | 17 | 2 | 0 | 13 / 0 / 0 | n14 w3 r0 |  | Pace Hotel; Dhule Base Camp; TRUSS |
| tourism=picnic_site | 96 | 24 | 1 | 44 / 5 / 0 | n75 w21 r0 | 0.17 km² (valley 0.01) | kalabang ghaderi picnic spot; Rapti Peace Park; picnic spot |
| tourism=theme_park | 22 | 7 | 1 | 20 / 1 / 0 | n10 w12 r0 | 0.09 km² (valley 0.05) | Ganatantra Stambha; Kathmandu Fun Park; Chacha Hue Fun Park |
| tourism=zoo | 4 | 1 | 0 | 4 / 0 / 1 | n1 w3 r0 | 0.07 km² (valley 0.06) | Central Zoo; cHIDIYA GHAR FOR ARYAN; Pachagaiya Zoo |
| tourism=information | 582 | 72 | 34 | 352 / 6 / 0 | n563 w19 r0 | 0.01 km² (valley 0.0) | Syamjo: Bluesheep Habitat Area; Sypchen; Thango River |

## Nature & scenery

| category | Nepal | Valley | Core | named / name:ne / wikidata (Nepal) | geometry (Nepal) | length/area | examples |
|---|---:|---:|---:|---|---|---|---|
| natural=peak | 3,955 | 11 | 0 | 754 / 80 / 176 | n3955 w0 r0 |  | Mount Everest; Everest South Peak; Kangchenjunga |
| natural=volcano | 0 | 0 | 0 | | | |  |
| natural=saddle | 488 | 3 | 0 | 238 / 39 / 11 | n488 w0 r0 |  | South Col; North Col; Lhakpa La |
| mountain_pass=yes | 1,224 | 3 | 0 | 1,169 / 34 / 10 | n1223 w1 r0 |  | South Col; North Col; Lhakpa La |
| natural=ridge | 2,784 | 0 | 0 | 41 / 0 / 3 | n0 w2784 r0 | 7,492.6 km (valley 0) | Sisne Himal; Churen Himal; Kalinchok Bhagawatī Ḍā̃ḍā |
| natural=cliff | 1,436 | 7 | 0 | 18 / 2 / 0 | n695 w741 r0 | 3.15 km² (valley 0); 424.0 km (valley 1.3) | Simpani Dadaa; maybe farmland; Landslide Risk Zone |
| natural=cave_entrance | 86 | 3 | 0 | 41 / 4 / 2 | n77 w9 r0 | 0.03 km² (valley 0.0) | Siddha Cave; Bat Cave; Ritiling |
| natural=cave | 4 | 0 | 0 | 4 / 1 / 0 | n4 w0 r0 |  | Shree Jalapa Secondary School, Diyale; Runchung Cave; Gupteshwar Mahadev |
| natural=spring | 5,429 | 1,206 | 2 | 5,214 / 13 / 0 | n5425 w4 r0 |  | Jhyaripandhera well; Nasa Marti नासा मर्ति; water tap |
| natural=hot_spring | 17 | 0 | 0 | 11 / 0 / 0 | n16 w1 r0 |  | Tatopani Hot Springs II; Jagat hot spring; Jhinu Hot Springs |
| natural=glacier | 3,781 | 0 | 0 | 152 / 19 / 32 | n0 w3623 r158 | 4,755.61 km² (valley 0) | Ngozumpa Glacier; Langtang Glacier; Kangshung Glacier |
| natural=tree | 12,758 | 493 | 156 | 664 / 17 / 0 | n12758 w0 r0 |  | Manakamana Marga; चाैतारी; Pipaltree |
| natural=tree_row | 2,093 | 17 | 3 | 5 / 0 / 0 | n0 w2093 r0 | 0.03 km² (valley 0); 510.9 km (valley 8.1) | Managed Forest; Trees; pashupati kiwi farm |
| natural=wood | 66,481 | 938 | 7 | 232 / 43 / 1 | n292 w63573 r2616 | 45,300.34 km² (valley 308.5); 89.3 km (valley 0) | Kapilavastu (I) Tilaurakot; Keda Ban Samudaya; Kots forest |
| natural=scrub | 22,224 | 328 | 14 | 10 / 0 / 0 | n0 w21871 r353 | 1,419.27 km² (valley 10.65); 6.6 km (valley 0) | Koshi Tappu Wildlife Reserve; Katarniaghat WLS; + |
| natural=grassland | 14,178 | 231 | 73 | 98 / 3 / 0 | n4 w14082 r92 | 375.17 km² (valley 0.68) | Chhayang छयङ; Devta Sain देवता सैन; Donggang Foo डोङ्गङ फू |
| natural=heath | 910 | 0 | 0 | 4 / 0 / 0 | n0 w906 r4 | 85.17 km² (valley 0) | नदी उकास; Dharmshala Ground; Health Post |
| natural=water | 42,655 | 454 | 58 | 878 / 94 / 26 | n28 w42156 r471 | 1,198.37 km² (valley 2.55); 85.9 km (valley 0.6) | Mahakali River; Mahakali River; Imja Tsho |
| natural=water|water=lake | 1,545 | 3 | 0 | 198 / 35 / 15 | n0 w1530 r15 | 100.83 km² (valley 0.0) | Imja Tsho; Chola Tsho; Phewa Lake |
| natural=water|water=river | 2,562 | 41 | 8 | 52 / 12 / 4 | n0 w2183 r379 | 872.28 km² (valley 2.01) | Mahakali River; Mahakali River; Trisuli |
| natural=water|water=pond | 14,765 | 201 | 21 | 336 / 28 / 5 | n3 w14737 r25 | 37.78 km² (valley 0.22) | Taudaha Lake; Rani Pokhari; Siddhapokhari |
| natural=wetland | 1,305 | 26 | 5 | 12 / 0 / 0 | n21 w1260 r24 | 76.48 km² (valley 0.12) | Majhauli Taal; Bandha Tal; Chandramukhi Taal |
| natural=bare_rock | 1,013 | 4 | 0 | 14 / 0 / 0 | n0 w1003 r10 | 265.04 km² (valley 0.03) | Bare rock covered with snow; Hard bare rocks with seasoanl snow cover; Landslide Area |
| natural=scree | 1,333 | 0 | 0 | 2 / 0 / 1 | n0 w1249 r84 | 361.57 km² (valley 0) | Landslide Area; Gumba 1 |
| natural=sand | 5,322 | 68 | 16 | 16 / 0 / 0 | n0 w5239 r83 | 450.15 km² (valley 0.37) | Sand Mining Area; Sandbar; Tundikhel |
| natural=shingle | 2,970 | 6 | 0 | 3 / 1 / 0 | n0 w2907 r63 | 193.92 km² (valley 0.02) | falakhu river; Rishi Odar Area; Seti Ghat |
| natural=beach | 258 | 2 | 1 | 11 / 0 / 0 | n3 w253 r2 | 8.89 km² (valley 0) | Bhalayetar Ghat; Bhantar Ghat; Ramghat |
| natural=rock | 274 | 1 | 0 | 15 / 0 / 0 | n57 w216 r1 | 21.78 km² (valley 0.0) | Snow filled rock structure; Landslide Area; Landslide Zone |
| natural=stone | 53 | 2 | 0 | 7 / 0 / 0 | n35 w18 r0 | 0.38 km² (valley 0) | Machhapuchchre Stone; Baag dhunga; large stone |
| natural=waterfall | 0 | 0 | 0 | | | |  |
| natural=valley | 16 | 3 | 0 | 13 / 2 / 2 | n8 w7 r1 | 493.72 km² (valley 493.72); 96.2 km (valley 52.2) | Kathmandu Valley; Western Cwm; Kathmandu Valley |
| natural=gorge | 0 | 0 | 0 | | | |  |

## Waterways

| category | Nepal | Valley | Core | named / name:ne / wikidata (Nepal) | geometry (Nepal) | length/area | examples |
|---|---:|---:|---:|---|---|---|---|
| waterway=river | 5,489 | 128 | 26 | 1,766 / 237 / 48 | n1 w5451 r37 | 26,776.1 km (valley 288.9) | अरुण नदी; Bagmati River; Arun River |
| waterway=stream | 49,966 | 318 | 5 | 2,180 / 85 / 23 | n0 w49941 r25 | 0.33 km² (valley 0); 66,457.4 km (valley 250.2) | Kyumrung Khola; Jum Khola; Phusre Khola |
| waterway=canal | 5,762 | 18 | 1 | 519 / 24 / 0 | n0 w5760 r2 | 3,842.9 km (valley 7.9) | Western Gandak main canal; Koshi Pump Nahar(Canal); Maila khola |
| waterway=ditch | 19,211 | 37 | 0 | 128 / 0 / 0 | n0 w19211 r0 | 5,901.9 km (valley 11.0) | Rampur Canal; Phun Khola; Shankarpur Branch Canal |
| waterway=drain | 4,415 | 49 | 3 | 11 / 0 / 0 | n0 w4415 r0 | 1,600.3 km (valley 8.5) | Latkanya khola; thokarawala kulo to chikani pond; culvurt |
| waterway=waterfall | 335 | 5 | 0 | 69 / 8 / 2 | n332 w3 r0 | 3.5 km (valley 0) | Narchyang waterfall; Ganga Jamuna; Him Pipe |
| waterway=rapids | 2 | 0 | 0 | 0 / 0 / 0 | n2 w0 r0 |  |  |
| waterway=dam | 217 | 4 | 0 | 36 / 2 / 0 | n46 w171 r0 | 0.13 km² (valley 0.0); 27.9 km (valley 0.0) | Singati Hydropower Water Reservoir; Trishuli Hydro Power Dam; Khopasi HydroPower Dam |
| waterway=weir | 261 | 3 | 1 | 2 / 1 / 0 | n81 w180 r0 | 0.04 km² (valley 0.02); 13.3 km (valley 3.8) | Manohara; Seti River Weir |
| waterway=riverbank | 0 | 0 | 0 | | | |  |
| whitewater=* | 0 | 0 | 0 | | | |  |

## Land use & green

| category | Nepal | Valley | Core | named / name:ne / wikidata (Nepal) | geometry (Nepal) | length/area | examples |
|---|---:|---:|---:|---|---|---|---|
| landuse=forest | 11,744 | 482 | 11 | 187 / 22 / 1 | n12 w11240 r492 | 4,354.75 km² (valley 95.86) | Katarniaghat WLS; Godawari forest; Hariyali Community Forest |
| landuse=farmland | 121,146 | 1,961 | 35 | 730 / 13 / 1 | n14 w118619 r2513 | 11,384.49 km² (valley 78.68) |  |
| landuse=orchard | 1,522 | 19 | 0 | 61 / 1 / 0 | n0 w1520 r2 | 38.17 km² (valley 0.12) | Himalayan Goodrick Tea Estate; Tokla Tea Estate(Nepal Tea Development Coorporation); banana kheti |
| landuse=meadow | 4,684 | 82 | 24 | 21 / 1 / 0 | n0 w4607 r77 | 294.38 km² (valley 0.78) | Syang Bhayer श्यङ भायर; Mili Chaur; Lung-gum लङ्गम |
| landuse=grass | 1,831 | 415 | 224 | 33 / 5 / 0 | n0 w1814 r17 | 54.29 km² (valley 1.57) |  |
| landuse=residential | 86,373 | 1,356 | 61 | 1,114 / 36 / 1 | n18 w85938 r417 | 1,896.55 km² (valley 26.6) |  |
| landuse=commercial | 421 | 87 | 40 | 144 / 19 / 0 | n1 w417 r3 | 9.34 km² (valley 0.32) |  |
| landuse=industrial | 2,001 | 202 | 13 | 441 / 77 / 0 | n9 w1983 r9 | 47.67 km² (valley 1.37) |  |
| landuse=religious | 164 | 22 | 10 | 97 / 24 / 2 | n0 w161 r3 | 1.94 km² (valley 0.09) | Muktinath; Budhanilkantha; Swayambhunath |
| landuse=cemetery | 61 | 18 | 2 | 40 / 9 / 0 | n26 w33 r2 | 0.12 km² (valley 0.01) |  |
| landuse=military | 152 | 27 | 11 | 93 / 19 / 0 | n14 w136 r2 | 25.19 km² (valley 1.51) |  |
| landuse=reservoir | 3,454 | 13 | 1 | 15 / 1 / 1 | n2 w3450 r2 | 59.25 km² (valley 0.01) | Kamal Pokhari; glacier; Irrigation Intake |
| landuse=quarry | 1,027 | 20 | 0 | 24 / 0 / 0 | n7 w994 r26 | 28.77 km² (valley 0.8) |  |
| landuse=construction | 87 | 12 | 5 | 12 / 0 / 0 | n0 w86 r1 | 1.49 km² (valley 0.09) |  |
| landuse=farmyard | 4,956 | 157 | 5 | 228 / 20 / 0 | n9 w4930 r17 | 18.64 km² (valley 0.64) |  |
| landuse=plant_nursery | 61 | 22 | 8 | 34 / 4 / 0 | n1 w60 r0 | 2.0 km² (valley 0.02) |  |
| leisure=park | 1,007 | 343 | 115 | 514 / 40 / 3 | n238 w756 r13 | 17.57 km² (valley 1.4) | Ratna Park; Garden of Dreams; Shahid Gate |
| leisure=garden | 3,069 | 2,946 | 25 | 59 / 4 / 0 | n40 w3027 r2 | 0.69 km² (valley 0.07) | Eden garden; Lumbini World Heritage Site; Community Garden |
| leisure=nature_reserve | 35 | 1 | 0 | 32 / 12 / 12 | n3 w19 r13 | 19,751.34 km² (valley 0) | Annapurna Conservation Area; Kanchanjunga Conservation Area; Manaslu Conservation Area |
| leisure=pitch | 691 | 266 | 87 | 248 / 35 / 1 | n68 w620 r3 | 2.07 km² (valley 0.45) |  |
| leisure=stadium | 49 | 10 | 3 | 32 / 4 / 4 | n2 w47 r0 | 0.84 km² (valley 0.18) | Pokhara Rangasala Stadium; Dashrath Rangasala Stadium; Narayani Stadium |
| leisure=playground | 1,032 | 184 | 49 | 343 / 35 / 0 | n267 w761 r4 | 4.13 km² (valley 0.47) |  |
| leisure=sports_centre | 137 | 94 | 45 | 122 / 11 / 0 | n66 w71 r0 | 0.3 km² (valley 0.12) |  |
| leisure=swimming_pool | 210 | 91 | 36 | 39 / 3 / 0 | n14 w196 r0 | 0.05 km² (valley 0.02) |  |
| leisure=golf_course | 4 | 1 | 1 | 2 / 1 / 0 | n0 w4 r0 | 0.56 km² (valley 0.11) | Himalayan Golf Course; Surya Golf Course |
| leisure=water_park | 21 | 6 | 1 | 13 / 1 / 0 | n8 w13 r0 | 0.1 km² (valley 0.03) | Whoopee Land Amusement and Water Park; Wonderland; James World Fun Park |
| boundary=protected_area | 31 | 5 | 2 | 25 / 13 / 9 | n1 w18 r12 | 19,316.91 km² (valley 18.4) | Annapurna Conservation Area; Kanchanjunga Conservation Area; Manaslu Conservation Area |
| boundary=national_park | 19 | 1 | 0 | 16 / 13 / 11 | n0 w7 r12 | 13,202.89 km² (valley 103.04) | Shey Phoksundo National Park; Langtang National Park; Chitwan National Park |

## Transport infrastructure

| category | Nepal | Valley | Core | named / name:ne / wikidata (Nepal) | geometry (Nepal) | length/area | examples |
|---|---:|---:|---:|---|---|---|---|
| aerialway=cable_car | 2 | 0 | 0 | 2 / 0 / 0 | n0 w2 r0 | 2.3 km (valley 0) | Maula Kalika Cable Car; Maulakali Cable Car |
| aerialway=gondola | 6 | 1 | 0 | 4 / 1 / 0 | n0 w6 r0 | 11.7 km (valley 2.3) | Manakamana cable car; Lumbini Cable Car; Chandragiri Cable Car |
| aerialway=chair_lift | 0 | 0 | 0 | | | |  |
| aerialway=zip_line | 10 | 1 | 0 | 1 / 0 / 0 | n0 w10 r0 | 4.7 km (valley 1.7) | flood prone area kusumghat |
| aerialway=goods | 0 | 0 | 0 | | | |  |
| aerialway=station | 12 | 2 | 0 | 9 / 1 / 0 | n12 w0 r0 |  | Lumbini Cable Car Top Station; Manakamana cable car hill station; Chandragiri Cable Car |
| railway=rail | 311 | 0 | 0 | 217 / 215 / 2 | n0 w311 r0 | 155.4 km (valley 0) | Nepal Railways; Nepal Railways; Nepal Railways |
| railway=narrow_gauge | 6 | 0 | 0 | 0 / 0 / 0 | n0 w6 r0 | 30.8 km (valley 0) |  |
| railway=abandoned | 39 | 0 | 0 | 2 / 2 / 0 | n0 w39 r0 | 48.1 km (valley 0) | Nepal Railways; Nepal Railways |
| railway=construction | 116 | 0 | 0 | 102 / 4 / 0 | n0 w116 r0 | 80.2 km (valley 0) |  |
| railway=station | 21 | 0 | 0 | 21 / 4 / 11 | n21 w0 r0 |  | Nepalganj Road; Bhikhna Thori; Kakraha Rest House |
| railway=halt | 3 | 0 | 0 | 3 / 1 / 0 | n3 w0 r0 |  | Perbaha; Mahinathpur; Kangali Halt |
| aeroway=aerodrome | 58 | 1 | 1 | 55 / 22 / 41 | n10 w48 r0 | 15.05 km² (valley 2.65); 16.9 km (valley 0) | Syangboche Airfield; Manang Airport; Simikot Airport |
| aeroway=runway | 78 | 3 | 2 | 6 / 0 / 0 | n0 w78 r0 | 0.49 km² (valley 0); 47.7 km (valley 3.3) | Dhangadhi Airport; 10/28; 10/28 |
| aeroway=helipad | 364 | 26 | 5 | 52 / 0 / 0 | n126 w238 r0 | 0.17 km² (valley 0.01) | Kyāṅjiṅ; Rimche; Kath koirala |
| aeroway=heliport | 0 | 0 | 0 | | | |  |
| aeroway=taxiway | 251 | 53 | 51 | 0 / 0 / 0 | n0 w251 r0 | 0.01 km² (valley 0); 29.2 km (valley 9.0) |  |
| aeroway=apron | 35 | 6 | 6 | 4 / 0 / 0 | n0 w35 r0 | 0.59 km² (valley 0.28) |  |
| aeroway=terminal | 28 | 3 | 3 | 15 / 2 / 0 | n1 w27 r0 | 0.08 km² (valley 0.02) |  |
| highway=bus_stop | 1,351 | 379 | 89 | 1,214 / 98 / 1 | n1350 w1 r0 |  | Chandra Surya Chowk; chormara bus stand; Pharping |
| amenity=bus_station | 656 | 178 | 41 | 566 / 41 / 0 | n564 w92 r0 | 0.58 km² (valley 0.13) | Naya Bus Park, Gongabu; Paras Bus Park; Kohalpur new Bus Park |
| amenity=fuel | 631 | 158 | 43 | 483 / 71 / 1 | n542 w89 r0 | 0.15 km² (valley 0.1) |  |
| amenity=parking | 457 | 291 | 164 | 81 / 4 / 0 | n122 w334 r1 | 0.48 km² (valley 0.27) |  |
| amenity=taxi | 64 | 25 | 13 | 25 / 5 / 0 | n59 w5 r0 |  |  |
| highway=traffic_signals | 60 | 50 | 21 | 4 / 2 / 0 | n60 w0 r0 |  |  |
| highway=crossing | 1,370 | 616 | 239 | 19 / 1 / 0 | n1370 w0 r0 |  |  |
| highway=mini_roundabout | 35 | 8 | 1 | 8 / 1 / 0 | n35 w0 r0 |  |  |
| highway=turning_circle | 204 | 20 | 1 | 7 / 0 / 0 | n204 w0 r0 |  |  |
| highway=motorway_junction | 40 | 4 | 1 | 22 / 0 / 0 | n40 w0 r0 |  |  |
| highway=trailhead | 2 | 0 | 0 | 0 / 0 / 0 | n2 w0 r0 |  |  |
| ford=yes | 18,955 | 137 | 4 | 96 / 5 / 0 | n15691 w3264 r0 |  |  |

## Power & man-made

| category | Nepal | Valley | Core | named / name:ne / wikidata (Nepal) | geometry (Nepal) | length/area | examples |
|---|---:|---:|---:|---|---|---|---|
| power=line | 525 | 88 | 14 | 42 / 0 / 0 | n0 w525 r0 | 4,802.7 km (valley 188.4) |  |
| power=minor_line | 101 | 81 | 22 | 0 / 0 / 0 | n0 w101 r0 | 26.6 km (valley 2.8) |  |
| power=tower | 13,784 | 583 | 33 | 0 / 0 / 0 | n13784 w0 r0 |  |  |
| power=pole | 2,231 | 261 | 53 | 22 / 0 / 0 | n2230 w1 r0 |  |  |
| power=substation | 231 | 18 | 4 | 115 / 4 / 0 | n16 w215 r0 | 1.71 km² (valley 0.09); 9.6 km (valley 1.0) |  |
| power=plant | 104 | 0 | 0 | 74 / 2 / 4 | n25 w76 r3 | 1.47 km² (valley 0) |  |
| power=generator | 1,304 | 1,156 | 5 | 69 / 6 / 0 | n116 w1188 r0 | 0.13 km² (valley 0.01) |  |
| man_made=water_tower | 238 | 29 | 6 | 53 / 4 / 0 | n81 w157 r0 | 0.04 km² (valley 0.01) | Panityanki; Melamchi Water Storage; Shanischare Khanepani |
| man_made=mast | 62 | 5 | 2 | 9 / 1 / 1 | n61 w1 r0 |  | Dharan Clock Tower; Namaste Tower; TelephoneTower |
| man_made=tower|tower:type=communication | 173 | 11 | 5 | 35 / 1 / 0 | n156 w17 r0 |  | Radio Namaste; Ncell Tower; NTC Tower |
| man_made=tower|tower:type=observation | 42 | 9 | 2 | 18 / 1 / 0 | n22 w20 r0 | 0.01 km² (valley 0.0) | Poonhill tower; Tikapur View Tower; Kohalpur View Tower |
| man_made=flagpole | 26 | 10 | 1 | 6 / 0 / 0 | n25 w1 r0 |  | Mani Wall; nepal redcross society sitapur branch; रेडक्रस संयोजक |
| man_made=chimney | 887 | 135 | 3 | 13 / 0 / 0 | n856 w31 r0 | 0.02 km² (valley 0) | Gautam Fishery; Green Nepal Citywaste Management; HM Brick Factory |
| man_made=bridge | 180 | 8 | 1 | 22 / 2 / 0 | n0 w180 r0 | 0.12 km² (valley 0.02) | Gandak Barrage; Balkumari Bridge; Kamala river Bridge (under construction) |
| man_made=embankment | 186 | 2 | 0 | 1 / 0 / 0 | n0 w186 r0 | 127.7 km (valley 0.3) | basin |
| man_made=dam | 0 | 0 | 0 | | | |  |
| man_made=water_tap | 871 | 104 | 7 | 175 / 11 / 0 | n853 w18 r0 |  | Sanayashi Dhara; drinking water tap; Mathilo Tole Dhara |
| man_made=water_well | 452 | 124 | 16 | 145 / 16 / 0 | n363 w89 r0 | 0.03 km² (valley 0.0) | Gurkhali Drinking Water; Dhaulagiri Drinking Water Supply; Tanani well |
| man_made=mani_wall | 0 | 0 | 0 | | | |  |
| man_made=works | 401 | 83 | 9 | 256 / 25 / 0 | n186 w215 r0 | 3.3 km² (valley 0.03) |  |
| man_made=storage_tank | 6,773 | 5,979 | 12 | 144 / 9 / 0 | n496 w6277 r0 | 0.1 km² (valley 0.02) |  |
| barrier=wall | 4,454 | 342 | 118 | 140 / 54 / 5 | n0 w4452 r2 | 3.14 km² (valley 1.4); 446.3 km (valley 41.7) | Budhanilakantha School; International Mountain Museum; Muktinath |
| barrier=city_wall | 19 | 1 | 0 | 0 / 0 / 0 | n0 w19 r0 | 2.0 km (valley 0.1) |  |
| barrier=fence | 933 | 206 | 21 | 27 / 8 / 3 | n0 w932 r1 | 0.59 km² (valley 0.0); 179.2 km (valley 16.5) |  |
| barrier=gate | 747 | 252 | 128 | 70 / 4 / 0 | n654 w93 r0 | 1.1 km (valley 0.6) | Sahid Dashrath Chanda Gate; Thimi Nagarpalika Gate; दक्षिण प्रवेश द्वार |
| barrier=retaining_wall | 241 | 12 | 4 | 0 / 0 / 0 | n0 w241 r0 | 40.6 km (valley 0.6) |  |
| barrier=hedge | 260 | 35 | 20 | 1 / 0 / 0 | n0 w259 r1 | 0.01 km² (valley 0.0); 25.7 km (valley 1.1) |  |

## Street furniture

| category | Nepal | Valley | Core | named / name:ne / wikidata (Nepal) | geometry (Nepal) | length/area | examples |
|---|---:|---:|---:|---|---|---|---|
| highway=street_lamp | 1,475 | 327 | 166 | 3 / 0 / 0 | n1475 w0 r0 |  | streetlight; Street Lamp; Street Lamp |
| amenity=bench | 778 | 113 | 35 | 29 / 5 / 0 | n776 w2 r0 |  |  |
| amenity=shelter | 932 | 220 | 28 | 209 / 5 / 0 | n621 w309 r2 | 0.13 km² (valley 0.0) | SOS Children Village Gandaki; Shepherds Pen; Shelters |
| amenity=shelter|shelter_type=public_transport | 43 | 1 | 0 | 17 / 1 / 0 | n15 w28 r0 |  | Laam Pokhara - Transit Shelter; Indra Chowk Paritchyalaya; Chautaro |
| amenity=shelter|shelter_type=(none) | 816 | 195 | 27 | 170 / 4 / 0 | n559 w255 r2 | 0.13 km² (valley 0.0) | SOS Children Village Gandaki; Shepherds Pen; Shelters |
| highway=bus_stop | 1,351 | 379 | 89 | 1,214 / 98 / 1 | n1350 w1 r0 |  | Chandra Surya Chowk; chormara bus stand; Pharping |
| amenity=drinking_water | 1,947 | 383 | 32 | 574 / 34 / 1 | n1838 w109 r0 | 0.02 km² (valley 0.01) | Tusha Hiti; Behada baba drinking water; Doshro Sana Sahari Khanepani |
| man_made=water_tap | 871 | 104 | 7 | 175 / 11 / 0 | n853 w18 r0 |  | Sanayashi Dhara; drinking water tap; Mathilo Tole Dhara |
| amenity=fountain | 88 | 32 | 12 | 26 / 1 / 0 | n52 w36 r0 | 0.01 km² (valley 0.0) | Ku fountain; Fountain; Water Fountain |
| amenity=waste_basket | 212 | 14 | 5 | 21 / 0 / 0 | n212 w0 r0 |  |  |
| amenity=toilets | 878 | 250 | 121 | 100 / 17 / 0 | n577 w301 r0 | 0.01 km² (valley 0.0) |  |
| amenity=post_box | 28 | 11 | 7 | 11 / 1 / 0 | n28 w0 r0 |  |  |
| amenity=telephone | 46 | 15 | 8 | 11 / 2 / 0 | n44 w2 r0 |  |  |
| amenity=water_point | 624 | 61 | 22 | 139 / 8 / 0 | n611 w13 r0 |  |  |
| amenity=atm | 1,124 | 574 | 304 | 401 / 168 / 0 | n1124 w0 r0 |  |  |
| natural=tree | 12,758 | 493 | 156 | 664 / 17 / 0 | n12758 w0 r0 |  | Manakamana Marga; चाैतारी; Pipaltree |
| tourism=artwork | 312 | 149 | 51 | 240 / 3 / 0 | n295 w17 r0 |  | Lord Buddha; Buddha; buddha statue |
| amenity=clock | 0 | 0 | 0 | | | |  |
| amenity=vending_machine | 33 | 2 | 0 | 20 / 6 / 0 | n33 w0 r0 |  |  |
| advertising=billboard | 0 | 0 | 0 | | | |  |

## Adventure & sport

| category | Nepal | Valley | Core | named / name:ne / wikidata (Nepal) | geometry (Nepal) | length/area | examples |
|---|---:|---:|---:|---|---|---|---|
| sport=climbing | 6 | 3 | 3 | 5 / 1 / 0 | n5 w1 r0 |  | Maurice Herzog Climbing Wall; Kathmandu Sport Climbing Centre; Chhetri Sisters Cliff |
| sport=paragliding | 1 | 0 | 0 | 1 / 0 / 0 | n1 w0 r0 |  | Sky Rider Paragliding |
| sport=free_flying | 12 | 0 | 0 | 9 / 6 / 0 | n6 w6 r0 | 0.03 km² (valley 0) | Paragliding Takeoff @ Mandredhunga; Old Paragliding Landing; Old Paragliding Takeoff @ Sarangkot |
| sport=bungee | 0 | 0 | 0 | | | |  |
| sport=canoe | 0 | 0 | 0 | | | |  |
| sport=rafting | 0 | 0 | 0 | | | |  |
| sport=kayak | 0 | 0 | 0 | | | |  |
| sport=mountain_biking | 0 | 0 | 0 | | | |  |
| sport=motocross | 0 | 0 | 0 | | | |  |
| sport=golf | 1 | 1 | 1 | 1 / 0 / 0 | n1 w0 r0 |  | Nepal Golf Zone |
| sport=horse_riding | 0 | 0 | 0 | | | |  |
| sport=skiing | 0 | 0 | 0 | | | |  |
| sport=soccer | 220 | 84 | 21 | 112 / 18 / 1 | n11 w207 r2 | 1.32 km² (valley 0.46) | ANFA; Pulchok Football Ground; Dharan Stadium |
| sport=cricket | 43 | 14 | 4 | 26 / 1 / 0 | n5 w38 r0 | 0.54 km² (valley 0.11) | Gautam Budda Internationl Cricket Ground; 5 NO Field; Mulpani Cricket Stadium |
| sport=swimming | 44 | 29 | 10 | 24 / 0 / 0 | n7 w37 r0 | 0.02 km² (valley 0.01) | BIG SPLASH Waterpark; Balaju Swimming Pool; Sirens Club |
| sport=multi | 23 | 3 | 3 | 10 / 2 / 1 | n4 w19 r0 | 0.12 km² (valley 0.0) | Narayani Stadium; APF Ground; Illam TudiKhel |
| attraction=amusement_ride | 1 | 0 | 0 | 0 / 0 / 0 | n0 w1 r0 |  |  |
| attraction=animal | 71 | 61 | 0 | 65 / 0 / 0 | n5 w66 r0 | 0.01 km² (valley 0.01) | Royal Bengal Tiger; One Horned Rhinoceros; Wild Water Buffalo |
| attraction=water_slide | 8 | 0 | 0 | 5 / 0 / 0 | n3 w5 r0 | 0.02 km² (valley 0) | Temple; Chandandevi Temple; shankar jharana |
| attraction=summer_toboggan | 0 | 0 | 0 | | | |  |
| climbing=crag | 0 | 0 | 0 | | | |  |
| leisure=water_park | 21 | 6 | 1 | 13 / 1 / 0 | n8 w13 r0 | 0.1 km² (valley 0.03) | Whoopee Land Amusement and Water Park; Wonderland; James World Fun Park |

## Places of worship by religion

| religion | Nepal | Valley | Core | named | wikidata |
|---|---:|---:|---:|---:|---:|
| hindu | 6,826 | 1,352 | 352 | 5,947 | 25 |
| (none) | 1,167 | 367 | 47 | 925 | 2 |
| buddhist | 1,068 | 299 | 98 | 768 | 2 |
| christian | 336 | 80 | 9 | 285 | 1 |
| muslim | 331 | 2 | 2 | 309 | 0 |
| buddhist;hindu | 8 | 7 | 0 | 7 | 0 |
| multifaith | 5 | 4 | 4 | 5 | 0 |
| sikh | 4 | 1 | 0 | 3 | 0 |
| bahai | 3 | 2 | 0 | 3 | 0 |
| bon | 3 | 1 | 0 | 2 | 0 |
| none | 2 | 0 | 0 | 2 | 0 |
| baun | 2 | 0 | 0 | 2 | 0 |
| jain | 2 | 1 | 1 | 2 | 0 |
| osho | 1 | 0 | 0 | 1 | 0 |
| newari | 1 | 0 | 0 | 1 | 0 |
| waaaaa | 1 | 1 | 0 | 1 | 0 |
| kirat | 1 | 0 | 0 | 1 | 0 |
| hindu;budhhist | 1 | 0 | 0 | 1 | 0 |
| ghostism | 1 | 0 | 0 | 1 | 0 |
| birendra_ma_bi | 1 | 0 | 0 | 1 | 0 |
| manileka_ma_bi | 1 | 0 | 0 | 1 | 0 |
| shinto | 1 | 0 | 0 | 1 | 0 |
| koimenia_church | 1 | 1 | 0 | 0 | 0 |
| jewish | 1 | 0 | 0 | 0 | 0 |
| sunuwar | 1 | 0 | 0 | 1 | 0 |
| hindu;buddhist | 1 | 1 | 0 | 1 | 0 |

Buildings: Nepal 8,297,951, valley 511,189, core 133,712 (named 16,696 / 5,123). Top values Nepal: yes 8,090,864, house 149,594, residential 32,075, school 6,755, industrial 2,805, greenhouse 1,662, commercial 1,540, construction 1,391, apartments 1,302, hut 1,042, detached 997, shed 793

## Roads and trails (km)

| highway | ways Nepal | km Nepal | km Valley | km Core | named ways % | named km Nepal | name:ne ways | bridges (Nepal / Valley) | tunnels | surface-tagged km % |
|---|---:|---:|---:|---:|---:|---:|---:|---|---:|---:|
| path | 264,024 | 107,451 | 1,278 | 39 | 1.2 | 2,470 | 25 | 5,436 / 97 | 83 | 25.4 |
| unclassified | 89,185 | 76,329 | 1,824 | 25 | 2.1 | 2,281 | 99 | 4,072 / 135 | 23 | 15.3 |
| track | 91,799 | 43,953 | 956 | 4 | 0.6 | 482 | 20 | 1,779 / 31 | 48 | 8.2 |
| residential | 114,771 | 28,288 | 3,054 | 563 | 6.2 | 1,990 | 884 | 1,951 / 222 | 9 | 10.2 |
| tertiary | 10,694 | 15,931 | 476 | 58 | 17.5 | 2,898 | 287 | 1,187 / 67 | 3 | 25.7 |
| primary | 3,256 | 5,201 | 148 | 25 | 45.5 | 2,763 | 360 | 729 / 31 | 0 | 40.8 |
| secondary | 3,317 | 4,793 | 396 | 67 | 35.0 | 1,761 | 423 | 593 / 54 | 4 | 37.2 |
| service | 21,417 | 3,247 | 441 | 130 | 0.9 | 50 | 16 | 226 / 22 | 15 | 9.7 |
| trunk | 2,120 | 2,399 | 118 | 19 | 85.9 | 2,023 | 1,146 | 625 / 37 | 6 | 40.8 |
| footway | 11,933 | 2,297 | 243 | 80 | 4.1 | 114 | 22 | 930 / 109 | 24 | 8.4 |
| road | 1,450 | 465 | 8 | 0 | 0.8 | 6 | 0 | 33 / 1 | 0 | 3.2 |
| construction | 225 | 268 | 18 | 0 | 20.0 | 138 | 40 | 46 / 0 | 7 | 3.3 |
| living_street | 983 | 141 | 26 | 8 | 8.6 | 16 | 1 | 6 / 0 | 0 | 10.6 |
| steps | 1,134 | 115 | 23 | 4 | 6.1 | 14 | 17 | 18 / 1 | 0 | 22.6 |
| pedestrian | 417 | 76 | 12 | 6 | 11.8 | 9 | 2 | 22 / 3 | 0 | 24.6 |
| cycleway | 84 | 26 | 18 | 2 | 16.7 | 16 | 0 | 4 / 1 | 0 | 3.5 |
| bridleway | 57 | 19 | 2 | 0 | 10.5 | 1 | 0 | 9 / 1 | 0 | 16.1 |
| tertiary_link | 39 | 14 | 0 | 0 | 10.3 | 0 | 2 | 2 / 1 | 0 | 0.0 |
| trunk_link | 74 | 4 | 1 | 0 | 8.1 | 0 | 0 | 5 / 0 | 0 | 4.4 |
| primary_link | 93 | 2 | 0 | 0 | 2.2 | 0 | 0 | 0 / 0 | 0 | 16.0 |
| rest_area | 33 | 1 | 0 | 0 | 21.2 | 0 | 0 | 0 / 0 | 0 | 0.0 |

Distinct highway names: Nepal 9,297, valley 3,253, core 1,473.

## Winding roads (curvature)

per way: Douglas-Peucker simplification at 5 m removes digitising jitter; turning = sum |heading change| at vertices; hairpin = same-sense cumulative turn >= 120 deg within 60 m of path (non-overlapping; in town grids a U-shaped street also counts). Named roads are aggregated by exact name across ways (a generic name may merge separate roads: check extent_deg). Turning between consecutive ways is not counted.

| class | km Nepal | deg/km Nepal | hairpins Nepal | hairpins/100 km | deg/km Valley | hairpins Valley |
|---|---:|---:|---:|---:|---:|---:|
| trunk | 2,353 | 151.6 | 399 | 16.96 | 146.2 | 12 |
| primary | 5,162 | 388.6 | 2,910 | 56.37 | 256.5 | 25 |
| secondary | 4,766 | 424.8 | 3,017 | 63.3 | 447.8 | 226 |
| tertiary | 15,880 | 524.9 | 14,660 | 92.32 | 518.5 | 319 |
| unclassified | 76,061 | 604.6 | 85,684 | 112.65 | 831.9 | 2,637 |
| track | 43,651 | 613.5 | 46,414 | 106.33 | 911.9 | 1,595 |

**Top 20 most winding named roads in Nepal (>= 2 km)**

| road | ref | km | deg/km | hairpins | classes | centroid | extent (deg) |
|---|---|---:|---:|---:|---|---|---|
| Dunai to Tiplagaun |  | 2.0 | 1675.6 | 17 | track | [82.8929, 28.9499] | [0.0, 0.0] |
| Mahabhir- Tikot | 43DR019 | 2.0 | 1422.1 | 12 | unclassified | [83.6209, 28.4464] | [0.0, 0.0] |
| Fikkal Nayabazar Namsaling Road |  | 4.7 | 1374.5 | 18 | unclassified | [88.0615, 26.9537] | [0.0, 0.0] |
| bajasthal rajasthal margha |  | 2.2 | 1374.3 | 11 | unclassified | [84.0467, 28.0656] | [0.0, 0.0] |
| Phadi-Setidevi Marga |  | 2.1 | 1320.0 | 9 | track | [85.0807, 27.7431] | [0.0, 0.0] |
| Nurigad-Marphan |  | 7.3 | 1311.3 | 13 | track | [82.1151, 29.4072] | [0.02, 0.01] |
| pipal chautara municipality road | 67DR002 | 3.0 | 1309.6 | 13 | unclassified | [81.3325, 29.3611] | [0.0, 0.0] |
| Bahrabise - Ramche - Ghorthali Road |  | 7.0 | 1286.7 | 33 | tertiary | [85.8729, 27.7839] | [0.0, 0.0] |
| Tahuley Road |  | 4.4 | 1279.1 | 10 | unclassified | [85.2538, 27.7913] | [0.0, 0.0] |
| Tilchwok road |  | 3.1 | 1273.3 | 8 | unclassified | [85.5056, 27.5711] | [0.0, 0.0] |
| Palu bari road |  | 2.6 | 1256.9 | 5 | unclassified | [85.4806, 27.7276] | [0.01, 0.0] |
| Mahabhir Gharamdi |  | 8.7 | 1254.1 | 45 | unclassified | [83.6286, 28.4419] | [0.0, 0.0] |
| .. |  | 3.7 | 1251.3 | 11 | unclassified | [85.5847, 27.6567] | [0.0, 0.0] |
| Bhitriban Road | 46A021 | 11.8 | 1223.1 | 28 | unclassified | [83.401, 28.1328] | [0.01, 0.03] |
| Krishi M. |  | 2.2 | 1214.4 | 10 | unclassified | [85.5205, 27.6457] | [0.0, 0.0] |
| Badhkhola-Raniswanra-Nuwakot Naya Bato |  | 3.7 | 1200.3 | 13 | unclassified | [83.8914, 28.1081] | [0.0, 0.0] |
| Maane Tallo Ghyang Road |  | 2.6 | 1190.4 | 11 | unclassified | [86.0321, 27.5643] | [0.01, 0.01] |
| Myanglung-BusPark-Okhre | 08DR008 | 11.7 | 1189.3 | 47 | tertiary | [87.5236, 27.1043] | [0.0, 0.0] |
| tongwa chok-chiyabagan-okmalu swaaule bhirgaaun sadak |  | 6.2 | 1184.8 | 21 | unclassified | [87.3249, 27.0291] | [0.0, 0.0] |
| Khadbari-Ramche-Lebrang-Dake-Bahrabise | 09DR009 | 7.6 | 1159.9 | 24 | tertiary | [87.2241, 27.3791] | [0.0, 0.0] |

**Most winding named roads >= 10 km**

| road | ref | km | deg/km | hairpins | classes | centroid | extent (deg) |
|---|---|---:|---:|---:|---|---|---|
| Bhitriban Road | 46A021 | 11.8 | 1223.1 | 28 | unclassified | [83.401, 28.1328] | [0.01, 0.03] |
| Myanglung-BusPark-Okhre | 08DR008 | 11.7 | 1189.3 | 47 | tertiary | [87.5236, 27.1043] | [0.0, 0.0] |
| dipayal to khullek | 70DR017 | 11.0 | 1154.9 | 27 | unclassified | [80.9421, 29.2866] | [0.0, 0.0] |
| Nagdanda-sirubari road | NH70-002 | 11.0 | 1086.6 | 40 | tertiary | [83.8126, 28.1357] | [0.03, 0.0] |
| Bhairabkunda Road |  | 13.4 | 1066.7 | 46 | unclassified | [85.8719, 27.9406] | [0.0, 0.0] |
| diktel to khotangbazar road | NH12 | 29.0 | 1013.2 | 71 | secondary | [86.8185, 27.106] | [0.1, 0.13] |
| Narikot-Pathihalna-Khung-Pokhra-Gulmi | 52A011R | 14.3 | 1002.1 | 21 | tertiary | [83.0105, 28.1983] | [0.02, 0.01] |
| Local village road |  | 10.7 | 977.3 | 25 | tertiary | [82.4136, 28.2837] | [0.0, 0.0] |
| Manahari Rupachuri Gramin Sadak | 31DR004 | 13.0 | 936.4 | 22 | track, tertiary | [84.7881, 27.5634] | [0.03, 0.02] |
| Gumtang-Bhairabkunda Road | 23DR026 | 35.5 | 935.7 | 87 | unclassified, tertiary | [85.8626, 27.8566] | [0.01, 0.07] |
| Khadbari-Badreni-Lingling-Chainpur | 09DR010 | 28.5 | 932.8 | 68 | tertiary | [87.2625, 27.3344] | [0.08, 0.06] |
| Darsing - Sankhar Road | 39DR038 | 10.3 | 930.2 | 17 | tertiary | [83.8918, 27.9276] | [0.03, 0.03] |
| Manebhanjyang-Sitalpati-Heluwabesi | 09DR011 | 13.5 | 926.9 | 29 | unclassified, track | [87.1573, 27.4177] | [0.05, 0.02] |
| Road to Myanlung |  | 17.1 | 924.4 | 45 | tertiary | [87.556, 27.0825] | [0.0, 0.0] |
| Helambu Trek | F26 | 25.0 | 910.5 | 38 | unclassified, secondary, tertiary | [85.4687, 27.8159] | [0.05, 0.15] |
| waling-huwas | 39DR006 | 13.5 | 903.1 | 26 | unclassified | [83.724, 27.9982] | [0.02, 0.02] |
| Khandbari-Ramche-Lebrang-Dake-Bahrabise | 09DR009 | 17.1 | 895.0 | 29 | tertiary | [87.2701, 27.4011] | [0.04, 0.03] |
| Hurke-Qureni Road | 59DR008 | 14.6 | 891.1 | 33 | tertiary | [81.4841, 28.6611] | [0.0, 0.0] |
| Hile-Sadeshwor mandir-sewa kendra bhawan-madhuganga-malbaase-bihibaare hatiya sadak | 07DR016 | 19.0 | 886.9 | 31 | unclassified | [87.346, 27.0168] | [0.03, 0.06] |
| Chuniya – Namtar –Kalikatar-Bharta- Khairang Road | 31DR007 | 19.2 | 886.6 | 36 | tertiary | [84.9759, 27.5628] | [0.08, 0.04] |

**Most hairpins (named roads)**

| road | ref | km | deg/km | hairpins | classes | centroid | extent (deg) |
|---|---|---:|---:|---:|---|---|---|
| Mahakali Highway | NH03 | 310.6 | 595.7 | 264 | primary, secondary, trunk | [80.5518, 29.3884] | [0.23, 1.07] |
| Tribhuvan Highway | NH41 | 121.7 | 709.1 | 182 | primary, trunk | [85.0608, 27.544] | [0.21, 0.63] |
| मेची राजमार्ग | NH04 | 171.5 | 508.4 | 155 | primary, secondary | [87.9363, 26.909] | [0.35, 0.61] |
| राप्ती राजमार्ग | NH55 | 131.1 | 566.0 | 118 | primary | [82.3125, 28.4008] | [0.26, 0.46] |
| Sanphebagar-Martadi-Kolti Highway | NH63 | 94.7 | 636.1 | 110 | primary | [81.4387, 29.402] | [0.44, 0.25] |
| Siddhartha Highway | NH47 | 171.6 | 459.5 | 90 | trunk | [83.6634, 27.8992] | [0.51, 0.67] |
| Gumtang-Bhairabkunda Road | 23DR026 | 35.5 | 935.7 | 87 | unclassified, tertiary | [85.8626, 27.8566] | [0.01, 0.07] |
| Koshi Rajmarg | NH03 | 111.3 | 328.2 | 83 | primary | [87.2943, 26.8906] | [0.07, 0.58] |
| Karnali Rajmarg | NH58 | 139.9 | 549.3 | 79 | primary | [81.5175, 28.849] | [0.17, 0.53] |
| BP Highway | NH13 | 157.1 | 403.6 | 76 | trunk | [85.8506, 27.3441] | [0.45, 0.6] |
| diktel to khotangbazar road | NH12 | 29.0 | 1013.2 | 71 | secondary | [86.8185, 27.106] | [0.1, 0.13] |
| Bhimphedi – Kogate – Ipa Deurali- Sisneri Road | 31DR010 | 30.0 | 870.8 | 68 | unclassified | [85.1889, 27.5329] | [0.09, 0.01] |
| Khadbari-Badreni-Lingling-Chainpur | 09DR010 | 28.5 | 932.8 | 68 | tertiary | [87.2625, 27.3344] | [0.08, 0.06] |
| Gaindakot - Rampur Road | NH09 | 46.5 | 681.6 | 67 | secondary, tertiary | [84.0383, 27.8436] | [0.26, 0.05] |
| Jeep Road Beni-Lo Manthang | NH48 | 84.5 | 416.0 | 64 | primary, track, unclassified | [83.8548, 29.002] | [0.22, 0.4] |
| Adi Kailash Parvat road | NH9 | 72.0 | 465.6 | 62 | primary | [80.7839, 30.0856] | [0.27, 0.26] |
| Pasang Lhamu Highway | NH18 | 80.3 | 585.0 | 61 | primary | [85.2201, 27.9796] | [0.2, 0.37] |
| Bhojpur-Takshar-Gumba-Dalgau-Bhulke-Dhodhlekhani Road | 10DR007 | 36.6 | 792.6 | 61 | tertiary, secondary | [86.9985, 27.15] | [0.13, 0.02] |
| Dailekha Rajmarg | NH60 | 54.8 | 635.0 | 56 | secondary | [81.6404, 28.7034] | [0.07, 0.16] |
| Kailash-Manasarovar Road | NH9 | 19.2 | 727.1 | 54 | tertiary | [80.988, 30.2384] | [0.07, 0.0] |

**Most winding named roads in the valley**

| road | ref | km | deg/km | hairpins | classes | centroid | extent (deg) |
|---|---|---:|---:|---:|---|---|---|
| Tahuley Road |  | 4.4 | 1279.1 | 10 | unclassified | [85.2538, 27.7913] | [0.0, 0.0] |
| Tilchwok road |  | 3.1 | 1273.3 | 8 | unclassified | [85.5056, 27.5711] | [0.0, 0.0] |
| Palu bari road |  | 2.6 | 1256.9 | 5 | unclassified | [85.4806, 27.7276] | [0.01, 0.0] |
| Krishi M. |  | 2.2 | 1214.4 | 10 | unclassified | [85.5205, 27.6457] | [0.0, 0.0] |
| nala nagarkot marga | F98 | 3.9 | 1153.8 | 12 | secondary, unclassified | [85.5122, 27.686] | [0.01, 0.0] |
| Kothaukhola -Nagarkot Sadak |  | 3.7 | 1131.2 | 11 | unclassified | [85.5269, 27.665] | [0.0, 0.0] |
| Magada Road |  | 3.6 | 1114.9 | 8 | unclassified | [85.2582, 27.7928] | [0.0, 0.0] |
| Way to Nagarkot | F28 | 7.0 | 1093.7 | 15 | secondary | [85.5026, 27.7115] | [0.03, 0.01] |
| Helambu Trek | F26 | 13.8 | 1021.9 | 28 | unclassified, secondary, tertiary | [85.4775, 27.7875] | [0.04, 0.06] |
| kalamasi ghimie gaun |  | 2.5 | 988.7 | 2 | unclassified, track | [85.5108, 27.685] | [0.01, 0.0] |
| Telkot |  | 2.8 | 986.2 | 2 | unclassified | [85.465, 27.7077] | [0.01, 0.0] |
| Goshpul Sudal Kamalsi Road |  | 3.9 | 965.9 | 12 | unclassified | [85.4926, 27.6806] | [0.0, 0.0] |
| Taukhal Road |  | 2.2 | 923.4 | 2 | track | [85.4941, 27.5952] | [0.01, 0.01] |
| Nala Nagarkot Marga |  | 4.6 | 917.5 | 8 | unclassified | [85.4998, 27.6707] | [0.0, 0.0] |
| Kakani Park Marg | F77 | 4.5 | 877.4 | 6 | primary | [85.2612, 27.8135] | [0.0, 0.0] |
| Godavari Phulchoki Marg | F24 | 12.6 | 874.3 | 33 | unclassified | [85.3933, 27.5779] | [0.02, 0.01] |
| Suntole Marg | F96 | 6.0 | 858.7 | 10 | secondary, unclassified | [85.4999, 27.7256] | [0.03, 0.01] |
| Mahamanjushree Marga | F98 | 2.5 | 842.4 | 4 | secondary | [85.4785, 27.6865] | [0.01, 0.0] |
| Trishuli Highway | F76 | 11.6 | 841.5 | 22 | tertiary | [85.2529, 27.7795] | [0.05, 0.01] |
| Way to Dakshinkali |  | 4.2 | 802.9 | 7 | track | [85.2713, 27.5952] | [0.0, 0.0] |

**Hairpin hotspots (0.1° cells):** (85.2, 27.8) 970, (85.5, 27.8) 919, (88.1, 26.9) 912, (85.6, 27.6) 863, (85.6, 27.8) 841, (85.3, 27.9) 813, (85.1, 27.8) 780, (84.9, 27.8) 767, (85.6, 27.7) 758, (85.8, 27.5) 738, (85.5, 27.7) 721, (83.8, 28.0) 704

## Bridges

Bridge ways by class (Nepal count / valley count): path 5,436/97, unclassified 4,072/135, residential 1,951/222, track 1,779/31, tertiary 1,187/67, footway 930/109, primary 729/31, trunk 625/37, secondary 593/54, service 226/22, construction 46/0, road 33/1, pedestrian 22/3, steps 18/1

Foot bridges (footway/path/steps/...): Nepal 6,419 (370.31 km), valley 212. Suspension (tag or name): Nepal 594 (54.01 km), valley 16. bridge=* values: {'yes': 17568, 'low_water_crossing': 31, 'boardwalk': 23, 'cantilever': 17, 'aqueduct': 7, 'construction': 7, 'covered': 6, 'movable': 4, 'abandoned': 3, 'viaduct': 3, 'log_bridge': 3, 'concrete': 1, 'wodden': 1, 'suspension_bridge': 1, '+1': 1, 'suspension': 1, 'strava;survey': 1, 'bridge': 1}. bridge:structure: {'(none)': 15525, 'simple-suspension': 1285, 'suspension': 562, 'beam': 195, 'truss': 27, 'wooden': 16, 'simple_wooden': 16, 'simple-wodden': 15, 'log': 14, 'arch': 8, 'cable-stayed': 6, 'humpback': 3, 'floating': 2, 'wood': 1, 'concrete': 1, 'yes': 1, 'beam bridge': 1, 'suspention': 1}.

Longest suspension bridges: Dodhara Chandani Bridge 1483.3 m (footway, 80.11498,28.92391); झोलुङ्गे पुल 880.7 m (footway, 82.71401,27.81831); Kusma-Balewa Bridge (Kushma Bungee Footbridge) 492.8 m (path, 83.66986,28.22194); (unnamed) 421.7 m (unclassified, 81.29777,28.28467); Triveni-Balmiki 382.5 m (path, 83.93829,27.45747); suspension bridge 373.5 m (path, 83.32511,28.50434); (unnamed) 369.3 m (footway, 83.65086,28.15667); (unnamed) 349.1 m (path, 86.26524,26.92867); Kushma-Gyandi Suspension Bridge 342.1 m (footway, 83.67693,28.21024); Khokari Suspension Bridge 323.8 m (path, 82.1827,27.97585)

Longest foot bridges: Raniban Anadu Eco Trail 2919.3 m ((none)); (unnamed) 2026.1 m (simple-suspension); CHALTERAHO MUNNA BHAI 1518.0 m ((none)); Dodhara Chandani Bridge 1483.3 m (suspension); (unnamed) 1251.8 m ((none)); झोलुङ्गे पुल 880.7 m (suspension); (unnamed) 650.4 m (simple-suspension); (unnamed) 622.7 m ((none)); Baglung Parbat Footbridge 545.1 m ((none)); Kusma-Balewa Bridge (Kushma Bungee Footbridge) 492.8 m (suspension)

## Route relations

| route | count | named | sum km | union km | valley count | valley union km |
|---|---:|---:|---:|---:|---:|---:|
| road | 2148 | 144 | 47,690.7 | 38,157.5 | 135 | 984.5 |
| hiking | 127 | 109 | 6,226.4 | 4,015.9 | 11 | 47.5 |
| bus | 54 | 52 | 729.4 | 335.0 | 45 | 235.1 |
| power | 35 | 0 | 1,092.8 | 690.0 | 11 | 48.3 |
| railway | 12 | 12 | 151.6 | 151.6 | 0 | 0 |
| microbus | 11 | 11 | 93.7 | 59.6 | 11 | 59.6 |
| foot | 11 | 9 | 258.5 | 258.4 | 4 | 0.8 |
| tempo | 9 | 9 | 60.0 | 35.3 | 9 | 35.3 |
| bus;microbus | 4 | 4 | 39.9 | 16.1 | 4 | 16.1 |
| bicycle | 4 | 3 | 28.8 | 9.7 | 0 | 0 |
| micro | 3 | 3 | 33.5 | 22.7 | 3 | 22.7 |
| ? | 3 | 0 | 179.9 | 179.9 | 1 | 0.1 |
| microbus;tempo | 2 | 2 | 9.7 | 9.7 | 2 | 9.7 |
| janata yatayat bus | 1 | 1 | 17.7 | 17.7 | 1 | 17.7 |
| bishalnagar micro bus | 1 | 1 | 6.8 | 6.8 | 1 | 6.8 |
| swyambhu yatayt | 1 | 1 | 29.6 | 29.6 | 1 | 29.6 |
| trans himalaya | 1 | 1 | 9.6 | 9.6 | 0 | 0 |

**hiking** (longest): Great Himalayan Trail Nepal 1404.4 km; Annapurna Circuit 238.9 km; Upper Dolpo Circuit 209.6 km; Jomsom to Dolpo 170.7 km; Lumthi to Simikot 167.1 km; Manaslu Circuit 146.5 km; Manaslu 118.3 km; Dhaulagiri Trek 111.9 km; Kanchendjunga 1: Suketar - Base Camp 100.3 km; Three Passes Trek 99.3 km; Annapurna 90.8 km; Numbur Cheese Circuit 88.3 km; Jiri — Lukla trek 86.3 km; Piplang to Siraanchaur 84.9 km; Kanchendjunga 2: Taplejung - Base Camp 84.4 km; DHORPATAN TREK 78.0 km; Khanigaon to Chharka Bhot 76.8 km; Saldang to Chharka Bhot 73.3 km; Siraanchaur to Bhijer 71.0 km; Sangda La 68.4 km; GHT lower trails 67.3 km; Juphal to Pelma 64.8 km; Rupina La 64.7 km; Naar-Phu trek 64.2 km; Lumba Samba Pass 61.1 km

**foot** (longest): Kanchendjunga base camp trek 157.2 km; Ghang La track 57.4 km; Route to Tipta La 22.6 km; Yalung base camp trek 10.2 km; Path to Kimathanka 10.1 km; Purano Bus Park-Ratnapark 0.5 km; Bagbazaar- Ratnapark 0.2 km; Ratnapark - Bagbazar 0.2 km; r4762625 0.1 km; way to shivapuri 0.0 km; r17232123 0.0 km

**bicycle** (longest): Mathkot to Jauljibi 9.6 km; Rampur to Nepal 9.6 km; Trans Himalaya 9.6 km; r16062208 0.0 km

## Famous treks: what OSM has

| trek | route relations (km) | named trail ways (km) | objects named like it |
|---|---|---|---|
| Annapurna Circuit | Annapurna Circuit (238.9) | Annapurna circuit (14.2); Annapurna Circuit (5.3) | 70 (highway=path, highway=unclassified, highway=tertiary) |
| Annapurna Base Camp / Sanctuary | Annapurna Base Camp Trek (23.9) | way To ABC-Komrong (5.0) | 52 (tourism=hotel, amenity=school, amenity=restaurant) |
| Poon Hill / Ghorepani | **none** | Vamarkot-Ghorepani (8.2) | 28 (highway=path, place=village, natural=peak) |
| Mardi Himal | Mardi Himal trek (30.1); Mardi Himal Base Camp Trail (2.0) | Mardi Himal East ascent trail (10.9); Mardi Himal East Trail (8.9) | 36 (tourism=hotel, tourism=information, highway=path) |
| Khopra Danda | **none** | lower safer trail to Khopra Danda (1.1) | 3 (place=hamlet, tourism=guest_house, highway=path) |
| Everest Base Camp / Khumbu | Everest Base Camp Trek (53.8) | - | 82 (tourism=guest_house, amenity=restaurant, boundary=administrative) |
| Gokyo / Three Passes | Three Passes Trek (99.3) | - | 24 (tourism=guest_house, tourism=viewpoint, natural=peak) |
| Langtang Valley | Langtang Trek (30.9) | Langtang Trek Trail (29.4); Langtang Trek (3.8) | 76 (highway=path, building=hotel, -) |
| Gosaikunda / Helambu | Helambu Trek (43.3); GosainKunda Trek (38.6) | Gosain Kund Trek (21.1); GosainKunda Trek (17.9); Helambu Trek (17.9) | 105 (highway=path, highway=unclassified, boundary=administrative) |
| Manaslu Circuit | Manaslu Circuit (146.5); Manaslu (118.3) | Manaslu Circuit (102.8) | 213 (highway=path, highway=unclassified, tourism=guest_house) |
| Tsum Valley | Tsum Valley trek (38.1) | Tsum Valley Path (37.1) | 40 (highway=path, tourism=information, tourism=hotel) |
| Upper Mustang | **none** | old jeep road Beni-Lo Manthang (5.2); Jeep Road Beni-Lo Manthang (2.4) | 69 (highway=primary, highway=track, boundary=administrative) |
| Nar Phu | **none** | Trail to Nar-Phu (17.6) | 28 (highway=path, boundary=administrative, highway=track) |
| Kanchenjunga | Kanchendjunga 1: Suketar - Base Camp (100.3); Kanchendjunga 2: Taplejung - Base Camp (84.4); Kanchendjunga 2 Base Camp (54.9); Kanchendjunga base camp trek (157.2) | Ramche to Oktang (3.5) | 23 (tourism=alpine_hut, route=hiking, natural=peak) |
| Makalu Base Camp | Makalu base camp trek with Arun valley (53.3); Makalu Basecamp (20.0) | Makalu Marg (0.0) | 64 (boundary=administrative, amenity=restaurant, tourism=guest_house) |
| Rara Lake | **none** | Triveni-Seribazar Rara (19.0); Rara Ring Track (14.6); Seribazar-Rara-Nagma (4.4) | 98 (highway=path, boundary=administrative, amenity=school) |
| Dolpo / Phoksundo | Upper Dolpo Circuit (209.6); Jomsom to Dolpo (170.7); Lower Dolpo Circuit (2.6) | - | 49 (boundary=administrative, tourism=hotel, amenity=school) |
| Great Himalaya Trail | Great Himalayan Trail Nepal (1404.4) | Great Himalaya Trail (152.0) | 44 (highway=path, highway=unclassified, highway=track) |
| Tamang Heritage Trail | **none** | - | 1 (amenity=fast_food) |
| Chisapani / Nagarkot / Valley Rim | Chisapani-Nagarkot (21.0); Shivapuri Peak - Chisapani (9.5); Sundarijal-Chisapani (9.2); Nangi Gumba - Shivapuri Peak (4.2) | Shivapuri trail (1.2); Nagarkot Buddha Peace Garden Hiking Trail (0.3) | 185 (place=hamlet, tourism=hotel, amenity=school) |
| Pikey Peak | **none** | - | 7 (amenity=school, tourism=alpine_hut, leisure=resort) |
| Dhorpatan | DHORPATAN TREK (78.0) | - | 18 (boundary=administrative, aeroway=aerodrome, place=village) |
| Api / Saipal | **none** | API BASE CAMP TREK ROUTE (28.3); ROAD TO API BASE CAMP (2.4) | 38 (boundary=administrative, amenity=school, highway=path) |
| Ruby Valley / Ganesh Himal | Ganesh Himal Trail (34.3) | Ganesh Himal Trail (34.1) | 60 (highway=path, highway=track, tourism=guest_house) |
| Jiri | Jiri — Lukla trek (86.3) | Jiri-Shivalaya koshi bridge (0.0) | 56 (highway=secondary, boundary=administrative, amenity=bank) |
| Royal Trek | **none** | - | 0 () |
| Panch Pokhari | **none** | - | 23 (boundary=administrative, natural=water, tourism=hotel) |
| Kalinchowk | **none** | Kalinchok Footpath (1.3) | 73 (highway=tertiary, boundary=administrative, amenity=restaurant) |
| Limi Valley | **none** | - | 9 (waterway=river, tourism=hotel, tourism=guest_house) |
| Mohare Danda | Lespar - Mohare danda (8.7); Nangi - Mohare danda (8.1) | Mohare - Kokhe - Banthati (5.6); Mohare Danda to Hampal pass (1.0); Kokhe - Mohare (0.8) | 22 (tourism=information, highway=track, highway=path) |
| Champadevi / Chandragiri | **none** | - | 81 (boundary=administrative, amenity=school, amenity=place_of_worship) |

Named trail ways: 2,075 distinct names. Longest: Great Himalaya Trail 152.0 km; Manaslu Circuit 102.8 km; Shey to Saldang 42.2 km; Tsum Valley Path 37.1 km; Ganesh Himal Trail 34.1 km; Ringmo to Shey Trail 31.4 km; Tokyu to Chhamdang 29.5 km; Langtang Trek Trail 29.4 km; API BASE CAMP TREK ROUTE 28.3 km; road 22.5 km; Pangagaon to Charkabhot 21.6 km; Hurikot to Kagmara La 21.5 km; Gosain Kund Trek 21.1 km; Chhepka to Ringmo 19.6 km; Triveni-Seribazar Rara 19.0 km

## Waterfalls, rivers

Waterfalls: 335 (69 named; by tag {'waterway=waterfall': 335}; geometry {'n': 332, 'w': 3}; 132 sit on a waterway line; 5 in the valley bbox; 3 have height; 2 wikidata). Name-only candidates (named like a waterfall/jharana/chhahara but not tagged): 106.

Named waterfalls (first 40): Augusto waterfall; Bagre ko Chago; Basmati waterfall; Bayal Jharana; Bhulbhule waterfall; Chadaya Khola Jharana; Chiti चीती; Chyachhara Jharna; Dhungsel Jharna; Dulepani Chhahara; Fung Funge Jharana; Ganga Jamuna; Ghumaure घुमौरे झरना; Hayenga Waterfall; Hidden Wild Waterfall; Him Pipe; Him Pipe; Huge three level waterfall; Huge waterfall; Huge waterfall right next to the new road; Hyatung Waterfall; Jaljala / Virgin Falls; Jharana; Jharna  (Artificial ); Lamo jharna, Jalbire; Lauke Waterfall; Main Waterfall; Mallillin waterfall; Mello Waterfall; Mohini Waterfall; Nagarkot Waterfall; Namgung Gonpa Waterfall; Narchyang waterfall; New Bridge Jharana; Nice bathing; Nyauli Waterfall; Om Yogi waterfall; Patada Waterfall; Patale Chhango; Phutphute waterfall; Pretty Jomay's Waterfall; Punngpu; Purandhara Jharana; RAHUL'S HOME; River; Royal Twins Waterfall and Canyoning; Rupse Waterfalls; Small waterfall; Spry waterfall; Synakhudi Jharna; Teen Change Jharana; Tindhare jharana; Todke Jharna waterfall; Waterfal; Waterfall to begin hike; Waterfall with big natural pool; Waterfalls; Wayland-Joan Waterfalls; Wayland-Joan Waterfalls; Yaanaaa park; bedrock; small waterfall; unclisfied; धागेछहरा; नहर; नहरको घर; शिव जट्टा; १०८ छहरा; 开热瀑布

Longest rivers (relations): कर्णाली नदी 541.9 km; घाघरा 516.4 km; कालीगण्डकी 391.2 km; Trisuli River system 312.3 km; Sharda 304.2 km; Sun Kosi 279.8 km; Narayani river 267.6 km; Kamala River 241.6 km; Seti 232.5 km; Bagmati River 206.7 km; Arun River 201.7 km; Marsyangdi 192.2 km; Tamor 183.2 km; त्रिशुली नदी 163.6 km; Seti Gandaki 152.6 km

Valley rivers (way sums): Bagmati Nadi (बागमती नदी) 49.1 km; Kalphu River 14.8 km; Punyamata River 14.0 km; Godawari Khola 12.5 km; Manohara Nadi 10.3 km; Kodku Khola 9.3 km; Roshi Khola 8.2 km; Bishnumati River 8.2 km; Bishnumati 7.5 km; Balkhu River 6.7 km; Lilawati Khola 6.7 km; Maheshkhola 6.5 km; Nakhhu 6.4 km; Kasan Khola 6.1 km; Sangle River 6.0 km

Protected-area relations: 24. Largest: अन्‍नपूर्ण संरक्षण क्षेत्र (protected_area, 7,496.2 km²); शे-फोकसुण्डो राष्ट्रिय निकुञ्‍ज (national_park, 3,553.4 km²); गौरीशंकर संरक्षण क्षेत्र (protected_area, 2,203.9 km²); लाङ्टाङ् राष्ट्रिय निकुञ्‍ज (national_park, 1,803.7 km²); कञ्चनजङ्घा संरक्षण क्षेत्र (protected_area, 1,779.2 km²); मकालु बरुण राष्ट्रिय निकुञ्ज (national_park, 1,721.1 km²); मनास्लु संरक्षण क्षेत्र (protected_area, 1,629.1 km²); चितवन राष्ट्रिय निकुञ्‍ज (national_park, 1,203.4 km²); सगरमाथा राष्ट्रिय निकुञ्ज (national_park, 1,131.9 km²); Dudhwa Tiger Reserve (protected_area, 862.8 km²); Valmiki WLS/Tiger Reserve (protected_area, 782.8 km²); Askot Musk Deer Wildlife Sanctuary (protected_area, 624.8 km²); Sohelwa WLS (protected_area, 620.7 km²); पर्सा राष्ट्रिय निकुञ्‍ज (national_park, 574.6 km²); शुक्लाफाँट राष्ट्रिय निकुञ्‍ज (national_park, 396.0 km²); Valmiki National park (national_park, 357.9 km²); कोशी टप्पु वन्यजन्तु आरक्ष (protected_area, 141.5 km²); रारा राष्ट्रिय निकुञ्‍ज (national_park, 113.4 km²); शिवपुरी नागार्जुन राष्ट्रिय निकुञ्‍ज (national_park, 103.0 km²); Khangchendzonga National Park (national_park, 0.0 km²)

## Adventure

Adventure-flavoured objects: 394 Nepal, 165 valley. By reason: {'name': 131, 'attraction/whitewater/climbing': 95, 'sport=swimming': 44, 'tourism=theme_park': 22, 'leisure=water_park': 16, 'leisure=fishing': 13, 'sport=free_flying': 12, 'aerialway=zip_line': 10, 'sport=climbing': 6, 'sport=motor': 6, 'aerialway=gondola': 6, 'leisure=amusement_arcade': 4, 'leisure=horse_riding': 4, 'tourism=zoo': 4, 'leisure=golf_course': 4, 'sport=archery': 2, 'aerialway=cable_car': 2, 'sport=golf': 1, 'leisure=miniature_golf': 1, 'sport=canoe;kayak;rafting': 1, 'sport=climbing;fitness;weightlifting': 1, 'sport=swimming;multi': 1, 'leisure=ice_rink': 1, 'sport=Paragliding': 1, 'leisure=trampoline_park': 1, 'sport=gymnastics; swimming; badminton; sauna': 1, 'sport=soccer;swimming;badminton;volleyball;karate;table_tennis': 1, 'sport=zipline': 1, 'sport=karting': 1, 'sport=cycling': 1}.

Tagged adventure items (sample): (unnamed) [leisure=fishing]; Chhetri Sisters Cliff [sport=climbing]; Nepal Golf Zone [sport=golf]; Mahendra Park [tourism=theme_park]; Nature Club [sport=swimming]; Golf School [leisure=miniature_golf]; (unnamed) [sport=swimming]; Hardic Fitness Centre Pool [sport=swimming]; Club Mosses Swimming Pool [sport=swimming]; Astrek Climbing Wall [sport=climbing]; Sukute Beach [sport=canoe;kayak;rafting]; bagmani masala research [tourism=theme_park]; Baneshwar Spa [sport=swimming]; BIG SPLASH [leisure=water_park]; (unnamed) [sport=motor]; (unnamed) [sport=motor]; P and B Game house [leisure=amusement_arcade]; Pokhari आईतबारे पोखरी ६६७ [sport=swimming]; Kathmandu Sport Climbing Centre [sport=climbing]; Outdoor Adventure Centre [sport=climbing;fitness;weightlifting]; Pony Farm [leisure=horse_riding]; Old Paragliding Landing Zone [sport=free_flying]; Bhimsen Park [tourism=theme_park]; Village Fish hachery [leisure=fishing]; Dhanushadham Protected Forest - Zoo Area [tourism=zoo]; Bulet Billiards [leisure=amusement_arcade]; One Horned Rhinoceros Exit Point [attraction/whitewater/climbing]; पोखरि [leisure=fishing]; ग्यारेज [sport=motor]; Kathmandu Sport Climbing Center [sport=climbing]; Wind Horse Stables [leisure=horse_riding]; (unnamed) [tourism=theme_park]; Pony Services [attraction/whitewater/climbing]; पुरानो प्याराग्लाइडिङ टेकऑफ [sport=free_flying]; (unnamed) [attraction/whitewater/climbing]; Thapa agrovet center [attraction/whitewater/climbing]; (unnamed) [attraction/whitewater/climbing]; tarahara pond [leisure=fishing]; Heaven Water Park [sport=swimming;multi]; Panauti Ekikrit Machha tahta Pashupanch Farm [leisure=fishing]

Name-only adventure items (sample): Epic Mountain Bikes [amenity=bicycle_rental]; Swing [amenity=festival_grounds]; Jungle Safari Lodge [tourism=guest_house]; Elephant Safari Resort [tourism=guest_house]; Safari Adventure Lodge [tourism=hotel]; Manakamana cable car hill station [aerialway=station]; Manakamana Cable Car Station [aerialway=station]; Adventure South Asia Tours and Travel [tourism=information]; The BABU Adventures HQ [tourism=guest_house]; GO KART RACING NEPAL [-]; Icicles Adventure Treks and Tours [-]; Overlander Camping [tourism=caravan_site]; Shankhapura Paragliding Take off site [tourism=attraction]; Wild Trak Adventure [tourism=hotel]; Highground Adventure PVT.LTD. [aerialway=pylon]; Adventure Jaljale trekking & Expedition (P) Ltd. [tourism=outdoor_activities]; Offroadnepal Pvt Ltd [tourism=information]; Adventure home [tourism=hotel]; Nepalthok camping site. [tourism=camp_site]; Nepal Flying (paragliding school) [office=educational_institution]; Go Kart [tourism=attraction]; Nature Safari Resort [tourism=hotel]; Fly Nirvana Paragliding [office=company]; Crocodile Safari Lodge & Camp [tourism=hostel]; Rock climbing crag [tourism=attraction]; Big Smile Paragliding [office=company]; Meghauli Serai Chitwan National Park - A Taj Safari Lodge [tourism=hotel]; Adrenaline Rush Rafting Camp [tourism=camp_site]; Fishtail Nepal Office Paragliding Pvt. Ltd [-]; View Manaslu Camping Resort [leisure=resort]; camping for porters [tourism=camp_site]; Trisuli center rafting [tourism=information]; Nepal Flying Paragliding School [office=educational_institution]; Kahare Paragliding Parking [amenity=parking]; Sedi Camping side [tourism=camp_site]; Kayakalpa Diet Clinic [amenity=clinic]; Kathmandu Sport Climbing Center [-]; Mountain Overview Paragliding [office=company]; Camping [tourism=camp_site]; Camping [tourism=camp_site]

## Name keywords (Nepali geography & heritage words)

| keyword | Nepal | Valley | Core | top primary tags (Nepal) |
|---|---:|---:|---:|---|
| waterfall | 206 | 19 | 4 | waterway=waterfall 52, boundary=administrative 27, amenity=restaurant 15, office=government 11, tourism=attraction 10 |
| cave | 135 | 16 | 7 | amenity=place_of_worship 29, tourism=attraction 26, natural=cave_entrance 26, boundary=administrative 7, - 5 |
| hot_spring | 81 | 1 | 0 | natural=hot_spring 11, boundary=administrative 9, amenity=school 8, tourism=hotel 6, natural=spring 6 |
| lake_pond | 1,482 | 181 | 50 | natural=water 388, - 387, place=hamlet 73, amenity=school 69, natural=spring 55 |
| river_khola | 3,977 | 194 | 19 | waterway=stream 1703, waterway=river 1318, natural=spring 380, place=hamlet 94, - 56 |
| hill_danda | 878 | 64 | 8 | place=hamlet 275, natural=spring 118, amenity=school 100, - 44, place=locality 32 |
| pass_bhanjyang | 1,828 | 92 | 25 | mountain_pass=yes 963, natural=saddle 185, amenity=school 107, place=hamlet 67, amenity=restaurant 43 |
| himal_peak | 674 | 140 | 59 | natural=peak 138, amenity=school 97, tourism=hotel 54, tourism=guest_house 45, place=region 28 |
| glacier | 176 | 5 | 2 | natural=glacier 147, natural=water 6, tourism=viewpoint 4, tourism=hotel 3, tourism=camp_site 2 |
| viewpoint | 267 | 29 | 3 | tourism=viewpoint 135, tourism=guest_house 29, amenity=restaurant 24, tourism=hotel 16, tourism=attraction 11 |
| fort_gadhi_kot | 256 | 25 | 3 | boundary=administrative 56, place=hamlet 29, amenity=place_of_worship 26, amenity=school 25, place=village 20 |
| durbar_palace | 431 | 218 | 95 | amenity=restaurant 75, tourism=hotel 61, amenity=events_venue 53, amenity=community_centre 20, building=yes 18 |
| temple_mandir | 5,986 | 1,227 | 317 | amenity=place_of_worship 5234, amenity=school 229, building=yes 66, - 63, landuse=religious 36 |
| stupa_chaitya | 285 | 84 | 38 | amenity=place_of_worship 181, historic=wayside_shrine 16, historic=monument 12, tourism=hotel 8, - 7 |
| gompa_monastery | 550 | 83 | 34 | amenity=place_of_worship 334, amenity=school 37, amenity=monastery 27, place=hamlet 21, building=yes 11 |
| ghat | 132 | 24 | 5 | place=hamlet 22, amenity=place_of_worship 20, natural=spring 12, landuse=cemetery 8, place=village 7 |
| bahal_baha_courtyard | 68 | 64 | 22 | amenity=place_of_worship 31, place=locality 7, place=neighbourhood 3, leisure=park 3, leisure=common 3 |
| hiti_dhunge_dhara | 546 | 224 | 45 | natural=spring 237, historic=stone_tap 72, man_made=water_tap 63, amenity=drinking_water 39, amenity=water_point 25 |
| pati_sattal_chautara | 756 | 85 | 13 | natural=tree 368, amenity=bank 48, - 37, amenity=school 21, amenity=place_of_worship 20 |
| chowk_square | 1,862 | 339 | 107 | - 527, place=locality 271, highway=bus_stop 271, place=neighbourhood 161, amenity=bus_station 69 |
| bridge_pul | 317 | 69 | 33 | - 100, tourism=attraction 31, amenity=school 15, amenity=police 14, tourism=viewpoint 13 |
| forest_ban | 703 | 81 | 10 | natural=wood 165, landuse=forest 99, office=government 74, - 52, tourism=hotel 28 |
| park_garden | 1,087 | 429 | 183 | leisure=park 289, amenity=restaurant 106, amenity=bus_station 84, tourism=hotel 67, amenity=school 61 |
| trek_trail | 564 | 250 | 226 | shop=travel_agency 184, shop=outdoor 52, tourism=information 34, route=hiking 34, tourism=camp_site 29 |
| tower_stambha | 256 | 57 | 16 | man_made=tower 90, tourism=viewpoint 32, man_made=communications_tower 25, building=yes 14, tourism=attraction 10 |
| adventure | 299 | 132 | 94 | shop=travel_agency 102, tourism=camp_site 30, building=yes 16, tourism=guest_house 11, tourism=hotel 11 |

## Pipeline classification of all Nepal (tags.poi_kind / area_kind / line_kind)

| PoiKind | Nepal | Valley | Core | Pack bbox | in valley pack (decoded) |
|---|---:|---:|---:|---:|---:|
| TEMPLE_HINDU | 6,829 | 1,353 | 352 | 1,377 | 1,384 |
| STUPA | 122 | 16 | 8 | 17 | 19 |
| GOMPA | 1,136 | 328 | 116 | 331 | 335 |
| SHRINE | 842 | 22 | 18 | 22 | 22 |
| MOSQUE | 331 | 2 | 2 | 2 | 2 |
| CHURCH | 337 | 80 | 9 | 80 | 80 |
| PLACE_OF_WORSHIP | 1,222 | 403 | 61 | 418 | 418 |
| CHORTEN | 3 | 0 | 0 | 0 | 0 |
| MANI_WALL | 0 | 0 | 0 | 0 | 0 |
| HERITAGE_SQUARE | 60 | 21 | 4 | 21 | 21 |
| PALACE | 129 | 16 | 2 | 16 | 16 |
| MONUMENT | 344 | 114 | 44 | 120 | 121 |
| RUINS | 167 | 12 | 1 | 12 | 12 |
| STONE_TAP | 147 | 125 | 22 | 126 | 126 |
| CITY_GATE | 53 | 10 | 0 | 10 | 10 |
| MUSEUM | 82 | 31 | 15 | 31 | 31 |
| PEAK | 3,956 | 11 | 0 | 12 | 12 |
| VIEWPOINT | 1,385 | 118 | 16 | 133 | 135 |
| WATERFALL | 334 | 5 | 0 | 6 | 7 |
| CAVE | 86 | 3 | 0 | 3 | 3 |
| LAKE | 468 | 50 | 5 | 51 | 51 |
| HOT_SPRING | 24 | 1 | 0 | 3 | 3 |
| GLACIER | 3,781 | 0 | 0 | 0 | 0 |
| PASS | 1,517 | 2 | 0 | 2 | 2 |
| SPRING | 5,422 | 1,206 | 2 | 1,872 | 1,932 |
| RIVER | 0 | 0 | 0 | 0 | 0 |
| PARK | 4,063 | 3,285 | 138 | 3,289 | 3,292 |
| PROTECTED_AREA | 64 | 6 | 2 | 6 | 6 |
| NOTABLE_TREE | 661 | 63 | 20 | 63 | 63 |
| RIDGE | 2,784 | 0 | 0 | 0 | 0 |
| AIRPORT | 58 | 1 | 1 | 1 | 1 |
| HELIPAD | 364 | 26 | 5 | 30 | 30 |
| BUS_STATION | 656 | 178 | 41 | 181 | 181 |
| FUEL | 631 | 158 | 43 | 159 | 159 |
| CABLE_CAR_STATION | 12 | 2 | 0 | 2 | 3 |
| RAILWAY_STATION | 24 | 0 | 0 | 0 | 0 |
| TAXI_STAND | 64 | 25 | 13 | 25 | 25 |
| BRIDGE | 180 | 8 | 1 | 8 | 8 |
| PARKING | 455 | 291 | 164 | 293 | 293 |
| HOTEL | 4,266 | 957 | 572 | 990 | 1,000 |
| GUEST_HOUSE | 3,535 | 636 | 227 | 651 | 652 |
| HOSTEL | 652 | 354 | 159 | 357 | 357 |
| CAMP_SITE | 595 | 39 | 6 | 39 | 39 |
| TEAHOUSE | 180 | 4 | 0 | 4 | 4 |
| RESTAURANT | 8,948 | 4,419 | 1,416 | 4,442 | 4,456 |
| CAFE | 2,070 | 1,329 | 476 | 1,344 | 1,347 |
| ATTRACTION | 1,059 | 269 | 63 | 279 | 277 |
| SHOP | 32,922 | 18,975 | 5,238 | 19,004 | 19,014 |
| MARKETPLACE | 362 | 58 | 23 | 58 | 58 |
| INFORMATION | 578 | 71 | 33 | 88 | 89 |
| PICNIC_SITE | 95 | 23 | 1 | 25 | 25 |
| THEME_PARK | 22 | 7 | 1 | 7 | 8 |
| ZOO | 4 | 1 | 0 | 1 | 1 |
| BUNGEE | 5 | 0 | 0 | 0 | 0 |
| PARAGLIDING | 23 | 1 | 0 | 1 | 1 |
| ZIPLINE | 13 | 1 | 0 | 1 | 1 |
| RAFTING | 3 | 1 | 1 | 1 | 1 |
| BOATING | 4 | 0 | 0 | 0 | 0 |
| SAFARI | 4 | 1 | 1 | 1 | 1 |
| SCHOOL | 26,974 | 3,293 | 1,053 | 3,374 | 3,407 |
| HOSPITAL | 4,998 | 856 | 257 | 863 | 870 |
| GOVERNMENT | 3,488 | 586 | 220 | 599 | 600 |
| BANK | 6,638 | 1,435 | 581 | 1,451 | 1,451 |

PoiKinds with zero objects in all Nepal: ['MANI_WALL', 'RIVER']. AreaKinds zero: []. LineKinds zero: ['CHAIR_LIFT'].

| AreaKind | Nepal count | Nepal km² | Valley count | Valley km² |
|---|---:|---:|---:|---:|
| FARMLAND | 125,965 | 11,383.03 | 2,459 | 79.4 |
| RESIDENTIAL | 86,085 | 1,894.43 | 1,343 | 26.6 |
| FOREST | 77,322 | 48,853.37 | 1,378 | 357.19 |
| WATER_POND | 24,626 | 125.86 | 292 | 0.28 |
| SCRUB | 22,969 | 1,489.48 | 327 | 10.65 |
| WATER_LAKE | 20,175 | 202.06 | 141 | 0.28 |
| GRASSLAND | 14,258 | 760.51 | 231 | 0.68 |
| SAND_SHINGLE | 8,529 | 652.46 | 71 | 0.39 |
| MEADOW | 6,748 | 344.52 | 584 | 2.66 |
| PARK | 4,765 | 24.84 | 3,345 | 2.07 |
| GLACIER | 3,781 | 4,755.61 | 0 | 0 |
| INDUSTRIAL | 2,992 | 76.72 | 216 | 2.3 |
| WATER_RIVER | 2,866 | 887.86 | 41 | 2.01 |
| WETLAND | 1,698 | 81.22 | 25 | 0.13 |
| ORCHARD | 1,479 | 32.12 | 41 | 0.15 |
| SCREE | 1,332 | 361.43 | 0 | 0 |
| BARE_ROCK | 1,224 | 286.11 | 5 | 0.03 |
| RELIGIOUS | 874 | 2.76 | 333 | 0.37 |
| PITCH | 715 | 3.27 | 283 | 0.78 |
| COMMERCIAL | 482 | 9.7 | 107 | 0.34 |
| TEA_GARDEN | 424 | 16.91 | 0 | 0 |
| MILITARY | 158 | 29.62 | 27 | 1.51 |
| PROTECTED | 54 | 32,507.32 | 4 | 103.41 |
| AERODROME | 48 | 15.05 | 1 | 2.65 |
| CEMETERY | 43 | 0.14 | 12 | 0.02 |
| PEDESTRIAN | 38 | 0.06 | 20 | 0.04 |

| LineKind | Nepal ways | Nepal km | Valley ways | Valley km |
|---|---:|---:|---:|---:|
| STREAM | 50,045 | 66,589.6 | 318 | 250.2 |
| DITCH | 23,626 | 7,502.2 | 86 | 19.5 |
| CANAL | 5,760 | 3,842.9 | 18 | 7.9 |
| RIVER | 5,451 | 26,776.1 | 128 | 288.9 |
| RAILWAY | 317 | 186.3 | 0 | 0 |
| TAXIWAY | 294 | 31.9 | 53 | 9.0 |
| RUNWAY | 78 | 81.6 | 3 | 3.3 |
| CITY_WALL | 19 | 2.1 | 1 | 0.1 |
| ZIP_LINE | 10 | 4.7 | 1 | 1.7 |
| CABLE_CAR | 8 | 13.9 | 1 | 2.3 |
| MANI_WALL | 4 | 0.3 | 0 | 0 |
| WATERFALL | 3 | 3.5 | 0 | 0 |

## What reached the built packs

**kathmandu_valley** (built 2026-10-05T00:04:03Z, bbox [85.18, 27.55, 85.58, 27.83]): tiles 2282, roads 53523, road_km 10364.2, buildings 556960 (in tiles 542261), pois 42989, places 1118, areas 13882, lines 774, search entries 5956.

Decoded leaf tiles (pipeline/build/regions/kathmandu_valley/qa/*.geojson (qa_export decodes every leaf tile of the .ghpk)): 43,524 unique POIs; by kind: SHOP 19014, RESTAURANT 4456, SCHOOL 3407, PARK 3292, SPRING 1932, BANK 1451, TEMPLE_HINDU 1384, CAFE 1347, HOTEL 1000, HOSPITAL 870, GUEST_HOUSE 652, GOVERNMENT 600, PLACE_OF_WORSHIP 418, PLACE_LOCALITY 387, HOSTEL 357, GOMPA 335, PLACE_NEIGHBOURHOOD 315, PARKING 293, ATTRACTION 277, PLACE_HAMLET 190, BUS_STATION 181, FUEL 159, VIEWPOINT 135, STONE_TAP 126, MONUMENT 121, PLACE_SUBURB 106, INFORMATION 89, CHURCH 80, NOTABLE_TREE 63, MARKETPLACE 58, LAKE 51, CAMP_SITE 39, MUSEUM 31, HELIPAD 30, PICNIC_SITE 25, TAXI_STAND 25, SHRINE 22, HERITAGE_SQUARE 21, PLACE_TOWN 19, STUPA 19, PLACE_VILLAGE 17, PALACE 16, PLACE_FARM 13, PEAK 12, RUINS 12, CITY_GATE 10, PLACE_SQUARE 9, THEME_PARK 8, BRIDGE 8, WATERFALL 7, PROTECTED_AREA 6, PLACE_CITY 4, TEAHOUSE 4, CABLE_CAR_STATION 3, CAVE 3, HOT_SPRING 3, PASS 2, MOSQUE 2, ZIPLINE 1, ZOO 1, PLACE_LOCAL_LEVEL 1, RAFTING 1, SAFARI 1, AIRPORT 1, PLACE_PROVINCE 1, PARAGLIDING 1

Lines by kind: STREAM 423 ways/353.6 km, RIVER 146 ways/330.9 km, DITCH 88 ways/20.2 km, CANAL 23 ways/9.6 km, TAXIWAY 53 ways/9.0 km, RUNWAY 3 ways/3.3 km, CABLE_CAR 1 ways/2.3 km, ZIP_LINE 1 ways/1.7 km, CITY_WALL 1 ways/0.1 km

Areas by kind: FOREST 1765/481.46 km², PROTECTED 4/103.4 km², FARMLAND 2909/99.32 km², RESIDENTIAL 2009/32.01 km², SCRUB 503/16.41 km², MEADOW 600/2.76 km², WATER_RIVER 48/2.75 km², AERODROME 1/2.65 km², INDUSTRIAL 230/2.55 km², PARK 3339/2.14 km², MILITARY 31/1.55 km², SAND_SHINGLE 93/1.16 km², GRASSLAND 244/0.83 km², PITCH 287/0.79 km², RELIGIOUS 330/0.38 km², COMMERCIAL 108/0.35 km², WATER_LAKE 146/0.3 km², WATER_POND 310/0.28 km², ORCHARD 43/0.22 km², WETLAND 25/0.13 km², PEDESTRIAN 20/0.04 km², BARE_ROCK 5/0.03 km², CEMETERY 12/0.02 km²

Roads by class: RESIDENTIAL 3097.9 km, UNCLASSIFIED 2315.8 km, TRACK 1194.2 km, TERTIARY 560.2 km, SECONDARY 453.1 km, SERVICE 448.9 km, PRIMARY 170.0 km, TRUNK 127.4 km, LIVING_STREET 25.9 km, ROAD 10.7 km | trails: PATH 1629.8 km, FOOTWAY 259.4 km, STEPS 23.9 km, CYCLEWAY 18.1 km, PEDESTRIAN 13.9 km, BRIDLEWAY 1.6 km

**kathmandu_core** (built 2026-10-05T00:53:46Z, bbox [85.283, 27.69, 85.375, 27.735]): tiles 263, roads 17568, road_km 1442.9, buildings 218622 (in tiles 167576), pois 22075, places 314, areas 1816, lines 128, search entries 1156.

Decoded leaf tiles (shared/sample-regions/kathmandu_core/kathmandu_core.ghpk decoded with tile_format.decode_tile): 15,186 unique POIs; by kind: SHOP 7070, RESTAURANT 1936, SCHOOL 1367, BANK 713, HOTEL 631, CAFE 602, TEMPLE_HINDU 435, HOSPITAL 363, GUEST_HOUSE 292, GOVERNMENT 272, HOSTEL 224, PARKING 186, PARK 172, GOMPA 120, PLACE_LOCALITY 102, PLACE_NEIGHBOURHOOD 95, PLACE_OF_WORSHIP 88, ATTRACTION 69, FUEL 64, BUS_STATION 60, MONUMENT 46, INFORMATION 33, MARKETPLACE 28, STONE_TAP 26, NOTABLE_TREE 22, VIEWPOINT 21, SHRINE 18, CHURCH 17, TAXI_STAND 17, MUSEUM 15, PLACE_SUBURB 12, STUPA 12, HELIPAD 8, CAMP_SITE 7, BRIDGE 6, LAKE 5, HERITAGE_SQUARE 5, THEME_PARK 3, PLACE_SQUARE 3, CITY_GATE 2, PROTECTED_AREA 2, SPRING 2, PLACE_HAMLET 2, PALACE 2, MOSQUE 2, WATERFALL 1, PLACE_CITY 1, PICNIC_SITE 1, RAFTING 1, SAFARI 1, RUINS 1, HOT_SPRING 1, AIRPORT 1, PLACE_TOWN 1

Lines by kind: RIVER 34 ways/46.7 km, TAXIWAY 53 ways/8.6 km, RUNWAY 3 ways/3.1 km, STREAM 7 ways/2.3 km, CANAL 1 ways/0.9 km, DITCH 3 ways/0.8 km

Areas by kind: AERODROME 1/2.42 km², FOREST 25/1.12 km², MEADOW 344/1.02 km², MILITARY 13/0.91 km², PARK 217/0.81 km², RESIDENTIAL 76/0.69 km², WATER_RIVER 15/0.57 km², PROTECTED 2/0.34 km², FARMLAND 85/0.29 km², PITCH 110/0.22 km², INDUSTRIAL 20/0.19 km², RELIGIOUS 91/0.17 km², COMMERCIAL 54/0.13 km², SCRUB 13/0.08 km², GRASSLAND 96/0.08 km², WETLAND 12/0.07 km², WATER_POND 35/0.05 km², PEDESTRIAN 12/0.03 km², WATER_LAKE 26/0.02 km², CEMETERY 4/0.01 km², SAND_SHINGLE 17/0.01 km², ORCHARD 12/0.01 km²

Roads by class: RESIDENTIAL 817.5 km, SERVICE 166.6 km, TERTIARY 83.1 km, SECONDARY 82.5 km, PRIMARY 39.6 km, TRUNK 34.3 km, UNCLASSIFIED 32.3 km, LIVING_STREET 8.1 km, TRACK 7.2 km, ROAD 0.1 km | trails: FOOTWAY 101.8 km, PATH 55.7 km, PEDESTRIAN 6.2 km, STEPS 4.5 km, CYCLEWAY 1.6 km

**thamel_test** (built 2026-10-04T22:53:08Z, bbox [85.3, 27.7, 85.32, 27.72]): tiles 67, roads 5624, road_km 630.566, buildings 87147 (in tiles 34693), pois 8213, places 107, areas 597, lines 28, search entries 587.
