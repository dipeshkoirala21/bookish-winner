# Vehicles and traffic of the Kathmandu Valley: research for Wave 2

> Status: research input for Wave 2 "Kathmandu comes alive", 2026-10-05. Feeds ARCHITECTURE §7.6 (vehicles) and §7.7 (traffic), ASSET_MANIFEST §8 (vehicle ids reused below), CONTENT_COVERAGE gaps R4, S2, S3, S4, B14, B20 and work items D2, 1.4, 1.8. Companion: [street_life.md](street_life.md) §5.7 (parked vehicles, traffic mix).
>
> Evidence tags: **[S]** sourced (URL in §9), **[O]** measured from OSM in this repo (`pipeline/data/raw/osm/nepal.osm.pbf`, valley bbox 85.18–85.56 E, 27.55–27.83 N, scripts run with pyosmium on 2026-10-05), **[E]** estimate (reasoned from references; replace when better data arrives), **(verify)** check against current photos or a field visit before final art.
>
> Rules carried from ASSET_MANIFEST §8: **all vehicles are generic**. Dimensions below come from real reference classes so proportions read true, but models carry no badges, grilles, logos, model names, operator names or real liveries. Plates carry fictional numbers. Sounds ship only from CC0 / public-domain sources or procedural synthesis.

---

## 1. Headline numbers

| Fact | Value | Tag |
|---|---|---|
| Side of road | **Drive on the LEFT**, right-hand-drive vehicles; overtake on the right | [S] Wikipedia plates/traffic page |
| Registered vehicles, Bagmati Province | ≈ 2.3 million; 1.58 million two-wheelers at the Gurjudhara (Kathmandu) two-wheeler office alone (Aug 2025) → two-wheelers ≈ **69%** of the fleet | [S] Meroauto (digitization article) |
| Vehicles managed in the valley | "more than 2 million", mostly by **hand signals and whistles** | [S] Meroauto, Ratopati (SSP Kafle) |
| Person-trip mode share (JICA/MoPIT 2011, ~3.4 M trips/day) | Walk 40.7%, bus/public 27.6%, motorcycle 23–25.8%, car 4.2%, bicycle 1.5% | [S] CEN factsheets |
| Trend 1991→2011 | Motorcycle share ×2.5–2.8; bicycle ÷4.5 (6.6% → 1.5%) | [S] CEN |
| Public-transport vehicles | < 3% of the registered fleet but ≈ the same trip share as all private vehicles | [S] CEN |
| Average speed on major streets | ≈ **20 km/h** | [S] Onlinekhabar / Himalayan Times |
| Posted limit on main valley roads | **50 km/h** (incl. Kalanki–Koteshwor Ring Road) | [S] Onlinekhabar "50 kmph speed limit" |
| Peaks | 08:00–10:00 and 16:00–19:00; ~50 min to clear Koteshwor and neighbouring junctions in the peak | [S] arroshan blog (weak), Onlinekhabar |
| Traffic signals in OSM, valley | 50 `highway=traffic_signals` nodes; police-controlled chowks dominate | [O] |
| Police points in OSM, valley | 134 `amenity=police` nodes (incl. traffic posts) | [O] |
| Bus stops / bus stations in OSM, valley | 380 `bus_stop` nodes; 156 `bus_station` nodes (+ polygons) | [O] |
| Fuel stations / taxi stands in OSM, valley | 138 / 22 | [O] |
| Speed cameras in OSM, valley | 24 | [O] |

---

## 2. Traffic mix (what the spawner draws)

### 2.1 Modal and fleet shares

| Measure | Two-wheeler | Car / taxi / jeep / van | Bus / minibus / micro / tempo | Truck / tanker / tipper / tractor | Bicycle | Source |
|---|---|---|---|---|---|---|
| Registered fleet (Bagmati) | ≈ 69% | ≈ 22–25% [E] | < 3% | ≈ 3–5% [E] | n/a (not registered) | [S] Meroauto; CEN |
| Person trips (2011) | 23–26% | 4.2% | 27.6% | — | 1.5% | [S] CEN |
| **Moving vehicles on urban roads (game spawn weights)** | **62%** | **20%** | **8%** | **4%** | **3%** (+3% other: e-rickshaw, ambulance, police, school bus) | [E] from fleet share × trip length; consistent with street_life.md §5.7 |

Two-wheeler split for the spawner [E from 2025 import data, Meroauto]: in H1 2025 ICE scooter imports were ~46.6 k units (Honda 44%, TVS 31%, Yamaha 21%) against ~86 k motorcycles in a comparable half-year (Bajaj 55%). So **scooter : commuter motorbike ≈ 35 : 65** of new two-wheelers; add ~8% electric scooters in the city core [E]. Cruisers ("Royal-style") ~3% [E].

Car split [E]: small hatchbacks (Alto/i10/Swift class) 45%, compact EV crossovers 20% (EVs dominate recent car imports; verify current share), SUVs/jeeps 20%, sedans 7%, pickups 8%.

### 2.2 Spawn mix by road class (per 100 moving vehicles) [E]

| Road class (OSM) | Two-wheeler | Car+taxi | Micro/minibus | Big bus | Safa tempo | Truck/tanker/tipper | Tractor | Bicycle | Cycle rickshaw | Other (ambulance, school bus, police) |
|---|---|---|---|---|---|---|---|---|---|---|
| trunk (Ring Road, Araniko, Tribhuvan Rajpath) | 55 | 22 | 7 | 4 | 1 | 7 | 1 | 2 | 0 | 1 |
| primary (Kantipath, Putalisadak, Lazimpat) | 62 | 20 | 7 | 2 | 3 | 2 | 0 | 3 | 0 | 1 |
| secondary / tertiary | 65 | 19 | 5 | 1 | 3 | 2 | 1 | 3 | 0 | 1 |
| residential / unclassified | 72 | 15 | 1 | 0 | 0 | 2 | 2 | 6 | 0 | 2 |
| old-core lanes (Asan, Indrachowk, Patan, Bhaktapur cores; living_street, pedestrian) | 70 | 3 | 0 | 0 | 0 | 0 | 0 | 15 | 10 | 2 |
| peri-urban / rim roads (Bungamati, Khokana, Kirtipur, Thimi outskirts) | 60 | 12 | 6 | 3 | 0 | 6 | 7 | 6 | 0 | 0 |

Cycle rickshaws: rare in Kathmandu today, a handful of tourist rickshaws around Thamel–Asan–Basantapur (ASSET_MANIFEST already lists them as M4 Terai, "optional early use in Thamel"). Keep to the old-core tiles only, max 2 alive at once [E].

---

## 3. Vehicle spec table (cartoon procedural models)

Dimensions are metres, from the named **reference class** (public spec sheets of the class; generic models must stay within ±5% for the silhouette to read). Cartoon exaggeration is applied on top: wheels ×1.15, cab/head ×1.1, body length ×0.95 (ASSET_MANIFEST §8 squash rig). Colours are hex presets for the instanced tint; "share" is within the vehicle's own category unless said otherwise.

