# Screenshot tour: reference brief (package `shots`)

The owner wants real gameplay screenshots in the README, taken at places a Kathmandu local recognises at once, from
the viewpoints people actually photograph them from. This brief records, per anchor of
`game/Assets/Ghumante/World/Debug/ScreenshotTourAnchors.cs`, what is there now, the classic viewpoint (from the
reference photos), the light, and the framing chosen. Photos are references only (Openverse, open licences; nothing
copied into the repository), kept under `/home/user/wt/refs/shots/` and the other packages' folders under
`/home/user/wt/refs/`. Framing checks (plans and renders of the sample region from each eye, made with the Core
meshers and `tools/mesh-preview`) are in `/home/user/wt/previews/shots/`.

Conventions: positions are game metres (x east, z north; `game = TM84 - (100 000, 2 900 000)`), offsets are from the
anchor's subject; heights are above the ground under the point (the tour samples the streamed ground); hours are local
solar time in October (day 280), the month the game starts in; the sun azimuth is clockwise from north.

## Rules applied to every shot

* **Front light.** The sun stands behind or beside the camera (at least 70° away from the view direction; tested), so
  facades, gilt roofs and painted eyes read as in daylight photos. October sun at 27.7° N: about 122° / 34° at 09:00,
  145° / 48° at 10:30, 180° / 56° at noon, 244° / 28° at 15:30; sunset about 17:45.
* **Where people stand.** Street-level eyes at 1.7-2.3 m; elevated eyes only where photographers really are (rooftop,
  temple plinth, the bank above the river, the hilltop). No eye inside a building (tested on the sample's footprints).
* **The explorer.** In street shots the explorer stands 5-12 m in front of the camera, a little to the side, facing the
  monument like a visitor (inside the portrait frame too; tested); in the Thamel shot the camera is the game's chase view
  behind them. Skyline and elevated shots show no explorer.
* **Both orientations.** Landscape 1920 x 1080 at the anchor's vertical field of view; portrait 1080 x 1920 keeps 62 %
  of the landscape's horizontal view (so the monument is not a sliver), at most 88° vertically, pitched up 6° by default
  (`PortraitTiltDeg`) and, where the wide frame holds two monuments, turned onto the main one (`PortraitYawDeg`).
* **Line of sight checked.** Every eye was chosen on a plan of the sample's footprints and hero plinths (a ray test
  from the eye to the monument's upper part), then rendered from the Core meshers; eyes that the data walls off (the
  lanes south of Kasthamandap, the bank row at Thapathali, the houses round Asan) were moved.
* **Search confirmation.** Each sample anchor's query finds the place within 500 m in the region's own search index;
  the coordinates stay authoritative (search hits such as "Krishna Mandir" exist in several towns).

## Anchors

### 1. `thamel_lane`: Thamel at street level (chase view)

* **Now**: Thamel Marg, the main north-south street of the tourist quarter north of Thamel Chowk: 4-6 storey concrete
  houses with shopfronts (trekking gear, pashmina, thangka, cafes), signboards stacked up the facades and over the
  street, cable bundles, prayer flags; scooters (Honda Dio, TVS Ntorq, Yamaha Ray ZR), motorbikes, taxis and walkers
  share the 6-8 m street.
* **Classic view**: down the street at eye height, signboards converging, people walking towards and away from the
  camera, late sun raking across the facades (refs/roads/thamel_street_kathmandu 00 andreweland, 01 and 06
  esmar.abdulhamid, 02 mckaysavage).
* **Framing**: explorer on Thamel Marg at (529289, 166382) facing 11° (the street's bearing between (-98,-65) and
  (-73,67) of the Thamel place, a straight 130 m run), camera 5.5 m behind and 2.3 m up, looking 16 m ahead at chest
  height; 52° vertical. 15:30: sun from the west-south-west lights the east-side facades.

### 2. `asan_tole`: Asan Tole

