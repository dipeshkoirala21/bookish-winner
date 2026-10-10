using System;
using Ghumante.Core.Data;
using Ghumante.Core.Generators.Fauna;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The fauna generators (Core/Generators/Fauna): meshes of every species and level are valid, skinned, rounded,
    /// within their triangle budgets and deterministic; the procedural clips are finite, periodic and keep feet and
    /// bellies on the ground; the flock and herd simulations are deterministic and behave (bursts, landing,
    /// soaring, keeping away from the player); the ambient planner places the curated residents and respects the clock.
    /// </summary>
    [TestFixture]
    public class GeneratorsFaunaTests
    {
        private static FaunaSpecies[] AllSpecies()
        {
            var a = new FaunaSpecies[FaunaCatalog.SpeciesCount];
            for (int i = 0; i < a.Length; i++) a[i] = (FaunaSpecies)i;
            return a;
        }

        [Test]
        public void EverySpeciesLevelAndPatternIsAValidSkinnedMesh()
        {
            foreach (FaunaSpecies sp in AllSpecies())
                for (int lod = 0; lod < FaunaMesher.LodCount; lod++)
                    foreach (CoatPattern pat in FaunaPalette.Patterns(sp))
                    {
                        FaunaMesh f = FaunaMesher.Create(sp, lod, pat);
                        MeshData m = f.Mesh;
                        string what = sp + " lod" + lod + " " + pat;
                        Assert.Greater(m.VertexCount, 20, what);
                        Assert.Greater(m.TriangleCount, 10, what);
                        Assert.IsTrue(m.HasUv0, what + ": UV0 = (channel, AO)");
                        for (int v = 0; v < m.VertexCount; v++)
                        {
                            for (int k = 0; k < 3; k++)
                            {
                                Assert.IsFalse(float.IsNaN(m.Positions[3 * v + k]) || float.IsInfinity(m.Positions[3 * v + k]), what);
                                Assert.IsFalse(float.IsNaN(m.Normals[3 * v + k]), what);
                            }
                            float ch = m.Uv0[2 * v], ao = m.Uv0[2 * v + 1];
                            Assert.That(ch, Is.InRange(0f, (float)MaterialChannel.Marking), what + " channel");
                            Assert.AreEqual(Math.Round(ch), ch, 1e-4, what + " channel is integral");
                            Assert.That(ao, Is.InRange(0.15f, 1f), what + " AO");
                            Assert.Less(f.Bone0[v], FaunaRig.BoneCount, what);
                            Assert.Less(f.Bone1[v], FaunaRig.BoneCount, what);
                            Assert.That(f.Weight0[v], Is.InRange(0.5f, 1f), what + " heavier bone first");
                        }
                        for (int i = 0; i < m.IndexCount; i++) Assert.That(m.Indices[i], Is.InRange(0, m.VertexCount - 1), what);
                        Assert.Greater(f.BoundsRadiusM, 0.05f, what);
                        Assert.IsTrue(f.Uses(FaunaBone.Head), what + " has a head");
                    }
        }

        [Test]
        public void MeshesAreDeterministic()
        {
            foreach (FaunaSpecies sp in AllSpecies())
            {
                FaunaMesh a = FaunaMesher.Create(sp, 0, FaunaPalette.Patterns(sp)[0]);
                FaunaMesh b = FaunaMesher.Create(sp, 0, FaunaPalette.Patterns(sp)[0]);
                Assert.AreEqual(a.VertexCount, b.VertexCount, sp.ToString());
                Assert.AreEqual(a.Mesh.IndexCount, b.Mesh.IndexCount, sp.ToString());
                for (int i = 0; i < a.VertexCount * 3; i++) Assert.AreEqual(a.Mesh.Positions[i], b.Mesh.Positions[i], sp.ToString());
                for (int i = 0; i < a.VertexCount * 4; i++) Assert.AreEqual(a.Mesh.Colors[i], b.Mesh.Colors[i], sp.ToString());
            }
        }

        [Test]
        public void TrianglesDrawnStayWithinTheBudgetOfEachLevel()
        {
            var pose = new FaunaPose();
            var sk = new FaunaSkinner();
            var md = new MeshData();
            foreach (FaunaSpecies sp in AllSpecies())
            {
                FaunaSpeciesInfo info = FaunaCatalog.Info(sp);
                int lods = info.Flies ? 2 : 3;
                for (int lod = 0; lod < lods; lod++)
                    foreach (CoatPattern pat in FaunaPalette.Patterns(sp))
                    {
                        FaunaMesh f = FaunaMesher.Create(sp, lod, pat);
                        foreach (FaunaClip clip in new[] { FaunaClip.Stand, FaunaClip.Flap })
                        {
                            if (clip == FaunaClip.Flap && !info.IsAvian) continue;
                            FaunaAnimator.Evaluate(f, clip, 0.3f, 0f, 1u, pose);
                            sk.Bake(f, pose, md);
                            Assert.LessOrEqual(md.TriangleCount, info.Budget(lod), sp + " lod" + lod + " " + pat + " " + clip);
                        }
                    }
                // Each level is lighter than the one before.
                Assert.Less(FaunaMesher.Create(sp, 1).TriangleCount, FaunaMesher.Create(sp, 0).TriangleCount, sp.ToString());
                if (info.Flies)
                {
                    BirdMesher.PaperBird(sp, 0.3f, md);
                    Assert.LessOrEqual(md.TriangleCount, info.Budget(2), sp + " paper bird");
                    Assert.AreEqual(BirdMesher.PaperBirdTris, md.TriangleCount);
                }
                else
                {
                    Assert.Less(FaunaMesher.Create(sp, 2).TriangleCount, FaunaMesher.Create(sp, 1).TriangleCount, sp.ToString());
                }
            }
        }

        [Test]
        public void MeshesAreRoundedNotBoxy()
        {
            // Smooth shading: almost every vertex normal differs from its triangle neighbours by a small angle, and the
            // normals point in many directions (a box has six).
            foreach (FaunaSpecies sp in new[] { FaunaSpecies.Cow, FaunaSpecies.Dog, FaunaSpecies.Macaque, FaunaSpecies.Pigeon })
            {
                MeshData m = FaunaMesher.Create(sp, 0).Mesh;
                var dirs = new System.Collections.Generic.HashSet<int>();
                for (int v = 0; v < m.VertexCount; v++)
                {
                    int bx = (int)Math.Round(m.Normals[3 * v] * 6), by = (int)Math.Round(m.Normals[3 * v + 1] * 6), bz = (int)Math.Round(m.Normals[3 * v + 2] * 6);
                    dirs.Add(bx * 169 + by * 13 + bz);
                }
                Assert.Greater(dirs.Count, 100, sp + " normal directions");
            }
        }

        [Test]
        public void MammalsAreLeftRightSymmetric()
        {
            foreach (FaunaSpecies sp in new[] { FaunaSpecies.Cow, FaunaSpecies.Bull, FaunaSpecies.Buffalo, FaunaSpecies.Goat, FaunaSpecies.Macaque })
            {
                FaunaMesh f = FaunaMesher.Create(sp, 0);
                f.Mesh.GetBounds(out float x0, out _, out _, out float x1, out _, out _);
                Assert.AreEqual(-x0, x1, 0.01f * (x1 - x0) + 0.005f, sp.ToString());
                Fv3 l = f.Pivot[(int)FaunaBone.FrontUpperL], r = f.Pivot[(int)FaunaBone.FrontUpperR];
                Assert.AreEqual(-l.X, r.X, 1e-5f);
                Assert.Less(l.X, 0f, "left is −X");
            }
        }

        [Test]
        public void IdentityPoseReproducesTheBindMesh()
        {
            var pose = new FaunaPose();
            var sk = new FaunaSkinner();
            var md = new MeshData();
            foreach (FaunaSpecies sp in AllSpecies())
            {
                FaunaMesh f = FaunaMesher.Create(sp, 1);
                pose.Reset();
                sk.Bake(f, pose, md);
                Assert.AreEqual(f.Mesh.IndexCount, md.IndexCount, sp + ": nothing hidden in the bind pose");
                for (int i = 0; i < f.VertexCount * 3; i++) Assert.AreEqual(f.Mesh.Positions[i], md.Positions[i], 1e-5f, sp.ToString());
            }
        }

        [Test]
        public void SkinnerRotatesAboutThePivot()
        {
            FaunaMesh f = FaunaMesher.Create(FaunaSpecies.Cow, 1);
            var pose = new FaunaPose();
            var sk = new FaunaSkinner();
            pose.Set(FaunaBone.Head, 30f, 0f, 0f);
            sk.Solve(f, pose);
            Fv3 piv = f.Pivot[(int)FaunaBone.Head];
            Fv3 moved = sk.Bones[(int)FaunaBone.Head].Point(piv);
            Assert.AreEqual(0f, Fv3.Distance(piv, moved), 1e-5f, "the pivot stays put");
            // A point in front of the poll goes down when the head pitches down.
            Fv3 nose = piv + new Fv3(0f, 0f, 0.3f);
            Assert.Less(sk.Bones[(int)FaunaBone.Head].Point(nose).Y, nose.Y - 0.1f);
            // Children follow.
            Assert.Less(sk.Bones[(int)FaunaBone.Jaw].Point(f.Pivot[(int)FaunaBone.Jaw]).Y, f.Pivot[(int)FaunaBone.Jaw].Y);
        }

        [Test]
        public void EveryClipGivesAFinitePose()
        {
            var pose = new FaunaPose();
            var sk = new FaunaSkinner();
            foreach (FaunaSpecies sp in AllSpecies())
            {
                FaunaMesh f = FaunaMesher.Create(sp, 2);
                for (int c = 0; c <= (int)FaunaClip.Run; c++)
                    for (float t = 0f; t < 6f; t += 0.37f)
                    {
                        FaunaAnimator.Evaluate(f, (FaunaClip)c, t, 0f, 12345u, pose);
                        sk.Solve(f, pose);
                        for (int b = 0; b < FaunaRig.BoneCount; b++)
                        {
                            FXform x = sk.Bones[b];
                            Assert.IsFalse(float.IsNaN(x.T.X + x.T.Y + x.T.Z + x.R.M00 + x.R.M11 + x.R.M22 + x.S), sp + " " + (FaunaClip)c);
                        }
                    }
            }
        }

        [Test]
        public void GaitsLoop()
        {
            var a = new FaunaPose();
            var b = new FaunaPose();
            foreach (FaunaSpecies sp in new[] { FaunaSpecies.Cow, FaunaSpecies.Dog, FaunaSpecies.Goat, FaunaSpecies.Pigeon })
            {
                FaunaMesh f = FaunaMesher.Create(sp, 2);
                foreach (FaunaClip clip in new[] { FaunaClip.Walk, FaunaClip.Trot, FaunaClip.Flap })
                {
                    if (clip == FaunaClip.Flap && !FaunaCatalog.Info(sp).IsAvian) continue;
                    if (clip == FaunaClip.Trot && FaunaCatalog.Info(sp).IsAvian) continue;
                    float cycle = FaunaAnimator.CycleSeconds(sp, clip, 0f);
                    Assert.Greater(cycle, 0.05f);
                    FaunaAnimator.Evaluate(f, clip, 1.0f, 0f, 0u, a);
                    FaunaAnimator.Evaluate(f, clip, 1.0f + cycle, 0f, 0u, b);
                    foreach (FaunaBone bone in new[] { FaunaBone.FrontUpperL, FaunaBone.HindUpperR, FaunaBone.WingL })
                    {
                        FRot ra = a.Rot[(int)bone], rb = b.Rot[(int)bone];
                        Assert.AreEqual(ra.M11, rb.M11, 2e-3f, sp + " " + clip + " " + bone);
                        Assert.AreEqual(ra.M12, rb.M12, 2e-3f, sp + " " + clip + " " + bone);
                    }
                }
            }
        }

        [Test]
        public void StandingFeetAndLyingBelliesRestOnTheGround()
        {
            var pose = new FaunaPose();
            var sk = new FaunaSkinner();
            var md = new MeshData();
            foreach (FaunaSpecies sp in new[] { FaunaSpecies.Cow, FaunaSpecies.Buffalo, FaunaSpecies.Dog, FaunaSpecies.Goat, FaunaSpecies.Hen, FaunaSpecies.Pigeon })
            {
                FaunaMesh f = FaunaMesher.Create(sp, 0);
                float h = FaunaCatalog.Info(sp).HeightM;
                FaunaAnimator.Evaluate(f, FaunaClip.Stand, 0f, 0f, 3u, pose);
                sk.Bake(f, pose, md);
                md.GetBounds(out _, out float y0, out _, out _, out _, out _);
                Assert.AreEqual(0f, y0, 0.03f * h + 0.004f, sp + " standing on the ground");
                if (FaunaCatalog.Info(sp).IsAvian) continue;
                FaunaClip rest = sp == FaunaSpecies.Dog ? FaunaClip.Sleep : FaunaClip.Lie;
                FaunaAnimator.Evaluate(f, rest, 0f, 0f, 3u, pose);
                sk.Bake(f, pose, md);
                md.GetBounds(out _, out y0, out _, out _, out float y1, out _);
                Assert.That(y0, Is.InRange(-0.2f * h, 0.05f * h), sp + " lying: folded legs only just below the belly line");
                Assert.Less(y1, 0.92f * h + 0.3f, sp + " lying is lower than standing");
            }
        }

        [Test]
        public void FlightClipsCentreTheBirdAndHideTheFoldedWings()
        {
            var pose = new FaunaPose();
            var sk = new FaunaSkinner();
            var md = new MeshData();
            foreach (FaunaSpecies sp in new[] { FaunaSpecies.Pigeon, FaunaSpecies.BlackKite, FaunaSpecies.Crow })
            {
                FaunaMesh f = FaunaMesher.Create(sp, 0);
                FaunaAnimator.Evaluate(f, FaunaClip.Glide, 0f, 0f, 1u, pose);
                Assert.AreEqual(0f, pose.Scale[(int)FaunaBone.FoldL]);
                Assert.AreEqual(1f, pose.Scale[(int)FaunaBone.WingL]);
                sk.Bake(f, pose, md);
                md.GetBounds(out float x0, out float y0, out float z0, out float x1, out float y1, out float z1);
                float span = FaunaCatalog.Info(sp).WingspanM;
                Assert.Greater(x1 - x0, 0.8f * span, sp + " wings spread");
                Assert.Less(Math.Abs(0.5f * (y0 + y1)), 0.3f * span, sp + " centred");
                FaunaAnimator.Evaluate(f, FaunaClip.Stand, 0f, 0f, 1u, pose);
                sk.Bake(f, pose, md);
                md.GetBounds(out x0, out _, out _, out x1, out _, out _);
                Assert.Less(x1 - x0, 0.4f * span, sp + " wings folded at rest");
            }
        }

        [Test]
        public void CoatsMapToPatternsTheSpeciesHas()
        {
            foreach (FaunaSpecies sp in AllSpecies())
                for (int t = 0; t < 40; t++)
                {
                    FaunaPalette.CoatFor(sp, t, out CoatPattern p, out uint rgb);
                    Assert.Contains(p, FaunaPalette.Patterns(sp), sp + " tint " + t);
                    Assert.LessOrEqual(rgb, 0xFFFFFFu);
                }
        }

        // ------------------------------------------------------------------------------------------------------------
        // Flocks and herds

        private static void Run(FlockSim s, float seconds, float tx, float tz, bool threat)
        {
            for (float t = 0f; t < seconds; t += 0.05f) s.Step(0.05f, tx, tz, threat);
        }

        [Test]
        public void FlocksAreDeterministic()
        {
            for (int k = 0; k <= (int)FlockKind.Egrets; k++)
            {
                var a = new FlockSim((FlockKind)k, 60, 1000.0, 2000.0, 1300f, 99u);
                var b = new FlockSim((FlockKind)k, 60, 1000.0, 2000.0, 1300f, 99u);
                Run(a, 3f, 50f, 50f, false);
                Run(b, 3f, 50f, 50f, false);
                a.Startle();
                b.Startle();
                Run(a, 6f, 0f, 0f, true);
                Run(b, 6f, 0f, 0f, true);
                for (int i = 0; i < a.Count; i++)
                {
                    Assert.AreEqual(a.X[i], b.X[i], (FlockKind)k + " x");
                    Assert.AreEqual(a.Y[i], b.Y[i], (FlockKind)k + " y");
                    Assert.AreEqual(a.Clip[i], b.Clip[i], (FlockKind)k + " clip");
                }
            }
        }

        [Test]
        public void PigeonsBurstCircleAndReLand()
        {
            var s = new FlockSim(FlockKind.Pigeons, 150, 0.0, 0.0, 0f, 7u);
            Run(s, 5f, 80f, 80f, false);
            Assert.IsFalse(s.Airborne);
            for (int i = 0; i < s.Count; i++)
            {
                Assert.AreEqual(0f, s.Y[i], "pecking on the ground");
                Assert.Less(Math.Sqrt(s.X[i] * s.X[i] + s.Z[i] * s.Z[i]), s.P.DiscRadiusM * 1.4f, "inside the disc");
            }
            // Walk up to the flock: it bursts.
            Run(s, 1.5f, s.X[0], s.Z[0], true);
            Assert.IsTrue(s.Airborne);
            int up = 0;
            for (int i = 0; i < s.Count; i++)
                if (s.Y[i] > 0.5f)
                    up++;
            Assert.Greater(up, s.Count * 9 / 10, "almost all in the air 1.5 s after the burst");
            // In the air: birds keep their distance and fly at about the cruise speed.
            Run(s, 6f, 80f, 80f, false);
            float minD = float.MaxValue, maxY = 0f;
            for (int i = 0; i < s.Count; i++)
            {
                maxY = Math.Max(maxY, s.Y[i]);
                for (int j = i + 1; j < s.Count; j++)
                {
                    float dx = s.X[i] - s.X[j], dy = s.Y[i] - s.Y[j], dz = s.Z[i] - s.Z[j];
                    minD = Math.Min(minD, dx * dx + dy * dy + dz * dz);
                }
            }
            Assert.Greater(Math.Sqrt(minD), 0.05f, "no two birds in the same place");
            Assert.Greater(maxY, 6f, "circling above the roofs");
            // Back on the ground within 45 s of the burst.
            Run(s, 45f, 80f, 80f, false);
            Assert.IsFalse(s.Airborne, "re-landed");
            for (int i = 0; i < s.Count; i++) Assert.AreEqual(0f, s.Y[i]);
        }

        [Test]
        public void KitesSoarInBankedCirclesHighUp()
        {
            var s = new FlockSim(FlockKind.Kites, 6, 0.0, 0.0, 0f, 3u);
            for (int step = 0; step < 400; step++)
            {
                s.Step(0.05f, 0f, 0f, false);
                for (int i = 0; i < s.Count; i++)
                {
                    Assert.That(s.Y[i], Is.InRange(45f, 215f), "height");
                    Assert.That(Math.Abs(s.Bank[i]), Is.InRange(15f * FMath.Deg - 1e-4f, 30f * FMath.Deg + 1e-4f), "bank");
                    float v = (float)Math.Sqrt(s.VX[i] * s.VX[i] + s.VZ[i] * s.VZ[i]);
                    Assert.That(v, Is.InRange(7f, 13f), "glide speed");
                }
            }
        }

        [Test]
        public void HerdsAreDeterministicAndKeepAwayFromThePlayer()
        {
            var a = new HerdSim(FaunaSpecies.Macaque, 20, 0.0, 0.0, 0f, 10f, 5u);
            var b = new HerdSim(FaunaSpecies.Macaque, 20, 0.0, 0.0, 0f, 10f, 5u);
            for (int k = 0; k < 100; k++)
            {
                a.Step(0.1f, 0f, 0f, true);
                b.Step(0.1f, 0f, 0f, true);
            }
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a.X[i], b.X[i]);
                Assert.AreEqual(a.Clip[i], b.Clip[i]);
            }
            // After ten seconds with the player standing at home, no adult macaque is within 1.5 m of it.
            for (int i = 0; i < a.Count; i++)
            {
                if (a.IsRider(i)) continue;
                Assert.Greater(Math.Sqrt(a.X[i] * a.X[i] + a.Z[i] * a.Z[i]), 1.5, "macaque " + i);
            }
            // One in ten carries a baby.
            int riders = 0;
            for (int i = 0; i < a.Count; i++)
                if (a.IsRider(i))
                {
                    riders++;
                    Assert.AreEqual(FaunaSpecies.MacaqueBaby, a.SpeciesOf(i));
                }
            Assert.AreEqual(2, riders);
        }

        // ------------------------------------------------------------------------------------------------------------
        // Planner

        private sealed class FixedHabitat : IFaunaHabitat
        {
            public AreaType Area;

            public AreaType AreaAt(double x, double z)
            {
                return Area;
            }
        }

        [Test]
        public void PlannerPutsTheMacaquesAtSwayambhuAndPigeonsOnBasantapur()
        {
            var p = new AmbientFaunaPlanner(42u);
            var dst = new AmbientGroup[64];
            int sway = Array.FindIndex(AmbientFaunaPlanner.Sites, s => s.Name == "Swayambhu");
            p.SitePosition(sway, out double sx, out double sz);
            int n = p.Plan(sx + 30.0, sz, 300f, 10, 10f, null, dst);
            int troops = 0;
            for (int i = 0; i < n; i++)
                if (dst[i].Kind == AmbientKind.MacaqueTroop)
                    troops++;
            Assert.AreEqual(3, troops, "three troops round the stupa");
            for (int i = 1; i < n; i++) Assert.LessOrEqual(dst[i - 1].DistanceM, dst[i].DistanceM, "nearest first");
            int bas = Array.FindIndex(AmbientFaunaPlanner.Sites, s => s.Name == "Basantapur");
            p.SitePosition(bas, out double bx, out double bz);
            n = p.Plan(bx, bz, 150f, 10, 11f, null, dst);
            Assert.IsTrue(Array.Exists(dst, g => g.Kind == AmbientKind.PigeonFlock && g.Count >= 100 && g.DistanceM < 20f), "the Basantapur flock");
            // At night the pigeons roost.
            n = p.Plan(bx, bz, 150f, 10, 23f, null, dst);
            for (int i = 0; i < n; i++) Assert.AreNotEqual(AmbientKind.PigeonFlock, dst[i].Kind);
        }

        [Test]
        public void PlannerIsDeterministicAndFollowsTheAreaType()
        {
            var p = new AmbientFaunaPlanner(7u);
            var a = new AmbientGroup[48];
            var b = new AmbientGroup[48];
            var rural = new FixedHabitat { Area = AreaType.Rural };
            int na = p.Plan(500000.0, 3000000.0, 400f, 7, 9f, rural, a);
            int nb = new AmbientFaunaPlanner(7u).Plan(500000.0, 3000000.0, 400f, 7, 9f, rural, b);
            Assert.AreEqual(na, nb);
            Assert.Greater(na, 5);
            for (int i = 0; i < na; i++)
            {
                Assert.AreEqual(a[i].Key, b[i].Key);
                Assert.AreNotEqual(AmbientKind.PigeonFlock, a[i].Kind, "no pigeon flocks in the fields");
                Assert.Greater(a[i].Count, 0);
            }
            Assert.IsTrue(Array.Exists(a, g => g.Kind == AmbientKind.EgretGroup), "egrets in the July paddy");
            var core = new FixedHabitat { Area = AreaType.OldCore };
            int nc = p.Plan(500000.0, 3000000.0, 400f, 1, 9f, core, a);
            for (int i = 0; i < nc; i++)
            {
                Assert.AreNotEqual(AmbientKind.GoatHerd, a[i].Kind, "no goat herds in the old core");
                Assert.AreNotEqual(AmbientKind.SwallowGroup, a[i].Kind, "no swallows in January");
            }
        }

        [Test]
        public void PaperBirdFacesBothWays()
        {
            var m = new MeshData();
            BirdMesher.PaperBird(FaunaSpecies.Pigeon, 0.5f, m);
            int up = 0, down = 0;
            for (int i = 0; i < m.IndexCount; i += 3)
            {
                int a = m.Indices[i], b = m.Indices[i + 1], c = m.Indices[i + 2];
                var pa = new Fv3(m.Positions[3 * a], m.Positions[3 * a + 1], m.Positions[3 * a + 2]);
                var pb = new Fv3(m.Positions[3 * b], m.Positions[3 * b + 1], m.Positions[3 * b + 2]);
                var pc = new Fv3(m.Positions[3 * c], m.Positions[3 * c + 1], m.Positions[3 * c + 2]);
                float ny = Fv3.Cross(pb - pa, pc - pa).Y;
                if (ny > 0f) up++;
                else if (ny < 0f) down++;
            }
            Assert.AreEqual(4, up);
            Assert.AreEqual(4, down);
        }
    }
}
