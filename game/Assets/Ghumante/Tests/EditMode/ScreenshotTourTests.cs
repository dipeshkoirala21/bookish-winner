using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Ghumante.Core.Data;
using Ghumante.Core.Generators.Sacred;
using Ghumante.Core.Geo;
using Ghumante.Core.Search;
using Ghumante.Platform.Regions;
using Ghumante.World.Debugging;
using Ghumante.World.Sky;
using NUnit.Framework;

namespace Ghumante.Tests.EditMode
{
    /// <summary>
    /// The screenshot tour's anchors (docs/images/README.md, World/Debug/ScreenshotTourAnchors.cs): every place resolves
    /// in the bundled sample region (search hit near it, coordinates inside the sample's detail tiles) or names the full
    /// valley region and lies inside it; monuments sit where the hero catalogue puts them and in the camera's view; the
    /// eyes are not inside buildings; the sun lights each subject from behind or beside the camera at the anchor's hour
    /// (golden hour for the valley rim); and the framing maths, file names and README. Engine-free.
    /// </summary>
    public class ScreenshotTourTests
    {
        /// <summary>The kathmandu_valley bounding box of pipeline/config/regions.yaml (lon/lat).</summary>
        private const double ValleyLonMin = 85.18, ValleyLatMin = 27.55, ValleyLonMax = 85.58, ValleyLatMax = 27.83;

        /// <summary>Day of the year the world's sky uses in October (SkyPalette.Evaluate's default).</summary>
        private const int OctoberDay = 280;

        private static RegionManifest _manifest;
        private static SearchEngine _search;

        private static RegionManifest Manifest
        {
            get
            {
                if (_manifest == null) _manifest = RegionManifest.Parse(File.ReadAllText(SampleRegion.FilePath(".manifest.json")));
                return _manifest;
            }
        }

        private static SearchEngine Search
        {
            get
            {
                if (_search == null) _search = new SearchEngine(SearchIndexReader.Read(File.ReadAllBytes(SampleRegion.FilePath(".search.ghsi"))));
                return _search;
            }
        }

        private static bool InSample(double x, double z)
        {
            RegionManifest m = Manifest;
            return ScreenshotTourAnchors.Covers(m.MinX, m.MinZ, m.MaxX, m.MaxZ, m.LeafLevel, x, z);
        }

        private static bool InValley(double x, double z)
        {
            double x0, z0, x1, z1;
            WorldFrame.LonLatToGame(ValleyLonMin, ValleyLatMin, out x0, out z0);
            WorldFrame.LonLatToGame(ValleyLonMax, ValleyLatMax, out x1, out z1);
            return x >= Math.Min(x0, x1) && x <= Math.Max(x0, x1) && z >= Math.Min(z0, z1) && z <= Math.Max(z0, z1);
        }

        [Test]
        public void AnchorsAreTheCuratedTwelveWithUniqueFileStems()
        {
            IReadOnlyList<ScreenshotAnchor> all = ScreenshotTourAnchors.All;
            Assert.That(all.Count, Is.InRange(10, 16));
            var ids = new HashSet<string>();
            var stem = new Regex("^[a-z0-9]+(_[a-z0-9]+)*$");
            foreach (ScreenshotAnchor a in all)
            {
                Assert.That(stem.IsMatch(a.Id ?? ""), Is.True, "file stem: " + a.Id);
                Assert.That(ids.Add(a.Id), Is.True, "duplicate id " + a.Id);
                Assert.That(a.Title, Is.Not.Empty, a.Id);
                Assert.That(a.Caption, Is.Not.Empty, a.Id);
                Assert.That(a.Hours, Is.InRange(5.5f, 18.5f), a.Id + ": a daylight hour");
                Assert.That(a.FovDeg, Is.InRange(30f, 75f), a.Id);
                ScreenshotAnchor same;
                Assert.That(ScreenshotTourAnchors.TryGet(a.Id, out same) && ReferenceEquals(same, a), Is.True, a.Id);
            }
            foreach (string must in new[] { "thamel_lane", "asan_tole", "basantapur_durbar", "patan_durbar", "bhaktapur_nyatapola", "boudhanath",
                                             "swayambhu_stair", "pashupati_east_bank", "maitighar_mandala", "shahid_gate", "thapathali_bridge",
                                             "valley_rim_golden_hour" })
            {
                Assert.That(ids.Contains(must), Is.True, "the owner's list names " + must);
            }
        }

