# Ghumante: Unity project

Unity **6000.3.25f1** (Unity 6.3 LTS, the current LTS line; 6.0 LTS support ends 2026-10-16) with URP 17.3. See `docs/ARCHITECTURE.md` section 7 for the runtime design.

## First open

1. Install Unity 6000.3.25f1 with Android Build Support (OpenJDK, Android SDK & NDK) and iOS Build Support.
2. Open `game/` in Unity Hub. Only `ProjectSettings/ProjectVersion.txt` is committed; Unity generates the rest.
3. Run **Ghumante → Project Setup**. It applies every player setting from code (ARCHITECTURE.md 11), creates the URP assets for the Low/Medium/High quality levels, the PanelSettings asset and the Bootstrap scene, and enables UI Toolkit's Advanced Text Generator (see `Assets/Ghumante/UI/README.md`).
4. Commit what Unity and ProjectSetup generated: all `.meta` files, `ProjectSettings/*.asset`, `Packages/packages-lock.json`, `Assets/Ghumante/Settings/**`, `Assets/Ghumante/Scenes/Bootstrap.unity`. From then on GUIDs are stable for everyone.
5. Press Play in `Assets/Ghumante/Scenes/Bootstrap.unity`: the animated main menu appears (what to try: `Assets/Ghumante/UI/README.md`, "Motion"). The Devanagari TextSpike is behind **Text test** in the bottom bar, or set **Start Screen = Text Spike** on the Bootstrap object.

Batch mode equivalent of step 3: `Unity -batchmode -quit -projectPath game -executeMethod Ghumante.EditorTools.ProjectSetup.ApplyFromCommandLine`.

## What ProjectSetup applies

| Setting | Value |
|---|---|
| Company / product | Ghumante / Ghumante |
| Application id | `GHUMANTE_BUNDLE_ID` env or `-ghumanteBundleId` flag, default `com.ghumante.game` (placeholder) |
| Android | min API 29 (Android 10), target API 36 (Google Play requirement since 2026-08-31), IL2CPP, ARM64 only, Vulkan then OpenGL ES 3, ASTC, GameActivity, frame pacing |
| iOS | target iOS 16.0, Metal, IL2CPP, device SDK; signing is done by fastlane |
| Both | Linear colour space, incremental GC, managed stripping Medium, engine code stripping, .NET Standard API level; auto-rotation to portrait, landscape left and landscape right (not upside-down), default orientation AutoRotation (ADR-017) |
| Quality levels | Low / Medium / High, each with its own URP asset and renderer (budgets from ARCHITECTURE.md 10); Bootstrap picks one from the device tier |
| Editor | Force Text serialization, visible meta files |
| UI textures | `UiTextureImportRules` (AssetPostprocessor) imports everything under `Assets/Ghumante/UI/` as Sprite, no mipmaps, Read/Write off, ASTC 4x4 on Android and iOS (ARCHITECTURE.md 10) |

## Assemblies

One asmdef per module (ARCHITECTURE.md 7.1), all referencing `Ghumante.Core` (written separately, `noEngineReferences`). `Ghumante.DebugTools` compiles only with `DEVELOPMENT_BUILD || UNITY_EDITOR` and installs itself, so nothing references it. `Ghumante.EditorTools` is editor-only. `Ghumante.Tests.EditMode` holds the EditMode tests CI runs.

## Building

* Editor menu: **Ghumante → Build → Android App Bundle / iOS Xcode Project** (outputs under `game/Builds/`).
* Command line / CI: `-executeMethod Ghumante.EditorTools.BuildScript.BuildFromCommandLine` with GameCI's arguments (`-customBuildTarget`, `-customBuildPath`, `-buildVersion`, `-androidVersionCode`, keystore flags). See the class comment and `.github/workflows/unity.yml`.
* Store uploads: `game/fastlane/Fastfile` (`bundle exec fastlane ios beta`, `bundle exec fastlane android internal`). Secrets: `docs/CI_SECRETS.md`.

## Without Unity

`tools/unity-compile-check/run.sh --audit` compiles all game C# outside Unity and checks every Unity API it uses against Unity 6000.3's reference source (missing, renamed, non-public or `[Obsolete]` members fail). See `tools/unity-compile-check/README.md`.

## Tools (`game/Tools/`, outside `Assets/` so Unity ignores them)