* **Now**: the six-way market crossroads of the old city; the Annapurna Ajima temple in the middle of the square:
  small (about 7 m plan), three gilded copper roofs about 12 m high, metal-clad facade, a Ganesh shrine and a small
  Narayan temple beside it; vendors of spices, rice, dried fish and brass ware on the paving; Newar brick houses with
  carved windows above modern shopfronts (refs/roads/asan_tole_kathmandu 00 eriktorner, 01-06 isapisa; refs/temples/
  annapurna).
* **Classic view**: from Yogbir Singh Marg, the diagonal market street from Indra Chowk, looking north-east into the
  square with the temple ahead and the street continuing beyond.
* **Framing**: the houses close the square in the sample's footprints: from Yogbir Singh Marg (-35, -8) and from the
  south-east corner the temple is hidden. The eye stands at the mouth of the street from Kel Tole on the south-east
  side (13.5, -10), 1.8 m, looking north-west at the temple at 6.5 m; 56° to hold the house fronts either side. 11:30: the
  high sun from the south-south-east lights the gilded roofs between the houses.

### 3. `basantapur_durbar`: Kathmandu Durbar Square from the Basantapur side

* **Now**: the brick plaza of Basantapur in front of the nine-storey Basantapur tower; to its west Kumari Ghar (the
  Living Goddess's house, carved windows), Trailokya Mohan Narayan, the nine-step plinth of Maju Dega (three roofs, about
  25 m), the Shiva-Parvati house temple, Gaddi Baithak (the white 1908 Rana palace), and Kasthamandap (rebuilt after
  2015, reopened 2021) at the west end; Taleju (35 m, three gilded roofs on a twelve-step plinth) rises to the north-east
  behind Hanuman Dhoka (refs/temples/basantapur, basantapur2 00 and 06, ktmdurbar, kasthamandap, taleju; refs/shots/
  basantapur 03 lab604).
* **Classic view**: from Basantapur looking north-west over Kumari Ghar to Trailokya Mohan and Maju Dega, Kasthamandap's
  roofs to the left; photographers stand on the plaza or on the plinths. Taleju is a separate picture to the north-east
  (it cannot share a frame with Kasthamandap without a fisheye: about 95° apart from the plaza).
* **Framing**: a street-level line of sight search over the sample (eyes on open paving that see the upper part of
  Maju Dega and of Kasthamandap) leaves one pocket: the paving between Kumari Ghar, Trailokya Mohan and Maju Dega. The
  eye stands there at (26, -20) from Maju Dega, 1.8 m, looking west at (-14, -10) at 9 m; 54°: Maju Dega's nine steps and
  three roofs fill the right half with the lion-guarded shrine in front, Kasthamandap's roofs close the square on the
  left. The portrait turns 16° right onto Maju Dega. Taleju is not in this frame (see above). 10:00: the sun from the
  south-east stands behind the camera's left shoulder. The eyes first tried (the lane south of Kasthamandap, the plaza
  in front of Gaddi Baithak) look straight into house walls in the data.

### 4. `patan_durbar`: Patan Durbar Square (needs the valley region)

* **Now**: the brick square of Patan in front of the royal palace; the stone Krishna Mandir (1637, shikhara with three
  storeys of pavilions and 21 pinnacles), Vishwanath (two roofs, elephants at the stair), Bhimsen, the Garuda and
  Yoganarendra Malla columns, the Taleju bell (refs/temples/krishna 00 and 05, refs/temples/patan 04 dalbera and 08
  eriktorner, refs/shots/patan).
* **Classic view**: from the south end of the square looking north: Krishna Mandir on the left, Vishwanath and the
  column right of it, Bhimsen beyond.
* **Framing**: eye 48 m south of the Krishna Mandir (11, -47), 1.8 m, looking a little east of it (4, 0) at 9 m; 54°:
  the shikhara with Yoganarendra Malla's column in front of the palace side. 10:00, sun from the south-east behind the
  camera. (From (12, -62) a temple plinth filled the left foreground.)

### 5. `bhaktapur_nyatapola`: Taumadhi Square, Bhaktapur (needs the valley region)