| Asset id (ASSET_MANIFEST §8) | Vehicle | Reference class (for proportions only) | L × W × H (m) | Wheelbase (m) | Wheel Ø (m) | Seats | Max speed (real) / game cruise | Colour presets (hex) | Share of moving traffic | Tag |
|---|---|---|---|---|---|---|---|---|---|---|
| `ghm_veh_scooter_a` | Step-through scooter 110–125 cc | Dio / Jupiter / Ray class | 1.81 × 0.72 × 1.15 | 1.26 | 0.48 (12″/10″) | 2 | 85 / 30 urban | #E53935 red, #1E88E5 blue, #FDD835 yellow, #FAFAFA white, #212121 black, #8E24AA purple, #43A047 green, #FB8C00 orange | 21% of all (35% of 2W) | [E] class specs |
| `ghm_veh_motorbike_commuter_a` | Commuter motorbike 125–160 cc | Pulsar / Shine / Apache class | 2.05 × 0.77 × 1.08 | 1.32 | 0.62 (17″) | 2 | 110 / 35 urban, 50 trunk | #212121 black (35%), #C62828 red, #1565C0 blue, #9E9E9E grey, #FAFAFA white, #2E7D32 green | 39% of all | [E] |
| `ghm_veh_motorbike_cruiser_a` | Cruiser 350 cc | Classic-style retro single | 2.15 × 0.80 × 1.09 | 1.39 | 0.66 (19″/18″) | 2 | 120 / 45 | #212121, #3E2723 maroon-brown, #455A64 gunmetal, chrome #CFD8DC | 2% | [E] |
| (new) `ghm_veh_escooter_a` | Electric scooter | generic e-scooter | 1.80 × 0.70 × 1.10 | 1.30 | 0.45 | 2 | 60 / 30 | pastels #80DEEA, #F8BBD0, #FFF59D, #FAFAFA | 5% | [E] |
| `ghm_veh_bicycle_a` | City roadster / MTB | Hero-style roadster | 1.80 × 0.60 × 1.05 | 1.12 | 0.71 (28″) | 1 (+carrier) | 25 / 14 | #212121 black (roadster), #1565C0, #C62828, #2E7D32 | 3% | [E] |
| `ghm_veh_rickshaw_cycle_a` | Cycle rickshaw (old core) | Indian-pattern tricycle rickshaw | 2.60 × 1.10 × 1.90 (hood up) | 1.55 | 0.66 | 2 + puller | 15 / 8 | body #1565C0 / #C62828 / #2E7D32, hood #212121 or #FBC02D, tassels #E91E63 | old core only | [E] (verify current Kathmandu presence) |
| `ghm_veh_tempo_safa_a` | **Safa tempo** (electric 3-wheeler, shared, ~10–12 pax) | Vikram-type chassis, battery-electric (72 V, 12 deep-cycle batteries, ~60 km range) | 3.60 × 1.45 × 1.90 [E] | 2.10 [E] | 0.60 | 10–12 (2 rear benches facing) + driver | 40 / 25 | body **white #F4F4F0**, **green** trim/lettering #2E8B57, black lower skirt #263238; route-board yellow #FFD54F | 2% (secondary/tertiary inner routes) | [S] CEN EV factsheet, Himal (714 vehicles, white "green-nature"); dims [E] |
| `ghm_veh_microvan_a` | **Microbus** ("micro", 14–15 seats) | Hiace H200 standard-roof class (diesel; some new electric micros) | 4.70 × 1.70 × 1.98 (high-roof 5.38 × 1.88 × 2.29) | 2.57 (3.11) | 0.66 | 14–15 | 140 / 35 | **white #F5F5F5** base (80%) with a coloured belt stripe #1565C0 / #C62828 / #2E7D32 / #F9A825; Devanagari route board on the windscreen (cream #FFF3E0, red text #B71C1C) | 5% | [S] Nepali Times (Hiace micros, 14–15 seats); dims [E] class |
| `ghm_veh_bus_city_a` (Sajha livery preset) | **Sajha Yatayat city bus** (diesel + 40 CHTC e-buses; fleet 111) | 10–10.5 m low-entry city bus | 10.5 × 2.50 × 3.20 [E] | 5.4 [E] | 0.95 | 36 seats, 42+ total | 80 / 35 | **green** body #2E7D32 (main), lighter green #66BB6A band, white #FFFFFF roof and window band, yellow #FDD835 route display; no operator logo | 0.5% (≈ 111 buses) | [S] Kathmandu Post (green, no repaint), Onlinekhabar (36 seats, 200 km); dims [E] |
| `ghm_veh_bus_city_a` (private minibus preset) | City minibus (private committees: Mahanagar, Nepal Yatayat, Bhaktapur Minibus Samiti…) | 7–8 m minibus on light-truck chassis | 7.2 × 2.10 × 2.85 | 3.8 | 0.85 | 25–30 | 80 / 30 | multi-colour bands: cream #FFF8E1 base + red #C62828 / blue #1565C0 / orange #EF6C00 stripes, painted floral flourishes at corners, route board in Devanagari | 2% | [E] (verify) |
| `ghm_veh_bus_tourist_a` | **Long-distance bus** (Kathmandu–Pokhara, night buses) with painted decoration | 11 m front-engine coach on truck chassis | 11.0 × 2.50 × 3.35 (+0.35 roof rack) | 5.6 | 1.00 | 40–45 | 90 / 50 highway | base #FAFAFA or #FFF8E1; art bands #D32F2F, #FBC02D, #1976D2, #388E3C; truck-art panels (§8.1 atlas): floral borders, mountains, peacocks, "Horn Please" / "Speed Control" / "Road King" style slogans (generic wording, review) | 0.5% (trunk entries: Kalanki/Thankot, Koteshwor, Gongabu bus park) | [S] Nepali Times "Art and poetry in motion" |
| `ghm_veh_taxi_small_a` | **Taxi** | Alto / Eon / old 800-class hatchback | 3.40 × 1.48 × 1.48 (800-class 3.34 × 1.44 × 1.41) | 2.36 (2.18) | 0.55 | 4 | 140 / 35 | **white #F5F5F5** (≈70%) or **yellow #FBC02D** (≈30%) [E]; **black plate** (public class); roof sign "TAXI / ट्याक्सी" yellow #FFEB3B with black text | 4% | [S] In Your Pocket, World Travel Guide ("small white or yellow cars… black number plates… taxi sign on the roof"); split [E] (verify) |
| `ghm_veh_hatchback_a` | Private hatchback (dominant car) | i10 / Swift / Alto class | 3.77 × 1.68 × 1.52 | 2.43 | 0.58 | 5 | 160 / 35 | white #F5F5F5 30%, silver #BDBDBD 20%, red #C62828 15%, grey #616161 10%, blue #1565C0 10%, black #212121 5%, orange/teal 10% | 9% | [E] |
| `ghm_veh_ev_compact_a` | Compact EV crossover | Atto-3 / Nexon-EV / Neta-V class | 4.10 × 1.75 × 1.58 | 2.55 | 0.66 | 5 | 150 / 35 | white, grey #9E9E9E, blue #1E88E5, teal #00897B | 4% | [E] (EV share verify) |
| `ghm_veh_suv_a` / `ghm_veh_jeep_mountain_a` | SUV / jeep | Scorpio / Bolero class | 4.46 × 1.82 × 1.98 | 2.68 | 0.75 | 7 | 150 / 40 | white #FAFAFA 40%, black #212121, silver, maroon #6D1B1B | 4% | [E] |
| (pickup) | Pickup / small goods | Bolero-pickup class | 4.86 × 1.70 × 1.86 | 3.01 | 0.70 | 2 | 120 / 35 | white, red, blue | 1.5% | [E] |
| `ghm_veh_truck_painted_a` | **Decorated truck** ("Horn Please") | Tata LPT 1613 class 2-axle, 6-wheel | 8.10 × 2.45 × 3.30 (crown headboard to 3.60) | 4.80 | 1.05 | 2–3 | 80 / 40 | cab #D32F2F / #1976D2 / #FBC02D / #388E3C; body panels #FF8F00 / #00897B; headboard art + tassels; rear "HORN PLEASE" + Devanagari slogans (generic, review); mudflaps #212121 | 2% (trunk); night ×3 | [S] Onlinekhabar "Tattooed trucks of Nepal – Horn Please", Nepali Times |
| (tipper) `ghm_veh_truck_plain_a` | Tipper (sand, bricks) | Tata 2518-class 10-wheel tipper | 7.60 × 2.50 × 3.10 | 3.8 + 1.35 tandem | 1.05 | 2 | 80 / 35 | cab #FBC02D / #D32F2F, tipper box #F57F17 / grey | 1% | [E] |
| `ghm_veh_tanker_a` | **Water tanker** (private drinking-water supply, very common in the valley) | 1109–1613 class with 6–12 kL tank | 7.50 × 2.40 × 3.00 | 4.20 | 1.00 | 2 | 80 / 30 | tank #1E88E5 blue or #FAFAFA white, cab any; "पिउने पानी" ("drinking water") lettering style, generic | 1% (residential ×2 mornings) | [E] (verify) |
| `ghm_veh_tractor_a` | Tractor + trolley | 45–50 hp 2WD tractor + single-axle trolley | tractor 3.45 × 1.75 × 2.10 (no cab, ROPS optional); trolley 3.60 × 1.90 × 1.10 | 1.95 | rear 1.40, front 0.80 | 1 (+ riders on trolley, game: none) | 30 / 18 | #C62828 red, #1565C0 blue, #2E7D32 green, rims #FDD835 | 1% (peri-urban 7%) | [E] |
| (new) `ghm_veh_ambulance_a` | Ambulance | van class (Hiace / Eeco) | 4.70 × 1.70 × 2.10 | 2.57 | 0.66 | 2 + stretcher | 140 / 50 (sirens, others yield) | white #FFFFFF, red #D32F2F band and red "AMBULANCE / एम्बुलेन्स" lettering (reversed on bonnet); blue/red beacon. **No Red Cross / Red Crescent emblem** (protected symbol, ICRC). | 0.3% | [E] (verify) |
| (new) `ghm_veh_bus_school_a` | School bus | 7–9 m midi bus | 8.0 × 2.30 × 3.00 | 4.20 | 0.90 | 30–40 | 80 / 30 | **yellow #FBC02D mandated**, black #212121 band, "SCHOOL BUS / विद्यालय बस" boards front and rear | 0.5%; only 06:30–09:00 and 14:30–17:00 | [S] EducateNepal ("mandatory for school buses to be painted yellow… board in front and rear") |
| (new) `ghm_veh_police_jeep_a` | Police pickup / jeep (generic) | Bolero class | 4.40 × 1.75 × 1.90 | 2.68 | 0.70 | 5 | 140 / 40 | white #FAFAFA + blue #1565C0 band, light bar; generic "POLICE / प्रहरी" text only | 0.2% | [E] (review: non-violent framing, no weapons) |

