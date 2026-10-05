# W2 research: aircraft over the Kathmandu Valley (TIA traffic, flight paths, cartoon aircraft, sight and sound)

> Status: research input for Wave 2 "Kathmandu comes alive", 2026-10-05. Feeds a new ambient-aviation system (proposed `Ghumante.Traffic/Air` or a sub-folder of `Ghumante.World`, ARCHITECTURE §7.1/§7.7), the TIA landmark (`ghm_lmk_tribhuvan_airport`, ASSET_MANIFEST §5, CONTENT_COVERAGE S11) and the aircraft rows of ASSET_MANIFEST §8. Audio synthesis recipes live in [audio.md](audio.md) §2.11 and §6; this file adds the aviation-side numbers (what flies, where, when, how high, how loud, how lit).
>
> Confidence tags (same scheme as the other W2 files):
>
> | Tag | Meaning |
> |---|---|
> | **[S]** | Stated by a cited source (URL in the row or in §13) |
> | **[D]** | Derived by us from cited data (OSM, Copernicus DEM, chart values) with the computation shown or reproducible |
> | **[E]** | Estimate or design value. Good enough for a cartoon world; tune in play |
> | **[V]** | Needs a human check before it ships |
>
> **Not for navigation.** The procedure geometry below comes from publicly posted, superseded Jeppesen chart copies (2007–2012) and press reports. It is used only to make ambient game traffic look right. The current CAAN eAIP (e-aip.caanepal.gov.np) was not reachable from this environment (TLS chain failure and HTTP 503), so the current procedure names and altitudes are **[V]**.
>
> **Branding rule (ASSET_MANIFEST "Rules for every asset", LICENSES.md):** no airline names, logos, liveries, colour schemes recognisable as a carrier, or real registrations. Airline names appear in this file only as research facts about the traffic mix.

---

## 0. Ten-line summary

1. TIA (VNKT/KTM) has **one runway, 02/20, 45 m wide**, paved end to end **3,326 m** in OSM (3,350 m published) with **displaced thresholds ~280 m in at both ends**, so ~2,764 m between thresholds; threshold elevations ≈ **1,315 m (02)** and **1,338 m (20)**; true heading **022°/202°** (magnetic variation ≈ 0°) [S][D].
2. **No ILS.** Arrivals almost always use **runway 02 from the south**: STAR ROMEO → RATAN → GURAS (D17, 11,500 ft) down the KTM VOR **R-202 / 022° inbound course**, over the **Mahabharat ridge at SIERRA (D10, 9,500 ft, ~650 m above the crest at Bhattedanda)**, a steep **5.5° segment D9→D3**, then **3.0°** over Thecho, Sunakothi and Koteshwor to the threshold [S][D]. RNP AR (2.8°, curved RF legs) exists for some operators [S].
3. Secondary IFR arrivals: **VOR/DME-A from the west** (DARKE, R-288, 3.6° over Thankot–Kalanki–Teku) and **VOR/DME-B from the east** (IGRIS, R-105, over Banepa–Bhaktapur–Thimi), both ending in a **visual circle in the south-east quadrant** to runway 02 [S][D]. Holding fixes are **DARKE** (west of Chandragiri) and **IGRIS** (near Panauti), MHA 10,500 ft, max 13,500 ft [S].
4. Departures climb at ≥ 6% and **circle inside the valley on 4–5 DME arcs (7.4–9.3 km radius around the VOR)** before leaving west (DARKE) or east (IGRIS): departing aircraft sweep over Boudha, Maharajgunj, Swayambhu and back over the city, which is the most visible aviation spectacle for a player [S][D].
5. Traffic: **138,145 movements in 2025** (≈ 378/day), ≈ 25% international, peak hour > 30 movements; **open 06:00–24:00** since 1 April 2025 [S]. §4 gives an hour-by-hour generator table (domestic turboprops, helicopters, international narrow- and wide-bodies).
6. Fleet mix: domestic = ATR 72-style high-wing twin turboprops (Buddha Air ~16 ATR 72 + 2 ATR 42, Yeti ATR 72-500, Shree Dash 8-400 and CRJ) plus STOL Twin Otter/Dornier/Let types and a large AS350/H125 helicopter fleet; international = A320/737 families, occasional A330/787-class wide-bodies [S]. Five generic cartoon models cover it (§7).
7. Aprons from OSM: **international apron w736369689 = 10.7 ha (699 × 189 m)** in front of the international terminal; **domestic apron w736369694 = 3.0 ha** with stands D1–D17; maintenance/military aprons; a new 11.6 ha north apron w1368706500. Taxiways are 23 m wide [S][D].
8. Flight paths are given as **WGS84 polylines with altitudes in metres MSL** and ground height from the project's Copernicus DSM (§5), ready to convert with NPL-TM84 into game coordinates.
9. Seeing and hearing: arrivals pass **250–300 m above Thecho/Satdobato** and ~2 km east of Patan Durbar Square; departures pass **~200–350 m over the Boudha–Jorpati area**; at night, landing lights show as a slow bright "star" over the southern ridge for ~4 minutes before each arrival. Jets are audible to ~6–9 km, turboprops to ~4–6 km, helicopters to ~3–4 km (§8–§10).
10. Budget: real-time rates (not game-clock-compressed) give **≈ 3–5 airborne aircraft at peak** inside 30 km; all fly on splines with no physics; LOD0 only within ~1.5 km, a lit sprite beyond ~8 km (§11).

---

## 1. Tribhuvan International Airport: facts

| Item | Value | Tag / source |
|---|---|---|
| ICAO / IATA | VNKT / KTM | [S] OSM w118505122 tags; Wikipedia |
| Aerodrome reference point | N27°41.8′ E085°21.5′ (27.6967, 85.3583) | [S] Jeppesen 10-9 (2011) |
| Aerodrome elevation | 4,390 ft (2007) / **4,395 ft (2012) = 1,340 m**; OSM `ele=1339.5` | [S] Jeppesen; OSM |
| Aerodrome area (OSM) | 2.66 km², 4.1 km along the runway axis × 1.4 km across | [D] w118505122 |
| Runway | **02/20**, asphalt concrete, **45 m wide** (OSM `width=45`, Jeppesen 150 ft = 46 m) | [S] |
| Runway length | 3,350 m published (Wikipedia); 3,078 m × 45 m (SkyVector, older); OSM `length=3074` on the centre section | [S] |
| Paved length in OSM | **3,326 m**: south displaced section w1453500827 285 m + w340948564 2,764 m + north displaced section w1453500826 277 m | [D] |
| True heading | **022.0° / 202.0°** (OSM bearing 21.96°); magnetic variation 0° (chart "VAR 0°") | [D][S] |
| Threshold 02 (landing, end of displaced section) | **85.3534132, 27.6839528** (OSM node shared by w1453500827/w340948564); DSM ground 1,313 m; Jeppesen THR/TDZE 4,313–4,318 ft (1,315–1,316 m) | [D][S] |
| Threshold 20 (landing) | **85.3639103, 27.7070042**; DSM 1,337 m; TDZE 4,387 ft (1,337 m) | [D][S] |
| Pavement end south / north | 85.3523298, 27.6815732 / 85.3649612, 27.7093117 | [D] |
| SkyVector threshold coordinates (cross-check) | 02: N27°41.03′ E085°21.20′; 20: N27°42.43′ E085°21.84′; displaced threshold 02 = 984 ft (300 m) | [S] skyvector.com/airport/VNKT; agree with OSM to 30–260 m |
| Gradient | ~0.8% up toward the north from the threshold elevations (Wikipedia states 1.2%) | [D][S] |
| Runway lighting | HIRL edge lights; centreline, threshold and end lights; **HIALS 870 m extended centreline approach lights on 02**; **PAPI-L 3.0°** on both ends | [S] Jeppesen 10-9; Wikipedia |
| Navaids | **KTM VOR/DME 113.2 MHz** at N27°40.4′ E085°20.9′ (85.3483, 27.6733): **1.28 km before threshold 02 on the extended centreline** (bearing to THR 023°); **KTM NDB 318 kHz** (OSM n4723585277, 85.3537, 27.6937, west of the runway) | [S] charts, OSM; [D] |
| ATC | ATIS 127.0, Approach 120.6/125.1, Tower 118.1, Ground 121.9 MHz (2012 values) | [S] Jeppesen |
| Transition | TA 13,500 ft, TL FL150 | [S] |
| Operating hours | **06:00–24:00 local (18 h) since 1 Apr 2025**; 22:00–08:00 closure Nov 2024–Mar 2025 for works; 21–22 h a day before that | [S] Himalpress, Crisis24, Himalayan Times |
| Annual traffic | 2023: 108,202 movements, 8.69 M pax; 2024: 135,977 movements, 9.48 M pax; **2025: 138,145 movements, 9.78 M pax** (5.25 M international, 4.53 M domestic) | [S] Wikipedia; Ekantipur 14 Jan 2026 |
| International share | 34,594 international flights in 2025 (≈ 25%); the domestic figure printed in coverage (135,551) cannot be right next to the total; we use 138,145 − 34,594 = **103,551 domestic** | [S] Ekantipur via search; [D] |
| Busiest month | November (Nov 2024: ≈ 12,525 movements ≈ 417/day) | [S] Onlinekhabar |
| Runway capacity | "current runway capacity 33 aircraft per hour"; peak-hour movements > 50 reported; pre-COVID ~400/day, exceeded the stated 35/h at peaks | [S] ADB/CAAN documents via search (ewsdata ADB-38349-025) |
| Aprons | International apron 17 stands (3 wide-body); domestic apron design 17, actually holds ~35 aircraft | [S] Wikipedia |
| Taxiways | Parallel taxiway (F/G) on the **west** side does **not** reach the thresholds, so aircraft **backtrack on the runway** | [S] Wikipedia; OSM taxiways F/G stop short of both thresholds |

### 1.1 Airside layout from OSM (for the landmark and for taxi animation)

All features lie **west of the runway** except one small apron on the east side.

