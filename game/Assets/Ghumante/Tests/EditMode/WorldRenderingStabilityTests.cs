using System;
using System.Collections.Generic;
using System.IO;
using Ghumante.Core.Driving;
using Ghumante.Core.Synth.Look;
using Ghumante.World.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Ghumante.Tests.EditMode
{
    /// <summary>
    /// Owner playtest #4 and #12 ("buildings constantly flicker and stutter... make them stable and solid"): the building
    /// bands' stencil hand-off (every pixel of a band edge shows exactly one band's mesh) and the occluder fade's sight gate
    /// as the game runs it (<see cref="ToonLook.OccluderGate"/>, <see cref="OccluderFadeGate"/>). Nothing
    /// dissolves while the explorer is in plain view; a house that really hides the explorer fades in smoothly after a
    /// sustained moment and fades out after a sustained moment in sight (hysteresis), never flickering as the camera
    /// moves; the shader only clips while the gate is open. The full measurement on real streets is in
    /// core-tests/WorldRenderingOccluderSpotsTests.
    /// </summary>
    public class WorldRenderingStabilityTests
    {
        private const float Dt = 1f / 30f;

        /// <summary>Axis-aligned boxes (game metres) with a marching sphere sweep.</summary>
        private sealed class Boxes : IViewObstacleQuery
        {
            private readonly List<double[]> _b = new List<double[]>();

            public Boxes Add(double x0, double y0, double z0, double x1, double y1, double z1)
            {
                _b.Add(new[] { x0, y0, z0, x1, y1, z1 });
                return this;
            }

            private double Distance(double x, double y, double z)
            {
                double best = double.PositiveInfinity;
                foreach (double[] b in _b)
                {
                    double dx = Math.Max(Math.Max(b[0] - x, 0.0), x - b[3]);
                    double dy = Math.Max(Math.Max(b[1] - y, 0.0), y - b[4]);
                    double dz = Math.Max(Math.Max(b[2] - z, 0.0), z - b[5]);
                    best = Math.Min(best, Math.Sqrt(dx * dx + dy * dy + dz * dz));
                }
                return best;
            }

            public bool SphereCast(double ox, double oy, double oz, double dx, double dy, double dz, double radius, double maxDist, out double hitDist)
            {
                double t = 0.0;
                while (t <= maxDist)
                {
                    double d = Distance(ox + dx * t, oy + dy * t, oz + dz * t);
                    if (d <= radius + 1e-6)
                    {
                        hitDist = t;
                        return true;
                    }
                    t += Math.Max(0.002, d - radius);
                }
                hitDist = maxDist;
                return false;
            }
        }

        // A 6 m lane running north between two rows of houses (x -6..-3 and 3..6, z 0..40), a house across it at
        // z 20..26 from x -3 to 3 in the north half, the explorer's chest 1 m above the ground at y = 1.
        private static Boxes Lane()
        {
            return new Boxes().Add(-6, 0, 0, -3, 9, 40).Add(3, 0, 0, 6, 9, 40).Add(-3, 0, 20, 3, 9, 26);
        }

        private static OccluderCapsule Frame(OccluderFadeGate gate, IViewObstacleQuery q, Vector3 cam, Vector3 chest)
        {
            return gate.Update(q, 0.0, 0.0, 0.0, cam.x, cam.y, cam.z, chest.x, chest.y, chest.z, Dt, Mathf.Tan(31f * Mathf.Deg2Rad) / (16f / 9f), 16f / 9f);
        }

        [Test]
        public void RidingDownALaneNothingDissolves()
        {
            // A chase camera 8 m behind and 2.8 m above the explorer riding north along the lane, bobbing and swaying as a
            // camera does: in plain view the whole way, so the strength stays 0 every frame (the old capsule dissolved
            // the lane's walls next to the camera).
            var gate = new OccluderFadeGate();
            Boxes q = Lane();
            for (int k = 0; k < 120; k++)
            {
                float z = 9f + 6f * k * Dt; // 6 m/s, up to 1.5 m short of the house across the lane
                if (z > 18.5f) break;
                var chest = new Vector3(0.6f * Mathf.Sin(k * 0.2f), 1f, z);
                var cam = new Vector3(1.2f * Mathf.Sin(k * 0.37f), 2.8f + 0.2f * Mathf.Sin(k * 0.9f), z - 8f);
                OccluderCapsule c = Frame(gate, q, cam, chest);
                Assert.AreEqual(0f, c.Strength, "frame " + k);
                Assert.IsFalse(gate.Hidden, "frame " + k);
            }
        }

        [Test]
        public void AHouseThatHidesTheExplorerFadesInAfterAMomentAndOutAfterAMoment()
        {
            var gate = new OccluderFadeGate();
            OccluderFadeSettings s = gate.Settings;
            Boxes q = Lane();
            var cam = new Vector3(0f, 2.8f, 12f);
            var behind = new Vector3(0f, 1f, 30f); // behind the house across the lane
            var inView = new Vector3(0f, 1f, 19f); // in front of it (11 m away: not a cut)
            float t = 0f, prev = 0f;
            for (; t < s.EngageS + s.FadeInS + 0.3f; t += Dt)
            {
                OccluderCapsule c = Frame(gate, q, cam, behind);
                Assert.IsTrue(gate.Hidden);
                if (t + Dt < s.EngageS - 1e-4f) Assert.AreEqual(0f, c.Strength, "waits for a sustained moment, " + t);
                Assert.LessOrEqual(c.Strength - prev, 1.5f * Dt / s.FadeInS + 1e-5f, "smooth fade-in");
                prev = c.Strength;
            }
            Assert.AreEqual(1f, prev, 1e-5f, "fully faded");
            for (t = 0f; t < s.ReleaseS + s.FadeOutS + 0.3f; t += Dt)
            {
                OccluderCapsule c = Frame(gate, q, cam, inView);
                Assert.IsFalse(gate.Hidden);
                if (t + Dt < s.ReleaseS - 1e-4f) Assert.AreEqual(1f, c.Strength, 1e-5f, "holds through a short glimpse, " + t);
                Assert.LessOrEqual(prev - c.Strength, 1.5f * Dt / s.FadeOutS + 1e-5f, "smooth fade-out");
                prev = c.Strength;
            }
            Assert.AreEqual(0f, prev, "solid again");
        }

        [Test]
        public void ABriefOcclusionNeverFlickersTheFade()
        {
            // A pillar crossing the line of sight for a few frames, over and over: the gate never opens.
            var gate = new OccluderFadeGate();
            Boxes q = Lane().Add(-0.3, 0, 15.8, 0.3, 4, 16.2);
            var cam = new Vector3(0f, 2.8f, 8f);
            for (int k = 0; k < 300; k++)
            {
                // Behind the pillar for 6 frames (0.2 s) out of every 15.
                float x = (k % 15) < 6 ? 0f : 1.5f;
                OccluderCapsule c = Frame(gate, q, cam, new Vector3(x, 1f, 19f));
                Assert.AreEqual(0f, c.Strength, "frame " + k);
            }
        }

        [Test]
        public void ToonLookStartsSolidAndForwardsItsSettingsToTheGate()
        {
            ToonLook.EnsureInitialized();
            Assert.AreEqual(0f, Shader.GetGlobalVector(ToonLook.OccluderBId).w, "no fade before a world camera renders");
            Assert.AreEqual(0f, ToonLook.OccluderGate.Strength);
            OccluderFadeSettings saved = ToonLook.Occluder;
            try
            {
                Assert.AreEqual(0f, saved.NearCoverM, "the cone keeps only the line of sight (no frustum cover)");
                Assert.Greater(saved.EngageS, 0.2f, "a sustained moment before anything dissolves");
                Assert.Greater(saved.ReleaseS, saved.EngageS, "longer to release than to engage");
                OccluderFadeSettings tuned = saved;
                tuned.EngageS = 0.5f;
                ToonLook.Occluder = tuned;
                Assert.AreEqual(0.5f, ToonLook.OccluderGate.Settings.EngageS, 1e-6f);
            }
            finally
            {
                ToonLook.Occluder = saved;
            }
        }

        [Test]
        public void BandsHandOverByStencilFromInnerToOuter()
        {
            // Each band reads the marks of every band inside it and passes where none is set, marks its own bit (URP's
            // user bits 0-3; B3, the last, marks nothing) and draws after every band inside it, before the overlays.
            int inner = 0, queue = -1;
            for (int order = ToonLitLayout.BandOrderNear; order <= ToonLitLayout.BandOrderB3; order++)
            {
                int reference, read, write;
                ToonLitLayout.BandStencil(order, out reference, out read, out write);
                Assert.AreEqual(inner, read, "band " + order + " reads every inner band's mark");
                Assert.AreEqual(0, reference & read, "band " + order + " passes where no inner band drew");
                Assert.AreEqual(0, write & read, "band " + order + " never masks itself");
                Assert.AreEqual(0, write & ~0x0F, "band " + order + " marks only URP's user stencil bits");
                Assert.AreEqual(write, reference & write, "band " + order + " marks with its reference");
                int q = ToonLitLayout.BandQueue(order);
                Assert.Greater(q, queue, "band " + order + " draws after the bands inside it");
                Assert.Less(q, WorldMaterialDefaults.OverlayQueue, "band " + order + " is opaque geometry before the overlays");
                queue = q;
                inner |= write;
            }
            Assert.AreEqual(0x0F, inner, "near, lite, B1 and B2 each mark a bit");
        }

        [Test]
        public void ABandMaterialAndItsCopiesKeepTheirPlaceInTheOrder()
        {
            Shader shader = Shader.Find(WorldShaders.ToonLit);
            Assert.IsNotNull(shader);
            foreach (string p in new[] { "_BandStencilRef", "_BandStencilRead", "_BandStencilWrite", "_BandStencilComp", "_BandStencilPass" })
                Assert.GreaterOrEqual(shader.FindPropertyIndex(p), 0, p);
            var plain = new Material(shader);
            var lite = new Material(shader);
            try
            {
                // Materials outside the bands leave the stencil alone.
                Assert.AreEqual((float)UnityEngine.Rendering.CompareFunction.Always, plain.GetFloat(WorldShaders.BandStencilComp));
                Assert.AreEqual((float)UnityEngine.Rendering.StencilOp.Keep, plain.GetFloat(WorldShaders.BandStencilPass));
                Assert.AreEqual(0f, plain.GetFloat(WorldShaders.BandStencilWrite));
                WorldMaterialDefaults.SetBandOrder(lite, ToonLitLayout.BandOrderLite);
                Assert.AreEqual(2f, lite.GetFloat(WorldShaders.BandStencilRef));
                Assert.AreEqual(1f, lite.GetFloat(WorldShaders.BandStencilRead));
                Assert.AreEqual(2f, lite.GetFloat(WorldShaders.BandStencilWrite));
                Assert.AreEqual((float)UnityEngine.Rendering.CompareFunction.Equal, lite.GetFloat(WorldShaders.BandStencilComp));
                Assert.AreEqual((float)UnityEngine.Rendering.StencilOp.Replace, lite.GetFloat(WorldShaders.BandStencilPass));
                Assert.AreEqual(ToonLitLayout.BandQueue(ToonLitLayout.BandOrderLite), lite.renderQueue);
                // The streamer copies the band materials per range (new Material(source)): the copy keeps the order.
                var copy = new Material(lite);
                try
                {
                    Assert.AreEqual(lite.renderQueue, copy.renderQueue);
                    Assert.AreEqual(1f, copy.GetFloat(WorldShaders.BandStencilRead));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(copy);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(plain);
                UnityEngine.Object.DestroyImmediate(lite);
            }
        }

        [Test]
        public void TheBandShaderHandsOverInsteadOfDitheringBothSides()
        {
            string dir = Path.Combine(Application.dataPath, "Ghumante", "World", "Shaders");
            string hlsl = File.ReadAllText(Path.Combine(dir, "ToonLitInput.hlsl"));
            string shader = File.ReadAllText(Path.Combine(dir, "ToonLit.shader"));
            StringAssert.Contains("float fadeIn = _BandRange.x <= 0.0 ? 1.0 : d - (_BandRange.x - h);", hlsl, "the incoming band fills its whole fade-in");
            StringAssert.Contains("clip(min(fadeIn, fadeOut - GhBayer4(pixel)));", hlsl, "only the outgoing band dithers");
            StringAssert.Contains("clip(min(d - inner, (_BandRange.y + h) - d));", hlsl, "shadow casters overlap over the shared fade");
            Assert.AreEqual(2, System.Text.RegularExpressions.Regex.Matches(shader, @"Pass \[_BandStencilPass\]").Count, "both lit passes mark");
        }

        [Test]
        public void TheShaderClipsOnlyWhileTheGateIsOpen()
        {
            string hlsl = File.ReadAllText(Path.Combine(Application.dataPath, "Ghumante", "World", "Shaders", "ToonLitInput.hlsl"));
            StringAssert.Contains("float fade = side * cap * away * _GhOccluderB.w;", hlsl, "the strength scales the fade");
            StringAssert.Contains("if (_GhOccluderB.w > 0.001)", hlsl, "no clip at all while the strength is 0");
        }
    }
}
