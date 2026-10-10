# W2 detail pass: reference brief for the sacred generators (package `temples`)

> Status: reference brief for the W2 detail pass (docs/W2_DETAIL_CONTRACT.md), 2026-10-10. It turns photos of the real
> monuments **as they stand now** (post-2015 rebuilds included) into modelling rules for `Core/Generators/Sacred/**`.
> Facts and proportions come from [temples.md](temples.md) (OSM-measured plans, heights, tiers, door yaws) and
> W2_DESIGN §3; this file adds what the photos show: silhouettes, part proportions, colours (hex, cartoon-boosted
> about +15 % saturation and +10 % value over the photo) and the distinctive details a Kathmandu local checks first.
> Photos were fetched with `/home/user/wt/refimg.py` from Openverse (Flickr, Wikimedia; open licences) into
> `/home/user/wt/refs/temples/<topic>/` and are **reference only**: never copied into the repo, never traced.
> Confidence tags as in temples.md: [O] OSM, [S] source, [E] estimate from photos, [V] needs a reviewer.

## 0. What makes each one read as itself (the checklist used for previews)

| Object | The five things a local checks first |
|---|---|
| Nyatapola | five ever-smaller **dark brown-red tile** roofs stacked very high; the steep stone stair up the **south** face of a five-step **salmon brick** plinth; **stone guardian pairs on every landing** (wrestlers, elephants, lions, griffins, goddesses, bottom → top); a ring of wooden posts (ambulatory) round the sanctum on top; a tiny gilt finial |
| Boudhanath | huge flattened white dome on three **white 20-cornered terraces**; the **gilt square harmika with the painted eyes** (white eye, blue iris, red lid line, black brows, the curly "१" nose); a red/yellow/green/blue **drapery valance** over the eyes; a **stepped square gilt pyramid** of 13 tiers; a gilt crown with a yellow cloth skirt; **hundreds of prayer-flag lines** fanning from the top to the terrace edges; **saffron lotus-petal arcs** painted on the dome |
| Swayambhu | white dome splashed with saffron; gilt harmika with eyes under a **gilt pentagonal torana panel on each face**; a **round, conical stack of 13 gilt rings** with dark grooves; gilt parasol with a yellow cloth skirt; **five gilt Buddha shrines** (multi-pinnacled) against the dome base; flags; the giant gilt vajra on the east stair head |
| Kasthamandap (rebuilt 2021) | a squat, very wide **three-roof timber hall**: a broad low first hip roof over an **open ground floor of many wooden posts** behind a low railing; a white band storey with small windows and a **wooden balcony gallery**; a second roof; a small third roof with a white/gilt finial; **red cloth fringes** along the eaves |
| Taleju (KTM) | three **gilt-copper roofs** (aged gold to bronze) high above a **12-step brick plinth**; a red walled compound with a white cornice and a **gilt torana gate** flanked by lions; small single-roof corner shrines; a big **gilt bell-shaped gajur with four small gilt finials** at its corners; upturned corner ornaments; red eave fringes |
| Krishna Mandir (Patan) | **grey-buff carved stone**; a 3-step plinth with stone lions/elephants at the east stair; an **arcaded ground floor** (cusped arches on slim pillars, 5 bays a side), a balcony with a frieze, a second arcade storey; **small domed pavilions (chhatris) with gilt finials** at corners and mid-sides; a **tall ribbed curvilinear spire** with amalaka and gilt kalasha |
| Pashupatinath | square two-tier pagoda whose **two roofs are gilt copper with standing-seam ribs** running down the slope; bright gilt gajur and trident; silver doors on four sides; white-plaster and brick compound; the big gilt **Nandi** facing the west door |
| Generic 2-tier pagoda (Patan type) | brick plinth of 2–4 steps with **stone lions on pedestals** flanking the stair; square sanctum of **dark glazed brick** with a carved wooden door, gilt torana and lattice windows; a ring of carved struts under each eave; brown-red tile roofs with deep overhang, slight corner lift; a small gilt finial; bells along every eave |
| Corner Ganesh shrine | a small **stone or brick box shrine** on a low platform, an **arched niche** (cusped arch) with an image **smothered in orange-red sindoor**, a pair of small stone lions, a stepped stone cap or a **single tiny tile roof with a gilt knob**, a brass bell hung beside it, marigold scraps |

## 1. Newar pagoda (dega): part rules from the photos

Sources: Nyatapola (refs/temples/nyatapola, struts), Patan square (patan), Maju Dega (maju), Taleju (taleju),
Dattatreya (dattatreya), Kasthamandap (kasthamandap). Proportions are fractions of the sanctum width `S` or of the eave
width `E` of that tier unless metres are given.

### 1.1 Plinth (pīṭha)
* Each level is a **plain brick wall** with a **projecting coping** at the top: a 2-course brick or stone cap that
  overhangs 0.08–0.15 m, and a slightly wider **base course** (0.15–0.25 m tall, 0.05 m proud). Nyatapola's coping is
  a lighter brick band; Maju Dega's is ochre brick throughout [E].
