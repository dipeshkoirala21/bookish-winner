# Swayambhunath (स्वयम्भू महाचैत्य): structure-by-structure reference brief

Package `mon-swayambhu` (heritage research, W2 detail pass, monuments round). Owner: "National Heritage and Monuments
sites should look exactly as in real life ... do an in-depth research about the places ... try to draw how each of these
monuments looks from each angle and carefully design the 3D model of it." This brief is what the modeller builds
Swayambhu hill from: the site plan, every structure with its numbers, colours and details, what the game draws today,
and a must-have checklist per structure. Cartoon style may simplify surfaces only; silhouettes, proportions, tiers,
roof forms, gilding, colours and the arrangement of the hill must match the place.

Tags: **[O]** OSM (valley extract of 2025-10, checked against `api.openstreetmap.org/api/0.6/map` for the platform bbox
85.2894,27.7141 - 85.2914,27.7160 on 2026-10-11: no changes), **[A]** measured on aerial imagery (Google z20 0.132 m/px,
Esri z19 0.264 m/px; the imagery sits about 5.5 m E and 1.5 m N of OSM and is corrected for that), **[P]** read off
photos (scaled by the 6.2 m harmika or a person), **[S]** published source, **[E]** estimate, **[V]** to verify on a
current photo or on site, **[G]** what the game draws today (base a87e5a4).

Reference material (outside the repo, never copied in): photos `/home/user/wt/refs/mon-swayambhu/<topic>/` and the
older sets `refs/temples/swayambhu*`, `refs/shots/swayambhu*`, `refs/final-visual/swayambhu` (Openverse, credits in each
`credits.txt`, list in §10); aerial crops and the official site map in `refs/mon-swayambhu/sat/` and `refs/mon-swayambhu/web/`;
OSM-over-aerial plans `/home/user/wt/previews/mon-swayambhu/site_plan_platform_osm.jpg` (platform, labelled ids) and
`site_plan_hill_osm.jpg` (whole hill, stairs in red); current-game renders `previews/mon-swayambhu/current/`;
photo-vs-game pairs `previews/mon-swayambhu/pairs/cmp_*.png`.

---

## 0. Verdict: what is wrong today (in order of how much it hurts recognition)

| # | In the game now [G] (renders in `current/`, pairs in `pairs/`) | Reality |
|---|---|---|
| 1 | The OSM polygon **"Dorje" w1179993484** (historic=pillory, building=yes) at the **top of the east stair** is drawn as a **12.5 m, 4-storey yellow modern house with water tanks**, right beside the vajra (`cmp_stairhead.png`). Next to it a teal 4-storey block (**Param Bhairav** w906619390). | The stair head is an **open paved terrace**: the gilt vajra on its drum between two stone lions, the two white shikharas left and right, the stupa's east shrines behind (§3.2). Nothing on the platform is over 3-4 storeys; the shrines are 1-2 storeys. |
| 2 | The stupa is a parametric template: plain white dome, small box shrines with three spikes, a stack of yellow discs, dark cords for flags. No prayer-wheel gallery, no Tara shrines, no torana panels with Buddhas, no drapery. | A white dome with **saffron lotus arcs**, a gilt repoussé **harmika** with the eyes, the "?" nose and the red third eye, green-red-yellow-blue **drapery** under a fringed cornice, **gilt pentagonal toranas** with seated Buddhas on all four faces, a cone of **13 gilt rings with black grooves**, a gilt crown with a **yellow cloth skirt** and a tall gajur; **10 gilt shrines** (5 Buddhas, 5 Taras) with tiered pinnacle crowns round a **black-iron prayer-wheel gallery** (§3.1). |
| 3 | **Hariti (Ajima) temple** is a generic red-brick 2-tier pagoda with brown tile roofs on a 1.5 m brick plinth with a stone stair and lions (`cmp_hariti.png`). | A **fully gilt** 2-tier pagoda: gilt-copper roofs with curled corners and a hanging gilt leaf fringe, a **red pleated valance** under the lower eave, gilt lattice upper storey, gilt facade with two yellow lattice windows, a long gilt pataka, an ornate gajur; low plinth, worshippers kneel at the door (§3.5). |
| 4 | **Karmaraja Mahavihar** (big white monastery NE of the stupa, w115376616) and the **Drukpa Kagyu monastery** (pink, gilt roof, W) are **missing** (OSM outlines carry no `building` tag); Karmaraja's site holds a 2 m-wide sliver "Shop and markets". Instead OSM `building=yes` outlines without levels become **4-6-storey modern pastel blocks** ringing the platform (inferred 12.5-18.5 m), two of them (w904424689/690) on the open south terrace where the aerial shows **no building at all** (`current_site_bird_se.png`). | The platform is ringed by low **2-3-storey brick and whitewashed monastery and shop buildings** with pitched or flat roofs; the two big monasteries are distinctive (white flat-roofed Tibetan block NE; salmon-pink gompa with a gilt roof W) (§3.13-3.16). |
| 5 | The **vajra** is a 1.5 m stone-block drum with three gilt balls on top; the 365-step stair is a dark ramp of slabs with no walls, statues or rails beyond side kerbs (`cmp_stair_upper.png`). | A **2.2 m gilt vajra** with curving prongs on a gilt **dharmadhatu mandala** lid over a round carved stone drum (12 calendar animals); the stair is a **4-5 m wide** stone flight with a **central and two side handrails**, whitewashed side walls with merlon coping, painted **vahana statues** in pairs, and three **big painted Buddhas** at the foot (§3.2, §3.18-3.19). |
| 6 | The hero's five shrine boxes stand at bearings ≈ 5°, 95°, 185°, 268° and **320° (NW)**: the fifth (meant as Vairocana, coded as "south-east") lands in the north-west, where Arya Tara is; the five **Tara** shrines are absent. | Akshobhya and Vairocana are a **pair on the east face** (Vairocana just south of Akshobhya), and five Tara shrines stand between (§3.1.4). |
| 7 | **Amideva Buddha Park**: the three 20 m gilt statues are three 6-7 m plain boxes (grey, white, red-roofed) in a lawn beside a tall beige block (w341677213, 11 × 35 m, inferred 12.5 m and standing on a slope, reads as 6 storeys) (`cmp_amideva.png`). | Three giant **gilt seated figures** (Chenrezig, Shakyamuni, Guru Rinpoche) on lotus thrones over painted plinths, on a raised terrace behind a prayer-wheel wall on the Ring Road (§3.22). |
| 8 | The hill is bare grass (the forest comes from the nature package) and the platform edge has no parapet. | A **dense, closed forest** covers the whole hill; the platform sits on a **whitewashed parapet with merlon coping** on the south and east, monkeys on every wall (§2.4). |

What is right today: the stupa anchor, its 26.7 m plan, the 33 m height class, the stair line along OSM way 24707651 with
curated drops, the shikharas' positions (heroes `her.ktm.pratappur`/`anantapur`), flags from the spire, saffron arcs.

---

## 1. Sources and method

* **OSM [O]:** all ways and nodes within 1 km of the stupa from `/tmp/.../scratchpad/dp/valley.osm.pbf` (pyosmium),
  diffed against the live API for the platform (no changes, 2026-10-11). Overpass was unreachable. Key objects: stupa
  **w201223707** (man_made=tower, tower:type=stupa, 26.8 m circle, height 12, roof:height 11, `#fcecc0`), building parts
  w421790281/275 (dome-top collar 7.1/6.9 m, top 11.6/11.8 m), **w421790274** (harmika 6.2 m, to 16 m), **w421790277**
  (cornice 6.7 m, 15.1-16 m), **w421790278/279/280** (three torana panels, 4.0-4.3 m wide, 16-19 m, `#DAA520`, the east
  one is unmapped), **w421790271** (ring base 5.4 m, 16-18 m), **w234218672** (13 rings, 6.5 m, 18-30 m pyramidal),
  **w421292573** (crown 4.0 m, 26.2-28 m), **w421790270** (3.8 m disc, 25.8-26.2 m), **w421790269** (gajur 1.2 m, 28-33 m);
  kora ring **w302394468** (Ø 31 m); landuse **w115379177** (130 × 100 m, `ref:whc=121-004`); protected area
  **w256548640**; the ten shrine nodes **n10959599249, n10959612609, n10959612608, n10959618751, n10959612610,
  n10959612612, n10959612613, n10959587873, n10959612611, n10959618750** (check_date 2026-10); east stair **w24707651**
  (265 m) + head **w925755319**.
* **Official site map [S]:** swoyambhustupa.com "Map of Swoyambhu Stupa" (36 numbered monuments; reference image
  `refs/mon-swayambhu/web/map_1400.jpg`, oriented with the east stair at the bottom). Used for names and relative
  positions of everything OSM lacks (Nagpur, Shamarpa bell, chaitya groups).
