# Ghumante UI (UI Toolkit)

All UI is UI Toolkit (ADR-007): Devanagari needs OpenType shaping (conjuncts, reph, reordered i-matra), and in Unity 6 only UI Toolkit's **Advanced Text Generator** (ATG) does that. TextMeshPro and uGUI text are not used.

## Advanced Text Generator: the two switches

ATG must be on in **two** places, otherwise Devanagari renders as unshaped code points (dotted circles, halants between full consonants, ि after its consonant).

1. **Project setting**: *Edit → Project Settings → UI Toolkit → **Enable Advanced Text Generator***. Unity 6.3 has no public scripting API for it. The checkbox is backed by the internal static property `UnityEditor.UIElements.UIToolkitProjectSettings.enableAdvancedText`, serialized as `m_EnableAdvancedText` in `ProjectSettings/UIToolkitProjectSettings.asset` (verified in the Unity 6000.3.25f1 C# reference source). `Ghumante.EditorTools.ProjectSetup` sets it by reflection (`AdvancedTextGeneratorSetting.Enable()`); if a future Unity renames it, ProjectSetup logs a warning with this manual step instead. After the first run, commit `ProjectSettings/UIToolkitProjectSettings.asset`.
2. **Per element**: `-unity-text-generator: advanced;` is set on `.gh-root` in `Styles/Ghumante.uss`, so every screen's tree inherits it. In C# the equivalent is `element.style.unityTextGenerator = TextGeneratorType.Advanced` (not used, so the runtime code also compiles against older reference assemblies).

ATG limits worth knowing (Unity manual, *Enable and use Advanced Text Generator*): it does not support **static** font assets, so fonts are referenced as `.ttf` files and UI Toolkit builds dynamic font assets from them; glyph customisation in font assets is not available.

## Files

| Path | What |
|---|---|
| `Styles/Ghumante.uss` | Design tokens (`--gh-*` custom properties) and components: cream panels, ribbon header, glossy pills (yellow/cyan/green/white/red), round icon buttons, counter pills with "+" |
| `Themes/GhumanteRuntime.tss` | Runtime theme for the PanelSettings asset (imports Unity's default theme only) |
| `Screens/MainMenu.uxml` + `MainMenuScreen.cs` | The animated main menu: living backdrop, title ribbon "Ghumante / घुमन्ते", Explore, Map, Collections, Settings, counters bar, toast, settings sheet (see "Motion" below) |
| `Screens/Explore.uxml` + `ExploreScreen.cs` | The Explore HUD (M1): loading overlay, speed, surface chip, place name, compass, camera-angle button, route chip, Search / Walk-Ride / Menu, touch controls, search sheet, pause panel, the shared settings sheet (see "Explore HUD" below) |
| `Hud/Hud.uss` | HUD styles for both orientations (layout classes `orient-*` and `gh-hud--ride` / `gh-hud--walk`) |
| `Hud/TouchControls.cs`, `Hud/TouchMath.cs` | Touch controls: floating stick, pedals, the portrait one-thumb drive zone, look and pinch on the world; when they show |
| `Hud/SearchSheet.cs` | "Where to?": search as you type, results with kind and distance, Ride there / Teleport |
| `Hud/HudFormat.cs` | Engine-free HUD text: km/h, distances, ETA, Devanagari digits, surface and kind string keys (unit-tested) |
| `Hud/HudToast.cs`, `Hud/HudSlider.cs` | The HUD's toast and the debug time-of-day slider |
| `Screens/SettingsSheet.cs` | Settings: Vibration, Reduce motion, Language, "Feel the haptics"; bottom sheet in portrait, side panel in landscape |
| `Screens/ScreenBase.cs` | Base presenter: localisation, orientation, an animator per screen, `Feel(button, haptic, action)` |
| `Screens/TextSpike.uxml` + `TextSpikeScreen.cs` | The P3 shaping test (generated, see below) |
| `Motion/` | UI motion runtime: `UiAnimator`, `MotionNode`, `PressFeel`, `ParticleBurst`, `LivingBackdrop`, `TiltInput`, `MotionSettings` |
| `TextSpike/` | Its layout USS, generated reference USS and the HarfBuzz reference images |
| `Localization/strings.{en,ne}.json`, `Localizer.cs` | Flat string tables and the M0 dictionary localizer (replaced by the Unity Localization package in M1, same keys) |
| `OrientationWatcher.cs`, `SafeArea.cs` | Portrait/landscape classification from the safe area (orient-* classes, `OrientationChanged`) and safe-area padding |
| `Fonts/` | Baloo 2 (display; static Bold/ExtraBold instances cut from the variable font) and Mukta (body), SIL OFL 1.1, licence files alongside |
| `Icons/` | Placeholder icons, menu backgrounds, the living-menu layers (`bg-*`), clouds, birds, prayer flag and rope, sparkles, puffs, shine and scooter, all drawn by `game/Tools/make_ui_art.py` |

Text elements carry their localisation key in `binding-path` (temporary convention; `binding-path` only drives editor SerializedObject binding, so it is inert at runtime). `Localizer.Apply(root)` fills them and puts `gh-lang-en`/`gh-lang-ne` on the screen root.

## Portrait and landscape (ADR-017, ARCHITECTURE.md 7.10a)

The game rotates live between portrait and both landscapes (ProjectSetup enables AutoRotation; upside-down
portrait is off). Every screen is one UXML; `ScreenBase` hands its `.gh-root` to `OrientationWatcher`, which
classifies the **safe area** (taller than wide = portrait, else landscape), toggles `.orient-portrait` /
`.orient-landscape` and raises `OrientationWatcher.OrientationChanged`. The USS re-flows on those classes
("Orientation" section at the end of `Styles/Ghumante.uss`, and of `TextSpike/TextSpike.uss`):

| Screen | Landscape | Portrait |
|---|---|---|
| Main menu | counters + icon buttons in one top row; hero (ribbon, subtitle) left, menu panel right | counters centred along the top, icon buttons below them on the right; hero in the upper half; menu panel (Explore, Map, ...) at the bottom of the body, i.e. in the bottom third |
| Explore HUD | place pill top-left, compass, camera angle and Search / Walk-Ride / Menu top-right; speedometer and surface chip bottom centre; the route chip under the place pill (beside the speedometer when no thumb zone is there); two-thumb controls: floating stick on the left, Go and Brake pedals on the right | buttons along the top right, place pill under them; route chip under them on the left (above the horizon); speedometer at the top of the thumb zone; one-thumb controls in the bottom 42%: riding, hold anywhere to go and slide sideways to steer, Brake bottom-left; walking, a floating stick wherever the thumb lands |
| TextSpike | case / live / reference in three columns | one block per case, live above reference, column headings hidden; toggles in the bottom bar in both orientations |

PanelSettings uses an orientation-neutral scale (square 1500x1500 reference, match 0.5), so a 20:9 phone is
about 2069x931 panel units in landscape and 931x2069 in portrait and elements keep their physical size
when the device turns. Camera rigs use `Ghumante.World.Cameras.CameraFov.VerticalFromHorizontal` to keep a
minimum horizontal FOV (vertical FOV = 2*atan(tan(h/2)/aspect), clamped to 100 degrees).

Unverified without Unity: the actual layout at each reference resolution. Check in the UI Builder or on
device at 1080x2400 and 2400x1080 (and an iPad, both ways) before M0 sign-off.

## Motion: the living main menu

The product owner asked for a menu that is "animated, interesting and fun", with haptics on phones that
have them (2026-10-05). It follows ARCHITECTURE.md 8 ("bouncy UI tweens") and ASSET_MANIFEST.md 12
("overshoot 1.1, 180-250 ms, squash on press, reduced-motion setting").

### How it works

| Layer | Where | What |
|---|---|---|
| Maths | `Core/Motion` (engine-free, `core-tests/MotionTests.cs`) | `Easing` (Linear, OutCubic, InCubic, InOutSine, OutBack, OutElastic, OutBounce, InBack; exact at 0 and 1), `Spring` (closed-form damped spring: frame-rate independent, cannot blow up), `Tween` / `Stagger` (delay, duration, ease as data), `Wave` (idle loops), `Squash`, `CountUp`, `MotionRandom` |
| Runtime | `UI/Motion/UiAnimator.cs` | One per screen, created by `ScreenBase` on the screen root. Ticked by the **panel scheduler** on every panel update (`schedule.Execute(..).Every(0)`, i.e. once per rendered frame; the tier's target frame rate is the only throttle), so it stops by itself when the tree leaves the panel; `Dispose` pauses it. Never schedule per-frame work with an interval near the frame period (`Every(16)` at 60 fps, `Every(33)` at 30 fps): the scheduler compares whole milliseconds without carry-over and skips every frame that arrives a fraction of a millisecond early, so the motion judders. Runs timers (`After`), idle loops (`OnIdle`), reactive updates (`OnUpdate`) and `MotionNode`s. |
| Per element | `UI/Motion/MotionNode.cs` | Base pose (tweens) + offsets (idle loops, parallax) + springs (squash, wobble, hop). Writes only `style.translate/scale/rotate/opacity`, only when the value changed, and only the properties it was asked to animate, so **layout never runs** and USS keeps the rest (a pill's `:active` sink stays USS). Elements that move every frame get `UsageHints.DynamicTransform`. No allocations per frame. |

Rules for new screens: animate transforms and opacity only; never put a USS `transition` or static value on a
property code animates on the same element (inline styles win, and a USS transition would smooth every
frame); put `transform-origin` in USS; use `Feel(button, kind, action)` for buttons, and
`.ExtendTo(rowOrPill)` when the control is smaller than 44 pt / 48 dp (about 108 units on a phone) so its row
or pill is the touch target; register idle loops with `Animator.OnIdle(update, rest)` so Reduce motion and
focus loss stop them. Anything that extends under the safe-area insets reads them with `SafeArea.Insets(root)`
(the padding SafeArea wrote), never `root.resolvedStyle.padding*`, which lags one layout pass behind after a
rotation.

### Main menu

* **Entrance** (about 1.15 s, replayed every time the menu is shown, e.g. back from the text test):
  counters drop in one by one with a bounce (0.05 s + 0.08 s each, OutBounce); globe and gear pop; the
  title ribbon swings in from its top edge (OutBack) and its tails flutter into place (OutElastic); the
  subtitle rises and fades in; the cream panel settles; the four pills pop from 0.6 one after another
  (0.4 s + 0.08 s each, OutBack); the bottom bar slides up.
* **Living backdrop** (`LivingBackdrop`, art from `make_ui_art.py`): sky, sun (turns slowly), four clouds
  drifting and wrapping, a V of three birds gliding across every 8-15 s, the Himalaya (a fishtail hero peak
  in the middle, which is what portrait shows), green hills with terraces and houses, a string of prayer
  flags in lungta order (blue, white, red, green, yellow, repeating) fluttering in a travelling gust, and a
  flowery meadow. Layers shift with **device tilt** (Input System `Accelerometer`, or `GravitySensor`,
  enabled while the menu is up, slowly re-centred so any holding angle is neutral) or with the **mouse** in
  the editor, for a parallax look into the mountains; the sensor is switched off again whenever the idle
  loops stop (Reduce motion, app unfocused). The backdrop bleeds under the notch and home indicator, and
  follows rotation (including a 180-degree turn, which moves the insets without resizing anything). In
  landscape the hills and meadow fit the width, so their crests and bushes are never clipped flat.
* **Idle life**: the Explore pill breathes and a shine sweeps across it every 4.2 s; the ribbon sways about
  1.6 degrees and its tails flutter; a counter icon hops every few seconds.
* **Fun**: "+" (or anywhere on its counter pill) rolls the demo value up (0.6 s) with a pop, the icon hops,
  coins/stars/hearts/bolts and sparkles burst out (pooled particles, which stay below the safe-area top and
  draw over the toast), and a Success haptic plays (hearts stop at 5 and energy at 100 with a
  "full" wiggle). Explore (a MediumImpact press) opens the world (see "Explore HUD"). Map and Collections give
  a friendly "soon" toast. Tapping the title
  ribbon says "Namaste!" (wobble, bubble, sparkles, Selection haptic). The globe flips every label edge-on,
  swaps the language and flips them back. The gear spins and opens Settings.
* **Settings sheet**: a bottom sheet in portrait, a side panel in landscape, sliding in over a dimmed scrim
  (rows slide in after it). Its little overshoot is a stretch from the screen edge it rests on, so it never
  lifts off that edge. Drag the grabber or header towards the edge to dismiss it (a Selection tick marks the
  point where letting go closes it; a flick closes it from anywhere; pulling it in stretches it a little);
  the Android back button and Escape close it too. Each switch's whole row is its touch target, as is the
  "Feel the haptics" row for Play. Toggles move with a squashing knob. **Vibration**
  plays Success when switched on; **Reduce motion**; **Language** (English / नेपाली); **Feel the haptics**
  plays every `HapticKind` 0.75 s apart and names each one, so it can be checked on a phone (and seen in the
  editor). Rotating while it is open, or mid-slide, moves it to the right edge for the new orientation
  (bottom in portrait, right in landscape).

### Haptics

Screens only see `Ghumante.Core.Services.IHaptics` (App creates it with
`Ghumante.Platform.Haptics.MobileHaptics.Create(settings.Haptics)`; on iOS UIKit feedback generators, on
Android `performHapticFeedback`/Vibrator, silent in the editor). The implementation rate-limits with
`HapticGate` and honours the player's switch and the OS settings.

| Interaction | When | HapticKind |
|---|---|---|
| Any pill or round button (Map, Collections, Settings, globe, gear, Text test, "+" or its counter pill, close, Done) | press-down | LightImpact |
| Back button / Escape closing Settings | press | LightImpact |
| Dragging Settings past the point where letting go closes it (or back) | drag | Selection |
| Explore | press-down | MediumImpact |
| Toggles (or their rows), language choice, TextSpike toolbar toggles, scrim | press-down | Selection |
| "+" adds to a counter | click | Success |
| "+" on full hearts / energy, Map / Collections before they exist | click | Warning |
| Title ribbon ("Namaste!") | tap | Selection |
| Language switched (globe or sheet) | mid-flip | Selection |
| Vibration switched on | click | Success |
| Feel the haptics | every 0.75 s | Selection, LightImpact, MediumImpact, HeavyImpact, Success, Warning, Error |

Press-down feedback needs `RegisterCallback<PointerDownEvent>(.., TrickleDown.TrickleDown)`: `Button.clicked`
fires on release and the Clickable manipulator captures the pointer (`PressFeel` does this).

A click's outcome (Success, Warning, Error, HeavyImpact) often arrives less than 35 ms after its own
press-down tick: a quick tap delivers both in one frame, and at 30 fps they are 33 ms apart. The rate limiter
would drop it, so `Core.Services.HapticPacer` holds it instead and it plays a frame or two later (never more
than 250 ms late). Press ticks are still dropped when they come too fast.

Platform notes:

* **Android**: the Editor build hook `EditorTools/AndroidVibratePermission.cs` declares
  `android.permission.VIBRATE` (an install-time permission, so there is no prompt). This lets
  MediumImpact and up use the Vibrator's predefined effects. Without the permission, every kind falls back
  to a View key-tap constant. Selection uses `CONTEXT_CLICK`, which plays as a crisp tick on API 29 to 36.
* **iOS**: iPads and Apple-silicon Macs have no Taptic Engine. `IsSupported` is false on them, so Settings
  shows the "can't vibrate" note.

### Reduce motion, Low tier, focus

* **Reduce motion** (Settings, or the OS setting until the player picks: iOS Reduce Motion, Android Remove
  animations, via `Ghumante.Platform.DeviceAccessibility`): no bounces, squash, parallax, idle loops, birds
  or particles; screens fade in (0.25 s); the sheet fades instead of sliding; the toast only fades. Haptics
  still play unless Vibration is off. Switching it on puts everything back at rest immediately.
* **Low tier** (`MotionSettings.LowPower`, from the device tier): two clouds, eleven flags at half rate, no
  birds, no shine (its clipping mask costs a stencil pass), a smaller particle pool. The tier's 30 fps target
  frame rate already limits the animator to 30 ticks a second.
* **Focus**: in players, idle loops freeze while the app is not focused (notification shade, Control
  Centre, a system dialog). The editor keeps them running while you click around the Inspector.

### Settings persistence

`Ghumante.Save.LocalSaveStore` writes the whole `SaveData` as UTF-8 JSON to
`Application.persistentDataPath/ghumante-save.json`, atomically (temp file, flush, `File.Replace`), keeping
two backups (`.bak`, `.bak2`) that are read if the save is unreadable; a save from a newer build is never
overwritten. App loads it at startup and saves whenever Vibration, Reduce motion or the language changes.
"The player chose" is recorded in `settings` as `reduceMotionChosen` / `languageChosen`
(`Ghumante.Save.SettingsChoices`); until then the game follows the device. To start fresh, delete the file
(macOS editor: `~/Library/Application Support/Ghumante/Ghumante/`).

### Trying it

**Mac editor, Play mode** (Bootstrap scene): the menu builds itself in about a second; move the mouse over
the Game view and the mountains, hills, flags and meadow shift at different depths. Hover a pill to make it
wobble, press and hold to see it squash, release for the stretch. Tap "+" next to the coins, the title
ribbon, Explore, the globe, the gear. In Settings, "Feel the haptics" names each kind as it plays; the debug
HUD (bottom right) shows `haptic: <kind> (editor - not felt)` for every request, which is how haptics are
checked without a motor. Switch Reduce motion on and off and watch the scene stop and come back.

**Device Simulator** (Window > General > Device Simulator, e.g. iPhone 15 or a Pixel): rotate with the
simulator's rotate buttons, also in the middle of the entrance or with Settings open; portrait shows
counters on top, the hero peak behind the title and the menu in the bottom third; landscape shows hero
left and menu right. Click-drag on the sky to steer the parallax (the simulator has no tilt sensor).

**Phone**: tilt the phone gently for the parallax; every button press should tick, "+" should give a
success pattern. Settings > Feel the haptics plays all seven kinds in order (iOS:
selection tick, light/medium/heavy taps, success/warning/error notifications; Android: the closest
predefined effects). With iOS Reduce Motion or Android Remove animations on, a fresh install starts with
Reduce motion on. Pull down the notification shade: flags and clouds stop until you come back.

## Explore HUD (M1 track D)

`Screens/Explore.uxml` + `ExploreScreen.cs`, fed every frame by `App/Explore/ExploreSession.cs` (which owns the world,
the explorer and the camera). Everything follows the menu's rules: transforms and opacity only, press feel and haptics on
every button, Reduce motion turns pops and slides into fades, text is written only when what it shows changes (the
speedometer reads cached digit strings, so it allocates nothing per frame).

| Element | What |
|---|---|
| Loading overlay | The Himalaya backdrop, a bobbing scooter, a status line ("Opening Kathmandu Core…", "Drawing the streets around Thamel…") and a progress bar: the region opening (unpacking on Android), then the tiles around the spawn streaming in. No region installed, or opening failed: a friendly explanation (Ghumante > Project Setup / Import Region Pack) and Back to menu. |
| Speedometer | Whole km/h, Devanagari digits (०-९) in Nepali. |
| Surface chip | The road's own surface (Asphalt, Brick, Gravel...) or, off the road, the ground's group with "· off road"; coloured by physics group (paved, gravel, dirt, mud). A small white dot when the road's surface is inferred by the pipeline (ADR-005); hover it for the explanation. It pops when the surface changes. |
| Place pill | The landmark you stand by (within 60 m, from the loaded tiles' POIs; never businesses, ADR-010) or the neighbourhood / village / town you are in; EN or NE (Nepali only where OSM has it). Changes after two looks half a second apart, so it never flickers. |
| Compass | The red tick points north; the rose turns with the camera. |
| Search / Walk-Ride / Menu | Search opens the search sheet; Walk-Ride shows the other mode (a walker while riding, the scooter while walking); Menu pauses. Tooltips name them on desktop. |
| Route chip | W2 detail pass (owner: "direction info blocks the view"): a compact chip, no longer a banner across the top. A yellow arrow shows the next turn (or points back to the route when more than 25 m off it), the big line how far the turn is ("240 m", "Straight on", "Back to the route"), the small line the distance and time left ("4.9 km · 9 min"; ETA = the route's own time scaled by what is left), × stops the route. `UI/Hud/RouteChipLayout.cs` places it (inline left/top, on every geometry change of the HUD or of any touch control, and on the next panel update after a layout change or the touch controls showing or hiding) in the first slot that is inside the safe area, clear of every other HUD element, including the floating stick's whole zone and the portrait drive zone (a thumb may land anywhere there), and never over the road ahead (`RoadAheadZone`, the envelope of every chase rig): landscape on the bottom edge left (then right) of the speedometer, which in practice means right of it on a tablet, left of it riding along (no stick) and under the top bar otherwise; portrait under the top bar (left, then right); else the other orientation's slots. "Finding the way to …" while A* runs; "Finding a new way…" when re-routing after 3 s more than 45 m off the route or after changing vehicle (the route profile follows the vehicle: car profile in four-wheelers). |
| Camera button | A little camera in the top bar: the next camera angle of the vehicle class (same as C and the right-stick press), named in a short toast; remembered per class in the save. |
| Arrival | Within about 40 m of the destination (or the end of the road once nearly there): confetti, a star toast "You made it to …!" and a Success haptic. |
| Search sheet | Bottom sheet in portrait, side panel in landscape (the settings sheet's styles and EdgeSheet pose). Search as you type (Latin, Devanagari, romanised, typos: Core's SearchEngine); empty query suggests the region's landmarks. Rows: name in the current language, the other script below, kind and distance; Ride there (MediumImpact) and, in development builds and the editor, Teleport. Enter rides to the first result. Gameplay pauses while it is open. |
| Pause panel | Resume, Settings (the main menu's SettingsSheet, same element names), Main menu (closes the world and frees its memory). The game also pauses when the app goes to the background. |
| Attribution | "Map data © OpenStreetMap contributors", always on screen. |
| Debug clock | Development builds and the editor: time of day with a slider. |

**Controls.** Keyboard: WASD or arrows (W/up throttle, S/down brake then reverse, A/D steer; on foot camera-relative),
Space brake, Shift boost (sprint on foot), E walk/ride, M map (a "soon" toast until wave 2), / search, Esc pause, mouse
wheel zoom, right-drag look. Gamepad: left stick, right trigger throttle, left trigger brake/reverse, A walk/ride, B brake,
X boost, Y search, Start pause, shoulders zoom, right stick look. Touch: see the orientation table above; one finger on the
world turns the camera, two fingers pinch to zoom. Touch controls hide as soon as a keyboard or gamepad drives and come
back on the first touch (on a Mac without the Device Simulator they start hidden). HUD buttons never take keyboard focus,
so Space and gamepad A only drive. Escape, Android back and gamepad B arrive as UI cancel events: they close the top panel
(settings, search, pause); while playing Escape and back pause, B (the brake) does not.

**Haptics in Explore.**

| Interaction | HapticKind |
|---|---|
| HUD buttons, pedals, route ×, pause buttons (camera button: Selection) | LightImpact (Walk-Ride, Ride there and Main menu: MediumImpact) |
| A thumb lands on the stick or the drive zone | Selection |
| Riding onto another surface | Selection |
| A bump or a firm landing | LightImpact |
| Stuck recovery (the hop and nudge) | MediumImpact |
| Arrival | Success |
| The ride starts / a teleport | MediumImpact |
| No region installed | Warning |

**Trying it**: game/README.md, "How to play the M1 slice on a Mac".

## The TextSpike (week-1 device test, ARCHITECTURE.md P3)

Open it from the main menu's "Text test" button (or pick *Start Screen: Text Spike* on the Bootstrap object to start there). Each row shows a hard case three ways: its id and what to look for, the **live** UI Toolkit rendering, and a **reference** image of the same string rendered by HarfBuzz (the shaping engine ATG uses) with the same font file. On every test device:

1. Compare each live cell with its reference. They must match glyph for glyph (sizes may differ by a pixel).
2. Tap "Shaping: Advanced" to switch the samples to the standard generator: the conjunct rows must visibly break. If they do not change, ATG was not active in the first place.
3. Screenshot the screen (the device line at the top identifies device, OS, GPU API and Unity version) and attach it to the spike report.

Cases live in `game/Tools/textspike_cases.json`; regenerate with `python game/Tools/textspike.py` (needs Pillow with libraqm). CI (`localization.yml`) fails if the UXML is out of date.

If the spike fails on device, the fallback in ARCHITECTURE.md P3 applies: a HarfBuzz-based shaping plugin.

## Checks (no Unity needed)

* `python tools/check_localization.py`: keys present in both tables, same placeholders, NFC, keys used by UXML/C# exist.
* `python tools/unity-compile-check/check_ui.py`: USS properties and keyword values valid for Unity 6.3, `var()` and `url()` resolve, UXML elements/attributes known, classes defined, presenters' `Required<T>("name")` match the UXML.
* `python game/Tools/fonts.py verify`: fonts are TrueType, OFL-licensed, cover the Devanagari block and shape the conjuncts.
