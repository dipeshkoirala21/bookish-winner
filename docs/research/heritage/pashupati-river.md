# Pashupatinath river front: Bagmati ghats, east bank, Guhyeshwari, Gaurighat (structure-by-structure brief)

Package `mon-pashupati-river` (heritage research, W2 detail pass, monuments round; key `pashupati-river`). Owner:
"National Heritage and Monuments sites should look exactly as in real life. Pashupatinath had no detail ... It did not
look Pashupatinath at all ... Monuments are one thing that we cannot absolutely slack off on." This brief is what the
modeller builds the **river front of Pashupatinath** from: the site plan, every structure with its numbers, colours and
details, what the game draws there today, and a must-have checklist per structure. Cartoon style may simplify surfaces
only; silhouettes, proportions, arrangement and colours must match the place.

**Scope.** The Bagmati from Guhyeshwari (upstream, NE) past Gaurighat and Kirateshwar, the ghats of Pashupatinath
(Arya Ghat, Bhasmeshvar Ghat and their stone platforms and steps), the two footbridges, the riverside pavilions and
dharmashala buildings, the Pandra Shivalaya row and the terraces and stairs climbing the east hill (Mrigasthali) with
its shrines, the Ram Mandir, Gorakhnath, Vishwarupa, the Shleshmantak forest with its monkeys and deer, sadhu spots.
The main temple and its walled compound (west bank) belong to the sibling package `mon-pashupati-temple`
(`docs/research/heritage/pashupati-temple.md`); where the two meet (the east-gate stairs, the Arya Ghat landing) this
brief describes the river side and flags the seam (§9).

**Tags:** **[O]** OSM (valley extract of the Nepal PBF used by the pipeline, pyosmium), **[A]** measured on Esri World
Imagery z19 (0.264 m/px, crops in `/home/user/wt/previews/mon-pashupati-river/aerial/`), **[P]** read from photos
(scaled by people ≈ 1.65 m, steps ≈ 0.2 m, shrine cellas ≈ 4.6 m), **[S]** published source (§11), **[E]** estimate,
**[V]** to verify on a current photo or on site.

**Frame.** Positions are **local metres, x east / y north, from the Pashupatinath temple centroid** (OSM w913170315,
27.710465 N 85.348665 E = game (532963.9, 165857.5)); the site lies in level-10 tiles 520/161, 521/161, 520/162,
521/162 of `kathmandu_core`. Bearings are compass degrees. Heights are above the local ground unless "a.s.l.".

**Reference material** (outside the repo, never copied in): photos `/home/user/wt/refs/mon-pashupati-river/<topic>/`
(≈ 800 open-licensed photos from Openverse / Wikimedia Commons / Flickr, credits in each `credits.txt`, cited ones in
§12); aerial crops with the OSM overlay `/home/user/wt/previews/mon-pashupati-river/aerial/{core,guhye,gorakh,gauri}{,_osm}.jpg`;
current-game renders `/home/user/wt/previews/mon-pashupati-river/current/` (per-structure `s_*.png`, views `v1..v6`,
sheets `sheet_current_0/1.png`); game-vs-photo pairs `/home/user/wt/previews/mon-pashupati-river/compare/*.png`; the
OSM inventory `/home/user/wt/previews/mon-pashupati-river/osm_inventory.txt` (534 lines) and the pack inventory
`.../pack_inventory.txt` (every BLDG/POI/AREA/ROAD record the game holds in the box, with the generator it gets).

---

## 0. Verdict: what is wrong today (in order of how much it hurts recognition)

| # | In the game now (`current/`, `compare/`) | Reality |
|---|---|---|
| 1 | The river is a **flat blue sheet** in a sandy trench with grass banks (`v2_aryaghat_to_eastbank.png`, `compare/bridges_ghats.png`). No ghat, no step, no wall. | The Bagmati runs in a **stone-walled channel 12-20 m wide** lined on both banks by **stepped stone ghats** (flights of 8-15 steps), landings, arcaded retaining walls and **three terrace levels on the east bank, ≈ 7 m from water to the shrine terrace** (§3.1). The water is olive-brown, shallow in winter with exposed rocks, fast and full in monsoon. |
| 2 | **Pandra Shivalaya** = 11 random-coloured boxes (green, yellow, pink, cyan plinths) under grey bullet caps (`s_shivalaya_row.png`). | **Eleven identical grey-stone cubes (4.6 m) with arched doors on both row ends, carved toranas and corner figures, under white lime-plastered bell roofs with a ribbed band and a pointed finial**, ≈ 7.5-8 m tall, 7 m apart, on the top terrace facing the temple, with a tall red flagpole in front of each (§3.6). The single most-photographed view of Pashupati is the temple **seen across this row**. |
| 3 | The west bank south of the bridges is a **plain grey flat-roofed slab** (the Briddhashram) and a sandy strip; Pancha Devalaya is a **60 m two-tier pagoda** covering a whole courtyard (`s_aryaghat_bridha.png`, `s_pancha_deval.png`). | A **row of 6-8 square stone cremation platforms** at the water's edge, each under an open steel canopy with a two-tier pyramidal metal roof, in front of the long **ochre-arcaded Pashupati Briddhashram** (old people's home); behind it **Pancha Deval: five white Rana-era shikharas on a stepped brick platform inside a brick sattal courtyard** (§3.4, §3.5, §3.18). |
| 4 | Everything near the water is filled with **4-6-storey pastel apartment blocks** (OSM `building=yes` drawn as ModernUrban): Mukti Mandal, the riverside dharmashalas, the Gorakhnath and Guhyeshwari sattals, Vishwarupa's courtyard ring (a **pink 4-storey ring**). | The river front is **low**: 1-3-storey brick or lime-washed Newar and Rana buildings with tin or tile roofs, carved windows, arcades and balconies; sattals around courtyards are 2 storeys of red brick with red-painted woodwork (§3.4, §3.10-3.15). |
| 5 | Guhyeshwari = a small **one-tier tile pagoda** inside a ring of colourful flat-roofed blocks (`compare/guhyeshwari.png`). | A low, broad **gilt-copper shrine with two shallow gilt roof stages and red frills, crowned by four gilt serpents arching up to a central finial**, gilt repoussé walls, in a stone-paved courtyard with tall stone pillars carrying gilt figures, enclosed by **red-painted two-storey sattals**; a white gate with painted eyes in a long red brick wall (§3.13). |
| 6 | Gorakhnath, the hill "Shiva Temple", Kirateshwar and Gaurighat Hanuman Mandir are **generic tile pagodas** (1-2 tiers). Vishwarupa is a generic 2-tier pagoda. | Gorakhnath is a **red-and-white Nagara shikhara** (white ribbed spire on a red cella); Kirateshwar is an **open, roofless red-walled courtyard** with a big stone face-linga; Vishwarupa is a **large square temple with a white dome** in a 63 × 58 m brick sattal square with domed corner shrines; Gaurighat has **red brick pati and sattals with red tin roofs** by a rocky river bend (§3.10-3.16). |
| 7 | The two footbridges are thin grey slabs with pipe rails. | Two short **stone-clad footbridges (≈ 20 and 24 m)** with **solid grey stone parapets ≈ 0.85 m** and flat copings, nearly level with the ghat landings (§3.2). |
| 8 | The east hill is a smooth grass slope with a forest of identical round trees, a few boxes and a road ribbon. | The hill is **terraced and stepped**: a broad flight of steps with **white lime-washed stepped side walls**, landings lined with **dozens of grey stone votive shikharas** and red-brick white-domed shrines, stone-paved lanes, bells on posts, a deer enclosure, macaques on every wall (§3.7, §3.10, §3.17). |
| 9 | The Ram Mandir, the caves of Naropa/Tilopa, Surya Ghat, the Birupaksha shrine, Bhasmeshvar Mahadev, the octagonal pavilions at Guhyeshwari, the Gaurighat bridge and the riverside flagpoles are **missing**. | All present and all part of the recognisable river front (§3.3-3.5, §3.8, §3.9, §3.13-3.16). |

The main temple itself (gilt two-tier pagoda, hero `her.ktm.pashupatinath`, 21,655 triangles at LOD0) is the only hero
in this area (`pack_inventory.txt`: `HERO her.ktm.pashupatinath ... HEROBUILT tris=21655`). Its brief is the temple
package's; from the river it must read as **two broad gilt roofs and a gilt gajur rising above the east-gate pavilion
and the riverside buildings** (ghats/ref_01, complex/ref_06).

---

## 1. Sources and method

* **OSM [O]:** every way, node and relation in 85.342-85.360 E / 27.704-27.717 N from the valley extract
  (`dp/valley.osm.pbf`, cut from `pipeline/data/raw/osm/nepal.osm.pbf`), 6,285 ways, 523 tagged nodes, 53 relations;
  river widths sampled every 20 m along the centreline `w751466758` against the river area `w751466756`. Overpass and
  the Commons API were rate-limited from this machine (HTTP 429); OSM data used as extracted.
* **Pack [O]:** the committed sample `shared/sample-regions/kathmandu_core` read with the real core readers in a
  temporary dump test (not committed): every BLDG, POI, AREA, ROAD, LINE record in the box with its archetype, flags,
  `SacredSelector.TrySelect` kind, hidden-by-hero flag; meshes from `TerrainMesher`, `RoadMesher`, `AreaMesher`,
  `BridgeMesher`, `BuildingMesher` (B0 kit-lite near, B1 styled far, curated hero hide set applied), `HeroBuilder`,
  `TreePlacement` + `FloraMesher` (LOD1), rendered with `tools/mesh-preview/render.py`.
* **Photos [P]:** 120+ Openverse queries (Wikimedia Commons series by Rajesh Dhungana, Prof. Mortel, Nabin K. Sapkota,
  Goutam1962, gje; Flickr by dalbera, girolame, Jorge Lascar, Nick Kenrick and others), every contact sheet looked at,
  key frames at full size; colours sampled from sunlit patches with outlined regions (sheets in the scratch folder,
  values below marked [P]).
* **Aerial [A]:** Esri World Imagery z19 crops of the ghats, the east hill, Guhyeshwari and Gaurighat with the OSM
  outlines drawn on top (the imagery and OSM agree within 1-3 m along the river; the temple outline is ≈ 3 m off).
* **Texts [S]:** Pashupati Area Development Trust (PADT) monument pages (Pandhra Shivalaya, Gorakhnath, Visworupa, Ram
  Temple, Kirateshwor, Vatsaleshwori, Rajarajeshwari, Panchadewal, Guheswori, ghats), Wikipedia (Pashupatinath,
  Guhyeshwari), Lonely Planet POI notes, J.-C. Latombe's 2023 photo log (Stanford), travel guides (§11).

---

## 2. Site plan

### 2.1 The river and the banks

```
            N                                         (local metres from the temple centroid; not to scale)
            ^
   Gaurighat (85,287) .--------- river comes from Guhyeshwari (500,150) flowing W ----------.
   Hanuman Mandir      \   Gaurighat bridge (115,277)                                       |
                        \__  river loops S round the Kirateshwar hillock (112,232)          |  Guhyeshwari
                           \                                                                 |  (457,83)
   Kailash hill (-21,176)   \_ bend (150,90) _                                              |  courtyard +
   (west bank, forest,          \            caves of Naropa/Tilopa (76,66)                 |  bridge (494,148)
    shrines, Surya Ghat)         \                                                          |
                                  \   Pandra Shivalaya row (45,-51)..(75,13), bearing 25°   |
      MAIN TEMPLE (0,0)   east     \  ==== upper terrace, battered wall, path (68,-52)-(97,14)
      compound (west bank) gate ->  ||  Mrigasthali hill ............. Gorakhnath (276,7)
      Arya Ghat (15..30, -10..-40)  ||   east stairs (30,-76)->(75,-91)->(221,-79) -> Shiva temple (215,-49)
      north bridge (12,-42)-(29,-54)==  stair shrines x6 (46,-60)..(77,-75)          -> Vishwarupa (395,-150)
      south bridge (-2,-63)-(20,-73)==
      Vatsala (-15,-66)            ||  small white shrines (8..12, -78..-92)
      Bhasmeshvar Mahadev (-28,-79) ||  Ram Mandir complex (17..88, -105..-183)
      cremation platforms (-17..-35,-93..-178)
      Briddhashram (-46,-126)       ||   forest (Shleshmantak) ......................
      Pancha Deval (-93,-96)        ||
      Rajrajeshwari pond (-58,-264) \\  river continues S towards Tilganga / Gaushala bridge
```

| Reach | River centre (local) | Flow | Water width [O] | Banks | Notes |
|---|---|---|---|---|---|
| Guhyeshwari bend | (520,100) → (494,149) | N, then W | 13-16 m | south bank: Guhyeshwari compound and its ghat; north bank: road, cafés | Guheswori footbridge w196281728 (26 m) at (494,148) [O] |
| Mrigasthali north edge | (456,162) → (266,228) | W (290°) | 18-28 m | south: Mrigasthali forest edge, wall w1373373385 (267 m) along the riverside path w1368994718; north: Gaurighat houses | widest reach, sand bars (area w1376666629 sand/shingle) |
| Gaurighat loop | (202,273) → (78,231) → (90,155) → (150,104) | W, S, SE | 8-14 m | outer (west/north) bank: Gaurighat with its pati and sattals (85,287); inner bank: the Kirateshwar hillock | rocky bed with red-smeared boulders; Gaurighat bridge w112664385 (18 m, `layer=1`, paving) at (115,277) |
| Surya Ghat / caves | (138,75) → (83,47) | SW (216°-250°) | 11-17 m | west: Surya Ghat steps under Kailash hill; east: caves of Naropa (76,66) and Tilopa (81,70) | |
| **Temple ghats** | (59,15) → (16,-52) | SSW (209-219°) | **10-18 m** | west: Arya Ghat below the temple's east gate; east: the three-level terraces and the Pandra Shivalaya | the two footbridges at y = -48 and -68 |
| **Cremation ghats** | (7,-70) → (-19,-145) | S (195-207°) | 17-21 m | west: Bhasmeshvar Ghat platforms; east: walkway, small white shrines, Ram Mandir wall | |
| South | (-22,-185) → (-11,-304) | S (171-178°) | 18-26 m | west: Rajrajeshwari ghat and pond; east: forest | |

