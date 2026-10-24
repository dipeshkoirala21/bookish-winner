# Boudhanath (Boudha Stupa): structure-by-structure reference brief

Package `mon-boudha` (heritage research, W2 detail pass, monuments round). Owner: "National Heritage and Monuments
sites should look exactly as in real life ... do an in-depth research about the places ... carefully design the 3D model
of it." This brief is what the modeller builds Boudha from: the site plan, every structure with its numbers, colours and
details, what is in the game today, and a must-have checklist per structure. Cartoon style may simplify surfaces only;
silhouettes, proportions, arrangement and colours must match the place.

Tags: **[O]** OSM (valley extract of 2025-10; the live OSM API returned the identical data for the site on 2026-10-11),
**[A]** measured on Esri World Imagery (z19, 0.26 m/px), **[P]** measured on photos (telephoto shots, scaled by the 7.0 m
harmika), **[S]** published source, **[E]** estimate, **[V]** to verify on a current photo or on site.

Reference material (outside the repo, never copied in): photos `/home/user/wt/refs/mon-boudha/<topic>/` (Openverse +
Wikimedia Commons, credits in each `credits.txt`, list in §9), aerial crops and the OSM plan
`/home/user/wt/previews/mon-boudha/site_plan_osm.png`, current-game renders `/home/user/wt/previews/mon-boudha/current/`,
photo-vs-game pairs `/home/user/wt/previews/mon-boudha/pairs/pair_0..5.jpg`.

---

## 0. Verdict: what is wrong today (in order of how much it hurts recognition)

| # | Problem in the game now (renders in `current/`, pairs in `pairs/`) | Reality |
|---|---|---|
| 1 | The top is squat: harmika 4 m + spire 8 m + parasol 1.5 + finial 1.5, total 36.5 m (pair_4). | From the harmika base to the finial tip is **~21 m** (0.62 × the 33.5 m dome) **[P]**; the gilt spire alone is 1.3 × the harmika width. The stupa's silhouette is a low white dome under a tall, slender gold tower. Total **≈ 44 m** above the kora (§3.1). |
| 2 | No compound (kora) wall; the 147 prayer-wheel niches are drawn into the lowest terrace wall. | A free-standing **white wall** ~2.6 m high runs round the whole stupa at r ≈ 45-55 m with the 147 wheel niches, chorten finials on top, lamp niches and flagpoles. The terraces stand **inside** it (§3.2). |
| 3 | No north entrance, no Ajima shrine, no Guru Lhakhang; the only gate is a white gatehouse with a red band and a dharma wheel standing on the kora 46 m south of the centre, straddling the terrace stair. | The upper terraces are entered from the **north gate**, beside the **Ajima (Hariti) shrine** with its gilt roof, facing the **Guru Lhakhang (Tamang Gompa)** and its Big Bell across the kora (§3.3-3.5). The **main gate** is a Tibetan painted arch with a yellow roof, dragons and snow lions **on Boudha Main Road, 72-85 m S** of the centre (§3.6). |
| 4 | The ground is a 5-8 m terrain bump under the stupa and a sloping, sandy kora (the Copernicus DSM picks up the stupa and the ring houses); the kora is a brown 8 m road ribbon floating over parts of it. | One **flat, level, fully paved plaza**: grey stone flags along the wall, red brick beyond, to the house fronts (§2.3). |
| 5 | Terraces: 20-corner squares with shallow projections; white floors; tiny white corner chortens only. | Each terrace is a **cross on a square** (arms 55-62 % of the body width, protruding 4-9 m), salmon-brick floors, big white bell chortens and **mini-Boudha chortens with eyes and gilt spires** on the lowest terrace, many potted plants, statues at the stairs (§3.1.2). |
| 6 | Dome: yellow petals radiating like a star from the harmika; dark flag lines. | White dome with **12-14 big saffron scallop arcs** hanging from a yellow cap, a ring of **108 niches** at its foot; hundreds of dense multicoloured flag lines in bundles (§3.1.3-3.1.7). |
| 7 | The ring of houses is generic grey-roofed boxes, mostly 2-4 storeys. | A continuous, colourful **ring of 4-6-storey houses** (ochre, red, salmon, cream, exposed brick with carved windows), shops with awnings at the kora, rooftop cafés with umbrellas, gompa facades (§3.7). |

---

## 1. Sources and method

* **OSM [O]:** every way and node within 400 m from `/tmp/.../scratchpad/dp/valley.osm.pbf` (pyosmium), checked against
  `api.openstreetmap.org/api/0.6/map` for bbox 85.3605,27.7202 - 85.3635,27.7228 on 2026-10-11 (no changes). Overpass
  mirrors were unreachable. Key objects: stupa outline **w56688295**, compound wall **w56688296**, building parts
  w235084185 (T2), w421288398 (T3), w421288394 (base ring), w421288397 (dome), w421288399 (harmika), w244677433
  (spire), w421288395 (canopy), w421288396 (finial); kora ways w201561511, w357375505, w1196490820.
* **Aerial [A]:** Esri World Imagery z19 mosaic (reference only), OSM overlaid: the OSM outlines sit on the imagery to
  ~1-2 m; the dome/spire parts are mapped ~3 m west of the terrace centre (parallax of the 45 m spire in off-nadir
  imagery) - centre everything on the terraces.
* **Photos [P]:** ~45 Openverse queries and Wikimedia Commons categories (Boudhanath, Boudhanath gate, Ajima Temple,
  Guru Lhakhang, Jamchen Lhakhang, Buildings surrounding Boudhanath stupa). Every contact sheet was looked at; the
  ones used are listed per structure and in §9. Commons rate-limited the sandbox (HTTP 429) after a few files.
* **Web [S]:** Wikipedia "Boudha Stupa" (ed. 2026-09-10); Kathmandu Post 2016-10-31 and 2016-11-23 (reconstruction);
  Studio Matrx guide (2026); sacredsites.com and Lonely Planet (108 images, 147 niches, north entrance and Ajima);
  mountainsoftravelphotos.com captions (gate, elephants, juniper kiln, gompa positions); thelongestwayhome.com
  (Ajima, Tamang Gompa). Links in §9.
* **Game [G]:** temporary dump test (not committed) of the hero at LOD0-3, the B0 buildings, roads, areas and terrain of
  tile 10/521/163 within 140 m, rendered with `tools/mesh-preview/render.py` from the photo viewpoints (§6).

---

## 2. Site frame and plan

### 2.1 Anchor and axes

