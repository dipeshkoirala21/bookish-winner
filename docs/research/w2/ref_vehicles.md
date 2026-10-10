# W2 detail pass: reference brief for the vehicles (package `vehicles`)

> Status: reference brief for the W2 detail pass (docs/W2_DETAIL_CONTRACT.md), 2026-10-10. It turns what actually
> drives in the Kathmandu Valley **today (2023-2026)** into modelling rules for `Core/Driving/VehicleMesher*.cs`.
> Fleet facts, class dimensions and plate rules come from [vehicles_traffic.md](vehicles_traffic.md) and W2_DESIGN
> §5.2; this file adds which models are on the street now, what their silhouettes look like, part proportions
> (cm), colours (hex, cartoon-boosted about +10 % saturation over the photo) and the details a Kathmandu local checks
> first. Photos were fetched with `/home/user/wt/refimg.py` (Openverse: Flickr and Wikimedia, open licences) into
> `/home/user/wt/refs/vehicles/<topic>/` and are **reference only**: never copied into the repo, never traced.
> Tags: [S] sourced (URL in §9), [E] estimate from photos, [V] verify with a reviewer.
>
> **Branding rule (ASSET_MANIFEST):** no logos, badges, model names, wordmarks, operator names or real liveries.
> Every model below is a *type* that blends the common features of the best-selling models of its class (the way GTA
> does), never a 1:1 copy of one protected design. Tank graphics are abstract stripes; grilles are generic slats.

## 0. What is on the road now (what the mesher must cover)

| Class | Street share (W2_DESIGN 5.2) | What a local sees in 2025-26 | Source |
|---|---|---|---|
| Commuter motorbike | ≈ 34 % of moving traffic | **Bajaj Pulsar 150** is the most imported motorcycle of 2025 (18,948 units), then **Royal Enfield Classic 350** (10,240), **Yamaha FZ** (8,318), **Pulsar 220F** (7,808), **Honda SP 125** (5,758), **TVS Apache RTR 160** (5,580); 125-350 cc dominates | [S] Meroauto 2025 imports |
| Scooter (petrol) | ≈ 21 % | **Honda Dio 125** led from April 2025, **TVS Ntorq 125** led Q1 2025, then **Yamaha Ray ZR 125 / Fascino**; Honda Activa family; 44 % Honda, 31 % TVS, 21 % Yamaha | [S] Meroauto scooter articles |
| E-scooter | ≈ 5 % (8 % in the core) | NIU, Yadea, Ather-style e-scooters: clean flat panels, round or bar LED lamps, no exhaust | [E] street photos |
| Cruiser | ≈ 2 % | Royal Enfield Classic 350 (and Hunter / Bullet) | [S] Meroauto |
| Car | ≈ 20 % | Hatchbacks (Swift, Grand i10 / i10 Nios, Alto K10, Tiago); **EVs are ≈ 73 % of new car sales (2025)**: BYD Dolphin, Atto 3, Atto 2, MG S5 / ZS EV, Tata Tigor EV, Punch EV, Nexon EV; SUVs Scorpio, Creta, Seltos, Land Cruiser Prado; Bolero and Hilux pickups | [S] Onlinekhabar EV share, Meroauto EV imports |
| Taxi | ≈ 4 % | small white or yellow hatchbacks (Alto / 800 / Santro / i10 class) with **black plates** and a roof "TAXI" sign; electric Tata taxis (Tigor/Xpres-T) since 2022 | [S] World Travel Guide, Rest of World, Onlinekhabar |
| Microbus | ≈ 7 % | **Toyota Hiace H200** white micros with a coloured belt, a Devanagari route board and a conductor at the sliding door; a growing share (up to 60 % on some entries) of **electric micros** (King Long-style one-box vans) | [S] Nepali Times, Kathmandu Post, Meroauto |
| Safa tempo | ≈ 3 % | white battery-electric three-wheelers with green trim, rear entry, two facing benches | [S] CEN, Himal |
| City bus | ≈ 1.5 % | **Sajha Yatayat** green buses: older front-engine diesel buses and **40 CHTC Kinwin electric low-floor buses** (36 seats, 200 km range), plus private minibuses on **Tata 709-type** chassis | [S] Himalayan Times, Onlinekhabar, Meroauto |
| Long-distance / tourist bus | ≈ 0.25 % | front-engine coaches on truck chassis, cream/white with **painted art bands**, a **roof rack with tarp-covered luggage**, a ladder at the back, mud flaps | [S] Nepali Times "Art and poetry in motion" |
| Truck | ≈ 1 % (night ×3) | **Tata LPT 1613-type** two-axle trucks, every one **hand painted**: crown headboard over the windscreen, "HORN PLEASE" at the back, chrome bumper strips, tassels | [S] Onlinekhabar "Tattooed trucks" |
| Tipper / tanker | ≈ 1 % | Tata-type tippers with orange boxes; private **drinking-water tankers** (blue or white tanks, "पिउने पानी" lettering) | [E] |
| Tractor | 0 % urban, 7 % peri-urban | **Mahindra 575 / Sonalika / Swaraj**-type 45-50 hp 2WD tractors in red, blue or orange with a two-wheel **trolley** | [S] khetiwadi spec page |

