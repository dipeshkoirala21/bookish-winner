using System;

namespace Ghumante.World.Buildings
{
    /// <summary>The building LOD bands of W2_DESIGN 2.4, in draw order from the camera outwards.</summary>
    public enum BuildingBandLayer : byte
    {
        /// <summary>Full grammar per 64 m detail cell (BuildingDetailMesher.BuildCell).</summary>
        B0 = 0,

        /// <summary>Styled extrusion, per 128 m block of a tile.</summary>
        B1 = 1,

        /// <summary>Simplified prisms, per 128 m block.</summary>
        B2 = 2,

        /// <summary>16 m city blocks, per 256 m block.</summary>
        B3 = 3,
    }

    /// <summary>
    /// Band radii per device tier (W2_DESIGN 2.4 table, measured horizontally from the camera) and the block sizes the
    /// streamer splits the band layers into. Each band draws [inner, outer]; the band shader dithers a
    /// <see cref="FadeM"/> wide cross-fade around every boundary (opaque, no alpha blending). Engine-free.
    /// </summary>
    public sealed class BandConfig
    {
        /// <summary>Cross-fade width at each band boundary (W2_DESIGN 2.4: 4 m, dithered).</summary>
        public const float FadeM = 4f;

        /// <summary>Block side of the B1 layer (two by two B0 cells).</summary>
        public const double B1BlockM = 128.0;

        /// <summary>Block side of the B2 layer (as fine as B1: a coarser block would push whole blocks of prisms through
        /// the vertex stage for a sliver of band).</summary>
        public const double B2BlockM = 128.0;

        /// <summary>Block side of the B3 layer.</summary>
        public const double B3BlockM = 256.0;

        /// <summary>Side of a B0 detail cell (Core BuildingDetailMesher.CellM).</summary>
        public const double CellM = 64.0;

        /// <summary>Outer radius of B0, B1, B2 and B3.</summary>
        public float B0OuterM, B1OuterM, B2OuterM, B3OuterM;

        /// <summary>B0 cells kept in the LRU (24 / 48 / 96).</summary>
        public int CellCacheSize;

        /// <summary>Hero slice in triangles (W2_DESIGN 10.4: 20 k / 40 k / 60 k).</summary>
        public int HeroBudgetTris;

        /// <summary>Low never uses hero LOD0 (W2_DESIGN 3.2).</summary>
        public bool HeroLod0Allowed;

        /// <summary>The tier's bands: 0 Low, 1 Mid, 2 High (StreamingConfig tier numbering).</summary>
        public static BandConfig ForTier(int tier)
        {
            switch (tier <= 0 ? 0 : tier >= 2 ? 2 : 1)
            {
                case 0:
                    return new BandConfig
                    {
                        B0OuterM = 35f, B1OuterM = 120f, B2OuterM = 350f, B3OuterM = 750f, CellCacheSize = 24,
                        HeroBudgetTris = 20000, HeroLod0Allowed = false,
                    };
                case 1:
                    return new BandConfig
                    {
                        B0OuterM = 60f, B1OuterM = 200f, B2OuterM = 500f, B3OuterM = 1250f, CellCacheSize = 48,
                        HeroBudgetTris = 40000, HeroLod0Allowed = true,
                    };
                default:
                    return new BandConfig
                    {
                        B0OuterM = 80f, B1OuterM = 250f, B2OuterM = 700f, B3OuterM = 1750f, CellCacheSize = 96,
                        HeroBudgetTris = 60000, HeroLod0Allowed = true,
                    };
            }
        }

        /// <summary>Inner and outer radius a band draws (B1 with <paramref name="b1Full"/> starts at 0: its B0 cells
        /// are not ready, so it stands in for them).</summary>
        public void Range(BuildingBandLayer band, bool b1Full, out float inner, out float outer)
        {
            switch (band)
            {
                case BuildingBandLayer.B0:
                    inner = 0f;
                    outer = B0OuterM;
                    return;
                case BuildingBandLayer.B1:
                    inner = b1Full ? 0f : B0OuterM;
                    outer = B1OuterM;
                    return;
                case BuildingBandLayer.B2:
                    inner = B1OuterM;
                    outer = B2OuterM;
                    return;
                default:
                    inner = B2OuterM;
                    outer = B3OuterM;
                    return;
            }
        }

        /// <summary>
        /// True when a renderer whose horizontal bounds are [minX, maxX] × [minZ, maxZ] has any point inside the band
        /// [inner − fade/2, outer + fade/2] around the camera at (cx, cz): the conservative block test (the shader does
        /// the exact per-fragment cut).
        /// </summary>
        public static bool Touches(double minX, double minZ, double maxX, double maxZ, double cx, double cz, float inner, float outer)
        {
            double h = FadeM * 0.5;
            double dx = Math.Max(0, Math.Max(minX - cx, cx - maxX));
            double dz = Math.Max(0, Math.Max(minZ - cz, cz - maxZ));
            double near = Math.Sqrt(dx * dx + dz * dz);
            double fx = Math.Max(Math.Abs(minX - cx), Math.Abs(maxX - cx));
            double fz = Math.Max(Math.Abs(minZ - cz), Math.Abs(maxZ - cz));
            double far = Math.Sqrt(fx * fx + fz * fz);
            return near <= outer + h && far >= inner - h;
        }

        /// <summary>Nearest horizontal distance from (cx, cz) to a box.</summary>
        public static double NearestDistance(double minX, double minZ, double maxX, double maxZ, double cx, double cz)
        {
            double dx = Math.Max(0, Math.Max(minX - cx, cx - maxX));
            double dz = Math.Max(0, Math.Max(minZ - cz, cz - maxZ));
            return Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Dither opacity of a fragment at horizontal distance <paramref name="d"/> for a band (the C# twin of
        /// the shader's GhBandFade): 1 inside, ramping linearly over <see cref="FadeM"/> centred on each edge. An
        /// inner edge at 0 never fades. The ramps of two adjacent bands sum to 1, so the cross-fade has no hole.</summary>
        public static float Opacity(double d, float inner, float outer)
        {
            double h = FadeM * 0.5;
            double a = inner <= 0f ? 1.0 : Clamp01((d - (inner - h)) / FadeM);
            double b = Clamp01(((outer + h) - d) / FadeM);
            return (float)Math.Min(a, b);
        }

        private static double Clamp01(double v)
        {
            return v < 0 ? 0 : v > 1 ? 1 : v;
        }
    }
}
