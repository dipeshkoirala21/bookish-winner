# W2 research: street life of the Kathmandu Valley (procedural checklist with numbers)

> Status: research input for Wave 2 "Kathmandu comes alive", 2026-10-05. Feeds ARCHITECTURE §7.7 (traffic, pedestrians, animals), ROADMAP items 1.17 (forests and trees) and 1.19 (street objects and procedural dressing), and gaps S5, S9, S15, N2, N3, B11, B18 and B20 in [CONTENT_COVERAGE](../../CONTENT_COVERAGE.md). Asset ids are the ones in [ASSET_MANIFEST](../../ASSET_MANIFEST.md) §§ 6, 7, 9 and 10.
> Everything here is meant to become **spawn-table parameters**. Confidence tags (same as [temples.md](temples.md)):
>
> | Tag | Meaning |
> |---|---|
> | **[O]** | Measured in this research from OSM (`pipeline/data/raw/osm/nepal.osm.pbf`, the snapshot the pack uses) with pyosmium + shapely, valley box 85.18–85.55 E, 27.55–27.82 N, 250 m grid |
> | **[S]** | Stated by a cited source (URL in the row or in §17) |
> | **[E]** | Estimate (design value from sources, photos used as reference only, or convention). Good enough for a cartoon world, not a survey |
> | **[V]** | Needs a human cultural or factual reviewer before it ships |
>
> Colours are cartoon palette targets in sRGB hex, all **[E]**, picked to read well under the toon ramp. Reference photographs are reference only (never shipped, never traced). Sound: only CC0 / public-domain recordings or procedural synthesis may ship (§15).
> CONTENT_COVERAGE O2 still applies: procedural dressing is generic, unnamed, never discoverable and never on the map; it **never creates temples, shrines, chortens, chautari or settlements**. Animals, people and vendors below are ambient life, not places.

---

## 0. Ten-line summary

1. A 250 m grid over the valley box gives five area types by building coverage and land use [O]: **old core** (coverage ≥ 0.45, ≥ 180 bldg per cell; median 50 bldg/ha, 612 shops/km²), **modern urban** (coverage 0.22–0.45; 29 bldg/ha, 172 shops/km²), **peri-urban** (0.06–0.22; 11 bldg/ha, 35 shops/km²), **fields/rural** (< 0.06; 1.6 bldg/ha) and **forest** (≥ 50% wood/forest). Areas: 2.8 / 66.7 / 147 / 490 / 329 km².
2. OSM under-detects the Newar cores of Bhaktapur, Patan, Kirtipur and Thimi (coverage 0.15–0.33 there), so "old core" must be a **curated polygon list** (§1.3) with the coverage rule only as a fallback.
3. Pedestrian mix per area type (§3.2) and clothing palettes (§3.3): about 45–60% casual western wear, 15–25% kurta/sari, 3–8% daura suruwal + topi (mostly older men), school uniforms 10–30% at school hours; monks only near Boudha/Swayambhu gompas, sadhus only at Pashupati (and festival days), porters with doko/namlo in bazaars, tourists 30–50% of walkers in Thamel in October.
4. Spawn targets per 100 m of street at peak (§3.1): old-core bazaar 60–120 people, modern main road 15–30, peri-urban 4–10, village lane 1–4 [E], clipped by the ASSET_MANIFEST §9.5 crowd caps.
5. Animals [S]: **14.2 adult roaming dogs per km of street** in Kathmandu (ICAM/KMC survey); about **1,200 street cattle** in KMC (≈ 24/km², clustered on the Ring Road and in Kalimati, Baneshwor, Thapathali); **≈ 450 rhesus macaques at Swayambhu** (stable since 1991) and about 50–450 at Pashupati (sources disagree, §7.3).
6. Birds [S]: the top five in the valley urban bird count are rock dove, house crow, house sparrow, barn swallow and common myna; black kites, cattle egrets, red-vented bulbuls and jungle mynas follow. Flock parameters and per-area counts in §8.
7. Street trees: pipal and bar on chautari platforms (every OSM tree is placed; 1,132 trees in the valley box, about 50 named pipal/bar) [O]; jacaranda purple from March to early May [S], silky oak (Grevillea) and bottlebrush from the Rana era [S], poinsettia (lalupate) red in winter, bamboo clumps by houses.
8. Forest rim bands [S]: Schima–Castanopsis 1,400–1,800 m, oak–laurel with rhododendron 1,800–2,400 m, evergreen oak (Quercus semecarpifolia) 2,400–2,760 m; chir pine on dry south ridges and plantations; Shivapuri spans 1,350–2,732 m.
9. Fields [S]: paddy transplanted from Asar 15 (late June), golden in Kartik–Mangsir (Oct–Nov), then wheat and yellow mustard through winter; this drives the valley floor's seasonal colour (§12–13).
10. Festivals are **place-fixed ambient events** (§6): Indra Jatra at Basantapur (Sept), Bisket at Taumadhi, Bhaktapur (mid-April, 25 m yosin pole), Rato Machhindranath from Pulchowk to Jawalakhel (Apr–Jun, 18–20 m chariot). Kites fill the sky from Nag Panchami to Haribodhini Ekadashi, peaking at Dashain. The Kumari is never shown, and no sacrifice or cremation is depicted.

---

## 1. Area types for spawning

### 1.1 Classifier (runs per 250 m cell, or per 100 m street segment using the cell it lies in)

| Area type | Rule (first match wins) | Valley box area [O] | Median buildings/ha [O] (p90) | Median building coverage [O] |
|---|---|---|---|---|
| `OLD_CORE` | inside a curated core polygon (§1.3), else coverage ≥ 0.45 **and** ≥ 180 buildings in the cell | 2.8 km² by the rule alone; ≈ 9–10 km² with the curated list [E] | 50.4 (84.8) | 0.47 |
| `URBAN` | coverage ≥ 0.22 | 66.7 km² | 29.3 (45.4) | 0.31 |
| `FOREST` | wood/forest ≥ 50% of the cell and coverage < 0.05 (or BIOM = forest) | 329.2 km² | 0.0 (1.4) | 0.00 |
| `PERI_URBAN` | coverage ≥ 0.06 | 147.4 km² | 10.6 (18.4) | 0.11 |
| `FIELDS_RURAL` | everything else (farmland, scrub, grass, scattered houses) | 489.5 km² | 1.6 (5.4) | 0.01 |

Coverage = building footprint area ÷ cell area. Overlays that override the type for spawning purposes: **SACRED** (temple, stupa and gompa compounds, ghats; RELIGIOUS AreaKind), **MARKET** (OSM `amenity=marketplace`, 58 in the box [O]), **SCHOOL** (campus polygons; 3,303 school/college POIs in the box [O]), **TOURIST** (Thamel, Freak Street, Boudha kora, Durbar Squares), **PARK** (§10).

### 1.2 OSM point density per area type [O] (per km²; valley box; mapping-biased, use as relative weights)

| Area type | Buildings | Shops | Restaurants/cafés | Lodging | Schools | Hindu worship | Buddhist worship | Bus stops | Street lamps | Power poles | Mapped trees | Taps |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| OLD_CORE | 5,712 | 612 | 170 | 95 | 37.7 | 24.5 | 7.5 | 2.5 | 11.4 | 9.2 | 1.4 | 2.5 |
| URBAN | 3,119 | 172 | 54 | 14.0 | 27.0 | 8.9 | 2.3 | 3.0 | 2.3 | 1.5 | 3.8 | 2.4 |
| PERI_URBAN | 1,138 | 34.6 | 9.1 | 2.0 | 6.6 | 3.1 | 0.6 | 1.6 | 0.9 | 0.6 | 1.3 | 1.4 |
| FIELDS_RURAL | 223 | 1.4 | 0.5 | 0.2 | 0.8 | 0.4 | 0.05 | 0.2 | 0.01 | 0.09 | 1.4 | 0.2 |
| FOREST | 40 | 0.08 | 0.16 | 0.08 | 0.15 | 0.13 | 0.05 | 0.03 | 0.03 | 0 | 0.06 | 0.02 |

Valley box totals [O]: 18,973 shops, 5,716 food places, 1,624 lodgings, 1,362 Hindu / 300 Buddhist places of worship, 543 bus stops, 327 lamps, 249 power poles + 579 towers, 1,132 trees, 456 taps. Poles and lamps are almost unmapped (CONTENT_COVERAGE B11, B12), so they are generated (§5). Median tagged `building:levels` = 3 (n = 18,827) [O].

### 1.3 Curated old-core polygons (centre and radius for a first cut; replace with drawn polygons) [E]

Measured inside each circle [O]:

