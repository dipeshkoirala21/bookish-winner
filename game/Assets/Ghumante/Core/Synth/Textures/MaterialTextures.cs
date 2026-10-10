using System;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using static Ghumante.Core.Synth.Textures.TextureNoise;

namespace Ghumante.Core.Synth.Textures
{
    /// <summary>
    /// The procedural surface textures of the cartoon look (docs/W2_DETAIL_CONTRACT.md §5, World/README.md "Look"):
    /// one tileable square texture per <see cref="MaterialChannel"/>, generated at startup from code and a seed (nothing
    /// is downloaded or stored). Brick with mortar, glazed dachi apa brick, wood grain, carved wood (rosettes and
    /// lattice), jhingati roof tiles, galvanised metal, hammered gilt with glints, ashlar stone, asphalt with wear,
    /// patches and cracks, concrete, grass with tiny flowers, foliage, bark, fabric weave, skin, glass with cartoon
    /// shine bands, water ripples, paint with chips, rubber, dirt with pebbles, flagstones, hair, leather and worn road
    /// paint; slice 0 (<see cref="MaterialChannel.Plain"/>) holds the low-frequency macro variation.
    ///
    /// <para><b>Encoding</b> (what <c>Ghumante/ToonLit</c> expects): RGBA, sRGB colour, alpha = tint weight. The shader
    /// computes <c>albedo = lerp(rgb, tint × rgb × 2, a)</c>: where a = 1 the texture modulates the mesh's vertex colour
    /// (rgb 0.5 = unchanged), where a = 0 rgb is an absolute colour (mortar, grout, worn-through asphalt).</para>
    ///
    /// <para><b>Orientation</b>: v (rows) is "up" in the side projections of the triplanar mapping, so vertical
    /// features (bark fissures, wood grain, drips) run along v.</para>
    ///
    /// Deterministic: the seed of a channel is FNV-1a over its name and <see cref="Version"/>, every pattern is a pure
    /// function of the seed and the texel, and every pattern is periodic over the tile (integer noise periods), so the
    /// textures tile seamlessly at every size. Thread-safe; allocates only the returned arrays.
    /// </summary>
    public static class MaterialTextures
    {
        /// <summary>Bump to re-roll every texture (part of each channel's seed).</summary>
        public const int Version = 1;

        /// <summary>Default edge length (texels) on Mid and High; Low uses 128.</summary>
        public const int DefaultSize = 256;

        /// <summary>Slices of the texture array: one per channel up to <see cref="MaterialChannel.Marking"/>.</summary>
        public static int SliceCount
        {
            get { return MaterialLooks.Count; }
        }

        /// <summary>Deterministic seed of a channel: FNV-1a 32 over "ghumante.material.v{Version}.{name}".</summary>
        public static uint SeedOf(MaterialChannel channel)
        {
            string key = "ghumante.material.v" + Version + "." + channel;
            var bytes = new byte[key.Length];
            for (int i = 0; i < key.Length; i++) bytes[i] = (byte)key[i];
            return Hashes.Fnv1a32(bytes, 0, bytes.Length);
        }

        /// <summary>Mip levels of a square power-of-two texture down to 1 × 1.</summary>
        public static int MipCount(int size)
        {
            int n = 1;
            while (size > 1)
            {
                size >>= 1;
                n++;
            }
            return n;
        }

        /// <summary>True for a power of two from 8 to 2048 (the supported texture sizes).</summary>
        public static bool IsValidSize(int size)
        {
            return size >= 8 && size <= 2048 && (size & (size - 1)) == 0;
        }

        /// <summary>
        /// Renders a channel into <paramref name="rgba"/> (size × size × 4 linear floats, rows bottom-up: index
        /// <c>(y × size + x) × 4</c>, y along v). Colour components are linear; alpha is the tint weight.
        /// </summary>
        public static void Render(MaterialChannel channel, int size, float[] rgba)
        {
            Render(channel, size, rgba, 0, 0);
        }

        /// <summary>
        /// <see cref="Render(MaterialChannel, int, float[])"/> of the window starting <paramref name="offsetX"/>,
        /// <paramref name="offsetY"/> texels into the infinite periodic plane (for the tiling tests: a texture tiles when
        /// the shifted window equals the rolled tile).
        /// </summary>
        internal static void Render(MaterialChannel channel, int size, float[] rgba, int offsetX, int offsetY)
        {
            if (!IsValidSize(size)) throw new ArgumentOutOfRangeException(nameof(size), "power of two, 8..2048");
            if (rgba == null || rgba.Length < size * size * 4) throw new ArgumentException("rgba needs size * size * 4 floats", nameof(rgba));
            var p = new Painter(size, SeedOf(channel), rgba, offsetX, offsetY);
            switch (channel)
            {
                case MaterialChannel.Plaster: Plaster(p); break;
                case MaterialChannel.Brick: Brick(p, false); break;
                case MaterialChannel.BrickGlazed: Brick(p, true); break;
                case MaterialChannel.Wood: Wood(p); break;
                case MaterialChannel.WoodCarved: WoodCarved(p); break;
                case MaterialChannel.RoofTile: RoofTile(p); break;
                case MaterialChannel.Metal: Metal(p); break;
                case MaterialChannel.Gilt: Gilt(p); break;
                case MaterialChannel.Stone: Stone(p); break;
                case MaterialChannel.Asphalt: Asphalt(p); break;
                case MaterialChannel.Concrete: Concrete(p); break;
                case MaterialChannel.Grass: Grass(p); break;
                case MaterialChannel.Foliage: Foliage(p); break;
                case MaterialChannel.Bark: Bark(p); break;
                case MaterialChannel.Fabric: Fabric(p); break;
                case MaterialChannel.Skin: Skin(p); break;
                case MaterialChannel.Glass: Glass(p); break;
                case MaterialChannel.Water: Water(p); break;
                case MaterialChannel.Paint: Paint(p); break;
                case MaterialChannel.Rubber: Rubber(p); break;
                case MaterialChannel.Dirt: Dirt(p); break;
                case MaterialChannel.Flagstone: Flagstone(p); break;
                case MaterialChannel.Hair: Hair(p); break;
                case MaterialChannel.Leather: Leather(p); break;
                case MaterialChannel.Marking: Marking(p); break;
                default: Macro(p); break;
            }
            p.Finish();
        }

