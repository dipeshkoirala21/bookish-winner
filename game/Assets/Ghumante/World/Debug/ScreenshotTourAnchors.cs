#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Ghumante.Core.Data;
using Ghumante.Core.Search;
using Ghumante.World.Cameras;

namespace Ghumante.World.Debugging
{
    /// <summary>How a <see cref="ScreenshotAnchor"/> frames its shot.</summary>
    public enum ShotFraming
    {
        /// <summary>A fixed cinematic eye and look point (offsets from the subject); the explorer stands a few metres in
        /// front of the camera, gazing at the subject like a visitor.</summary>
        Fixed = 0,

        /// <summary>The game's chase view: the explorer stands on the subject point facing <see cref="ScreenshotAnchor.BearingDeg"/>
        /// and the camera sits behind and above them, looking down the street.</summary>
        Chase = 1,
    }

    /// <summary>The two README sizes.</summary>
    public enum ShotOrientation
    {
        /// <summary>1920 x 1080.</summary>
        Landscape = 0,

        /// <summary>1080 x 1920.</summary>
        Portrait = 1,
    }

    /// <summary>
    /// One curated place of the screenshot tour (docs/images/README.md): where it is in game metres (x east, z north;
    /// docs/research/w2/ref_shots.md has the photo references every framing was matched to), how the camera frames it and
    /// at what time of day. Engine-free data; the tour (<see cref="ScreenshotTour"/>) samples the streamed ground for the
    /// heights, so every height here is relative to the ground under the point. Treat instances as read-only.
    /// </summary>
    public sealed class ScreenshotAnchor
    {
        /// <summary>File stem: <c>&lt;Id&gt;_landscape.jpg</c>, <c>&lt;Id&gt;_portrait.jpg</c> (lowercase, digits, underscores).</summary>
        public string Id;

        /// <summary>Short English title for the README table.</summary>
        public string Title;

        /// <summary>One English sentence for the README: what the shot shows.</summary>
        public string Caption;

        /// <summary>Search query that must find the place near <see cref="X"/>, <see cref="Z"/> in a region that covers it
        /// (within <see cref="ScreenshotTourAnchors.SearchMatchRadiusM"/>); null for a pure coordinate anchor. The search
        /// confirms the place, the coordinates stay authoritative (so the tuned framing never drifts).</summary>
        public string Query;

        /// <summary>The hero replica the shot is about (<c>HeroCatalog</c> id), or null.</summary>
        public string HeroId;

        /// <summary>Subject point, game metres: the monument centre, or the explorer's spot for <see cref="ShotFraming.Chase"/>.</summary>
        public double X, Z;

        public ShotFraming Framing;

        /// <summary><see cref="ShotFraming.Fixed"/>: eye offset from the subject in metres (east, north).</summary>
        public double EyeEast, EyeNorth;

        /// <summary>Eye height above the ground under the eye (Fixed); camera height above the explorer's feet (Chase).</summary>
        public float EyeHeightM = 1.7f;

        /// <summary><see cref="ShotFraming.Fixed"/>: look-point offset from the subject in metres (east, north).</summary>
        public double LookEast, LookNorth;

        /// <summary>Look-point height above the ground under it (Fixed), above the explorer's feet (Chase).</summary>
        public float LookHeightM = 8f;

        /// <summary><see cref="ShotFraming.Chase"/>: compass bearing the explorer faces (degrees clockwise from north);
        /// the camera is <see cref="ChaseBackM"/> behind. Ignored by Fixed shots (the explorer faces the look point).</summary>
        public float BearingDeg;

        /// <summary><see cref="ShotFraming.Chase"/>: metres behind the explorer.</summary>
        public float ChaseBackM = 5f;

        /// <summary><see cref="ShotFraming.Chase"/>: metres ahead of the explorer the camera looks at.</summary>
        public float ChaseAheadM = 14f;

        /// <summary><see cref="ShotFraming.Fixed"/>: the explorer stands this far in front of the eye (0: no explorer in
        /// the frame; it waits on the ground under the eye).</summary>
        public float PlayerAheadM = 7f;

