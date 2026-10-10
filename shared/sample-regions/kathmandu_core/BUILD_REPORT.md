# Build report: kathmandu_core

* Built at 2026-10-10T15:42:05Z by pipeline 0.1.0 (data_version 2).
* bbox [85.283, 27.69, 85.375, 27.735], horizon [84.6, 27.2, 86.4, 28.6]; detail levels [8, 9, 10], horizon levels [5, 6].
* Extract from cache; 4 POI kinds set from curated landmarks, 0 landmark POIs added.
* road_km counts roads clipped to the leaf tiles (what the tiles and the routing graph hold); road_km_extract includes the extract's buffer zone.

## Files

| File | Bytes | MB | SHA-256 |
|---|---:|---:|---|
| kathmandu_core.ghpk | 11,911,873 | 11.91 | `99575b4fb6c08fde…` |
| kathmandu_core.search.ghsi | 155,776 | 0.16 | `e76829dffd0bd490…` |
| kathmandu_core.route.ghrg | 2,274,177 | 2.27 | `9310e52bfc8b55d4…` |
| kathmandu_core.transit.ghrt | 32,827 | 0.03 | `6aec529618567361…` |
| kathmandu_core.curated.ghcd | 5,949 | 0.01 | `43cc35fc37b5d588…` |
| hero_recipes.json | 18,819 | 0.02 | `51cbe65ea554733e…` |
| kathmandu_core.aviation.json | 31,856 | 0.03 | `7b7a40a0e394d75e…` |

## Tiles

| Level | Tile size | Tiles |
|---:|---:|---:|
| 5 | 32,768 m | 42 |
| 6 | 16,384 m | 132 |
| 8 | 4,096 m | 9 |
| 9 | 2,048 m | 20 |
| 10 | 1,024 m | 60 |

Bytes per chunk type (stored, after DEFLATE):

| Chunk | Bytes | Share |
|---|---:|---:|
| HGHT | 5,269,108 | 44.3% |
| BLDG | 4,850,885 | 40.7% |
| ROAD | 500,865 | 4.2% |
| BFNT | 378,846 | 3.2% |
| NAME | 281,041 | 2.4% |
| POIS | 193,587 | 1.6% |
| BIOM | 154,144 | 1.3% |
| AREA | 134,818 | 1.1% |
| RATR | 51,678 | 0.4% |
| RSTR | 18,110 | 0.2% |
| PROP | 15,278 | 0.1% |
| LINE | 12,727 | 0.1% |
| META | 12,638 | 0.1% |
| SEED | 1,246 | 0.0% |
| JNCT | 1,038 | 0.0% |

Leaf tiles: 60; buildings placed 167,944 of 219,024 extracted (the rest lie in the bbox buffer).
Feature records: {"area_parts": 1493, "bfnt_fronts": 141619, "buildings": 167944, "buildings_trimmed": 36616, "junctions": 53, "line_pieces": 168, "places": 216, "pois": 15252, "props": 1119, "ratr_corridor_samples": 79486, "road_pieces": 14371, "rstr_deck_pieces": 828}

## Content

| What | Value |
|---|---:|
| roads | 17,568 |
| road_km | 1442.9 |
| road_km_extract | 2141.912 |
| buildings | 219,024 |
| pois | 22,165 |
| places | 314 |
| areas | 1,824 |
| lines | 128 |
| admin_areas | 12 |
| search_entries | 1,213 |
| search_keys | 2,954 |
| graph_nodes | 20,832 |
| graph_edges | 52,064 |

## Wave 2 data (W2_DESIGN section 9)

