# Reference brief: hill roads cut into the slope (cut/fill package)

Owner playtest (docs/W2_PLAYTEST_1.md), screenshot at Gaurighat Hanuman Mandir on the Bagmati by Pashupati:
"Fix the roads on slopes. Make them flat like how they make them, cutting the hills. See, everything is distorted here.
The dog looks like it is sleeping in the air; half of the motorcycle is in the air. Fix this everywhere the roads are in
the hills."

Reference photos are kept outside the repository (`/home/user/wt/refs/cutfill/*/contact_sheet.jpg`, Openverse,
open licences; credits in each folder's `credits.txt`). They are used as reference only and never copied into the game.

## 1. What is there (Kathmandu Valley, 2023-2026)

* **Valley streets** (Chabahil, Gaurighat, Gaushala, Balaju, Swayambhu ring road): asphalt or concrete carriageways of
  5.5-8 m, level across with a small camber, laid on the valley's terraces. Where a street climbs the edge of a river
  terrace (the Bagmati gorge at Gaurighat and Pashupati, the Bishnumati at Balaju) or a hill (Swayambhu, the Nagarjun
  foothills) the hillside is **cut** on the uphill side and **filled** on the downhill side; the road itself never tilts
  with the hill.
* **Cut faces**: fresh cuts in the valley's lake and residual soils are ochre to red-brown earth (photo: ILO "Nepal
  earthquake 2015" road works, `Nepal_landslide_road` ref_04-09); taller and steeper cuts expose layered grey rock (the
  Bagmati gorge below Pashupati shows grey, thinly bedded rock: `Kathmandu_Pashupati_Bagmati` ref_07 "Caves @ Pashupati",
  Anuma S. Bhattarai, CC BY-SA 2.0), often faced with dry stone or concrete breast walls (road to Pharping,
  `Nepal_road_construction` ref_05, Wonderlane, CC BY 2.0; Swayambhu "old cliffside, built up with stones",
  `Swayambhunath_road` ref_01, Wonderlane, CC BY 2.0).
* **Fills and retaining walls**: earth embankments that grass over within a season; where the drop is steep, a stone
  masonry or **gabion** wall (wire baskets about 1 x 1 x 2 m filled with river stone, built in courses that step back
  10-20 cm each, a 10 % batter: `gabion_wall` ref_02, ref_09 and ref_11, International Rivers, CC BY-NC-SA 2.0, stepped
  gabion walls on a Himalayan road).
* **Edge protection**: on newer highways steel W-beam crash barriers on posts; on most hill roads in and around the
  valley a low stone or concrete parapet (about 0.5-0.8 m) or white-painted concrete posts with black or yellow bands.

## 2. Numbers used

| Element | Value | Source and note |
|---|---|---|
| Carriageway camber | 2-2.5 % bituminous, 3-4 % gravel and earth | Nepal Road Standard (NRS 2070) camber table, as summarised in course notes. The game uses **3 %** (`RoadGrade.Camber`). |
| Superelevation | up to about 6 % in the game | No Nepal-specific maximum was found; IRC hill-road practice allows up to 10 %, 7 % where snow-bound. The game caps all cross-fall at **6 %** (`RoadGrade.MaxBank`): e = v² / (225 R), v = 40 km/h (trunk, primary), 30 (secondary, tertiary), 20 (others). |
| Cross-fall on hillsides | toward the hill (inward to the side drain) | Hill road practice; on gentle ground the carriageway leans with the slope (less earthwork). |
| District hill road | 5.5 m pavement, 0.75 m shoulders, max gradient 12 % | Suryabinayak Municipality project report under NRS 2070. |
| Cut slope in soil | 1 : 1 (45°) | Common hill-road practice; Durham University field study notes cuts in Nepal are often steeper than the guidelines and fail in the monsoon. The game cuts at **1 : 1** (`TerrainCarve.CutSlope`). |
| Fill slope | 1 : 1.5 (34°) | Embankment practice. The game fills at **1 : 1.5** (`TerrainCarve.FillSlope`). |
| Walls | breast walls on cuts over about 3 m, gabion walls on embankments over about 4 m | Indian NHIDCL hill-road drawings (not a Nepal DoR rule; used only as a guide). Gabion base 0.4-0.65 H, 10 % batter (Nepal micro-hydro civil works guideline). In the game the faces steepen beyond 6 m from the corridor (`SteepenStartM`) and are drawn as rock or stone wall from 52° (`CutFill.WallDeg`). |
| Side drain, shoulder, verge | 0.6-1 m drain on the hill side, 0.5-1 m shoulder | The carve keeps a level verge of one terrain sub-cell diagonal plus 0.3 m (about 3.1 m on the 2 m sub-grid) beside every corridor. |

## 3. Colours (cartoon, but faithful)

| Surface | Hex | Channel |
|---|---|---|
| Fresh soil cut (ochre-brown) | `#A57D55` | Dirt |
| Rock cut, layered grey-brown (phyllite, schist) | `#8B867D`, banded ±8 % with height | Stone |
| Stone masonry and gabion walls | `#9A958B` | Stone |
| Embankment earth, greening over | `#8E8058` blended 55 % toward the grass | Grass / Dirt |
| Level verge, drain and shoulder (gravel and earth) | `#9B8E78` | Dirt |

The carved ground keeps the natural colour where it moved less than 3 cm and takes the cut or fill colour fully from
50 cm (`CutFill.Blend`), so streets in the flat city look as before and only real cuttings change.

## 4. Sources

* [Nepal Road Standard 2070 summary slides (camber by surface)](https://www.scribd.com/document/875617775/Chapter-3)
* [Engineering aspect design for local road network in Nepal](https://roadsforwater.org/wp-content/uploads/2020/11/Engineering-Aspect-Design-for-Local-Road-Network-in-Nepal.pdf)
* [Nepal Rural Road Standards (2055)](https://mail.dpnet.org.np/uploads/files/Nepal%20Rural%20Road%20Standards%20(2055)%202022-09-06%2004-51-42.pdf)
* [Durham University: road cut slopes and retaining walls in Nepal](https://durham-repository.worktribe.com/output/2290346)
* [JICA: Narayanghat-Mugling road slope protection](https://openjicareport.jica.go.jp/pdf/11922267_01.pdf)
* [NHIDCL gabion wall drawings (India)](https://www.nhidcl.com/sites/default/files/corrigendum/gabion.pdf)
* [Civil works guidelines for micro-hydropower in Nepal (gabion walls)](https://samplecontents.library.ph/en-practical_action/Energy/Hydro%20power/civil_works_guidelines_for_micro_hydro/page132.html)
* [IRC 52 (Indian hill road manual) summary](https://infralens.in/code/IRC-52-2001)