        /// <summary><see cref="ShotFraming.Fixed"/>: and this far to the right of the view line (negative: left).</summary>
        public float PlayerSideM = 1.6f;

        /// <summary>Local solar time of the shot (the world clock, 0-24); chosen so the sun lights the subject from
        /// behind or beside the camera (tested), golden hour for the valley rim.</summary>
        public float Hours = 10f;

        /// <summary>Vertical field of view of the landscape shot in degrees; the portrait shot keeps
        /// <see cref="ScreenshotFraming.PortraitWidthShare"/> of its horizontal view (<see cref="ScreenshotFraming.VerticalFovDeg"/>).</summary>
        public float FovDeg = 50f;

        /// <summary>The portrait shot pitches up by this much (degrees) from the landscape view line: a tall frame keeps
        /// the roofs, signboards and spires instead of an empty foreground (0 for shots looking down).</summary>
        public float PortraitTiltDeg = 6f;

        /// <summary>The portrait shot turns this much (degrees, clockwise seen from above; negative turns left) from the
        /// landscape view line: when the wide frame holds two monuments, the narrow one centres on the main subject.</summary>
        public float PortraitYawDeg;

        /// <summary>The region the place needs when the bundled sample does not cover it (the full valley), or null.</summary>
        public string Region;

        /// <summary>True when the sun may face the camera (a backlit golden-hour skyline); the tests let it off the
        /// front-light rule.</summary>
        public bool Backlit;

        public override string ToString()
        {
            return Id;
        }
    }

    /// <summary>
    /// The screenshot tour's curated places (W2 detail pass, owner request: real gameplay screenshots in the README) and
    /// their checks: region coverage and the search confirmation. Engine-free; EditMode tests
    /// (ScreenshotTourTests) validate every anchor against the bundled sample region, the hero catalogue and the sun.
    /// </summary>
    public static class ScreenshotTourAnchors
    {
        /// <summary>The full-valley region (pipeline/config/regions.yaml); Patan and Bhaktapur are only in it.</summary>
        public const string ValleyRegion = "kathmandu_valley";

        /// <summary>A search hit this close to the anchor confirms the place (a district name such as Thapathali sits a
        /// few hundred metres from the bridge it is known for).</summary>
        public const double SearchMatchRadiusM = 500.0;

        /// <summary>Search results considered per query.</summary>
        public const int SearchResults = 8;

