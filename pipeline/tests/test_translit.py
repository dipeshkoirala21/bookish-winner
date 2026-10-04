"""Tests for translit: Devanagari romanisation and search-key folding."""

from __future__ import annotations

import random
import re
import unicodedata

import pytest

from ghumante_pipeline.translit import (
    CONSONANTS,
    INDEPENDENT_VOWELS,
    PHONETIC_RULES,
    VOWEL_SIGNS,
    clean,
    fold,
    has_devanagari,
    romanize,
)

KEY_RE = re.compile(r"^(?:[a-z0-9]+(?: [a-z0-9]+)*)?$")


# ---------------------------------------------------------------------------
# has_devanagari
# ---------------------------------------------------------------------------
@pytest.mark.parametrize("s,expected", [
    ("ठमेल", True), ("Thamel ठमेल", True), ("Thamel", False), ("", False), (None, False),
    ("२०८१", True), ("।", True), ("ꣲ", True), ("Boudhanāth", False), ("ཀ་ཏ་མན་ཏུ", False),
])
def test_has_devanagari(s, expected):
    assert has_devanagari(s) is expected


# ---------------------------------------------------------------------------
# romanize
# ---------------------------------------------------------------------------
@pytest.mark.parametrize("dev,rom", [
    # examples from the spec
    ("ठमेल", "thamel"),
    ("पोखरा", "pokharaa"),
    ("नेपाल", "nepaal"),
    ("काठमाडौं", "kaathamaadaun"),  # documented: medial schwa kept, final anusvara -> n
    ("काठमाडौँ", "kaathamaadaun"),  # chandrabindu spelling
    # conjuncts
    ("क्षेत्र", "kshetra"),  # क्ष = ksh, त्र = tr, conjunct-final keeps its "a"
    ("ज्ञान", "gyaan"),
    ("बौद्ध", "bauddha"),
    ("बौद्धनाथ स्तूप", "bauddhanaath stoop"),
    ("श्री", "shree"),
    ("भक्तपुर", "bhaktapur"),
    ("ललितपुर", "lalitapur"),
    ("विष्णु", "wishnu"),
    # anusvara before labials vs elsewhere; chandrabindu; visarga
    ("स्वयंभू", "swayambhoo"),
    ("स्वयम्भू", "swayambhoo"),
    ("संग", "sang"),
    ("गाउँ", "gaaun"),
    ("दुःख", "duhkh"),
    # schwa: kept on a word's only syllable and before a final sign
    ("र", "ra"),
    ("कं", "kan"),
    ("राम र सीता", "raam ra seetaa"),
    # independent vowels and ॐ
    ("ॐ", "om"),
    ("ऋषि", "rishi"),
    # explicit halant at the end of a word
    ("वाक्", "waak"),
])
def test_romanize_examples(dev, rom):
    assert romanize(dev) == rom


def test_romanize_independent_vowel_sequence():
    assert romanize("अ आ इ ई उ ऊ ऋ ए ऐ ओ औ") == "a aa i ee u oo ri e ai o au"


def test_romanize_vowel_signs_on_ka():
    signs = ["ा", "ि", "ी", "ु", "ू", "ृ", "े", "ै", "ो", "ौ"]
    assert [romanize("क" + s) for s in signs] == [
        "kaa", "ki", "kee", "ku", "koo", "kri", "ke", "kai", "ko", "kau"]


def test_romanize_consonant_table_from_spec():
    spec = ("क k, ख kh, ग g, घ gh, ङ ng, च ch, छ chh, ज j, झ jh, ञ n, ट t, ठ th, ड d, ढ dh, ण n, "
            "त t, थ th, द d, ध dh, न n, प p, फ ph, ब b, भ bh, म m, य y, र r, ल l, व w, श sh, ष sh, "
            "स s, ह h")
    for pair in spec.split(", "):
        dev, rom = pair.split(" ")
        assert CONSONANTS[dev] == rom
        # a lone consonant keeps its inherent vowel (no earlier vowel in the word)
        assert romanize(dev) == rom + "a"
        # word-final after a vowel: schwa deleted
        assert romanize("अ" + dev) == "a" + rom
        # with virama: bare consonant
        assert romanize(dev + "्") == rom


def test_romanize_nukta_ignored():
    # precomposed (U+095B) and decomposed forms both read as the base letter
    assert romanize("ज़") == romanize("ज़") == "ja"
    assert romanize("ड़") == "da"
    assert romanize("फ़िल्म") == romanize("फिल्म")


def test_romanize_digits_danda_avagraha_and_passthrough():
    assert romanize("२०८१") == "2081"
    assert romanize("वडा नं. ३") == "wadaa nan. 3"
    assert romanize("नमस्ते। धन्यवाद॥") == "namaste  dhanyawaad "
    assert romanize("सोऽहम्") == "soham"
    assert romanize("Thamel ठमेल MART") == "thamel thamel mart"
    assert romanize("Ğüş") == "ğüş"  # passed through, lowercased


def test_romanize_word_boundary_resets_schwa_state():
    # each word is judged on its own: "ल" is word-final in "ठमेल", the first
    # consonant of the next word keeps its "a"
    assert romanize("ठमेल,म") == "thamel,ma"
    assert romanize("ठमेल5") == "thamel5"


def test_romanize_zwj_zwnj_ignored():
    # ZWJ after a virama keeps the conjunct state (eyelash/half forms)
    assert romanize("उद्‍घाटन") == "udghaatan"
    assert romanize("क्‌ष") == romanize("क्ष") == "ksha"


def test_romanize_every_block_code_point_leaves_no_devanagari():
    for o in range(0x0900, 0x0980):
        ch = chr(o)
        for s in (ch, "क" + ch, ch + "क", "क्" + ch):
            out = romanize(s)
            assert not has_devanagari(out), (hex(o), out)
            assert out == out.lower()