## 1. Two-wheelers (the bulk of the street, and what the player rides)

All two-wheelers: rear axle at z = 0, front axle at the wheelbase, rider hip at z ≈ 0.42 × wheelbase, seat top
0.78-0.81 m (bicycle 0.95 m) [VehicleSeats]. **Silhouette rule:** a real bike is *not* a stack of boxes: tank, tail and
side panels are sculpted, rounded volumes; the frame, forks, swingarm, exhaust, footpegs, levers and mirrors are thin
round parts with daylight between them. Every model has two round mirrors on stalks, front and rear indicators
(amber), a lit headlamp, a tail lamp, a number plate (rear two-line, front one-line), black rubber tyres with a visible
rim and a disc or drum brake.

### 1.1 Commuter "sport" (Pulsar 150 type) — the most common bike in Kathmandu
* Dimensions 205.5 × 76.5 × 106 cm, wheelbase 132 cm, seat 78.5 cm, 17" wheels (front 80/100, rear 100/90) [S autocarindia].
* Front: **bikini fairing** (small angular half-fairing round the headlamp) with the headlamp and **two pilot lamps
  ("wolf eyes")**, a short tinted visor on top; telescopic fork with black gaiters/upper tubes and silver lowers.
* Tank: muscular, wide at the knees, **tank shrouds** (extensions) sweeping down and forward past the radiator area,
  carrying the colour graphics; black frame and engine below.
