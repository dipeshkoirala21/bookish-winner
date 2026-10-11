# W2 playtest-fix round: reference brief for the chase cameras (package `camera`)

> Status: brief for owner playtest #5 (docs/W2_PLAYTEST_1.md), 2026-10-11. It supersedes the camera numbers of
> W2_DESIGN §6.4. Code: `Core/Characters/ChaseCamera.cs` (the engine-free camera), `Core/Characters/CameraSprings.cs`
> (springs and the frame clock), `Core/Characters/ChaseCameraBoom.cs` (the boom), `Core/Characters/CameraRigTable.cs`
> (the numbers below), `Characters/Cameras/ChaseCameraRig.cs` (the Unity driver). Tests:
> `core-tests/CharactersCameraRideTests.cs` (recorded rides on real streets), `core-tests/CharactersCameraTests.cs`,
> `Tests/EditMode/ExploreCameraTests.cs`. Tags: [S] sourced (§8), [E] estimate (geometry, gameplay footage),
> [M] measured here (§7).

## 0. Owner feedback (verbatim)

"Camera angle is shitty.. It constantly keeps moving which make the driving experience really bad. reach about the
camera position while riding on a different vehicles like cars, motorcycle, scooter, bus etc and try to implement
that camera angle."

## 1. What was wrong (measured before the fix, §7)

The pre-fix rig (commit 5a7668a) was ported line by line into `core-tests/CharactersCameraLegacy.cs` and driven over
the same recorded rides as the new camera.

1. **The camera shook against the vehicle every frame.** The followed point used Unity's `SmoothDamp` with 0.06 s.
   `SmoothDamp` holds the goal still during the step, so its lag behind a moving vehicle depends on the frame time;
   with a phone's frame clock (33 ms ± 4 ms, a 16.7 ms frame now and then, a 50 ms hitch every 3 s) the lag changed
   by centimetres every frame. Position jerk on the car ride at 32 km/h: p95 **2 314 m/s³** (the car itself:
   120 m/s³).
2. **Ground height went straight into the view.** The vehicle's height is the ground sample under it (seams between
   road and terrain, kerbs, deck joints); the pivot followed it within 0.06 s and the ground clamp under the camera
   lifted it in one frame (a 3.2 m jump on the car ride, 513°/s of rotation on a straight).
3. **Lane mode pumped.** In the old core, walls within 3 m on both sides switch on "lane mode" (+15° pitch, −15 %
   distance over 0.5 s, re-tested at 10 Hz). Every gap between houses switched it off and on: on the scooter ride
   through Indra Chowk the camera rotated at a p95 of **41°/s on straight lanes**.
4. **Pull-in snaps.** The boom pulled in "at once" when anything crossed it and eased out 0.2 s later: 1.7 m steps
   in one frame in the lanes.
