# Kathmandu Durbar Square, the temples and the square: structure-by-structure reference brief

Package `mon-kds-temples` (heritage research, W2 detail pass, monuments round). Owner (W2_PLAYTEST_1 item 17):
"National Heritage and Monuments sites should look exactly as in real life ... Do same for the Basantapur Durbar Square.
Creating a bare minimum buildings wont cut out ... Try to draw how each of these monuments looks from each angle and
carefull design the 3D model of it." This brief is what the modeller builds the free-standing temples, statues, bell,
drums, rest houses, platforms and open spaces of Kathmandu (Hanuman Dhoka / Basantapur) Durbar Square from: the site
plan, every structure with measured numbers, colours and details, the views from each side, what the game shows today,
and a must-have checklist per structure. Cartoon style may simplify surfaces only; silhouette, proportions, tiers,
plinth stages, roof shapes, struts, windows, doors, finials, gilding, colours and the arrangement of the site must match
the place **as it stands in 2023-2026** (after the post-2015 reconstructions).

Scope: Kasthamandap, Silyan (Simha) Sattal, Ashok Binayak (Maru Ganesh), Maju Dega and the small white shikhara at its
foot, the Narayan temple (still a ruined plinth), Shiva-Parvati, Trailokya Mohan Narayan with its Garuda, the Basantapur
Dabali, Laxmi Narayan Sattal, the Taleju (Big) Bell, Saraswati phalcha, the octagonal Krishna temple (Chyasin Dega), the
Big Drums, the Lakshmi Narayan temple, Kal Bhairav, Jagannath with its four corner shrines, Pratap Malla's column,
Indrapur, Gopinath (Shri Krishna Mahavishnu), Kotilingeshwar, Kageshwor (Kakeshwar), Mahavishnu, Mahendreshwar,
Bhagwati, the Sweta Bhairav mask, and Basantapur square with its paving and stalls. The palace itself (Hanuman Dhoka
gate, Taleju, Degu Taleju, Nasal Chowk, the towers, Gaddi Baithak, Kumari Ghar) is in the sibling brief
`docs/research/heritage/kds-palace.md` (worktree `mon-kds-palace`); shared items (Bhagwati, Sweta Bhairav, Big Drums)
are cross-referenced, and where this brief found a better position it says so (§7).

Tags: **[O]** OSM (Nepal extract `pipeline/data/raw/osm/nepal.osm.pbf`, Oct 2026), **[D]** DANAM, the Heidelberg
Academy's *Digital Archive of Nepalese Arts and Monuments* (measured drawings 2002-2026 by KVPT, W. Korn, A./B./Bi.
Basukala, T. Schrom; photos 2021-2026; CC BY-SA 4.0 unless stated), **[A]** Esri World Imagery z19 aerial with the local
grid, **[P]** read from photos, **[S]** published source, **[E]** estimate, **[V]** verify, **[G]** the game today
(base `97012ab`).

Reference material (outside the repo, never copied in): DANAM folders `/home/user/wt/refs/mon-kds-temples/danam/KATxxxx/`
(measured drawings `NN_dwg.jpg`, photos `NN_ph.jpg`, `captions.txt` with author and date, `contact_sheet.jpg`); Commons
folders `.../c_<topic>/` (`credits.txt`); Openverse folders `.../<topic>/` (`credits.txt`); DANAM descriptions as JSON in
the session scratchpad; OSM inventory `/home/user/wt/previews/mon-kds-temples/osm/inventory_osm.txt`; aerial overlays
`.../osm/aerial_west_osm.jpg`, `aerial_north_osm.jpg`, `aerial_bell_osm.jpg`; site plan
`/home/user/wt/previews/mon-kds-temples/site_plan_kds_temples.jpg`; current-game renders `.../current97/` (base
`97012ab`) and `.../cur34_*.png`, `cmp_*.jpg` (base `a87e5a4`, same hero recipes); mesh dumps `.../obj97/kds/`.

---

## 0. Verdict: what is wrong today (in order of how much it hurts recognition)

| # | In the game now [G] | Reality |
|---|---|---|
| 1 | **Kasthamandap** is a tall three-roof pagoda with steep roofs, ≈ 8 m posts and a white top cage on a high red brick block, 30.5 × 30.9 m (`cmp_kasthamandap.jpg`, `current97/vp_kastha_E.png`). | A **squat, very wide timber hall** (rebuilt 2021): 21 m plinth only 0.6 m high, an **open forest of dark posts 4.5 m tall**, a **very broad low first roof** (eaves 22.7 m across at 4.5 m), a brick band storey, a **continuous carved balcony gallery**, a second roof, a small top pavilion, 18.8 m to a **dark bronze** finial; a **low wooden picket fence** round it (§3 T1). |
| 2 | **Kal Bhairav** stands on the wrong footprint and faces west: the hero is anchored on OSM `w196261745`, which is really the small **Lakshmi Narayan temple**; the true Kal Bhairav spot (`w908861416`) carries a **4-storey generic house** (12.5 m). | Kal Bhairav is a **8.1 m tall stone shrine-wall facing north**: a blue-black six-armed relief in a flame arch, three bell domes on top, two lions, bells on the side wings (§3 T16, T17). |
| 3 | **The Narayan temple** (`w111984927`) is drawn as a complete generic two-roof pagoda. | It **collapsed in 2015 and has not been rebuilt** (DANAM, May 2026): a **bare four-stage brick plinth overgrown with grass and brick rubble**, fenced with a low wire mesh, motorbikes parked round it (§3 T6). |
| 4 | **Maju Dega** has an orange brick 9-step pyramid, brick-coloured upper walls and a gilt top; plinth 28 m. | Nine **tall, narrow brick stages** (25 → 9.9 m, 6.6 m high) under a much smaller temple; the upper storeys are **white lime plaster with black window frames**; the finial is a **white bell-shaped dome** with a gilt tip; a **white shikhara shrine** stands at the foot of the east stair (§3 T4, T5). |
| 5 | **Octagonal Krishna (Chyasin Dega)**: a 7 m one-roof generic pagoda; **Mahavishnu, Indrapur, Taleju Bell, Big Drums, Bhagwati wing**: plain generic houses 10-16 m tall; **Kotilingeshwar, Ashok Binayak, Garuda, Pratap Malla column, Silyan lions**: missing. | An octagonal three-roof temple 17.3 m on a 3.5 m stepped plinth with 24 columns; a three-roof temple restored 2023; a two-roof temple with a very broad lower roof; a bell under a small roof on two stone pillars on a white block; a brick drum house with two giant drums; a grey stone domed shrine; a brass-caged gilt-roofed shrine; a black kneeling Garuda; a stone column with a gilt royal group (§3). |
| 6 | **Trailokya Mohan**, **Jagannath**, **Shiva-Parvati** read as generic templates: brick-orange everything, no white plaster, no corner shrines on Jagannath, Shiva-Parvati without its window couple or its two-stage plinth with lions. | Trailokya: white plaster sanctum and upper walls with black keyhole niches, 5 brick stages, Garuda 10.7 m west; Jagannath: **four miniature two-roof temples on the platform corners**, three-door brick facades with pink blind arches; Shiva-Parvati: long brick house-temple, **the two figures at the central upper window**, five gilt finials (§3 T7, T8, T18). |
| 7 | Ground: sandy beige noise with brown road ribbons across the square (the NNE lane is drawn as a road through the temple field); Basantapur is an empty sand field (`current97/vp_basantapur_W.png`, `top_temples.png`); a **13.5 m generic house** stands on the 0.5 m platform `w908861401` at the foot of Maju Dega's stair (`vp_maju_SE.png`). | Pedestrian square paved with **grey stone flags** and **buff-pink brick**; Basantapur square holds **rows of curio stalls** (low tables, mats, umbrellas) on its raised brick platform (§2.3, T28). |

---

## 1. Sources and method

* **OSM [O]:** every way and node within ≈ 250 m (`kds_prev.json`, pyosmium), converted to local metres (centroids
  area-weighted, minimum-area boxes, bearings). Rich 3D parts exist for Maju Dega (`w246110126`..`w1415140479`),
  Trailokya Mohan (`w1414593332`..`w1416686188`), Shiva-Parvati (`w420291386`..`w420292576`), Kageshwor
  (`w1414590540..42`) and Kasthamandap (`w1416709264`). Many monuments are **unmapped or mis-tagged** (§2.5).
* **DANAM [D]:** 40 resources fetched (drawings, 2023-2026 photos, descriptions): KAT1970 Kasthamandap, KAT1990 Silyan
  Sattal, KAT1930 Maru Ganesh, KAT1910 Maju Dega, KAT1890 Narayan, KAT2020 Trailokya Mohan, KAT1840 Big Bell, KAT1820
  Saraswati phalcha, KAT1810 Chyasin Dega, KAT1830 Krishna, KAT1780 Lakshmi Narayan, KAT1770 Kal Bhairav, KAT1740
  Jagannath, KAT1791 Pratap Malla column, KAT1730 Hanuman pillar, KAT1520 Mahavishnu, KAT1521 Kashinath, KAT1522 Varaha,
  KAT1530 Kotilingeshwar, KAT1510 Mahendreshwar, KAT1800 Sweta Bhairav, KAT2000 Kavindrapur, KAT1550/1560 Taleju gate
  Shiva temples and others. **Dimensions below come from these drawings**: each scale bar was measured in pixels and
  the silhouettes were profiled row by row (Kasthamandap elevation 42.8 px/m, Maju Dega 43.2, Jagannath 60.6, Chyasin
  Dega section 60.5, Mahavishnu section 82.5, Kal Bhairav 121; site plans 21-60 px/m). Relative positions between
  neighbouring monuments were read from the DANAM site plans (KAT1740 and KAT1780 show Jagannath, Lakshmi Narayan, Kal
  Bhairav, Indrapur and Shri Krishna Mahavishnu on one sheet; KAT1520 shows Mahavishnu, Kotilingeshwar and Kageshwor;
  KAT1910 and KAT2020 show Maju Dega, Trailokya, the Garuda and Kumari Ghar).
* **Photos [P]:** DANAM 2023-2026 sets (the current state), Wikimedia Commons categories (≈ 60 topics × 16-18 images,
  2007-2026, incl. "Kageshwor Temple (2026)", "Ashok Binayak Temple (2026)", "Basantapur Dabali 2024"), Openverse
  (Flickr CC, mostly 2008-2014, pre-quake) for extra angles.
* **Aerial [A]:** Esri World Imagery z19 tiles (georef from the sibling package) with the local grid and OSM outlines
  (`osm/aerial_*.jpg`, `site_plan_kds_temples.jpg`). Shadows and haze limit it to ±2-3 m.
* **Published [S]:** DANAM descriptions; Lonely Planet (Bhagwati "on the north-west corner of the Gaddhi Baithak"; Big
  Drums north of the Krishna temple next to the Kot); Wikipedia "List of monuments in Hanuman Dhoka"; reconstruction news
  (Kasthamandap completed 2021; Basantapur tower handed over 2025). Links in §9.
