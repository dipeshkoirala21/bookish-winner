# Kathmandu Durbar Square, Hanuman Dhoka palace complex: structure-by-structure reference brief

Package `mon-kds-palace` (heritage research, W2 detail pass, monuments round). Owner: "National Heritage and Monuments
sites should look exactly as in real life ... Do same for the Basantapur Durbar Square. Creating a bare minimum
buildings wont cut out ... Monuments are one thing that we cannot absolutely slack off on." This brief is what the
modeller builds the old royal palace (Hanuman Dhoka Durbar) from: the site plan, every structure with numbers, colours
and details, what the game shows today, and a must-have checklist per structure. Cartoon style may simplify surfaces
only. Silhouettes, proportions, arrangement and colours must match the place. The game is rated 9+: carvings are
described by their deities, animals and patterns in general terms only.

Scope: the palace and its parts: the Hanuman Dhoka gate (the palace's "golden gate", gilt door frame in a blue
surround) with the Hanuman statue under its umbrella, the lions and the Narasimha statue inside; the outer facades on
the square and on Basantapur; Degu Taleju with the Seto Bhairav screen; Agamchhen; Nasal Chowk with Panch Mukhi Hanuman;
Mohan, Sundari and Mul Chowks; the Taleju temple on its 12-stage plinth with its gate; the nine-storey Basantapur tower
and Lohan Chowk with the Kirtipur, Bhaktapur and Lalitpur towers as rebuilt after 2015; Gaddi Baithak; the Hnulunche
facade with Bhagwati; Kumari Ghar; the Big Drums and the Kot area. The free-standing temples of the square (Jagannath,
Kal Bhairav, Gopinath, Kageshwor, Mahavishnu, Indrapur, Maju Deval, Trailokya Mohan, Shiva-Parvati, Kasthamandap, Big
Bell, Pratap Malla column and so on) and Basantapur Dabali are in the sibling brief `kds-temples.md`. Shared edges are
noted where they matter. The Seto Bhairav screen, Bhagwati and the Big Drums are also listed in the kds-temples
modelling package: this brief describes them with their host palace buildings so the two packages build one object
each (the palace package builds the host walls and roofs, the temples package the screen, drum house and Bhagwati
temple, or as the lead assigns).

Tags: **[O]** OSM (Nepal extract `pipeline/data/raw/osm/nepal.osm.pbf`, snapshot of 2026-10-04), **[A]** measured on
2026 satellite imagery registered to OSM (see §1), **[P]** read from photos, **[S]** published source, **[E]**
estimate, **[V]** to verify on a current photo or on site, **[G]** the game today.

Reference material (outside the repo, never copied in): Commons photos `/home/user/wt/refs/mon-kds-palace/commons/<topic>/`
(sheet.jpg and credits.txt per folder), Openverse photos `/home/user/wt/refs/mon-kds-palace/<topic>/` (contact_sheet.jpg
and credits.txt per folder), aerial crops `/home/user/wt/refs/mon-kds-palace/aerial/`, site plans
`/home/user/wt/previews/mon-kds-palace/site_plan_v2.png` (this brief) and `site_plan_osm.png` (first pass),
current-game renders `/home/user/wt/previews/mon-kds-palace/current/` (base a87e5a4) and `current97/` (base 97012ab,
with game-vs-photo sheets `sbs_1.jpg`, `sbs_2.jpg`), mesh dumps `/home/user/wt/previews/mon-kds-palace/obj/` and
`obj97/`, and the working scripts and the OSM way list (`tools/`: extract, oriented boxes, imagery grid and overlay,
site plan).

**Revision note (finishing pass, 2026-10-17).** The first researcher measured the courts on Esri imagery that is
offset about 5 m west of OSM here. This pass re-measured everything on 2026 satellite imagery that lines up with the
OSM outlines within about 2 m (checked at Taleju, Gaddi Baithak, the Basantapur tower and Kumari Ghar). It moves
Nasal, Lohan, Mul and Mohan Chowk and the three corner towers by 4-7 m. It also corrects the Taleju roof colour
(patinated copper, grey-green on top), Degu Taleju's roofs (copper, no red valances), the Basantapur tower's top roof
(three gilt finials on a short ridge) and the forms of the Lalitpur and Bhaktapur towers. It records that Agamchhen is
still under reconstruction, and adds the inventory table (§0.2), views from four sides for every structure, and the
must-have checklists.

---

## 0. Summary

### 0.1 Verdict: what is wrong today (in order of how much it hurts recognition)

| # | In the game now (renders in `current/`, `current97/`) | Reality |
|---|---|---|
| 1 | The palace itself does not exist. Its footprint is covered by generic boxes from the house grammar: glass-striped blocks, a white 4-storey apartment, plain red 4-6-storey boxes with blue awnings (`a_basantapur_north.png`, `b_square_gate.png`). Nasal Chowk and Mohan Chowk are open sandy ground (`g_top_palace.png`). | A continuous **low palace of courtyards**: white Rana-era plastered ranges with black timber galleries on the square side, red-brick Newar ranges with dense carved windows round Lohan Chowk and on Basantapur, all under deep **tiled roofs** on carved struts, eaves 10-13 m. Nothing modern stands inside the palace outline (§2, §3.3, §3.12, §3.16). |
| 2 | The Hanuman Dhoka gate is a small red-brick box with a gilt door, a tile roof and a red ellipsoid "Hanuman", placed **in the middle of Nasal Chowk**, about 45 m from the real gate (its anchor `n11365076769` is a tourism node inside the court). | The gate is a **door in the white west wall of the palace** facing the square at (-12.4, -2.4), facing WNW. It is painted **blue** with **gilt jambs**, has a carved canopy with three niches above, and two white **stone lions with riders**. The **red-cloaked Hanuman on a pillar under a red-fringed umbrella** stands just left (north) of it (§3.1, §3.2). |
| 3 | The Basantapur tower hero stands in the **Lohan Chowk court** (anchor `n6348849285`, 31 m from the tower) on a plain grey block. The Kirtipur, Bhaktapur and Lalitpur towers are missing. | The **nine-storey tower** is the **SW corner** of the Lohan Chowk complex at (1.6, -66.7) [O w906985354]. Its top roof carries **three gilt finials**. The other corners hold the **Kirtipur tower** (curved grey-green copper roof), the **octagonal Bhaktapur tower** and the **square Lalitpur tower** with its broad single top roof (§3.11-3.15). |
| 4 | Taleju has three bright yellow-gold roofs on a 12-step red brick pyramid (equal 1 m steps to a 26 m platform), with no mini shrines, no compound wall and no gate (`hero_taleju.png`, `c_taleju_w.png`). | The lower plinth stages are broad, low and grassy. The **8th stage is a walled compound**, with **12 small two-roof shrines in front of the wall and 4 inside**. The **lowest roof is very wide** (≈ 33 m [O]). The roofs are **copper**: grey-green patina on the upper surfaces, dark bronze from the ground, gilt only at the finial, ridge ornaments and bells. The **red-ochre Singha Dhoka** gate with lions is on the west (§3.10). |
| 5 | Gaddi Baithak is a white box with a flat white roof, with portico and steps on the **south** face, 14 m tall (`hero_gaddi_baithak.png`, `d_gaddi_w.png`). | The main **portico faces west** (towards Trailokya Mohan and Kumari Ghar): six giant columns on pedestals over a 4-5 m podium with a pierced balustrade. The **long south facade** on Basantapur has giant paired columns, two storeys of arched windows and a balustraded parapet. A **dark red pitched roof** hides behind the parapet. It is 21 m to the parapet [O] (§3.17). |
| 6 | Kumari Ghar has roughly the right mass, but plain side windows and a yellow arch over the door (`hero_kumari_ghar.png`, `e_kumari_ne.png`). | Every bay of the **north facade** carries dark carved windows. A gilt **torana** over a carved door is flanked by two **white lions**. A red-fringed eave valance runs along the eave and a **cluster of five gilt pinnacles** stands on the ridge (§3.19). |
| 7 | Panch Mukhi Hanuman, Degu Taleju, Agamchhen, the Seto Bhairav screen, Bhagwati and the Big Drums are missing or generic. | **Five round roofs** stand on a white drum over the NE corner of Nasal Chowk. Two **three-roof temples** ride on the palace behind the gate. The tall arched **lattice screen** of Seto Bhairav faces the forecourt. Bhagwati's three roofs rise over the NW wing, and the open drum house stands north of the gate (§3.4-3.8, §3.18, §3.20). |
| 8 | The ground is sandy beige noise with brown road ribbons crossing the square and the palace edge (`bird_sw.png`, `g_top_palace.png`). | The ground is level and **paved**: grey stone flags in front of the gate and inside the courts, red **brick** paving on Basantapur square. There is no road surface inside or against the palace (§2.3). |

### 0.2 Inventory at a glance

Positions are local metres (x east, y north) from 27.704500 N, 85.307500 E (§2.1). "Facing" is the compass bearing of
the main facade's outward normal. Heights are above the square. ± is the position tolerance.

| ID | Structure | OSM | Centre (x, y) ± | Facing | Footprint | Height | Roofs / tiers | Plinth | Key colours |
|---|---|---|---|---|---|---|---|---|---|
| S1 | Hanuman Dhoka gate | in w1374620542 (west face) | (-12.4, -2.4) ±2.5 | 282° | surround 4.5 w × 0.8 d | 6 (surround), wall eave 12 | carved canopy | 2-3 steps | blue #2E6FA3, gilt #C9A43A, white #F2F0EA |
| S2 | Hanuman statue + umbrella | - | (-12.9, 1.3) ±2.5 | 282° | pillar 0.7 sq | umbrella top 4.5 | parasol | step 0.2 | red cloth #C8102E, frill #C8202A |
| S3 | West front and Dakha N facade | w420797415 (N edge) | (-39.6, -2.4) to (-13.4, -7.0) | 10° (Dakha), 282° (Mohan front) | range 10 deep | eave 12, ridge 15 | low tiled roof, dormer lanterns | raised platform 3-4 steps | white, black gallery #2B211B, red dado #A8322A |
| S4 | Degu Taleju + Seto Bhairav screen | w1415152806 (top roof) | (-0.1, 18.5) ±3; screen (-7, 12) ±4 | 282° (screen) | tower base ≈ 11 × 11 | ≈ 26 | 3 copper roofs on a brick tower with a tile skirt roof | - | brick #9C4A36, copper #4F4A3E, red valance #B8262B |
| S5 | Agamchhen | (roof over w1374620542) | (-6, 3) ±4 | 282° | ≈ 10 × 9 | ≈ 22-24 | 3 roofs (under reconstruction) | - | brick, red valances |
| S6 | Mohan and Sundari Chowk | n7371671476 | court (11, 7) ±2 | - | court ≈ 13 × 15 | ranges ≈ 11 | 4 ranges, corner turrets | sunken Sun Dhara pit at (11, 7.2) | brick, white |
| S7 | Nasal Chowk | n6348676385 | court centre (4, -34) ±2, axis 12° | - | court 20 × 47 | ranges 11-13 | - | platform 7 × 5 × 0.6 at (3.5, -40) | grey flags #8E8A84 |
| S8 | Panch Mukhi Hanuman | w1414600809 | (19.5, -8) ±2 | round | drum Ø ≈ 7, lowest roof Ø ≈ 10 | ≈ 25 | **5 round roofs** | on palace roof | white drum #F0EEE8, red frills |
| S9 | Mul Chowk | n6348676386 | court (37, -26.5) ±2 | - | court ≈ 14 × 15 | ranges ≈ 8 (2 storeys) | gilt roof on S range | - | brick |
| S10 | Taleju temple | w1414600808 + 9 parts | (45.1, 44.4) ±1 | 283° (gate) | plinth 45.3 × 47.7 | 35 | 3 copper roofs 33 / 25 / 18 m | 12 stages, wall at stage 8, 16 mini shrines | copper patina #8FA593 / #5E7466, gilt finial #D4A93A |
| S11 | Basantapur tower (Nautale) | w906985354 | (1.6, -66.7) ±1 | 190° (S face) | 13.8 × 13.4 | 30 | skirt + 3 upper roofs, top with 3 finials | brick base | brick #9C4A36 / rebuilt #B5583E, wood #3B2A20 |
| S12 | Lohan Chowk | n6348849285 | court (30.5, -57.5) ±2 | - | court ≈ 11 × 17 | ranges ≈ 12-13 (4 storeys) | deep tiled roofs | - | brick, carved windows |
| S13 | Kirtipur tower (NW) | - | (16.5, -42.5) ±3 | - | ≈ 9 × 9 | ≈ 23 | curved copper "Bangla" roof over 2 hipped roofs | - | copper #6F8A7E / #4E6258 |
| S14 | Bhaktapur tower (NE, Lakshmi Bilas) | - | (40, -47.5) ±3 | - | octagon ≈ 8 across | ≈ 19 | octagonal pyramidal roof | - | brick, carved band |
| S15 | Lalitpur tower (SE, Bilas Mandir) | - | (39.5, -71) ±3 | 190° | ≈ 11 × 11, eaves ≈ 16 | ≈ 21 | 2 skirts + broad square top roof | - | brick, carved band |
| S16 | South facade + white SW wing | w906935471 (wing), w906985353 (E range) | line at bearing 100° through (10, -74.5) and (40, -79.5), x -23..66 | 190° | - | 12-15 | tiled | railing, lions | brick, white |
| S17 | Gaddi Baithak | w247501389, hall w430743883, portico parts | hall (-67.5, -60.0) ±1; portico (-87.5, -54.3) | 281° (portico), 191° (S facade) | hall 41.4 × 16.3 | 21 | red-brown pitched roof behind parapet | podium 4-5 | white #F4F2EC, teal #2E6F86, roof #7E3A34 |
| S18 | Hnulunche W facade + Bhagwati | w420797472 | range (-83.3, -36.8); Bhagwati (-80, -30) ±5 | 281° | 18.5 × 6.8 | 12; Bhagwati ≈ 22 | 3 roofs (Bhagwati) | - | white, red arch |
| S19 | Kumari Ghar | w429890388, r2688997 | (-102.3, -84.0) ±1 | 10° (N facade) | 25.3 (N-S) × 21.1 (E-W) | ridge ≈ 14 | hipped tile, gilt pinnacle cluster | brick plinth, steps | brick #A6563F, wood #3E2B20, valance #B5222A |
| S20 | Big Drums, Kot area | - | drums (-5, 40) ±8 | 282° | ≈ 5 × 4 | ≈ 6 | tiled, red valance | - | red-ochre #C4532E |

