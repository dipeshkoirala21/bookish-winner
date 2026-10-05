# W2 research: sacred architecture of the Kathmandu Valley (typology + hero specs)

> Status: research input for Wave 2 "Kathmandu comes alive", 2026-10-05. Feeds `ghm_bld_temple_pagoda`, `ghm_bld_temple_shikhara`, `ghm_bld_stupa`, `ghm_bld_shrine`, `ghm_bld_newar_bahal_*`, `ghm_roof_pagoda_tier` and the `ghm_lmk_*` hero rows in [ASSET_MANIFEST](../../ASSET_MANIFEST.md) §§ 4–7, and gaps L3–L17 / B3 / B6 in [CONTENT_COVERAGE](../../CONTENT_COVERAGE.md).
> Everything here is meant to become **generator parameters**. Every number carries a confidence tag:
>
> | Tag | Meaning |
> |---|---|
> | **[O]** | Measured from OSM (`pipeline/data/raw/osm/nepal.osm.pbf`, snapshot used by the pack), minimum-area bounding box (OBB) of the polygon, metres, computed in this research with pyosmium + shapely |
> | **[S]** | Stated by a cited source (URL in the row or in §9) |
> | **[E]** | Estimate (from proportions in OSM parts, photos used as reference only, or architectural convention). Good enough for a cartoon replica, not a survey |
> | **[V]** | Must be verified by a human reviewer (cultural or factual) before it ships |
>
> Units: metres, degrees. **Yaw** = compass bearing (clockwise from true north) of the **outward normal of the main door / main stair**, i.e. the direction a person looks when walking *out* of the shrine. OBB "axis" = bearing of the long side.
> Reference photographs are reference only (never shipped, never traced). No hand modelling: every shape below is described so code can build it.

---

## 0. Ten-line summary

1. The valley bbox (85.18–85.55 E, 27.55–27.82 N) has **2,131 OSM places of worship** (1,362 Hindu, 300 Buddhist, 371 untagged), 692 of them with a polygon; **167 dhunge dhara**, **101 named pokhari**, 2,437 `building:part`s [O].
2. Median OSM footprints: temple building 9.4 m long side (p10 4.2, p90 26.2), Ganesh shrine 7.2 × 5.1 m, Shiva temple 9.1 m, bahal courtyard 21.2 × 15.7 m, stupa/chaitya 12.3 m, gompa 27.9 m, pati/sattal 11.5 × 7.8 m, pokhari 35 × 20 m [O].
3. Pagoda proportions from real `building:part` data (Nyatapola, Patan Taleju, Pashupati): **each roof tier ≈ 0.74 × the one below** (range 0.62–0.84), sanctum ≈ 0.45 × bottom plinth width, first eave ≈ 1.8–2.1 × sanctum width, eaves ≈ 4.5 m apart on a 30 m temple [O].
4. Newar roofs are **straight-pitched with only a slight corner lift** (not Chinese curves); pitch ≈ 30–35° lower tiers, 35–45° upper [E]; eave bells every ≈ 0.4 m (Nyatapola: 529 bells, 168 on the lowest roof) [S].
5. OSM `height` on most heroes is a **plinth height** (Taleju 0.5, Kasthamandap 1, Nyatapola 2, Maju Dega 0.5): never use it for the temple [O]. Use §5 heights.
6. Boudhanath: three 20-cornered terraces 81.5 / 62.5 / 50.2 m, dome ring 38 m, dome 33.5 m, harmika 7 m [O]; total height **36 m** [S] (OSM parts claim 51 m, rejected); main gate on the **south** [O].
7. Swayambhu: dome Ø 26.7 m, total ≈ 33 m [O], **365-step eastern stairway** [S], Anantapur and Pratappur shikharas 17 m [O], Harati shrine NW of the stupa [O].
8. Big pagodas: Nyatapola 5 tiers on 5 plinths, 30 m (Wikipedia 33.2 m), stair faces south [S/O]; Taleju KTM 35 m on a 12-stage plinth, 3 gilt roofs [S]; Pashupati 2 gilt-copper tiers, 23.7 m, 4 silver doors, main door west [S]; Kumbheshwar 5 tiers [S]; Patan Taleju 34 m [O].
9. Shikharas: Krishna Mandir (stone, 1637, 21 pinnacles, 3 storeys, faces east) [S]; Vatsala Durga (stone, rebuilt 2021) [S]; Mahabouddha (terracotta, 1585, >9,000 Buddha tiles) [S]; Dharahara rebuilt 72 m, 22 storeys, opened 2021/2024 [S].
10. Cultural rules kept even with "everything enterable": sanctums (garbhagriha) are never entered or shown inside, the Kumari is never shown, there is no sacrifice, cremation or blood, struts are non-explicit, stupas are walked clockwise, and Pashupati's main compound gets a reviewer decision (§7).

---

## 1. What the OSM data gives us (valley bbox)

### 1.1 Counts [O]

| Feature | Count | Note |
|---|---|---|
| `amenity=place_of_worship` | 2,131 | 692 with polygon, 1,439 points only |
| … religion=hindu / buddhist / none / christian | 1,362 / 300 / 371 / 77 | + 7 `buddhist;hindu`, 1 jain, 2 muslim, 1 sikh, 2 bahai, 1 bon |
| … name matches Ganesh / Binayak | 235 | corner-shrine archetype |
| … Shiva / Mahadev / -eshwar | 226 | linga shrines, 1–3-tier pagodas, white shikharas |
| … Bhairav | 36 | |
| … Bhimsen | 21 | traders' god, usually a 2–3-storey "house temple" |
| … bahal / bahi / vihar | 99 | Newar Buddhist courtyards |
| … gompa / monastery | 64 | Tibetan style, Boudha, Swayambhu, Kopan |
| … stupa / chaitya (name or `tower:type=stupa`) | 46 | thousands of small chaityas are unmapped |
| `historic=wayside_shrine` | 25 | |
| Dhunge dhara (`dhungedhara=yes`, `historic=stone_tap`, name *hiti/dhara*) | 167 | many carry `number_of_water_spouts` |
| Named ponds (`natural=water` + *pokhari/tank/pond*) | 101 | |
| Pati / sattal / mandap (by name) | 33 | real number is far higher (unnamed) |
| `building:part` | 2,437 | rich for Nyatapola (35 parts), Patan Taleju (9), Boudha (10), Swayambhu (12), Pashupati (5), Dharahara (4) |

### 1.2 Footprint sizes by type (OBB, metres) [O]

| Type | n (polygons) | Long side p25 / p50 / p75 | Short side p50 | Area p50 m² |
|---|---|---|---|---|
| All temple-type buildings | 444 | 5.9 / **9.4** / 16.3 (p10 4.2, p90 26.2) | — (aspect p50 1.20, p75 1.48) | — |
| Ganesh / Binayak | 52 | 4.5 / **7.2** / 10.7 | 5.1 | 33 |
| Shiva | 52 | 5.3 / **9.1** / 17.2 | 6.6 | 61 |
| Bhimsen | 6 | 8.0 / **8.1** / 11.5 | 6.2 | 44 |
| Bahal (courtyard outline) | 54 | 12.1 / **21.2** / 27.0 | 15.7 | 286 |
| Stupa / chaitya | 18 | 8.3 / **12.3** / 22.5 | 11.5 | 111 |
| Gompa | 26 | 21.9 / **27.9** / 79.2 | 19.4 | 421 |
| Pati / sattal / mandap | 14 | 7.8 / **11.5** / 14.8 | 7.8 | 81 |
| Pokhari (water polygon) | 39 | 17.6 / **35.0** / 47.0 | 19.9 | 703 |

Use these as the default size distribution when a point-only POI needs a prop (CONTENT_COVERAGE 1.18): sample the long side from the type's p25–p75, aspect from 1.0–1.5.

### 1.3 Data pitfalls found

* Temple `height` tags are plinth heights: Taleju KTM `0.5`, Maju Dega `0.5`, Trailokya Mohan `0.6`, Shiva-Parvati `0.6`, Kasthamandap `1`, Krishna Mandir `1` (note "base step 1"), Nyatapola `2`, Pashupati `6`. Treat `height < 3` on a temple polygon as **plinth height** and take the body from this document.
* Footprints of heroes are mostly the **plinth outline**, not the sanctum. The sanctum is ~0.45 × that width (§2.1).
* Boudhanath: the anchor in `landmarks.yaml` is the compound wall **w56688296**; the stupa is **w56688295** (also noted in CONTENT_COVERAGE L2).
* Kal Bhairav **w196261745** is tagged `building=temple` + pyramidal roof, but the real object is an **open-air stone relief** (no roof) [V].
* Guhyeshwari is mapped at the wrong place (n4525481095, 27.7448 N — that is 4 km from Pashupati); place it by hand from imagery [V].
* Several key objects are unnamed `tourism=attraction` nodes (Hanuman Dhoka n11365076769, Basantapur tower n6348849285, Bhaktapur Golden Gate n11365076869): coordinates usable, identity [V].

---

## 2. Typology: parametric specs

### 2.0 Shared palette (cartoon, sRGB hex) [E unless noted]

