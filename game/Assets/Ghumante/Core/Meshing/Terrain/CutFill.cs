using System;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// The look of carved ground (<see cref="TerrainCarve"/>; docs/research/w2/ref_cutfill.md): what a Kathmandu hill road
    /// cutting shows. Fresh cuts in the valley's soils are ochre to red-brown earth; steep cuts expose grey phyllite and
    /// schist in layers (or are faced with dry stone masonry and gabion baskets); fills are earth embankments that grass
    /// over, held by stone walls where they are steep; the level verge beside the road is packed gravel and earth.
    /// Colours are 0xRRGGBBAA; the weight grows with how far the ground was moved, so a few centimetres keep the natural
    /// ground's colour.
    /// </summary>
    public static class CutFill
    {
        /// <summary>Freshly cut valley soil (ochre-brown, Kathmandu's lacustrine and residual soils).</summary>
        public const uint SoilCut = 0xA57D55FFu;

        /// <summary>Exposed rock of a steep cut (grey-brown phyllite and schist).</summary>
        public const uint RockCut = 0x8B867DFFu;

        /// <summary>A dry stone masonry or gabion retaining wall (grey stone).</summary>
        public const uint StoneWall = 0x9A958BFFu;

        /// <summary>An earth embankment (fill), part grassed over.</summary>
        public const uint FillEarth = 0x8E8058FFu;

        /// <summary>The level verge, drain and shoulder beside the road (packed gravel and earth).</summary>
        public const uint Verge = 0x9B8E78FFu;

        /// <summary>Slope (degrees) above which a cut is rock or a faced wall, a fill a retaining wall.</summary>
        public const float WallDeg = 52f;

        /// <summary>Slope (degrees) under which carved ground counts as the level bench or verge.</summary>
        public const float BenchDeg = 14f;

        /// <summary>
        /// The carved ground's own colour and channel for a vertex moved by <paramref name="delta"/> (negative: cut down,
        /// positive: filled up) with normal Y <paramref name="ny"/>, at height <paramref name="h"/> and world position
        /// (<paramref name="wx"/>, <paramref name="wz"/>).
        /// </summary>
        public static uint Colour(uint natural, float delta, float ny, float h, double wx, double wz, out MaterialChannel ch)
        {
            float slope = ny >= 1f ? 0f : (float)(Math.Acos(Math.Max(0f, Math.Min(1f, ny))) * (180.0 / Math.PI));
            float n = TerrainNoise.Fbm(wx, wz, 3.5, 2, 0x43555446u);
            uint c;
            if (slope < BenchDeg)
            {
                c = Verge;
                ch = MaterialChannel.Dirt;
            }
            else if (delta < 0f)
            {
                if (slope > WallDeg)
                {
                    // Layered rock: bands of lighter and darker stone with height.
                    float band = (float)Math.Sin(h * 2.3 + 1.7 * n);
                    c = MeshColor.Scale(RockCut, 1f + 0.08f * band);
                    ch = MaterialChannel.Stone;
                }
                else
                {
                    c = MeshColor.Lerp(SoilCut, RockCut, Smooth(38f, WallDeg, slope) * 0.6f);
                    ch = MaterialChannel.Dirt;
                }
            }
            else
            {
                if (slope > WallDeg)
                {
                    c = StoneWall;
                    ch = MaterialChannel.Stone;
                }
                else
                {
                    // Embankments green over: more grass on the gentler ones.
                    c = MeshColor.Lerp(FillEarth, natural, 0.55f * (1f - Smooth(20f, 40f, slope)));
                    ch = slope < 30f ? MaterialChannel.Grass : MaterialChannel.Dirt;
                }
            }
            c = MeshColor.Scale(c, 1f + 0.07f * n);
            return c | 0xFFu;
        }

        /// <summary>The natural colour blended toward the carved ground's by how far the ground moved (none under 3 cm,
        /// all from half a metre); <paramref name="weight"/> is that blend.</summary>
        public static uint Blend(uint natural, MaterialChannel naturalCh, float delta, float ny, float h, double wx, double wz, out MaterialChannel ch,
                                 out float weight)
        {
            uint carved = Colour(natural, delta, ny, h, wx, wz, out ch);
            weight = Smooth(0.03f, 0.5f, Math.Abs(delta));
            if (weight <= 0.5f) ch = naturalCh;
            return MeshColor.Lerp(natural, carved, weight) | 0xFFu;
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
