# Roundabouts and chowks of the Kathmandu Valley: what stands in the middle

> W2 detail pass, package **ornaments** (docs/W2_DETAIL_CONTRACT.md §0: "Make roundabouts more detailed. Add statues in the middle of the roundabout wherever applicable. Do an in-depth research on how it looks and try to design it."). This file is the fact base for `Core/Generators/Ornaments/RoundaboutCatalog.cs` and the island decorator. Tags: **[O]** measured in OSM (`pipeline/data/raw/osm/nepal.osm.pbf`, October 2026 extract, pyosmium) or in the valley pack's `JNCT` records; **[S]** a cited source (§7); **[V]** visual recollection of photos, to verify on the ground; **[E]** an estimate or a design choice for the game.

## 1. Summary

1. The valley has only **21 roundabout/circular ways and 10 mini-roundabout nodes** in OSM (vehicles_traffic.md §5.1). The `JNCT` chunk of the valley pack carries **11 rings** (kind Roundabout or Circular), **11 mini roundabouts**, **15 synthetic police islands** and **10 police chowks** without an island [O].
2. **Real centrepieces found** (OSM objects inside or on the island, cross-checked with sources):
   * **Maitighar Mandala** — the mandala monument (OSM `historic=monument` w120106732, ≈ 24.6 m across) in a 55 m island [O]; 32 vajras, 16 lotus petals and 32 garlands in concentric circles, blue ground with black, orange and blue circles, the Ashtamangala at the four corners [S Wikipedia]; stone water spouts and a small stupa [S Kathmandu Post 2014]; a 10 × 7.5 m national flag raised there on Martyrs' Day 2017 [S Onlinekhabar].
   * **Tripureshwor** — **King Tribhuvan's statue** (n1966285198) in the 12.8 m island of the `circular` ring [O]; damaged in April 2015 and refurbished in 2016 [S Republica, Kathmandu Post].
   * **Jamal / Durbar Marg south end** — **King Mahendra's statue** ("Mahendra Shalik", w194744623, a 15.8 m island) in the 25 m roundabout (w550489464) [O]; "stands in the centre of the roundabout at the junction of Durbar Marg with Jamal, maintained by Hotel Del' Annapurna" [S Wikipedia].
   * **New Road west end (Basantapur)** — **Juddha Shumsher's statue** (w494375491, 8.9 m plinth island) in the 20 m tertiary roundabout (w494375479) [O]; "at the westernmost roundabout of the road" [S Wikipedia New Road]. Photos show a **standing** bronze figure on a tall die on a round stepped platform [P ref_ornaments.md §5] (equestrianstatue.org's entry is another statue).
   * **Shahid Gate (Martyrs' Memorial)**, Sundhara — the memorial arch on its round park island (w193705373, 30.5 m equal-area Ø, 30.3-30.9 m across in every direction) at the south end of Tundikhel [O]; designed by Shankar Nath Rimal, inaugurated 13 April 1961; **King Tribhuvan's bust in the lantern on top of the arch, the four martyrs (Shukraraj Shastri, Dharmabhakta Mathema, Gangalal Shrestha, Dashrath Chand) in the side pavilions** [S Wikipedia, P]; a grey-beige ashlar stone arch with a pointed span and splayed legs [P ref_ornaments.md §1]. A 2012 cabinet decision to move Tribhuvan's statue was stopped by the Supreme Court [S].
   * **Narayan Gopal Chowk**, Maharajgunj (Ring Road) — the singer's statue (n4113142690 `historic=monument` at the chowk node) [O]; "his statue stands at the crossroads" [S]; police chowk, 2-4 officers [O/S].
   * **Jawalakhel** — a monument at the centre of the 36 m grass island (n4112383204) [O]: **King Birendra's statue** (Commons category *Statue of Birendra Bir Bikram Shah, Patan* [S]) in green verdigris bronze on a tall concave-sided stone spire rising out of a round fountain basin [P ref_ornaments.md §6].
   * **Singha Durbar main gate** — **Prithvi Narayan Shah's statue** (n2088245265, `artwork_type=statue`) on a 6.2 m traffic island (w691998242) [O].
   * **Kalimati** — **poet Chittadhar Hridaya's statue** (`historic=memorial` n4371577109) on the traffic island between Ganeshman Singh Path and the two one-way Tankeshwor roads [O]; by sculptor Bal Krishna Tuladhar [S Kathmandu Post 2015].
   * **Lagankhel Chowk** — a **big old shade tree on a red-brick chautari** (OSM `natural=tree` n6077222968) with the Langeshwor Mahadev shrine beside it, in the paved chowk inside the one-way loop of the Satdobato road, 20 m SSE of the chowk node [O] [P cf_lagankhel_chowk 00-01].
   * **TIA approach roundabout** (Gaushala-Sinamangal airport road) — **no monument**: the island is a lawn ring (`landuse=grass` multipolygon r4572868, outer ring 22 m and inner ring 10 m from the centre) round an unmapped Ø 20 m centre [O]. *Correction*: the first pass read the 8 m polygon w340948572 as a tower inside the island; its tags are `aeroway=tower`, `tower:type=air_traffic_control`, `service=aircraft_control`, `building:part=yes`, and its centroid is 56.5 m from the ring centre while the ring centreline is only 25.5-27.7 m away (w592151332, w592151333, w327215591): it is the airport's control tower, outside the roundabout.
3. Everywhere else the island is **a garden**: lawn, a ring of marigolds or roses, a clipped hedge, a low painted railing, and at police chowks the **traffic police post** (the white drum with a box sign, or the older podium under a big umbrella). The game never adds a fountain, flag pole or tower that is not mapped there. Kathmandu Metropolitan City counted "approximately 25 traffic islands" kept as small green spaces [S Himalayan Times 2017]. **No statue or shrine is ever invented** for a generic island (W2_DESIGN 4.7, O2).
4. **Respect rule** [E, binding for the generators]: statues are **generic likenesses** — the recognisable pose, attire and finish (bronze, gilt or black stone), smooth cartoon forms, never a portrait, a caricature or a comic face. Statues are single-colour metal or stone, so faces carry no painted features. Religious figures (Buddha at Budhanilkantha, Bhadrakali temple island) are left to the Sacred generators.

## 2. Inventory: roundabouts and major chowks

Coordinates are WGS84 (lon, lat) of the junction node or the ring centre; game coordinates follow from `WorldFrame.LonLatToGame` (Tm84). Ring Ø = mapped centreline diameter of the circulating carriageway; island Ø = the raised island (JNCT `island_diameter_cm` or the island polygon's equal-area diameter).

| # | Place | lon, lat | Kind (JNCT) | Ring Ø / island Ø (m) | What stands in the middle | Tags |
|---|---|---|---|---|---|---|
| 1 | **Maitighar Mandala** | 85.32043, 27.69454 | Circular, 8 arms, police 2-4 | 68.3 / 54.9 | Mandala monument (a 21 m stepped square, its centroid 9.3 m WSW of the ring centre, with concentric rings, corner emblems, stone spouts, small central stupa); lawns, flower beds, paths; big national flag (2017) | [O] [S] |
| 2 | **Tripureshwor** (Tribhuvan Chowk) | 85.31412, 27.69380 | Circular, 8 arms, police 2-4 | 32.3 / 12.8 | King Tribhuvan statue on a plinth | [O] [S] |
| 3 | **Jamal / Durbar Marg south** | 85.31732, 27.70997 | Roundabout, 5 arms | 25.1 / 15.8 | King Mahendra statue (Mahendra Shalik) | [O] [S] |
| 4 | **New Road west** (Basantapur) | 85.30914, 27.70365 | Roundabout, 4 arms, `width=5` | 20.2 / 8.9 | Juddha Shumsher, standing, on a tall die on round steps | [O] [S] [P] |
| 5 | **Shahid Gate** (Sundhara / Bhadrakali) | 85.31497, 27.69959 | no JNCT record (round park island) | — / 30.5 | Martyrs' Memorial: stone ogive arch springing from two pavilions, Tribhuvan's bust in the lantern on top, martyrs' busts in the pavilions | [O] [S] [P] |
| 6 | **Narayan Gopal Chowk** | 85.33712, 27.74000 | Synthetic island, police 2-4 | — / 12 [E] | Narayan Gopal statue | [O] [S] [V] |
| 7 | **Jawalakhel** | 85.31358, 27.67291 | Roundabout, 8 arms, police | 43.8 / 35.9 | King Birendra (verdigris bronze) on a concave stone spire in a round fountain basin; big lawn | [O] [S] [P] |
| 8 | **Singha Durbar main gate** | 85.32160, 27.69846 | traffic island (signals nearby) | — / 6.2 | Prithvi Narayan Shah statue | [O] |
| 9 | **Kalimati** | 85.30045, 27.69859 | no JNCT record (traffic island) | — / ≈ 13 | Chittadhar Hridaya statue | [O] [S] |
| 10 | **TIA approach** (Gaushala-Airport road) | 85.35647, 27.70054 | Roundabout, 8 arms, crossings | 53.7 / 39.7 | Lawn ring (grass multipolygon r4572868, 10-22 m) round an unmapped Ø 20 m centre; no monument (the "tower" is the airport's control tower, 56 m away) | [O] |
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
| 24 | Lagankhel | 85.32264, 27.66708 | Synthetic island, police | — | Big shade tree on a red-brick chautari with the Langeshwor Mahadev shrine (tree n6077222968, 20 m SSE of the node, in the paved loop of the one-way Satdobato road); Lagankhel Ashok Stupa 240 m south (Sacred) | [O] [P] |
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
* **Monument**: OSM polygon w120106732, 22.0 m equal-area Ø, 24.6 m extent: a 21 m square with two right-angle steps at each corner, its sides at 55/145 degrees, its centroid (−8.3 m east, −4.2 m north) 9.3 m WSW of the ring centre [O] (Atlas Obscura's "50 × 50 ft" is the inner square [S]). Concentric circles: **outermost 32 vajras, then 16 lotus petals, innermost 32 garlands**; a blue background with **black, orange and blue circles** (anger, love, compassion); **Ashtamangala** at the four corners of the enclosing square [S Wikipedia]. Photos [P]: a flat square platform with a teal-green border and a pale edge line, coloured corner fields, a big circle of yellow, orange, red, blue and white bands with a lotus ring; a thick **ring of yellow marigolds** round the square; a **colonnade of white columns with green tops** on the road side; a **giant national flag** on a tall pole since 2017; teal-green fence posts with pyramid caps; solar street lights; red-brick planter walls (ref_ornaments.md §2).
* **Game build** [E/P]: the stepped 21 m platform (0.32 m) facing 325 degrees (colonnade towards the north-west kerb, photo c_00), at the mapped offset from the island centre as far as the island the road layout draws allows (the sample pack's ring ways come within 18.7 m of the centre, so there it moves to the middle), with the coloured inlay as raised bands (border, corner fields, five rings, 16 lotus petals, 32 vajra beads, a small central stupa-like dome with a gilt finial), the marigold collar following the steps, lawn with white and marigold beds, paths converging on the mandala, a 26 m flag pole with the national flag, teal posts and rails round the island, 6 solar street lights, trees by the kerb, black-yellow kerb; police drum at the kerb.

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

* Park island "शहीदगेट" w193705373, `historic=memorial`, `memorial=bust`, `leisure=park`: a near-circle, 30.3-30.9 m across in every direction (30.5 m equal-area Ø), centroid within 0.5 m of 85.31497, 27.69959 [O]; named *Nepal Smarak* at the 1961 opening, called Shahid Gate by everyone [S].
* **Monument** [P c_shahid, ref_ornaments.md §1]: one huge pointed stone arch (span ≈ 11 m, crown ≈ 10 m) of beige ashlar with splayed legs; on its crown a lantern chamber with a lancet opening holding **King Tribhuvan's bust**, a flat flared cap and the national flag (≈ 16 m to the mast top); two side pavilions with concave crested gables ending in small up-turned horns, each with a tall lancet niche holding a martyr's bust on both faces; the arch legs spring out of the pavilions and flare outward at the foot; a stepped platform and a broad flight of steps under the arch; a blank dark plaque. The round island has a black iron railing on a low cream wall, white gate posts, hedges, clipped shrubs, flower boxes and lawn [P c_07, c_08, c_16]. Measured on the photos against the people on the steps (c_16) and the 2.6 m plaque: platform top 0.9 m, clear span 9 m, intrados apex 7.4 m above the island, band 1.6 m, lantern chamber 3.3 m with a 6 m cap slab, pavilions 6.6 m wide with peaks at 4.9 m, about 22 m across the pavilions [P/E].

### 3.6 Narayan Gopal Chowk (`narayan_gopal`)

* Junction node 85.33712, 27.74000: Ring Road × Maharajgunj Road × Bansbari Road, 4 lanes, 16-22 m arms [O]; JNCT SyntheticIsland with HAS_POLICE, OFFICERS_2_4, CROSSINGS_MARKED [O]; `historic=monument` n4113142690 at the node [O]. The chowk is named after the singer **Narayan Gopal Guruwacharya (1939-1990)**, "Swar Samrat", who lived nearby; his statue stands at the crossroads [S Wikipedia, Nepal news 2021]; it was taken down for a few months for pipeline works and re-installed on a death anniversary [S].
* **Statue** [V/E]: standing, **right hand on the chest** (a singer's bow), round glasses, dhaka topi, long coat over trousers; dark bronze; round two-step plinth ≈ 2.4 m, white with a dark band. Island Ø 12 m [E]: marigold ring, low railing, police podium at the kerb facing the Ring Road.

### 3.7 Jawalakhel (`jawalakhel`)

* Closed roundabout w171020947 "Jawalakhel", `width=7`, primary; ring Ø 43.8 (perimeter 138 m), island Ø 35.9 [O]; police [O]. Rato Machhindranath's chariot ends its journey at Jawalakhel for **Bhoto Jatra** [S]. Lalitpur Metropolitan City rebuilt Lalitmandap, a resting shelter (falcha) and Jawalahiti beside the chowk in 2025 [S Tourism Info Nepal].
* **Centre** [P c_jawa]: `historic=monument` "Jawalakhel Chowk" n4112383204 [O] is **King Birendra's statue** [S Commons category] in verdigris bronze with a big plume and a sceptre, on a **concave-sided grey stone spire** (≈ 7 m) with a red and yellow collar, rising out of a **round sunken fountain basin** (Ø ≈ 15 m) with a white tubular railing and globe lamps; lawn with sweeping marigold borders, round clipped shrubs, palms and conifers; black-and-white kerb and a low white railing round the island.

### 3.8 Singha Durbar gate (`pn_shah`)

* Traffic island w691998242 (6.2 m) with statue n2088245265 "Prithivi Narayan Shah" [O]. **Statue** [V/E]: the unifier king in the iconic pose with the **right index finger raised**, royal dress (jama) and plumed crown; dark bronze; square plinth 3 m on two steps; no lawn (paved), black railing.

### 3.9 Kalimati (`kalimati`)

* `historic=memorial` n4371577109 "Chittadhar Hridaya Statue" at 85.30045, 27.69859 [O]; by Bal Krishna Tuladhar [S]. It stands on the traffic island between three carriageways: Ganeshman Singh Path (trunk, `width=12`, centreline 12.8 m away) and the two one-way Tankeshwor roads (`width=9`, 11.0 and 12.1 m away), so about 6.5 m of island each way (Ø ≈ 13 m) [O]. No JNCT record: a standalone site, drawn as a 6 m round island fitted between the road corridors.
* **Statue** [E, no open photo of the statue found; his portraits show round glasses, long hair and a beard, a shawl]: the poet standing bareheaded in a long coat with round glasses, a **book in the left hand**, bronze; round white plinth (1.9 m, dark band) on two round steps; marigold bed, black iron railing, black-and-white kerb.

### 3.10 TIA approach roundabout (`airport`)

* Roundabout ways w592151332, w592151333, w327215591 (unclassified, paved; centreline 25.5-27.7 m from the centre) [O]; JNCT ring Ø 53.7, island Ø 39.7, HAS_ISLAND_AREA, CROSSINGS_MARKED [O]. The island is mapped as a lawn ring: the `landuse=grass` multipolygon r4572868 with its outer ring (w327215588) 22 m and its inner ring (w327215589) 10 m from the centre; nothing is mapped in the Ø 20 m centre [O]. *Correction*: w340948572 (8 m, `aeroway=tower`, `tower:type=air_traffic_control`) is the airport's control tower 56.5 m from the ring centre, outside the roundabout; the first pass misread it as a monument on the island and the game built a clock tower there. **Game build** [E]: a garden only (lawn, mixed beds, clipped shrubs, trees by the kerb, four paths, a central raised marigold bed, white railing, 6 solar street lights); what stands in the centre is to verify on the ground (§6).

### 3.11 Lagankhel Chowk (`lagankhel`)

* The chowk node (85.32264, 27.66708) is where the one-way loop of the Satdobato-Lagankhel road (`width=24`) meets the tertiary roads; JNCT synthetic island with police [O]. Inside the loop, 20 m SSE of the node, stands a **big old shade tree** (`natural=tree` n6077222968, 85.32270, 27.66691) on a **red-brick chautari** with a white coping, and beside it the small **Langeshwor Mahadev** shrine (Sacred package) behind a low red-and-white enclosure; the ground round it is paved and open to traffic on all sides [O] [P cf_lagankhel_chowk 00-01].
* **Game build** [P/E]: a standalone site at the tree: a 4.5 m paved island with a stone kerb, the round red-brick platform (0.75 m, white coping, packed-earth top, a step on the facing side), a massive trunk with buttress roots and three limbs, and a broad crown about 14 m across whose underside stays above 5 m (clear of the 4.5 m overhead limit where it reaches over the road). The record at the node keeps a generic police island.

### 3.12 Garden and police chowks (`thapathali`, `kalanki`, `chabahil`, `lainchaur`, `bagbazar`, `satdobato`, `sinamangal`, `balaju`, `koteshwor`)

* Rows 11 and 17-24, 30 of §2 (plus Koteshwor, row 19): nothing is mapped on these islands and no photo shows a statue, fountain or flag there; Kalanki Mai temple and Chabahil's Chandra Binayak stand beside the chowks (Sacred) [O] [P cf_kalanki_chowk, cf_chabahil_chowk]. Catalogue entries pin their look so it is never a dice roll: lawn (Thapathali, Chabahil, Lainchaur, Satdobato, Sinamangal, Balaju, Koteshwor) or a marigold ring (Kalanki, Bagbazar), the traffic police drum, 0-4 solar street lights, and the kerb paint [E: black-and-white on the Ring Road and newer islands, yellow-and-black in the old centre].

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

Generic choice per island (`RoundaboutCatalog.Generic`, seeded by FNV-1a of the junction node): **a garden only**, never a statue, shrine, fountain, flag pole, tree platform or tower (those come from the catalogue, where they are real; JNCT records do not say whether a fountain or flag is mapped on an island, so none is ever guessed). Kerb paint black-white 60 %, yellow-black 40 %; police chowks (HAS_POLICE, Police, synthetic islands) get the post (the drum with the box sign 70 %, the podium with an umbrella 30 %); no island: the post alone at the junction; radius < 3 m: lawn and a clipped shrub; 3 m and up: marigold (60 %) or rose garden, a railing (white arches 50 %, black iron 25 %), a keep-left sign (60 % from 4 m), a raised central marigold bed; 6 m and up: mixed beds with shrubs and palms 40 %; 7 m and up: 4 solar street lights; 12.5 m and up: a ring of up to 6 trees (columnar Ashoka-like and round shade trees) and 4 stone paths.

Placement rules (every island, generic or curated) [E]: the island is the one the road layout draws (§7); the centrepiece registers its plan footprint; then the street furniture claims its spots near the kerb (the police post first, at the bearing nearest 55° off the main arm where it stands clear of the centrepiece, the compact drum without its step where the full one does not fit; the flag pole, lamps and keep-left sign each move along the kerb to a free spot or are left out); then the planting fills what is left: bed and hedge rings leave gaps round the post, lamps, trees and the centrepiece (so they also run in front of and behind Shahid Gate and beside the offset mandala), shrubs and trees keep clear of every footprint, no palm, conifer or tree stands within 40° of the centrepiece's axis (the statue stays in view from the arms it faces), and every crown stays over the island (a shade tree's lobes reach 2.4 m, a palm's fronds 2.3 m), never over a lane below the 4.5 m overhead clearance.

## 5. Budgets and LOD [E]

Roundabout islands are counted like small heroes (W2_DESIGN 3.2 table: "every other hero" 12,000 / 3,000 / 800 / 200; the island adds its kerb, garden and furniture). Caps per island, enforced by `core-tests/GeneratorsOrnamentTests.cs` (`RoundaboutDecorator.HeroBudget` / `GenericBudget`):

| Design | LOD0 | LOD1 | LOD2 | LOD3 |
|---|---|---|---|---|
| Hero centrepiece (Mandala, Shahid Gate, statue islands) | 16,000 | 4,000 | 1,000 | 200 |
| Generic garden island | 8,000 | 2,000 | 500 | 100 |

Measured (synthetic site, `budgets.txt` of the preview dump, LOD 0 / 1 / 2 / 3): Maitighar 13.8k / 3.8k / 683 / 136; Jawalakhel 13.7k / 3.2k / 794 / 84; Shahid Gate 12.1k / 2.8k / 880 / 177; Durbar Marg 9.2k / 2.6k / 564 / 69; airport approach 8.4k / 1.7k / 354 / 42; Tripureshwor 7.7k / 2.9k / 540 / 69; Narayan Gopal 6.3k / 2.0k / 512 / 69; Kalimati 5.9k / 1.7k / 412 / 69; New Road 5.9k / 2.5k / 504 / 69; Singha Durbar 5.0k / 2.2k / 456 / 69; Lagankhel 3.4k / 446 / 253 / 107; the garden chowks 2.9k-4.8k / ≤ 1.0k / ≤ 244 / 30; generic islands up to 7.2k / 2.0k / 0.5k / 60.

LOD0 ≤ 60 m, LOD1 ≤ 150 m, LOD2 ≤ 400 m, LOD3 beyond (screen-height driven by the integration package's hero LOD allocator). LOD1 drops the flower heads, garland loops, railing pickets and close-up props and turns the beds into low coloured rings; LOD2 keeps the silhouette (platform, pedestal, figure as a few smooth solids, the gate's ribs and blocks, the mandala's main bands, lawn and kerb, lamps on heroes); LOD3 is the kerb, a lawn disc and the centrepiece as two or three blocks (the Maitighar flag stays: it is the landmark).

## 6. Open questions to verify on the ground

* Narayan Gopal's statue: hand position and plinth shape (photos needed).
* Chittadhar Hridaya's statue at Kalimati: pose, attire and plinth (no open photo found; the build follows his portraits).
* Shahid Gate: whether each side pavilion holds one or two busts (four martyrs).
* The TIA approach roundabout: what stands in the unmapped Ø 20 m centre of the lawn ring (paving, a bed, a fountain?). Until verified the game draws a garden.
* Lagankhel: the extent of the paved island round the chautari (drawn as a 4.5 m disc at the tree).

## 7. Generators and integration (package ornaments)

Code: `game/Assets/Ghumante/Core/Generators/Ornaments/` (engine-free Core). Tests and preview dumps: `core-tests/GeneratorsOrnamentTests.cs`.

| File | What |
|---|---|
| `OrnamentTypes.cs` | `Centrepiece` (Garden, Fountain, Statue, EquestrianStatue, MemorialArch, Mandala, FlagPole, PolicePodium, ShadeTree), `StatuePose`, `StatueAttire`, `StatueFinish`, `PlinthShape`, `PlatformShape`, `GardenStyle`, `KerbPaint`, `RailingStyle`, `PoliceStyle`; `StatueSpec`, `RoundaboutDesign` (with the centrepiece offset), `RoundaboutSite` (`LayoutIsland`), `OrnamentStats` (drawn radius, skipped items, plan footprints), `OrnamentFootprint`, `OrnamentSeed` (FNV-1a) |
| `RoundaboutCatalog.cs` | the curated entries (table below) with game coordinates from `WorldFrame.LonLatToGame`; `TryMatch(JunctionRecord, TileData)` (OSM node, else nearest island-capable record within 45 m whose name matches, one island per entry); `Generic(JunctionRecord, radius)` (§4 rules, a garden only); `StandaloneIn(TileData)` |
| `RoundaboutDecorator.cs` | the API: `BuildTile(TileData, IHeightSampler, MeshData, int lod, DecorOptions)`, `Build(in RoadIsland, …)` per layout island, `Build(JunctionRecord, …, islandRadiusM)` per record (0 = the layout's island, negative = explicitly none, positive = the caller's own, never larger than the layout's), `Build(RoundaboutSite, RoundaboutDesign, …)`; `TryLayoutIsland`, `TrySite`, `SiteOf`, `StandaloneSite`, `StandaloneIslands`, `FitToCorridors`, `TryDesign`, `MainArmBearing`, `WidestArm`; budgets |
| `IslandMesher.cs` | painted kerb (bands), cobble apron, domed lawn or paving, beds (marigold, roses, mixed, white) as gap-aware rings, hedges, shrubs, palms, conifers, shade trees, paths converging on the centrepiece, railings (teal posts, white arches, black iron on a cream wall), solar street lights, keep-left sign, traffic police post (drum with box sign, compact drum, or podium with umbrella) |
| `StatueMesher.cs` | stepped platforms (square, round, pots), pedestals (square die with panels and medallion, round, tapered obelisk with plaques, concave spire with collar), figures (poses, attires, cloak, sash, garlands, glasses, dhaka topi, shirpech with quill and bird-of-paradise feathers), busts, equestrian |
| `MonumentMesher.cs` | Shahid Gate (ogive arch springing from the pavilions, lantern with bust and peaked cap, pavilions with concave crested gables, horns and lancet niches with busts, platform, steps, plaque, flag), Maitighar Mandala (stepped outline, inlay bands, corner fields, emblems, petals, vajras, garland beads, stupa, marigold collar, colonnade), Jawalakhel basin with the spire statue, the Lagankhel chautari tree, fountain, flag pole |
| `OrnamentKit.cs`, `OrnamentPalette.cs` | per-thread build context (heights, scratch, footprints taken, free-spot search, road corridor guard), mounds, clumps, stacked lofts, planar ribs, flag of Nepal (strip-cut parallel layers; crescent moon cradling a rayed disc, rayed sun), lamps, pots; colours from the photos |

Every vertex has UV0 = (MaterialChannel, AO): Stone, Grass, Foliage, Metal, Gilt, Paint, Plaster, Water, Fabric, Flagstone, Bark, Dirt, Brick, Glass, Concrete; AO baked per object (`ShapeAo`). Positions are tile-local with absolute Y; heights come from the `IHeightSampler` (absolute game coordinates).

| Entry | Match | Centrepiece | Island |
|---|---|---|---|
| `maitighar` | JNCT n425306923 | Mandala at its mapped offset (9.3 m WSW, facing 325°), 26 m flag pole, teal fence, white garden, trees, paths | the layout's ring island |
| `tripureshwor` | JNCT n103589515 | King Tribhuvan, white marble, garlands, square plinth on 3 steps, pots | the layout's ring island |
| `durbar_marg` | JNCT n313918587 | King Mahendra, bronze, dark glasses, tapered die with plaques, 3 steps | the layout's ring island |
| `new_road` | JNCT n1273136899 | Juddha Shumsher, bronze, plume helmet, die with medallion on round 4-step platform, paved | the layout's ring island |
| `jawalakhel` | JNCT n425307049 | King Birendra, verdigris, sceptre, concave spire in the fountain basin, mixed garden | the layout's ring island |
| `narayan_gopal` | JNCT n1945933074 | Narayan Gopal, bronze, coat and topi, round plinth [E] | the layout's synthetic island |
| `thapathali` … `koteshwor`, `airport` | JNCT node or position and name | garden and police post only (§3.10, §3.12) | the layout's island, or the post alone |
| `shahid_gate` | standalone (round park w193705373) | Shahid Gate facing south, mixed garden, black iron railing | Ø 30.5 m |
| `pn_shah` | standalone (traffic island w691998242) | Prithvi Narayan Shah, pointing up, square plinth | Ø 6.2 m |
| `kalimati` | standalone (n4371577109) | Chittadhar Hridaya, bronze, coat, glasses, book, round plinth [E] | Ø 12 m |
| `lagankhel` | standalone (tree n6077222968) | the chautari tree on red brick, paved | Ø 9 m |

**Islands follow the road layout.** `BuildTile` dresses exactly the islands `RoadLayout.For(t).Islands` draws (a ring island clamped to the ring carriageway's inner edge, a synthetic island shrunk clear of other carriageways or dropped below 6 m), at their `RadiusM` and `ApronM`, plus the police post alone at police chowks where the layout draws no island, plus the standalone curated sites. The JNCT island diameter and the curated radius never enlarge an island (finding: at Maitighar the JNCT 27.4 m island put the railing, lamps and flag pole on the ring lanes that start at 18.7 m). `GeneratorsOrnamentTests.SamplePackIslandsFollowTheRoadLayout` checks every level-10 tile of the sample pack: each decorated radius equals the layout's, junctions without a layout island get the post alone, and nothing below 4.85 m above the ground stands outside a layout island, a post or a standalone island.

**Integration notes and open issues** (for the roads and integration packages; the decorator cannot change their files):

1. **The decorator owns the island base and the police post of every island it dresses** (`DecorOptions.DrawBase` and `DrawPolice` stay on). `JunctionMesher` must then skip its own island disc and podium for those islands, and its podium at police caps without an island (the decorator puts the post there): on `w2d/roads`, `RoadOptions.PolicePodiums = false` covers the podiums; the island disc, kerb bands and planting strip it now draws need a matching switch (for example `RoadOptions.DrawIslands = false`, keeping the ring carriageway and the hole). Switching `DrawBase`/`DrawPolice` off and keeping the road mesher's island instead is **not** consistent: its podium stands at the island centre, inside the statue's platform, the fountain or the central bed, and its interior height (0.30 m on `w2d/roads`) differs from the decorator's lawn (0.22-0.27 m above the road), so beds and pedestals would float or sink.
2. **Standalone sites need their islands cut out**: Shahid Gate, the Singha Durbar statue, Kalimati and Lagankhel's tree are not junction records, so the road layout does not route round them (at Singha Durbar the island sits inside one junction plate that Ram Shah Path and Prithvi Path cross, edges 1.7-2.2 m from the statue). `RoundaboutDecorator.StandaloneIslands(t, list)` lists their mapped islands; the roads package should cut these discs out of its junction plates, corridors and lane graph. Until it does, pass the tile's `IRoadCorridorQuery` (`RoadCorridorIndex.ForTile`) as `DecorOptions.Corridors`: each standalone island then shrinks to the gap between the corridors (less 0.3 m) and is left out (counted in `OrnamentStats.Skipped`) when even its pedestal does not fit; with corridors modelled from the sample pack's road centrelines and widths (the test's fake index), Shahid Gate and Kalimati fit and the Singha Durbar statue does not.
3. Pass the road mesher's options as `DecorOptions.Roads` so the kerb foot matches the road surface lift (`RoadLiftOf` uses `RoadMesher.LiftOf(TopRoad) + JunctionMesher.CapExtraLiftM`).
4. Islands rise ≈ 0.5 m above the terrain: register their tops with the ground query, and pass `DecorOptions.Colliders` for solid boxes round pedestals, the gate, basins, the chautari and posts (`StructureColliders`).
5. Build at the hero LOD bands on a worker thread (allocation-free after warm-up), one mesh per island or per tile.

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
