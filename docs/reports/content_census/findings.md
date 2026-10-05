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
