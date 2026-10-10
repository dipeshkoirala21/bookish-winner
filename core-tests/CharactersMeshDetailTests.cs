using System;
using System.Collections.Generic;
using Ghumante.Core.Characters;
using Ghumante.Core.Meshing;
using Ghumante.Core.Traffic;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The detail fixes measured on the meshes (docs/research/w2/ref_characters.md §2, §7, §11): the topi's silhouette,
    /// weave and fit over the hair, the porter's namlo over the cap and hair, the chappal's strap on the foot, the bare
    /// foot's toe line, the sneaker's rounded heel and the daypack's straps over a child's shirt.
    /// </summary>
    public class CharactersMeshDetailTests
    {
        private static MeshData Build(CharacterRecipe r, int lod, out SkinWeights w)
        {
            var m = new MeshData(16384, 49152);
            w = new SkinWeights(16384);
            HumanoidMesher.Build(r, lod, m, w, CharacterMeshOptions.For(HeadwearMode.Outfit));
            return m;
        }

        private static MaterialChannel Channel(MeshData m, int v)
        {
            return (MaterialChannel)(int)Math.Round(m.Uv0[v * 2]);
        }

        private static uint Rgb(MeshData m, int v)
        {
            return (uint)(m.Colors[v * 4] << 16 | m.Colors[v * 4 + 1] << 8 | m.Colors[v * 4 + 2]);
        }

        private static V3 P(MeshData m, int v)
        {
            return new V3(m.Positions[v * 3], m.Positions[v * 3 + 1], m.Positions[v * 3 + 2]);
        }

        /// <summary>Topi vertices: fabric bound to head_attach above the brows.</summary>
        private static List<int> Topi(MeshData m, SkinWeights w, float aboveY)
        {
            var list = new List<int>();
            for (int v = 0; v < m.VertexCount; v++)
                if (w.Bone0[v] == (byte)Bone.HeadAttach && Channel(m, v) == MaterialChannel.Fabric && m.Positions[v * 3 + 1] > aboveY) list.Add(v);
            return list;
        }

        [Test]
        public void TheTopiStandsTallWithUprightWallsAndACreasedRidgeLikeThePhotos()
        {
            var r = new CharacterRecipe { Hair = 1, Seed = 7u };
            r[OutfitSlotKind.Head] = new OutfitSlot(OutfitItem.DhakaTopi, 0, 0);
            MeshData m = Build(r, 0, out SkinWeights w);
            HumanoidMesher.HeadShape(BodyMetrics.For(r), out V3 hc, out V3 hr);
            List<int> topi = Topi(m, w, hc.Y);
            Assert.That(topi.Count, Is.GreaterThan(600));
            float rimY = float.MaxValue, topY = float.MinValue, maxX = 0f;
            foreach (int v in topi)
            {
                V3 p = P(m, v);
                rimY = Math.Min(rimY, p.Y);
                topY = Math.Max(topY, p.Y);
                maxX = Math.Max(maxX, Math.Abs(p.X - hc.X));
            }
            float rimWidth = 2f * maxX, height = topY - rimY;
            TestContext.WriteLine("topi rim width " + rimWidth + ", height " + height);
            // Tall, as worn in the photos (the old cap was a squat bowl about half as high as wide).
            Assert.That(height / rimWidth, Is.InRange(0.7f, 1.05f), "height to rim width");
            // Worn high: the rim is no wider than the head.
            Assert.That(maxX, Is.LessThanOrEqualTo(hr.X + 0.004f), "the rim sits inside the head's silhouette");
            // Upright walls: halfway up the sides the cap is still nearly as wide as at the rim.
            float midY = rimY + 0.4f * height, midX = 0f;
            foreach (int v in topi)
            {
                V3 p = P(m, v);
                if (Math.Abs(p.Y - midY) < 0.012f) midX = Math.Max(midX, Math.Abs(p.X - hc.X));
            }
            Assert.That(midX, Is.GreaterThan(0.85f * maxX), "near-vertical walls, not a bowl");
            // A creased ridge: near the top the two sides meet in a narrow line (no dome).
            float nearTop = topY - 0.08f * height, ridgeX = 0f;
            foreach (int v in topi)
            {
                V3 p = P(m, v);
                if (p.Y > nearTop) ridgeX = Math.Max(ridgeX, Math.Abs(p.X - hc.X));
            }
            Assert.That(ridgeX, Is.LessThan(0.35f * maxX), "the walls meet in a ridge");
        }

        [Test]
        public void TheDefaultWeaveIsDense()
        {
            for (int weave = 0; weave < CharacterPalette.DhakaWeaves; weave++)
            {
                int motif = 0, all = 0;
                for (int col = 0; col < HumanoidMesher.TopiColumns; col++)
                    for (int row = 0; row < 12; row++)
                    {
                        all++;
                        if (HumanoidMesher.DhakaCell(weave, col, row) > 0) motif++;
                        // Seamless round the cap.
                        Assert.That(HumanoidMesher.DhakaCell(weave, col, row), Is.EqualTo(HumanoidMesher.DhakaCell(weave, col + HumanoidMesher.TopiColumns, row)));
                    }
                TestContext.WriteLine("weave " + weave + ": " + (100 * motif / all) + "% motif");
                Assert.That(motif, Is.GreaterThan(all * 55 / 100), "weave " + weave + " reads as its colours, not its ground");
            }
            Assert.That(CharacterPalette.Luma(CharacterPalette.TopiBase[0]), Is.LessThan(CharacterPalette.Luma(0xF1E6D6u)), "the default ground is pink, not cream");
        }

        [Test]
        public void NoHairShowsThroughTheTopi()
        {
            foreach (byte hair in new byte[] { 0, 1, 3, 4, 5, 7, 9, 11 })
            {
                var r = new CharacterRecipe { Hair = hair, Seed = 3u + hair };
                r[OutfitSlotKind.Head] = new OutfitSlot(OutfitItem.DhakaTopi, 1, 2);
                MeshData m = Build(r, 0, out SkinWeights w);
                HumanoidMesher.HeadShape(BodyMetrics.For(r), out V3 hc, out V3 hr);
                List<int> topi = Topi(m, w, hc.Y);
                float rimTop = float.MinValue;
                foreach (int v in topi)
                    if (m.Positions[v * 3 + 1] < hc.Y + HumanoidMesher.TopiRimY + 0.03f) rimTop = Math.Max(rimTop, m.Positions[v * 3 + 1]);
                for (int v = 0; v < m.VertexCount; v++)
                {
                    if (Channel(m, v) != MaterialChannel.Hair) continue;
                    V3 p = P(m, v);
                    if (p.Y < rimTop + 0.004f) continue;
                    float a = MathF.Atan2(p.X - hc.X, p.Z - hc.Z), rad = Radius(p, hc);
                    float cap = 0f;
                    foreach (int t in topi)
                    {
                        V3 q = P(m, t);
                        if (Math.Abs(q.Y - p.Y) > 0.012f || AngleGap(MathF.Atan2(q.X - hc.X, q.Z - hc.Z), a) > 0.2f) continue;
                        cap = Math.Max(cap, Radius(q, hc));
                    }
                    Assert.That(rad, Is.LessThanOrEqualTo(cap + 0.001f), "hair style " + hair + ": a hair vertex at height " + (p.Y - hc.Y) + " pokes through the cap");
                }
            }
        }

        private static float Radius(V3 p, V3 c)
        {
            float dx = p.X - c.X, dz = p.Z - c.Z;
            return MathF.Sqrt(dx * dx + dz * dz);
        }

        private static float AngleGap(float a, float b)
        {
            float d = Math.Abs(a - b);
            return d > MathF.PI ? 2f * MathF.PI - d : d;
        }

        [Test]
        public void TheNamloLiesOverTheTopiAndTheHair()
        {
            int checkedBands = 0;
            foreach (bool topiOn in new[] { true, false })
                for (uint seed = 1; seed < 160 && checkedBands < (topiOn ? 3 : 6); seed++)
                {
                    CharacterRecipe r = CharacterRecipe.ForPedestrian(PedArchetype.Porter, (int)seed, seed, StreetStyle.OldBazaar, 1);
                    bool hasTopi = r[OutfitSlotKind.Head].Item == OutfitItem.DhakaTopi || r[OutfitSlotKind.Head].Item == OutfitItem.BhadgaunleTopi;
                    if (hasTopi != topiOn || !CharacterRecipe.UsesNamlo(r[OutfitSlotKind.Back].Item)) continue;
                    MeshData m = Build(r, 0, out SkinWeights w);
                    HumanoidMesher.HeadShape(BodyMetrics.For(r), out V3 hc, out V3 hr);
                    var band = new List<int>();
                    var under = new List<int>();
                    for (int v = 0; v < m.VertexCount; v++)
                    {
                        V3 p = P(m, v);
                        if (p.Y < hc.Y - 0.05f) continue;
                        uint c = Rgb(m, v);
                        bool strap = w.Bone0[v] == (byte)Bone.Head && Channel(m, v) == MaterialChannel.Fabric && (c == 0xA88B5Eu || c == CharacterPalette.Shade(0xA88B5Eu, 0.82f));
                        if (strap) band.Add(v);
                        else if (Channel(m, v) == MaterialChannel.Hair || w.Bone0[v] == (byte)Bone.HeadAttach) under.Add(v);
                    }
                    Assert.That(band.Count, Is.GreaterThan(60), "seed " + seed + ": the band is drawn");
                    foreach (int v in band)
                    {
                        V3 p = P(m, v);
                        if (p.Y < hc.Y || Math.Abs(MathF.Atan2(p.X - hc.X, p.Z - hc.Z)) > 1.6f) continue; // the band, not the ropes behind
                        float a = MathF.Atan2(p.X - hc.X, p.Z - hc.Z), rad = Radius(p, hc), below = 0f;
                        foreach (int u in under)
                        {
                            V3 q = P(m, u);
                            if (Math.Abs(q.Y - p.Y) > 0.006f || AngleGap(MathF.Atan2(q.X - hc.X, q.Z - hc.Z), a) > 0.12f) continue;
                            below = Math.Max(below, Radius(q, hc));
                        }
                        Assert.That(rad, Is.GreaterThanOrEqualTo(below - 0.0015f), "seed " + seed + (topiOn ? " (topi)" : " (hair)") + ": the band cuts into what it lies on");
                    }
                    // One continuous band across the forehead: band vertices on both sides and in front.
                    bool left = false, right = false, front = false;
                    foreach (int v in band)
                    {
                        V3 p = P(m, v);
                        float a = MathF.Atan2(p.X - hc.X, p.Z - hc.Z);
                        if (Math.Abs(a) < 0.15f) front = true;
                        if (a > 1.2f && a < 1.8f) right = true;
                        if (a < -1.2f && a > -1.8f) left = true;
                    }
                    Assert.That(front && left && right, Is.True, "seed " + seed + ": the band crosses the forehead from temple to temple");
                    checkedBands++;
                }
            Assert.That(checkedBands, Is.GreaterThanOrEqualTo(4));
        }

        [Test]
        public void TheChappalStrapLiesOnTheFootAndTheToesStepBack()
        {
            var r = new CharacterRecipe { Seed = 5u };
            r[OutfitSlotKind.Feet] = new OutfitSlot(OutfitItem.Chappal, 0);
            r[OutfitSlotKind.Legs] = new OutfitSlot(OutfitItem.Shorts, 1);
            MeshData m = Build(r, 0, out SkinWeights w);
            uint strap = CharacterPalette.Colour(OutfitItem.Chappal, 0);
            var skin = new List<int>();
            var straps = new List<int>();
            for (int v = 0; v < m.VertexCount; v++)
            {
                V3 p = P(m, v);
                if (p.Y > 0.12f || p.X > 0f) continue; // the left foot
                bool footBone = w.Bone0[v] == (byte)Bone.FootL || w.Bone0[v] == (byte)Bone.ToeL;
                if (Channel(m, v) == MaterialChannel.Skin && footBone) skin.Add(v);
                if (Channel(m, v) == MaterialChannel.Rubber && Rgb(m, v) == strap && p.Y > 0.022f) straps.Add(v);
            }
            Assert.That(straps.Count, Is.GreaterThan(40), "the strap is drawn above the sole");
            int onTop = 0;
            foreach (int v in straps)
            {
                V3 p = P(m, v);
                if (w.Bone0[v] == (byte)Bone.ToeL && p.Y < 0.04f) continue; // the post between the toes
                // Over the top of the foot (the middle 60% of its width at this station): above the skin there.
                float lo = float.MaxValue, hi = float.MinValue, top = float.MinValue;
                foreach (int s in skin)
                {
                    V3 q = P(m, s);
                    if (Math.Abs(q.Z - p.Z) > 0.004f || q.Y > 0.1f) continue;
                    lo = Math.Min(lo, q.X);
                    hi = Math.Max(hi, q.X);
                }
                if (lo == float.MaxValue || p.X < lo + 0.2f * (hi - lo) || p.X > hi - 0.2f * (hi - lo)) continue;
                foreach (int s in skin)
                {
                    V3 q = P(m, s);
                    float dx = q.X - p.X, dz = q.Z - p.Z;
                    if (dx * dx + dz * dz < 0.004f * 0.004f) top = Math.Max(top, q.Y);
                }
                if (top == float.MinValue) continue;
                onTop++;
                Assert.That(p.Y, Is.GreaterThanOrEqualTo(top - 0.0015f), "a strap vertex is buried in the foot");
            }
            Assert.That(onTop, Is.GreaterThan(straps.Count / 6), "the strap runs over the top of the foot");
            // The toe line: the big toe (inner side, +x on the left foot) reaches further than the little toe.
            float minX = float.MaxValue, maxX = float.MinValue;
            foreach (int s in skin)
            {
                minX = Math.Min(minX, m.Positions[s * 3]);
                maxX = Math.Max(maxX, m.Positions[s * 3]);
            }
            float span = maxX - minX, innerTip = float.MinValue, outerTip = float.MinValue;
            foreach (int s in skin)
            {
                V3 q = P(m, s);
                if (q.Y > 0.05f) continue;
                if (q.X > maxX - 0.3f * span) innerTip = Math.Max(innerTip, q.Z);
                if (q.X < minX + 0.3f * span) outerTip = Math.Max(outerTip, q.Z);
            }
            Assert.That(innerTip, Is.GreaterThan(outerTip + 0.012f), "an oblique toe line, big toe first");
        }

        [Test]
        public void ShoeHeelsAreRoundedCountersFacingOutward()
        {
            // The old sneaker ended in a knife edge whose flat end cap faced inward (a dark slit down the heel). Every
            // triangle at the back end of a shoe or sandal now faces backward or sideways, and the heel is already wide
            // a few millimetres in.
            foreach (OutfitItem f in new[] { OutfitItem.Sneakers, OutfitItem.LeatherShoes, OutfitItem.TrekBoots, OutfitItem.Chappal })
            {
                var r = new CharacterRecipe { Seed = 2u };
                r[OutfitSlotKind.Feet] = new OutfitSlot(f, 0);
                r[OutfitSlotKind.Legs] = new OutfitSlot(OutfitItem.Shorts, 1);
                foreach (int lod in new[] { 0, 1 })
                {
                    MeshData m = Build(r, lod, out SkinWeights w);
                    float back = float.MaxValue;
                    var shoe = new List<int>();
                    for (int v = 0; v < m.VertexCount; v++)
                    {
                        V3 p = P(m, v);
                        MaterialChannel ch = Channel(m, v);
                        if (p.X > 0f || p.Y > 0.06f || (ch != MaterialChannel.Fabric && ch != MaterialChannel.Rubber && ch != MaterialChannel.Leather)) continue;
                        if (w.Bone0[v] != (byte)Bone.FootL && w.Bone0[v] != (byte)Bone.ToeL) continue;
                        shoe.Add(v);
                        back = Math.Min(back, p.Z);
                    }
                    var isShoe = new HashSet<int>(shoe);
                    int checkedTris = 0;
                    for (int t = 0; t < m.IndexCount; t += 3)
                    {
                        int a = m.Indices[t], b = m.Indices[t + 1], c = m.Indices[t + 2];
                        if (!isShoe.Contains(a) || !isShoe.Contains(b) || !isShoe.Contains(c)) continue;
                        V3 pa = P(m, a), pb = P(m, b), pc = P(m, c);
                        if ((pa.Z + pb.Z + pc.Z) / 3f > back + 0.003f) continue;
                        V3 fn = V3.Cross(pb - pa, pc - pa);
                        if (fn.LengthSq < 1e-14f) continue;
                        checkedTris++;
                        Assert.That(fn.Normalized.Z, Is.LessThan(0.3f), f + " lod " + lod + ": a heel triangle faces into the shoe");
                    }
                    Assert.That(checkedTris, Is.GreaterThan(4), f + " lod " + lod);
                    float minX = float.MaxValue, maxX = float.MinValue;
                    foreach (int v in shoe)
                    {
                        V3 p = P(m, v);
                        if (p.Z > back + 0.006f || p.Y > 0.05f) continue;
                        minX = Math.Min(minX, p.X);
                        maxX = Math.Max(maxX, p.X);
                    }
                    Assert.That(maxX - minX, Is.GreaterThan(0.012f), f + " lod " + lod + ": the heel is round, not a sliver");
                }
            }
        }

        [Test]
        public void DaypackStrapsStayOutsideTheShirtOnChildrenAndAdults()
        {
            foreach (AgeGroup age in new[] { AgeGroup.Child, AgeGroup.Teen, AgeGroup.Adult })
                foreach (byte figure in new byte[] { 0, 1 })
                {
                    CharacterRecipe r = CharacterRecipe.ForPedestrian(PedArchetype.SchoolKid, 3, 11u, StreetStyle.Urban, 0);
                    r.Age = age;
                    r.Figure = figure;
                    r.Beard = FacialHair.None;
                    r[OutfitSlotKind.Back] = new OutfitSlot(OutfitItem.Daypack, 2);
                    r.Validate();
                    foreach (int lod in new[] { 0, 1 })
                    {
                        MeshData m = Build(r, lod, out SkinWeights w);
                        uint strap = CharacterPalette.Shade(CharacterPalette.Colour(OutfitItem.Daypack, 2), 0.78f);
                        var straps = new List<int>();
                        var cloth = new List<int>();
                        float shoulder = BodyMetrics.For(r).ShoulderY;
                        for (int v = 0; v < m.VertexCount; v++)
                        {
                            V3 p = P(m, v);
                            if (p.Z < 0.02f || p.Y > shoulder + 0.01f || p.Y < shoulder - 0.3f) continue; // the front of the chest
                            bool chest = w.Bone0[v] == (byte)Bone.Chest || w.Bone0[v] == (byte)Bone.Spine || w.Bone0[v] == (byte)Bone.Neck;
                            uint c = Rgb(m, v);
                            if (c == strap || c == CharacterPalette.Shade(strap, 0.9f)) straps.Add(v);
                            else if (chest && Channel(m, v) == MaterialChannel.Fabric) cloth.Add(v);
                        }
                        Assert.That(straps.Count, Is.GreaterThan(20), age + " lod " + lod + ": the straps cross the chest");
                        foreach (int v in straps)
                        {
                            V3 p = P(m, v);
                            float a = MathF.Atan2(p.X, p.Z), rad = MathF.Sqrt(p.X * p.X + p.Z * p.Z), shirt = 0f;
                            foreach (int u in cloth)
                            {
                                V3 q = P(m, u);
                                if (Math.Abs(q.Y - p.Y) > 0.008f || AngleGap(MathF.Atan2(q.X, q.Z), a) > 0.05f) continue;
                                shirt = Math.Max(shirt, MathF.Sqrt(q.X * q.X + q.Z * q.Z));
                            }
                            Assert.That(rad, Is.GreaterThanOrEqualTo(shirt - 0.001f), age + " figure " + figure + " lod " + lod + ": a strap sinks into the shirt at height " + p.Y);
                        }
                    }
                }
        }
    }
}