### 3.1 Number plates

The street today shows two systems side by side [S Wikipedia; Meroauto "Bagmati makes embossed number plates mandatory, again"; Ratopati]:

| Ownership class | Legacy (painted, Devanagari, still the majority on the street) background / text | Typical vehicles in game |
|---|---|---|
| Private | **Red #C62828 / white** | private cars, motorbikes, scooters, private SUVs |
| Public / commercial (for hire) | **Black #111111 / white** | taxis, micros, minibuses, buses, Safa tempos, trucks, tankers |
| Government | **White / red** text | ministry and police vehicles |
| National corporation | Yellow #FDD835 / blue-black | utility trucks (NEA, NOC) |
| Tourist | Green #2E7D32 / white | tourist coaches, hire cars |
| Diplomatic | Blue #1565C0 / white | rare |

Embossed RFID plates (rolling out since 2017, made mandatory again in Bagmati) use Latin script and Arabic numerals on a reflective plate; English Wikipedia gives them as black on white for all classes, which conflicts with other reports (verify with photos before art). **Game recommendation:** use the legacy colour code (instantly readable, and still most common) with **fictional** numbers in the legacy format `बा ## X ####` (e.g. "बा २३ प ४५६७"). The zone code "बा" (Bagmati) plus a class letter: private **प/क/च**, public **फ/ख/ज**, government **ग/झ/ब** [S Wikipedia]. Plate sizes [S Wikipedia, embossed spec]: car front 0.45 × 0.11 m, rear 0.30 × 0.185 m; heavy 0.52 × 0.11 m / 0.36 × 0.21 m; three-wheeler 0.24 × 0.13 m; two-wheeler [E] 0.20 × 0.13 m rear only.

---

## 4. Traffic AI rules

### 4.1 Base rules (real practice → game behaviour)