def test_romanize_tables_are_ascii_lowercase():
    for table in (CONSONANTS, VOWEL_SIGNS, INDEPENDENT_VOWELS):
        for v in table.values():
            assert re.fullmatch(r"[a-z]*", v), v


def test_romanize_nfc_and_empty():
    assert romanize("") == ""
    assert romanize(unicodedata.normalize("NFD", "ठमेल")) == "thamel"


# ---------------------------------------------------------------------------
# clean / fold
# ---------------------------------------------------------------------------
def test_clean_steps():
    assert clean("  Boudhanāth   Stupa!! ") == "boudhanath stupa"
    assert clean("Children's Park") == "childrens park"
    assert clean("Ṭhamel’s ＴＥＳＴ") == "thamels test"  # NFKD folds full-width letters
    assert clean("Durbar-Square/Patan") == "durbar square patan"
    assert clean("ठमेल") == "thamel"
    assert clean("!!!") == ""


@pytest.mark.parametrize("group", [
    ["Kathmandu", "Kathmandoo", "kathmandu", "KATHMANDU", " Kathmandu "],
    ["Boudha", "Bouddha", "Baudha", "बौद्ध"],
    ["Swayambhu", "Swoyambhu", "स्वयम्भू", "स्वयंभू"],
    ["Swayambhunath", "Swoyambhunath", "स्वयम्भूनाथ", "स्वयंभुनाथ"],
    ["Thamel", "ठमेल", "Thaamel"],
    ["Pokhara", "पोखरा", "Pokharaa"],
    ["Nepal", "नेपाल", "Nepaal"],
    ["Patan", "पाटन"],
    ["Bhaktapur", "भक्तपुर"],
    ["Pashupatinath", "पशुपतिनाथ", "Pasupatinath"],
    ["Vishnu", "Bishnu", "विष्णु", "Wishnu"],
    ["Phewa", "Fewa", "फेवा"],
    ["Chowk", "Chok", "चोक"],
    ["Shree", "Sri", "Shri", "श्री"],
    ["Krishna", "कृष्ण"],
    ["Gyan", "ज्ञान"],
    ["Boudhanāth Stupa", "Boudhanath Stupa", "BOUDHANATH  STUPA"],
])
def test_fold_equivalence_classes(group):
    folded = {fold(g) for g in group}
    assert len(folded) == 1, dict(zip(group, (fold(g) for g in group)))


@pytest.mark.parametrize("s,key", [
    ("Kathmandu", "katmandu"),
    ("काठमाडौं", "katamadaun"),
    ("Boudha", "bauda"),
    ("Swayambhu", "sbayambu"),
    ("Thamel", "tamel"),
    ("Pokhara", "pokara"),
    ("pokhra", "pokra"),
    ("Chhetrapati", "chetrapati"),
    ("Ward 11", "bard 11"),  # digits are never collapsed
    ("Ward 1", "bard 1"),
    ("Zoo", "ju"),
    ("Qadri", "qadri"),
    ("", ""),
    ("   ", ""),
    ("—", ""),
])
def test_fold_exact_keys(s, key):
    assert fold(s) == key


def test_fold_kathmandu_devanagari_shares_prefix():
    # The Devanagari spelling has no "n" before ड and keeps the medial schwa, so
    # it does not fold to the English exonym; it shares the prefix "kat".
    a, b = fold("काठमाडौं"), fold("kathmandu")
    common = 0
    while common < min(len(a), len(b)) and a[common] == b[common]:
        common += 1
    assert (a, b, common) == ("katamadaun", "katmandu", 3)


def test_fold_rules_are_documented_in_order():
    assert [p for p, _ in PHONETIC_RULES][:3] == ["wo", "ow(?![aeiou])", "ou"]
    assert PHONETIC_RULES[-1] == (r"([a-z])\1+", r"\1")


def test_fold_output_alphabet():
    for s in ["Ṭhāmel", "काठमाडौं उपत्यका", "Durbar   Square!!", "ward ११", "Ünïcödé — test", "ﬁsh"]:
        out = fold(s)
        assert KEY_RE.match(out), (s, out)


_ALPHABET = (
    list("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789") * 3
    + list("aaeeiioouuhhwwvy") * 4  # bias towards the letters the rules touch
    + list("  -'’.,!/") + list("āṭṇśḍüéñ")
    + [chr(o) for o in range(0x0900, 0x0980)] + ["‌", "‍", "ﬁ", "Ａ", "²"]
)


def _random_strings(n: int, seed: int):
    rng = random.Random(seed)
    for _ in range(n):
        yield "".join(rng.choice(_ALPHABET) for _ in range(rng.randint(0, 24)))


def test_fold_idempotent_randomised():
    for s in _random_strings(4000, seed=20261004):
        f = fold(s)
        assert fold(f) == f, (s, f)
        assert KEY_RE.match(f), (s, f)


def test_fold_idempotent_on_phonetic_edge_cases():
    words = ["owo", "wwoo", "ooo", "eee", "oou", "ouu", "shh", "vh", "fh", "chchh", "ddhh", "kkhh", "wowow",
             "owa", "bowl", "tthh", "sshh", "aawoo", "qqh", "zzh", "fhh"]
    for w in words + [" ".join(words)]:
        f = fold(w)
        assert fold(f) == f, (w, f)


def test_romanize_is_deterministic_and_fold_matches_romanize_fold():
    for s in _random_strings(500, seed=7):
        assert romanize(s) == romanize(s)
        if has_devanagari(s):
            assert fold(romanize(s)) == fold(s)
