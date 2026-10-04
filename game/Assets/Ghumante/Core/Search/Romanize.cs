using System.Text;

namespace Ghumante.Core.Search
{
    /// <summary>
    /// Nepali-aware Devanagari to lowercase ASCII romanisation for search keys: an exact port of
    /// <c>translit.romanize</c> (see that module's docstring for the rules). Never shown in the UI (ADR-005).
    /// </summary>
    public static class Romanize
    {
        private const char Virama = '्';
        private const char Nukta = '़';
        private const char Anusvara = 'ं';
        private const char Chandrabindu = 'ँ';
        private const char InvertedChandrabindu = 'ऀ';
        private const char Visarga = 'ः';
        private const char Zwnj = '‌';
        private const char Zwj = '‍';
        private const char Ja = 'ज';
        private const char Nya = 'ञ';

        // Tables indexed by (c - U+0900); null = not in the table.
        private static readonly string[] Consonants = new string[0x80];
        private static readonly string[] VowelSigns = new string[0x80];
        private static readonly string[] IndependentVowels = new string[0x80];

        static Romanize()
        {
            Set(Consonants, 0x0915, "k", "kh", "g", "gh", "ng", "ch", "chh", "j", "jh", "n", "t", "th", "d", "dh", "n",
                "t", "th", "d", "dh", "n", "n", "p", "ph", "b", "bh", "m", "y", "r", "r", "l", "l", "l", "w", "sh", "sh",
                "s", "h");
            // precomposed nukta letters (NFC decomposes them anyway) and rare additions to the block
            Set(Consonants, 0x0958, "k", "kh", "g", "j", "d", "dh", "ph", "y");
            Set(Consonants, 0x0978, "d", "j", "y", "g", "j", "", "d", "b");

            Set(VowelSigns, 0x093A, "o", "oo");
            Set(VowelSigns, 0x093E, "aa", "i", "ee", "u", "oo", "ri", "ri", "e", "e", "e", "ai", "o", "o", "o", "au");
            Set(VowelSigns, 0x094E, "e", "au");
            Set(VowelSigns, 0x0955, "e", "u", "oo");
            Set(VowelSigns, 0x0962, "li", "li");

            Set(IndependentVowels, 0x0904, "a", "a", "aa", "i", "ee", "u", "oo", "ri", "li", "e", "e", "e", "ai", "o",
                "o", "o", "au");
            Set(IndependentVowels, 0x0950, "om");
            Set(IndependentVowels, 0x0960, "ri", "li");
            Set(IndependentVowels, 0x0972, "a", "o", "oo", "au", "u", "oo");
        }

        private static void Set(string[] table, int first, params string[] values)
        {
            for (int i = 0; i < values.Length; i++) table[first - 0x0900 + i] = values[i];
        }

        private static string Lookup(string[] table, char c)
        {
            int k = c - 0x0900;
            return k >= 0 && k < 0x80 ? table[k] : null;
        }

        private static bool IsVedic(char c)
        {
            return c >= '᳐' && c <= '᳿' || c >= '꣠' && c <= 'ꣿ';
        }

        private static bool IsSkip(char c)
        {
            return c == Nukta || c == Zwj || c == Zwnj || IsVedic(c);
        }

        private static bool IsDrop(char c)
        {
            return c == 'ऽ' || c == '॑' || c == '॒' || c == '॓' || c == '॔' || c == 'ॱ'
                   || c == Virama;
        }

        private static bool IsLabial(char c)
        {
            return c == 'प' || c == 'फ' || c == 'ब' || c == 'भ' || c == 'म';
        }

        /// <summary>True for any character of the Devanagari blocks (U+0900-U+097F, U+A8E0-U+A8FF).</summary>
        public static bool HasDevanagari(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s)
                if (c >= 'ऀ' && c <= 'ॿ' || c >= '꣠' && c <= 'ꣿ') return true;
            return false;
        }

        /// <summary>Devanagari letters and signs (not digits or dandas) continue a word.</summary>
        private static bool IsWordChar(char c)
        {
            if (IsSkip(c)) return true;
            return c >= 'ऀ' && c <= 'ॣ' || c >= 'ॱ' && c <= 'ॿ';
        }

        private static int Skip(string s, int j)
        {
            while (j < s.Length && IsSkip(s[j])) j++;
            return j;
        }

        /// <summary>Romanise <paramref name="text"/> (NFC-normalised first).</summary>
        public static string Apply(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string s = Fold.Nfc(text);
            var out_ = new StringBuilder(s.Length * 2);
            int n = s.Length;
            int i = 0;
            bool wordHasVowel = false;
            bool afterVirama = false;
            while (i < n)
            {
                char ch = s[i];
                string cons = Lookup(Consonants, ch);
                if (cons != null)
                {
                    int j;
                    int k = Skip(s, i + 1);
                    if (ch == Ja && k + 1 < n && s[k] == Virama && s[k + 1] == Nya)
                    {
                        cons = "gy";
                        j = Skip(s, k + 2);
                    }
                    else
                    {
                        j = k;
                    }
                    out_.Append(cons);
                    char nxt = j < n ? s[j] : '\0';
                    bool hasNext = j < n;
                    if (hasNext && nxt == Virama)
                    {
                        afterVirama = true;
                        i = Skip(s, j + 1);
                        continue;
                    }
                    string sign = hasNext ? Lookup(VowelSigns, nxt) : null;
                    if (sign != null)
                    {
                        out_.Append(sign);
                        wordHasVowel = true;
                        j++;
                    }
                    else if (!(hasNext && IsWordChar(nxt)) && wordHasVowel && !afterVirama)
                    {
                        // schwa deletion: word-final consonant
                    }
                    else
                    {
                        out_.Append('a');
                        wordHasVowel = true;
                    }
                    afterVirama = false;
                    i = j;
                    continue;
                }

                if (IsSkip(ch))
                {
                    i++;
                    continue;
                }
                afterVirama = false;
                string v;
                if ((v = Lookup(IndependentVowels, ch)) != null)
                {
                    out_.Append(v);
                    wordHasVowel = true;
                }
                else if ((v = Lookup(VowelSigns, ch)) != null) // stray sign with no consonant before it
                {
                    out_.Append(v);
                    wordHasVowel = true;
                }
                else if (ch == Anusvara)
                {
                    int j = Skip(s, i + 1);
                    out_.Append(j < n && IsLabial(s[j]) ? 'm' : 'n');
                }
                else if (ch == Chandrabindu || ch == InvertedChandrabindu)
                {
                    out_.Append('n');
                }
                else if (ch == Visarga)
                {
                    out_.Append('h');
                }
                else if (IsDrop(ch))
                {
                }
                else if (ch >= '०' && ch <= '९')
                {
                    out_.Append((char)('0' + (ch - '०')));
                    wordHasVowel = false;
                }
                else if (ch == '।' || ch == '॥' || ch == '॰')
                {
                    out_.Append(' ');
                    wordHasVowel = false;
                }
                else if (ch >= 'ऀ' && ch <= 'ॿ')
                {
                    // unassigned code points in the block
                }
                else
                {
                    // Python str.lower uses full case mapping: U+0130 is the one letter that lowercases to two.
                    if (ch == '\u0130') out_.Append("i\u0307");
                    else out_.Append(char.ToLowerInvariant(ch));
                    wordHasVowel = false;
                }
                i++;
            }
            return out_.ToString();
        }
    }
}
