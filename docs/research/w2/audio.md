# W2 research: audio for a cartoon Kathmandu (sound catalogue, synthesis recipes, CC0 sources, mobile mix)

> Status: research input for Wave 2 "Kathmandu comes alive", 2026-10-05. Feeds the `Ghumante.Audio` assembly (ARCHITECTURE §7.1), the audio budgets in ARCHITECTURE §10 (25 / 40 / 50 MB per tier) and the audio section of [ASSET_MANIFEST](../../ASSET_MANIFEST.md) §13 (formats §13.1, ambience §13.3, vehicles §13.4, foley/animals/sacred §13.5). It complements [street_life.md](street_life.md) §15 (ambience per area type) and uses its area types and time-of-day table.
> Every number is meant to become a **parameter** in a ScriptableObject or a synthesis struct. Confidence tags (same scheme as the other W2 files):
>
> | Tag | Meaning |
> |---|---|
> | **[S]** | Stated by a cited source (URL in the row or in §12) |
> | **[E]** | Estimate or design value (physics, convention, tuning by ear). Good enough for a cartoon world; tune on device |
> | **[V]** | Needs a human check (cultural reviewer, licence check on the exact file, or a device measurement) before it ships |
>
> **Licence rule (task and LICENSES.md):** only **CC0 / public-domain recordings or procedural synthesis** ship. Reference recordings or videos may be listened to, never shipped, sampled or traced. Sacred chants and ceremonies are never recorded or used as a bed without permission (ASSET_MANIFEST §13).

---

## 0. Ten-line summary