| OSM | Feature | Centroid (lon, lat) | Size | Notes |
|---|---|---|---|---|
| w327224048 | International Terminal (`height=5`, brown walls) | 85.35675, 27.69904 | 16,977 m² footprint, 212 × 102 m | Brick-clad; real height ~15–20 m [E][V] |
| w327212841 | Arrivals hall | 85.35679, 27.69844 | 490 m² | |
| w340948572 | ATC tower (concrete, `building:part`) | 85.35697, 27.70028 | 8 × 8 m footprint | Height not tagged; ~30–35 m [E][V] |
| w185408474 | Domestic Terminal | 85.35918, 27.70237 | 6,675 m², 102 × 138 m | |
| **w736369689** | **International apron** | 85.35768, 27.69880 | **107,085 m² (10.7 ha), 699 m along × 189 m across** | Gates 1–11 (OSM nodes `aeroway=gate` ref 1–11, plus "Departure A") along the terminal face |
| **w736369694** | **Domestic apron** | 85.35986, 27.70579 | **30,303 m² (3.0 ha), 244 × 253 m** | Stands **D1–D7, D15–D17** mapped as `parking_position` lines (36–63 m long) |
| w340948578 | Maintenance apron (military-tagged) | 85.35967, 27.70368 | 10,487 m² | |
| w220704670 | Military apron | 85.35914, 27.70448 | 5,677 m² | Static only; no military aircraft shown [E] |
| w912639950 | Apron east of the runway | 85.36330, 27.69897 | 14,309 m² | Near the army gate; VIP/government use [V] |
| w1368706500 | New north apron (tagged `building=yes` too) | 85.36098, 27.70903 | 115,783 m² (11.6 ha), 436 × 268 m | CONTENT_COVERAGE S11 flags the wrong building; treat as apron, not a HILL_VILLAGE block |
| taxiways | 23 m wide (`width=23`), refs A–H; covered taxiway tunnels w1554926421/-423 at the north apron | — | — | Taxi graph for parked/taxiing aircraft |
| n8489800229, n8489800230 | Windsocks | 85.36132, 27.69922; 85.36501, 27.70741 | — | `ghm_prp_windsock` |
| n10121417304 | Helipad (charter operator) | 85.34595, 27.68408 | — | Outside the fence on the west; helicopters also use the domestic apron |

Public viewpoints in OSM: "Airport View Restaurant" (n10121362086, 85.34924, 27.67952, south-west of threshold 02) and "Kathmandu Airport View Hotel" (w1280228733, 85.36573, 27.70103, east side). ADR-010 keeps business names off the map; the places themselves are good spots to watch arrivals.

---

## 2. Terrain and the shape of every procedure

The valley floor sits at 1,300–1,340 m. Ridges rise to 2,500–2,800 m on every side: Chandragiri (2,551 m) west, Shivapuri (2,732 m) north, Phulchoki (2,782 m) south-east, and the Mahabharat crest at **Bhattedanda (2,524 m)** right under the southern approach [S] (PIA 268 report). Copernicus DSM along the final approach course confirms the ridge: ground 2,062–2,248 m around D10–D9 (8.5–10 km south of the valley rim) [D].

Consequences that make TIA visually distinctive (and that the game should keep):

* Jets come in **steep and high**: 9,500 ft (2,896 m) over the ridge, then 5.5° (almost twice the normal 3°) down to D3, then a normal 3° over the southern suburbs [S].
* With no ILS and only non-precision approaches, **circling** in the south-east quadrant is routine for arrivals from west and east, so aircraft are often seen turning low over Thimi, Imadol, Lubhu and Satdobato [S][D].
* Departures **must gain height inside the valley** before crossing the ridges, so they fly loops of 7–9 km radius over the city [S].
* Holding happens over **DARKE** (west, beyond Chandragiri) and **IGRIS** (east, beyond Bhaktapur), and in congestion far outside the valley over Hetauda/Simara. Press reports describe **30 minutes of holding as normal** in busy periods and diversions after an hour [S] (Kathmandu Post 2017).

---

## 3. Published procedures (historical charts, for shape only)

Distances are DME from the **KTM VOR** (85.3483, 27.6733). 1 NM = 1,852 m; 1 ft = 0.3048 m.

### 3.1 Arrivals to runway 02 from the south (the main flow)

| Fix | Position | Altitude | Source |
|---|---|---|---|
| ROMEO | N27°02.8′–03.2′ E085°04.1′–04.7′ (≈ 85.068, 27.053), 41 DME | FL150 (15,000 ft, 4,572 m) | [S] Jeppesen 10-2A; PIA 268 |
| RATAN | N27°18.1′ E085°10.8′ (85.180, 27.302), on KTM R-202 | 13,500 ft assumed | [S] position; altitude [E] |
| GURAS (IAF, D17) | N27°24.6′ E085°13.8′ (85.230, 27.410) | 11,500 ft until established on 022° | [S] 2012 chart (2009 chart names this point NOPEN) |
| SIERRA (D10) | 85.278, 27.519 | 9,500 ft (2,896 m) | [S] PIA 268 |
| Recommended DME altitudes | D9 9,000 / D8 8,400 / D7 7,850 / D6 7,250 / D5 6,650 / D4 6,100 / D3 5,500 / D2 5,200 ft | — | [S] Jeppesen 13-1 |
| Descent angles | **5.50° D9→D3**, **3.00° D3→threshold** | — | [S] |
| MDA (straight-in) | 4,950 ft (632 ft above threshold) | — | [S] |
| Missed approach | Straight ahead to 5,500 ft, right turn (max 185 kt) to the VOR at ≥ 7,500 ft, R-288 to DARKE ≥ 10,500 ft and hold | — | [S] |
| Simara feed | From SMR VOR (N27°09.9′ E084°58.9′) on R-052/054 to RATAN | — | [S] |

**RNP AR 02** (2012, Quovadis/NAVBLUE): curved approach with **radius-to-fix turns in the final segment** "around the mountains" and a **2.8° glide path** instead of the VOR 5.3–5.5°; used by a limited number of international operators. **RNP AR 20** and RNP AR departures were designed in 2021–2022 [S] (Airbus press release 2022, NAVBLUE). Exact waypoints are not public in our sources [V]; §5.2 models it as a curved variant.

### 3.2 Arrivals from the west and east (circling)

| Procedure | Inbound | Profile | Minimums | Circling area |
|---|---|---|---|---|
| **VOR/DME-A** (CAT A/B) | DARKE / D16 R-288 → **108°** to the VOR; 10,500 ft until established | 3.6°: D10 8,640, D9 8,260, D8 7,880, D7 7,500, D6 7,120, D5 6,740, D4 6,360, D3 5,980, D2 5,600 ft | MDA 5,600 ft (1,282 ft above THR) | Visual manoeuvre at ≥ 5,600 ft **within D2.5 between R-105 and R-202** (south-east quadrant), D3.0 elsewhere; landing 02. Runway 20 not authorised |
| **VOR/DME-B** (CAT A/B/C) | IGRIS / D15 R-105 → **285°**; 9,500 ft at D10 | Not recovered from the text | MDA 5,600 ft (C: 5,800) | Same visual area; "to be used when there is no traffic from GURAS" |
| STARs from Bharatpur / Pokhara (2009) | Airway B345 on R-270, or W41 on R-293, to D25; then to D16; intercept R-290 | 10,500 ft | — | "DHARKE" circling |
| Arrivals from the north-west | From BIDUR on 121° to GAJUR, intercept R-288 inbound for VOR/DME-A | — | — | — |

[S] Jeppesen 10-2/10-2A (2012), 13-2, 13-3. DARKE = R-288 D13.5 = (85.107, 27.743); IGRIS = R-105 D15 = (85.621, 27.608) [D].

### 3.3 Holding

| Fix | Inbound course | MHA | Max | Typical use |
|---|---|---|---|---|
| DARKE (R-288 D13.5) | 108° | 10,500 ft (3,200 m) | 13,500 ft (4,115 m) | Missed approaches, west arrivals |
| IGRIS (R-105 D15) | 285° | 10,500 ft | 13,500 ft | DME-B missed approach, east arrivals |
| Outside the valley | Hetauda / Simara area | — | — | Congestion, VVIP movements [S] Kathmandu Post 2016 |

Holding geometry for the game [E]: racetrack with 1-minute legs at 180 kt TAS (5.6 km legs), standard-rate turns (3°/s → turn radius ≈ 1.9 km), so a racetrack is ≈ 5.6 × 3.8 km. Stack up to 3 aircraft at 1,000 ft (305 m) spacing starting at MHA.

### 3.4 Departures (SIDs, 2007–2012)

All SIDs require a **minimum climb gradient of 6% (365 ft/NM)**, with turns limited to **180 kt** [S].

| SID | Runway | Routing (abridged) | Exit |
|---|---|---|---|
| DARKE 1A | 20 | Straight ahead to KTM VOR, turn RIGHT within 4 DME, at R-270 turn LEFT, R-288 to DARKE | West |
| DARKE 1B | 20 | Straight ahead to KTM, RIGHT along the 4 DME arc, after R-040 turn RIGHT to KTM, R-288 to DARKE | West |
| DARKE 1C | 02 | Straight ahead to D2.5, RIGHT to KTM, R-288 | West |
| DARKE 1D | 02 | Straight ahead to D3, LEFT along the 4 DME arc, at R-310 LEFT to KTM, R-038 to D2.2, LEFT along the 4 DME arc, at R-310 RIGHT, R-288 to DARKE | West |
| IGRIS 1A | 20 | Straight ahead to KTM, turn RIGHT within 4 DME, at R-084 turn LEFT, R-105 to IGRIS | East |
| IGRIS 1B | 02 | Straight ahead to D2.5, LEFT within 5 DME, at R-320 LEFT to KTM, R-105 to IGRIS | East |

Onward routes: airway B345 west to Bharatpur, G336/G348 south to Simara/India, L626 west, east toward Biratnagar [S] (chart text).

### 3.5 Runway in use

* Runway **02 is the default** and the only straight-in instrument runway for most jets; "international flights have to land only from Zero Two" [S] (Nepali Times, "Why do planes go around").
* Afternoons bring **strong south/south-westerly winds, cloud build-up and turbulence**; heavy aircraft cannot land with **> 12 kt tailwind**, causing go-arounds and diversions in pre-monsoon months [S] (Nepali Times; Himalpress "Strong tailwinds disrupt flights at TIA"; Ekantipur May 2025).
* Domestic turboprops land visually on 20 when the wind favours it; departures can use either end [E][V].