* **Now**: Nyatapola (1702, five roofs, about 30 m, the tallest pagoda in Nepal) on five plinths, its south stairway
  guarded by pairs of wrestlers, elephants, lions, griffins and goddesses; Bhairavnath's three-roof temple on the east
  side of the brick square; cafes with roof terraces (refs/temples/nyatapola 00-04, refs/shots/nyatapola 04 and 09
  ekkun).
* **Classic view**: from the square south of the stairway, looking up the guardians to the five roofs.
* **Framing**: eye 49 m south-south-west of the temple (-12, -48), 1.8 m, looking at 13 m; 52°; the explorer 12 m ahead
  at the foot of the square so the guardians stay clear. 10:00: the sun from the south-east lights the stair face.

### 6. `boudhanath`: Boudhanath

* **Now**: the 36 m stupa: white dome on three terraces, the gilded harmika with the painted eyes on four sides, the
  thirteen-step spire, strings of prayer flags from the pinnacle to the terraces; the kora ring of 4-6 storey houses,
  shops and rooftop cafes; the entrance gate from the main road on the south (refs/temples/boudha 01 jmhullot, 07
  sadhuism; refs/shots/boudha 00-02, 05).
* **Classic view**: from the south entrance (Baudhadwar) looking north at the stupa's south face and eyes, flags
  overhead.
* **Framing**: eye on the entrance path (-18.5, -71), 1.7 m, looking at the harmika at 15 m; 52°. 09:30: the sun from the
  south-east lights the white dome and the south face.

### 7. `swayambhu_stair`: the stairway to Swayambhunath

* **Now**: the eastern stairway, about 365 steps from the three big Buddha statues at its foot through forest to the
  great vajra at the top, with a central handrail, monkeys and prayer flags; the stupa's gilded spire appears above the
  last flight (refs/temples/swayambhu_stairs 00 Nick Kenrick, 06 Wonderlane; refs/shots/swayambhu 01 and 02 MattW,
  ironybored).
* **Classic view**: from the lower flights looking straight up the steps to the spire.
* **Framing**: eye on the steps way at the foot of the last flight (108, -17.6) from the stupa, 1.7 m, looking straight up
  the flight at the dome (10 m above the platform); 52°; the explorer five steps up, just right of the centre line, as a
  climber. 09:00: sun from the east-south-east behind the camera. (From the stair's foot (138, -40) and (200, -55) the
  stupa was a small bump on the hill beside the stair.) The forest of the hill is not in the sample's preview renders;
  in the game its trees line the stair and frame the flight.

### 8. `pashupati_east_bank`: Pashupatinath from the east bank

* **Now**: the two-roofed pagoda with gilded roofs (23.7 m) inside its compound, the ghats on the Bagmati below, the row
  of small stone Shiva shrines on the east bank terraces opposite, the forest of Kailash hill behind them (refs/temples/
  pashupati3 00 leonyaakov, 02 and 08 shankar s.: "the classic Pashupatinath viewpoint across the Bagmati").
* **Classic view**: from the east-bank terraces by the shrine row, looking west across the river to the temple roofs.
* **Framing**: eye on the east-bank terrace by the shrines (88, -5), 6 m (the upper terrace steps, over the shrine row),
  looking at the roofs at 13 m; 46°; no explorer (the shrines hide anyone standing on the lower terrace). 09:00: sun
  from the east-south-east behind the camera.

### 9. `maitighar_mandala`: Maitighar Mandala

* **Now**: the roundabout garden at the head of the Singha Durbar road: a square mandala platform inlaid in teal, red,
  blue and yellow with a marigold collar, white columns with green tops on the road side, a giant national flag on a
  tall pole, teal posts round the island, solar street lights (docs/research/w2/ref_ornaments.md 2; refs/ornaments/
  maitighar2 00; refs/shots/maitighar 01 NEPMET).
* **Classic view**: from a building above the south-east corner of the roundabout, looking down north-west on the
  mandala.
* **Framing**: the anchor is the mandala platform itself. Eye 14 m up over the south-east kerb (32, -40), looking down
  at the platform (0, 0) at 1 m; 50°; no explorer; no portrait tilt (the shot looks down). 10:30: sun from the south-east
  behind the camera.