        [Test]
        public void EveryAnchorResolvesInTheSampleOrNamesTheValley()
        {
            foreach (ScreenshotAnchor a in ScreenshotTourAnchors.All)
            {
                double ex, ez, lx, lz;
                ScreenshotFraming.EyeXZ(a, out ex, out ez);
                ScreenshotFraming.LookXZ(a, out lx, out lz);
                if (a.Region == null)
                {
                    Assert.That(InSample(a.X, a.Z), Is.True, a.Id + ": subject inside the sample's detail tiles");
                    Assert.That(InSample(ex, ez), Is.True, a.Id + ": eye inside the sample");
                    var leaf = new TileId(10, (int)Math.Floor(a.X / TileId.SizeAt(10)), (int)Math.Floor(a.Z / TileId.SizeAt(10)));
                    Assert.That(SampleRegion.Pack.Contains(leaf), Is.True, a.Id + ": the sample pack holds tile " + leaf);
                    double d;
                    SearchEntry hit = ScreenshotTourAnchors.Confirm(Search, a, out d);
                    Assert.That(hit, Is.Not.Null, a.Id + ": search \"" + a.Query + "\" finds the place within " + ScreenshotTourAnchors.SearchMatchRadiusM + " m");
                }
                else
                {
                    Assert.That(a.Region, Is.EqualTo(ScreenshotTourAnchors.ValleyRegion), a.Id);
                    Assert.That(InSample(a.X, a.Z), Is.False, a.Id + " is in the sample: drop its Region");
                    Assert.That(InValley(a.X, a.Z) && InValley(ex, ez) && InValley(lx, lz), Is.True, a.Id + ": inside the kathmandu_valley region");
                    Assert.That(a.HeroId, Is.Not.Null, a.Id + ": a valley-only anchor is checked against the hero catalogue");
                }
            }
        }

        [Test]
        public void MonumentsAreWhereTheHeroCatalogueSaysAndInView()
        {
            foreach (ScreenshotAnchor a in ScreenshotTourAnchors.All)
            {
                if (a.HeroId == null) continue;
                HeroRecipe hero;
                Assert.That(HeroCatalog.TryGet(a.HeroId, out hero), Is.True, a.Id + ": hero " + a.HeroId);
                double hx, hz;
                WorldFrame.LonLatToGame(hero.Lon, hero.Lat, out hx, out hz);
                Assert.That(Math.Sqrt((hx - a.X) * (hx - a.X) + (hz - a.Z) * (hz - a.Z)), Is.LessThan(30.0), a.Id + ": subject on " + a.HeroId);
                AssertInView(a, hx, hz, a.HeroId);
            }
        }

        [Test]
        public void SubjectIsInFrontOfTheCameraAndTheExplorerInFrame()
        {
            foreach (ScreenshotAnchor a in ScreenshotTourAnchors.All)
            {
                double ex, ez;
                ScreenshotFraming.EyeXZ(a, out ex, out ez);
                // A subject under the eye is the viewpoint itself (the valley rim from Swayambhu hill).
                bool viewpoint = Math.Sqrt((a.X - ex) * (a.X - ex) + (a.Z - ez) * (a.Z - ez)) < 5.0;
                if (a.Framing == ShotFraming.Fixed && !viewpoint) AssertInView(a, a.X, a.Z, "subject");
                double px, pz;
                float heading;
                if (!ScreenshotFraming.PlayerSpot(a, out px, out pz, out heading)) continue;
                AssertInView(a, px, pz, "explorer");
                double d = Math.Sqrt((px - ex) * (px - ex) + (pz - ez) * (pz - ez));
                Assert.That(d, Is.InRange(3.0, 15.0), a.Id + ": the explorer stands a few metres from the camera");
            }
        }

        /// <summary>The point lies in front of the eye inside the portrait shot's horizontal view (the narrower one) or,
        /// for a far subject, at least inside the landscape view.</summary>
        private static void AssertInView(ScreenshotAnchor a, double x, double z, string what)
        {
            double ex, ez, lx, lz;
            ScreenshotFraming.EyeXZ(a, out ex, out ez);
            ScreenshotFraming.LookXZ(a, out lx, out lz);
            // The portrait's view line: the eye-to-look line turned clockwise by the portrait yaw.
            double yaw = ScreenshotFraming.YawDeg(a, ShotOrientation.Portrait) * Math.PI / 180.0;
            double dx = lx - ex, dz = lz - ez;
            double vx = dx * Math.Cos(yaw) + dz * Math.Sin(yaw), vz = -dx * Math.Sin(yaw) + dz * Math.Cos(yaw);
            double tx = x - ex, tz = z - ez;
            double angle = Math.Abs(Math.Atan2(vx * tz - vz * tx, vx * tx + vz * tz)) * 180.0 / Math.PI;
            float portraitH = Ghumante.World.Cameras.CameraFov.HorizontalFromVertical(
                ScreenshotFraming.VerticalFovDeg(a, ShotOrientation.Portrait), (float)ScreenshotFraming.LandscapeHeight / ScreenshotFraming.LandscapeWidth);
            Assert.That(angle, Is.LessThan(portraitH * 0.5), a.Id + ": " + what + " inside the portrait view (" + angle.ToString("0.0") + " deg off axis)");
        }