* **Game [G]:** `HeroCatalog.cs` recipes (kasthamandap, maju_dega, trailokya_mohan, jagannath, shiva_parvati,
  kal_bhairav), `SacredSelector` generics, the house grammar; a temporary dump test (kept outside the repo at
  `/home/user/wt/previews/mon-kds-temples/KdsTemplesDumpTmp.cs.txt`) dumped heroes, generics, buildings, terrain, roads and areas around the square; rendered with
  `tools/mesh-preview/render.py` from the photo viewpoints of §6 (`/home/user/wt/previews/mon-kds-temples/current97/`:
  `vp_kastha_E`, `vp_maju_SE`, `vp_basantapur_W`, `vp_trailokya_W`, `vp_shivaparvati_S`, `vp_kalbhairav_N`,
  `vp_forecourt_W`, `vp_bell_lane_N`, `top_temples`, `bird_all`; `sheet_vp.jpg`, `sheet_vp2.jpg`).

---

## 2. Site frame and plan

### 2.1 Anchor and axes

* Local frame **identical to the palace brief**: origin **27.704500 N, 85.307500 E**; x metres east, y metres north
  (1° lon = 98,573 m, 1° lat = 110,574 m). lat = 27.7045 + y / 110574; lon = 85.3075 + x / 98573. The origin lies in game
  tile 10/516/161 at tile-local (528.07, 288.42): Unity (528.07 + x, h, 288.42 + y). Ground ≈ 1300.8 m at the origin,
  ≈ 1303.5 m at Kasthamandap [G terrain].
* Most temples sit on a grid turned **≈ 10-22° clockwise** from north (Kasthamandap 11.6°, Trailokya 11°, Jagannath
  10.9°, Chyasin 10°, Kotilingeshwar 20°, Narayan 21°, Maju Dega 16.5°): use each structure's own bearing below.
* The square is level to ±1.5 m across 200 m (falls gently to the east); plinths and platforms are built objects, not
  terrain.

### 2.2 Zones

| Zone | Extent (local m) | What is there |
|---|---|---|
| Maru (west end) | x -200..-150, y -110..-30 | Kasthamandap (T1), Silyan Sattal (T2) south of it with the buried-timber platform "Maru Dabu" between, Ashok Binayak (T3) at the NE corner, two small Shiva temples (T30, T31), Maru Hiti (sunken spout, (-199, 2)), Laxmi Narayan Sattal (T11). |
| Temple field | x -150..-90, y -70..0 | Maju Dega (T4), the white shikhara (T5), Trailokya Mohan (T8) with Garuda (T9), Basantapur Dabali (T10), Narayan ruin (T6), Shiva-Parvati (T7). Grey flagstones, pigeons, people sitting on the plinths. |
| Bell lane | x -90..-55, y -20..40 | The street running NNE between the Bhagwati wing (T26) and the houses: Big Bell (T12), Saraswati phalcha (T13), Chyasin Dega (T14), Big Drums (T15). |
| Forecourt | x -45..-10, y 5..40 | Lakshmi Narayan (T16), Kal Bhairav (T17), Jagannath (T18), Pratap Malla column (T19), Indrapur (T20), Gopinath (T21), Sweta Bhairav screen (T27) on the palace front. |
| North field (Makhan side) | x -40..20, y 45..100 | Kotilingeshwar (T22), Kageshwor (T23), Mahavishnu (T24), Mahendreshwar (T25), the Taleju precinct to the east (palace brief). |
| Basantapur | x -95..150, y -125..-75 | Raised brick platform with stalls (T28), Kumari Ghar at the SW corner, the nine-storey tower on the north (palace brief). |

### 2.3 Ground and paving (must fix)

* **Pedestrian square** (OSM relation `r21291455`, `surface=sett`, outer `w1257513676`, 85 nodes): the whole area from
  Kasthamandap to the Taleju gate and down to Basantapur. No road surface inside it [O]. Vehicles still cross it on the
  NNE lane past the bell (traffic ban only partly enforced) [P].