1. ~120 distinct sound behaviours are needed for W2 (§2): footsteps on 10 surface families, 9 powered vehicle sound models + bicycle, 6 horn types, people/crowds, sacred sounds (bells, conch, cymbals, prayer wheels, instrumental bhajan), 8 birds and 6 other animals, weather (wind, rain on tin, thunder), aircraft (ATR 72, A320, AS350 helicopter) and a distant city hum.
2. **Split by method** (§3): ~65% fully procedural (all engines, horns, bells, birds, wind, rain, aircraft, air brakes, bicycle freewheel), ~25% CC0 recordings (footsteps, cloth, crowd-walla texture, water, door clunks), ~10% hybrid (recorded grain + procedural control).
3. Engines are **additive firing-order synthesis** driven by RPM and load: firing frequency f = RPM/60 × cylinders / (2 for 4-stroke, 1 for 2-stroke); harmonic tables per vehicle in §4.2; Royal Enfield-style thump, CVT scooter drone, 3-cylinder Alto burble and 6-cylinder diesel bus are distinct presets.
4. Phone speakers reproduce almost nothing below ~250–400 Hz [E], so every low source (bus, Enfield, bells, aircraft) carries strong 2nd–8th harmonics ("missing fundamental") and an optional "speaker" EQ profile (§6.8).
5. Unity 6.3 adds a **scriptable audio pipeline with `IAudioGenerator`** (Burst-compatible generators feeding an AudioSource) [S]; use it for real-time synthesis voices, with `OnAudioFilterRead` as the fallback. One-shots (bells, horns, birds, barks) are **baked once into AudioClips at region load** from deterministic seeds: zero download, ~3 MB PCM.
6. CC0 sources that pass a licence check (§5): Freesound with the CC0 filter (per-file), Kenney audio packs (CC0 [S]), BigSoundBank (CC0 1.0 [S]), OpenGameArt **only items tagged CC0**, Wikimedia Commons PD/CC0. **Excluded:** Sonniss GDC bundles (proprietary EULA, not CC0 [S]), Pixabay, Mixkit, Zapsplat, BBC, any CC-BY/NC.
7. Voice budget (§6.1): 24 / 32 / 48 real voices (Low/Mid/High), of which at most 4 / 8 / 12 are real-time synthesis voices; a priority scheme keeps player, horns near the player and landmark bells audible.
8. Distance model (§6.2): custom rolloff curves per class that reach zero at max distance (Unity's logarithmic rolloff plateaus instead), plus an air-absorption low-pass for far sources (aircraft heard to 6–9 km). **Doppler is computed in our own code** because floating-origin rebases (ADR-003) would otherwise produce pitch spikes.
9. Ambience is zoned (§7) on the street_life 250 m grid (old core, urban, peri-urban, fields, forest) plus special zones (temple compound, stupa kora, durbar square, ghat, airport, ring road) × 6 time-of-day bands × 3 seasons; occlusion uses courtyard/galli snapshots and one round-robin raycast per voice at 4 Hz, never per-frame per-voice.
10. The whole valley audio footprint fits **≈ 7.5 MB compressed on disk and ≈ 14 / 20 / 26 MB resident** (§8), inside the manifest's ≤ 12 MB M1 audio download and the 25 / 40 / 50 MB tier budgets.

---

## 1. Constraints already fixed elsewhere (do not re-decide)

| Item | Value | Where |
|---|---|---|
| Audio memory per tier | Low 25 MB, Mid 40 MB, High 50 MB (sub-budgets: ambience 8/12/15, vehicles 5/8/10, foley 3/5/6, animals 2/4/5, UI 2/3/3, voice 1/2/3, music 2/3/4, reserve 2/3/4) | ARCHITECTURE §10, ASSET_MANIFEST §13.1 |
| M1 audio in the install | ≤ 12 MB; later regions ≤ 8 MB audio each | ASSET_MANIFEST §5 table |
| Formats | Masters 48 kHz / 24-bit WAV; ambience beds Vorbis ~0.4 stereo 32 kHz; spots Vorbis mono 32 kHz; engine loops ADPCM mono 32 kHz; short SFX ADPCM mono 22.05 kHz | ASSET_MANIFEST §13.1 |
| Loudness | ≈ −16 LUFS integrated, −1 dBTP | ASSET_MANIFEST §13.1 |
| Asset ids | `ghm_amb_*`, `ghm_sfx_veh_*`, `ghm_sfx_tyre_*`, `ghm_sfx_fs_<surface>_{walk,run}_NN`, `ghm_sfx_ani_*`, `ghm_sfx_sacred_*` | ASSET_MANIFEST §13.3–13.5 |
| Surfaces | `Surface` enum: ASPHALT, CONCRETE, BRICK, COBBLE, GRAVEL, COMPACTED, DIRT, MUD, SAND, GRASS, ROCK, SNOW_ICE, WOOD, METAL (+ UNKNOWN) | `pipeline/ghumante_pipeline/model.py` (read only) |
| Engine | Unity 6000.3.25f1 | `game/ProjectSettings/ProjectVersion.txt` |
| Rating | 9+ / PEGI 7, non-violent: no crashes with sound of injury, no sirens of tragedy, animals never hurt | brief |

**One change proposed to §13.1:** footsteps and short SFX as **ADPCM "Compressed in Memory"** rather than "Decompress on Load". ADPCM decodes very cheaply, and decompressing on load stores 16-bit PCM (≈ 3.5× larger) [E, Unity import docs describe ADPCM as ~3.5:1]. The manifest owner decides.

---

## 2. Sound catalogue

Method codes: **P** = procedural at runtime (real-time generator), **B** = procedural, **baked** to an AudioClip at region load, **R** = CC0 recording, **H** = hybrid (recorded grains, procedural control). Priority uses the manifest's P0/P1/P2.

### 2.1 Footsteps per surface (player and nearby NPCs)

Cadence [E]: walk 1.8–2.0 steps/s (108–120 per min), jog 2.6 steps/s, run 2.9–3.0 steps/s; the cartoon character's footstep is triggered by animation events, not by a timer. Each set: 6 walk + 6 run variants, ≤ 0.35 s each, plus land and scuff. Random pitch ±4% (walk) / ±6% (run), volume ±1.5 dB, no immediate repeat (Audio Random Container "Random, avoid repeat" [S]).

| Footstep set | `Surface` values mapped | Where in the valley | Method | Character of the sound | Spectral notes [E] |
|---|---|---|---|---|---|
| `fs_asphalt` | ASPHALT, CONCRETE, UNKNOWN on roads | Ring Road, arterials | R | Dry, short heel-toe tap | Transient 1–4 kHz, 60–120 ms, no tail |
| `fs_brick` | BRICK | Newar old-town streets, chowks, courtyards (ma-apa brick, herringbone) | R | Slightly hollow, gritty, small clatter | Like asphalt + grit layer 3–6 kHz; 10% of steps add a loose-brick "clink" |
| `fs_stone` | COBBLE, ROCK | Durbar-square flags, temple plinths, stone stairs (Swayambhu's eastern stairway) | R | Hard, clean tap with short ring | 1.5–5 kHz, 40–80 ms; small courtyard slap if inside compound (§6.5) |
| `fs_gravel` | GRAVEL, COMPACTED | Unsealed lanes, construction margins, river banks | R or H | Crunch, 100–200 ms grain cluster | Granular: 15–40 micro-impulses per step, 2–8 kHz |
| `fs_dirt` | DIRT, SAND | Field paths, peri-urban lanes | R | Soft thud + dust scuff | Thud 150–400 Hz, scuff noise 1–3 kHz |
| `fs_mud` | MUD, and DIRT in monsoon (weather rule from ARCHITECTURE §7.6) | Fields, unpaved lanes Jun–Sep | R | Squelch + suck-out on lift (comic, exaggerated) | Lift sound is separate and delayed 120–200 ms after contact |
| `fs_grass` | GRASS | Tundikhel, Ratna Park, Bhrikutimandap, field edges | R | Soft swish | Band noise 2–6 kHz, slow attack 20 ms |
| `fs_wood` | WOOD | Temple platforms (dabali), wooden balconies, plank bridges, temple interiors | R | Hollow knock, resonant | Resonance 120–300 Hz under the click |
| `fs_metal` | METAL | Bailey and suspension bridges, manhole covers, tin sheets | R | Clank + ring for grates | Ringing partials 400 Hz–3 kHz, decay 0.2–0.5 s |
| `fs_water` | puddle decals, `ghm_sfx_splash_puddle`, shallow fords, hiti (stone water spout) basins | Monsoon puddles, dhunge dhara pits | R or B | Splash, wet slap | Bubble chirps (B: sine 600–2,000 Hz with upward 10–20% glide, 20–40 ms) + noise |
| `fs_stairs_stone` | stairs on steps ways | Swayambhu stairs, temple plinths | R (reuse `fs_stone`) | Heavier heel | +2 dB, −5% pitch |
| `fs_barefoot` | inside temple sanctum zones where shoes come off (Pashupati and many shrines require removing shoes) [V] | Temple interiors | R | Soft pat | Low-passed 2 kHz; triggered when the player is in a `shoes_off` zone |

The barefoot set is a cultural touch: the character takes shoes off at a compound threshold that requires it (shoe-rack prop + sound), only where a reviewer confirms the custom.

### 2.2 Two-wheelers and three-wheelers

Traffic mix on urban roads: motorbike/scooter 60–70% of moving vehicles [E, street_life §9]. Kathmandu-specific models are listed only as sound references; nothing is branded in game.

| Model id | Real-world reference (sound only) | Layout | Idle / max RPM [E] | Firing freq. idle → max | Character to capture | Method |
|---|---|---|---|---|---|---|
| `moto_commuter` | 100–125 cc single 4-stroke commuters (Honda Shine, Hero Splendor, Bajaj) | 1-cyl, 4-stroke | 1,400 / 8,500 | 11.7 → 71 Hz | Busy "putter" at idle, nasal buzz at speed | P |
| `moto_sport` | 150–200 cc single (Pulsar, FZ, Apache) | 1-cyl, 4-stroke | 1,400 / 10,000 | 11.7 → 83 Hz | Raspier, more upper harmonics, gear shifts every 1–1.5 s when accelerating | P |
| `moto_cruiser` | 350 cc long-stroke single (Royal Enfield Classic/Bullet, very popular in Nepal [E]) | 1-cyl, 4-stroke, long stroke | 900 / 5,500 | 7.5 → 46 Hz | The "dug-dug" thump: individually audible firing pulses at idle; strong 2nd/3rd harmonics | P |
| `scooter` | 110–125 cc CVT scooters (Dio, Activa, Ntorq) | 1-cyl, 4-stroke, CVT | 1,700 / 8,000 | 14 → 67 Hz | CVT "rubber band": RPM jumps to ~5,500 on throttle, then holds while speed rises; belt whine 600–1,200 Hz | P |
| `moto_2stroke` | Vintage 2-stroke (Yamaha RX100-type), rare | 1-cyl, 2-stroke | 1,500 / 9,500 | 25 → 158 Hz | "Ring-ding" buzz, expansion-chamber resonance, popping on overrun; use for ≤ 2% of bikes | P |
| `ebike` / `escooter` | Electric motorbikes and scooters (e.g. Nepali-made Yatri, imported hub-motor scooters) | Hub or mid motor | wheel 0–900 RPM | Whine 150–900 Hz rising with speed | Quiet sine-ish whine + 2nd harmonic + inverter hiss + tyre noise; **no fake engine** | P |
| `tempo_safa` | **Safa tempo**: battery-electric 3-wheeler, 72 V pack of 12 deep-cycle batteries, DC motor, top speed ≈ 45 km/h, ≈ 600–714 in service [S CEN, DownToEarth] | DC motor + chain/gear | motor 0–3,000 [E] | Whine 200–1,500 Hz | **Rising-falling DC-motor whine** + gear mesh + body rattles of the boxy body + clacking door bar; conductor's call and body slap at stops | P + B |
| `tempo_diesel` | Old diesel "Vikram" tempos were banned from the valley in 1999 [S Nepali Times, DownToEarth] | — | — | — | **Not spawned** in the valley (historically accurate) | — |
| `tractor` | Two-wheel and small 4-wheel tractors in fields (peri-urban) | 1-cyl diesel | 900 / 2,400 | 7.5 → 20 Hz | Slow "putt-putt", trolley rattle | P |

### 2.3 Bicycle

| Sound | Parameters [E] | Method |
|---|---|---|
| Freewheel ratchet (coasting) | Click rate = pawl engagement points × (wheel rev/s − cog rev/s). Cheap freewheels have ~18–24 engagement points; 26"/700c wheel ≈ 2.1 m circumference; at 15 km/h (4.17 m/s) wheel ≈ 2.0 rev/s → **36–48 clicks/s**. Each click: 2 ms noise burst through band-pass 3–5 kHz, Q 4 | B (click baked) + P (scheduler) |
| Chain and drivetrain | Cadence 60–90 rpm (1–1.5 Hz pedal); chain-roller rate = cadence × chainring teeth (≈ 38–44) /60 → 40–66 Hz rumble at −30 dB, plus a soft tick every half crank turn | P |
| Tyre roll | Pink noise, band 200 Hz–2 kHz, level ∝ speed^1.5; surface multipliers from the tyre table in §2.5 | P |
| Bell | Classic rotary "ding-ding" thumb bell (double strike, 70–90 ms apart). Modal: partials 1.00, 2.76, 5.40, 8.93 × f0 with f0 ≈ 2.3–2.8 kHz, T60 0.4–0.7 s | B |
| Brake | Rim-brake squeal sine 2–4 kHz with slow vibrato 6 Hz, 0.2–0.6 s, 30% of stops | B |
| Gear shift / kickstand / basket rattle | Short clicks; basket rattle when surface is BRICK/COBBLE/GRAVEL | R |

### 2.4 Cars, SUVs, microbuses

| Model id | Reference | Layout | Idle / max RPM [E] | Firing freq. | Character | Method |
|---|---|---|---|---|---|---|
| `car_3cyl` | Small hatch and taxi (Suzuki Alto 800, Maruti-type) — Kathmandu taxis are mostly small Suzukis [E] | 3-cyl 4-stroke | 800 / 6,000 | 20 → 150 Hz | Uneven 1.5-order burble, thin | P |
| `car_4cyl` | 1.2 L hatch/sedan (Swift, i10, i20) | 4-cyl 4-stroke | 750 / 6,500 | 25 → 217 Hz | Smooth hum, 2nd order dominant | P |
| `suv_diesel` | Jeep/SUV diesel (Scorpio, Bolero-type), Sumo-type jeeps | 4-cyl diesel | 750 / 4,000 | 25 → 133 Hz | Diesel clatter (injection ticks), turbo whistle 2–4 kHz at load | P |
| `car_ev` | EV cars (BYD, Tata, MG, Hyundai; EVs are now the majority of new car imports in Nepal [E, verify current share]) | Electric | — | Motor whine 300–2,000 Hz faint | Low-level futuristic hum (pedestrian warning style) below 20 km/h, then tyre/wind only | P |
| `microbus` | Toyota HiAce-type microbus (diesel) and the newer electric microbuses | 4-cyl diesel or EV | 700 / 3,800 | 23 → 127 Hz | Diesel rattle; sliding door "thunk-clack" is the signature | P + R (door) |

Car extras: door open/close (R), indicator tick 1.5 Hz (B: two 3 ms clicks, 1.2 kHz), key-start (cranking 4–6 Hz × 0.8–1.2 s then catch) (P), handbrake ratchet (R), comic gear grind (B: band-noise 1–3 kHz with 30 Hz AM, 0.3 s; the manifest asks for it on jeeps).

### 2.5 Buses, trucks and tyres

| Model id | Reference | Layout | Idle / max RPM [E] | Firing freq. | Character | Method |
|---|---|---|---|---|---|---|
| `bus_city` | Sajha Yatayat and private city buses, Tata/Ashok Leyland chassis [E] | 6-cyl diesel (some 4-cyl) | 600 / 2,600 | 30 → 130 Hz (6-cyl) | Deep growl, turbo whistle, body/window rattle 8–14 Hz AM on rough surfaces | P |
| `bus_ev` | Sajha's electric buses (since 2020s) [V] | Electric | — | Whine 200–1,200 Hz | Whine + air compressor chug + air brakes still present | P |
| `truck` | Tata/Ashok Leyland rigid trucks, tippers, water tankers | 6-cyl diesel | 600 / 2,500 | 30 → 125 Hz | Heavier, exhaust brake "blat" on long downhills (Nagdhunga) | P |
| `truck_reverse` | Reversing tipper / tanker | — | — | Reverse alarm 1.0–1.4 kHz, 0.5 s on / 0.5 s off [E]; or a **musical reverse horn** (original jingle only, no copyrighted tune) | B |

| Brake / pneumatic | Parameters [E] | Method |
|---|---|---|
| Service air-brake release "pssht" | White noise, HP 1 kHz, peak 3–6 kHz, attack 5 ms, decay 0.3–0.8 s; triggered when a bus/truck stops | B (4 variants) |
| Parking-brake "PSSHHT" | As above, +6 dB, 0.8–1.4 s, slight pitch fall | B |
| Compressor chug | 1-cyl chug at 8–12 Hz while stationary, 3–8 s after braking | P |
| Door (bus) | Pneumatic hiss + rubber thud | B + R |
| Conductor body slap | Two flat palm slaps on the bus side (the "go" signal), 150–250 ms apart; plus a generic non-verbal destination call (no real route names, street_life §15) | R + B; signal custom **[V]** (manifest flags "verify the conductor's signal") |

| Tyre loop (`ghm_sfx_tyre_*`) | Spectrum [E] | Level vs speed |
|---|---|---|
| Asphalt / concrete | Pink noise BP 300 Hz–2 kHz | ∝ v^1.5, audible from 15 km/h |
| Brick | Asphalt + periodic rumble at (speed / brick length 0.23 m) ≈ 18 Hz at 15 km/h | +3 dB |
| Cobble / stone flags | Rattle impulses at speed / 0.4 m spacing, suspension clunks | +5 dB |
| Gravel | Granular crunch 2–8 kHz, 200–800 grains/s ∝ speed | +6 dB |
| Dirt / mud | Low thud bed + squelch grains (mud) | +2 dB; mud adds suck-out when stuck |
| Wood planks | Clatter at speed / plank width 0.2–0.3 m | +4 dB |
| Metal grate (Bailey bridge) | Rattle + ringing partials 300 Hz–2 kHz | +6 dB |
| Skid | Band-noise 600–1,800 Hz with slow FM; never with impact sounds (non-violent) | — |

### 2.6 Horns and horn culture

**Facts:** the Metropolitan Traffic Police Division declared the valley a **no-horn zone from Nepali New Year, 14 April 2017**; horns are allowed in emergencies and at turning points, pressure horns are banned, fines up to Rs 500 [S Kathmandu Post 2017-04-15; Himalayan Times; Onlinekhabar]. Enforcement has been repeatedly "revived", so horns are still common but far less constant than in Indian cities [S Himalayan Times "revive campaign", Onlinekhabar "tighten"].

**Design consequence:** the manifest's "constant playful honking" (`ghm_amb_ktm_street_day`) should be **toned down to short, friendly taps at junctions, blind corners and to warn a pedestrian**, which is both accurate to the post-2017 rule and less fatiguing. Musical pressure horns are heard **only on highways outside the Ring Road** (Prithvi corridor) and rarely on old trucks.

| Horn | Real form | Synthesis [E] | Use rule |
|---|---|---|---|
| `horn_moto` | Single 12 V disc horn | Square-ish wave f0 400–450 Hz, 2nd/3rd harmonics, slight rise 2% over 40 ms attack; tap 120–250 ms | 2 taps at junctions; never held > 0.6 s near temples, schools, hospitals |
| `horn_scooter` | Smaller disc | f0 480–520 Hz, thinner | — |
| `horn_car` | Pair of disc horns low+high | Two detuned square/saw oscillators at ≈ 420 Hz and ≈ 510 Hz (≈ major third), band-pass 300 Hz–3 kHz | Tap 150–300 ms |
| `horn_bus` | Air or twin electric horn | 3 oscillators at 290, 345, 435 Hz (minor-ish triad), slow attack 30 ms | City buses: single blasts 0.4–0.8 s |
| `horn_truck_musical` | Multi-trumpet "musical" pressure horn (illegal in valley since 2017) | Sequence of 4–7 trumpet tones; **original melodies only**, each note 120–250 ms; trumpet = sawtooth through formant at 1.2 kHz | Highways only, 1 per 2–5 min, not in valley ambience |
| `horn_tempo` | Small electric beep | f0 600–700 Hz pure-ish, 2 short beeps | — |
| `bell_cycle` | §2.3 | — | — |
| No sirens | Emergency vehicles are not part of the 9+ ambient set in W2 | — | — |

Horn rate targets per area are in §7.2; the traffic AI owns *when* (`Ghumante.Traffic` "horn logic", ARCHITECTURE §7.1), audio owns *how*.

### 2.7 People, crowds and bazaar life

| Sound | Detail | Method |
|---|---|---|
| Crowd walla bed (3 densities) | Non-intelligible babble, no words in any language; built from 8–16 overlapping "syllable" grains of CC0 crowd texture, or synthesised: band-limited noise shaped by random vowel formants (F1 300–800 Hz, F2 900–2,400 Hz), 3–6 syllables/s per virtual talker, 6–40 talkers by density | R (texture) or P |
| Footstep crowd layer | Not per-NPC: a granular stream of brick footsteps at rate = nearby walkers × 1.9/s, capped at 40/s | H |
| Shop shutter (dhoka) | Rolling steel shutter: up = 1.2–2.0 s ratchet rattle (impulses 25–40/s through comb filter at 180 Hz) + final clang; heard 08:30–10:00 and 19:30–21:30 (street_life time table) | B |
| Pressure-cooker whistle | Kitchen whistle from houses, very characteristic of Nepali mornings and evenings: steam whistle 2–3 kHz with noisy onset, 1–3 s; 07:00–09:30 and 18:00–19:30, peri-urban and old core [E] | B |
| Vendor calls | Non-verbal or generic "aau aau" style calls, never real product/brand words (manifest) | B or VO |
| Scrap collector (kawadi) bicycle call | Generic sung call + bicycle bell; peri-urban mornings [E][V] | VO (original) |
| Tea stall | Glass tumbler clinks (R), kettle bubble (B) | R + B |
| Kids playing | Distant non-verbal laughter and shouts after 15:00; never in danger contexts | R |
| Wedding band (panche baja / brass band) | Marriage season (roughly Nov–Feb and spring auspicious dates [V]); original folk-idiom pieces only | Music system (original composition) |
| Loudspeaker music | Omit (copyright; quality) | — |

### 2.8 Sacred sounds (review with a cultural reviewer before ship)

| Sound | Facts and parameters | Method |
|---|---|---|
| Hand bell (ghanti, puja) | Small bronze bell with clapper, rung continuously during puja. Partials ≈ 1.00, 2.32, 4.25, 6.63 × f0 [E typical small-bell ratios], f0 1.8–3.2 kHz, clapper rate 4–8 Hz, T60 0.6–1.2 s | B |
| Shrine bell (hung, ghanta) | Hung bronze bell rung by devotees by pulling the clapper on arrival; one strike every 20–90 s at busy shrines 05:30–09:00 (street_life). Partials (church-bell-like minor-third profile, an acceptable cartoon approximation) **hum 0.5, prime 1.0, tierce 1.19, quint 1.5, nominal 2.0, upper 2.5, 2.66, 3.0, 4.0** × f0; f0 300–900 Hz; T60 hum 6–10 s, nominal 3–5 s, upper 0.5–1.5 s; beating 0.5–2 Hz between split partials (doublets 0.3% apart) | B (6 sizes × 3 strikes) |
| Great bells | **Taleju bell, Kathmandu Durbar Square** (erected by Rana Bahadur Shah, 1797 [S Wikipedia KDS]); **Taleju bell, Patan Durbar Square** (1736, Vishnu Malla [S Wikipedia PDS]); **Taleju bell, Bhaktapur Durbar Square** (1737, Ranajit Malla) and the small "bell of barking dogs" beside it — local lore says dogs bark when it rings [S Wikipedia BDS][V]. f0 ≈ 150–300 Hz, T60 12–20 s [E] | B; ringing schedule [V]; easter egg: nearby dog barks 1–3 s after the Bhaktapur small bell (only if reviewer agrees) |
| Conch (shankha) | Blown at dawn/dusk worship and at aarti. Breathy tone f0 ≈ 250–450 Hz [E] with strong odd harmonics, 2–4 s, pitch drops 5–10% at the end; formant 800–1,200 Hz; synthesised as a lightly-damped resonator excited by noise + pulse train | B |
| Cymbals (jhyali / tah) | Small hand cymbals in bhajan, 1–2 strikes/beat at 80–110 bpm; strike = noise through 6–10 resonant band-passes 3–9 kHz, T60 0.3–1.0 s | B |
| Bhajan (instrumental only) | Evening devotional singing groups at pati and temple porches (e.g. around Hanuman Dhoka, Pashupati) — **voices are not recorded or synthesised**; represented by harmonium drone (sawtooth pair, slow bellows AM 0.3–0.6 Hz), madal/dholak rhythm and jhyali, from the music system (original). 18:00–20:30 | P (music stems) |
| Evening aarti at Pashupati ghats | Daily Bagmati aarti with bells, conch and lamps (`ghm_amb_ghats_aarti`, P2); devotional singing needs permission (manifest) | B + music |
| Prayer wheels (mani) | Boudha and Swayambhu koras: creak of the axle (friction squeak 400–900 Hz, 0.2–0.4 s) and on big wheels a **bell struck once per revolution** by a striker | B |
| Monastery horns (dungchen, gyaling), chanting | Only as distant low suggestion near gompas at puja times; chants **not used** without permission (manifest §13). Dungchen: very low brass drone f0 ≈ 60–110 Hz [E] with rich harmonics | B, **[V]** |
| Pigeon flock at temples | See §2.9; Basantapur, Patan, Boudha | B + R |
| Drums (dhime, nagara) | Festival only (Indra Jatra, Bisket); music system | music |
| Mosque call to prayer, church bells | **Not included** in W2 (sacred, needs community consultation) **[V]** | — |

Player interaction: the player may ring a shrine bell once (cooldown 3 s, max 3 per visit), never spam; ringing inside a compound adds +1 "respect" and no score for volume. No sound is ever played as comedy on a sacred object (no "boing" accents inside compounds).

### 2.9 Birds and animals

Species and densities come from street_life §7–8 (top urban birds: rock dove, house crow, house sparrow, barn swallow, common myna; then black kite, cattle egret, red-vented bulbul, jungle myna [S there]).

| Animal | Call model [E] | Rates and times | Method |
|---|---|---|---|
| House crow | Harsh "kaa": noise + pulse train 25–40 Hz glottal rate, formants 700 Hz and 1.5 kHz, 0.25–0.45 s, often 2–4 in a row | 2–6/min urban; dawn and dusk roost choruses near Ratna Park, Tundikhel | B (12 variants) |
| Rock dove / pigeon | Coo: sine 300–550 Hz [E] with throat AM 20–30 Hz, rising–falling, 0.5–1.2 s; **flock take-off**: 40–200 wing-clap impulses over 0.6–1.5 s (each a 10 ms LF thud + 2–4 kHz flutter) | Squares, rooftops; take-off when player runs within 3 m | B |
| Common / jungle myna | Varied whistles and chatter: FM chirps 1–4 kHz, 50–200 ms, 4–10 notes per phrase | Rooftops, wires, fields | B (procedural phrase generator) |
| House sparrow | "Chirp" 3–6 kHz, 60–120 ms, rapid series | Eaves, shops | B |
| Black kite | Whinnying descending whistle: tone 4.5 → 2.5 kHz [E] with 8–12 Hz trill, 1–2 s | Soaring over all areas, 0–1/min | B |
| Asian koel | Rising "ko-el" repeated 5–10×, each note higher, f0 ≈ 1–2 kHz [E]; spring and monsoon (Mar–Aug) [E] | Gardens, trees; famous dawn caller | B |
| Indian cuckoo | Four-note call, culturally linked to "kafal pakyo" in Nepal [V] | Spring (Apr–Jun), wooded edges | B |
| Red-vented bulbul | Cheerful 3–5 note whistles 1.5–3.5 kHz | Gardens | B |
| Cattle egret | Low croak 200–600 Hz, rare | Fields Jun–Jul | B |
| Roaming dog | Bark: pulse at 200–400 Hz f0 [E] through formants 500–900 Hz, 1.5–2.5 kHz, 0.15–0.3 s; **night bark chains**: one dog triggers 1–4 others at 1–5 s delays across 50–300 m (Kathmandu nights are known for this) | Day: mostly asleep (street_life §15); night 22:00–04:00: 1–4 chain events/min | B (16 variants) |
| Cow (street cattle) | Moo: 120–200 Hz saw, formant glide, 1–2 s; bell clank when walking | Ring Road, Kalimati etc. | B |
| Rhesus macaque | Chatter, threat-free "coo" and squeals | Swayambhu, Pashupati | R (CC0) or B |
| Rooster | Crow 4 syllables, 1.5–2.5 s, f0 500–700 Hz with harmonics | Peri-urban 04:30–07:00 | B |
| Goat, buffalo, chicken | Bleat (AM 6–8 Hz vibrato), low (100–160 Hz), clucks | Peri-urban, fields | B/R |
| Insects (crickets, cicadas) | Crickets: 4–5 kHz carrier, chirp rate 1–3 Hz (temperature-dependent); cicadas: 4–7 kHz buzz with 50–150 Hz AM, summer | Night all areas (crickets), forests in summer (cicadas) | P |
| Frogs | Monsoon nights in fields: croaks 400–1,500 Hz pulsed 10–30 Hz | Jun–Sep, fields, ponds | B |

### 2.10 Weather, water and environment

| Sound | Model [E] | Method |
|---|---|---|
| Wind (open) | Pink noise → band-pass (centre 300–800 Hz, Q 0.7) with gust LFO: sum of 3 sines 0.03, 0.11, 0.27 Hz + random gust events every 8–30 s; level by wind speed (weather system) and elevation (higher on Nagarkot, Chandragiri, Phulchoki) | P |
| Wind in leaves | Wind gust envelope drives a high band (2–8 kHz) rustle; forest and tree-dense cells | P |
| Wind whistle (wires, railings) | Aeolian tone f = 0.2 × v / d (Strouhal) — for a 6 mm cable at 10 m/s ≈ 330 Hz | P, near cable car and power lines only |
| Rain (ground) | Poisson drop impulses: light rain 400–1,500 drops/s/listener-area, heavy 3,000–8,000 [E]; each a 1–4 ms noise burst; + filtered noise bed | P |
| **Rain on tin roofs** (iconic, manifest) | Drops on corrugated galvanised iron: per-drop click 2–6 kHz + a short metallic ring (2–3 modal partials 900 Hz–3 kHz, T60 60–150 ms); density from the fraction of tin-roof buildings within 30 m (building archetype + roof material) | P |
| Gutter and spout overflow | Band-noise 500 Hz–3 kHz + bubble chirps; monsoon | P |
| Thunder (distant) | Low-passed noise burst 40–300 Hz, 3–8 s, 2–5 rumble peaks; delay after lightning = distance/343 s | B (4 variants) |
| Stone water spout (dhunge dhara / hiti) | Continuous splash + bubble chirps; Sundhara, Manga Hiti (Patan) etc. | P or R |
| Rivers | `ghm_amb_water_*` (manifest P0): Bagmati, Bishnumati; lower-flow pink-noise bed in dry season, louder with chirps in monsoon | R (CC0) + P |
| Distant city hum | Valley-scale bed: brown noise 40–400 Hz + faint distant horn taps 1–3/min, level by distance to Ring Road and urban fraction; audible from hills (Swayambhu, Chandragiri) at night | P |
| Fireworks/firecrackers | Tihar only, soft and distant (manifest "soft firecrackers"); 9+ fine at low level | B |

### 2.11 Aircraft and the airport (TIA)

**Facts:** Tribhuvan International Airport (VNKT), single runway **02/20, 3,350 m** (extended 300 m south in 2020, length then undeclared), elevation ≈ 1,338 m; **no ILS**, VOR/DME non-precision approaches (VOR/DME at Kathmandu and Bhattedanda along the **extended approach path of runway 02**), PAPI 3°, RNP AR since 2012; helipad holds up to 17 helicopters [S Wikipedia, ICAO APANPIRG IP8]. Traffic: **12,525 movements in November 2024, ≈ 417 flights/day** [S Onlinekhabar]; the airport operates **18 hours a day** since 1 April 2025 (06:00–24:00 is the expected window **[V]**) [S Ekantipur]. Domestic fleet is dominated by ATR 72 (Buddha Air, Yeti) [S Wikipedia]; international narrow-bodies are mostly A320/737 family [E].

Game schedule [E]: one movement every **2–4 minutes** between 06:00 and 24:00 (417/18 h ≈ 1 every 2.6 min), mix ≈ 55% turboprop (ATR 72), 30% jet (A320/737), 15% helicopter (AS350-type); arrivals mostly from the **south-southwest onto runway 02** (aligned with the Bhattedanda VOR), departures on the reciprocal or 02 depending on wind **[V: the runway-in-use split]**.

| Aircraft | Source physics [E unless noted] | Synthesis | Audible to |
|---|---|---|---|
| ATR 72-500/-600 (turboprop) | PW127 engines; 6-blade propellers (568F); prop speed ≈ 1,200 rpm at take-off, ≈ 980 rpm in cruise/approach → **blade-pass frequency 120 Hz (T/O), ≈ 98 Hz (approach)**; turbine whine 3–6 kHz; taxi "beta" growl | Harmonic stack at BPF × (1…10) with amplitudes 1, 0.7, 0.5, 0.35, …; + turbine tone with 0.1% jitter; + broadband jet noise LP 1 kHz; on landing: reverse/beta roar +6 dB for 6–10 s | 5–6 km |
| A320 (jet) | CFM56-5B: fan 36 blades, N1 100% ≈ 5,000 rpm → **fan BPF ≈ 3.0 kHz at T/O, ≈ 1.5–1.8 kHz on approach (N1 50–60%)**; "buzz-saw" multiple pure tones at shaft-order multiples (≈ 83 Hz × n) at take-off when fan tips are supersonic; A320ceo approach "whine" around 500–600 Hz from wing-underside fuel-vent cavities (widely reported, fixed by vortex generators [V]) | Take-off: broadband jet roar (peak 150–500 Hz at distance) + buzz-saw comb + fan tone; approach: fan tone + whine + airframe noise | 8–9 km |
| AS350 B3-type helicopter (common in Nepal for mountain flights and rescue [E]) | Main rotor 3 blades ≈ 390 rpm → **19.5 Hz blade pass** (felt as "thwop"); tail rotor 2 blades ≈ 2,050 rpm → ≈ 68 Hz; Arriel turbine whine 4–8 kHz | Blade-pass as amplitude-modulated noise (AM depth 0.6–0.9) + harmonics 39, 58, 78 Hz for phone speakers; blade-slap impulse on descent | 3–4 km |
| Ground ops | APU hum 400 Hz+harmonics, tug, baggage carts, unintelligible tower/radio chatter (manifest: "radio chatter (unintelligible)") | B | 300 m |

Flight path rule: aircraft fly visible, scripted 3° glideslopes onto the real runway line (OSM w340948564, CONTENT_COVERAGE S11). Height on approach h = (distance to threshold) × tan 3° ≈ 52 m per km. A turboprop 4 km out is at ≈ 210 m AGL, which sets attenuation and Doppler realistically.

### 2.12 Player-character foley and UI

Covered by ASSET_MANIFEST §13.5 (`ghm_sfx_foley_*`, `ghm_sfx_ui_*`); cartoon accents (squash "boing", jump whoosh) are B: short sine sweeps 300→900 Hz in 80 ms with soft saturation. Not repeated here.

---

## 3. Method per sound: decision table

| Group | Recommended method | Why | Fallback |
|---|---|---|---|
| Footsteps (10 sets) | **R** CC0 recordings | Foley realism is hard to synthesise convincingly; small (§8) | Granular footstep synth for gravel/grass |
| Cloth, doors, glass clinks, shutters' clang | R | Same | — |
| Engines (all, NPC and player) | **P** additive (§4.2) | 0 MB, infinite RPM continuity, every vehicle distinct by seed | Manifest's 4 RPM-layer ADPCM loops crossfaded (needs CC0 recordings; few exist for South-Asian bikes) |
| Tyres | P noise models + surface parameters | Continuous with speed and surface | R short loops |
| Horns, bells, conch, cymbals, birds, dogs, air brakes, beeps | **B** baked at load | Cheap playback, deterministic variants, no download | P live if memory is tight |
| Wind, rain, rain-on-tin, city hum, insects | P | Driven by weather and density parameters | B 30 s loops |
| Crowd walla | H (CC0 texture grains + procedural density) or R beds | Synth babble can sound uncanny; texture grains are safer | P formant babble |
| Aircraft, helicopter | P | Physics-driven, long Doppler flybys | B layers |
| Water (rivers, spouts) | R beds + P chirps | Water synthesis is OK but beds are cheap | P |
| Music | Composer stems (manifest §13.2) | — | — |

---

## 4. Procedural synthesis spec (C#, Unity 6.3)

### 4.1 Architecture

```
Ghumante.Audio
├── Synth/                     pure C#, Burst-compatible structs, no UnityEngine in the hot path
│   ├── Osc.cs                 phase accumulators (double phase), PolyBLEP saw/square, sine via table (2048, linear interp)
│   ├── Noise.cs               xorshift32 white, Paul Kellet pink, brown (leaky integrator)
│   ├── Biquad.cs              RBJ cookbook LP/HP/BP/peak; per-sample coefficients smoothed (one-pole, 5 ms)
│   ├── Modal.cs               bank of decaying sinusoids (bells, clicks, metal)
│   ├── Grain.cs               Hann-windowed grain player over a mono float[] (granular engines/crowds)
│   ├── EngineVoice.cs         §4.2
│   ├── ProceduralBank.cs      bakes one-shots into AudioClips at region load (§4.4)
│   └── Presets/*.asset        ScriptableObjects with the parameter tables below
├── Runtime/
│   ├── GeneratorVoice.cs      IAudioGenerator implementation (6.3 scriptable audio pipeline)
│   ├── FilterReadVoice.cs     OnAudioFilterRead fallback
│   ├── VoiceManager.cs        pool, priorities, virtualisation (§6.1)
│   ├── AmbienceDirector.cs    zones, time, season, weather (§7)
│   └── DopplerSolver.cs       custom Doppler (§6.3)
```

**Two runtime paths for real-time voices:**

1. **Preferred: `IAudioGenerator` (Unity 6.3 scriptable audio pipeline).** A generator is "a custom sound source that produces audio and injects it directly into a scene through an AudioSource"; the factory `IAudioGenerator.CreateInstance()` returns a `GeneratorInstance`; it may be a ScriptableObject asset or a MonoBehaviour assigned to the AudioSource's Generator field; `Process` must be real-time safe (no allocations, locks, blocking I/O, no UnityEngine API), Burst-compiled struct code is recommended, and main-thread → audio-thread control goes through `IControl.OnMessage()` pipes [S Unity manual "Scriptable processors / generators"]. Not supported on Web (irrelevant here). Runtime-created AudioClips are **not** usable as generator input [S], which does not matter for pure synthesis. **[V] Verify on device that generator output is spatialised by the AudioSource (3D pan + rolloff)**; the docs page does not say.
2. **Fallback: `MonoBehaviour.OnAudioFilterRead(float[] data, int channels)`** runs on the audio thread, data is interleaved, sample rate from `AudioSettings.outputSampleRate`; with no clip on the AudioSource the filter itself is the source [S Unity scripting reference]. Known caveat **[V]**: with no clip, 3D attenuation/panning may not be applied as expected; the common workaround is a looping 1-second constant-1.0 dummy clip created with `AudioClip.Create` and **multiplying** the synthesis into `data` (the clip carries spatialisation). Avoid managed allocation inside the callback; IL2CPP compiles it to native code but it is not Burst, so budget ~2–3× the generator cost.

**Threading contract:** the main thread writes target parameters (rpm, load, speed, surface, gain, pitchDoppler) into a 64-byte struct per voice; the audio thread reads it once per buffer and **ramps every parameter linearly across the buffer** (no zipper noise). No locks: single-writer/single-reader with a sequence counter, or the generator's message pipe.

### 4.2 Engine model (additive firing-order synthesis)

Per sample:

```
f_fire  = rpm / 60 * cylinders / (fourStroke ? 2 : 1)       // firing frequency
f_crank = rpm / 60                                           // order 1 (for 3-cyl 1.5-order and 1-cyl unevenness)
phase  += f_fire / sampleRate   (+ per-cycle jitter: each new cycle multiplies period by 1 + N(0, jitterPeriod))
pulse   = sum_{k=1..H} A_k(load) * sin(2π k phase + φ_k)     // H = 8–16 harmonics, band-limited: drop k*f_fire > 0.45*fs
pulse  *= 1 + jitterAmp * cycleRandom                        // cycle-to-cycle amplitude variation
exhaust = Biquad.BP(pulse, f_pipe, Q_pipe) * mixPipe + pulse * (1-mixPipe)
noise   = pink * (noiseBase + noiseLoad*load) * (0.5 + 0.5*cos(2π phase))   // combustion noise gated by cycle
mech    = for diesels: tick impulses at f_fire through BP 2–4 kHz (injection clatter)
intake  = BP(white, 800–1,500 Hz) * load * rpmNorm
out     = SoftClip(drive(load) * (exhaust + noise + mech + intake))   // tanh, drive 1.0–2.5
// harmonic tilt: A_k(load) = A_k * (k ^ (-tilt0 + tiltLoad*load))  → on-load is brighter
// off-load (decel): tilt steeper, + random "pops" (2-stroke, bikes) 0–6 per second
```

`f_pipe` (exhaust quarter-wave) = c / (4 L) with hot-gas c ≈ 500 m/s [E]: bike L ≈ 1.2 m → ≈ 105 Hz; car L ≈ 2.5–3 m → ≈ 45 Hz; bus ≈ 4 m → ≈ 30 Hz. Small phones will not reproduce these, so the band-pass is mostly about tone colour in the 2nd–8th harmonics.

**Preset table [E] (tune by ear; values are starting points):**

| Preset | Cyl | Stroke | Idle / red RPM | H | A_1..A_8 (relative) | Tilt (off / on) | Jitter period / amp | Noise base / load | Pipe f / Q | Drive | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|
| moto_commuter | 1 | 4 | 1,400 / 8,500 | 12 | 1, .8, .6, .5, .35, .3, .2, .15 | 1.4 / 0.8 | 2% / 8% | .10 / .25 | 105 / 2 | 1.6 | 5 gears |
| moto_sport | 1 | 4 | 1,400 / 10,000 | 14 | 1, .9, .7, .6, .5, .4, .3, .25 | 1.2 / 0.6 | 1.5% / 6% | .10 / .30 | 120 / 2.5 | 2.0 | 5–6 gears |
| moto_cruiser | 1 | 4 | 900 / 5,500 | 16 | 1, 1.1, .9, .6, .45, .3, .2, .1 | 1.6 / 1.0 | 4% / 15% | .08 / .20 | 80 / 3 | 2.2 | the thump: gate each cycle with a 30% duty envelope |
| scooter | 1 | 4 | 1,700 / 8,000 | 10 | 1, .7, .5, .3, .2, .15, .1, .05 | 1.6 / 1.0 | 1% / 4% | .12 / .20 | 140 / 1.5 | 1.3 | CVT map below; belt whine 600–1,200 Hz at −24 dB |
| moto_2stroke | 1 | 2 | 1,500 / 9,500 | 16 | 1, .9, .8, .7, .6, .55, .5, .45 | 0.9 / 0.4 | 2% / 10% | .20 / .40 | 160 / 6 (expansion chamber "ring") | 2.5 | overrun pops 2–6/s |
| car_3cyl | 3 | 4 | 800 / 6,000 | 12 | 1, .6, .5, .3, .25, .2, .1, .1 | 1.3 / 0.8 | 1% / 5% (+0.5-order AM 15%) | .06 / .15 | 45 / 1.5 | 1.3 | 5-speed manual |
| car_4cyl | 4 | 4 | 750 / 6,500 | 10 | 1, .5, .35, .2, .15, .1, .05, .05 | 1.5 / 1.0 | 0.5% / 3% | .05 / .12 | 50 / 1.2 | 1.2 | — |
| suv_diesel | 4 | 4 | 750 / 4,000 | 12 | 1, .7, .5, .4, .3, .25, .2, .1 | 1.2 / 0.8 | 1% / 5% | .08 / .20 | 45 / 1.5 | 1.6 | injection ticks −12 dB; turbo 2–4 kHz on load |
| microbus | 4 | 4 | 700 / 3,800 | 12 | as suv_diesel | 1.2 / 0.8 | 1% / 6% | .10 / .20 | 40 / 1.5 | 1.6 | panel rattle AM 8–14 Hz on rough surfaces |
| bus_city / truck | 6 | 4 | 600 / 2,600 | 16 | 1, .9, .7, .6, .5, .4, .3, .25 | 1.0 / 0.6 | 0.8% / 4% | .08 / .25 | 30 / 2 | 2.0 | turbo whistle 2–5 kHz ∝ load×rpm; exhaust brake = load −1 → bright "blat" |
| tractor | 1 | 4 diesel | 900 / 2,400 | 14 | 1, 1, .8, .7, .5, .4, .3, .2 | 1.0 / 0.7 | 3% / 12% | .10 / .20 | 60 / 4 | 2.0 | putt-putt |

**Electric motors (ebike, tempo_safa, car_ev, bus_ev):** tone f_e = motorRPM/60 × polePairs (+ gear-mesh tone = shaftRPM/60 × teeth); render sine + 0.3 × 2nd + 0.15 × 3rd harmonic, level −20 to −14 dB relative to petrol presets, + inverter hiss (HP 6 kHz, very low) + tyre noise. Safa tempo: map speed 0–45 km/h to whine 200→1,500 Hz with a slight lag (one-pole, 300 ms) and add chain/gear mesh at 0.3–0.6 × that frequency; body rattles from the brick/cobble tyre table. Pole pairs [E]: hub motor 20–23, DC brushed motor equivalent 2.

**RPM from speed:** rpm = idle + (v / C_wheel) × 60 × gearRatio × finalDrive, clamped to red; shift up at 85–90% red under throttle, down at 40% when braking; shift event: 120–250 ms RPM drop with load cut (cartoon-snappy). CVT scooter: rpm_target = lerp(idle, 5,800, throttle) until v > 0.6 × vmax, then rises to 7,500.

**Cost [E]:** one 16-harmonic voice at 48 kHz with 3 biquads and noise ≈ 48k × ~110 flops ≈ 5.3 Mflop/s; Burst with SIMD on a Cortex-A55 low-tier core ≈ 0.4–0.7% of one core. Budget §6.1 caps simultaneous synth voices so total audio-thread synthesis stays under **15% of one little core** on Low **[V device]**.

**Granular alternative (if a CC0 engine ramp recording exists):** slice a 10–15 s idle→red ramp into grains of exactly 2 firing cycles (detected by onset), index by rpm, overlap-add with Hann windows, 50% overlap, grain length 20–60 ms; pitch-shift ±3% to fill gaps. Only for the player's vehicle; NPCs stay additive.

### 4.3 Recipes for the other procedural sounds

| Sound | Recipe [E] |
|---|---|
| Bells (modal) | `y = Σ a_i · e^(−t/τ_i) · sin(2π f0 r_i t + φ_i)`; strike = 3 ms noise burst into the bank; 6–9 partials; doublets: each partial split into two at ±0.15% for beating; randomise f0 ±1.5% and amplitudes ±2 dB per variant. Alternatively 2-operator FM bell: carrier f0, modulator 1.4 × f0, index decaying 10 → 0.5 over T60 (cheaper, less accurate) |
| Horns | 2–3 oscillators (PolyBLEP square + saw 30/70), detune 0.3–1%, attack 15–40 ms, release 30–60 ms, BP 300 Hz–3 kHz, soft clip; random 2% pitch sag on long blasts (battery droop) |
| Conch | Noise + pulse train excite a resonator (f0, Q 30) and formant (1 kHz, Q 3); breath noise HP 2 kHz −18 dB; vibrato 4–5 Hz ±0.5% |
| Crow / dog / cow / rooster | Source-filter: glottal pulse train (f0 contour from preset), 2–3 formant band-passes, noise aspiration; per-call contour as 4–6 breakpoints |
| Small birds | Sine with FM sweeps (start f, end f, curvature), AM envelope; phrase generator picks 3–10 notes from a species palette |
| Wind | Pink noise → 2 band-passes (body 300–800 Hz, whistle 1–3 kHz Q 8) with independent gust LFOs |
| Rain | Poisson impulses (rate λ from mm/h) through 3 random band-pass "drop" filters; on tin: add 2–3 modal partials per drop; bed = filtered pink |
| Shutter | Impulse train 25–40/s, each into a comb filter (delay 5.5 ms ≈ 180 Hz) + modal clang at the end |
| Air brake | White noise, HP 1 kHz, peaking 4 kHz +6 dB, exp decay τ 0.15–0.4 s |
| Aircraft | Harmonic stacks at blade-pass/fan frequencies + broadband jet noise (pink → LP); ground-reflection comb: second path delayed by Δ = 2h·sinθ/c with gain 0.7 (gives the realistic phasing sweep on flybys) |
| Helicopter | Noise AM at 19.5 Hz (depth 0.7), plus sine harmonics 39/58/78 Hz, tail rotor tone 68 Hz ±, turbine 4–8 kHz |
| Crickets | 4.5 kHz sine, pulse trains (3–5 pulses of 15 ms per chirp), chirp rate ∝ temperature (Dolbear-like: ~1–3/s) |

### 4.4 Bake-on-load bank (`ProceduralBank`)

At region load, a worker thread renders one-shots into `float[]` (mono, 32 kHz), then the main thread calls `AudioClip.Create(name, n, 1, 32000, false)` + `SetData` in slices of ≤ 2 ms per frame (fits the 2 ms main-thread upload cap of ARCHITECTURE §7.2). Seeds come from `SEED` so variants are identical on every device.

| Bank | Variants | Avg length | PCM size (16-bit equivalent; Unity stores float→PCM16 for created clips [E]) |
|---|---|---|---|
| Shrine/hand bells (6 sizes × 3) | 18 | 3.0 s | 3.5 MB at 32 kHz → **use 22.05 kHz for small bells: 2.4 MB** |
| Great bells (3 landmarks) | 3 | 12 s | 2.3 MB at 32 kHz — **stream these as real-time Modal voices instead (0 MB)** |
| Horns (6 types × 4) | 24 | 0.4 s | 0.6 MB |
| Birds (8 species × 8) | 64 | 0.6 s | 2.5 MB at 32 kHz → 1.7 MB at 22.05 kHz |
| Dogs, crows, cow, rooster, goat | 48 | 0.5 s | 1.5 MB |
| Air brakes, door hiss, shutters, pressure cooker | 20 | 1.0 s | 1.3 MB |
| Conch, cymbals, prayer-wheel creak+ding | 16 | 1.5 s | 1.5 MB |
| **Total** | ≈ 210 | — | **≈ 9 MB PCM16 at mixed rates; ≈ 6 MB after the two downgrades above** |

Memory option: on Low, bake at 22.05 kHz and half the variants (≈ 3 MB).

---

## 5. CC0 / public-domain recording sources (licence-checked)

| Source | Licence (verified 2026-10-05) | Use for | How to filter / notes |
|---|---|---|---|
| **Freesound** — https://freesound.org | Per-file: CC0, CC-BY, CC-BY-NC (and legacy Sampling+). **Only files whose page says "Creative Commons 0" ship.** | Footsteps per surface, cloth, doors, glass clinks, crowd textures, rain, rivers, macaques | Search with the licence facet `Creative Commons 0` (URL form `https://freesound.org/search/?q=<terms>&f=license:%22Creative+Commons+0%22` [V URL syntax]). Record sound id, author, URL, licence text and download date in LICENSES.md; save the page as PDF, because uploaders can change the licence of future versions (licence applies to the copy you downloaded). Avoid recordings of identifiable speech |
| **Kenney** — https://kenney.nl/assets/category:Audio | **CC0** stated per pack (e.g. *Impact Sounds*, 130 files: "Creative Commons CC0") [S] | Impacts, footsteps (concrete/wood/grass/snow/carpet are in Impact Sounds [V contents]), UI clicks (*UI Audio*, *Interface Sounds*), RPG foley | Check each pack page shows CC0 before download; keep the bundled License.txt |
| **BigSoundBank** (Joseph Sardin) — https://bigsoundbank.com | **CC0 1.0 Universal**: "Use, including for commercial purposes. Without any restrictions"; credit appreciated, not required [S] | Field recordings: vehicles, rain, wind, bells (not Nepali), footsteps, crowds | Large, well-labelled French library; good for tyre/door/rain textures |
| **OpenGameArt** — https://opengameart.org | Mixed: CC0, CC-BY, CC-BY-SA, GPL, OGA-BY [S FAQ]. **Only items whose licence field is exactly CC0.** Previews are "all rights reserved" [S FAQ] | Footstep packs, UI | Use advanced search with licence = CC0 and type = Sound Effect; multi-licensed items: choose CC0 only if listed |
| **Wikimedia Commons** — https://commons.wikimedia.org/wiki/Category:Audio_files | Per-file; select **CC0 or Public domain** only | Bird calls (some PD/CC0 xeno-canto mirrors are **not** CC0 — check), historical | Check the file page licence template |
| **US federal works (public domain)**: NASA audio, NPS Sound Gallery (https://www.nps.gov/subjects/sound/gallery.htm) | PD in the US for federal-employee works; **some NPS items are contributed and not PD** [V each file] | Wind, thunder, generic birds, aircraft flyovers (non-Nepali) | Check credit line per item |
| Own field recording (future) | Studio-owned; CC0 not required for own work | Real Kathmandu ambience, bells, bus conductors | Needs a recordist trip; **sacred sounds only with permission** (manifest) |

**Excluded (fails the CC0/PD rule):**

| Source | Why |
|---|---|
| Sonniss #GameAudioGDC bundles | Proprietary EULA, *not* CC0: royalty-free and attribution-free for projects, but no redistribution as sound effects, no AI training, no resale [S sonniss.com/gdc-bundle-license]. Generous, but not CC0 → excluded by project rule |
| Pixabay sound effects | Pixabay Content License, not CC0 (since 2019) [V] |
| Mixkit, Zapsplat, Soundsnap, Epidemic, YouTube Audio Library | Own licences / attribution / subscription |
| Freesound CC-BY, CC-BY-NC, Sampling+ | Attribution or non-commercial |
| xeno-canto bird recordings | Mostly CC-BY-NC-SA/ND → excluded (great **reference** for birds' calls; use them to tune the synth by ear only) |
| BBC Sound Effects (RemArc) | Non-commercial only |
| YouTube recordings of Kathmandu | Reference only |

Procedural synthesis has no licence issue; record the generator version and seed in LICENSES.md as "procedural, original".

---

## 6. Mixing for mobile

### 6.1 Voices, priorities, virtualisation

Unity's `AudioConfiguration` exposes `numRealVoices` and `numVirtualVoices` (set via Project Settings → Audio or `AudioSettings.Reset`). Unity defaults are commonly 32 real / 512 virtual **[V in AudioManager.asset]**; virtual voices must exceed active voices to avoid warnings [S Unity Audio Manager manual].

| Tier | Real voices | Of which real-time synth (generator) | Virtual | Max NPC engines audible | Max NPC footsteps | Max one-shots/s |
|---|---|---|---|---|---|---|
| Low | 24 | 4 (player engine + 3 nearest NPC engines; wind and rain baked loops) | 128 | 3 synth + 4 as baked "pass-by" one-shots | 2 | 12 |
| Mid | 32 | 8 | 256 | 6 | 4 | 20 |
| High | 48 | 12 | 512 | 10 | 6 | 30 |

Unity AudioSource **priority** is 0 (highest) … 256 (lowest). Assignments:

| Class | Priority | Steal rule |
|---|---|---|
| UI, stings, player vehicle, player footsteps | 0–16 | Never stolen |
| Great bells, conch, aarti when in that zone | 32 | — |
| Ambience beds (2) | 40 | Never stolen; crossfaded |
| Horns within 30 m, air brakes within 20 m, aircraft | 64 | — |
| NPC engines (nearest first) | 96 + 4 × rank | Steal farthest/quietest |
| Animals, birds, shrine bells | 128 | Steal oldest |
| NPC footsteps, shutters, pressure cookers | 192 | Steal first |

Rule: a one-shot whose estimated loudness at the listener is < −45 dBFS is not started (cull before play). Same-sound concurrency caps: horn ≤ 3, bell ≤ 3, crow ≤ 2, dog bark ≤ 2, footsteps (NPC) as table.

### 6.2 Distance attenuation (custom curves)

Unity's Logarithmic rolloff behaves like 1/r beyond Min Distance and **stops attenuating at Max Distance** rather than reaching zero [S Unity manual: Max Distance is "the distance where the sound stops attenuating"], so far voices never die and keep consuming voices. Use **Custom rolloff**: 1/r from min to 0.7 × max, then a smooth fade to 0 at max.

| Class | Min (m) | Max (m) | Spatial blend | Spread | Air-absorption LPF | Notes |
|---|---|---|---|---|---|---|
| Player footsteps | 1 | 15 | 0.6 | 60° | no | partly 2D so they stay present in third person |
| NPC footsteps | 1 | 12 | 1 | 0 | no | |
| Bicycle | 1.5 | 25 | 1 | 0 | no | |
| Motorbike / scooter / tempo | 3 | 60 | 1 | 30° | no | |
| Car / microbus | 4 | 80 | 1 | 45° | no | |
| Bus / truck | 6 | 150 | 1 | 60° | yes > 80 m | |
| Horns | 5 | 200 (moto 120) | 1 | 0 | yes > 80 m | |
| Shrine bell | 2 | 40 | 1 | 0 | no | |
| Great bell | 10 | 400 | 1 | 90° | yes | |
| Conch | 5 | 150 | 1 | 0 | yes | |
| Crow / myna | 3 | 80 | 1 | 0 | no | |
| Black kite whistle | 5 | 200 | 1 | 0 | yes | |
| Dog bark | 4 | 250 | 1 | 0 | yes | night bark chains rely on long range |
| Helicopter | 30 | 4,000 | 1 | 120° | yes | |
| ATR 72 | 50 | 6,000 | 1 | 120° | yes | |
| A320 | 80 | 9,000 | 1 | 120° | yes | |
| Ambience beds | — | — | 0 (2D) | — | — | zone-weighted |

**Air absorption** (approximating ISO 9613-1 attenuation at ~20 °C, 50–70% RH [E]: ≈ 1 dB/km at 250 Hz, ≈ 5 dB/km at 1 kHz, ≈ 10 dB/km at 2 kHz, ≈ 25–30 dB/km at 4 kHz, ≈ 80+ dB/km at 8 kHz). As a one-pole low-pass cutoff by distance:

| Distance | 50 m | 200 m | 500 m | 1 km | 2 km | 4 km | 8 km |
|---|---|---|---|---|---|---|---|
| LPF cutoff [E] | 20 kHz (off) | 11 kHz | 7 kHz | 4.5 kHz | 3 kHz | 1.8 kHz | 1.1 kHz |

Apply it inside synth voices (a per-voice one-pole costs ~3 flops/sample) or with `AudioLowPassFilter` only on aircraft and great bells (Unity's per-source DSP filters add cost; keep ≤ 4 instances).

### 6.3 Doppler (custom)

Unity Doppler (`AudioSource.dopplerLevel`, global Doppler Factor 0–1 [S]) is derived from position changes. **Floating-origin rebases (ADR-003) teleport every transform**, which Unity's Doppler can read as a huge velocity → pitch spike. Rules:

* Set `dopplerLevel = 0` on every AudioSource.
* `DopplerSolver` computes per voice `pitch = (c + v_listener·n) / (c − v_source·n)` with c = 343 m/s, using **world-space velocities from the simulation** (vehicle controller, traffic agent, aircraft path), clamped to 0.7–1.4, smoothed over 50 ms. Pass it to synth voices as a frequency multiplier (exact), and to clip voices as `AudioSource.pitch`.
* Strength per class: vehicles 1.0 (cartoon may exaggerate to 1.3), aircraft 1.0, horns 1.0, animals 0, footsteps 0, ambience 0.
* Rebase frame: audio needs no special handling with this scheme because velocities come from simulation, not transform deltas.

### 6.4 Listener placement (portrait and landscape)

`AudioListener` on a child of the camera rig placed **35% of the way from the camera to the player's head** [E]: keeps the player's own sounds near-centre and stable when the camera orbits or when the device rotates between portrait and landscape (ADR-017). Rotation of the device changes nothing in audio (stereo stays left/right relative to the camera view).

### 6.5 Occlusion and acoustic spaces (cheap approximations)

No per-frame, per-voice raycasting on mobile.

| Technique | Rule [E] | Cost |
|---|---|---|
| Round-robin occlusion ray | For the 8 most important 3D voices, one raycast every 250 ms each against the **LOD1 merged building collider layer** (or the tile's footprint grid). Hit → target −6 dB and LPF 1.5 kHz, ramped over 200 ms | ≤ 8 raycasts per 250 ms |
| Street-graph proximity | If source and listener are on road segments not connected within 2 hops / 40 m, treat as occluded (traffic one street over) | Free (lane graph exists, §7.7) |
| Courtyard snapshot | Inside a compound polygon (temple compound, bahal/bahi, durbar courtyard — enterable per product owner) → mixer snapshot `Courtyard`: street bed −9 dB, street LPF 2.5 kHz, reverb RT60 0.7–1.0 s (brick walls), early reflection 20–40 ms; sacred and pigeon layers +3 dB | One snapshot transition (0.8 s) |
| Galli (narrow lane) slapback | Lane width w from the road model (roads.md): echo delay = 2w/343 s (w 3 m → 17 ms, 6 m → 35 ms), feedback 0.15, wet −14 dB, only for footsteps, horns and engines | One SFX Reverb on a send |
| Durbar square | Open square bounded by temples: RT60 ≈ 1.2 s, pre-delay 60 ms, wet −18 dB | Snapshot |
| Temple interior / sanctum | Small room RT60 0.3–0.5 s, outside −15 dB, LPF 1.2 kHz | Snapshot |
| Vehicle interior (driving car/bus, first-person or close camera) | Exterior −8 dB, LPF 3 kHz; own engine LPF 2 kHz, +cabin rattle | Snapshot |

Snapshots via `AudioMixerSnapshot.TransitionTo` or `AudioMixer.TransitionToSnapshots` with weights for blended zones.

### 6.6 Mixer graph and levels

```
Master (limiter −1 dBTP)
├── Music          (−18 LU under gameplay; ducked −4 dB by Stings)
├── UI             (0 dB ref)
├── Stings
├── World
│   ├── Ambience   (beds −20 LU; ducked −3 dB by aircraft flyby within 1 km)
│   ├── Sacred     (bells, conch, aarti)
│   ├── Traffic    (NPC engines, tyres, horns)  ← send to Galli reverb
│   ├── Player     (own vehicle, footsteps, foley)  ← send to Galli reverb
│   ├── Nature     (birds, animals, weather)
│   └── Aircraft
└── Voice (barks)  (ducks Ambience −4 dB, Traffic −3 dB)
```

Relative level targets at 10 m [E], tuned so the scene sits near −16 LUFS integrated: player engine at cruise −14 dBFS RMS, NPC motorbike pass-by −20, bus pass-by −16, horn tap peaks −10, shrine bell −18, great bell −12, crow −24, ambience bed −24, footsteps −22. Real Kathmandu levels are ≈ 67 dB(A) average, 72 dB(A) in high-traffic zones, up to ≈ 80 dB(A) at New Road, ≈ 64 dB(A) in Thamel, ≈ 63 dB(A) residential [S NepJOL commercial-zones study and city-wide surveys]; the game compresses that range: old core and arterials about 6 dB louder than residential lanes, fields 12 dB quieter, forest 15 dB quieter.

### 6.7 Unity 6 settings and performance notes

| Setting | Value | Why |
|---|---|---|
| DSP Buffer Size | **Good latency** (Mid/High), **Best performance** on Low | Options trade latency for CPU [S]; commonly 512 and 1,024 frames **[V via `AudioSettings.GetDSPBufferSize` on device]**; 1,024 frames at 48 kHz = 21 ms, fine for a non-rhythm game |
| Sample rate | 0 (system default) — on iOS and Android it is reference-only [S]; expect 48 kHz | Synth code always reads `AudioSettings.outputSampleRate` |
| Speaker mode | Stereo | Default [S] |
| Doppler Factor (global) | 0 (custom Doppler §6.3) | Floating origin |
| Virtual voices | §6.1 | |
| Load in background | On for beds and spots | Avoid main-thread hitch on region load |
| Preload audio data | Off for region audio; on for UI and footsteps | |
| Audio Random Container | Use for footsteps, birds' baked variants, bells, horns: avoid-repeat, volume/pitch randomisation, trigger intervals [S Unity 6 manual] | Built-in, no custom code |
| iOS audio session | "Mute Other Audio Sources" off so the player's own music keeps playing and the game respects the silent switch **[V exact category on device]**; pause audio on `OnApplicationPause(true)` | Store review friendliness |
| Bluetooth output | Adds roughly 100–250 ms latency [E]; never sync gameplay to audio | |
| Thermal step-down | When Adaptive Performance reports serious thermal state, halve synth voices and drop to baked loops | ARCHITECTURE §10 |

Profiling: Unity Profiler Audio module (DSP CPU, voice counts, memory per clip), a `ProfilerMarker` inside generator `Process` for development builds only, and the audio memory counters on the FPS/memory HUD (ARCHITECTURE §7.13). Acceptance: audio thread DSP < 10% of one core on the Low reference device, zero dropouts in the 20-minute soak.

### 6.8 Phone speakers

Built-in phone speakers have little output below ≈ 250–400 Hz and are often mono or narrow stereo [E]. Mitigations:

* Every low-pitched source has energy in harmonics 2–8 (the ear infers the missing fundamental): bus, Enfield, bells, helicopter (39/58/78 Hz → push 120–400 Hz partials), aircraft.
* Settings → Audio → "Output: Auto / Speaker / Headphones". Speaker profile: HP 120 Hz, gentle saturation (tanh, drive 1.3) on Traffic and Player groups, +2 dB shelf at 2 kHz; Headphones profile: flat, full stereo width. "Auto" defaults to Speaker [E] (Unity has no portable headphone-detection API; a native plugin could add it later).
* Keep ambience beds' stereo width ≤ 70% so mono summing on phone speakers loses nothing important.

---

## 7. Ambience zoning by area, time and season

### 7.1 Zones

Base weights come from the street_life 250 m grid (area type per cell), bilinearly interpolated at the listener, crossfade time 3 s (walking) to 1.5 s (driving). Special zones override with polygons.

| Zone | Source | Bed A | Bed B | Spot emitters |
|---|---|---|---|---|
| OLD_CORE | grid + curated polygons (street_life §1.3) | Crowd walla (dense) | Scooter traffic hum, brick footsteps | Shrine bells, pigeons, shutters, vendor calls, pressure cookers, tea clinks |
| URBAN | grid | Traffic hum (P, density from traffic) | Light crowd | Horns, crows, bus air brakes, conductor slaps |
| PERI_URBAN | grid | Light traffic | Birds (sparrow, myna, bulbul) | Dogs, roosters, pressure cookers, tractors |
| FIELDS | grid | Wind in crops | Insects / frogs (season) | Egrets, distant tractors, irrigation trickle |
| FOREST | grid | Wind in leaves | Birdsong (by altitude band) | Cicadas (summer), woodpecker, monkeys near troops |
| TEMPLE_COMPOUND | compound polygons (landmarks + bahal footprints) | Courtyard murmur | Pigeons | Bells (rate from time of day), cymbals, conch at dawn/dusk; snapshot `Courtyard` |
| STUPA_KORA | Boudha, Swayambhu kora rings | Walking crowd (slow) | Prayer-wheel creaks | Wheel bells, pigeons, distant dungchen [V], macaques (Swayambhu) |
| DURBAR_SQUARE | Kathmandu/Hanuman Dhoka, Patan, Bhaktapur squares | Crowd walla | Pigeons | Great bell (schedule [V]), shrine bells, vendors; snapshot `DurbarSquare` |
| GHAT | Pashupati ghats (no cremation depicted) | River | Temple murmur | Aarti at 18:00 [V], bells, conch, macaques |
| RING_ROAD / ARTERIAL | road class | Heavy traffic hum | — | Bus/truck air brakes, horns |
| AIRPORT | aerodrome polygon + 3 km buffer | Distant city hum | — | Aircraft (schedule §2.11), APU hum on apron |
| PARK | leisure=park | Birds, light wind | Distant traffic −12 dB | Kids, crows |
| WATER | river/stream lines | `ghm_amb_water_*` by LineKind and slope | — | Spout splash at hitis |
| HILLTOP (> 1,600 m) | elevation | Wind (stronger) | Distant city hum (night) | Kites (bird), dogs far below |

### 7.2 Time-of-day multipliers (bed/spot gain; 1.0 = nominal)

Times follow street_life's daily table. Values [E].

| Band | Traffic | Crowd | Horn rate (per min, urban) | Sacred | Birds | Dogs | Insects | Notes |
|---|---|---|---|---|---|---|---|---|
| Dawn 04:30–06:00 | 0.2 | 0.1 | 0–2 | 0.8 (conch, first bells) | **1.0 (dawn chorus, koel, crows)** | 0.6 | 0.4 | Roosters peri-urban |
| Morning puja 06:00–08:00 | 0.4 | 0.4 | 3–8 | **1.0 (bells every few s at busy shrines)** | 0.8 | 0.3 | 0.1 | Pressure cookers, shutters start |
| Rush 08:00–10:30 | **1.0** | 0.8 | 10–20 | 0.5 | 0.4 | 0.1 | 0 | School buses |
| Day 10:30–16:00 | 0.8 | 1.0 | 8–15 | 0.3 | 0.4 | 0.1 (asleep) | 0.3 (summer cicadas) | Kites (bird) |
| Evening 16:00–19:00 | **1.0** | **1.0** | 10–20 | 0.8 (aarti, bhajan from 18:00) | 0.7 (roost flights) | 0.3 | 0.3 | Shutters down from 19:30 |
| Night 19:00–22:00 | 0.5 | 0.4 (Thamel 1.0 to 22:00) | 3–8 | 0.3 | 0.1 | 0.6 | 0.8 | |
| Late night 22:00–04:30 | 0.1 | 0.05 | 0–1 | 0 | 0 | **1.0 (bark chains)** | 1.0 | Distant city hum only; aircraft stop at 24:00 [V] |

### 7.3 Season and weather

| Season (approx.) | Changes |
|---|---|
| Spring (Mar–May) | Koel and Indian cuckoo; dry wind gusts; dust on gravel |
| Monsoon (Jun–Sep) | Rain beds and **rain on tin roofs**, gutters, frogs at night in fields, rivers +6 dB with more chirps, mud footsteps replace dirt, thunder (distant) |
| Autumn (Oct–Nov) | Clear air (less LPF on distant sounds, +20% audible range for aircraft and bells [E]); kite-flying string zips and cheers around Dashain (generic); Tihar evenings: soft distant firecrackers, deusi-bhailo groups only from permitted recordings |
| Winter (Dec–Feb) | Morning fog: −3 dB distant layers, more LPF; fewer insects; wedding brass bands on auspicious days [V] |

Weather overrides season: rain intensity from the weather system drives rain synth λ and ducks birds (−12 dB in heavy rain) and crowds (−6 dB).

---

## 8. Memory and size budget (valley, W2)

Rates [E]: Vorbis mono 32 kHz q≈0.4 ≈ 6–8 KB/s; Vorbis stereo 32 kHz q≈0.4 ≈ 10–12 KB/s; ADPCM mono 22.05 kHz ≈ 11 KB/s, 32 kHz ≈ 16 KB/s; PCM16 mono 32 kHz = 64 KB/s. Resident = compressed size for "Compressed in Memory" (plus decoder state), PCM size for baked clips.

| Item | Count × length | Format | Disk (MB) | Resident Mid (MB) |
|---|---|---|---|---|
| Footsteps: 10 sets × (6 walk + 6 run + land + scuff) | 140 × 0.3 s | ADPCM 22 kHz, compressed in memory | 0.46 | 0.46 |
| Tyre loops (procedural) | — | P | 0 | 0 |
| Engine loops | — (procedural) | P | 0 | 0 |
| Recorded foley (cloth, doors, glass, kick-start, kickstand, baskets, shutter clang, sliding door) | 60 × 0.6 s | ADPCM 22 kHz | 0.40 | 0.40 |
| Ambience beds: OLD_CORE crowd, URBAN traffic texture, PERI birds, FIELDS, FOREST hill + subalpine, courtyard murmur, stupa kora, water stream/river/roar, rain light/heavy | 13 × 45 s stereo | Vorbis 32 kHz stereo | 6.4 | 2 beds at once, ≈ 1.1 (+ decode buffers) — streaming optional |
| Crowd texture grains (for H walla) | 1 × 20 s mono | Vorbis | 0.14 | 0.14 |
| Macaque, cow bell, chicken, goat recordings | 20 × 1.5 s | Vorbis mono | 0.21 | 0.21 |
| Baked procedural bank (§4.4) | ≈ 210 | PCM in memory | 0 | ≈ 6 (Low ≈ 3) |
| Real-time synth state | ≤ 12 voices | — | 0 | < 0.2 |
| Music (manifest §13.2) | streaming | Vorbis | outside this file | 3 |
| **Total for this spec** | | | **≈ 7.6 MB disk** | **≈ 11.5 MB + music 3 ≈ 14.5 (Low), ≈ 20 (Mid), ≈ 26 (High with all variants)** |

Fits: ≤ 12 MB M1 audio download (ASSET_MANIFEST) and the 25 / 40 / 50 MB tier caps with headroom for UI, voice barks and festivals. If beds are switched to **Streaming** load type, resident drops by ≈ 1 MB but costs one streaming decoder per bed (2 at most).

---

## 9. Data definitions (engineer-facing)

`VehicleSoundDef` (ScriptableObject, one per preset in §4.2):

```json
{
  "id": "moto_commuter",
  "engine": { "type": "combustion", "cylinders": 1, "fourStroke": true,
              "idleRpm": 1400, "redRpm": 8500, "harmonics": [1,0.8,0.6,0.5,0.35,0.3,0.2,0.15,0.1,0.08,0.06,0.04],
              "tiltOff": 1.4, "tiltOn": 0.8, "jitterPeriod": 0.02, "jitterAmp": 0.08,
              "noiseBase": 0.10, "noiseLoad": 0.25, "pipeHz": 105, "pipeQ": 2.0, "drive": 1.6,
              "gears": [3.0, 1.9, 1.4, 1.1, 0.9], "finalDrive": 3.1, "wheelCircM": 1.9,
              "shiftUp": 0.88, "shiftDown": 0.40, "shiftMs": 180 },
  "horn": "horn_moto",
  "tyre": { "widthClass": "narrow" },
  "extras": ["kickstart", "electric_start", "gear_clunk", "brake_squeak"],
  "attenuation": { "min": 3, "max": 60, "spread": 30, "airLpf": false },
  "priorityBase": 96,
  "seedVariance": { "rpmScale": 0.05, "harmonicJitter": 0.1, "pipeHz": 0.08 }
}
```

`seedVariance` makes each NPC vehicle (seeded from its agent id) sound slightly different without new assets. `AmbienceZoneDef`: bed ids, spot list `{id, ratePerMin by time band, minDist, maxDist, season mask, weather mask}`, snapshot name. `FootstepMap`: `Surface` → footstep set id (§2.1), with weather override (DIRT→MUD in monsoon) mirroring the vehicle grip table.

---

## 10. Cultural and rating checklist (for the reviewer)

1. Bells, conch, cymbals: authentic use contexts only (arrival at shrine, puja, aarti); no comic sound effects on sacred objects; player bell ringing rate-limited.
2. No recorded chants, mantras, bhajan vocals, azan or church bells without community permission (manifest §13); synthesised suggestions are instrumental or drone only **[V]**.
3. Great-bell ringing schedule and the Bhaktapur "barking dogs" bell easter egg need a reviewer's OK **[V]**.
4. Shoes-off barefoot footsteps only at compounds where the custom applies **[V per site]**.
5. Horn usage reflects the 2017 no-horn rule (friendly short taps), musical pressure horns only outside the valley.
6. No sirens, crashes with injury sounds, animal distress, sacrifice, cremation or gunfire-like sounds; firecrackers soft and distant (Tihar only).
7. No intelligible speech in walla; vendor calls generic; no real brand, route or product names.
8. Safa tempo, microbus and bus conductors are friendly and dignified; the body-slap signal verified **[V]**.

---

## 11. Engineer checklist (order of work)

1. `Synth/` primitives + unit tests in EditMode (deterministic output hash per seed; no allocations in `Process`, verified with the GC allocation profiler).
2. `GeneratorVoice` (6.3) and `FilterReadVoice` fallback; **device test of 3D spatialisation for both** (§4.1 [V]).
3. EngineVoice + presets; RPM/gear model; tune by ear against reference recordings (not shipped).
4. ProceduralBank (bells, horns, birds, animals, brakes); time-sliced `SetData`.
5. VoiceManager with priorities, culling and caps (§6.1); custom rolloff curves; DopplerSolver.
6. Footstep sets from CC0 sources with LICENSES.md entries; FootstepMap from `Surface`.
7. AmbienceDirector: grid lookup, zones, snapshots, time/season/weather matrices.
8. Aircraft scheduler over TIA runway line, approach 3° from the SSW onto 02 [V], helicopter shuttles from the helipad.
9. Speaker/headphone profile; thermal step-down; audio HUD counters.
10. Device pass on the Low reference phone: DSP < 10% of a core, 0 dropouts in 20 min, resident ≤ 25 MB.

---

## 12. Sources

Licences and libraries
- Sonniss #GameAudioGDC bundle licence (not CC0; no redistribution, no AI training): https://sonniss.com/gdc-bundle-license/
- Kenney audio packs, e.g. Impact Sounds (CC0, 130 files): https://kenney.nl/assets/impact-sounds and https://kenney.nl/assets/category:Audio
- BigSoundBank licence (CC0 1.0): https://bigsoundbank.com/droit.html
- OpenGameArt licences FAQ: https://opengameart.org/content/faq
- Freesound licences: https://freesound.org/help/faq/
- CC0 1.0 legal code: https://creativecommons.org/publicdomain/zero/1.0/
- NPS Sound Gallery: https://www.nps.gov/subjects/sound/gallery.htm

Unity
- `MonoBehaviour.OnAudioFilterRead` (audio thread, interleaved data, works without a clip): https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.OnAudioFilterRead.html
- Audio settings (DSP buffer, Doppler factor, sample rate, voices): https://docs.unity3d.com/6000.3/Documentation/Manual/class-AudioManager.html
- Scriptable audio pipeline, generators (`IAudioGenerator`, Burst, real-time safety): https://docs.unity3d.com/Manual/audio-scriptable-processors-generators.html and https://docs.unity.cn/Manual/audio-scriptable-processors.html
- New in Unity 6.3 (Audio Random Container VU meter, scriptable processors): https://docs.unity.com/en-us/engine/6000.6/manual/whats-new/unity63.md
- Audio Random Container: https://docs.unity.com/en-us/engine/6000.3/manual/audio/random-container/ui

Kathmandu soundscape facts
- No-horn regulation from 14 April 2017: https://kathmandupost.com/valley/2017/04/15/no-horn-regulation-comes-into-effect ; https://kathmandupost.com/valley/2017/04/04/valley-to-be-declared-no-horn-zone ; https://thehimalayantimes.com/kathmandu/honking-ban-valley-new-nepali-year
- Enforcement revived/tightened: https://thehimalayantimes.com/kathmandu/traffic-police-revive-campaign-against-needless-honking ; https://english.onlinekhabar.com/traffic-police-tighten-no-horn-rule-in-kathmandu.html
- Noise levels (New Road 79.6 dB(A), Thamel 63.9 dB(A)): https://www.nepjol.info/index.php/arj/article/view/87534 ; policy effectiveness study: https://link.springer.com/article/10.1007/s11356-021-13236-7 ; https://thehimalayantimes.com/kathmandu/noise-pollution-alarming-in-kathmandu/
- Safa tempo (72 V, 12 batteries, ~45 km/h, 600–714 vehicles): https://cen.org.np/uploads/doc/ev-fact-sheet-60bc4644dabb5.pdf ; https://www.downtoearth.org.in/news/clean-drive-27297 ; https://nepalitimes.com/for-nepal-four-wheels-good-three-wheels-bad ; https://makeshiftmobility.substack.com/p/27-hello-safa-tempo
- TIA runway, VOR/DME approach to 02, helipad, airlines: https://en.wikipedia.org/wiki/Tribhuvan_International_Airport ; ILS status: https://www.icao.int/APAC/Meetings/2014%20APANPIRG25/IP%208%20-%20AI%203.pdf
- TIA traffic (12,525 movements, Nov 2024): https://english.onlinekhabar.com/tribhuvan-international-airport-handled-over-850000-passengers-and-12000-flights-in-november.html
- TIA 18-hour operation from 2025-04-01: https://ekantipur.com/news/2025/04/01/tribhuvan-international-airport-will-operate-daily-for-18-hours-from-today-28-28.html
- Durbar squares and their Taleju bells: https://en.wikipedia.org/wiki/Kathmandu_Durbar_Square ; https://en.wikipedia.org/wiki/Patan_Durbar_Square ; https://en.wikipedia.org/wiki/Bhaktapur_Durbar_Square (bell dates and the "barking dogs" lore to be confirmed by the reviewer [V])
- Bird species ranking, dogs, cattle, macaques: see [street_life.md](street_life.md) §7–8 and its sources.

Engineering estimates (no single source; marked [E]): engine RPM ranges, harmonic tables, exhaust resonances, propeller/fan blade-pass frequencies (derived from published blade counts and typical shaft speeds), bell partial ratios, air-absorption figures (approximating ISO 9613-1), codec bitrates, CPU costs. Tune on device and by ear against reference recordings that are never shipped.