* Colours: Bhaktapur salmon brick `#C25A3F` (Nyatapola), ochre `#A9643A` (Maju Dega), Patan brick `#B8563E`; coping
  `#D27A5E` (brick) or stone `#A39A8C`.
* Tall levels (Nyatapola 1.4 m) show a **row of small square niches/vents** near the top on long faces (one per
  ~3 m) [E: refs nyatapola 01].
* Stair: grey stone `#8F8A82`, 0.18–0.25 m risers, flanked by **stone side walls (balustrade blocks)** 0.4–0.6 m wide
  whose tops step with the flight; on Maju Dega the side walls are **whitewashed** `#EEEAE0` [E: maju 01].
* Guardians stand on **stone pedestals** (0.5–0.8 m tall, moulded top and base) on the side walls at each landing.

### 1.2 Sanctum and ground storey
* Sanctum wall: **dark glazed brick** (dachi apa) `#9C3F2C`, framed top and bottom by **carved wooden bands**
  (`#3E2416`): a sill beam 0.2–0.3 m at the base and a cornice of 2–3 stepped beams (0.4–0.6 m in all) under the struts.
* Door: **1.0–1.4 m × 1.9–2.2 m**, deep carved frame (3 stepped jambs, 0.3–0.45 m in all), the **lintel extends past
  the jambs as "ears"** (0.25–0.4 m each side) and the sill likewise; dark leaves `#2A1A10`; the sanctum is never
  shown inside: the doorway is opaque dark with a lamp glow.
* **Torana** over the door: a gilt `#D9A62E` semicircular (often slightly pointed) tympanum, door width + 0.6 m, with a
  raised rim, a central deity relief and a kirtimukha knob at the apex.
* Windows: one **lattice window** (tiki jhya, square grid) each side of the door on bigger temples, 0.6–0.9 m square,
  dark carved frame with ears.
* **Ambulatory**: Nyatapola, Maju Dega, Dattatreya and most 3-tier temples have a **ring of wooden posts** round the
  sanctum on the top plinth (posts 0.2–0.3 m, ~1.6–2.2 m apart, carved capitals with brackets), a timber beam on top.

### 1.3 Roof tiers
* Eave: **deep overhang** (0.4–0.55 × S each side for the first tier, temples.md §2.1); the eave line is **straight**
  with the corners lifted **4–8 %** of the half width; the slope is nearly straight with a faint concave sag.
* Section: from the eave edge up: a **wooden fascia** board 0.15–0.25 m tall (dark wood, sometimes with a thin red or
  gilt edge), the roof surface, then the next storey wall. Under the eave: a dark **soffit** with 2–3 visible purlin
  beams parallel to the eave.
* Tiles: **jhingati** clay tiles in visible **horizontal courses** (0.25–0.35 m), colour per site: Nyatapola dark
  `#7A3528`, Patan/KTM brown-red `#8E4A36`, with moss patches `#6F6A3C` on old roofs; ridge/hip caps a darker
  `#5E2A1E` rounded roll. **Gilt roofs** (Taleju, Pashupati, Annapurna, Golden Temple, Changu) are copper sheets with
  **ribs running down the slope** (every ~0.5 m) and a gilt `#D9A62E` / aged `#B58A2E` finish.
* Corners: each eave corner carries an **upturned ornament** (a curled bird/serpent head, the *kunsala*), 0.4–0.7 m,
  dark wood or gilt.
* **Struts (tunāla)**: carved boards 0.18–0.28 m wide, 0.1–0.15 m thick, leaning out at 50–60° from the wall cornice to
  the eave purlin; a small figure or animal at the foot, a **standing deity** (painted: red, blue, green, gilt) on the
  main panel, a scroll capital at the top. Spacing ≈ 1.1 m; one larger **winged corner strut** (griffin) at each
  corner. Nyatapola: **108 struts** in all [S: bhaktapur.com].
* **Bells**: small bronze bells `#B08A3E` with a leaf-shaped clapper plate, hung from the fascia every ≈ 0.4 m
  (Nyatapola 168 / 128 / 104 / 80 / 48, bottom → top [S]).
* **Fringe**: Kathmandu Durbar Square temples (Taleju, Maju Dega, Kasthamandap, Jagannath) hang a **red cloth valance**
  `#C8202A` with a white edge `#F2EEE6` along every eave, 0.3–0.45 m deep, scalloped [E: taleju 09, maju 03,
  kasthamandap 00]. Bhaktapur and Patan temples mostly do not.
* Upper storeys: brick walls with a **band of carved windows or open lattice panels**; on Dattatreya and Kasthamandap a
  projecting **wooden balcony** with a lattice parapet.

### 1.4 Finial and banner
* **Gajur**: a gilt stack: a square stepped base on the top roof, a pot (kalasha), 2–3 stacked bell/lotus rings, a small
  parasol (chhatra) and a point; 0.10–0.15 × total height. Taleju KTM: a **big gilt bell** with **four small gilt
  finials** at the corners of its base and a gilt frame over it [E: taleju 01].
* **Pataka**: a long gilt strip 0.3–0.6 m wide from the finial down the front of every roof to above the door torana,
  made of linked plates [E][V].