        private static readonly ScreenshotAnchor[] Anchors =
        {
            new ScreenshotAnchor
            {
                Id = "thamel_lane", Title = "Thamel at street level",
                Caption = "The explorer in a Thamel lane: signboards, shopfronts, motorbikes and walkers in the late-afternoon sun.",
                Query = "Thamel", X = 529289.0, Z = 166382.0, Framing = ShotFraming.Chase, BearingDeg = 11f,
                EyeHeightM = 2.3f, LookHeightM = 1.6f, ChaseBackM = 5.5f, ChaseAheadM = 16f, Hours = 15.5f, FovDeg = 52f,
            },
            new ScreenshotAnchor
            {
                Id = "asan_tole", Title = "Asan Tole",
                Caption = "Asan's market crossroads: the gilded three-roofed Annapurna temple between the old houses.",
                Query = "Asan", HeroId = "her.ktm.annapurna_asan", X = 529374.51, Z = 165475.44, Framing = ShotFraming.Fixed,
                EyeEast = 13.5, EyeNorth = -10.0, EyeHeightM = 1.8f, LookHeightM = 6.5f, Hours = 11.5f, FovDeg = 56f, PlayerAheadM = 6f,
            },
            new ScreenshotAnchor
            {
                Id = "basantapur_durbar", Title = "Kathmandu Durbar Square",
                Caption = "Kathmandu Durbar Square from the Basantapur side: the nine-step Maju Dega and a lion-guarded shrine, with Kasthamandap down the square.",
                Query = "Basantapur Durbar Square", HeroId = "her.ktm.maju_dega", X = 528781.57, Z = 165125.38, Framing = ShotFraming.Fixed,
                EyeEast = 26.0, EyeNorth = -20.0, EyeHeightM = 1.8f, LookEast = -14.0, LookNorth = -10.0, LookHeightM = 9f, Hours = 10f, FovDeg = 54f,
                PortraitYawDeg = 16f, PortraitTiltDeg = 0f,
            },
            new ScreenshotAnchor
            {
                Id = "patan_durbar", Title = "Patan Durbar Square",
                Caption = "Patan Durbar Square from the south: the stone Krishna Mandir behind Yoganarendra Malla's column, the palace beside it.",
                Query = "Patan Durbar Square", HeroId = "her.ptn.krishna_mandir", X = 530670.9, Z = 161750.6, Framing = ShotFraming.Fixed,
                EyeEast = 11.0, EyeNorth = -47.0, EyeHeightM = 1.8f, LookEast = 4.0, LookHeightM = 9f, Hours = 10f, FovDeg = 54f,
                Region = ValleyRegion,
            },
            new ScreenshotAnchor
            {
                Id = "bhaktapur_nyatapola", Title = "Taumadhi Square, Bhaktapur",
                Caption = "Nyatapola's five roofs over its stairway of guardians in Taumadhi Square, Bhaktapur.",
                Query = "Nyatapola", HeroId = "her.bkt.nyatapola", X = 540971.9, Z = 161620.4, Framing = ShotFraming.Fixed,
                EyeEast = -12.0, EyeNorth = -48.0, EyeHeightM = 1.8f, LookHeightM = 13f, Hours = 10f, FovDeg = 52f, PlayerAheadM = 12f,
                Region = ValleyRegion,
            },
            new ScreenshotAnchor
            {
                Id = "boudhanath", Title = "Boudhanath",
                Caption = "Boudhanath's dome, painted eyes and prayer flags, seen from the gate lane on the south side of the kora.",
                Query = "Boudhanath", HeroId = "her.ktm.boudhanath", X = 534265.57, Z = 167086.35, Framing = ShotFraming.Fixed,
                EyeEast = -18.5, EyeNorth = -71.0, EyeHeightM = 1.7f, LookHeightM = 15f, Hours = 9.5f, FovDeg = 52f,
            },
            new ScreenshotAnchor
            {
                Id = "swayambhu_stair", Title = "Swayambhu stairway",
                Caption = "The last flight of the eastern stairway up Swayambhu hill, the white stupa and its gilded spire at the top.",
                Query = "Swayambhunath", HeroId = "her.ktm.swayambhunath", X = 527212.98, Z = 166291.64, Framing = ShotFraming.Fixed,
                EyeEast = 108.0, EyeNorth = -17.6, EyeHeightM = 1.7f, LookHeightM = 10f, Hours = 9f, FovDeg = 52f,
                PlayerAheadM = 5f, PlayerSideM = 0.9f, PortraitTiltDeg = 0f,
            },
            new ScreenshotAnchor
            {
                Id = "pashupati_east_bank", Title = "Pashupatinath from the east bank",
                Caption = "Pashupatinath's golden roofs and ghats across the Bagmati, from the shrine terraces of the east bank.",
                Query = "Pashupatinath", HeroId = "her.ktm.pashupatinath", X = 532963.93, Z = 165857.51, Framing = ShotFraming.Fixed,
                EyeEast = 88.0, EyeNorth = -5.0, EyeHeightM = 6f, LookHeightM = 13f, Hours = 9f, FovDeg = 46f, PlayerAheadM = 0f,
            },
            new ScreenshotAnchor
            {
                Id = "maitighar_mandala", Title = "Maitighar Mandala",
                Caption = "Maitighar Mandala: the roundabout garden and its mandala at the start of the Singha Durbar road.",
                Query = "Maitighar Mandala", X = 530190.6, Z = 164058.8, Framing = ShotFraming.Fixed,
                EyeEast = 32.0, EyeNorth = -40.0, EyeHeightM = 14f, LookHeightM = 1f, Hours = 10.5f, FovDeg = 50f,
                PlayerAheadM = 0f, PortraitTiltDeg = 0f,
            },
            new ScreenshotAnchor
            {
                Id = "shahid_gate", Title = "Shahid Gate",
                Caption = "Shahid Gate, the martyrs' memorial arch with its busts and statue, on its island by Tundikhel.",
                Query = "Shahid Gate", X = 529654.8, Z = 164616.4, Framing = ShotFraming.Fixed,
                EyeEast = 0.0, EyeNorth = -40.0, EyeHeightM = 2.2f, LookHeightM = 8f, Hours = 11f, FovDeg = 50f, PlayerSideM = 3f,
            },
            new ScreenshotAnchor
            {
                Id = "thapathali_bridge", Title = "Bagmati bridge at Thapathali",
                Caption = "The Thapathali bridge over the Bagmati: deck, railings and piers, with the traffic between Kathmandu and Patan.",
                Query = "Thapathali", X = 529845.0, Z = 163500.0, Framing = ShotFraming.Fixed,
                EyeEast = 63.8, EyeNorth = -42.0, EyeHeightM = 3.5f, LookHeightM = 1f, Hours = 9.5f, FovDeg = 50f, PlayerAheadM = 9f, PlayerSideM = 3.3f,
            },
            new ScreenshotAnchor
            {
                Id = "valley_rim_golden_hour", Title = "The valley rim at golden hour",
                Caption = "Evening from Swayambhu hill: the city below, the valley rim and the Himalaya beyond in the last golden light.",
                Query = "Swayambhunath", X = 527258.0, Z = 166297.0, Framing = ShotFraming.Fixed,
                EyeHeightM = 28f, LookEast = 800.0, LookNorth = 800.0, LookHeightM = 0f, Hours = 17.3f, FovDeg = 46f, PlayerAheadM = 0f,
            },
        };