| Script | Purpose |
|---|---|
| `fonts.py fetch / instance / verify` | Download Baloo 2 + Mukta from google/fonts (pinned SHA-256), cut static Baloo 2 Bold/ExtraBold, verify Devanagari coverage and shaping |
| `textspike.py [--check]` | Generate the TextSpike UXML, reference USS and HarfBuzz reference images from `textspike_cases.json` |
| `make_ui_art.py` | Regenerate the placeholder icons and menu background |

## How to play the M1 slice on a Mac

The Explore slice of M1 (wave 2, stage 1): 1:1 Kathmandu streamed around a detailed cartoon explorer who walks, jumps,
namastes and hops on any vehicle, with traffic, crowds, temples and aircraft. It runs on the committed `kathmandu_core`
sample (Swayambhunath to Boudhanath); once the full valley is imported (`python build.py --region kathmandu_valley` in
`pipeline/`, then **Ghumante → Import Region Pack…**), Explore opens `kathmandu_valley` instead.

**Set up once (and after pulling M1 changes)**

1. Open `game/` in Unity 6000.3.25f1 and run **Ghumante → Project Setup**. Besides the settings it copies the sample
   region into `Assets/StreamingAssets/Regions/`, creates the world materials, and rebuilds
   `Assets/Ghumante/Scenes/Bootstrap.unity` if it predates the Explore screen (the scene is generated; Play mode in the
   editor also finds the Explore screen without this, but player builds need it saved in the scene).
2. Open `Assets/Ghumante/Scenes/Bootstrap.unity`. Optional: set **Start Screen = Explore** on the *Ghumante* object to
   skip the menu.

**Keys (Game view)**

| Do | Keyboard | Gamepad |
|---|---|---|
| Walk / steer | W A S D or the arrows (walking is relative to the camera) | left stick |
| Run, sprint | push further; **Shift** sprints (not inside temple compounds) | X |
| Jump | **Space** on foot (apex 1 m) | A with nothing to hop on |
| Hop on / Hop off | **E** (tap) | A |
| Ride as passenger / hail a taxi | hold **E** (0.35 s next to a stopped bus, taxi or micro; 0.4 s with nothing near hails the nearest taxi) | hold A |
| Throttle, brake / reverse, brake | **W**, **S**, **Space** | right trigger, left trigger, B |
| Horn (bicycle: bell) | **H** (hold for a long horn) | left stick press |
| Namaste | **N** | D-pad up |
| Call your vehicle (garage whistle) | **G** | D-pad down |
| Ring the stop bell (passenger) / a shrine bell (in a compound) | **B** | D-pad right |
| Search, pause | **/**, **Esc** | Y, Start |
| Next camera angle (per vehicle class, remembered) | **C** | right stick press |
| Camera | wheel zooms, right-drag looks | shoulders zoom, right stick looks |

The first time you play, the explorer gets a fresh look (a dhaka topi, a dhaka jacket over jeans, sneakers and a
daypack; the skin swatch is random but never #1) and a starter garage (scooter, bicycle, small hatchback). Both are
saved as `player.appearance` and `player.garage` and come back next time.

**1. Walk around Basantapur**

1. Press **Play**, click **Explore**. You start on foot in Thamel ("Namaste from Thamel!") with your red scooter
   parked beside you. The walking camera sits closer in portrait (6.8 m) than before.
2. Press **/**, type **Basantapur** (or *Kathmandu Durbar Square*), click **Teleport** (editor and development builds;
   in a release build use **Ride there** and follow the ribbon on foot or on the scooter).
3. Walk around the square: the gait plants each foot, the topi wobbles, footsteps follow the ground (brick, stone,
   asphalt). Stand still for 6 s and the explorer fidgets (looks around, stretches, tugs the topi). Press **N** for a
   namaste (palms at the chest, a small bow). **Space** jumps.

**2. Enter a temple compound**