| # | Rule | Real basis | Game implementation |
|---|---|---|---|
| T1 | **Keep left**; overtake on the right | [S] LHT | Lane graph built with left-hand offsets; overtaking lane = rightmost |
| T2 | Roundabouts circulate **clockwise**; give way to vehicles already on the circle (from the right) | LHT convention [S] | Yield at entry; target circle speed 15–20 km/h |
| T3 | Police-controlled chowk: the officer's arm phase overrides everything | [S] "2 million vehicles directed by hand signals" | Junction controller "POLICE": 2–4 phases, 25–60 s each, with animated officer (§4.3) |
| T4 | Signals only where OSM has `highway=traffic_signals` (50 in valley; e.g. Koteshwor) | [O], [S] | Junction controller "SIGNAL"; cycle 90 s (3 phases 25 s + 3 × 5 s amber/clear) [E] |
| T5 | Two-wheelers **filter** between cars and to the stop line front | universal local practice [E] | Motorbike agents use sub-lane offsets ±0.6 m; at a stop they occupy the "box" in front of cars (max 8 per lane) |
| T6 | Honking: **"no horn" rule since 14 Apr 2017** (fines up to Rs 5,000; emergencies and turning points excepted) | [S] Kathmandu Post 2017, Onlinekhabar (re-tightened) | Low-frequency honk model (§4.4), not constant cacophony; buses/trucks one-tap "pip" at blind corners only |
| T7 | Everyone gives way to **cows** and dogs; vehicles stop, steer round | ARCHITECTURE §7.7 | Soft avoidance radius 3 m, stop if path blocked > 2 s |
| T8 | Micros, minibuses and tempos **stop anywhere** near a stop to pick up | [E], observed | Public agents stop 8–20 s at `bus_stop` props and at random 1 per 600 m on route; conductor-call SFX |
| T9 | Pedestrians cross anywhere; zebra crossings exist (619 OSM `crossing` nodes) | [O] | Yield to pedestrian on zebra; urban cars slow to 15 km/h when a pedestrian is within 6 m ahead |
| T10 | Ambulance with siren: everybody edges left | [S] honking-rule exemption | Agents within 60 m ahead shift left 1 m and slow to 10 km/h |
| T11 | Oneway and turn restrictions from OSM (90 restriction relations Nepal-wide) | [O] CONTENT_COVERAGE S3 | Lane graph honours `oneway`; restrictions after D2 lands |
| T12 | No motor vehicles in temple compounds and Durbar Square cores | CONTENT_COVERAGE L16 (SACRED_NO_VEHICLE) | Spawner never places a motor vehicle in those AREAs; player vehicle gets the friendly "park here" prompt |

### 4.2 Speeds by road class (km/h)

| OSM class | Posted (OSM `maxspeed` most common in valley) [O] | Free-flow real [E] | Peak real [E] | Game agent target (free / peak) |
|---|---|---|---|---|
| trunk (Ring Road express lanes, Araniko, Rajpath) | 40 (46 ways), 80 (38, outer highway), 60 (23) | 45–55 | 10–20 | 45 / 15 |
| trunk service lanes | 40 [E] | 30 | 10 | 28 / 10 |
| primary | 30 (30 ways), 50 (24) | 35–45 | 10–15 | 35 / 12 |
| secondary | 50 (15), 30 (8) | 30–40 | 10–15 | 30 / 12 |
| tertiary | 20 (18), 40 (8) | 25–35 | 10 | 25 / 10 |
| residential / unclassified | 20 (113 + 12) | 15–25 | 8–12 | 18 / 8 |
| old core lanes, living_street | 20 | 8–15 | 5 | 10 / 5 |
| Average on major streets, all day | — | — | ≈ 20 [S] | 20 |

Vehicle-class caps applied on top: bicycle 14, cycle rickshaw 8, tractor 18, Safa tempo 25, trucks −10% of class target, buses −15%.

### 4.3 Traffic police at chowks

* Count [E]: one officer at medium chowks, 2–4 at Kalanki, Koteshwor, Chabahil, Narayan Gopal Chowk, Satdobato, Maitighar, Thapathali, Tripureshwor, Jamal, Lainchaur (police-controlled list in §5.3).
* Station: a small raised **podium/booth** on the central island or at the conflict point, often with a sun umbrella [E] (verify by photo; the asset can be a 1.2 m Ø round podium, 0.4 m high, with a 2.2 m umbrella).
* Uniform for a generic cartoon officer [E] (review/verify): light-blue shirt #90CAF9, navy trousers #1A237E, white gloves, white cap or helmet, reflective vest #CDDC39, whistle. **No weapons**, ever (P11).
* Hand-signal animation set (generic, readable): (1) **STOP front**: one arm raised vertical, palm facing the stream; (2) **STOP behind**: arm extended back, palm out; (3) **GO**: beckoning sweep across the body towards the target exit; (4) **SLOW**: arm out, palm down, patting motion; (5) **turn right allowed**: point + sweep. Whistle blip at each phase change (procedural 2.8–3.2 kHz chirp, 120 ms).
* Phase logic: 2 phases at T-junctions, 3–4 at 4-arm chowks; each 25–60 s, lengthened for the arm with the longest queue (queue > 12 vehicles → +15 s), min 20 s. During a jam the officer waves a short "free right turn" pulse.

### 4.4 Honking model [E]

| Situation | Probability per agent per event | Note |
|---|---|---|
| Blind bend / hill hairpin | 0.6 (trucks, buses 0.9) | the main legitimate use |
| Overtaking a slower vehicle | 0.25 (motorbike 0.35) | short double tap |
| Blocked > 4 s by a stopped vehicle | 0.3, then every 6–10 s at 0.2 | cap 3 honks per blockage |
| Green phase / police wave and the front car does not move in 2 s | 0.4 | |
| Cow on road | 0 (stop quietly) | respect rule |
| Old core, temple squares | ×0.3 | |
| Ambient cap | 20–60 horns per minute audible in URBAN (street_life.md §7) | mixer limits voices to 4 |

Horn sounds: procedural two-tone (car 420 + 510 Hz square→LPF; motorbike 380 Hz single; bus/truck 300 + 360 Hz with musical "pressure horn" variant only on highway trucks, volume −6 dB, rare because pressure horns are banned [S]).

### 4.5 Congestion and time of day [E unless marked]

| Time | Density multiplier (urban trunk/primary) | Notes |
|---|---|---|
| 05:00–07:00 | 0.3 | milk vans, water tankers, tempos start |
| 07:00–08:00 | 0.7 | school buses |
| **08:00–10:00** | **1.0 (jam)** | [S] peak; queue-spawn at Koteshwor, Kalanki, Chabahil, Thapathali, Maitighar |
| 10:00–16:00 | 0.75 | trucks banned in the core by day [E] (verify current rule) |
| **16:00–19:00** | **1.0 (jam)** | [S] peak |
| 19:00–22:00 | 0.5 | |
| 22:00–05:00 | 0.12 | trucks and long-distance buses ×3 relative, tankers |
| Saturday | ×0.6 | weekly holiday (street_life.md) |
| Festival road closures | per-event | chariot routes close (Jawalakhel–Pulchowk for Rato Machhindranath) |

Jam behaviour: when a junction's queue exceeds 25 vehicles, spawn rate on its approaches is throttled to keep the CPU budget, and agents switch to a "creep" mode (5 km/h, 2 m gaps, motorbikes filtering).

---

## 5. Junction catalogue (from OSM, valley)

### 5.1 Ways tagged `junction=roundabout|circular` [O]

Only **21 ways** in the valley carry a roundabout/circular tag, plus **10 `mini_roundabout` nodes**. Most famous chowks are mapped as plain crossings, so the runtime must synthesise islands (§5.4). Diameters are of the **mapped centreline** of the circulating carriageway: for closed rings, perimeter/π; for split arcs, max chord across all arc vertices (dmax).