### 1.5 Guardians (stone, grey `#8F8A80` or lime-washed `#F1ECE0` with red/green/yellow accents)
* **Newar lion** (simha), as the stair lions of Bhaktapur and Kathmandu stand (refs lions 00, 05, 09; nyatapola_guard
  00): **standing on four planted column legs** (not seated, no raised paw on the stair lions), a deep body, the chest
  high and the neck rising steeply so the **big boxy head is held up** with its muzzle tilted to the sky; a wide-open
  rectangular jaw with teeth and fangs, bulging eyes under heavy curled brows, a ruff of curls framing the face and
  **rows of hanging ringlets** down the back and sides of the neck (the mane, 4–5 rows); a **bell necklace**, spiral
  reliefs on the shoulders, **banded anklets** and big clawed paws; the tail curls up over the rump. 1.4–1.9 m without
  the pedestal. Carved in one block: no masonry joints on the figure. Painted lions (Hanuman Dhoka, Kumari Ghar) are
  lime white `#F1ECE0` with a green `#2F6E4F` mane and gilt trim.
* Elephant: standing, with a saddle cloth and a small rider/howdah block; trunk curled; tusks. On Nyatapola the cloth,
  ornaments and tusks are **carved in the same stone** (refs lions 06), not painted.
* Wrestlers (Jaya, Patta): kneeling/half-squatting heavy human figures holding maces (Nyatapola bottom, Dattatreya).
* Griffin (sardula): lion body with a short, hard-hooked eagle beak, curled horns and folded wings (refs
  nyatapola_guard 01).
* Goddesses (Simhini, Vyaghrini): seated female figures, the top pair on Nyatapola.

### 1.6 In front of the temple
* **Temple bell**: a bronze bell (0.4–1.0 m) hung from a timber or stone beam on two posts, sometimes under a tiny roof.
* **Lamp pillar** (deep stambha): a stone/brass column with a ring of lamp cups or a lotus top, 2–3 m.
* **Vahana column**: Garuda (Vishnu) kneeling on a lotus atop a stone column, or a seated gilt Nandi (Shiva) at the
  door; Dattatreya and Trailokya Mohan have Garuda; Pashupati the gilt Nandi.

## 2. Stupas

### 2.1 Boudhanath (refs/temples/boudha, boudha_eyes)
* Terraces: three **20-cornered** white `#F4F1EA` terraces, each with a rounded coping and a low parapet; wide
  stairs mid-side; a ring of **small white chortens** at the terrace corners [E].
* Base ring (drum): white plinth rings stepping in to the dome, with a band of **108 small niches** (Amitabha images)
  at the foot of the dome; around the outside the **kora wall with 147 niches of 4–5 prayer wheels** each [S:
  sacredsites, Lonely Planet].
* Dome (anda): flattened (rise ≈ 0.31 × Ø), whitewash `#F6F2E8`, painted with **saffron `#F2A93B` lotus-petal arcs**
  hanging from the harmika in scallops (about 16–24 petals) [E: boudha 07].
* Harmika: square, **gilt** `#D2A03A` brick-textured block; on every face a pair of eyes: white almond, blue iris
  `#2F5DA8`, black pupil, a **red lid line** `#C8282E` under the eye, black arched brows; the **"१" nose** (white with
  a red outline) between and below; a third eye (urna) dot above the nose.
* Valance: above the eyes a **ruffled drapery**: bands of blue, yellow, green over a big red ruffle `#D0232A`, hung
  from a gilt cornice.
* Spire: **13 stepped square gilt tiers** (a stepped pyramid), base 0.24 × Ø, top 0.12 × Ø.
* Crown: a gilt round **parasol with a yellow `#F2C230` cloth skirt**, a gilt lantern and finial with a small parasol.
* Flags: many strings from the parasol to the terrace rims, flags blue `#2E6FD8`, white, red `#D93A2B`, green
  `#2E9E4F`, yellow `#F2C230` in that order, sagging 8–12 %.

### 2.2 Swayambhunath (refs/temples/swayambhu)
* Dome: white with **yellow-saffron splash streaks** running down from the harmika.
* Harmika: gilt with eyes (no red lid lines; black outline), "१" nose; **a gilt pentagonal torana panel above each
  pair of eyes** with a seated Buddha relief; green and red drapery hung below the panels.
* Spire: **13 round gilt rings** in a cone (each ring a gilt band with a dark groove), then a gilt umbrella with a
  yellow skirt and a gilt pinnacle with a small parasol.
* Five **gilt Buddha shrines** against the dome base (multi-pinnacled gilt shrine fronts, 3–4 m tall), the east one
  over the stair; a **prayer-wheel gallery** round the base; the gilt **vajra** on a drum at the stair head; the white
  shikharas **Anantapur and Pratappur** beside the stupa.

### 2.3 Chaitya (Newar votive stupa)
* Grey stone `#8E8A80`: a square stepped base (2–4 steps with mouldings), a drum with **four arched niches** holding the
  four directional Buddhas (E Akshobhya blue, S Ratnasambhava yellow, W Amitabha red, N Amoghasiddhi green as painted
  accents), a small dome, a square harmika, a 13-ring cone and a parasol knob; sindoor and rice on the niches.

