# Ghumante: content coverage contract

> Status: **draft, 2026-10-05.** This is the contract for the product owner's vision: *"a GTA style open world explore game in cartoonish theme. It should include every place, object, landmark, monument, scenery, winding road, waterfall, building, trekking route, forest and adventure spot in their actual places."* It says what that means at 1:1, how far the data and code are from it today, and the work that closes the gap.
> Companion docs: [ARCHITECTURE](ARCHITECTURE.md) · [ROADMAP](ROADMAP.md) · [M1_PLAN](M1_PLAN.md) · [DATA_FORMATS](DATA_FORMATS.md) · [ASSET_MANIFEST](ASSET_MANIFEST.md) · [LICENSES](LICENSES.md) · [tag coverage](reports/tag_coverage.md) · [landmarks](reports/landmarks.md)
> "GTA style" means the open-world **format**: seamless free roaming, hop on and off any vehicle, living streets, minimap and waypoints, discovery, side activities. It does not mean crime or weapons (9+ / PEGI 7, ARCHITECTURE P11).

**Where the numbers come from.** A one-pass census of the OSM Nepal extract (2026-10-02, 468 MB), the built `kathmandu_valley` pack and the `kathmandu_core` sample, plus targeted DEM, WorldCover and Wikidata checks (2026-10-05). The census script and its tables are in [reports/content_census/](reports/content_census/README.md); work item D13 makes them a per-build pipeline report. Scopes:

| Label | Meaning |
|---|---|
| **Nepal** | The whole extract. It includes Indian and Tibetan border strips (e.g. Raxaul aerodrome, Shishapangma). Counts marked *in Nepal* are clipped to the national polygon (e.g. 8,219,167 buildings in Nepal vs 8,297,951 in the extract). |
| **valley** | Census box 85.18–85.56 E, 27.57–27.82 N |
| **pack** | `kathmandu_valley` pack box 85.18–85.58 E, 27.55–27.83 N (what M1 ships) |
| **core** | `kathmandu_core` sample box 85.283–85.375 E, 27.690–27.735 N (committed in the repo) |

---

## 1. The promise, made precise

### 1.1 What "in its actual place" means at 1:1

| Aspect | Rule | Source | Precision today |
|---|---|---|---|
| Horizontal position | Every feature that is mapped sits at its real coordinates. No moving, merging, snapping or "typical" placement. | OSM (ODbL) | Exact. Buildings round-trip at ≤ 0.98 cm; roads keep every vertex (max deviation 0.0099 m on 12 k checked vertices); POIs at cm. NPL-TM84 scale error ≤ 0.18%. |
| Shape | Real outlines: footprints, road centrelines, river lines, lakes, forests, compounds. Lower LOD tiers may carry simplified copies, flagged display-only and never used for driving, routing or placement. | OSM | Exact at leaf level (no simplification anywhere in the pipeline). |
| Height and relief | Real terrain, vertical 1:1 (ADR-004). Where the DEM is known to be wrong (summits, canopy, narrow gorges, water surfaces, road benches) the runtime corrects it from OSM, without writing OSM into the DEM layer (ADR-013). | Copernicus GLO-30 (2021 release) | 30 m DSM, shipped at 8 m spacing, 15 cm quantisation. Includes canopy and buildings. |
| Land cover | Forest, cropland, built-up and so on come from real land cover; OSM polygons win where mapped. | ESA WorldCover 2021 (10 m), OSM | Nearest pixel at 16 m BIOM spacing. |
| Names | Only real names: OSM, or the curated DB (Wikidata CC0, our own research). Nothing named is ever generated. Devanagari is never machine-made (ADR-005). | OSM, curated DB | 12.6% of named OSM features have `name:ne`. |
| Things OSM lacks | Fixed upstream in OSM when they are mappable facts, otherwise added to the **curated DB**: a separate, licence-clean database keyed by our own ids with OSM refs (ADR-013). | OSM edits, curated DB | Curated DB does not exist yet (D12). |
| Decoration | Procedural fill (trees inside real forests, poles and wires, rooftop tanks, parked bikes) is deterministic from `SEED`, generic, unnamed, never discoverable, never on the map, suppressed near a real object of the same kind, and labelled "procedural" in the debug inspector. It never creates temples, shrines, chortens, chautari or settlements. | `SEED` + rules | No scatter code yet. |
| Time | One pinned OSM snapshot per build (`SOURCES.lock.json`). The world is "as mapped on that date". | Lock file | 2026-10-02. |

### 1.2 Fidelity tiers

Every feature class has a tier today and a target tier per milestone. The matrix in §2 uses these.

| Tier | Meaning | Example |
|---|---|---|
| **T0** | Dropped by the pipeline | Route relations, `natural=tree` without a name, power lines |
| **T1** | In data only: map, search or pin, nothing in the 3D world | Point-only temples, rivers drawn only as lines |
| **T2** | In the world at the real position, generic look | A shrine prop at a temple node; a procedural pagoda from a footprint |
| **T3** | In the world at the real position with its real shape and attributes (height, tiers, width, span, species class) | A tiered temple from `building:part`; a suspension bridge with its real span and deck height |
| **T4** | Hand-made hero art at the real position and orientation | Boudhanath, Swayambhunath, Kathmandu Durbar Square |

**Contract:** in every region we ship, every in-scope feature that exists in OSM (at the pinned snapshot) or in the curated DB reaches **T2 or better**; named heritage, water, bridges and landmark-lite sites reach **T3**; the hero list reaches **T4**. The build report measures this per category (D13).

### 1.3 Honest limits

| Limit | Effect | What we do |
|---|---|---|
| OSM completeness | Some real things are not mapped: about 450 of 573 recorded stone spouts, most waterfalls (335 tagged, 69 named), mani walls (0), most adventure spots, Gorkha Durbar, the Bhote Koshi bungee, ~90% of tea estates. 21% of Nepal's buildings (1.74 M) are more than about 1 km from any named place. | OSM mapping campaigns with the local community; curated DB for game facts; ward names as fallback for nameless settlements. Never invent. |
| OSM errors | Plinth heights tagged as building heights; houses tagged `historic=castle`; wrong municipality names; a kinked 1.7 km "zip line" at Naikap. | Plausibility gates, overrides with provenance, fix tasks sent upstream (§4.4). |
| 30 m DSM | Cannot resolve gorges narrower than a pixel (Chobhar shows a 33 m "dam"), terrace steps, hiti pits, Lukla's runway slope. Includes canopy (+3.0 m median over cropland) and buildings. Summits infilled (Everest −111 m). | Runtime conformance: water profiles, road benches, lake flattening, summit cones, terrace steps from the real contours. |
| 1:1 density | One instance per real tree, pole or wire is impossible: Phulchoki's near ring holds about 0.48 M trees against a 110 k-triangle vegetation budget. | Real extents and real single trees are exact; density inside them is representative. |
| Interiors | Not modelled, except hero courtyards. | By design. |
| Change over time | The world is a snapshot (new buildings, a rebuilt Dharahara, a new cable car). | Rebuild packs from new snapshots; curated records carry a "verified on" date. |

### 1.4 Rules

1. **OSM first, then curated, never invented.** A named or sacred thing appears only if OSM or the curated DB has it.
2. **Every inferred or generated value is flagged** (ADR-005), in data and in the debug inspector.
3. **Layer separation (ADR-013).** OSM-derived data ships in the ODbL packs. DEM- and WorldCover-derived data stays in its own chunks. Curated facts stay in the curated DB. Anything computed from OSM geometry (for example a curated trek snapped to OSM ways) is ODbL and ships in the pack.
4. **Fix the source.** A fact that is wrong or missing in OSM is fixed in OSM with the community, not patched silently.
5. **Measured every build.** Coverage per category and tier is in `BUILD_REPORT.md` and diffed nightly.
6. **Sacred content is reviewed** by the cultural consultant before a public build (ROADMAP 1.15).

---

## 2. Coverage matrix

Columns: **source data** gives *Nepal · valley* counts unless stated. **Gap**: ok, minor, major or missing, with the tier move needed. **Action** points to work items in §3 (D = pipeline/data track, 1.x = ROADMAP runtime items, C = curation).

### 2.1 Places and names

| # | Feature | Real world | Source data | Pipeline today | Runtime / milestone | Gap | Action |
|---|---|---|---|---|---|---|---|
| P1 | Cities and towns | 6 metropolitan, 11 sub-metropolitan, 276 municipalities | city 28, town 289 *in Nepal* (230 with `name:ne`) · pack 4 cities, 19 towns | Exact label point; outline dropped; written only to leaf POIS; no wikidata or population on `PlaceFeature`, so ranking bonuses never apply | Search W1 (1.10). No 3D label item | minor (T1→T2) | D6, 1.20 |
| P2 | Villages and hamlets | Tens of thousands (GeoNames NP: 87,903 populated places) | village 5,097 (1,709 unnamed), hamlet 13,719; `name:ne` 24% / 34% · valley 19 villages, 206 hamlets | 3,160 unnamed place objects dropped; FARM and business-like names (291) reach search (ADR-010); "Sankhu" ranks 3rd for "Sankhu" | Only "procedural roadside life" (M2) | major | D1, D6 |
| P3 | Toles, neighbourhoods, localities | Hundreds of Newar toles (Asan, Jhochhen, Taumadhi…) | suburb 463, neighbourhood 1,170, locality 1,128 · valley 838 | Exact. Hyphenated Newar names fold badly ("Ja-rhun-hi-ti") | Map labels W2; no area banner | minor (T1→T2) | D6, 1.20 |
| P4 | Squares and chowks | 3 Durbar Squares; hundreds of traffic chowks | `place=square` 48 · 9 | Every `place=square` becomes HERITAGE_SQUARE (tags.py:1260): Koteshwar, Samakhushi, Milan, Jagriti chowks get the heritage pin and +50 rank | Heritage pin W2 | minor (wrong) | D1 |
| P5 | Provinces and districts | 7 and 77 | All 7 provinces; 79 level-6 relations (2 to reconcile) | Search only, at an arbitrary interior point. Admin entries skip the coverage filter: 18 of 46 valley and 10 of 12 core entries are off the playable tiles ("Dhading" is 33.8 km away) | W1 search-then-teleport can land off the map. Passport (M2) needs polygons the pack lacks | major | D6 |
| P6 | Local levels | 753 | 749 level-7 relations; 415 with Devanagari; `admin_centre` on 394. Wrong names: Dhulikhel r12394199 is "दुधौली नगरपालिका", Mahalaxmi r10535035 is "ललितपुर महानगरपालिका" | Search entry at `point_on_surface`; wrong names copied | Search W1; welcome gates M2 | minor | D6, C (admin overrides), §4.4 |
| P7 | Wards | 6,743; every house is in a ward ("Mahankal-6") | 6,741 level-9 relations (494 with `name:ne`) · 287 in the pack box | The extractor supports level 9; only the default `admin_levels=(4,6,7)` leaves wards out (build.py:62). Not in search | Not planned | missing (T0→T1) | D6 |
| P8 | Nameless settlements | Every house cluster has a name | 17,520 of 30,089 building clusters (1.74 M buildings, 21%) have no named place within ~1 km · valley 97 clusters (6,519 buildings, 17 with ≥ 100) | No settlement inference | None | major | D6 (anchors + ward names), O4 |
| P9 | Place names in the 3D world | Welcome arches, ward signs, tole names | 5,956 GHSI entries in the valley | Places only in leaf POIS, so only the 0.75–1.75 km near ring has them | No M1 item. `PlaceEntered` has no producer | missing (T1→T2) | 1.20 |
| P10 | Map and minimap labels | — | 1,091 valley place entries | — | 1.10 W2; style sheet has no PlaceKind hierarchy | major | 1.20, art `ghm_map_label_places` |
| P11 | Search (EN, NE, typos) | Many repeated names (30 "Deurali") | Valley self-search top-5: EN 99.6%, NE 99.5%, one-letter typo 94.6%. National simulation top-1 EN 78% | No local level or ward in GHSI to tell same-named places apart; no ाे→ो normalisation; no joined key | C# port W1 | minor | D6 (keys, W2), D16 (GHSI v2, M2) |
| P12 | Devanagari names | Every place has one | Places with `name:ne` 34%; local levels 334 of 749 lack it; wards 6,247 of 6,741 · valley 276 of 1,091 | ADR-005: no machine Devanagari | NE UI shows Latin for most villages | major | C (`names_ne` from Wikidata CC0), O5 |
| P13 | Discoverable places, fast travel | Every town and tole is findable | Valley: **0 of 1,091 places discoverable**; 4,268 POIs discoverable, including 1,827 household springs and 199 mapping-campaign gardens | DISCOVERABLE only for POI kinds 100–299 | Fast travel only to discovered places and hubs (§7.8), so never to a town | major | D1, D6, 1.20 |
| P14 | Reachability | Most villages by road or trail; some trek-only | Valley: all 1,045 settlements within 500 m of the FOOT network. Nepal: 10.3% of settlements' nearest way is a disconnected fragment; 501 are > 5 km from a motor road | No last-leg model; no cross-region graph | A* W1 | minor | 1.10 (arrival hint, `max_snap_m`), M2 stitched graph |
| P15 | National gazetteer | 753 local levels, 6,743 wards, all settlements | ~27 k entries, about 3.1 MB | None; no ROADMAP id | Needed for M2 "download Pokhara on demand" | missing | D16 (M2) |
| P16 | Place-tag noise | — | 123 free-text `place=*` values; 291 business-like names | Dropped or copied verbatim | — | minor | D1 |

