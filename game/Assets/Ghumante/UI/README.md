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
| `Screens/MainMenu.uxml` + `MainMenuScreen.cs` | Main menu mock: title ribbon "Ghumante / घुमन्ते", Explore, Map, Collections, Settings, counters bar |
| `Screens/TextSpike.uxml` + `TextSpikeScreen.cs` | The P3 shaping test (generated, see below) |
| `TextSpike/` | Its layout USS, generated reference USS and the HarfBuzz reference images |
| `Localization/strings.{en,ne}.json`, `Localizer.cs` | Flat string tables and the M0 dictionary localizer (replaced by the Unity Localization package in M1, same keys) |
| `OrientationWatcher.cs`, `SafeArea.cs` | Portrait/landscape classification from the safe area (orient-* classes, `OrientationChanged`) and safe-area padding |
| `Fonts/` | Baloo 2 (display; static Bold/ExtraBold instances cut from the variable font) and Mukta (body), SIL OFL 1.1, licence files alongside |
| `Icons/` | Placeholder icons and menu background, drawn by `game/Tools/make_ui_art.py` |

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
| TextSpike | case / live / reference in three columns | one block per case, live above reference, column headings hidden; toggles in the bottom bar in both orientations |

PanelSettings uses an orientation-neutral scale (square 1500x1500 reference, match 0.5), so a 20:9 phone is
about 2069x931 panel units in landscape and 931x2069 in portrait and elements keep their physical size
when the device turns. Camera rigs use `Ghumante.World.Cameras.CameraFov.VerticalFromHorizontal` to keep a
minimum horizontal FOV (vertical FOV = 2*atan(tan(h/2)/aspect), clamped to 100 degrees).

Unverified without Unity: the actual layout at each reference resolution. Check in the UI Builder or on
device at 1080x2400 and 2400x1080 (and an iPad, both ways) before M0 sign-off.

## The TextSpike (week-1 device test, ARCHITECTURE.md P3)

Every M0 build starts on this screen. Each row shows a hard case three ways: its id and what to look for, the **live** UI Toolkit rendering, and a **reference** image of the same string rendered by HarfBuzz (the shaping engine ATG uses) with the same font file. On every test device:

1. Compare each live cell with its reference. They must match glyph for glyph (sizes may differ by a pixel).
2. Tap "Shaping: Advanced" to switch the samples to the standard generator: the conjunct rows must visibly break. If they do not change, ATG was not active in the first place.
3. Screenshot the screen (the device line at the top identifies device, OS, GPU API and Unity version) and attach it to the spike report.

Cases live in `game/Tools/textspike_cases.json`; regenerate with `python game/Tools/textspike.py` (needs Pillow with libraqm). CI (`localization.yml`) fails if the UXML is out of date.

If the spike fails on device, the fallback in ARCHITECTURE.md P3 applies: a HarfBuzz-based shaping plugin.

## Checks (no Unity needed)

* `python tools/check_localization.py`: keys present in both tables, same placeholders, NFC, keys used by UXML/C# exist.
* `python tools/unity-compile-check/check_ui.py`: USS properties and keyword values valid for Unity 6.3, `var()` and `url()` resolve, UXML elements/attributes known, classes defined, presenters' `Required<T>("name")` match the UXML.
* `python game/Tools/fonts.py verify`: fonts are TrueType, OFL-licensed, cover the Devanagari block and shape the conjuncts.
