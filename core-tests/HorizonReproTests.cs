using System;
using System.Collections.Generic;
using System.Globalization;
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

        /// <summary>Largest bearing error allowed between the drawn summit and the real one (the task's 2 degrees); the
        /// drawn summit is the local maximum of the drawn skyline nearest to the true bearing.</summary>
        internal const double BearingToleranceDeg = 2.0;

        /// <summary>At least this share of a summit's own colour must survive the scene fog when only the streamed terrain
        /// draws it (the game before the skyline).</summary>
        internal const double MinTransmittance = 0.4;

        /// <summary>
        /// A peak drawn by the skyline must stand out from the sky behind it: the mean absolute luminance difference of
        /// its top 0.1 degree (±0.5 degree round the summit), shaded as the skyline shader shades it, must reach this
        /// share of the sky's luminance there at every daytime hour checked (sunlit faces brighter than the sky, shaded
        /// faces and rock darker; the photos show 0.3-0.5).
        /// </summary>
        internal const double MinContrast = 0.15;

        /// <summary>Low's bar: its coarser panorama (0.4 degree rays, 0.05 degree tolerance, 256 m grid) merges more of a
        /// summit's rocky shoulder into its faces; its worst sighting, Pabil from Boudha at 15:00, reads 0.148.</summary>
        internal const double MinContrastLow = 0.14;

        /// <summary>The contrast a peak must reach on <paramref name="tier"/>.</summary>
        internal static double MinContrastFor(int tier)
        {
            return tier == HorizonSettings.TierLow ? MinContrastLow : MinContrast;
        }

        /// <summary>Local solar hours the contrast must hold at: morning, noon and afternoon light (the sun behind the
        /// eastern peaks in the morning, behind the Ganesh range in the afternoon).</summary>
        internal static readonly double[] DaytimeHours = { 9.0, 12.0, 15.0 };

        /// <summary>Angle one pixel spans in the game's landscape view (62 degrees across 1 920 pixels): the rib detail
        /// fades as it does on screen.</summary>
        internal const double GamePixelRad = 62.0 / 1920.0 * Math.PI / 180.0;

        /// <summary>A peak as drawn from a viewpoint, against its true bearing and elevation.</summary>
        internal struct Sighting
        {
            public string Peak;
            public double TrueBearing, TrueElevation, DistanceM;
            public double DrawnBearing, DrawnElevation, DrawnAltitude, Transmittance;
            public bool Drawn, LocalMax, FromRing;

            /// <summary>Lowest mean contrast over <see cref="DaytimeHours"/> (ring only) and the hour it occurs.</summary>
            public double Contrast, ContrastHours;

            /// <summary>Null when the peak is drawn right, else why not.</summary>
            public string Failure;

            public double BearingError
            {
                get
                {
                    double d = DrawnBearing - TrueBearing;
                    return ((d % 360.0) + 540.0) % 360.0 - 180.0;
                }
            }

            public double ElevationError
            {
                get { return DrawnElevation - TrueElevation; }
            }

            public string Head
            {
                get { return string.Format("{0} (bearing {1:0.0}, {2:0.0} km, true elevation {3:0.00} deg)", Peak, TrueBearing, DistanceM / 1000.0, TrueElevation); }
            }
        }

        /// <summary>
        /// Every peak that really stands above the rim is drawn at its true bearing (the drawn summit within 2 degrees)
        /// and elevation (within 0.1 degree), clear enough to read at morning, noon and afternoon light, from all three
        /// places on every tier. At the playtest only the streamed horizon tiles could show the Himalaya: on Low and Mid
        /// they are cut off or fogged out (the fog floor that hides the end of the last LOD ring leaves 0-12 % of a peak
        /// at 65-105 km), and on High the 2 km terrain quads of the level-5 ring flatten the summits by hundreds of metres.
        /// </summary>
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void PeaksDrawnAtTrueBearingAndElevation(int tier)
        {
            var report = new StringBuilder();
            var table = new StringBuilder();
            int missing = 0, checkedPeaks = 0;
            table.Append("viewpoint   peak                      true brg  drawn brg  d.brg   true el  drawn el  d.el   km     contrast (worst hour)\n");
            foreach (HorizonViewpoint v in HorizonViewpoint.All)
            {
                var scene = new HorizonScene(v, tier);
                foreach (TruePeak p in VisibleFrom(v))
                {
                    checkedPeaks++;
                    Sighting si = Check(scene, p, scene.DrawnRay, DaytimeHours);
                    table.AppendFormat(CultureInfo.InvariantCulture, "{0,-11} {1,-25} {2,7:0.00}  {3,8:0.00}  {4,6:+0.00;-0.00}  {5,7:0.00}  {6,8:0.00}  {7,6:+0.00;-0.00}  {8,5:0.0}  {9:0.00} ({10:0}h){11}\n",
                                       v.Name, p.Name, si.TrueBearing, si.DrawnBearing, Round2(si.BearingError), si.TrueElevation, si.DrawnElevation, Round2(si.ElevationError),
                                       si.DistanceM / 1000.0, si.Contrast, si.ContrastHours, si.Failure == null ? "" : "  FAIL");
                    if (si.Failure == null) continue;
                    missing++;
                    report.Append(v.Name).Append(": ").Append(si.Failure).Append('\n');
                }
            }
            TestContext.WriteLine("tier " + tier + ": " + missing + " of " + checkedPeaks + " peak sightings wrong or invisible\n" + table + report);
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

        /// <summary>Null when <paramref name="p"/> is drawn right in <paramref name="ray"/>'s skyline seen from the scene
        /// (contrast at 9 am), else why not.</summary>
        internal static string CheckDrawn(HorizonScene scene, TruePeak p, Func<double, HorizonRay> ray)
        {
            return Check(scene, p, ray, new[] { scene.Hours }).Failure;
        }

        /// <summary>How <paramref name="p"/> is drawn in <paramref name="ray"/>'s skyline seen from the scene: the drawn
        /// summit (the local maximum of the skyline on the peak's range nearest to its true bearing, within the bearing
        /// tolerance), its errors, and for the ring the lowest contrast over <paramref name="hours"/>.</summary>
        internal static Sighting Check(HorizonScene scene, TruePeak p, Func<double, HorizonRay> ray, double[] hours)
        {
            double px, pz, bearing, dist, elev;
            WorldFrame.LonLatToGame(p.Lon, p.Lat, out px, out pz);
            scene.Sight(px, pz, p.HeightM, out bearing, out dist, out elev);
            var si = new Sighting { Peak = p.Name, TrueBearing = bearing, TrueElevation = elev, DistanceM = dist, Contrast = double.NaN };
            // The skyline on the peak's range (within 15 km of its distance: a nearer ridge in the window is not the peak).
            const double step = 0.01;
            int count = (int)Math.Round(2 * BearingToleranceDeg / step) + 1;
            var rays = new HorizonRay[count];
            for (int k = 0; k < count; k++)
            {
                HorizonRay r = ray(((bearing - BearingToleranceDeg + k * step) % 360.0 + 360.0) % 360.0);
                rays[k] = r.Hit && Math.Abs(r.DistanceM - dist) <= 15000.0 ? r : default(HorizonRay);
            }
            // Local maxima (highest within ±0.15 degree); the one nearest the true bearing is the drawn summit.
            int best = -1, highest = -1;
            const int half = 15;
            for (int k = 0; k < count; k++)
            {
                if (!rays[k].Hit) continue;
                if (highest < 0 || rays[k].ElevationDeg > rays[highest].ElevationDeg) highest = k;
                bool max = true;
                for (int j = Math.Max(0, k - half); j <= Math.Min(count - 1, k + half) && max; j++)
                    if (j != k && rays[j].Hit && rays[j].ElevationDeg > rays[k].ElevationDeg) max = false;
                if (max && (best < 0 || Math.Abs(k - (count - 1) / 2) < Math.Abs(best - (count - 1) / 2))) best = k;
            }
            si.LocalMax = best >= 0;
            if (best < 0) best = highest;
            if (best < 0)
            {
                si.Failure = si.Head + string.Format(": nothing drawn near that distance within +-2 deg (far clip {0:0} km, scene haze leaves {1:0.00})",
                                                     scene.FarClipM / 1000.0, scene.SceneTransmittance(dist));
                return si;
            }
            HorizonRay top = rays[best];
            si.Drawn = true;
            si.FromRing = top.FromRing;
            si.DrawnBearing = ((bearing - BearingToleranceDeg + best * step) % 360.0 + 360.0) % 360.0;
            si.DrawnElevation = top.ElevationDeg;
            si.DrawnAltitude = top.AltitudeM;
            si.Transmittance = top.Transmittance;
            if (Math.Abs(si.ElevationError) > ElevationToleranceDeg)
            {
                si.Failure = si.Head + string.Format(": drawn summit {0:0.00} deg ({1:+0.00;-0.00} deg, {2:0} m) at bearing {3:0.0}, transmittance {4:0.00}",
                                                     top.ElevationDeg, si.ElevationError, top.AltitudeM, si.DrawnBearing, top.Transmittance);
                return si;
            }
            if (Math.Abs(si.BearingError) > BearingToleranceDeg)
            {
                si.Failure = si.Head + string.Format(": drawn summit at bearing {0:0.00} ({1:+0.00;-0.00} deg)", si.DrawnBearing, si.BearingError);
                return si;
            }
            if (!top.FromRing)
            {
                if (top.Transmittance < MinTransmittance)
                    si.Failure = si.Head + string.Format(": drawn but hazed away (transmittance {0:0.00})", top.Transmittance);
                return si;
            }
            si.Contrast = double.MaxValue;
            List<RingSample> samples = SummitSamples(scene, si.DrawnBearing, dist);
            foreach (double h in hours)
            {
                double c = SummitContrast(scene, samples, h);
                if (c < si.Contrast)
                {
                    si.Contrast = c;
                    si.ContrastHours = h;
                }
            }
            if (si.Contrast < MinContrastFor(scene.Tier))
                si.Failure = si.Head + string.Format(": drawn but lost against the sky (contrast {0:0.00} at {1:0.0} h, transmittance {2:0.00})", si.Contrast,
                                                     si.ContrastHours, top.Transmittance);
            return si;
        }

        /// <summary>A point of the drawn summit area: its direction and the ring surface there.</summary>
        internal struct RingSample
        {
            public double BearingDeg, ElevationDeg;
            public RingPoint Point;
        }

        /// <summary>The top 0.1 degree of the drawn skyline within ±0.5 degree of <paramref name="bearingDeg"/>, where the
        /// ring on the peak's range (within 15 km of <paramref name="distanceM"/>) forms it.</summary>
        internal static List<RingSample> SummitSamples(HorizonScene scene, double bearingDeg, double distanceM)
        {
            var list = new List<RingSample>();
            for (double b = bearingDeg - 0.5; b <= bearingDeg + 0.5 + 1e-9; b += 0.025)
            {
                double bb = (b % 360.0 + 360.0) % 360.0;
                HorizonRay top = scene.DrawnRay(bb);
                if (!top.Hit || !top.FromRing || Math.Abs(top.DistanceM - distanceM) > 15000.0) continue;
                for (int k = 1; k <= 8; k++)
                {
                    double e = top.ElevationDeg - k * 0.0125;
                    RingPoint pt;
                    if (!scene.RingSurface(bb, e, out pt)) continue;
                    list.Add(new RingSample { BearingDeg = bb, ElevationDeg = e, Point = pt });
                }
            }
            return list;
        }

        /// <summary>Mean |L - L_sky| / L_sky over <paramref name="samples"/> at <paramref name="hours"/> local solar time:
        /// the ring shaded as the skyline shader shades it (<see cref="HorizonPreviews.ShadeRing"/>) against the game's
        /// sky in the same direction (SkyGradient).</summary>
        internal static double SummitContrast(HorizonScene scene, List<RingSample> samples, double hours)
        {
            if (samples.Count == 0) return 0.0;
            SkyState sky = SkyPalette.Evaluate(hours);
            HorizonLight light = HorizonPreviews.LightFor(sky, scene.FogDensityAt(hours));
            double sum = 0.0;
            foreach (RingSample smp in samples)
            {
                float r, g, b;
                HorizonPreviews.ShadeRing(scene, light, smp.BearingDeg, smp.ElevationDeg, smp.Point, GamePixelRad, out r, out g, out b);
                double br = smp.BearingDeg * Math.PI / 180.0, er = smp.ElevationDeg * Math.PI / 180.0;
                double sr, sg, sb;
                HorizonPreviews.SkyColour(sky, Math.Sin(br) * Math.Cos(er), Math.Sin(er), Math.Cos(br) * Math.Cos(er), out sr, out sg, out sb);
                double ls = Luminance(sr, sg, sb), lp = Luminance(r, g, b);
                sum += Math.Abs(lp - ls) / Math.Max(1e-3, ls);
            }
            return sum / samples.Count;
        }

        /// <summary>Rounded to 0.01 without a negative zero (for the table).</summary>
        private static double Round2(double v)
        {
            double r = Math.Round(v, 2);
            return r == 0.0 ? 0.0 : r;
        }

        internal static double Luminance(double r, double g, double b)
        {
            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
        }
    }
}