        /// <summary>Every anchor in tour order.</summary>
        public static IReadOnlyList<ScreenshotAnchor> All
        {
            get { return Anchors; }
        }

        /// <summary>The anchor with this id.</summary>
        public static bool TryGet(string id, out ScreenshotAnchor anchor)
        {
            for (int i = 0; i < Anchors.Length; i++)
            {
                if (string.Equals(Anchors[i].Id, id, StringComparison.Ordinal))
                {
                    anchor = Anchors[i];
                    return true;
                }
            }
            anchor = null;
            return false;
        }

        /// <summary>
        /// True when a region whose detail bounds are (<paramref name="minX"/>..<paramref name="maxZ"/>, game metres) covers
        /// (<paramref name="x"/>, <paramref name="z"/>): the bounds are widened to the leaf-tile grid
        /// (<c>2^(20 - leafLevel)</c> m cells), which is what the region's detail tiles cover (the pipeline builds every
        /// leaf tile the bounding box touches, with the extract's buffer inside).
        /// </summary>
        public static bool Covers(double minX, double minZ, double maxX, double maxZ, int leafLevel, double x, double z)
        {
            if (leafLevel < 0 || leafLevel > 20) leafLevel = 10;
            double size = Math.Pow(2.0, 20 - leafLevel);
            double x0 = Math.Floor(minX / size) * size, z0 = Math.Floor(minZ / size) * size;
            double x1 = Math.Ceiling(maxX / size) * size, z1 = Math.Ceiling(maxZ / size) * size;
            return x >= x0 && x < x1 && z >= z0 && z < z1;
        }

        /// <summary>
        /// The nearest of the top <see cref="SearchResults"/> hits for <see cref="ScreenshotAnchor.Query"/> within
        /// <see cref="SearchMatchRadiusM"/> of the anchor, or null (no query, no engine, or nothing near: then the
        /// coordinates alone place the shot). Allocates (the search does); call once per anchor.
        /// </summary>
        public static SearchEntry Confirm(SearchEngine search, ScreenshotAnchor anchor, out double distanceM)
        {
            distanceM = double.PositiveInfinity;
            if (search == null || anchor == null || string.IsNullOrEmpty(anchor.Query)) return null;
            List<SearchResult> results = search.Search(anchor.Query, SearchResults);
            SearchEntry best = null;
            for (int i = 0; i < results.Count; i++)
            {
                SearchEntry e = results[i].Entry;
                double dx = e.X - anchor.X, dz = e.Z - anchor.Z;
                double d = Math.Sqrt(dx * dx + dz * dz);
                if (d <= SearchMatchRadiusM && d < distanceM)
                {
                    distanceM = d;
                    best = e;
                }
            }
            return best;
        }
    }

