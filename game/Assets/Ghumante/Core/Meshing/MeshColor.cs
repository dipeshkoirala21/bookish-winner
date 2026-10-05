namespace Ghumante.Core.Meshing
{
    /// <summary>RGBA32 colours packed as <c>0xRRGGBBAA</c> (the order <see cref="MeshData.Colors"/> stores bytes in).</summary>
    public static class MeshColor
    {
        /// <summary>Opaque colour from a <c>0xRRGGBB</c> hex value (as written in ASSET_MANIFEST.md).</summary>
        public static uint FromHex(uint rgb)
        {
            return rgb << 8 | 0xFF;
        }

        public static uint Pack(int r, int g, int b, int a = 255)
        {
            return (uint)(Clamp(r) << 24 | Clamp(g) << 16 | Clamp(b) << 8 | Clamp(a));
        }

        public static int R(uint c)
        {
            return (int)(c >> 24);
        }

        public static int G(uint c)
        {
            return (int)(c >> 16 & 0xFF);
        }

        public static int B(uint c)
        {
            return (int)(c >> 8 & 0xFF);
        }

        public static int A(uint c)
        {
            return (int)(c & 0xFF);
        }

        /// <summary>Linear blend of the RGB channels (alpha from <paramref name="a"/>); t is clamped to [0, 1].</summary>
        public static uint Lerp(uint a, uint b, float t)
        {
            if (t <= 0f) return a;
            if (t >= 1f) return b;
            return Pack((int)(R(a) + (R(b) - R(a)) * t + 0.5f), (int)(G(a) + (G(b) - G(a)) * t + 0.5f),
                        (int)(B(a) + (B(b) - B(a)) * t + 0.5f), A(a));
        }

        /// <summary>RGB multiplied by <paramref name="f"/> (above 1 brightens), clamped; alpha kept.</summary>
        public static uint Scale(uint c, float f)
        {
            return Pack((int)(R(c) * f + 0.5f), (int)(G(c) * f + 0.5f), (int)(B(c) * f + 0.5f), A(c));
        }

        /// <summary>Blend toward white by <paramref name="t"/> (0..1): a lighter tint that keeps the hue.</summary>
        public static uint Lighten(uint c, float t)
        {
            return Lerp(c, 0xFFFFFFFFu, t);
        }

        private static int Clamp(int v)
        {
            return v < 0 ? 0 : v > 255 ? 255 : v;
        }
    }
}
