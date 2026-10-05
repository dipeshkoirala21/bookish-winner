# Build report: kathmandu_core

* Built at 2026-10-05T00:53:46Z by pipeline 0.1.0 (data_version 1).
* bbox [85.283, 27.69, 85.375, 27.735], horizon [84.6, 27.2, 86.4, 28.6]; detail levels [8, 9, 10], horizon levels [5, 6].
* Extract from cache; 4 POI kinds set from curated landmarks, 0 landmark POIs added.
* road_km counts roads clipped to the leaf tiles (what the tiles and the routing graph hold); road_km_extract includes the extract's buffer zone.

## Files

| File | Bytes | MB | SHA-256 |
|---|---:|---:|---|
| kathmandu_core.ghpk | 11,281,736 | 11.28 | `5073bc4d2603d1c0…` |
| kathmandu_core.search.ghsi | 150,685 | 0.15 | `44341abb62c15608…` |
| kathmandu_core.route.ghrg | 2,274,177 | 2.27 | `c80a79fa8d9a7ef5…` |

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
| HGHT | 5,269,108 | 46.7% |
| BLDG | 4,707,473 | 41.8% |
| ROAD | 487,707 | 4.3% |
| NAME | 278,652 | 2.5% |
| POIS | 192,332 | 1.7% |
| BIOM | 154,142 | 1.4% |
| AREA | 134,055 | 1.2% |
| LINE | 12,727 | 0.1% |
| META | 12,638 | 0.1% |
| SEED | 1,246 | 0.0% |

Leaf tiles: 60; buildings placed 167,576 of 218,622 extracted (the rest lie in the bbox buffer).
Feature records: {"area_parts": 1485, "buildings": 167576, "line_pieces": 168, "places": 216, "pois": 15186, "road_pieces": 14371}

## Content

| What | Value |
|---|---:|
| roads | 17,568 |
| road_km | 1442.9 |
| road_km_extract | 2141.912 |
| buildings | 218,622 |
| pois | 22,075 |
| places | 314 |
| areas | 1,816 |
| lines | 128 |
| admin_areas | 12 |
| search_entries | 1,156 |
| search_keys | 2,831 |
| graph_nodes | 20,832 |
| graph_edges | 52,064 |

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

Surface mix (% of length): {"ASPHALT": 82.96, "CONCRETE": 3.9, "BRICK": 5.24, "COBBLE": 0.27, "GRAVEL": 2.1, "COMPACTED": 0.46, "DIRT": 5.0, "SAND": 0.0, "GRASS": 0.02, "ROCK": 0.02, "METAL": 0.02}

Building archetypes: {"CHURCH": 43, "GOMPA": 44, "GREENHOUSE": 124, "HILL_VILLAGE": 341, "HUT": 29, "INDUSTRIAL": 91, "INSTITUTIONAL": 3747, "MODERN_URBAN": 199095, "NEWAR": 14940, "SHRINE": 30, "STUPA": 1, "TEMPLE_PAGODA": 137}

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
| extract | 4.62 |
| dem | 3.44 |
| landcover_detail | 0.03 |
| landcover_horizon | 15.84 |
| surface | 0.71 |
| trails | 0.35 |
| buildings | 11.05 |
| search_entries | 0.2 |
| search | 0.1 |
| tiles | 40.62 |
| pack | 0.04 |
| clip_roads | 0.2 |
| routing | 0.27 |
| total | 77.58 |

## Unrecognised tag values (top)

* **access**: `service` (2)
* **building**: `Advertising_Agency` (1), `National Human Rights Commission` (1), `Youth_Organization` (1), `aeroplane` (1), `bike servicing center` (1), `party_place` (1)
* **building:levels**: `0` (14), `7m` (1)
* **height**: `0` (4), `Hashi Khushi Auto - Pepsicola` (1)
* **place**: `Bode-8` (1), `Gatthaghar` (1), `kathmandu` (1), `yes` (1)
* **roof:material**: `masonary` (40), `fabrick` (10)
* **width**: `0` (1)

## Attribution

* © OpenStreetMap contributors
* produced using Copernicus WorldDEM-30 © DLR e.V. 2010-2014 and © Airbus Defence and Space GmbH 2014-2018 provided under COPERNICUS by the European Union and ESA; all rights reserved
* © ESA WorldCover project 2021 / Contains modified Copernicus Sentinel data (2021) processed by ESA WorldCover consortium. Licensed under CC BY 4.0 (creativecommons.org/licenses/by/4.0/). Resampled and stylised for this game.