| Item | Value |
|---|---|
| Centre (terrace centre) | **27.721445 N, 85.362004 E** [O]; game tile 10/521/163, Unity (761.8, 176.4) (hero anchor = OSM centroid, within 1 m) |
| Plan axes | rotated **9.0° clockwise** from true north [O, A]: the four faces and stairs point to bearings **9° (N), 99° (E), 189° (S), 279° (W)**. All three terrace parts measure -9.0 to -9.1°; the Guru Lhakhang (N) lies on bearing 9.7°, the main gate on 194.8°. The game's `YawDeg = 180` is the south face, which is **189°**. |
| Main entrance from the city | south, from Boudha Main Road (the Boudha-Jorpati road, E-W, ~90-100 m S of the centre) through the main gate (§3.6) and a 6-8 m paved lane (w4847306) to the kora |
| Entrance to the terraces | **north** gate in the compound wall next to the Ajima shrine [S: Lonely Planet, Wikipedia] |
| Ground | flat; kora ≈ 1331 m a.s.l. [O DEM]. Use one level for the whole plaza and the south lane (§2.3). |

Local coordinates below: **u** = across (toward bearing 99°), **v** = along the north axis (toward 9°), metres from the
centre, heights in metres above the kora paving.

### 2.2 Zones from the centre outward (radii measured from the centre)

| Zone | Radius | What | Source |
|---|---|---|---|
| Stupa | 0-40.8 | three cross-on-square terraces (81.5 × 79.4 m overall), base ring Ø 38, dome Ø 33.5 | [O] |
| Compound ground | 40-49 | narrow paved strip between T1 and the wall, with the north stair, lamp houses, the NE compound house | [A, P] |
| **Compound wall** | **45.1-55.4** (18-sided polygon w56688296, perimeter 312 m) | white wall, 147 wheel niches | [O, S] |
| Kora (inner path) | wall face + 3.5 | grey stone flags, the walking ring | [P] |
| Kora (outer plaza) | to the house fronts | red-brick paving, benches, pigeons, lamp tables | [P] |
| House fronts | N 59-62, NE 48-60 (low houses squeeze in), E 56-58, SE 55-56, S 55-57, SW 57-64, W 64-75 (Jamchen recess 81), NW 61-64 | the continuous ring | [O] |
| South lane | bearing ~195°, r 53 → 90 | 6-8 m paved lane to the main gate and Boudha Main Road | [O] |

OSM kora ways: w201561511 (width 8, S/W arc, r 52.8-59), w357375505 (width 8, NW/N arc, r 56.3-58.7), w1196490820
(width 4, E arc, r 46.7-59: it squeezes past the NE houses w912264291/2). Treat the whole annulus wall → house fronts as
one plaza (6-25 m wide), not as an 8 m road.

Compound wall vertices (E, N) from the centre [O, w56688296]: (-47.3,6.1) (-40.8,24.8) (-28.6,37.8) (-9.8,47.4)
(0.7,49.0) (10.5,47.2) (22.3,42.9) (37.9,27.6) (42.0,16.3) (48.4,6.7) (55.3,0.8) (53.6,-12.6) (46.4,-19.1)
(27.5,-41.6) (7.2,-49.4) (-24.0,-45.6) (-40.3,-29.7) (-47.5,-12.8). The aerial shows the same faceted ring; the east
bulge (r 55) is real.

### 2.3 Ground and paving (must fix)

* The game terrain under the site is the Copernicus DSM: **+5 to +8 m under the stupa** (1335.5-1339.1 at r < 10 m),
  1330.9-1333.9 m on the kora ring, 1330.4-1335.7 m at the house fronts [G]. The hero stands at 1330.56. Flatten
  r ≤ 75 m (and the south lane to the road) to **one level** (≈ 1330.9 m, the kora minimum), terrain under the stupa
  hidden or cut.
* **Inner path**: smooth grey stone flags `#8C8A86` (wet/dark `#5A5C60`), 3.5-4 m wide along the wall, laid in
  courses parallel to the wall, with drain grates; a 0.25 m stone kerb/gutter line `#6E6A64` separates it from the outer
  zone [P: kora/08, circle/04-09, commons_late/c03].
* **Outer plaza**: red-brown brick `#9C5A40` (sun `#B66B4C`) in herringbone, worn smooth [P: c03]; benches (wood/iron, backrests)
  face the stupa along the brick zone [O: 3 benches W at r 55; P: kora/08].
* South lane: grey flag paving `#7F7E7A`, the same as the inner path [P: y2019/00-02].

### 2.4 Plan sketch (north up = bearing 9°, not to scale)

```
                     Ghyoilisang pond + Guru Rinpoche statue (NW, r 116)
          ring houses N (r 59-62)  [Guru Lhakhang + Big Bell, r 62-66, on the N axis]
                              =========  kora (stone flags + brick)  =========
                 ___________________ compound wall r 45-55, 147 wheel niches ___________
                | stair up <-[N gate W][AJIMA][N gate E]   NE compound house       |
   Jamchen      |              _____     T1 (81.5 x 79.4, top 4.0)     _____         |   E houses
   Lhakhang  W  |    chorten  |  T2 (62.5 x 61.8, top 8.0)            |  chorten     |  E  (r 56-58)
   (r 81-88)    |             |    T3 (49.8 x 50.1, top 10.5)         |              |
                |             |      ring O 38 / dome O 33.5          |              |
                |             |        harmika 7.0 + spire            |              |
                |  lamp house |_____                            _____ |              |
                |     S gate pavilion + juniper kiln  [V side]                       |
                |______________________________________________________________________|
                              =========  kora  =========  ring houses S (r 55-57)
                                    | south lane (6-8 m), map board r 53 |
                                    | ticket control r 78                 |
                                [ MAIN GATE ]  r 72-85, bearing 195
           ==================== Boudha Main Road (E-W, r 90-100) ====================
```

---

## 3. Structure inventory

### 3.1 S1 - The Great Stupa (Boudha Stupa, Jarung Khashor) - hero

* **OSM**: w56688295 (`building=temple`, `heritage=1`, `ref:whc=121-005`, height 4 = T1) + 8 `building:part`s [O].
  Curated hero `her.ktm.boudhanath` (HeroCatalog, `StupaStyle.Boudha`).
* **Built** 14th c. (present form) on a 5th-c. site [S Wikipedia]; spire cracked in the 25 Apr 2015 earthquake, taken
  down to the dome, rebuilt with traditional brick, lime and surkhi mortar and **30+ kg of gold** for the spire,
  reconsecrated and reopened **22 Nov 2016** [S Kathmandu Post, Wikipedia, AP]. Model the **post-2016** state.

#### 3.1.1 Section (heights above the kora) - recommended