        [Test]
        public void StreetLevelEyesAreNotInsideBuildings()
        {
            foreach (ScreenshotAnchor a in ScreenshotTourAnchors.All)
            {
                if (a.Region != null || a.EyeHeightM > 12f) continue;
                double ex, ez;
                ScreenshotFraming.EyeXZ(a, out ex, out ez);
                AssertOutsideBuildings(a, ex, ez, "eye");
                double px, pz;
                float heading;
                if (ScreenshotFraming.PlayerSpot(a, out px, out pz, out heading)) AssertOutsideBuildings(a, px, pz, "explorer");
            }
        }

        private static void AssertOutsideBuildings(ScreenshotAnchor a, double x, double z, string what)
        {
            double size = TileId.SizeAt(10);
            var id = new TileId(10, (int)Math.Floor(x / size), (int)Math.Floor(z / size));
            if (!SampleRegion.Pack.Contains(id)) return;
            TileData t = SampleRegion.Pack.ReadTile(id);
            double lx = (x - id.X0) * 100.0, lz = (z - id.Z0) * 100.0;
            foreach (BuildingRecord b in t.Buildings)
            {
                if (b.Rings == null || b.Rings.Length == 0) continue;
                Assert.That(Inside(b.Rings[0], lx, lz), Is.False, a.Id + ": " + what + " inside building " + b.OsmRef);
            }
        }