| OSM way(s) | Name (OSM) | Location (lat, lon) | Class | Mapped diameter (m) | Note |
|---|---|---|---|---|---|
| w171020947 | जावालाखेल (**Jawalakhel**) | 27.67291, 85.31358 | primary | **43.8** (closed, perimeter 138 m) | Central grass island ≈ 36 m Ø [O]; Rato Machhindranath jatra venue |
| w111845390, w111845384, w172008702, w1469161151, w170533894 | **Maitighar Mandala** (माइतिघर) | 27.6945, 85.3203 | trunk | **≈ 57** (dmax of 5 arcs, total arc length 214 m) | Mandala monument island; mapped as `junction=circular` arcs |
| w171599262, w1442118982 | त्रिपुरेश्वर चौक (**Tripureshwor**) | 27.6938, 85.3141 | trunk/primary | **≈ 32** (dmax) | Islands 12.9 m and 14.3 m [O] |
| w610849877, w1444441468 | Kathmandu Ringroad (**Balaju**, Ring Road × Lekhnath Sadak) | 27.7270, 85.3046 | trunk | **≈ 29** (dmax 29.5) | 7 m grass island [O] |
| w550489464 | unnamed (central Kathmandu, near Durbar Marg south / Jamal; verify) | 27.70997, 85.31732 | primary | **25.1** (closed) | |
| w303570216 | unnamed (Imadol / "Tarnani Chok" area, Lalitpur) | 27.64788, 85.35515 | unclassified | **29.2** (closed) | |
| w592151332, w592151333, w327215591 | unnamed (Sinamangal, airport approach) | 27.7005, 85.3564 | unclassified | **≈ 50** (dmax 53.8 incl. arcs) | split arcs |
| w508257269 | unnamed (Gongabu/Tokha side) | 27.74438, 85.30771 | residential | 19.2 | |
| w184260901 | धुम्भाराही मार्ग (**Dhumbarahi**) | 27.72423, 85.33958 | secondary | 15.8 | |
| w494375479, w1154453949 | unnamed (old core near Basantapur; verify) | 27.7037, 85.3091 | tertiary | ≈ 20 | |
| w499793101 | unnamed (Budhanilkantha) | 27.77418, 85.35986 | service | 15.3 | |
| w194056136 | unnamed (Swayambhu bus park) | 27.71816, 85.28303 | service | 13.4 | |

Mini-roundabout nodes [O]: n268310364 (27.73667, 85.30179), n1317087930 (27.69025, 85.34274), n1380073964 (27.64290, 85.37467), n2169415609/n2169415697 (27.7754, 85.3561), n3509987988 (27.66385, 85.26204), n6287470129 (27.64910, 85.30665), n8134465362/3 (27.8296, 85.4532), n12880800092 (27.73886, 85.35889). Render as 4–6 m Ø painted/raised disc.

### 5.2 Ring Road (OSM ref **NH39**) [O] + sources

| Item | Value | Tag |
|---|---|---|
| Loop length | ≈ 27 km | [S] |
| OSM ways | 130 ways, **39.9 km of carriageway centreline** (dual sections counted twice), ref NH39 trunk (39.1 km); short H16 primary pieces | [O] |
| Lanes tagged | `lanes=4` on 16.3 km (northern half, mapped as single two-way way, `width=14–22`), `lanes=2` on 23.4 km (southern half, mapped per direction, `oneway=yes`, `width=6–9`) | [O] |
| Width tags (km) | 14 m: 13.0; 7 m: 10.8; 6 m: 9.5; 16 m: 2.8; 9 m: 2.4; 12 m: 0.7; 22 m: 0.2 | [O] |
| maxspeed tags | 50: 6.2 km, 40: 4.4 km, untagged 29.2 km | [O] |
| **Phase 1 Kalanki–Koteshwor** (southern half, 10.5 km) | Chinese-grant upgrade, built 2013–Dec 2018, handed over 28 Jan 2019: **8 lanes = 4-lane central expressway (2 per direction) + 2 service lanes each side**, design speed 50 km/h on the expressway; median, cycle lanes, footpaths, pedestrian overpasses; Kalanki underpass | [S] Kathmandu Post 2019 |
| **Phase 2 Kalanki–Basundhara** (western/northern, 8.2 km) | Rs 11 bn Chinese grant; widen to **8 lanes excluding service lanes**, **3 m median**, total **62 m** right-of-way with service lanes, footpaths, green belts; flyover at Machhapokhari, bridge at Dhungedhara, 3 pedestrian overbridges. Current: Kalanki–Sitapaila 4 lanes, Sitapaila–Basundhara 2 lanes. Works pushed to **after Dashain 2026** (status in Oct 2026: not yet built) | [S] Kathmandu Post Jun 2026, Himalpress, AidData |
| Northern east (Narayan Gopal Chowk–Chappal Karkhana–Dhobikhola) | 0.7 km done (government funds); Chappal Karkhana–Dhobikhola 1.2 km to 8 lanes, tendered Jul 2026 | [S] Ekantipur, Meroauto |
| Araniko Highway medians (Koteshwor–Suryabinayak) | `area:highway=traffic_island` grass strips 320–750 m long, area 700–2,080 m² → **median ≈ 2–3 m** wide | [O] |

**Game cross-section (recommended)** [E from the sources above]:

* *Phase-1 section (Kalanki → Balkhu → Ekantakuna → Satdobato → Gwarko → Koteshwor):* footpath 3.0 | service lane 7.0 (2 × 3.5) | separator 1.5 (kerb + planter) | expressway 7.0 (2 × 3.5) | **median 3.0** (concrete with shrubs, gaps only at chowks) | expressway 7.0 | separator 1.5 | service lane 7.0 | footpath 3.0 → **≈ 40 m kerb to kerb + footpaths**. This matches OSM's per-direction `width=6–9` on main carriageways.
* *Northern section today (Koteshwor → Tinkune → Sinamangal → Chabahil → Narayan Gopal Chowk → Basundhara → Samakhushi → Balaju → Swayambhu → Sitapaila):* 4 lanes undivided or with a low 0.6 m divider, `width=14` → 2 × 7.0 m + 1.0 m footpaths; widen to the Phase-2 62 m profile only as a future content update (snapshot rule, CONTENT_COVERAGE "Change over time").

### 5.3 Major chowks (junction nodes measured in OSM) [O]

Arms = number of road arms meeting at the main node. Island = nearest mapped grass/park polygon within 45 m (equivalent diameter from area / mapped max extent). Control: **P** = police (default for the valley), **S** = OSM traffic signals, **R** = mapped roundabout.