1. Walk onto the square or into a temple courtyard (Taleju's plinth steps, Kasthamandap, Kumari Ghar's courtyard). A
   card appears ("A sacred place: walk gently", or the site's rule such as "Shoes off here, please"), sprint and comic
   fidgets switch off (only look-around, shifting weight and hands together), and nothing can be ridden in here: the
   chip reads **Vehicles rest outside**, and the garage whistle is refused until you are back outside the gate.
2. Press **B** (on touch: the Action button, which now reads **Ring bell**) to ring a shrine bell (at most once every
   3 s, three times per visit).
3. Ride up to a compound on the scooter, at any speed: it brakes so that it stops at the edge ("Vehicles rest
   outside"), reversing too; keep pushing at the edge and it parks itself and you hop off. A road that only bends past
   a compound never stops you. A bicycle may cross the open Durbar squares, never a compound.

**3. Take a bus**

1. Teleport to **Ratna Park** (or any bus stop on a route). Green city buses, cream minibuses, white micros and Safa
   tempos run on the real routes; one stops every few minutes with its doors open.
2. Walk up to the open front-left door: the small **Ride as passenger** button appears (or hold **E**). You climb in
   and sit; the camera orbits a little wider than the bus's own driving camera and the chip says "Ring the bell to get
   off".
3. Press **B** (or the **Stop** button): "Ding! Stopping at the next stop." The bus stops at its next stop and you hop
   down beside it (never inside a wall or the bus).
4. Taxis: with nothing near, hold **E** for 0.4 s: the explorer waves and the nearest taxi within 150 m pulls over
   within 30 m. Hop in at the left rear door, ride, press **B** or **E** and it pulls over to let you out.

**4. Drive a car round a roundabout**

1. Press **G** (or the garage button in the top bar): your scooter drives up from out of view. Press **G** again
   while it stands beside you to call the next garage vehicle instead (bicycle, then the small hatchback). Or look for
   a parked car, van, truck or bus with a **green key tag** (a diamond on the roof or above the handlebar): that is
   the community fleet, free to borrow; it
   goes home by itself 10 minutes after you leave it or once you are 150 m away.
2. Walk to the driver's door: the right side (Nepal drives on the left, cars are right-hand drive; a wall there sends
   you round). **E** hops in (0.9 s for a car, 1.4 s for a truck; any move key in the first 60% steps back out).
3. Teleport (or drive) to **Maitighar Mandala** and go round it clockwise, keeping left. Each class has its own
   handling and camera: a bus sits 16.5 m back and swings wide, a scooter leans, a bicycle sways and stops pedalling
   uphill. **H** honks (the bicycle rings a bell). Cows on the road slow you to 5 km/h within 4 m and stop you
   gently at 1.6 m; they keep chewing.
4. **E** hops off: at speed it brakes first ("Slowing down to hop off…"); buses and trucks stop fully.

**5. Watch a plane land at TIA**

1. Teleport to **Tribhuvan International Airport** and walk or ride to the west fence near Sinamangal. The sample
   (south edge 27.690° N) covers the aprons and the northern part of the runway; with the valley pack you can also
   stand in Koteshwor under the approach to runway 02.
2. From 06:00 to midnight game time a movement comes every few real minutes: ATRs and jets descend over Koteshwor at
   50-100 m, touch down and roll out towards you; departures climb out over Boudha; helicopters lift off the domestic
   apron. Use the debug slider (top right) or **T** to run time faster. Airside is fenced and not drivable.

**Portrait, landscape and touch (Device Simulator)**: **Window → General → Device Simulator**, pick an iPhone 15 or a
Pixel and press Play. Mouse clicks act as touches:

* On foot: a floating stick wherever the thumb lands (portrait: the lower area left of the button column; landscape:
  the left side), the big yellow **Action** button (Jump, turning green **Hop on** when a seat is offered; hold it to
  ride as a passenger or hail a taxi), **Namaste** beside it, **Ride as passenger** when a bus, taxi or micro offers a
  seat, the garage button in the top bar.
* In a vehicle: portrait holds anywhere in the lower-left area to go and slides to steer, **Brake** bottom left,
  **Horn** and **Hop off** in the right column; landscape steers with the left stick and has **Go**, **Brake**,
  **Horn** and **Hop off** on the right.
* Riding along: **Stop** (the bell) and **Hop off**; no steering.
* Drag on the open world to look around; rotate mid-ride and the camera blends in 0.3 s. The camera button in the top
  bar (a little camera, left of Search) cycles the camera angles.

If Explore says *No map installed yet*, run **Ghumante → Project Setup** (or **Import Region Pack…**) and press Play
again. Other debug keys (track C): **F3** free-fly camera, **T** fast time, **[ ]** an hour back/forward, **P** pause
the clock. What to report: anything that feels wrong about the character, a vehicle class, a camera, the HUD in
either orientation, or the passenger and garage flows.

## The detail pass on a Mac (camera, HUD and routes)

The owner's detail-pass feedback (`docs/W2_DETAIL_CONTRACT.md` §0) asked for a camera that is not blocked when backing
up in narrow streets or by houses, several camera angles per vehicle, buses, direction info that does not cover the
view, cars that keep off streets they cannot use, proper bridges and flyovers, and detailed roundabouts. The camera, HUD
and route parts live in `Characters/Cameras`, `UI/Hud`, `UI/Screens` and `App/Explore`; the bridges, flyovers,
roundabouts and buses come from their own packages and are checked here from the player's seat.

**Set up**: pull, open `game/` in Unity 6000.3.25f1, run **Ghumante → Project Setup**, open
`Assets/Ghumante/Scenes/Bootstrap.unity`, press **Play**, click **Explore**. Steps 1 to 6 work on the committed
`kathmandu_core` sample; the Kalanki underpass and Jawalakhel need the valley pack (see above). Use **/** and
**Teleport** (editor and development builds) to jump between places.

**1. Camera angles per vehicle (C, right stick press, or the camera button in the top bar)**

1. On foot press **C** three times: *Far chase* (farther and higher), *Over the shoulder* (close behind the right
   shoulder, looking where you look), back to *Near chase*. Each press shows a short toast with the angle's name.
2. **G** for the scooter, **E** to hop on, then **C**: *Near chase*, *Far chase*, *Low cinematic* (low behind the
   rear wheel), *Handlebar* (first person over the handlebar; it leans half as much as the bike). Do the same on the
   bicycle (**G** again while it stands beside you calls the next garage vehicle).
3. In the small hatchback (or a community-fleet car or taxi): *Near*, *Far*, *Bonnet* (on the bonnet), *Driver's
   view* (the driver's eyes). The vehicle meshes have no interior yet, so inside a closed body the camera package shows
   a stand-in cockpit (`Characters/Rides/CockpitMesher.cs`): the dashboard with two gauges in a binnacle, vents, a
   console, the steering column and a wheel that turns with your steering, A-pillars, the windscreen header with the
   rear-view mirror and a little mala, side mirrors, door cards, a sunroof over you, a string of marigolds along the
   screen. The shell's outline is switched off while you sit inside it. Look down (right-drag, right stick) to see the
   wheel. The vehicles package's interior LOD will replace the stand-in.
4. In a bus, truck or tractor (community fleet: the green key tag; or step 5): *High chase*, *Far*, *Driver's seat*.
   The bus and truck driver's seat shows the same stand-in cab: a flat dash with four gauges, the split windscreen
   (bus), wipers, a tinsel garland, prayer flags and a framed picture along the header (tilt the view up, or rotate to
   portrait, to see them), the bus's seat rows with white covers and its grab rails behind you. The tractor is open:
   its own bonnet is the view.
5. Riding along as a passenger (step 5): *Near*, *Far*, *Window seat*: the camera leans out of the window beside
   your seat and looks ahead along the bus's flank (the vehicle you ride is drawn by traffic and cannot be hidden from
   inside, so there is no view from inside it yet). Next to a wall it leans out only as far as the wall allows.
6. Every class remembers its own angle: pick *Handlebar* on the scooter, hop off (walking keeps its own angle), hop
   back on: *Handlebar* again. Stop Play and press Play again: still *Handlebar* (saved as
   `settings.camera_views` in the save).
7. Rotate the Device Simulator (below) mid-ride: every angle keeps the minimum horizontal field of view in portrait and
   landscape (driving 62°, walking 55°, bus and truck 64°, first-person views 68-72°; the vertical FOV stops at 100°).

**2. Reversing in Thamel (the blocked camera)**

1. Teleport to **Thamel Chowk**, take the scooter (**G**, **E**) and ride into one of the narrow side lanes (4.8 m
   between the houses at the narrowest after the detail pass).
2. In the lane the chase camera goes about 15° steeper and 15% closer ("lane mode") and looks over the eaves instead of
   into the walls.
3. Stop, then hold **S** to back up. After 0.3 s the camera rises and shortens so the lane behind you shows under the
   bike; keep reversing for 1 s and it swings round to look along the way you are going. Never inside a house,
   never behind one: where a house is in the way the camera pulls in at once and eases back out over about 0.6 s.
4. Back up towards a corner or a dead end: the camera lifts over low walls and stays above the bike. Ride forwards
   again: the swing undoes at once and the camera settles back behind you.
5. Back right up to the house at the end of a dead-end lane (or stand with your back to a house front) and wait 3 s:
   when the reversing frame lets go there is no room for the camera behind you. It never goes into the house or into
   the rider: it rises straight up over your head (less under a balcony) and looks down the lane ahead, the front of
   the bike at the bottom of the view. Ride or walk about 2 m away from the wall and it comes back down behind you.
6. Walk along a house front in *Over the shoulder*, brushing the wall: the shoulder offset shrinks to nothing, so the
   camera never enters the wall.

**3. Houses in the way**

1. Teleport to **Indra Chowk** and ride the lanes towards Asan. Turn sharp corners: the camera never shows the inside
   of a house or the back of a wall; it pulls in and eases out. What still sits between the camera and you (an eave,
   a strut, a wire) fades out with a dither (look package).

**4. Bridges and flyovers**

1. Teleport to **Kalopul** (the Black Bridge over the Dhobi Khola) and ride across: a real deck with kerbs and
   railings on both sides. Do the same at **Nilo pul (Blue Bridge)** and **Balaju Bridge** (Bishnumati).
2. Ride along the river under a bridge where a road passes beneath: at least 5.5 m of clearance, nothing gets stuck,
   and the camera stays under the deck (it is an obstacle for the boom).
3. Valley pack: teleport to **Kalanki underground bridge** and drive through the underpass and over the junction above
   it; pedestrian overbridges stand where OpenStreetMap maps them. Try every camera angle on a deck.

**5. Buses**

1. Teleport to **Ratna Park bus station**. Buses run on the real routes; hold **E** at the open front-left door to ride
   along. **C** cycles *Near*, *Far*, *Window seat*; **B** rings the bell, you get off at the next stop.
2. Borrow a community-fleet bus (green key tag) and drive it: *High chase*, *Far*, *Driver's seat*.

**6. Roundabouts and routes for each vehicle**

1. Teleport to **Maitighar Mandala** and drive round it (keep left, clockwise): the island, its kerbs and its centre
   piece come from the roundabout package; the camera keeps the island out of its boom.
2. Routes: on the scooter press **/**, type **Indra Chowk**, press **Ride there**. The direction info is now a small
   chip that never covers the road ahead and never sits under a thumb: in landscape under the top bar on the left
   (or right of the speedometer on a tablet; left of it while riding along, when there is no stick), in portrait under
   the top bar. It shows the next turn's arrow and how far it is ("240 m", or "Straight on"), then the distance and
   time left ("2.4 km · 6 min"); off the route it reads "Back to the route" and its arrow points back to it; **×**
   stops the route. Ride up to a corner: the distance counts down to the corner itself ("20 m" when it is 20 m away,
   however many map nodes lead up to it) and turns at the junction, and on a roundabout it points to the middle of
   your way round.
3. Hop into the car (**G** until the hatchback comes, **E**): the route is planned again for the car ("Car route: only
   streets a car fits through.") and goes round by streets a car can use (the GHRG car profile, which leaves out gallis,
   streets under about 3 m, footways, paths, steps and `motorcar=no`). Hop back on the scooter: a new route that may take
   the lanes again ("Scooter route: finding a new way…"). On foot the route uses footways and steps and its time
   is a walking time.
4. Valley pack: **Jawalakhel** roundabout in Patan, and **Ride there** from Thamel to Patan Durbar Square by car and by
   scooter to compare.

**Device Simulator (portrait, landscape, notches)**: **Window → General → Device Simulator**, pick an iPhone 15 (Dynamic
Island) and a Pixel, press Play and ride a route in both orientations. The chip stays inside the safe area (clear of
the notch, the punch hole and the home indicator), clear of the speedometer, the top bar and every touch control,
including the whole floating-stick zone on the left in landscape and the lower stick or drive zone in portrait (the
stick jumps to wherever the thumb lands). It moves to its next slot whenever the controls change: hop from the car
into a bus, hop off, ride along, or plug in a gamepad (the touch controls hide) and watch it settle in the next
frame.

What to report: an angle that frames badly for a vehicle, any frame where the camera is inside or behind a house, the
chip covering anything, or a car route that still enters a lane a car cannot fit.
