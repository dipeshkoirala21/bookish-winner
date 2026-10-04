#!/usr/bin/env python3
"""Static checks for the UI Toolkit assets under game/Assets/Ghumante (no Unity needed).

    python tools/unity-compile-check/check_ui.py

USS
  * every property is a Unity 6.3 USS property (list below; with --audit sources present it is
    re-derived from Unity's StylePropertyCache.cs and compared, so the list cannot silently go stale);
  * every var(--x) is declared somewhere in our USS;
  * every url("...") resolves to a file, relative to the USS file;
  * braces balance and comments close.
UXML
  * well-formed XML; only known elements and attributes (Unity 6.3 names);
  * <Style src> paths resolve;
  * every class used is defined by our USS (or is a Unity built-in unity-* class);
  * every binding-path is a key of the English string table.
Presenters
  * every Required<T>("name") in UI/Screens/*.cs exists in the screen's UXML with the matching element type.
"""
from __future__ import annotations

import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
UI = REPO / "game" / "Assets" / "Ghumante" / "UI"
STYLE_CACHE = HERE / ".cache" / "UnityCsReference" / "Modules" / "UIElements" / "Core" / "Style" / "Generated" / "StylePropertyCache.cs"

# USS properties of Unity 6.3 (6000.3), from UnityCsReference Modules/UIElements/Core/Style/Generated/StylePropertyCache.cs.
USS_PROPERTIES = set("""
align-content align-items align-self all aspect-ratio background-color background-image background-position
background-position-x background-position-y background-repeat background-size border-bottom-color
border-bottom-left-radius border-bottom-right-radius border-bottom-width border-color border-left-color
border-left-width border-radius border-right-color border-right-width border-top-color border-top-left-radius
border-top-right-radius border-top-width border-width bottom color cursor display filter flex flex-basis
flex-direction flex-grow flex-shrink flex-wrap font-size height justify-content left letter-spacing margin
margin-bottom margin-left margin-right margin-top max-height max-width min-height min-width opacity overflow
padding padding-bottom padding-left padding-right padding-top position right rotate scale text-overflow
text-shadow top transform-origin transition transition-delay transition-duration transition-property
transition-timing-function translate -unity-background-image-tint-color -unity-background-scale-mode
-unity-editor-text-rendering-mode -unity-font -unity-font-definition -unity-font-style -unity-material
-unity-overflow-clip-box -unity-paragraph-spacing -unity-slice-bottom -unity-slice-left -unity-slice-right
-unity-slice-scale -unity-slice-top -unity-slice-type -unity-text-align -unity-text-auto-size
-unity-text-generator -unity-text-outline -unity-text-outline-color -unity-text-outline-width
-unity-text-overflow-position visibility white-space width word-spacing
""".split())

# Keyword-valued properties we use, with their allowed keywords (from the same file).
KEYWORDS = {
    "-unity-text-generator": {"standard", "advanced"},
    "-unity-font-style": {"normal", "italic", "bold", "bold-and-italic"},
    "-unity-text-align": {f"{v}-{h}" for v in ("upper", "middle", "lower") for h in ("left", "center", "right")},
    "position": {"relative", "absolute"},
    "display": {"flex", "none"},
    "flex-direction": {"column", "row", "column-reverse", "row-reverse"},
    "white-space": {"normal", "nowrap", "pre", "pre-wrap"},
    "overflow": {"visible", "hidden", "scroll"},
    "-unity-background-scale-mode": {"stretch-to-fill", "scale-and-crop", "scale-to-fit"},
}

UXML_ELEMENTS = {
    "UXML": {"editor-extension-mode"},
    "Style": {"src"},
    "Template": {"src", "name"},
    "VisualElement": set(),
    "Label": {"text", "binding-path", "enable-rich-text", "display-tooltip-when-elided"},
    "Button": {"text", "binding-path"},
    "ScrollView": {"mode", "horizontal-scroller-visibility", "vertical-scroller-visibility", "elasticity",
                   "touch-scroll-type", "nested-interaction-kind"},
    "Image": set(),
}
COMMON_ATTRIBUTES = {"name", "class", "style", "picking-mode", "tooltip", "focusable", "tabindex", "view-data-key",
                     "usage-hints", "enabled", "content-container", "data-source-path", "language-direction"}

failures: list[str] = []


def fail(msg: str) -> None:
    failures.append(msg)
    print("FAIL", msg)


def strip_comments(text: str, path: Path) -> str:
    if text.count("/*") != text.count("*/"):
        fail(f"{rel(path)}: unbalanced /* */ comment")
    return re.sub(r"/\*.*?\*/", "", text, flags=re.S)


def rel(p: Path) -> str:
    return str(p.relative_to(REPO))


def check_style_cache_current() -> None:
    if not STYLE_CACHE.exists():
        print("note: Unity reference source not fetched; using the embedded USS property list (run.sh --audit to refresh)")
        return
    names = set(re.findall(r'\{"(-?[a-z][a-z-]*)",\s*"', STYLE_CACHE.read_text(encoding="utf-8")))
    helper = {"easing-function", "length-percentage", "single-transition", "single-transition-property", "timing-function", "ratio"}
    names -= helper
    if names != USS_PROPERTIES:
        fail(f"embedded USS property list is stale: missing {sorted(names - USS_PROPERTIES)}, extra {sorted(USS_PROPERTIES - names)}")
    else:
        print(f"ok   USS property list matches Unity reference source ({len(names)} properties)")