        /// <summary>
        /// A channel's full mip chain as sRGB RGBA8 (index 0 = size × size, then halving to 1 × 1; rows bottom-up), ready
        /// for <c>Texture2DArray.SetPixelData</c>. Mips are box-filtered in linear space.
        /// </summary>
        public static byte[][] Bake(MaterialChannel channel, int size)
        {
            var level = new float[size * size * 4];
            Render(channel, size, level);
            int mips = MipCount(size);
            var result = new byte[mips][];
            int s = size;
            for (int m = 0; m < mips; m++)
            {
                result[m] = new byte[s * s * 4];
                EncodeSrgb(level, s * s, result[m]);
                if (m + 1 < mips)
                {
                    level = Downsample(level, s);
                    s >>= 1;
                }
            }
            return result;
        }

        /// <summary>Box-filters a size × size RGBA float image to half size (2 × 2 average).</summary>
        public static float[] Downsample(float[] src, int size)
        {
            int h = Math.Max(1, size >> 1);
            var dst = new float[h * h * 4];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < h; x++)
                {
                    int a = ((2 * y) * size + 2 * x) * 4, b = a + 4, c = a + size * 4, d = c + 4;
                    int o = (y * h + x) * 4;
                    for (int k = 0; k < 4; k++) dst[o + k] = 0.25f * (src[a + k] + src[b + k] + src[c + k] + src[d + k]);
                }
            }
            return dst;
        }

        private static readonly byte[] SrgbLut = MakeSrgbLut();

        private static byte[] MakeSrgbLut()
        {
            var lut = new byte[4096];
            for (int i = 0; i < lut.Length; i++)
            {
                double l = i / 4095.0;
                double s = l <= 0.0031308 ? 12.92 * l : 1.055 * Math.Pow(l, 1.0 / 2.4) - 0.055;
                lut[i] = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(s * 255.0)));
            }
            return lut;
        }

        /// <summary>Linear RGBA floats to sRGB colour bytes (alpha stays linear) for <paramref name="texels"/> texels.</summary>
        public static void EncodeSrgb(float[] rgba, int texels, byte[] dst)
        {
            for (int i = 0; i < texels * 4; i += 4)
            {
                dst[i] = SrgbLut[(int)(Clamp01(rgba[i]) * 4095f + 0.5f)];
                dst[i + 1] = SrgbLut[(int)(Clamp01(rgba[i + 1]) * 4095f + 0.5f)];
                dst[i + 2] = SrgbLut[(int)(Clamp01(rgba[i + 2]) * 4095f + 0.5f)];
                dst[i + 3] = (byte)(Clamp01(rgba[i + 3]) * 255f + 0.5f);
            }
        }

        /// <summary>The linear value an sRGB byte decodes to (the GPU's sRGB sampler).</summary>
        public static float SrgbToLinear(byte b)
        {
            double s = b / 255.0;
            return (float)(s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4));
        }

        // ------------------------------------------------------------------------------------------------------------
        // Painter: the target image plus an optional height field for baked relief shading.

        private sealed class Painter
        {
            public readonly int N;
            public readonly uint Seed;
            public readonly float[] Px;
            public float[] Height;
            private float _emboss;

            private readonly int _ox, _oy;

            public Painter(int n, uint seed, float[] px, int offsetX, int offsetY)
            {
                N = n;
                Seed = seed;
                Px = px;
                _ox = offsetX;
                _oy = offsetY;
            }

            /// <summary>Texture coordinate of a texel column's centre.</summary>
            public float U(int x)
            {
                return (x + _ox + 0.5f) / N;
            }

            /// <summary>Texture coordinate of a texel row's centre.</summary>
            public float V(int y)
            {
                return (y + _oy + 0.5f) / N;
            }

            /// <summary>Per-texel white noise in [0, 1) (periodic over the tile, like everything else).</summary>
            public float TexelHash(int x, int y, uint salt)
            {
                return Hash01(Wrap(x + _ox, N), Wrap(y + _oy, N), Seed + salt);
            }

            /// <summary>Tinted texel: modulation <paramref name="m"/> (1 = the vertex colour) per component.</summary>
            public void Tint(int x, int y, float mr, float mg, float mb)
            {
                int i = (y * N + x) * 4;
                Px[i] = 0.5f * mr;
                Px[i + 1] = 0.5f * mg;
                Px[i + 2] = 0.5f * mb;
                Px[i + 3] = 1f;
            }

            public void Tint(int x, int y, float m)
            {
                Tint(x, y, m, m, m);
            }

            /// <summary>Texel blended from a tinted modulation and an absolute linear colour by
            /// <paramref name="tintWeight"/> (the alpha the shader lerps with).</summary>
            public void Mixed(int x, int y, float r, float g, float b, float tintWeight)
            {
                int i = (y * N + x) * 4;
                Px[i] = r;
                Px[i + 1] = g;
                Px[i + 2] = b;
                Px[i + 3] = tintWeight;
            }

            /// <summary>Allocates the height field (tile units: 1 = one tile edge) and sets the relief strength used by
            /// <see cref="Finish"/>.</summary>
            public float[] UseHeight(float emboss)
            {
                Height = new float[N * N];
                _emboss = emboss;
                return Height;
            }

            /// <summary>Applies the baked relief (light from the upper left of the tile) and clamps.</summary>
            public void Finish()
            {
                if (Height != null)
                {
                    float[] h = Height;
                    float k = _emboss * 0.5f * N;
                    for (int y = 0; y < N; y++)
                    {
                        int yu = (y + 1) & (N - 1), yd = (y - 1) & (N - 1);
                        for (int x = 0; x < N; x++)
                        {
                            int xr = (x + 1) & (N - 1), xl = (x - 1) & (N - 1);
                            float hx = (h[y * N + xr] - h[y * N + xl]) * k;
                            float hy = (h[yu * N + x] - h[yd * N + x]) * k;
                            float shade = 1f + 0.6f * hx - 0.8f * hy;
                            shade = shade < 0.45f ? 0.45f : shade > 1.6f ? 1.6f : shade;
                            int i = (y * N + x) * 4;
                            Px[i] *= shade;
                            Px[i + 1] *= shade;
                            Px[i + 2] *= shade;
                        }
                    }
                }
                for (int i = 0; i < N * N * 4; i++) Px[i] = Clamp01(Px[i]);
            }
        }

        private static float Hue(uint h, int shift)
        {
            return ((h >> shift) & 0xFF) * (1f / 255f) - 0.5f;
        }

        // ------------------------------------------------------------------------------------------------------------
        // Channels. u, v in [0, 1) over the tile; every term is periodic with integer periods.

        private static void Macro(Painter p)
        {
            for (int y = 0; y < p.N; y++)
            for (int x = 0; x < p.N; x++)
            {
                float u = p.U(x), v = p.V(y);
                float n = Fbm(u, v, 3, 3, 4, p.Seed);
                p.Tint(x, y, 0.6f + 0.8f * n);
            }
        }

        private static void Plaster(Painter p)
        {
            uint s = p.Seed;
            for (int y = 0; y < p.N; y++)
            for (int x = 0; x < p.N; x++)
            {
                float u = p.U(x), v = p.V(y);
                float m = 1f + 0.22f * (Fbm(u, v, 4, 4, 5, s) - 0.5f);
                m += 0.07f * (Value(u, v, 96, 96, s + 1) - 0.5f);
                // Drips and rain streaks run down the wall (v is up).
                m += 0.06f * (Value(u, v, 20, 2, s + 2) - 0.5f);
                // Hairline cracks in patches.
                Worley(u, v, 5, 5, s + 3, 0.9f, out float f1, out float f2, out _);
                float mask = SmoothStep(0.55f, 0.7f, Fbm(u, v, 3, 3, 3, s + 4));
                m *= 1f - 0.25f * SmoothStep(0.035f, 0f, f2 - f1) * mask;
                float warm = 0.03f * (Value(u, v, 6, 6, s + 5) - 0.5f);
                p.Tint(x, y, m * (1f + warm), m, m * (1f - warm));
            }
        }

        private static void Brick(Painter p, bool glazed)
        {
            uint s = p.Seed;
            int rows = glazed ? 10 : 8, perRow = 2;
            // Tile units (one tile = 0.5 m, MaterialLooks): joint half-width about 4 mm (glazed 1.25 mm), rounded arris 5 mm
            // (glazed 3 mm); 8 courses of 62.5 mm, 2 bricks of 0.25 m per row.
            float joint = glazed ? 0.0025f : 0.0085f, bevel = glazed ? 0.006f : 0.01f;
            float[] h = p.UseHeight(glazed ? 1.2f : 1.6f);
            for (int y = 0; y < p.N; y++)
            for (int x = 0; x < p.N; x++)
            {
                float u = p.U(x), v = p.V(y);
                float rv = v * rows;
                int row = Floor(rv);
                float fy = rv - row;
                float ru = u * perRow + ((row & 1) != 0 ? 0.5f : 0f);
                int col = Wrap(Floor(ru), perRow);
                float fx = Frac(ru);
                float dx = Math.Min(fx, 1f - fx) / perRow, dy = Math.Min(fy, 1f - fy) / rows;
                float edge = Math.Min(dx, dy);
                uint bh = Hash(col, Wrap(row, rows), s);
                float relief = SmoothStep(joint, joint + bevel, edge);
                h[y * p.N + x] = relief * 0.006f + 0.0006f * Value(u, v, 64, 64, s + 1);
                if (edge < joint)
                {
                    if (glazed)
                    {
                        p.Tint(x, y, 0.42f);
                    }
                    else
                    {
                        // Lime-surkhi mortar: absolute light grey, a quarter tinted by the wall colour.
                        float mg = 0.9f + 0.2f * Value(u, v, 128, 128, s + 2);
                        mg *= 0.8f + 0.2f * (edge / joint);
                        p.Mixed(x, y, 0.40f * mg, 0.38f * mg, 0.35f * mg, 0.25f);
                    }
                    continue;
                }
                float t = (bh & 0xFFFF) * (1f / 65536f);
                float tone = glazed ? 0.94f + 0.12f * t : 0.84f + 0.28f * t;
                if (!glazed && ((bh >> 16) & 0xFF) < 22) tone *= 0.72f; // over-fired brick
                float hue = Hue(bh, 24) * (glazed ? 0.05f : 0.14f);
                float grain = 1f + (glazed ? 0.04f : 0.1f) * (Value(u, v, 96, 96, s + 3) - 0.5f);
                // Weathered faces: soft blotches across the wall.
                grain *= 1f + 0.08f * (Fbm(u, v, 4, 4, 3, s + 4) - 0.5f);
                if (glazed)
                {
                    // Glaze sheen along the upper part of each brick and fine bright specks.
                    grain *= 1f + 0.10f * SmoothStep(0.55f, 0.75f, fy) * SmoothStep(0.95f, 0.8f, fy);
                    if (p.TexelHash(x, y, 5) > 0.994f) grain *= 1.18f;
                }
                else if (Value(u, v, 160, 160, s + 6) < 0.12f)
                {
                    grain *= 0.85f; // pits
                }
                float m = tone * grain;
                p.Tint(x, y, m * (1f + hue), m * (1f - 0.3f * hue), m * (1f - 0.6f * hue));
            }
        }

        private static void Wood(Painter p)
        {
            uint s = p.Seed;
            for (int y = 0; y < p.N; y++)
            for (int x = 0; x < p.N; x++)
            {
                float u = p.U(x), v = p.V(y);
                // Grain runs along v: wide in u, long in v.
                float g = Fbm(u, v, 5, 1, 4, s);
                float rings = Frac(g * 7f);
                float ring = SmoothStep(0f, 0.12f, rings) * SmoothStep(1f, 0.55f, rings);
                float m = 0.84f + 0.2f * ring;
                m *= 0.93f + 0.14f * Value(u, v, 64, 3, s + 1);
                Worley(u, v, 3, 2, s + 2, 0.8f, out float f1, out _, out uint id);
                if ((id & 0xFF) < 100)
                {
                    float knot = SmoothStep(0.14f, 0.04f, f1);
                    float halo = SmoothStep(0.24f, 0.14f, f1) * (1f - knot);
                    m *= 1f - 0.4f * knot - 0.08f * halo;
                }
                float hue = 0.04f * (Value(u, v, 4, 2, s + 3) - 0.5f);
                p.Tint(x, y, m * (1f + hue), m, m * (1f - hue));
            }
        }

        private static void WoodCarved(Painter p)
        {
            uint s = p.Seed;
            float[] h = p.UseHeight(2.2f);
            for (int y = 0; y < p.N; y++)
            for (int x = 0; x < p.N; x++)
            {
                float u = p.U(x), v = p.V(y);
                float cu = Frac(u * 2f), cv = Frac(v * 2f);
                int cell = (Floor(u * 2f) + Floor(v * 2f)) & 1;
                float e = Math.Min(Math.Min(cu, 1f - cu), Math.Min(cv, 1f - cv));
                // Raised frame with a beaded inner moulding.
                float frame = SmoothStep(0.0f, 0.02f, e) * SmoothStep(0.075f, 0.055f, e);
                float along = e == cu || e == 1f - cu ? cv : cu;
                float bead = SmoothStep(0.11f, 0.095f, e) * SmoothStep(0.075f, 0.09f, e) *
                             SmoothStep(0.5f, 0.2f, Math.Abs(Frac(along * 10f) - 0.5f) * 2f);
                float motif;
                if (cell == 0)
                {
                    // Lotus rosette: eight domed petals and a boss.
                    float dx = cu - 0.5f, dy = cv - 0.5f;
                    float r = (float)Math.Sqrt(dx * dx + dy * dy);
                    float th = (float)Math.Atan2(dy, dx);
                    float rEdge = 0.33f * (0.72f + 0.28f * Math.Abs((float)Math.Cos(4f * th)));
                    float petal = SmoothStep(rEdge, rEdge - 0.035f, r) * (0.55f + 0.45f * (1f - r / rEdge));
                    float vein = SmoothStep(0.9f, 1f, Math.Abs((float)Math.Cos(4f * th))) * SmoothStep(0.08f, 0.2f, r);
                    petal *= 1f - 0.35f * vein;
                    float boss = SmoothStep(0.085f, 0.06f, r) * 1.15f;
                    float ring = SmoothStep(0.012f, 0f, Math.Abs(r - 0.4f)) * 0.5f;
                    motif = Math.Max(Math.Max(petal, boss), ring);
                }
                else
                {
                    // Tiki jhya lattice: a diagonal grid of bars with deep holes.
                    float a = Frac((cu + cv) * 4f), b = Frac((cu - cv) * 4f);
                    float bar = Math.Max(SmoothStep(0.2f, 0.12f, Math.Abs(a - 0.5f)), SmoothStep(0.2f, 0.12f, Math.Abs(b - 0.5f)));
                    motif = bar * SmoothStep(0.08f, 0.11f, e);
                }
                float height = Math.Max(Math.Max(frame, bead * 0.8f), motif);
                h[y * p.N + x] = height * 0.012f;
                float m = 0.5f + 0.55f * height;
                m *= 0.94f + 0.12f * Value(u, v, 48, 3, s + 1);
                p.Tint(x, y, m);
            }
        }

        private static void RoofTile(Painter p)
        {
            uint s = p.Seed;
            const int rows = 6, perRow = 4; // one tile = 0.5 m: 0.125 m tiles in 83 mm rows
            float[] h = p.UseHeight(1.4f);
            for (int y = 0; y < p.N; y++)
            for (int x = 0; x < p.N; x++)
            {
                float u = p.U(x), v = p.V(y);
                float rv = v * rows;
                int row = Floor(rv);
                float fy = rv - row; // 0 = lower lip, 1 = under the next row's lip
                float ru = u * perRow + ((row & 1) != 0 ? 0.5f : 0f);
                int col = Wrap(Floor(ru), perRow);
                float fx = Frac(ru);
                uint th = Hash(col, Wrap(row, rows), s);
                float gap = Math.Min(fx, 1f - fx) / perRow; // tile units
                // Rounded lower corners: the lip curves up near the tile sides.
                float corner = 0.10f * (float)Math.Pow(Math.Abs(fx * 2f - 1f), 6f);
                float lip = SmoothStep(corner, corner + 0.08f, fy);
                float height = (1f - 0.7f * fy) * SmoothStep(0.002f, 0.006f, gap) * lip;
                h[y * p.N + x] = height * 0.012f;
                float t = (th & 0xFFFF) * (1f / 65536f);
                float tone = 0.82f + 0.3f * t;
                float hue = Hue(th, 16) * 0.12f;
                float m = tone * (0.94f + 0.12f * Value(u, v, 64, 64, s + 1));
                // Shadow cast by the lip of the row above onto the top of this row.
                m *= 1f - 0.32f * SmoothStep(0.72f, 0.97f, fy);
                if (gap < 0.003f || fy < corner) m *= 0.5f;
                float mr = 1f + hue, mg = 1f - 0.3f * hue, mb = 1f - 0.5f * hue;
                if (((th >> 24) & 0xFF) < 28)
                {
                    // Old tile with moss.
                    float moss = SmoothStep(0.45f, 0.75f, Fbm(u, v, 8, 8, 3, s + 2));
                    m *= 0.82f;
                    mr *= 1f - 0.18f * moss;
                    mg *= 1f + 0.12f * moss;
                    mb *= 1f - 0.22f * moss;
                }
                p.Tint(x, y, m * mr, m * mg, m * mb);
            }
        }

        private static void Metal(Painter p)
        {
            uint s = p.Seed;
            for (int y = 0; y < p.N; y++)
            for (int x = 0; x < p.N; x++)
            {
                float u = p.U(x), v = p.V(y);
                // Galvanised spangle crystals.
                Worley(u, v, 9, 9, s, 0.9f, out float f1, out float f2, out uint id);
                float m = 0.93f + 0.12f * ((id & 0xFFFF) * (1f / 65536f));
                m *= 1f - 0.05f * SmoothStep(0.03f, 0f, f2 - f1);
                // Brushed streaks along u, blotches.
                m *= 0.95f + 0.1f * Value(u, v, 2, 160, s + 1);
                m *= 1f + 0.12f * (Fbm(u, v, 3, 3, 3, s + 2) - 0.5f);
                // A few long scratches (integer slopes keep them periodic).
                float sc = Math.Min(Math.Abs(Frac(u + 3f * v + 0.17f) - 0.5f), Math.Abs(Frac(2f * u - v + 0.61f) - 0.5f));
                if (sc < 0.0025f && Value(u, v, 6, 6, s + 3) > 0.62f) m *= 1.25f;
                p.Tint(x, y, m);
            }
        }

        private static void Gilt(Painter p)
        {
            uint s = p.Seed;
            float[] h = p.UseHeight(1.8f);
            for (int y = 0; y < p.N; y++)
            for (int x = 0; x < p.N; x++)
            {
                float u = p.U(x), v = p.V(y);
                // Hammered (repoussé) copper under the gilding: shallow dents.
                Worley(u, v, 7, 7, s, 0.95f, out float f1, out _, out uint id);
                h[y * p.N + x] = f1 * f1 * 0.012f;
                float m = 0.94f + 0.12f * ((id & 0xFFFF) * (1f / 65536f));
                m *= 1f + 0.12f * (Fbm(u, v, 2, 2, 3, s + 1) - 0.5f);
                if (p.TexelHash(x, y, 2) > 0.991f) m *= 1.5f; // glints
                p.Tint(x, y, m * 1.02f, m, m * 0.96f);
            }
        }

        private static void Stone(Painter p)
        {
            uint s = p.Seed;
            const int rows = 4, perRow = 2; // 0.25 m courses of 0.5 m blocks (an even row count keeps the bond periodic)
            const float joint = 0.006f, bevel = 0.014f;
            float[] h = p.UseHeight(1.4f);
            for (int y = 0; y < p.N; y++)
            for (int x = 0; x < p.N; x++)
            {
                float u = p.U(x), v = p.V(y);
                float rv = v * rows;
                int row = Floor(rv);
                float fy = rv - row;
                float ru = u * perRow + ((row & 1) != 0 ? 0.5f : 0f);
                int col = Wrap(Floor(ru), perRow);
                float fx = Frac(ru);
                float edge = Math.Min(Math.Min(fx, 1f - fx) / perRow, Math.Min(fy, 1f - fy) / rows);
                float weather = Fbm(u, v, 4, 4, 5, s + 1);
                h[y * p.N + x] = SmoothStep(joint, joint + bevel, edge) * 0.01f + 0.002f * weather;
                if (edge < joint)
                {
                    p.Mixed(x, y, 0.20f, 0.19f, 0.18f, 0.4f);
                    continue;
                }
                uint bh = Hash(col, Wrap(row, rows), s);
                float tone = 0.88f + 0.22f * ((bh & 0xFFFF) * (1f / 65536f));
                float m = tone * (1f + 0.2f * (weather - 0.5f));
                float speck = Value(u, v, 192, 192, s + 2);
                if (speck > 0.78f) m *= 1.12f;
                else if (speck < 0.18f) m *= 0.88f;
                Worley(u, v, 30, 30, s + 3, 0.9f, out float f1, out _, out uint pid);
                if ((pid & 3) == 0 && f1 < 0.16f) m *= 0.88f;
                float hue = Hue(bh, 16) * 0.06f;
                p.Tint(x, y, m * (1f + hue), m, m * (1f - hue));
            }
        }

        private static void Asphalt(Painter p)
        {
            uint s = p.Seed;
            // Two repair patches per tile (rectangles in tile units, wrapping across the edges).
            float pcx0 = Hash01(1, 0, s), pcy0 = Hash01(2, 0, s), pw0 = 0.14f + 0.16f * Hash01(3, 0, s), ph0 = 0.1f + 0.2f * Hash01(4, 0, s);
            float pcx1 = Hash01(1, 1, s), pcy1 = Hash01(2, 1, s), pw1 = 0.1f + 0.12f * Hash01(3, 1, s), ph1 = 0.12f + 0.12f * Hash01(4, 1, s);
            for (int y = 0; y < p.N; y++)
            for (int x = 0; x < p.N; x++)
            {
                float u = p.U(x), v = p.V(y);
                float m = 1f;
                // Aggregate: light chips and dark voids.
                float ag = Value(u, v, 180, 180, s);
                if (ag > 0.76f) m += 0.9f * (ag - 0.76f);
                else if (ag < 0.2f) m -= 0.12f;
                m += 0.08f * (Value(u, v, 256, 256, s + 1) - 0.5f);
                // Wear and oil: large soft blotches.
                m *= 1f + 0.2f * (Fbm(u, v, 3, 3, 4, s + 2) - 0.5f);
                // Repair patches: darker fresh asphalt with a lighter sealed edge.
                float patch0 = PatchEdge(u, v, pcx0, pcy0, pw0, ph0), patch1 = PatchEdge(u, v, pcx1, pcy1, pw1, ph1);
                float inside = Math.Max(patch0, patch1);
                if (inside > 0f)
                {
                    m *= 0.86f;
                    if (inside < 0.01f) m *= 1.2f;
                }
                // Cracks.
                Worley(u, v, 4, 4, s + 3, 0.9f, out float f1, out float f2, out _);
                float crackMask = SmoothStep(0.52f, 0.62f, Fbm(u, v, 4, 4, 3, s + 4));
                m *= 1f - 0.4f * SmoothStep(0.022f, 0.006f, f2 - f1) * crackMask;
                // Old oil spots.
                Worley(u, v, 3, 3, s + 5, 0.8f, out float o1, out _, out uint oid);
                if ((oid & 3) == 0) m *= 1f - 0.12f * SmoothStep(0.22f, 0.05f, o1);
                p.Tint(x, y, m);
            }
        }

        /// <summary>Distance inside a wrapping rectangle (tile units), or −1 outside.</summary>
        private static float PatchEdge(float u, float v, float cx, float cy, float w, float h)
        {
            float dx = Math.Abs(Frac(u - cx + 0.5f) - 0.5f), dy = Math.Abs(Frac(v - cy + 0.5f) - 0.5f);
            float ix = 0.5f * w - dx, iy = 0.5f * h - dy;
            return ix > 0f && iy > 0f ? Math.Min(ix, iy) : -1f;
        }

        private static void Concrete(Painter p)
        {
            uint s = p.Seed;
            for (int y = 0; y < p.N; y++)
            for (int x = 0; x < p.N; x++)
            {
                float u = p.U(x), v = p.V(y);
                float m = 1f + 0.16f * (Fbm(u, v, 4, 4, 5, s) - 0.5f) + 0.07f * (Value(u, v, 128, 128, s + 1) - 0.5f);
                Worley(u, v, 40, 40, s + 2, 0.9f, out float f1, out _, out uint id);
                if ((id & 1) == 0 && f1 < 0.13f) m *= 0.82f; // pores
                // Formwork seams every metre and run-off streaks.
                float seamV = Math.Min(Frac(v * 2f), 1f - Frac(v * 2f)) * 0.5f, seamU = Math.Min(Frac(u), 1f - Frac(u));
                if (seamV < 0.0025f) m *= 0.8f;
                if (seamU < 0.002f) m *= 0.86f;
                m += 0.07f * (Value(u, v, 24, 2, s + 3) - 0.5f);
                p.Tint(x, y, m);
            }
        }

        private static void Grass(Painter p)
        {
            uint s = p.Seed;
            for (int y = 0; y < p.N; y++)
            for (int x = 0; x < p.N; x++)
            {
                float u = p.U(x), v = p.V(y);
                // Blade strokes in two directions.
                float blades = Math.Max(Value(u, v, 150, 24, s), Value(u, v, 24, 150, s + 1));
                float m = 0.8f + 0.36f * blades;
                if (Value(u, v, 90, 90, s + 2) < 0.14f) m *= 0.72f; // soil showing between tufts
                // Clumps: sunnier, yellower patches.
                float c = (Fbm(u, v, 5, 5, 4, s + 3) - 0.5f) * 2f;
                float mr = 1f + 0.22f * c, mg = 1f + 0.06f * c, mb = 1f - 0.18f * c;
                m *= 1f + 0.08f * c;
                // Tiny wild flowers (absolute colours: white, yellow, pink).
                Worley(u, v, 14, 14, s + 4, 0.9f, out float f1, out _, out uint id);
                if ((id & 0xFF) < 10 && f1 < 0.16f)
                {
                    int kind = (int)((id >> 8) % 3u);
                    bool centre = f1 < 0.06f;
                    if (centre) p.Mixed(x, y, 0.85f, 0.62f, 0.08f, 0f);
                    else if (kind == 0) p.Mixed(x, y, 0.82f, 0.82f, 0.78f, 0f);
                    else if (kind == 1) p.Mixed(x, y, 0.88f, 0.70f, 0.10f, 0f);
                    else p.Mixed(x, y, 0.85f, 0.38f, 0.52f, 0f);
                    continue;
                }
                p.Tint(x, y, m * mr, m * mg, m * mb);
            }
        }

        private static void Foliage(Painter p)
        {
            uint s = p.Seed;
            float[] h = p.UseHeight(1.2f);
            for (int y = 0; y < p.N; y++)
            for (int x = 0; x < p.N; x++)
            {
                float u = p.U(x), v = p.V(y);
                Worley(u, v, 12, 12, s, 0.95f, out float f1, out float f2, out uint id);
                h[y * p.N + x] = (1f - f1) * 0.004f;
                float tone = 0.86f + 0.28f * ((id & 0xFFFF) * (1f / 65536f));
                float hue = Hue(id, 16) * 0.16f;
                float m = tone * (1f - 0.42f * SmoothStep(0.07f, 0f, f2 - f1));
                m *= 1f + 0.1f * (Fbm(u, v, 3, 3, 3, s + 1) - 0.5f);
                p.Tint(x, y, m * (1f + hue), m * (1f + 0.2f * hue), m * (1f - hue));
            }
        }

        private static void Bark(Painter p)
        {
            uint s = p.Seed;
            float[] h = p.UseHeight(1.6f);
            for (int y = 0; y < p.N; y++)
            for (int x = 0; x < p.N; x++)
            {
                float u = p.U(x), v = p.V(y);
                // Plates elongated along the trunk (v), deep fissures between them.
                Worley(u, v, 7, 2, s, 0.9f, out float f1, out float f2, out uint id);
                float fissure = SmoothStep(0.13f, 0f, f2 - f1);
                h[y * p.N + x] = (f2 - f1) * 0.01f;
                float tone = 0.9f + 0.2f * ((id & 0xFFFF) * (1f / 65536f));
                float m = tone * (1f - 0.5f * fissure) * (0.88f + 0.24f * Value(u, v, 48, 6, s + 1));
                float mr = 1f, mg = 1f, mb = 1f;
                float lichen = SmoothStep(0.64f, 0.74f, Fbm(u, v, 4, 4, 3, s + 2)) * (1f - fissure);
                if (lichen > 0f)
                {
                    m *= 1f + 0.25f * lichen;
                    mg = 1f + 0.1f * lichen;
                    mb = 1f - 0.05f * lichen;
                }
                p.Tint(x, y, m * mr, m * mg, m * mb);
            }
        }

        private static void Fabric(Painter p)
        {
            uint s = p.Seed;
            const int threads = 40;
            for (int y = 0; y < p.N; y++)
            for (int x = 0; x < p.N; x++)
            {
                float u = p.U(x), v = p.V(y);
                float tu = u * threads, tv = v * threads;
                int i = Floor(tu), j = Floor(tv);
                float fu = tu - i, fv = tv - j;
                bool warpOver = ((i + j) & 1) == 0;
                float pu = (float)Math.Sin(Math.PI * fu), pv = (float)Math.Sin(Math.PI * fv);
                float m = 0.8f + 0.26f * (warpOver ? pu * (0.75f + 0.25f * pv) : pv * (0.75f + 0.25f * pu));
                m *= 0.95f + 0.1f * Value(u, v, 5, 40, s);
                p.Tint(x, y, m);
            }
        }

        private static void Skin(Painter p)
        {
            uint s = p.Seed;
            for (int y = 0; y < p.N; y++)
            for (int x = 0; x < p.N; x++)
            {
                float u = p.U(x), v = p.V(y);
                float m = 1f + 0.06f * (Fbm(u, v, 3, 3, 4, s) - 0.5f);
                if (Value(u, v, 96, 96, s + 1) < 0.1f) m *= 0.97f;
                float flush = 0.04f * (Value(u, v, 5, 5, s + 2) - 0.5f);
                p.Tint(x, y, m * (1f + flush), m, m * (1f - 0.5f * flush));
            }
        }

        private static void Glass(Painter p)
        {
            uint s = p.Seed;
            for (int y = 0; y < p.N; y++)
            for (int x = 0; x < p.N; x++)
            {
                float u = p.U(x), v = p.V(y);
                // Cartoon shine: two diagonal bands (one wide, one thin), periodic along u + v.
                float d = Frac(u + v);
                float band = SmoothStep(0.10f, 0.12f, d) * SmoothStep(0.25f, 0.23f, d) * 0.38f +
                             SmoothStep(0.29f, 0.30f, d) * SmoothStep(0.34f, 0.33f, d) * 0.28f;
                float m = 0.92f + band + 0.08f * (Fbm(u, v, 3, 3, 3, s) - 0.5f);
                p.Tint(x, y, m);
            }
        }

        private static void Water(Painter p)
        {
            uint s = p.Seed;
            for (int y = 0; y < p.N; y++)
            for (int x = 0; x < p.N; x++)
            {
                float u = p.U(x), v = p.V(y);
                Worley(u, v, 5, 5, s, 0.9f, out float a1, out float a2, out _);
                Worley(u, v, 9, 9, s + 1, 0.9f, out float b1, out float b2, out _);
                float lines = SmoothStep(0.10f, 0f, a2 - a1) * 0.24f + SmoothStep(0.08f, 0f, b2 - b1) * 0.12f;
                float m = 0.9f + lines + 0.1f * (Fbm(u, v, 3, 3, 3, s + 2) - 0.5f);
                p.Tint(x, y, m, m, m * 1.02f);
            }
        }

        private static void Paint(Painter p)
        {
            uint s = p.Seed;
            float[] h = p.UseHeight(1.0f);
            for (int y = 0; y < p.N; y++)
            for (int x = 0; x < p.N; x++)
            {
                float u = p.U(x), v = p.V(y);
                float m = 1f + 0.06f * (Fbm(u, v, 3, 3, 4, s) - 0.5f) + 0.04f * (Value(u, v, 3, 80, s + 1) - 0.5f);
                Worley(u, v, 10, 10, s + 2, 0.9f, out float f1, out _, out uint id);
                float r = 0.08f + 0.08f * Value(u, v, 40, 40, s + 3);
                bool chip = (id & 0xFF) < 14 && f1 < r;
                h[y * p.N + x] = chip ? 0f : 0.0015f;
                if (chip)
                {
                    p.Mixed(x, y, 0.30f, 0.29f, 0.27f, 0f); // primer under the chipped paint
                    continue;
                }
                p.Tint(x, y, m);
            }
        }

        private static void Rubber(Painter p)
        {
            uint s = p.Seed;
            for (int y = 0; y < p.N; y++)
            for (int x = 0; x < p.N; x++)
            {
                float u = p.U(x), v = p.V(y);
                float m = 0.9f + 0.14f * Value(u, v, 64, 64, s);
                Worley(u, v, 16, 16, s + 1, 0.8f, out float f1, out _, out _);
                if (f1 < 0.22f) m *= 1.07f;
                p.Tint(x, y, m);
            }
        }

        private static void Dirt(Painter p)
        {
            uint s = p.Seed;
            float[] h = p.UseHeight(1.3f);
            for (int y = 0; y < p.N; y++)
            for (int x = 0; x < p.N; x++)
            {
                float u = p.U(x), v = p.V(y);
                float c = Fbm(u, v, 4, 4, 5, s);
                float m = 0.8f + 0.4f * c;
                float c2 = (Fbm(u, v, 3, 3, 3, s + 1) - 0.5f) * 2f;
                float mr = 1f + 0.12f * c2, mb = 1f - 0.12f * c2;
                Worley(u, v, 5, 5, s + 2, 0.9f, out float k1, out float k2, out _);
                float crackMask = SmoothStep(0.58f, 0.68f, Fbm(u, v, 3, 3, 3, s + 3));
                m *= 1f - 0.32f * SmoothStep(0.03f, 0.008f, k2 - k1) * crackMask;
                Worley(u, v, 22, 22, s + 4, 0.8f, out float f1, out _, out uint id);
                float pr = 0.18f + 0.12f * ((id >> 8 & 0xFF) * (1f / 255f));
                bool pebble = id % 3u == 0u && f1 < pr;
                h[y * p.N + x] = pebble ? (pr - f1) * 0.02f : 0.001f * c;
                if (pebble)
                {
                    float t = 0.8f + 0.4f * ((id >> 16 & 0xFF) * (1f / 255f));
                    p.Mixed(x, y, 0.30f * t, 0.28f * t, 0.25f * t, 0.45f);
                    continue;
                }
                p.Tint(x, y, m * mr, m, m * mb);
            }
        }

        private static void Flagstone(Painter p)
        {
            uint s = p.Seed;
            float[] h = p.UseHeight(1.5f);
            for (int y = 0; y < p.N; y++)
            for (int x = 0; x < p.N; x++)
            {
                float u = p.U(x), v = p.V(y);
                Worley(u, v, 4, 4, s, 0.85f, out float f1, out float f2, out uint id);
                float e = f2 - f1;
                float wear = Fbm(u, v, 4, 4, 4, s + 1);
                h[y * p.N + x] = SmoothStep(0.05f, 0.16f, e) * 0.008f + 0.002f * wear;
                if (e < 0.05f)
                {
                    float moss = SmoothStep(0.55f, 0.7f, Fbm(u, v, 6, 6, 3, s + 2));
                    p.Mixed(x, y, 0.12f - 0.02f * moss, 0.115f + 0.03f * moss, 0.11f - 0.03f * moss, 0.3f);
                    continue;
                }
                float tone = 0.85f + 0.27f * ((id & 0xFFFF) * (1f / 65536f));
                float hue = Hue(id, 16) * 0.08f;
                float m = tone * (1f + 0.16f * (wear - 0.5f)) * (0.95f + 0.1f * Value(u, v, 160, 160, s + 3));
                p.Tint(x, y, m * (1f + hue), m, m * (1f - hue));
            }
        }

        private static void Hair(Painter p)
        {
            uint s = p.Seed;
            for (int y = 0; y < p.N; y++)
            for (int x = 0; x < p.N; x++)
            {
                float u = p.U(x), v = p.V(y);
                float strands = Value(u, v, 90, 3, s) * (0.7f + 0.3f * Value(u, v, 30, 2, s + 1));
                float m = 0.78f + 0.34f * strands;
                p.Tint(x, y, m);
            }
        }

        private static void Leather(Painter p)
        {
            uint s = p.Seed;
            float[] h = p.UseHeight(0.8f);
            for (int y = 0; y < p.N; y++)
            for (int x = 0; x < p.N; x++)
            {
                float u = p.U(x), v = p.V(y);
                Worley(u, v, 22, 22, s, 0.9f, out float f1, out float f2, out _);
                float crease = SmoothStep(0.08f, 0f, f2 - f1);
                h[y * p.N + x] = (f2 - f1) * 0.002f;
                float m = (1f - 0.2f * crease) * (1f + 0.14f * (Fbm(u, v, 3, 3, 3, s + 1) - 0.5f));
                p.Tint(x, y, m);
            }
        }

        private static void Marking(Painter p)
        {
            uint s = p.Seed;
            for (int y = 0; y < p.N; y++)
            for (int x = 0; x < p.N; x++)
            {
                float u = p.U(x), v = p.V(y);
                float wear = Fbm(u, v, 5, 5, 4, s) + 0.25f * (Value(u, v, 64, 64, s + 1) - 0.5f);
                Worley(u, v, 4, 4, s + 2, 0.9f, out float f1, out float f2, out _);
                float crackMask = SmoothStep(0.5f, 0.6f, Fbm(u, v, 3, 3, 2, s + 5));
                float worn = Math.Max(SmoothStep(0.68f, 0.72f, wear), SmoothStep(0.008f, 0.003f, f2 - f1) * crackMask);
                float g = 0.045f * (0.75f + 0.6f * Value(u, v, 160, 160, s + 3));
                float paint = 0.5f * (0.95f + 0.08f * Value(u, v, 96, 96, s + 4));
                p.Mixed(x, y, Lerp(paint, g, worn), Lerp(paint, g, worn), Lerp(paint, g * 1.03f, worn), 1f - worn);
            }
        }
    }
}