| Chowk | Node (lat, lon) | Arms | Roads meeting (OSM) | OSM lanes / width of arms | Island measured | Control (game) | Notes |
|---|---|---|---|---|---|---|---|
| **Kalanki** | 27.69329, 85.28153 | 4 | Ring Road × Tribhuvan Rajpath (to Thankot/Nagdhunga) × Kalanki road | ring 2 lanes/dir, 9 m/dir | none mapped | P (+ underpass) | Western gateway; long-distance buses; heavy jam |
| **Balkhu** | 27.68483, 85.29776 | 4 | Ring Road × University Path (Kirtipur) | 2/dir, 7 m | — | P | Bagmati bridge next to it |
| **Ekantakuna** | 27.66669, 85.30824 | 4 | Ring Road × Jawalakhel–Ekantakuna road | 2/dir, 6 m | — | P | Transport office (four-wheelers) |
| **Satdobato** ("seven roads") | 27.65873, 85.32472 | 4 (+3 nearby) | Ring Road × Satdobato–Godavari Rd × Lagankhel road | 2/dir, 6 m; Godavari arm 12 m | — | P | Chapagaun Dobato 230 m west |
| **Mahalaxmi Chowk** | 27.66134, 85.31822 | 4 | Ring Road × Mahalaxmisthan Rd | 2/dir, 6–10 m | — | P | |
| **Koteshwor** | 27.67873, 85.34950 | 4 | Ring Road × Araniko Highway (to Bhaktapur) | ring 4 lanes 14 m; Araniko 2/dir 9 m | park 36.8 m extent (≈ 23 m Ø) | **S** (signals mapped) + P | Busiest junction; 50 min to clear in peak [S] |
| **Tinkune** | 27.6833, 85.3492 | 3 | Ring Road × Madan Bhandari Path (from Baneshwor) | 2–4 lanes/dir, 9 m | grass 19 m and 15 m; Tinkune park 26,574 m² (184 m eq. Ø) | P | Airport side; large triangular park |
| **Sinamangal** | 27.69527, 85.35501 | 3 | Ring Road × Sinamangal Rd | 2/dir, 7 m | — | P | Airport entrance (TIA) |
| **Chabahil** | 27.71713, 85.34670 | 4 | Ring Road × Boudha Main Road / Chabahil–Gaushala | 2/dir, 12–14 m | Chabahil Park 2,501 m² nearby | P | Chabahil stupa 150 m |
| **Sukedhara** | 27.72780, 85.34572 | 4 | Ring Road × Baraha Marga | 4 lanes, 14 m | — | P | |
| **Narayan Gopal Chowk** (Maharajgunj) | 27.74000, 85.33712 | 4 | Ring Road × Maharajganj Rd × Bansbari Sadak | 4 lanes, 16–22 m | — | P | Police checkpost (OSM) |
| **Basundhara** | 27.7421, 85.33202 | 3 | Ring Road × tertiary | 4 lanes, 14 m | — | P | End of Phase 2 |
| **Samakhushi** | 27.73510, 85.31809 | 4 | Ring Road × Tokha Road | 4 lanes, 14 m | — | P | |
| **Gongabu / Naya Bus Park** | ≈ 27.7350, 85.3110 [E] | 3–4 | Ring Road × Gongabu bus park access | 4 lanes, 14–16 m | bus-park polygon w58754707 | P | Main long-distance bus terminal |
| **Balaju** (Ring × Lekhnath Sadak) | 27.72703, 85.30458 | 4 | Ring Road × Lekhnath Sadak | `width=9` per direction | **R ≈ 29 m**, 7 m island | R + P | Balaju industrial area |
| **Machhapokhari / Balaju Bypass** | 27.73527, 85.30589 | 4 | Ring Road × Balaju–Machhapokhari bypass | 4 lanes, 16 m | — | P | Planned flyover (Phase 2) |
| **Sitapaila** | 27.70767, 85.28253 | 4 | Ring Road × Museum Marg | 4 lanes, 14 m | — | P | |
| **Maitighar Mandala** | 27.6945, 85.3203 | 5 | Ram Shah Path, Singha Durbar wall road, Babarmahal, Thapathali road | 1–2 lanes/dir, 6–9 m | Mandala island (arcs dmax 57 m) | R + P | Protest/assembly square; monument |
| **Thapathali** | 27.6908, 85.3176 [E] | 4 | Thapathali–Patan bridge approach × Tripureshwor–Maitighar | 2/dir | — | P | Bagmati bridge to Kupondole |
| **Tripureshwor** | 27.6939, 85.3140 | 3 | Kanti Path × Tripureshwor chowk | 6–7 m | **R ≈ 32 m**, 13 m island | R + P | Near Dasharath Stadium |
| **Jamal** | 27.70871, 85.31487 | 3 | Kanti Path × Jamal Way | 2 lanes 14 m | grass 15 m | P | |
| **Lainchaur** | 27.71711, 85.31611 | 3 | Lekhnath Sadak × Narayan Gopal Road | 2–4 lanes, 7–14 m | grass 40 m extent (≈ 32 m Ø) | P | Thamel edge |
| **Putalisadak** | 27.70563, 85.32283 | 4 | Putali Sadak × Bagbazar Sadak | 2 lanes, 6–9 m | — | P | |
| **Bagbazar / Ratna Park** | 27.70615, 85.31622 | 4 | Bagbazar × Durbar Marg × Ratna Park Path | 2 lanes, 9–14 m | Ratna Park | P | Old bus park, micro station |
| **New Baneshwor** | 27.68837, 85.33560 | 5 | Naya Baneshwor Sadak × Madan Bhandari Path | 1–2 lanes/dir, 9–16 m | — | P | Parliament (BICC) corner |
| **Bhatbhateni** | 27.71917, 85.33107 | 3 | Bishalnagar Marg × Thirbam Sadak | 2–4 lanes, 7–9 m | park 44 m extent | P | |
| **Jawalakhel** | 27.67270, 85.31350 | 3 + ring | Jawalakhel roundabout | 7 m | **R 43.8 m**, 36 m island | R + P | |
| **Pulchowk** | 27.67714, 85.31630 | 6 | Pulchok–Mangalbazar Rd, Kupondole Rd, Jawalakhel Rd | 3.5–7 m | park 38 m extent | P | Sajha e-bus depot (14 chargers) nearby [S] |
| **Lagankhel** | 27.66708, 85.32264 | 4 | Satdobato–Lagankhel Rd (24 m) | 24 m | bus park w120443290 | P | Patan bus hub |
| **Jorpati** | 27.72174, 85.37266 | 3 | Boudha Main Road × Jorpati road | 3 lanes 14 m | Jorpati Chowk Park 39.6 m extent (≈ 26 m Ø) | P | |
| **Gatthaghar** | 27.67398, 85.37402 | 4 | Araniko Highway × Thimi road | 2/dir, 10 m; median 2–3 m | — | P | Thimi / Madhyapur |
| **Jadibuti** | 27.67529, 85.35278 | 4 | Araniko Highway × primary | 9–14 m | median strip | P | |
| **Sanepa** | 27.68532, 85.30714 | 3 | Sanepa Marg × D. B. Mathema Rd | 7–9 m | — | P | |