Game rule [E]: `runwayInUse = 02` from 06:00 to 12:00; from 12:00 to 18:00 pick 20 for **domestic visual arrivals and all departures** with probability 0.35 in March–May and 0.15 otherwise; jets always land 02. Afternoon go-around probability 2% per jet arrival in March–May, 0.5% otherwise (a visible, harmless event; real rates are lower).

---

## 4. Schedule generator

### 4.1 Real totals to calibrate against

| Quantity | Value | Tag |
|---|---|---|
| Average day (2025) | 138,145 / 365 = **378 movements/day** (1 movement = 1 take-off or 1 landing) | [D] from [S] |
| International | 34,594 / 365 = **95/day** (≈ 47 arrivals + 47 departures) | [D] |
| Domestic incl. helicopters | 103,551 / 365 = **284/day** | [D] |
| Busiest month | November: ≈ 417/day (Nov 2024) → seasonal factor 1.10 | [S][D] |
| Peak hour | 33/h stated capacity; > 50/h reported at peaks | [S] |
| Operating window | 06:00–24:00 local; closed 00:00–06:00 | [S] |
| Helicopter share of domestic | not published in our sources; **≈ 50/day assumed** (H125 fleet ≥ 20 machines delivered since 2012 across ≥ 9 operators; Lukla/Everest helicopters fly year-round from Kathmandu) | [E]; [S] Vertical Mag, Airbus/HeliHub via search |
| International weekly frequencies (examples) | Qatar Airways 14/week to Doha, flydubai ~4 daily, IndiGo 14/week to Delhi, Air India several daily, Himalaya and Nepal Airlines daily Gulf flights | [S] search summaries of carrier pages (§13) [V exact 2026 timetable] |

### 4.2 Movements per hour by time of day (average day, both directions)

Each class splits 50/50 into arrivals and departures except where noted. The shape follows how Nepal's domestic network works (dawn mountain flights, VFR-only outstations that close at dusk) and the international pattern (midday Indian and Gulf rotations, evening Gulf departures); the totals match §4.1. **All cells are [E]** shaped by the [S] totals.

| Local hour | Domestic turboprop / STOL | Helicopter | Intl narrow-body | Intl wide-body | Total | Notes |
|---|---|---|---|---|---|---|
| 00–06 | 0 | 0 | 0 | 0 | **0** | Airport closed; empty, quiet sky |
| 06 | 16 | 6 | 2 | 0 | 24 | Mountain (Everest) flights 06:00–08:00 are departures that return ~60 min later |
| 07 | 22 | 7 | 3 | 0 | 32 | Domestic first wave |
| 08 | 22 | 7 | 4 | 0 | 33 | Peak |
| 09 | 20 | 6 | 5 | 0 | 31 | |
| 10 | 18 | 5 | 6 | 1 | 30 | |
| 11 | 17 | 4 | 7 | 1 | 29 | |
| 12 | 16 | 4 | 7 | 1 | 28 | Afternoon wind begins (§3.5) |
| 13 | 16 | 3 | 7 | 1 | 27 | |
| 14 | 16 | 3 | 6 | 1 | 26 | |
| 15 | 16 | 2 | 6 | 1 | 25 | |
| 16 | 15 | 2 | 5 | 1 | 23 | |
| 17 | 13 | 1 | 5 | 1 | 20 | Domestic outstations close near sunset |
| 18 | 9 | 0 | 5 | 0 | 14 | Only trunk routes (Pokhara, Biratnagar, Bhairahawa, Nepalgunj) after dusk [V] |
| 19 | 4 | 0 | 5 | 0 | 9 | |
| 20 | 0 | 0 | 5 | 0 | 5 | International only; landing lights over the southern ridge |
| 21 | 0 | 0 | 4 | 0 | 4 | |
| 22 | 0 | 0 | 3 | 0 | 3 | |
| 23 | 0 | 0 | 2 | 0 | 2 | Last movement by 23:59 |
| **Day total** | **240** | **50** | **87** | **7** | **384** | Real 2025 average: 378 |

Helicopters are **≈ 70% departures before 10:00** (outbound charters and rescue) and **≈ 70% arrivals after 12:00**; use those splits instead of 50/50 [E].

### 4.3 Modifiers

| Modifier | Values | Tag |
|---|---|---|
| Month | Oct–Nov 1.10; Mar–May 1.05; Dec–Feb 0.95; Jun–Sep (monsoon) 0.85 | [E] from November being busiest [S] |
| Winter fog (Dec–Jan) | On fog days (probability 0.4 in the weather system), domestic and helicopter rates × 0 until 10:00, then the missed backlog spreads over 10:00–14:00 at ≤ 33/h total; international holds or diverts (do not spawn) | [E]; [S] Ekantipur Mar 2025 low-visibility report, Nepali Times "flights delayed by pollution" |
| Monsoon afternoon storms | 14:00–18:00 domestic × 0.7, helicopters × 0.4 | [E] |
| Pre-monsoon afternoon tailwind | Jet go-around probability 2% (§3.5); after a go-around the aircraft flies the published missed approach to DARKE, holds 1–2 laps, then re-approaches | [E]; procedure [S] |
| Congestion holding | When the arrival queue > 3 aircraft, new arrivals hold at DARKE/IGRIS for 1–4 laps (≈ 4 min each) | [E]; [S] holding is common |
| Device tier | Rates unchanged; only the **visible concurrency cap** changes (§11) | [E] |

### 4.4 The clock problem: real-time rates, game-time profile

The world is 1:1 in space and aircraft fly at real speed, but the day is compressed (1 game day = 48 real minutes, ARCHITECTURE §5.3), so 1 game hour lasts 2 real minutes. Applying "33 movements per game hour" would put a movement every 3.6 real seconds, which is absurd for aircraft that take 7 minutes to fly the approach.

**Rule [E]:** the table in §4.2 is read with the **game hour** to pick the row, but the rate is applied **per real hour**: `spawnsPerRealSecond = rate(gameHour) / 3600 × monthFactor × weatherFactor`. At the 08:00 peak this gives a movement every ~110 real seconds, the real experience of standing near TIA. Because one game hour lasts only 2 real minutes, 1–2 movements happen per game hour; that is the honest cost of time compression. Add a **"busy airport" multiplier** setting (default 1.0, max 2.0) if play-testing wants more.

### 4.5 Algorithm

```
state: queue of scheduled runway events (time, kind, class, procedure)
every real second:
  for each class c in {DOM_TP, HELI, INTL_NB, INTL_WB}:
    for kind in {ARR, DEP}:
      λ = rate[c][gameHour] * split[c][kind] * month * weather / 3600
      if rng(seed, t, c, kind) < λ:
        proc = pickProcedure(c, kind, runwayInUse)          # §4.6 weights
        tRunway = max(now + leadTime(proc), lastRunwayEvent + sep(c, prevClass))
        enqueue(tRunway, kind, c, proc)
  spawn arrivals at tRunway − flightTime(proc, c); spawn departures at their stand (taxi) or on the runway
runway separation sep: 100 s after a turboprop or narrow-body, 120 s after a wide-body,
                       60 s for a helicopter that does not use the runway (domestic apron lift-off)
```

* **Deterministic seed:** `seed = hash(regionSeed, dayIndex, realMinute)`, so tests can replay a day (ARCHITECTURE §7.2 determinism rule).
* `leadTime` for arrivals: from ROMEO ≈ 16 min, from GURAS ≈ 7 min, from DARKE/IGRIS ≈ 6 min (§6.1). Arrivals can be spawned already in flight at the right point of their path, so the sky is populated instantly after a teleport or a load.
* Backtracking: a departure on 02 enters the runway at the mid-field link, backtracks south to the threshold (60–90 s), turns and takes off; this real TIA quirk (no full-length parallel taxiway) is visible from the Ring Road at Sinamangal [S][E].

### 4.6 Procedure weights (which path a flight uses)

| Class | Arrival | Departure |
|---|---|---|
| International (from India, the Gulf, Turkey, China via the south-west) ≈ 85% | A1 straight-in 02 (70%), A2 RNP curved variant (15%) | D1 west from 02 (morning) / D3 west from 20 (afternoon, 20 in use) |
| International (Bangkok, Kuala Lumpur, Dhaka, Kunming; east) ≈ 15% | A4 DME-B from IGRIS (CAT A/B/C) or vectors to GURAS (50/50) | D2 / D4 east |
| Domestic west (Pokhara, Bharatpur, Bhairahawa, Nepalgunj, Dhangadhi, Surkhet) ≈ 45% | A3 DME-A from DARKE | D1 / D3 |
| Domestic east and south (Biratnagar, Bhadrapur, Janakpur, Simara, Tumlingtar, Rajbiraj) ≈ 45% | A4 DME-B from IGRIS (east), A1 from GURAS (Simara, Janakpur) | D2 / D4 |
| Mountain flights ≈ 10% of domestic before 08:00 | Return via A4 after ~60 min | D2 / D4 east, climbing to ~5,000–6,000 m outside the valley [E] |
| Helicopter | Reverse of the departure corridor | Corridors H-E / H-N / H-W (§5.4), 50 / 20 / 30% |

Destinations are [E] from the domestic network on CAAN's domestic-airline page and carrier route maps [S] (§13); split percentages are [E].

---

## 5. Flight paths (WGS84 polylines with altitudes)

