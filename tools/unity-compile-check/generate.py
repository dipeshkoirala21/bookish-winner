#!/usr/bin/env python3
"""Mirror game/Assets/Ghumante's asmdef graph as .NET projects, so `dotnet build` compiles the Unity C#
outside Unity (no licence needed).

    python tools/unity-compile-check/generate.py   # writes tools/unity-compile-check/generated/
    dotnet build tools/unity-compile-check/generated/CompileCheck.slnx   (run.sh does both)

One csproj per asmdef, with:
  * references between Ghumante assemblies exactly as the asmdefs declare them, so an illegal
    cross-module dependency (ARCHITECTURE.md 7.1) fails here just as it would in Unity;
  * UnityEngine from the NuGet package UnityEngine.Modules 2021.3.33 (reference assemblies; the newest on
    nuget.org). Unity 6.3's runtime API is mostly a superset; tools/unity-compile-check/Stubs/Unity6 holds
    the Unity-6-only declarations our runtime code needs (kept minimal);
  * UnityEditor and URP from hand-written declaration stubs (Stubs/UnityEditor, Stubs/Urp), which the API
    audit (run.sh --audit) checks against Unity's 6000.3 reference source;
  * NUnit for the EditMode tests;
  * C# 9 and .NET Standard 2.1, like Unity 6.3.

Package assemblies we have no reference for (Burst, Collections, Mathematics, Input System, Addressables,
Localization, ...) are skipped: code that starts using them fails to compile here until a stub or a
reference package is added, which is the signal we want.
"""
from __future__ import annotations

import json
import shutil
import sys
from pathlib import Path
from xml.sax.saxutils import escape

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
GAME = REPO / "game" / "Assets" / "Ghumante"
OUT = HERE / "generated"

STUB_PROJECTS = {
    "UnityEditor": HERE / "Stubs" / "UnityEditor" / "UnityEditor.csproj",
    "Unity.RenderPipelines.Universal.Runtime": HERE / "Stubs" / "Urp" / "Unity.RenderPipelines.Universal.Runtime.csproj",
    "Unity6": HERE / "Stubs" / "Unity6" / "Unity6.Stubs.csproj",
}
# Package assemblies that resolve to nothing we can compile against yet.
KNOWN_UNAVAILABLE = {
    "Unity.Burst", "Unity.Collections", "Unity.Mathematics", "Unity.InputSystem", "Unity.Addressables",
    "Unity.ResourceManager", "Unity.Localization", "Unity.AdaptivePerformance",
    "Unity.RenderPipelines.Core.Runtime",  # its few types we touch live in UnityEngine.CoreModule
}
TEST_RUNNER = {"UnityEngine.TestRunner", "UnityEditor.TestRunner"}


def asmdefs() -> dict[str, tuple[Path, dict]]:
    found: dict[str, tuple[Path, dict]] = {}
    for path in sorted(GAME.rglob("*.asmdef")):
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        found[data["name"]] = (path, data)
    return found


def sources_for(asmdef_path: Path, all_asmdef_dirs: list[Path]) -> str:
    """Compile items: every .cs under the asmdef folder, minus nested asmdef folders."""
    folder = asmdef_path.parent
    nested = [d for d in all_asmdef_dirs if d != folder and folder in d.parents]
    rel = lambda p: escape(str(Path("..") / ".." / ".." / ".." / p.relative_to(REPO)).replace("\\", "/"))
    items = [f'    <Compile Include="{rel(folder)}/**/*.cs" />']
    for d in nested:
        items.append(f'    <Compile Remove="{rel(d)}/**/*.cs" />')
    return "\n".join(items)


