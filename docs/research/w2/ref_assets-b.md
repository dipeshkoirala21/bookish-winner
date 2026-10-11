# Reference brief: palms, bananas, bamboo, sayapatri beds, the Dharahara and Manga Hiti (assets-b)

What stands there **now**, the silhouette and proportions to hold, colours (hex, cartoon-boosted), the details a
local checks, the photos used and what the render-vs-photo comparison changed. Photos are reference only (never copied
into the repo); contact sheets live under `/home/user/wt/refs/assets-b/`, the side-by-side comparisons under
`/home/user/wt/previews/assets-b/v2/` (`cmp_far_*.png`, `cmp_marigold.png`, `cmp_dharahara*.png`, `lods_*.png`) and
`/home/user/wt/previews/assets-b/` (`cmp_manga_*.png`, `mh_*.png`).
Code: `Core/Generators/Flora/FountainFar.cs` (far forms of the palm, the banana clump and bamboo), `FloraMesher.cs`
(far-form table), `TreeForms.cs` (bamboo LOD0/LOD1, banana), `PlantForms.cs` (marigold bed),
`Core/Generators/Sacred/HeroForms.cs` (`Dharahara`, `Hiti`, `ManiMandap`); the renderer wiring in
`World/Instancing/DressingRenderer.cs` (outside this package, §5.1). Tests: `core-tests/GeneratorsFloraLookTests.cs`
and `HeroLandmarkLookTests.cs` (the reproductions of the findings, on the build API that existed before the fix, so they
run against the old code too: §6), `GeneratorsFloraTests.cs` (`LodsKeepTheSilhouette`, far-form table),
`HeroLandmarkFormsTests.cs` (the hiti in detail, preview dumps), `GeneratorsFloraSilhouette.cs` (the outline, solidity
and view-share measures).

## 1. Palms, bananas and bamboo at every distance

**What is there.** Palms in hotel and garden compounds (pinnate royal/date-type palms and clustered areca; refs
royalpalm 00, 04): a slender grey-brown ringed trunk (`#8A7A64`), about two thirds of the height, carrying a starburst
of arching fronds: young ones up, mature ones out level, old ones drooping well below the crown point. Bananas (kera)
beside 30 % of peri-urban and village houses (refs banana 03, 09): green pseudostems (`#8DA04A`) to about half the
height, crowned by huge paddle leaves 2-2.6 m long and 0.5-0.6 m wide, the young ones held up, the older ones arching
out with drooping, torn tips. Bans bamboo clumps behind one village house in four (refs dendrocalamus 05,
bamboo_clump 09): culms splaying from a narrow foot into a vase to about 0.45 of the height, then a broad, ragged crown
of feathery sprays hanging from the arching culm tops and drooping at the edges (12 × 7 m): a fountain, never a round
head.

**What the game drew (finding).** At LOD2 (from about 150 m, nearer on Low) and LOD3 every Fountain-family tree became
the broadleaf volume, a lumpy ball on a stick (`cmp_far_*.png`, right column "before").

**Now.** Seven far forms (`FloraMesher.FarForms`): the four crowns of leaves, the **banana clump**
(`TreeShape.Fountain`), the **palm** (`PalmForm` = 5) and the **bamboo** (`BambooForm` = 6), picked per species with
`FloraMesher.FarFormOf`.
* Banana clump: three pseudostems splaying from one foot to 0.45-0.62 of the height, each crowned by three bent paddle
  blades (one rising, two arching out and drooping): 108 triangles; LOD3 two crossed V cards and ten sails (24).
* Palm: a leaning ringed trunk to 0.76, eight fronds in three tiers (rising, level, drooping), a hanging dead frond
  (106); LOD3 a three-sided trunk, seven frond sails and the spear (19).
* Bamboo: eight culms along the detailed clump's own arch (`TreeForms.BambooArch`: splaying from the foot, topping out
  at 0.89 of the culm height some 0.6 of the way out, drooping to 0.7 at the edge), each a thin stem strip under a
  leafy curtain hanging down and out from the arch (112); LOD3 two crossed V cards to 0.64 and ten draped sails,
  turned a little about the culm so they show from every side (24).
* Bamboo LOD0 and LOD1 follow the same vase: the culms splay from the foot instead of rising straight, and the sprays
  are larger (LOD0 cards 0.6 m wide, 1.3-1.8 m long, five sprays per culm from a third of the way up; LOD1 1.1 m wide)
  so the crown is the broad hanging mass of the photos (LOD0 1 568 triangles, LOD1 216).
