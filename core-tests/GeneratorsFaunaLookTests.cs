using System;
using Ghumante.Core.Generators.Fauna;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The looks fixed in the review of the detail pass (docs/research/w2/ref_animals.md): the zebu hump and the dog's
    /// winter ruff are part of their lofts (no pasted-on blobs); the macaque is stocky with a golden rump and a pale
    /// cheek ruff, sits with its hands on the ground or at its mouth (no zombie arms) on rigid limb segments with
    /// round knees and elbows, and climbs from the foot of a wall; the near pigeon is one loft from rump to bill
    /// with a pupil in its eye and the sheen only round its neck; the egret flies with its neck drawn in and its legs
    /// trailing.
    /// </summary>
    [TestFixture]
    public class GeneratorsFaunaLookTests
    {
        private static readonly FaunaPose Pose = new FaunaPose();
        private static readonly FaunaSkinner Skinner = new FaunaSkinner();

        /// <summary>Connected component of every vertex (shared triangle indices), by union-find.</summary>
        private static int[] Components(MeshData m)
        {
            var parent = new int[m.VertexCount];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            for (int i = 0; i < m.IndexCount; i += 3)
            {
                Union(parent, m.Indices[i], m.Indices[i + 1]);
                Union(parent, m.Indices[i], m.Indices[i + 2]);
            }
            for (int i = 0; i < parent.Length; i++) parent[i] = Find(parent, i);
            return parent;
        }

        private static int Find(int[] p, int i)
        {
            while (p[i] != i)
            {
                p[i] = p[p[i]];
                i = p[i];
            }
            return i;
        }

        private static void Union(int[] p, int a, int b)
        {
            a = Find(p, a);
            b = Find(p, b);
            if (a != b) p[a] = b;
        }

        private static Fv3 Pos(MeshData m, int v)
        {
            return new Fv3(m.Positions[3 * v], m.Positions[3 * v + 1], m.Positions[3 * v + 2]);
        }

        private static void Rgba(MeshData m, int v, out int r, out int g, out int b, out int a)
        {
            r = m.Colors[4 * v];
            g = m.Colors[4 * v + 1];
            b = m.Colors[4 * v + 2];
            a = m.Colors[4 * v + 3];
        }

        // ------------------------------------------------------------------------------------------------------------
        // Zebu hump and dog ruff

        [Test]
        public void TheZebuHumpGrowsOutOfTheBackLine()
        {
            foreach ((FaunaSpecies sp, float minRise) in new[] { (FaunaSpecies.Bull, 0.15f), (FaunaSpecies.Cow, 0.06f) })
                for (int lod = 0; lod < 2; lod++)
                {
                    FaunaMesh f = FaunaMesher.Create(sp, lod, CoatPattern.Plain);
                    MeshData m = f.Mesh;
                    int[] comp = Components(m);
                    int bodyComp = -1, humps = 0;
                    float back = float.MinValue, hump = float.MinValue;
                    for (int v = 0; v < m.VertexCount; v++)
                    {
                        var part = (FaunaPart)f.Part[v];
                        Fv3 p = Pos(m, v);
                        if (part == FaunaPart.Body && p.Z > -0.6f && p.Z < -0.1f) back = Math.Max(back, p.Y);
                        if (part == FaunaPart.Body && bodyComp < 0) bodyComp = comp[v];
                        if (part != FaunaPart.Hump) continue;
                        humps++;
                        hump = Math.Max(hump, p.Y);
                    }
                    string what = sp + " lod" + lod;
                    Assert.Greater(humps, lod == 0 ? 6 : 1, what + ": a hump");
                    for (int v = 0; v < m.VertexCount; v++)
                        if ((FaunaPart)f.Part[v] == FaunaPart.Hump)
                            Assert.AreEqual(bodyComp, comp[v], what + ": the hump is part of the body loft, not a separate blob");
                    Assert.Greater(hump - back, minRise * (lod == 0 ? 1f : 0.7f), what + ": the hump rises above the back line");
                }
            // The crossbred dairy cows have only a low rise.
            FaunaMesh x = FaunaMesher.Create(FaunaSpecies.Cow, 0, CoatPattern.Patched);
            float top = float.MinValue;
            for (int v = 0; v < x.VertexCount; v++)
                if ((FaunaPart)x.Part[v] == FaunaPart.Hump)
                    top = Math.Max(top, x.Mesh.Positions[3 * v + 1]);
            Assert.Less(top, 1.2f);
        }

        [Test]
        public void TheDogsRuffIsItsNeckNotACollar()
        {
            for (int lod = 0; lod < 2; lod++)
            {
                FaunaMesh f = FaunaMesher.Create(FaunaSpecies.Dog, lod, CoatPattern.Plain);
                int[] comp = Components(f.Mesh);
                int neck = -1;
                float wide = 0f;
                for (int v = 0; v < f.VertexCount; v++)
                {
                    if ((FaunaPart)f.Part[v] != FaunaPart.Neck) continue;
                    if (neck < 0) neck = comp[v];
                    Assert.AreEqual(neck, comp[v], "lod" + lod + ": one neck loft (no separate ruff shell)");
                    Fv3 p = Pos(f.Mesh, v);
                    if (p.Z > 0.15f && p.Z < 0.23f) wide = Math.Max(wide, Math.Abs(p.X));
                }
                Assert.Greater(wide, lod == 0 ? 0.08f : 0.06f, "lod" + lod + ": the thick winter ruff at the base of the neck");
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // Macaque

        [Test]
        public void TheMacaqueIsStockyWithAGoldenRumpAndAPaleRuff()
        {
            FaunaMesh f = FaunaMesher.Create(FaunaSpecies.Macaque, 0);
            MeshData m = f.Mesh;
            Assert.Greater(f.BodyHalfWidthM, 0.1f, "a stocky, thick-furred torso");
            int golden = 0, rumpVerts = 0, ruff = 0;
            for (int v = 0; v < m.VertexCount; v++)
            {
                var part = (FaunaPart)f.Part[v];
                Fv3 p = Pos(m, v);
                Rgba(m, v, out int r, out int g, out int b, out int a);
                if (part == FaunaPart.Body && p.Z < -0.16f && p.Y > 0.33f)
                {
                    rumpVerts++;
                    // Golden-orange: a fixed colour (not the grey-brown tint), red well above blue.
                    if (a < 100 && r > b + 60 && r > g) golden++;
                }
                if (part == FaunaPart.Head && Math.Abs(r - (int)((PrimateMesher.Ruff >> 24) & 0xFF)) < 40 && a < 220) ruff++;
            }
            Assert.Greater(rumpVerts, 10);
            Assert.Greater(golden, rumpVerts * 3 / 4, "the lower back and rump are golden-orange");
            Assert.Greater(ruff, 10, "a pale ruff frames the face");
            // The thighs are golden too, the upper arms grey-brown (tinted).
            int thighGold = 0, thighs = 0, armTint = 0, arms = 0;
            for (int v = 0; v < m.VertexCount; v++)
            {
                if ((FaunaPart)f.Part[v] != FaunaPart.Leg) continue;
                Rgba(m, v, out int r, out _, out int b, out int a);
                if (f.Bone0[v] == (byte)FaunaBone.HindUpperL)
                {
                    thighs++;
                    if (r > b + 40) thighGold++;
                }
                if (f.Bone0[v] == (byte)FaunaBone.FrontUpperL)
                {
                    arms++;
                    if (a > 200) armTint++;
                }
            }
            Assert.Greater(thighGold, thighs / 2);
            Assert.Greater(armTint, arms / 2);
        }

        [Test]
        public void MacaqueLimbsAreRigidSegmentsWithRoundJoints()
        {
            for (int lod = 0; lod < 2; lod++)
            {
                FaunaMesh f = FaunaMesher.Create(FaunaSpecies.Macaque, lod);
                for (int v = 0; v < f.VertexCount; v++)
                    if ((FaunaPart)f.Part[v] == FaunaPart.Leg)
                        Assert.AreEqual(1f, f.Weight0[v], "lod" + lod + ": a limb segment follows one bone (no folded, blended joints)");
                // A ball sits on each elbow and knee pivot.
                foreach (FaunaBone joint in new[] { FaunaBone.FrontLowerL, FaunaBone.HindLowerL, FaunaBone.FrontLowerR, FaunaBone.HindLowerR })
                {
                    Fv3 pv = f.Pivot[(int)joint];
                    int around = 0;
                    for (int v = 0; v < f.VertexCount; v++)
                        if ((FaunaPart)f.Part[v] == FaunaPart.Leg && Math.Abs(Fv3.Distance(Pos(f.Mesh, v), pv) - 0.03f) < 0.008f)
                            around++;
                    Assert.Greater(around, 8, "lod" + lod + " " + joint + ": a round joint");
                }
            }
        }

        /// <summary>Seed of a sitting variant (FaunaAnimator.MacaqueSit picks by (seed >> 5) % 3).</summary>
        private static uint SeedOf(int variant)
        {
            return (uint)variant << 5;
        }

        private static Fv3 Joint(FaunaMesh f, FaunaBone b)
        {
            return Skinner.Bones[(int)b].Point(f.Pivot[(int)b]);
        }

        [Test]
        public void TheSittingMacaqueRestsItsHandsOrEats()
        {
            FaunaMesh f = FaunaMesher.Create(FaunaSpecies.Macaque, 0);
            var md = new MeshData();
            for (float t = 0f; t < 6f; t += 0.7f)
            {
                // Variant 0: both hands on the ground in front of the feet.
                FaunaAnimator.Evaluate(f, FaunaClip.Sit, t, 0f, SeedOf(0), Pose);
                Skinner.Solve(f, Pose);
                Fv3 hip = Joint(f, FaunaBone.HindUpperL);
                foreach (FaunaBone w in new[] { FaunaBone.FrontFootL, FaunaBone.FrontFootR })
                {
                    Fv3 p = Joint(f, w);
                    Assert.Less(p.Y, 0.06f, "hands on the ground (" + w + ")");
                    Assert.Greater(p.Z, hip.Z + 0.08f, "in front of the body");
                }
                // The feet are flat on the ground in front of the rump, the knees up.
                Fv3 ankle = Joint(f, FaunaBone.HindFootL), knee = Joint(f, FaunaBone.HindLowerL);
                Assert.Less(ankle.Y, 0.07f);
                Assert.Greater(knee.Y, ankle.Y + 0.08f, "the knee is folded up");
                Assert.Greater(ankle.Z, hip.Z + 0.1f);

                // Variant 1: the right hand brings food to the mouth now and then, the left rests on the ground.
                FaunaAnimator.Evaluate(f, FaunaClip.Sit, t, 0f, SeedOf(1), Pose);
                Skinner.Solve(f, Pose);
                Assert.Less(Joint(f, FaunaBone.FrontFootL).Y, 0.06f);
                Fv3 sh = Joint(f, FaunaBone.FrontUpperR), wr = Joint(f, FaunaBone.FrontFootR);
                Assert.Less(Math.Abs(wr.X), 0.12f, "the eating hand comes in to the middle");
                Assert.Greater(wr.Z, sh.Z, "in front of the chest");
                Assert.Less(wr.Z - sh.Z, 0.2f, "not held out at arm's length");
            }
            // Over a few seconds the bite reaches the mouth.
            float best = float.MaxValue;
            for (float t = 0f; t < 6f; t += 0.05f)
            {
                FaunaAnimator.Evaluate(f, FaunaClip.Sit, t, 0f, SeedOf(1), Pose);
                Skinner.Solve(f, Pose);
                best = Math.Min(best, Fv3.Distance(Joint(f, FaunaBone.FrontFootR), Joint(f, FaunaBone.Jaw)));
            }
            Assert.Less(best, 0.09f, "hand to mouth");
            // Grooming: both hands in front of the belly, below the chin, never out at arm's length.
            FaunaAnimator.Evaluate(f, FaunaClip.Groom, 1f, 0f, 3u, Pose);
            Skinner.Solve(f, Pose);
            foreach (FaunaBone w in new[] { FaunaBone.FrontFootL, FaunaBone.FrontFootR })
            {
                Fv3 p = Joint(f, w), s = Joint(f, w == FaunaBone.FrontFootL ? FaunaBone.FrontUpperL : FaunaBone.FrontUpperR);
                Assert.Less(p.Y, s.Y - 0.05f, w + " below the shoulder");
                Assert.That(p.Z - s.Z, Is.InRange(0f, 0.18f), w + " just in front of the body");
            }
            // Every sitting variant keeps the posed monkey on the ground, nothing under it.
            for (int variant = 0; variant < 3; variant++)
            {
                FaunaAnimator.Evaluate(f, FaunaClip.Sit, 2f, 0f, SeedOf(variant), Pose);
                Skinner.Bake(f, Pose, md);
                md.GetBounds(out _, out float y0, out _, out _, out float y1, out _);
                Assert.That(y0, Is.InRange(-0.02f, 0.02f), "variant " + variant + " sits on the ground");
                Assert.That(y1, Is.InRange(0.42f, 0.62f), "variant " + variant + " sitting height");
            }
        }

        [Test]
        public void AClimbingMacaqueFacesTheWallAndStartsFromItsFoot()
        {
            FaunaMesh f = FaunaMesher.Create(FaunaSpecies.Macaque, 0);
            var md = new MeshData();
            FaunaAnimator.Evaluate(f, FaunaClip.Climb, 0.3f, 0f, 1u, Pose);
            Skinner.Bake(f, Pose, md);
            md.GetBounds(out _, out float y0, out float z0, out _, out _, out float z1);
            Assert.That(y0, Is.InRange(-0.03f, 0.08f), "the feet start at the foot of the wall");
            // Hands and feet reach the wall plane in front; the body hangs behind them.
            Skinner.Solve(f, Pose);
            float wall = Joint(f, FaunaBone.FrontFootL).Z;
            Assert.AreEqual(wall, Joint(f, FaunaBone.FrontFootR).Z, 0.03f);
            Assert.AreEqual(wall, Joint(f, FaunaBone.HindFootL).Z, 0.04f);
            Assert.Greater(wall, Joint(f, FaunaBone.Pelvis).Z + 0.05f);
            Assert.Greater(Joint(f, FaunaBone.FrontFootL).Y, Joint(f, FaunaBone.FrontUpperL).Y + 0.1f, "reaching up the face");

            // The sim walks a monkey to the foot of the face, climbs there facing the wall, then sits on top.
            var h = new HerdSim(FaunaSpecies.Macaque, 12, 0.0, 0.0, 0f, 6f, 5u);
            h.SetPerches(new[] { 3f }, new[] { 2.4f }, new[] { 3f }, new[] { 1.6f }, new[] { 0f }, new[] { 1.4f }, 1);
            bool climbed = false, sat = false;
            for (int k = 0; k < 3000 && !(climbed && sat); k++)
            {
                h.Step(0.1f, 100f, 100f, false);
                for (int i = 0; i < h.Count; i++)
                {
                    if (!h.Elevated[i] || h.IsRider(i)) continue;
                    if (h.Clip[i] == FaunaClip.Climb)
                    {
                        climbed = true;
                        Assert.AreEqual(3f, h.X[i], 0.35f, "climbing at the foot of the face");
                        Assert.AreEqual(1.6f, h.Z[i], 0.35f);
                        Assert.AreEqual(0f, h.Heading[i], 0.6f, "facing the wall (+Z)");
                    }
                    else if ((h.Clip[i] == FaunaClip.Sit || h.Clip[i] == FaunaClip.Groom) && Math.Abs(h.Y[i] - 1.4f) < 0.01f)
                    {
                        sat = true;
                        Assert.AreEqual(3f, h.X[i], 0.01f, "sitting on the spot on top");
                        Assert.AreEqual(2.4f, h.Z[i], 0.01f);
                    }
                }
            }
            Assert.IsTrue(climbed && sat);
        }

        // ------------------------------------------------------------------------------------------------------------
        // Pigeon and egret

        [Test]
        public void TheNearPigeonIsOneLoftWithAPupilAndANeckBand()
        {
            FaunaMesh f = FaunaMesher.Create(FaunaSpecies.Pigeon, 0);
            MeshData m = f.Mesh;
            int[] comp = Components(m);
            int body = -1, pupil = 0, iris = 0;
            float neckBaseY = f.Pivot[(int)FaunaBone.Neck].Y;
            float bodyY0 = float.MaxValue;
            for (int v = 0; v < m.VertexCount; v++)
                if ((FaunaPart)f.Part[v] == FaunaPart.Body)
                    bodyY0 = Math.Min(bodyY0, m.Positions[3 * v + 1]);
            for (int v = 0; v < m.VertexCount; v++)
            {
                var part = (FaunaPart)f.Part[v];
                Rgba(m, v, out int r, out int g, out int b, out _);
                if (part == FaunaPart.Body || part == FaunaPart.Neck || part == FaunaPart.Head)
                {
                    if (body < 0) body = comp[v];
                    Assert.AreEqual(body, comp[v], part + ": rump, breast, neck and head are one loft (no separate head, no collar)");
                }
                if (part == FaunaPart.Eye)
                {
                    if (r < 40 && g < 40 && b < 40) pupil++;
                    else if (r > 180 && g > 80 && b < 90) iris++;
                }
                // The sheen is a band round the neck: the lower breast and the belly are plain grey.
                if (part == FaunaPart.Body && m.Positions[3 * v + 1] < bodyY0 + 0.55f * (neckBaseY - bodyY0))
                {
                    int max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
                    Assert.LessOrEqual(max - min, 40, "no green or purple low on the breast (vertex " + v + ")");
                }
            }
            Assert.Greater(pupil, 1, "a black pupil");
            Assert.Greater(iris, 4, "in an orange iris");
            int sheen = 0;
            for (int v = 0; v < m.VertexCount; v++)
            {
                var part = (FaunaPart)f.Part[v];
                if (part != FaunaPart.Body && part != FaunaPart.Neck) continue;
                Rgba(m, v, out int r, out int g, out int b, out _);
                if (Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b)) > 45) sheen++;
            }
            Assert.GreaterOrEqual(sheen, 5, "the iridescent neck band");
            // Toes on the near bird.
            int toes = 0;
            for (int v = 0; v < m.VertexCount; v++)
                if ((FaunaPart)f.Part[v] == FaunaPart.Feet)
                    toes++;
            Assert.Greater(toes, 12);
        }

        [Test]
        public void TheEgretFliesWithItsNeckDrawnInAndItsLegsTrailing()
        {
            var md = new MeshData();
            foreach (FaunaClip clip in new[] { FaunaClip.Flap, FaunaClip.Glide })
                for (float t = 0f; t < 1f; t += 0.13f)
                {
                    FaunaMesh f = FaunaMesher.Create(FaunaSpecies.Egret, 0, CoatPattern.Plain);
                    FaunaAnimator.Evaluate(f, clip, t, 0f, 1u, Pose);
                    Skinner.Bake(f, Pose, md);
                    float footY0 = float.MaxValue, footY1 = float.MinValue, footZ1 = float.MinValue, headY = float.MinValue, headZ0 = float.MaxValue;
                    for (int v = 0; v < f.VertexCount; v++)
                    {
                        Fv3 p = Pos(md, v);
                        var bone = (FaunaBone)f.Bone0[v];
                        if (bone == FaunaBone.HindFootL || bone == FaunaBone.HindFootR)
                        {
                            footY0 = Math.Min(footY0, p.Y);
                            footY1 = Math.Max(footY1, p.Y);
                            footZ1 = Math.Max(footZ1, p.Z);
                        }
                        if ((FaunaPart)f.Part[v] == FaunaPart.Head)
                        {
                            headY = Math.Max(headY, p.Y);
                            headZ0 = Math.Min(headZ0, p.Z);
                        }
                    }
                    string what = clip + " t=" + t;
                    // The body is levelled and centred on the origin: the legs trail behind it at its height.
                    Assert.Less(footZ1, -0.2f, what + ": feet trailing behind the body");
                    Assert.That(footY0, Is.InRange(-0.12f, 0.08f), what + ": not hanging down");
                    Assert.That(footY1, Is.InRange(-0.08f, 0.1f), what + ": not sticking up over the back");
                    // The head rests just over the shoulders, in front: the neck is drawn in.
                    Assert.Less(headY, 0.14f, what + ": neck drawn in");
                    Assert.Greater(headZ0, 0.02f, what + ": head forward");
                }
        }
    }
}
