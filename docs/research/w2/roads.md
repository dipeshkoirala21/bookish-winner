# W2 research: real road widths of the Kathmandu Valley (width model for road building)

> Status: research input for Wave 2 "Kathmandu comes alive", 2026-10-05. Feeds ARCHITECTURE §7.4 (road ribbons), §7.7 (traffic lane graph), ROADMAP 1.4 (road mesher, junction caps) and 1.8, the road kit in [ASSET_MANIFEST](../../ASSET_MANIFEST.md) §3 (`ghm_rd_kerb_*`, `ghm_rd_footpath_tile_*`, `ghm_rd_drain_open_*`, `ghm_dcl_lane_*`, `ghm_rd_speed_breaker`), and gap R4 ("Width, lanes, junctions") in [CONTENT_COVERAGE](../../CONTENT_COVERAGE.md).
> Everything here is meant to become **generator parameters**. Every number carries a confidence tag (same scheme as [temples.md](temples.md) and [street_life.md](street_life.md)):
>
> | Tag | Meaning |
> |---|---|
> | **[O]** | Measured in this research from OSM (`pipeline/data/raw/osm/nepal.osm.pbf`, the snapshot the pack uses) with pyosmium + shapely, projected to UTM 45N (EPSG:32645), valley box 85.18–85.55 E, 27.55–27.82 N. Lengths are the in-box part of each way |
> | **[S]** | Stated by a cited source (URL in the row or in §13) |
> | **[E]** | Estimate or design value (from standards, OSM proportions or convention). Good enough for a cartoon world, not a survey |
> | **[V]** | Needs a human check (current practice, a recent rebuild, or a local rule) before it ships |
>
> Units are metres unless stated. Nepal drives on the **left**. Reference photographs are reference only. Nothing here is a sound or image asset, so the CC0 sound rule does not apply.

---

## 0. Ten-line summary

1. The valley box has **9,230 km of `highway=*`** [O]: residential 3,054 km, unclassified 1,885, path 1,361, track 973, tertiary 490, service 449, secondary 396, footway 242, primary 154, trunk 111.
2. Width tagging is good on the main roads and poor below them [O]: `width` covers trunk 74% / primary 78% / secondary 61% / tertiary 44% of length, but only residential 11% and unclassified 13%. `lanes` covers trunk 86% and primary 52%. `est_width`, `lanes:forward` and `width:carriageway` are effectively absent (≈ 0%). `sidewalk*` covers 28% of trunk (mostly `no`) and < 8% of everything else. `lit` < 7% everywhere.
3. Tagged carriageway medians [O]: trunk 9 m per way (6–7 m per one-way carriageway, 14–16 m on single 4-lane ways), primary 8, secondary 7, tertiary 6, residential 4, unclassified 3, track 3, footway 2.5. Mappers use whole metres (7, 6, 14, 4, 3 dominate).
4. **Ring Road verified** [O+S]: the Kalanki–Koteshwor south half (built with Chinese aid, done 2018–19) is mapped as two one-way carriageways of **2 lanes, 6–7 m** each, centrelines 7.4 m apart, plus parallel one-way **2-lane 6–7 m service roads** whose centrelines sit 9.3 m out. 4 + 4 = the "8 lanes" of the press; the building-to-building corridor is 64 m (p50), matching the 62 m right of way [S]. The north half (Kalanki–Balaju–Chabahil) is still **one undivided 4-lane way, 14 m**; its 8-lane rebuild was only signed in April 2026 [S].
5. Standards [S]: NRS 2070 right of way **50 / 30 / 20 m** (highway / feeder / district); lane **3.5 m** (single lane 3.75, intermediate 5.5); shoulders 0.75–3.75 m; median ≥ 5 m (3 m restricted). NURS 2076 urban right of way **50 / 30 / 20 / 10 m** (arterial / sub-arterial / collector / local); 2-lane 7.0 m (7.5 kerbed); local street 3 m per lane; minimum kerbed road 5.5 m; footpath ≥ 2.0 m (2.4 preferred, 3.5 at shop frontages), kerb/footpath **+150 mm**; municipal minimum road 4 m old settlements / 6 m new.
6. Building-to-building corridors [O] (a proxy for usable street space): old-core residential p25 6.7 m and p10 4.1 m; in the Asan–Indrachowk core 13% of motor-way samples and 25% of walkway samples are under 4 m wall to wall; urban residential p50 17 m; peri-urban 25 m. Patan and Bhaktapur cores are wider than Kathmandu's (p25 8.4–8.6 m).
7. Width model (§8): a real carriageway, lane count, footpath, median and drain per **class × area type** (OLD_CORE, URBAN, PERI_URBAN, RURAL, HILL), used when OSM has no `width`. Tagged widths always win.
8. Generous game rule (§9): `game = max(real × 1.25, class minimum)`, minimum **6.5 m** for two-way motor roads of tertiary and above, **4.0 m** for any one-way or single-lane motor road, then **clamped to the real corridor** (twice the nearer building distance minus 1 m) so roads never cut through real buildings, and never narrower than the real width. In simulation this clamp bites on 25–43% of urban and old-core samples and on ≤ 6% of rural and hill samples.
9. Narrow old-core lanes stay real (§9.4): under 1.8 m it is walk only (the bike is pushed); 1.8–3.5 m is motorbike, bicycle and walk; 3.5–4.5 m allows the player's car one way with no AI cars; ≥ 6.0 m allows bus and truck. This matches how Asan, Indrachowk and Bhaktapur really work and gives the old towns their scooter-only character.
10. Kit numbers (§10) [S]: centre and lane lines 100 mm white, broken 1.5 m line / 4.5 m gap in town; edge line 150 mm **yellow**; stop line 200 mm, 2–3 m before the zebra; zebra 2–4 m deep (never narrower than the footpath, ≥ 2 m); refuge island ≥ 1.2 m when the crossing is longer than 10.5 m; kerbs 100–200 mm (barrier type 150 mm in town), crossing kerbs ≤ 50 mm; speed humps 3.7 m wide, 100 mm high, 200 mm black-and-white stripes at 45°; median and island kerbs in 500 mm black-and-white bands.

---

## 1. Method

### 1.1 Data

* OSM ways with `highway=*` that have any node in the box: 49,369 ways. Buildings (514,206 footprints, from closed ways and multipolygons), `area:highway` polygons (57), forests (1,330), and 1,467 tagged highway nodes (crossings, signals, kerbs, mini-roundabouts) [O].
* Elevation: Copernicus GLO-30 (`pipeline/data/raw/dem/…N27_00_E085_00_DEM.tif`), sampled per 250 m cell.
* Scripts were scratch only; nothing under `pipeline/` was touched.

### 1.2 Area types

These are the same 250 m grid rules as [street_life.md](street_life.md) §1.1 (building coverage, ≥ 180 buildings for OLD_CORE, curated old-core circles from §1.3 there), with one addition for roads:

| Area type | Rule | Cells (250 m) [O] |
|---|---|---|
| OLD_CORE | inside a curated core circle (Asan–Indrachowk–Basantapur, Thamel, Patan Mangal Bazar, Bhaktapur, Kirtipur, Thimi, Bungamati, Khokana), or coverage ≥ 0.45 and ≥ 180 buildings | 135 |
| URBAN | coverage ≥ 0.22 | 1,004 |
| PERI_URBAN | coverage ≥ 0.06 | 2,360 |
| RURAL | everything else on the valley floor | 9,230 |
| **HILL** (new) | a PERI_URBAN, RURAL or FOREST cell whose steepest neighbour gradient is > 12% or whose DSM height is > 1,650 m | (part of the RURAL and FOREST cells) |

FOREST cells only carry roads as HILL roads here. The HILL rule is coarse (it uses a 30 m DSM and 250 m cells) [E], but it separates the valley floor (≈ 1,280–1,400 m) from the rim roads.

### 1.3 Corridor measurement ("wall to wall")

Every road way (no bridges, tunnels or non-zero layers) was sampled every 20 m. At each sample two rays were cast perpendicular to the centreline, 40 m each side, and the distance to the first building outline was recorded. **Corridor = left + right** when both sides hit a building within 40 m. This is the space between building fronts: carriageway + footpaths + setbacks + plinths. It is an **upper bound** on the paved width, and it is the **hard limit** for any widening, because buildings sit at their real positions (CONTENT_COVERAGE §1.1). Samples whose point lies inside a footprint (mapping offset) were ignored. Building-outline offsets in OSM are typically 1–3 m [E], so read p10 and p25 as "tight sections", not single-point truth. 682,621 samples were taken [O].

---

## 2. Standards: what Nepal designs to

### 2.1 Nepal Road Standard 2070 (DoR, strategic roads) [S]

