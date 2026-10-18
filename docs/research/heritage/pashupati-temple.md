# Heritage brief: Pashupatinath Temple, main temple and its courtyard (`pashupati-temple`)

> Research brief for the bespoke, structure-by-structure model of the Pashupatinath main temple, its inner courtyard, the
> west main gate and its forecourt, the east gate and stairs, and the buildings that ring the courtyard. Written 2026-10-11
> for the monument round that answers the owner's "Pashupatinath had no detail ... looked like some random temple".
> The river front (ghats, Arya Ghat, bridges, the east-bank terraces and the Pandra Shivalaya) is the sibling brief
> `pashupati-river`; this brief describes only what is seen of it from the courtyard and the classic view.
>
> Confidence tags, as in docs/research/w2/temples.md: **[O]** measured from OpenStreetMap (snapshot of 2026-10-04 in
> `pipeline/data/raw/osm/nepal.osm.pbf`), **[S]** stated by a cited source, **[P]** read from the cited photographs
> (scale from people, steps and known sizes), **[E]** estimate, **[V]** verify with a person who knows the inside of the
> courtyard (photography is not allowed inside it, so the public photo record is thin there).
> Reference photos are reference only: nothing is copied into the repository. Local copies for the modellers are under
> `/home/user/wt/refs/mon-pashupati-temple/` (credits in each folder's `credits.txt`, and in §9).

## 0. Summary

* **What it is.** A square two-roofed Newar pagoda (the present building is from 1692-1697, King Bhupalendra Malla
  [S: PADT, Wikipedia]) standing on a single low plinth in the middle of a cramped paved courtyard, 23.6-23.7 m to the tip
  of its finial [S]. Both roofs are gilded copper with standing-seam ribs; the four doors (one on each side) are sheathed
  in silver and gilt, with gilt toranas; the struts are carved wood, and between them hang slanting dark wooden lattice
  screens that make the underside of both roofs read as black bands. A large gilt finial (bell, lotus, pot, parasol) with
  four corner pinnacles and a separate gilt trident on a pole crowns it. A huge gilt **Nandi** bull on a vermilion
  pedestal lies before the **west** door, on the axis of the west main gate, so the famous view from outside the gate is
  the Nandi's gilt rump framed by the gate with the silver west door behind [S][P].
* **Orientation.** The whole compound is turned: the temple's sides run at **36.7° / 126.7°** [O]. The main ("west")
  door faces **306.7°** (north-west by west), the east door 126.7° to the Bagmati. This brief uses a temple frame:
  **a** = metres toward "compound east" (126.7°), **b** = metres toward "compound north" (36.7°), origin the temple
  centroid = game (532963.93, 165857.51) [O]. Game x = 532963.93 + 0.8016 a + 0.5976 b, game z = 165857.51 − 0.5976 a
  + 0.8016 b. The anchor sits 30 m south of the level-10 tile edge z = 165888: the courtyard spans tiles (520,161) and
  (520,162).
* **Around it.** West: the cream two-storey **West Gate** pavilion with a big painted blue Shiva panel, opening from a
  flagstone forecourt reached from Gaushala. East: a cream gate with a clock face and gilt finials at the top of a stone
  stair from the river. South-east: the **Mukti Mandap**, a dark wooden arcaded pavilion under a grey metal roof on top of
  a white riverside block with a red-and-white crenellated parapet. North-east: the small two-roofed **Vasuki** temple
  with verdigris (green) copper roofs. South: a dark two-roofed **Bhairav** temple and, further, a grey octagonal
  three-roofed temple. White, cream and red-brick buildings of 2-3 storeys with pitched rust-red metal roofs close the
  courtyard on every side.
* **What the game shows now** (rendered from the sample pack, §6): the hero is a generic two-tier gilt pagoda on a wide
  red brick slab with a colonnade of thin posts, a big stair with stone lions and lamp pillars, a pataka strip down both
  roofs, a small Nandi crowded against the stair foot, and a plain stacked-bulb finial. Every other building in the site
  is a 4-6 storey flat-roofed "modern urban" block in candy colours (blue, purple, green, pink), the Bhairav temple and the
  Mukti Mandap included; the West Gate is a brown gabled "hut" with a thatch roof; the six small shrines are flat boxes;
  the Vasuki temple, the east gate, the stairs, the kings' pillars, the trident, the bells and the lamp pillars are
  missing. That, not the hero alone, is why it "looked like some random temple".

## 1. Sources

* OSM objects (ids in §4), extracted with pyosmium from the pack's source snapshot and expressed in the temple frame
  (scripts were scratch; results in §4 and the plan image).
* Esri World Imagery (z19, about 0.26 m/px) as an orientation check of the courtyard only (the imagery sits about 3 m
  east of OSM here); not copied.
* Text: Wikipedia "Pashupatinath Temple"; Pashupati Area Development Trust (PADT) pages (pashupati.org.np/pasupatinath-temple,
  /monuments); sacred-destinations.com (Pashupati, the most detailed courtyard description); thelongestwayhome.com guide;
  hopnepal.com and bestheritagetour.com (lists of the inner-courtyard shrines); mountainsoftravelphotos.com (west gate,
  golden door, Nandi age); Nepali Wikipedia; The Himalayan Times (2021 gilding plan); Nepalica archive (VS 1941 order on
  the trust of the golden bull). URLs in §9.
* Photos: Openverse (Flickr CC) and Wikimedia Commons (CC BY-SA), listed with credits in §9. About 25 are load-bearing
  for this brief; their local file names are cited as `refs/<folder>/<file>`.

## 2. Site plan

Plan images in `/home/user/wt/previews/mon-pashupati-temple/` (not in the repository): `site_plan.png` (OSM outlines in
the temple frame, 10 m grid, structure IDs, estimated points for features OSM lacks), `osm_ids_temple_frame.png` (every
OSM outline and node labelled by the last four digits of its id), `satellite_temple_frame.png` (Esri z19 imagery
rotated into the same frame with the OSM outlines; reference only) and `osm_site_extract.json` (the raw extract: 144
tagged nodes, 1,412 ways, 37 relations around the site).

```
                 b (compound north, 36.7°)
                 ^            PT-10 shrines (6 small, 3.5 m)
     PT-12 north wing (31 x 12, red roof)   [] [] []            PT-11 east wing (white, 3 storeys)
 ------------------------------------------ [] PT-06 Vasuki?    |                    |  river
  |PT-04|                                       []              |                    |  Bagmati
  |west |  PT-15 trishul                                        |  PT-11 east gate ==|== stairs down to
  |gate |==== PT-02 NANDI ===>| PT-01 MAIN TEMPLE |  (east door) |                    |   the ghat walk
  |     |   (on door axis)    |  19.5 m plinth     |              PT-08 Mukti Mandap   |
  forecourt                    ------------------                  (on white block,    |
                 PT-14 kings' pillars, Dharmashila                  red/white parapet) |
     PT-13 south gate range      PT-07 Bhairav (2 roofs)        PT-21 SE block          |
                                   PT-09 grey octagonal 3-roof temple (south court)    |
                                                                          -> a (compound east, 126.7°)
```

### 2.1 Levels and ground

* Courtyard floor: grey stone flagstones (granite-grey slabs about 0.6 × 0.4 m, worn, with vermilion powder and marigold
  petals in places) [P: refs/c_westgate/c_26, nandi/ref_02]. Treat it as level. The DSM is too noisy here to give the
  step heights (it reads roofs); use these [E]: courtyard +0.0; west-gate passage about +0.5 above the courtyard, with
  three steps down into it [P: c_westgate/c_26]; west forecourt about +0.0 to +0.3; riverside walkway (west bank, Arya
  Ghat level, sibling brief) about −4.5 m; the east stair climbs that 4.5 m in about 25 risers of 0.18 m [P: c_gje/c_01,
  c_gje/c_04].
* Temple plinth +0.9 m [E] (a single tier [S: sacred-destinations]); four door steps of about 0.22 m at each door.
* The courtyard is tight [O]: open band around the plinth about 13 m on the west (Nandi side), 6 m on the north,
  5 m on the east and 2.5-5 m on the south. Pilgrims queue on the covered platform under the lower roof.

### 2.2 Paths, gates and circulation

* **West approach (main, for pilgrims and the famous view).** Ring Road at Gaushala → `Pashupati Sadak` (OSM ways
  58428581, 1154468932, 112779628, service roads, about 450 m east and north-east) → the last stretch on foot through
  the Deopatan bazaar (puja shops: marigold garlands, rudraksha beads, sindoor, brass) → the west forecourt (PT-05) →
  West Gate (PT-04) → three steps down → the Nandi → the west door. A red carpet runner bordered in orange runs across
  the forecourt to the gate [P: c_westgate/c_03, c_05]. Shoes and leather goods are left outside; police at the gate
  check bags [S: thelongestwayhome; P: c_westgate/c_26].
* **East approach.** From the west-bank riverside walkway (footway 80913703, paving stones, sibling brief) up the east
  stair (steps 112776516 [O], about 3.5 m wide, dark grey stone, brass handrail on the north side, garlands) to the East
  Gate (PT-11) and the east door. A brass bell hangs from a bracket at the top of the stair [P: c_gje/c_01].
* **South and north.** The courtyard is also entered from the south (the brick South Gate range, PT-13, from the lanes
  towards the 64-linga quadrangle) and from the north (towards Kailash and the Rudragadeshwor linga) [S: a travel-guide
  text found by web search, unattributed; P: c_area/c_33] [V]. The north passage sits at about a −3..+1, b +16..+30 between the north wing
  and the shrine group [O gap][V].
* Circumambulation of the temple is clockwise (keep the temple on the right) [S: Hindu custom].
* Vehicles: none in the courtyard (`SACRED_NO_VEHICLE`); service vehicles reach the forecourt's west side only
  [P: c_westgate/c_03 shows a truck there].

### 2.3 Walls, roofs around, skyline

* The courtyard is closed by the backs and fronts of 2-3 storey buildings, not by a free-standing wall: white or cream
  lime plaster with arched and rectangular lattice windows, red brick (dachi apa) on the South Gate range, pitched roofs
  of rust-red corrugated metal (`#9C4A3A`, faded `#B86C5A`) or grey metal, white parapets. On the north side the outer
  wall top carries razor wire [P: c_inner/c_07].
* Trees: two very tall Araucaria (hoop-pine type) conifers rise north of the courtyard and are in every east-bank view
  (right of the temple), and a tall, thin columnar tree with a tufted crown stands west of the temple (behind it in the
  classic view) [P: roof/ref_05, c_gje/c_16]. No tree inside the courtyard [P].

## 3. Structures at a glance

| ID | Structure | OSM | Centre (a, b) m | Footprint | Height | Now in game |
|---|---|---|---|---|---|---|
| PT-01 | Main temple (Pashupatinath) | w913170315 + parts w1032558820, w1032559974, w1032559975, w1033128464 | 0, 0 | plinth 19.5 × 19.2 | 23.6-23.7 | hero (parametric pagoda) |
| PT-02 | Gilt Nandi on pedestal | none | −15.5, 0 | pedestal ≈ 5.0 × 2.6 | ≈ 3.9 | hero prop, wrong place and size |
| PT-03 | Gajur and trident (part of PT-01) | w1033128464 (part) | 0, 0 | base ≈ 2.2 sq | top at 23.6 | stacked bulb, no corner pinnacles, no trident |
| PT-04 | West Gate (main gate) | w913170307 (gatehouse) + flanks w913170313, w913170308 | −36.7, −2 | 6.8 deep × 10.0 wide | ≈ 12.5 to finial | "Hut", brown gable, thatch |
| PT-05 | West forecourt and flanking buildings | r3442318, w913170310, w913170311, w913170312 | −58, 0 | ≈ 30 × 40 | — | modern blocks |
| PT-06 | Vasuki (Basukinath) temple | candidate w1094633492 or w1094633491 [V] | ≈ +11, +20 | body ≈ 3.5 sq, eaves ≈ 5.5 | ≈ 8-9 | flat box / missing |
| PT-07 | Bhairav temple (Unmatta Bhairav [V]) | w913170316 "Bhairav Mandir" | +5.1, −17.4 | 9.4 × 10.0 | ≈ 11 | 5-storey flat block |
| PT-08 | Mukti Mandap | w913217412 "Mukti Mandal" | +23.3, −5.7 | 9.4 × 9.2 | ≈ 13.5 (on its block) | 4-storey flat block |
| PT-09 | Grey octagonal three-roof temple with a linga | w256799892 + parts, node n2322979162 | +3.8, −29.9 | octagon 7.1 | ≈ 12-13 | generic pagoda, tile roofs |
| PT-10 | Six small shrines (NE group) | w1094633491, -492, -493, -496, w1368994740, -741 | +1..+15, +17..+29 | 3.5 sq each | 3-6 | flat 3.5 m boxes |
| PT-11 | East Gate, east stair, east wing | gate in w913217416; steps w112776516; wing w913217416 + part w1033128669 | +22, +7 (gate) | wing 18 × 28 | wing 6 / 11 | 2 + 4 storey flat blocks, no gate, no stair |
| PT-12 | North wing | w913170309 | −20.4, +21.9 | 31.7 × 11.8 | ≈ 9 [E] | 6-storey flat block |
| PT-13 | South Gate range (Newar brick) | w913170317 [V] | −14.2, −20.5 | 24.0 × 11.3 | ≈ 9 | 6-storey flat block |
| PT-14 | Kings' pillars and Dharmashila | none | ≈ −6, −12 | — | ≈ 4.5 | missing |
| PT-15 | Great trident (trishul) | none | ≈ −26, +6 [E] | — | ≈ 4 | missing |
| PT-16 | Bells, lamp pillars, linga shrines, rails | none | various | — | — | lamp pillars and bell pavilion only, on the hero stair |
| PT-17 | Courtyard floor and queue | — | — | — | — | plain ground |
| PT-18 | Classic east-bank view composition | — | from +71, +9.5 | — | — | §5 |
| PT-19 | West and south-west priests' quarters (Mul Bhatta Niwas etc.) | w913170301, w913170302, w913170306, w913217394 | −20..−52, −26..−37 | — | 2-3 storeys | 5-6 storey flat blocks |
| PT-20 | Entrance lane from Gaushala | ways 58428581, 1154468932, 112779628 | west, 150-500 m | — | — | ordinary street |
| PT-21 | South-east riverside block | w913217411 | +24.8, −18.7 | 18.2 × 13.1 | ≈ 9 | 5-storey flat block |

## 4. Structure by structure

Heights are above the courtyard floor unless stated. Colours are cartoon-ready sRGB; "lit" and "shade" are the two ends
seen in sun.

### PT-01 Main temple

**Identity.** पशुपतिनाथ, OSM w913170315 (`building=temple`, `height=6`, `tourism=attraction`) [O]. Shiva as
Pashupati; four-faced black stone mukhalinga about 1 m high inside (never modelled or shown) [S]. Present building 1692
(Wikipedia) / 1697 (PADT), Bhupalendra Malla; struts late 17th c. [S].

**Plan and orientation [O].** Plinth outline 19.5 × 19.2 m, edges at 36.7° and 126.4°; the four doors face 306.7°
(main, "west"), 36.7° ("north"), 126.7° ("east", to the river) and 216.7° ("south"). OSM parts:

| Part | OSM | Plan | Min-max height |
|---|---|---|---|
| Lower roof | w1032558820 | 18.5 × 18.8 eave | 9.0 → 11.5 |
| Upper body (sanctum core) | w1032559975 | 8.7 × 8.3 | 0 → 14.5 |
| Upper roof | w1032559974 | 13.3 × 13.5 eave | 14.5 → 19.0 |
| Finial base | w1033128464 | 1.8 × 1.8 | 13 → 16 (mis-tagged; finial top is ≈ 23.6) |

19.0 m roof top + a 4.6 m gajur = **23.6 m** total, exactly the published 23.6-23.7 m [S], so **use the OSM heights
as they are** (the older advice in temples.md §4.3 to scale × 1.1 above 9 m is not needed).

**Section, bottom to top [O/P/E]:**

1. **Plinth** 0 → 0.9 m [E]: one tier, grey stone `#8E8A80` with a moulded top edge `#A39E92`, to the full
   19.5 × 19.2 outline. The 3.2 m wide band of plinth outside the walls is a covered walk under the lower roof (pilgrims
   queue there). Four short flights of 4 steps at the four doors, each flanked by a small stone or gilt guardian on a
   block [P: c_gje/c_04 shows stone lions at the east gate; at the temple doors guardians are gilt images in niches,
   below]. No big stair, no lion-and-lamp procession on the plinth (the current hero's stair is invented).
2. **Ground storey walls** 0.9 → ≈ 7.0 m, a square of ≈ 13.0 m [E: lower-roof eave 18.5 m minus a 2.75 m overhang a
   side] around the inner sanctum (8.7 m), leaving the narrow inner ambulatory ("outer garbhagriha") inside [S]. Each
   face: in the centre a door opening ≈ 1.8 m wide × 3.2 m high [P: c_gje/c_03], double leaves sheathed in **silver
   repoussé** (`#C9CCD1`, tarnished `#8E9296`) with gilt borders, under a **gilt semicircular torana** (≈ 2.6 m wide,
   `#D9A62E`) with a central deity and a kirtimukha at the apex [S: PADT "silver and golden gilded doors ... metal
   carvings in the tympanum"; P: nandi/ref_03, c_westgate/c_26]. On both sides of each door, gilt-framed **niches with
   gold-painted guardian deities** in two sizes [S: sacred-destinations]. The rest of the wall is clad in large
   rectangular panels framed in gilt bands (dark repoussé and painted relief inside), with a **carved wooden frieze** of a
   row of small figures/heads at ≈ 4.5-5.5 m [P: c_inner/c_01, nandi/ref_03, c_gje/c_03]. Wall base and quoins grey stone.
3. **Lower struts and lattice skirt** ≈ 6.8 → 9.0 m: carved dark-wood struts (tundal) leaning out at ≈ 50° from a
   projecting wooden cornice to the eave purlin, **≈ 9 per side plus a corner strut** [P: night/ref_05 counts 9-11 on the
   east face], each a standing deity in the upper two-thirds with traces of red, green and gold paint (Shiva's family and
   Ramayana scenes [S: sacred-destinations]; carve non-explicit, generic deities). **Between the struts, slanting panels
   of dark wooden lattice** (square/diamond grid, near-black `#1E1A18`) close the whole underside, so from outside the
   roof sits on a black lattice band: this is the strongest single cue of Pashupati's look [P: c_area/c_28,
   c_gje/c_03, pagoda/ref_02].
4. **Lower roof** eave 9.0 m (18.5 × 18.8), rising to 11.5 m where it meets the upper body: pitch ≈ 27°, straight
   slopes, eave line straight with the corners lifted slightly and ending in **curled gilt corner tips** (spiral
   makara-like hooks, ≈ 0.5 m) [P: nandi/ref_01, c_area/c_28]. Gilt copper sheet with **standing-seam ribs ≈ 0.55 m
   apart** running down the slope (about 16-17 ribs per face half) and a gilt eave board [P: c_gje/c_00, c_area/c_27].
5. **Upper body** 11.5 → 14.5 m, 8.7 × 8.3 m: dark carved wood, a narrow band of lattice windows and carved panels
   above the lower roof, gilt metal edging; upper struts ≈ 6 per side + corners from ≈ 12.3 m to the upper eave, with
   the same slanting black lattice between them [P: pagoda/ref_01, pashupati_nath/ref_01, c_area/c_28].
6. **Upper roof** eave 14.5 m (13.3 × 13.5) to a flat square top at 19.0 m (≈ 2.4 m square): pitch ≈ 34°, gilt
   standing seams ≈ 0.55 m apart, curled gilt corner tips.
7. **Gajur (PT-03)** 19.0 → 23.6 m.

**Colours.** Gilt copper roofs: lit `#E2BE57`, mid `#C9A13B`, shade `#8C6A22`; aged on overcast days `#A88A45` (the
roofs read darker and more bronze than the finial: do not make them lemon yellow) [P]. Lattice and soffits `#1E1A18`;
struts `#3A2518` with paint `#A8322A`, `#3E6B45`, gilt `#D9A62E`; silver `#C9CCD1`; plinth `#8E8A80`.

**What each side shows.**

* West (main, 306.7°): the Nandi's rump on its red pedestal in front, the silver west door with its gilt torana, gilt
  niches and panels; a brass queue rail runs along the south side of the Nandi to the door [P: c_westgate/c_26].
* East (126.7°, the river and the classic view): the east door (dark silver) on axis with the East Gate; a stone lamp
  pillar (dipastambha) on the path between them; queues; from the east bank only the two gilt roofs, the black lattice
  bands and the gajur show above the Mukti Mandap and the East Gate [P: c_gje/c_03, roof/ref_05].
* North and south: the same door scheme (silver, torana, niches); to the south the kings' pillars (PT-14) and the Bhairav
  temple (PT-07) are 2.5-6 m away, to the north the shrine group (PT-10) and Vasuki (PT-06).
* From above: two nested gilt pyramids with ribs, square to the courtyard, the gajur at the centre [P: satellite].

**Must have.**

- [ ] Plan square to the OSM outline (36.7° / 126.7°), main door 306.7°, four doors.
- [ ] Single 0.9 m stone plinth to the full 19.5 m outline; 4-step flights at each door; no big stair, no stair lions.
- [ ] Ground storey walls ≈ 13 m square, ≈ 6 m tall, with a covered 3.2 m walk outside them under the lower roof.
- [ ] Each door: silver double leaves, gilt semicircular torana, gilt niches with guardian images either side.
- [ ] Carved wooden frieze band; gilt-framed wall panels.
- [ ] Struts (≈ 9 per side + corners lower, ≈ 6 + corners upper) and **slanting black lattice panels between them under
      both roofs**.
- [ ] Two gilt roofs with ≈ 0.55 m standing-seam ribs, straight eaves, curled gilt corner tips; heights 9.0/11.5 and
      14.5/19.0 m; eaves 18.5 and 13.3 m.
- [ ] No pataka strip down the roofs (photos show none on the east and west faces; the gajur carries its own scalloped
      streamer, PT-03) [P][V]; no triangular bell fringe (no eave bells are visible in the photos) [P][V].
- [ ] No outer colonnade of free-standing posts (the current hero's post ring is invented).
- [ ] Sanctum closed; doors not walk-through (W2-O1).

### PT-02 Gilt Nandi and pedestal

**Identity.** Shiva's bull, gilded brass/bronze (sources say "bronze", "brass", "gilded") [S], "the largest Nandi in
Nepal" [S: Commons caption, Prakat Shrestha 2017]; a VS 1941 (1884) order of Prime Minister Ranodip Singh set up a trust
for the golden bull before Pashupatinath [S: Nepalica]; often quoted as about 300 years old [S: mountainsoftravelphotos]
[V]. No OSM object.

**Place [P/E].** On the west-door axis (b = 0), centre at a ≈ −15.5 (game 532951.5, 165866.8), recumbent, **facing the
west door** (its head toward +a, 126.7°), so from the West Gate (a −37) one sees its rump straight through the gate
passage with the door beyond [P: c_westgate/c_09, c_26, nandi/ref_02, ref_03].

**Form [P].** Recumbent bull ≈ 3.6 m long, body ≈ 1.9 m wide, ≈ 2.3 m from the pedestal top to the hump/horn tips
[E from people for scale]. A smooth, rounded gilt body; big rounded rump with the tail laid along the right haunch and
curling forward; folded legs tucked under; a high round hump; a long head with a broad flat brow; **long slender horns
curving up and slightly in, crescent-like, ≈ 0.8 m**, with scaled caps at the base; ears straight out sideways with a
round boss; large eyes, white-grey ball and a dark ring iris (`#D8D8D0`, `#2B2B2B`); a **collar strap down the neck
carrying a row of 8-10 round bells** (ghanti, ≈ 0.15 m) and a beaded band; garlands and red cloth often draped on it
[P: c_inner/c_00, nandi/ref_00, ref_03].

**Pedestal [P].** ≈ 5.0 × 2.6 m, ≈ 1.6 m high, stone with heavy mouldings, smeared **vermilion-orange** (`#C0502C`,
worn `#9A4A36`), on a two-step stone base; small brass oil lamps on the steps.

**Colours.** Gilt lit `#D7B048`, mid `#B8922F`, shade `#7A5E28` (a warmer, smoother gold than the roofs; pigeons sit on it).

**Must have.** On axis, facing the door, 5-6 m from the plinth edge; 3.6 m long on a 1.6 m vermilion pedestal; crescent
horns, bell collar, curled tail; the through-the-gate rump view must work from the forecourt.

### PT-03 Gajur (finial) and trident

From the roof top platform (19.0 m) upward [P: c_area/c_26, c_27, c_28, c_gje/c_15, pagoda/ref_01]:

1. A stepped square gilt base ≈ 2.2 m wide, two or three tiers with small upturned corner horns.
2. At each base corner a small **gilt bell-shaped pinnacle** on a lotus collar, ≈ 0.9 m tall (bell + two knobs + spike).
3. Main body: a big **bell (ghanta) shape** ≈ 1.6 m wide × 1.3 m tall; a ring of rounded **lotus petals**; a round
   **pot (kalasha)** ≈ 0.9 m; two stacked smaller discs/bells; a small pot; a slender stem; a **tiered conical parasol
   (chhatra)** ≈ 0.6 m wide with fringed rings; a tiny knob tip at 23.6 m.
4. A narrow gilt **scalloped streamer** (pataka) hangs from the parasol down over the bell to the base on one face.
5. Beside it a **gilt trident (trishul) on a pole** ≈ 3.2 m tall with a small damaru drum tied below the prongs, braced to
   the bell by a thin rod [P]; the trishul sits on one corner of the base (which corner [V]; photos from the east and
   south-east show it to the right of the finial).

Colours: bright gilt `#EFD66C` lit, `#A3832B` shade (brighter than the weathered roofs).

**Must have.** Bell + lotus + pot + parasol profile (not stacked bulbs), 4 corner pinnacles, the streamer, the separate
trident on its pole; total height 4.6 m above the roof top.

### PT-04 West Gate (main gate)

**OSM.** Gatehouse w913170307 (`building=gatehouse`), 6.8 m deep (along a) × 10.0 m wide (along b), a −40..−33,
b −8.9..+1.1 [O]; entrance node n442150828 (a −38.4, b +3.6). The passage runs on the door axis (b ≈ 0 ± 2; align it to
b = 0 so the Nandi view is centred, as in the photos) [O/P].

**Form [P: c_westgate/c_05 (Gerd Eichmann 2014), c_27 (Bijay Chaurasia 2016), c_03, entrance/ref_02].** A two-storey
gate pavilion, ≈ 10 m wide, ≈ 12.5 m to the finial tips, facing the forecourt (306.7°):

* **Ground storey** (0 → 5.5 m): cream lime plaster (`#EDE4C6`); **four round dark stone columns** (`#4A4A48`), ≈ 0.55 m
  diameter, on square bases, with acanthus (Corinthian-like) capitals, two either side of the central opening;
  the opening ≈ 3.0 m wide × 4.2 m high with a dark wooden frame and doors (`#4A2A1C`) under a **gilt semicircular
  repoussé torana** (Shiva family, kirtimukha at the top) ≈ 3.4 m wide; on the wall bays left and right, painted
  cusped-arch medallions in blue frames (Ganesh on the left, Surya-like deity on the right, ≈ 1.2 × 1.5 m), small blue
  square panels with a moon face (left) and a sun face (right) above them; a low black-arched blind niche below each;
  **two seated painted guardian lions** (orange-gold `#C8782E`) on low plinths at the column feet.
* **Black lintel band** with a green signboard "श्री पशुपतिनाथ मन्दिर" in yellow letters with swastikas each side
  (render as an ornamental band without readable letters if text is a problem; it is a sign, not a brand).
* **Upper storey** (5.5 → 9.0 m): an open loggia with cream round columns and wide arches either side, a scrollwork
  balustrade (dark `#3A2A20` on cream); in the centre a **large painted relief panel ≈ 2.4 × 2.0 m: blue Shiva standing
  with trident and damaru before snowy Kailash, a linga at his feet** (blue `#72A6CA`, white peaks, green water)
  flanked by white panels with vases of lotus vines; above it a pointed gable panel on blue with a big orange **Om**.
* **Roof** (9.0 → 11.5 m): a cream-painted metal hip roof (`#E9E0C2`) with **red edge trim** (`#B3262A`), a gable
  over the Om, and on top a small square lantern with white lattice panels and a red-edged mini roof carrying **three
  gilt finials** (centre ≈ 1.0 m, sides ≈ 0.7 m) [P].
* **Courtyard face**: the same pavilion seen from inside, with a blue painted panel of Shiva and Parvati [P: c_gje/c_00].
* Passage inside: white plastered walls with a dark red-brown dado, stone floor, three steps down into the courtyard;
  police post on one side [P: c_westgate/c_26].

**Flanks [O/P].** North: a white three-storey Rana-period building with lattice windows and a rust-red corrugated gable
roof (r3442318: `building:levels=3`, `height=9`, white, brown gabled roof [O]; it projects 10 m in front of the gate).
South: a white two-storey building with a gilt-framed door between two **painted eyes**, a red LED sign "Welcome to
Pashupatinath temple" above, and a rust-red roof with a dormer [P: c_westgate/c_03, c_gje/c_14] (OSM w913170311 /
w913170310 [V]). "A golden door sits just to the right of the western entrance gate" [S: mountainsoftravelphotos].

**Must have.** Cream two-storey pavilion with 4 dark columns, gilt torana, blue Shiva panel, Om gable, cream roof with
red trim and a lattice lantern with three gilt finials; lions at the foot; passage on the door axis; flanking white
buildings with rust-red roofs; the golden door with painted eyes to its south.

### PT-05 West forecourt

A paved square ≈ 30 m (along a) × 40 m (along b) in front of the gate (a −42..−72) [O/P]: light grey stone flags
(`#CFC7B8`) with the red runner (`#B4483A`, orange border) to the gate; tall street lamps (solar-panel heads); pigeons
fed on the square [P: c_westgate/c_21]; on its west and north sides 2-3 storey white and red-brick buildings with shops
(puja goods), one Newar brick building with carved windows [P: c_westgate/c_21]. Nights: lamps sodium-orange, gate lit
yellow [P: c_gje/c_14]. Current game: buildings around it are 4-6 storey candy-coloured blocks (wrong); the sample has
no square.

**Must have.** Open flagstone square, the runner, lamps, pigeons, low white and brick frontages (2-3 storeys), no blocks
taller than ≈ 10 m.

### PT-06 Vasuki (Basukinath) temple

**Identity.** Vasuki, king of the nagas; image a naga from the waist up with snake coils below [S]; "small pagoda temple
in the north-east corner of the courtyard" [S: sacred-destinations], west-facing, built by Pratap Malla in VS 1697
(1640) [S: Commons caption, Rajesh Dhungana]; pilgrims worship here before entering the main temple [S].

**Place.** North-east quarter, about a +7..+15, b +18..+22 [E from the east-bank photos and the OSM shrine group]; OSM
candidates w1094633492 (a +15.1, b +21.3) or w1094633491 (a +6.7, b +17.7), 3.5 m squares [O][V].

**Form [P: c_inner/c_07, c_09, c_inner/c_21].** A small two-tier pagoda: body ≈ 3.5 m square on a low plinth, door to
the west (306.7°); lower roof eave ≈ 5.5 m at ≈ 4.5 m, upper roof eave ≈ 3.8 m at ≈ 6.8 m, top ≈ 8.5 m with a **gilt
bell-shaped kalasha finial**. Both roofs **verdigris copper** (`#7FA595`, lit `#94AC95`, shade `#4E6A5E`) with standing
seams and curled corner tips; carved dark struts and lattice between them; carved wooden upper body.

**Must have.** Green (verdigris) two-tier roofs, gilt bell finial, west door; visible right of the main temple from the
east bank.

### PT-07 Bhairav temple (south)

**OSM.** w913170316 "Bhairav Mandir" (`building=yes`), 9.4 × 10.0, a +0.4..+9.8, b −22.4..−12.4 [O]. Sources place the
Unmatta Bhairav temple south of the main temple [S: bestheritagetour, search summary of travel guides] [V identity].

**Form [P: complex/ref_09, c_inner/c_21, satellite].** A dark grey metal-roofed **two-tier pagoda** (dark slate grey
`#5E6466`, lit `#7D8486`) with a gilt finial, dark wood struts and upper body, brick/plaster ground storey on a low plinth;
top ≈ 11 m [E]. Door toward the main temple (north, 36.7°) [E][V].

**Must have.** A real two-roof temple (not a block), dark grey roofs, gilt finial; south of the temple 2.5 m off the
plinth edge.

### PT-08 Mukti Mandap

**OSM.** w913217412 "Mukti Mandal", 9.4 × 9.2, a +18.6..+28.0, b −10.3..−1.1 [O].

**Form [P: c_gje/c_03, c_04, c_08, pashupati_nath/ref_01, night/ref_05].** A square pavilion standing on the flat top
of a **white plastered riverside block** whose courtyard-and-river edge carries a **red-and-white crenellated parapet**
(cream panels `#E6DCC4` between red piers `#9E2F25` with red caps, ≈ 1.1 m high) where pilgrims lean to watch. The
pavilion: a raised floor; **dark carved wooden arcade** (`#2A2220`) with **cusped multifoil arches**, 5 bays a side on
carved posts, a wooden frieze; a **single wide hip roof of grey metal** (`#A4ADB4`, standing seams, lit `#BFC6CC`) with
deep eaves; on its apex a small **white plaster shikhara finial with a gilt spike**. Roof top ≈ 13.5 m above the
courtyard [E] (its roof sits about level with the main temple's lower roof in the east view). Inside: people sitting on
mats (a pavilion for rituals and resting).

**Must have.** Grey metal hip roof, white mini-shikhara finial, dark cusped arcade, red-and-white crenellated parapet on
a white block; seen front-left of the main temple from the east bank.

### PT-09 Grey octagonal three-roof temple (south court)

**OSM.** w256799892 (octagon-like, 11 vertices, 7.1 m, `height=0.1`) with parts w256799895 (4-5 m, gabled, dimgray,
6.1 m), w256799894 (7-8 m, dimgray, 5.0 m), w256799893 (10-11 m, pyramidal, dimgray, 4.0 m), w256799896 (to 10 m,
3.0 m) and w256931055 (gold onion finial from 11 m); node n2322979162 "शिवलिंग / Shivalinga" [O]. a +3.8, b −29.9.

**Form [O/P: c_gje/c_17, complex/ref_09].** An octagonal (or square with chamfered corners) temple with **three grey
metal roofs** stepping up at about 4.5, 7.5 and 10.5 m, eaves 6.1 / 5.0 / 4.0 m, and a **gilt onion finial** to ≈ 12.5 m;
a Shiva linga inside. Grey `#6E7477`, finial gilt. Identity [V] (possibly the shrine listed as Kirtimukh Bhairav or the
184-linga shrine; do not name it in game until verified).

**Must have.** Three stepped grey roofs and a gilt onion finial on an octagonal plan.

### PT-10 Small shrines (north-east group)

**OSM.** w1094633491, w1094633492, w1094633493, w1094633496, w1368994740, w1368994741: 3.3-3.8 m squares (a +1..+15,
b +17..+29) [O]. In the satellite their roofs are white [P].

**Form [P: c_inner/c_00 background, c_gje/c_03, c_inner/c_01].** The courtyard's small shrines are of three kinds: (1)
**white plastered mini-shikharas** ≈ 3-5 m with a curved tower and a gilt bell finial; (2) **grey stone linga shrines**
with a ribbed (fluted) dome and a small spire, ≈ 2-3 m; (3) **red-plastered pyramidal shikharas** with a gilt crown
finial, ≈ 4 m (one stands between the Mukti Mandap and the East Gate [P: c_gje/c_03, c_04]). Also **maroon fluted-dome
shrines** with gilt bands and finials (two, west and south-west of the temple [P: pagoda/ref_02, c_gje/c_03]). Use white
mini-shikharas for the six OSM squares (one of them may be Vasuki, PT-06) [E][V]; place the red and maroon ones where the
photos show them.

**Must have.** Real small shrines with finials (white, grey stone, red, maroon), not flat boxes.

### PT-11 East Gate, east stair and east wing

**East Gate [P: c_gje/c_01, c_03, c_04, night/ref_05, pashupati_nath/ref_01].** At the top of the east stair, at about
a +22, b +7 (game 532985.8, 165850.0), facing 126.7°, set between the Mukti Mandap block (south) and the east wing
(north). A narrow pale-yellow/cream (`#E8DCA8`) plastered gate ≈ 5 m wide × 7.5 m to the finial: a dark wooden door
(`#4A2A1C`) in a frame with grey pilasters; above it a **blue semicircular painted lunette** (Shiva and Parvati on blue
`#3E7FB8`, green rim `#2E7D5B`); above that a curved cream pediment with a **roundel**: a gold Om on blue in the 2007 and 2014
photos, a white clock face with a blue rim in the 2013 and later night photos [P: c_gje/c_01, c_04, c_03, night/ref_05]
[V current]; on top a **large gilt bell
finial** (≈ 1.4 m) with two smaller gilt finials at the corners; two dark stone seated lions at the top step.

**East stair [O/P].** Steps way w112776516 from the riverside walkway (a +36, b +10) up to the gate (a +24, b +7), 12 m
long [O]; ≈ 25 risers, ≈ 3.5 m wide, dark grey stone (`#5C5A55`), a brass handrail and marigold garlands on the north
side, a hanging brass bell at the top left [P]. A yellow "Entrance" sign at the foot (Hindus only).

**East wing [O/P: c_inner/c_15, c_gje/c_03].** w913217416 (`building=yes`, white, `height=6`) with part w1033128669
(`height=11`), a +15..+33, b +5.6..+33.7 [O]: a long white lime-plastered building of 2-3 storeys stepping down to the
river: rows of tall round-arched windows with red frames and lattice, a first-floor dark wooden balcony with a red
awning roof facing the river, a ground-floor arcade at the ghat level, white parapets and roof terraces; its river face
is part of the classic view (right of the stair). Colours: white `#F1EEE6`, red trim `#B3262A`, wood `#3A2518`.

**Must have.** Cream gate with blue lunette, Om roundel, gilt bell finial and lions; the stone stair with rail and bell
down to the riverside walk; the white arched-window east wing with the wooden balcony.

### PT-12 North wing

**OSM.** w913170309 (`building=yes`), 31.7 × 11.8, a −36..−5, b +16..+28 [O]. Satellite: a long pitched roof in
red/orange corrugated metal with a hipped end [P].

**Form [E][V].** A 2-3 storey building, white or cream plaster, lattice windows, red metal pitched roof ≈ 9 m to the
ridge; its south face closes the courtyard's north side 6 m from the temple. (No public photo of its courtyard face
was found.)

### PT-13 South Gate range

**Form [P: c_area/c_33, door/ref_09, c_inner/c_06].** A two-storey Newar building of red facing brick (`#A4553A`, mortar
lines) with a projecting wooden cornice and carved struts under a long eave, carved wooden windows, and in the middle a
**tall carved dark-wood door (≈ 2.2 × 4.0 m) under a carved torana** (Garuda and nagas): its two leaves carry a moon face
and a sun face, lattice panels, Shiva's eyes and two guardians at the foot. **Red-painted stone lions** on brick
plinths flank the door steps, with low brick parapets along the stair. Faces the outside lane (south); OSM candidate
w913170317 (24.0 × 11.3, a −26..−2, b −26..−15) [V].

### PT-14 Kings' pillars and the Dharmashila

"On the south side of the temple is the Dharmashila, a stone where sacred oaths are taken, and pillars with statues of
various Shah kings" [S: sacred-destinations]. Photo [P: c_inner/c_01, Gerd Eichmann 2007]: **gilt kneeling royal
figures with hands joined in namaskar, each under a gilt multi-headed naga hood, on a gilt lotus-and-throne capital on
a grey stone pillar** (pillar ≈ 3 m, figure ≈ 1.2 m; the pillar top ≈ 4.5 m), facing the temple; at least two.
Place at about a −8..−2, b −11.5..−12.5 (between the plinth and the south range) [E][V]. Also there: the 7th-century
**Chandeshvar linga** (inscribed) south of the temple and a 9th-century **Brahma temple** north of it [S:
sacred-destinations] [V positions].

### PT-15 Great trident

A large **gilt trident (trishul) about 2 m across the prongs on a dark stone pillar**, ≈ 4 m tall, inside the
courtyard in front of the West Gate (to its north side as seen from the temple) [P: c_gje/c_00]; a "huge trident north
of the main temple" is also listed [S: bestheritagetour]. Place at a ≈ −26, b ≈ +6 [E][V].

### PT-16 Small furniture

* **Bells**: brass temple bells on brackets or frames at the gates and doors (one at the top of the east stair [P]); ring
  only on interaction.
* **Lamp pillar** (stone dipastambha, ≈ 2.5 m, a grey column with a lamp bowl and a small spire) on the path between
  the East Gate and the east door [P: c_gje/c_03]; brass oil-lamp stands (diyo) at the doors.
* **Linga shrines** in stone and bell-domed stone mini-shrines along the courtyard edges; a red wall with black iron
  railings topped by small tridents on the courtyard's west side behind the Nandi [P: c_inner/c_00].
* Brass queue rails along the south side of the Nandi to the west door and on the east stair [P].
* Pigeons on the Nandi and the roofs; marigold petals, vermilion powder and oil stains on the flags.

### PT-17 Courtyard floor and queue

Grey stone flags (§2.1) with a brass-railed queue lane from the West Gate along the Nandi to the west door and around
the covered walk under the lower roof [P]. Keep the band between the Nandi pedestal and the door open (≥ 3 m) for the
player.

### PT-19 Priests' quarters (west and south-west)

w913170301 "Mulbhatt Niwas" (residence of the chief priest; Pashupati's priests are Bhatta Brahmins from South India
[S: common knowledge, V]), w913170302, w913170306, w913217394, w913170312, w913170310 [O]. 2-3 storey white or cream
plastered buildings with simple lattice windows and rust-red or grey pitched metal roofs, the yellow-cream one seen
behind the temple in east views [P: c_gje/c_03]. Not 5-6 storeys.

### PT-20 Entrance lane from Gaushala

Gaushala Chowk on the Ring Road → `Pashupati Sadak` (service, ≈ 450 m, OSM 58428581, 1154468932, 112779628) → the
forecourt. Narrow, paved, lined with 2-4 storey brick and plaster houses with puja shops at ground level (garlands of
marigold, rudraksha beads, sindoor, brass lamps, pictures), sadhus near the gate, pilgrims in red saris on Mondays and at
Teej [S: ref_characters.md; P: ktm/ref_04 amulets]. Vehicles end at the forecourt's west edge.

### PT-21 South-east riverside block

w913217411, 18.2 × 13.1 (a +15..+33, b −26..−13) [O]: white plastered block on the river's west bank with the same
red-and-white parapet as the Mukti Mandap block; south of it the riverside buildings of Arya Ghat (sibling brief).

## 5. The views that must work (PT-18)

1. **Classic view from the east bank** (viewpoint node n560496817 at a +71, b +9.5, game 533026.6, 165822.6, eye
   ≈ 6 m above the terrace, looking 307°) [P: roof/ref_05, main/ref_03, pagoda/ref_00, complex/ref_09]: left to right —
   the red-brick Arya Ghat building with its dark wooden arcade (sibling), the dark grey two-roof Bhairav temple behind,
   the **Mukti Mandap's grey roof and cusped arcade over the red-and-white parapet** in front-left, the **two gilt roofs
   with black lattice bands and the gajur with its trident** in the centre, the **East Gate with its blue lunette and
   gilt finials at the top of the stair** just right of centre, the **white arched-window east wing** on the right, the
   **green two-roof Vasuki** temple behind it, the cream West Gate lantern with three gilt finials peeping beyond the
   temple, the tall Araucarias to the right and the columnar tree behind, Deopatan's houses beyond. Stone shikharas and
   white mini-shrines on the ghat platform in the foreground (sibling).
2. **Through the West Gate** from the forecourt (a −50, b 0, eye 1.6 m): gate pavilion framing the Nandi's gilt rump on
   its vermilion pedestal with the silver west door and black lattice band behind [P: c_westgate/c_09, c_26].
3. **From the east stair**: the East Gate, then the east door with the lamp pillar, the gilt lower roof above.
4. **From the courtyard** (a −22, b +10): Nandi, the silver west door, the struts and lattice, the gilt roofs rising
   above the north wing; the trident and the West Gate's courtyard face behind.

**What must survive at distance (LOD priorities).** At 150-300 m (the east bank, the Ring Road, aircraft on the TIA
approach) the site reads by: two nested gilt pyramids with a **black band under each** (the lattice skirts), the tall
gajur with its corner pinnacles and trident, the grey Mukti Mandap roof in front, the green Vasuki roofs, the cream West
Gate lantern, and low white and cream buildings with rust-red roofs around (mostly 6-11 m; nothing around the courtyard
reaches the temple's upper eave at 14.5 m) [P]. Struts, niches, bells and the Nandi's details drop first; the black
bands, the ribs' shading and the gajur silhouette must stay to the last LOD.

## 6. Current in-game state (base a87e5a4, sample `kathmandu_core`)

Renders: `/home/user/wt/previews/mon-pashupati-temple/current/` — `hero_views.png` (hero alone, 6 views),
`site_eastbank.png`, `site_courtyard_west.png`, `site_forecourt.png`, `site_bird.png`, and side by side with photos
`cmp_eastbank.jpg`, `cmp_nandi_westdoor.jpg`, `cmp_westgate.jpg`. Dumps: `obj/site/*.obj`, inventory
`obj/site_inventory.txt`.

| Structure | Drawn as | Problem |
|---|---|---|
| PT-01 main temple | Hero `her.ktm.pashupatinath` (HeroCatalog `Pagoda(...)`: 2 tiers, 1 plinth level, 1.5 m rise, GiltAll, 4 doors, Ambulatory, Pataka, FrontBell, LampPillars 2), 21,655 tris | wide red brick slab plinth and red brick walls; a ring of thin posts (invented); one big west stair with stone lions and lamp pillars (invented); pataka strip on both roofs (not seen); triangle bell fringe (not seen); no black lattice between the struts; doors small and plain; roofs lemon yellow without the ribs' weight; finial a stacked bulb without corner pinnacles, streamer or trident |
| PT-02 Nandi | hero prop, ≈ 2.6 m, at the stair foot | too small, touching the stair, on a grey block; should be 3.6 m on a 1.6 m vermilion pedestal 5-6 m out |
| PT-04 West Gate | BLDG w913170307, archetype **Hut**, gabled **thatch**, wood, 4.5 m | a brown hut box |
| PT-05 forecourt flanks | ModernUrban 3-6 levels, 9-19 m, flat concrete, random colours | should be 2-3 storey white/brick with rust-red roofs |
| PT-06 Vasuki | flat 3.5 m box (one of the PT-10 squares) | missing temple |
| PT-07 Bhairav | ModernUrban 5 levels, 15.5 m, flat concrete | should be a 2-roof grey pagoda |
| PT-08 Mukti Mandap | ModernUrban 4 levels, 12.5 m, flat concrete | should be the arcaded pavilion on its white block |
| PT-09 octagonal temple | generic pagoda from parts, tile roofs | roofs should be grey metal, finial gilt onion |
| PT-10 shrines | ModernUrban 1 level, 3.5 m flat boxes | should be small shikhara and linga shrines |
| PT-11 east wing | ModernUrban 2 + 4 levels, flat | should be white arched-window 2-3 storey with balcony; East Gate and stair missing |
| PT-12 north wing | ModernUrban 6 levels, 18.5 m | ≈ 9 m with a red pitched roof |
| PT-13 south range | ModernUrban 6 levels, 18.5 m | ≈ 9 m Newar brick with the carved door |
| PT-14..16 kings, trident, bells, lamps | missing | |
| PT-19 priests' quarters | ModernUrban 4-6 levels, 12.5-19 m | 2-3 storeys |

The ring of 12-19 m candy-coloured blocks (levels inferred, `LevelsInferred`) towers over a 23.6 m temple and hides it;
from the east bank they fill the frame (`cmp_eastbank.jpg`). This is a bigger error than the hero's own shape.

## 7. Rules and sensitivities

* The courtyard is walkable in game (W2-O1); the sanctum is never entered or shown; doors closed; a non-blocking card
  explains that in reality only Hindus may enter and photography is forbidden inside [S].
* No cremation, bodies or fire in this site (they belong to the river, and there only as smoke and steps per the
  existing rules). Struts generic, non-explicit.
* Signs: the gate's devanagari name board and the LED "Welcome" sign are civic signs, not brands; fine to suggest as
  unreadable ornament bands.
* The 2021 PADT plan to re-gild the roof and the Nandi and install a 103-108 kg gold jalahari inside [S: Himalayan
  Times] does not change the exterior look in this brief (gilt roofs and Nandi either way).

## 8. Open issues for other packages (not owned here)

1. **data / buildings**: in this site every non-hero building gets `LevelsInferred` 4-6 storeys and a flat concrete
   roof; real heights are 2-3 storeys with pitched metal roofs. A heritage-zone height cap and roof rule (or curated
   attrs) is needed for the Pashupati compound (and other heritage cores).
2. **data**: w913170307 `building=gatehouse` maps to archetype **Hut** with a thatch roof; gatehouses in heritage zones
   need their own archetype (or the West Gate becomes a curated hero part).
3. **data / temples**: OSM "Bhairav Mandir" w913170316 and "Mukti Mandal" w913217412 (`building=yes`) are not
   recognised as sacred (named temples/mandaps should map to TEMPLE_PAGODA / mandapa).
4. **temples / heritage**: r1574190 "Pancha Devalaya" (the 74 × 69 m courtyard multipolygon south of this site, sibling
   area) is drawn by the generic pagoda generator as one 74 m pagoda (`site_inventory.txt`: `arch TemplePagoda ...
   sacred Pagoda`); it should be a courtyard with five shrines and a three-storey sattal ring.
5. **temples**: point POIs `TempleHindu` (n2322979162 etc.) are not drawn; `SacredSelector.TrySelectPoi` is unused.
6. **heritage**: the hero's hide set covers only the temple and its parts; the bespoke site model needs the other
   courtyard footprints replaced (PT-04..PT-13, PT-19, PT-21). (Done by the bespoke site: `HeroRecipe.Hide`.)
7. **lead / integration**: the bespoke site adds shared hooks that the other monument packages may add in their own
   form: `HeroRecipe.SiteOf`, `BudgetTris`, `Hide` and `HeroForm.Bespoke` (HeroCatalog), the part and hide wiring in
   `World/Sacred/HeroSet`, `HeroBuilder.PrepareGround` and its one-line call in `World/Streaming/TileBuild` before the
   terrain is meshed. Merge them into one mechanism for every bespoke site.
8. **terrain / cutfill**: the height grid's 8 m cells cannot hold the forecourt's 0.6 m step at the West Gate, so the
   terrain eases across the gate passage (hidden under the passage floor, which is a level walkway); a local carve at
   heritage plinths and gate steps would let the drawn footway meet the floor exactly.
9. **streaming / terrain**: the finest ring draws level-10 tiles at terrain step 2 (Mid, High) or 4 (Low), 16 m and
   32 m cells, which cannot hold a 60 m level courtyard cut into rising ground. The site cuts the ground for one 16 m
   cell round the paving and drapes the paving over what still rises (see §10), so on Low the courtyard's west strip
   rises by up to 0.4 m toward the West Gate and the players, walkers and placers there stand on the coarse terrain a
   few decimetres under the paving. A finer terrain step (or a terrain hole) under bespoke sites' level floors would
   remove both.
10. **heritage / tests**: `SacredFramingTests.NoSacredPlinthPlatformStepOrWallStandsOnADrawnRoad` measures hero colliders
    against the raw sample tile's terrain, not the ground `HeroBuilder.PrepareGround` gives the streamed tile; at
    Pashupati the raw surface model is metres above the levelled courtyard, so the check is weaker there than it reads.
    (The test file is not this package's; the West Gate's passage ramps were split so they pass on either ground.)
11. **mon-pashupati-river**: the ground cut extends to a = 51 m east of the courtyard north of the East Gate and to
    23 m round the south court; it only lowers ground standing above the courtyard level, so the ghat walk and the
    river are untouched, but a riverside ground prepared by the river package must run after it (or both become one
    `PrepareGround` pass).

## 9. References and credits

Web sources:

* Wikipedia, Pashupatinath Temple: https://en.wikipedia.org/wiki/Pashupatinath_Temple
* Nepali Wikipedia, पशुपतिनाथ मन्दिर: https://ne.wikipedia.org/wiki/पशुपतिनाथ_मन्दिर
* PADT, Pashupatinath temple: https://pashupati.org.np/pasupatinath-temple/ ; monuments: https://pashupati.org.np/monuments/
* Sacred Destinations, Pashupatinath: http://www.sacred-destinations.com/nepal/kathmandu-pashupatinath
* The Longest Way Home, guide: https://www.thelongestwayhome.com/travel-guides/nepal/guide-to-pashupatinath.html
* Hop Nepal: https://hopnepal.com/blog/pashupatinath-temple-kathmandu-nepal
* Best Heritage Tour: https://www.bestheritagetour.com/blog/what-to-see-at-pashupatinath-temple
* Mountains of Travel Photos (Jerry Ellis): https://mountainsoftravelphotos.com/Nepal%20-%20Kathmandu/Pashupatinath/index.html
* Nepaltraveller, architectural features: https://nepaltraveller.com/sidetrack/unique-architectural-features-of-pashupatinath-temple
* Studio Matrx guide: https://www.studiomatrx.org/guides/pashupatinath-temple-architecture
* The Himalayan Times, gold plating plan: https://thehimalayantimes.com/kathmandu/pashupatinath-to-be-gold-plated-for-rs-1bn
* Nepalica, VS 1941 order on the golden bull: https://nepalica.hadw-bw.de/nepal/catitems/viewitem/48071/1718
* Wikimedia Commons, Category:Pashupatinath Temple, Kathmandu (1,366 files; subcategories West gate, Sculptures, Views).

Photos used (reference only; local copies under `/home/user/wt/refs/mon-pashupati-temple/`):

| Local | Shows | Author, licence, source |
|---|---|---|
| roof/ref_05 | classic east-bank view | shankar s., CC BY 2.0, https://www.flickr.com/photos/77742560@N06/49626879017 |
| roof/ref_08 | upper roofs over the Mukti Mandap | SarahDepper, CC BY 2.0, https://www.flickr.com/photos/86657422@N00/3825020373 |
| main/ref_03 | east-bank view, dusk | mariusz kluzniak, CC BY-NC-ND 2.0, https://www.flickr.com/photos/39997856@N03/8432255546 |
| pagoda/ref_00 | east-bank view | leonyaakov, CC BY-NC-SA 2.0, https://www.flickr.com/photos/106447493@N05/17265596061 |
| pagoda/ref_01, ref_02 | upper roof, struts, finial; lower-roof corner | Cheryl Marland, CC BY 2.0, https://www.flickr.com/photos/13654353@N08/3016999450 , /3016167711 |
| pashupati_nath/ref_01 | upper roof, Mukti Mandap roof, East Gate | Anuma S. Bhattarai, CC BY-SA 2.0, https://www.flickr.com/photos/8942726@N03/3892622776 |
| night/ref_05 | east face at night | shirishpoudel07, CC0, https://wordpress.org/photos/photo/24067f666c/ |
| complex/ref_09 | east-bank view | gorbulas_sandybanks, CC BY-NC-SA 2.0, https://www.flickr.com/photos/26621269@N00/8481007099 |
| nandi/ref_00 | Nandi rump and west facade | Anuma S. Bhattarai, CC BY-SA 2.0, https://www.flickr.com/photos/8942726@N03/3425943150 |
| nandi/ref_01 | Nandi side, lower roof corner, shrines (inside) | kairoinfo4u, CC BY-NC-SA 2.0, https://www.flickr.com/photos/82575904@N00/16676377934 |
| nandi/ref_02, ref_03 | through the west gate | calamur, CC BY-NC-ND 2.0, https://www.flickr.com/photos/63189496@N00/3029373972 ; xitus, CC BY-NC-ND 2.0, https://www.flickr.com/photos/56726611@N00/305849994 |
| entrance/ref_02 | west gate ground storey | Cheryl Marland, CC BY 2.0, https://www.flickr.com/photos/13654353@N08/3016174689 |
| door/ref_09 | south gate carved door | shankar s., CC BY 2.0, https://www.flickr.com/photos/77742560@N06/49626157268 |
| c_westgate/c_05, c_21, c_26 and c_gje/c_00, c_01, c_03, c_04 | west gate, forecourt, Nandi through the gate; courtyard face of the west gate and trident; east stair and gate; east face and Mukti Mandap | Gerd Eichmann, CC BY-SA 4.0, Commons files Kathmandu-Pashupatinath-Tempel-24-Westzugang-gje.jpg, Pashupatinath-Tempel-20-Westzugang-Tauben-2014-gje.jpg, Pashupatinath-Tempel-27-Westzugang-Nandu-2014-gje.jpg, Pashupatinath-Tempel-08-Pavillon_im_Hauptheiligtum-2013-gje.jpg, Pashupatinath-Tempel-06-Treppe-2007-gje.jpg, Kathmandu-Pashupatinath-Tempel-03-2013-gje.jpg, Kathmandu-Pashupatinath-Tempel-04-2014-gje.jpg |
| c_inner/c_01 | kings' pillars, naga hoods, temple base frieze | Gerd Eichmann, CC BY-SA, Commons Pashupatinath-Tempel-07-innen-2007-gje.jpg |
| c_westgate/c_27 | west gate upper storey and roof | Bijay Chaurasia, CC BY-SA 4.0, Commons Pasupatinath_Temple,_Front_gate_Gajur-IMG_3500.jpg |
| c_inner/c_15 | east panorama (2019) | Bijay Chaurasia, CC BY-SA 4.0, Commons Pashupatinath_Temple-1874.jpg |
| c_westgate/c_03 | west forecourt and gate | Commons BRP_Pashupatinath_Main_entrance.jpg (licence on file page) |
| c_inner/c_00 | Nandi head | Prakat Shrestha, CC BY-SA 4.0, Commons Nandi_(bull)_Inside_Pashupati_Temple._Largest_Nandi_in_Nepal.jpg |
| c_inner/c_06 | carved door (south range) | Commons Doors_in_Pashupatinath_Temple.jpg (licence on file page) |
| c_inner/c_07 | Vasuki temple roofs | Rajesh Dhungana, CC BY-SA 4.0, Commons Basukisthan_Inside_Pashupati_Temple_..._(1).jpg |
| c_inner/c_21 | east view 2017 | Commons Pashupatinath_Temple_2017_139.jpg (licence on file page) |
| c_area/c_26, c_27, c_28 | gajur, trident, upper soffit lattice | Rajesh Dhungana, CC BY-SA 4.0, Commons Pashupati_Temple_Gajur_(Arch)_..._(1..3).jpg |
| c_area/c_33 | South Gate range | Rajesh Dhungana, CC BY-SA 4.0, Commons Pashupati_South_Gate_..._(2).jpg |
| c_gje/c_14, c_15, c_16, c_17 | west gate at night; gajur at dusk; view from the south; from the river | Commons Pashupathinath_West_Gate.jpg, Pashupati_Gajur.JPG, New_Face_of_Pashupatinath.jpg, NP-pashu-tempel.jpg (licences on file pages) |

## 10. The bespoke model (W2 monuments round)

The site is now modelled structure by structure in `game/Assets/Ghumante/Core/Generators/Sacred/Heroes/Pashupati/`
instead of the parametric pagoda template. Six heroes build it, all anchored in the temple's level-10 tile (10/520/161)
so the hero LOD allocator can keep the temple at LOD0 while the far side of the courtyard steps down:

| Hero id | Part | Structures |
|---|---|---|
| `her.ktm.pashupatinath` (curated) | `PashupatiPart.Temple` | PT-01 temple, PT-03 gajur and trident, PT-02 Nandi and queue rail, PT-17 floor (paving, vermilion and petal stains), PT-16 lamp pillar and bell frames |
| `….west_gate` | `WestGate` | PT-04 West Gate, its flanks and the inner west buildings, PT-05 forecourt (three steps up, red runner), PT-15 great trident |
| `….east_gate` | `EastGate` | PT-11 East Gate, the stone stair down to the ghat walk (rail, garlands, bell), the white east wing |
| `….mukti_mandap` | `MuktiMandap` | PT-08 Mukti Mandap on its riverside block with the crenellated parapet, the red pyramidal shrine, PT-21 south-east block |
| `….south_court` | `SouthCourt` | PT-07 Bhairav, PT-09 octagonal temple, PT-14 kings' pillars, Dharmashila and Chandeshvar linga, the maroon fluted-dome shrines, the stone linga shrines and the red wall with its trident railing, PT-13 South Gate range, PT-19 priests' quarters |
| `….north_court` | `NorthCourt` | PT-06 Vasuki, PT-10 shrine group (white mini-shikharas, the bell shrine), PT-12 north wing, the two Araucarias |

The parts are `HeroCatalog` recipes (`HeroForm.Bespoke`, `SiteOf` = the temple) that `World/Sacred/HeroSet` adds
whenever the curated DB holds the temple; each recipe lists the OSM ways it rebuilds (`HeroRecipe.Hide`), which the house
bands then skip. `HeroBuilder.PrepareGround` (called by `TileBuild` on every decoded tile) levels the courtyard to
1310.15 m and the forecourt 0.6 m above it, blending into the surrounding surface model. The streamer draws a level-10
tile's terrain decimated (every second height sample on the Mid and High tiers, every fourth on Low), so a 16 m or
32 m terrain cell reaching in from the higher ground north and west of the courtyard pushed through the paving (up to
0.33 m on High, a 2.2 m hill on Low): the ground is also cut down to the courtyard level for 23 m round the paved zones
(cuts never fill; east, toward the river, the ground already falls away), and the paving drapes 2 cm over the highest
of the drawn terrain surfaces where one still rises through it (the strip at the West Gate's courtyard face, where the
forecourt's step eases down across one coarse cell, and the north-east and north-west corners under the ring houses on
Low). The models read a prepared copy of the ground (`PashupatiGround.Prepared`) and never alter a tile they are given.

Modelling decisions against the photos (recorded where they differ from §3/§4):

* **Upper tier proportions.** The upper body is 6.8 × 7.0 m and the upper eave 11.8 × 12.0 m at 15.4 m (the OSM parts
  w1032559975 / w1032559974 map the sanctum core and an over-wide eave); the east-bank photos fit the roofs at 30-32°
  and the finial tip at 23.6 m.
* **West Gate passage** 3.6 m south of the door axis (b = −3.6), on the OSM gatehouse w913170307 (b −3.4) and the
  mapped footway through it (w1328801417, b −3.75); c_westgate and c_26 agree (from the forecourt the door shows right
  of the Nandi). The inner buildings either side are trimmed to it: w913170311 ends at b −8.75 and w913170313 at
  b −5.9 (their OSM outlines overlap the gatehouse), and the brass queue rail runs from the passage mouth (a −32) along
  the Nandi's south side to the west flight.
* **East Gate** on its OSM outline, its door 2.3 m wide on the mapped steps way (centre b 6.65; the drawn way is 2.5 m
  wide there, so the piers stand beside it, not on it); the stair runs 11° off the site axis toward the riverside walk
  and ends at the walk's west edge (a 34.75), so its walls, rail and garlands stay off the drawn walk. The garlands hang
  along the south wall and the brass bell hangs from it at the top of the stair (c_gje/c_01: both on the left looking up).
* **West Gate passage floor** is a level walkway at the forecourt's height (ramp colliders, not a solid box; the first
  piece lies along the footway's own end): the mapped footway w1328801417 ends inside the passage and the 8 m terrain
  grid eases the 0.6 m step from the forecourt to the courtyard under it, so a solid floor stood 0.1-0.2 m proud of the
  drawn footway. The three steps down into the courtyard end on the paving as draped there (0.3 m above the courtyard
  level at the flight's foot), so they are 0.1 m risers instead of 0.2 m.
* **Round 4-6 details** (render|photo pairs in `previews/mon-pashupati-temple/r4..r6`): the kings' naga rises as a
  slim body behind the king and hooks over his head with its spread hood and small heads, the capital is the bulging
  two-row lotus bowl (c_inner/c_01); the West Gate medallions carry a seated Ganesh (left) and a seated golden solar
  deity (right) with the moon and sun faces above, the loggias are open through the gate with tall slender columns and
  shallow arches, the lantern is about 3.5 m wide with three gilt bell finials (c_westgate/c_27); the temple's wall
  panels are blackened repoussé with silver standing deities under gilt aureoles; the gajur has bell-shaped corner
  pinnacles, a long slender stem under the parasol and a narrow streamer (c_area/c_27, c_28); the Nandi's horns are
  long crescents and its neck strap carries seven round bells a side (c_inner/c_00).
* **Vasuki** stands 0.7 m west of its OSM square so its 4.7 m lower eave clears the east wing wall behind it (c_inner/c_07
  shows the two touching).
* **Small shrines.** The five other OSM squares carry white mini-shikharas (5.4, 4.6, 5.0, 4.3 m) and the white bell
  shrine (c_inner/c_01); the red pyramidal shrine stands between the Mukti Mandap and the East Gate (c_gje/c_03, c_04);
  the maroon fluted-dome shrines south-west of the temple (pagoda/ref_02, c_gje/c_03) and the row of grey stone linga
  shrines before the red railing wall behind the Nandi (c_inner/c_00) are placed from the photos [E].
* **Kings' pillars** at a −6.5 and −3.4, b ≈ −12 [E], figures facing the temple; the Dharmashila and the Chandeshvar
  linga beside them [E][V].
* **Araucarias** at (2, 44) and (12, 48), 28.5 and 30 m [E from c_gje/c_17, c_inner/c_07]; they stand in the tile to the
  north and are the only things round the courtyard taller than the temple's upper eave.
* **Sacred rules kept**: the doors are closed (dark doorway panels, no interior); no bodies, fire or cremation; struts
  generic; the inner courtyard is modelled fully, the entry rule stays with the game's card (W2-O1).
