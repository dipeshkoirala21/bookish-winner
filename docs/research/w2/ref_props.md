# Reference brief: street furniture (package **props**)

> What a Kathmandu local sees on the street today (2024-2026), measured from open-licensed photos (Flickr and Wikimedia Commons via Openverse, Commons directly) and from news photos of the latest municipal projects, written so the prop kit in `game/Assets/Ghumante/Core/Generators/Props/StreetPropMesher.cs` (drawn by `World/Instancing/KitMeshes.Prop`) can be checked against it. Photos were viewed for reference only and are **not** in the repo (they live in `/home/user/wt/refs/props/`). Side-by-side renders are in `/home/user/wt/previews/props/`. Branding rule (ASSET_MANIFEST): no logos, brand names or text; signs carry pictograms only.
>
> Tags: **[P]** read off a photo (credit in §9), **[S]** a written source (§9), **[E]** an estimate or a design choice.

## 0. What is actually on the street now

| Object | What the first pass (W1 kit) drew | What Kathmandu has today |
|---|---|---|
| Street lamp | a 6-sided frustum with a box head | three families: (1) the **grid lamp** of the arterials (Kantipath, Araniko Highway, Koteshwor, Ring Road): a tall galvanised round pole whose top **bends over in one smooth swan-neck** into a near-horizontal arm with a **flat LED head** [P kp_04, c_00]; (2) the **solar lamp** (150 for the 2014 SAARC summit on Ratnapark-Baneshwor-Maitighar-Thapathali [S], 1,085 more by KMC [S]): a lighter pole with a **tilted solar panel on top**, a short straight inclined arm and a **battery box** on the pole [P c_00, crop_c00_lamp]; (3) the **heritage lantern lamps** KMC put on New Road / Shukrapath (Indrachowk-Bishal Bazar, June 2026 [S]) and Durbar Marg (July 2026 [S]): black poles with gold collars, scroll arms and hanging bell lanterns [P shukrapath 04, 05, 08, 10] |
| Bus stop sign | a box sign on a 6-sided post | a white post with a **blue square sign** carrying a white bus pictogram and a small plate under it [P kp_04 crop] |
| Bus shelter | box posts and a flat blue box roof | KMC's new stops are **falcha-style pavilions** (policy since FY 2079/80, first one at Baneshwor on the Araniko Highway, 14 May 2024; more than 30 at Kalanki, Babarmahal, Basundhara, Bishalnagar, Minbhawan, Tinkune, Chabahil, Baluwatar, Sinamangal, Koteshwor by Nov 2025 [S]): carved dark wooden columns, a **hipped jhingati tile roof**, a red frilled valance, lattice frieze, benches for 20-22 people [P falcha f1-f8]. Older ones are donated concrete "waiting points" with a stepped gable board [P bus_stop 03] |
| Traffic signal | a box head | a grey pole with a **three-aspect head** (dark housing, light-rimmed back plate, hooded lenses) [P signal 00, kp_04]; KMC added **push-button pedestrian signals** at 36 places in 2025 [S] |
| Power pole | a 4-sided frustum with one bar | NEA **square-tapered pre-stressed concrete poles** 9-11 m, an 11 kV cross-arm with pin insulators on top, an LT rack below, and the famous **black coils and loops of slack telecom cable** at 4-6 m [P power_lines 05, 07, wires 00]; distribution transformers on **two-pole H-frames** [P power_lines 07] |
| Water tap | a stone box with a bar | a community standpost: a **GI pipe with a brass tap** over a **concrete apron**, plastic jerrycans queued [P tap 00] |
| Bench | a box seat | cast-iron end frames with **wooden slats**; New Road's new benches are **green** [P bench 01, 06; shukrapath 04] |
| Gate | three boxes making an arch | compound gates: two plastered pillars with caps and **steel grille leaves**; drawn open, with nothing overhead (the 4.5 m clearance rule) [P gate; E] |

## 1. Grid street lamp (`StreetLamp`, 9-12 m, the generated default on arterials and local roads)

Photos: kp_04 (Bijay Chaurasia), kp_01 (Deejay2005), c_00 (Bijay Chaurasia), signal 07 (anantal).