* **area_types**: `{"FOREST": 4, "HILL": 88, "OLD_CORE": 76, "PERI_URBAN": 259, "RURAL": 66, "URBAN": 695}`
* **style_profiles**: `{"BOUDHA_KORA": 1038, "KATHMANDU_CORE": 10828, "KIRTIPUR": 3025, "METRO": 167486, "PATAN": 1227, "RIM": 31629, "THAMEL": 2530, "THIMI": 1261}`
* **sacred_zones**: `{"areas": 138, "by_kind": {"COMPOUND": 122, "COURTYARD": 13, "HERITAGE_SQUARE": 1, "STUPA_KORA": 2}, "flagged_areas": 138}`
* **dual**: `{"dual_ways": 165, "service_ways": 30}`
* **junctions**: `{"chowks_unmatched": ["ekantakuna", "satdobato", "mahalaxmi", "jawalakhel", "lagankhel", "jadibuti"], "mini": 3, "police": 24, "police_on_rings": 3, "records": 67, "rings": 9, "signals": 34, "synthetic_islands": 10}`
* **transit**: `{"by_mode": {"BUS": 42, "FOOT": 4, "HIKING": 2, "MICROBUS": 12, "TEMPO": 9}, "restrictions": 45, "routes": 69, "stops_from_members": 62, "stops_inferred": 535, "with_gaps": 55}`
* **heritage**: `{"compound_missing": [], "cut_buildings": 1, "hidden_buildings": 197, "no_hide_zone": ["her.ktm.hanuman_dhoka"], "not_in_extract": ["her.bkt.bhairavnath", "her.bkt.dattatreya", "her.bkt.golden_gate", "her.bkt.nyatapola", "her.bkt.vatsala_durga", "her.ptn.bhimsen", "her.ptn.golden_temple", "her.ptn.krishna_mandir", "her.ptn.manga_hiti", "her.ptn.taleju", "her.ptn.taleju_bell", "her.ptn.vishwanath", "her.ptn.yoganarendra_column"], "outside": ["her.bkt.fifty_five_window_palace", "her.bkt.peacock_window", "her.ptn.kumbheshwar"], "records": 35, "resolved": 19, "unhidden_overlaps": 4}`
* **parts**: `{"hosts": 80, "parts": 395, "parts_with_host": 388}`
* **poi_hints**: `{"buildings_with_shops": 5337, "religious_pois_footprint_kept": 26, "religious_pois_to_footprints": 143, "shop_pois_in_footprints": 8189}`
* **courtyards**: `{"courtyard_holes": 1, "courtyards_demoted": 0, "courtyards_dropped": 0}`
* **transit_file**: `{"bytes": 32827, "restrictions": 45, "routes": 69}`
* **curated_file**: `{"bytes": 5949, "records": 19}`
* **aviation**: `{"tia": {"procedures": 12, "runway": "w340948564", "threshold_distance_m": 2755.81}}`
* **chunks**: `{"chunk_bytes": {"BFNT": 378846, "JNCT": 1038, "PROP": 15278, "RATR": 51678, "RSTR": 18110}, "corridor": {"narrower_pct": 0.01, "narrower_than_tagged": 1, "open": 0, "p25_p50_p75_m": [4.8, 4.8, 5.6], "samples": 79486, "tagged_width_samples": 16821}, "fronts": {"buildings": 167944, "corners": 21114, "trimmed_for_road": 36616, "with_front": 141619}, "junction_kinds": {"CIRCULAR": 2, "MINI_ROUNDABOUT": 2, "POLICE": 10, "ROUNDABOUT": 6, "SIGNALS": 25, "SYNTHETIC_ISLAND": 8}, "props": {"AEROWAY_GATE": 12, "BUS_STOP": 134, "CROSSING_MARKED": 158, "CROSSING_UNMARKED": 225, "GATE": 154, "HELIPAD": 8, "PARKING_POSITION": 10, "STORAGE_TANK": 14, "STREET_LAMP": 168, "TAXI_STAND": 17, "TRAFFIC_SIGNALS": 27, "TREE": 190, "WINDSOCK": 2}, "structures": {"corridor_samples_below_4_8_m": 46, "deck_points": 6837, "pieces_by_kind": {"BRIDGE": 246, "FLYOVER": 19, "FORD": 4, "NONE": 14011, "TUNNEL": 5, "UNDERPASS": 86}}}`
* **routing**: 23 road pieces lost their motor modes inside sacred zones (D14).

## Detail pass (docs/W2_DETAIL_CONTRACT.md decisions 1, 3-5)

