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
* **Newar lion** (simha): seated upright on its haunches, chest out, head big and round with a **curly mane**, mouth
  open, one forepaw often raised; 1.0–1.8 m with pedestal [E: ktmdurbar 08, patan 00].
* Elephant: standing, with a saddle cloth and a small rider/howdah block; trunk curled; tusks.
* Wrestlers (Jaya, Patta): kneeling/half-squatting heavy human figures holding maces (Nyatapola bottom, Dattatreya).
* Griffin (sardula): lion body with a beaked/horned head and small wings.
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
* Heroes keep their plan, tier count, heights and door yaw from `HeroCatalog` (W2_DESIGN §3.4). The yaw is snapped to the
  nearest axis of the OSM outline when within 30°, so the plinth, walls and roofs line up with the mapped footprint.
* Hero LOD budgets: centrepieces 32,000 / 9,000 / 2,000 / 200 triangles (LOD0-3), others 18,000 / 4,500 / 800 / 200;
  generic sacred buildings keep `SacredSelector.MaxTris`. At LOD1 the bells, tile courses, gilt seams and soffit purlins
  drop out and half the struts are kept, each in a plain two-section form.
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