### 5.4 Synthesising islands where OSM has none [E]

* If a chowk has ≥ 4 arms, at least one trunk/primary arm, and no mapped island: place a **round island of Ø 8–14 m** (scale with the widest arm: Ø = 0.7 × widest arm width, clamp 6–16 m) with the police podium, a planter or a small shrine/statue base (generic, review).
* Mapped islands override: use the OSM polygon as the island footprint (exact-position rule).
* Junction caps: road ribbons stop at the island edge + 1.0 m; circulating lane width 6.5 m for trunk/primary, 5.0 m for secondary.

---

## 6. Real bus routes in OSM (D2 input) [O]

Nepal-wide `type=route` relations with a bus-like `route=*`: **75** = **55 bus**, **11 microbus**, **9 tempo** (matches CONTENT_COVERAGE S4: 54/11/9 ± one). Of the 55 bus relations, 6 are Pokhara city lines and 1 is Mugling–Pokhara; **≈ 68 serve the valley** (or start from it, e.g. Kathmandu–Mugling). Only ~15 relations carry stop members (max 26 stops, Ratnapark–Dhulikhel). One relation is a Ring Road circuit.

| OSM relation | route | ref | Name | Operator (OSM) | Ways | Stops |
|---|---|---|---|---|---|---|
| r2266660 | bus | RingRoad | Ring Road Circuit | many | 55 | 0 |
| r2301263 | bus | — | Chakrapath Parikrama | — | 89 | 0 |
| r3100600 | bus | — | Kalanki – TIA Airport | Sajha Yatayat | 53 | 0 |
| r3138552 | bus | — | Lagankhel – Naya Buspark (Sajha) | — | 67 | 0 |
| r2988993 | bus | 12 | Ratnapark – Dhulikhel | — | 80 | 26 |
| r2988891 | bus | 11 | Ratnapark – Panauti | — | 83 | 24 |
| r2909799 / r2988890 | bus | 7 | Kamalbinayak ⇄ Ratnapark / Bagbazar | Bhaktapur Minibus Samiti | 66 / 69 | 0 / 2 |
| r2988893 / r2989027 | bus | — | Ratnapark ⇄ Changu | Bhaktapur Minibus Samiti | 77 / 74 | 1 / 0 |
| r2295734 / r2323381 | bus | 5 | Ratna Park ⇄ Budhanilkantha School | — | 26 / 27 | 11 / 11 |
| r2301205 / r2301206 | bus | 5 | Purano Bus Park ⇄ Shivapuri | — | 44 / 38 | 12 / 13 |
| r2989012 | bus | 22 | Ratna Park – Dakshinkali | — | 51 | 0 |
| r2282101 | bus | 2 | Ratna Park – Sundarijal | — | 42 | 0 |
| r2975649 | bus | 1 | Purano Buspark – Chabahil | — | 63 | 0 |
| r3068536 / r3068548 | bus | — | Kalanki ⇄ Lagankhel | — | 22 / 23 | 0 |
| r3070344 | bus | 40 | Kausaltar – Naikap | — | 59 | 0 |
| r2301306 / r3468819 | bus | Nepal Yatayat | Balkumari ⇄ Gopi Krishna | — | 63 / 62 | 0 |
| r2276770 | microbus | 10 | Purano Bus Park – Thimi | — | 46 | 0 |
| r2988835 / r2988836 | microbus | 20 | Ratna Park ⇄ Thankot | — | 62 / 70 | 1 / 0 |
| r3101629 | microbus | — | Lagankhel–Satdobato–Khumaltar–Dhapakhel | Lalitpur Microbus Sewa Samiti | 19 | 4 |
| r3074202 | microbus | — | Kapan–Chabahil–Mitidevi–Ratnapark | — | 27 | 0 |
| r2276999 | tempo | Annapurna A | Shobhabhagwati – Koteshwor | network=safatempo | 54 | 0 |
| r2294107 / r2295768 | tempo | 14 | Ratna Park – Gwarko / Mangal Bazar | — | 59 / 44 | 0 |
| r2295902…r2295942 (4) | tempo | 1 MA | Sundhara – Kattyani Chowk / Purano Baneshwor (both ways) | — | 37–44 | 0 |
| r3071562 | tempo | — | Naya Baneshwor–Koteshwor–Satdobato–Lagankhel–Jawalakhel–Kupondol loop | — | 62 | 0 |

Gaps for D2: operators named on < 15%; many relations are old (r2–r3 million ids, ~2010–2014), so route geometry should be **validated against the current road graph** and stop order inferred from `bus_stop` nodes within 25 m of the route (380 valley stops) [E]. Main terminals: Ratna Park / Purano Bus Park (old bus park), Naya Bus Park Gongabu (w58754707), Lagankhel bus park (w120443290), Kalanki, Koteshwor, Kamalbinayak (Bhaktapur).

Fleet scale for spawn caps: Sajha 111 buses (71 diesel + 40 electric; +100 e-buses budgeted FY 2082/83) [S]; Safa tempo ≈ 700 vehicles on ~11–17 routes carrying ~100,000 passengers/day [S CEN, Himal]; micros: 200+ Hiace micros replaced the diesel Vikram tempos in the early 2000s [S Nepali Times] (current count verify).

---

## 7. Audio notes (licence-safe)

| Sound | Approach | Parameters [E] |
|---|---|---|
| Scooter / motorbike engine | Procedural: pulse train at firing frequency f = rpm/60 × (cylinders/2) for 4-stroke singles, 1,500–9,000 rpm → 12–75 Hz fundamental + harmonics, band-pass 80–2,000 Hz, noise exhaust layer; gear-shift dips | idle 1,400 rpm; scooter CVT smooth ramp, motorbike 5 gear steps |
| Cars | Same engine model, 4-cyl (f = rpm/30), softer LPF 1.2 kHz | |
| Buses / trucks | Diesel: 6-cyl, 600–2,600 rpm, heavy LPF, air-brake hiss (white noise burst 0.4 s, HPF 2 kHz), reversing beeper absent (not common) | |
| Safa tempo / EVs | Electric whine: sine sweep 600–2,400 Hz ∝ speed, −18 dB, plus tyre noise | quiet; good contrast |
| Bicycle | freewheel ratchet (clicks at 25–40 Hz ∝ speed), bell 2 rings (CC0 or synthesised: 2.5 kHz + 6.8 kHz partials, 1 s decay) | |
| Horns | §4.4 | |
| Conductor calls | Recorded voice only with consent / CC0; else a generic vocal "pom" synth; never real route names | |
| Crowd/traffic bed | CC0 city ambience (Freesound CC0 filter) or granular synthesis of the above | |

---

## 8. Hand-off checklist for implementation

