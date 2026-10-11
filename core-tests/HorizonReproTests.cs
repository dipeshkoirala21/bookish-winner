using System;
using System.Collections.Generic;
using System.Text;
using Ghumante.Core.Geo;
using Ghumante.Core.Horizon;
using Ghumante.World.Sky;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Owner playtest #12 ("Add mountainous peaks in the far background"): from Basantapur, Boudha and Swayambhu the snow
    /// peaks that really stand above the valley rim (Ganesh Himal, Langtang Lirung, Phurbi Chyachu of the Jugal,
    /// Gauri Shankar) must be drawn at their real bearing and elevation, clear enough to read on every tier.
    /// <para>
    /// Which peaks are visible from each place was worked out on the 30 m Copernicus GLO-30 DEM (curvature with
    /// R_eff = 7/6 R, eye 2 m above the ground; docs/research/w2/ref_sky.md): from the valley floor they rise only
    /// 0.3-1.0 degrees above the rim, exactly as in the reference photos. Bearings and elevations here come from the
    /// published summit coordinates and heights, independent of any game data.
    /// </para>
    /// </summary>
    [TestFixture]
    public class HorizonReproTests
    {
        internal readonly struct TruePeak
        {
            public readonly string Name;
            public readonly double Lat, Lon, HeightM;

            public TruePeak(string name, double lat, double lon, double heightM)
            {
                Name = name;
                Lat = lat;
                Lon = lon;
                HeightM = heightM;
            }
        }

        internal static readonly TruePeak Yangra = new TruePeak("Ganesh I (Yangra)", 28.39250, 85.13000, 7422);
        internal static readonly TruePeak Pabil = new TruePeak("Ganesh IV (Pabil)", 28.34583, 85.08000, 7104);
        internal static readonly TruePeak Salasungo = new TruePeak("Ganesh III (Salasungo)", 28.33500, 85.12167, 7043);
        internal static readonly TruePeak LangtangLirung = new TruePeak("Langtang Lirung", 28.25750, 85.51583, 7234);
        internal static readonly TruePeak PhurbiChyachu = new TruePeak("Phurbi Chyachu (Jugal)", 28.13056, 85.87194, 6637);
        internal static readonly TruePeak GauriShankar = new TruePeak("Gauri Shankar", 27.95333, 86.33583, 7134);

        /// <summary>Peaks above the rim from each viewpoint (GLO-30 line of sight, eye 2 m above the ground).</summary>
        internal static TruePeak[] VisibleFrom(HorizonViewpoint v)
        {
            if (v.Name == HorizonViewpoint.Basantapur.Name) return new[] { Yangra, Pabil, Salasungo, LangtangLirung, PhurbiChyachu, GauriShankar };
            if (v.Name == HorizonViewpoint.Boudha.Name) return new[] { Yangra, Pabil, Salasungo, PhurbiChyachu, GauriShankar };
            return new[] { LangtangLirung, GauriShankar };
        }

        /// <summary>Largest error allowed between the drawn summit and the real one, in degrees of elevation: 0.1 degree
        /// is about 110 m at 65 km, a tenth of how far the peaks rise above the rim.</summary>
        internal const double ElevationToleranceDeg = 0.1;

        /// <summary>Bearing window searched around the real bearing (the task's 2 degrees).</summary>
        internal const double BearingToleranceDeg = 2.0;

        /// <summary>At least this share of a summit's own colour must survive the scene fog when only the streamed terrain
        /// draws it (the game before the skyline).</summary>
        internal const double MinTransmittance = 0.4;

        /// <summary>A peak drawn by the skyline must differ from the sky behind it by at least this share of the sky's
        /// luminance (sunlit snow against the hazy horizon band), so it reads as a peak and not as sky.</summary>
        internal const double MinContrast = 0.12;

        /// <summary>
        /// Every peak that really stands above the rim is drawn at its true bearing (within 2 degrees) and elevation
        /// (within 0.1 degree), clear enough to read, from all three places on every tier. At the playtest only the
        /// streamed horizon tiles could show the Himalaya: on Low and Mid they are cut off or fogged out (the fog floor
        /// that hides the end of the last LOD ring leaves 0-12 % of a peak at 65-105 km), and on High the 2 km terrain
        /// quads of the level-5 ring flatten the summits by hundreds of metres.
        /// </summary>
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void PeaksDrawnAtTrueBearingAndElevation(int tier)
        {
            var report = new StringBuilder();
            int missing = 0, checkedPeaks = 0;
            foreach (HorizonViewpoint v in HorizonViewpoint.All)
            {
                var scene = new HorizonScene(v, tier);
                foreach (TruePeak p in VisibleFrom(v))
                {
                    checkedPeaks++;
                    string why = CheckDrawn(scene, p, scene.DrawnRay);
                    if (why == null) continue;
                    missing++;
                    report.Append(v.Name).Append(": ").Append(why).Append('\n');
                }
            }
            TestContext.WriteLine("tier " + tier + ": " + missing + " of " + checkedPeaks + " peak sightings wrong or invisible\n" + report);
            Assert.That(missing, Is.EqualTo(0), "tier " + tier + "\n" + report);
        }

        /// <summary>
        /// The playtest state, for the record (run explicitly): the same check with the streamed terrain alone, which is
        /// all the game drew before the skyline ring (docs/research/w2/ref_sky.md "Before").
        /// </summary>
        [Test, Explicit("before/after evidence: the game without the skyline ring")]
        public void ReportStreamedTerrainAlone()
        {
            for (int tier = 0; tier <= 2; tier++)
            {
                var report = new StringBuilder();
                int missing = 0, checkedPeaks = 0;
                foreach (HorizonViewpoint v in HorizonViewpoint.All)
                {
                    var scene = new HorizonScene(v, tier, 9.0, false);
                    foreach (TruePeak p in VisibleFrom(v))
                    {
                        checkedPeaks++;
                        string why = CheckDrawn(scene, p, scene.DrawnRay);
                        if (why == null) continue;
                        missing++;
                        report.Append(v.Name).Append(": ").Append(why).Append('\n');
                    }
                }
                TestContext.WriteLine("without the skyline, tier " + tier + ": " + missing + " of " + checkedPeaks + " peak sightings wrong or invisible\n" + report);
            }
        }

        /// <summary>Null when <paramref name="p"/> is drawn right in <paramref name="ray"/>'s skyline seen from the scene,
        /// else why not.</summary>
        internal static string CheckDrawn(HorizonScene scene, TruePeak p, Func<double, HorizonRay> ray)
        {
            double px, pz, bearing, dist, elev;
            WorldFrame.LonLatToGame(p.Lon, p.Lat, out px, out pz);
            scene.Sight(px, pz, p.HeightM, out bearing, out dist, out elev);
            // The drawn summit: the highest point of the skyline within the bearing window that lies near the peak's
            // distance (a nearer ridge in the window is not the peak).
            HorizonRay top = default(HorizonRay);
            double topBearing = 0;
            top.ElevationDeg = -90;
            for (double b = bearing - BearingToleranceDeg; b <= bearing + BearingToleranceDeg + 1e-9; b += 0.05)
            {
                HorizonRay r = ray((b + 360.0) % 360.0);
                if (!r.Hit || Math.Abs(r.DistanceM - dist) > 15000.0) continue;
                if (r.ElevationDeg > top.ElevationDeg)
                {
                    top = r;
                    topBearing = b;
                }
            }
            string head = string.Format("{0} (bearing {1:0.0}, {2:0.0} km, true elevation {3:0.00} deg)", p.Name, bearing, dist / 1000.0, elev);
            if (!top.Hit)
                return head + string.Format(": nothing drawn near that distance within +-2 deg (far clip {0:0} km, scene haze leaves {1:0.00})",
                                            scene.FarClipM / 1000.0, scene.SceneTransmittance(dist));
            double err = top.ElevationDeg - elev;
            if (Math.Abs(err) > ElevationToleranceDeg)
                return head + string.Format(": drawn summit {0:0.00} deg ({1:+0.00;-0.00} deg, {2:0} m) at bearing {3:0.0}, transmittance {4:0.00}",
                                            top.ElevationDeg, err, top.AltitudeM, topBearing, top.Transmittance);
            if (!top.FromRing && top.Transmittance < MinTransmittance)
                return head + string.Format(": drawn but hazed away (transmittance {0:0.00})", top.Transmittance);
            if (top.FromRing)
            {
                double contrast = SummitContrast(scene, topBearing, top);
                if (contrast < MinContrast)
                    return head + string.Format(": drawn but lost against the sky (contrast {0:0.00}, transmittance {1:0.00})", contrast, top.Transmittance);
            }
            return null;
        }

        /// <summary>Relative luminance difference between a sunlit summit of the ring (HorizonLook, the shader's model)
        /// and the game's sky behind it at that bearing and elevation.</summary>
        internal static double SummitContrast(HorizonScene scene, double bearingDeg, HorizonRay top)
        {
            SkyState sky = SkyPalette.Evaluate(scene.Hours);
            HorizonLight light = HorizonPreviews.LightFor(sky, scene.FogDensity);
            double b = bearingDeg * Math.PI / 180.0, e = top.ElevationDeg * Math.PI / 180.0;
            double dx = Math.Sin(b) * Math.Cos(e), dy = Math.Sin(e), dz = Math.Cos(b) * Math.Cos(e);
            // The face of the summit seen from the eye, tilted up (as the mesher's pseudo-normal is).
            double nx = -dx * 0.8, ny = 0.55, nz = -dz * 0.8, nl = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            // The summit's mean colour along 2 km of curtain (its snow with the rock ribs the wide view shows).
            double r = 0, g = 0, bl = 0;
            const int samples = 16;
            for (int k = 0; k < samples; k++)
            {
                float sr0, sg0, sb0;
                HorizonLook.Shade(top.AltitudeM, 0.2, k * 125.0, 0.0, nx / nl, ny / nl, nz / nl, top.DistanceM, scene.EyeM, light, out sr0, out sg0, out sb0);
                r += sr0 / samples;
                g += sg0 / samples;
                bl += sb0 / samples;
            }
            double sr, sg, sb;
            HorizonPreviews.SkyColour(sky, dx, dy, dz, out sr, out sg, out sb);
            double ls = 0.2126 * sr + 0.7152 * sg + 0.0722 * sb, lp = 0.2126 * r + 0.7152 * g + 0.0722 * bl;
            return Math.Abs(lp - ls) / Math.Max(1e-3, ls);
        }
    }
}
