"""Devanagari romanisation and search-key folding (the reference for the C# port).

Search keys are not shown to anyone. They are canonical ASCII strings that
English spellings ("Boudha", "Bouddha", "Baudha"), romanised Nepali and
Devanagari ("बौद्ध") collapse onto, so that ``search_index`` can match them
with exact, prefix and fuzzy lookups. Nothing here is ever displayed in the UI
(ADR-005: no machine transliteration for Nepali readers).

``romanize(text)``: Devanagari -> plain lowercase ASCII
=====================================================

The input is NFC-normalised, then scanned left to right:

* **Consonants** emit their base (table ``CONSONANTS``: क k, ख kh, ग g, घ gh,
  ङ ng, च ch, छ chh, ज j, झ jh, ञ n, ट t, ठ th, ड d, ढ dh, ण n, त t, थ th, द d,
  ध dh, न n, प p, फ ph, ब b, भ bh, म m, य y, र r, ल l, व w, श sh, ष sh, स s,
  ह h; ऩ n, ऱ r, ळ l, ऴ l). The conjunct ज्ञ is "gy". क्ष (k+sh) and त्र (t+r)
  come out as "ksh" and "tr" on their own. After the consonant (skipping
  nukta, ZWJ and ZWNJ):

  - a virama (्) emits nothing: the next consonant is part of a conjunct;
  - a vowel sign emits its vowel (``VOWEL_SIGNS``: ा aa, ि i, ी ee, ु u,
    ू oo, ृ ri, े e, ै ai, ो o, ौ au, plus rare signs);
  - otherwise the inherent vowel "a" is emitted, **except** for schwa
    deletion: the "a" is dropped when the consonant is word-final (the next
    character is not a Devanagari letter or sign, see ``_is_word_char``)
    **and** a vowel has already been emitted in this word **and** the
    consonant does not close a conjunct (is not preceded by a virama).
    So ठमेल -> "thamel", नेपाल -> "nepaal", but र -> "ra" (no earlier vowel)
    and बौद्ध -> "bauddha", क्षेत्र -> "kshetra" (conjunct-final).
    Medial schwas are kept: काठमाडौं -> "kaathamaadaun", ललितपुर ->
    "lalitapur". Nepali deletes far fewer medial schwas than Hindi, and a
    medial rule would turn पोखरा into "pokhraa".

* **Independent vowels** (``INDEPENDENT_VOWELS``): अ a, आ aa, इ i, ई ee,
  उ u, ऊ oo, ऋ ri, ए e, ऐ ai, ओ o, औ au, plus rare letters. ॐ is "om".
* **Anusvara** (ं) is "m" when the next character is a labial consonant
  (प फ ब भ म) and "n" otherwise. **Chandrabindu** (ँ, and the inverted ऀ)
  is "n". **Visarga** (ः) is "h".
* **Dropped:** nukta (़; so the precomposed nukta letters क़..य़ read as their
  base letters), avagraha (ऽ), stress marks (॑ ॒ ॓ ॔), the high spacing dot
  (ॱ), ZWJ / ZWNJ (U+200D / U+200C, which Nepali uses for half forms), and
  the Vedic marks U+1CD0-U+1CFF and U+A8E0-U+A8FF.
* **Digits** ०-९ become 0-9. **Danda** (।), double danda (॥) and the
  abbreviation sign (॰) become a space.
* Any other character is passed through, lowercased (``str.lower``).

A word boundary (which resets the "vowel already emitted" and conjunct
state) is any character that is not a Devanagari letter or sign.

``fold(s)``: the canonical search key
=====================================

1. NFC-normalise.
2. If the string contains Devanagari (``has_devanagari``), ``romanize`` it
   (this also lowercases the non-Devanagari parts).
3. NFKD-normalise and drop every non-spacing mark (Unicode category Mn), so
   "ā" -> "a" and "ṭ" -> "t".
4. Lowercase.
5. Delete apostrophes (U+0027 ' U+0060 ` U+2018 ‘ U+2019 ’ U+02BC ʼ), then
   replace every run of characters outside ``[a-z0-9]`` with one space and
   trim. The result is lowercase ASCII words separated by single spaces.
6. Apply the phonetic rules ``PHONETIC_RULES`` in order, each one a global
   left-to-right, non-overlapping regex replacement over the whole string
   (no rule matches across a space, so this is the same as per-word):

   ====  ==========================  ==========  =================================
   step  pattern                     becomes     why
   ====  ==========================  ==========  =================================
   1     ``wo``                      ``wa``      Swoyambhu = Swayambhu
   2     ``ow`` not before a vowel   ``o``       Chowk = Chok (चोक)
   3     ``ou``                      ``au``      Boudha = Baudha
   4     ``ee``                      ``i``       ी "ee" = English "i"
   5     ``oo``                      ``u``       ू "oo" = Kathmandu/Kathmandoo
   6     ``v`` / ``w``               ``b``       व: Vishnu = Bishnu = विष्णु
   7     ``z``                       ``j``       ज़ / English z
   8     ``f``                       ``p``       Fewa = Phewa (फ)
   9     ``[kgjtdbps]h``             drop ``h``  aspirates kh gh jh th dh bh ph,
                                                 and sh -> s (श ष स merge)
   10    ``([a-z])\\1+``              ``\\1``      doubled letters: aa -> a,
                                                 ll -> l, dd -> d, chh -> ch
   ====  ==========================  ==========  =================================

   Steps 1-10 are repeated until the string stops changing. Every rule
   either shortens the string or lowers the number of ``o v w z f``
   letters, and none adds those letters, so the loop ends (in practice
   after one or two passes). That makes ``fold`` idempotent:
   ``fold(fold(x)) == fold(x)``. Digits are never rewritten, so "ward 11"
   stays distinct from "ward 1".

Examples: fold("Kathmandu") = fold("Kathmandoo") = "katmandu";
fold("काठमाडौं") = "katamadaun" (it shares only the prefix "kat" because
the Devanagari spelling has no "n" before the "d" and keeps the medial
schwa; the entry's own ``name:en`` key covers the English exonym);
fold("Boudha") = fold("Bouddha") = fold("Baudha") = fold("बौद्ध") = "bauda";
fold("Swayambhu") = fold("Swoyambhu") = fold("स्वयम्भू") = "sbayambu";
fold("Thamel") = fold("ठमेल") = "tamel"; fold("Pokhara") = fold("पोखरा") =
"pokara".
"""