---

## 1. Sources and method

* **OSM [O]:** all ways and nodes within 260 m of the anchor, extracted with pyosmium and converted to local metres
  with oriented boxes (centre, sides, bearing of the first side). Key objects:
  * Taleju: plinth `w1414600808`, 3D parts `w420797418`, `w1414588339`, `w1414024864`, `w1413718110`,
    `w1413440908`, `w1413440907`, `w1413440906`, `w1413440905`, `w1413440904`; 16 mini shrines `w1414610434..445` and
    `w1415152802..805`; precinct `w196261746`; shop row `w1374620543`.
  * Gaddi Baithak: `w247501389`, hall `w430743883` and portico parts `w420797416`, `w420797474`, `w420797500`,
    `w420797433/438/447/453`; column parts `w420797478..492`, pedestals `w420797430/448/473/475`.
  * Other palace parts: white palace block `w420797415`; gate block `w1374620542`; Basantapur tower `w906985354`;
    white SW wing `w906935471`; south block `w910121098`; west block `w910121099`; east range on the street
    `w906985353`; Panch Mukhi (probable) `w1414600809`; Degu Taleju top `w1415152806`; Hnulunche west range
    `w420797472`; long NW wing `w1415152800`.
  * Kumari Ghar: building part `w429890388` (with `w111984923`) and court-range parts `w429890329/381/382/383/384/386/387/326`,
    multipolygon `r2688997` "Kumari House", node `n2659104413`.
  * Courts: `w199483689` (Dakha), `w199483682` (Lam), `w199484514` (Nucha/Hnulunche). Nodes `n6348676385` Nasal Chowk,
    `n6348676386` Mul Chowk, `n6348849285` Lohan Chowk, `n7371671476` Mohan Chowk, `n11365076769` Hanuman Dhoka,
    `n13278125833` Basantapur Durbar, `n1875597201` Royal Mahendra Museum, `n12998841982` pipal tree (24 m).
  * The square is the multipolygon `r21291455` "Basantapur Durbar Square" (`place=square`, `surface=sett`, World
    Heritage).
  * Most palace ranges are **not mapped as buildings**; their outlines below come from the imagery **[A]**.
* **Imagery [A]:** 2026 satellite tiles at zoom 20 (0.13 m/px). They were registered by drawing the OSM outlines on
  top: the Taleju plinth and temple parts, Gaddi Baithak's hall and portico parts, the Basantapur tower outline and
  Kumari Ghar all match within about 2 m. Esri World Imagery and Bing z19 (used in the first pass) are darker and
  about 5 m off here; positions from them were re-measured. Tall roofs lean north in the 2026 image (about 0.19 m per
  metre of height), so the bases of towers are given with that lean removed. Working crops with a 2 m grid are in the
  scratchpad; overlays from the first pass are in `aerial/`.
* **Photos [P]:** Wikimedia Commons categories fetched as 960 px thumbnails (34 topics, about 600 images), plus Openverse
  searches (Lohan Chowk, Nautale, Mul Chowk, Kumari, Taleju from the plinth, Basantapur 2024, panoramas). They cover
  every side of every structure, and include 2023-2026 photos: Commons "Kathmandu Darbar Square Nepal 20241202" (Dec
  2024: Degu Taleju, Seto Bhairav), "Basantapur Durbar Square Premise 2026" (Taleju, Gaddi), "Hanuman Dhoka Durbar
  Square Kathmandu 97-103" (2023: Lalitpur tower and the rebuilt south-east), and Flickr "2024-11-11 Kathmandu Durbar
  Square" (Steynard). Kumari Ghar and Big Drums photos are in the sibling folders
  `/home/user/wt/refs/mon-kds-temples/c_kumari/` and `.../c_bigdrum/` (read only).
* **Published [S]:**
  * Wikipedia "Hanuman Dhoka": the courtyard layout. Nasal Chowk is entered at its NW corner, has the Mahavishnu image
    on its east veranda, the Malla audience chamber NE, the coronation platform in the middle and Panch Mukhi NE with
    five circular roofs. The Basantapur tower stands on the south side of Nasal Chowk. The four towers date from 1768.
    Mul Chowk has two-storey ranges all round and Taleju's golden torana on its south side. Degu Taleju has three
    roofs. Mohan Chowk (1649) has the sunken golden Sun Dhara and towers at its four corners, with Sundari Chowk north
    of it. Hanuman 1672, Narasimha 1673, the 15-language inscription.
  * Thamel.com and the University of Idaho page quoting the UNESCO 1970s mission:
    * the nine-storey tower is 100 ft (≈ 30 m) and rises five storeys above the general roof level;
    * Lalitpur tower (south-east) and Bhaktapur tower (north-east) each rise two storeys above the quadrangle roofs;
    * Bhaktapur tower is octagonal;
    * Kirtipur tower (north-west) has a copper roof "unparalleled in Nepalese architecture".
  * Rising Nepal "Alluring Chowks of Hanumandhoka": 12 courts, Lohan = stone slab at the centre, Sisa Baithak on the
    Nasal Chowk platform, the Mul Chowk Dashain ceremonies, the Trishul Chowk at the foot of Taleju.
  * Lonely Planet: Taleju 35 m on a 12-stage plinth; Seto Bhairav's lattice screen faces the Jagannath temple.
  * Nepaltraveller: the 8th stage forms a wall, 12 miniature temples in front, 4 inside, gilt copper roofs.
  * The Himalayan Times, China Daily and AidData on the China-funded rebuild of the nine-storey palace and the four
    towers. Work ran 2017-2025. In 2015 the top three tiers of the SW tower and the sixth tier of the NE tower
    collapsed. Exterior finished in 2023; handed over and reopened in 2025.
  * Culture in Crisis and Miyamoto on the US-funded seismic restoration of Gaddi Baithak (grant 2016, completed).
  * Tokyo National Research Institute for Cultural Properties (2016) and Onlinekhabar (Dec 2023) on Agamchhen:
    propped after 2015 and not yet rebuilt in 2023; reconstruction in traditional style agreed.
  * Links in §9.
* **Game [G]:**
  * `HeroCatalog.cs` recipes (taleju, basantapur_tower, hanuman_dhoka, kumari_ghar, gaddi_baithak) and `HeroPalaces.cs`
    (Gate/HanumanDhoka, PalaceTower, RanaPalace).
  * `HeroBuilder.cs` placement: OSM ref match first, then record X/Z, then recipe Lat/Lon.
  * Streaming scene dumps (`StreamingSceneTests.DumpStreetScenes`) rendered with `tools/mesh-preview/render.py` on the
    first-pass base a87e5a4 (`current/`) and again on 97012ab (`current97/`).

---

## 2. Site frame and plan

### 2.1 Anchor and axes

* Local frame: origin **27.704500 N, 85.307500 E**; x metres east, y metres north (1° lon = 98,573 m, 1° lat =
  110,574 m here). lat = 27.7045 + y / 110574, lon = 85.3075 + x / 98573. The modeller converts with
  `WorldFrame.LonLatToGame`. The origin lies in game tile 10/516/161 at tile-local (528.1, 288.9).
* The palace is laid out on a grid **rotated ≈ 12° clockwise** from north: long axes run to bearings ≈ 10-13° and
  ≈ 100-103° (every OSM box here). Taleju's plinth uses the same grid (13° / 103°). "West facade" below means the face
  whose outward normal points to bearing ≈ 282°; "north facade" ≈ 10°; "south facade" ≈ 190°.
* Ground is flat: treat the square, the courts and Basantapur as one level (±0.3 m), with steps and plinths as built
  objects, not terrain.

### 2.2 Zones (local metres)

