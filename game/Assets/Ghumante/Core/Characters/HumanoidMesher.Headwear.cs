using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Characters
{
    public static partial class HumanoidMesher
    {
        /// <summary>Quads round the topi at LOD0 (one ≈ 2.2 cm on the cartoon cap, 1.3 cm on a real one); every weave
        /// repeats in a divisor of it, so the pattern closes without a seam.</summary>
        public const int TopiColumns = 60;

        /// <summary>
        /// The Palpali dhaka weave as a cell pattern (ref_characters.md §2): woven cloth is a grid of threads, so the
        /// topi's quads carry it directly. Families: diagonal rows of single motif dots between dotted dark lines, the
        /// dot colours alternating row by row (weaves 0, 2, 5, the classic Palpali look); a lattice of small stepped
        /// diamonds (1, 4); a check of small blocks with dark lines and bright dots (3).
        /// Returns 0 for the ground, 1..4 for motif colours 0..3.
        /// </summary>
        public static int DhakaCell(int weave, int col, int row)
        {
            weave = ((weave % CharacterPalette.DhakaWeaves) + CharacterPalette.DhakaWeaves) % CharacterPalette.DhakaWeaves;
            switch (weave)
            {
                case 1:
                case 4:
                {
                    // Lattice: diamonds 4 columns by 4 rows, offset every other row of diamonds; dark centres.
                    int band = FloorDiv(row, 4);
                    int x = Mod(col + 2 * Mod(band, 2), 4) - 2, y = Mod(row, 4) - 2;
                    int d = Math.Abs(x) + Math.Abs(y);
                    if (d == 0) return 4;
                    if (d == 1) return 1;
                    if (d == 2) return Mod(band, 2) == 0 ? 2 : 3;
                    return 0;
                }
                case 3:
                {
                    // Check: 2 × 2 blocks of two colours with a dark line between, a bright dot in each block and a
                    // ground dot where the lines cross (period 6 round the cap).
                    int bx = Mod(col, 3), by = Mod(row, 3);
                    if (bx == 2 && by == 2) return 0;
                    if (bx == 2 || by == 2) return 2;
                    if (bx == 0 && by == 0) return 4;
                    return Mod(FloorDiv(col, 3) + FloorDiv(row, 3), 2) == 0 ? 1 : 3;
                }
                default:
                {
                    // Diagonal rows of single-thread motifs, as the small butta of the Palpali weave read at this scale:
                    // a dotted dark line, a row of motif dots alternating two colours band by band, and a row of dots in
                    // the fourth colour, all on the ground (period 6 round the cap, so the 60 columns close seamlessly).
                    int D = col + row, A = col - row;
                    int band = FloorDiv(D, 3), k = Mod(D, 3);
                    if (k == 0) return Mod(A, 2) == 0 ? 3 : 0;
                    if (k == 1) return Mod(A, 3) == 0 ? (Mod(band, 2) == 0 ? 1 : 2) : 0;
                    return Mod(A, 3) == 1 ? 4 : 0;
                }
            }
        }

        private static int Mod(int a, int n)
        {
            int r = a % n;
            return r < 0 ? r + n : r;
        }

        private static int FloorDiv(int a, int n)
        {
            return a >= 0 ? a / n : -((-a + n - 1) / n);
        }

        private sealed partial class Builder
        {
            private uint[] _topiQuads = new uint[0];

            private void Headwear()
            {
                if (_o.Headwear == HeadwearMode.None) return;
                if (_o.Headwear == HeadwearMode.Helmet)
                {
                    Helmet();
                    return;
                }
                switch (_head.Item)
                {
                    case OutfitItem.DhakaTopi:
                        Topi(true);
                        break;
                    case OutfitItem.BhadgaunleTopi:
                        Topi(false);
                        break;
                    case OutfitItem.Beanie:
                        Beanie(CharacterPalette.Colour(OutfitItem.Beanie, _head.Colour));
                        break;
                    case OutfitItem.SunHat:
                        SunHat(CharacterPalette.Colour(OutfitItem.SunHat, _head.Colour));
                        break;
                    case OutfitItem.Cap:
                        PeakedCap(CharacterPalette.Colour(OutfitItem.Cap, _head.Colour), false);
                        break;
                    case OutfitItem.PoliceCap:
                        PeakedCap(CharacterPalette.PoliceCap[0], true);
                        break;
                    case OutfitItem.Headscarf:
                        Headscarf(CharacterPalette.Colour(OutfitItem.Headscarf, _head.Colour));
                        break;
                }
                if (CharacterRecipe.UsesNamlo(_back.Item)) Namlo();
            }

            /// <summary>The head's half-widths at height <paramref name="y"/> above its centre (x across, z deep).</summary>
            private void HeadSection(float y, out float rx, out float rz)
            {
                float ny = CharMath.Clamp(y / _hr.Y, -0.99f, 0.99f);
                float ring = MathF.Pow(Math.Max(0f, 1f - MathF.Pow(MathF.Abs(ny), HeadExponent)), 1f / HeadExponent);
                rx = _hr.X * ring * JawX(ny);
                rz = _hr.Z * ring;
            }

            /// <summary>
            /// The Nepali topi (W2_DESIGN 6.1, ref_characters.md §2): a brimless cap on a round base that fits the head,
            /// its walls leaning in to a crown pinched into a front-to-back ridge with a shallow trough in the middle;
            /// the front stands <see cref="TopiFrontM"/> and the back <see cref="TopiBackM"/> (the real 3 : 2 on the
            /// cartoon head), and it sits tilted <see cref="TopiTiltDeg"/> toward the left forehead. The dhaka
            /// topi carries the Palpali weave as per-quad colours with the light lining at the rim; the Bhadgaunle
            /// (kalo) topi is plain black.
            /// </summary>
            private void Topi(bool dhaka)
            {
                Mat(MaterialChannel.Fabric);
                float hs = _hs;
                int cols = Lod == 0 ? TopiColumns : Lod == 1 ? 20 : 8, rows = Lod == 0 ? 11 : Lod == 1 ? 4 : 1;
                float baseY = TopiRimY * hs;
                HeadSection(baseY, out float hx, out float hz);
                float rx = hx * 1.05f + 0.004f, rz = hz * 1.05f + 0.004f;
                float front = TopiFrontM * hs, back = TopiBackM * hs;
                float trough = (TopiFoldDeg / 8f) * 0.014f * hs;
                float tilt = MathF.Tan(TopiTiltDeg * Quat.Deg2Rad);
                V3 pivot = _hc + new V3(0f, baseY, 0.004f * hs);
                uint ground = dhaka ? CharacterPalette.Colour(OutfitItem.DhakaTopi, _head.Colour) : CharacterPalette.Bhadgaunle[0];
                int weave = _head.Pattern;
                // Grid: rows 0..rows the wall (base to the pinched top), row rows + 1 the ridge line.
                int gr = rows + 2;
                _k.BeginGrid(gr, cols);
                for (int i = 0; i < gr; i++)
                {
                    // Row 1 sits just above the rim so the light lining shows as a thin edge.
                    float t = i == 0 ? 0f : i == 1 ? 0.045f : Math.Min(1f, 0.045f + 0.955f * (i - 1) / (float)rows);
                    for (int j = 0; j < cols; j++)
                    {
                        float a = 6.28318530718f * j / cols;
                        float sa = MathF.Sin(a), ca = MathF.Cos(a);
                        float h = back + (front - back) * (0.5f + 0.5f * ca);
                        // The walls lean in steadily (a trapezoid from the front and from the side), then round over
                        // into the front-to-back ridge.
                        float sx = 1f - 0.4f * Math.Min(t, TopiPinchStart) / TopiPinchStart, sz = 1f - 0.2f * t;
                        if (t > TopiPinchStart)
                        {
                            float u = (t - TopiPinchStart) / (1f - TopiPinchStart);
                            sx *= MathF.Sqrt(Math.Max(0f, 1f - u * u));
                        }
                        if (i == gr - 1) sx = 0f;
                        float bulge = 1f + 0.03f * MathF.Sin(t * MathF.PI);
                        float x = sa * rx * sx * bulge, z = ca * rz * sz * bulge;
                        // The trough along the ridge: the middle sits lower than the front and back peaks.
                        float y = h * t - trough * MathF.Pow(MathF.Abs(sa), 1.5f) * MathF.Pow(t, 4f);
                        // Tilt toward the left forehead: the wearer's right stands higher.
                        y += x * tilt;
                        _k.GridSet(i, j, cols, pivot + new V3(x, y, z), ground, Bone.HeadAttach, Bone.HeadAttach, 1f, i == 0 ? 0.75f : 1f);
                    }
                }
                int quads = (gr - 1) * cols;
                if (_topiQuads.Length < quads) _topiQuads = new uint[quads];
                // The folded edge at the rim: the cloth turned in, a little darker than the ground.
                uint lining = dhaka ? CharacterPalette.Shade(ground, 0.84f) : 0x2A2A2Eu;
                for (int i = 0; i < gr - 1; i++)
                {
                    for (int j = 0; j < cols; j++)
                    {
                        uint c = ground;
                        if (i == 0) c = lining;
                        else if (dhaka)
                        {
                            // Sample the weave at LOD0 resolution so coarser levels keep the same colours; the pattern
                            // starts at the back so any seam hides there.
                            int cc = (j * TopiColumns / cols + TopiColumns / 2) % TopiColumns, rr = (i - 1) * 10 / Math.Max(1, rows - 1);
                            int cell = DhakaCell(weave, cc, rr);
                            if (Lod >= 2) cell = (i + j) % 3 == 0 ? 1 : 0;
                            if (cell > 0) c = CharacterPalette.DhakaMotif(weave, cell - 1);
                        }
                        else if ((i + j) % 2 == 0) c = 0x202024u; // a faint weave on the black cap
                        _topiQuads[i * cols + j] = c;
                    }
                }
                _k.EndGridColours(gr, cols, true, pivot + new V3(0f, 0.03f * hs, 0f), V3.Zero, _topiQuads, null);
            }

            private void Helmet()
            {
                Mat(MaterialChannel.Paint);
                uint shell = CharacterPalette.Helmet[_r.HelmetColour % CharacterPalette.Helmet.Length];
                uint stripe = _r.HelmetColour == 3 ? 0x1E88E5u : 0xFAFAFAu;
                float hs = _hs;
                // An open-face shell over the crown cut above the brows and over the ears, with a padded rim, a short
                // visor, a stripe and a chin strap.
                Shell(0.036f * hs, 0.07f * hs, -0.07f * hs, -0.16f * hs, shell, 0.03f * hs, MaterialChannel.Paint, Bone.HeadAttach);
                if (Lod >= 2) return;
                Mat(MaterialChannel.Rubber);
                RimRing(0.036f * hs, 0.07f * hs, -0.07f * hs, -0.16f * hs, 0.006f * hs, 0x2B2B30u, 0.03f * hs);
                Mat(MaterialChannel.Paint);
                _k.Ellipsoid(_hc + new V3(0f, _hr.Y * 0.7f + 0.01f, -0.02f), new V3(0.03f * hs, 0.42f * _hr.Y + 0.03f, _hr.Z * 0.88f + 0.035f), Quat.Euler(-14f, 0f, 0f),
                             stripe, _k.Seg(8, 4), _k.Seg(10, 5), Bone.HeadAttach);
                Mat(MaterialChannel.Paint);
                _k.Ellipsoid(_hc + new V3(0f, 0.085f * hs, _hr.Z + 0.03f * hs), new V3(0.13f, 0.01f, 0.055f) * hs, Quat.Euler(-16f, 0f, 0f), 0x2B3A48u,
                             _k.Seg(12, 6), 3, Bone.HeadAttach);
                if (Lod == 0)
                {
                    Mat(MaterialChannel.Fabric);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        V3 a = _hc + new V3(s * (_hr.X + 0.02f * hs), -0.06f * hs, -0.01f);
                        V3 b = FacePoint(s * 0.06f * hs, -0.98f * _hr.Y, 0.006f * hs, out V3 n);
                        _k.Curve(a, (a + b) * 0.5f + new V3(s * 0.01f, 0f, -0.01f), b, 4, 0.006f * hs, 0.006f * hs, 0.0015f, 0.0015f, V3.Forward, 0x2A2A2Eu, 4,
                                 Bone.HeadAttach);
                    }
                }
            }

            /// <summary>A hat shell like the hair cap (offset from the head) above a brim line, bound to head_attach.</summary>
            private void Shell(float offset, float frontY, float sideY, float backY, uint rgb, float lift, MaterialChannel ch, Bone bone)
            {
                _k.Channel = ch;
                int cols = _k.Seg(24, 8), rows = Lod >= 2 ? 2 : _k.Seg(7, 3);
                var r = new V3(_hr.X + offset, _hr.Y + offset + lift, _hr.Z + offset);
                _k.BeginGrid(rows + 1, cols);
                for (int row = 0; row <= rows; row++)
                    for (int j = 0; j < cols; j++)
                    {
                        float a = 6.28318530718f * j / cols;
                        float sa = MathF.Sin(a), ca = MathF.Cos(a);
                        float fw = MathF.Pow(Math.Max(0f, ca), 1.4f), bw = MathF.Pow(Math.Max(0f, -ca), 1.4f);
                        float lineY = sideY + (frontY - sideY) * fw + (backY - sideY) * bw;
                        float phiMax = MathF.Acos(Math.Max(-0.98f, Math.Min(0.98f, lineY / r.Y)));
                        float phi = row == 0 ? 0.03f : phiMax * row / rows;
                        var p = new V3(r.X * MathF.Sin(phi) * sa, r.Y * MathF.Cos(phi), r.Z * MathF.Sin(phi) * ca);
                        _k.GridSet(row, j, cols, _hc + p, rgb, bone, bone, 1f, row == rows ? 0.8f : 1f);
                    }
                _k.EndGrid(rows + 1, cols, true, _hc, V3.Zero, null, true, false);
            }

            /// <summary>A padded tube along a shell's brim line.</summary>
            private void RimRing(float offset, float frontY, float sideY, float backY, float radius, uint rgb, float lift)
            {
                int n = _k.Seg(24, 8);
                V3[] path = _k.PathBuffer(n + 1, out float[] rx, out float[] rz);
                var r = new V3(_hr.X + offset, _hr.Y + offset + lift, _hr.Z + offset);
                for (int j = 0; j <= n; j++)
                {
                    float a = 6.28318530718f * j / n;
                    float sa = MathF.Sin(a), ca = MathF.Cos(a);
                    float fw = MathF.Pow(Math.Max(0f, ca), 1.4f), bw = MathF.Pow(Math.Max(0f, -ca), 1.4f);
                    float lineY = sideY + (frontY - sideY) * fw + (backY - sideY) * bw;
                    float phi = MathF.Acos(Math.Max(-0.98f, Math.Min(0.98f, lineY / r.Y)));
                    path[j] = _hc + new V3(r.X * MathF.Sin(phi) * sa, r.Y * MathF.Cos(phi), r.Z * MathF.Sin(phi) * ca);
                    rx[j] = radius;
                    rz[j] = radius;
                }
                _k.Sweep(path, n + 1, rx, rz, V3.Up, rgb, 5, Bone.HeadAttach, LoftCap.None, LoftCap.None);
            }

            private void Beanie(uint rgb)
            {
                float hs = _hs;
                Shell(0.02f * hs, 0.07f * hs, -0.02f * hs, -0.1f * hs, rgb, 0.025f * hs, MaterialChannel.Fabric, Bone.HeadAttach);
                if (Lod >= 2) return;
                _k.BeginLoft();
                uint band = CharacterPalette.Shade(rgb, 0.85f);
                for (int k = 0; k < 3; k++)
                {
                    float y = (0.055f + 0.025f * k) * hs;
                    HeadSection(y, out float sx, out float sz);
                    _k.FlatRing(_hc + new V3(0f, y, 0f), sx + 0.03f * hs, sz + 0.03f * hs, band, Bone.HeadAttach, Bone.HeadAttach, 1f, HeadExponent);
                }
                _k.EndLoft(_k.Seg(20, 8), LoftCap.None, LoftCap.None);
                if (Lod == 0) _k.Sphere(_hc + new V3(0f, _hr.Y + 0.055f * hs, -0.01f), 0.035f * hs, CharacterPalette.Shade(rgb, 1.15f), 8, 5, Bone.HeadAttach);
            }

            private void SunHat(uint rgb)
            {
                Mat(MaterialChannel.Fabric);
                float hs = _hs;
                float y0 = 0.07f * hs;
                HeadSection(y0, out float sx, out float sz);
                _k.BeginLoft();
                _k.FlatRing(_hc + new V3(0f, y0, 0f), sx + 0.02f, sz + 0.02f, CharacterPalette.Shade(rgb, 0.75f), Bone.HeadAttach, Bone.HeadAttach, 1f, 2.4f);
                if (Lod < 2)
                {
                    // The hat band.
                    _k.FlatRing(_hc + new V3(0f, y0 + 0.03f * hs, 0f), sx + 0.018f, sz + 0.018f, CharacterPalette.Shade(rgb, 0.75f), Bone.HeadAttach, Bone.HeadAttach, 1f, 2.4f);
                    _k.FlatRing(_hc + new V3(0f, y0 + 0.032f * hs, 0f), sx + 0.017f, sz + 0.017f, rgb, Bone.HeadAttach, Bone.HeadAttach, 1f, 2.4f);
                }
                _k.FlatRing(_hc + new V3(0f, _hr.Y - 0.035f * hs, 0f), _hr.X * 0.86f + 0.012f, _hr.Z * 0.86f + 0.012f, rgb, Bone.HeadAttach, Bone.HeadAttach, 1f, 2.4f);
                _k.FlatRing(_hc + new V3(0f, _hr.Y + 0.008f * hs, 0f), _hr.X * 0.66f, _hr.Z * 0.66f, rgb, Bone.HeadAttach, Bone.HeadAttach, 1f, 2.4f);
                _k.FlatRing(_hc + new V3(0f, _hr.Y + 0.018f * hs, 0f), _hr.X * 0.4f, _hr.Z * 0.4f, rgb, Bone.HeadAttach, Bone.HeadAttach, 1f, 2.4f);
                _k.EndLoft(_k.Seg(18, Lod >= 2 ? 6 : 8), LoftCap.None, LoftCap.Flat);
                // A soft brim drooping at the edge.
                _k.BeginLoft();
                _k.FlatRing(_hc + new V3(0f, y0 + 0.004f, 0.005f), sx + 0.02f, sz + 0.02f, rgb, Bone.HeadAttach, Bone.HeadAttach, 1f, 2.2f);
                if (Lod < 2)
                    _k.FlatRing(_hc + new V3(0f, y0 - 0.01f * hs, 0.01f), sx + 0.09f * hs, sz + 0.09f * hs, rgb, Bone.HeadAttach, Bone.HeadAttach, 1f, 2.2f);
                _k.FlatRing(_hc + new V3(0f, y0 - 0.03f * hs, 0.012f), sx + 0.12f * hs, sz + 0.12f * hs, CharacterPalette.Shade(rgb, 0.92f), Bone.HeadAttach, Bone.HeadAttach, 1f, 2.2f);
                _k.EndLoft(_k.Seg(22, Lod >= 2 ? 6 : 8), LoftCap.None, LoftCap.None);
                if (Lod >= 2) return;
                _k.BeginLoft();
                _k.FlatRing(_hc + new V3(0f, y0 - 0.034f * hs, 0.012f), sx + 0.12f * hs, sz + 0.12f * hs, CharacterPalette.Shade(rgb, 0.8f), Bone.HeadAttach, Bone.HeadAttach, 1f, 2.2f);
                _k.FlatRing(_hc + new V3(0f, y0 - 0.006f * hs, 0.005f), sx + 0.02f, sz + 0.02f, CharacterPalette.Shade(rgb, 0.7f), Bone.HeadAttach, Bone.HeadAttach, 1f, 2.2f);
                _k.EndLoft(_k.Seg(22, 8), LoftCap.None, LoftCap.None);
            }

            /// <summary>A peaked cap: a baseball cap (six-panel crown with a button) or the traffic police's navy service cap
            /// (a wider flat crown on a band, black visor; no badge).</summary>
            private void PeakedCap(uint rgb, bool police)
            {
                float hs = _hs;
                Mat(MaterialChannel.Fabric);
                if (!police)
                {
                    Shell(0.016f * hs, 0.085f * hs, 0.02f * hs, -0.03f * hs, rgb, 0.012f * hs, MaterialChannel.Fabric, Bone.HeadAttach);
                    if (Lod < 2)
                    {
                        _k.Sphere(_hc + new V3(0f, _hr.Y + 0.03f * hs, 0f), 0.012f * hs, rgb, 6, 4, Bone.HeadAttach);
                        if (Lod == 0)
                            for (int k = 0; k < 3; k++)
                            {
                                float a = (k - 1) * 0.9f;
                                V3 p0 = _hc + new V3(MathF.Sin(a) * 0.05f, _hr.Y + 0.026f * hs, MathF.Cos(a) * 0.05f);
                                V3 p1 = _hc + new V3(MathF.Sin(a) * (_hr.X + 0.01f), 0.09f * hs, MathF.Cos(a) * (_hr.Z + 0.015f));
                                _k.Curve(p0, (p0 + p1) * 0.5f + (p1 - _hc) * 0.25f, p1, 4, 0.0016f, 0.0016f, 0.0016f, 0.0016f, V3.Up, CharacterPalette.Shade(rgb, 0.75f), 3,
                                         Bone.HeadAttach, LoftCap.None, LoftCap.None);
                            }
                    }
                }
                else
                {
                    float y0 = 0.07f * hs;
                    HeadSection(y0, out float sx, out float sz);
                    _k.BeginLoft();
                    _k.FlatRing(_hc + new V3(0f, y0, 0f), sx + 0.012f, sz + 0.012f, 0x15151Au, Bone.HeadAttach, Bone.HeadAttach, 1f, 2.4f);
                    _k.FlatRing(_hc + new V3(0f, y0 + 0.04f * hs, 0f), sx + 0.014f, sz + 0.014f, 0x15151Au, Bone.HeadAttach, Bone.HeadAttach, 1f, 2.4f);
                    if (Lod < 2)
                        _k.FlatRing(_hc + new V3(0f, y0 + 0.042f * hs, 0f), sx + 0.016f, sz + 0.016f, rgb, Bone.HeadAttach, Bone.HeadAttach, 1f, 2.4f);
                    _k.FlatRing(_hc + new V3(0f, y0 + 0.1f * hs, -0.01f * hs), sx + 0.05f * hs, sz + 0.06f * hs, rgb, Bone.HeadAttach, Bone.HeadAttach, 1f, 2.6f);
                    _k.FlatRing(_hc + new V3(0f, y0 + 0.118f * hs, -0.012f * hs), sx + 0.045f * hs, sz + 0.055f * hs, rgb, Bone.HeadAttach, Bone.HeadAttach, 1f, 2.6f);
                    _k.EndLoft(_k.Seg(20, Lod >= 2 ? 6 : 8), LoftCap.None, LoftCap.Flat);
                }
                // The visor (peak).
                Mat(police ? MaterialChannel.Leather : MaterialChannel.Fabric);
                float vy = police ? 0.07f * hs : 0.088f * hs;
                HeadSection(vy, out float vx, out float vz);
                _k.Ellipsoid(_hc + new V3(0f, vy - 0.004f * hs, vz + (police ? 0.035f : 0.055f) * hs), new V3(vx * 0.72f, 0.008f * hs, (police ? 0.055f : 0.075f) * hs),
                             Quat.Euler(police ? -18f : -8f, 0f, 0f), police ? 0x111114u : CharacterPalette.Shade(rgb, 0.95f), _k.Seg(14, 6), Lod >= 2 ? 2 : 3, Bone.HeadAttach, 3f);
            }

            /// <summary>A cotton headscarf over the hair, knotted at the nape with two short tails.</summary>
            private void Headscarf(uint rgb)
            {
                float hs = _hs;
                Shell(0.022f * hs, 0.1f * hs, -0.08f * hs, -0.17f * hs, rgb, 0.006f * hs, MaterialChannel.Fabric, Bone.HeadAttach);
                if (Lod >= 2) return;
                V3 knot = _hc + new V3(0f, -0.1f * hs, -_hr.Z - 0.02f * hs);
                _k.Sphere(knot, 0.03f * hs, CharacterPalette.Shade(rgb, 0.9f), 8, 5, Bone.HeadAttach);
                for (int s = -1; s <= 1; s += 2)
                    _k.Ellipsoid(knot + new V3(s * 0.025f, -0.06f, -0.01f) * hs, new V3(0.022f, 0.06f, 0.008f) * hs, Quat.Euler(10f, 0f, s * 15f), rgb, 8, 4,
                                 Bone.Neck);
            }

            /// <summary>The namlo: the tumpline strap across the top of the forehead and down behind the ears to the load.</summary>
            private void Namlo()
            {
                if (Lod >= 2) return;
                Mat(MaterialChannel.Fabric);
                float hs = _hs;
                uint strap = 0x8A6A44u;
                // The band over the front of the head.
                int n = Lod == 0 ? 9 : 5;
                V3[] path = _k.PathBuffer(n, out float[] rx, out float[] rz);
                for (int i = 0; i < n; i++)
                {
                    float a = -1.45f + 2.9f * i / (n - 1);
                    float y = 0.13f * hs - 0.035f * hs * MathF.Abs(MathF.Sin(a));
                    HeadSection(y, out float sx, out float sz);
                    float off = HasHeadwear && HeadItem != OutfitItem.None ? 0.03f : 0.018f;
                    path[i] = _hc + new V3(MathF.Sin(a) * (sx + off * hs), y, MathF.Cos(a) * (sz + off * hs));
                    rx[i] = 0.018f * hs;
                    rz[i] = 0.003f * hs;
                }
                _k.Sweep(path, n, rx, rz, V3.Up, strap, 4, Bone.Head, LoftCap.Flat, LoftCap.Flat);
                // Ropes from the temples down the back to the load.
                V3 attach = _sk.BindPosition[(int)Bone.BackAttach];
                for (int s = -1; s <= 1; s += 2)
                {
                    V3 a = path[s < 0 ? 0 : n - 1];
                    V3 b = attach + new V3(s * 0.17f, 0.12f, -0.12f);
                    _k.Curve(a, (a + b) * 0.5f + new V3(s * 0.04f, 0.05f, -0.05f), b, Lod == 0 ? 5 : 3, 0.006f, 0.006f, 0.006f, 0.006f, V3.Right, strap, 4, Bone.Head,
                             LoftCap.None, LoftCap.None, Bone.BackAttach, 0.5f);
                }
            }
        }
    }
}