* **Published [S]:** Rubin Museum, *The Svayambhu Chaitya of Kathmandu* (dome ≈ 8.5 m high, harmika ≈ 5 m, 12 m of the
  yashti above the harmika, total > 25 m, Vairocana niche "just to the south of Akshobhya", Hariti two-tiered, Shantipur,
  Vasupura); swoyambhustupa.com pages (five Buddhas with directions, colours, mudras, vahanas; Vairocana between
  Akshobhya and Ratnasambhava; two shikharas built by Pratap Malla N.S. 775, rebuilt after 2015, inaugurated
  18 Aug 2018 / completed 2019; big vajra installed N.S. 788 (1667) on a gilt dharmadhatu mandala over a round stone base
  carved with twelve animals, renovated 2008; Paschimi (Vasubandhu) chaitya); Wikipedia (Trayodashabhuvana 13 rings,
  2008-2010 regilding with 20 kg gold, east stair by Pratap Malla, SW entrance by the car road); Mona museum (vajra
  7 ft 2 in = 2.18 m); Lonely Planet via search (painted Buddhas at the foot, pairs of Garudas, lions, elephants, horses
  and peacocks on the upper flights, Anantapur collapsed in 2015); ECS Nepal *Restoring the Self-Created* (Karmaraja
  Mahavihar and the old gompa collapsed in 2015, Shantipur back wall damaged); Kathmandu Post 2017 (Shantipur murals);
  Atlas Obscura / Nepal Traveller (Amideva park: Shakyamuni ≈ 67 ft centre, Chenrezig and Guru Rinpoche ≈ 64 ft, 2003).
* **Photos [P]:** ~70 Openverse contact sheets under `refs/mon-swayambhu/` (stupa, harmika, eyes, spire, shrines,
  vajra, shikharas, Hariti, chaityas, wheels, lamps, bells, stair, foot statues, Amideva park, distant views) plus the
  earlier `refs/temples|shots|final-visual/swayambhu*` sets; all looked at.
* **Aerials [A]:** Google z20 platform mosaic with the OSM outlines overlaid (`site_plan_platform_osm.jpg`; the overlay
  fits the dome circle, both shikharas, Hariti, Agam Chhen, Vasupura and the stair after the 5.5 m / 1.5 m shift), z19
  hill mosaic, z20 crops of the stair foot, west car park, Manjushri hill and Amideva park.
* **Game [G]:** a temporary dump test (not committed) built tiles 10/514/162 and 10/515/162 of `kathmandu_core` with the
  real meshers (terrain, roads, areas, B0 buildings with the hero hide list, every curated hero at LOD0, every generic
  sacred building) and classified every building and POI within 700 m; renders with `tools/mesh-preview` from the photo
  viewpoints (§8).

---

## 2. Site frame and plan

### 2.1 Anchor and axes

* **Centre:** stupa outline w201223707 centroid **27.714931 N, 85.290391 E** = game (527212.98, 166291.64) = tile
  10/514/162 local **(876.98, 403.64)** [O][G]. All positions below are **(x east, y north) in metres from this centre**
  unless marked; bearings clockwise from true north; heights above the paved kora unless marked. Back to lon/lat:
  lon = 85.290391 + x / 98 548, lat = 27.714931 + y / 111 320; then place with `WorldFrame.LonLatToGame`.
* **Game grid vs true north:** the game frame is transverse Mercator with its central meridian at 84° E (`Tm84`), so
  at Swayambhu **true north lies ≈ 0.6° west of the game's +Z** (grid convergence; measured 0.6-0.8° between the OSM
  and game positions of the Amideva statues 610 m away). Game yaw = true bearing − 0.6°. Rotating the (x, y) table by
  +0.6° (counter-clockwise) gives game offsets; it matters beyond ~200 m (6-9 m at Amideva park).
* **Site axes are turned ≈ 10° clockwise.** The east stair arrives at bearing ≈ 100°, the vajra sits on that line, and
  the buildings round the platform are drawn on a grid turned 8-13° (Agam Chhen 99°, Dorje 98°, Gyanmala 107°,
  Hariti 102°, Shantipur 13°) [O]. The shrine nodes agree within mapping noise (Akshobhya 101°, Amoghasiddhi 5°,
  Ratnasambhava 191°). **Build the stupa's cardinal frame at "east" = true bearing 100° (game yaw ≈ 99.4°)**;
  the harmika faces, the toranas and the shrines follow it. The OSM harmika parts are drawn turned 21°; that is a mapping artefact [A][V].
* **Ground:** the platform is a level paved hilltop at game height ≈ 1402 m (the stupa base 1402.0, kora 1402-1406 in the
  Copernicus-based terrain [G]). The east stair foot is 78 m lower (curated drop; the game terrain says 84 m) [S][G].
  Real hilltop ≈ 77 m above the valley floor [S].

### 2.2 Zones from the centre outward

| Zone | Radius / extent | What is there |
|---|---|---|
| Dome + base ring | r 0-13.4 (outline Ø 26.8 [O]) | White dome (springing Ø ≈ 24.5 [A]), the prayer-wheel gallery and the 10 gilt shrines on its base. |
| Kora (inner walk) | r 13.4-15.5 (path ring Ø 31 [O]) | Grey stone-flag ring walk, clockwise; shrine forecourts with gilt vahana pairs, flagpoles, lamp pillars. |
| Inner platform | to r ≈ 25-35 | Votive chaityas, stone lamp columns, bells, the vajra (E), the two shikharas (NE, SE), Hariti (NW), the five element shrines, lamp racks. |
| Platform edge | r ≈ 30-45 S and E; 60-90 N | Whitewashed parapet with merlon coping on the S and E (curved, [A]); buildings on the W and N (museum, monasteries, shops, houses). |
| North quarter | y 20-90 | Chaitya garden (NW), Karmaraja (NE), shop and house rows, Shantipur (N end, y ≈ 78). |
| Hill | to ~300 m | Closed forest, the east stair (ESE), the SW stair (S road to platform), the W path to the car park, the circuit road (Swayambhu Parikrama) round the foot with a prayer-wheel wall on its south side. |

### 2.3 Ground, paving, walls, steps

* **Kora ring and inner platform:** grey stone flags `#9A958C` (worn, darker joints `#6E6A62`), brick strips `#A0563B`
  in places; many low stone curbs round chaitya groups [P][A].
* **Parapet:** a low (0.8-1.1 m) whitewashed brick parapet `#EDE9E0` along the S and E edges, curving (aerial), with a
  **stepped-merlon / sawtooth coping** (photos of monkeys on the wall), small white round chaityas set on it [A][P];
  retaining wall w115376896 below it on the south arc [O].
* **Retaining walls:** w115376896 (S arc, 70 m), w115376897 (NE, 15 m) [O]; rough stone-and-brick walls under the
  parapet, moss streaks.
* **Steps:** east stair w24707651 (§3.18), SW stair w142107919 (203 m, handrail, paving stones) + w135461252 (105 m,
  cobble) from the south road up to the west side of the platform, W path/steps w24684297 to the car park, short
  flights inside the platform (w925755319 stair head, w619948102, w1437108574, w1437108573) [O].

### 2.4 Hill, forest, water, people

* **Forest:** closed broadleaf canopy over the whole hill (Google z18: unbroken crowns from the parikrama road to the
  platform parapet), dark green `#2F5A2E` to `#4E7A3A`; tall trees overhang the east stair [A][P]. The nature package's
  forest must reach the platform edge and both sides of the stair (open issue O-6).
* **Water:** World Peace Pond (w201223708, round Ø 9.9 m, (-92, 104)) at the west car park, green water, a statue in the
  middle, a natural spring beside it (n2425060440) [O][A]; Aajima pond w1180014425 (44.6 × 17 m, intermittent, (-190,-100))
  in the SW forest [O].
* **Monkeys:** ≈ 450 rhesus macaques (ref_animals.md): on parapets, chaityas, shrine roofs, stair handrails and the pond.
* **Pigeons** on Hariti's roof and the kora; **prayer flags** everywhere (§3.20); **butter-lamp racks** (§3.21).

### 2.5 Plan sketch (north up = true north; site axes turned 10° clockwise; not to scale)

```
                                  N
              Shantipur (0,78) ── shop/house rows ── toilets (12,65)
       Agnipur (-38,40)      Small chaitya (-22,34)        Karmaraja Mahavihar (25..44, 14..37)
  Dipankar (-46,22)  Dev Dharma Mahavihar (-30,15)   Aananta Pur stupa (-12,20)    Param Bhairav (22,12)
                 Hariti (-18,13)    chaitya garden NW          Nagpur (~1,17)     Pratappur (18.5,5.8)   Gyanmala sattal (31,5)
  Drukpa Kagyu (-29,2)   Amitabha(W) ──────( STUPA )────── Akshobhya/Vairocana (E) ── VAJRA (26,-7) ══ EAST STAIR ══> foot (284,-51)
  Museum (-33,-13)       Vayupur (-22,-20)        Ratnasambhava (S)        Anantapur (9,-19)   Shamarpa bell (~14,-15)
            white niche chaitya (-15,-18)   Agam Chhen (-8,-22)   Vasupura (5,-25)
                          ── parapet (merlon coping), view terrace (0..25,-30..-36) ──
        W path / SW stair → car park, World Peace Pond (-92,104), Paschimi chaitya (-74,76)
```

### 2.6 Views

* **From the city (E, NE):** a forested dome of a hill; on its crest the **gilt spire** rising well above everything,
  the white dome half hidden, **two white shikharas** close beside it (from the east: Pratappur right/north, Anantapur
  left/south), pink-white-yellow monastery blocks either side. Shikhara tops reach ≈ 0.6 of the spire's height above
  the platform [P: y2015 ref_05, monastery ref_02]. At night the stupa and shikharas are floodlit gold.
* **From the south (Chhauni, Kimdol):** spire centre-left, both shikharas to its right close together, a long row of
  pink and cream monastery buildings to the left (west) [P].
* **From the stair head (E, looking W):** vajra and lions in front, Akshobhya + Vairocana shrines behind, eyes above,
  Pratappur right, Anantapur left (`cmp_stairhead.png`).
* **From the kora (S/W):** shrine fronts with dark grilles, the wheel gallery, lamp columns, chaitya forest, Hariti's
  gilt roofs (NW), the white niche chaitya (SW).