def check_uss(files: list[Path]) -> set[str]:
    declared_vars: set[str] = set()
    used_vars: list[tuple[Path, str]] = []
    classes: set[str] = set()
    for path in files:
        text = strip_comments(path.read_text(encoding="utf-8"), path)
        if text.count("{") != text.count("}"):
            fail(f"{rel(path)}: unbalanced braces")
            continue
        for selector, body in re.findall(r"([^{}]+)\{([^{}]*)\}", text):
            classes.update(re.findall(r"\.(-?[A-Za-z_][\w-]*)", selector))
            for decl in body.split(";"):
                decl = decl.strip()
                if not decl:
                    continue
                if ":" not in decl:
                    fail(f"{rel(path)}: malformed declaration '{decl}' in '{selector.strip()}'")
                    continue
                prop, value = (s.strip() for s in decl.split(":", 1))
                if prop.startswith("--"):
                    declared_vars.add(prop)
                elif prop not in USS_PROPERTIES:
                    fail(f"{rel(path)}: unknown USS property '{prop}' in '{selector.strip()}'")
                if prop in KEYWORDS and not value.startswith("var(") and value not in KEYWORDS[prop]:
                    fail(f"{rel(path)}: invalid value '{value}' for {prop}")
                used_vars += [(path, v) for v in re.findall(r"var\((--[\w-]+)\)", value)]
                for url in re.findall(r'url\("([^"]+)"\)', value):
                    if "://" in url:
                        fail(f"{rel(path)}: use a relative url, not '{url}'")
                    elif not (path.parent / url).resolve().exists():
                        fail(f"{rel(path)}: url '{url}' does not resolve")
    for path, v in used_vars:
        if v not in declared_vars:
            fail(f"{rel(path)}: var({v}) is never declared")
    print(f"ok   {len(files)} USS/TSS files, {len(declared_vars)} tokens, {len(classes)} classes")
    return classes


def local(tag: str) -> str:
    return tag.split("}", 1)[-1]


def check_uxml(files: list[Path], classes: set[str], keys: set[str]) -> dict[Path, dict[str, str]]:
    names_by_file: dict[Path, dict[str, str]] = {}
    for path in files:
        try:
            root = ET.parse(path).getroot()
        except ET.ParseError as e:
            fail(f"{rel(path)}: {e}")
            continue
        names: dict[str, str] = {}
        for el in root.iter():
            tag = local(el.tag)
            if tag not in UXML_ELEMENTS:
                fail(f"{rel(path)}: unknown element <{tag}>")
                continue
            allowed = UXML_ELEMENTS[tag] | (COMMON_ATTRIBUTES if tag not in ("UXML", "Style", "Template") else set())
            for attr in el.attrib:
                if local(attr) not in allowed:
                    fail(f"{rel(path)}: <{tag}> has unknown attribute '{local(attr)}'")
            if tag == "Style" and not (path.parent / el.attrib.get("src", "")).resolve().exists():
                fail(f"{rel(path)}: <Style src='{el.attrib.get('src')}'> does not resolve")
            for cls in el.attrib.get("class", "").split():
                if cls not in classes and not cls.startswith("unity-"):
                    fail(f"{rel(path)}: class '{cls}' is not defined in any USS")
            if "binding-path" in el.attrib and el.attrib["binding-path"] not in keys:
                fail(f"{rel(path)}: binding-path '{el.attrib['binding-path']}' is not a string table key")
            if "picking-mode" in el.attrib and el.attrib["picking-mode"] not in ("Position", "Ignore"):
                fail(f"{rel(path)}: picking-mode must be Position or Ignore")
            if "name" in el.attrib:
                if el.attrib["name"] in names:
                    fail(f"{rel(path)}: duplicate name '{el.attrib['name']}'")
                names[el.attrib["name"]] = tag
        names_by_file[path] = names
    print(f"ok   {len(files)} UXML files checked")
    return names_by_file


def check_presenters(names_by_file: dict[Path, dict[str, str]]) -> None:
    for cs in sorted((UI / "Screens").glob("*Screen.cs")):
        uxml = UI / "Screens" / (cs.stem.replace("Screen", "") + ".uxml")
        if uxml not in names_by_file:
            continue
        names = names_by_file[uxml]
        wanted = re.findall(r'Required<(\w+)>\("([^"]+)"\)', cs.read_text(encoding="utf-8"))
        for typ, name in wanted:
            if name not in names:
                fail(f"{rel(cs)}: Required<{typ}>(\"{name}\") but {rel(uxml)} has no element named '{name}'")
            elif names[name] != typ and not (typ == "VisualElement"):
                fail(f"{rel(cs)}: '{name}' is <{names[name]}> in {rel(uxml)}, presenter expects {typ}")
        print(f"ok   {cs.name}: {len(wanted)} required elements present in {uxml.name}")


def main() -> int:
    check_style_cache_current()
    uss = sorted(UI.rglob("*.uss")) + sorted(UI.rglob("*.tss"))
    classes = check_uss(uss)
    keys = set(json.loads((UI / "Localization" / "strings.en.json").read_text(encoding="utf-8")))
    names = check_uxml(sorted(UI.rglob("*.uxml")), classes, keys)
    check_presenters(names)
    print("PASS" if not failures else f"{len(failures)} UI check failure(s)")
    return 0 if not failures else 1


if __name__ == "__main__":
    sys.exit(main())