Water surface ≈ **1302-1304 m a.s.l.** at the temple ghats; the temple plinth ≈ 1310 m; the east terrace with the
shrines ≈ 1309 m; the Mrigasthali hilltop (Gorakhnath, Vishwarupa) ≈ 1343-1345 m [O: Copernicus GLO-30 in the pack].
The channel walls are vertical-to-battered grey stone; the river level swings ≈ 1.5 m between winter (a 5-8 m stream
between gravel and rubbish, the lowest steps dry; photos y2019/04, ghats/ref_09) and the monsoon (water over the lowest
landings).

### 2.2 Paths, steps, bridges, walls (all [O], lengths along the way)

| Id | What | Geometry (local) | Surface | Notes |
|---|---|---|---|---|
| w112664331 | **north footbridge** | (12,-42) → (29,-54), 20 m, bearing 125° | paving stones, `bridge:structure=arch`, layer 1 | §3.2 |
| w37707651 | **south footbridge** | (-2,-63) → (20,-73), 24 m, bearing 114° | paving stones, arch, layer 1 | §3.2 |
| w44105888 | east-bank landing linking the bridge heads | (20,-73) → (29,-54), 21 m | paving | the lower east ghat landing |
| w112664313, w686351440 | stubs to the east stairs | (29,-54)→(38,-61); (20,-73)→(30,-76) | paving | |
| w686351441 | **east stairs, first flight** | (30,-76) → (66,-87) → (75,-91), 48 m, `incline=up` | paving stones | the six stair shrines stand on its north side |
| w32911668 | **east stairs to the hilltop** | (38,-61) (59,-72) (69,-82) (75,-91) (91,-99) (118,-109) (126,-104) (164,-95) (189,-87) (204,-77) (221,-79), 205 m | paving stones, `handrail=no` | rises ≈ 35 m; white lime-washed stepped side walls (eastbank_a/11) |
| w1259731662, w44105884 | steps down to the shrine terrace | (102,-69)→(68,-52); (91,-99)→(102,-69) | paving | |
| w44105885 | **upper terrace walk** behind the Shivalaya row | (68,-52) → (97,14), 72 m | paving stones | parallel to the row, ≈ 22 m from the water |
| w80913703 | **west-bank riverside walk** | (-2,-63) (12,-42) (34,-15) (41,-5) (48,10) (52,19) (55,24) (69,50), 134 m | paving stones | from the south bridge head north past Arya Ghat to Surya Ghat |
| w112776516 | steps from the walk up to the temple's east gate | (34,-15) → (23,-9), 12 m | stone | seam with the temple package |
| w389829593 | east-bank walk south | (20,-73) (18,-78) (4,-119) (14,-185) ... (205,-444), 440 m | paved | past the small white shrines and the Ram Mandir wall |
| w762865028 / w762865029 | west-bank path behind the platforms, south | (-6,-60) (-18,-90) (-35,-159) ... (20,-463) | dirt | |
| w44105887, w80913706 | hilltop paths | (221,-79) → (305,-110) → (361,-138) (to Vishwarupa); (221,-79) → (271,-7) (to Gorakhnath) | paving / dirt | |
| w80913709 | Gorakhnath courtyard (area) | ring (271..297, -9..20) | paving | |
| w112664322 | forest path Gorakhnath → Guhyeshwari | (286,20) → (388,69) → (453,133), 213 m | dirt | |
| w1368994718 | riverside path along Mrigasthali's north edge | (161,274) → (509,111), 419 m | paved | with wall w1373373385 |
| w196281728 | **Guheswori footbridge** | (488,137) → (500,160), 26 m, layer 1 | | steel truss with green railings (guhy_a/12) [P] |
| w112664385 | **Gaurighat bridge** | ≈ (107,280) → (124,275), 18 m, `highway=residential`, layer 1 | paving stones | |
| w112772991 | Ram Mandir compound wall, river side | (42,-180) (24,-178) (20,-151) (18,-133), 64 m | | |
| w341994423, w341994422 | Gorakhnath compound wall | (252,-10) (223,4) (218,7) (224,21) (240,58) (281,42) (275,28) | | |
| w80913711 | "Arya Ghat" crematorium strip (OSM name; see §3.4) | (-17,-93) → (-35,-178), 87 × 3 m | | the platform row of **Bhasmeshvar Ghat** |

Nodes [O]: viewpoint n560496817 (62,-36) on the shrine terrace; "view of the bull" n6021457085 (28,31); Cafe Pashupati
n5175879624 (74,-45); Cafe du Pashupati n6390260885 (199,-70) at the top of the stairs; toilets w686351442 (103,-112)
and n943251150 (27,-105); Bhasmesor Temple n2620944363 (-29,-78); "Bagmati crematory" shops n6161727487 (12,-47),
n6161731685 (-14,-88); Kirateshwar Mahadev n2322977673 (115,232); caves of Naropa n9625428417 (76,66) and Tilopa
n9625428517 (81,70); Shleshmantak Van n4221781891 (407,-267); Guhyeshwari entrance n2592436735 (482,111); public park
entrance n5656895033 (85,265).

### 2.3 Ground, water, trees

* **Paving:** grey stone flags (`#8E8A80`, sunlit `#9C978C` [P]) on every ghat landing, the bridges, the shrine terrace
  and the hilltop lanes; red brick (`#A4553A`) herringbone or running bond in the courtyards of Bhasmeshvar, Gorakhnath
  and the Ram Mandir; packed earth with gravel on the lower forest paths.
* **Water:** olive-brown `#6B6448` (shaded `#484332` [P]), white riffles over the rocks at Gaurighat; reflections of the
  white shrines.
* **Trees [P]:** the east hill above the shrine row carries **tall chir pines and araucarias** (dark columnar crowns
  rising above the terrace, shivalaya_a/11, 15; rd ref_19), the forest (Shleshmantak, w172656314, 16.4 ha; Mirgasthali
  w80913713) is **broadleaved** (sal/Schima/Castanopsis/fig) with big pipal and banyan by the temples (Ram Mandir,
  Gaurighat "religious tree" guhy_d/09); a large pipal stands at the Guhyeshwari forecourt.
* **Ground cover:** dry grass and bare earth on the slopes in winter, lush green in monsoon; rubbish and marigolds at the
  water line (do not model rubbish; marigold scraps and leaf plates are fine).

---

## 3. Structures

Each entry: OSM ids, position, orientation, footprint, height, form, materials, colours (cartoon targets; [P] where
sampled), details, what you see from each side, the main viewpoints, the reference photos, **what the game draws
today**, and the must-have list (repeated compactly in §8).

### 3.1 Ghat embankments of the temple reach (both banks, y = +60 … -180)

* **Form (west bank, Arya Ghat reach, y = -10 … -60):** from the water: **10-15 stone steps** (0.18-0.22 m rise,
  0.3-0.35 m tread) in long flights, broken by **landings 2-4 m deep** and by small projecting **square stone platforms**
  (2.5-4 m) at the water; above, the riverside walk w80913703 (3-4 m wide) runs at ≈ +3.5 m, backed by the buildings and
  the steps rising to the east gate (river_view/ref_01, ghats/ref_01, bagmati/ref_00, ref_05, pandra/ref_05). Red
  sindoor and marigold stains on the bottom steps (arya_a/08).
* **Form (east bank, y = -50 … +15, below the Shivalaya row) [P bridge_a/ref_00, chaitya_a/06, shivalaya_a/11]:**
  three levels: (1) **water-level flight of 8-12 steps** up to a **lower walk 3-4 m wide**; (2) a **2.3-2.8 m stone
  retaining wall pierced by small arched doorways** (0.8-1 m wide, 1.6 m high, every 4-6 m; meditation cells where
  sadhus sit) with a projecting coping; (3) a middle terrace 3-5 m deep reached by **straight stairs set between side
  walls** (1.8-2.5 m wide) at the shrine intervals; (4) the top wall 2.5-3 m with a moulded coping; the **shrine
  terrace** on top at ≈ +7 m above water. Behind the shrines the hill is held by a **high battered stone wall** (the
  12 m-wide dark band in the aerial [A]) with the upper walk w44105885 on top.
* **Material/colour:** grey-brown stone blocks, weathered dark at the bottom: walls `#7A7266` (shade `#3E4041` [P]),
  steps `#8A8478` (sunlit `#9A958A`), copings lighter `#A8A296`; moss streaks `#5E6B3E` near the water.
* **Seen from:** the west bank (the classic photograph: the stepped east bank wall with the white shrine row on top and
  pines behind), the bridges (both banks receding upstream with steps), the east terrace looking down.
* **Today:** **missing**: terrain slopes from a sandy flat bank into a flat water sheet (`v2`, `s_bridges_ghats.png`).
* **Must have:** stepped channel walls on both banks for the whole temple reach (y +60 … -180), three east-bank levels
  with arched cells and stair slots, landing platforms at the water, copings; water sunk ≥ 2 m below the landings;
  colliders so the player can walk every step and landing.

### 3.2 The two footbridges

| | North bridge | South bridge |
|---|---|---|
| OSM | w112664331 (layer 1, `bridge:structure=arch`, paving stones) | w37707651 (same tags) |
| Span | (12,-42) → (29,-54), **20 m**, bearing 125° | (-2,-63) → (20,-73), **24 m**, bearing 114° |
| Width | ≈ 3.3 m between parapets [A] | ≈ 3.3-3.5 m [A] |
| Deck | nearly level with the ghat landings (a hump of 0.3-0.5 m at most); a thin flat stone-clad slab ≈ 0.6-0.8 m deep with a shallow segmental soffit over the channel, resting on stone abutments built into the ghat walls [P bridge_a/07, 10; ghats/ref_05, 07] | same; the Vatsala temple stands at its west head |
| Parapet | **solid grey stone wall ≈ 0.85 m** with a flat projecting coping (0.35 m wide); small end posts; no railing | same; a red-and-white painted strip on the inner face appears on some recent photos [V] |
| Colours | stone `#8E8A80`, coping `#A8A296` (matches `RailingStyle.Newar` grey stone in `docs/research/w2/bridges_flyovers.md`) | same |

* **Seen from:** the ghats (low flat bands across the channel, people leaning on the parapets watching the ghats,
  bridge_a/07, 10); from the bridges: the shrine row to the NE, the temple's gilt roofs to the NW (rd ref_55).
* **Today:** drawn by the bridges package as thin grey decks with pipe rails (`s_bridges_ghats.png`) [check after the
  bridges2 merge].
* **Must have:** both bridges, solid stone parapets with copings, deck flush with the landings, abutments in the ghat
  walls, walkable both ways; the east heads land on the lower east walk w44105888.

### 3.3 Arya Ghat landing below the east gate (west bank, y ≈ -10 … -45) and its three stone shrines

* **What:** the landing and steps directly below the temple's **east gate** (the gate, its stairs and the grey
  metal-roofed **viewing pavilion on a red-balustered terrace** south of the stairs belong to the temple package; see
  §9). Traditionally the royal / VIP cremation ghat [S: nepjol paper via search; gpsmycity; PADT services]; the
  evening Bagmati aarti is performed here and watched from the east bank.
* **Three small stone shikharas** stand at the foot of the east-gate stairs on the landing (ghats/ref_01, complex/ref_06,
  bagmati/ref_00, ref_05): grey stone, square cellas ≈ 2.2-2.8 m, 4-5.5 m tall, curvilinear stone spires with gilt
  finials; the **middle one is the Birupaksha (Virupaksha) shrine** (arya_a/01: arched door with an iron grille and a
  carved stone torana; inside a half-sunk stone torso of Birupaksha with a curled-hair head, rd ref_01, ref_07-10) [S:
  albinger]. A **red-brick shrine with a white bell dome** and a white domed shrine stand beside them at the north
  (ghats/ref_01, eastbank_a/13).
* **Platforms:** two or three raised square stone platforms at the water's edge, higher than the southern ones
  (gpsmycity: "highly elevated stone platforms historically reserved for royalty").
* **Riverside buildings (west bank, between the bridges and the gate):** OSM w913217411 (8,-30) 17.6 × 12.4 m,
  w913244097 (-9,-47) 21.2 × 3.3 m, w913244098 (-10,-36) 16.8 × 5.9 m, w913217412 "Mukti Mandal" (15,-19) 9.4 × 9.1 m
  [O]. In photos (bagmati/ref_02, ref_04, ref_16; ghats/ref_05): a **long two-storey red-brick building along the
  ghat with an arcade of round-arched openings on the ground floor and a parapet**, orange-red lime-washed (`#B8553A`);
  north of the gate a **three-storey Newar building of red brick with carved wooden lattice windows and a projecting
  balcony band**, tiled and metal roofs (ghats/ref_05, rd ref_54). Mukti Mandal is the hospice where the dying wait
  (name only [O]; form [V]).
* **White riverside building w913217416 / w1033128669** (32,2), 27.1 × 15.7 m, `building:colour=white`, height 6 with
  an 11 m part [O]: the large **white three-storey Rana-style block with a red awning** at the north end of Arya Ghat
  (river_view/ref_01 left, pandra/ref_05).
* **Today:** the landing is a sandy flat; the riverside buildings are 4-6-storey pastel apartment blocks ("Mukti Mandal"
  12.5 m, w913244097/098 12-15 m, w913217411 15.5 m; `s_mukti_whitebldg.png`); the three shrines and the platforms are
  missing; the Birupaksha POI is not drawn (POI shrines are never built: `SacredSelector.TrySelectPoi` has no caller).
* **Must have:** stepped landing, three grey stone shikharas with the middle Birupaksha door, the raised royal platforms,
  the arcaded red-brick riverside building and the carved Newar building (2-3 storeys, not 5), the white Rana block, the
  river walk; **no pyre, no body, no smoke** (§4).

