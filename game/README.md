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

The Explore slice of M1 wave 1: 1:1 Kathmandu streamed around a scooter or a walker, search, a route to follow,
portrait and landscape. It runs on the committed `kathmandu_core` sample (Swayambhunath to Boudhanath); once the
full valley is imported (`python build.py --region kathmandu_valley` in `pipeline/`, then **Ghumante → Import Region
Pack…**), Explore opens `kathmandu_valley` instead.

**Set up once (and after pulling M1 changes)**

1. Open `game/` in Unity 6000.3.25f1 and run **Ghumante → Project Setup**. Besides the settings it copies the sample
   region into `Assets/StreamingAssets/Regions/`, creates the world materials, and rebuilds
   `Assets/Ghumante/Scenes/Bootstrap.unity` if it predates the Explore screen (the scene is generated; Play mode in the
   editor also finds the Explore screen without this, but player builds need it saved in the scene).
2. Open `Assets/Ghumante/Scenes/Bootstrap.unity`. Optional: set **Start Screen = Explore** on the *Ghumante* object to
   skip the menu.

**Ride from Thamel to Boudhanath (keyboard, Game view)**

1. Press **Play**, then click **Explore** on the main menu. The loading overlay shows "Opening Kathmandu Core…", then
   "Drawing the streets around Thamel…" while the tiles around the spawn stream in (a few seconds).
2. You start on the red scooter on a street in Thamel, facing along it ("Namaste from Thamel!"). The pill at the top
   left names the place, the compass beside the buttons points north, the speedometer is at the bottom.
3. Ride: **W** (or up) throttle, **A / D** steer, **S** brake then reverse, **Space** brake, **Shift** boost. The mouse
   wheel zooms, a right-drag looks around (it swings back behind you). The chip under the speed names the surface; a
   small white dot means OpenStreetMap does not say and the pipeline guessed it.
4. Press **/** (or click the magnifier), type **Boudha**. The first result is *Boudhanāth Stupa* (Stupa, about 4.9 km
   away). Click **Ride there**.
5. A yellow chevron ribbon appears on the roads and the banner shows "To Boudhanāth Stupa", the distance left and the
   ETA (about 7.5 km and 9 minutes), with an arrow pointing along the route ahead. Follow the ribbon east. Leave it for
   a few seconds and it finds a new way.
6. Within about 40 m of the end: confetti, "You made it to Boudhanāth Stupa!" and a Success haptic (in the editor the
   debug box, top left under the place name in Explore, shows `haptic: Success (editor - not felt)`).
7. Press **E** to hop off (at speed it brakes first) and walk around the stupa: W walks away from the camera, A/D and S
   walk left, right and towards it; Shift sprints. **E** again hops back on (a scooter left far away rolls up to you).
8. **Esc** pauses: Resume, Settings (Vibration, Reduce motion, Language: switch to नेपाली to see Nepali place names
   and numerals), **Main menu** (closes the world and frees its memory).

Shortcut while testing: each search result also has **Teleport** (editor and development builds only), which drops
you on the nearest road to it. Other debug keys (track C): **F3** free-fly camera, **T** fast time, **[ ]** an hour
back/forward, **P** pause the clock; the HUD's debug slider (top right) sets the time of day.

**Gamepad**: left stick steers (walks), right trigger throttle, left trigger brake/reverse (squeezed while the right
trigger is held it brakes; the same goes for W + S and the touch Go + Brake), A walk/ride, B brake, X boost, Y search
(D-pad and A pick a result), Start pause (D-pad and A reach Settings and Main menu; Start also closes Search and
Settings), shoulders zoom, right stick look. Touch controls hide as soon as a keyboard or gamepad drives.

**Portrait, landscape and touch (Device Simulator)**: **Window → General → Device Simulator**, pick an iPhone 15 or a
Pixel and press Play. Mouse clicks act as touches and the touch controls appear:

* Landscape (two thumbs): drag in the left part of the screen for the floating stick (steering), hold **Go** and
  **Brake** on the right.
* Portrait (one thumb): press and hold anywhere in the lower part of the screen to throttle and slide sideways to steer;
  **Brake** (bottom left) brakes, then reverses. On foot, a stick appears wherever your thumb lands in the lower part.
* Drag on the open world to look around. Rotate with the simulator's rotate buttons, also mid-ride: the camera rises
  and pulls back in portrait and blends there in 0.3 s, and the HUD re-flows.

If Explore says *No map installed yet*, run **Ghumante → Project Setup** (or **Import Region Pack…**) and press Play
again. What to report: anything that feels wrong about the scooter, the camera, the HUD in either orientation, or the
route; the debug box shows fps, memory and streaming counters.