| Zone | Extent / centre | Notes |
|---|---|---|
| Hanuman Dhoka forecourt (west of the gate) | x -45..-12, y -5..30 | Grey flags. Hanuman statue, gate, Seto Bhairav screen, Pratap Malla column and inscription. Faces Jagannath (-27.6, 16.2) and Kal Bhairav (-40.2, 24.8) [O, sibling]. |
| Mohan Chowk block (closed) | x -8..25, y -3..30; court ≈ (11, 7), 13 × 15 m [A]; node (6.6, 8.0) [O] | Residential court of 1649 with the sunken Sun Dhara: its white horseshoe parapet, about 6 m across, is at (11, 7.2) [A]. Sundari Chowk is north of it [S]. Degu Taleju, the Seto Bhairav building and Agamchhen form its west side. The east range carried a large blue tarpaulin in 2026 [A]. |
| Nasal Chowk | rotated rectangle, centre (4, -34), 20 × 47 m, long axis 12°; corners NW (-1, -9), NE (18.7, -13), SE (9, -59), SW (-10.7, -55) [A]; node (3.8, -29.7) [O] | Entered at its NW corner from the gate passage [S]. West side = the east face of the white Tribhuvan Museum block (`w420797415`). |
| Dakha / Lam / Tribhuvan Museum block (white) | OSM `w420797415` 53.2 × 35.0 m, h 15, centred (-27.0, -31.6) [O] | Courts Dakha (-35.6, -25.7) and Lam (-39.4, -51.4) [O]. Its north face is the long white facade on the forecourt (§3.3). |
| Mul Chowk block (closed) | x 23..49, y -10..-38; court ≈ (37, -26.5), 14 × 15 m [A]; node (33.2, -26.3) [O] | Taleju's ritual court [S]. A dark mark at (36, -30) is probably the ceremonial post [A, V]. |
| Lohan Chowk complex | x -5..48, y -82..-36; court ≈ (30.5, -57.5), 11 × 17 m [A]; node (29.7, -54.4) [O] | Four corner towers (§3.11-3.15). The south facade is on Basantapur; the east face is on a paved lane at x 50-56. |
| Hnulunche (Nucha) Chowk and Gaddi Baithak | x -92..-45, y -68..-25; Nucha court (-73.5, -43.9) [O] | Gaddi's main hall is the south range (§3.17). Bhagwati rides the NW wing (§3.18). |
| Taleju precinct | landuse `w196261746` 66 × 75 m centred (48.6, 29.4); plinth centred (45.1, 44.4) [O] | North-east of the palace, across a lane from the Mohan Chowk block; Trishul Chowk at its foot [S]. |
| Basantapur square | south of the palace, y < -80 (east part) and y < -68 (west part), x -100..45 | Red brick paving; market stalls (`w218440319` marketplace). Kumari Ghar at its SW corner (-102.3, -84.0) [O]. |
| Kot area | north of the palace front, about x -30..0, y 35..90 | Big Drums, Kageshwor (-7.3, 53.2) [O, sibling], Hanuman Dhoka police station `w196112325` (-61.2, 77.3) [O]. |

### 2.3 Ground and paving (must fix)

* **Forecourt in front of the gate and along the white facades:** grey stone flags about 0.6 × 0.4 m, #8F8B83 lit /
  #6F6B65 shade. A red-painted plinth band and a low red bench run along the palace foot (§3.3) [P].
* **Basantapur square:** red brick paving laid in herringbone, #A85B44 (weathered #94503C), with lighter worn paths
  and a few square stone flags round the monuments [P: gaddi c_12-c_15, lohan_south c_07].
* **Courts (Nasal, Lohan, Mul, Mohan):** grey stone flags #8E8A84 with darker joints. Nasal Chowk has the raised
  platform (§3.7). Lohan Chowk has a brick plinth about 0.6 m round its edges with steps [P].
