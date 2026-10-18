# Reference brief: hill roads cut into the slope (cut/fill package)

Owner playtest (docs/W2_PLAYTEST_1.md, item 16), screenshot at Gaurighat Hanuman Mandir on the Bagmati by Pashupati:
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
  with the hill. Junctions on these slopes sit on their own levelled platform.
* **Cut faces**: fresh cuts in the valley's lake and residual soils are ochre to red-brown earth (photo: ILO "Nepal
  earthquake 2015" road works, `Nepal_landslide_road` ref_04-09); taller and steeper cuts expose layered grey rock (the
  Bagmati gorge below Pashupati shows grey, thinly bedded rock: `Kathmandu_Pashupati_Bagmati` ref_07 "Caves @ Pashupati",
  Anuma S. Bhattarai, CC BY-SA 2.0), often faced with dry stone or concrete breast walls (road to Pharping,
  `Nepal_road_construction` ref_05, Wonderlane, CC BY 2.0; Swayambhu "old cliffside, built up with stones",
  `Swayambhunath_road` ref_01, Wonderlane, CC BY 2.0; "On the roads of Kathmandu - not a wall, just a stack",
  `Nepal_road_construction` ref_11, Carsten ten Brink, CC BY-NC-ND 2.0).
* **Fills and retaining walls**: earth embankments that grass over within a season; where the drop is steep, a stone
  masonry or **gabion** wall (wire baskets about 1 x 1 x 2 m filled with river stone, built in courses that step back
  10-20 cm each, a 10 % batter: `gabion_wall` ref_02, ref_09 and ref_11, International Rivers, CC BY-NC-SA 2.0, stepped
  gabion walls on a Himalayan road). Gabion walls are Nepal's everyday retaining wall: flexible, cheap and built from
  local river stone; they came through the 2015 Gorkha earthquake better than rigid walls.
