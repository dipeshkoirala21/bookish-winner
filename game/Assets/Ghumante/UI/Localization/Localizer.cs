using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ghumante.UI.Localization
{
    /// <summary>
    /// Dictionary-based string lookup for M0 (ARCHITECTURE.md 7.9). It is deliberately small: the Unity
    /// Localization package replaces it in M1, reading the same keys. Until then:
    /// <list type="bullet">
    /// <item>String tables are flat JSON files, one per locale (<c>UI/Localization/strings.&lt;locale&gt;.json</c>).
    /// <c>tools/check_localization.py</c> fails CI when a key is missing from either table.</item>
    /// <item>UXML text elements carry their key in <c>binding-path</c>; <see cref="Apply"/> fills them in.</item>
    /// <item>Nepali numerals (०-९) and lakh grouping (12,34,567) are a per-locale option.</item>
    /// </list>
    /// A missing key falls back to English, then to the key itself in brackets, so gaps are visible.
    /// </summary>
    public sealed class Localizer
    {
        public const string English = "en";
        public const string Nepali = "ne";

        /// <summary>USS class put on the root of every localised tree, e.g. <c>gh-lang-ne</c>.</summary>
        public const string LanguageClassPrefix = "gh-lang-";

        private readonly Dictionary<string, Dictionary<string, string>> _tables =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        private string _locale = English;
        private bool _nativeNumerals = true;

        /// <summary>Raised after the locale or the numeral option changes.</summary>
        public event Action Changed;

        /// <summary>Current locale code (<c>en</c> or <c>ne</c>).</summary>
        public string Locale
        {
            get { return _locale; }
        }

        /// <summary>
        /// When true (the default) and the locale is Nepali, numbers use Devanagari digits and lakh grouping.
        /// </summary>
        public bool NativeNumerals
        {
            get { return _nativeNumerals; }
            set
            {
                if (_nativeNumerals == value) return;
                _nativeNumerals = value;
                RaiseChanged();
            }
        }

        /// <summary>True when numbers are currently rendered with Devanagari digits.</summary>
        public bool UsesDevanagariDigits
        {
            get { return _locale == Nepali && _nativeNumerals; }
        }

        public IEnumerable<string> Locales
        {
            get { return _tables.Keys; }
        }

        /// <summary>Adds or replaces the table of a locale from flat JSON text.</summary>
        public void AddTable(string locale, string json)
        {
            if (string.IsNullOrEmpty(locale)) throw new ArgumentException("Locale must not be empty.", nameof(locale));
            _tables[locale] = FlatJson.Parse(json);
        }

        /// <summary>Convenience for TextAssets referenced from the Bootstrap scene.</summary>
        public void AddTable(string locale, TextAsset json)
        {
            if (json == null)
            {
                Debug.LogError("Localizer: string table for '" + locale + "' is not assigned.");
                return;
            }
            AddTable(locale, json.text);
        }

        public bool HasTable(string locale)
        {
            return _tables.ContainsKey(locale);
        }

        public void SetLocale(string locale)
        {
            if (!_tables.ContainsKey(locale))
            {
                Debug.LogWarning("Localizer: no string table for locale '" + locale + "'; keeping '" + _locale + "'.");
                return;
            }
            if (_locale == locale) return;
            _locale = locale;
            RaiseChanged();
        }

        /// <summary>Picks Nepali for devices set to Nepali or Hindi, English otherwise.</summary>
        public static string LocaleForSystemLanguage(SystemLanguage language)
        {
            // Unity has no SystemLanguage.Nepali; Hindi-language devices read Devanagari too.
            return language == SystemLanguage.Hindi ? Nepali : English;
        }

        public bool Has(string key)
        {
            Dictionary<string, string> table;
            return _tables.TryGetValue(_locale, out table) && table.ContainsKey(key);
        }

        /// <summary>Returns the string for <paramref name="key"/> in the current locale.</summary>
        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            string value;
            Dictionary<string, string> table;
            if (_tables.TryGetValue(_locale, out table) && table.TryGetValue(key, out value)) return value;
            if (_locale != English && _tables.TryGetValue(English, out table) && table.TryGetValue(key, out value)) return value;
            return "[" + key + "]";
        }

        /// <summary>
        /// <see cref="string.Format(IFormatProvider,string,object[])"/> on a localised pattern. Integer
        /// arguments are formatted with <see cref="FormatNumber"/> so they follow the numeral option.
        /// </summary>
        public string Format(string key, params object[] args)
        {
            var converted = new object[args == null ? 0 : args.Length];
            for (int i = 0; i < converted.Length; i++)
            {
                object a = args[i];
                if (a is int) converted[i] = FormatNumber((int)a);
                else if (a is long) converted[i] = FormatNumber((long)a);
                else converted[i] = a;
            }
            return string.Format(CultureInfo.InvariantCulture, Get(key), converted);
        }

        /// <summary>
        /// Formats an integer with grouping: <c>1,234,567</c> in English; <c>१२,३४,५६७</c> in Nepali (lakh
        /// grouping, Devanagari digits), or <c>12,34,567</c> when <see cref="NativeNumerals"/> is off.
        /// </summary>
        public string FormatNumber(long value)
        {
            string digits = value < 0
                ? (value == long.MinValue ? "9223372036854775808" : (-value).ToString(CultureInfo.InvariantCulture))
                : value.ToString(CultureInfo.InvariantCulture);
            // Grouping follows the locale (Nepali uses lakh/crore grouping with either digit set);
            // the digit set follows the NativeNumerals option.
            string grouped = _locale == Nepali ? GroupLakh(digits) : GroupThousands(digits);
            if (value < 0) grouped = "-" + grouped;
            return UsesDevanagariDigits ? ToDevanagariDigits(grouped) : grouped;
        }

        /// <summary>Replaces ASCII digits 0-9 with Devanagari digits ०-९ (U+0966-U+096F).</summary>
        public static string ToDevanagariDigits(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                sb.Append(c >= '0' && c <= '9' ? (char)('०' + (c - '0')) : c);
            }
            return sb.ToString();
        }

        /// <summary>Applies digit conversion to free text when the numeral option is active.</summary>
        public string LocalizeDigits(string s)
        {
            return UsesDevanagariDigits ? ToDevanagariDigits(s) : s;
        }

        /// <summary>
        /// Fills every text element under <paramref name="root"/> whose <c>binding-path</c> holds a key, and
        /// sets the <c>gh-lang-*</c> class on the root so USS can tune Devanagari sizes.
        /// </summary>
        public void Apply(VisualElement root)
        {
            if (root == null) return;
            foreach (string locale in _tables.Keys)
            {
                root.EnableInClassList(LanguageClassPrefix + locale, locale == _locale);
            }
            root.Query<TextElement>().ForEach(element =>
            {
                string key = element.bindingPath;
                if (!string.IsNullOrEmpty(key)) element.text = Get(key);
            });
        }

        private void RaiseChanged()
        {
            Action handler = Changed;
            if (handler != null) handler();
        }

        private static string GroupThousands(string digits)
        {
            var sb = new StringBuilder();
            int lead = digits.Length % 3;
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (i - lead) % 3 == 0) sb.Append(',');
                sb.Append(digits[i]);
            }
            return sb.ToString();
        }

        private static string GroupLakh(string digits)
        {
            // Last three digits, then groups of two: 1234567 -> 12,34,567.
            if (digits.Length <= 3) return digits;
            string head = digits.Substring(0, digits.Length - 3);
            string tail = digits.Substring(digits.Length - 3);
            var sb = new StringBuilder();
            int lead = head.Length % 2;
            for (int i = 0; i < head.Length; i++)
            {
                if (i > 0 && (i - lead) % 2 == 0) sb.Append(',');
                sb.Append(head[i]);
            }
            sb.Append(',').Append(tail);
            return sb.ToString();
        }
    }
}
