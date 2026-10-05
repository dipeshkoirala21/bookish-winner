# Unity compile check (no Unity licence needed)

Unity cannot run in CI without a licence, and not at all in some dev environments. This tool still answers the two questions that matter for code review: *does the game C# compile*, and *does every Unity API it uses exist, unchanged and non-obsolete, in the Unity version we ship (6000.3)*.

```bash
tools/unity-compile-check/run.sh           # compile only (offline after the first NuGet restore)
tools/unity-compile-check/run.sh --audit   # compile + audit against Unity's 6000.3 reference source
```

Requires the .NET 8 SDK, Python 3 and git. `unity.yml` runs it with `--audit` on every change under `game/`.

## How it works

1. **`generate.py`** reads every `.asmdef` under `game/Assets/Ghumante` and writes one `.csproj` per assembly to `generated/` (git-ignored), with the same references between our assemblies. A module that reaches into another module it does not declare fails here exactly as in Unity.
2. **References.** UnityEngine comes from the NuGet package `UnityEngine.Modules` **2021.3.33** (reference assemblies; the newest on nuget.org, Unity publishes none for Unity 6). UnityEditor, URP and the Input System have no reference packages, so `Stubs/UnityEditor`, `Stubs/Urp` and `Stubs/InputSystem` declare just the members our code uses, with their Unity 6.3 (and `game/Packages/manifest.json`) signatures. For a stubbed package, `generate.py` also applies the asmdefs' `versionDefines` from the manifest (e.g. `GHUMANTE_INPUT_SYSTEM`), so the code those defines guard is compiled and audited too. `Stubs/Unity6` is for Unity-6-only runtime APIs and is empty: the M0 runtime code avoids them (Unity-6-only features are used from USS instead). EditMode tests compile against NUnit 3.14.
3. **Two passes**: player assemblies are compiled with `UNITY_ANDROID`, then with `UNITY_IOS`, so platform `#if` branches are checked too; editor assemblies see `UNITY_EDITOR`. C# 9, .NET Standard 2.1, warnings as errors, `[Obsolete]` use is an error.
4. **API audit (`--audit`).** `audit/fetch_reference_sources.sh` shallow-clones the Unity C# reference source at the tag in `ProjectVersion.txt`, the Graphics repository at the matching `6000.3/staging` branch, and the Input System package (`Unity-Technologies/InputSystem` at the version tag in `game/Packages/manifest.json`) into `.cache/`. `audit/Indexer` parses them (syntax only) into `.cache/unity-api-index.tsv`: every declared type and member with accessibility, signature, property accessors and `[Obsolete]` state. The Roslyn analyzer in `audit/Analyzer` then runs inside the compile and checks every Unity symbol our code binds to:
   * `GHU001` the type or member does not exist in Unity 6.3,
   * `GHU002` it is `[Obsolete]` in Unity 6.3 (for example `Rigidbody.velocity`, `Object.FindObjectOfType`),
   * `GHU003` it exists with a different signature (parameter or value type),
   * `GHU004` it is not public, or its setter is not public and we assign it.
   This is what makes the hand-written stubs trustworthy: a stub member that does not match Unity 6.3 fails the audit.
5. **`check_ui.py`** validates USS and UXML: properties and keyword values against Unity 6.3's USS property table (re-derived from the reference source when `--audit` fetched it), `var()`/`url()` resolution, known UXML elements and attributes, defined classes, localisation keys, and that every `Required<T>("name")` in a screen presenter exists in its UXML.

The Unity reference source is used for reference only, as its licence allows: it is read to list declarations and is never committed, modified or redistributed (`.cache/` is git-ignored). The same goes for the Input System package source (Unity Companion License), which is only read for its declarations.

## What it cannot verify

* Behaviour: nothing here runs Unity. Serialization, import, URP rendering, UI Toolkit layout and text shaping on device need the editor or a device (EditMode tests and the TextSpike screen cover the first steps).
* Package APIs without stubs (Burst, Collections, Mathematics, Addressables, Localization): code that starts using them fails to compile here until a stub or reference package is added; `generate.py` lists them.
* Overload resolution subtleties: the audit compares simple type names, which is enough to tell real overloads apart but would not notice, say, a namespace change of a parameter type with the same name.
* `Ghumante.Core` is compiled from its asmdef when present (it is written separately); until then references to it are skipped.

## Adding a Unity editor or URP API

Use it in game code, run `run.sh`: the compile fails on the missing stub. Add the member to `Stubs/UnityEditor/UnityEditor.Stubs.cs` (or `Stubs/Urp/Urp.Stubs.cs`) with the signature from the Unity 6.3 Scripting API docs, then run `run.sh --audit`; the analyzer confirms or rejects the signature.
