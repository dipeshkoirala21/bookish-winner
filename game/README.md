# Ghumante: Unity project

Unity **6000.3.25f1** (Unity 6.3 LTS, the current LTS line; 6.0 LTS support ends 2026-10-16) with URP 17.3. See `docs/ARCHITECTURE.md` section 7 for the runtime design.

## First open

1. Install Unity 6000.3.25f1 with Android Build Support (OpenJDK, Android SDK & NDK) and iOS Build Support.
2. Open `game/` in Unity Hub. Only `ProjectSettings/ProjectVersion.txt` is committed; Unity generates the rest.
3. Run **Ghumante → Project Setup**. It applies every player setting from code (ARCHITECTURE.md 11), creates the URP assets for the Low/Medium/High quality levels, the PanelSettings asset and the Bootstrap scene, and enables UI Toolkit's Advanced Text Generator (see `Assets/Ghumante/UI/README.md`).
4. Commit what Unity and ProjectSetup generated: all `.meta` files, `ProjectSettings/*.asset`, `Packages/packages-lock.json`, `Assets/Ghumante/Settings/**`, `Assets/Ghumante/Scenes/Bootstrap.unity`. From then on GUIDs are stable for everyone.
5. Press Play in `Assets/Ghumante/Scenes/Bootstrap.unity`: the Devanagari TextSpike screen appears; its back button leads to the main menu mock.

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