    /// <summary>
    /// The camera maths of the tour (engine-free): output sizes, the field of view per orientation, the eye and look
    /// points of an anchor once the ground heights are known, where the explorer stands, and the file names.
    /// </summary>
    public static class ScreenshotFraming
    {
        public const int LandscapeWidth = 1920, LandscapeHeight = 1080;

        /// <summary>Share of the landscape shot's horizontal view the portrait shot keeps (a tall frame of the same
        /// place, not a slice of a fisheye).</summary>
        public const float PortraitWidthShare = 0.62f;

        /// <summary>The portrait vertical field of view never exceeds this.</summary>
        public const float MaxPortraitFovDeg = 88f;

        /// <summary>Chase shots look at a point this high above the explorer's feet.</summary>
        public const float ChaseLookHeightM = 1.6f;

        /// <summary>Pixel size of a shot.</summary>
        public static void Size(ShotOrientation o, out int width, out int height)
        {
            width = o == ShotOrientation.Portrait ? LandscapeHeight : LandscapeWidth;
            height = o == ShotOrientation.Portrait ? LandscapeWidth : LandscapeHeight;
        }

        /// <summary>"landscape" or "portrait".</summary>
        public static string Name(ShotOrientation o)
        {
            return o == ShotOrientation.Portrait ? "portrait" : "landscape";
        }

        /// <summary>The JPEG name of an anchor's shot: <c>&lt;id&gt;_&lt;orientation&gt;.jpg</c>.</summary>
        public static string FileName(ScreenshotAnchor anchor, ShotOrientation o)
        {
            return anchor.Id + "_" + Name(o) + ".jpg";
        }

        /// <summary>A file name for a single (F12) shot: <c>shot_yyyyMMdd_HHmmss_&lt;orientation&gt;[_hud].jpg</c>.</summary>
        public static string SingleShotName(DateTime now, ShotOrientation orientation, bool hud)
        {
            return "shot_" + now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + "_" + Name(orientation) + (hud ? "_hud" : "") + ".jpg";
        }

        /// <summary>The supported supersampling factor for a request (1, 2 or 4).</summary>
        public static int SuperSampleFactor(int requested)
        {
            return requested >= 4 ? 4 : requested >= 2 ? 2 : 1;
        }

        /// <summary>
        /// Vertical field of view for the orientation: the anchor's own in landscape; in portrait the one that keeps
        /// <see cref="PortraitWidthShare"/> of the landscape horizontal view (CameraFov maths), at least the landscape
        /// value and at most <see cref="MaxPortraitFovDeg"/>.
        /// </summary>
        public static float VerticalFovDeg(ScreenshotAnchor anchor, ShotOrientation o)
        {
            float v = anchor.FovDeg;
            if (o == ShotOrientation.Landscape) return v;
            float h = CameraFov.HorizontalFromVertical(v, (float)LandscapeWidth / LandscapeHeight) * PortraitWidthShare;
            float p = CameraFov.VerticalFromHorizontal(h, (float)LandscapeHeight / LandscapeWidth);
            return Math.Max(v, Math.Min(MaxPortraitFovDeg, p));
        }

        /// <summary>Extra upward pitch (degrees) of the shot in this orientation: <see cref="ScreenshotAnchor.PortraitTiltDeg"/>
        /// in portrait, 0 in landscape.</summary>
        public static float TiltDeg(ScreenshotAnchor anchor, ShotOrientation o)
        {
            return o == ShotOrientation.Portrait ? anchor.PortraitTiltDeg : 0f;
        }

