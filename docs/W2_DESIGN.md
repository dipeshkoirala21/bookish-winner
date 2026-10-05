# W2 design: "Kathmandu comes alive"

> Status: **buildable design, 2026-10-05.** It turns the eight W2 research files into one plan that five tracks can build in parallel. Inputs: [research/w2/newar_houses.md](research/w2/newar_houses.md) (H), [temples.md](research/w2/temples.md) (T), [street_life.md](research/w2/street_life.md) (S), [vehicles_traffic.md](research/w2/vehicles_traffic.md) (V), [roads.md](research/w2/roads.md) (R), [player_and_controls.md](research/w2/player_and_controls.md) (P), [audio.md](research/w2/audio.md) (A), [aviation.md](research/w2/aviation.md) (AV). Section references such as "H §6" point into those files, which carry the source URLs. The contracts this design depends on are [ARCHITECTURE](ARCHITECTURE.md) §7 and §10, [DATA_FORMATS](DATA_FORMATS.md), [CONTENT_COVERAGE](CONTENT_COVERAGE.md) §3, [ASSET_MANIFEST](ASSET_MANIFEST.md) §1.4–1.5 and [M1_PLAN](M1_PLAN.md).
>
> **Review pass (2026-10-05):** added Asan and the Newar-town acceptance rows and heroes 42–52, the Himalayan-skyline check G8, the sacred LOD rules and hero LOD allocator (§3.2), parked-vehicle, animal, bird and aircraft LODs, a rebalanced per-tier triangle split (§10.4), the vehicle catalogue, sacred-zone, area-type and footstep-surface contracts (§10.3), plate letter rules, a generic city-bus livery, and corrected vehicle shares and vegetation caps.
>
> **Evidence labels** (same scheme as the research): **[S]** cited source, **[O]** measured in OSM or the pack, **[E]** estimate or design value (tune in play), **[V]** needs a human check before ship. A number without a label is a design decision made here.
>
> **Hard rules carried in:** 9+ / PEGI 7 and non-violent: no damage, theft, weapons, injury, ragdolls or chases. Everything is generated in code from parameters, with no hand modelling. Only CC0 or public-domain recordings, or procedural synthesis, may ship as sound. Reference photos are for reference only. No real brands, liveries or operator names appear. Real objects stay at their real positions (CONTENT_COVERAGE §1.1). Procedural dressing never creates temples, shrines, chautari or settlements (O2).

---

## 0. Owner decisions recorded by this design

| # | Decision (2026-10-05) | Supersedes | What stays |
|---|---|---|---|
| W2-O1 | **Every temple compound can be entered on foot**, including Pashupatinath's main courtyard, the Taleju compounds (Kathmandu, Patan, Bhaktapur Mul Chowk), bahals and bahis, palace chowks and stupa terraces. | CONTENT_COVERAGE **O10** ("Pashupati's main compound is closed to every player") and the temples.md §7 "escalate" row; ASSET_MANIFEST `ghm_lmk_pashupatinath` "player cannot enter" | **Sanctums (garbhagriha) are never entered, modelled or lit inside.** A sanctum door shows a dark interior and a lamp glow. No vehicles in compounds, squares or stupa koras (L16 `SACRED_NO_VEHICLE`). Clockwise kora (anticlockwise only at Bon sites). The Kumari is never shown. No sacrifice, cremation, bodies or pyres. Calm mode inside compounds (§6.6). A per-site `entry_rule` from the curated DB may show a **non-blocking** info card ("Shoes off here", "Hindu worship site: please be quiet") [V cultural reviewer]. The reviewer cannot re-block a compound without going back to the owner. |
| W2-O2 | **Bicycle is a W2 player vehicle** (walk, cycle, ride, drive car, bus, truck). | CONTENT_COVERAGE **O14** (bicycle in M2) | MTB trail tags still arrive with D2/M2 |
| W2-O3 | **Roads are wider than reality** wherever the real space between buildings allows (§4.2). | ARCHITECTURE §7.4 "width from the tag or a class default" | Road centrelines and every building stay at their exact positions. A road never cuts a building. Narrow old-core lanes stay narrow, and their width decides who may enter (§4.4). |
| W2-O4 | Cartoon replicas of the major temples are **exact in plan, tier count, height (±3%), orientation and deity**, and cartoon only in proportions inside that envelope (§3.1). | ASSET_MANIFEST §5 "hand-made hero art" for the W2 slice | Hand-art heroes can still replace a generated replica later; the anchors and hide zones stay the same |
| W2-O5 | Vehicle access without theft: own garage, plus a **community fleet** of parked vehicles with a green key tag that the player can borrow and that return on their own; moving NPC vehicles can only be ridden **as a passenger** (P §6.5). | — | Nobody is ever pulled out of a vehicle |
| W2-O6 | **Singha Durbar** is shown intact, with light scaffolding on the west wing (H §8). | — | It is never shown with fire or damage |

---

## 1. Goals and acceptance

### 1.1 Goal

When the owner lands in the valley, the cartoon city reads as Kathmandu within 10 seconds: brick Newar rows with carved windows in the old cores, pastel concrete with water tanks outside them, real temples at their real places with the right tiers and the right faces, scooters filtering through police-controlled chowks, buses on real routes, cows lying on the Ring Road, pigeons bursting off Basantapur, monkeys on Swayambhu, an ATR sliding down over Koteshwor, and the right sound under every footstep. The player can walk, cycle, ride and drive anything with wheels, and enter every temple compound.

### 1.2 Acceptance by place

Each row is a scripted camera stop for the verification run (§10.6). It is checked at 11:00 and 18:00 game time, in October, on Mid and on Low, in portrait and in landscape. "Visible" means on screen at the stated camera rig.

| Place (anchor lat, lon) | What the owner sees (pass criteria) |
|---|---|
| **Thamel** (Thamel Chowk 27.7153, 85.3108; TOURIST overlay r = 450 m, S §1.3) | Buildings use the `thamel` profile: median 5 storeys [O 23%], 80% MODERN, 15% HYBRID, 5% NEWAR (H §6). **Ground floors are shops on ≥ 85% of frontages**, with 3–8 generic signboards per frontage and vertical blade signs on the upper floors. Rooftop terraces with umbrellas sit on 30–40% of buildings. Prayer-flag strings cross a lane every 20–40 m, in the fixed order blue, white, red, green, yellow. Wire bundles cross every 15–25 m (10–40 cables). Street game widths stay inside the real 5.8 / 8.8 / 15.0 m corridor p25/p50/p75 (R §6). Traffic is about 70% two-wheelers. Cars run one way only on 3.5–4.5 m lanes. At least 15 pedestrians are visible within 60 m, about 45% of them tourists (S §3.2). The Garden of Dreams walls and pavilions are at w85650618. Kathesimbhu stupa (n3377725834) is a medium parametric stupa. |
| **Asan and Indra Chowk** (Annapurna Ajima n3569849497, 27.7074, 85.3122; the G1 densest tile 10/516/161) | Six lanes meet at Asan Tol around the **Annapurna (Asan Ajima) temple: 3 gilt-copper tiers**, gilt finial and lions at the door [S Nepali Times], with Ason Ganesh (n2088657256) beside it. Grain, spice and vegetable vendors on mats fill the chowk (OLD_CORE bazaar density, §5.4). The lane south-west to Indra Chowk passes **Jana Bahal** (Seto Machhindranath, w501427078, gilt pagoda in a courtyard full of chaityas) and ends at the **Akash Bhairab** house-temple (w136526480). No car can enter (lanes < 3.5 m game width, §4.4); scooters filter through at walking pace. |
| **Basantapur / Kathmandu Durbar Square** (r21291455, 27.7043, 85.3073) | The square is walk-and-cycle only, with no motor vehicles (heritage zone). The heroes from §3.6 stand at their OSM anchors and yaws: **Taleju** (12-stage plinth ≈ 9 m, 3 gilt tiers, 35 m [S]), **Kasthamandap** (3-tier open mandapa, 21.8 × 21.4 m, ≈ 20 m), **Maju Dega** (9-stage plinth, 3 tiers, faces east), **Trailokya Mohan** with a kneeling Garuda facing it, Jagannath, Shiva-Parvati with the two figures in the upper window, the Kal Bhairav relief (open air, no roof), the nine-storey Basantapur tower, the Hanuman Dhoka gate and the white neoclassical front of Gaddi Baithak. **Kumari Ghar's courtyard can be entered; the Kumari is never shown and the windows stay closed.** A pigeon flock of 30–200 birds bursts off when the player runs within 3–4 m (most birds draw as 4-triangle paper birds, §5.6). People sit on the Maju Dega steps. Dharahara (72 m, white, cone top) shows over the roofs 650 m to the south-east. |
| **Patan** (r4557971, 27.6732, 85.3248) | **Krishna Mandir**: stone shikhara, 3 storeys, **21 pinnacles**, faces east toward the palace. **Taleju** (34 m, 3 tiers on the palace block), Vishwanath (2 tiers, stone elephants at the east door), Bhimsen (gilt balcony), the King Yoganarendra Malla column, the Taleju bell and Manga Hiti (3 makara spouts in a 3 m pit). Off the square: the **Golden Temple** courtyard (r4624856, 3 gilt tiers on the shrine range) and **Kumbheshwar** (5 tiers). Bahal entrances with paired stone lions appear every 60–120 m along the main lanes [E]. Lanes are 5 m (residential median [O]). Patan houses use red `brick.ptn` `#BF5236`. |
| **Bhaktapur** (Taumadhi 27.6714, 85.4294; Durbar Square w1192827651) | **Nyatapola**: 5 tiers on 5 plinths, 33.2 m, stair facing south, with the five guardian pairs bottom to top: wrestlers, elephants, lions, griffins, goddesses. Bhairavnath (3-tier rectangular pagoda) faces west onto the square. The 55-Window Palace, the Golden Gate (gilt torana in a red gatehouse set in white walls), Vatsala Durga (stone shikhara) and Dattatreya (3 tiers) at the east end of Tachupal. The peacock window is a decal on the Pujari Math lane wall. Herringbone brick paving `#A4553A`. **No AI cars inside the core**: two-wheelers, bicycles and porters only. Houses are the narrowest in the valley (4.9 m median frontage [O]), with 35° roofs and 1.2 m eaves. Pots dry in rows on Pottery Square. |
| **Newar towns** (Kirtipur 27.6786, 85.2775; Thimi 27.6760, 85.3853; Bungamati 27.6297, 85.3021; Khokana 27.6358, 85.2981) | Each town uses its own profile (§2.2) and brick token, and shows its own temple at its real place: **Kirtipur** Bagh Bhairab (3 storeys, gilt top roof), Uma Maheshwar on the hilltop (**3 roofs**: a fourth was lost in 1934 [S]) and the Chilancho stupa (w1074769018); **Thimi** Balkumari (**3 tiers, gilt copper, faces north** [S]) with pots and kilns; **Bungamati** the Rato Machhindranath shikhara in its bahal square with carving workshops; **Khokana** the Rudrayani temple (**3 storeys**, Chwe Lachi square [S]) with mustard oil mills and mustard drying in the street. These are S2 heroes (§3.4); in S1 the town profiles alone must read as distinct from the Kathmandu core. |
| **Boudhanath** (stupa w56688295, 27.7214, 85.3620) | The stupa stands on the stupa way, not on the kora wall (D5 fix). Three 20-cornered terraces of 81.5 / 62.5 / 50.2 m with tops at 4 / 8 / 10.5 m, dome Ø 33.5 m to 21 m, harmika 21–25 m with eyes on four faces (nose drawn as "१", urna dot), 13 gilt rings to 33 m, parasol to 34.5 m, finial to **36 m** [S]. The main gate faces south (n5302729521). 60–120 prayer-flag strings fan out from the spire. Prayer wheels in niches run at a 0.5 m pitch. **NPCs walk the kora clockwise**: 60–80% of people on the ring at 05:30–08:00, about 22% of them monks. Pigeons gather on the plaza. Terrace 1 is walkable. No vehicles inside the kora wall w56688296. |
| **Swayambhunath** (w201223707, 27.7149, 85.2904) | Dome Ø 26.7 m, about 33 m to the finial, five Dhyani Buddha niches set into the dome. The **eastern stairway of 365 steps** climbs to a giant gilt vajra; the rise per step is computed from the terrain between the stair foot and the platform (≈ 0.19 m and ≈ 70 m in all [E]), never fixed, so the stair always lands on the real hilltop. The **Anantapur and Pratappur shikharas** (17 m, white) stand south-east and north-east of the stupa, and the Harati pagoda stands 13 m north and 18 m west of it. **40–80 macaques** are simulated within 200 m, and at least 6 / 15 / 30 (Low / Mid / High) are rendered at once, nearest first (§5.5 LOD rule); they are never aggressive and never fed by the player. Departing jets bank overhead (R-310 D4 lies over the hill, AV §8). |
| **Ring Road** (south: Kalanki–Koteshwor; north: Balaju–Chabahil) | **South half**: a dual carriageway meshed as one road (§4.6): footpath 3.0, service lane pair 6.5, separator 2.8, main pair 6.5, median ≈ 1.0 m, mirrored on the other side. That is about 32.6 m kerb to kerb (38.6 m with both footpaths), 8 motor lanes in all [O/S]. These are **real** widths; the game widths follow §4.2–4.3 (each 6.5 m carriageway becomes ≥ 7.0 m, up to 8.1 m where the corridor allows). **North half**: one undivided 4-lane way, 14 m real (17.5 m game). Chowks are police-controlled (an officer on a podium with an umbrella) at Kalanki, Balkhu, Satdobato, Chabahil and the others in §4.7. **Koteshwor** has OSM signals plus police, and **Balaju** has a roundabout (Ø 29 m). Traffic flows on the left and roundabouts turn clockwise. The mix is 55% two-wheelers, 22% cars, 11% buses and micros, 8% trucks. Targets are 45 km/h free flow and 15 km/h at peak. 1–3 cows per km lie on the road, and every vehicle steers round them. Green city buses and white micros stop at real stops. |
| **TIA** (aerodrome w118505122; runway w340948564) | A 3,326 m × 45 m runway on true heading 022°/202°, flattened to its real thresholds (1,315 m / 1,337 m), with markings, edge lights and 870 m of approach lights reaching into Koteshwor. The international (10.7 ha) and domestic (3.0 ha) aprons hold parked generic aircraft. **From 06:00 to 24:00 a movement every 1.5–4 real minutes** (AV §4.4). Arrivals descend over Thecho at 250–300 m and over Koteshwor at 50–100 m. Departures loop over Boudha, Maharajgunj and Swayambhu. Helicopters lift off the domestic apron. ATR turboprops buzz at 98 Hz and jets roar on take-off, audible 5–9 km away. Navigation and beacon lights show at night. Airside is fenced and not drivable. |

### 1.3 Global acceptance