* Banana LOD0/1 leaves: longer (0.52 w), wider (0.13 w), the older ones lower and drooping more.

**Checks.** `PalmsBananasAndBambooStayBladesAtEveryFarLod` (side outlines from four bearings at a few dozen pixels):
each far LOD matches the species' LOD0 clearly better than a round crown (palm, banana; bamboo at least about as well,
since a blurred ball also covers its broad crown), its crown has gaps like LOD0's (row solidity within 0.15 of LOD0,
where the round crown is solid at 1.0), the blades reach the crown edge, palm fronds start on a bare trunk, banana
leaves start low and bamboo sprays start where LOD0's do. Measured now (outline vs LOD0, round crown in brackets;
solidity, LOD0's in brackets): palm LOD2 0.48 (0.21), 0.48 (0.62); LOD3 0.27 (0.20), 0.48. Banana LOD2 0.32 (0.18),
0.52 (0.52); LOD3 0.25 (0.17), 0.65. Bamboo LOD2 0.58 (0.54), 0.72 (0.75); LOD3 0.56 (0.56), 0.85.
Comparisons: `cmp_far_palm.png`, `cmp_far_banana.png`, `cmp_far_bamboo.png` (photos, LOD0, LOD2, LOD3, the pre-fix ball;
bottom row at 1/7 size), `lods_Bamboo.png`, `lods_palm_banana.png`.

## 2. Sayapatri (African marigold) beds

**What is there.** In October-November (Tihar) the beds of parks, temple gardens and the Maitighar collar are a dense
carpet of round pom-pom heads held just above dark green ferny leaves: deep orange `#F5821F` with golden yellow
`#F6B21B` and a few red-orange `#E8641A` (refs marigold_np 05, Ghandruk; marigold_field 00, 07; marigold_ktm 08, 09 for
the colours of the Tihar garlands of Kathmandu). Heads of the cultivar are 6-10 cm; the game draws them bolder.

**Was (finding).** 46 small ellipsoid heads at LOD0 (6 at LOD1) at a fixed height, many inside the foliage mounds: 25 %
of the bed read as bloom from above at LOD0, 3 % at LOD1 (`GeneratorsFloraLookTests`, on the pre-fix code).

**Now.** LOD0 (1 550 triangles): four foliage mounds fill the brick-edged bed; a jittered grid of 55 ruffled pom-poms
(r 0.12 m: crown point, upper ring of five, ruffled rim of ten; ball normals, lighter crown, darker rim) held at the
crest of the mound under each one. LOD1 (drawn far, 208 triangles): one broad mound with an orange-flecked top and 21
domed heads of r 0.235 m over the whole bed top, capped by the dome's own height so they no longer sink into the
mound's middle. `FloraCatalog.Bloom` thins the carpet in the other months.

**Checks.** `MarigoldBedIsACarpetOfPompomsAtTihar` (depth-tested views; the share of the foliage and soil pixels that
are bloom): October LOD0 62 % from above and 58 % from 30° (at least 60 / 50 required), LOD1 54 % / 47 % (45 / 40), July
LOD0 47 %, LOD1 42 % (30). Comparison: `cmp_marigold.png`.

## 3. The Dharahara (rebuilt 2021, Sundhara)

**What is there** (refs dharahara_new c_00, c_02, c_05, c_07, c_11: Commons photos from Sundhara after the 2021
reopening): a white `#F7F5EF` round tower of 72 m on a dark stone stepped plinth; the shaft tapers from about 6.0 m to
4.75 m radius (1.26 : 1) and **telescopes** in nine sections, each storey band a projecting pale cream `#F0E6CC`
moulded ring (eight below the gallery at about 9, 15, 21, 27, 33, 39, 45 and 51 m); small round porthole windows on
every section; the corbelled balcony gallery at about 57.5 m with a white balustrade (oversailing the shaft by about
2.2 m); a drum with arched openings and a cornice; a narrower collar ring; a tall white bell dome (r ≈ 3.55 m, rising
about 4 m) and a gilt kalash and needle spire to 72 m (photos: gold, not bronze). OSM (w1414016053, w1413193272) maps
the shaft as one cylinder of r ≈ 5.2 m to 61 m and the drum as r ≈ 4.7 m from 58 to 63 m; the photo-measured taper is
kept (the OSM parts are coarse). The pre-2015 tower (refs temples/dharahara) was nine storeys and 62 m: not used.

**Was (finding).** An almost straight fluted cylinder with 10 cm bands (a 4 cm ring at 9 m, measured by the exact
section in `HeroLandmarkLookTests`) and a squat quarter-ellipse cap under a bronze mast.

