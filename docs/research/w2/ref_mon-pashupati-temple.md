# Reference brief: Pashupatinath temple and courtyard (`mon-pashupati-temple`)

The modeller's per-object brief for the bespoke Pashupatinath site (W2 monuments round). The research behind it, with
every source, OSM id and confidence tag, is `docs/research/heritage/pashupati-temple.md`; this file lists, per modelled
object, what a Kathmandu local recognises it by (silhouette, proportions, colours, details) and the photos each part was
checked against. Photos are reference only, never copied into the repository; local copies are under
`/home/user/wt/refs/mon-pashupati-temple/` with their credits in each folder's `credits.txt` (and the table in the
heritage brief §9). Renders, render|photo comparisons and the scripts that make them are under
`/home/user/wt/previews/mon-pashupati-temple/` (rounds `r1`..`r7`; `r7` adds `low_*` renders with the Low tier's 32 m terrain;
the judges' fixer round is `f2/r1`..`f2/r8`, its scripts in `f2/tools`: `situ_all.sh` renders every judged viewpoint in
situ, `parts_all.sh`, `temple.sh` and `nandi.sh` the part close-ups, `cmp_all.sh` the render|photo sheets).

Code: `game/Assets/Ghumante/Core/Generators/Sacred/Heroes/Pashupati/` (one file per structure group), site frame
`a` = metres toward 126.7° (the east door), `b` = toward 36.7°, origin the temple centroid, heights above the courtyard.

## PT-01 Main temple (`PashupatiTemple.cs`)

* **Recognised by:** two nested gilt (bronze-gold) pyramids with standing seams, each sitting on a **black lattice
  band** (the slanting lattice skirt between the carved struts), the tall gilt gajur with four bell pinnacles and a
  trident on a pole beside it; a dark, silver-and-gilt ground storey with a silver door under a gilt torana on each side.
* **Proportions (m):** plinth 0.9, full OSM outline 19.2 × 19.4; walls 12.9 × 13.1, 0.9 → 6.45, cornice 6.8; lower eave
  18.3 × 18.5 at 9.0 rising 30° to 12.4; upper body 6.8 × 7.0; upper eave 11.8 × 12.0 at 15.4 to a 2.4 m flat top at
  18.3; gajur 5.05 to the tip at 23.6. Struts 9 a side + corners (lower), 6 a side + corners (upper). Door 1.8 × 3.2,
  torana 2.6 wide.
* **Colours:** the roofs are pale champagne brass, not brassy yellow (photo medians `#C5AA81` in c_gje/c_03 and
  `#C0B09D` in pagoda/ref_01): roof `#BFA572`, ribs `#D2BA86`, eave board `#9C8456`; bright gilt `#E9CB62` only on
  finials, curls and toranas. The ground storey reads dark (median `#434245` in c_gje/c_03): charcoal carved wood
  `#37322E` between the panels, panel fields `#2E2A27`, pale stone only at the base course and quoins (`#3E3A36`,
  `#46413C`). Lattice `#1E1A18`, perforation lines `#4E4944` every 14 cm. Struts: dark boards `#3A2518` about 0.38 m
  wide with the deity painted deep blue `#2C4A6E` or green `#3C5240` (alternating by `Hashes.Fnv1a`), robe `#6E3026`,
  crown and jewels `#B08A3A`, the attendant or griffin at the foot. Doors: silver repoussé, raised panels `#B9BCC0` on a
  field `#8E9094` with grooves `#74767A`; torana gilt relief `#E9CB62` on `#A88C52`.
* **Details:** each face: door with silver leaves (raised lattice-grooved panels, the sun roundel on the left leaf and the
  moon on the right, oval medallions and foot bosses), three nested silver frame bands edged in gilt, the semicircular
  gilt torana with its seated deity boss and kirtimukha; niches with gilt guardians in the round (crowned head, four
  arms, mace and lotus, aureole); tall gilt-framed panels with blackened-bronze standing deities `#5C4A34` in modelled
  relief (6-10 cm proud: crowned head, face, four arms with attributes, dhoti folds) under gilt aureoles, a band of small
  seated figures above; the dark frieze of carved heads between gilt bands; quoins. Curled gilt corner tips on both roofs. No pataka down the roofs, no eave bells, no colonnade, no big
  stair (the photos show none). Four-step flights at the four doors with brass lamps on the cheeks.
* **Photos:** c_gje/c_03 (east face, Gerd Eichmann), night/ref_05 (east face, shirishpoudel07, CC0), roof/ref_05
  (classic view, shankar s., CC BY 2.0), c_area/c_28 (upper soffit, R. Dhungana, CC BY-SA 4.0), nandi/ref_03 (west
  door, xitus, CC BY-NC-ND 2.0), c_inner/c_01 (base frieze and panels, Gerd Eichmann, CC BY-SA).