* Seat: **split seat** — rider seat low, pillion step 6-8 cm higher; **split grab rails** (two black tubes).
* Tail: narrow, sharp, up-swept; LED tail lamp; long rear hugger/fender with the plate.
* Exhaust: side silencer on the right, rising toward the rear, black body with a silver/chrome tip and heat shield.
* Colours: black with red, blue or silver graphics; also solid red, blue. Engine and frame black, crankcase silver.
* Photos: refs/vehicles/pulsar150 (00 "Pulsar rider - Pan shot", Abhijith B.Rao, CC BY-NC-SA; 02 "Bajaj Pulsar 180
  DTS-i", helixblue, CC BY-NC; 07 "Bajaj Pulsar 200").

### 1.2 Commuter (Shine / SP 125 / Splendor type)
* 202 × 78.5 × 110 cm, wheelbase 128.5 cm, 18" wheels [E class].
* Front: **trapezoid headlamp in a cowl with a small visor**, indicators on stalks; long front fender.
* Tank: slim, rounded, tank-side panels with pinstripes; **long flat single seat**; black side panels under the seat.
* **Full chain cover**, a **rear carrier / grab rail** in chrome or black, round-ish tail lamp, chrome silencer with a
  heat shield along the right side.
* Colours: black with stripes, red, blue, grey, white.

### 1.3 Streetfighter (FZ / Apache RTR / Pulsar N type)
* 200-205 × 79 × 108-110 cm, wheelbase 133-136 cm, fat rear tyre (140/60-17).
* **Compact LED headlamp** with DRL "claws", very short; **big angular tank with large shrouds**; split seat; short
  sharp tail with LED lamp; **belly pan** under the engine; short stubby silencer (or twin-barrel); petal discs.
* Colours: matte black, blue, red, grey with neon accents.

### 1.4 Retro cruiser (Classic 350 type)
* 214.5 × 78.5 × 109 cm, wheelbase 139 cm, seat 80.5 cm, front 19" / rear 18" **wire-spoked** wheels [S Royal Enfield spec].
* **Round headlamp in a nacelle ("casquette") with two small pilot lamps**, chrome bezel; **teardrop tank** with
  rubber knee pads; **big valanced mudguards** front and rear; **sprung single saddle** (or one long seat) and a
  separate pillion pad; chrome **peashooter exhaust** running nearly straight back; upright wide handlebar; round
  mirrors; round tail lamp; tall upright single-cylinder engine with cooling fins.
* Colours: black, maroon-brown, gunmetal grey, desert sand; chrome everywhere.
* Photos: refs/vehicles/classic350 (00 Vinc3nt CC BY-SA; 01 Lakpura LLC CC BY; 03, 04).

### 1.5 Scooters (Dio / Ntorq / Activa / Ray ZR type)
* 181-187 × 70-72 × 115-116 cm, wheelbase 126-128.5 cm, 12" front / 10-12" rear wheels.
* **Step-through**: a front **apron (leg shield)** that curves round the front wheel, a flat **floorboard**, a
  bulbous **rear body** covering the engine, a long two-step seat over under-seat storage, a **swinging engine/CVT
  case on the left** with the silencer on the right, a front mudguard on the fork, grab rail behind the seat.
* Headlamp: **on the apron** (Dio: sharp V LED on the nose; Activa: chrome-garnished lamp; Ntorq: "jet" LED with DRL);
  the handlebar cover carries the speedometer and indicators.
* Tail: Ntorq and Dio taper to a **sharp tail** with an LED lamp; Activa is round and family-like.
* Colours: red, blue, matte grey/black, white, yellow; two-tone panels with black inner panels.

### 1.6 E-scooters (NIU / Yadea / Ather type)
* 180 × 70 × 110 cm. **Round LED ring headlamp** on the handlebar (NIU) or a slim bar on the apron (Ather);
  flat, clean panels; **no exhaust**, a hub motor in the rear wheel; often pastel or white with grey/black inner
  panels; a slim LED tail.

### 1.7 Bicycles
* **MTB (most common new bike):** 26-27.5" knobbly wheels, sloping top tube, front suspension fork, disc brakes,
  flat bar, black/blue/red frame with white graphics.
* **Roadster (Hero / Atlas type, the old workhorse):** black 28" roadster, **level top tube**, rod brakes, chain
  guard, **rear carrier**, full mudguards, swept-back handlebar with a bell, sprung saddle.
* Photos: refs/vehicles/bicycle; street_moto 02 (roadster rider in Kathmandu, World Bank Photo Collection CC BY-NC-ND).

### 1.8 Riders
* Riders **always wear a full-face or open-face helmet** (W2_DESIGN 5.2): black, white, red, blue with a dark visor;
  jacket or shirt, jeans, shoes; hands on the grips, feet on the pegs. Pillions common (not modelled in traffic).

## 2. Cars

All cars: right-hand drive, black **rubber mouldings**, black B-pillars, two mirrors on the doors, four (or five)
doors with visible shut lines, door handles, headlamps with a projector or LED DRL, a black lower grille, a number
plate front (0.45 × 0.11) and rear (0.30 × 0.185), wheels with alloy or steel+hubcap rims.

| Type (catalogue entry) | Proportions | What makes it read |
|---|---|---|
| **Swift type** (hatchback) | 386 × 173.5 × 152 cm, wb 245 | rounded "cab-forward" hatch, **wraparound headlamps**, oval grille, **black (floating) roof pillars**, high rear haunches, wraparound tail lamps, short overhangs |
| **i10 Nios / Tiago type** (hatchback) | 381.5 × 168 × 152 cm, wb 245 | tall-boy hatch, upright windscreen, **cascading grille with boomerang DRLs**, flatter roof, chunky tail lamps |
| **Alto type** (taxi / small hatch) | 353 × 149 × 152 cm, wb 238 | very small, **tall boxy greenhouse**, small headlamps, narrow grille slot, tiny 13" wheels |
| **Taxi (old 800 / Alto / Santro class)** | 340 × 148 × 148 | as Alto, **white (70 %) or yellow**, roof sign "TAXI" box (no text needed at far LODs), **black plate**, often a roof carrier |
| **Dolphin type** (EV) | 429 × 177 × 157, wb 270 | rounded hatch, **wave-like shoulder crease**, slim headlamps, no grille (closed nose with a lower intake), floating C-pillar |
| **Nexon / Punch EV type** (EV) | 399 × 181 × 162, wb 250 | compact SUV, **split lamps: slim LED DRL bar on top, main lamps lower**, black cladding on arches, floating roof, roof rails |
| **Atto 3 type** (EV) | 445 × 187 × 162, wb 272 | crossover, **one wide lamp bar across the nose**, closed grille, silver C-pillar "wave" trim, chunky black cladding |
| **Scorpio type** (SUV) | 446-466 × 182-192 × 186-198, wb 268 | tall boxy SUV, **upright vertical-slat grille**, tall flat bonnet, roof rails, side step, black arch cladding, vertical tail lamps |
| **Creta / Seltos type** (SUV) | 433 × 179 × 164, wb 261 | crossover with a big parametric grille, split lamps, roof rails, floating roof |
| **Land Cruiser Prado type** (SUV) | 484 × 188.5 × 189, wb 279 | large box, **big chrome grille**, upright greenhouse, rear-mounted spare wheel, side steps |
| **Bolero Pik-Up type** | 521 × 170 × 186.5, wb 326 | **flat-fronted single cab**, simple grille with vertical slats, tall flat bed with drop sides, white or silver |
| **Hilux type** (double cab) | 532 × 185.5 × 181.5, wb 308 | double cab, chrome grille, wide arches, short bed, roll bar |
| Police jeep | Bolero class | white with a blue band, blue light bar, "प्रहरी / POLICE" only as stripes at far LODs; no weapons [V] |
| Ambulance | Hiace / Eeco van | white with a red band and red/blue beacon; **no Red Cross emblem** |

## 3. Vans, tempos and buses

### 3.1 Microbus (Hiace H200 type) — the city's main public transport
* 469.5 × 169.5 × 198 cm, wheelbase 257 cm [S class]. **Semi-bonnet one-box**: a short sloping nose, a huge
  raked windscreen, headlamps high on the front corners, a horizontal grille below, a black bumper; a **sliding door
  on the left** (kerb side in Nepal), side windows all round, a rear tailgate with a big window.
* Nepal look: white with a **coloured belt stripe**, a **Devanagari route board** (cream with red text) high on the
  windscreen or the roof edge, a roof rack on many, a **conductor hanging at the open door** (not modelled).
* Electric micros (King Long type): the same box with a **smooth closed nose and a full-width lamp band**.

### 3.2 Safa tempo
* 360 × 145 × 190 cm, wheelbase 210 cm, three wheels (one in front) [E]. **Narrow driver cab** with a big flat
  windscreen and **one round headlamp** in the nose, a **wider passenger box** behind with open windows, a rear
  entry step with a grab rail, a flat roof with a luggage lip.
* White `#F4F4F0` body with **green `#2E8B57` stripes and lettering**, black skirt `#263238`, yellow route board.

### 3.3 Sajha green city bus
* 10.5 × 2.5 × 3.2 m. Older diesel: front engine, high floor, **flat front with a two-piece windscreen**, a front
  door on the left, **green body `#2E7D32` with a lighter green `#66BB6A` band and a white roof**. Electric
  (CHTC Kinwin): **low floor**, a big one-piece curved windscreen, two doors, roof-mounted battery pods, green and
  white. No operator logo, own stripe layout.

### 3.4 Minibus (Tata 709 type)
* 7.2 × 2.1 × 2.85 m. **Semi-forward cab with a short hood** in front of the windscreen, a split windscreen,
  headlamps in a chrome grille, a cream body with red/blue/orange stripes, **painted decoration on the front panel**
  (floral, a figure), a roof rack with luggage, a Devanagari route board.
* Photo: refs/vehicles/tourist_bus 06 "This Tata 709 model seems to rule the roads".

### 3.5 Long-distance and tourist coach
* 11 × 2.5 × 3.35 m + roof rack. Front-engine coach on a truck chassis: a tall flat front with a big windscreen,
  **art bands** (red, yellow, blue, green), floral flourishes, a roof rack with luggage under a blue/orange tarp,
  a ladder at the rear, mud flaps, chrome bumper.

### 3.6 School bus
* 8 × 2.3 × 3 m midi bus, **yellow `#FBC02D` (mandated)** with a black band, "SCHOOL BUS / विद्यालय बस" boards (plain
  boards at our LODs).

## 4. Trucks, tankers, tippers, tractors

### 4.1 Decorated truck (Tata LPT 1613 type)
* 8.1 × 2.45 × 3.3 m (headboard 3.6 m), wheelbase 4.8 m, single front axle, dual rear wheels.
* Cab: the classic **rounded-front Tata cab**: a wide two-piece windscreen, round headlamps in a full-width
  **chrome grille with horizontal bars**, rounded roof corners, a big front bumper painted in stripes.
* Nepal art (the reason locals smile): the cab is painted in strong colours (orange, red, blue, green), a
  **"crown" headboard** rises from the cargo body over the cab roof with a painted name panel; **painted panels**
  (flowers, eyes, mountains) on the cargo sides; **tassels and chains** hanging from the bumper; **mud flaps** with
  painted eyes; reflectors; "HORN PLEASE" on the tailgate (stripes only at far LODs) [S Onlinekhabar].
* Photos: refs/vehicles/truck_art (01 "Metamorphosis of the trusty truck", LilyinNepal CC BY-SA; 03 "Nepali
  decorated and colourful truck").

### 4.2 Tipper
* 7.6 × 2.5 × 3.1 m, tandem rear axle. Modern flat-fronted **cab-over** cab, **rectangular steel tipper box**
  (orange `#F57F17`) with a canopy over the cab and reinforcing ribs on the sides.

### 4.3 Water tanker
* 7.5 × 2.4 × 3.0 m. Old Tata cab, an **elliptical tank** (blue `#1E88E5` or white, often painted red/blue) with a
  manhole and a ladder, a rear valve, painted Devanagari "drinking water" lettering (a cream band at our LODs).
* Photo: refs/vehicles/tanker 00 "Water tankers getting refilled".

### 4.4 Tractor and trolley
* Mahindra 575 type: 357 × 198 × ~210 cm, wheelbase 194.5 cm, **rear 14.9-28 tyres (Ø ≈ 1.42 m) with deep chevron
  lugs**, front 6-16 (Ø ≈ 0.8 m) ribbed tyres [S khetiwadi].
* A **long bonnet** with a front grille and two lamps, an **upright exhaust stack** with a rain cap, an open
  platform, a seat with a backrest between **big rear fenders** with lamps, a steering wheel on a raked column, a
  drawbar hitch. Red `#C62828` (Mahindra / Sonalika), blue `#1565C0`, orange; **yellow rims** `#FDD835`; grey grille.
* Trolley: 3.6 × 1.9 × 1.1 m single-axle box with drop sides, blue or green.
* Photos: refs/vehicles/tractor (02 "On the double", 03 "Modern transport in Jomsom", apurdam CC BY-NC-ND).

## 5. Number plates
* Legacy painted Devanagari plates (W2_DESIGN 5.2): **private red / white text**, **public black / white**
  (taxis, micros, buses, trucks, tempos), government white / red, tourist green / white. Two-wheelers carry a small
  front plate on the mudguard or the headlamp cowl and a two-line rear plate on the rear hugger.

## 6. Colour palette (cartoon, hex)

| Part | Hex |
|---|---|
| Tyre rubber | `#2A2C30` |
| Black trim, grilles, cladding | `#26292E` |
| Chrome / polished | `#D9DEE3` |
| Silver alloy | `#B8BEC4`; dark alloy `#4A4F55` |
| Glass (tinted) | `#2E4456`, windscreen `#3B566B` |
| Headlamp lens | `#F4F1E0`; DRL `#FFFFFF` |
| Tail lamp | `#D32F2F`; indicator `#FFA000` |
| Seat | `#2B2B2E` |
| Engine | crankcase `#AEB4BA`, fins `#5E646B`, black `#2B2E33` |

## 7. Level of detail (per W2_DESIGN 10.4, raised where justified)
Budgets are per vehicle **with its wheels and rider** (`VehicleMesher.Budget(lod, shape)`, tested for every model
type and livery by `core-tests/VehicleMeshTests`):

| Family | LOD0 | LOD1 | LOD2 |
|---|---|---|---|
| Two-wheelers and the rickshaw | 7 000 | 2 200 | 700 |
| Cars, taxis, SUVs, pickups, vans, tempo | 7 500 | 2 400 | 900 |
| Buses, trucks, tippers, tankers, tractor | 9 000 | 2 600 | 900 |

* LOD0 (player and the nearest one to three agents): everything above, numbered plates. W2_DESIGN 10.4 gives the
  player 6 k; the two-wheeler cap is 7 k because the helmeted traffic rider alone is ≈ 0.9 k and the Classic-type
  bike needs its spoked wheels, chrome lamp and springs to read. Buses and trucks fill the screen and at most one is
  at LOD0.
* LOD1 (25-80 m): same silhouette, small parts dropped (levers, cables, nuts, wipers, mirrors' glass), small
  rounded parts become plain boxes, cladding and fenders use a plain section, blank plates; wheels are a 5-point
  tyre on 10-14 segments with a rim face and the style's spokes or lugs (10.4 assumes ≈ 2 k).
* LOD2: rounded silhouette, wheels baked, rider as boxes, 700-900 (10.4 assumes ≈ 600). Most LOD2 agents are
  two-wheelers at 650-700.
* **Slice check (open issue for the 10.4 owner):** 10.4's worked count for Low is player 6 k + 1 × 2 k + 6 × 0.6 k
  ≈ 11.6 k moving. With these caps a typical Low frame (player on a bike 7 k, one LOD1 car 2.4 k, six LOD2
  two-wheelers 4.2 k) is ≈ 13.6 k, about 2 k over; the worst case (heavy vehicles at every level) is ≈ 3.5 k over.
  Mid and High have the headroom (34 k / 56 k worked counts against 45 k / 75 k slices).
* Block-out ≤ 80 and box 12 for parked vehicles (W2_DESIGN 5.2).

## 8. Modal mix (unchanged, W2_DESIGN 5.2)
Two-wheelers dominate (55-72 % per road class); within them scooter : commuter : cruiser : e-scooter ≈ 35 : 55 : 3 : 7.
The model *type* inside a catalogue entry is picked from the agent id by the weights in `VehicleMesher.ModelFor`
(e.g. commuter: Pulsar type 40 %, Shine/SP type 25 %, streetfighter 35 %).

## 9. Sources
* Meroauto, "Top five motorcycles by import volume in Nepal in 2025": https://www.en.meroauto.com/top-five-motorcycles-by-import-volume-in-nepal-in-2025/
* Meroauto, "TVS races ahead of Honda in Nepal's scooter battle": https://www.en.meroauto.com/tvs-races-ahead-of-honda-in-nepals-scooter-battle/
* Meroauto, "Honda dominates Nepal's scooter market with 44% market share": https://www.en.meroauto.com/honda-dominates-nepals-scooter-market-with-44-market-share/
* Meroauto, "Five most imported electric cars of 2024/25": https://www.en.meroauto.com/five-most-imported-electric-cars-of-2024-25-with-list/
* Onlinekhabar, "Nepal ranks second globally in EV sales": https://english.onlinekhabar.com/nepal-ranks-second-globally-in-ev-sales.html
* Meroauto, "Chinese brands dominate EV sales, but Tata's Tigor EV takes the crown": https://www.en.meroauto.com/tata-takes-the-crown-as-chinese-evs-rule-the-road-in-numbers/
* Onlinekhabar, "Electric taxis on Kathmandu roads": https://english.onlinekhabar.com/electric-taxis-on-kathmandu-roads.html
* Himalayan Times, "Sajha Yatayat inks pact to procure 40 electric buses": https://thehimalayantimes.com/kathmandu/sajha-yatayat-inks-pact-to-procure-40-electric-buses
* Meroauto, "Sajha Yatayat expands electric fleet with five new microbuses for Lalitpur": https://www.en.meroauto.com/sajha-yatayat-expands-electric-fleet-with-five-new-microbuses-for-lalitpur/
* Autocar India, Bajaj Pulsar 150 specifications: https://www.autocarindia.com/bikes/bajaj/pulsar-150/specifications
* Royal Enfield, New Classic 350 spec sheet: https://www.royalenfield.com/content/dam/open-pdf/royal-enfield-new-classic-350.pdf
* Khetiwadi, Mahindra 575 DI: https://www.khetiwadi.com/tractor/detail/9/mahindra-575-di
* Photo credits: see `credits.txt` in each `/home/user/wt/refs/vehicles/<topic>/` folder (title, author, licence, URL).