5. **The lean rolled the view** (12 % of a two-wheeler's lean), the speed FOV kick and the look-ahead read the raw,
   noisy speed.
6. **The framing itself**: a 62° horizontal FOV kept on a 20:9 landscape phone leaves a 30° vertical FOV (a long
   lens) and the rigs looked down 12–17°: on a car, SUV, bus or truck the horizon sat at or above the top edge of the
   screen (NDC +0.8 to +1.1), so the player saw road and walls but not where the road goes. In portrait the vehicle
   was tiny (7–10 % of the screen height) under half a screen of sky.

## 2. How the best open-world games frame and steady a vehicle camera

Exact parameters of GTA V, Forza Horizon, Watch Dogs or Mafia are not published; the rules below are what the
documentation, the engines' own camera rigs and the modding communities agree on, with the numbers we derive from
them in §4.

| Rule | Evidence |
|---|---|
| Follow with **critically damped springs** (no overshoot, no abrupt change of motion, the camera never loses the player, identical at any frame rate) | Juckett, "Damped Springs" [S1]; Unity `SmoothDamp` is Game Programming Gems 4 §1.10 [S2]; Unreal's `CriticallyDampedSmoothing` "smoothing time is the lag when tracking constant motion" [S3] |
| **Yaw follows the direction of travel** with a gentle delay, and **fades in with speed**: no swinging while parking or turning on the spot; it chases more at speed | iRacing: the chase camera's direction "is relative to the direction of travel of the focus car" [S4]; GTA V and Forza Horizon style trailing camera: "tracks the vehicle's real heading instead of steering input … fades in with speed, so there is no swinging while parking or pivoting in place" [S5]; GTA V's own vehicle camera "unlocked at low speed … chases more actively as speed increases" [S6]; PhysX follow-velocity cameras fall back to the body's heading below a minimum speed [S7] |
| **Pitch and roll do not follow the suspension or bumps**; the height is tied to the road, the camera aims at a point above the middle of the car, so the car's own bounce shows while the world stays still | iRacing chase camera: "height … locked relative to the track", aims "at a fixed point above the track near the center of the focus car" [S4]; parenting a camera to the car "makes it shake with the car" [S8]; Unreal's vehicle spring arms switch off pitch and roll inheritance [S9] |
| **No bobbing, shake or tilt** the player did not cause (motion sickness); offer to switch them off | Xbox Accessibility Guideline 117 [S10]; Game Accessibility Guidelines: camera shake, tilt and bob are common causes; provide an option [S11] |
| **Look-around springs back** after a pause | GTA V third-person vehicle camera re-centres after a short delay; mods expose "auto-recenter (view drifts back to center if you stop looking around)" [S12] |
| **Speed shows through a small FOV kick and a little pull-back**, not through shaking | BeamNG orbit camera "FOV Speed Factor" [S13]; arcade chase cameras add dynamic FOV and acceleration kicks [S14] |
| **Collision: hold, damp, prefer raising**: ignore brief occluders, hold the pulled-in position for a while (avoid pumping), ease in and out with separate damping, a camera radius, optionally keep the height or the distance instead of pulling straight in | Cinemachine Deoccluder: Minimum Occlusion Time, Smoothing Time, Damping vs Damping When Occluded, Camera Radius, strategies Pull Forward / Preserve Height / Preserve Distance [S15] |
| **Predict instead of react**; hard constraints make motion artefacts, damped springs do not; collision probes can be amortised over frames | Haigh-Hutchinson, Real-Time Cameras (Navigation and Occlusion) [S16] |
| **FOV**: chase cameras in racing games sit around 50–55° vertical at 16:9 (≈ 80–85° horizontal); wider screens widen the view (Hor+) | Forza Motorsport chase cameras default 53 (range 25–65) [S17]; "55° vertical … 85 horizontal at 16:9 … fairly typical for a racing game" [S18]; Hor+ is the common scaling [S19] |

Two-wheeler games (RIDE, MotoGP) and GTA's bikes keep the chase view level while the rider leans; only the
first-person view leans with the bike [E]. Bus and truck simulators (OMSI, Bus Simulator, ETS2 "camera 2") put the
chase camera high behind the roof so the driver sees over the vehicle into the street [E][S20].

## 3. The behaviour we build (every class)

1. **Exact springs.** Every moving quantity is a critically damped spring stepped with the closed-form solution for a
   goal that moves linearly during the frame (`CameraSpring`), so the camera's place relative to the vehicle does not
   depend on how the frame time is cut up. `ω = 2 / smoothTime`.
2. **One clock.** `CameraClock` smooths the raw *unscaled* frame time (10 % running average plus a 10 % repayment of
   what it left out: the game time still equals real time within a few ms; a 100 ms hitch is spread over about 0.5 s;
   a 16.7/33.3 ms alternation becomes an even 25 ms) and the explorer *and* the camera advance by that same time.
3. **The followed point** is the middle of the body on the ground (a vehicle's ground point is under its rear axle):
   across the ground a light spring (0.07–0.14 s, a slight trail at speed); up and down a spring of 0.2–0.4 s with
   the climb rate (averaged over 0.6 s) fed forward, so a kerb or a seam is taken in over about a third of a second
   while a hill is followed without lag. Climb rates under 5 cm/s are not fed forward (fully from 15 cm/s): the
   seams and patches of a road average to a few cm/s that would otherwise leak into the view.
4. **Yaw** follows the body's heading with 0.30 s (two-wheelers) to 0.60 s (bus): a steady turn of 30°/s shows
   9–18° of the inside of the turn, then the camera re-centres. It does not follow below 0.8 m/s and follows fully
   from 3 m/s. In a lane it follows 40 % tighter (the boom swings less into the walls).
5. **Pitch and roll** are the rig's: no suspension, no bumps, no lean. Only an aircraft's view banks with 20 % of its
   bank (0 with Reduce motion), and its boom tilts with half its flight-path angle.
6. **Speed** (look-ahead, FOV kick, pull-back) reads a speed smoothed over 0.8 s.
7. **Look-around** returns 1.4 s after the player lets go, over about 0.55 s (critically damped).
8. **Boom** (`ChaseBoom`): a 0.3 m sphere swept from the pivot; **never inside or behind a wall** (hard limit); a
   second sweep from where the pivot and the boom's yaw will be in 0.35 s lets it shorten early and smoothly (0.16 s
   spring) instead of snapping; it waits 0.35 s and needs 0.3 m of room to spare before it lengthens again (0.45 s);
   when cut short by more than 15 % it first tries steeper booms (four 11.25° steps, 45° at most) and rises over a low wall, a parked truck
   or a railing; the lift drops only after 0.6 s of room. Lane mode (walls within 3 m both sides) enters after 0.5 s,
   leaves after 2.5 s, blends over 1.6 s (eased) and changes the boom by only +4° and −10 %. With no room at all the
   overhead fallback rises over the rider and looks down the lane.
9. **Ground**: the camera is lifted smoothly (0.08 s up, 0.4 s down) to stay 0.8 m above the ground under it (the
   level under the camera: under a flyover, the road below), and never comes within 0.45 m of it.

## 4. Framing targets (what a player sees)

Composition read from GTA V, Forza Horizon and Watch Dogs driving footage and turned into numbers [E]: the vehicle
centred, its tail on the ground at about a quarter of the screen height (NDC −0.55), the road running to a horizon
at about two thirds of the screen (NDC +0.26 to +0.30), the camera a little above the roof. Portrait (9:19.5) keeps
the vehicle in the lower third and the horizon at about NDC +0.15, so the tall screen shows the road ahead rather
than the sky.

| Class (body) | Landscape: tail NDC / body height share / horizon NDC | Portrait: tail / share / horizon |
|---|---|---|
| Walk (1.75 m) | −0.58 / 0.33 / +0.30 | −0.46 / 0.14 / +0.15 |
| Bicycle (1.7 m with rider) | −0.56 / 0.31 / +0.30 | −0.48 / 0.12 / +0.15 |
| Scooter, motorbike (1.72 m with rider) | −0.56 / 0.29 / +0.30 | −0.48 / 0.115 / +0.15 |
| Car, taxi (1.5 m) | −0.55 / 0.26 / +0.28 | −0.48 / 0.095 / +0.15 |
| SUV, micro, tempo (1.95 m) | −0.56 / 0.28 / +0.28 | −0.48 / 0.10 / +0.15 |
| Bus (3.2 m), truck (3.3 m) | −0.58 / 0.30 / +0.26 | −0.50 / 0.12 / +0.13 |
| Tractor (2.1 m) | −0.56 / 0.28 / +0.26 | −0.48 / 0.11 / +0.15 |

## 5. Numbers per class (`CameraRigTable`)

Boom from a pivot over the body's middle; camera height and distance behind the tail follow (flat ground, rest).
FOV: landscape keeps a minimum **vertical** FOV (46° drive, 48° walk/heavy: ≈ 85–90° horizontal on a 20:9 phone,
74–76° at 16:9), portrait a minimum **horizontal** FOV (55° walk, 62° drive, 64° bus/truck; the vertical opens to
about 100°).

| Class | Landscape boom / pitch / pivot | camera height / behind tail | Portrait boom / pitch | camera height | Foot NDC rest → fast | Follow / pos / height (s) | Kick | Pull-back |
|---|---|---|---|---|---|---|---|---|
| 5.1 Walk | 6.0 m / 12.5° / 1.0 | 2.3 / 5.7 m | 5.6 m / 27.5° | 3.6 m | −0.55 → −0.59 | 0.9 / 0.08 / 0.20 | — | — |
| 5.2 Bicycle | 7.2 / 10.5° / 1.0 | 2.3 / 6.2 | 6.6 / 28° | 4.1 | −0.46 → −0.52 | 0.32 / 0.07 / 0.24 | +3° 3→8 m/s | 0.6 m at 8 m/s |
| 5.3 Scooter, motorbike | 7.8 / 11° / 1.0 | 2.5 / 6.7 | 7.0 / 28.5° | 4.3 | −0.46 → −0.54 | 0.30 / 0.07 / 0.25 | +6° 60→90 km/h | 1.0 m at 20 m/s |
| 5.4 Car, taxi | 8.5 / 9° / 1.0 | 2.3 / 6.5 | 8.1 / 25.5° | 4.5 | −0.37 → −0.45 | 0.40 / 0.09 / 0.28 | +4° 8→25 m/s | 1.2 m at 22 m/s |
| 5.5 SUV, micro, tempo | 10.3 / 10° / 1.1 | 2.9 / 7.9 | 10.0 / 26.5° | 5.6 | −0.38 → −0.46 | 0.45 / 0.10 / 0.30 | +3° | 1.2 m |
| 5.6 Bus | 16.9 / 9° / 1.8 | 4.4 / 11.5 | 15.9 / 22° | 7.8 | −0.32 → −0.39 | 0.60 / 0.14 / 0.38 | +2° | 1.5 m at 16 m/s |
| 5.7 Truck, tipper, tanker | 15.6 / 10° / 1.8 | 4.5 / 11.3 | 14.8 / 24.5° | 7.9 | −0.37 → −0.44 | 0.55 / 0.13 / 0.36 | +2° | 1.5 m |
| 5.8 Tractor | 10.0 / 10.5° / 1.2 | 3.0 / 8.1 | 9.4 / 27.5° | 5.5 | −0.42 → −0.47 | 0.50 / 0.12 / 0.32 | — | 0.5 m |
| 5.9 Aircraft (length L) | 7 + 1.15 L / 6.5° / 0.18 L | Cessna 172: 3.4 / 12.5 | ×1.08 / 16° | | −0.28 | 0.55 / 0.12 / 0.25 | +4° 40→80 m/s | 0.08 L |
| Passenger | the driver rig of the vehicle ridden × 1.2, no look-ahead, kick or pull-back | | | | | ≥ 0.6 | — | — |

Alternative views keep working on top of these (`CameraViews`): far (×1.45, +5°), over the shoulder, low cinematic,
high chase, and the mounted handlebar, bonnet, interior, driver's seat and passenger window views (the mounted views
are unchanged and still follow their own share of the body's roll).

## 6. Verification thresholds (`CharactersCameraRideTests`)

Rides recorded on the committed `kathmandu_core` sample through the world's own ground query (bridge decks, solids
trimmed to the road corridors): a car from Thamel Chowk to Ratna Park (the real `ArcadeVehicle` with the road
magnet, 1.5 m left of the centre line), a scooter from Indra Chowk through Garcha Marg and the Himal Marg galli to
Asan, and a bus from Maitighar east along the Baneshwor road (driven the same way, on the centre line: 1.5 m left of
it a solid at 85.32753, 27.69324 stops the bus, and the first choice, Ratna Park to Maitighar along Kantipath, is
blocked for a bus near Ratna Park; both are open issues for the ride package).
The autopilot holds a constant cruise speed (9, 6 and 8 m/s) except where a bend needs less. Frames on a phone clock (33.3 ms ± 4 ms, a
16.7 ms frame every 13, a 50 ms hitch every 97). Landscape 20:9 and portrait 9:20. Thresholds (after 1.5 s):
position jerk p95 ≤ 60 m/s³ and p99 ≤ 200 m/s³; angular velocity on straight roads (heading within 1.5° for 1.5 s)
max ≤ 6°/s and p95 ≤ 4°/s; angular velocity ≤ 90°/s anywhere; angular acceleration p99 ≤ 250°/s²; the camera's
offset from the vehicle never jumps more than 0.5 m in a frame; no roll; the boom never shortens by more than
0.25 m in a frame in the old-core lanes, and the camera is never inside a house.