### 3.4 Bhasmeshvar Ghat: the cremation platforms (west bank, y = -93 … -178)

* **OSM:** `amenity=crematorium` w80913711 named "Arya Ghat", a strip 87 × 3.3 m along the west bank from (-17,-93) to
  (-35,-178), bearing 194° [O]. **Naming:** most sources call this southern row **Bhasmeshvar Ghat** (public) and keep
  "Arya Ghat" for the royal platforms north of the bridges [S: gpsmycity, Lonely Planet, nepjol]; PADT lists both
  ("Pashupati Aryaghat", "Bhasmeshwor Ghat"). In game labels use "Bhasmeshvar Ghat" here and "Arya Ghat" in §3.3 [V
  cultural reviewer].
* **Form [P ghats/ref_03, ref_09, back/ref_05, y2019/ref_04, ref_09, evening/ref_04; A]:** a row of **6-8 square stone
  platforms** (≈ 3.5-4.5 m square, 0.8-1.4 m above the lowest step), each projecting from the bank into the water on a
  stepped base, joined by a 3-4 m stone walk behind; short flights of steps between them down to the water. Over each
  platform an **open canopy**: four thin steel posts (≈ 3.5-4 m) carrying a **two-tier pyramidal metal roof** (a broad
  lower tier and a small raised top tier, Newar-pagoda-like; grey `#7D8188` or brown-red `#8A4A36`, rusted `#8A5A3C`)
  [P platform_a/08, 12; back/05]; the canopies were added in the 2010s. Behind: the long Briddhashram (§3.5).
* **Colours:** stone `#8E8A80` darkened by soot to `#5A554D` on the tops; canopy roofs grey or brown-red metal.
* **Seen from:** the east bank walk and the Ram Mandir terrace (the row of canopies along the water with the long
  ochre arcaded building behind, the Pancha Deval shikharas above it), the south bridge.
* **Today:** a flat sandy strip; nothing marks the ghat (`s_aryaghat_bridha.png`).
* **Must have:** the platform row with stepped bases and the steel canopies, the walk behind, steps between; platforms
  **always empty**: at most butter lamps, marigolds and a folded cloth (§4).

### 3.5 Pashupati Briddhashram and the west-bank dharmashalas (south of the bridges)

* **Briddhashram** (Social Welfare Centre old people's home; Mother Teresa sisters' section; elderly/ref_00-09):
  OSM multipolygon **r3404627** (-46,-126), outer 65.8 × 28.6 m with two courtyards (33.6 × 12.1 and 13.6 × 12.8),
  bearing 15° [O]. **Two storeys**, long facade to the river: ground floor an **arcade of round arches** (ghats/ref_03,
  eastbank_a/12: ≈ 14-18 bays), upper floor small windows; **ochre-cream plaster arcade `#C9A866` with dark iron
  grilles in the arches** (platform_a/09, 10) and brick above, a **low-pitched corrugated-metal roof** (grey-brown) with wide eaves; inside: a brick-paved square courtyard with 2-storey brick wings,
  carved wooden windows, a wooden gate with the Nepali/English sign "Social Welfare Centre Briddhashram" (elderly/08).
* **Bhasmeshvar Mahadev temple courtyard** (n2620944363 "Bhasmesor Temple" (-29,-78); w112664353 (-47,-79) 14.9 × 13.5
  m and the courtyard west of the south bridge [O/A]): a **white plastered Mughal-style shrine with three domes**
  (a big central dome and two side domes, green-grey `#8C9478` lead/plaster caps, arched doors), in a **red-brick
  paved courtyard** with square brick platforms carrying lingas, a painted mural wall of deities, a red-painted
  two-storey Rana building with white trim behind (rd ref_13, ref_37, ref_41, ref_52, ref_58) [P].
* **Vatsala (Vatsaleshwari) temple** at the west head of the south bridge: **two-roof Newar pagoda** (PADT) with
  carved struts; likely OSM w913244106 (-15,-66) 7.6 × 7.1 m [O/V] (eastbank_a/07 shows a two-tier pagoda at the bridge
  head; bridge_a/04 "mini temple west of the bridge": a single-roof pagoda with red-painted woodwork on a brick plinth).
  Known for the "dancing skeleton" mural by its wall (evening/06).
* **Today:** r3404627 is a 3-storey plain grey flat-roofed box; w112664353 a 3-storey block; w913244106 a 4-storey
  block; Bhasmeshvar node not drawn.
* **Must have:** a long 2-storey arcaded Briddhashram with a metal roof and two courtyards; the three-domed white
  Bhasmeshvar shrine in its brick courtyard; the two-roof Vatsala pagoda at the bridge head; nothing taller than 3
  storeys between the river and Pancha Deval.

### 3.6 Pandra Shivalaya: the row of eleven shrines (east bank, top terrace)

* **OSM [O]:** eleven `building=yes` outlines **w1032548353 … w1032548363** (stone cellas, `building:colour=brown`,
  `height=4`, 4.6 × 4.5 m) each carrying two parts: a white "mansard" roof part 3.6 × 3.5 m from 4 to 7 m
  (w1032548330 … 352) and a white "onion" part 2.6 × 2.5 m from 5 to 8 m (`roof:height=3`). Centres (45,-51), (48,-45),
  (51,-39), (54,-33), (57,-26), (60,-19), (63,-13), (66,-6), (69,0), (72,7), (75,13): **spacing 7.0 m, gap ≈ 2.4 m,
  row bearing 25° (NNE), 71 m long**, square to the row (edges at 26°/116°). Row centre 27.710293 N 85.349274 E.
* **History/meaning [S]:** "Pandhra Shivalaya: a row of fifteen miniature temples containing Shiva lingas in front of
  the eastern bank", erected under Jung Bahadur Rana (1846-77; PADT gives 1862-1876) (another PADT page credits his
  father Kazi Bal Narsingh Kunwar); each houses a linga in memory of a deceased person [S: Latombe]; "the shrine's
  alignment allows an unobstructed view through the open windows of the series of lingas from one end to the other"
  (PADT). The name says fifteen; **eleven stand in this row** [S: Latombe "line of 11", albinger "eleven in all"; O].