| Token | Hex | Use |
|---|---|---|
| `s.brick.dachi` | `#A0472E` | Newar glazed facing brick (dachi apa), temple walls, plinths |
| `s.brick.plinth` | `#B5603E` | Rough brick plinth steps, Maju Dega ochre plinth |
| `s.brick.ochre` | `#9A5A32` | OSM roof/wall colour of Maju Dega [O] |
| `s.tile.roof` | `#6B3A2A` | Jhingati clay roof tiles (Nyatapola parts `#800000` [O] → cartoon `#7A2E22`) |
| `s.wood.carved` | `#3E2416` | Struts, lattice windows, door frames (oiled sal) |
| `s.wood.paint.red` | `#B3261E` | Painted struts / torana accents |
| `s.gilt` | `#D9A62E` (hi `#F2CC5B`, lo `#9C7317`) | Gilt copper roofs, gajur, pataka, toranas (Swayambhu parts `#DAA520` [O]) |
| `s.silver` | `#C9CED6` | Pashupati doors |
| `s.stone.grey` | `#8E8A80` | Stone shikharas, guardian figures, chaityas, hiti spouts |
| `s.stone.buff` | `#B9A684` | Krishna Mandir / Vatsala Durga sandstone |
| `s.whitewash` | `#F6F0E2` | Stupa dome (manifest `w.whitewash`); Swayambhu OSM `#fcecc0` [O] |
| `s.saffron` | `#F2A93B` | Lotus-petal arcs on Boudha / Swayambhu dome (festival layer) |
| `s.sindoor` | `#E0412B` | Red powder on shrine images, Hanuman's red paste |
| `s.marigold` | `#F7A21B` | Garlands |
| `s.eyes.ink` | `#1B1B24` | Buddha eyes outline, brows |
| `s.eyes.blue` | `#2F5DA8` | Iris accent / brow tint on some harmikas [V] |
| Prayer flags (left→right) | blue `#1F5BA8`, white `#F5F5F0`, red `#C8282E`, green `#2E8B3F`, yellow `#F4C21A` | The order is fixed: sky, air, fire, water, earth [S: Wikipedia "Prayer flag"] |

### 2.1 Newar pagoda temple (dega / degah)

**Anatomy, bottom to top:** stepped plinth (*pīṭha*) → square (sometimes rectangular) brick sanctum (*garbhagṛha*) with one door (Vishnu/Devi) or four doors (many Shiva temples) → an ambulatory / colonnade on the bigger ones → 1–5 roof tiers, each carried on carved diagonal wooden struts (*tunāla / tundal*) → gilt pinnacle (*gajur*) → optional metal banner (*pataka*) hanging from the top roof → wind bells under every eave. Struts carry the roof load from the wall to the eave [S: nepaltraveller "Tundal in Nepalese Architecture"].

**Measured proportions from OSM `building:part` data [O]:**

| Temple | Bottom plinth | Plinth step widths (m) | Sanctum / upper body | Eave widths per tier (m) | Tier ratio | Roof rise per tier (m) | Top |
|---|---|---|---|---|---|---|---|
| Nyatapola | 24.2 × 21.4 | 24.2 → 21.7 → 18.1 → 15.4 → 10.9 | 10.9 × 9.4 | 19.3, 16.3, 12.6, 9.3, 5.8 | 0.84, 0.77, 0.74, 0.62 | 4.9, 4.8, 4.4, 3.5, 2.8 | eaves at 11.6, 16.4, 20.7, 24.7, 29.0; roof top 31.7 |
| Taleju (Patan) | tower body 16.9 × 15.3 to 17.5 m | (rises from palace block) | — | 18.3, 13.5, 9.4 | 0.74, 0.70 | 4.0, 2.5, 2.0 | finial top 34.0, finial Ø 1.9 |
| Pashupatinath | 19.5 × 19.2 | — | 8.7 × 8.3, to 14.5 m | 18.5, 13.3 | 0.72 | 2.5 (+?), 4.5 | ≈ 19 m roof top + gajur (23.7 m total [S]) |

**Generator parameters (cartoon replica):**

| Parameter | Symbol | Value | Basis |
|---|---|---|---|
| Plinth levels | `nPlinth` | 0–12 (small shrines 1–3, Maju Dega 9, Taleju KTM 12, Nyatapola 5) | [S]/[O] |
| Plinth step rise | `hStep` | small temples 0.25–0.6 m; monumental 0.8–1.6 m (Nyatapola OSM levels 1.6/1.2/0.8/0.4 — rises look under-mapped; use ≈ 1.4 m each for 7 m) | [O]/[E] |
| Plinth inset per level (each side) | `dPlinth` | 0.06–0.09 × bottom width (Nyatapola: 1.25, 1.8, 1.35, 2.25 m) | [O] |
| Stair | — | one axial stair on the door side (Nyatapola, Taleju) or four (shrines with four doors); stair width ≈ 0.25–0.35 × plinth width [E] | |
| Sanctum width | `wCore` | **0.45 × bottom plinth width** (Nyatapola 0.45, Pashupati 0.45) | [O] |
| Storey height of sanctum | `hCore` | 0.45–0.6 × `wCore` for the ground floor [E] | |
| First eave width | `wEave1` | **1.8–2.1 × `wCore`** (Nyatapola 1.77, Pashupati 2.1) ⇒ overhang per side 0.4–0.55 × `wCore` | [O] |
| Tier shrink | `kTier` | **0.74 mean** (0.62–0.84); the top tier shrinks the most | [O] |
| Eave-to-eave spacing | `hTier` | ≈ 0.23 × `wEave1` (Nyatapola 4.6 m on 19.3 m) | [O] |
| Roof rise / eave width | — | 0.22–0.25 bottom tier → 0.35–0.48 top tier | [O] |
| Roof pitch | `pitch` | 30–35° bottom tiers, 35–45° top tier | [E] |
| Eave line | — | **straight**, corners lifted ≤ 5 % of eave half-width (cartoon may go to 8 %). No Chinese upsweep | [E] (photo reference) |
| Roof material | — | Clay tiles (`s.tile.roof`) by default; **gilt copper** (`s.gilt`) on the top tier or all tiers of royal/major temples (Taleju, Pashupati, Golden Temple, Changu Narayan, Bagh Bhairab top roof) | [S] |
| Struts per side per tier | `nStrut` | `max(2, round(wallWidth / 1.1 m)) + 1`, plus one diagonal **corner strut** (winged griffin/kunsala) at each corner | [E] |
| Strut angle | — | 50–60° from horizontal, foot at ≈ 0.55 × storey height on the wall, head at the eave purlin | [E] |
| Strut art | — | Generic stylised deity panel (upper 2/3), small figure/ornament at foot. **No explicit imagery** (many real struts, e.g. Jagannath, Bhaktapur Pashupati, Maju Dega, are erotic) [V] | ASSET_MANIFEST |
| Gajur height | `hGajur` | 0.10–0.15 × total height (Patan Taleju 5 m on 34 m); shape: stacked gilt bell/kalasha with chhatra (parasol) at top | [O]/[E] |
| Pataka | — | Gilt sheet banner from top roof down the front over the lower roofs; width 0.3–0.6 m, ends above the door torana; only on major shrines (Golden Temple, Kumbheshwar, Taleju, Pashupati) [E][V] | |
| Bells | — | Pipal-leaf-clapper bells under every eave, **≈ 0.4 m spacing** (Nyatapola 529 bells: 168/128/104/80/48 bottom→top) [S]; plus 1–2 large hand-bells on posts by the door | [S] |
| Torana | — | Semicircular gilt/wood tympanum above the main door, width = door width + 2 × 0.3 m [E] | |
| Door | — | 1.0–1.6 m wide, 1.8–2.2 m high (cartoon), carved frame, often 3-bay "tiki" lattice either side [E] | |
| Guardian lions | — | One pair flanking the bottom of the main stair (more pairs per plinth on monumental temples). Body ≈ 0.12–0.18 × plinth width, typically 1.0–1.8 m tall incl. pedestal; lime-white or stone grey with red/gold accents [E] | |
| Polycount | — | ≤ 600 per tier, cap 5,000–9,000 per temple (ASSET_MANIFEST) | |

**Tier counts in the valley:** 1–2 tiers are the norm; 3 tiers on royal/major temples; **only two free-standing 5-tier temples exist: Nyatapola and Kumbheshwar** [S: Wikipedia Kumbheshwar Temple]. Never generate 4 or 5 tiers procedurally for an unnamed temple.

### 2.2 Shikhara (deval)

Two families:

1. **Stone granthakuta shikhara** (Krishna Mandir Patan, Vatsala Durga Bhaktapur, Chyasim Deval Patan): a square stone plinth of 3–5 steps; an arcaded ground floor with corridors; 2 upper storeys of small open **pavilions (chhatris)** clustered around a central curvilinear tower; amalaka (ribbed disc) + kalasha + gilt finial. Krishna Mandir: **21 pinnacles, three storeys** [S: nepaltraveller].
   * Params: plinth `nPlinth` 3–5, `hStep` 0.4–0.6; central tower base = 0.5 × plinth width; tower height = 2.0–2.4 × its base; pavilions = 0.18–0.22 × plinth width, arranged 4 corners + 4 mid-sides on storey 1, 4 corners on storey 2 [E]. Colour `s.stone.buff` / `s.stone.grey`.
2. **Plastered brick shikhara** (Fasidega Bhaktapur, Mahabouddha in terracotta, Rato Machhindranath Bungamati, Pratappur/Anantapur at Swayambhu, Maju Dega is *not* one): a curvilinear tower on a stepped base, whitewashed or brick, often with a red/saffron pennant.
   * Params: height/base 2.0–2.5 (Anantapur 17 m on 8.8 m = 1.9 [O]); 12–24 vertical offsets (rathas) [E]; amalaka disc Ø 0.6 × top width.

### 2.3 Stupa (large)

**Anatomy:** mandala base terraces → drum / base ring with niches (prayer wheels at Boudha, Buddhas at Swayambhu) → hemispherical whitewashed dome (*anda*) → square **harmika** with the Buddha's eyes on all four faces (nose drawn as the Nepali numeral **१**, urna dot between the brows) → **13 gilt rings** (trayodashabhuvana) [S: Wikipedia Swayambhunath] → parasol (*chhatra*) → gilt finial (*gajur*). Prayer-flag lines run from the spire to the terrace edge; worshippers walk the kora **clockwise**. On Swayambhu, five Dhyani Buddha shrines are set into the dome [S].

