#!/usr/bin/env python3
"""Fetch, instance and verify the UI fonts (Baloo 2 + Mukta, SIL OFL 1.1).

    python game/Tools/fonts.py fetch     # download from github.com/google/fonts (pinned SHA-256)
    python game/Tools/fonts.py instance  # Baloo 2 variable -> static Bold (700) / ExtraBold (800)
    python game/Tools/fonts.py verify    # magic bytes, licences, Devanagari coverage, HarfBuzz shaping

Why static instances: Unity's TextCore/UI Toolkit font import uses the default instance of a variable
font (Baloo 2's default is wght=400). The display style needs the heavy weights, so we cut static
instances with fontTools' instancer. Baloo 2's OFL declares no Reserved Font Name, so modified versions
may keep the name; they stay under the OFL (Baloo2-OFL.txt sits next to them).

Requirements: fontTools (pip install fonttools). `verify` also uses uharfbuzz when installed (pip install
uharfbuzz) to prove the conjuncts actually shape; without it the shaping check is skipped with a note.
"""
from __future__ import annotations

import argparse
import hashlib
import sys
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FONT_DIR = ROOT / "Assets" / "Ghumante" / "UI" / "Fonts"
RAW = "https://raw.githubusercontent.com/google/fonts/main/ofl"

# Files fetched from google/fonts (main, commit 9710da1e of 2026-09-30) with their SHA-256.
UPSTREAM = {
    "Baloo2-Variable.ttf": (f"{RAW}/baloo2/Baloo2%5Bwght%5D.ttf",
                            "d47a6852548059b1db49a1319d06d499d546c3fa2237cf9eee9c43c8abb025c2"),
    "Baloo2-OFL.txt": (f"{RAW}/baloo2/OFL.txt", None),
    "Mukta-Regular.ttf": (f"{RAW}/mukta/Mukta-Regular.ttf",
                          "2958e4af564507df2a856164df6f9978dacb03f999a4f34a0c269dc8a4de9688"),
    "Mukta-Medium.ttf": (f"{RAW}/mukta/Mukta-Medium.ttf", None),
    "Mukta-Bold.ttf": (f"{RAW}/mukta/Mukta-Bold.ttf", None),
    "Mukta-OFL.txt": (f"{RAW}/mukta/OFL.txt", None),
}

STATIC_INSTANCES = {"Baloo2-Bold.ttf": 700, "Baloo2-ExtraBold.ttf": 800}

# Every Devanagari code point the UI and the TextSpike screen use must be in every font.
DEVANAGARI_REQUIRED = (
    [chr(c) for c in range(0x0901, 0x0904)]      # candrabindu, anusvara, visarga
    + [chr(c) for c in range(0x0905, 0x0915)]    # independent vowels
    + [chr(c) for c in range(0x0915, 0x093A)]    # consonants
    + ["़", "ऽ"]                       # nukta, avagraha
    + [chr(c) for c in range(0x093E, 0x094E)]    # dependent vowels + virama
    + ["ॐ", "।", "॥"]             # om, danda, double danda
    + [chr(c) for c in range(0x0966, 0x0970)]    # digits 0-9
)

# (text, description): sequences that must NOT shape to one glyph per code point.
SHAPING_CASES = [
    ("क्ष", "क्ष conjunct"),
    ("त्र", "त्र conjunct"),
    ("ज्ञ", "ज्ञ conjunct"),
    ("श्र", "श्र conjunct"),
    ("द्ध", "द्ध conjunct"),
    ("द्व", "द्व conjunct"),
    ("र्क", "र्क reph"),
]


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def cmd_fetch(_: argparse.Namespace) -> int:
    FONT_DIR.mkdir(parents=True, exist_ok=True)
    for name, (url, expected) in UPSTREAM.items():
        dest = FONT_DIR / name
        print(f"fetch {url} -> {dest.relative_to(ROOT)}")
        with urllib.request.urlopen(url, timeout=120) as resp:
            data = resp.read()
        if expected and hashlib.sha256(data).hexdigest() != expected:
            print(f"  SHA-256 mismatch for {name}: upstream changed; review and update the pin", file=sys.stderr)
            return 1
        dest.write_bytes(data)
    return 0