| Item | Value | Source |
|---|---|---|
| Right of way | Highway **50 m**, feeder road **30 m**, district road **20 m** (Table 11-6) | NRS 2070 |
| Carriageway | Single lane **3.75 m** (3.0 m in difficult terrain); intermediate lane **5.5 m**; multi-lane **3.5 m per lane** (Table 11-1). Single-lane roads get treated shoulders to 5.5 m total | NRS 2070 |
| Two-lane capacity row | "Double lane road (7.0 m) with good quality shoulders ≥ 1.0 m" (Table 5-1) | NRS 2070 |
| Shoulders | ≥ 0.75 m each side; recommended Class I 3.75, II 2.5, III 2.0, IV 1.5 m (Table 11-2); a different colour or texture from the carriageway is desirable | NRS 2070 |
| Medians | Recommended on ≥ 4 lanes; minimum **5 m**, **3 m** where land is restricted; 1.5 m on long bridges (never < 1.2 m); width changes taper **1 in 20** | NRS 2070 §11.3 |
| Camber | Concrete 1.5–2.0%, bituminous 2.5%, gravel 4.0%, earthen 5.0% (Table 11-3) | NRS 2070 |
| Superelevation | Maximum 7% plain/rolling, 10% hills | NRS 2070 §11.6 |
| Vertical clearance | 5.0 m over the whole roadway; power lines 6 m (1 kV) to 9 m (550 kV) | NRS 2070 §11.9.3 |
| Design vehicle | Width **2.50 m**, height 4.75 m, length 18 m | NRS 2070 §4.1 |
| Road humps | Urban class IV roads only; width ≥ **3.7 m**, parabolic, **0.10 m** high; faces painted **200 mm** black and white stripes at 45° | NRS 2070 §13.2 |
| Cycle track | ≥ 1.2 m per direction, ≥ 1 m from the roadway | NRS 2070 §13.3 |
| Footpaths | All roads through populated areas; minimum **1.5 m**; 1.5 / 2.0 / 2.5 / 3.0 m for < 500 / 500–1,500 / 1,500–2,500 / 2,500–3,500 pedestrians per hour (Table 13-1); passing bay 1.8 × 2.0 m every ≤ 50 m on narrow paths | NRS 2070 §13.4 |
| Bus lay-bys | 15 m standing length per bus, ≥ 3.0 m wide (3.75 preferred), entry taper 5:1, exit 3:1, far side of junctions | NRS 2070 §13.5 |
| Kerbs | Barrier (vertical) or mountable (sloping); height **10–20 cm**; mountable at medians and islands; no vertical kerbs on high-speed roads | NRS 2070 §13.6 |
| Lighting | Poles ≥ 9 m (10–15 m preferred), 30 lux on important fast roads, 15 lux on other main roads | NRS 2070 §13.7 |
| Admin vs function | National highway = class I–II on plains, II–III in mountains; feeder = II–III / III–IV | NRS 2070 Table 3-1 |

### 2.2 Nepal Urban Road Standard 2076 (Ministry of Urban Development) [S]

| Item | Value |
|---|---|
| Classes | Arterial (*Path*), sub-arterial (*Sadak*), collector (*Marg*), local (*Upa-marg*) |
| Right of way (Table 16) | Arterial **50 m**, sub-arterial **30 m**, collector **20 m**, local **10 m** |
| Carriageway (Table 17) | Arterial/sub-arterial: single lane 3.5; 2-lane 7.0 (no kerb) / **7.5 (raised kerb)**; multi-lane 3.5 per lane. Collector: 3.5 / 7.0 / 7.5; 3-lane 10.5–11.0; 4-lane 14.0. **Local street 3.0 m per lane.** Minimum kerbed urban road **5.5 m** |
| Capacity table lanes (Table 5) | 1 lane 3.5–4.0 m; 2 lanes 7–7.5 m; 3 lanes 10.5 m; 4 lanes 14.0 m; 6 lanes 21.0 m |
| Design speed (Table 13) | Arterial 40–50 km/h, sub-arterial 30–40, collector 20–30, local 10–20 |
| Footpath (§4.3) | Clear width ≥ **2.0 m**, 2.4 m preferred on arterial/sub-arterial; +1 m dead width in business areas; **3.5 m** at shop frontages, **4.5 m** desirable on long frontages; cross-fall 2.5–3%; **raised +150 mm** over the carriageway |
| Footpath capacity (Table 18) | 2.0 / 2.5 / 3.0 / 4.0 m for 800 / 2,400 / 3,200 / 4,000 pedestrians per hour both ways |
| Cycle track (§4.4) | ≥ 2 m, +1 m per extra lane; raised +150 mm; 0.6 m buffer from parking or traffic |
| Medians (§4.5) | Required on ≥ 6 lanes; pedestrian refuge **1.2 m**; right-turn protection 4.0 m; urban absolute minimum **1.2 m**, desirable 5 m; continuous median when kerb-to-kerb ≥ 11 m |
| Parking lane (§4.7) | Parallel parking 3.0 m (2.5 m where tight) |
| Bus bays (§4.8) | ≥ 40 m from a junction; 15 m recess per bus; taper 1:8 (≥ 1:6); lay-bys 3 m × 30 m with 15 m tapers; stops every 200–400 m |
| Kerbs (§4.9) | Mountable at channelisation and medians; semi-barrier where pedestrians are few; **barrier type next to busy footpaths** |
| Camber (Table 20) | WBM/gravel 2.5–3.5%, thin bitumen 2–2.5%, high-type bitumen or concrete 1.5–2% |
| Raised crossings (§4.11) | Raised to footpath level (**150–200 mm**), vehicle ramps ≥ 1:4, at all junctions and every 150–200 m; crosswalk as wide as the footpath and never < 2 m |
| Lighting (Table 14) | Footpath/cycle track 4–6 m poles at 12–18 m; local/collector 9–10 m at 25–30 m; arterial/sub-arterial 10–12 m at 30–35 m; ≥ 15 lux |
| Curve widening (Table 10) | Two-lane: +1.5 m (R ≤ 40 m), +1.2 (41–60), +0.9 (61–100), +0.6 (101–300); single lane: +0.9 (R ≤ 20), +0.6 (21–60) |
| Lateral clearance | 1.0 m (arterial) / 0.5 m (collector, local) from the pavement edge without a footpath |
| BRT lane | 3.3 m + buffer |

### 2.3 Local Kathmandu Valley rules [S]

| Rule | Value | Source |
|---|---|---|
| Minimum municipal road | **4 m** in existing settlements; **6 m** in new settlements before a building permit; the post-2015-earthquake committee proposed 6 m everywhere for fire engines and ambulances | Kathmandu Post 2015 |
| DoR breadths quoted for the valley | Main highways 50 m, **Ring Road 62 m**, main roads 22 m, sub roads 14 m; grades from 8 m to 61 m | press summary of DoR (via search; [V] exact document) |
| Tripureshwor–Kalanki–Nagdhunga | Right of way 25 m (demolitions enforced on it) | Himalayan Times |
| Ring Road phase 1 (Koteshwor–Kalanki, 2011 plan) | 62 m: per side two main lanes, one service lane, one cycle track | ShareSansar 2011 |
| Ring Road phase 2 (Kalanki–Basundhara, 8.2 km) | 50–60 m, eight lanes plus 6 m pedestrian lanes each side; implementation agreement signed **April 2026**; not built yet | Himalayan Times 2019; eKantipur 2025, 2026 |
| Araniko Highway | Koteshwor–Jadibuti 6 lanes, about **44 m**; Suryabinayak–Dhulikhel being widened to 6 lanes with 2 service lanes and **2.5 m footpaths** each side (≈ 50–73% complete 2024–25) | JICA report; Ratopati; eKantipur 2025 |

---

## 3. OSM coverage by highway class [O]

Percentages are shares of length that carry the key. `sidewalk%` counts any `sidewalk*` key; the value is often `no`.

