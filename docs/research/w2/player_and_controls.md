# W2 research: the player character and GTA-style traversal (spec with numbers)

> Status: research and design input for Wave 2 "Kathmandu comes alive", 2026-10-05. It feeds ARCHITECTURE §7.6 (vehicles), §7.7 (traffic, pedestrians, animals), §7.10a (orientation, controls, cameras), §8 (squash and stretch), ASSET_MANIFEST §8 (vehicle rigs and sockets), §9 (humanoid rig `hum`, player customisation, animation sets) and §11 (HUD), and CONTENT_COVERAGE S2 ("hop on and off vehicles").
>
> Companions: [vehicles_traffic.md](vehicles_traffic.md) (vehicle dimensions, colours, traffic rules, engine sounds), [street_life.md](street_life.md) (clothing palettes, cows, footstep surfaces), [newar_houses.md](newar_houses.md) (door heights, plinths), [temples.md](temples.md) (sacred rules inside compounds).
>
> Evidence tags: **[S]** sourced (URL in §15), **[R]** read from this repository (file named), **[C]** computed here (formula shown), **[E]** estimate or design proposal (tune in playtest), **(review)** needs cultural review, **(verify)** check against photos or device tests.
>
> Hard rules carried in: 9+ / PEGI 7, **non-violent** (no damage, no theft, no weapons, nobody is ever hurt); **riders always wear helmets** on two-wheelers (ASSET_MANIFEST §8); **cows are never harmed or touched** (§7.7, ASSET_MANIFEST §10); everything is built **procedurally in code** from parameters (no hand modelling, no mocap); only CC0/public-domain or synthesised sounds ship.

---

## 0. Ten-line summary

1. **Body:** a chunky, age-neutral young explorer, **1.55 m** tall barefoot (1.64 m with a dhaka topi) and **3.9 heads tall** (head 0.40 m). It sits between Animal Crossing (≈2.5 heads) and Fortnite (≈6.5) [E]. That size fits under real 1.70 m Newar doors and ducks under the 1.45 m ones on purpose [R newar_houses §2.2].
2. **Why the big head:** with the repo's camera rigs, a portrait phone shows the player only **≈190 px tall and the head ≈48 px** [C §3.1]. Eyes must therefore be ≥ 12% of head height and the silhouette must read from 3 shapes: head, torso wedge and big feet.
3. **Built from primitives:** 22 parametric parts (superellipsoids, capsules, lathes, extruded cloth panels) are skinned to the existing 37-bone `hum` rig, at **≤ 5 000 tris** [R ASSET_MANIFEST §9]. Outfits are parameter recipes: dhaka topi, dhaka jacket, kurta, daura suruwal with **8 ties**, sneakers, chappal, helmets. There are 10 skin swatches, 12 hairstyles and 8 hair colours (§2).
4. **Animation without mocap:** about 70% of motion is code: a phase-driven gait, analytic two-bone IK for feet and hands, second-order springs (f, ζ, r) on the topi, hair, backpack and belly, volume-preserving squash-stretch, and lean-into-turn. About 30% is short key poses authored as data (namaste, wave, sit, mount). §4 lists every clip with its frame count and the method that drives it.
5. **On foot:** walk 1.6 m/s, run 4.5, sprint 6.0 [R VehicleSpec.Walker]. Jump apex **1.0 m** in 0.32 s, coyote time 0.10 s, jump buffer 0.12 s. Auto step-up 0.45 m (pikha plinths), mantle up to 1.3 m. There is no fall damage.
6. **Any vehicle, no theft:** a contextual **Hop on** button appears within 2.0 m (two-wheelers) to 3.0 m (cars, buses, trucks) of a seat socket. The player auto-walks ≤ 4 m to the correct LHT/RHD side, and mounting takes **0.5–1.4 s**. Parked vehicles come from a friendly **community fleet** (borrow, auto-return). Taxis, buses, micros and tempos can be **hailed and ridden as a passenger**. Nobody is ever pulled out of a vehicle.
7. **Handling per class:** §6 lists the numbers. Bicycle 25 km/h, scooter 70, motorbike 85, taxi/car 75–90, bus 60, truck 55. Acceleration ranges from 6.5 m/s² (bike) down to 1.1 m/s² (bus). Minimum turning radius at the front axle ranges from 1.7 m (bicycle) to 8.4 m (11 m bus) [C]. Speed-sensitive steering caps lateral acceleration at 10 m/s² (bike), 8.5 m/s² (car) and 3.5 m/s² (bus/truck). Lean is `atan(v²/gR)`, capped at 38°.
8. **Cameras:** every rig keeps the repo's minimum horizontal FOV (walk 55°, drive 62°). Distance scales with vehicle length, from 8 m (scooter) to 17 m (bus). In portrait the camera pitches 8–10° steeper and the look-ahead doubles. An **old-core lane mode** raises the pitch by +15° when walls are within 3 m.
9. **Touch:** portrait is one thumb (floating stick, 120 pt diameter; vehicles: hold-to-throttle plus horizontal drag-steer with full lock at 70 pt). Landscape is two thumbs. Primary buttons are 72 pt and secondary 52 pt, every one ≥ 44 pt/48 dp [S Apple HIG / Material], with 8 pt gaps. Auto-throttle, steering assist and hold-or-toggle are options.
10. **Contact is never harm:** pedestrians predict the player's path 0.8 s ahead and do a comic **hop-aside** (0.35 m hop, 1.5 m sideways). Anything that still touches gets a damage-free bounce. Cows sit inside a **soft cushion** (4 m slow zone, 1.6 m hard stop): the vehicle stops, the cow keeps chewing and moos. Walls give a squash-bounce. There are no wanted levels: a traffic officer's whistle and "Bistārai!" (slowly) is the only consequence.

---

## 1. What is already decided in the repo (do not contradict)

| Item | Value | Where [R] |
|---|---|---|
| Humanoid rig | `hum`, ≤ 40 bones (37 used): root, `squash`, hips, spine, chest, neck, head; shoulder/upper/fore/hand ×2; thumb 2 + mitten fingers 2 ×2; thigh/shin/foot/toe ×2; `prop_R`, `prop_L`, `back_attach`, `head_attach`; `skirt_F`, `skirt_B` | ASSET_MANIFEST §9.1 |
| Player budget | 5 000 tris + 600 accessories; 4 weights/vertex; palette + face + pattern atlas | ASSET_MANIFEST §1.x table, §9.2 |
| Player customisation list | 4 adult builds, 4 skin-tone sets, 12 hairstyles, dhaka topi, casual, daura suruwal, kurta suruwal, sari, helmets (8 colours), trek gear | ASSET_MANIFEST §9.2 |
| Animation set names | `ghm_anim_hum_*` locomotion, idle_variety, jump, sit (incl. `sit_chautari 90L`), ride_twowheel, ride_vehicle, emotes (namaste 40, wave 30) | ASSET_MANIFEST §9.4 |
| Clip format | 30 fps, in place (no root motion) | ASSET_MANIFEST §1.4 |
| Vehicle rig | transform hierarchy: `body` (squash pivot), `steer`, `wheel_*`, `sock_driver`, `sock_pass_NN`, `sock_pillion`; two-wheelers add `ik_hand_{L,R}`, `ik_foot_{L,R}` | ASSET_MANIFEST §8 |
| Walker | walk 1.6 m/s (half stick), run 4.5 m/s, sprint 6.0 m/s (boost ×1.333); accel 12 m/s², brake 16; turn 300°/s; max slope 40° | `game/Assets/Ghumante/Core/Driving/VehicleSpec.cs` `Walker()` |
| Motorbike | 85 km/h; accel 6.5 m/s²; brake 8; wheelbase 1.3 m; max steer 35° at 220°/s; lateral grip 10.5 m/s²; max lean 38°; stuck recovery at 3 s | `VehicleSpec.Motorbike()` |
| Taxi | 75 km/h; accel 4.2; brake 7; wheelbase 2.2; max steer 32° at 120°/s; grip 8.5; no lean | `VehicleSpec.Taxi()` |
| Scooter boost | ×1.2 | `Vehicles/Simulation/VehicleTuning.cs` |
| Chase cameras | Ride L: 8.0 m, 13°, look-ahead 0.45 s ≤ 9 m. Ride P: 9.2 m, 21°, 0.9 s ≤ 16 m. Walk L: 8.4 m, 12°. Walk P: 8.6 m, 22°. Pivot 1.0 m; min hFOV walk 55°, drive 62°; vFOV clamp 100° | `Characters/Cameras/ChaseRigProfile.cs`, ARCHITECTURE §7.10a |
| Control frame | MoveX/Y, Throttle, Reverse, Brake, Boost, ToggleMode (get on/off), Map, Pause, Search, zoom and look | `Characters/Control/ControlFrame.cs` |
| Orientation | Portrait one-thumb (floating stick + action; vehicles hold-to-throttle bottom right, horizontal drag steer, brake/reverse button); landscape two-thumb | ARCHITECTURE §7.10a |
| Cows | soft avoidance radius; vehicles stop; damage-free bump | ARCHITECTURE §7.7 |

Everything below **extends** these values. Where this doc proposes a different number (for example the portrait walking camera distance), it says so and gives the reason.

---

## 2. Character design

### 2.1 Benchmarks: stylised open-world and social characters

Heads-tall values are measured by eye from official renders and are **estimates [E]**. They are used here only to place Ghumante on a scale.

| Game | Heads tall [E] | Silhouette idea | What to borrow |
|---|---|---|---|
| Fall Guys | ≈ 1.5 (one "jellybean") | Rounded bean body, stubby limbs, **no neck** [S Fall Guys anatomy] | Read at any distance; outfits are colour plus one shape |
| Animal Crossing: New Horizons (villagers, player) | ≈ 2.5–3 | Each species starts as a silhouette **only detailed enough to identify it** [S gonintendo] | Silhouette-first design; huge heads carry expression |
| Chibi rule of thumb | 1.75–3 | Head, eyes and hair exaggerated; feet, hands and torso deformed; nose and fingers omitted [S Clip Studio] | Mitten hands (already in `hum`: 2 finger bones) |
| A Short Hike, Captain Toad, Ooblets | ≈ 3 | Big head, small body, oversized shoes | Big feet sell footsteps and squash |
| Zelda: Breath of the Wild / Tears of the Kingdom (Link) | ≈ 5.5–6 | Realistic body with simplified anime face | Secondary motion on hair and cloth; idle personality |
| Fortnite | ≈ 6.5 | Heroic cartoon | Too adult and lanky for a 9+ cosy explorer |
| GTA V / Red Dead | ≈ 7.5 (realistic) | Realistic | Only the **format** (hop on anything, seamless) is borrowed (CONTENT_COVERAGE header) |

