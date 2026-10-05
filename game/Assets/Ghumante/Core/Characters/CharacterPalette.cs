namespace Ghumante.Core.Characters
{
    /// <summary>
    /// Colours of the character (P §2.5–2.7, street_life §3.3), as 0xRRGGBB. Skin swatches are numbered 1–10 and carry
    /// no group names; each has a toon-ramp shadow and a cheek blush. Outfit palettes are per item; a recipe's colour
    /// index wraps into its item's palette (<see cref="ColourCount"/>). The vertex colours of
    /// <see cref="HumanoidMesher"/> come from here as 0xRRGGBBAA.
    /// </summary>
    public static class CharacterPalette
    {
        /// <summary>Skin swatches #1..#10 (index 0..9): base, from light olive to deep brown.</summary>
        public static readonly uint[] SkinBase =
        {
            0xF3D3B5, 0xEBC39E, 0xE0B48C, 0xD4A276, 0xC69064, 0xB67F55, 0xA26D47, 0x8C5A3A, 0x744830, 0x5C3826,
        };

        /// <summary>Toon-ramp shadow per swatch.</summary>
        public static readonly uint[] SkinShadow =
        {
            0xD9A98A, 0xCC9A76, 0xBF8A66, 0xB07A55, 0xA06A47, 0x8E5B3B, 0x7C4C30, 0x683C26, 0x55301F, 0x40241A,
        };

        /// <summary>Cheek blush per swatch.</summary>
        public static readonly uint[] SkinBlush =
        {
            0xF2A7A0, 0xEE9C92, 0xE8918A, 0xDE8678, 0xD27A6C, 0xC46E60, 0xB66556, 0xA2594C, 0x8E4E43, 0x7A4239,
        };

        /// <summary>Hair: black, soft black, dark brown, brown, auburn, grey, white, teal (the collection unlock).</summary>
        public static readonly uint[] Hair = { 0x1B1A1C, 0x2A2422, 0x3B2A22, 0x5A3E2E, 0x7A3B24, 0x8E8E8E, 0xE6E2DA, 0x2A8C8C };

        /// <summary>Helmets (auto-equipped on every two-wheeler mount).</summary>
        public static readonly uint[] Helmet = { 0xE53935, 0x1E88E5, 0xFDD835, 0xFAFAFA, 0x212121, 0x43A047, 0xFB8C00, 0xEC6FA0 };

        /// <summary>Dhaka topi base colours (red, navy, black, mustard, green); the Bhadgaunle topi is plain black.</summary>
        public static readonly uint[] TopiBase = { 0xB5302B, 0x2E3A6B, 0x1E1E1E, 0xC99A2E, 0x3E7A4A };

        /// <summary>Dhaka lattice dot colours (white, yellow, plus red and green on dark bases).</summary>
        /// <summary>The Bhadgaunle topi is plain black.</summary>
        public static readonly uint[] Bhadgaunle = { 0x1A1A1A };

        public static readonly uint[] TopiPatterns = { 0xF4EFE0, 0xF2C230, 0xD93A2B, 0x3FA35C };

        public static readonly uint[] DhakaJacket = { 0x2B3550, 0x3A3D42 };
        public static readonly uint[] Kurta = { 0xF4B6C2, 0xA8D5E2, 0xF7D488, 0xB5E3B0, 0xE3C1F0, 0xF5F2EA };
        public static readonly uint[] Daura = { 0xEDE6D6, 0xC9C6BE, 0xB8C8D8, 0xD9C7A3 };
        public static readonly uint[] Waistcoat = { 0x3A3D42, 0x2B3550 };
        public static readonly uint[] Sari = { 0xE23B2A, 0xF49AC1, 0xF6A21B, 0x8E24AA, 0x2E9E4F, 0xFFD95A, 0xC2185B, 0x1E1E1E };

        /// <summary>Sari borders (the hakupatasi's red border on black is colour 7 with border 0).</summary>
        public static readonly uint[] SariBorder = { 0xB0201A, 0xD9A93A, 0x2E3A6B };

        /// <summary>Monk maroon and saffron, sadhu saffron.</summary>
        public static readonly uint[] Robe = { 0x7A1F1F, 0xE07B1A };

        /// <summary>School and police uniforms (generic, no insignia).</summary>
        public static readonly uint[] Uniform = { 0x7FC8F8, 0xF7F6F0, 0x1F3A93, 0x1F3A93 };

        /// <summary>T-shirts and hoodies: 12 brights.</summary>
        public static readonly uint[] Brights =
        {
            0xE53935, 0x1E88E5, 0xFDD835, 0x43A047, 0xFB8C00, 0x8E24AA, 0x00ACC1, 0xEC6FA0, 0xF5F5F2, 0x2E3440, 0x7CB342, 0x6D4C41,
        };

        /// <summary>Jeans, joggers and shorts: denim, black, khaki, grey.</summary>
        public static readonly uint[] Trousers = { 0x3E5C8A, 0x24262B, 0xB8A67E, 0x7C8088 };

        /// <summary>Sneaker uppers (soles are white): red, blue, yellow, white, black, teal, purple.</summary>
        public static readonly uint[] Sneakers = { 0xE53935, 0x1E88E5, 0xFDD835, 0xF5F5F2, 0x24262B, 0x2A9D8F, 0x7B4FA0 };

        public static readonly uint[] Chappal = { 0x2A5DB0, 0xC62828, 0x24262B };
        public static readonly uint[] Boots = { 0x5A4636, 0x6E6E70 };
        public static readonly uint[] Daypack = { 0xE53935, 0x1E88E5, 0xFDD835, 0x43A047, 0xFB8C00, 0x00ACC1 };

        /// <summary>Doko (bamboo basket).</summary>
        public static readonly uint[] Doko = { 0xB8935A, 0xA0784A };

        public static readonly uint[] SunHat = { 0xD9C7A3, 0xF5F2EA, 0x7CB342 };
        public static readonly uint[] Beanie = { 0xC62828, 0x2E3A6B, 0x3E7A4A, 0xF2C230 };

        public const uint SoleWhite = 0xF5F5F2, EyeWhite = 0xFDFCF8, Pupil = 0x1E1A18, Mouth = 0x7A2A2A, Lashes = 0x1E1A18;
        public const uint Tassel = 0xD93A2B, Lace = 0xF5F5F2, Zip = 0xD9A93A, TieString = 0xEDE6D6, Strap = 0x3A2E2A;

        /// <summary>The palette of an item (null for <see cref="OutfitItem.None"/>).</summary>
        public static uint[] Of(OutfitItem item)
        {
            switch (item)
            {
                case OutfitItem.DhakaTopi: return TopiBase;
                case OutfitItem.BhadgaunleTopi: return Bhadgaunle;
                case OutfitItem.Beanie: return Beanie;
                case OutfitItem.SunHat: return SunHat;
                case OutfitItem.TShirt:
                case OutfitItem.Hoodie: return Brights;
                case OutfitItem.DhakaJacket: return DhakaJacket;
                case OutfitItem.Kurta: return Kurta;
                case OutfitItem.Daura: return Daura;
                case OutfitItem.Sari: return Sari;
                case OutfitItem.Robe: return Robe;
                case OutfitItem.Uniform: return Uniform;
                case OutfitItem.Jeans:
                case OutfitItem.Joggers:
                case OutfitItem.Shorts: return Trousers;
                case OutfitItem.Suruwal: return Daura;
                case OutfitItem.Sneakers: return Sneakers;
                case OutfitItem.Chappal: return Chappal;
                case OutfitItem.TrekBoots: return Boots;
                case OutfitItem.Daypack: return Daypack;
                case OutfitItem.Doko: return Doko;
                default: return null;
            }
        }

        /// <summary>Colours an item offers (0 for none).</summary>
        public static int ColourCount(OutfitItem item)
        {
            uint[] p = Of(item);
            return p == null ? 0 : p.Length;
        }

        /// <summary>The item's colour at <paramref name="index"/> (wrapped), or <paramref name="fallback"/>.</summary>
        public static uint Colour(OutfitItem item, int index, uint fallback = 0x808080)
        {
            uint[] p = Of(item);
            if (p == null || p.Length == 0) return fallback;
            return p[((index % p.Length) + p.Length) % p.Length];
        }

        /// <summary>0xRRGGBB to the mesh's 0xRRGGBBAA (opaque).</summary>
        public static uint Rgba(uint rgb)
        {
            return (rgb << 8) | 0xFF;
        }

        /// <summary>Darkens (factor &lt; 1) or lightens (&gt; 1) a 0xRRGGBB colour.</summary>
        public static uint Shade(uint rgb, float factor)
        {
            int r = (int)((rgb >> 16) & 0xFF), g = (int)((rgb >> 8) & 0xFF), b = (int)(rgb & 0xFF);
            r = Clamp((int)(r * factor + 0.5f));
            g = Clamp((int)(g * factor + 0.5f));
            b = Clamp((int)(b * factor + 0.5f));
            return (uint)(r << 16 | g << 8 | b);
        }

        private static int Clamp(int v)
        {
            return v < 0 ? 0 : v > 255 ? 255 : v;
        }
    }
}