## 3. Shikharas

### 3.1 Krishna Mandir, Patan (refs/temples/krishna, patan)
* Stone `#A59682` weathered to `#8C7F6E` in recesses. Three-step plinth (≈ 0.33 m rises) with **stone lions** and two
  **elephants** at the east stair.
* Ground floor: an **arcade of 5 cusped arches per side** on slim square pillars, a deep corridor behind (dark), a
  moulded beam above with the **Mahabharata frieze** band.
* First floor: a **balcony with a stone balustrade**, a second arcade (smaller), **pavilions (chhatris)** at the four
  corners and four mid-sides: each a small square kiosk on 4 pillars with a **ribbed dome cap and a gilt finial**.
* Second floor: four corner pavilions and the **central shikhara**: a tall curvilinear spire with **vertical ribs**
  (rathas), an **amalaka** (ribbed disc) and a **gilt kalasha** + finial. 21 pinnacles in all [S].

### 3.2 Plastered shikharas (Anantapur, Pratappur at Swayambhu; Fasidega style)
* White plaster `#F4F0E6`, a stepped base, a cubic cella with small arched doors on 4 sides, then a **curvilinear spire
  with 12–24 vertical offsets** and horizontal ribs, an amalaka and a gilt finial; a red or saffron pennant [E].

## 4. Small shrines and rest houses

* **Ganesh corner shrine** (refs/temples/ganesh 03, 06, 09): stone box shrine 1.2–2.2 m wide on a 0.3–0.5 m platform;
  a deep arched niche (cusped) between plain pilasters, its head about half-way up the box, the image a rounded relief
  filling most of the niche and completely **smeared in sindoor** `#E0412B` with marigold `#F6A21B` garlands; a carved
  lintel band; small stone lions each side on the platform; an **overhanging cornice and a shallow hipped stone roof**
  with a stacked stone finial (ref_03), or a single small tile/metal roof with a gilt finial; weathered grey stone
  `#7E7A71`; a **brass bell** `#C9A13A` on a bracket.
* **Pati** (refs/temples/chaitya 09): a raised brick platform (0.4–0.9 m), a back wall, **3–5 carved wooden posts** with
  bracket capitals across the open front, a single-slope or hip tile roof with struts.
* **Bell pavilion** (Taleju bell, Patan): a big bronze bell hung under a single-roof pavilion on 4 posts/pillars.

## 5. Other heroes

* **Dharahara (2021)**: rebuilt "in the same style with a larger diameter" [S: Wikipedia], white `#F7F5EF`, a tapering
  round fluted shaft with storey bands, a **gallery balcony** ring near the top, a short drum and an onion/cone cap with
  the bronze `#8C6A3A` mast; 72 m [S].
* **Kal Bhairav**: open-air stone relief, black-blue body `#2A3550`, gilt crown, red and yellow accents; no roof.
* **Hanuman Dhoka**: stone Hanuman on a plinth **wrapped in a red cloth** with a **red parasol** over it, flanked by
  lions; the gilt door in a painted palace wall.
* **Golden Gate (Bhaktapur)**: gilt torana and doors set in a **bright red** gatehouse within white walls, a small tile
  roof over it.

## 6. How the generators use this brief (implementation notes)

* Every face carries a `MaterialChannel` (RoofTile, Gilt, WoodCarved, BrickGlazed, Brick, Stone, Plaster, Paint, Fabric,
  Metal, Plain) in UV0.x and a baked ambient-occlusion term in UV0.y (`SacredDraw.Bake`: ground fade, undersides,
  concavities), so the ToonLit shader picks the material texture and darkens soffits, niches and the space under eaves.
* Heroes keep their plan, tier count, heights and door yaw from `HeroCatalog` (W2_DESIGN §3.4). A recipe yaw (often a
  cardinal estimate, [V]) is snapped to the nearest axis of the OSM outline's oriented box (always within 45°) and the
  plan is measured in that frame, so the plinth, sanctum, storeys, roofs and stair are square to the mapped footprint
  (Pashupatinath: outline edges at 37.4° / 127°, door at 307.4°; sanctum 0.45 × the plinth width).
* Hero LOD budgets: centrepieces 36,000 / 9,000 / 2,000 / 200 triangles (LOD0-3), others 18,000 / 4,500 / 800 / 200;
  generic sacred buildings keep `SacredSelector.MaxTris` (6,000 / 3,000 / 800 / 200) by spreading their repeated detail
  (`DetailScale`: bells, struts, posts, tile courses, fringe scallops) with their size instead of dropping to LOD1 next
  to the camera. At LOD1 the bells, tile courses, gilt seams and soffit purlins drop out and half the struts are kept,
  each in a plain two-section form.
* Sculpture (guardians, Garuda) uses the stone tint on the `Plain` channel with a position colour jitter for weathering:
  the `Stone` channel draws ashlar joints, right for plinths and stairs but wrong on a figure carved from one block.