| highway | ways | km | width | lanes | est_width | lanes:fwd | oneway | surface | lit | sidewalk | name | maxspeed | width p10/50/90 (n ways) | lanes (length share) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| trunk | 334 | 111 | 74.4 | 86.1 | 0 | 0 | 84.3 | 92.5 | 4.8 | 28.3 | 96.4 | 52.5 | 6 / 9 / 14 (260) | 2: 76%, 4: 24% |
| trunk_link | 24 | 1 | 35.7 | 1.9 | 0 | 0 | 89.0 | 13.3 | 0 | 0 | 0 | 0 | 4 / 6 / 6 (6) | 2 |
| primary | 369 | 154 | 77.6 | 51.7 | 0 | 0.2 | 43.6 | 62.1 | 6.5 | 7.6 | 74.5 | 18.3 | 6 / 8 / 14 (314) | 2: 75%, 4: 21% |
| secondary | 675 | 396 | 61.0 | 15.1 | 0 | 0 | 11.6 | 49.4 | 1.4 | 1.8 | 55.6 | 6.4 | 4 / 7 / 9 (482) | 2: 84%, 4: 11%, 1: 5% |
| tertiary | 923 | 490 | 44.0 | 11.5 | 0 | 0 | 11.9 | 25.0 | 0.7 | 1.2 | 39.1 | 7.1 | 3 / 6 / 9 (489) | 2: 59%, 1: 40% |
| unclassified | 4,658 | 1,885 | 13.1 | 1.4 | 0 | 0 | 2.0 | 10.9 | 0.6 | 0.2 | 10.7 | 0.7 | 2 / 3 / 7 (611) | 1: 49%, 2: 51% |
| residential | 22,615 | 3,054 | 10.8 | 2.4 | 0 | 0 | 2.8 | 7.9 | 0.3 | 0.9 | 19.2 | 1.2 | 3 / 4 / 7 (1,397) | 1: 58%, 2: 41% |
| living_street | 231 | 26 | 5.0 | 6.9 | 0 | 0 | 6.2 | 19.3 | 0.3 | 1.6 | 27.2 | 4.9 | 2 / 2 / 5 (10) | 1: 72% |
| service | 4,736 | 449 | 5.8 | 0.9 | 0 | 0 | 2.2 | 7.0 | 0.2 | 0.1 | 3.9 | 0.4 | 2 / 6 / 8 (142) | 1: 59%, 3: 34% |
| track | 3,200 | 973 | 8.2 | 0.8 | 0 | 0 | 0.2 | 9.8 | 0 | 0 | 2.6 | 0.1 | 1 / 3 / 4 (271) | 1: 64%, 2: 30% |
| road | 27 | 8 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 1.3 | 0 | – | – |
| pedestrian | 145 | 15 | 16.0 | 0 | 0 | 0 | 3.0 | 31.8 | 6.0 | 0 | 30.0 | 0.7 | 2 / 4 / 8 (19) | – |
| footway | 2,702 | 242 | 8.7 | 0.2 | 0 | 0 | 0.1 | 10.1 | 1.0 | 0.2 | 10.9 | 0 | 0.9 / 2.5 / 4 (72) | – |
| path | 8,156 | 1,361 | 0.7 | 0 | 0 | 0 | 0.1 | 4.5 | 0.4 | 0 | 2.9 | 0 | 0.5 / 2 / 3 (70) | – |
| steps | 446 | 23 | 6.7 | 0 | 0 | 0 | 0.2 | 21.8 | 6.3 | 0 | 9.9 | 0 | 1.0 / 1.2 / 2.0 (14) | – |
| cycleway | 45 | 18 | 1.8 | 0 | 0 | 0 | 1.2 | 3.9 | 0.3 | 0 | 87.1 | 0 | – | – |
| construction | 10 | 23 | 0 | 0 | 0 | 0 | 100 | 0 | 0 | 0 | 100 | 0 | – | – |

Totals [O]: motor classes (trunk to track) 7,546 km, of which 1,323 km (17.5%) have `width`; trunk to tertiary 1,151 km, of which 659 km (**57%**) have `width`. The valley is much better tagged than Nepal as a whole (CONTENT_COVERAGE R4: `width` on ≤ 7% of road km).

Other tags [O]:
* `footway=sidewalk`: 15.6 km (separately drawn footpaths, mostly Kanti Path, Durbar Marg, Baneshwor, Araniko). `footway=crossing`: 6.0 km.
* `junction=roundabout` 13 ways, `junction=circular` 8 ways (Maitighar Mandala and Tripureshwor Chowk are `circular`); `highway=mini_roundabout` 8 nodes.
* Surface on the 7.5% of trunk with a value other than asphalt/paved is mostly missing; residential and unclassified are 92% and 89% untagged (ARCHITECTURE §6.2 surface inference applies).
* Rejected width strings: `10'0"` (5 ways), `12'0"`, `8-10`, `3 approx`. The pipeline's `parse_length_m` already handles feet and ranges.

### 3.1 Tagged width values (what mappers write) [O]

Share of tagged length per value.

| Class | Tagged km | Most common values |
|---|---|---|
| trunk | 82.8 | 6: 18%, 8: 16%, 14: 16%, 7: 13%, 9: 11%, 10: 10%, 16: 6%, 12: 5%, 22: 3% |
| primary | 119.3 | 7: 23%, 14: 21%, 6: 15%, 8: 14%, 9: 7%, 3: 5%, 10: 4%, 11: 4% |
| secondary | 241.5 | 7: 22%, 6: 19%, 9: 14%, 4: 14%, 8: 10%, 5: 9%, 10: 5% |
| tertiary | 210.4 | 7: 21%, 6: 20%, 4: 17%, 3: 10%, 5: 9%, 8: 6% |
| unclassified | 246.6 | 3: 35%, 4: 24%, 2: 13%, 1: 6%, 5: 5%, 7: 4% |
| residential | 330.8 | 3: 23%, 4: 23%, 5: 17%, 6: 14%, 7: 9%, 2: 4%, 8: 4% |
| service | 26.1 | 6: 21%, 7: 16%, 8: 16%, 3: 16%, 4: 11% |
| track | 79.3 | 3: 34%, 2: 26%, 4: 18%, 1: 11% |
| footway | 20.8 | 3: 29%, 4: 20%, 1: 20%, 0.9: 13%, 2: 10% |

Mapper widths are whole metres and sometimes include shoulders or the whole street (a 3 m "primary" is an error; a 14 m "tertiary" is a road plus its verge). Treat a tag as true but **clamp it** to `[class floor, corridor]` (§9.3).

---

## 4. Width and lanes by class × area type [O]

Tagged `width` (length-weighted p25 / p50 / p75) and `lanes` (share of `lanes`-tagged length; "w1" = on a one-way way, so one carriageway of a dual road). For one-way NH39/NH34 carriageways the width was doubled to give the full road.

| Class | Area | km | width-tagged km | width p25/p50/p75 | lanes |
|---|---|---|---|---|---|
| trunk | URBAN | 54 | 51 | 12 / 14 / 16 | 2w1: 61%, 4: 31%, 2: 5% |
| trunk | PERI_URBAN | 29 | 21 | 14 / 18 / 20 | 2w1: 78%, 4w1: 10%, 4: 8% |
| trunk | HILL | 24 | 6 | 8 / 8 / 8 | 2: 75%, 2w1: 25% |
| primary | OLD_CORE | 6 | 6 | 7 / 14 / 14 | 2: 37%, 4: 29%, 2w1: 23% |
| primary | URBAN | 49 | 49 | 7 / 7 / 12 | 2w1: 63%, 4: 18%, 2: 9% |
| primary | PERI_URBAN | 38 | 37 | 6 / 7 / 8 | 4: 52%, 2w1: 21%, 2: 20% |
| primary | RURAL | 6 | 5 | 6 / 6 / 7 | 2w1: 54%, 4w1: 46% |
| primary | HILL | 54 | 21 | 8 / 10 / 14 | 2: 99% |
| secondary | OLD_CORE | 8 | 8 | 4 / 6 / 7 | – |
| secondary | URBAN | 76 | 72 | 7 / 7 / 9 | 2: 81%, 4: 18% |
| secondary | PERI_URBAN | 77 | 63 | 5 / 7 / 8 | 2: 69%, 1: 16%, 4: 11% |
| secondary | RURAL | 15 | 8 | 4 / 5 / 5 | – |
| secondary | HILL | 221 | 89 | 4 / 6 / 7 | 2: 91%, 4: 6% |
| tertiary | OLD_CORE | 8 | 7 | 4 / 5 / 6 | – |
| tertiary | URBAN | 75 | 69 | 6 / 7 / 7 | 2: 63%, 1: 19%, 2w1: 15% |
| tertiary | PERI_URBAN | 82 | 47 | 5 / 6 / 8 | 2: 71%, 1: 26% |
| tertiary | RURAL | 37 | 17 | 4 / 4 / 6 | 2: 66%, 1: 34% |
| tertiary | HILL | 288 | 71 | 3 / 5 / 7 | 1: 60%, 2: 40% |
| unclassified | URBAN | 61 | 15 | 3 / 4 / 7 | 1: 97% |
| unclassified | PERI_URBAN | 150 | 26 | 3 / 5 / 6 | 2: 73%, 1: 27% |
| unclassified | RURAL | 187 | 17 | 3 / 4 / 4 | 1: 76% |
| unclassified | HILL | 1,483 | 188 | 3 / 3 / 4 | 1: 56%, 2: 44% |
| residential | OLD_CORE | 99 | 12 | 4 / 5 / 6 | 2: 60%, 1: 38% |
| residential | URBAN | 922 | 119 | 4 / 5 / 6 | 1: 51%, 2: 44% |
| residential | PERI_URBAN | 919 | 90 | 4 / 5 / 6 | 1: 79%, 2: 18% |
| residential | RURAL | 314 | 24 | 3 / 4 / 5 | 2: 66%, 1: 34% |
| residential | HILL | 799 | 84 | 3 / 3 / 5 | 2: 74%, 1: 22% |
| service | URBAN | 143 | 13 | 4 / 6 / 6 | 1: 80% |
| service | PERI_URBAN | 129 | 8 | 6 / 8 / 8 | 3: 72% (airport/industrial aprons) |
| track | HILL | 807 | 65 | 2 / 3 / 3 | 1: 58%, 2: 42% |
| footway | OLD_CORE | 17 | 2 | 1.5 / 3 / 3 | – |
| footway | PERI_URBAN | 51 | 6 | 3 / 4 / 4 | – |
| path | HILL | 1,108 | 4 | 1.5 / 2 / 2 | – |