**Measured from Boudha and Swayambhu parts [O]:**

| Element | Boudhanath (OSM) | Ratio to dome Ø | Swayambhu (OSM) | Ratio to dome Ø |
|---|---|---|---|---|
| Outer compound / kora wall | 95.8 × 95.5 | 2.86 | — (hilltop platform 129.7 × 100 landuse) | — |
| Terrace 1 (lowest) | 81.5 × 79.5, top 4 m | 2.43 | — | — |
| Terrace 2 | 62.5 × 61.7, top 8 m | 1.87 | — | — |
| Terrace 3 | 50.2 × 49.8, top 10.5 m | 1.50 | — | — |
| Base ring / drum | 38.0 × 37.7, 10–12 m | 1.13 | (drum ~1 m) | — |
| Dome | **33.5**, 10 → 23.7 m (dome rise 11.7) | 1.00 | **26.7**, 1 → 12 m (rise 11) | 1.00 |
| Harmika | 7.0 × 7.0, 20 → 27 m | 0.21 | 5.4–6.7, 15.1 → 18–19 m | 0.20–0.25 |
| 13-ring spire base | 8.0, 26.8 → 45 m | 0.24 | 6.5, 18 → 30 m (pyramidal) | 0.24 |
| Parasol | 5.5, 42 → 45 m | 0.16 | 4.0, 26.2 → 28 m | 0.15 |
| Finial | 2.0, to 51 m | 0.06 | 1.2, to 33 m | 0.045 |

Boudha's OSM spire (to 51 m) contradicts the published **36 m** total [S: Wikipedia Boudhanath]. Recommended Boudha section (keep the OSM plan, scale the elevation) [E]: terraces to 10.5 m (as OSM) → dome to 21 m (rise 10.5 m, a flattened hemisphere 0.31 × Ø, matching photos) → harmika 21–25 m → 13 rings 25–33 m → parasol 33–34.5 m → finial to 36 m.

**Generic parametric stupa (`ghm_bld_stupa_{s,m,l}`)** [E]:

| Param | Value |
|---|---|
| Terrace count | 0 (small chaitya), 1 (medium), 3 (Boudha type) |
| Terrace width ratio (to dome Ø) | 2.4 / 1.9 / 1.5 bottom → top; terrace rise 0.1–0.12 × dome Ø each |
| Terrace plan | square with **20 corners** (each side broken by two re-entrant steps), cardinal-aligned [S: sacredsites.com / studiomatrx guide] |
| Dome rise | 0.33–0.42 × dome Ø (Swayambhu 0.41, Boudha 0.31–0.35) |
| Harmika | 0.21 × Ø wide, 0.15–0.2 × Ø tall; eyes on 4 faces; a gilt torana panel above the eyes |
| Spire | 13 rings, base 0.24 × Ø, top 0.12 × Ø, height 0.3–0.45 × Ø, each ring ≈ 1/13 of height, gilt |
| Parasol + finial | parasol Ø 0.15 × Ø; finial height 0.08–0.1 × Ø |
| Prayer flags | 16–36 lines from spire top to the terrace edge, 20–30 flags per 10 m, sag 8–12 % of span |
| Prayer wheels | in wall niches, cylinder Ø 0.20–0.25 m, height 0.35–0.45 m, pitch 0.5 m along the kora wall (Boudha perimeter ≈ 382 m ⇒ ≈ 700 wheels at full spacing; the real count is unverified) [E][V] |

### 2.4 Chaitya (Newar votive stupa)

Small, carved stone, everywhere in bahals and toles. Height 0.8–3 m (Licchavi-era examples ~1–2 m) [E]. Stack: square stepped base (2–4 steps) → drum with **four niches with the four directional Buddhas: Akshobhya (east), Ratnasambhava (south), Amitabha (west), Amoghasiddhi (north)** (Vairocana at the centre, unseen) → small dome → harmika → 13-ring cone → parasol. Colour `s.stone.grey`, sindoor and rice offerings at the niches. Footprint 0.6–2.0 m square [E]. A courtyard chaitya stands on the axis between the gate and the shrine.

### 2.5 Buddhist bahal / bahi courtyard

* Plan: a closed quadrangle of **2–3-storey** brick ranges around a square or near-square courtyard. OSM median courtyard outline **21.2 × 15.7 m** [O]; Golden Temple (Kwa Bahal) compound 26.1 × 25.0 m [O].
* Gate: a single low entrance (often 1.6–2.0 m high) in the middle of one range, with a torana and a pair of stone lions [E].
* Shrine (*kwapa dyo*): in the range **opposite the gate**, marked by a richer facade, gilt torana, sometimes a 1–3-tier roof rising above the range (Golden Temple: 3 gilt tiers [S]).
* Courtyard: brick/stone paving, 1–5 chaityas, a dharmadhatu mandala on a stone, a bell, a pole; ground floor arcades with carved wooden posts (3–5 bays per side).
* **Bahi** variant: lower (1–2 storeys), wider arcades, often outside the old city core.
* Interaction: courtyards are walkable public space (accurate). Shrine interior closed (§7).

### 2.6 Tibetan-style gompa (Boudha, Swayambhu, Kopan)

* Body: 2–4-storey rectangular block, OSM median 27.9 × 19.4 m [O]; Kopan main gompa 30.4 × 19.8 m, Kopan Nunnery 42.4 × 23.9 m [O].
* Facade: whitewashed or ochre walls, **tapering trapezoid windows** with black (`#1B1B24`) frames, maroon (`#7A1F1F`) parapet band of stacked twigs (penbe) under the roof, gilt roof ornaments (dharma wheel flanked by two deer above the entrance, victory banners *gyaltsen* on the corners), a portico with 2–4 red columns and painted capitals, door curtains, prayer-wheel rows on the outer wall.
* Roof: flat with a small gilt Chinese-style hip roof over the main hall on larger gompas [E].
* Palette: white `#F3EFE6`, maroon `#7A1F1F`, saffron `#E89B2E`, gilt `s.gilt`, window black, door red `#B3261E`.
* Do **not** use gompa forms for Newar bahals (CONTENT_COVERAGE L6).

### 2.7 Small shrines

| Kind | Form | Size [E unless noted] | Notes |
|---|---|---|---|
| Ganesh corner shrine | Open niche or 1-tier mini pagoda; often a sunken image at street level; brass bells; tin or tile canopy | OSM median 7.2 × 5.1 m footprint incl. platform [O]; image 0.3–0.8 m | Red sindoor, marigolds; Ganesh is worshipped first, so put shrines at junction corners and gateways |
| Bhimsen | Usually a **2–3-storey house-like temple**, upper-floor shrine, gilt window | OSM median 8.1 × 6.2 m [O] | Patron of traders; found in market toles (Patan Durbar Square has the major one) |
| Bhairav | Mask or relief, sometimes in a small pagoda | 2–6 m | Fearsome but cartoon-friendly; no blood |
| Shiva linga | Linga on a yoni base in an open pavilion or 1-tier pagoda; Nandi facing it | linga 0.3–1 m; shrine 3–6 m | Often four doors |
| Nag stone / Nasa dyo | Flat stone or hole in a wall | 0.3–1 m | |
| Garuda / king column | Stone column 6–9 m with a kneeling Garuda or a gilt king on top, facing the temple or palace | column Ø 0.5–0.8 m | Patan: King Yoganarendra Malla column n2097740385, Garuda n2097740384 [O] |

### 2.8 Pati, sattal, mandapa

| Kind | Form | Size |
|---|---|---|
| **Pati** | 1-storey open rest house on a 0.4–0.9 m brick plinth, open front with 3–5 wooden posts and carved brackets, tile roof (single slope or hip) | 6–12 × 3–5 m [E]; OSM median of named pati/sattal/mandap 11.5 × 7.8 m [O] |
| **Sattal** | 2–3-storey rest house for pilgrims, open ground floor | Simha Sattal 14.8 × 9.8 m, height 7.5 m, hipped roof 4 m [O]; Laxmi Narayan Sattal 13.5 × 11.2 m [O] |
| **Mandapa** | Square open pavilion, columns all round, 1–3 tiers | Kasthamandap 21.8 × 21.4 m (largest) [O]; Chyasing Mandap Bhaktapur 7.8 × 7.8 m [O] |
| **Dabali (dabu)** | Raised square stone/brick platform for masked dance-drama | 0.6–1.2 m high, 5–9 m square [E]; Bhaktapur Durbar Square "Dabu" n9871425646 [O] |
| **Kot** | Palace/armoury compound (e.g. Kot near Hanuman Dhoka); in game, just a walled courtyard archetype | — |

All of these are enterable public space; sitting animations on pati plinths are appropriate.

### 2.9 Dhunge dhara (hiti, stone water spouts)

* A **sunken stepped pit** (hiti-gaḥ) below street level, depth 1.5–5 m [E] (Sundhara is a deep pit; Manga Hiti about 3 m), rectangular or cruciform plan; stone steps down one or more sides; a back wall with **1–9 spouts** (OSM: Tusha Hiti 1, Manga Hiti 3, Alko Hiti 5, Dhalko Hiti 5 [O]).
* Spout: a carved stone **makara** (crocodile-like water monster) head, 0.6–1.0 m long, often with a small deity niche above; water falls into a stone basin.
* Manga Hiti (Patan) is among the oldest working spouts, Licchavi-era (6th century) [V]; Sundhara (KTM) OSM w70954208 29.9 × 28.9 m [O].
* Params: pit size 4–30 m [O range], steps rise 0.2 m / tread 0.3 m, spout height above basin 0.9–1.4 m. Water: flowing (looping VFX) or dry (OSM `status=Non-Operational` on some, use dry variant).
* Audio: water splash loop, CC0/procedural only.