## 7. Before and after (measured)

`CharactersCameraRideTests` (rides recorded on the sample, 150 s each, 4 601 frames on the phone clock; the old rig
is `CharactersCameraLegacy.cs`, landscape and its old portrait rows). Numbers copied at commit time; "turn on
straights" is the view's angular velocity while the vehicle's heading stays within 1.5° for 1.5 s; "worst one-frame
jump" is the largest change of the camera's offset from the vehicle in one frame (in a turn the boom swings with the
yaw, so a few decimetres there are expected).

| Ride | Orientation | Camera | Jerk p95 / p99 (m/s³) | Turn on straights max / p95 (°/s) | Turn max (°/s) | Ang. acc. p99 (°/s²) | Worst one-frame jump (m) | Boom step (m) | Roll (°) |
|---|---|---|---|---|---|---|---|---|---|
| Car, Thamel to Ratna Park, 9 m/s | landscape | before | **2 314** / 2 501 | **513.5** / 1.8 | **514** | 111 | **3.20** | 0.00 | 0 |
| | | after | 25 / 49 | 3.4 / 1.8 | 75 | 114 | 0.39 | 0.01 | 0 |
| | portrait | before | **2 319** / 2 505 | **315.8** / 1.8 | **316** | 126 | **2.35** | **2.29** | 0 |
| | | after | 26 / 54 | 4.1 / 1.7 | 75 | 103 | 0.34 | 0.04 | 0 |
| Scooter, Indra Chowk to Asan, 6 m/s | landscape | before | **1 528** / 2 168 | **45.5** / **41.1** | 73 | **316** | **1.72** | **1.71** | **3.2** |
| | | after | 32 / 74 | 5.3 / 3.8 | 72 | 107 | 0.43 | 0.17 | 0 |
| | portrait | before | **1 544** / 2 219 | **44.7** / **40.6** | 78 | **311** | **2.46** | **2.46** | **3.3** |
| | | after | 29 / 52 | 4.8 / 3.6 | 72 | 112 | 0.34 | 0.03 | 0 |
| Bus, Maitighar to Baneshwor, 8 m/s | landscape | before | **1 835** / 2 131 | 3.4 / 1.7 | 30 | 137 | 0.30 | 0.00 | 0 |
| | | after | 23 / 46 | 3.4 / 1.7 | 31 | 117 | 0.32 | 0.01 | 0 |
| | portrait | before | **1 836** / 2 131 | 3.4 / 1.7 | 30 | 111 | 0.32 | 0.00 | 0 |
| | | after | 22 / 45 | 3.4 / 1.7 | 31 | 111 | 0.28 | 0.01 | 0 |