**Decision [E]: 3.9 heads.** Below about 3.5 heads the legs get too short to read a walk cycle and to sit on bikes with real-size seats (§2.3). Above about 4.5 the face disappears in portrait (§3.1).

### 2.2 Who the player is

* A **young explorer of open age** (reads as 16–25, never a small child). Kids appear in the world as NPCs (ASSET_MANIFEST §9.3). A young adult plausibly rides motorbikes and drives buses, so a 9+ audience does not see a child driving a truck. (review)
* **Gender-neutral base**: four builds (A slim, B average, C sturdy, D tall), with no gender gate on outfits. A sari or kurta can go on any build, which follows the asset list. Pronouns are not used in UI text (EN and NE). (review)
* **Name**: the default is "Ghumante" (घुमन्ते, "wanderer"). It can be edited, and a profanity filter runs in both languages.

### 2.3 Proportions (adult build B, metres; all [E] unless noted)

Real reference: Nepal's average adult height is about 1.63 m (men) and 1.51 m (women) [S NCD-RisC via Our World in Data]. Newar ground-floor doors are **1.45–1.70 m** high and low on purpose [R newar_houses §2.2]. Real two-wheeler seat heights run 0.76–0.80 m (scooter/commuter class, [E] from class specs in vehicles_traffic §2).

| Part | Size | Ratio | Notes |
|---|---|---|---|
| Total height (barefoot) | **1.55 m** | 3.9 heads | With topi 1.64 m, with helmet 1.67 m |
| Head (chin to crown) | **0.40 m** tall × 0.36 wide × 0.38 deep | 1 head (26%) | Superellipsoid, exponent 2.4 (slightly boxy-round). Real human head ≈ 0.23 m: ×1.75 |
| Neck | 0.04 m visible | 0.1 | Almost hidden under the jaw (Fall Guys lesson) |
| Torso (shoulder line to crotch) | 0.48 m | 1.2 heads | Tapered wedge: shoulders 0.42 m wide, hips 0.34 m. Chest depth 0.24 m |
| Legs (crotch to sole) | **0.63 m** | 1.6 heads | Thigh 0.30, shin 0.27, foot height 0.06. Inseam is long enough to reach the ground from a 0.78 m seat with one tip-toe |
| Arms (shoulder to fingertip) | 0.58 m | 1.45 heads | Reach to knee level, so a hand rests on a knee when sitting |
| Hands | 0.13 m long × 0.09 wide (mitten + thumb) | ×1.4 real | Read at 48 px |
| Feet / shoes | 0.27 m long × 0.11 wide × 0.09 high | ×1.3 real | Sneaker toe-spring 15°. Big feet anchor the IK and the footstep squash |
| Eyes | 0.055 m tall each (14% of head), spacing 0.11 m, set at 45% of head height | — | Face atlas, not geometry. Pupils move for look-at |
| Shoulder width / height | 0.27 | — | Silhouette: a wedge torso, not a bean |

**Builds** scale the bones (no new topology):

| Build | Height | Shoulder × | Hip × | Belly (spine-front offset) | Leg × |
|---|---|---|---|---|---|
| A slim | 1.52 | 0.92 | 0.95 | 0 | 1.00 |
| B average | 1.55 | 1.00 | 1.00 | 0.01 m | 1.00 |
| C sturdy | 1.56 | 1.12 | 1.12 | 0.04 m | 0.97 |
| D tall | 1.66 | 1.00 | 1.00 | 0.01 m | 1.10 (head size fixed, so 4.15 heads) |

Doorways: build D with a topi (1.75 m) ducks under every Newar door, and build B ducks only under doors below 1.68 m. Ducking is a personality beat, not a penalty: a 0.25 s head-tilt and knee-bend, an additive layer driven by a head-height raycast 0.6 m ahead. It honours the "low door on purpose" tradition [R newar_houses S10].

### 2.4 Building the body from primitives (procedural mesh recipe)

Every part is a parametric primitive, built once at character creation (and when the wardrobe changes) in a Burst job, then skinned to `hum`. Outfits are **parameter sets on the same primitives**, not separate meshes, so the wardrobe costs nothing to ship.

| # | Part | Primitive | Segments (u × v) | Tris | Bones (weights) |
|---|---|---|---|---|---|
| 1 | Head | Superellipsoid, e = 2.4, jaw flattened 20% | 20 × 14 | 520 | head |
| 2 | Ears | Flattened half-ellipsoid ×2 | 8 × 4 | 96 | head |
| 3 | Nose | Small sphere bump, 0.035 m (optional per face preset) | 8 × 6 | 80 | head |
| 4 | Hair cap | Offset shell of the head (+0.015 m) clipped by a hairline curve | 20 × 10 | 360 | head |
| 5 | Hairstyle extra | One of 12 recipes (§2.6): lathe buns, swept extrusions, braid chain | — | 150–600 | head (+ 2–3 virtual spring bones, see §5.3) |
| 6 | Neck | Capsule | 10 × 3 | 60 | neck/head |
| 7 | Torso | Lofted superellipse rings (6 rings from hips to shoulders), front/back profile curves | 16 × 6 | 400 | hips/spine/chest |
| 8 | Upper arms ×2 | Tapered capsule | 10 × 4 | 2×100 | shoulder/upper arm |
| 9 | Forearms ×2 | Tapered capsule | 10 × 4 | 2×100 | forearm |
| 10 | Hands ×2 | Mitten = flattened capsule + thumb capsule | — | 2×140 | hand, finger, thumb |
| 11 | Thighs ×2 | Tapered capsule | 10 × 4 | 2×100 | hips/thigh |
| 12 | Shins ×2 | Tapered capsule | 10 × 4 | 2×100 | shin |
| 13 | Shoes ×2 | Lofted sole outline (toe-spring 15°) + rounded upper + laces decal | — | 2×260 | foot/toe |
| 14 | Top garment shell | Offset of the torso (+0.012 m) with a hem curve and sleeves (§2.7) | — | 300–700 | spine/chest/arms (+`skirt_F/B` for long hems) |
| 15 | Bottom garment shell | Offset of the legs; suruwal flare via the radius curve | — | 300–500 | hips/thigh/shin |
| 16 | Headwear | Topi, helmet, beanie or sunhat (§2.7) | — | 120–400 | `head_attach` |
| 17 | Back item | Daypack (rounded box) or none | — | 0–400 | `back_attach` |
| | **Total (typical)** | | | **≈ 4 400–4 900** | within 5 000 + 600 |

Rules for generated meshes:
* **Smoothed normals in UV2** for the inverted-hull outline (ASSET_MANIFEST §5). The outline is 2 px at 1080p and ink `ui.ink` 85%.
* **No alpha clipping** (ASSET_MANIFEST §5). Dhaka patterns, laces, eyelashes and the face are texture or atlas work on opaque surfaces.
* Joints get **1.15× radius "sausage" bulges** at the elbows and knees (two extra rings), so bends never pinch. This is a cheap stand-in for corrective blend shapes.
* LOD1 (2 000 tris): halve the segments and merge the shoes into the shins. The player is exempt from the `maximumLODLevel` clamp (ASSET_MANIFEST §5).

### 2.5 Skin tones (10 swatches, toon ramp base / shadow) [E] (review)

Nepal has **142 caste and ethnic groups** (2021 census) [S Wikipedia, 2021 census], spanning Indo-Aryan and Tibeto-Burman peoples from the Terai to the Himalaya. The palette must span light olive to deep brown evenly, and **no swatch carries a group name**. Swatches are numbered and shown as round chips only.

| # | Base | Shadow (ramp dark) | Cheek blush (face atlas) |
|---|---|---|---|
| 1 | `#F3D3B5` | `#D9A98A` | `#F2A7A0` |
| 2 | `#EBC39E` | `#CC9A76` | `#EE9C92` |
| 3 | `#E0B48C` | `#BF8A66` | `#E8918A` |
| 4 | `#D4A276` | `#B07A55` | `#DE8678` |
| 5 | `#C69064` | `#A06A47` | `#D27A6C` |
| 6 | `#B67F55` | `#8E5B3B` | `#C46E60` |
| 7 | `#A26D47` | `#7C4C30` | `#B66556` |
| 8 | `#8C5A3A` | `#683C26` | `#A2594C` |
| 9 | `#744830` | `#55301F` | `#8E4E43` |
| 10 | `#5C3826` | `#40241A` | `#7A4239` |

Toon ramp: 3 steps (lit, half at 0.55, shadow at 0.2) with a warm rim `#FFE2C2` at 30%. The picker opens on a random swatch, never on #1.

### 2.6 Hair (12 styles × 8 colours) [E]

| id | Style | Build recipe | Spring bones |
|---|---|---|---|
| h01 | Short side-part | Cap + swept fringe extrusion | fringe ×1 |
| h02 | Buzz | Cap only, stubble texture | — |
| h03 | Curly crop | Cap + 14 instanced sphere curls | — |
| h04 | Spiky (anime-lite) | Cap + 7 cones | tips ×2 |
| h05 | Shoulder bob | Cap + lathe skirt to the jaw | back ×1 |
| h06 | Long straight | Cap + extruded sheet to the mid-back | 3-link chain |
| h07 | Single braid (*chulthi*) with a red tassel (*dhago / parandi*) | Cap + braid = 9 interlocked ellipsoids + tassel cone | 4-link chain (review) |
| h08 | High ponytail | Cap + lathe tie + tapered tube | 3-link chain |
| h09 | Low bun | Cap + sphere bun at the occiput | — |
| h10 | Top knot | Cap + sphere at the crown | knot ×1 |
| h11 | Wavy medium | Cap + 5 swept ribbons | ribbons ×2 |
| h12 | Two puffs | Cap + 2 spheres | puffs ×2 |