        /// <summary>Turn (degrees clockwise seen from above) of the shot in this orientation away from the eye-to-look line:
        /// <see cref="ScreenshotAnchor.PortraitYawDeg"/> in portrait, 0 in landscape.</summary>
        public static float YawDeg(ScreenshotAnchor anchor, ShotOrientation o)
        {
            return o == ShotOrientation.Portrait ? anchor.PortraitYawDeg : 0f;
        }

        /// <summary>Plan position of the eye (game metres) before heights are known (streaming focus, ground samples).</summary>
        public static void EyeXZ(ScreenshotAnchor a, out double x, out double z)
        {
            if (a.Framing == ShotFraming.Chase)
            {
                double s, c;
                SinCos(a.BearingDeg, out s, out c);
                x = a.X - s * a.ChaseBackM;
                z = a.Z - c * a.ChaseBackM;
                return;
            }
            x = a.X + a.EyeEast;
            z = a.Z + a.EyeNorth;
        }

        /// <summary>Plan position of the look point (game metres).</summary>
        public static void LookXZ(ScreenshotAnchor a, out double x, out double z)
        {
            if (a.Framing == ShotFraming.Chase)
            {
                double s, c;
                SinCos(a.BearingDeg, out s, out c);
                x = a.X + s * a.ChaseAheadM;
                z = a.Z + c * a.ChaseAheadM;
                return;
            }
            x = a.X + a.LookEast;
            z = a.Z + a.LookNorth;
        }

        /// <summary>
        /// Eye and look heights (absolute metres) from the ground under them: Fixed shots add
        /// <see cref="ScreenshotAnchor.EyeHeightM"/> / <see cref="ScreenshotAnchor.LookHeightM"/> to the ground at the eye
        /// and at the look point; Chase shots measure both from the explorer's feet (<paramref name="playerGround"/>) and
        /// keep the eye at least 1.2 m over the ground behind (a street that climbs).
        /// </summary>
        public static void Heights(ScreenshotAnchor a, float eyeGround, float lookGround, float playerGround, out float eyeY, out float lookY)
        {
            if (a.Framing == ShotFraming.Chase)
            {
                eyeY = Math.Max(playerGround + a.EyeHeightM, eyeGround + 1.2f);
                lookY = playerGround + a.LookHeightM;
                return;
            }
            eyeY = eyeGround + a.EyeHeightM;
            lookY = lookGround + a.LookHeightM;
        }

        /// <summary>
        /// Where the explorer stands and the bearing it faces (radians clockwise from north, the game's heading): Chase on
        /// the subject facing <see cref="ScreenshotAnchor.BearingDeg"/>; Fixed <see cref="ScreenshotAnchor.PlayerAheadM"/>
        /// in front of the eye and <see cref="ScreenshotAnchor.PlayerSideM"/> to the right, facing the look point; false
        /// when the shot shows no explorer (it then waits on the ground under the eye).
        /// </summary>
        public static bool PlayerSpot(ScreenshotAnchor a, out double x, out double z, out float headingRad)
        {
            if (a.Framing == ShotFraming.Chase)
            {
                x = a.X;
                z = a.Z;
                headingRad = (float)(a.BearingDeg * Math.PI / 180.0);
                return true;
            }
            double ex, ez, lx, lz;
            EyeXZ(a, out ex, out ez);
            LookXZ(a, out lx, out lz);
            double dx = lx - ex, dz = lz - ez, len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-6)
            {
                dx = 0;
                dz = 1;
                len = 1;
            }
            dx /= len;
            dz /= len;
            if (a.PlayerAheadM <= 0f)
            {
                x = ex;
                z = ez;
                headingRad = (float)Math.Atan2(dx, dz);
                return false;
            }
            // Right of the view direction (x east, z north): (dz, -dx).
            x = ex + dx * a.PlayerAheadM + dz * a.PlayerSideM;
            z = ez + dz * a.PlayerAheadM - dx * a.PlayerSideM;
            double tx = lx - x, tz = lz - z;
            headingRad = (float)Math.Atan2(tx, tz);
            return true;
        }