* Props in front (bell, lamp pillars, vahana) are NoClimb colliders and keep out of the road corridors; eave bells and
  fringes never hang into a corridor below 4.5 m (contract §1), using `SacredClearance` over the tile's roads until the
  roads package's `RoadCorridorIndex` is wired in.
* Self-check previews (photo vs render from the same angle) live outside the repo under `previews/temples/`; the
  `GeneratorsSacredPreview` test dumps every stage-one hero at LOD0-2 and the generic forms as OBJ when
  `GHUMANTE_PREVIEW_DIR` is set.

## 7. Photo credits (reference only; Openverse, licences as listed)

Per-topic credits files: `/home/user/wt/refs/temples/<topic>/credits.txt`. The photos used most:

| Topic | File | Author | Licence | Source |
|---|---|---|---|---|
| Nyatapola | ref_00 | Kjunstorm | CC BY-NC 2.0 | https://www.flickr.com/photos/87425939@N00/8732930191 |
| Nyatapola | ref_01 | Cycling Man | CC BY-NC-ND 2.0 | https://www.flickr.com/photos/41301446@N05/6053616973 |
| Nyatapola | struts ref_00 | leonyaakov | CC BY-NC-SA 2.0 | https://www.flickr.com/photos/106447493@N05/17081222949 |
| Boudhanath | ref_04, ref_05, ref_07 | (see credits.txt) | CC BY / BY-NC-ND 2.0 | refs/temples/boudha/credits.txt |
| Swayambhu | ref_02 | dalbera | CC BY 2.0 | https://www.flickr.com/photos/72746018@N00/8405631416 |
| Kasthamandap | ref_00 | wonker | CC BY 2.0 | https://www.flickr.com/photos/94056408@N00/2384240387 |
| Taleju KTM | ref_01, ref_06, ref_09 | Prof. Mortel et al. | CC BY-NC-SA 2.0 | refs/temples/taleju/credits.txt |
| Krishna Mandir | ref_02, ref_06 | eriktorner et al. | CC BY-NC-SA 2.0 | refs/temples/krishna/credits.txt |
| Pashupatinath | ref_03 | mariusz kluzniak | CC BY-NC-ND 2.0 | https://www.flickr.com/photos/39997856@N03/8432255546 |
| Patan square | ref_00, ref_07 | tvancort et al. | CC BY-NC-SA 2.0 | refs/temples/patan/credits.txt |
| Ganesh shrine | ref_03 | xitus | CC BY-NC-ND 2.0 | https://www.flickr.com/photos/56726611@N00/295501305 |
| Maju Dega | ref_00, ref_01 | Jorge Lascar | CC BY 2.0 | https://www.flickr.com/photos/8721758@N06/17663233260 |
| Dattatreya | ref_00 | jmhullot | CC BY 2.0 | https://www.flickr.com/photos/32856635@N00/16154779025 |

