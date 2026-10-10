# Bridges, flyovers, underpasses and foot overbridges of the Kathmandu Valley

> W2 detail pass, package **bridges** (decisions 3 and 4 of `docs/W2_DETAIL_CONTRACT.md`). Owner feedback: *"If there is a road passing from the river, make sure to design it as a Proper bridge with Railings. If there is a road going from the underneath the bridge, add more clearance so the the user can pass through. Find wherever there is an overpass in the city and design that."*
>
> Evidence tags as in the other W2 research notes: **[O]** counted from OpenStreetMap (`pipeline/data/raw/osm/nepal.osm.pbf`, the extract the pipeline builds from), **[S]** a cited source, **[V]** seen in photographs or street imagery during research, **[E]** an engineering estimate or design choice.

## 1. Method

* **OSM inventory [O].** pyosmium pass over the Nepal PBF, valley box 85.17–85.56 E, 27.56–27.83 N: 50,152 highway ways, 611 waterway lines (river, stream, canal, drain, ditch) and 8 `man_made=bridge` outlines. Every highway with `bridge≠no` was tested for segment crossings with the waterways and with highways that are not bridges or sit on a lower `layer`; ways with `tunnel=*` or a negative layer were listed as underpass candidates. Scripts: the session scratchpad (`osm_bridges.py`, `osm_analyse.py`); they are a one-off and are not part of the pipeline.
* **News and reports [S]** for the flyovers, the underpass and the overhead bridges (URLs in §8).
* **Appearance [V]/[E].** Railing, pier and lamp details come from photographs of the valley's bridges; no Department of Roads standard drawing for railings was found online, so the dimensions in §5 are design values within normal Indian Roads Congress and Nepal Road Standard practice, marked [E].

## 2. Counts [O]

| Kind | Ways in the valley box | Notes |
|---|---|---|
| Road bridges over water | **568** | median 16 m, 90th percentile 55 m, longest 253 m (Kolphu Khola bridge, Trishuli Highway side) |
| Footbridges over water | **151** | median 22 m; 15 `bridge:structure=suspension`, 3 `simple-suspension`, 3 `arch` |
| Road bridges over roads (flyover candidates) | 35 | almost all are short culvert-like crossings of service roads or tagging artefacts; the real flyover is Gwarko (§3.2) |
| Footways/paths over roads (foot overbridges) | **43** | Kanti Path, Durbar Marg, Ring Road, Araniko Highway, Pulchowk Road (§3.4) |
| Road bridges crossing neither (bridge over a ditch not mapped, or a viaduct) | 16 | e.g. the 278 m trunk viaduct at the Nagdhunga tunnel approach |
| Tunnels and negative-layer highways | 102 (49 motor) | Kalanki underpass, Nagdhunga tunnel, short covered passages (§3.3) |
| `man_made=bridge` outlines | 8 | Balkumari, Narayantar, Balkhu Khola, Balaju bridges; one `arch`, one `beam` |

Almost every bridge carries `layer=1` (a few `layer=2` over a lower bridge or a riverside road). Only 24 of 719 river bridges carry `bridge:structure`; the rest must be styled by rule (§5).

## 3. Inventory

### 3.1 River bridges (major roads) [O]

Dual roads cross on **twin decks** (each carriageway its own `oneway` bridge way, 7–12 m apart), and the Kalanki–Koteshwor Ring Road crosses on **four parallel decks** (two expressway carriageways and two service roads, all `oneway`).

