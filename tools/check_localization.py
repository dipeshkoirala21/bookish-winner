#!/usr/bin/env python3
"""Fail CI when the English and Nepali string tables disagree (ARCHITECTURE.md 7.9).

    python tools/check_localization.py [--tables DIR] [--source DIR]

Checks (errors fail the run, warnings are printed only):
  error   a key present in one table and missing in the other
  error   invalid JSON, duplicate keys, non-string values, empty values
  error   format placeholders ({0}, {1}, ...) differ between the two translations of a key
  error   text not in Unicode NFC (the game and the pipeline compare NFC strings)
  error   a key used by the game (UXML binding-path, Localizer.Get/Format("...") in C#) missing from the tables
  warning a Nepali value with no Devanagari at all (often an untranslated copy)
  warning keys no UXML or C# file references (one summary line)
"""
from __future__ import annotations

import argparse
import json
import re
import sys
import unicodedata
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
DEFAULT_TABLES = REPO / "game" / "Assets" / "Ghumante" / "UI" / "Localization"
DEFAULT_SOURCE = REPO / "game" / "Assets" / "Ghumante"
LOCALES = ("en", "ne")
PLACEHOLDER = re.compile(r"\{(\d+)(?:[^}]*)\}")
DEVANAGARI = re.compile(r"[ऀ-ॿ]")
CODE_KEY = re.compile(r"""\.(?:Get|Format)\(\s*"([A-Za-z0-9_.]+)\"""")
UXML_KEY = re.compile(r'binding-path="([^"]+)"')


def load_table(path: Path, errors: list[str]) -> dict[str, str]:
    def no_duplicates(pairs):
        seen: dict[str, object] = {}
        for k, v in pairs:
            if k in seen:
                errors.append(f"{path.name}: duplicate key '{k}'")
            seen[k] = v
        return seen

    try:
        data = json.loads(path.read_text(encoding="utf-8"), object_pairs_hook=no_duplicates)
    except (OSError, json.JSONDecodeError) as e:
        errors.append(f"{path.name}: cannot read: {e}")
        return {}
    if not isinstance(data, dict):
        errors.append(f"{path.name}: top level must be an object of key -> string")
        return {}
    table: dict[str, str] = {}
    for key, value in data.items():
        if not isinstance(value, str):
            errors.append(f"{path.name}: '{key}' must be a string, got {type(value).__name__}")
            continue
        if not value.strip():
            errors.append(f"{path.name}: '{key}' is empty")
        if unicodedata.normalize("NFC", value) != value or unicodedata.normalize("NFC", key) != key:
            errors.append(f"{path.name}: '{key}' is not NFC-normalised")
        table[key] = value
    return table


def used_keys(source: Path) -> dict[str, set[str]]:
    used: dict[str, set[str]] = {}
    for path in list(source.rglob("*.uxml")) + list(source.rglob("*.cs")):
        if "Tests" in path.parts:
            continue  # tests use made-up keys on purpose
        text = path.read_text(encoding="utf-8")
        pattern = UXML_KEY if path.suffix == ".uxml" else CODE_KEY
        for key in pattern.findall(text):
            used.setdefault(key, set()).add(str(path.relative_to(REPO)) if REPO in path.parents else str(path))
    return used


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--tables", type=Path, default=DEFAULT_TABLES, help="folder with strings.<locale>.json")
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE, help="folder scanned for used keys")
    args = parser.parse_args()

    errors: list[str] = []
    warnings: list[str] = []
    tables = {loc: load_table(args.tables / f"strings.{loc}.json", errors) for loc in LOCALES}

    all_keys = set().union(*(t.keys() for t in tables.values()))
    for key in sorted(all_keys):
        missing = [loc for loc in LOCALES if key not in tables[loc]]
        if missing:
            errors.append(f"key '{key}' missing in: {', '.join(f'strings.{m}.json' for m in missing)}")
            continue
        holders = {loc: sorted(set(PLACEHOLDER.findall(tables[loc][key]))) for loc in LOCALES}
        if len({tuple(h) for h in holders.values()}) > 1:
            errors.append(f"key '{key}': placeholders differ: " + ", ".join(f"{loc}={h}" for loc, h in holders.items()))
        if not DEVANAGARI.search(tables["ne"][key]):
            warnings.append(f"key '{key}': Nepali text has no Devanagari ('{tables['ne'][key]}'); untranslated?")

    if args.source.exists():
        used = used_keys(args.source)
        for key, files in sorted(used.items()):
            if key not in all_keys:
                errors.append(f"key '{key}' is used by {', '.join(sorted(files))} but is not in the string tables")
        unused = sorted(all_keys - set(used))
        if unused:
            # Expected while screens are mocks (HUD and settings keys land in M1); keys picked at run time
            # (e.g. Get(flag ? "a" : "b")) also show up here.
            warnings.append(f"{len(unused)} key(s) not referenced by a UXML binding-path or a literal "
                            f"Localizer.Get/Format call: {', '.join(unused)}")

    for w in warnings:
        print(f"warning: {w}")
    for e in errors:
        print(f"error: {e}")
    counts = ", ".join(f"{loc}: {len(tables[loc])} keys" for loc in LOCALES)
    if errors:
        print(f"FAILED ({counts}; {len(errors)} error(s), {len(warnings)} warning(s))")
        return 1
    print(f"OK ({counts}; {len(warnings)} warning(s))")
    return 0


if __name__ == "__main__":
    sys.exit(main())