### 2.10 Pokhari (ponds/tanks)

* Rectangular stone- or brick-edged tanks with stepped ghats on 1–4 sides, sometimes a central shrine reached by a causeway.
* OSM median 35.0 × 19.9 m [O]. Rani Pokhari 165.8 × 124.7 m, central Balgopaleshwar shrine on an island with a causeway from the south [O]/[V]; Kamal Pokhari 130.6 × 86.5 m [O]; Nag Pokhari 38.2 × 32.5 m [O]; Bhaktapur Siddha Pokhari and Kamal Pokhari (54.6 × 13.3 m) [O].
* Edge: 0.5–1.5 m drop to water, steps 0.25 m, wall cap of dressed stone. Water colour green-grey (`#6E8B6A`) [E].

---

## 3. Hero summary table

Coordinates are the OSM centroid (lat, lon) of the given object. Heights are total to the finial tip unless noted. Yaw = outward normal of the main door (§ header).

| # | Hero | OSM | Lat, lon | Footprint (OBB) | Height | Tiers / form | Roof | Deity / dedication | Main door yaw |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Boudhanath | w56688295 (stupa), w56688296 (kora wall) | 27.721436, 85.362004 | terraces 81.5 → 50.2; wall 95.8 [O] | 36 [S] | stupa, 3 × 20-corner terraces | whitewash dome, gilt spire | Buddhist (relics of Kashyapa Buddha by tradition [V]) | main gate **180° (south)** [O: gate n5302729521] |
| 2 | Swayambhunath | w201223707 | 27.714931, 85.290391 | dome Ø 26.7 [O] | ≈ 33 above platform [O] | stupa on hilltop | whitewash dome, gilt spire | Adi-Buddha, Buddhist + Hindu | stairway from the **east, 90°** [S] |
| 3 | Pashupatinath | w913170315 | 27.710465, 85.348665 | 19.5 × 19.2 [O] | 23.7 [S] | 2-tier pagoda, 4 doors | gilt copper both tiers [S] | Shiva (Pashupati, 4-faced linga) | main door **270° (west)**, Nandi at west door [V] |
| 4 | Taleju (KTM) | w1414600808 (plinth) | 27.704902, 85.307958 | 47.7 × 45.3 plinth [O] | 35 [S] | 3-tier pagoda on 12-stage plinth [S] | gilt copper ×3 [S] | Taleju Bhawani | ≈ 190° (south, toward the square) [E][V] |
| 5 | Kasthamandap | w183558418 | 27.703934, 85.305779 | 21.8 × 21.4 [O] | ≈ 20 [E] | 3-tier open mandapa (rebuilt, completed 2021 [S]) | tile | Gorakhnath shrine at centre [S] | open on all sides; shrine faces ≈ 90° [V] |
| 6 | Hanuman Dhoka gate | n11365076769 (unnamed attraction) | 27.704099, 85.307449 | palace frontage ≈ 100 m [E] | gate ≈ 6 [E] | palace gate, Hanuman statue (1672) | gilt door | Hanuman | gate faces ≈ 280° (west onto square) [V] |
| 7 | Kumari Ghar | n2659104413 | 27.703770, 85.306524 | ≈ 22 × 22 courtyard house [E] | ≈ 12 (3 storeys) [E] | Newar bahal-type palace (1757) [V] | tile | Kumari (living goddess) — never shown | door ≈ 0° (north onto Basantapur/Durbar Sq) [V] |
| 8 | Basantapur tower (Nautalle) | n6348849285 (unnamed) | 27.704008, 85.307802 | ≈ 10 × 10 [E] | ≈ 30 (9 storeys) [E] | 9-storey tower (1770) [V] | tile, top pavilion | — (palace) | faces 180° onto Basantapur [V] |
| 9 | Jagannath | w169429518 | 27.704647, 85.307220 | 14.7 × 14.3 [O] | ≈ 15 [E] | 2-tier on 3-stage plinth (1563) [V] | tile | Jagannath (Vishnu) | ≈ 270° [V] |
| 10 | Shiva-Parvati | w185882281 | 27.704413, 85.306492 | 15.8 × 10.9 [O] | ≈ 10 [E] | 2-storey rectangular house-temple (late 18th c.) | tile `#fc8c64` per OSM [O] | Shiva & Parvati (figures at the central upper window) | ≈ 190° (south, faces the square) [V] |
| 11 | Maju Dega | w183562980 | 27.704264, 85.306174 | 22.6 × 22.2 [O] | ≈ 25 [E] | 3-tier pagoda on **9-stage plinth** (1690) [S] | tile | Shiva (linga) | ≈ 90° (east) [V] |
| 12 | Trailokya Mohan Narayan | w1414593331 | 27.703958, 85.306275 | 13.1 × 12.5 [O] | ≈ 18 [E] | 3-tier on 5-stage plinth (1680) [V] | tile | Narayan (Vishnu); kneeling Garuda in front | ≈ 270° (west, toward Garuda) [V] |
| 13 | Kal Bhairav | w196261745 | 27.704725, 85.307092 | 5.8 × 5.6 platform [O] | relief ≈ 3.6 [E] | open-air stone relief (17th c.) | none (OSM roof tag wrong) | Kal Bhairav | faces ≈ 270° [V] |
| 14 | White (Seto) Bhairav | near Taleju Bell w183563646 | ≈ 27.70460, 85.30675 [E] | wall panel ≈ 3 × 3.5 [E] | mask ≈ 3.5 [E] | gilt mask behind wooden lattice (1794) [V] | — | Seto Bhairav | faces the square [V] |
| 15 | Krishna Mandir | w120443307 | 27.673623, 85.324964 | 14.3 × 14.2 [O] | ≈ 20 [E] | stone shikhara, 3 storeys, **21 pinnacles** (1637) [S] | stone | Krishna (Radha, Rukmini; Shiva 2nd, Lokeshwar 3rd floor) [S] | **90° (east)**, Garuda column in front [V] |
| 16 | Bhimsen (Patan) | w326472980 | 27.673873, 85.325182 | 11.5 × 11.4 [O] | ≈ 14 [E] | 3-storey house-temple with gilt balcony | tile + gilt | Bhimsen | ≈ 90° [V] |
| 17 | Vishwanath | w328903213 | 27.673711, 85.325134 | 12.6 × 11.4 [O] | ≈ 15 [E] | 2-tier pagoda (1627) [V] | tile `#C27C36` per OSM | Shiva | **90° (east)**, stone elephants at the east door [V] |
| 18 | Taleju (Patan) | w120443289 + parts | 27.673202, 85.325223 | tower 16.9 × 15.3, eave 18.3 [O] | **34** [O] | 3-tier on palace tower | tile/red, gilt finial | Taleju Bhawani | ≈ 270° (onto square) [V] |
| 19 | Sundari Chowk + Tusha Hiti | n10034234966 (Tusha Hiti) | 27.672722, 85.325134 | courtyard ≈ 25 × 25 [E] | 2–3 storeys | palace courtyard, sunken royal bath (1647) [V] | tile | — | — |
| 20 | Golden Temple (Hiranya Varna Mahavihar, Kwa Bahal) | r4624856 | 27.675224, 85.324602 | 26.1 × 25.0 [O] | shrine ≈ 15 [E] | bahal with 3-tier gilt shrine (1409) [S] | gilt copper | Shakyamuni Buddha | entries east (stone) and west (wood) [S]; shrine faces ≈ 90° [V] |
| 21 | Kumbheshwar | n1759661519 (temple), w81147178 (complex) | 27.676581, 85.326046 | complex 80.6 × 59.2 [O] | ≈ 25 [E] | **5-tier** pagoda (1392) [S] | tile, gilt finial | Shiva (Nandi facing) | [V] |
| 22 | Mahabouddha | n564129617 | 27.668998, 85.327141 | ≈ 9 × 9 [E] | ≈ 18 [E] | terracotta shikhara, >9,000 Buddha images (1585) [S] | terracotta | Buddha | [V] |
| 23 | Nyatapola | w85470341 + 35 parts | 27.671410, 85.429373 | 26.8 × 21.7 with stair [O] | **30** (Wikipedia 33.2) [S] / OSM parts 31.7 + finial | **5-tier on 5 plinths** (1702) [S] | tile `#800000` [O] | Siddhi Lakshmi [S] | **180° (south)** onto Taumadhi [O+photo][V] |
| 24 | Bhairavnath (Taumadhi) | w185746728 | 27.671094, 85.429470 | 16.9 × 14.6 [O] | ≈ 20 [E] | 3-tier rectangular pagoda | tile | Bhairav (Kasi Vishwanath) | ≈ 270° (west, onto square) [V] |
| 25 | 55-Window Palace | (no footprint) | ≈ 27.67235, 85.42830 [E] | ≈ 50 × 12 [E] | ≈ 12 [E] | 3-storey palace, 55 carved windows (1697) [V] | tile | — | faces 180° (south) [V] |
| 26 | Golden Gate (Lun Dhoka) | n11365076869 (unnamed) | 27.672172, 85.428616 | gate ≈ 4 wide [E] | ≈ 5.5 [E] | gilt torana gate (1754) [V] | gilt | Taleju (Kali/Garuda on torana) | faces 180° [V] |
| 27 | Vatsala Durga | w211082585 | 27.672191, 85.428777 | 7.3 × 6.1 [O] | ≈ 14 [E] | stone shikhara (rebuilt 2017–21) [S] | stone | Vatsala Durga | ≈ 180° [V] |
| 28 | Pashupati (Bhaktapur) | n12293366201 | 27.671866, 85.428449 | ≈ 12 × 12 [E] | ≈ 15 [E] | 2-tier pagoda (15th c.) [S] | tile | Shiva (replica of Pashupati) [S] | [V] |
| 29 | Dattatreya | n11365112169 | 27.673539, 85.435359 | ≈ 12 × 12 [E] | ≈ 18 [E] | 3-tier pagoda (1427) [V] | tile | Dattatreya (Brahma-Vishnu-Shiva) | ≈ 270° (west onto Tachupal, w1192828078 46 × 22.8) [V] |
| 30 | Peacock Window (Pujari Math) | — | ≈ 27.6732, 85.4356 [E] | window ≈ 1.2 × 1.0 [E] | — | carved window in a lane wall | — | — | faces lane [V] |
| 31 | Changu Narayan | w186264410 | 27.716347, 85.427897 | 15.0 × 14.8 [O]; compound 74.5 × 68.7 [O] | ≈ 15 [E] | 2-tier pagoda, plinth ≈ 1.2 m [S] | **gilded copper** [S] | Vishnu | **270° (west)**, Garuda kneels before the west door [S] |
| 32 | Dharahara (rebuilt) | n11622074774, parts w1413193272/4, w1414016053 | 27.700545, 85.312169 | base 14.2, shaft 10.3, top 9.3 [O] | **72** [S] (OSM parts 63) | 22-storey tower | white, cone top + bronze mast | — (civic) | entrance [V] |
| 33 | Budhanilkantha | w85552443 | 27.778126, 85.362341 | compound 48.1 × 42.8 [O] | statue 5 m long [S] | reclining Vishnu in a **13 m** tank [S] | open sky | Vishnu (Jalashayana) | [V] |
| 34 | Dakshinkali | n360060921 | 27.605258, 85.263489 | ≈ 15 × 10 [E] | canopy ≈ 4 [E] | open-air shrine in a gorge, gilt canopy | gilt | Kali | [V] — sacrifice never shown |
| 35 | Bajrayogini (Sankhu) | w235411212 | 27.743819, 85.467239 | complex 78.0 × 60.5 [O] | ≈ 15 [E] | 3-tier pagoda (1655) [S] | tile + gilt | Vajrayogini / Ugratara | [V] |
| 36 | Bagh Bhairab (Kirtipur) | w317754743 | 27.679407, 85.276472 | courtyard 35.5 × 31.0 [O] | ≈ 15 [E] | 3-storey; 2 tile roofs + top gilt copper [S] | tile + gilt | Bhairav as tiger | [V] |
| 37 | Rato Machhindranath (Bungamati) | w363529870; bahal w442472145 | 27.629655, 85.302118 | 11.2 × 10.8; bahal 53.7 × 52.3 [O] | ≈ 20 [E] | shikhara in square (collapsed 2015, rebuilt) [S][V] | brick/plaster | Rato Machhindranath (Karunamaya) | [V] |
| 38 | Kopan Monastery | w206085866 (main gompa) | 27.742542, 85.364241 | 30.4 × 19.8 [O] | ≈ 15 [E] | Tibetan gompa on hill | flat + gilt ornaments | Tibetan Buddhist (Gelug, FPMT) [V] | ≈ 180° [V] |
| 39 | Kathesimbhu (Thamel) | n3377725834 | 27.709542, 85.309756 | ≈ 15 [E] | ≈ 12 [E] | small replica of Swayambhu (c. 1650) [V] | whitewash, gilt | Buddhist; Harati shrine n4582884189 next to it [O] | — |