* **From the platform outwards:** the whole Kathmandu valley to the E with the Himalaya on clear days (sky package).

---

## 3. Structure inventory

Summary (positions are centroids; G = game today):

| # | Structure | OSM | Position (x,y) / bearing, distance | Plan (m) | Height (m) | G today |
|---|---|---|---|---|---|---|
| 3.1 | Great stupa (Swayambhu Mahachaitya) | w201223707 + parts | (0,0) | Ø 26.8 | ≈ 33 | hero `her.ktm.swayambhunath` (template) |
| 3.1.4 | 5 Buddha + 5 Tara shrines | 10 nodes | ring r ≈ 12.5 | 1.8-2.6 wide | 3.5-5 | 5 hero shrine boxes; Taras missing |
| 3.2 | Big vajra + drum + lions | w1179993484 "Dorje" | (26,-8), 108°, 27 m | drum Ø ≈ 2.4 | ≈ 2.6 | hero (simple) **+ a 12.5 m house on it** |
| 3.3 | Pratappur shikhara | w115376623 (+w421292577) | (18.5,5.8), 73°, 19 m | 7.9 | ≈ 20 | hero (generic plaster shikhara) |
| 3.4 | Anantapur shikhara | w255752871 (+w421292578) | (9.2,-19.4), 155°, 21 m | 8.8 | ≈ 20 | hero (same template) |
| 3.5 | Hariti (Harati Ajima) temple | w257581957 (+4 parts) | (-18.4,13.5), 306°, 23 m | 6.5 | ≈ 10 | generic red-brick pagoda |
| 3.6 | Agam Chhen (secret ritual house) | w257590740 (+2 parts) | (-8.3,-22.4), 200°, 24 m | 8.6 × 5.4 | 7 | plain house (flat roof + hipped part) |
| 3.7 | Vasupura (Basundhara, earth) | w257590741 (+2 parts) | (5.0,-25.3), 169°, 26 m | 7.5 × 4.0 | 4.1 | plain building (Gompa archetype) |
| 3.8 | Vayupura (wind) | w257747924 | (-22.0,-19.7), 228°, 30 m | 4.1 × 3.1 | 3.3 | plain building |
| 3.9 | Agnipura (fire) | w904938037, n2615946693 | (-37.8,39.7), 316°, 55 m | 2.7 × 3.9 | ≈ 3 | plain house 3.5 m |
| 3.10 | Nagpura (water) | none [S map 19] | ≈ (1,17) N of the kora [S][V] | ≈ 2 × 2 | sunken | missing |
| 3.11 | Shantipur (sky) | w257736697 (+2 parts) | (0.5,78.3), 0°, 78 m | 17.0 × 11.7 | 5 | plain (Gompa archetype, 0.1 m tag) |
| 3.12 | Pavan/Param Bhairav | w906619390 | (21.7,12.7), 60°, 25 m | 6.3 × 4.6 | ≈ 4-5 [E] | 4-storey teal block |
| 3.12 | Yogamvar | w904433284 (monument) | (-21.7,71.6) | 5.2 × 3.0 | [V] | missing |
| 3.13 | Karmaraja Mahavihar | w115376616 (no building tag) | (30.5,27.9), 47°, 41 m | 16.6 × 18.6 | ≈ 10-12 [E] | **missing** |
| 3.14 | Deva Dharma Mahavihar | w115376612 | (-30.5,15.4) | 12.7 × 6.0 | ≈ 9 [E] | 6-storey block 18.5 m |
| 3.15 | Drukpa Kagyu monastery | w1179996912 (monument) | (-29.1,2.3) | 13.1 × 14.7 | ≈ 7-9 [E] | **missing** |
| 3.16 | Gyanmala Bhajan sattal | w257593927 (+2 parts) | (30.9,5.7), 80°, 31 m | 8.9 × 6.0 | 5.25 | plain flat-roof house |
| 3.17 | Swayambhu Buddhist Museum | w255881416 | (-32.9,-13.2) | 18.6 × 6.7 | 3.5 | institutional box |
| 3.17 | Ring of houses and shops | many building=yes | N, NW, W | 4-15 m | 2-4 storeys | **4-6-storey pastel blocks** |
| 3.18 | East stairway (365 steps) | w24707651, w925755319 | (35,-7) → (284,-51) | 4-5 wide | -78 | hero ramp (slabs) |
| 3.19 | Painted Buddhas, mane wheels at the foot | n10959703544, w1180013332, w908430314, w1180013656 | (113,-24); (276,-83); (265..268,-89..-99) | | | missing |
| 3.20 | Prayer flags | | spire → platform | | | hero cords (16-20 lines) |
| 3.21 | Chaityas, lamp columns, bells, lamp racks | w1179992315, w1179997380, w906619392 ... | platform | | | 2 generic chaityas; rest missing |
| 3.22 | Amideva Buddha Park | w1158356596, statues w619948098/099/100 | (-606,133) | 90 × 78 park | statues ≈ 20 | 3 boxes + a tall block |
| 3.23 | Paschimi (west) chaitya, World Peace Pond, car park | w619948088, w201223708, w1180009693 | (-74,76), (-92,104), (-123,74) | | | generic small stupas |
| 3.24 | Manjushri (Saraswati) shrine, west knoll | w256276407 | (-265,113) | 10.6 × 6.8 | [V] | plain 3-storey box |

### 3.1 Great stupa (Swayambhu Mahachaitya) - hero

The oldest and holiest stupa of the valley; built over a protruding rock (3.3 m) that the dome encases [S Rubin];
renovated about twice a century, last major regilding 2008-2010 (20 kg gold, Tibetan Nyingma Meditation Center) [S].
The yashti (sal pole, 1817) rises through the harmika and rings to the crown [S].

#### 3.1.1 Section (heights above the kora) - recommended

| Element | From → to | Size | Source |
|---|---|---|---|
| Base plinth (white, with the wheel gallery on it) | 0 → 1.0 | outer Ø 26.8 | [O] outline; [P] |
| Dome (anda), whitewashed | 1.0 → 11.6 | springing Ø 24.5, rise 10.6 (0.43 Ø) | [A] Ø; [O] 1 → 12; Rubin says ≈ 8.5 m high [S] - photos show a near-hemisphere, keep 0.42-0.44 Ø |
| Dome-top collar (square, two steps) | 11.6 → 11.8 | 7.1 → 6.9 square | [O] w421790281/275 |
| Harmika box (eyes) | 11.8 → 15.1 | 6.2 square, 3.3 high (h/w 0.53) | [O] w421790274; [P] h/w 0.52 |
| Cornice with fringe | 15.1 → 16.0 | 6.7 square (0.25 m overhang), fringe hangs 0.5 m | [O] w421790277; [P] |
| Torana panels, one per face | 16.0 → 19.0 | 4.2 wide, pentagonal (gabled top), 0.3 thick | [O] w421790278-280; [P] |
| Ring base (square plinth inside the toranas) | 16.0 → 18.0 | 5.4 | [O] w421790271 |
| 13 rings (Trayodashabhuvana) | 18.0 → 26.0 | Ø 6.5 → ≈ 3.0; 13 equal bands, 0.6 m pitch | [O] w234218672; [S] 13 |
| Disc under the crown | 25.8 → 26.2 | Ø 3.8 | [O] w421790270 |
| Crown (chhatra, bell-shaped gilt drum) + yellow skirt | 26.2 → 28.0 | Ø 4.0; skirt hangs 0.5-0.7 below the rim | [O] w421292573; [P] |
| Gajur (finial) with a small parasol | 28.0 → 33.0 | Ø 1.2 at the base; parasol Ø ≈ 1.0 at ≈ 31 m | [O] w421790269; total "> 25 m" [S], telephoto ≈ 30-33 [P] |

Silhouette rule: the gilt part (harmika + rings + crown + gajur, 11.8 → 33 m) is **about twice the dome's rise** and
reads as a tall tapering cone with a "head" on top; the dome is a full, slightly squashed hemisphere, not a flat cap.

#### 3.1.2 Dome

* Whitewash `#F3F1EB`, rain streaks `#D8D2C4` running down, grey grime at the foot `#BDB7AB`.
* **Saffron lotus arcs:** a ring of 12-16 large shallow scallops in saffron/yellow ochre `#E2A62A` round the upper
  third of the dome, petals hanging from the collar, each scallop with a second inner line; **drips** of the same
  colour run down from each cusp (the colour is thrown from buckets on festival days) [P: stupa ref_08, harmika ref_08,
  ref_09]. The current saffron arcs [G] are in the right spirit but too thin and too few.
* The dome base meets the gallery roof; no niches in the dome itself (the shrines stand in front of it).

#### 3.1.3 Prayer-wheel gallery on the base

* Between the shrines, all round: a **white base wall** 0.7 m with **black cast-iron scroll grilles** (big S-curves and
  circles) in front of it, a row of **copper/brass prayer wheels** (Ø 0.18-0.22 m, 0.30 m tall, embossed *Om mani padme
  hum*, `#9A5B34` / brass `#B58A3A` on a wooden spindle, pitch 0.35 m) at 0.8-1.2 m, a black wooden top bar, black
  posts every ≈ 2 m and a **small black sloping canopy roof** (0.4 m deep) over the wheels at 1.8-2.0 m [P: wheel2 ref_08,
  entrance ref_02/09]. ≈ 120-150 wheels in total [E]. Frame `#1C1C1C`.
* Pilgrims walk clockwise and spin them (gameplay: spin on touch while walking clockwise, ASSET_MANIFEST).

