using System;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Synth.Look;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The occluder fade's sight gate (owner playtest #4 "buildings constantly flicker and stutter", #12 "make the buildings
    /// very solid"): it dissolves only what really hides the explorer for a sustained moment, with hysteresis and a smooth
    /// ramp, never what the explorer rides past or stands beside, and never flickers as the camera moves.
    /// </summary>
    public class WorldRenderingOccluderGateTests
    {
        private const float Dt = 1f / 30f;
        private static readonly OccluderFadeSettings S = OccluderFadeSettings.Default;
        private const double H = 1300.0;

        [Test]
        public void ABriefOcclusionNeverDissolvesAnything()
        {
            // A lamp post, a corner or a balcony crossing the line for less than the engage time: strength stays 0.
            var g = new OccluderFadeGate();
            // Flickering input every other frame (a fence's pickets): never engages.
            for (int k = 0; k < 300; k++) Assert.That(g.Step((k & 1) == 0, Dt), Is.EqualTo(0f));
            // Hidden for 0.3 s, clear for 0.1 s, repeatedly: each spell restarts the count, nothing dissolves.
            g.Reset();
            for (int k = 0; k < 300; k++) Assert.That(g.Step(k % 12 < 9, Dt), Is.EqualTo(0f));
        }

        [Test]
        public void ASustainedOcclusionFadesInAndOutSmoothly()
        {
            var g = new OccluderFadeGate();
            float prev = 0f;
            int k = 0;
            float t = 0f;
            // Hidden: nothing until the engage time, then a smooth ramp to full over the fade-in time.
            for (; t < S.EngageS + S.FadeInS + 0.2f; k++, t += Dt)
            {
                float s = g.Step(true, Dt);
                if (t + Dt < S.EngageS - 1e-4f) Assert.That(s, Is.EqualTo(0f), "before engaging, " + t);
                Assert.That(s, Is.GreaterThanOrEqualTo(prev), "monotonic in");
                Assert.That(s - prev, Is.LessThanOrEqualTo(1.5f * Dt / S.FadeInS + 1e-5f), "no pop in");
                prev = s;
            }
            Assert.That(g.Engaged, Is.True);
            Assert.That(g.Strength, Is.EqualTo(1f));
            // In sight again: stays until the release time, then a smooth ramp out.
            for (t = 0f; t < S.ReleaseS + S.FadeOutS + 0.2f; t += Dt)
            {
                float s = g.Step(false, Dt);
                if (t + Dt < S.ReleaseS - 1e-4f) Assert.That(s, Is.EqualTo(1f), "held through the release time, " + t);
                Assert.That(s, Is.LessThanOrEqualTo(prev), "monotonic out");
                Assert.That(prev - s, Is.LessThanOrEqualTo(1.5f * Dt / S.FadeOutS + 1e-5f), "no pop out");
                prev = s;
            }
            Assert.That(g.Engaged, Is.False);
            Assert.That(g.Strength, Is.EqualTo(0f));
        }

        [Test]
        public void OnceEngagedShortGlimpsesDoNotFlickerIt()
        {
            // Riding behind a row of houses with gaps: hidden 0.5 s, a 0.3 s glimpse, repeatedly. The fade engages once
            // and stays, no on/off.
            var g = new OccluderFadeGate();
            int toggles = 0;
            bool on = false;
            for (int k = 0; k < 600; k++)
            {
                bool hidden = (k % 24) < 15;
                float s = g.Step(hidden, Dt);
                bool now = s > 0.5f;
                if (now != on) toggles++;
                on = now;
            }
            Assert.That(toggles, Is.EqualTo(1));
        }

        [Test]
        public void PausedFramesAndHitchesDoNotSkipTheHysteresis()
        {
            var g = new OccluderFadeGate();
            for (int k = 0; k < 100; k++) Assert.That(g.Step(true, 0f), Is.EqualTo(0f), "a paused game changes nothing");
            Assert.That(g.Step(true, 2f), Is.EqualTo(0f), "one 2 s hitch counts as 0.25 s: not engaged yet");
            g.Step(true, 0.25f);
            Assert.That(g.Engaged, Is.True);
            Assert.That(g.Step(true, float.NaN), Is.EqualTo(g.Strength));
        }

        [Test]
        public void ACameraCutStartsOverSolid()
        {
            TileData t = DrivingCollisionTests.FlatTile(DrivingCollisionTests.House(100, 100, 120, 120));
            var q = new TileGroundQuery();
            q.Add(t);
            var g = new OccluderFadeGate();
            // Explorer behind the house (south of it), camera north of it: hidden, engaged after a second.
            for (int k = 0; k < 40; k++)
                g.Update(q, t.Tile.X0, 0.0, t.Tile.Z0, 110f, (float)H + 2.5f, 126f, 110f, (float)H + 1f, 95f, Dt, 0.33f, 16f / 9f);
            Assert.That(g.Strength, Is.EqualTo(1f));
            // The camera jumps 30 m (a teleport, a camera cut): solid at once.
            OccluderCapsule c = g.Update(q, t.Tile.X0, 0.0, t.Tile.Z0, 110f + 30f, (float)H + 2.5f, 126f, 110f, (float)H + 1f, 95f, Dt, 0.33f, 16f / 9f);
            Assert.That(c.Strength, Is.EqualTo(0f));
            Assert.That(g.Engaged, Is.False);
        }

        [Test]
        public void TheSightTestSeesHousesNotTheStreetWalls()
        {
            // A 6 m street running north between two rows of houses (x 94..100 and 106..112), a house across its end
            // (z 140..150) and a low 1 m garden wall across the street at z 125.
            TileData t = DrivingCollisionTests.FlatTile(
                DrivingCollisionTests.House(94, 100, 100, 135), DrivingCollisionTests.House(106, 100, 112, 135),
                DrivingCollisionTests.House(96, 140, 110, 150));
            var q = new TileGroundQuery();
            q.Add(t);
            var walls = new StructureColliders();
            walls.AddWall(new ColliderWall { X0 = 101, Z0 = 125, X1 = 105, Z1 = 125, HalfThickness = 0.1f, Bottom = (float)H - 0.3f, Top = (float)H + 1.0f });
            q.Register(t.Tile.Key, walls);
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            bool Hidden(double cx, double cy, double cz, double tx, double ty, double tz) =>
                OccluderFadeGate.IsHidden(q, S, x0 + cx, cy, z0 + cz, x0 + tx, ty, z0 + tz);
            double eye = H + 2.8, chest = H + 1.0;
            // Down the street with walls 2.6-3 m either side: in sight.
            Assert.That(Hidden(103, eye, 104, 103, chest, 112), Is.False, "chase camera in the street");
            // Close to one side wall (the camera squeezed against it): still in sight, the side wall is not in the way.
            Assert.That(Hidden(100.5, eye, 104, 101, chest, 112), Is.False, "camera beside a wall");
            // The explorer standing right against the house at the end: the wall they lean on does not hide them.
            Assert.That(Hidden(103, eye, 128, 103, chest, 139.7), Is.False, "explorer leaning on the end wall");
            // Behind the end house: hidden.
            Assert.That(Hidden(103, eye, 130, 103, chest, 155), Is.True, "explorer behind the house across the end");
            // Behind the 1 m garden wall: only the hips are hidden; the head is in sight.
            Assert.That(Hidden(103, H + 1.2, 118, 103, H + 0.6, 130), Is.False, "low garden wall");
            // Behind a row of houses (camera in the street, explorer in the yard behind): hidden.
            Assert.That(Hidden(103, eye, 110, 90, chest, 110), Is.True, "explorer behind the row");
            // No query: never hidden.
            Assert.That(OccluderFadeGate.IsHidden(null, S, 0, 0, 0, 10, 0, 0), Is.False);
        }

        [Test]
        public void TheConeLeavesTheWallsTheExplorerRidesPastAndStandsBeside()
        {
            // Full strength (the explorer hidden), a chase camera 8 m back and 2.8 m up: the cone clears the line of sight,
            // but a wall 1.4 m beside the explorer and walls 0.8 m beside the lens stay solid, and so does everything at
            // or beyond the explorer.
            OccluderCapsule c = OccluderCapsule.From(S, 0f, 2.8f, -8f, 0f, 1f, 0f, 0.33f, 16f / 9f);
            c.Strength = 1f;
            Assert.That(c.Opacity(0f, 2.35f, -6f, 0f, false), Is.EqualTo(S.MinOpacity).Within(1e-4f), "on the line");
            Assert.That(c.Opacity(1.4f, 1.2f, -0.5f, 0f, false), Is.EqualTo(1f), "the wall the explorer stands beside");
            Assert.That(c.Opacity(1.6f, 1.6f, -2f, 0f, false), Is.EqualTo(1f), "the wall the explorer rides past");
            Assert.That(c.Opacity(0.9f, 2.8f, -7.6f, 0f, false), Is.EqualTo(1f), "the wall beside the lens");
            Assert.That(c.Opacity(0f, 1f, 0.3f, 0f, false), Is.EqualTo(1f), "beyond the explorer");
            // Half strength: halfway between solid and the full fade, so the screen door thins smoothly.
            c.Strength = 0.5f;
            Assert.That(c.Opacity(0f, 2.35f, -6f, 0f, false), Is.EqualTo(1f - 0.5f * (1f - S.MinOpacity)).Within(1e-4f));
            c.Strength = 0f;
            Assert.That(c.Opacity(0f, 2.35f, -6f, 0f, false), Is.EqualTo(1f), "strength 0: nothing fades");
        }
    }
}