---

## 4. Per-hero specs

Each spec gives the build recipe in the order a generator would execute it. "Enterable" means the compound/plinth/courtyard is walkable (owner decision), never the sanctum (§7).

### 4.1 Boudhanath Stupa (बौद्धनाथ स्तूप) — UNESCO 121-005

* **Anchor:** stupa w56688295 centroid 27.721436, 85.362004; kora wall w56688296 (95.8 × 95.5 m, `barrier=wall`); main gate n5302729521 at 27.720814, 85.361817, ≈ 70 m south of centre, "Traditional Buddhist gateway adorned with prayer flags" [O]. Plan axes: OSM long-axis bearings 99° / 44° ⇒ cardinal-aligned square plan (the 44° is the diagonal of a near-square) [O].
* **Plan** [O]: terrace 1 81.5 × 79.5; terrace 2 62.5 × 61.7; terrace 3 50.2 × 49.8; drum 38.0; dome 33.5; harmika 7.0. Terrace plan **20-cornered** [S: sacredsites / studiomatrx].
* **Elevation** (36 m total [S]; vertical split per §2.3) [E]: terrace tops 4 / 8 / 10.5 m [O]; dome to 21 m; harmika 21–25; 13 gilt rings 25–33; parasol to 34.5; finial to 36.
* **Details:** eyes on 4 faces of the harmika, nose = "१", urna dot; gilt torana panel above each pair of eyes; 108 small Amitabha images around the base [S: search result citing triptotemples]; prayer wheels set in the base/kora wall (count [V]); prayer-flag lines spire → terrace edges in radial fans; saffron lotus-petal arcs on the dome for festival layer.
* **Colours:** dome `s.whitewash`; terrace walls white with OSM roof `#ECCCA0` (cream) [O]; spire `s.gilt`; harmika base white with gilt torana.
* **Surroundings:** ring of 3–5-storey houses, shops and gompas on the kora; Old Dhabzang w209312611, Sakya Maitreya Lhakang w209530023 [O]. Pigeons.
* **Rules:** clockwise kora; no vehicles inside the wall; terrace 1 walkable (accurate: people walk the lowest terrace).
* **History note:** damaged 25 Apr 2015, reopened 22 Nov 2016 [S].

### 4.2 Swayambhunath (स्वयंभुनाथ) — UNESCO 121-004

* **Anchor:** stupa w201223707 27.714931, 85.290391; religious landuse w115379177 129.7 × 100 m (ele 1293 m); protected area w256548640 [O].
* **Stupa:** dome Ø 26.7 m (OSM circle area 557 m² ⇔ Ø 26.6) [O]; dome rise 11 m on 1 m drum [O]; harmika 15.1–19 m with eyes + gilt torana panels; 13 rings (Trayodashabhuvana) [S] 18–30 m pyramidal (base 6.5); parasol 26.2–28 m (Ø 4.0); finial to 33 m [O]. Five Dhyani Buddha shrines set into the dome at the cardinal points (+1) as gilt niche fronts [S].
* **Shikharas:** **Anantapur** w255752871 (8.8 × 8.7, h 17, white, SE of the stupa) and **Pratappur** w115376623 (7.9 × 7.8, h 17, white, NE) [O]; height/base ≈ 2.0.
* **Stairway:** eastern stairway of **365 stone steps** [S: nepalhikingteam / thirdrock], built under Pratap Malla (17th c.) [S]. Rise ≈ 0.18–0.22 m ⇒ ≈ 70 m vertical [E]; total length ≈ 300 m with landings [E]; stone Buddha statues and animal vahanas flanking the lower flight [V]. At the top: the giant gilt **vajra (dorje)** on a drum pedestal (≈ 3 m long) [E], immediately east of the stupa.
* **Harati (Hariti) temple:** w257581957 6.6 × 6.5 m, 27.715045, 85.290210 — 13 m N, 18 m W of the stupa (NW) [O]; small 2-tier pagoda with gilt roof [V]. Hindu-Buddhist protector of children.
* **Monkeys:** rhesus macaques, 30–60 visible at once on the hill [E]; behaviour per ASSET_MANIFEST §10 (playful, never fed by player).
* **Other:** Karmaraja Mahavihar w115376616 [O]; Shantipur w257736697 [O]; prayer-wheel ring on the stupa base; butter-lamp houses.

### 4.3 Pashupatinath (पशुपतिनाथ)

* **Anchor:** main temple w913170315 27.710465, 85.348665, 19.5 × 19.2 m plinth; religious landuse w125634570 242.3 × 168.1 m [O]. Pancha Devalaya r1574190 [O].
* **Form:** square **two-tier pagoda**, roofs of **copper with gold covering** [S], height **23.7 m** base→pinnacle [S]; **four main doors covered with silver sheets** [S]. OSM parts: sanctum 8.7 × 8.3 to 14.5 m; roof 1 eave 18.5 m (9–11.5 m); roof 2 eave 13.3 m (14.5–19 m); gajur ≈ 1.8 m [O]. Use 23.7 m total by scaling the OSM parts ×1.1 above 9 m [E].
* **Details:** gilded **Nandi** (bull) facing the west door, seen from behind [V]; gilt trident; gilt pataka on the front.
* **Ghats and river:** the Bagmati runs past the east side of the temple compound; Arya Ghat stone steps on the west bank; footbridges; east bank terraces with rows of small white Shiva-linga shrines (pancha-deval style) on the hill [V].
* **Rules:** see §7 (non-Hindu entry, no cremations/bodies/fires).

### 4.4 Kathmandu (Basantapur) Durbar Square

Square r21291455 (sett paving, 238.6 × 193.6 OBB, `fee=yes`) [O].

