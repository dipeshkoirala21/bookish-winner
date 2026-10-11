# Reference brief: palms, bananas, bamboo, sayapatri beds, the Dharahara and Manga Hiti (assets-b, final visual fix)

What stands there **now**, the silhouette and proportions to hold, colours (hex, cartoon-boosted), the details a
local checks, the photos used and what the render-vs-photo comparison changed. Photos are reference only (never copied
into the repo); contact sheets and side-by-side comparisons live outside the repo under
`/home/user/wt/refs/assets-b/` and `/home/user/wt/previews/assets-b/` (`cmp_*.png`, `lods_*.png`, `marigold_*.png`).
Code: `Core/Generators/Flora/FountainFar.cs`, `FloraMesher.cs` (far forms), `TreeForms.cs` (banana, bamboo LOD1),
`PlantForms.cs` (marigold bed), `Core/Generators/Sacred/HeroForms.cs` (`Dharahara`, `Hiti`, `ManiMandap`). Tests:
`core-tests/GeneratorsFloraTests.cs` (`LodsKeepTheSilhouette`, `PalmsBananasAndBambooKeepTheirFarSilhouettes`),
`core-tests/HeroLandmarkFormsTests.cs`.

## 1. Palms, bananas and bamboo at every distance

**What is there.** Palms in hotel and garden compounds (pinnate royal/date-type palms and clustered areca; refs
royalpalm 00-07): a slender grey-brown ringed trunk (`#8A7A64`), about two thirds of the height, carrying a starburst
of arching fronds: young ones up, mature ones out level, old ones drooping well below the crown point. Bananas (kera)
beside 30 % of peri-urban and village houses (refs banana 03, 04, 09): one or more green pseudostems (`#8DA04A`) to
about half the height, crowned by huge paddle leaves 2-2.6 m long and 0.5-0.6 m wide, the young ones held up, the
older ones arching out over the garden with drooping, torn tips. Bans bamboo clumps behind one village house in four
(refs dendrocalamus 05, 07, bamboo_clump): culms from a narrow base splaying into a vase, the upper half of every culm
hung with feathery leaf sprays that arch out and droop at the edge (12 × 7 m) — a fountain, never a round head.

**What the game drew.** At LOD2 (from about 150 m, nearer on Low) and LOD3 every Fountain-family tree became the
broadleaf volume: a lumpy ball on a stick (finding, `png/flora/lods_m4.png`); bamboo's LOD1 was two balls on five
culms.

**Now.** Far forms (`FloraMesher.FarForms` = 6): the four crowns of leaves, the **clump** (`TreeShape.Fountain`: banana
and bamboo) and the **palm** (`FloraMesher.PalmForm`). The clump: three stems splaying from one foot to 0.45-0.62 of
the height, each crowned by three bent blades (one rising, two arching out to the edge and drooping) — 108 triangles;
LOD3 draws stems and blades as two-sided triangles (24). The palm: a leaning ringed trunk to 0.76, eight fronds in
three tiers (rising, level, drooping), a hanging dead frond (106 triangles); LOD3 a three-sided trunk, seven frond
"sails" and the spear (19). Bamboo LOD1: six arching culms hung with two sprays of big leaf cards each (no balls);
bamboo LOD0 lost its two round inner plumes and has 14 culms. Banana LOD0/1 leaves: longer (0.52 w), wider (0.13 w),
the older ones lower and drooping more.

**Checks.** `LodsKeepTheSilhouette` now also compares the far-off outline (side views from four bearings blurred to a
few dozen pixels; soft IoU) for the crowns; `PalmsBananasAndBambooKeepTheirFarSilhouettes` asserts that each fountain
species' own far form matches its detailed model better than a round crown, that its crown is blades with gaps (row
solidity well under a round crown's 1.0), that palm fronds start on top of a bare trunk and that clump leaves start
low on their stems.

**Wiring (open issue, §5.1).** The instanced renderer keeps one far mesh per `TreeShape`; until it keys them by
`FloraMesher.FarFormOf(species)` palms draw with the clump form.

## 2. Sayapatri (African marigold) beds

**What is there.** In October-November (Tihar) the beds in parks, temple gardens and the Maitighar collar are a dense
carpet of round pom-pom heads, deep orange `#F5821F` with golden yellow `#F6B21B` and a few red-orange `#E8641A`,
covering most of the green (refs marigold_bed 03, 04; nature/marigold for the colours of Tihar garlands). Heads of
the cultivar are 6-10 cm; the game draws them bolder (r 8-11 cm).

**Now.** LOD0 (1 550 triangles): four unturned lumpy foliage mounds fill the brick-edged bed; a jittered grid of 55
ruffled pom-poms (crown point, upper ring of five, ruffled rim of ten; 15 triangles each, ball normals, lighter crown,
darker rim) seated on the mound surface under each one, so about 60 % of the bed reads orange in bloom (fewer in other
months, from `FloraCatalog.Bloom`). LOD1 (drawn far, 226 triangles): one broad mound with an orange-flecked top and 21
domed heads of r 0.16 m set into it.

## 3. The Dharahara (rebuilt 2021, Sundhara)

**What is there** (refs dharahara_new c_00, c_02, c_05, c_07: Commons photos from Sundhara after the 2021
reopening): a white `#F7F5EF` round tower of 72 m on a dark stone stepped plinth; the shaft tapers from about 6.0 m to
4.75 m radius (1.26 : 1) and **telescopes** in nine sections, each storey band a projecting pale cream `#F0E6CC`
moulded ring (eight below the gallery at about 9, 15, 21, 27, 33, 39, 45 and 51 m — about one ring per 2¾ of the 22
storeys inside); small round porthole windows on every section; the corbelled balcony gallery at about 57.5 m with a
white balustrade (oversailing the shaft by about 2.2 m); a drum with arched openings, a cornice and a collar; a white
bell dome (r ≈ 3.9 m) and a gilt kalash and needle spire to 72 m (photos: gold, not bronze). The pre-2015 tower (refs
temples/dharahara) was nine storeys, 62 m: not used.