from __future__ import annotations

import re
import unicodedata

# ---------------------------------------------------------------------------
# Devanagari tables
# ---------------------------------------------------------------------------
VIRAMA = "्"
NUKTA = "़"
ANUSVARA = "ं"
CHANDRABINDU = "ँ"
INVERTED_CHANDRABINDU = "ऀ"
VISARGA = "ः"
ZWNJ = "‌"
ZWJ = "‍"

CONSONANTS: dict[str, str] = {
    "क": "k", "ख": "kh", "ग": "g", "घ": "gh", "ङ": "ng",
    "च": "ch", "छ": "chh", "ज": "j", "झ": "jh", "ञ": "n",
    "ट": "t", "ठ": "th", "ड": "d", "ढ": "dh", "ण": "n",
    "त": "t", "थ": "th", "द": "d", "ध": "dh", "न": "n",
    "प": "p", "फ": "ph", "ब": "b", "भ": "bh", "म": "m",
    "य": "y", "र": "r", "ल": "l", "व": "w",
    "श": "sh", "ष": "sh", "स": "s", "ह": "h",
    # precomposed letters that NFC keeps
    "ऩ": "n", "ऱ": "r", "ळ": "l", "ऴ": "l",
    # precomposed nukta letters (NFC decomposes these to base + nukta anyway)
    "क़": "k", "ख़": "kh", "ग़": "g", "ज़": "j",
    "ड़": "d", "ढ़": "dh", "फ़": "ph", "य़": "y",
    # rare additions to the block
    "ॸ": "d", "ॹ": "j", "ॺ": "y", "ॻ": "g", "ॼ": "j",
    "ॽ": "", "ॾ": "d", "ॿ": "b",
}

VOWEL_SIGNS: dict[str, str] = {
    "ा": "aa", "ि": "i", "ी": "ee", "ु": "u", "ू": "oo",
    "ृ": "ri", "ॄ": "ri", "ॢ": "li", "ॣ": "li",
    "ॅ": "e", "ॆ": "e", "े": "e", "ै": "ai",
    "ॉ": "o", "ॊ": "o", "ो": "o", "ौ": "au",
    "ऺ": "o", "ऻ": "oo", "ॎ": "e", "ॏ": "au",
    "ॕ": "e", "ॖ": "u", "ॗ": "oo",
}

INDEPENDENT_VOWELS: dict[str, str] = {
    "ऄ": "a", "अ": "a", "आ": "aa", "इ": "i", "ई": "ee",
    "उ": "u", "ऊ": "oo", "ऋ": "ri", "ॠ": "ri", "ऌ": "li", "ॡ": "li",
    "ऍ": "e", "ऎ": "e", "ए": "e", "ऐ": "ai",
    "ऑ": "o", "ऒ": "o", "ओ": "o", "औ": "au",
    "ॲ": "a", "ॳ": "o", "ॴ": "oo", "ॵ": "au", "ॶ": "u", "ॷ": "oo",
    "ॐ": "om",  # ॐ
}

LABIALS = frozenset("पफबभम")
_DIGIT_BASE = 0x0966  # ०
_SPACE_CHARS = frozenset("।॥॰")  # । ॥ ॰
# Dropped without a trace (and skipped when looking at "the next character").
_SKIP_CHARS = frozenset({NUKTA, ZWJ, ZWNJ})
_DROP_CHARS = frozenset({"ऽ", "॑", "॒", "॓", "॔", "ॱ", VIRAMA})


def _is_vedic(ch: str) -> bool:
    o = ord(ch)
    return 0x1CD0 <= o <= 0x1CFF or 0xA8E0 <= o <= 0xA8FF