* **Silhouette**: a thin vertical line that turns over at the top like a shepherd's crook flattened out: the bend radius is about 1/8 of the pole height, the arm runs out 1.5-1.8 m and rises 5-8 degrees; the head is a slim flat lozenge, not a box [P].
* **Proportions** (10 m pole): base Ø 0.20 m tapering to 0.10 m at the bend [E from P]; a round concrete footing Ø 0.5 m, 0.2 m high [P kp_04, E]; a steel base flange and a small access door at 0.6 m [E]; bend radius 1.2 m; arm reach 1.75 m from the pole axis; LED head 0.65 × 0.28 × 0.09 m with a pale lens underneath [P c_00].
* **Colours**: galvanised grey `#9DA3A6` (Metal), footing concrete `#A9A59C` (Concrete), head body `#6A7076`, lens warm white `#FFF1C9` (Glass) [P].
* **Placement**: 10-12 m every 30-35 m on arterials, 9-10 m every 25-30 m on local roads, alternating sides at the kerb (NURS Table 14, roads.md §11) [S].

## 2. Solar street lamp (`StreetLampSolar`, 7-8.5 m)

Photos: c_00 and its crop (Bijay Chaurasia, Koteshwor 2019); ornaments brief §2 (Maitighar).

* **Silhouette**: a pole topped by a **big tilted dark-blue panel** (reads as a flat square "hat"), a short straight arm at about 15 degrees up with a slim head, and a box halfway up the pole [P].
* **Proportions** (8 m): pole Ø 0.15 → 0.08 m, light grey `#C9CCCB` (Paint); panel 1.15 × 0.7 m tilted 25 degrees on a short stub, aluminium frame `#B8BCBE`, cells `#1F3A6B` (Glass); arm 1.3 m long; head 0.55 × 0.22 × 0.07 m; battery box 0.32 × 0.45 × 0.16 m at 0.4 of the height [P/E].
* **Placement**: about one road in three (by way id) gets solar lamps instead of grid lamps, a little lower (7-8.5 m) [E: the SAARC and KMC solar programmes covered the main city roads].

## 3. Heritage lantern lamp (`StreetLampHeritage`, 5.6-6.4 m)

Photos: shukrapath 00-13 (Ratopati, 27 June 2026, New Road / Shukrapath); the Himalayan Times gallery of 14 July 2026 describes the same "vintage-style" lights on New Road and Durbar Marg [S].

* **Silhouette**: a slim black pole with **gold rings**, a **scroll arm** to one side about 0.7 of the height (a quarter circle out and over, with a spiral curl underneath carrying a gold rosette) and a **hanging lantern** at its tip, and a second lantern on a short crook at the very top on the other side [P 08, 10].
* **Proportions**: pole Ø 0.11 m, 6 m; a flared **gold base collar** Ø 0.24 m × 0.3 m; gold rings at 1.2 m (a vase knop 0.25 m tall), 3.0 m, under the arm and at the top [P 04, 08]; scroll arm reach 0.85 m, its curl Ø 0.35 m; lantern: a black **bell hood** Ø 0.42 m (wide brim, 0.18 m tall) with a gold finial, a clear ribbed **glass bowl** Ø 0.2 × 0.25 m below [P 10].
* **Colours**: black `#1C1D1F` (Metal), gold `#C9A23A` (Gilt), glass `#F4EBD0` (Glass) [P].
* **Placement**: the named heritage streets (New Road / Juddha Sadak / Shukrapath, Durbar Marg) at both kerbs, staggered, about every 20 m, even in the old core [P: the photos show a tight rhythm on both sides].

## 4. Bus stop sign (`BusStop`, 2.7 m) and taxi stand sign (`TaxiStand`)

Photos: kp_04 crop (blue sign by the kerb on Kantipath), c_00 (green "public bus" direction plates at Koteshwor).

