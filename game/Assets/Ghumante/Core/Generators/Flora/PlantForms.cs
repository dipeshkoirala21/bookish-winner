using System;
using Ghumante.Core.Generators.Placement;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Generators.Flora
{
    /// <summary>
    /// The small things of the nature kit (docs/research/w2/ref_nature.md): shrubs and clipped hedges, marigold
    /// beds, roses, poinsettia (lalupate), bougainvillea, sunflowers, potted plants as they stand on Kathmandu
    /// doorsteps and roofs (terracotta pots, painted tins, cut jerry cans), the tulsi math pedestal, grass tufts,
    /// ferns, rocks and boulders, rice-straw stacks (kunyu) and the chautari platform. Built at model size in metres
    /// with the foot at the origin; <see cref="FloraMesher"/> normalises to the unit kit.
    /// </summary>
    internal static class PlantForms
    {
        public static void Build(TreeSpecies s, TreeKit k, int month)
        {
            FloraInfo info = FloraCatalog.Info(s);
            float h = info.ModelHeightM, w = info.ModelWidthM;
            uint leaf = FloraCatalog.LeafColour(s, month);
            float bloom = FloraCatalog.Bloom(s, month);
            uint bloomRgb = FloraCatalog.BloomColour(s);
            k.FarRgb = bloom >= 0.3f && bloomRgb != 0 ? FloraCatalog.FoliageColour(s, month) : 0u;
            switch (s)
            {
                case TreeSpecies.Hedge: Hedge(k, h, w, leaf); break;
                case TreeSpecies.MarigoldBed: MarigoldBed(k, h, w, leaf, bloom); break;
                case TreeSpecies.Rose: Rose(k, h, w, leaf, bloom, bloomRgb); break;
                case TreeSpecies.Poinsettia: Poinsettia(k, h, w, leaf, bloom, bloomRgb); break;
                case TreeSpecies.Bougainvillea: Bougainvillea(k, h, w, leaf, bloom, bloomRgb); break;
                case TreeSpecies.Sunflower: Sunflower(k, h, w, leaf, bloom); break;
                case TreeSpecies.PottedPlant: Pots(k, leaf, bloom); break;
                case TreeSpecies.TulsiMath: Tulsi(k, h, w, leaf); break;
                case TreeSpecies.GrassTuft: Grass(k, h, w, leaf, month); break;
                case TreeSpecies.Fern: Fern(k, h, w, leaf); break;
                case TreeSpecies.Rock: Rocks(k, h, w, false); break;
                case TreeSpecies.Boulder: Rocks(k, h, w, true); break;
                case TreeSpecies.StrawStack: StrawStack(k, h, w); break;
                default: Shrub(k, h, w, leaf); break;
            }
        }

        private static FloraBuilder.PuffStyle Bush(uint leaf, Vec3 centre, float radius)
        {
            var st = FloraBuilder.PuffStyle.Foliage(leaf, centre, radius);
            st.TopRgb = FloraBuilder.Mix(leaf, 0xB0D070u, 0.3f);
            st.AoLow = 0.45f;
            return st;
        }

        /// <summary>A rounded garden or roadside shrub: a mound of five to seven clumps on short woody stems.</summary>
        private static void Shrub(TreeKit k, float h, float w, uint leaf)
        {
            var c = new Vec3(0f, 0.5f * h, 0f);
            var st = Bush(leaf, c, 0.5f * w);
            int n = k.D.Near ? 6 : 3;
            if (k.D.Near)
                for (int i = 0; i < 3; i++)
                {
                    double a = i * 2.1;
                    k.Limb(Vec3.Zero - Vec3.Up * 0.05f, new Vec3((float)Math.Cos(a) * 0.25f * w, 0.45f * h, (float)Math.Sin(a) * 0.25f * w), 0.04f, 0.015f, 0f,
                           FloraBuilder.TubeStyle.Bark(0x5A4632u), 0.4f, 0.6f, 1, 3);
                }
            if (!k.D.Near)
            {
                // Mid distance: one smooth lumpy mound and a side lobe.
                k.Clump(new Vec3(0f, 0.48f * h, 0f), new Vec3(0.46f * w, 0.5f * h, 0.42f * w), 0.3f, st, 0, 1);
                k.Clump(new Vec3(0.2f * w, 0.35f * h, 0.1f * w), new Vec3(0.28f * w, 0.32f * h, 0.28f * w), 0.3f, st, 0, 1);
                return;
            }
            k.Clump(new Vec3(0f, 0.55f * h, 0f), new Vec3(0.32f * w, 0.42f * h, 0.32f * w), 0.2f, st, 4);
            for (int i = 0; i < n - 1; i++)
            {
                double a = i * 2 * Math.PI / (n - 1) + 0.4;
                float r = k.Rng.Range(0.2f, 0.27f) * w;
                k.Clump(new Vec3((float)Math.Cos(a) * 0.26f * w, 0.38f * h + k.Rng.Jitter(0.08f) * h, (float)Math.Sin(a) * 0.26f * w), new Vec3(r, 0.75f * r, r), 0.25f, st, 3);
            }
        }

        /// <summary>A clipped hedge segment (Duranta and box hedges of parks and compounds): a rounded block with a lumpy
        /// top, along X.</summary>
        private static void Hedge(TreeKit k, float h, float w, uint leaf)
        {
            float d = 0.3f * w; // depth (0.9 m for a 3 m segment)
            var st = Bush(leaf, new Vec3(0f, 0.5f * h, 0f), 0.5f * w);
            k.B.BevelBox(0f, 0f, 0.5f * w, 0.5f * d, -0.05f, 0.8f * h, 0.22f, 0f, FloraBuilder.Leaf(leaf, 0.8f), FloraBuilder.Leaf(FloraBuilder.Mix(leaf, 0xA8C860u, 0.3f), 1f),
                         MaterialChannel.Foliage, 0.45f, MaterialChannel.Foliage, k.Seed, 0.05f);
            int n = k.D.Near ? 4 : 2;
            for (int i = 0; i < n; i++)
            {
                float x = (-0.5f + (i + 0.5f) / n) * w * 0.9f;
                k.Clump(new Vec3(x, 0.8f * h, 0f), new Vec3(0.55f * w / n + 0.1f, 0.22f * h, 0.45f * d), 0.18f, st);
            }
        }

        /// <summary>
        /// A marigold (sayapatri) bed of parks, temple gardens and house fronts: a low brick edge around a soil mound
        /// packed with orange and yellow pom-pom heads on dark green foliage (most at Dashain and Tihar).
        /// </summary>
        private static void MarigoldBed(TreeKit k, float h, float w, uint leaf, float bloom)
        {
            float d = 0.5f * w; // a 2.6 × 1.3 m bed
            // Brick edge (a low bevelled kerb on all four sides) and the soil inside.
            uint brick = FloraBuilder.Fixed(0xA8553Au), cap = FloraBuilder.Fixed(0xB8654Au);
            float t = 0.09f, eh = 0.16f;
            if (k.D.Near)
            {
                k.B.BevelBox(0f, 0.5f * d - t, 0.5f * w, t, -0.05f, eh, 0.03f, 0f, brick, cap, MaterialChannel.Brick, 0.5f);
                k.B.BevelBox(0f, -0.5f * d + t, 0.5f * w, t, -0.05f, eh, 0.03f, 0f, brick, cap, MaterialChannel.Brick, 0.5f);
                k.B.BevelBox(0.5f * w - t, 0f, t, 0.5f * d - 2 * t, -0.05f, eh, 0.03f, 0f, brick, cap, MaterialChannel.Brick, 0.5f);
                k.B.BevelBox(-0.5f * w + t, 0f, t, 0.5f * d - 2 * t, -0.05f, eh, 0.03f, 0f, brick, cap, MaterialChannel.Brick, 0.5f);
                k.B.BevelBox(0f, 0f, 0.5f * w - 2 * t, 0.5f * d - 2 * t, -0.05f, 0.12f, 0.05f, 0f, FloraBuilder.Fixed(0x5A4030u), FloraBuilder.Fixed(0x6A4A34u),
                             MaterialChannel.Dirt, 0.6f);
            }
            else k.B.BevelBox(0f, 0f, 0.5f * w, 0.5f * d, -0.05f, eh, 0.05f, 0f, brick, FloraBuilder.Fixed(0x6A4A34u), MaterialChannel.Brick, 0.5f, MaterialChannel.Dirt);
            // Foliage mounds in two rows.
            var st = Bush(leaf, new Vec3(0f, 0.25f, 0f), 0.5f * w);
            int cols = k.D.Near ? 4 : 2;
            for (int r = 0; r < 2; r++)
                for (int c = 0; c < cols; c++)
                {
                    float x = (-0.5f + (c + 0.5f) / cols) * (w - 0.35f), z = (r == 0 ? -0.22f : 0.22f) * d;
                    k.Clump(new Vec3(x, 0.24f, z), new Vec3(0.5f * (w - 0.3f) / cols + 0.06f, 0.16f, 0.24f * d), 0.2f, st);
                }
            // Flower heads: pom-poms on top, orange with yellow among them.
            int heads = (int)((k.D.Near ? 46 : 6) * Math.Max(0.15f, bloom));
            for (int i = 0; i < heads; i++)
            {
                float x = k.Rng.Range(-0.5f, 0.5f) * (w - 0.35f), z = k.Rng.Range(-0.5f, 0.5f) * (d - 0.3f);
                float y = 0.3f + 0.14f * (float)Math.Cos(z / d * 3.0) + k.Rng.Jitter(0.04f);
                float r = k.Rng.Range(0.075f, 0.1f);
                uint col = FloraBuilder.Fixed(k.Rng.Chance(0.7f) ? 0xF5821Fu : 0xF6B21Bu, k.Rng.Range(0.92f, 1.08f));
                k.B.Blob(new Vec3(x, Math.Min(y, h - r), z), r, r * 0.8f, r, col, MaterialChannel.Foliage, 0.6f);
            }
        }

        /// <summary>A rose bush: dark glossy clumps on thorny stems with red and pink roses (cupped flowers).</summary>
        private static void Rose(TreeKit k, float h, float w, uint leaf, float bloom, uint bloomRgb)
        {
            Shrub(k, h, w, leaf);
            int n = (int)((k.D.Near ? 12 : 4) * bloom);
            for (int i = 0; i < n; i++)
            {
                Vec3 o;
                Vec3 p = k.OnClumps(out o, 0f);
                uint c = FloraBuilder.Fixed(k.Rng.Chance(0.7f) ? bloomRgb : 0xF06A8Au, k.Rng.Range(0.95f, 1.05f));
                if (k.D.Near) k.B.Star(p + o * 0.04f, o, 5, 0.08f, 0.08f, 0.03f, c, FloraBuilder.Fixed(0x9A1028u), MaterialChannel.Foliage, 0.9f, k.Rng.Range(0f, 1f));
                else k.B.Blob(p, 0.07f, 0.05f, 0.07f, c, MaterialChannel.Foliage, 0.8f);
            }
        }

        /// <summary>
        /// Poinsettia (lalupate) by garden walls: a woody shrub-tree with stiff branches ending in leaf clumps, crowned
        /// from Tihar to February with flat red star-shaped bracts.
        /// </summary>
        private static void Poinsettia(TreeKit k, float h, float w, uint leaf, float bloom, uint bloomRgb)
        {
            var f = BroadleafForm.Default(h, w, leaf, 0x6A5A48u);
            f.TrunkR = 0.05f;
            f.ForkFrac = 0.25f;
            f.Stems = 2;
            f.Lobes = 0;
            f.Limbs = 5;
            f.CrownY = 0.62f;
            f.CrownRy = 0.38f;
            f.Clumps = 7;
            f.ClumpR = 0.36f;
            f.ClumpFlat = 0.85f;
            f.Fringe = 3;
            f.LeafTop = FloraBuilder.Mix(leaf, 0x9CC060u, 0.3f);
            f.LeanM = 0.1f;
            k.Broadleaf(f);
            // The red bract whorls crown every branch tip in winter (lalupate by the house in Tihar to Shivaratri).
            int n = (int)((k.D.Near ? 19 : 4) * bloom);
            uint red = FloraBuilder.Fixed(bloomRgb), eye = FloraBuilder.Fixed(0xE8C030u);
            for (int i = 0; i < n; i++)
            {
                Vec3 o;
                Vec3 p = k.OnClumps(out o, 0.1f);
                Vec3 nrm = (o + Vec3.Up * 1.2f).Normalized;
                if (k.D.Near) k.B.Star(p + o * 0.05f, nrm, 6, k.Rng.Range(0.26f, 0.34f), 0.09f, 0.04f, red, eye, MaterialChannel.Foliage, 0.9f, k.Rng.Range(0f, 1f));
                else k.B.Blob(p, 0.2f, 0.08f, 0.2f, red, MaterialChannel.Foliage, 0.8f);
            }
        }

        /// <summary>
        /// Bougainvillea over compound walls and gates: woody arching canes under a cascading mass of magenta bracts
        /// with green showing through (heaviest in spring and again in October-November).
        /// </summary>
        private static void Bougainvillea(TreeKit k, float h, float w, uint leaf, float bloom, uint bloomRgb)
        {
            var cane = FloraBuilder.TubeStyle.Bark(0x6A5442u, 0.08f);
            int canes = k.D.Near ? 5 : 2;
            for (int i = 0; i < canes; i++)
            {
                float x = (-0.5f + (i + 0.5f) / canes) * 0.6f * w;
                k.Limb(new Vec3(x * 0.3f, -0.05f, 0f), new Vec3(x, 0.8f * h, k.Rng.Jitter(0.2f)), 0.05f, 0.025f, 0.2f, cane, 0.4f, 0.7f);
            }
            var c = new Vec3(0f, 0.65f * h, 0f);
            var leafSt = Bush(leaf, c, 0.5f * w);
            var flower = FloraBuilder.PuffStyle.Solid(bloomRgb, MaterialChannel.Foliage);
            flower.TopRgb = FloraBuilder.Mix(bloomRgb, 0xF060A0u, 0.4f);
            flower.ShadeLow = 0.78f;
            int n = k.D.Near ? 9 : 4;
            for (int i = 0; i < n; i++)
            {
                // A cascade: a high ridge with lobes tumbling down the sides.
                float u = (i + 0.5f) / n;
                float x = (u - 0.5f) * w * 0.9f;
                float y = h * (0.78f - 0.35f * (float)Math.Pow(Math.Abs(u - 0.5f) * 2f, 1.5f)) + k.Rng.Jitter(0.08f) * h;
                float r = k.Rng.Range(0.16f, 0.22f) * w;
                var st = k.Rng.Chance(0.25f + 0.65f * bloom) ? flower : leafSt;
                k.Clump(new Vec3(x, y, k.Rng.Jitter(0.12f) * w), new Vec3(r, r * 0.85f, r * 0.8f), 0.3f, st, 4, -1, 0.5f, 0.5f, 0.8f);
            }
            // Hanging sprays at the front edge.
            int sprays = (int)((k.D.Near ? 6 : 2) * Math.Max(0.4f, bloom));
            for (int i = 0; i < sprays; i++)
            {
                var p = new Vec3(k.Rng.Range(-0.4f, 0.4f) * w, 0.55f * h, k.Rng.Range(0.1f, 0.2f) * w);
                k.B.PuffAlong(p, new Vec3(0f, -1f, 0.2f), 0.35f * h * 0.5f, 0.12f * w, 0, 0.25f, k.Seed ^ (uint)i, bloom > 0.3f ? flower : leafSt);
            }
        }

        /// <summary>Sunflowers by village fields and gardens (monsoon to Dashain): three stalks with broad leaves and big
        /// yellow heads with brown centres, nodding to the east.</summary>
        private static void Sunflower(TreeKit k, float h, float w, uint leaf, float bloom)
        {
            var stalk = new FloraBuilder.TubeStyle { Rgb = 0x6A9A3Au, Channel = MaterialChannel.Foliage, Tinted = true };
            uint lc = FloraBuilder.Leaf(leaf, 1f);
            for (int i = 0; i < 3; i++)
            {
                double a = i * 2.1 + 0.3;
                var foot = new Vec3((float)Math.Cos(a) * 0.2f * w, -0.05f, (float)Math.Sin(a) * 0.2f * w);
                float hh = h * (i == 0 ? 0.95f : k.Rng.Range(0.72f, 0.88f));
                var head = foot + new Vec3(0.05f, hh - 0.15f, 0.08f);
                k.Limb(foot, head, 0.035f, 0.025f, 0f, stalk, 0.5f, 0.9f, k.D.Near ? 2 : 1, k.D.Near ? 4 : 3);
                int leaves = k.D.Near ? 3 : 1;
                for (int q = 0; q < leaves; q++)
                {
                    double b = a + q * 2.4;
                    var dir = new Vec3((float)Math.Cos(b), 0.3f, (float)Math.Sin(b)).Normalized;
                    k.B.Blade(foot + Vec3.Up * (0.35f + 0.45f * q / Math.Max(1, leaves)) * hh, dir, new Vec3(-(float)Math.Sin(b), 0f, (float)Math.Cos(b)), 0.32f, 0.04f, 0.22f, 0f,
                              0.08f, 0.15f, 2, lc, MaterialChannel.Foliage, 0.6f, 0.9f);
                }
                if (bloom <= 0f) continue;
                Vec3 face = new Vec3(0.6f, 0.2f, 0.75f).Normalized;
                if (k.D.Near)
                {
                    k.B.Star(head + face * 0.02f, face, 12, 0.2f, 0.11f, 0f, FloraBuilder.Fixed(0xF6C21Bu), FloraBuilder.Fixed(0xF6C21Bu), MaterialChannel.Foliage, 0.95f);
                    k.B.Puff(head + face * 0.04f, new Vec3(0.1f, 0.1f, 0.1f), 0, 0.05f, k.Seed, FloraBuilder.PuffStyle.Solid(0x5A3A1Eu, MaterialChannel.Foliage));
                }
                else k.B.Blob(head, 0.18f, 0.18f, 0.18f, FloraBuilder.Fixed(0xF6C21Bu), MaterialChannel.Foliage, 0.8f);
            }
        }

        /// <summary>
        /// A doorstep or rooftop group of potted plants: a big terracotta pot with a leafy plant and red geraniums, a
        /// painted ghee tin with marigolds and a cut blue jerry can with a spiky aloe-like plant.
        /// </summary>
        private static void Pots(TreeKit k, uint leaf, float bloom)
        {
            var st = Bush(leaf, new Vec3(0f, 0.6f, 0f), 0.4f);
            int sides = k.D.Near ? 10 : 6;
            // 1. Terracotta pot (lathe with a rolled rim) and a leafy plant with geraniums.
            float[] r = { 0.13f, 0.15f, 0.18f, 0.2f, 0.21f, 0.2f };
            float[] y = { 0f, 0.05f, 0.2f, 0.33f, 0.36f, 0.4f };
            k.B.Lathe(new Vec3(-0.08f, 0f, 0.02f), r, y, k.D.Near ? 6 : 3, sides, FloraBuilder.Fixed(0xB8643Cu), MaterialChannel.Brick, 0.5f, 0.9f, true,
                      FloraBuilder.Fixed(0x4A3426u), MaterialChannel.Dirt, 0.34f);
            k.Clump(new Vec3(-0.08f, 0.55f, 0.02f), new Vec3(0.22f, 0.2f, 0.22f), 0.25f, st);
            int g = (int)((k.D.Near ? 7 : 2) * Math.Max(0.3f, bloom));
            for (int i = 0; i < g; i++)
            {
                double a = i * 2.39996;
                var p = new Vec3(-0.08f + (float)Math.Cos(a) * 0.14f, 0.68f + k.Rng.Jitter(0.04f), 0.02f + (float)Math.Sin(a) * 0.14f);
                k.B.Blob(p, 0.045f, 0.04f, 0.045f, FloraBuilder.Fixed(i % 3 == 0 ? 0xF05A78u : 0xE0302Au), MaterialChannel.Foliage, 0.8f);
            }
            // 2. Painted ghee tin (square) with a small marigold.
            uint tin = FloraBuilder.Fixed(0x2E7DBAu);
            k.B.BevelBox(0.2f, -0.12f, 0.1f, 0.1f, 0f, 0.26f, 0.015f, 0.3f, tin, FloraBuilder.Fixed(0x4A3426u), MaterialChannel.Metal, 0.5f, MaterialChannel.Dirt);
            k.Clump(new Vec3(0.2f, 0.36f, -0.12f), new Vec3(0.12f, 0.11f, 0.12f), 0.25f, st);
            if (k.D.Near)
                for (int i = 0; i < 4; i++)
                {
                    double a = i * 1.7;
                    k.B.Blob(new Vec3(0.2f + (float)Math.Cos(a) * 0.07f, 0.44f, -0.12f + (float)Math.Sin(a) * 0.07f), 0.035f, 0.03f, 0.035f,
                             FloraBuilder.Fixed(0xF5821Fu), MaterialChannel.Foliage, 0.8f);
                }
            // 3. Cut jerry can (blue plastic) with a spiky plant.
            k.B.BevelBox(0.12f, 0.2f, 0.09f, 0.07f, 0f, 0.2f, 0.02f, -0.2f, FloraBuilder.Fixed(0x3A8AD8u), FloraBuilder.Fixed(0x4A3426u), MaterialChannel.Plain, 0.5f,
                         MaterialChannel.Dirt);
            int blades = k.D.Near ? 7 : 3;
            uint lc = FloraBuilder.Leaf(FloraBuilder.Mix(leaf, 0x6A9A6Au, 0.4f), 1f);
            for (int i = 0; i < blades; i++)
            {
                double a = i * 2 * Math.PI / blades;
                var dir = new Vec3((float)Math.Cos(a) * 0.45f, 1f, (float)Math.Sin(a) * 0.45f).Normalized;
                k.B.Blade(new Vec3(0.12f, 0.19f, 0.2f), dir, new Vec3(-(float)Math.Sin(a), 0f, (float)Math.Cos(a)), 0.3f, 0.05f, 0.04f, 0f, 0.03f, 0.25f, 2, lc,
                          MaterialChannel.Foliage, 0.6f, 1f);
            }
        }

        /// <summary>
        /// Tulsi math: the square masonry pedestal of Hindu courtyards and doorsteps, painted ochre and red with a
        /// stepped base, a small lamp niche in front and flared horns at the top corners, the holy basil growing out
        /// of it.
        /// </summary>
        private static void Tulsi(TreeKit k, float h, float w, uint leaf)
        {
            uint ochre = FloraBuilder.Fixed(0xE8A62Eu), red = FloraBuilder.Fixed(0xC8302Au), white = FloraBuilder.Fixed(0xF2ECDCu);
            float hw = 0.5f * w;
            k.B.BevelBox(0f, 0f, hw, hw, -0.05f, 0.14f, 0.03f, 0f, red, red, MaterialChannel.Plaster, 0.5f);
            if (k.D.Near) k.B.BevelBox(0f, 0f, hw * 0.78f, hw * 0.78f, 0.14f, 0.22f, 0.03f, 0f, white, white, MaterialChannel.Plaster, 0.6f);
            k.B.BevelBox(0f, 0f, hw * 0.6f, hw * 0.6f, 0.14f, 0.78f, 0.04f, 0f, ochre, ochre, MaterialChannel.Plaster, 0.6f);
            if (k.D.Near) k.B.BevelBox(0f, 0f, hw * 0.72f, hw * 0.72f, 0.78f, 0.88f, 0.03f, 0f, red, red, MaterialChannel.Plaster, 0.7f);
            k.B.BevelBox(0f, 0f, hw * 0.82f, hw * 0.82f, 0.88f, 1.02f, 0.05f, 0f, ochre, FloraBuilder.Fixed(0x4A3426u), MaterialChannel.Plaster, 0.75f, MaterialChannel.Dirt);
            if (k.D.Near)
            {
                // Lamp niche on the front (+Z) face: a dark recess with a red frame.
                float zf = hw * 0.6f + 0.005f;
                k.B.BevelBox(0f, zf, 0.09f, 0.012f, 0.4f, 0.6f, 0.01f, 0f, red, red, MaterialChannel.Plaster, 0.7f);
                k.B.BevelBox(0f, zf + 0.008f, 0.06f, 0.008f, 0.43f, 0.57f, 0.005f, 0f, FloraBuilder.Fixed(0x2A1E18u), FloraBuilder.Fixed(0x2A1E18u), MaterialChannel.Plaster, 0.3f);
                // Corner horns.
                for (int i = 0; i < 4; i++)
                {
                    float sx = i % 2 == 0 ? -1f : 1f, sz = i < 2 ? -1f : 1f;
                    var a = new Vec3(sx * hw * 0.7f, 1.0f, sz * hw * 0.7f);
                    k.B.PuffAlong(a + new Vec3(sx, 1.4f, sz).Normalized * 0.06f, new Vec3(sx, 1.4f, sz).Normalized, 0.09f, 0.04f, 0, 0f, k.Seed,
                                  FloraBuilder.PuffStyle.Solid(0xE8A62Eu, MaterialChannel.Plaster));
                }
            }
            // The tulsi: a small purplish-green bush of upright sprigs.
            var st = Bush(leaf, new Vec3(0f, 1.25f, 0f), 0.25f);
            k.Limb(new Vec3(0f, 0.98f, 0f), new Vec3(0f, 1.2f, 0f), 0.025f, 0.015f, 0f, FloraBuilder.TubeStyle.Bark(0x5A4A3Au), 0.6f, 0.8f, 1, 3);
            k.Clump(new Vec3(0f, 1.22f, 0f), new Vec3(0.17f, 0.15f, 0.17f), 0.3f, st);
            int sprigs = k.D.Near ? 5 : 2;
            for (int i = 0; i < sprigs; i++)
            {
                double a = i * 2 * Math.PI / sprigs;
                var c = new Vec3((float)Math.Cos(a) * 0.1f, 1.33f, (float)Math.Sin(a) * 0.1f);
                k.B.PuffAlong(c, new Vec3((float)Math.Cos(a) * 0.3f, 1f, (float)Math.Sin(a) * 0.3f), 0.12f, 0.05f, 0, 0.2f, k.Seed ^ (uint)i,
                              k.Rng.Chance(0.5f) ? st : FloraBuilder.PuffStyle.Foliage(0x6A4A6Au, new Vec3(0f, 1.25f, 0f), 0.25f));
            }
        }

        /// <summary>A grass tuft of the hills and field edges: a fountain of blades, a few seed heads in autumn.</summary>
        private static void Grass(TreeKit k, float h, float w, uint leaf, int month)
        {
            int n = k.D.Near ? 16 : 6;
            uint baseC = FloraBuilder.Leaf(FloraBuilder.Mix(leaf, 0x3E5A22u, 0.35f), 1f), tip = FloraBuilder.Leaf(FloraBuilder.Mix(leaf, 0xD8D890u, 0.25f), 1f);
            for (int i = 0; i < n; i++)
            {
                double a = i * 2.39996 + k.Rng.Jitter(0.3f);
                float lean = k.Rng.Range(0.35f, 1f);
                float hh = h * k.Rng.Range(0.6f, 1f);
                var b = new Vec3((float)Math.Cos(a) * 0.06f, -0.03f, (float)Math.Sin(a) * 0.06f);
                var tipP = new Vec3((float)Math.Cos(a) * lean * 0.5f * w, hh, (float)Math.Sin(a) * lean * 0.5f * w);
                k.B.GrassBlade(b, tipP, k.Rng.Range(0.035f, 0.055f), baseC, tip, 0.35f);
            }
            if (k.D.Near && (month >= 9 && month <= 11))
                for (int i = 0; i < 3; i++)
                {
                    double a = i * 2.2;
                    var p = new Vec3((float)Math.Cos(a) * 0.12f * w, h * 0.9f, (float)Math.Sin(a) * 0.12f * w);
                    k.B.PuffAlong(p, new Vec3((float)Math.Cos(a) * 0.3f, 1f, (float)Math.Sin(a) * 0.3f), 0.045f, 0.012f, 0, 0f, k.Seed,
                                  FloraBuilder.PuffStyle.Solid(0xC8B480u, MaterialChannel.Grass));
                }
        }

        /// <summary>A fern of the forest floor and shady walls: arching folded fronds from a crown.</summary>
        private static void Fern(TreeKit k, float h, float w, uint leaf)
        {
            int n = k.D.Near ? 8 : 4;
            uint c = FloraBuilder.Leaf(leaf, 1f), rib = FloraBuilder.Leaf(FloraBuilder.Mix(leaf, 0x2E4A1Eu, 0.4f), 1f);
            for (int i = 0; i < n; i++)
            {
                double a = i * 2 * Math.PI / n + k.Rng.Jitter(0.25f);
                float pitch = k.Rng.Range(1.1f, 1.8f);
                var dir = new Vec3((float)Math.Cos(a), pitch, (float)Math.Sin(a)).Normalized;
                var side = new Vec3(-(float)Math.Sin(a), 0f, (float)Math.Cos(a));
                float len = 0.62f * w * k.Rng.Range(0.85f, 1.1f);
                k.B.Blade(new Vec3(0f, 0.02f, 0f), dir, side, len, 0.03f, 0.2f, 0f, 0.45f * h, 0.3f, k.D.Near ? 4 : 2, c, MaterialChannel.Foliage, 0.4f, 0.9f, rib,
                          k.D.Near ? 0.5f : 0f);
            }
        }

        /// <summary>A rock (two stones) or a boulder (a big mossy one with a smaller one leaning on it).</summary>
        private static void Rocks(TreeKit k, float h, float w, bool boulder)
        {
            int sub = k.D.Near ? 1 : 0;
            uint grey = boulder ? 0x8E887Eu : 0x9C968Cu, moss = boulder ? 0x5E7A3Au : 0x8A9A5Au;
            k.B.Rock(new Vec3(0f, 0.3f * h, 0f), new Vec3(0.42f * w, 0.72f * h, 0.36f * w), sub, 0.3f, k.Seed, grey, moss, boulder ? 0.7f : 0.35f);
            k.B.Rock(new Vec3(0.36f * w, 0.18f * h, -0.18f * w), new Vec3(0.17f * w, 0.36f * h, 0.15f * w), sub, 0.35f, k.Seed ^ 0x77u, FloraBuilder.Mix(grey, 0xB0A890u, 0.3f),
                     moss, 0.3f);
        }

        /// <summary>
        /// A rice-straw stack (kunyu) of the valley's fields from the October harvest to spring (ref: straw stacks by
        /// the Kathmandu ring road and in the Pokhara valley): a drum of sheaves laid round a central pole, widening a
        /// little to the eave, then a steep conical thatch roof whose eave shows as a step, in tiers of lighter and
        /// darker straw, with a top knot and the pole tip showing.
        /// </summary>
        private static void StrawStack(TreeKit k, float h, float w)
        {
            // Profile (height fraction, radius fraction): foot, drum, eave, step in, cone, tip.
            float[] pt = k.D.Near ? NearStack : FarStack, pr = k.D.Near ? NearStackR : FarStackR;
            int n = pt.Length, sides = k.D.Near ? 14 : 7;
            float[] r = new float[n], y = new float[n];
            uint[] col = new uint[n];
            for (int i = 0; i < n; i++)
            {
                float t = pt[i];
                y[i] = -0.05f + t * 0.92f * h;
                r[i] = 0.5f * w * pr[i];
                // Thatch tiers: the roof rings alternate light (sunlit straw ends) and darker (the tier below).
                bool roof = t > 0.45f;
                uint c = roof ? (i % 2 == 0 ? 0xDDBB62u : 0xB8924Au) : (i % 2 == 0 ? 0xCFAA55u : 0xC39D4Cu);
                col[i] = FloraBuilder.Fixed(c, 0.9f + 0.12f * t);
            }
            k.B.Lathe(Vec3.Zero, r, y, n, sides, col[0], MaterialChannel.Grass, 0.45f, 0.95f, false, 0, MaterialChannel.Plain, float.NaN, 0.06f, k.Seed, col);
            // Top knot and the pole tip.
            k.B.Puff(new Vec3(0f, 0.9f * h, 0f), new Vec3(0.07f * w, 0.06f * h, 0.07f * w), 0, 0.2f, k.Seed, FloraBuilder.PuffStyle.Solid(0xB8904Au, MaterialChannel.Grass));
            k.Limb(new Vec3(0f, 0.85f * h, 0f), new Vec3(0.02f, h, 0f), 0.04f, 0.03f, 0f, FloraBuilder.TubeStyle.Bark(0x6A5440u), 0.8f, 0.9f, 1, 4);
        }

        private static readonly float[] NearStack = { 0f, 0.12f, 0.3f, 0.42f, 0.46f, 0.56f, 0.66f, 0.76f, 0.86f, 0.94f, 1f };
        private static readonly float[] NearStackR = { 0.8f, 0.9f, 0.97f, 1f, 0.9f, 0.74f, 0.58f, 0.42f, 0.26f, 0.13f, 0.05f };
        private static readonly float[] FarStack = { 0f, 0.42f, 0.46f, 0.72f, 1f };
        private static readonly float[] FarStackR = { 0.82f, 1f, 0.9f, 0.48f, 0.05f };

        /// <summary>
        /// The chautari (W2_DESIGN 5.8): a square platform of dressed stone around a pipal or bar, three coursed walls
        /// with a projecting slab top, a lower porter ledge (where a doko is set down) along the south face with a
        /// step, and a small red-daubed shrine stone at one corner. Built at 6 × 6 m, 0.8 m high.
        /// </summary>
        public static void Chautari(FloraBuilder b, bool near)
        {
            const float half = 3f, top = 0.8f;
            uint stone = FloraBuilder.Fixed(0x9A948Au), stone2 = FloraBuilder.Fixed(0x8C867Cu), slab = FloraBuilder.Fixed(0xB0AA9Fu);
            int courses = near ? 3 : 1;
            for (int c = 0; c < courses; c++)
            {
                float y0 = -0.3f + (top + 0.3f - 0.1f) * c / courses, y1 = -0.3f + (top + 0.3f - 0.1f) * (c + 1) / courses;
                float inset = 0.02f * c;
                b.BevelBox(0f, 0f, half - inset, half - inset, y0, y1, 0.05f, 0f, c % 2 == 0 ? stone : stone2, stone, MaterialChannel.Stone, 0.45f);
            }
            // Slab top with a small overhang and bevel; the tree grows through it (the instance trunk covers the hole).
            b.BevelBox(0f, 0f, half + 0.08f, half + 0.08f, top - 0.1f, top, 0.05f, 0f, slab, slab, MaterialChannel.Flagstone, 0.75f);
            // Porter ledge (the dhalo) on the south face, then a step.
            b.BevelBox(0f, -half - 0.35f, half * 0.8f, 0.35f, -0.3f, 0.48f, 0.05f, 0f, stone2, slab, MaterialChannel.Stone, 0.5f, MaterialChannel.Flagstone);
            if (near)
            {
                b.BevelBox(0f, -half - 0.85f, half * 0.3f, 0.18f, -0.3f, 0.2f, 0.04f, 0f, stone, slab, MaterialChannel.Stone, 0.5f, MaterialChannel.Flagstone);
                // A shrine stone daubed with red sindur and a marigold at the north-east corner.
                b.Rock(new Vec3(half - 0.45f, top + 0.12f, half - 0.45f), new Vec3(0.16f, 0.2f, 0.14f), 0, 0.2f, 0x5348u, 0xB8302Au, 0xD84A2Au, 0.8f);
                b.Blob(new Vec3(half - 0.3f, top + 0.05f, half - 0.62f), 0.06f, 0.05f, 0.06f, FloraBuilder.Fixed(0xF5821Fu), MaterialChannel.Foliage, 0.8f);
            }
        }
    }
}