* **Edge protection**: on the Strategic Road Network steel W-beam crash barriers on posts (73 km of barriers went onto
  eight national roads in 2016-17 under the World Bank's road safety programme); the Department of Roads' own low-cost
  barrier is a gabion wall about 1 m high (it contains a 16 t lorry and is easy to repair); on most hill roads in and
  around the valley white-painted concrete posts with a black (or yellow) band mark the edge of a drop, and minor lanes
  often have nothing but the drop itself.

## 2. Numbers used

| Element | Value | Source and note |
|---|---|---|
| Carriageway camber | 2-2.5 % bituminous, 3-4 % gravel and earth | Nepal Road Standard (NRS 2070) camber table, as summarised in course notes. The game uses **3 %** (`RoadGrade.Camber`). |
| Superelevation | up to about 6 % in the game | No Nepal-specific maximum was found; IRC hill-road practice allows up to 10 %, 7 % where snow-bound. The game caps all cross-fall at **6 %** (`RoadGrade.MaxBank`): e = v² / (225 R), v = 40 km/h (trunk, primary), 30 (secondary, tertiary), 20 (others). |
| Cross-fall on hillsides | toward the hill (inward to the side drain) | Hill road practice; on gentle ground the carriageway leans with the slope (less earthwork). |
| Junction platforms | a plane through the natural ground, its fall held to **6 %** (`RoadPads.MaxGrade`) | Every arm's end row lies in its junction's plane. |
| District hill road | 5.5 m pavement, 0.75 m shoulders, max gradient 12 % | Suryabinayak Municipality project report under NRS 2070. |
| Cut slope in soil | 1 : 1 (45°) | Common hill-road practice; Durham University field study notes cuts in Nepal are often steeper than the guidelines and fail in the monsoon. The game cuts at **1 : 1** (`TerrainCarve.CutSlope`). |
| Fill slope | 1 : 1.5 (34°) | Embankment practice. The game fills at **1 : 1.5** (`TerrainCarve.FillSlope`). |
| Walls | breast walls on cuts, gabion walls on fills, from **1.5 m** up to **5 m** (`WallMinM`, `WallMaxM`) where the open slope would not meet the ground within 6 m | Indian NHIDCL hill-road drawings (not a Nepal DoR rule; used only as a guide). Gabion base 0.4-0.65 H, 10 % batter (Nepal micro-hydro civil works guideline). Gabions step back **0.15 m** per 1 m course. |
| Side drain and shoulder (verge) | about **3.8 m** on the High and Mid tiers, 1.5 m on Low | Real drains and shoulders are 1-2 m; the level verge is as narrow as the terrain's 4 m sub-quads allow without the ground rising through the road (a sub-quad straddling the edge rises at most k (L - v)² / 4L over it, held to 18 cm under the 25 cm road lift). |
| Parapet posts | white 0.18 m concrete posts, 0.78 m above the road, black band, every **3 m** | Along main roads where the ground 4 m out lies 2 m or more below the edge, and along every fill wall. Main roads add a W-beam at 0.42-0.72 m. |

## 3. Colours (cartoon, but faithful)

| Surface | Hex | Channel |
|---|---|---|
| Fresh soil cut (ochre-brown) | `#A57D55` | Dirt |
| Rock cut, layered grey-brown (phyllite, schist) | `#8B867D`, banded ±8 % with height | Stone |
| Stone masonry and gabion walls | `#9A958B` / gabion stone `#8F8B82` | Stone |
| Embankment earth, greening over | `#8E8058` blended 55 % toward the grass | Grass / Dirt |
| Level verge, drain and shoulder (gravel and earth) | `#9B8E78` | Dirt |
| Parapet post | `#EDEBE3` with a `#2B2B2B` band | Paint |
| W-beam | `#B2B6B8` | Metal |

The carved ground keeps the natural colour where it moved less than 3 cm and takes the cut or fill colour fully from
50 cm (`CutFill.Blend`), so streets in the flat city look as before and only real cuttings change.

## 4. How the game builds it (one carved surface)

1. **Level roads** (`RoadGrade`): the profile follows the smoothed natural drape along the road; across it the carriageway
   has only camber or superelevation (at most 6 %). Every pin is absolute: tile seams, knees, junction platforms, deck ends.
2. **The carved surface** (`TerrainCarve`), per tile and terrain step, built from the bench (road surface less its lift):
   * under every road's drawn extent (carriageway with footpaths or shoulders, junction caps, roundabouts) the surface
     everyone stands on is exactly the bench (`ExtentAt`), for gentle streets and hill roads alike, whatever the grid draws
     hidden under the road;
   * beside a hill road (natural ground 0.35-0.7 m off the bench and a hillside falling 8-15 % across it) the ground is cut
     at 1 : 1 after the verge and filled at 1 : 1.5, steepening beyond 6 m, with breast and gabion walls;
   * the coarse grid is carved beside hill roads and lowered (up to 3 m) under every road so no ground rises through it;
     cells where that is not enough are refined into 4 m sub-quads (High and Mid; the Low tier's 32 m cells are never
     refined and keep the coarse carve).
3. **Everyone reads it**: the terrain mesh draws it, `TileHeightSampler` answers it (so the ground query, the lane graph,
   the solids, buildings, trees and props stand on it), junction caps are drawn on it, parked vehicles pitch and roll to
   the road under their wheels and never hang over an edge, street animals lie only where the drawn road surface carries
   their whole body, and the parapets and breast walls are solids.

## 5. Sources

* [Nepal Road Standard 2070 summary slides (camber by surface)](https://www.scribd.com/document/875617775/Chapter-3)
* [Engineering aspect design for local road network in Nepal](https://roadsforwater.org/wp-content/uploads/2020/11/Engineering-Aspect-Design-for-Local-Road-Network-in-Nepal.pdf)
* [Nepal Rural Road Standards (2055)](https://mail.dpnet.org.np/uploads/files/Nepal%20Rural%20Road%20Standards%20(2055)%202022-09-06%2004-51-42.pdf)
* [Durham University: road cut slopes and retaining walls in Nepal](https://durham-repository.worktribe.com/output/2290346)
* [JICA: Narayanghat-Mugling road slope protection](https://openjicareport.jica.go.jp/pdf/11922267_01.pdf)
* [NHIDCL gabion wall drawings (India)](https://www.nhidcl.com/sites/default/files/corrigendum/gabion.pdf)
* [Civil works guidelines for micro-hydropower in Nepal (gabion walls)](https://samplecontents.library.ph/en-practical_action/Energy/Hydro%20power/civil_works_guidelines_for_micro_hydro/page132.html)
* [IRC 52 (Indian hill road manual) summary](https://infralens.in/code/IRC-52-2001)
* [World Bank: enhancing the safety of Nepal's mountain roads (crash barriers, 2016-17)](https://www.worldbank.org/en/results/2020/11/10/enhancing-the-safety-of-nepals-mountain-roads)
* [SSATP case study: safety in Nepal (the DoR gabion barrier)](https://ssatp.org/sites/default/files/publications/HTML/rural_transport/knowledge_base/English/Module%201%5C1_4b%20Case%20Study%20-%20Safety%20in%20Nepal.pdf)
* [TRID: an affordable safety barrier for Nepal](https://trid.trb.org/View/504315)
* [Gabions as low-cost roadside barriers (Queen's University Belfast)](https://pureadmin.qub.ac.uk/ws/files/12579953/Paper_Gabions_evaluation_of_potential_as_low_cost_roadside_barriers_OA.pdf)