**Now.** Every cornice is a lathed ring with an undercut cove projecting 0.49 m (0.36 m under the gallery; LOD2 a
0.34 m shelf). The top was refined in this round against the c_00 and c_11 crops: the drum cornice steps in to a
narrower collar ring, the dome is a taller bell narrower than the collar (base r 3.55 m instead of 3.95 m), the kalash
sits on its crown and the balustrade has fat white balusters under a thicker rail. Triangles 11 388 / 4 260 / 739 / 195.
Collider: a solid cylinder for the shaft and three walkable plinth steps. Comparisons: `cmp_dharahara.png`,
`cmp_dharahara_top.png`.

## 4. Manga Hiti and the Mani Mandap (Patan Durbar Square)

**What is there** (refs mangahiti_c 00-17, Commons; OSM): a sunken stepped stone pit at the north-west corner of the
square. OSM maps its walls: the outer brick wall w328923444 from −2.5 m to a 0.8 m parapet, 0.4 m thick, enclosing about
15 × 15 m, and the inner wall w328923445 from −3.5 to −2.5 m round about 9 × 9 m; the stair w328922776 enters from the
west between two short walls (w328921958, w328923443), next to the **Mani Mandap**, two open royal pavilions of 1700
(w328706342, w328706352: about 7 × 7 m roofs, 6 m high, pyramidal tile roofs on carved posts on a brick plinth). In the
photos: red-brick walls (`#B5533A`) with a stone string course, a brick-paved ledge with potted plants, a lower stone
tier (`#8C887C`, mossy `#737A62`) carrying **three makara (crocodile) spouts** under horseshoe stelae with deity niches
(c_12, c_14), the water channel at their foot, a flagstone court with a round stone basin (c_04, c_10), black iron
railings on the stair walls (c_01, c_06).

**Was (finding).** A raised stone tray: rings stepping down from a 0.5 m rim to a floor 0.11 m above the square (on
the pre-fix code: lowest point −0.2 m, 1.95 m of it standing above the square, no way down).

**Now.** `HeroForms.Hiti` builds the pit below grade: parapet with coping (0.8 m), broken at the front (+W) by the main
stair; brick pit walls to the terrace ledge (`HitiLevels`: −2.5 m for Manga Hiti, shallower for small hitis); stone
flights stepping down both sides to the court (−3.44 m for the catalogue's 12 × 8 m, −3.5 m at the OSM size); the back
(−W) spout wall with the makara spouts (snout, head, eyes, crest, curled lip), stelae with niches and water; the basin,
pots and railings on a hero-sized pit. Colliders: parapet walls, walkable ledge and court boxes, a ramp down the stair
(3.44 m at 12 × 8 m, 2.35 m for a 6 × 4 m hiti). With `maniMandap: true` the two pavilions stand beyond the stair.
Budgets: Manga Hiti at the OSM size 7 272 / 1 853 / 464 / 70; a 6 × 4 m generic hiti 712 / 347 / 110 / 42 (generic
caps 1 500 / 600 / 120 / 120). Comparisons: `cmp_manga_spouts.png`, `cmp_manga_mandap.png`, `mh_lod0.png` (the pit in a
paved square with the opening cut, as the game will draw it once the ground is cut, §5.2).

## 5. Open issues (outside this package)

1. **Far forms per species: wired here, a minimal edit outside the package.** `World/Instancing/DressingRenderer.cs`
   now builds one far batch pair per `FloraMesher.FarForms` form with `FloraMesher.Far(form, 2 + k, m)` and picks each
   tree's batch from a per-species table of `FloraMesher.FarFormOf(species)` instead of `(int)t.Shape` (nine lines; the
   owner of the renderer should review it at merge). The engine-free budget estimates in
   `core-tests/GeneratorsFloraBudgetTests.cs` and `GeneratorsPlacementScenes.cs` still price far trees per `TreeShape`
   family; the palm (106 / 19) and bamboo (112 / 24) forms cost within 4 triangles of the banana clump (108 / 24), so
   the budgets hold, but they could switch to the far-form table.