## PT-03 Gajur and trident (`PashupatiTemple.Gajur`)

* **Recognised by:** a stout, convex-shouldered gilt bell, a ring of lotus petals on its shoulder disc, two heavy round
  knobs and a small third, a short neck and a tiered conical parasol; four corner bells on lotus collars with gilt rods
  from their tips up to the parasol's foot (the A-frame outline of pagoda/ref_01); a thin dark streamer from the parasol
  to the base; a gilt trident with its damaru on a pole beside it, braced to the bell.
* **Proportions:** stepped base 2.24 m wide (three tiers, upturned horns); great bell 1.32 m at the lip, 1.24 m at
  mid-height (0.55 of the base), shouldering in to 0.7 m at 1.35 m; knobs about 0.6 m across over 1.65-2.4 m; stem at
  least 0.14 m across; six-ring parasol to 4.7 m; rods 0.05 m; corner bells about 0.6 m wide and 0.9 m tall; trident
  pole 3.15 m; tip 23.6 m.
* **Colours:** bright gilt `#E9CB62`, shade `#A88C52`.
* **Photos:** c_area/c_26, c_27, c_28 (R. Dhungana, CC BY-SA 4.0), c_gje/c_15.

## PT-02 Nandi (`PashupatiNandi.cs`)

* **Recognised by:** a huge, smooth, glossy, rotund gilt recumbent bull on a vermilion pedestal, rump to the West Gate,
  head held high toward the west door; the near-spherical rump (two haunches with the groove between), the tail down the
  groove and curled round the right haunch to its tuft; a broad rounded muzzle with the lip crease and nostrils; long
  crescent horns rising from ridged caps and curving in; ears straight out with round bosses; big white-grey eyes with
  dark irises in gilt lids; a raised strap of round bells down each side of the neck.
* **Proportions:** body about 3.1 m long and 2.0 m from the slab to the hump (length:height about 1.6:1), the rump
  1.9 m wide by 1.7 m tall; muzzle 0.55 m; horns about 0.9 m along the curve tapering 0.12 → 0.02 m; 7 bells a side,
  0.17 m with a slot; pedestal 2.6 × 5.0 × 1.55 on two steps, slab top 1.75. Smooth lofted and lathed forms: body 20
  sides, head 20, round parts at least 16 at LOD0 and 8 at LOD1, smoothed normals.
* **Colours:** gilt `#B8923C` / light `#D9B860` / dark `#7A5E26`, shaded from the dark underside to the lit back;
  pedestal `#C0502C` worn `#9A4A36`; eye `#D8D8D0`, iris `#2B2B2B`.
* **Photos:** c_inner/c_00 (Prakat Shrestha, CC BY-SA 4.0), nandi/ref_00 (Anuma S. Bhattarai, CC BY-SA 2.0),
  c_westgate/c_26 (Gerd Eichmann, CC BY-SA 4.0).

## PT-04 West Gate (`PashupatiGates.West`)

* **Recognised by:** a narrow cream two-storey pavilion: four dark stone columns with leafy capitals, the dark doorway
  under a bronze torana, blue-framed painted medallions (Ganesh on the left, a seated golden solar deity on the right)
  with the moon and sun faces above, two orange painted lions; the black lintel band with the green name board; upstairs
  the blue Shiva-with-trident relief between white vine panels, open loggias with round columns and wide shallow arches,
  scrollwork balustrades; the pointed blue gable with the orange Om; the cream hip roof with red trim; the white lattice
  lantern with three gilt bell finials.
* **Proportions:** 9.6 m wide, 6.8 m deep, passage 3.0 m on the temple's door axis (b 0: from the forecourt, square on,
  the Nandi's rump sits centred in the opening with the silver west door just beyond, c_westgate/c_09, c_05, c_26; the
  great trident stands before the gate's north half, c_gje/c_00), ground storey 5.3, lintel band 1.0, upper floor 6.3,
  eave 9.3, finial tip 13.4 above the courtyard (12.8 above the forecourt). White walls abut it on both sides (the
  north wall closes the slot to the Rana house; no second way in).
* **Upstairs:** outer face, the blue Shiva `#4F86C6` with trident and gilt aureole (about 2.2 m) filling its ornate frame
  on sky `#72A6CA`; courtyard face, its own composition: the painted Shiva-Parvati-on-Nandi panel (about 2.2 × 2.6 m)
  framed in blue between blue-and-white patterned pilasters, no name board; wide flared white roof; three lantern
  finials of about 0.9-1.1 m.