Colours: black `#1B1A1C`, soft black `#2A2422`, dark brown `#3B2A22`, brown `#5A3E2E`, auburn `#7A3B24`, grey `#8E8E8E`, white `#E6E2DA`, and a fun colour unlocked by collection: teal `#2A8C8C`. Hair under a helmet or topi uses a **squashed variant**: the crown shell is scaled to 0.6 so nothing clips, and any ponytail or braid stays out.

### 2.7 Outfits as parameter recipes

Colours reuse [street_life.md §3.3](street_life.md) wherever possible, so the player looks like part of the city.

| Slot | Item | Shape parameters | Palette (hex) | Cultural notes |
|---|---|---|---|---|
| Head | **Dhaka topi** (ढाका टोपी) | Soft cap on a round base that fits the head (circumference ×1.05). Real cap height is 3–4 in (7.6–10 cm) [S Wikipedia]. **Asymmetric**: the front rises higher and the crown slopes back, which is said to represent a mountain peak [S Wikipedia]. Cartoon: front 0.11 m, back 0.075 m (×1.15 real), crown fold 8° off-centre. Worn tilted 6° to one side. 12 × 6 ring loft, 160 tris | Pattern atlas: small diamond lattice on a base of red `#B5302B`, navy `#2E3A6B`, black `#1E1E1E`, mustard `#C99A2E` or green `#3E7A4A`, with dots of white `#F4EFE0` and yellow `#F2C230` | Worn mostly by men in daily life [S Wikipedia]. In-game it is available to all builds. Spring: f 3.5 Hz, ζ 0.45 (it wobbles on landing). Bhadgaunle variant: plain black `#1A1A1A` (review) |
| Head | Helmets (open-face, full-face, bicycle) | Sphere-cap shell with brim or visor. Open 0.30 m Ø shell, full-face adds a chin bar, bicycle = 6 vent ridges | 8 presets: red `#E53935`, blue `#1E88E5`, yellow `#FDD835`, white `#FAFAFA`, black `#212121`, green `#43A047`, orange `#FB8C00`, pink `#EC6FA0` | **Auto-equipped on every mount** of a scooter, motorbike or bicycle, for rider and pillion. Nepal law requires helmets for riders and passengers, though pillion compliance is under 1% [S Himalayan Times; TU Berlin study]. The game models the law, not the street |
| Torso | **Dhaka jacket** (modern) | Zip bomber: torso shell +0.02 m, ribbed hem band 0.05 m, collar 0.04 m, sleeves to the wrist; dhaka pattern on yoke and pockets only (30% of area) | Body navy `#2B3550` or charcoal `#3A3D42`; dhaka panel as above | Collection unlock (ASSET_MANIFEST `dhaka_jacket`) |
| Torso | **Kurta** | Knee-length tunic: hem at 0.92 of leg length; side slits from the hip; band collar 0.03 m; hem driven by `skirt_F/B` springs (f 2.2, ζ 0.5) | Pastels `#F4B6C2`, `#A8D5E2`, `#F7D488`, `#B5E3B0`, `#E3C1F0`; men's kurta white `#F5F2EA` | Worn by all genders; optional shawl/dupatta (a 2-link spring ribbon) |
| Torso | **Daura** (with suruwal) | Closed round neck, double-breasted wrap crossing left over right, knee length. **Eight tie-strings** (*Astamatrika singini*, each linked to one of the eight mother goddesses), modelled as 4 tie pairs (2 at the shoulder/neck, 2 at the waist), plus **five pleats** [S Wikipedia Daura-Suruwal; Himalayan Times] | Off-white `#EDE6D6`, pale grey `#C9C6BE`, light blue `#B8C8D8`, beige `#D9C7A3`; waistcoat charcoal `#3A3D42` or navy `#2B3550` | National dress for men, declared 2017 [S Wikipedia]. Pair with the topi and a dark waistcoat. Never comedic. (review) |
| Legs | **Suruwal** | Radius curve: thigh ×1.35 (loose), knee ×1.1, ankle ×0.85 (fitted) | Matches the daura | |
| Legs | Jeans / joggers / shorts | Straight tube; joggers add an ankle cuff; shorts end at 0.55 of the thigh | Denim `#3E5C8A`, black `#24262B`, khaki `#B8A67E`, grey `#7C8088` | Default casual |
| Torso | T-shirt / hoodie | Shell +0.01 m; hoodie adds a hood torus on `back_attach` (spring f 2.0) | Free palette of 12 brights | Default casual |
| Feet | **Sneakers** | Lofted sole (0.035 m thick, white `#F5F5F2`); upper colour; 3-stripe or swoosh-like marks **not allowed** (no brands), so use a generic panel stitch | Upper: red, blue, yellow, white, black, teal `#2A9D8F`, purple `#7B4FA0` | Default. Squash on each footstep (§5.4) |
| Feet | Chappal (flip-flops) | Flat sole 0.02 m + Y strap; toes from the shoe primitive with the toe bone exposed | Blue `#2A5DB0`, red, black | 40% of locals wear them [R street_life §3.3]. Footstep sound "flap" |
| Feet | Trek boots | Sole 0.04 m, ankle cuff, lace hooks | Brown `#5A4636`, grey | M3a |
| Full | Sari, gunyu cholo, hakupatasi | As ASSET_MANIFEST §9.2; skirt bones drive the hem | street_life palettes | (review drape) |
| Accessory | Daypack | Rounded box 0.30 × 0.40 × 0.15 m on `back_attach`; spring f 2.0, ζ 0.5 | Brights | Default |
| Accessory | Khata, marigold mala, tika | ASSET_MANIFEST §9.2 | — | Event rewards (review) |

**Silhouette test (acceptance):** render the player as solid black at 48 px head height in 8 random wardrobes. Players should name the outfit type (casual, kurta, daura, sari, trek) at ≥ 80% accuracy in a 5-person hallway test, and the player must be told apart from crowd NPCs at 30 m. Two tools help: an outline 0.5 px thicker than NPC outlines, and a **soft ground ring** (`#FFF3D6` at 40%, 0.9 m Ø) under the player when stationary in crowds [E].

### 2.8 Face (atlas-driven)

Expressions come from `ghm_chr_player_face_set` (blink, smile, surprise, tired, two talk shapes) [R]. Add **6 states** that procedural systems can call:

| State | Trigger | Eyes | Mouth | Duration |
|---|---|---|---|---|
| Blink | Random, every 2.5–5.5 s (uniform), 20% chance of a double blink | Closed 3 frames | — | 0.1 s |
| Look-at | Point of interest within 12 m (landmark, monkey, cow, pigeons, other characters); max 70° yaw, 30° pitch, head 60% / eyes 40% | Pupils offset ≤ 0.012 m | — | Hold while in view, ≥ 0.8 s |
| Joy | Discovery, landing a jump, "hop aside" seen | Squint-smile arcs | Open smile | 1.2 s |
| Wince-laugh | Bump into a wall or vehicle | Squeezed | Teeth grin | 0.6 s |
| Puff | Sprint > 4 s, steep climb | Half-lidded | O-shape, cheek puff | While active |
| Calm | Inside temple compounds | Soft | Gentle smile; **no silly idles** | While inside a `SACRED` area |

---

## 3. Seeing the player on a phone

### 3.1 Screen size of the player with the repo's camera rigs [C]

`vFOV = 2·atan(tan(hFOV/2)/aspect)`, clamped to 100° (ARCHITECTURE §7.10a). Visible height at the player = `2·d·tan(vFOV/2)`.

| Mode, device | Aspect | vFOV | Visible height at player | Player (1.55 m) on screen | Head (0.40 m) |
|---|---|---|---|---|---|
| Walk, landscape 16:9 (1920×1080) | 1.78 | 32.6° | 4.9 m | **340 px** (31%) | 88 px |
| Walk, landscape 19.5:9 phone | 2.17 | 27.0° | 4.0 m | 415 px | 107 px |
| Walk, portrait 9:19.5 (1080×2340) | 0.46 | 96.9° | 19.4 m | **187 px** (8%) | **48 px** |
| Ride, portrait | 0.46 | 100° (clamped) | 21.9 m | 165 px | 43 px |
| Ride, landscape 19.5:9 | 2.17 | 31.0° | 4.4 m | 377 px | 97 px |

**Consequences**
1. Portrait is the hard case. The face has about 48 px, so eye shapes must be at least 6–7 px. That is why the eyes are 14% of head height (§2.3).
2. **Recommendation [E]:** shorten the portrait walking camera from 8.6 m to **6.8 m** and lower the pivot to 0.9 m. Because the minimum horizontal FOV already widens the vertical view so much, the closer camera still shows ≈ 15 m of vertical space while the player grows to ≈ 240 px. The landscape distance stays as it is. This is a tuning proposal for the camera owner; this doc does not change code.
3. A 2-step pinch zoom (0.75× and 1.3× distance) is already supported by `ZoomSteps`. Keep those limits.

---

## 4. Animation set (what plays, how it is made)

Method key: **P** = fully procedural (code only); **K** = key poses authored as data (a JSON list of bone rotations at frame numbers, made in a small in-editor pose tool or written by hand), interpolated with ease curves; **K+P** = key poses plus procedural layers (IK, springs, noise). Frame counts at 30 fps match ASSET_MANIFEST §9.4 where it names them.

### 4.1 Locomotion (P)