* **Temple field and forecourt:** grey stone flags 0.5-0.8 × 0.4 m, #8F8B83 lit / #6E6A64 shade, darker joints #57534D;
  worn smooth; red-brick strips (#A4583F) round some plinths [P: KAT1970 04, KAT1740 06, KAT2020 14].
* **Basantapur square:** buff-pink brick in herringbone / running bond, **#B88E70** lit / #93684F shade (sun-bleached),
  with grey flag borders and walkways [P: c_basantapur_chowk 04-10]. The raised platform edge is a grey stone kerb
  0.2-0.3 m [P: c_dabli 00].
* **Plinth treads:** brick with grey stone nosings; grass tufts in joints on Maju Dega and the Narayan ruin (#6F7A45) [P].
* Pigeons in flocks on the Jagannath platform, Maju Dega steps and Basantapur (fauna package).

### 2.4 Plan sketch (north up; not to scale; positions §3, drawn on the aerial in `site_plan_kds_temples.jpg`)

```
                                  Mahendreshwar T25 (13,92)
                    Mahavishnu T24 (-16,72)
          Kotilingeshwar T22 (-30,64)    Kageshwor T23 (-7,53)          TALEJU (45,44) [palace]
   Big Drums T15 (-61,38)  Indrapur T20 (-29,34)  Gopinath T21 (-19,32)
   CHYASIN T14 (-63,28)  Kal Bhairav T17 (-39,31) faces N
                         Lakshmi Narayan T16 (-40,25)
   Saraswati T13 (-71,15)         JAGANNATH T18 (-28,16) + 4 corner shrines   column T19 (-20,15)  Sweta Bhairav T27 (-9,15)
   BIG BELL T12 (-79,9)                                                     |palace gate (-13,0) [palace]
                                    ?w907080042 (-47,-4)        +-- white palace front --
   NARAYAN ruin T6 (-120,-5)  SHIVA-PARVATI T7 (-99,-10)
                              Bhagwati wing T26 (-79,-16) ///
 Shiva T30 (-177,-45)  MAJU DEGA T4 (-131,-26)  stair -> E
 Ashok Binayak T3 (-159,-47)     white shikhara T5 (-114,-37)
 Laxmi Narayan Sattal T11 (-145,-50)                         GADDI BAITHAK [palace]
 KASTHAMANDAP T1 (-170,-63)  Garuda T9 (-132,-58) -> TRAILOKYA T8 (-121,-60) <- Dabali T10 (-111,-62)
 Shiva T31 (-188,-74)                                   KUMARI GHAR (-102,-84) [palace]
 SILYAN SATTAL T2 (-173,-96)                         BASANTAPUR SQUARE + stalls T28 (-86..-4, -77..-116)
```

### 2.5 OSM identity fixes (the game reads these ids)

| OSM | Tagged as | Really | Evidence |
|---|---|---|---|
| `w196261745` (-40.2, 24.8) 5.8 × 5.6 | "Kal Bhairab Temple", pyramidal | **Lakshmi Narayan temple** (two roofs, faces W) | DANAM KAT1780/KAT1740 site plans: LN sits 12.6 m W, 9.0 m N of Jagannath's centre = this way; Kal Bhairav is 6 m further N. |
| `w908861416` (-38.9, 30.9) 5.0 × 2.5 | building=yes | **Kal Bhairav** relief backing wall | Same plans: Kal Bhairav's back slab 11.2 m W, 14.5 m N of Jagannath. |
| `w169429514` (-19.0, 32.0) | "Gopinath Temple" | Gopinath = DANAM KAT1750 **Shri Krishna Mahavishnu**, three roofs | KAT1740 plan. |
| `w907080039` (-28.7, 34.3) | building=yes | **Indrapur** temple | KAT1740 plan (Indrapur 1.2 m W, 17.4 m N of Jagannath). |
| `w1415081497` (-15.5, 72.2) | building=yes | **Mahavishnu** temple (KAT1520, restored 2023) | KAT1520 plan: 8.5 m W, 16.6 m N of Kageshwor. |
| `w907080047` (-63.1, 27.8) octagon | building=yes | **Chyasin Dega**, octagonal Krishna temple | Octagonal outline ≈ 13 m; DANAM KAT1810 neighbours match `w907080046` (N) and `w1029960700` (W); node `n2083743575` "Krishna Temple" beside it. |
| `w907080046` (-60.9, 37.7) | building=yes | **Big Drums** house [V] | "Just beyond the Krishna temple" (LP); the small brick block north of Chyasin Dega in KAT1810's plan. |
| `w907080044` (-71.2, 15.0) | building=yes | **Saraswati phalcha** [V] | Node `n2083751556` "Saraswati Temple" inside it. |
| `w111984927` (-120.1, -5.4) | "Narayan Mandir" | Narayan temple **ruin** (plinth only) | DANAM KAT1890, May 2026. |
| `w1415152800` (-78.8, -15.2) | brick range, 3 levels | Palace NW wing **carrying Bhagwati** [P/V] | Photos from Shiva-Parvati show the three-roof Bhagwati rising from this range. |
| `w1028854863` (-113.7, -37.4) + 20 parts | temple | small **white shikhara** at Maju Dega's stair foot | 3D parts all white, stepped to 5.9 m. |
| `n2084347901` (-150.8, -34.2) | "Maru Ganesh" | Ashok Binayak is ≈ (-158.6, -47.0) [D] | KAT1970 site plan (14 m from the node). |
| `n2084347899` (-124.8, -53.6) | "Garuda" | Garuda pedestal ≈ (-131.5, -58.2) [D] | KAT2020 site plan: 10.7 m W, 1.7 m N of the temple centre, on the stair axis. |
| `w218440319` (-47.6, -97.3) | amenity=marketplace | Basantapur square stall platform | Matches the stall grid on the aerial. |
| `w908861401` (-114.4, -26.1) 6.6 × 5.0 | building, `height=0.5` | low stone platform at the foot of Maju Dega's east stair | OSM height; the game builds a 13.5 m 4-storey house on it [G]. |
| `w196809461` (-187.2, -43.0) | "Ashoka Ganesha Dyochhen" | the god-house (dyochhen) building NW of Kasthamandap, not the shrine | Ashok Binayak itself is T3. |

---

## 3. Structure inventory

Positions are local (x, y) metres and lat/lon. Heights are above the local ground. Colours are lit / shade hex for the
toon material (shade ≈ 0.68 × lit). "Front" = the main door or stair. Photos: DANAM `danam/KATxxxx/NN_ph.jpg`, Commons
`c_<topic>/c_NN.jpg`.

### Shared Kathmandu-square vocabulary (applies to every pagoda below unless stated)

* **Roof tiles (jhingati):** small overlapping clay tiles in rows down the slope, weathered terracotta-brown **#8E5B48**
  / #5F3D30, with moss streaks #6B6A45 on older roofs; new tiles (Kasthamandap 2021, Chyasin, Trailokya) warmer
  **#9A6049**. Hipped, slightly concave roofs; eave pitch 20-30°, upturned corner tips carrying small bells or
  bird-like finials. Ridge pieces darker.
* **Red cloth valance (frill):** hung under every eave in KDS (not in Patan/Bhaktapur): crimson **#A9232C**, pleated
  drops 0.3-0.6 m deep with paler pink-red folds **#D2555A**; on the first roofs often doubled [P everywhere].
* **Struts (tunala):** dark carved timber **#3B2A20** / #241A14 leaning ≈ 45° from the wall top to the eave, 4-8 per
  side plus diagonal corner struts (corner struts often winged mythical beasts). Carve them as stacked deity panels with
  a small figure at the foot, **general deity/animal/pattern carving only** (several real struts, e.g. Jagannath,
  Maju Dega, are erotic; ASSET_MANIFEST forbids depicting that).
* **Timber:** posts, lintels, window frames dark brown #3E2B20; carved latticework windows (nearly black #2A1E18) in
  brick or white walls.
* **Brick:** exposed red-brown **#A4553D** / #73392A (Jagannath, Chyasin, Mahavishnu, Kumari); Maju Dega plinth
  **#A35A3C** with buff stone nosings **#BDB2A0**.
* **White lime plaster:** **#EEEBE3** / #CFCAC0 (Maju Dega, Trailokya, Narayan, Kageshwor shikhara, Silyan parapet, Bell
  block).
* **Gilt:** finials, toranas, roof-edge ornaments **#D4A93A** / #9C7A26; brass **#B8973F**; dark bronze **#5B5246**.
* **Grey stone:** **#8C877D** / #66625B (Kotilingeshwar, Kal Bhairav frame, lions, columns).

---

### T1 - Kasthamandap (Maru Satah), rebuilt 2021

* **OSM:** `w183558418` plinth outline 21.8 × 21.4 m, `height=1` (= plinth); part `w1416709264` 19.8 × 19.6 m roof
  [O]. DANAM KAT1970 (site plan, floor plan, elevation and section 2023) [D].
* **Position:** **(-169.7, -62.6)** = **27.703934 N, 85.305778 E**. Grid bearing 11.6°/101.6°.
* **Orientation:** main entrance **east** (bearing ≈ 102°): 3 steps between two stone lions on short pedestals; smaller
  stairs in the middle of the N, W and S sides [D site plan].
* **History [S/D]:** the timber pavilion that named Kathmandu (mentioned 1143 CE); collapsed 25 Apr 2015; rebuilt by KMC
  and the community with traditional joinery and clay tiles, completed Dec 2021.
* **Plan [D]:** square plinth **21.1 m**, ≈ 0.6 m high, two steps. Open ground floor with an outer ring of posts on a
  **19.2 m** square and inner rings (≈ 50 posts). **Four corner bays** (≈ 7 × 7 m) are raised platforms closed by brick
  walls on their outer faces, each with a Ganesh shrine at its inner corner; the cross-shaped centre is open; a small
  **Gorakhnath shrine** (wooden aedicule ≈ 2.5 × 1.8 m) stands at the centre; a stair to the upper floor in the NW
  quadrant.
* **Elevation (E) [D, measured]:**
  * Posts and open hall: 0.6-4.5 m. Dark timber posts ≈ 0.25 m square with carved capitals.
  * Corner mezzanine **carved balconies**: projecting balcony fronts ≈ 6.5 m long at 2.3-3.0 m left and right of the
    open central bay on each face.
  * **First roof:** eave **22.7 m** across at **4.5 m**, rising at ≈ 21° to **7.6 m** where it meets the upper storey:
    broad, low and sweeping; the dominant shape from every side.
  * **Second storey:** brick wall band **11.8 m** wide, 7.6-8.9 m, with a row of small carved window/vent panels; above
    it a **continuous projecting carved balcony gallery 14.7 m** wide at 8.9-10.0 m with posts up to the second eave.
  * **Second roof:** eave **17.4 m** at **10.6 m**, up to 13.7 m.
  * **Third storey:** small pavilion 6.5 m wide with a balcony rail, posts.
  * **Third roof:** eave **10.1 m** at **15.5 m**, apex 17.7 m; **finial** (dark bronze bell-and-spire with a small gilt
    tip) to **18.8 m**.
* **Colours [P: KAT1970 04-14, 2023]:** tiles #9A6049 (new) / #63402F; valances crimson #A9232C with #D2555A folds, deep
  on the first roof; posts, balconies and galleries dark brown #4A3326 (new timber slightly redder #6B4A35); brick
  #A65A40; plinth grey stone #9C978E; lions pale grey stone #B9B4AA; finial #5B5246.
* **Distinctive details:** the **low wooden picket fence** round the plinth (≈ 1.0 m, pointed stakes, weathered
  #A08B73) with gaps at the stairs; deep shadow under the first roof; pigeons on the first roof; the empty niche
  (Marugah) in the south plinth; carved wooden figures on the west lower facade.
* **Views:** **E** (DANAM 04, 09): the postcard: wide low first roof over the dark open hall, lions at the centre,
  balconies left and right, two smaller roofs stacked above. **S** (DANAM 12, 14): same profile across the Maru Dabu
  open space from Silyan Sattal. **W**: a narrow lane, houses 4-6 m away. **N**: Ashok Binayak's brass cage and the
  small Shiva temple. From the air (DANAM 06): three nested square roofs, the first very wide.
* **In the game now [G]:** `Mandapa` hero 30.5 × 30.9 m, 20.5 m; ground-floor posts ≈ 8 m tall, the first roof much
  steeper and narrower, a dark wooden box as the second storey, a white cage on top and a gilt finial; no balconies, no
  fence (`current97/vp_kastha_E.png`, `cmp_kasthamandap.jpg`).
* **Must have:**
  1. Footprint 21.1 m plinth at the OSM outline, 0.6 m high, entrance and lions on the east.
  2. Open ground floor of dark posts 4.5 m tall, see-through in the centre; closed brick backs of the corner bays.
  3. First roof eave 22.7 m at 4.5 m, low pitch (≈ 21°), deep crimson valance.
  4. Brick band + continuous carved balcony gallery (14.7 m) between the first and second roofs.
  5. Second roof 17.4 m at 10.6 m; small third storey; third roof 10.1 m at 15.5 m; total 18.8 m.
  6. Dark bronze finial, not gilt.
  7. Corner mezzanine balconies on the ground floor; low picket fence.
  8. Proportions: width ≈ 1.2 × height (it reads wide and squat, not tall).

### T2 - Silyan Sattal (Simha Sattal, "lion rest house")

* **OSM:** `w183559499` "Simha Sattal" 14.8 × 9.8 m, h 7.5, hipped [O]; DANAM KAT1990 floor plan **13.10 × 7.37 m** [D].
* **Position:** **(-173.2, -95.9)** = 27.703633 N, 85.305743 E; long axis E-W (bearing 98.6°); 33 m south of
  Kasthamandap across the Maru Dabu open space.
* **Form [D/P: KAT1990 02-11]:** three storeys, ≈ 11 m [E]:
  * Ground floor: closed **dark brown timber shutters / shop fronts** on all sides on a low plinth, under a **pent
    (skirt) tile roof** with a red valance at ≈ 3.2 m.
  * First-floor parapet: a **white plastered band with a scalloped (cusped) crenellation**, white square **corner
    piers** carrying **gilt winged lions (sardula)** ≈ 1 m tall, one per corner (added 1929).
  * Second floor: open **veranda gallery** all round: slender dark posts, carved balcony rail.
  * Main roof: hipped, tile, red valance; on the ridge centre a **gilt pinnacle with a gilt flame arch**.
* **Orientation:** main facade **south** (to the square); the shrine (Krishna on Garuda) inside faces west [D].
* **Views:** **S** (KAT1990 02-03): the long front, lions at both ends of the white parapet; **E/W** (KAT1990 07, 10):
  short sides, the corner lions frame the gallery; **N**: faces Kasthamandap across the open Maru Dabu; **NE** (06):
  three layers - dark shutters, white scalloped band, open gallery - under a broad hipped roof.
* **In the game now [G]:** plain 2-storey house box (house grammar, `w183559499` "Roof", 7.5 m).
* **Must have:** 13.1 × 7.4 m; closed timber ground floor with pent roof and valance; white scalloped parapet; four gilt
  winged lions at the corners; open veranda storey; hipped roof with gilt flame-arch pinnacle.

### T3 - Ashok Binayak (Maru Ganesh, Ashoka Vinayaka)

* **OSM:** node `n2084347901` "Maru Ganesh" at (-150.8, -34.2) [O, 14 m off]; DANAM KAT1930/KAT1970 site plans put the
  shrine at **≈ (-158.6, -47.0)** = 27.704075 N, 85.305891 E, at the NE corner of Kasthamandap, attached to a house on
  its north [D].
* **Form [D/P: KAT1930 02-19, c_ashok 2026]:** single-storey cubic shrine ≈ **2.4 × 2.6 m**, ≈ 4.5 m to the ridge [E];
  walls clad in **brass** (#B8973F) with gilt-framed lattice windows and carved brass pilasters; a **single gilt copper
  roof** with flared, upturned corners and a red valance, **no finial** ("Gaju-maru", without a pinnacle); door **south**
  with a silver repoussé torana; a pair of stone lions and a brass lion at the door, a brass shrew in front; a big bell
  and lamp stand on the east. The whole shrine is enclosed by a **brass-bar cage** ≈ 2.8 m tall with rows of small bells
  hung on it.
* **Views:** **S** (KAT1930 02): door, lions, brass lattice, gilt roof above the cage; **SE/NE** (03-07): the cage's
  bell rails; from Kasthamandap's NE corner it sits in a narrow lane opening, a house wall behind (north).
* **In the game now [G]:** missing (the Ashoka Ganesha Dyochhen `w196809461` is a plain 12.5 m house).
* **Must have:** small brass cube + gilt flared roof without finial; bar cage round it; door south.

### T4 - Maju Dega (Maju Deval, Shiva, 1690)

* **OSM:** `w183562980` "Maju Dega Mandir" 22.6 × 22.2 m (= plinth stage 2), `height 0.5`; 3D parts `w1415139993`
  (19.9 m, 1.4), `w246110143` (18.5, 2.0), `w246110126` (17.1, 2.6), `w1415349760` (15.8, 3.6), `w246110133` (12.8, 4.4),
  `w246110142` (8.6, 6), `w246110418` roof 14.5 m at 9-12.5, `w248254287` roof 11.9 m at 13.5-16.5, `w248256020` roof
  9.0 m at 18-21, white walls `w247405487`/`w420245281` [O]. DANAM KAT1910 (site plan and floor plan 2025, W. Korn's
  1975 west elevation updated 2025, photos Jan-Apr 2026) [D].
* **Position:** **(-130.7, -26.1)** = 27.704264 N, 85.306174 E. Grid bearing ≈ 16.5° (site plan) / 106.5° (OSM).
* **Orientation:** faces **east** (bearing ≈ 100-107°): one straight stair up the middle of the east face [D].
* **Plinth [D, measured]:** **nine stages**, each ≈ 0.73 m high, widths **25.0, 22.6, 20.5, 18.5, 16.5, 14.7, 13.2,
  11.5, 9.9 m**; top platform at **6.6 m**. Brick faces #A35A3C with buff stone nosings #BDB2A0 on every tread; people sit
  on the steps. East stair ≈ 1.6 m wide, 30+ steps, with **sloping whitewashed side parapets** [P: KAT1910 05, 12].
  A stone **Nandi** sits on the lowest stage at the **west** (rear) [D].
* **Temple [D, measured]:** ground floor: an ambulatory of dark wooden posts (4 per side) round a sanctum with doors on
  all four sides, posts 6.6-9.8 m; a beam band; wall/strut zone 9.8-11.6 m. **First roof** eave **14.1 m** at **11.8 m**
  (top ≈ 14.6 m); second storey 5.3-6.7 m wide; **second roof** eave **9.6 m** at **16.6 m** (top 19.0 m); third storey
  ≈ 3-4 m; **third roof** eave **6.7 m** at **20.8 m**, apex 22.5 m; **finial: a white plastered bell-dome** with a small
  gilt tip, top **23.5 m**.
* **Colours [P: KAT1910 03-15, 2026]:** upper walls and sanctum **white lime plaster** #EEEBE3 with **black carved
  windows** (#2A1E18) set as a strict symmetric pattern of dark rectangles on white (the strongest recognition cue from
  far); struts dark #3B2A20; tiles #8E5B48; valances #A9232C; pinnacle white with gilt #D4A93A tip.
* **Views:** **E** (from Shiva-Parvati / Basantapur): the stair rising to the centre, white shikhara (T5) at its foot,
  three roofs on top of a tall stepped pyramid. **N** (KAT1910 07): pyramid of steps with the temple small on top.
  **SW** (KAT1910 08): plinth corner sharp, houses behind. **S**: Trailokya Mohan in front.
* **In the game now [G]:** `Pagoda` template, plinth 28.1 × 27.7 m, 25.5 m tall; uniform orange brick, gilt finial, steps
  equal and too broad, temple walls brick (`cur34_maju_dega.png`, `cmp_maju_dega.jpg`).
* **Must have:**
  1. Nine stages 25.0 → 9.9 m wide, top at 6.6 m; buff stone nosings on every tread.
  2. One central east stair with white sloping parapets.
  3. Temple only 8.4 m wide at the post line on a 9.9 m top: the temple is small on a big pyramid.
  4. White plaster walls with black window pattern; three roofs 14.1 / 9.6 / 6.7 m at 11.8 / 16.6 / 20.8 m.
  5. White bell-dome finial, total 23.5 m; red valances on all three eaves; Nandi at the rear (west).

### T5 - Small white shikhara at Maju Dega's foot

* **OSM:** `w1028854863` (2.7 × 2.6 m) with ≈ 20 white 3D parts stepping to 5.9 m and pinnacle parts [O]; POI
  `n3499843086` place_of_worship [O]. Name not confirmed (a Shiva shrine) [V].
* **Position:** **(-113.7, -37.4)** = 27.704162 N, 85.306347 E: 7 m south-east of the foot of Maju Dega's east stair.
* **Form [P: KAT1910 10, c_basantapur_chowk 12-13, c_shivaparvati 14]:** an all-white plastered **shikhara**: a stepped
  base (two plinth steps), a cubical cella with a small door, then a curvilinear tower made of stacked receding tiers
  (OSM parts at 2.0-5.9 m), a white amalaka-like cap and a small gilt pinnacle; total ≈ 7 m [E]. Strong white silhouette
  against the brick plinths.
* **In the game now [G]:** `GENERIC-Shrine` (517 triangles).
* **Must have:** white curvilinear shikhara ≈ 7 m on the OSM footprint.

### T6 - Narayan temple (Dhvaja Joshi Narayan), a ruin since 2015

* **OSM:** `w111984927` "Narayan Mandir" 12.8 × 12.4 m, bearing 21.4° [O]. DANAM KAT1890 (plans and photos 30 May 2026)
  [D].
* **Position:** **(-120.1, -5.4)** = 27.704451 N, 85.306282 E, at the corner where the lane leaves for Pyaphal, west of
  Shiva-Parvati, north of Maju Dega.
* **State [D, S]:** rebuilt and completed in 2015, then **totally destroyed in the 25 Apr 2015 earthquake** before
  handover; legal complications have kept it **unreconstructed (still so in May 2026)**. The stone Narayan image is kept
  at Hanuman Dhoka.
* **What stands [D/P: KAT1890 02-12]:** a **four-stage brick plinth** (DANAM plan: stages 7.5, 6.3, 5.1 m wide; the OSM
  12.8 m outline includes the lowest, partly buried stage and the fenced margin) ≈ 2.5 m high [E], its top heaped with
  **loose bricks, timber fragments and grass/weeds**, its faces broken; a **low fence of wire mesh on thin posts** around
  it; motorbikes parked along the fence; a big black plastic water tank at the NW.
* **Former form (for reference only, do not model):** three roofs, three bays of posts on each side, white plastered
  walls (DANAM; Pruscha 1975 photo).
* **Views:** **S** (KAT1890 02, 11, 12): a grassy brick mound behind the wire fence with Shiva-Parvati's roof on the
  right; **E** (03): rubble on top, bikes in front; **W/NW** (07, 09): the water tank and the lane to Pyaphal.
* **In the game now [G]:** a complete generic two-roof pagoda (`GENERIC-Pagoda tiers=2`).
* **Must have:** **no temple**: a ruined, grassy four-stage brick plinth with rubble, fenced.

### T7 - Shiva-Parvati temple (Nava Yogini, late 18th c., Bahadur Shah)

* **OSM:** `w185882281` 15.8 × 10.9 m, bearing 109.4°, `building:colour #fc8c64`; parts: plinth `w420291393`
  14.7 × 9.8 (0.5-1.2 m), `w420291396` 11.8 × 7.0 (1.2-1.4), ground storey `w420291403` 11.5 × 6.6 (1.2-5.5), cornice band
  `w420291386` 12.1 × 7.2 (5.5-7.0), upper storey `w420291397` 10.8 × 5.9 (5.7-8.0), roof `w420291395` 15.7 × 10.8
  (7-10, hipped 2.6 m), finials `w420292573/74/75` (9-11.3) [O].
* **Position:** **(-99.4, -9.6)** = 27.704413 N, 85.306492 E.
* **Orientation:** front on the long **south-south-west side** (bearing ≈ 199°), toward Maju Dega and the open square
  [O/P]; the game's yaw 190° is right.
* **Form [P: c_shivaparvati 00-15, shiva_parvati_window, KAT1910 10]:**
  * **Two-stage brick plinth** ≈ 1.4 m with a broad central stair on the front, a **pair of stone lions** on the lower
    stage at the stair; a small white domed shrine at the plinth's west corner [P].
  * Ground storey: brick #B5502F, a row of **five carved cusped (multifoil) arches** on the front, the centre one the
    door, the others carved lattice windows; carved wooden pilasters and a deep carved lintel band.
  * Upper storey: a band of carved windows; at the **centre, a wide window where the painted figures of Shiva and
    Parvati look out** side by side (white faces, black hair, gilt crowns and jewellery, colourful garments) - the
    temple's signature.
  * Hipped tile roof (ridge 10 m, eave 7.0 m, eave spread 15.7 × 10.8 m), red valance, **five gilt finials** with gilt
    leaf/flag ornaments in a row on the ridge.
* **State:** survived 2015 with cracks and timber shores (2015 SOAS record); 2023-26 status of the shores [V] - model
  restored, without shores.
* **Views:** **SSW** (front): arches, the couple at the window, lions, stair. **E** (from the bell lane): the short side,
  brick with carved windows. **N**: plain rear. **W**: Narayan ruin 20 m away.
* **In the game now [G]:** `HouseTemple` 23.1 × 20.0 m, 10.4 m: a plain brick box with small windows under a big hipped
  roof (`cur34_shiva_parvati.png`): no arcade, no figures, no two-stage plinth, no lions.
* **Must have:** 15.8 × 10.9 m two-stage plinth with stair and lions on the SSW side; five cusped arches; upper carved
  window band with **the Shiva and Parvati figures at the central window**; hipped roof to 10 m with red valance and five
  gilt finials.

### T8 - Trailokya Mohan Narayan (1680, Vishnu), rebuilt after 2015

* **OSM:** `w1414593331` 13.1 × 12.5 m, bearing 101.1°, `height 0.6`; plinth stages `w111984928` 12.6 × 11.9 (1.2),
  `w1414593332` 11.7 × 11.1 (1.8), `w1414593333` 10.8 × 10.2 (2.4), `w1414593334` 9.9 × 9.4 (2.8); sanctum `w420797427`
  7.0 m (3.0), `w429890385` 6.3 m (3.6); roofs `w1415349762` 12.6 × 11.9 at 8-11.5, `w1416278296` 9.9 × 9.4 at 11-14.5,
  `w1416686188` 6.3 × 6.2 at 14.3-16.5, pinnacle `w429890379` to 15.5+ [O]. DANAM KAT2020 (plans and photos 2023) [D].
* **Position:** **(-120.8, -59.9)** = 27.703958 N, 85.306275 E. Grid ≈ 11°.
* **Orientation:** faces **west** (bearing ≈ 281°): stone stair in the middle of the west face, **Garuda (T9) on the
  stair axis 10.7 m west** [D].
* **Form [D/P: KAT2020 02-16]:** **five brick stages** (outer ≈ 14 m [D] / 13.1 m [O]) to 2.8 m; ground floor an
  ambulatory of carved dark posts (5 per side) round a **white plastered sanctum** with a carved lattice door on each side
  flanked by **black keyhole-shaped blind niches** on white; struts; **first roof** ≈ 12.6 m at ≈ 8 m; second storey
  white plaster with black windows; second roof ≈ 9.9 m at ≈ 11 m; third storey; third roof ≈ 6.3 m at ≈ 14.3 m; **white
  stepped pinnacle** with a gilt tip; total ≈ **18.5 m** [O/E]. Valances on all three eaves.
* **Colours:** plinth brick #A35A3C (on the east side often painted red #B8483A for festivals [P]); walls white #EEEBE3;
  windows, niches black #2A1E18; tiles #9A6049 (new); valances #A9232C.
* **Views:** **W** (Garuda in front, Kasthamandap behind the viewer): the classic; **E** (from the dabali T10): the
  rear with the stair-less east face; **N** (from Maju Dega): three roofs above the Kumari Ghar roofline; **S**: Kumari
  Ghar 20 m behind.
* **In the game now [G]:** `Pagoda` template 15.4 × 14.9 m, 18.5 m, orange brick and dark wood, gilt finial, no white
  walls, Garuda off-axis (`cur34_trailokya_mohan.png`).
* **Must have:** five stages, stair west; white sanctum with black keyhole niches; three roofs with valances; white
  pinnacle; Garuda on the axis.

### T9 - Garuda kneeling before Trailokya Mohan

* **Position:** **≈ (-131.5, -58.2)** = 27.703974 N, 85.306166 E [D] (OSM node `n2084347899` at (-124.8, -53.6) is
  ≈ 8 m off); **faces east** to the temple door.
* **Form [D/P: KAT2020 03, 18, 19, 23]:** a **black-grey stone Garuda kneeling on one knee, hands joined in anjali**,
  **wings spread** wide behind, crowned, ≈ 1.6 m tall, on a **red-brick pedestal ≈ 1.2 m** high capped by a stone slab
  (≈ 1.4 × 1.0 m) carrying an inscription; total ≈ 2.8 m. Stone #4E4B47.
* **In the game now [G]:** a `VahanaGaruda` 6 m from the temple, not on the stair axis.
* **Must have:** kneeling winged Garuda facing east on a brick pedestal, on the west axis.

### T10 - Basantapur Dabali (dance platform east of Trailokya Mohan)

* **Position:** **≈ (-111, -61.5)** = 27.703944 N, 85.306374 E [D/P/V]: the low platform east of Trailokya Mohan,
  between it, Kumari Ghar (south) and Gaddi Baithak (east); a second smaller platform ≈ 6.1 × 4.3 m lies north of it at
  ≈ (-109, -52) [D KAT2020 site plan].
* **Form [P: c_dabli 01-11, KAT2020 14]:** a square raised **stage ≈ 5.8 × 6.3 m, ≈ 0.5-0.6 m high**: brick sides painted
  **red** (#B8483A) with a **grey stone capping** (#8F8B83) and grey corner blocks; a carved stone face block ("Na:ga
  dabli") with an **iron chain** on one face; people sit on its edge. During Indra Jatra the masked dances of Kumari,
  Nyatabhulu Ajima and Naradevi are performed on it, with small red-canopied shrines brought out [P].
* **Name note:** "Basantapur Dabali" is also used loosely for the whole open square (T28); OSM relation `r3498817`
  "Basantapur Dabali" is a heritage site record pointing at Kumari Ghar [O].
* **In the game now [G]:** absent (sandy ground).
* **Must have:** low red-sided, stone-capped square stage east of Trailokya Mohan.

### T11 - Laxmi Narayan Sattal

* **OSM:** `w183559497` "Laxmi Narayan Sattal", shelter, 13.5 × 11.2 m L-shaped outline [O].
* **Position:** **(-145.0, -50.3)** = 27.704045 N, 85.306029 E, between Kasthamandap and Maju Dega.
* **Form [P: c_laxmi_sattal, KAT1910 08]:** two-storey Newar rest house: open arcaded ground floor (posts) used by
  vegetable and flower sellers, brick upper storey with carved windows, tile roof with valance; ≈ 9 m [E][V].
* **In the game now [G]:** plain 4.5 m house box.
* **Must have:** arcaded ground floor open to the square, brick upper storey, tile roof.

### T12 - Taleju (Big) Bell, "Tahgah Gan" (1797, Rana Bahadur Shah)

* **OSM:** `w183563646` "Taleju Bell" 7.9 × 7.4 m, bearing 111.7° [O]. DANAM KAT1840 floor plan **6.19 × 8.28 m** incl.
  the stair [D].
* **Position:** **(-79.2, 8.6)** = 27.704578 N, 85.306697 E, on the west side of the bell lane.
* **Form [D/P: KAT1840 02-18]:** a **white plastered block** ≈ 6.2 × 6.8 m, ≈ **3.5 m** high [E], brick-paved top with a
  low parapet; a stair along its **south-south-west** side (≈ 1 m wide, 13 risers, entered from the ESE end); on top two
  **square buff stone pillars** (≈ 0.8 m square, 3.1 m apart, ≈ 3 m tall, with notched bracket capitals, #A89A86) carry a
  stone beam from which hangs the **bronze bell**: mouth **≈ 1.9 m** across, ≈ 1.8 m tall, dark bronze #6B5D45 with
  inscription bands; a small **hipped tile roof** ≈ 3.5 × 2.5 m with a red valance over the pillars; total ≈ **8.5 m**
  [E]. The pillar axis runs NNE-SSW [D].
* **Views:** **S/SE** (KAT1840 02-04): the white block with the bell framed between the pillars under the red-fringed
  roof; **SW** (07): scaffolding-era photo with the stair; **NW/NE** (09, 10): pillars and roof over the palace wing
  roofs.
* **In the game now [G]:** a plain 4-storey house box, 13.5 m.
* **Must have:** white block with stair, two stone pillars, big bronze bell visible under a small valanced roof.

### T13 - Saraswati phalcha (mid/late 17th c.)

* **OSM:** `w907080044` 34 m² with node `n2083751556` "Saraswati Temple" [O]. DANAM KAT1820 [D].
* **Position:** **(-71.2, 15.0)** = 27.704636 N, 85.306778 E, north of the bell, east side of the bell lane [O/V].
* **Form [D/P: KAT1820 05-17]:** a square open rest platform on a **1.2 m stone plinth**, **eight carved wooden posts**,
  open on three sides and attached to a brick wall on the west, with a small walled sanctum on that wall (four-armed
  Saraswati); tile roof with a metal finial, plain struts; carved beams (ninah) with deities.
* **In the game now [G]:** a one-roof generic pagoda 13 m.
* **Must have:** stone plinth, eight posts, tile roof, wall at the back.

### T14 - Chyasin Dega, the octagonal Krishna temple (1649, Pratap Malla), rebuilt after 2015

* **OSM:** `w907080047` octagon ≈ 12.9 × 12.3 m [O]; node `n2083743575` "Krishna Temple" [O]. DANAM KAT1810 (KVPT
  section and elevation 2002, plans 2023, photos 2023) [D].
* **Position:** **(-63.1, 27.8)** = 27.704751 N, 85.306860 E.
* **Orientation:** door and stair **east-south-east** (bearing ≈ 100°), toward the palace forecourt; two stone pillars
  flank the stair foot, one carrying a small deity statue on a lotus capital (≈ 5 m) [D].
* **Form [D, measured section 17.31 m]:** **octagonal** throughout. Stepped **octagonal brick plinth** (outer ≈ 14.4 m
  across flats, 6-7 steps) to **3.5 m**; an octagonal **colonnade of 24 slender carved wooden columns** (3 per side)
  round an octagonal brick sanctum, 3.5-6.2 m; **first roof** eave **12.7 m** across at **7.2 m**; second storey; second
  roof **9.2 m** at **11.4 m**; third roof **5.7 m** at **14.6 m**; apex 15.8 m; gilt finial to **17.3 m**.
* **Colours [P: KAT1810 04-14]:** brick #A4553D; columns dark #3E2B20 with carved capitals; new tiles #9A6049; valances
  #A9232C with pink folds; gilt finial.
* **Views:** **E** (KAT1810 05, 09): the stair and the two stone pillars, columns behind, three octagonal roofs;
  **N** (04, 14): the plinth's many steps, a brick house attached on the north; **S** (13): from the bell lane, the
  octagonal roofs over the houses; **SE** (08, 12).
* **In the game now [G]:** a one-roof generic square pagoda, 7 m (`GENERIC-Pagoda tiers=1`;
  `current97/vp_forecourt_W.png`).
* **Must have:** octagonal plan everywhere; 3.5 m stepped plinth; 24-column colonnade; three octagonal roofs; 17.3 m;
  stair ESE with the two stone pillars.

### T15 - Big Drums (Nagara, 1800) [shared with palace brief §3.20]

* **OSM:** `w907080046` (-60.9, 37.7), 38 m² [O/V] (position from Lonely Planet "just beyond the Krishna temple" and
  the brick block north of Chyasin Dega in KAT1810's site plan).
* **Form [P: c_bigdrum 00-12]:** a small two-storey **red-ochre brick pavilion** (#C4532E), ≈ 6 × 5 m [E], ≈ 6.5 m to the
  ridge: ground floor plain brick with a small door; upper floor an **open bay ≈ 4 m wide, 2 m high** framed by brick
  pilasters and a carved brick cornice, in which **two huge drums lie on their sides** (cream skins with painted red and
  multicoloured patterns, red rims); hipped tile roof with a **red valance**. Collapsed 2015, rebuilt.
* **In the game now [G]:** a plain 4-storey house, 12.5 m.
* **Must have:** brick drum house with the open upper bay showing two giant drums.

### T16 - Lakshmi Narayan temple

* **OSM:** `w196261745` 5.8 × 5.6 m (tagged "Kal Bhairab Temple") [O]; DANAM KAT1780 (KVPT plans 2004-08) [D].
* **Position:** **(-40.2, 24.8)** = 27.704724 N, 85.307092 E.
* **Form [D]:** two-roof temple **facing west** on a stepped brick plinth ≈ 5.8 m; carved doors and windows; corner
  struts carved as winged horses (kumsala); tile roofs with valances; metal finial; ≈ 9 m [E]. Rebuilt 1941 after 1934,
  restored by KVPT 2007 and after 2015 (VS 2074).
* **In the game now [G]:** this footprint carries the Kal Bhairav hero (wrong).
* **Must have:** small two-roof temple facing west on this footprint.

### T17 - Kal Bhairav (court Bhairav, 17th c.)

* **OSM:** `w908861416` 5.0 × 2.5 m (building=yes) [O]; DANAM KAT1770 (KVPT N, W, S elevations and plan 2004; photos
  Feb 2023) [D].
* **Position:** **(-38.9, 30.9)** = 27.704779 N, 85.307105 E (back slab); **faces north** (bearing ≈ 10°) [D].
* **Form [D, measured north elevation, 8.12 m]:**
  * A stone **shrine wall** 4.8 m wide (grey stone blocks #8C877D, blackened by oil and soot #4A4642), with side wings
    (≈ 1.0 m each, 1.9 m high) holding **bronze bells** and small carved steles; overall 7.2 m wide.
  * The **relief**: six-armed Bhairav standing on a reclining figure, ≈ 3.6 m to the crown, inside an **ogee-arched
    niche 3.9 m wide, 5.2 m high**, the arch rim carved with flames and serpents and a monster face at the top; sun and
    moon discs left and right above.
  * Painted: body **blue-black #22304F**, face with large white eyes and red mouth, **gilt-yellow crown #E0B437** with a
    row of small skulls, a **flame aureole orange-red #D04A1E**, red and gilt ornaments (general depiction, no gore).
  * A small projecting tile eave with a **red valance** at 5.3 m; a cornice platform at 6.1 m; on top **three bell-shaped
    ribbed domes** (stupa-like, dark metal/stone #5A5A55 with gilt tips), the centre one to **8.12 m**, the sides 7.6 m.
  * Front: a low platform with a **pair of stone lions** (painted white with red, ≈ 1.7 m on pedestals) at the NW and NE,
    two pennant staffs, iron railing and offering trays; worshippers in front.
* **Views:** **N** (KAT1770 05, 09, 11): the full face, worshippers, the three domes against Jagannath's roofs and
  the white palace behind; **E/NE** (07, 12): the stone back-wall in profile, the bell wing; **SW** (08): the plain grey
  back of the wall (no carving), Lakshmi Narayan beside it; **W**: the side wing with a bell.
* **In the game now [G]:** `Relief` hero on the Lakshmi Narayan footprint, yaw 270 (west), 4.8 m tall; the real spot
  has a 4-storey generic house (`current97/vp_kalbhairav_N.png`: the view from the north shows that house where the
  relief should be).
* **Must have:** move to `w908861416`, face north; 8.1 m stone wall with the painted six-armed figure in a flame arch;
  three domes on top; lions and bells; hide the generic house.

### T18 - Jagannath temple (1563, Mahendra Malla) and its four corner shrines

* **OSM:** `w169429518` 14.7 × 14.3 m, bearing 100.9° [O]. DANAM KAT1740 (elevation, plan, section 2017-23; photos
  2023) [D].
* **Position:** **(-27.6, 16.2)** = 27.704647 N, 85.307220 E.
* **Orientation:** four identical three-door facades; inscriptions and the copper plate on the **east** door, toward
  the palace gate; principal side **east** [D] (the game's yaw 270 shows the same facade).
* **Form [D, measured east elevation]:**
  * **Platform:** a broad low brick-and-stone platform ≈ 17 × 19 m, **1.4 m** high, with a **miniature two-roof temple at
    each corner** (brick base ≈ 3.0 m square to 1.4 m, a small brick cella with a door, two small tile roofs with deep red
    valances and a gilt finial, top ≈ 6.3 m) [D/P: KAT1740 16-20].
  * Temple plinth: two steps to 2.3 m; walls ≈ 9 m square.
  * **Walls:** exposed brick #A4553D; each face a **tripartite portal**: a central carved door under a **gilt-trimmed
    torana with Garuda**, flanked by two **blind doors shown as pink-plastered cusped arches** (#D9A0A0) in carved
    frames; carved window rosettes and two small hanging lamps.
  * **First roof:** eave ≈ **14 m** across at **7.4 m**, up to ≈ 10.1 m; struts carved (deities above, figures at the
    foot); second storey with carved windows; **second roof** ≈ 8.5 m at 12.3 m; apex 14.1 m; **gilt finial with a
    Vishnu wheel (chakra)** to **16.4 m**.
  * Valances crimson on both eaves; tiles #8E5B48.
* **Views:** **W** (from Kal Bhairav): the pillar of Pratap Malla rises behind the temple's east side; **E** (from the
  gate): the inscribed facade with the column in front; **S**: Sweta Bhairav screen and Degu Taleju beyond; **N**:
  Indrapur and Gopinath.
* **In the game now [G]:** `Pagoda` 17.3 × 17.0 m, 15.5 m, 3 equal brick steps, colonnade of posts, no corner shrines,
  no tripartite doors (`cur34_jagannath.png`).
* **Must have:** 1.4 m platform with **four corner miniature temples**; brick walls with tripartite portals (pink blind
  arches); two roofs 14 / 8.5 m at 7.4 / 12.3 m; gilt finial with wheel; 16.4 m.

### T19 - Pratap Malla's column (1670)

* **Position:** **≈ (-19.8, 14.7)** = 27.704633 N, 85.307299 E [D: KAT1740 site plan "4 Stambha", 7.8 m east of
  Jagannath's centre], between Jagannath and Degu Taleju; the statue faces the palace (east).
* **Form [D/P: KAT1791 00-11]:** a tall **square-to-octagonal stone shaft** (#B7B0A2, ≈ 0.6 m) on a stepped base, ≈ 7 m,
  with a **dark bronze lotus capital** (bulging, #4F473D); on it the **gilt bronze group of King Pratap Malla seated with
  his queens and sons**, hands joined, under a **rearing serpent hood / canopy** on a stem; total ≈ 9 m [E].
* **In the game now [G]:** missing.
* **Must have:** slender column with lotus capital and gilt seated group under the serpent canopy, east of Jagannath.

### T20 - Indrapur temple

* **OSM:** `w907080039` ≈ 8.6 × 8.0 m (building=yes) [O]; DANAM KAT1760 (plan on KAT1740/1780 sheets) [D].
* **Position:** **(-28.7, 34.3)** = 27.704810 N, 85.307209 E.
* **Form [D/P: c_indrapur 04, 14, c_kageshwor 11]:** stepped brick plinth (3 stages, outer ≈ 7.5-8.5 m) with a ring of
  posts on the top stage round a small sanctum; a **very broad, low first roof** reaching nearly to the plinth edge with a
  red valance, then a **small square upper storey** with carved windows and a **small second roof**; gilt finial; ≈ 11 m
  [E]. Its squat, wide-brimmed silhouette next to the slender Gopinath is the cue.
* **Views:** **S** (c_indrapur 14, c_kageshwor 11): from Jagannath's platform, Indrapur's wide low roof between
  Jagannath's corner shrine and Gopinath; **SW** (c_indrapur 04): beside the slender three-roof Gopinath; **N**: from the
  north field, Kal Bhairav's domes to its right (west).
* **In the game now [G]:** a plain 4-storey house, 13.5 m.
* **Must have:** squat two-roof temple, first roof very broad and low, on a stepped plinth.

### T21 - Gopinath (Shri Krishna Mahavishnu, DANAM KAT1750)

* **OSM:** `w169429514` "Gopinath Temple" 10.4 × 10.2 m [O].
* **Position:** **(-19.0, 32.0)** = 27.704789 N, 85.307307 E.
* **Form [D/P: c_indrapur 04, c_gopinath 00-15]:** **three-roof** temple on a stepped brick plinth with a colonnade
  round the sanctum, roofs with valances, gilt finial, ≈ 16 m [E]; struts carved.
* **Views:** **S/SW** (c_indrapur 04, c_kageshwor 11): the tallest roofs of the forecourt group, behind Jagannath;
  **W**: next to Indrapur; **N**: from Kageshwor.
* **In the game now [G]:** generic two-roof pagoda.
* **Must have:** three roofs, slender, on a 3-step plinth.

### T22 - Kotilingeshwar Mahadev (1566, Mahendra Malla)

* **OSM:** not mapped. DANAM KAT1530 (plans 2023, photos Feb 2023) [D].
* **Position:** **≈ (-29.5, 63.5)** = 27.705074 N, 85.307201 E [D: KAT1520 site plan, SW of Mahavishnu] (±3 m).
* **Orientation:** axes at bearing ≈ 20°; main door **south-south-west**; Nandi (two) on the west [D].
* **Form [D/P: KAT1530 02-11]:** a **grey stone shrine** on a **three-stage cruciform stone plinth 10.73 × 10.73 m**
  with recessed corners (pairs of composite-headed oxen carved on the corner stones); the cella ≈ 6.7 m with **porches
  on all four sides**, each an arched opening with a **dark red wooden grille window/door**; a **red cloth valance**
  under the stone cornice; a large **hemispherical stone dome** (gumbaz) with a small finial over the centre and small
  domes over the porches; ≈ 10 m [E]. A big pipal tree behind (north), a low wooden railing round the plinth.
* **Views:** **S** (KAT1530 03): front porch and steps, the dome over the cornice valance, Mahavishnu's three roofs at
  the right; **SW** (02): stepped plinth corners; **E/SE** (06, 07): the porch with the grille, the big tree behind;
  **W** (05): the Nandi in front of the west porch.
* **In the game now [G]:** missing (open ground).
* **Must have:** grey stone cruciform domed shrine on a three-stage stepped plinth; valance; dome.

### T23 - Kageshwor (Kakeshwar) Mahadev (1681, rebuilt 20th c.)

* **OSM:** `w169429525` "Kageshwor Temple" 11.0 × 10.8 m, roof `min_height 4`, `height 8.2`, pyramidal; white parts
  `w1414590540` 7.6 m (0-6 m), `w1414590541` dome 4.0 m (3.5-12, roof 6), `w1414590542` 2.9 m (to 14) [O].
* **Position:** **(-7.3, 53.2)** = 27.704981 N, 85.307426 E.
* **Form [P: c_kageshwor 00-15, incl. "Kageshwor Temple (2026)"]:** a hybrid: the lower part is a Newar pagoda storey -
  low brick plinth (1-2 steps), an **ambulatory of wooden posts** round a brick sanctum, and **one broad hipped tile roof**
  (eave ≈ 11 m at ≈ 4 m, red valance); out of that roof rises a **white plastered cubic storey with a small balustrade**
  and a tall **white curvilinear shikhara** (ribbed) with a gilt finial; ≈ 14-15 m [O/E]. White against a big tree.
* **Views:** **SW/W** (c_kageshwor 00, 05, 12, 13): broad tile roof on posts, the white shikhara rising behind its
  ridge; **S** (10): the shikhara over the roof with Taleju's roofs to the right; **N**: the big tree behind.
* **In the game now [G]:** generic two-roof pagoda starting at 4 m.
* **Must have:** one broad tile roof over a post ambulatory, then a white cube and a white shikhara to ≈ 14.5 m.

### T24 - Mahavishnu temple (restored to its pre-1934 three-roof form, 2023)

* **OSM:** `w1415081497` 12.3 × 11.6 m (building=yes) [O]; DANAM KAT1520 (KVPT plans, elevation and section 2003;
  photos Mar 2023) [D].
* **Position:** **(-15.5, 72.2)** = 27.705153 N, 85.307343 E.
* **Form [D, measured section 13.60 m above plinth]:** three-stage brick plinth **2.2 m**, ≈ 11 m at the base; ground
  floor an ambulatory of carved posts round a brick sanctum 6.3 m wide; **first roof** eave **10.3 m** at **6.1 m**;
  **second roof** 7.6 m at 9.5 m; **third roof** 4.8 m at 12.4 m; apex 13.5 m; gilt finial to **15.8 m**. Corner struts
  carved as winged horses, others plain; tiles, red valances; brick #A4553D [P: KAT1520 05-16].
* **Orientation:** main entrance **south** [D/P].
* **Views:** **S** (KAT1520 05, 06, 11, 16): stepped plinth and stair, three roofs; **E** (09, 10): with the broad
  open ground and pigeons; **SW** (14): Kotilingeshwar's dome in front.
* **In the game now [G]:** a plain 5-storey house, 16.5 m.
* **Must have:** three-roof brick temple on a 2.2 m three-stage plinth, 15.8 m.

### T25 - Mahendreshwar (Makhan Pashupati, 1561)

* **OSM:** `w907048006` "temple" ≈ 6.3 × 4.3 m [O/V]; DANAM KAT1510 (plans and photos Jan 2023) [D].
* **Position:** **(12.7, 91.6)** = 27.705328 N, 85.307629 E [O/V], NW of Taleju at Makhan Tole.
* **Form [D/P: KAT1510 02-12]:** a walled compound ≈ 11.4 m square entered on the **south** through a **brick gateway**
  (two tall brick piers with a small roof, bronze lions); inside, a two-roof temple on a single platform ≈ 6.3 m with
  doors on four sides, the main door south under a **brightly painted carved torana**; walls **white plaster** with
  red/yellow bands; **roofs clad in metal sheet** with **gilt edges**; struts and rafters **painted bright blue, red and
  white** with deity figures; gilt pinnacle with a bell; ≈ 11 m [E]. Bells, a Nandi on the west, a trident on the north.
* **Views:** **S/SW** (KAT1510 02, 09): the brick gate with lions and the temple's gilt-edged roofs behind;
  **NW** (08): the two metal roofs over the compound wall, a big tree to the west.
* **In the game now [G]:** `GENERIC-None` (18 triangles).
* **Must have:** white walled compound with brick gate; two metal roofs with gilt edges; painted blue-red struts.

### T26 - Bhagwati temple [shared with palace brief §3.18]

* **Position:** rising from the palace's north-west brick wing `w1415152800` (29.9 × 6.4 m, bearing 36°) at **≈ (-79,
  -16)** = 27.704355 N, 85.306699 E [P/V] ("on the north-west corner of the Gaddi Baithak" [S]); the palace brief's
  (-80, -30) is ≈ 14 m further south.
* **Form [P: c_bhagwati 00-15]:** a **three-roof temple standing on the roof of a long brick range**: the range has
  shopfronts (souvenir and thangka stalls) at street level and two storeys of dark carved windows, a tiled roof; the
  temple's roofs are small, dark (metal/tile) with **gilt edges** and a gilt finial, top ≈ 22 m [E].
* **Must have:** long brick range with shops and carved windows; three-roof temple on its roof.

### T27 - Sweta (Seto) Bhairav mask [shared with palace brief §3.4]

* **Position:** on the palace front (Degu Taleju base building's west wall) **≈ (-9, 15)** [palace brief, V].
* **Form [P: c_sweta 08, c_sweta_facade 06, 13]:** a **carved wooden lattice screen** (three latticed panels ≈ 4 m wide,
  3 m high) in a brick facade with carved windows, two brass hanging lamps; behind it the **giant gilt mask** (≈ 3 m):
  gilt crown with small skull ornaments and serpents, three eyes, a red-and-white painted face with fangs (general
  depiction); shown only during Indra Jatra, when the screen opens and a red valance hangs above.
* **Must have:** closed lattice screen with lamps (default); optional festival state with the mask visible.

### T28 - Basantapur square (and its stall platform)

* **OSM:** `w218440319` amenity=marketplace, **85.4 × 23.8 m** (corners (-82.9, -76.8), (-4.4, -92.9), (-3.7, -115.8),
  (-86.2, -95.9)), bearing 101.6° [O].
* **Form [P: c_basantapur_chowk 00-15, c_dabli 00, 05, c_basantapur]:** a wide open square south of the palace, paved in
  **buff-pink brick** (#B88E70) with grey stone borders; a slightly raised platform (kerb 0.2-0.3 m) holds the **curio
  market**: **rows of low wooden tables and ground mats** (≈ 1.5 × 0.8 m) set in a grid, laden with bronze figures,
  singing bowls, masks and jewellery, with **red tarpaulins** and **patio umbrellas** (white, blue, purple), vendors on
  stools; street lamps on tall poles; pigeons. Edges: Gaddi Baithak (white, NW), the palace's red-brick south facade and
  nine-storey tower (N), Kumari Ghar (W), white-and-black Rana-era fronts with arched windows and shops (S, Freak Street
  side), Basantapur Plaza (SE).
* **Views:** **from the nine-storey tower, looking SW** (c_basantapur_chowk 00, 01, c_dabli 05): stall rows filling the
  platform, Freak Street at the far end; **from the E end looking W** (c_basantapur_chowk 04-10): Gaddi Baithak white on
  the right, Kumari Ghar and the temple roofs beyond; **from the W looking E**: the tower and Lohan Chowk's red facade
  on the left.
* **In the game now [G]:** sandy beige ground with brown road ribbons; no stalls (`current97/vp_basantapur_W.png`).
* **Must have:** brick paving with grey borders; stall rows inside the OSM marketplace outline; no road ribbons.

### T30, T31 - Two small Shiva temples at Kasthamandap

* `w196809462` "Shiva Temple" **(-176.6, -44.5)** 8.2 × 8.1 m: a stepped plinth (3 stages, ≈ 9.5 m outer [D KAT1970
  plan]) north of Kasthamandap with a small shrine; `w196809346` "Shiva Temple" **(-187.9, -73.6)** 6.5 × 5.4 m west of
  Kasthamandap [O]. Small one- or two-roof shrines with valances [P: c_kastha 14]; form [V].
* **In the game now [G]:** generic two-roof pagodas (acceptable placeholders; scale them to ≈ 6-8 m).

---

## 4. Small site elements

| Element | Where | Notes |
|---|---|---|
| Kasthamandap lions | E entrance (T1) | Pale grey stone, ≈ 1.3 m on 0.5 m pedestals. |
| Kasthamandap picket fence | Round T1 | ≈ 1.0 m pointed stakes, weathered wood. |
| Shiva-Parvati lions | Front stair (T7) | Grey stone, on the lower stage. |
| Maju Dega Nandi | West foot of T4 | Grey stone, reclining, ≈ 1.6 m long. |
| Garuda | T9 | §3. |
| Silyan winged lions | Corners of T2 | Gilt metal, ≈ 1 m. |
| Kal Bhairav lions, bells | T17 | White-red painted lions; bronze bells on the side wings. |
| Chyasin stone pillars | Stair foot of T14 | ≈ 5 m, one with a small statue on a lotus capital. |
| Pratap Malla column | T19 | §3. |
| Jagannath corner temples (4) | T18 | §3. |
| Narayan ruin fence | T6 | Wire mesh on thin posts, ≈ 1.2 m. |
| Ashok Binayak cage | T3 | Brass bars ≈ 2.8 m with bells. |
| Stall tables, mats, umbrellas | T28 | Grid in the marketplace outline. |
| Pigeon flocks | Jagannath platform, Maju steps, Basantapur | Fauna package. |
| Lamp posts | Basantapur, bell lane | Tall dark poles with lantern heads; some solar panels on posts [P]. |
| Motorbikes | Along the Narayan ruin fence and the bell lane edges | Parked perpendicular, playtest #3 rules. |

---

## 5. Recommended build split

1. **Shared temple kit (bespoke parts, not the old template):** stepped plinth (square, octagonal, cruciform), post
   ambulatory, sanctum with tripartite portals, strut ring, jhingati hip roof with crimson valance, finial types (gilt
   gajur, white bell-dome, white stepped pinnacle, dark bronze), white-plaster wall with black window pattern.
2. **Bespoke heroes** composed from the kit with the measured numbers of §3: Kasthamandap (T1), Maju Dega (T4),
   Trailokya Mohan + Garuda (T8, T9), Shiva-Parvati (T7), Jagannath + 4 shrines + column (T18, T19), Chyasin Dega (T14),
   Kal Bhairav (T17), Kageshwor (T23), Mahavishnu (T24), Kotilingeshwar (T22).
3. **Small bespoke pieces:** Silyan Sattal, Ashok Binayak, Big Bell, Big Drums, Lakshmi Narayan, Indrapur, Gopinath,
   Mahendreshwar, Saraswati phalcha, the white shikhara, the dabali, the Narayan ruin.
4. **Hide** the generic or house-grammar buildings on every footprint of §2.5 (`w908861416`, `w907080039`,
   `w1415081497`, `w907080046`, `w183563646`, `w183559499`, `w183559497`, `w907080044`, `w907080047`, `w111984927`,
   `w1028854863`, `w169429514`, `w169429525`, `w907048006`) and move the Kal Bhairav hero to `w908861416`. Draw
   `w908861401` (6.6 × 5.0 m, `height=0.5`, at the foot of Maju Dega's stair) as a 0.5 m stone platform, not a house.
5. **Ground:** pave the pedestrian square (`r21291455`) with flags and Basantapur with brick; remove road ribbons inside
   it; stalls in `w218440319`.

---

## 6. Photo-matched viewpoints (for render-vs-photo checks)

Unity coordinates = (528.07 + x, ground + 1.6, 288.42 + y).

| # | Camera (local) | Look at | Match to |
|---|---|---|---|
| K1 | (-144, -66) | Kasthamandap E front (-170, -62) | DANAM KAT1970 04, 09 (`current97/vp_kastha_E.png`) |
| K2 | (-172, -88) | Kasthamandap S | KAT1970 12, c_kastha |
| K3 | (-103, -49) | Maju Dega from SE (-131, -26) | KAT1910 12; (`current97/vp_maju_SE.png`) |
| K4 | (-30, -100) | WNW across Basantapur to Gaddi Baithak, Trailokya and Maju | c_basantapur_chowk 04-12 (`vp_basantapur_W.png`) |
| K5 | (-140, -58) | Trailokya from W behind Garuda | KAT2020 03, 06 (`vp_trailokya_W.png`) |
| K6 | (-39, 48) | Kal Bhairav from N (-39, 30) | KAT1770 05, 11 (`vp_kalbhairav_N.png`) |
| K7 | (-80, 19) | E over the forecourt to Jagannath | KAT1740 04, 14 (`vp_forecourt_W.png`) |
| K8 | (-75, -9) | N up the bell lane to the bell, Saraswati and Chyasin (69, 34) | KAT1840 07, KAT1810 13 (`vp_bell_lane_N.png`) |
| K9 | (-103, -32) | Shiva-Parvati front from SSW | c_shivaparvati 02, 06 (`vp_shivaparvati_S.png`) |
| K10 | (-30, 45) | S from the north field to Indrapur, Gopinath, Jagannath | c_indrapur 14, c_kageshwor 11 |

---

## 7. Open questions [V]

1. Big Drums footprint: `w907080046` is inferred (LP text + KAT1810 plan); confirm on a 2024-26 photo with a landmark.
2. Bhagwati: exact position along `w1415152800` (photos put it mid-range ≈ (-79, -16); palace brief (-80, -30)).
3. `w907080042` (-47.2, -3.8), 14.7 × 14.5 m, `roof:shape=pyramidal`: a building at the palace's NW corner; what
   stands there (palace wing or a temple) [V], not in this scope.
4. Saraswati phalcha footprint and orientation (attached to a wall on its west) vs OSM `w907080044`.
5. The name of the white shikhara T5 and the forms of the two Shiva temples T30, T31.
6. Shiva-Parvati: whether 2015 timber shores are still in place in 2026.
7. Heights marked [E]: Trailokya (18.5 m from OSM parts), Silyan (11 m), Indrapur (11 m), Gopinath (16 m),
   Kotilingeshwar (10 m), Mahendreshwar (11 m), Big Bell (8.5 m).
8. Kal Bhairav orientation: DANAM text and plan say north; confirm the game's ground slope does not hide the front
   platform.

---

## 8. Numbers at a glance

| Structure | Position (local) | Footprint | Height | Tiers / stages |
|---|---|---|---|---|
| T1 Kasthamandap | (-169.7, -62.6) | plinth 21.1 m, posts 19.2 m | 18.8 m | 3 roofs: 22.7 / 17.4 / 10.1 m at 4.5 / 10.6 / 15.5 m |
| T2 Silyan Sattal | (-173.2, -95.9) | 13.1 × 7.4 m | ≈ 11 m | pent roof + main hip roof, 4 gilt lions |
| T3 Ashok Binayak | ≈ (-158.6, -47.0) | 2.4 × 2.6 m + cage | ≈ 4.5 m | 1 gilt roof, no finial |
| T4 Maju Dega | (-130.7, -26.1) | 25.0 m base | 23.5 m | 9 stages to 6.6 m; 3 roofs 14.1 / 9.6 / 6.7 m |
| T5 White shikhara | (-113.7, -37.4) | 2.7 m | ≈ 7 m | stepped white shikhara |
| T6 Narayan ruin | (-120.1, -5.4) | 7.5 m (12.8 m fenced) | ≈ 2.5 m | 4 stages, rubble |
| T7 Shiva-Parvati | (-99.4, -9.6) | 15.8 × 10.9 m | 10 m (finials 11.3) | 2-stage plinth, 2 storeys, 1 hip roof |
| T8 Trailokya Mohan | (-120.8, -59.9) | ≈ 13-14 m base | ≈ 18.5 m | 5 stages to 2.8 m; 3 roofs |
| T9 Garuda | ≈ (-131.5, -58.2) | 1.4 × 1.0 m | 2.8 m | — |
| T10 Dabali | ≈ (-111, -61.5) | 5.8 × 6.3 m | 0.55 m | — |
| T12 Big Bell | (-79.2, 8.6) | 6.2 × 8.3 m incl. stair | ≈ 8.5 m | bell Ø 1.9 m |
| T14 Chyasin Dega | (-63.1, 27.8) | octagon 14.4 m | 17.3 m | plinth 3.5 m; 3 octagonal roofs 12.7 / 9.2 / 5.7 m |
| T15 Big Drums | (-60.9, 37.7) [V] | ≈ 6 × 5 m | ≈ 6.5 m | 2 storeys |
| T16 Lakshmi Narayan | (-40.2, 24.8) | 5.8 m | ≈ 9 m | 2 roofs, faces W |
| T17 Kal Bhairav | (-38.9, 30.9) | 4.8 m wall (7.2 with wings) | 8.12 m | faces N |
| T18 Jagannath | (-27.6, 16.2) | platform ≈ 17 × 19 m | 16.4 m | 1.4 m platform; 2 roofs 14 / 8.5 m at 7.4 / 12.3 m |
| T19 Pratap Malla column | ≈ (-19.8, 14.7) | 0.6 m shaft | ≈ 9 m | — |
| T20 Indrapur | (-28.7, 34.3) | ≈ 8 m | ≈ 11 m | 2 roofs |
| T21 Gopinath | (-19.0, 32.0) | 10.4 m | ≈ 16 m | 3 roofs |
| T22 Kotilingeshwar | ≈ (-29.5, 63.5) | 10.73 m cruciform | ≈ 10 m | 3 stages, dome |
| T23 Kageshwor | (-7.3, 53.2) | 11 m | ≈ 14.5 m | 1 tile roof + white shikhara |
| T24 Mahavishnu | (-15.5, 72.2) | ≈ 11 m | 15.8 m | plinth 2.2 m; 3 roofs 10.3 / 7.6 / 4.8 m |
| T25 Mahendreshwar | (12.7, 91.6) [V] | compound 11.4 m, temple 6.3 m | ≈ 11 m | 2 metal roofs |
| T28 Basantapur stalls | (-47.6, -97.3) | 85.4 × 23.8 m | — | — |

---

## 9. Photo and source credits (reference only, not in the repo)

* **DANAM** (Digital Archive of Nepalese Arts and Monuments, Heidelberg Academy of Sciences and Humanities,
  danam.cats.uni-heidelberg.de; images CC BY-SA 4.0 unless stated; Pruscha 1975 and private historical photos "free
  access, no reuse"): drawings by Wolfgang Korn (Kasthamandap elevation/section 2023, Maju Dega elevation 1975/2025),
  Anil, Bijay and Bibek Basukala (site and floor plans 2020-2026), Thomas Schrom (KAT1520/1521/1522/1740/1780/2060
  compilations 2023-25), the Kathmandu Valley Preservation Trust (Mahavishnu 2003, Kal Bhairav 2004, Lakshmi Narayan
  2004-08, Chyasin Dega 2002, Saraswati phalcha 2002, Jagannath 2017 with P. Awal, B. L. Maharjan, R. Dangol,
  A. Tamrakar); photos by Yogesh Budathoki (Kasthamandap, Silyan Sattal, Big Bell, Kal Bhairav, Narayan 2023-2026),
  Prasanna Bajracharya (Maju Dega 2025-26), Pankaj Nakarmi (Trailokya Mohan, Chyasin Dega, Kotilingeshwar,
  Mahendreshwar, Maru Ganesh 2023), Thomas Schrom (Jagannath, Mahavishnu, Pratap Malla column, Saraswati phalcha
  2021-23). Resources: KAT1970 08e2c058-..., KAT1910 084e848a-..., KAT2020 2eb2709a-..., KAT1890 d9f732cb-...,
  KAT1810 56bfc1ff-..., KAT1740 eeb8d34e-..., KAT1770 158d3469-..., KAT1780 05ed8b92-..., KAT1840 5555d2e1-...,
  KAT1990 03be16ed-..., KAT1930 90cef951-..., KAT1530 d799f03e-..., KAT1520 74763c55-..., KAT1510 38540f68-...,
  KAT1791 72fbb5ec-..., KAT1820 75693640-... (full ids in each `captions.txt`).
* **Wikimedia Commons** (author and licence on each file page; folder `c_<topic>/credits.txt` lists the files):
  Kasthamandap_Kathmandu_(2023); Kageshwor_Temple_(2026); Ashok_Binayak_Temple_(2026); Basantapur_Dabali_2024;
  Basantapur_dabli_GP1-5 and Na_ga_Dabli_basantapur_GP (Ganesh Paudel); Basantapur_chwok_GP_(2-15) (Ganesh Paudel);
  Indrapur_Temple_(left)_and_Vishnu_Temple_(right)_at_the_Kathmandu_Durbar_Square-Nepal;
  Kathmandu_Durbar_Square_(17827282625); Divine_Inspiration,_Finding_Peace_in_Kathmandu's_Temples;
  Kathmandu-Kageshwor-04-2007-gje, Kathmandu-Bhagvati-04-2013-gje, Kathmandu-Krishna-02-2007-gje,
  Kathmandu-Narayana-02-2007-gje (Gerd Eichmann, CC BY-SA 4.0); Bhagwati_Temple_–_Kathmandu_-_01;
  Big_Drum_(Naga_Baja)_Hanuman_Dhoka_Palace, Big_Drums_after_2015_earthquake, Durbar-Square-Kathmandu-great-drums;
  Great_mask_of_Sweta_Bhairava_at_Hanuman_Dhoka, Shwet_Bhairabh,_Kathmandu_Durbar_Square1,
  Kathmandu_Darbar_Square_Nepal_20241202_102/108/113; Big_bell_basantapur_GP series; Silyan_Sattal_of_Hanuman-Dhoka_Durbar_Square,
  Singha_sattal_GP; 2014.12.09_Kathmandu_09/16_Durbar_Sq_Shiva-Parvati; Basantapur_Kathmandu_Nepal (Flickr series
  5118951249, 5119551654, 8528240713, 8528263167 ...).
* **Openverse / Flickr** (`<topic>/credits.txt`): Jorge Láscar (CC BY 2.0: Maju Dega, Trailokya, Shiva-Parvati window,
  Indrapur), Prof. Mortel (CC BY-NC-SA 2.0: Shiva-Parvati, Jagannath, Mahendreshwar), Adam Jones (CC BY-SA 2.0:
  Kasthamandap 2008), wonker (CC BY 2.0), Matt-Zimmerman (CC BY 2.0), Michael Cornelius (CC BY-SA 2.0), eriktorner
  (CC BY-NC-SA 2.0), foxtail_1 (CC BY-NC-SA 2.0: Basantapur), Richard Weil (CC BY-SA 2.0), Cheryl Marland (CC BY 2.0:
  drums), Wonderlane (CC BY 2.0: Garuda).

Web sources: [DANAM](https://danam.cats.uni-heidelberg.de/) (resource pages per KAT id);
[Wikipedia: List of monuments in Hanuman Dhoka, Kathmandu](https://en.wikipedia.org/wiki/List_of_monuments_in_Hanuman_Dhoka,_Kathmandu);
[Lonely Planet: Bhagwati Temple](https://www.lonelyplanet.com/nepal/kathmandu/attractions/bhagwati-temple/a/poi-sig/1583591/357144),
[Great Bell](https://www.lonelyplanet.com/nepal/kathmandu/attractions/great-bell/a/poi-sig/386897/357144),
[Great Drums & Kot Square](https://www.nonprod.lonelyplanet.com/points-of-interest/great-drums-kot-square/386899),
[Krishna Temple](https://www-lonelyplanet-com-57e2.imagizer.com/nepal/kathmandu/attractions/krishna-temple/a/poi-sig/386963/357144);
[nepaltraveller: The Shiva-Parvati Temple](https://nepaltraveller.com/sidetrack/the-shiva-parvati-temple-kathmandu);
[SOAS digital: 2015 earthquake record](https://digital.soas.ac.uk/SWAY002268/00001/citation);
[Kathmandu Post: KMC cheque to rebuild Kasthamandap](https://kathmandupost.com/valley/2018/10/14/kmc-hands-rs-50m-cheque-to-rebuild-kasthamandap);
[ShareSansar: Kasthamandap rebuilding nears final phase (2021)](https://www.sharesansar.com/newsdetail/rebuilding-of-kasthamandap-nears-final-phase-to-be-inaugurated-within-a-few-days-2021-11-23);
[China Daily: Basantapur tower handed over (2025)](https://global.chinadaily.com.cn/a/202501/07/WS677d13c9a310f1265a1d97f8.html);
[Onlinekhabar: dabali, the Newa stage](https://english.onlinekhabar.com/dabali-dabu-newa-architecture-ktm.html).
Aerials: Esri World Imagery tiles (reference only). OSM data © OpenStreetMap contributors (ODbL).