* **Post**: white `#E8E8E4` round post Ø 0.06 m (Paint) on a small concrete footing; a **blue** `#1F5FAF` square sign 0.6 × 0.6 m with rounded corners and a white border, a white **bus pictogram** (rounded body, three windows, two wheels) raised on it; a small white plate under it (blank: no text) [P/E].
* The taxi stand uses the same post with a **car pictogram** (Kathmandu's taxis are the small black hatchbacks with a roof sign, ref_vehicles.md).

## 5. Falcha bus shelter (`BusShelter`)

Photos: falcha f1-f8 (Kantipur, May 2024 Baneshwor; July 2025 Babarmahal, Tinkune and others).

* **Plan**: 6.0 × 3.6 m (KMC: 13 ft wide, 21 ft long [S]); a 0.25 m stone-faced plinth with a front step [P f6, f8].
* **Columns**: two rows of five dark walnut `#3A2416` (WoodCarved) carved columns, 2.3 m clear, turned profiles (a base block, a fluted shaft, a bracket capital) [P f4, f7].
* **Roof**: a **hipped** roof of terracotta jhingati tiles `#A8553A` (RoofTile), ridge about 4.0 m, eaves 0.55 m beyond the columns at 2.7 m, slightly flared; a **red frilled valance** `#C0282D` (Fabric) with a black edge hangs round all four eaves [P f1, f2, f6]; a lattice frieze (WoodCarved) between the beam and the eaves [P f2, f4].
* **Benches**: long wooden benches between the back columns and along the ends [P f6].
* Placement: OSM `amenity=shelter`, long side along the nearest road, the open front at the kerb [E].

## 6. Traffic signal (`TrafficSignal`, 4.5 m)

Photos: signal 00 (World Bank), kp_04 crop, c_00 (Koteshwor gantry).

* Grey galvanised pole Ø 0.12 m (`#8E9497`, Metal) on a concrete footing; a three-aspect head 0.34 × 1.0 × 0.24 m, housing `#2A2D31`, a **back plate with a white rim** `#E8E8E4`; three Ø 0.2 m lenses red `#E23B2E`, amber `#F2B01E`, green `#27B05A` (Glass) under tubular **visors** [P]; a yellow push-button box `#F2C230` at 1.1 m and a small two-lens pedestrian head at 2.6 m [S: KMC push-button signals, 2025].
* Placement: OSM signals and generated heads stand at a **corner of the junction**, outside both carriageways, facing the traffic of the widest arm [E].

## 7. Utility poles (`PowerPole` 9 m, `TransformerPole`)

Photos: power_lines 05 (NEPMET, Dharahara), 07 (CS Sharada Prasad), wires 00 (RobiNZ), c_02 (Sandeep Raut, Ring Road), street_life.md §5.1.

* **Pole**: square-tapered pre-stressed concrete 0.24 → 0.12 m, light grey `#B3AFA6` darkening to `#7D7A73` at the foot (Concrete) [S street_life]; a red/white number band at 2.5 m [P wires 00].
* **Top**: a galvanised channel cross-arm 1.5 m with three brown porcelain pin insulators `#7A4E33` [P 05]; an LT rack with four shackle insulators at 7.3 m [S/E].
* **The tangle**: two or three **coils of black cable** (Ø 0.5-0.8 m) hung on the pole at 4.5-6 m, a fat black bundle clamped down the pole, a slack **loop** hanging below the coils, and two small grey ISP junction boxes [P wires 00, power_lines 07]. Spans to the next pole are not part of the prop (the kit has no pole-to-pole wires; open issue).
* **Transformer**: two poles 2.4 m apart (an H-frame) with a channel platform at 4 m carrying a grey `#7E8A8C` tank with radiator fins, three HT bushings and LT bushings; drop-out fuses on the top arm; LT cables down to a box at 1.5 m; a red danger plate [P 07, E]. Chosen for about one urban OSM power pole in six (OSM maps transformers badly) [E].

## 8. Bench, water tap, gate, tank and the rest

* **Bench** (`Bench`, 0.85 m): 1.6 m long, two cast-iron end frames `#2E4A3A` (Metal) with scrolled arms, four seat and three back slats `#8A5A32` (Wood) [P bench 01, 06; shukrapath 04].
* **Water tap** (`WaterTap`, 1.0 m): concrete apron 1.0 × 0.8 m with a lip, a small brick-and-plaster pedestal, a GI pipe `#8C949C` to 0.95 m with a gooseneck and a brass tap `#B8902F`; a yellow jerrycan on the apron [P tap 00].
* **Gate** (`Gate`, 3.2 m): pillars 0.55 m square, 2.7 m, cream plaster `#E7DCC4` with a brick-red plinth band and a small hipped tile cap; dark green `#2F5D46` steel grille leaves opened back along the road; pillars at ±2.95 m so a 4.8 m corridor passes between them; **nothing overhead** [E, W2 decision 2].
* **Storage tank** (`StorageTank`): the black/blue 1,000 L ribbed polyethylene tank Ø 1.1 × 1.2 m on a steel angle stand (street_life §5.4) [S]; tinted per instance.
* **Windsock, helipad, aeroway gate, parking position** (TIA): an orange-and-white striped sock on a hoop and a white mast with red bands; a concrete pad with the white H and circle; yellow gate sign; yellow stand markings [E from aviation.md].
* **Mast** (`Mast`, 25 m): a tapered red-and-white monopole with antenna panels and a dish [E].
* **Power tower** (`PowerTower`, 30 m): a four-legged lattice tower with cross-arms and insulator strings [E].
* **Well** (`Well`): a round brick parapet Ø 1.6 m with a stone coping and dark water, a wooden pulley frame [E, Newar inar].
* **Solar panel** (`SolarPanel`): a 2 × 1.2 m panel tilted south on an aluminium frame [E].
* **Chimney** (`Chimney`): a brick-kiln chimney, round tapered brick with steel bands and a sooty top (the valley's FCBTK kilns) [E].
* **Artwork** (`Artwork`): a bronze standing figure on a moulded stone pedestal (most public art in the valley is a statue) [E].

## 9. Sources and photo credits

Written sources [S]:
* KMC falcha bus stops: Rising Nepal (risingnepaldaily.com/news/62611); ekantipur 14 May 2024, 29 July 2025, 19 Nov 2025.
* Heritage lamps: Ratopati 28 June 2026 (Shukrapath, Indrachowk-Bishal Bazar); The Himalayan Times photo gallery 14 July 2026 (New Road, Durbar Marg).
* Solar lamps: Kathmandu Post 28 Sep 2014; Onlinekhabar 15 Jan 2016 (Ratnapark-New Baneshwor SAARC lamps); MyRepublica (1,085 KMC solar lights).
* Median smart lamps on poles, Koteshwor-Suryabinayak: The Himalayan Times; high-mast lights at 62 intersections: Himal Press 19 Oct 2025; Koteshwor-Kalanki street lights begun June 2026: Hamro Patro.
* Push-button pedestrian signals (36 places, 2025): Kathmandu Post 31 Jan 2025.
* Poles and wires: street_life.md §5.1 and its sources.

Photos [P] (reference only):
* kp_01 Deejay2005 "Kantipath" CC BY-NC-ND 2.0; kp_04 Bijay Chaurasia "Kantipath road, Kathmandu" CC BY-SA 4.0.
* c_00 Bijay Chaurasia "Koteshwor, Kathmandu" (2019) CC BY-SA 4.0; c_02 Sandeep Raut "Kathmandu Ring Road during Lockdown 02" CC BY-SA 4.0.
* signal 00 World Bank Photo Collection "Street traffic in Kathmandu" CC BY-NC-ND 2.0; signal 07 anantal "Empty road ... SAARC Summit 2014" CC BY 2.0.
* bus_stop 03 Pacific Air Forces "120907-F-CP197-866" CC BY 2.0.
* power_lines 05 NEPMET "Dharahara" CC BY 2.0; power_lines 07 CS Sharada Prasad "Fixing Power Lines" CC BY 2.0; wires 00 RobiNZ "Kathmandu, wiring diagram?" CC BY-NC 2.0.
* tap 00 water.alternatives "Fetching water, Kathmandu" CC BY-NC 2.0.
* bench 01 Wonderlane (Boudha) CC BY 2.0; bench 06 Francisco Anzola "Bench outside Kumari House" CC BY 2.0.
* shukrapath 00-13 Ratopati / Nepal Photo Library (news photos, viewed only); falcha f1-f8 Kantipur (news photos, viewed only).
