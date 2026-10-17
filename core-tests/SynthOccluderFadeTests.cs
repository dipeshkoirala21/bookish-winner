using System;
using Ghumante.Core.Synth.Look;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The occluder fade (World/README.md "Look", the owner's "camera view gets blocked if I try to back up my motorbike in
    /// the narrower streets"): the engine-free twin of the shader's cone clears the line from the camera to the player for
    /// long and short camera distances alike, from the lens on, while the player's body, the background, the side walls
    /// (beside the lens too, playtest #4) and the road surface stay. When it applies at all is the sight gate's decision
    /// (core-tests/WorldRenderingOccluderGateTests, WorldRenderingOccluderSpotsTests).
    /// </summary>
    public class SynthOccluderFadeTests
    {
        private static readonly OccluderFadeSettings S = OccluderFadeSettings.Default;
        private const float TanHalf60 = 0.57735027f;

        // A chase camera `length` metres from the player end, behind it along -z and up by a 20° pitch; the player end
        // (chest) at (0, 1, 0), feet at y = 0.
        private static OccluderCapsule Chase(float length, float aspect = 9f / 16f, float tanHalfFov = TanHalf60)
        {
            float pitch = 20f * (float)Math.PI / 180f;
            float cy = 1f + length * (float)Math.Sin(pitch), cz = -length * (float)Math.Cos(pitch);
            return OccluderCapsule.From(S, 0f, cy, cz, 0f, 1f, 0f, tanHalfFov, aspect);
        }

        // A point `fromCamera` metres from the camera along the view line, offset by (dx, dy) in the camera's right/up.
        private static float AlongLine(in OccluderCapsule c, float fromCamera, float dx = 0f, float dy = 0f, float normalY = 0f, bool ground = false)
        {
            float fx = c.Bx - c.Ax, fy = c.By - c.Ay, fz = c.Bz - c.Az;
            float len = (float)Math.Sqrt(fx * fx + fy * fy + fz * fz);
            fx /= len;
            fy /= len;
            fz /= len;
            // right = forward × up(0, 1, 0) = (-fz, 0, fx) normalised; up' = right × forward.
            float rx = -fz, rz = fx, rl = (float)Math.Sqrt(rx * rx + rz * rz);
            rx /= rl;
            rz /= rl;
            float ux = -rz * fy, uy = rz * fx - rx * fz, uz = rx * fy;
            float px = c.Ax + fx * fromCamera + rx * dx + ux * dy;
            float py = c.Ay + fy * fromCamera + uy * dy;
            float pz = c.Az + fz * fromCamera + rz * dx + uz * dy;
            return c.Opacity(px, py, pz, normalY, ground);
        }

        [Test]
        public void EverythingRightInFrontOfTheLensFades()
        {
            foreach (float length in new[] { 1.5f, 2f, 3f, 6f, 8f })
            {
                OccluderCapsule c = Chase(length);
                foreach (float d in new[] { 0.05f, 0.1f, 0.3f, 0.5f, 0.7f })
                {
                    if (d > length - 0.75f) continue; // the solid sphere around the player's chest
                    Assert.That(AlongLine(c, d), Is.EqualTo(S.MinOpacity).Within(1e-4f), "on the line " + d + " m from the camera, capsule " + length + " m");
                }
            }
        }

        [Test]
        public void WallsBesideTheLensStaySolid()
        {
            // Playtest #4: the old capsule covered the whole view frustum in front of the lens, so every wall the camera
            // passed close to (a galli, the gap between two houses at Hiti Pokhari) dithered. The cone keeps only the line
            // of sight: 0.9 m beside the lens, or at the screen corners 2 m ahead, a wall stays solid.
            foreach (float aspect in new[] { 9f / 16f, 16f / 9f, 20f / 9f })
            foreach (float length in new[] { 2f, 6f, 8f })
            {
                OccluderCapsule c = Chase(length, aspect);
                Assert.That(c.CameraRadius, Is.EqualTo(S.CameraRadiusM).Within(1e-6f), "no frustum cover");
                Assert.That(AlongLine(c, 0.3f, 0.9f, 0f), Is.EqualTo(1f), "0.9 m beside the lens, aspect " + aspect + ", capsule " + length);
                Assert.That(AlongLine(c, 0.3f, 0f, 0.9f), Is.EqualTo(1f), "0.9 m above the lens");
                float far = 2f;
                if (far > length - 1f) continue;
                float u = TanHalf60 * aspect, v = TanHalf60;
                Assert.That(AlongLine(c, far, u * far, v * far), Is.EqualTo(1f), "the screen corner 2 m ahead, aspect " + aspect);
            }
        }

        [Test]
        public void ShortCapsulesStillClearTheView()
        {
            // Backing a motorbike down a galli: the collision-aware camera ends up 1.5-3 m from the rider.
            foreach (float length in new[] { 1.5f, 2f, 3f })
            {
                OccluderCapsule c = Chase(length);
                Assert.That(AlongLine(c, 0.5f * length), Is.EqualTo(S.MinOpacity).Within(1e-4f), "halfway, capsule " + length);
                Assert.That(AlongLine(c, 0.5f * length, 0.4f, 0.2f), Is.EqualTo(S.MinOpacity).Within(1e-4f), "eave beside the line, capsule " + length);
                float worst = 0f;
                for (float d = 0.02f; d < length - 0.8f; d += 0.02f) worst = Math.Max(worst, AlongLine(c, d));
                Assert.That(worst, Is.EqualTo(S.MinOpacity).Within(1e-4f), "the whole line up to the player's body, capsule " + length);
            }
        }

        [Test]
        public void AWallBehindTheRiderFades()
        {
            // The rear of the bike points at the camera: a wall 1 to 1.5 m before the rider (and 0.3 m to the side) must go.
            foreach (float length in new[] { 2.5f, 4f, 6f, 8f })
            {
                OccluderCapsule c = Chase(length);
                foreach (float before in new[] { 1.0f, 1.25f, 1.5f })
                {
                    if (before > length - 0.05f) continue;
                    Assert.That(AlongLine(c, length - before), Is.EqualTo(S.MinOpacity).Within(1e-4f), before + " m before the rider, capsule " + length);
                    Assert.That(AlongLine(c, length - before, 0.3f), Is.EqualTo(S.MinOpacity).Within(1e-4f), before + " m before, 0.3 m aside");
                }
            }
        }

        [Test]
        public void ThePlayerTheBackgroundAndTheSidesStay()
        {
            OccluderCapsule c = Chase(6f);
            Assert.That(c.Opacity(0f, 1f, 0f, 0f, false), Is.EqualTo(1f), "the player's chest");
            Assert.That(c.Opacity(0.3f, 1f, 0f, 0f, false), Is.EqualTo(1f), "beside the player's body");
            Assert.That(c.Opacity(0f, 0.75f, -0.2f, 0f, false), Is.EqualTo(1f), "the player's seat");
            foreach (float beyond in new[] { 0.02f, 0.5f, 1f, 3f })
                Assert.That(AlongLine(c, 6f + beyond), Is.EqualTo(1f), "the background " + beyond + " m beyond the player");
            Assert.That(AlongLine(c, 6f + 0.05f, 1.0f), Is.EqualTo(1f), "the wall the player stands in front of");
            Assert.That(AlongLine(c, 3f, 2.4f), Is.EqualTo(1f), "the walls of a 4.8 m street corridor");
            Assert.That(AlongLine(c, -2.5f), Is.EqualTo(1f), "behind the camera");
            // Solid near the player, gone just outside the solid sphere.
            Assert.That(AlongLine(c, 6f - 0.3f), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(AlongLine(c, 6f - 0.8f), Is.EqualTo(S.MinOpacity).Within(1e-4f));
        }

        [Test]
        public void CapsuleScalesWithItsLengthAndTheView()
        {
            OccluderCapsule shortOne = Chase(1.5f), longOne = Chase(8f);
            Assert.That(shortOne.Soft, Is.LessThan(longOne.Soft), "soft edges scale with the capsule");
            Assert.That(shortOne.Soft, Is.GreaterThanOrEqualTo(S.SoftMinM).And.LessThanOrEqualTo(0.2f));
            Assert.That(longOne.Soft, Is.EqualTo(S.SoftMaxM).Within(1e-6f));
            Assert.That(shortOne.SolidRadius, Is.LessThanOrEqualTo(0.25f * 1.5f + 1e-5f), "the solid sphere never takes more than a quarter");
            Assert.That(longOne.SolidRadius, Is.EqualTo(S.SolidRadiusM));
            Assert.That(Chase(6f, 9f / 16f).CameraRadius, Is.EqualTo(S.CameraRadiusM), "portrait: the line of sight only");
            OccluderCapsule wide = Chase(6f, 20f / 9f, (float)Math.Tan(37.5 * Math.PI / 180.0));
            Assert.That(wide.CameraRadius, Is.EqualTo(S.CameraRadiusM), "a wide landscape view does not widen the camera end");
            // The optional frustum cover still works when asked for (tuning), bounded by the maximum.
            OccluderFadeSettings cover = S;
            cover.NearCoverM = 0.8f;
            OccluderCapsule covered = OccluderCapsule.From(cover, 0f, 3f, -6f, 0f, 1f, 0f, (float)Math.Tan(37.5 * Math.PI / 180.0), 20f / 9f);
            Assert.That(covered.CameraRadius, Is.GreaterThan(S.CameraRadiusM));
            Assert.That(covered.CameraRadius, Is.LessThanOrEqualTo(S.MaxCameraRadiusM));
            Assert.That(longOne.Strength, Is.EqualTo(1f), "From gives full strength; the gate scales it");
            Assert.That(longOne.Length, Is.EqualTo(8f).Within(1e-4f));
            Assert.That(longOne.KeepY, Is.EqualTo(S.GroundKeepM).Within(1e-5f), "feet at y = 0");
            Assert.That(longOne.OverheadY, Is.EqualTo(1f + S.OverheadM).Within(1e-5f));
        }

        [Test]
        public void GroundLayersKeepTheRoadButDropStructures()
        {
            OccluderCapsule c = Chase(6f);
            float Ground(float x, float y, float z, float normalY) => c.Opacity(x, y, z, normalY, true);
            // The road surface, a kerb face and a footpath stay, however close to the line.
            Assert.That(Ground(0f, 0f, -1.5f, 1f), Is.EqualTo(1f), "road under the rider");
            Assert.That(Ground(0.4f, 0.15f, -1.5f, 0f), Is.EqualTo(1f), "kerb face");
            Assert.That(Ground(0f, 0.9f, -4f, 1f), Is.EqualTo(1f), "the road climbing towards the camera");
            // Bridge railings, piers and parapets between camera and rider dissolve from the knees up.
            Assert.That(Ground(0f, 1.3f, -2f, 0f), Is.EqualTo(S.MinOpacity).Within(1e-4f), "railing post");
            Assert.That(Ground(0f, 1.6f, -3f, -1f), Is.EqualTo(S.MinOpacity).Within(1e-4f), "a beam's underside");
            // An upward-facing deck overhead (a flyover between a raised camera and the rider below) dissolves too.
            Assert.That(Ground(0f, 2.2f, -3.3f, 1f), Is.EqualTo(S.MinOpacity).Within(1e-4f), "deck top overhead");
            // A point below the knees inside the cone: kept on a ground layer, fading on a structure layer.
            Assert.That(Ground(0f, 0.4f, -1.2f, 0f), Is.EqualTo(1f), "ground layer below the knees");
            Assert.That(c.Opacity(0f, 0.4f, -1.2f, 0f, false), Is.LessThan(1f), "structure layer");
        }

        [Test]
        public void OpacityStaysInRangeAndIsContinuous()
        {
            OccluderCapsule c = Chase(4f);
            var rng = new Random(7);
            for (int i = 0; i < 20000; i++)
            {
                float x = (float)(rng.NextDouble() * 8 - 4), y = (float)(rng.NextDouble() * 6 - 1), z = (float)(rng.NextDouble() * 10 - 7);
                float ny = (float)(rng.NextDouble() * 2 - 1);
                bool g = (i & 1) == 0;
                float o = c.Opacity(x, y, z, ny, g);
                Assert.That(o, Is.InRange(S.MinOpacity - 1e-5f, 1f + 1e-5f));
                // A 1 mm step never changes the opacity by more than the steepest ramp allows (no hard cuts).
                float o2 = c.Opacity(x + 0.001f, y + 0.001f, z + 0.001f, ny, g);
                Assert.That(Math.Abs(o2 - o), Is.LessThan(0.05f), "continuous at " + x + ", " + y + ", " + z);
            }
        }
    }
}
