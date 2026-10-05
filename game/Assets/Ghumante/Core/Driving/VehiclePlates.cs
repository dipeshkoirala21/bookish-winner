using System.Text;

namespace Ghumante.Core.Driving
{
    /// <summary>
    /// A fictional Nepali number plate in the legacy painted format (W2_DESIGN 5.2, vehicles research §3.1):
    /// <c>बा ## X ####</c> with Devanagari numerals, where "बा" is the Bagmati zone code, ## a one- or two-digit lot, X the
    /// class letter (ownership × size) and #### the number. Colours follow the legacy code: private red with white text,
    /// public black with white, government white with red, corporation yellow with blue-black, tourist green with white.
    /// </summary>
    public struct PlateText
    {
        /// <summary>The whole plate on one line, e.g. "बा ३ च ४५६७".</summary>
        public string Full;

        /// <summary>The two-line layout of rear plates: "बा ३ च" over "४५६७".</summary>
        public string Line1, Line2;

        /// <summary>Background and text colours, 0xRRGGBB.</summary>
        public uint Background, Ink;

        /// <summary>Plate size in metres (rear plate; front plates of cars and heavies are wider and one line).</summary>
        public float WidthM, HeightM;

        public float FrontWidthM, FrontHeightM;
    }

    /// <summary>Builds deterministic fictional plates (never a real registration pattern beyond the public format).</summary>
    public static class VehiclePlates
    {
        /// <summary>Zone code (Bagmati).</summary>
        public const string Zone = "बा";

        private const string Digits = "०१२३४५६७८९";

        // Class letters [ownership, size]: private क/च/प, public ख/ज/फ, government ग/झ/ब. Corporation and tourist plates
        // use the public letters [E].
        private static readonly string[,] Letters =
        {
            { "क", "च", "प" },
            { "ख", "ज", "फ" },
            { "ग", "झ", "ब" },
            { "ख", "ज", "फ" },
            { "ख", "ज", "फ" },
        };

        /// <summary>The class letter for an ownership and size.</summary>
        public static string Letter(PlateOwnership o, PlateSize s)
        {
            int oi = (int)o, si = (int)s;
            if (oi < 0 || oi > 4) oi = 0;
            if (si < 0 || si > 2) si = 1;
            return Letters[oi, si];
        }

        /// <summary>Background and ink colours of the legacy code.</summary>
        public static void Colours(PlateOwnership o, out uint background, out uint ink)
        {
            switch (o)
            {
                case PlateOwnership.Public:
                    background = 0x111111;
                    ink = 0xFFFFFF;
                    return;
                case PlateOwnership.Government:
                    background = 0xFFFFFF;
                    ink = 0xC62828;
                    return;
                case PlateOwnership.Corporation:
                    background = 0xFDD835;
                    ink = 0x102040;
                    return;
                case PlateOwnership.Tourist:
                    background = 0x2E7D32;
                    ink = 0xFFFFFF;
                    return;
                default:
                    background = 0xC62828;
                    ink = 0xFFFFFF;
                    return;
            }
        }

        /// <summary>A number in Devanagari digits (non-negative).</summary>
        public static string Devanagari(int n)
        {
            if (n < 0) n = -n;
            if (n == 0) return Digits.Substring(0, 1);
            var sb = new StringBuilder(8);
            while (n > 0)
            {
                sb.Insert(0, Digits[n % 10]);
                n /= 10;
            }
            return sb.ToString();
        }

        /// <summary>The plate of a catalogue entry for an agent seed (the same seed always gives the same plate).</summary>
        public static PlateText For(in VehicleCatalogEntry e, uint seed)
        {
            PlateText p = Make(e.Plate, e.PlateSize, seed);
            if (e.Shape == BodyShape.Tempo || e.Shape == BodyShape.Rickshaw)
            {
                // Three-wheelers carry one small plate (0.24 × 0.13 m).
                p.WidthM = 0.24f;
                p.HeightM = 0.13f;
                p.FrontWidthM = 0f;
                p.FrontHeightM = 0f;
            }
            return p;
        }

        /// <summary>A plate for an ownership and size from a seed: lot 1–99 for two-wheelers, 1–12 for light and 1–9
        /// for heavy vehicles; number 1000–9999.</summary>
        public static PlateText Make(PlateOwnership o, PlateSize s, uint seed)
        {
            uint h1 = VehicleCatalogEntry.Mix(seed ^ 0x504C4154u), h2 = VehicleCatalogEntry.Mix(h1 + 0x9E3779B9u);
            int lots = s == PlateSize.TwoWheeler ? 99 : s == PlateSize.Light ? 12 : 9;
            int lot = 1 + (int)(h1 % (uint)lots);
            int number = 1000 + (int)(h2 % 9000u);
            string head = Zone + " " + Devanagari(lot) + " " + Letter(o, s);
            string tail = Devanagari(number);
            var p = new PlateText { Line1 = head, Line2 = tail, Full = head + " " + tail };
            Colours(o, out p.Background, out p.Ink);
            switch (s)
            {
                case PlateSize.Heavy:
                    p.WidthM = 0.36f;
                    p.HeightM = 0.21f;
                    p.FrontWidthM = 0.52f;
                    p.FrontHeightM = 0.11f;
                    break;
                case PlateSize.TwoWheeler:
                    p.WidthM = 0.20f;
                    p.HeightM = 0.13f;
                    break;
                default:
                    p.WidthM = 0.30f;
                    p.HeightM = 0.185f;
                    p.FrontWidthM = 0.45f;
                    p.FrontHeightM = 0.11f;
                    break;
            }
            return p;
        }
    }
}
