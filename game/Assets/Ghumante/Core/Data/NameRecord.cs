using System;

namespace Ghumante.Core.Data
{
    /// <summary>
    /// A <c>(default, en, ne)</c> name triple as stored in NAME chunks, the search index and the routing graph.
    /// <c>Default</c> is OSM <c>name=*</c>, <c>En</c> the English name, <c>Ne</c> the Nepali (Devanagari) name.
    /// </summary>
    public sealed class NameRecord : IEquatable<NameRecord>
    {
        public readonly string Default;
        public readonly string En;
        public readonly string Ne;

        public NameRecord(string @default, string en, string ne)
        {
            Default = @default ?? "";
            En = en ?? "";
            Ne = ne ?? "";
        }

        public bool IsEmpty
        {
            get { return Default.Length == 0 && En.Length == 0 && Ne.Length == 0; }
        }

        /// <summary>The name to show for a locale: Nepali when asked for and present, else English, else default
        /// (ADR-005: never machine-transliterated).</summary>
        public string Display(bool nepali)
        {
            if (nepali && Ne.Length > 0) return Ne;
            if (En.Length > 0) return En;
            return Default.Length > 0 ? Default : Ne;
        }

        public bool Equals(NameRecord other)
        {
            return other != null && Default == other.Default && En == other.En && Ne == other.Ne;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as NameRecord);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Default.GetHashCode() * 397 ^ En.GetHashCode()) * 397 ^ Ne.GetHashCode();
            }
        }

        public override string ToString()
        {
            return "(" + Default + ", " + En + ", " + Ne + ")";
        }

        internal static NameRecord Read(BinReader r)
        {
            string d = r.Str();
            string en = r.Str();
            string ne = r.Str();
            return new NameRecord(d, en, ne);
        }
    }
}
