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
* Gate: a door set flush in the white wall, 4.5 × 6 m surround; the door frame three stepped gilt repoussé mouldings
  (#B8902E); pale grey-blue pilasters (#7FA6C4) with painted eyes and a column of four round deity medallions each;
  over the door a red and green frieze of round medallions; the blue upper panel with seated figures (Krishna with
  gopinis on the left, the large multi-armed deity in a gilt aureole in the middle, two seated figures on the right)
  under three carved wooden toranas; struts up to the black gallery; two white lions with red stripes, riders and
  marigold garlands (no green mane).
* Hanuman (hanuman ref_02, ref_08): a draped figure, not a pillar: a rounded head (about 0.5 m) under the hood of the
  red-orange cloak (#D9541E) with a darker red sindoor-smeared face, the cloak bulging over the shoulders (about
  0.9 m) and falling to about 1 m at the base, marigold garlands; on a 1.8 m red-smeared stepped pedestal with a lotus
  cornice; the grey metal parasol with its deep red valance on a pole and two dark metal pataka banners on thin poles
  either side; 3.5 m north of the door, 1.3 m out of the wall.
* West front: blind cusped arches over a red dado and bench, windows with dark lattice or white jali in cusped stucco
  surrounds and white louvred shutters (no glass anywhere), the continuous black gallery on diagonal struts (a mostly
  closed dark band: carved black balustrade, dark lattice screens between the posts, a narrow open band under the top
  beam), a low mossy tile roof (eave 11 m).
* Forecourt: grey stone flags (0.6 × 0.4 m, #8F8B83, joints #6F6B65) from wall to wall over palace grid a -50..-11.6,
  b -7.5..+30, clipped round the sibling temples' plinths, walkable.
* Agamchhen: built as restored (the 2026 image shows temporary roofs): three small red-valanced roofs to 23 m.

### 2.2 Taleju (S10)

* Photos: commons/taleju c_02, c_25, c_26 (and c_12, c_13 from afar); singha_dhoka c_00, c_04, c_05, c_11.
* Plinth: twelve stages. Seven broad grassy terraces with low brick risers to 2.0 m (the first two at the OSM outlines
  w1414600808 and w420797418), the eighth the compound wall (about 2 m above the terrace, white coping, the carved
  doorway in the middle of its north side, photo c_25), four brick stages inside to 6.8 m with the iron railing.
* Twelve outer two-roof shrines on the seventh stage in front of the wall (OSM w1414610434..445, regularised round the
  ring centre) and four smaller ones at the compound's corners (w1415152802..805). Each: a two-step brick pedestal, a
  brick shaft with a niche, two mossy tile roofs, a small gilt finial.
* Temple (judged round r8, measured on taleju ref_00, ref_02, ref_05, ref_06 and c_25): the lowest roof is about 1.4 ×
  the body, not the OSM roof outline's 33 m (that outline is the plinth-top terrace's eaves line): 27 × 26.4 m on the
  19.2 × 18.7 m body (OSM w1414024864), eave at 14.3 m, so the gilt-window storey stands about 7.5 m tall (0.39 × its
  width, the carved frieze under the soffit included) and clears the Singha Dhoka's crest from the west forecourt.
  Roof 1 rises to 18.6 m, roof 2 from 22.4 to 25.8 m, roof 3 from 28.3 to 31.8 m, the gajur from 31.75 to 36 m.
  Roof 2 spans about 0.68 × and roof 3 about 0.43 × roof 1 (measured 0.62-0.63 and 0.37-0.40 on c_25 from Makhan Tole
  and on the SW telephoto ref_02; the judge's 0.84 for roof 2 is not what the photos show, so roof 2's half span is
  9.4 m, not 11.4 m). The three gilt windows close together in the middle of each face (about 3.1 m apart, c_25), a
  gilt repoussé torana over each of the three, dark carved windows either side.
* Body brick darker brown-red (#7A3E30). The upper storeys are dark carved timber (#3B2A20 lattice panels between posts
  about every 1.1 m, carved cornice bands) on a 0.6 m brick foot band (`KpWalls.Timber`).
* Roofs: copper sheet, grey-green on the upper surfaces (#6F8577 mid tone), standing seams, bells, no red valance; the
  slopes about 25-30° with a light sag (4 % of the run); every corner flicks up into a horn about 0.6-0.8 m. The deep
  soffits carry dense dark rafters (the lattice ceiling of c_25) that hold the outer eave beyond the struts.
* Gajur (ref_03): a stepped gilt base, the big gilt bell (ghanta), a tall slim spire with rings and a small parasol,
  four small gilt bell-and-spire pinnacles on the base's corners and four sloping metal stays to the spire; the long
  gilt pataka banner (about 0.4 × 8 m) down the west face over roof 3.
* The red-ochre west wall (#C4472E) and the Singha Dhoka (ref_06, ref_09): the red gate block about as high as the
  wall (4.9 m), its white stepped stucco crest in three receding tiers to about 6.9 m and a small white open pavilion
  (four posts, a low roof) on top to about 8 m; the black studded door inside a large gilt repoussé torana (jamb bands,
  three concentric half rings about 2.6 m across and 0.12 m proud, a kirtimukha boss at the crown); red piers
  projecting 0.4 m either side with large painted eyes (0.35 m) and gilt roundels; two white lions with red stripes on
  grey stone pedestals.
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
* Kirtipur tower (kirtipur_tower c_00-c_10, nasal_chowk c_08): the grey-green copper hood (#6F8A7E, ribs #4E6258) is a
  square-plan Bangla bell roof, not a dome: an S profile, convex over rounded shoulders to a short ridge (half length
  1.5 m), its lower third flaring out (concave) into a lip 1.3 m beyond the drum that curls up a little at mid-side and
  into horns at the four sharp corners (plan superellipse exponent 8); big against its drum (the lip about 1.4 × the
  drum across, the rise 4.1 m, about 0.6 × the drum's width); ribs on the four hips only, standing seams between. The
  drum under it: dark timber with cusped-arch lattice windows about every 1.1 m, the tall pointed heads (rise about
  0.7 × the width) in lighter carved wood; finial to 23.7 m.
* Bhaktapur tower: octagonal belvedere of eight lattice faces under an octagonal roof, 19.5 m.
* Lalitpur tower: square, two skirt roofs, the lattice belvedere and the broad pyramid roof (16 m), its grey lion. The
  east shop range on the street (c_04): shopfronts between carved posts, a row of small carved windows close under the
  5.3 m eave, the hipped tile roof at about 35° to 10.1 m so its tile surface reads from the street.
* White south-west wing (gaddi ref_01, ref_06; OSM w910121098 west block, w906935471 east block), one front along the
  real front line: the west block three storeys under one deep tile roof whose eave (9.6 m, on struts) sits right
  above the second row of teal-shuttered windows, ridge 12.6 m, no pent roof and no row above; the east block the same
  front with an attic storey of three windows set back about 1.75 m behind the main roof's slope (attic eave 13.2 m,
  ridge 15.6 m) and the small white roof pavilion on the attic. The plinth band and the eave fascia run unbroken
  across both blocks.
* Basantapur frontage of the wing and the tower (gaddi ref_01, basantapur ref_08): grey stone flags from the wall out
  past a low stone curb and the black iron railing (posts every 2 m, bars every 0.15 m), with its colliders, as along
  the Lohan south range.

### 2.4 Nasal Chowk (S7), Panch Mukhi (S8), Mohan and Mul Chowk (S6, S9)

* Photos: nasal_chowk c_08, c_16; panchamukhi c_07, c_10, c_11.
* North range: cream arcade, green louvred shutters, white arched gallery, deep tile roof. East range: low brick range
  with a red-post veranda. The coronation platform with potted plants, the white shrine.
* Panch Mukhi (panchamukhi c_10, gate2 ref_09): five round roofs of warm terracotta tile (#A0664A) on a white drum,
  roofs 11.2 → 4.8 m across, each hung with a deep red valance (0.5 m drop, #B8262B) under a 0.08 m white top band:
  from Nasal Chowk the five red bands are what identifies it.

### 2.5 Degu Taleju (S4) and the Seto Bhairav screen

* Photos: sweta_bhairav c_01, c_06-c_08 (Dec 2024).
* The brick tower base to 12.4 m with its tile skirt and sawtooth fascia, three dark copper roofs with bells and a gilt
  finial at 26 m, no red valances on them. The base is not a grid of windows (c_05-c_08): on each face one projecting
  carved bay window and two long-eared carved windows on the top two storeys, plain brick with one carved cornice band
  below; an oculus beside the screen; a deep pent roof with a red valance over the screen range at about 7 m.
* The screen on the adjoining brick range: a round-topped arch 3.2 m wide and 4.6 m high, dark frame, square lattice in
  3 × 4 panels, a red fringe following the arch, two brass lamps on chains, a carved stone base with two guardians.

### 2.6 Gaddi Baithak (S17) and the Hnulunche range (S18)

* Photos: commons/gaddi c_00, c_04, c_06, c_08-c_10, c_16, c_22; hnulunche_west.
* West portico (OSM parts): the plain white podium is 4 m high with a pierced balustrade along its top and no stair on
  the west front; pedestals to 6.2 m; six giant columns (pairs at the ends, two singles between) to 14.6 m; the
  entablature to 16.5 m, the attic to 18.6 m with a small segmental crest and scroll volutes; three bays of arched
  openings (teal) on two storeys; two black candelabra on the podium.
* South front on Basantapur (gaddi ref_00, ref_03, ref_05, c_00): a giant-order colonnade, not a flat wall: round
  columns 0.9 m across standing free on the podium (pedestals to 6.2 m, shafts to 14.6 m, Ionic capitals as on the west
  portico) at the west front's rhythm, carrying the entablature, the balustraded parapet and the small central gable;
  behind them a deep shaded loggia (the hall's wall 2.5-3 m back from the podium edge) with round-headed openings and
  white panelled doors; the end bays project. The arched openings read dark (shaded recesses behind dark-green painted
  frame bars), no glazing. The dark red-brown pitched roof behind (#7E3A34).

### 2.7 Kumari Ghar (S19)

* Photos: mon-kds-temples/c_kumari c_00, c_02, c_12-c_14; kumari ref_00-ref_09.
* North facade 21.1 m (c_00 at about 24 px/m): plinth with two broad steps; ground floor arched dark openings at
  ±5.0 and ±8.4 m either side of the carved door; the door's carved crest (dark red wood with a gilt boss); two white
  lions about 1.55 m on 0.7 m pedestals; first floor: broad three-bay carved windows under semicircular carved toranas
  (dark wood fan tympana about 1.6 m across with radial carving) at 0, ±4.6, ±8.4 m and small ones between; the top
  row a continuous band of cusped carved windows about every 1.5 m, set 0.3 m back in the shadow of the eave (deepened
  to about 1.6 m) with the gilt window over the door; broad carved string courses (0.3-0.35 m) at 4.2 and 7.8 m; eave
  10.9 m with the red valance and white top band; a low mossy hipped roof; on the north ridge five separate slim gilt
  bell-and-spire pinnacles on a common base, gaps between them, with the gilt banner down to the front eave.
* East side on Basantapur: the same rows and the narrow white pier with the yellow door at its north end.
* Kumari Chowk (LOD0-1): carved windows on all four faces, the gilt window under a red valance on the south range.

### 2.8 The Dakha front's west continuation (OSM w907080042, w1415152801 + w1029965548, w1117943799)

* Photos: dakha_north c_07 (the forecourt looking west), jagannath ref_04; 2026 imagery.
* These four outlines are palace, not houses (the house grammar had built them as flat-roofed apartment boxes in pink,
  mint and cream). The square block at the forecourt's west end (w907080042, about 17 × 17 m): white, a grey-green
  metal hipped roof (#6E7F72) with dormers on two slopes, eave about 12 m. The range beyond it (w1415152801 with
  w1029965548) and the long range behind them to the south (w1117943799): white, in the Dakha front's language (red
  dado and bench, blind cusped arches, white-shuttered windows about every 3.5 m, the black gallery on diagonal struts
  on the top floor), eave 12 m, ridge 15 m, two or three white dormer lanterns each. The four are hidden from the house
  bands (`KdsPalaceSite.HideRefs(Dakha)`) and the level zone runs west to grid a -73. The west annex of w1029965548
  (its slanted west face at grid a -70.7..-68.5) stops at a -66.2: the street the road mesher draws along it covers
  that end (0.8 m deep at b -18.5, 4 m at b -11.8), and no palace solid stands on a drawn road.

### 2.9 The kit (every structure)

* Struts (tunala; taleju ref_00, ref_02, lalitpur_tower c_04, kirtipur_tower): slender (about 0.15-0.25 m), steep
  (about 60°), close-set (0.8 m on the temples, 1.0 m on the ranges), each standing on the wall with its head tight
  under the soffit's inner purlin and reaching out only about 0.45 of the eave's depth (at most 1.25 m, a strut at
  most about 2.5 m long), winged griffin struts on the corners; under a deep roof they read as one dense dark carved
  band, and the soffit rafters carry the outer eave. (The judge suggested a reach of up to 2.2 m; on Taleju's 7 m
  eaves that gives 4.4 m struts, longer than the 2-2.5 m the same photos show, so the reach is capped at 1.25 m.)
* Newar windows (`Kp.LatticeFrame`, `Kp.FramedWindow`): deep, thickly moulded dark frames (jambs to 0.18 m) with long
  T-ears on lintel and sill (to 0.35 m), a stepped inner moulding, and a fine 45° lattice of brown bars (#5A3E2C) over
  a dark backing that reads as one brown screen; one screen panel from LOD1. Rana windows (`Kp.RanaWindow`): dark
  lattice or white jali in the opening, white louvred shutters closed on the upper floors, a cusped stucco hood on the
  gate front. No glass anywhere on the palace.

## 3. Views checked (render | photo)

In-situ renders go through `TileBuild.Execute` exactly as the streamer builds the tile (tier 2;
`GeneratorsSacredKdsPalaceTests.DumpKdsPalaceSite`); part renders come from `DumpKdsPalaceParts`. Viewpoints are the
site frame's metres (x east, y north, h above the square); photos are the commons sets named.

| View | Eye → look | Photo | Rounds |
|---|---|---|---|
| v1 Gate from the forecourt | (-32, 4, 1.6) → (-12.4, -2.4, 5) | hanuman_gate c_14 | r1, r2, r4, r5 |
| v2 Gate from the NW (in situ) | (-19, -2.5, 1.6) → (-11, 2, 6) | hanuman_gate c_06 | r2, r4, r5 |
| v3 Nasal Chowk looking S (NW corner) | (-2, -13, 1.6) → (14, -50, 9) | nasal_chowk c_08 | r2-r7 |
| v4 Nasal Chowk looking NNE | (-4, -54, 1.6) → (12, -8, 12) | panchamukhi c_10 | r2-r7 |
| v5 Basantapur square looking NE | (-25, -100, 1.6) → (5, -67, 13) | basantapur_tower c_02 | r5 |
| v6 Gaddi Baithak portico from the W | (-108, -62, 1.6) → (-88, -55, 9) | gaddi c_00, c_09 | r2-r5 |
| v7 Taleju from Makhan Tole (N) | (46, 81, 1.6) → (45, 44, 17) | taleju c_25, c_02 | r2-r7 |
| Taleju from the SW above (aerial 45°) | (10, 0, 40) → (45, 44, 8) | taleju c_26 | r3, r4 (+ LOD1, LOD2) |
| v8 Kumari Ghar from the N | (-99, -46, 1.6) → (-102, -75, 6.5) | c_kumari c_00 | r2, r4, r5 |
| v9 Lalitpur tower from the SE street | (75, -92, 1.6) → (42, -71, 10) | lalitpur_tower c_04 | r2, r4-r7 |
| v10 Seto Bhairav screen from Jagannath | (-22, 13, 2.5) → (-7, 12, 5) | sweta_bhairav c_06 | r3-r5 |
| v11 Basantapur tower S face (rebuilt) | (-5, -97.7, 1.6) → (0.2, -73, 13) | basantapur_tower c_00 | r5, r6 |
| v12 White wing and tower from the SW | (-32, -96, 1.6) → (2, -68, 11) | basantapur_tower c_02, c_10 | r5 |
| v13 Gaddi Baithak from the SE | (-50, -95, 1.6) → (-70, -62, 9) | gaddi c_00 | r5 |

The viewpoints are scripted in the preview tools (`tools/views.sh` next to the round folders); part renders against
their photos are in each round's `png/` folder.

What each round changed (render against photo):

* r1-r2: masses and positions from the OSM outlines; Taleju's roof spans checked against c_02; the Basantapur upper
  storeys widened (c_10); Gaddi's podium to 4 m with no front stair; Kumari's bays placed from c_00.
* r3: Taleju's concave copper roofs (sag 7.5 %), deep soffits with dense rafters, gilt windows close together, the
  Singha Dhoka crest; Basantapur eaves 11 / 16.4 / 20.6 / 25 m; the Seto Bhairav screen on its range; Panch Mukhi's five
  round roofs with the thin red-and-white fringe; strut feet clamped to the cornice.
* r4: the gate's door opens into the passage (Narasimha visible; the block's west face no longer covers it); the
  triangle pass below; the in-situ NW gate view moved out of Jagannath.
* r5 (Basantapur fronts, render | photo c_00, c_02, c_10): the fronts read as an office grid of small windows; the photos
  show few, heavy Newar windows and long tile **pent roofs** at mid height. Now:
  * a pent-roof kit piece (`Kp.Pent`: concave tiled slope, fascia, soffit, end boards, tile courses, plain or carved
    figure struts) on the Lohan south range (eave 7.35 m over a carved lattice band, a short storey of small windows
    above it under the main eave) and on the white SW wing (eave 8.35 m between the second and top rows of windows);
  * `Kp.FramedWindow`: the big carved storey window with the long-eared lintel and sill (the T-shape of a Newar window
    seen from the square), stepped mouldings, close lattice and an optional crest;
  * the Basantapur tower's base faces rebuilt to the rebuilt south face (c_00): plain brick ground storey with thin
    brick courses, three carved windows on the second storey (the middle one larger under a crest), a carved cornice,
    the projecting three-bay window between two small windows, and under roof 1 the continuous lattice gallery on a row
    of carved figure struts (`Kp.FigureStrut`); on the Nasal Chowk side the carved door and the three red-framed arches
    over a raised stone plinth with steps (nasal_chowk c_08) in place of the generic lean-to;
  * the white wing's ground floor of round-headed windows with orange-brown lattice (`Kp.ArchedLatticeWindow`),
    recessed plaster panels between the shuttered windows (`Kp.BlindPanel`), four rows of openings as in c_02;
  * colours: the tiles lighter and warmer (#875A46, tower #8C5C49, the rebuilt terracotta), the Lohan complex in the
    rebuilt brick (#AA5843).
* r5 (Gaddi Baithak portico, c_08, c_09, c_16): the three bays were flat teal windows on the wall; now an arcade face
  0.9 m proud of the hall wall with three tall round-headed recesses (archivolt, soffit, shaded back wall) holding a
  pedimented teal door and an arched teal window each; Ionic scrolls and an echinus ring on the six capitals; a row of
  urns along the attic over the columns; the balustrade's piercings as slim crossed lozenges in dark grey.
* r5 (Hanuman, c_14): the parasol's canopy is grey metal, taller (0.55 m), with the deep red valance (#B8262B).
* r5 (Taleju from Makhan Tole, c_25, c_02): the ground body above the gilt windows was a bare brick field; now a carved
  frieze of small panels clears the torana, the zone under the deep soffit behind the long struts is dark timber, and a
  dark carved window flanks the three gilt windows on each side with a small carved window near each corner.
* r5 (Kumari Ghar, c_00): the white lions stand on grey stone pedestals (they were red).
* r5 (z-fighting): every arch's flat spandrels lifted off the wall plane they sat on (blind arches, Gaddi's windows, the
  Hnulunche gate, the Singha Dhoka, the Seto Bhairav screen, the white shrine).
* r6 (Nasal Chowk looking S, nasal_chowk c_08 from the north-west corner): the photograph everyone takes shows, under the
  Kirtipur tower's copper hood, a tall brick front on a raised stone plinth with steps, a carved door, the three
  red-framed arches of a small arcade, a projecting carved window and a deep pent roof; the model had the low red-post
  veranda there. The south-east stretch of the court's east side is now that front (`KpCourts.KirtipurFront`: plinth
  and steps, door with crest, red arcade, long-eared windows and the bay, pent roof on figure struts at 7.3 m, an upper
  storey of small windows, the range roof at the Kirtipur skirt level); the low veranda keeps the northern stretch. The
  Basantapur tower's Nasal Chowk face keeps a carved door and two small windows on the plinth. The in-situ view moved
  to the north-west corner looking south-east, where the photo was taken.
* r6 (Lalitpur tower and the east shop range, lalitpur_tower c_04): the shop range read taller than the tower's first
  roof; in the photo its eave is about 5 m (shopfronts, a low upper floor of small carved windows close under the
  eave) under a big deep roof. Eave 8.0 → 5.3 m, ridge 10.8 → 9.6 m. The in-situ view moved east along the street to
  the photo's position.
* r6 (streamed ground): the street south of the shop range floated over the levelled square because the derived road
  heights (`StructureFallback`) were taken before the palace's ground was levelled; the streamer now levels the site
  first (`HeroBuilder.PrepareGround` before `StructureFallback.Apply` in `TileBuild`).
* r7 (v9 again, lalitpur_tower c_04): the street still tilted up to the shop range where the square's level zone ended
  at the range; a level zone now covers the shop range and the street along its front (`KpGround`), so the street
  meets the shopfronts flat as in the photo.
* r7 (v7, taleju c_25 and c_02): the shop row under Taleju read as a band of flat colour; now dark open shopfronts with
  narrow strips of hanging goods in a few colours, as in the photos.
* r7 (v3/v4, panchamukhi c_10): the small white shrine stands at the court's south-east, in front of the east range
  and right of the stone platform, where c_10 shows it.

Judged round 1 (scores 5, 6; seventeen findings F1-F17), fixed in r8-r11 and re-rendered from the judges' viewpoints
(`r8/tools/views.sh`, `views2.sh`; the judges' photos cut into `r8/photos/`; renders in `r10/views/`, `r11/views/`):

* F1 Taleju tiers (p1b, v7, p2, p8): roof 1 was 33 m on a 19 m body with its eave at 12 m, so it sagged down to the
  Singha Dhoka's crest and hid the body. Now roof 1 is 27 m (1.4 × the body) with its eave at 14.3 m: the gilt-window
  storey stands 7.5 m tall and shows whole above the crest from the forecourt; roof 1 / body reads 1.43 from Makhan
  Tole (photo 1.36). Upper tiers moved up keeping the 36 m top; slopes 25-30° (sag 4 %). Roof 2's half span stays 9.4 m
  rather than the judge's 11.4 m: c_25 and ref_02 both give roof 2 / roof 1 at 0.62-0.63.
* F2 struts (p11, p10, p12): they were 5.5 m logs at 25° fanning out to the eave edge. Now slender, steep (60°),
  close-set (0.8 m on the temples, 1.0 m on the ranges), reach capped at 0.45 × the eave depth and 1.25 m, heads tight
  under the inner purlin, griffin struts on the corners; the soffit rafters carry the outer eave. The Lalitpur shop
  range has its row of small carved windows under the 5.3 m eave and its roof at about 35°.
* F3 Taleju storeys, finial, corners (p11, p8): the upper storeys are dark carved timber on a brick foot band; the
  ground storey's middle three windows have gilt frames and gilt toranas, the others dark carved frames; the gajur is
  the bell, spire, four corner pinnacles and four stays, with the pataka down the west face; horns on every roof
  corner; body brick #7A3E30.
* F4 Kirtipur hood (p12, k1, n1): the pumpkin dome is now the Bangla bell roof (S profile, short ridge, flared lip
  curling into corner horns, plan exponent 8, ribs on the hips only), enlarged in r11 to the photo's proportions (lip
  1.3 m beyond the drum, rise 4.1 m); the drum's faces are cusped-arch lattice windows with tall pointed heads.
* F5 Gaddi south front (p6, p7): a free-standing giant colonnade (0.9 m columns, pedestals to 6.2 m, shafts to
  14.6 m, Ionic capitals) on the podium before a loggia 2.5-3 m deep with white doors in shaded arches; end bays
  project; in r11 the remaining teal glazing in the arched openings became shaded recesses with dark-green bars.
* F6 window kit (p3, p4, p5, p9, v1, v2, v10): thick dark moulded frames with long T-ears and a fine 45° brown lattice
  in every Newar window; Rana windows with lattice or white jali and white louvred shutters, cusped stucco hoods on
  the gate front; no glass on the palace.
* F7 Kumari Ghar (p3, p4): fan toranas over three-bay carved windows at 0, ±4.6, ±8.4 m; the continuous cusped top row
  set back under a 1.6 m eave; broad carved string courses; five separate slim pinnacles on the ridge.
* F8 gate (p5, v1, v2): pale grey-blue pilasters with medallions and eyes, stepped gilt door frame, medallion frieze,
  seated figures with the aureole under three toranas, lions with riders and marigold garlands.
* F9 Hanuman (p5): the lathed draped figure on the 1.8 m lotus pedestal with garlands and the two pataka poles.
* F10 Singha Dhoka (p1, p1b, t3): the gilt torana with its kirtimukha, projecting red piers with large eyes and
  roundels, the three-tier white crest with the small open pavilion, lions on grey stone pedestals.
* F11 Degu Taleju base (p9, d2): one bay window and two long-eared windows per face on the top two storeys, plain
  brick and a carved cornice below, the oculus, the red-valanced pent roof over the screen range.
* F12 Panch Mukhi (n1): terracotta tile roofs, each with a 0.5 m red valance under a white band.
* F13 black gallery (p5, v1): lattice screens fill the bays between the posts; a narrow open band under the top beam.
* F14 Dakha west (f2, f3, plan): the four outlines are built as white palace ranges (§2.8) and hidden from the house
  bands; a test checks that no house the streamer draws has its centroid inside the palace outline.
* F15 forecourt (f2, f3, ground_top): grey flags wall to wall, the level zone extended; the walkable stone collider is
  built in rows of 2.4 m cells that keep 0.2 m clear of the drawn footway and pedestrian ribbons still crossing the
  paving (where they cross, the road's surface is the floor until the roads package clips them).
* F16 white wing (s1, s2b): the west block three storeys under one 9.6 m eave; the east block's attic set back; one
  continuous plinth band and fascia.
* F17 Basantapur frontage (s1, l5): flags, stone curb and the black railing with its colliders along the wing and the
  tower, as along the Lohan range.

### Triangle budget (LOD0 / LOD1 / LOD2, as built)

| Part | LOD0 | LOD1 | LOD2 | LOD3 |
|---|---|---|---|---|
| Taleju | 38,970 | 13,282 | 1,879 | 82 |
| Basantapur tower | 24,979 | 6,037 | 575 | 56 |
| Dakha range and its west continuation (forecourt fronts, museum block, the four white ranges) | 24,476 | 9,146 | 1,548 | 42 |
| Kumari Ghar | 23,095 | 7,882 | 1,036 | 26 |
| Gate group (gate, Hanuman, lions, Narasimha, forecourt flags) | 22,692 | 5,619 | 771 | 32 |
| Nasal Chowk ranges | 20,290 | 6,155 | 886 | 44 |
| Lohan Chowk | 19,816 | 4,025 | 716 | 48 |
| Gaddi Baithak | 16,942 | 5,676 | 1,839 | 46 |
| Degu Taleju | 16,726 | 4,746 | 705 | 56 |
| Lalitpur tower | 13,302 | 3,093 | 463 | 36 |
| Kirtipur tower | 13,030 | 3,641 | 547 | 38 |
| Mohan Chowk | 11,160 | 2,782 | 845 | 26 |
| South (white) wing | 10,894 | 3,503 | 372 | 40 |
| Panch Mukhi | 7,318 | 2,023 | 593 | 96 |
| Mul Chowk | 5,756 | 1,866 | 575 | 26 |
| Bhaktapur tower | 4,892 | 813 | 197 | 52 |
| **Site** | **274,338** | **80,289** | **13,547** | |

The site is sixteen structures; Taleju, the one major temple, is about 38,970 at LOD0 (the round allows 60,000). The
whole site is held to 285,000 at LOD0 (`KdsPalaceSite.SiteBudgetLod0`), above the round's ~150,000 guide for a
single-temple site: the judged round added the close-set slender struts (0.8 m on the temples, 1.0 m on the ranges,
about 44,000 in all), the 45° lattice in every Newar window (about 23,000), Taleju's timber storeys and gajur, the
gate's figures, the four white ranges west of the forecourt (which replace four generic houses) and the forecourt
flags. The hero allocator shows a part at LOD0 only while it fills a third of the screen, so a handful at once at most
(standing in Basantapur: the tower, Lohan, Kumari Ghar and the white wing, about 79,000; on the forecourt the gate,
the Dakha fronts and Degu Taleju, about 64,000, with Taleju at LOD1). Every part keeps LOD1 under 0.45 × LOD0, LOD2
under 0.12 × LOD0 and LOD3 at 200 or fewer (`KdsPalaceSite.Budgets`, checked by the tests). Earlier passes: in r4 the
site came from 300,000 to about 200,000 (a lean lattice window, standing seams as two-faced ribs every 1.9 m, carved
figure struts only under the lowest roof, tile courses every 1.7 m on the long ranges, the generic figure for the
stone and Kumari and Singha Dhoka lions, small finials as cones from LOD1, the enclosed courts one level coarser than
the street faces).

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
* `TileBuild` now levels every hero site's ground (`HeroBuilder.PrepareGround`) before `StructureFallback` derives the
  road heights, so the derived roads of any site with level zones sit on the levelled ground (for the palace: the
  streets round Basantapur and the shop range). Sibling sites that flatten ground get the same order.
* The Kirtipur front on Nasal Chowk's south-east side is read from c_08, the aerials and the OSM outline; what lies
  behind it (the rooms between Nasal Chowk and Lohan Chowk) is massed only.
* Open issue (roads): the road mesher draws the footway and pedestrian ribbons across the forecourt (grid a -50..-11.6,
  b -7.5..+30) over the new flags. This package publishes the paved area as a no-road mask, `KpGround.Paved(tile, x,
  z)` (and `KpGround.InForecourt(a, b)` in grid metres); the roads package should clip its ribbons inside it.
* Judge suggestions not taken, with the photo evidence: Taleju's roof 2 half span 9.4 m, not 11.4 m (roof 2 / roof 1
  measures 0.62-0.63 on c_25 and ref_02); the strut reach capped at 1.25 m, not 2.2 m (2.2 m on a 60° strut gives
  struts about 4.4 m long; the photographed tunala are 2-2.5 m).
* The cream and pink houses seen beyond the forecourt's west end from the gate corner stand west of the street,
  outside the palace outline (OSM and 2026 imagery); they are ordinary houses.