Where the road length sits [O] (km): residential OLD_CORE 104 / URBAN 913 / PERI 917 / RURAL 325 / HILL 793; unclassified 5 / 63 / 154 / 177 / 1,455; tertiary 10 / 71 / 92 / 40 / 273; secondary 10 / 77 / 69 / 25 / 214; primary 7 / 49 / 35 / 8 / 52; trunk 0 / 53 / 30 / 3 / 26; footway 16 / 85 / 48 / 14 / 79; path 17 / 38 / 92 / 92 / 1,102.

---

## 5. Building-to-building corridors [O]

### 5.1 By class × area type

"Both built" = share of samples with a building within 40 m on both sides. Corridor percentiles are over those samples.

| Class | Area | Samples | Both built % | Corridor p10 / p25 / p50 / p75 / p90 |
|---|---|---|---|---|
| trunk | URBAN | 2,766 | 45 | 22.9 / 42.1 / 54.9 / 63.1 / 67.8 |
| trunk | PERI_URBAN | 1,552 | 28 | 26.9 / 37.8 / 50.1 / 59.1 / 66.1 |
| trunk | HILL | 1,596 | 12 | 24.1 / 34.8 / 47.7 / 55.0 / 64.8 |
| primary | OLD_CORE | 385 | 50 | 11.1 / 19.8 / 24.5 / 35.3 / 45.0 |
| primary | URBAN | 2,812 | 42 | 15.8 / 19.9 / 28.4 / 39.5 / 51.4 |
| primary | PERI_URBAN | 1,896 | 28 | 14.8 / 20.1 / 27.8 / 40.2 / 49.3 |
| primary | HILL | 2,829 | 12 | 11.5 / 16.7 / 24.4 / 36.2 / 46.3 |
| secondary | OLD_CORE | 604 | 71 | 6.2 / 9.7 / 14.3 / 28.3 / 39.4 |
| secondary | URBAN | 4,510 | 56 | 9.8 / 13.5 / 21.1 / 33.0 / 43.4 |
| secondary | PERI_URBAN | 4,247 | 28 | 10.9 / 16.0 / 24.8 / 37.9 / 48.5 |
| secondary | HILL | 16,237 | 6 | 10.9 / 15.1 / 23.4 / 36.1 / 46.0 |
| tertiary | OLD_CORE | 593 | 67 | 5.7 / 9.5 / 16.1 / 27.4 / 40.4 |
| tertiary | URBAN | 4,495 | 56 | 8.8 / 13.3 / 24.2 / 37.9 / 48.6 |
| tertiary | PERI_URBAN | 5,670 | 30 | 10.0 / 14.2 / 24.5 / 36.6 / 46.6 |
| unclassified | OLD_CORE | 404 | 69 | 3.6 / 5.9 / 11.7 / 24.4 / 33.3 |
| unclassified | URBAN | 4,391 | 64 | 5.4 / 9.2 / 17.7 / 31.1 / 41.6 |
| unclassified | PERI_URBAN | 10,758 | 25 | 8.7 / 14.4 / 25.9 / 38.8 / 48.5 |
| residential | OLD_CORE | 6,891 | 76 | 4.1 / 6.7 / 12.6 / 23.9 / 35.7 |
| residential | URBAN | 62,762 | 71 | 5.8 / 9.2 / 17.2 / 29.8 / 41.0 |
| residential | PERI_URBAN | 65,728 | 30 | 7.9 / 13.6 / 25.1 / 38.2 / 48.8 |
| residential | HILL | 58,674 | 17 | 8.2 / 13.8 / 25.1 / 38.1 / 48.8 |
| living_street | OLD_CORE | 359 | 84 | 3.0 / 5.5 / 12.0 / 23.6 / 32.6 |
| living_street | URBAN | 612 | 76 | 4.5 / 7.7 / 12.6 / 24.7 / 35.8 |
| service | OLD_CORE | 920 | 55 | 4.0 / 8.4 / 18.0 / 32.4 / 42.0 |
| service | URBAN | 10,433 | 49 | 6.3 / 11.4 / 21.5 / 34.8 / 46.3 |
| pedestrian | OLD_CORE | 257 | 59 | 6.8 / 11.4 / 20.9 / 32.4 / 44.8 |
| footway | OLD_CORE | 1,327 | 74 | 2.5 / 4.7 / 11.0 / 22.3 / 34.7 |
| footway | URBAN | 6,740 | 59 | 3.2 / 6.2 / 13.4 / 26.6 / 39.3 |
| path | OLD_CORE | 1,462 | 75 | 2.3 / 4.4 / 10.5 / 20.7 / 33.0 |
| steps | OLD_CORE | 146 | 45 | 2.8 / 7.0 / 19.1 / 31.9 / 42.9 |
| track | URBAN | 829 | 66 | 3.1 / 6.7 / 15.5 / 30.9 / 41.9 |

Share of samples whose corridor is under a threshold (residential, unclassified, service, living_street, footway, pedestrian, path and tertiary together) [O]:

| Area | n | < 2 m | < 3 m | < 4 m | < 5 m | < 6 m | < 8 m | < 10 m |
|---|---|---|---|---|---|---|---|---|
| OLD_CORE | 8,916 | 3% | 7% | 12% | 18% | 23% | 33% | 41% |
| URBAN | 61,895 | 1% | 2% | 5% | 8% | 12% | 20% | 28% |
| PERI_URBAN | 28,839 | 0% | 1% | 2% | 3% | 5% | 10% | 15% |

Corridor ÷ tagged width (median, where both are known) [O]: trunk 5.2, primary 3.1, secondary 3.0, tertiary 4.1, residential 4.7, unclassified 5.9. Real carriageways fill only a quarter to a third of the space between buildings. **There is room to widen almost everywhere outside the cores.**

### 5.2 The old cores one by one [O]

| Core | Group | Samples | Both built % | p10 | p25 | p50 | < 3 m | < 4 m | < 6 m |
|---|---|---|---|---|---|---|---|---|---|
| Kathmandu Asan–Indrachowk–Basantapur | motor ways | 1,780 | 81 | 3.7 | 5.9 | 11.8 | 7% | 13% | 26% |
| | walkways | 1,293 | 72 | 2.2 | 4.0 | 9.2 | 16% | 25% | 35% |
| Thamel | motor ways | 718 | 82 | 4.1 | 6.5 | 12.6 | 4% | 10% | 22% |
| | walkways | 262 | 80 | 2.5 | 4.3 | 9.4 | 14% | 23% | 38% |
| Patan Mangal Bazar | motor ways | 1,210 | 74 | 5.0 | 8.6 | 17.1 | 3% | 6% | 14% |
| | walkways | 388 | 64 | 5.6 | 11.7 | 22.0 | 4% | 6% | 11% |
| Bhaktapur core | motor ways | 2,298 | 59 | 4.8 | 8.4 | 15.4 | 3% | 6% | 16% |
| | walkways | 218 | 60 | 3.1 | 6.4 | 16.2 | 8% | 18% | 24% |
| Kirtipur | motor ways | 396 | 56 | 7.9 | 12.0 | 20.3 | 0% | 1% | 4% |
| | walkways | 402 | 68 | 2.4 | 4.6 | 13.3 | 16% | 22% | 31% |
| Thimi | motor ways | 669 | 35 | 5.3 | 10.4 | 21.3 | 4% | 8% | 11% |
| | walkways | 169 | 56 | 2.9 | 7.3 | 13.7 | 12% | 16% | 20% |
| Khokana | motor ways | 97 | 31 | 7.7 | 12.1 | 19.5 | 0% | 3% | 10% |

"Motor ways" = primary to residential plus living_street and service; "walkways" = footway, path, pedestrian and steps. Bungamati has too few mapped ways (0.2 km living_street) to measure; Bhaktapur's and Patan's building mapping is incomplete (street_life §1.3), so their corridors read wider than they are.

Tagged widths inside the cores [O]: Asan–Indrachowk 58 ways with `width`, p25/p50/p75 4 / 6 / 8.75 m; Patan 55 ways, 4 / 5 / 6; Bhaktapur 40 ways, 4 / 5 / 7; Thamel 20 ways, 6.5 / 7 / 10.25.

The old-core galli (lane) is real and measurable: **2–4 m wall to wall on a quarter of Kathmandu-core walkways**, and 3.7–6 m on the motor streets at p10–p25. The task brief's "old core gallis 2–4 m" holds.

### 5.3 `area:highway` polygons [O]

Only 57 polygons exist (20 service, 14 pedestrian, 9 traffic islands, 7 footway). Width = area ÷ centreline length inside the polygon:

| Polygon | Area m² | Width ≈ |
|---|---|---|
| Patan Durbar Square (pedestrian) | 3,059 | 19.7 m |
| Basantapur Durbar area (pedestrian) | 7,994 | 12.1 m |
| Ta Bahal (pedestrian) | 643 | 6.3 m |
| Dugha Bahi (pedestrian) | 215 | 3.4 m |
| Unnamed old-core pedestrian lanes (6) | 397–3,573 | 2.8–6.3 m (median ≈ 4 m) |
| Te Bahal (service) | 3,529 | 15.4 m |
| Service lanes (17) | 79–4,446 | 3.0–11.2 m (median ≈ 4.5 m) |
| Bus stop | 298 | 2.0 m (lay-by depth) |
| Traffic islands on the Araniko Highway, Koteshwor–Thimi (9) | 700–2,070 | equal-area Ø 30–51 m |