How to use: interpolate a Catmull-Rom or centripetal spline through the points; altitudes are **metres above mean sea level** (the game's Y is true elevation, ADR-004). Convert lon/lat with NPL-TM84 then subtract the game origin (ARCHITECTURE §5.1). "ground" is the project's Copernicus DSM (`pipeline/data/raw/dem/Copernicus_DSM_COG_10_N27_00_E085_00_DEM.tif`, buildings and trees included), "AGL" = altitude − ground. Points marked "(est)" are our construction; chart points carry the chart value. On and near the runway the DSM is noisy (±5 m), so the runtime snaps wheels to the runway surface between the thresholds. Generation script: inline in this research pass (radials from the KTM VOR with great-circle destination, NM = 1,852 m) [D].

### 5.1 Checks that the geometry is right [D]

* GURAS computed as KTM R-202 D17 lands at (85.2289, 27.4108); the chart gives (85.2300, 27.4100): **120 m apart**.
* The KTM VOR sits **1.28 km before threshold 02, 1° off the runway axis**, so "DME" ≈ distance to threshold + 0.69 NM, and the chart's 3.0° segment from D3 (5,500 ft) meets the threshold at 50 ft: consistent.
* Final approach clearance over the Mahabharat crest at SIERRA: aircraft 2,896 m, DSM max within 500 m = 2,248 m → **≈ 650 m clearance**; at D9: 2,743 m vs 2,308 m (435 m).
* The final 022° course passes **directly over Thecho** (0.1 km cross-track, D3.3), 1.4 km east of Chapagaun, 1.5 km east of Satdobato, 0.5 km from Koteshwor, and 2.1 km east of Patan Durbar Square.

### 5.2 Arrival paths

A1 is the backbone (most jets and southern domestic arrivals). A2 is our **estimate** of what a curved RNP AR path looks like: the lowest crossing of the southern ridge within 16 km of the VOR is on bearing ~220° (max DSM 1,651 m, versus 2,040–2,250 m on bearings 180–200°), which is the Bagmati gorge below Chobhar, so the variant comes up that gap and joins the 022° final at ~D3 with a 2.8° path; minimum terrain clearance along it is ≈ 350 m [D][E][V]. A3 and A4 end in visual circles inside the D2.5 south-east area; A5 is the domestic visual circuit to runway 20 when it is in use [E].

#### A1 VOR/DME 02 straight-in from the south
| # | lon | lat | alt m MSL | ground m | AGL m | note |
|---|---|---|---|---|---|---|
| 0 | 85.06833 | 27.05333 | 4572 | 100 | 4472 | ROMEO, FL150 (chart) |
| 1 | 85.18000 | 27.30167 | 4115 | 421 | 3694 | RATAN (alt estimate) |
| 2 | 85.23000 | 27.41000 | 3505 | 834 | 2671 | GURAS IAF D17, 11 500 ft (chart) |
| 3 | 85.25691 | 27.47255 | 2987 | 790 | 2197 | D13, 9 800 ft (read from the chart profile [V]) |
| 4 | 85.27798 | 27.51889 | 2896 | 2062 | 834 | SIERRA D10, 9 500 ft (PIA268 report) |
| 5 | 85.28501 | 27.53434 | 2743 | 2229 | 515 | D9 start 5.5 deg segment (chart) |
| 6 | 85.29204 | 27.54978 | 2560 | 1974 | 586 |  |
| 7 | 85.29907 | 27.56523 | 2393 | 1593 | 800 |  |
| 8 | 85.30610 | 27.58067 | 2210 | 1564 | 646 |  |
| 9 | 85.31313 | 27.59612 | 2027 | 1423 | 603 |  |
| 10 | 85.32017 | 27.61156 | 1859 | 1459 | 400 |  |
| 11 | 85.32721 | 27.62700 | 1676 | 1399 | 278 | D3 start 3.0 deg (chart) |
| 12 | 85.33425 | 27.64245 | 1585 | 1309 | 276 |  |
| 13 | 85.34129 | 27.65789 | 1478 | 1304 | 175 | est |
| 14 | 85.34833 | 27.67333 | 1396 | 1306 | 90 | KTM VOR (est on 3 deg) |
| 15 | 85.35341 | 27.68395 | 1329 | 1313 | 16 | THR 02 (displaced), TCH 15 m est |
| 16 | 85.35570 | 27.68896 | 1317 | 1321 | 0 | touchdown zone ~600 m (est) |


#### A2 RNP-style curved variant to 02 through the Bagmati gap (geometry est)
| # | lon | lat | alt m MSL | ground m | AGL m | note |
|---|---|---|---|---|---|---|
| 0 | 85.23000 | 27.41000 | 3505 | 834 | 2671 | GURAS, 11 500 ft |
| 1 | 85.21899 | 27.48965 | 2611 | 1343 | 1268 | (est) 2.8 deg along track |
| 2 | 85.23269 | 27.54204 | 2319 | 1230 | 1088 | (est) enters Bagmati gap 2.8 deg along track |
| 3 | 85.25171 | 27.57123 | 2135 | 1359 | 776 | (est) 2.8 deg along track |
| 4 | 85.27737 | 27.59565 | 1954 | 1331 | 623 | (est) RF turn starts 2.8 deg along track |
| 5 | 85.30550 | 27.61259 | 1790 | 1355 | 435 | (est) 2.8 deg along track |
| 6 | 85.32291 | 27.62503 | 1682 | 1419 | 263 | (est) RF turn ends on 022 2.8 deg along track |
| 7 | 85.33425 | 27.64245 | 1573 | 1309 | 264 |  2.8 deg along track |
| 8 | 85.34833 | 27.67333 | 1392 | 1306 | 86 |  2.8 deg along track |
| 9 | 85.35341 | 27.68395 | 1329 | 1313 | 16 | THR 02 2.8 deg along track |


#### A3 VOR/DME-A from the west (DARKE) + visual circling to 02
| # | lon | lat | alt m MSL | ground m | AGL m | note |
|---|---|---|---|---|---|---|
| 0 | 85.06194 | 27.75539 | 3200 | 602 | 2598 | D16 R-288 (IF), 10 500 ft (chart) |
| 1 | 85.10672 | 27.74261 | 3200 | 738 | 2462 | DARKE D13.5 (hold fix) |
| 2 | 85.16939 | 27.72469 | 2633 | 909 | 1725 | 3.6 deg profile (chart) |
| 3 | 85.20519 | 27.71443 | 2402 | 1490 | 912 | 3.6 deg profile (chart) |
| 4 | 85.24099 | 27.70417 | 2170 | 1426 | 744 | 3.6 deg profile (chart) |
| 5 | 85.27677 | 27.69390 | 1939 | 1309 | 629 | 3.6 deg profile (chart) |
| 6 | 85.31256 | 27.68362 | 1707 | 1307 | 400 | 3.6 deg profile (chart) |
| 7 | 85.34833 | 27.67333 | 1707 | 1306 | 401 | over VOR at MDA 5 600 ft |
| 8 | 85.38410 | 27.66304 | 1707 | 1325 | 382 | continue 108 deg (est) |
| 9 | 85.37089 | 27.63871 | 1646 | 1314 | 332 | right downwind (est) |
| 10 | 85.34456 | 27.63517 | 1554 | 1340 | 215 | right base (est) |
| 11 | 85.33847 | 27.65171 | 1463 | 1329 | 135 | short final (est) |
| 12 | 85.35341 | 27.68395 | 1329 | 1313 | 16 | THR 02 |


#### A4 VOR/DME-B from the east (IGRIS) + visual circling to 02
| # | lon | lat | alt m MSL | ground m | AGL m | note |
|---|---|---|---|---|---|---|
| 0 | 85.62066 | 27.60841 | 3200 | 962 | 2238 | IGRIS D15 (hold fix) |
| 1 | 85.52992 | 27.63011 | 2896 | 1457 | 1438 | D10 R-105 (IF) 9 500 ft (chart) |
| 2 | 85.49361 | 27.63877 | 2579 | 1532 | 1046 | linear descent (est) |
| 3 | 85.45730 | 27.64743 | 2262 | 1391 | 870 | linear descent (est) |
| 4 | 85.42099 | 27.65607 | 1945 | 1413 | 532 | linear descent (est) |
| 5 | 85.40282 | 27.66039 | 1786 | 1351 | 435 | linear descent (est) |
| 6 | 85.38434 | 27.64656 | 1707 | 1347 | 360 | turn to left downwind (est) |
| 7 | 85.35682 | 27.63069 | 1646 | 1357 | 289 | (est) |
| 8 | 85.33860 | 27.64116 | 1524 | 1317 | 207 | base (est) |
| 9 | 85.33988 | 27.65480 | 1448 | 1309 | 138 | short final (est) |
| 10 | 85.35341 | 27.68395 | 1329 | 1313 | 16 | THR 02 |


#### A5 Domestic visual circuit to runway 20 (est)
| # | lon | lat | alt m MSL | ground m | AGL m | note |
|---|---|---|---|---|---|---|
| 0 | 85.49361 | 27.63877 | 2400 | 1532 | 868 | from IGRIS/D8 R-105 (est) |
| 1 | 85.44095 | 27.68776 | 1950 | 1326 | 624 | join downwind east of the runway (est) |
| 2 | 85.38463 | 27.69959 | 1800 | 1308 | 492 | downwind abeam THR 20, 1,500 ft AGL (est) |
| 3 | 85.39040 | 27.72663 | 1700 | 1365 | 335 | base turn (est) |
| 4 | 85.37533 | 27.73202 | 1500 | 1370 | 130 | final 3 km, 3.2 deg (est) |
| 5 | 85.36962 | 27.71951 | 1420 | 1331 | 89 |  |
| 6 | 85.36391 | 27.70700 | 1352 | 1336 | 16 | THR 20 |


### 5.3 Departure paths

Altitudes use a constant climb gradient (jets 7%, turboprops 6%, the published minimum is 6%) from 10 m above the runway, capped at 13,500 ft (4,115 m); in game, a turboprop levels at 3,200–3,800 m and a jet keeps climbing outside the valley [E]. D1 shows the full DARKE 1D double loop, the most spectacular SID for a viewer in the city.

#### D1 Departure 02 west (DARKE 1D shape), jet 7 % gradient (est)
| # | lon | lat | alt m MSL | ground m | AGL m | note |
|---|---|---|---|---|---|---|
| 0 | 85.35798 | 27.69396 | 1324 | 1329 | 0 | lift-off ~1 200 m past THR (est) |
| 1 | 85.36496 | 27.70931 | 1453 | 1335 | 118 | runway end |
| 2 | 85.36948 | 27.71966 | 1539 | 1330 | 209 | D3 on R-022: turn LEFT (chart DARKE 1D) |
| 3 | 85.36140 | 27.73894 | 1699 | 1354 | 345 | 4 DME arc |
| 4 | 85.34571 | 27.73991 | 1808 | 1348 | 459 | 4 DME arc |
| 5 | 85.33012 | 27.73797 | 1916 | 1324 | 592 | 4 DME arc |
| 6 | 85.31534 | 27.73321 | 2024 | 1303 | 721 | 4 DME arc |
| 7 | 85.30200 | 27.72582 | 2133 | 1295 | 838 | 4 DME arc |
| 8 | 85.29068 | 27.71614 | 2241 | 1384 | 857 | R-310: turn to VOR |
| 9 | 85.31951 | 27.69474 | 2501 | 1287 | 1214 |  |
| 10 | 85.34833 | 27.67333 | 2760 | 1306 | 1454 | over VOR, then R-038 |
| 11 | 85.37381 | 27.70221 | 3045 | 1337 | 1708 | D2.2 R-038: turn LEFT |
| 12 | 85.37688 | 27.73498 | 3301 | 1374 | 1927 | 4 DME arc |
| 13 | 85.35695 | 27.73952 | 3443 | 1333 | 2110 | 4 DME arc |
| 14 | 85.33637 | 27.73911 | 3585 | 1345 | 2240 | 4 DME arc |
| 15 | 85.31669 | 27.73378 | 3726 | 1302 | 2425 | 4 DME arc |
| 16 | 85.29938 | 27.72393 | 3868 | 1295 | 2573 | 4 DME arc |
| 17 | 85.28574 | 27.71030 | 4010 | 1319 | 2691 | 4 DME arc |
| 18 | 85.27677 | 27.69390 | 4115 | 1309 | 2805 | 4 DME arc |
| 19 | 85.24099 | 27.70417 | 4115 | 1426 | 2688 | R-288 outbound |
| 20 | 85.16939 | 27.72469 | 4115 | 909 | 3206 |  |
| 21 | 85.10672 | 27.74261 | 4115 | 738 | 3377 | DARKE |


#### D2 Departure 02 east (IGRIS 1B), turboprop 6 % gradient
| # | lon | lat | alt m MSL | ground m | AGL m | note |
|---|---|---|---|---|---|---|
| 0 | 85.35798 | 27.69396 | 1324 | 1329 | 0 | lift-off |
| 1 | 85.36496 | 27.70931 | 1434 | 1335 | 99 | runway end |
| 2 | 85.36595 | 27.71194 | 1453 | 1313 | 140 | D2.5: turn LEFT (IGRIS 1B) |
| 3 | 85.33363 | 27.74714 | 1756 | 1339 | 417 | within 5 DME |
| 4 | 85.29391 | 27.73074 | 2014 | 1303 | 712 | R-320: turn LEFT to VOR |
| 5 | 85.32415 | 27.69885 | 2292 | 1297 | 995 |  |
| 6 | 85.34833 | 27.67333 | 2514 | 1306 | 1208 | over VOR, R-105 outbound |
| 7 | 85.43914 | 27.65175 | 3070 | 1381 | 1689 |  |
| 8 | 85.52992 | 27.63011 | 3626 | 1457 | 2168 |  |
| 9 | 85.62066 | 27.60841 | 4115 | 962 | 3153 | IGRIS |


#### D3 Departure 20 west (DARKE 1A), jet 7 %
| # | lon | lat | alt m MSL | ground m | AGL m | note |
|---|---|---|---|---|---|---|
| 0 | 85.35934 | 27.69700 | 1347 | 1333 | 14 | lift-off |
| 1 | 85.35233 | 27.68157 | 1476 | 1313 | 163 | runway end |
| 2 | 85.34833 | 27.67333 | 1546 | 1306 | 240 | over VOR: turn RIGHT (DARKE 1A) |
| 3 | 85.29134 | 27.64417 | 2000 | 1288 | 712 | within 4 DME |
| 4 | 85.27311 | 27.67331 | 2259 | 1365 | 895 | R-270: turn LEFT |
| 5 | 85.24099 | 27.70417 | 2586 | 1426 | 1160 | intercept R-288 |
| 6 | 85.16939 | 27.72469 | 3105 | 909 | 2196 |  |
| 7 | 85.10672 | 27.74261 | 3558 | 738 | 2820 | DARKE |


#### D4 Departure 20 east (IGRIS 1A), turboprop 6 %
| # | lon | lat | alt m MSL | ground m | AGL m | note |
|---|---|---|---|---|---|---|
| 0 | 85.35934 | 27.69700 | 1347 | 1333 | 14 | lift-off |
| 1 | 85.35233 | 27.68157 | 1458 | 1313 | 145 | runway end |
| 2 | 85.34833 | 27.67333 | 1518 | 1306 | 212 | over VOR: turn RIGHT (IGRIS 1A) |
| 3 | 85.29572 | 27.62572 | 1962 | 1269 | 693 | 4 DME arc |
| 4 | 85.27821 | 27.64922 | 2150 | 1293 | 857 | 4 DME arc |
| 5 | 85.27322 | 27.67703 | 2338 | 1364 | 974 | 4 DME arc |
| 6 | 85.28165 | 27.70418 | 2526 | 1300 | 1226 | 4 DME arc |
| 7 | 85.30200 | 27.72582 | 2714 | 1295 | 1419 | 4 DME arc |
| 8 | 85.33063 | 27.73809 | 2902 | 1324 | 1577 | 4 DME arc |
| 9 | 85.36244 | 27.73877 | 3089 | 1363 | 1726 | 4 DME arc |
| 10 | 85.39172 | 27.72777 | 3277 | 1364 | 1913 | 4 DME arc |
| 11 | 85.41324 | 27.70703 | 3465 | 1330 | 2135 | 4 DME arc |
| 12 | 85.42315 | 27.68028 | 3653 | 1310 | 2343 | 4 DME arc |
| 13 | 85.45730 | 27.64743 | 3951 | 1391 | 2560 | intercept R-105 |
| 14 | 85.52992 | 27.63011 | 4115 | 1457 | 2658 |  |
| 15 | 85.62066 | 27.60841 | 4115 | 962 | 3153 | IGRIS |

### 5.4 Helicopter corridors (estimates)

No helicopter route data was found in public sources; these corridors follow the obvious terrain gaps and keep clear of the 02/20 final approach. Helicopters cruise 300–600 m above the valley floor inside the valley and climb in the gaps [E][V].

#### H-E helicopter corridor east (Khumbu, Lukla, rescue) (est)
| # | lon | lat | alt m MSL | ground m | AGL m | note |
|---|---|---|---|---|---|---|
| 0 | 85.36000 | 27.70650 | 1342 | 1337 | 5 | domestic apron lift-off |
| 1 | 85.37320 | 27.71324 | 1500 | 1306 | 194 | climb away from the final |
| 2 | 85.42071 | 27.70178 | 1650 | 1384 | 266 | over Thimi north |
| 3 | 85.48002 | 27.68771 | 1900 | 1395 | 505 | Bhaktapur north edge |
| 4 | 85.56001 | 27.67512 | 2600 | 1465 | 1135 | Nagarkot-Banepa ridge |
| 5 | 85.66167 | 27.66862 | 3400 | 885 | 2515 | onward east |


#### H-N helicopter corridor north (Langtang, Gosaikunda) (est)
| # | lon | lat | alt m MSL | ground m | AGL m | note |
|---|---|---|---|---|---|---|
| 0 | 85.36000 | 27.70650 | 1342 | 1337 | 5 | domestic apron lift-off |
| 1 | 85.37306 | 27.72028 | 1550 | 1330 | 220 |  |
| 2 | 85.37578 | 27.75862 | 1850 | 1396 | 454 | Gokarna |
| 3 | 85.37765 | 27.79506 | 2900 | 1935 | 665 | Sundarijal climb |
| 4 | 85.37241 | 27.83193 | 3200 | 1559 | 1641 | over the Shivapuri ridge |


#### H-W helicopter corridor west (Pokhara, rescue west) (est)
| # | lon | lat | alt m MSL | ground m | AGL m | note |
|---|---|---|---|---|---|---|
| 0 | 85.36000 | 27.70650 | 1342 | 1337 | 5 | domestic apron lift-off |
| 1 | 85.34240 | 27.71549 | 1600 | 1328 | 272 | cross north of the runway end, then west |
| 2 | 85.29905 | 27.70649 | 1700 | 1294 | 406 | Thapathali-Teku |
| 3 | 85.25881 | 27.69863 | 1800 | 1431 | 369 | Kalanki |
| 4 | 85.20773 | 27.70171 | 2000 | 1437 | 563 | Thankot pass |
| 5 | 85.13653 | 27.70632 | 2300 | 1685 | 615 | Naubise valley |

### 5.5 Holding racetracks [E]

| Fix | Centre of the inbound leg end (fix) | Inbound | Turns | Legs | Levels (m MSL) |
|---|---|---|---|---|---|
| DARKE | 85.10672, 27.74261 | 108° | right (standard) [V] | 1 min ≈ 5.6 km at 180 kt TAS | 3,200 / 3,505 / 3,810 |
| IGRIS | 85.62066, 27.60841 | 285° | right [V] | same | 3,200 / 3,505 / 3,810 |

From the city, DARKE traffic shows as slow-moving dots ~22 km west, about 1.5–2° above the Chandragiri skyline from Thamel; IGRIS traffic ~25 km east, just above the Nagarkot–Banepa ridge from Bhaktapur [D].

---

## 6. Speeds, timings and the visual behaviour of each class

### 6.1 Speed schedule (indicated air speed; true airspeed is ~9% higher at 4,400 ft and ~20% higher at 10,000 ft) [E]

| Phase | Domestic turboprop (ATR 72-class) | STOL (Twin Otter-class) | Narrow-body jet | Wide-body jet | Helicopter (H125-class) |
|---|---|---|---|---|---|
| Arrival over ROMEO / outside the valley | 230 kt | 140 kt | 250 kt | 250 kt | 110 kt |
| GURAS / DARKE / IGRIS | 180 kt | 120 kt | 210 kt | 210 kt | — |
| SIERRA (D10) → D3 (5.5°) | 160 → 130 kt, gear and flaps down by D6 | 100 kt | 180 → 150 kt | 180 → 155 kt | — |
| Final (D3 → threshold) | **Vapp ≈ 115 kt** | 80 kt | **Vapp ≈ 140 kt** (A320 ~135, 737-800 ~145) | 145 kt | 60 kt → hover |
| Rollout | 1,000–1,400 m, prop "beta" roar | 400 m | 1,600–2,000 m, reverse thrust 4–6 s | 2,000 m | — |
| Take-off roll | 1,100–1,300 m (high elevation) | 400 m | 1,800–2,400 m | 2,500 m | vertical lift-off, 30 s hover-taxi |
| Initial climb | 125 kt, 6–8% | 90 kt | 160 kt, 7–9% | 165 kt, 6% | 70 kt, 5 m/s |
| Turns inside 4–5 DME | ≤ 180 kt, bank 20–25° | — | ≤ 180 kt, bank 25° | ≤ 180 kt | — |

Rule-of-thumb timings (for `leadTime`): ROMEO → THR ≈ 16 min; GURAS → THR ≈ 7 min; DARKE → THR (DME-A + circle) ≈ 6 min; IGRIS → THR (DME-B + circle) ≈ 7 min; lift-off → DARKE via the 1D double loop ≈ 7–8 min; lift-off → IGRIS ≈ 5 min [D] from path length and the speeds above.

Bank and pitch for animation [E]: bank = atan(v²/(g·r)); a 180 kt turn on a 4 DME arc (r = 7.4 km) needs ≈ 17° bank, standard-rate 3°/s turns ≈ 25°. Approach pitch +2 to +5° (jets), 0 to +2° (turboprops on the 5.5° segment, nose down -2°). Take-off rotation 8–15°, gear up 5 s after lift-off, flaps retract by 1,000 ft AGL.

### 6.2 Ground behaviour (aprons and taxi)

| Behaviour | Value | Tag |
|---|---|---|
| Parked by day (08:00–18:00) | International apron 4–8 of 17 stands (≤ 2 wide-body); domestic apron 15–25 aircraft; helicopters 6–12 | [E]; capacities [S] |
| Parked overnight | Most of the domestic fleet night-stops at KTM: 25–35 aircraft on the domestic and north aprons; 2–5 international | [E][V] |
| Taxi speed | 10–15 kt straight, 5 kt in turns | [E] |
| Runway vacate | Arrivals on 02 roll toward the north end and leave west via the links to taxiway F/G; on 20 they turn off near mid-field | [E] from OSM layout |
| Backtrack | Departures from 02 enter mid-field and backtrack ~1.2 km to the threshold | [S] no full parallel taxiway; distance [D] |
| Engine start / APU | APU whine at the stand 2–4 min before push-back; push-back 1–2 min (international) | [E] |
| Airside access for the player | None. Airside is not drivable (ASSET_MANIFEST `ghm_lmk_tribhuvan_airport`); viewing from the fence, the Ring Road, Sinamangal, Gaushala, Pashupati's east bank and rooftops | [S] manifest |

---

## 7. Cartoon aircraft models (generic, procedural)

Real dimensions set the **bounding size** so altitude and distance read correctly in a 1:1 world; the cartoon look comes from **proportions inside that box**. Every model is built in code from parameters (lofted fuselage from a radius curve, wings from a planform polygon with thickness, nacelles as lathed profiles, props and rotors as instanced blades).

### 7.1 Reference dimensions [S] (manufacturer data via Wikipedia type pages, §13)

| Real type (reference only) | Length m | Span m | Height m | Fuselage width m | Engines / props | Seen at KTM as |
|---|---|---|---|---|---|---|
| ATR 72-500/600 | 27.17 | 27.05 | 7.65 | 2.87 | 2 × PW127, **6-blade** props Ø 3.93 m | Most domestic flights (Buddha Air ~16, Yeti 5–7) [S] |
| ATR 42-500/600 | 22.67 | 24.57 | 7.59 | 2.87 | 2 × PW127, 6-blade | A few [S] |
| De Havilland Canada Dash 8-400 | 32.83 | 28.42 | 8.34 | 2.69 | 2 × PW150A, 6-blade Ø 4.11 m | Shree Airlines (4–6) [S] |
| Bombardier CRJ200 / 700 | 26.77 / 32.30 | 21.21 / 23.24 | 6.22 / 7.57 | 2.69 | 2 rear-mounted turbofans | Shree Airlines, being retired [S] |
| DHC-6 Twin Otter / Dornier 228 / Let 410 | 15.77 / 16.56 / 14.42 | 19.81 / 16.97 / 19.98 | 5.94 / 4.86 / 5.83 | ~1.6–1.9 | 2 turboprops, 3–5-blade | STOL operators (Tara, Sita, Summit); existing `ghm_veh_stol_turboprop_a` |
| Airbus A320ceo / A320neo | 37.57 | 35.80 | 11.76 | 3.95 | 2 underwing turbofans | Most international flights |
| Boeing 737-800 / 737 MAX 8 | 39.47 / 39.52 | 35.79 / 35.92 | 12.55 / 12.29 | 3.76 | 2 underwing turbofans | Gulf and Indian carriers |
| Airbus A330-200 / -300 | 58.82 / 63.66 | 60.30 | 17.39 / 16.79 | 5.64 | 2 large turbofans | National carrier and some foreign carriers; ≤ 3 wide-body stands [S] |
| Airbus H125 / AS350 B3 | 12.94 (rotors turning), fuselage 10.93 | rotor Ø 10.69 | 3.34 | ~1.9 | 1 turboshaft; 3-blade main, 2-blade tail rotor | Dominant helicopter type [S] |

### 7.2 Game models (ids follow ASSET_MANIFEST §8; budgets from §1.5: aircraft 8,000 / 2,500 / 800 tris)

| id | Look (generic) | Bounding size L × span × H (m) | Cartoon proportion rules [E] | Rig / moving parts | Share of traffic |
|---|---|---|---|---|---|
| `ghm_veh_air_turboprop_a` **(new)** | High wing, T-tail, two 6-blade props, gear sponsons on the lower fuselage, big round cockpit windows | 27 × 27 × 7.7 | Fuselage radius × 1.15; nose 10% rounder; props × 1.1; 10 large windows per side instead of 17; T-tail fin 5% taller; wing thickness × 1.3 | Props (blur disc above 600 rpm), gear (retract 6 s), flaps 2 stages, rudder, door, beacon/strobe sockets | 75% of domestic fixed-wing |
| `ghm_veh_air_turboprop_a` stretch preset | Same model, longer cabin, nacelles slightly bigger (Dash 8-400-like) | 32.8 × 28.4 × 8.3 | Parameter preset, no new mesh | same | 10% |
| `ghm_veh_stol_turboprop_a` (exists, M3b) | High wing with struts, fixed tricycle gear, square cabin | 15.8 × 19.8 × 5.9 | as manifest | as manifest | 15% of domestic fixed-wing; brought forward to W2 as ambient-only [E] |
| `ghm_veh_air_narrowbody_a` **(new)** | Low wing, two underwing turbofans, conventional tail, small winglets | 38 × 36 × 12 | Fuselage radius × 1.2, length × 0.95; engine nacelles × 1.25 and rounder intakes with a painted spiral spinner; 14 windows per side (instead of ~30); cockpit windows as one wrap-around visor | Gear (retract 8 s), flaps 3 stages, spoilers, reverse-thrust cowl slide (animated 0.3 m), beacons | 92% of international |
| `ghm_veh_air_widebody_a` **(new)** | Same family scaled up, twin-aisle body | 60 × 60 × 17 | Same rules; two-tone belly | same | 8% of international |
| `ghm_veh_helicopter_a` (exists, M3b) | Single-engine utility helicopter, skids | 12.9 × Ø10.7 × 3.3 | as manifest; no medical crosses | rotors with blur discs, door | all helicopters; brought forward to W2 as ambient [E] |

Tri split guidance for the new models [E]: fuselage 35%, wings and tail 25%, engines and props 20%, gear and doors 10%, lights and details 10%. LOD2 keeps the silhouette, props become flat discs. Beyond ~8 km render a **camera-facing sprite** (4 × 4 px minimum) plus the light sprites (§9.2).

### 7.3 Liveries (generic only)

Palette-first texture rule (ASSET_MANIFEST §1.6): one palette row per livery plus a paint mask (body, belly, tail, cheatline, engine). **No text, no registrations, no logos, no national flag on the tail** (a flag reads as a national carrier). Fictional, non-real 4-character "tail codes" are also avoided because they can collide with real registrations; leave the fin blank except for a simple shape.

| Livery id | Body | Belly | Tail | Cheatline / engines | Tail shape |
|---|---|---|---|---|---|
| `liv_glacier` | #F4F6F8 | #D9DEE4 | #2A9D8F teal | #264653 | Three soft mountain peaks |
| `liv_rhodo` | #FAF7F2 | #E9E2D8 | #D1495B rhododendron pink-red | #EDAE49 | Five-petal flower (generic) |
| `liv_kite` | #F7F9FB | #CFE8F7 | #4FC3F7 sky blue | #FFD54F | Diamond kite with tail ribbon |
| `liv_monsoon` | #EEF2F0 | #B7C9BF | #5B8C5A leaf green | #A3C4BC | Raindrop |
| `liv_sunrise` | #FFFFFF | #F3E3D3 | gradient #F4A261 → #E76F51 | #6D597A | Half sun |
| `liv_yak` | #F3E9D2 | #D8C8A8 | #8D6E63 brown | #3E2723 | Horn curve |
| `liv_heli_{red,yellow,blue}` | #E63946 / #FFC93C / #3A86FF | white | same | black skids | none (rescue look without crosses, manifest rule) |

Avoid lists [V art review against current photographs of every carrier using KTM]: burgundy-and-grey schemes (Qatar Airways), blue-with-orange (flydubai), all-over indigo (IndiGo), red tail with white disc emblem (Turkish Airlines), red/aubergine/gold (Air India), and the domestic carriers' white-with-blue, white-with-yellow or white-with-red tail combinations. Our six palettes were chosen away from these; the review must confirm.

Assign liveries by class [E]: turboprops use `liv_glacier`, `liv_rhodo`, `liv_kite`, `liv_monsoon` (equal weights); jets use all six; wide-bodies `liv_sunrise` and `liv_glacier`.

---

## 8. Where the player sees aircraft (sight lines from real places)

Distances from the KTM VOR (85.3483, 27.6733) and the final course (022° through threshold 02); cross-track positive to the east [D].

| Place | Relation to the flows | What the player sees |
|---|---|---|
| **Thecho / Sunakothi / Dhapakhel** | Final 02 passes overhead at **~250–300 m AGL** (D3–D3.5) | Biggest, loudest view of arrivals; gear down, landing lights by day too |
| **Satdobato / Imadol** | 1.5 km west of the final at ~200–280 m AGL | Arrivals sliding past at ~6–10° elevation every 2–4 min at peak |
| **Patan Durbar Square** | 2.1 km west of the final; aircraft at ~140–200 m AGL there | Low over the roofs to the east; best from the temple plinths and rooftop cafés |
| **Koteshwor / Tinkune / Sinamangal** | Under the last 1 km of the final; the 02 approach lights (HIALS, 870 m) stand among the houses | Aircraft at 50–100 m overhead, very loud; approach lights at night |
| **Pashupatinath / Gaushala** | 1.5 km west of the runway, mid-field | Take-off rolls and lift-offs; keep the aviation mix polite here (sacred site, §10.4) |
| **Boudhanath** | 0.8 km west of the extended centreline, 1 km beyond the north pavement end | Departures 02 climbing out at ~150–250 m AGL; arrivals on 20 low overhead in the afternoon |
| **Maharajgunj / Budhanilkantha south / Swayambhu** | The departures' 4 DME arc (7.4 km radius) passes over them; R-310 D4 is **directly over Swayambhu** | Jets in a climbing left turn at ~700–900 m AGL (D1) and again at ~2,500 m on the second loop |
| **Thamel** | 5 km west of the final; under the north-west part of the departure arc | Departures banking overhead at 600–800 m AGL; arrivals as distant low shapes to the south-east from rooftops |
| **Kirtipur / Chobhar** | West arrivals (DME-A) cross 3 km north at ~600 m AGL; 20-departures arc over Kirtipur | Turboprops descending from the west over Kalanki and Teku |
| **Bhaktapur / Thimi** | East arrivals (DME-B, R-105) pass 0.5–2 km south at 400–900 m AGL; helicopter corridor H-E north of the city | Turboprops from the east, helicopters at dawn heading for the mountains |
| **Chandragiri summit, Nagarkot** | Above DARKE / IGRIS holding levels | Aircraft *below* the viewer, holding over the hills: a rare, delightful angle |

### 8.1 Visibility and haze [E]

| Condition | Meteorological visibility | Effect |
|---|---|---|
| Post-monsoon clear (Oct–Nov) | 20–40 km | Holding traffic over DARKE/IGRIS visible; Himalaya behind |
| Pre-monsoon haze (Mar–May) | 3–8 km | Aircraft emerge from the haze ~5 km out; lights help |
| Winter morning fog (Dec–Jan) | < 1 km until ~10:00 | Sky empty of domestic traffic (§4.3); engines heard above the fog |
| Monsoon (Jun–Sep) | 5–15 km between showers, low cloud on the ridges | Aircraft appear below the cloud base at ~2,000–2,500 m |

Angular size on a phone [D]: with a 1,170 px wide portrait view and a 50° horizontal FOV (focal ≈ 1,255 px), an A320-sized span (36 m) covers ~22 px at 2 km, ~9 px at 5 km, ~4.5 px at 10 km; an ATR (27 m) ~17 px at 2 km. Hence the LOD distances in §11.

---

## 9. Lights (day and night)

### 9.1 Airport lights

| Light | Colour / spec | Place | Tag |
|---|---|---|---|
| Runway edge (HIRL) | White, 60 m spacing [E], last 600 m yellow [E] | Both sides of 02/20 | [S] HIRL; spacing [E] |
| Threshold / end | Green / red bars | 85.3534, 27.6840 and 85.3639, 27.7070 | [S] |
| Approach lights HIALS on 02 | White centreline bars and crossbar, **870 m** long, from the threshold toward 202° (end ≈ 85.3497, 27.6767, Koteshwor) | Among houses south of the airport | [S][D] |
| PAPI | 4 units, left side of each runway, 3.0°; red/white | ~300 m beyond each threshold [E] | [S] |
| Taxiway edge | Blue | Taxiways A–H | [E] ICAO convention |
| Apron floodlights | Warm white masts 25–30 m | International and domestic aprons | [E] |
| Tower beacon | Alternating white/green | ATC tower w340948572 | [E] ICAO aerodrome beacon convention [V] |
| Obstacle lights | Red, steady | Tower top, hangars | [E] |

### 9.2 Aircraft lights [S] US 14 CFR §25.1385–25.1401 (same as EASA CS-25)

| Light | Spec | Game behaviour |
|---|---|---|
| Navigation | **Red left wingtip, green right wingtip, white tail** (steady) | Always on when moving at night and at dusk |
| Anti-collision beacon | Red, flashing **40–100 cycles per minute** (§25.1401) | On whenever engines run; 60 /min, top and belly alternating |
| Strobes | White wingtip strobes, double flash ~1/s | On from runway entry to runway exit |
| Landing lights | Bright white, forward | On below 10,000 ft (3,050 m) by convention [E]; at night they are the first thing seen: a "star" that rises over the southern ridge (Bhattedanda notch) ~8–10 km out and grows for ~4 min |
| Logo / tail lights | — | Not used (no branding) |
| Helicopter | Same nav and beacon; a searchlight is not used | |

Render as HDR sprites with distance-independent minimum size (2 px) and an intensity curve `I = I0 / max(1, d/1 km)^1.2` (cartoon-tuned [E]), so the lights read at 25 km. Sprites cost one quad each; cap 12 aircraft × 6 lights.

---

## 10. Sound (aviation specifics; synthesis recipes in [audio.md](audio.md) §2.11, §6)

### 10.1 Levels and frequencies [E]

Ground-level maximum A-weighted levels for an aircraft passing at the stated slant distance (estimates consistent with published Lmax-versus-height data, e.g. a B737-400 at 73 dB(A) and a B767-300 at 85 dB(A) measured under an approach path [S], NATS Lmax explainer [S]):

| Class | Approach at 300 m | Departure at 300 m | Key frequencies | Audible to (quiet street / busy street) |
|---|---|---|---|---|
| Domestic turboprop (6-blade prop, Np ≈ 1,200 rpm at take-off, ~82% on approach) | 74 dB(A) | 80 dB(A) | Blade-pass **≈ 98 Hz on approach, 120 Hz at take-off** + harmonics 2–5×; turbine whine 6–8 kHz | 5 km / 2 km |
| STOL turboprop | 70 | 77 | 3-blade props, BPF 80–110 Hz | 3 km / 1.5 km |
| Narrow-body jet | 80 | 86 | Fan "buzz-saw" 1.5–3 kHz on approach (fan BPF ≈ fan rpm × 24–36 blades / 60); broadband jet roar 80–500 Hz on take-off | 8 km / 3 km |
| Wide-body jet | 84 | 90 | Lower fan tones (0.8–2 kHz), deeper roar | 9 km / 4 km |
| Helicopter | 77 | 79 | Main rotor 3 blades × ~390 rpm → **19.5 Hz** blade-pass (AM "thwop"), tail rotor ~68 Hz | 4 km / 1.5 km |
| Reverse thrust after touchdown (jets) | +6 dB swell for 4–6 s | — | broadband | — |

Propagation for the game [E]: `L(r) = Lref − 20·log10(r / 300 m) − α·(r − 300 m)` with air absorption α ≈ 0.5 dB per 100 m above 2 kHz and ≈ 0.1 dB per 100 m at 250 Hz (ISO 9613-1 order of magnitude), plus a ground-reflection comb (audio.md §6). Speed of sound c ≈ 340 m/s at valley temperatures; Doppler factor `c / (c − v_radial)`: an arriving jet at 72 m/s (140 kt) shifts up by ~27% coming and down by ~17% going.

### 10.2 Events

| Event | Trigger | Sound |
|---|---|---|
| Fly-over | Slant distance minimum | Doppler sweep + comb; duck ambience −3 dB within 1 km (audio.md mix) |
| Touchdown | Main gear contact | Tyre chirp (jets), squeal 0.3 s |
| Reverse / beta | 0–6 s after touchdown | Swell, then idle |
| Take-off roll | Throttle up | 5 s spool-up, roar peaking at rotation |
| Go-around | §3.5 | Sudden spool-up at low altitude: an unmistakable, harmless drama |
| Apron | Player within 300 m of the fence | APU whine (~400–500 Hz + harmonics), ground power hum, distant tug engines; tower radio never intelligible (manifest `ghm_amb_airport_apron` reused) |

### 10.3 Licensing

Ship **procedural synthesis only** for aircraft (audio.md: all aircraft in the procedural 65%). Public-domain US federal recordings (NASA, NPS) of generic flyovers may be used as *reference* for tuning; if any is shipped it must be checked per file (audio.md §5) [V]. No recordings from Kathmandu airport videos.

### 10.4 Cultural note

Pashupatinath sits beside the runway, with cremation ghats on the Bagmati. Real aircraft noise is part of that place, and the game should keep it, but the mix there should not exaggerate it: cap aircraft gain at −6 dB inside the Pashupati compound zone and never trigger "fun" aviation stingers there [E][V cultural reviewer].

---

## 11. Runtime design (proposed, for the implementation stage)

| Topic | Proposal [E] |
|---|---|
| Simulation | Pure kinematic: each aircraft follows its procedure spline with a speed schedule (§6.1); no physics, no collision. `WorldPos` doubles; positions recomputed from the spline parameter after floating-origin shifts (no drift) |
| Spawn radius | Simulate everything on the schedule (cost ≈ microseconds); **render** only within 30 km of the camera |
| Concurrency (from the §4.2 rates) | Peak ≈ 4 airborne + 2 taxiing; cap rendered aircraft at **Low 4 / Mid 6 / High 10** (excess drawn as light sprites only) |
| LOD distances | LOD0 < 0.8 km (> 60 px), LOD1 0.8–2.5 km, LOD2 2.5–8 km, sprite beyond 8 km [D from §8.1] |
| Parked aircraft | Static LOD1/LOD2 meshes, GPU-instanced per model and livery; LOD0 only within 300 m of the fence |
| Shadows | Only LOD0 aircraft below 300 m AGL cast a blob shadow onto the terrain (cheap and very readable) |
| Audio voices | Max 2 real-time aircraft synth voices (nearest two), others as baked flyby clips or silent (audio.md voice budget) |
| Map | Aircraft are **not** shown on the map or minimap (keeps the map clean); the airport POI shows the next arrival time as a fun fact (optional) |
| Data | One config file, e.g. `tia_aviation.json` (location decided by the implementing workflow; not created here): runway thresholds, VOR, procedures as point lists with altitudes and speed profiles, hourly table, modifiers, liveries |
| Tests | Golden-schedule test: seed → day of events, check totals per class within ±5% of §4.2 and minimum runway separation; path tests: every point AGL ≥ 0 except the last 2 km before touchdown, clearance ≥ 300 m over terrain outside 5 km from the runway |

Example config fragment [E]:

```json
{
  "runway": {"thr02": [85.3534132, 27.6839528, 1315.0], "thr20": [85.3639103, 27.7070042, 1337.0],
             "end_s": [85.3523298, 27.6815732], "end_n": [85.3649612, 27.7093117], "width_m": 45},
  "vor": [85.34833, 27.67333],
  "hours_open": [6, 24],
  "rates_per_hour": {"DOM_TP": [0,0,0,0,0,0,16,22,22,20,18,17,16,16,16,16,15,13,9,4,0,0,0,0],
                     "HELI":   [0,0,0,0,0,0,6,7,7,6,5,4,4,3,3,2,2,1,0,0,0,0,0,0],
                     "INTL_NB":[0,0,0,0,0,0,2,3,4,5,6,7,7,7,6,6,5,5,5,5,5,4,3,2],
                     "INTL_WB":[0,0,0,0,0,0,0,0,0,0,1,1,1,1,1,1,1,1,0,0,0,0,0,0]},
  "month_factor": [0.95,0.95,1.05,1.05,1.05,0.85,0.85,0.85,0.85,1.10,1.10,0.95],
  "procedures": {"A1": {"points": "§5.2 A1", "kind": "ARR", "runway": "02"}}
}
```

---

## 12. Open questions [V]

1. Current CAAN eAIP: SID/STAR names, the RNP AR 02/20 waypoints and the current helicopter routes in the Kathmandu CTR (the eAIP host failed TLS and returned 503 from this environment).
2. Real share of runway 20 use by time of day and season (no statistics found).
3. Helicopter movements per day at TIA (assumed ≈ 50).
4. Domestic night operations after 18:00 (which trunk routes still fly).
5. Real heights of the international terminal and the ATC tower (OSM `height=5` on the terminal is too low).
6. Livery avoid-list check against current photos of every KTM carrier (§7.3).
7. Cultural reviewer sign-off on the Pashupati audio cap (§10.4).

---

## 13. Sources

Data in this repository
* OSM extract `pipeline/data/raw/osm/nepal.osm.pbf` (read with pyosmium): w340948564, w1453500826, w1453500827 (runway), w118505122 (aerodrome), w736369689, w736369694, w340948578, w220704670, w912639950, w1368706500 (aprons), w327224048, w185408474 (terminals), w340948572 (tower), taxiways and parking positions D1–D17, gates 1–11, n4723585277 (NDB), windsocks. © OpenStreetMap contributors, ODbL.
* Copernicus GLO-30 DSM tile `pipeline/data/raw/dem/Copernicus_DSM_COG_10_N27_00_E085_00_DEM.tif` for ground heights.
* `pipeline/build/regions/kathmandu_valley/qa/lines.geojson` (RUNWAY lines with `width_cm` 4500/4600).
* `docs/reports/content_census/census.md` (aeroway counts), ASSET_MANIFEST, ARCHITECTURE §5 and §7, CONTENT_COVERAGE S11, [audio.md](audio.md).

Web
* Wikipedia, Tribhuvan International Airport: https://en.wikipedia.org/wiki/Tribhuvan_International_Airport
* Ekantipur, "TIA to handle over 9.7 million passengers, 138,000 flights in 2025" (14 Jan 2026): https://ekantipur.com/news/2026/01/14/en/tribhuvan-international-airport-to-handle-over-97-million-passengers-138000-flights-in-2025-27-21.html
* Onlinekhabar, "TIA handled over 850,000 passengers and 12,000 flights in November": https://english.onlinekhabar.com/tribhuvan-international-airport-handled-over-850000-passengers-and-12000-flights-in-november.html
* Himalpress, "TIA now operational for 18 hours": https://en.himalpress.com/?p=72818 ; B360, "TIA resumes 18-hour operations": https://www.b360nepal.com/detail/24844/tia-resumes-18-hour-operations ; Crisis24 closure alert: https://crisis24.garda.com/alerts/2024/11/nepal-kathmandus-tribhuvan-international-airport-to-remain-closed-2200-0800-nightly-until-at-least-early-april-2025-for-expansion-work
* ADB project document (runway capacity 33/h, peak > 50/h): https://ewsdata.rightsindevelopment.org/files/documents/25/ADB-38349-025.pdf ; Himalayan Times, "TIA and congestion": https://thehimalayantimes.com/opinion/tribhuvan-international-airport-and-congestion-ways-to-deal-with-it
* Airbus / NAVBLUE, RNP AR procedures at Kathmandu (2022): https://aircraft.airbus.com/en/newsroom/press-releases/2022-06-kathmandu-airport-adopts-navblues-rnp-ar-procedures-for-better ; https://www.navblue.aero/kathmandu-airport-implements-new-rnp-ar-procedures-designed-by-navblue/
* ICAO APANPIRG information papers on Nepal PBN: https://www.icao.int/APAC/Meetings/2014%20APANPIRG25/IP%208%20-%20AI%203.pdf
* SkyVector VNKT (thresholds, displaced threshold, VOR): https://skyvector.com/airport/VNKT/Tribhuvan-International-Airport
* Superseded Jeppesen chart copies (STAR 10-2/10-2A, SID 10-3–10-3C, 10-9, 13-1–13-3; 2007–2012), reference only: http://nepalfir.yolasite.com/resources/VNKTcharts.pdf ; https://www.tropicairvirtual.com/company/twoballs/Carte/VNKT_CHARTS.pdf ; https://www.virtualairlines.eu/charts/VNKT.pdf
* Wikipedia, PIA Flight 268 (ROMEO 41 DME FL150, SIERRA 10 DME 9,500 ft, Bhattedanda 2,524 m): https://en.wikipedia.org/wiki/Pakistan_International_Airlines_Flight_268
* Wikipedia, US-Bangla Flight 211 (holding at 13,500 ft, approach delays): https://en.wikipedia.org/wiki/US-Bangla_Airlines_Flight_211
* Kathmandu Post, holding and diversions: https://kathmandupost.com/money/2017/04/20/goma-air-flight-diverted-due-to-tia-congestion ; https://kathmandupost.com/money/2017/04/19/traffic-saturation-jams-tia ; https://kathmandupost.com/money/2016/11/03/vvip-flight-creates-delays
* Nepali Times, "Why do planes go around": https://nepalitimes.com/why-do-planes-go-around ; Himalpress, "Strong tailwinds disrupt flights at TIA": https://en.himalpress.com/strong-tailwinds-disrupt-flights-at-tia/ ; Ekantipur low-visibility report (Mar 2025): https://ekantipur.com/news/2025/03/13/en/due-to-low-visibility-flights-have-been-affected-for-a-week-54-05.html ; Nepali Times "Flights delayed by pollution": https://nepalitimes.com/flights-delayed-by-pollution
* Fleets: Buddha Air https://en.wikipedia.org/wiki/Buddha_Air ; Yeti Airlines https://en.wikipedia.org/wiki/Yeti_Airlines and https://en.himalpress.com/yeti-airlines-acquires-two-atr-72-500-aircraft/ ; Shree Airlines https://english.onlinekhabar.com/shree-airlines-adds-two-more-aircraft-to-its-fleet.html and https://www.ch-aviation.com/news/114117-nepals-shree-airlines-eyes-full-transition-to-turboprops ; CAAN domestic airlines https://caanepal.gov.np/air-transports/domestic
* Helicopters in Nepal (H125 fleet, operators): https://verticalmag.com/features/helicopter-ops-nepal-high-altitude-high-tempo/ ; Lukla fixed-wing flights moved to Ramechhap in peak seasons: https://www.bestheritagetour.com/blog/flight-vs-helicopter-to-lukla
* Mountain flights from 06:00–06:30: https://www.buddhaair.com/blog/everest-experience-flight
* Aircraft dimensions (reference only): https://en.wikipedia.org/wiki/ATR_72 ; https://en.wikipedia.org/wiki/ATR_42 ; https://en.wikipedia.org/wiki/De_Havilland_Canada_Dash_8 ; https://en.wikipedia.org/wiki/Bombardier_CRJ100/200 ; https://en.wikipedia.org/wiki/De_Havilland_Canada_DHC-6_Twin_Otter ; https://en.wikipedia.org/wiki/Airbus_A320_family ; https://en.wikipedia.org/wiki/Boeing_737_Next_Generation ; https://en.wikipedia.org/wiki/Airbus_A330 ; https://en.wikipedia.org/wiki/Eurocopter_AS350_Ecureuil
* Aircraft lights: 14 CFR §25.1385–25.1401: https://www.ecfr.gov/current/title-14/chapter-I/subchapter-C/part-25/subpart-F
* Noise: NATS, "Lmax": https://www.nats.aero/environment/noise-and-emissions/measuring-noise/lmax/ ; measured approach Lmax by type (Jeju National University thesis): https://oldlib.jejunu.ac.kr/bitstream/2020.oak/19384/2/
