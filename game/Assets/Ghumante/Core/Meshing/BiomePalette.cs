using System;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing
{
    /// <summary>The four palette seasons of ASSET_MANIFEST.md section 2 (blended over two weeks by the shader later).</summary>
    public enum Season : byte
    {
        Spring = 0,   // Mar–May, pre-monsoon
        Monsoon = 1,  // Jun–Sep, lush and wet
        Autumn = 2,   // Oct–Nov, harvest and festivals
        Winter = 3,   // Dec–Feb
    }

    /// <summary>
    /// The toon ground palette (ASSET_MANIFEST.md section 2, provisional tokens): per <see cref="Biome"/> a flat
    /// colour per season, a slope (steep soil) colour, a rock colour and the slope range in degrees over which the
    /// flat colour blends into the slope colour. Colours are packed <c>0xRRGGBBAA</c> (<see cref="MeshColor"/>).
    /// NONE and values this build does not know use HILL_GRASSLAND's row.
    /// </summary>
    public static class BiomePalette
    {
        /// <summary>The season <see cref="Rgba(Biome)"/> uses: the lush monsoon row, the brightest cartoon look.</summary>
        public const Season DefaultSeason = Season.Monsoon;

        private struct Row
        {
            public uint Spring, Monsoon, Autumn, Winter, Slope, Rock;
            public float BlendStartDeg, BlendEndDeg;
        }

        private static Row R(uint spring, uint monsoon, uint autumn, uint winter, uint slope, uint rock, float a0, float a1)
        {
            return new Row
            {
                Spring = MeshColor.FromHex(spring), Monsoon = MeshColor.FromHex(monsoon), Autumn = MeshColor.FromHex(autumn),
                Winter = MeshColor.FromHex(winter), Slope = MeshColor.FromHex(slope), Rock = MeshColor.FromHex(rock),
                BlendStartDeg = a0, BlendEndDeg = a1,
            };
        }

        private static Row R(uint all, uint slope, uint rock, float a0, float a1)
        {
            return R(all, all, all, all, slope, rock, a0, a1);
        }

        // Indexed by Biome value; hex values from ASSET_MANIFEST.md section 2.
        private static readonly Row[] Rows =
        {
            R(0xB4C25E, 0x9CC75A, 0xB8B85E, 0xC8B56A, 0x9A7A50, 0x8E877C, 30, 45), // NONE (copies HILL_GRASSLAND)
            R(0x6E8C6A, 0x7A6A52, 0x8C8478, 25, 40), // WATER (bed under the water surface)
            R(0xC9B9A0, 0xB49C7C, 0x9C968C, 35, 50), // URBAN_DENSE
            R(0x9CCB63, 0x7FC456, 0xA9C463, 0xB9B56E, 0x8C7A5A, 0x9C968C, 35, 50), // URBAN_GREEN
            R(0xCDB27A, 0x8BD65A, 0xE7C34A, 0xC9A877, 0x9A8460, 0xA08E74, 15, 30), // TERAI_PADDY
            R(0xDDBE5A, 0x7CC24E, 0xC8A774, 0xF8D733, 0x9A8460, 0xA08E74, 15, 30), // TERAI_CROPLAND
            R(0xC98B3A, 0x557F33, 0x6E7A3A, 0x8E7A3E, 0x7D6A3A, 0xA08E74, 25, 40), // TERAI_SAL_FOREST
            R(0xB8B35A, 0x7FB24A, 0xA6B04E, 0xC2A86A, 0x9A8460, 0xA08E74, 20, 35), // TERAI_GRASSLAND
            R(0xCFC7B8, 0xB8AE9C, 0xA39E96, 30, 45), // RIVERBED_GRAVEL
            R(0x6E8A3E, 0x4F8A3A, 0x7A8A44, 0x8A7E4A, 0xA8653F, 0xB08A6A, 25, 40), // CHURE_FOREST
            R(0xC9A877, 0x7FD457, 0xE2C04C, 0x9CCB55, 0x8C7A5A, 0x8E877C, 35, 50), // HILL_TERRACES
            R(0x5E8A3E, 0x4E8A3A, 0x5A7E3A, 0x6A7A42, 0x7A6A3E, 0x8E877C, 30, 45), // HILL_FOREST
            R(0xA39A55, 0x8FA049, 0x9C9A50, 0xB59A5A, 0x9A7A50, 0x8E877C, 30, 45), // HILL_SCRUB
            R(0xB4C25E, 0x9CC75A, 0xB8B85E, 0xC8B56A, 0x9A7A50, 0x8E877C, 30, 45), // HILL_GRASSLAND
            R(0x4E7A46, 0x3F6E46, 0x5A6E40, 0x9FB0A8, 0x6A5E44, 0x8C877F, 28, 42), // SUBALPINE_FOREST (winter: snow-dusted)
            R(0xA6C46A, 0x8DC65E, 0xC89A5A, 0xE6EEF8, 0x8E7A5A, 0x8C877F, 28, 40), // ALPINE_MEADOW
            R(0x8A8E52, 0x7E8A4E, 0xA0603A, 0xE6EEF8, 0x7A6A50, 0x8C877F, 28, 40), // ALPINE_SCRUB
            R(0xA39E96, 0x8C877F, 0x77726B, 20, 35), // SCREE_ROCK
            R(0x8F877C, 0x7E766B, 0x6E675E, 20, 35), // MORAINE
            R(0xDCEFF7, 0xBFDDEB, 0x8C877F, 15, 30), // GLACIER
            R(0xFFFFFF, 0xE6EEF8, 0x8C877F, 40, 60), // SNOW
            R(0xCDA875, 0xC9AA70, 0xC99E68, 0xD8CCB8, 0xB5683F, 0xA58A6C, 25, 40), // TRANS_HIMALAYAN_STEPPE
            R(0xA5D45A, 0x8FCB52, 0xE7A0B8, 0xB8A27A, 0xA58A6C, 0xA39E96, 20, 35), // TRANS_HIMALAYAN_CROPLAND
            R(0x9CCB63, 0x7FC456, 0xA9C463, 0xB9B56E, 0x8C7A5A, 0x9C968C, 30, 45), // ORCHARD
            R(0x8FD45A, 0x5DAE4A, 0x5A9E46, 0x4E8A42, 0x8C7A5A, 0x9C968C, 30, 45), // TEA_GARDEN
            R(0x8E9C5E, 0x7C9C58, 0x9A9A5E, 0xA49A6A, 0x7A6A50, 0x8C877F, 15, 30), // WETLAND
            R(0xC9A877, 0x7FD457, 0xE2C04C, 0xF3D23A, 0x9A8460, 0x9C968C, 25, 40), // VALLEY_CROPLAND
            R(0xB07A4E, 0x9A6440, 0x9C8A74, 25, 40), // BARE_SOIL
        };

        private static Row RowOf(Biome b)
        {
            int i = (int)b;
            return i < Rows.Length ? Rows[i] : Rows[0];
        }

        /// <summary>Flat-ground colour of a biome in <see cref="DefaultSeason"/>.</summary>
        public static uint Rgba(Biome b)
        {
            return Rgba(b, DefaultSeason);
        }

        /// <summary>Flat-ground colour of a biome in a season.</summary>
        public static uint Rgba(Biome b, Season season)
        {
            Row r = RowOf(b);
            switch (season)
            {
                case Season.Spring: return r.Spring;
                case Season.Autumn: return r.Autumn;
                case Season.Winter: return r.Winter;
                default: return r.Monsoon;
            }
        }

        /// <summary>Steep-soil colour.</summary>
        public static uint SlopeRgba(Biome b)
        {
            return RowOf(b).Slope;
        }

        /// <summary>Bare-rock colour (cliffs well past the slope band).</summary>
        public static uint RockRgba(Biome b)
        {
            return RowOf(b).Rock;
        }

        /// <summary>Slope range in degrees over which flat ground blends into the slope colour.</summary>
        public static void SlopeBlendDeg(Biome b, out float startDeg, out float endDeg)
        {
            Row r = RowOf(b);
            startDeg = r.BlendStartDeg;
            endDeg = r.BlendEndDeg;
        }

        /// <summary>
        /// Ground colour for a biome at a surface normal's vertical component <paramref name="normalY"/>
        /// (cos of the slope): the flat colour, smoothly blended into the slope colour across the biome's range,
        /// then into rock over the next 20 degrees.
        /// </summary>
        public static uint Ground(Biome b, Season season, float normalY)
        {
            Row r = RowOf(b);
            uint flat = Rgba(b, season);
            if (normalY >= 0.9999f) return flat;
            float ny = normalY < 0f ? 0f : normalY > 1f ? 1f : normalY;
            float slope = (float)(Math.Acos(ny) * (180.0 / Math.PI));
            if (slope <= r.BlendStartDeg) return flat;
            uint c = MeshColor.Lerp(flat, r.Slope, Smooth(r.BlendStartDeg, r.BlendEndDeg, slope));
            if (slope > r.BlendEndDeg) c = MeshColor.Lerp(c, r.Rock, Smooth(r.BlendEndDeg, r.BlendEndDeg + 20f, slope));
            return c;
        }

        private static float Smooth(float e0, float e1, float x)
        {
            float t = (x - e0) / (e1 - e0);
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return t * t * (3f - 2f * t);
        }
    }
}

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// Deterministic world-coordinate value noise for the terrain and area looks (hashed with FNV-1a like every
    /// other seeded generator, W2_DESIGN 10.2): because it is a function of game coordinates only, two tiles agree
    /// exactly on their shared edge, and every LOD of the same ground shows the same patches. Results in [-1, 1].
    /// Allocation-free; thread-safe.
    /// </summary>
    public static class TerrainNoise
    {
        private static float Lattice(long x, long z, uint seed)
        {
            unchecked
            {
                uint h = Data.Hashes.Fnv32Offset;
                ulong a = (ulong)x, b = (ulong)z;
                for (int i = 0; i < 8; i++) h = (h ^ (byte)(a >> (8 * i))) * Data.Hashes.Fnv32Prime;
                for (int i = 0; i < 8; i++) h = (h ^ (byte)(b >> (8 * i))) * Data.Hashes.Fnv32Prime;
                for (int i = 0; i < 4; i++) h = (h ^ (byte)(seed >> (8 * i))) * Data.Hashes.Fnv32Prime;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                return (h & 0xFFFFFF) * (2f / 16777215f) - 1f;
            }
        }

        /// <summary>Smooth value noise at (x, z) in lattice units.</summary>
        public static float Value(double x, double z, uint seed)
        {
            long ix = (long)Math.Floor(x), iz = (long)Math.Floor(z);
            float fx = (float)(x - ix), fz = (float)(z - iz);
            fx = fx * fx * (3f - 2f * fx);
            fz = fz * fz * (3f - 2f * fz);
            float a = Lattice(ix, iz, seed), b = Lattice(ix + 1, iz, seed), c = Lattice(ix, iz + 1, seed), d = Lattice(ix + 1, iz + 1, seed);
            return a + (b - a) * fx + (c - a + (a - b - c + d) * fx) * fz;
        }

        /// <summary>Fractal noise: <paramref name="octaves"/> octaves from <paramref name="scaleM"/> metres per cell down.</summary>
        public static float Fbm(double x, double z, double scaleM, int octaves, uint seed)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            double f = 1.0 / scaleM;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * Value(x * f, z * f, seed + (uint)o * 0x9E3779B9u);
                norm += amp;
                amp *= 0.5f;
                f *= 2.0;
            }
            return sum / norm;
        }

        /// <summary>Gradient (per metre) of <see cref="Fbm"/> by central differences over 1 m.</summary>
        public static void FbmGradient(double x, double z, double scaleM, int octaves, uint seed, out float gx, out float gz)
        {
            gx = 0.5f * (Fbm(x + 1, z, scaleM, octaves, seed) - Fbm(x - 1, z, scaleM, octaves, seed));
            gz = 0.5f * (Fbm(x, z + 1, scaleM, octaves, seed) - Fbm(x, z - 1, scaleM, octaves, seed));
        }
    }

    /// <summary>
    /// The field patterns of the valley floor and the terraced hills (research street_life.md 12; ref_nature.md:
    /// paddy, mustard and wheat by season): a world-fixed grid of plots, turned per 512 m district so the fields do not
    /// all line up, each plot carrying one crop state drawn from the season's mix; the terrace contour spacing; and
    /// the colours of risers, bunds and lips. Shared by <see cref="TerrainMesher"/> (plot colours per vertex) and
    /// <see cref="AreaMesher"/> (terrace risers and bunds), so they agree. Hex values are the design's [E] palette.
    /// </summary>
    public static class FieldPattern
    {
        /// <summary>Plot size along the two field axes (metres): long strips of valley paddy.</summary>
        public const double PlotU = 26.0, PlotV = 17.0;

        /// <summary>Height of one terrace step (riser plus bed) on cropland slopes.</summary>
        public const double TerraceStepM = 1.9;

        /// <summary>Height of the riser face drawn below each terrace edge, and of the bright lip on top of it.</summary>
        public const double RiserM = 0.55, LipM = 0.16;

        /// <summary>Slopes (rise over run) from which cropland is terraced, and above which it is left wild.</summary>
        public const double TerraceMinSlope = 0.1, TerraceMaxSlope = 0.85;

        /// <summary>True for the cropland biomes (plot patchwork, terraces and bunds).</summary>
        public static bool IsCrop(Biome b)
        {
            return b == Biome.ValleyCropland || b == Biome.HillTerraces || b == Biome.TeraiPaddy || b == Biome.TeraiCropland || b == Biome.TransHimalayanCropland;
        }

        /// <summary>The field frame at a world point: the district's axis angle as a unit vector.</summary>
        public static void Axis(double wx, double wz, out double ux, out double uz)
        {
            long dx = (long)Math.Floor(wx / 512.0), dz = (long)Math.Floor(wz / 512.0);
            double a = (TerrainNoise.Value(dx * 0.5 + 0.25, dz * 0.5 + 0.25, 0xF1E1D5u) * 0.5 + 0.5) * Math.PI;
            ux = Math.Cos(a);
            uz = Math.Sin(a);
        }

        /// <summary>Plot coordinates (field-axis metres) of a world point.</summary>
        public static void Local(double wx, double wz, out double u, out double v)
        {
            double ux, uz;
            Axis(wx, wz, out ux, out uz);
            u = wx * ux + wz * uz;
            v = -wx * uz + wz * ux;
        }

        /// <summary>A hash in [0, 1) of the plot (or terrace bed) containing a world point; <paramref name="band"/>
        /// selects terrace beds (one per step of height) instead of flat plots.</summary>
        public static float PlotHash(double wx, double wz, int band = int.MinValue)
        {
            double u, v;
            Local(wx, wz, out u, out v);
            long iu = (long)Math.Floor(u / (band == int.MinValue ? PlotU : PlotU * 1.6)), iv = band == int.MinValue ? (long)Math.Floor(v / PlotV) : band;
            return 0.5f + 0.5f * TerrainNoise.Value(iu + 0.5, iv + 0.5, 0xF1E1D6u);
        }

        /// <summary>
        /// The crop colour (0xRRGGBB) of a plot in a season and whether it is bare soil: monsoon paddy greens (with a
        /// lighter young paddy and maize), autumn harvest gold with green and stubble plots, winter mustard yellow,
        /// wheat green and fallow brown, spring wheat gold, ploughed earth and green.
        /// </summary>
        public static uint CropColour(float plot, Season s, out bool bare)
        {
            bare = false;
            switch (s)
            {
                case Season.Monsoon:
                    return plot < 0.7f ? 0x5DAA3Au : plot < 0.85f ? 0x8CC84Au : 0x6E9A3Au;
                case Season.Autumn:
                    if (plot < 0.55f) return 0xD9B44Au;
                    if (plot < 0.8f) return 0x8FB848u;
                    bare = true;
                    return 0xC9A86Au;
                case Season.Winter:
                    if (plot < 0.3f) return 0xF2D22Eu;
                    if (plot < 0.65f) return 0x8DBF4Au;
                    if (plot < 0.75f) return 0x5E8A3Au;
                    bare = true;
                    return 0xA88C65u;
                default:
                    if (plot < 0.4f) return 0xD9B65Au;
                    if (plot < 0.7f) return 0x7FB04Au;
                    bare = true;
                    return 0x8C6A4Au;
            }
        }

        /// <summary>Riser face colour (0xRRGGBB, grassy soil) of a terrace edge in a season.</summary>
        public static uint RiserColour(Season s)
        {
            return s == Season.Winter || s == Season.Spring ? 0x9A8A5Au : 0x6E8C40u;
        }

        /// <summary>Lip colour (0xRRGGBB, the bright grassy top of a terrace edge) in a season.</summary>
        public static uint LipColour(Season s)
        {
            return s == Season.Winter ? 0xC8C08Au : s == Season.Autumn ? 0xB8C070u : 0xA8D070u;
        }
    }
}
