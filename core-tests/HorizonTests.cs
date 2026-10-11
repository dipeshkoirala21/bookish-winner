using System;
using System.Collections.Generic;
using System.Diagnostics;
using Ghumante.Core.Geo;
using Ghumante.Core.Horizon;
using Ghumante.Core.Meshing;
using Ghumante.Core.Streaming;
using Ghumante.World.Rendering;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The distant-mountains layer (Core/Horizon): curated peaks, the horizon height field from the sample pack, the
    /// panorama ray-marcher, the ring mesher's per-tier cost, the rim hills, rebuild tolerance, haze continuity at the
    /// handover, determinism and allocation-free rebuilds. The headline check (every peak above the rim drawn true from
    /// Basantapur, Boudha and Swayambhu) is <see cref="HorizonReproTests"/>.
    /// </summary>
    [TestFixture]
    public class HorizonTests
    {
        private static readonly int[] Tiers = { 0, 1, 2 };

        /// <summary>Sky + water share of the visible-triangle budget per tier (W2_DESIGN 10.4): 4 k / 10 k / 20 k.</summary>
        private static readonly int[] SkySlice = { 4000, 10000, 20000 };

        [Test]
        public void PeakTableIsCurated()
        {
            var ids = new HashSet<string>();
            for (int i = 0; i < HorizonPeaks.Count; i++)
            {
                HorizonPeak p = HorizonPeaks.At(i);
                Assert.IsTrue(ids.Add(p.Id), "unique id " + p.Id);
                Assert.That(p.Lat, Is.InRange(27.0, 29.0), p.Id);
                Assert.That(p.Lon, Is.InRange(84.0, 87.5), p.Id);
                Assert.IsFalse(string.IsNullOrEmpty(p.NameEn) || string.IsNullOrEmpty(p.NameNe), p.Id);
                if (p.Kind == HorizonPeakKind.SnowPeak) Assert.That(p.HeightM, Is.InRange(6000.0, 8850.0), p.Id);
                else Assert.That(p.HeightM, Is.InRange(1800.0, 3000.0), p.Id);
            }
            Assert.AreEqual(8849, HorizonPeaks.ById("everest").HeightM, 0.5);
            Assert.AreEqual(7234, HorizonPeaks.ById("langtang_lirung").HeightM, 0.5);
            Assert.AreEqual(2732, HorizonPeaks.ById("shivapuri").HeightM, 0.5);
            Assert.Throws<ArgumentException>(() => HorizonPeaks.ById("k2"));
        }

        [Test]
        public void CurvatureIsTheShadersCurvature()
        {
            Assert.AreEqual(EarthCurvature.InverseTwoEffectiveRadius, HorizonPanorama.InverseTwoEffectiveRadius, 1e-18);
        }

        /// <summary>The ray-marcher on a made-up field: one pyramid 60 km north-east of the eye on flat ground is seen at
        /// its bearing and at its analytic elevation (curvature included), in the band its distance falls in.</summary>
        [Test]
        public void PanoramaFindsAPyramidAtItsBearingAndElevation()
        {
            const double s = 100.0;
            int n = 2001;
            var h = new float[n * n];
            // A flat-topped pyramid (plateau 1 km across at 6 300 m, 45 degree faces) 70 km north-east of the eye.
            double px = 100000 + 70000 * Math.Sin(Math.PI / 4), pz = 100000 + 70000 * Math.Cos(Math.PI / 4);
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    double x = i * s, z = j * s, r = Math.Max(Math.Abs(x - px), Math.Abs(z - pz));
                    h[j * n + i] = (float)Math.Max(1300.0, 6300.0 - Math.Max(0.0, r - 500.0));
                }
            var field = new HorizonHeightField(0, 0, s, n, n, h, false);
            var settings = HorizonSettings.ForTier(2);
            var p = new HorizonPanorama();
            p.Build(field, 100000, 100000, 1302, settings);
            int ray = (int)Math.Round(45.0 / p.AzimuthStepDeg);
            double edge = 70000 - 500 * Math.Sqrt(2.0); // the plateau's near corner on the diagonal
            double expected = Math.Atan((6300 - 1302 - edge * edge * HorizonPanorama.InverseTwoEffectiveRadius) / edge) * 180 / Math.PI;
            Assert.AreEqual(expected, p.ElevationDeg(2, ray), 0.03, "summit elevation, band 60-100 km");
            float tan, dist, alt, steep;
            Assert.IsTrue(p.TryGet(2, ray, out tan, out dist, out alt, out steep));
            Assert.AreEqual(edge, dist, 400);
            Assert.AreEqual(6300, alt, 30);
            Assert.IsTrue(double.IsNaN(p.ElevationDeg(2, ray + p.Azimuths / 8)) || p.ElevationDeg(2, ray + p.Azimuths / 8) < 0.0,
                          "45 degrees off the pyramid there is only the flat valley");
        }

        [Test]
        public void SummitsStandAtTheirPublishedHeights()
        {
            HorizonHeightField f = HorizonFields.For(128);
            Assert.NotNull(f, "the sample pack has horizon tiles");
            int inside = 0, synthetic = 0;
            for (int c = 0; c < f.ConeCount; c++)
            {
                HorizonCone cone = f.Cone(c);
                float h;
                Assert.IsTrue(f.TryHeight(cone.X, cone.Z, out h));
                Assert.AreEqual(cone.HeightM, h, 1.0, "summit " + c);
                if (cone.Synthetic) synthetic++;
                else inside++;
            }
            Assert.GreaterOrEqual(inside, 15, "the Ganesh, Langtang, Jugal and rim summits lie on the sample's horizon tiles");
            Assert.GreaterOrEqual(synthetic, 2, "peaks beyond the sample's horizon tiles are synthetic massifs");
            foreach (string id in new[] { "cho_oyu", "everest" })
            {
                double px, pz;
                HorizonPeaks.ById(id).GamePosition(out px, out pz);
                bool found = false;
                for (int c = 0; c < f.ConeCount; c++)
                {
                    HorizonCone cone = f.Cone(c);
                    if (Math.Abs(cone.X - px) < 1 && Math.Abs(cone.Z - pz) < 1) found = cone.Synthetic;
                }
                Assert.IsTrue(found, id + " (beyond 86.6 E) stands as a synthetic massif");
            }
            // The DEM alone holds them low (GLO-30 infill): the reason for the cones.
            float g;
            double x, z;
            HorizonPeaks.ById("gauri_shankar").GamePosition(out x, out z);
            Assert.IsTrue(f.TryGridHeight(x, z, out g));
            Assert.Less(g, 7134 - 100, "GLO-30 holds Gauri Shankar 200+ m low");
        }

        /// <summary>A 256 m grid (Low) keeps every summit: the highest of the four 128 m samples of each cell.</summary>
        [Test]
        public void CoarseGridKeepsTheSummits()
        {
            HorizonHeightField fine = HorizonFields.For(128), coarse = HorizonFields.For(256);
            Assert.Less(coarse.Bytes, fine.Bytes / 3);
            foreach (string id in new[] { "langtang_lirung", "ganesh_1", "dorje_lakpa", "shivapuri" })
            {
                double x, z;
                HorizonPeaks.ById(id).GamePosition(out x, out z);
                float best128 = float.MinValue, best256 = float.MinValue;
                for (double dz = -1500; dz <= 1500; dz += 32)
                    for (double dx = -1500; dx <= 1500; dx += 32)
                    {
                        float a, b;
                        if (fine.TryGridHeight(x + dx, z + dz, out a)) best128 = Math.Max(best128, a);
                        if (coarse.TryGridHeight(x + dx, z + dz, out b)) best256 = Math.Max(best256, b);
                    }
                Assert.GreaterOrEqual(best256, best128 - 1.0f, id);
            }
        }

        /// <summary>Per-tier triangle cost of the ring from the three viewpoints: within the tier's cap and well inside the
        /// sky + water slice.</summary>
        [Test]
        public void RingFitsTheTierBudget()
        {
            foreach (int tier in Tiers)
            {
                HorizonSettings settings = HorizonSettings.ForTier(tier);
                Assert.LessOrEqual(settings.TriangleCap, SkySlice[tier] * 3 / 4, "the cap leaves room for water");
                foreach (HorizonViewpoint v in HorizonViewpoint.All)
                {
                    var scene = new HorizonScene(v, tier);
                    int tris = scene.Ring.TriangleCount;
                    TestContext.WriteLine("tier " + tier + " " + v.Name + ": " + tris + " triangles, " + scene.Ring.VertexCount + " vertices");
                    Assert.LessOrEqual(tris, settings.TriangleCap, "tier " + tier + " " + v.Name);
                    Assert.Greater(tris, settings.TriangleCap / 6, "a real panorama, not a stub");
                }
            }
        }

        /// <summary>The ring stays true while the camera moves until the next rebuild: built the rebuild distance away
        /// in four directions, every peak above the rim is still at its bearing and elevation.</summary>
        [Test]
        public void PeaksStayTrueUntilTheNextRebuild()
        {
            var failures = new List<string>();
            foreach (int tier in new[] { 0, 2 })
            {
                double offset = HorizonSettings.ForTier(tier).RebuildDistanceM;
                foreach (HorizonViewpoint v in HorizonViewpoint.All)
                    for (double b = 45; b < 360; b += 90)
                    {
                        var scene = new HorizonScene(v, tier, 9.0, true, offset, b);
                        foreach (HorizonReproTests.TruePeak p in HorizonReproTests.VisibleFrom(v))
                        {
                            string why = HorizonReproTests.CheckDrawn(scene, p, scene.DrawnRay);
                            if (why != null) failures.Add("tier " + tier + " " + v.Name + " built " + offset + " m at " + b + ": " + why);
                        }
                    }
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        /// <summary>The green rim of the valley at its true places and heights from Basantapur and Boudha: Shivapuri to the
        /// north, Phulchoki to the south-east, Chandragiri to the south-west, Nagarjun to the north-west.</summary>
        [Test]
        public void RimHillsAtTheirTrueBearingsAndHeights()
        {
            var failures = new List<string>();
            var rims = new[]
            {
                new HorizonReproTests.TruePeak("Shivapuri", 27.81240, 85.38460, 2732), new HorizonReproTests.TruePeak("Phulchoki", 27.57100, 85.40675, 2782),
                new HorizonReproTests.TruePeak("Chandragiri", 27.66625, 85.20525, 2551), new HorizonReproTests.TruePeak("Nagarjun", 27.74525, 85.25925, 2128),
            };
            foreach (int tier in Tiers)
                foreach (HorizonViewpoint v in new[] { HorizonViewpoint.Basantapur, HorizonViewpoint.Boudha })
                {
                    var scene = new HorizonScene(v, tier);
                    foreach (HorizonReproTests.TruePeak p in rims)
                    {
                        string why = CheckRim(scene, p);
                        if (why != null) failures.Add("tier " + tier + " " + v.Name + ": " + why);
                    }
                }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        /// <summary>
        /// A rim hill is broad and near, so the window test of the snow peaks does not fit it (its own nearer flank can
        /// stand higher in angle a degree off the summit's bearing). At the summit's exact bearing the drawn skyline must
        /// reach the summit's true elevation (within 0.1 deg) on the hill itself (no farther than the summit + 1.5 km),
        /// and nothing within +-2 deg may stand more than 0.25 deg above it.
        /// </summary>
        private static string CheckRim(HorizonScene scene, HorizonReproTests.TruePeak p)
        {
            double px, pz, bearing, dist, elev;
            WorldFrame.LonLatToGame(p.Lon, p.Lat, out px, out pz);
            scene.Sight(px, pz, p.HeightM, out bearing, out dist, out elev);
            string head = string.Format("{0} (bearing {1:0.0}, {2:0.0} km, true elevation {3:0.00} deg)", p.Name, bearing, dist / 1000.0, elev);
            HorizonRay at = scene.DrawnRay(bearing);
            if (!at.Hit) return head + ": nothing drawn at its bearing";
            if (at.ElevationDeg < elev - 0.1)
                return head + string.Format(": drawn {0:0.00} deg ({1:0} m) at its bearing", at.ElevationDeg, at.AltitudeM);
            if (at.DistanceM > dist + 1500.0)
                return head + string.Format(": the skyline at its bearing lies {0:0.0} km away, behind the hill", at.DistanceM / 1000.0);
            for (double b = bearing - 2.0; b <= bearing + 2.0 + 1e-9; b += 0.05)
            {
                HorizonRay r = scene.DrawnRay((b + 360.0) % 360.0);
                if (r.Hit && Math.Abs(r.DistanceM - dist) < 15000.0 && r.ElevationDeg > elev + 0.25)
                    return head + string.Format(": a spurious ridge at {0:0.00} deg, bearing {1:0.0}, {2:0.0} km", r.ElevationDeg, b, r.DistanceM / 1000.0);
            }
            return null;
        }

        /// <summary>
        /// The ring replaces streamed terrain only beyond the handover, where the scene's fog has already taken most of it
        /// (so the swap is not a visible change), inside the camera's far clip; and for low sight lines its haze is the
        /// scene fog exactly, so a ridge crossing the handover keeps its colour.
        /// </summary>
        [Test]
        public void HandoverIsSeamless()
        {
            foreach (int tier in Tiers)
            {
                HorizonSettings s = HorizonSettings.ForTier(tier);
                StreamingConfig c = StreamingConfig.ForTier(tier);
                double far = c.ViewRadiusM * 1.25 + 5000.0; // WorldRoot.ConfigureCamera
                double floor = Math.Sqrt(Math.Log(1.0 / 0.04)) / c.ViewRadiusM; // WorldSky.FogDensityFloor
                double rho = Math.Max(1.2e-5, floor);
                double t = Math.Exp(-(rho * s.HandoverM) * (rho * s.HandoverM));
                Assert.LessOrEqual(t, 0.6, "tier " + tier + ": streamed terrain at the handover is mostly haze");
                Assert.Less(s.HandoverM * 1.02, far, "tier " + tier + ": the ring is drawn inside the far clip");
                Assert.Less(s.HandoverM, c.ViewRadiusM, "tier " + tier + ": the ring takes over inside the streamed world");
                double low = HorizonLook.HazeTransmittance(s.HandoverM, 1300, 2200, rho, 1.2e-5);
                Assert.AreEqual(t, low, 1e-9, "low sight lines: the scene fog");
                double peak = HorizonLook.HazeTransmittance(65000, 1300, 7234, rho, 1.2e-5);
                Assert.Greater(peak, Math.Exp(-(rho * 65000) * (rho * 65000)), "sight lines to the snow peaks run above the valley haze");
                Assert.GreaterOrEqual(peak, HorizonLook.HazeTransmittance(65000, 1300, 7234, rho, rho) - 1e-12,
                                      "and are hazed by the air, not by the floor that hides the end of the streamed world");
            }
        }

        [Test]
        public void RingIsDeterministicAndRebuildsWithoutAllocating()
        {
            HorizonHeightField f = HorizonFields.For(128);
            HorizonSettings s = HorizonSettings.ForTier(2);
            double x, z;
            WorldFrame.LonLatToGame(HorizonViewpoint.Basantapur.Lon, HorizonViewpoint.Basantapur.Lat, out x, out z);
            var p = new HorizonPanorama();
            var mesher = new HorizonMesher();
            var a = new MeshData();
            var b = new MeshData();
            p.Build(f, x, z, 1320, s);
            mesher.Build(p, s, a);
            p.Build(f, x + 300, z - 200, 1340, s); // warm both with another eye
            mesher.Build(p, s, b);
            long before = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            p.Build(f, x, z, 1320, s);
            mesher.Build(p, s, b);
            sw.Stop();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            TestContext.WriteLine("High rebuild: " + sw.Elapsed.TotalMilliseconds.ToString("0") + " ms, " + p.Samples + " samples, " + allocated + " bytes");
            Assert.Less(allocated, 1024, "a warm rebuild allocates nothing");
            Assert.AreEqual(a.VertexCount, b.VertexCount);
            Assert.AreEqual(a.IndexCount, b.IndexCount);
            for (int i = 0; i < a.VertexCount * 3; i++) Assert.AreEqual(a.Positions[i], b.Positions[i]);
            for (int i = 0; i < a.IndexCount; i++) Assert.AreEqual(a.Indices[i], b.Indices[i]);
            for (int i = 0; i < a.VertexCount * 4; i++) Assert.AreEqual(a.Colors[i], b.Colors[i]);
        }

        /// <summary>Build cost per tier (worker thread, every few hundred metres of travel): report and keep it bounded.</summary>
        [Test]
        public void RebuildCostPerTier()
        {
            double x, z;
            WorldFrame.LonLatToGame(HorizonViewpoint.Boudha.Lon, HorizonViewpoint.Boudha.Lat, out x, out z);
            foreach (int tier in Tiers)
            {
                HorizonSettings s = HorizonSettings.ForTier(tier);
                HorizonHeightField f = HorizonFields.For(s.GridSpacingM);
                var p = new HorizonPanorama();
                var mesher = new HorizonMesher();
                var m = new MeshData();
                p.Build(f, x, z, 1340, s);
                mesher.Build(p, s, m);
                var sw = Stopwatch.StartNew();
                p.Build(f, x + 250, z, 1340, s);
                mesher.Build(p, s, m);
                sw.Stop();
                TestContext.WriteLine("tier " + tier + ": " + sw.Elapsed.TotalMilliseconds.ToString("0") + " ms, " + p.Samples + " height samples, " +
                                      m.TriangleCount + " triangles, field " + f.Bytes / 1024 + " KB, tolerance " + mesher.ToleranceUsedDeg.ToString("0.000") + " deg");
                Assert.Less(sw.Elapsed.TotalMilliseconds, 1500, "tier " + tier);
                Assert.Less(p.Samples, 3000000, "tier " + tier);
            }
        }

        [Test]
        public void SettingsValidate()
        {
            foreach (int tier in Tiers) HorizonSettings.ForTier(tier).Validate();
            Assert.Throws<ArgumentOutOfRangeException>(() => HorizonSettings.ForTier(3));
            var bad = HorizonSettings.ForTier(1);
            bad.BandEdgesM = new double[] { 5000, 4000 };
            Assert.Throws<ArgumentException>(() => bad.Validate());
        }

        /// <summary>The look: snow above the snow line, rock below it, forest at the rim; sunlit snow brighter than its
        /// shaded flank and both still bright through the clear air above the haze; the rib detail breaks the snow with
        /// rock and fades out when a rib would be narrower than three pixels.</summary>
        [Test]
        public void LookFollowsAltitudeAndLight()
        {
            float r, g, b;
            int white = 0, n = 0;
            for (double along = 0; along < 20000; along += 37, n++)
            {
                HorizonLook.Albedo(7000, 0.1, along, 1.0, out r, out g, out b);
                if (r > 0.8f) white++;
            }
            Assert.Greater(white, n * 0.55, "snow: mostly white above the snow line");
            HorizonLook.Albedo(4300, 0.3, 0.0, 0.0, out r, out g, out b);
            Assert.Less(r, 0.2f, "rock below the snow line");
            HorizonLook.Albedo(2400, 0.0, 0.0, 1.0, out r, out g, out b);
            Assert.Greater(g, r, "green forest on the rim");
            int rocky = 0, samples = 0;
            for (double along = 0; along < 20000; along += 37)
            {
                HorizonLook.Albedo(6200, 0.8, along, 1.0, out r, out g, out b);
                samples++;
                if (r < 0.5f) rocky++;
            }
            Assert.That(rocky / (double)samples, Is.InRange(0.08, 0.5), "steep snow faces show rock ribs, but stay mostly white");
            Assert.AreEqual(1.0, HorizonLook.Detail(15), 1e-9, "10 pixels per rib: all the detail");
            Assert.AreEqual(0.0, HorizonLook.Detail(60), 1e-9, "2.5 pixels per rib: none (no shimmer)");
            Assert.Greater(HorizonLook.ClearAirTransmittance(70000, 1300, 7000), HorizonLook.ClearAirTransmittance(70000, 1300, 3000),
                           "thinner air on the sight lines to the summits");
            var light = new HorizonLight
            {
                LightX = 0.6f, LightY = 0.6f, LightZ = -0.53f, LightR = 1f, LightG = 1f, LightB = 0.95f, AmbientR = 0.3f, AmbientG = 0.4f, AmbientB = 0.6f,
                FogR = 0.4f, FogG = 0.55f, FogB = 0.75f, HorizonR = 0.45f, HorizonG = 0.7f, HorizonB = 0.92f, FogDensity = 1.5e-5f,
            };
            float lr, lg, lb, sr, sg, sb;
            HorizonLook.Shade(7000, 0.1, 0.0, 0.0, 0.6, 0.6, -0.53, 65000, 1300, light, out lr, out lg, out lb);
            HorizonLook.Shade(7000, 0.1, 0.0, 0.0, -0.6, 0.3, 0.75, 65000, 1300, light, out sr, out sg, out sb);
            Assert.Greater(lr + lg + lb, sr + sg + sb + 0.1f, "lit flank brighter than the shaded one");
            Assert.Greater(sb, sr, "shaded snow is bluish");
        }
    }
}
