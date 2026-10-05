# Build report: kathmandu_core

* Built at 2026-10-05T17:06:00Z by pipeline 0.1.0 (data_version 2).
* bbox [85.283, 27.69, 85.375, 27.735], horizon [84.6, 27.2, 86.4, 28.6]; detail levels [8, 9, 10], horizon levels [5, 6].
* Extract read from the PBF; 4 POI kinds set from curated landmarks, 0 landmark POIs added.
* road_km counts roads clipped to the leaf tiles (what the tiles and the routing graph hold); road_km_extract includes the extract's buffer zone.

## Files

| File | Bytes | MB | SHA-256 |
|---|---:|---:|---|
| kathmandu_core.ghpk | 11,778,286 | 11.78 | `898baa10f629c3d4…` |
| kathmandu_core.search.ghsi | 155,776 | 0.16 | `e76829dffd0bd490…` |
| kathmandu_core.route.ghrg | 2,274,177 | 2.27 | `443a709d62f16f52…` |
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
| HGHT | 5,269,108 | 44.8% |
| BLDG | 4,722,375 | 40.1% |
| ROAD | 487,712 | 4.1% |
| BFNT | 383,165 | 3.3% |
| NAME | 281,039 | 2.4% |
| POIS | 193,593 | 1.6% |
| BIOM | 154,144 | 1.3% |
| AREA | 134,818 | 1.1% |
| RATR | 74,501 | 0.6% |
| PROP | 15,278 | 0.1% |
| LINE | 12,727 | 0.1% |
| META | 12,638 | 0.1% |
| SEED | 1,246 | 0.0% |
| JNCT | 1,038 | 0.0% |

Leaf tiles: 60; buildings placed 167,977 of 219,024 extracted (the rest lie in the bbox buffer).
Feature records: {"area_parts": 1493, "bfnt_fronts": 141639, "buildings": 167977, "junctions": 53, "line_pieces": 168, "places": 216, "pois": 15252, "props": 1119, "ratr_corridor_samples": 50458, "road_pieces": 14371}

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
* **chunks**: `{"chunk_bytes": {"BFNT": 383165, "JNCT": 1038, "PROP": 15278, "RATR": 74501}, "corridor": {"narrower_pct": 0.0, "narrower_than_tagged": 0, "open": 18084, "p25_p50_p75_m": [5.2, 8.8, 16.4], "samples": 50458, "tagged_width_samples": 6218}, "fronts": {"buildings": 167977, "corners": 20950, "with_front": 141639}, "junction_kinds": {"CIRCULAR": 2, "MINI_ROUNDABOUT": 2, "POLICE": 10, "ROUNDABOUT": 6, "SIGNALS": 25, "SYNTHETIC_ISLAND": 8}, "props": {"AEROWAY_GATE": 12, "BUS_STOP": 134, "CROSSING_MARKED": 158, "CROSSING_UNMARKED": 225, "GATE": 154, "HELIPAD": 8, "PARKING_POSITION": 10, "STORAGE_TANK": 14, "STREET_LAMP": 168, "TAXI_STAND": 17, "TRAFFIC_SIGNALS": 27, "TREE": 190, "WINDSOCK": 2}}`
* **routing**: 23 road pieces lost their motor modes inside sacred zones (D14).

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
| extract | 141.36 |
| dem | 4.03 |
| landcover_detail | 0.04 |
| landcover_horizon | 17.05 |
| surface | 0.9 |
| trails | 0.36 |
| w2_hints | 11.2 |
| buildings | 13.5 |
| search_entries | 0.22 |
| search | 0.11 |
| transit | 0.3 |
| w2_inputs | 6.41 |
| tiles | 63.67 |
| w2_chunk_stats | 5.62 |
| pack | 0.04 |
| clip_roads | 0.22 |
| routing | 0.38 |
| w2_files | 0.1 |
| total | 271.34 |

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