def main() -> int:
    graph = asmdefs()
    if not graph:
        print("no asmdef files found under", GAME, file=sys.stderr)
        return 1
    if OUT.exists():
        shutil.rmtree(OUT)
    OUT.mkdir(parents=True)
    dirs = [p.parent for p, _ in graph.values()]
    projects: list[Path] = []
    notes: list[str] = []
    missing_own: set[str] = set()

    for name, (path, data) in graph.items():
        editor_only = data.get("includePlatforms") == ["Editor"]
        no_engine = bool(data.get("noEngineReferences"))
        refs: list[str] = []
        package_refs: list[str] = []
        for ref in data.get("references", []):
            if ref.startswith("GUID:"):
                notes.append(f"{name}: GUID reference {ref} cannot be resolved outside Unity; use names")
            elif ref in graph:
                refs.append(f'    <ProjectReference Include="../{ref}/{ref}.csproj" />')
            elif ref in STUB_PROJECTS:
                refs.append(f'    <ProjectReference Include="{STUB_PROJECTS[ref]}" />')
            elif ref in TEST_RUNNER:
                pass
            elif ref in KNOWN_UNAVAILABLE:
                notes.append(f"{name}: no reference assembly for {ref} (skipped)")
            elif ref.startswith("Ghumante."):
                missing_own.add(ref)
            else:
                notes.append(f"{name}: unknown reference {ref} (skipped)")
        if editor_only:
            refs.append(f'    <ProjectReference Include="{STUB_PROJECTS["UnityEditor"]}" />')
        if any(r in TEST_RUNNER for r in data.get("references", [])) or \
                "nunit.framework.dll" in data.get("precompiledReferences", []):
            package_refs.append('    <PackageReference Include="NUnit" Version="3.14.0" />')
        if not no_engine:
            refs.append(f'    <ProjectReference Include="{STUB_PROJECTS["Unity6"]}" />')

        defines = ["UNITY_6000_3_OR_NEWER", "UNITY_6000_0_OR_NEWER", "UNITY_2021_3_OR_NEWER", "ENABLE_IL2CPP"]
        # Player assemblies are compiled once per mobile platform (run.sh passes UnityPlatformDefine=UNITY_ANDROID,
        # then UNITY_IOS) so platform #if branches are checked too; editor assemblies see UNITY_EDITOR.
        defines += ["UNITY_EDITOR", "UNITY_INCLUDE_TESTS"] if editor_only else ["DEVELOPMENT_BUILD", "$(UnityPlatformDefine)"]
        unity_import = "" if no_engine else '  <Import Project="$(MSBuildThisFileDirectory)../../UnityEngine.props" />\n'
        csproj = f'''<Project Sdk="Microsoft.NET.Sdk">
  <!-- GENERATED by tools/unity-compile-check/generate.py from {path.relative_to(REPO)}. Do not edit. -->
  <PropertyGroup>
    <AssemblyName>{escape(name)}</AssemblyName>
    <DefineConstants>$(DefineConstants);{";".join(defines)}</DefineConstants>
    <AllowUnsafeBlocks>{str(bool(data.get("allowUnsafeCode"))).lower()}</AllowUnsafeBlocks>
  </PropertyGroup>
{unity_import}  <ItemGroup>
{sources_for(path, dirs)}
  </ItemGroup>
  <ItemGroup>
{chr(10).join(refs + package_refs)}
  </ItemGroup>
</Project>
'''
        proj_dir = OUT / name
        proj_dir.mkdir()
        proj = proj_dir / f"{name}.csproj"
        proj.write_text(csproj, encoding="utf-8")
        projects.append(proj)

    sln = ["<Solution>"] + [f'  <Project Path="{p.relative_to(OUT).as_posix()}" />' for p in projects] + ["</Solution>", ""]
    (OUT / "CompileCheck.slnx").write_text("\n".join(sln), encoding="utf-8")
    unavailable: dict[str, set[str]] = {}
    for n in sorted(set(notes)):
        if ": no reference assembly for " in n:
            asm, pkg = n.split(": no reference assembly for ")
            unavailable.setdefault(pkg.replace(" (skipped)", ""), set()).add(asm)
        else:
            print("note:", n)
    for pkg, users in sorted(unavailable.items()):
        print(f"note: no reference assembly for {pkg} (declared by {', '.join(sorted(users))}); "
              "code using it will not compile here until a stub is added")
    for ref in sorted(missing_own):
        print(f"note: {ref} is referenced but has no asmdef yet (being written separately?); reference skipped")
    print(f"generated {len(projects)} projects in {OUT.relative_to(REPO)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