### 2.2 Landmarks and monuments

| # | Feature | Real world | Source data | Pipeline today | Runtime / milestone | Gap | Action |
|---|---|---|---|---|---|---|---|
| L1 | UNESCO sites | 4 inscribed; Kathmandu Valley has 7 zones; 15 tentative sites, 4 inside the pack (Kirtipur, Panauti, Khokana, Bajrayogini) | `heritage=1` 14 · 9. KTM DS r21291455 (8,032 m²), Patan DS r4557971, Bhaktapur DS w1192827651 (no heritage tag). Gorkha Durbar is not in OSM | `type=site` relations dropped (only multipolygon and boundary are read, osm_extract.py:352); no WHS flag | Hero rows M1 P0–P2 | major | D5, C (`heritage_sites`), §4.4 |
| L2 | Hero anchors (66) | — | 60 found, 5 weak, 1 missing. "Found" but wrong object: Boudhanath → compound wall w56688296 (the stupa is w56688295), Patan DS → a node (the square is r4557971), Muktinath → a pond. The `manual:` placement in the docs is not implemented | Hiding uses one exact ref: only 4 valley buildings carry LANDMARK | 1.6 placeholders W2 | major | D5 |
| L3 | Newar pagodas | Nyatapola 5 tiers (~30 m), Kasthamandap, Taleju, Maju Dega, hundreds of 1–2 tier temples | 79% of places of worship are points. Valley TEMPLE_HINDU 1,377: 770 nodes with no footprint. 24 of 36 height-tagged valley religious buildings are < 3 m (an earthquake survey's plinth heights): Nyatapola 2 m, Kasthamandap 1 m, Taleju 0.5 m. Their `building:part` ways carry the real heights (Nyatapola 31.7 m, Taleju 35 m) | Tagged height always wins; parts dropped; a temple node inside a `building=yes` footprint does not pass its religion to it (213 cases) | W1 draws 1–2 m stubs in all three Durbar Squares | major (T1→T3) | D4, D1, 1.18, C (`heritage_overrides`) |
| L4 | Shikharas | Krishna Mandir, Vatsala and Siddhi Lakshmi, Pratappur and Anantapur | No style tag; only names | Shikhara chosen only in the Terai (buildings.py:250): valley has 0 | Mesher has a shikhara generator that is never selected | major | D1, C |
| L5 | Stupas and chaityas | Boudha (36 m), Swayambhu, Chabahil, the Ashoka stupas; thousands of chaityas in bahals | `man_made=stupa` 110 · 10; valley STUPA POIs 17 (6 with footprints) | `man_made=stupa` ways are not footprints; Boudha becomes a 4 m TEMPLE_PAGODA pyramid | M1 acceptance ("arrive at Boudha") ends at the wrong building | major | D1, D5, 1.18 |
| L6 | Gompas vs Newar bahals | Kopan, Shechen; bahals are pagoda-roofed courtyards | Buddhist places of worship 1,068 · 230; valley GOMPA 331 (191 without a footprint) | Every Buddhist site is GOMPA, including bahals and Swayambhu's Buddha niches | Gompa kit M1 P1 | minor | D1, 1.18 |
| L7 | Chortens and mani walls | Thousands along Himalayan trails | `man_made=mani_wall` 0; MANI_WALL lines 0.3 km; CHORTEN POIs 3 | Himalayan chortens tagged `wayside_shrine` become a Hindu SHRINE | M3a | missing | D1, §4.4 campaign, O3 |
| L8 | Small shrines, generic places of worship | Ganesh, Bhairab, nag stones at every crossroads | SHRINE 842, place of worship without religion 1,222 · valley SHRINE 22, PLACE_OF_WORSHIP 418 | Point records only | No placement rule | major (T1→T2) | 1.18 |
| L9 | Durbar squares, palaces, Rana palaces | Hanuman Dhoka, Patan and Bhaktapur palaces; Singha Durbar, Narayanhiti, Kaiser Mahal, Garden of Dreams | `historic/building=palace` 0. `historic=castle` 130 · 16; all 16 valley ones are houses ("Rishen's House", a cow farm). Gaddi Baithak tagged 1 m | `castle` → PALACE (junk pins); no Rana archetype | Squares are hero B rows | major | D1, RANA_PALACE archetype, C |
| L10 | City gates, arches, walls | Golden Gate, dhokas, modern welcome arches | `city_gate` 53 · 10 (all modern arches); city walls 2.1 km · 0.1 km | Exact points, no orientation | Welcome gate M2, no data link | minor | 1.18 |
| L11 | Statues and memorials | Budhanilkantha, Kailashnath at Sanga, Maitighar Mandala, Shahid Gate | `tourism=artwork` 312 (302 with no other tag) · 127; `historic=monument` 200 · 98; memorial 160 · 28 | No rule for artwork: 302 dropped, including "Lying Lord Vishnu" w686731982 | No statue prop | major (T0→T2) | D1, 1.18, C |
| L12 | Towers | Dharahara (rebuilt 2021), Ghantaghar | `man_made=tower` 413. Dharahara: node n11622074774, outline w1413193271 (13 m ring, `min_height` 50, height 51, cone roof), parts up to 63 m | Outline is drawn as a 1 m disc floating at 50 m; parts dropped; the core sample has no Dharahara POI (build.py:232) | Hero row M1 P1 | major | D4, D5 |
| L13 | Stone spouts (hiti) | 573 on record (2019), 479 recovered | `historic=stone_tap` 90 · 89; valley STONE_TAP 126 (94 point-only), about 22% of known. Some are mapped as ponds or ruins | Exact points; a 30 m DEM cannot hold a 3 m pit | Prop M1 P0 with no placement | major | 1.18, D1, §4.4 |
| L14 | Sacred ponds | Rani Pokhari (island temple), Kamal, Siddha, Taudaha, Nag Daha | 348 named pond-like features; valley: 86 named WATER_POND with no POI | Water outlines exact; LAKE POI only for lake/reservoir, so these are unsearchable | Water drawn tilted (W1) | minor | D1, 1.16 |
| L15 | Ghats | Arya Ghat, Teku, Sankhamul, Gokarna | 132 named ghat; tags vary (cemetery, religious, crematorium) | No GHAT kind; crematoria become SHOP | Pashupati hero includes ghats, no cremation | missing | D1, 1.18 |
| L16 | Sacred access rules | No entry to Pashupati's main temple for non-Hindus; clockwise kora; no vehicles; Bon sites anticlockwise | Valley RELIGIOUS areas 329; 1.26 km of motor-legal road inside compounds, 1.66 km in squares | SACRED POI flag only; routing ignores AreaKind | No barrier in the plan; consultant not named (§14 Q9) | major | D14, C (rules), 1.15, O10 |
| L17 | Landmark-lite tier | Wikidata-notable sites beyond the 66 (Kasthamandap, Kumbheshwar, Kal Bhairab, Golden Temple, Rani Pokhari…) | `wikidata` on 0.8% of named features; Wikidata heights: Boudha only of 17 checked | Procedural archetypes, with the height and archetype errors above | Not in ASSET_MANIFEST | major | D5, C (~150 valley entries) |

### 2.3 Nature and scenery

| # | Feature | Real world | Source data | Pipeline today | Runtime / milestone | Gap | Action |
|---|---|---|---|---|---|---|---|
| N1 | Forest extents | Valley rim almost all forest; sacred groves in the city | OSM FOREST 48,853 km² · 357 km². WorldCover tree cover 546.6 km² in the valley box (52.7%) | Exact polygons; BIOM from WorldCover first. Valley L10: HILL_FOREST 55.7% (728 km²) | Flat green tint 0.10 m above terrain. **No tree item in any M1 wave** | major (T1→T2) | 1.17 |
| N2 | Species and density | Schima-Castanopsis, chir pine, oak with rhododendron, bamboo by houses | `leaf_type` on 0.36% of forest ways; species on 1 | `SEED` hashes `data_version`, so every pipeline version re-rolls every tree | No scatter code; budget 20 k / 60 k / 110 k triangles | missing | 1.17, D10 |
| N3 | Single trees, chautari, tree rows | Pipal and bar on chautari platforms | `natural=tree` 12,758 · 493 (pack 1,135); 424 named chautari/pati; `tree_row` 511 km · 8.1 km | Only named trees kept (NOTABLE_TREE 63 in the valley); 12,094 unnamed dropped; species not carried; tree rows dropped | None (chautari M2) | major (T0→T3) | D3, 1.17 |
| N4 | Terraces and paddies | Rim terraces; monsoon rice on the valley floor | WorldCover cropland 206.7 km² · valley; `farmland` 11,383 km² · 79 | HILL_TERRACES (102 km²) and VALLEY_CROPLAND (145 km²) in the valley; no 35° cap; Terai paddy placed by noise | Colour only; terrace geometry M2 | major | 1.17 (W3), terrace generator M2, D8 |
| N5 | Rivers and streams | Bagmati 20–60 m wide in town; Trishuli, Koshi | river 26,776 km, stream 66,457 km; width tag on 0.9% · valley RIVER 331 km, STREAM 354 km; 23.5% of valley river line has a riverbank polygon | Exact, downstream point order kept. On L10 heights, 22.5% of river length is > 2 m above its upstream minimum: the Bagmati climbs 20 m beside Pashupatinath and 33 m at Chobhar | **LINE is never meshed**: line-only rivers are invisible; polygons drape over DSM bumps | major (T1→T3) | 1.16, D8 |
| N6 | Lakes and ponds | Rara, Phewa, Gosaikunda; Taudaha, Nag Daha, Rani Pokhari | `water=lake` 1,545 (100.8 km²); 51% of named lakes show > 5 m of relief inside in the DEM | Outlines exact; `ele`, `depth` not carried; no OSM water in horizon tiles | Draped, tilted surfaces | major | 1.16, D8 |
| N7 | Waterfalls | Hundreds; valley rim: Fung Funge, Nagarkot, Lauke, Jhor, Sundarijal | `waterway=waterfall` 335 (69 named, 3 with height, 132 on a waterway line); 106 more named like waterfalls. Valley box 5; Fung Funge (27.829 N) is inside the pack; Lauke in its buffer; Jhor untagged; Sundarijal absent; "Basmati" is a 1 m weir | WATERFALL POI at the node; not linked to its stream; no drop | **Nothing in M1**; assets M2, keyed to LineKind.WATERFALL, which 3 falls use | missing (T1→T3) | D8 (FALL), 1.16, C, §4.4 |
| N8 | Glaciers, snowline | ICIMOD 2010: 3,808 glaciers, ~3,902 km² | `natural=glacier` 3,781 (4,756 km², ~22% above the 2010 extent: stale outlines), 152 named | Debris tongues get clean ice; moraine colour changes with LOD; no OSM glaciers in horizon tiles; 3,629 unnamed GLACIER POIs | Skyline look | minor | D9, D1 |
| N9 | Peaks | 8 of the 14 eight-thousanders are in Nepal; rim: Phulchoki 2,782, Shivapuri 2,732, Chandragiri 2,551 | `natural=peak` 3,955 (754 named, 3,345 with `ele`), 20 ≥ 8,000 m (incl. Shishapangma) · 11 | PEAK POIs leaf-only. L5 prefilter lowers ≥ 6,000 m summits by a median 114 m (Everest renders at 8,614 m) on top of DSM infill (−111 m). The planned "summit correction" in dem.py would write OSM into HGHT (conflicts with ADR-013) | No peak labels | major | D9, 1.9 (runtime summit cones) |
| N10 | Himalayan skyline | From Nagarkot (curvature-correct line of sight): Langtang Lirung 62 km, Shishapangma 77, Gauri Shankar 86, Ganesh I 90, Cho Oyu 121, Manaslu 134, Everest 142 | DEM tiles N27/N28 E084–E086 on disk; Makalu needs E087 | `horizon_bbox` stops at 84.60–86.40 E; no skyline ring code | Far clip is 67.5 / 105 / 155 km (Low/Mid/High): Everest is beyond it on Mid. ROADMAP 1.9 has the impostor ring; M1_PLAN W1 dropped it | major | D9, 1.9 |
| N11 | Passes and saddles | Thorong La; rim bhanjyangs | `mountain_pass` 1,224, saddle 488 · 2 PASS POIs | Not linked to the trail crest | M3a | minor | D9 (M3a) |
| N12 | Cliffs, gorges, DSM bias | Chobhar gorge, Seti gorge | `natural=cliff` 1,436 (424 km); gorge 0 | Cliffs not extracted; cliff relief halves by L5; 2,784 unnamed RIDGE POIs | No cliff or gorge geometry | major | D3 (CLIFF), 1.16 (gorge carve), D9 |
| N13 | Caves | Chobhar, Pharping (Asura, Gorakhnath) | `cave_entrance` 86 · 3 CAVE POIs | A cave shrine becomes a temple only | M2 | minor | D1, C |
| N14 | Hot springs, springs | Tatopani, Jhinu; none in the valley | `hot_spring` 17; name regex makes 24 (3 dubious in the valley). `natural=spring` 5,429 · 1,206 → 1,932 SPRING POIs | Named springs discoverable | — | minor | D1 |
| N15 | Viewpoints | Nagarkot, Chandragiri, Phulchoki, Kakani, Champadevi, Swayambhu | `tourism=viewpoint` 1,397 · 119 (houses and offices among them) | Discoverable when named; no filter | Discovery W2 | minor | D1, C (viewshed score) |
| N16 | Protected areas | Shivapuri-Nagarjun NP on the north rim | 24 boundary relations; Shivapuri-Nagarjun 103 km² in OSM vs ~159 km² official (verify) | Biome tier and 6 POIs | Not drawn; no entry event | minor | 1.20 |
| N17 | Landslide scars | Fresh scars on every hill road | `hazard/geological=landslide` 781 · 3 | Not read; steep bare ground turns grey scree | Scar variant asset M1 P1 | minor | D9 (M2) |
| N18 | Tea gardens | Ilam, Jhapa: ~16,000 ha (verify) | TEA_GARDEN 424 polygons, 16.9 km² (~10%) | Only `crop=tea` on farmland or orchard | M5 | major (M5) | §4.4 campaign |

### 2.4 Roads, bridges and trekking routes

| # | Feature | Real world | Source data | Pipeline today | Runtime / milestone | Gap | Action |
|---|---|---|---|---|---|---|---|
| R1 | Winding road geometry | Rajpath, BP, Siddhartha; rim: Godavari–Phulchoki F24, Nagarkot F28/F98, Kakani F76, Sundarijal F26, Dakshinkali | `highway=*` 291,039 km · 9,044 km; hairpins 153 k · ~4.8 k; Rajpath 709 °/km | Every vertex kept, nothing simplified | Mitred corners pinch (776 valley vertices below half width); F24 apex radius 3.6 m, too tight for a bus | minor | 1.4 (arc smoothing) |
| R2 | Winding roads from afar | Switchbacks on facing slopes read from km away | — | ROAD only in L10; L9 and L8 have 0 road records | Roads vanish beyond 0.75–1.75 km | major | D7, 1.4 (W3) |
| R3 | Road vertical profile | Benched carriageways, level across | DSM only; `incline`, `embankment`, `cutting` < 1% | No profile. On the DSM: Rajpath grade > 20% on 14.3%; 1,192 of 2,076 km of valley unclassified road have > 1 m cross-fall; 11,372 valley spots block a taxi on dirt; 15.4 km of stacked switchback legs | Ribbons drape on terrain. Conformance is in ROADMAP 1.3 but in no M1 wave | major | D7 (RPRF), 1.3 |
| R4 | Width, lanes, junctions | Ring Road 4–8 lanes; hill highways 7–10 m | `width` on ≤ 7% of road km; `lanes` on 12–20% of major roads | Parsed, not inferred | Class defaults; lanes ignored; no junction caps | minor | 1.4, 1.8 |
| R5 | Tunnels, roads under construction | Nagdhunga Tunnel 2.68 km; Kathmandu–Terai Fast Track | Valley tunnels 64; `highway=construction` 269 km (Fast Track 134.7 km) | Tunnels routable; construction dropped | Tunnels not drawn: the route line and car go over the ridge | major | 1.10 (guard), D1, tunnels M2 |
| R6 | Road bridges, flyovers, overbridges | Every river crossing; pedestrian overbridges | 17,679 bridge ways · 815 (pack 856); 111 valley bridges over roads | Bridge is a bool; `bridge:structure` dropped; cut at tile borders (45 valley bridges) | Deck from per-piece terrain: 85–87% have < 2 m clearance (decks lie on rivers); dips at tile borders up to 18.4 m; overpasses coincide with the road below | major (T1→T3) | D7 (BRDG), 1.4 |
| R7 | Suspension footbridges (jholunge) | ~10,000 trail bridges (brief) | 1,797 foot + 80 road identified (median span 77.8 m); 4,622 foot bridges untagged · 25 valley | Structure dropped | Flat 2 m ribbon; kit M2 | major | D7, 1.4 (greybox W2) |
| R8 | Trails, tracks, fords | Porter paths, stone trails, jeep tracks | path 107,451 km, track 43,953 · 1,278 / 956 km | Full geometry; `sac_scale` inferred; FORD flag | Walker blocked above 40° ground slope (39.7 km of valley path) | minor | 1.7, 1.4 |
| R9 | Steps | Swayambhu's eastern stairway | `highway=steps` 115 km · 23.4 km | Direction and `step_count` not carried | Flat ribbon; stone-step kit M1 P0 | minor | D7, 1.4 |
| R10 | Trail difficulty | Popular treks T1–T3 | `sac_scale` on 2.21% of path km | Inference over-grades 59.5% of tagged trek km; the ≥ 4,500 m → T4 rule bars horses from 156 km of ≤ T3 trail | Used in M3a | major (M3a) | D14 (M2) |
| R11 | **Named trekking routes** | Annapurna Circuit, ABC, Poon Hill, EBC, Langtang; rim hikes | 127 hiking + 11 foot relations, 4,089 km. In the pack: Chisapani–Nagarkot r9400905 (21.1 km), Sundarijal–Chisapani (9.3), Shivapuri–Chisapani (9.5), Nangi Gumba–Shivapuri (4.2), Heritage Walk r2721476, Bhaktapur Tourist Routes 1–2, Helambu (16.9 of 43.5 km). No relation: Poon Hill, Upper Mustang, Tamang Heritage, Panch Pokhari, Champadevi | **Every route relation is dropped** (osm_extract.py:352) | None. M3a expects "the OSM hiking relations" | missing (T0→T2) | D2, 1.22, C (`treks`) |
| R12 | Trek infrastructure | Teahouses, checkposts, permits, signposts | Lodging nodes near routes: Annapurna Circuit 496, EBC 180; checkpoints 165; guideposts 207 | `alpine_hut` → TEAHOUSE; teahouse archetype only ≥ 2,500 m; checkpoints dropped | M3a assets | major (M3a) | D1 (M2), C (permits) |
| R13 | Trek ETAs and stamina | Acclimatisation limits daily gain | — | Climb counted only at graph nodes: 36–71% of real ascent | A* W1 | major (M3a) | D14 (GHRG v2) |
| R14 | Railway | Janakpur–Jaynagar | `railway=rail` 155 km; none in the valley | RAILWAY line; railway bridges not flagged | M4 | minor | D7, M4 |

### 2.5 Buildings and objects

| # | Feature | Real world | Source data | Pipeline today | Runtime / milestone | Gap | Action |
|---|---|---|---|---|---|---|---|
| B1 | Footprints | ~0.5 M in the valley; near-complete after 2015 campaigns | 8,219,167 *in Nepal* · 511,189; pack places 542,261 | Exact, unclipped. Duplicates kept (177 pairs > 50% overlap); extra multipolygon parts dropped (49); building nodes ignored | W1 extrusion; roofs ignore holes (163 courtyards roofed over) | minor | D4, 1.5 |
| B2 | Heights and levels | Core 3–6 storeys, Newar storeys 2.1–2.4 m, rim 1–2 | Levels tagged on 3.38% in the valley (0.42% Nepal). Bad values: 50 heights ≤ 2.5 m; `levels=45` hostel (renders 135.5 m) | Tagged always wins; 96.6% inferred; "town" density from 3 buildings/ha, so rim villages get 2–4 storeys | Heights clamped 2–300 m; storey fixed at 3 m | major | D4 |
| B3 | `building:part` | Tiered temples, Boudha, Dharahara | 3,794 · pack 2,425 (Patan DS 368, KTM DS 182, Bhaktapur DS 44) | Dropped; PART flag never set | Not planned | major (T0→T3) | D4, 1.5 |
| B4 | Archetypes | Newar cores in ~20 towns; RCC infill; >2,000 temples | 97.5% `building=yes`; 374 footprints contain a religious POI (321 styled as houses); 8,590 contain shops | MODERN_URBAN 78.8%; ~3,500 buildings in Newar towns outside the rectangle zones (Banepa, Chapagaun, Pharping, Bode, Thecho) | W2 kit-lite | major | D1, D4 |
| B5 | Facades, roofs, materials | CGI and concrete on the rim; tiled Newar roofs | `roof:shape` on 14,022 valley buildings | Rim HILL_VILLAGE gets thatch 7,038 and slate 10,764 (unrealistic in 2026); no frontage edge | Sloped roofs on convex quads only; canopies solid | minor | D4, 1.5 |
| B6 | Hero hiding | — | 14 valley landmarks | LANDMARK only on the exact ref; Boudha stupa and Dharahara outline not hidden | `SkipLandmarks` | major | D5 |
| B7 | The city from afar | Continuous city from Swayambhu, Chandragiri, Nagarkot | 147,499 buildings within 4 km of Asan | No building data in L9/L8 | Buildings vanish beyond 1.25–1.75 km | major | D11, 1.5 |
| B8 | Triangle budget at 1:1 density | 21,029 buildings within 1.25 km of Asan | — | — | W1 extrusion ~18 tris/building ≈ 380 k vs a 95 k Mid slice | major | 1.5, D11, 1.14 |
| B9 | Point-only structures | See L3, L8, L11, L13 | Valley: ~1,250 temples, gompas, shrines, spouts, monuments, gates with no footprint | Exact points | Invisible | major (T1→T2) | 1.18 |
| B10 | High-voltage lines and pylons | 66–400 kV lines across the valley | `power=line` 4,803 km; towers 13,784 · pack 629 towers, 205.6 km | Dropped | No asset | missing (T0→T3) | D3, 1.19 |
| B11 | Distribution poles and wire tangles | Iconic street look | Almost absent: pack 261 poles, 2.8 km of minor line (core needs ~2,600 poles) | — | Asset M1 P0, no item | major | 1.19 (procedural, labelled) |
| B12 | Street lamps | Main roads, ~35–40 m | 1,475 · 327 | Dropped | Asset M1 P1 | major | D3, 1.19 |
| B13 | Benches | Parks, squares | 778 · 113 | Dropped | Asset M1 P1 | minor | D3, 1.19 |
| B14 | Bus stops, shelters, bus parks | Sajha, micro, tempo stops; Gongabu, Ratnapark, Lagankhel | `bus_stop` 1,351 · 379 (pack 395); 24 valley bus-park polygons | Stops dropped; bus-park polygons dropped | 1.8 needs stops and route order | major | D2, D3, 1.8 |
| B15 | Chautari, pati, sattal | Resting platforms and Newar patis | 368 trees named chautari etc.; pack 93 pati/sattal/phalcha | Shelter nodes dropped | Asset M2 | major | D3, 1.19 |
| B16 | Taps and wells | Public taps, wells | Pack: drinking water 384, taps 87, wells 90 | ~380 taps and 90 wells dropped; household springs become discoverables | — | minor | D3, D1 |
| B17 | Prayer wheels | Boudha, Swayambhu, gompa walls | 2 · 0 | — | Hero art | minor | Hero prefabs; rows only on real gompa/stupa walls |
| B18 | Prayer flags | Stupa spires, gompas, passes, bridges | 2 | — | Procedural, M1 P0 | minor | Anchored to real stupas and gompas only; never on Hindu temples |
| B19 | Shopfronts, signals, crossings | Shops on almost every core ground floor | Valley SHOP 19,004 (8,712 inside footprints); signals 60 · 50; crossings 620 (pack) | Shops not linked to facades; signals and crossings dropped | Generic signboards, no rule | major | D4 (BFNT), D3, 1.5, 1.8 |
| B20 | Parked vehicles | Motorbikes on every street | Parking 293 POIs (215 polygons) | Parking polygons dropped | No rule | minor | D3, 1.19 |
| B21 | Markets and bazaars | Asan–Indra Chowk–Kel Tole, Kalimati, Pote Bazar | `marketplace` 362 · 58 (Kalimati w84035029, Ason w326105495; some are shops) | Polygons dropped | Stalls M1 P0, no rule | major | D3, 1.19 |
| B22 | Walls, fences, gates | Compound walls on most plots | Pack: walls 68 km, fences ~17 km, gates 253 | Only city walls and mani walls kept | Fence kit M1 P0 | major | D3, 1.19 |
| B23 | Towers, masts, water towers | Hilltop masts on Phulchoki, Nagarkot, Shivapuri | Towers 413, masts 62 · ~70 | Dropped unless also a building | No assets | major | D3, 1.19 |
| B24 | Brick kilns, chimneys, quarries | Smoking chimneys in the Thimi–Bhaktapur fields | `man_made=kiln` 495 (pack 106); chimney 887 · 135; quarry 1,027 (28.8 km²) | Dropped; quarries a subtle tint | Assets M1 P1 with no data hook | major | D3, 1.19 |
| B25 | Rooftop tanks and solar | Most urban flat roofs | Storage tanks 6,773 (pack 5,969, one Banepa campaign: 35% of mapped buildings); rooftop solar 1,145 (8%) | Dropped | Procedural roof props | minor | D3 (real ones), 1.19 (rates) |
| B26 | Campuses | School yards, TU Kirtipur, Bir Hospital | `amenity=school` 25,681 (6,803 outlines, 17.3 km²; valley 1,885, 3.72 km²); university outlines 38; hospital outlines 467 | No AreaKind; schools and hospitals excluded from search | Gate and flagpole assets, no rule | major | D3, 1.19, O12 |
| B27 | Hydropower | Dams and penstocks along highway rivers | 46 hydro plants (+21 Kyanjin lodges mistagged); dams 217; weirs 261; pipelines 39.8 km; mills 64 · Khopasi dam w873336005 | Dropped | None | major (M2) | D3 |
| B28 | Stadiums, golf, pools, zoo | Dasharath Rangasala (in core), TU ground, Surya Golf, Central Zoo | Stadiums 49; golf 4; pools 210 · 92; zoo w52885782 | Stadiums → PITCH tint; golf and pools dropped | — | major | D3, 1.19 |
| B29 | Police, army, prisons, embassies | Bhadrakali HQ, Chhauni, embassies in Maharajgunj | Police 899 · 183; MILITARY 158 polygons (31 pack); prisons 32; diplomatic 48 · 46 | MILITARY not drawn; routable inside | 9+ rating applies | major | 1.19, D14, O9 |
| B30 | Provenance framework | — | ~15 k OSM-positioned objects in the pack are dropped today | `SEED` only: "prop instances are never stored" (DATA_FORMATS §1.9) | No props item in M1 | major | D3, D10, 1.19, 1.13 |

### 2.6 Adventure spots

| # | Feature | Real world | Source data | Pipeline today | Runtime / milestone | Gap | Action |
|---|---|---|---|---|---|---|---|
| A1 | Bungee | The Cliff, Kushma (228 m drop per OSM) on the Kusma–Balewa bridge; The Last Resort, Bhote Koshi (operating status after the 2021 floods: verify) | `attraction=bungee_jumping` 2 (n9044782738, n13055717501); bridge w870647006 (OSM length 520 m, 493 m measured). Bhote Koshi site absent | Only `sport=bungee` is read (0); a name regex yields BUNGEE 5, 3 false (two hamlets, a school). Landmark resolves (status "weak") to the wrong bridge, Kushma–Gyadi w230721093. Kushma lies outside every planned region box | M3b | major | D1, D5, regions, C, §4.4 |
| A2 | Paragliding | Sarangkot; Bandipur, Sirkot, Mansangkot, Dharan; valley: Shankharapur | `sport=free_flying` 12. Sarangkot: three take-offs OSM labels "old"; current Mandredhunga w1156893063; landing n10758437454. Valley: n3944316658 | PARAGLIDING 23, of which 8 are offices, schools or parking. Take-off vs landing dropped. Landmark bound to an "old" take-off | M2 activity | minor | D1, C (ground-truth with pilots) |
| A3 | Ziplines | ZipFlyer Sarangkot–Hemja (~1.8 km, verify), Chhebetar, Dhulikhel | `aerialway=zip_line` 10: 1 recreational, river-crossing wires, 2 mistags (valley w913294084, a kinked 1.7 km line at Naikap; w704254793, a ring). ZipFlyer only as a building | The valley pack ships the mistag as ZIP_LINE plus a POI; the landmark regex cannot match "Zip Flyer" | M2 P2 | major | D1, D5, §4.4 |
| A4 | Passenger cable cars | Chandragiri (2.29 km, 953 m rise), Manakamana (2.8 km), Annapurna, Maulakalika, Butwal/Lumbini, Kalinchowk | 6 gondola + 2 cable_car ways (13.9 km); pylons on line vertices; Maulakalika mapped twice; Kalinchowk absent | 2D line, no heights; duplicate station POI at Chandragiri | Chandragiri assets M1 P2, but no mesher and no work item: **it will not render** | major (T1→T3) | 1.21, D1 |
| A5 | Rafting and kayaking | Trishuli, Bhote Koshi, Seti, Kali Gandaki, Sun Koshi, Marsyangdi, Karnali, Tamor, Arun | River geometry excellent (Trishuli r4839538 163.6 km, Sun Kosi 279.8, Karnali 541.9). Activity tags: `whitewater` 4, `attraction=river_rafting` 5, rapids 2 | RAFTING 3; one is a Thamel company office in the valley pack (ADR-010) | No ROADMAP item | major | C (river sections), D1, M2 float, M5 whitewater |
| A6 | Lake boating, ferries | Phewa doongas, Tal Barahi, Begnas | Phewa r8202581 (`boat=yes`); 6 ferry routes on Phewa (5.88 km); 9 Phewa ferry terminals; boat rental 4; piers 39 | Ferries, piers, terminals and boat access dropped | M2 | minor | D3, M2 |
| A7 | Jungle safari | Chitwan, Bardiya: jeep, canoe, walks, watchtowers | Park relations; watchtowers, canoe departures, breeding centres mapped; jeep routes are untagged tracks | SAFARI 4: none is a safari feature (two booking offices, a bar, a Thamel agency in the valley pack) | M4 | major (M4) | C, D1, O11 |
| A8 | Canyoning | Jalbire, Kalikhola, Sundarijal (verify) | `attraction=canyoning` 1 | Dropped | None | missing | C (M2+) |
| A9 | Climbing | Nagarjun/Hattiban crags (verify); trekking peaks (Island, Mera) | `sport=climbing` 6 | Dropped | None | missing | C; trekking peaks M3b |
| A10 | Mountain biking, cycling | Shivapuri, Nagarjun, Chandragiri trails | `mtb:scale` on 374 ways (334 km) · 57 ways (28.9 km); 4 bicycle route relations; bicycle rental 24 · 8 | MTB tags not carried; routes dropped | No bicycle in M1 | minor | D2, O14 |
| A11 | Horse riding | Pokhara ponies; Mustang | `leisure=horse_riding` 4 (Wind Horse Stables in the valley) | Dropped | Horses M3a | minor | M3a |
| A12 | Air: mountain flights, helicopter, ultralight, balloon, skydiving | Everest flights from TIA; Pokhara ultralight (verify) | Aerodromes 58, helipads 364 | — | M3b | minor | M3b, C |
| A13 | Theme parks, zoo, fun parks | Central Zoo, Kathmandu Fun Park | `theme_park` 22 (mistags such as "Ganatantra Stambha", "Dailekh Road"); zoo 4 | THEME_PARK discoverable without a filter | — | minor | D1, C |
| A14 | Activity data model | — | — | PoiKinds 500–505 are mostly noise; no activity records | `Ghumante.Activities` is an empty marker | major | D12 (`activities`), 1.21 |

### 2.7 Open-world systems

| # | Feature | What the format needs | Data today | Pipeline today | Runtime / milestone | Gap | Action |
|---|---|---|---|---|---|---|---|
| S1 | Seamless 1:1 streaming | No loading screens | Packs | Done (M0) | 1.2 W1 | ok | — |
| S2 | Hop on and off vehicles | Walk, motorbike, taxi, bus, cable car, boat, flight | Taxi stands 25 (valley) | — | Walk and motorbike W1, taxi W2, bus ride-along M2, cable car W3 stretch, boats M2, flights M3b | ok for M1 | 1.21 |
| S3 | Living streets: traffic | Lane graph, one-ways, turns, junction control | `oneway` kept; 90 turn-restriction relations; signals 50 (valley junctions are mostly police-controlled) | Restrictions dropped | 1.8 W2 | minor | D2, 1.8 (traffic police at major chowks) |
| S4 | Buses on real routes | Real lines, stops, terminals | 54 bus, 11 microbus, 9 tempo relations; only 15 PT relations carry stop members | Dropped | 1.8 is blocked as written | missing | D2, C (stop order) |
| S5 | Pedestrians and crowds | Density where real life is: bazaars, temples, schools | Shop, temple, market and school POIs | No density field | §7.7 | minor | 1.19 (density from POI rasters) |
| S6 | Map, minimap, waypoints, fast travel | Route to anything findable | — | Admin entries off-map; tunnels routable | 1.10 W1–W2 | minor | D6, 1.10 |
| S7 | Discovery, fog, passport | Discover real places | 0 discoverable settlements; noisy discoverables | No admin polygons in packs | W2; passport M2 | major | D1, D6, 1.20 |
| S8 | Side activities | Non-violent things to do at real spots | Chandragiri cable car, rim hikes, heritage walks, kora, viewpoints | Routes dropped; activities empty | None in M1 | major | 1.21, 1.22, 1.20 (photo spots) |
| S9 | Day, night, weather, seasons | Seasonal paddy, snowline, dry streams Nov–May, kiln smoke, monsoon mud | Intermittent flag on 20.5% of streams | — | 1.9 W1 | minor | Hooks in 1.16, 1.17, 1.19 |
| S10 | Ambient sound by place | Falls, rivers, temple bells, bazaars | — | — | `ghm_amb_water_*` M1 P0, no item | minor | 1.16, 1.18, 1.19 |
| S11 | Airports and airstrips | TIA (most visitors' first place), Lukla, STOL fields | Aerodromes 58, runways 47.7 km, helipads 364. Core: AERODROME 2.65 km², runway w340948564 (45 m) | Runway lines never meshed; AERODROME a hidden subtle tint; apron+building w1368706500 becomes an 11.6 ha, 4.5 m HILL_VILLAGE block on TIA; a travel agency tagged aerodrome becomes a fast-travel hub | TIA greybox M1 P2 | major | 1.21, D1, D3 |
| S12 | World edge and border | A graceful edge at the real border; crossings | 71 `border_control`; extract includes Indian and Tibetan strips | No Nepal mask: a Terai region would ship Indian roads and POIs | None | missing (M2) | D15, O13 |
| S13 | Restricted compounds | Neutral, non-enterable | MILITARY 31 (pack) | Not drawn, routable | — | major | 1.19, D14, O9 |
| S14 | Festivals and jatras | Indra Jatra (Basantapur), Bisket (Bhaktapur), Rato Machhindranath (Pulchowk–Jawalakhel), Ghode Jatra (Tundikhel) | Anchor places in OSM; no event data; "festival" appears 0 times in ROADMAP, ARCHITECTURE and M1_PLAN | — | None | missing (M2) | C (`festivals`), O11 |
| S15 | Animals | Cows, dogs, Swayambhu monkeys; Shivapuri wildlife | Biomes, protected areas | — | Cows 1.8, monkeys 1.6, wildlife M4 | minor | Spawn-table switch on protected-area entry (M2) |
| S16 | Interiors | — | — | — | Hero courtyards only | by design | — |
| S17 | Provenance and coverage QA | "Is this real?" answerable for every object | — | Census not in repo | 1.13 inspector | minor | D13, 1.13, 1.23 |

---

## 3. Gaps ranked by impact, and the work

### 3.1 Ranked gaps

Ranked by how much of the vision they block, how visible they are in the M1 slice, and how many places they affect.

| Rank | Gap | Vision words | Size | In M1? | Fix |
|---|---|---|---|---|---|
| 1 | **Route relations dropped**: no named treks, no real bus routes | trekking routes; living streets | 138 hiking/foot relations (4,089 km); 74 bus/micro/tempo relations | Yes: 7 rim hikes, 1.8 buses | D2, 1.22, 1.8 |
| 2 | **Mapped objects not drawn**: point-only sacred and heritage sites; ~15 k street objects dropped | objects, landmarks, monuments | ~1,250 valley footprint-less structures; 79% of Nepal's places of worship are points | Yes | 1.18, D3, 1.19 |
| 3 | **Monuments drawn wrong** | monuments, landmarks | Plinth heights (24 of 36 valley religious heights); 2,425 parts dropped; 4 of 14 heroes hide anything; Boudha a 4 m pyramid; Dharahara a floating disc; 0 shikharas | Yes, inside the M1 acceptance ride | D4, D5, D1, 1.5, 1.6 |
| 4 | **Water not drawn, or running uphill** | waterfalls, scenery | Rivers unmeshed; 22.5% of river length > 2 m above upstream; 51% of lakes tilted > 5 m; no waterfall in M1 | Yes (Bagmati at Pashupati) | 1.16, D8 |
| 5 | **Forests are tints** | forests | 55.7% of valley L10 area; no vegetation item | Yes | 1.17, D10 |
| 6 | **Winding roads drive wrong** | winding roads | DSM drape (Rajpath > 20% grade on 14%), decks on rivers, a tunnel route over a ridge, roads gone past 1.75 km | Yes | D7, 1.3, 1.4 |
| 7 | **Places are not places** | every place | 0 discoverable places, no 3D labels, no wards, 18 of 46 admin results off-map, 21% of buildings nameless | Yes | D6, 1.20 |
| 8 | **Skyline incomplete and too low** | scenery, peaks | Everest, Cho Oyu, Manaslu beyond the far clip; L5 −114 m; no labels | Yes | D9, 1.9 |
| 9 | **Adventure barely in data** | adventure spots | ~10 correctly tagged sites in Nepal; Chandragiri will not render | Partly | D12, 1.21, D1 |
| 10 | **Whole categories missing** | buildings, objects | Airports (TIA in core), campuses, kilns, hydropower, stadiums, markets, bus parks, restricted compounds | Yes | D3, D1, 1.19, 1.21 |
| 11 | **No city beyond the near ring; density over budget** | buildings | 147 k buildings within 4 km of Asan, none drawn past 1.75 km; ~380 k triangles vs 95 k | Yes | D11, 1.5, 1.14 |
| 12 | **Sacred rules and review** | (respect) | 1.26 km of motor road in compounds; consultant unnamed | Yes | D14, 1.15, O10 |
| 13 | **Discovery noise** | discovery | 1,827 springs, 199 gardens, houses as viewpoints and palaces | Yes | D1 |
| 14 | **OSM-side holes beyond the valley** | waterfalls, adventure, monuments | Mani walls 0; ~90% of tea; Bhote Koshi, Gorkha, Kalinchowk, ZipFlyer cable absent | No | C, §4.4 (M2–M5) |
| 15 | **No world edge; no festivals** | open world | No Nepal mask; no event layer | No | D15, C (M2) |

### 3.2 Pipeline and format work (data track)

**Timing.** Nothing here touches `pipeline/`, `shared/` or the C# readers until **M1 Wave 1 lands**. Format changes then come in four batches, so the C# side changes a few times, not continuously. Each batch ships with regenerated `shared/enums.json`, C# enums and golden files, and each new chunk lands together with its C# reader and golden test in the consuming track.

| Batch | When | What | Compatibility |
|---|---|---|---|
| **F1** | First days of W2 | Enum and flag appends (list below), `SEED` definition change (D10), classifier and search-content fixes (D1, D6 entries) | No layout change; `ENUMS_VERSION` bump |
| **F2** | W2 | New tile chunks `PROP`, `ADMN`, `RTES`, `RPRF`, `BRDG`, `FALL`, `AATR`, `BFNT`; new region files `.ghrt` (routes), `.ghcd` (curated DB), `.ghsk` (skyline ring + peak table) | Additive: readers skip unknown fourccs (DATA_FORMATS §1), so no `GHT1` bump |
| **F3** | W3 | L9/L8 content: `BLKS` building blocks, display-only ROAD/LINE LOD copies, `STEP`; pack-size rebalance (O8) | Additive, plus ADR-011 change if L10 heights are dropped |
| **F4** | M2–M3a | `GHSI` v2 (local level, ward, region id), national index, Nepal mask; `GHRG` v2 (ascent/descent per edge) | Version bumps |

**Enum and flag appends (F1).** Ids are indicative; final values are assigned once, append-only.

| Enum | Append |
|---|---|
| PoiKind | STATUE 127, GHAT 128, PARAGLIDING_LANDING 506; M2: CHECKPOINT 309, REST_STOP 310, BORDER_CROSSING 311, GUIDEPOST 414 |
| LineKind | TREE_ROW 14, CLIFF 15, GHAT_EDGE 16, POWER_LINE 17, WALL 18, FENCE 19, HEDGE 20, RETAINING_WALL 21, KERB 22, FERRY 23, DAM 24, WEIR 25, PENSTOCK 26 |
| AreaKind | MARKETPLACE 27, PARKING 28, BUS_PARK 29, CAMPUS 30, KILN 31, QUARRY 32, STADIUM 33, GOLF 34, POOL 35, APRON 36, POWER_PLANT 37 |
| RoadClass | CONSTRUCTION 18 (no access) |
| Biome | GLACIER_DEBRIS 28 |
| BuildingArchetype | RANA_PALACE 20 |
| BuildingFlags (u8, 3 bits left) | HAS_PARTS 32, TAG_SUSPECT 64, OPEN_CANOPY 128 |
| PoiFlags (u8, 4 bits left) | HAS_FOOTPRINT 16, LANDMARK_LITE 32, INFERRED 64 |
| AREA flags | bit1 HERITAGE_ZONE, bit2 SACRED_NO_VEHICLE |
| LINE flags | bit4 BRIDGE (railway), bits 5–6 voltage class |
| New | BridgeKind (BEAM, TRUSS_BAILEY, ARCH, SUSPENSION, SIMPLE_SUSPENSION, WOODEN_LOG, CABLE_STAYED, BOARDWALK, LOW_WATER); ObjectKind for `PROP` |

**Work items.** P0 = needed for the M1 slice, P1 = M1 if capacity allows, P2 = later milestone.

| ID | Work | Format | Pri | When | Closes |
|---|---|---|---|---|---|
| **D1** | **Classifier and search fixes** (`tags.py`, `buildings.py`, `search_index.py`): HERITAGE_SQUARE only with heritage, durbar name or wikidata; PALACE only with palace-like names and not houses; read `attraction=*`, `whitewater=*`, `free_flying:site`; bungee regex `\bbung(ee\|y)\b`; no activity kinds on offices, shops, schools, parking; artwork → STATUE; observation/clock towers → POI; stupa, shikhara, chorten, gompa vs bahal archetype rules; religious and shop POIs pass use to the containing footprint; ghats; named pokhari → LAKE; crematorium never SHOP; discoverable filter (no household springs, unnamed parks, house-like viewpoints, "old" take-offs); drop unnamed GLACIER and RIDGE POIs; HOT_SPRING only with the tag; FARM and isolated dwellings out of search; AIRPORT hub only with runway, IATA/ICAO or wikidata; no building for `aeroway=apron`; construction roads; THEME_PARK filter | F1 enums | P0 | W2 | P2, P4, P13, P16, L4–L11, L14, L15, N13–N15, A1–A3, A5, A7, A13, S11 |
| **D2** | **Route relations**: keep `type=route`, `superroute`, `route_master` (hiking, foot, bus, microbus, tempo, share_taxi, bicycle, mtb, ferry, road) and `type=restriction`. Region routes file `.ghrt` (ordered chains, km, ascent from RPRF, high point, passes, stops); per-tile `RTES` (route index → way ids); routes in GHSI | F2 | P0 | W2 | R11, S3, S4, B14, A10 |
| **D3** | **`PROP` chunk + new Line/Area kinds**: every OSM tree (species class pipal, bar, broadleaf, conifer, palm; chautari flag), power towers and portals, lamps, bus stops, shelters, benches, taps, wells, signals, crossings, gates, chimneys, masts, towers, rooftop tanks and solar, artwork. Record: kind, x/z cm, yaw, height dm, osm_ref, flags. Lines for walls, fences, power, cliffs, tree rows, dams, weirs, penstocks, ferries. Areas for markets, parking, bus parks, campuses, kilns, quarries, stadiums, golf, pools, aprons, power plants. Valley cost ~100–150 KB | F1 + F2 | P0 | W2 | N3, N12, B10–B16, B19–B30, A6, S11 |
| **D4** | **Buildings**: extract `building:part` (PART records, HAS_PARTS on hosts); dedupe overlaps; keep every outer of multipolygons; plausibility gate (height < 2.5 m without parts, height/levels outside 1.8–6 m, levels > 20) → TAG_SUSPECT and inference; per-archetype storey heights (Newar 2.3 m); level priors calibrated on the 18,844 tagged valley buildings and GHSL aggregates; Newar zones traced from real historic cores, plus missing Newar towns; rim roof priors (CGI, concrete); `BFNT` frontage edge and shopfront mask | F1 + F2 | P0 (parts, gate), P1 (priors, BFNT) | W2 | L3, L12, B1–B5, B19 |
| **D5** | **Landmarks and heritage**: `landmarks.yaml` schema (`osm` pins, `compound`, `hide` refs or polygon, `manual: {lon, lat, source}`, `yaw_deg`); pins: Boudha w56688295 + compound w56688296, Patan r4557971, Muktinath n1431996477, Kushma n9044782738 / w870647006, Kalinchowk near point, Sarangkot current take-off, ZipFlyer regex; hide w1413193271 and Dharahara parts; LANDMARK on every building inside a hide zone; landmark POIs for any pack whose box contains them; `type=site` → HERITAGE_ZONE; landmark-lite tier (LANDMARK_LITE flag + search bonus); regenerate `landmarks.md` and the ASSET_MANIFEST §5 OSM column; tests that no unhidden building intersects a hero | F1 | P0 (before 1.6) | W2 | L1, L2, L12, L17, B6, A1–A3 |
| **D6** | **Places and admin**: admin entries at `admin_centre` and never outside coverage (fixes a W1 bug); DISCOVERABLE on settlements (~690 valley entries); wards on (default admin levels and search levels); `ADMN` chunk (exact rings for levels 4/6/7/9; display-only generalised copy for the country map in M2); settlement anchors for unnamed places and building clusters, named by ward; `PlaceFeature` carries wikidata and population; joined key and ाे→ो normalisation mirrored in C# with golden tests | F1 + F2 | P0 | W2 | P1–P15 |
| **D7** | **Roads**: `RPRF` road profile (DSM every 4 m, lower envelope in tree/built pixels, grade caps 12/15/20/25% by class, shared z at junctions, separation of stacked legs); `BRDG` decks (z at abutments, ≥ water + 3 m, ≥ 5.5 m over roads, one deck across tile seams, BridgeKind, span); suspension inference (untagged foot bridges ≥ 30 m over a waterway, flagged); railway bridge flag; display-only road LOD in L9 (motorable, 2 m DP) and L8 (tertiary+ and named trails, 4 m DP); `STEP` (direction, step count) | F2, F3 | P0 (RPRF, BRDG), P1 (LOD, STEP) | W2–W3 | R2, R3, R6, R7, R9, R14 |
| **D8** | **Water**: river width (tag, else riverbank area ÷ length, else class default scaled by upstream length); `FALL` (waterfall snapped to its stream, top/bottom xyz, drop, width class); `AATR` area attributes (ele, depth, intermittent, crop/terraced/irrigated, boat); OSM lakes ≥ 1 ha and glaciers in horizon tiles; document downstream point order (DATA_FORMATS §1.5) | F2 | P0 (widths, FALL), P1 | W2 | N4–N7, L14, A6 |
| **D9** | **Terrain and skyline** (DEM-only except the peak table): `.ghsk` skyline ring (360° × 3 bands to 200 km) and skyline-peak table (named OSM peaks with `ele`); peak-preserving horizon prefilter (L5 summit loss < 20 m); biome slope at one fixed scale; GLACIER_DEBRIS; terrace slope cap 35°; pass crest snap (M3a); landslide BARE_SOIL (M2); DSM-to-DTM canopy correction after a licence check (M2) | F2 | P0 (ring, peaks), P1 | W2–W3 | N8–N12, N17 |
| **D10** | **Stable scatter seed**: `tile_seed = FNV-1a(tile_key, ruleset)`, without `data_version`, so trees and props stay put between builds | F1 | P0 (before any scatter ships) | W2 | N2, B30 |
| **D11** | **City from afar**: `BLKS` at L9/L8 (block polygons or a height/coverage raster, display-only); per-block merged prisms for the near-ring LOD2 band; pay bytes from HGHT (O8) | F3 | P1 | W3 | B7, B8 |
| **D12** | **Curated DB**: `config/curated/*.yaml` (`heritage_sites`, `heritage_overrides`, `activities`, `treks`, `admin_overrides`, `names_ne`, later `festivals`) compiled to a per-region `.ghcd`; schema in §4.2; LICENSES rows | F2 | P0 (format + valley records) | W2 | L1, L3, L17, R11, A1–A14, P6, P12 |
| **D13** | **Content coverage report**: port the census into the pipeline; per category: OSM count, pack count, tier reached; nightly diff; OSM fix-task export (GeoJSON) for the community | — | P1 | W2 | All (measurement), S17 |
| **D14** | **Routing rules**: drop motor modes inside SACRED_NO_VEHICLE and MILITARY/prison interiors (GHRG content only, W2); M2: `sac_scale` recalibration on the 772 km tagged trek set, horses to T3; M3a: GHRG v2 per-edge ascent/descent, Tobler/Naismith | F1 (W2), F4 | P0 (W2 part) | W2, M2, M3a | L16, R10, R13, B29 |
| **D15** | **Nepal mask**: clip vectors and search to the national polygon + 50 m (keep terrain beyond); BORDER_CROSSING kind | F4 | P2 | M2 (needs O13) | S12 |
| **D16** | **National gazetteer** (~27 k entries, ~3.1 MB, with region ids) and GHSI v2 | F4 | P2 | M2 | P11, P15 |

### 3.3 Runtime work (ROADMAP ids)

Existing items get explicit additions; new items are 1.16–1.23. Tracks follow M1_PLAN (A Core streaming + meshing, B Core driving, C World runtime, D Explore gameplay).

| ID | Work | Wave | Track | Needs |
|---|---|---|---|---|
| 1.3 + | Near-field conformance (it is in ROADMAP 1.3 but in no wave): benched road corridor from RPRF with cut/fill ≤ 45° and 2.5% camber; lake flattening; river channel carve; runway flattening | W2 | A | D7, D8 |
| 1.4 + | Hairpin arcs within 1.5 m of the OSM line, inner-edge widening; decks from BRDG with piers and railings; suspension bridge greybox (towers, sag ≈ span/30, flags); stone steps; trail width and visibility; L9/L8 road LOD ribbons | W2 (LOD W3) | A | D7 |
| 1.5 + | Render `building:part`, skip hosts with parts; roof holes; Newar storey heights; shopfronts on real shop frontages; distance bands inside the near ring (kit ≤ 80 m capped, extrusion ≤ 250 m, merged blocks beyond); L9 city blocks | W2 (bands, blocks W3) | A | D4, D11 |
| 1.6 + | Hide zones; Boudha on its real stupa; landmark-lite greybox from parametric pagoda, shikhara and stupa generators | W2 (lite W3) | C | D5, D12 |
| 1.8 + | Buses on the real routes with bus parks as terminals; turn restrictions; traffic police at major chowks | W2 | C | D2, D3 |
| 1.9 + | Skyline impostor ring past the far clip with earth curvature (restore the ROADMAP wording in M1_PLAN); peak labels where the line of sight is clear; runtime summit cones to OSM `ele` | W2 | C | D9 |
| 1.10 + | No result outside playable tiles; tunnel edges excluded until tunnels render; arrival radius and "walk the last N m" hint; `max_snap_m` "no road: trek" | W2 | D | D6 |
| 1.13 + | Inspector shows provenance: OSM ref, curated id, or "procedural (seed …)" | W2 | C | D3, D12 |
| 1.15 + | Consultant named before W2 sacred placements; sign-off is a public-build gate | W2–W3 | — | O10 |
| **1.16** | **Water**: flowing river and stream ribbons with real widths and downstream flow; WaterConformance (monotone surface along flow, carved channels, Chobhar and Pashupati gorges); flat lakes and ponds; valley waterfalls as cascades with plunge pools, mist and sound; weirs as weirs; dry beds Nov–May | W2 (cascades W3) | A + C | D8 |
| **1.17** | **Forests and trees**: clump trees near, impostor clusters mid, canopy shell far, masked by real BIOM and FOREST; species rules in Core (biome × elevation band × aspect); every OSM tree at its position (pipal, bar, chautari); paddy and terrace look on real cropland | W2 (instancing, paddy W3) | A + C | D3, D10 |
| **1.18** | **POI-anchored structures**: a deterministic prop at every structural POI without a footprint (shrine variant by name and religion, small pagoda, chaitya, gompa, hiti pit, statue, column, welcome arch spanning the road, ghat steps), facing the nearest road; hiti terrain stamp | W2 (stamp W3) | C | D1, D3 |
| **1.19** | **Props and compounds**: OSM objects from `PROP` at their real positions (pylons with catenary spans, lamps, stops, benches, taps, signals, crossings, gates, walls and fences, kilns and chimneys, masts, rooftop tanks and solar); then labelled procedural dressing (poles and wire tangles, tanks and solar at measured rates, parked bikes, stalls inside real markets); campuses, stadiums, golf, pools; neutral restricted compounds | W2 (OSM), W3 (procedural, compounds) | C | D3, D10 |
| **1.20** | **Place presence**: 3D labels from GHSI by importance (city 20 km, town 8 km, village 3 km, tole 600 m); map and minimap label hierarchy; bilingual area banner on entering a ward, municipality, settlement or protected area (raises `PlaceEntered`); settlements as discoverables and fast-travel targets; curated viewpoint photo spots | W2 | D | D6 |
| **1.21** | **Aerialways and airfields**: Chandragiri cable car (towers at the OSM pylons, catenary chords, stations, moving cabins, visible from the mid ring); ride-along as a stretch; TIA runway and taxiways with markings, flattened, fenced airside | W2 (ride W3 stretch) | A + C | D1, D3 |
| **1.22** | **Named routes**: valley hikes, heritage walks and kora circuits selectable on the map; route ribbon, waymarks, start and end markers, distance and ascent | W2 | D | D2, D12 |
| **1.23** | **Coverage gate**: automated checks for the slice (§5.3) in `core-tests` and the pipeline report | W2–W3 | all | D13 |

### 3.4 Later milestones

| Milestone | Coverage added |
|---|---|
| M2 | Terrace geometry; Nagdhunga and other tunnels; Prithvi rest stops and bus parks; landslide scars; Manakamana and Annapurna cable cars; Phewa boats and ferries to Tal Barahi; Sarangkot paragliding with real take-off and landing; Devi's Falls; a gentle Trishuli raft float; Prithvi-corridor hydropower; national gazetteer and GHSI v2; ADMN passport stamps; Nepal mask and border edge; festivals calendar with the valley jatras; canyoning (curated); bicycle (O14); procedural chautari only at trail junctions, unnamed |
| M3a | Treks from RTES with teahouse stops and checkposts; `sac_scale` recalibration and GHRG v2; passes on real crests; chortens and mani walls from OSM campaigns (O3); Kushma bungee region; horses and mules |
| M3b | Lukla's sloped runway (curated thresholds); helipads; mountain flights; the Bhote Koshi bungee (OSM-first); debris glaciers; trekking peaks |
| M4 | Safari from curated records keyed to park relations and towers; railway; barrages; Terai paddies (WorldCereal, if the licence checks out); border crossings; Lumbini and Tilaurakot |
| M5 | Whitewater and kayak minigame; Hyatung Falls; tea gardens after a mapping campaign; Kalinchowk cable car; Lo Manthang wall; ~1,000 landmark-lite sites nationally; national QA of nameless settlements |

---

## 4. Data enrichment and curation

### 4.1 Licence-compatible sources

| Source | Licence | What we take | Where it lives | Status |
|---|---|---|---|---|
| OpenStreetMap | ODbL 1.0 | Everything placed in the world | Packs (ODbL) | In use. Contribute fixes back (§4.4). |
| Copernicus GLO-30 | Copernicus DEM licence | Terrain; derived road profiles, waterfall drops, skyline ring | `HGHT` and DEM-derived chunks (`RPRF`, `FALL`, `.ghsk`) | In use. [lawyer] whether DEM values keyed to OSM features (RPRF, FALL) keep layer separation. |
| ESA WorldCover 2021 | CC BY 4.0 | Biomes, canopy mask | `BIOM` | In use |
| **Wikidata** | **CC0** | Devanagari labels, QIDs, WHS ids (P757/P1435), local-level labels, coordinates (always cross-checked), the rare heights (Boudha only of 17 checked) | Curated DB; CC0 may also merge into the ODbL DB | **Add a LICENSES row** (courtesy credit) |
| Wikimedia Commons | Per file (CC BY, BY-SA, PD) | Visual reference for artists only | Reference boards; never shipped, never a source of facts | Add a policy line to LICENSES §4 |
| GeoNames | CC BY 4.0 | Names for nameless settlements (87,903 populated places in Nepal) | Separate layer, `name_source=GEONAMES` | [lawyer] CC BY inside an ODbL collective; O4 |
| GHSL GHS-BUILT-H R2023A | CC BY 4.0 (verify) | Aggregate storey priors only | Pipeline config | Verify; LICENSES row |
| ETH Global Canopy Height 2020 | CC BY 4.0 (verify) | Canopy height class; DSM-to-DTM correction | DEM-family chunk | Verify before use |
| ESA WorldCereal 2021 | CC BY 4.0 (verify) | Terai paddy and irrigation (M4) | Raster chunk | Verify before use |
| FABDEM | CC BY-NC-SA | — | — | **Excluded** (non-commercial) |
| HydroSHEDS | WWF EULA | — | — | Excluded (ADR-014) |
| ICIMOD glacier inventory, DFRS forest assessment, KVWSMB hiti list, NTCDB tea data | Unknown or restrictive | QA checklists only | Internal QA | Never imported |
| Operator sites, news, guidebooks | Copyright | Facts we verify (site status, jump height, route sections); no copied text | Curated record with source URL | OK as reference |
| Aerial imagery | Per the OSM-approved imagery list | Only through OSM editing | OSM | OK |

### 4.2 The curated DB

A separate database per region (`.ghcd`), compiled from `pipeline/config/curated/*.yaml`, keyed by **our own ids** (`her.patan.krishna_mandir`, `act.bungee.kushma`, `trek.champadevi`). It holds only facts we own or that are CC0, plus references. Anything computed from OSM geometry ships in the pack instead.

| Field | Meaning |
|---|---|
| `id`, `kind` | Our id and record type (heritage, activity, trek, admin override, name, festival) |
| `name` | `en`, `ne` (human-entered or Wikidata; never machine Devanagari) |
| `osm` | Anchor ref, plus `compound`, `hide` refs where relevant |
| `wikidata` | QID |
| `manual` | `{lon, lat, source}` only when OSM has no object yet |
| `attrs` | Kind-specific: tiers, total height, plinth steps, yaw, gilt roof; fee, `entry_rule` (non_hindu_no_entry, interior_no_photo, shoes_off), `kora` (cw or ccw); river section put-in/take-out; permit; festival date rule |
| `provenance` | Source, licence, URL, "verified on" date |
| `status` | candidate, verify, verified, superseded (OSM now has it) |
| `review` | Reviewer; cultural sign-off for sacred records |

Rendering flags derived from curated refs (for example LANDMARK on buildings inside a hide zone) are game instructions, not facts, and may be written into tiles. Curated facts (heights, tiers, rules) are applied at runtime from `.ghcd`. [lawyer]

### 4.3 Curation workflow

1. **Generate candidates** from the coverage report (D13): name-only waterfalls (106), TAG_SUSPECT heights, wrong landmark anchors, routes without relations, nameless building clusters, discoverable noise, Wikidata items in the region with no OSM link.
2. **Choose the route.** A verifiable fact about the world → fix it in OSM (§4.4). A game fact (tiers, yaw, rules, fees, activity sections, festival dates) → curated DB. Anything else → it does not exist in the game.
3. **Record** it with provenance and `status: verify`. Cross-check every Wikidata coordinate against OSM or imagery (Khokana's QID is about 50 km off).
4. **Review** by a second person; the cultural consultant reviews sacred records (1.15).
5. **Build**: the pipeline compiles `.ghcd`, sets rendering flags, and reports curated counts per category.
6. **Retire**: when OSM gains the object, the record becomes `superseded` and points at the OSM ref.

### 4.4 Upstream OSM fixes (first batch, valley first)

Done with OSM Nepal and Kathmandu Living Labs, following the OSMF Organised Editing Guidelines. No imports and no automated edits.

| Fix | Objects |
|---|---|
| Wrong local-level names | Dhulikhel r12394199, Mahalaxmi r10535035 |
| Plinth heights on temple outlines (move to `building:part` or correct) | 34 hosts < 3 m, including Nyatapola, Kasthamandap, Taleju, Maju Dega |
| Duplicate and container buildings | 177 overlapping pairs (e.g. r2469558 = w508324945) |
| Houses tagged `historic=castle` | 16 in the valley |
| Mistagged aerialways | w913294084 (Naikap), w704254793 |
| Stale or wrong objects | Dharahara `landuse=construction` w341772537; Durbar High School `military=barracks`; travel agency tagged aerodrome; 21 Kyanjin lodges tagged `power=plant`; apron w1368706500 tagged `building=yes` |
| Missing heritage | Gorkha Durbar footprint; `heritage=1` on Bhaktapur Durbar Square; Mahabouddha temple footprint |
| Missing water features | Jhor and Sundarijal waterfalls; Basmati re-tagged as a weir; ~450 hitis (KVWSMB list as a checklist only) |
| Missing adventure objects | ZipFlyer cable; Kalinchowk cable car; Bhote Koshi bungee bridge |
| Missing trek relations | Poon Hill, Upper Mustang, Tamang Heritage, Panch Pokhari, Champadevi |
| Thin tags | `bridge:structure` on trail bridges; `name:ne` on wards and local levels (from Wikidata, by hand); chautari trees; mani walls (M3a campaign); tea estates (M4 campaign) |

---

## 5. The Kathmandu M1 slice: what the player will see

The M1 slice is the **full `kathmandu_valley` pack** (the base-install region), not only the committed `kathmandu_core` sample, which has no valley rim, no winding roads, no Chandragiri and no Nagarkot.

### 5.1 In M1, each at its real position

| What | Real examples | Count | From | Wave |
|---|---|---|---|---|
| Every building, with real heights where the data is sane, tiered temples from their parts, and the city skyline beyond the near ring | Asan, Patan, Bhaktapur, Kirtipur, Thimi | 542,261 buildings; 2,425 parts | OSM + D4 | W2 (blocks W3) |
| Hero landmarks on the right objects, everything under them hidden | Boudhanath stupa w56688295, Swayambhu, KTM DS, Pashupati, Patan DS, Bhaktapur DS, Changu Narayan, Dharahara | 14 | D5 + 1.6 | W2 |
| Landmark-lite heritage, greybox | Kasthamandap, Kumbheshwar, Kal Bhairab, Golden Temple, Budhanilkantha's sleeping Vishnu, Kailashnath statue, Gaddi Baithak, Bajrayogini, Kirtipur, Panauti, Khokana | ~150 | Curated | W3 |
| Every temple, shrine, stupa, gompa, spout, monument and gate drawn, footprint or prop | Ganesh shrines in every tole; 126 hitis | ~1,250 point sites + footprints | 1.18 | W2 |
| Sacred rules | No vehicles in 329 compounds and the squares; clockwise at stupas; Pashupati main temple not enterable, view from the east bank | — | D14, curated | W2 |
| Every road, rim switchbacks drivable end to end, roads seen from afar | F24 Godavari–Phulchoki (33 hairpins), F28/F98 Nagarkot, F76 Kakani, F26 Sundarijal, Dakshinkali, Rajpath to Thankot | 8,404 km roads, 1,947 km trails | D7 + 1.3/1.4 | W2 (LOD W3) |
| Bridges with real decks; suspension footbridges | Sundarighat (83 m), Koteshwor Jhulungepul (69 m), Kankeswari (64 m) | 856 bridges, 25 suspension | D7 + 1.4 | W2 |
| Named hikes and walks | Chisapani–Nagarkot, Sundarijal–Chisapani, Shivapuri–Chisapani, Nangi Gumba–Shivapuri, Helambu start, Heritage Walk, Bhaktapur Tourist Routes 1–2; curated Champadevi and Nagarkot panoramic | 9 | D2 + 1.22 | W2 |
| Buses on real routes | Valley bus, microbus and tempo lines; Gongabu, Ratnapark, Lagankhel parks | up to 74 relations | D2 + 1.8 | W2 |
| Rivers flowing downhill at real widths; flat lakes and ponds | Bagmati, Bishnumati, Manohara, Dhobi Khola, Tukucha; Rani Pokhari, Kamal and Siddha Pokhari, Taudaha, Nag Daha | 331 km river, 354 km stream | 1.16 | W2 |
| Valley waterfalls with sound | Fung Funge, Nagarkot Waterfall, Lauke; Jhor and Sundarijal (curated) | 5 | D8 + 1.16 | W3 |
| Forests as forests; every mapped tree | Shivapuri, Nagarjun, Chandragiri, Phulchoki, Nagarkot; Swayambhu hill, Mrigasthali | 546.6 km² tree cover; 493–1,135 OSM trees; 63 named pipal/bar | 1.17 | W2 (perf W3) |
| Terraces and paddies on real cropland | Kirtipur, Chapagaun, Pharping, Sankhu slopes | 102 km² terraces, 145 km² valley cropland | 1.17 | W3 |
| Peaks and the Himalayan skyline, labelled, at true height | Phulchoki 2,782 m … Langtang Lirung, Ganesh, Gauri Shankar, Shishapangma; Everest and Cho Oyu where line of sight allows | 11 rim + skyline table | D9 + 1.9 | W2 |
| Every settlement labelled and discoverable; area banners; no nameless area | 4 cities, 19 towns, 206 hamlets, 838 toles; 287 wards; 36 municipalities | 1,045 + admin | D6 + 1.20 | W2 |
| Street objects from OSM, then labelled procedural dressing | 629 pylons, 327 lamps, 395 stops, 135 kiln chimneys, 68 km of walls; poles and wires in the old core | ~15 k OSM objects | D3 + 1.19 | W2 / W3 |
| Chandragiri cable car; TIA runway | w638657930 (2.29 km, 953 m rise); runway w340948564 | 2 | 1.21 | W2 (ride W3 stretch) |
| Discoverables that are real places | Settlements, heritage, curated viewpoints (Nagarkot, Chandragiri, Phulchoki, Kakani, Champadevi, Swayambhu terrace), Shivapuri-Nagarjun NP entry | — | D1, D6, 1.20 | W2 |

### 5.2 Not in M1 (and when)

Waterfalls and rafting outside the valley (M2+), festivals (M2), terrace step geometry (M2), tunnels drawn (M2: Nagdhunga is excluded from routes until then), the border edge (M2), trek gameplay with stamina (M3a), safaris (M4), whitewater (M5), interiors (never, except hero courtyards).

### 5.3 Coverage acceptance for M1 (1.23)

1. 100% of valley religious, heritage and monument POIs render something in 3D (footprint or spawned prop).
2. No unhidden building intersects a hero landmark footprint.
3. No rendered river or stream vertex is above its upstream water level by more than 0.1 m; every lake surface is flat.
4. Zero grade-block spots for the taxi on F24, F28, F98, F76, F26, the Dakshinkali road and the Rajpath valley section; no carriageway cross-fall above class camber.
5. Every bridge deck clears water by ≥ 3 m and roads by ≥ 5.5 m, and has no dip at tile borders.
6. No search result teleports outside the playable tiles; every valley settlement is searchable, labelled and discoverable.
7. The 7 rim and walk relations plus 2 curated routes are selectable and drawable.
8. The coverage report lists, per category, OSM count, pack count and tier reached; T2+ for every in-scope category in §5.1.
9. Triangle budgets per tier hold on the densest tile (10/516/161, Asan); the pack stays within the size target (O8).

---

## 6. Owner decisions

The lead adopted these defaults on 2026-10-05 so work can proceed. The product owner can override any of them; PROGRESS.md records changes.

| # | Decision | Default (adopted) |
|---|---|---|
| O1 | What "every" means | Every in-scope feature mapped in OSM at the pinned snapshot, plus every curated record, at the tier in §1.2. Gaps in reality are fixed in OSM or curated, never invented. Coverage % per category is published each build. |
| O2 | Limits on procedural decoration | Generic, unnamed objects only (trees in real forests, poles and wires, roof tanks, parked bikes, stalls in real markets). Never temples, shrines, chortens, chautari or settlements. Never discoverable or on the map. Labelled in the inspector. |
| O3 | Generated chortens and mani stones along treks where OSM has none (M3a) | No. Run an OSM mapping campaign instead; reconsider only with consultant sign-off, flagged as generated. |
| O4 | GeoNames (CC BY) names for nameless settlements | Not in M1. Use ward names ("Godawari-5"). Revisit in M2 after lawyer review. |
| O5 | EN UI for the 225 places named only in Devanagari | Show a marked machine romanisation in the EN UI; keep ADR-005 (no machine Devanagari in the NE UI). |
| O6 | Should the team edit OSM? | Yes, as a small documented organised-editing effort with OSM Nepal and KLL; no imports, no automated edits. |
| O7 | Pull scenery and adventure forward into M1 | Yes: Chandragiri cable car as scenery (W2) with the ride as a W3 stretch; valley waterfalls (W3); TIA runway (W2); valley hikes (W2). |
| O8 | Valley pack size: 60.95 MB today vs a 60 MB target, and the new chunks add an estimated 2–4 MB | Stop storing L10 heights and upsample L9 (ARCHITECTURE §5.3 lever, ADR-011 change; L10 HGHT is roughly 20 MB by tile count). Keep the 60 MB target. |
| O9 | Police, army, prisons, embassies | Neutral walled compounds with closed gates; no guards, weapons, insignia or flags; not searchable, not labelled; grey "restricted" fill on the map. |
| O10 | Sacred access in a game that does not ask a player's religion | Pashupati's main compound is closed to every player, with the view from the east bank; no vehicles in temple compounds and squares; clockwise at stupas (anticlockwise at Bon sites). Name the consultant before W2 placements (ARCHITECTURE §14 Q9). |
| O11 | Animals and festivals | No elephant-back rides (observation only); no animal sacrifice; the Kumari never shown; no cremation depicted. |
| O12 | Search scope for public institutions | Admit universities, public hospitals and campuses ≥ 1 ha as public places; keep businesses and private schools out (ADR-010). |
| O13 | Border depiction (P15) | Needed before M2, when the Nepal mask and world edge are built. |
| O14 | Bicycle as a vehicle (334 km of tagged MTB trail) | M2. |
| O15 | M1 grows by eight work items (1.16–1.23) plus the data track | Accept roughly 3–4 more weeks (estimate, four tracks plus one data engineer) rather than cutting water, forests or POI-anchored structures. If something must give, cut in this order: cable-car ride, procedural street dressing, terrace and paddy look, L9/L8 road LOD. |

---

## Appendix: errata for other documents

These come from the census and its review. Applied on 2026-10-05: ARCHITECTURE (roadside life, summit correction), DATA_FORMATS §1.9, TAG_COVERAGE_FINDINGS (trek routes), M1_PLAN (W2 conformance and skyline). The rest are fixed when the work that touches them lands.

| Doc | Says | Correction |
|---|---|---|
| reports/TAG_COVERAGE_FINDINGS.md:20 | Named trek routes "are available" | The pipeline drops every route relation (osm_extract.py:352). |
| reports/TAG_COVERAGE_FINDINGS.md | Documents `manual: [lon, lat]` placements | Not implemented in `landmarks.py` or `build.py` (D5). |
| reports/landmarks.md | boudhanath, patan_durbar_square, muktinath "found" | Found, but on the wrong object (wall, node, pond). kushma_bungee is "weak" and on the wrong bridge (Kushma–Gyadi w230721093; the jump is on Kusma–Balewa w870647006). |
| ASSET_MANIFEST §5.1 | Nagarkot anchor n4430216290 | `landmarks.yaml` pins n1398437048. |
| ASSET_MANIFEST §5.1 | Dharahara has no footprint | Outline w1413193271 exists (min height 50 m, height 51 m) plus parts to 63 m; W1 draws a floating 1 m disc. |
| ARCHITECTURE §5.3 | Procedural roadside life includes "temples" and "chautari" | Procedural content must not create temples or named places (O2); chautari only from real data in M1. |
| ARCHITECTURE §6.1 | Summit correction (M3) in `dem.py` | Would write OSM into HGHT (ADR-013); do it at runtime instead. |
| ARCHITECTURE §6.1 | Terraces are cropland on 8–35° slopes | `biomes.py` has no upper bound. |
| DATA_FORMATS §1.9 | "Individual prop instances are never stored" | Superseded by `PROP` for real OSM objects only (D3). |
| DATA_FORMATS §1.5 | — | Should state that waterway point order is downstream. |
| M1_PLAN W1 | 1.3 "terrain (skirts, biome toon colours)"; 1.9 without the skyline ring | ROADMAP 1.3 includes road conformance and 1.9 the skyline impostor ring; both are scheduled in W2 here. |
| Census | "Nepal" totals | The whole extract, including Indian and Tibetan border strips. |
| Earlier notes | ICIMOD: ~3,800 glaciers, ~5,300 km² (2010) | 2010: 3,808 glaciers, ~3,902 km². ~5,324 km² is the 2001 inventory (3,252 glaciers). |
| Earlier notes | Extending `horizon_bbox` puts Everest on the Nagarkot skyline | Not alone: the far clip (67.5 / 105 / 155 km) cuts it on Mid. Only an impostor past the far plane shows it. |
| Earlier notes | Wards are "not extracted at all" | The extractor supports level 9; only the default omits it. |
| Earlier notes | Dodhara–Chandani 1,483 m | That is the OSM way including approaches; the published length is about 1,452.5 m (verify). |
| Earlier notes | Fung Funge falls lies in the buffer | It is inside the pack box (27.829 N) but outside the census valley box; only Lauke is in the buffer. |