| Clip | Frames / cycle | Method | Parameters |
|---|---|---|---|
| idle (breathing) | 90 L | P | Chest scale 1 ± 0.015 at 0.25 Hz (15 breaths/min); head micro-sway ±1.5° from Perlin noise at 0.3 Hz; weight shift every 4–7 s |
| walk | P (phase) | P | Speed 0.3–1.6 m/s. Step length = `0.42·legLen + 0.18·v` (0.44 m at 1.0 m/s, 0.55 m at 1.6 m/s). Cadence = v / step, so 1.6 m/s gives **2.9 steps/s**: brisk and toddler-cute on short legs. Pelvis bob 0.025 m at 2× step frequency; pelvis roll ±4°; arm swing ±22° opposite to the legs; torso twist ±6° |
| jog / run | P | P | 2.0–4.5 m/s. Step `0.55·legLen + 0.16·v` (1.07 m at 4.5 m/s, 4.2 steps/s). Flight phase 25% of the cycle; forward lean 8° + 2° per m/s of acceleration; arms bent 85°, swing ±35° |
| sprint | P | P | 6.0 m/s, forward lean 14°, step 1.25 m, 4.8 steps/s, hair/topi springs excited ×1.5, dust puff every 2nd step |
| start / stop | 12 / 14 | P | Anticipation lean back 4° for 0.1 s, then forward; stop overshoots 6° forward and settles with a spring (f 4, ζ 0.5) |
| turn on the spot | 16 | P | Feet replant in 2 steps when the yaw delta exceeds 50° |
| uphill / downhill | P | P | Torso lean ±(0.5 × slope)°, step −15% up / +10% down; cadence fixed |
| stairs up/down | P | P | Foot IK targets snapped to stair treads (raycast per step); step height taken from the stair mesh. Newar stairs are steep and ladder-like [R newar_houses] |

**Why procedural gait works here.** Dynamic similarity says animals of different sizes move alike at the same Froude number `Fr = v²/(g·l)`, and humans switch from walking to running near Fr ≈ 0.5 (Alexander; [S Froude number, Wikipedia]). With the cartoon leg length l = 0.63 m, the natural walk-run switch is at `√(0.5·9.81·0.63)` ≈ **1.76 m/s** [C]. That lines up with the walker's 1.6 m/s walk and its run above 2 m/s, so the generator blends the walk to the run between 1.6 and 2.0 m/s. A real 1.7 m adult walks comfortably at about 1.4 m/s [S TravelTime/walking-speed literature]. The cartoon walks a little faster on shorter legs, so the cadence is higher, which reads as cheerful.

### 4.2 Idle personality ("fidgets")

After **6 s** without input (ASSET_MANIFEST §9.4), pick from a weighted table with no repeat within 3 picks. Then idle for 4–8 s before the next pick.

| Fidget | Frames | Method | Weight | Context |
|---|---|---|---|---|
| look_around (head + eyes follow a random point or the nearest point of interest) | 120 | P | 3 | Everywhere |
| stretch (arms up, rise on toes, squash 0.94 on release) | 90 | K+P | 2 | Not in `SACRED` |
| check_phone (phone prop on `prop_R`, thumb taps) | 120 | K | 2 | Urban |
| shift_weight | 90 | P | 3 | Everywhere |
| yawn | 90 | K | 1 | After 20:00 game time |
| **adjust_topi** (both hands tug the cap; spring overshoot) | 45 | K+P | 2 | Only with a topi |
| **flick_hair** | 40 | K+P | 1 | Long hair |
| **toe_tap** (one foot taps the beat of nearby music or bells) | 60 L ×2 | P | 1 | Near bhajan or bell sources |
| **wave_at_npc** (if an NPC within 6 m looks back) | 30 | K | 2 | Streets |
| **watch_bird / pigeon flock** (head follows the flock centroid) | P | P | 2 | Squares with pigeons |
| **monkey_double_take** | 40 | K+P | 1 | Within 8 m of a macaque |
| **shiver** (arms hug, 8 Hz shake) | 60 | P | 3 | Below 5 °C or with snow |
| **fan_self** | 60 | K | 2 | Above 30 °C (Terai) |
| **hands-together rest** | 90 L | K | 3 | Inside `SACRED` areas (replaces all comic fidgets) |

### 4.3 Jump, land, climb

| Clip | Frames | Method | Notes |
|---|---|---|---|
| jump_start | 8 (0.27 s) **visual only** | P | Anticipation squash to 0.85 over 2 frames, then stretch 1.12. The jump leaves the ground on the **frame of the input**; the anticipation plays for 0.07 s overlapped with take-off so input never lags |
| air | 12 L | P | Knees tuck 30° at the apex; arms up 40°; springs follow |
| land / land_squash | 10 / 12 | P | Squash `s = clamp(1 − 0.06·v_impact, 0.75, 1)`, recover with f 5 Hz, ζ 0.35 (one visible bounce) |
| stumble_recover | 24 | K+P | Falls of more than 4 m: a forward roll and a "ta-da" pose. **No damage, ever** |
| mantle (0.45–1.3 m ledge) | 18 | K+P | Hands IK to the ledge edge, knee up, pop |
| auto step-up (≤ 0.45 m) | — | P | Foot IK and a 0.12 s pelvis ease. Covers pikha aprons (0.3–0.45 m [R newar_houses]) and temple plinth steps |

### 4.4 Social and sacred emotes (K+P)

| Clip | Frames | Pose spec |
|---|---|---|
| **namaste** | 40 | Palms pressed together **at the chest (sternum)**, fingers pointing up, elbows out 25°, **head bow 15° + upper spine 8°**, eyes close for 6 frames at the bottom of the bow [S namaste etiquette sources]. Default greeting, auto-offered when an NPC greets first. For elders (old-man and old-woman NPCs), play the **namaskar** variant: hands 0.05 m higher and bow 22°, since "the higher the hands the higher the status" [S] (review) |
| wave | 30 | Right hand up to head height, forearm swings ±25° at 2.5 Hz, 3 swings; body bounce 0.02 m |
| cheer / clap / laugh / bow / thumbs_up / point / selfie / photograph | as ASSET_MANIFEST §9.4 | — |
| dance_folk | 96 L | (review): a generic light step-and-turn. No sacred dance (no Lakhe, no masked deity dances) |
| spin_prayer_wheel_walk | 32 L | **Right hand, clockwise**, walking clockwise (kora) at 0.9 m/s (ASSET_MANIFEST §9.4; temples.md) (review) |
| ring_bell | 30 | Right hand pulls the clapper rope or strikes the bell; one swing |

### 4.5 Sitting, including on a chautari

| Clip | Frames | Spec |
|---|---|---|
| sit_chautari | 90 L | A chautari is a stone platform built around a pipal and/or bar tree [S Wikipedia Chautari]. Platform heights vary; the generator uses **0.45–0.9 m [E]**, with a **porter ledge (*bisaune*) at about 0.9–1.0 m** for resting a doko [E] (verify). Two variants: (a) a ledge ≤ 0.55 m means a seated pose with feet IK-planted on the ground; (b) a ledge > 0.55 m means sitting on the edge with legs dangling, swinging ±10° at 0.5 Hz out of phase. Hands rest on the knees or behind on the stone. Approach: walk to a seat socket on the platform edge, turn 180°, then a 20-frame hop-sit |
| sit_bench / pikha | 60 L | Seat height 0.30–0.45 m: knees up, forearms on the knees |
| sit_ground_crosslegged | 90 L | In courtyards and ghats. In `SACRED` areas, sit facing the shrine with no feet pointing at it (review) |
| sit_to_stand | 20 | Lean forward 20°, push on the knees |
| chautari bonus idle | — | Drinking chiya (tea glass on `prop_R`), 3 sips per 90 frames [E] |

### 4.6 Riding and driving poses (K + IK)

All riding poses are **base key poses plus IK** to the vehicle sockets (`sock_driver`, `ik_hand_*`, `ik_foot_*`) [R ASSET_MANIFEST §8], so one pose fits every vehicle of a class.

| Vehicle class | Pose | IK targets | Procedural layers |
|---|---|---|---|
| Bicycle | Spine lean 25°; hands on grips; feet on pedals | Pedal crank angle → foot IK (crank radius 0.17 m) | Pedal cadence = wheel rpm / gear ratio, clamped to **50–90 rpm** (wheel Ø 0.71 m at 14 km/h means 105 wheel rpm; ratio 1.6 gives 66 cadence rpm) [C]; standing on the pedals uphill > 6%; side lean (§6.3); bell ring on the horn button |
| Scooter | Upright, spine 5° back; knees together; feet flat on the floorboard | `ik_hand`, `ik_foot` (floorboard) | Lean into turns, honk bounce 12, brake pitch forward 6° |
| Motorbike | Spine lean 15°; knees grip the tank; feet on pegs | `ik_hand`, `ik_foot` (pegs) | Lean; at stops the **left foot goes down** (IK to the ground) below 1 km/h; wheelie_squash 16 on launch over bumps |
| Pillion | Hands on the rider's waist or the grab rail | `sock_pillion` | Lean follows the bike at 0.8×; looks around |
| Car (driver, RHD) | Seated, hands at 10 and 2 on the wheel | Hand IK on a wheel rim of radius 0.19 m, rotated with `steer` | Wheel rotation = steer angle × **steering ratio 15:1** (so 32° of wheel angle is 480° at the rim; clamp the visual to ±270° and switch to hand-over-hand above ±120°) [E] |
| Bus (driver) | Upright, big flat wheel tilted 30° from horizontal | Hand IK on a rim of radius 0.25 m | Ratio 20:1; visual clamp ±360°; door-lever reach on stop |
| Truck (driver) | Like the bus, cab 1.4 m above the ground | Rim 0.24 m | Ratio 20:1; gear-change arm swing on every 15 km/h upshift (cosmetic) |
| Tractor | Open seat, upright | Rim 0.2 m | Body vibration 18 Hz, 0.004 m |
| Passengers (taxi, bus seated/standing, micro, tempo) | `car_passenger`, `bus_seated`, `bus_standing_sway`, `tempo_passenger` [R] | Hand IK to the grab rail when standing | Sway spring on lateral acceleration (f 1.5, ζ 0.4, gain 3°/(m/s²)); look out of the window toward points of interest |

### 4.7 Mount and dismount (enter/exit) (K+P)

