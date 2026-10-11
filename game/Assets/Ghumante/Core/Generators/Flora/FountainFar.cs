using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Generators.Flora
{
    /// <summary>
    /// The far forms of the plants that are not crowns of leaves (docs/research/w2/ref_assets-b.md §1): the
    /// <b>clump</b> of banana mats and bamboo (stems from the ground with big blades arching up, out and over, their
    /// tips drooping: <see cref="TreeShape.Fountain"/>) and the <b>palm</b> (a bare ringed trunk carrying a star of
    /// arching fronds). LOD2 bends each blade along a curve (a two-sided strip, widest past the middle, pointed);
    /// LOD3 draws each blade as one two-sided "sail" triangle from its root over its arch to its drooping tip, so a
    /// far plant still reads as fronds or paddles, never a ball. Unit models (foot at the origin, height 1, widest
    /// extent 1); foliage grey (alpha 255) for the instance's crown colour. Deterministic; engine-free.
    /// </summary>
    internal static class FountainFar
    {
        /// <summary>Palm trunk lean along +X (unit width): the species' LOD0 leans 0.06 h toward +X.</summary>
        private const float PalmLean = 0.08f;

        /// <summary>Grey of the clump stems (the instance tint makes them a darker green than the leaves).</summary>
        private const uint StemGrey = 0xA8A8A8u;

        /// <summary>Palm crown point (top of the trunk) height.</summary>
        private const float PalmCrownY = 0.76f;

        /// <summary>
        /// The clump (banana mat, bamboo clump): three stems (pseudostems, culms) splaying from one foot to 0.45-0.6 of
        /// the height, each crowned by three blades (paddle leaves, leafy culm tops) that rise and arch out to the
        /// clump's edge with drooping tips; LOD2 bends each blade (≤ 112 triangles), LOD3 draws stems and blades as
        /// two-sided triangles (24).
        /// </summary>
        public static void Clump(FloraBuilder b, int lod)
        {
            if (lod >= 3)
            {
                ClumpImpostor(b);
                return;
            }
            // Stems: tinted a little darker than the leaves (banana and bamboo stems are green).
            for (int i = 0; i < 3; i++)
            {
                double a = i * 2 * Math.PI / 3 + 0.5;
                float ca = (float)Math.Cos(a), sa = (float)Math.Sin(a);
                var radial = new Vec3(ca, 0f, sa);
                var foot = radial * 0.04f - Vec3.Up * 0.03f;
                float topY = i == 0 ? 0.62f : i == 1 ? 0.55f : 0.48f;
                var top = radial * 0.18f + Vec3.Up * topY;
                Vec3[] p = FloraBuilder.Pts(2);
                float[] r = FloraBuilder.Radii(2);
                p[0] = foot;
                p[1] = top;
                r[0] = 0.034f;
                r[1] = 0.024f;
                var st = new FloraBuilder.TubeStyle { Rgb = StemGrey, Tinted = true, Channel = MaterialChannel.Bark };
                b.Tube(p, r, 2, 3, st, 0.45f, 0.75f, false);
                // Three blades from the stem's top: one rising over the clump, two arching out sideways and down.
                for (int j = 0; j < 3; j++)
                {
                    double ab = a + (j - 1) * 0.95;
                    var rd = new Vec3((float)Math.Cos(ab), 0f, (float)Math.Sin(ab));
                    Vec3 root = top - Vec3.Up * 0.02f;
                    Vec3 ctrl, tip;
                    if (j == 1)
                    {
                        ctrl = radial * 0.26f + Vec3.Up * Math.Min(1.0f, topY + 0.4f);
                        tip = radial * 0.42f + Vec3.Up * Math.Min(0.86f, topY + 0.26f);
                    }
                    else
                    {
                        ctrl = rd * 0.3f + Vec3.Up * (topY + 0.24f);
                        tip = rd * 0.5f + Vec3.Up * (topY - 0.06f - 0.03f * i);
                    }
                    Blade(b, root, ctrl, tip, rd, j == 1 ? 0.11f : 0.13f, j == 1 ? 1.02f : 0.95f, (j + i) % 2 == 0 ? 25f : -25f);
                }
            }
        }

        /// <summary>
        /// The far clump (LOD3, 24 triangles): the stems as two crossed V cards splaying from the foot, three blades
        /// rising over the top and seven arching out to the edge with drooping tips, each a two-sided sail.
        /// </summary>
        private static void ClumpImpostor(FloraBuilder b)
        {
            uint stem = FloraBuilder.Leaf(StemGrey, 1f);
            for (int q = 0; q < 2; q++)
            {
                var across = q == 0 ? new Vec3(1f, 0f, 0f) : new Vec3(0f, 0f, 1f);
                Vec3 fn = q == 0 ? new Vec3(0f, 0f, 1f) : new Vec3(1f, 0f, 0f);
                var foot = new Vec3(0f, -0.03f, 0f);
                Vec3 l = across * -0.15f + Vec3.Up * 0.52f, r = across * 0.15f + Vec3.Up * 0.52f;
                for (int face = 0; face < 2; face++)
                {
                    Vec3 f = face == 0 ? fn : -fn;
                    int a = b.Vertex(foot, f, stem, MaterialChannel.Bark, 0.45f);
                    int c = b.Vertex(l, f, stem, MaterialChannel.Bark, 0.7f);
                    int d = b.Vertex(r, f, stem, MaterialChannel.Bark, 0.7f);
                    b.AddOriented(a, c, d, f);
                }
            }
            for (int i = 0; i < 10; i++)
            {
                bool rising = i < 3;
                double a = rising ? i * 2 * Math.PI / 3 + 0.5 : (i - 3) * 2 * Math.PI / 7 + 0.2;
                var rd = new Vec3((float)Math.Cos(a), 0f, (float)Math.Sin(a));
                Vec3 root = rd * (rising ? 0.08f : 0.13f) + Vec3.Up * (rising ? 0.52f : 0.48f);
                Vec3 ctrl = rising ? rd * 0.22f + Vec3.Up * 1.0f : rd * 0.3f + Vec3.Up * ((i & 1) == 0 ? 0.76f : 0.84f);
                Vec3 tip = rising ? rd * 0.38f + Vec3.Up * 0.86f : rd * 0.5f + Vec3.Up * ((i & 1) == 0 ? 0.42f : 0.5f);
                Sail(b, root, ctrl, tip, rd, rising ? 0.98f : 0.92f);
            }
        }

        /// <summary>The palm: a leaning ringed trunk, a star of eight arching fronds and a hanging dead frond (LOD2,
        /// ≤ 112 triangles); LOD3 a three-sided trunk under seven frond sails and an upright spear.</summary>
        public static void Palm(FloraBuilder b, int lod)
        {
            bool far = lod >= 3;
            var crown = new Vec3(PalmLean, PalmCrownY, 0f);
            uint barkRgb = 0x8A7A64u;
            if (far)
            {
                int ap = b.Vertex(crown + Vec3.Up * 0.02f, Vec3.Up, FloraBuilder.Fixed(barkRgb, 0.9f), MaterialChannel.Bark, 0.6f);
                int[] tb = new int[3];
                for (int i = 0; i < 3; i++)
                {
                    double a = i * 2 * Math.PI / 3 + 0.4;
                    var d = new Vec3((float)Math.Cos(a), 0f, (float)Math.Sin(a));
                    tb[i] = b.Vertex(new Vec3(d.X * 0.045f, -0.03f, d.Z * 0.045f), d, FloraBuilder.Fixed(barkRgb), MaterialChannel.Bark, 0.45f);
                }
                for (int i = 0; i < 3; i++) b.Tri(ap, tb[i], tb[(i + 1) % 3]);
            }
            else
            {
                Vec3[] p = FloraBuilder.Pts(3);
                float[] r = FloraBuilder.Radii(3);
                p[0] = new Vec3(0f, -0.03f, 0f);
                p[1] = new Vec3(0.3f * PalmLean, 0.4f, 0f);
                p[2] = crown;
                r[0] = 0.05f;
                r[1] = 0.038f;
                r[2] = 0.034f;
                var st = new FloraBuilder.TubeStyle { Rgb = barkRgb, PatchRgb = 0x6E604Eu, PatchAmount = 0.6f, Banded = true, Channel = MaterialChannel.Bark };
                b.Tube(p, r, 3, 4, st, 0.5f, 0.8f, false);
            }
            int fronds = far ? 7 : 8;
            for (int i = 0; i < fronds; i++)
            {
                double a = i * 2 * Math.PI / fronds + 0.2;
                float ca = (float)Math.Cos(a), sa = (float)Math.Sin(a);
                var radial = new Vec3(ca, 0f, sa);
                Vec3 root = crown + radial * 0.02f + Vec3.Up * 0.02f;
                // Three tiers round the crown: young fronds rising, mature ones arching out, old ones drooping.
                int tier = i % 3;
                float reach = tier == 0 ? 0.38f : tier == 1 ? 0.48f : 0.44f;
                float ctrlY = tier == 0 ? 1.06f : tier == 1 ? 0.88f : 0.8f;
                float tipY = tier == 0 ? 0.94f : tier == 1 ? 0.64f : 0.5f;
                Vec3 ctrl = new Vec3(PalmLean, 0f, 0f) + radial * (0.5f * reach) + Vec3.Up * ctrlY;
                Vec3 tip = new Vec3(PalmLean, 0f, 0f) + radial * reach + Vec3.Up * tipY;
                if (far) Sail(b, root, ctrl, tip, radial, 0.95f);
                else Blade(b, root, ctrl, tip, radial, 0.11f, 0.98f, (i & 1) == 0 ? 20f : -20f);
            }
            // The spear (the unopened young frond) standing up from the crown.
            Vec3 spearTip = crown + Vec3.Up * 0.24f + new Vec3(0.01f, 0f, 0f);
            if (far) Sail(b, crown + new Vec3(-0.03f, 0f, 0f), crown + new Vec3(0f, 0.14f, 0f), spearTip, new Vec3(0f, 0f, 1f), 1.02f);
            else
            {
                // A dead frond hanging against the trunk (the skirt the near LODs show).
                var dir = new Vec3(-0.4f, 0f, 0.3f).Normalized;
                Blade(b, crown, crown + dir * 0.1f + Vec3.Up * -0.08f, crown + dir * 0.12f - Vec3.Up * 0.22f, dir, 0.06f, 0.7f, 0f, 0x9A7A4Au);
            }
        }

        /// <summary>A two-sided blade along the quadratic curve root → ctrl → tip: three segments, widest past the
        /// middle, pointed (10 triangles), its width turned <paramref name="twistDeg"/> about the midrib (a leaf held
        /// at an angle shows from the side and from above). <paramref name="fixedRgb"/> ≠ 0 draws it in a fixed
        /// (untinted) colour.</summary>
        private static void Blade(FloraBuilder b, Vec3 root, Vec3 ctrl, Vec3 tip, Vec3 radial, float width, float shade, float twistDeg, uint fixedRgb = 0)
        {
            const int segs = 3;
            Vec3 flat = Vec3.Cross(Vec3.Up, radial);
            if (flat.Length < 1e-4f) flat = new Vec3(1f, 0f, 0f);
            flat = flat.Normalized;
            double tw = twistDeg * Math.PI / 180;
            Vec3 side = (flat * (float)Math.Cos(tw) + Vec3.Up * (float)Math.Sin(tw)).Normalized;
            Vec3 outward = (radial + Vec3.Up * 0.6f).Normalized;
            for (int face = 0; face < 2; face++)
            {
                int pl = -1, pr = -1;
                for (int s = 0; s <= segs; s++)
                {
                    float t = (float)s / segs;
                    Vec3 c = Bez(root, ctrl, tip, t);
                    Vec3 tan = BezTan(root, ctrl, tip, t);
                    Vec3 fn = Vec3.Cross(side, tan).Normalized;
                    if (fn.Y < 0f) fn = -fn;
                    if (face == 1) fn = -fn;
                    Vec3 n = ((outward - fn * Vec3.Dot(outward, fn)) * 0.7f + fn * 0.75f).Normalized;
                    float lit = shade * (0.82f + 0.18f * t);
                    uint rgba = fixedRgb != 0 ? FloraBuilder.Fixed(fixedRgb, lit) : FloraBuilder.Leaf(0xFFFFFFu, face == 0 ? lit : 0.86f * lit);
                    float ao = 0.55f + 0.45f * t;
                    if (s == segs)
                    {
                        int tv = b.Vertex(c, n, rgba, MaterialChannel.Foliage, ao);
                        b.AddOriented(pl, tv, pr, fn);
                        break;
                    }
                    float w = width * (s == 0 ? 0.3f : s == 1 ? 1f : 0.8f);
                    int l = b.Vertex(c - side * (0.5f * w), n, rgba, MaterialChannel.Foliage, ao);
                    int r = b.Vertex(c + side * (0.5f * w), n, rgba, MaterialChannel.Foliage, ao);
                    if (pl >= 0)
                    {
                        b.AddOriented(pl, l, r, fn);
                        b.AddOriented(pl, r, pr, fn);
                    }
                    pl = l;
                    pr = r;
                }
            }
        }

        /// <summary>A far blade as one two-sided triangle (root, the top of its arch, its drooping tip): 2 triangles.</summary>
        private static void Sail(FloraBuilder b, Vec3 root, Vec3 ctrl, Vec3 tip, Vec3 radial, float shade)
        {
            // Between the arch's top on the curve and its control point: the sail covers the blade's sweep (its
            // leaflets and its droop), as the blade's outline does a few dozen pixels away.
            Vec3 arch = Vec3.Lerp(Bez(root, ctrl, tip, 0.45f), ctrl, 0.5f);
            Vec3 fn = Vec3.Cross(arch - root, tip - root).Normalized;
            Vec3 outward = (radial + Vec3.Up * 0.6f).Normalized;
            if (Vec3.Dot(fn, outward) < 0f) fn = -fn;
            for (int face = 0; face < 2; face++)
            {
                Vec3 f = face == 0 ? fn : -fn;
                Vec3 n = ((outward - f * Vec3.Dot(outward, f)) * 0.7f + f * 0.75f).Normalized;
                uint lit = FloraBuilder.Leaf(0xFFFFFFu, face == 0 ? shade : 0.86f * shade);
                int a = b.Vertex(root, n, FloraBuilder.Leaf(0xFFFFFFu, 0.8f * shade), MaterialChannel.Foliage, 0.6f);
                int c = b.Vertex(arch, n, lit, MaterialChannel.Foliage, 1f);
                int d = b.Vertex(tip, n, lit, MaterialChannel.Foliage, 0.9f);
                b.AddOriented(a, c, d, f);
            }
        }

        private static Vec3 Bez(Vec3 a, Vec3 c, Vec3 b, float t)
        {
            float u = 1f - t;
            return a * (u * u) + c * (2f * u * t) + b * (t * t);
        }

        private static Vec3 BezTan(Vec3 a, Vec3 c, Vec3 b, float t)
        {
            Vec3 d = (c - a) * (2f * (1f - t)) + (b - c) * (2f * t);
            return d.Length < 1e-6f ? (b - a).Normalized : d.Normalized;
        }
    }
}
