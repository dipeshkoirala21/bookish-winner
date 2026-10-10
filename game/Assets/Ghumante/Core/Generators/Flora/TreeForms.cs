using System;
using Ghumante.Core.Generators.Placement;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Generators.Flora
{
    /// <summary>
    /// The tree recipes of the nature kit, one per <see cref="TreeSpecies"/> tree, modelled on photos of the trees as
    /// they stand in and around the Kathmandu Valley (docs/research/w2/ref_nature.md: silhouette, proportions,
    /// bark, leaf colour and the bloom of each species). Built at the catalogue's model size in metres with the
    /// foot at the origin; <see cref="FloraMesher"/> normalises to the unit kit. Cartoon stylisation only simplifies
    /// leaves into lumpy clumps with a leafy fringe and slightly boldens trunks; silhouettes and colours follow the
    /// real trees.
    /// </summary>
    internal static class TreeForms
    {
        private const uint Moss = 0x6B7A3Au;

        public static void Build(TreeSpecies s, TreeKit k, int month)
        {
            FloraInfo info = FloraCatalog.Info(s);
            float h = info.ModelHeightM, w = info.ModelWidthM;
            uint leaf = FloraCatalog.LeafColour(s, month), bark = FloraCatalog.BarkColour(s);
            float bloom = FloraCatalog.Bloom(s, month);
            uint bloomRgb = FloraCatalog.BloomColour(s);
            k.FarRgb = bloom >= 0.3f ? FloraCatalog.FoliageColour(s, month) : 0u;
            switch (s)
            {
                case TreeSpecies.Pipal: Pipal(k, h, w, leaf, bark, month); break;
                case TreeSpecies.Bar: Bar(k, h, w, leaf, bark); break;
                case TreeSpecies.Jacaranda: Jacaranda(k, h, w, leaf, bark, bloom, bloomRgb); break;
                case TreeSpecies.SilkyOak: SilkyOak(k, h, w, leaf, bark, bloom, bloomRgb); break;
                case TreeSpecies.Bottlebrush: Bottlebrush(k, h, w, leaf, bark, bloom, bloomRgb); break;
                case TreeSpecies.Camphor: Camphor(k, h, w, leaf, bark, month); break;
                case TreeSpecies.Eucalyptus: Eucalyptus(k, h, w, leaf, bark); break;
                case TreeSpecies.Bamboo: Bamboo(k, h, w, leaf, bark); break;
                case TreeSpecies.Palm: Palm(k, h, w, leaf, bark); break;
                case TreeSpecies.Schima: Schima(k, h, w, leaf, bark, bloom, bloomRgb, month); break;
                case TreeSpecies.Castanopsis: Castanopsis(k, h, w, leaf, bark, bloom, bloomRgb); break;
                case TreeSpecies.Alnus: Alnus(k, h, w, leaf, bark); break;
                case TreeSpecies.ChirPine: ChirPine(k, h, w, leaf, bark); break;
                case TreeSpecies.Oak: Oak(k, h, w, leaf, bark); break;
                case TreeSpecies.Rhododendron: Rhododendron(k, h, w, leaf, bark, bloom, bloomRgb); break;
                case TreeSpecies.BrownOak: BrownOak(k, h, w, leaf, bark); break;
                case TreeSpecies.Sal: Sal(k, h, w, leaf, bark, month); break;
                case TreeSpecies.Banana: Banana(k, h, w, leaf, bark); break;
                default: Broadleaf(k, h, w, leaf, bark); break;
            }
        }

        /// <summary>Bloom and detail counts: all at LOD0; none at LOD1, where the crown colour carries the bloom.</summary>
        private static int Count(TreeKit k, int near)
        {
            return k.D.Near ? near : 0;
        }

        /// <summary>A sunlit top colour: the leaf colour toward a warm yellow-green.</summary>
        private static uint Sunlit(uint leaf, float t = 0.3f)
        {
            return FloraBuilder.Mix(leaf, 0xC8E070u, t);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Broadleaf family

        /// <summary>Generic street and village broadleaf: round crown on a short trunk with four or five limbs.</summary>
        private static void Broadleaf(TreeKit k, float h, float w, uint leaf, uint bark)
        {
            var f = BroadleafForm.Default(h, w, leaf, bark);
            f.LeafTop = Sunlit(leaf);
            k.Broadleaf(f);
        }

        /// <summary>
        /// Pipal (Ficus religiosa): a massive pale grey fluted trunk (fused root ridges), splitting into five or six
        /// thick limbs that spread wide; a broad layered dome of glossy clumps, often as wide as tall; a coppery-pink
        /// flush of new leaves in March-April. Sacred trees carry a red tika band and white thread round the trunk.
        /// </summary>
        private static void Pipal(TreeKit k, float h, float w, uint leaf, uint bark, int month)
        {
            var f = BroadleafForm.Default(h, w, leaf, bark);
            f.TrunkR = 0.05f * h;
            f.ForkFrac = 0.34f;
            f.Lobes = 7;
            f.LobeDepth = 0.45f;
            f.Bark = FloraBuilder.TubeStyle.Bark(bark, 0.09f);
            f.Bark.PatchRgb = 0x7E786Eu;
            f.Bark.PatchAmount = 0.35f;
            f.Limbs = 6;
            f.LimbR = 0.62f;
            f.CrownY = 0.66f;
            f.CrownRx = 0.5f;
            f.CrownRy = 0.3f;
            f.Umbrella = true;
            f.Clumps = 12;
            f.ClumpR = 0.27f;
            f.ClumpFlat = 0.78f;
            f.Fringe = 5;
            f.LeafTop = Sunlit(leaf, 0.35f);
            // The copper-pink flush of new leaves shows at the branch tips (the fringe), not as whole brown clumps.
            if (month == 3 || month == 4) f.FringeRgb = 0xC27C5Eu;
            k.Broadleaf(f);
            if (k.D.Near)
            {
                // Puja band: red tika paint and a white thread wound round the trunk at chest height.
                float r = f.TrunkR * 1.14f;
                Band(k, 1.0f, 1.3f, r, 0xC8202Au);
                Band(k, 1.38f, 1.45f, r * 0.98f, 0xF2EEE2u);
            }
        }

        /// <summary>A painted band (or wound thread) round the trunk between two heights.</summary>
        private static void Band(TreeKit k, float y0, float y1, float r, uint rgb)
        {
            Vec3[] p = FloraBuilder.Pts(2);
            float[] rr = FloraBuilder.Radii(2);
            p[0] = new Vec3(0f, y0, 0f);
            p[1] = new Vec3(0f, y1, 0f);
            rr[0] = r;
            rr[1] = r * 0.97f;
            var st = new FloraBuilder.TubeStyle { Rgb = rgb, Channel = MaterialChannel.Fabric };
            k.B.Tube(p, rr, 2, 10, st, 0.8f, 0.85f, false);
        }

        /// <summary>
        /// Bar (banyan, Ficus benghalensis): a complex trunk of fused stems, a very wide low dome of dark glossy
        /// clumps, and aerial roots hanging from the limbs (some reaching the ground as prop roots).
        /// </summary>
        private static void Bar(TreeKit k, float h, float w, uint leaf, uint bark)
        {
            var f = BroadleafForm.Default(h, w, leaf, bark);
            f.TrunkR = 0.055f * h;
            f.ForkFrac = 0.3f;
            f.Lobes = 6;
            f.LobeDepth = 0.4f;
            f.Stems = 3;
            f.Bark = FloraBuilder.TubeStyle.Bark(bark, 0.1f);
            f.Limbs = 7;
            f.LimbR = 0.55f;
            f.CrownY = 0.62f;
            f.CrownRx = 0.5f;
            f.CrownRy = 0.33f;
            f.Umbrella = true;
            f.Clumps = 9;
            f.ClumpR = 0.3f;
            f.ClumpFlat = 0.74f;
            f.Fringe = 3;
            f.LeafTop = FloraBuilder.Mix(leaf, 0x6AA848u, 0.45f);
            k.Broadleaf(f);
            // Aerial roots from the limbs: thin strands hanging down; every third reaches the ground (a prop root).
            int roots = k.D.Near ? 12 : 2;
            var st = FloraBuilder.TubeStyle.Bark(0x9A8A74u, 0.05f);
            for (int i = 0; i < roots; i++)
            {
                Vec3 tip = k.Tips[i % Math.Max(1, k.TipCount)];
                float t = k.Rng.Range(0.35f, 0.85f);
                Vec3 top = Vec3.Lerp(new Vec3(0f, f.ForkFrac * h, 0f), tip, t) + new Vec3(k.Rng.Jitter(0.6f), -0.2f, k.Rng.Jitter(0.6f));
                bool prop = i % 3 == 0;
                float bottom = prop ? -0.2f : top.Y * k.Rng.Range(0.25f, 0.6f);
                k.Limb(top, new Vec3(top.X + k.Rng.Jitter(0.2f), bottom, top.Z + k.Rng.Jitter(0.2f)), prop ? 0.12f : 0.05f, prop ? 0.16f : 0.025f, 0f, st, 0.5f,
                       prop ? 0.4f : 0.6f, k.D.Near ? 2 : 1, 3);
            }
        }

        /// <summary>
        /// Jacaranda: a slender dark trunk forking low into a vase of crooked limbs, an umbrella crown with a flattish
        /// top; from March to May the whole crown is violet with few leaves, and fallen petals carpet the ground.
        /// </summary>
        private static void Jacaranda(TreeKit k, float h, float w, uint leaf, uint bark, float bloom, uint bloomRgb)
        {
            var f = BroadleafForm.Default(h, w, leaf, bark);
            f.TrunkR = 0.026f * h;
            f.ForkFrac = 0.32f;
            f.Lobes = 0;
            f.LeanM = 0.06f * h;
            f.Bark = FloraBuilder.TubeStyle.Bark(bark, 0.07f);
            f.Limbs = 5;
            f.LimbR = 0.6f;
            f.CrownY = 0.72f;
            f.CrownRx = 0.5f;
            f.CrownRy = 0.26f;
            f.Umbrella = true;
            f.Clumps = 10;
            f.ClumpR = 0.28f;
            f.ClumpFlat = 0.66f;
            f.Lumps = 0.18f;
            f.Fringe = 5;
            f.LeafTop = Sunlit(leaf, 0.35f);
            var rng = new FloraRng(k.Seed, 0x4A414341);
            const uint violet = 0x8E6CC8u, lilac = 0xA58AD8u;
            k.Broadleaf(f, (i, n) =>
            {
                if (bloom <= 0f || !rng.Chance(0.96f * bloom + 0.02f)) return 0u;
                return rng.Chance(0.55f) ? violet : lilac;
            });
            if (bloom > 0f)
            {
                // The violet clumps carry violet fringes (flower panicles); fallen petals carpet the ground in full bloom.
                if (k.D.Near && bloom >= 0.5f)
                    k.B.Disc(new Vec3(0f, 0.16f, 0f), 0.44f * w, 0.44f * w, 22, FloraBuilder.Fixed(0x9C7FD0u, 0.95f), MaterialChannel.Foliage, 0.85f, 1f,
                             k.Seed, 0.22f, FloraBuilder.Fixed(0xB4A4CCu));
            }
        }

        /// <summary>
        /// Bottlebrush (Callistemon): a short, often multi-stemmed tree with a dense rounded crown of weeping
        /// branchlets; red cylindrical brushes hang at the branch ends from March to May (and a smaller autumn flush).
        /// </summary>
        private static void Bottlebrush(TreeKit k, float h, float w, uint leaf, uint bark, float bloom, uint bloomRgb)
        {
            var f = BroadleafForm.Default(h, w, leaf, bark);
            f.TrunkR = 0.035f * h;
            f.ForkFrac = 0.3f;
            f.Stems = 2;
            f.Lobes = 0;
            f.Bark = FloraBuilder.TubeStyle.Bark(bark, 0.1f);
            f.Limbs = 5;
            f.CrownY = 0.6f;
            f.CrownRx = 0.5f;
            f.CrownRy = 0.36f;
            f.Clumps = 7;
            f.ClumpR = 0.34f;
            f.ClumpFlat = 0.95f;
            f.Fringe = 5;
            f.FringeDroop = 0.9f;
            f.LeafTop = Sunlit(leaf, 0.25f);
            k.Broadleaf(f);
            if (bloom <= 0f) return;
            // Brushes hang at the branch ends: short red cylinders with a lighter tip.
            int n = (int)(Count(k, 16) * bloom);
            var bs = new FloraBuilder.TubeStyle { Rgb = bloomRgb, PatchRgb = 0xF06070u, PatchAmount = 0.6f, Banded = true, Channel = MaterialChannel.Foliage };
            Vec3[] p = new Vec3[3];
            float[] r = new float[3];
            for (int i = 0; i < n; i++)
            {
                Vec3 o;
                Vec3 at = k.OnClumps(out o, -0.7f);
                Vec3 axis = new Vec3(o.X * 0.6f, -1f, o.Z * 0.6f).Normalized;
                float len = k.Rng.Range(0.3f, 0.42f);
                p[0] = at + o * 0.06f;
                p[1] = p[0] + axis * (0.5f * len);
                p[2] = p[0] + axis * len;
                r[0] = 0.06f;
                r[1] = 0.085f;
                r[2] = 0.055f;
                k.B.Tube(p, r, 3, k.D.Near ? 5 : 3, bs, 0.7f, 0.85f, true, 0, 0f, k.Seed ^ (uint)(i * 31));
            }
        }

        /// <summary>Camphor: short stout trunk, dense round glossy crown; red-tinted new leaves in spring.</summary>
        private static void Camphor(TreeKit k, float h, float w, uint leaf, uint bark, int month)
        {
            var f = BroadleafForm.Default(h, w, leaf, bark);
            f.TrunkR = 0.035f * h;
            f.ForkFrac = 0.32f;
            f.Limbs = 5;
            f.CrownY = 0.62f;
            f.CrownRy = 0.36f;
            f.Clumps = 11;
            f.ClumpR = 0.3f;
            f.ClumpFlat = 0.86f;
            f.LeafTop = Sunlit(leaf, 0.35f);
            if (month == 3 || month == 4) f.FringeRgb = 0xA8603Eu;
            k.Broadleaf(f);
        }

        /// <summary>
        /// Eucalyptus: a tall pale trunk with peeling grey-brown patches, branching high into a few ascending limbs,
        /// and a thin open crown of drooping blue-green clumps with sky showing through.
        /// </summary>
        private static void Eucalyptus(TreeKit k, float h, float w, uint leaf, uint bark)
        {
            var f = BroadleafForm.Default(h, w, leaf, bark);
            f.TrunkR = 0.017f * h;
            f.ForkFrac = 0.48f;
            f.Lobes = 0;
            f.LeanM = 0.04f * h;
            f.TrunkTopR = 0.6f;
            f.Bark = new FloraBuilder.TubeStyle { Rgb = bark, PatchRgb = 0xA89A84u, PatchAmount = 0.75f, Channel = MaterialChannel.Bark, Rough = 0.03f };
            f.Limbs = 6;
            f.LimbR = 0.7f;
            f.CrownY = 0.72f;
            f.CrownRx = 0.5f;
            f.CrownRy = 0.27f;
            f.Clumps = 10;
            f.ClumpR = 0.24f;
            f.ClumpFlat = 1.05f;
            f.Lumps = 0.25f;
            f.Fringe = 6;
            f.FringeDroop = 0.7f;
            f.Shell = 0.88f;
            f.LeafTop = FloraBuilder.Mix(leaf, 0xB8D0B8u, 0.3f);
            k.Broadleaf(f);
        }

        /// <summary>Schima (chilaune): dark fissured trunk, broad dense crown; reddish new leaves in spring and white
        /// fragrant flowers in May-June.</summary>
        private static void Schima(TreeKit k, float h, float w, uint leaf, uint bark, float bloom, uint bloomRgb, int month)
        {
            var f = BroadleafForm.Default(h, w, leaf, bark);
            f.TrunkR = 0.03f * h;
            f.ForkFrac = 0.4f;
            f.Bark = FloraBuilder.TubeStyle.Bark(bark, 0.12f);
            f.Limbs = 5;
            f.CrownY = 0.67f;
            f.CrownRy = 0.31f;
            f.Clumps = 11;
            f.LeafTop = Sunlit(leaf, 0.3f);
            if (month == 3 || month == 4) f.FringeRgb = 0xA8503Au;
            k.Broadleaf(f);
            Dots(k, (int)(Count(k, 34) * bloom), bloomRgb, 0.17f);
        }

        /// <summary>Castanopsis (katus): grey trunk, dense rounded crown; upright cream-yellow catkins in spring.</summary>
        private static void Castanopsis(TreeKit k, float h, float w, uint leaf, uint bark, float bloom, uint bloomRgb)
        {
            var f = BroadleafForm.Default(h, w, leaf, bark);
            f.TrunkR = 0.032f * h;
            f.ForkFrac = 0.36f;
            f.Bark = FloraBuilder.TubeStyle.Bark(bark, 0.08f);
            f.Limbs = 5;
            f.CrownY = 0.64f;
            f.CrownRy = 0.34f;
            f.Clumps = 10;
            f.ClumpR = 0.31f;
            f.ClumpFlat = 0.88f;
            f.LeafTop = Sunlit(leaf, 0.35f);
            k.Broadleaf(f);
            int n = (int)(Count(k, 12) * bloom);
            var bs = FloraBuilder.PuffStyle.Solid(bloomRgb, MaterialChannel.Foliage);
            for (int i = 0; i < n; i++)
            {
                Vec3 o;
                Vec3 p = k.OnClumps(out o, 0.2f);
                Vec3 axis = new Vec3(o.X * 0.4f, 1f, o.Z * 0.4f).Normalized;
                k.B.PuffAlong(p + axis * 0.12f, axis, 0.25f, 0.07f, 0, 0f, k.Seed ^ (uint)(i * 17), bs);
            }
        }

        /// <summary>Flower dots on the outer clumps (Schima's white flowers).</summary>
        private static void Dots(TreeKit k, int n, uint rgb, float size)
        {
            var ps = FloraBuilder.PuffStyle.Solid(rgb, MaterialChannel.Foliage);
            for (int i = 0; i < n; i++)
            {
                Vec3 o;
                Vec3 p = k.OnClumps(out o, 0f);
                float s = size * k.Rng.Range(0.8f, 1.2f);
                k.B.Puff(p + o * (s * 0.5f), new Vec3(s, s * 0.6f, s), 0, 0.1f, k.Seed ^ (uint)(i * 23), ps);
            }
        }

        /// <summary>Oak and laurel of the 1,800-2,400 m band: a crooked trunk, many limbs, dense dark lumpy crown.</summary>
        private static void Oak(TreeKit k, float h, float w, uint leaf, uint bark)
        {
            var f = BroadleafForm.Default(h, w, leaf, bark);
            f.TrunkR = 0.036f * h;
            f.ForkFrac = 0.34f;
            f.LeanM = 0.08f * h;
            f.Bark = FloraBuilder.TubeStyle.Bark(bark, 0.14f);
            f.Limbs = 6;
            f.CrownY = 0.64f;
            f.CrownRy = 0.34f;
            f.Clumps = 11;
            f.ClumpR = 0.29f;
            f.Lumps = 0.2f;
            f.Fringe = 5;
            f.LeafTop = Sunlit(leaf, 0.3f);
            k.Broadleaf(f);
        }

        /// <summary>
        /// Brown oak (Quercus semecarpifolia) near the summits: gnarled mossy trunk, dense olive crown (the leaves are
        /// brown underneath) and lichen strands hanging from the limbs in the fog.
        /// </summary>
        private static void BrownOak(TreeKit k, float h, float w, uint leaf, uint bark)
        {
            var f = BroadleafForm.Default(h, w, leaf, bark);
            f.TrunkR = 0.04f * h;
            f.ForkFrac = 0.32f;
            f.LeanM = 0.1f * h;
            f.Bark = new FloraBuilder.TubeStyle { Rgb = 0x5E5444u, PatchRgb = Moss, PatchAmount = 0.8f, Channel = MaterialChannel.Bark, Rough = 0.15f };
            f.Limbs = 6;
            f.CrownY = 0.64f;
            f.Clumps = 11;
            f.ClumpR = 0.29f;
            f.Lumps = 0.2f;
            f.Fringe = 5;
            f.LeafTop = FloraBuilder.Mix(leaf, 0x9AA060u, 0.3f);
            k.Broadleaf(f);
            if (k.D.Near)
            {
                var st = new FloraBuilder.TubeStyle { Rgb = 0xA8B088u, Channel = MaterialChannel.Foliage };
                for (int i = 0; i < 8 && k.TipCount > 0; i++)
                {
                    Vec3 tip = k.Tips[i % k.TipCount];
                    Vec3 a = Vec3.Lerp(new Vec3(0f, f.ForkFrac * h, 0f), tip, k.Rng.Range(0.4f, 0.8f));
                    k.Limb(a, a - new Vec3(0f, k.Rng.Range(0.6f, 1.4f), 0f), 0.05f, 0.01f, 0f, st, 0.7f, 0.8f, 1, 3);
                }
            }
        }

        /// <summary>
        /// Rhododendron arboreum (laligurans, the national flower): gnarled reddish multi-stemmed trunk, an irregular
        /// rounded crown of leaf whorls at the branch ends, and from February to April crimson trusses all over it.
        /// </summary>
        private static void Rhododendron(TreeKit k, float h, float w, uint leaf, uint bark, float bloom, uint bloomRgb)
        {
            var f = BroadleafForm.Default(h, w, leaf, bark);
            f.TrunkR = 0.04f * h;
            f.ForkFrac = 0.3f;
            f.Stems = 3;
            f.LeanM = 0.08f * h;
            f.Lobes = 0;
            f.Bark = FloraBuilder.TubeStyle.Bark(bark, 0.16f);
            f.Bark.PatchRgb = 0x8A5A44u;
            f.Bark.PatchAmount = 0.5f;
            f.Limbs = 6;
            f.CrownY = 0.64f;
            f.CrownRy = 0.35f;
            f.Clumps = 8;
            f.ClumpR = 0.32f;
            f.ClumpFlat = 0.8f;
            f.Lumps = 0.22f;
            f.Fringe = bloom > 0.5f ? 1 : 5;
            f.LeafTop = Sunlit(leaf, 0.3f);
            k.Broadleaf(f);
            // In March the crown is studded with scarlet trusses (they read from the valley floor).
            int n = (int)(Count(k, 16) * bloom);
            for (int i = 0; i < n; i++)
            {
                Vec3 o;
                Vec3 p = k.OnClumps(out o, -0.3f);
                float s = k.Rng.Range(0.3f, 0.4f);
                var ps = FloraBuilder.PuffStyle.Solid(k.Rng.Chance(0.85f) ? bloomRgb : 0xE0457Au, MaterialChannel.Foliage);
                ps.TopRgb = 0xE83848u;
                ps.Spike = 0.3f;
                k.B.Puff(p + o * (s * 0.45f), new Vec3(s, s * 0.85f, s), 0, 0.22f, k.Seed ^ (uint)(i * 29), ps);
            }
        }

        /// <summary>Sal (Shorea robusta) of the Terai and Chure: a tall straight dark trunk with a clear bole and an
        /// oval crown of big leaves; a light bronze-green flush in spring.</summary>
        private static void Sal(TreeKit k, float h, float w, uint leaf, uint bark, int month)
        {
            var f = BroadleafForm.Default(h, w, leaf, bark);
            f.TrunkR = 0.022f * h;
            f.ForkFrac = 0.5f;
            f.Lobes = 3;
            f.LobeDepth = 0.2f;
            f.Bark = FloraBuilder.TubeStyle.Bark(bark, 0.12f);
            f.Limbs = 5;
            f.CrownY = 0.73f;
            f.CrownRy = 0.25f;
            f.Clumps = 11;
            f.ClumpR = 0.3f;
            f.ClumpFlat = 0.82f;
            f.LeafTop = Sunlit(leaf, 0.35f);
            if (month == 3 || month == 4) f.FringeRgb = 0xB8A050u;
            k.Broadleaf(f);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Whorled and columnar trees

        /// <summary>
        /// A tree with a straight leader to the top and tiers of branches along it, each ending in a clump or a needle
        /// tuft, the reach shrinking toward the top (<paramref name="shapePow"/>): silky oak, utis (alder) and chir pine.
        /// </summary>
        private static void Whorled(TreeKit k, float h, float w, float trunkR, float firstFrac, int tiers, int perTier, float pitchLo, float pitchHi,
                                    float clumpR, float flat, float spike, float droop, uint leaf, uint leafTop, in FloraBuilder.TubeStyle bark, float shapePow,
                                    int res, int fringe, float irregular = 0.3f, float leafLen = 0.42f, int needles = 0, uint fringeRgb = 0)
        {
            k.ClumpCount = 0;
            k.TipCount = 0;
            var lean = new Vec3(k.Rng.Jitter(0.02f * h), 0f, k.Rng.Jitter(0.02f * h));
            k.Trunk(Vec3.Zero, h * 0.94f, trunkR, trunkR * 0.18f, lean, 0, 0f, bark, 0.015f * h);
            var crown = new Vec3(0f, h * (0.5f + 0.5f * firstFrac), 0f);
            k.CrownC = crown;
            k.CrownRad = 0.5f * (0.5f * w + 0.5f * h * (1f - firstFrac));
            var st = FloraBuilder.PuffStyle.Foliage(leaf, crown, Math.Max(0.5f * w, 0.5f * h));
            st.TopRgb = leafTop;
            st.Spike = spike;
            st.Bend = 0.3f;
            if (!k.D.Near)
            {
                // Mid distance: the leader and one tall smooth mass over the whole crown, tapered toward the top as
                // the tiers shrink, with a lobe bulging out of one side (one silhouette, no stacked balls).
                // Crown base: where the lowest tier's clumps hang (their branches rise at the mean pitch).
                float lowReach = Math.Max(0.12f * w, 0.5f * w - clumpR), meanPitch = 0.5f * (pitchLo + pitchHi) * (float)(Math.PI / 180);
                float y0 = h * firstFrac + lowReach * (float)Math.Tan(meanPitch) - clumpR * flat, y1 = h * 0.96f;
                float rr = 0.5f * w * 1.08f, ry = 0.5f * (y1 - y0);
                var ls = st;
                ls.Spike = Math.Min(st.Spike, 0.12f);
                var lobeStyle = ls;
                if (k.FarRgb != 0)
                {
                    // The season's colour (bloom, flush) as the far LODs show it.
                    ls.Rgb = FloraBuilder.Mix(leaf, k.FarRgb, 0.55f);
                    ls.TopRgb = FloraBuilder.Mix(ls.Rgb, 0xFFFFFFu, 0.12f);
                    lobeStyle.Rgb = k.FarRgb;
                    lobeStyle.TopRgb = FloraBuilder.Mix(k.FarRgb, 0xFFFFFFu, 0.12f);
                }
                int first = k.B.M.VertexCount;
                k.Clump(new Vec3(lean.X * 0.5f, y0 + ry, lean.Z * 0.5f), new Vec3(rr, ry, rr), 0.22f, ls, 0, 1);
                double la = k.Rng.Range(0f, 6.283f);
                float lobeY = y0 + ry * 0.75f;
                k.Clump(new Vec3(lean.X * 0.4f + (float)Math.Cos(la) * rr * 0.42f, lobeY, lean.Z * 0.4f + (float)Math.Sin(la) * rr * 0.42f),
                        new Vec3(rr * 0.55f, ry * 0.42f, rr * 0.55f), 0.25f, lobeStyle, 0, 1);
                k.B.Taper(first, lean.X * 0.5f, lean.Z * 0.5f, y0 + 0.25f * ry, y1, 0.75f * (1f - (float)Math.Pow(0.2, shapePow)));
                return;
            }
            int nt = tiers;
            int per = perTier;
            double b = k.Rng.Range(0f, 6.283f);
            for (int t = 0; t < nt; t++)
            {
                float f = nt == 1 ? 0.5f : (float)t / (nt - 1);
                float y = h * (firstFrac + (0.86f - firstFrac) * f) + k.Rng.Jitter(0.02f) * h;
                // The tier's reach to the clump centres: the clumps themselves fill out to the crown envelope.
                float reach = Math.Max(0.12f * w, 0.5f * w * (float)Math.Pow(1f - 0.8f * f, shapePow) - clumpR);
                for (int i = 0; i < per; i++)
                {
                    // Branches spread up and down round their tier (an irregular crown, not stacked plates).
                    float yb = y + (i - 0.5f * (per - 1)) * 0.035f * h * irregular + k.Rng.Jitter(0.015f) * h;
                    Vec3 at = TreeKit.TrunkPoint(Vec3.Zero, h * 0.94f, lean, yb / (h * 0.94f), 0.015f * h, k.Seed);
                    b += 2.39996 + k.Rng.Jitter(0.45f);
                    float pitch = k.Rng.Range(pitchLo, pitchHi) * (float)(Math.PI / 180);
                    float rch = reach * k.Rng.Range(1f - irregular, 1.05f);
                    var dir = new Vec3((float)Math.Cos(b), (float)Math.Tan(pitch), (float)Math.Sin(b));
                    Vec3 tip = at + dir * rch;
                    float r0 = trunkR * 0.35f * (1f - 0.6f * f);
                    k.Limb(at, tip - new Vec3(0f, droop * rch, 0f), r0, r0 * 0.35f, 0.06f * rch, bark, 0.7f, 0.6f, k.D.Near ? 2 : 1, k.D.Near ? 4 : 3);
                    if (k.TipCount < k.Tips.Length) k.Tips[k.TipCount++] = tip;
                    float cr = clumpR * (0.8f + 0.4f * (1f - f)) * k.Rng.Range(0.85f, 1.15f) * (k.D.Near ? 1f : 1.25f);
                    Vec3 c = tip - new Vec3(0f, droop * rch * 0.6f, 0f);
                    k.Clump(c, new Vec3(cr * 1.2f, cr * flat, cr), 0.18f, st, fringe, res, 0.2f, leafLen, 0.66f, fringeRgb);
                    if (needles > 0) Needles(k, c, cr, needles, leaf, leafTop);
                }
            }
            // The leader's top tuft.
            float topR = clumpR * 0.85f;
            k.Clump(new Vec3(lean.X, h * 0.98f - topR * 1.3f, lean.Z), new Vec3(topR * 0.8f, topR * 1.3f, topR * 0.8f), 0.18f, st, fringe, res, 0f, leafLen);
        }

        /// <summary>A tuft of long drooping needles round a pine branch end: thin cards radiating out and hanging.</summary>
        private static void Needles(TreeKit k, Vec3 c, float r, int n, uint leaf, uint top)
        {
            for (int i = 0; i < n; i++)
            {
                double a = i * 2 * Math.PI / n + k.Rng.Jitter(0.3f);
                var o = new Vec3((float)Math.Cos(a), k.Rng.Range(-0.2f, 0.5f), (float)Math.Sin(a)).Normalized;
                var dir = (o + new Vec3(0f, -0.55f, 0f)).Normalized;
                uint col = FloraBuilder.Leaf(i % 2 == 0 ? leaf : top, k.Rng.Range(0.9f, 1.05f));
                k.B.LeafCard(c + o * (0.55f * r), dir, o, r * k.Rng.Range(0.9f, 1.2f), r * 0.32f, col, 0.85f);
            }
        }

        /// <summary>
        /// Silky oak (Grevillea robusta): tall straight grey-brown furrowed trunk and a narrow, irregular, layered
        /// crown of fern-like fronds, airy with gaps; golden-orange comb flowers on top of the fronds in April-May.
        /// </summary>
        private static void SilkyOak(TreeKit k, float h, float w, uint leaf, uint bark, float bloom, uint bloomRgb)
        {
            var st = FloraBuilder.TubeStyle.Bark(bark, 0.1f);
            // In April-May the golden-orange combs cover the outside of the crown: bloom-tinted clumps and comb cards.
            uint crown = FloraBuilder.Mix(leaf, bloomRgb, 0.3f * bloom);
            Whorled(k, h, w, 0.017f * h, 0.3f, 5, 2, 25f, 50f, 0.09f * h, 0.8f, 0f, 0.06f, crown, FloraBuilder.Mix(crown, 0xA8C088u, 0.35f), st, 0.75f,
                    k.D.PuffRes, bloom > 0f ? 5 : 3, 0.5f, 0.6f, 0, bloom > 0f ? bloomRgb : 0u);
            int n = (int)(Count(k, 10) * bloom);
            var bs = FloraBuilder.PuffStyle.Solid(bloomRgb, MaterialChannel.Foliage);
            bs.TopRgb = 0xF8C050u;
            for (int i = 0; i < n; i++)
            {
                Vec3 o;
                Vec3 p = k.OnClumps(out o, 0.1f);
                Vec3 axis = new Vec3(o.X, 0.6f, o.Z).Normalized;
                k.B.PuffAlong(p + axis * 0.12f, axis, 0.3f, 0.13f, 0, 0.1f, k.Seed ^ (uint)(i * 13), bs);
            }
        }

        /// <summary>Utis (Alnus nepalensis) by streams: a straight pale trunk and a narrow oval crown, light green.</summary>
        private static void Alnus(TreeKit k, float h, float w, uint leaf, uint bark)
        {
            var st = FloraBuilder.TubeStyle.Bark(bark, 0.05f);
            Whorled(k, h, w, 0.02f * h, 0.36f, 5, 2, 30f, 55f, 0.1f * h, 0.85f, 0f, 0f, leaf, Sunlit(leaf, 0.35f), st, 0.5f, k.D.PuffRes, 4);
        }

        /// <summary>
        /// Chir pine (Pinus roxburghii) of the Nagarjun, Chandragiri and Kirtipur plantations and the dry south faces:
        /// a straight trunk with thick reddish-brown plated bark, bare below, whorls of near-horizontal branches ending
        /// in bushy tufts of long drooping needles, and an open crown that rounds off with age.
        /// </summary>
        private static void ChirPine(TreeKit k, float h, float w, uint leaf, uint bark)
        {
            var st = FloraBuilder.TubeStyle.Bark(bark, 0.16f);
            st.PatchRgb = 0x5A3422u;
            st.PatchAmount = 0.5f;
            Whorled(k, h, w, 0.021f * h, 0.5f, 5, 3, -5f, 25f, 0.065f * h, 0.62f, 0.2f, 0.08f, leaf, FloraBuilder.Mix(leaf, 0x98B878u, 0.3f), st, 0.7f, 0, 0,
                    0.35f, 0.42f, k.D.Near ? 6 : 0);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Grasses and giant herbs

        /// <summary>
        /// Bamboo clump (bans) beside village houses and field edges: a dozen culms rising from a shared clump,
        /// leaning out and arching over at the top, with darker nodes, and a feathery plume of leaf sprays along the
        /// upper culms.
        /// </summary>
        private static void Bamboo(TreeKit k, float h, float w, uint leaf, uint culm)
        {
            k.ClumpCount = 0;
            k.TipCount = 0;
            int culms = k.D.Near ? 11 : 5;
            int segs = k.D.Near ? 5 : 2, sides = k.D.Near ? 4 : 3;
            var cs = new FloraBuilder.TubeStyle { Rgb = culm, PatchRgb = FloraBuilder.Mix(culm, 0x3E5A22u, 0.5f), PatchAmount = 0.6f, Banded = true, Channel = MaterialChannel.Bark };
            k.CrownC = new Vec3(0f, 0.68f * h, 0f);
            k.CrownRad = 0.4f * w;
            var st = FloraBuilder.PuffStyle.Foliage(leaf, k.CrownC, 0.5f * w);
            st.TopRgb = Sunlit(leaf, 0.35f);
            st.Bend = 0.35f;
            if (!k.D.Near)
            {
                // Mid distance: five culms under two smooth plume masses.
                var cs1 = new FloraBuilder.TubeStyle { Rgb = culm, Channel = MaterialChannel.Bark };
                for (int i = 0; i < 5; i++)
                {
                    double a = i * 2.39996;
                    var foot = new Vec3((float)Math.Cos(a) * 0.4f, -0.1f, (float)Math.Sin(a) * 0.4f);
                    var tipP = new Vec3((float)Math.Cos(a) * 0.3f * w, 0.75f * h, (float)Math.Sin(a) * 0.3f * w);
                    k.Limb(foot, tipP, 0.08f, 0.04f, 0.1f * h, cs1, 0.5f, 0.85f, 2, 3);
                }
                k.Clump(new Vec3(0f, 0.68f * h, 0f), new Vec3(0.44f * w, 0.2f * h, 0.44f * w), 0.3f, st, 0, 1);
                k.Clump(new Vec3(0f, 0.85f * h, 0f), new Vec3(0.26f * w, 0.14f * h, 0.26f * w), 0.3f, st, 0, 1);
                return;
            }
            // A dense inner plume (drooping clumps) the culms rise through.
            var coreSt = st;
            coreSt.ShadeLow = 0.6f;
            coreSt.ShadeHigh = 0.85f;
            int core = k.D.Near ? 4 : 3;
            for (int i = 0; i < core; i++)
            {
                double a = i * 2 * Math.PI / core + 0.4;
                float rad = i == 0 ? 0f : 0.18f * w;
                var c = new Vec3((float)Math.Cos(a) * rad, h * (i == 0 ? 0.62f : 0.55f), (float)Math.Sin(a) * rad);
                float cr = 0.13f * w * (k.D.Near ? 1f : 1.6f);
                k.Clump(c, new Vec3(cr, 0.09f * h, cr), 0.25f, coreSt, 0, -1);
            }
            uint leafC = FloraBuilder.Leaf(leaf, 1f), leafTop = FloraBuilder.Leaf(Sunlit(leaf, 0.4f), 1f);
            Vec3[] p = new Vec3[segs + 1];
            float[] r = new float[segs + 1];
            for (int i = 0; i < culms; i++)
            {
                double a = i * 2.39996 + k.Rng.Jitter(0.4f);
                float rb = (float)Math.Sqrt((i + 0.5f) / culms) * 0.65f;
                var foot = new Vec3((float)Math.Cos(a) * rb, -0.1f, (float)Math.Sin(a) * rb);
                var outDir = new Vec3((float)Math.Cos(a), 0f, (float)Math.Sin(a));
                float hc = h * k.Rng.Range(0.82f, 1f);
                float reach = 0.48f * w * k.Rng.Range(0.65f, 1f);
                // Each culm rises nearly straight, then arches out and over; its tip hangs.
                Vec3 p1 = foot + Vec3.Up * (1.4f * hc) + outDir * (0.15f * reach);
                Vec3 end = foot + Vec3.Up * (0.7f * hc) + outDir * reach;
                for (int s = 0; s <= segs; s++)
                {
                    float t = (float)s / segs;
                    p[s] = Bez(foot, p1, end, t);
                    r[s] = 0.085f * (1f - 0.65f * t);
                }
                k.B.Tube(p, r, segs + 1, sides, cs, 0.5f, 0.9f, false, 0, 0f, k.Seed ^ (uint)i);
                // Sprays: fans of narrow leaves hanging from the nodes of the upper, arching half.
                int sprays = k.D.Near ? 4 : 1;
                for (int q = 0; q < sprays; q++)
                {
                    float t = sprays == 1 ? 0.75f : 0.4f + 0.18f * q;
                    Vec3 c = Bez(foot, p1, end, t);
                    int cards = k.D.Near ? 4 : 3;
                    for (int j = 0; j < cards; j++)
                    {
                        double b = a + (j - (cards - 1) * 0.5) * 0.55 + k.Rng.Jitter(0.2f);
                        var dir = new Vec3((float)Math.Cos(b) * 0.8f, k.Rng.Range(-0.9f, -0.2f), (float)Math.Sin(b) * 0.8f).Normalized;
                        Vec3 o = (outDir + Vec3.Up * 0.4f).Normalized;
                        float len = k.Rng.Range(0.9f, 1.3f);
                        k.B.LeafCard(c, dir, o, len, 0.24f, j % 2 == 0 ? leafC : leafTop, 0.85f);
                    }
                    if (k.ClumpCount < k.ClumpC.Length)
                    {
                        k.ClumpC[k.ClumpCount] = c;
                        k.ClumpR[k.ClumpCount++] = new Vec3(0.5f, 0.5f, 0.5f);
                    }
                }
            }
        }

        private static Vec3 Bez(Vec3 a, Vec3 b, Vec3 c, float t)
        {
            return Vec3.Lerp(Vec3.Lerp(a, b, t), Vec3.Lerp(b, c, t), t);
        }

        /// <summary>
        /// Palm (date and fan palms of Rana gardens): a ringed trunk with a slight curve, a crown of arching pinnate
        /// fronds (folded, with a zig-zag leaflet edge and drooping tips), dead brown fronds hanging under it.
        /// </summary>
        private static void Palm(TreeKit k, float h, float w, uint leaf, uint bark)
        {
            var st = new FloraBuilder.TubeStyle { Rgb = bark, PatchRgb = 0x6E604Eu, PatchAmount = 0.7f, Banded = true, Channel = MaterialChannel.Bark, Rough = 0.04f };
            var lean = new Vec3(0.06f * h, 0f, 0f);
            float trunkH = h * 0.78f;
            k.Trunk(Vec3.Zero, trunkH, 0.032f * h, 0.024f * h, lean, 0, 0f, st, 0.01f * h);
            Vec3 top = TreeKit.TrunkPoint(Vec3.Zero, trunkH, lean, 1f, 0.01f * h, k.Seed);
            k.CrownC = top;
            k.CrownRad = 0.5f * w;
            k.B.Puff(top, new Vec3(0.05f * h, 0.065f * h, 0.05f * h), k.D.PuffRes, 0.2f, k.Seed, FloraBuilder.PuffStyle.Solid(0x7A6A48u, MaterialChannel.Bark));
            int fronds = k.D.Near ? 12 : 6;
            uint c = FloraBuilder.Leaf(leaf, 1f), rib = FloraBuilder.Leaf(FloraBuilder.Mix(leaf, 0xD8E0A0u, 0.4f), 1f);
            for (int i = 0; i < fronds; i++)
            {
                double a = i * 2.39996;
                float pitch = (float)(k.Rng.Range(10f, 55f) * Math.PI / 180);
                var dir = new Vec3((float)(Math.Cos(a) * Math.Cos(pitch)), (float)Math.Sin(pitch), (float)(Math.Sin(a) * Math.Cos(pitch)));
                var side = new Vec3(-(float)Math.Sin(a), 0f, (float)Math.Cos(a));
                float len = 0.52f * w * k.Rng.Range(0.85f, 1.05f) / (float)Math.Max(0.6, Math.Cos(pitch));
                k.B.Blade(top + Vec3.Up * 0.2f, dir, side, len, 0.12f, 0.9f, 0f, len * 0.32f, 0.35f, k.D.Near ? 8 : 3, c, MaterialChannel.Foliage, 0.6f, 1f, rib,
                          k.D.Near ? 0.55f : 0f);
            }
            k.B.Blade(top + Vec3.Up * 0.2f, Vec3.Up, new Vec3(1f, 0f, 0f), 0.14f * h, 0.08f, 0.22f, 0f, 0f, 0.3f, 2, c, MaterialChannel.Foliage, 0.8f, 1f);
            // Dead fronds hanging against the trunk (the skirt the mid LOD keeps too).
            for (int i = 0; i < (k.D.Near ? 3 : 2); i++)
            {
                double a = i * 2.1 + 0.7;
                var dir = new Vec3((float)Math.Cos(a) * 0.35f, -1f, (float)Math.Sin(a) * 0.35f).Normalized;
                k.B.Blade(top, dir, new Vec3(-(float)Math.Sin(a), 0f, (float)Math.Cos(a)), 0.24f * h, 0.08f, 0.45f, 0f, 0f, 0.3f, k.D.Near ? 4 : 2,
                          FloraBuilder.Fixed(0x9A7A4Au), MaterialChannel.Foliage, 0.5f, 0.6f, 0, k.D.Near ? 0.5f : 0f);
            }
        }

        /// <summary>
        /// Banana (kera) in house gardens: a green pseudostem streaked with brown sheaths, a crown of huge paddle
        /// leaves arching out with pale midribs and drooping tips (dry leaves hanging), a hanging purple bud below a
        /// bunch of green bananas, and suckers at the foot.
        /// </summary>
        private static void Banana(TreeKit k, float h, float w, uint leaf, uint stem)
        {
            var st = new FloraBuilder.TubeStyle { Rgb = stem, PatchRgb = 0x8A6A3Au, PatchAmount = 0.55f, Channel = MaterialChannel.Foliage };
            float stemH = h * 0.56f;
            var lean = new Vec3(0.03f * h, 0f, 0f);
            k.Trunk(Vec3.Zero, stemH, 0.034f * h, 0.026f * h, lean, 0, 0f, st, 0.005f * h);
            Vec3 top = TreeKit.TrunkPoint(Vec3.Zero, stemH, lean, 1f, 0.005f * h, k.Seed);
            k.CrownC = top;
            k.CrownRad = 0.5f * w;
            int leaves = k.D.Near ? 8 : 5;
            uint c = FloraBuilder.Leaf(leaf, 1f), rib = FloraBuilder.Leaf(0xD0E098u, 1f);
            for (int i = 0; i < leaves; i++)
            {
                double a = i * 2.39996 + 0.3;
                float pitch = (float)(k.Rng.Range(35f, 70f) * Math.PI / 180);
                var dir = new Vec3((float)(Math.Cos(a) * Math.Cos(pitch)), (float)Math.Sin(pitch), (float)(Math.Sin(a) * Math.Cos(pitch)));
                var side = new Vec3(-(float)Math.Sin(a), 0f, (float)Math.Cos(a));
                float len = 0.46f * w * k.Rng.Range(0.85f, 1.05f);
                float wide = 0.15f * w;
                // The petiole is the narrow first part of the blade.
                k.B.Blade(top - Vec3.Up * 0.1f, dir, side, len, 0.06f, wide, wide * 0.45f, len * 0.3f, 0.15f, k.D.Near ? 5 : 3, c, MaterialChannel.Foliage, 0.6f, 1f, rib);
            }
            if (!k.D.Near) return;
            for (int i = 0; i < 2; i++)
            {
                double a = i * 3.1 + 1.2;
                var dir = new Vec3((float)Math.Cos(a) * 0.25f, -1f, (float)Math.Sin(a) * 0.25f).Normalized;
                k.B.Blade(top - Vec3.Up * 0.2f, dir, new Vec3(-(float)Math.Sin(a), 0f, (float)Math.Cos(a)), 0.3f * h, 0.06f, 0.2f, 0.06f, 0f, 0.15f, 3,
                          FloraBuilder.Fixed(0xA08050u), MaterialChannel.Foliage, 0.5f, 0.55f);
            }
            // Bunch of green bananas on a curved stalk, with the purple bud hanging below it.
            var stalkTop = top + new Vec3(0.35f, -0.05f, 0.1f);
            var bunch = stalkTop + new Vec3(0.3f, -0.35f, 0.05f);
            k.Limb(top, stalkTop, 0.04f, 0.035f, 0.05f, st, 0.7f, 0.7f, 2, 4);
            k.Limb(stalkTop, bunch + new Vec3(0f, -0.45f, 0f), 0.03f, 0.025f, 0f, st, 0.7f, 0.7f, 1, 4);
            var bs = FloraBuilder.PuffStyle.Solid(0x8DB040u, MaterialChannel.Foliage);
            bs.Spike = 0.2f;
            k.B.Puff(bunch, new Vec3(0.17f, 0.26f, 0.17f), 1, 0.15f, k.Seed ^ 0xBA4u, bs);
            k.B.PuffAlong(bunch + new Vec3(0f, -0.55f, 0f), -Vec3.Up, 0.17f, 0.085f, 0, 0f, k.Seed, FloraBuilder.PuffStyle.Solid(0x6A2A4Au, MaterialChannel.Foliage));
            for (int i = 0; i < 2; i++)
            {
                double a = i * 2.6 + 0.9;
                var foot = new Vec3((float)Math.Cos(a) * 0.45f, 0f, (float)Math.Sin(a) * 0.45f);
                k.Limb(foot - Vec3.Up * 0.1f, foot + Vec3.Up * 0.8f, 0.07f, 0.05f, 0f, st, 0.5f, 0.8f, 1, 4);
                for (int q = 0; q < 2; q++)
                {
                    double b = a + q * 2.5;
                    var dir = new Vec3((float)Math.Cos(b) * 0.6f, 0.8f, (float)Math.Sin(b) * 0.6f).Normalized;
                    k.B.Blade(foot + Vec3.Up * 0.75f, dir, new Vec3(-(float)Math.Sin(b), 0f, (float)Math.Cos(b)), 0.8f, 0.03f, 0.26f, 0.08f, 0.2f, 0.15f, 2, c,
                              MaterialChannel.Foliage, 0.6f, 0.95f, rib);
                }
            }
        }
    }
}