| # | Check | Pass |
|---|---|---|
| G1 | Frame time on the densest tile (10/516/161, Asan) with traffic, crowds and audio | Low 30 fps (≤ 33 ms, main thread ≤ 22 ms); Mid 30 fps (≤ 14 ms main); High 60 fps (≤ 9 ms main). See §10.4 for the triangle and batch budgets per tier. |
| G2 | Peak memory in a 20-minute soak (Ring Road loop plus the old core) | Low ≤ 1.0 GB, Mid ≤ 1.5 GB, High ≤ 1.4 GB on a 4 GB iPhone (ARCHITECTURE §10) |
| G3 | Vehicles the player can drive | Bicycle, scooter, motorbike, taxi, hatchback, SUV, microbus, Safa tempo, minibus, city bus, truck, tanker, tractor: 13 classes, each enterable and exitable from both sides where the class allows |
| G4 | Sacred rules | 0 motor-vehicle frames inside `SACRED_NO_VEHICLE`, with a test that fuzzes 1,000 spawns; NPC kora at Boudha and Swayambhu is 100% clockwise; no sanctum interior geometry exists |
| G5 | Non-violence | 0 frames where a vehicle collider overlaps a cow collider (fuzz test); pedestrians are never knocked down (no ragdoll code path exists) |
| G6 | Audio | Audio DSP < 10% of one core on the Low reference phone; 0 dropouts in 20 min; resident audio ≤ 25 / 40 / 50 MB; every shipped recording has a CC0 or PD row in LICENSES.md |
| G7 | Real positions | No unhidden building intersects a hero footprint (CONTENT_COVERAGE §5.3 #2); no road ribbon overlaps a building footprint by > 0.1 m (new check, §4.2) |
| G8 | Himalayan skyline | On a clear October morning (no fog, 08:00–11:00) the D9 skyline ring shows Langtang Lirung, Ganesh Himal and Dorje Lakpa at their true bearings north of the valley from the Swayambhu platform, the Dharahara balcony and Basantapur rooftops (CONTENT_COVERAGE N10; ROADMAP 1.9) |

---

## 2. Regional building styles

### 2.1 Style profiles and how a building picks one

A building gets one **style profile** (a new enum, below) in the pipeline (E), stored per building in `BFNT` (§9.3). The runtime maps the profile to grammar parameters. The profile is chosen by the **first match** in this order:

1. **Curated historic-core polygon** (`pipeline/config/style_zones.yaml`, new, owned by E). Stage 1 seeds it from the existing `archetype_zones.yaml` `newar_core` rectangles (Kathmandu, Patan, Bhaktapur, Kirtipur, Thimi, Bungamati, Khokana, Sankhu, Panauti), plus the Thamel rectangle 85.306–85.316 E, 27.712–27.719 N (H App. A) and the Boudha kora circle (85.3620, 27.7215, r = 350 m; S §1.3). Stage 2 replaces the rectangles with polygons traced on the real historic cores (D4 "Newar zones traced from real historic cores") [V traced by a reviewer].
2. **Other Newar towns** without a traced polygon: a 250 m circle (villages) or 400 m circle (towns) around the OSM `place=*` node of Bode, Sankhu, Chapagaun, Pharping, Thecho, Sunakothi, Harisiddhi, Lubhu, Siddhipur, Tokha, Banepa and Dhulikhel. They get the `kirtipur` profile, or `khokana` for villages under 1,500 buildings (H §3.2).
3. **Municipality polygon** (OSM `boundary=administrative`, `admin_level=7`), **matched by relation id, never by name**, because OSM names are wrong on some (r10535035 "Mahalaxmi" carries Lalitpur's Devanagari name, CONTENT_COVERAGE P6) [O]:

| Municipality | OSM relation [O] | Profile outside its cores |
|---|---|---|
| Kathmandu Metropolitan City | r12394677 | `metro` |
| Lalitpur Metropolitan City | r10535034 | `metro` |
| Bhaktapur Municipality | r7715519 | `metro` (Bhaktapur's core comes from rule 1) |
| Madhyapur Thimi Municipality | r7715169 | `metro` |
| Kirtipur Municipality | r12394676 | `metro` |
| Tokha r12394680, Budhanilkantha r12394681, Gokarneshwar r12394682, Tarakeshwar r12394679, Nagarjun r12394678, Chandragiri r12394675, Dakshinkali r12394674, Godawari r10535033, Mahalaxmi r10535035 / r12379739, Changunarayan r10534891, Suryabinayak r10534890, Shankharapur r12394684, Konjyosom r10535032, Bagmati r10535030 / r10537759 / r10532308, Kageshwari-Manohara (id to resolve [V]) | as listed | `metro` in URBAN cells, else `rim` |

4. **Area-type grid** (S §1.1, 250 m cells): URBAN → `metro`; PERI_URBAN → `rim`; RURAL, FOREST and HILL → `rim` (with HILL_VILLAGE beyond the built-up edge, as today).

```text
enum StyleProfile : byte { None=0, KathmanduCore=1, Thamel=2, Patan=3, Bhaktapur=4, Kirtipur=5, Thimi=6,
                           Bungamati=7, Khokana=8, Panauti=9, Metro=10, Rim=11, BoudhaKora=12 }   // append-only (F1)
```

`BoudhaKora` uses the `metro` mix with gompa-style palette accents (white `#F3EFE6`, maroon band `#7A1F1F`) on 30% of ring-facing facades, and prayer flags allowed (a Buddhist site, S §5.5).

### 2.2 Archetype mix per profile

A profile gives **seeded priors**. Real OSM tags win after the D4 plausibility gate. Rules for the archetype choice (H §10): `building:structure` containing `reinforced_concrete` → HYBRID or MODERN; `load_bearing_brick_wall_in_mud_mortar` → NEWAR; `roof:shape=flat` → HYBRID or MODERN; `gabled` or `hipped` → NEWAR. A shop POI inside the footprint forces a shop ground floor (8,712 valley shops lie inside footprints [O]).

| Profile | Storeys 2/3/4/5/6+ (%) | NEWAR / NEWAR_HYBRID / MODERN (%) | Shop ground floor without a POI (%) | Tile roof share of NEWAR + HYBRID (%) | Brick token | Distinctive dressing |
|---|---|---|---|---|---|---|
| KathmanduCore | 6 / 13 / 18 / 34 / 29 [O] | 20 / 35 / 45 | 70 on bazaar lanes, 40 elsewhere | 25 | `brick.ktm` `#C25A3C` | Black or brown painted windows on 30%; glazed-tile fronts on 10–15%; dense wires |
| Thamel | 10 / 17 / 25 / 28 / 20 | 5 / 15 / 80 | 85 | 10 | — | Signs, rooftop terraces, prayer-flag strings (§2.6) |
| Patan | 22 / 29 / 25 / 17 / 7 [O] | 35 / 40 / 25 | 30 on main lanes, 10 elsewhere | 45 | `brick.ptn` `#BF5236` | Bahal gates with lions; metal-shop fronts with hanging copper and brass |
| Bhaktapur | 2 / 32 / 51 / 13 / 2 [S, read from the Yamamoto et al. figure] | 60 / 30 / 10 | 25 on squares and main lanes, 10 elsewhere | 70 | `brick.bkt` `#B4432F` | 1.2 m eaves; carved timber floor bands; pottery near the pottery squares |
| Kirtipur (and other Newar towns) | 20 / 30 / 30 / 18 / 2 | 40 / 35 / 25 | 20 | 50 | `brick.ktp` `#B85A40` | Stone plinths; stepped lanes |
| Thimi | 20 / 30 / 30 / 18 / 2 | 45 / 30 / 25 | 20 | 50 | `brick.thm` `#C9663F` | Pots and kilns |
| Bungamati | 10 / 30 / 35 / 20 / 5 | 35 / 40 / 25 | 15 (carving workshops) | 45 | `brick.vil` `#B86A4A` | Mud-plaster patches `#C9A27A`; carving props |
| Khokana | 15 / 40 / 30 / 13 / 2 | 50 / 35 / 15 | 10 (oil mills) | 55 | `brick.vil` | Mustard drying on the main street (Oct–Jan) |
| Panauti | 30 / 45 / 20 / 5 / 0 | 60 / 25 / 15 | 15 | 70 | `brick.pnt` `#BC553B` | Two-storey lanes |
| Metro | 18 / 38 / 18 / 7 / 5 [O]; 1-storey tags go to sheds | 0 / 0 / 100, plus 5% RANA-style houses where `start_date` < 1960 | 80 on primary and trunk frontages, 20 on side streets | — | — | Pastel paint (§2.5), tanks, rebar stubs |
| Rim | 18 / 23 / 11 / 3 / 1 [O]; 44% of buildings are 1 storey | 0 / 0 / 100; HILL_VILLAGE beyond the built-up edge | 10 | — | — | CGI roofs on 40% (tin is 40% of tagged roof material [O]) |

Height sanity: inside the Bhaktapur, Kirtipur and Panauti profiles, an untagged building never exceeds 6 storeys; a tag above that is flagged TAG_SUSPECT (H §10).

### 2.3 Facade and roof grammar

The grammar is engine-free (Track A, `Core/Meshing/Buildings/`). It takes a footprint, the `BFNT` front edge, the profile, the archetype and the seed, and emits geometry for the LOD band it is asked for (§2.4). Floors are counted G, 1, 2, 3 (ground = 0).

**Plot split** (H §10.2). Footprints whose front edge is longer than 9 m are split into plots of 4–8 m (seeded, uniform) in the KathmanduCore, Patan, Thamel and Kirtipur profiles. Each plot gets its own storey-height jitter (±0.1 m), eave step (0.2–0.6 m between groups of 2–4 plots), palette draw and archetype draw. Bhaktapur and Khokana are already mapped house by house (median footprint 35–38 m² [O]) and are not split.

**Storey stacks (floor-to-floor, metres)**

| Archetype | G | 1 | 2 | 3 | 4+ | Plinth | Source |
|---|---|---|---|---|---|---|---|
| NEWAR | 2.40 | 2.25 | 2.25 | 2.10 (attic, to the eave plate) | — | 0.15–0.45 + a front apron (*pikhā*) 0.3–0.45 high × 0.6 deep | H §2.1 [S/E] |
| NEWAR_HYBRID (new archetype, value 21) | 2.40 | 2.40 | 2.40 | 2.80 | 2.80 | as NEWAR | H §4 |
| MODERN_URBAN | 3.0 (3.2 when a shop) | 3.0 | 3.0 | 3.0 | 3.0 | 0.3 | H §5 |
| RANA_PALACE (value 20, F1) | 4.5 (rusticated) | 5.0 (piano nobile) | 4.0 | 4.0 | — | 0.6–1.2 | H §8 [E] |

The W1 constant `BuildingStyle.LevelHeightM = 3f` becomes `BuildingStyle.StoreyHeightM(archetype, floor)`.

**Bays.** `n = clamp(round(frontage / 1.6 m), 1, 7)`. When frontage ≥ 4.5 m, n is forced odd so the facade has a centre bay. The facade is mirrored about the centre bay on floors 1 and 2, and on G when G has a house door. A shop breaks the symmetry on G.

**NEWAR elements (sizes in metres)** (H §2.2–2.3)

| Floor | Element | Size | Rule |
|---|---|---|---|
| G | House door | 0.75–0.90 W × **1.45–1.70 H** (low on purpose); frame 0.12; lintel and sill "ears" extend 0.25–0.35 past each jamb | Centre bay; a deity panel above the door; vermilion `#E23B2A` on the frame |
| G | Shop *dalan* | 2–4 timber posts 0.12 square, plank shutters, or a roller shutter 2.4–2.7 high (shutter palette §2.5) | When `BFNT.shop_bays` > 0 or the profile's draw says shop |
| G/1, 1/2 | Floor band | 0.20–0.30 high, projecting ≤ 0.23 | Brick cornice; a carved timber band in Bhaktapur |
| 1 | *Tikijhya* (lattice window) | 0.60–0.80 × 0.80–1.00, sill 0.35–0.45, outer frame +0.15 per side, lintel ears 0.20 | One per bay. The lattice is an opaque inset (no alpha) |
| 2 | *Sanjhya* (projecting bay window) | 2.4–3.6 × 1.1–1.4, projecting 0.30–0.60 on brackets; a full-width "long sanjhya" (frontage − 0.6) on 15% of houses wider than 7 m | Centre bay when frontage ≥ 4.5 m, else a single-unit projecting window. **Never dropped by the triangle cap.** Marigold pot on 10% of sills |
| 3 | *Gajhya* (under-roof window) | 0.9–1.4 × 0.6–0.8, projecting 0.3–0.5 | Centre, under the eave; 20–30% of houses get a *kausi* roof terrace (2–3 m wide, 0.9 m parapet) instead |
| Roof | Gable, ridge parallel to the front edge | Pitch **32° ± 3°** (Bhaktapur 35°); overhang 1.0 m (Bhaktapur 1.2); eave fascia 0.18 | Jhingati tile mix (§2.5). Gable ends only show at the end of a row or at a gap; party walls hide them |
| Roof | Struts (*tundal*) | One per 1.2–1.8 m along the eave; 1.0–1.4 long at 35–45° | Plain or lightly carved on houses; **never figurative** on houses |
| — | Wall reveals | 0.45 on G and 1, 0.35 above | Shown only at the openings |

**Openings ratio:** 15–25% of the facade area on floors 1–2, 30–60% on a shop G.

**NEWAR_HYBRID** (H §4): floors G–2 use the NEWAR grammar at 2.4 m storeys. Floors 3–5 use brick veneer (heritage profiles) or plaster paint (§2.5), with plain 0.8 × 1.4 m windows. A single-slope eave hood (jhingati or CGI), 0.6–0.9 m deep at 25–30° on 2–4 struts, sits at the old eave line or on the top floor. On 20% of houses a seam shows: a change in brick tone or a plaster line. The roof is a flat terrace with a 0.9 m parapet, a 2.4 m stair cabin (*mumty*) and props (§2.6). A steel-railed balcony on the top floor appears on 30%.

**MODERN_URBAN** (H §5): columns 0.23–0.30 at 3–4.5 m; the slab edge shows as a 0.15 m band. Two or three windows per storey on a 7–8 m front (1.2 × 1.35 m), each with a concrete *chhajja* sunshade 0.30–0.45 deep. A centre bay of balconies (0.9–1.2 deep, steel railing) on 40–60% of houses. Glazed-tile fronts on 10–15%. **The street facade is painted and the side and back walls are raw brick `#B8654A` on 60% of houses.**

**RANA_PALACE** (H §8): bays of 3.0–3.6 m; a central portico of 3–5 bays under a pediment; Corinthian columns Ø 0.5–0.7; French windows 1.2 × 3.0 with fanlights and white trim; a cornice projecting 0.4–0.6; a 1.0 m balustrade with urns on the piers. Colours: stucco `#F8F5EC` / `#F1E3BE`, trim `#FFFFFF`, shutters `#4E7D52`. Heroes measured in the pack: Singha Durbar r1574200 (104.8 × 85.1 m), Kaiser Mahal r1650246 (81.6 × 53.9), Gaddi Baithak w247501389 (46.4 × 36.3, with a height override: OSM says `height=1`), Babar Mahal w512547750. Narayanhiti is 1960s modernist and uses INSTITUTIONAL with a tower roof, not RANA (H §8).

**Never generated on ordinary houses:** the peacock window (hero decal at Pujari Math only), palace windows, *pāsukhā* five-unit windows (bahal shrine ranges only), figurative struts and temple *gajur* finials.

### 2.4 LOD bands and triangle budgets

W1 extrudes every building at about 18 triangles and draws all of them out to the near-ring edge: about 380 k triangles within 1.25 km of Asan, against a 95 k Mid slice (CONTENT_COVERAGE B8). W2 adds **four distance bands inside the near ring**. Band radii are measured from the camera.

| Band | What is drawn | Avg tris / building | Low radius | Mid | High | Build path |
|---|---|---|---|---|---|---|
| **B0 kit-lite** | Full §2.3 grammar: openings as insets, sanjhya, struts, bands, shopfronts, roof props | 900 (cap 2,500 for a house; religious buildings use their own budget, §3) | 0–35 m | 0–60 m | 0–80 m | Per **64 m detail cell**, built on a worker when the cell enters the band (`BuildingDetailMesher.BuildCell`), uploaded through the existing 2 ms / 1 MB queue |
| **B1 styled extrusion** | W1 extrusion with a real roof (gable or hip with overhang, flat with parapet), floor bands as vertex-colour stripes, window rows as darker vertex-colour quads (2 tris per window row per facade) | 70 | 35–120 m | 60–200 m | 80–250 m | Per tile (the W1 buildings layer, enriched) |
| **B2 prism** | Footprint simplified to its oriented box (or convex hull of ≤ 6 points), walls shared with a neighbour culled, flat or 2-triangle roof | 10 | 120–350 m | 200–500 m | 250–700 m | Per tile, a second building layer |
| **B3 city block** | Footprints rasterised to 16 m cells (max height, built-cover ≥ 30%), greedy-merged into boxes | ≈ 10 per box, about 1 box per 4 built cells | 350–750 m | 500–1,250 m | 700–1,750 m | Per tile, a third layer; in W3 it is replaced by D11 `BLKS` at L9/L8 |

**Budget check at Asan density** (5,712 buildings/km² [O], about 40% inside the view frustum [E]):

| Tier | B0 | B1 | B2 | B3 | Total | W2 slice (§10.4; ASSET_MANIFEST §1.4 value) |
|---|---|---|---|---|---|---|
| Low | 9 bldg × 700 = 6 k | 95 × 60 = 6 k | 777 × 8 = 6 k | ≈ 2 k | **≈ 20 k** | 22 k (32 k) |
| Mid | 26 × 800 = 21 k | 261 × 80 = 21 k | 1,506 × 10 = 15 k | ≈ 6 k | **≈ 63 k** | 72 k (95 k) |
| High | 46 × 900 = 41 k | 402 × 80 = 32 k | 3,068 × 10 = 31 k | ≈ 11 k | **≈ 115 k** | 135 k (170 k) |

The headroom (10–20%) covers plot splits and taller cores. W2 moves the rest of the ASSET_MANIFEST building slice to characters, vehicles and animals, which the life targets of §5 cannot meet inside their original slices (§10.4). **Generic sacred buildings count against this slice** (heroes do not; §3.2). **Drop order when a B0 building exceeds its cap** (H §10.8): lattice relief, then struts (fall back to a fascia stripe), then floor bands, then roof props. The sanjhya is never dropped. Band transitions cross-fade over 4 m with a dithered opaque fade (no alpha blending) [E]. B0 cells are cached in an LRU of 24 / 48 / 96 cells per tier.

**Courtyards.** Building multipolygons with holes, and AREA kinds COURTYARD (new) and RELIGIOUS that are bahals or chowks, get **no roof over the courtyard** (W1 roofs over 163 courtyards, CONTENT_COVERAGE B1). The courtyard decorator places paving, a well or tap, and, for bahals, a chaitya on the axis between the gate and the shrine range plus a shrine range with a torana and a *pāsukhā* window (§3.4). Courtyards are walkable public space. The shrine room door shows a curtain and an offering ledge, with no interior.

### 2.5 Palettes (cartoon hex values, all [E] anchored on H §9)

| Family | Tokens |
|---|---|
| Brick per profile (face / joint / highlight) | bkt `#B4432F` `#6E2219` `#D4664A`; ptn `#BF5236` `#74291D` `#DE7352`; ktm `#C25A3C` `#7A3022` `#E07A57`; ktp `#B85A40` `#713526` `#D47A5C`; thm `#C9663F` `#7E3A22` `#E8885E`; vil `#B86A4A` `#76402C` `#D48A68`; pnt `#BC553B` `#742C1F` `#DA7556` |
| Jhingati, a seeded 4-way mix per roof | carmine `#B8322B` 25%, scarlet `#D2452E` 35%, vermilion `#E2603A` 25%, tangerine `#EC8A3E` 10%, aged patches `#6F5A3C` 5%, ridge `#9E3A26` |
| Wood and ritual | sal dark `#4A2C1C`, mid `#6B4129`, light `#8E5B37`, painted black `#2B221D`, painted brown `#5A3222`, brass `#D9A93A`, copper `#C46A3A`, sindoor `#E23B2A`, marigold `#F6A21B`, lime `#F4EFE0`, ochre `#E0A845`, mud `#C9A27A`, plinth stone `#9A948A` |
| Modern paint (share) | white `#F7F6F0` 16, cream `#F6E6BE` 14, yellow `#FFD95A` 9, peach `#F8B48A` 9, pink `#F49AC1` 8, sky `#7FC8F8` 8, mint `#9FE2B8` 7, lime `#B6E05A` 5, turquoise `#3CC9C0` 5, lavender `#B79CE8` 4, orange `#F59A3B` 4, raw concrete `#ABA79F` 6, raw brick `#B8654A` 5 |
| Trim, railing, shutter, tank, CGI | trim white or body −20% value (50/50); railing `#2E3440` / `#3D7CC9` / `#C9A13A` (60/20/20); shutter `#8C949C` / `#3D7CC9` / `#C9433A` / `#3FA35C` (55/15/15/15); tank `#2A2A2E` / `#2F6FD6` / `#E9E4D4` / `#F2C230` (55/25/15/5); CGI `#3D7CC9`, `#3FA35C`, `#C9433A`, `#A2603A`, `#B9BEC3` (20 each) |
| Signs and flags | sign red `#E8483A`, yellow `#FFD23F`, blue `#2F7DE1`, green `#2FA84F`, white / ink `#FFFFFF` / `#1E1E1E`; prayer flags in a fixed order: blue `#2E6FD8`, white `#FFFFFF`, red `#D93A2B`, green `#2E9E4F`, yellow `#F2C230` |

Brightness rule: each family keeps its hue, with saturation raised 15–25% and value about 10% over the real material, so it reads on a phone in sunlight (H §9).

### 2.6 Roof and street-face props (rates per building, seeded)

| Prop | MODERN / HYBRID rate | Size | Notes |
|---|---|---|---|
| Water tank (black or blue polyethylene) | 60–80% of flat roofs, 1–3 each | Ø 0.9–1.3 × 1.0–1.6 on a 0.3–1.5 m stand (a 1,000 L tank is about Ø 1.1 × 1.2 [S]) | Real `storage_tank` objects from PROP replace the rate inside the Banepa campaign area |
| Solar water heater | 15–25% | 2 × 1.5 m rack, tilted 30–45° south | |
| Rebar stubs | 30–50% on growing streets (MODERN 4+ storeys) | 4–12 columns 0.3–1.0 m tall, rust `#8B4A2B` | |
| Stair cabin (*mumty*) | 100% of flat roofs ≥ 3 storeys | 8–15% of the roof area, 2.4 m high | |
| Laundry line | 40–60% from 08:00 to 17:00 on dry days | 2–6 m | None when it rains |
| Satellite dish | 10–20% | Ø 0.6–0.9 | |
| CGI shed (SKILLION) | 20% | | |
| Rooftop restaurant (Thamel) | 30–40% | Umbrellas, plastic chairs, string lights (emissive at night) | Thamel and Boudha kora only |
| Signboards | Shops: 80–95% of urban shop fronts; Thamel 3–8 per frontage plus blade signs 0.4–0.6 × 1.5–3 m | Fascia 0.6–1.0 m tall | Generic text only: Devanagari + English from a fixed list of 120 generic shop words [V localisation] |
| Prayer-flag strings across lanes | Thamel and the Boudha kora: every 20–40 m | flags 0.2–0.3 × 0.25–0.35 at a 0.3–0.4 m pitch | **Only at Buddhist sites and in Thamel**; never on Hindu temples (B18) |
| Wires and poles | §5.7 | | |

---

## 3. Temple generators and hero replicas

### 3.1 Replica fidelity rule (W2-O4)

For every hero and every OSM temple with `building:part` data:

| Property | Rule |
|---|---|
| Plan | The OSM outline (plinth) and parts at their exact coordinates. The sanctum is 0.45 × the bottom plinth width (Nyatapola and Pashupati both measure 0.45 [O]). |
| Height | From the T §5 table (heroes) or from the parts' `min_height`/`height`. **Any `height < 3` on a temple outline is a plinth height** (T §1.3). Total height is held to ±3%. |
| Tiers, plinth steps, doors | Exact (Nyatapola 5 + 5; Taleju KTM 3 on 12; Maju Dega 3 on 9; Pashupati 2 tiers, 4 doors). **Never 4 or 5 tiers on an unnamed temple**: only Nyatapola and Kumbheshwar are free-standing 5-tier temples [S]. |
| Orientation | The main door yaw from T §3 (bearing of the outward normal). Rows marked [V] are checked against imagery before stage 2 ships. |
| Deity and iconography | As T §3. Strut panels are generic stylised deities with no explicit imagery. Guardians appear in their real order. Garuda or Nandi face the door. |
| Cartoon latitude | Inside the envelope only: wall and roof thickness ×1.3, eave bells ×2 in size, gajur ×1.15 within the height budget, corner lift up to 8% (real ≤ 5%). The eave line stays straight, with no Chinese upsweep. |

### 3.2 Generator family

Every generator is engine-free, in `Core/Generators/Sacred/` (Track A). Each takes a parameter struct and a frame (anchor, yaw, ground height) and emits (a) `MeshData` for a LOD (0–3), and (b) a `StructureColliders` set: oriented boxes, step ramps and walkable tops, which the ground query uses (§10.3 contract) so the player can climb plinths and walk compounds.

| Generator | Key parameters (defaults; T §2) | Tri budget LOD0 / 1 / 2 |
|---|---|---|
| **Pagoda** | `PlinthLevels` 0–12; `StepRise` 0.25–0.6 (monumental 0.75–1.4); inset per level 0.06–0.09 × width; sanctum 0.45 × plinth width, core storey 0.45–0.6 × sanctum width; `FirstEave` 1.8–2.1 × sanctum; `TierShrink` **0.74** (0.62–0.84, the top tier shrinks most); eave spacing 0.23 × first eave; roof rise/width 0.22–0.25 at the bottom to 0.35–0.48 at the top; pitch 30–35° lower tiers, 35–45° top; struts per side per tier `max(2, round(wall/1.1)) + 1` plus a corner strut, at 50–60°; gajur 0.10–0.15 × H; bells every 0.4 m along each eave (Nyatapola 529 = 168/128/104/80/48 [S]); torana width = door + 0.6; door 1.0–1.6 × 1.8–2.2; lion pair at the stair, body 0.12–0.18 × plinth width; `Finish` Tile / GiltTop / GiltAll; optional pataka 0.3–0.6 wide | ≤ 600 per tier + plinth 300; generic 4,000 (6,000 for a curated 3-tier), hero per the LOD table below |
| **House-temple** (Bhimsen, Shiva-Parvati, Kumari Ghar type) | NEWAR grammar at 2.4 m storeys, 2–3 storeys, gilt upper window or balcony, tiled hip | ≤ 5,000 / 1,200 / 300 |
| **Shikhara, stone granthakuta** | 3–5 step plinth (0.4–0.6 rise); arcaded G (5 bays a side, ≈ 4 m); tower base 0.5 × plinth, tower height 2.0–2.4 × base; pavilions 0.18–0.22 × plinth width, 8 on storey 1 (4 corners + 4 mid-sides) and 4 on storey 2; amalaka Ø 0.6 × top; kalasha + gilt finial; colours `#B9A684` / `#8E8A80` | generic 6,000; hero per the LOD table below |
| **Shikhara, plastered** | height / base 2.0–2.5 (Anantapur 1.9 [O]); 12–24 ratha offsets; white `#F6F0E2` or brick; red or saffron pennant | ≤ 4,000 |
| **Stupa** | terraces 0 / 1 / 3, square with **20 corners**, widths 2.4 / 1.9 / 1.5 × dome Ø, rise 0.1–0.12 × Ø each; dome rise 0.33–0.42 × Ø (Boudha 0.31); harmika 0.21 × Ø wide, 0.15–0.2 × Ø tall, eyes on 4 faces + gilt torana; **13 rings**, base 0.24 × Ø, top 0.12 × Ø, spire height 0.3–0.45 × Ø; parasol 0.15 × Ø; finial 0.08–0.1 × Ø; 16–36 flag lines with 8–12% sag (heroes: Boudha 60–120); wheel niches Ø 0.20–0.25 × 0.35–0.45 at a 0.5 m pitch | S ≤ 2,000; M ≤ 6,000; Boudha 25,000 |
| **Chaitya** | 0.8–3 m tall, 0.6–2.0 m square; 2–4 base steps; drum with four niches in fixed order: **E Akshobhya, S Ratnasambhava, W Amitabha, N Amoghasiddhi**; dome, harmika, 13-ring cone, parasol; `#8E8A80` with sindoor and rice | ≤ 600 |
| **Shrine** (corner Ganesh, Bhairav, linga, nag stone) | Kind from the name or religion (T §2.7); size sampled from the OSM distribution per kind (Ganesh 4.5 / 7.2 / 10.7 m long side p25 / p50 / p75 [O]); open niche, or a 1-tier mini pagoda, or a tin canopy; image 0.3–0.8 m; bells Ø 0.12–0.25 | ≤ 1,500 |
| **Pati / sattal / mandapa** | Pati 6–12 × 3–5 m on a 0.4–0.9 m plinth, 3–5 posts, single slope or hip; sattal 2–3 storeys with an open G (Simha Sattal 14.8 × 9.8 × 7.5 m [O]); mandapa square with columns all round, 1–3 tiers (Kasthamandap 21.8 × 21.4 [O]); dabali platform 0.6–1.2 high, 5–9 m square | ≤ 3,000 / 5,000 / 12,000 |
| **Hiti** (dhunge dhara) | Sunken pit 1.5–5 m deep, rectangular or cruciform, 4–30 m; steps with a 0.2 rise and 0.3 tread; `number_of_water_spouts` makara spouts (else 1–3), 0.6–1.0 m long, 0.9–1.4 above the basin; flowing or dry (`status=Non-Operational`). A terrain stamp lowers the pit (W3 stamp; stage 1 draws the pit as a raised rim plus a dark floor) | ≤ 2,500 |
| **Pokhari** | Stepped stone edge, 0.5–1.5 m drop to the water, 0.25 m steps, water `#6E8B6A`; optional island shrine and causeway (Rani Pokhari: Balgopaleshwar, causeway from the south [V]) | ≤ 3,000 + shrine |
| **Gompa** | 2–4 storeys, whitewash `#F3EFE6` with a maroon penbe band `#7A1F1F` under the roof, trapezoid black windows, a portico with 2–4 red columns, a gilt dharma wheel with two deer, gyaltsen on the corners. Never used for Newar bahals (L6) | ≤ 6,000 |
| **Bahal decorator** | Quadrangle of 2–3 storey ranges, gate in the middle of one range (1.6–2.0 m high, torana, lion pair), shrine range opposite (richer facade, gilt torana, *pāsukhā* window, optional 1–3 tier lantern roof), 1–5 chaityas, mandala stone, bell, 3–5 bay arcades | uses the house grammar + chaitya |

**Selection for non-hero OSM objects** (Track A `SacredSelector`, from D1 classification):

| Input | Output |
|---|---|
| `building:part` present | Tiers and heights from the parts; the generator only adds struts, bells, gajur and plinth |
| TEMPLE_PAGODA footprint, no parts | Long side < 6 m → 1 tier; 6–14 m → 2 tiers when the name matches `mandir|temple|dega|deval` and religion is Hindu, else 1; ≥ 14 m → 2 tiers. 3 tiers only with a curated record. Gilt only with a curated record |
| Name matches `bhimsen` | House-temple |
| TEMPLE_SHIKHARA (D1 rules) | Stone granthakuta if `building:material=stone` or the name is in the curated list, else plastered |
| `man_made=stupa` or `tower:type=stupa` | Stupa S (< 8 m), M (8–20 m), L (curated) |
| POI without a footprint (1.18) | Shrine by name (Ganesh 235, Shiva 226, Bhairav 36 in the valley [O]), chaitya inside bahal areas, hiti at stone taps, pati at named pati and sattal; facing the nearest road |

Procedural dressing **never** places one of these where OSM or the curated DB has nothing (O2).

**LOD and triangle rules for sacred structures** (added in review; the per-generator caps above are ceilings, not what a frame can afford):

| Kind | Where it is counted | LOD0 / LOD1 / LOD2 / LOD3 (tris) | Distance bands | Concurrency cap |
|---|---|---|---|---|
| Generic (non-hero) pagoda, shikhara, stupa S/M, shrine, pati, hiti | Buildings slice (§2.4) | 1–2 tiers 4,000, 3 tiers (curated) 6,000, shrine/chaitya ≤ 1,500 / 600 / 120 / box | LOD0 inside the B0 radius, LOD1 inside B1, LOD2 inside B2, LOD3 (box + pyramid roofs) inside B3 | At most **3 / 5 / 8** generic sacred structures at LOD0 per frame (Low / Mid / High), nearest first; the rest drop one LOD |
| Hero (§3.4) | Hero slice: Low 20 k, Mid 40 k, High 60 k | Centrepiece heroes (Boudhanath, Swayambhunath, Nyatapola, Taleju KTM, Pashupatinath, Krishna Mandir) 25,000 / 8,000 / 2,000 / 200; every other hero 12,000 / 3,000 / 800 / 200 (ASSET_MANIFEST §1.5 class A and class B) | By screen height 0.35 / 0.12 / 0.04 (ASSET_MANIFEST §1.5) | **HeroLodBudget** (Track C): each frame, sort visible heroes by screen height, give each its LOD by screen height, then step the smallest-on-screen hero down one LOD until the slice fits. Low never uses LOD0. A hero changes at most one LOD per 0.5 s (hysteresis 10% of screen height) |

Worked check at Basantapur (about 14 heroes in view from the square): Mid = 1 hero at LOD0 (12 k) + 6 at LOD1 (18 k) + 7 at LOD2 (5.6 k) ≈ 36 k ≤ 40 k; Low = 3 at LOD1 (9 k) + 11 at LOD2 (8.8 k) ≈ 18 k ≤ 20 k; High = Taleju at LOD0 (25 k) + 1 at LOD0 (12 k) + 5 at LOD1 (15 k) + 7 at LOD2 (5.6 k) ≈ 58 k ≤ 60 k. V3 and V11 check it.

### 3.3 Compounds are walkable (W2-O1)

* A compound is any AREA `RELIGIOUS`, `COURTYARD` (new), landmark `compound` ref (D5), or bahal or chowk multipolygon hole. Inside it: `SACRED_NO_VEHICLE` (D14 routing and runtime), calm mode (§6.6), the courtyard audio snapshot (§7.4), and NPC behaviours from S §3.4 (touch-and-bow, bell ringing, sitting on plinths).
* Plinths are climbable. Step rises ≤ 0.45 m use auto step-up. Taller plinth levels (Nyatapola 1.4 m, Taleju 0.75 m × 12) are reached by their **real stair**, which the generator builds as a ramp collider with visual steps. Roofs, domes and spires are not climbable: their colliders are tagged `NoClimb`. A **0.8 m soft margin** around temple walls makes contact gentle.
* Sanctum doors are an opaque dark quad with a lamp-glow sprite. No interior volume exists.
* Pashupatinath: the main courtyard is walkable (W2-O1). The Bagmati ghats show steps and diyas, never cremation. Aircraft audio is capped at −6 dB inside the compound zone (AV §10.4).
* The OSM building under a hero or its parts is hidden by the hide zone (D5, CONTENT_COVERAGE B6). A test checks that no unhidden building intersects a hero footprint.

### 3.4 Hero replica list

Stage 1 (S1) heroes are the acceptance set in §1.2. Stage 2 (S2) adds the rest. All are generated from `HeroRecipe` records (curated DB, §9.4). Coordinates are OSM centroids (T §3).

| # | Hero | OSM anchor | Plan (m) | Height (m) | Form | Finish | Deity / dedication | Door yaw ° | Stage |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Boudhanath | stupa w56688295; kora wall w56688296 (95.8 × 95.5) | terraces 81.5 / 62.5 / 50.2; drum 38.0; dome 33.5; harmika 7.0 | **36** [S]; terrace tops 4 / 8 / 10.5 [O] | Stupa L, 3 terraces | Whitewash, gilt spire | Buddhist | 180 gate (n5302729521) | S1 |
| 2 | Swayambhunath | w201223707; landuse w115379177 (129.7 × 100) | dome Ø 26.7 | 33 | Stupa L + 5 Buddha niches; 365-step east stair; vajra ≈ 3 m long | Whitewash, gilt | Adi-Buddha | 90 stair | S1 |
| 3 | Anantapur / Pratappur | w255752871 / w115376623 | 8.8 × 8.7 / 7.9 × 7.8 | 17 / 17 [O] | Plastered shikhara | White | — | — | S1 |
| 4 | Harati (Swayambhu) | w257581957 | 6.6 × 6.5 | ≈ 8 [E] | 2-tier pagoda | Gilt roof [V] | Hariti | [V] | S2 |
| 5 | Pashupatinath | w913170315; landuse w125634570 (242.3 × 168.1) | 19.5 × 19.2 plinth; sanctum 8.7 × 8.3 | **23.7** [S] | 2-tier pagoda, 4 silver doors | Gilt copper both tiers | Shiva (Pashupati) | 270 (Nandi at the west door) | S1 (exterior + courtyard) |
| 6 | Taleju (KTM) | plinth w1414600808 (47.7 × 45.3) | body ≈ 18 m eave | **35** [S] | 3 tiers on a 12-stage plinth (≈ 0.75 × 12 = 9 m), compound wall on step 12 | Gilt ×3 | Taleju Bhawani | 190 [V] | S1 |
| 7 | Kasthamandap | w183558418 | 21.8 × 21.4; core 12 × 12 [S] | ≈ 20 [E] | 3-tier open mandapa (rebuilt 2021) | Tile | Gorakhnath shrine at the centre | open; shrine 90 [V] | S1 |
| 8 | Maju Dega | w183562980 | 22.6 × 22.2 | ≈ 25 [E] | 3 tiers on a 9-stage ochre plinth (≈ 6 m) | Tile | Shiva linga | 90 [V] | S1 |
| 9 | Trailokya Mohan Narayan | w1414593331 | 13.1 × 12.5 | ≈ 18 | 3 tiers on a 5-stage plinth + Garuda | Tile | Narayan | 270 [V] | S1 |
| 10 | Jagannath | w169429518 | 14.7 × 14.3 | ≈ 15 | 2 tiers on 3 stages | Tile | Jagannath | 270 [V] | S1 |
| 11 | Shiva-Parvati | w185882281 | 15.8 × 10.9 | ≈ 10 | House-temple, 2 storeys, figures at the window | Tile `#FC8C64` [O] | Shiva and Parvati | 190 [V] | S1 |
| 12 | Kal Bhairav | w196261745 (platform 5.8 × 5.6) | relief ≈ 3.6 tall | — | Open-air stone relief, **no roof** (the OSM roof tag is wrong) | Black-blue, gilt crown | Kal Bhairav | 270 [V] | S1 |
| 13 | Basantapur tower | n6348849285 [V] | ≈ 10 × 10 | ≈ 30 | 9-storey tower, tiered upper roofs | Tile | Palace | 180 | S1 |
| 14 | Hanuman Dhoka gate + palace front | n11365076769 [V] | front ≈ 100 | gate ≈ 6 | Gate, Hanuman statue (red cloth, umbrella), lions | Gilt door | Hanuman | 280 [V] | S1 |
| 15 | Kumari Ghar | n2659104413 | ≈ 22 × 22 | ≈ 12 | Newar courtyard house; **courtyard walkable, no Kumari, windows closed** | Tile | Kumari | 0 [V] | S1 |
| 16 | Gaddi Baithak | w247501389 | 46.4 × 36.3 | ≈ 14 [E] | RANA_PALACE front | Stucco | — | onto the square | S1 |
| 17 | Krishna Mandir | w120443307 | 14.3 × 14.2 | ≈ 20 | Stone shikhara, 3 storeys, **21 pinnacles**, friezes as decal bands | Stone `#B9A684` | Krishna (Radha, Rukmini) | **90** | S1 |
| 18 | Taleju (Patan) | w120443289 + parts | tower 16.9 × 15.3; eaves 18.3 / 13.5 / 9.4 | **34** [O] | 3 tiers on the palace block (17.5) | Tile, gilt finial | Taleju | 270 [V] | S1 |
| 19 | Vishwanath | w328903213 | 12.6 × 11.4 | ≈ 15 | 2 tiers; stone elephants at the east door | Tile `#C27C36` [O] | Shiva | 90 | S1 |
| 20 | Bhimsen (Patan) | w326472980 | 11.5 × 11.4 | ≈ 14 | House-temple, gilt balcony | Tile + gilt | Bhimsen | 90 [V] | S1 |
| 21 | Yoganarendra column; Taleju bell; Manga Hiti | n2097740385; w199775523 (12.6 × 9.0); n10034234987 | — | column 6–9 | Column; bell pavilion; hiti with 3 spouts | Gilt king | — | — | S1 |
| 22 | Golden Temple (Kwa Bahal) | r4624856 | court 26.1 × 25.0 | shrine ≈ 15 | Bahal + 3-tier shrine, prayer wheels, central chaitya | Gilt copper | Shakyamuni | entries E and W; shrine 90 [V] | S1 |
| 23 | Kumbheshwar | n1759661519; complex w81147178 (80.6 × 59.2) | ≈ 12 × 12 [E] | ≈ 25 | **5 tiers**, ratio 0.76; Nandi | Tile, gilt finial | Shiva | [V] | S1 |
| 24 | Mahabouddha | n564129617 (no footprint) | ≈ 9 × 9 | ≈ 18 | Terracotta shikhara, Buddha-tile relief (> 9,000 [S]) as a repeated texture | Terracotta `#B5603E` | Buddha | [V] | S2 |
| 25 | Nyatapola | w85470341 + 35 parts | plinth 24.2 → 21.7 → 18.1 → 15.4 → 10.9; sanctum 10.9 × 9.4; eaves 19.3 / 16.3 / 12.6 / 9.3 / 5.8 at 11.6 / 16.4 / 20.7 / 24.7 / 29.0 | **33.2** (roof 31.7 + finial 1.5) | 5 tiers on 5 plinths (5 × 1.4 m); guardian pairs on the south stair | Tile `#7A2E22` | Siddhi Lakshmi | **180** | S1 |
| 26 | Bhairavnath | w185746728 | 16.9 × 14.6 | ≈ 20 | 3-tier **rectangular** pagoda, offering window | Tile, gilt pataka | Bhairav | 270 [V] | S1 |
| 27 | 55-Window Palace | none [O] (≈ 27.67235, 85.42830 [E]) | ≈ 50 × 12 | ≈ 12 | 3 storeys, 55 windows at a 0.9 m pitch | Brick | — | 180 [V] | S1 |
| 28 | Golden Gate | n11365076869 [V] | ≈ 4 wide | ≈ 5.5 | Gilt torana in a red gatehouse, white walls; Mul Chowk beyond is walkable (W2-O1): a generated palace courtyard (bahal decorator with the Taleju shrine range exterior, sanctum closed, no greybox in a shipped build) and an `entry_rule` card that says the real courtyard is closed to non-Hindus | Gilt | Taleju | 180 [V] | S1 |
| 29 | Vatsala Durga | w211082585 | 7.3 × 6.1 | ≈ 14 | Stone shikhara + barking-dog bell pavilion | Stone | Vatsala Durga | 180 [V] | S1 |
| 30 | Pashupati (Bhaktapur), Fasidega, Siddhi Lakshmi, Chyasing Mandap | n12293366201; w185750794 (21.8 × 21.6, 6-stage plinth); w211082584; w495632745 | as listed | ≈ 15 / ≈ 15 | 2-tier pagoda; white shikhara with elephants; stone shikhara; mandap 7.8 × 7.8 | Tile / white / stone | Shiva / Shiva / Siddhi Lakshmi / — | [V] | S2 |
| 31 | Dattatreya | n11365112169; Tachupal w1192828078 (46 × 22.8) | ≈ 12 × 12 | ≈ 18 | 3 tiers, wrestler pair | Tile | Dattatreya | 270 [V] | S1 |
| 32 | Peacock window | Pujari Math lane | 1.2 × 1.0 [E] | — | Decal on a brick wall | Carved wood | — | lane | S1 |
| 33 | Changu Narayan | w186264410; compound w706922152 (74.5 × 68.7) | 15.0 × 14.8 | ≈ 15 | 2 tiers, plinth 1.2 [S]; 4 gates with guardian pairs; Garuda | **Gilt copper** | Vishnu | **270** | S2 |
| 34 | Dharahara (2021) | n11622074774; parts w1413193274 / w1414016053 / w1413193272 | base 14.2, shaft 10.3, top 9.3 | **72** [S] incl. bronze mast | 22-storey fluted white tower, balcony ring, cone cap | White | Civic | [V] | S1 |
| 35 | Budhanilkantha | w85552443 (48.1 × 42.8) | 13 m tank | 5 m figure | Reclining Vishnu on Shesha in a tank, open sky | Black basalt | Vishnu | head direction [V] | S2 |
| 36 | Bajrayogini (Sankhu) | w235411212 (78.0 × 60.5) | — | ≈ 15 | 3 tiers | Tile + gilt | Vajrayogini | [V] | S2 |
| 37 | Bagh Bhairab (Kirtipur) | w317754743 (35.5 × 31.0) | — | ≈ 15 | 3 storeys: 2 tile roofs + a gilt top | Tile + gilt | Bhairav (tiger) | [V] | S2 |
| 38 | Rato Machhindranath (Bungamati) | w363529870; bahal w442472145 (53.7 × 52.3) | 11.2 × 10.8 | ≈ 20 | Plastered shikhara (rebuilt [V]) | Brick / plaster | Karunamaya | [V] | S2 |
| 39 | Kopan main gompa | w206085866 | 30.4 × 19.8 | ≈ 15 | Gompa | Whitewash, maroon | Gelug | 180 [V] | S2 |
| 40 | Kathesimbhu | n3377725834 | dome Ø 10–12 [E] | ≈ 12 | Stupa M + Harati shrine n4582884189 | Whitewash | Buddhist | — | S1 |
| 41 | Rani Pokhari | (water polygon, 165.8 × 124.7 [O]) | — | — | Pokhari with island shrine and causeway | Stone | Balgopaleshwar | causeway S [V] | S2 |
| 42 | Annapurna (Asan Ajima) | n3569849497 (no footprint; take the containing building or a curated outline [V]) | ≈ 7 × 7 [E] | ≈ 12 [E] | 3-tier pagoda; a silver purnakalash, not an image, inside (never shown) | **Gilt copper all tiers** [S] | Annapurna | [V] | **S1** (Asan, G1 tile) |
| 43 | Seto Machhindranath (Jana Bahal) | w501427078; bahal node n4308805189 | ≈ 9 × 9 [E] | ≈ 14 [E] | Pagoda in the middle of a bahal court, chaityas and pillars around it; tier count [V] (2 per photos [E]) | Gilt metal roof [S] | Karunamaya / Avalokiteshvara (Janabaha Dyo), worshipped by Hindus and Buddhists | [V] | S2 |
| 44 | Akash Bhairab (Indra Chowk) | w136526480 | per OSM | ≈ 12 [E] | House-temple, 3–4 storeys, gilt upper windows, lions on the balcony [V] | Tile + gilt | Akash Bhairab (the mask is shown only through closed lattice, as most of the year) | onto Indra Chowk [V] | S2 |
| 45 | Seto Bhairav mask | near the Taleju bell w183563646 | panel ≈ 3 × 3.5 [E] | — | Gilt mask behind a closed wooden lattice (T §4.4) | Gilt | Seto Bhairav | onto the square [V] | S2 |
| 46 | Balkumari (Thimi) | n1945526719 / n10068876012 (no footprint) | ≈ 10 × 10 [E] | ≈ 15 [E] | **3-tier pagoda** [S] | **Gilt copper** [S] | Balkumari | **0 (north)** [S] | S2 |
| 47 | Rudrayani (Khokana) | n4344434602 (no footprint) | ≈ 9 × 9 [E] | ≈ 13 [E] | **3-storey** pagoda with a projecting inclined lattice on the second storey [S] | Tile | Rudrayani (a form of Durga) | onto Chwe Lachi square [V] | S2 |
| 48 | Uma Maheshwar (Kirtipur) | curated [V] (hilltop of Kirtipur, not in the OSM name index) | ≈ 10 × 10 [E] | ≈ 15 [E] | **3 roofs** (built with 4; the top one was lost in 1934 and never rebuilt [S]) on a tall stepped platform, stone elephants at the stair [V] | Tile | Shiva and Parvati | [V] | S2 |
| 49 | Chilancho stupa (Kirtipur) | w1074769018 | per OSM | ≈ 8 [E] | Stupa M with four small stupas around it [V] | Whitewash | Buddhist | — | S2 |
| 50 | Ghantaghar | w194744621 | per OSM | OSM `height=15` [O]; the pre-1934 tower was about 30 m [S] — take the rebuilt height from imagery [V] | Rana-era clock tower, white, four clock faces | Stucco | Civic | — | S2 |
| 51 | Guhyeshwari | w112664308 (27.7112, 85.3533: correct position; the node n4525481095 is the wrong one) | per OSM | ≈ 12 [E] | Courtyard temple, the shrine roofed by a gilt canopy held up by four gilt serpents [V] | Gilt | Guhyeshwari (Shakti peeth) | [V] | S2 |
| 52 | Dakshinkali | n360060921 | ≈ 15 × 10 [E] | canopy ≈ 4 [E] | Open-air shrine in a forested gorge at a stream confluence, long stair with stalls | Gilt canopy | Kali | [V] | S2: **no sacrifice, animals being led, blood or knives are ever shown**; bells only |

**Open grounds that make the centre read as Kathmandu** (no generator; area dressing only, S2): Tundikhel w184884797 (open grass, crows at dusk, the army pavilion as a closed building), Ratna Park, Narayanhiti (w206868352, INSTITUTIONAL with a tower roof, §2.3) and the Bagmati ghats at Teku and Thapathali (steps and small shrines, no cremation).

Landmark-lite (CONTENT_COVERAGE L17, about 150 valley sites) uses the same generators with curated tiers and yaw in W3.

---

## 4. Roads

### 4.1 Real-width model (when OSM has no `width`)

Tagged widths always win (`width` covers 74% of trunk length, 78% of primary, 61% of secondary and 44% of tertiary, but only 11% of residential [O]). They are clamped to `[class floor, corridor]`. For one-way ways the width is **per carriageway**. Missing values come from class × area type (R §8.1; area type from §9.2 `RATR`):

| Class | OLD_CORE | URBAN | PERI_URBAN | RURAL | HILL |
|---|---|---|---|---|---|
| trunk (two-way way) | – | 14.0 / 4 lanes | 14.0 / 4 | 9.0 / 2 + 1.5 shoulders | 7.5 / 2 + 1.0 |
| trunk (one-way carriageway) | – | 7.0 / 2 | 7.0 / 2 (9–10 m = 3 lanes on Araniko) | 7.0 / 2 | 7.0 / 2 |
| primary | 7.0 / 2 | 7.0 / 2 (14 / 4 if `lanes=4`) | 7.0 / 2 | 6.5 / 2 | 7.0 / 2 + 1.0 |
| secondary | 6.0 / 2 | 7.0 / 2 | 7.0 / 2 | 5.5 intermediate | 5.5–6.0 / 2 + 0.75 |
| tertiary | 5.0 / 1–2 | 7.0 / 2 | 6.0 / 2 | 4.5 / 1 | 4.5 / 1 + passing bays |
| unclassified | 3.5 / 1 | 4.0 / 1 | 5.0 / 1–2 | 3.5 / 1 | 3.5 / 1 |
| residential | 4.0 / 1 | 5.0 / 1–2 | 5.0 / 1 | 3.5 / 1 | 3.5 / 1 |
| living_street | 3.0 | 3.0 | 3.5 | 3.0 | 3.0 |
| service | 3.5 | 4.0 (6 at fuel stations, bus parks) | 4.5 | 3.5 | 3.0 |
| track | 3.0 | 3.0 | 3.0 | 3.0 | 3.0 |
| pedestrian street | 4.0 | 4.0 | 4.0 | – | 3.0 |
| footway / path / steps | 1.5–2.0 / 1.5 / 1.5 | 2.0 / 1.5 / 2.0 | 2.0 / 1.5 / 1.5 | 1.5 / 1.2 / 1.2 | 1.2 / 1.0 / 1.2 |

Lanes when `lanes` is missing: `clamp(round(width / 3.5), 1, 6)` for trunk to secondary, `round(width / 3.0)` for tertiary and below. A two-way way narrower than 5.5 m is one lane with no centre line (RSN5, NURS 2076 [S]). Floors: trunk 6, primary 5, secondary 4, tertiary 3, residential, unclassified and service 2.5, track 2, footway 0.9, path 0.6 m.

### 4.2 The wider-game rule (W2-O3)

```text
real    = tagged (parse_length_m) if plausible, else §4.1(class, area)      ; clamp [floor, 40]
scaled  = real × S          S = 1.25 motor classes, 1.15 pedestrian, 1.0 footway/path/steps
game    = max(scaled, Min(class, oneway))                                     ; §4.3
limit   = corridor − 1.0    corridor = 2 × min(dLeft, dRight) from RATR samples (0.5 m to each building)
game    = max(min(game, limit), real)                                         ; never cut a building, never narrower than real
game    = min(game, real + 6.0)                                               ; proportions survive
```

* **Why 1.25:** 3.5 m lanes become 4.4 m, and 7 m two-lane roads become 8.75 m. That fits a chunky 2.0–2.2 m cartoon car overtaking a scooter, and it keeps the order trunk > primary > residential (R §9.1). In a simulation on real corridors the clamp bites on 25–43% of urban and old-core samples and on ≤ 6% of rural and hill samples (R §9.3).
* **When the corridor is unknown** (no building within 40 m on both sides): `limit = ∞` outside OLD_CORE and `limit = real` inside it. OLD_CORE cells with building cover < 0.3 (incomplete mapping in Bhaktapur, Patan, Thimi, Kirtipur [O]) also use `limit = real`.
* **Width changes** along a way taper at **1 : 20** (minimum 10 m) when Δw ≥ 0.5 m. Smaller corridor jitter is smoothed with a 30 m moving minimum, so roads never "breathe".
* **Curve widening** (NURS Table 10 [S], × S, on the inside of the curve): two lanes +1.5 m at R ≤ 40, +1.2 at 41–60, +0.9 at 61–100, +0.6 at 101–300; one lane +0.9 at R ≤ 20, +0.6 at 21–60.
* **Dual carriageways** grow outward, away from the median, so the median never disappears.
* This replaces `RoadStyle.DefaultWidthM` (W1 defaults: trunk 10, primary 8, secondary 7, tertiary 6, residential 5). Those are too wide for HILL and OLD_CORE and too narrow for a 4-lane trunk.

### 4.3 Minimum game widths

| Road kind | Minimum | Reason |
|---|---|---|
| Two-way trunk, primary, secondary, tertiary | **6.5 m** | Two 2.5 m buses + 3 × 0.5 m clearance (NRS design vehicle [S]) |
| One carriageway of a dual road | 7.0 m | Ring Road and Araniko carriageways are 6–7 m real |
| Two-way residential, unclassified, service | 4.0 m | A car and a scooter pass; municipal minimum 4 m [S] |
| Any single-lane or one-way motor road | 4.0 m | ≥ 1 m of clearance each side for a cartoon car |
| Track, living street | 3.0 m | |
| Footway, path, galli | 1.5 m (OLD_CORE 1.2) | Two walkers pass; camera clearance |
| Steps | 1.2 m | |

The corridor clamp always wins over a minimum. Access then decides who may enter (§4.4).

### 4.4 Access by final game width (narrow lanes stay real)

| Final game width | Player may | AI traffic | Real match |
|---|---|---|---|
| < 1.8 m | Walk; a bicycle or motorbike is pushed (slow walk animation) | Pedestrians only | Asan and Indrachowk gallis, bahal passages |
| 1.8–3.5 m | + cycle, scooter, motorbike | Two-wheelers, bicycles, porters, carts | Most old-core lanes, Thamel side lanes |
| 3.5–4.5 m | + car or taxi, one lane, no overtaking | Two-wheelers + an occasional car in one direction; no micro | New Road side streets, Patan core |
| 4.5–6.0 m | + micro, jeep, tempo, small truck | Two-way cars; no bus or full truck | Residential streets |
| ≥ 6.0 m | Everything | Full mix | Tertiary and above |

Also: **heritage squares and every compound are walk-and-cycle only** whatever their width (OSM `highway=pedestrian`, RELIGIOUS AreaKind, HERITAGE_ZONE flag). `access=no` and `motor_vehicle=no` always win. **No bus** on a way whose smoothed hairpin apex radius is under 12 m (F24 has a 3.6 m apex [O]). Inside the Bhaktapur core there are **no AI cars**, and the player's car gets the "park here" prompt at the core edge [V current municipal rule]. The access masks also feed the routing profiles (`Travel`), so routes, AI and mesh agree.

### 4.5 Cross-section, markings and kit (game metres; widths across scale with S, lengths along stay real)

| Element | Value | Source |
|---|---|---|
| Centre and lane line | White `#F2F0E8`, 100 mm × S; broken 1.5 m / 4.5 m gap (urban), 2.0 / 7.0 (rural); warning 4.0 / 2.0 approaching junctions; continuous on curves, crests and bridges | RSN5 Table 1 [S] |
| Paint rule | Centre line only when the **real** width ≥ 5.5 m and the surface is ASPHALT or CONCRETE; lane lines when lanes ≥ 3 or on a dual carriageway | RSN5 [S] |
| Edge line | **Yellow** `#F2C230`, 150 mm, 100 mm in from the edge; continuous near bends and bridges | RSN5 [S] |
| Wear | Trunk and primary URBAN crisp; PERI, RURAL and HILL decal alpha 0.4–0.7 | [S/E] |
| Stop line | 200 mm (urban); 2–3 m before an unsignalised zebra | IRC:35 [S] |
| Zebra | Depth ≥ footpath width, min 2.0, usually 2–4 m; bars 0.5 m with 0.5 m gaps across the full carriageway [V local practice] | IRC:35, NURS [S] |
| Zebra placement | At OSM `crossing=zebra|marked|traffic_signals` or `crossing:markings=yes|zebra`; plus generated at every signalised or police chowk, every 200 m on URBAN trunk, primary and secondary, and within 150 m of schools. **None in OLD_CORE** (shared streets) | R §10.2 |
| Refuge island | ≥ 1.2 m wide where the crossing is longer than 10.5 m | IRC:35, NURS [S] |
| Footpath | Per class × area (R §8.2): trunk dual 2.5–3.0 (+2.0 cycle track on Ring Road south); primary URBAN 2.0–3.5 (Kanti Path ≈ 3–4); secondary and tertiary URBAN 1.5–2.0 on 60% of length, one side on 20%, none on 20%; OLD_CORE none (shared surface, 0.3–0.6 m plinths along the house fronts). **Raised +150 mm**, cross-fall 2.5–3%, red/grey pavers `#B5655A` / `#9C9A94` | NURS §4.3 [S] |
| Kerb | Barrier 150 mm next to footpaths; mountable 100 mm with a 45° face on medians and islands; 150 mm top × 300 mm deep, `#BDB8AE`; ≤ 50 mm at crossings; median and island kerbs painted in 500 mm black-and-white bands (`#202124`) [V: yellow-black in practice] | NRS §13.6, IRC:35 [S] |
| Drains | Covered slab drain 0.45–0.6 m under URBAN footpath edges; open 0.5 m ditch on PERI and RURAL; uphill open drain 0.6 × 0.45 m on HILL; brick centre channel 0.2–0.3 m in Bhaktapur and Patan | R §8.2 |
| Speed hump | 3.7 m along, 0.10 m high, parabolic, 200 mm black-and-white stripes at 45°; residential and unclassified URBAN near schools, hospitals and temple gates, ≈ 1 per 300 m; never on trunk | NRS §13.2 [S] |
| Lamps | Arterial 10–12 m poles every 30–35 m; local 9–10 m every 25–30 m; OSM lamps first | NURS Table 14 [S] |
| Bus bay | 15 m recess per bus, 3.0 m deep, tapers 1 : 8; at OSM `bus_stop` nodes on trunk and primary | NURS §4.8 [S] |
| Surfaces | Fresh asphalt `#4A4D52` (Ring Road, Araniko), worn `#6B6A66`, concrete `#A9A59C`, brick `#A4553A` with joints `#7E3F2B`, stone slabs `#8E8A80` | R §10.4 |

Markings are drawn into a separate **road-decal layer** (an extra tile layer, `TileLayers.RoadDecals`), with a polygon offset so they never z-fight with the ribbon.

### 4.6 Dual carriageways and medians

Two one-way ways with opposite directions, the same `ref` or name, and centrelines < 15 m apart are paired by the pipeline (`RATR.partner_way`). They are meshed as **one road with a median**: median = centreline spacing − the two half-widths, never < 1.0 m. Ring Road south: centrelines 7.4 m apart [O] → median ≈ 1.0 m, kerbed and mountable with a 1.1 m steel fence [E]. Durbar Marg and Kanti Path: tree median 1.5–4 m [V]. Araniko: medians 2–3 m (traffic-island polygons 700–2,080 m² [O]). The **service roads** of Ring Road south (one-way `primary`, 9.3 m out [O]) keep their own ribbons, with a 2.8 m green separator (planters every 6 m) between them and the main carriageway.

### 4.7 Junctions and roundabouts

| Item | Rule |
|---|---|
| Junction cap | A polygon from the **game** widths of every arm. Corner kerb radius 6 m when both roads are ≥ 6.5 m, 3 m otherwise, 0.5 m in OLD_CORE (sharp brick corners). Ribbons stop at the cap edge |
| Mapped roundabout or circular (21 ways + 10 mini nodes in the valley [O]) | Ring width = widest approach (game) + 1 m, min 7 m; island Ø = ring Ø − 2 × ring width; a 1.0 m mountable apron inside the ring for buses; splitter islands on approaches ≥ 7 m wide. Jawalakhel: ring centreline Ø 43.8 → island ≈ 35 m (measured 36 m [O], a check). Balaju Ø 29.5 with a 7 m island; Tripureshwor ≈ 32 with a 13 m island; Maitighar Mandala ≈ 57 with the mandala monument island |
| Mini roundabout | A painted or raised disc Ø 4–6 m |
| Synthetic island | Chowks with ≥ 4 arms, at least one trunk or primary arm, and no mapped island: a round island of Ø = 0.7 × widest arm, clamped to 6–16 m, holding the police podium and a planter. **No shrine or statue is generated** (O2) |
| Mapped islands | `area:highway=traffic_island` polygons and grass or park polygons within 45 m are used as the island footprint |
| Police podium | Ø 1.2–1.5 m, 0.3–0.4 m high, white, with a 2.2 m umbrella (red or blue) [E/V] |
| Signals | Only at the 50 OSM `traffic_signals` nodes [O]; pole + 3 lamps; cycle 90 s |

Police-controlled chowks (curated list `pipeline/config/curated/chowks.yaml`, from V §5.3): Kalanki, Balkhu, Ekantakuna, Satdobato, Mahalaxmi, **Koteshwor (signals + police)**, Tinkune, Sinamangal, Chabahil, Sukedhara, Narayan Gopal Chowk, Basundhara, Samakhushi, Gongabu, **Balaju (roundabout + police)**, Machhapokhari, Sitapaila, **Maitighar (roundabout + police)**, Thapathali, **Tripureshwor (roundabout + police)**, Jamal, Lainchaur, Putalisadak, Bagbazar, New Baneshwor, Bhatbhateni, **Jawalakhel (roundabout + police)**, Pulchowk, Lagankhel, Jorpati, Gatthaghar, Jadibuti, Sanepa: 33 chowks. 1 officer at medium chowks, 2–4 at Kalanki, Koteshwor, Chabahil, Narayan Gopal, Satdobato, Maitighar, Thapathali, Tripureshwor, Jamal and Lainchaur.

---

## 5. Life

### 5.1 Traffic simulation

**Lane graph** (Track B, engine-free, `Core/Traffic/`):

* Built per level-10 tile from `ROAD` + `RATR` + `JNCT`, stitched at tile borders through the context points. Only motor-legal roads with game width ≥ 1.8 m are included; trails go into the pedestrian graph.
* **Left-hand traffic.** Lane `k` (0 = kerb side) in the travel direction is centred at `−(k + 0.5) · laneW` from the road centreline for two-way roads (negative = left of travel), and at `(k + 0.5) · laneW − w/2` on one-way carriageways. `laneW = gameWidth / lanes`. Two-way 1-lane roads (< 5.5 m real) carry one shared lane, and agents pass head-on by both shifting 0.6 m left at 10 km/h.
* Overtaking uses the rightmost lane. Motorbikes keep to the kerb lane and **filter**: sub-lane offsets ±0.6 m, with up to 8 per lane in the "box" in front of the stop line (V T5).
* Lane connectors at junctions are cubic Béziers between lane ends with a 0.4 × chord handle length. Turn restrictions come from D2 `type=restriction` (90 relations Nepal-wide [O]).

**Car following: IDM** (Intelligent Driver Model) per class [E]:

| Class | v0 | T headway (s) | s0 min gap (m) | a (m/s²) | b comfortable decel (m/s²) |
|---|---|---|---|---|---|
| Two-wheeler | class target (§5.2) | 0.9 | 1.0 | 2.5 | 3.0 |
| Car, taxi, SUV | target | 1.2 | 2.0 | 2.0 | 3.0 |
| Micro, tempo, minibus | target −10% | 1.4 | 2.5 | 1.4 | 2.5 |
| Bus, truck, tanker | target −15% / −10% | 1.7 | 3.0 | 0.9 | 2.0 |
| Bicycle, rickshaw, tractor | 14 / 8 / 18 km/h | 1.0 | 1.0 | 0.8 | 2.0 |

**Speeds** (V §4.2, game agent free / peak, km/h): trunk 45 / 15, trunk service 28 / 10, primary 35 / 12, secondary 30 / 12, tertiary 25 / 10, residential and unclassified 18 / 8, old-core lanes and living streets 10 / 5. Time-of-day density multipliers: 05–07 0.3, 07–08 0.7, **08–10 1.0**, 10–16 0.75, **16–19 1.0**, 19–22 0.5, 22–05 0.12 (trucks and long-distance buses ×3 at night); Saturday ×0.6. A queue of more than 25 vehicles at a junction throttles spawning on its approaches and switches agents to creep mode (5 km/h, 2 m gaps).

**Junction controllers:**

| Kind | Logic |
|---|---|
| POLICE (default on the chowk list) | 2 phases at T-junctions, 3–4 at 4-arm chowks; each phase 25–60 s; +15 s for an arm with a queue > 12; min 20 s; a short "free right turn" pulse in jams. An animated officer plays 5 hand signals (stop front, stop behind, go, slow, right turn allowed) and a whistle chirp (2.8–3.2 kHz, 120 ms) on each phase change |
| SIGNAL (the 50 OSM nodes) | 90 s cycle: 3 phases of 25 s + 3 × 5 s amber and clear |
| ROUNDABOUT | Circulate **clockwise**; give way at entry to vehicles already on the circle (they come from the right); circle speed 15–20 km/h; gap acceptance 3.0 s (cars), 2.0 s (two-wheelers), 4.5 s (buses) [E] |
| PRIORITY | The higher class has priority; equal classes yield to the right [E]; two-wheelers creep in with 1.5 s gaps |

**Other rules** (V §4.1): everyone gives way to cows and dogs (soft avoidance radius 3 m; stop if the path is blocked > 2 s; **no honk at cows**). Pedestrians cross anywhere: urban cars slow to 15 km/h when a pedestrian is within 6 m ahead, and yield on zebras. Micros, minibuses and tempos stop 8–20 s at bus stops and at random once per 600 m on their route. **No ambulance sirens** in the W2 ambient set (A §2.6, rating). There are **no motor vehicles in compounds or heritage squares** (L16).

**Honking** (post-2017 no-horn rule [S]): blind bend 0.6 (trucks and buses 0.9); overtaking 0.25 (motorbike 0.35, double tap); blocked > 4 s 0.3, then 0.2 every 6–10 s, max 3 per blockage; front car not moving 2 s after a green phase or a police wave 0.4; cow 0; old core and temple squares ×0.3. The mixer caps horn voices at 3–4.

**Simulation LOD** (per tier, Low / Mid / High): a full agent sim within 150 / 250 / 400 m of the player; beyond that, a "ghost" sim on the lane graph only (position along the lane, IDM at 2 Hz, no ground query) out to the near-ring radius. Agents spawn 80–150 m away and out of view, and despawn when they are > 1.2 × the sim radius away and out of view. Caps from ARCHITECTURE §10: **moving vehicles 12 / 30 / 50**, NPCs 25 / 60 / 120.

### 5.2 Vehicle catalogue (generic, procedural)

Dimensions from the reference class, ±5% (V §3). Cartoon overlay: wheels ×1.15, cab or head ×1.1, length ×0.95. Plates use the legacy colour code with fictional numbers in Devanagari numerals in the format `बा ## X ####`, where the letter X encodes size and ownership (private क heavy / च light / प two-wheeler; public ख / ज / फ; government ग / झ / ब; V §3.1 [S Wikipedia plates]), e.g. a private car `बा ३ च ४५६७`, a taxi `बा १ ज ...`, a private scooter `बा ८६ प ...`: private red `#C62828` with white text, public black `#111111`, government white with red, corporation yellow, tourist green `#2E7D32` [S Wikipedia plates].

| Asset id | Class | L × W × H (m) | Wheelbase / wheel Ø | Seats | Colour presets (hex) | Share on an URBAN primary road (sums to 100; the per-class spawn mix below is authoritative) | Driven by player |
|---|---|---|---|---|---|---|---|
| `ghm_veh_scooter_a` | CVT scooter | 1.81 × 0.72 × 1.15 | 1.26 / 0.48 | 2 | `#E53935` `#1E88E5` `#FDD835` `#FAFAFA` `#212121` `#8E24AA` `#43A047` `#FB8C00` | 21% | yes |
| `ghm_veh_motorbike_commuter_a` | 125–160 cc | 2.05 × 0.77 × 1.08 | 1.32 / 0.62 | 2 | `#212121` 35%, `#C62828`, `#1565C0`, `#9E9E9E`, `#FAFAFA`, `#2E7D32` | 34% | yes |
| `ghm_veh_motorbike_cruiser_a` | 350 cc retro | 2.15 × 0.80 × 1.09 | 1.39 / 0.66 | 2 | `#212121` `#3E2723` `#455A64` chrome `#CFD8DC` | 2% | yes |
| `ghm_veh_escooter_a` (new) | E-scooter | 1.80 × 0.70 × 1.10 | 1.30 / 0.45 | 2 | pastels `#80DEEA` `#F8BBD0` `#FFF59D` `#FAFAFA` | 5% (core) | yes |
| `ghm_veh_bicycle_a` | Roadster / MTB | 1.80 × 0.60 × 1.05 | 1.12 / 0.71 | 1 | `#212121` `#1565C0` `#C62828` `#2E7D32` | 3% | yes |
| `ghm_veh_rickshaw_cycle_a` | Cycle rickshaw | 2.60 × 1.10 × 1.90 | 1.55 / 0.66 | 2 + puller | body `#1565C0` / `#C62828` / `#2E7D32`, hood `#212121` / `#FBC02D` | old core only, max 2 alive | passenger |
| `ghm_veh_tempo_safa_a` | Safa tempo (EV, 10–12 pax) | 3.60 × 1.45 × 1.90 | 2.10 / 0.60 | 12 | white `#F4F4F0`, green `#2E8B57`, skirt `#263238` | 3% (inner routes) | yes |
| `ghm_veh_microvan_a` | Microbus 14–15 seats | 4.70 × 1.70 × 1.98 | 2.57 / 0.66 | 15 | white `#F5F5F5` 80% + belt stripe `#1565C0` / `#C62828` / `#2E7D32` / `#F9A825`; Devanagari route board `#FFF3E0` with `#B71C1C` text | 7% | yes |
| `ghm_veh_bus_city_a` (`city_green` preset) | 10.5 m city bus | 10.5 × 2.50 × 3.20 | 5.4 / 0.95 | 36 + standing | green `#2E7D32`, band `#66BB6A`, white roof `#FFFFFF`, display `#FDD835`; no operator logo, and the stripe layout is our own, not a copy of any operator's livery (hard rule: no real liveries) [V brand check with the plate and livery avoid-list] | 0.5% | yes |
| `ghm_veh_bus_city_a` (minibus preset) | 7.2 m minibus | 7.2 × 2.10 × 2.85 | 3.8 / 0.85 | 25–30 | cream `#FFF8E1` + stripes `#C62828` / `#1565C0` / `#EF6C00`, floral corners | 1.0% | yes |
| `ghm_veh_bus_tourist_a` | 11 m long-distance coach | 11.0 × 2.50 × 3.35 | 5.6 / 1.00 | 40–45 | `#FAFAFA` / `#FFF8E1` + art bands `#D32F2F` `#FBC02D` `#1976D2` `#388E3C` | 0.25% (Kalanki, Koteshwor, Gongabu) | yes |
| `ghm_veh_bus_school_a` (new) | School bus | 8.0 × 2.30 × 3.00 | 4.2 / 0.90 | 30–40 | **yellow `#FBC02D`** (mandated [S]) + black band | 0.25%, 06:30–09:00 and 14:30–17:00 only | no |
| `ghm_veh_taxi_small_a` | Taxi | 3.40 × 1.48 × 1.48 | 2.36 / 0.55 | 4 | white 70% / yellow `#FBC02D` 30%; black plate; roof sign `#FFEB3B` | 4% | yes |
| `ghm_veh_hatchback_a` | Hatchback | 3.77 × 1.68 × 1.52 | 2.43 / 0.58 | 5 | white 30, silver `#BDBDBD` 20, red 15, grey `#616161` 10, blue 10, black 5, orange/teal 10 | 8% | yes |
| `ghm_veh_ev_compact_a` | EV crossover | 4.10 × 1.75 × 1.58 | 2.55 / 0.66 | 5 | white, `#9E9E9E`, `#1E88E5`, `#00897B` | 3% | yes |
| `ghm_veh_suv_a` | SUV / jeep | 4.46 × 1.82 × 1.98 | 2.68 / 0.75 | 7 | white 40%, `#212121`, silver, `#6D1B1B` | 3.5% | yes |
| (pickup) | Pickup | 4.86 × 1.70 × 1.86 | 3.01 / 0.70 | 2 | white, red, blue | 1.5% | yes |
| `ghm_veh_truck_painted_a` | 2-axle truck, painted | 8.10 × 2.45 × 3.30 (headboard 3.60) | 4.80 / 1.05 | 2–3 | cab `#D32F2F` / `#1976D2` / `#FBC02D` / `#388E3C`; panels `#FF8F00` / `#00897B`; truck-art atlas | 1.0% (night ×3) | yes |
| `ghm_veh_truck_plain_a` | Tipper | 7.60 × 2.50 × 3.10 | 3.8 + 1.35 / 1.05 | 2 | cab `#FBC02D` / `#D32F2F`; box `#F57F17` | 0.4% | yes |
| `ghm_veh_tanker_a` | Water tanker | 7.50 × 2.40 × 3.00 | 4.20 / 1.00 | 2 | tank `#1E88E5` or `#FAFAFA`; generic "पिउने पानी" lettering | 0.6% (residential ×2 in the morning) | yes |
| `ghm_veh_tractor_a` | Tractor + trolley | 3.45 × 1.75 × 2.10 + 3.60 × 1.90 × 1.10 | 1.95 / 1.40 rear, 0.80 front | 1 | `#C62828` `#1565C0` `#2E7D32`, rims `#FDD835` | 0% (peri-urban 7%) | yes |
| `ghm_veh_police_jeep_a` (new) | Police jeep | 4.40 × 1.75 × 1.90 | 2.68 / 0.70 | 5 | white + `#1565C0` band; "POLICE / प्रहरी"; **no weapons** | 0.5% | no |
| `ghm_veh_ambulance_a` (new) | Ambulance | 4.70 × 1.70 × 2.10 | 2.57 / 0.66 | 2 | white + `#D32F2F` band; **no Red Cross emblem** [S ICRC] | 0.5%, no siren | no |

**Spawn mix by road class** (per 100 moving vehicles, V §2.2): trunk 55 two-wheelers / 22 car / 7 micro / 4 bus / 1 tempo / 7 truck / 1 tractor / 2 bicycle / 1 other; primary 62 / 20 / 7 / 2 / 3 / 2 / 0 / 3 / 1; secondary and tertiary 65 / 19 / 5 / 1 / 3 / 2 / 1 / 3 / 1; residential 72 / 15 / 1 / 0 / 0 / 2 / 2 / 6 / 2; old-core lanes 70 / 3 / 0 / 0 / 0 / 0 / 0 / 15 bicycle / 10 rickshaw / 2; peri-urban 60 / 12 / 6 / 3 / 0 / 6 / 7 / 6 / 0. Two-wheelers split about 35 : 55 : 3 : 7 scooter : commuter motorbike : cruiser : e-scooter (the e-scooter share rises to 8% in OLD_CORE). Riders and pillions **always wear helmets**.

**Parked vehicles** (static instances, separate from the moving caps): old core 10–25 motorbikes per 100 m (angled 60–90° to the wall), urban 5–15, peri-urban 1–4; bicycles 0.5–2 per 100 m; caps of 80 / 200 / 400 instances per tier. **Parked LODs** (the moving LODs would cost 48–240 k triangles at these counts): ≤ 20 m the moving LOD2 mesh (≈ 600 tris, rider removed), 20–80 m a 80-triangle block-out (body box, 2–4 wheel discs, seat or cabin), 80–150 m a 12-triangle box with a palette tint, culled beyond 150 m. At most 2 / 10 / 10 parked vehicles use the near LOD and 20 / 60 / 120 the block-out. That is ≈ 3.5 k / 12 k / 19 k triangles, inside the vehicle slice of §10.4. About 30% of parked vehicles carry the green community-fleet tag (W2-O5).

### 5.3 Buses on real routes

* **Data:** D2 imports the 75 bus-like route relations: 55 bus, 11 microbus, 9 tempo [O], of which about 68 serve the valley. Geometry is validated against the current road graph. Missing stops are inferred from the 380 `bus_stop` nodes within 25 m of the route, ordered along it. Only about 15 relations carry stop members [O].
* **Runner:** each route gets a headway by mode and time of day [E]: bus 8 min (peak 5), micro 5 min (peak 3), tempo 6 min, scaled by the density multiplier. Vehicles are spawned only inside the sim radius, at the position the schedule implies, so a bus always "arrives" along its route. Dwell is 8–15 s per stop. Terminals: Ratna Park / Purano Bus Park, Naya Bus Park Gongabu (w58754707), Lagankhel (w120443290), Kalanki, Koteshwor, Kamalbinayak.
* **Liveries by mode:** Sajha routes (r3100600 Kalanki–TIA, r3138552 Lagankhel–Naya Buspark, per OSM `operator`) use the generic `city_green` preset (green is the common colour of public city buses in Kathmandu; no operator's livery is reproduced); other bus relations use the minibus preset; micro relations the microbus; tempo relations (`network=safatempo`) the Safa tempo. Route boards show the **route `ref` and terminal names from OSM** in Devanagari or Latin as tagged, never an operator name.
* **Player loops** (P §6.4): ride as a passenger (bell button = stop at the next stop; skip-to-next-stop with a 1.2 s fade), or borrow a bus at a bus park and drive the route (doors open below 5 km/h in a 25 m stop zone; 3–8 NPCs board; stars for decel < 2 m/s²; no penalties).

### 5.4 Pedestrians

**Density targets** (people visible per 100 m of street, both sides, peak; S §3.1), clipped by the NPC caps and the crowd representation of ASSET_MANIFEST §9.5 (≤ 15 m skinned LOD0: 1 / 3 / 6; 15–40 m skinned LOD1: 4 / 10 / 20; 40–90 m VAT: 13 / 39 / 78 visible; dense squares VAT to 120 m on High; §10.4). These are on-screen counts: a sacred square at 150 people per 1,000 m² is simulated at the NPC cap and drawn nearest-first up to these numbers:

| Area | Peak | Off-peak | Night | Standing share |
|---|---|---|---|---|
| OLD_CORE bazaar | 60–120 | 25–50 | 0–3 | 30% |
| OLD_CORE lane ≤ 4 m | 8–20 | 4–10 | 0–1 | 40% |
| TOURIST (Thamel, Boudha kora) | 40–80 | 15–30 | 2–8 to 23:00 | 25% |
| Sacred square or compound | 50–150 per 1,000 m² | 15–40 | 0–5 | 50% (sitting on plinths) |
| URBAN main road | 15–30 | 6–12 | 0–2 | 15% |
| URBAN side street | 6–15 | 3–6 | 0–1 | 25% |
| PERI_URBAN | 4–10 | 1–4 | 0 | 30% |
| Fields | 1–4 | 0–2 | 0 | 50% |

Daily multipliers (S §14): 04:30–05:30 0.05, 05:30–08:00 0.4 (sacred 1.0), 08:00–10:30 0.8, 10:30–13:00 1.0, 13:00–15:00 0.8, 15:00–17:00 0.9, **17:00–19:00 1.0**, 19:00–21:00 0.6, 21:00–23:00 0.1, later 0.01. Rain ×0.5, with umbrellas on 30–60%.

**Archetype mix** (S §3.2, % per area: old core / tourist / urban / peri / fields / Boudha-Swayambhu / Pashupati): urban casual 40/30/52/45/25/30/35; kurta or sari 18/8/15/18/20/12/25; daura suruwal + topi 6/2/3/6/8/3/8; hakupatasi 3 (Bhaktapur 6) in Newar towns only; school kids 8/2/10/10/8/3/2; porters with doko and namlo 6/2/1/2/4/1/1; vendors 6/4/3/2/0/4/4; tourists 6/45/2/0/0/20/10; monks 1/2/0.5/0.5/0/22/0 (only within 400 m of a Buddhist site); sadhus 0 except Pashupati 6; farmers 0/0/0/6/33/0/0; traffic police only at chowks. Clothing palettes from S §3.3 as instanced tints.

**Pedestrian graph:** footpaths of the road profile (both sides), OSM footways, paths, steps and pedestrian areas, compound polygons (free-walk navmesh-lite: a 2 m grid of walkable cells built per compound), zebras and junction corners. In OLD_CORE shared streets pedestrians use the whole width and vehicles drive at ≤ 10 km/h.

**Behaviours** (VAT clips: idle, walk, carry, sit, chat, cheer, pray, pick, vendor, kite, run, namaste): namaste pair 1 per 200 m of bazaar per minute; touch-and-bow within 3 m of a shrine (15% of locals in the morning, 5% later); bell ring 1 per 20–90 s at busy shrines 05:30–09:00; **clockwise circumambulation** at stupas, chaityas and temples; sitting on pikha, plinths and chautari (20–40% of NPCs near them at midday); pigeon feeding at squares (1–3 people per flock); 8–12% of pedestrians carry a doko, sack, gas cylinder or baby. Porters walk at 0.8× and rest on a tokma every 60–120 s.

**Vendors** (S §4): fruit cart 1.8 × 0.9 × 0.9, vegetable mat, chatpate table, momo steamer stack, tea stall, corn roaster (seasonal), flower and puja-goods seller **within 50 m of every busy temple**, grain seller at the squares, pottery in Bhaktapur and Thimi. Per 100 m at peak: OLD_CORE bazaar 6–15, TOURIST 3–6, URBAN main road 1–3 (60% within 50 m of a junction or stop), PERI 0.2–1. They pack away between 20:00 and 21:30.

**Contact is never harm** (P §10): every pedestrian in a capsule of length `v·0.8 s + 2 m` and width `vehicle + 1.0 m` ahead of a player vehicle does a comic hop-aside (0.35 m up, 1.5 m sideways, 0.45 s, reaction 0.1–0.25 s, 1 in 4 spin). If contact still happens, the pedestrian becomes kinematic, bounce-hops 0.5 m up and 2 m sideways, lands on their feet, dusts off and waves; the vehicle drops to 40% speed with a squash. Crowd areas (> 0.2 people/m²) cap the player at 15 km/h.

### 5.5 Animals

| Animal | Density and rules | Source |
|---|---|---|
| Cows (humped zebu) | ≈ 1,200 street cattle in KMC [S], about 24/km²; spawn 60% on Ring Road and arterials, 30% urban, 10% old-core edges. Ring Road 1–3 per km, arterial 0.3–1, old core 0–0.3, peri-urban villages 1–3 (tethered). Lie chewing 60% of the time, stand at vegetable waste 30%, walk 10%, in groups of 1–5. Colours `#EDE7DA` `#D8C9A8` `#9D9890` `#2B2B2B`/`#EFEFEF` `#8B5A3C`. **Cushion: within 4 m of a cow the player is limited to 5 km/h; within 1.6 m a hard stop with a gentle bump. The cow keeps chewing.** | S §7.1 [S Himalayan Times] |
| Dogs | **14.2 roaming dogs per km of street** [S ICAM]: old core 12–18 per km, urban 12–16, peri-urban 8–14, villages 4–10. By day 60–70% asleep (sun patches, plinths, under bikes); at night 70% awake, with bark chains of 1–4 dogs at 1–5 s delays across 50–300 m. Singles 60%, pairs 25%, packs of 3–6 15%. Tan `#C49A6C` 40%, black `#2A2A2A` 20%, black-and-tan 15%, cream 10%, patched 15% | S §7.2 |
| Rhesus macaques | Swayambhu ≈ 450 [S]: 40–80 visible within 200 m, troops of 10–40 on roofs, rails, chaityas and trees, babies on 10%. Pashupati 20–40 visible (counts disagree [V]). Gokarna, Nilbarahi, Bajrayogini, Patan 5–20. Rim forest below 2,000 m 0.5–2 troops per km². **Never aggressive, never steal, never fed by the player**; they keep 2 m away and leap onto walls | S §7.3 |
| Goats, chickens, buffalo, ducks | Peri-urban and village yards: goats 2–8 per household cluster and herds of 5–20 on field bunds; chickens 3–12 per yard (they flap 0.6 m up when flushed; roosters crow 04:30–06:30); buffalo 0–3 per farm; ducks 3–10 per village pond. **No sacrifice depicted** | S §7.4 |

**Animal LODs and render caps** (added in review; the animal slice is 4 k / 13 k / 30 k, §10.4): cows, dogs, goats and macaques use LOD0 (2,500) for at most 0 / 1 / 2 animals within 10 m, LOD1 (1,000) for at most 1 / 3 / 6 within 30 m, VAT LOD2 (300) for at most 6 / 15 / 35 beyond, nearest first; the rest of the simulated animals are not drawn. The Swayambhu and Pashupati targets above are simulation counts; the acceptance in §1.2 checks the rendered minimum.

### 5.6 Birds

Flocks are VAT instances: 300 tris per bird only within 8 m (max 0 / 4 / 8 birds Low / Mid / High), 80 tris to 25 m (max 15 / 30 / 75), and beyond that an opaque 4-triangle **paper bird** (two quads in a V, wing flap in the vertex shader, no alpha) with no count cap. A 200-bird pigeon burst therefore costs ≈ 2–9 k triangles instead of 60 k. Caps within 90 m: 2 pigeon flocks + 3 small flocks on High, 1 + 2 on Mid, 1 + 1 on Low. Pigeons may be fed (grain from the square vendor). All other birds are for watching only.

| Species | Where | Count | Behaviour parameters [E] | Colours |
|---|---|---|---|---|
| Rock pigeon | Durbar squares, Boudha, Swayambhu, temple roofs | 30–200 per square; urban 50–150/km² | Peck in a 3–10 m disc; **burst take-off** at 3–4 m from the player or a vehicle; 1–3 laps of a 30–60 m radius circuit at 10–15 m/s; re-land in 20–40 s; boids: separation 0.6 m, alignment 3 m, cohesion 8 m | `#8A8F99`, neck `#4E7A6A`/`#7B5C8E`, bars `#2F2F35` |
| House crow | Urban wires, roofs | 20–60/km², groups of 2–15; evening roosts of hundreds (Tundikhel, Ratna Park, Narayanhiti) | Hop, perch on the top wire, fly 8–12 m/s | `#6E6E70` / `#141414` |
| Black kite | Over the city and rivers | 2–8 in any 1 km² view | Soar 08:30–16:00, circle radius 20–50 m, height 40–300 m, bank 15–30° | `#5C4632` / `#8F7558` |
| Myna | Lawns, roofs, wires | 30–80/km² | Strut walk, short flights | `#5A3E2B`, head `#1A1A1A`, bill `#F2C21B` |
| House sparrow | Eaves, shopfronts | 50–150/km², 5–30 per group | Short hops, flush together | `#8B6B4A`, crown `#8C8C8C` |
| Barn swallow | Fields and rivers, Mar–Oct | 10–40/km² | Low swoops 1–5 m up at 10–15 m/s | `#1C2A44`, throat `#A8492E` |
| Cattle egret | Paddy, behind ploughs and buffalo | 5–40 per ha at transplanting (Jun–Jul) | Walk behind cattle; V-ish evening strings | `#F7F7F2`, breeding buff `#E0B070` |

Kites (paper) in season (S §5.8): Nag Panchami to Haribodhini Ekadashi, peak in the Dashain fortnight: 100–400 per km² over the old core and urban roofs from 14:00 to 18:00 (5–30 outside the peak), 20–120 m up on catenary strings; diamond 0.4–0.6 m in flat colours; a cut kite drifts away. October 2026 is inside the peak.

### 5.7 Street dressing: poles and wires

Square-tapered concrete poles `#A8A49C`, 9 m (LT) and 11 m (11 kV), on one side of the road at a spacing of 25–35 m (old core), 35–45 m (urban) and 40–60 m (peri-urban). Cross-arms 1–2 plus an LT rack at 7–8 m. Power sag 2–3% of span; telecom bundles sag 5–8%, with **10–40 cables plus coiled slack loops every 2–4 poles in the old core and Thamel**, 5–15 urban, 2–6 peri-urban. Clearance ≥ 5.5 m over roads [S]. A transformer on a 2-pole H-frame every 250–400 m. **New Road is kept clean** (cables bundled 2023–2025 [S]). Crows and pigeons perch on the top line. Real OSM pylons (629 in the pack) and lines are placed first (D3); generated poles are labelled procedural.

### 5.8 Trees and forests

**Placement order:** (1) every OSM tree from `PROP` at its position (1,132 in the valley box, about 50 named pipal or bar [O]); a chautari flag gives a stone platform 0.45–0.9 m high with a porter ledge at about 0.9–1.0 m [E]. (2) Avenue rows on URBAN arterials without OSM trees: 30% of segments, spacing 8–15 m. (3) Forest scatter inside real FOREST areas and BIOM forest, masked by roads, buildings and water.

| Species | Where | Size [E] | Look and season (hex) |
|---|---|---|---|
| Pipal *Ficus religiosa* | Chautari, temple courtyards (OSM only) | 15–25 m, crown 15–25 m | `#4E8A3A`; copper flush `#C27C5E` in Mar–Apr; trunk `#9A948A` |
| Bar *Ficus benghalensis* | Chautari, with pipal (OSM only) | 15–20 m, very wide, aerial roots | `#2F6B2F` |
| Jacaranda | Avenues: Tundikhel, Durbar Marg, Lazimpat, Maharajgunj, Baluwatar, Kamaladi | 8–15 m, umbrella | **Violet `#8E6CC8` / `#A58AD8` from March to early May**, with a petal decal `#9C7FD0` under the tree [S] |
| Silky oak *Grevillea* | Older roads, Rana avenues, Bagmati corridor | 18–30 m, narrow | Golden comb flowers `#F2A33A` in Apr–May |
| Bottlebrush | Medians | 4–8 m, weeping | Red `#D7263D` in Mar–May |
| Camphor, eucalyptus | Bagmati corridor, campuses | 10–35 m | `#3F7F3A`; eucalyptus `#7FA08A` with trunk `#D9D2C3` |
| Bamboo clump | 1 per 2–5 peri-urban and village houses | Ø 3–6 m, 8–15 m tall | `#6FA03A`; yellow-culm variant `#D8C24A` (Garden of Dreams) |
| Banana, poinsettia (lalupate), bougainvillea | Gardens and compound walls | 3–5 m; 1.5–4 m shrubs; climbers | `#7DBA3A`; red bracts `#D11F2A` Nov–Feb; magenta `#C2185B` on 20% of urban compound walls |

Avenue mix: north and central districts are jacaranda 45, silky oak 20, bottlebrush 15, other 20 (%); elsewhere jacaranda 20, bottlebrush 25, silky oak 15, ficus 15, other 25. **No street trees in OLD_CORE** (too narrow).

**Forest bands** (S §11 [S Shivapuri NP, Phulchoki zonation]):

| Band | Elevation | Mix (%) | Colour |
|---|---|---|---|
| Valley fringe and sacred groves | 1,300–1,400 m | Schima, Castanopsis, figs; Alnus by streams | `#4C8B3E` |
| Schima–Castanopsis | 1,400–1,800 m | Schima 40, Castanopsis 30, Alnus 10, others 20 | `#4A7F36`; Schima white flowers `#F4F1E6` May–Jun |
| Chir pine | 1,400–2,000 m on dry **south-facing slopes** (aspect 135–225°: 60% of the broadleaf weight swaps to pine) and plantations (Nagarjun, Chandragiri, Kirtipur) | Pinus roxburghii 70–100 | `#4F7A3A`, litter `#A9773F`, bark `#7A4A2E` |
| Oak–laurel + rhododendron | 1,800–2,400 m | oaks and laurels 60, Rhododendron arboreum 20, bamboo 10, others 10 | `#3B6B34`; **rhododendron red `#C8102E` Feb–Apr** |
| Brown oak | 2,400–2,760 m | Quercus semecarpifolia 70–90 | `#6B7A4A`, mossy trunks |

**Instancing and budgets** (W2 vegetation slice 18 k / 54 k / 100 k tris, §10.4; ASSET_MANIFEST §1.4 had 20 / 60 / 110 k. The earlier caps here multiplied out to 32 k and 62 k on Low and Mid and were corrected in review): cartoon trees are 3 shape families (round broadleaf, cone conifer, umbrella) × 4 sizes. Clumps of 3–7 trees are merged into one mesh per clump. Visible density near the camera: 150–300 trees/ha in closed broadleaf, 80–150 in pine, 30–60 in degraded forest.

| Band | Low | Mid | High |
|---|---|---|---|
| LOD0 full tree (≤ 600 tris) | ≤ 25 m, max 12 trees (≈ 7 k) | ≤ 40 m, max 33 (≈ 20 k) | ≤ 60 m, max 60 (≈ 36 k) |
| LOD1 (≤ 120 tris) | 25–120 m, max 65 (≈ 8 k) | 40–180 m, max 200 (≈ 24 k) | 60–250 m, max 400 (≈ 48 k) |
| Impostor quads (2 tris, 8-direction atlas) | to 750 m, max 1,500 (≈ 3 k) | to 1.25 km, max 4,000 (≈ 8 k) | to 1.75 km, max 6,000 (≈ 12 k) |
| Canopy shell (mid ring, per BIOM forest cell) | merged per tile | merged per tile | merged per tile |

Placement uses `SEED` = FNV-1a(tile_key, ruleset) (D10), so trees stay put between builds. Wind sway runs in the vertex shader (0.3–0.6 Hz, amplitude ∝ height).

---

## 6. Player

### 6.1 Character (P §2)

A chunky, age-open young explorer (reads as 16–25): **1.55 m barefoot, 3.9 heads tall** (head 0.40 m), shoulders 0.42 m, hips 0.34, legs 0.63 (thigh 0.30, shin 0.27), hands 0.13 × 0.09 (mittens + thumb), feet 0.27 × 0.11 × 0.09, eyes 0.055 m tall (14% of the head). There are 4 builds (A slim 1.52, B average 1.55, C sturdy 1.56, D tall 1.66) with no gender gate on outfits, 10 numbered skin swatches (`#F3D3B5` to `#5C3826`, the picker opens on a random one, never #1), 12 hairstyles × 8 colours, and outfits as parameter recipes: dhaka topi (asymmetric, front 0.11 m and back 0.075 m, tilted 6°), dhaka jacket, kurta, daura suruwal (**8 ties = 4 pairs, 5 pleats**, closed round neck), jeans, T-shirt, hoodie, sneakers, chappal, trek boots, sari, and helmets auto-equipped on every two-wheeler mount (8 colours). The mesh is built from **22 parametric primitives** (superellipsoid head e = 2.4, tapered capsules, lofted torso, offset garment shells) skinned to the existing 37-bone `hum` rig, at **≤ 5,000 tris + 600 accessories**; LOD1 2,000. No alpha clipping. Joints get 1.15× radius bulges. Brands are not allowed. A soft ground ring (`#FFF3D6` at 40%, Ø 0.9 m) appears under the idle player in crowds.

**Animation:** about 70% procedural. A phase-driven gait (step = `0.42·legLen + 0.18·v`; 2.9 steps/s at 1.6 m/s; walk-run blend at 1.6–2.0 m/s, matching a Froude walk-run switch at about 1.76 m/s for l = 0.63 m [C]). Analytic two-bone IK for feet and hands. Second-order springs (f, ζ, r): topi 3.5 / 0.45 / 0, braid 2.4 / 0.35, backpack 2.0 / 0.5, hem 2.2 / 0.5, head look-at 2.5 / 0.9 / −0.2. Volume-preserving squash, clamped to s ∈ [0.72, 1.18]. About 30% are key poses as JSON at 30 fps: namaste 40 frames (palms at the sternum, head bow 15° + spine 8°; for elders hands +0.05 m and bow 22°), wave 30, sit_chautari 90 L, 12 mount clips. Fidgets come after 6 s idle; inside SACRED areas only "hands-together rest" and look-around play (§6.6). Player animation cost ≤ 0.15 ms per frame on Low.

**On foot:** walk 1.6 m/s, run 4.5, sprint 6.0 (existing `VehicleSpec.Walker`). Jump apex 1.0 m in 0.32 s, coyote time 0.10 s, jump buffer 0.12 s. Auto step-up 0.45 m (pikha aprons, temple steps), mantle up to 1.3 m. No fall damage: a fall over 4 m becomes a roll and a "ta-da".

### 6.2 Enter and exit any vehicle (W2-O5)

| Step | Rule |
|---|---|
| Detect | Seat sockets in a 120° cone: **2.0 m** for two-wheelers, **3.0 m** for cars, **3.5 m** for bus and truck doors. Score = 0.6·(1 − d/range) + 0.4·cos(angle); 0.3 m hysteresis |
| Prompt | The Action button morphs from Jump to **Hop on** (class icon) in 0.12 s; a second small "Ride as passenger" button appears where a passenger seat exists. Hold E or A for 0.35 s for passenger |
| Approach | Auto-walk ≤ 4 m at 2.2 m/s to the side-specific socket; abort after 0.5 s blocked |
| Sides (left-hand traffic, right-hand drive) | Two-wheelers from the left; **car and taxi driver through the right door**, passengers through the left rear door (kerb side); micro and tempo through the side door or rear step; bus at the front-left door; truck at the right cab door; tractor from the left over the axle step. If a wall is within 0.8 m, use the other side |
| Durations (enter / exit, s) | bicycle 0.5 / 0.35; scooter 0.6 / 0.45; motorbike 0.7 / 0.5; car 0.9 / 0.7; micro 1.0 / 0.8; bus 1.2 / 0.9; truck 1.4 / 1.0; tractor 1.0 / 0.7; rickshaw 0.8 / 0.6 |
| Cancel and exit | Any move input in the first 60% of an enter cancels it. Exit auto-brakes below 8 km/h first (bus and truck to 0), then plays a comic hop-off. The player is placed at the exit socket, snapped to ground, trying the opposite side and then behind (radius 2.5 m) |
| Camera | Walk-to-vehicle blend 0.45 s ease-in-out; exit 0.35 s |
| Where vehicles come from | Own garage (whistle summon: the vehicle arrives within 20 s from a road node ≥ 60 m away, never popping into view); community fleet (green tag, ~30% of parked vehicles, auto-return after 10 min or 150 m); hail taxis (hold Action 0.4 s, the nearest free taxi within 150 m stops within 30 m); buses, micros and tempos at stops; rentals in Thamel |
| Sacred compounds | The Hop on prompt becomes "Vehicles rest outside"; two-wheelers auto-park at the gate |

### 6.3 Handling per class

Speed-sensitive steering: `δmax(v) = min(δ0, atan(L·aLat / v²))`. Lean `θ = atan(v²/(g·R))`, capped at 38° [S arXiv 1611.03857]. Visual body roll 0.6°/(m/s²) for cars and 1.0° for buses and trucks (caps 4° / 6°); pitch 0.8°/(m/s²). Physics stays flat. Existing values are marked [R].

| Class | Top km/h (boost) | Accel m/s² | Brake | Wheelbase | δ0 | Steer °/s | Min R front axle (m) | aLat | Lean / roll | Notes |
|---|---|---|---|---|---|---|---|---|---|---|
| Walker | 16.2 / 21.6 [R] | 12 [R] | 16 [R] | — | — | 300 yaw [R] | 0 | — | ±15° | |
| Bicycle | 25 (30 with stamina) | 1.4 | 4.0 | 1.12 | 40° | 200 | 1.74 | 6 | lean 30° | Stamina drains 0→100 in 12 s and refills in 8 s; push the bike at 1.2 m/s above 10% grade; −1.5 km/h per % above 6% |
| Scooter | 70 (84) | 5.0 | 7.0 | 1.26 | 38° | 220 | 2.05 | 9.5 | 32° | CVT smooth; small-wheel wobble ×1.3 |
| Motorbike | 85 (102) [R] | 6.5 [R] | 8 [R] | 1.3 [R] | 35° [R] | 220 [R] | 2.27 | 10.5 [R] | 38° [R] | 4 audible gear steps; drifts on DIRT and MUD |
| Cruiser | 100 | 5.5 | 8 | 1.39 | 33° | 180 | 2.55 | 9.5 | 35° | Lean spring f 1.6 |
| Taxi / small hatch | 75 [R] | 4.2 [R] | 7 [R] | 2.36 | 34° | 120 [R] | 4.2 | 8.5 [R] | roll 4° | Opt-in drift: brake + steer above 30 km/h gives rear grip 0.6× for 0.8 s with 50% counter-steer assist |
| Hatchback / EV | 90 | 4.5 (EV 5.2 below 30) | 7.5 | 2.43–2.55 | 34° | 130 | 4.35–4.6 | 8.5 | 4° | |
| SUV / jeep | 90 | 3.8 | 7 | 2.68 | 33° | 110 | 4.9 | 7.5 | 5° | DIRT grip +10% |
| Microbus | 80 | 2.8 | 6.5 | 2.57 | 34° | 100 | 4.6 | 6.5 | 5° | |
| Safa tempo | 40 | 2.2 | 5 | 2.10 | 38° | 110 | 3.4 | 5 | 6° tilt, never tips | |
| Minibus | 65 | 1.6 | 5 | 3.8 | 38° | 70 | 6.2 | 4 | 6° | |
| City / tourist bus | 60 (80 highway) | 1.1 | 4.5 | 5.4 / 5.6 | 42° | 55 | 8.1 / 8.4 (body sweep ≈ 10.5–11, inside the 12.5 m bus circle [S]) | 3.5 | 6° | 0.25 s steering delay; rear overhang 2.6 m swings out 0.6 m; kneel hiss at stops |
| Truck / tipper / tanker | 55 (70 highway) | 1.0 (loaded ×0.8) | 4.0 | 4.8 | 40° | 60 | 7.5 | 3.5 | 6° | Engine-brake 1.2 off throttle; tanker slosh (body spring f 0.8, ζ 0.3) |
| Tractor + trolley | 30 | 1.5 | 4 | 1.95 | 38° | 90 | 3.2 | 3 | 5° | Trailer hitch 1.2 m behind the rear axle |

Two-wheelers never fall over: wobble ±3° at 1.2 Hz below 5 km/h, a foot goes down below 1 km/h, and the lean leads the yaw by 0.08 s.

### 6.4 Cameras

Distance ≈ `6.0 + 1.0 × vehicle length` (landscape). Portrait adds 10–15% distance, 8–10° of pitch and twice the look-ahead time. Every rig keeps the minimum horizontal FOV (walk 55°, drive 62°, bus and truck 64°; vertical FOV clamped to 100°).

| Rig | Landscape d / pitch / pivot | Portrait d / pitch / pivot | Look-ahead L / P (s, max m) | Follow (s) | Speed FOV kick |
|---|---|---|---|---|---|
| Walk | 8.4 / 12 / 1.0 [R] | **6.8 / 20 / 0.9** (W2 change from 8.6 / 22 / 1.0: the player grows from ≈ 187 px to ≈ 240 px on a 1080 × 2340 portrait phone) | 0.3, 2.5 / 0.6, 4.5 [R] | 0.9 / 1.0 [R] | — |
| Bicycle | 7.5 / 12 / 1.0 | 8.5 / 20 / 1.0 | 0.4, 6 / 0.8, 10 | 0.30 | +3° |
| Scooter / motorbike | 8.0 / 13 / 1.0 [R] | 9.2 / 21 / 1.0 [R] | 0.45, 9 / 0.9, 16 [R] | 0.28 / 0.32 [R] | +6° above 60 km/h |
| Car / taxi | 9.5 / 14 / 1.3 | 10.8 / 22 / 1.3 | 0.5, 12 / 1.0, 20 | 0.35 | +5° |
| SUV / micro / tempo | 10.5 / 15 / 1.6 | 12.0 / 23 / 1.6 | 0.5, 12 / 1.0, 20 | 0.40 | +4° |
| Bus | 16.5 / 17 / 2.6 | 18.5 / 25 / 2.6 | 0.6, 18 / 1.2, 28 | 0.60 | +2° |
| Truck / tanker | 14.5 / 16 / 2.4 | 16.5 / 24 / 2.4 | 0.6, 16 / 1.2, 26 | 0.55 | +2° |
| Tractor | 10.0 / 18 / 1.8 | 11.5 / 26 / 1.8 | 0.3, 6 / 0.6, 10 | 0.50 | — |
| Passenger ride-along | Orbit at 1.2× the driver rig, auto-yaw 6°/s toward points of interest within 300 m | same | — | — | — |

Collision: sphere cast r = 0.3 m, min distance 2.5 m (walk) / 4 m (vehicles); pulls in at 20 m/s and eases out over 0.6 s. **Lane mode:** walls within 3 m on both sides → pitch +15°, distance −15%, blend 0.5 s. Eaves, struts and wires between the camera and the player dither out (opaque dither) within 1.2 m of the player on screen. Shake = trauma² × Perlin, decaying 1.5/s, clamped at 0.25 m / 1.5°, triggered only by landings, bumps and the honk bounce.

### 6.5 Touch controls (pt ≈ dp; Apple 44 pt and Material 48 dp minimums, 8 pt gaps [S])

| Orientation | On foot | Vehicle |
|---|---|---|
| **Portrait, one thumb** | Floating stick (ring 120 pt, knob 56 pt) anywhere in the lower 45% left of the button column; dead zone 12%; walk below 60% deflection, run above, sprint by holding 100% for 0.6 s. **Action 72 pt** at (84%, 14%) morphs Jump / Hop on / Sit / Namaste. Emote wheel 52 pt at (84%, 27%), 6 slots, namaste on top. Camera look: one-finger drag in the upper 55% (0.25°/pt yaw, 0.15°/pt pitch) | **Throttle 88 pt (hold)** at (84%, 12%), slide up 30 pt = boost. **Drag-steer** anywhere in the lower 45% left of the throttle: full lock at 70 pt, response exponent 1.6, recentre over 0.12 s. Brake / reverse 64 pt at (66%, 8%) (reverse after 0.4 s stopped). Horn 52 pt at (66%, 22%). Hop off 52 pt at (92%, 30%). Cruise assist 44 pt toggle under the minimap |
| **Landscape, two thumbs** | Floating stick in the left 40% (ring 140 pt). Action 80 pt at (88%, 18%), emote 56 at (76%, 12%), sprint 56 at (92%, 38%), camera drag in the right upper 60% | Steer: X-stick or two 72 pt arrow pads (option). Throttle 88 at (88%, 18%), brake/reverse 72 at (74%, 12%), boost 56 at (90%, 42%), horn 52 at (76%, 32%). Bus and truck add Doors/Stop 56 pt at (12%, 60%) at stops |
| Options | Auto-throttle; tilt steering (±25° roll = full lock, 3° dead zone, off by default); left-handed mirror; hold-to-toggle for throttle and sprint; steer assist (`RoadAssistPerS` ×1.5). Haptics: light on landing from ≥ 1 m, medium on bumps, a selection tick when Hop on appears. Touch-to-response ≤ 50 ms | |

### 6.6 Calm mode and contact rules

Inside `SACRED` areas: walking pace capped at the run speed (no sprint), no comic fidgets, no running emotes, no "boing" accents, and an optional shoe-rack prompt where curated. The player may ring a shrine bell once per 3 s, at most 3 per visit. Sitting faces the shrine with the feet never pointing at it [V]. Rule-breaking in traffic (wrong side, running a police chowk) gets only a whistle and a "Bistārai!" bubble: no wanted level, no fine, no chase. Vehicle against vehicle is a bumper-car bounce (impulse 0.5 × closing speed); against walls, poles and temples a squash of 0.85 and a star puff. Water bounces the player back to the shore.

---

## 7. Audio

### 7.1 System

* **Engine-free synth core** (`Core/Synth/`, Track C): phase-accumulator oscillators (PolyBLEP saw and square, a 2,048-entry sine table), noise (xorshift32 white, Kellet pink, brown), RBJ biquads with coefficients smoothed over 5 ms, a modal bank, a Hann grain player and `EngineVoice`. No allocation in the hot path. Golden tests hash the output per seed.
* **Real-time voices** use Unity 6.3's scriptable audio pipeline (`IAudioGenerator`), with `OnAudioFilterRead` on a looping 1 s constant-1.0 clip as the fallback, so spatialisation still applies [V device test of both]. Main thread → audio thread: a 64-byte parameter struct per voice with a sequence counter, ramped linearly over each buffer.
* **Baked bank:** one-shots are rendered at region load on a worker into PCM (32 kHz; small bells and birds 22.05 kHz) and turned into clips with `AudioClip.Create` + `SetData` in slices of ≤ 2 ms per frame. That is about 210 variants and ≈ 6 MB (≈ 3 MB on Low). Seeds come from `SEED`.
* **Voices** (Low / Mid / High): real 24 / 32 / 48, of which real-time synth 4 / 8 / 12 (the player's engine + the nearest NPC engines); virtual 128 / 256 / 512. Priorities: UI, player vehicle and player footsteps 0–16 (never stolen); great bells and conch 32; ambience beds 40; horns within 30 m, air brakes within 20 m and aircraft 64; NPC engines 96 + 4 × rank; animals, birds and shrine bells 128; NPC footsteps, shutters and cookers 192. One-shots estimated below −45 dBFS at the listener are not started. Same-sound caps: horn 3, bell 3, crow 2, bark 2.
* **Distance:** custom rolloff, 1/r to 0.7 × max then a fade to 0. Max distances (m): player footsteps 15, NPC footsteps 12, bicycle 25, two-wheelers 60, cars 80, bus and truck 150, horns 200 (moto 120), shrine bell 40, great bell 400, conch 150, dog bark 250, helicopter 4,000, ATR 6,000, A320 9,000. **Air absorption** as a one-pole low-pass: 11 kHz at 200 m, 7 at 500 m, 4.5 at 1 km, 3 at 2 km, 1.8 at 4 km, 1.1 at 8 km.
* **Doppler in our own code:** `dopplerLevel = 0` on every AudioSource. Pitch = `(c + v_listener·n) / (c − v_source·n)` with c = 343 m/s, from **simulation velocities** (floating-origin rebases would otherwise spike), clamped to 0.7–1.4 and smoothed over 50 ms.
* **Listener** 35% of the way from the camera to the player's head. Rotating the device changes nothing.
* **Occlusion:** for the 8 most important 3D voices, one round-robin raycast each every 250 ms against the B1/B2 building colliders (hit → −6 dB, LPF 1.5 kHz, 200 ms ramp). A street-graph proximity test (not connected within 2 hops or 40 m → occluded). **Snapshots:** Courtyard (street −9 dB, LPF 2.5 kHz, RT60 0.7–1.0 s), DurbarSquare (RT60 1.2 s, pre-delay 60 ms), Galli slapback (delay `2w/343` s, so 17 ms for a 3 m lane and 35 ms for 6 m; feedback 0.15, wet −14 dB), VehicleInterior (exterior −8 dB, LPF 3 kHz).
* **Mixer:** Master (limiter −1 dBTP, about −16 LUFS integrated) → Music, UI, Stings, World {Ambience, Sacred, Traffic, Player, Nature, Aircraft}, Voice. Level targets at 10 m: player engine −14 dBFS RMS, NPC motorbike pass-by −20, bus −16, horn tap peaks −10, shrine bell −18, great bell −12, crow −24, beds −24, footsteps −22. Ambience ducks −3 dB during a flyby within 1 km.
* **Phone speakers:** every low source carries harmonics 2–8 (missing fundamental). Output setting Auto / Speaker / Headphones; the Speaker profile adds HP 120 Hz, tanh drive 1.3 on Traffic and Player, and +2 dB at 2 kHz. Bed stereo width ≤ 70%. Thermal step-down halves the synth voices.

### 7.2 Ambience zoning

Zone weights come from the 250 m area-type grid, interpolated at the listener, with a crossfade of 3 s walking and 1.5 s driving. Polygons override: OLD_CORE, URBAN, PERI_URBAN, FIELDS, FOREST, TEMPLE_COMPOUND, STUPA_KORA, DURBAR_SQUARE, GHAT, RING_ROAD and ARTERIAL, AIRPORT (aerodrome + 3 km), PARK, WATER, HILLTOP (> 1,600 m). Time bands and multipliers per A §7.2; seasons per A §7.3 (monsoon: rain on tin, frogs, mud footsteps, rivers +6 dB; autumn: +20% audible range; winter fog −3 dB on distant layers).

### 7.3 Sound list (method and licence)

Methods: **P** procedural real-time; **B** procedural, baked at load; **R** CC0 recording; **H** hybrid. Every **R** file needs a LICENSES.md row: source URL, author, licence text, download date, and a saved copy of the page. Allowed sources: Freesound **CC0 filter only**, Kenney audio packs (CC0 [S]), BigSoundBank (CC0 1.0 [S]), OpenGameArt items licensed exactly CC0, Wikimedia Commons PD or CC0 files, US federal PD works (checked per file). **Excluded:** Sonniss GDC bundles (proprietary EULA [S]), Pixabay, Mixkit, Zapsplat, BBC RemArc, xeno-canto (mostly NC), any CC-BY or NC file, and YouTube recordings.

| Group | Sounds | Method | Key parameters |
|---|---|---|---|
| Footsteps (10 sets × 6 walk + 6 run + land + scuff) | asphalt/concrete, **brick** (10% loose "clink"), **stone** (squares, plinths, Swayambhu stairs), gravel, dirt, **mud** (monsoon: DIRT→MUD; lift squelch 120–200 ms after contact), grass, **wood** (dabali, balconies), **metal** (Bailey bridges, grates), water/puddle; barefoot only in curated shoes-off zones [V] | R (gravel and grass may be H) | Triggered by gait events; pitch ±4% walk / ±6% run; no immediate repeat; chappal adds a "flap" click |
| Two- and three-wheelers | `moto_commuter` 1-cyl 1,400–8,500 rpm (11.7–71 Hz firing); `moto_sport`; `moto_cruiser` 900–5,500 rpm, a 30% duty "dug-dug"; `scooter` CVT (rpm jumps to ~5,500 then holds), belt whine 600–1,200 Hz at −24 dB; `moto_2stroke` ≤ 2% of bikes; e-scooter whine 150–900 Hz; **Safa tempo** DC whine 200→1,500 Hz over 0–45 km/h with a 300 ms lag + body rattles; tractor 1-cyl diesel | P | Firing frequency `rpm/60 × cyl / 2` (4-stroke); 8–16 harmonics with load tilt; jitter; pipe band-pass (bike ≈ 105 Hz); tanh drive 1.3–2.5 (A §4.2 preset table) |
| Bicycle | Freewheel 36–48 clicks/s at 15 km/h (2 ms noise burst, BP 3–5 kHz Q 4); chain 40–66 Hz at −30 dB; tyre pink noise ∝ v^1.5; **bell** (partials 1.00 / 2.76 / 5.40 / 8.93 × f0, f0 2.3–2.8 kHz, double strike 70–90 ms apart); rim squeal on 30% of stops | B + P | |
| Cars | `car_3cyl` (taxis: 800–6,000 rpm, 1.5-order burble); `car_4cyl`; `suv_diesel` (injection ticks −12 dB, turbo 2–4 kHz); `car_ev` (hum below 20 km/h, then tyres and wind); microbus diesel + the sliding-door "thunk-clack" (R); indicator 1.5 Hz; key start; comic gear grind on jeeps | P (+R doors) | |
| Buses and trucks | `bus_city` / `truck` 6-cyl 600–2,600 rpm (30–130 Hz) + turbo whistle; `bus_ev` whine + compressor; **air-brake "pssht"** (white noise, HP 1 kHz, peak 4 kHz +6 dB, τ 0.15–0.4 s, 4 variants); parking brake +6 dB 0.8–1.4 s; compressor chug 8–12 Hz; exhaust-brake "blat"; reverse alarm 1.0–1.4 kHz 0.5 / 0.5 s; conductor's double palm slap 150–250 ms apart [V] + a non-verbal call | P + B (+R slap) | |
| Tyres | Asphalt pink noise BP 300 Hz–2 kHz; brick + 18 Hz rumble at 15 km/h (speed / 0.23 m); cobble rattle (speed / 0.4 m); gravel 200–800 grains/s; planks; Bailey grate ring; skid 600–1,800 Hz (never with impact sounds) | P | |
| Horns | moto 400–450 Hz square; scooter 480–520; car two oscillators ≈ 420 + 510 Hz; bus 290 / 345 / 435 Hz; tempo 600–700 Hz double beep; musical truck horn **highways outside the valley only**, original melodies | B | Per the honk model in §5.1 |
| People | Crowd walla at 3 densities (no intelligible words; CC0 texture grains or formant babble F1 300–800, F2 900–2,400 Hz, 6–40 talkers); crowd footstep granular stream (≤ 40/s); **shutter** (1.2–2.0 s ratchet, 25–40 impulses/s through a 180 Hz comb + a clang; 08:30–10:00 and 19:30–21:30); **pressure-cooker whistle** 2–3 kHz, 1–3 s (07:00–09:30, 18:00–19:30); vendor calls (generic "aau aau"); tea glass clinks; kids' laughter after 15:00 | H, B, R | |
| Sacred | Hand bell ghanti (partials 1.00 / 2.32 / 4.25 / 6.63, f0 1.8–3.2 kHz, clapper 4–8 Hz); **hung shrine bell** (hum 0.5, prime 1.0, tierce 1.19, quint 1.5, nominal 2.0, 2.5, 2.66, 3.0, 4.0 × f0; f0 300–900 Hz; T60 hum 6–10 s; doublets ±0.15%); **great Taleju bells** at the three Durbar squares (f0 150–300 Hz, T60 12–20 s, real-time modal voices, schedule [V]); conch (f0 250–450 Hz, formant 0.8–1.2 kHz, 2–4 s, pitch −5–10% at the end); jhyali cymbals; prayer-wheel creak + one bell per revolution; bhajan as **instrumental only** (harmonium drone, madal, jhyali) 18:00–20:30; dungchen drone near gompas [V]; **no recorded chants, mantras, azan or church bells** | B, P | Player bell: 1 per 3 s, max 3 per visit |
| Birds and animals | House crow (glottal pulse 25–40 Hz, formants 700 Hz / 1.5 kHz); pigeon coo + **flock take-off** (40–200 wing claps over 0.6–1.5 s); myna phrases; sparrow chirps 3–6 kHz; black kite whistle 4.5→2.5 kHz with an 8–12 Hz trill; koel (Mar–Aug); Indian cuckoo; bulbul; egret croak; **dog barks** (night chains); cow moo + bell; macaque chatter (R CC0 or B); rooster; goat, buffalo, chicken; crickets 4.5 kHz; cicadas; monsoon frogs | B (macaque R) | |
| Weather and environment | Wind (pink noise, 2 band-passes, gust LFO 0.03 / 0.11 / 0.27 Hz); leaves; aeolian wire whistle (`0.2·v/d`); rain (Poisson drops); **rain on tin roofs** (drop click 2–6 kHz + 2–3 modal partials 0.9–3 kHz, density from the CGI-roof share within 30 m); gutters; distant thunder; hiti splash; rivers (R CC0 + P chirps); distant city hum (brown noise 40–400 Hz); soft distant firecrackers at Tihar only | P, B, R | |
| Aircraft | §8.6 | P | |
| UI and foley | Squash "boing" (FM 220→440 Hz), jump whoosh, helmet pop, mount "hup", cloth rustle | B | |

**Not in W2:** sirens, crash or injury sounds, animal distress, gunfire-like sounds, loudspeaker music, real route or brand names.

### 7.4 Budget

About **7.6 MB on disk** (footsteps 0.46, foley 0.40, 13 ambience beds 6.4, crowd grains 0.14, animal recordings 0.21; everything else is procedural). Resident ≈ 14.5 (Low) / 20 (Mid) / 26 (High) MB including 3 MB of music. That fits the ≤ 12 MB M1 audio download and the 25 / 40 / 50 MB tier caps. Proposal to the manifest owner: footsteps and short SFX as ADPCM "Compressed in Memory" (≈ 3.5:1) instead of "Decompress on Load".

---

## 8. Aviation at TIA

### 8.1 Airport geometry (AV §1)

| Item | Value |
|---|---|
| Runway | 02/20, **45 m wide**, paved 3,326 m [O]: south displaced section 285 m + 2,764 m + north displaced section 277 m; true heading **022.0° / 202.0°** |
| Thresholds | 02: 85.3534132, 27.6839528, 1,315 m; 20: 85.3639103, 27.7070042, 1,337 m. Pavement ends 85.3523298, 27.6815732 / 85.3649612, 27.7093117. Gradient about 0.8% up to the north |
| Runway mesh | Flattened corridor: the runway profile is a straight line between the threshold elevations (displaced sections continue the slope); conformance band 45 m + 2 × 30 m graded strip with a falloff (1.3 near-field conformance) |
| Markings | Threshold bars, designators "02" and "20", centreline (30 m dashes / 20 m gaps), touchdown zone, displaced-threshold arrows; taxiways 23 m wide, yellow centrelines |
| Lights | HIRL edge lights, white at a 60 m spacing with the last 600 m yellow [E]; threshold green / end red; **HIALS 870 m** on 02 (end ≈ 85.3497, 27.6767, among the Koteshwor houses); PAPI-L 3.0°; taxiway edges blue; apron masts 25–30 m warm white; tower beacon white/green [V] |
| Aprons (AreaKind APRON) | International w736369689, 10.7 ha (699 × 189), gates 1–11; domestic w736369694, 3.0 ha, stands D1–D7 and D15–D17; maintenance w340948578; military w220704670 (static, no military aircraft); east w912639950; new north apron w1368706500, 11.6 ha (**not** a building: CONTENT_COVERAGE S11) |
| Buildings | International terminal w327224048 (212 × 102 m; real height ~15–20 m [V], OSM says 5); ATC tower w340948572 (8 × 8 m, ~30–35 m [V]); domestic terminal w185408474 |
| Access | Airside fenced, not drivable (ASSET_MANIFEST); viewing from the fence, the Ring Road, Sinamangal, Gaushala, Pashupati's east bank and rooftops |

### 8.2 Schedule (AV §4)

Real 2025 totals: **138,145 movements** (≈ 378/day; 95 international) [S]; open **06:00–24:00** since 1 April 2025 [S]. Movements per local hour, both directions:

| Hour | 06 | 07 | 08 | 09 | 10 | 11 | 12 | 13 | 14 | 15 | 16 | 17 | 18 | 19 | 20 | 21 | 22 | 23 | 00–05 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Domestic turboprop / STOL | 16 | 22 | 22 | 20 | 18 | 17 | 16 | 16 | 16 | 16 | 15 | 13 | 9 | 4 | 0 | 0 | 0 | 0 | 0 |
| Helicopter | 6 | 7 | 7 | 6 | 5 | 4 | 4 | 3 | 3 | 2 | 2 | 1 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| International narrow-body | 2 | 3 | 4 | 5 | 6 | 7 | 7 | 7 | 6 | 6 | 5 | 5 | 5 | 5 | 5 | 4 | 3 | 2 | 0 |
| International wide-body | 0 | 0 | 0 | 0 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |

**Clock rule:** the game hour picks the row, but the rate is applied **per real hour** (`λ = rate / 3600 × month × weather` per real second), because one game hour lasts 2 real minutes. At the 08:00 peak that is a movement every ~110 real seconds. A "busy airport" setting goes up to ×2.0. Month factors: Oct–Nov 1.10, Mar–May 1.05, Dec–Feb 0.95, Jun–Sep 0.85. Winter fog days (probability 0.4 in Dec–Jan): domestic and helicopter rates ×0 until 10:00. Monsoon storms 14:00–18:00: domestic ×0.7, helicopters ×0.4. Runway separation: 100 s after a turboprop or narrow-body, 120 s after a wide-body. Helicopters lifting off the domestic apron are separated by 60 s. Seed `hash(regionSeed, dayIndex, realMinute)` (replayable).

**Runway in use:** 02 from 06:00 to 12:00. From 12:00 to 18:00, runway 20 for domestic visual arrivals and all departures with probability 0.35 in March–May and 0.15 otherwise. **Jets always land on 02.** A go-around (2% of jet arrivals in Mar–May, 0.5% otherwise) flies the missed approach to DARKE, holds 1–2 laps and re-approaches. Departures on 02 enter mid-field and **backtrack ~1.2 km** to the threshold (there is no full parallel taxiway [S]).

### 8.3 Paths (WGS84 polylines with altitudes in m MSL; AV §5)

Procedures: **A1** straight-in to 02 from the south (ROMEO FL150 → RATAN → GURAS D17 3,505 m → SIERRA D10 2,896 m, ≈ 650 m over the Bhattedanda crest → **5.5°** D9–D3 → **3.0°** to the threshold, over Thecho at 250–300 m AGL); **A2** curved RNP-style variant through the Bagmati gap at 2.8° [E/V]; **A3** VOR/DME-A from DARKE (3.6°) + a visual circle in the south-east quadrant; **A4** VOR/DME-B from IGRIS + a circle; **A5** domestic visual circuit to 20 [E]; departures **D1** (02 west, the DARKE 1D double loop on the 4 DME arc, r ≈ 7.4 km, over Maharajgunj and Swayambhu), **D2** (02 east, IGRIS 1B), **D3** (20 west), **D4** (20 east); helicopter corridors **H-E** (Thimi north → Nagarkot–Banepa ridge), **H-N** (Gokarna → Sundarijal → Shivapuri), **H-W** (Thapathali → Kalanki → Thankot), 300–600 m above the floor; holding racetracks at DARKE and IGRIS (1-minute legs ≈ 5.6 km, turn radius ≈ 1.9 km, levels 3,200 / 3,505 / 3,810 m). Procedure weights per class are in AV §4.6. The point tables in AV §5 are imported **unchanged** into `kathmandu_valley.aviation.json` (§9.5). Paths are centripetal Catmull-Rom splines; the wheels snap to the runway surface between the thresholds.

### 8.4 Speeds and animation

Final approach turboprop Vapp ≈ 115 kt, narrow-body ≈ 140, wide-body 145, STOL 80, helicopter 60 → hover. Rollout 1,000–1,400 m (turboprop, beta roar) and 1,600–2,000 m (jets, reverse 4–6 s). Take-off roll 1,100–1,300 m (turboprop) and 1,800–2,400 m (narrow-body). Climb 6–8% (turboprop) and 7–9% (jets); turns ≤ 180 kt at a 17–25° bank. Gear retracts 5 s after lift-off. Taxi 10–15 kt (5 in turns). Parked by day: international 4–8 of 17 stands (≤ 2 wide-bodies), domestic 15–25 aircraft, helicopters 6–12; overnight 25–35 on the domestic and north aprons.

### 8.5 Models (generic, procedural; aircraft 8,000 / 2,500 / 800 tris)

| id | Look | Box L × span × H (m) | Cartoon rules | Share |
|---|---|---|---|---|
| `ghm_veh_air_turboprop_a` (new) | High wing, T-tail, two **6-blade** props | 27 × 27 × 7.7 | Fuselage radius ×1.15, nose 10% rounder, props ×1.1, 10 big windows a side, wing thickness ×1.3; props become blur discs above 600 rpm | 75% of domestic fixed-wing (+ a stretch preset 32.8 × 28.4 × 8.3, 10%) |
| `ghm_veh_stol_turboprop_a` (brought forward) | Strutted high wing, fixed gear | 15.8 × 19.8 × 5.9 | as the manifest | 15% of domestic fixed-wing |
| `ghm_veh_air_narrowbody_a` (new) | Low wing, two underwing fans | 38 × 36 × 12 | Fuselage radius ×1.2, length ×0.95, nacelles ×1.25 with spiral spinners, 14 windows a side, wrap-around cockpit visor | 92% of international |
| `ghm_veh_air_widebody_a` (new) | Twin-aisle | 60 × 60 × 17 | same rules, two-tone belly | 8% of international |
| `ghm_veh_helicopter_a` (brought forward) | Single engine, skids, 3-blade main rotor | 12.9 × Ø 10.7 × 3.3 | blur discs; **no medical crosses** | all helicopters |

Liveries are generic only: `liv_glacier` (body `#F4F6F8`, tail teal `#2A9D8F`, three soft peaks), `liv_rhodo` (`#D1495B` five-petal flower), `liv_kite` (`#4FC3F7` diamond kite), `liv_monsoon` (`#5B8C5A` raindrop), `liv_sunrise` (tail gradient `#F4A261`→`#E76F51`, half sun), `liv_yak` (`#8D6E63` horn curve); helicopters red `#E63946`, yellow `#FFC93C` or blue `#3A86FF`. **No text, registrations, logos or flags.** An avoid-list check against every carrier serving KTM is required [V]. Lights per 14 CFR §25.1385–1401 [S]: red left wingtip, green right, white tail; red beacon at 60 flashes per minute while engines run; white double strobes on the runway; landing lights below 3,050 m. They render as HDR sprites (minimum 2 px, `I = I0 / max(1, d/1 km)^1.2`), so a night arrival shows as a slow "star" rising over the southern ridge about 4 minutes before landing.

### 8.6 Aircraft sound (P)

| Class | Synthesis | Audible to (quiet / busy street) | Lmax at 300 m approach / departure |
|---|---|---|---|
| Turboprop | Harmonic stack at the blade-pass frequency × 1…10, **≈ 98 Hz on approach, 120 Hz at take-off** (6 blades × 980–1,200 rpm); turbine whine 3–8 kHz; beta roar +6 dB for 6–10 s after touchdown | 5 / 2 km | 74 / 80 dB(A) [E] |
| Narrow-body jet | Fan tone ≈ 1.5–1.8 kHz on approach and ≈ 3.0 kHz on take-off; buzz-saw comb at ≈ 83 Hz × n on take-off; broadband roar 80–500 Hz; approach "whine" 500–600 Hz [V]; reverse thrust +6 dB for 4–6 s; tyre chirp | 8 / 3 km | 80 / 86 |
| Wide-body | Lower fan tones 0.8–2 kHz, deeper roar | 9 / 4 km | 84 / 90 |
| Helicopter | Noise amplitude-modulated at **19.5 Hz** (3 blades × 390 rpm), depth 0.6–0.9, + harmonics at 39 / 58 / 78 Hz for phone speakers; tail rotor ≈ 68 Hz; turbine 4–8 kHz | 4 / 1.5 km | 77 / 79 |
| Apron | APU 400–500 Hz + harmonics, tugs, unintelligible radio | 300 m | — |

Ground-reflection comb: a second path delayed by `2h·sinθ / c` with gain 0.7. At most **2 real-time aircraft voices** (the nearest two); the rest are baked flyby clips or silent. Inside the Pashupati compound aircraft gain is capped at −6 dB [V].

### 8.7 Runtime and budgets

Kinematic only: spline parameter + speed schedule, positions in `WorldPos` doubles, recomputed after origin shifts. Everything on the schedule is simulated (microseconds per aircraft); only aircraft within 30 km render. Rendered cap: **Low 4 / Mid 6 / High 10** aircraft; the excess draws as light sprites only. LOD0 < 0.8 km, LOD1 0.8–2.5 km, LOD2 2.5–8 km, sprite beyond 8 km (an A320 is ≈ 22 px at 2 km and ≈ 4.5 px at 10 km on a 1,170 px portrait view [D]). Parked aircraft are static and instanced per model and livery. With 25–45 aircraft parked by day, LOD0 everywhere within 300 m would be 200 k+ triangles, so all aircraft (flying and parked) share one budget: at most **1** at LOD0 on Mid and High (the nearest, < 300 m; none on Low), **2 / 3 / 5** at LOD1, the rest at LOD2 (800) to 2.5 km and light sprites beyond, ≤ **6 k / 16 k / 25 k** triangles in all, taken from the building slice (§10.4). Only LOD0 aircraft below 300 m AGL cast a blob shadow. Aircraft are not shown on the map.

---

## 9. Data needs (pipeline, Track E)

### 9.1 Existing work items this design depends on

| Item | Needed for | Stage |
|---|---|---|
| **D1** classifier fixes (stupa, shikhara, gompa vs bahal rules; temple POI → containing footprint; AIRPORT; no building on aprons) | §3 selection, §8.1 | S1 (F1) |
| **D2** route relations → `.ghrt`, `RTES`, `type=restriction` | §5.3 buses, turn restrictions | S1 |
| **D3** `PROP` chunk (trees with species class and chautari flag, lamps, stops, shelters, signals, crossings, taps, wells, gates, chimneys, masts, tanks, solar, artwork, pylons) + Line and Area kinds | §5.7, §5.8, §4.5 crossings, §2.6 tanks | S1 (PROP), S2 (rest) |
| **D4** building parts, plausibility gate, Newar storey heights, `BFNT` frontage edge and shop mask | §2, §3 | S1 |
| **D5** landmark anchors, `compound`, `hide` zones, `yaw_deg`, `manual:` placements (Mahabouddha, 55-Window Palace, Golden Gate, Hanuman Dhoka, Kumari Ghar, Annapurna Asan; S2: Balkumari Thimi, Rudrayani Khokana, Uma Maheshwar Kirtipur) | §3.4 | S1 |
| **D6** places and admin (`ADMN` rings for level 7) | §2.1 rule 3 | S1 |
| **D7** `RPRF` road profiles and `BRDG` decks | wider roads sit on benched profiles | S1 |
| **D10** stable scatter seed | trees, props, dressing | S1 (first) |
| **D12** curated DB `.ghcd` (heritage records with tiers, heights, yaw, gilt, `entry_rule`) | §3.4 hero recipes | S1 |
| **D13** coverage report | §10.6 verification | S1–S2 |
| **D14** sacred routing (no motor modes in SACRED_NO_VEHICLE) | §4.4, G4 | S1 (F1) |
| **D9** skyline ring `.ghsk` + peak table, and ROADMAP 1.9 impostor ring with earth curvature | G8 (the Himalaya seen from the valley) | S2 (owned by the terrain work, not by this design; listed because G8 depends on it) |

### 9.2 New work items

| ID | Work | Output | Stage |
|---|---|---|---|
| **D17** | **Road attributes**: area type per road (the street_life 250 m grid + HILL rule, R §1.2); `sidewalk*`, `footway=sidewalk` lines, `lit`, `maxspeed`, `lanes:forward/backward`, `access`/`motor_vehicle`; **corridor sampling** (rays every 20 m, 40 m each side, first building hit, R §1.3) stored only for OLD_CORE and URBAN pieces of class ≤ tertiary; **dual-carriageway pairing** (opposite one-ways, same ref or name, centrelines < 15 m apart) with median width; Ring Road service-road flag | `RATR` chunk | S1 |
| **D18** | **Junctions**: roundabout and circular rings (ring diameter, member ways), mini roundabouts, signal nodes, the curated police-chowk list (`config/curated/chowks.yaml`, 33 entries, §4.7), island polygons (`area:highway=traffic_island`, grass or park within 45 m), arm count, heritage flag | `JNCT` chunk | S1 |
| **D19** | **Style zones**: `config/style_zones.yaml` (S1: rectangles + circles; S2: traced polygons); profile per building by the §2.1 rules, using `ADMN` level-7 membership **by relation id**; `start_date` < 1960 + stucco hint for RANA-style houses; `building:structure` and `roof:shape` hints; bahal and chowk COURTYARD areas (named closed ways matching `bah(a|al|il|i)|chowk|chok|dabali|vihar`, H App. A) | `BFNT` fields (§9.3); AreaKind COURTYARD | S1 |
| **D20** | **Aeroways into tiles**: runway and taxiway LINEs with width (already LineKind 9/10), aprons as AreaKind APRON 36, `aeroway=gate` and `parking_position` as PROP (yaw from the position line), windsocks, helipads; drop the building on w1368706500; the region sidecar **`<region>.aviation.json`** from `config/aviation/tia.yaml` (thresholds from the OSM runway nodes, VOR, procedures and schedule copied from AV §4–5, liveries) | PROP kinds, AREA kind, sidecar file | S1 |
| **D21** | **Pedestrian and life rasters**: per-tile 64 m grid of pedestrian density weight (shops, food, worship, schools, markets and bus stops per S §1.2 densities) and area type, plus markets (`amenity=marketplace` 58 [O]) and school campuses for the SCHOOL overlay | `LIFE` chunk | S2 (S1 uses the area type from `RATR` only) |
| **D22** | **Bus-route enrichment**: stop inference (`bus_stop` within 25 m, ordered), terminals (bus-park polygons), mode, `ref`, livery class (`city_green` when OSM `operator` names the public city-bus operator, else by mode; the operator name is read but never shown), default headways from `config/curated/transit.yaml` | `.ghrt` fields | S1 (with D2) |

### 9.3 Formats (batch F1 enums, batch F2 chunks; additive, readers skip unknown fourccs)

**F1 enum appends** (indicative values, assigned once, append-only): `BuildingArchetype` RANA_PALACE 20, **NEWAR_HYBRID 21**; `AreaKind` APRON 36 (already planned), **COURTYARD 38, TRAFFIC_ISLAND 39**; new enums `StyleProfile` (§2.1), `AreaType` {UNKNOWN 0, OLD_CORE 1, URBAN 2, PERI_URBAN 3, RURAL 4, HILL 5, FOREST 6}, `JunctionKind` {PLAIN 0, ROUNDABOUT 1, CIRCULAR 2, MINI_ROUNDABOUT 3, SIGNALS 4, POLICE 5, SYNTHETIC_ISLAND 6}, `Sidewalk` {UNKNOWN 0, NONE 1, LEFT 2, RIGHT 3, BOTH 4, SEPARATE 5}; `ObjectKind` (PROP) adds TRAFFIC_SIGNALS, CROSSING_MARKED, CROSSING_UNMARKED, AEROWAY_GATE, PARKING_POSITION, WINDSOCK, HELIPAD, TAXI_STAND.

**`RATR`** (one record per `ROAD` record, same order):

```text
varint count                       == ROAD count
count × RoadAttr:
  u8     area_type                 AreaType
  u8     sidewalk                  Sidewalk
  u8     lanes_fwd, lanes_bwd      0 = unknown
  u8     maxspeed_kmh              0 = unknown
  u8     flags                     bit0 DUAL (has partner), bit1 SERVICE_ROAD, bit2 HERITAGE_PEDESTRIAN,
                                   bit3 NO_MOTOR (access/motor_vehicle=no), bit4 LIT, bit5 BUS_ROUTE,
                                   bit6 RING_MEMBER (junction=roundabout|circular), bit7 PAINTABLE (real ≥ 5.5 m, sealed)
  varint partner_way_id            0 = none
  varint median_cm                 0 = none
  varint corridor_count            0 = unknown; else samples every 20 m from the piece's first rendered point
  corridor_count × varint corridor_dm   2 × min(dLeft, dRight) at the sample; 0 = open on one side (unbounded)
```

**`JNCT`**:

```text
varint count
count × Junction:
  varint osm_node_id
  u8     kind                      JunctionKind
  u8     arms
  u8     flags                     bit0 HAS_ISLAND_AREA, bit1 OFFICERS_2_4, bit2 HERITAGE_NO_MOTOR, bit3 CROSSINGS_MARKED
  svarint x_cm, z_cm               centre, absolute tile-local
  varint ring_diameter_cm          0 = no ring
  varint island_diameter_cm        0 = none or synthetic (runtime computes §4.7)
  varint island_area_osm_ref       (osm_id << 1) | is_relation of the AREA island, 0 = none
  varint name_ref
```

**`BFNT`** (D4, extended; one record per `BLDG` record, same order):

```text
varint count                       == BLDG count
count × BuildingFront:
  u8     style_profile             StyleProfile
  u8     area_type                 AreaType
  u8     front_edge                ring-0 edge index facing the nearest motor road or pedestrian street; 255 = none
  u8     front_dist_dm             front edge midpoint to the road centreline, decimetres (0..255)
  u8     shop_bays                 bits 0-3 shop count from shop POIs inside (0..15); bit7 FROM_POI
  u8     flags                     bit0 COURTYARD_HOST, bit1 CORNER (second road-facing edge), bit2 FACES_HERITAGE_SQUARE,
                                   bit3 RANA_HINT, bit4 STRUCTURE_RCC, bit5 STRUCTURE_MUD, bit6 ROOF_FLAT_TAGGED
  u8     second_edge               255 = none (corner houses)
```

**Region files:** `.ghrt` (D2 + D22), `.ghcd` (D12, heritage `attrs`: `tiers`, `plinth_levels`, `height_m`, `yaw_deg`, `finish`, `doors`, `guardians`, `entry_rule`, `kora`), and `<region>.aviation.json` (D20, listed in the manifest `files[]` with its SHA-256; ODbL-derived thresholds plus our own procedure tables).

**Size estimate** (valley pack) [E]: RATR ≈ 0.6 MB deflated (most of it corridor samples on ~2,000 km of URBAN and OLD_CORE roads), JNCT ≈ 40 KB, BFNT ≈ 1.1 MB (542 k buildings × 7 bytes raw ≈ 3.8 MB, about 2 bytes each after DEFLATE because most fields repeat within a tile), aviation JSON ≈ 30 KB, `.ghrt` ≈ 150 KB. Total ≈ 2 MB, inside the O8 plan (drop the L10 heights, ≈ −20 MB).

### 9.4 Hero recipes

`pipeline/config/curated/heritage_sites.yaml` (D12, Track E) holds one record per §3.4 row: `id` (`her.ktm.taleju`), `osm` anchor and `compound`/`hide` refs, `kind` (PAGODA, SHIKHARA_STONE, SHIKHARA_PLASTER, STUPA, HOUSE_TEMPLE, MANDAPA, RELIEF, COLUMN, GATE, PALACE, TOWER, HITI, POKHARI, GOMPA, BAHAL), `attrs` (the generator parameters of §3.2 that differ from defaults), `provenance` (the T §9 URLs) and `review` (cultural sign-off). The generators read the compiled `.ghcd`. Until D12 lands, E's compiler also writes the same records as `hero_recipes.json`, which C loads from `World/Resources/` (a temporary bridge; removed in S2).

### 9.5 Pipeline tests (Track E)

Determinism of every new chunk (built twice, same SHA-256); golden files for RATR, JNCT, BFNT and `.ghrt` regenerated in `shared/` with C# reader tests (A); D19 profile counts per municipality within ±2% of a reference table; D17 corridor samples never smaller than the tagged width on more than 1% of samples (the mapping-offset guard); D20 threshold coordinates equal the OSM runway end nodes (to the cm).

---

## 10. Implementation plan

### 10.1 Tracks and file ownership

A file belongs to exactly one track. Cross-track changes go through the contracts in §10.3. Tests live next to the owner (`core-tests/` for Core, `game/Assets/Ghumante/Tests/` for Unity EditMode).

| Track | Owns (paths under `game/Assets/Ghumante/` unless stated) | W2 deliverables |
|---|---|---|
| **A: Core meshing + generators** | `Core/Meshing/**` (RoadMesher, BuildingMesher, BuildingStyle, new `Buildings/` grammar, `Roads/` width model, junction and marking meshers), `Core/Generators/**` (new: `Sacred/`, `Placement/` for trees and props), `Core/Data/**` (readers for RATR, JNCT, BFNT, PROP, `.ghrt`, `.ghcd`), `core-tests/Meshing*`, `core-tests/Data*`, `core-tests/Generators*` | §2 grammar and LOD bands, §3 generators and hero builds, §4 widths, profiles, junction caps, roundabouts and markings, tree and prop placement rules, chunk readers |
| **B: Core driving + traffic sim + vehicles** | `Core/Driving/**` (VehicleSpec presets, ArcadeVehicle, ground queries incl. structure colliders), `Core/Traffic/**` (new: lane graph, IDM, junction controllers, spawn, bus runner, pedestrian and animal agents, contact rules), `Core/Aviation/**` (new: schedule and path sim), `Vehicles/**` (procedural vehicle models, liveries, seat sockets), `core-tests/Driving*`, `Traffic*`, `Aviation*` | §5.1–5.5 simulation, §5.2 catalogue models, §6.3 handling, §8.2–8.4 aviation sim |
| **C: World runtime: rendering, instancing, audio** | `World/**` (new layers: building bands B0–B3, road decals, sacred and hero placement, `Instancing/` for trees, props, parked vehicles, crowds and flocks via BatchRendererGroup or `DrawMeshInstanced`, `Aviation/` presenter, airport lights), `Traffic/**` (agent presenters), `Wildlife/**` (flocks, monkeys, dogs), `Audio/**` and `Core/Synth/**` (synth core, voices, bank, ambience director, mixer), `World/Shaders/**` (vertex wind, VAT, dither fade), `Tests/EditMode/World*` | Everything the player sees and hears from A's and B's data |
| **D: Gameplay, character, HUD** | `Characters/**` (player avatar, mount and seat controller, cameras), `Core/Characters/**` (new: CharacterRecipe, HumanoidMesher, gait, springs, IK: engine-free), `UI/**` (touch layouts for portrait and landscape, prompts, emote wheel, bus and taxi flows), `App/**` wiring, `Save/**` (`player.appearance`, garage), `Activities/**` (bus driver, taxi fares), `core-tests/Characters*` | §6 whole, §5.3 player loops, the calm-mode and sacred prompts |
| **E: Data pipeline** | `pipeline/**`, `shared/**`, golden-file generators, `pipeline/config/**` (style_zones, chowks, transit, heritage_sites, aviation), `tools/qa-viewer/**` layers for the new chunks | §9: D1–D7, D10, D12–D14, D17–D22 |

### 10.2 Shared rules

* Frames: game metres (X east, Z north, Y up); tile-relative mesh positions from the tile's south-west corner with absolute Y (M1_PLAN contract).
* Determinism: every random choice comes from `Hashes.Fnv1a` of (OSM ref or tile seed, purpose constant). No `System.Random` without a seed. No wall-clock time in Core.
* Core stays engine-free (`noEngineReferences`, netstandard2.1, C# 9); Burst lives in wrappers in the Unity assemblies.
* Budgets in §10.4 are enforced by tests (`MeshingPerformanceTests`-style) and by the debug HUD counters.

### 10.3 Contracts (C# names and semantics)

```csharp
// ---- Ghumante.Core.Data (Track A readers; E writes the bytes) -------------------------------------
public enum StyleProfile : byte { None, KathmanduCore, Thamel, Patan, Bhaktapur, Kirtipur, Thimi, Bungamati,
                                  Khokana, Panauti, Metro, Rim, BoudhaKora }
public enum AreaType : byte { Unknown, OldCore, Urban, PeriUrban, Rural, Hill, Forest }
public enum JunctionKind : byte { Plain, Roundabout, Circular, MiniRoundabout, Signals, Police, SyntheticIsland }
public struct RoadAttrRecord { public AreaType Area; public byte Sidewalk, LanesFwd, LanesBwd, MaxspeedKmh, Flags;
                               public long PartnerWayId; public int MedianCm; public int[] CorridorDm; }   // CorridorDm may be empty
public struct JunctionRecord { public long OsmNodeId; public JunctionKind Kind; public byte Arms, Flags;
                               public int XCm, ZCm, RingDiameterCm, IslandDiameterCm; public long IslandAreaRef; public int NameRef; }
public struct BuildingFrontRecord { public StyleProfile Profile; public AreaType Area; public byte FrontEdge, FrontDistDm,
                                    ShopBays, Flags, SecondEdge; }
// TileData gains: IReadOnlyList<RoadAttrRecord> RoadAttrs; IReadOnlyList<JunctionRecord> Junctions;
//                 IReadOnlyList<BuildingFrontRecord> BuildingFronts; IReadOnlyList<PropRecord> Props;   (empty when absent)
public sealed class RouteSet  { public IReadOnlyList<TransitRoute> Routes; public static RouteSet Read(byte[] ghrt); }
public sealed class TransitRoute { public string Id, Ref; public TransitMode Mode; public LiveryClass Livery;
                                   public long[] WayIds; public bool[] WayForward; public TransitStop[] Stops; public float HeadwayPeakS, HeadwayOffS; }
public sealed class CuratedDb { public bool TryGetHeritage(string id, out HeritageRecord r); public IEnumerable<HeritageRecord> Heritage { get; } }

// ---- Ghumante.Core.Meshing (Track A) ---------------------------------------------------------------
public struct RoadProfile {                     // one cross-section, game metres, left/right relative to point order
    public float CarriagewayM, MedianM, FootpathLeftM, FootpathRightM, KerbLeftM, KerbRightM, ShoulderM;
    public byte Lanes, LanesFwd, LanesBwd; public Travel Access; public bool CentreLine, LaneLines, EdgeLines;
}
public static class RoadWidthModel {
    public static float RealWidthM(in RoadRecord r, in RoadAttrRecord a);              // §4.1
    public static float GameWidthM(in RoadRecord r, in RoadAttrRecord a, float alongM); // §4.2, tapered, smoothed
    public static Travel AccessFor(float gameWidthM, in RoadRecord r, in RoadAttrRecord a); // §4.4
    public static RoadProfile ProfileAt(in RoadRecord r, in RoadAttrRecord a, float alongM);
    public const float Scale = 1.25f, TaperRatio = 20f, MaxGainM = 6f;
}
public static class JunctionMesher { public static int Build(TileData t, IHeightSampler h, RoadOptions o, MeshData surface, MeshData decals); }
public static class MarkingMesher  { public static int Build(TileData t, IHeightSampler h, RoadOptions o, MeshData decals); }
public enum BuildingBand : byte { B0KitLite, B1Styled, B2Prism, B3Block }
public static class BuildingGrammar {
    public static StyleParams For(StyleProfile p);                                     // §2.2 priors, §2.5 palette
    public static BuildingArchetype ResolveArchetype(in BuildingRecord b, in BuildingFrontRecord f, uint seed);
    public static float StoreyHeightM(BuildingArchetype a, int floor);                 // replaces LevelHeightM
}
public static class BuildingDetailMesher {   // B0, per 64 m cell; deterministic; ≤ 4 ms worker time per cell on Mid
    public static int BuildCell(TileData t, IHeightSampler h, int cellX, int cellZ, BuildingOptions o, MeshData m, StructureColliders c);
}
// BuildingMesher.Build gains: BuildingOptions.Band (B1 default = W1 behaviour + styled roof/stripes), B2Prism, B3Block.

// ---- Ghumante.Core.Generators.Sacred (Track A) -----------------------------------------------------
public struct GenFrame { public double X, Z; public float GroundY, YawDeg; }           // tile-local metres, yaw = door bearing
public struct PagodaParams { public float PlinthW, PlinthD; public int PlinthLevels; public float StepRiseM, StepInsetFrac;
    public float CoreFrac /*0.45*/, CoreHeightFrac, FirstEaveFrac /*1.9*/, TierShrink /*0.74*/, TierSpacingFrac /*0.23*/;
    public int Tiers; public float PitchBottomDeg, PitchTopDeg, CornerLift, GajurFrac, TotalHeightM /*0 = derive*/;
    public RoofFinish Finish; public byte Doors; public bool Pataka; public GuardianSet Guardians; public float BellSpacingM /*0.4*/; }
public struct ShikharaParams { ... }  public struct StupaParams { ... }  public struct ChaityaParams { ... }
public struct ShrineParams { ... }    public struct PatiParams { ... }   public struct HitiParams { ... }
public static class PagodaGenerator   { public static int Build(in PagodaParams p, in GenFrame f, int lod, MeshData m, StructureColliders c); }
// ... one static Generator per kind with the same Build shape ...
public static class SacredSelector    { public static bool TrySelect(TileData t, int buildingOrPoiIndex, out SacredKind kind, out SacredParams p); }
public static class HeroBuilder       { public static int Build(HeritageRecord r, TileData anchorTile, int lod, MeshData m, StructureColliders c); }

// ---- Ghumante.Core.Driving (Track B) ---------------------------------------------------------------
public struct OrientedBox { public double CX, CZ; public float CY, HalfX, HalfY, HalfZ, YawRad; public ColliderFlags Flags; } // Walkable, NoClimb, SoftMargin
public struct StepRamp    { public double X0, Z0, X1, Z1; public float Y0, Y1, HalfWidth; }
public sealed class StructureColliders { public void Clear(); public void AddBox(in OrientedBox b); public void AddRamp(in StepRamp r); }
public interface IStructureGround { void Register(ulong tileKey, StructureColliders c); void Unregister(ulong tileKey); }
// TileGroundQuery implements IStructureGround: walkable tops and ramps join the ground; NoClimb boxes block.
public enum VehicleKind : byte { Motorbike = 0, Taxi = 1, Walker = 2, Bicycle = 3, Scooter = 4, Car = 5, Suv = 6,
                                 Microbus = 7, Tempo = 8, Minibus = 9, Bus = 10, Truck = 11, Tractor = 12, Cruiser = 13 }  // append-only
// VehicleSpec gains presets per kind (§6.3) and fields SteerInputDelayS, VisualRollPerMps2, VisualPitchPerMps2,
// RearOverhangM, LengthM, WidthM, SeatSockets (count), DriverSide (Left/Right).

// ---- Ghumante.Core.Traffic (Track B) ---------------------------------------------------------------
public readonly struct LaneId { public readonly int Value; }
public sealed class LaneGraph {                  // built incrementally per resident level-10 tile
    public void AddTile(TileId id, TileData t);  public void RemoveTile(TileId id);
    public bool TryNearestLane(double x, double z, VehicleClass c, out LaneId lane, out float along);
    public LaneInfo Info(LaneId l);              // centreline polyline (game m), width, speed target, class mask, controller id
}
public enum VehicleClass : byte { TwoWheeler, Bicycle, Rickshaw, Car, Taxi, Suv, Microbus, Tempo, Minibus, Bus, Truck, Tanker, Tractor, Service }
public struct AgentPose { public double X, Z; public float Y, HeadingRad, SpeedMps, Lean, Pitch, Roll; public VehicleClass Class;
                          public ushort Variant; public byte Livery, AnimState; public int AgentId; }  // AgentId stable for audio seeds
public sealed class TrafficSim {
    public TrafficSim(LaneGraph g, TrafficSettings s, ulong seed);
    public void SetFocus(double x, double z, float simRadiusM);
    public void Step(float dt, float gameHourOfDay, float wetness01);    // worker thread; ≤ budget §10.4
    public int CopyPoses(AgentPose[] dst);                               // double-buffered snapshot for C
    public void AddObstacle(int id, double x, double z, float radiusM, ObstacleKind k);   // cows, dogs, player
    public event Action<int, HornKind> Horn;                             // audio hook (C subscribes)
}
public struct PedPose   { public double X, Z; public float Y, HeadingRad, SpeedMps; public byte Archetype /*S §3.2 order*/, Tint, ClipId /*VAT clip, ASSET_MANIFEST §9.5 order*/;
                          public float ClipTime; public ushort CarryProp /*0 none, doko, sack, cylinder, baby, umbrella*/; public int AgentId; }
public struct AnimalPose { public double X, Z; public float Y, HeadingRad, SpeedMps; public AnimalKind Kind; public byte Tint, ClipId; public float ClipTime; public int AgentId; }
public struct FlockState { public int FlockId; public BirdKind Kind; public double CX, CZ; public float CY; public ushort Count; public float Phase; public FlockMode Mode /*Ground, Burst, Circuit, Landing*/; }
public sealed class PedestrianSim { /* same ctor/SetFocus/Step shape as TrafficSim; int CopyPoses(PedPose[] dst) */ }
public sealed class AnimalSim     { /* cows, dogs, goats, chickens, buffalo, ducks, macaques: int CopyPoses(AnimalPose[] dst); int CopyFlocks(FlockState[] dst) */ }
public static class ContactRules  { public static void HopAsideCapsule(...); public const float CowSlowM = 4f, CowStopM = 1.6f; }
public sealed class BusRouteRunner { public BusRouteRunner(RouteSet routes, LaneGraph g, TrafficSim sim); public void Step(float dt, float hour); }

// ---- Ghumante.Core.Aviation (Track B) --------------------------------------------------------------
public sealed class AviationConfig { public static AviationConfig Parse(string json); }
public struct AircraftState { public int Id; public AircraftClass Class; public byte Livery; public double X, Z; public float Y,
    HeadingRad, PitchRad, BankRad, SpeedMps, GearDown01, PropRpm; public LightState Lights; public bool OnGround; }
public sealed class AirTrafficSim { public AirTrafficSim(AviationConfig c, ulong regionSeed);
    public void Step(double realSeconds, float gameHour, int month, WeatherKind w); public int CopyStates(AircraftState[] dst); }

// ---- Ghumante.Core.Synth (Track C) -----------------------------------------------------------------
public struct EnginePreset { public byte Cylinders; public bool FourStroke; public float IdleRpm, RedRpm; public float[] Harmonics; /* A §4.2 */ }
public struct EngineVoiceParams { public float Rpm, Load, SpeedMps, Gain, DopplerPitch; public byte Surface; } // 64-byte block per voice
public sealed class EngineVoice { public EngineVoice(in EnginePreset p, uint seed); public void Render(float[] mono, int frames, int sampleRate, in EngineVoiceParams from, in EngineVoiceParams to); }
public static class ProceduralBank { public static float[] Render(BankSound s, int variant, int sampleRate, uint seed); }

// ---- Ghumante.Core.Characters (Track D) ------------------------------------------------------------
public sealed class CharacterRecipe { public byte Build, Skin, Hair, HairColour; public OutfitSlot[] Outfit; public uint Seed; } // save: player.appearance
public static class HumanoidMesher { public static void Build(CharacterRecipe r, int lod, MeshData m, SkinWeights w); }   // ≤ 5,000 tris LOD0
public struct SecondOrder { public SecondOrder(float f, float zeta, float r, float x0); public float Update(float dt, float x); }
public static class TwoBoneIk { public static void Solve(...); }
public sealed class GaitSolver { public void Step(float dt, float speed, float yawRate, ...); }
public enum SeatRole : byte { Driver, Pillion, Passenger, Standing }
public sealed class MountPlan { public static bool TryPlan(VehicleClass c, in SeatSocket s, double px, double pz, IGroundQuery g, out MountPlan p); } // side, path ≤ 4 m, duration

// ---- Shared catalogue and zone queries (added in review) --------------------------------------------
// One table maps every §5.2 asset to both enums, so the traffic sim (VehicleClass), the player's handling (VehicleKind),
// the presenters (asset id) and audio (EnginePreset) agree. Owner: Track B (Core/Driving/VehicleCatalog.cs).
public readonly struct VehicleCatalogEntry { public readonly string AssetId; public readonly VehicleClass TrafficClass;
    public readonly VehicleKind DriveKind; public readonly byte HandlingPreset, EnginePreset, LiveryCount; public readonly bool PlayerDrivable; }
public static class VehicleCatalog { public static IReadOnlyList<VehicleCatalogEntry> All { get; }   // AgentPose.Variant indexes this list
    public static bool TryGet(string assetId, out VehicleCatalogEntry e); }
// Mapping fixed here: tanker → DriveKind Truck (preset "tanker"); pickup, hatchback, EV → Car; e-scooter → Scooter; tourist and school bus → Bus;
// police jeep → Suv; ambulance → Microbus; rickshaw → Bicycle (player passenger only). VehicleClass gains nothing: variants carry the rest.

// Sacred and compound zones: one index used by traffic (no motor spawns or player vehicles), gameplay (calm mode, prompts),
// audio (Courtyard snapshot) and pedestrians (kora). Built per tile from AREA RELIGIOUS / COURTYARD / HERITAGE_SQUARE,
// bahal and chowk holes and D5 landmark compounds. Owner: Track A (Core/Data/SacredZoneIndex.cs).
public struct SacredZone { public long AreaRef; public SacredZoneKind Kind /*Compound, Courtyard, HeritageSquare, StupaKora, Ghat*/;
    public string HeroId /*null if none*/; public EntryRule Rule; public bool Kora; public double CX, CZ /*kora centre*/; }
public sealed class SacredZoneIndex { public void AddTile(TileId id, TileData t, CuratedDb db); public void RemoveTile(TileId id);
    public bool TryGetZone(double x, double z, out SacredZone zone); public bool IntersectsSegment(double x0, double z0, double x1, double z1); }

// Area type at any point (the 250 m grid of S §1.1): needed in S1 by ambience, spawns and pedestrians before the D21 LIFE
// raster exists. Majority vote of BFNT.area_type and RATR.area_type inside each 250 m cell; replaced by LIFE in S2.
// Owner: Track A (Core/Data/AreaTypeGrid.cs).
public sealed class AreaTypeGrid { public void AddTile(TileId id, TileData t); public void RemoveTile(TileId id);
    public AreaType At(double x, double z); public void Weights(double x, double z, Span<float> perAreaType); }   // bilinear for crossfades

// Footstep surface: the ground query reports what the foot touches, so footsteps follow the real material (owner request).
// Owner: Track B (ground query); Track C maps FootSurface to the §7.3 footstep sets.
public enum FootSurface : byte { Asphalt, Concrete, Brick, Stone, Gravel, Dirt, Mud, Grass, Wood, Metal, Water }
// GroundSample gains: public FootSurface Foot;  resolved in this order: (1) a StructureColliders box or ramp under the foot
// (OrientedBox/StepRamp gain a FootSurface Material field: plinths and squares Stone, Bhaktapur and Patan paving Brick,
// dabali and balconies Wood, Bailey decks Metal); (2) an AREA with a paving surface (squares, courtyards); (3) the road's
// Surface (Asphalt, Concrete, Brick, Cobble→Stone, Gravel, Compacted→Gravel, Dirt, Mud, Wood, Metal); (4) the biome
// (BiomeGround: built-up → Concrete, field and forest → Dirt, grass → Grass, wetland and paddy → Mud, water → Water);
// then wetness ≥ 0.5 turns Dirt into Mud (as ArcadeVehicle does for SurfaceGroup).

// Hero LOD allocator (§3.2): Track C, World/Sacred/HeroLodBudget.cs. Input: visible heroes with screen height; output: LOD per hero.

// ---- Events (Core.Services.EventBus) ---------------------------------------------------------------
// VehicleEntered(vehicleId, VehicleClass, SeatRole), VehicleExited(...), SacredZoneEntered(areaRef, entryRule),
// SacredZoneExited(areaRef), HornUsed(VehicleClass), BellRung(propRef), BusStopReached(routeId, stopIndex).
```

**Semantics fixed by these contracts**

* `RoadWidthModel` is the only place that decides widths. `RoadMesher`, `JunctionMesher`, `RoadSpatialIndex.HalfWidthM`, `LaneGraph` and the routing access masks all call it, so the mesh, physics, AI and routes agree.
* `StructureColliders` are produced with the mesh (same build job) and registered when the mesh becomes visible, exactly like the W1 rule that "an area enters the ground query when its mesh becomes visible".
* `TrafficSim`, `PedestrianSim`, `AnimalSim` and `AirTrafficSim` run on worker threads and expose double-buffered snapshots. Track C presenters only read snapshots and never call back into the sim during a step.
* `VehicleCatalog` is the only mapping between asset ids, `VehicleClass` and `VehicleKind`; nobody switches on asset-id strings elsewhere. `SacredZoneIndex` is the only answer to "is this point sacred?": D14 routing (E) and the runtime index (A) are built from the same AREA records, and V5 fuzzes both.
* `HumanoidMesher` (D) also builds the NPC LOD0 and LOD1 bodies from `CharacterRecipe`s; Track C bakes the VAT crowd body from its LOD2 output, so NPCs and the player share one character style.
* `AgentPose.AgentId` seeds per-agent variation (engine `seedVariance`, tint, plate number) so an agent looks and sounds the same for its whole life.
* Floating origin: all sims use game metres in doubles; presenters convert with `WorldRoot.ToScene`. Nothing in Core shifts on rebase.

### 10.4 Budgets per tier (ARCHITECTURE §10)

| Budget | Low | Mid | High |
|---|---|---|---|
| Frame | 30 fps, render scale 0.7 | 30 (60 option) | 60 |
| Main thread total | ≤ 22 ms | ≤ 14 ms / 9 ms | ≤ 9 ms |
| — streaming upload (existing) | 2 ms | 2 ms (1.5 at 60) | 1.5 ms |
| — sim snapshot copy + instanced submission | ≤ 1.2 ms | ≤ 1.2 ms | ≤ 1.5 ms |
| — player (controller, animation, camera) | ≤ 0.6 ms | ≤ 0.5 ms | ≤ 0.5 ms |
| Worker: traffic sim | ≤ 1.0 ms/frame | ≤ 1.5 | ≤ 2.0 |
| Worker: pedestrians + animals + flocks | ≤ 0.8 | ≤ 1.2 | ≤ 1.8 |
| Worker: B0 building cells | ≤ 4 ms per cell, ≤ 1 cell per 100 ms | same | same |
| Audio DSP (audio thread) | < 10% of one core | < 10% | < 10% |
| Visible triangles (§1.4 slices) | 150 k | 400 k | 700 k |
| — terrain / roads + decals / sky + water | 30 k / 8 k / 4 k | 70 k / 20 k / 10 k | 110 k / 30 k / 20 k |
| — buildings (incl. generic sacred) / heroes in view | **22 k** / 20 k | **72 k** / 40 k | **135 k** / 60 k |
| — vegetation / props | **18 k** / 8 k | **54 k** / 25 k | **100 k** / 45 k |
| — characters / vehicles (moving + parked) / animals and birds | **21 k / 15 k / 4 k** | **50 k / 45 k / 14 k** | **95 k / 75 k / 30 k** |
| SRP batches | ≤ 120 | ≤ 200 | ≤ 300 |
| Moving vehicles / NPCs (sim) | 12 / 25 | 30 / 60 | 50 / 120 |
| Parked vehicle instances | 80 | 200 | 400 |
| Skinned NPCs LOD0 / LOD1 / VAT visible | 1 / 4 / 13 | 3 / 10 / 39 | 6 / 20 / 78 |
| Moving vehicles drawn LOD0 / LOD1 / LOD2 (+ player) | 0 / 1 / 6 | 1 / 6 / 16 | 3 / 10 / 20 |
| Aircraft rendered | 4 | 6 | 10 |
| Real / synth voices | 24 / 4 | 32 / 8 | 48 / 12 |
| Memory: meshes (W2 adds B0 cells, instances) | 100 MB | 180 MB | 250 MB |
| Memory: audio | 25 MB | 40 MB | 50 MB |
| Memory: VAT crowd textures | 4.4 MB (shared) | 4.4 MB | 4.4 MB |

**Slice reallocation (review, 2026-10-05).** The bold cells differ from ASSET_MANIFEST §1.4, whose slices cannot hold the W2 life targets: on Low the manifest's own crowd recipe (player 5 k + 2 skinned LOD0 at 3.5 k + 6 LOD1 at 1.5 k) is already 21 k against a 15 k slice, 80 parked motorbikes at the moving LOD2 would be 48 k, and a 200-bird pigeon flock at 300 tris would be 60 k. The building slice gives up what §2.4 shows it does not use (≈ 20 / 63 / 115 k used). Each column still sums to 150 / 400 / 700 k. Worked counts: characters Low = player 5 k + 1 × 3.5 k + 4 × 1.5 k + 13 × 0.5 k (VAT) ≈ 21 k, i.e. 18 NPCs on screen; Mid ≈ 50 k (52 NPCs); High ≈ 95 k (104 NPCs). Vehicles Low = player 6 k + 1 × 2 k + 6 × 0.6 k ≈ 11.6 k moving + 3.5 k parked ≈ 15 k (8 moving vehicles on screen; the other sim agents are behind the camera or culled); Mid ≈ 34 k + 12 k; High ≈ 56 k + 19 k. Proposal to the ASSET_MANIFEST owner: adopt these slices and the §9.5 skinned counts 1 / 3 / 6 (LOD0) and 4 / 10 / 20 (LOD1). The aircraft at TIA come out of the building slice (an aerodrome has almost no buildings): at most 1 aircraft at LOD0 (none on Low) and 2 / 3 / 5 at LOD1, the rest at LOD2 (800) or as light sprites, ≤ 6 k / 16 k / 25 k in all (§8.7).

### 10.5 Ordering: two implementation stages

**Stage 1: "the city reads right and moves"** (goal: the §1.2 rows for Asan, Basantapur, Boudha, Swayambhu, Thamel, Ring Road and TIA pass on Mid)

| Order | E (data) | A (meshing) | B (sim) | C (runtime) | D (gameplay) |
|---|---|---|---|---|---|
| S1.1 (days 1–4) | F1 enums; D10 seed; D1 and D14; D5 anchors and hide zones; D19 rectangles; `chowks.yaml`, `heritage_sites.yaml` (S1 rows) | `RoadWidthModel` against the tagged widths only (no RATR yet); `StoreyHeightM`; reader stubs that accept missing chunks; `SacredZoneIndex` and `AreaTypeGrid` on W1 AREA data | VehicleKind appends and the 13 presets; `VehicleCatalog`; `StructureColliders` + ground integration; `GroundSample.Foot`; lane graph on W1 data | B1 styled extrusion in the buildings layer; road-decal layer plumbing; Synth core + EngineVoice | CharacterRecipe + HumanoidMesher v1; mount and exit for all classes using the existing ArcadeVehicle; portrait walk camera 6.8 m |
| S1.2 (days 5–12) | F2: RATR (D17), JNCT (D18), BFNT (D4 + D19), PROP subset (trees, lamps, stops, signals, crossings, tanks, gates, aeroway), D20 aviation sidecar, D2 + D22 `.ghrt`, D12 `.ghcd` for the S1 heroes | Readers + golden tests; corridor clamp, tapers, dual pairing, footpaths, kerbs, markings, junction caps, roundabouts; B0 grammar for NEWAR, HYBRID, MODERN; B2 and B3 bands; Pagoda, stupa, shikhara, chaitya and shrine generators; HeroBuilder for the S1 heroes | IDM, LHT lanes, POLICE / SIGNAL / ROUNDABOUT controllers, spawn tables and time-of-day; cows and dogs as obstacles + cushion; bus runner; pedestrian sim v1 (sidewalks, crossings, compounds, kora); AirTrafficSim with A1, D1, D2 and H-E | Band scheduler (B0 cells, cross-fade); hero placement + hide; instanced trees (OSM + avenues + forest clumps), parked vehicles and props; traffic and crowd presenters (skinned near, VAT far); officers; aviation presenter + runway mesh and lights; voice manager, baked bank, ambience zones, footsteps, engines, horns, bells, aircraft | Handling per class + cameras per class; touch layouts in both orientations; Hop on / passenger flows; hail taxi; bus passenger; calm mode and sacred prompts; garage and community fleet; save of `player.appearance` |
| S1.3 (days 13–16) | D13 coverage report run; QA-viewer layers | Perf pass on 10/516/161; drop order for the triangle cap | Sim LOD (ghost lanes); perf | Occlusion rays, snapshots (Courtyard, DurbarSquare, Galli), custom Doppler | Gait, IK and springs polish; emotes; idle fidgets |
| Gate | §10.6 checks V1–V9 green on Mid and Low | | | | |

**Stage 2: "Kathmandu comes alive"** (goal: every §1.2 row on Low, Mid and High, plus S2 heroes and the full life set)

| E | A | B | C | D |
|---|---|---|---|---|
| D19 traced polygons; PROP rest (benches, taps, wells, chimneys, masts, artwork, shelters); D21 `LIFE` raster; D3 lines and areas (walls, fences, markets, bus parks, campuses); D12 S2 heroes + `entry_rule`; transit headway tuning | RANA_PALACE grammar; Thamel signage and terraces; bahal and chowk courtyard decorator; pati, sattal, mandapa, hiti, pokhari and gompa generators; S2 heroes (Changu Narayan, Mahabouddha, Budhanilkantha, Bajrayogini, Bagh Bhairab, Bungamati, Kopan, Rani Pokhari, Bhaktapur S2 set, and rows 42–52: Jana Bahal, Akash Bhairab, Seto Bhairav, Balkumari Thimi, Rudrayani Khokana, Uma Maheshwar and Chilancho Kirtipur, Ghantaghar, Guhyeshwari, Dakshinkali); poles and wire generator; bus bays and speed humps | Vendors (carts, mats, pack-up); macaques, goats, chickens, buffalo; flocks (pigeons, crows, kites); remaining procedures A2–A5, D3, D4, H-N, H-W, holding, go-arounds, fog and monsoon modifiers; pillion NPC; tractor trailer | Rain on tin, weather layers, season palettes (jacaranda, rhododendron, paddy colour); kites in the sky; night lights (aircraft, airport, Thamel signs); great bells as real-time modal voices; Speaker/Headphones profiles; thermal step-down | Bus driver and taxi driver activities; emote wheel complete; namaste interactions; bicycle stamina; tilt steering and accessibility options; photo of a hero from the plinth |
| Gate: §10.6 V1–V12 green on all tiers; cultural reviewer sign-off on §10.7 | | | | |

### 10.6 Verification

| # | Check | Owner | How |
|---|---|---|---|
| V1 | Width model | A | Unit tests: §4.1 defaults per class × area; §4.2 formula (scale, minimums, clamp, never narrower than real, `real + 6` cap); taper 1 : 20; dual-carriageway median ≥ 1.0 m; Jawalakhel island within ±2 m of 36 m |
| V2 | No road over a building | A | On the full valley pack: no ribbon triangle overlaps a building footprint by > 0.1 m (G7) |
| V3 | Building budgets | A | `MeshingPerformanceTests`: B0 average ≤ 900 and cap ≤ 2,500 per house; band totals on tile 10/516/161 within the §2.4 table ± 15% |
| V4 | Replica fidelity | A | For every S1 hero: plan within 0.1 m of the OSM outline, total height within ±3% of §3.4, tier and plinth counts exact, door yaw within ±2°; bells and struts per the formulas; chaitya niche order E, S, W, N |
| V5 | Sacred rules | B + C | 1,000 random spawns inside SACRED_NO_VEHICLE produce 0 motor agents; Boudha and Swayambhu NPC angular velocity about the stupa is always clockwise; no geometry inside a sanctum volume |
| V6 | Traffic correctness | B | LHT: lane offsets negative-left on 100% of two-way roads; roundabout circulation clockwise; yield at entry; IDM no-collision over a 30-minute headless run on the Ring Road; cow collider overlap 0 frames (G5) |
| V7 | Handling | B | Turning radius per class within ±5% of §6.3; δmax(v) law; bus body sweep ≤ 11 m; a two-wheeler never tips |
| V8 | Enter / exit | D | Fuzz 1,000 parked spots: exit placement never inside geometry; every class enterable; RHD driver side right; time to control within §6.2 ± 0.1 s |
| V9 | Determinism | all | Same seed → identical traffic, crowd and aircraft snapshots after 10 minutes of headless sim; same pack → identical B0 cells; audio golden hashes per seed |
| V10 | Aviation | B + C | Golden-schedule test: one seeded day gives class totals within ±5% of §8.2 and runway separation ≥ 100 s; every path point AGL ≥ 0 except the last 2 km before touchdown; ≥ 300 m clearance outside 5 km from the runway |
| V11 | Performance | C | Scripted captures at the ten §1.2 anchors at 11:00 and 18:00, portrait and landscape: frame time, main-thread ms, triangles, SRP batches and memory within §10.4 on the reference device per tier; a 20-minute soak (G1, G2) |
| V12 | Audio | C | DSP < 10% of a core on Low; 0 dropouts in 20 minutes; resident ≤ tier cap; LICENSES.md has a CC0/PD row for every shipped recording (CI script: every `.wav/.ogg` under `Audio/` maps to a row) |
| V13 | Coverage | E | D13 report: T2+ for every in-scope category in CONTENT_COVERAGE §5.1; 100% of valley religious, heritage and monument POIs render something; no unhidden building intersects a hero footprint |

Acceptance captures (§1.2) are stored as PNGs per tier and orientation, with an automatic counter overlay (triangles, batches, agents, voices). The owner reviews them at each stage gate.

### 10.7 Cultural review checklist (gate for stage 2; ROADMAP 1.15)

1. Door yaw of every [V] hero (§3.4) checked against imagery; post-2015 reconstruction state as of 2026 for Maju Dega, Trailokya Mohan, the Basantapur tower and Rato Machhindranath Bungamati.
2. Generic strut panels, guardian order (Nyatapola), chaitya Buddha order, Buddha eyes with the "१" nose.
3. W2-O1 implementation: sanctums closed, `entry_rule` cards worded respectfully in EN and NE; Pashupati courtyard behaviour; no cremation; aircraft audio cap at Pashupati.
4. Prayer flags only at Buddhist sites and Thamel; clockwise kora; prayer wheels clockwise with the right hand.
5. Clothing: daura suruwal (8 ties, 5 pleats), dhaka topi shape, hakupatasi only in Newar towns, sadhus and monks dignified with no photo mechanic, porters cheerful with no exploitation mechanic.
6. Sacred audio: no recorded chants or bhajan vocals; great-bell schedule; the Bhaktapur "barking dog" bell easter egg only with consent.
7. Kumari never shown; Indra Jatra chariot (later) with a closed canopy; Akash Bhairab and Seto Bhairav masks shown only behind their lattices; the Annapurna purnakalash and every other sanctum image never modelled.
11. Rows 42–52 (§3.4): tier counts and door yaws of the added heroes (Jana Bahal tiers, Rudrayani, Uma Maheshwar, Balkumari "faces north"), and Dakshinkali and Guhyeshwari presented without sacrifice or ritual detail.
8. Skin swatches numbered, never named after groups.
9. Liveries checked against every KTM carrier; no Red Cross emblem on ambulances; no military aircraft.
10. One romanisation for Newar terms in UI text (sanjhya, tikijhya) chosen by a Nepal Bhasa reviewer.

### 10.8 Open questions (defaults apply if unanswered)

| # | Question | Default |
|---|---|---|
| Q1 | Does W2-O1 include the Taleju compounds that open once a year in reality? | Yes (owner override); an `entry_rule` info card explains the real custom |
| Q2 | Bhaktapur core: are any private cars allowed today? | No AI cars; the player parks at the edge [V municipal rule] |
| Q3 | The zebra module (500/500 mm) and the yellow-black kerb paint | White zebra bars; black-and-white kerbs until a photo check |
| Q4 | Ring Road north: model as mapped (4 lanes, 14 m) or as the signed Phase 2 (8 lanes)? | As mapped (snapshot rule) |
| Q5 | Helicopter movements per day (assumed ≈ 50) and current runway-20 use | As §8.2; tune after the [V] checks |
| Q6 | Portrait walking camera 6.8 m | Ship in stage 1; revisit in the W3 device pass |

---

## Sources

Every fact above is taken from the eight W2 research files, which list their sources with URLs. The principal ones:

* Houses: World Housing Encyclopedia report 99 (https://world-housing.net/report-99-traditional-nawari-house-in-kathmandu-valley/); FAO Bhaktapur Development Project (https://www.fao.org/4/l2680e/l2680e01.htm); Kirtipur by-laws (https://kirtipurmun.gov.np/storage/01K56DSTRMWM9WHMTXDTYYQJ8M.pdf); Wikipedia "Newar window" (https://en.wikipedia.org/wiki/Newar_window); Nepali Times on jhingati (https://nepalitimes.com/protecting-kathmandu-s-historic-roofscape).
* Temples: Wikipedia Boudhanath, Swayambhunath, Pashupatinath, Nyatapola, Kumbheshwar, Dharahara, Changu Narayan, Budhanilkantha (https://en.wikipedia.org/wiki/Boudhanath etc.); Nepal Traveller on Krishna Mandir (https://nepaltraveller.com/sidetrack/krishna-mandir-a-marble-of-stone-carving); Shanker Hotel on Taleju (https://www.shankerhotel.com.np/blog/2025/5/23/taleju-temple-the-sacred-heart-of-kathmandus-spiritual-legacy).
* Roads: Nepal Road Standard 2070 (https://dor.gov.np/home/publication/general-documents/force/nepal-road-standard-2-7); Nepal Urban Road Standard 2076 (https://download.hermes.com.np/wp-content/uploads/sites/12/2023/07/Urban-Road-standard-2076.pdf); DoR Road Safety Note 5 (https://dor.gov.np/home/publication/traffic-safety/force/delineation-measures); IRC:35-2015 (https://law.resource.org/pub/in/bis/irc/irc.gov.in.035.2015.pdf); Kathmandu Post on Ring Road phase 1 (https://kathmandupost.com/valley/2019/01/29/china-formally-hands-over-kalanki-koteshwor-project).
* Traffic and life: Meroauto fleet data (https://www.en.meroauto.com/digitization-of-vehicle-records-underway-in-kathmandu-valley/); CEN mobility factsheets (https://www.cen.org.np/uploads/doc/poster-urban-mobility-kathmandu-60b9fc8544142.pdf); no-horn rule (https://kathmandupost.com/valley/2017/04/15/no-horn-regulation-comes-into-effect); ICAM dog survey (https://www.icam-coalition.org/wp-content/uploads/2019/08/1-Dog-population-assessment-Kathmandu.pdf); urban bird count (https://english.onlinekhabar.com/urban-birds-up-kathmandu.html); Shivapuri-Nagarjun NP (https://en.wikipedia.org/wiki/Shivapuri_Nagarjun_National_Park).
* Player: Apple HIG / Material touch targets (https://blog.openreplay.com/improving-tap-targets-mobile-ux/); lean angle (https://arxiv.org/pdf/1611.03857); Daura-Suruwal (https://en.wikipedia.org/wiki/Daura-Suruwal); Dhaka topi (https://en.wikipedia.org/wiki/Dhaka_topi).
* Audio: Unity scriptable audio generators (https://docs.unity3d.com/Manual/audio-scriptable-processors-generators.html); Kenney CC0 (https://kenney.nl/assets/category:Audio); BigSoundBank CC0 (https://bigsoundbank.com/droit.html); Sonniss licence (not CC0) (https://sonniss.com/gdc-bundle-license/).
* Aviation: Wikipedia TIA (https://en.wikipedia.org/wiki/Tribhuvan_International_Airport); Ekantipur 2025 traffic (https://ekantipur.com/news/2026/01/14/en/tribhuvan-international-airport-to-handle-over-97-million-passengers-138000-flights-in-2025-27-21.html); 14 CFR §25.1385–1401 (https://www.ecfr.gov/current/title-14/chapter-I/subchapter-C/part-25/subpart-F).
* OSM data © OpenStreetMap contributors (ODbL), `pipeline/data/raw/osm/nepal.osm.pbf` (2026-10-02 snapshot); the municipality relation ids in §2.1 were read from it for this design with pyosmium.
