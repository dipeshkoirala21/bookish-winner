# Roundabouts and chowks of the Kathmandu Valley: what stands in the middle

> W2 detail pass, package **ornaments** (docs/W2_DETAIL_CONTRACT.md §0: "Make roundabouts more detailed. Add statues in the middle of the roundabout wherever applicable. Do an in-depth research on how it looks and try to design it."). This file is the fact base for `Core/Generators/Ornaments/RoundaboutCatalog.cs` and the island decorator. Tags: **[O]** measured in OSM (`pipeline/data/raw/osm/nepal.osm.pbf`, October 2026 extract, pyosmium) or in the valley pack's `JNCT` records; **[S]** a cited source (§7); **[V]** visual recollection of photos, to verify on the ground; **[E]** an estimate or a design choice for the game.

## 1. Summary

1. The valley has only **21 roundabout/circular ways and 10 mini-roundabout nodes** in OSM (vehicles_traffic.md §5.1). The `JNCT` chunk of the valley pack carries **11 rings** (kind Roundabout or Circular), **11 mini roundabouts**, **15 synthetic police islands** and **10 police chowks** without an island [O].
2. **Real centrepieces found** (OSM objects inside or on the island, cross-checked with sources):
   * **Maitighar Mandala** — the mandala monument (OSM `historic=monument` w120106732, ≈ 24.6 m across) in a 55 m island [O]; 32 vajras, 16 lotus petals and 32 garlands in concentric circles, blue ground with black, orange and blue circles, the Ashtamangala at the four corners [S Wikipedia]; stone water spouts and a small stupa [S Kathmandu Post 2014]; a 10 × 7.5 m national flag raised there on Martyrs' Day 2017 [S Onlinekhabar].
   * **Tripureshwor** — **King Tribhuvan's statue** (n1966285198) in the 12.8 m island of the `circular` ring [O]; damaged in April 2015 and refurbished in 2016 [S Republica, Kathmandu Post].
   * **Jamal / Durbar Marg south end** — **King Mahendra's statue** ("Mahendra Shalik", w194744623, a 15.8 m island) in the 25 m roundabout (w550489464) [O]; "stands in the centre of the roundabout at the junction of Durbar Marg with Jamal, maintained by Hotel Del' Annapurna" [S Wikipedia].
   * **New Road west end (Basantapur)** — **Juddha Shumsher's statue** (w494375491, 8.9 m plinth island) in the 20 m tertiary roundabout (w494375479) [O]; "at the westernmost roundabout of the road" [S Wikipedia New Road]. Photos show a **standing** bronze figure on a tall die on a round stepped platform [P ref_ornaments.md §5] (equestrianstatue.org's entry is another statue).
   * **Shahid Gate (Martyrs' Memorial)**, Sundhara — the memorial arch (park polygon w193705373, 30.5 m equal-area Ø) at the south end of Tundikhel [O]; designed by Shankar Nath Rimal, inaugurated 13 April 1961; **King Tribhuvan's bust in the lantern on top of the arch, the four martyrs (Shukraraj Shastri, Dharmabhakta Mathema, Gangalal Shrestha, Dashrath Chand) in the side pavilions** [S Wikipedia, P]; a grey-beige ashlar stone arch with a pointed span and splayed legs [P ref_ornaments.md §1]. A 2012 cabinet decision to move Tribhuvan's statue was stopped by the Supreme Court [S].
   * **Narayan Gopal Chowk**, Maharajgunj (Ring Road) — the singer's statue (n4113142690 `historic=monument` at the chowk node) [O]; "his statue stands at the crossroads" [S]; police chowk, 2-4 officers [O/S].
   * **Jawalakhel** — a monument at the centre of the 36 m grass island (n4112383204) [O]: **King Birendra's statue** (Commons category *Statue of Birendra Bir Bikram Shah, Patan* [S]) in green verdigris bronze on a tall concave-sided stone spire rising out of a round fountain basin [P ref_ornaments.md §6].
   * **Singha Durbar main gate** — **Prithvi Narayan Shah's statue** (n2088245265, `artwork_type=statue`) on a 6.2 m traffic island (w691998242) [O].
   * **Kalimati** — **poet Chittadhar Hridaya's statue** (n4371577109) at the Kalimati roundabout [O]; by sculptor Bal Krishna Tuladhar [S Kathmandu Post 2015].
   * **TIA approach roundabout** (Gaushala-Sinamangal airport road) — a 40 m island with an 8 m `man_made=tower` inside (w340948572) [O].
3. Everywhere else the island is **a garden**: lawn, a ring of marigolds or roses, a clipped hedge, sometimes a fountain or flag pole, a low painted railing, and at police chowks the **white traffic-police podium under a big umbrella**. Kathmandu Metropolitan City counted "approximately 25 traffic islands" kept as small green spaces [S Himalayan Times 2017]. **No statue or shrine is ever invented** for a generic island (W2_DESIGN 4.7, O2).
4. **Respect rule** [E, binding for the generators]: statues are **generic likenesses** — the recognisable pose, attire and finish (bronze, gilt or black stone), smooth cartoon forms, never a portrait, a caricature or a comic face. Statues are single-colour metal or stone, so faces carry no painted features. Religious figures (Buddha at Budhanilkantha, Bhadrakali temple island) are left to the Sacred generators.

## 2. Inventory: roundabouts and major chowks

Coordinates are WGS84 (lon, lat) of the junction node or the ring centre; game coordinates follow from `WorldFrame.LonLatToGame` (Tm84). Ring Ø = mapped centreline diameter of the circulating carriageway; island Ø = the raised island (JNCT `island_diameter_cm` or the island polygon's equal-area diameter).

| # | Place | lon, lat | Kind (JNCT) | Ring Ø / island Ø (m) | What stands in the middle | Tags |
|---|---|---|---|---|---|---|
| 1 | **Maitighar Mandala** | 85.32043, 27.69454 | Circular, 8 arms, police 2-4 | 68.3 / 54.9 | Mandala monument (≈ 24 m square with concentric rings, corner emblems, stone spouts, small central stupa); lawns, flower beds, paths; big national flag (2017) | [O] [S] |
| 2 | **Tripureshwor** (Tribhuvan Chowk) | 85.31412, 27.69380 | Circular, 8 arms, police 2-4 | 32.3 / 12.8 | King Tribhuvan statue on a plinth | [O] [S] |
| 3 | **Jamal / Durbar Marg south** | 85.31732, 27.70997 | Roundabout, 5 arms | 25.1 / 15.8 | King Mahendra statue (Mahendra Shalik) | [O] [S] |
| 4 | **New Road west** (Basantapur) | 85.30914, 27.70365 | Roundabout, 4 arms, `width=5` | 20.2 / 8.9 | Juddha Shumsher, standing, on a tall die on round steps | [O] [S] [P] |
| 5 | **Shahid Gate** (Sundhara / Bhadrakali) | 85.31497, 27.69959 | no JNCT record (park island) | — / 30.5 | Martyrs' Memorial: stone ogive arch, Tribhuvan's bust in the lantern on top, martyrs' busts in the side pavilions | [O] [S] [P] |
| 6 | **Narayan Gopal Chowk** | 85.33712, 27.74000 | Synthetic island, police 2-4 | — / 12 [E] | Narayan Gopal statue | [O] [S] [V] |
| 7 | **Jawalakhel** | 85.31358, 27.67291 | Roundabout, 8 arms, police | 43.8 / 35.9 | King Birendra (verdigris bronze) on a concave stone spire in a round fountain basin; big lawn | [O] [S] [P] |
| 8 | **Singha Durbar main gate** | 85.32160, 27.69846 | traffic island (signals nearby) | — / 6.2 | Prithvi Narayan Shah statue | [O] |
| 9 | **Kalimati** | 85.30045, 27.69859 | no JNCT record | — / 8 [E] | Chittadhar Hridaya statue | [O] [S] |
| 10 | **TIA approach** (Gaushala-Airport road) | 85.35647, 27.70054 | Roundabout, 8 arms, crossings | 53.7 / 39.7 | 8 m tower monument inside a big garden | [O] |
| 11 | **Balaju** (Ring Road × Lekhnath Sadak) | 85.30458, 27.72704 | Roundabout, 9 arms, police | 29.4 / 6.9 | Grass island, police | [O] |
| 12 | Dhumbarahi | 85.33959, 27.72423 | Roundabout, 3 arms | 15.8 / — | Small grass island | [O] |
| 13 | Imadol (Tarnani) | 85.35515, 27.64788 | Circular, 8 arms | 29.2 / — | Grass island | [O] |
| 14 | Gongabu / Tokha side | 85.30771, 27.74439 | Roundabout | 19.2 / — | Grass island | [O] |
| 15 | Budhanilkantha | 85.35986, 27.77418 | Roundabout (service) | 15.3 / 10.3 | A Buddha statue (n5121768772) — religious, left to the Sacred generators | [O] |
| 16 | Swayambhu bus park | 85.28303, 27.71816 | Roundabout (service) | 13.4 / — | Plain island | [O] |
| 17 | Thapathali | 85.31759, 27.69070 | Synthetic island, police 2-4 | — | Garden island + podium; Bagmati bridge to Kupondole | [O] |
| 18 | Kalanki | 85.28153, 27.69329 | Synthetic island, police 2-4 | — | Garden island + podium; underpass below | [O] |
| 19 | Koteshwor | 85.34950, 27.67873 | Signals + police | — | No island in the box; park 23 m beside it; podium | [O] |
| 20 | Chabahil | 85.34674, 27.71723 | Synthetic island, police 2-4 | — | Garden + podium (no Ganesh Man Singh statue found here: his statues are at Mahankal, Boudha and Katunje [S]) | [O] [S] |
| 21 | Lainchaur | 85.31576, 27.71704 | Police 2-4 | — | Grass island ≈ 32 m extent [O]; a water fountain 140 m south (w649876123) | [O] |
| 22 | Ratna Park / Bagbazar | 85.31622, 27.70615 | Synthetic island, police | — | Garden + podium; Ratna Park beside it | [O] |
| 23 | Satdobato | 85.32470, 27.65875 | Synthetic island, police 2-4 | — | Garden + podium | [O] |
| 24 | Lagankhel | 85.32264, 27.66708 | Synthetic island, police | — | Garden + podium; Lagankhel Ashok Stupa 240 m south (Sacred) | [O] |
| 25 | Thamel Chowk | 85.31138, 27.71434 | — | — | No island: a tight crossing of lanes, prayer flags and wires overhead (street dressing, not ornaments) | [O] [V] |
| 26 | Bhadrakali | 85.31674, 27.69932 | — | — | **Bhadrakali temple island** (Sacred package); flag pole (n12625431315) and an unnamed statue (n13240105816) nearby | [O] |
| 27 | Bhrikuti Mandap | 85.31829, 27.70141 | — | — | Exhibition park with a fountain (n3512873896), not a roundabout | [O] |
| 28 | Gaushala | 85.34344, 27.70804 | — | — | Plain crossing, no island found | [O] |
| 29 | New Baneshwor | 85.33560, 27.68837 | Signals + police | — | Chowk without island; a 23.5 m fountain basin (w120105824) by the convention centre 170 m east | [O] |
| 30 | Sinamangal | 85.35494, 27.69523 | Synthetic island, police | — | Garden + podium | [O] |

Other named statues near roads (not on a ring, recorded for later work): Bhanubhakta Acharya (n3577277381, Rani Pokhari wall, 1959 [S]), King Pratap Malla monument and Ganesh Man Singh memorial stone at Rani Pokhari (w348771974, w348771060), Shukraraj Shastri (n2128612807), Arun Thapa bust (n1945544350, "Arun Thapa Chok"), Ghataraj Salik Chok (n4071313801), Birendra Chowk (w206805746), Nijamati Smarak (w574929699), Sankhadhar Sakhwa (n3294328568, n3489694998), Gopi Maharjan (n7765273342), the "Time (Clock) Tower" at 85.30855, 27.73391 (n6212383985), and the Ghantaghar clock tower at Trichandra (w194744621, not on a ring) [O].

## 3. Site notes (pose, plinth, colours, garden)

### 3.1 Maitighar Mandala (`maitighar`)

* **Island**: JNCT ring Ø 68.3, island Ø 54.9 [O]; the ring is five `junction=circular` arcs (w111845390 named Maitighar, `width` 6-8) [O]. Police roundabout with 2-4 officers [O]. Built for the 11th SAARC summit (2001-2002), first called the "Garden of Hope"; the plan wanted **white flowers only**, many colours are planted now; **stone water spouts** are dry; coloured rope lights in blue, red and yellow [S Nepali Times]. Renovated in 2014 for the 18th SAARC summit [S Kathmandu Post]. Long the valley's protest and assembly square [S Wikipedia, Atlas Obscura].
* **Monument**: OSM polygon 22.0 m equal-area Ø, 24.6 m extent [O] (Atlas Obscura's "50 × 50 ft" is the inner square [S]). Concentric circles: **outermost 32 vajras, then 16 lotus petals, innermost 32 garlands**; a blue background with **black, orange and blue circles** (anger, love, compassion); **Ashtamangala** at the four corners of the enclosing square [S Wikipedia]. Photos [P]: a flat square platform with a teal-green border and a pale edge line, coloured corner fields, a big circle of yellow, orange, red, blue and white bands with a lotus ring; a thick **ring of yellow marigolds** round the square; a **colonnade of white columns with green tops** on the road side; a **giant national flag** on a tall pole since 2017; teal-green fence posts with pyramid caps; solar street lights; red-brick planter walls (ref_ornaments.md §2).
* **Game build** [E/P]: a 24 m square platform (0.3 m) set square to the main arm with the coloured inlay as raised bands (border, corner fields, five rings, 16 lotus petals, 32 vajra beads, a small central stupa-like dome with a gilt finial), the marigold ring round it, the colonnade on the main-arm side, lawn with white and marigold beds, a 28 m flag pole with the national flag, teal posts and rails round the island, 6 solar street lights, black-yellow kerb; police post at the kerb.

### 3.2 Tripureshwor (`tripureshwor`)

* JNCT Circular, ring Ø 32.3, island Ø 12.8 (OSM islands 12.9 and 14.3 m) [O]; police 2-4 [O]. Statue n1966285198 "King Tribhuvan Statue" at the ring centre [O]; damaged in 2015 and refurbished in 2016 [S].
* **Statue** [P c_tribhuvan 00-02]: King Tribhuvan standing in **white marble**, long cloak to the ground, plumed crown, hands at the waist; **marigold garlands** round the neck; a square white pedestal with a blank inscription panel on a square three-step grey stone platform with big terracotta pots at the corners. Figure 3.0 m, pedestal 2.0 m, platform 0.9 m [E].
* **Garden**: lawn and hedge in the 12.8 m island, solar street lights, painted kerb [P/E].

### 3.3 Jamal / Durbar Marg south end (`mahendra`)

* Roundabout w550489464, ring Ø 25.1, `width=7`, primary [O]; island polygon "Mahendra Shalik" w194744623, 15.8 m [O]. The White Machhindranath chariot circles this spot three times in its festival [S Wikipedia Durbar Marg].
* **Statue** [P c_mahendra 00-02]: King Mahendra standing in dark bronze, uniform with sash, long cloak, plumed crown with a tall curving plume, **dark glasses**, a hand on the sword; on a **tall tapered cream-pink marble die** with a black bronze plaque, over a wider square base with a black granite plaque in a gilt border, on three broad white steps. One travel guide calls it equestrian [S Holidify/Routard]; the photos show the standing figure.
* **Garden**: roses and seasonal flowers, clipped hedge, trees, lamp posts [E].

### 3.4 New Road west (`juddha`)

* Roundabout ways w494375479 + w1154453949, `width=5`, tertiary, ring ≈ 20 m [O]; plinth island w494375491 "Juddha Shamsher", 8.9 m [O]. New Road was rebuilt after the 1934 earthquake under Prime Minister Juddha Shumsher and first named Juddha Sadak [S].
* **Statue** [P c_juddha 00-01, c_tribhuvan 04-06]: **standing** in bronze, Rana court uniform, long cloak, the tall curved plume helmet, a hand on the sword hilt; a tall pale-grey stone die with blank inscription panels, a cornice and a round crest medallion; a **round four-step platform** with potted plants; paved island, no lawn.

### 3.5 Shahid Gate (`shahid_gate`)

* Park island "शहीदगेट" w193705373, `historic=memorial`, `memorial=bust`, 30.5 m equal-area Ø, at 85.31497, 27.69959 [O]; named *Nepal Smarak* at the 1961 opening, called Shahid Gate by everyone [S].
* **Monument** [P c_shahid, ref_ornaments.md §1]: one huge pointed stone arch (span ≈ 11 m, crown ≈ 10 m) of beige ashlar with splayed legs; on its crown a lantern chamber with a lancet opening holding **King Tribhuvan's bust**, a flat flared cap and the national flag (≈ 16 m to the mast top); two side pavilions with flared crested roofs, each with a pointed niche holding a martyr's bust; a stepped platform and a broad flight of steps under the arch; a blank dark plaque. The island is elongated (≈ 30 × 18 m) with a black iron railing on a low cream wall, white gate posts, hedges and lawn [E from photos].

### 3.6 Narayan Gopal Chowk (`narayan_gopal`)

* Junction node 85.33712, 27.74000: Ring Road × Maharajgunj Road × Bansbari Road, 4 lanes, 16-22 m arms [O]; JNCT SyntheticIsland with HAS_POLICE, OFFICERS_2_4, CROSSINGS_MARKED [O]; `historic=monument` n4113142690 at the node [O]. The chowk is named after the singer **Narayan Gopal Guruwacharya (1939-1990)**, "Swar Samrat", who lived nearby; his statue stands at the crossroads [S Wikipedia, Nepal news 2021]; it was taken down for a few months for pipeline works and re-installed on a death anniversary [S].
* **Statue** [V/E]: standing, **right hand on the chest** (a singer's bow), round glasses, dhaka topi, long coat over trousers; dark bronze; round two-step plinth ≈ 2.4 m, white with a dark band. Island Ø 12 m [E]: marigold ring, low railing, police podium at the kerb facing the Ring Road.

### 3.7 Jawalakhel (`jawalakhel`)

* Closed roundabout w171020947 "Jawalakhel", `width=7`, primary; ring Ø 43.8 (perimeter 138 m), island Ø 35.9 [O]; police [O]. Rato Machhindranath's chariot ends its journey at Jawalakhel for **Bhoto Jatra** [S]. Lalitpur Metropolitan City rebuilt Lalitmandap, a resting shelter (falcha) and Jawalahiti beside the chowk in 2025 [S Tourism Info Nepal].
* **Centre** [P c_jawa]: `historic=monument` "Jawalakhel Chowk" n4112383204 [O] is **King Birendra's statue** [S Commons category] in verdigris bronze with a big plume and a sceptre, on a **concave-sided grey stone spire** (≈ 7 m) with a red and yellow collar, rising out of a **round sunken fountain basin** (Ø ≈ 15 m) with a white tubular railing and globe lamps; lawn with sweeping marigold borders, round clipped shrubs, palms and conifers; black-and-white kerb and a low white railing round the island.

### 3.8 Singha Durbar gate (`pn_shah`)

* Traffic island w691998242 (6.2 m) with statue n2088245265 "Prithivi Narayan Shah" [O]. **Statue** [V/E]: the unifier king in the iconic pose with the **right index finger raised**, royal dress (jama) and plumed crown; dark bronze; square plinth 3 m on two steps; no lawn (paved), black railing.

### 3.9 Kalimati (`kalimati`)

* Statue n4371577109 "Chittadhar Hridaya Statue" at the Kalimati crossing [O]; by Bal Krishna Tuladhar [S]. **Statue** [E]: standing poet, **book in the left hand**, daura suruwal with waistcoat and dhaka topi, bronze; round plinth 2.2 m; island Ø 8 m [E] with marigolds.

### 3.10 TIA approach roundabout (`airport`)

* Roundabout ways w592151332, w592151333, w327215591 (unclassified, paved) [O]; JNCT ring Ø 53.7, island Ø 39.7, HAS_ISLAND_AREA, CROSSINGS_MARKED [O]; a `man_made=tower` polygon of 8 m Ø inside (w340948572) [O]. Game build [E]: a slender square tower monument (clock tower form, 11 m), lawns, mixed beds, a fountain, 8 lamp posts, trees ring.

## 4. Generic islands (everything else)

Measured and estimated parts [E unless noted], used by `RoundaboutCatalog.Generic` and the decorator:

| Part | Size and finish |
|---|---|
| Kerb | 0.20-0.25 m high, 0.25 m wide, rounded top; faces painted in **0.5 m yellow and black bands** (Kathmandu practice [V]) or black and white (IRC:35 §14.2.4 [S], roads.md §10.3) |
| Apron | 1.0 m mountable cobble band inside the kerb on rings (W2_DESIGN 4.7) |
| Lawn | slightly domed (+0.15 m at the centre), mown green; dry patches on old islands |
| Flower beds | 0.6-1.2 m soil rings with a brick edge: **marigolds** (saya patri, orange and yellow, Ø 0.25 m pom-poms, 0.4 m tall), **roses** (red, pink, white on 0.6 m bushes), white flowers at Maitighar [S]; seasonal [V] |
| Hedge | clipped 0.5-0.7 m, 0.5 m wide, dark green (duranta/privet [E]) |
| Railing | 0.6-0.9 m, posts every 1.5-2 m, two rails, painted green, black or white with yellow tips [V] |
| Lamp posts | 4-5 m, black or green, lantern or globe heads; 4-8 per big island [E] |
| Fountain | round basin Ø 4-12 m, rim 0.5 m, one or two tiers, central jet 2-4 m and rim jets; many are dry (show the water anyway, the game is cheerful [E]) |
| Flag pole | white or steel, 8-18 m, national flag (two stacked crimson pennants with a blue border, white moon and sun) |
| Police podium | Ø 1.2-1.5 m, 0.3-0.4 m high, white with a blue band, 2.2 m umbrella (red-white or blue-white panels) (W2_DESIGN 4.7, vehicles_traffic.md §2) |
| Name board | 1.8 × 0.6 m panel on two posts at the kerb facing the main arm (blank colour panel: Devanagari is never machine-made, ADR-005) |

Generic choice per island (`RoundaboutCatalog.Generic`, seeded by FNV-1a of the junction node, never a statue or shrine): kerb paint black-white 60 %, yellow-black 40 %; police chowks (HAS_POLICE, Police, synthetic islands) get the post at the island edge 55° off the main arm (the drum with the box sign 70 %, the podium with an umbrella 30 %), never at the centre; no island (radius < 0.5 m): the post alone at the junction; radius < 3 m: lawn and a clipped shrub; 3 m and up: marigold (60 %) or rose garden, a railing (white arches 50 %, black iron 25 %), a keep-left sign (60 % from 4 m); 6 m and up: a fountain 30 %, a flag pole 15 %, a clock tower 10 %, else a raised central marigold bed, and mixed beds with shrubs and palms 40 %; 7 m and up: 4 solar street lights; 12.5 m and up: a ring of up to 6 trees (columnar Ashoka-like and round shade trees) and 4 stone paths.

## 5. Budgets and LOD [E]

Roundabout islands are counted like small heroes (W2_DESIGN 3.2 table: "every other hero" 12,000 / 3,000 / 800 / 200; the island adds its kerb, garden and furniture). Caps per island, enforced by `core-tests/GeneratorsOrnamentTests.cs` (`RoundaboutDecorator.HeroBudget` / `GenericBudget`):

| Design | LOD0 | LOD1 | LOD2 | LOD3 |
|---|---|---|---|---|
| Hero centrepiece (Mandala, Shahid Gate, statue islands) | 16,000 | 4,000 | 1,000 | 200 |
| Generic garden / fountain / flag / clock island | 8,000 | 2,000 | 500 | 100 |

Measured (synthetic site, `budgets.txt` of the preview dump): Maitighar 13.3k / 3.2k / 0.64k / 90; Jawalakhel 13.2k / 3.2k / 0.79k / 84; Shahid Gate 12.9k / 3.2k / 0.59k / 146; Durbar Marg 8.8k / 2.5k / 0.56k / 69; Tripureshwor 7.2k / 2.9k / 0.54k / 69; generic islands up to 8.0k / 2.0k / 0.5k / 60 (R = 20 m with trees, lamps and railing).

LOD0 ≤ 60 m, LOD1 ≤ 150 m, LOD2 ≤ 400 m, LOD3 beyond (screen-height driven by the integration package's hero LOD allocator). LOD1 drops the flower heads, garland loops, railing pickets and close-up props and turns the beds into low coloured rings; LOD2 keeps the silhouette (platform, pedestal, figure as a few smooth solids, the gate's ribs and blocks, the mandala's main bands, lawn and kerb, lamps on heroes); LOD3 is the kerb, a lawn disc and the centrepiece as two or three blocks (the Maitighar flag stays: it is the landmark).

## 6. Open questions to verify on the ground

* Narayan Gopal's statue: hand position and plinth shape (photos needed).
* Shahid Gate: whether each side pavilion holds one or two busts (four martyrs).
* The TIA approach "tower": what it is (clock, monument or a telecom mast).

## 7. Generators and integration (package ornaments)

Code: `game/Assets/Ghumante/Core/Generators/Ornaments/` (engine-free Core). Tests and preview dumps: `core-tests/GeneratorsOrnamentTests.cs`.

| File | What |
|---|---|
| `OrnamentTypes.cs` | `Centrepiece`, `StatuePose`, `StatueAttire`, `StatueFinish`, `PlinthShape`, `PlatformShape`, `GardenStyle`, `KerbPaint`, `RailingStyle`, `PoliceStyle`, `IslandShape`; `StatueSpec`, `RoundaboutDesign`, `RoundaboutSite`, `OrnamentStats`, `OrnamentSeed` (FNV-1a) |
| `RoundaboutCatalog.cs` | the curated entries (table below) with game coordinates from `WorldFrame.LonLatToGame`; `TryMatch(JunctionRecord, TileData)` (OSM node, else nearest island-capable record within 45 m whose name matches, one island per entry); `Generic(JunctionRecord, radius)` (§4 rules, never a statue); `StandaloneIn(TileData)` |
| `RoundaboutDecorator.cs` | the API: `Build(JunctionRecord j, TileData t, IHeightSampler h, MeshData m, int lod, float islandRadiusM, float mainArmDeg, OrnamentStats, DecorOptions)`, `BuildTile(TileData, IHeightSampler, MeshData, int lod, DecorOptions)`, `Build(RoundaboutSite, RoundaboutDesign, …)`; `TrySite`, `TryDesign`, `MainArmBearing`, `WidestArm`; budgets |
| `IslandMesher.cs` | painted kerb (bands), cobble apron, domed lawn or paving, beds (marigold, roses, mixed, white), hedges, shrubs, palms, conifers, shade trees, paths, railings (teal posts, white arches, black iron on a cream wall), solar street lights, keep-left sign, traffic police post (drum with box sign, or podium with umbrella) |
| `StatueMesher.cs` | stepped platforms (square, round, pots), pedestals (square die with panels and medallion, round, tapered obelisk with plaques, concave spire with collar), figures (poses, attires, cloak, sash, garlands, glasses, dhaka topi, shirpech with quill and bird-of-paradise feathers), busts, equestrian |
| `MonumentMesher.cs` | Shahid Gate (ogive arch, lantern with bust, pavilions with busts, platform, steps, plaque, flag), Maitighar Mandala (inlay bands, corner fields, emblems, petals, vajras, garland beads, stupa, marigold collar, colonnade), Jawalakhel basin (rim, water, railing, globe lamps, jets) with the spire statue, clock tower, fountain, flag pole |
| `OrnamentKit.cs`, `OrnamentPalette.cs` | per-thread build context (heights, scratch), mounds, clumps, stacked lofts, planar ribs, flag of Nepal (strip-cut parallel layers), lamps, pots; colours from the photos |

Every vertex has UV0 = (MaterialChannel, AO): Stone, Grass, Foliage, Metal, Gilt, Paint, Plaster, Water, Fabric, Flagstone, Bark, Dirt, Brick, Glass, Concrete; AO baked per object (`ShapeAo`). Positions are tile-local with absolute Y; heights come from the `IHeightSampler` (absolute game coordinates).

| Entry | JNCT node | Centrepiece | Island |
|---|---|---|---|
| `maitighar` | 425306923 | Mandala, 26 m flag pole, teal fence, white garden | JNCT island Ø 54.9 |
| `tripureshwor` | 103589515 | King Tribhuvan, white marble, garlands, square plinth on 3 steps, pots | Ø 12.8 |
| `durbar_marg` | 313918587 | King Mahendra, bronze, dark glasses, tapered die with plaques, 3 steps | Ø 15.8 (curated) |
| `new_road` | 1273136899 | Juddha Shumsher, bronze, plume helmet, die with medallion on round 4-step platform, paved | Ø 8.9 (curated) |
| `jawalakhel` | 425307049 | King Birendra, verdigris, sceptre, concave spire in the fountain basin, mixed garden | Ø 35.9 |
| `narayan_gopal` | 1945933074 | Narayan Gopal, bronze, coat and topi, round plinth [E] | synthetic, Ø 12.2 (curated) |
| `airport` | 1008825374 | clock tower (the mapped tower) | Ø 39.7 |
| `shahid_gate` | — (standalone, park island) | Shahid Gate, facing south | stadium 33 × 17 m |
| `pn_shah` | — (standalone, traffic island) | Prithvi Narayan Shah, pointing up, square plinth | Ø 6.2 |

Integration notes (open issues for the integration and roads packages):

* Pass the island radius the road layout drew (`RoadLayout.Islands[i].RadiusM`) as `islandRadiusM`, so the kerb matches the ring hole; `DefaultRoadLiftM` matches the primary-road lift, pass `RoadMesher.LiftOf` for an exact kerb foot.
* Either keep `DecorOptions.DrawBase` on and skip `JunctionMesher`'s island disc and podium for decorated islands, or switch `DrawBase`/`DrawPolice` off and keep the road mesher's (the two must not both draw).
* Standalone sites (Shahid Gate, Singha Durbar) assume the roads already go round the mapped island; the island top (≈ 0.5 m above the terrain) needs a ground-query entry, and `DecorOptions.Colliders` gives solid boxes for pedestals, the gate, towers, basins and posts to register with `StructureColliders`.
* Build at the hero LOD bands on a worker thread (allocation-free after warm-up), one mesh per island or per tile.

## 8. Sources

* Photo brief and credits: [ref_ornaments.md](ref_ornaments.md) (Wikimedia Commons and Openverse photos, reference only)
* Wikipedia, *Maitighar Mandala*: https://en.wikipedia.org/wiki/Maitighar_Mandala
* Atlas Obscura, *Maitighar Mandala*: https://www.atlasobscura.com/places/maitighar-mandala
* Nepali Times, *Sight for sore eyes in Kathmandu*: https://nepalitimes.com/sight-for-sore-eyes-in-kathmandu
* Kathmandu Post, *Maitighar decked up for SAARC* (2014): https://kathmandupost.com/valley/2014/10/16/maitighar-decked-up-for-saarc
* Onlinekhabar, *Super big national flag installed on Martyrs' Day* (2017): https://english.onlinekhabar.com/nepal-super-big-national-flag-installed-martyrs-day.html
* Wikipedia, *Shahid Gate*: https://en.wikipedia.org/wiki/Shahid_Gate
* Kathmandu Post, *Man of bronze* (2015, sculptors Ratna Bahadur and Bal Krishna Tuladhar): https://kathmandupost.com/miscellaneous/2015/06/12/man-of-bronze
* Himalayan Times, *Martyrs' Day observed at Shahid Gate*: https://thehimalayantimes.com/kathmandu/martyrs-day-observed-at-shahid-gate
* Wikipedia, *Durbar Marg*: https://en.wikipedia.org/wiki/Durbar_Marg
* Wikipedia, *New Road of Kathmandu*: https://en.wikipedia.org/wiki/New_Road_of_Kathmandu
* Equestrian statue database, *Juddha Shumsher*: https://equestrianstatue.org/shumsher-juddha/
* Republica, renovation of King Tribhuvan's statue at Tripureshwor Chowk: https://myrepublica.nagariknetwork.com/news/renovation-2
* Wikipedia, *Narayan Gopal*: https://en.wikipedia.org/wiki/Narayan_Gopal
* The Nepal Weekly, *Narayan Gopal's statue installed at his birthplace*: https://thenepalweekly.com/narayan-gopals-statue-installed-at-his-birthplace/
* Wikipedia, *Jawalakhel*: https://en.wikipedia.org/wiki/Jawalakhel
* Tourism Info Nepal, *Revamped Lalitmandap in Jawalakhel* (2025): https://tourisminfonepal.com/lalitmandap-in-jawalakhel-becomes-public/
* Wikimedia Commons, *Statues of men in Kathmandu*: https://commons.wikimedia.org/wiki/Category:Statues_of_men_in_Kathmandu
* Himalayan Times, *KMC Mayor Shakya inspects traffic islands* (2017): https://thehimalayantimes.com/kathmandu/kmc-mayor-bidhya-sundar-shakya-inspects-traffic-islands
* OSM extract `pipeline/data/raw/osm/nepal.osm.pbf` (ODbL) and the valley pack `JNCT` chunk (pipeline/ghumante_pipeline/junctions.py).
* Earlier W2 research: docs/research/w2/vehicles_traffic.md §5 (rings and chowks), roads.md §7 and §10.3 (kerbs, island paint).