Too few polygons to model from, but they confirm the core numbers: shared lanes ≈ 3–6 m and squares 12–20 m. Riverbanks and landuse were not useful for roads.

---

## 6. Named roads [O unless marked]

Corridor = building to building (§1.3); "width" = OSM tag on the way.

| Road | km | OSM class, ref | Lanes, width as mapped | Corridor p25 / p50 / p75 | Notes |
|---|---|---|---|---|---|
| **Ring Road, south half (Kalanki–Balkhu–Ekantakuna–Satdobato–Gwarko–Koteshwor)** | 21.6 | trunk NH39 | **Dual carriageway**: one-way ways, 2 lanes, 7 m (10.6 km) or 6 m (9.2 km); carriageway centrelines 7.4 m apart (p25–p75 7.0–8.0). **Service roads**: unnamed one-way `primary`, 2 lanes, 6–7 m, centrelines 9.3 m (8.6–10.7) from the main carriageway | 61.8 / **64.3** / 67.9 | The "8 lanes" are 2 + 2 main + 2 + 2 service [O]. Right of way 62 m [S]. `cycleway=no` on 24 km; maxspeed 40–50 on 7 km. Derived cross-section in §6.1 |
| **Ring Road, north half (Kalanki–Balaju–Maharajgunj–Chabahil–Tinkune)** | 17.4 | trunk NH39 | **Single way, 4 lanes, 14 m** (12.5 km) or 16 m (2.8 km); 11% one-way pieces at junctions | 58.8 / 62.3 / 66.3 | Phase 2 (Kalanki–Basundhara, 8 lanes, 50–60 m) signed April 2026, not built [S]. Model the road as mapped: 4 lanes undivided |
| Araniko Highway (Koteshwor–Bhaktapur–Sanga) | 40.0 | trunk NH34 | One-way carriageways, 2 lanes: 9 m (6.4 km), 10 m (6.1 km), 6 m (4.9 km); 4w1 10 m (2.4 km); centrelines 10.8 m apart (7.0–12.2) | 44.3 / 49.8 / 55.6 | "6-lane, about 44 m" Koteshwor–Jadibuti [S]. The 9–10 m one-way carriageways are 3 lanes drawn as 2 [E]. `sidewalk=no` on 28 km |
| Kanti Path | 2.7 | primary | 14 m (1.7 km), 12 m (0.6); lanes 2–4; 68% one-way pieces | 24.0 / 33.8 / 41.9 | Mapped sidewalk centrelines lie 10.9 m from the road centreline, so footpaths ≈ 3–4 m on a 14 m carriageway [E] |
| Durbar Marg | 1.5 | primary (+ Bag Durbar residential) | 9 m and 14 m; 80% one-way pieces (divided boulevard) | 20.9 / 32.3 / 42.6 | Sidewalk centrelines 7.0 m from the carriageway centreline; central median with trees [E/V] |
| Lazimpat (Lazimpat Sadak, F25) | 1.7 | primary F25 | **4 lanes, 14 m**, two-way, `sidewalk=both` | 19.9 / 24.9 / 37.3 | A full 4-lane urban arterial; continues north as Narayan Gopal Sadak (4 lanes, 14 m) |
| New Road (Juddha Sadak) | 1.4 | tertiary / unclassified | 5 m (0.9 km), 1 lane on 0.4 km, 66% one-way | 14.3 / 18.8 / 43.3 | Mapped carriageway is narrow, but the corridor is 14–19 m: wide footpaths and shopfronts [E] |
| Tripureshwor Marg | 2.4 | trunk (NH41 end) | 16 m (0.9 km), 4 lanes (0.9 km) | 21.0 / 25.1 / 33.1 | Right of way 25 m on Tripureshwor–Kalanki [S] |
| Tribhuvan Rajpath / Kalanki–Nagdhunga | 30.5 | trunk NH41 | 2 lanes (24.9 km); 22 m at the Kalanki end (2.7 km), 12 m and 16 m in parts; untagged on 23.9 km | 23.7 / 30.8 / 42.9 | The Nagdhunga Tunnel (2.7 km) is tagged as a separate way (CONTENT_COVERAGE R5) |
| Putalisadak / Bagbazar | 2.4 | primary, secondary | 6 m and 12 m; 2 lanes | 18.5 / 21.6 / 30.4 | |
| Chabahil–Boudha–Jorpati (F26/F27) | 11.2 | primary, secondary | 14 m (2.6 km), 8 m (2.2 km); 3–4 lanes on 2.6 km | 13.5 / 21.9 / 32.5 | Planned right of way 22 m [S] |
| Satdobato–Godawari (F24) | 12.4 | tertiary, primary | 11 m (9 km) | 15.1 / 23.6 / 36.7 | The 11 m tag probably includes shoulders [E] |
| Tokha / Chhahare (NH40, F82) | 15.7 | secondary | 2 lanes; 8–10 m; asphalt and concrete | 14.1 / 22.8 / 34.8 | |
| Thamel streets (Jyatha, Thahity–Jyatha, Thamel Marg, Chaksibari) | 2.7 | residential | 4 m where tagged; no lanes | **5.8 / 8.8 / 15.0** | Typical old-core shopping street: 4–6 m of road between 9 m building fronts |

### 6.1 Ring Road south: derived cross-section [O/E]

From the measured centreline offsets, with the 62 m right of way [S]:

| Element (from the centre outwards, each side) | Width |
|---|---|
| Central median (kerbed, mountable, with a steel fence or barrier) | ≈ 1.0 m total (7.4 m centreline spacing − 2 × 3.2 m half carriageway) [E] |
| Main carriageway, 2 lanes | 6.5 m (OSM 6–7) [O] |
| Separator / green belt between main and service carriageway | ≈ 2.8 m (9.3 m offset − 3.25 − 3.25) [E] |
| Service carriageway, 2 lanes, one-way with the main flow | 6.5 m (OSM 5–7) [O] |
| Cycle track + footpath + verge to the right-of-way line | ≈ 13 m remaining to 31 m; split as cycle track 2.0 m, footpath 3.0 m, verge or drain 1.0 m, the rest building setback [E] |

Total paved road ≈ 2 × (6.5 + 2.8 + 6.5) + 1.0 ≈ **32.6 m**, inside a 62–64 m corridor [O/E]. The press description "per side two main lanes, one service lane, one cycle track" [S] matches, except that OSM tags the service road as 2 lanes [O]. **Verified: 8 motor lanes in total, of which 4 are main lanes.** [V] A check against current street-level photos is still needed for the median and separator widths.

---

## 7. Junctions, crossings and roundabouts [O]

| Feature | Count in box | Notes |
|---|---|---|
| `highway=crossing` | 617 | `crossing=uncontrolled` 143, `marked` 85, `zebra` 52, `traffic_signals` 13, `unmarked` 18, `informal` 6; `crossing:markings=yes` 130, `zebra` 8 |
| `highway=traffic_signals` | 50 | Kathmandu relies mostly on traffic police at chowks [E] |
| `highway=mini_roundabout` | 8 | |
| `junction=roundabout` / `circular` ways | 13 / 8 | Jawalakhel roundabout: ring centreline 138 m, so Ø ≈ **44 m** outer road ring, `width=7`; Dhumbarahi roundabout Ø ≈ 16 m (centreline 49 m), `width=7`; Ring Road Balaju roundabout centreline 88 m (Ø ≈ 28 m), `width=9`; Maitighar Mandala and Tripureshwor Chowk mapped as `circular` trunk, `width` 6–8; small roundabouts Ø 13–29 m |
| Large median islands (`area:highway=traffic_island`) | 9 | Araniko Highway (Koteshwor–Thimi): equal-area Ø 30–51 m |
| `kerb=*` nodes | 17 | raised 14, lowered 1, flush 1: almost no data, so kerbs must be generated |
| `traffic_calming` | 1 (table) | Speed breakers are effectively unmapped; place them procedurally (§10) |
| `highway=street_lamp` | 327 | (street_life §5 generates the rest) |
| `highway=bus_stop` | 370 nodes | 543 bus-stop objects in total (street_life §1.2) |

Roundabout model for the game [E from the measurements above]: central island Ø = ring Ø − 2 × ring width; ring width = the widest approach + 1 m (min 7 m); a mountable apron 1.0 m inside the ring for buses; splitter islands on approaches ≥ 7 m wide. Chowks without a mapped roundabout get a **plain junction cap** and, if `highway=traffic_signals` or a famous chowk, a traffic-police platform (Ø 1.5 m, 0.3 m high, white with a red or blue roof) [E/V].

---

## 8. The width model (real values, used when OSM has no `width`)

Everything is per **class × area type**. Lanes are total lanes on a two-way way, or per carriageway on a one-way way. "Footpath" is per side; "–" means none (shared street). These are design values from §2–§6. The [O] medians from §4 anchor them, and standards fill the gaps.

### 8.1 Carriageway and lanes