2. **Cut the ground at hitis (terrain, areas, ground query).** The pit lies below grade, so the terrain mesher, the paved
   AREA mesher and `TileGroundQuery` must leave `HeroForms.HitiGroundCut(w, d)` (the inside of the parapet, in the
   hero's frame) open, like the footprint of a hero plinth, and let the hiti's walkable colliders be the ground there.
   The cutfill package's carved-surface work (TerrainCarve) is the natural home. Until then the square's surface covers
   the pit (only the parapet shows) and the player walks over it. No hiti is built in the `kathmandu_core` sample today
   (Manga Hiti is in Patan, outside it, and the POI hitis of `SacredSelector.TrySelectPoi` have no caller), so nothing
   the owner plays changes before the cut lands.
3. **Hero builder and data (`HeroBuilder.cs`, `HeroCatalog.cs`, the pipeline's hero recipes).** (a) Manga Hiti: PlanW /
   PlanD 15 × 15 m and yaw 285° (the stair enters from the west, the spouts face west from the east wall), anchored at
   the pit centre (≈ 27.673636, 85.325448: the POI n10034234987 lies 13 m off it); call
   `HeroForms.Hiti(..., maniMandap: true)` **only together with** hiding the two OSM `building=roof` ways w328706342
   and w328706352 in the recipe's hidden list, or the pavilions draw twice and z-fight (playtest #4). `stats.TopM` for
   a hiti is the parapet, 0.8 m (6 m with the Mani Mandap). (b) Route the LOD3 of the Dharahara and of hitis to
   `HeroForms.Dharahara(..., lod 3)` / `HeroForms.Hiti(..., lod 3)` instead of the generic box-and-pyramid `Lod3` (a
   39.6 m box under a pyramid for the tower; a raised slab for the hiti). (c) The pipeline's Dharahara recipe still
   carries `fluted: yes`, `cap: cone`, `mast: bronze`; the form ignores them, but the data should say `fluted: no`,
   `cap: bell_dome`, `finial: gilt`.
4. **Photos to verify.** No open-licensed close-up of the rebuilt Mani Mandap: the pavilion form follows the pre-2015
   photo c_00 and OSM. Openverse holds no open photo of a sayapatri bed inside the Kathmandu valley; the colours and the
   head form follow the Ghandruk photo and the Tihar garlands of Kathmandu.

## 6. Verification: the findings reproduced on the old code, passing on the new

The look tests use only the build API that existed before this package's changes (`FloraMesher.Build`,
`FloraMesher.Family`, `HeroForms.Dharahara`, `HeroForms.Hiti` with its original parameters), so the same files were
compiled against the pre-fix Core (the parent of commit `4154cc2`) and against this branch:

| Check | Before | After |
|---|---|---|
| Palm LOD2: outline vs LOD0 (round crown) / row solidity / lowest leaf | 0.23 (0.21) / 1.00 / y 0.36 | 0.48 (0.21) / 0.48 / y 0.50 |
| Banana LOD2: outline (round crown) / solidity | 0.21 (0.19) / 1.00 | 0.32 (0.18) / 0.52 |
| Bamboo LOD2: outline (round crown) / solidity | 0.46 (0.43) / 1.00 | 0.58 (0.54) / 0.72 |
| Palm / banana / bamboo LOD3 solidity | 1.00 / 1.00 / 1.00 | 0.48 / 0.65 / 0.85 |
| Marigold bed bloom from above, October, LOD0 / LOD1 | 25 % / 3 % | 62 % / 54 % |
| Dharahara: storey cornice at 9 m out of the shaft (exact section) | 0.04 m | 0.49 m |
| Manga Hiti (12 × 8 m): lowest point / height above the square / stair drop | −0.20 m / 1.95 m / none | −3.64 m / 1.77 m (railings) / 3.44 m |

## 7. Photo credits (reference only)

royalpalm 00 "Problematic Royal Palm Tree", thienzieyung, CC BY 2.0; royalpalm 04 "Juvenile Royal Palm tree",
lezumbalaberenjena, CC BY-NC-ND 2.0; banana 03 "Banana Plants", alcides OTA, CC BY-NC 2.0; banana 09 "Banana plants",
christinejwarner, CC BY 2.0; dendrocalamus 05 "Dendrocalamus membranaceus", tanetahi, CC BY 2.0; bamboo_clump 09
"clumping bamboo", colleendowd, CC BY-NC-SA 2.0; marigold_np 05 "Marigolds - Ghandruk - Trekking in Nepal", Phil @
Delfryn Design, CC BY-NC-SA 2.0; marigold_field 00 and 07 "Marigold Field", sakeeb, CC BY 2.0; dharahara_new c_00 and
c_02 "Dharahara after renovation", Nirjal stha, CC BY-SA 4.0; dharahara_new c_11 "Dharara Tower KTM", Harsh Kumar,
CC BY-SA 4.0; mangahiti_c 00 "Manga Hiti on Patan Durbar Square", Carlos Felipe Pardo, CC BY 2.0 (the rest in
`/home/user/wt/refs/assets-b/*/credits.txt`).