Durations are the time from the button press until control returns [E]. Each is short enough to feel snappy (GTA V's car entry takes about 1.5–2.5 s; this game is snappier because nothing is ever locked).

| Vehicle | Approach | Enter sequence | Enter (s) | Exit (s) |
|---|---|---|---|---|
| Bicycle | Left side | Step over the top tube, push off | **0.5** | 0.35 |
| Scooter | Left side | Helmet pops on (squash 1.2 → 1, sparkle), swing the right leg, sit | **0.6** | 0.45 |
| Motorbike | Left side | Helmet pop, high leg swing over the seat, kick the side-stand up | **0.7** | 0.5 |
| Pillion (any 2W) | Left side | Hop on behind the rider, hands to the waist | 0.6 | 0.45 |
| Car / taxi (drive) | **Right-side driver door** (RHD, left-hand traffic [R vehicles_traffic §1]) | Door opens 65°, duck, slide in, close door | **0.9** | 0.7 |
| Car / taxi (passenger) | **Left rear door (kerb side)** | Same with a seat-belt gesture | 0.9 | 0.7 |
| Microbus / tempo (passenger) | Side sliding door, left / rear step | Step up 0.35 m, duck under a 1.25 m door, sit | 1.0 | 0.8 |
| Bus (drive) | Right cab door or front door [E] (verify per bus type) | Climb 2 steps of 0.35 m, sit | **1.2** | 0.9 |
| Bus (passenger) | **Front/left door at a stop** | Climb 2–3 steps, walk to a seat or grab a rail | 1.2 + walk | 0.9 |
| Truck / tanker | Right cab door | Grab handle, climb 2 rungs (0.45 m each), swing in | **1.4** | 1.0 (hop down with a squash land) |
| Tractor | Left, from the rear wheel | Climb onto the axle step, sit | 1.0 | 0.7 |
| Cycle rickshaw (passenger) | Left/rear | Step up 0.45 m, sit; the puller NPC pedals | 0.8 | 0.6 |

* **Interruptible:** pressing any move input during the first 60% of an enter cancels it (the player steps back). Exit is always allowed. If the vehicle is moving, exit is a **comic hop-off** that only begins below 8 km/h, with automatic braking first; it is never a leap from speed.
* **Exit side:** if the default side is blocked (a wall within 0.8 m, as in old-core lanes), use the other side. Two-wheelers exit to whichever side is free.
* **Helmet rule:** the helmet stays on for 1.5 s after dismount, then pops off into the backpack with a "pop" sound. The player's chosen helmet colour persists.

---

## 5. Procedural animation techniques (feasible without mocap)

### 5.1 Pipeline per frame (player only; NPCs use VAT, ASSET_MANIFEST §9.5)

```
State machine (on foot / mounting / riding / seated / emote)
  → Base pose:   procedural gait generator (phase φ ∈ [0,1)) OR key-pose clip sampler
  → Additives:   lean, breathing, look-at, fidget overlays, duck
  → IK pass:     feet (ground/pedals/pegs), hands (grips/wheel/rails/ledges), two-bone analytic
  → Secondary:   second-order springs on topi, hair chain, braid, backpack, hems, belly
  → Squash:      `squash` bone scale (volume-preserving)
  → Write:       transforms to the SkinnedMeshRenderer
```

Implement it as an engine-free solver in `Ghumante.Characters` (pure math on arrays, unit-testable like `VehicleSpec`), plus one Burst job on device. Unity's **Animation Rigging** package (Two Bone IK, Multi-Aim, Damped Transform constraints running on Animation Jobs [S Unity docs]) is an acceptable alternative for prototyping, but a single custom job keeps the cost predictable on the player. Budget: **≤ 0.15 ms per frame on a Low-tier device** for the player [E].

### 5.2 Gait generator

* A phase accumulator advances by `dφ = cadence·dt/2` (one cycle = 2 steps). The left foot is at φ, the right at φ + 0.5.
* Foot trajectory per foot: stance from φ 0 to 0.6 (walk) or 0 to 0.35 (run); swing arc with **lift height 0.07 m (walk) / 0.16 m (run)**, using a sin² ease.
* Foot targets are **planted in world space** during stance (no sliding). This is David Rosen's approach in Overgrowth, which animated a whole character from **13 key frames** plus procedural interpolation and IK [S GDC 2014 "An Indie Approach to Procedural Animation"].
* Ground: one raycast per foot per frame from 0.5 m above the hip. Pelvis height = min of the two foot heights + leg length × 0.97, smoothed with f 6 Hz, ζ 1.0.
* Speed changes re-time the cycle without popping: cadence is lerped over 0.15 s.

### 5.3 Second-order dynamics ("springs with personality")

Use the **f, ζ, r** parameterisation from t3ssel8r's "Giving Personality to Procedural Animations using Math" (2022): f = natural frequency (Hz), ζ = damping, r = initial response (r > 1 overshoots on the way in, r < 0 anticipates). Integrate with semi-implicit Euler and clamp the step for stability [S]. Constants: `k1 = ζ/(πf)`, `k2 = 1/(2πf)²`, `k3 = r·ζ/(2πf)`.

| Driven thing | f (Hz) | ζ | r | Input | Output limit |
|---|---|---|---|---|---|
| Dhaka topi tilt | 3.5 | 0.45 | 0 | Head acceleration | ±12° |
| Helmet | 6.0 | 0.7 | 0 | Head acceleration | ±4° |
| Ponytail / braid chain (per link, 3–4 links) | 2.4 | 0.35 | 0 | Parent link tip | ±55° per link |
| Short hair fringe | 4.0 | 0.5 | 0 | Head | ±10° |
| Backpack | 2.0 | 0.5 | 0 | Chest | 0.05 m translation, ±10° |
| Kurta / daura hem (`skirt_F/B`) | 2.2 | 0.5 | 0 | Thigh swing + velocity | ±35° |
| Belly (build C) | 3.0 | 0.3 | 0 | Pelvis vertical | ±0.012 m |
| Camera follow (for reference) | 1.2 | 1.0 | 0 | Target | — |
| Head look-at | 2.5 | 0.9 | −0.2 | Point of interest | ±70° |
| Body lean into turns | 2.0 | 0.8 | 0 | Lean target (§5.5) | ±15° on foot |

### 5.4 Squash and stretch

* Volume-preserving: scale the `squash` bone by `(1/√s, s, 1/√s)` on X, Y, Z.
* On-foot footstep: s = 0.97 at each heel strike (walk), 0.93 (run).
* Jump: anticipation 0.85, take-off stretch 1.12, land per §4.3.
* Vehicles: the existing `body` squash springs (ASSET_MANIFEST §8) feed a **sympathetic 50% squash** to the seated rider.
* Clamp s to [0.72, 1.18] so the outfit shells never invert.

### 5.5 Lean and banking

* On foot: lean = `atan(v·ω/g)` (v speed, ω yaw rate), clamped to ±15°, smoothed (§5.3).
* Two-wheelers: real steady-state lean is `θ = atan(v²/(g·R))` [S arXiv 1611.03857]. Since `v²/R = v·ω`, the same formula works from yaw rate. Clamp to `MaxLeanDeg` 38° [R]. At 30 km/h on an R = 20 m bend that gives 19.5° [C].
* Rider counter-lean: the rider's spine leans an extra 20% of θ into the turn (racing style) on motorbikes, and 0% on scooters (upright, with the knees together).
* Four-wheelers: visual body roll = `0.6° per m/s²` of lateral acceleration (car), `1.0°` (bus, truck), clamped to 4° / 6°. Pitch = `0.8° per m/s²` on braking and acceleration. These are visual only; physics stays flat.

### 5.6 IK

* **Analytic two-bone IK** (law of cosines) for each arm and leg, with a pole vector: knees forward, elbows out and back.
* **Foot pitch** aligns to the ground normal (±25° clamp).
* **Hand IK on wheels:** target = rim point at angle (wheelAngle ± 60°). On hand-over-hand, release one hand when its target passes ±100° from the top and regrab at the opposite side; the cross-over takes 0.15 s.
* **Seat fit:** the pelvis goes to the seat socket, and leg IK reaches the pegs, floorboard or ground. If the reach error is > 0.05 m (for example build A on a tall truck seat), toes point down to make up the gap.

---

## 6. Vehicle handling per class

### 6.1 Principles

* **Arcade, readable, never punishing.** The existing raycast-wheel controller with surface grip tables and 3 s stuck recovery stays [R ARCHITECTURE §7.6].
* **Speeds are real-world class speeds** where the 1:1 world makes long trips playable (ARCHITECTURE §5.3). Traffic in the valley moves at about 20 km/h on average and the posted limit on main roads is 50 km/h [R vehicles_traffic §1].
* **Speed-sensitive steering** keeps every class controllable on a phone: the maximum wheel angle at speed v is `δmax(v) = min(δ0, atan(L·aLat / v²))`, where L is the wheelbase and aLat the class lateral-acceleration target. This makes slow manoeuvring tight and fast steering stable.
* **Weight** is felt through lower acceleration, slower steering rate, longer brake distance, visual body roll and pitch, camera lag and sound. Real mass is not needed.

### 6.2 Class table (proposed values; existing ones marked [R])

Minimum turning radius (front axle, kinematic) = `L / sin δ0` [C]. The real-world reference is the kerb-to-kerb turning circle (a UK bus must turn within a 12.5 m-radius circle under C&U Regulation 13 [S Wikipedia Turning radius]).

| Class (asset) | Top speed km/h (boost) | Accel m/s² (0–30 km/h time) | Brake m/s² | Wheelbase L (m) | δ0 max steer | Steer rate °/s | Min R front axle (m) [C] | aLat target m/s² | Max lean/roll | Character of handling |
|---|---|---|---|---|---|---|---|---|---|---|
| Walker | 16.2 run / 21.6 sprint [R] | 12 [R] | 16 [R] | — | — | 300 yaw [R] | 0 (turns in place) | — | ±15° | Instant, cute |
| Bicycle `ghm_veh_bicycle_a` | **25** (sprint 30 with pedal stamina) | 1.4 (6.0 s) | 4.0 | 1.12 | 40° | 200 | **1.74** | 6 | lean 30° | Light and twitchy at low speed; freewheel coast decel 0.4; uphill > 6% drops top speed by 1.5 km/h per % |
| Scooter `ghm_veh_scooter_a` | **70** (×1.2 = 84) | 5.0 (1.7 s) | 7.0 | 1.26 | 38° | 220 | **2.05** | 9.5 | lean 32° | Upright and forgiving; "CVT" smooth acceleration with no gear steps; small-wheel bump wobble ×1.3 |
| Motorbike `ghm_veh_motorbike_commuter_a` | **85** [R] (×1.2 = 102 [R]) | 6.5 [R] (1.3 s) | 8 [R] | 1.3 [R] | 35° [R] | 220 [R] | **2.27** | 10.5 [R] | lean 38° [R] | Nimble; the 4 gear steps are audible (cosmetic); **drifts on DIRT/MUD** from the grip tables |
| Cruiser `_motorbike_cruiser_a` | 100 | 5.5 | 8 | 1.39 | 33° | 180 | 2.55 | 9.5 | 35° | Heavier lean onset (lean spring f 1.6 vs 2.0) |
| Taxi / small hatchback `ghm_veh_taxi_small_a` | **75** [R] | 4.2 [R] (2.0 s) | 7 [R] | 2.36 (taxi [R] 2.2) | 34° (32 [R]) | 120 [R] | **4.2** | 8.5 [R] | roll 4° | Light car. Opt-in **drift**: brake + steer above 30 km/h drops rear grip to 0.6× for 0.8 s, with auto counter-steer assist of 50% |
| Private hatchback / EV | 90 | 4.5 (1.9 s) | 7.5 | 2.43–2.55 | 34° | 130 | 4.35–4.6 | 8.5 | 4° | EV: instant torque (accel 5.2 below 30 km/h), whine sound |
| SUV / jeep | 90 | 3.8 (2.2 s) | 7 | 2.68 | 33° | 110 | **4.9** | 7.5 | 5° | Higher camera; best on DIRT (grip table +10%) |
| Microbus (14–15 seats) | 80 | 2.8 (3.0 s) | 6.5 | 2.57 | 34° | 100 | 4.6 | 6.5 | 5° | Boxy wobble in crosswind (cosmetic) |
| Safa tempo (3-wheeler, EV) | **40** | 2.2 (3.8 s) | 5 | 2.10 | 38° | 110 | 3.4 | 5 | 6° (tilts on turns, never tips) | Whine; single front wheel steers |
| City minibus | 65 | 1.6 (5.2 s) | 5 | 3.8 | 38° | 70 | **6.2** | 4 | 6° | — |
| City bus (Sajha preset) / tourist bus | **60** (tourist 80 on highways) | **1.1** (7.6 s) | 4.5 | 5.4 / 5.6 | 42° | **55** | **8.1 / 8.4** (outer body ≈ 10.5 m) | **3.5** | 6° | Heavy: 0.25 s input delay on steering, wide swing (rear overhang 2.6 m swings out 0.6 m), "kneel" hiss at stops |
| Truck (2-axle painted) / tipper / tanker | **55** (highway 70) | **1.0** (8.3 s); loaded ×0.8 | 4.0 | 4.8 | 40° | 60 | **7.5** | 3.5 | 6° | Heavy; air-brake hiss; engine-brake decel 1.2 off throttle; **tanker slosh** (visual body sway spring f 0.8, ζ 0.3) |
| Tractor + trolley | **30** | 1.5 | 4 | 1.95 | 38° | 90 | 3.2 | 3 | 5° | Trolley follows as a trailer (hitch 1.2 m behind the rear axle); bouncy |

Speed-sensitive steering, worked example for the motorbike [C]: at 30 km/h, `δmax = atan(1.3·10.5/8.33²)` = 11.2°, which gives R ≈ 6.7 m. At 50 km/h, δmax = 4.1° and R ≈ 18 m. Lean at that limit is atan(aLat/g) = 47°, so the 38° cap governs and aLat effectively becomes ≈ 7.7 m/s².

Bus reality check [C]: kinematic R = 8.4 m at the front axle. Adding half the track (1.05 m) and the front overhang gives an outer-body sweep of about 10.5–11 m. That sits inside the 12.5 m regulatory circle, so the bus can turn at the ring-road junctions but not into 3–5 m old-core lanes. The access mask already forbids it there with a friendly prompt [R ARCHITECTURE §7.6].

### 6.3 Two-wheeler feel details

* **Lean leads steering:** the visual lean target leads the yaw by 0.08 s (a counter-steer feel without a counter-steer input).
* **Low-speed balance:** below 5 km/h the bike wobbles ±3° (noise at 1.2 Hz) and below 1 km/h a foot goes down. **A two-wheeler never falls over.**
* **Pillion NPC** (offer-a-ride activity, P2): +10% to braking distance and −8% acceleration, and the rider's lean is shared.
* **Bicycle stamina:** sprinting drains an energy meter 0→100 in 12 s and refills in 8 s. Uphill gradients above 10% let the player hop off and push the bike at 1.2 m/s. This is real Kathmandu hill practice and is never forced.

### 6.4 Bus, taxi and truck as gameplay

| Role | Loop | Numbers [E] |
|---|---|---|
| **Bus passenger** | Walk to an OSM `bus_stop` (380 in the valley [R vehicles_traffic §1]). A bus arrives every 2–6 game minutes on that route (by time of day). Board, then choose: sit (camera roams, ride-along) or stand (sway). Press **Stop** (the bell) to get off at the next stop. Skip-to-next-stop as in ARCHITECTURE §5.3 | Dwell 8–15 s per stop; bus targets 30 km/h urban; skip fades 1.2 s |
| **Bus driver** | Borrow a bus from a bus park (Gongabu, Ratnapark-style, `bus_station` polygons). Follow a real route with stops: pull into a 25 m stop zone under 5 km/h, doors open automatically, 3–8 NPCs board with a "conductor" voice cue | Stars for gentle stops (decel < 2 m/s²) and on-time arrivals; **no penalties** |
| **Taxi passenger** | Hail gesture (hold the action button 0.4 s: arm up). The nearest free taxi within 150 m pulls over within 30 m. Pick a destination on the map (bottom sheet in portrait). Ride-along with skip | Fare in game coins: 10 + 2 per km [E] (or free in "relaxed" settings) |
| **Taxi driver** | Borrow a taxi from a taxi stand (22 in OSM [R]). Optional fares: an NPC waves, gets in the left rear, and names a landmark; the route ribbon guides | Tips scale with smooth driving; the passenger chats with fun facts about the destination (localised) |
| **Truck** | Borrow at a depot or fuel station on the trunk roads; deliver cargo (bricks, vegetables, water) | Water tanker deliveries in residential areas in the morning (vehicles_traffic: tankers ×2 mornings) |

### 6.5 Where vehicles come from (no theft)

The PEGI 7 / 9+ framing rules out GTA-style carjacking. Proposal [E] (owner decision, §14 Q1):

1. **Own garage:** the player's scooter, bicycle and unlocked vehicles. Summon one with a whistle button (ASSET_MANIFEST icon "garage"). It rolls up within 20 s from an off-screen road node ≥ 60 m away, never popping in view.
2. **Community fleet ("Ghumante share"):** any **parked** vehicle with a small green key tag (≈ 30% of parked vehicles in streets [E]) can be borrowed by walking up and pressing Hop on. On leaving it more than 150 m away or after 10 min, it **auto-returns** (fades out at its next unseen moment and respawns at its home spot).
3. **Moving NPC vehicles:** the player can **only ride as a passenger** (hail taxi, bus, micro, tempo, rickshaw). Nobody is ever pulled out.
4. **Rentals:** OSM `amenity=bicycle_rental`, motorbike rental shops in Thamel and Lakeside (gateway for tourist players).

---

## 7. Enter / exit interaction design

| Step | Rule | Number |
|---|---|---|
| Detection | Seat sockets within range in a 120° cone in front of the player (or the camera-forward cone in portrait) | **2.0 m** two-wheelers, **3.0 m** cars, **3.5 m** buses and trucks (door sockets) |
| Prompt | Contextual button morphs from Jump to **Hop on** (icon of the vehicle class). With several candidates, pick the best score = 0.6·(1 − distance/range) + 0.4·cos(angle). A second small button "Ride as passenger" appears when a passenger seat exists | Prompt fades in over 0.12 s; hysteresis 0.3 m to stop flicker |
| Approach | Auto-walk on a straight or arc path to the side-specific socket (§4.7), avoiding obstacles with the player's own capsule | ≤ 4 m, at 2.2 m/s; abort if blocked for 0.5 s (then a "can't reach" wiggle) |
| Align | Turn to the socket orientation | ≤ 0.2 s |
| Mount | Clip from §4.7 with IK blending in over 0.15 s | 0.5–1.4 s |
| Handover | Control maps switch (§9); camera blends from walk rig to vehicle rig | Camera blend 0.45 s, ease-in-out |
| Exit | Same button (now "Hop off"). Auto-brake to < 8 km/h first (bus/truck: to 0) | Exit clip 0.35–1.0 s; camera blend 0.35 s |
| Exit placement | Player placed at the exit socket, snapped to the navmesh and ground; if occupied, try the opposite side, then behind | Search radius 2.5 m |
| Gamepad / keyboard | E / gamepad A = Hop on / off (already `ToggleMode` [R]); hold = passenger | Hold threshold 0.35 s |

**Sacred compounds:** inside `SACRED_NO_VEHICLE` areas (329 in the valley [R CONTENT_COVERAGE L16]), the Hop on prompt is replaced by a gentle "Vehicles rest outside" note, and two-wheelers auto-park at the gate (side-stand clip). The owner overrode the no-entry rule for **people**, not vehicles.

---

## 8. Cameras per vehicle

The repo rig keeps the player's ground point at a fixed screen height and pins the look-ahead [R ChaseRigProfile]. The table extends it per class; distance roughly follows `d = 6.0 + 1.0·vehicleLength` (landscape) [E]. Portrait adds +10–15% distance, +8–10° pitch and doubles the look-ahead time (per the existing pattern).

| Rig | Landscape d / pitch / pivot (m, °, m) | Portrait d / pitch / pivot | Look-ahead L / P (s, max m) | Min hFOV | Follow time (s) | Speed FOV kick | Shake |
|---|---|---|---|---|---|---|---|
| Walk | 8.4 / 12 / 1.0 [R] | **6.8 / 20 / 0.9** (proposed; [R] 8.6 / 22 / 1.0) | 0.3, 2.5 / 0.6, 4.5 [R] | 55° [R] | 0.9 / 1.0 [R] | none | footstep 0 |
| Bicycle | 7.5 / 12 / 1.0 | 8.5 / 20 / 1.0 | 0.4, 6 / 0.8, 10 | 62° | 0.30 | +3° at top speed | bumps 0.02 m |
| Scooter / motorbike | 8.0 / 13 / 1.0 [R] | 9.2 / 21 / 1.0 [R] | 0.45, 9 / 0.9, 16 [R] | 62° [R] | 0.28 / 0.32 [R] | +6° above 60 km/h | surface table [R] |
| Car / taxi | 9.5 / 14 / 1.3 | 10.8 / 22 / 1.3 | 0.5, 12 / 1.0, 20 | 62° | 0.35 | +5° | 0.6× bike |
| SUV / micro / tempo | 10.5 / 15 / 1.6 | 12.0 / 23 / 1.6 | 0.5, 12 / 1.0, 20 | 62° | 0.40 | +4° | 0.7× |
| Bus | **16.5 / 17 / 2.6** | **18.5 / 25 / 2.6** | 0.6, 18 / 1.2, 28 | 64° | 0.60 | +2° | low-frequency sway 0.3 Hz |
| Truck / tanker | 14.5 / 16 / 2.4 | 16.5 / 24 / 2.4 | 0.6, 16 / 1.2, 26 | 64° | 0.55 | +2° | 0.8× |
| Tractor + trolley | 10.0 / 18 / 1.8 | 11.5 / 26 / 1.8 | 0.3, 6 / 0.6, 10 | 62° | 0.50 | none | 1.2× |
| Passenger ride-along | Orbit at 1.2× the driver rig, auto-yaw 6°/s toward points of interest within 300 m (landmarks, peaks) | same | — | 62° | — | — | — |
| Seated / emote close-up | 3.5 / 8 / 1.1, framing 3/4 front | 4.0 / 12 / 1.1 | — | 50° | 0.6 | — | — |

**Camera collision and old-core lanes**
* Sphere cast radius 0.3 m from the pivot to the desired position; minimum distance 2.5 m (walk) / 4 m (vehicles). The camera pulls in at 20 m/s and eases back out over 0.6 s.
* **Lane mode:** when walls are within 3 m on both sides (Ason, Bhaktapur lanes 3–5 m wide [R newar_houses]), pitch +15°, distance −15%. This looks over the eaves instead of into walls. Blend 0.5 s.
* Occluders between the camera and the player (eaves, struts, wires) **dither out** within a 1.2 m radius around the player's screen position. Use opaque dither, not alpha, per ASSET_MANIFEST.
* Juice references: screen shake by trauma² with Perlin noise, decaying 1.5/s, clamped at 0.25 m / 1.5° [S Eiserloh GDC 2016 "Juicing Your Cameras With Math"]. The only triggers are landing, bumps and honk bounce, so shake stays minimal for a calm explorer.

---

## 9. Mobile touch controls

Units are iOS points (pt) ≈ Android dp. Apple's minimum target is **44×44 pt** and Material's is **48×48 dp**, with ≥ 8 pt/dp spacing [S HIG / Material]. Positions are percentages of the **safe area** (`SafeArea`, ARCHITECTURE §7.10a). All buttons use `ghm_ui_hud_actions` [R].

### 9.1 Portrait (one thumb, bottom third)

| Mode | Control | Size | Position (x%, y% from bottom-left) | Behaviour |
|---|---|---|---|---|
| On foot | **Floating stick** | Ring 120 pt Ø, knob 56 pt | Spawns where the thumb lands anywhere in the lower 45% (excluding the button column) | Dead zone 12% of radius; walk below 60% deflection, run above, sprint by pushing to 100% for 0.6 s (or the Boost button); recentres on release over 0.1 s |
| On foot | **Action** (Jump ↔ Hop on ↔ Sit ↔ Namaste/Talk, contextual) | **72 pt** | (84%, 14%) | Morphs icon by context (§7) |
| On foot | Emote wheel | 52 pt | (84%, 27%) | Opens a radial with 6 slots (namaste default at the top) |
| On foot | Camera look | — | One-finger drag on the upper 55% | 0.25°/pt yaw, 0.15°/pt pitch |
| Vehicle | **Throttle (hold)** | 88 pt | (84%, 12%) | Pressure-free; hold = 100%; slide up 30 pt = boost |
| Vehicle | **Steer (horizontal drag)** | Anywhere in the lower 45% left of the throttle | Drag from the touch origin; **full lock at 70 pt** of drag, with a response curve exponent 1.6 (fine near centre) | Release = recentre over 0.12 s |
| Vehicle | **Brake / reverse** | 64 pt | (66%, 8%) | Brakes, then reverses after 0.4 s stopped [R ControlFrame] |
| Vehicle | Horn | 52 pt | (66%, 22%) | Bounce animation; horn model per vehicles_traffic §4.4 |
| Vehicle | Hop off | 52 pt | (92%, 30%) | — |
| Vehicle | Cruise assist | 44 pt toggle | top-right under the minimap | Keeps lane and speed (ARCHITECTURE §5.3) |

**Options:** auto-throttle (steering only, the vehicle holds the road's game target speed); **tilt steering** (±25° device roll = full lock, dead zone 3°, off by default); left-handed mirror layout; steer assist (road assist `RoadAssistPerS` up ×1.5 [R]).

### 9.2 Landscape (two thumbs)

| Mode | Left thumb | Right thumb |
|---|---|---|
| On foot | Floating stick in the left 40% (ring 140 pt) | Action 80 pt at (88%, 18%); Emote 56 pt at (76%, 12%); Sprint 56 pt at (92%, 38%); camera drag on the right upper 60% |
| Two-wheeler / car | Steer: stick (X only) **or** two arrow pads 72 pt (option) | Throttle 88 pt at (88%, 18%); Brake/reverse 72 pt at (74%, 12%); Boost 56 pt at (90%, 42%); Horn 52 pt at (76%, 32%) |
| Bus / truck | Same; plus a **Doors / Stop** button 56 pt at (12%, 60%) when at a stop | Same |

### 9.3 Feel and accessibility

* Haptics (iOS UIImpactFeedback, Android VibrationEffect): light on footstep landing (≥ 1 m falls only), medium on bumps, a selection tick on the Hop on prompt appearing. Off in settings.
* **Hold-to-toggle** option for throttle and sprint, so motor-impaired players can ride long distances.
* Input latency budget: touch → visible response ≤ 50 ms (one 60 fps frame plus the input poll).
* Gamepad (ignores orientation [R]): left stick move/steer, RT/LT throttle/brake, A hop on/off and jump, X boost, Y search, B horn, RB emote wheel.

---

## 10. Non-violent contact rules

| Situation | What happens | Numbers [E] |
|---|---|---|
| Player vehicle heading at a **pedestrian** | **Predictive hop-aside:** every pedestrian in a capsule of length `v·0.8 s + 2 m` ahead of the vehicle (width = vehicle width + 1.0 m) picks the clear side and does a comic hop: 0.35 m high, 1.5 m sideways, over 0.45 s, with a "Hoi!" bubble (localised) or a startled-laugh face | Prediction radius ≥ 4 m; reaction delay 0.1–0.25 s random; 1 in 4 do a spin-hop |
| Contact still happens (high speed, crowd) | Pedestrian becomes kinematic, gets a bounce-hop 0.5 m up and 2 m sideways, lands on their feet, dusts off and waves. Vehicle speed is cut to 40% with a soft "boing" squash. **No ragdoll, no falling down, no injury, no sound of pain** | Vehicle `body` squash 0.9 |
| Crowded squares (`SACRED`, Durbar squares, Asan) | Vehicles are mostly blocked by access masks. Where two-wheelers are allowed, the player's top speed is auto-capped at **15 km/h** within crowd areas (> 0.2 people/m²) | Cap blends over 1 s |
| **Cows / bulls** (sacred, national animal) | A **soft cushion**: within 4 m of a cow the player's vehicle is speed-limited to 5 km/h; within **1.6 m** it hard-stops with a gentle bump and squash. The cow never reacts with alarm: it keeps chewing, maybe moos and flicks its tail. The player must steer round. A horn tap near a cow plays a polite "pip" and the cow ignores it | No damage, no contact ever [R ARCHITECTURE §7.7, ASSET_MANIFEST §10] |
| Dogs, goats, chickens, ducks, pigeons | Hop or flutter aside like pedestrians (chickens flap 0.6 m up; pigeons take off as a flock if the player runs through them, which is a fun beat at Basantapur) | Flush radius 3 m (walk/run) |
| Monkeys | Keep 2 m away and leap onto walls | — |
| Player on foot runs into an NPC | Both stop; a shoulder-bump wobble; the NPC says "sorry" or "maaf garnu" style lines (review) | — |
| NPC vehicle near the player on foot | NPC vehicles always yield: slow to 15 km/h within 6 m, stop within 2.5 m [R vehicles_traffic T9]. Player walking into a parked or stopped vehicle bounces off | — |
| Vehicle vs vehicle | Bumper-car bounce: impulse 0.5× closing speed, squash on both, honk from the NPC. **No damage states, ever** [R ASSET_MANIFEST §8] | — |
| Vehicle vs wall / pole / temple | Bounce with a squash 0.85 and a star puff. Temples and shrines have a **0.8 m soft margin** so even contact feels gentle | — |
| Fall from height (on foot) | ≤ 4 m: normal landing; > 4 m: roll and "ta-da" | — |
| Water | No swimming: a comic bounce back to the shore [R ASSET_MANIFEST §9.4 water] | — |
| Rule-breaking (wrong side, running a police-controlled chowk) | A traffic officer's whistle and "Bistārai!" bubble. There is no wanted level, no fine and no chase. The optional "Kind explorer" badge counts smooth, polite driving | — |

All of this runs **without physics ragdolls**. Pedestrians are kinematic agents with a scripted hop curve (`y = 4h·t(1−t)`), and lateral motion follows the same ease. That is cheap and fully deterministic.

---

## 11. Player audio hooks (synthesis or CC0 only)

| Sound | Trigger | Source plan |
|---|---|---|
| Footsteps × 9 surfaces: brick, stone flags, asphalt, dirt, mud, grass, wood, metal, water [R street_life §15] | Heel strike from the gait phase (§5.2); shoe type modifies (sneaker soft, chappal "flap" +2 kHz click, boots heavier) | Procedural: filtered noise burst (20–60 ms) + resonant body per surface (brick 900 Hz band, stone 1.6 kHz, wood 400 Hz with a 120 ms decay, metal 2.5 kHz ring), ±3 dB / ±8% pitch jitter. Or CC0 recordings (Freesound CC0 filter only) |
| Cloth rustle | Arm swing peaks while running | Bandpassed noise, −24 dB |
| Breathing / puff | Sprint > 4 s, climbs | Synth noise envelope, 0.4 Hz |
| Helmet pop, topi wobble, mount "hup", landing "boing" | Clip events | Synth (sine sweeps, FM boing 220→440 Hz) |
| Hop-aside "Hoi!" | Pedestrian hop | Voice bark recorded in-house (owned) or a non-verbal synth chirp |
| Vehicle engines | See [vehicles_traffic.md §6](vehicles_traffic.md) | — |

---

## 12. Data and implementation mapping (for the code workflow; this doc changes no code)

| Piece | Where (assembly) | Data |
|---|---|---|
| `CharacterRecipe` (build, skin #, hair id+colour, outfit slots with param sets, seed) | `Ghumante.Characters` (engine-free struct) + save section `player.appearance` | JSON in the save; ~120 bytes |
| Primitive mesh builders (superellipsoid, tapered capsule, loft, lathe, offset shell) | `Ghumante.Characters` (Burst job) | Params from §2.3–2.7 |
| Gait generator, springs (f, ζ, r), IK solver, squash | `Ghumante.Characters` (engine-free math; unit tests like `core-tests/Driving*`) | §4–5 tables as `CharacterMotionProfile` |
| Key-pose clips (namaste, wave, mount ×12…) | `Resources/Characters/Poses/*.json` | Bone quaternions per key; 30 fps |
| Vehicle class specs | Extend `VehicleSpec` presets: Bicycle, Scooter, Car, SUV, Micro, Tempo, Minibus, Bus, Truck, Tractor (§6.2) | Existing fields plus `SteerInputDelayS`, `VisualRollPerMps2`, `VisualPitchPerMps2`, `RearOverhangM` |
| Enter/exit | `Ghumante.Characters` state machine + `Ghumante.Vehicles` sockets | §4.7, §7 numbers |
| Camera profiles per class | `ChaseRigProfile` statics per class × orientation | §8 |
| Contact rules | `Ghumante.Traffic` (pedestrian hop, cow cushion) | §10 |

**Tests to add (suggested):** gait foot-slide < 0.02 m per stance at all speeds; IK reach error < 0.01 m for the 4 builds on all 12 vehicle seats; spring stability at dt = 1/20 s; turning radius per class within ±5% of §6.2; enter/exit placement never inside geometry (fuzz 1 000 random parked spots); the hop-aside capsule always clears pedestrians at the class top speed; no frame where a player-vehicle collider overlaps a cow collider.

---

## 13. Cultural and rating checklist (reviewer)

1. Daura suruwal: 8 ties (4 pairs), 5 pleats, closed round neck; topi asymmetric with the front higher; nothing comic about the national dress (review).
2. Namaste: palms at the chest, fingers up, slight head bow; namaskar higher for elders (review).
3. Prayer wheels: right hand, clockwise; kora clockwise at stupas (anticlockwise only at Bon sites) [R CONTENT_COVERAGE O10].
4. Calm mode inside `SACRED` areas: no silly fidgets, no running emotes, no vehicles; shoes-off prompt where curated `attrs` say so.
5. Helmets always on two-wheelers, for pillions too; no riders on bus roofs [R ASSET_MANIFEST §8].
6. No theft, carjacking, damage, injury, ragdoll, police chase or wanted level.
7. Skin swatches are numbered, not named after groups; the picker opens on a random swatch.
8. No brands on shoes, jackets or helmets.
9. Cows: no contact, no alarm reactions, always given way.
10. Kumari never shown or photographed [R ASSET_MANIFEST §6].

## 14. Open questions for the product owner

| # | Question | Default if no answer |
|---|---|---|
| Q1 | Vehicle access model: own garage + community fleet (borrow and auto-return) + passenger-only for moving vehicles (§6.5)? | Yes, as proposed |
| Q2 | Player age reading (young adult, 16–25) so driving buses and trucks is plausible? | Yes |
| Q3 | Taxi fares in coins or free? | Coins with a "relaxed" free option |
| Q4 | Portrait walking camera 6.8 m (closer than the current 8.6 m) for face readability? | Try in Wave 3 device pass |
| Q5 | Opt-in car drift on paved roads, or only on loose surfaces? | Opt-in, gentle, as in §6.2 |
| Q6 | Tilt steering as an option? | Off by default, available |

---

## 15. Sources

Repository (read 2026-10-05): `docs/ARCHITECTURE.md` §5.3, §7.6, §7.7, §7.10a, §8; `docs/ASSET_MANIFEST.md` §5, §8, §9, §10, §11; `docs/CONTENT_COVERAGE.md` header, L16, O10, S2; `docs/research/w2/vehicles_traffic.md`, `street_life.md`, `newar_houses.md`; `game/Assets/Ghumante/Core/Driving/VehicleSpec.cs`; `game/Assets/Ghumante/Vehicles/Simulation/VehicleTuning.cs`; `game/Assets/Ghumante/Characters/Cameras/ChaseRigProfile.cs`; `game/Assets/Ghumante/Characters/Control/ControlFrame.cs`.

External:
* Dhaka topi (shape, 3–4 in height, mountain-peak symbolism): https://en.wikipedia.org/wiki/Dhaka_topi ; museum specimens: https://www.britishmuseum.org/collection/object/A_As1992-10-43 ; topi types: https://english.onlinekhabar.com/nepali-topi-types.html
* Daura-Suruwal (8 strings *Astamatrika singini*, 5 pleats, national dress 2017): https://en.wikipedia.org/wiki/Daura-Suruwal ; https://thehimalayantimes.com/opinion/daura-and-suruwal-their-history-journey
* Chautari: https://en.wikipedia.org/wiki/Chautari ; https://thehimalayantimes.com/nepal/traditional-resting-platforms-displaced-development/
* Namaste etiquette: https://factsanddetails.com/south-asia/Nepal/People_Nepal/entry-7827.html ; https://www.acethehimalaya.com/what-does-namaste-mean/
* Nepal ethnic diversity (142 groups, 2021 census): https://en.wikipedia.org/wiki/Ethnic_groups_in_Nepal ; https://en.wikipedia.org/wiki/2021_Nepal_census
* Average adult height by country (NCD-RisC): https://ourworldindata.org/human-height
* Pillion helmet law and compliance: https://thehimalayantimes.com/kathmandu/pillion-riders-will-have-to-wear-helmet/ ; https://depositonce.tu-berlin.de/items/bd04b54b-cf56-4890-9488-2a34497c7e9b
* Chibi and cartoon proportions: https://tips.clip-studio.com/en-us/articles/4863 ; https://tips.clip-studio.com/en-us/articles/4868
* Silhouette-first character design: Animal Crossing villagers https://gonintendo.com/stories/368235-nintendo-details-their-process-for-creating-unique-villagers-in-a ; Fall Guys anatomy https://www.essentiallysports.com/fall-guys-concept-artist-shares-the-adorable-anatomy-of-the-characters-mediatonic-esports-news/ ; shape language https://rocketbrush.com/blog/shape-language-in-game-character-design-how-to-make-characters-readable-and-consistent
* David Rosen, "An Indie Approach to Procedural Animation", GDC 2014: https://www.gdcvault.com/play/1020583/Advanced-Animation-with ; summary https://gamedeveloper.com/design/video-an-indie-approach-to-procedural-animation
* t3ssel8r, "Giving Personality to Procedural Animations using Math" (2022), second-order dynamics: https://www.youtube.com/watch?v=KPoeNZZ6H4s ; Rust port documenting the parameters: https://docs.rs/spring_motion
* Squirrel Eiserloh, "Math for Game Programmers: Juicing Your Cameras With Math", GDC 2016: https://www.gamedeveloper.com/programming/video-sprucing-up-cameras-with-math
* Coyote time and input buffering: https://bugnet.io/blog/coyote-time-and-input-buffering-explained
* Unity Animation Rigging constraints: https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.2/manual/ConstraintComponents.html ; https://unity.com/resources/procedural-poses-motion-animation-rigging
* Walking speed (≈1.4 m/s) and walk-run transition (≈2 m/s): https://help.traveltime.com/en/articles/8524005-what-is-the-default-walking-speed ; Froude number / dynamic similarity: https://en.wikipedia.org/wiki/Froude_number
* Lean angle `θ = atan(v²/gR)`: https://arxiv.org/pdf/1611.03857
* Turning circle definitions; UK bus 12.5 m circle (C&U Reg. 13): https://en.wikipedia.org/wiki/Turning_radius ; single-unit truck/bus turning path: https://dimensions.com/element/single-unit-truck-buses-20-foot-wheelbase-turning-path-radius
* Touch target sizes (Apple 44 pt, Material 48 dp, 8 pt spacing): https://blog.openreplay.com/improving-tap-targets-mobile-ux/ ; https://ezud.com/touch-target-sizes-mobile-accessibility/

Licences: all sources above are **reference only**. No images, meshes or sounds are taken from them. Ship sounds only from CC0/public-domain or synthesis (§11).
