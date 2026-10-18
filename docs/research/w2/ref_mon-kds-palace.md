# Reference brief: Hanuman Dhoka palace complex (package mon-kds-palace)

The modeller's reference brief for the bespoke palace site (Kathmandu Durbar Square: Hanuman Dhoka Durbar, Taleju,
the Basantapur tower and the Lohan Chowk towers, Degu Taleju, Panch Mukhi, Gaddi Baithak, Kumari Ghar). The research
brief with the site plan, every OSM outline and the full photo credits is `docs/research/heritage/kds-palace.md`; this
file records what the model takes from the photographs and the numbers measured on them while modelling, the views
the renders were checked from, and the differences that remain. Photos are reference only (Wikimedia Commons and
Openverse, open licences; never copied into the repository): `/home/user/wt/refs/mon-kds-palace/` (credits.txt per
folder) and the Kumari Ghar set in `/home/user/wt/refs/mon-kds-temples/c_kumari/`.

Code: `game/Assets/Ghumante/Core/Generators/Sacred/Heroes/KdsPalace/` (one file per structure group, the kit in
`Kp.cs`, `KpStack.cs`, `KpFacade.cs`). Previews: `/home/user/wt/previews/mon-kds-palace/` (`r2/`, `r3/`, ... one folder
per round; `views/sbs_*.jpg` and `png/sbs_*.jpg` put each render beside its photo).

## 1. Site frame

Local metres x east, y north from 27.704500 N, 85.307500 E, h above the square at 1301.0 m (`KdsPalaceSite`). The
palace grid is turned 12° clockwise (grid a to bearing 102°, b to 12°). The streamer flattens the palace's zones to that
level before meshing (`KpGround`, called from `HeroBuilder.PrepareGround` in `TileBuild`).

## 2. Structures

Each entry: what identifies it in the photos (silhouette, proportions, colours, details) and what the model does.

### 2.1 Hanuman Dhoka gate (S1), Hanuman (S2), the white west front (S3b), Agamchhen (S5)