def cmd_instance(_: argparse.Namespace) -> int:
    from fontTools.ttLib import TTFont
    from fontTools.varLib import instancer

    src = FONT_DIR / "Baloo2-Variable.ttf"
    for name, weight in STATIC_INSTANCES.items():
        font = TTFont(src, recalcTimestamp=False)
        static = instancer.instantiateVariableFont(font, {"wght": weight}, updateFontNames=True)
        # Deterministic output: keep head.modified from the source instead of stamping "now".
        static.recalcTimestamp = False
        static.save(FONT_DIR / name)
        print(f"wrote {name} (wght={weight}, {sha256(FONT_DIR / name)[:16]}...)")
    return 0


def _magic_ok(path: Path) -> bool:
    head = path.read_bytes()[:4]
    return head in (b"\x00\x01\x00\x00", b"OTTO", b"true")


def cmd_verify(_: argparse.Namespace) -> int:
    from fontTools.ttLib import TTFont

    failures = 0
    fonts = sorted(FONT_DIR.glob("*.ttf"))
    if not fonts:
        print("no fonts found", file=sys.stderr)
        return 1
    for lic in ("Baloo2-OFL.txt", "Mukta-OFL.txt"):
        text = (FONT_DIR / lic).read_text(encoding="utf-8-sig")
        ok = "SIL OPEN FONT LICENSE Version 1.1" in text
        print(f"{'ok  ' if ok else 'FAIL'} licence {lic}")
        failures += 0 if ok else 1

    try:
        import uharfbuzz as hb  # type: ignore
    except ImportError:
        hb = None
        print("note: uharfbuzz not installed; skipping the shaping check (pip install uharfbuzz)")

    for path in fonts:
        if not _magic_ok(path):
            print(f"FAIL {path.name}: not a TrueType/OpenType file (bad magic bytes)")
            failures += 1
            continue
        font = TTFont(path)
        cmap = font.getBestCmap()
        missing = [f"U+{ord(c):04X}" for c in DEVANAGARI_REQUIRED if ord(c) not in cmap]
        latin_missing = [c for c in "AZaz09+/,.!?" if ord(c) not in cmap]
        has_gsub_deva = False
        if "GSUB" in font:
            scripts = {rec.ScriptTag for rec in font["GSUB"].table.ScriptList.ScriptRecord}
            has_gsub_deva = bool(scripts & {"deva", "dev2"})
        name = font["name"].getDebugName(4)
        status = "ok  " if not missing and not latin_missing and has_gsub_deva else "FAIL"
        print(f"{status} {path.name}: '{name}', {len(cmap)} code points, "
              f"Devanagari GSUB={'yes' if has_gsub_deva else 'NO'}, missing={missing + latin_missing or 'none'}")
        if status == "FAIL":
            failures += 1
            continue
        if hb is not None:
            blob = hb.Blob(path.read_bytes())
            hb_font = hb.Font(hb.Face(blob))
            for text, label in SHAPING_CASES:
                buf = hb.Buffer()
                buf.add_str(text)
                buf.guess_segment_properties()
                hb.shape(hb_font, buf, {})
                glyphs = [hb_font.glyph_to_string(i.codepoint) for i in buf.glyph_infos]
                shaped = len(glyphs) < len(text) or any(g.startswith(("uni0930", "rakar", "reph")) for g in glyphs)
                # A conjunct either collapses into fewer glyphs or uses half/reph forms; the plain virama
                # glyph surviving between two full consonants means shaping did not happen.
                virama_left = any(g in ("uni094D", "viramadeva", "halant") for g in glyphs)
                ok = shaped or not virama_left
                if not ok:
                    failures += 1
                print(f"     {'ok  ' if ok else 'FAIL'} shape {label}: {text} -> {glyphs}")
    print("PASS" if failures == 0 else f"{failures} failure(s)")
    return 0 if failures == 0 else 1


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="cmd", required=True)
    sub.add_parser("fetch").set_defaults(func=cmd_fetch)
    sub.add_parser("instance").set_defaults(func=cmd_instance)
    sub.add_parser("verify").set_defaults(func=cmd_verify)
    args = parser.parse_args()
    return args.func(args)


if __name__ == "__main__":
    sys.exit(main())