#### 3.1.4 The ten shrines (5 Dhyani Buddhas, 5 Taras)

Positions from OSM nodes [O] except Vairocana (see note); radius ≈ 12.5 m; each faces outward along its radius.

| # [S map] | Shrine | Bearing (site) | OSM (x,y) | Type | Buddha colour / vahana pair [S] |
|---|---|---|---|---|---|
| 3 | Akshobhya | E (100°) | (12.3,-2.5) | Buddha shrine | blue, earth-touching / **elephants** |
| 4 | Vairocana | **just S of Akshobhya** (≈ 115°) | node at (12.4,1.9) is **north** - wrong [S Rubin, map, swoyambhustupa] | Buddha shrine | white, dharmachakra / **lions** |
| 10 | Vajradhatvishvari Tara | ESE (≈ 125°) | (11.3,-5.2) | Tara shrine | consort of Vairocana |
| 8 | Mamaki Tara | SE (≈ 145°) | (7.4,-9.8) | Tara shrine | |
| 5 | Ratnasambhava | S (190°) | (-2.5,-12.5) | Buddha shrine | yellow, boon-giving / **horses** |
| 9 | Pandara (Padmani) Tara | SW (≈ 235°) | (-9.0,-8.5) | Tara shrine | |
| 1 | Amitabha | W (280°) | (-12.7,-0.1) | Buddha shrine | red, meditation / **peacocks**; the principal image of Swayambhu [S] |
| 6 | Arya Tara | NW (≈ 325°) | (-9.0,8.7) | Tara shrine | |
| 2 | Amoghasiddhi | N (10°) | (1.1,12.8) | Buddha shrine | green, fearlessness, seven-hooded naga canopy / **garudas** |
| 7 | Saptalochani (Lochana) Tara | NE (≈ 55°) | (9.7,7.2) | Tara shrine | |

Fix Vairocana to the east face, about 4.5 m south of Akshobhya (≈ (11.5,-6.5) in site axes), the pair reading as one
double shrine (`stupa ref_08`); move Vajradhatvishvari a little further south (≈ 135°) to make room [V on a photo
of the east face].

**Buddha shrine (×5)** [P: stupa ref_07, entrance ref_09, y2015 ref_01]: a gilt-copper shrine built against the dome
on a **white masonry plinth** 0.9-1.0 m high (an iron-grilled offering hole in front); body **2.4-2.6 m wide**, projecting
**1.8 m** from the dome, 2.2 m tall: two pilasters, a **dark iron lattice window/door** (1.0 × 1.3 m) with the painted
Buddha visible inside (colours above), two pairs of small gilt standing figures in side niches, an ornate **gilt torana
arch** with a kirtimukha/garuda over the door; a projecting cornice; then **three receding stepped stages** (pyramid)
crowned by a **cluster of three gilt pinnacles** (small chaitya-shaped spires with umbrellas). Total ≈ 5 m to the
pinnacle tips. In front, on the kora edge: a **pair of gilt vahana figures** on low pedestals (≈ 0.8 m) and a pair of
**gilt banner poles** (dhvaja) ≈ 3 m with flat gilt flag plates. Gilt `#D6A934`, hi `#F2CF6A`, lo `#8A6620`; plinth
`#EDEBE4`; grille `#1A1A1A`.

**Tara shrine (×5)** [P: stupa ref_08 left, harmika ref_06 right]: the same idea, smaller: **1.8 m** wide, 1.4 m deep,
**≈ 3.5 m** tall, one stage less, crowned by a **single** domed pinnacle; no banner poles.

#### 3.1.5 Harmika and the eyes

* Box 6.2 × 6.2 × 3.3 m of **gilt repoussé copper**, a faint grid of square plates (0.5 m) with low relief rosettes
  [P: general ref_06, harmika ref_00].
* **Eyes** on all four faces: centred ≈ 0.33 of the box height above its base (low on the face); each eye ≈ 1.15 m wide
  (0.19 of the face), almond, white `#F5F3EE` with a brown iris `#4A3526` and a dark ring, upper lid and outline
  blue-black `#1F2E5E`, a long **arched brow** in blue-black above each eye; between the brows the **urna (third eye)**,
  a red `#C62828` dot in a gilt ring; under it the **nose: the Nepali numeral १ (a "?" curl)**, white with a red-maroon
  outline, its tail reaching the dome [P][S]. No red lids.
* **Drapery** under the cornice: a deep **green** valance `#2E8B57` on the face, with a broad sash of **red `#C8102E`,
  yellow `#F4C430` and blue `#1E5AA8` stripes** hanging in swags from the corners [P: harmika ref_08, general ref_06,
  stupa ref_07]. Thin flag cords cross in front.
* **Cornice** 6.7 m: overhanging gilt moulding with a **lace-like hanging fringe** of small gilt leaves; small gilt
  figures along it; a **gilt kalasha finial at each corner** [P].

#### 3.1.6 Torana panels, rings, crown, gajur

* **Torana panels (×4, one per face):** gilt pentagonal plates (a rectangle with a pointed gable top, the gable ≈ 40 %
  of the height), 4.2 m wide, 3 m tall, standing on the cornice in front of the ring base; a **large seated Buddha in
  a niche at the top** and a **row of five small seated Buddhas** across the bottom, scroll borders, small gilt
  pinnacles at the shoulders [P: general ref_06]. The east panel is unmapped in OSM - all four exist.
* **13 rings:** each a **rounded gilt band** (a torus-like bulge) separated by a **deep black groove** `#2B2014`
  (groove ≈ 25 % of the pitch), stacked as a straight-sided cone from Ø 6.5 to Ø 3.0 m; gilt slightly darker and
  browner than the harmika `#B8902E` [P: stupa ref_07/08].
* **Crown:** a gilt **bell-shaped drum** (Ø 4.0, 1.8 m high) with an umbrella rim; a **yellow cloth skirt** `#F2C230`
  hangs from under the rim (0.6 m) [P: stupa ref_07/08, refs/temples brief].
* **Gajur:** gilt vase (kalasha) + small gilt umbrella + spike, 5 m in all; flag lines tie on just below the crown.

#### 3.1.7 Views of the stupa

* **N, S, W:** the same composition each way (eyes, torana, central Buddha shrine with its vahana pair, Taras either
  side); W (Amitabha) is the principal face; N has the Amoghasiddhi shrine with its naga hood.
* **E:** the double shrine (Akshobhya + Vairocana) behind the vajra; the stair axis meets the stupa slightly south of
  centre on the double shrine.
* **From above:** a white disc with a gilt ring of shrines and a gilt square in a gilt star of flag lines.

#### 3.1.8 In the game now [G]

Hero `her.ktm.swayambhunath`, 14,549 triangles LOD0 incl. stair and vajra (`current_stupa_views.png`): Ø 26.7 dome on a
thin white drum ring, rise 11 (ok), 5 shrine boxes 2 × 1.5 × 3 m with a dark square opening and three yellow spikes
(at bearings ≈ 5°, 95°, 185°, 268° and 320°: the fifth lands NW); harmika gilt with eyes and a red band, a stack of
round yellow discs (no grooves to speak of), a yellow cylinder crown and a small finial; 20 flag cords as dark lines
with flag rows; thin saffron arcs. **Missing:** prayer-wheel gallery, Taras, torana panels, cornice fringe and corner kalashas, drapery colours, nose and urna detail,
crown skirt, vahana pairs, banner poles; Vairocana misplaced; the harmika is not turned with the site axes.

#### 3.1.9 Must-have checklist (stupa)

- [ ] Section as §3.1.1 (gilt part ≈ 2 × dome rise; total ≈ 33 m); site frame turned 10°.
- [ ] Whitewashed dome with 12-16 saffron scallops + drips; grime at the foot.
- [ ] Wheel gallery: white base, black scroll grilles, copper wheels, black canopy, between the shrines all round.
- [ ] 5 Buddha shrines (plinth, gilt body, black lattice, torana, 3 stages, 3 pinnacles, vahana pair, two banner poles).
- [ ] 5 Tara shrines (smaller, single pinnacle); Akshobhya + Vairocana as the east double shrine.
- [ ] Gilt repoussé harmika; eyes low on the face; blue-black brows; red urna; white "१" nose with red outline.
- [ ] Green valance + red/yellow/blue swags; fringed cornice; corner kalashas.
- [ ] Four gilt pentagonal toranas with one big and five small Buddhas.
- [ ] 13 bulging gilt rings with black grooves in a straight cone Ø 6.5 → 3.0.
- [ ] Bell-shaped gilt crown with yellow skirt; tall gajur with a small umbrella.
- [ ] Flag lines from under the crown in all directions (§3.20).

### 3.2 Big vajra (Dorje) at the stair head, with lions, bell and the stair landing

* **Position:** on the stair axis at the head of the east stair, ≈ 26 m from the centre (OSM "Dorje" polygon centroid
  (25.8,-8.5), stair head (35.1,-6.9)) [O][A]; the vajra lies **across** the axis (N-S), so a climber sees it broadside
  [P: shots/swayambhu_top ref_06, ref_01].
* **Vajra:** gilt bronze, **2.18 m** long [S], central sphere Ø ≈ 0.45 m, lotus collars, **five prongs at each end**
  (four curving outer prongs meeting at a central prong, makara heads at the roots), held up off the lid on short
  supports. Gilt `#C7A048`, worn to brown `#8C6A2E`; khata scarves and red/yellow cloth tied on [P].
