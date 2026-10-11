# Reference brief: flight package (owner playtest #11)

> Owner, first Unity playtest (docs/W2_PLAYTEST_1.md #11): "Add Car, Bus, and truck driving, Airplane flying as well
> for the character. Allow the user to grab and start riding the parked vehicles around, including planes."
> This brief covers the aircraft half (ground vehicles are the play package): what really stands at Tribhuvan
> International Airport (TIA, VNKT/KTM) today, the dimensions and colours the models follow, where they park, how they
> fly in the game and how the player boards them. Airport geometry, traffic and procedures come from
> [aviation.md](aviation.md); this file only adds what the flyable aircraft need.
>
> Branding rule (ASSET_MANIFEST, LICENSES): no airline names, logos, liveries recognisable as a carrier,
> registrations or flags. Carriers below are research facts about the fleet mix only; the models blend the common
> types into generic cartoon shapes in the generic palettes of aviation.md §7.3.

## 1. What flies from TIA's domestic apron now (2023–2026)

| Type (real) | Who flies it at KTM | Why it is in the game |
|---|---|---|
| ATR 72-500 / -600 | Buddha Air (largest fleet, first 72-600 delivered July 2026), Yeti Airlines; also ATR 42s | The commonest domestic airliner in Nepal: the default flyable aircraft (`FlightCraft.Turboprop`) |
| De Havilland Canada Dash 8-400 | Shree Airlines (6+ since 2024) | The longer high-wing twin with gear in the nacelles (`TurbopropStretch`) |
| BAe Jetstream 41 | Guna Airlines (4), formerly Yeti and Agni Air | The low-wing 29-seat commuter, distinctive cruciform tail (`Commuter`) |
| DHC-6 Twin Otter (also Dornier 228, Let 410) | Tara Air, Nepal Airlines, Summit/Sita (Do 228) | The mountain STOL workhorse to Lukla, strut-braced high wing, fixed gear (`Stol`) |
| Airbus H125 / AS350 B3 | Fishtail Air, Simrik, Shree, Heli Everest, Kailash ... | The dominant charter and rescue helicopter (`Helicopter`); 2005 Everest summit landing was an AS350 B3 |

Sources: Ekantipur "Buddha bringing new generation ATR aircraft" (2026-07-21); Onlinekhabar "Buddha Air's new ATR
72-600 begins commercial operations"; Himalayan Times "Yeti adds ATR 72-500"; Himal Press "Shree Airlines add two Dash
8s"; ch-aviation "Shree Airlines to add three Q400s"; aviospace.org "Nepal's main fixed-wing & helicopter operators in
2025"; Wikipedia type pages (dimensions, §2). International jets (A320/737/A330) are not flyable: they stay ambient
traffic (AirTrafficSim).

## 2. Dimensions and performance (manufacturer data via Wikipedia type pages)

| Craft | Length m | Span m | Height m | Props | Real speeds used | Game spec notes |
|---|---|---|---|---|---|---|
| ATR 72-600 | 27.17 | 27.05 | 7.65 | 2 × 6-blade, Ø 3.93 m | Vs ≈ 98 kt clean / 82 kt full flap, Vr ≈ 107 kt, max cruise 275 kt, TO run 1,279 m (SL) | Mass 19 t, wing 61 m², static thrust 40 kN, door rear left (real) |
| Dash 8-400 | 32.8 | 28.4 | 8.34 | 2 × 6-blade, Ø 4.11 m | cruise 300–360 kt, TO 1,425 m, landing 1,289 m; main gear retracts aft into the long nacelles | Mass 25 t, 63.1 m², airstair door front left |
| Jetstream 41 | 19.25 | 18.29 | 5.74 | 2 × 5-blade, Ø 2.9 m | 260–295 kt cruise, 2,200 ft/min climb, TO run 1,500 m | Mass 9.5 t, 32.6 m², low wing, cruciform tail |
| DHC-6-300 Twin Otter | 15.77 | 19.81 | 5.94 | 2 × 3-blade | stall 56 kt (flaps), 182 kt max cruise, STOL take-off 370 m to 50 ft, 1,600 ft/min | Mass 5 t, 39 m², fixed tricycle gear, struts |
| H125 / AS350 B3 | 10.93 fuselage (12.94 rotors turning) | rotor Ø 10.69 | 3.14–3.34 | 3-blade main, 2-blade tail rotor | cruise 132 kt, Vne 155 kt, climb 8.5 m/s, ceiling 4,600 m | Mass 2 t, skids |

The flight model derives its lift coefficients from these stall speeds at TIA's elevation (1,340 m) and its drag from
the top speed at 3,000 m, so each type stalls, rotates and cruises at its real speeds (`FlightSpec.For`, checked by
`FlightTests.TheSpecsMatchTheRealAircraft`). Arcade simplifications: no wind, no engine failure, auto-trim holds the
flight path hands off, auto gear, wings level themselves below 15 m.

## 3. Where they stand at TIA (OSM, ODbL)

Extracted from `pipeline/data/raw/osm/nepal.osm.pbf` (pyosmium, `aeroway=*` in 85.335–85.375 E, 27.675–27.716 N):
94 features, including 10 `parking_position` lines (domestic stands D1–D7 and D15–D17), 12 `gate` nodes (international
gates 1–11 and "Departure A"), 6 aprons, 53 taxiways, 2 windsocks and the charter helipad.

| Stand | OSM | Line (lon, lat → lon, lat) | Aircraft in the game | Notes |
|---|---|---|---|---|
| D1 | w220704665 | 85.360135, 27.704458 → 85.360474, 27.704690 | Twin Otter-class | short stand at the south end of the domestic apron |
| D2 | w909580836 | 85.360017, 27.704781 → 85.360559, 27.705073 | ATR 72-class | |
| D3 | w909580837 | 85.359903, 27.705094 → 85.360641, 27.705305 | Dash 8-class | |
| D4 | w909580838 | 85.359786, 27.705415 → 85.360534, 27.705587 | ATR 72-class | |
| D5 | w1550068566 | 85.359677, 27.705713 → 85.360422, 27.705879 | Jetstream-class, 50 m along | the ambient traffic parks at the apron's vertex mean (85.359813, 27.705913): kept ≥ 30 m clear |
| D6 | w1550068567 | 85.359571, 27.706004 → 85.360308, 27.706177 | ATR 72-class, 50 m along | same clearance |
| D7 | w909580839 | 85.359484, 27.706242 → 85.360196, 27.706470 | Twin Otter-class | clear of the ambient helicopters' lift-off point (85.36000, 27.70650) |
| D16, D17 | w1550068572, w1550068571 | continue D5, D4 towards the taxi lane | H125-class helicopters | |
| Gate 10 | n9593444182 (gate node) | 85.358497, 27.701709, nose out on 111.7° | ATR 72-class | domestic turboprops also fly KTM–Varanasi from the international terminal [S: Buddha Air route]; remote stand in front of gate 10 |
| Helipad | n10121417304 "Fish Tail Air Helicopter Charter and Rescue Services" | 85.345946, 27.684082 | H125-class | outside the fence, west of threshold 02 (outside the kathmandu_core sample tiles: drawn only where ground is resident) |

Gameplay choices [E]: aircraft stand with the nose towards the taxi lane (east, towards the runway) so the player can
taxi straight out; a parked aircraft the player has flown stays where it was left (`FlightStands.Park`).

Runway (aviation.md §1): 02/20, 45 m, thresholds 02 (85.3534132, 27.6839528, 1,315 m) and 20 (85.3639103, 27.7070042,
1,337 m), displaced sections 285 m and 277 m. The flight model's runway surface is exactly the drawn one
(`RunwayMesher`: straight profile between the threshold elevations, raised over the terrain, +0.3 m), shoulders 7.5 m.

## 4. Look of each model (silhouette, colours, details)

Reference photos (Openverse, open licences; reference only, never copied): contact sheets in
`/home/user/wt/refs/flight/{atr72,dash8,jetstream,twinotter,dhc6,h125,buddha,tia,tia_dom}/contact_sheet.jpg`.

| Craft | Silhouette features modelled | Colours | Photo credits (examples) |
|---|---|---|---|
| ATR 72 | round fuselage, short rounded nose with a dark wrap-around windscreen band, **main-gear sponsons low on the sides**, high straight wing (constant chord to the nacelles, tapered outboard), nacelles hanging under the wing with an intake scoop under the spinner, **six-blade props**, **T-tail with a strongly swept fin and dorsal fillet**, black leading-edge de-icing boots, rear left passenger door, forward left cargo door, ~15 small windows a side | white body, grey belly, livery tail and cheat-line, black boots and blades with light tips | "Avianca ATR 72-600" Clément Alloing CC BY-NC-ND 2.0; "PS-VPB ATR 72-500 Voepass" IndiaEcho CC BY 2.0; "Buddha Air ATR 72-500 on finals into Tribhuvan International" Rui Miguel CC BY-SA 4.0 |
| Dash 8-400 | longer, pointier nose, **long nacelles reaching past the trailing edge** with the twin main wheels under them on tall legs, upright T-tail fin, 18 windows | as above | "PAL Airlines Dash 8 Q400" Karly Barker CC BY-SA 4.0; "Dash 8 Q400 YL-BAQ" Gerard van der Schaaf CC BY 2.0 |
| Jetstream 41 | **low wing with dihedral**, ventral wing-box fairing, over-wing nacelles close to the fuselage, **five-blade props**, **cruciform tail** (stabiliser half way up the fin), big oval windows, forward airstair door | as above | "Jetstream 41 G-MAJL" Ronnierob CC BY-SA 2.0; "ZS-NRK Jetstream 41" Aeroprints.com CC BY-SA 3.0 |
| Twin Otter | **boxy cabin** (superellipse sections), long baggage nose, **rectangular high wing with a strut each side**, slim nacelles, **three-blade props**, fixed gear with splayed main legs, square windows, big rear cargo door, cockpit doors both sides | white with stripe and tail colour | "Twin Otter aircraft on Lukla airstrip" markhorrell CC BY-NC-SA 2.0; "DHC-6 Twin Otter N63119" Asylumkid CC BY-SA 2.0 |
| H125 | **bubble cabin with big glazing**, engine cowling hump on the roof, thin tail boom, fin above and below, stabiliser with end plates, tail rotor on the left, **three-blade main rotor**, skids on cross tubes | red / yellow / blue body with a white or dark belly band (no medical crosses) | "F-HADE AS 350 B3" Laurent Quérité CC BY-NC-SA 2.0; "G-TVGB H125" James CC BY-SA 2.0 |

Cockpit view: dark glareshield over a panel with four glowing displays (ATR/Dash 8/Jetstream glass cockpits; the
Twin Otter's is analogue in reality, drawn the same for simplicity), yokes, the nose in the body colour ahead and the
window posts (Dash 8 cockpit photos by Markusnl, CC BY-NC-SA 2.0).

## 5. Flight, boarding and resets in the game

* **Boarding**: walk up to any parked aircraft (within 3.5 m of its skin); the prompt says "Board" and E / the Action
  button boards it as pilot; the walker is placed in the cockpit, engines start, the aircraft's stand is free.
  Getting out: stand still on the ground, then hold E (or the HUD's Get out button): the pilot steps out by the main
  door on the left and the aircraft stays parked there.
* **Controls** (keyboard): W / S pitch (W pushes the nose down, S pulls it up, as in every flight game), A / D roll, Q
  / E yaw (rudder and nose-wheel steering), Shift / Ctrl throttle up / down (the lever stays where it is put), F / V
  flaps down / up, Space brakes, C chase or cockpit view. Gamepad: left stick pitch / roll, triggers throttle, shoulders
  rudder, D-pad flaps. Touch (`UI/Hud/AircraftHud.cs`): a floating flight stick on the left (pitch / roll, steering on
  the ground), a throttle slider on the right edge, Brake and Flaps down / up beside it, Left / Right rudder buttons,
  View and Get out under the top bar.
* **HUD**: an instrument strip under the top bar (airspeed in knots, height above the ground in feet, climb in ft/min,
  heading, power %, flaps stage, wheels up / down), a red "Too slow!" banner on the stall warning, a cyan "Turning back"
  banner at the region's edge, a how-to-fly hint for 12 s after boarding (keys or touch), and "Full power, then pull up
  gently at N kt" while rolling for take-off. The km/h speedometer hides while flying. English and Nepali.
* **Model**: taxi with nose-wheel steering (60° at walking pace), rotate at Vr, climb, bank-to-turn with coordinated
  turns, stall warning at 85% of the stall angle or 1.12 × stall speed, a soft stall that drops the nose by itself,
  flaps 0 / 15 / 30 (two stages), auto gear, ground effect in the flare.
* **Never a crash**: touching a hillside, water, a building or the ground too hard makes a soft cartoon bounce
  (1.3 s), then the aircraft is reset to the nearest runway end lined up (within 3.5 km, or when slow) or to 350 m
  above the highest ground around, heading for the airport, with a friendly toast.
* **Bounds**: past a 2.5 km band inside the region (the sample's bbox grown to hold the runway and a circuit, plus
  3 km) the aircraft banks back towards the centre; the hard edge and a 6,000 m ceiling cannot be crossed.
* **Ambient traffic**: while the player is in an aircraft at or near the airport, `AirTrafficSim.HoldRunwayForPlayer`
  keeps its aircraft off the runway: departures hold short, arrivals not yet airborne wait, arrivals further than
  1.5 km from touchdown go around (a real, frequent TIA sight).
* **Camera**: chase view at 1.25 × the aircraft's size + 8 m behind, a fifth of that above, following the flight path
  with a 0.6 s yaw ease and a third of the climb angle, never rolling (steady horizon: the owner's #5 complaint about
  cameras); cockpit view at the pilot's eye.

## 6. Render vs photo (previews)

`/home/user/wt/previews/flight/compare_{Turboprop,TurbopropStretch,Commuter,Stol,Helicopter}.jpg` put the LOD0 render
(3/4 and side) next to two reference photos of the real type; `*_lod0.png` are the four-view sheets. What the
comparison changed: the windscreen was vertex paint smeared over the coarse fuselage ring (a blurry dark smudge on the
nose); it is now a dark glazing visor sampled on the skin (two windscreen panes and two side windows with slim posts,
the side glazing sweeping back as on the real cockpits; the helicopter's bubble runs down to the chin). The cheat-line
was a one-vertex-row paint band that read as a thin smear; it is now a crisp strip on the skin that sweeps up toward
the tail. The Twin Otter-class was 1 m too long (fin trailing edge behind a too-long tail cone): fin, fillet and
stabiliser moved forward and the cone shortened to the real 15.77 m. Remaining simplifications: windows are fewer and
rounder than the real rows, the ATR's sponsons are rounder, no registration or name anywhere (generic liveries).

## 7. Open questions [V]

1. Which domestic stands each operator really uses (assignment here is illustrative, by type size).
2. Real stand orientation on the domestic apron (nose-in towards the terminal vs. nose-out); the game parks nose-out
   for playability.
3. Exact helicopter parking spots inside the domestic apron (OSM maps only D15–D17 as short lines).
