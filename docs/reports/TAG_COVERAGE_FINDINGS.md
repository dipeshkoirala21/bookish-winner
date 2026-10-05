# OSM tag coverage: findings and consequences

Source: Nepal extract from 2026-10-02 (newest tagged object 2026-10-02 19:34 UTC), 468 MB PBF. Full numbers are in [tag_coverage.md](tag_coverage.md) and [tag_coverage.json](tag_coverage.json). The scan takes about 5 minutes on 4 cores.

> **Data source note.** The brief names Geofabrik as the source. From this cloud environment, `download.geofabrik.de` resets every connection, which is consistent with Geofabrik blocking cloud egress. The same daily extract came from the OSMToday mirror (`geo2day.com/asia/nepal.pbf`, ODbL, MD5 verified). `fetch.py` tries Geofabrik first and falls back to the mirror.

## Headline numbers

| What | Nepal | Kathmandu Valley | Consequence |
|---|---|---|---|
| Drivable road length | **180 955 km** (339 k ways) | 7 606 km | Large; most of it is rural `unclassified` (76 k km) and `track` (44 k km). |
| Road length with `surface` | **15.2%** | 14.4% | **85% of road surfaces must be inferred.** Tagged coverage is best on trunk/primary (41%) and worst on tracks (8%). |
| Most common surface values (km) | unpaved 10 284, ground 7 657, asphalt 3 357, paved 2 325, gravel 1 540, dirt 1 005 | asphalt 466, paved 159, unpaved 153 | Enough tagged data to fit an empirical model per class and region (ADR-005). |
| `tracktype` on tracks | 8.3% (grade3 > grade2 > grade4) | — | Used when present (`DERIVED`). |
| `smoothness` | 2.0% | 1.2% | Too sparse to rely on. |
| Road `width` / `lanes` | 1.6% / 3.3% | 17.6% / 5.3% | Widths come from class defaults (configurable). |
| Road `name` / `name:ne` | 7.9% / 1.5% | 19.4% / 5.6% | Most roads are unnamed. Turn hints use landmarks ("past the stupa") more than street names. |
| Trail length (`path`, `footway`, `steps`...) | **110 074 km** | — | A huge trail network: trekking has real geometry everywhere. |
| Trails with `sac_scale` | **2.2%** | — | Trail difficulty must be **inferred** from slope and altitude. |
| Hiking route relations | 122 (Annapurna Circuit, Annapurna Base Camp, Chisapani–Nagarkot, Dhorpatan, ...) | — | Named trek routes exist in OSM for the trekking activity. The pipeline does not keep route relations yet; that is CONTENT_COVERAGE D2. |
| Bus / microbus / tempo route relations | 55 / 11 / 9 (Kathmandu and Pokhara) | most | Real public-transport lines for city traffic AI and "ride the bus" in M1–M2. |
| Buildings | **8 297 952** | **515 790** | The valley alone needs aggressive LOD and instancing (ARCHITECTURE §7.5). |
| `building=yes` share | 97.5% | 94.6% | **Use must be inferred.** POIs inside a footprint and land use help. |
| `building:levels` | **0.42%** | 3.65% | **Levels must be inferred** from archetype and density. |
| `height` / `roof:shape` / `roof:material` | 0.01% / 0.31% / 0.32% | 0.06% / 3.4% / 3.4% | Roofs come from archetype rules. |
| Places (`place=*`) | 14 049 hamlets, 5 248 villages, 304 towns, 28 cities, 1 172 neighbourhoods | | |
| Places with `name:ne` | city 93%, town 77%, suburb 88%, village **14%**, hamlet **29%** | town 100%, neighbourhood 81% | The Nepali UI shows the Latin name when there is no `name:ne`. We do not show machine transliteration (ADR-005). |
| Script of primary `name` | 85.7% Latin, 13.5% Devanagari | 88.3% Latin | Search must match Latin queries against Devanagari names and vice versa, through the romaniser. |
| Places of worship | 9 770 (hindu 6 836, buddhist 1 073, christian 336, muslim 331) | 2 128 | Plenty of temple and gompa discoverables. 1 421 have no `religion`, so the archetype falls back to "shrine". |
| Peaks | 3 955 (`ele` 85%, `name` 19%) | 11 | Every 8 000 m peak is present with name, `name:ne` and elevation. |
| Glaciers | 3 781 polygons | 0 | Glacier biome from OSM plus WorldCover snow/ice. |
| Waterfalls | 335 | 5 | A good seed list for hidden gems. |
| Viewpoints | 1 397 | 126 | Same. |
| Airports / helipads | 58 aerodromes, 78 runways, 364 helipads | | Air travel network for M3. |
| Admin boundaries | provinces at `admin_level=4` (7), **districts at `admin_level=6` (77)**, local levels at `7` (~753), wards at `9` (6 741) | | **Explorer's Passport can use OSM boundaries directly**: 77 district stamps and 7 province stamps. |
| Protected areas | Chitwan, Langtang, Annapurna CA, Kanchenjunga CA, Koshi Tappu, ... | | Wildlife spawn regions come from these. |

## Data-quality quirks the parser must handle

Values seen in the extract include:

* **Surface:** `Blacktopped`, `bitumin`, `black_topped`, `blacktop`, `premix`, `ottaseal` (all mean asphalt); `G` and `3.5` (junk); `500 m`; `concrete;unpaved` (a multi-value); `בלתי_סלול` (Hebrew for "unpaved").
* **Place:** typos and free text such as `toukhel 3`, `motichaur 3` and phone numbers.
* **Religion:** `Baun` (a caste name), `WAaaaa`, `hindu;budhhist`.

Normalisation tables live in `tags.py`. Unknown values are counted in the build report, never silently dropped.

## Hero landmarks

See [landmarks.md](landmarks.md). On the first pass, 59 of 66 brief landmarks resolved to an OSM object with the expected tags. The weak or missing ones (Gorkha Durbar, the Bhote Koshi bungee site, Gupteshwor Cave, Kalinchowk temple, the Pokhara zipline) need hand placement or an OSM edit. Hand placements are recorded in `config/landmarks.yaml` as `manual: [lon, lat]` with a source note.

## Decisions taken because of these numbers

1. **ADR-005, inference-first.** Every inferred attribute carries a provenance flag (`SurfaceSource`, `LEVELS_INFERRED`, `SAC_INFERRED`). The QA viewer and the debug inspector show provenance.
2. **The surface model is fitted per build** on that build's tagged roads, conditioned on class, density, elevation and slope. Coverage will improve as OSM improves, and the nightly job tracks it.
3. **No machine Devanagari in the UI.** Romanisation is used only to build search keys.
4. **Passport regions come from OSM admin boundaries** (levels 4 and 6).
5. **City buses follow real OSM bus routes** where they exist.