        /// <summary>Compass bearing of the view (degrees clockwise from north, 0..360).</summary>
        public static double ViewBearingDeg(ScreenshotAnchor a)
        {
            double ex, ez, lx, lz;
            EyeXZ(a, out ex, out ez);
            LookXZ(a, out lx, out lz);
            double b = Math.Atan2(lx - ex, lz - ez) * 180.0 / Math.PI;
            return b < 0 ? b + 360.0 : b;
        }

        private static void SinCos(float bearingDeg, out double s, out double c)
        {
            double r = bearingDeg * Math.PI / 180.0;
            s = Math.Sin(r);
            c = Math.Cos(r);
        }
    }

    /// <summary>The Markdown index of a tour's output folder (<c>docs/images/game/README.md</c>).</summary>
    public static class ScreenshotReadme
    {
        /// <summary>What the tour did with one anchor.</summary>
        public struct Entry
        {
            public ScreenshotAnchor Anchor;
            public bool Landscape, Portrait;

            /// <summary>Why it was skipped (null when shot).</summary>
            public string Skipped;
        }

        /// <summary>
        /// The README: a table per anchor with the landscape shot inline and the portrait one linked, the skipped anchors
        /// with their reason, how the images were made and the map-data notice (docs/LICENSES.md 1.1: screenshots are
        /// Produced Works). <paramref name="regionId"/> and <paramref name="whenUtc"/> describe the capture run.
        /// </summary>
        public static string Build(IReadOnlyList<Entry> entries, string regionId, DateTime whenUtc)
        {
            var sb = new StringBuilder(4096);
            CultureInfo c = CultureInfo.InvariantCulture;
            sb.Append("# Gameplay screenshots\n\n");
            sb.Append("Captured in the Unity editor by **Ghumante > Capture Screenshot Tour** (how-to: [../README.md](../README.md)) on ")
              .Append(whenUtc.ToString("yyyy-MM-dd HH:mm", c)).Append(" UTC, region `").Append(string.IsNullOrEmpty(regionId) ? "?" : regionId)
              .Append("`. Every image is a real frame of the game (world, traffic, people and the explorer), without the HUD. ")
              .Append("Landscape 1920 x 1080 and portrait 1080 x 1920; the anchors live in ")
              .Append("`game/Assets/Ghumante/World/Debug/ScreenshotTourAnchors.cs`.\n\n");
            sb.Append("| Place | Landscape | Portrait |\n|---|---|---|\n");
            int skipped = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                if (e.Anchor == null) continue;
                if (!e.Landscape && !e.Portrait)
                {
                    skipped++;
                    continue;
                }
                string land = ScreenshotFraming.FileName(e.Anchor, ShotOrientation.Landscape);
                string port = ScreenshotFraming.FileName(e.Anchor, ShotOrientation.Portrait);
                sb.Append("| **").Append(e.Anchor.Title).Append("**<br>").Append(e.Anchor.Caption).Append(" | ");
                if (e.Landscape) sb.Append("![").Append(e.Anchor.Title).Append("](").Append(land).Append(')');
                else sb.Append('-');
                sb.Append(" | ");
                if (e.Portrait) sb.Append('[').Append(port).Append("](").Append(port).Append(')');
                else sb.Append('-');
                sb.Append(" |\n");
            }
            if (skipped > 0)
            {
                sb.Append("\nNot captured in this run:\n\n");
                for (int i = 0; i < entries.Count; i++)
                {
                    Entry e = entries[i];
                    if (e.Anchor == null || e.Landscape || e.Portrait) continue;
                    sb.Append("* **").Append(e.Anchor.Title).Append("**: ").Append(string.IsNullOrEmpty(e.Skipped) ? "skipped" : e.Skipped).Append('\n');
                }
            }
            sb.Append("\nMap data © OpenStreetMap contributors (ODbL); terrain from Copernicus WorldDEM-30 © DLR e.V. 2010-2014 and © Airbus ")
              .Append("Defence and Space GmbH 2014-2018 provided under COPERNICUS by the European Union and ESA; land cover © ESA WorldCover ")
              .Append("project 2021 (CC BY 4.0). See docs/LICENSES.md.\n");
            return sb.ToString();
        }
    }
}
#endif
