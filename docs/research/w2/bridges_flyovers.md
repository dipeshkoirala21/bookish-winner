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
* **Arch bridges** [S][V]: Bijulibazar (network arch, Dhobi Khola), Dallu Arch Bridge (Bishnumati) and the unfinished Tinkune network arch: steel arches over the deck with hangers (W2 draws the arch-tagged ways with two ribs over the deck and hangers; Bijulibazar and Tinkune are not tagged and stay girder bridges until the data package carries `bridge:structure`).
* **Flyover** (Gwarko) [S][V]: a short single span on solid ramps between retaining walls, crash parapets, lamp posts; 6 m clearance below [S].
* **Foot overbridges** [V]: painted steel girder or truss decks with mesh or bar side panels, steel stairs at both ends (often with a landing), many with an arched sheet roof; some carry advertising boards [S].
* **Footbridges over rivers** [O][V]: steel suspension bridges (Sundarighat 83 m, Kankeswari 64 m, Koteshwor Jhulungepul 69 m, the Teku footbridge 125 m) and short concrete or steel beam footbridges; brick-and-stone footbridges at the heritage ghats (Pashupati, Teku, Sankhamul).

## 5. Game design (what the package builds)

| Element | Rule |
|---|---|
| Spans | Every road whose structure record is `Bridge` or `Flyover` with deck heights (or, on packs without the structure chunk, every `RoadFlags.Bridge` piece, derived as below) |
| Deck height | The record's `DeckY` per road point, interpolated linearly (the roads package draws the surface at the same height). Fallback derivation (`BridgeStructures.Derive`, packs without the structure chunk) mirrors the W1 ribbon: straight between the lifted ends, never below the lifted terrain at a road point. Every road crossed in plan without a shared node is a crossing: a vehicle bridge rises over it to `lower + 5.5 m + 1.2 m` within the class grade where its pinned ends allow (a bridge over no water that clears a road becomes a `Flyover`); foot bridges over no water that cross a vehicle road are foot overbridges, every foot deck joined to one is part of the same structure, and the structure gets one flat deck at `max(lower) + 5.5 m + 0.6 m`; the `highway=steps` chains joined to it get stair records from the deck down to the ground. `BridgeStructures.For(t)` publishes the derived records for the roads and collide packages (§7) |
| Width | Carriageway from `RoadLayout` (the widened corridor the ribbon draws), walkways 1.5 m on trunk to secondary, 1.2 m on tertiary, a 0.6 m safety kerb elsewhere, widened so the clear width between the railings is at least `RoadClearance.MinCorridorM` (4.8 m; foot overbridges 2.6 m) |
| Cross-section | Deck slab with fascia and drip edge over a rounded T-girder (road bridges 1.3 m deep, flyover box 1.5 m), steel or concrete box for footbridges (0.55 m); kerbs 0.22 m high, painted black and yellow in 1.5 m stripes for 15 m from a real end (the approach warning), whitewashed beyond |
| Railings, both sides | Style by rule (`BridgeStyle.RailingFor`): crash parapet + handrail on flyovers and oneway trunk/motorway bridges; RCC post-and-rail or pipe rail (hash) on other road bridges and on paved city footbridges; steel truss with mesh on foot overbridges and suspension footbridges (galvanised chain-link there); the heritage parapet (`RailingStyle.Newar`: a solid wall 0.78 m high with a flat stone coping, pilasters every 4.8 m and end pillars with stone finials; grey stone at the Pashupati, Teku and Sankhamul ghats, Newar brick in the Durbar Square cores) only on footbridges with the RATR heritage-pedestrian flag or inside the curated heritage zones (`BridgeStyle.InHeritageZone`), never from `surface` (paving stones and interlock are modern city pavers); a sidewalk deck sharing a side with a vehicle deck takes that deck's railing and paint; end pillars at real ends where no road beside the bridge head reaches them (else a plain post, else none: `BridgeSpan.EndPost`) |
| Twin decks | Where two parallel decks would meet, each stops at the middle and a painted median barrier replaces the railing on that side, so no railing stands in a lane |
| Supports | Abutment (stone in rivers) with a bearing shelf and splayed wing walls at each real end, moved back or dropped (wing walls cut short) where another road's corridor is; piers where an open stretch is longer than 15 m, about every 21 m in rivers (wall piers with cutwaters), 18 m under footbridges and 25 m on flyovers (hammerhead or two/three columns with a cap beam), their whole footprint (the deck's width, 1.6 m along) kept out of every other road's clearance envelope; flyover ramps lower than 4.2 m are fill between panelled retaining walls, with a bent wall where the fill meets the open span. Suspension footbridges (`bridge:structure=suspension`, curated way list until the record carries the tag) have no piers: a painted steel lattice tower portal at each end of the river run (two tapering lattice legs, top beam, tie beam and X bracing; 9 to 11 m), main cables, hangers and backstays to anchor blocks where the roads around allow; arch road bridges get two ribs over the deck with hangers; arch footbridges (the Pashupati crossings) a deck arch under the walkway |
| Clearance | Every road passing under a deck in plan (no shared node) is either kept clear or left open, never ignored. Over a road kept clear the structure thins (down to 0.45 m; foot decks, a steel plate between the side trusses, 0.25 m) so the soffit stays at least `MinUnderpassClearanceM` = 5.5 m above the drawn road (foot ways below: 2.5 m headroom); `BridgeDeckIndex.TryCeiling` reports the real soffit. A record whose deck is at most 0.5 m short of that keeps its thinnest structure (soffit at least 5.0 m, still over the 4.5 m overhead envelope) and is reported in `BridgeLayout.Issues`; a deck further short (or a derived deck at grade) leaves the road open: no slab, kerb, railing, wall or support over its corridor and no ceiling. Railings and walkways open where a road beside the deck reaches the railing line at grade |
| Lamps | Single-arm LED poles (8 m on flyovers and major roads, 6.5 m elsewhere) every 15 m alternating sides, both sides on decks wider than 14 m, each on a concrete pilaster in the railing line (a plain block at LOD1, so no pole floats), dropped where the pole would stand in a road beside the deck |
| Foot overbridges | Steel truss sides, an arched roof on half of them (by structure hash); one steel paint per connected structure. Stairs follow the mapped `highway=steps` ways (from the deck edge where they leave, through a railing gap and a landing end railing, down their own line; a chain mapped too short runs on at its grade or turns back as a dog-leg; a foot that would land on a carriageway is pulled back onto the footpath); a generated flight (0.165 m rise, 0.30 m tread, a 1.5 m landing every 14 steps) only at a real end with nothing mapped, along the pavement it meets or sideways, whichever keeps out of the roads. Ends joined to another deck are junctions: no stair, pillar, cap or column there, and the railing of the deck it leaves opens. A deck end inside the widened corridor of a road it crosses is extended out of it (up to 15 m). Steep stretches inside a deck (records stepping between two crossing levels) become flights across its width. The drawn flight and the walkable query share one profile (`BridgeStair`: treads, landings and the bottom run add up to the flight length; the query is a ramp through the nosings, flat on landings, never under a tread and at most one riser above it) |
| Underpasses | Records of kind `Underpass` with lowered deck heights get trench walls with a parapet where the road runs more than 0.5 m below the ground |
| Seams | A span cut by a tile border ends on the border with the cross-section the neighbour starts with; piers on cut spans, posts, lamps and kerb stripes sit at world-anchored stations, so each is drawn by exactly one tile |
| LOD | 0: rounded everything, every post, kerb stripes, lattice towers; 1: plain corners, a quarter of the posts, one rail less, plain kerbs, simple lamps; 2: deck box (top 6 cm lower), solid parapets, plain piers, slabs for stairs, no lamps. The sweeps ring the deck at its key stations, thinned per LOD by a Douglas-Peucker pass (`BridgePath.Near/Mid/Far`: millimetres, a few centimetres, a decimetre or two of deck, width or depth). `BridgeOptions.LodFor(tier, distance)`: LOD0 within 30 m (Mid) / 90 m (High), never on Low; LOD1 within 60 / 250 / 400 m; LOD2 beyond |
| Budget | Bridges take at most half of the W2_DESIGN §10.4 roads + decals slice: 4 k / 10 k / 15 k visible triangles on Low / Mid / High (an open issue for the lead, §7). `BudgetsHoldPerTier` counts them as the slice means them: from a camera on every span of the sample region, the worst 90° view of every span within the tier's ring at its LOD. On the data package's RSTR sample (October 2026): 3.5 k / 8.9 k / 13.3 k at the Kanti Path overbridges and Teku; on the derived pack 2.0 k / 4.7 k / 7.4 k; per tile LOD2 at most 4 k |
| Materials | UV0 = (`MaterialChannel`, AO): Concrete, Stone (abutments in rivers), Paint (kerbs, painted rails and posts), Metal (steel, lamps), Brick and WoodCarved (Newar), Glass (lamp heads); AO baked dark on soffits, girders and pier feet |