* **Taleju plinth treads:** brick, grassy and mossy in the joints (#6F7A45 tufts) [P: taleju c_26, Openverse eriktorner].
* **No roads inside the palace outline.** The traffic street passes **south of Basantapur square** (y ≈ -82..-90,
  OSM path drawn at y ≈ -82) and a paved lane runs **east of Lohan Chowk** (x 50-56). The brown ribbons drawn across
  the square and along the palace front are wrong [G].

### 2.4 Site plan

Vector plan with the OSM outlines, the measured courts, every structure of this brief and the photo viewpoints:
`/home/user/wt/previews/mon-kds-palace/site_plan_v2.png`. Text sketch (north up; the palace grid is turned ≈ 12°
clockwise; not to scale):

```
                       Kot / Big Drums (-5,40)?     Kageshwor (-7,53)        TALEJU (45,44) on 12-stage plinth
                                                              Singha Dhoka (17,45) ->|  [shop row on its north foot]
   Kal Bhairav  Jagannath     +-------- Mohan Chowk block --------+       pipal (35,22)
   (-40,25)    (-28,16)       | Degu Taleju (0,18) 3 copper roofs |
                              | Seto Bhairav screen W face (-7,12)|
     forecourt (grey flags)   | Agamchhen (-6,3)  Mohan court     |
   Hanuman (-12.9,1.3) ->     |   Sun Dhara pit (11,7)  Sundari N |
   GATE (-12.4,-2.4) -> 282°  +-----------------------------------+-- Panch Mukhi (19.5,-8) ----+
   +-- Dakha N facade, white, eave 12, faces N --+  NASAL CHOWK  |    MUL CHOWK court (37,-26) |
   | Dakha court (-36,-26)                       |  20 x 47 m,   |                             |
   |               Tribhuvan Museum (white) ->   |  centre (4,-34)                             |
 Hnulunche/Bhagwati (-80,-30)  Lam court (-39,-51)  platform (3.5,-40)  Kirtipur NW (16.5,-42.5)   Bhaktapur NE (40,-47.5)
 +-- GADDI BAITHAK ---------------------------+-------------+     LOHAN court (30.5,-57.5)          |
 | portico W (-87.5,-54)  hall (-67.5,-60) 21 m| white SW wing  BASANTAPUR 9-storey (1.6,-66.7)   Lalitpur SE (39.5,-71)
 +--------------------------------------------+-(-16.6,-65)---+-- south facade (10,-74.5)-(40,-79.5) --+-- E range w906985353
   KUMARI GHAR (-102,-84), door N                 BASANTAPUR SQUARE (red brick)          street y≈-85
```

---

## 3. Structure inventory

Positions are local (x, y) metres with lat/lon. Heights are above the square. Colours are lit / shade hex for the toon
material. "Views" lists what each side shows and the photos that show it (`commons/<folder>/c_NN.jpg`, Openverse
`<folder>/ref_NN.jpg`).

### 3.1 S1 - Hanuman Dhoka gate (palace main entrance, the "golden gate")

* **OSM:** no gate object. `n11365076769` "Hanuman Dhoka" (-5.0, -44.3) is a tourism node in Nasal Chowk, not the
  gate. The gate is a door in the **west face of the block `w1374620542`** (9.6 × 8.2 m, centre (-8.4, -3.1), axes
  12° / 102°) [O]. That face runs from the re-entrant corner with the Dakha range at (-13.4, -7.0) north to about
  (-12.0, 2.8).
* **Position:** door centre ≈ **(-12.4, -2.4)** ±2.5 m = **27.704478 N, 85.307374 E**, about 4.7 m north of the corner
  (photos: 2-3 m of white wall between the right-hand lion and the corner) [O/P]. Faces **bearing 282°** (WNW) onto the
  forecourt, towards Jagannath. The passage behind runs east about 10 m through the block to the NW corner of Nasal
  Chowk at (-1, -9) [S/A].
* **Setting:** the gate is set flush in a **white plastered wall** (#F2F0EA / #CFCBC2) with blind cusped arches at
  ground level and black timber windows and galleries above. Immediately south (right, when facing it) the long white
  Dakha range turns west on a raised platform with 3-4 broad stone steps (§3.3) [P: hanuman_gate c_06, c_09, c_14, c_15].
* **Form [P]:** overall surround ≈ 4.5 m wide × 6 m high [E]:
  * Plinth: 2-3 stone steps about 0.5 m high across the full width of the surround, with the lions on their own
    pedestals at each end.
  * Door: opening ≈ 1.6 m × 2.7 m [E]. Dark carved wooden leaves; **gilt repoussé jambs and lintel** (≈ 0.35 m bands,
    #C9A43A / #8E6F25) with small figures.
  * Outer frame: wide pilaster panels painted **bright blue** (#2E6FA3 / #1F4E78) with painted deities, roundels and
    pairs of painted eyes; small dark wooden pilasters at the outer edges.
  * Over the door: a blue panel with **three carved niches** under cusped arches (Krishna with attendants in the
    centre [S/V]), framed by a carved dark-wood hood; a small carved panel of a king and queen sits on the blue frame
    above (hanuman_gate c_18, c_19). On top is a projecting **carved wooden canopy** (#3B2A20) at ≈ 5.5 m [E].
  * Signboards: small brass "Hanuman Dhoka Durbar Museum" plaques either side of the canopy [P].
* **Lions [P/S]:** a pair of stone lions ≈ 1.2 m on ≈ 0.6 m pedestals, **painted white** with **red stripes**, red
  mouths and yellow collars. Each carries a rider in polychrome (Shiva on one, Parvati on the other [S]). They face out
  (WNW), one each side of the door.
* **Narasimha (inside):** a black stone Narasimha group (1673 [S]) ≈ 1.8 m [E] in a **silver repoussé arched niche**
  with two sun discs, on the left just inside the entrance [S/V] (narasimha c_00-c_06). Model it as a dark stone figure
  group in a bright niche; no gore, a general "lion-headed guardian" reading.
* **Views:**
  * **W** (from the forecourt, V1): the postcard. Gate, lions and Hanuman; the white wall with black galleries; the
    three-roof temple rising behind (agamchhen c_07, c_08, c_14; hanuman_gate c_12-c_15).
  * **N** (from the Degu Taleju corner, V2): gate in profile, with the white Dakha range projecting beyond it on a
    stepped platform (hanuman_gate c_06, c_10, c_11).
  * **S** (from the Dakha platform): gate and Hanuman with the Mohan front and Degu Taleju beyond.
  * **E**: inside, the passage opens into the NW corner of Nasal Chowk (nasal_chowk c_21).
* **In the game now [G]:** `GateHanuman` hero: a 5 × 2.5 m red-brick box with a gilt door, a small tile roof, a red
  ellipsoid Hanuman and generic lions, placed in Nasal Chowk at (-5, -44) (`hero_hanuman_dhoka.png`,
  `g_top_palace.png`). Its yaw of 280° is right; position, form and colours are wrong.
* **Must have:**
  1. Gate in the palace west wall at ≈ (-12.4, -2.4) facing 282°, flush with a white plastered wall, not a free-standing box.
  2. Blue painted surround with gilt door jambs and lintel and dark carved door leaves.
  3. Three-niche carved panel and dark wooden canopy above the door, top ≈ 5.5-6 m.
  4. Two white lions with red stripes and riders on pedestals, one each side, on a 2-3 step stone plinth.
  5. Black timber windows and gallery of the palace wall continuing above and to the north; the white Dakha range
     turning west immediately to the south.
  6. Narasimha niche just inside (LOD0 only).

### 3.2 S2 - Hanuman statue (1672) under its umbrella

* **Position:** ≈ 3.5 m north of the door centre and 1.2 m in front of the wall: **(-12.9, 1.3)** ±2.5 m =
  27.704512 N, 85.307369 E [P/O]. It stands left of the gate as seen from the square.
* **Form [P: hanuman_gate c_05-c_09, c_12-c_15]:**
  * A square stone pillar ≈ 0.7 m wide × 1.6 m high, smeared red (#8E2B22, darker at the base), on a low step.
  * On it the standing Hanuman ≈ 1.5 m, **wrapped in a red cloth cloak** (#C8102E with folds, sometimes orange
    #E06A1A), with only the sindoor-smeared head showing and no readable face.
  * Over it a **parasol** ≈ 1.8 m across on a pole: dark canopy (#2A2422) with a deep **red fringed valance**
    (#C8202A, 0.4 m) and a small gilt top. Umbrella top ≈ 4.5 m.
  * A thin iron flag/bell stand and a small railing sometimes stand beside it.
* **Views:**
  * **W:** red figure against the white wall, the umbrella overlapping the black window above.
  * **N/S:** pillar and umbrella in profile, umbrella pole visible behind the figure.
  * **E:** not seen (wall).
* **In the game now [G]:** a red ellipsoid on the gate box; no pillar, no umbrella.
* **Must have:** red-smeared pillar; red-cloaked figure without a modelled face; dark parasol with red frill; placed
  north of the gate, in front of the wall; readable from 60 m as a red vertical with a red-and-black umbrella.

### 3.3 S3 - Palace west front and Dakha Chowk north facade (white ranges on the square)

* **What it is:** the outer facades seen from the square:
  * (a) the **Dakha Chowk north facade**, a long white range from the gate corner (-13.4, -7.0) west to (-39.6, -2.4)
    (north edge of `w420797415`, h 15 m, red gabled roof [O]). It faces **bearing 10°**.
  * (b) the west face of the Mohan Chowk block from the gate north to the Seto Bhairav building (x ≈ -12..-7,
    y 3..10), facing 282° [A/P].
  * (c) the inscription and plinth along their foot.
* **Form [P: dakha_north c_00-c_11, agamchhen c_02-c_06, outer_facades c_05-c_13]:** three storeys, eave ≈ 12 m,
  ridge ≈ 15 m:
  * Ground floor: white plaster with a continuous row of **blind cusped (Mughal) arches** in low relief and few small
    windows. A **red-painted dado/plinth** (#A8322A) ≈ 0.6 m with a red bench ledge where people sit. Along the Dakha
    range runs a raised stone platform with 3-4 steps.
  * First floor: rectangular windows with dark louvred shutters (#3A2C24) in plaster surrounds, ≈ 3.5 m spacing.
  * Second floor: a **continuous projecting timber gallery** (black/dark brown #2B211B) with slender posts and a
    balustrade, carried on a row of **black diagonal struts**.
  * Roof: low hipped/gabled tile roof (#6E4B3A, moss #5B6B3A) with deep eaves. Small **white gabled roof pavilions
    (dormer lanterns)** stand on the ridge.
* **Current state [P]:** photos from 2015-2023 show raking **timber shores** against the Dakha range. Model the facade
  restored, without shores [V on a 2026 photo].
* **Pratap Malla inscription (1664) [S/P: eichmann_palast c_11, c_12; inscription]:** a long dark grey stone slab
  (≈ 2.5 × 0.8 m [E]) set low in the outer wall near the gate. It has lines of script in 15 languages, a small relief
  in the middle and a square spout hole (it is a jaladroni). Top ≈ 0.9 m above the ground; exact bay [V].
* **Views:**
  * **N** (from Kal Bhairav / Jagannath): the Dakha range runs left to right as a white wall with a black gallery
    stripe under a brown roof, with the platform steps at its foot (agamchhen c_04-c_06).
  * **W** (from the forecourt): the Mohan front north of the gate in the same language, with the three-roof temples
    rising above (agamchhen c_07, c_14).
  * **E/S:** inside the palace (Dakha and Lam courts, not public).
* **In the game now [G]:** generic modern boxes and a glass-striped block on this frontage (`b_square_gate.png`).
* **Must have:** white plaster walls with blind cusped arches at ground level; red dado and bench; black continuous top
  gallery on diagonal struts; low tile roof with white dormer pavilions; eave ≈ 12 m; inscription slab at the foot;
  stepped stone platform along the Dakha range.

### 3.4 S4 - Degu Taleju and the Seto Bhairav screen

* **OSM:** the top roof part `w1415152806` 6.3 × 6.2 m, pyramidal, centre **(-0.1, 18.5)** = 27.704667 N,
  85.307499 E [O]. The base tower is not mapped.
* **Form [P: sweta_bhairav c_05-c_09, Dec 2024; dakha_north c_06]:**
  * A **massive square brick tower** (≈ 11 × 11 m [E], #9C4A36) of three to four storeys with brick corner pilasters,
    stepped brick cornices and small carved lattice windows. It ends at ≈ 13 m in a **tile skirt roof with a carved
    sawtooth fascia** and a brick parapet.
  * On it the temple: **three roofs of dark copper sheet** (#4F4A3E, grey-green on top #7E9184) on long carved
    struts. A carved and gilt-trimmed body storey has gilt torana panels; hanging bells line every eave, a small
    pataka banner hangs on each face, and a **gilt finial** tops it.
  * The temple roofs have **no red valance** (correction to the first pass); the red fringe is on the adjoining palace
    range. Finial ≈ **26 m** [E].
* **Seto (Sweta) Bhairav screen:** on the west face of the **two-storey brick palace range** that adjoins Degu Taleju
  on its south-west, facing the forecourt opposite Jagannath [S: Lonely Planet], at ≈ **(-7, 12)** ±4 m.
  * A **tall round-topped arch** ≈ 3.2 m wide × 4.5 m high [E] filled with a carved lattice in 3 × 4 panels.
  * A **red fringed valance follows the arch**. Two **brass hanging lamps** hang on chains in front.
  * At the base: a carved stone panel ≈ 0.8 m with **two guardian figures** at its ends.
  * The range around it: brick with carved windows (round oculus windows, a carved bay window), eave ≈ 7 m, with a
    **red fringed valance (#B8262B) along the eave**.
  * Behind the screen the giant gilt mask of Seto Bhairav is shown only during Indra Jatra (not modelled).
* **Views:**
  * **W** (from Jagannath's plinth): the arched lattice screen in the brick range with the tower and the three copper
    roofs rising behind-left (sweta_bhairav c_06-c_08).
  * **N** (from the Kot side): the brick tower base with the skirt roof and the temple above, the white Mohan front
    beyond.
  * **S** (from the gate): the three roofs above the white Mohan front, north of Agamchhen (hanuman_gate c_06, c_10).
  * **E:** inside Mohan Chowk (closed).
* **In the game now [G]:** missing.
* **Must have:** brick tower base with its tile skirt roof at ≈ 13 m; three dark copper roofs with bells and a gilt
  finial at ≈ 26 m, no red frills on them; the tall arched lattice screen with red fringe, two hanging lamps and
  guardian base on the adjoining brick range, which carries a red valance along its eave.

### 3.5 S5 - Agamchhen (Agam Chhen, mid-17th century)

* **Position:** on the block **directly behind the gate**, ≈ **(-6, 3)** ±4 m = 27.704527 N, 85.307439 E [A/P]. From
  the forecourt it rises over the white wall just behind and slightly north of the gate.
* **Status [S/P/A]:** damaged in 2015 and propped with timber (NRICPT 2016). Not yet rebuilt in December 2023
  (Onlinekhabar: JICA study, reconstruction in traditional style agreed). A December 2024 photo shows scaffolding with
  green netting south of Degu Taleju (sweta_bhairav c_05). The 2026 satellite image shows **temporary blue and red
  sheet roofs** over x -12..0, y -2..8. **Decision for the build:** model the completed traditional form (as in
  2010-2014 photos), because the reconstruction restores it. Flag this to the owner as the one structure shown
  "as restored, not as seen today".
* **Form [P: agamchhen c_01, c_07-c_09, c_14 (pre-2015)]:** a slim **three-roof temple** standing on a brick palace
  building whose upper storey has a carved lattice gallery. The roofs are small (lowest ≈ 7 m across [E]), dark tile
  or copper with **red fringed valances** on all three eaves, and a gilt finial. Top ≈ **23 m** [E]. It is lower and
  slimmer than Degu Taleju.
* **Views:**
  * **W:** directly over the gate and Hanuman, framed by the white range (agamchhen c_07, c_14).
  * **N:** behind Degu Taleju's base, partly hidden.
  * **S** (from the Dakha platform and the Jagannath corner): three red-fringed roofs above the white wall
    (agamchhen c_00, c_10).
  * **E:** from Nasal Chowk's north end, above the north range roofs.
* **In the game now [G]:** missing.
* **Must have:** three small red-valanced roofs on the roof directly behind the gate, lower and slimmer than Degu
  Taleju, visible over the gate from the forecourt.

### 3.6 S6 - Mohan Chowk and Sundari Chowk (closed courts)

* **Position:** Mohan court ≈ **(11, 7)** ±2, 13 × 15 m on the 12° grid [A]; node (6.6, 8.0) [O]. Sundari Chowk is
  north of it [S], in the block's north part up to y ≈ 28.
* **Form [S/A/P]:**
  * Mohan Chowk (1649) is a closed residential court with ranges of 3 storeys (eave ≈ 10-11 m) and **small towers at
    its four corners** [S]. The roofs are tiled.
  * At its centre the **golden Sun Dhara** spout sits in a pit several metres below the court [S]. From the air the
    pit's **white horseshoe parapet**, about 6 m across, shows at (11, 7.2) [A].
  * Sundari Chowk is smaller, with lattice windows and doors [S].
  * Both courts are closed to the public, and Mohan Chowk was under repair in 2023-2026 (a large blue tarpaulin on its
    east range in the 2026 image) [S/A].
  * Seen from the Basantapur tower top and from the air only.
* **Views:** N/E/S/W only from above: four tiled ranges round a court with a white ring in the middle; corner turrets
  [V].
* **Must have (LOD for distance and air):** an enclosed court with tiled roofs on four ranges and four small corner
  roof turrets [V: form]; brick and plaster mix; a dark sunken pit with a white parapet in the court; no other
  interior detail.

### 3.7 S7 - Nasal Chowk

* **Court [A/P]:** rotated rectangle **20 × 47 m**, long axis 12°, centre **(4, -34)** = 27.704193 N, 85.307541 E.
  Corners NW (-1, -9), NE (18.7, -13), SE (9, -59), SW (-10.7, -55). Grey stone flags. Entered at the **NW corner** [S].
* **Coronation platform:** a low rectangular stone platform ≈ **7 × 5 m × 0.6 m** at **(3.5, -40)** [A] with potted
  plants on its edges (Birendra was crowned here in 1975 [S]; the Sisa Baithak, a glass-fronted pavilion, stood on it
  [S]) [P: panchamukhi c_10, c_11, c_14].
* **North range [P: nasal_chowk c_02, c_03, c_05; panchamukhi c_05, c_07-c_11]:** Rana-era, three storeys of white
  plaster:
  * ground floor: an **arcade of cream-yellow columns and arches** (#E9D9A6);
  * first floor: a continuous row of windows with **green louvred shutters** (#1F7A55 / #145238);
  * second floor: an **open timber gallery** of white arches with a dark balustrade;
  * deep tile roof, flowerpots along the arcade. Panch Mukhi rises at its east end.
* **East range [P/S]:** a long low brick range with an **open veranda of red-painted wooden posts** (#9E3B2B) under a
  tiled roof. The golden Mahavishnu image sits on this veranda, the Malla audience chamber is at the NE corner, and
  the dancing Shiva (Nasa Dyo) shrine gives the court its name [S].
* **White shrine:** a small whitewashed shrine ≈ 2.5 m square, ≈ 4 m high, with an arched door and a bell-shaped top,
  in front of the east range near its south end [P: nasal_chowk c_07, c_08, c_16-c_20; panchamukhi c_09, c_13].
* **West range:** the white 4-storey Rana block (Tribhuvan Museum [S]) with regular windows [P: nasal_chowk c_13].
* **South side:** the brick Basantapur tower (SW) and the NW wing of the Lohan Chowk complex with the Kirtipur tower:
  the photograph everyone takes (nasal_chowk c_08, c_11, c_16-c_20; Openverse basantapur_tower ref_01, ref_04, ref_07).
* **Views:**
  * **Looking N** (from the platform, V4): white north range with yellow arcade, green shutters and top gallery, Panch
    Mukhi's five round roofs at its right end.
  * **Looking S** (V3): the Kirtipur tower's green copper roof in the centre-left over a multi-roofed brick mass, the
    nine-storey tower right and further back, the white shrine and the red-post veranda on the left.
  * **Looking E:** the long red-post veranda under a tile roof with Mul Chowk's roofs behind.
  * **Looking W:** the plain white Tribhuvan block.
* **In the game now [G]:** open sandy ground with the misplaced gate box in it.
* **Must have:** stone-flagged court of the right size and angle; the platform; white north range with yellow arcade,
  green shutters and top gallery; red-post east veranda; white shrine; Tribhuvan white range on the west; brick towers
  closing the south.

### 3.8 S8 - Panch Mukhi Hanuman temple (five round roofs)

* **OSM:** probably `w1414600809` (9.1 × 7.6 m, 4 levels, pyramidal brown roof) at (18.4, -7.5) [O]. The named
  `w199296907` "Panch Mukti Hanuman Temple" at (99, -9.5) is 80 m east, outside the palace: a mis-mapping.
* **Position:** base **(19.5, -8)** ±2 = 27.704428 N, 85.307698 E [A: concentric round roofs at (21, -4) in the 2026
  image, lean removed], at the **NE corner of Nasal Chowk** [S].
* **Form [P: panchamukhi c_01-c_15; Openverse basantapur_square ref_09; square_2024 ref_04]:**
  * A **cylindrical white drum** (#F0EEE8, diameter ≈ 7 m [E]) with a red-framed arched door rises from the court
    corner above the 3-storey ranges.
  * It carries **five circular roofs**, stacked and shrinking upwards (≈ 10, 8.5, 7, 5.5, 4 m across [E]). Each is a
    dark ribbed cone (#5A4537) with a **red-and-white fringed valance** (#B8262B), on carved struts, with a round
    timber gallery storey between roofs.
  * A **gilt finial** tops it at ≈ **25 m** [E].
  * Round in plan at every level: this is its signature.
* **Views:**
  * **S/SW** (from Nasal Chowk): the full stack over the white north range.
  * **W** (from the forecourt and Jagannath): the top three rings over the palace roofs, right of Agamchhen.
  * **N/E:** seen over Mohan and Mul Chowk roofs from the Taleju plinth (Openverse ov_Hanuman_Dhoka_gate ref_04, ref_05).
* **In the game now [G]:** missing.
* **Must have:** five round stacked roofs with red frills on a white cylinder at the NE corner of Nasal Chowk, visible
  over the palace from the forecourt and from Basantapur.

### 3.9 S9 - Mul Chowk (Taleju's court, closed)

* **Position:** court ≈ **(37, -26.5)** ±2, 14 × 15 m [A]; node (33.2, -26.3) [O]; the block spans x 23..49,
  y -10..-38.
* **Form [S/P: mul_chowk Openverse]:**
  * Enclosed by **two-storey** brick ranges on all sides (eave ≈ 7-8 m) [S], with carved windows and a ground-floor
    colonnade like a monastery court [S].
  * On the **south side** is the Taleju shrine with a **golden torana**, its door flanked by images of **Ganga and
    Yamuna** [S]; a small gilt roof of that shrine shows above the ranges [V].
  * Stone or brick court with a ceremonial post near the centre [A/V].
* **Views:** from Nasal Chowk only its roofs over the east range; from the Basantapur tower top a low closed court
  between Nasal Chowk and the lane.
* **Must have:** an enclosed two-storey court lower than its neighbours; one gilt roof element on the south range.

### 3.10 S10 - Taleju Temple (1564, Mahendra Malla)

* **OSM:** plinth `w1414600808` 45.3 (N-S) × 47.7 (E-W) m centred **(45.1, 44.4)** = 27.704902 N, 85.307958 E,
  axes 13° / 103°; precinct `w196261746` 66 × 75 m.
* **Height:** **35 m** [S: Lonely Planet; O: finial part to 35 m].
* **Plinth [S/O/P: taleju c_02-c_05, c_08-c_13, c_24-c_27; Openverse ov_Hanuman_Dhoka_gate ref_00-ref_06]:**
  * **12 stages** [S]. The lower stages are broad grassy terraces with low brick risers (0.4-0.6 m, #A0503A), stepping
    in from 45 × 48 m [O]. OSM gives the first three as 0.5, 1.0 and 2.0 m high at 45 × 48, 44 × 42 and 43 × 40 m.
  * The **8th stage forms a brick wall** round a raised compound [S].
  * **12 small two-roof shrines stand in front of that wall** and **4 more inside** at the corners [S]. OSM maps all
    16: 12 at 3.2 × 3.0 m, 6 m tall (`w1414610434..445`, about 20 m from the centre) and 4 at 7.2 m
    (`w1415152802..805`, at the inner corners) [O].
  * Each mini shrine is a brick pedestal of two steps (≈ 1 m), a brick shaft (≈ 1.2 m), two small square mossy tile
    roofs and a gilt finial.
  * Above the wall come a brick upper stage and a stepped brick base with an **iron railing** round the temple's base
    storey. Plinth top under the temple ≈ 6-7 m [E/V].
* **Precinct wall and gate:** the precinct is closed on the west by a tall **red-ochre** (#C4472E) wall with a white
  stepped cornice. The **Singha Dhoka** gate is at ground level on the west, ≈ **(17.5, 45)** ±4, facing the Kageshwor
  side [P: singha_dhoka c_00-c_11]:
  * a **black studded metal door** in a richly carved polychrome torana arch;
  * painted roundels and pairs of eyes either side;
  * **two white lions with red stripes** on pedestals.
* **Temple [O/P]:**
  | Part (OSM) | Plan | Base - top |
  |---|---|---|
  | Ground body `w1414024864` | 19.8 × 19.2 m | 0 - 12 m (includes the upper plinth) |
  | Roof 1 `w1413718110` | **33.2 × 32.4 m** | eave 12 m, roof rise 7 m, to 19 m |
  | Body 2 `w1413440908` | 14.6 × 14.1 m | to 19.5 m |
  | Roof 2 `w1413440907` | 24.9 × 24.3 m | 19.5 - 25 m |
  | Body 3 `w1413440906` | 9.7 × 9.4 m | 20 - 27.5 m |
  | Roof 3 `w1413440905` | 18.4 × 17.9 m | 26.5 - 31 m |
  | Finial `w1413440904` (gold) | 5.1 × 5.0 m | to 35 m |

  The lowest roof is very wide relative to the upper two (ratio ≈ 1 : 0.75 : 0.55). Every eave has deep carved struts
  and rows of hanging bells. The body storeys are brick with **three gilt window panels** per face, a gilt torana over
  the door and carved cornices (taleju c_19, c_26), and **pataka** banners hang from the top [P].
* **Colours [P/A] (correction):** the roofs are **copper sheet with standing seams**.
  * Upper surfaces are patinated **grey-green**: #8FA593 lit / #5E7466 shade. This is how they read from the Basantapur
    tower, the plinth and the air (taleju c_26; 2026 image). The OSM `roof:colour #165E54` has the right hue but is
    too dark and saturated.
  * From the square at a low angle they read as dark bronze-grey (#5A5446) with dark struts (#3B2A20).
  * Gilt (#D4A93A) only on the finial, ridge ornaments, corner bells and the window panels.
  * Never bright yellow.
  * Brick #9C4A36.
* **North foot:** a long single-storey **shop row** `w1374620543` (55.8 × 7.2 m, h 3, red gabled roof, at (46.7, 69.6))
  with red cloth awnings. This is the view from Makhan Tole and Indra Chowk (taleju c_02-c_05, c_08-c_10) [O/P].
* **Trees:** a big pipal tree, 24 m [O `n12998841982`], at **(34.8, 22.0)**, SW of the plinth. It must stand beside
  the plinth, not on it.
* **Views:**
  * **N** (Makhan Tole, V7): the temple above the shop row and the mini shrines along the wall top (taleju c_02-c_13).
  * **W** (Kot side): the Singha Dhoka in its red wall with the roofs above (singha_dhoka c_00, c_06, c_11).
  * **S/SW** (forecourt, Maju Deval): the three dark roofs above the white palace ranges and the pipal tree.
  * **E:** lane and houses; the stepped plinth corners show (taleju c_24).
  * **Above** (from the Basantapur tower): grey-green roofs and grassy terraces (basantapur_tower ref_06).
* **In the game now [G]:**
  * `Pagoda` hero with plinth widths 47.7 - 2i: 12 equal 1 m steps to a 25.7 m platform.
  * Roofs RoofColour 0x96814A with GiltAll finish, which reads bright yellow-gold, with red eave trims
    (`current97/v7_taleju_from_n.png`). The real roofs have no red valance: bells and banners hang there instead.
  * The compound wall flag is on, but there are no mini shrines, no Singha Dhoka and no shop row
    (`hero_taleju.png`, `c_taleju_w.png`).
* **Must have:**
  1. 12-stage plinth: broad, low, grassy lower stages; a walled compound at stage 8; 12 + 4 two-roof mini shrines in
     the right places.
  2. Three roofs with the real spans (≈ 33 / 25 / 18 m) and eaves (12 / 19.5 / 26.5 m); finial to 35 m.
  3. Copper roofs: grey-green patina on top, dark from below, gilt accents only; hanging bells and pataka banners,
     no red cloth valances.
  4. Red Singha Dhoka on the west with a black door, torana and white lions.
  5. Shop row along the north foot; pipal tree at (34.8, 22.0).

### 3.11 S11 - Basantapur tower (Nautale Durbar, nine-storey, 1768-70)

* **OSM:** `w906985354` 13.8 × 13.4 m centred **(1.6, -66.7)** = 27.703897 N, 85.307516 E [O]; a stale
  `w341127950` "partially destroyed after earthquake" lies over it. HeroCatalog uses node `n6348849285` (29.7, -54.4),
  the Lohan Chowk court centre: **31 m off**.
* **Status:** the 2015 quake brought down its top three tiers [S]. It was rebuilt by the China-funded project
  (2017-2023 exterior), handed over and reopened in 2025 [S]. Model the rebuilt tower.
* **Height:** **30 m** (100 ft, UNESCO mission [S]); it rises five storeys above the general palace roof level [S].
* **Form from the square [P: basantapur_tower c_00, c_02, c_03, c_07, c_10, c_18; lohan_south c_00-c_06]:** square,
  tapering, nine storeys:
  * **Base (storeys 1-3, 0-10 m):** brick (#9C4A36; rebuilt brick brighter #B5583E) with small carved lattice windows
    and carved cornice bands. A **central three-bay carved window** projects at the 2nd-3rd storey on the south face,
    with carved figure struts either side.
  * **Roof 1 (eave ≈ 10 m):** a broad hipped tile roof (#5A3E30) on carved struts, continuous with the Lohan south
    range roof to the east.
  * **Storey 4:** a continuous band of carved lattice windows on all sides under a carved cornice.
  * **Roof 2 (eave ≈ 15 m):** nearly as broad as roof 1, on long struts.
  * **Storeys 5-6:** brick with a projecting carved window balcony at the centre of each face.
  * **Roof 3 (eave ≈ 20 m).**
  * **Top storeys:** a **timber belvedere** with continuous carved lattice on all four sides.
  * **Roof 4 (eave ≈ 25 m):** a broad **hipped roof with a short ridge carrying three gilt finials** (correction: not
    a pyramid with one finial; c_00, c_10, ref_04).
  * Deep eaves throughout; struts carry figure carvings [S] (render as generic carved figures).
* **Faces:** S onto Basantapur square (lions and railing at its foot, §3.16), W onto the white SW wing, N onto Nasal
  Chowk, E onto Lohan Chowk.
* **Views:**
  * **S** (V5): the full tower rising from the brick south range, white SW wing left, Lalitpur tower far right
    (c_02, c_10, c_18).
  * **N** (Nasal Chowk): four roofs stepping up right of the Kirtipur tower (nasal_chowk c_08, c_11).
  * **E** (Lohan Chowk): rises from the court corner over 4-storey ranges.
  * **W** (Gaddi corner): over the white SW wing.
* **In the game now [G]:** `PalaceTower` hero 10 × 10 m, 9 storeys, 4 tiers, 30 m, yaw 180, in the wrong place, on a
  plain block (`hero_basantapur_tower.png`, `bird_sw.png`).
* **Must have:** at the SW corner of the Lohan complex (1.6, -66.7); 13.8 m base; brick base with the central carved
  bay; four broad stacked roofs (the lowest continuous with the south range); lattice belvedere; three gilt finials
  on the top ridge; 30 m.

### 3.12 S12 - Lohan Chowk (court and ranges)

* **Court:** ≈ **11 × 17 m** on the 12° grid, centre **(30.5, -57.5)** ±2 [A] (the first pass had (24.9, -58.1), about
  6 m west). Grey stone flags; a large stone slab at the centre gives the court its name [S]. A brick plinth ≈ 0.6 m
  with steps runs round the edges [P].
* **Ranges [P: lohan_chowk c_01-c_19; Openverse basantapur_square ref_05-ref_08]:**
  * Four storeys of red brick, eave ≈ 12-13 m [E], with **very dense carved windows**: a projecting three-bay window
    centred on each range, rows of small lattice windows, and a continuous carved lattice band on the third floor.
  * Ground floors are **open colonnades of carved wooden pillars** on stone bases (4-6 bays) with an internal stair.
  * Deep tile roofs on carved struts.
  * A carved door with painted eyes is on the west side (lohan_chowk c_12).
* **Views (inside, all sides):** brick on every side, the Basantapur tower filling the SW corner, the Kirtipur copper
  roof NW, the octagonal Bhaktapur tower NE, the Lalitpur top roof SE.
* **Must have:** enclosed four-storey brick court with the four towers at its corners, dense carved windows, ground
  colonnades.

### 3.13 S13 - Kirtipur tower (NW corner of the Lohan Chowk complex)

* **Position:** ≈ **(16.5, -42.5)** ±3 = 27.704116 N, 85.307667 E [A: the grey-green copper roof shows at (17.3, -37.8)
  in the 2026 image, lean removed]. It stands at the SE corner of Nasal Chowk.
* **Form [P: kirtipur_tower c_00-c_10; Openverse basantapur_tower ref_04]:**
  * A square brick tower ≈ 9 × 9 m [E] rising above the court roofs:
    * the lowest broad roof continuous with the ranges;
    * two brick storeys with windows;
    * a broad hipped tile roof;
    * a storey of carved windows.
  * On top its signature **curved "Bangla" roof clad in copper**: a domed, ribbed, four-sided hood whose eaves curve
    down and flare out at the corners like a shell. Patinated **grey-green** (#6F8A7E lit, #4E6258 shade) with darker
    ribs and a small finial.
  * Top ≈ **23 m** [E].
  * It survived 2015 with its copper roof [S].
* **Views:**
  * **N** (Nasal Chowk): centre of the classic view, in front of the nine-storey tower.
  * **S/E** (from Lohan Chowk): the copper hood over the NW range.
  * **W:** over the Tribhuvan block from the Dakha side.
  * **Above:** an oblong grey-green dome.
* **Must have:** the grey-green copper curved roof (unique in the valley's palaces) on a square brick tower, seen from
  Nasal Chowk in front of and left of the nine-storey tower.

### 3.14 S14 - Bhaktapur tower (Lakshmi Bilas, NE corner)

* **Position:** ≈ **(40, -47.5)** ±3 = 27.704070 N, 85.307906 E [A: radial ridge pattern of an octagonal roof with its
  apex at (40.5, -44) in the 2026 image, lean removed].
* **Status:** its sixth tier collapsed in 2015 [S]; rebuilt by the same project [S].
* **Form [S/P: lalitpur_tower c_04, c_10 (2023); kirtipur_tower c_01]:**
  * An **octagonal** tower ≈ 8 m across [S/E] on a square brick base. It rises two storeys above the quadrangle roofs
    [S]: a brick storey with small windows, then a belvedere of **eight carved lattice window faces** under an
    **octagonal pyramidal tile roof** with deep eaves and a **gilt finial**.
  * Top ≈ **19 m** [E].
* **Views:**
  * **E** (from the lane): over the east range roof.
  * **SE** (street corner): far right above the shop range (lalitpur c_04).
  * **W** (Nasal Chowk): a small pointed roof left of the Kirtipur tower (nasal_chowk c_16).
  * **From the east rooftops:** right of the Lalitpur tower (lalitpur c_10).
* **Must have:** octagonal plan and roof with a carved band of eight window faces; lower than the Kirtipur tower and
  the Lalitpur tower.

### 3.15 S15 - Lalitpur tower (Bilas Mandir, SE corner)

* **Position:** ≈ **(39.5, -71)** ±3 = 27.703858 N, 85.307901 E [A: broad pyramidal roof about 15 m across with its
  apex at (40.5, -67.5), lean removed]. It stands at the corner where the brick south facade meets the shop range
  `w906985353` running east along the street.
* **Form [P: lalitpur_tower c_03-c_10, 2023]:**
  * Square, ≈ **11 × 11 m**, red brick:
    * two lower skirt roofs on carved struts;
    * a brick storey with windows;
    * the **top belvedere wrapped in a continuous band of carved lattice windows** on all four sides;
    * a **broad square pyramidal tile roof** with deep eaves (≈ 16 m across) and a single gilt finial.
  * It rises two storeys above the quadrangle roofs [S]. Top ≈ **21 m** [E].
  * A **grey stone lion on a tall pedestal** stands at its south-west foot on the street (lalitpur c_02, c_04, c_06).
* **Views:**
  * **S/SE** (street): tall brick corner with the lion, the shop range to its right and the octagonal tower beyond
    (lalitpur c_04-c_08).
  * **E** (rooftops): the broad top roof between the nine-storey and octagonal towers (c_10).
  * **N/W:** from Lohan Chowk, the SE corner of the court.
* **Must have:** square tower closing the east end of the Lohan south facade with one broad top roof over a carved
  window band; lion on its pedestal at the foot.

### 3.16 S16 - Outer south facade on Basantapur, the white SW wing and the east shop range

* **Line:** the south facade runs along a line at bearing 100° through (10, -74.5) and (40, -79.5) [A] (it faces 190°;
  the shadow band on the square marks it), from the white SW wing (x ≈ -23, south face y ≈ -72) to the Lalitpur tower
  (x ≈ 45), and the shop range `w906985353` (23.4 × 8.5 m, centre (54.4, -74.0)) continues it east to x ≈ 66 [O/A].
  The white SW wing `w906935471` (13.1 × 16.8 m, (-16.6, -65.1), `historic=yes`) and `w910121098` (22.7 × 11.5 m,
  (-35.8, -63.1)) continue the frontage west to Gaddi Baithak [O].
* **Lohan south range [P: lohan_south c_00-c_11, basantapur_tower c_02, c_03, c_07, c_10, c_11, c_18, c_19]:**
  * Four storeys of brick with rows of carved windows and a carved central balcony.
  * Deep tile roof on struts.
  * A **black cast-iron railing** along its foot on a low plinth.
  * **Two grey stone lions** on tall pedestals: one at the Basantapur tower's SW corner and one further east
    (basantapur_tower c_19, lohan_south c_05, c_06).
* **East shop range [P: lalitpur c_04, c_05, c_08, 2023]:** two storeys (eave ≈ 8 m) under a deep hipped tile roof on
  struts. The ground floor is a row of shopfronts between carved wooden posts, with potted plants.
* **White SW wing [P: basantapur_tower c_02, c_03, c_07, c_10]:** three storeys of white plaster with regular windows in
  dark frames and green-teal louvred shutters, dark lattice windows at ground level, a hipped tile roof with a small
  white roof pavilion. Rana style.
* **Views:**
  * **From the south** (V5), the skyline reads left to right: Gaddi Baithak (white), white SW wing, nine-storey tower,
    brick range, Lalitpur tower, shop range (basantapur_tower c_02, c_27; gaddi c_12-c_15).
  * **From the east:** Lalitpur corner and shop range.
* **In the game now [G]:** generic boxes (`a_basantapur_north.png`).
* **Must have:** continuous brick range with carved windows and railing; two grey lions; white Rana wing with tile
  roof; shop range east of the Lalitpur tower; the tower skyline.

### 3.17 S17 - Gaddi Baithak (1908, Rana neoclassical)

* **OSM:**
  * Whole wing `w247501389` 46.4 × 36.3 m centred (-67.2, -49.7) [O].
  * Main hall `w430743883`: **41.4 × 16.3 m, h 21 m, red gabled roof along the long axis**, centred **(-67.5, -60.0)**
    = 27.703957 N, 85.306815 E.
  * West portico parts:
    * podium 17.6 × 8.4 m, h 4 (`w420797500`);
    * steps 13.4 m wide, h 4.4-5 (`w420797433/438/447/453`);
    * **six column shafts** 0.9 × 0.9 m to **16 m** at y ≈ -47.6, -48.6, -52.3, -56.4, -60.3, -61.3 along
      x ≈ -88..-90.5 (`w420797478..492`);
    * pedestals to 6.2 m (`w420797430/448/473/475`);
    * entablature 15.8 × 3.6 m to 16 m (`w420797416`);
    * attic 6.8 × 17.6 m from 14.5 to 18 m (`w420797474`) [O].
  * All parts line up with the 2026 image.
* **Status:** seismic restoration with the US Ambassadors Fund (grant 2016, completed) [S]; 2021-26 photos show it
  freshly white (gaddi c_06, c_21, c_23).
* **Form [P: gaddi c_00-c_23, hnulunche_west c_00-c_10]:**
  * **West portico** (towards Trailokya Mohan and Kumari Ghar, facing 281°):
    * giant columns grouped as pairs at the ends with two single columns between (six shafts), on tall pedestals over
      a **4-5 m podium** faced with a **pierced (quatrefoil) balustrade**, with stairs up from the square;
    * behind the columns, three bays with two storeys of **round-arched windows** with teal glazing;
    * a heavy entablature and an **attic with a segmental crest** at ≈ 18-21 m;
    * two cast-iron **candelabra lamp standards** on the podium corners.
  * **South facade** (on Basantapur, ≈ 41 m long, facing 191°):
    * the same giant order as **paired engaged columns** along the whole front;
    * two storeys of **tall arched windows** with teal-blue frames and doors (#2E6F86);
    * a balustrade along the podium and a **balustraded parapet** at the top with a small central gable;
    * the end bays project.
  * Colour: lime-white #F4F2EC, shade #D0CDC4. No other colour except teal windows and black lamp standards.
  * Roof: **dark red-brown pitched roof** (#7E3A34 in the 2026 image) behind the parapet, visible from a distance and
    from the air [O/A].
* **Views:**
  * **W** (V6): the temple-front portico on its podium (gaddi c_01, c_09, c_16).
  * **S** (Basantapur): the long colonnaded facade (gaddi c_06, c_10, c_12-c_15).
  * **SE:** both faces with the corner (gaddi c_00, c_04).
  * **N:** the Hnulunche range and Bhagwati beyond (hnulunche_west).
  * **E:** joins the white Lam block.
* **In the game now [G]:**
  * `PalaceRana` hero 46.4 × 36.3 m, 14 m, with portico and steps on the south and a flat white roof.
  * The recipe's Lat/Lon (27.70420, 85.30745) points into Nasal Chowk at (-4.9, -33.2). Placement uses the OSM
    match, but `FaunaGround` keep-outs read the Lat/Lon [G].
* **Must have:** west portico of six giant columns on a balustraded podium with stairs and lamp standards; south
  colonnade of paired giant columns over two storeys of arched windows; parapet with balustrade; red-brown roof behind;
  21 m to the parapet; pure white with teal glazing.

### 3.18 S18 - Hnulunche Chowk west facade and Bhagwati temple

* **Hnulunche west range:** `w420797472` 18.5 × 6.8 m, white, h 12, red gabled roof, centred (-83.3, -36.8) [O], north
  of the Gaddi portico.
  * Two storeys of white plaster with a **pierced balustrade parapet** and plain windows with teal shutters.
  * One **dark carved timber oriel** at first-floor centre.
  * A **red-framed round-arched gateway** at ground level [P: hnulunche_west c_00-c_10].
* **Bhagwati temple [P: bhagwati c_00-c_15]:** a **three-roof temple** riding on a long brick palace wing NW of Gaddi
  Baithak, ≈ (-80, -30) ±5 [A/V] = 27.704229 N, 85.306688 E. The wing (`w1415152800`, 29.9 × 6.4 m, brown, 3 levels
  [O] is a candidate) is lined with shopfronts and carved windows at street level, with a big tree behind. The roofs
  are dark with red valances and a gilt finial; top ≈ 22 m [E].
* **Views:**
  * **W** (from Maju Deval / Trailokya Mohan): the white parapeted range with the red arch beside the portico, and
    Bhagwati's roofs above the brick wing.
  * **N** (from the Big Bell side): the brick wing frontage.
* **Must have:** white parapeted range with oriel and red arched gate beside the portico; three-roof Bhagwati above the
  brick wing behind it.

### 3.19 S19 - Kumari Ghar (1757, Jaya Prakash Malla)

* **OSM:**
  * Building part `w429890388` (+ `w111984923`) **25.3 m (N-S) × 21.1 m (E-W)**, axes 10° / 100°, centred
    **(-102.3, -84.0)** = 27.703740 N, 85.306462 E, `building:colour #e1977a`.
  * Inner court ranges as skillion parts 12 m high: `w429890329` north range 21.0 × 4.4 at y -74; `w429890381`,
    `w429890382`, `w429890384`, `w429890386`, `w429890387`, `w429890326`, `w429890383`.
  * Multipolygon `r2688997` "Kumari House"; node `n2659104413` at (-96.2, -80.7) is 6.6 m off the centroid [O].
* **Orientation:** the main door is in the **north facade** (21 m wide, facing ≈ 10°) onto the square [O/P].
* **Form [P: mon-kds-temples c_kumari 00-15; kumari ref_00-ref_09; hanuman_dhoka c_36]:**
  * Three storeys plus attic on a brick plinth with a few steps; eave ≈ 11 m, ridge ≈ 14 m [E].
  * The north facade is brick (#A6563F) packed with **dark carved windows** (#3E2B20): small lattice windows in rows,
    three-bay windows at the centre, carved cornice bands.
  * Centre: a carved wooden door under a **gilt torana**, flanked by **two white stone lions** with red/green markings
    on pedestals.
  * Roof: hipped tile, mossy, with a **red fringed cloth valance** along the eave (#B5222A). At the ridge centre stands
    a **cluster of five gilt pinnacles** on a gilt base (three tall, two short, with leaf-shaped flanges; hanuman_dhoka
    c_36).
  * A narrow white-plastered corner pier with a yellow door adjoins on the east (c_kumari c_12, c_13).
  * Post-2015 photos show raking shores [V: model without].
* **Kumari Chowk (inner court):** a three-storey brick court about 12 × 13 m with richly carved windows, some painted
  in polychrome. The **gilt window** with a red valance where the Kumari appears is on the upper floor
  [P: c_kumari c_04; kumari ref_09]. LOD0 only.
* **Views:**
  * **N** (V8, from the Gaddi portico side): the carved facade with lions, torana and pinnacles (c_kumari c_00, c_14).
  * **E** (from Basantapur): the long side with the white corner pier and Gaddi at left (c_kumari c_08, c_12).
  * **S/W:** lanes and houses.
* **In the game now [G]:** `KumariGhar` hero 22 × 22 m, 3 storeys, 12 m; plain side windows and a yellow arch over the
  door (`hero_kumari_ghar.png`, `e_kumari_ne.png`).
* **Must have:** 25 × 21 m footprint at the OSM centroid with its 10° rotation; dense carved windows on the north
  facade; gilt torana door with white lions; red eave valance; five-pinnacle gilt cluster on the ridge.

### 3.20 S20 - Big Drums and the Kot area

* **Big Drums [P: mon-kds-temples c_bigdrum c_00-c_12]:**
  * A small **red-ochre brick pavilion** (#C4532E), its upper level an open bay ≈ 4 m wide where **two huge drums**
    lie on their sides (painted skins, red-and-white rims).
  * Carved brick cornice, tile roof with a **red fringed valance**; total ≈ 6 m high [E].
  * North of the Degu Taleju corner on the way to the Kot, ≈ (-5, 40) ±8 [V]; a small white shrine stands in front.
* **Kot:** the courtyard north of the palace front, a historic court of the Shah palace, now behind army and police
  buildings. The Hanuman Dhoka police station is `w196112325` (-61.2, 77.3) [O]. Low priority: keep its frontage
  plain, two storeys.
* **Must have:** drum house with two visible drums; nothing more needed for the Kot.

---

## 4. Small site elements

| Element | Where | Notes |
|---|---|---|
| Gate lions (pair, riders) | Gate (§3.1) | White with red stripes. |
| Singha Dhoka lions (pair) | Taleju west gate | White with red stripes. |
| Kumari Ghar lions (pair) | Kumari door | White. |
| Basantapur lions (pair, grey stone) | South facade, at the tower's SW corner and further east | Tall pedestals, unpainted. |
| Lalitpur lion | SW foot of the Lalitpur tower | Grey stone on a tall pedestal. |
| Lamp standards | Gaddi Baithak podium | Black cast-iron candelabra, 3-4 m. |
| Hanging brass lamps | Seto Bhairav screen | Two, on chains. |
| Stone flags, red bench, dado | Palace front | §3.3. |
| Iron railing | Basantapur south facade | Black, waist high. |
| Pipal tree | (34.8, 22.0) [O] | ≈ 24 m tall, broad crown; beside the Taleju plinth, never on it. |
| Potted plants | Nasal Chowk platform and arcade; shop range | Many. |
| Ticket booth (red-roofed kiosk) | Palace entrance area (outer_facades c_02) | Small tiled kiosk with a red frill [P]. |

---

## 5. Recommended build split

1. **Palace shell** (one generator): ranges as extruded bands following the zones of §2.2, with three facade
   languages:
   * Rana white: S3, S7 north and west, S16 wing, S18.
   * Newar brick with carved windows: S4 base, S12, S16, Mul and Mohan.
   * Gaddi neoclassical: S17.

   Deep tile roofs with struts everywhere except Gaddi.
2. **Rooftop temples and towers** as separate kit pieces placed on the shell: Degu Taleju, Agamchhen, Panch Mukhi,
   Bhagwati, Basantapur, Kirtipur, Bhaktapur, Lalitpur.
3. **Taleju** as its own hero with plinth stages, mini shrines, precinct wall and gate.
4. **Gate set** (gate, lions, Hanuman, inscription, Narasimha niche) and **Kumari Ghar** as their own heroes.
5. **Ground**: paving areas and plinths; remove the road ribbons and the generic buildings inside the palace outline
   (`w420797415`, `w1374620542`, `w906935471`, `w910121098`, `w910121099`, `w906985354`, `w906985353`, `w1117943799`
   and every unmapped plot inside the zones of §2.2).

---

## 6. Photo-matched viewpoints (for render-vs-photo checks)

| # | Camera (local), eye 1.6 m | Look | Match to |
|---|---|---|---|
| V1 | (-32, 4) | ESE (100°) to the gate | hanuman_gate c_14, c_15; agamchhen c_07, c_14 |
| V2 | (-24, 22) | SE (140°) | hanuman_gate c_06; sweta_bhairav c_05 |
| V3 | Nasal Chowk N (4, -14) | S (190°) | nasal_chowk c_08, c_16; kirtipur_tower c_01; basantapur_tower ref_01 |
| V4 | Nasal Chowk S (-4, -54) | NNE (20°) | panchamukhi c_07, c_10, c_11 |
| V5 | Basantapur square (-25, -100) | NE (45°) | basantapur_tower c_02, c_07, c_10; lohan_south c_00 |
| V6 | Square SW of Gaddi (-108, -62) | E (80°) | gaddi c_00, c_16; hnulunche_west c_00 |
| V7 | Makhan Tole (48, 92) | S (185°) | taleju c_02, c_08-c_10 |
| V8 | (-100, -62) | S (190°) to Kumari Ghar | c_kumari 00, 14 |
| V9 | Street SE of Lalitpur (58, -90) | NW (315°) | lalitpur_tower c_04, c_05 |
| V10 | Jagannath plinth (-22, 13) | E (100°) to the Seto Bhairav screen | sweta_bhairav c_06-c_08 |

---

## 7. Current game state (renders)

* `current/` (base a87e5a4, first pass): `a_basantapur_north`, `b_square_gate`, `c_taleju_w`, `d_gaddi_w`,
  `e_kumari_ne`, `f_top`, `g_top_palace`, `bird_sw` and the five hero close-ups `hero_*.png`. Findings in §0.1.
* `current97/` (base 97012ab, this pass): the same views re-rendered after the merge of the heritage, perf-life and
  assets-b work. See §7.1.

### 7.1 Re-render on 97012ab

How it was made:

* `GHUMANTE_PREVIEW_DIR=.../obj97 dotnet test core-tests --filter StreamingSceneTests.DumpStreetScenes` (tier 2,
  October).
* `current97/views.sh` repeats the first-pass views.
* `current97/views_photo.sh` renders the photo viewpoints of §6. Tile-local camera = (528.1 + x, ground + 1.6,
  288.9 + y).
* `sbs_1.jpg` and `sbs_2.jpg` put each render beside its photo.

What the game shows from each viewpoint:

| View | The game shows | The photo shows |
|---|---|---|
| V1 gate from forecourt | 5-storey glass-striped and red generic boxes where the gate and white palace front stand; a grey paved path | blue-and-gilt gate, white lions, red Hanuman under its umbrella, white wall with black windows |
| V2 gate from NW | the camera sits under Jagannath's eave (sibling hero); generic boxes beyond | gate surround and lions close up |
| V3 Nasal Chowk looking S | sandy ground, generic brick boxes all round, the small gate hero in the court, the tower hero behind a box in the Lohan court | Kirtipur copper roof centre, nine-storey tower right, red-post veranda and white shrine left |
| V4 Nasal Chowk looking N | a generic roofed box and brick blocks; no Panch Mukhi | white north range with yellow arcade and green shutters, five round roofs, platform |
| V5 Basantapur square NE | glass-striped block, white apartment, red boxes with blue awnings; the tower hero far behind | white SW wing, nine-storey tower, brick south range, Lalitpur tower |
| V6 Gaddi portico | white box with flat pilasters and green windows, flat roof, no portico on this face | six giant columns on a podium, arched windows, crest, lamp standards |
| V7 Taleju from N | 12 equal brick steps to a walled top, three yellow-gold roofs with red trims; no shop row, no mini shrines | shop row in front, mini shrines along the wall, dark copper roofs, gilt finial only |
| V8 Kumari Ghar from N | brick facade with plain dark windows, a yellow arch over the door, small lions, a red-white strip at the eave | dense carved windows, gilt torana, white lions, red valance, gilt pinnacle cluster |
| V9 Lalitpur from SE | generic boxes | Lalitpur tower with its lion, shop range, octagonal tower beyond |
| V10 Seto Bhairav | generic boxes; a pagoda far away | arched lattice screen with red fringe and lamps, Degu Taleju's three copper roofs above |

There is no visible change at this site between a87e5a4 and 97012ab:

* the same heroes in the same wrong places;
* the same generic boxes over the palace;
* the grey paved ring round the Jagannath group;
* the brown road ribbons along the palace's south side and diagonally across the forecourt (`f_top.png`,
  `g_top_palace.png`);
* the Taleju pipal tree standing at the plinth's south-west corner, its crown overhanging the lowest steps.

---

## 8. Open questions [V]

1. The exact gate bay: the brief places it 4.7 m north of the Dakha corner from photo spacing; a measured photo or
   site visit would fix it to ±0.5 m.
2. The Agamchhen 2026 state: under reconstruction or complete? (The brief models it complete.)
3. The Degu Taleju base size and the Seto Bhairav screen's exact bay; the Big Drums position.
4. Taleju plinth stage heights above stage 3 and the compound size (OSM maps only the first three stages).
5. Whether the timber shores are still on the Dakha range and Kumari Ghar in 2026.
6. Mohan Chowk corner turrets: form and height (closed court, no public photos).
7. Bhagwati's host wing (`w1415152800` or the range south of it).

---

## 9. Numbers at a glance

| Item | Value |
|---|---|
| Taleju | plinth 45 × 48 m, 12 stages, 16 mini shrines, roofs 33 / 25 / 18 m at eaves 12 / 19.5 / 26.5 m, 35 m |
| Basantapur tower | 13.8 × 13.4 m, 9 storeys, 4 roofs (top with 3 finials), 30 m, at (1.6, -66.7) |
| Kirtipur / Bhaktapur / Lalitpur towers | ≈ 23 / 19 / 21 m at (16.5, -42.5) / (40, -47.5) / (39.5, -71) |
| Gaddi Baithak | hall 41.4 × 16.3 m, 21 m; portico 6 columns to 16 m on a 4-5 m podium |
| Kumari Ghar | 25.3 × 21.1 m, ≈ 14 m ridge, door north |
| Nasal Chowk | 20 × 47 m at (4, -34), axis 12° |
| Lohan Chowk court | 11 × 17 m at (30.5, -57.5) |
| Mul Chowk court | 14 × 15 m at (37, -26.5) |
| Mohan Chowk court | 13 × 15 m at (11, 7) |
| Gate | ≈ 4.5 × 6 m surround at (-12.4, -2.4), facing 282° |
| Hanuman | pillar 1.6 m, figure 1.5 m, umbrella top 4.5 m, at (-12.9, 1.3) |
| Degu Taleju / Agamchhen / Panch Mukhi | ≈ 26 / 23 / 25 m |
| White palace ranges | eave ≈ 12 m, ridge ≈ 15 m |

---

## 10. Photo and source credits (reference only, not in the repo)

Commons file pages (author and licence on each page; folder `commons/<folder>/c_NN.jpg`):

* **hanuman_gate:**
  * Ganesh Paudel (CC BY-SA 3.0): Basantapur_palace_images_gp_(10/11/12).
  * Shulav Neupane (CC BY-SA 3.0): Entrance_basantapur.
  * Jorge Láscar (CC BY 2.0): Hanuman_statue_at_Hanumandhoka_-_Basantapur_Durbar_Square_(17807030456).
  * Junu131 (CC BY-SA 3.0): Hanuman_Statue_of_Hanuman_Dhoka_palace.
  * Clemensmarabu (CC BY-SA 3.0): HanumanDhokaKathmandu.
  * janak bhatta (CC BY 3.0): Idol_of_lord_Hahuman_-_panoramio.
  * Anup Raj Rai (CC BY-SA 4.0): In_front_of_Basantapur_15/16.
  * Bijaya Kumar Shrestha (CC BY-SA 4.0): Kathmandu_Durbar_Square_Primeses_08.
  * Gerd Eichmann (CC BY-SA 4.0): Kathmandu-Hanuman_Dhoka-Eingang-02-2014/04-2007/06-2013/08-2013-gje.
* **basantapur_tower:**
  * Nautalle_durbar_shirshak_baniya; Durbar_square_kathmandu_shirshak_baniya.
  * Basantapur_chwok_GP_(1/16/17).
  * Basantapur_Durbar_and_the_Nasal_Chok_courtyard_(17829885472).
  * Basantapur_Kathmandu_Nepal_(5118964555, 5119009253, 5119012021, 5119013189, 5119565132, 5119566550, 8528217811,
    8529286986).
  * Basantapur_Tower_(17829966162, 17847959822, 17848081782); Basantapur_Tower_Kathmandu_Durbar_Square_Nepal.
* **lohan_chowk / lohan_south / kirtipur_tower / lalitpur_tower:**
  * Basantapur_Tower_(17209209823, 17644940798).
  * Beautiful_panels_of_Hanuman_Dhoka_2023_January (im_02, im_03).
  * Hanuman_Dhoka_Durbar_Square_Kathmandu_89-93, 97, 100-103.
  * Basantapur_Dabali-_Basantpur_Durbar_Square,_Kathmandu_Nepal-0315..0318.
  * Lohan_Chowk,_outer_southern_facade; Side_view_of_Kathmandu_Durbar_Square; Hanumandhok_general_by_Sunil_Dongol.
  * Kathmandu-Hanuman_Dhoka-Nasal_Chowk-30-Basantapur_Bhavan-2013-gje; Royal_residence_tower_inside_Hanuman_Dhoka_Palace.
  * Kathmandu_Darbar_Square_193/204; Main_Entrance_of_Basantapur_Durbar_Square,_Kathmandu_Nepal-0306.
* **taleju:** Kathmandu-Taleju-12-Palast-2014-gje; 05._Taleju_temple; Basantapur_Durbar_Square_Premise_2026_26..31;
  Basantapur_Kathmandu_Nepal_(22..52) series; Basantapur_Kathmandu_Nepal_(5116136383).
* **singha_dhoka:** Kathmandu-Taleju-22-2013-gje; Entrance_basantapur_(2); Durbar_Square_Kathmandu,_Nepal_(3930991696);
  Kathmandu_Darbar0659.
* **gaddi / hnulunche_west:**
  * Gaddi_Baithak_(17824824076); 02._Gaddi_Baithak; Basanatpur_Durbar_Square_after_renovation; Basantapur_durbar_(2).
  * Basantapur_Durbar_Hanumandhoka_Durbar_Basantapur_Kathmandu_Nepal_Rajesh_Dhungana_(1,7,8,10,11).
  * Basantapur_Durbar_Square_Premise_2026_06; Gaddhi_baithak_GP1_(1/2); Gaddi_Baithak_Palace.
* **nasal_chowk / panchamukhi:**
  * 2014.12.09_Kathmandu_32/33_Durbar_Sq_Nasal_Chowk.
  * Architectural_Detail_-_Nasal_Chowk_-_Hanuman_Dhoka_01-03.
  * Couryard_of_Nasal_Chowk_-_Viewed_from_Nine-Storied_Tower.
  * Kathmandu-Hanuman_Dhoka-Nasal_Chowk-02..24-Pancamukhi_Hanuman_Mandir-gje (Gerd Eichmann, CC BY-SA 4.0).
* **dakha_north / outer_facades / agamchhen / sweta_bhairav / bhagwati / narasimha / inscription / hanuman_dhoka:**
  * Dakha_chowk_GP_(1-3); 02_Hanumandhoka_Palace_Area_08; Agama_chhe_temple_ktm_GP_(1-9).
  * Taleju_Bhawani_Hanumandhoka_Durbar_Square_Kathmandu_Nepal_Rajesh_Dhungnaa series.
  * Hanuman_Dhoka_Durbar_Square_Kathmandu_79/80; Kathmandu_Darbar_Square_Nepal_20241202_102/108/113 (Dec 2024).
  * Kathmandu-Bhagvati-04-2013-gje; Narsingha_inside_hanumandhoka; Narasimha.KTM.
  * Kathmandu-Hanuman_Dhoka-aussen-02-Inschrift_Pratapamallas-2013-gje (Gerd Eichmann).
  * Basantapur_Kathmandu_Nepal_(8529373640) (Kumari Ghar pinnacles).
* **Kumari Ghar and Big Drums** (sibling folders): see `/home/user/wt/refs/mon-kds-temples/c_kumari/credits.txt` and
  `.../c_bigdrum/credits.txt`.

Openverse (folder `<folder>/ref_NN.jpg`, credits.txt per folder):

* **basantapur_tower:** MATomlinson (CC BY-NC-SA 2.0, views from the tower); Artha at English Wikipedia (CC BY-SA 3.0,
  Basantapurpalace); markhorrell (CC BY-NC-SA 2.0); kasiat (CC BY-NC-SA 2.0); Mehraz Morshed (CC0, tower with carved
  windows); NettyA (CC BY-NC-ND 2.0, 1988).
* **ov_Hanuman_Dhoka_gate_Kathmandu / ov_Taleju_temple...:** eriktorner (CC BY-NC-SA 2.0, Taleju from the plinth);
  Jorge Láscar (CC BY 2.0); 3dom (CC BY-NC 2.0).
* **square_2024:** Steynard (CC BY-NC-SA 2.0, 2024-11-11 series); foxtail_1 (CC BY-NC-SA 2.0, Panch Mukhi and windows).
* **basantapur_square / ov_Lohan_Chowk_Kathmandu:** gorbulas_sandybanks (CC BY-NC-SA 2.0, Lohan and Nasal Chowk); terbeck.
* **kumari:** Matt-Zimmerman (CC BY 2.0); Gαurαv; randomix; chaostrophy (Kumari Chowk window).
* **nasal_chowk, panorama, hanuman_statue, palace:** scottgunn; Maesi64 (CC0); Gerd Eichmann; Adam Jones (CC BY-SA 2.0,
  panoramas); Jorge Láscar (Panch Mukhi); wonker; shankar s.
* The folder `degu_taleju/` holds Maju Deval photos (a search mismatch); ignore it for Degu Taleju.

Web sources:

* [Wikipedia: Hanuman Dhoka](https://en.wikipedia.org/wiki/Hanuman_Dhoka)
* [Wikipedia: Taleju Temple, Kathmandu](https://en.wikipedia.org/wiki/Taleju_Temple,_Kathmandu)
* [Wikipedia: Nautalle Durbar](https://en.wikipedia.org/wiki/Nautalle_Durbar)
* [Lonely Planet: Taleju Temple](https://www.lonelyplanet.com/nepal/kathmandu/attractions/taleju-temple/a/poi-sig/387081/357144)
* [Thamel.com: Around Kathmandu (towers, UNESCO mission notes)](https://www.thamel.com/destinationnepal/katmandu.html)
* [The Rising Nepal: Alluring Chowks of Hanumandhoka](https://risingnepaldaily.com/news/14269)
* [nepaltraveller: 10 facts about Taleju Bhawani](https://nepaltraveller.com/sidetrack/191)
* [The Himalayan Times: reconstruction of the nine-storey palace](https://thehimalayantimes.com/kathmandu/reconstruction-historic-nine-storey-palace-underway/)
* [China Daily: Chinese expertise helps restore damaged monument](https://global.chinadaily.com.cn/a/202502/05/WS67a2a4daa310a2ab06eaa1a0_2.html)
* [China Daily: Basantapur tower restored after eight-year project](https://www.chinaservicesinfo.com/s/202501/09/WS677f3d44498eec7e1f72ccd2/nepals-basantapur-tower-restored-after-eight-year-project.html)
* [nepaltraveller: reconstruction of 38 heritages completes at Basantapur](https://nepaltraveller.com/news/reconstruction-of-38-heritages-completes-at-basantapur)
* [AidData project 53714](https://china.aiddata.org/projects/53714)
* [Culture in Crisis: Gaddi Baithak restoration](https://cultureincrisis.org/projects/restoration-and-seismic-strengthening-of-the-early-20th-century-gaddi-baithak-in-kathmandu-durbar-square)
* [Miyamoto: Gaddi Baithak restoration](https://miyamotointernational.com/gaddi-baithak-restoration-project-us-embassy/)
* [NRICPT 2016: Aganchen temporary support](https://www.tobunken.go.jp/materials/?p=236780)
* [Onlinekhabar (Dec 2023): Agamchhen strengthening plan](https://www.onlinekhabar.com/?p=1412266)
* [thelongestwayhome: Basantapur Tower](https://www.thelongestwayhome.com/travel-guides/nepal/kathmandu/basantapur-tower-nine-story-palace.html)

Imagery: 2026 satellite tiles, Esri World Imagery and Bing Maps tiles (reference only, never copied). OSM data ©
OpenStreetMap contributors (ODbL).