* **Taleju Temple** — plinth w1414600808 47.7 × 45.3 m; landuse w196261746 74.6 × 66.3 m [O]. **35 m**, **12-stage plinth**, **three gilded roofs**, built **1564** by Mahendra Malla [S: shankerhotel / Wikipedia]. Walled compound on top of the plinth with a gate; opened once a year at Dashain [S]. Build: 12 plinth steps at ≈ 0.75 m (9 m) [E]; compound wall on step 12; temple body ≈ 0.4 × plinth (≈ 18 m eave) [E]; 3 gilt tiers ratio 0.74.
* **Kasthamandap** — w183558418, 21.8 × 21.4 m [O]. Three-storey open wooden mandapa, collapsed 2015, rebuilt and completed 2021 [S: sharesansar]; core foundation 12 × 12 m [S]. Ground floor open on all sides (public shelter: genuinely enterable), Gorakhnath shrine at the centre [S], small Ganesh shrines at the four corners [V]. Height ≈ 20 m [E].
* **Hanuman Dhoka** — gate at ≈ n11365076769; stone Hanuman (1672) wrapped in red cloth under an umbrella, face covered in red-orange paste, flanked by two stone lions; gilt door [V]. Palace complex behind with Nasal Chowk courtyard (enterable courtyard; museum interiors not modelled).
* **Kumari Ghar** — n2659104413. 3-storey Newar courtyard house, built 1757 by Jaya Prakash Malla [V], richly carved windows, two white stone lions at the door. **The Kumari is never shown; windows stay closed** (ASSET_MANIFEST). Courtyard enterable in real life by visitors (no photos of the Kumari); in game allow the courtyard, no figure.
* **Basantapur tower (Nautalle)** — ≈ n6348849285. Nine-storey tower of the Basantapur palace, 1770 [V]; restored after 2015 [V]. Tiered sloped roofs on the upper storeys, latticed windows. Not enterable above ground floor (stairs too narrow in real life) [E].
* **Jagannath** — w169429518, 14.7 × 14.3 m. Two-tier, three-stage plinth, 1563 [V]. Struts erotic in reality → generic stylised.
* **Shiva-Parvati** — w185882281, 15.8 × 10.9 m, OSM 2 levels, colour `#fc8c64`, `roof:material=slate`, `complex_regular` roof [O]. Rectangular house-temple; carved white-painted Shiva and Parvati figures look out of the central upper window [V]; stone lions at the door; on a 2-stage plinth [E].
* **Maju Dega** — w183562980, 22.6 × 22.2 m, `#9A5A32` brick [O]. **Nine-stage ochre brick plinth, three tiers, 1690**, Shiva linga [S: Lonely Planet / backpackandsnorkel]. Destroyed 2015; reconstruction state as of 2026 [V]. Plinth steps are a favourite seating spot (sit interaction).
* **Trailokya Mohan Narayan** — w1414593331, 13.1 × 12.5 m [O]. Three tiers, five-stage plinth, 1680; kneeling stone Garuda statue (1689) facing it [V]; collapsed 2015, rebuilt [V].
* **Kal Bhairav** — w196261745 platform 5.8 × 5.6 m [O]. Open-air six-armed stone relief ≈ 3.6 m tall, painted black/blue body with gilt crown and yellow accents [E][V]. Cartoon-fearsome, no gore, no blood offerings shown.
* **White (Seto) Bhairav** — huge gilt mask on the palace wall near the Taleju Bell (w183563646), hidden behind a wooden lattice except during Indra Jatra [V].
* Others with OSM footprints: Gopinath w169429514 (10.4 × 10.2), Kageshwor w169429525 (11.0 × 10.8, red, eave 4–8.2 m), Narayan w111984927, Laxmi Narayan Sattal w183559497, Simha Sattal w183559499, Maru Hiti n4520340222, Garuda n2084347899, Maru Ganesh n2084347901 [O].

### 4.5 Patan Durbar Square

Square r4557971 (129.6 × 57.1 OBB, paving stones, `heritage:operator=whc`); the landmark anchor should move from node n2066253623 to r4557971 (CONTENT_COVERAGE D5) [O].

* **Krishna Mandir** — w120443307, 14.3 × 14.2 m stone, OSM "base step 1" [O]. Built **1637** by Siddhi Narasimha Malla, granthakuta shikhara of carved stone, **21 gilt pinnacles**, **3 storeys** (Krishna with Radha and Rukmini on the 1st, Shiva on the 2nd, Lokeshwar on the 3rd) [S: nepaltraveller]; restored 2018 [S]. Faces **east** toward the palace, with a Garuda on a pillar in front [V]. Build: 3-step plinth (1 m) → arcaded ground floor (≈ 4 m, 5 bays per side) → storey 2 with 8 pavilions → storey 3 with 4 pavilions + central tower → amalaka + gilt kalasha; total ≈ 20 m [E]. Mahabharata frieze on the 1st-floor beam, Ramayana on the 2nd [S] → decal bands.
* **Bhimsen** — w326472980, 11.5 × 11.4 m [O]. Three-storey house-temple with a gilt upper balcony facade [V].
* **Vishwanath** — w328903213, 12.6 × 11.4 m, OSM colour `#C27C36` [O]. Two-tier Shiva temple, 1627 [V]; stone elephants with riders guard the east door, Nandi on the opposite side [V].
* **Taleju (Patan)** — w120443289 + parts [O]: palace block to 13–17.5 m, roof 1 eave 18.3 m (17.5–22 m), roof 2 eave 13.5 m (23.5–26.5), roof 3 eave 9.4 m (28.5–31), gilt finial to **34 m**. Colours: walls red, roofs brown/red, finial gold [O]. Plus Taleju Bhawani w120443314 (7.0 × 6.6) [O].
* **Hari Shankar** — w120443315 / w330219342 (3rd roof 14.5–17.5 m) [O]; **Chyasim Deval** w326385650 (9.8 × 9.7, octagonal stone shikhara [V]); **Bhaidega** w330216628 (19.8 × 14.4) [O].
* **King Yoganarendra Malla column** n2097740385 (gilt king kneeling on a lotus atop a stone column, with a cobra hood behind) [O][V]; **Taleju Bell** w199775523 (bell pavilion 12.6 × 9.0, eaves 2.3–4.4 m) [O].
* **Sundari Chowk / Tusha Hiti** — Tusha Hiti n10034234966 (1 spout) [O]: sunken royal bath with stepped sides lined with small stone deity figures, a gilt spout [V]. Mul Chowk and Keshav Narayan Chowk are the other two palace courtyards (enterable courtyards).
* **Manga Hiti** n10034234987 (3 spouts) [O] beside the Mani Mandap pavilions; Bhandarkhal tank w199780880 (16.7 × 15.6) [O].

**Patan, beyond the square:**

