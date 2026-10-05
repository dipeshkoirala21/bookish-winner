using System;
using Ghumante.World.Navigation;
using Ghumante.World.Rendering;
using Ghumante.World.Sky;
using NUnit.Framework;

namespace Ghumante.Tests.EditMode
{
    /// <summary>Engine-free parts of the sky, the route ribbon and the earth-curvature constant.</summary>
    public class WorldSkyRouteTests
    {
        [Test]
        public void EarthCurvatureMatchesTheArchitectureFigures()
        {
            Assert.AreEqual(7432833.3, EarthCurvature.EffectiveRadiusM, 1.0);
            Assert.AreEqual(1722.0, EarthCurvature.DropM(160000), 5.0, "about 1.7 km at 160 km (ARCHITECTURE 5.3)");
            Assert.AreEqual(0.067, EarthCurvature.DropM(1000), 0.001);
            // The shader constant GH_INV_TWO_R_EFF (GhumanteCommon.hlsl) is 6.7269e-8.
            Assert.AreEqual(6.7269e-8, EarthCurvature.InverseTwoEffectiveRadius, 1e-11);
        }

        [Test]
        public void SunRisesInTheEastAroundSixAndSetsInTheWest()
        {
            SkyState noon = SkyPalette.Evaluate(12.0);
            SkyState midnight = SkyPalette.Evaluate(0.0);
            Assert.Greater(noon.SunElevationDeg, 50.0);
            Assert.Less(midnight.SunElevationDeg, -40.0);
            Assert.IsFalse(noon.LightIsMoon);
            Assert.IsTrue(midnight.LightIsMoon);

            double rise = -1, set = -1;
            for (int m = 1; m < 24 * 60; m++)
            {
                double a = SkyPalette.Evaluate((m - 1) / 60.0).SunElevationDeg, b = SkyPalette.Evaluate(m / 60.0).SunElevationDeg;
                if (a < 0 && b >= 0) rise = m / 60.0;
                if (a >= 0 && b < 0) set = m / 60.0;
            }
            Assert.That(rise, Is.InRange(5.5, 6.5), "early October sunrise (solar time)");
            Assert.That(set, Is.InRange(17.5, 18.5));
            Assert.That(SkyPalette.Evaluate(rise).SunAzimuthDeg, Is.InRange(80.0, 110.0), "east");
            Assert.That(SkyPalette.Evaluate(set).SunAzimuthDeg, Is.InRange(250.0, 280.0), "west");
            Assert.That(SkyPalette.Evaluate(12.0).SunAzimuthDeg, Is.InRange(150.0, 210.0), "south at noon");
        }

        [Test]
        public void SunriseIsGoldenAndTheDayChangesSmoothly()
        {
            SkyState dawn = SkyPalette.Evaluate(6.0);
            SkyState noon = SkyPalette.Evaluate(12.0);
            Assert.Greater(dawn.Golden, 0.8f, "golden hour at sunrise");
            Assert.Less(noon.Golden, 0.01f);
            Assert.Greater(dawn.Light.R, dawn.Light.B + 0.2f, "warm alpenglow light");
            Assert.Greater(noon.Zenith.B, noon.Zenith.R, "blue midday sky");

            SkyState prev = SkyPalette.Evaluate(0.0);
            for (int m = 1; m <= 24 * 60; m++)
            {
                SkyState s = SkyPalette.Evaluate(m / 60.0);
                Assert.Less(Math.Abs(s.LightIntensity - prev.LightIntensity), 0.08f, "light jumps at minute " + m);
                Assert.Less(MaxDelta(s.Horizon, prev.Horizon), 0.06f, "horizon colour jumps at minute " + m);
                Assert.Less(MaxDelta(s.Fog, prev.Fog), 0.06f, "fog colour jumps at minute " + m);
                Assert.GreaterOrEqual(s.LightIntensity, 0f);
                prev = s;
            }
        }