def has_devanagari(s: str | None) -> bool:
    """True when ``s`` contains any character of the Devanagari blocks
    (U+0900-U+097F, U+A8E0-U+A8FF)."""
    if not s:
        return False
    return any(0x0900 <= ord(ch) <= 0x097F or 0xA8E0 <= ord(ch) <= 0xA8FF for ch in s)


def _is_word_char(ch: str) -> bool:
    """Devanagari letters and signs (not digits or dandas) continue a word."""
    if ch in _SKIP_CHARS or _is_vedic(ch):
        return True
    o = ord(ch)
    return 0x0900 <= o <= 0x0963 or 0x0971 <= o <= 0x097F


def _skip(s: str, j: int) -> int:
    n = len(s)
    while j < n and (s[j] in _SKIP_CHARS or _is_vedic(s[j])):
        j += 1
    return j


def romanize(text: str) -> str:
    """Nepali-aware Devanagari -> lowercase ASCII romanisation for search keys.

    Not for display. See the module docstring for the exact rules.
    """
    if not text:
        return ""
    s = unicodedata.normalize("NFC", text)
    out: list[str] = []
    n = len(s)
    i = 0
    word_has_vowel = False  # a vowel was emitted since the last word boundary
    after_virama = False  # the previous consonant ended with a virama
    while i < n:
        ch = s[i]
        if ch in CONSONANTS:
            if ch == "ज" and s.startswith("्ञ", _skip(s, i + 1)):  # ज्ञ
                base = "gy"
                j = _skip(s, _skip(s, i + 1) + 2)
            else:
                base = CONSONANTS[ch]
                j = _skip(s, i + 1)
            out.append(base)
            nxt = s[j] if j < n else ""
            if nxt == VIRAMA:
                after_virama = True
                i = _skip(s, j + 1)
                continue
            if nxt in VOWEL_SIGNS:
                out.append(VOWEL_SIGNS[nxt])
                word_has_vowel = True
                j += 1
            elif not (nxt and _is_word_char(nxt)) and word_has_vowel and not after_virama:
                pass  # schwa deletion: word-final consonant
            else:
                out.append("a")
                word_has_vowel = True
            after_virama = False
            i = j
            continue

        if ch in _SKIP_CHARS or _is_vedic(ch):
            i += 1
            continue
        after_virama = False
        if ch in INDEPENDENT_VOWELS:
            out.append(INDEPENDENT_VOWELS[ch])
            word_has_vowel = True
        elif ch in VOWEL_SIGNS:  # stray sign with no consonant before it
            out.append(VOWEL_SIGNS[ch])
            word_has_vowel = True
        elif ch == ANUSVARA:
            j = _skip(s, i + 1)
            out.append("m" if j < n and s[j] in LABIALS else "n")
        elif ch in (CHANDRABINDU, INVERTED_CHANDRABINDU):
            out.append("n")
        elif ch == VISARGA:
            out.append("h")
        elif ch in _DROP_CHARS:
            pass
        elif 0x0966 <= ord(ch) <= 0x096F:
            out.append(chr(ord("0") + ord(ch) - _DIGIT_BASE))
            word_has_vowel = False
        elif ch in _SPACE_CHARS:
            out.append(" ")
            word_has_vowel = False
        elif 0x0900 <= ord(ch) <= 0x097F:
            pass  # unassigned code points in the block
        else:
            out.append(ch.lower())
            word_has_vowel = False
        i += 1
    return "".join(out)


# ---------------------------------------------------------------------------
# Folding
# ---------------------------------------------------------------------------
_APOSTROPHES = re.compile("['`‘’ʼ]")
_NON_ALNUM = re.compile(r"[^a-z0-9]+")

# (pattern, replacement), applied in order; see the module docstring.
PHONETIC_RULES: tuple[tuple[str, str], ...] = (
    (r"wo", "wa"),
    (r"ow(?![aeiou])", "o"),
    (r"ou", "au"),
    (r"ee", "i"),
    (r"oo", "u"),
    (r"[vw]", "b"),
    (r"z", "j"),
    (r"f", "p"),
    (r"([kgjtdbps])h", r"\1"),
    (r"([a-z])\1+", r"\1"),
)
_COMPILED_RULES = tuple((re.compile(p), r) for p, r in PHONETIC_RULES)


def _phonetic(s: str) -> str:
    while True:
        prev = s
        for rx, rep in _COMPILED_RULES:
            s = rx.sub(rep, s)
        if s == prev:
            return s


def clean(s: str) -> str:
    """Steps 1-5 of ``fold``: script and case normalisation without the phonetic rules."""
    if not s:
        return ""
    s = unicodedata.normalize("NFC", s)
    if has_devanagari(s):
        s = romanize(s)
    s = unicodedata.normalize("NFKD", s)
    s = "".join(ch for ch in s if unicodedata.category(ch) != "Mn")
    s = s.lower()
    s = _APOSTROPHES.sub("", s)
    return _NON_ALNUM.sub(" ", s).strip()


def fold(s: str) -> str:
    """Canonical search key: lowercase ASCII words separated by single spaces.

    Idempotent. See the module docstring for the exact steps.
    """
    c = clean(s)
    return _phonetic(c) if c else ""