* **structures**: `{"bridges_inferred": 51, "bridges_tagged_over_water": 215, "crossings": 130, "fords": 7, "kinds": {"BRIDGE": 285, "FLYOVER": 22, "FORD": 7, "NONE": 17161, "TUNNEL": 4, "UNDERPASS": 89}, "stations": 158820, "tunnels": 4, "water_snapped_to_bridge": 6, "ways_with_heights": 940}`
* **corridors**: `{"galli_ways": 367, "stations": 240411, "stations_below_min_space": 90535, "stations_touching_protected": 58, "ways_shifted": 231, "ways_squeezed": 17}`
* **trim**: `{"area_removed_m2": 251416.8, "extra_parts": 66, "protected_intruded": 27, "protected_intruded_refs": ["w112448570", "w115376623", "w1252760259", "w1412956655", "w1412956656", "w1412956657", "w183562980", "w196809346", "w196809462", "w201527639", "w255752871", "w257581957", "w257581960", "w304684945", "w304750886", "w310352075", "w341978116", "w377732314", "w495365591", "w501427078", "w509563807", "w519900311", "w519934305", "w819470706", "w819470711", "w910319024", "w913217407"], "protected_intrusion_m2": {"max": 183.84, "total": 381.4}, "protected_near_bands": 105, "protected_overlaps": 105, "removed": 98, "rings_repaired": 1, "roads_into_protected": [111823096, 136525993, 169116990, 169188262, 169188265, 172659831, 206839384, 219617682, 302394468, 304684149, 304684946, 311923710, 340712275, 377732313, 377741781, 519900310, 519934306, 686351447, 686351448, 798107752, 819470709, 819470710, 905308718, 1087173549, 1087173550, 1159185293, 1415342718, 1547755132], "slivers_dropped": 129, "split_into_parts": 62, "trimmed": 36589}`
* **car**: `{"car_accessible": 13845, "galli": 367, "ways": 17568}`
* **routing**: 346 ways lost CAR, JEEP and BUS (structures.car_accessible); motorbikes, bicycles and walkers keep them.

Buildings trimmed for a road corridor, per leaf tile (36,616 in 60 tiles):

| Tile | Trimmed |
|---|---:|
| 10/516/161 | 2,199 |
| 10/516/162 | 1,203 |
| 10/516/160 | 1,138 |
| 10/518/161 | 1,017 |
| 10/520/160 | 983 |
| 10/519/162 | 977 |
| 10/520/162 | 922 |
| 10/519/163 | 880 |
| 10/516/164 | 877 |
| 10/519/159 | 843 |
| 10/520/163 | 832 |
| 10/517/161 | 827 |
| 10/518/160 | 810 |
| 10/521/162 | 774 |
| 10/514/160 | 765 |
| 10/521/163 | 760 |
| 10/518/159 | 759 |
| 10/515/160 | 755 |
| 10/515/162 | 754 |
| 10/516/163 | 744 |
| 10/517/163 | 729 |
| 10/515/161 | 722 |
| 10/519/161 | 722 |
| 10/519/164 | 711 |
| 10/522/160 | 708 |
| 10/517/164 | 702 |
| 10/519/160 | 693 |
| 10/522/163 | 681 |
| 10/518/163 | 678 |
| 10/520/164 | 660 |
| 10/520/159 | 659 |
| 10/522/162 | 624 |
| 10/514/163 | 565 |
| 10/515/159 | 513 |
| 10/515/163 | 511 |
| 10/518/164 | 503 |
| 10/518/162 | 489 |
| 10/521/164 | 477 |
| 10/523/163 | 432 |
| 10/523/164 | 424 |
| 10/514/161 | 423 |
| 10/515/164 | 405 |
| 10/520/161 | 398 |
| 10/514/164 | 395 |
| 10/516/159 | 391 |
| 10/517/159 | 384 |
| 10/514/159 | 375 |
| 10/523/162 | 323 |
| 10/522/164 | 320 |
| 10/514/162 | 277 |
| 10/521/159 | 277 |
| 10/522/159 | 273 |
| 10/517/162 | 268 |
| 10/517/160 | 242 |
| 10/523/161 | 223 |
| 10/522/161 | 220 |
| 10/523/159 | 147 |
| 10/523/160 | 147 |
| 10/521/160 | 77 |
| 10/521/161 | 29 |

## Inference provenance

| What | % |
|---|---:|
| surface_tagged_pct | 18.25 |
| surface_derived_pct | 0.08 |
| surface_inferred_pct | 81.58 |
| surface_default_pct | 0.09 |
| building_levels_tagged_pct | 5.97 |
| building_levels_inferred_pct | 94.03 |

Trails: sac_scale tagged on 1, inferred on 3,393.