## 6. Asks for the data package [E]

1. Split Gwarko-style ways: OSM tags whole ramps as `bridge=yes`; the deck heights should follow a vertical curve (≤ 5% ramps, a 6 m clearance at the crossing) and the record should keep `Flyover`.
2. Densify elevated ways to about 10 m so `DeckY` can carry a crest or a ramp (the deck is linear between road points).
3. Set `DeckY` at least `lower road + 5.5 m + depth` over every road below **measured from the drawn road**: the RSTR (October 2026) measures from the terrain, but the road ribbon floats `RoadMesher.LiftOf` (0.25 m + the class lift, about 0.3 m) above it, so foot overbridge decks come out 5.76–5.88 m over the drawn road instead of 6.1 m. The layout thins foot decks to 0.25 m to keep 5.5 m, and reports the three decks that still fall short (`BridgeLayout.Issues`): 301644010 and 351926854 on Kanti Path (0.23 m and 0.07 m short: kept at their thinnest), and the sidewalk bridge 1136873418 only 4.4 m over a residential road at Balkhu (left open); adding the lift to the requirement removes them.
4. Give the river crossings a deck above the bank (DEM channels are shallow at 30 m resolution); carve the DEM along underpass trenches (Kalanki) so the lowered road is not under the terrain.
5. Carry `bridge:structure` (suspension, simple-suspension, arch) in the record; `BridgeStyle.FormOf` uses a curated list of the 24 suspension and 3 arch ways until then.
6. A suspension footbridge's deck should be one smooth sag between its towers: the foot-stair rule (50 % grade) currently steps the Teku footbridge (225466624) up and down over the bank roads, which the layout draws as short flights inside the deck.

