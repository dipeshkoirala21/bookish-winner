using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;
using Ghumante.Core.Traffic;
using Ghumante.World.Instancing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Owner playtest (docs/W2_PLAYTEST_1.md, screenshot at Gaurighat Hanuman Mandir by the Bagmati): "Fix the roads on
    /// slopes. Make them flat like how they make them, cutting the hills. The dog looks like it is sleeping in the air; half
    /// of the motorcycle is in the air. Fix this everywhere the roads are in the hills." Headless reproductions on the real
    /// kathmandu_core sample at the runtime terrain step: every drawn road's cross-fall, and how far parked vehicles, street
    /// animals and walkers stand off the drawn ground (road mesh and terrain mesh) on the hill roads.
    /// </summary>
    public class RoadCutHillTests
    {
        /// <summary>Steepest cross-fall of a carriageway: superelevation on curves (Nepal Road Standard: 2-3 % camber,
        /// up to about 6 % in curves).</summary>
        private const double MaxCrossFall = 0.06;

        /// <summary>Most an object's contact points may stand off the drawn ground.</summary>
        private const double MaxOffGroundM = 0.03;

        private static IEnumerable<TileData> Leaves()
        {
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (t != null && t.Roads.Count > 0) yield return t;
            }
        }

        private static string Where(TileData t, double lx, double lz)
        {
            double lon, lat;
            WorldFrame.GameToLonLat(t.Tile.X0 + lx, t.Tile.Z0 + lz, out lon, out lat);
            return lat.ToString("0.00000", CultureInfo.InvariantCulture) + "," + lon.ToString("0.00000", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Every drawn carriageway of the sample stays level across within road practice: no row of any piece (decks
        /// excluded: they are level by construction) falls more than <see cref="MaxCrossFall"/> from edge to edge.
        /// Before the cut/fill package the road banked with the hillside up to 45 % (RoadGrade.MaxSteepBank 1.0).
        /// </summary>
        [Test]
        public void NoRoadTiltsAcrossMoreThanSixPercent()
        {
            int pieces = 0, tilted = 0;
            double tiltedM = 0, worst = 0;
            var list = new List<KeyValuePair<double, string>>();
            foreach (TileData t in Leaves())
            {
                var h = TileHeightSampler.ForArea(t, t.Tile, HillPlaces.RuntimeStep);
                var o = new RoadOptions();
                RoadGrade g = RoadGrade.For(t, h, o);
                RoadLayout lay = RoadLayout.For(t);
                for (int i = 0; i < t.Roads.Count; i++)
                {
                    if (!g.Has(i) || !lay.DrawsRibbon(i) || g.IsAbsolute(i)) continue;
                    RoadCentreline c = lay.Centres[i];
                    RoadWidthProfile prof = lay.Profiles[i];
                    pieces++;
                    double pieceWorst = 0, at = 0, over = 0;
                    for (double s = 0.5; s < c.RawLengthM - 0.5; s += 1.0)
                    {
                        if (g.OnDeck(i, s) || lay.InGap(i, s)) continue;
                        double x, z, tx, tz;
                        c.At(s, out x, out z, out tx, out tz);
                        double ux = -tz, uz = tx;
                        float shift = prof.ShiftAt(s), half = 0.5f * prof.DrawnAt(s);
                        if (half < 0.5f) continue;
                        double ol = shift + half, or = shift - half;
                        float yl = g.SurfaceY(i, s, ol, x + ux * ol, z + uz * ol), yr = g.SurfaceY(i, s, or, x + ux * or, z + uz * or);
                        double tilt = Math.Abs(yl - yr) / (2.0 * half);
                        if (tilt > MaxCrossFall + 1e-3) over += 1.0;
                        if (tilt > pieceWorst)
                        {
                            pieceWorst = tilt;
                            at = s;
                        }
                    }
                    worst = Math.Max(worst, pieceWorst);
                    if (pieceWorst <= MaxCrossFall + 1e-3) continue;
                    tilted++;
                    tiltedM += over;
                    double px, pz, ptx, ptz;
                    c.At(at, out px, out pz, out ptx, out ptz);
                    NameRecord nm = t.Name(t.Roads[i].NameRef);
                    list.Add(new KeyValuePair<double, string>(pieceWorst, string.Format(CultureInfo.InvariantCulture,
                        "{0,5:0.0} % {1} road {2} ({3} '{4}') at {5:0} m, {6}, {7:0} m over", 100 * pieceWorst, t.Tile, i, t.Roads[i].RoadClass,
                        nm != null ? nm.Default : "", at, Where(t, px, pz), over)));
                }
            }
            list.Sort((a, b) => b.Key.CompareTo(a.Key));
            var sb = new StringBuilder();
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "cross-fall: {0} drawn pieces, {1} tilt more than {2:0} % across ({3:0} m of road), worst {4:0.0} %",
                                        pieces, tilted, 100 * MaxCrossFall, tiltedM, 100 * worst));
            for (int k = 0; k < Math.Min(25, list.Count); k++) sb.AppendLine("  " + list[k].Value);
            TestContext.Progress.WriteLine(sb.ToString());
            Assert.That(pieces, Is.GreaterThan(5000));
            Assert.That(tilted, Is.EqualTo(0), sb.ToString());
        }

        private struct Tally
        {
            public int Count, Over3, Over10;
            public double Float, Sink;
            public string FloatAt, SinkAt;

            public void Add(double off, string what)
            {
                Count++;
                if (Math.Abs(off) > 0.03) Over3++;
                if (Math.Abs(off) > 0.10) Over10++;
                if (off > Float)
                {
                    Float = off;
                    FloatAt = what;
                }
                if (-off > Sink)
                {
                    Sink = -off;
                    SinkAt = what;
                }
            }

            public string Line(string name)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0,-26} {1,5} contacts ({6} off by more than 3 cm, {7} by more than 10 cm), worst float {2,5:0.000} m ({3}), worst sink {4,5:0.000} m ({5})",
                                     name, Count, Float, FloatAt ?? "-", Sink, SinkAt ?? "-", Over3, Over10);
            }
        }

        /// <summary>The drawn ground of a level-10 tile as the streamer builds it on the High tier: the terrain mesh at the
        /// runtime step and the roads layer on the matching sampler.</summary>
        private static DrawnGround Drawn(TileData t, out TileHeightSampler h)
        {
            h = TileHeightSampler.ForArea(t, t.Tile, HillPlaces.RuntimeStep);
            var terrain = new MeshData();
            TerrainMesher.Build(t, t.Tile, new TerrainOptions { Step = HillPlaces.RuntimeStep }, terrain);
            var roads = new MeshData();
            RoadMesher.Build(t, h, new RoadOptions(), roads);
            var g = new DrawnGround();
            g.Add(terrain);
            g.Add(roads, 0.55f);
            return g;
        }

        /// <summary>Contact points (forward, right, up, in metres from the object's origin) of a footprint.</summary>
        private static void Contacts(List<double> into, double halfLong, double halfWide)
        {
            into.Clear();
            for (int a = -1; a <= 1; a += 2)
            for (int b = -1; b <= 1; b += 2)
            {
                into.Add(a * halfLong);
                into.Add(b * halfWide);
                into.Add(0);
            }
        }

        private static void Measure(DrawnGround g, TileData t, double gx, double gz, float y, double headingRad, List<double> contacts, ref Tally tally, string what,
                                    float sameSurfaceM = float.PositiveInfinity, float pitchDeg = 0f, float rollDeg = 0f)
        {
            double fx = Math.Sin(headingRad), fz = Math.Cos(headingRad), rx = fz, rz = -fx;
            double p = pitchDeg * Math.PI / 180.0, r = rollDeg * Math.PI / 180.0;
            for (int k = 0; k + 2 < contacts.Count; k += 3)
            {
                // Unity's Euler(pitch, yaw, roll): roll about forward, then pitch about right, then the yaw (heading).
                double lf = contacts[k], lr = contacts[k + 1], lu = contacts[k + 2];
                double r1 = lr * Math.Cos(r) - lu * Math.Sin(r), u1 = lr * Math.Sin(r) + lu * Math.Cos(r);
                double u2 = u1 * Math.Cos(p) - lf * Math.Sin(p), f2 = u1 * Math.Sin(p) + lf * Math.Cos(p);
                double cx = gx - t.Tile.X0 + fx * f2 + rx * r1;
                double cz = gz - t.Tile.Z0 + fz * f2 + rz * r1;
                float cy = y + (float)u2, ground;
                if (!g.TryHeight(cx, cz, cy, out ground)) continue;
                if (Math.Abs(cy - ground) > sameSurfaceM) continue; // a kerb or deck edge between the feet, not a slope
                tally.Add(cy - ground, what + " " + Where(t, cx, cz));
            }
        }

        /// <summary>
        /// At the owner's screenshot (Gaurighat) and the other hill roads of the sample, nothing stands off the drawn ground:
        /// parked vehicles (wheel contacts), the traffic sim's street dogs and cows (body footprint, at the height the sim
        /// gives them) and walkers on the carriageway and footpaths (feet, standing on the drawn ground at their centre) are
        /// within <see cref="MaxOffGroundM"/> of the road and terrain meshes under them.
        /// </summary>
        [Test]
        public void NothingFloatsOrSinksOnHillRoads()
        {
            var sb = new StringBuilder();
            var contacts = new List<double>();
            Tally allParked = default(Tally), allAnimals = default(Tally), allWalkers = default(Tally);
            var grounds = new Dictionary<ulong, DrawnGround>();
            LaneGraph graph = TrafficData.Graph;
            foreach (HillPlaces.Place place in HillPlaces.All)
            {
                double px, pz;
                place.Game(out px, out pz);
                TileData t = StreamingSampleRegion.Tile(place.Tile);
                if (t == null) continue;
                TileHeightSampler h;
                DrawnGround g;
                if (!grounds.TryGetValue(t.Tile.Key, out g))
                {
                    g = Drawn(t, out h);
                    grounds[t.Tile.Key] = g;
                }
                h = TileHeightSampler.ForArea(t, t.Tile, HillPlaces.RuntimeStep);
                // Parked vehicles (wheel contacts).
                Tally parked = default(Tally);
                var list = new List<ParkedVehicle>();
                ParkedPlacement.Place(t, h, list, new RoadOptions());
                foreach (ParkedVehicle v in list)
                {
                    double gx = t.Tile.X0 + v.X, gz = t.Tile.Z0 + v.Z;
                    if ((gx - px) * (gx - px) + (gz - pz) * (gz - pz) > place.RadiusM * place.RadiusM) continue;
                    VehicleCatalogEntry e = VehicleCatalog.At(v.Variant);
                    // Wheel contact points (the catalogue's wheel sockets: right, forward) at the wheel bottoms.
                    contacts.Clear();
                    foreach (WheelSocket ws in VehicleMesher.Wheels(e))
                    {
                        contacts.Add(ws.Z);
                        contacts.Add(ws.X);
                        contacts.Add(ws.Y - ws.Radius);
                    }
                    Measure(g, t, gx, gz, v.Y, v.YawDeg * Math.PI / 180.0, contacts, ref parked, "parked " + e.AssetId, float.PositiveInfinity, v.PitchDeg,
                            v.RollDeg);
                }
                // Street dogs and cows of the traffic sim around the place.
                Tally animals = default(Tally);
                // As LifeHost wires it: the animals stand on the carved ground of the committed tiles.
                var ground = new CarvedGround();
                ground.Add(CarvedGround.Prepare(t, 1));
                var sim = new AnimalSim(graph, AnimalSettings.High(), 7) { Ground = ground };
                sim.SetFocus(px, pz, (float)place.RadiusM);
                var poses = new AnimalPose[256];
                for (int step = 0; step < 6; step++)
                {
                    sim.Step(37f, 9f + step, 0f);
                    int n = sim.CopyPoses(poses);
                    for (int k = 0; k < n; k++)
                    {
                        AnimalPose a = poses[k];
                        if (!t.Tile.Contains(a.X, a.Z)) continue;
                        bool cow = a.Kind == AnimalKind.Cow;
                        bool lying = a.ClipId == (byte)AnimalClip.Sleep || a.ClipId == (byte)AnimalClip.Lie;
                        Contacts(contacts, cow ? (lying ? 0.8 : 0.7) : (lying ? 0.4 : 0.25), cow ? (lying ? 0.35 : 0.2) : (lying ? 0.15 : 0.08));
                        Measure(g, t, a.X, a.Z, a.Y, a.HeadingRad, contacts, ref animals, (cow ? "cow " : "dog ") + (lying ? "lying" : "up"), float.PositiveInfinity,
                                a.PitchDeg, a.RollDeg);
                    }
                }
                // Walkers: standing on the drawn ground at their centre, heading along the street.
                Tally walkers = default(Tally);
                RoadLayout lay = RoadLayout.For(t);
                RoadGrade grade = RoadGrade.For(t, h, new RoadOptions());
                Contacts(contacts, 0.13, 0.11);
                for (int i = 0; i < t.Roads.Count; i++)
                {
                    RoadCentreline c = lay.Centres[i];
                    if (c == null || c.Count < 2 || !lay.DrawsRibbon(i)) continue;
                    // Bridge and flyover decks are drawn at their own heights by the bridges package.
                    if (grade.Has(i) && grade.IsAbsolute(i)) continue;
                    RoadWidthProfile prof = lay.Profiles[i];
                    for (double s = 2; s < c.RawLengthM - 2; s += 6)
                    {
                        if (grade.Has(i) && grade.OnDeck(i, s)) continue;
                        double x, z, tx, tz;
                        c.At(s, out x, out z, out tx, out tz);
                        double gx0 = t.Tile.X0 + x, gz0 = t.Tile.Z0 + z;
                        if ((gx0 - px) * (gx0 - px) + (gz0 - pz) * (gz0 - pz) > place.RadiusM * place.RadiusM) continue;
                        float half = 0.5f * prof.DrawnAt(s), shift = prof.ShiftAt(s);
                        double heading = Math.Atan2(tx, tz);
                        foreach (double f in new[] { -0.7, -0.35, 0.0, 0.35, 0.7 })
                        {
                            double off = shift + f * half;
                            double wx = x - tz * off, wz = z + tx * off;
                            float y;
                            if (!g.TryHeight(wx, wz, float.PositiveInfinity, out y)) continue;
                            Measure(g, t, t.Tile.X0 + wx, t.Tile.Z0 + wz, y, heading, contacts, ref walkers, "walker", 0.5f);
                        }
                    }
                }
                sb.AppendLine(place.Name + " (" + t.Tile + "):");
                sb.AppendLine("  " + parked.Line("parked vehicles"));
                sb.AppendLine("  " + animals.Line("street animals"));
                sb.AppendLine("  " + walkers.Line("walkers"));
                Merge(ref allParked, parked);
                Merge(ref allAnimals, animals);
                Merge(ref allWalkers, walkers);
            }
            sb.AppendLine("all hill places:");
            sb.AppendLine("  " + allParked.Line("parked vehicles"));
            sb.AppendLine("  " + allAnimals.Line("street animals"));
            sb.AppendLine("  " + allWalkers.Line("walkers"));
            TestContext.Progress.WriteLine(sb.ToString());
            Assert.That(allWalkers.Count, Is.GreaterThan(1000));
            Assert.That(allParked.Count + allAnimals.Count, Is.GreaterThan(20));
            Assert.That(Math.Max(allParked.Float, allParked.Sink), Is.LessThanOrEqualTo(MaxOffGroundM), sb.ToString());
            Assert.That(Math.Max(allAnimals.Float, allAnimals.Sink), Is.LessThanOrEqualTo(MaxOffGroundM), sb.ToString());
            Assert.That(Math.Max(allWalkers.Float, allWalkers.Sink), Is.LessThanOrEqualTo(MaxOffGroundM), sb.ToString());
        }

        private static void Merge(ref Tally all, in Tally t)
        {
            all.Count += t.Count;
            all.Over3 += t.Over3;
            all.Over10 += t.Over10;
            if (t.Float > all.Float)
            {
                all.Float = t.Float;
                all.FloatAt = t.FloatAt;
            }
            if (t.Sink > all.Sink)
            {
                all.Sink = t.Sink;
                all.SinkAt = t.SinkAt;
            }
        }
    }
}
