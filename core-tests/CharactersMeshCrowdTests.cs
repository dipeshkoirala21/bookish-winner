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
                int worst = CrowdLodPlan.WorstCaseTriangles(tier, HumanoidMesher.Budget(0), HumanoidMesher.Budget(1), HumanoidMesher.Budget(2),
                                                            HumanoidMesher.Budget(CrowdLodPlan.PlayerLod(tier)));
                TestContext.WriteLine("tier " + tier + ": " + worst + " of " + CrowdLodPlan.SliceTris[tier]);
                Assert.That(worst, Is.LessThanOrEqualTo(CrowdLodPlan.SliceTris[tier]), "tier " + tier);
            }
            // The player is always LOD0; the far band is always the baked LOD2; Low draws no NPC above LOD2.
            for (int tier = 0; tier < 3; tier++)
            {
                Assert.That(CrowdLodPlan.PlayerLod(tier), Is.EqualTo(0));
                Assert.That(CrowdLodPlan.MeshLod(tier, 2, 0), Is.EqualTo(2));
                Assert.That(CrowdLodPlan.Skinned(2), Is.False);
            }
            for (int rank = 0; rank < 30; rank++)
            {
                Assert.That(CrowdLodPlan.MeshLod(0, 0, rank), Is.EqualTo(2));
                Assert.That(CrowdLodPlan.MeshLod(0, 1, rank), Is.EqualTo(2));
            }
            Assert.That(CrowdLodPlan.MeshLod(2, 0, 0), Is.EqualTo(0), "High: the nearest person is LOD0");
            Assert.That(CrowdLodPlan.MeshLod(2, 0, 1), Is.EqualTo(1));
            Assert.That(CrowdLodPlan.MeshLod(1, 0, 0), Is.EqualTo(1));
        }

        [Test]
        public void EverydayCrowdBodiesKeepFullDetailAndFitTheSlices()
        {
            int[] worst = new int[3];
            int bodies = 0, dropped = 0;
            foreach (PedArchetype a in Enum.GetValues(typeof(PedArchetype)))
            {
                foreach (StreetStyle style in Enum.GetValues(typeof(StreetStyle)))
                {
                    foreach (int carry in new[] { 0, a == PedArchetype.Porter ? 1 : 5 })
                    {
                        for (int variant = 0; variant < CrowdVariants.Variants; variant += 3)
                        {
                            CharacterRecipe r = CrowdVariants.Recipe(CrowdVariants.Key(a, style, carry, variant));
                            for (int lod = 1; lod <= 2; lod++)
                            {
                                var m = new MeshData();
                                var w = new SkinWeights();
                                HumanoidMesher.Build(r, lod, m, w);
                                worst[lod] = Math.Max(worst[lod], m.TriangleCount);
                                bodies++;
                                if (HumanoidMesher.DetailDrop(r, lod, HeadwearMode.Outfit) > 0) dropped++;
                            }
                        }
                    }
                }
            }
            // A sample of LOD0 bodies (the nearest person on High).
            foreach (PedArchetype a in Enum.GetValues(typeof(PedArchetype)))
            {
                CharacterRecipe r = CrowdVariants.Recipe(CrowdVariants.Key(a, StreetStyle.Urban, 0, 1));
                var m = new MeshData();
                HumanoidMesher.BuildStatic(r, 0, m);
                worst[0] = Math.Max(worst[0], m.TriangleCount);
                Assert.That(HumanoidMesher.DetailDrop(r, 0, HeadwearMode.Outfit), Is.EqualTo(0), a + " at LOD0");
            }
            TestContext.WriteLine("crowd worst triangles: LOD0 " + worst[0] + ", LOD1 " + worst[1] + ", LOD2 " + worst[2] + "; " + dropped + " of " + bodies +
                                  " bodies needed a detail drop");
            Assert.That(dropped, Is.LessThanOrEqualTo(bodies / 20), "nearly every everyday body keeps its full detail");
            for (int tier = 0; tier < 3; tier++)
                Assert.That(CrowdLodPlan.WorstCaseTriangles(tier, worst[0], worst[1], worst[2], HumanoidMesher.Budget(0)),
                            Is.LessThanOrEqualTo(CrowdLodPlan.SliceTris[tier]));
            // The default player recipe never drops detail.
            var player = new CharacterRecipe();
            for (int lod = 0; lod <= 2; lod++)
            {
                Assert.That(HumanoidMesher.DetailDrop(player, lod, HeadwearMode.Outfit), Is.EqualTo(0), "player lod " + lod);
                Assert.That(HumanoidMesher.DetailDrop(player, lod, HeadwearMode.Helmet), Is.EqualTo(0), "player helmet lod " + lod);
            }
        }

        [Test]
        public void VariantKeysRoundTripAndShareFarBodies()
        {
            foreach (PedArchetype a in Enum.GetValues(typeof(PedArchetype)))
                foreach (StreetStyle style in Enum.GetValues(typeof(StreetStyle)))
                    for (int carry = 0; carry <= 5; carry++)
                        for (int variant = 0; variant < CrowdVariants.Variants; variant++)
                        {
                            int key = CrowdVariants.Key(a, style, carry, variant);
                            CrowdVariants.Split(key, out PedArchetype a2, out StreetStyle s2, out int c2, out int v2);
                            Assert.That(a2, Is.EqualTo(a));
                            Assert.That(s2, Is.EqualTo(style));
                            Assert.That(c2, Is.EqualTo(carry));
                            Assert.That(v2, Is.EqualTo(variant));
                            CrowdVariants.Split(CrowdVariants.FarKey(key), out _, out _, out _, out int fv);
                            Assert.That(fv, Is.LessThan(CrowdVariants.FarVariants));
                        }
            for (int id = 0; id < 500; id++)
            {
                int v = CrowdVariants.VariantOf(id);
                Assert.That(v, Is.InRange(0, CrowdVariants.Variants - 1));
                Assert.That(CrowdVariants.VariantOf(id), Is.EqualTo(v), "deterministic");
            }
            // A variant body is the same recipe every time; different variants differ.
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