* **Pedestal:** a gilt copper **dharmadhatu mandala lid** (round, Ø ≈ 2.2 m, a raised mandala plate) [S] on a **round
  carved stone drum** Ø ≈ 2.4 m, ≈ 1.6 m high: a lotus-petal moulding and bead band at the top, a deep band with the
  **twelve animals of the (Tibetan) calendar** in relief, a plinth ring [S][P]; stone grey-green with copper-green
  patina `#4F6B5B`, `#5E5A52`. Total height to the vajra top ≈ 2.6 m.
* **Lions:** two big seated stone **lions** on pedestals ≈ 3 m either side of the vajra (N and S), facing E down the
  stair [P].
* **10th Shamarpa's bell** (map 23): a large bronze bell hung under a stone lintel on two stone posts near Anantapur
  between it and the vajra (≈ (14,-15), SE) [S map][P: bell2 ref_06/07][V]. Other bronze bells in stone frames round
  the platform.
* **Stair landing:** the last flight arrives between buildings onto a small paved terrace; a ticket booth n13305239104
  (27.8,-6.4) and a few stalls stand at the top [O].
* **In the game now [G]:** a 1.5 m drum of stone blocks with a toy vajra of three gilt balls (`current_vajra.png`), plus
  the **12.5 m yellow house** drawn from w1179993484 right on top of the landing (`cmp_stairhead.png`).
* **Must-have:** [ ] hide/replace w1179993484 (no building there); [ ] 2.2 m vajra with curved five-pronged ends,
  across the axis; [ ] gilt mandala lid + carved stone drum with the animal band; [ ] two stone lions; [ ] Shamarpa bell
  frame near Anantapur; [ ] open paved landing.

### 3.3 Pratappur shikhara (north of the stair head)

* Built by **King Pratap Malla** (N.S. 775, ≈ 1655) with Anantapur to give the stupa a "majestic" east front [S];
  damaged by fire 2008, lightning 2011, dismantled after 2015 and **rebuilt to the old design (2018-2019)** [S].
* **Position:** (18.5,5.8) [O][A], 19 m from the centre at 73°, NE of the stair head; doors usually closed [S].
* **Form:** a white **plastered curvilinear (Nagara) shikhara** on a square base [P: mon-swayambhu/shikhara ref_07 (Anantapur),
  harmika ref_06/08, stairs ref_04, entrance ref_05/07]:
  * Base: a stepped plinth (2 steps, 0.6 m) under a **square cella storey ≈ 7.9 m** with an **open pillared verandah**
    (corner pillars, a corridor round the cella) and a small door on each side; a **gilt embossed frieze band** (≈ 0.5 m)
    above it, a projecting **maroon `#7B2A23` cornice band**.
  * **Corner turrets:** four miniature shikharas at the corners of the spire's base, each with a gilt-and-maroon cap.
  * **Spire:** tall curvilinear tower, ≈ 0.75 of the base width at its foot, with **vertical rathas** (3-5 offset
    ribs per face, rounded) and fine horizontal mouldings, rows of **small dark square vents** up the ribs, a small
    projecting **pedimented niche** (gilt image) on the face at mid-height; near the top a gilt band and a maroon ring.
  * **Crown:** a white ribbed amalaka cushion, then a **gilt kalasha and spike** (OSM part w421292577: Ø 2.4, pyramidal
    to 20 m).
* **Size:** base 7.9 × 7.8 [O]; **height ≈ 20 m** to the gajur (OSM 17 + crown part to 20) [O]; telephoto: shikhara top
  ≈ 0.6 of the stupa top above the platform [P] (consistent).
* **Colours:** white `#F4F2EC`, gilt band `#D4A437`, maroon `#7B2A23`, dark vents `#3A3632`; a red-saffron pennant on
  the finial on festival days [E].
* **G today:** hero `her.ktm.pratappur` (3,647 tris): white ribbed spire on a cella with arched doors, four smooth
  dome-like turrets, a red pennant, 18.2 m. **Missing:** verandah pillars, gilt frieze band, maroon cornice, vents,
  face niche, amalaka; turrets should be mini-shikharas, not domes.
* **Must-have:** [ ] square plinth + verandah storey with corner pillars; [ ] gilt frieze + maroon cornice; [ ] four
  mini-shikhara corner turrets; [ ] ribbed curvilinear spire with vents and a face niche; [ ] amalaka + gilt kalasha,
  20 m; [ ] the twin of Anantapur (same design, slightly smaller).

### 3.4 Anantapur shikhara (south of the stair head)

* Named after Pratap Malla's queen **Ananta Priya**; a stone inscription below it praises Ekajata Vajrayogini [S].
  **Collapsed in the 2015 earthquake**, rebuilt (90 % in 2018, inaugurated 2018-2019) [S].
* **Position:** (9.2,-19.4) [O][A], 21 m at 155°, SE of the stupa, south of the stair head.
* **Form, colours:** as Pratappur (§3.3); base **8.8 m**, ≈ 20 m tall [O]; the photo `shikhara ref_07` (markhorrell)
  is this tower: white, gilt frieze band with scroll ornament over the verandah storey, maroon band, corner turret with
  a gilt gable, dark vents, face niche, white amalaka, gilt kalasha.
* **G today:** hero `her.ktm.anantapur`, identical to Pratappur (`cmp_anantapur.png`).
* **Must-have:** as Pratappur; base 8.8 m; the bell frame and the inscription stone below it on the stupa side.

### 3.5 Hariti (Harati Ajima) temple

* Goddess of smallpox and protector of children (Ajima, Sitalamaju); her image holds five children [S]; worshipped
  daily by Hindus and Buddhists; the stupa's protector temple [S].
* **Position:** (-18.4,13.5), NW of the stupa [O][A]; plan **6.5 × 6.5**, turned with the site grid (sides at 102°/12°)
  [O]; main door faces the stupa/east [P][V].
* **Form** [P: hariti2 ref_04, ref_05, hariti ref_02]:
  * **Two-tier pagoda, both roofs gilt copper** `#C9A23C` with raised seams, **upturned curled corner tips** (dragon
    curls) and a **hanging fringe of small gilt leaves/bells** along the upper eave; **red pleated cloth valance**
    `#B3141B` with gold trim along the lower eave (≈ 0.4 m deep).
  * Under the eaves: gilt carved struts and **gilt lattice** panels; upper storey a gilt lattice box (OSM parts:
    upper storey 4.5 m square 3-6 m, top roof 6.5-8 m `#F0E68C`) [O].
  * Ground storey: a gilt facade of three bays: centre door with an ornate **gilt torana** and gilt flame-shaped
    "wings", two **yellow lattice windows** `#E6C04A` either side, gilt pillars; a **long gilt pataka** (banner) hangs
    from the top roof down the front.
  * **Gajur:** an elaborate gilt finial (bell-kalasha + stacked umbrellas + spike) ≈ 1.5-2 m.
  * Plinth: low (≈ 0.3-0.5 m) with a rail of brass posts round the front where devotees kneel; no lions on a high stair.
* **Height:** ≈ 9.5-10 m to the gajur tip [O parts 8 m + finial].
* **G today:** generic red-brick 2-tier pagoda, brown tile roofs, carved brown struts, brass bells, a 1.5 m brick
  plinth and a stone stair with lions (`current_hariti.png`).
* **Must-have:** [ ] gilt roofs with curled corners and leaf fringe; [ ] red valance; [ ] gilt facade with yellow lattice
  windows and torana; [ ] gilt pataka; [ ] gilt gajur; [ ] low plinth, brass rail, pigeons; [ ] turned 12° with the grid.

### 3.6 Agam Chhen (Aagan Chhen, secret ritual house)

* (-8.3,-22.4), SSW, 8.6 × 5.4 m [O]. Brick `#D66749` walls (OSM building:colour) to 5 m, a **hipped roof** to 7 m,
  dark (OSM `#3E1E15`; the aerial shows a dark green-grey metal roof) [O][A]. A Newar ritual house: carved wooden
  windows, a small gilt torana over the door [E][V].
* **G today:** plain house: a 5 m flat-roof block plus a floating hipped part. **Must-have:** [ ] two storeys of brick
  with carved windows under one dark hipped roof; [ ] door facing the kora [V].

### 3.7 Vasupura (Basundhara, earth)

* (5.0,-25.3), S, 7.5 × 4.0 m [O]; a single-storey **maroon `#9C2A30`** shrine (3 m) under a **dark hipped roof** to
  4.1 m [O]; holds a Vasudhara image (75 cm) [S]. Next to Agam Chhen on the south edge, the view terrace in front.
* **G today:** plain building. **Must-have:** [ ] maroon shrine, dark hipped roof, door to the kora.

### 3.8-3.11 The other element shrines (five puras)

* **Vayupura (wind)** (-22.0,-19.7), SW: 4.1 × 3.1 m, 3.3 m, a small whitewashed shrine with a flat or low roof [O][A].
* **Agnipura (fire)** (-37.8,39.7) NW (OSM node "Agnipura" (-30,45)): 2.7 × 3.9 m small shrine [O]; in Newar practice a
  low stone shrine with a fire-pit niche [E][V].
* **Nagpura (water)** (map 19): a small **sunken tank/basin** with a stone surround just north of the kora, beside
  Amoghasiddhi [S map][V]; not in OSM; keep it a flat 2 × 2 m stone-rimmed pit with a grille.
* **Shantipur (sky)** (0.5,78.3), at the north end of the hilltop, 17.0 × 11.7 m, turned 13° [O]: a **white plaster and
  stucco** temple [S] of one storey (4 m) with a **gilt gabled roof** part (OSM `#DAA520`, 4-5 m) [O]; inside a
  Chakrasamvara shrine and the 170-180 m Swayambhu Purana mural [S]; door always locked [S]. Damaged 2015, conserved [S].