| Part | From → to (m) | Plan size | Basis |
|---|---|---|---|
| Terrace 1 (T1) | 0 → **4.0** | 81.5 × 79.4 cross-on-square | [O] heights, [P] wall ≈ 1.5 × a person behind the 2.6 m wall |
| Terrace 2 (T2) | 4.0 → **8.0** | 62.5 × 61.8 | [O] |
| Terrace 3 (T3) | 8.0 → **10.5** | 49.8 × 50.1 | [O] |
| Base ring (drum), two steps | 10.5 → 12.0 | Ø 38.0 → Ø 35.5 | [O] w421288394 Ø 38.0 |
| Dome (anda) | 12.0 → **23.7** apex | **Ø 33.5**, rise 11.7 (0.35 × Ø, a flattened hemisphere) | [O] w421288397; [P] tele photos give rise/Ø 0.36 |
| Harmika (eye block), visible body | ~23.0 → 26.1 (3.1 m above the dome shoulder) | **7.0 × 7.0** | [O] w421288399 7.0 m; [P] 0.44 × width |
| Gilt cornice + cloth valance | 26.1 → 27.6 | 7.7 m (cornice projects 0.35) | [P] |
| Spire, 13 square steps | 27.6 → **36.7** (9.1 m) | 6.6 at the base → 2.4 at the top | [P] height = 1.30 × harmika width (two independent telephotos agree, 1.29 and 1.30); [O] spire part 8.0 m base |
| Neck | 36.7 → 37.1 | Ø 1.8 | [P] |
| Canopy (chhatra) drum + cloth skirt | 37.1 → 40.6 | Ø 4.4 (cloth Ø 4.8) | [P]; [O] canopy part Ø 5.5 |
| Finial (gajur) in its gilt frame | 40.6 → **44.8** | frame 1.6 wide at its foot | [P] |

**Total ≈ 44-45 m** above the kora. Published figure: "36 m" [S Department of Archaeology via Wikipedia; Studio Matrx says
"some sources say more"]; OSM parts give 51 m. The photo measurement (two telephotos: `panorama/ref_02` Cheryl Marland CC
BY 2.0 and `general/ref_08` terbeck CC BY-NC-SA 2.0, scaled by the OSM harmika) gives **harmika base → finial tip
= 20.4-21.4 m**, i.e. **0.62 × the dome diameter**; with the OSM dome apex at 23.7 m the total is 44-45 m. The 36 m
figure most likely counts from the terrace top [V]. **Hold the ratios even if the absolute height changes**:

| Ratio (silhouette rules) | Value |
|---|---|
| harmika width / dome Ø | 0.21 |
| spire height / harmika width | 1.30 |
| spire top width / base width | 0.36 |
| (canopy + finial) / harmika width | 1.1 |
| harmika base → tip / dome Ø | 0.62 |
| dome rise / dome Ø | 0.35 |
| T3 top / dome Ø | 0.31 |

#### 3.1.2 Terraces (the mandala plinth)

* **Plan**: each terrace is a **square body with four arms** (a cross on a square = 20 corners), axes at 9°/99° [O]:

| Terrace | Body (u × v) | Arms: width, how far they stick out | Overall |
|---|---|---|---|
| T1 (w56688295) | 66 × 66 (u ±33, v -35 → +31) | 41 m wide; N +8.8, S +4.7, E +7.5, W +7.8 | 81.5 × 79.4 |
| T2 (w235084185) | 52 × 52 | 30.7-31 m wide; +4.7 to +5.2 each | 62.5 × 61.8 |
| T3 (w421288398) | 41.5 × 41.6 | 22-23 m wide; +3.4 to +4.0 each; on the N arm two 0.8 m stair cheeks protrude 1.5 m (stair 3.9 m wide) | 49.8 × 50.1 |

  (Rule: arm width 0.62 / 0.60 / 0.55 of the body; arms protrude 0.07-0.13 × the body.) The current game's
  `TwentyCornerLocal` projections are far too shallow (render `hero_lod0_views.png`, top view).
* **Walls**: vertical, white lime plaster `#F2EEE6` with soft grey weathering at the foot; a low base course and a
  rounded projecting coping (0.25 m) at each top edge; **row of small white chorten finials** (0.6-0.8 m, stepped base,
  bulb, tiny spike) on the outer edge of T1 every ~2.5 m [P: corner/02, pair_3] and on T2/T3 edges near the stairs.
* **Floors**: salmon-brick / terracotta paving **`#C39A86`** (shade `#A6857A`) - not white [P: around/03, corner/06,
  reconstruction/08-09].
* **Stairs**: the main stair is on the **north axis**: from the north gate a straight flight with **gilt-yellow handrails**
  up to T1, then flights up T2, T3 and the base ring, each ~4 m wide with white cheek walls stepping down [P: around/03
  zoom, pair_0]. Each flight is lined with **clay flower pots** (terracotta `#B5603E`, green plants) [P: earthquake/06,
  around/03]. Other faces have shorter flights between terraces [V].
* **Statues**: at the north stair head of T2 a pair of white figures; on the stairs **white elephants with riders and
  colourful dotted harness** (red/blue/yellow) [P: tamang/08; mountainsoftravelphotos: "a pair of elephants and a kiln
  used to burn juniper welcomes you at the entrance"; the left rider has a sword and shield]. Dhyani Buddha statues
  (Amitabha, Ratnasambhava) at the top step of the entrance stair [S mountainsoftravelphotos 07-1, 14].
* **Stone shrine**: a grey carved stone shrine with makara-scroll pediment, a central small chaitya finial and an
  arched niche stands at the foot of the dome stair (south face in the photos) [P: wheels/04-05, earthquake/07] [V face].
* **Corner chortens on T1** (unmapped; positions from photos [P: pair_0, corner/04-06, spire/06, smallstupa/02-04]):
  * **Bell chortens**: white, ~3.5 m square stepped base (3 steps), bell-shaped dome, small square harmika, short white
    or gilt finial; ~5-6 m tall. Two flank the north arm on T1; similar pairs at the other arms [V count].
  * **Mini-Boudha chortens** (the signature detail): white square base and dome ~3.5 m Ø, a **small gilt niche shrine
    with a torana on the dome front**, a **white harmika with painted eyes** (blue irises, red "१" nose), a **gilt
    13-step square spire** and gilt finial; ~6.5-7 m tall [P: spire/06 Bibek KC CC0, recent]. At least 2 on T1 corners
    (SE and SW in photos), probably 4 [V].
* **Lamp houses on the terraces / compound**: open sheds with **ochre-yellow sloping roofs** `#D9B13B` holding tables
  of brass butter lamps [P: pair_1 bottom right, around/03 left].

#### 3.1.3 Base ring and the 108 niches

* Two white steps from Ø 38.0 at 10.5 m to Ø 35.5 at 12.0 m [O, P]. On the upper step, right at the foot of the
  dome, a **band of 108 small niches** with images of Amitabha [S sacredsites, Studio Matrx]: niches ~0.45 × 0.55 m,
  pitch ≈ 1.03 m (π × 35.5 / 108), dark recess `#3A2E28` with small gilt/painted figures; **red festoon garlands** hang
  in shallow swags along the ring [P: c04 Commons "Boudhanath Stupa from monastery"].

#### 3.1.4 Dome

* Smooth lime-white `#F4F0E6` (warm in sun `#F3E6C8`), slightly flattened (rise 0.35 Ø).
* **Saffron "lotus" painting**: **12-14 large scallop arcs** (each ~8-9 m across) hanging from a pale-yellow wash cap
  around the harmika; the arc lines are saffron-ochre **`#E3A73A`**, ~0.4-0.6 m wide, the area above the arcs washed
  pale yellow **`#F2D27A`**; the arcs' lowest points reach about half-way down the dome [P: spire/06, c04, pair_0,
  general3/09]. Thin vertical drips under some arcs. Pre-2015 photos show a grey-green weathered dome: do not use them for
  colour.