Surface mix (% of length): {"ASPHALT": 82.96, "CONCRETE": 3.9, "BRICK": 5.25, "COBBLE": 0.29, "GRAVEL": 2.08, "COMPACTED": 0.46, "DIRT": 5.0, "SAND": 0.0, "GRASS": 0.02, "ROCK": 0.02, "METAL": 0.02}

Building archetypes: {"CHURCH": 43, "GOMPA": 52, "GREENHOUSE": 124, "HILL_VILLAGE": 340, "HUT": 29, "INDUSTRIAL": 91, "INSTITUTIONAL": 3746, "MODERN_URBAN": 199107, "NEWAR": 14980, "RANA_PALACE": 1, "SHRINE": 25, "STUPA": 41, "TEMPLE_PAGODA": 438, "TEMPLE_SHIKHARA": 7}

Surface provenance by class (% of length):

| Class | km | tagged | derived | inferred | default |
|---|---:|---:|---:|---:|---:|
| TRUNK | 47.9 | 89.56 | 0.0 | 10.44 | 0.0 |
| PRIMARY | 67.7 | 76.65 | 0.0 | 23.35 | 0.0 |
| SECONDARY | 107.3 | 49.79 | 0.02 | 50.19 | 0.0 |
| TERTIARY | 117.8 | 34.26 | 0.0 | 65.74 | 0.0 |
| UNCLASSIFIED | 89.9 | 15.11 | 0.0 | 84.89 | 0.0 |
| RESIDENTIAL | 1230.1 | 10.0 | 0.03 | 89.97 | 0.0 |
| LIVING_STREET | 9.7 | 41.97 | 0.0 | 58.03 | 0.0 |
| SERVICE | 202.8 | 8.96 | 0.0 | 91.04 | 0.0 |
| TRACK | 30.5 | 12.45 | 3.37 | 84.18 | 0.0 |
| ROAD | 0.1 | 0.0 | 0.0 | 0.0 | 100.0 |
| PEDESTRIAN | 8.3 | 36.83 | 0.0 | 63.17 | 0.0 |
| FOOTWAY | 124.1 | 13.62 | 0.0 | 86.38 | 0.0 |
| PATH | 94.9 | 17.44 | 0.26 | 82.31 | 0.0 |
| STEPS | 8.5 | 32.75 | 0.0 | 67.25 | 0.0 |
| CYCLEWAY | 2.2 | 18.46 | 0.0 | 0.0 | 81.54 |
| BRIDLEWAY | 0.1 | 0.0 | 0.0 | 0.0 | 100.0 |

## Timings (s)

| Stage | s |
|---|---:|
| extract | 4.02 |
| dem | 3.12 |
| landcover_detail | 0.03 |
| landcover_horizon | 15.1 |
| surface | 0.71 |
| trails | 0.39 |
| w2_hints | 14.48 |
| buildings | 18.36 |
| search_entries | 0.29 |
| search | 0.15 |
| transit | 0.46 |
| w2_inputs | 9.37 |
| tiles | 233.82 |
| w2_chunk_stats | 6.43 |
| pack | 0.06 |
| clip_roads | 0.3 |
| routing | 0.58 |
| w2_files | 0.09 |
| total | 308.01 |

## Unrecognised tag values (top)

* **access**: `service` (2)
* **building**: `Advertising_Agency` (1), `National Human Rights Commission` (1), `Youth_Organization` (1), `aeroplane` (1), `bike servicing center` (1), `party_place` (1)
* **building:levels**: `0` (14), `7m` (1)
* **building:material**: `gold` (1)
* **height**: `0` (4), `Hashi Khushi Auto - Pepsicola` (1)
* **min_height**: `0` (9)
* **place**: `Bode-8` (1), `Gatthaghar` (1), `kathmandu` (1), `yes` (1)
* **roof:material**: `masonary` (40), `fabrick` (10)
* **width**: `0` (1)

## Attribution

* © OpenStreetMap contributors
* produced using Copernicus WorldDEM-30 © DLR e.V. 2010-2014 and © Airbus Defence and Space GmbH 2014-2018 provided under COPERNICUS by the European Union and ESA; all rights reserved
* © ESA WorldCover project 2021 / Contains modified Copernicus Sentinel data (2021) processed by ESA WorldCover consortium. Licensed under CC BY 4.0 (creativecommons.org/licenses/by/4.0/). Resampled and stylised for this game.
