# W2 detail pass: road structures in the Kathmandu Valley (data package)

> Generated from the OSM extract of the `kathmandu_valley` region (`pipeline/config/regions.yaml`, bbox 85.18–85.58 E, 27.55–27.83 N, the valley pack's leaf coverage) by `structures.analyse`, the same code that writes the `RSTR` tile chunk (docs/DATA_FORMATS.md 1.15). It lists **every bridge over water and every flyover, underpass and foot overbridge** the pipeline finds, for the bridges package (docs/W2_DETAIL_CONTRACT.md decisions 3 and 4). Evidence labels as in the other research files: everything here is **[O]** (measured in OSM), heights and clearances are pipeline results **[E]**, and the classification of untagged crossings needs a look on site **[V]**. Map data © OpenStreetMap contributors (ODbL).

## 1. Summary

| What | Ways | Notes |
|---|---:|---|
| Bridges over water, tagged `bridge` | 751 | river, stream, canal, ditch lines or water areas; 74 also span a road (riverside roads) |
| Bridges over water, inferred | 358 | untagged roads crossing a river, stream or canal line (or a pond): the pipeline builds a span there |
| Bridges over no water or road | 69 | tagged bridges over gullies, drains or unmapped streams |
| Flyovers / overpasses (vehicle decks over roads) | 7 | |
| Foot overbridges | 41 | footway, path, steps or pedestrian-street decks over roads |
| Underpasses (roads under a deck) | 153 | 78 with a lowered profile: a cutting under a ground road (tunnel-tagged ways and those chained to them) or under a river bridge (riverside roads) |
| Tunnels (no road above) | 5 | not drawn in W2, kept out of car routes |
| Fords | 33 | |
| Passages under buildings | 103 | `tunnel=building_passage`, `covered`, `indoor`, short tunnels: the house stays whole, a gateway with at least 4.5 m |
| Decks that land on a road | 7 | the road crosses within 2 m of a deck end: the two meet at one height, no underpass |
| Motor roads raised more than 3 m in URBAN / OLD_CORE | 25 | approach embankments decision 3 still needs (section 12) |
| Motor ramps steeper than their class grade | 46 | at most 15 %, connectors between a riverside cutting and a bridge approach (section 12) |
| Ways that only carry an approach ramp | 1161 | `APPROACH`: embankments, stairs or cuttings next to a structure |

Analysis counters: `{"abutment_joins": 7, "bridges_inferred": 388, "bridges_tagged_over_water": 753, "clearance_slack": 2, "crossings": 247, "crossings_at_abutment": 7, "crossings_short_of_clearance": 2, "embankment_over_cap_vertices": 182, "fords": 39, "lp_components": 896, "lp_vertices": 154587, "passages": 103, "pins_violated": 0, "riverside_crossings": 121, "stair_steps": 6, "stations": 1586232, "steep_ramp_edges": 159, "tunnels": 5, "water_not_snapped": 11, "water_snapped_to_bridge": 12, "ways_with_heights": 2534}`.

Columns: **OSM** way id; **class** the highway class; **length** of the way (m); **width** the tagged `width` (m), or in brackets the W2_DESIGN 4.1 real width the pipeline assumes; **layer** the effective layer (OSM `layer`, else +1 bridge, -1 tunnel); **lanes** the `lanes` tag; **at** the way's middle (lat, lon). Bridges and flyovers carry `railing_dm` 11 (vehicle) or 13 (foot) in `RSTR`; deck depth 1.2 m (foot 0.6 m) below the deck surface is reserved for the structure so every road below keeps 5.5 m.

## 2. How the data describes them (for the bridges and roads packages)

* `RSTR.kind` BRIDGE / FLYOVER / UNDERPASS / TUNNEL / FORD / PASSAGE per ROAD piece; flags `WATER_CROSSING`, `DECK_FROM_TAGS` (from OSM tags, else inferred), `FOOT_OVERBRIDGE`, `OVER_ROAD` (a road passes under: no piers on it), `LOWERED`, `APPROACH`, `CAR_ACCESSIBLE`.
* Per point: draped / deck / ramp and the absolute surface height. Deck points are the span (piers, railings, kerbs, abutments at the first and last deck point); ramp points are embankments (rising to a deck, retaining walls) or cuttings (lowered underpasses); foot structures climb ramps by stairs (50 %).
* Heights (one linear programme per neighbourhood, DATA_FORMATS 1.15): decks at least the water surface + 3 m over rivers (1.5 m streams, 2 m canals, 1 m ponds), and at least 5.5 m + the deck depth above every point of a road below within the deck's corridor (not only at the centreline); never below the line between the abutments. Rivers and streams in URBAN / OLD_CORE ground are incised (surface 4.5 m, streams 2.5 m, below the lowest DEM terrain along them: the sand-mined channels the 30 m DEM smooths away), so river bridges stay at street level and a riverside road under one sinks into a cutting (down to the water + 1 m) before the deck rises. Along the ground a road's offset from the terrain changes by at most the class grade per metre (5 % trunk and primary, 6 % secondary and tertiary, 8 % others, 50 % stairs), so nothing steps; junctions share one height.
* Long OSM bridge ways (Ring Road, Araniko Highway) are whole decks; their piers belong every 20–30 m outside the water and the roads below [E].

## 3. Bridges over water (tagged)

Sorted by the water crossed, then the road name.

| OSM | name | class | length | width | layer | lanes | water | structure | over road | at |
|---|---|---|---:|---:|---:|---:|---|---|---|---|
| w136443346 | Bagmati Bridge | unclassified | 52.3 | 7 | 3 | – | Bagmati River | – |  | 27.65893, 85.29341 |
| w271391183 | Bagmati Bridge | secondary | 101.4 | 7 | 1 | – | Bagmati River | – |  | 27.68475, 85.32706 |
| w342750604 | Nangra Bridge | path | 96 | (1.2) | 1 | – | Bagmati River | – |  | 27.58720, 85.28291 |
| w112785201 | Sankhamul Pul / संखामुल पुल | residential | 120.5 | 2.5 | 1 | – | Bagmati River | – | yes | 27.68072, 85.33092 |
| w53097930 | – | path | 46.2 | (1.2) | 1 | – | Bagmati River | suspension |  | 27.63192, 85.29301 |
| w245248657 | – | footway | 66.9 | (1.5) | 1 | – | Bagmati River | – |  | 27.62260, 85.29266 |
| w330211288 | – | residential | 31.7 | (5) | 1 | – | Bagmati River | – |  | 27.67936, 85.33523 |
| w340487599 | – | footway | 20.5 | (1.5) | 1 | – | Bagmati River | – |  | 27.76169, 85.42273 |
| w340487600 | – | tertiary | 23.6 | (4.5) | 1 | – | Bagmati River | – |  | 27.77941, 85.42463 |
| w340720636 | – | unclassified | 22.2 | (3.5) | 1 | – | Bagmati River | – |  | 27.73822, 85.40932 |
| w340807367 | – | path | 18.3 | (1.2) | 1 | – | Bagmati River | – |  | 27.77157, 85.42590 |
| w341865065 | – | residential | 28.4 | (3.5) | 1 | – | Bagmati River | – |  | 27.73495, 85.40390 |
| w342044217 | – | unclassified | 75.2 | (3.5) | 1 | – | Bagmati River | – |  | 27.58220, 85.27701 |
| w342070227 | – | footway | 128.4 | (1.5) | 1 | – | Bagmati River | suspension | yes | 27.60612, 85.29336 |
| w342153282 | – | footway | 67.8 | (1.5) | 1 | – | Bagmati River | suspension |  | 27.59620, 85.28605 |
| w343253048 | – | unclassified | 25.3 | (3.5) | 1 | – | Bagmati River | – |  | 27.73599, 85.39961 |
| w346308105 | – | unclassified | 30.2 | (3.5) | 1 | – | Bagmati River | – |  | 27.75531, 85.42169 |
| w882833703 | – | path | 18 | (1.2) | 1 | – | Bagmati River | – |  | 27.73972, 85.41195 |
| w917260740 | – | residential | 18.2 | (3.5) | 1 | – | Bagmati River | – |  | 27.73692, 85.39612 |
| w917297458 | – | path | 17.4 | (1.2) | 1 | – | Bagmati River | – |  | 27.73600, 85.40685 |
| w1056551607 | – | path | 131.1 | (1.2) | 1 | – | Bagmati River | suspension |  | 27.64396, 85.28632 |
| w1179679792 | – | unclassified | 101 | (3.5) | 1 | – | Bagmati River | – |  | 27.60510, 85.29480 |
| w1217992127 | – | path | 7.9 | (1.2) | 1 | – | Bagmati River | – |  | 27.77111, 85.42735 |
| w1290889300 | – | path | 29 | (1.2) | 1 | – | Bagmati River | – |  | 27.74863, 85.42254 |
| w1384512290 | – | residential | 31.5 | (3.5) | 1 | 1 | Bagmati River | – |  | 27.74468, 85.41646 |
| w1416263085 | – | tertiary | 77.8 | (6) | 1 | – | Bagmati River | – |  | 27.67913, 85.33254 |
| w1452211308 | – | path | 33.3 | (1.2) | 1 | – | Bagmati River | – |  | 27.77899, 85.42470 |
| w1452211309 | – | path | 18.6 | (1.2) | 1 | – | Bagmati River | – |  | 27.77493, 85.42526 |
| w1559324589 | – | footway | 33.5 | (1.5) | 1 | – | Bagmati River | – |  | 27.65887, 85.29380 |
| w670112952 | Kathmandu Ringroad / काठमाडाैँ चक्रपथ | trunk | 128.4 | 7 | 1 | 2 | Bagmati River, bagmati | – | yes | 27.68459, 85.30014 |
| w185470054 | Ring Road / काठमाडाैँ चक्रपथ | trunk | 128.8 | 7 | 1 | 2 | Bagmati River, bagmati | – | yes | 27.68468, 85.29884 |
| w179242639 | Sundarighat Suspension Bridge | path | 83.4 | 1 | 1 | – | Bagmati River, bagmati | suspension |  | 27.67426, 85.29363 |
| w670112954 | – | primary | 127.9 | 7 | 1 | 2 | Bagmati River, bagmati | – | yes | 27.68459, 85.29884 |
| w694819296 | – | residential | 127.3 | 8 | 1 | – | Bagmati River, bagmati | – |  | 27.67370, 85.29316 |
| w1091877749 | – | primary | 128.6 | 7 | 1 | 2 | Bagmati River, bagmati | – | yes | 27.68467, 85.30016 |
| w1540981400 | Bagmati / बागामती | secondary | 41.3 | 9 | 1 | – | Bagmati, Bagmati River | – | yes | 27.71220, 85.36009 |
| w52916461 | Bagmati Bridge / बागमती पुल | primary | 156.8 | 9 | 1 | 3 | Bagmati, Bagmati River | – | yes | 27.69005, 85.31727 |
| w136448419 | Bagmati Bridge / बागमती पुल | primary | 162.7 | 6 | 1 | 2 | Bagmati, Bagmati River | – | yes | 27.68994, 85.31737 |
| w683098904 | Ello Bridge / एल्लो पुल | residential | 30.9 | (5) | 1 | – | Bagmati, Bagmati River | – |  | 27.68283, 85.33903 |
| w196281728 | Guheswori Bridge | footway | 26.1 | (1.5) | 1 | – | Bagmati, Bagmati River | – |  | 27.71170, 85.35362 |
| w52782410 | Kalo pool / कालो पुल | secondary | 138.9 | 5 | 1 | – | Bagmati, Bagmati River | – | yes | 27.69322, 85.30428 |
| w27034031 | Madan Bhandari Path / मदन भण्डारी मार्ग | trunk | 108.7 | 6 | 2 | 2 | Bagmati, Bagmati River | – | yes | 27.68650, 85.34327 |
| w225456438 | Madan Bhandari Path / मदन भण्डारी पथ | trunk | 108.7 | 6 | 2 | 2 | Bagmati, Bagmati River | – | yes | 27.68632, 85.34435 |
| w1032574601 | Nilo Pul | footway | 39.2 | (2) | 1 | – | Bagmati, Bagmati River | – |  | 27.70263, 85.34963 |
| w190532597 | Ring Road / काठमाडाैँ चक्रपथ | trunk | 51.6 | 14 | 1 | 4 | Bagmati, Bagmati River | – |  | 27.70609, 85.34939 |
| w195246449 | Sairam Marg | residential | 31.4 | (5) | 1 | 1 | Bagmati, Bagmati River | – |  | 27.68851, 85.34843 |
| w204259216 | shiva chwok pul | footway | 70.6 | 1.5 | 1 | – | Bagmati, Bagmati River | – | yes | 27.71774, 85.38162 |
| w646793006 | Sinamangal Bridge / सिनामंगल पुल | secondary | 70.4 | 7 | 1 | – | Bagmati, Bagmati River | – | yes | 27.69899, 85.34721 |
| w1353164359 | Sinamangal Bridge / सिनामंगल पुल | secondary | 80.8 | 7 | 1 | – | Bagmati, Bagmati River | – | yes | 27.69911, 85.34637 |
| w27034168 | – | primary | 78.6 | (7) | 1 | – | Bagmati, Bagmati River | – | yes | 27.72175, 85.38197 |
| w32134335 | – | tertiary | 54 | (4.5) | 1 | – | Bagmati, Bagmati River | – |  | 27.73904, 85.38742 |
| w32134337 | – | tertiary | 24.9 | (4.5) | 1 | – | Bagmati, Bagmati River | – |  | 27.73810, 85.39176 |
| w37707651 | – | footway | 23.5 | (2) | 1 | – | Bagmati, Bagmati River | arch |  | 27.70989, 85.34865 |
| w112662003 | – | path | 25.6 | (1.5) | 1 | – | Bagmati, Bagmati River | – |  | 27.73783, 85.38683 |
| w112664331 | – | footway | 20.5 | (2) | 1 | – | Bagmati, Bagmati River | arch |  | 27.71008, 85.34879 |
| w112664384 | – | footway | 31.2 | (2) | 1 | – | Bagmati, Bagmati River | – |  | 27.70749, 85.34845 |
| w112664385 | – | residential | 18.3 | (5) | 1 | – | Bagmati, Bagmati River | – |  | 27.71309, 85.34971 |
| w116921737 | – | track | 43.6 | (3) | 1 | – | Bagmati, Bagmati River | – |  | 27.69295, 85.35157 |
| w136617990 | – | unclassified | 89.6 | (5) | 1 | – | Bagmati, Bagmati River | – | yes | 27.69181, 85.31032 |
| w185408067 | – | tertiary | 54.4 | 4 | 1 | – | Bagmati, Bagmati River | – | yes | 27.71213, 85.37262 |
| w217177684 | – | residential | 37.1 | 2 | 1 | – | Bagmati, Bagmati River | – |  | 27.71620, 85.37920 |
| w225466624 | – | footway | 124.2 | (2) | 1 | – | Bagmati, Bagmati River | suspension | yes | 27.69318, 85.30447 |
| w234510793 | – | unclassified | 36.1 | (4) | 1 | – | Bagmati, Bagmati River | – |  | 27.73103, 85.38484 |
| w300621772 | – | footway | 26.1 | (2) | 1 | – | Bagmati, Bagmati River | – |  | 27.68970, 85.34809 |
| w327548310 | – | footway | 20.4 | (2) | 1 | – | Bagmati, Bagmati River | – |  | 27.70110, 85.34916 |
| w340554249 | – | service | 35.8 | (4.5) | 1 | – | Bagmati, Bagmati River | – |  | 27.73955, 85.38862 |
| w377741779 | – | footway | 43.2 | (2) | 1 | – | Bagmati, Bagmati River | – |  | 27.71633, 85.37513 |
| w541556060 | – | residential | 51.2 | (5) | 1 | – | Bagmati, Bagmati River | – |  | 27.69255, 85.35100 |
| w762800882 | – | path | 26.4 | (1.5) | 1 | – | Bagmati, Bagmati River | – |  | 27.71187, 85.35722 |
| w1005237325 | – | residential | 44.4 | (5) | 1 | – | Bagmati, Bagmati River | – | yes | 27.71828, 85.38137 |
| w1091006164 | – | footway | 109.6 | (2) | 1 | – | Bagmati, Bagmati River | – | yes | 27.68621, 85.34433 |
| w1122233366 | – | tertiary | 52.4 | (7) | 1 | – | Bagmati, Bagmati River | – | yes | 27.69116, 85.30245 |
| w1155181710 | – | path | 26 | (1.5) | 1 | – | Bagmati, Bagmati River | – |  | 27.71071, 85.36811 |
| w1166034394 | – | footway | 106.9 | (2) | 1 | – | Bagmati, Bagmati River | – | yes | 27.68645, 85.34439 |
| w1364760293 | – | residential | 49 | 2 | 1 | 1 | Bagmati, Bagmati River | – | yes | 27.71624, 85.37513 |
| w1536225655 | – | path | 43.7 | (1.5) | 1 | – | Bagmati, Bagmati River | – |  | 27.71165, 85.36504 |
| w185470026 | Gaurishankar Sadak | residential | 13 | (5) | 1 | – | Balkhu River | – |  | 27.68879, 85.29111 |
| w777629766 | Kathmandu Ringroad / काठमाडाैँ चक्रपथ | trunk | 53 | 7 | 1 | 2 | Balkhu River | – |  | 27.68484, 85.29805 |
| w1167867843 | Madan Nagar Marg | residential | 2.2 | (5) | 1 | – | Balkhu River | – |  | 27.68763, 85.29276 |
| w184874643 | Madan Nagar PUL | residential | 27.4 | 5 | 1 | – | Balkhu River | – |  | 27.68659, 85.29297 |
| w270788536 | Malpot Bridge | residential | 15.6 | (5) | 1 | – | Balkhu River | – |  | 27.69059, 85.28186 |
| w195858822 | Ring Road / काठमाडाैँ चक्रपथ | trunk | 53 | 7 | 1 | 2 | Balkhu River | – |  | 27.68484, 85.29750 |
| w225457470 | Ring Road / काठमाडाैँ चक्रपथ | trunk | 69.7 | 7 | 1 | 2 | Balkhu River | – |  | 27.69007, 85.28355 |
| w1090820454 | Ring Road / काठमाडाैँ चक्रपथ | trunk | 70.2 | 7 | 1 | 2 | Balkhu River | – |  | 27.68956, 85.28398 |
| w189630256 | Sahid Basu Smriti Marga | residential | 10.5 | (5) | 1 | – | Balkhu River | – |  | 27.69169, 85.27718 |
| w184874113 | Sunar Gau Marg | residential | 44 | 7 | 1 | – | Balkhu River | – |  | 27.68954, 85.28725 |
| w184871306 | – | residential | 14.8 | (5) | 1 | – | Balkhu River | – |  | 27.69058, 85.27818 |
| w189630262 | – | residential | 10.3 | (5) | 1 | – | Balkhu River | – |  | 27.68777, 85.27438 |
| w206711597 | – | residential | 13.6 | (5) | 1 | – | Balkhu River | – |  | 27.69054, 85.27594 |
| w270788746 | – | residential | 15.8 | (5) | 1 | – | Balkhu River | – |  | 27.68568, 85.27258 |
| w343036468 | – | unclassified | 28.2 | (5) | 1 | – | Balkhu River | – |  | 27.68272, 85.27060 |
| w343036470 | – | unclassified | 22 | (3.5) | 1 | – | Balkhu River | – |  | 27.68087, 85.26748 |
| w347198529 | – | path | 17.9 | 3 | 1 | – | Balkhu River | – |  | 27.68331, 85.29836 |
| w347198535 | – | service | 19.3 | (4.5) | 1 | 1 | Balkhu River | – |  | 27.68434, 85.29783 |
| w605977332 | – | path | 86.8 | (1.5) | 1 | – | Balkhu River | – |  | 27.68409, 85.27055 |
| w777629764 | – | primary | 54.1 | 7 | 1 | 2 | Balkhu River | – |  | 27.68476, 85.29749 |
| w777629768 | – | primary | 70.6 | 7 | 1 | 2 | Balkhu River | – |  | 27.69003, 85.28348 |
| w777629769 | – | primary | 70.4 | 7 | 1 | 2 | Balkhu River | – |  | 27.68961, 85.28405 |
| w1133068189 | – | residential | 15.6 | (5) | 1 | – | Balkhu River | beam |  | 27.68880, 85.28821 |
| w1133073172 | – | primary | 52.8 | 7 | 1 | 2 | Balkhu River | – |  | 27.68492, 85.29805 |
| w1169193502 | – | residential | 27.7 | (5) | 1 | – | Balkhu River | – |  | 27.68564, 85.27353 |
| w230309607 | Banepa Panauti Khopasi Road | primary | 17.3 | (7) | 1 | – | Bansdol River | – |  | 27.58892, 85.51452 |
| w839559521 | – | unclassified | 9.9 | (3.5) | 1 | – | Bansdol River | – |  | 27.59139, 85.51383 |
| w921130894 | – | path | 5.3 | (1.2) | 1 | – | Bansdol River | – |  | 27.59292, 85.51291 |
| w1192743664 | Sidhicharan Sadak / सिद्धिचरण सादक | secondary | 9 | (7) | 1 | – | Bhachaa River | – |  | 27.71944, 85.29483 |
| w444134034 | – | residential | 26.1 | (5) | 1 | – | Bhachaa River | – |  | 27.71824, 85.28934 |
| w1175962341 | – | service | 4.9 | (4.5) | 1 | – | Bhachaa River | – |  | 27.71800, 85.28778 |
| w1487089848 | Kanti Lokhpath | primary | 5.9 | (6.5) | 1 | – | Bhaise Khola | – |  | 27.55925, 85.30124 |
| w938056237 | Basantanagr Marga | residential | 14.7 | (5) | 1 | – | Bishnumati | – |  | 27.73944, 85.30959 |
| w24691221 | Bijeshwori Bridge / बिजेश्वरी पुल | secondary | 82 | 7 | 1 | – | Bishnumati | – | yes | 27.71363, 85.30158 |
| w248967188 | Bishnumati Bridge / बिष्णुमती पुल | secondary | 83.4 | 7 | 1 | – | Bishnumati | – | yes | 27.70159, 85.30195 |
| w1123582689 | Bishnumati Bridge / बिष्णुमती पुल | secondary | 88.4 | (7) | 1 | – | Bishnumati | – | yes | 27.70198, 85.30270 |
| w651121137 | Dallu Arch Bridge | secondary | 40.1 | 7 | 1 | – | Bishnumati | arch |  | 27.70886, 85.30252 |
| w27033738 | Dallu bridge | secondary | 64.2 | 6 | 1 | – | Bishnumati | – | yes | 27.70942, 85.30306 |
| w24691223 | Ganeshman Singh Path / गणेशमान सिंह पथ | trunk | 72.5 | 9 | 1 | 4 | Bishnumati | – | yes | 27.69802, 85.30255 |
| w184870766 | Kankeswari Bridge | footway | 64.1 | 1.5 | 2 | – | Bishnumati | suspension | yes | 27.70683, 85.30259 |
| w196261764 | Nepaltar Bridge / नेपालटार पुल | tertiary | 34.4 | 5 | 1 | – | Bishnumati | – | yes | 27.74099, 85.30329 |
| w1528723556 | Ring Road / काठमाडाैँ चक्रपथ | trunk | 66.5 | 16 | 1 | 4 | Bishnumati | – | yes | 27.73522, 85.30758 |
| w184708634 | Ropeway Sadak | secondary | 36.2 | 7 | 1 | – | Bishnumati | – |  | 27.71979, 85.29931 |
| w1278116118 | Seshmati Bridge | residential | 14 | (5) | 1 | – | Bishnumati | – |  | 27.74403, 85.30009 |
| w53016680 | Teku Dovaan marg | residential | 59.7 | 5 | 1 | – | Bishnumati | – |  | 27.69213, 85.30090 |
| w119313488 | – | path | 41.7 | 2 | 1 | – | Bishnumati | – |  | 27.71488, 85.30151 |
| w302368029 | – | footway | 25.3 | 2 | 1 | – | Bishnumati | – |  | 27.70391, 85.30203 |
| w656909413 | – | footway | 30.4 | (2) | 1 | – | Bishnumati | – |  | 27.70062, 85.30252 |
| w800850655 | – | path | 29.5 | 1.5 | 1 | – | Bishnumati | – |  | 27.71137, 85.30251 |
| w937060390 | – | residential | 14.3 | (5) | 1 | – | Bishnumati | – |  | 27.74069, 85.30720 |
| w1258229981 | – | path | 30.9 | (1.5) | 1 | – | Bishnumati | – |  | 27.73709, 85.30807 |
| w1278116119 | – | residential | 13.2 | (5) | 1 | – | Bishnumati | – |  | 27.74390, 85.30010 |
| w1373633501 | – | footway | 34.2 | (2) | 1 | – | Bishnumati | – |  | 27.73343, 85.30659 |
| w1518635760 | – | primary | 53.1 | (7) | 1 | – | Bishnumati | – |  | 27.72548, 85.30539 |
| w199009951 | Baniyatar Bridge / बानियाँटार पुल | residential | 16.3 | 10 | 1 | 2 | Bishnumati River | – |  | 27.74482, 85.31652 |
| w366002504 | Ghale Bridge / घले पुल | residential | 24.1 | 2 | 1 | 2 | Bishnumati River | – |  | 27.74044, 85.31227 |
| w196261769 | Manamaiju Nayapul / मनमाइजु नायापुल | residential | 18.5 | 4 | 1 | – | Bishnumati River | – |  | 27.74240, 85.31426 |
| w209816119 | Tokha Road / टोखा सडक | secondary | 8.7 | (7) | 1 | – | Bishnumati River | – |  | 27.75610, 85.32738 |
| w196261767 | – | tertiary | 23.6 | (4.5) | 1 | – | Bishnumati River | – |  | 27.76430, 85.33812 |
| w197349142 | – | unclassified | 10.9 | (5) | 1 | – | Bishnumati River | – |  | 27.74673, 85.32169 |
| w199009949 | – | residential | 13.2 | (5) | 1 | – | Bishnumati River | – |  | 27.74507, 85.31988 |
| w201713609 | – | residential | 10.5 | (5) | 1 | – | Bishnumati River | – |  | 27.74388, 85.31520 |
| w341818770 | – | residential | 19.1 | (5) | 1 | – | Bishnumati River | – |  | 27.75127, 85.32294 |
| w341818773 | – | footway | 10.4 | (2) | 1 | – | Bishnumati River | – |  | 27.74809, 85.32231 |
| w341818775 | – | residential | 9.7 | (5) | 1 | – | Bishnumati River | – |  | 27.74903, 85.32294 |
| w341818780 | – | unclassified | 11.2 | (5) | 1 | – | Bishnumati River | – |  | 27.75333, 85.32572 |
| w341914979 | – | unclassified | 13.1 | (3.5) | 1 | – | Bishnumati River | – |  | 27.77239, 85.34457 |
| w343226570 | – | residential | 12.6 | (5) | 1 | – | Bishnumati River | – |  | 27.74504, 85.31795 |
| w570344077 | – | residential | 21 | (3.5) | 1 | – | Bishnumati River | – |  | 27.75909, 85.33418 |
| w747469001 | – | residential | 22.4 | 6 | 1 | – | Bishnumati River | – |  | 27.74466, 85.31538 |
| w938056236 | – | residential | 14.4 | (5) | 1 | – | Bishnumati River | – |  | 27.74447, 85.32074 |
| w1003844155 | – | residential | 10.7 | (5) | 1 | – | Bishnumati River | – |  | 27.73893, 85.31009 |
| w1080188669 | – | residential | 15.8 | (5) | 1 | – | Bishnumati River | – |  | 27.75684, 85.32817 |
| w1090210851 | – | residential | 14.6 | (5) | 1 | – | Bishnumati River | – |  | 27.75698, 85.33243 |
| w1180164806 | – | residential | 8.1 | (3.5) | 1 | – | Bishnumati River | – |  | 27.76877, 85.34131 |
| w1184919316 | – | unclassified | 16 | (3.5) | 1 | – | Bishnumati River | – |  | 27.77183, 85.34445 |
| w1374421724 | – | residential | 17.2 | (5) | 1 | – | Bishnumati River | – |  | 27.74448, 85.32213 |
| w1141852177 | – | track | 75 | (3) | 1 | – | Chauthe Khola | – |  | 27.78779, 85.17986 |
| w591851545 | Panga Road | residential | 6 | (5) | 1 | 1 | Chikhu | – |  | 27.67291, 85.27971 |
| w200704589 | – | residential | 15.6 | (5) | 1 | – | Chikhu | – |  | 27.67297, 85.28118 |
| w333418938 | – | residential | 11.5 | (5) | 1 | – | Chikhu | – |  | 27.67293, 85.28018 |
| w923798861 | Miteri Marga | residential | 13.7 | (3.5) | 1 | – | daudali khola | – |  | 27.70212, 85.25684 |
| w923798419 | – | residential | 5.3 | (3.5) | 1 | – | daudali khola | – |  | 27.70076, 85.26197 |
| w926065672 | – | path | 3.2 | (1.5) | 1 | – | daudali khola | – |  | 27.69981, 85.26309 |
| w1192602228 | – | residential | 9.9 | (5) | 1 | – | daudali khola | – |  | 27.69698, 85.26671 |
| w883874074 | – | path | 24.9 | (1.5) | 1 | – | Dhobi Khola | – |  | 27.69237, 85.32874 |
| w1090806815 | – | footway | 53.5 | (2) | 2 | – | Dhobi Khola | – | yes | 27.69052, 85.32854 |
| w1072029207 | – | tertiary | 6.5 | (4.5) | 1 | 1 | Dhobu Khola | – |  | 27.79382, 85.24757 |
| w77635795 | – | secondary | 20.1 | (7) | 1 | – | Fulchoki Khola | – |  | 27.59926, 85.43281 |
| w342704199 | – | path | 5.9 | (1.2) | 1 | – | Fyang Khola | – |  | 27.54279, 85.56044 |
| w959998970 | – | residential | 26 | (3.5) | 1 | – | Ganesh Khola | – |  | 27.79540, 85.32643 |
| w1179951434 | – | secondary | 6.1 | (5.5) | 1 | – | Ganesh Khola | – |  | 27.80057, 85.33001 |
| w1180151518 | – | tertiary | 9.1 | (6) | 1 | – | Ganesh Khola | – |  | 27.79868, 85.32813 |
| w32134353 | – | secondary | 47.1 | (7) | 1 | – | ghatte Khola | – |  | 27.74030, 85.38997 |
| w32134597 | – | unclassified | 21.3 | (3.5) | 1 | – | ghatte Khola | – |  | 27.74243, 85.39252 |
| w337636740 | – | tertiary | 25.2 | (4.5) | 1 | – | ghatte Khola | – |  | 27.75713, 85.39312 |
| w341906414 | – | unclassified | 8.9 | (3.5) | 1 | – | Ghatte Khola | – |  | 27.66394, 85.39298 |
| w341906415 | – | unclassified | 8.6 | (3.5) | 1 | – | Ghatte Khola | – |  | 27.66506, 85.39250 |
| w341906420 | – | unclassified | 7.6 | (3.5) | 1 | – | Ghatte Khola | – |  | 27.66682, 85.39239 |
| w343458551 | – | unclassified | 14.3 | 4 | 1 | – | Ghatte Khola | – |  | 27.66030, 85.39638 |
| w343458582 | – | unclassified | 11.8 | (3.5) | 1 | – | Ghatte Khola | – |  | 27.65806, 85.39743 |
| w1361163582 | – | track | 9.3 | (3) | 1 | – | Ghatte Khola | – |  | 27.66198, 85.39382 |
| w178915837 | Balkumari-Balkot Road | secondary | 51.8 | 5 | 1 | – | Godawari Khola | – | yes | 27.66425, 85.36326 |
| w199767113 | Godawari - Bisankhunarayan Road | tertiary | 19.4 | (6) | 1 | – | Godawari Khola | – |  | 27.59998, 85.37635 |
| w345060519 | Godawari - Bisankhunarayan Road | tertiary | 26.8 | (4.5) | 1 | – | Godawari Khola | – |  | 27.62399, 85.35349 |
| w196261835 | Gwarko-Lamatar / ग्वार्को-लामाटार सडक | secondary | 23.8 | (7) | 1 | – | Godawari Khola | – |  | 27.64713, 85.36510 |
| w499272142 | nahar bridge | path | 12.9 | (1.5) | 1 | – | Godawari Khola | – |  | 27.59980, 85.37619 |
| w207910634 | – | tertiary | 61.6 | 6 | 1 | – | Godawari Khola | – |  | 27.64225, 85.36208 |
| w207911066 | – | unclassified | 23 | (3.5) | 1 | – | Godawari Khola | – |  | 27.63391, 85.35929 |
| w343937886 | – | residential | 22.6 | (5) | 1 | – | Godawari Khola | – |  | 27.65930, 85.36160 |
| w344664511 | – | unclassified | 24.1 | (3.5) | 1 | – | Godawari Khola | – |  | 27.63226, 85.35322 |
| w344664512 | – | unclassified | 13 | (3.5) | 1 | – | Godawari Khola | – |  | 27.63306, 85.35664 |
| w360780743 | – | unclassified | 16 | (5) | 1 | – | Godawari Khola | – |  | 27.60144, 85.37237 |
| w360780744 | – | unclassified | 15.2 | (3.5) | 1 | – | Godawari Khola | – |  | 27.63580, 85.36074 |
| w1105454970 | – | residential | 11.9 | (5) | 1 | – | Godawari Khola | – |  | 27.64972, 85.36604 |
| w1141938402 | – | unclassified | 17.2 | (5) | 1 | – | Godawari Khola | – |  | 27.66668, 85.36233 |
| w1156855286 | – | residential | 41.7 | (3.5) | 1 | – | Godawari Khola | – |  | 27.65529, 85.36327 |
| w1192552974 | – | road | 10.9 | (5) | 1 | – | Godawari Khola | – |  | 27.66452, 85.36333 |
| w1536437335 | – | unclassified | 7 | (3.5) | 1 | – | Godawari Khola | – |  | 27.62936, 85.35288 |
| w230501487 | – | residential | 20.7 | (3.5) | 1 | – | gomati | – |  | 27.63029, 85.39143 |
| w196261841 | – | residential | 27.4 | (5) | 1 | – | gomati, sringamati | – |  | 27.63094, 85.39019 |
| w188435249 | Araniko Highway | trunk | 54.2 | 7 | 1 | 4 | Hanumante | – | yes | 27.67417, 85.39961 |
| w194607858 | Araniko Highway | trunk | 54.3 | 10 | 1 | 4 | Hanumante | – | yes | 27.67412, 85.40018 |
| w155210263 | Thimi Gamcha Tarkhal Pataletar Road | tertiary | 35.1 | (7) | 1 | – | Hanumante | – |  | 27.67091, 85.38576 |
| w1359805624 | Thimi Gamcha Tarkhal Pataletar Road | tertiary | 49.2 | (6) | 1 | 2 | Hanumante | – | yes | 27.67069, 85.38796 |
| w126555536 | – | residential | 19.8 | (5) | 1 | – | Hanumante | – |  | 27.67148, 85.38422 |
| w193293717 | – | tertiary | 45 | 4 | 1 | 1 | Hanumante | – |  | 27.67140, 85.37732 |
| w341906416 | – | path | 35.1 | (1.2) | 1 | – | Hanumante | suspension | yes | 27.67124, 85.39418 |
| w341906419 | – | path | 16.4 | (1.5) | 1 | – | Hanumante | suspension |  | 27.67061, 85.38864 |
| w1356342624 | – | path | 28 | (1.5) | 1 | – | Hanumante | – |  | 27.67108, 85.37278 |
| w1363281955 | – | path | 21.7 | (1.5) | 1 | – | Hanumante | – |  | 27.67676, 85.40248 |
| w1462580356 | – | footway | 29.3 | (2) | 1 | – | Hanumante | – |  | 27.67304, 85.39803 |
| w1536588021 | – | service | 56.8 | (4.5) | 1 | – | Hanumante | – | yes | 27.67393, 85.40008 |
| w191185395 | Tikathali-Lokanthali Road / टिकथली-लोकन्थली सडक | secondary | 31.4 | 4 | 1 | – | Hanumante Nadi, Manohara | – |  | 27.66890, 85.35884 |
| w185402786 | – | tertiary | 23.7 | 6 | 1 | – | Hanumante Nadi, Manohara | – |  | 27.66813, 85.35315 |
| w1352345917 | – | secondary | 40.5 | (7) | 1 | – | Hanumante Nadi, Manohara | – |  | 27.66822, 85.35248 |
| w520778144 | Barahi Bridge / बाराही पुल | residential | 27.4 | (5) | 1 | – | Hanumante River | – |  | 27.66792, 85.42441 |
| w126549426 | Ram Mandir Bridge / राम मन्दिर पुल | secondary | 26.6 | (7) | 1 | – | Hanumante River | – |  | 27.66807, 85.42721 |
| w32133797 | – | primary | 19.2 | (7) | 1 | – | Hanumante River | – |  | 27.67314, 85.40924 |
| w185747820 | – | residential | 20.4 | (5) | 1 | – | Hanumante River | – |  | 27.67044, 85.41330 |
| w656637558 | – | residential | 24.9 | (5) | 1 | – | Hanumante River | – |  | 27.67472, 85.40790 |
| w1155808979 | – | residential | 23.5 | (5) | 1 | – | Hanumante River | – |  | 27.66863, 85.43035 |
| w1462602010 | – | footway | 21.8 | (2) | 1 | – | Hanumante River | – |  | 27.66783, 85.41987 |
| w889267604 | – | footway | 31.8 | (2) | 1 | – | Hanumante River, Manohara | – |  | 27.66937, 85.36274 |
| w192705729 | Kausaltar-Balkot / कौशलटार-बालकोट | secondary | 44.7 | 4.5 | 1 | – | Hanumante, Manohara | – |  | 27.67264, 85.36476 |
| w279514050 | – | track | 15.3 | (3) | 1 | – | Hydropower Canal | – |  | 27.57567, 85.52257 |
| w341547630 | – | residential | 6.6 | (3.5) | 1 | – | Hydropower Canal | – |  | 27.56746, 85.53152 |
| w344661735 | – | unclassified | 3.8 | (3.5) | 1 | – | Hydropower Canal | – |  | 27.57919, 85.51024 |
| w207937152 | Nil Sarsaswati Marga | residential | 7 | (5) | 1 | – | Icchumati | – |  | 27.72003, 85.32557 |
| w1534481393 | Shakuna Marg | residential | 5.3 | (5) | 1 | – | Icchumati | – |  | 27.72063, 85.32636 |
| w1052979235 | Shubarna Shamsher Road / सुबर्ण शमशेर सडक | secondary | 12.1 | (7) | 1 | 4 | Icchumati | – |  | 27.72139, 85.32796 |
| w1161671407 | Thirbam Sadak / थिरबम सडक | primary | 10.7 | (7) | 1 | 2 | Icchumati | – |  | 27.72192, 85.33134 |
| w192917232 | Tukucha Marga | residential | 19.6 | (5) | 1 | – | Icchumati | – |  | 27.72121, 85.32934 |
| w195249439 | Uttar Dhoka Road / उत्तर ढोका सडक | secondary | 17.2 | (7) | 1 | – | Icchumati | – |  | 27.71740, 85.32458 |
| w1157453694 | – | footway | 8.4 | (2) | 1 | – | Icchumati | – |  | 27.71953, 85.32549 |
| w1534481422 | – | footway | 3.9 | (2) | 1 | – | Icchumati | – |  | 27.72021, 85.32597 |
| w341561006 | Bhothang Road | tertiary | 110.7 | (4.5) | 1 | – | Indrawati river | – |  | 27.82848, 85.57700 |
| w340533813 | – | path | 88.2 | (1.2) | 1 | – | Indrawati river | simple-suspension |  | 27.83447, 85.57767 |
| w340543774 | – | footway | 163.7 | (1.5) | 1 | – | Indrawati River | – |  | 27.79129, 85.57612 |
| w1011615991 | – | path | 289.8 | (1.2) | 1 | – | Indrawati river | – | yes | 27.80987, 85.57777 |
| w1289565621 | – | secondary | 199.7 | (5.5) | 1 | – | Indrawati River | – |  | 27.79371, 85.57553 |
| w921799827 | Kalanti | unclassified | 10.9 | (3.5) | 1 | – | Kalanti | – |  | 27.56490, 85.48989 |
| w921799829 | Kalanti | unclassified | 14.6 | (3.5) | 1 | – | Kalanti | – |  | 27.56615, 85.49067 |
| w921799825 | – | unclassified | 15.7 | (3.5) | 1 | – | Kalanti | – |  | 27.56311, 85.48842 |
| w1268547260 | – | unclassified | 6.8 | (3.5) | 1 | – | Kalanti | – |  | 27.56701, 85.49058 |
| w341258231 | – | unclassified | 9 | (5) | 1 | – | Kalcha | – |  | 27.66771, 85.43100 |
| w554393458 | kolphukhola bridge | tertiary | 252.4 | (4.5) | 1 | – | Kalphu River | – |  | 27.78485, 85.24989 |
| w1125447508 | Kolpu Khola Bridge | unclassified | 32.5 | (3.5) | 1 | – | Kalphu River | – |  | 27.76951, 85.19079 |
| w340427225 | Trishuli Highway | tertiary | 30.7 | (4.5) | 1 | – | Kalphu River | – |  | 27.77993, 85.24824 |
| w153237858 | – | tertiary | 33.5 | (4.5) | 1 | – | Kalphu River | – |  | 27.77430, 85.23960 |
| w341594849 | – | path | 19.7 | (1.2) | 1 | – | Kalphu River | – |  | 27.77221, 85.20927 |
| w1087502138 | – | unclassified | 26.3 | (3.5) | 1 | – | Kalphu River | – |  | 27.77369, 85.23045 |
| w1095601894 | – | track | 11.5 | (3) | 1 | 1 | Kalphu River | – |  | 27.79268, 85.25340 |
| w1125234707 | – | path | 39.4 | (1.2) | 1 | – | Kalphu River | – |  | 27.77126, 85.18166 |
| w553570363 | – | path | 93.8 | (1.2) | 1 | – | Kalphu River, Magada Stream | suspension |  | 27.79091, 85.25369 |
| w193291241 | Balkumari-Balkot Road / बालकुमारी-बालकोट सडक | secondary | 29.9 | 6 | 1 | – | Karmanasha Khola | – |  | 27.67114, 85.34304 |
| w111859874 | Gwarko-Lamatar | secondary | 22 | 8 | 1 | – | Karmanasha Khola | – |  | 27.66584, 85.33375 |
| w303931579 | proposed UDAY bridge | unclassified | 23.5 | (5) | 1 | – | Karmanasha Khola | – |  | 27.66745, 85.33883 |
| w193291243 | – | unclassified | 11.5 | (4) | 1 | – | Karmanasha Khola | – |  | 27.66985, 85.33941 |
| w329762135 | – | service | 22.1 | (4.5) | 1 | – | Karmanasha Khola | – |  | 27.66420, 85.33465 |
| w912815560 | – | residential | 13.7 | (5) | 1 | – | Karmanasha Khola | – |  | 27.64947, 85.33671 |
| w961020397 | – | residential | 12.7 | (5) | 1 | – | Karmanasha Khola | – |  | 27.66690, 85.33701 |
| w1146435408 | – | residential | 12.3 | (5) | 1 | – | Karmanasha Khola | – |  | 27.66276, 85.33445 |
| w1180672448 | – | residential | 8.8 | (5) | 1 | – | Karmanasha Khola | – |  | 27.65561, 85.33555 |
| w1275177650 | – | secondary | 30.1 | (7) | 1 | – | Karmanasha Khola | – |  | 27.67180, 85.34364 |
| w343755666 | Changu Narayan Road | secondary | 13.5 | 5 | 1 | – | Kasan Khola | – |  | 27.68885, 85.43836 |
| w245748910 | Khasang Khusung Bridge | unclassified | 11.5 | (3.5) | 1 | – | Kasan Khola | – |  | 27.67917, 85.42593 |
| w186261909 | Mandev Marg | secondary | 21.9 | (5.5) | 1 | – | Kasan Khola | – |  | 27.67945, 85.42947 |
| w191725888 | – | unclassified | 13.2 | (4) | 1 | – | Kasan Khola | – |  | 27.67931, 85.43297 |
| w192862504 | – | unclassified | 19.1 | (3.5) | 1 | – | Kasan Khola | – |  | 27.69160, 85.44810 |
| w195599901 | – | unclassified | 10.7 | (3.5) | 1 | – | Kasan Khola | – |  | 27.67788, 85.41755 |
| w240113069 | – | unclassified | 9.2 | (3.5) | 1 | – | Kasan Khola | – |  | 27.67853, 85.42254 |
| w332666276 | – | unclassified | 12.7 | (3.5) | 1 | – | Kasan Khola | – |  | 27.69051, 85.44351 |
| w332666365 | – | service | 10.1 | (3.5) | 1 | – | Kasan Khola | – |  | 27.69305, 85.45127 |
| w340439447 | – | unclassified | 24 | (3.5) | 1 | – | Kasan Khola | – |  | 27.68290, 85.43581 |
| w343755668 | – | residential | 9.2 | (3.5) | 1 | – | Kasan Khola | – |  | 27.68607, 85.43710 |
| w1166123926 | – | residential | 8.8 | 4 | 1 | – | Kasan Khola | – |  | 27.67870, 85.41541 |
| w1192822425 | – | residential | 8.5 | (5) | 1 | – | Khahare Khola | – |  | 27.70166, 85.23085 |
| w1192907054 | – | unclassified | 11.5 | (5) | 1 | – | Khahare Khola | – |  | 27.70600, 85.23019 |
| w1192907056 | – | unclassified | 9.1 | (5) | 1 | – | Khahare Khola | – |  | 27.70658, 85.22951 |
| w1192953204 | – | residential | 6.8 | (5) | 1 | – | Khahare Khola | – |  | 27.70320, 85.23074 |
| w1214350557 | – | residential | 9.7 | (5) | 1 | – | Khola River | – |  | 27.64371, 85.33451 |
| w1416521728 | – | residential | 4.5 | (5) | 1 | – | Khola River | – |  | 27.64417, 85.32929 |
| w1416521730 | – | residential | 4.5 | (5) | 1 | – | Khola River | – |  | 27.64388, 85.33309 |
| w1416521732 | – | residential | 9.3 | (3.5) | 1 | – | Khola River | – |  | 27.64396, 85.33061 |
| w1416963270 | – | residential | 5 | (5) | 1 | – | Khola River | – |  | 27.64421, 85.32837 |
| w1416963273 | – | residential | 6 | (5) | 1 | – | Khola River | – |  | 27.64426, 85.32889 |
| w342309473 | kodku khola bridge | unclassified | 18.5 | (3.5) | 1 | – | Kodku Khola | – |  | 27.62037, 85.34478 |
| w671662519 | Mero City Road | residential | 27.8 | (5) | 1 | – | Kodku Khola | – |  | 27.64658, 85.33598 |
| w194900751 | Satdobato-Godawari Road | primary | 54.2 | (7) | 1 | – | Kodku Khola | – |  | 27.64794, 85.33574 |
| w341156768 | – | path | 5.3 | (1.2) | 1 | – | Kodku Khola | – |  | 27.62914, 85.33747 |
| w342056242 | – | track | 10.4 | (3) | 1 | – | Kodku Khola | – |  | 27.61466, 85.34990 |
| w342296638 | – | tertiary | 15.1 | (4.5) | 1 | – | Kodku Khola | – |  | 27.60686, 85.34939 |
| w342296640 | – | unclassified | 2.7 | (3.5) | 1 | – | Kodku Khola | – |  | 27.60244, 85.34074 |
| w930378837 | – | residential | 18.8 | (5) | 1 | – | Kodku Khola | – |  | 27.64380, 85.33452 |
| w32134618 | – | secondary | 12.6 | (5.5) | 1 | – | Kolmati Khola | – |  | 27.74132, 85.40187 |
| w1391521877 | – | residential | 7.4 | (5) | 1 | – | Kolmati Khola | – |  | 27.74042, 85.40215 |
| w1391521881 | – | residential | 5.8 | (3.5) | 1 | – | Kolmati Khola | – |  | 27.74068, 85.40195 |
| w344795810 | – | path | 87.9 | (1.2) | 1 | – | Kulekhani | – |  | 27.55618, 85.21102 |
| w345300940 | – | path | 10 | (1.2) | 1 | – | Kulekhani | – |  | 27.55290, 85.21973 |
| w905354107 | – | residential | 20.3 | (3.5) | 1 | – | Kulekhani | – |  | 27.57865, 85.18020 |
| w905354110 | – | path | 20.6 | (1.2) | 1 | – | Kulekhani | suspension |  | 27.57877, 85.18008 |
| w341325152 | tikabhairab bridge II | footway | 28.5 | (2) | 1 | – | Lele River | – |  | 27.57463, 85.31310 |
| w341259039 | – | unclassified | 13.4 | (3.5) | 1 | – | Lele River | – |  | 27.57140, 85.32019 |
| w341259040 | – | residential | 18 | (3.5) | 1 | – | Lele River | – |  | 27.57118, 85.32779 |
| w794638178 | – | unclassified | 15 | (3.5) | 1 | – | Lele River | – |  | 27.57374, 85.31816 |
| w1172360811 | – | residential | 10.8 | (5) | 1 | – | Lele River | – |  | 27.57492, 85.31342 |
| w230376680 | nayapool | unclassified | 23.5 | (3.5) | 1 | – | Lilawati River | – |  | 27.59341, 85.46880 |
| w230372502 | Way to Ashapuri | unclassified | 17 | (3.5) | 1 | – | Lilawati River | – |  | 27.59444, 85.46350 |
| w230363988 | – | unclassified | 16.1 | (3.5) | 1 | – | Lilawati River | – |  | 27.60083, 85.43357 |
| w230366178 | – | path | 15.3 | (1.2) | 1 | – | Lilawati River | – |  | 27.59611, 85.44658 |
| w230366186 | – | path | 20.9 | (1.2) | 1 | – | Lilawati River | simple-suspension |  | 27.59758, 85.45394 |
| w230366187 | – | unclassified | 19.8 | (3.5) | 1 | – | Lilawati River | – |  | 27.59999, 85.45641 |
| w230378398 | – | unclassified | 14.1 | (3.5) | 1 | – | Lilawati River | – |  | 27.59235, 85.47415 |
| w230380066 | – | unclassified | 12.6 | (3.5) | 1 | – | Lilawati River | – |  | 27.59157, 85.47794 |
| w230383823 | – | unclassified | 17 | (3.5) | 1 | – | Lilawati River | – |  | 27.58687, 85.48670 |
| w1218552556 | – | secondary | 20.8 | (5.5) | 1 | – | Lilawati River | – |  | 27.58942, 85.47990 |
| w331291558 | Bishnudol-Lubhu Road / बिष्णुदोल-लुभु रोड | tertiary | 14.9 | 4 | 1 | – | Lubhu Khola | – |  | 27.64000, 85.36638 |
| w350997346 | dadathok road | unclassified | 8.1 | (3.5) | 1 | – | Lubhu Khola | – |  | 27.63816, 85.37772 |
| w343020482 | – | unclassified | 7.5 | (3.5) | 1 | – | Lubhu Khola | – |  | 27.63723, 85.37561 |
| w343020483 | – | unclassified | 9.1 | (3.5) | 1 | – | Lubhu Khola | – |  | 27.63853, 85.38041 |
| w343020484 | – | unclassified | 6.2 | (3.5) | 1 | – | Lubhu Khola | – |  | 27.63815, 85.37208 |
| w343020485 | – | unclassified | 7.5 | (3.5) | 1 | – | Lubhu Khola | – |  | 27.63669, 85.37657 |
| w32134154 | Subedi Gaun Gagalphedi Road | tertiary | 16.8 | (6) | 1 | – | Madhav Khola | – |  | 27.73052, 85.42905 |
| w340444542 | – | unclassified | 9.3 | (3.5) | 1 | – | Madhav Khola | – |  | 27.74260, 85.43066 |
| w341566196 | – | unclassified | 11.5 | (3.5) | 1 | – | Madhav Khola | – |  | 27.74507, 85.43201 |
| w341567190 | – | unclassified | 15.2 | (3.5) | 1 | – | Madhav Khola | – |  | 27.73428, 85.42711 |
| w1395664826 | – | service | 6 | (4.5) | 1 | – | Madhav Khola | – |  | 27.73035, 85.42832 |
| w1395664833 | – | path | 6.8 | (1.2) | 1 | – | Madhav Khola | – |  | 27.73742, 85.42577 |
| w553570368 | – | path | 47.5 | (1.2) | 1 | – | Magada Stream | suspension |  | 27.79108, 85.25473 |
| w343319537 | – | unclassified | 18.1 | (3.5) | 1 | – | Maheshkhola | – |  | 27.73758, 85.20440 |
| w343323173 | – | unclassified | 12.6 | (3.5) | 1 | – | Maheshkhola | – |  | 27.73815, 85.20741 |
| w343323175 | – | path | 9.6 | (1.2) | 1 | – | Maheshkhola | – |  | 27.73850, 85.20908 |
| w343326715 | – | unclassified | 18.2 | (3.5) | 1 | – | Maheshkhola | – |  | 27.73169, 85.18182 |
| w184880265 | Dhawok Marg | residential | 17.6 | (5) | 1 | – | Manamati | – |  | 27.69664, 85.29115 |
| w199430773 | Gyanodaya Marg | residential | 26.5 | (5) | 1 | – | Manamati | – |  | 27.69931, 85.28415 |
| w1126833392 | Gyantirtha marg | residential | 26.9 | 9 | 1 | – | Manamati | beam |  | 27.69530, 85.30005 |
| w199430776 | Manamati Marg | residential | 15.7 | (5) | 1 | – | Manamati | – |  | 27.69813, 85.28602 |
| w579511124 | Matatirtha Marg | residential | 8.2 | (5) | 1 | – | Manamati | – |  | 27.69714, 85.28983 |
| w184872593 | Ring Road / काठमाडाैँ चक्रपथ | trunk | 28.2 | 14 | 1 | 4 | Manamati | – |  | 27.69964, 85.28160 |
| w184867809 | Soltimode Chowk / सोल्टिमोड चौक | trunk | 22.9 | 12 | 1 | – | Manamati | – |  | 27.69635, 85.29335 |
| w184867817 | University Path / विश्वविद्यालयको पथ | primary | 16.2 | 14 | 1 | – | Manamati | – |  | 27.69608, 85.29877 |
| w184867825 | – | residential | 18.9 | (5) | 1 | – | Manamati | – |  | 27.69585, 85.29944 |
| w184867831 | – | service | 9.3 | (4) | 1 | – | Manamati | – |  | 27.69661, 85.29717 |
| w343496457 | – | residential | 16.2 | (3.5) | 1 | – | Manamati | – |  | 27.70486, 85.27496 |
| w788476641 | – | unclassified | 11.8 | (5) | 1 | – | Manamati | – |  | 27.70734, 85.26988 |
| w1164305255 | – | residential | 11.7 | (5) | 1 | – | Manamati | – |  | 27.70025, 85.27962 |
| w1175551908 | – | unclassified | 16.2 | (5) | 1 | – | Manamati | – |  | 27.70608, 85.27031 |
| w185402774 | Araniko Highway | trunk | 90.3 | 9 | 1 | 4 | Manohara Nadi | – | yes | 27.67509, 85.35480 |
| w194607857 | Araniko Highway | trunk | 90.1 | 9 | 1 | 4 | Manohara Nadi | – | yes | 27.67515, 85.35572 |
| w32133465 | – | primary | 78.2 | 6 | 1 | – | Manohara Nadi | – |  | 27.70299, 85.39376 |
| w185402777 | – | secondary | 20 | 3 | 1 | – | Manohara Nadi | – |  | 27.67033, 85.35232 |
| w185476585 | – | cycleway | 29.6 | (2) | 1 | – | Manohara Nadi | – |  | 27.67311, 85.35477 |
| w194610100 | – | primary | 62.5 | 7 | 1 | 2 | Manohara Nadi | – | yes | 27.68631, 85.36442 |
| w879392395 | – | secondary | 30.5 | (7) | 1 | – | Manohara Nadi | – |  | 27.66954, 85.35249 |
| w1183881586 | – | footway | 37.5 | (2) | 1 | – | Manohara Nadi | – |  | 27.69046, 85.37180 |
| w1275177652 | – | service | 96.3 | (4.5) | 1 | – | Manohara Nadi | – | yes | 27.67526, 85.35578 |
| w1275178030 | – | residential | 89.1 | (5) | 1 | – | Manohara Nadi | – | yes | 27.67502, 85.35480 |
| w1373303135 | – | footway | 24.1 | (2) | 1 | – | Manohara Nadi | – |  | 27.67713, 85.35525 |
| w1381020677 | – | tertiary | 51.3 | (6) | 1 | – | Manohara Nadi | – |  | 27.69386, 85.38040 |
| w1384267956 | – | footway | 18.8 | (2) | 1 | – | Manohara Nadi | – |  | 27.68214, 85.36024 |
| w1384267959 | – | footway | 21.3 | (2) | 1 | – | Manohara Nadi | – |  | 27.68201, 85.35895 |
| w1462580816 | – | footway | 36.8 | (2) | 1 | – | Manohara Nadi | – |  | 27.68910, 85.36903 |
| w207801962 | – | tertiary | 68.1 | 6 | 1 | – | Manohara River | – |  | 27.71389, 85.41103 |
| w903017811 | – | tertiary | 54.4 | (4.5) | 1 | – | Manohara River | – |  | 27.72253, 85.42252 |
| w1126617119 | – | unclassified | 13 | (3.5) | 1 | – | Manohara River | – |  | 27.70683, 85.40254 |
| w670112945 | Kathmandu Ringroad / काठमाडाैँ चक्रपथ | trunk | 120.4 | 7 | 1 | 2 | Manohara, Manohara Nadi | – | yes | 27.67352, 85.34224 |
| w179094375 | Koteshwor Jhulungepul | footway | 68.4 | 1.5 | 1 | – | Manohara, Manohara Nadi | suspension |  | 27.67582, 85.33848 |
| w903302818 | Manohara Pul | secondary | 47 | (7) | 1 | – | Manohara, Manohara Nadi | – |  | 27.67764, 85.33574 |
| w345052247 | Ring Road / काठमाडाैँ चक्रपथ | trunk | 121.6 | 7 | 1 | 2 | Manohara, Manohara Nadi | – | yes | 27.67265, 85.34149 |
| w670112943 | – | primary | 121.1 | 7 | 1 | 2 | Manohara, Manohara Nadi | – | yes | 27.67259, 85.34155 |
| w1126606030 | – | footway | 120.2 | (2) | 1 | – | Manohara, Manohara Nadi | – | yes | 27.67258, 85.34159 |
| w1126627063 | – | footway | 121.1 | (2) | 1 | – | Manohara, Manohara Nadi | – | yes | 27.67277, 85.34133 |
| w1126627064 | – | primary | 119.5 | 7 | 1 | 2 | Manohara, Manohara Nadi | – | yes | 27.67356, 85.34217 |
| w1149817203 | – | residential | 35.2 | (5) | 1 | – | Manohara, Manohara Nadi | – |  | 27.66815, 85.34923 |
| w1355352016 | – | footway | 39.3 | (2) | 1 | – | Manohara, Manohara Nadi | – |  | 27.67140, 85.34438 |
| w340433283 | – | tertiary | 79.9 | (4.5) | 1 | – | Melamchi River | – |  | 27.83532, 85.56882 |
| w341812309 | – | path | 170.7 | (1.2) | 1 | – | Melamchi River | simple-suspension | yes | 27.83354, 85.57521 |
| w343320700 | – | path | 12.7 | (1.2) | 1 | – | Murali Khola | – |  | 27.60091, 85.43294 |
| w1536812205 | Dipankha Marg / दिपङ्ख मार्ग | residential | 19.4 | (5) | 1 | – | Nakhhu | – |  | 27.64184, 85.31195 |
| w283070917 | – | footway | 82.9 | (1.5) | 1 | – | Nakhhu | – | yes | 27.62601, 85.31141 |
| w285166681 | – | tertiary | 20.9 | (6) | 1 | – | Nakhhu | – |  | 27.63161, 85.30756 |
| w1262782335 | – | unclassified | 28.5 | (3.5) | 1 | – | Nakhhu | – |  | 27.62604, 85.31153 |
| w136441544 | Ekantakuna-Tikabhairab Road | secondary | 50.2 | (7) | 1 | – | Nakhu River | – |  | 27.66275, 85.30606 |
| w199430784 | Nakkhu Pul | residential | 32.9 | (5) | 1 | – | Nakhu River | – |  | 27.65082, 85.31231 |
| w1428968937 | – | residential | 18.7 | 5 | 1 | – | Nakhu River | – |  | 27.65747, 85.31068 |
| w283069932 | Champi Bridge | path | 118.8 | (1.2) | 1 | – | Nallu Khola | – | yes | 27.58533, 85.31176 |
| w340975854 | Kanti Highway / कान्ति लोकपथ | primary | 53.6 | (7) | 1 | – | Nallu Khola | – |  | 27.57552, 85.31245 |
| w283070196 | – | footway | 19 | (1.5) | 1 | – | Nallu Khola | – |  | 27.58985, 85.31269 |
| w343719391 | – | path | 45.2 | (1.2) | 1 | – | Nallu Khola | – | yes | 27.59425, 85.31491 |
| w343719393 | – | path | 28.9 | (1.2) | 1 | – | Nallu Khola | – | yes | 27.60120, 85.31794 |
| w343320598 | – | secondary | 9.4 | (7) | 1 | – | Neure Khola | – |  | 27.59900, 85.43241 |
| w609422361 | – | path | 5.8 | (1.5) | 1 | – | Neurey Khola | – |  | 27.79279, 85.32531 |
| w1180157353 | – | residential | 4 | (3.5) | 1 | – | Neurey Khola | – |  | 27.79322, 85.32761 |
| w1180157355 | – | residential | 5.4 | (5) | 1 | – | Neurey Khola | – |  | 27.79318, 85.32709 |
| w286616529 | Bhaktapur-Nala-Banepa road | primary | 13.1 | 6 | 1 | – | Punyamata River | – |  | 27.64758, 85.51212 |
| w230310383 | Dudhmil Bridge | unclassified | 14.3 | (5) | 1 | – | Punyamata River | – |  | 27.59070, 85.51695 |
| w128056126 | Maneshwori Pul | secondary | 16.1 | (7) | 1 | – | Punyamata River | – |  | 27.58680, 85.51377 |
| w43918120 | – | footway | 33.1 | (2) | 1 | – | Punyamata River | – |  | 27.58575, 85.51798 |
| w128032570 | – | footway | 21.9 | (2) | 1 | – | Punyamata River | – |  | 27.58580, 85.51835 |
| w224214662 | – | service | 14.6 | (4.5) | 1 | – | Punyamata River | – |  | 27.58617, 85.51366 |
| w230151736 | – | path | 14 | (1.2) | 1 | – | Punyamata River | – |  | 27.60906, 85.53273 |
| w230155228 | – | unclassified | 15.3 | (3.5) | 1 | – | Punyamata River | – |  | 27.59961, 85.52786 |
| w230307983 | – | footway | 19.7 | (2) | 1 | – | Punyamata River | – |  | 27.58604, 85.51553 |
| w237173508 | – | unclassified | 14.4 | (5) | 1 | – | Punyamata River | – |  | 27.65445, 85.50559 |
| w341261237 | – | unclassified | 10.7 | (3.5) | 1 | – | Punyamata River | – |  | 27.64705, 85.51146 |
| w344769564 | – | unclassified | 13.9 | (3.5) | 1 | – | Punyamata River | – |  | 27.65652, 85.52710 |
| w344858571 | – | unclassified | 38.6 | (5) | 1 | – | Punyamata River | suspension |  | 27.63984, 85.51558 |
| w344858572 | – | tertiary | 14.3 | (6) | 1 | – | Punyamata River | – |  | 27.64263, 85.51341 |
| w960114314 | – | unclassified | 28.4 | (5) | 1 | – | Punyamata River | – |  | 27.59150, 85.51725 |
| w1060646045 | – | path | 26.1 | (1.2) | 1 | – | Punyamata River | – |  | 27.66803, 85.50421 |
| w1060942725 | – | unclassified | 15.8 | (3.5) | 1 | – | Punyamata River | – |  | 27.64804, 85.51021 |
| w1061433887 | – | unclassified | 26.7 | (5) | 1 | – | Punyamata River | – |  | 27.64479, 85.51193 |
| w1063671980 | – | path | 8 | (1.2) | 1 | – | Punyamata River | – |  | 27.65278, 85.51304 |
| w341323891 | – | footway | 29.5 | (2) | 1 | – | Punyamata River, Roshi Khola | – |  | 27.58534, 85.51924 |
| w334574290 | Araniko Highway | trunk | 32.3 | 8 | 1 | – | Punyamati River | – |  | 27.63209, 85.51452 |
| w230296586 | Banepa Panauti Khopasi Road | primary | 36.7 | 6 | 1 | – | Punyamati River | – |  | 27.62484, 85.52331 |
| w1238996374 | Mukti M.(W) | residential | 14.8 | (5) | 1 | – | Punyamati River | – |  | 27.63033, 85.51694 |
| w230300135 | Punyamata road | tertiary | 24.6 | (6) | 1 | – | Punyamati River | – |  | 27.61453, 85.53203 |
| w266071713 | Sinagal M | residential | 27.6 | (5) | 1 | – | Punyamati River | – |  | 27.62821, 85.51837 |
| w266071715 | Tulti M. | residential | 27.2 | (5) | 1 | – | Punyamati River | – |  | 27.62722, 85.51961 |
| w343921016 | – | track | 17.9 | (3) | 1 | – | Punyamati River | – |  | 27.62598, 85.52150 |
| w1093892808 | – | track | 10.8 | (3) | 1 | – | Punyamati River | – |  | 27.62439, 85.52599 |
| w1238996380 | – | residential | 23.1 | (5) | 1 | – | Punyamati River | – |  | 27.63244, 85.51474 |
| w1304806250 | – | residential | 19.1 | (5) | 1 | – | Punyamati River | beam bridge |  | 27.63611, 85.51595 |
| w331606300 | 0 | residential | 14.3 | (3.5) | 1 | – | Roshi Khola | – |  | 27.57355, 85.49174 |
| w225225559 | Banepa Panauti Khopasi Road | secondary | 34.9 | (5.5) | 1 | – | Roshi Khola | – |  | 27.58347, 85.51531 |
| w331734955 | Jhulunge Pul | path | 79.2 | (1.2) | 1 | – | Roshi Khola | – |  | 27.56296, 85.53848 |
| w331433289 | Khakle Chaur Pul | path | 99.6 | (1.2) | 1 | – | Roshi Khola | – | yes | 27.57285, 85.53149 |
| w1031957878 | Rishi Khola RPC Bridge | service | 27.4 | (3.5) | 1 | – | Roshi Khola | – |  | 27.58501, 85.51982 |
| w331606264 | Roshi | unclassified | 31.4 | (3.5) | 1 | – | Roshi Khola | – |  | 27.56818, 85.47935 |
| w331606267 | Roshi | unclassified | 15.7 | (5) | 1 | – | Roshi Khola | – |  | 27.57175, 85.48720 |
| w331304031 | way to kalanti | unclassified | 25.4 | (3.5) | 1 | – | Roshi Khola | – |  | 27.57480, 85.49934 |
| w128025737 | – | footway | 26.8 | (2) | 1 | – | Roshi Khola | – |  | 27.58458, 85.51727 |
| w128027719 | – | path | 28.6 | (1.2) | 1 | – | Roshi Khola | – |  | 27.58361, 85.51611 |
| w230306336 | – | path | 30.1 | (1.2) | 1 | – | Roshi Khola | – |  | 27.58208, 85.52677 |
| w230307370 | – | footway | 26.2 | (2) | 1 | – | Roshi Khola | – |  | 27.58495, 85.51890 |
| w230307372 | – | footway | 17.9 | (1.5) | 1 | – | Roshi Khola | – |  | 27.58294, 85.51506 |
| w331433288 | – | unclassified | 30.8 | (3.5) | 1 | – | Roshi Khola | – |  | 27.56994, 85.53911 |
| w331433293 | – | path | 49.4 | (1.2) | 1 | – | Roshi Khola | – |  | 27.57945, 85.52864 |
| w331575527 | – | service | 16.4 | (3.5) | 1 | – | Roshi Khola | – |  | 27.58087, 85.51073 |
| w331606298 | – | unclassified | 34.1 | (5) | 1 | – | Roshi Khola | – |  | 27.57698, 85.50517 |
| w331734954 | – | unclassified | 10.4 | (3.5) | 1 | – | Roshi Khola | – |  | 27.55846, 85.54348 |
| w341876558 | – | footway | 46.7 | 1 | 1 | – | Roshi Khola | simple-suspension |  | 27.56283, 85.55801 |
| w343008213 | – | service | 9.3 | (3.5) | 1 | – | Roshi Khola | – |  | 27.56898, 85.48210 |
| w1000105641 | – | unclassified | 23.9 | (3.5) | 1 | – | Roshi Khola | – |  | 27.55136, 85.55881 |
| w1333670614 | – | track | 8.1 | (3) | 1 | – | Roshi Khola | – |  | 27.56771, 85.47665 |
| w197150362 | Jyotinagar Pool | unclassified | 21 | (5) | 1 | – | Rudramati Khola | – |  | 27.73937, 85.35335 |
| w188580085 | – | footway | 21.3 | (2) | 1 | – | Rudramati Khola | – |  | 27.73677, 85.35387 |
| w196261766 | – | unclassified | 15.1 | (5) | 1 | – | Rudramati Khola | – |  | 27.74713, 85.35640 |
| w206892476 | – | residential | 19.5 | (5) | 1 | – | Rudramati Khola | – |  | 27.73992, 85.35224 |
| w1107335156 | – | residential | 11.4 | (5) | 1 | – | Rudramati Khola | – |  | 27.74359, 85.35156 |
| w903292163 | – | residential | 19.5 | 7 | 1 | – | Rudramati river | – |  | 27.68693, 85.32459 |
| w1455712205 | – | path | 28.9 | (1.2) | 1 | – | salinadi river | – |  | 27.73547, 85.47035 |
| w1156587057 | Janamilan Marg | residential | 5.2 | (5) | 1 | 1 | Samakhushi | – |  | 27.72889, 85.31696 |
| w1395983973 | Parijat Sadak / पारिजात सादक | tertiary | 7.6 | 6 | 1 | – | Samakhushi | – |  | 27.72856, 85.30690 |
| w1353140418 | Rom Marg | residential | 4.4 | (5) | 1 | – | Samakhushi | – |  | 27.72871, 85.31635 |
| w1156587055 | Samakhusi Marg / सामाखुसी मार्ग | secondary | 12.6 | 8 | 1 | – | Samakhushi | – |  | 27.72800, 85.31456 |
| w1156587061 | Shital Niwas Marg | tertiary | 14.5 | (6) | 1 | – | Samakhushi | – |  | 27.73450, 85.32250 |
| w190380604 | Shree Kanti Marg / श्री कान्ति मार्ग | tertiary | 9.9 | 6 | 1 | – | Samakhushi | – |  | 27.73744, 85.32582 |
| w197741214 | – | residential | 8.4 | (5) | 1 | – | Samakhushi | – |  | 27.73822, 85.32782 |
| w1184921248 | – | residential | 6.2 | (5) | 1 | – | Samakhushi | – |  | 27.73679, 85.32539 |
| w1184923352 | – | residential | 5 | (5) | 1 | – | Samakhushi | – |  | 27.73564, 85.32437 |
| w1184923355 | – | residential | 5.7 | (5) | 1 | – | Samakhushi | – |  | 27.73613, 85.32460 |
| w1184923359 | – | residential | 4.3 | (5) | 1 | – | Samakhushi | – |  | 27.73544, 85.32404 |
| w1395983975 | – | residential | 5.8 | (5) | 1 | – | Samakhushi | – |  | 27.72879, 85.30833 |
| w1395983976 | – | residential | 6.5 | (5) | 1 | – | Samakhushi | – |  | 27.72896, 85.30943 |
| w1148799063 | Indrayani Bridge | residential | 11.2 | (5) | 1 | – | Sangle River | – |  | 27.75031, 85.31622 |
| w234215000 | – | unclassified | 14.3 | (3.5) | 1 | – | Sangle River | – |  | 27.77300, 85.31918 |
| w234215002 | – | unclassified | 14.7 | (5) | 1 | – | Sangle River | – |  | 27.78647, 85.32578 |
| w1011149624 | – | unclassified | 13.7 | (3.5) | 1 | – | Sangle River | – |  | 27.78201, 85.32201 |
| w1171084773 | – | path | 22.7 | (1.2) | 1 | – | Sangle River | – |  | 27.76446, 85.31777 |
| w1171084775 | – | unclassified | 15.8 | (3.5) | 1 | – | Sangle River | – |  | 27.76177, 85.31700 |
| w1219687972 | – | unclassified | 10.6 | 3 | 1 | – | Sangle River | – |  | 27.77352, 85.32040 |
| w1273623562 | – | residential | 10.9 | (5) | 1 | – | Sangle River | – |  | 27.75633, 85.31707 |
| w343515328 | Tribhuvan Rajpath / त्रिभुवन राजपथ | trunk | 31.3 | (9) | 1 | 2 | Sisne Khola | – |  | 27.70696, 85.20120 |
| w343519201 | – | path | 15.6 | (1.2) | 1 | – | Sisne Khola | – |  | 27.71882, 85.18600 |
| w1494972229 | – | track | 13.3 | (3) | 1 | – | Sisne Khola | – |  | 27.71976, 85.18370 |
| w1548907289 | – | trunk | 42.6 | (9) | 1 | – | Sisne Khola | – | yes | 27.70801, 85.19975 |
| w230499722 | – | unclassified | 10.7 | (3.5) | 1 | – | sringamati | – |  | 27.62711, 85.39208 |
| w1216197883 | Trishuli Highway | tertiary | 9.4 | (4.5) | 1 | – | Thulo Khola | – |  | 27.78723, 85.27277 |
| w1136888932 | – | unclassified | 7.5 | (3.5) | 1 | – | Thulo Khola | – |  | 27.77823, 85.25909 |
| w754991648 | – | residential | 15.4 | (5) | 1 | – | Tukucha Khola | – |  | 27.69138, 85.31548 |
| w193053264 | Araniko Highway | trunk | 35.6 | 8 | 1 | – | – | – |  | 27.66386, 85.44206 |
| w344595310 | Banepa Panauti Khopasi Road | primary | 15.1 | (6.5) | 1 | – | – | – |  | 27.60106, 85.52791 |
| w111845395 | Bhaktithapa Road | tertiary | 58.2 | 5 | 1 | – | – | – | yes | 27.69240, 85.32922 |
| w39985979 | Bhatkeko Pul / भत्केको पुल | tertiary | 55.2 | 5 | 1 | – | – | – | yes | 27.71805, 85.34001 |
| w903287083 | Bhrigu Marga | path | 18.1 | (1.5) | 1 | – | – | – |  | 27.68767, 85.32641 |
| w111846942 | Bijulibazar Bridge / बिजुलिबजार पुल | trunk | 56.4 | 6 | 2 | 2 | – | – | yes | 27.69046, 85.32807 |
| w169339097 | Bijulibazar bridge / बिजुलिबजार पुल | trunk | 55.7 | 6 | 2 | 2 | – | – | yes | 27.69028, 85.32860 |
| w126552558 | Brahamayni Bridge | residential | 16.1 | (5) | 1 | – | – | – |  | 27.67415, 85.44385 |
| w511400481 | Brahmayani bridge | unclassified | 15.9 | (5) | 1 | – | – | – |  | 27.67372, 85.44355 |
| w222843984 | Chunnepakha Galli | path | 9.1 | (1.5) | 1 | – | – | – |  | 27.69653, 85.29622 |
| w269491340 | Dhalane Dhobikhola pool (Bridge) / धलाने धोबिखोला (पुल) | residential | 14.1 | (5) | 1 | – | – | – |  | 27.72876, 85.34997 |
| w190892854 | Dhalane Pool (Bridge) | residential | 13.7 | (5) | 1 | – | – | – |  | 27.72940, 85.35015 |
| w751767742 | German Quater Galli | residential | 9.5 | (5) | 1 | – | – | – |  | 27.71540, 85.29664 |
| w126549374 | Hanumante | residential | 32.6 | (5) | 1 | – | – | – |  | 27.66896, 85.43174 |
| w343458581 | Harsha chowk Makalepati Kiwachowk Magar Gaun road | unclassified | 10.6 | (5) | 1 | – | – | – |  | 27.65628, 85.39755 |
| w344797303 | Helambu Trek | unclassified | 17.6 | (3.5) | 1 | – | – | – |  | 27.80629, 85.46388 |
| w1087122904 | Hindunagar Kalasha Marg / हिन्दुनगर कलश मार्ग | residential | 8.3 | (3.5) | 1 | – | – | – |  | 27.68362, 85.40150 |
| w196261768 | Jarankhu-pul | tertiary | 22.7 | (6) | 1 | – | – | – |  | 27.75773, 85.30180 |
| w191859859 | Kalopul | secondary | 89.8 | 7 | 1 | – | – | – | yes | 27.71189, 85.33709 |
| w332069882 | Khopasi Khyaku Balthali Road | unclassified | 30.5 | (3.5) | 1 | – | – | – |  | 27.56300, 85.53670 |
| w340839218 | Kisipide to Gurjudhara | tertiary | 20.7 | (6) | 1 | 2 | – | – |  | 27.69340, 85.24671 |
| w190895514 | Miteri Pool (Bridge) | footway | 10.2 | (2) | 1 | – | – | – |  | 27.73541, 85.35242 |
| w343744060 | Mulsaghu Bridge | secondary | 25.6 | 5 | 1 | – | – | – |  | 27.68224, 85.45806 |
| w1203853920 | Nagin Khola Pul | residential | 6.8 | (3.5) | 1 | – | – | – |  | 27.74986, 85.29171 |
| w416213727 | Nilo Pool / निलो पूल | tertiary | 14.6 | 6 | 1 | 1 | – | – |  | 27.73136, 85.34946 |
| w246173480 | Pasang Lhamu Highway | primary | 12.3 | (6.5) | 1 | – | – | – |  | 27.83204, 85.19135 |
| w271389950 | Rajesh Marg / राजेश मार्ग | residential | 16.8 | (5) | 1 | – | – | – |  | 27.68785, 85.32568 |
| w111965835 | Ratopul / रातोपुल | secondary | 54.9 | 7 | 2 | – | – | – | yes | 27.70808, 85.33664 |
| w180373624 | Ring Road / काठमाडाैँ चक्रपथ | trunk | 55 | 14 | 2 | 4 | – | – | yes | 27.72229, 85.34544 |
| w1192743662 | Ropeway Sadak | secondary | 9 | (7) | 1 | – | – | – |  | 27.72014, 85.29554 |
| w331304016 | Roshi | unclassified | 31.1 | (3.5) | 1 | – | – | – |  | 27.57390, 85.49231 |
| w904058269 | Rudramati Marg | residential | 19 | (5) | 1 | – | – | – |  | 27.68765, 85.32500 |
| w32133151 | Sankhu Road | primary | 52 | 7 | 1 | – | – | – |  | 27.72909, 85.44620 |
| w111965837 | Setopul / सेतोपुल | secondary | 44.4 | 9 | 1 | – | – | – | yes | 27.70296, 85.33577 |
| w206890837 | Shanti Ram Chowk Road | residential | 11.5 | (3.5) | 1 | – | – | – |  | 27.74824, 85.35877 |
| w341653198 | Sindu Road | secondary | 17.9 | (5.5) | 1 | – | – | – |  | 27.80733, 85.52322 |
| w340829802 | Small Bridge | tertiary | 10.8 | (6) | 1 | 2 | – | – |  | 27.69365, 85.24396 |
| w1018508301 | TaakhelTaukhel Road / टाखेल तौखेल सडक | unclassified | 10.5 | 4 | 1 | – | – | – |  | 27.60024, 85.34373 |
| w1018508304 | TaakhelTaukhel Road / टाखेल तौखेल सडक | tertiary | 8.1 | 4 | 1 | – | – | – |  | 27.60323, 85.35128 |
| w199430779 | Tribhuvan Rajpath | trunk | 37.9 | (14) | 1 | 2 | – | – |  | 27.68619, 85.26025 |
| w537930474 | Way to Jorpati | tertiary | 15.6 | 6 | 1 | – | – | – |  | 27.71542, 85.40256 |
| w331883446 | Way to Namo Budha | secondary | 17.3 | (5.5) | 1 | – | – | – |  | 27.58100, 85.55486 |
| w331883447 | Way to Namo Budha | tertiary | 14.3 | (4.5) | 1 | – | – | – |  | 27.57745, 85.55777 |
| w27034171 | – | primary | 20.5 | (7) | 1 | – | – | – |  | 27.67744, 85.40787 |
| w32133114 | – | primary | 45.2 | (7) | 1 | – | – | – |  | 27.72948, 85.42995 |
| w32133938 | – | tertiary | 13 | 4 | 1 | – | – | – |  | 27.69882, 85.42702 |
| w126533992 | – | secondary | 26.2 | (7) | 1 | – | – | – |  | 27.66905, 85.43405 |
| w126550519 | – | primary | 16.9 | (7) | 1 | – | – | – |  | 27.66969, 85.43757 |
| w126550599 | – | residential | 15.6 | (5) | 1 | – | – | – |  | 27.67080, 85.43631 |
| w126551053 | – | residential | 14.7 | (5) | 1 | – | – | – |  | 27.67087, 85.43558 |
| w126551055 | – | residential | 23.6 | (5) | 1 | – | – | – |  | 27.66973, 85.43493 |
| w126552258 | – | primary | 28.4 | 6 | 1 | – | – | – |  | 27.67346, 85.44805 |
| w178427787 | – | residential | 16.3 | (5) | 2 | – | – | – |  | 27.73603, 85.35290 |
| w188446400 | – | primary | 22.4 | (7) | 1 | – | – | – |  | 27.66894, 85.43738 |
| w190891707 | – | residential | 26.2 | (5) | 1 | – | – | – |  | 27.72473, 85.34894 |
| w190892855 | – | footway | 20.3 | (2) | 1 | – | – | – |  | 27.72723, 85.34891 |
| w190894105 | – | footway | 13.3 | (2) | 1 | – | – | – |  | 27.73346, 85.35108 |
| w195597713 | – | residential | 8.7 | (5) | 1 | – | – | – |  | 27.66441, 85.44211 |
| w199767114 | – | tertiary | 27.4 | (4.5) | 1 | – | – | – |  | 27.61522, 85.35888 |
| w200724462 | – | unclassified | 14.6 | (3.5) | 1 | 1 | – | – |  | 27.76166, 85.36426 |
| w207259814 | – | residential | 9.7 | (3.5) | 1 | – | – | – |  | 27.75169, 85.35928 |
| w207267367 | – | footway | 9.1 | (1.5) | 1 | – | – | – |  | 27.75694, 85.36286 |
| w213124634 | – | secondary | 33.2 | (5.5) | 1 | – | – | – |  | 27.78220, 85.56957 |
| w217175831 | – | residential | 8 | (3.5) | 1 | – | – | – |  | 27.68283, 85.40504 |
| w217175833 | – | service | 3.4 | (3.5) | 1 | – | – | – |  | 27.68533, 85.40063 |
| w221198911 | – | path | 22.4 | (1.2) | 1 | – | – | – |  | 27.70128, 85.47217 |
| w225395944 | – | tertiary | 18.5 | (7) | 1 | – | – | – |  | 27.69967, 85.33098 |
| w225395947 | – | tertiary | 16.4 | (7) | 1 | – | – | – |  | 27.69798, 85.33146 |
| w230296784 | – | residential | 17.8 | (5) | 1 | – | – | – |  | 27.62581, 85.52674 |
| w230301127 | – | path | 5.1 | (1.2) | 1 | – | – | – |  | 27.61103, 85.53328 |
| w230365055 | – | path | 12.6 | (1.5) | 1 | – | – | – |  | 27.59706, 85.44112 |
| w230378399 | – | unclassified | 7.6 | (3.5) | 1 | – | – | – |  | 27.59303, 85.47270 |
| w230547361 | – | path | 17.1 | (1.2) | 1 | – | – | – |  | 27.60006, 85.43627 |
| w239791653 | – | residential | 5.5 | (3.5) | 1 | – | – | – |  | 27.68373, 85.40123 |
| w278977043 | – | footway | 12.2 | (2) | 1 | – | – | – |  | 27.71469, 85.30099 |
| w317810414 | – | unclassified | 12.5 | (3.5) | 1 | – | – | – |  | 27.69962, 85.43151 |
| w319760589 | – | track | 118 | 1 | 1 | – | – | – |  | 27.72735, 85.43623 |
| w331883440 | – | unclassified | 7.6 | (3.5) | 1 | – | – | – |  | 27.58626, 85.55881 |
| w331883441 | – | unclassified | 10.3 | (3.5) | 1 | – | – | – |  | 27.56807, 85.54492 |
| w331883442 | – | secondary | 14 | (5.5) | 1 | – | – | – |  | 27.57420, 85.55042 |
| w331883443 | – | tertiary | 10.2 | (4.5) | 1 | – | – | – |  | 27.58378, 85.55532 |
| w332457697 | – | secondary | 20.6 | 7 | 1 | – | – | – |  | 27.72939, 85.46931 |
| w332694764 | – | residential | 37.6 | (3.5) | 1 | – | – | – |  | 27.72253, 85.45223 |
| w340440354 | – | unclassified | 7.8 | (3.5) | 1 | – | – | – |  | 27.66399, 85.44717 |
| w340440356 | – | unclassified | 18.4 | (3.5) | 1 | – | – | – |  | 27.66370, 85.44953 |
| w340533792 | – | secondary | 13.1 | (7) | 1 | – | – | – |  | 27.82783, 85.57385 |
| w340610319 | – | secondary | 11.1 | (5.5) | 1 | – | – | – |  | 27.80041, 85.57063 |
| w340699438 | – | path | 39.8 | (1.2) | 1 | – | – | – |  | 27.54823, 85.35217 |
| w340829806 | – | unclassified | 12.7 | (5) | 1 | – | – | – |  | 27.69237, 85.25078 |
| w340829809 | – | unclassified | 13.9 | (3.5) | 1 | – | – | – |  | 27.69327, 85.23820 |
| w340839227 | – | residential | 14.4 | (5) | 1 | – | – | – |  | 27.68815, 85.25601 |
| w340863541 | – | unclassified | 5.4 | (3.5) | 1 | – | – | – |  | 27.70134, 85.21715 |
| w340864901 | – | unclassified | 14.1 | (5) | 1 | – | – | – |  | 27.70024, 85.22071 |
| w340864903 | – | unclassified | 14.1 | (5) | 1 | – | – | – |  | 27.69926, 85.22667 |
| w340881564 | – | track | 13.4 | (3) | 1 | – | – | – |  | 27.69608, 85.23176 |
| w340891504 | – | track | 14.8 | (3) | 1 | – | – | – |  | 27.71997, 85.18174 |
| w340891508 | – | unclassified | 63.6 | (3.5) | 1 | – | – | – |  | 27.72908, 85.17787 |
| w340892525 | – | unclassified | 5.4 | 4 | 1 | – | – | – |  | 27.74984, 85.36274 |
| w340927591 | – | residential | 12.7 | (3.5) | 1 | – | – | – |  | 27.77155, 85.32491 |
| w341136940 | – | unclassified | 4.3 | (3.5) | 1 | – | – | – |  | 27.77113, 85.31004 |
| w341136941 | – | unclassified | 19.1 | (3.5) | 1 | – | – | – |  | 27.78023, 85.31314 |
| w341174810 | – | footway | 58.4 | (1.5) | 1 | – | – | – |  | 27.81224, 85.50977 |
| w341245690 | – | path | 10.7 | (1.2) | 1 | – | – | – |  | 27.56911, 85.33213 |
| w341304620 | – | unclassified | 36 | (3.5) | 1 | – | – | – |  | 27.72061, 85.46937 |
| w341420511 | – | secondary | 60.3 | (7) | 1 | – | – | – |  | 27.78724, 85.56995 |
| w341482610 | – | track | 58.6 | (3) | 1 | – | – | – |  | 27.81663, 85.57791 |
| w341508510 | – | unclassified | 15.5 | (3.5) | 1 | – | – | – |  | 27.66019, 85.43917 |
| w341545166 | – | secondary | 17.7 | (5.5) | 1 | – | – | – |  | 27.77140, 85.32964 |
| w341545317 | – | unclassified | 12.8 | (3.5) | 1 | – | – | – |  | 27.77615, 85.33316 |
| w341589971 | – | residential | 11.2 | (5) | 1 | – | – | – |  | 27.66190, 85.36316 |
| w341595612 | – | footway | 39 | (1.5) | 1 | – | – | suspension |  | 27.83750, 85.42810 |
| w341601130 | – | path | 11.6 | (1.2) | 1 | – | – | – |  | 27.82811, 85.41032 |
| w341620018 | – | path | 19.1 | (1.2) | 1 | – | – | – |  | 27.69897, 85.57120 |
| w341653610 | – | path | 71.2 | (1.2) | 1 | – | – | simple-suspension |  | 27.80438, 85.52434 |
| w341653611 | – | path | 70.6 | (1.2) | 1 | – | – | suspension |  | 27.80062, 85.53126 |
| w342105331 | – | footway | 22.1 | (2) | 1 | – | – | – |  | 27.62875, 85.35357 |
| w342296635 | – | path | 4 | (1.2) | 1 | – | – | – |  | 27.59640, 85.34489 |
| w342296636 | – | footway | 6.2 | (1.5) | 1 | – | – | – |  | 27.59777, 85.34332 |
| w342783641 | – | residential | 63.9 | 6 | 1 | – | – | – |  | 27.76322, 85.31870 |
| w342783642 | – | residential | 13 | 8 | 1 | – | – | – |  | 27.76364, 85.30894 |
| w342832172 | – | track | 44.3 | (3) | 1 | – | – | – |  | 27.75549, 85.28695 |
| w342910329 | – | residential | 33.2 | (3.5) | 1 | – | – | – |  | 27.72111, 85.45148 |
| w343036469 | – | tertiary | 38.6 | (6) | 1 | – | – | – |  | 27.68193, 85.26205 |
| w343050628 | – | residential | 21.8 | (3.5) | 1 | – | – | – |  | 27.61121, 85.54086 |
| w343056878 | – | track | 11.8 | (3) | 1 | – | – | – |  | 27.54774, 85.36182 |
| w343259406 | – | track | 13.4 | (3) | 1 | – | – | – |  | 27.75945, 85.30105 |
| w343259407 | – | unclassified | 14.5 | (5) | 1 | – | – | – |  | 27.75771, 85.30168 |
| w343506580 | – | unclassified | 6.5 | (5) | 1 | – | – | – |  | 27.70556, 85.23178 |
| w343506581 | – | unclassified | 12 | (3.5) | 1 | – | – | – |  | 27.70967, 85.23143 |
| w343513362 | – | path | 3.3 | (1.2) | 1 | – | – | – |  | 27.71940, 85.19970 |
| w343563783 | – | footway | 57.1 | (1.5) | 1 | – | – | – |  | 27.54982, 85.34138 |
| w343563788 | – | path | 19.9 | (1.2) | 1 | – | – | – |  | 27.55089, 85.33116 |
| w343744058 | – | secondary | 23.5 | (5.5) | 1 | – | – | – |  | 27.67592, 85.44862 |
| w343767574 | – | tertiary | 15.5 | (4.5) | 1 | – | – | – |  | 27.81875, 85.58132 |
| w344306617 | – | track | 71 | (3) | 1 | – | – | – |  | 27.72591, 85.43176 |
| w344376422 | – | tertiary | 13.8 | 3 | 1 | – | – | – |  | 27.69553, 85.43945 |
| w344528309 | – | track | 20.9 | (3) | 1 | – | – | – |  | 27.71896, 85.48652 |
| w344584621 | – | unclassified | 8.8 | (3.5) | 1 | – | – | – |  | 27.60868, 85.49498 |
| w344753414 | – | track | 16 | (3) | 1 | – | – | – |  | 27.74112, 85.55272 |
| w344754040 | – | residential | 15.8 | (3.5) | 1 | – | – | – |  | 27.69772, 85.45755 |
| w344757409 | – | secondary | 12.9 | (5.5) | 1 | – | – | – |  | 27.68172, 85.47899 |
| w344854071 | – | track | 10.3 | (3) | 1 | – | – | – |  | 27.75298, 85.50873 |
| w344854073 | – | track | 7.4 | (3) | 1 | – | – | – |  | 27.75266, 85.50857 |
| w346552687 | – | path | 38.1 | (1.2) | 1 | – | – | – |  | 27.57861, 85.18018 |
| w347222661 | – | path | 13.5 | (1.5) | 1 | – | – | – |  | 27.69988, 85.33241 |
| w360780742 | – | unclassified | 10.7 | (5) | 1 | – | – | – |  | 27.60310, 85.37135 |
| w367946648 | – | footway | 10.6 | (1.5) | 1 | – | – | – |  | 27.65812, 85.47319 |
| w367946650 | – | footway | 17.4 | (1.5) | 1 | – | – | – |  | 27.65982, 85.46119 |
| w444132598 | – | track | 14.3 | (3) | 1 | – | – | – |  | 27.71831, 85.28916 |
| w452758956 | – | residential | 12.1 | (5) | 1 | – | – | – |  | 27.75315, 85.30259 |
| w500574845 | – | path | 12.3 | (1.5) | 1 | – | – | – |  | 27.75852, 85.30309 |
| w500748553 | – | service | 11.9 | (3.5) | 1 | – | – | – |  | 27.75999, 85.30605 |
| w501166634 | – | track | 7.1 | (3) | 1 | – | – | – |  | 27.76369, 85.29785 |
| w501166636 | – | service | 3.9 | (3.5) | 1 | – | – | – |  | 27.76517, 85.29787 |
| w501166637 | – | residential | 11.8 | (3.5) | 1 | – | – | – |  | 27.76522, 85.29826 |
| w501166639 | – | residential | 13 | (3.5) | 1 | – | – | – |  | 27.76700, 85.29642 |
| w548615533 | – | path | 5.8 | (1.2) | 1 | – | – | – |  | 27.70349, 85.47600 |
| w605729012 | – | residential | 8.1 | (5) | 1 | – | – | – |  | 27.76294, 85.34733 |
| w650951425 | – | tertiary | 15 | (7) | 1 | – | – | – |  | 27.69423, 85.32994 |
| w652166750 | – | path | 10 | (1.5) | 1 | – | – | – |  | 27.69721, 85.33172 |
| w653866897 | – | path | 21.9 | (1.5) | 1 | – | – | – |  | 27.71420, 85.33847 |
| w766157957 | – | tertiary | 61.4 | (7) | 2 | – | – | – | yes | 27.69038, 85.32801 |
| w890591564 | – | unclassified | 13.6 | (3.5) | 1 | – | – | – |  | 27.78506, 85.20848 |
| w891351417 | – | track | 11.6 | (3) | 1 | – | – | – |  | 27.57006, 85.32934 |
| w891351467 | – | path | 26 | (1.2) | 1 | – | – | simple-suspension | yes | 27.55396, 85.31898 |
| w891351469 | – | path | 11.8 | (1.2) | 1 | – | – | – |  | 27.55386, 85.31874 |
| w891434928 | – | path | 34.9 | (1.2) | 1 | – | – | suspension |  | 27.83988, 85.21184 |
| w903294852 | – | path | 14.4 | (1.5) | 1 | – | – | – |  | 27.69610, 85.33089 |
| w922787306 | – | track | 14.1 | (3) | 1 | – | – | – |  | 27.81368, 85.18186 |
| w938739043 | – | tertiary | 55.9 | (7) | 2 | – | – | – | yes | 27.69037, 85.32869 |
| w955048062 | – | residential | 9.2 | (3.5) | 1 | – | – | – |  | 27.62626, 85.26774 |
| w959175579 | – | residential | 11.3 | (5) | 1 | – | – | – |  | 27.71455, 85.29872 |
| w987347025 | – | tertiary | 15 | (7) | 1 | – | – | – |  | 27.72179, 85.34451 |
| w1011615993 | – | unclassified | 13.6 | (3.5) | 1 | – | – | – |  | 27.81648, 85.57718 |
| w1014296943 | – | secondary | 14 | (5.5) | 1 | – | – | – |  | 27.81150, 85.57211 |
| w1021895714 | – | unclassified | 12.1 | (3.5) | 1 | – | – | – |  | 27.80683, 85.57965 |
| w1085245478 | – | residential | 4.1 | (5) | 1 | – | – | – |  | 27.72498, 85.33846 |
| w1085245480 | – | footway | 5.7 | (2) | 1 | – | – | – |  | 27.72419, 85.33803 |
| w1099720090 | – | path | 4.6 | (1.5) | 1 | – | – | – |  | 27.66090, 85.36507 |
| w1105178271 | – | footway | 12.1 | (2) | 1 | – | – | – |  | 27.73384, 85.35213 |
| w1105667116 | – | residential | 9 | (5) | 1 | – | – | – |  | 27.76455, 85.34993 |
| w1117541541 | – | residential | 18.6 | (3.5) | 1 | – | – | – |  | 27.76421, 85.31946 |
| w1121140132 | – | tertiary | 12.7 | (4.5) | 1 | – | – | – |  | 27.58851, 85.29615 |
| w1125569556 | – | tertiary | 12.7 | (4.5) | 1 | – | – | – |  | 27.77782, 85.39296 |
| w1126627984 | – | footway | 2.3 | (2) | 1 | – | – | – |  | 27.71411, 85.31453 |
| w1130275490 | – | path | 46.1 | (1.2) | 1 | – | – | – |  | 27.82199, 85.21690 |
| w1136096502 | – | tertiary | 10.7 | (4.5) | 1 | – | – | – |  | 27.78534, 85.24740 |
| w1136476677 | – | residential | 19.9 | (5) | 1 | – | – | – | yes | 27.68435, 85.26045 |
| w1136873418 | – | footway | 52.3 | 12 | 2 | – | – | – | yes | 27.69028, 85.32800 |
| w1137239126 | – | unclassified | 7.7 | (3.5) | 1 | – | – | – |  | 27.65829, 85.43713 |
| w1146984562 | – | track | 23.8 | (3) | 1 | – | – | – |  | 27.67417, 85.44470 |
| w1156551243 | – | path | 21.5 | (1.5) | 1 | – | – | – |  | 27.71907, 85.34084 |
| w1156551244 | – | path | 21.7 | (1.5) | 1 | – | – | – |  | 27.71643, 85.33885 |
| w1156551245 | – | tertiary | 23.1 | (7) | 1 | – | – | – |  | 27.69557, 85.32984 |
| w1171230420 | – | unclassified | 10.8 | (3.5) | 1 | – | – | – |  | 27.66238, 85.45425 |
| w1175542348 | – | residential | 3.6 | (5) | 1 | – | – | – |  | 27.70809, 85.27007 |
| w1175551910 | – | footway | 10.4 | (1.5) | 1 | – | – | – |  | 27.70721, 85.26684 |
| w1178275627 | – | unclassified | 18.5 | (3.5) | 1 | – | – | – |  | 27.72071, 85.24955 |
| w1180364679 | – | track | 12.6 | (3) | 1 | – | – | – |  | 27.70820, 85.26438 |
| w1180364682 | – | residential | 13.3 | (5) | 1 | – | – | – |  | 27.70875, 85.26349 |
| w1180399767 | – | residential | 11.9 | (3.5) | 1 | – | – | – |  | 27.76614, 85.34054 |
| w1180606691 | – | path | 10.1 | (1.2) | 1 | – | – | – |  | 27.76535, 85.34107 |
| w1183878174 | – | residential | 19.9 | (3.5) | 1 | – | – | – |  | 27.77290, 85.32232 |
| w1183878175 | – | residential | 12.1 | (3.5) | 1 | – | – | – |  | 27.77240, 85.32194 |
| w1183878180 | – | residential | 9.6 | (3.5) | 1 | – | – | – |  | 27.77193, 85.32210 |
| w1183880876 | – | residential | 12.8 | (3.5) | 1 | – | – | – |  | 27.77138, 85.32705 |
| w1183885950 | – | path | 7.8 | (1.5) | 1 | – | – | – |  | 27.77257, 85.33233 |
| w1187978941 | – | residential | 9.1 | (5) | 1 | – | – | – |  | 27.68428, 85.26134 |
| w1188603536 | – | residential | 21.5 | (5) | 1 | – | – | – |  | 27.68214, 85.26052 |
| w1188603538 | – | residential | 15 | (5) | 1 | – | – | – |  | 27.68232, 85.26052 |
| w1188603540 | – | residential | 21.2 | (5) | 1 | – | – | – |  | 27.68222, 85.26130 |
| w1189291007 | – | residential | 14.5 | (3.5) | 1 | – | – | – |  | 27.59773, 85.56215 |
| w1189331422 | – | trunk | 100.2 | (14) | 1 | – | – | – | yes | 27.69506, 85.23470 |
| w1189360901 | – | unclassified | 13.9 | (5) | 1 | – | – | – |  | 27.69492, 85.23408 |
| w1191168297 | – | residential | 11.3 | (5) | 1 | – | – | – |  | 27.69220, 85.25319 |
| w1192743666 | – | residential | 11.9 | (5) | 1 | – | – | – | yes | 27.71784, 85.29615 |
| w1192785913 | – | trunk | 34.4 | (14) | 1 | – | – | – |  | 27.69998, 85.22480 |
| w1192788233 | – | residential | 9.9 | (3.5) | 1 | – | – | – |  | 27.69926, 85.22927 |
| w1203853922 | – | residential | 4.9 | (3.5) | 1 | – | – | – |  | 27.75075, 85.29064 |
| w1212451065 | – | residential | 12.4 | (3.5) | 1 | – | – | – |  | 27.74983, 85.45017 |
| w1212451067 | – | residential | 16.5 | (3.5) | 1 | – | – | – |  | 27.75032, 85.45094 |
| w1212452235 | – | track | 26 | (3) | 1 | – | – | – |  | 27.73277, 85.44997 |
| w1212452241 | – | track | 27.3 | 4 | 1 | – | – | – |  | 27.73557, 85.44985 |
| w1212452243 | – | track | 9.2 | (3) | 1 | – | – | – |  | 27.74095, 85.44793 |
| w1236236946 | – | residential | 9.7 | (5) | 1 | – | – | – |  | 27.69813, 85.23183 |
| w1238237900 | – | unclassified | 11.4 | (5) | 1 | – | – | – |  | 27.64881, 85.38493 |
| w1238237903 | – | residential | 6.6 | (5) | 1 | – | – | – |  | 27.64777, 85.38543 |
| w1247717003 | – | secondary | 9.3 | 6 | 1 | – | – | – |  | 27.63376, 85.27919 |
| w1253162401 | – | unclassified | 10.6 | (5) | 1 | – | – | – |  | 27.74795, 85.30085 |
| w1255593534 | – | residential | 8.4 | (5) | 1 | – | – | – |  | 27.74628, 85.30003 |
| w1258082790 | – | residential | 3.4 | (3.5) | 1 | – | – | – |  | 27.74909, 85.29308 |
| w1258082792 | – | residential | 4.1 | (3.5) | 1 | – | – | – |  | 27.74960, 85.29264 |
| w1259115540 | – | residential | 10.6 | (3.5) | 1 | – | – | – |  | 27.77142, 85.28961 |
| w1261892591 | – | service | 10.3 | (4.5) | 1 | – | – | – |  | 27.71470, 85.40301 |
| w1261892592 | – | residential | 7 | (5) | 1 | – | – | – |  | 27.71560, 85.40242 |
| w1268588321 | – | residential | 7.7 | (5) | 1 | – | – | – |  | 27.71476, 85.29768 |
| w1291948098 | – | residential | 12 | (3.5) | 1 | – | – | – |  | 27.76143, 85.29792 |
| w1291948102 | – | residential | 26.4 | (3.5) | 1 | – | – | – |  | 27.76212, 85.29782 |
| w1296380993 | – | path | 4.5 | (1.2) | 1 | – | – | – |  | 27.74919, 85.36449 |
| w1307455252 | – | unclassified | 23.8 | (3.5) | 1 | – | – | – |  | 27.76886, 85.36522 |
| w1373594605 | – | footway | 13.4 | (2) | 1 | – | – | – |  | 27.72825, 85.34986 |
| w1373594609 | – | residential | 19.1 | (5) | 1 | – | – | – |  | 27.73327, 85.35157 |
| w1373610026 | – | footway | 17.5 | (2) | 1 | – | – | – |  | 27.71989, 85.34194 |
| w1384267967 | – | residential | 13.2 | (5) | 1 | – | – | – |  | 27.68167, 85.35580 |
| w1413998975 | – | residential | 10.1 | (5) | 1 | – | – | – |  | 27.71632, 85.29651 |
| w1416502109 | – | path | 13.8 | (1.5) | 1 | – | – | – |  | 27.71481, 85.30094 |
| w1416763474 | – | service | 18.8 | (4) | 1 | – | – | – |  | 27.68671, 85.25881 |
| w1416763476 | – | residential | 29.8 | (5) | 1 | – | – | – |  | 27.68681, 85.25841 |
| w1417982077 | – | residential | 5.5 | (3.5) | 1 | – | – | – |  | 27.76732, 85.36326 |
| w1420219123 | – | path | 7.5 | (1.5) | 1 | – | – | – |  | 27.69169, 85.24791 |
| w1423873509 | – | service | 6 | (3.5) | 1 | – | – | – |  | 27.74455, 85.44804 |
| w1423981013 | – | residential | 9.2 | 3 | 1 | – | – | – |  | 27.77645, 85.32979 |
| w1423981015 | – | path | 10.6 | 7 | 1 | – | – | – |  | 27.77638, 85.32992 |
| w1423981020 | – | path | 8.9 | 7 | 1 | – | – | – |  | 27.77633, 85.33074 |
| w1423981021 | – | residential | 6.4 | (3.5) | 1 | – | – | – |  | 27.77610, 85.33002 |
| w1423981023 | – | residential | 6 | (3.5) | 1 | – | – | – |  | 27.77578, 85.33018 |
| w1423981029 | – | path | 14 | (1.2) | 1 | – | – | – |  | 27.77530, 85.33064 |
| w1423982872 | – | residential | 12.2 | 8 | 1 | – | – | – |  | 27.77477, 85.33113 |
| w1423982873 | – | residential | 7.4 | (5) | 1 | – | – | – |  | 27.77456, 85.33109 |
| w1450304626 | – | residential | 23.8 | (3.5) | 1 | – | – | – |  | 27.74458, 85.44291 |
| w1484959282 | – | trunk | 53 | (9) | 1 | – | – | – | yes | 27.69674, 85.23169 |
| w1487197672 | – | track | 7.5 | (3) | 1 | – | – | – |  | 27.69980, 85.22256 |
| w1512345557 | – | residential | 12.3 | (3.5) | 1 | – | – | – |  | 27.75297, 85.29003 |
| w1512345558 | – | residential | 7.1 | (3.5) | 1 | – | – | – |  | 27.75207, 85.29086 |
| w1516441109 | – | residential | 6.6 | (5) | 1 | – | – | – |  | 27.77277, 85.36164 |
| w1525393091 | – | path | 5.5 | (1.2) | 1 | – | – | – |  | 27.74792, 85.56332 |
| w1525437542 | – | path | 32.7 | (1.2) | 1 | – | – | suspention |  | 27.65436, 85.57222 |
| w1526074460 | – | track | 13.1 | (3) | 1 | – | – | beam |  | 27.63919, 85.57433 |
| w1526336291 | – | path | 24.9 | (1.2) | 1 | – | – | suspension |  | 27.63850, 85.57420 |
| w1536597559 | – | service | 10.8 | (4.5) | 1 | – | – | – |  | 27.66028, 85.36959 |
| w1553876483 | – | unclassified | 12.1 | (3.5) | 1 | – | – | – |  | 27.77245, 85.23883 |
| w1559324588 | – | path | 6.8 | (1.2) | 1 | – | – | – |  | 27.65804, 85.29318 |

## 4. Bridges over water (inferred from untagged crossings) [V]

Untagged roads that cross a river, stream or canal line: mostly culverts over small khola in real life; the pipeline still builds a short deck with railings (decision 3). Worth a check before replacing them by culverts.

| OSM | name | class | length | width | layer | lanes | water | at |
|---|---|---|---:|---:|---:|---:|---|---|
| w1103057010 | – | pedestrian | 104.5 | 4 | 0 | – | Alko Hiti area | 27.67828, 85.32593 |
| w930029831 | – | track | 72.4 | (3) | 0 | – | badare kholsa | 27.73969, 85.21307 |
| w960125106 | – | footway | 117.5 | (1.5) | 0 | – | Bansdol River | 27.59218, 85.51315 |
| w302394469 | – | service | 190.3 | (4.5) | 0 | – | Bhachaa River | 27.71785, 85.29208 |
| w1231014497 | – | residential | 284.2 | (5) | 0 | – | Bhachaa River | 27.71753, 85.29171 |
| w1525642173 | – | residential | 418.3 | (5) | 0 | – | Bhachaa River | 27.71960, 85.29556 |
| w205424636 | – | residential | 180.4 | (5) | 0 | – | Bishnumati River | 27.74602, 85.32284 |
| w340465983 | – | unclassified | 313.5 | (3.5) | 0 | – | Bishnumati River | 27.77475, 85.34589 |
| w341539402 | – | path | 213.8 | (1.2) | 0 | – | Bojinee Dam | 27.69366, 85.49705 |
| w341546383 | – | path | 275.7 | (1.2) | 0 | – | Bojinee Dam | 27.69238, 85.49715 |
| w577932439 | Dharke Highway | secondary | 862.5 | (5.5) | 0 | – | boksikhola | 27.74141, 85.21814 |
| w1408714173 | – | track | 439.1 | (3) | 0 | – | boksikhola | 27.74019, 85.22002 |
| w344771538 | Kuntabesi-Nayagaun-Nagarkot Road | tertiary | 12931.1 | 3.5 | 0 | 1 | Cha-Khola | 27.70717, 85.56560 |
| w343020265 | – | path | 491.1 | (1.2) | 0 | – | Cha-Khola | 27.70294, 85.53250 |
| w341363366 | – | unclassified | 67.6 | (3.5) | 0 | – | Chauthe Khola | 27.77839, 85.17918 |
| w340639572 | – | unclassified | 5211.3 | (3.5) | 0 | – | Chhahare Khola | 27.83817, 85.22515 |
| w170703859 | Bhajangal Road | residential | 956.7 | (5) | 0 | – | Chikhu | 27.67226, 85.28394 |
| w152303281 | Town Planning Road | residential | 245.5 | (5) | 0 | – | Chikhu | 27.67344, 85.28045 |
| w179238237 | – | residential | 167.4 | (5) | 0 | – | Chikhu | 27.67326, 85.28202 |
| w315267795 | – | residential | 192.3 | (5) | 0 | – | Chikhu | 27.67297, 85.28118 |
| w319080800 | – | residential | 225.7 | (5) | 0 | – | Chikhu | 27.67271, 85.27774 |
| w322201565 | – | residential | 289.3 | (5) | 0 | – | Chikhu | 27.67464, 85.28165 |
| w922786980 | – | track | 1571.2 | (3) | 0 | – | Dovan khola | 27.82388, 85.22403 |
| w1110930089 | – | unclassified | 2379.7 | (3.5) | 0 | – | Dovan khola | 27.82479, 85.22281 |
| w554444123 | Tahuley Road | unclassified | 4416.6 | (3.5) | 0 | 1 | Dubalo Khola, Magada Stream, Taagu Khola | 27.79207, 85.25721 |
| w1229251209 | – | path | 556.1 | (1.2) | 0 | – | Ghatte Khola | 27.67018, 85.39219 |
| w680349051 | – | footway | 10.4 | (2) | 0 | – | Ghyoilisang Pond and Peace Park | 27.72240, 85.36142 |
| w680349053 | – | footway | 9.3 | (2) | 0 | – | Ghyoilisang Pond and Peace Park | 27.72240, 85.36148 |
| w340678029 | – | unclassified | 619.2 | (3.5) | 0 | – | Godawari Khola | 27.60899, 85.36300 |
| w342105342 | – | track | 99.6 | (3) | 0 | – | Godawari Khola | 27.64227, 85.36178 |
| w1074109679 | – | footway | 370.2 | (1.5) | 0 | – | Godawari Khola | 27.63091, 85.35236 |
| w1422205415 | – | path | 100.2 | (1.2) | 0 | – | Godawari Khola | 27.63403, 85.35883 |
| w279514047 | – | unclassified | 1146.3 | (3.5) | 0 | – | Hydropower Canal | 27.57147, 85.52584 |
| w900828257 | – | unclassified | 394.4 | (3.5) | 0 | – | Hydropower Canal | 27.57280, 85.52300 |
| w900828259 | – | unclassified | 281.3 | (3.5) | 0 | – | Hydropower Canal | 27.57156, 85.52457 |
| w184877737 | Dasarath Chand Marg | residential | 304.4 | (5) | 0 | – | Icchumati | 27.72276, 85.33413 |
| w184540622 | guruma marg | residential | 173.8 | (5) | 0 | – | Icchumati | 27.72944, 85.33613 |
| w39985876 | Ichhunadi Marg | residential | 688.5 | (5) | 0 | – | Icchumati | 27.72345, 85.33347 |
| w85552302 | Lamtangin Marg / लामटाङ्गिन मार्ग | tertiary | 391.5 | 6 | 0 | – | Icchumati | 27.72926, 85.33582 |
| w185008602 | Shiva Galli | residential | 204.4 | (5) | 0 | – | Icchumati | 27.72075, 85.32558 |
| w184540623 | – | residential | 33.2 | (5) | 0 | – | Icchumati | 27.72921, 85.33595 |
| w185578991 | – | service | 39 | (4) | 0 | – | Icchumati | 27.73034, 85.33550 |
| w243147504 | – | residential | 38.5 | (5) | 0 | – | Icchumati | 27.72887, 85.33629 |
| w153233422 | – | unclassified | 1993.6 | (3.5) | 0 | – | jhakri khola | 27.73445, 85.21649 |
| w341826682 | – | path | 124.3 | (1.2) | 0 | – | jhakri khola | 27.73694, 85.20964 |
| w340530702 | – | track | 3289.5 | (3) | 0 | – | Kalphu River | 27.76594, 85.20259 |
| w341734595 | – | path | 659.5 | (1.2) | 0 | – | Kalphu River | 27.77036, 85.19119 |
| w1125447517 | – | unclassified | 490.9 | (3.5) | 0 | – | Kalphu River | 27.76905, 85.18081 |
| w1137133986 | – | track | 337.3 | (3) | 0 | – | Kalphu River | 27.77636, 85.24689 |
| w341220044 | – | residential | 365.1 | (5) | 0 | – | Karmanasha Khola | 27.66711, 85.34106 |
| w345180208 | – | residential | 60.1 | (5) | 0 | – | Karmanasha Khola | 27.67116, 85.34193 |
| w1146435400 | – | unclassified | 90.5 | 5 | 0 | – | Karmanasha Khola | 27.65868, 85.33554 |
| w1149114578 | – | service | 807.4 | (4.5) | 0 | – | Karmanasha Khola | 27.66702, 85.34143 |
| w1183348011 | – | unclassified | 222.5 | (3.5) | 0 | – | Karmanasha Khola | 27.65857, 85.33580 |
| w220905583 | – | unclassified | 1417.1 | (3.5) | 0 | – | Kasan Khola | 27.69744, 85.45423 |
| w1303884433 | – | service | 157.4 | (3.5) | 0 | – | Kasan Khola | 27.68671, 85.43711 |
| w153233434 | – | tertiary | 981.1 | (6) | 0 | – | Khahare Khola | 27.70024, 85.22720 |
| w357017711 | Gems Galli - Government's Official Path | path | 355.1 | (1.5) | 0 | – | Khola River | 27.64186, 85.32593 |
| w223504510 | Udhyog Marg | residential | 567.4 | (5) | 0 | – | Khola River | 27.64543, 85.33437 |
| w341676741 | – | track | 124.3 | (3) | 0 | – | Khola River | 27.63465, 85.32461 |
| w341677136 | – | path | 166.7 | (1.2) | 0 | – | Khola River | 27.63438, 85.32400 |
| w1231942372 | – | unclassified | 48.4 | (5) | 0 | – | Khola River | 27.63804, 85.32588 |
| w1231942373 | – | unclassified | 40.1 | (5) | 0 | – | Khola River | 27.63773, 85.32581 |
| w341648406 | Chautara- Harishiddi Marg | residential | 476.9 | (5) | 0 | – | Kodku Khola | 27.63623, 85.33265 |
| w225436007 | – | unclassified | 1305.4 | (3.5) | 0 | – | Kodku Khola | 27.61620, 85.34766 |
| w342056238 | – | track | 450 | (3) | 0 | – | Kodku Khola | 27.61158, 85.35109 |
| w1436242614 | – | residential | 353 | (5) | 0 | – | Kodku Khola | 27.63740, 85.33561 |
| w1391531513 | – | path | 808.7 | (1.2) | 0 | – | Kolmati Khola | 27.73740, 85.40475 |
| w340992476 | – | track | 690.4 | (3) | 0 | – | Kulekhani | 27.56210, 85.20215 |
| w340995929 | – | track | 1516.2 | (3) | 0 | – | Kulekhani | 27.56457, 85.20091 |
| w344795818 | – | path | 290.5 | (1.2) | 0 | – | Kulekhani | 27.55528, 85.21701 |
| w340769729 | – | unclassified | 856.4 | (3.5) | 0 | – | Ladku Khola | 27.55587, 85.55174 |
| w342876573 | – | footway | 354.7 | (1.5) | 0 | – | Ladku Khola | 27.55500, 85.55123 |
| w659112771 | – | pedestrian | 337.3 | (4) | 0 | – | Ladku Khola | 27.55202, 85.54538 |
| w341245687 | – | track | 184.1 | (3) | 0 | – | Lele River | 27.57058, 85.32872 |
| w340941970 | – | unclassified | 242.9 | (3.5) | 0 | – | Lilawati River | 27.59732, 85.45060 |
| w351019949 | – | path | 70.9 | (1.2) | 0 | – | Lubhu Khola | 27.63868, 85.36881 |
| w517638526 | – | residential | 679.2 | (3.5) | 0 | – | Madhav Khola | 27.74919, 85.43359 |
| w340459159 | Magada Road | unclassified | 3653.4 | (3.5) | 0 | 1 | Magada Stream | 27.79572, 85.26147 |
| w340459001 | – | track | 2216.5 | (3) | 0 | – | Magada Stream | 27.80237, 85.26760 |
| w341154295 | – | path | 308 | (1.2) | 0 | – | Magada Stream | 27.80886, 85.27223 |
| w1066436600 | – | track | 722.4 | (3) | 0 | – | Magada Stream | 27.81078, 85.27278 |
| w578366640 | Pasang Lhamu Highway / पासाङ् लाह्मु राजमार्ग | primary | 20793.8 | (6.5) | 1 | 2 | Magada Stream, Sisneri khola, dhading khola | 27.82945, 85.24236 |
| w153233477 | way to sana kisan | unclassified | 549.5 | (3.5) | 0 | – | Maheshkhola, badare kholsa | 27.73840, 85.21034 |
| w1433742675 | – | unclassified | 930.8 | (3.5) | 0 | – | Maheshkhola, neupanekhol | 27.73768, 85.21923 |
| w332496088 | – | secondary | 1731.8 | (5.5) | 0 | – | Maheshkhola, sanokholsi | 27.73572, 85.22852 |
| w244360515 | – | residential | 45.7 | (5) | 0 | – | Manamati | 27.70804, 85.27027 |
| w340710298 | – | residential | 175.9 | (5) | 0 | – | Manamati | 27.70841, 85.27062 |
| w1413289020 | – | unclassified | 133.7 | (3.5) | 0 | – | Manamati | 27.70457, 85.27522 |
| w1396508736 | – | service | 763 | (4) | 0 | – | Manohara, Manohara Nadi | 27.67119, 85.33755 |
| w575849467 | – | unclassified | 262.5 | (3.5) | 0 | – | Murali Khola | 27.60681, 85.43512 |
| w575849469 | – | unclassified | 729.7 | (3.5) | 0 | – | Murali Khola | 27.60338, 85.43244 |
| w277756365 | – | unclassified | 4622 | 11 | 0 | – | Nallu Khola | 27.61519, 85.31040 |
| w283081380 | – | unclassified | 524.5 | (3.5) | 0 | – | Nallu Khola | 27.60129, 85.31942 |
| w341325158 | – | track | 43.9 | (3) | 0 | – | Nallu Khola | 27.57458, 85.31270 |
| w342038224 | – | unclassified | 1663.9 | (3.5) | 0 | – | Nallu Khola | 27.59518, 85.31629 |
| w577932440 | Dharke Highway | secondary | 1194.6 | (5.5) | 0 | 2 | neupanekhol | 27.73943, 85.22340 |
| w341480080 | Sitapaila Jeevanpur Road | tertiary | 12794.9 | (4.5) | 0 | 1 | neupanekhol | 27.75953, 85.19781 |
| w341545159 | Chhahare-Tokha Sadak | tertiary | 3479.1 | 9 | 0 | 2 | Neurey Khola | 27.78637, 85.32828 |
| w1180163872 | – | path | 156 | (1.5) | 1 | – | Neurey Khola | 27.79446, 85.33078 |
| w1180163879 | – | residential | 411.6 | (5) | 0 | – | Neurey Khola | 27.79434, 85.33128 |
| w230377571 | – | unclassified | 2597.5 | (3.5) | 0 | – | padhere Kholso | 27.59422, 85.46689 |
| w307094089 | Bhaktapur-Nala-Banepa Road | primary | 1917.1 | 6 | 0 | – | Punyamata River | 27.66195, 85.49353 |
| w344764433 | – | path | 815.5 | (1.5) | 0 | – | Punyamata River | 27.65444, 85.50180 |
| w966098822 | – | unclassified | 1545.4 | (3.5) | 0 | – | Punyamata River | 27.59552, 85.52334 |
| w1060034849 | – | path | 240.4 | (1.2) | 0 | – | Punyamata River | 27.65360, 85.51218 |
| w1061433793 | – | unclassified | 162.4 | 14 | 0 | – | Punyamata River | 27.65594, 85.50177 |
| w1061433886 | – | unclassified | 86.4 | 1 | 0 | – | Punyamata River | 27.64515, 85.51154 |
| w1063671978 | – | path | 202.2 | (1.2) | 0 | – | Punyamata River | 27.65308, 85.51393 |
| w1063671979 | – | path | 19.5 | (1.2) | 0 | – | Punyamata River | 27.65280, 85.51291 |
| w342109294 | – | residential | 286.2 | (5) | 0 | – | Rajkulo | 27.62542, 85.34565 |
| w342309478 | – | unclassified | 637.4 | (5) | 0 | – | Rajkulo | 27.62293, 85.34614 |
| w344661736 | – | path | 14.9 | (1.5) | 0 | – | Roshi Khola | 27.58147, 85.51216 |
| w461691450 | – | path | 568.4 | (1.2) | 0 | – | Roshi Khola | 27.55715, 85.54398 |
| w1337988434 | – | track | 76.8 | (3) | 0 | – | Roshi Khola | 27.56726, 85.47384 |
| w677223473 | Sali Nadi Road | secondary | 5921.5 | (5.5) | 0 | – | salinadi river | 27.74843, 85.48310 |
| w187350036 | Nawasangam Marg | residential | 377.9 | (5) | 0 | – | Samakhushi | 27.72869, 85.31081 |
| w187350049 | Pushpanjali Marg | residential | 153.9 | (5) | 0 | – | Samakhushi | 27.72799, 85.31415 |
| w58754699 | Rani Devi Marg / रानी देवी मार्ग | tertiary | 780.9 | 4 | 0 | – | Samakhushi | 27.72746, 85.32123 |
| w197615456 | Sublime Boutique Road | residential | 235.8 | (5) | 0 | – | Samakhushi | 27.73790, 85.32927 |
| w112446680 | – | residential | 170.8 | (5) | 0 | – | Samakhushi | 27.72774, 85.31065 |
| w219223410 | – | residential | 103.4 | (5) | 0 | – | Samakhushi | 27.73274, 85.32036 |
| w221325184 | – | path | 35.9 | (1.5) | 0 | – | Samakhushi | 27.72977, 85.31780 |
| w523236487 | – | residential | 14.9 | (5) | 0 | – | Samakhushi | 27.72828, 85.31499 |
| w1096913111 | – | residential | 304.9 | (5) | 0 | – | Samakhushi | 27.73293, 85.32079 |
| w1231026667 | – | residential | 7.1 | (5) | 0 | – | Samakhushi | 27.73796, 85.32854 |
| w431924284 | chamela gaero bato | path | 859.5 | (1.2) | 0 | – | sano kholso | 27.59992, 85.46988 |
| w230379214 | – | unclassified | 551.5 | (3.5) | 0 | – | sano kholso | 27.59623, 85.46949 |
| w431939494 | – | path | 400.3 | (1.2) | 0 | – | sano kholso | 27.59553, 85.46905 |
| w1548907292 | Tribhuvan Rajpath | trunk | 206.4 | (7) | 0 | 2 | Sisne Khola | 27.70795, 85.20013 |
| w1107018201 | – | unclassified | 112.3 | (3.5) | 0 | – | Sisneri khola | 27.81204, 85.21547 |
| w1130566235 | – | unclassified | 3155.6 | (3.5) | 0 | – | Sisneri khola | 27.80989, 85.20921 |
| w578366639 | Pasang Lhamu Highway | primary | 635.1 | (6.5) | 0 | 2 | Thulo Khola | 27.79332, 85.27812 |
| w643297390 | – | path | 239.6 | (1.5) | 0 | – | Tukucha Khola | 27.69075, 85.31543 |
| w184550790 | Anandamaya Marg | residential | 791 | (5) | 0 | – | – | 27.72202, 85.34097 |
| w172325561 | Binayak Basti Marg | residential | 214.2 | (5) | 0 | – | – | 27.72285, 85.29410 |
| w579228668 | BP Highway | trunk | 1845 | (9) | 0 | – | – | 27.61531, 85.55145 |
| w153233413 | Dharke sitapaila road | secondary | 6811.6 | (5.5) | 0 | – | – | 27.73568, 85.18401 |
| w39986163 | Dhumbharai Marg / धुम्बाराही मार्ग | secondary | 346.3 | 7 | 0 | – | – | 27.72268, 85.33864 |
| w1247315969 | gankhu dharamapur way | residential | 622.3 | (5) | 0 | – | – | 27.68290, 85.39381 |
| w517839542 | Hansha Marg | residential | 41.3 | (5) | 0 | – | – | 27.72104, 85.29586 |
| w677218898 | Helambu Trek | unclassified | 302.6 | (3.5) | 0 | – | – | 27.80722, 85.46291 |
| w36485774 | Jal Binayak Marg / जल विनायक मार्ग | residential | 309.3 | 6 | 0 | – | – | 27.67533, 85.31700 |
| w1132486378 | Jamacho Marg / जामाचो मार्ग | tertiary | 1710.7 | 6 | 0 | – | – | 27.72872, 85.28921 |
| w206890309 | Jautar Marg | residential | 673.3 | (5) | 0 | – | – | 27.71535, 85.40286 |
| w356033604 | Namobuddha Road | primary | 3205.8 | (6.5) | 0 | – | – | 27.58847, 85.57728 |
| w25512463 | Pasang Lhamu Highway | primary | 1711.6 | (6.5) | 0 | – | – | 27.83817, 85.19255 |
| w756183449 | Ring Road / काठमाडाैँ चक्रपथ | trunk | 95.9 | 14 | 0 | 4 | – | 27.72311, 85.29411 |
| w58755816 | Sidhicharan Sadak / सिद्धिचरण सादक | secondary | 512.2 | 9 | 0 | – | – | 27.72116, 85.29559 |
| w53097942 | Sikali Marg / सिकली मार्ग | unclassified | 940.4 | (5) | 0 | – | – | 27.64465, 85.29372 |
| w332133311 | Way to Dakshinkali | track | 4183.2 | (3) | 0 | – | – | 27.59425, 85.27186 |
| w27705654 | – | secondary | 3663.4 | 7 | 0 | – | – | 27.71773, 85.26552 |
| w27705797 | – | unclassified | 686.2 | (5) | 0 | – | – | 27.71834, 85.25508 |
| w32135509 | – | track | 6452.9 | (3) | 0 | – | – | 27.77231, 85.40409 |
| w38605269 | – | tertiary | 1349.8 | 4 | 0 | – | – | 27.68742, 85.46606 |
| w43918636 | – | primary | 988.2 | 7 | 0 | – | – | 27.67852, 85.40537 |
| w44385118 | – | steps | 64.2 | (1.2) | 0 | – | – | 27.60508, 85.26409 |
| w44385121 | – | steps | 57 | (1.2) | 0 | – | – | 27.60534, 85.26362 |
| w131386801 | – | track | 1251.3 | (3) | 0 | – | – | 27.73215, 85.53219 |
| w135956146 | – | track | 2188.9 | (3) | 0 | – | – | 27.71929, 85.53685 |
| w154116172 | – | unclassified | 888.7 | (3.5) | 0 | – | – | 27.59695, 85.28702 |
| w172659727 | – | tertiary | 897.6 | 7 | 0 | – | – | 27.72050, 85.34134 |
| w172659787 | – | residential | 148.4 | (5) | 0 | – | – | 27.72010, 85.34060 |
| w172664968 | – | residential | 410.6 | (5) | 0 | – | – | 27.72348, 85.33730 |
| w172680106 | – | residential | 422.7 | (5) | 0 | – | – | 27.67647, 85.44544 |
| w186284203 | – | tertiary | 1154.7 | 6 | 0 | – | – | 27.70778, 85.41409 |
| w194874481 | – | residential | 362.5 | (5) | 0 | – | – | 27.67315, 85.27709 |
| w200101358 | – | residential | 286.9 | (5) | 0 | – | – | 27.72897, 85.34057 |
| w200101369 | – | residential | 97.1 | (5) | 0 | – | – | 27.72858, 85.34073 |
| w200101371 | – | residential | 83.2 | (5) | 0 | – | – | 27.72515, 85.33878 |
| w200101372 | – | residential | 113.9 | (5) | 0 | – | – | 27.72907, 85.34084 |
| w206863802 | – | residential | 205.2 | (5) | 0 | – | – | 27.71959, 85.38520 |
| w206878297 | – | residential | 824.6 | (5) | 0 | – | – | 27.71767, 85.40015 |
| w206974426 | – | track | 976.6 | 4 | 0 | – | – | 27.73131, 85.44814 |
| w207077852 | – | path | 667.1 | (1.2) | 0 | – | – | 27.77477, 85.39017 |
| w216006964 | – | path | 514 | (1.2) | 0 | – | – | 27.69906, 85.43856 |
| w216012460 | – | track | 439.8 | (3) | 0 | – | – | 27.70208, 85.43414 |
| w217336802 | – | residential | 1924.8 | (5) | 0 | – | – | 27.65588, 85.36822 |
| w219889835 | – | unclassified | 727.8 | (3.5) | 0 | – | – | 27.69541, 85.47078 |
| w230377572 | – | unclassified | 2068.6 | (3.5) | 0 | – | – | 27.60471, 85.46463 |
| w237658091 | – | unclassified | 327.1 | 1 | 0 | – | – | 27.65429, 85.52968 |
| w244911328 | – | footway | 47.6 | (2) | 0 | – | – | 27.72344, 85.33752 |
| w244913123 | – | footway | 39.2 | (2) | 0 | – | – | 27.72633, 85.33962 |
| w244919016 | – | footway | 37.3 | (2) | 0 | – | – | 27.72710, 85.33989 |
| w245265318 | – | path | 330.6 | (1.5) | 0 | – | – | 27.64762, 85.27861 |
| w277780760 | – | secondary | 1336.2 | (5.5) | 0 | – | – | 27.57798, 85.56440 |
| w279687280 | – | unclassified | 310.1 | (3.5) | 0 | – | – | 27.61293, 85.49187 |
| w298757715 | – | track | 4943.6 | (3) | 0 | – | – | 27.83301, 85.33657 |
| w303977025 | – | unclassified | 8364.1 | (3.5) | 0 | – | – | 27.83360, 85.35079 |
| w340425600 | – | unclassified | 4360.6 | (3.5) | 0 | – | – | 27.77975, 85.50878 |
| w340427796 | – | track | 1196.6 | (3) | 0 | – | – | 27.79886, 85.40942 |
| w340427806 | – | track | 4541.8 | (3) | 0 | – | – | 27.79310, 85.42386 |
| w340427846 | – | unclassified | 1013.7 | (3.5) | 0 | – | – | 27.76428, 85.39196 |
| w340428888 | – | residential | 136.6 | (5) | 0 | – | – | 27.76959, 85.29331 |
| w340430546 | – | unclassified | 707.2 | (3.5) | 0 | – | – | 27.82334, 85.20081 |
| w340430557 | – | track | 380.4 | (3) | 0 | – | – | 27.82580, 85.19920 |
| w340444959 | – | path | 411.9 | (1.2) | 0 | – | – | 27.66255, 85.44130 |
| w340452961 | – | unclassified | 610.4 | (3.5) | 0 | – | – | 27.82255, 85.48619 |
| w340452973 | – | unclassified | 2949.4 | (3.5) | 0 | – | – | 27.80469, 85.50457 |
| w340455166 | – | unclassified | 2961.9 | (3.5) | 0 | – | – | 27.76757, 85.50758 |
| w340455622 | – | unclassified | 1379.7 | (3.5) | 0 | – | – | 27.66325, 85.55647 |
| w340455627 | – | tertiary | 448.2 | (4.5) | 0 | – | – | 27.66133, 85.55455 |
| w340455872 | – | footway | 718.9 | (1.5) | 0 | – | – | 27.65394, 85.57728 |
| w340468425 | – | unclassified | 10916.3 | (3.5) | 0 | – | – | 27.80245, 85.21097 |
| w340469642 | – | tertiary | 1000.4 | (6) | 0 | – | – | 27.77794, 85.36781 |
| w340470213 | – | residential | 491.1 | (3.5) | 0 | – | – | 27.75189, 85.45312 |
| w340471145 | – | path | 526.2 | (1.2) | 0 | – | – | 27.73208, 85.53289 |
| w340471601 | – | track | 237.6 | (3) | 0 | – | – | 27.73493, 85.53423 |
| w340487605 | – | track | 1106.3 | (3) | 0 | – | – | 27.77632, 85.42572 |
| w340494745 | – | unclassified | 1590.9 | (3.5) | 0 | – | – | 27.78132, 85.20340 |
| w340519607 | – | track | 585.5 | (3) | 0 | – | – | 27.83118, 85.40590 |
| w340519608 | – | track | 573.4 | (3) | 0 | – | – | 27.82924, 85.41409 |
| w340520531 | – | unclassified | 2820.8 | (3.5) | 0 | – | – | 27.78890, 85.51842 |
| w340536123 | – | track | 375.4 | (3) | 0 | – | – | 27.68221, 85.48278 |
| w340536919 | – | unclassified | 4596 | (3.5) | 0 | – | – | 27.77954, 85.53812 |
| w340539728 | – | unclassified | 4790.6 | (3.5) | 0 | – | – | 27.78708, 85.18225 |
| w340540557 | – | track | 783.5 | (3) | 0 | – | – | 27.81604, 85.55982 |
| w340544480 | – | unclassified | 311.4 | (3.5) | 0 | – | – | 27.79962, 85.57606 |
| w340544511 | – | track | 250.7 | (3) | 0 | – | – | 27.81517, 85.18191 |
| w340544513 | – | track | 181.2 | (3) | 0 | – | – | 27.81605, 85.18108 |
| w340544514 | – | track | 1083.6 | (3) | 0 | – | – | 27.81784, 85.17951 |
| w340546520 | – | path | 191.9 | (1.2) | 0 | – | – | 27.82386, 85.19811 |
| w340547020 | – | unclassified | 1798.2 | (3.5) | 0 | – | – | 27.81480, 85.56479 |
| w340570291 | – | path | 808.9 | (1.2) | 0 | – | – | 27.79806, 85.19159 |
| w340572903 | – | residential | 151.5 | 3 | 0 | – | – | 27.72810, 85.46877 |
| w340574151 | – | unclassified | 367.6 | (3.5) | 0 | – | – | 27.68658, 85.46386 |
| w340575325 | – | unclassified | 574.2 | (3.5) | 0 | – | – | 27.62844, 85.35244 |
| w340575423 | – | tertiary | 1056.2 | (4.5) | 0 | – | – | 27.82022, 85.55692 |
| w340579870 | – | path | 1718.8 | (1.2) | 0 | – | – | 27.80807, 85.19704 |
| w340682036 | – | unclassified | 459.8 | (3.5) | 0 | – | – | 27.79667, 85.55135 |
| w340683148 | – | path | 497.4 | (1.2) | 0 | – | – | 27.61311, 85.46825 |
| w340697893 | – | unclassified | 1257.8 | (3.5) | 0 | – | – | 27.54710, 85.36014 |
| w340714015 | – | unclassified | 1525 | (3.5) | 0 | – | – | 27.77839, 85.20319 |
| w340883183 | – | path | 539.1 | (1.2) | 0 | – | – | 27.79272, 85.32104 |
| w340967382 | – | track | 986.5 | (3) | 0 | – | – | 27.58006, 85.27349 |
| w340976782 | – | unclassified | 332.1 | (5) | 0 | – | – | 27.69493, 85.46799 |
| w341114943 | – | secondary | 51.3 | (5.5) | 0 | – | – | 27.82390, 85.48738 |
| w341136942 | – | unclassified | 939.3 | 6 | 0 | – | – | 27.76785, 85.30843 |
| w341152930 | – | track | 27.2 | (3) | 0 | – | – | 27.59333, 85.29168 |
| w341159815 | – | path | 1629.3 | (1.2) | 0 | – | – | 27.81600, 85.46375 |
| w341159819 | – | unclassified | 959.4 | (3.5) | 0 | – | – | 27.81296, 85.47336 |
| w341191666 | – | track | 421.7 | (3) | 0 | – | – | 27.55002, 85.34312 |
| w341195293 | – | unclassified | 224 | (3.5) | 0 | – | – | 27.54868, 85.35543 |
| w341378646 | – | path | 990.3 | (1.2) | 0 | – | – | 27.54512, 85.36732 |
| w341387426 | – | footway | 209.4 | (1.5) | 0 | – | – | 27.71176, 85.19095 |
| w341412908 | – | residential | 44.5 | (5) | 0 | – | – | 27.65733, 85.37022 |
| w341484811 | – | track | 1577.3 | (3) | 0 | – | – | 27.72071, 85.56684 |
| w341497985 | – | path | 113.4 | (1.2) | 0 | – | – | 27.83883, 85.40922 |
| w341536194 | – | unclassified | 172.9 | (3.5) | 0 | – | – | 27.65186, 85.37242 |
| w341576382 | – | track | 267.7 | (3) | 0 | – | – | 27.78497, 85.30909 |
| w341583352 | – | unclassified | 749.2 | (3.5) | 0 | – | – | 27.75391, 85.42672 |
| w341608432 | – | path | 1145.6 | (1.2) | 0 | – | – | 27.82691, 85.41375 |
| w341620213 | – | path | 623 | (1.2) | 0 | – | – | 27.76688, 85.39273 |
| w341624320 | – | track | 816.1 | (3) | 0 | – | – | 27.78195, 85.42372 |
| w341624325 | – | track | 896.8 | (3) | 0 | – | – | 27.78752, 85.42316 |
| w341636076 | – | track | 525.5 | (3) | 0 | – | – | 27.83712, 85.21773 |
| w341796629 | – | path | 463.1 | (1.2) | 0 | – | – | 27.81742, 85.56405 |
| w341817688 | – | track | 166.4 | (3) | 0 | – | – | 27.74622, 85.22110 |
| w341875101 | – | unclassified | 2510.7 | (3.5) | 0 | – | – | 27.79700, 85.53190 |
| w341882107 | – | tertiary | 4182.1 | (4.5) | 1 | – | – | 27.72617, 85.53495 |
| w341892519 | – | service | 674.8 | (3.5) | 0 | – | – | 27.58475, 85.28745 |
| w341892521 | – | unclassified | 770.4 | (3.5) | 0 | – | – | 27.57971, 85.28414 |
| w341912431 | – | unclassified | 312.6 | (3.5) | 0 | – | – | 27.73312, 85.22448 |
| w341925170 | – | unclassified | 1325.1 | (3.5) | 0 | – | – | 27.83273, 85.28855 |
| w342024678 | – | path | 159.7 | (1.2) | 0 | – | – | 27.77676, 85.39262 |
| w342084902 | – | track | 304.7 | (3) | 0 | – | – | 27.54925, 85.35909 |
| w342091103 | – | path | 471.5 | (1.2) | 0 | – | – | 27.57322, 85.29448 |
| w342278172 | – | unclassified | 3004.7 | (3.5) | 0 | – | – | 27.82015, 85.46860 |
| w342329444 | – | path | 60 | (1.2) | 0 | – | – | 27.58057, 85.28755 |
| w342389833 | – | path | 619.1 | (1.2) | 0 | – | – | 27.73307, 85.43049 |
| w342390967 | – | path | 1093 | (1.2) | 0 | – | – | 27.73723, 85.43369 |
| w342393221 | – | unclassified | 331.2 | (5) | 0 | – | – | 27.65866, 85.46495 |
| w342561095 | – | track | 293.8 | (3) | 0 | – | – | 27.69085, 85.43837 |
| w342611191 | – | track | 772.2 | (3) | 0 | – | – | 27.81644, 85.55642 |
| w342817242 | – | path | 391.4 | (1.2) | 0 | – | – | 27.83775, 85.41010 |
| w342964196 | – | residential | 46.5 | (3.5) | 0 | – | – | 27.75281, 85.45480 |
| w343155942 | – | residential | 388.6 | (5) | 0 | – | – | 27.68153, 85.39628 |
| w343316717 | – | path | 124.7 | (1.2) | 0 | – | – | 27.73925, 85.20884 |
| w343428252 | – | path | 594.8 | (1.2) | 0 | – | – | 27.63391, 85.56798 |
| w343563780 | – | unclassified | 777.4 | (3.5) | 0 | – | – | 27.54896, 85.32657 |
| w343563781 | – | track | 174.9 | (3) | 0 | – | – | 27.55056, 85.34013 |
| w343563784 | – | unclassified | 10.9 | (3.5) | 0 | – | – | 27.55106, 85.32869 |
| w343563787 | – | track | 518.3 | (3) | 0 | – | – | 27.54893, 85.32879 |
| w343563809 | – | path | 40.5 | (1.2) | 0 | – | – | 27.55072, 85.33727 |
| w343602550 | – | track | 1167.9 | (3) | 0 | – | – | 27.82742, 85.43650 |
| w344577588 | – | path | 253.9 | (1.2) | 0 | – | – | 27.60400, 85.42544 |
| w344592880 | – | secondary | 165.2 | (5.5) | 0 | – | – | 27.82325, 85.48769 |
| w344633154 | – | path | 95.9 | (1.2) | 0 | – | – | 27.60756, 85.49520 |
| w344795816 | – | track | 389.2 | (3) | 0 | – | – | 27.55857, 85.21375 |
| w344817220 | – | track | 279.7 | (3) | 0 | – | – | 27.78564, 85.50506 |
| w345156761 | – | unclassified | 1930 | (3.5) | 0 | – | – | 27.83275, 85.46824 |
| w354686070 | – | path | 170.9 | (1.2) | 0 | – | – | 27.72130, 85.46654 |
| w355551310 | – | unclassified | 419.8 | (5) | 0 | – | – | 27.69131, 85.47079 |
| w358548159 | – | secondary | 459.2 | 8 | 0 | – | – | 27.72013, 85.38276 |
| w359647226 | – | secondary | 18844.5 | (5.5) | 0 | – | – | 27.56174, 85.22487 |
| w452872896 | – | footway | 209 | (2) | 0 | – | – | 27.60162, 85.35169 |
| w517506100 | – | path | 21.5 | (1.2) | 0 | – | – | 27.72764, 85.46918 |
| w517506116 | – | path | 78.4 | (1.2) | 0 | – | – | 27.72434, 85.47031 |
| w517726586 | – | unclassified | 85.1 | (3.5) | 0 | – | – | 27.75351, 85.42205 |
| w521771435 | – | residential | 34.6 | (5) | 0 | – | – | 27.72107, 85.38630 |
| w572250067 | – | path | 131.9 | (1.2) | 0 | – | – | 27.67130, 85.50534 |
| w573421423 | – | track | 380.9 | (3) | 0 | – | – | 27.55360, 85.24739 |
| w573423739 | – | track | 1528 | (3) | 0 | – | – | 27.56818, 85.25148 |
| w573480768 | – | unclassified | 579.1 | 2 | 0 | – | – | 27.69794, 85.46996 |
| w575206009 | – | unclassified | 658 | (3.5) | 0 | – | – | 27.54803, 85.34665 |
| w576964792 | – | track | 147.8 | (3) | 0 | – | – | 27.75854, 85.46470 |
| w578314176 | – | track | 498.2 | (3) | 0 | – | – | 27.58470, 85.27706 |
| w592288153 | – | unclassified | 47.4 | (3.5) | 0 | – | – | 27.63470, 85.28099 |
| w648631786 | – | residential | 88.4 | (5) | 0 | – | – | 27.72151, 85.38699 |
| w668633301 | – | path | 20.1 | (1.2) | 0 | – | – | 27.57196, 85.29430 |
| w763119076 | – | residential | 40.7 | (3.5) | 0 | – | – | 27.68920, 85.43823 |
| w855457144 | – | residential | 109.1 | (3.5) | 0 | – | – | 27.70287, 85.44875 |
| w856215230 | – | track | 3474.4 | (3) | 0 | – | – | 27.63019, 85.56135 |
| w871194330 | – | unclassified | 1193.3 | (3.5) | 0 | – | – | 27.81671, 85.47093 |
| w873123038 | – | track | 558.7 | (3) | 0 | – | – | 27.74629, 85.56819 |
| w886181011 | – | unclassified | 1168.2 | (3.5) | 0 | – | – | 27.83486, 85.43189 |
| w886218239 | – | unclassified | 1704.8 | (3.5) | 0 | – | – | 27.83587, 85.33462 |
| w887903960 | – | track | 188.9 | (3) | 0 | – | – | 27.81247, 85.50764 |
| w891351457 | – | unclassified | 1095.9 | (3.5) | 0 | – | – | 27.55551, 85.31602 |
| w891351462 | – | service | 251.3 | (3.5) | 0 | – | – | 27.55167, 85.32103 |
| w904098022 | – | footway | 5.1 | (1.5) | 0 | – | – | 27.69093, 85.38881 |
| w905368214 | – | track | 6.2 | (3) | 0 | – | – | 27.57208, 85.19439 |
| w930378838 | – | residential | 187.2 | (5) | 0 | – | – | 27.64357, 85.33586 |
| w930381307 | – | residential | 203.4 | (5) | 0 | – | – | 27.64414, 85.33532 |
| w1007433036 | – | residential | 180.7 | (5) | 0 | – | – | 27.56385, 85.53613 |
| w1017098887 | – | path | 697.5 | (1.2) | 0 | – | – | 27.69757, 85.57215 |
| w1023025907 | – | unclassified | 2093.3 | (3.5) | 0 | – | – | 27.80716, 85.45598 |
| w1023025911 | – | unclassified | 855.6 | (3.5) | 0 | – | – | 27.82953, 85.47544 |
| w1039477785 | – | path | 357 | (1.2) | 0 | – | – | 27.77802, 85.58108 |
| w1039826857 | – | track | 1298.8 | (3) | 0 | – | – | 27.74526, 85.57173 |
| w1044106114 | – | footway | 4023.9 | 4 | 0 | – | – | 27.71680, 85.38002 |
| w1065588375 | – | track | 434.8 | 1 | 0 | – | – | 27.63258, 85.52963 |
| w1066314915 | – | unclassified | 437 | (3.5) | 0 | – | – | 27.81554, 85.21124 |
| w1068777956 | – | track | 110.9 | 3 | 0 | – | – | 27.63931, 85.52443 |
| w1087499969 | – | unclassified | 3488.2 | (3.5) | 0 | – | – | 27.77819, 85.22538 |
| w1107018198 | – | track | 4028.8 | (3) | 0 | – | – | 27.81925, 85.21725 |
| w1128010437 | – | unclassified | 1164.5 | (3.5) | 0 | – | – | 27.82887, 85.21594 |
| w1137136063 | – | unclassified | 35.1 | (3.5) | 0 | – | – | 27.77559, 85.23782 |
| w1137560869 | – | unclassified | 796.2 | (3.5) | 0 | – | – | 27.77638, 85.22407 |
| w1145555680 | – | unclassified | 971 | (3.5) | 0 | – | – | 27.79882, 85.18580 |
| w1160621723 | – | residential | 133.3 | (3.5) | 0 | – | – | 27.67882, 85.44691 |
| w1168924798 | – | residential | 1043.8 | 12 | 0 | – | – | 27.76117, 85.31688 |
| w1183878177 | – | residential | 48 | (3.5) | 0 | – | – | 27.77272, 85.32212 |
| w1221229571 | – | path | 1144.2 | (1.2) | 0 | – | – | 27.79966, 85.45785 |
| w1227751946 | – | path | 1814 | (1.2) | 0 | – | – | 27.78188, 85.38906 |
| w1262772728 | – | path | 206 | (1.5) | 0 | – | – | 27.67194, 85.44709 |
| w1350835982 | – | unclassified | 1580.8 | (3.5) | 0 | – | – | 27.58503, 85.18831 |
| w1392336966 | – | residential | 99.8 | (3.5) | 0 | – | – | 27.70801, 85.41147 |
| w1392358652 | – | track | 255.9 | (3) | 0 | – | – | 27.72315, 85.44151 |
| w1433332085 | – | unclassified | 1122.2 | (3.5) | 0 | – | – | 27.56990, 85.19219 |
| w1486013076 | – | track | 38.9 | (3) | 0 | – | – | 27.69675, 85.23133 |
| w1524301515 | – | residential | 156.9 | (5) | 0 | – | – | 27.72301, 85.29331 |
| w1525133030 | – | unclassified | 1214.5 | (3.5) | 0 | – | – | 27.72852, 85.53556 |
| w1525387140 | – | unclassified | 983.8 | (3.5) | 0 | – | – | 27.75427, 85.54428 |
| w1525393085 | – | track | 1369.1 | (3) | 0 | – | – | 27.74399, 85.56554 |
| w1525393089 | – | track | 321.7 | (3) | 0 | – | – | 27.74489, 85.56879 |
| w1525428640 | – | path | 754.5 | (1.2) | 0 | – | – | 27.65309, 85.56677 |
| w1525437541 | – | footway | 366.5 | (1.5) | 0 | – | – | 27.65390, 85.57349 |

## 5. Flyovers and overpasses

| OSM | name | class | length | width | layer | lanes | over | at |
|---|---|---|---:|---:|---:|---:|---|---|
| w406752033 | Ring Road / काठमाडाैँ चक्रपथ | trunk | 345.2 | 6 | 1 | 2 | Mangalbazar to gwarko (w136518312) | 27.66583, 85.33154 |
| w1023025918 | – | unclassified | 7 | (3.5) | 1 | – | path (w344778538) | 27.82728, 85.47743 |
| w1174194088 | – | service | 8.9 | (4) | 1 | – | residential (w219811389) | 27.69609, 85.36719 |
| w1426160993 | Kathmandu Ringroad / काठमाडाैँ चक्रपथ | trunk | 1089.1 | 6 | 1 | 2 | Mangalbazar to gwarko (w136518312) | 27.66742, 85.33291 |
| w1484959280 | – | trunk | 19.8 | (7) | 1 | – | Tribhuvan Rajpath (w232502593) | 27.68840, 85.23624 |
| w1499708786 | – | trunk | 19.4 | (14) | 1 | – | residential (w1484959277) | 27.69102, 85.23721 |
| w1548907287 | – | tertiary | 26 | (4.5) | 1 | – | trunk (w1192785914) | 27.70118, 85.22432 |

## 6. Foot overbridges

| OSM | name | class | length | width | layer | lanes | over | at |
|---|---|---|---:|---:|---:|---:|---|---|
| w112116410 | – | footway | 14.2 | (2) | 1 | – | Kanti Path (w672194957) | 27.70878, 85.31445 |
| w112116412 | – | footway | 32.9 | (2) | 1 | – | Kanti Path (w172387564), Kanti Path (w231885504) | 27.70872, 85.31459 |
| w112448812 | – | footway | 49.4 | (2) | 1 | – | Durbar Marg (w171215507), Ratna Park Path (w171603746) | 27.70621, 85.31610 |
| w112449571 | – | footway | 17.3 | (1.5) | 1 | – | Kanti Path (w172387590) | 27.70483, 85.31382 |
| w112449582 | – | footway | 15.5 | (2) | 1 | – | primary (w422272357), Kanti Path (w422272360) | 27.69964, 85.31337 |
| w112449587 | – | footway | 21 | (1.5) | 1 | – | Kanti Path (w696986540) | 27.70018, 85.31360 |
| w120443300 | – | footway | 23.5 | (2) | 2 | – | Pulchowk Road (w232518478), Pulchowk Road (w439996379) | 27.67760, 85.31641 |
| w188440142 | – | pedestrian | 26.4 | (4) | 1 | – | Araniko Highway (w232096883), Araniko Highway (w1080177873) | 27.67410, 85.36905 |
| w195516756 | – | footway | 17.5 | (2) | 1 | – | Kanti Path (w4825630) | 27.70713, 85.31433 |
| w215470949 | – | pedestrian | 32 | (4) | 1 | – | Araniko Highway (w229075024), Araniko Highway (w1080177870) | 27.67463, 85.36462 |
| w301644010 | – | footway | 16.6 | (2) | 1 | – | Durbar Marg (w171603744) | 27.69999, 85.31669 |
| w301779936 | – | footway | 19.4 | (2) | 1 | – | Ratna Park Path (w171603746) | 27.70638, 85.31604 |
| w301779941 | – | footway | 18.3 | (2) | 1 | – | Baghbazaar Road (w220025931) | 27.70601, 85.31639 |
| w351926854 | Mahankal Marg | path | 17.9 | 2 | 1 | – | Durbar Marg (w351926855) | 27.70425, 85.31629 |
| w416294072 | – | footway | 53.9 | (2) | 1 | – | Ring Road (w416294069), Kathmandu Ringroad (w648697035), primary (w670112944) … | 27.67713, 85.34645 |
| w432547662 | – | footway | 33.8 | 3 | 2 | – | Araniko Highway (w229075030), Araniko Highway (w232096882), service (w438377936) | 27.67490, 85.36047 |
| w495663549 | – | footway | 25.9 | (2) | 1 | – | Araniko Highway (w194607860), Araniko Highway (w232099517) | 27.67012, 85.41030 |
| w495663573 | – | footway | 29.7 | (2) | 1 | – | Araniko Highway (w232099517), Araniko Highway (w232434589), service (w1363281971) | 27.66735, 85.41663 |
| w509772997 | – | footway | 32 | (2) | 1 | – | Araniko Highway (w171203193), Araniko Highway (w231882046) | 27.67328, 85.38200 |
| w905354109 | – | path | 15 | (1.2) | 1 | – | secondary (w359647226) | 27.57890, 85.18002 |
| w924354499 | – | footway | 141.9 | (2) | 1 | – | path (w1287017671) | 27.62955, 85.32327 |
| w1019961179 | – | path | 40.1 | (1.5) | 1 | – | Madan Bhandari Path (w221323019), Madan Bhandari Path (w231880680), tertiary (w435253159) … | 27.68855, 85.33503 |
| w1073749287 | – | pedestrian | 26.2 | (4) | 1 | – | Araniko Highway (w194607852), Araniko Highway (w231882046), service (w1354585618) | 27.67383, 85.37331 |
| w1075038728 | – | footway | 36.7 | (2) | 1 | – | Kathmandu Ringroad (w193085043), Ring Road (w216297391), primary (w1091658641) … | 27.65812, 85.32406 |
| w1082033794 | – | footway | 40.1 | (2) | 1 | – | Kathmandu Ringroad (w193085043), primary (w704143313), Ring Road (w1091658655) … | 27.66762, 85.30755 |
| w1082033805 | – | footway | 42.9 | (2) | 1 | – | primary (w172348744), Ring Road (w175587305), Kathmandu Ringroad (w193085043) … | 27.67263, 85.30359 |
| w1090811377 | – | footway | 32.6 | (2) | 1 | – | Ring Road (w416294069), Kathmandu Ringroad (w648697035), primary (w670112944) … | 27.67753, 85.34779 |
| w1091000047 | – | footway | 36.7 | (2) | 1 | – | primary (w670112951), primary (w1090811373), Ring Road (w1426160995) … | 27.67042, 85.33886 |
| w1091006194 | – | footway | 14.4 | (2) | 1 | – | Araniko Highway (w226834863) | 27.67517, 85.35200 |
| w1091006196 | – | footway | 9.2 | (2) | 1 | – | Araniko Highway (w231882049) | 27.67530, 85.35201 |
| w1094497382 | – | footway | 28.2 | (2) | 1 | – | Araniko Highway (w224823944), Araniko Highway (w232096884) | 27.67339, 85.38734 |
| w1171213926 | – | footway | 26.2 | (2) | 1 | – | Araniko Highway (w232099515), Araniko Highway (w232434590) | 27.66589, 85.42362 |
| w1172034810 | – | path | 23.4 | (1.5) | 1 | – | Araniko Highway (w194607861), Araniko Highway (w194607863) | 27.67291, 85.40470 |
| w1174960075 | – | footway | 16.8 | (1.5) | 1 | – | New Road (w4840388), Kanti Path (w172387593) | 27.70294, 85.31377 |
| w1175398049 | – | footway | 43 | (2) | 2 | – | primary (w342998885), Ring Road (w406752033), primary (w1091658641) … | 27.66408, 85.33031 |
| w1268595049 | – | footway | 26.6 | (2) | 1 | – | Chandra Binayak Marg (w303580912), Kathmandu Ringroad (w1071901893), Kathmandu Ringroad (w1071913169) | 27.71699, 85.34641 |
| w1268595053 | – | footway | 19 | (2) | 1 | – | Lekhnath Sadak (w176144636) | 27.71793, 85.31305 |
| w1268595057 | – | footway | 16.3 | (2) | 1 | – | Ram Shah Path (w173044198), Ram Shah Path (w231880679) | 27.69535, 85.32097 |
| w1268595060 | – | footway | 15.8 | (2) | 1 | – | Bhaktithapa Road (w1090992808) | 27.69028, 85.33576 |
| w1433527431 | – | footway | 12.2 | (2) | 1 | – | Bishnumati Track Road (w81146145) | 27.70725, 85.30189 |
| w1467493658 | – | footway | 236.4 | (1.5) | 1 | – | footway (w218419299), Araniko Highway (w357506892), unclassified (w575210055) | 27.64450, 85.47469 |

## 7. Underpasses

Every road that passes under a deck, with the free height the pipeline guarantees (deck underside above the road surface, over the whole stretch under the deck's corridor; at least 5.5 m). Lowered ones sink into a cutting under a ground road or a river bridge.

| OSM | name | class | length | width | layer | lanes | clearance m | lowered | under | at |
|---|---|---|---:|---:|---:|---:|---:|---|---|---|
| w4825630 | Kanti Path / कान्ति पथ | primary | 165.7 | 14 | 0 | 2 | 5.50 | yes | footway (w195516756) | 27.70843, 85.31431 |
| w4840388 | New Road | tertiary | 459.2 | 5 | 0 | – | 5.50 |  | footway (w1174960075) | 27.70321, 85.31291 |
| w81146145 | Bishnumati Track Road | secondary | 892.3 | 14 | 0 | 2 | 5.50 | yes | footway (w1433527431) | 27.70564, 85.30167 |
| w81146155 | Bishnumati Track Road | secondary | 351.9 | 7 | 0 | – | 5.50 | yes | Kankeswari Bridge (w184870766) | 27.70538, 85.30220 |
| w112664643 | Janata Marg / जनता मार्ग | secondary | 426.9 | 6 | 0 | – | 5.50 |  | Ring Road (w345052247), primary (w670112943), Kathmandu Ringroad (w670112945) … | 27.67424, 85.34090 |
| w120435381 | – | residential | 2138.3 | (5) | 0 | – | 5.50 | yes | Sinamangal Bridge (w646793006), Sinamangal Bridge (w1353164359) | 27.69047, 85.34860 |
| w124188127 | – | pedestrian | 549.1 | 7 | 0 | – | 5.50 | yes | Sankhamul Pul (w112785201) | 27.67932, 85.33077 |
| w134726849 | – | footway | 18.4 | (2) | -1 | – | 5.50 | yes | Bagmati Corridor Yela Marga (w751465485) | 27.68354, 85.32789 |
| w134729040 | – | tertiary | 21.5 | 7 | 0 | – | 5.50 | yes | Kalo pool (w52782410) | 27.69215, 85.30435 |
| w136518312 | Mangalbazar to gwarko / मङ्गलबजारदेखि ग्वारकोसम्म | secondary | 45.1 | (7) | 0 | – | 5.50 |  | Ring Road (w406752033), Kathmandu Ringroad (w1426160993) | 27.66657, 85.33225 |
| w171203193 | Araniko Highway | trunk | 557.2 | 10 | 0 | 4 | 5.50 |  | footway (w509772997) | 27.67322, 85.38413 |
| w171215507 | Durbar Marg / दरबार मार्ग | primary | 212.7 | 9 | 0 | 2 | 5.50 | yes | footway (w112448812) | 27.70674, 85.31644 |
| w171603744 | Durbar Marg / दरबार मार्ग | primary | 280.4 | 4 | 0 | 4 | 5.50 |  | footway (w301644010) | 27.70178, 85.31650 |
| w171603746 | Ratna Park Path / रत्न पार्क पथ | primary | 106.4 | 14 | 0 | – | 5.50 | yes | footway (w112448812), footway (w301779936) | 27.70635, 85.31592 |
| w172333281 | Transformor Marg / ट्रान्सफर्मर मार्ग | secondary | 698.9 | 6 | 0 | – | 5.50 | yes | Ganeshman Singh Path (w24691223) | 27.69622, 85.30111 |
| w172348744 | – | primary | 1521.1 | 7 | 0 | 2 | 5.50 |  | footway (w1082033805) | 27.67474, 85.30193 |
| w172387564 | Kanti Path / कान्ति पथ | primary | 52.1 | 7 | -1 | 2 | 5.50 | yes | footway (w112116412) | 27.70862, 85.31455 |
| w172387590 | Kanti Path / कान्ति पथ | primary | 142.9 | 14 | 0 | – | 5.50 | yes | footway (w112449571) | 27.70479, 85.31389 |
| w172387593 | Kanti Path / कान्ति पथ | primary | 90.2 | 14 | 0 | – | 5.50 |  | footway (w1174960075) | 27.70365, 85.31369 |
| w173044198 | Ram Shah Path / राम शाह पथ | primary | 182.4 | 6 | 0 | 1 | 5.50 |  | footway (w1268595057) | 27.69563, 85.32091 |
| w175587305 | Ring Road / काठमाडाैँ चक्रपथ | trunk | 750.6 | 7 | 0 | 2 | 5.50 |  | footway (w1082033805) | 27.67359, 85.30257 |
| w176144636 | Lekhnath Sadak / लेखनाथ सडक | primary | 115.7 | 14 | 0 | 2 | 5.50 |  | footway (w1268595053) | 27.71787, 85.31290 |
| w180205547 | Bagmati Marg / बागमती मार्ग | residential | 88.3 | 7 | 0 | – | 5.50 | yes | Kalo pool (w52782410), footway (w225466624) | 27.69316, 85.30460 |
| w185032787 | – | tertiary | 30.2 | 7 | 0 | – | 5.50 | yes | footway (w225466624) | 27.69224, 85.30476 |
| w193085043 | Kathmandu Ringroad / काठमाडाैँ चक्रपथ | trunk | 4216.7 | 6 | 0 | 2 | 5.50 |  | footway (w1075038728), footway (w1082033794), footway (w1082033805) | 27.66372, 85.31537 |
| w194607852 | Araniko Highway | trunk | 108.4 | 10 | 0 | 2 | 5.50 |  | pedestrian (w1073749287) | 27.67393, 85.37332 |
| w194607860 | Araniko Highway | trunk | 401.8 | 12 | 0 | 2 | 5.50 |  | footway (w495663549) | 27.67082, 85.40966 |
| w194607861 | Araniko Highway | trunk | 131.1 | 10 | 0 | 2 | 5.50 |  | path (w1172034810) | 27.67280, 85.40449 |
| w194607863 | Araniko Highway | trunk | 489.6 | 10 | 0 | 2 | 5.50 |  | path (w1172034810) | 27.67332, 85.40306 |
| w216297391 | Ring Road / काठमाडाैँ चक्रपथ | trunk | 249.6 | 6 | 0 | 2 | 5.50 |  | footway (w1075038728) | 27.65784, 85.32309 |
| w218419299 | – | footway | 143.6 | (1.5) | 0 | – | 49.57 |  | footway (w1467493658) | 27.64372, 85.47435 |
| w219811389 | – | residential | 430.5 | (4) | 0 | – | 5.50 |  | service (w1174194088) | 27.69481, 85.36704 |
| w220025931 | Baghbazaar Road / बागबजार सडक | secondary | 96.2 | 9 | 0 | 2 | 5.50 | yes | footway (w301779941) | 27.70605, 85.31670 |
| w221323019 | Madan Bhandari Path / मदन भण्डारी पथ | trunk | 428.8 | 6 | 0 | 2 | 5.57 |  | path (w1019961179) | 27.68876, 85.33312 |
| w224823944 | Araniko Highway | trunk | 1420 | 10 | 0 | 2 | 5.50 |  | footway (w1094497382) | 27.67464, 85.39583 |
| w225457469 | Ring Road / काठमाडाैँ चक्रपथ | trunk | 55.7 | 7 | -1 | 2 | 0.00 | yes |  | 27.69286, 85.28174 |
| w226834863 | Araniko Highway / अरनिको राजमार्ग | trunk | 591.5 | 9 | 0 | 2 | 5.50 |  | footway (w1091006194) | 27.67635, 85.34999 |
| w227926515 | – | unclassified | 2900 | (3.5) | 0 | – | 5.50 | yes | footway (w342070227) | 27.60344, 85.29356 |
| w229075024 | Araniko Highway | trunk | 58.7 | 9 | 0 | 2 | 5.50 |  | pedestrian (w215470949) | 27.67456, 85.36458 |
| w229075030 | Araniko Highway | trunk | 445 | 9 | 0 | 2 | 5.50 |  | footway (w432547662) | 27.67475, 85.36142 |
| w231880679 | Ram Shah Path / राम शाह पथ | primary | 169.6 | 6 | 0 | 1 | 5.50 |  | footway (w1268595057) | 27.69549, 85.32096 |
| w231880680 | Madan Bhandari Path / मदन भण्डारी पथ | trunk | 87.4 | 7 | 0 | 2 | 5.50 |  | path (w1019961179) | 27.68834, 85.33530 |
| w231882046 | Araniko Highway | trunk | 1020.9 | 10 | 0 | 2 | 5.50 |  | footway (w509772997), pedestrian (w1073749287) | 27.67371, 85.37788 |
| w231882049 | Araniko Highway | trunk | 113.4 | 12 | 0 | 4 | 5.50 |  | footway (w1091006196) | 27.67533, 85.35197 |
| w231885504 | Kanti Path | primary | 44.2 | 7 | -1 | 1 | 5.50 | yes | footway (w112116412) | 27.70875, 85.31467 |
| w232096882 | Araniko Highway | trunk | 848.1 | 9 | 0 | 2 | 6.07 |  | footway (w432547662) | 27.67469, 85.36051 |
| w232096883 | Araniko Highway | trunk | 793.8 | 9 | 0 | 2 | 5.50 |  | pedestrian (w188440142) | 27.67434, 85.36614 |
| w232096884 | Araniko Highway | trunk | 1112.9 | 10 | 0 | 4 | 5.50 |  | footway (w1094497382) | 27.67389, 85.39073 |
| w232099515 | Araniko Highway | trunk | 310.5 | 8 | 0 | 2 | 5.50 |  | footway (w1171213926) | 27.66578, 85.42307 |
| w232099517 | Araniko Highway | trunk | 856.3 | 9 | 0 | 2 | 5.50 |  | footway (w495663549), footway (w495663573) | 27.66971, 85.41124 |
| w232434589 | Araniko Highway | trunk | 661.2 | 10 | 0 | 2 | 5.50 |  | footway (w495663573) | 27.66809, 85.41494 |
| w232434590 | Araniko Highway | trunk | 451.2 | 12 | 0 | 2 | 5.50 |  | footway (w1171213926) | 27.66581, 85.42475 |
| w232502593 | Tribhuvan Rajpath | trunk | 1170.3 | (7) | 0 | 2 | 5.50 |  | trunk (w1484959280) | 27.68843, 85.23528 |
| w232518478 | Pulchowk Road / पुल्चोक सडक | primary | 84.4 | 7 | 0 | 2 | 5.50 |  | footway (w120443300) | 27.67766, 85.31632 |
| w282864106 | – | path | 1477.2 | (1.2) | 0 | – | 5.50 | yes | Khakle Chaur Pul (w331433289) | 27.57328, 85.53459 |
| w300620125 | – | track | 270.7 | (3) | 0 | – | 5.50 | yes | Ganeshman Singh Path (w24691223) | 27.69768, 85.30187 |
| w303580912 | Chandra Binayak Marg | residential | 262 | (5) | 0 | – | 5.50 |  | footway (w1268595049) | 27.71676, 85.34534 |
| w311521602 | Melamchi Road | secondary | 2044.5 | (5.5) | 0 | – | 5.50 | yes | path (w341812309) | 27.83239, 85.57104 |
| w312110127 | – | tertiary | 33.9 | 7 | 0 | – | 5.50 | yes | Bhaktithapa Road (w111845395) | 27.69230, 85.32899 |
| w328701829 | – | service | 86.3 | (4.5) | 0 | – | 5.50 | yes | Ring Road (w185470054), Kathmandu Ringroad (w670112952), primary (w670112954) … | 27.68488, 85.30011 |
| w328701965 | – | residential | 213.2 | 5 | 0 | – | 5.50 | yes | Ring Road (w185470054), Kathmandu Ringroad (w670112952), primary (w670112954) … | 27.68438, 85.29902 |
| w341437031 | – | service | 579.2 | 7 | 0 | – | 5.50 | yes | Araniko Highway (w188435249), Araniko Highway (w194607858), service (w1536588021) | 27.67366, 85.40221 |
| w341848771 | – | residential | 628.1 | (5) | 0 | – | 5.50 | yes | Nepaltar Bridge (w196261764) | 27.74129, 85.30031 |
| w341906410 | – | path | 197.1 | (1.2) | 0 | – | 5.50 | yes | path (w341906416) | 27.67134, 85.39455 |
| w341906412 | – | path | 217 | (1.5) | 0 | – | 5.50 | yes | Thimi Gamcha Tarkhal Pataletar Road (w1359805624) | 27.67064, 85.38662 |
| w342342845 | – | path | 178 | (1.5) | 0 | – | 7.86 |  | path (w341812309) | 27.83343, 85.57455 |
| w342998885 | – | primary | 711 | 6 | 0 | 2 | 5.50 |  | footway (w1175398049) | 27.66486, 85.33053 |
| w344778538 | – | path | 322.8 | (1.2) | 0 | – | 5.50 |  | unclassified (w1023025918) | 27.82742, 85.47755 |
| w351926855 | Durbar Marg / दरबार मार्ग | primary | 250.3 | 14 | 0 | – | 5.50 |  | Mahankal Marg (w351926854) | 27.70352, 85.31638 |
| w357506892 | Araniko Highway | trunk | 1576.7 | 8 | 0 | 2 | 36.65 |  | footway (w1467493658) | 27.64287, 85.47015 |
| w416294069 | Ring Road / काठमाडाैँ चक्रपथ | trunk | 367.2 | 7 | 0 | 2 | 5.50 |  | footway (w416294072), footway (w1090811377) | 27.67714, 85.34698 |
| w422272357 | – | primary | 611.2 | 11 | 0 | – | 5.50 |  | footway (w112449582) | 27.69812, 85.31337 |
| w422272360 | Kanti Path | primary | 57.1 | 8 | 0 | – | 5.50 |  | footway (w112449582) | 27.69967, 85.31341 |
| w435253159 | – | tertiary | 868.8 | 8 | 0 | 2 | 5.50 |  | path (w1019961179) | 27.68782, 85.33694 |
| w438377936 | – | service | 104.2 | (4) | 0 | – | 5.50 |  | footway (w432547662) | 27.67467, 85.35992 |
| w439996379 | Pulchowk Road / पुल्चोक रोड | primary | 111.7 | 7 | 0 | 2 | 5.50 |  | footway (w120443300) | 27.67769, 85.31624 |
| w575210055 | – | unclassified | 65.8 | (3.5) | 0 | – | 14.73 |  | footway (w1467493658) | 27.64302, 85.47401 |
| w648697035 | Kathmandu Ringroad / काठमाडाैँ चक्रपथ | trunk | 862 | 7 | 0 | 2 | 5.50 |  | footway (w416294072), footway (w1090811377) | 27.67595, 85.34504 |
| w649457745 | – | unclassified | 67.6 | (4) | 0 | – | 5.50 | yes | Madan Bhandari Path (w27034031), Madan Bhandari Path (w225456438), footway (w1091006164) … | 27.68660, 85.34356 |
| w653848641 | Ring Road / काठमाडाैँ चक्रपथ | trunk | 41.6 | 7 | -1 | 2 | 0.00 | yes |  | 27.69424, 85.28157 |
| w670112944 | – | primary | 818.5 | 7 | 0 | 2 | 5.50 |  | footway (w416294072), footway (w1090811377) | 27.67488, 85.34390 |
| w670112951 | – | primary | 1144.2 | 7 | 0 | 2 | 5.50 |  | footway (w1091000047) | 27.67022, 85.33838 |
| w672194957 | Kanti Path / कान्ति पथ | primary | 49.8 | 7 | 0 | 1 | 5.50 | yes | footway (w112116410) | 27.70877, 85.31453 |
| w696986540 | Kanti Path / कान्ति पथ | primary | 44.2 | 14 | 0 | – | 5.50 |  | footway (w112449587) | 27.70054, 85.31350 |
| w704143313 | – | primary | 301.2 | 6 | 0 | – | 5.50 |  | footway (w1082033794) | 27.66712, 85.30750 |
| w709760710 | – | tertiary | 671.6 | 8 | 0 | – | 5.50 |  | path (w1019961179) | 27.68932, 85.33136 |
| w751465485 | Bagmati Corridor Yela Marga | secondary | 1506 | 7 | 0 | 2 | 5.50 | yes | Bagmati Bridge (w52916461), Bagmati Bridge (w136448419) | 27.68630, 85.32257 |
| w754991649 | – | residential | 259.6 | 7 | 0 | – | 5.50 | yes | Bagmati Bridge (w52916461), Bagmati Bridge (w136448419) | 27.69100, 85.31566 |
| w762800885 | – | footway | 1483.6 | (2) | 0 | – | 5.50 | yes | Bagmati (w1540981400) | 27.71172, 85.36099 |
| w891858753 | – | tertiary | 55.1 | 7 | 0 | 1 | 5.50 | yes | Ratopul (w111965835) | 27.70780, 85.33691 |
| w891858754 | – | tertiary | 68.7 | 7 | 0 | 2 | 5.50 | yes | Ratopul (w111965835) | 27.70792, 85.33669 |
| w901202690 | – | residential | 25.6 | (5) | 0 | – | 5.50 | yes | Sankhamul Pul (w112785201) | 27.68068, 85.33090 |
| w920068550 | – | tertiary | 59.9 | (7) | 0 | – | 5.50 | yes | Setopul (w111965837) | 27.70299, 85.33595 |
| w920068551 | – | tertiary | 82.3 | (7) | 0 | – | 5.50 | yes | Setopul (w111965837) | 27.70291, 85.33618 |
| w922785831 | Nagdhunga Tunnel / नागढुंगा सुरुङमार्ग | trunk | 2679.6 | (14) | -1 | 2 | 5.50 | yes | Tribhuvan Rajpath (w188865973), tertiary (w301435532), residential (w305847706) … | 27.70487, 85.21175 |
| w922870589 | – | secondary | 48 | 7 | -1 | – | 5.50 | yes | Ring Road (w345052247), primary (w670112943), Kathmandu Ringroad (w670112945) … | 27.67262, 85.34171 |
| w968701839 | – | unclassified | 162 | (5) | 0 | – | 5.50 | yes | Araniko Highway (w185402774), Araniko Highway (w194607857), service (w1275177652) … | 27.67544, 85.35498 |
| w987347844 | – | tertiary | 44.6 | 7 | 0 | – | 5.50 | yes | Bhatkeko Pul (w39985979) | 27.71824, 85.33993 |
| w1004300040 | – | tertiary | 48.8 | 7 | 0 | – | 5.50 | yes | Ring Road (w180373624) | 27.72194, 85.34582 |
| w1004854746 | RUDRAMATI MARGA -2 | tertiary | 57.2 | 6 | 0 | 1 | 5.50 | yes | Kalopul (w191859859) | 27.71159, 85.33754 |
| w1021895715 | – | unclassified | 1168.6 | (3.5) | 0 | – | 18.09 |  | path (w1011615991) | 27.81106, 85.57754 |
| w1042964628 | – | tertiary | 51.2 | 7 | 0 | – | 5.50 | yes | Kalopul (w191859859) | 27.71160, 85.33720 |
| w1047537056 | – | secondary | 89.3 | (7) | 0 | – | 5.50 | yes | primary (w27034168) | 27.72208, 85.38275 |
| w1065100935 | – | tertiary | 38.9 | 7 | 0 | – | 5.50 | yes | Bhaktithapa Road (w111845395) | 27.69268, 85.32893 |
| w1071901893 | Kathmandu Ringroad / काठमाडाैँ चक्रपथ | trunk | 36.6 | 12 | 0 | 2 | 5.50 |  | footway (w1268595049) | 27.71696, 85.34650 |
| w1071913169 | Kathmandu Ringroad / काठमाडाैँ चक्रपथ | trunk | 44.3 | 12 | 0 | 2 | 5.50 |  | footway (w1268595049) | 27.71688, 85.34659 |
| w1080177870 | Araniko Highway | trunk | 59.4 | 9 | 0 | 2 | 5.50 |  | pedestrian (w215470949) | 27.67444, 85.36456 |
| w1080177873 | Araniko Highway | trunk | 794.9 | 9 | 0 | 2 | 5.50 |  | pedestrian (w188440142) | 27.67433, 85.36812 |
| w1090811373 | – | primary | 1148.8 | 7 | 0 | 2 | 5.50 |  | footway (w1091000047) | 27.67034, 85.33811 |
| w1090811380 | – | footway | 502.7 | (2) | 0 | – | 5.50 |  | footway (w416294072) | 27.67662, 85.34646 |
| w1090820447 | Ring Road / काठमाडाैँ चक्रपथ | trunk | 79.8 | 7 | -1 | 2 | 5.50 | yes | Kalanki (w1090820448), footway (w1178138621) | 27.69363, 85.28161 |
| w1090820451 | Ring Road / काठमाडाैँ चक्रपथ | trunk | 175 | 7 | -1 | 2 | 5.50 | yes | Kalanki (w1090820448), footway (w1178138621) | 27.69304, 85.28176 |
| w1090992808 | Bhaktithapa Road / भक्तिथापा रोड | tertiary | 7.7 | 3.5 | 0 | – | 5.50 |  | footway (w1268595060) | 27.69036, 85.33579 |
| w1090992823 | – | path | 230.6 | (1.5) | 0 | – | 5.50 | yes | Sankhamul Pul (w112785201) | 27.68096, 85.32955 |
| w1091658633 | – | residential | 52.7 | (5) | 0 | – | 5.50 | yes | Madan Bhandari Path (w27034031), Madan Bhandari Path (w225456438), footway (w1091006164) … | 27.68646, 85.34423 |
| w1091658641 | – | primary | 1998.1 | 6 | 0 | 2 | 5.50 |  | footway (w1075038728), footway (w1175398049) | 27.65796, 85.32363 |
| w1091658646 | – | primary | 1111.1 | 5 | 0 | 2 | 5.50 |  | footway (w1075038728) | 27.65801, 85.32309 |
| w1091658655 | Ring Road / काठमाडाैँ चक्रपथ | trunk | 308.5 | 6 | 0 | 2 | 5.50 |  | footway (w1082033794) | 27.66723, 85.30751 |
| w1091877742 | – | primary | 1770.3 | 7 | 0 | 2 | 5.50 |  | footway (w1082033794), footway (w1082033805) | 27.67390, 85.30257 |
| w1097935310 | – | tertiary | 60.2 | 7 | -1 | – | 5.50 | yes | Bijulibazar Bridge (w111846942), Bijulibazar bridge (w169339097), tertiary (w766157957) … | 27.69005, 85.32841 |
| w1099697741 | Parijat Sadak | unclassified | 480.8 | 6 | 0 | 2 | 5.50 | yes | Ring Road (w1528723556) | 27.73250, 85.30675 |
| w1125345897 | – | secondary | 124.3 | (7) | 0 | – | 5.50 | yes | Bishnumati Bridge (w248967188), Bishnumati Bridge (w1123582689) | 27.70202, 85.30203 |
| w1126627065 | – | primary | 911.2 | 7 | 0 | 2 | 5.50 |  | footway (w416294072), footway (w1090811377) | 27.67642, 85.34558 |
| w1128301224 | – | unclassified | 95.7 | (3.5) | 0 | – | 5.50 | yes | Champi Bridge (w283069932) | 27.58590, 85.31173 |
| w1132488103 | – | tertiary | 44.3 | 7 | 0 | – | 5.50 | yes | Bhatkeko Pul (w39985979) | 27.71827, 85.33956 |
| w1132545087 | Bagmati Corridor / बागमती कोरिडोर | secondary | 613.5 | 7 | 0 | 2 | 5.50 | yes | Sankhamul Pul (w112785201) | 27.68413, 85.32868 |
| w1147905518 | – | tertiary | 79.1 | 7 | 0 | – | 5.50 | yes | Ring Road (w180373624) | 27.72210, 85.34547 |
| w1187978944 | – | residential | 245.8 | (5) | 0 | – | 5.50 | yes | residential (w1136476677) | 27.68415, 85.26118 |
| w1189331427 | – | track | 25.6 | (3) | -1 | – | 5.50 | yes | trunk (w1484959282) | 27.69691, 85.23167 |
| w1189360898 | – | unclassified | 16.9 | (5) | -1 | – | 5.50 | yes | trunk (w1189331422) | 27.69526, 85.23456 |
| w1192785914 | – | trunk | 233.2 | (9) | 0 | – | 5.50 | yes | tertiary (w1548907287) | 27.70074, 85.22440 |
| w1198619516 | – | secondary | 63.6 | (7) | 0 | – | 5.38 | yes | Bishnumati Bridge (w248967188), Bishnumati Bridge (w1123582689) | 27.70178, 85.30266 |
| w1214231626 | – | secondary | 47.6 | (7) | 0 | – | 5.50 | yes | Dallu bridge (w27033738) | 27.70941, 85.30244 |
| w1275177651 | – | residential | 46.6 | 7 | 0 | – | 5.50 | yes | Araniko Highway (w185402774), Araniko Highway (w194607857), service (w1275177652) … | 27.67529, 85.35556 |
| w1287017671 | – | path | 231.1 | (1.5) | 0 | – | 5.50 |  | footway (w924354499) | 27.63003, 85.32276 |
| w1353311680 | – | service | 172.2 | (4.5) | 0 | – | 5.50 | yes | primary (w194610100) | 27.68657, 85.36467 |
| w1354585618 | – | service | 101.6 | (4) | 0 | – | 5.67 |  | pedestrian (w1073749287) | 27.67389, 85.37261 |
| w1356913094 | – | footway | 968.2 | (2) | 0 | – | 5.50 | yes | Kalo pool (w52782410), footway (w225466624), tertiary (w1122233366) | 27.69264, 85.30542 |
| w1356913095 | – | service | 208.8 | (4.5) | 0 | – | 5.50 | yes | unclassified (w136617990) | 27.69217, 85.30977 |
| w1363281971 | – | service | 95.7 | (4.5) | 0 | – | 5.50 |  | footway (w495663573) | 27.66708, 85.41666 |
| w1367405975 | – | service | 22.4 | 7 | 0 | – | 5.50 | yes | Bijeshwori Bridge (w24691221) | 27.71372, 85.30175 |
| w1368711450 | – | footway | 1459.1 | (2) | 0 | – | 5.50 | yes | tertiary (w185408067) | 27.71080, 85.36842 |
| w1369173721 | – | service | 2289.8 | (4.5) | 0 | – | 5.50 | yes | primary (w27034168), shiva chwok pul (w204259216), residential (w1005237325) | 27.72032, 85.38221 |
| w1369517073 | – | service | 71.5 | (4) | 0 | – | 5.50 | yes | primary (w27034168) | 27.72167, 85.38208 |
| w1396844863 | – | footway | 15.7 | (2) | -1 | – | 5.50 | yes | Kanti Path (w693606874), Ratna Park Path (w693606879) | 27.70674, 85.31412 |
| w1396844864 | – | footway | 6.2 | (2) | -1 | – | 0.00 | yes |  | 27.70673, 85.31428 |
| w1426160995 | Ring Road / काठमाडाैँ चक्रपथ | trunk | 483.9 | 7 | 0 | 2 | 5.50 |  | footway (w1091000047) | 27.67132, 85.34011 |
| w1426160996 | Kathmandu Ringroad / काठमाडाैँ चक्रपथ | trunk | 482.1 | 6 | 0 | 2 | 5.50 |  | footway (w1091000047) | 27.67143, 85.34011 |
| w1427620615 | – | residential | 16.8 | (5) | 0 | – | 4.82 | yes | Balkumari-Balkot Road (w178915837) | 27.66438, 85.36309 |
| w1484959277 | – | residential | 289.5 | (5) | 0 | – | 5.50 |  | trunk (w1499708786) | 27.69142, 85.23641 |
| w1486013075 | – | service | 535.5 | (3.5) | 0 | – | 5.50 |  | trunk (w1484959282) | 27.69678, 85.23135 |
| w1536496259 | – | service | 187.5 | (4.5) | 0 | – | 5.50 | yes | trunk (w1189331422) | 27.69561, 85.23471 |
| w1562246729 | – | secondary | 155.1 | (7) | 0 | – | 5.50 | yes | Bishnumati Bridge (w248967188), Bishnumati Bridge (w1123582689) | 27.70162, 85.30258 |

## 8. Bridges over no water or road

| OSM | name | class | length | width | layer | lanes | at |
|---|---|---|---:|---:|---:|---:|---|
| w44385120 | – | footway | 29 | (1.5) | 1 | – | 27.60506, 85.26383 |
| w169116990 | – | path | 79.3 | (1.5) | 1 | – | 27.70781, 85.31530 |
| w183827055 | Ring Road / काठमाडाैँ चक्रपथ | trunk | 657.8 | 7 | 1 | 2 | 27.66730, 85.33286 |
| w187165591 | Pabitranagar Marg | residential | 8.4 | (5) | 1 | – | 27.72855, 85.31222 |
| w187603789 | Susangam Marg | residential | 4.1 | (5) | 1 | – | 27.72869, 85.31081 |
| w193705374 | – | secondary | 24.9 | 10 | 1 | – | 27.69700, 85.31910 |
| w195254869 | Bhadrakali Marg | service | 20.3 | 6 | 1 | – | 27.69602, 85.31873 |
| w199009953 | – | steps | 10 | (2) | 1 | – | 27.74162, 85.33427 |
| w225426572 | Exhibition Road / प्रदर्शनी मार्ग | secondary | 25.2 | 9 | 1 | – | 27.70178, 85.32113 |
| w225426574 | Prithvi Path / पृथ्वी पथ | primary | 23.4 | 4 | 1 | 2 | 27.69878, 85.31977 |
| w225426578 | Adwait Marg | residential | 12 | (5) | 1 | – | 27.70315, 85.32159 |
| w225426579 | Prithvi Path / पृथ्वी मार्ग | primary | 23.3 | 9 | 1 | – | 27.69881, 85.32001 |
| w233337375 | – | residential | 6.4 | (3.5) | 1 | – | 27.66098, 85.18172 |
| w244931721 | – | footway | 7.7 | (2) | 1 | – | 27.71951, 85.32558 |
| w268230112 | – | residential | 19.2 | 8 | 1 | – | 27.69861, 85.31972 |
| w268851405 | – | footway | 18.1 | 2.5 | 1 | – | 27.70174, 85.32105 |
| w331433292 | – | path | 16.7 | (1.2) | 1 | – | 27.58392, 85.52478 |
| w332680090 | – | track | 41.4 | (3) | 1 | – | 27.71698, 85.47263 |
| w338814312 | – | unclassified | 13.4 | (3.5) | 1 | – | 27.73942, 85.49928 |
| w340449267 | – | tertiary | 4.1 | (4.5) | 1 | – | 27.66136, 85.55928 |
| w341325156 | – | footway | 15.1 | (2) | 1 | – | 27.57438, 85.31293 |
| w341779484 | – | footway | 31.7 | (1.5) | 1 | – | 27.73914, 85.18611 |
| w341910397 | Godawari - Bisankhunarayan Road | tertiary | 13.9 | (6) | 1 | – | 27.59880, 85.37690 |
| w342554422 | – | residential | 20.8 | (5) | 1 | – | 27.71166, 85.25838 |
| w342896706 | – | unclassified | 22.4 | (3.5) | 1 | – | 27.68382, 85.47307 |
| w343320701 | – | track | 11.5 | (3) | 1 | – | 27.60473, 85.43388 |
| w343594955 | – | path | 17.1 | (1.2) | 1 | – | 27.70314, 85.39569 |
| w344769567 | – | service | 12.9 | (4.5) | 1 | – | 27.65690, 85.52268 |
| w347175554 | – | path | 23 | (1.2) | 1 | – | 27.61764, 85.29108 |
| w355551312 | – | unclassified | 12.4 | (5) | 1 | – | 27.69119, 85.46837 |
| w445851503 | – | path | 13.2 | (1.2) | 1 | – | 27.69521, 85.38642 |
| w466341419 | – | bridleway | 27 | (1.2) | 1 | – | 27.68296, 85.48566 |
| w499586460 | – | unclassified | 10.1 | (5) | 1 | – | 27.60842, 85.37413 |
| w500748565 | – | residential | 13.8 | (5) | 1 | – | 27.75913, 85.30385 |
| w500748575 | – | path | 3.3 | (1.5) | 1 | – | 27.76198, 85.30265 |
| w527323431 | – | residential | 5.3 | (5) | 1 | – | 27.64613, 85.27622 |
| w527323435 | – | residential | 8.9 | (5) | 1 | – | 27.64641, 85.27676 |
| w527326166 | – | path | 4.5 | (1.5) | 1 | – | 27.64655, 85.27700 |
| w527326167 | – | path | 5.5 | (1.5) | 1 | – | 27.64658, 85.27716 |
| w639502773 | – | unclassified | 17.7 | (3.5) | 1 | – | 27.59686, 85.38554 |
| w866869260 | Mahadev Khola Bridge | unclassified | 20.8 | (3.5) | 1 | – | 27.64643, 85.40926 |
| w916910706 | Chautara Bridge | residential | 5.7 | (5) | 1 | – | 27.78062, 85.37163 |
| w938038663 | TaakhelTaukhel Road / टाखेल तौखेल सडक | unclassified | 13.1 | 4 | 1 | – | 27.59174, 85.32787 |
| w954517481 | Bhajya Pukhu Bridge | footway | 44.6 | (2) | 1 | – | 27.67059, 85.42093 |
| w1033780078 | Panauti-Bansdol Road | residential | 11.6 | (5) | 1 | – | 27.60452, 85.49788 |
| w1056138249 | Dulikhel Hospital Road | residential | 11.5 | (3.5) | 1 | – | 27.61865, 85.54402 |
| w1072030502 | – | residential | 31.9 | (3.5) | 1 | – | 27.77550, 85.22957 |
| w1090392738 | Nilo Pul, Rudramati | tertiary | 9.8 | (7) | 1 | – | 27.77891, 85.36355 |
| w1125334143 | – | tertiary | 20.1 | (4.5) | 1 | – | 27.77642, 85.38731 |
| w1125334144 | Himalaya M. | tertiary | 17.9 | (4.5) | 1 | – | 27.64124, 85.52722 |
| w1154258000 | – | path | 126.6 | (1.5) | 1 | – | 27.63070, 85.30240 |
| w1167867841 | – | residential | 0.8 | (3.5) | 1 | – | 27.68713, 85.29297 |
| w1175542353 | – | footway | 7.6 | (2) | 1 | – | 27.70842, 85.27020 |
| w1186698316 | Bishnumati Pool | secondary | 27 | (7) | 1 | – | 27.78163, 85.35600 |
| w1255593536 | Basnetar-Balaju | tertiary | 6.9 | 8 | 1 | – | 27.74655, 85.29932 |
| w1320229165 | Thimi Gamcha Tarkhal Pataletar Road | tertiary | 12.8 | (6) | 1 | 2 | 27.66687, 85.38860 |
| w1353374751 | Satdobato-Godawari Road | tertiary | 83.7 | 11 | 1 | – | 27.60197, 85.36671 |
| w1360827813 | Pharsingtar-Hindunagar Road / फर्सिङ्गतार-हिन्दुनगर रोड | tertiary | 12.2 | (4.5) | 1 | – | 27.68520, 85.40325 |
| w1377954695 | bridge | unclassified | 22.6 | (3.5) | 1 | – | 27.63740, 85.39203 |
| w1385714830 | – | footway | 20.9 | 1 | 1 | – | 27.72359, 85.49714 |
| w1422563818 | – | footway | 3.8 | (1.5) | 1 | – | 27.79736, 85.32465 |
| w1422563821 | – | path | 6.6 | (1.2) | 1 | – | 27.79683, 85.32453 |
| w1423631554 | – | residential | 12.6 | (5) | 1 | – | 27.64890, 85.38484 |
| w1426160994 | Ring Road / काठमाडाैँ चक्रपथ | trunk | 84.4 | 6 | 1 | 2 | 27.66351, 85.32963 |
| w1429949284 | – | footway | 5.8 | (2) | 1 | – | 27.77557, 85.36600 |
| w1484959278 | – | trunk | 277.7 | (7) | 1 | – | 27.68922, 85.23616 |
| w1485439341 | – | residential | 6.9 | (5) | 1 | – | 27.63181, 85.49371 |
| w1554440545 | – | residential | 3.7 | (3.5) | 1 | – | 27.75414, 85.29859 |
| w1554440550 | – | residential | 6.7 | (3.5) | 1 | – | 27.75525, 85.29788 |

## 9. Tunnels and fords

| kind | OSM | name | class | length | width | layer | lanes | at |
|---|---|---|---|---:|---:|---:|---:|---|
| ford | w185470052 | Bhuwaneshwari Marg | service | 174.3 | (4.5) | 0 | – | 27.68868, 85.29027 |
| ford | w209432221 | Khatri Colony Road | residential | 294.8 | (3.5) | 0 | 2 | 27.75560, 85.33838 |
| ford | w302844673 | – | residential | 123.4 | (5) | 0 | – | 27.69022, 85.34647 |
| ford | w315651317 | – | footway | 19.3 | 2 | 0 | – | 27.71641, 85.34178 |
| ford | w329410846 | Nhulaan | footway | 78.9 | 1.5 | 0 | – | 27.67669, 85.32539 |
| ford | w329411221 | Nhulann | footway | 83.8 | (2) | 0 | – | 27.67676, 85.32513 |
| ford | w330885206 | KRMC Internal Road | residential | 39.9 | (5) | 0 | – | 27.66954, 85.30485 |
| ford | w340455877 | – | unclassified | 33.7 | (3.5) | 0 | – | 27.65153, 85.56913 |
| ford | w340533807 | – | unclassified | 7.8 | (5) | 0 | – | 27.83279, 85.57507 |
| ford | w340656643 | – | track | 1184 | (3) | 0 | – | 27.56226, 85.46467 |
| ford | w340713997 | – | unclassified | 36.7 | (3.5) | 0 | – | 27.77497, 85.21878 |
| ford | w341528390 | – | path | 4.1 | (1.2) | 0 | – | 27.75923, 85.50495 |
| ford | w342359421 | – | track | 38.2 | (3) | 0 | – | 27.74887, 85.57542 |
| ford | w343320698 | – | unclassified | 17.5 | (3.5) | 0 | – | 27.59668, 85.45044 |
| ford | w343719394 | – | service | 22.5 | (3.5) | 0 | – | 27.59381, 85.31435 |
| ford | w343719395 | – | service | 15.2 | (3.5) | 0 | – | 27.60101, 85.31766 |
| ford | w443941549 | Shortcut to Dawadi Village | footway | 93.9 | 1 | 0 | – | 27.69906, 85.25777 |
| ford | w905364877 | – | track | 43 | (3) | 0 | – | 27.56951, 85.20252 |
| ford | w905364879 | – | track | 59.8 | (3) | 0 | – | 27.57002, 85.20249 |
| ford | w905364881 | – | track | 36.8 | (3) | 0 | – | 27.57402, 85.19354 |
| ford | w905364883 | – | track | 23.5 | (3) | 0 | – | 27.57524, 85.19204 |
| ford | w905370272 | – | path | 6.9 | (1.2) | 0 | – | 27.57817, 85.18117 |
| ford | w905661404 | – | track | 29.3 | (3) | 0 | – | 27.55998, 85.25033 |
| ford | w1022149002 | – | unclassified | 21 | (5) | 0 | – | 27.83297, 85.57513 |
| ford | w1022149003 | – | unclassified | 26.5 | (5) | 0 | – | 27.83272, 85.57505 |
| ford | w1178138078 | Khatri Colony Road | residential | 82.6 | (3.5) | 0 | 2 | 27.75584, 85.33743 |
| ford | w1178138079 | Khatri Colony Road | residential | 116.8 | (3.5) | 0 | 2 | 27.75634, 85.33792 |
| ford | w1178138080 | Khatri Colony Road | residential | 59.9 | (3.5) | 0 | 2 | 27.75689, 85.33822 |
| ford | w1178138081 | Khatri Colony Road | residential | 38.2 | (5) | 0 | 2 | 27.75456, 85.33801 |
| ford | w1179808514 | – | path | 10.8 | (1.5) | 1 | – | 27.74518, 85.31804 |
| ford | w1333613562 | – | track | 216.1 | (3) | 0 | – | 27.56442, 85.46412 |
| ford | w1467515142 | – | service | 1 | (4.5) | 0 | – | 27.69133, 85.31661 |
| ford | w1518655703 | – | residential | 10.4 | (5) | 0 | – | 27.66982, 85.30522 |
| tunnel | w680355305 | – | service | 32 | (4.5) | -1 | – | 27.71472, 85.31859 |
| tunnel | w1287631569 | – | service | 32.1 | (4) | -1 | – | 27.73600, 85.32323 |
| tunnel | w1336880029 | – | service | 3.6 | (4.5) | -1 | – | 27.78044, 85.35045 |
| tunnel | w1432220585 | – | footway | 11.5 | (2) | -1 | – | 27.70458, 85.30645 |
| tunnel | w1432220587 | – | footway | 8.7 | (2) | -1 | – | 27.70455, 85.30664 |

## 10. Passages under buildings

Ways that run under a building that stays whole (the dhoka passages into the bahal courtyards of the old towns, covered and indoor ways, short tunnels). The corridor never trims the house; the buildings package meshes a gateway with the free height listed (`RoadClearance.MinOverheadClearanceM` 4.5 m, or the tagged `building:min_height`).

| OSM | name | class | length | width | layer | lanes | clearance m | tunnel | at |
|---|---|---|---:|---:|---:|---:|---:|---|---|
| w24683724 | Z Marg | pedestrian | 26.9 | (4) | 0 | – | 4.50 | – | 27.71683, 85.30912 |
| w58753045 | – | service | 218.5 | (4) | 0 | – | 4.50 | – | 27.70062, 85.29055 |
| w183827060 | – | service | 12.6 | (4.5) | 0 | – | 4.50 | – | 27.67150, 85.41791 |
| w200399692 | Chi Baha Galli | footway | 3.3 | (2) | 0 | – | 4.50 | – | 27.71726, 85.33615 |
| w200904705 | Janak Marga | residential | 11.1 | (5) | 0 | – | 4.50 | – | 27.70815, 85.32878 |
| w230310386 | – | service | 7.1 | (3.5) | 0 | – | 4.50 | – | 27.59369, 85.51816 |
| w239406149 | – | footway | 6.9 | (2) | 0 | – | 4.50 | building_passage | 27.71590, 85.33413 |
| w239432502 | – | footway | 5.4 | (2) | 0 | – | 4.50 | building_passage | 27.71597, 85.33544 |
| w243772181 | – | footway | 5.8 | (2) | 0 | – | 4.50 | building_passage | 27.71688, 85.33570 |
| w243772182 | – | footway | 2.7 | (2) | 0 | – | 4.50 | building_passage | 27.71648, 85.33545 |
| w243772183 | – | footway | 5.1 | (2) | 0 | – | 4.50 | building_passage | 27.71667, 85.33562 |
| w245214524 | – | pedestrian | 126.6 | 4 | 0 | – | 4.50 | – | 27.66713, 85.32126 |
| w340661283 | – | path | 15.9 | (1.5) | 0 | – | 4.50 | yes | 27.67564, 85.32431 |
| w340661439 | – | path | 16.1 | (1.5) | 0 | – | 4.50 | yes | 27.67521, 85.32419 |
| w341635674 | – | footway | 12.7 | (2) | 0 | – | 4.50 | yes | 27.72109, 85.31989 |
| w344235389 | – | service | 12.9 | (4.5) | 0 | – | 4.50 | – | 27.67139, 85.41780 |
| w344348521 | – | service | 16 | (4.5) | 0 | – | 4.50 | – | 27.69847, 85.32176 |
| w357506891 | Arniko Raj Marga | trunk | 11.4 | 8 | 0 | – | 4.50 | – | 27.64195, 85.47730 |
| w435437634 | – | footway | 28.6 | (2) | 0 | – | 4.50 | building_passage | 27.67627, 85.32272 |
| w439041647 | Mahabuddha Temple | path | 29.3 | (1.5) | 0 | – | 4.50 | – | 27.66890, 85.32728 |
| w452276356 | – | service | 4 | (4.5) | 0 | – | 4.50 | – | 27.74271, 85.36510 |
| w453993770 | – | path | 8.7 | (1.5) | 0 | – | 4.50 | – | 27.71523, 85.28516 |
| w455191162 | – | service | 20.9 | 6 | 0 | – | 4.50 | – | 27.67869, 85.31716 |
| w456517084 | – | footway | 8.7 | (2) | 0 | – | 4.50 | – | 27.58464, 85.51476 |
| w456529760 | – | unclassified | 4.2 | (4) | 0 | – | 4.50 | – | 27.72567, 85.28865 |
| w483151656 | – | footway | 10.1 | (2) | 0 | – | 4.50 | building_passage | 27.67479, 85.32274 |
| w510398039 | – | path | 8.7 | (1.5) | 0 | – | 4.50 | building_passage | 27.67450, 85.32467 |
| w566029395 | – | footway | 17.6 | (2) | 0 | – | 4.50 | building_passage | 27.67439, 85.32683 |
| w653003639 | Summit Hotel | footway | 14.9 | (2) | 0 | – | 4.50 | yes | 27.68535, 85.31162 |
| w757109190 | – | unclassified | 3.7 | (4) | 0 | – | 4.50 | – | 27.70116, 85.35391 |
| w933125094 | Seto Dhoka Marg | service | 8 | (4) | 0 | – | 4.50 | yes | 27.70933, 85.31645 |
| w975927668 | – | service | 4.7 | (4) | 0 | – | 4.50 | – | 27.73550, 85.33097 |
| w1033824178 | – | unclassified | 3.7 | (4) | 0 | – | 4.50 | – | 27.70110, 85.35390 |
| w1034704796 | – | footway | 262.8 | (2) | 0 | – | 4.50 | – | 27.69864, 85.35552 |
| w1035929654 | – | footway | 9.8 | (2) | 0 | – | 4.50 | – | 27.69921, 85.35599 |
| w1037350575 | – | path | 10.9 | (1.5) | 0 | – | 4.50 | – | 27.70006, 85.35462 |
| w1087173550 | Tharlam Gompa road | residential | 9.7 | (5) | 0 | – | 4.50 | – | 27.72309, 85.36454 |
| w1092549593 | – | service | 6.8 | (4) | 0 | – | 4.50 | – | 27.67256, 85.30648 |
| w1093758728 | – | service | 15.8 | (4) | 0 | – | 4.50 | – | 27.72257, 85.38740 |
| w1102576017 | – | service | 12 | (4) | 0 | – | 4.50 | – | 27.71125, 85.29063 |
| w1126674715 | गोठाटार मार्ग | residential | 82.5 | (5) | 0 | – | 4.50 | – | 27.70553, 85.37160 |
| w1134476351 | – | service | 8.2 | (4) | 0 | – | 4.50 | – | 27.71311, 85.31562 |
| w1134476353 | – | service | 5.2 | (4) | 0 | – | 4.50 | – | 27.71320, 85.31561 |
| w1141938399 | – | service | 8.4 | (4.5) | 0 | – | 4.50 | – | 27.66545, 85.36020 |
| w1152853630 | – | service | 11.8 | (4) | 0 | – | 4.50 | – | 27.70891, 85.32999 |
| w1153183815 | – | service | 15.1 | (4) | 0 | – | 4.50 | – | 27.69862, 85.29991 |
| w1173209390 | – | service | 18.6 | 4 | 0 | – | 4.50 | – | 27.69773, 85.31658 |
| w1181706172 | भौमाल मार्ग | residential | 296.3 | (5) | 0 | – | 4.50 | – | 27.70584, 85.37564 |
| w1181706173 | भौमाल मार्ग | residential | 56.3 | (5) | 0 | – | 4.50 | – | 27.70671, 85.37616 |
| w1184384050 | भौमाल मार्ग | residential | 29.7 | (5) | 0 | – | 4.50 | – | 27.70607, 85.37507 |
| w1184384051 | गोठाटार मार्ग | residential | 26.4 | (5) | 0 | – | 4.50 | – | 27.70583, 85.37104 |
| w1184384053 | गोठाटार मार्ग | residential | 51.9 | (5) | 0 | – | 4.50 | – | 27.70559, 85.37145 |
| w1184908463 | – | service | 5.7 | (4) | 0 | – | 4.50 | – | 27.71315, 85.32373 |
| w1185059542 | – | service | 25.3 | (4) | 0 | – | 4.50 | – | 27.73616, 85.32288 |
| w1189304043 | – | path | 8.6 | (1.5) | 0 | – | 4.50 | building_passage | 27.70824, 85.30772 |
| w1190298816 | – | path | 7.4 | (1.5) | 0 | – | 4.50 | building_passage | 27.70680, 85.30893 |
| w1190298829 | – | path | 9.3 | (1.5) | 0 | – | 4.50 | building_passage | 27.70738, 85.30740 |
| w1190302050 | – | path | 5.7 | (1.5) | 0 | – | 4.50 | building_passage | 27.70756, 85.31148 |
| w1190302058 | – | path | 4.9 | (1.5) | 0 | – | 4.50 | building_passage | 27.70750, 85.31146 |
| w1190302060 | – | path | 10.4 | (1.5) | 0 | – | 4.50 | building_passage | 27.70718, 85.31126 |
| w1190302062 | – | path | 13 | (1.5) | 0 | – | 4.50 | building_passage | 27.70704, 85.31123 |
| w1190302064 | – | path | 4.3 | (1.5) | 0 | – | 4.50 | building_passage | 27.70743, 85.31102 |
| w1190302067 | – | path | 6.8 | (1.5) | 0 | – | 4.50 | building_passage | 27.70790, 85.31013 |
| w1190302073 | सुचिका गल्ली | path | 8.4 | (1.5) | 0 | – | 4.50 | building_passage | 27.70753, 85.31064 |
| w1190302079 | – | path | 6.4 | (1.5) | 0 | – | 4.50 | building_passage | 27.70702, 85.31108 |
| w1190302082 | – | path | 5.5 | (1.5) | 0 | – | 4.50 | building_passage | 27.70736, 85.31088 |
| w1190302084 | – | path | 7.3 | (1.5) | 0 | – | 4.50 | building_passage | 27.70726, 85.31085 |
| w1190302087 | – | path | 4.4 | (1.5) | 0 | – | 4.50 | building_passage | 27.70700, 85.31081 |
| w1190302088 | सुचिका गल्ली | path | 8.4 | (1.5) | 0 | – | 4.50 | building_passage | 27.70743, 85.31082 |
| w1190302089 | – | path | 13.8 | (1.5) | 0 | – | 4.50 | building_passage | 27.70703, 85.31062 |
| w1190302091 | – | path | 3.6 | (1.5) | 0 | – | 4.50 | building_passage | 27.70726, 85.31064 |
| w1190302095 | – | path | 6.3 | (1.5) | 0 | – | 4.50 | building_passage | 27.70688, 85.31071 |
| w1190305338 | सुचिका गल्ली | path | 9 | (1.5) | 0 | – | 4.50 | building_passage | 27.70763, 85.31021 |
| w1190305340 | सुचिका गल्ली | path | 8.9 | (1.5) | 0 | – | 4.50 | building_passage | 27.70764, 85.31011 |
| w1190322325 | पाण्डे चुक | path | 8.6 | (1.5) | 0 | – | 4.50 | building_passage | 27.70758, 85.31105 |
| w1190322327 | योगविर सिंह मार्ग | path | 10.7 | (1.5) | 0 | – | 4.50 | building_passage | 27.70708, 85.31032 |
| w1190745702 | मासं गल्ली | path | 6.9 | (1.5) | 0 | – | 4.50 | building_passage | 27.70592, 85.31125 |
| w1190745711 | – | path | 4 | (1.5) | 0 | – | 4.50 | building_passage | 27.70453, 85.31086 |
| w1190745717 | – | path | 5.9 | (1.5) | 0 | – | 4.50 | building_passage | 27.70465, 85.31208 |
| w1190745719 | – | path | 6.5 | (1.5) | 0 | – | 4.50 | building_passage | 27.70490, 85.31214 |
| w1190954675 | बर्मु चुक | path | 7.2 | (1.5) | 0 | – | 4.50 | building_passage | 27.70735, 85.31133 |
| w1190954680 | सुचिका गल्ली | path | 11.8 | (1.5) | 0 | – | 4.50 | building_passage | 27.70740, 85.31062 |
| w1190954703 | योगविर सिंह मार्ग | path | 6.5 | (1.5) | 0 | – | 4.50 | building_passage | 27.70716, 85.31034 |
| w1190956387 | – | path | 1.9 | (1.5) | 0 | – | 4.50 | building_passage | 27.70733, 85.31136 |
| w1190956389 | सुचिका गल्ली | path | 4.7 | (1.5) | 0 | – | 4.50 | building_passage | 27.70688, 85.31058 |
| w1190956391 | – | path | 7.2 | (1.5) | 0 | – | 4.50 | building_passage | 27.70724, 85.31063 |
| w1190956397 | – | path | 3.7 | (1.5) | 0 | – | 4.50 | building_passage | 27.70468, 85.31209 |
| w1237224753 | – | service | 4.7 | (4.5) | 0 | – | 4.50 | – | 27.72607, 85.28308 |
| w1265025456 | – | service | 13 | (4.5) | 0 | – | 4.50 | – | 27.64877, 85.27808 |
| w1334273583 | – | service | 16.9 | 6 | 0 | – | 4.50 | – | 27.69880, 85.31701 |
| w1374726956 | – | service | 10.5 | (4.5) | 0 | – | 4.50 | – | 27.63723, 85.49224 |
| w1374897408 | – | path | 112.9 | (1.5) | 0 | – | 4.50 | building_passage | 27.72119, 85.36248 |
| w1429946077 | – | service | 8.6 | (4.5) | 0 | – | 4.50 | – | 27.69985, 85.32205 |
| w1429946085 | – | service | 10 | (4.5) | 0 | – | 4.50 | – | 27.69840, 85.32187 |
| w1455110148 | – | service | 5.6 | (4.5) | 0 | – | 4.50 | – | 27.68757, 85.33757 |
| w1457962505 | – | unclassified | 14 | (4) | 0 | – | 4.50 | – | 27.67696, 85.32761 |
| w1464865108 | – | service | 12.7 | (4) | 0 | – | 4.50 | – | 27.72547, 85.29864 |
| w1467207608 | – | service | 21.5 | (4.5) | 0 | – | 4.50 | yes | 27.69134, 85.31662 |
| w1555354160 | – | footway | 9.7 | (2) | 0 | – | 4.50 | – | 27.69980, 85.35636 |
| w1555354162 | – | footway | 25.1 | (2) | 0 | – | 4.50 | – | 27.70297, 85.35819 |
| w1557723852 | – | footway | 11.5 | (2) | 0 | – | 6.00 | – | 27.70103, 85.35394 |
| w1557723854 | – | footway | 11.4 | (2) | 0 | – | 6.00 | – | 27.70119, 85.35385 |
| w1562248312 | – | service | 7.9 | (4) | 0 | – | 4.50 | – | 27.68760, 85.24050 |

## 11. Decks that land on a road (no underpass)

A road whose centreline crosses a deck within 2 m (across the road) of a deck end that no other deck continues meets the bridge there: a junction mapped without a shared node, or a bank road at the bridge head. The two share one height at the crossing; no clearance applies.

| deck | road | at |
|---|---|---|
| path (w343323175) | way to sana kisan (w153233477) | 27.73842, 85.20909 |
| residential (w1005237325) | footway (w1044106114) | 27.71801, 85.38169 |
| footway (w1126606030) | service (w1396508736) | 27.67341, 85.34236 |
| footway (w1136873418) | residential (w172338701) | 27.69028, 85.32801 |
| Ropeway Sadak (w1192743662) | residential (w1525642173) | 27.72014, 85.29554 |
| footway (w1268595057) | footway (w268851407) | 27.69537, 85.32082 |
| Bagmati (w1540981400) | footway (w1368711450) | 27.71256, 85.36016 |

## 12. What decision 3 still costs

Raised motor roads in URBAN and OLD_CORE cells (more than 3 m above the terrain somewhere): approaches that must climb over a road passing under a bridge or flyover where no riverside cutting can take the height (canal and stream bridges, flyovers mapped over a lane). The solver pays a heavy penalty for every metre over 3 m, so these are the cases with no cheaper answer. Doors facing such an embankment need a retaining wall or a service lane; worth a check on site [V].

| OSM | name | class | max rise m | at |
|---|---|---|---:|---|
| w670112944 | – | primary | 8.44 | 27.67488, 85.34390 |
| w183827054 | Ring Road | trunk | 7.91 | 27.67348, 85.34229 |
| w1073726670 | Manohara Corridor | tertiary | 7.87 | 27.67343, 85.34248 |
| w670112946 | Kathmandu Ringroad | trunk | 7.18 | 27.67383, 85.34253 |
| w172651329 | Kot Devi Marg | residential | 7.17 | 27.67257, 85.34409 |
| w1156837463 | – | service | 7.11 | 27.69607, 85.36738 |
| w1126627065 | – | primary | 6.81 | 27.67642, 85.34558 |
| w231786210 | Ring Road | trunk | 6.77 | 27.67363, 85.34245 |
| w1192743667 | – | residential | 6.53 | 27.71785, 85.29627 |
| w184712508 | Nirmal Marg | residential | 6.48 | 27.71914, 85.29601 |
| w1192743665 | – | residential | 6.48 | 27.71785, 85.29628 |
| w1174194089 | – | service | 6.44 | 27.69609, 85.36710 |
| w172651228 | Devasthal Marg | residential | 6.39 | 27.67482, 85.34393 |
| w172325501 | – | residential | 6.34 | 27.71781, 85.29575 |
| w832899518 | Nirmal Marg | residential | 5.87 | 27.71632, 85.29652 |
| w196277319 | Ring Road | trunk | 5.69 | 27.67496, 85.34378 |
| w169984786 | – | residential | 5.19 | 27.69649, 85.36689 |
| w648697035 | Kathmandu Ringroad | trunk | 4.93 | 27.67595, 85.34504 |
| w112785219 | Sankhamul Road | residential | 4.55 | 27.68077, 85.33099 |
| w172137063 | Bhaktithapa Road | tertiary | 3.97 | 27.69324, 85.32747 |
| w111845394 | Bhaktithapa Road | tertiary | 3.94 | 27.69231, 85.32950 |
| w1122233365 | – | tertiary | 3.68 | 27.69112, 85.30257 |
| w136445396 | – | residential | 3.34 | 27.71882, 85.29644 |
| w136448239 | – | residential | 3.34 | 27.71795, 85.29685 |
| w172325522 | – | residential | 3.17 | 27.71811, 85.29575 |

Underpasses the valley-wide run could not bring to 5.5 m (a loop the steepest ramp cannot close; this run uses the real-width fallback for corridor widths, the region build its measured corridors) [E]:

| OSM | name | class | clearance m | under | at |
|---|---|---|---:|---|---|
| w1427620615 | – | residential | 4.82 | Balkumari-Balkot Road (w178915837) | 27.66438, 85.36309 |
| w1198619516 | – | secondary | 5.38 | Bishnumati Bridge (w248967188), Bishnumati Bridge (w1123582689) | 27.70178, 85.30266 |

Motor ramps steeper than their class grade (never steeper than 15 %, never a step): where a riverside road sinks under a bridge and meets that bridge's approach a few tens of metres away, the class grade cannot close the 6.7 m between them.

| OSM | name | class | max grade | at |
|---|---|---|---:|---|
| w4825621 | – | primary | 15.0 % | 27.72201, 85.38424 |
| w4840304 | Ring Road | trunk | 15.0 % | 27.73482, 85.31024 |
| w81146145 | Bishnumati Track Road | secondary | 15.0 % | 27.70564, 85.30167 |
| w111845394 | Bhaktithapa Road | tertiary | 15.0 % | 27.69231, 85.32950 |
| w112449586 | Bishnumati Track Road | secondary | 15.0 % | 27.70009, 85.30302 |
| w112664643 | Janata Marg | secondary | 15.0 % | 27.67424, 85.34090 |
| w112783299 | – | tertiary | 15.0 % | 27.70607, 85.33662 |
| w112783306 | Rudra Mati Marg | tertiary | 15.0 % | 27.70919, 85.33696 |
| w112783313 | Rudra Mati Marg | tertiary | 15.0 % | 27.70257, 85.33584 |
| w120430418 | Pashupati Sadak | secondary | 15.0 % | 27.70814, 85.33545 |
| w136518312 | Mangalbazar to gwarko | secondary | 15.0 % | 27.66657, 85.33225 |
| w153237894 | – | secondary | 15.0 % | 27.70950, 85.30241 |
| w172137063 | Bhaktithapa Road | tertiary | 15.0 % | 27.69324, 85.32747 |
| w172652561 | Balkumari-Balkot road | secondary | 15.0 % | 27.66443, 85.36209 |
| w183110570 | Rudramati Marg | tertiary | 15.0 % | 27.68728, 85.32730 |
| w199430771 | Bishnumati Track Road | secondary | 15.0 % | 27.70320, 85.30222 |
| w202382798 | Tankeshwor sadak | secondary | 15.0 % | 27.70116, 85.30206 |
| w230151726 | – | track | 15.0 % | 27.60939, 85.53378 |
| w248967187 | Bishnu Mati Bridge | secondary | 15.0 % | 27.70158, 85.30188 |
| w302563246 | – | service | 15.0 % | 27.73530, 85.30771 |
| w312110127 | – | tertiary | 15.0 % | 27.69230, 85.32899 |
| w343494449 | – | unclassified | 15.0 % | 27.66645, 85.36282 |
| w343494760 | – | residential | 15.0 % | 27.66434, 85.36284 |
| w358548159 | – | secondary | 15.0 % | 27.72013, 85.38276 |
| w644044950 | – | tertiary | 15.0 % | 27.70050, 85.33448 |
| w670112944 | – | primary | 15.0 % | 27.67488, 85.34390 |
| w756183474 | – | service | 15.0 % | 27.70966, 85.30241 |
| w891858754 | – | tertiary | 15.0 % | 27.70792, 85.33669 |
| w920068550 | – | tertiary | 15.0 % | 27.70299, 85.33595 |
| w920068551 | – | tertiary | 15.0 % | 27.70291, 85.33618 |
| w1047537056 | – | secondary | 15.0 % | 27.72208, 85.38275 |
| w1073726670 | Manohara Corridor | tertiary | 15.0 % | 27.67343, 85.34248 |
| w1097935309 | – | tertiary | 15.0 % | 27.68989, 85.32845 |
| w1097935310 | – | tertiary | 15.0 % | 27.69005, 85.32841 |
| w1099697741 | Parijat Sadak | unclassified | 15.0 % | 27.73250, 85.30675 |
| w1125345897 | – | secondary | 15.0 % | 27.70202, 85.30203 |
| w1198619516 | – | secondary | 15.0 % | 27.70178, 85.30266 |
| w1214231626 | – | secondary | 15.0 % | 27.70941, 85.30244 |
| w1300403947 | – | residential | 15.0 % | 27.66354, 85.36263 |
| w1396508736 | – | service | 15.0 % | 27.67119, 85.33755 |
| w1414579283 | Setopul | secondary | 15.0 % | 27.70282, 85.33619 |
| w1416264761 | Dhobikhola (धोबीखोला) Marg | tertiary | 15.0 % | 27.69233, 85.32910 |
| w1416264762 | Namuna Marg | tertiary | 15.0 % | 27.69252, 85.32864 |
| w1416264764 | Shanti Binayak Marg | tertiary | 15.0 % | 27.69244, 85.32921 |
| w1427620615 | – | residential | 15.0 % | 27.66438, 85.36309 |
| w1103057010 | – | pedestrian | 8.6 % | 27.67828, 85.32593 |

## 13. Open issues for other packages

* **roads**: tell the final corridors by `TileData.FinalCorridors` (META `"ratr_corridor": "final"`, DATA_FORMATS 1.10), not by the presence of RSTR (`RoadWidthModel.UsesFinalCorridor`). The base (stage-1) `RoadWidthModel` subtracts a further 1 m from every corridor, so on the new sample `GeneratorsPlacementTests.StreetPropsKeepOsmObjectsAndLightTheStreets` finds a lamp in a building; with w2d/roads (a4ead16) plus this package's `Core/Data` and `shared/` it passes (reproduction: `git archive w2d/roads`, copy `game/Assets/Ghumante/Core/Data`, `shared/sample-regions/kathmandu_core`, `shared/golden` and `shared/enums.json` over it, `dotnet test core-tests --filter FullyQualifiedName~StreetPropsKeepOsmObjectsAndLightTheStreets`).
* **collide**: `TrafficTests.RingRoadRunsHalfAnHourWithoutContact` turns on chaotic details of the lane graph: on this package's sample it counts 2 contacts with the base roads code and 4 with w2d/roads (0 with an earlier build of the same rules). The contacts are one case: a microbus stopped at the end of a lane (s 62.6 m) beside the Chabahil Ring Road (27.71378, 85.34561, no structure there) is overlapped by a minibus pulling away on the next lane, which the IDM does not see (different lanes). Worth a lateral-clearance check between neighbouring lanes at lane ends.
* **roads**: `RoadStructureRecord.CorridorShiftCm` is now sampled every 5 m (`ShiftSpacingM`, read it with `ShiftAtM`); the road mesher does not apply the shift yet, so a corridor beside a temple is drawn centred. Draw `PASSAGE` pieces as a gateway floor under the house (the corridor does not trim it).
* **nature**: `PropPlacement.Place` tests the building mask with the double lamp position but stores floats, and the test reads the mask at the float position; a lamp on a 0.5 m cell edge can flip cells (latent whatever the data). Round the position once and test that.
* **buildings**: mesh a gateway (free height `RoadStructureRecord.ClearanceM`, at least 4.5 m) where a `PASSAGE` piece runs under a footprint; a building a corridor cut in two is several BLDG records with the same `osm_ref` (look-ups by `osm_ref`, e.g. hero hide sets, must take all of them).
* **bridges**: use `RoadStructureRecord.DeckDepthFor(roadClass)` (foot bridges over water are foot decks too); the urban river bridges of the sample (Thapathali, Teku, Kalimati) now sit at street level and the bank roads under them run in cuttings (`LOWERED`, `APPROACH`), so sample-based expectations change; a deck that lands on a road (section 11) shares its height there.
* **collide / roads**: lowered stretches (riverside cuttings, Kalanki) run below the HGHT terrain: ground queries inside a corridor must take the RSTR surface, and the road mesher's retaining walls hide the terrain above.

## Sources

* OpenStreetMap, Nepal extract (`pipeline/data/raw/osm/nepal.osm.pbf`, see `SOURCES.lock.json`): `highway`, `bridge`, `bridge:structure`, `tunnel`, `covered`, `indoor`, `layer`, `width`, `lanes`, `waterway`, `natural=water` [O].
* The Bagmati's sand-mined channel: "sand removal has caused the river to scour a canyon in Thapathali"; in the 1950s it "flowed clean and wide at the Thapathali bridge", today it "flows down a canyon carved by sand extraction" (Nepali Times, https://nepalitimes.com/a-bagmati-park); sand mining "greatly denuded the river bed" and created "a canyon of sorts at different places" (https://nepalitimes.com/news.php?id=13742). Basis of the incised urban channel depth, whose value (4.5 m, streams 2.5 m) is an estimate [E].
* Copernicus GLO-30 DEM through the leaf `HGHT` grid for surfaces and abutments [E].
* docs/W2_DETAIL_CONTRACT.md decisions 3-5; docs/DATA_FORMATS.md 1.11 and 1.15 for the rules.