* **Golden Temple (Hiranya Varna Mahavihar, Kwa Bahal)** — r4624856, 26.1 × 25.0 m courtyard [O]. Current form **1409** [S]; three-tier **gilt-copper** shrine roof on the west side of the courtyard [V]; entry through an ornate stone doorway on the east or a wooden doorway on the west [S]; gilt pataka, rows of prayer wheels, central small shrine (Swayambhu-style chaitya) in the courtyard, guardian lions/elephants. Real rule: no leather inside the courtyard [V] — show a shoe/leather sign, do not force.
* **Kumbheshwar** — temple n1759661519, complex w81147178 80.6 × 59.2 m [O]. One of only two free-standing **5-tier** temples; built c. **1392** by Jayasthiti Malla; Shiva, with a large Nandi facing it; two hiti ponds in the complex said to be fed from Gosaikunda [S]. Height ≈ 25 m [E]; tiers ratio 0.76 [E]; gilt finial and pataka [V].
* **Mahabouddha** — n564129617 (no footprint; CONTENT_COVERAGE lists this as missing heritage) [O]. Terracotta shikhara built 1585, modelled on Bodh Gaya's Mahabodhi, covered in **> 9,000** moulded Buddha tiles [S: masumihayashi]. Hemmed into a tight courtyard; height ≈ 18 m [E]; base ≈ 9 × 9 m [E]; terracotta `#B5603E` with small repeated relief texture.
* **Rato Machhindranath (Ta Bahal, Patan)** — w206482247, 75.3 × 62.6 m compound [O]; 3-tier pagoda (the deity's other home) [V].

### 4.6 Bhaktapur: Durbar Square, Taumadhi, Dattatreya

Durbar Square w1192827651 (44.6 × 24.5 core, `place=square`; no `heritage` tag) [O].

* **Nyatapola** — w85470341 (26.8 × 21.7 incl. stair, long axis 20°) + 35 parts [O]. **Five-tier pagoda on a five-level plinth**, completed **15 July 1702** (Bhupatindra Malla), Siddhi Lakshmi [S: Wikipedia]. Height: widely quoted **30 m**; Wikipedia 33.23 m; OSM parts to 31.7 m + finial ⇒ build **31.7 + 1.5 m finial = 33.2 m** to match Wikipedia, or 30 m for the "widely quoted" figure [O/S]; choose 33.2 (consistent with parts). Plinth steps 24.2 → 21.7 → 18.1 → 15.4 → 10.9 m [O]; use 5 × 1.4 m rises (plinth top ≈ 7 m) [E]; sanctum 10.9 × 9.4 m [O]. Roofs per §2.1 table; tile roofs `#800000` [O]. **529 bells** (48/80/104/128/168 top→bottom) [S]. Stairway on the **south** side rising from Taumadhi, flanked at each of the five plinth levels by pairs, bottom to top: **two wrestlers (Jaya and Patta), two elephants, two lions, two griffins (sardula), two goddesses Simhini and Vyaghrini** [S]; each guardian is said to be ten times as strong as the one below [V]. Ganesh images at the plinth corners [S]. Bricks: 1,135,350 + 102,304 for the plinths; 1,528 stones [S] (trivia for the discovery card).
* **Bhairavnath** — w185746728, 16.9 × 14.6 m [O]. Three-tier **rectangular** pagoda on Taumadhi's east side, ≈ 35 m SSE of Nyatapola [O]; front faces west onto the square [V]; small window through which offerings are passed; gilt pataka [V].
* **55-Window Palace** — no OSM footprint [O]. Three-storey brick palace, 55 carved wooden windows in a row on the upper floor, east of the Golden Gate [S: Lonely Planet]; c. 1697 [V]. Build ≈ 50 × 12 × 12 m [E], window pitch ≈ 0.9 m, facade faces south onto the square.
* **Golden Gate (Lun Dhoka / Sun Dhoka)** — ≈ n11365076869 [O][V]. Gilt gate set into a **bright red gatehouse surrounded by white palace walls** [S: Lonely Planet]; 1754 (Jaya Ranjit Malla) [V]; gilt torana with Taleju/Kali and Garuda at the apex [V]. Gate ≈ 4 × 5.5 m [E]. Leads to the Taleju courtyard (Mul Chowk n12289962101) which is closed to non-Hindus in real life [V] → courtyard beyond the gate not modelled.
* **Vatsala Durga** — w211082585 "Vatsala Shikhara", 7.3 × 6.1 m [O]. Stone shikhara, 1672 or 1727, destroyed 2015, rebuilt 2017–2021 [S: Lonely Planet]. Height ≈ 14 m [E]. The bronze "barking bell" (Taleju bell, 1737 [V]) stands in front on a stone pavilion.
* **Pashupati (Bhaktapur)** — n12293366201 [O]. Two-tier replica of Pashupatinath, Yaksha Malla, 1475 [S: Lonely Planet]; erotic struts → generic.
* **Siddhi Lakshmi** w211082584, **Kedarnath** w211080703, **Krishna** w197122572, **Fasidega (Shilu Mahadev)** w185750794 (21.8 × 21.6 plinth; white plastered shikhara on a 6-stage plinth with elephants [V]), **Chyasing Mandap** w495632745 (7.8 × 7.8), **Ta Dhi Chhen bahal** w185746826, **Dabu** n9871425646, lion statue n1942507475 [O].
* **Dattatreya** — n11365112169 at the east end of **Tachupal Tole** (w1192828078, 46.0 × 22.8 m) [O]. Three-tier pagoda, 1427 (Yaksha Malla), legend: built from the timber of a single tree [V]; the same pair of wrestlers (Jaya, Patta) as at Nyatapola guard its stair [V]; faces west down the square [V]. Bhimsen temple at the west end of Tachupal [V].
* **Peacock Window** — on the Pujari Math lane south-east of Dattatreya [V]; a carved latticed window with a fanned peacock, ≈ 1.2 × 1.0 m [E]. Render as a decal panel on a brick wall.

### 4.7 Other valley heroes

* **Changu Narayan** — w186264410 (15.0 × 14.8, `ref:whc` 121-007), compound w706922152 74.5 × 68.7 m [O]. **Two-tier pagoda with gilded copper roof and pinnacle** [S]; Vishnu; main **west gate** with chakra, conch, lotus and sword symbols [S]; four entrances guarded by pairs of lions, sarabhas, griffins and elephants [S]; plinth ≈ 1.2 m (4 ft) [S]. Life-size kneeling **Garuda** facing the west door; the AD 464 Manadeva stone pillar inscription in front [S]. Ridge-top village with a stone-paved lane up from the east [V].
* **Dharahara (2021 rebuild)** — n11622074774; parts: base ring w1413193274 14.2 m (h 1), shaft w1414016053 10.3 m (1–61 m), top w1413193272 9.3 m (58–63 m, cone roof 3 m), all white [O]. Official: **72 m, 22 storeys**, two lifts, inaugurated 24 Apr 2021, fully opened 19 Sep 2024 [S: Wikipedia]; 330 kg bronze pinnacle installed Apr 2021 [S: sharesansar]. Build: tapering white fluted shaft with a circular balcony near the top, cone cap, bronze mast; scale OSM parts to 72 m incl. mast (OSM to 63 m + ≈ 9 m mast/pinnacle region) [E]. Old Dharahara stump memorial n3498367331 next to it [O]. Sundhara spout pit w70954208 to the north-east [O].
* **Budhanilkantha** — w85552443, compound 48.1 × 42.8 m [O]. **5 m** reclining Vishnu of one block of black basalt in a **13 m** recessed tank [S], lying on the coils of the cosmic serpent (multi-hooded Shesha; hood count [V]), holding chakra, club, conch and gem [S]. Open sky, no roof. Real rule: the King of Nepal was barred from seeing it — no game impact. Tank rim stone, water dark green; devotees on a walkway reaching the head. Head direction [V].
* **Dakshinkali** — n360060921 ("sacrificial altar" comment) 27.605258, 85.263489 [O]. Open-air shrine at the meeting of two streams at the bottom of a forested ravine, reached by a long stair with stalls; a gilt canopy with gilded serpents over black stone images [V]. **No sacrifice, blood or animals being led shown** (9+ rating). Ambient bells only.
* **Bajrayogini (Sankhu)** — w235411212 complex 78.0 × 60.5 m [O], above Sankhu town in forest, stone stairway up. Three-tier pagoda built **1655** by Pratap Malla [S] (note: source says "Raja Prakash Malla" [V]); gilt roof parts [V].
* **Bagh Bhairab (Kirtipur)** — w317754743 courtyard 35.5 × 31.0 m [O]. Three-storey temple, probably 16th c., in a brick-paved rectangular courtyard with rest houses around; **two tile roofs + top roof of gilt copper** [S: kirtipurmun.gov.np via search]. Bhairav in tiger form (clay image) [V]. Old swords/shields on the facade (historical trophies) — render as neutral ornaments [V].
* **Rato Machhindranath (Bungamati)** — w363529870 (11.2 × 10.8) in Machhindranath Bahal w442472145 (53.7 × 52.3) [O]. Large **shikhara** in the village square, home of the deity six months a year; collapsed 2015 [S: thelongestwayhome]; reconstruction state 2026 [V]. Karya Binayak temple complex w448912454 nearby [O].
* **Kopan Monastery** — main gompa w206085866 30.4 × 19.8 m; nunnery w210724282 42.4 × 23.9 m [O]. Hilltop Tibetan Gelug monastery north of Boudha (FPMT) [V]; gompa archetype §2.6, stupa field, prayer-flag lines across the garden.
* **Kathesimbhu (Thamel/Kathmandu)** — n3377725834 (`tower:type=stupa`) [O]. Small copy of Swayambhu (c. 1650) in a courtyard; Harati shrine n4582884189 beside it [O]. Dome Ø ≈ 10–12 m [E]; parametric stupa "m" with a harmika and 13 rings.

---

## 5. Height and orientation quick reference for the generator

| Hero | Use height (m) | Source of height | Plinth height (m) | Tiers | Yaw (°) |
|---|---|---|---|---|---|
| Boudhanath | 36 | [S] | 10.5 (3 terraces) [O] | — | 180 gate |
| Swayambhunath | 33 | [O] | 1 | — | 90 stair |
| Pashupatinath | 23.7 | [S] | ≈ 1.5 [E] | 2 | 270 |
| Taleju KTM | 35 | [S] | ≈ 9 (12 steps) [E] | 3 | 190 [V] |
| Kasthamandap | 20 | [E] | ≈ 1 [O] | 3 | open |
| Maju Dega | 25 | [E] | ≈ 6 (9 steps) [E] | 3 | 90 [V] |
| Trailokya Mohan | 18 | [E] | ≈ 3 (5 steps) [E] | 3 | 270 [V] |
| Jagannath | 15 | [E] | ≈ 1.5 (3 steps) [E] | 2 | 270 [V] |
| Krishna Mandir | 20 | [E] | ≈ 1 [O] | shikhara, 21 pinnacles | 90 |
| Vishwanath | 15 | [E] | ≈ 1.3 [O] | 2 | 90 |
| Taleju Patan | 34 | [O] | (palace block 17.5) | 3 | 270 [V] |
| Kumbheshwar | 25 | [E] | ≈ 1.5 [E] | 5 | [V] |
| Nyatapola | 33.2 | [S]/[O] | ≈ 7 (5 levels) [E] | 5 | 180 |
| Bhairavnath | 20 | [E] | ≈ 1 [E] | 3 | 270 [V] |
| Changu Narayan | 15 | [E] | 1.2 [S] | 2 | 270 |
| Dharahara | 72 | [S] | 1 [O] | 22 storeys | [V] |
| Golden Temple shrine | 15 | [E] | ≈ 1 [E] | 3 | 90 [V] |
| Bajrayogini | 15 | [E] | ≈ 1.5 [E] | 3 | [V] |
| Bagh Bhairab | 15 | [E] | ≈ 1 [E] | 3 | [V] |

Rule for every unnamed tiered temple with `building:part` data: trust the parts' `min_height`/`height` and roof heights (they are consistent with sources where we could check), but ignore any `height < 3` on the outline itself.

---

## 6. Generator checklist (what code must do)

1. **Pagoda**: `plinth(n, widths from OSM parts or 0.06–0.09 inset) → core(0.45 × base) → for each tier i: eave = wEave1 × 0.74^i, rise per §2.1, straight pitch with ≤ 5 % corner lift → struts (count formula) → bells (0.4 m) → gajur (0.10–0.15 × H) → optional pataka → lions at the main stair → torana above the door`. Yaw from §3.
2. **Shikhara**: stone granthakuta (pavilions) or plastered curvilinear; amalaka + kalasha.
3. **Stupa**: 0/1/3 twenty-cornered terraces, dome rise 0.33–0.42 Ø, harmika with eye decals, 13 rings, parasol, finial, flag lines, wheel niches.
4. **Chaitya**: 4 niches with the directional Buddhas in the correct order (E Akshobhya, S Ratnasambhava, W Amitabha, N Amoghasiddhi).
5. **Bahal**: quadrangle with a gate opposite the shrine, courtyard chaityas.
6. **Hiti**: sunken pit, makara spouts = `number_of_water_spouts` or 1–3, flowing or dry.
7. **Pokhari**: stepped edges, optional island shrine + causeway.
8. **Shrines**: size from §1.2 distributions; kind from name (Ganesh, Bhimsen, Bhairav, Shiva) per §2.7.
9. **Height sanity**: OSM `height < 3` on temple outlines = plinth.
10. **Hide** the underlying OSM building when a hero is placed (CONTENT_COVERAGE B6).

---

## 7. Cultural rules (with the owner's "fully enterable" override)

The product owner overrode the earlier no-entry rule: temple **compounds** are fully enterable. Recommendations to keep that respectful and accurate:

| Rule | Recommendation | Status |
|---|---|---|
| Sanctum (garbhagriha) | Never enterable, never rendered inside; door shows a dark interior with a lamp glow. No deity image is modelled inside a sanctum | [V] |
| Pashupatinath main compound | In reality non-Hindus may not enter. Owner override applies to compounds; reviewer must decide whether the main courtyard is walkable. Safe default: east-bank terraces, ghats and outer shrines walkable; the main courtyard gate shows a respectful prompt | **[V] escalate** |
| Taleju (KTM, Patan, Bhaktapur Mul Chowk) | Real temples open once a year / closed to non-Hindus. Plinth walkable, compound gate closed | [V] |
| Kumari Ghar | Courtyard enterable; the Kumari is never shown; windows closed | ASSET_MANIFEST |
| Stupa kora | Clockwise only for NPCs; player not forced, but prayer wheels spin clockwise | ASSET_MANIFEST |
| Shoes / leather | Shoe racks at temple doors; Golden Temple "no leather" sign | [V] |
| Struts | Non-explicit stylised deity panels everywhere | ASSET_MANIFEST |
| Dakshinkali, Bhairav, Kali | No sacrifice, blood, weapons-in-use or gore; fierce faces in cartoon style are fine | 9+/PEGI 7 |
| Ghats | No cremations, bodies, or pyres; ghats shown as steps with diyas | ASSET_MANIFEST |
| Vehicles | No vehicles on temple plinths, compounds, stupa koras or heritage squares | CONTENT_COVERAGE L16 |
| Climbing | Player may climb plinth steps (people sit there) but not roofs or the stupa dome | [E] |
| Monkeys at Swayambhu | Never fed by the player, never aggressive | ASSET_MANIFEST |

---

## 8. Open verification list

1. Main-door yaw for every hero marked [V] in §3 (check with satellite imagery and reference photos; for Nyatapola, Changu, Pashupati, Krishna Mandir the stated yaw is high-confidence).
2. Post-2015 reconstruction status as of 2026: Maju Dega, Trailokya Mohan, Basantapur tower, Rato Machhindranath Bungamati, Vatsala Durga (done 2021), Kasthamandap (done 2021).
3. Boudhanath true height: 36 m (Wikipedia) vs OSM parts 51 m. Our spec uses 36 m.
4. Nyatapola height: 30 m (common) vs 33.23 m (Wikipedia) vs OSM parts 31.7 m + finial. Our spec uses 33.2 m.
5. Dharahara: 72 m official vs OSM parts 63 m (no mast). Our spec uses 72 m.
6. Prayer-wheel counts at Boudha and Swayambhu.
7. Taleju KTM, Kasthamandap, Kumbheshwar, Krishna Mandir heights (estimates).
8. Naga hood count at Budhanilkantha; Bagh Bhairab image; Golden Gate iconography.
9. Guhyeshwari: OSM position is wrong; place it from imagery.
10. Missing OSM footprints: Mahabouddha, 55-Window Palace, Golden Gate, Hanuman Dhoka gate, Kumari Ghar. Add `manual:` placements in `landmarks.yaml` (pipeline workflow, not this one).

---

## 9. Sources

OSM: © OpenStreetMap contributors, ODbL. All [O] values come from `pipeline/data/raw/osm/nepal.osm.pbf` (bbox 85.18–85.55 E, 27.55–27.82 N), extracted 2026-10-05 with pyosmium 4.3.1; OBB = shapely `minimum_rotated_rectangle` in a local equirectangular metre frame.

Web (facts only; photos are reference only, never shipped):

* Boudhanath, height 36 m, 2015 damage and 2016 reopening: https://en.wikipedia.org/wiki/Boudhanath
* Boudhanath ground plan > 300 ft, 108 Amitabha images: https://sacredsites.com/boudhnath.html ; https://triptotemples.com/blogs/nepal/all-about-boudhnath-stupa
* Boudhanath three 20-cornered terraces, 13 levels: https://www.studiomatrx.org/guides/boudhanath-stupa-architecture ; https://sacredsites.com/boudhnath.html
* Swayambhunath, 13 rings (Trayodashabhuvana), five Buddhas, Hariti, monkeys, renovation dates: https://en.wikipedia.org/wiki/Swayambhunath
* Swayambhunath 365 eastern steps, Pratap Malla: https://www.nepalhikingteam.com/swayambhunath-temple ; https://www.thirdrockadventures.com/blog/top-10-facts-of-swayambhunath
* Pashupatinath 23.7 m, two-level gilt copper roofs, four silver doors, 518 mini-temples: https://en.wikipedia.org/wiki/Pashupatinath_Temple (note: that page's "817 m" elevation is wrong; the valley floor is ≈ 1,300 m)
* Nyatapola guardians, 5 plinths, 5 roofs, 1702, 529 bells, bricks: https://en.wikipedia.org/wiki/Nyatapola_Temple ; https://www.bhaktapur.com/discover/nyatapola/
* Taleju KTM 35 m, 12-stage plinth, 1564, three roofs: https://www.shankerhotel.com.np/blog/2025/5/23/taleju-temple-the-sacred-heart-of-kathmandus-spiritual-legacy ; https://en.wikipedia.org/wiki/Taleju_Temple,_Kathmandu ; https://www.lonelyplanet.com/nepal/kathmandu/attractions/taleju-temple/a/poi-sig/387081/357144
* Kasthamandap: https://en.wikipedia.org/wiki/Kasthamandap ; https://www.sharesansar.com/newsdetail/rebuilding-of-kasthamandap-nears-final-phase-to-be-inaugurated-within-a-few-days-2021-11-23 ; https://nepalitimes.com/resurrecting-kasthamandap-from-the-rubble
* Maju Dega 1690, nine-stage plinth, three roofs: https://www.lonelyplanet.com/nepal/kathmandu/attractions/maju-deval/a/poi-sig/386989/357144 ; https://backpackandsnorkel.com/Nepal/Day2/52112c-MajuDegaTemple/
* Krishna Mandir 1637, 21 pinnacles, 3 storeys, friezes: https://nepaltraveller.com/sidetrack/krishna-mandir-a-marble-of-stone-carving ; https://en.wikipedia.org/wiki/Krishna_Mandir,_Patan
* Kumbheshwar 5 storeys, 1392, Nandi, ponds: https://en.wikipedia.org/wiki/Kumbheshwar_Temple
* Golden Temple 1409, gilt copper, east/west entries: https://en.wikipedia.org/wiki/Hiranya_Varna_Mahavihar ; https://www.thelongestwayhome.com/travel-guides/nepal/golden-temple-patan.html
* Mahabouddha 1585, > 9,000 Buddha images: https://masumihayashi.com/artwork/sacred-architectures/mahabuddha-temple-patan-nepal/
* Bhaktapur: Vatsala Durga, 55-Window Palace, Golden Gate, Pashupati: https://www.lonelyplanet.com/nepal/around-the-kathmandu-valley/bhaktapur/attractions/vatsala-durga-temple/a/poi-sig/1432547/357114 ; https://www.lonelyplanet.com/nepal/around-the-kathmandu-valley/bhaktapur/attractions/pashupatinath-temple/a/poi-sig/449981/357114
* Changu Narayan: https://en.wikipedia.org/wiki/Changu_Narayan ; https://www.lonelyplanet.com/nepal/the-eastern-valley/attractions/changu-narayan-temple/a/poi-sig/1172162/1327513
* Dharahara 72 m, 22 storeys, 2021/2024: https://en.wikipedia.org/wiki/Dharahara ; https://www.sharesansar.com/newsdetail/330-kg-pinnacle-installed-in-dharahara-2021-04-22
* Budhanilkantha 5 m statue, 13 m pool: https://en.wikipedia.org/wiki/Budhanilkantha_Temple
* Bagh Bhairab: https://kirtipurmun.gov.np/heritage/baghbhairav ; https://nepaltraveller.com/sidetrack/bagh-bhairab-temple-a-mystical-beauty-in-kirtipur
* Bajrayogini 1655: https://en.wikipedia.org/wiki/Bajrayogini_Temple ; https://www.windhorsetours.com/sights/bajrayogini-temple
* Bungamati Rato Machhindranath: https://en.wikipedia.org/wiki/Bungamati ; https://www.thelongestwayhome.com/travel-guides/nepal/kathmandu/bungamati.html
* Tundal (struts): https://nepaltraveller.com/sidetrack/tundal-in-nepalese-architecture
* Newar architecture overview: https://en.wikipedia.org/wiki/Newar_architecture
* Prayer flag colour order: https://en.wikipedia.org/wiki/Prayer_flag
* Further reading (not fetched; recommended for proportions): W. Korn, *The Traditional Architecture of the Kathmandu Valley* (1976) and *The Traditional Newar Architecture of the Kathmandu Valley: The Śikharas*; N. Gutschow, *Architecture of the Newars* (2011); KVPT (Kathmandu Valley Preservation Trust) reconstruction drawings.