Bold: over the §6 thresholds. The old camera fails every ride on jerk (its `SmoothDamp` lag changes with every frame
time); the car's 513°/s on a straight is the ground clamp lifting the camera 3.2 m in one frame where the
camera's ground sample landed on a higher surface; the scooter's 41°/s p95 on straight lanes is lane mode switching
on and off at every gap between houses (+15°/−15 % in 0.5 s), with 1.7 m boom snaps and the lean's 3° of roll. The new
camera passes every threshold in both orientations; its turn rate on straights comes from the gentle lane-mode blend
and the boom rising for eaves (the scooter's p95 of 3.8°/s is the closest to its limit).

Other measurements (same tests):

* **Frame clock.** The car's first 40 s recorded on the phone clock and at an even 60 fps: the camera's offset from the
  car differs by at most **0.6 cm** (old camera: **10.1 cm**). An exact spring over a moving goal on the phone clock
  stays within 0.2 cm of a 600 Hz reference (Unity `SmoothDamp`: 17.1 cm). `CameraClock` turns ±2.3 ms of frame jitter
  into ±0.76 ms and drifts 2.6 ms from real time in 67 s; a 100 ms hitch never shows as a frame over 60 ms.
* **Road noise.** A scooter at 8 m/s on a surface jumping ±4 cm every 7 cm and leaning ±20°: the camera moves up and
  down by 2.7 cm in all, the view's pitch by 0.00°, no roll.
* **Lanes.** On the scooter ride through the old core the boom never changes by more than 0.17 m in a frame
  (landscape; 0.03 m portrait), the camera is never inside a house, and a house front comes between the rider and the
  camera in fewer than 0.5 % of the frames.
* **Framing** (`EveryClassFramesItsBodyAsTheBriefSays`): every class lands its tail, body share and horizon on the
  §4 targets within 0.06 / 0.05 / 0.07 NDC in landscape at 20:9 and 16:9 and in portrait.

Previews (outside the repository): `/home/user/wt/previews/camera/playtest5/` has, per ride, `sheet_<ride>.png` with the
same frame of the same recorded ride seen by the old and the new camera (a straight at cruise, the sharpest turn and,
for the scooter, the narrowest lane; landscape 20:9 and portrait), rendered from the streamed street with the vehicle
on its recorded pose (`CharactersCameraPreviewDumps`), and `trace_<ride>.png`: the view's turn rate, the camera's height
and distance behind the vehicle and the boom over the whole ride, plus a 4 s zoom of the frame-to-frame wobble.

## 8. Sources

* [S1] R. Juckett, "Damped Springs", https://ryanjuckett.com/damped-springs
* [S2] Unity Scripting API, Mathf.SmoothDamp / Vector3.SmoothDamp (Game Programming Gems 4, 1.10, T. Lowe),
  https://docs.unity3d.com/ScriptReference/Mathf.SmoothDamp.html
* [S3] Unreal Engine API, FMath::CriticallyDampedSmoothing and SpringMath,
  https://dev.epicgames.com/documentation/unreal-engine/API/Runtime/Core/FMath/CriticallyDampedSmoothing
* [S4] iRacing Camera Tool, chase camera, https://support.iracing.com/support/solutions/articles/31000157467
* [S5] Farming Simulator 25 "Trailing Camera" (GTA V / Forza Horizon style), https://fs25.net/trailing-camera-v2-0-2/
* [S6] "Custom Camera V Plus" (GTA V), https://ru.gta5-mods.com/scripts/custom-camera-v-plus/download/40741
* [S7] NVIDIA Omniverse PhysX cameras (follow velocity, minimum speed),
  https://docs-prod.omniverse.nvidia.com/extensions/latest/ext_physics/simulation-control/ext_physics-cameras.html
* [S8] three.js forum, "Smooth chase camera for an object", https://discourse.threejs.org/t/solved-smooth-chase-camera-for-an-object/3216
* [S9] Unreal Engine USpringArmComponent (bInheritPitch, bInheritRoll, camera lag),
  https://dev.epicgames.com/documentation/en-us/unreal-engine/API/Runtime/Engine/USpringArmComponent
* [S10] Xbox Accessibility Guideline 117, https://devdocs.xbox.com/gaming/accessibility/xbox-accessibility-guidelines/117
* [S11] Game Accessibility Guidelines, "Avoid or provide option to disable any difference between controller movement
  and camera movement", https://gameaccessibilityguidelines.com/avoid-or-provide-option-to-disable-any-difference-between-controller-movement-and-camera-movement/
* [S12] "Driver's Eye" (GTA V), https://www.gta5-mods.com/scripts/driver-s-eye
* [S13] BeamNG forum, "Camera Settings & Revamped Chase Camera", https://www.beamng.com/posts/1822642
* [S14] "BACC – Better Arcade Chaser Camera" (Assetto Corsa, NFS-style), https://www.overtake.gg/downloads/bacc-forza-horizon-better-arcade-chaser-camera-for-csp.49738/
* [S15] Cinemachine 3.0 Deoccluder, https://docs.unity3d.com/Packages/com.unity.cinemachine@3.0/manual/CinemachineDeoccluder.html
* [S16] M. Haigh-Hutchinson, "Real-Time Cameras – Navigation and Occlusion",
  https://www.gamedeveloper.com/design/real-time-cameras---navigation-and-occlusion
* [S17] Forza Motorsport camera and FOV settings, https://forums.forza.net/t/camera-and-fov-settings-in-forza-motorsport/619113 ;
  https://seekingtech.com/how-to-change-the-field-of-view-in-forza-motorsport/
* [S18] Steam community, Forza Horizon 5 FOV discussion, https://steamcommunity.com/app/1551360/discussions/0/3791505882413038946
* [S19] "Field of view in video games", https://en.wikipedia.org/wiki/Field_of_view_in_video_games
* [S20] Euro Truck Simulator 2 camera discussions (camera 2: chase bound to the truck),
  https://steamcommunity.com/app/227300/discussions/0/3194736442558252181