## 7. Open issues

* **Integration (lead):** on packs without the structure chunk the roads package and `TileGroundQuery` read the tile's (absent) records and draw the W1 ribbon, while the bridges stand on the derived records: raised foot overbridges (5.75–10.8 m above the W1 line on the sample), their stairs and any raised vehicle deck. Read `BridgeStructures.For(t)` instead of `t.RoadStructureOf(i)` there (one call, cached with the layout). Packs with RSTR (the data package's rebuilt sample) are unaffected.
* **Integration (after the data merge):** the RSTR `DeckRole` (Deck / Ramp per point) is not in this package's stub record; once merged, ramp points should become fill (no piers, retaining walls) like the derived flyover ramps.
* **Budget share (lead):** the bridges take half of the W2_DESIGN §10.4 roads + decals slice (4 k / 10 k / 15 k); the roads package's own per-tile caps (90 k) do not follow that slice either, so the two need one reconciled line in §10.4.
* Derived fallback only: a derived deck is linear between road points, so over a convex bank between two points it can dip under the terrain (Teku on the derived pack); RSTR records do not have this.
* Underpass covers (the cross road over a trench) are not in the deck index yet; the roads package draws the cross road and the terrain stays solid until the DEM is carved.
* Advertising boards on foot overbridges and the planned lifts at New Baneshwor are not modelled; the Thapathali railing's openwork key pattern is drawn as the plain post-and-rail balustrade.

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

## 9. Reference brief (photographs, October 2026)

Photos are references only (Openverse, open licences; nothing copied into the repo), kept under `/home/user/wt/refs/bridges/`. Renders beside them: `/home/user/wt/previews/bridges/fix2/` (`suspension_vs_photos.png`, `pashupati_vs_photo.png`).

| Object | What the photos show | How the package draws it |
|---|---|---|
| Suspension footbridge in the city (Francisco Anzola, "Pedestrian bridge in downtown Kathmandu", CC BY 2.0; The Advocacy Project, "pedestrian bridge in Kathmandu", CC BY-NC-SA 2.0) | A portal of two tall steel lattice legs (about 12 m, tapering, red-brown or dark paint), a top beam, a tie beam and big X bracing; main cables over the top, vertical hangers; a low concrete parapet at the entry; chain-link sides | Lattice legs 0.84 → 0.40 m wide, top and tie beams, X bracing, 9–11 m over the deck (LOD0); cables, hangers every 1.5 m, backstays to concrete anchor blocks |
| Trail suspension bridges (jamehand, "Suspension Bridge - Nepal", CC BY-NC-SA 2.0; Doha Sam, "Suspension -- Nepal", CC BY-NC-SA 2.0) | Galvanised chain-link fence from the deck to the handrail cable on both sides (light grey, see-through), a galvanised grating deck, cables near handrail height at mid-span, concrete anchor blocks at the ends | Galvanised mesh panels `#A3ABB0` between posts every 2 m, steel handrail and mid rail, the main cable at handrail height along the middle of the run |
| Thapathali (Bagmati) bridge (bmaharjan, "Thapathali Bridge Evening Sky…", "Lined Up Evening Sky Thapathali Bridge…", CC BY-SA 2.0) | Twin RCC decks with a gap between them, deep fascia girders with rust streaks; a concrete balustrade of posts, top rail and an openwork panel; single-arm lamp posts at the railing | Twin decks, RCC post-and-rail or pipe rails (by hash), rounded fascia and girder, lamps every 15 m alternating sides |
| Pashupati ghat crossing (girolame, "« Ghats » de crémation à Pashupatinath", CC BY 2.0; Jorge Lascar, "Pashupatinath", CC BY 2.0) | A stone beam bridge with low solid grey stone parapet walls and a flat coping, between stepped stone ghats; no wooden railing | The heritage parapet in grey stone `#8E8A80` with a `#A8A296` coping at the ghats (brick `#A4553A` in the Durbar cores) |
