using System;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;

namespace Ghumante.Core.Characters
{
    /// <summary>
    /// How people dress in a part of the valley (docs/research/w2/ref_characters.md §1, street_life §1.3): the crowd's
    /// wardrobe follows the place, not only the archetype the simulation picked. Append-only.
    /// </summary>
    public enum StreetStyle : byte
    {
        /// <summary>Ordinary modern Kathmandu (jeans, jackets, kurta suruwal).</summary>
        Urban = 0,

        /// <summary>The Asan–Indrachowk–Basantapur bazaar: more topis, shawls, chappals and elders.</summary>
        OldBazaar = 1,

        /// <summary>Bhaktapur, Patan, Kirtipur, Thimi, Khokana and Bungamati: the black Bhadgaunle topi on men, haku patasi
        /// and shawls on older women, chappals on brick lanes.</summary>
        NewarTown = 2,

        /// <summary>Thamel and Freak Street: trekking gear, sun hats and daypacks; shopkeepers in jackets.</summary>
        Tourist = 3,

        /// <summary>Boudha and Swayambhu: Tibetan chuba with a striped apron, prayer beads and hand prayer wheels on the
        /// kora.</summary>
        Buddhist = 4,

        /// <summary>Durbar Marg, Putalisadak, Singha Durbar, New Baneshwor and Pulchowk: shirts, trousers, blazers and
        /// leather shoes; kurta or sari for women.</summary>
        Office = 5,

        /// <summary>Peri-urban roads: casual, more chappals and topis.</summary>
        Suburb = 6,

        /// <summary>Fields and hill villages: rolled trousers, headscarves, sun hats, dokos.</summary>
        Village = 7,

        /// <summary>Pashupatinath and the Bagmati ghats: Hindu pilgrims (women mostly in red and maroon saris, men in daura
        /// suruwal and dhaka topi, a tika on most foreheads) and the sadhus in saffron with their jata and tilak.</summary>
        Pashupati = 8,
    }

    /// <summary>
    /// Picks the <see cref="StreetStyle"/> at a game position: curated district circles first (street_life §1.3 plus
    /// the office districts of ref_characters.md §1), then the area type. Engine-free; the circles are projected once.
    /// </summary>
    public static class StreetStyles
    {
        // lon, lat, radius (m), style. Earlier rows win (Freak Street sits inside the old bazaar, Thamel next to it).
        private static readonly double[] Circles =
        {
            85.3488, 27.7105, 350, (double)StreetStyle.Pashupati, // Pashupatinath temple, the ghats and Deopatan
            85.3110, 27.7150, 450, (double)StreetStyle.Tourist, // Thamel
            85.3070, 27.7025, 150, (double)StreetStyle.Tourist, // Freak Street (Jhochhen)
            85.3620, 27.7215, 380, (double)StreetStyle.Buddhist, // Boudhanath kora
            85.2903, 27.7149, 380, (double)StreetStyle.Buddhist, // Swayambhu
            85.4290, 27.6720, 800, (double)StreetStyle.NewarTown, // Bhaktapur core
            85.3250, 27.6735, 600, (double)StreetStyle.NewarTown, // Patan Mangal Bazar
            85.2780, 27.6780, 400, (double)StreetStyle.NewarTown, // Kirtipur
            85.3870, 27.6800, 400, (double)StreetStyle.NewarTown, // Thimi
            85.3000, 27.6280, 250, (double)StreetStyle.NewarTown, // Bungamati
            85.2960, 27.6370, 250, (double)StreetStyle.NewarTown, // Khokana
            85.3095, 27.7065, 700, (double)StreetStyle.OldBazaar, // Asan - Indrachowk - Basantapur
            85.3180, 27.7120, 400, (double)StreetStyle.Office, // Durbar Marg, Kamaladi
            85.3225, 27.7040, 350, (double)StreetStyle.Office, // Putalisadak, Bagbazar
            85.3240, 27.6975, 400, (double)StreetStyle.Office, // Singha Durbar, Maitighar
            85.3355, 27.6890, 500, (double)StreetStyle.Office, // New Baneshwor
            85.3165, 27.6780, 350, (double)StreetStyle.Office, // Pulchowk, Jawalakhel
        };

        private static double[] _game;

        /// <summary>Number of curated district circles.</summary>
        public static int CircleCount
        {
            get { return Circles.Length / 4; }
        }

        /// <summary>The style at game position (<paramref name="x"/>, <paramref name="z"/>) whose area type is
        /// <paramref name="area"/>.</summary>
        public static StreetStyle At(double x, double z, AreaType area)
        {
            double[] g = Projected();
            for (int i = 0; i < g.Length; i += 4)
            {
                double dx = x - g[i], dz = z - g[i + 1], r = g[i + 2];
                if (dx * dx + dz * dz <= r * r) return (StreetStyle)(int)g[i + 3];
            }
            return OfArea(area);
        }

        /// <summary>The style of an area type outside the curated districts.</summary>
        public static StreetStyle OfArea(AreaType area)
        {
            switch (area)
            {
                case AreaType.OldCore: return StreetStyle.OldBazaar;
                case AreaType.PeriUrban: return StreetStyle.Suburb;
                case AreaType.Rural:
                case AreaType.Hill:
                case AreaType.Forest: return StreetStyle.Village;
                default: return StreetStyle.Urban;
            }
        }

        /// <summary>The game position and radius of district circle <paramref name="i"/>.</summary>
        public static void Circle(int i, out double x, out double z, out double radiusM, out StreetStyle style)
        {
            double[] g = Projected();
            if (i < 0 || i * 4 >= g.Length) throw new ArgumentOutOfRangeException(nameof(i));
            x = g[i * 4];
            z = g[i * 4 + 1];
            radiusM = g[i * 4 + 2];
            style = (StreetStyle)(int)g[i * 4 + 3];
        }

        private static double[] Projected()
        {
            double[] g = _game;
            if (g != null) return g;
            g = new double[Circles.Length];
            for (int i = 0; i < Circles.Length; i += 4)
            {
                WorldFrame.LonLatToGame(Circles[i], Circles[i + 1], out double x, out double z);
                g[i] = x;
                g[i + 1] = z;
                g[i + 2] = Circles[i + 2];
                g[i + 3] = Circles[i + 3];
            }
            _game = g;
            return g;
        }
    }
}