**Was.** An almost straight fluted cylinder with 10 cm bands and a squat quarter-ellipse cap under a bronze mast.

**Now.** Every cornice is a lathed ring with an undercut cove projecting 0.36 m (LOD0-1) or a 0.34 m shelf (LOD2),
measured by `DharaharaHasProjectingStoreyCorniceTaperAndDomeSpireTop`; the top (drum, collar, dome, gilt spire) rises
9.6 m above the collar, 2.5 times the dome's base radius. Triangles 11 400 / 4 260 / 739 / 195 (LOD3 is supported for
the hero far LOD, see §5.3). Collider: a solid cylinder for the shaft and three walkable plinth steps.
Comparison: `previews/assets-b/cmp_dharahara.png`, `dh_lods.png`.

## 4. Manga Hiti and the Mani Mandap (Patan Durbar Square)

**What is there** (refs mangahiti_c 00-17; OSM): a sunken stepped stone pit at the north-west corner of the square.
OSM maps its walls: the outer brick wall w328923444 from −2.5 m to a 0.8 m parapet, 0.4 m thick, enclosing about
15 × 15 m, and the inner wall w328923445 from −3.5 to −2.5 m round about 9 × 9 m; the stair w328922776 enters from the
west between two short walls (w328921958, w328923443), next to the **Mani Mandap**, two open royal pavilions of 1700
(w328706342, w328706352: about 7 × 7 m roofs, 6 m high, pyramidal tile roofs on carved posts on a brick plinth). In the
photos: red-brick walls (`#B5533A`) with a stone string course, a brick-paved ledge with potted plants, a lower stone
tier (`#8C887C`, mossy `#737A62`) carrying **three makara (crocodile) spouts** under horseshoe stelae with deity
niches (c_12, c_14), the water channel at their foot, a flagstone court with a round stone basin (c_04, c_10), black
iron railings on the stair walls (c_01, c_06).

**Was.** A raised stone tray: rings stepping down from a 0.5 m rim to a floor 0.11 m above the square.

**Now.** `HeroForms.Hiti` builds the pit below grade: parapet with coping (0.8 m), broken at the front (+W) by the main
stair; brick pit walls to the terrace ledge (`HitiLevels`: −2.5 m for Manga Hiti, shallower for small hitis); stone
flights stepping down both sides to the court (−3.44 m for the catalogue's 12 × 8 m, −3.5 m at the OSM size); the back
(−W) spout wall with the makara spouts (snout, head, eyes, crest, curled lip), stelae with niches and water; the basin,
pots and railings on a hero-sized pit. Colliders: parapet walls, walkable ledge and court boxes, a ramp down the stair.
With `maniMandap: true` the two pavilions stand beyond the stair (eaves just past the parapet, 1.3 m apart).
Budgets: Manga Hiti at the OSM size 7 300 / 1 850 / 460 / 70; a 6 × 4 m generic hiti 712 / 347 / 110 / 42 (generic
caps 1 500 / 600 / 120 / 120). Comparisons: `cmp_manga_spouts.png`, `cmp_manga_mandap.png`, `mh_lod0.png` (the pit in a
paved square with the opening cut, as the game will draw it).

## 5. Open issues (outside this package)

1. **Far forms per species (integration / nature renderer, `World/Instancing/DressingRenderer.cs`).** Build the far
   batches for `FloraMesher.FarForms` forms with `FloraMesher.Far(form, 2 + k, m)` instead of the five `TreeShape`
   families, and pick each tree's batch with a per-species table of `FloraMesher.FarFormOf(species)` instead of
   `(int)t.Shape`. Until then palms draw with the clump form (banana and bamboo, the common ones, are right).
   `KitMeshes.Tree` needs no change.
2. **Cut the ground at hitis (terrain, areas, ground query).** The pit lies below grade, so the terrain mesher, the paved
   AREA mesher and `TileGroundQuery` must leave `HeroForms.HitiGroundCut(w, d)` (the inside of the parapet, in the
   hero's frame) open, like the footprint of a hero plinth, and let the hiti's walkable colliders be the ground there.
   Without the cut the square's surface covers the pit (only the parapet shows) and the player walks over it.
3. **Hero builder (`HeroBuilder.cs`, `HeroCatalog.cs`).** (a) Manga Hiti: PlanW/PlanD 15 × 15 m and yaw 285° (the stair
   enters from the west, the spouts face west from the east wall), anchored at the pit centre (≈ 27.673636, 85.325448:
   the POI n10034234987 lies 13 m off it, n4738424321 "Manga Hiti" fountain is at the centre); call
   `HeroForms.Hiti(..., maniMandap: true)` for it and hide the two OSM `building=roof` ways w328706342 and w328706352 (the
   pavilions the hero now draws). `stats.TopM` for a hiti is the parapet, 0.8 m (Mani Mandap 6 m). (b) Route the LOD3 of
   the Dharahara and of hitis to `HeroForms.Dharahara(..., lod 3)` / `HeroForms.Hiti(..., lod 3)` instead of the generic
   box-and-pyramid `Lod3` (a 39.6 m box under a pyramid for the tower; a raised slab for the hiti).
4. **Photos to verify.** No open-licensed close-up of the rebuilt Mani Mandap (Commons rate-limited the search); the
   pavilion form follows the pre-2015 photo c_00 and OSM.