* Hundreds of pigeons sit on the dome and fly up in flocks (life layer).

#### 3.1.5 Harmika and the eyes

* **Block**: 7.0 m square, gilt copper plates in a **grid of raised square panels** (~0.45 m, aged gold `#C9A050`,
  lit `#E0BC63`) [P: tamang/06 CCRcreations, general3/00 Prof. Mortel].
* **Eyes** on all four faces, painted on the gilt: each eye ~1.7 × 0.6 m, centres ±1.35 m from the face centre at
  ~0.55 of the visible height; **white almond** `#FFFFFF`; **iris** two rings, dark blue `#1F4E9A` outer and sky blue
  `#73B9E6` inner; black pupil `#111111`; black upper lid line; **red-orange lower contour** `#D8402A`; thick **dark blue-
  black brows** `#1B2140` arching over each eye and meeting above the nose [P: tamang/06, eyes/00].
* **Nose**: the numeral **"१"** drawn as a white teardrop with a **red spiral** inside (0.55 × 0.9 m) below and between
  the eyes; **urna** (third eye) a small blue-outlined dot between the brows [P, S Studio Matrx].
* **Cornice and valance**: gilt cornice (0.35 m projection, repoussé border); hanging from it a cloth **valance**: thin
  stripes yellow `#F2C230` / red `#C8282E` / blue `#2E5DA8` / white, then a deep **red drape `#C8202A` with a pleated
  frill** ~0.9 m (some years green `#2F6A3A` - use red, the 2016-2024 photos) [P: eyes/00, general3/08, pair_0].

#### 3.1.6 Spire, canopy and finial

* **Spire**: 13 square gilt steps (stepped pyramid, not round rings), each riser ~0.65 m tall and set back ~0.16 m
  per side, faced with two rows of raised square gilt plates; colour bright gold `#E2B54A`, shade `#A87C2E`
  [P: general3/00, spire/05].
* **Canopy (chhatra)**: short gilt neck, then a gilt cylindrical drum Ø 4.4, ~1.6 m tall, repoussé top and scalloped
  rim; wrapped by a **striped cloth band** (white / blue `#2E5DA8` / red `#C8282E` narrow stripes) over a wide
  **saffron-yellow skirt `#F0A01E`** ~1.2 m [P: general3/00, eyes/03, tele2].
* **Finial**: gilt bell cap, stacked discs, a pinnacle with a small umbrella and spike, held in a **gilt tripod / four-leg
  lantern frame** whose legs meet at the top [P: spire/05, c04, general3/00].