| Core | Centre (lon, lat) | r (m) | Bldg/ha | Coverage | Shops/ha | Character |
|---|---|---|---|---|---|---|
| Kathmandu Asan–Indrachowk–Basantapur | 85.3095, 27.7065 | 700 | 54.9 | 0.40 | 4.9 | Densest bazaar; Newar houses 4–6 storeys; diagonal Asan trade street |
| Thamel (tourist overlay) | 85.3110, 27.7150 | 450 | 37.9 | 0.44 | 16.0 | Shop/restaurant density 3× the old core; signboards and wires densest |
| Patan Mangal Bazar | 85.3250, 27.6735 | 600 | 35.8 | 0.33 | 2.4 | Bahal courtyards, quieter; metal-workers (Okubahal) |
| Bhaktapur core | 85.4290, 27.6720 | 800 | 43.5 | 0.24 | 1.1 | Brick-paved, few cars (heritage zone), pottery square, drying grain |
| Kirtipur | 85.2780, 27.6780 | 400 | 40.4 | 0.27 | 3.6 | Hilltop Newar town, steep lanes |
| Thimi | 85.3870, 27.6800 | 400 | 16.3 | 0.15 | 3.2 | Pottery and mask town |
| Bungamati, Khokana | ≈ 85.300, 27.628 / 85.296, 27.637 | 250 | — | — | — | Newar villages; mustard oil presses (Khokana); woodcarving (Bungamati); coordinates to be pinned from OSM `place=village` [V] |
| Boudha kora (tourist overlay) | 85.3620, 27.7215 | 350 | 28.4 | 0.38 | 1.7 | Kora ring; monks, pilgrims, prayer wheels |

The coverage of OSM building mapping is uneven (Bhaktapur and Patan read lower than they look), which is why the cores are a curated list rather than a threshold.

---

## 2. What "Kathmandu Valley" looks like at street level: checklist

A designer can tick these off per area type. **●** = must be present, **○** = sometimes, **–** = absent.

| Element | Old core | Modern urban | Peri-urban | Fields | Forest | Section |
|---|---|---|---|---|---|---|
| Brick Newar houses with carved windows, pikha (stoop) benches | ● | ○ | ○ (villages) | – | – | buildings research |
| Concrete 3–6 storey houses with rebar "future floor" stubs on the roof | ○ | ● | ● | ○ | – | 5.4 |
| Rolling steel shop shutters | ● | ● | ○ | – | – | 5.3 |
| Pole and wire tangles | ● (dense) | ● | ○ | power towers only | – | 5.1 |
| Small roadside shrines, red-orange paint on stones, marigold | ● | ○ | ○ | ○ | ○ | (anchored to OSM only) |
| Temple bells being rung | ● | ○ | ○ | – | – | 5.6 |
| Prayer flags | at stupas/gompas only | at gompas | at gompas | – | passes/gompas | 5.5 |
| Vendors on foot, carts and mats | ● | ● | ○ | – | – | 4 |
| Motorbikes parked along the frontage | ● | ● | ○ | – | – | 5.7 |
| Cows lying on the road | ○ | ● (Ring Road) | ● | ● | – | 7.1 |
| Dogs asleep in the sun | ● | ● | ● | ● | – | 7.2 |
| Pigeon flocks | ● (squares) | ○ | – | – | – | 8 |
| Crows on wires, black kites overhead | ● | ● | ● | ○ | ○ | 8 |
| Laundry and drying chillies/grain on roofs | ● | ● | ● | ○ (on mats) | – | 5.4 |
| Kites (Aug–Nov, peak Dashain) | ● | ● | ● | ○ | – | 5.8 |
| Pipal/bar on chautari | ○ | ○ | ● | ● | – | 9 |
| Jacaranda, silky oak, bottlebrush along roads | ○ | ● | ○ | – | – | 9 |
| Bamboo clumps by houses | – | ○ | ● | ● | ○ | 9 |
| Rice paddy / winter wheat and mustard | – | – | ○ | ● | – | 12 |
| Brick-kiln chimneys | – | – | ○ | ● (south and east valley) | – | (CONTENT_COVERAGE: 135 chimneys) |
| Monkeys | at Swayambhu / Pashupati / Gokarna / Nilbarahi | – | – | – | ○ (rim) | 7.3 |

---

## 3. Pedestrians

### 3.1 Density targets (people visible per 100 m of street, both sides, at peak hour) [E]

These are design targets for "how busy it feels". The renderer then caps by tier (ASSET_MANIFEST §9.5: ≤ 6 skinned LOD0, ≤ 20 LOD1, ~50 VAT on High). Derived from population density (KMC 17,440 people/km², 2021 census [S]), the OSM shop density in §1.2 and reference photos; numbers are estimates.

| Area type / overlay | Peak per 100 m | Off-peak per 100 m | Night (22–05) per 100 m | Share standing still (vendors, chatting, sitting) |
|---|---|---|---|---|
| OLD_CORE bazaar street (Asan, Indrachowk, Mangal Bazar) | 60–120 | 25–50 | 0–3 | 30% |
| OLD_CORE residential lane (≤ 4 m) | 8–20 | 4–10 | 0–1 | 40% (pikha sitters) |
| TOURIST (Thamel, Freak St, Boudha kora) | 40–80 | 15–30 | 2–8 (until 23:00) | 25% |
| SACRED compound or square (Durbar Squares, Boudha, Swayambhu) | 50–150 per 1,000 m² of plaza | 15–40 per 1,000 m² | 0–5 | 50% (sitting on temple plinths) |
| URBAN main road (footpath) | 15–30 | 6–12 | 0–2 | 15% |
| URBAN side street | 6–15 | 3–6 | 0–1 | 25% |
| PERI_URBAN road | 4–10 | 1–4 | 0 | 30% |
| FIELDS village lane / field path | 1–4 (farmers in fields: 2–6 per hectare in planting and harvest weeks) | 0–2 | 0 | 50% |
| FOREST trail | 0–1 (Saturdays: 1–3 hikers per 100 m near Shivapuri, Nagarkot, Chandragiri) | 0 | 0 | 20% |
| SCHOOL gate (± 150 m), 07:00–09:30 and 15:00–16:30 | +30–60 children | — | — | 30% |
| MARKET polygon (Kalimati 18,796 m² [O], Asan, Tukucha 4,355 m² [O]) | 1 per 8–15 m² | 1 per 30 m² | 0 | 60% |

### 3.2 Archetype mix by area type (% of spawned pedestrians) [E]

Daytime, non-festival, October (the peak tourist month: 128,443 international arrivals in Oct 2025 [S]). Rows sum to 100.

| Archetype (ASSET_MANIFEST id) | Old core | Tourist overlay | Modern urban | Peri-urban | Fields | Sacred: Boudha / Swayambhu | Sacred: Pashupati |
|---|---|---|---|---|---|---|---|
| Urban casual adult `ghm_chr_npc_urban_{m,f}` | 40 | 30 | 52 | 45 | 25 | 30 | 35 |
| Woman in kurta suruwal or sari `ghm_chr_outfit_kurta_suruwal`, `_sari` | 18 | 8 | 15 | 18 | 20 | 12 | 25 |
| Older man in daura suruwal + dhaka/bhadgaunle topi | 6 | 2 | 3 | 6 | 8 | 3 | 8 |
| Newar woman in hakupatasi (Bhaktapur, Patan, Kirtipur, Khokana only; mostly older women and festival days) | 3 (Bhaktapur 6) | 0 | 0 | 1 | 2 | 0 | 0 |
| School children `ghm_chr_npc_kid_{a,b}` (uniform variant in school hours) | 8 | 2 | 10 | 10 | 8 | 3 | 2 |
| Porter with doko/namlo or tokma `ghm_chr_npc_porter_{a,b}` | 6 | 2 | 1 | 2 | 4 | 1 | 1 |
| Vendor on foot (bicycle or basket) `ghm_chr_npc_vendor_{a,b}` | 6 | 4 | 3 | 2 | 0 | 4 | 4 |
| Tourist / trekker `ghm_chr_npc_tourist_{a,b,c}` | 6 | 45 | 2 | 0 | 0 | 20 | 10 |
| Monk or nun (maroon robes) `ghm_chr_npc_monk_tibetan` | 1 | 2 | 0.5 | 0.5 | 0 | 22 | 0 |
| Sadhu `ghm_chr_npc_sadhu_a` | 0 | 0 | 0 | 0 | 0 | 0 | 6 (Shivaratri: 15) |
| Farmer with sickle or hoe `ghm_chr_npc_farmer_{m,f}` | 0 | 0 | 0 | 6 | 33 | 0 | 0 |
| Traffic police (only at major chowks, §7.7 of ARCHITECTURE) | 0.5 | 0.5 | 1 | 0.5 | 0 | 0 | 0 |
| Kids flying kites (Aug–Nov, rooftops and open ground) | seasonal, §5.8 | | | | | | |

Placement rules:
* Monks spawn only within 400 m of a Buddhist place of worship (300 in the box [O]) and on the Boudha and Swayambhu koras; they walk the kora **clockwise**, often in pairs, with a hand prayer wheel or mala.
* Sadhus spawn only on the Pashupati ghats and courtyard edges (east bank terraces), sitting, and in Shivaratri crowds. No pay-for-photo mechanic (ASSET_MANIFEST row `ghm_lmk_pashupatinath`). [V]
* Porters carry doko (conical bamboo basket) with namlo (head strap across the forehead), or a sack on the back; they walk at 0.8× normal speed and rest the load on a tokma (T-stick) every 60–120 s of walking [E]. In bazaars, "bhariya" porters carry sacks and gas cylinders.
* Tourists cluster: 70% within the TOURIST overlay, 25% at heritage squares and stupas, 5% elsewhere [E]. They carry daypacks and cameras and stop to photograph (look-up idle).

### 3.3 Clothing palettes (instanced tint sets) [E]