| Class | OLD_CORE | URBAN | PERI_URBAN | RURAL (valley floor) | HILL |
|---|---|---|---|---|---|
| trunk (two-way way) | – | 14.0 m, 4 lanes | 14.0 m, 4 lanes | 9.0 m, 2 lanes + 1.5 m shoulders | 7.5 m, 2 lanes + 1.0 m shoulders |
| trunk (one-way carriageway) | – | 7.0 m, 2 lanes | 7.0 m, 2 lanes (9–10 m = 3 lanes on Araniko) | 7.0 m, 2 lanes | 7.0 m, 2 lanes |
| primary | 7.0 m, 2 lanes | 7.0 m, 2 lanes (14 m, 4 lanes if `lanes=4`) | 7.0 m, 2 lanes | 6.5 m, 2 lanes | 7.0 m, 2 lanes + 1.0 m shoulders |
| secondary | 6.0 m, 2 lanes | 7.0 m, 2 lanes | 7.0 m, 2 lanes | 5.5 m, intermediate | 5.5–6.0 m, 2 lanes + 0.75 m shoulders |
| tertiary | 5.0 m, 1–2 | 7.0 m, 2 lanes | 6.0 m, 2 lanes | 4.5 m, 1 lane | 4.5 m, 1 lane (3.75 + passing bays) |
| unclassified | 3.5 m, 1 | 4.0 m, 1 | 5.0 m, 1–2 | 3.5 m, 1 | 3.5 m, 1 (unpaved common) |
| residential | 4.0 m, 1 | 5.0 m, 1–2 | 5.0 m, 1 | 3.5 m, 1 | 3.5 m, 1 |
| living_street | 3.0 m | 3.0 m | 3.5 m | 3.0 m | 3.0 m |
| service | 3.5 m | 4.0 m (6 m at fuel stations, bus parks) | 4.5 m | 3.5 m | 3.0 m |
| track | 3.0 m | 3.0 m | 3.0 m | 3.0 m | 3.0 m |
| pedestrian (street, not a square) | 4.0 m | 4.0 m | 4.0 m | – | 3.0 m |
| footway | 1.5–2.0 m (galli) | 2.0 m (sidewalk 2.5) | 2.0 m | 1.5 m | 1.2 m |
| path | 1.5 m | 1.5 m | 1.5 m | 1.2 m | 1.0 m (trail) |
| steps | 1.5 m | 2.0 m | 1.5 m | 1.2 m | 1.2 m |
| cycleway | – | 2.0 m | 2.0 m | 2.0 m | 2.0 m |

Lane count when `lanes` is missing: `lanes = clamp(round(width / 3.5), 1, 6)` for trunk to secondary; `round(width / 3.0)` for tertiary and below; a two-way way under 5.5 m is 1 lane (no centre line, per RSN5 and NURS) [S/E].

### 8.2 Footpaths, medians, kerbs and drains

| Class | Area | Footpath per side | Median | Kerb | Drain |
|---|---|---|---|---|---|
| trunk dual (Ring Road S, Araniko) | URBAN, PERI | 2.5–3.0 m (+ 2.0 m cycle track on Ring Road S) | 1.0–1.5 m kerbed + fence; 2.8 m green separator to the service road | Barrier 150 mm on footpaths; mountable 100 mm on medians | Covered slab drain under the footpath edge, 0.6 m |
| trunk 4-lane undivided (Ring Road N) | URBAN | 2.0 m, frequently broken | None, or a 0.3–0.5 m concrete divider with a fence on busy stretches [V] | 150 mm | Covered, 0.6 m |
| trunk | HILL | None; 1.0 m shoulder | None | None | **Open stone/concrete side drain on the uphill side, 0.6 × 0.45 m** |
| primary | OLD_CORE | 1.5 m where it exists, often none | None | 150 mm | Covered |
| primary | URBAN | 2.0–3.5 m (Kanti Path ≈ 3–4 m, Durbar Marg wide) | Durbar Marg and Kanti Path: tree median 1.5–4 m [V]; 4-lane F25 none | Barrier 150 mm | Covered 0.45–0.6 m |
| primary | PERI_URBAN | 1.5 m on 50% of length | None | 150 mm where there is a footpath | Open 0.5 m |
| secondary, tertiary | URBAN | 1.5–2.0 m on 60%, one side on 20%, none on 20% | None | 150 mm | Covered or open 0.45 m |
| secondary, tertiary | PERI, RURAL | None (0.5–1.0 m earth shoulder) | None | None | Open earth or stone ditch 0.5 m |
| secondary, tertiary | HILL | None; 0.75 m shoulder | None | None | Uphill open drain 0.6 m; valley side: parapet blocks or guardrail on curves |
| residential, unclassified | OLD_CORE | **None**: shared surface; 0.3–0.6 m brick plinths (*dalan*) along house fronts | None | None (flush) | Central or side channel 0.2–0.3 m in brick (Bhaktapur, Patan) |
| residential, unclassified | URBAN, PERI | None (shared street) | None | None | Side channel 0.3 m |
| living_street, pedestrian | all | Shared | None | Flush | Brick channel |

Presence rates are estimates [E]. OSM `sidewalk*` covers too little length to measure them (§3). In the valley, footpaths mostly exist on trunk and primary roads in URBAN cells [O: the `sidewalk=both` and `footway=sidewalk` lengths sit on Kanti Path, Durbar Marg, Lazimpat, Baneshwor, Araniko and the Ring Road].

---

## 9. The game width rule ("generous but real")

The owner asked for wider roads. Buildings are at their real positions and the road centreline is exact (ARCHITECTURE §5, CONTENT_COVERAGE §1.1), so a road can only grow **into the free space between building fronts**. §5 shows that this space is usually 3–5 times the carriageway outside the old cores.

### 9.1 Formula

```
real   = tagged width (parse_length_m)                if present and plausible
         else §8.1 default for (class, area type)
real   = clamp(real, classFloor, classCeiling)        # classFloor: trunk 6, primary 5, secondary 4, tertiary 3,
                                                       # residential/unclassified 2.5, service 2.5, track 2, footway 0.9, path 0.6
                                                       # ceiling 40 (existing RoadStyle.MaxWidthM)
scaled = real × S(class)                               # S = 1.25 motor classes, 1.0 footway/path/steps, 1.15 pedestrian
game   = max(scaled, Min(class, oneway))               # §9.2
limit  = 2 × min(dLeft, dRight) − 1.0                  # corridor clamp (0.5 m clearance to each building)
game   = max(min(game, limit), real)                   # never cut buildings, never narrower than reality
game   = min(game, real + 6.0)                         # cap the absolute gain so proportions survive
```

`dLeft` and `dRight` are the per-sample building distances from §1.3. The pipeline can compute them once and store a per-way or per-segment `corridor_cm` (§11). If they are unknown, use `limit = ∞` outside OLD_CORE and `limit = real` inside it.

Why **1.25** [E]: it turns 3.5 m lanes into 4.4 m lanes and a 7 m two-lane road into 8.75 m. That reads clearly as "wider" on a phone, fits a chunky cartoon car (≈ 2.0–2.2 m wide, about 1.15–1.2 × a real 1.7–1.9 m hatchback) with room to overtake a scooter, and keeps the order trunk > primary > secondary > residential (a uniform factor keeps proportions). It also stays inside the corridor on 57–98% of samples (§9.3).

### 9.2 Minimum widths (game, after scaling)

| Road kind | Minimum game width | Reasoning |
|---|---|---|
| Two-way trunk, primary, secondary, tertiary | **6.5 m** | Two buses of 2.5 m (NRS design vehicle) + 3 × 0.5 m clearance = 6.5 [S/E] |
| One-way carriageway of a dual road | 7.0 m (2 lanes) | Ring Road and Araniko carriageways are 6–7 m real |
| Two-way residential, unclassified, service | 4.0 m | One car and one scooter pass; the NURS local street is 3 m per lane, and the municipal minimum is 4 m [S] |
| Any single-lane or one-way motor road | 4.0 m | Driving a cartoon car between walls needs ≥ 1 m clearance each side [E] |
| Track | 3.0 m | Jeep tracks; the real p50 is 3 m |
| Living street | 3.0 m | Shared surface |
| Footway, path, galli | 1.5 m (OLD_CORE 1.2 m) | Two walkers pass; camera clearance [E] |
| Steps | 1.2 m | |

The minimum never overrides the corridor clamp. **The clamp wins, and the access rules in §9.4 decide what may drive there.**

### 9.3 Simulation against real corridors [O]

The §9.1 rule was applied to every corridor sample (§1.3). "Clamped" = the scaled width would come within 0.5 m of a building on the nearer side, so the clamp reduced it.