* **G today:** all plain buildings or missing (Nagpur). **Must-have:** [ ] each as a small shrine with its own form;
  [ ] Shantipur white with a gilt roof.

### 3.12 Pavan (Param) Bhairav and Yogamvar

* **Bhairav shrine** (map 16, OSM "Param Bhairav" w906619390) (21.7,12.7), NE between Pratappur and Karmaraja, 6.3 ×
  4.6 m [O]: a small Bhairav shrine, one storey under a dark hipped roof [A]; **not** a 4-storey house [G].
* **Yogamvar** (map 15, w904433284 monument) (-21.7,71.6), N: a protector shrine [S]; small, one storey [E][V].

### 3.13 Karmaraja Mahavihar (Karma Kagyu monastery) - missing

* w115376616, NE (25..44, 14..37), 16.6 × 18.6 m [O]; the big **white flat-roofed** block NE of the stupa in the aerial
  (roof parapet, small roof kiosk) [A]; collapsed in 2015 and rebuilt; houses a giant Buddha [S].
* Recommended look [E][V]: 3 storeys (≈ 10-12 m), whitewashed walls `#F2F0EA`, Tibetan **black trapezoid window
  frames** with red-yellow lintel bands, a **maroon parapet frieze** under the roof edge, a gilt dharma wheel with deer
  over the entrance; door facing the platform (SW).
* **G today:** missing (no building tag); a 15.3 × 2.0 m sliver "Shop and markets" (w1179993856) stands in it.

### 3.14 Deva Dharma Mahavihar

* w115376612, W-NW (-33..-25, 7..21), 12.7 × 6.0 m, L-shaped [O]; a monastery with a grey flat roof [A], ≈ 3 storeys
  [E]. **G today:** a 6-storey 18.5 m block. **Must-have:** [ ] 3 storeys, monastery facade, flat roof.

### 3.15 Drukpa Kagyu monastery - missing

* w1179996912 (historic=monument, no building tag), W (-37..-23, -3..+9), 13.1 × 14.7 m [O]; in the aerial a
  **salmon-pink `#D99A86` building round a court with a square gilt roof** (a gilt Chinese-style roof over the shrine
  hall) in the middle [A]. 2 storeys + the gilt roof [E][V].
* **G today:** missing. **Must-have:** [ ] pink walls, gilt central roof, faces the stupa.

### 3.16 Gyanmala Bhajan sattal (devotional singing pavilion)

* w257593927 (+parts), E (26..37, 1..9) at the NE corner of the stair head, 8.9 × 6.0 m [O]; in the aerial a **dark
  grey pyramidal (hipped) roof** about 10 m square with its eaves [A]. One storey (4 m) open pillared hall where bhajans
  are sung each morning [E][V], hipped roof to 5.25 m [O].
* **G today:** a flat-roof house. **Must-have:** [ ] open pillared pavilion with a dark pyramidal roof.

### 3.17 Museum, houses and shops round the platform

* **Swayambhu Buddhist Museum** w255881416 (-33,-13), 18.6 × 6.7 m, red, 3.5 m, flat roof [O]: a long one-storey hall on
  the west edge.
* **North and north-west rows:** shops (thangka, art), cafés (Ajima Restro) and houses, **2-3 storeys** (some 4), brick
  `#A35C40` or whitewashed, many with **red-brown pitched tile/metal roofs** and roof terraces [A][P: chaitya2 ref_06,
  shops ref_04]. In OSM they are `building=yes` without levels; the game infers 3-6 storeys (9.5-18.5 m) and paints them
  modern pastel. Use 2-3 storeys and the Newar/brick palette here.
* **Not there:** w904424689 and w904424690 (open view terrace S, no building in the 2024-25 aerial), w1179993484 (the
  vajra), probably w906619392 is not a house but the **white tiered niche chaitya** (§3.21) [A][P].

### 3.18 East stairway (365 steps)

* Built under **Pratap Malla** (17th c.) [S]; 365 steps by tradition [S]. OSM w24707651 (handrail, paving stones,
  width tag 2 - too narrow) from the foot (284.3,-51.0) at the Swayambhu Parikrama road (E) to the head (35.1,-6.9) +
  w925755319 to the kora [O]; 265 m long, **78 m** rise (curated drop) [S/G].
* **Profile** (distance from the head along the line, drop): 0 → 22.7 m: 9.6 m (**42 %**, the steep last flight between
  walls) → 64.5: 23.7 (34 %) → 76.9: 29.9 (50 %) → 87.6: 35.1 (47 %) → 96.4: 38.0 → 113: 40.4 (14 %, a landing) →
  144.7: 48.8 → 176.6: 57.9 → 209.9: 66.6 (26-29 %) → 236.1: 74.3 → 264.8: 78.1 (10-20 %, lowest flights). Average rise
  ≈ 0.21 m per step [S/G]. A branch w1437106374 (160 m, handrail) leaves at (119,-22) north round the hill [O].