* Photos: commons/hanuman_gate c_06, c_09, c_12-c_15; agamchhen c_07, c_14.
* Gate: a door set flush in the white wall, 4.5 × 6 m surround; gilt repoussé jambs and lintel (#C9A43A); narrow bright
  blue pilaster panels (#2E6FA3) with painted eyes and roundels; a red and green carved frieze; the blue three-niche
  panel under a dark carved canopy; struts up to the black gallery; two white lions with red stripes and riders.
* Hanuman: a red-smeared square pillar about 1.6 m, the figure wrapped in a red-orange cloak (#D9541E) with no modelled face,
  a dark parasol with a deep red fringe on a pole, top about 4.5 m; 3.5 m north of the door, 1.3 m out of the wall.
* West front: blind cusped arches over a red dado and bench, shuttered windows, the continuous black gallery on
  diagonal struts, a low mossy tile roof (eave 11 m).
* Agamchhen: built as restored (the 2026 image shows temporary roofs): three small red-valanced roofs to 23 m.

### 2.2 Taleju (S10)

* Photos: commons/taleju c_02, c_25, c_26 (and c_12, c_13 from afar); singha_dhoka c_00, c_04, c_05, c_11.
* Plinth: twelve stages. Seven broad grassy terraces with low brick risers to 2.0 m (the first two at the OSM outlines
  w1414600808 and w420797418), the eighth the compound wall (about 2 m above the terrace, white coping, the carved
  doorway in the middle of its north side, photo c_25), four brick stages inside to 6.8 m with the iron railing.
* Twelve outer two-roof shrines on the seventh stage in front of the wall (OSM w1414610434..445, regularised round the
  ring centre) and four smaller ones at the compound's corners (w1415152802..805). Each: a two-step brick pedestal, a
  brick shaft with a niche, two mossy tile roofs, a small gilt finial.
* Temple: the OSM spans (roofs 33.2 / 24.9 / 18.4 m at eaves 12 / 19.6 / 26.5 m, finial to 35 m). Checked on c_02:
  from Makhan Tole the three roofs appear 1 : 0.59 : 0.36, which is what these spans give from that viewpoint (the
  upper roofs are further away), so the OSM spans stand. The ground body is 19.2 × 18.7 m (OSM w1414024864) with the
  three gilt windows close together in the middle of each face (about 3.1 m apart, c_25) and the gilt torana over the
  middle one.
* Roofs: copper sheet, grey-green on the upper surfaces (#6F8577 mid tone), standing seams, no red valance; seen from
  the street the lowest roof shows only its edge over the deep soffit, so the slopes are concave (shallow at the eave,
  steep at the top: sag 7.5 % of the run). The deep soffits carry dense dark rafters (the lattice ceiling of c_25).
* The red-ochre west wall (#C4472E) and the Singha Dhoka: a taller red gate block with a heavy white stepped crest and
  a small gilt kalash (no roof), the black studded door in a carved polychrome arch, painted eyes and roundels either
  side, two white lions with red stripes.
* Shop row (w1374620543): a lean-to of terracotta tiles sloping to Makhan Tole, about 3 m at the back, over open
  shopfronts with hanging goods (c_02).

### 2.3 Basantapur tower (S11) and the Lohan Chowk complex (S12-S16)

* Photos: commons/basantapur_tower c_00 (rebuilt south face), c_02, c_10 (south-west, before 2015), c_18; lohan_chowk,
  lohan_south, kirtipur_tower, lalitpur_tower; nasal_chowk c_08.
* Measured on c_10 (scale from the 13.8 m base): roof eaves at about 11.5, 16.9, 20.5 and 25 m; the upper storeys stay
  13.5-14.6 m wide (barely tapering) and every roof overhangs them by 2.5-3 m (spans about 20, 19 and 16 m). The model
  uses eaves 11 / 16.4 / 20.6 / 25 m, storeys 13 / 12.6 / 12 m, top ridge with three gilt finials at 28.6-30 m.
* Base brick darker red-brown (#9C4A36), the carved window band with figure struts at 9-13 m, the storeys above roof 1
  dark timber lattice (storey 4 and the top belvedere) and brick with a central balcony window (storey 5).
* Lohan south range on Basantapur: four storeys of brick with carved windows, a deep tile roof (ridge 14.4 m), the black
  iron railing on a low plinth and two grey stone lions on tall pedestals.
* Kirtipur tower: the grey-green ribbed copper hood (#6F8A7E, ribs #4E6258) over two tile roofs, 23 m.
* Bhaktapur tower: octagonal belvedere of eight lattice faces under an octagonal roof, 19.5 m.
* Lalitpur tower: square, two skirt roofs, the lattice belvedere and the broad pyramid roof (16 m), its grey lion.
* White south-west wing: three storeys of white plaster, windows about 3.9 m apart with dark lattice and teal shutters,
  blind arches at the foot, a deep tile roof on struts with a small white roof pavilion.

### 2.4 Nasal Chowk (S7), Panch Mukhi (S8), Mohan and Mul Chowk (S6, S9)

* Photos: nasal_chowk c_08, c_16; panchamukhi c_07, c_10, c_11.
* North range: cream arcade, green louvred shutters, white arched gallery, deep tile roof. East range: low brick range
  with a red-post veranda. The coronation platform with potted plants, the white shrine.
* Panch Mukhi: five round dark roofs on a white drum, each with a thin red-and-white fringe, roofs 11.2 → 4.8 m across.

### 2.5 Degu Taleju (S4) and the Seto Bhairav screen

* Photos: sweta_bhairav c_01, c_06-c_08 (Dec 2024).
* The brick tower base to 12.4 m with its tile skirt and sawtooth fascia, three dark copper roofs with bells and a gilt
  finial at 26 m, no red valances on them.
* The screen on the adjoining brick range: a round-topped arch 3.2 m wide and 4.6 m high, dark frame, square lattice in
  3 × 4 panels, a red fringe following the arch, two brass lamps on chains, a carved stone base with two guardians.

### 2.6 Gaddi Baithak (S17) and the Hnulunche range (S18)

* Photos: commons/gaddi c_00, c_04, c_06, c_08-c_10, c_16, c_22; hnulunche_west.
* West portico (OSM parts): the plain white podium is 4 m high with a pierced balustrade along its top and no stair on
  the west front; pedestals to 6.2 m; six giant columns (pairs at the ends, two singles between) to 14.6 m; the
  entablature to 16.5 m, the attic to 18.6 m with a small segmental crest and scroll volutes; three bays of arched
  openings (teal) on two storeys; two black candelabra on the podium.
* South front: paired engaged columns over two storeys of tall arched teal windows, projecting end bays, a balustraded
  parapet and a small central gable; the dark red-brown pitched roof behind (#7E3A34).

### 2.7 Kumari Ghar (S19)

* Photos: mon-kds-temples/c_kumari c_00, c_02, c_12-c_14; kumari ref_00-ref_09.
* North facade 21.1 m (c_00 at about 24 px/m): plinth with two broad steps; ground floor arched dark openings at
  ±5.0 and ±8.4 m either side of the carved door; the door's carved crest (dark red wood with a gilt boss); two white
  lions about 1.55 m on 0.7 m pedestals; first floor carved windows under big crests at 0, ±4.6, ±8.4 m and small ones
  between; the top row of carved windows close under the eave with the gilt window over the door; carved timber
  string courses at 4.2 and 7.8 m; eave 10.9 m with the red fringe and white top band; a low mossy hipped roof; the
  cluster of five gilt pinnacles on the north ridge with its gilt banner down to the front eave.
* East side on Basantapur: the same rows and the narrow white pier with the yellow door at its north end.
* Kumari Chowk (LOD0-1): carved windows on all four faces, the gilt window under a red valance on the south range.

## 3. Views checked (render | photo)

In-situ renders go through `TileBuild.Execute` exactly as the streamer builds the tile (tier 2;
`GeneratorsSacredKdsPalaceTests.DumpKdsPalaceSite`); part renders come from `DumpKdsPalaceParts`. Viewpoints are the
site frame's metres (x east, y north, h above the square); photos are the commons sets named.

| View | Eye → look | Photo | Rounds |
|---|---|---|---|
| Gate from the forecourt | (-30, 1, 1.7) → (-12.4, -2.4, 4.5) | hanuman_gate c_14 | r1, r2, r4 |
| Gate from the NW (in situ) | (-19, -2.5, 1.6) → (-11, 2, 6) | hanuman_gate c_06 | r2, r4 |
| Taleju from Makhan Tole (street) | (53, 84, 1.6) → (45.5, 45, 16) | taleju c_02 | r2, r3, r4 |
| Taleju from the SW above (aerial 45°) | (10, 0, 40) → (45, 44, 8) | taleju c_26 | r3, r4 (+ LOD1, LOD2) |
| Basantapur tower from the SW | (-12, -110, 1.6) → (2, -66, 14) | basantapur_tower c_10 | r2, r3, r4 (+ LOD1) |
| Nasal Chowk looking S / NNE | (4, -14) → (1, -60, 11); (-4, -54) → (12, -8, 12) | nasal_chowk c_08; panchamukhi c_10 | r2, r3, r4 |
| Gaddi Baithak portico from the W | (-112, -60, 1.6) → (-88, -54, 9) | gaddi c_09, c_00 | r2, r3, r4 |
| Kumari Ghar from the N | (-101, -62, 1.6) → (-102, -78, 6.5) | c_kumari c_00 | r2, r4 |
| Seto Bhairav screen from Jagannath | (-22, 13, 2) → (-8, 11, 4.5) | sweta_bhairav c_06 | r3, r4 |
| Lalitpur tower from the SE street | (58, -90, 1.6) → (40, -71, 12) | lalitpur_tower c_04 | r2, r4 |

What each round changed (render against photo):

* r1-r2: masses and positions from the OSM outlines; Taleju's roof spans checked against c_02; the Basantapur upper
  storeys widened (c_10); Gaddi's podium to 4 m with no front stair; Kumari's bays placed from c_00.
* r3: Taleju's concave copper roofs (sag 7.5 %), deep soffits with dense rafters, gilt windows close together, the
  Singha Dhoka crest; Basantapur eaves 11 / 16.4 / 20.6 / 25 m; the Seto Bhairav screen on its range; Panch Mukhi's five
  round roofs with the thin red-and-white fringe; strut feet clamped to the cornice.
* r4: the gate's door opens into the passage (Narasimha visible; the block's west face no longer covers it); the
  triangle pass below; the in-situ NW gate view moved out of Jagannath.

### Triangle budget (LOD0 / LOD1 / LOD2, as built)

The site is sixteen structures; Taleju, the one major temple, is about 30,000 at LOD0 (the round allows 60,000). The
whole site is held to 200,000 at LOD0 (`KdsPalaceSite.SiteBudgetLod0`), above the round's ~150,000 guide for a
single-temple site. The hero allocator shows a part at LOD0 only while it fills a third of the screen, so a handful at
once at most (standing in Basantapur: the tower, Lohan, Kumari Ghar and the white wing, about 55,000). The pass that
brought the site from 300,000 to about 200,000: a lean lattice window (44 triangles instead of 72; 16 at LOD1), standing
seams as two-faced ribs every 1.9 m, carved figure struts only under the lowest roof (plain above), struts every 1.2-1.3
m and bells every 0.85 m, tile courses every 1.7 m on the long ranges, the generic figure for the stone and Kumari and
Singha Dhoka lions (the Hanuman Dhoka pair keeps the hero figure), small finials as cones from LOD1, and the enclosed
courts (Mohan, Mul, Lohan) one level coarser than the street faces.

## 4. Known differences and open questions

* Agamchhen is built as restored (the 2026 photo shows temporary roofs).
* Hanuman's cloak is one red-orange; in photos it ranges from deep red to orange (it changes with offerings).
* Taleju's upper plinth stage heights (stages 9-12) are estimated from c_25 and c_26; the outer twelve shrines are
  regularised round the ring centre.
* The Basantapur square paving, the Kot, the Big Drums, the Bhagwati temple and the other square temples are the
  kds-temples package's. The Seto Bhairav screen is modelled here on its host wall; the kds-temples package also places
  one (T27) on the OSM wall of w1415152806, which falls inside this package's Degu Taleju tower (open issue for the
  merge).
* The modern beige building east of Basantapur comes from the house grammar (not part of this site).
* Not modelled: the interiors beyond the gate passage and the courts; the museum's modern fittings.