| River | Bridge (OSM name) | Ways and length | Where |
|---|---|---|---|
| **Bagmati** | **Bagmati Bridge, Thapathali–Kupondole** | 2 × primary, oneway, 157 m (3 lanes, 9 m) and 163 m (2 lanes, 6 m) | 27.6900, 85.3173. The crossing replaced the 1810 timber bridge (Nepal's first proper bridge) and its successors [S] |
| Bagmati | Kalo Pul (Teku–Sanepa) | secondary, 139 m | 27.6932, 85.3043; the original was a suspension bridge of VS 1996 (1940) [S] |
| Bagmati | Ring Road, Balkhu | 4 × 128–129 m (2 trunk + 2 primary, oneway) | 27.6846, 85.2988 |
| Bagmati | Madan Bhandari Path, Tinkune | 2 × trunk 109 m, layer 2 (over the riverside road) | 27.6864, 85.3438 |
| Bagmati | Bagmati Bridge, Shankhamul | secondary 101 m | 27.6848, 85.3271 |
| Bagmati | Sinamangal Bridge | 2 × secondary 70–81 m | 27.6991, 85.3464 |
| Bagmati | Tinkune network arch bridge | 85 m, 16 m wide (12 m carriageway, 2 m footpaths, 1.2 m arch beams), arch 15.75 m above the deck; **unfinished** (works restarted 2025, extension requested) [S] | Tinkune |
| **Bishnumati** | Bishnumati Bridge (Kalimati–Teku) | 2 × secondary oneway 83 / 88 m | 27.7016, 85.3023 |
| Bishnumati | Bijeshwori Bridge | secondary 82 m | 27.7136, 85.3016 |
| Bishnumati | Ganeshman Singh Path | trunk 72 m, 4 lanes | 27.6980, 85.3026 |
| Bishnumati | Ring Road, Balaju | trunk 66 m, 4 lanes, 16 m | 27.7352, 85.3076 |
| Bishnumati | Dallu bridge and Dallu Arch Bridge | secondary 64 m; secondary 40 m (`bridge:structure=arch`) | 27.709, 85.303 |
| **Dhobi Khola** | Bijulibazar bridges | 2 × trunk 56 m + 2 × tertiary 56–61 m, layer 2 (over the riverside road); the network-arch bridge here, Nepal's first, about 51.5 m, opened about 2020 [S] | 27.6904, 85.3283 |
| Dhobi Khola | Ratopul / Setopul | secondary 55 m (layer 2) / 44 m | 27.708, 85.337 |
| Dhobi Khola | Kalopul (Gaushala) | secondary 90 m | 27.7119, 85.3371 |
| Dhobi Khola | Bhatkeko Pul | tertiary 55 m | 27.7181, 85.3400 |
| Dhobi Khola | Ring Road, Chabahil | trunk 55 m, 4 lanes, 14 m, layer 2 | 27.7223, 85.3454 |
| **Manohara** | Ring Road, Koteshwor–Balkumari | 4 × 120–122 m | 27.6726, 85.3415 |
| Manohara | Araniko Highway, Koteshwor–Thimi | 2 × trunk 90 m, 4 lanes | 27.6751, 85.3548 |
| Manohara | Mulpani / Manohara Pul | primary 78 m; secondary 47 m | 27.703, 85.394; 27.678, 85.336 |
| **Tukucha** | Prithvi Path (Bhadrakali) | 2 × primary 23 m | 27.6988, 85.3199 |
| Tukucha | Exhibition Road | secondary 25 m | 27.7018, 85.3211 |
| **Hanumante** | Araniko Highway, Sallaghari | 2 × trunk 54 m | 27.6741, 85.3996 |
| Hanumante | Ram Mandir Bridge, Bhaktapur | secondary 27 m | 27.6681, 85.4272 |
| **Balkhu Khola** | Ring Road, Kalanki–Balkhu | 4 × 70 m and 4 × 53 m | 27.690, 85.284; 27.685, 85.298 |
| Kodku Khola | Satdobato–Godawari Road | primary 54 m | 27.6479, 85.3357 |
| Samakhushi, Icchumati, Manamati | Ring Road and city streets | 8–28 m culvert bridges | north and west of the core |

### 3.2 Flyovers and overpasses

| Site | Status (October 2026) | Evidence |
|---|---|---|
| **Gwarko** (Ring Road over the Mangalbazar–Gwarko road, Lalitpur) | **Built, opened June 2025**: the valley's first flyover. Four lanes (two each way), one 35 m span, ramps 320 m toward Satdobato and 185 m toward Balkumari (540 m in all), 6 m vertical clearance [S]. Another report gives 455 m with 180 m and 110 m ramps [S] | OSM: trunk `bridge=yes` ways 183827055 (658 m), 1426160993 (1,090 m), 406752033 (346 m), layer 1 [O]. OSM tags the ramps as bridge too, so the data package must split deck from fill (§6) |
| **Koteshwor** | Planned: a 440 m flyover and a 640 m underpass, JICA loan signed 3 Dec 2025; not built [S] | none in OSM |
| Satdobato, Ekantakuna | Studied with Gwarko; **only pedestrian overhead bridges built** [S] | foot overbridges in OSM [O] |
| Kalanki | No flyover: an **underpass** (§3.3) [S] | — |
| Machhapokhari (Balaju) | Planned in Ring Road phase 2, not built [S] (vehicles_traffic.md) | — |
| Narayan Gopal Chowk (Maharajgunj), Suryabinayak (Bhaktapur), Tinkune | No flyover found in OSM or the news [O][S] | Tinkune has the layer-2 Madan Bhandari Path river bridge only |
| Nagdhunga | Tunnel approach viaduct (trunk, 278 m) and short bridges over the Tribhuvan Rajpath [O] | 27.689, 85.236 |

### 3.3 Underpasses [O][S]

* **Kalanki underpass**: the Ring Road through lanes dip under the Kalanki junction, about 800 m between Khasi Bazaar and Bafal; four-lane road tunnel with service lanes on the upper deck; trial opening 17 Aug 2018 [S]. In OSM: trunk `tunnel=yes`, layer −1 ways 225457469 (56 m), 653848641 (42 m), 1090820447 (80 m), 1090820451 (176 m).
* **Balkumari**: a secondary road under the Ring Road (`tunnel=yes`, 48 m).
* **Bijulibazar**: the tertiary riverside road under the Dhobi Khola bridges (`tunnel=yes`, 60 m), the case the owner hit ("it gets stuck").
* **Nagdhunga Tunnel** (2.68 km, trunk) and a few covered passages (Seto Dhoka, hospital and school drives) stay out of W2 (tunnels are not drawn).
* Pedestrian subways: the valley had 22 overhead bridges and subways in 2019 [S]; none is mapped as a subway in OSM.

### 3.4 Foot overbridges [O][S]

43 footway/path/pedestrian bridges cross roads: **Kanti Path** (Jamal, Rani Pokhari, Bir Hospital, Bhotahity, Bhadrakali: 7 ways), **Durbar Marg** and Ratna Park (5), New Road gate, Baghbazar, **Pulchowk** (layer 2), Lainchaur (Lekhnath Sadak), Putalisadak (Ram Shah Path), Old Baneshwor, **Ring Road** at Satdobato, Ekantakuna, Gwarko–Balkumari, Koteshwor and Chabahil, and about ten on the **Araniko Highway** between Koteshwor and Sallaghari. The Ring Road improvement project planned ten overhead bridges [S]; Chabahil got a portable steel bridge 23 m long and 2 m wide [S]; KMC plans lifts on the New Baneshwor bridge [S].

## 4. How they look

* **Older RCC bridges** (most river crossings, 1960s–2000s) [V]: RCC T-beam or slab decks on wall piers with rounded noses (cutwaters) in the river and stone-masonry abutments with splayed wing walls. Railings are **RCC post-and-rail balustrades**: a low plinth, square posts about every 2–2.5 m with small caps, two or three horizontal rails; whitewashed, the posts and kerbs often painted in **black and yellow** bands. Raised footpaths on both sides behind a kerb.
* **Newer city bridges** [V]: RCC posts with two or three **steel pipe rails**, painted blue, green, red or left galvanised; footpaths with pavers.
* **Ring Road and Araniko Highway** (Chinese-built 2013–2019) [V][S]: twin or four parallel decks, **concrete crash parapets** with a galvanised steel handrail, single-arm LED lamp posts about every 30 m alternating sides, yellow-and-black painted kerbs at the approaches.
* **Arch bridges** [S][V]: Bijulibazar (network arch, Dhobi Khola), Dallu Arch Bridge (Bishnumati) and the unfinished Tinkune network arch: steel arches over the deck with hangers (W2 draws them as girder bridges; the arch is an open issue, §7).
* **Flyover** (Gwarko) [S][V]: a short single span on solid ramps between retaining walls, crash parapets, lamp posts; 6 m clearance below [S].
* **Foot overbridges** [V]: painted steel girder or truss decks with mesh or bar side panels, steel stairs at both ends (often with a landing), many with an arched sheet roof; some carry advertising boards [S].
* **Footbridges over rivers** [O][V]: steel suspension bridges (Sundarighat 83 m, Kankeswari 64 m, Koteshwor Jhulungepul 69 m, the Teku footbridge 125 m) and short concrete or steel beam footbridges; brick-and-stone footbridges at the heritage ghats (Pashupati, Teku, Sankhamul).

## 5. Game design (what the package builds)

| Element | Rule |
|---|---|
| Spans | Every road whose structure record is `Bridge` or `Flyover` with deck heights (or, on packs without the structure chunk, every `RoadFlags.Bridge` piece, derived as below) |
| Deck height | The record's `DeckY` per road point, interpolated linearly (the roads package draws the surface at the same height). Fallback derivation mirrors the W1 ribbon: straight between the lifted ends, never below the lifted terrain; a flyover's interior points rise toward the clearance within a 10% grade |
| Width | Carriageway from `RoadLayout` (the widened corridor the ribbon draws), walkways 1.5 m on trunk to secondary, 1.2 m on tertiary, a 0.6 m safety kerb elsewhere, widened so the clear width between the railings is at least `RoadClearance.MinCorridorM` (4.8 m; foot overbridges 2.6 m) |
| Cross-section | Deck slab with fascia and drip edge over a rounded T-girder (road bridges 1.3 m deep, flyover box 1.5 m), steel or concrete box for footbridges (0.55 m); kerbs 0.22 m high, painted black and yellow in 1 m stripes |
| Railings, both sides | Style by rule (`BridgeStyle.RailingFor`): crash parapet + handrail on flyovers and oneway trunk/motorway bridges; RCC post-and-rail or pipe rail (hash) on other road bridges; steel truss with mesh on foot overbridges; Newar brick parapet with carved wooden rail, turned balusters and stone finials on heritage footbridges; end pillars with finials at real ends |
| Twin decks | Where two parallel decks would meet, each stops at the middle and a painted median barrier replaces the railing on that side, so no railing stands in a lane |
| Supports | Abutment (stone in rivers) with a bearing shelf and splayed wing walls at each real end; piers where an open stretch is longer than 15 m, about every 21 m in rivers (wall piers with cutwaters) and 25 m on flyovers (hammerhead or two/three columns with a cap beam), kept out of the corridors of the roads below; flyover ramps lower than 4.2 m are fill between panelled retaining walls, with a bent wall where the fill meets the open span |
| Clearance | Over a road below, the structure thins (down to 0.45 m) so the soffit stays at least `MinUnderpassClearanceM` = 5.5 m above it; `BridgeDeckIndex.TryCeiling` reports the real soffit |
| Lamps | Single-arm LED poles (8 m on flyovers and major roads, 6.5 m elsewhere) every 15 m alternating sides, both sides on decks wider than 14 m |
| Foot overbridges | Steel truss sides, stairs at both real ends (0.165 m rise, 0.30 m tread, a 1.5 m landing every 14 steps, side stringers, handrails, landing columns), an arched roof on half of them (by hash) |
| Underpasses | Records of kind `Underpass` with lowered deck heights get trench walls with a parapet where the road runs more than 0.5 m below the ground |
| Seams | A span cut by a tile border ends on the border with the cross-section the neighbour starts with; piers on cut spans, posts, lamps and kerb stripes sit at world-anchored stations, so each is drawn by exactly one tile |
| LOD | 0: rounded everything, every post, balusters, kerb stripes (≤ 9,000 triangles per 100 m of deck, plus about 1,000 per stair flight); 1: one bevel step, half the posts, one rail less, plain kerbs, simple lamps (≤ 4,000); 2: deck box, solid parapets, plain piers, ramps for stairs, no lamps (≤ 1,200) |
| Materials | UV0 = (`MaterialChannel`, AO): Concrete, Stone (abutments in rivers), Paint (kerbs, painted rails and posts), Metal (steel, lamps), Brick and WoodCarved (Newar), Glass (lamp heads); AO baked dark on soffits, girders and pier feet |

## 6. Asks for the data package [E]

1. Split Gwarko-style ways: OSM tags whole ramps as `bridge=yes`; the deck heights should follow a vertical curve (≤ 5% ramps, a 6 m clearance at the crossing) and the record should keep `Flyover`.
2. Densify elevated ways to about 10 m so `DeckY` can carry a crest or a ramp (the deck is linear between road points).
3. Set `DeckY` at least `lower road + 5.5 m + BridgeStyle` depth (1.3 / 1.5 / 0.55 m) over every road below; the mesher thins the structure when it is not, but only down to 0.45 m.
4. Give the river crossings a deck above the bank (DEM channels are shallow at 30 m resolution); carve the DEM along underpass trenches (Kalanki) so the lowered road is not under the terrain.

## 7. Open issues

* Arch, suspension and truss superstructures (Bijulibazar, Dallu, Tinkune arches; Sundarighat, Kankeswari, Teku suspension footbridges) are drawn as girder or steel-box bridges.
* Underpass covers (the cross road over a trench) are not in the deck index yet; the roads package draws the cross road and the terrain stays solid until the DEM is carved.
* Advertising boards on foot overbridges and the planned lifts at New Baneshwor are not modelled.

## 8. Sources

* Gwarko flyover opening (World Highways, June 2025): https://www.worldhighways.com/news/kathmandu-flyover-opening ; construction details (540 m, 35 m span, 6 m clearance): https://www.worldhighways.com/wh10/news/nepal-flyover-construction-underway ; https://www.en.meroauto.com/gwarko-overpass-ready-nearing-completion/ ; opening coverage: https://ekantipur.com/news/2025/06/20/en/gwarko-overpass-prime-minister-cuts-ribbon-to-traffic-chief-own-name-on-inscription-12-56.html
* Satdobato, Ekantakuna and Gwarko flyover study: https://english.onlinekhabar.com/three-flyovers-in-kathmandu-in-the-offing.html
* Koteshwor flyover and underpass (JICA): https://www.en.meroauto.com/koteshwor-to-get-flyover-and-underpass-for-uninterrupted-traffic-flow/
* Kalanki underpass: https://kathmandupost.com/valley/2018/08/18/kalanki-underpass-opens-for-public ; https://kathmandupost.com/valley/2018/08/17/nepals-first-underpass-opens-for-trial ; https://kathmandupost.com/valley/2018/08/23/traffic-jams-resume-as-kalanki-subway-closes
* Bijulibazar (Dhobi Khola) network arch bridge: https://english.onlinekhabar.com/nepals-first-network-arch-bridge-to-come-into-operation-before-dashain.html ; https://kathmandupost.com/valley/2020/02/04/bijuli-bazaar-arch-bridge-is-set-to-open-in-a-month-but-there-are-concerns-it-will-ease-traffic
* Tinkune arch bridge: https://www.en.meroauto.com/?p=10781 ; https://thehimalayantimes.com/business/pappu-construction-told-to-dismantle-rebuild-tinkune-arch-bridge/
* Thapathali crossing history: https://askmeaboutnepal.com/the-history-of-nepal-s-bridges ; Kalo Pul, Teku: https://danam.cats.uni-heidelberg.de/2a6267a7-99df-4760-bf1a-b283445df4da/k%C4%81lo-pula-black-bride-at-teku-kathmandu
* New Bagmati Bridge (Thapathali) JICA study: https://openjicareport.jica.go.jp/pdf/11129962_01.pdf
* Overhead bridges: https://thehimalayantimes.com/business/kalanki-koteshwor-section-ring-road-completed-july-2018/ ; https://thehimalayantimes.com/kathmandu/construction-of-several-overhead-bridges-delayed/ ; https://thehimalayantimes.com/kathmandu/21-locations-valley-need-overhead-bridges ; https://www.sharesansar.com/newsdetail/construction-of-portable-overhead-bridge-started-at-chabahil-chowk-expected-to-be-completed-before-july-11-2020 ; https://thehimalayantimes.com/kathmandu/kmc-to-construct-eight-new-over-head-bridges ; https://aawaajnews.com/social-development-news/kmc-to-install-elevators-on-overhead-bridge-for-accessibility
* Ring Road phase 2 (Machhapokhari flyover): see `docs/research/w2/vehicles_traffic.md` §2.