### 10. `shahid_gate`: Shahid Gate

* **Now**: the martyrs' memorial arch on its round island by Tundikhel: a grey-beige stone pointed arch with King
  Tribhuvan's bust in the lantern, two side pavilions with the martyrs' busts, the national flag on top, a black iron
  railing, gardens (docs/research/w2/ref_ornaments.md 1; refs/ornaments/c_shahid).
* **Classic view**: from the road south of the island, looking north at the gate's face.
* **Framing**: eye 40 m south of the island centre on the approach (0, -40), 2.2 m, looking at 8 m; 50°; the explorer to
  the right on the kerb. 11:00: sun from the south-south-east behind the camera.

### 11. `thapathali_bridge`: the Bagmati bridge at Thapathali

* **Now**: twin reinforced-concrete decks of 157 and 163 m between Thapathali and Kupondole (the crossing of Nepal's first
  bridge, 1810) with a concrete openwork balustrade and lamp posts, the wide Bagmati riverbed below and the Bagmati
  Corridor road passing under the south end (docs/research/w2/bridges_flyovers.md; refs/bridges/thapathali; refs/shots/
  thapathali 01-02 bmaharjan).
* **Classic view**: the bridge seen from the bank, deck and railing against the sky, the river in front.
* **Framing**: eye on the riverside road of the north-east bank (63.8, -42) from the bridge's middle, 3.5 m (the road's
  embankment), looking at the deck (0, 0) at 1 m; 50°; the explorer 9 m ahead on the road, 3.3 m right of the view line
  (nearer the line they stand on a patch of the river polygon that overlaps the road). 09:30: sun from the south-east
  behind the camera (looking west-north-west along the river). (From (91, -42) the bank's house row hid the deck.)

### 12. `valley_rim_golden_hour`: the valley rim at golden hour

* **Now**: from Swayambhu hill the whole city spreads east and north-east to the rim: Shivapuri (2732 m) to the north,
  the Nagarkot ridge to the east; in October's clear air the Langtang and Jugal Himal stand above the rim at sunset,
  lit pink and gold while the valley already lies in shadow (refs/shots/swayambhu 00 "Kathmandu view from Swayambhunath
  steps", MattW).
* **Framing**: eye 28 m above the east edge of the hilltop (527258, 166297), looking north-east down over the city
  (the look point 1.1 km away on the ground: about 6° down, so the rim and the Himalaya sit in the upper third); 46°; no
  explorer. 17:18: sun about 6° high in the west-south-west behind the camera, the palette's golden hour.

## Render checks

Per anchor, `/home/user/wt/previews/shots/final/<anchor>/landscape.png` and `portrait.png` are the frames rendered from
the anchor's eye with the Core meshers (terrain, roads, areas, B0/B1 buildings, heroes, bridges, ornaments, the explorer)
and `/home/user/wt/previews/shots/compare_<anchor>.jpg` set them beside a reference photo. They show geometry and
framing only: no trees, life, sky colour or game lighting. Findings that are not this package's to fix are listed in
the package report (an orange block east of Maju Dega from a suspect OSM building, the river polygon over the
Thapathali riverside road).

## Credits (reference photos, Flickr and Wikimedia Commons via Openverse)

andreweland, esmar.abdulhamid, mckaysavage (Thamel); eriktorner, isapisa (Asan); lab604, Prof. Mortel (Kathmandu Durbar
Square); leonyaakov, dalbera, eriktorner, Jeanne Menjoulet (Patan); Kjunstorm, Cycling Man, jmhullot, abrami maura,
eriktorner, ekkun (Bhaktapur); jmhullot, sadhuism, terbeck, Dey, Phil @ Delfryn Design (Boudhanath); Nick Kenrick,
Wonderlane, MattW, ironybored, Jorge Lascar (Swayambhu); leonyaakov, shankar s. (Pashupatinath); NEPMET, Ratopati.com
(Maitighar); Ksodari, Anup Raj Rai, Janak Bhatta (Shahid Gate); bmaharjan, askmeaks (Thapathali bridge).
Licences per image in each folder's `credits.txt`.
