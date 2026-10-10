using System;
using Ghumante.Core.Characters;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Traffic;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The crowd built from the character generator (W2_DESIGN 5.4, 10.4): mesh levels per band and tier against the
    /// character slices, body variants and their keys, the far crowd's baked frames, the crowd animation and poser, and
    /// the district styles that pick the clothes.
    /// </summary>
    public class CharactersMeshCrowdTests
    {
        [Test]
        public void TheCrowdPlanFitsEveryTierSliceAtTheCaps()
        {
            for (int tier = 0; tier < 3; tier++)
            {
                int worst = CrowdLodPlan.WorstCaseAtCaps(tier);
                TestContext.WriteLine("tier " + tier + ": " + worst + " of " + CrowdLodPlan.SliceTris[tier]);
                Assert.That(worst, Is.LessThanOrEqualTo(CrowdLodPlan.SliceTris[tier]), "tier " + tier);
            }
            // The player is always LOD0 (the light LOD0 on Low); the far band is always the baked far level.
            for (int tier = 0; tier < 3; tier++)
            {
                Assert.That(CrowdLodPlan.PlayerLod(tier), Is.EqualTo(0));
                Assert.That(CrowdLodPlan.PlayerLight(tier), Is.EqualTo(tier == 0));
                Assert.That(CrowdLodPlan.MeshLod(tier, 2, 0), Is.EqualTo(HumanoidMesher.FarLod));
                Assert.That(CrowdLodPlan.Skinned(2), Is.False);
                // The person brushing past is never drawn below LOD1, on any tier.
                for (int rank = 0; rank < CrowdLodPlan.Caps[tier][0]; rank++) Assert.That(CrowdLodPlan.MeshLod(tier, 0, rank), Is.LessThanOrEqualTo(1));
            }
            Assert.That(CrowdLodPlan.PlayerBudget(0), Is.EqualTo(HumanoidMesher.Lod0LightBudget));
            Assert.That(CrowdLodPlan.PlayerBudget(2), Is.EqualTo(HumanoidMesher.Budget(0)));
            Assert.That(CrowdLodPlan.MeshLod(0, 0, 0), Is.EqualTo(1), "Low: the nearest person is LOD1");
            for (int rank = 0; rank < 30; rank++) Assert.That(CrowdLodPlan.MeshLod(0, 1, rank), Is.EqualTo(2));
            Assert.That(CrowdLodPlan.MeshLod(2, 0, 0), Is.EqualTo(0), "High: the nearest person is LOD0");
            Assert.That(CrowdLodPlan.MeshLod(2, 0, 1), Is.EqualTo(1));
            Assert.That(CrowdLodPlan.MeshLod(1, 0, 0), Is.EqualTo(1));
            Assert.That(CrowdLodPlan.MeshLod(1, 1, 0), Is.EqualTo(1), "Mid: the nearest of the mid band is LOD1");
            Assert.That(HumanoidMesher.Budget(2), Is.GreaterThan(HumanoidMesher.Budget(HumanoidMesher.FarLod)), "the mid-distance body is richer than the far one");
        }

        [Test]
        public void EverydayCrowdBodiesKeepFullDetailAndFitTheSlices()
        {
            int[] worst = new int[HumanoidMesher.FarLod + 1];
            int bodies = 0, dropped = 0, droppedMore = 0;
            var m = new MeshData();
            var w = new SkinWeights();
            foreach (PedArchetype a in Enum.GetValues(typeof(PedArchetype)))
            {
                foreach (StreetStyle style in Enum.GetValues(typeof(StreetStyle)))
                {
                    for (int carry = 0; carry <= 5; carry++)
                    {
                        // Everyday bodies (nothing carried, or the archetype's usual load) are sampled for the detail drop;
                        // every body of every carry prop must hold the LOD2 and far caps.
                        bool everyday = carry == 0 || carry == (a == PedArchetype.Porter ? 1 : 5);
                        for (int variant = 0; variant < CrowdVariants.Variants; variant++)
                        {
                            CharacterRecipe r = CrowdVariants.Recipe(CrowdVariants.Key(a, style, carry, variant));
                            for (int lod = 1; lod <= HumanoidMesher.FarLod; lod++)
                            {
                                bool sample = everyday && variant % 3 == 0;
                                if (lod == 1 && !sample) continue;
                                m.Clear();
                                w.Clear();
                                HumanoidMesher.Build(r, lod, m, w, CrowdVariants.Options(r));
                                worst[lod] = Math.Max(worst[lod], m.TriangleCount);
                                Assert.That(m.TriangleCount, Is.LessThanOrEqualTo(HumanoidMesher.Budget(lod)), a + " " + style + " carry " + carry + " v" + variant + " lod " + lod);
                                if (!sample) continue;
                                bodies++;
                                int drop = HumanoidMesher.DetailDrop(r, lod, HeadwearMode.Outfit);
                                if (drop > 0) dropped++;
                                if (drop > 1) droppedMore++;
                            }
                        }
                    }
                }
            }
            // A sample of LOD0 bodies (the nearest person on High).
            foreach (PedArchetype a in Enum.GetValues(typeof(PedArchetype)))
            {
                CharacterRecipe r = CrowdVariants.Recipe(CrowdVariants.Key(a, StreetStyle.Urban, 0, 1));
                var m0 = new MeshData();
                HumanoidMesher.BuildStatic(r, 0, m0);
                worst[0] = Math.Max(worst[0], m0.TriangleCount);
                Assert.That(HumanoidMesher.DetailDrop(r, 0, HeadwearMode.Outfit), Is.EqualTo(0), a + " at LOD0");
            }
            TestContext.WriteLine("crowd worst triangles: LOD0 " + worst[0] + ", LOD1 " + worst[1] + ", LOD2 " + worst[2] + ", far " + worst[3] + "; " + dropped +
                                  " of " + bodies + " bodies needed a detail drop, " + droppedMore + " beyond plainer accents");
            // Step 1 only makes the accents (earrings, glasses, a mala) one level plainer; the umbrella's canopy pushes many
            // mid-distance bodies there, which nobody sees at 15–40 m. Hair, back items and headwear stay for nearly all.
            Assert.That(dropped, Is.LessThanOrEqualTo(bodies / 12), "most everyday bodies keep their full detail");
            Assert.That(droppedMore, Is.LessThanOrEqualTo(bodies / 20), "nearly every everyday body keeps its hair, load and headwear");
            for (int tier = 0; tier < 3; tier++)
                Assert.That(CrowdLodPlan.WorstCaseTriangles(tier, worst[0], worst[1], worst[2], worst[3], CrowdLodPlan.PlayerBudget(tier)),
                            Is.LessThanOrEqualTo(CrowdLodPlan.SliceTris[tier]));
            // The default player recipe never drops detail, and its light LOD0 (Low) keeps the face, hands and topi.
            var player = new CharacterRecipe();
            for (int lod = 0; lod <= 2; lod++)
            {
                Assert.That(HumanoidMesher.DetailDrop(player, lod, HeadwearMode.Outfit), Is.EqualTo(0), "player lod " + lod);
                Assert.That(HumanoidMesher.DetailDrop(player, lod, HeadwearMode.Helmet), Is.EqualTo(0), "player helmet lod " + lod);
            }
            var light = CharacterMeshOptions.For(HeadwearMode.Outfit);
            light.Light = true;
            var lm = new MeshData();
            HumanoidMesher.Build(player, 0, lm, null, light);
            int[] lightParts = HumanoidMesher.Breakdown(player, 0, light);
            int[] fullParts = HumanoidMesher.Breakdown(player, 0, CharacterMeshOptions.For(HeadwearMode.Outfit));
            TestContext.WriteLine("player light LOD0 " + lm.TriangleCount + " triangles");
            Assert.That(lm.TriangleCount, Is.LessThanOrEqualTo(HumanoidMesher.Lod0LightBudget));
            Assert.That(HumanoidMesher.DetailDrop(player, 0, light), Is.EqualTo(0));
            int face = Array.IndexOf(HumanoidMesher.PartNames, "face"), headwear = Array.IndexOf(HumanoidMesher.PartNames, "headwear");
            Assert.That(lightParts[face], Is.EqualTo(fullParts[face]), "the light LOD0 keeps the full face");
            Assert.That(lightParts[headwear], Is.EqualTo(fullParts[headwear]), "and the full topi");
        }

        [Test]
        public void LookKeysRoundTripAndEveryBandDrawsTheSameShape()
        {
            foreach (PedArchetype a in Enum.GetValues(typeof(PedArchetype)))
                foreach (StreetStyle style in Enum.GetValues(typeof(StreetStyle)))
                    for (int carry = 0; carry <= 5; carry++)
                        for (int variant = 0; variant < CrowdVariants.Variants; variant++)
                            for (int colour = 0; colour < CrowdVariants.Colours; colour += 5)
                            {
                                int key = CrowdVariants.Key(a, style, carry, variant, colour);
                                CrowdVariants.Split(key, out PedArchetype a2, out StreetStyle s2, out int c2, out int v2);
                                Assert.That(a2, Is.EqualTo(a));
                                Assert.That(s2, Is.EqualTo(style));
                                Assert.That(c2, Is.EqualTo(carry));
                                Assert.That(v2, Is.EqualTo(variant));
                                Assert.That(CrowdVariants.ColourOf(key), Is.EqualTo(colour));
                                Assert.That(CrowdVariants.ShapeKey(key), Is.EqualTo(CrowdVariants.Key(a, style, carry, variant)), "the colour never changes the shape");
                            }
            for (int id = 0; id < 500; id++)
            {
                int v = CrowdVariants.VariantOf(id);
                Assert.That(v, Is.InRange(0, CrowdVariants.Variants - 1));
                Assert.That(CrowdVariants.VariantOf(id), Is.EqualTo(v), "deterministic");
                int key = CrowdVariants.KeyOf(PedArchetype.UrbanCasual, StreetStyle.Urban, 9, id, id % 16);
                Assert.That(CrowdVariants.ColourOf(key), Is.EqualTo(id % 16), "the sim's tint is the garment colour");
                CrowdVariants.Split(key, out _, out _, out int carry, out _);
                Assert.That(carry, Is.EqualTo(0), "carry props past the umbrella count as none");
            }
            // A shape is the same recipe every time; different shapes differ.
            int k0 = CrowdVariants.Key(PedArchetype.KurtaSari, StreetStyle.NewarTown, 0, 0);
            int k1 = CrowdVariants.Key(PedArchetype.KurtaSari, StreetStyle.NewarTown, 0, 1);
            Assert.That(CrowdVariants.Recipe(k0).Equals(CrowdVariants.Recipe(k0)), Is.True);
            Assert.That(CrowdVariants.Recipe(k0).Equals(CrowdVariants.Recipe(k1)), Is.False);
            // The umbrella carry puts an umbrella in the hand; the far frame keeps the hold.
            CharacterRecipe wet = CrowdVariants.Recipe(CrowdVariants.Key(PedArchetype.UrbanCasual, StreetStyle.Urban, 5, 2));
            Assert.That(CrowdVariants.HoldOf(wet), Is.EqualTo(CrowdHold.Umbrella));
            Assert.That(CrowdVariants.FramePose(FarFrame.Walk2, CrowdHold.Umbrella).Hold, Is.EqualTo(CrowdHold.Umbrella));
        }

        [Test]
        public void TheFarBodyIsThePersonsOwnBodyInTheirOwnColour()
        {
            // The near and mid bands recolour the shape's masked build on the CPU; the far band bakes the same masked build
            // and tints it per instance in the shader. Emulating that shader on a baked frame gives exactly the colours of
            // the recoloured body, vertex for vertex, for every shape and colour: nobody changes clothes, age, skin, hair or
            // garment colour when they cross 40 m.
            int checkedLooks = 0, tinted = 0;
            var scratch = new byte[64 * 1024];
            foreach (PedArchetype a in Enum.GetValues(typeof(PedArchetype)))
                foreach (StreetStyle style in new[] { StreetStyle.Urban, StreetStyle.NewarTown, StreetStyle.Buddhist, StreetStyle.Office })
                    for (int variant = 0; variant < CrowdVariants.Variants; variant += 5)
                    {
                        int shape = CrowdVariants.Key(a, style, 0, variant);
                        CharacterRecipe r = CrowdVariants.Recipe(shape);
                        var m = new MeshData();
                        var w = new SkinWeights();
                        HumanoidMesher.Build(r, HumanoidMesher.FarLod, m, w, CrowdVariants.Options(r));
                        var frame = new MeshData();
                        new CrowdBaker().Bake(m, w, new HumanoidSkeleton(r), CrowdVariants.FramePose(FarFrame.Walk2, CrowdVariants.HoldOf(r)), frame);
                        if (HumanoidMesher.TintKeyOf(r) != 0) tinted++;
                        for (int colour = 0; colour < CrowdVariants.Colours; colour += 3)
                        {
                            int key = CrowdVariants.Key(a, style, 0, variant, colour);
                            Assert.That(CrowdVariants.Recipe(key).Equals(r), Is.True, "every look of a shape is the shape's recipe");
                            uint rgb = CrowdVariants.GarmentColour(r, colour);
                            CrowdVariants.Recolour(m, rgb, scratch);
                            float tr = rgb == 0 ? 1f : Lin((rgb >> 16) & 0xFF), tg = rgb == 0 ? 1f : Lin((rgb >> 8) & 0xFF), tb = rgb == 0 ? 1f : Lin(rgb & 0xFF);
                            for (int v = 0; v < frame.VertexCount; v++)
                            {
                                int i = v * 4;
                                byte fr = frame.Colors[i], fg = frame.Colors[i + 1], fb = frame.Colors[i + 2];
                                if (frame.Colors[i + 3] == 255 && rgb != 0)
                                {
                                    // ToonLit _INSTANCE_TINT: albedo * linear(tint), weighted by the vertex alpha.
                                    fr = CrowdVariants.TintChannel(fr, tr);
                                    fg = CrowdVariants.TintChannel(fg, tg);
                                    fb = CrowdVariants.TintChannel(fb, tb);
                                }
                                Assert.That(fr == scratch[i] && fg == scratch[i + 1] && fb == scratch[i + 2], Is.True, a + " " + style + " v" + variant + " c" + colour + " vertex " + v);
                            }
                            checkedLooks++;
                        }
                    }
            TestContext.WriteLine(checkedLooks + " looks checked, " + tinted + " tintable shapes");
            Assert.That(tinted, Is.GreaterThan(checkedLooks / CrowdVariants.Colours * 3 / 2 / 3), "most shapes vary their garment colour");
        }

        [Test]
        public void ALookRecoloursOnlyTheGarment()
        {
            // Against the plain (unmasked) build of the same shape: colour 0 reproduces the shape's own colours, any other
            // colour changes the garment's fabric only (never skin, eyes, teeth, hair, shoes or accessories).
            int changed = 0;
            foreach (PedArchetype a in new[] { PedArchetype.UrbanCasual, PedArchetype.KurtaSari, PedArchetype.DauraSuruwal, PedArchetype.SchoolKid, PedArchetype.Tourist })
                for (int variant = 0; variant < CrowdVariants.Variants; variant += 2)
                {
                    CharacterRecipe r = CrowdVariants.Recipe(CrowdVariants.Key(a, StreetStyle.Urban, 0, variant));
                    if (HumanoidMesher.TintKeyOf(r) == 0) continue;
                    foreach (int lod in new[] { 0, 1, 2 })
                    {
                        var plain = new MeshData();
                        HumanoidMesher.Build(r, lod, plain, null, CharacterMeshOptions.For(HeadwearMode.Outfit));
                        var masked = new MeshData();
                        HumanoidMesher.Build(r, lod, masked, null, CrowdVariants.Options(r));
                        Assert.That(masked.VertexCount, Is.EqualTo(plain.VertexCount));
                        var own = new byte[masked.VertexCount * 4];
                        var other = new byte[masked.VertexCount * 4];
                        CrowdVariants.Recolour(masked, CrowdVariants.GarmentColour(r, 0), own);
                        uint rgb = CrowdVariants.GarmentColour(r, 7);
                        CrowdVariants.Recolour(masked, rgb, other);
                        int garment = 0;
                        for (int v = 0; v < masked.VertexCount; v++)
                        {
                            int i = v * 4;
                            // Shades lighter than the garment clamp to its colour in the mask (a few pale highlights).
                            for (int ch = 0; ch < 3; ch++)
                                Assert.That(Math.Abs(own[i + ch] - plain.Colors[i + ch]), Is.LessThanOrEqualTo(24), a + " lod " + lod + " vertex " + v + ": colour 0 is the shape's own");
                            bool isGarment = masked.Colors[i + 3] == 255;
                            if (isGarment)
                            {
                                garment++;
                                Assert.That((MaterialChannel)(int)Math.Round(masked.Uv0[v * 2]), Is.EqualTo(MaterialChannel.Fabric), "only cloth is recoloured");
                            }
                            else
                                for (int ch = 0; ch < 3; ch++)
                                    Assert.That(other[i + ch], Is.EqualTo(plain.Colors[i + ch]), a + " lod " + lod + " vertex " + v + " is not garment and keeps its colour");
                        }
                        Assert.That(garment, Is.GreaterThan(masked.VertexCount / 20), a + " lod " + lod + ": the garment is a real share of the body");
                        changed++;
                    }
                }
            Assert.That(changed, Is.GreaterThan(20));
            // Untintable shapes: uniforms, monks' robes, the black haku patasi.
            Assert.That(HumanoidMesher.TintKeyOf(CharacterRecipe.ForPedestrian(PedArchetype.TrafficPolice, 1, 1u)), Is.EqualTo(0u));
            Assert.That(HumanoidMesher.TintKeyOf(CharacterRecipe.ForPedestrian(PedArchetype.Monk, 1, 1u, StreetStyle.Buddhist, 0)), Is.EqualTo(0u));
            Assert.That(HumanoidMesher.TintKeyOf(CharacterRecipe.ForPedestrian(PedArchetype.Hakupatasi, 1, 1u, StreetStyle.NewarTown, 0)), Is.EqualTo(0u));
            Assert.That(CrowdVariants.GarmentColour(CharacterRecipe.ForPedestrian(PedArchetype.TrafficPolice, 1, 1u), 5), Is.EqualTo(0u));
        }

        private static float Lin(uint c)
        {
            float x = c / 255f;
            return x <= 0.04045f ? x / 12.92f : MathF.Pow((x + 0.055f) / 1.055f, 2.4f);
        }

        [Test]
        public void AnUrbanCrowdShowsManyDifferentPeople()
        {
            // The sim's urban archetype mix (PedestrianSim, S §3.2) and carry odds: about 60% urban casuals, 90% carrying
            // nothing, tints 0..15. Shapes come from the agent's id and colours from its tint, so no look may be more than a
            // small share of the street, a High near-and-mid band of 26 people shows almost no exact twins, and the far
            // band (78 people) still shares batches per shape and frame.
            float[] mix = { 52, 15, 3, 0, 10, 1, 3, 2, 0.5f, 0, 0 };
            float total = 0f;
            foreach (float m in mix) total += m;
            var looks = new System.Collections.Generic.Dictionary<int, int>();
            var shapes = new System.Collections.Generic.Dictionary<int, int>();
            const int n = 4000;
            uint h = 12345u;
            int[] keys = new int[n];
            var phases = new double[n];
            for (int id = 0; id < n; id++)
            {
                h = CharMath.Hash(h, (uint)id);
                float u = CharMath.Unit(h) * total;
                int a = 0;
                for (; a < mix.Length - 1; a++)
                {
                    u -= mix[a];
                    if (u < 0f) break;
                }
                int tint = (int)(CharMath.Hash(h, 7u) % 16u);
                float c = CharMath.Unit(CharMath.Hash(h, 9u));
                int carry = a == (int)PedArchetype.Porter ? 1 : c < 0.10f ? 1 + (int)(CharMath.Hash(h, 11u) % 4u) : 0;
                int key = CrowdVariants.KeyOf((PedArchetype)a, StreetStyle.Urban, carry, id, tint);
                // Looks that show the same garment colour are the same look (the renderer folds them together, and an
                // untintable shape has one look whatever the tint).
                CharacterRecipe shapeRecipe = CrowdVariants.Recipe(key);
                key = CrowdVariants.ShapeKey(key) | CrowdVariants.CanonicalColour(shapeRecipe, CrowdVariants.ColourOf(key)) << 24;
                keys[id] = key;
                phases[id] = CharMath.Unit(CharMath.Hash(h, 13u)) * 2.0 * Math.PI;
                looks.TryGetValue(key, out int k);
                looks[key] = k + 1;
                int sk = CrowdVariants.ShapeKey(key);
                shapes.TryGetValue(sk, out int ks);
                shapes[sk] = ks + 1;
            }
            int mostLook = 0, mostShape = 0;
            foreach (var kv in looks) mostLook = Math.Max(mostLook, kv.Value);
            foreach (var kv in shapes) mostShape = Math.Max(mostShape, kv.Value);
            TestContext.WriteLine("looks " + looks.Count + " (most common " + mostLook + "), shapes " + shapes.Count + " (most common " + mostShape + ") of " + n);
            Assert.That(mostLook, Is.LessThan(n * 2 / 100), "no look is more than 2% of the street");
            Assert.That(mostShape, Is.LessThan(n * 8 / 100), "no body shape is more than 8% of the street");
            Assert.That(looks.Count, Is.GreaterThan(400));
            // Exact twins (same shape and colour) within groups of 26 (the High near and mid bands).
            int twins = 0, groups = 0;
            for (int g = 0; g + 26 <= n; g += 26, groups++)
            {
                var seen = new System.Collections.Generic.HashSet<int>();
                for (int i = g; i < g + 26; i++)
                    if (!seen.Add(keys[i])) twins++;
            }
            float perGroup = twins / (float)groups;
            TestContext.WriteLine("exact twins per 26 people: " + perGroup);
            Assert.That(perGroup, Is.LessThan(1f));
            // Far batches for 78 walkers (High): distinct (shape, frame) pairs; colours ride on the instance tint.
            int batches = 0, sets = 0;
            for (int g = 0; g + 78 <= n; g += 78, sets++)
            {
                var seen = new System.Collections.Generic.HashSet<long>();
                for (int i = g; i < g + 78; i++)
                    seen.Add((long)CrowdVariants.ShapeKey(keys[i]) << 8 | (long)CrowdVariants.FrameOf(PedClip.Walk, phases[i]));
                batches += seen.Count;
            }
            float perSet = batches / (float)sets;
            TestContext.WriteLine("far batches per 78 walkers: " + perSet);
            Assert.That(perSet, Is.LessThan(70f), "the far crowd shares batches per shape and frame (colours ride on the tint)");
        }

        [Test]
        public void PrayerWheelWalkersRaiseTheWheelWhileWalking()
        {
            // Kora walkers at Boudha carry a hand prayer wheel: the body's hold goes into the pose (CrowdPresenter asks
            // PeopleRenderer.HoldOf before posing), so the right arm stays raised in front of the chest through the walk.
            CharacterRecipe wheel = null;
            for (int v = 0; v < CrowdVariants.Variants && wheel == null; v++)
            {
                CharacterRecipe r = CrowdVariants.Recipe(CrowdVariants.Key(PedArchetype.KurtaSari, StreetStyle.Buddhist, 0, v));
                if (CrowdVariants.HoldOf(r) == CrowdHold.PrayerWheel) wheel = r;
            }
            Assert.That(wheel, Is.Not.Null, "some Boudha variants carry a prayer wheel");
            CrowdHold hold = CrowdVariants.HoldOf(wheel);
            var sk = new HumanoidSkeleton(wheel);
            var local = new Quat[HumanoidSkeleton.BoneCount];
            var pos = new V3[HumanoidSkeleton.BoneCount];
            var rot = new Quat[HumanoidSkeleton.BoneCount];
            for (int i = 0; i < 12; i++)
            {
                float t = i * 0.09f;
                CrowdPose held = CrowdAnimation.Pose(PedClip.Walk, t, t, 1.4f, 3, hold);
                CrowdPose plain = CrowdAnimation.Pose(PedClip.Walk, t, t, 1.4f, 3, CrowdHold.None);
                Assert.That(held.ArmR, Is.InRange(20f, 45f), "the upper arm forward, frame " + i);
                Assert.That(held.ElbowR, Is.InRange(60f, 100f), "the elbow bent, frame " + i);
                Assert.That(Math.Abs(plain.ElbowR - held.ElbowR), Is.GreaterThan(20f), "a plain walk swings the arm instead");
                CrowdPoser.Solve(held, sk, local, out V3 hips);
                sk.Solve(local, hips, 1f, pos, rot);
                Assert.That(pos[(int)Bone.HandR].Y, Is.GreaterThan(pos[(int)Bone.Hips].Y + 0.12f), "the wheel is held above the hip, frame " + i);
            }
        }

        [Test]
        public void FarFramesFollowTheClip()
        {
            Assert.That(CrowdVariants.FrameOf(PedClip.Walk, 0.0), Is.EqualTo(FarFrame.Walk0));
            Assert.That(CrowdVariants.FrameOf(PedClip.Walk, 2.0 * Math.PI - 1e-6), Is.EqualTo(FarFrame.Walk5));
            Assert.That(CrowdVariants.FrameOf(PedClip.Run, 2.0 * Math.PI * 3.5), Is.EqualTo(FarFrame.Walk3));
            Assert.That(CrowdVariants.FrameOf(PedClip.Carry, -0.1), Is.EqualTo(FarFrame.Walk5), "negative phases wrap");
            Assert.That(CrowdVariants.FrameOf(PedClip.Sit, 1.0), Is.EqualTo(FarFrame.Sit));
            Assert.That(CrowdVariants.FrameOf(PedClip.Pray, 1.0), Is.EqualTo(FarFrame.Pray));
            Assert.That(CrowdVariants.FrameOf(PedClip.Namaste, 1.0), Is.EqualTo(FarFrame.Pray));
            Assert.That(CrowdVariants.FrameOf(PedClip.Vendor, 1.0), Is.EqualTo(FarFrame.ArmUp));
            Assert.That(CrowdVariants.FrameOf(PedClip.Idle, 1.0), Is.EqualTo(FarFrame.Stand));
            Assert.That(CrowdVariants.FrameOf(PedClip.Chat, 1.0), Is.EqualTo(FarFrame.Stand));
            // Consecutive walk frames swing the legs through a full cycle.
            float min = 0f, max = 0f;
            for (int f = 0; f < 6; f++)
            {
                CrowdPose p = CrowdVariants.FramePose((FarFrame)f, CrowdHold.None);
                min = Math.Min(min, p.ThighL);
                max = Math.Max(max, p.ThighL);
            }
            Assert.That(max - min, Is.GreaterThan(30f), "the far walk swings the legs");
        }

        [Test]
        public void TheBakerPosesTheFarBodyWithoutChangingItsTopology()
        {
            CharacterRecipe r = CrowdVariants.Recipe(CrowdVariants.Key(PedArchetype.Porter, StreetStyle.Village, 1, 0));
            var m = new MeshData();
            var w = new SkinWeights();
            var o = CharacterMeshOptions.For(HeadwearMode.Outfit);
            o.TintMask = true;
            HumanoidMesher.Build(r, 2, m, w, o);
            var sk = new HumanoidSkeleton(r);
            var baker = new CrowdBaker();
            var stand = new MeshData();
            baker.Bake(m, w, sk, CrowdVariants.FramePose(FarFrame.Stand, CrowdHold.None), stand);
            Assert.That(stand.VertexCount, Is.EqualTo(m.VertexCount));
            Assert.That(stand.IndexCount, Is.EqualTo(m.IndexCount));
            Assert.That(stand.HasUv0, Is.True);
            stand.GetBounds(out _, out float minY, out _, out _, out float maxY, out _);
            Assert.That(minY, Is.EqualTo(0f).Within(0.05f), "standing on the ground");
            var sit = new MeshData();
            baker.Bake(m, w, sk, CrowdVariants.FramePose(FarFrame.Sit, CrowdHold.None), sit);
            sit.GetBounds(out _, out _, out _, out _, out float sitTop, out _);
            Assert.That(sitTop, Is.LessThan(maxY - 0.15f), "sitting lowers the body");
            // Frames append one after another (the baked frame batches share one buffer).
            var both = new MeshData();
            baker.Bake(m, w, sk, CrowdVariants.FramePose(FarFrame.Walk1, CrowdHold.None), both);
            baker.Bake(m, w, sk, CrowdVariants.FramePose(FarFrame.Walk4, CrowdHold.None), both);
            Assert.That(both.VertexCount, Is.EqualTo(2 * m.VertexCount));
            Assert.That(both.Indices[m.IndexCount], Is.EqualTo(m.Indices[0] + m.VertexCount));
            Assert.Throws<ArgumentException>(() => baker.Bake(m, new SkinWeights(), sk, default, new MeshData()));
        }

        [Test]
        public void TheWalkStrikesTwiceACycleAndHoldsKeepTheHandLevel()
        {
            int strikes = 0;
            float dt = 1f / 30f, prev = 0f;
            for (int i = 1; i <= 300; i++)
            {
                float t = i * dt;
                CrowdPose p = CrowdAnimation.Pose(PedClip.Walk, t, prev, 1.4f, 3);
                if (p.Strike != 0) strikes++;
                prev = t;
            }
            float expected = CrowdAnimation.StepRate(1.4f) * 10f;
            Assert.That(strikes, Is.EqualTo(expected).Within(2f), "one strike per step");
            Assert.That(CrowdAnimation.StepRate(3.5f), Is.GreaterThan(CrowdAnimation.StepRate(1.4f)));
            // Holding an umbrella keeps the right hand level in the world through the walk.
            var sk = new HumanoidSkeleton(new CharacterRecipe());
            var local = new Quat[HumanoidSkeleton.BoneCount];
            var pos = new V3[HumanoidSkeleton.BoneCount];
            var rot = new Quat[HumanoidSkeleton.BoneCount];
            for (int i = 0; i < 12; i++)
            {
                CrowdPose p = CrowdAnimation.Pose(PedClip.Walk, i * 0.09f, i * 0.09f, 1.4f, 5, CrowdHold.Umbrella);
                Assert.That(p.Hold, Is.EqualTo(CrowdHold.Umbrella));
                CrowdPoser.Solve(p, sk, local, out V3 hips);
                sk.Solve(local, hips, 1f, pos, rot);
                V3 up = rot[(int)Bone.HandR] * V3.Up;
                Assert.That(up.Y, Is.GreaterThan(0.98f), "frame " + i);
            }
            // Sitting drops the hips onto the seat; the officer raises an arm.
            Assert.That(CrowdAnimation.Pose(PedClip.Sit, 1f, 1f, 0f, 1).DropM, Is.GreaterThan(0.2f));
            CrowdPose stop = CrowdAnimation.Officer(0, 0.5f);
            Assert.That(Math.Max(Math.Max(stop.ArmL, stop.ArmR), Math.Max(stop.ArmOutL, stop.ArmOutR)), Is.GreaterThan(60f));
            Assert.Throws<ArgumentException>(() => CrowdPoser.Solve(default, sk, new Quat[3], out _));
        }

        [Test]
        public void DistrictsPickTheirStreetStyle()
        {
            Assert.That(StyleAt(85.3110, 27.7150, AreaType.Urban), Is.EqualTo(StreetStyle.Tourist), "Thamel");
            Assert.That(StyleAt(85.3620, 27.7215, AreaType.Urban), Is.EqualTo(StreetStyle.Buddhist), "Boudhanath");
            Assert.That(StyleAt(85.2903, 27.7149, AreaType.Urban), Is.EqualTo(StreetStyle.Buddhist), "Swayambhu");
            Assert.That(StyleAt(85.4290, 27.6720, AreaType.OldCore), Is.EqualTo(StreetStyle.NewarTown), "Bhaktapur Durbar Square");
            Assert.That(StyleAt(85.3250, 27.6735, AreaType.OldCore), Is.EqualTo(StreetStyle.NewarTown), "Patan");
            Assert.That(StyleAt(85.3095, 27.7065, AreaType.OldCore), Is.EqualTo(StreetStyle.OldBazaar), "Asan");
            Assert.That(StyleAt(85.3355, 27.6890, AreaType.Urban), Is.EqualTo(StreetStyle.Office), "New Baneshwor");
            Assert.That(StyleAt(85.3488, 27.7105, AreaType.Urban), Is.EqualTo(StreetStyle.Pashupati), "Pashupatinath");
            Assert.That(StyleAt(85.3510, 27.7090, AreaType.Urban), Is.EqualTo(StreetStyle.Pashupati), "the Arya Ghat side");
            // Outside the districts the area type decides.
            Assert.That(StyleAt(85.20, 27.80, AreaType.Rural), Is.EqualTo(StreetStyle.Village));
            Assert.That(StyleAt(85.20, 27.80, AreaType.PeriUrban), Is.EqualTo(StreetStyle.Suburb));
            Assert.That(StyleAt(85.20, 27.80, AreaType.OldCore), Is.EqualTo(StreetStyle.OldBazaar));
            Assert.That(StyleAt(85.20, 27.80, AreaType.Urban), Is.EqualTo(StreetStyle.Urban));
            Assert.That(StreetStyles.CircleCount, Is.GreaterThanOrEqualTo(15));
            for (int i = 0; i < StreetStyles.CircleCount; i++)
            {
                StreetStyles.Circle(i, out double x, out double z, out double radius, out StreetStyle s);
                Assert.That(radius, Is.InRange(100.0, 1000.0));
                Assert.That(double.IsNaN(x) || double.IsNaN(z), Is.False);
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => StreetStyles.Circle(StreetStyles.CircleCount, out _, out _, out _, out _));
        }

        [Test]
        public void SadhusAndRedSarisWalkAtPashupati()
        {
            // Sadhus at Pashupati (ref_characters.md §5): some of the men walking there are dressed as sadhus, nobody
            // elsewhere; women pilgrims wear red and maroon saris more than anywhere else; most pilgrims carry a tika.
            int sadhus = 0, elsewhere = 0, men = 0, red = 0, saris = 0, tika = 0, pilgrims = 0;
            for (uint seed = 1; seed <= 400; seed++)
            {
                foreach (PedArchetype a in new[] { PedArchetype.UrbanCasual, PedArchetype.DauraSuruwal, PedArchetype.Vendor })
                {
                    CharacterRecipe at = CharacterRecipe.ForPedestrian(a, (int)seed, seed, StreetStyle.Pashupati, 0);
                    men++;
                    if (at.Hair == CharacterRecipe.HairDreadlocks && at[OutfitSlotKind.Torso].Item == OutfitItem.Robe) sadhus++;
                    else
                    {
                        pilgrims++;
                        if (at.Has(CharacterAccents.Tika) || at.Has(CharacterAccents.Bindi)) tika++;
                    }
                    CharacterRecipe away = CharacterRecipe.ForPedestrian(a, (int)seed, seed, StreetStyle.OldBazaar, 0);
                    if (away.Hair == CharacterRecipe.HairDreadlocks) elsewhere++;
                }
                CharacterRecipe woman = CharacterRecipe.ForPedestrian(PedArchetype.KurtaSari, (int)seed, seed, StreetStyle.Pashupati, 0);
                if (woman[OutfitSlotKind.Torso].Item == OutfitItem.Sari)
                {
                    saris++;
                    uint c = CharacterPalette.Colour(OutfitItem.Sari, woman[OutfitSlotKind.Torso].Colour);
                    if (c == 0xC8202Fu || c == 0x7A1F2Bu || c == 0xC2185Bu) red++;
                }
            }
            TestContext.WriteLine(sadhus + " sadhus of " + men + " men; " + red + " red of " + saris + " saris; tika on " + tika + " of " + pilgrims);
            Assert.That(sadhus, Is.InRange(men * 6 / 100, men * 20 / 100));
            Assert.That(elsewhere, Is.EqualTo(0));
            Assert.That(saris, Is.GreaterThan(400 * 6 / 10));
            Assert.That(red, Is.GreaterThan(saris / 2));
            Assert.That(tika, Is.GreaterThan(pilgrims / 2));
        }

        [Test]
        public void TintKeysFindTheShadesOfTheMainGarment()
        {
            const uint key = 0xD9455Bu;
            Assert.That(BodyKit.ShadeOf(key, key), Is.EqualTo(1f));
            Assert.That(BodyKit.ShadeOf(CharacterPalette.Shade(key, 0.8f), key), Is.EqualTo(0.8f).Within(0.03f));
            Assert.That(BodyKit.ShadeOf(CharacterPalette.Shade(key, 1.15f), key), Is.EqualTo(1.15f).Within(0.05f));
            Assert.That(BodyKit.ShadeOf(0x2E8B57u, key), Is.EqualTo(-1f), "a green is not a shade of red");
            Assert.That(BodyKit.ShadeOf(0x101010u, 0x0A0A0Au), Is.EqualTo(-1f).Or.EqualTo(1f), "near-black keys are never tinted by accident");
        }

        [Test]
        public void UniformsAndHakuPatasiFollowTheReferencePhotos()
        {
            int vests = 0, drapes = 0, beads = 0;
            const int n = 80;
            for (uint seed = 1; seed <= n; seed++)
            {
                CharacterRecipe police = CharacterRecipe.ForPedestrian(PedArchetype.TrafficPolice, (int)seed, seed, StreetStyle.Urban, 0);
                Assert.That(HumanoidMesher.TintKeyOf(police), Is.EqualTo(0u), "a uniform is never tinted");
                if (HumanoidMesher.WearsPoliceVest(police)) vests++;
                CharacterRecipe haku = CharacterRecipe.ForPedestrian(PedArchetype.Hakupatasi, (int)seed, seed, StreetStyle.NewarTown, 0);
                Assert.That(haku[OutfitSlotKind.Torso].Colour, Is.EqualTo(CharacterPalette.HakuSari));
                if (haku[OutfitSlotKind.Torso].Pattern % 2 == 1)
                {
                    drapes++;
                    if (haku.Has(CharacterAccents.Pote)) beads++;
                    Assert.That(haku.Has(CharacterAccents.Shawl), Is.False, "the draped pallu shows");
                }
            }
            Assert.That(vests, Is.InRange(n / 5, n * 4 / 5), "about half the officers wear the vest");
            Assert.That(drapes, Is.InRange(n / 5, n * 4 / 5), "about half drape the patasi with its pallu");
            Assert.That(beads, Is.EqualTo(drapes), "with the red bead necklace");
        }

        private static StreetStyle StyleAt(double lon, double lat, AreaType area)
        {
            WorldFrame.LonLatToGame(lon, lat, out double x, out double z);
            return StreetStyles.At(x, z, area);
        }
    }
}
