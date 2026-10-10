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
        /// topi's quads carry it directly. Families: dense diagonal bands (a dark zig-zag line, two motif rows, a broken
        /// motif row and an accent row; weaves 0, 2, 5, the classic Palpali look); a lattice of small stepped diamonds
        /// (1, 4); a check of small blocks with dark lines and bright dots (3).
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
                    // The dense Palpali diagonal (weaves 0, 2, 5; topi ref_02, ref_06, ref_07): bands six threads wide run
                    // diagonally round the cap: a solid dark zig-zag line, two rows of the main motif colour broken by the
                    // second colour, a row of motifs with the ground showing between them, then a mostly ground row with
                    // small accent dots. About 70% of the cloth is motif, so the cap reads as its colours, not its ground
                    // (period 12 round the cap, so the 60 columns close seamlessly).
                    int D = col + row, A = col - row;
                    switch (Mod(D, 6))
                    {
                        case 0: return 3;
                        case 1: return Mod(A, 4) == 3 ? 2 : 1;
                        case 2: return Mod(A, 4) == 1 ? 1 : 2;
                        case 3: return Mod(A, 4) == 3 ? 0 : 1;
                        case 4: return Mod(A, 3) == 0 ? 4 : 0;
                        default: return Mod(A, 6) == 3 ? 2 : 0;
                    }
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
            /// The Nepali topi (W2_DESIGN 6.1, ref_characters.md §2), to the photos: a tall brimless cap worn high on the
            /// head, its rim fitting the head a finger above the brows (narrower than the head's widest), its two side
            /// walls standing near-vertical and then leaning in to a crisp creased ridge that runs from the front peak
            /// (<see cref="TopiFrontM"/>) down to the back (<see cref="TopiBackM"/>) in a straight line, so it reads as a
            /// trapezoid from the side and a tall pointed cap from the front. The front fold pulls the peak
            /// <see cref="TopiPeakShift"/> of the rim's half-width toward one side (by the recipe), so one side of the front
            /// rises steeper than the other, and the cap sits tilted <see cref="TopiTiltDeg"/> toward the left forehead. The
            /// rim follows the head at its own tilted height with room over the hair. The dhaka topi carries the dense
            /// Palpali weave as per-quad colours (rows of equal slant height, columns of equal rim length, so the cells
            /// stay square up the walls) with the darker turned-in lining at the rim and a seam down the front fold; the
            /// Bhadgaunle (kalo) topi is plain black.
            /// </summary>
            private void Topi(bool dhaka)
            {
                Mat(MaterialChannel.Fabric);
                float hs = _hs;
                int cols = Lod == 0 ? TopiColumns : Lod == 1 ? 20 : Mid ? 10 : 8;
                // Wall rows up to the pinch, then pinch rows to the ridge.
                int wallRows = Lod == 0 ? 7 : Lod == 1 ? 3 : 1, pinchRows = Lod == 0 ? 4 : Lod == 1 ? 2 : 1;
                int rows = wallRows + pinchRows;
                float baseY = TopiRimY * hs;
                float front = TopiFrontM * hs, back = TopiBackM * hs;
                float tilt = MathF.Tan(TopiTiltDeg * Quat.Deg2Rad);
                float clear = 0.011f * hs; // room over the hair under the cap (its shell sits 4–5 mm off the head)
                HeadSection(baseY, out float hx0, out float hz0);
                float side = (_r.Seed & 16u) != 0 ? 1f : -1f;
                float peakX = side * TopiPeakShift * (hx0 + clear);
                V3 pivot = _hc + new V3(0f, 0f, 0.004f * hs);
                uint ground = dhaka ? CharacterPalette.Colour(OutfitItem.DhakaTopi, _head.Colour) : CharacterPalette.Bhadgaunle[0];
                int weave = _head.Pattern;
                // Grid rows: 0 the rim, 1 the top of the turned-in lining (not on the far bodies), the wall up to the pinch,
                // then the pinch up to the ridge (last row).
                bool lining = Lod < 2;
                int gr = rows + (lining ? 2 : 1);
                _k.BeginGrid(gr, cols);
                for (int i = 0; i < gr; i++)
                {
                    int k = lining ? i - 1 : i;
                    float t = i == 0 ? 0f : lining && i == 1 ? 0.04f
                            : k <= wallRows ? (lining ? 0.04f : 0f) + (TopiPinch - (lining ? 0.04f : 0f)) * k / wallRows
                            : TopiPinch + (1f - TopiPinch) * (k - wallRows) / pinchRows;
                    float w = TopiWall(t);
                    for (int j = 0; j < cols; j++)
                    {
                        float a = 6.28318530718f * j / cols;
                        float sa = MathF.Sin(a), ca = MathF.Cos(a);
                        // The rim fits the head at its own (tilted) height.
                        float rimY = baseY + sa * (hx0 + clear) * tilt;
                        HeadSection(rimY, out float hx, out float hz);
                        float rx = hx + clear, rz = hz + clear;
                        // The ridge point of this column: on the straight ridge from the back to the front peak, which leans
                        // back from the rim and bends toward the fold side at the front.
                        float s = 0.5f + 0.5f * ca;
                        float zTop = ca >= 0f ? ca * rz * 0.84f : ca * rz * 0.9f;
                        float xTop = peakX * s * s;
                        float h = back + (front - back) * s;
                        float x = xTop + (sa * rx - xTop) * w, z = zTop + (ca * rz - zTop) * w;
                        float y = rimY + h * t;
                        // The cloth turns in at the rim: the lowest row tucks a little toward the head.
                        if (i == 0)
                        {
                            x *= 0.985f;
                            z *= 0.985f;
                        }
                        _k.GridSet(i, j, cols, pivot + new V3(x, y, z), ground, Bone.HeadAttach, Bone.HeadAttach, 1f, i == 0 ? 0.75f : 1f);
                    }
                }
                int quads = (gr - 1) * cols;
                if (_topiQuads.Length < quads) _topiQuads = new uint[quads];
                // The folded edge at the rim: the cloth turned in, a little darker than the ground.
                uint liningRgb = dhaka ? CharacterPalette.Shade(ground, 0.8f) : 0x2A2A2Eu;
                for (int i = 0; i < gr - 1; i++)
                {
                    for (int j = 0; j < cols; j++)
                    {
                        uint c = ground;
                        if (i == 0 && lining) c = liningRgb;
                        else if (dhaka)
                        {
                            // The weave at LOD0 resolution (coarser levels sample it), starting at the back so the seam hides
                            // there; rows count from the lining up the wall.
                            int cc = (j * TopiColumns / cols + TopiColumns / 2) % TopiColumns;
                            int rr = (Math.Max(0, i - 1) * 12) / rows;
                            int cell = DhakaCell(weave, cc, rr);
                            if (cell > 0) c = CharacterPalette.DhakaMotif(weave, cell - 1);
                            if (Lod >= 2) c = CharacterPalette.Mix(ground, CharacterPalette.DhakaMotif(weave, (i + j) % 3), 0.55f);
                            // The front fold: a soft seam line from the rim up to the peak.
                            if (Lod == 0 && (j == 0 || j == cols - 1) && i > 0) c = CharacterPalette.Shade(c, 0.82f);
                        }
                        else if ((i + j) % 2 == 0) c = 0x202024u; // a faint weave on the black cap
                        _topiQuads[i * cols + j] = c;
                    }
                }
                _k.EndGridColours(gr, cols, true, _hc + new V3(0f, baseY + 0.03f * hs, 0f), V3.Zero, _topiQuads, null);
            }

            /// <summary>The topi wall's reach from the ridge toward the rim at height fraction <paramref name="t"/> of the
            /// column (1 at the rim, 0 on the ridge): near-vertical walls tapering to 90% up to <see cref="TopiPinch"/>,
            /// then pinched in to the ridge, meeting it at an angle (a crisp crease, not a dome).</summary>
            private static float TopiWall(float t)
            {
                const float taper = 0.9f;
                if (t <= TopiPinch) return 1f - (1f - taper) * t / TopiPinch;
                float u = (t - TopiPinch) / (1f - TopiPinch);
                return u >= 1f ? 0f : taper * (1f - MathF.Pow(u, TopiPinchExp));
            }

            /// <summary>The outer radius of the worn topi at angle <paramref name="a"/> (0 front, increasing toward the
            /// wearer's right) and height <paramref name="y"/> above the head centre (its rim fits the head with room over
            /// the hair), or 0 outside the cap's height.</summary>
            private float TopiRadius(float a, float y)
            {
                float hs = _hs;
                float sa = MathF.Sin(a), ca = MathF.Cos(a);
                float clear = 0.011f * hs;
                HeadSection(TopiRimY * hs, out float hx0, out _);
                float rimY = TopiRimY * hs + sa * (hx0 + clear) * MathF.Tan(TopiTiltDeg * Quat.Deg2Rad);
                float s = 0.5f + 0.5f * ca;
                float h = (TopiBackM + (TopiFrontM - TopiBackM) * s) * hs;
                float t = (y - rimY) / h;
                if (t < 0f || t > 1f) return 0f;
                HeadSection(rimY, out float hx, out float hz);
                float rx = hx + clear, rz = hz + clear;
                float w = TopiWall(t);
                // Radius of the rim ellipse in this direction, shrunk toward the ridge (which lies near the centre line).
                float rim = 1f / MathF.Sqrt(sa * sa / (rx * rx) + ca * ca / (rz * rz));
                float ridge = MathF.Abs(ca) * (ca >= 0f ? 0.84f : 0.9f) * rz;
                return ridge + (rim - ridge) * w;
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

            /// <summary>Colour of the namlo: woven jute or nettle fibre (porter ref_01).</summary>
            private const uint NamloRgb = 0xA88B5Eu;

            /// <summary>
            /// The namlo (porter ref_01): a wide flat tumpline band across the upper forehead and back along the temples
            /// above the ears, then the ropes down behind the shoulders to the load. The band follows the outermost surface
            /// at every point: the topi's wall where a cap is worn (the band rides over its lower front), else the hair
            /// over the head, so it never cuts into either. Five rows across its width give it a rounded section.
            /// </summary>
            private void Namlo()
            {
                if (Lod >= 2) return;
                Mat(MaterialChannel.Fabric);
                float hs = _hs;
                bool topi = HasHeadwear && (HeadItem == OutfitItem.DhakaTopi || HeadItem == OutfitItem.BhadgaunleTopi);
                int n = Lod == 0 ? 21 : 11;
                const int rowsAcross = 5;
                float halfW = 0.026f * hs, thick = 0.0045f * hs, frontY = 0.115f * hs, sideDrop = 0.058f * hs;
                uint edge = CharacterPalette.Shade(NamloRgb, 0.82f);
                _k.BeginGrid(rowsAcross, n);
                for (int i = 0; i < n; i++)
                {
                    float a = -1.78f + 3.56f * i / (n - 1);
                    float yc = frontY - sideDrop * (1f - MathF.Cos(a));
                    for (int k = 0; k < rowsAcross; k++)
                    {
                        float u = k / (float)(rowsAcross - 1) * 2f - 1f; // −1 bottom edge .. 1 top edge
                        float y = yc + u * halfW;
                        // Edges stand clear of the chords of the surface below (no z-fighting with the cap or hair).
                        float lift = 0.0025f * hs + thick * MathF.Sqrt(Math.Max(0f, 1f - u * u * 0.85f));
                        float r = OuterRadius(a, y) + lift;
                        var p = new V3(_hc.X + MathF.Sin(a) * r, _hc.Y + y, _hc.Z + MathF.Cos(a) * r);
                        _k.GridSet(k, i, n, p, k == 0 || k == rowsAcross - 1 ? edge : NamloRgb, Bone.Head, Bone.Head, 1f);
                    }
                }
                _k.EndGrid(rowsAcross, n, false, _hc, V3.Up, null, false, false);
                // Ropes from the band's ends (behind the temples) down the back to the load.
                V3 attach = _sk.BindPosition[(int)Bone.BackAttach];
                for (int s = -1; s <= 1; s += 2)
                {
                    float a = s * 1.78f, yc = frontY - sideDrop * (1f - MathF.Cos(a));
                    float r = OuterRadius(a, yc) + 0.004f * hs;
                    V3 from = new V3(_hc.X + MathF.Sin(a) * r, _hc.Y + yc, _hc.Z + MathF.Cos(a) * r);
                    V3 to = attach + new V3(s * 0.17f, 0.12f, -0.12f);
                    _k.Curve(from, (from + to) * 0.5f + new V3(s * 0.04f, 0.05f, -0.05f), to, Lod == 0 ? 5 : 3, 0.007f, 0.006f, 0.007f, 0.006f, V3.Right, NamloRgb, 5,
                             Bone.Head, LoftCap.Round, LoftCap.None, Bone.BackAttach, 0.5f);
                }
            }

            /// <summary>The outermost radius round the head axis at angle <paramref name="a"/> (0 front, toward the wearer's
            /// right) and height <paramref name="y"/> above the head centre: the head with its hair (a thin layer under a cap),
            /// or the worn topi's wall where that stands further out.</summary>
            private float OuterRadius(float a, float y)
            {
                float sa = MathF.Sin(a), ca = MathF.Cos(a);
                HeadSection(y, out float hx, out float hz);
                float head = 0f;
                if (hx > 1e-4f && hz > 1e-4f)
                {
                    float e = HeadExponent;
                    head = 1f / MathF.Pow(MathF.Pow(MathF.Abs(sa) / hx, e) + MathF.Pow(MathF.Abs(ca) / hz, e), 1f / e);
                }
                bool covered = HasHeadwear;
                float hair = _r.Hair == CharacterRecipe.HairShaved ? 0.003f : covered ? 0.007f : 0.014f;
                float r = head + hair * _hs;
                if (covered && (HeadItem == OutfitItem.DhakaTopi || HeadItem == OutfitItem.BhadgaunleTopi)) r = Math.Max(r, TopiRadius(a, y));
                return r;
            }
        }
    }
}
