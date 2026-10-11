using System;
using Ghumante.Core.Geo;

namespace Ghumante.Core.Horizon
{
    /// <summary>What a curated summit is on the skyline.</summary>
    public enum HorizonPeakKind : byte
    {
        /// <summary>A Himalayan snow peak (summit cone to the published height, or a whole synthetic massif when the
        /// region's horizon tiles do not reach it).</summary>
        SnowPeak = 0,

        /// <summary>A green rim hill of the Kathmandu Valley (only the summit is corrected; the DEM holds the hill).</summary>
        RimHill = 1,
    }

    /// <summary>One curated summit: real coordinates (WGS84) and published height.</summary>
    public readonly struct HorizonPeak
    {
        /// <summary>Stable id (snake case).</summary>
        public readonly string Id;

        public readonly string NameEn;

        /// <summary>Devanagari name (for labels; not shown yet).</summary>
        public readonly string NameNe;

        public readonly double Lat, Lon;

        /// <summary>Published summit height in metres.</summary>
        public readonly double HeightM;

        public readonly HorizonPeakKind Kind;

        public HorizonPeak(string id, string nameEn, string nameNe, double lat, double lon, double heightM, HorizonPeakKind kind)
        {
            Id = id;
            NameEn = nameEn;
            NameNe = nameNe;
            Lat = lat;
            Lon = lon;
            HeightM = heightM;
            Kind = kind;
        }

        /// <summary>Game metres (x east, z north) of the summit.</summary>
        public void GamePosition(out double x, out double z)
        {
            WorldFrame.LonLatToGame(Lon, Lat, out x, out z);
        }

        public override string ToString()
        {
            return NameEn + " " + HeightM.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " m";
        }
    }

    /// <summary>
    /// The curated skyline peak table (CONTENT_COVERAGE D9, docs/research/w2/ref_sky.md): the snow peaks seen from the
    /// Kathmandu Valley and the green rim hills, with published heights. The DEM horizon tiles flatten summits
    /// (GLO-30 infill plus resampling leaves them 40-260 m low), so <see cref="HorizonHeightField"/> raises a summit cone
    /// to each published height, and builds a whole massif for the peaks beyond the region's horizon tiles (the
    /// Rolwaling peaks east of 86.4° E, Everest, Cho Oyu). Coordinates are Wikipedia's unless noted in the brief.
    /// </summary>
    public static class HorizonPeaks
    {
        private static readonly HorizonPeak[] Table =
        {
            // Ganesh Himal (Wikipedia "Ganesh Himal" table).
            new HorizonPeak("ganesh_1", "Ganesh I (Yangra)", "गणेश हिमाल (याङ्ग्रा)", 28.39250, 85.13000, 7422, HorizonPeakKind.SnowPeak),
            new HorizonPeak("ganesh_2", "Ganesh II", "गणेश हिमाल २", 28.37917, 85.05667, 7118, HorizonPeakKind.SnowPeak),
            new HorizonPeak("ganesh_3", "Ganesh III (Salasungo)", "गणेश हिमाल ३ (सालासुङ्गो)", 28.33500, 85.12167, 7043, HorizonPeakKind.SnowPeak),
            new HorizonPeak("ganesh_4", "Ganesh IV (Pabil)", "गणेश हिमाल ४ (पाबिल)", 28.34583, 85.08000, 7104, HorizonPeakKind.SnowPeak),
            // Langtang and Jugal.
            new HorizonPeak("langtang_lirung", "Langtang Lirung", "लाङटाङ लिरुङ", 28.25750, 85.51583, 7234, HorizonPeakKind.SnowPeak),
            new HorizonPeak("langtang_ri", "Langtang Ri", "लाङटाङ री", 28.38139, 85.68361, 7205, HorizonPeakKind.SnowPeak),
            new HorizonPeak("shishapangma", "Shishapangma", "शिशापाङ्मा", 28.35222, 85.77972, 8027, HorizonPeakKind.SnowPeak),
            new HorizonPeak("dorje_lakpa", "Dorje Lakpa", "दोर्जे लाक्पा", 28.17389, 85.78111, 6966, HorizonPeakKind.SnowPeak),
            new HorizonPeak("phurbi_chyachu", "Phurbi Chyachu", "फुर्बी च्याचु", 28.13056, 85.87194, 6637, HorizonPeakKind.SnowPeak),
            // Rolwaling (Gaurishankar) and Khumbu.
            new HorizonPeak("gauri_shankar", "Gauri Shankar", "गौरीशंकर", 27.95333, 86.33583, 7134, HorizonPeakKind.SnowPeak),
            new HorizonPeak("melungtse", "Melungtse", "मेलुङ्त्से", 27.97250, 86.43167, 7181, HorizonPeakKind.SnowPeak),
            new HorizonPeak("kang_nachugo", "Kang Nachugo", "काङ नाचुगो", 27.90500, 86.44250, 6735, HorizonPeakKind.SnowPeak),
            new HorizonPeak("chobutse", "Chobutse", "चोबुत्से", 27.88222, 86.49167, 6685, HorizonPeakKind.SnowPeak),
            new HorizonPeak("tengi_ragi_tau", "Tengi Ragi Tau", "तेङ्गी रागी ताउ", 27.85722, 86.55417, 6938, HorizonPeakKind.SnowPeak),
            new HorizonPeak("numbur", "Numbur", "नुम्बुर", 27.75445, 86.57307, 6958, HorizonPeakKind.SnowPeak),
            new HorizonPeak("cho_oyu", "Cho Oyu", "चो ओयु", 28.09417, 86.66083, 8188, HorizonPeakKind.SnowPeak),
            new HorizonPeak("everest", "Mount Everest (Sagarmatha)", "सगरमाथा", 27.98833, 86.92528, 8849, HorizonPeakKind.SnowPeak),
            // Manaslu group, west-north-west.
            new HorizonPeak("manaslu", "Manaslu", "मनास्लु", 28.54944, 84.56194, 8163, HorizonPeakKind.SnowPeak),
            new HorizonPeak("himalchuli", "Himalchuli", "हिमालचुली", 28.43417, 84.63750, 7893, HorizonPeakKind.SnowPeak),
            // The valley rim (summits located on the GLO-30 DEM; published heights).
            new HorizonPeak("shivapuri", "Shivapuri", "शिवपुरी", 27.81240, 85.38460, 2732, HorizonPeakKind.RimHill),
            new HorizonPeak("phulchoki", "Phulchoki", "फूलचोकी", 27.57100, 85.40675, 2782, HorizonPeakKind.RimHill),
            new HorizonPeak("chandragiri", "Chandragiri", "चन्द्रागिरि", 27.66625, 85.20525, 2551, HorizonPeakKind.RimHill),
            new HorizonPeak("nagarjun", "Nagarjun (Jamacho)", "नागार्जुन (जामाचो)", 27.74525, 85.25925, 2128, HorizonPeakKind.RimHill),
        };

        /// <summary>Number of curated peaks.</summary>
        public static int Count
        {
            get { return Table.Length; }
        }

        /// <summary>Peak <paramref name="i"/> (0 .. <see cref="Count"/> - 1).</summary>
        public static HorizonPeak At(int i)
        {
            return Table[i];
        }

        /// <summary>The peak with <paramref name="id"/>; throws when unknown.</summary>
        public static HorizonPeak ById(string id)
        {
            for (int i = 0; i < Table.Length; i++)
                if (Table[i].Id == id) return Table[i];
            throw new ArgumentException("unknown horizon peak '" + id + "'", nameof(id));
        }
    }
}