* **Form [P complex/ref_05, ref_07, bridge_a/ref_00, shivalaya_a/07, ref_04-06, pandra/*]:**
  * **Plinth:** two moulded stone steps, ≈ 0.5 m in all, projecting 0.25-0.35 m.
  * **Cella:** a cube of **grey dressed stone** 4.6 × 4.5 m, walls ≈ 3.2-3.5 m to the cornice, **corner pilasters**
    with carved figures (Garuda/naga guardians, shivalaya_a/05, 06), a **moulded cornice** band.
  * **Openings:** on the **two row-axis faces** (NNE and SSW) a **doorway** with a carved stone frame (dark, almost
    black wood-toned stone, `#3B3127`) under a **cusped arch with a carved torana** (kirtimukha with makara/naga, in
    stone, shivalaya_a/04), so all eleven doorways line up into one corridor (pandra/02, 06: you see 5-6 shrines deep
    through the doors); on the **river face** (WNW) and the hill face a smaller **arched window** with the same torana.
  * **Inside:** a black stone linga on a round yoni with a spout, on a square stepped stone base, smeared red; wooden
    beam ceiling (pandra/01, 09).
  * **Roof:** a **white lime-plastered bell roof**: a convex flared skirt (the "mansard" part) rising from the cornice
    to ≈ 6 m, a horizontal **ribbed/moulded band**, then a smaller **onion/bell dome** to ≈ 7.3 m and a **pointed kalash
    finial** (white or with a small gilt tip) to ≈ 8.0 m; four small knobs at the skirt corners on some.
  * **In front (river side):** one **tall red-painted flagpole** (≈ 8-9 m, Ø 0.12 m) per shrine with a small
    triangular saffron/orange flag, plus **yellow-painted lamp posts** between some shrines (bridge_a/ref_00); butter
    lamps on the sills.
* **Colours:** stone `#7B6E5E` (sunlit [P]) / `#58544D` (shade [P]); lime white `#EDE9E2` (sunlit [P]) with grey
  weathering `#C6C6C0` [P]; doorframes `#3B3127`; flagpoles `#B3261E`; flags `#F2A93B`; lamp posts `#E8C33A`.
* **Seen from:** **W (the west bank / east gate):** the eleven white bells in a line on top of the stepped wall, pines
  behind (bridge_a/07, 10; chaitya_a/06); **E (the upper walk / viewpoint (62,-36)):** looking over the row and the
  river to the temple's gilt roofs, the east gate and the white Rana blocks (ghats/ref_01, complex/ref_06, rd ref_25,
  45) – **the classic Pashupati view**; **N/S (along the terrace):** the receding row of identical bells, the doors
  lining up (pandra/05); **from the bridges:** the row rising above the ghat walls (rd ref_19, 55).
* **Today:** eleven B0 "ModernUrban" buildings with parts: random-colour brick/plaster boxes (green, yellow, pink,
  cyan), a white box and a grey bullet cap; no doors, no plinth, no poles; the terrace and walls missing
  (`s_shivalaya_row.png`, `v1_eastbank_to_temple.png`).
* **Must have:** 11 identical shrines at the OSM centres and spacing; grey stone cellas with corner pilasters and
  cornice; aligned arched doorways on both row-axis faces (see-through corridor) and arched windows on the side faces;
  stone toranas; white bell skirt + rib band + small dome + pointed finial (≈ 8 m total); two-step plinth; a red
  flagpole with a saffron flag in front of each; the linga inside (visible through the door, sanctum not enterable);
  identical colours (never random tints).

### 3.7 The stair shrines and the east stairs

* **Stair shrines [O]:** six `building=yes` outlines **w258616175, w258616192, w258616171, w258616178, w258616185,
  w258616183**, each 5.8 × 5.6 m, `height=8`, `roof:shape=pyramidal`, with two parts (4.7 × 4.5 m to 9 m and
  3.7 × 3.5 m to 10 m); centres (45,-61), (52,-64), (58,-67), (64,-70), (70,-73), (76,-75): **a row of six at 6.6 m
  spacing, bearing 116° (ESE), stepping up the hill along the north side of the first stair flight w686351441**.
* **Form [P shivalaya_a/01, ref_07, ref_08, mrig_a/01, complex/ref_07 left]:** larger **grey stone shrines on stepped
  plinths that follow the stair** (each 0.6-1 m higher than the last): stone cella ≈ 4.5 m with arched doors and
  toranas, a **stepped stone pyramid / bell roof in two or three diminishing stages** with mouldings and a pointed finial
  (≈ 9-10 m total, per the OSM parts), some plastered white on the upper stages, one **red-brick cella with a white bell
  dome** at the stair head (shivalaya_a/01). Flagpoles and bells between them.
* **The stairs:** 4-5 m wide stone flights with **white lime-washed stepped side walls (parapets) 0.6-1.2 m high**
  (eastbank_a/11), landings every 10-15 steps, the first flight to the shrine terrace level, the long flight w32911668
  turning south-east up through the forest to the **Cafe du Pashupati** and the hilltop shrines (+35 m). Sadhus and
  hawkers sit on the landings; monkeys run along the walls.
* **Today:** six "ModernUrban" boxes with pyramid roofs in random colours (`s_stair_shrines.png`); the stairs are a
  ribbon on the terrain (yellow line in the overlay) without walls.
* **Must have:** six stone shrines stepping up beside the stair at the OSM centres, two-to-three-stage stone bell
  roofs with finials to 9-10 m; real stone steps with white stepped parapets and landings, climbable on foot.

### 3.8 East bank north and south of the row: small shrines, caves, cafés

* **South of the bridges on the east bank** (x 8-12, y -78 … -92): a line of **5-6 small white shrines with bell domes**
  (1.5-2.5 m square, 2.5-3.5 m tall) along the walk at the water [A ghat_zoom; P eastbank_a/13]; red-brick cellas with
  white domes and grey stone shikharas in between.
* **North end of the row** (x 70-80, y 15-30): a **white two-storey house with a red-brown hip roof** closes the
  terrace (river_view/ref_01, bridge_a/07) [P/A].
* **Caves of Naropa and Tilopa** (n9625428417 (76,66), n9625428517 (81,70)) in the rock/retaining wall above the river
  opposite Surya Ghat: low arched cave mouths framed in stone, used by meditators [O/S albinger "caves where sadhus
  stay"].
* **Cafés/toilets:** Cafe Pashupati (74,-45) behind the row; toilets w686351442 (103,-112), 5 × 5 m, 2 storeys [O];
  modest 1-2-storey buildings, not landmarks.

### 3.9 Ram Mandir complex (east bank, south of the stairs)

* **OSM [O]:** compound buildings w112772980 (40,-140) 34.5 × 32.1 m (ring/L, 225 m²), w112772997 (34,-148) 7.9 × 7.8,
  w112772996 (31,-170) 8.3 × 8.0, w112772978/987/988 (39-40,-160…-172) ≈ 4 m squares, w206932181 (68,-140)
  31.8 × 10.6, w206932185 (73,-160) 18 × 17.8, w206932182 (74,-172) 15.3 × 5.7, w913244100 (72,-123), w112772989
  (16,-113) 21.7 × 5.8, w914378446 (21,-96) 15.4 × 6.9, w340959831 (26,-119); river-side wall w112772991. Identified
  from the aerial (a **white-paved courtyard** at x 20-45, y -125 … -180 west of a **large red-roofed courtyard
  building** at x 48-88 [A]) and the photos; the photo series are not geotagged, so the attribution of the
  OSM outlines to the Ram Mandir is by elimination [V].
* **Ram Mandir (Vishnu temple) [P rd ref_00, ref_04, ref_12; ram_mandir/00-02; S PADT "white-domed temple at Ramghat",
  image of Vishnu]:** a **white plastered rectangular temple** ≈ 8 × 8 m (w112772997), ≈ 5 m to the eaves: an
  **arcade of cusped Mughal arches on slender pilasters** on all sides, a deep **dark-red (`#754235` [P]) low hipped
  metal roof** with a white cornice, and on top a **white ribbed bell dome (gumbaz) in two stages with a gilt finial**
  (total ≈ 10-11 m). In front: a **vermilion Hanuman** figure under a small canopy, a stone pillar with a gilt figure,
  stone images; a small white domed shrine beside it; a **huge pipal/banyan** behind.
* **Laxmi Narayan temple** (same courtyard, rd ref_18): a white shrine with a columned front porch (four pillars) and a
  big **ribbed white dome with a gilt kalash**; stone-flagged courtyard, round brick tree platforms (chautara).
* **Sattals:** two-storey **red-painted brick buildings with white trim and wooden windows** around the courtyard (rd
  ref_12 left, ref_18 left), tin roofs; the big red-roofed block east of the court [A].
* **Use:** the gathering place of sadhus during Maha Shivaratri; monthly full-moon music [S: albinger, visitsnepal,
  Tripadvisor via search]. Its terrace looks across to the cremation platforms.
* **Today:** generic 2-3-storey flat-roofed "ModernUrban" boxes; no temple, no dome.
* **Must have:** walled white courtyard with the white arcaded Ram Mandir (red roof + white ribbed dome), the domed
  Laxmi Narayan, red/white sattals, the vermilion Hanuman, a big tree; entrance from the east-bank walk.

### 3.10 Mrigasthali hilltop: the votive-shrine lanes, Shiva temple and ashrams

* **Votive shikharas [P mrig_a/01, 03, 04, 06, 08, 09, 14; gorakh_b/11; complex/ref_01; y2015/09]:** dozens (Wikipedia
  counts 518 mini-temples in the whole Pashupati area [S]) of **grey stone Newar shikharas** 1.5-3 m square, 3-6 m
  tall: a moulded plinth of 2-4 steps, a cella with a cusped arched door and carved torana, corner pilasters with
  figures, and a **stepped/curvilinear stone spire with miniature turrets and a stone finial**; rows of them line the
  **stone-paved lanes** of the hilltop and the stair landings; some **red-brick cellas with white plaster bell domes**
  (mrig_a/11) and lime-washed white shrines between them; **bronze bells on stone or iron posts** (mrig_a/05, 10).
* **"Shiva Temple" w111956443** (215,-49), 22.3 × 10.1 m, bearing 34°, at the top of the stairs [O]: the linga-shrine
  group "on the forest edge" past the café [S: LP via search]; form [V] (a row of stone shrines on a long platform or a
  long sattal-shrine).
* **Prapannacharya Ashram** (rd ref_15, 17): a **two-storey Rana building**, red-painted plaster with white stucco
  window surrounds, arched upper windows, hip roof; on the hilltop near Gorakhnath [P].
* **Yogi Narharinath Samadhi** (gorakh_b/09, 10): a gilt seated statue of the yogi on a stone platform under a
  pavilion frame with a white arch and bell, next to Gorakhnath [P].
* **Deer enclosure** (mrig_a/02, 15): chain-link pens with spotted deer (chital), at the forest edge [P].
* **Today:** the hilltop holds generic 2-6-storey colourful blocks (w112664349, w112664358, w341718823, w1032548328 …)
  and the 2-tier generic pagoda for the "Shiva Temple" (`s_shiva_temple_hill.png`, `v5`); no small shrines at all.
* **Must have:** stone-paved lanes lined with grey stone votive shikharas (instance a kit of 3-4 variants, ≥ 40
  shrines), bells on posts, red-brick white-domed shrines, the Rana ashram, the samadhi statue, the deer pen; low
  buildings only.

### 3.11 Gorakhnath temple and compound

* **OSM [O]:** temple w112664329 (276,7), 5.5 × 4.2 m, bearing 25°; paved courtyard w80913709 (271-297, -9 … 20);
  compound wall w341994423/w341994422; sattals w341718823 (268,16) 27.7 × 12.7, w112664358 (236,28) 16.8 × 7.3,
  w1368994738/739, w1032548329, w913337071 (299,0) 22.2 × 8.5, w112664310 (261,42) 19.5 × 6.9.
* **History [S]:** shikhara-style; houses the paduka (footprints) of Guru Gorakhnath, 18th-century structure (PADT; the
  British Library catalogue dates the foundation to Jayasthiti Malla 1382-95), with a sculpture of Machhendranath; a
  Manakamana shrine nearby; Nath yogi samadhis around.
* **Form [P gorakh_b/00-08, rd ref_27, 40, 49, 53; hill/04]:** a **red-and-white Nagara shikhara**: a red-painted
  square cella (≈ 5 × 4 m) with a **front porch** of two stone pillars and a cusped arch, **four small corner turrets
  (red, with white mini domes)** on a red balustered parapet, on a **red stepped plinth** (2-3 steps); above, a tall
  **white plastered curvilinear spire** with vertical ribs and small turret-copies (urushringa) at its base, an
  amalaka ring and a **gilt kalash finial** with a small red pennant; total ≈ 12-14 m [E]. Facing roughly west/south-west
  into the courtyard [V].
* **Courtyard:** red-brick and stone paving, a **tall bamboo/steel flagpole (≈ 12 m) with a saffron triangular flag**,
  **iron tridents (trishul) 2-3 m** planted in stone bases, a stone **lamp pillar/linga pillar** (dark grey, faceted),
  a **big bronze bell on a frame**, small grey stone shikharas; long **single/two-storey red-painted sattals with red
  hip roofs** (corrugated metal, `#B83A2C`) on three sides; tall pines behind.
* **Colours:** red `#C0262B` (bright, sun) / `#8A2A34` (shade [P]), white spire `#F2EFEA`, gilt `#D9A62E`, stone
  `#6E6A62`.
* **Seen from:** the path from the stairs (white spire above red roofs through the trees), from across the valley the
  white spire is a landmark on the east hill skyline.
* **Today:** a generic **one-tier tile pagoda** hidden among 3-4-storey modern blocks (`s_gorakhnath.png`).
* **Must have:** red cella + porch + corner turrets + white ribbed spire + gilt kalash; red stepped plinth; flagpole,
  tridents, bell, lamp pillar; low red sattals with red roofs around a paved court; compound wall.

### 3.12 Vishwarupa (Bishwarup) temple

* **OSM [O]:** temple w112664373 (395,-150), **20.6 × 19.4 m**, bearing 8°; sattal ring w206887479 (395,-157) **63.2 ×
  58.2 m** outer; long wing w1369046774 (401,-124) 46.1 × 6.0; corner shrines w206887503 (415,-134) and w1369046775
  (376,-164), 6.8 × 6.6 m each. Lat/lon 27.709108 N 85.352673 E.
* **Facts [S PADT]:** "dome-styled temple ... about 108 ft tall [≈ 33 m; treat as [V], photos suggest less], enshrining
  Shiva and Parvati in Yab-Yum (Vishwarupa, a many-headed, many-armed image brought by Jung Bahadur Rana)"; "four
  miniature domed temples enshrining Ram-Sita, Radha-Krishna, Shiva-Parvati, Laxmi-Narayan in the four corners"; Hindus
  only; DoA approved a reconstruction (Himalayan Times, 2021/22).
* **Form [A gorakh.jpg; P vishwarupa/00-01]:** a **square courtyard** enclosed by a **two-storey red-brick sattal** with
  small windows, a pitched corrugated roof (red `#B5503A` from the air) and an inner veranda (vishwarupa/00); inside, a
  **large square temple block with a broad white/light-grey dome** filling the centre (≈ 20 m) and four small **domed
  shrines in the corners**; entrance through a carved wooden arched gateway (vishwarupa/01).
* **Today:** a generic 2-tier tile pagoda inside a **pink four-storey apartment ring** (`s_vishwarupa.png`,
  `compare/vishwarupa.png`).
* **Must have:** 2-storey red brick sattal square (≈ 6-7 m tall) with the entrance on the west/north-west path side [V],
  the central white-domed square temple, four domed corner shrines; closed sanctum.

### 3.13 Guhyeshwari temple

* **OSM [O]:** temple **w112664308** (457,83), 8.5 × 7.7 m, bearing 35°; religious landuse **w1363541450** (462,88)
  58.5 × 58.4 m; enclosing wings w913200312 (441,77) 29.7 × 18.0, w913200313 (478,83) 21.1 × 8.8, w913200311 (464,64)
  17.2 × 13.9, w913200310 (464,72), w913200314 (476,97), w913200315 (444,99), w341994425 (456,107) 26.7 × 7.5;
  entrance n2592436735 (482,111) with steps w253294816, linked by w196281729 to the **Guheswori bridge** w196281728
  over the Bagmati to the north. The node n4525481095 elsewhere is wrong (temples.md §1.3); use the way.
* **Facts [S]:** one of the Shakti Peethas, the second most important temple of the area after Pashupatinath (PADT);
  renovated by Pratap Malla in the 17th century (Wikipedia); worship of a **kalasha over a spring in a pit** surrounded by
  a silver octagonal frame (secondary sources); Hindus only inside.
* **Main shrine [P guhy_snakes/01-11, guhy_gold/00-11]:** a **low, broad, square shrine (≈ 8.5 × 7.7 m, ≈ 4 m to the
  first eave)** standing on a **carved stone base with a low railing** and **rows of small gilt guardian figures** (lions,
  deities) round the foot; walls clad in **gilt-copper repoussé panels** with arched niches and a central gilt torana
  door; **two shallow gilt roof stages** (low-pitched, broad overhangs, gilt copper with repoussé fascias) each hung with
  a **red cloth frill** (`#830D0B` [P]) and a white edge; on top, instead of a pinnacle, an **open gilt crown: four
  gilt serpents (nagas) rising from the corners of the upper roof and arching inwards to hold a central gilt finial /
  chhatra** (the arches ≈ 4-5 m above the roof, total ≈ 11-12 m); small gilt kalash finials and birds at the roof
  corners; small yellow banners on poles at the corners.
* **Courtyard [P]:** **grey stone flag paving** (`#B5B7B8` [P]); in front of the shrine a line of **3-5 tall stone
  pillars (dhvaja stambha, 5-7 m) carrying gilt figures** (lions, a bird, a Garuda-like figure, one with a dark bronze
  top), brass lamp stands, a bell; the court is enclosed by **two-storey sattals of red brick painted red, with
  carved black wooden lattice windows and bright red metal roofs** (`#C0362C`) with a deep overhang; a big tree inside
  one corner.
* **Outside [P guhy_a/00, 03, 10; guhy_snakes/00]:** the compound shows to the path a **long red-painted brick
  plinth wall (≈ 2 m) under a two-to-three-storey red brick building** with dark wooden windows; the **main gate** in
  its middle: a whitewashed gatehouse with yellow pilasters, teal/blue panels painted with deities, **two big painted
  eyes** on the white wall either side of the door, a **gilt torana** over the door, stairs rising inside; a forecourt
  of packed earth with large trees and stalls.
* **Riverside [P guhy_a/05, 11, 13, 15; guhy_gold/06; A]:** a stone ghat along the river south-west of the compound;
  two **octagonal cream-plastered pavilions** ("Shivalaya artistic", Rana era) at (403-438, 113-135) [A]: arcaded on
  each face (Tuscan columns, round arches), a dark stone **multi-turreted spire/dome** crowning each, used for ritual
  bathing; small stone shikharas on the bank; a red brick shrine with a pagoda roof on the far bank.
* **Today:** a **one-tier generic tile pagoda** (`TemplePagoda`, 13 m) inside a ring of colourful flat-roofed blocks
  (`s_guhyeshwari.png`, `compare/guhyeshwari.png`); the pavilions, pillars and gate are missing.
* **Must have:** low square gilt shrine with gilt repoussé walls, two shallow gilt roof stages with red frills, **four
  gilt serpent arches with the central finial**, guardian figures round the base; stone-paved court; pillar row with gilt
  figures; red two-storey sattals with lattice windows and red roofs; the white painted-eye gate in a long red brick
  wall; the two octagonal riverside pavilions; the footbridge to the north.

### 3.14 Kirateshwar Mahadev

* **OSM [O]:** node n2322977673 "किरातेश्वर महादेव / Kirateshwar Mahadev" (115,232) (`name:ru` "Naropa's cave" is a
  mistake); compound building w913217407 (117,227) 20.1 × 16.0 m, wing w913217406 (120,238) 17.7 × 6.0,
  w913217405 (132,227), w913217409 (109,203); paths w686351443/446/447/448 round it. On the **forested hillock inside
  the Gaurighat river loop**, reached from Gaurighat bridge.
* **Facts [S PADT]:** "an open shrine at the top of a hillock across the Bagmati river ... contains a tall shivalinga";
  Kirata-period (before 300 AD); Arjuna received the Pashupatastra here. Famous for full-moon classical music concerts
  (Kirateshwar Sangeet Ashram) [S: search, holidaystonepal].
* **Form [P kirat_a/01-11, guhy_d/11-15]:** an **open-air walled courtyard** (no roof over the image): high
  **red-painted plastered walls** (`#B91B1C` [P]) with white-framed round-arched niches and small lattice windows, a
  white cornice and the white-and-red two-storey sattal wings around; grey stone-flag floor; at the centre the
  **Kirateshwar image: a big stone face-linga ≈ 1.2-1.5 m tall, smeared orange-red**, on a **round stone platform
  (≈ 2.5 m)** with small stone Nandis in front; an **iron frame with a hanging copper water vessel (dhara-patra)**
  over it, brass lamps and bells on iron posts; a painted multicolour door frame at the entrance.
* **Today:** an unnamed `TemplePagoda` outline drawn as a **two-tier generic tile pagoda** plus modern blocks
  (`s_kirateshwar.png`); the POI is not drawn.
* **Must have:** roofless red-walled court with white-framed niches, the orange-red face-linga on its round platform,
  the hanging vessel frame, lamps and bells, the painted gate; forest round the hillock.

### 3.15 Gauri Ghat (west/north bank of the river loop)

* **OSM [O]:** "Gaurighat Hanuman mandir" w255634474 (87,284), 17.6 × 11.6 m, bearing 144°; small buildings
  w913342674/675 (71-75, 270-273); Gaurighat bridge w112664385 (115,277); "public park to free entrance to
  Pashupatinath" n5656895033 (85,265).
* **Form [P gauri_a/00-03, gauri_b/02-09]:** a **stone-paved riverside ghat** on the outer bank of the bend: stone
  steps and walls down to a rocky, fast channel where **natural boulders smeared with red sindoor** are worshipped
  (Gauri); two **red-brick Newar buildings with red metal hip roofs**: a **two-storey pati/sattal with an open ground
  floor of carved wooden columns** (gauri_b/07) and a **three-storey brick house-temple with lattice windows and a
  skirt roof** (gauri_a/00, gauri_b/02, 06); an **orange-red Parvati image under a parasol** on a small platform
  (gauri_a/01); round stone platforms; small white shrines; big trees and the forested Kirateshwar hillock across.
* **Today:** a generic 2-tier tile pagoda by a flat river (`s_gaurighat_hanuman.png`).
* **Must have:** the ghat steps and rocky bed, red brick pati with wooden columns, the 3-storey brick building with red
  roofs, the Parvati shrine with parasol, the bridge to Kirateshwar.

### 3.16 Surya Ghat and the Kailash slope (west bank north of the temple)

* **Kailash** (`leisure=common` w201527615 "Kailash", 30,160 m², centroid (-21,176)) [O]: the wooded rise on the west
  bank north of the temple ("the rocky rise above the Surya Ghat is called Kailash Hill" [S albinger]); stone shrines,
  big trees, monkeys; the riverside walk w80913703 runs along its foot.
* **Surya Ghat:** steps on the west bank around (55-70, 20-50) facing the caves of Naropa/Tilopa [O/S]; form as §3.1
  west bank, smaller.
* **Must have:** the walk continues north past stepped ghats to Surya Ghat; trees and stone shrines on Kailash.

### 3.17 Shleshmantak forest and Mrigasthali

* **OSM [O]:** `landuse=forest` w172656314 (broadleaved, 16.4 ha, centroid (354,-191)) and "Mirgasthali" w80913713
  (7.1 ha, centroid (191,112)); Shleshmantak Van node n4221781891 (407,-267).
* **Facts [S]:** Mrigasthali, where Shiva roamed as a deer; Shleshmantak Van, a sacred grove for rites and meditation
  (PADT).
* **Look [P mrig_a, gorakh_b/11, hill/03, back/06]:** tall broadleaved and pine trees over a dry floor, stone shrines
  scattered under them, stone-paved and dirt paths, retaining walls; **rhesus macaques** everywhere (on walls, roofs,
  shrine domes, trees, the bridges), **spotted deer** in the pen, cows and dogs, pigeons on the ghats.
* **Today:** dense uniform forest canopy from `TreePlacement` (Castanopsis/Schima/Alnus) right up to the shrine
  terrace (`v3_bird_from_south.png`).
* **Must have:** forest kept off the terraces, stairs and lanes; pines/araucarias on the slope above the shrine row;
  macaque troops (W2_DESIGN: 20-40 visible) on the shrine roofs and walls; deer pen.

### 3.18 Pancha Deval (adjacent, west bank)

* **OSM [O]:** multipolygon **r1574190** "Pancha Devalaya" (-93,-96): outer 64.0 × 59.2 m, inner court 42.0 × 41.4 m,
  bearing 12°.
* **Facts [S PADT]:** "five fabulous temples inside a large courtyard surrounded by a three-storied sattal", Rana-era
  "gumbaj-shaped" style, all for Shiva, built over three generations of 19th-century Shah kings.
* **Form [P rd ref_38, 42, 43, 59; ghats/ref_02 background; y2015/09]:** a **brick platform of 4-6 broad steps (≈ 2 m)**
  in the middle of the court carrying **five white plastered Rana-Mughal shikharas**: the central one taller (≈ 15 m),
  four at the corners (≈ 11 m); each a square cella with arched doors and **three tiers of miniature turrets and spires
  diminishing upward**, dark-grey/green-tinted spire caps and small domes, gilt finials; stone animal statues along the
  platform steps; the court is lawn and brick; a **2-3-storey brick sattal** with small windows and metal roofs all
  round (used by the old people's home).
* **Today:** the whole 64 m outline is a **single 2-tier generic tile pagoda 60 m wide** (`s_pancha_deval.png`).
* **Must have:** the courtyard with the stepped platform and five white turreted shikharas, the brick sattal ring.

### 3.19 Rajrajeshwari (south, adjacent)

* "Raja rajeshwaripokhari" pond w951468052 (-58,-264) 22.6 × 19.8 m and Rajarajyashwari hiti n8806825713 (-43,-268)
  [O]; Rajrajeshwari (Nawadurga) temple: **single-storey temple with a brass-plated roof, struts with Asta Bhairav and
  Asta Matrika** (PADT); photo rd ref_16: a one-tier pagoda with a gilt ridged roof and three gilt finials on a brick
  plinth with an iron fence [P]. Outside the core scope; listed so it is not drawn as a modern block.

---

## 4. Life, festivals and the rules for this site

* **Sadhus** (W2_DESIGN: 6 % of walkers at Pashupati): saffron, orange and yellow robes, painted faces, dreadlocks,
  sitting on the east-bank terrace steps by the Shivalayas, in the arched cells of the ghat wall, at the Ram Mandir and
  on the stair landings (y2015/00-05, sadhu/*, eastbank_a/00, 04). Posed, never begging animations.
* **Pilgrims:** bathing at the bottom steps (Arya Ghat, Gauri Ghat), offerings of marigolds and leaf plates, butter
  lamps on the platforms; mourners in white near the platforms (no ritual shown); tourists on the east terrace watching
  the west bank.
* **Evening aarti** (daily at dusk, audio `ghm_amb_ghats_aarti`): priests on the Arya Ghat landing with brass lamps,
  bells, conch; spectators on the east-bank steps.
* **Maha Shivaratri** (Feb/Mar): crowds, sadhus camped on the east bank and Mrigasthali; **Teej** (Aug/Sep): women in
  red saris bathing at the ghats; **Bala Chaturdashi** (Nov/Dec): seed scattering through the forest at night.
* **Animals:** rhesus macaques (on the Shivalaya domes, the bridge parapets, the stair walls; babies on 10 %), pigeons
  on the ghat steps (arya_a/08, 11), cows, dogs, spotted deer in the pen.
* **Binding rules (W2_DESIGN W2-O1, ASSET_MANIFEST, temples.md §7):** **no cremation, no fire on platforms, no bodies,
  no pyres, no smoke from the ghats**; platforms are empty stone with lamps and flowers; sanctums are never entered or
  modelled inside (the Shivalaya lingas may be seen through the open doors as in reality, the doors themselves not
  walkable); Guhyeshwari and Vishwarupa interiors closed (Hindus only in reality); no vehicles anywhere in the area
  (`SACRED_NO_VEHICLE`); calm mode; aircraft audio capped at -6 dB.

---

## 5. Viewpoints the model must survive (render-vs-photo targets)

| # | Eye (local, height above ground) | Look | Photo to match | Must read |
|---|---|---|---|---|
| V1 | east terrace behind the row, (95,-55), +12 m (upper walk / hill) | WSW to the temple | ghats/ref_01, complex/ref_06, rd ref_45 | white bells of the row in the foreground, the river channel with ghats, the east gate stairs and pavilion, the two gilt roofs and gajur above |
| V2 | south bridge, (10,-66), +1.6 m | NNE up the channel | river_view/ref_01, pandra/ref_05 | stepped ghats both sides, the row on its stepped wall to the right, the white Rana block and arcaded red building to the left, pines behind |
| V3 | west bank Bhasmeshvar walk, (-25,-120), +1.6 m | NE | bridge_a/ref_00, rd ref_19 | the east bank's three-level wall with arched cells and the row on top, flagpoles |
| V4 | east bank walk, (12,-110), +1.6 m | W/NW | ghats/ref_03, back/05, eastbank_a/12 | the canopied platform row at the water, the long arcaded Briddhashram, Pancha Deval's white spires above |
| V5 | hill path, (240,0), +1.6 m | E to Gorakhnath | gorakh_b/00-06 | red-and-white spire above a paved court with trident, flagpole, bell and red sattals |
| V6 | Guhyeshwari court, (450,95), +1.6 m | SE to the shrine | guhy_snakes/05-09 | low gilt shrine with red frills and the four-serpent crown; pillar row; red sattals |
| V7 | Guhyeshwari forecourt, (470,120), +1.6 m | SW to the gate | guhy_a/00, 03 | long red wall, white painted-eye gate with gilt torana |
| V8 | Gaurighat, (80,280), +1.6 m | E across the bend | gauri_a/00, gauri_b/06 | brick pati and houses with red roofs, rocky river, wooded Kirateshwar hillock |
| V9 | bird view from S, (150,-330), +180 m | N | aerial core.jpg | the white dotted line of the row along the river, the channel, the temple's gilt square, the courtyards |

The current-state renders from V1-V3 and V9 are `current/v1..v3`; the pairs `compare/*.png` put them next to the photos.

---

## 6. Colours (cartoon targets)

| Token | Hex | Where | Source |
|---|---|---|---|
| ghat stone | `#8A8478` (lit `#9A958A`, shade `#3E4041`) | steps, landings, walls | [P bridge_a/00, river_view/01] |
| stone coping | `#A8A296` | parapets, wall caps, bridge copings | bridges_flyovers.md |
| shrine stone | `#7B6E5E` lit / `#58544D` shade | Shivalaya cellas, votive shikharas | [P] |
| lime white | `#EDE9E2` (weathered `#C6C6C0`) | Shivalaya bells, Ram Mandir, Pancha Deval, Rana blocks | [P] |
| door stone/wood | `#3B3127` | Shivalaya door frames | [P pandra] |
| flagpole red | `#B3261E` | poles, Gorakhnath accents | `s.wood.paint.red` |
| saffron flag | `#F2A93B` | flags, sadhu robes | `s.saffron` |
| lamp-post yellow | `#E8C33A` | east-bank posts | [P] |
| Gorakhnath red | `#C0262B` (shade `#8A2A34`) | cella, turrets, sattals | [P] |
| Gorakhnath white | `#F2EFEA` | spire | [P] |
| Kirateshwar red | `#B91B1C` | court walls | [P] |
| gilt | `#D9A62E` (hi `#F2CC5B`, lo `#9C7317`, deep relief `#663813`) | Guhyeshwari, finials | `s.gilt`, [P] |
| frill red | `#830D0B` with white edge `#F2EEE6` | Guhyeshwari roof frills | [P] |
| sattal red roof | `#C0362C` | Guhyeshwari, Gaurighat roofs | [P] |
| Ram Mandir roof | `#754235` | hipped metal roof | [P] |
| Newar brick | `#A4553A` / dachi `#A0472E` | sattals, pati, courtyards | ref_temples |
| canopy metal | `#7D8188` (rust `#8A5A3C`) | platform canopies | [E] |
| water | `#6B6448` (shade `#484332`) | Bagmati | [P] |
| Guhyeshwari paving | `#B5B7B8` | stone flags | [P] |
| sindoor | `#E0412B` | images, rocks, steps | `s.sindoor` |

---

## 7. What the current game draws for every structure (pack + generators, 2026-10-11 base a87e5a4)

| Structure | Pack record(s) | Generator today | Class |
|---|---|---|---|
| Pashupatinath temple | w913170315 + parts, hero her.ktm.pashupatinath | HeroBuilder pagoda, 21,655 tris | **hero** (temple package) |
| Pandra Shivalaya (11) | w1032548353-363 + 22 parts, ModernUrban | B0 house grammar with parts: random-tint boxes + bullet caps | **plain building** (wrong) |
| Stair shrines (6) | w258616175/192/171/178/185/183 + parts, ModernUrban | boxes with pyramid roofs, random tints | **plain building** |
| Ghat walls, steps, landings (both banks) | none (river area w751466756 + footways) | terrain + flat water area | **missing** |
| North / south footbridge | w112664331, w37707651 (RSTR bridge) | BridgeMesher footbridge, thin deck + rails | **bridge (generic)** |
| Arya Ghat 3 shrines + Birupaksha | not mapped (no record) | nothing | **missing** |
| Riverside dharmashalas (Mukti Mandal etc.) | w913217412, w913217411, w913244097/098, w913217416 | ModernUrban 4-6 storeys | **plain building** (too tall, wrong style) |
| Bhasmeshvar Ghat platforms + canopies | only a POI of kind `Ghat` for w80913711 (no area, no building) | nothing | **missing** |
| Briddhashram | r3404627 ModernUrban | 3-storey grey flat box | **plain building** |
| Bhasmeshvar Mahadev | n2620944363 POI; w112664353 ModernUrban | POI not built; 3-storey block | **missing / plain** |
| Vatsala temple | w913244106 ModernUrban | 4-storey block | **plain building** (wrong) |
| Ram Mandir complex | w112772980, w112772997 … ModernUrban | 2-4-storey boxes | **plain building** |
| Small white shrines (east bank south) | none | nothing | **missing** |
| Votive shikharas on Mrigasthali | none mapped | nothing | **missing** |
| "Shiva Temple" (hill) | w111956443 TemplePagoda | generic 2-tier pagoda | **generic temple** |
| Gorakhnath | w112664329 TemplePagoda | generic 1-tier pagoda (small) | **generic temple** (wrong form) |
| Gorakhnath sattals | w341718823, w112664358 … ModernUrban | 2-6-storey colourful blocks | **plain building** |
| Vishwarupa | w112664373 TemplePagoda; ring w206887479 ModernUrban | generic 2-tier pagoda in a pink 4-storey ring | **generic temple** (wrong form) |
| Guhyeshwari | w112664308 TemplePagoda (`Religious`, 13 m) | generic 1-tier tile pagoda | **generic temple** (wrong form) |
| Guhyeshwari sattals, gate | w913200309-315, w341994425 ModernUrban | colourful flat blocks | **plain building** |
| Octagonal pavilions (Guhyeshwari ghat) | none | nothing | **missing** |
| Kirateshwar | w913217407 TemplePagoda (unnamed); POI n2322977673 | generic 2-tier pagoda | **generic temple** (wrong: open court) |
| Gaurighat Hanuman Mandir | w255634474 TemplePagoda | generic 2-tier pagoda | **generic temple** |
| Gaurighat bridge | w112664385 | BridgeMesher | bridge (generic) |
| Pancha Deval | r1574190 TemplePagoda | one 2-tier pagoda 60 m wide | **generic temple** (badly wrong) |
| Caves of Naropa/Tilopa | POIs n9625428417/517 | nothing | **missing** |
| Forest / trees | w172656314, w80913713 | TreePlacement forest: Castanopsis 1,189, Schima 637, Alnus 211 … in the 4 tiles | generic forest (covers terraces) |

---

## 8. Must-have checklist per structure (for the modeller and the reviewer)

**R1 Ghats and channel**
- [ ] Stone channel walls both banks from y +60 to -180, water ≥ 2 m below the landings, olive-brown water.
- [ ] West bank: long stepped flights, landings, projecting platforms; riverside walk w80913703.
- [ ] East bank: water steps → lower walk → arcaded wall with small arched cells → middle terrace with stair slots →
      top wall → shrine terrace (+7 m); battered upper wall and the upper walk w44105885.
- [ ] Everything walkable with colliders; no railings except where photos show them.

**R2 Footbridges**
- [ ] Two bridges at the OSM lines, 20 m and 24 m, ≈ 3.3 m wide, deck flush with the landings.
- [ ] Solid grey stone parapets ≈ 0.85 m with flat copings; stone abutments in the ghat walls.

**R3 Arya Ghat landing**
- [ ] Stepped landing below the east gate; 2-3 raised royal platforms.
- [ ] Three grey stone shikharas, the middle one the Birupaksha shrine (grilled arched door, torana).
- [ ] Arcaded red-brick riverside building (2 storeys), carved Newar building (3 storeys), white Rana block w913217416.

**R4 Bhasmeshvar Ghat**
- [ ] 6-8 square stone platforms projecting into the water with stepped bases and steps between.
- [ ] Steel canopy with a pyramidal metal roof over each platform; walk behind.
- [ ] Platforms empty (lamps, marigolds only). No fire, smoke, bodies.

**R5 West bank buildings**
- [ ] Briddhashram: 66 × 29 m, 2 storeys, river arcade, metal roof, two courtyards.
- [ ] Bhasmeshvar Mahadev: white three-domed shrine in a brick courtyard with linga platforms and the mural wall.
- [ ] Vatsala: two-roof pagoda at the south bridge's west head.

**R6 Pandra Shivalaya**
- [ ] 11 identical shrines at the OSM centres, 7.0 m apart, row bearing 25°, on the top terrace.
- [ ] Grey stone cella 4.6 m, corner pilasters with figures, cornice, two-step plinth.
- [ ] Aligned arched doorways on both row-axis faces (see-through), arched windows on the other faces, stone toranas.
- [ ] White bell skirt + ribbed band + small dome + pointed finial, ≈ 8 m.
- [ ] Red flagpole with a saffron flag in front of each; lamp posts; linga visible inside.

**R7 Stair shrines and east stairs**
- [ ] Six stone shrines stepping up beside the first flight, 9-10 m, two-to-three-stage stone bell roofs.
- [ ] Stone stair w686351441 + w32911668 with white lime-washed stepped parapets and landings, climbable.

**R8 East bank extras**
- [ ] Small white bell-domed shrines along the walk south of the bridges; white red-roofed house at the north end.
- [ ] Cave mouths of Naropa and Tilopa in the bank opposite Surya Ghat.

**R9 Ram Mandir complex**
- [ ] White walled courtyard; white arcaded temple with red hipped roof and white ribbed dome + gilt finial.
- [ ] Domed Laxmi Narayan shrine; red/white sattals; vermilion Hanuman; big pipal.

**R10 Mrigasthali hilltop**
- [ ] Stone-paved lanes lined with ≥ 40 grey stone votive shikharas (3-4 variants), red-brick white-domed shrines, bells.
- [ ] Rana-style Prapannacharya Ashram; Yogi Narharinath samadhi statue; deer pen.

**R11 Gorakhnath**
- [ ] Red cella with porch and corner turrets, red stepped plinth, white ribbed Nagara spire, gilt kalash (12-14 m).
- [ ] Paved court with flagpole, tridents, bell, lamp pillar, small stone shrines; low red sattals with red roofs.

**R12 Vishwarupa**
- [ ] 63 × 58 m two-storey red brick sattal square; central square temple with a broad white dome; four domed corner shrines.

**R13 Guhyeshwari**
- [ ] Low square gilt shrine with gilt repoussé walls and guardian figures round the base.
- [ ] Two shallow gilt roof stages with red frills; four gilt serpent arches holding the central finial.
- [ ] Stone-flag court, pillar row with gilt figures, lamps, bell; red two-storey sattals with lattice windows, red roofs.
- [ ] Long red brick wall with the white painted-eye gate and gilt torana; forecourt with big trees.
- [ ] Riverside ghat with the two octagonal arcaded pavilions; Guheswori footbridge (steel truss, green rails).

**R14 Kirateshwar**
- [ ] Open red-walled court with white-framed arched niches; orange-red face-linga on a round platform; hanging vessel frame, lamps, bells.

**R15 Gauri Ghat**
- [ ] Riverside ghat steps and sindoor-red boulders; red brick pati with wooden columns; 3-storey brick building with red roofs; Parvati shrine with parasol; Gaurighat bridge.

**R16 Pancha Deval (adjacent)**
- [ ] Stepped brick platform with five white turreted shikharas (centre taller) in a 2-3-storey brick sattal court.

**R17 Forest and life**
- [ ] Trees off terraces, stairs and courts; pines/araucarias above the row; macaques, pigeons, deer, cows, dogs; sadhus.

---

## 9. Seams and open issues for other packages (record, do not edit their files)

1. **Temple package seam** (`mon-pashupati-temple`): the east gate, its stairs (w112776516 and the gate stair down to
   Arya Ghat), the grey metal-roofed viewing pavilion on the red-balustered terrace south of the stairs and the
   compound's riverside wall are theirs; this brief's Arya Ghat landing and the three shrines must meet them at the
   foot of the stairs (≈ (20..30, -10..-40)). Agree on one owner for the riverside buildings w913217411, w913217412,
   w913244097/098, w913217416 (proposed: river package).
2. **Hero hide set / curated DB (data, temples):** the bespoke river-front model must hide every pack building it
   replaces: w1032548353-363 (+ parts w1032548330-352), w258616171/175/178/183/185/192 (+ parts), r3404627,
   w112664353, w913244106, w913244097/098, w913217411/412/416, w1033128669, w112772980/989/996/997/978/987/988,
   w206932181/182/185, w913244100, w914378446, w340959831, w112664329, w341718823, w112664358, w1368994738/739,
   w1032548329, w913337071, w112664310, w111956443, w112664373, w206887479, w206887503, w1369046774/775, w112664308,
   w913200309-315, w341994425, w913217405-407/409, w255634474, r1574190.
3. **Generic sacred selector (temples / heritage):** today `TemplePagoda` outlines without a hero become pagodas: Pancha
   Devalaya (60 m two-tier pagoda), Guhyeshwari, Gorakhnath, Vishwarupa, Kirateshwar (unnamed w913217407),
   Gaurighat Hanuman Mandir, the hill "Shiva Temple". Until the bespoke models land, a **courtyard-sized outline
   (> 30 m) must never become one pagoda**, and these names should map to the right form (shikhara for Gorakhnath,
   dome for Vishwarupa, open court for Kirateshwar).
4. **POI shrines are never built** (`SacredSelector.TrySelectPoi` has no caller): Birupaksha/Arya Ghat shrines,
   Bhasmeshvar, Kirateshwar, Dakshinamurti, Kumari Sthan, the caves, Shleshmantak Van are invisible.
5. **Building grammar (buildings):** ModernUrban with `building:part` children is drawn with **random facade tints**
   (the Shivalayas came out green/yellow/pink). Parts with `building:colour=white` / `brown` should keep those colours.
6. **Water/terrain (nature, cutfill):** river areas are drawn as a flat sheet at bank height in a smooth trench; the
   ghat reach needs a sunk channel with vertical walls (cut) that this package's ghat model sits in.
7. **Trees (nature):** `TreePlacement` fills Mirgasthali w80913713 right over the shrine terrace and stairs; keep a
   clearance mask from the river-front model's colliders (like `KeepTreesOffHeroes`).
8. **Bridges (bridges2):** the two Pashupati footbridges should take the heritage stone parapet with copings, deck flush
   with the landings, no pipe rails; Guheswori bridge is a steel truss footbridge with green rails.
9. **Naming (data/curated):** OSM names the southern platform strip "Arya Ghat"; label it **Bhasmeshvar Ghat** in game
   and use "Arya Ghat" for the royal landing below the east gate [V cultural reviewer].
10. **Data quality:** node n2322977673 carries `name:ru` "Пещера Наропы" (Naropa's cave) for Kirateshwar; the cave
    nodes are separate; Guhyeshwari node n4525481095 is 4 km off (use w112664308).

---

## 10. Dimensions summary (for quick building)

| Item | Plan (m) | Height (m) | Spacing / count | Source |
|---|---|---|---|---|
| Channel at temple ghats | 10-18 wide | water ≈ 2-3.5 below west walk, ≈ 7 below east shrine terrace | | [O][P] |
| Ghat step | tread 0.30-0.35 | rise 0.18-0.22 | flights of 8-15 | [P] |
| East-bank arched cell | 0.8-1.0 wide | 1.6 | every 4-6 m | [P] |
| Footbridges | 20 × 3.3; 24 × 3.4 | parapet 0.85 | 2 | [O][A] |
| Shivalaya | 4.6 × 4.5 cella; roof skirt 3.6; dome 2.6 | cella 3.5 + plinth 0.5; total ≈ 8.0 | 11 at 7.0 m | [O][P] |
| Stair shrine | 5.8 × 5.6 base; 4.7; 3.7 | 8 / 9 / 10 | 6 at 6.6 m | [O] |
| Cremation platform | 3.5-4.5 square | 0.8-1.4 above lowest step; canopy 3.5-4 | 6-8 | [P][A] |
| Briddhashram | 65.8 × 28.6 | ≈ 8 (2 storeys + roof) | | [O][E] |
| Ram Mandir | ≈ 8 × 8 | eaves 5, dome to 10-11 | | [O][P][E] |
| Gorakhnath | cella 5.5 × 4.2 (+ porch) | 12-14 | | [O][E] |
| Vishwarupa | temple 20.6 × 19.4; sattal 63.2 × 58.2 | sattal 6-7; temple "108 ft" [S] / ≈ 18-25 [E][V] | 4 corner shrines 6.8 m | [O][S][A] |
| Guhyeshwari | 8.5 × 7.7; court ≈ 22-25; compound 58.5 | eave 4; roof stages to 7; serpent crown to ≈ 11-12 | 3-5 pillars 5-7 | [O][P][E] |
| Kirateshwar court | ≈ 20 × 16 | walls 3-4; image 1.2-1.5 | | [O][P] |
| Pancha Deval | court 42 × 41; outer 64 × 59 | platform 2; temples 11 / 15 | 5 | [O][P][E] |

---

## 11. Text sources

* Pashupati Area Development Trust, monument pages: Pandhra Shivalaya (https://www.pashupati.org.np/?p=526), Visworupa
  (https://pashupati.org.np/?p=524), Gorakhnath (https://pashupati.org.np/?p=522), Ram Temple, Kirateshwor, Vatsaleshawori,
  Rajarajeshwari, Panchadewal, Guheswori, Pashupati Arya Ghat, Bhasmeshwor Ghat, Shlesmantak Van
  (https://www.pashupati.org.np/category/monument/ pages 1-3).
* Wikipedia: Pashupatinath Temple (https://en.wikipedia.org/wiki/Pashupatinath_Temple), Guhyeshwari Temple
  (https://en.wikipedia.org/wiki/Guhyeshwari_Temple).
* J.-C. Latombe, "Nepal (October 2023): Pashupatinath Temple" (https://ai.stanford.edu/~latombe/mountain/photo/nepal-2023/nepal-2023-6.htm).
* A. Albinger, "Pashupatinath: Shiva's Kathmandu Valley Temple" (https://albinger.me/2018/08/26/pashupatinath-shivas-kathmandu-valley-temple/).
* GPSmyCity, "Cremation Ghats, Kathmandu" (https://www.gpsmycity.com/attractions/cremation-ghats-30863.html).
* Holidays to Nepal photo blog (https://www.holidaystonepal.in/blog/pashupatinath-temple-photos).
* Lonely Planet POIs (Guhyeshwari, Gorakhnath, western cremation ghats), via search summaries.
* The Himalayan Times, "DoA grants approval for reconstruction of Vishwaroop Temple"
  (https://thehimalayantimes.com/kathmandu/doa-grants-approval-for-reconstruction-of-vishwaroop-temple/).
* Repo: `docs/W2_DESIGN.md` (W2-O1, §3.4 rows 5 and 51, life tables), `docs/research/w2/temples.md` §4.3,
  `docs/research/w2/ref_temples.md`, `docs/research/w2/bridges_flyovers.md` (heritage parapet).

---

## 12. Photo credits (references only; nothing copied into the repo)

Paths are under `/home/user/wt/refs/mon-pashupati-river/`. Licences as given by Openverse.

| Ref | Title | Creator | Licence | Source |
|---|---|---|---|---|
| ghats/ref_01 | Le site sacré de Pashupatinath (Katmandou) | dalbera | by 2.0 | https://www.flickr.com/photos/72746018@N00/8630934152 |
| ghats/ref_02 | Le site sacré de Pashupatinath (Katmandou) | dalbera | by 2.0 | https://www.flickr.com/photos/72746018@N00/8629829507 |
| ghats/ref_03 | Les ghats des crémations de Pashupatinath (Katmandou) | dalbera | by 2.0 | https://www.flickr.com/photos/72746018@N00/8630936794 |
| ghats/ref_05 | Le site sacré de Pashupatinath (Katmandou) | dalbera | by 2.0 | https://www.flickr.com/photos/72746018@N00/8630937750 |
| ghats/ref_07 | « Ghats » de crémation à Pashupatinath | girolame | by 2.0 | https://www.flickr.com/photos/91241612@N00/6989938202 |
| ghats/ref_09 | Burning Ghats at Pashupatinath, Kathmandu Nepal | eriktorner | by-nc-sa 2.0 | https://www.flickr.com/photos/39267804@N05/13256086845 |
| river_view/ref_01 | Pashupatinath temple, Kathmandu, August 2011 | leonyaakov | by-nc-sa 2.0 | https://www.flickr.com/photos/106447493@N05/17154590799 |
| river_view/ref_07 | Funeral - Pashupatinath Temple - Kathmandu | Phil @ Delfryn Design | by-nc-sa 2.0 | https://www.flickr.com/photos/43555170@N00/5248469037 |
| bagmati/ref_00 | Pashupatinath-Bagmati River | Cheryl Marland | by 2.0 | https://www.flickr.com/photos/13654353@N08/3012580126 |
| bagmati/ref_02 | Pashupatinath-Bagmati-River | Cheryl Marland | by 2.0 | https://www.flickr.com/photos/13654353@N08/3017017676 |
| bagmati/ref_04 | Pashupatinath-Bagmati-River | Cheryl Marland | by 2.0 | https://www.flickr.com/photos/13654353@N08/3012577820 |
| bagmati/ref_05 | Pashupatinath-Bagmati River | Cheryl Marland | by 2.0 | https://commons.wikimedia.org/w/index.php?curid=8699126 |
| complex/ref_01 | Pashupatinath Temple Complex (HDR) | Grant Eaton | by-nc-nd 2.0 | https://www.flickr.com/photos/62606667@N04/8343916222 |
| complex/ref_05 | Pashupatinath Temple complex, Kathmandu | gorbulas_sandybanks | by-nc-sa 2.0 | https://www.flickr.com/photos/26621269@N00/8480872335 |
| complex/ref_06 | Pashupatinath Temple complex, Kathmandu | gorbulas_sandybanks | by-nc-sa 2.0 | https://www.flickr.com/photos/26621269@N00/8481016461 |
| complex/ref_07 | Pashupatinath Temple complex, Kathmandu | gorbulas_sandybanks | by-nc-sa 2.0 | https://www.flickr.com/photos/26621269@N00/8482106230 |
| y2015/ref_00 | Sadhus / Pashupatinath temple in Kathmandu, Nepal (Explored, 16/04/2015) | fotopamas | by 2.0 | https://www.flickr.com/photos/126011817@N06/16972176188 |
| y2015/ref_07 | 2015-03-30 04-15 Nepal 159 Kathmandu, Pashupatinath Temple | Allie_Caulfield | by 2.0 | https://www.flickr.com/photos/28577026@N02/17361182461 |
| y2015/ref_09 | 2015-03-30 04-15 Nepal 167 Kathmandu, Pashupatinath Temple | Allie_Caulfield | by 2.0 | https://www.flickr.com/photos/28577026@N02/17361700105 |
| y2019/ref_04 | Very open Hindu cremations at Pashupatinath | shankar s. | by 2.0 | https://www.flickr.com/photos/77742560@N06/49626152628 |
| y2019/ref_09 | A Hindu cremation- very open, very public | shankar s. | by 2.0 | https://www.flickr.com/photos/77742560@N06/49626650306 |
| back/ref_01 | Pandra Shivalaya | Baron Reznik | by-nc-sa 2.0 | https://www.flickr.com/photos/56077632@N00/46680418192 |
| back/ref_05 | Very open Hindu cremations at Pashupatinath | shankar s. | by 2.0 | https://www.flickr.com/photos/77742560@N06/49626152628 |
| back/ref_06 | A monkey on a tree on the steps to the Syamwanbhunath stupa | shankar s. | by 2.0 | https://www.flickr.com/photos/77742560@N06/49626061708 |
| hill/ref_03 | A monkey on a tree on the steps to the Syamwanbhunath stupa | shankar s. | by 2.0 | https://www.flickr.com/photos/77742560@N06/49626061708 |
| hill/ref_04 | Shrines to the gods, on the hill above Pashupatinath | SarahDepper | by 2.0 | https://www.flickr.com/photos/86657422@N00/3825823502 |
| white_temples/ref_02 | The Temple | calamur | by-nc-nd 2.0 | https://www.flickr.com/photos/63189496@N00/3027837124 |
| elderly/ref_00 | Pashupatinath-elderly-home-lunchtime | Cheryl Marland | by 2.0 | https://www.flickr.com/photos/13654353@N08/3016180921 |
| elderly/ref_01 | Pashupatinath-elderly-home-square | Cheryl Marland | by 2.0 | https://www.flickr.com/photos/13654353@N08/3016180283 |
| elderly/ref_02 | Pashupatinath-elderly-home-square | Cheryl Marland | by 2.0 | https://www.flickr.com/photos/13654353@N08/3017014966 |
| elderly/ref_08 | Pashupatinath-Mother-Teresa-elderly-home | Cheryl Marland | by 2.0 | https://www.flickr.com/photos/13654353@N08/3016177705 |
| evening/ref_04 | Burning Ghats at Pashupatinath, Kathmandu Nepal | eriktorner | by-nc-sa 2.0 | https://www.flickr.com/photos/39267804@N05/13256086845 |
| evening/ref_06 | Drawings and carvings at Pashupatinath Temple, Kathmandu Nepal | eriktorner | by-nc-sa 2.0 | https://www.flickr.com/photos/39267804@N05/13256483344 |
| evening/ref_08 | Pashupatinath, Kathmandu Nepal | eriktorner | by-nc-sa 2.0 | https://www.flickr.com/photos/39267804@N05/13256079775 |
| rd_pashupati/ref_00 | Vishnu Temple Ram Mandir Pasupatinath Area Pashupati Kathmandu Nepal Rajesh Dhun | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=109983705 |
| rd_pashupati/ref_01 | Birupaksha Temple Birupachha Statue Arya Ghat Pashupati Area Pashupati Temple Pa | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110010232 |
| rd_pashupati/ref_02 | Bishworoop Temple Mrigasthali Pashupati Kathmandu Nepal Rajesh Dhungana (1) | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=111437360 |
| rd_pashupati/ref_03 | Bishworoop Temple Mrigasthali Pashupati Kathmandu Nepal Rajesh Dhungana (2) | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=111437358 |
| rd_pashupati/ref_04 | Vishnu Temple Ram Mandir Pasupatinath Area Pashupati Kathmandu Nepal Rajesh Dhun | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=109983690 |
| rd_pashupati/ref_07 | Birupaksha Temple Birupachha Statue Arya Ghat Pashupati Area Pashupati Temple Pa | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110010228 |
| rd_pashupati/ref_08 | Birupaksha Temple Birupachha Statue Arya Ghat Pashupati Area Pashupati Temple Pa | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110010241 |
| rd_pashupati/ref_09 | Birupaksha Temple Birupachha Statue Arya Ghat Pashupati Area Pashupati Temple Pa | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110010233 |
| rd_pashupati/ref_10 | Birupaksha Temple Birupachha Statue Arya Ghat Pashupati Area Pashupati Temple Pa | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110010238 |
| rd_pashupati/ref_11 | Shiva Temple Sankaracharya Math Pashupati Kathmandu Nepal Rajesh Dhungana | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110174465 |
| rd_pashupati/ref_12 | Vishnu Temple Ram Mandir Pasupatinath Area Pashupati Kathmandu Nepal Rajesh Dhun | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=109983698 |
| rd_pashupati/ref_13 | Yadav Mukteshwor Mahadev Temple Bhasmeshwor Mahadev Temple Pashupati Kathmandu N | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110174824 |
| rd_pashupati/ref_15 | Prapannacharya Ashram Mrigasthali Pashupati Kathmandu Nepal Rajesh Dhungana 2 | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110137410 |
| rd_pashupati/ref_16 | Rajrajeshwori Durga Temple Pashupati Kathmandu Nepal Rajesh Dhungana | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110138441 |
| rd_pashupati/ref_17 | Prapannacharya Ashram Mrigasthali Pashupati Kathmandu Nepal Rajesh Dhungana 1 | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110137408 |
| rd_pashupati/ref_18 | Laxmi Narayan Temple Ram Mandir Pashupatinath Kathmandu Nepal Rajesh Dhungana (2 | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110083144 |
| rd_pashupati/ref_19 | Shivalaya Pashupatinath Kathmandu Nepal Rajesh Dhungana (5) | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110710706 |
| rd_pashupati/ref_25 | Pashupati Temple Area Pashupati Temple Kathmandu Nepal Rajesh Dhungana (26) | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=114532158 |
| rd_pashupati/ref_27 | Gorakhnath or Goraksanath Temple Mrigasthali Pasupati Kathmandu Nepal Rajesh Dhu | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110051340 |
| rd_pashupati/ref_37 | Bhasmeshwor Mahadev Temple Pashupati Area Pashupatinath Temple Kathmandu Nepal R | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110006744 |
| rd_pashupati/ref_38 | Panchadewal Pashupatinath Kathmandu Nepal Rajesh Dhungana (20) | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110777756 |
| rd_pashupati/ref_40 | Gorakhnath or Goraksanath Temple Mrigasthali Pasupati Kathmandu Nepal Rajesh Dhu | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110051323 |
| rd_pashupati/ref_41 | Bhasmeshwor Mahadev Temple Pashupati Area Pashupatinath Temple Kathmandu Nepal R | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110006788 |
| rd_pashupati/ref_42 | Panchadewal Pashupatinath Kathmandu Nepal Rajesh Dhungana (26) | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110777791 |
| rd_pashupati/ref_43 | Panchadewal Pashupatinath Kathmandu Nepal Rajesh Dhungana (21) | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110777757 |
| rd_pashupati/ref_45 | Pashupati Temple Area Pashupati Temple Kathmandu Nepal Rajesh Dhungana (29) | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=114532167 |
| rd_pashupati/ref_48 | Pashupati Temple Area Pashupati Temple Kathmandu Nepal Rajesh Dhungana (5) | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=114532130 |
| rd_pashupati/ref_49 | Gorakhnath or Goraksanath Temple Mrigasthali Pasupati Kathmandu Nepal Rajesh Dhu | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110051365 |
| rd_pashupati/ref_52 | Bhasmeshwor Mahadev Temple Pashupati Area Pashupatinath Temple Kathmandu Nepal R | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110006792 |
| rd_pashupati/ref_53 | Gorakhnath or Goraksanath Temple Mrigasthali Pasupati Kathmandu Nepal Rajesh Dhu | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110051325 |
| rd_pashupati/ref_54 | Pashupati Temple Area Pashupati Temple Kathmandu Nepal Rajesh Dhungana (21) | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=114532151 |
| rd_pashupati/ref_55 | Pashupati Temple Area Pashupati Temple Kathmandu Nepal Rajesh Dhungana (19) | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=114532148 |
| rd_pashupati/ref_57 | Birateshwor Mahadev Pashupati Area Pashupatinath Kathmandu Nepal Rajesh Dhungana | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110011753 |
| rd_pashupati/ref_58 | Bhasmeshwor Mahadev Temple Pashupati Area Pashupatinath Temple Kathmandu Nepal R | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110006725 |
| rd_pashupati/ref_59 | Panchadewal Pashupatinath Kathmandu Nepal Rajesh Dhungana (28) | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110777805 |
| guhy_a/ref_00 | 2015 Earthquake in Nepal-Guhyeshwari Temple Area (2) | Nabin K. Sapkota | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=39888388 |
| guhy_a/ref_01 | Guhyeshwari Temple Primises 01 | Nabin K. Sapkota | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=42807495 |
| guhy_a/ref_02 | Toran of Guhyeshwari | Jujubhaju | cc0 1.0 | https://commons.wikimedia.org/w/index.php?curid=173342681 |
| guhy_a/ref_03 | Guhyeshwari Temple (2) | Prof. Mortel | by-nc-sa 2.0 | https://www.flickr.com/photos/43714545@N06/6856618571 |
| guhy_a/ref_04 | Guhyeshwari Temple (4) | Prof. Mortel | by-nc-sa 2.0 | https://www.flickr.com/photos/43714545@N06/6856664241 |
| guhy_a/ref_05 | Guhyeshwari Temple (1) | Prof. Mortel | by-nc-sa 2.0 | https://www.flickr.com/photos/43714545@N06/6856605505 |
| guhy_a/ref_10 | I wasn't allowed into Guhyeshwari Temple | gorbulas_sandybanks | by-nc-sa 2.0 | https://www.flickr.com/photos/26621269@N00/8481075477 |
| guhy_a/ref_11 | Shivalaya Artistic Shivalya Guhyeshwari Pashupatinath Kathmandu Nepal Rajesh Dhu | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110523481 |
| guhy_a/ref_12 | In and around Guhyeshwari Temple a Shakta pitha 46 | Goutam1962 | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=162504297 |
| guhy_a/ref_13 | Shivalaya Artistic Shivalya Guhyeshwari Pashupatinath Kathmandu Nepal Rajesh Dhu | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110523500 |
| guhy_a/ref_14 | In and around Guhyeshwari Temple a Shakta pitha 23 | Goutam1962 | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=162504166 |
| guhy_a/ref_15 | Shivalaya Artistic Shivalya Guhyeshwari Pashupatinath Kathmandu Nepal Rajesh Dhu | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110523472 |
| guhy_snakes/ref_00 | Guhyeshwari Temple Pashupatinath Kathmandu Nepal Rajesh Dhungana (2) | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110518030 |
| guhy_snakes/ref_01 | Guhyeshwari Temple Pashupatinath Kathmandu Nepal Rajesh Dhungana (16) | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110518153 |
| guhy_snakes/ref_05 | Guhyeshwari Temple Pashupatinath Kathmandu Nepal Rajesh Dhungana (6) | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110518063 |
| guhy_snakes/ref_07 | Guhyeshwari Temple Pashupatinath Kathmandu Nepal Rajesh Dhungana (28) | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110518349 |
| guhy_snakes/ref_09 | Guhyeshwari Temple Pashupatinath Kathmandu Nepal Rajesh Dhungana (14) | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110518143 |
| guhy_gold/ref_00 | Guhyeshwari Temple Pashupatinath Kathmandu Nepal Rajesh Dhungana (16) | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110518153 |
| guhy_gold/ref_06 | Guhyeshwari Temple Pashupatinath Kathmandu Nepal Rajesh Dhungana (1) | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110518032 |
| guhy_gold/ref_08 | Guhyeshwari Temple Pashupatinath Kathmandu Nepal Rajesh Dhungana (15) | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110518146 |
| guhy_d/ref_09 | Religious Tree | anantal | by 2.0 | https://www.flickr.com/photos/33088287@N00/507912989 |
| guhy_d/ref_11 | Kirateshwor Mahadev Temple Guheshwori Pashupatinath Kathmandu Nepal Rajesh Dhung | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110428186 |
| kirat_a/ref_01 | Kirateshwor Mahadev Temple Guheshwori Pashupatinath Kathmandu Nepal Rajesh Dhung | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110428186 |
| kirat_a/ref_02 | Kirateshwor Mahadev Temple Guheshwori Pashupatinath Kathmandu Nepal Rajesh Dhung | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110428177 |
| kirat_a/ref_03 | Kirateshwor Mahadev Temple Guheshwori Pashupatinath Kathmandu Nepal Rajesh Dhung | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110428143 |
| kirat_a/ref_05 | Kirateshwor Mahadev Temple Guheshwori Pashupatinath Kathmandu Nepal Rajesh Dhung | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110428162 |
| gorakh_b/ref_00 | Gorakhnath or Goraksanath Temple Mrigasthali Pasupati Kathmandu Nepal Rajesh Dhu | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110051340 |
| gorakh_b/ref_01 | Gorakhnath or Goraksanath Temple Mrigasthali Pasupati Kathmandu Nepal Rajesh Dhu | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110051323 |
| gorakh_b/ref_06 | Gorakhnath or Goraksanath Temple Mrigasthali Pasupati Kathmandu Nepal Rajesh Dhu | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110051328 |
| gorakh_b/ref_09 | Yogi Narharinath Samadhi Sthal Cemeteries Mrigasthali Gorakhnath Temple Pashupat | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110172332 |
| gorakh_b/ref_10 | Yogi Narharinath Samadhi Sthal Cemeteries Mrigasthali Gorakhnath Temple Pashupat | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110172334 |
| gorakh_b/ref_11 | Shiva Temples Mrigasthali Pashupatinath Kathmandu Nepal Rajesh Dhungana (01) | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110242952 |
| gauri_a/ref_00 | Gaurighat Photo 19th April 2015 | DazLyn | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=52936880 |
| gauri_a/ref_01 | Gaurighat - Parvati Goddess | DazLyn | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=52937923 |
| gauri_a/ref_02 | File:Towards Gaurighat (the other side of the temple) - panoramio.jpg | aabhash | by-sa 3.0 | https://commons.wikimedia.org/w/index.php?curid=55278067 |
| gauri_a/ref_03 | Gaurighat, Pashupatinath (3016988258) | Cheryl Marland from Seattle, USA | by 2.0 | https://commons.wikimedia.org/w/index.php?curid=101229820 |
| gauri_b/ref_02 | Gauri Ghat Guheshwori Pashupatinath Kathmandu Nepal Rajesh Dhungana (3) | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110458497 |
| gauri_b/ref_06 | Gauri Ghat Guheshwori Pashupatinath Kathmandu Nepal Rajesh Dhungana (2) | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110458498 |
| gauri_b/ref_07 | Gauri Ghat Guheshwori Pashupatinath Kathmandu Nepal Rajesh Dhungana (4) | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110458504 |
| gauri_b/ref_09 | Gaurighat Guheshwori Pashupatinath Kathmandu Nepal Rajesh Dhungana (1) | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110458496 |
| mrig_a/ref_01 | Pashupatinath-Mrigasthali-46-Votivstupas-2014-gje | Gerd Eichmann | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=85814204 |
| mrig_a/ref_02 | Deers in Mrigasthali, Kathmandu (2) | Babin Dulal | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=135346236 |
| mrig_a/ref_03 | Pashupatinath-Mrigasthali-34-Votivstupas-2014-gje | Gerd Eichmann | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=85814197 |
| mrig_a/ref_04 | Pashupatinath-Mrigasthali-38-Votivstupas-2014-gje | Gerd Eichmann | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=85814199 |
| mrig_a/ref_05 | Pashupatinath-Mrigasthali-42-Glocke-2014-gje | Gerd Eichmann | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=85814201 |
| mrig_a/ref_06 | Pashupatinath-Mrigasthali-28-2013-gje | Gerd Eichmann | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=85814193 |
| mrig_a/ref_08 | Pashupatinath-Mrigasthali-44-Votivstupas-2014-gje | Gerd Eichmann | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=85814202 |
| mrig_a/ref_09 | Pashupatinath-Mrigasthali-40-Votivstupas-2014-gje | Gerd Eichmann | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=85814200 |
| mrig_a/ref_10 | Pashupatinath-Mrigasthali-20-Glocke-2013-gje | Gerd Eichmann | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=85814189 |
| mrig_a/ref_11 | Pashupatinath-Mrigasthali-48-Rinder-2014-gje | Gerd Eichmann | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=85814205 |
| mrig_a/ref_14 | Pashupatinath-Mrigasthali-04-2007-gje | Gerd Eichmann | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=85814172 |
| mrig_a/ref_15 | Deers in Mrigasthali, Kathmandu (10) | Babin Dulal | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=135346270 |
| pandra/ref_01 | Shiva linga inside Pandra Shivalaya -Pashupatinath Temple-1887 | Bijay Chaurasia | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=82383552 |
| pandra/ref_02 | Pandra Shivalaya | Mabacam | by-nc-nd 2.0 | https://www.flickr.com/photos/59275783@N04/6641060029 |
| pandra/ref_05 | Pashupatinath Temple with Pandra Shivalaya 1 | Dudva | cc0 1.0 | https://commons.wikimedia.org/w/index.php?curid=182408550 |
| pandra/ref_06 | Shiva linga inside Pandra Shivalaya -Pashupatinath Temple-1893 | Bijay Chaurasia | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=82383614 |
| pandra/ref_09 | Shiva linga inside Pandra Shivalaya -Pashupatinath Temple-1885 | Bijay Chaurasia | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=82383536 |
| shivalaya_a/ref_01 | Pashupatinath Temple - Kathmandu | Phil @ Delfryn Design | by-nc-sa 2.0 | https://www.flickr.com/photos/43555170@N00/5249121350 |
| shivalaya_a/ref_04 | Votive shrines detail - Pashupatinath Temple - Kathmandu | Phil @ Delfryn Design | by-nc-sa 2.0 | https://www.flickr.com/photos/43555170@N00/5248494529 |
| shivalaya_a/ref_05 | Votive shrines Detail - Pashupatinath Temple - Kathmandu | Phil @ Delfryn Design | by-nc-sa 2.0 | https://www.flickr.com/photos/43555170@N00/5249107038 |
| shivalaya_a/ref_06 | Stone Carving | Mabacam | by-nc-nd 2.0 | https://www.flickr.com/photos/59275783@N04/6640984651 |
| shivalaya_a/ref_07 | Votive shrines - Pashupatinath Temple - Kathmandu | Phil @ Delfryn Design | by-nc-sa 2.0 | https://www.flickr.com/photos/43555170@N00/5249067444 |
| shivalaya_a/ref_08 | Votive shrines - Pashupatinath Temple - Kathmandu | Phil @ Delfryn Design | by-nc-sa 2.0 | https://www.flickr.com/photos/43555170@N00/5248483313 |
| shivalaya_a/ref_11 | Shivalaya Pashupatinath Kathmandu Nepal Rajesh Dhungana (18) | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110710790 |
| shivalaya_a/ref_15 | Shivalaya Pashupatinath Kathmandu Nepal Rajesh Dhungana (7) | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110710719 |
| chaitya_a/ref_06 | Chaitya | kudumomo | by 2.0 | https://www.flickr.com/photos/9751325@N02/6336578885 |
| bridge_a/ref_00 | Pashupatinath | Cheryl Marland | by 2.0 | https://www.flickr.com/photos/13654353@N08/3011743703 |
| bridge_a/ref_04 | Mini temple west of the bridge | markhorrell | by-nc-sa 2.0 | https://www.flickr.com/photos/41849531@N04/18808205103 |
| bridge_a/ref_07 | Pashupatinath-Bagmatibruecke-24-2015-gje | Gerd Eichmann | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=85811661 |
| bridge_a/ref_10 | Pashupatinath-Bagmatibruecke-10-Votivstupas-2007-gje | Gerd Eichmann | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=85811654 |
| ghat_a/ref_00 | Pashupatinath ghats | germeister | by-nc-nd 2.0 | https://www.flickr.com/photos/90005740@N00/271291929 |
| ghat_a/ref_13 | Cremation Ghat | Mabacam | by-nc-nd 2.0 | https://www.flickr.com/photos/59275783@N04/6640959057 |
| eastbank_a/ref_00 | Sadhus | Mabacam | by-nc-nd 2.0 | https://www.flickr.com/photos/59275783@N04/6640951393 |
| eastbank_a/ref_03 | Cremations at Pashupatinath | Jorge Lascar | by 2.0 | https://www.flickr.com/photos/8721758@N06/17824641266 |
| eastbank_a/ref_04 | Temple sadhus at Pashupatinath | Jorge Lascar | by 2.0 | https://www.flickr.com/photos/8721758@N06/17831526112 |
| eastbank_a/ref_07 | Pashupatinath | Jorge Lascar | by 2.0 | https://www.flickr.com/photos/8721758@N06/17804565536 |
| eastbank_a/ref_11 | Pashupatinath step walls | Jorge Lascar | by 2.0 | https://www.flickr.com/photos/8721758@N06/17643004248 |
| eastbank_a/ref_12 | Cremation ceremonies across the Bagmati river - Pashupatinath | Jorge Lascar | by 2.0 | https://www.flickr.com/photos/8721758@N06/17831498792 |
| eastbank_a/ref_13 | Pashupatinath | Jorge Lascar | by 2.0 | https://www.flickr.com/photos/8721758@N06/17643032778 |
| arya_a/ref_01 | Birupaksha Temple Birupachha Statue Arya Ghat Pashupati Area Pashupati Temple Pa | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110010225 |
| arya_a/ref_08 | Brahamma Naal Tarkeshwor Mahadev Arya Ghat Pashupatinath Kathmandu Nepal Rajesh  | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110677751 |
| arya_a/ref_11 | Brahamma Naal Tarkeshwor Mahadev Arya Ghat Pashupatinath Kathmandu Nepal Rajesh  | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=110677740 |
| ram_mandir/ref_00 | Vishnu Temple Ram Mandir Pasupatinath Area Pashupati Kathmandu Nepal Rajesh Dhun | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=109983705 |
| ram_mandir/ref_01 | Vishnu Temple Ram Mandir Pasupatinath Area Pashupati Kathmandu Nepal Rajesh Dhun | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=109983690 |
| ram_mandir/ref_02 | Vishnu Temple Ram Mandir Pasupatinath Area Pashupati Kathmandu Nepal Rajesh Dhun | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=109983691 |
| vishwarupa/ref_00 | Bishworoop Temple Mrigasthali Pashupati Kathmandu Nepal Rajesh Dhungana (1) | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=111437360 |
| vishwarupa/ref_01 | Bishworoop Temple Mrigasthali Pashupati Kathmandu Nepal Rajesh Dhungana (2) | Rajesh Dhungana | by-sa 4.0 | https://commons.wikimedia.org/w/index.php?curid=111437358 |
| platform_a/ref_04 | Steps and cremation platform | markhorrell | by-nc-sa 2.0 | https://www.flickr.com/photos/41849531@N04/19428738375 |
| platform_a/ref_08 | Poor Man's Cremation | Grant Eaton | by-nc-nd 2.0 | https://www.flickr.com/photos/62606667@N04/8343926616 |
| platform_a/ref_09 | Funeral Pyre for the Wealthy | Grant Eaton | by-nc-nd 2.0 | https://www.flickr.com/photos/62606667@N04/8343924918 |
| platform_a/ref_10 | Burning Man | Grant Eaton | by-nc-nd 2.0 | https://www.flickr.com/photos/62606667@N04/8342866545 |
| platform_a/ref_12 | Very open Hindu cremations at Pashupatinath | shankar s. | by 2.0 | https://www.flickr.com/photos/77742560@N06/49626152628 |
| stairs_a/ref_00 | Pashupatinath-stairs | Cheryl Marland | by 2.0 | https://www.flickr.com/photos/13654353@N08/3012585128 |
| stairs_a/ref_02 | Pashupatinath ghats | germeister | by-nc-nd 2.0 | https://www.flickr.com/photos/90005740@N00/271291929 |
| sadhu/ref_00 | Pashupatinath Sadhu | Mabacam | by-nc-nd 2.0 | https://www.flickr.com/photos/59275783@N04/23459517881 |

Aerial imagery used for measurement only: Esri World Imagery (© Esri, Maxar, Earthstar Geographics), z19 tiles fetched
for reference, crops kept outside the repo in `/home/user/wt/previews/mon-pashupati-river/aerial/`.