* **Look** [P: steps ref_01/02, stairway ref_06, stairs ref_06/09, temples/swayambhu_stairs]:
  * Width **4-5 m** of grey stone steps `#8F8A80` (worn, darker nosings), divided by a **central steel handrail** on posts,
    with **side handrails**; flights of 10-30 steps between short landings.
  * Side walls: rough stone and **whitewashed walls with a sawtooth/merlon coping** `#E6E1D6`; in the lower forest
    sections open sides with roots and boulders; trees close overhead.
  * **Painted stone vahanas in pairs** on side plinths on the upper flights: elephants, horses, peacocks, garudas,
    lions (the Buddhas' mounts), painted blue-green, red, yellow [S][P]; a **painted naga** along a side wall [P].
  * **Last flight:** very steep, between a **brick building wall with windows** on the north side (right going up) and a
    **stone and brick wall with merlons** on the south side, prayer flags strung across overhead, the spire appearing
    above the top [P: steps ref_02, looking down the flight to the east][V].
  * Rest house (**tahara**, w1180013244, 9.8 × 2.5 m, (200,-44)) on the lower stair [O].
* **G today:** the hero stair follows the line with slab steps and low kerbs but is a dark ramp from afar; no rails,
  walls, vahanas, landings or flags (`cmp_stair_upper.png`).
* **Must-have:** [ ] 4-5 m wide steps with central + side rails; [ ] the steep walled last flight; [ ] landings; [ ] side
  walls with merlon coping; [ ] vahana pairs on the upper flights; [ ] flag strings over the top flight; [ ] trees close
  on both sides; [ ] monkeys on the rails.

### 3.19 Stair foot: painted Buddhas, mane wheels, chaityas

* **Three big painted seated Buddhas** near the foot (OSM node "Buddha Statues" n10959703544 (113,-24) on the stair and
  "Buddha murti" w1180013332 (276,-83), 3.7 × 3.5 m, by the road) [O][S]: each ≈ 3 m seated figure on a 1.2-1.5 m
  carved base, **gilt face and body `#D9A62E`, red-orange robe `#C4472B`**, set against a **blue nimbus `#2F6DB5`**
  framed by a **pink-red pointed arch `#D86B6B` with white corner pinnacles**, small standing attendant figures either
  side [P: steps ref_03, temples/swayambhu_flags ref_08]. Around them a stepped stone terrace with **white chaityas with
  gilt finials** and grey stone chaityas, a stone guardian figure.
* **Mane (big prayer wheels):** two houses w908430314 / w1180013656 (8.6 × 5.2 m, "Mane wheel") at (265..268,-89..-99)
  by the road [O]; ≈ 3.5 m red-gold drums in open pavilions [S "12-foot prayer wheel"][E].
* **Small white chaitya pavilions** (≈ 4.7 m squares, OSM amenity=monastery) line the south parikrama road
  ((93,-155), (173,-136), (196,-130), (210,-126), (237,-111)) [O][A]; the parikrama's south side carries a long
  **prayer-wheel wall** (w443785032, 346 m) [O].
* **G today:** missing. **Must-have:** [ ] Buddha group at the foot with the arch frames; [ ] mane pavilions; [ ] white
  chaityas; [ ] the entrance node n10799787706 (256,-54) as the start of the climb.

### 3.20 Prayer flags

* 16-30 lines from just under the crown (≈ 26 m) down to the parapets, the shikharas and the roofs of the ring
  buildings, in every direction; flags ≈ 25 × 30 cm in the order **blue `#2E6FD8`, white, red `#D93A2B`, green `#2E9E4F`,
  yellow `#F2C230`**, 20-30 per 10 m, sag 8-12 % [P][refs/temples]; more strings across the east stair top and between
  trees. **G today:** 20 cords with flag rows - keep, thicken the flag density, tie them under the crown.

### 3.21 Chaityas, lamp columns, bells, lamp racks (platform dressing)

* **Chaitya garden (NW, map 26):** 30-40 dark grey stone **votive chaityas** (1-3 m) in rows on stone curbs between the
  stupa and the north buildings, (-25..-10, 18..35) [A][P: chaitya2 ref_06]: square stepped bases, drums with four
  niches (Buddha reliefs), small domes, 13-ring cones, some shikhara-type with a niche Buddha; `#5F5B55`.
* **Named:** Aananta Pur stupa w1179992315 (-12.6,20.4) a **white Tibetan-style chorten** Ø 3 m [O][A]; "Small chaitya"
  w1179997380 (-21.8,33.8) Ø 4 m [O]; **Red stupa with relics of the 10th Shamarpa** (map 27) a red-painted chorten near
  Hariti (≈ (-20,19)) [S][A]; Bahu Mangal Dwar chaitya (map 24) SW near Vayupura [S]; **white tiered niche chaitya**
  (≈ 8 m, stacked tiers of small arched niches, whitewashed) at w906619392 (-15.4,-18.2) [A][P: harmika ref_06].
* **Stone lamp columns:** dark stone pillars 3-5 m with stacked lotus capitals and gilt or bronze finials (a garuda, a
  vajra, a bird) round the E and NE of the kora [P: harmika ref_09, general ref_01].
* **Bells:** big bronze bells under stone lintels on two posts (§3.2) [P].
* **Butter-lamp racks:** soot-black stone blocks and black iron racks (1 m high) with rows of small brass lamps, near
  the E and NE shrines [P: lamps].
* **Vahana pairs and banner poles** in front of each Buddha shrine (§3.1.4).
* **G today:** two generic chaityas (Aananta Pur as a small grey chaitya, Small chaitya as a stupa) and the POI shrines
  never drawn (`SacredSelector.TrySelectPoi` has no caller). **Must-have:** [ ] chaitya garden; [ ] white chorten;
  [ ] red stupa; [ ] white niche chaitya; [ ] lamp columns; [ ] bells; [ ] lamp racks.

### 3.22 Amideva Buddha Park (west foot, on the Ring Road)

* Park w1158356596 (-606,133), 90 × 78 m [O]; built 2002-2003 by a Manang committee [S]. Behind a white wall with
  **prayer wheels** (w1336880003 "moulins à prières", 148 m) along the Ring Road (W) and the parikrama road (S) [O][A].
* **Three gilt statues** on one N-S line, facing **west** to the courtyard and the Ring Road [A][V] (statue outlines
  w619948098 (-613,116.5) S, w619948099 (-611,126.4) centre, 8.2 × 6.4, w619948100 (-611,135.2) N) [O]:
  * **Centre: Shakyamuni** ≈ 67 ft (20 m), the largest, gilt body, **blue hair**, bowl in the lap [S][P].
  * **North (the viewer's left when facing the statues; Atlas Obscura: "to its left"): four-armed Chenrezig
    (Avalokiteshvara)** ≈ 64 ft, crowned, outer arms raised holding a lotus and a mala [S][P].
  * **South (viewer's right): Guru Rinpoche (Padmasambhava)** ≈ 64 ft, lotus hat, khatvanga staff in the crook of the
    left arm [S][P].
  * In the game these three outlines are archetype Gompa, 6.5-7.5 m, drawn as plain boxes [G].
  * Each sits on a **huge gilt lotus throne** (flattened bowl of petals) over a **round/polygonal plinth painted with
    Tibetan motifs** (red `#B22222`, blue `#1F4E9A`, green `#2E7D32` panels), on a **raised terrace** (≈ 3-4 m) with a
    steel railing and a central stair; two small gilt pavilions flank the stair [P: buddha_park2 ref_04].
* Paved cross-shaped terrace, square lawns, a white Tibetan chorten in the NW corner and a white stupa with a gilt
  shrine in the SE (w619948085 (-590,103), w244356909 (-640,145)) [A][O]; a giant red-gold prayer wheel in a pavilion
  [P: buddha_park2 ref_02].
* **G today:** three plain boxes (6.5-7.5 m) beside the tall beige block w341677213 on the park's east edge (`cmp_amideva.png`); ASSET_MANIFEST once set the
  park out of scope (P2) - the owner's request now includes it. **Must-have:** [ ] three gilt figures (≈ 12 m figure on
  ≈ 8 m throne + plinth); [ ] the terrace, rails, stair, pavilions; [ ] wheel wall on the roads; [ ] no tall blocks in
  or over the park: the game gives w341677212 / w341677213 (park edge, NE) 12.5 m and w208811579 / w342369320 (south of
  the park road) 19 / 18.5 m from inferred levels [G][V].

### 3.23 West approach: car park, World Peace Pond, Paschimi chaitya

* **Car park** w1180009693 (-123,74), 79 × 41 m, paid; the car road (Swayambhu Parikrama → Manjushri Marg) climbs to it
  from the south-west [O][S].
* **World Peace Pond** w201223708 (-92,104), round Ø 9.9 m, green water, a statue on a pedestal in the middle, a paved
  plaza round it [O][A].
* **Paschimi (west) chaitya / Vasubandhu chaitya:** a white **Vajradhatu chaitya** on a stepped square platform
  (≈ 14.5 m) above the car-park path, a **stone vajra** on its east side, four stone Tathagata images in niches, a
  13-step finial [S][A]; OSM stupa w619948088 (-77.3,76.8) is its dome; w619948053 (-77.2,95.6) and w619948087
  (-85.9,88.3) are smaller white chortens by the plaza [O][A].
* From the car park the visitor climbs the W steps w24684297 / w135461252 past souvenir stalls to the platform's west
  side (museum, Drukpa monastery) [O].

### 3.24 Manjushri (Saraswati) knoll

* w256276407 "मंजुश्री" (-265,113) on the western knoll: a paved terrace ≈ 45 × 25 m with a big tree, a **red-roofed
  shrine building** (≈ 8 × 14 m) at its west end, a row of small white chaityas and flag lines [O][A]. Low priority;
  keep it a small shrine, not a 3-storey box [G].

---

## 4. Small site elements (life and props)

* Pilgrims walking the kora clockwise spinning wheels; monks (maroon), Newar women in red saris on Saturdays at Hariti;
  tourists; souvenir and thangka stalls on the N rows and the W steps; prayer-flag sellers.
* Monkeys (many), stray dogs (few), pigeons (Hariti roof, kora).
* Black lamp racks with lit butter lamps (evening glow), incense, marigold garlands on the shrines.
* Iron railings with flags at the parapet; coin-throwing at the World Peace Pond.

## 5. Recommended build split (for the modeller)

| Hero / part | Contents | LOD0 budget guide |
|---|---|---|
| Stupa core | dome, base, wheel gallery, harmika, cornice, toranas, rings, crown, gajur | ≈ 20-24 k |
| Shrine ring | 5 Buddha + 5 Tara shrines with vahanas and banner poles (instance one master each) | ≈ 8-10 k |
| East front | vajra group (vajra, drum, lions), Pratappur, Anantapur, Shamarpa bell | 2 × 4-5 k + 2 k |
| Hariti | gilt 2-tier pagoda | ≈ 5 k |
| Element shrines and small temples | Agam Chhen, Vasupura, Vayupura, Agnipura, Nagpura, Shantipur, Bhairav, Yogamvar, Gyanmala sattal | 0.6-2 k each |
| Monasteries | Karmaraja, Deva Dharma, Drukpa Kagyu | 2-4 k each |
| Platform | paving, parapet with merlons, chaitya garden (instanced), lamp columns, lamp racks, flags | instanced |
| East stair | steps, rails, walls, vahanas, last-flight walls, foot terrace, Buddha group, mane pavilions | ≈ 8-10 k |
| Amideva park | 3 statues, thrones, plinths, terrace, wall | ≈ 15 k (separate hero, far LOD a silhouette) |

Hide every OSM building inside the platform that a hero replaces (stupa parts, shikharas, Hariti, the element shrines,
Dorje, Param Bhairav, Gyanmala, w904424689/690, w906619392, Shop-and-markets w1179993856) and give the ring buildings
2-3 storeys (open issue O-2).

## 6. Must-have checklist (site)

- [ ] Stupa per §3.1.9, site axes turned 10°.
- [ ] Vajra group, two shikharas, Hariti, the ten shrines in their real places and forms.
- [ ] No modern block on the platform; ring buildings 2-3 storeys, brick/whitewash; the two big monasteries present.
- [ ] Parapet with merlons and view terrace on the S/E edge; stone paving; chaitya garden NW.
- [ ] East stair as §3.18 with the Buddha group at the foot; forest closing over it.
- [ ] Flags from the crown in all directions; monkeys.
- [ ] From the city: gilt spire well above two white shikharas on a forested hill (§2.6).
- [ ] Amideva park statues if the tier budget allows (in the sample bbox).

## 7. Open issues for other packages (not this package's files)

* **O-1 (data/pipeline):** w1179993484 "Dorje" (historic=pillory, building=yes) becomes a building - drop it or hide it
  under the Swayambhu hero; same for w904424689/690 (no building in the aerial).
* **O-2 (data/buildings):** `building=yes` without levels on the hilltop and in Amideva park get 3-6 inferred storeys;
  cap inferred levels to 2-3 inside `landuse=religious` w115379177 and the park w1158356596.
* **O-3 (data):** monasteries mapped without `building` (Karmaraja w115376616 amenity=place_of_worship, Drukpa Kagyu
  w1179996912 historic=monument, Shree Karma Raja w1179996046) are dropped - carry them as heroes or buildings.
* **O-4 (temples):** POI shrines are never drawn (`SacredSelector.TrySelectPoi` has no caller); the ten shrine nodes
  arrive as Gompa POIs with `HasFootprint` and nothing renders them except the stupa hero.
* **O-5 (temples):** the "Gompa" archetype outlines (Vasupura, Vayupur, Shantipur, Manjushri) go through the house
  grammar, not a shrine generator.
* **O-6 (nature):** the forest must cover the whole hill to the parapet and both sides of the stair (the sample renders
  have no trees; check the nature placement in landuse religious/protected area w256548640).
* **O-7 (data):** OSM node "4. Virochana Buddha" n10959618751 is north of Akshobhya; sources put it just south.
* **O-8 (data):** east stair `width=2` is too narrow (photos 4-5 m).

## 8. Photo-matched viewpoints (for render-vs-photo checks)

All in OBJ coordinates of the dump (tile 10/514/162 frame, OBJ z = -Unity z), stupa at (876.98, 1402, -403.64).

| View (camera at, looking) | Eye | Look at | Photo |
|---|---|---|---|
| Stair head, W | (912, 1404.5, -396.6) | (877, 1416, -403.6) | shots/swayambhu_top ref_06 (Jorge Lascar) |
| Upper east stair, W | (988, 1372, -385.6) | (900, 1400, -397) | mon-swayambhu/steps ref_01 (MattW) |
| South kora, N | (877, 1400.2, -375.6) | (877, 1410, -403.6) | mon-swayambhu/stupa ref_07 (Frank Kehren) |
| Chaitya garden NW, SE | (854, 1407.7, -430.6) | (885, 1412, -397) | mon-swayambhu/chaitya2 ref_06 (scottgunn) |
| West kora, E | (847, 1404.6, -400.6) | (877, 1412, -403.6) | mon-swayambhu/harmika ref_08 (Jorge Lascar) |
| From the south slope | (900, 1352, -195) | (877, 1405, -403) | mon-swayambhu/monastery ref_02 (eriktorner) |
| Amideva park, E | (200, 1430, -560) | (272, 1372, -532) | mon-swayambhu/buddha_park2 ref_04 (markhorrell) |
| Bird's-eye SE | (1130, 1600, -150) | (877, 1395, -403) | aerial `site_plan_platform_osm.jpg` |

## 9. Numbers at a glance

| | |
|---|---|
| Stupa | outline Ø 26.8, dome Ø 24.5 rise 10.6, harmika 6.2 (11.8-15.1), rings 18-26 (Ø 6.5→3.0), crown 26.2-28 (Ø 4.0), gajur to 33 |
| Site axes | east = bearing 100° |
| Shrines | r 12.5; Buddha 2.5 w × 5 h; Tara 1.8 w × 3.5 h |
| Vajra | 2.18 m on a Ø 2.4 × 1.6 m drum, 26 m E |
| Shikharas | Pratappur 7.9 base (18.5,5.8); Anantapur 8.8 base (9.2,-19.4); ≈ 20 m |
| Hariti | 6.5 square, ≈ 10 m, (-18.4,13.5) |
| East stair | 265 m, 78 m drop, 365 steps, 4-5 m wide, steepest 42-50 % |
| Amideva | statues ≈ 20 m, (-611,116..135), facing W |

## 10. Photo and source credits (reference only, not in the repo)

Photos (Flickr via Openverse; paths under `/home/user/wt/refs/`):

| Use | File | Author | Licence | Link |
|---|---|---|---|---|
| Vajra from the stair head | shots/swayambhu_top/ref_06 | Jorge Lascar | CC BY 2.0 | https://www.flickr.com/photos/8721758@N06/17209789814 |
| Vajra broadside | shots/swayambhu_top/ref_01 | dalbera | CC BY 2.0 | https://www.flickr.com/photos/72746018@N00/8435899936 |
| Vajra with city | mon-swayambhu/vajra2/ref_07 | Inda Agudo | CC BY-SA 2.0 | https://www.flickr.com/photos/71174491@N07/7007488270 |
| Harmika close-up, toranas | mon-swayambhu/general/ref_06 | dalbera | CC BY 2.0 | https://www.flickr.com/photos/72746018@N00/8435684322 |
| Eyes, nose, urna | mon-swayambhu/harmika/ref_00 | avialpert | CC BY-SA 2.0 | https://www.flickr.com/photos/129651693@N07/16664031036 |
| Kora, shrines, shikhara | mon-swayambhu/harmika/ref_06, ref_08 | Jorge Lascar | CC BY 2.0 | https://www.flickr.com/photos/8721758@N06/17641135070 ; .../17228237094 |
| Lamp columns | mon-swayambhu/harmika/ref_09 | eriktorner | CC BY-NC-SA 2.0 | https://www.flickr.com/photos/39267804@N05/13255089465 |
| Buddha shrine, spire, crown | mon-swayambhu/stupa/ref_07 | Frank Kehren | CC BY-NC-ND 2.0 | https://www.flickr.com/photos/77759596@N00/8277435491 |
| Double shrine, Tara, rings | mon-swayambhu/stupa/ref_08 | Prof. Mortel | CC BY-NC-SA 2.0 | https://www.flickr.com/photos/43714545@N06/6862433319 |
| Platform NE, chortens | mon-swayambhu/general/ref_01 | dhilung | CC BY-NC 2.0 | https://www.flickr.com/photos/7236858@N07/3748643831 |
| Hill from the south | mon-swayambhu/monastery/ref_02 | eriktorner | CC BY-NC-SA 2.0 | https://www.flickr.com/photos/39267804@N05/13291107743 |
| Hill from the east (distance) | mon-swayambhu/y2015/ref_05 | Carsten ten Brink | CC BY-NC-ND 2.0 | https://www.flickr.com/photos/77334245@N00/8237802939 |
| Anantapur shikhara | mon-swayambhu/shikhara/ref_07 | markhorrell | CC BY-NC-SA 2.0 | https://www.flickr.com/photos/41849531@N04/17423658560 |
| Hariti temple | mon-swayambhu/hariti2/ref_04, ref_05 | Prof. Mortel | CC BY-NC-SA 2.0 | https://www.flickr.com/photos/43714545@N06/6862303087 ; .../6862863371 |
| Hariti front | mon-swayambhu/hariti/ref_02 | dalbera | CC BY 2.0 | https://www.flickr.com/photos/72746018@N00/8435316715 |
| Chaitya garden | mon-swayambhu/chaitya2/ref_06 | scottgunn | CC BY-NC 2.0 | https://www.flickr.com/photos/24172274@N00/43311888345 |
| Prayer wheels and grilles | mon-swayambhu/wheel2/ref_08 | Wonderlane | CC0 1.0 | https://www.flickr.com/photos/71401718@N00/3093537637 |
| Upper stair | mon-swayambhu/steps/ref_01 | MattW | CC BY-NC-ND 2.0 | https://www.flickr.com/photos/7757034@N07/5226326016 |
| Top flight | mon-swayambhu/steps/ref_02 | ironybored | CC BY 2.0 | https://www.flickr.com/photos/128785591@N08/15632760399 |
| Lower stair | mon-swayambhu/stairway/ref_06 | Wonderlane | CC BY 2.0 | https://www.flickr.com/photos/71401718@N00/4389505938 |
| Naga wall | mon-swayambhu/stairs/ref_06 | Wonderlane | CC BY 2.0 | https://www.flickr.com/photos/71401718@N00/4389600376 |
| Vahana on the stair | mon-swayambhu/stairs/ref_09 | hiphopmilk | CC BY-NC-ND 2.0 | https://www.flickr.com/photos/32070993@N02/28568374092 |
| Painted Buddha at the foot | mon-swayambhu/steps/ref_03 | Wonderlane | CC0 1.0 | https://www.flickr.com/photos/71401718@N00/2359292471 |
| Foot terrace, Buddhas, chaityas | temples/swayambhu_flags/ref_08 | Wonderlane (Steve D.) | CC0 1.0 | https://www.flickr.com/photos/71401718@N00/2359302513 |
| Amideva park | mon-swayambhu/buddha_park2/ref_04 | markhorrell | CC BY-NC-SA 2.0 | https://www.flickr.com/photos/41849531@N04/17740322076 |

Sources: Rubin Museum, *The Svayambhu Chaitya of Kathmandu*
(https://rubinmuseum.org/projecthimalayanart/essays/the-svayambhu-chaitya-of-kathmandu/); swoyambhustupa.com (map
https://swoyambhustupa.com/map-of-swoyambhu-stupa , two shikharas https://swoyambhustupa.com/two-shikharas/ , five Buddhas
https://swoyambhustupa.com/pancha-thathagat-swoyambhu/ , Hariti https://swoyambhustupa.com/harati-ajimaa-temple/ ,
Paschimi chaitya https://swoyambhustupa.com/vasubandhu-memorial-chaitya-paschimi-chaitya-2 , vajra
https://swoyambhustupa.com/?p=2238); Wikipedia https://en.wikipedia.org/wiki/Swayambhunath ; Mona museum (vajra size)
https://www.mona.com.np/art/v/vajra-1 ; Himalayan Times (Pratappur demolished)
https://thehimalayantimes.com/kathmandu/doa-demolishes-pratappur-temple-in-swayambhunath-stupa-citing-possible-risk-to-visitors/ ;
Xinhua 2018 (shikharas final stage) http://www.xinhuanet.com/english/2018-07/08/c_137310431.htm ; Khabarhub 2025
(renovation completed) https://english.khabarhub.com/2025/03/440904/ ; ECS Nepal *Restoring the Self-Created*
http://ecs.com.np/features/restoring-the-self-created ; Kathmandu Post (Shantipur murals)
https://kathmandupost.com/art-entertainment/2017/08/06/the-swayambhupurana-restoring-the-shantipur-murals ; Atlas Obscura
(Amideva park) https://assets.atlasobscura.com/places/amideva-buddha-park ; UNESCO WHC 121 (Kathmandu Valley,
Swayambhu monument zone). Map data © OpenStreetMap contributors (ODbL). Aerial imagery © Google / Esri, used only for
measurement here.
