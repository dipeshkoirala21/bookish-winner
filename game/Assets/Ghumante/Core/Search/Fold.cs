using System;
using System.Globalization;
using System.Text;

namespace Ghumante.Core.Search
{
    /// <summary>
    /// Canonical search keys: an exact port of <c>translit.clean</c> and <c>translit.fold</c>. The result is
    /// lowercase ASCII words separated by single spaces, so "Kathmandoo", "Boudha"/"बौद्ध" and so on collapse
    /// onto the same keys as in the pipeline's search index. Idempotent.
    /// </summary>
    public static class Fold
    {
        /// <summary>Steps 1-10 of translit.fold.</summary>
        public static string Apply(string s)
        {
            string c = Clean(s);
            return c.Length == 0 ? "" : Phonetic(c);
        }

        /// <summary>Steps 1-5 (script and case normalisation without the phonetic rules).</summary>
        public static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = Nfc(s);
            if (Romanize.HasDevanagari(s)) s = Romanize.Apply(s);
            s = Normalize(s, NormalizationForm.FormKD);

            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                // Drop non-spacing marks (category Mn), per code point.
                int len = char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]) ? 2 : 1;
                if (CharUnicodeInfo.GetUnicodeCategory(s, i) != UnicodeCategory.NonSpacingMark)
                    sb.Append(s, i, len);
                i += len - 1;
            }
            string lower = sb.ToString().ToLowerInvariant();

            // Delete apostrophes, replace runs outside [a-z0-9] by one space, trim.
            sb.Clear();
            bool pendingSpace = false;
            foreach (char ch in lower)
            {
                if (ch == '\'' || ch == '`' || ch == '‘' || ch == '’' || ch == 'ʼ') continue;
                if (ch >= 'a' && ch <= 'z' || ch >= '0' && ch <= '9')
                {
                    if (pendingSpace && sb.Length > 0) sb.Append(' ');
                    pendingSpace = false;
                    sb.Append(ch);
                }
                else
                {
                    pendingSpace = true;
                }
            }
            return sb.ToString();
        }

        /// <summary>The phonetic rules (translit.PHONETIC_RULES), repeated until nothing changes.</summary>
        public static string Phonetic(string s)
        {
            while (true)
            {
                string prev = s;
                s = Replace2(s, 'w', 'o', "wa"); // 1: wo -> wa
                s = OwNotBeforeVowel(s); // 2: ow(?![aeiou]) -> o
                s = Replace2(s, 'o', 'u', "au"); // 3: ou -> au
                s = Replace2(s, 'e', 'e', "i"); // 4: ee -> i
                s = Replace2(s, 'o', 'o', "u"); // 5: oo -> u
                s = s.Replace('v', 'b').Replace('w', 'b'); // 6
                s = s.Replace('z', 'j'); // 7
                s = s.Replace('f', 'p'); // 8
                s = DropAspirateH(s); // 9: ([kgjtdbps])h -> \1
                s = CollapseDoubles(s); // 10: ([a-z])\1+ -> \1
                if (s == prev) return s;
            }
        }

        // Global, left-to-right, non-overlapping replacement of the two-letter pattern ab.
        private static string Replace2(string s, char a, char b, string rep)
        {
            if (s.IndexOf(a) < 0) return s;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == a && i + 1 < s.Length && s[i + 1] == b)
                {
                    sb.Append(rep);
                    i++;
                }
                else
                {
                    sb.Append(s[i]);
                }
            }
            return sb.ToString();
        }

        private static bool IsVowel(char c)
        {
            return c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u';
        }

        private static string OwNotBeforeVowel(string s)
        {
            if (s.IndexOf('o') < 0) return s;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == 'o' && i + 1 < s.Length && s[i + 1] == 'w' && !(i + 2 < s.Length && IsVowel(s[i + 2])))
                {
                    sb.Append('o');
                    i++;
                }
                else
                {
                    sb.Append(s[i]);
                }
            }
            return sb.ToString();
        }

        private static bool IsAspirateBase(char c)
        {
            return c == 'k' || c == 'g' || c == 'j' || c == 't' || c == 'd' || c == 'b' || c == 'p' || c == 's';
        }

        private static string DropAspirateH(string s)
        {
            if (s.IndexOf('h') < 0) return s;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                sb.Append(s[i]);
                if (IsAspirateBase(s[i]) && i + 1 < s.Length && s[i + 1] == 'h') i++;
            }
            return sb.ToString();
        }

        private static string CollapseDoubles(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (i > 0 && c == s[i - 1] && c >= 'a' && c <= 'z') continue;
                sb.Append(c);
            }
            return sb.ToString();
        }

        internal static string Nfc(string s)
        {
            return Normalize(s, NormalizationForm.FormC);
        }

        private static string Normalize(string s, NormalizationForm form)
        {
            try
            {
                return s.Normalize(form);
            }
            catch (ArgumentException)
            {
                return s; // ill-formed UTF-16 (lone surrogates): leave it, the ASCII filter drops it anyway
            }
        }
    }
}