Web facts: Nyatapola 108 struts and 360 battens, bells 48/80/104/128/168
([bhaktapur.com](https://www.bhaktapur.com/discover/nyatapola/)); Boudhanath 108 Amitabha images and 147 niches of
prayer wheels ([sacredsites](https://sacredsites.com/boudhnath.html), [Lonely Planet](https://www.lonelyplanet.com/pois/1172098));
Kasthamandap rebuilt in the original style with clay tiles, completed 2021
([Himalayan Times](https://thehimalayantimes.com/nepal/kasthamandap-reconstruction-nearing-completion/),
[ShareSansar](https://www.sharesansar.com/newsdetail/rebuilding-of-kasthamandap-nears-final-phase-to-be-inaugurated-within-a-few-days-2021-11-23));
Dharahara rebuilt in the old style with a larger diameter, 22 storeys ([Wikipedia](https://en.wikipedia.org/wiki/Dharahara)).

## 8. Stage-one heroes: one brief each (fix pass, 2026-10-10)

What stands there **now**, the silhouette and proportions to hold, the colours (hex, cartoon-boosted), the details a
local checks, the photos used, and what the render-vs-photo comparison changed. Comparison sheets (photo left, LOD0
render right, same angle): `/home/user/wt/previews/temples/fix2/pairs_*.png`, `cmp_*.png` (outside the repo). Heroes
already briefed above: Nyatapola, Boudhanath, Swayambhunath, Kasthamandap, Taleju (Kathmandu), Krishna Mandir,
Pashupatinath (§0–§3), Dharahara, Hanuman Dhoka (§5).

**Research limits.** Openverse returned no open-licensed photo of the rebuilt Kasthamandap (completed December 2021,
"in the same old style", roof "covered with traditional clay tiles": [ShareSansar](https://www.sharesansar.com/newsdetail/rebuilding-of-kasthamandap-nears-final-phase-to-be-inaugurated-within-a-few-days-2021-11-23),
[Himalayan Times](https://thehimalayantimes.com/kathmandu/50pc-of-kasthamandap-reconstruction-work-over/)), and
Wikimedia Commons answered 429 (rate limited) to every thumbnail request from this sandbox, so the pre-2015 photos
(kasthamandap 00) stand for the form, which the rebuild kept; Kumbheshwar and Bhimsen have one usable photo each.
Items marked [V] want a check against a current photo in the cultural review.

### 8.1 Pashupatinath (Kathmandu) — refs pashupati3 00, pashupati_nandi 00
* Now: the square two-tier pagoda with **both roofs gilt copper** (standing seams), four silver doors, the gilt gajur
  and trident, in a white-and-brick compound; the huge **gilt Nandi** lies before the west door, seen from behind as a
  round gilt rump with a round poll and short crescent horns.
* Plan 19.5 × 19.2 m on its OSM outline (edges at 37.4° / 127°, so the "west" door faces 307°), sanctum 8.7 × 8.3 m,
  23.7 m; eaves 18.5 / 13.3 m. Gilt `#D9A62E`, aged `#B58A2E`, brick `#A8473A`, silver `#C9CCD1`.
* Fix pass: walls, roofs and stair square to the outline; sanctum at 0.45 × the plinth; the Nandi rebuilt as a
  recumbent bull about 2.6 m long with a hump, dewlap, short crescent horns, a red bell collar and a marigold garland
  hugging the neck (no more loose rings); the bell pavilion's roof square to its beam.

### 8.2 Annapurna / Asan Ajima (Asan Tole) — refs annapurna_qb09e 00, 03, 04
* Now: a three-storey pagoda in the south-east corner of Asan, its three roofs "heavily gilded", as are the finial,
  doorway and torana ([Nepali Times](https://nepalitimes.com/asans-annapurna)); the goddess is a silver purna kalash.
  It stands at **street level on one low step**: the **first roof is broad and low (eave about 3 m)** over an
  ambulatory screened all round by a **yellow brass lattice**, two small upper roofs on carved struts with repoussé metal
  valances, a long **gilt pataka** hanging from the finial; a bell hung in front; no stone lions.
* Plan 7 × 7 m [E], eaves 9.6 / 6.0 / 4.0 m at 3.1 / 6.6 / 9.0 m [E], 12 m. Weathered gilding `#A88A48`, brass
  `#D2A63A`, dark wood `#3E2416`, stone step `#8C857A`.
* Fix pass: was a tall narrow sanctum on a red brick plinth with gilt roofs high up; now the low broad first roof, the
  brass grille (`PagodaParams.Grille`), the pataka, no lions, the weathered gilt and the stone step.

### 8.3 Shiva-Parvati (Kathmandu Durbar Square) — refs shivaparvati 01, 03, 06
* Now: a long two-storey house temple (late 18th c.) on a stepped platform, **a row of tall carved windows under cusped
  arches across the ground floor**, Shiva and Parvati (painted white wooden figures) looking out of the central upper
  window, one broad hip roof of brown-red tiles over a **red valance**, **five ornate gilt finials clustered** at the
  middle of the ridge, stone lions at the stair.
* 15.8 × 10.9 m, 10 m; tile `#9C4A34`, valance `#C8202A` with a white edge, brick `#A8473A`, wood `#3E2416`.
* Fix pass: arched carved windows across the front, the red valance, five clustered finials, the darker tile.

### 8.4 Kal Bhairav (Hanuman Dhoka) — refs kalbhairav 00, 05
* Now: an open-air relief about 3.7 m tall in a grey stone arched frame: the **black-blue six-armed deity fills the
  arch**, a big gilt-and-red crown with skulls, white round eyes and fangs, a garland of skulls, sword, disc, trident
  and skull cup, trampling a figure; a **blue field** with sun and moon behind; red sindoor on the lower part; two big
  painted white lions flank it; a low railing and oil lamps in front.
* Body `#232A3D`, field `#5E86C2` (light `#8EC1DE` in the cartoon), crown gilt `#D9A62E` with red `#C8282E`, frame
  `#8F8A82`, lions `#F1ECE0`.
* Fix pass: the figure widened 1.3× so it fills the arch as the relief does, a skull garland, bigger lions (1.55 m).

### 8.5 Kumari Ghar (Basantapur) — refs kumari 00, 02, kumari3 00
* Now: the three-storey brick quadrangle (1757) with the **densest carved dark windows** in Kathmandu across its street
  front and round the court (the Kumari's own window in the court is gilt and closed), a **dark carved torana** with
  marigold garlands over the door, white lime lions either side, one continuous tile roof with a **red valance**.
* 27.7 × 31.3 m, 13.4 m; brick `#8E3A2B`, wood `#3A2216`, valance `#C8202A`.
* Fix pass: one continuous ring roof (was two hips with a gap), projecting carved bay windows on the front and in the
  court with the gilt one in the court, the red valance on the street eaves too, the dark carved gate torana (was gilt).

### 8.6 Gaddi Baithak (Kathmandu Durbar Square) — refs gaddi2 01, 03, 05
* Now: the white neoclassical Rana palace (1908) restored after 2015: a **central portico of paired Corinthian columns
  under a pediment** on a rusticated base, tall shuttered windows, cornice and balustrade along a flat roof.
* 46.4 × 36.3 m, 14 m; stucco `#F4F2EC`, shutters `#4E7D52`, glass `#34404A`.
* Fix pass (previous fixer, checked): portico, columns, pediment, balustrade and window bays read as in the photos.

### 8.7 Basantapur tower (Nautale Durbar) — refs basantapur2 01, 04, 08
* Now: the brick palace tower restored after 2015, carved windows on every storey, topped by **four stacked roofs**
  (the lowest broad, then narrowing) on carved struts, a small gilt finial.
* 13.8 × 13.8 m, 30 m; brick `#9C4434`, wood `#3E2416`, tile `#8E4A36`.
* Fix pass (previous fixer, checked): four real roof tiers with struts over the brick storeys.

### 8.8 55-Window Palace (Bhaktapur) — refs fiftyfive 03
* Now: a long brick palace whose top storey is a **continuous gallery of 55 black carved windows** under a deep tile
  roof; carved windows with hoods on the storey below; small windows and doors at ground level.
* 50 × 12 m, 12 m; brick `#A6503C`, black wood `#2A1A10`.
* Fix pass (previous fixer, checked): the 55-window gallery band and hooded windows below.

### 8.9 Golden Gate (Bhaktapur) — refs goldengate 00, 01; fiftyfive 05, 06
* Now: the gilt repoussé door frame and **torana with ten-armed Taleju and Garuda above** (1754), set in a red-orange
  brick gatehouse in the white palace wall, under a small gilt roof with a row of finials and two lions; gilt guardian
  figures in niches either side.
* Gate 4 × 2.5 m, 5.5 m; brick `#B5432E`, gilt `#D9A62E`, wall lime `#F4F2EC`.
* Fix pass (previous fixer, checked): the gilt torana and frame, finials and lions on the roof.

### 8.10 Bhairavnath (Taumadhi, Bhaktapur) — refs bhairavnath 01, 05
* Now: a **massive rectangular three-roofed temple** whose broad roofs nearly cover the plan, low on its plinth over
  shops; the **top roof is ridged, of gilt metal with a red valance, carrying a row of seven gilt finials** with
  banners (the middle one tallest); no long pataka.
* 16.9 × 14.6 m, 20 m; eaves 17.6 / 13.2 / 9.8 m [E]; tile `#7E3A2A`, gilt `#B8913E`, valance `#B02A24`.
* Fix pass: was a slim three-tier pagoda with a single point; now broad eaves, the ridged gilt top roof
  (`PagodaParams.RidgeFinials` = 7), the red valance, no pataka.

### 8.11 Kumbheshwar (Patan) — refs kumbh_b 00
* Now: one of the valley's two free-standing five-roofed temples: a **broad, low first roof** (metal-sheeted [V]) over a
  wide ground storey, then **four upper tiers stacked tight** and narrowing fast; gilt finial; Nandi in front.
* 12 × 12 m, 25 m; eaves 14.2 / 9.4 / 7.8 / 6.4 / 5.0 m [E]; tile `#6E3426`.
* Fix pass: was a slender tower of evenly spaced small roofs; now the broad first roof and the tight upper stack.

### 8.12 Bhimsen (Patan Durbar Square) — refs bhimsen_a 00
* Now: a three-storey house temple: a **broad skirt roof with its valance round the ground storey**, the gilt
  balcony front above it, a steep top roof with an ornate gilt finial.
* 11.5 × 11.4 m, 14 m; brick `#A04A36`, gilt `#D9A62E`, valance white/red [V: the photo shows a pale valance].
* Fix pass: the skirt roof on struts (`HouseTempleParams.SkirtRoof`) so it reads as two roofs, with the valance.

### 8.13 Vishwanath (Patan Durbar Square) — refs vishwanath 01
* Now: a two-tier Shiva temple (rebuilt after 2015) whose heavy brown-red tile roofs spread wide, the first over an
  **open ring of carved posts all round the sanctum**, a pale valance under the eaves, stone elephants at the stair.
* 12.6 × 11.4 m, 15 m; eaves 13.6 / 9.8 m [E]; tile `#8A4632`.
* Fix pass: was an orange-roofed closed box; now the ambulatory of posts, the broad eaves, the darker tile, the valance.

### 8.14 Taleju (Patan) — refs ptaleju 01, 03
* Now: the Taleju shrine rising as a **tall brick block of the palace with three roofs on top**, gilt torana doors in
  the court below. 16.9 × 15.3 m, 34 m, the first eave at 17.5 m.
* Fix pass: checked; the palace block with three roofs matches.

### 8.15 Vatsala Durga (Bhaktapur) — refs vatsala 00
* Now: the stone shikhara rebuilt after 2015 on a three-step plinth, an arcaded cella with corner pavilions and a
  **bullet-shaped curvilinear spire** (bulging, closing hard at the top) with its finial; buff stone `#B8A88E`.
* Fix pass: the Nagara spire curve made fuller (1 − 0.7 t^2.5); it applies to Krishna Mandir too (refs krishna 02).

### 8.16 Golden Temple / Kwa Bahal (Patan) — refs goldentemple 00, 01
* Now: a Buddhist monastery courtyard: the **three-roofed gilt shrine** on the west range facing the gate across the
  paved court, a small gilt shrine in the middle of the court, gilt and dark carved fronts all round, lions at the gate.
* Fix pass (previous fixer, checked): the quadrangle and the gilt three-tier shrine rising from the back range.

## 9. More photo credits (fix pass; reference only)

| Topic | File | Author | Licence | Source |
|---|---|---|---|---|
| Annapurna, Asan | annapurna_qb09e ref_00, 03, 04 | Matt-Zimmerman | CC BY 2.0 | https://www.flickr.com/photos/16725630@N00/24342619271 (and /23796895514, /24398842996) |
| Bhairavnath | bhairavnath ref_01 | jmhullot | CC BY 2.0 | https://www.flickr.com/photos/32856635@N00/15967465800 |
| Bhairavnath top | bhairavnath ref_05 | Mabacam | CC BY-NC-ND 2.0 | https://www.flickr.com/photos/59275783@N04/6684383945 |
| Shiva-Parvati | shivaparvati ref_01, 03 | Prof. Mortel | CC BY-NC-SA 2.0 | https://www.flickr.com/photos/43714545@N06/6849666205 |
| Kal Bhairav | kalbhairav ref_00 | wonker | CC BY 2.0 | https://www.flickr.com/photos/94056408@N00/2384237563 |
| Kal Bhairav | kalbhairav ref_05 | Matt-Zimmerman | CC BY 2.0 | https://www.flickr.com/photos/16725630@N00/24158461190 |
| Kumari Ghar | kumari ref_00 | Gαurαv | CC BY-NC-ND 2.0 | https://www.flickr.com/photos/7775323@N08/9851882794 |
| Kumari Ghar | kumari ref_02 | Matt-Zimmerman | CC BY 2.0 | https://www.flickr.com/photos/16725630@N00/24426743756 |
| Kumbheshwar | kumbh_b ref_00 | markhorrell | CC BY-NC-SA 2.0 | https://www.flickr.com/photos/41849531@N04/17356049225 |
| Bhimsen | bhimsen_a ref_00 | Prashant_sh | CC BY 2.0 | https://www.flickr.com/photos/13978609@N08/3918289898 |
| Vishwanath | vishwanath ref_01 | rapidtravelchai | CC BY 2.0 | https://www.flickr.com/photos/65638600@N05/6162927985 |
| Taleju, Patan | ptaleju ref_01, 03 | dalbera | CC BY 2.0 | https://www.flickr.com/photos/72746018@N00/8608570029 |
| Vatsala Durga | vatsala ref_00 | dalbera | CC BY 2.0 | https://www.flickr.com/photos/72746018@N00/8565851917 |
| Golden Temple | goldentemple ref_01 | Aasip Amatya | CC BY-SA 4.0 | https://commons.wikimedia.org/w/index.php?curid=89866321 |
| Basantapur tower | basantapur2 ref_08 | kasiat | CC BY-NC-SA 2.0 | https://www.flickr.com/photos/41897878@N00/8262229161 |
| Gaddi Baithak | gaddi2 ref_03 | Jorge Lascar | CC BY 2.0 | https://www.flickr.com/photos/8721758@N06/17824824076 |
| Golden Gate | goldengate ref_00 | Dey | CC BY-NC-SA 2.0 | https://www.flickr.com/photos/45966355@N00/103380900 |
| 55-Window Palace | fiftyfive ref_03 | Prof. Mortel | CC BY-NC-SA 2.0 | https://www.flickr.com/photos/43714545@N06/6837599795 |
| Lions | lions ref_00 | leonyaakov | CC BY-NC-SA 2.0 | https://www.flickr.com/photos/106447493@N05/17264240732 |
| Lions | lions ref_05 | Francisco Anzola | CC BY 2.0 | https://www.flickr.com/photos/10345599@N03/12679335443 |
| Nyatapola stair | lions ref_06 | xitus | CC BY-NC-ND 2.0 | https://www.flickr.com/photos/56726611@N00/313178074 |
| Griffin | nyatapola_guard ref_01 | Jean B | CC BY-NC-SA 2.0 | https://www.flickr.com/photos/127502565@N08/24605006718 |
| Pashupati Nandi | pashupati_nandi ref_00 | Anuma S. Bhattarai | CC BY-SA 2.0 | https://www.flickr.com/photos/8942726@N03/3425943150 |
| Pashupatinath | pashupati3 ref_00 | leonyaakov | CC BY-NC-SA 2.0 | https://www.flickr.com/photos/106447493@N05/17265596061 |
| Swayambhu stair | swayambhu_stairs ref_00 | Nick Kenrick | CC BY-NC-SA 2.0 | https://www.flickr.com/photos/33363480@N05/5714822262 |

Swayambhu's 365-step stair follows OSM way 24707651 (highway=steps, 265 m from the platform east down the hill); its
ground drop below the head (78 m, so a 0.21 m rise) is curated from the region pack's Copernicus GLO-30 terrain along
the line, because the stair's lower part lies in the next tile, whose heights a hero build does not see.