| Class | Area | Real p50 used | Game p50 | Clamped samples | Game < 4 m after clamp |
|---|---|---|---|---|---|
| trunk (dual, per carriageway) | URBAN | 7 | 11.2 (a mix of one-way pieces and 14 m ways) | 7% | 0% |
| primary | URBAN | 7 | 8.8 | 15% | 0% |
| primary | OLD_CORE | 7–14 (tagged) | 16.2 | 22% | 0% |
| secondary | URBAN | 7 | 8.8 | 28% | 0% |
| secondary | OLD_CORE | 6 | 7.5 | 39% | 3% |
| tertiary | URBAN | 7 | 8.1 | 25% | 0% |
| tertiary | HILL | 4.5 | 6.0 | 3% | 0% |
| residential | OLD_CORE | 4 | 5.0 | 43% | 1% |
| residential | URBAN | 5 | 6.2 | 37% | 1% |
| residential | PERI_URBAN | 5 | 6.2 | 16% | 0% |
| residential | HILL | 3.5 | 4.4 | 6% | 5% |
| unclassified | OLD_CORE | 3.5 | 4.4 | 39% | 34% |
| unclassified | HILL | 3.5 | 4.4 | 2% | 1% |
| service | URBAN | 4 | 5.0 | 19% | 1% |
| living_street | OLD_CORE | 3 | 3.8 | 46% | 97% |
| track | HILL | 3 | 3.8 | 1% | 98% |

Reading: outside the cores the generous widths fit almost everywhere. In URBAN and OLD_CORE a quarter to two fifths of samples are limited by a building, so the road there will **vary in width along its length**, as real Kathmandu streets do. The mesher must taper changes (§9.5).

### 9.4 Access by final game width (the old-core rule)

The narrow lanes **stay real**. Pushing them to 6 m would delete the most characteristic part of Kathmandu, Patan and Bhaktapur, and is impossible anyway without moving buildings. Width decides what may enter:

| Final game width | Player may | AI traffic | Real-world match |
|---|---|---|---|
| < 1.8 m | Walk; the bicycle or motorbike is pushed (slow walk animation) | Pedestrians only | Asan and Indrachowk gallis, bahal passages, Bhaktapur alleys |
| 1.8–3.5 m | Walk, cycle, ride a motorbike or scooter | Motorbikes, bicycles, porters, carts | Most old-core lanes; Thamel side lanes |
| 3.5–4.5 m | Also drive a car or taxi (one lane, no overtaking) | Motorbikes + an occasional car or taxi in one direction only; no microbus | New Road side streets, Patan core streets |
| 4.5–6.0 m | Also microbus, jeep, tempo, small truck | Two-way cars; no bus, no full truck | Typical residential streets |
| ≥ 6.0 m | Everything: bus, truck, tipper | Full mix (street_life §5 traffic mix) | Tertiary and above |

Extra rules:
* **Heritage pedestrian zones** [V]: Kathmandu (Basantapur) Durbar Square, Patan Durbar Square, Bhaktapur Durbar and Taumadhi squares, and the temple compounds (now enterable, CONTENT_COVERAGE L16) are walk and cycle only, whatever their width. Use OSM `highway=pedestrian`, the RELIGIOUS AreaKind and the HERITAGE_SQUARE POIs. Bhaktapur's core restricts private vehicles; confirm the current rule.
* `access=no` / `motor_vehicle=no` in OSM always wins.
* Buses are also limited by turning: no bus on any way where a hairpin's apex radius is < 12 m after arc smoothing (CONTENT_COVERAGE R1: F24 has a 3.6 m apex) [E].
* A friendly prompt ("the bus can't fit here") already exists in ARCHITECTURE §7.6.

### 9.5 Transitions and curves

* Width changes along a way taper at **1:20** (the NRS median transition) for Δw ≥ 0.5 m, with a minimum taper length of 10 m. Small width jitter from the corridor clamp (< 0.5 m) is smoothed with a 30 m moving minimum, so the road never "breathes" [S/E].
* Curve widening per NURS Table 10, scaled by S: two-lane +1.5 m at R ≤ 40 m, +1.2 at 41–60, +0.9 at 61–100, +0.6 at 101–300; single lane +0.9 at R ≤ 20, +0.6 at 21–60, applied on the inside of the curve [S].
* At junctions the cap polygon uses the **game** widths. Corner kerb radius: 6 m where both roads are ≥ 6.5 m, 3 m otherwise, and 0.5 m in OLD_CORE (sharp brick corners) [E].
* A dual carriageway (two one-way ways with opposite directions, the same `ref`, centrelines < 15 m apart) is meshed as **one road with a median**. The median width = centreline spacing − the two half-widths, never < 1.0 m (ring S ≈ 1.0 m, Araniko ≈ 1–4 m) [O/E]. Widening grows the carriageways **outwards** (away from the median), so the median does not disappear.

---

## 10. Markings, crossings, kerbs and road kit (numbers for the generators)

### 10.1 Longitudinal markings (DoR Road Safety Note 5 Table 1) [S]

| Marking | Colour | Width | Mark / gap (urban) | Mark / gap (rural) | Use |
|---|---|---|---|---|---|
| Centre or lane line, permissive (broken) | White | 100 mm | 1.5 m / 4.5 m | 2.0 m / 7.0 m | Normal two-lane and multi-lane roads ≥ 5.5 m |
| Centre or lane line, warning | White | 100 mm | 4.0 m / 2.0 m | 6.0 m / 3.0 m | Approaching hazards and junctions |
| Centre line, no overtaking (continuous) | White | 100 mm urban / **150 mm** rural | continuous | continuous | Curves, crests, bridges |
| Edge line, permissive | **Yellow** | 150 mm | – | 1.0 m / 2.0 m | 100 mm in from the pavement edge |
| Edge line, no parking (continuous) | **Yellow** | 150 mm | continuous | continuous | Bends, bridges and approaches |
| Road studs (if used) | Retro-reflective | – | 12 m (permissive), 6 m (warning), 4 m (no overtaking) | 18 / 9 / 6 m | Night delineation |

* Centre lines are only painted on roads **≥ 5.5 m** (no centre line on single and intermediate lane roads) [S: RSN5]. In game terms: paint a centre line when the **real** width is ≥ 5.5 m and `surface` is ASPHALT or CONCRETE, and lane lines when `lanes` ≥ 3 or on a dual carriageway.
* Paint wears: urban paint is re-applied every 6 months and rural every 1–1.25 years [S]. Show faded, patchy lines on PERI_URBAN, RURAL and HILL roads (decal alpha 0.4–0.7) and crisp lines on trunk and primary URBAN [E].
* The Chinese-built Ring Road uses white lane lines and a yellow line at the median edge in photos [V]. Keep the RSN5 white centre / yellow edge scheme everywhere unless a reviewer says otherwise.
* Paint in the game is scaled with S for **widths and spacings across** the road, not along it: mark and gap lengths stay real.

### 10.2 Transverse markings and crossings

| Item | Value | Source |
|---|---|---|
| Stop line | Solid white, **200 mm** urban, 300 mm rural | IRC:35-2015 §6.1.3 [S] |
| Stop line set-back | 2–3 m before an unsignalised zebra; at signals the zebra is 1 m ahead of the signal and the stop line another 1 m ahead of it | IRC:35-2015 §11.3.2 [S] |
| Zebra crossing depth (across the stripes) | Never less than the footpath width, minimum **2.0 m**, usually **2–4 m** | IRC:35-2015 §11.3.5 [S]; NURS 2076 §4.11 (≥ 2 m) [S] |
| Zebra stripe | White bars **500 mm** wide with **500 mm** gaps, parallel to the road centreline, across the full carriageway [E: IRC block-marking convention (Table A.4 drawing, not read as text); verify against Nepali practice] |
| Refuge island | ≥ **1.2 m** wide when the crossing is longer than 10.5 m | IRC:35 §11.3.3; NURS §4.5 [S] |
| Zebra night studs | Two rows of yellow studs at 0.5 m spacing | IRC:35 §5.5.7 [S] |
| Raised crossing / table | At footpath level, **150–200 mm**, vehicle ramps ≥ 1:4 | NURS §4.11 [S] |
| Kerb at a crossing | ≤ **50 mm** (wheelchair friendly) | IRC:35 §11.3.3 [S] |
| Crossing spacing | Every 150–200 m on busy urban roads, and at all junctions | NURS §4.11 [S] |

Placement in the game [E]: real `highway=crossing` nodes (617) get a zebra if `crossing` is `zebra`, `marked` or `traffic_signals`, or if `crossing:markings` is `yes` or `zebra`. Otherwise generate zebras at every signalised or police-controlled chowk and every 200 m on URBAN trunk, primary and secondary roads, and near schools (street_life SCHOOL overlay). In OLD_CORE, no zebras: the streets are shared.

### 10.3 Kerbs, humps and islands