* **Prayer flags**: lines start under the canopy (~37 m) and run in **~16 bundles of 4-8 lines** to the T1 corners and
  arm ends, the compound wall and the flagpoles; **hundreds** of lines; flags ~0.25 × 0.30 m, packed, in the order
  **blue `#1F5BA8`, white `#F5F5F0`, red `#C8282E`, green `#2E8B3F`, yellow `#F4C21A`**; sag 6-10 % [P: flags/*,
  pair_0, pair_4]. At distance they read as bright multicoloured fans, never as dark cords.

#### 3.1.7 Views

* **N** (Guru Lhakhang roof, pair_0): Ajima's gilt roof in front, the north gate and stair with yellow rails, the NE
  compound house (white, maroon band), two big white chortens on T1, statues at T2, the eyes facing you over the stair.
* **E** (kora, morning sun): faceted white wall with niches, low NE houses close to the wall, mini-Boudha chorten on the
  T1 corner, flag fans.
* **S** (from the lane): through the main gate arch the white stone gate shrine of the compound; on the kora the spire
  rises over the wall; benches and pigeons.
* **W** (kora by Jamchen): the plaza is widest here (to r 75); T1 west arm, lamp houses with yellow roofs inside the wall.
* **From 300 m+**: only the white dome and the tall gold tower with its red band and yellow-skirted crown show over the
  roofs of the ring (pair_4).

#### 3.1.8 In the game now [G]

Hero `StupaGenerator` (Boudha style), LOD0 34,670 / LOD1 6,914 / LOD2 545 / LOD3 58 triangles, 36.5 m tall; heights
scaled to 36 m (harmika 4, spire 8, parasol 1.5, finial 1.5); 20-corner terraces with shallow projections and white
floors; prayer-wheel niches embedded in the T1 wall; lotus "petals" radiate as a star; 64 flag lines that render dark;
small white corner chortens; one stair on the yaw-180 face (bearing 180, real 189) to a gatehouse on the kora at 46 m.
No eyes on corner chortens, no 108-niche band detail beyond a dark ring, no statues, no stone shrine, no lamp houses.

#### 3.1.9 Must-have checklist

- [ ] Section and ratios of §3.1.1 (tall slender gold top: harmika base → tip ≈ 0.62 × dome Ø)
- [ ] Terraces as cross-on-square with the exact body/arm sizes; axes at 9°
- [ ] Salmon-brick terrace floors, white walls with copings, chorten finials on the T1 edge
- [ ] North stair (main) with gilt rails and flower pots; flights on the other arms [V]
- [ ] Bell chortens and mini-Boudha chortens (eyes + gilt spire) on T1
- [ ] Elephant statues with riders, Dhyani Buddha statues, the grey stone shrine
- [ ] Base ring with 108 niches and red festoons
- [ ] White dome with 12-14 saffron scallop arcs under a yellow cap
- [ ] Gilt panelled harmika; eyes, brows, "१" nose, urna on all four faces; red drape valance with stripes
- [ ] 13 square gilt steps, striped + saffron-skirted canopy, gilt lantern finial
- [ ] Hundreds of bright flag lines in bundles to T1, the wall and poles
- [ ] Pigeons on the dome (life layer)

### 3.2 S2 - Compound wall with the prayer-wheel niches (kora wall)

* **OSM** w56688296 (`barrier=wall`, `amenity=place_of_worship`, height 2, width 0.6) [O]; 18-sided ring, r 45.1-55.4,
  perimeter 312 m.
* **Wall**: white lime plaster `#EEEAE0`, ~**2.6 m** to the coping (person-scaled [P: corner/02, spire/00]), 0.6-0.8 m
  thick, rounded coping; **small white chorten finials on the top every ~2 m**; tall **flagpoles** (darchor, 8-10 m,
  wooden, vertical cloth banners in the five colours) at intervals and at the gates [P: pair_0, pair_1].
* **Prayer-wheel niches**: **147 niches with 4-5 wheels each** [S sacredsites, Lonely Planet] → pitch ≈ 2.1 m. Each niche a
  rectangular recess ~1.6 m wide × 0.55 m high, sill ~0.9 m; inside, copper/brass wheels (Ø 0.20-0.25, 0.35-0.45 m tall)
  behind a dark carved grille band; over each niche a **cloth valance** - currently **yellow `#E8B530` with thin white
  and red stripes** [P: commons_late/c03, recent], earlier orange-yellow `#E89A2E` with a red top band or red with
  yellow/green/blue stripes [P: spire/00-01, gate/02-03, circle/02-09, gilding/07]. A few trees and shrubs grow inside
  the compound between the wall and T1 [P: c03, pair_0].
* **Between niches**: small arched **lamp niches** at the foot (soot-blackened `#2B2620`), small brass wheels/lamp
  holders on the piers [P: corner/02].
* **Buttress chortens**: white chorten-shaped piers/kilns (~1.2 m base, 1.8-2.2 m tall, a dark arched opening) project
  from the wall at intervals and in front of entrances [P: circle/02, circle/08, corner/02].
* **Openings**: the **two north gate pavilions** (§3.4) and a **south gate pavilion** (white, timber door, juniper kiln in front)
  [P: corner/02] [V exact side]; walkers pass through only at the gates.
* **In the game now**: missing; its niches are drawn on the T1 wall instead.
* **Must-have**: [ ] free-standing faceted white wall on the OSM line, 2.6 m [ ] 147 niches with wheels and coloured
  valances [ ] chorten finials on top, buttress chortens, lamp niches [ ] flagpoles [ ] openings only at the N gates and the S pavilion.

### 3.3 S3 - Ajima (Hariti) shrine - north entrance

* **OSM**: not mapped. Position: on the **north axis** at the wall line, r ≈ 48-52, between the two north gate
  pavilions (the stair gate is the west one), projecting ~3 m into the kora [P: around/03 zoom, commons_late/c05,
  y2019/06, y2019/08].
* **What**: shrine of the Dharma protectress Mammo Pukkasi, known as **Hariti / Ajima** (Newar guardian of children),
  "built at the main northern entrance"; the Mahaguru Gompa (Guru Lhakhang) faces it and keeps its offerings
  [S Wikipedia]; "a small shrine ... marks the entryway to the upper levels" [S Lonely Planet]; renovated 2016 facade,
  image behind glass, shoes off, butter lamps at night [S thelongestwayhome].
* **Form** [P: commons_late/c05 "Ajima Temple.jpg", c04 2013, around/03 zoom, y2019/08]: square single-storey shrine
  ~5 × 5 m on a **red-brick plinth ~0.9 m** with 2 brick steps (shoes left there); body ~3 m with **dark carved wooden
  lattice screens** (black-brown `#2A2018`) and a door; **one broad, shallow gilt-copper hipped roof** (standing-seam
  ribs, gilt `#C9A24A`, eaves ~1 m, hundreds of pigeons on it) with a **red valance band and a yellow ruffled frill**
  `#E8B530` (orange `#F09A2A` in some years) under the eave and marigold strings; a gilt pendant (torana drop) at the
  eave centre; **finial = gilt double vajra (vishvavajra) on a stepped gilt pedestal**, ~1.5 m [P: tamang/07 is that
  finial]; **two tall brass prayer wheels in lantern-like brass housings** flank the front; incense smoke; a snow-lion
  figure at the gate side.
* **Inside** (do not show; sanctum closed): grey stone-tiled room, small gilt pagoda canopy over a silver torana, the
  gilt goddess in red cloth and flower garlands [P: commons_ajima c01, c02].
* **In the game now**: missing.
* **Must-have**: [ ] shallow gilt hipped roof, red band + yellow frill valance, gilt double-vajra finial [ ] dark
  lattice front on a brick plinth with steps [ ] on the N axis between the two white gate pavilions [ ] the two brass
  wheel lanterns, lamps, incense smoke.

### 3.4 S4 - North gate and the stair to the terraces

* **Two** white plastered gate pavilions in the wall line, one on **each side** of the Ajima shrine [P: commons_late/c05]:
  each ~4 m wide × 3.5 m, arched opening ~1.6 × 2.4 m, a **gilt torana panel** with a yellow-striped valance over the
  door, pilasters, small chorten finials, a notice board ("Important Notice") on the west one; through the **west** one a
  straight stair with **gilt-yellow handrails** climbs to T1 [P: y2019/06, around/03 zoom; gate2/05 older].
* **NE compound house** (inside the wall, east of the gate): white 2-storey block with a **maroon frieze `#7A1F1F`
  with white dentils**, small windows, a little red pagoda-roofed shrine on its roof [P: around/03 zoom] [V use].
* **In the game now**: missing (the game's only gate is on the south).
* **Must-have**: [ ] two white gate pavilions flanking the Ajima shrine [ ] stair with yellow rails up to T1 through
  the west one [ ] NE compound house.

### 3.5 S5 - Guru Lhakhang (Mahaguru Gompa, "Tamang Gompa") and the Big Bell

* **OSM**: node n5566335222 "गुरु लख्खाङ मठ / Guru Lhakhang Monastery" (27.722023, 85.362115), building w520755992
  (`amenity=monastery`, 3 levels, 13.7 × 8.1 m), Big Bell n3877763908 (27.722015, 85.362130) [O]. Facing the north
  entrance across the kora, r 62-66, bearing 4.5-11° (on the N axis) [O]. Neighbour W: "श्री बौद्ध ध्यान गुठी"
  n5075026795.
* **Form** [P: bell/09 Cheryl Marland CC BY 2.0, gompa2/00-01, commons_more c01, c04]: Tibetan monastery front -
  ground-floor **portico** of painted columns (red shafts, blue/green carved capitals) with painted beams and cloud-
  scroll brackets, murals of the **Four Guardian Kings** at the entrance ("entrance door flanked by the Four Guardian
  Kings is on the top floor" [S mountainsoftravelphotos]); upper walls **ochre-yellow `#E3B84E`**; Tibetan windows
  (red frames, black trapezoid surrounds, small frilled awnings); a balcony; **gilt roof with dragon/makara-head corner
  finials and cloud-scroll cresting**; a rooftop **gilt Dharmachakra flanked by two deer**. A large prayer wheel inside.
* **Big Bell**: silver-bronze bell (~1.0 m Ø, 1.2 m tall) hanging in a **gilt horseshoe-arch frame** (~2.5 m wide ×
  3.5 m tall) on the kora in front of the porch; people sit on the porch plinth [P: bell/00-01, 04, 08, 09].
* **Next door (E)**: 3-storey ochre building with **green shutters**, white Rana-style stucco window pediments and a
  brown tiled roof; shopfronts below [P: bell/09].
* **In the game now**: house grammar, archetype Gompa, gabled 3 storeys 10.5 m, GompaWash palette [G]; no porch, no
  gilt roof, no bell.
* **Must-have**: [ ] yellow-ochre gompa front with painted portico [ ] gilt roof, dragon corners, dharma wheel + deer
  [ ] Big Bell in its gilt arch on the kora [ ] green-shuttered tiled-roof neighbour.

### 3.6 S6 - Main gate (Boudha Gate) on Boudha Main Road

* **OSM**: n5302729521 (`man_made=ceremonial_gate`, `barrier=entrance`, `name:en=Baudha Gate`, material stone,
  no vehicles) at 27.720814, 85.361817 (r 72, bearing 195) [O]; ticket control n13352538589 (r 78), ATMs n6024984386,
  n3626551804, n4537066692 (r 81-83), bus stop "Boudha" n560500317 (r 93). The 2019-2022 photos show the gate at the
  **lane mouth on the road frontage** with an ATM booth right against its east pier, so it stands ~10 m nearer the road
  than the OSM node (r ≈ 82-85) [P: commons gate photos] [V].
* **Form** [P: Commons "Boudhanath entrance gate.jpg", "...stupa entrance gate.jpg", "...and its premises.jpg"]:
  Tibetan ceremonial arch, ~7.5 m wide incl. the lions, ~6.5 m to the eave, ~8.5 m to the crest.
  * Two piers ~1.4 m wide, faces painted with the **eight auspicious symbols** (Ashtamangala) in colour on dark teal
    `#2E4A4E` panels framed in yellow-gilt `#D9B53A`.
  * A **cusped ogee arch** ~3.3 m wide × 4.3 m high with a gilt edge; spandrels densely painted with **dragons and
    clouds** in blue `#2B5FA0`, teal and gold.
  * A broad **yellow Tibetan roof/eave** `#E3CC3A` (gilt-yellow, slight upturned corners, moulded edge) over a painted
    frieze; a dark sign panel in the middle.
  * Crest: a central **white bulb chorten with a gilt lotus finial**, flanked by **two gilt dragons**; small gilt
    kalash finials at the roof ends.
  * Outside the piers: two **white snow lions with turquoise manes** on round lotus pedestals (blue lotus) atop white
    round columns ~1.3 m.
  * Steel bollards/railings and a ticket barrier just inside; from the road one looks through the arch to the white
    carved stone gate shrine of the compound.
  * Older form (1990s): white piers with symbol panels, cusped arch, flat cornice with a small chaitya and makaras
    [P: gate2/04] - superseded.
* **In the game now**: not built at the road; instead a white gatehouse (red band, rainbow eave, dharma wheel) stands
  on the kora at 46 m over the terrace stair [G, pair_2].
* **Must-have**: [ ] at the lane mouth on Boudha Main Road, bearing ~195°, facing S [ ] painted piers with Ashtamangala,
  cusped arch with dragons [ ] yellow roof, white chorten + two gilt dragons on top [ ] snow lions on pillars
  [ ] the lane north of it paved and lined with shops.

### 3.7 S7 - Ring of houses round the kora

* **OSM** (front row, bearing order from N): w520755992 (Guru Lhakhang), w520756005 (residential, 3 lv), w517234573
  (commercial, 3 lv), w911869006/7/8, w209312611 (Old Dhabzang), w912264293/4, w912264291/2 (low houses NE, r 48),
  w912264453/591/590/589 (E, 17 m long fronts), w912264741, w912269948/41/42, w211905448 (Cafe Terrace D'Stupa),
  w912269936, w912270352, w912270787, w912276969 (S, 23.9 × 14.2), w912276521 (S, 14.8 × 14.5), w912276520,
  w911869018/19 (SW, 24-26 m fronts), w517160028, w912278963/4/5, w912280177/176, w911869025/26/27/28 (W, 23-32 m fronts),
  w209530023 (Jamchen, recessed), w911869029, w912280174, w912280388, w209312596/517236636/517236635 (commercial, 3 lv),
  w912261800/799, w912262363, w912262534 [O]. Most have no height tag; the pipeline infers 3-6 levels.
* **Look** [P: gate2/00, bell/09, corner/04-05, reconstruction/07, circle/*, around/01]:
  * **4-6 storeys** (13-20 m) in a continuous wall of fronts following the circle; corners of the lanes break it at
    S (lane), W (passage n6021202191), NW, N (Pawo Gompa Marg), E.
  * Three families: (a) painted RCC houses - ochre-yellow `#E8C35A`, brick-red `#B5442E`, salmon `#E39A7A`, cream
    `#F1E6CC`, pale green `#BFD8B0` - with white Rana-style window pediments, balconies with railings and
    **pitched clay-tile or tin roofs** on some (gate2/00 shows two red roofs with dormers); (b) **exposed red brick**
    `#A64B32` Newar-revival fronts with dark carved wooden windows and tiled cornices (corner/04 left, Commons gate
    photos); (c) gompa fronts (§3.5, 3.8).
  * Ground floor: shops on the kora - thangka and singing-bowl shops, cafés, restaurants, money changers - with rolling
    shutters, **striped awnings** (yellow/white, green/white), signboards, hanging goods.
  * Roofs: **rooftop cafés** with railings and colourful umbrellas (OSM: Cafe Terrace D'Stupa, Stupa View Restaurant,
    Himalayan Java...), black/blue water tanks, solar heaters, prayer flags on poles.
* **In the game now**: house grammar (ModernUrban, profile BoudhaKora, GompaAccentShare 0.3), plan storeys often 2-3
  where the pipeline inferred 4-6 (e.g. lv 6 → 2 storeys), grey roofs with random roof props, no shopfront awnings
  at the kora [G, site_bird.png, roof_SW.png].
* **Must-have**: [ ] continuous 4-6-storey ring on the OSM fronts [ ] the three facade families and colours
  [ ] shops + awnings at the kora on every front [ ] rooftop cafés with umbrellas facing the stupa.

### 3.8 S8 - Jamchen Lhakhang (Sakya Maitreya Lhakhang), west

* **OSM** w209530023 (`amenity=place_of_worship`, `denomination=sakya`, "Sakya Maitreya Lhakang", 12.4 × 12.1 m) at
  r 81-88, bearing 273 (on the W axis), **set back** in a courtyard behind the ring front (front r 69-75) [O]; aerial shows
  a **gilt (yellow) square roof** [A].
* "Jamchen Gompa (Sakya) is to the left (west) of the main entrance"; entrance door with the Four Guardian Kings; a
  large Maitreya (Jampa) statue inside [S mountainsoftravelphotos 10-10-4]; Commons category "Jamchen Lhakhang,
  Boudhanath" (43 files) for more angles.
* **Form** [P: commons_late/c01 "Jamchen Lhakhang, Boudhanath 01.jpg"]: two-storey Tibetan gompa front in its own
  stone-paved court: an open **portico of four square maroon columns** `#9E2430` with richly painted capitals and
  brackets (cloud scrolls, gold, blue, green), a deep painted beam frieze (red/gold/blue lotus-and-jewel bands), white
  steel lattice screens between the columns, three green-marble steps, a central door with **yellow curtains** and a red
  valance, murals of the **Four Guardian Kings** on the side walls; above, a painted balcony of window panels under a dark
  red cornice; side walls **ochre-yellow `#E2B73F`** with maroon window frames; gilt roof above (aerial) [A].
* **In the game now**: house grammar, Gompa archetype, hipped, 3 storeys 10.5 m.
* **Must-have**: [ ] recessed courtyard gompa on the W axis [ ] gilt roof visible from the kora and from above
  [ ] guardian-king portico.

### 3.9 S9 - Other gompas on the ring

* **Old Dhabzang** (Kagyu) w209312611, 9.3 × 9.3 m, NE r 68-70 [O]; small gompa [V exterior].
* **Tashi Samtenling Monastery** n12959355617, E r 114 (beyond the ring; gilt roof visible in the aerial at bearing ~80°,
  r ~110) [O, A].
* **"small stupas"** w1418542893 (14.6 × 2.9 m row of chortens, NW r 104) [O].
* All drawn today as plain houses.

### 3.10 S10 - Ghyoilisang Peace Park, pond and Guru Rinpoche statue (NW, r 103-130)

* Pond w209312627 (26 × 20 m) with a ring path, the **Guru Rinpoche (Padmasambhava) statue** n6370326877 in the middle,
  park w680349047, gate n6370326843, toilets n6370326845 [O]. "Pond and Peace park in the backyard of Bouddhanath Stupa
  that housed Statue of Manjushree" (OSM description) / Padmasambhava statue [S Wikipedia]. Game: pond + park drawn
  as areas [G]. Statue [V photo].

---

## 4. Small site elements (life and props)

| Element | Where | Look |
|---|---|---|
| Butter-lamp tables | in front of the Ajima shrine, lamp houses, kora edges | brass cups (Ø 6-8 cm) in rows on black iron tables; lit at dusk [P: lamps/00, night/*] |
| Juniper kiln (sang-khang) | in front of the S gate pavilion (and N) | white chorten-shaped oven 1.8-2.2 m, sooty arched mouth, smoke [P: corner/02; S mountainsoftravelphotos] |
| Flagpoles (darchor) | along the wall, at T1 corners | 8-10 m wooden poles, vertical five-colour banners [P] |
| Benches | outer brick zone, W side r 55 [O n5162080318-20] | wooden slats, iron legs, facing the stupa |
| Map board | S lane r 53 [O n5162055056] | information map "Monasteries around Boudha Stupa" (painted wall map) [P: gate/04] |
| Public tap | N r 84 [O n3479688817] | stone/brass tap |
| Pigeons | dome, kora (S side feeding) | big flocks |
| Night | festival strings of coloured lights on the terraces, lamp rows [P: night/04] | optional |
| People | pilgrims walking **clockwise**, spinning wheels, monks in maroon, prostrating pilgrims, tourists | [P: kora/*, circle/*] |

---

## 5. Recommended build split (for the modeller)

The site exceeds one hero budget (class A 36k LOD0). Build it as separate objects, each with LOD0-2 and its own colliders:
(1) stupa core (ring, dome, harmika, spire, crown, flags); (2) terraces + stairs + chortens + statues; (3) compound wall
with niches, finials, kilns, poles (instanced niche module × 147); (4) north entrance complex (Ajima + gate + stair + NE
house); (5) main gate; (6) Guru Lhakhang facade + bell; (7) Jamchen and the other gompa fronts; (8) plaza paving decal and
benches. Ring houses stay in the house grammar but take the §3.7 profile (heights from the inferred levels, kora shops,
rooftop cafés). Flatten the terrain (§2.3).

---

## 6. Photo-matched viewpoints (for render-vs-photo checks)

Unity coordinates in tile 10/521/163 (OBJ z = -Unity z), ground ≈ 1330.9 after flattening:

| View | Eye (Unity x, y, z) | Look at | Photo |
|---|---|---|---|
| North rooftop (Guru Lhakhang roof) | 766.8, 1346, 242.4 | 761.8, 1338, 176.4 | around/03, pair_0 |
| SW rooftop | 701.8, 1352, 121.4 | 761.8, 1338, 176.4 | corner/04, pair_1 |
| Main gate from Boudha Main Road | 737.5, 1332.6, 84.0 | 741.0, 1335.5, 97.0 (gate at ≈ 740.3, 96.2) | Commons gate photos, pair_2 |
| Kora at the wall (south side) | 759.8, 1332.6, 123.4 | 761.8, 1350, 176.4 | corner/02, pair_3 |
| Telephoto silhouette | az 200°, el 4°, fov 8° | stupa | panorama/02, pair_4 |
| North kora, Guru Lhakhang front | 769.8, 1332.6, 231.4 | 772.7, 1338, 240.4 | bell/09, pair_5 |

---

## 7. Open questions [V]

1. Absolute height 36 m (published) vs ~44 m (photos + OSM dome) - this brief recommends the photo ratios.
2. Side of the south gate pavilion and kiln; stairs on the E/S/W arms; count and corners of the mini-Boudha chortens.
3. Exact position of the main gate (OSM node r 72 vs photo r ~83).
4. Old Dhabzang exterior and Jamchen's roof (the Jamchen portico is now documented; Commons rate-limited the rest).
5. Valance colour (red now; green in some years) and the current state of the saffron arcs (renewed at festivals).

---

## 8. Numbers at a glance

Centre 27.721445 N 85.362004 E; axes 9°/99°/189°/279°; T1 81.5 × 79.4 (top 4.0), T2 62.5 × 61.8 (8.0), T3 49.8 × 50.1
(10.5); ring Ø 38 → 35.5 (12.0); dome Ø 33.5, apex 23.7; harmika 7.0, visible 3.1 + valance 1.5; spire 13 steps 9.1 m,
6.6 → 2.4; canopy Ø 4.4, 3.5 m; finial 4.2 m; total ≈ 44.8 m; wall r 45-55, 2.6 m, 312 m, 147 niches; kora plaza to the
fronts at r 55-75; main gate r 72-85 bearing 195; Ajima + N gate r ~49 on bearing 9; Guru Lhakhang r 62-66 bearing 4-11;
Jamchen r 81-88 bearing 273.

---

## 9. Photo and source credits (reference only, not in the repo)

| Used for | File (refs/mon-boudha/) | Author, licence, source |
|---|---|---|
| North view, Ajima, N gate | around/ref_03 = corner/ref_00 | Jorge Lascar, CC BY 2.0, flickr.com/photos/8721758@N06/17230278683 |
| SW rooftop view | corner/ref_04 | Jorge Lascar, CC BY 2.0, flickr.com/photos/8721758@N06/17210367083 |
| Kora wall, kiln, gate pavilion | corner/ref_02 | Jorge Lascar, CC BY 2.0, flickr.com/photos/8721758@N06/17663021918 |
| T1 bell chorten, statues | corner/ref_06 | Jorge Lascar, CC BY 2.0, flickr.com/photos/8721758@N06/17664708809 |
| Wide views | corner/ref_05, around/ref_05-09 | Jorge Lascar, CC BY 2.0 |
| Mini-Boudha chorten, arcs | spire/ref_06 | Bibek KC, CC0, wordpress.org/photos/photo/5346a11cac/ |
| Wall niches with valances | spire/ref_00, ref_01 | shankar s., CC BY 2.0, flickr 49628737823, 49628742953 |
| Old spire and canopy frame | spire/ref_05 | Takemany Showfew, CC BY-NC-ND 2.0, flickr 53387799924 |
| Spire close-up | general3/ref_00 | Prof. Mortel, CC BY-NC-SA 2.0, flickr 6840763683 |
| Eyes close-up | tamang/ref_06; eyes/ref_00 | CCRcreations, CC BY-NC-ND 2.0, flickr 17474668063; eyes/credits.txt |
| Elephant statue | tamang/ref_08 | CCRcreations, CC BY-NC-ND 2.0, flickr 17472990834 |
| Telephoto silhouette | panorama/ref_02; general/ref_08 | Cheryl Marland CC BY 2.0 flickr 3015494344; terbeck CC BY-NC-SA 2.0 flickr 14515939701 |
| Guru Lhakhang + bell | bell/ref_09; gompa2/ref_00-01 | Cheryl Marland CC BY 2.0 flickr 3015500328; petmyrhino CC BY-NC-SA 2.0 flickr 16315286505, 16289346586 |
| Ajima exterior | y2019/ref_06, ref_08 | shankar s., CC BY 2.0, flickr 49629531942, 49628741103 |
| Ajima interior | commons_ajima/c01, c02 | Wikimedia Commons "Hārītī Devī temple, Boudhanath.jpg", "Ajima (Hariti Devi) Boudhanath.jpg" |
| Main gate (current) | commons/Boudhanath_entrance_gate*.jpg | Wikimedia Commons, Category:Boudhanath gate |
| Main gate (old) | gate2/ref_04 | Wonderlane, CC BY 2.0, flickr 4314891291 |
| Stair with pots, stone shrine | earthquake/ref_06, ref_07 | 10b travelling / Carsten ten Brink, CC BY-NC-ND 2.0, flickr 8257588870, 8226728631 |
| Dome from the north (roof ornaments) | commons_more/c04 | Wikimedia Commons "Boudhanath Stupa from monastery.jpg" |
| Gilt dragon roof corner | commons_more/c01 | Wikimedia Commons "Guru Lhakhang Monastery, Boudhanath.jpg" |
| Kora paving, benches | kora/ref_08 | eriktorner, CC BY-NC-SA 2.0, flickr 13298796893 |
| Kora today (paving, wall valances, ring fronts) | commons_late/c03 | Wikimedia Commons "Buildings surrounding Boudhanath stupa.jpg" |
| N kora from above, Ajima finial | commons_late/c04 | Wikimedia Commons "Bodnath-Umgang-18-Ringstrasse-2013-gje.jpg" |
| Ajima exterior (2016) | commons_late/c05 | Wikimedia Commons "Ajima Temple.jpg" |
| Jamchen Lhakhang portico | commons_late/c01 | Wikimedia Commons "Jamchen Lhakhang, Boudhanath 01.jpg" |
| Kora wall and pilgrims | circle/ref_02-09 | Wonderlane, CC BY 2.0, flickr 3161546366 ... 2357604034 |
| Ring houses from the stupa | gate2/ref_00 | Wonderlane, CC BY 2.0, flickr 12216642454 |
| 2016 reconstruction | reconstruction/ref_07-09; panorama/ref_05 | RachidH CC BY-NC 2.0; albedo20 CC BY-NC-ND 2.0 flickr 29338199074 |

Web: [Wikipedia, Boudha Stupa](https://en.wikipedia.org/wiki/Boudhanath) ·
[Kathmandu Post 2016-10-31](https://kathmandupost.com/valley/2016/10/31/boudhanath-rebuilding-nears-completion) ·
[Kathmandu Post 2016-11-23](https://kathmandupost.com/valley/2016/11/23/boudhanath-stupa-reopens) ·
[Studio Matrx guide](https://www.studiomatrx.org/guides/boudhanath-stupa-architecture) ·
[sacredsites.com](https://sacredsites.com/asia/nepal/boudhnath_stupa.html) ·
[Lonely Planet, Harati (Ajima) shrine](https://www.lonelyplanet.com/points-of-interest/harati-ajima-shrine/1508341) ·
[thelongestwayhome, Ajima shrine](https://thelongestwayhome.com/travel-guides/nepal/boudhanath-ajima-hariti-shrine.html) ·
[thelongestwayhome, guide](https://www.thelongestwayhome.com/travel-guides/nepal/guide-to-boudhanath.html) ·
[mountainsoftravelphotos, Boudhanath](https://mountainsoftravelphotos.com/Nepal%20-%20Kathmandu/Boudhanath/index.html) ·
[Wikimedia Commons, Boudhanath gate](https://commons.wikimedia.org/wiki/Category:Boudhanath_gate) ·
[Commons, Buddhist temples and monasteries at Boudhanath](https://commons.wikimedia.org/wiki/Category:Buddhist_temples_and_monasteries_at_Boudhanath) ·
[Commons, Buildings surrounding Boudhanath stupa](https://commons.wikimedia.org/wiki/Category:Buildings_surrounding_Boudhanath_stupa).
Map data © OpenStreetMap contributors (ODbL).