| Garment | Shape notes for the generator | Palette (hex) | Source |
|---|---|---|---|
| Daura (men's tunic) | Knee-length, double-breasted, **tied with 4 strings** (2 near the shoulder, 2 at the waist), no buttons | off-white #EDE6D6, pale grey #C9C6BE, light blue #B8C8D8, beige #D9C7A3 | [S] Himalayan Times, ECS |
| Suruwal (trousers) | Very loose at the thigh, tapering to tight at the ankle | same as daura (matching set) | [S] ECS |
| Waistcoat / coat over daura | Western-cut, dark | charcoal #3A3D42, navy #2B3550, brown #5A4636 | [S] ECS |
| Dhaka topi | Asymmetric soft cap, taller at the front; dhaka weave pattern of small diamonds | base red #B5302B, navy #2E3A6B, black #1E1E1E, mustard #C99A2E, green #3E7A4A, with white/yellow motif dots | [S] Nepal Traveller; pattern [E] |
| Bhadgaunle topi | Same cap shape, plain black | #1A1A1A | [S] Kathmandu Post 2015 |
| Sari + blouse | Draped, pallu over left shoulder; everyday cotton prints | red #C8202F, magenta #C2185B, orange #E8752A, green #2E8B57, royal blue #2A5DB0, yellow #F2C230; Teej: almost all red #C8102E with green bead necklace (pote) #2E8B3E | [E] |
| Kurta suruwal (women) | Knee-length tunic, trousers, dupatta scarf | pastel set: #F4B6C2, #A8D5E2, #F7D488, #B5E3B0, #E3C1F0; scarf contrasting | [E] |
| Hakupatasi (Newar) | Black sari wrapped at the waist with a **red border** | black #1C1C1C, border red #B3141E | ASSET_MANIFEST; [V] |
| Gunyu cholo (girls' ceremony, festival only) | Blouse + sari | festival brights | [E] |
| School uniforms | Light shirt, dark trousers or skirt, tie (private schools), belt; sweater Nov–Feb | shirt white #F5F5F2 or sky blue #9EC9EA; trousers/skirt navy #1F2D4F, grey #6B6E73 or maroon #6B1F2A; tie in school colours; sweater navy/maroon | [S] Nepal Police School (Wikipedia), KUHS uniform PDF |
| Monks' robes (Tibetan Buddhist) | Maroon wrap + saffron-yellow sleeveless shirt; shaved head | maroon #7A1F2B, saffron #E3A21A | [E] |
| Sadhu | Saffron/orange cloth, ash-grey body paint, long dreadlocks, tilak; keep dignified, not comic | saffron #E87A1D, ash #BDB8AE | [E][V] |
| Porter | Shorts or rolled trousers, vest, flip-flops or sneakers; dhaka topi on older porters | earth tones #6E5A44, #8A7B5F, #4D5A3C | [E] |
| Tourist | Trek trousers, fleece, sun hat, daypack | outdoor brights #E2552D, #2F7FC1, #F2B705, khaki #B8A67E | [E] |
| Winter layer (Dec–Feb, mornings) | Down jackets on 40–60% of walkers 06–10 h; shawls on older women | #2B2B2B, #4A5A7A, #7A2E2E | [E] |
| Tika and marigold | Red tika on the forehead on festival days (Dashain tika day: 80%+ of locals in the core) | tika red #D2001F, jamara shoots #CFE07A | [E][V] |

Footwear: flip-flops (chappal) on 40%, sneakers 40%, leather shoes 20% [E]. Umbrellas: 30–60% of walkers when it rains in the monsoon (Jun–Sep), also as sunshades in May [E].

### 3.4 Behaviours (VAT clip names from ASSET_MANIFEST §9.5)

| Behaviour | Where | Frequency [E] |
|---|---|---|
| `namaste` greeting pair | Any; more in old core | 1 pair per 200 m of bazaar per minute |
| Touch-and-bow at a shrine while passing (right hand to shrine, then forehead) | Within 3 m of any OSM shrine/temple node | 15% of passing locals in the morning, 5% later |
| Ring a temple bell | Shrine bell props | 1 ring per 20–90 s at busy shrines 05:30–09:00, rare after 11:00 |
| Circumambulate clockwise | Stupas, chaityas, temples | Kora walkers at Boudha: 60–80% of people on the ring in 05:30–08:00 and 17:00–19:00 |
| Sit on pikha / temple plinth / chautari | Old core, squares, chautari | 20–40% of NPCs near these surfaces at midday |
| Feed pigeons (grain from a vendor) | Basantapur, Patan and Bhaktapur squares, Boudha | 1–3 people at a time per flock |
| Kite flying (`kite` clip) | Rooftops and open ground, Aug–Nov | §5.8 |
| Carry (doko, sack, gas cylinder, baby on the back) | Bazaars | 8–12% of pedestrians carry something visible |

---

## 4. Street vendors and carts

### 4.1 Vendor types (props `ghm_prp_vendor_cart_{fruit,snack,veg}`, `_tea_stall`, `_momo_steamer`, `_market_stall_{a,b}`, `_goods_*`)

| Vendor | Form | Size (L × W × H, m) [E] | Palette and look [E] | Where | Hours | Share of vendors (old core / urban) [E] |
|---|---|---|---|---|---|---|
| Fruit cart (thela) | Flat 4-wheel wooden push cart, bicycle wheels, tilted display, umbrella or tarp | 1.8 × 0.9 × 0.9 (deck), umbrella Ø 1.8 | Green-painted wood #3E7A4A; tarp blue #2D6FB7; goods: bananas #F2D64B, apples #C8343B, oranges #F28C28, pomegranates #A3202E | Main roads, near bus stops, outside hospitals and temples | 08–20 | 25 / 30 |
| Vegetable seller | Mat on the pavement or bicycle with two side baskets | mat 1.5 × 1.0; baskets Ø 0.5 | Greens #4E9A3A, cauliflower #F2EEDC, tomatoes #D93A2B, chillies #C21D1D | Asan, Indrachowk, Ason-Kel Tol, Kalimati, Patan Mangal Bazar, Bhaktapur squares | 06–11 and 15–19 | 25 / 15 |
| Chatpate / panipuri stall | Small table or a shoulder-carried basket stand with a big aluminium bowl, glass jars | table 1.0 × 0.6 × 0.8 | Aluminium #C9CDD1, puffed rice #E9D9A6, lime green #9CCB3B | School gates, parks (Ratna Park), Bhrikuti Mandap, chowks | 12–19 | 10 / 15 |
| Momo shop or stall | Stacked aluminium steamer (3–5 tiers) on a gas burner at a shopfront | stack Ø 0.45 × 0.6–1.0 | Aluminium, steam VFX, plastic chairs red #C62828 / blue #1E5AA8 | Everywhere urban; densest Thamel, New Road, Baneshwor | 10–21 | 10 / 15 |
| Sel roti / fried snacks | Wok on a kerosene/gas stove, ring-shaped rice bread stacked on a tray | wok Ø 0.6 | Sel roti golden-brown #B87333 | Morning bazaars; festival (Tihar) peak | 06–10 | 5 / 5 |
| Tea stall (chiya pasal) | Tiny shop or kettle on a stove with a bench and glass tumblers | 2.0 × 1.5 floor; bench 1.5 × 0.3 | Kettle aluminium, tumblers clear, bench blue | Chowks, bus stops, everywhere | 05:30–20 | 10 / 10 |
| Corn (makai) roaster | Brazier on a stand | 0.5 × 0.5 × 0.8 | Coal glow #FF6A1F, smoke VFX | Roadsides in the monsoon and autumn | 15–20 | seasonal 5 |
| Flower and puja-goods seller | Mat or small stall: marigold garlands, incense, red powder, oil lamps | 1.2 × 0.8 | Marigold orange #F39C12 / yellow #F7C948, sindoor red #D2001F | **Within 50 m of every busy temple** (Pashupati, Dakshinkali, Kathmandu Durbar Sq., Swayambhu stairs) | 05–12 | 5 / 2 |
| Grain seller for pigeons | Bowls of grain on a mat | 0.6 × 0.6 | Grain #D8B86A | Basantapur, Boudha, Patan and Bhaktapur squares | 07–18 | at squares only |
| Pashmina / souvenir / thangka shop spill-out | Racks, hanging scarves | 1.5 × 0.5 rack | Bright scarves | TOURIST overlay | 09–21 | Thamel only |
| Ice-cream / bicycle vendor | Bicycle with a cool box, bell | bike + 0.5 box | White box #F4F4F4, red text | Parks, school gates (spring/summer) | 11–18 | 3 / 3 |
| Juju dhau (Bhaktapur curd) | Clay bowls on a shop counter | bowls Ø 0.15 | Clay #B5651D, curd #FFF7E6 | Bhaktapur core only | 09–19 | Bhaktapur only |
| Pottery | Pots drying in rows, wheel | pots Ø 0.2–0.5 | Clay #A0522D | Bhaktapur Pottery Square, Thimi | 07–17 | place-fixed |

### 4.2 Density [E]

| Area type | Vendors per 100 m of street (peak) | Notes |
|---|---|---|
| OLD_CORE bazaar | 6–15 | Plus shops every 3–5 m of frontage (612 shops/km² [O]) |
| TOURIST | 3–6 | Mostly shop spill-out |
| URBAN main road | 1–3 | Clustered at bus stops and junctions (60% within 50 m of a junction or stop) |
| PERI_URBAN | 0.2–1 | Tea stall per village chowk |
| Market polygon | 1 stall per 6–10 m² | Kalimati wholesale: trucks unloading 04–08 |
| Sacred approach (last 200 m) | 4–10 | Flower and puja-goods dominant |

Vendors are **packed away by night**: carts move off between 20:00 and 21:30, mats are rolled up (a "pack up" animation is enough).

---

## 5. Street dressing

### 5.1 Poles and wires (`ghm_prp_power_pole_*`, `_wire_tangle_*`, `_transformer`)

| Parameter | Value | Tag |
|---|---|---|
| Pole type | Square-tapered pre-stressed concrete (most common), some steel tubular; 9 m (LT) and 11 m (11 kV) | [E] |
| Pole colour | Concrete #A8A49C, weathered base #7D7A73; steel grey #7B8085 | [E] |
| Spacing along a street | Old core 25–35 m; urban 35–45 m; peri-urban 40–60 m; one side of the road (alternating on wide roads) | [E] |
| Cross-arms | 1–2 at the top (11 kV), plus a low LT rack at 7–8 m | [E] |
| Wire sag | Catenary sag ≈ 2–3% of span for power; telecom bundles sag 5–8% | [E] |
| Telecom/ISP cable count | Old core and Thamel 10–40 cables in loose bundles with coiled slack loops ("spaghetti") every 2–4 poles; urban 5–15; peri-urban 2–6 | [E] (photos; the clutter is real and has been the subject of NEA clean-up orders [S]) |
| Clearance | Optical fibre ≥ 2.4 m above footpaths and **≥ 5.5 m above roads** (regulatory minimum) | [S] Himalayan Times on NEA standards |
| Transformer | Pole-mounted on a 2-pole H-frame, every 250–400 m in urban areas | [E] |
| Street lamp | Arm on the power pole or a separate lamp post every 35–40 m on main roads | CONTENT_COVERAGE B12 |
| Birds on wires | Crows and pigeons perch on the top power line (§8) | — |
| Policy trend | Cables being bundled/undergrounded on New Road and some main roads (2023–2025) [S]; keep New Road **clean** and old-core lanes **messy** | [S] Onlinekhabar |

### 5.2 Signboards [E]
Shopfront signs on 80–95% of urban shop frontages: flex banners 0.6–1.0 m tall across the full shop width, Devanagari + English, saturated red #D32F2F, blue #1565C0, yellow #FBC02D, white backgrounds. **Generic text only** (no real brands: ARCHITECTURE no-branding rule). Vertical hanging signs in Thamel, 3–8 per 10 m.

### 5.3 Shop shutters [E]

| Parameter | Value |
|---|---|
| Rolling steel shutter width | 2.4–3.0 m (one per shop bay); old core bays narrower 1.8–2.4 m |
| Height | 2.4–2.7 m; shutter box 0.3 m above |
| Colours | galvanised grey #8E9399 (50%), blue #2F5D9E (20%), green #2E7D4F (15%), maroon #7B2D2D (10%), painted ad art (5%) |
| Open state | Pulled up 08:30–10:00, down 19:30–21:30; Saturdays 50–70% closed outside the tourist overlay; Dashain tika day 90% closed |
| Old Newar shop | Wooden plank shutters or folding doors, raised floor 0.4–0.6 m above the street (pikha plinth), shopkeeper sits cross-legged on the floor |
| Sound | Rolling shutter rattle 1.5–2.5 s (synthesised, §15) |

### 5.4 Rooftops and facades [E]

| Element | Frequency | Parameters |
|---|---|---|
| Black (or blue) plastic water tank | 70–85% of modern flat roofs | Cylinder Ø 0.9–1.3 m, h 1.0–1.6 m (500–2,000 L), ribbed; black #1E1E1E, blue #2A5DB0, on a 0.3–0.5 m stand |
| Solar water heater | 15–30% of modern roofs | 2 × 1 m panel tilted 30–45° south, white tank Ø 0.4 × 1.5 m |
| Rebar stubs ("future floor") | 25–40% of modern roofs | 4–12 columns 0.3–1.0 m tall with rusty rebar #8B4A2B sticking up |
| Rooftop railing / half-wall | 80% | 0.9–1.1 m |
| Laundry line | 40–60% of roofs and balconies, 08–17 h on dry days | 2–6 m line; cartoon clothes in the §3.3 palettes; **none in rain** |
| Potted plants, tulsi (holy basil) planter | 30–50% | Tulsi in a raised square planter (moth) #B5543A |
| Satellite dish | 10–20% | Ø 0.6–0.9 m |
| Drying chillies, grain, gundruk on mats | Oct–Dec on 10–20% of roofs; old core courtyards and village yards | Mat 2 × 1.5 m; chilli red #B71C1C, paddy #D8B65A |
| People on roofs | 06–09 (sun, tea, puja) and 16–18; kite flyers Aug–Nov | 1 person per 5–15 roofs at those hours |
| Kites | §5.8 | |
| Monkeys on roofs | within 400 m of Swayambhu and Pashupati | §7.3 |

### 5.5 Prayer flags (`ghm_prp_lungta_string`, `_darchor_pole_*`)

* **Only at Buddhist sites**: stupas, gompas, chortens, passes (CONTENT_COVERAGE B18). Never on Hindu temples.
* Colour order along the string, repeating: **blue – white – red – green – yellow** (sky, air/cloud, fire, water, earth) [S, common description; V]. Hex: blue #1F5FBF, white #F5F5F0, red #D2232A, green #1E9A4B, yellow #F7C71F. Flag size 0.2–0.3 × 0.25–0.35 m [E], spacing 0.3–0.4 m [E].
* Boudha: strings radiate from the harmika/spire to the outer terrace ring: 60–120 strings [E], each 30–50 m. Swayambhu: strings from the spire and across the hilltop and stairway trees. Gompas: 2–10 strings from the roof to a pole.
* Faded variants (sun-bleached 30–70%) and fresh sets for Losar (Feb/Mar) [E].

### 5.6 Temple bells (`ghm_prp_temple_bell_{s,m,l}`, `ghm_bld_pagoda_eave_bell`)

| Bell | Size [E] | Where | Behaviour |
|---|---|---|---|
| Hand-rung shrine bell | Ø 0.12–0.25 m, hanging from an iron frame or chain at the door | Every temple/shrine with a door | Rung by worshippers: §3.4 rates |
| Large bell in a pavilion | Ø 0.8–1.5 m, on a 2-post frame with a small roof | Kathmandu, Patan and Bhaktapur Durbar Squares (one each, the "Taleju bells") [V], plus large temples | Rung at morning and evening puja times (2–3 times a day) [V] |
| Eave bells (pagoda) | Ø 0.08–0.12 m with a leaf-shaped clapper flag | All pagoda eaves (≈ every 0.4 m: temples.md §0) | Wind-driven tinkle |
| Prayer-wheel bell | Small bell rung by a striker each turn | Large prayer wheels (Boudha, Swayambhu) | Ding once per revolution |

Bell sound: modal synthesis (3–6 inharmonic partials, decay 2–8 s by size) is enough and licence-clean (§15).

### 5.7 Parked vehicles and traffic texture [E]

* Motorbikes and scooters dominate: about **70% of vehicles registered in the capital are two-wheelers** [S, Meroauto]; 98,727 new two-wheelers registered in Kathmandu in FY 2025/26 [S].
* Parked motorbikes: old core 10–25 per 100 m (angled 60–90° against the wall, both sides where width allows), urban 5–15, peri-urban 1–4. Bicycles 0.5–2 per 100 m.
* Moving traffic mix on urban roads (vehicles, not PCU): motorbike/scooter 60–70%, car/taxi/jeep 15–20%, microbus/tempo/bus 5–8%, truck/tipper 2–5%, bicycle 2–4%, electric tempo (safa tempo, green-white) in the core [E].

### 5.8 Kites (Dashain season) (`ghm_prp_kite_{a,b,c}`, `ghm_chr_npc_kid_kite`)

| Parameter | Value |
|---|---|
| Season | Nag Panchami (Aug) to Haribodhini Ekadashi (Nov); **peak in the Dashain fortnight** (late Sep – Oct) [S Kathmandu Post 2021/2024] |
| Density at peak | 1 kite per 1–3 roofs in OLD_CORE and URBAN in the afternoon (14–18 h), i.e. 100–400 kites/km² [E]; outside Dashain 5–30/km² [E] |
| Altitude | 20–120 m above the flyer, string as a catenary [E] |
| Kite shape | Diamond (fighter kite), 0.4–0.6 m, with a tail on cheaper kites; flat colour blocks: red #E53935, yellow #FDD835, green #43A047, blue #1E88E5, white, black, two-colour halves [E] |
| Kite fighting | "Changa chait!" – a kite whose string is cut drifts away and falls slowly (cartoon); shout VO "chait!" [S Onlinekhabar]; no harm shown |
| Spool | Bamboo/wooden lattai |
| Who | Mostly kids and young men on rooftops and open grounds (Tundikhel, school grounds) [S] |

---

## 6. Festivals as place-fixed ambient events

Dates follow the lunar calendar and move each year; the windows below are typical Gregorian ranges. The game needs a festival calendar file per year (dates [V]). All festivals are observation-only crowds with music, chariots and decorations; no sacrifice, no blood, no cremation, and **the Kumari is never shown** (CONTENT_COVERAGE O11).

| Festival | Location (fixed) | Window | What to build | Crowd and rules |
|---|---|---|---|---|
| **Indra Jatra (Yenya)** | Kathmandu Durbar Square / Basantapur; chariot routes through the old core | 8 days ending around Bhadra/Ashwin full moon, **September** [S] | Yosin (lingo) wooden pole with Indra's banner raised in front of Hanuman Dhoka at the start [S]; masked dancers (Majipa Lakhey, Pulu Kisi elephant) [S]; Swet Bhairav mask display [V]; three chariots (Ganesh, Bhairav, Kumari) pulled on three days along the routes: day 1 Basantapur–Maru–Chikanmugal–Jaisidewal–Lagan–Brahma Marga–Wonde–Hyumata–Kohity–Bhimsensthan–Maru–Basantapur; day 2 Basantapur–Pyaphal–Yatkha–Nyata–Tengal–Nhyokha–Nhaikan Tol–Asan–Kel Tol–Indra Chowk–Makhan–Basantapur; day 3 Basantapur–Pyaphal–Yatkha–Nyata–Kilagal–Bhedasing–Indra Chowk [S Wikipedia] | Square at 2–4 people/m² at peak [E]. **Kumari's chariot is shown with a closed canopy and no visible figure** [V]. Lakhey as a friendly cartoon mask, not scary [V] |
| **Bisket (Biska) Jatra** | Bhaktapur: Taumadhi Square → Khalna Tole (Bhairav chariot); yosin raised at Khalna/Lyasinkhel [V] | 9 days around the Nepali new year, **mid-April** (starts 4 days before Baisakh 1) [S] | Bhairav chariot: 3-tier pagoda-style wooden chariot on big wheels; smaller Bhadrakali chariot [S]; **25 m yosin pole** with two long banners, raised then pulled down on new year's day [S] | Tug-of-war between upper and lower town teams pulling the chariot [S]; 1–3 people/m² near the chariot [E] |
| **Rato Machhindranath (Bunga Dyah) Jatra** | Patan: Pulchowk → Gabahal → Mangal Bazar → Hakha → Sundhara → Chakrabahil → Lagankhel → **Jawalakhel** [S] | Starts around Baisakh (late April), the chariot moves in stages for weeks; ends with **Bhoto Jatra** at Jawalakhel (May–June) [S] | Chariot ≈ 60–65 ft (18–20 m) tall [S]: wooden base on 4 large wheels, a tall tapering tower of bamboo covered in green foliage [E], red-and-gold canopy, long front beam; a smaller Minnath chariot follows [V] | The chariot "parks" at named stops for days: spawn it static at the current stop for the date [E]. Bhoto (jewelled vest) shown from the platform |
| **Dashain** | Everywhere; Hanuman Dhoka (Phulpati procession) [V] | 15 days, **late Sep – Oct** | Kites (§5.8), bamboo swings linge ping (in open ground, 6–10 m tall) and rote ping, jamara (yellow barley shoots), tika plates, marigold, banana-leaf gates [E] | Streets empty on tika day, everyone at home; shops closed. **No sacrifice depicted** |
| **Tihar** | Everywhere | 5 days, Oct–Nov (after Dashain) | Oil lamps (diyo), string lights on every house (60–90% of houses) [E], rangoli at doors, marigold garlands; crows fed (Kag Tihar), dogs garlanded with red tika (Kukur Tihar), cows garlanded (Gai Tihar) | Deusi/bhailo singing groups 5–15 people walking at night; firecracker VFX optional [V] |
| **Teej** | Pashupatinath, women's crowds in red saris | Aug–Sep | Red sari palette (§3.3) | Long queues; dancing groups |
| **Gai Jatra** | Kathmandu, Patan, **Bhaktapur** old cores | Aug–Sep | Procession of decorated cows and children dressed as cows; satire troupes [S, general] | Joyful procession, 30–200 participants per tole group [E] |
| **Shivaratri** | Pashupatinath | Feb–Mar | Sadhus from across South Asia, bonfires in the courtyards | Sadhu share 15% (§3.2); very large crowds [V] |
| **Buddha Jayanti** | Swayambhu, Boudha | Apr–May (Baisakh full moon) | Butter lamps, fresh prayer flags, processions | Monk and pilgrim share up |
| **Losar** | Boudha | Feb–Mar | New prayer flags, crowds on the kora | Clockwise kora |
| **Seto Machhindranath** | Jana Bahal (Kel Tol) → Asan–Indrachowk route, Kathmandu | Mar–Apr (Chaitra) [V] | Smaller chariot in the old core | Old core closed to traffic for the route |
| **Ghode Jatra** | Tundikhel | Mar–Apr | Horse parade on Tundikhel [V] | Observation from the edge; no military imagery (CONTENT_COVERAGE O9) |
| **Morning and evening aarti** | Pashupati ghats (east bank view), Boudha lamps | Daily, sunset | Lamps, bells, conch | §14 |

Today's reference: Dashain 2026 falls in October 2026 (exact dates from the official calendar [V]).

---

## 7. Domestic animals and monkeys

### 7.1 Cows and bulls (`ghm_ani_cow`)

| Parameter | Value | Tag |
|---|---|---|
| Street cattle in KMC | ≈ 1,200 (2016 KMC figure); valley ≈ 1,300 cattle registered with owners | [S] Himalayan Times |
| Composition | 95% old cows and oxen, 5% calves | [S] |
| Hotspots | Ring Road Balkhu–Koteshwor; Baneshwor, Thapathali, Kalimati | [S] |
| Derived density | ≈ 24 per km² averaged over KMC; spawn **60% on Ring Road / arterial segments**, 30% urban, 10% old core edges | [E] |
| Per road length | Ring Road 1–3 per km; urban arterial 0.3–1 per km; old core 0–0.3 per km; peri-urban village 1–3 per km (owned cows tethered near houses, 1–4 per household with cattle) | [E] |
| Behaviour | Lie in the middle of the road chewing (60% of the time), stand at vegetable waste (30%), walk slowly (10%); groups of 1–5 | [E] |
| Colours | white #EDE7DA, cream #D8C9A8, grey #9D9890, black-and-white #2B2B2B/#EFEFEF, brown #8B5A3C; humped zebu shape | [E] |
| Rules | Vehicles always stop and steer round; never harmed (ASSET_MANIFEST §10.2) | — |

### 7.2 Dogs (`ghm_ani_dog`)

| Parameter | Value | Tag |
|---|---|---|
| Street dogs in Kathmandu | 21,856 (first KMC census); estimates 22,000–30,000 for the city | [S] Kathmandu Post 2016, KAT Centre, ICAM |
| Survey density | **14.2 adult roaming dogs per km of street**, 33.1% female | [S] ICAM Kathmandu dog population assessment |
| Spawn per km of street [E] | Old core 12–18; urban 12–16; peri-urban 8–14; village 4–10 (owned dogs too); forest trail 0–1 | [E] from the [S] mean |
| Visible at once | Day: 60–70% asleep (sun patches, temple plinths, under parked bikes); night: 70% awake and roaming, barking chains | [E] |
| Groups | Singles 60%, pairs 25%, packs of 3–6 15% | [E] |
| Colours | Tan #C49A6C (40%), black #2A2A2A (20%), black-and-tan (15%), white/cream (10%), brindle and patched (15%) | [E] |
| Kukur Tihar | Garland of marigold + red tika on 80% of dogs that day | [V] |

### 7.3 Monkeys (`ghm_ani_macaque`)

| Site | Count | Tag | Spawn rule [E] |
|---|---|---|---|
| Swayambhu | **≈ 450**, stable 425–450 since counts began in 1991 | [S] Himalayan Times (Chalise) | 40–80 visible within 200 m of the stupa; troops of 10–40 on roofs, stair railings, chaityas, trees; babies on 10% of adults |
| Pashupati (incl. Mrigasthali forest) | 50 (older count) to 450 (later count) | [S] two Himalayan Times articles disagree [V] | 20–40 visible on the east-bank terraces and forest edge |
| Gokarna, Nilbarahi, Sankhu Bajrayogini, Patan Durbar | troops present (study sites) | [S] Kathmandu Post 2019 | 5–20 visible at each |
| Rim forests | wild troops | [E] | 0.5–2 troops per km² of HILL_FOREST below 2,000 m |

Behaviour: sit, groom, climb, hop between roofs; cheeky but never aggressive, never steal from the player [ASSET_MANIFEST]. Colour: brown-grey #8C7A64, pink face #D9A08B.

### 7.4 Goats, chickens, buffalo, ducks

| Animal | Where | Density [E] | Notes |
|---|---|---|---|
| Goat (`ghm_ani_goat`) | Peri-urban and village yards, field edges | 2–8 per village household cluster; herds of 5–20 grazing on field bunds; 0 in old core except before Dashain (goat markets, e.g. at Khula Manch near Ratna Park [V]) | No sacrifice depicted |
| Chicken (`ghm_ani_chicken`) | Village yards, Newar village courtyards (Khokana, Bungamati), peri-urban | 3–12 per yard; scatter on approach | Roosters crow 04:30–06:30 |
| Water buffalo (`ghm_ani_buffalo`) | Peri-urban sheds, field edges | 0–3 per farm; wallow in ponds in summer | Egrets follow them |
| Ducks | Ponds (pokhari) in villages | 3–10 per pond | [E] |

---

## 8. Birds and flocking

Valley urban bird count (Bird Conservation Nepal, 24 transects across urban, suburban and rural gradients): **6,701 birds counted in winter 2022** (urban transects 2,695, rural 1,815); top five by abundance: **rock dove, house crow, house sparrow, barn swallow, common myna**; also common: red-vented bulbul, jungle myna, Eurasian tree sparrow, black kite, cattle egret [S Onlinekhabar / Himalayan Times].

| Species (Nepali) | Asset | Where | Count per km² visible at once [E] | Group size | Behaviour parameters [E] | Colour [E] |
|---|---|---|---|---|---|---|
| Rock pigeon (parewa) | `ghm_ani_pigeon` | Basantapur, Patan and Bhaktapur Durbar Squares, Boudha, Swayambhu, temple roofs, wires | Squares: 1 flock of 30–200 per square; urban 50–150/km² | 30–200 (squares), 5–20 (roofs) | Ground peck in a loose disc 3–10 m; **burst take-off** when the player is within 3–4 m or a vehicle passes; all-flock circuit of radius 30–60 m at 10–15 m/s, 1–3 laps, re-land within 20–40 s; boids: separation 0.6 m, alignment radius 3 m, cohesion radius 8 m | grey #8A8F99, iridescent neck #4E7A6A/#7B5C8E, wing bars #2F2F35 |
| House crow (kag) | `ghm_ani_crow` | Everywhere urban; wires, roofs, rubbish | 20–60/km² | 2–15; evening roost flocks of 100s into big trees (Tundikhel, Ratna Park, Narayanhiti) [E] | Hop, perch on wires, caw; fly 8–12 m/s; Kag Tihar offering moment [V] | grey neck #6E6E70, black #141414 |
| Black kite (chil) | new: `ghm_ani_black_kite` (suggested; reuse vulture rig) | Over the city and rivers, all year, more in winter | 2–8 soaring over any 1 km² view | Singles or loose groups of 3–20 circling | Soar in thermals 08:30–16:00: circle radius 20–50 m, height 40–300 m, bank 15–30°, glide 8–12 m/s; forked tail twist | brown #5C4632, paler head #8F7558 |
| Common myna (rupi) / jungle myna | new: `ghm_ani_myna` (suggested) | Lawns, parks, roadsides, roofs | 30–80/km² | pairs, groups of 3–10 | Ground walk with a strut, short flights; noisy | brown #5A3E2B, black head #1A1A1A, yellow eye patch and bill #F2C21B, white wing patch |
| House sparrow (bhangera) | new: `ghm_ani_sparrow` (VAT) | Eaves, old core shopfronts, grain spills | 50–150/km² | 5–30 | Short hops, flush together into eaves | brown #8B6B4A, grey crown #8C8C8C, black bib |
| Barn swallow | VAT flock | Over paddy and rivers, Mar–Oct | 10–40/km² over fields | 5–30 | Low swooping flight 1–5 m over fields and water, 10–15 m/s; lines on wires | blue-black #1C2A44, rufous throat #A8492E |
| Cattle egret (bakulla) | `ghm_ani_egret` | Paddy fields, behind ploughs and buffalo, on cattle; roosts in trees (e.g. near the airport, Chobhar) [V] | Fields: 5–40 per hectare during transplanting and ploughing (Jun–Jul) | 5–40; roost colonies 100+ | Walk behind cattle and tractors; evening flight lines in V-ish strings to roosts | white #F7F7F2; breeding buff head #E0B070 (Apr–Jul) |
| Red-vented bulbul | small perched bird | Garden trees, bottlebrush | 20–60/km² | pairs | Perch-and-sing | dark brown #3B3330, red vent #C62828 |
| Rose-ringed / Alexandrine parakeet | optional | Big trees in parks, Godawari | 0–10/km² | 3–20 | Fast screeching flights | green #3DAA3D, red bill #D32F2F |

Rules: pigeons may be **fed** (players can buy grain from the square vendor); all other birds are observation-only. Flocks are VAT instances (ASSET_MANIFEST §1.5: 300 tris); cap 2 pigeon flocks + 3 small flocks within 90 m on High [E].

---

## 9. Urban trees

All OSM `natural=tree` nodes are placed (1,132 in the box [O]; names: pipal/peepal and variants ≈ 45, bar/banyan ≈ 5, *Juniperus chinensis* 19, plus a few kharibot, lakuri, chilaune, dhupi, mango, walnut, poplar, citrus [O]; 1,040 unnamed). Generic street trees fill avenues procedurally, flagged procedural.

| Species (Nepali) | Asset | Where | Size [E unless S] | Spacing / count rule [E] | Seasonal look (hex [E]) |
|---|---|---|---|---|---|
| Pipal, *Ficus religiosa* | `ghm_veg_peepal_{a,b}` | Chautari, temple courtyards, village chowks; often **paired with bar** ("bar-pipal" on one platform) | 15–25 m tall, crown Ø 15–25 m; heart-shaped leaves with a long drip tip; pale grey trunk #9A948A | At every OSM chautari/pipal node; unnamed ones may be species-guessed only where tagged | Evergreen-ish; new leaf flush copper-pink #C27C5E in Mar–Apr; foliage #4E8A3A |
| Bar, banyan, *Ficus benghalensis* | `ghm_veg_banyan_{a,b}` | Chautari, with pipal | 15–20 m, very wide crown; aerial roots | as above | Dark green #2F6B2F |
| Jacaranda, *Jacaranda mimosifolia* | new: `ghm_veg_jacaranda_{a,b}` | Avenues: Tundikhel edges, Durbar Marg, Lazimpat, Maharajgunj, Baluwatar, Kamaladi; Rana-era gardens; introduced by the Ranas (1920s per one historian; 150 years per another) [S Onlinekhabar] | 8–15 m, umbrella crown | 8–15 m along URBAN arterials in the north and centre | **Violet bloom #8E6CC8 / #A58AD8 from March to early May** (peak mid-April to early May; Kathmandu Post photo story 8 May 2026) [S]; fallen-petal decal #9C7FD0 on the road under the tree |
| Silky oak, *Grevillea robusta* | new: `ghm_veg_grevillea` | Roadsides, Rana-era palace avenues (brought by Jung Bahadur after 1850–51) [S Nepali Times], Bagmati corridor [S] | 18–30 m, narrow conical crown, fern-like leaves | 10–20 m along older roads | Golden-orange comb flowers #F2A33A in Apr–May |
| Bottlebrush, *Callistemon* | new: `ghm_veg_bottlebrush` | Roadside medians, gardens [S] | 4–8 m, weeping | 6–10 m on medians | **Red brushes #D7263D in Mar–May** (plus a smaller autumn flush) |
| Camphor, *Cinnamomum camphora* | generic broadleaf | Bagmati corridor, roadsides [S] | 10–20 m, dense round crown | — | Glossy green #3F7F3A, red-tinted new leaves |
| Eucalyptus | generic | Old plantations, campus edges, riverbanks (Rana era) [S] | 20–35 m, thin crown | — | Blue-green #7FA08A, pale trunk #D9D2C3 |
| Poinsettia, lalupate | shrub (new: `ghm_veg_lalupate`) | Garden walls, roadsides | 1.5–4 m shrubs | Clumps of 1–3 by houses | **Red bracts #D11F2A Nov–Feb** (Tihar to winter); green rest of year |
| Bougainvillea | shrub on walls | Compound walls, gates | climbs 2–6 m | 20% of urban compound walls | Magenta #C2185B / orange #F57C00 / white, bloom Mar–Jun and Oct–Nov |
| Bauhinia, koiralo | small tree | Peri-urban | 6–10 m | — | White-pink flowers #F2D7E6 Feb–Apr |
| Silk-cotton, simal | `ghm_veg_simal_{a}` | River edges, open ground | 20–30 m | rare | Leafless with red flowers #D32F2F Feb–Mar |
| Bamboo (bans) | `ghm_veg_bamboo_{a,b}` | Beside village houses, field edges, riverbanks; **Garden of Dreams yellow bamboo** [S] | Clumps Ø 3–6 m, 8–15 m tall | 1 clump per 2–5 peri-urban/village houses | Green #6FA03A; yellow-culm variant #D8C24A |
| Juniper, dhupi | `ghm_veg_juniper_{a,b}` | Temple and gompa courtyards, Boudha | 3–8 m, conical | as tagged | Dark blue-green #2F5D4A |
| Banana | `ghm_veg_banana_{a,b}` | Peri-urban house gardens | 3–5 m | 0–3 per garden | Bright green #7DBA3A |

Street-tree rule for URBAN arterials without OSM trees [E]: 30% of segments get a tree row on one or both sides; species mix by district: north/central (Lazimpat, Durbar Marg, Tundikhel, Maharajgunj) jacaranda 45%, silky oak 20%, bottlebrush 15%, camphor/other 20%; elsewhere jacaranda 20%, bottlebrush 25%, silky oak 15%, ficus 15%, other 25%. Old core: no street trees (streets too narrow), only courtyard and chautari trees.

---

## 10. Parks and gardens (OSM areas [O] where noted)

| Park | OSM area [O] | Character for the generator [E unless S] |
|---|---|---|
| Garden of Dreams (Swapna Bagaicha) | 6,895 m² [S] | Built 1920 by Field Marshal Kaiser Shumsher Rana, designed by Kishore Narshingh [S]; Edwardian formal axis with informal planting; pavilions (originally six, one per Nepali season), amphitheatre, ponds with water lilies, pergolas, urns, balustrades, birdhouses [S]; yellow bamboo, tree ferns, palms [S]. Cream/white classical pavilions #F1EAD8 with green trims; ticketed and quiet: 0.5–2 visitors per 100 m² |
| Ratna Park (रत्न पार्क) | 18,481 m² [O] | Central city park beside the old bus park and Rani Pokhari; lawns, benches, chatpate and peanut vendors on its edges, crows roosting [E] |
| Tribhuvan Park (Thankot) | 60,397 m² [O] | Picnic park on the western rim |
| Balaju Park (Baise Dhara) | 46,584 m² [O] | Picnic park with the 22 stone spouts (Baise Dhara) [V] |
| Bhrikuti Mandap | 21,486 m² [O] | Exhibition ground; fairs |
| Shankha Park, Manjushree Park, UN Park (Bagmati), Pushpalal Memorial Park, Bhandarkhal (Patan, inside the palace grounds) | 12,017 / 12,519 / 40,672 / 19,769 / 16,753 m² [O] | Lawns, joggers in the morning (06–08), couples and families in the evening |
| Tundikhel | (open ground) | Big parade ground; kite flying, cricket and football, jacaranda edges |

Park density [E]: mornings 06:00–08:00 walkers/joggers 2–5 per 1,000 m²; afternoons 3–8 per 1,000 m² (Saturday ×2); 1 vendor per 50 m of edge.

---

## 11. Forest rim

| Band | Elevation | Dominant species (mix % [E]) | Look (hex [E]) | Source |
|---|---|---|---|---|
| Valley floor fringe, sacred groves | 1,300–1,400 m | Mixed: *Schima*, *Castanopsis*, figs, *Alnus* along streams; Mrigasthali and Swayambhu hill groves | mid green #4C8B3E | [S] Shivapuri NP |
| **Schima–Castanopsis** (chilaune–katus) | 1,400–1,800 m | *Schima wallichii* 40, *Castanopsis indica/tribuloides* 30, *Alnus nepalensis* (utis) 10 by streams, others 20 (*Engelhardia*, wild Himalayan cherry, *Myrica*) | Broad rounded crowns; Schima white flowers #F4F1E6 in May–Jun; Castanopsis yellow catkins #D9C46A in spring; foliage #4A7F36 | [S] Phulchoki zonation, Shivapuri NP |
| **Chir pine** | 1,400–2,000 m on dry **south-facing** ridges; plantations at Nagarjun, Chandragiri, Kirtipur and Rani Ban edges | *Pinus roxburghii* 70–100 (+ some planted *P. patula* and *P. wallichiana* [V]) | Open, sunny, needle-litter floor #A9773F; foliage #4F7A3A; reddish bark #7A4A2E | [S] Shivapuri NP; plantation species [V] |
| **Oak–laurel with rhododendron** | 1,800–2,400 m | *Quercus lanata*, *Q. lamellosa*, *Lithocarpus*, laurels 60; *Rhododendron arboreum* 20; small bamboo patches 10; others 10 | Dense dark crowns #3B6B34; **rhododendron red #C8102E (pink/white variants) Feb–Apr**, earlier at lower altitude | [S] Phulchoki, Shivapuri |
| **Evergreen (brown) oak** | 2,400–2,760 m (Phulchoki 2,782 m, Shivapuri 2,732 m summits) | *Quercus semecarpifolia* 70–90, *Rhododendron*, little bamboo | Mossy trunks #6B7A4A; brown-backed leaves #7A6A4A; fog often | [S] Phulchoki (94% of carbon at upper zones is *Q. semecarpifolia*) |

Stem density for scatter [E]: real forests of these types hold several hundred stems ≥ 10 cm DBH per hectare; the game cannot afford 1:1 (CONTENT_COVERAGE "1:1 density"). Near-ring target: 150–300 visible tree instances per hectare in closed broadleaf forest, 80–150 in pine, 30–60 in degraded/community forest near villages, as clumps of 3–7, with impostor clusters mid-ring. Aspect rule: on slopes facing 135–225° (south) below 2,000 m, swap 60% of broadleaf weight to chir pine. Shivapuri–Nagarjun NP elevation 1,350–2,732 m [S].

Forest life: rhesus macaques (§7.3), barking deer and wild boar (rare, M4 assets), birds (laughingthrushes, babblers; the spiny babbler is a hill scrub bird, not urban). Hikers on Saturdays (§3.1).

---

## 12. Fields and crop calendar (valley floor)

Valley farmland in the box: 78.4 km² mapped as `landuse=farmland` [O] (cropland is under-mapped; use WorldCover cropland as well).

| Month (Nepali) | Paddy fields (lowland, khet) | Upland (bari) and winter crops | Colour of the floor (hex [E]) | Field life [E] |
|---|---|---|---|---|
| Jan (Poush–Magh) | Stubble or wheat seedlings | **Mustard (tori) in bright yellow bloom Dec–Jan**, wheat green, potato | mustard #F2D22E, wheat #8DBF4A, bare #A88C65 | Few farmers; egrets fewer |
| Feb–Mar (Falgun–Chaitra) | Wheat growing; mustard pods | Wheat green, garden vegetables | #7FB04A | Irrigation, vegetable plots |
| Apr–May (Baisakh–Jestha) | Wheat harvest (golden #D9B65A) late April; fields burned/ploughed, dusty | Maize sown on bari | stubble #C9A86A, ploughed #8C6A4A | Wheat harvest crowds 2–6 per ha |
| Jun–Jul (Asar–Shrawan) | **Flooded, transplanting from Asar 15 (Dhan Diwas, ≈ 29–30 June)** [S]: mirror-water fields #8FB5C2 with seedling rows #9CCB4A; mud play on Ropain day | Maize tall | water/young green | Planting crowds 4–10 per ha on Asar 15, singing; egrets 5–40 per ha; buffalo/tractor ploughing |
| Aug–Sep (Bhadra–Ashwin) | Lush green paddy #5DAA3A, 0.6–1.0 m tall | Maize harvest | deep green | Few people; frogs at night |
| Oct–Nov (Kartik–Mangsir) | **Golden ripening #D9B44A**, harvest from Kartik into Mangsir [S]; sheaves and round straw stacks (pira) | Mustard and wheat sowing | gold → stubble | Harvest crowds 3–8 per ha; threshing on mats; drying grain on roofs |
| Dec (Mangsir–Poush) | Stubble, straw stacks | Mustard and wheat green | #A8A060 | Quiet; morning fog in the valley until 09–10 h |

Brick kilns on the south-east and south valley floor (Bhaktapur, Lalitpur) smoke in the dry season (Dec–May) and are idle in the monsoon [E; CONTENT_COVERAGE].

---

## 13. Seasonal colour calendar (one row per palette switch)

| Period | Valley-wide accents (hex [E]) |
|---|---|
| Jan | Mustard yellow fields, lalupate red, morning fog, clear Himalaya views, down jackets |
| Feb–Mar | Rhododendron red on the rim (lower first), simal red, pipal copper flush, koiralo pink-white, Losar flags |
| Late Mar – early May | **Jacaranda violet** in the city, bottlebrush red, silky oak gold, hazy skies (pre-monsoon), dust |
| Jun–Sep | Monsoon: saturated greens, mirror paddies, wet streets with puddles, umbrellas, clouds hiding the mountains |
| Oct–Nov | Golden paddy, kites, marigold orange everywhere (Dashain–Tihar), clear skies, tourists peak |
| Dec | Stubble and straw stacks, lalupate red, fog, short days |

---

## 14. Daily rhythm (multipliers on the §3.1 and §4.2 peak targets) [E]

Sunrise ranges from about 05:05 (June) to 06:55 (January) Nepal time [E]; use the sky system's sun.

| Time | Pedestrians (old core / urban / sacred) | Vendors | Traffic (veh) | What happens |
|---|---|---|---|---|
| 04:30–05:30 | 0.05 / 0.02 / 0.2 | 0 | 0.05 | Roosters, first bells, kora walkers start at Boudha and Swayambhu, milk deliveries |
| 05:30–08:00 | 0.4 / 0.3 / **1.0** | 0.5 (veg, flowers, tea, sel roti) | 0.3 | **Morning puja**: bells every few seconds at busy shrines, women with puja plates (offering plate prop), joggers in parks, tea stalls busy, rooftop sun |
| 08:00–10:30 | 0.8 / 0.8 / 0.6 | 0.8 | **1.0 (rush)** | School buses and kids (07–09:30), shutters going up 08:30–10:00, office commute |
| 10:30–13:00 | 1.0 / 0.6 / 0.5 | 1.0 | 0.7 | Bazaar at full swing; porters; tourists out |
| 13:00–15:00 | 0.8 / 0.5 / 0.4 | 0.8 | 0.6 | Lunch: momo and khaja shops busy; dogs asleep |
| 15:00–17:00 | 0.9 / 0.7 / 0.6 | 1.0 | 0.8 | Schools out (15–16:30), chatpate at school gates, kites (season) |
| 17:00–19:00 | **1.0** / **1.0** / **1.0** | 1.0 | **1.0 (rush)** | Evening rush; evening aarti at Pashupati at dusk (bells, lamps, chanting); Boudha evening kora with butter lamps; crows fly to roosts; black kites gone |
| 19:00–21:00 | 0.6 / 0.4 / 0.3 | 0.4 (packing away) | 0.5 | Shutters down 19:30–21:30; Thamel stays busy (×1.0 until 22:00) |
| 21:00–23:00 | 0.1 / 0.05 / 0.05 | 0 | 0.2 | Dogs awake and barking; Thamel music |
| 23:00–04:30 | 0.01 | 0 | 0.05 | Dogs, occasional trucks (trucks enter the city mostly at night [V]) |

Weekly: **Saturday** is the main weekly holiday: shops in residential areas 50–70% closed, temples and parks ×1.5–2, traffic ×0.6, hikers on the rim [V]. Festival days override (§6).

Weather modifiers [E]: rain → pedestrians ×0.5, umbrellas 30–60%, vendors ×0.3, dogs shelter; winter fog until 09–10 h; pre-monsoon haze lowers far visibility.

---

## 15. Ambient sound hooks (licence-clean)

Only CC0 or public-domain recordings or procedural synthesis may ship (task rule; LICENSES.md). Practical sources: Freesound filtered to **CC0 only** (licence checked per file and recorded in LICENSES.md); procedural synthesis in code for bells (modal partials), shutters (noise bursts plus a comb filter), horns (two detuned square/saw oscillators), engines (granular or additive with RPM), crowd walla (layered noise-shaped babble, no intelligible words), birds (FM chirps for sparrows and mynas, a pitch-swept whistle for black kites, a noise-shaped caw for crows), dogs (formant-filtered bark), wind. Do **not** use CC-BY-NC, BBC RemArc, or "royalty-free" packs (not CC0).

| Area type | Bed | One-shots (rate per minute [E]) |
|---|---|---|
| OLD_CORE | Crowd walla, footsteps on brick, scooters | Shrine bell 2–20 (morning peak), horn 10–30, shutter 0–3 (morning/evening), vendor calls 2–6, pigeons wings at squares, distant bhajan (synth harmonium drone) at dusk |
| URBAN | Traffic hum, horns | Horn 20–60, bus conductor call 1–3 (generic "chha, chha" style shout, no real route names), crow caws 2–6 |
| PERI_URBAN | Light traffic, roosters, dogs | Dog bark 1–4, rooster 0–2 (morning), kite (bird) whistle 0–1 |
| FIELDS | Wind, insects; frogs at night in the monsoon | Cattle egret croak 0–2, water trickle by irrigation channels, distant planting songs on Asar 15 |
| FOREST | Wind in leaves, laughingthrush chatter (synth), cicadas in summer | Woodpecker drum 0–1, monkey calls near troops |
| Airport vicinity | Aircraft (see the airport research) | — |

Footstep surfaces needed: brick (old core), stone flags (squares), asphalt, dirt, mud (monsoon), grass, wood (temple platforms), metal (bridge decks), water (puddles).

---

## 16. Cultural and rating rules for street life (reviewer checklist)

1. Prayer flags only at Buddhist sites (B18). Clockwise movement on koras and around stupas and temples.
2. **Kumari never shown**; her chariot appears with a closed canopy [V].
3. No sacrifice, blood, butchery displays or cremation (O11). Dashain is kites, swings, tika and family; goats appear only alive and well.
4. Sadhus and monks are dignified NPCs, never comic props, and there is no paid-photo mechanic.
5. Cows and dogs are never harmed; vehicles stop. Kukur Tihar and Gai Tihar garlands are respectful.
6. Shrines are anchored to OSM objects only; procedural dressing never creates a shrine (O2).
7. No real brands on signs; generic Devanagari/English text.
8. Kite fighting is shown as kites drifting away; no glass-string injury.
9. Porters are shown as strong, cheerful workers, not as pitiable; no "carry for coins" exploitation mechanic [V].
10. Hakupatasi and other ethnic dress appear only where they really are (Newar towns) and in correct proportions, not as costumes on every NPC.

---

## 17. Sources

* OSM data: `pipeline/data/raw/osm/nepal.osm.pbf` (ODbL), measured with a pyosmium script in the session scratchpad (valley box 85.18–85.55 E, 27.55–27.82 N, 250 m cells).
* KMC population 2021 (862,400; 49.45 km²; 17,440/km²): https://en.wikipedia.org/wiki/Kathmandu_District , https://en.wikipedia.org/wiki/2021_Nepal_census
* Street dogs: https://kathmandupost.com/valley/2016/05/20/21856-stray-dogs-in-capital ; https://www.icam-coalition.org/wp-content/uploads/2019/08/1-Dog-population-assessment-Kathmandu.pdf (14.2 dogs/km of street, 33.1% female); https://en.wikipedia.org/wiki/KAT_Centre ; https://katcentre.org/animal-birth-control
* Stray cattle: https://thehimalayantimes.com/ampArticle/111236 ; https://thehimalayantimes.com/ampArticle/164360 ; https://kathmandupost.com/miscellaneous/2018/02/03/holy-strays ; https://www.npr.org/sections/goatsandsoda/2015/07/10/421463422/kathmandu-is-cowed-by-abandoned-cattle
* Monkeys: https://thehimalayantimes.com/ampArticle/95521 ; https://thehimalayantimes.com/ampArticle/159499 ; https://kathmandupost.com/valley/2019/03/30/study-monkey-population-threatens-heritage-sites ; https://nepjol.info/index.php/njz/article/view/30870 ; https://thehimalayantimes.com/kathmandu/monkeys-causing-no-harm-devotees-pashupati-area-development-trust/
* Urban birds: https://english.onlinekhabar.com/urban-birds-up-kathmandu.html ; https://thehimalayantimes.com/kathmandu/urban-bird-count-up-in-kathmandu-valley ; https://news.mongabay.com/2024/10/as-kathmandus-birds-get-used-to-humans-biodiversity-suffers-studies-show
* Street trees and jacaranda: https://english.onlinekhabar.com/the-beauty-of-jacaranda-in-kathmandu-and-the-facts-photos.html ; https://english.onlinekhabar.com/jacaranda-in-kathmandu-spring-flowers.html ; https://kathmandupost.com/visual-stories/2026/05/08/jacaranda-bloom-paints-kathmandu-purple ; https://ekantipur.com/photo_feature/2026/05/06/en/purple-spring-on-the-roadsides-of-the-valley-photos-41-30.html ; https://nepalitimes.com/right-trees-for-right-seasons-in-kathmandu ; https://www.ncbi.nlm.nih.gov/pmc/articles/PMC10622594/
* Forests: https://en.wikipedia.org/wiki/Shivapuri_Nagarjun_National_Park ; https://nepjol.info/index.php/IJE/article/view/13236 ; https://banglajol.info/index.php/BJB/article/view/81655 ; https://www.keybiodiversityareas.org/site/factsheet/14340 ; https://datazone.birdlife.org/site/factsheet/phulchoki-mountain-forests-iba-nepal
* Paddy calendar: https://english.onlinekhabar.com/asar-15-ropai-festival-paddy.html ; https://english.ratopati.com/story/68545/planting-from-mud-to-rice ; https://www.nepalhikingteam.com/rice-planting-festival-in-nepal
* Festivals: https://en.wikipedia.org/wiki/Indra_Jatra ; https://thehimalayantimes.com/kathmandu/indra-jatra-begins-with-erection-of-lingo/ ; https://en.wikipedia.org/wiki/Rato_Machindranath_Jatra ; https://thehimalayantimes.com/ampArticle/166984 ; https://nepaltraveller.com/sidetrack/bisket-jatra-the-vibrant-festival-of-bhaktapur ; https://ekantipur.com/news/2026/04/14/en/the-splendor-of-biscay-in-the-city-of-festivals-bhaktapur-57-30.html
* Kites: https://kathmandupost.com/art-culture/2021/09/23/rediscovering-the-joy-of-flying-kites ; https://www.kathmandupost.com/national/2024/10/03/kites-over-kathmandu-sky ; https://english.onlinekhabar.com/reasons-fly-kites-dashain-kathmandu.html ; https://nepalitimes.com/kite-fight-over-kathmandu
* Clothing: https://www.ecs.com.np/features/dented-pride-the-story-of-daura-suruwal-and-dhaka-topi ; https://thehimalayantimes.com/opinion/daura-and-suruwal-their-history-journey ; https://nepaltraveller.com/sidetrack/dhaka-topi-national-pride ; https://kathmandupost.com/valley/2015/01/13/bhadgaule-topi-winning-over-young-hearts ; https://en.wikipedia.org/wiki/Nepal_Police_School ; https://kuhs.edu.np/wp-content/uploads/2025/07/HSS_Uniform.pdf
* Tourists: https://www.sharesansar.com/newsdetail/tourist-arrivals-in-nepal-reach-128443-in-october-2025-11-03 ; https://en.himalpress.com/tourist-arrivals-cross-one-million-mark-for-third-straight-year/
* Vehicles: https://www.en.meroauto.com/?p=15118 ; https://www.nepaldrives.com/only-3-1-nepalese-families-own-cars-27-3-own-two-wheelers-census-finds ; https://thehimalayantimes.com/kathmandu/traffic-management-turns-daunting-as-vehicles-exceed-1-1-mil-mark
* Wires: https://thehimalayantimes.com/nepal/nea-issues-45-day-ultimatum-on-messy-cables ; https://nepalitimes.com/bijuli-ko-tar-tar-tar ; https://english.onlinekhabar.com/service-providers-begin-organising-overhead-utility-cables-in-kathmandu-photos.html ; https://english.onlinekhabar.com/overhead-structures-removed-clean-and-organised-new-road.html ; https://kathmandupost.com/kathmandu/2023/05/10/cable-firms-join-hands-with-kmc-to-clear-wire-mess
* Garden of Dreams: https://en.wikipedia.org/wiki/Garden_of_Dreams