        /// <summary>Even-odd point in polygon over a ring of tile-local centimetres.</summary>
        private static bool Inside(int[] ring, double x, double z)
        {
            int n = ring.Length / 2;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = ring[2 * i], zi = ring[2 * i + 1], xj = ring[2 * j], zj = ring[2 * j + 1];
                if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi) inside = !inside;
            }
            return inside;
        }

        [Test]
        public void TheSunLightsEverySubjectFromBehindOrBesideTheCamera()
        {
            double decl = SkyPalette.DeclinationDeg(OctoberDay);
            foreach (ScreenshotAnchor a in ScreenshotTourAnchors.All)
            {
                double sx, sy, sz, elevation, azimuth;
                SkyPalette.SunDirection(a.Hours, SkyPalette.DefaultLatitudeDeg, decl, out sx, out sy, out sz, out elevation, out azimuth);
                Assert.That(elevation, Is.GreaterThan(2.0), a.Id + ": the sun is up at " + a.Hours + " h");
                if (a.Backlit) continue;
                double view = ScreenshotFraming.ViewBearingDeg(a);
                double off = Math.Abs(((azimuth - view) % 360.0 + 540.0) % 360.0 - 180.0);
                Assert.That(off, Is.GreaterThan(70.0), a.Id + ": the sun (azimuth " + azimuth.ToString("0") + ") must not face the camera (view " + view.ToString("0") + ")");
            }
        }

        [Test]
        public void TheValleyRimIsShotAtGoldenHour()
        {
            ScreenshotAnchor rim;
            Assert.That(ScreenshotTourAnchors.TryGet("valley_rim_golden_hour", out rim), Is.True);
            SkyState sky = SkyPalette.Evaluate(rim.Hours, OctoberDay);
            Assert.That(sky.SunElevationDeg, Is.InRange(1.0, 12.0), "low evening sun");
            Assert.That(sky.Golden, Is.GreaterThan(0.3f), "golden light");
            Assert.That(rim.Hours, Is.GreaterThan(12f), "evening, not morning");
            Assert.That(rim.PlayerAheadM, Is.EqualTo(0f), "a skyline shot without the explorer");
        }

        [Test]
        public void FramingSizesFovAndFileNames()
        {
            int w, h;
            ScreenshotFraming.Size(ShotOrientation.Landscape, out w, out h);
            Assert.That((w, h), Is.EqualTo((1920, 1080)));
            ScreenshotFraming.Size(ShotOrientation.Portrait, out w, out h);
            Assert.That((w, h), Is.EqualTo((1080, 1920)));
            var a = new ScreenshotAnchor { Id = "test_place", X = 1000, Z = 2000, EyeEast = 0, EyeNorth = -30, FovDeg = 50f, PlayerAheadM = 6f, PlayerSideM = 1.5f, PortraitYawDeg = -8f };
            Assert.That(ScreenshotFraming.FileName(a, ShotOrientation.Landscape), Is.EqualTo("test_place_landscape.jpg"));
            Assert.That(ScreenshotFraming.FileName(a, ShotOrientation.Portrait), Is.EqualTo("test_place_portrait.jpg"));
            Assert.That(ScreenshotFraming.VerticalFovDeg(a, ShotOrientation.Landscape), Is.EqualTo(50f));
            float p = ScreenshotFraming.VerticalFovDeg(a, ShotOrientation.Portrait);
            Assert.That(p, Is.GreaterThan(50f).And.LessThanOrEqualTo(ScreenshotFraming.MaxPortraitFovDeg));
            float landH = Ghumante.World.Cameras.CameraFov.HorizontalFromVertical(50f, 16f / 9f);
            float portH = Ghumante.World.Cameras.CameraFov.HorizontalFromVertical(p, 9f / 16f);
            Assert.That(portH / landH, Is.EqualTo(ScreenshotFraming.PortraitWidthShare).Within(0.01f), "portrait keeps its share of the view");
            Assert.That(ScreenshotFraming.ViewBearingDeg(a), Is.EqualTo(0.0).Within(1e-9), "looking north");
            Assert.That(ScreenshotFraming.TiltDeg(a, ShotOrientation.Landscape), Is.EqualTo(0f));
            Assert.That(ScreenshotFraming.TiltDeg(a, ShotOrientation.Portrait), Is.EqualTo(a.PortraitTiltDeg));
            Assert.That(ScreenshotFraming.YawDeg(a, ShotOrientation.Landscape), Is.EqualTo(0f));
            Assert.That(ScreenshotFraming.YawDeg(a, ShotOrientation.Portrait), Is.EqualTo(-8f));

            double px, pz;
            float heading;
            Assert.That(ScreenshotFraming.PlayerSpot(a, out px, out pz, out heading), Is.True);
            Assert.That(px, Is.EqualTo(1001.5).Within(1e-9), "1.5 m right (east) of a north-facing view");
            Assert.That(pz, Is.EqualTo(1976.0).Within(1e-9), "6 m in front of the eye");
            Assert.That(Math.Sin(heading), Is.LessThan(0.0), "facing the subject, back to the camera (turned slightly west)");

            float eyeY, lookY;
            ScreenshotFraming.Heights(a, 1300f, 1302f, 1301f, out eyeY, out lookY);
            Assert.That(eyeY, Is.EqualTo(1300f + a.EyeHeightM));
            Assert.That(lookY, Is.EqualTo(1302f + a.LookHeightM));
        }

        [Test]
        public void ChaseShotsSitBehindTheExplorerAlongItsBearing()
        {
            var a = new ScreenshotAnchor { Id = "chase", X = 500, Z = 700, Framing = ShotFraming.Chase, BearingDeg = 90f, ChaseBackM = 5f, ChaseAheadM = 14f, EyeHeightM = 2.3f, LookHeightM = 1.6f };
            double ex, ez, lx, lz, px, pz;
            float heading;
            ScreenshotFraming.EyeXZ(a, out ex, out ez);
            ScreenshotFraming.LookXZ(a, out lx, out lz);
            Assert.That(ex, Is.EqualTo(495.0).Within(1e-6));
            Assert.That(ez, Is.EqualTo(700.0).Within(1e-6));
            Assert.That(lx, Is.EqualTo(514.0).Within(1e-6));
            Assert.That(ScreenshotFraming.PlayerSpot(a, out px, out pz, out heading), Is.True);
            Assert.That((px, pz), Is.EqualTo((500.0, 700.0)));
            Assert.That(heading, Is.EqualTo((float)(Math.PI / 2)).Within(1e-5f));
            float eyeY, lookY;
            ScreenshotFraming.Heights(a, 1290f, 1310f, 1300f, out eyeY, out lookY);
            Assert.That(eyeY, Is.EqualTo(1302.3f).Within(1e-4f), "from the explorer's feet");
            Assert.That(lookY, Is.EqualTo(1301.6f).Within(1e-4f));
            ScreenshotFraming.Heights(a, 1305f, 1310f, 1300f, out eyeY, out lookY);
            Assert.That(eyeY, Is.EqualTo(1306.2f).Within(1e-4f), "never below the street climbing behind");
        }

        [Test]
        public void CoversUsesTheLeafTileGrid()
        {
            // A bbox inside one 1024 m cell covers the whole cell, nothing beyond it.
            Assert.That(ScreenshotTourAnchors.Covers(1100, 1100, 1200, 1200, 10, 1030, 2040), Is.True);
            Assert.That(ScreenshotTourAnchors.Covers(1100, 1100, 1200, 1200, 10, 2050, 1100), Is.False);
            Assert.That(ScreenshotTourAnchors.Covers(1100, 1100, 1200, 1200, 10, 1100, 1020), Is.False);
            // The real sample: Boudhanath yes, Patan no.
            Assert.That(InSample(SampleRegion.BoudhaX, SampleRegion.BoudhaZ), Is.True);
            double x, z;
            WorldFrame.LonLatToGame(85.324964, 27.673623, out x, out z);
            Assert.That(InSample(x, z), Is.False);
            Assert.That(InValley(x, z), Is.True);
        }

        [Test]
        public void ConfirmIgnoresFarHitsOfTheSameName()
        {
            // "Krishna Mandir" exists in the sample, but none near Patan's: the anchor must not jump to another one.
            var a = new ScreenshotAnchor { Id = "k", Query = "Krishna Mandir", X = 530670.9, Z = 161750.6 };
            double d;
            Assert.That(ScreenshotTourAnchors.Confirm(Search, a, out d), Is.Null);
            Assert.That(double.IsPositiveInfinity(d), Is.True);
            var b = new ScreenshotAnchor { Id = "b", Query = "Boudhanath", X = SampleRegion.BoudhaX, Z = SampleRegion.BoudhaZ };
            Assert.That(ScreenshotTourAnchors.Confirm(Search, b, out d), Is.Not.Null);
            Assert.That(d, Is.LessThan(5.0));
            Assert.That(ScreenshotTourAnchors.Confirm(null, b, out d), Is.Null, "no search index: coordinates only");
        }

        [Test]
        public void ReadmeListsShotsSkipsAndTheMapNotice()
        {
            ScreenshotAnchor thamel, patan;
            ScreenshotTourAnchors.TryGet("thamel_lane", out thamel);
            ScreenshotTourAnchors.TryGet("patan_durbar", out patan);
            var entries = new List<ScreenshotReadme.Entry>
            {
                new ScreenshotReadme.Entry { Anchor = thamel, Landscape = true, Portrait = true },
                new ScreenshotReadme.Entry { Anchor = patan, Skipped = "outside region kathmandu_core" },
            };
            string md = ScreenshotReadme.Build(entries, "kathmandu_core", new DateTime(2026, 10, 11, 9, 30, 0, DateTimeKind.Utc));
            StringAssert.Contains("](thamel_lane_landscape.jpg)", md);
            StringAssert.Contains("[thamel_lane_portrait.jpg](thamel_lane_portrait.jpg)", md);
            StringAssert.Contains("2026-10-11 09:30 UTC", md);
            StringAssert.Contains("`kathmandu_core`", md);
            StringAssert.Contains("Not captured in this run", md);
            StringAssert.Contains(patan.Title + "**: outside region kathmandu_core", md);
            StringAssert.Contains("OpenStreetMap contributors", md);
            Assert.That(md, Does.Not.Contain("patan_durbar_landscape.jpg"));
        }

        [Test]
        public void SingleShotNamesSortByTime()
        {
            string a = ScreenshotFraming.SingleShotName(new DateTime(2026, 10, 11, 8, 5, 9), ShotOrientation.Portrait, false);
            string b = ScreenshotFraming.SingleShotName(new DateTime(2026, 10, 11, 18, 0, 0), ShotOrientation.Landscape, true);
            Assert.That(a, Is.EqualTo("shot_20261011_080509_portrait.jpg"));
            Assert.That(b, Is.EqualTo("shot_20261011_180000_landscape_hud.jpg"));
            Assert.That(string.CompareOrdinal(a, b), Is.LessThan(0));
            Assert.That(ScreenshotFraming.SuperSampleFactor(3), Is.EqualTo(2));
            Assert.That(ScreenshotFraming.SuperSampleFactor(0), Is.EqualTo(1));
            Assert.That(ScreenshotFraming.SuperSampleFactor(8), Is.EqualTo(4));
        }
    }
}