1. Add `ghm_veh_escooter_a`, `ghm_veh_ambulance_a`, `ghm_veh_bus_school_a`, `ghm_veh_police_jeep_a` and Sajha / private-minibus livery presets to ASSET_MANIFEST §8 (owner of that file decides).
2. Plate atlas: legacy colour code (§3.1) with fictional numbers; embossed variant optional after photo check.
3. Lane graph: left-hand offsets, clockwise roundabouts, controller types POLICE / SIGNAL / ROUNDABOUT / PRIORITY from §5.
4. Ring Road cross-section profiles §5.2, applied by OSM width/lanes tags where present (trunk `width` coverage 70%, `lanes` 80% in the valley [O]).
5. Spawn tables §2.2, speeds §4.2, time-of-day §4.5, honk model §4.4.
6. D2 routes: import §6 relations, validate geometry, infer stops.

### Road-tag coverage in the valley pack area [O]

| Class | km | % with `width` | % with `lanes` | Common lanes (km) | Common maxspeed (ways) |
|---|---|---|---|---|---|
| trunk | 118 | 70.1 | 79.5 | 2: 71.2, 4: 22.6 | 40, 80, 60 |
| primary | 162 | 73.9 | 54.3 | 2: 67.8, 4: 16.8 | 30, 50 |
| secondary | 407 | 59.4 | 15.3 | 2: 52.7, 4: 6.5 | 50, 30 |
| tertiary | 488 | 43.7 | 9.0 | 2: 33.4, 1: 10.1 | 20, 40 |
| unclassified | 2,060 | 12.4 | 1.4 | — | 20 |
| residential | 3,078 | 10.8 | 2.5 | — | 20 |

Class-default widths where untagged [E, from tagged medians above]: trunk 14 m (two-way) or 7–9 m per direction, primary 9–14 m, secondary 7–9 m, tertiary 6–7 m, unclassified 5 m, residential 4 m, old-core lanes 2.5–4 m.

---

## 9. Sources

* Vehicle registration plates of Nepal (colours, class letters, sizes, LHT): https://en.wikipedia.org/wiki/Vehicle_registration_plates_of_Nepal
* Embossed plates mandatory again, Bagmati: https://www.en.meroauto.com/bagmati-makes-embossed-number-plates-mandatory-again/
* Government vehicles with red plates (white→red history): https://english.ratopati.com/story/70655/red-plates-on-government-vehicles-security-threat-or-misuse
* Green plates for EVs (proposal, not adopted): https://www.nepaldrives.com/why-nepal-needs-green-number-plates-for-electric-vehicles
* Bagmati fleet 2.3 M, 1.58 M two-wheelers: https://www.en.meroauto.com/digitization-of-vehicle-records-underway-in-kathmandu-valley/
* Mode share 2011, trend, fleet vs trips (CEN / MoPIT-JICA): https://www.cen.org.np/uploads/doc/poster-urban-mobility-kathmandu-60b9fc8544142.pdf ; https://cen.org.np/uploads/doc/public-transportation-in-kv-maya-factsheet-4-60b9e4406ee31.pdf ; https://www.cen.org.np/uploads/doc/cycling-in-kathmandu-valley-maya-factsheet-3-60b9f58fe143e.pdf
* Scooter import shares 2025: https://www.en.meroauto.com/honda-dominates-nepals-scooter-market-with-44-market-share/ ; motorcycle imports 2025: https://www.en.meroauto.com/over-100000-motorcycles-imported-in-seven-months/
* Hand signals and whistles, 2 million vehicles: https://www.en.meroauto.com/kathmandu-valleys-2-million-vehicles-still-directed-by-hand-signals-not-tech/ ; https://english.ratopati.com/story/74315/traffic-management-of-over-two-million-vehicles-has-been-done-with-hand-signals-and-whistles ; signals out of order except Koteshwor: https://myrepublica.nagariknetwork.com/news/kathmandus-traffic-policing-goes-manual
* 50 km/h limit: https://english.onlinekhabar.com/50-kmph-speed-limit-kathmandu.html ; peaks: https://arroshan.odoo.com/blog/our-blog-1/kathmandu-ring-road-traffic-jam-explained-problems-and-fixes-17
* No-horn rule: https://kathmandupost.com/valley/2017/04/15/no-horn-regulation-comes-into-effect ; https://english.onlinekhabar.com/traffic-police-tighten-no-horn-rule-in-kathmandu.html
* Ring Road Phase 1 handover/specs: https://kathmandupost.com/valley/2019/01/29/china-formally-hands-over-kalanki-koteshwor-project ; https://kathmandupost.com/valley/2019/01/30/bigger-better-can-it-be-safer-too ; Kalanki underpass: https://kathmandupost.com/valley/2018/08/23/traffic-jams-resume-as-kalanki-subway-closes
* Ring Road Phase 2: https://kathmandupost.com/national/2026/06/28/ring-road-widening-pushed-back-again-work-now-after-dashain ; https://en.himalpress.com/rs-11-billion-chinese-grant-secured-for-second-phase-of-ring-road-expansion/ ; https://china.aiddata.org/projects/107015 ; Chappal Karkhana–Dhobikhola: https://ekantipur.com/news/2026/07/26/en/contracts-invited-for-the-slipper-factory-dhobikhola-section-of-the-ring-road-work-to-begin-before-dashain-41-47.html
* Sajha green buses: https://kathmandupost.com/valley/2026/07/08/sajha-yatayat-dismisses-rumours-of-repainting-buses-blue-for-women-only-service ; e-bus fleet and Pulchowk charging: https://sajhayatayat.com.np/sajha-yatayat-electric-buses-a-new-era-of-smart-public-transport/ ; 36 seats/200 km: https://english.onlinekhabar.com/sajha-yatayat-new-electric-buses.html
* Safa tempo: https://cen.org.np/uploads/doc/ev-fact-sheet-60bc4644dabb5.pdf ; https://www.himalmag.com/round-up-of-regional-news-61/ ; history: https://adayinnepal.com/?p=122
* Microbuses (Hiace, 14–15 seats): https://archive.nepalitimes.com/news.php?id=11077 ; https://nepalitimes.com/macro-traffic-jam
* Taxis (white or yellow, black plates, roof sign): https://www.inyourpocket.com/nepal/kathmandu/articles/getting-around-kathmandu/ ; https://www.worldtravelguide.net/guides/asia/nepal/kathmandu/gettingaround
* Truck art "Horn Please": https://english.onlinekhabar.com/tattooed-trucks-of-nepal-horn-please.html ; https://nepalitimes.com/review/art-and-poetry-in-motion
* Yellow school buses: https://www.educatenepal.com/news/detail/mtpd-enforces-new-regulation-on-school-buses ; https://www.educatenepal.com/news/detail/school-buses-yet-to-don-yellow-paint
* Red Cross emblem protection (do not use on ambulances in game): https://www.icrc.org/eng/resources/documents/news-release/2009-and-earlier/5rpetz.htm
* OSM data © OpenStreetMap contributors, ODbL (measurements in §§1, 4.2, 5, 6, 8 from the repo's Nepal extract).