* **Colours:** cream `#EDE4C6`, columns `#4A4A48`, black band `#24201E`, board `#2E7D4A`, medallion blue `#3E7FB8`,
  relief sky `#72A6CA`, Shiva `#4F86C6`, Om `#E8862A`, roof cream `#E9E0C2`, red trim `#B3262A`, Ganesh `#E06A2E`.
* **Photos:** c_westgate/c_05, c_26 (Gerd Eichmann), c_westgate/c_27 (Bijay Chaurasia, CC BY-SA 4.0), c_gje/c_00
  (courtyard face), c_westgate/c_03.

## PT-11 East Gate, stair and east wing (`PashupatiGates.East`, `PashupatiRing.EastWing`)

* **Recognised by:** a narrow pale-yellow gate at the top of a long, narrow dark stone stair in its canyon from the ghat
  walk: dark door, blue painted lunette in a green rim, the tall ogee pediment carrying the Om roundel, the great gilt
  bell finial on its apex (about a quarter of the gate's height) and two small ones at the corners, two dark stone lions
  on the top step; brass handrail with marigold garlands and a hanging brass bell; the Mukti block's wall with its
  crenellated parapet on the south side, the white east wing with red-framed round-arched windows and a dark wooden
  balcony on the north, both standing 5-6 m over the walk.
* **Proportions:** gate 4.6 m wide, body 4.4 m, pediment 1.4 m over the cornice with a 0.66 m roundel, central finial
  1.65 m (base 0.96 m across), corner finials 0.62 m, top 7.6 m; door 2.3 m wide (the drawn steps way's width) on
  b 6.65; stair 3.5 m wide, 25 risers of 0.18 m from the sill down to the riverside walk cut level at −4.5 m, 11° off
  the gate axis, ending at the walk's edge (a 34.75).
* **Colours:** pale yellow `#E8DCA8`, pilasters `#F0EEE6`, lunette `#3E7FB8` in `#2E7D5B`, stair `#5C5A55`.
* **Photos:** c_gje/c_01, c_04 (Gerd Eichmann), pashupati_nath/ref_01 (Anuma S. Bhattarai), night/ref_05.

## PT-08 Mukti Mandap and PT-21 south-east block (`PashupatiRing.MuktiMandap`, `SouthEastBlock`)

* **Recognised by:** a grey metal hip roof with deep eaves over a dark cusped arcade, on a white riverside block with a
  red-and-white crenellated parapet; a small white shikhara finial with a gilt spike.
* **Colours:** roof `#8E979E` ribs `#AAB2B8`; arcade `#2A2220`; parapet cream `#E6DCC4` and red `#9E2F25`.
* **Photos:** c_gje/c_03, c_gje/c_08, night/ref_05.

## PT-06, PT-07, PT-09, PT-10 Temples and shrines (`PashupatiShrines.cs`)

* **Vasuki (PT-06):** squat, spreading two-tier temple: a very broad shallow lower roof (about 21°, eave 5.4 m square),
  a short dark carved upper storey with its window band, the upper eave about 0.64 of the lower, verdigris roofs
  `#7FA595` (ribs `#94AC95`) with strongly upturned gilt corner tips, gilt bell finial; west door. Photos c_inner/c_07,
  c_inner/c_09 (R. Dhungana, CC BY-SA 4.0).
* **Bhairav (PT-07):** two-tier dark slate-grey metal roofs `#5E6466`, red brick ground storey, gilt finial, door to the
  main temple. Photos complex/ref_09, c_inner/c_21.
* **Octagonal temple (PT-09):** three grey metal roofs `#6E7477` stepping up at about 4.5, 7.5 and 10.5 m on an
  octagonal plan, gilt onion finial to 12.5 m. Photos c_gje/c_17, complex/ref_09.
* **Small shrines (PT-10):** white mini-shikharas with gilt bell finials, the white bell shrine with two gilt fluted
  bells, the red pyramidal shikhara with its gilt crown, maroon fluted-dome shrines, grey stone linga shrines before the
  red wall with its iron trident railing. Photos c_inner/c_01, c_inner/c_00, pagoda/ref_02.

## PT-14 Kings' pillars, Dharmashila, Chandeshvar linga (`PashupatiShrines.KingsPillars`)

* **Recognised by:** grey stone pillars with a great bulging lotus-bowl capital (two rows of petals), a gilt throne
  with a small seated lion at each corner, a gilt king (about 1.1 m) kneeling on his heels in a flared long coat, face
  and plumed crown, forearms raised to the palms joined in namaste, facing the temple; behind him the gilt naga: its
  tapering scaled body (0.1 → 0.06 m) rising straight up and arching forward high over him to a crowned serpent head
  looking down, and nearer, the spread hood curving up from his shoulders over his crown into a fan about 0.5 m across
  with seven small heads along its front edge (side-on the curved blade of the photo).
* **Proportions:** shafts 2.1 and 2.5 m, capital 1.24 m wide, throne 0.92 m, figure 1.1 m, the naga's head about 2.0 m
  over the throne slab, the hood fan 1.4 m.
* **Photo:** c_inner/c_01 (Gerd Eichmann, CC BY-SA).

## PT-15 Great trident (`PashupatiShrines.GreatTrident`)

Gilt trident about 1.7 m across on a dark stone pillar, damaru on the shaft, curved side prongs and a spear point; top
about 4.6 m. Photo c_gje/c_00.

## PT-12, PT-13, PT-19 Buildings round the courtyard (`PashupatiRing.cs`)

White, cream and red-brick houses of two or three storeys on their OSM outlines with lattice or round-arched windows and
pitched rust-red (`#9C4A3A`) or grey corrugated roofs; no windows on party walls (a neighbour right outside). The Newar
brick South Gate range with its carved door and red lions; the white Rana-period house north of the forecourt, joined
to the gate by a white wall; directly south of the gate the white two-storey golden-door house (north part of
w913170310): the black surround with the thick gilt ogee frame and two dark bronze leaves with gilt bosses on its
forecourt face between the painted eyes, the red LED band, a rust roof with a dormer; the red-brick Newar house only
beyond it (b < −16). North of the courtyard, round the Araucarias, cream `#EDE2C4` and white plastered houses of two
and three storeys under rust-red roofs (w913217414, w913217415), where the house bands drew one saturated block. None
is taller than the temple's upper eave. Photos c_westgate/c_03, c_00, c_31, c_area/c_33, door/ref_09, c_gje/c_03,
complex/ref_09.

## Skyline trees (`PashupatiShrines.Araucarias`)

* **The two Araucarias north of the courtyard** (2, 44) and (14.8, 39.5), 28.5 and 30 m: a straight trunk bare low down,
  a dense pillar of short branch tufts hugging it (about 3 m across) to seven tenths of the height, and a broad rounded
  head (about 7-8 m across) of drooping clumps under a flattish crown: the mushroom silhouette of c_inner/c_07 and
  c_gje/c_12, never flat discs.
* **The tall columnar Araucaria west of the forecourt** (−84, 4), about 36 m: a bare pole with a few small tufts and a
  dense umbrella crown of about ten clumps from 29 m (c_westgate/c_29); from the east bank it rises just right of, and
  above, the gajur (complex/ref_09, c_area/c_40). Colours: needles `#2E4A2E` / lit `#3D5C36`, bark `#5A4636`.

## PT-17 Courtyard floor and ground (`PashupatiGround`)

* **Recognised by:** one level of worn grey flagstones from the West Gate's three steps to the riverside blocks, with
  vermilion powder and marigold petals at the doors and round the Nandi; the forecourt three steps higher with its red
  runner to the gate.
* **In game:** the terrain is levelled under the paving and cut down for 23 m round it so the streamer's coarse terrain
  cells do not push through; the riverside walk strip east of the courtyard (a ≈ 33-40) is cut level at −4.5 m (the
  hand-off to `mon-pashupati-river`: walk at −4.5, ghats below), with the stair's canyon cut under the stair line; where one still rises (the strip at the West Gate's courtyard face, the north-east and
  north-west corners on Low) the paving drapes 2 cm over it. Checked in situ on High (`r7/situ_*`) and with the Low
  terrain (`r7/low_*`).
* **Photos:** c_westgate/c_26, nandi/ref_02, nandi/ref_03.

## Branding and sensitivity

No logos or wordmarks: the gate's devanagari name board and the LED sign are ornament bands without readable text. The
sanctum is never modelled or entered (doors closed); struts are generic deities; no cremation, bodies or fire here.

## Triangle budget (High, LOD0)

The whole site holds 143k triangles at LOD0 (ceiling 150k): temple part 53.0k (temple 41.3k, Nandi 9.6k, floor and
furniture 2.1k), West Gate group 23.4k, East Gate group 6.9k, Mukti Mandap group 7.8k, south court 28.9k, north court
23.2k; LOD1 about 46k, LOD2 about 8.8k. The fixer round paid for the Nandi, kings, East Gate, trees and golden door by
tessellating to need: straight house roofs draw their planar slopes as single quads and their ribs from two stations,
the skirts' perforation lines every 14 cm, small figures at 5-6 sides, and no windows on party walls.
