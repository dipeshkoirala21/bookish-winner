# Reference brief: Pashupatinath temple and courtyard (`mon-pashupati-temple`)

The modeller's per-object brief for the bespoke Pashupatinath site (W2 monuments round). The research behind it, with
every source, OSM id and confidence tag, is `docs/research/heritage/pashupati-temple.md`; this file lists, per modelled
object, what a Kathmandu local recognises it by (silhouette, proportions, colours, details) and the photos each part was
checked against. Photos are reference only, never copied into the repository; local copies are under
`/home/user/wt/refs/mon-pashupati-temple/` with their credits in each folder's `credits.txt` (and the table in the
heritage brief §9). Renders, render|photo comparisons and the scripts that make them are under
`/home/user/wt/previews/mon-pashupati-temple/` (rounds `r1`..`r7`; `r7` adds `low_*` renders with the Low tier's 32 m terrain).

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
* **Colours:** roof gilt `#A88C52`, ribs `#C4A462`, eave board `#8E7238`, bright gilt (finials, curls, torana) `#E9CB62`;
  lattice `#1E1A18` with bars `#4E4842`; struts `#3A2518` with paint `#6E3026` / `#3C5240` / `#B08A3A`; silver door
  `#A9ACB0`; dark repoussé panel fields `#3C3936` with silver relief `#8E8A82`; stone wall `#4E4842`, plinth `#8E8A80`.
* **Details:** each face: door with silver leaves (three rows of raised panels and bosses), gilt frame with ears,
  semicircular gilt torana; niches with gold guardians either side; tall gilt-framed panels with standing silver-relief
  deities under gilt aureoles and a row of small seated-figure panels above; the dark frieze of carved heads between gilt
  bands; quoins. Curled gilt corner tips on both roofs. No pataka down the roofs, no eave bells, no colonnade, no big
  stair (the photos show none). Four-step flights at the four doors with brass lamps on the cheeks.
* **Photos:** c_gje/c_03 (east face, Gerd Eichmann), night/ref_05 (east face, shirishpoudel07, CC0), roof/ref_05
  (classic view, shankar s., CC BY 2.0), c_area/c_28 (upper soffit, R. Dhungana, CC BY-SA 4.0), nandi/ref_03 (west
  door, xitus, CC BY-NC-ND 2.0), c_inner/c_01 (base frieze and panels, Gerd Eichmann, CC BY-SA).

## PT-03 Gajur and trident (`PashupatiTemple.Gajur`)

* **Recognised by:** a broad smooth gilt bell, a ring of lotus petals, a round pot, a small bell and pot, a slender stem
  and a tiered conical parasol; four corner bells on lotus collars; a thin scalloped streamer from the parasol to the
  base; a gilt trident with its damaru on a pole beside it, braced to the bell.
* **Proportions:** stepped base 2.24 m wide (three tiers, upturned horns); great bell 1.3 m wide; corner bells about
  0.6 m wide and 0.9 m tall; trident pole 3.15 m; tip 23.6 m.
* **Colours:** bright gilt `#E9CB62`, shade `#A88C52`.
* **Photos:** c_area/c_26, c_27, c_28 (R. Dhungana, CC BY-SA 4.0), c_gje/c_15.

## PT-02 Nandi (`PashupatiNandi.cs`)

* **Recognised by:** a huge smooth gilt recumbent bull on a vermilion pedestal, rump to the West Gate, head to the west
  door; long crescent horns from scaled caps, ears straight out with round bosses, big white-grey eyes with dark irises,
  a strap of round bells down the neck, the tail laid down the rump.
* **Proportions:** 3.6 m long, 1.9 m wide, 2.3 m from the slab to the horn tips; pedestal 2.6 × 5.0 × 1.55 on two
  steps, slab top 1.75; horns about 0.9 m along the curve; 7 bells a side, 0.17 m.
* **Colours:** gilt `#C9A23A` / light `#D7B048` / dark `#9A7A2C`; pedestal `#C0502C` worn `#9A4A36`; eye `#D8D8D0`, iris
  `#2B2B2B`.
* **Photos:** c_inner/c_00 (Prakat Shrestha, CC BY-SA 4.0), nandi/ref_00 (Anuma S. Bhattarai, CC BY-SA 2.0),
  c_westgate/c_26 (Gerd Eichmann, CC BY-SA 4.0).

## PT-04 West Gate (`PashupatiGates.West`)

* **Recognised by:** a narrow cream two-storey pavilion: four dark stone columns with leafy capitals, the dark doorway
  under a bronze torana, blue-framed painted medallions (Ganesh on the left, a seated golden solar deity on the right)
  with the moon and sun faces above, two orange painted lions; the black lintel band with the green name board; upstairs
  the blue Shiva-with-trident relief between white vine panels, open loggias with round columns and wide shallow arches,
  scrollwork balustrades; the pointed blue gable with the orange Om; the cream hip roof with red trim; the white lattice
  lantern with three gilt bell finials.
* **Proportions:** 9.6 m wide, 6.8 m deep, passage 3.0 m on the mapped footway (b −3.6), ground storey 5.3, lintel band
  1.0, upper floor 6.3, eave 9.3, finial tip 13.4 above the courtyard (12.8 above the forecourt).
* **Colours:** cream `#EDE4C6`, columns `#4A4A48`, black band `#24201E`, board `#2E7D4A`, medallion blue `#3E7FB8`,
  relief sky `#72A6CA`, Shiva `#4F86C6`, Om `#E8862A`, roof cream `#E9E0C2`, red trim `#B3262A`, Ganesh `#E06A2E`.
* **Photos:** c_westgate/c_05, c_26 (Gerd Eichmann), c_westgate/c_27 (Bijay Chaurasia, CC BY-SA 4.0), c_gje/c_00
  (courtyard face), c_westgate/c_03.

## PT-11 East Gate, stair and east wing (`PashupatiGates.East`, `PashupatiRing.EastWing`)

* **Recognised by:** a narrow pale-yellow gate at the top of a long dark stone stair from the ghat walk: dark door, blue
  painted lunette in a green rim, curved pediment with the clock roundel, a big gilt bell finial and two small ones, two
  dark stone lions; brass handrail with marigold garlands and a hanging brass bell; the white east wing with red-framed
  round-arched windows and a dark wooden balcony.
* **Proportions:** gate 4.6 m wide, body 4.4 m, finial to about 7.3; door 2.3 m wide (the drawn steps way's width) on
  b 6.65; stair 3.5 m wide, about 25 risers of 0.18 m, 11° off the gate axis, ending at the walk's edge (a 34.75).
* **Colours:** pale yellow `#E8DCA8`, pilasters `#F0EEE6`, lunette `#3E7FB8` in `#2E7D5B`, stair `#5C5A55`.
* **Photos:** c_gje/c_01, c_04 (Gerd Eichmann), pashupati_nath/ref_01 (Anuma S. Bhattarai), night/ref_05.

## PT-08 Mukti Mandap and PT-21 south-east block (`PashupatiRing.MuktiMandap`, `SouthEastBlock`)

* **Recognised by:** a grey metal hip roof with deep eaves over a dark cusped arcade, on a white riverside block with a
  red-and-white crenellated parapet; a small white shikhara finial with a gilt spike.
* **Colours:** roof `#8E979E` ribs `#AAB2B8`; arcade `#2A2220`; parapet cream `#E6DCC4` and red `#9E2F25`.
* **Photos:** c_gje/c_03, c_gje/c_08, night/ref_05.

## PT-06, PT-07, PT-09, PT-10 Temples and shrines (`PashupatiShrines.cs`)

* **Vasuki (PT-06):** small two-tier temple with verdigris roofs `#7FA595` (ribs `#94AC95`), gilt bell finial, carved
  dark upper body; west door. Photo c_inner/c_07 (R. Dhungana, CC BY-SA 4.0).
* **Bhairav (PT-07):** two-tier dark slate-grey metal roofs `#5E6466`, red brick ground storey, gilt finial, door to the
  main temple. Photos complex/ref_09, c_inner/c_21.
* **Octagonal temple (PT-09):** three grey metal roofs `#6E7477` stepping up at about 4.5, 7.5 and 10.5 m on an
  octagonal plan, gilt onion finial to 12.5 m. Photos c_gje/c_17, complex/ref_09.
* **Small shrines (PT-10):** white mini-shikharas with gilt bell finials, the white bell shrine with two gilt fluted
  bells, the red pyramidal shikhara with its gilt crown, maroon fluted-dome shrines, grey stone linga shrines before the
  red wall with its iron trident railing. Photos c_inner/c_01, c_inner/c_00, pagoda/ref_02.

## PT-14 Kings' pillars, Dharmashila, Chandeshvar linga (`PashupatiShrines.KingsPillars`)

* **Recognised by:** grey stone pillars with a great bulging lotus-bowl capital (two rows of petals), a gilt throne
  with lions at its corners, a gilt king kneeling with joined hands in a long coat and plumed crown, facing the temple,
  and behind him a gilt naga rising straight up and hooking over his head with its spread hood and small heads.
* **Proportions:** shafts 2.1 and 2.5 m, capital 1.24 m wide, throne 0.86 m, figure 1.1 m, the naga's hood top about
  1.9 m over the throne.
* **Photo:** c_inner/c_01 (Gerd Eichmann, CC BY-SA).

## PT-15 Great trident (`PashupatiShrines.GreatTrident`)

Gilt trident about 1.7 m across on a dark stone pillar, damaru on the shaft, curved side prongs and a spear point; top
about 4.6 m. Photo c_gje/c_00.

## PT-12, PT-13, PT-19 Buildings round the courtyard (`PashupatiRing.cs`)

White, cream and red-brick houses of two or three storeys on their OSM outlines with lattice or round-arched windows and
pitched rust-red (`#9C4A3A`) or grey corrugated roofs; the Newar brick South Gate range with its carved door and red
lions; the white Rana-period house north of the forecourt; the golden door between painted eyes south of the West Gate.
None is taller than the temple's upper eave. Photos c_westgate/c_03, c_area/c_33, door/ref_09, c_gje/c_03.

## PT-17 Courtyard floor and ground (`PashupatiGround`)

* **Recognised by:** one level of worn grey flagstones from the West Gate's three steps to the riverside blocks, with
  vermilion powder and marigold petals at the doors and round the Nandi; the forecourt three steps higher with its red
  runner to the gate.
* **In game:** the terrain is levelled under the paving and cut down for 23 m round it so the streamer's coarse terrain
  cells do not push through; where one still rises (the strip at the West Gate's courtyard face, the north-east and
  north-west corners on Low) the paving drapes 2 cm over it. Checked in situ on High (`r7/situ_*`) and with the Low
  terrain (`r7/low_*`).
* **Photos:** c_westgate/c_26, nandi/ref_02, nandi/ref_03.

## Branding and sensitivity

No logos or wordmarks: the gate's devanagari name board and the LED sign are ornament bands without readable text. The
sanctum is never modelled or entered (doors closed); struts are generic deities; no cremation, bodies or fire here.
