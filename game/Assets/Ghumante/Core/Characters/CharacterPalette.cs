namespace Ghumante.Core.Characters
{
    /// <summary>
    /// Colours of the character (P §2.5–2.8, street_life §3.3, docs/research/w2/ref_characters.md), as 0xRRGGBB. Skin
    /// swatches are numbered 1–10 and carry no group names; each has a toon-ramp shadow and a cheek blush. Outfit
    /// palettes are per item; a recipe's colour index wraps into its item's palette (<see cref="ColourCount"/>) and its
    /// pattern index into the item's patterns (<see cref="PatternCount"/>: dhaka weaves, sari borders, coat colours,
    /// apron stripes, school ties). The vertex colours of <see cref="HumanoidMesher"/> come from here as 0xRRGGBBAA.
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

        /// <summary>Lip tint per swatch (a little deeper and rosier than the blush).</summary>
        public static readonly uint[] SkinLip =
        {
            0xD98880, 0xD07C70, 0xC77066, 0xBB665A, 0xAE5C50, 0x9E5246, 0x8E4A3E, 0x7A4036, 0x68372E, 0x552D26,
        };

        /// <summary>Hair: black, soft black, dark brown, brown, auburn, grey, white, teal (the collection unlock), blonde
        /// and light brown (visitors).</summary>
        public static readonly uint[] Hair = { 0x1B1A1C, 0x2A2422, 0x3B2A22, 0x5A3E2E, 0x7A3B24, 0x8E8E8E, 0xE6E2DA, 0x2A8C8C, 0xC9A060, 0x8A6440 };

        /// <summary>Irises: dark brown, brown, hazel, green, grey-blue.</summary>
        public static readonly uint[] Iris = { 0x3A2418, 0x5A3A22, 0x7A5A2E, 0x4E7A4A, 0x5A7A9A };

        /// <summary>Helmets (auto-equipped on every two-wheeler mount).</summary>
        public static readonly uint[] Helmet = { 0xE53935, 0x1E88E5, 0xFDD835, 0xFAFAFA, 0x212121, 0x43A047, 0xFB8C00, 0xEC6FA0 };

        /// <summary>Dhaka topi ground colours as photographed in the valley (ref_characters.md §2): cream, pale pink,
        /// light grey, white, and the rarer maroon and black grounds.</summary>
        public static readonly uint[] TopiBase = { 0xF1E6D6, 0xEBC9C0, 0xD9D6D0, 0xF4EFE6, 0x7A2433, 0x232226 };

        /// <summary>The Bhadgaunle (kalo) topi is plain black.</summary>
        public static readonly uint[] Bhadgaunle = { 0x1A1A1C };

        /// <summary>Number of Palpali dhaka weaves (motif colour sets).</summary>
        public const int DhakaWeaves = 6;

        /// <summary>Palpali dhaka motif colours, four per weave (diagonal bands of stepped diamonds woven in a few
        /// colours on the ground; ref_characters.md §2): Palpali classic (muted rose, salmon, slate, sage, as on most topis worn today), red
        /// lattice (red, pink, white, black), ikat (slate, pink, teal, peach), check (pink, black, grey, yellow), earth
        /// (maroon, gold, black, cream), jade (green, rose, yellow, black).</summary>
        public static readonly uint[] DhakaMotifs =
        {
            0xB85A6E, 0xE8956E, 0x4A4048, 0x6E9A78,
            0xC62838, 0xF07C8C, 0xFFFFFF, 0x2A2224,
            0x5A6A7A, 0xD97A8A, 0x3E8E8A, 0xF2C1A0,
            0xD94A78, 0x2A2A2E, 0x9A9EA6, 0xF2C230,
            0x8A2E3A, 0xE8B04A, 0x2A2224, 0xEDE6D6,
            0x2E8B57, 0xD9455B, 0xF2C230, 0x2A2224,
        };

        /// <summary>Motif colour <paramref name="k"/> (0..3) of weave <paramref name="weave"/>.</summary>
        public static uint DhakaMotif(int weave, int k)
        {
            weave = ((weave % DhakaWeaves) + DhakaWeaves) % DhakaWeaves;
            return DhakaMotifs[weave * 4 + (k & 3)];
        }

        /// <summary>Legacy dhaka lattice colours (white, yellow, red, green).</summary>
        public static readonly uint[] TopiPatterns = { 0xF4EFE0, 0xF2C230, 0xD93A2B, 0x3FA35C };

        public static readonly uint[] DhakaJacket = { 0x2B3550, 0x3A3D42, 0x5A1F2A };

        /// <summary>Kurtas: pastels and the everyday deep colours (maroon, teal, mustard, navy), and the men's white.</summary>
        public static readonly uint[] Kurta = { 0xF4B6C2, 0xA8D5E2, 0xF7D488, 0xB5E3B0, 0xE3C1F0, 0xF5F2EA, 0x8E1F3A, 0x2A8C8C, 0xD9A03A, 0x2B3A6B };

        /// <summary>Suruwal and leggings under a kurta: white, cream, black, maroon, navy, mustard.</summary>
        public static readonly uint[] Suruwal = { 0xF5F2EA, 0xEDE6D6, 0x24262B, 0x7A1F2B, 0x2B3550, 0xC99A2E };

        /// <summary>Daura cloth (the suruwal matches): off-white, pale grey, light blue, beige.</summary>
        public static readonly uint[] Daura = { 0xEDE6D6, 0xC9C6BE, 0xB8C8D8, 0xD9C7A3 };

        /// <summary>The coat or waistcoat over the daura: charcoal, navy, brown, black.</summary>
        public static readonly uint[] Waistcoat = { 0x3A3D42, 0x2B3550, 0x5A4636, 0x232427 };

        /// <summary>Saris: red, magenta, orange, green, royal blue, yellow, maroon, purple, and the haku patasi black.</summary>
        public static readonly uint[] Sari = { 0xC8202F, 0xC2185B, 0xE8752A, 0x2E8B57, 0x2A5DB0, 0xF2C230, 0x7A1F2B, 0x6B3FA0, 0x1C1C1C };

        /// <summary>The haku patasi's colour index in <see cref="Sari"/> (black with the red border).</summary>
        public const byte HakuSari = 8;

        /// <summary>Sari borders: red, gold, navy, green (the haku patasi always takes the red).</summary>
        public static readonly uint[] SariBorder = { 0xB3141E, 0xD9A93A, 0x2E3A6B, 0x2E8B3E };

        /// <summary>Sari blouses (cholo): matching shades are derived; these are the contrast ones.</summary>
        public static readonly uint[] Blouse = { 0xD9A93A, 0xB3141E, 0xF5F2EA, 0x2A5DB0 };

        /// <summary>Haku patasi blouses: the dark print worn with the draped pallu, then the red, maroon and blue blouses of
        /// the patuka style (ref_characters.md §4).</summary>
        public static readonly uint[] HakuBlouse = { 0x33282E, 0xB3141E, 0x6E1F2E, 0x2A3A6B };

        /// <summary>Monk maroon and saffron, sadhu saffron; the monk's yellow dhonka shirt.</summary>
        public static readonly uint[] Robe = { 0x7A1F2B, 0xE07B1A };
        public const uint Dhonka = 0xE3A21A, DhonkaPiping = 0x2E5DA8;

        /// <summary>Chuba (Tibetan wrap dress): maroon, navy, black, brown, grey; its silk blouse colours; and the seven
        /// pangden apron stripes.</summary>
        public static readonly uint[] Chuba = { 0x5A1F2A, 0x2B3550, 0x2A2422, 0x5A4636, 0x6E6E70 };
        public static readonly uint[] ChubaBlouse = { 0xF2B6C6, 0xA8D5E2, 0xF5F2EA, 0xF2D27A };
        public static readonly uint[] Pangden = { 0xC62828, 0xE8752A, 0xF2C230, 0x2E8B57, 0x2A5DB0, 0xF4EFE0, 0x6B3FA0 };

        /// <summary>School and police uniforms (generic, no insignia).</summary>
        public static readonly uint[] Uniform = { 0x7FC8F8, 0xF7F6F0, 0x1F3A93, 0x1F3A93 };

        /// <summary>T-shirts and hoodies: what the valley wears (black, white, navy, grey, maroon, olive) and some brights.</summary>
        public static readonly uint[] Brights =
        {
            0x24262B, 0xF5F5F2, 0x2B3550, 0x7C8088, 0x7A1F2B, 0x6B6B45, 0xD93A2B, 0x1E5AA8, 0xF2C230, 0x2A8C8C, 0xC99A2E, 0xE86A9A,
        };

        /// <summary>Puffer and bomber jackets: black, navy, olive, maroon, red, grey.</summary>
        public static readonly uint[] Jacket = { 0x222326, 0x2B3550, 0x556B2F, 0x6E1F2A, 0xC62828, 0x6E7076 };

        /// <summary>Office blazers: charcoal, navy, black, grey, brown.</summary>
        public static readonly uint[] Blazer = { 0x3A3D42, 0x2B3550, 0x24262B, 0x7C8088, 0x5A4636 };

        /// <summary>Jeans: mid denim, dark denim, black, light denim, grey-blue.</summary>
        public static readonly uint[] Jeans = { 0x3E5C8A, 0x2B3E5E, 0x24262B, 0x7C9CC4, 0x5A6E8E };

        /// <summary>Joggers and shorts: denim, black, khaki, grey.</summary>
        public static readonly uint[] Trousers = { 0x3E5C8A, 0x24262B, 0xB8A67E, 0x7C8088 };

        /// <summary>Sneaker uppers (soles are white): white, black, navy, grey, red, blue, teal, maroon.</summary>
        public static readonly uint[] Sneakers = { 0xF5F5F2, 0x24262B, 0x2B3550, 0x9A9EA6, 0xD93A2B, 0x1E5AA8, 0x2A9D8F, 0x7A1F2B };

        /// <summary>Chappal straps: the classic blue, red, black, brown (the footbed is white or matching).</summary>
        public static readonly uint[] Chappal = { 0x2A5DB0, 0xC62828, 0x24262B, 0x6B4A2E };
        public static readonly uint[] Boots = { 0x5A4636, 0x6E6E70, 0x8A6440 };
        public static readonly uint[] Daypack = { 0x24262B, 0x2B3550, 0xD93A2B, 0x1E5AA8, 0x43A047, 0xFB8C00, 0x6E7076 };

        /// <summary>Doko (bamboo basket): fresh and weathered bamboo.</summary>
        public static readonly uint[] Doko = { 0xB8935A, 0xA0784A };

        public static readonly uint[] SunHat = { 0xD9C7A3, 0xF5F2EA, 0x7C8A4A, 0x2B3550 };
        public static readonly uint[] Cap = { 0x24262B, 0x2B3550, 0xD93A2B, 0xF5F2EA, 0xB8A67E, 0x556B2F };
        public static readonly uint[] PoliceCap = { 0x1F2D4F };
        public static readonly uint[] Headscarf = { 0xC2185B, 0xE8752A, 0x2E8B57, 0xC8202F, 0xF5F2EA, 0x6B3FA0, 0xF2C230 };

        /// <summary>Collared shirts: white, sky, pale grey, pale pink, maroon, navy, olive, stone.</summary>
        public static readonly uint[] Shirt = { 0xF5F5F2, 0x9EC9EA, 0xD8DCE4, 0xE9C9C9, 0x7A2E2E, 0x2B3550, 0x6B6B45, 0xC9C6BE };

        /// <summary>Traffic police shirt (blue) and the fluorescent vest with its silver tape (generic, no insignia).</summary>
        public static readonly uint[] PoliceShirt = { 0x6E9FD6 };
        public const uint PoliceVest = 0xC6E83A, PoliceTape = 0xD8DCE0, PoliceBand = 0xF2F0E8, PoliceTrousers = 0x1F2D4F;

        /// <summary>Trekking fleeces: outdoor brights (S §3.3).</summary>
        public static readonly uint[] Fleece = { 0xE2552D, 0x2F7FC1, 0xF2B705, 0x2E9E4F, 0x6B4E9A, 0x3A3D42 };

        /// <summary>Vests (earth tones of porters and farmers, plus white).</summary>
        public static readonly uint[] Vest = { 0x6E5A44, 0x8A7B5F, 0x4D5A3C, 0xF5F5F2, 0x7C8088 };

        /// <summary>School shirts (white, sky blue) with their tie stripe and trouser or skirt colours (navy, grey, maroon).</summary>
        public static readonly uint[] SchoolShirt = { 0xF5F5F2, 0x9EC9EA };
        public static readonly uint[] SchoolBottom = { 0x1F2D4F, 0x6B6E73, 0x6B1F2A };
        public static readonly uint[] SchoolTie = { 0x6B1F2A, 0x1F2D4F, 0x2E7A4A };

        /// <summary>Plain trousers: navy, charcoal, grey, khaki, brown, black.</summary>
        public static readonly uint[] PlainTrousers = { 0x1F2D4F, 0x2E3036, 0x6B6E73, 0xB8A67E, 0x5A4636, 0x222326 };

        /// <summary>Trekking trousers: khaki, olive, grey, black.</summary>
        public static readonly uint[] TrekTrousers = { 0xB8A67E, 0x6B6B45, 0x7C8088, 0x2E3036 };

        public static readonly uint[] LeatherShoes = { 0x1E1A18, 0x5A3A22 };

        /// <summary>Sacks (hessian), gas cylinders (LPG red) and baby slings (bright shawls).</summary>
        public static readonly uint[] Sack = { 0xC9B48A };
        public static readonly uint[] Gas = { 0xC0392B };
        public static readonly uint[] Sling = { 0xD84A7A, 0xE8752A, 0x2E9E4F, 0x8E24AA };

        /// <summary>Shawls and dupattas, glasses frames, earrings, pote beads, watch straps, mala beads.</summary>
        public static readonly uint[] Shawl = { 0xF5F2EA, 0xC2185B, 0x7A2E2E, 0xE8B04A, 0x4A5A7A, 0x2E9E4F, 0xC8202F };
        public const uint GlassesFrame = 0x2A2422, SunLens = 0x1E2A36, Gold = 0xE0B84A, PoteGreen = 0x2E8B3E, RedBead = 0xB3121B, Bindi = 0xC8102E, Sindoor = 0xD2001F;
        public const uint WatchStrap = 0x3A2E2A, MalaBead = 0x7A4A2A, Ash = 0xE8E4DA, Glove = 0xFAFAF6, Mask = 0xA9CBE8, TilakYellow = 0xF2C230;
        public static readonly uint[] Beanie = { 0xC62828, 0x2E3A6B, 0x3E7A4A, 0xF2C230 };

        /// <summary>Umbrella canopies: black (most), navy, maroon, a print; and the hand prayer wheel's brass.</summary>
        public static readonly uint[] Umbrella = { 0x1E1E22, 0x1E1E22, 0x2B3550, 0x7A1F2B, 0x2E8B57 };
        public const uint Brass = 0xC9A23A, Copper = 0xB06A3A;

        public const uint SoleWhite = 0xF5F5F2, SoleGum = 0x9A6A3E, EyeWhite = 0xFDFCF8, Pupil = 0x16120F, Mouth = 0x6E2626, Lashes = 0x1E1A18;

        /// <summary>The open mouth: cavity, teeth, tongue; plus fingernail and highlight tints.</summary>
        public const uint MouthInside = 0x4A1A1E, Teeth = 0xFBF8F0, Tongue = 0xD8686A, Highlight = 0xFFFFFF;
        public const uint Tassel = 0xD93A2B, Lace = 0xF5F5F2, Zip = 0xD9A93A, TieString = 0xEDE6D6, Strap = 0x3A2E2A, Ribbon = 0xC8202F;

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
                case OutfitItem.Chuba: return Chuba;
                case OutfitItem.Jacket: return Jacket;
                case OutfitItem.Blazer: return Blazer;
                case OutfitItem.Jeans: return Jeans;
                case OutfitItem.Joggers:
                case OutfitItem.Shorts: return Trousers;
                case OutfitItem.Suruwal: return Suruwal;
                case OutfitItem.Sneakers: return Sneakers;
                case OutfitItem.Chappal: return Chappal;
                case OutfitItem.TrekBoots: return Boots;
                case OutfitItem.Daypack: return Daypack;
                case OutfitItem.Doko: return Doko;
                case OutfitItem.Cap: return Cap;
                case OutfitItem.PoliceCap: return PoliceCap;
                case OutfitItem.Headscarf: return Headscarf;
                case OutfitItem.Shirt: return Shirt;
                case OutfitItem.PoliceUniform: return PoliceShirt;
                case OutfitItem.Fleece: return Fleece;
                case OutfitItem.Vest: return Vest;
                case OutfitItem.SchoolShirt: return SchoolShirt;
                case OutfitItem.Trousers: return PlainTrousers;
                case OutfitItem.Skirt: return SchoolBottom;
                case OutfitItem.TrekTrousers: return TrekTrousers;
                case OutfitItem.RolledTrousers: return Trousers;
                case OutfitItem.LeatherShoes: return LeatherShoes;
                case OutfitItem.Sack: return Sack;
                case OutfitItem.GasCylinder: return Gas;
                case OutfitItem.BabySling: return Sling;
                default: return null;
            }
        }

        /// <summary>Colours an item offers (0 for none).</summary>
        public static int ColourCount(OutfitItem item)
        {
            uint[] p = Of(item);
            return p == null ? 0 : p.Length;
        }

        /// <summary>Patterns an item offers (at least 1): dhaka weaves, sari borders, daura coats, chuba aprons, school
        /// ties, doko loads.</summary>
        public static int PatternCount(OutfitItem item)
        {
            switch (item)
            {
                case OutfitItem.DhakaTopi:
                case OutfitItem.DhakaJacket: return DhakaWeaves;
                case OutfitItem.Sari: return SariBorder.Length;
                case OutfitItem.Daura: return Waistcoat.Length;
                case OutfitItem.Chuba: return ChubaBlouse.Length;
                case OutfitItem.SchoolShirt: return SchoolTie.Length;
                case OutfitItem.Doko: return 3;
                case OutfitItem.Kurta: return Suruwal.Length;
                default: return 1;
            }
        }

        /// <summary>The item's colour at <paramref name="index"/> (wrapped), or <paramref name="fallback"/>.</summary>
        public static uint Colour(OutfitItem item, int index, uint fallback = 0x808080)
        {
            uint[] p = Of(item);
            if (p == null || p.Length == 0) return fallback;
            return p[((index % p.Length) + p.Length) % p.Length];
        }

        /// <summary>Entry <paramref name="index"/> of <paramref name="p"/>, wrapped.</summary>
        public static uint At(uint[] p, int index)
        {
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

        /// <summary>Linear mix of two 0xRRGGBB colours (t = 0 gives <paramref name="a"/>).</summary>
        public static uint Mix(uint a, uint b, float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            int r = (int)(((a >> 16) & 0xFF) * (1f - t) + ((b >> 16) & 0xFF) * t + 0.5f);
            int g = (int)(((a >> 8) & 0xFF) * (1f - t) + ((b >> 8) & 0xFF) * t + 0.5f);
            int bl = (int)((a & 0xFF) * (1f - t) + (b & 0xFF) * t + 0.5f);
            return (uint)(Clamp(r) << 16 | Clamp(g) << 8 | Clamp(bl));
        }

        /// <summary>Perceived brightness 0..1 of a 0xRRGGBB colour.</summary>
        public static float Luma(uint rgb)
        {
            return (0.299f * ((rgb >> 16) & 0xFF) + 0.587f * ((rgb >> 8) & 0xFF) + 0.114f * (rgb & 0xFF)) / 255f;
        }

        private static int Clamp(int v)
        {
            return v < 0 ? 0 : v > 255 ? 255 : v;
        }
    }
}