        [Test]
        public void ValleyHazeKeepsTheHimalayaVisible()
        {
            SkyState noon = SkyPalette.Evaluate(12.0);
            Func<double, double> visible = d => Math.Exp(-Math.Pow(noon.FogDensity * d, 2));
            Assert.Greater(visible(1000), 0.99, "the city is crisp");
            Assert.That(visible(60000), Is.InRange(0.4, 0.8), "peaks 60 km away fade but show");
            Assert.That(visible(100000), Is.InRange(0.12, 0.45), "100 km: soft blue, still there");
        }

        [Test]
        public void RibbonResamplesAndKeepsItsWidth()
        {
            var b = new RibbonBuilder { WidthM = 2f, HoverM = 0.5f, MaxSegmentM = 4.0 };
            // 100 m east, then 50 m north (a right angle).
            double[] path = { 1000, 2000, 1100, 2000, 1100, 2050 };
            b.SetPath(path, 3);
            Assert.AreEqual(1000.0, b.AnchorX);
            Assert.AreEqual(150.0, b.LengthM, 1e-9);
            Assert.AreEqual(1 + 25 + 13, b.PointCount, "4 m pieces: 25 along the first leg, 13 along the second");
            for (int i = 1; i < b.PointCount; i++)
                Assert.LessOrEqual(b.DistanceAlong(i) - b.DistanceAlong(i - 1), 4.0 + 1e-9);

            for (int i = 0; i < b.PointCount; i++) b.SetHeight(i, 1300f);
            var pos = new float[b.VertexCount * 3];
            var uv = new float[b.VertexCount * 2];
            var vis = new byte[b.VertexCount];
            b.WriteVertices(pos, uv, vis);
            // Second point, on the straight east leg: left edge 1 m north, right edge 1 m south, at height + hover.
            Assert.AreEqual(4f, pos[6], 1e-4f);
            Assert.AreEqual(1300.5f, pos[7], 1e-4f);
            Assert.AreEqual(1f, pos[8], 1e-4f);
            Assert.AreEqual(-1f, pos[11], 1e-4f);
            Assert.AreEqual(0f, uv[4]);
            Assert.AreEqual(1f, uv[6]);
            Assert.AreEqual(4f, uv[5], 1e-4f, "v is metres along the route");
            Assert.AreEqual(255, vis[3]);
            // The corner is mitred: its offset is sqrt(2) times the half width.
            int corner = 25 * 6;
            double ox = pos[corner] - 100.0, oz = pos[corner + 2];
            Assert.AreEqual(Math.Sqrt(2.0), Math.Sqrt(ox * ox + oz * oz), 1e-3);

            var idx = new int[b.IndexCount];
            b.WriteIndices(idx);
            Assert.AreEqual((b.PointCount - 1) * 6, idx.Length);
            Assert.AreEqual(b.VertexCount - 1, idx[idx.Length - 1]);
        }

        [Test]
        public void RibbonFillsUnknownHeightsFromItsNeighbours()
        {
            var b = new RibbonBuilder { HoverM = 0f, MaxSegmentM = 10.0 };
            b.SetPath(new double[] { 0, 0, 40, 0 }, 2);
            Assert.AreEqual(5, b.PointCount);
            b.SetHeight(0, 100f);
            b.SetHeight(4, 140f);
            var pos = new float[b.VertexCount * 3];
            var uv = new float[b.VertexCount * 2];
            var vis = new byte[b.VertexCount];
            b.WriteVertices(pos, uv, vis);
            Assert.AreEqual(120f, pos[2 * 6 + 1], 1e-3f, "linear between known heights");
            Assert.AreEqual(0, vis[4], "unknown ground is faded out");
            Assert.AreEqual(255, vis[0]);
            b.ClearHeights();
            Assert.IsFalse(b.HasHeight(0));
        }

        private static float MaxDelta(SkyColor a, SkyColor b)
        {
            return Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B)));
        }
    }
}