| Item | Value | Source |
|---|---|---|
| Kerb height, barrier (urban footpaths) | **150 mm** (range 100–200 mm) | NRS 2070 §13.6; NURS §4.3 (+150 mm footpath) [S] |
| Kerb height, mountable (medians, islands) | 100 mm, sloped face ≈ 45° [E within the NRS 100–200 range] | NRS 2070 §13.6 [S/E] |
| Kerb section | 150 mm wide top, 300 mm deep, concrete grey `#BDB8AE`; game piece `ghm_rd_kerb_*` 1/2/4 m [E] | |
| Median and island kerb paint | Alternate vertical **black and white bands 500 mm** wide (Kathmandu often uses **yellow and black** [V]) | IRC:35 §14.2.4 [S] |
| Speed hump | Width (along the road) ≥ **3.7 m**, height **0.10 m**, parabolic; **200 mm** black and white stripes at 45°; urban class IV roads only | NRS 2070 §13.2 [S] |
| Speed hump placement [E] | Residential and unclassified URBAN roads near schools, hospitals and temple gates, ≈ 1 per 300 m; never on trunk; checkered-paint approach warnings 15 m before | |
| Post delineators (hill roads) | 1.0 m above ground, 600 mm outside the shoulder edge; spacing 3 m at R < 30 m, 5 m at R 30 m, 8 m at R 50 m; alternate posts carry reflectors | RSN5 [S] |
| Guardrail trigger | Drop > 3 m or side slope steeper than 1:4, and the outside of sharp curves | NRS 2070 §13.1 [S] |
| Footpath cross-fall | 2.5–3% towards the road | NURS §4.3 [S] |
| Lamp poles | Arterial 10–12 m every 30–35 m; local 9–10 m every 25–30 m; footpath 4–6 m every 12–18 m | NURS Table 14 [S] |
| Bus bay | 15 m recess per bus, 3.0 m deep, tapers 1:8 | NURS §4.8 [S] |

### 10.4 Colours (cartoon palette targets, sRGB) [E]

| Surface or paint | Hex |
|---|---|
| Fresh asphalt (Ring Road, Araniko) | `#4A4D52` |
| Worn asphalt (most roads) | `#6B6A66` |
| Concrete road (Tokha, hill sections) | `#A9A59C` |
| Brick paving (Bhaktapur, Patan squares, herringbone) | `#A4553A`, joints `#7E3F2B` |
| Stone slabs (Durbar squares) | `#8E8A80` |
| Footpath tiles (red/grey interlocking pavers) | `#B5655A` / `#9C9A94` |
| Kerb concrete | `#BDB8AE` |
| White paint | `#F2F0E8` (worn alpha 0.5) |
| Yellow paint | `#F2C230` |
| Black hump and kerb stripes | `#202124` |
| Open drain water | `#5E6B4F` (murky green) |

---

## 11. What the pipeline and runtime need (recommendations; no code changed here)

| Item | Where | Why |
|---|---|---|
| Keep the tagged `width_cm` and `lanes` (already in `ROAD`) and treat them as **total** for two-way ways and **per carriageway** for one-way ways | DATA_FORMATS §1.4 | Matches how the valley is mapped (§4, §6) |
| Add a per-road `area_type` (u8: OLD_CORE, URBAN, PERI_URBAN, RURAL, HILL) so the runtime can pick the §8 default | pipeline (street_life §1 classifier + HILL rule) | Width defaults depend on area as much as on class |
| Add `corridor_cm` (min over the way of `2 × min(dLeft, dRight)`, or a small per-segment array) | pipeline (§1.3 method, 20 m sampling, about 0.7 M rays for the valley; a few minutes in shapely) | Allows the generous rule without cutting buildings |
| Carry `sidewalk` (none/left/right/both/separate), `footway=sidewalk` lines, `crossing` + `crossing:markings`, `junction=roundabout/circular`, `traffic_signals`, `access`/`motor_vehicle` | `ROAD` flags + the planned `PROP` chunk (CONTENT_COVERAGE D3) | §8.2, §10 placement |
| Pair dual carriageways (same `ref`/name, opposite direction, < 15 m apart) and store the partner way id | pipeline | One mesh with a median (§9.5) instead of two ribbons with a gap |
| Replace `RoadStyle.DefaultWidthM` with the §8.1 table (class × area type), then apply §9.1 | `game/…/Core/Meshing/RoadMesher.cs` (do not edit in this workflow) | Current defaults: motorway 12, trunk 10, primary 8, secondary 7, tertiary 6, unclassified/residential/road 5, living/pedestrian/service/track 4, trails 2. These are close for URBAN but too wide for HILL (tertiary 4.5 real) and OLD_CORE (residential 4 real), and too narrow for a two-way 4-lane trunk (14 real) |
| Travel masks by final width (§9.4) | routing profiles (`routing.py`, `Travel`) | So the bus route planner and AI agree with the mesh |

---

## 12. Caveats and open questions

* **[V]** Ring Road median and separator widths (§6.1) are derived from mapped centrelines, not measured on the ground. Check them against current imagery or street photos.
* **[V]** Durbar Marg and Kanti Path medians and tree strips: confirm the widths before hero dressing.
* **[V]** The Bhaktapur core vehicle restriction and the Patan and Kathmandu Durbar Square pedestrian rules: confirm the current municipal rules.
* **[V]** The zebra stripe module (500/500 mm) and the yellow-black kerb painting: confirm the local practice. The Nepali Traffic Signs Manual Vol. II drawings were not obtained as text.
* OSM building outlines in Bhaktapur, Patan, Thimi and Kirtipur are incomplete (street_life §1.3), so the corridors there are too wide and the clamp too generous. Treat OLD_CORE as "keep real" (`limit = real`) when building coverage in the cell is < 0.3.
* The HILL rule (slope > 12% or > 1,650 m on 250 m cells of a 30 m DSM) is coarse; ARCHITECTURE §6.3 road profiles will give a better terrain class later.
* Mapper widths sometimes include shoulders or verges (a "14 m tertiary") and sometimes only one carriageway; the clamp in §9.1 bounds the damage.

---

## 13. Sources

Standards and official documents:
* Nepal Road Standard 2070 (DoR / MoPIT), PDF: https://rcip-af.gandaki.gov.np/assets/225/Nepal_Road_Standard_2070_.pdf/file (also https://dor.gov.np/home/publication/general-documents/force/nepal-road-standard-2-7). Tables 3-1, 4-1, 5-1, 11-1, 11-2, 11-3, 11-6, 13-1; §§ 11.3, 13.2–13.7.
* Nepal Urban Road Standard 2076 (Ministry of Urban Development), PDF: https://download.hermes.com.np/wp-content/uploads/sites/12/2023/07/Urban-Road-standard-2076.pdf. Tables 5, 10, 13, 14, 16, 17, 18, 19, 20; §§ 3.11, 4.3–4.11.
* DoR Road Safety Notes 5, *Delineation Measures* (Traffic Engineering and Safety Unit, March 1996), Table 1 (centre, lane and edge lines), Table 3 (post delineators): https://dor.gov.np/home/publication/traffic-safety/force/delineation-measures
* IRC:35-2015 *Code of Practice for Road Markings* (Indian Roads Congress, used by Nepali designers as a reference): https://law.resource.org/pub/in/bis/irc/irc.gov.in.035.2015.pdf. §§ 5.5.7, 6.1.3–6.1.4, 11.3, 14.2.4.
* DoR Traffic Signs Manual Vol. I (referenced by NRS/NURS for markings): https://dor.gov.np/home/guideline/force/traffic-signs-manual-volume-1
* OSM Nepal road tagging guideline: https://wiki.openstreetmap.org/wiki/Nepal/Roads

News and project sources:
* Kalanki–Koteshwor eight lanes, 62 m (2011): https://www.sharesansar.com/newsdetail/kalanki-koteshwar-to-turn-into-an-eight-lane-road
* Ring Road phase 2 (60 m, 8 lanes, 6 m pedestrian lanes; phase 1 handed over 2018): https://thehimalayantimes.com/business/second-phase-of-ring-road-expansion-from-january
* Ring Road phase 2 survey (May 2025): https://ekantipur.com/business/2025/05/04/en/the-survey-of-the-ring-road-second-section-was-started-40-40.html
* Ring Road phase 2 implementation agreement (April 2026): https://ekantipur.com/business/2026/04/30/en/implementation-agreement-signed-for-expansion-of-second-section-of-ring-road-32-30.html
* Ring Road (Kathmandu), 27 km, 62 m right of way, 9.5 km widened in 2018: https://en.wikipedia.org/wiki/Ring_Road_(Kathmandu)
* Municipal minimum widths 4 m / 6 m (2015): https://kathmandupost.com/miscellaneous/2015/07/06/at-least-6m-proposed-width-of-city-roads
* Tripureshwor–Kalanki–Nagdhunga 25 m right of way: https://thehimalayantimes.com/business/tripureshwor-kalanki-nagdhunga-road-widening-to-begin
* Valley road widths (Chabahil–Jorpati–Sankhu 22 m; DoR class breadths 50 / 62 / 22 / 14 m): https://www.sharesansar.com/newsdetail/road-expansion-may-conclude-in-2-months and https://worldhighways.com/wh12/news/nepals-widened-kathmandu-road-reducing-traffic-jams
* Araniko Highway six lanes, 2.5 m footpaths, service lanes: https://english.ratopati.com/story/51117/article-expansion-plan-for-suryabinayak-dhulikhel-road and https://ekantipur.com/business/2025/11/23/en/suryabinayak-dhulikhel-road-construction-progress-50-percent-minister-ghisingh-directs-to-expedite-work-39-17.html
* Koteshwor–Jadibuti six-lane section ≈ 44 m (JICA): https://openjicareport.jica.go.jp/pdf/12382388_02.pdf

Data:
* © OpenStreetMap contributors (ODbL), the `nepal.osm.pbf` snapshot in `pipeline/data/raw/osm/`.
* Copernicus GLO-30 DEM (see the pack manifest attribution).
