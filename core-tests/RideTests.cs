using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Bridges;
using Ghumante.Core.Meshing.Roads;
using Ghumante.Core.Traffic;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Rides and walks on the sample region with the game's ground query (owner: "make roads very much rideable ... if
    /// there is a road going from underneath the bridge ... right now it gets stuck"): the Kalanki Ring Road underpass
    /// (tagged tunnel=yes, RSTR kind Underpass) and the dhoka passages of Asan (tunnel=building_passage, RSTR kind
    /// Passage) are drawn, indexed, laned and rideable; only RSTR kind Tunnel stays out.
    /// </summary>
    public class RideTests
    {
        // Kalanki: the Ring Road through lanes dip under the junction (bridges_flyovers.md §3.3), 5-7 m below the terrain.
        private static readonly ulong[] KalankiSouthbound = { 1090820450UL, 1090820451UL, 1090820449UL };
        private static readonly ulong[] KalankiNorthbound = { 653848630UL, 225457469UL, 1090820447UL, 653848641UL, 1090820453UL };
        private static readonly ulong[] KalankiUnderpass = { 225457469UL, 1090820447UL, 653848641UL, 1090820451UL };

        // Asan / Indra Chowk dhoka passages into the bahal courtyards (data_detail_pass.md §10).
        private const double AsanLon = 85.3112, AsanLat = 27.7073;

        /// <summary>The game's ground query (WorldRoot.Build: deck and corridor factories) over the sample's leaf tiles
        /// within <paramref name="radius"/> metres of a point.</summary>
        internal static TileGroundQuery Ground(double lon, double lat, double radius)
        {
            double x, z;
            Geo.WorldFrame.LonLatToGame(lon, lat, out x, out z);
            var g = new TileGroundQuery
            {
                CorridorFactory = RoadCorridorIndex.ForTile,
                DeckFactory = BridgeDeckIndex.ForTile,
            };
            foreach (TileData t in DrivingData.SampleTiles().Values)
            {
                if (t.Tile.Level != 10 || !t.HasDetail) continue;
                double cx = Math.Max(t.Tile.X0, Math.Min(x, t.Tile.X0 + t.Tile.Size));
                double cz = Math.Max(t.Tile.Z0, Math.Min(z, t.Tile.Z0 + t.Tile.Size));
                if ((cx - x) * (cx - x) + (cz - z) * (cz - z) > radius * radius) continue;
                g.Add(t);
            }
            return g;
        }

        /// <summary>The rendered polylines of every leaf-tile piece of the given ways chained in travel order (each next
        /// piece starts where the last one ends, a tile-border cut or a node), in game metres.</summary>
        internal static void Chain(ulong[] ways, List<double> px, List<double> pz)
        {
            var pieces = new List<double[]>();
            var wayOf = new List<ulong>();
            foreach (TileData t in DrivingData.SampleTiles().Values)
            {
                if (t.Tile.Level != 10) continue;
                for (int i = 0; i < t.Roads.Count; i++)
                {
                    RoadRecord r = t.Roads[i];
                    if (Array.IndexOf(ways, r.OsmWayId) < 0) continue;
                    int first, last;
                    RoadSpatialIndex.RenderedRange(r, out first, out last);
                    if (last <= first) continue;
                    var p = new double[2 * (last - first + 1)];
                    for (int k = first; k <= last; k++) t.LocalToGame(r.Points[2 * k], r.Points[2 * k + 1], out p[2 * (k - first)], out p[2 * (k - first) + 1]);
                    pieces.Add(p);
                    wayOf.Add(r.OsmWayId);
                }
            }
            Assert.That(pieces.Count, Is.GreaterThanOrEqualTo(ways.Length), "every way of the route is in the sample");
            // Start: a piece of the first way whose start no other piece ends at.
            int cur = -1;
            for (int i = 0; i < pieces.Count && cur < 0; i++)
            {
                if (wayOf[i] != ways[0]) continue;
                bool continues = false;
                for (int j = 0; j < pieces.Count; j++)
                    if (j != i && Near(pieces[j], pieces[j].Length - 2, pieces[i], 0)) continues = true;
                if (!continues) cur = i;
            }
            Assert.That(cur, Is.GreaterThanOrEqualTo(0), "the route has a start");
            var used = new bool[pieces.Count];
            px.Clear();
            pz.Clear();
            while (cur >= 0)
            {
                used[cur] = true;
                double[] p = pieces[cur];
                for (int k = px.Count == 0 ? 0 : 1; k < p.Length / 2; k++)
                {
                    px.Add(p[2 * k]);
                    pz.Add(p[2 * k + 1]);
                }
                int next = -1;
                for (int j = 0; j < pieces.Count && next < 0; j++)
                    if (!used[j] && Near(p, p.Length - 2, pieces[j], 0)) next = j;
                cur = next;
            }
            for (int i = 0; i < pieces.Count; i++)
                Assert.That(used[i], Is.True, "piece of way " + wayOf[i] + " joins the route");
        }

        private static bool Near(double[] a, int ia, double[] b, int ib)
        {
            double dx = a[ia] - b[ib], dz = a[ia + 1] - b[ib + 1];
            return dx * dx + dz * dz < 0.3 * 0.3;
        }

        /// <summary>How a ride along a path went.</summary>
        internal struct RideResult
        {
            public bool Finished;
            public double Reached, Length;
            public float MinBelowTerrain;
            public string Why;
        }

        /// <summary>
        /// Rides a vehicle of <paramref name="spec"/> along the polyline with a pure-pursuit driver at about
        /// <paramref name="cruise"/> m/s on <paramref name="g"/> (no road assist): finished when it gets within 2 m of the
        /// end, stuck when it makes no progress for 4 s. Also records how far under the terrain it rode.
        /// </summary>
        internal static RideResult Ride(TileGroundQuery g, VehicleSpec spec, List<double> px, List<double> pz, float cruise)
        {
            var ps = new double[px.Count];
            for (int k = 1; k < px.Count; k++) ps[k] = ps[k - 1] + Math.Sqrt(Sq(px[k] - px[k - 1]) + Sq(pz[k] - pz[k - 1]));
            double total = ps[ps.Length - 1];
            var res = new RideResult { Length = total, MinBelowTerrain = 0f };
            var v = new ArcadeVehicle(spec) { RoadAssist = false };
            double sx, sz, hx, hz;
            At(px, pz, ps, 0.5, out sx, out sz, out hx, out hz);
            v.Teleport(sx, sz, (float)Math.Atan2(hx, hz), g);
            const float dt = 1f / 30f;
            double best = 0.5, bestAt = 0, time = 0, maxTime = total / Math.Max(1.0, 0.5 * cruise) + 20;
            while (time < maxTime)
            {
                double s = Project(px, pz, ps, v.X, v.Z, best);
                if (s > best + 0.3)
                {
                    best = s;
                    bestAt = time;
                }
                if (best > total - 2)
                {
                    res.Finished = true;
                    break;
                }
                if (time - bestAt > 4.0)
                {
                    res.Why = "stuck at " + best.ToString("0.0") + " m, y " + v.Y.ToString("0.00");
                    break;
                }
                double tx, tz;
                At(px, pz, ps, Math.Min(total, best + 3.0), out tx, out tz, out hx, out hz);
                double err = Math.Atan2(tx - v.X, tz - v.Z) - v.HeadingRad;
                while (err > Math.PI) err -= 2 * Math.PI;
                while (err < -Math.PI) err += 2 * Math.PI;
                float steer = (float)Math.Max(-1, Math.Min(1, err * 2.5));
                float want = Math.Abs(err) > 0.5 ? 0.5f * cruise : cruise;
                float thr = (float)Math.Max(0, Math.Min(0.7, (want - v.SpeedMps) * 0.3));
                float brk = v.SpeedMps > want + 1f ? 0.6f : 0f;
                v.Step(new DriveInput(thr, brk, steer), dt, g, 0f);
                Assert.That(float.IsNaN(v.Y) || float.IsInfinity(v.Y), Is.False, "finite vehicle height");
                float ter;
                if (g.TryTerrainHeight(v.X, v.Z, out ter)) res.MinBelowTerrain = Math.Min(res.MinBelowTerrain, v.Y - ter);
                time += dt;
            }
            if (!res.Finished && res.Why == null) res.Why = "timeout at " + best.ToString("0.0") + " m";
            res.Reached = best;
            return res;
        }

        private static double Sq(double v)
        {
            return v * v;
        }

        private static void At(List<double> px, List<double> pz, double[] ps, double s, out double x, out double z, out double hx, out double hz)
        {
            int n = ps.Length;
            for (int k = 0; k + 1 < n; k++)
            {
                if (s > ps[k + 1] && k + 2 < n) continue;
                double len = ps[k + 1] - ps[k];
                double f = len > 1e-9 ? Math.Max(0, Math.Min(1, (s - ps[k]) / len)) : 0;
                x = px[k] + (px[k + 1] - px[k]) * f;
                z = pz[k] + (pz[k + 1] - pz[k]) * f;
                hx = (px[k + 1] - px[k]) / Math.Max(1e-9, len);
                hz = (pz[k + 1] - pz[k]) / Math.Max(1e-9, len);
                return;
            }
            x = px[n - 1];
            z = pz[n - 1];
            hx = 0;
            hz = 1;
        }

        private static double Project(List<double> px, List<double> pz, double[] ps, double x, double z, double near)
        {
            double best = double.MaxValue, bs = near;
            for (int k = 0; k + 1 < ps.Length; k++)
            {
                if (ps[k + 1] < near - 15 || ps[k] > near + 15) continue;
                double dx = px[k + 1] - px[k], dz = pz[k + 1] - pz[k], l2 = dx * dx + dz * dz;
                double t = l2 > 0 ? Math.Max(0, Math.Min(1, ((x - px[k]) * dx + (z - pz[k]) * dz) / l2)) : 0;
                double d = Sq(px[k] + dx * t - x) + Sq(pz[k] + dz * t - z);
                if (d < best)
                {
                    best = d;
                    bs = ps[k] + (ps[k + 1] - ps[k]) * t;
                }
            }
            return best < 16.0 ? bs : near;
        }

        /// <summary>
        /// The tunnel-tagged pieces of the Kalanki underpass (RSTR Underpass, car accessible, lowered 5-7 m) are ordinary
        /// drawn roads: the mesher draws them, the ground index, the clear corridors and the lane graph hold them (car lanes
        /// down in the cutting), and only RSTR kind Tunnel would stay out.
        /// </summary>
        [Test]
        public void KalankiUnderpassIsDrawnIndexedAndLaned()
        {
            int pieces = 0, carLanes = 0;
            float deepestLane = 0f;
            foreach (TileData t in DrivingData.SampleTiles().Values)
            {
                if (t.Tile.Level != 10) continue;
                RoadSpatialIndex idx = null;
                RoadCorridorIndex corridors = null;
                LaneGraph lanes = null;
                var h = new TileHeightSampler(t, 1);
                for (int i = 0; i < t.Roads.Count; i++)
                {
                    RoadRecord r = t.Roads[i];
                    if (Array.IndexOf(KalankiUnderpass, r.OsmWayId) < 0) continue;
                    RoadStructureRecord st = t.RoadStructureOf(i);
                    Assert.That((r.Flags & RoadFlags.Tunnel) != 0, Is.True, "tagged tunnel=yes in OSM");
                    Assert.That(st.Kind, Is.EqualTo(RoadStructureKind.Underpass));
                    Assert.That(RoadMesher.IsTunnel(t, i), Is.False);
                    Assert.That(RoadMesher.IsDrawn(t, i, new RoadOptions()), Is.True, "drawn");
                    Assert.That(RoadAccess.CarAllowed(t, i, 7f), Is.True, "a car road, as the GHRG car route has it");
                    pieces++;
                    idx = idx ?? new RoadSpatialIndex(t);
                    corridors = corridors ?? RoadCorridorIndex.ForTile(t);
                    int first, last;
                    RoadSpatialIndex.RenderedRange(r, out first, out last);
                    int k = (first + last) / 2;
                    double x, z;
                    t.LocalToGame(r.Points[2 * k], r.Points[2 * k + 1], out x, out z);
                    RoadHit hit;
                    Assert.That(idx.TryNearest(x, z, 0.0, out hit), Is.True, "in the ground index");
                    Assert.That(corridors.SignedDistance(x, z), Is.LessThan(0.0), "inside a clear corridor");
                    if (lanes == null)
                    {
                        lanes = new LaneGraph();
                        lanes.AddTile(t.Tile, t);
                    }
                    foreach (LaneId l in lanes.AllLanes())
                    {
                        LaneInfo li = lanes.Info(l);
                        if (li.WayId != (long)r.OsmWayId || (li.ClassMask & VehicleClasses.Bit(VehicleClass.Car)) == 0) continue;
                        carLanes++;
                        for (int q = 0; q < li.PointCount; q++)
                        {
                            float ter;
                            if (h.TryHeightClamped(li.X[q], li.Z[q], out ter)) deepestLane = Math.Min(deepestLane, li.Y[q] - ter);
                        }
                    }
                }
            }
            TestContext.WriteLine("Kalanki underpass pieces " + pieces + ", car lanes " + carLanes + ", deepest lane point " + deepestLane.ToString("0.0") + " m under the terrain");
            Assert.That(pieces, Is.GreaterThanOrEqualTo(4));
            Assert.That(carLanes, Is.GreaterThanOrEqualTo(4), "car lanes through the underpass");
            Assert.That(deepestLane, Is.LessThan(-4f), "the lanes run down in the cutting, not over the terrain");
        }

        /// <summary>
        /// A motorbike rides the Kalanki Ring Road underpass end to end in both directions on the game's ground query: down
        /// the approach cutting, 5-7 m under the terrain beneath the junction (the junction road at grade overhead is another
        /// level, not a wall), and up the other side, without getting stuck.
        /// </summary>
        [Test]
        public void RidesTheKalankiUnderpassEndToEnd()
        {
            TileGroundQuery g = Ground(85.2817, 27.6930, 700);
            foreach (ulong[] route in new[] { KalankiSouthbound, KalankiNorthbound })
            {
                var px = new List<double>();
                var pz = new List<double>();
                Chain(route, px, pz);
                RideResult res = Ride(g, VehicleSpec.Motorbike(), px, pz, 7f);
                TestContext.WriteLine("route from way " + route[0] + ": " + res.Length.ToString("0") + " m, reached " + res.Reached.ToString("0") +
                                      " m, deepest " + res.MinBelowTerrain.ToString("0.0") + " m under the terrain " + (res.Why ?? ""));
                Assert.That(res.Length, Is.GreaterThan(300.0));
                Assert.That(res.Finished, Is.True, "rode from way " + route[0] + " to the end: " + res.Why);
                Assert.That(res.MinBelowTerrain, Is.LessThan(-4f), "it rode through the underpass, under the junction");
            }
        }

        /// <summary>
        /// A walker walks through each dhoka passage of Asan (RSTR kind Passage under a house that stays whole) from one end
        /// to the other: the passage is drawn and in the ground index, the drawn house's ground floor is open along the
        /// gateway and its storeys stay solid above the gateway's free height.
        /// </summary>
        [Test]
        public void WalksThroughTheAsanDhokaPassages()
        {
            double ax, az;
            Geo.WorldFrame.LonLatToGame(AsanLon, AsanLat, out ax, out az);
            TileGroundQuery g = Ground(AsanLon, AsanLat, 300);
            int walked = 0, underHouse = 0;
            foreach (TileData t in DrivingData.SampleTiles().Values)
            {
                if (t.Tile.Level != 10) continue;
                for (int i = 0; i < t.Roads.Count; i++)
                {
                    RoadRecord r = t.Roads[i];
                    RoadStructureRecord st = t.RoadStructureOf(i);
                    if (st.Kind != RoadStructureKind.Passage || (r.Flags & RoadFlags.Tunnel) == 0) continue;
                    int first, last;
                    RoadSpatialIndex.RenderedRange(r, out first, out last);
                    double x0, z0;
                    t.LocalToGame(r.Points[2 * first], r.Points[2 * first + 1], out x0, out z0);
                    if (Math.Abs(x0 - ax) > 250 || Math.Abs(z0 - az) > 250) continue;
                    Assert.That(RoadMesher.IsDrawn(t, i, new RoadOptions()), Is.True, "passage " + r.OsmWayId + " drawn");
                    var px = new List<double>();
                    var pz = new List<double>();
                    for (int k = first; k <= last; k++)
                    {
                        double x, z;
                        t.LocalToGame(r.Points[2 * k], r.Points[2 * k + 1], out x, out z);
                        px.Add(x);
                        pz.Add(z);
                    }
                    double len = 0;
                    for (int k = 1; k < px.Count; k++) len += Math.Sqrt(Sq(px[k] - px[k - 1]) + Sq(pz[k] - pz[k - 1]));
                    if (len < 4) continue;
                    RideResult res = Ride(g, VehicleSpec.Walker(), px, pz, 1.4f);
                    Assert.That(res.Finished, Is.True, "walked through passage " + r.OsmWayId + " (" + len.ToString("0.0") + " m): " + res.Why);
                    walked++;
                    // Over the middle of the passage, inside a house, a body above the gateway's free height is in its storeys.
                    int mid = px.Count / 2;
                    double mx = 0.5 * (px[mid - 1 >= 0 ? mid - 1 : 0] + px[mid]), mz = 0.5 * (pz[mid - 1 >= 0 ? mid - 1 : 0] + pz[mid]);
                    GroundSample s;
                    if (!g.TrySample(mx, mz, out s)) continue;
                    if (!InsideFootprint(t, mx, mz)) continue;
                    underHouse++;
                    float clear = st.ClearanceM > 0f ? st.ClearanceM : RoadClearance.MinOverheadClearanceM;
                    Assert.That(g.InsideSolid(mx, mz, s.Height), Is.False, "the gateway of " + r.OsmWayId + " is open");
                    Assert.That(g.InsideSolid(mx, mz, s.Height + clear + 0.5f), Is.True, "the storeys over " + r.OsmWayId + " stay solid");
                }
            }
            TestContext.WriteLine("Asan dhoka passages walked " + walked + ", " + underHouse + " checked under their house");
            Assert.That(walked, Is.GreaterThanOrEqualTo(10));
            Assert.That(underHouse, Is.GreaterThanOrEqualTo(3));
        }

        /// <summary>True when (x, z) lies inside a house as the building meshers draw it (the corridor guard's outlines;
        /// a house the guard drops for standing in a road is not drawn, so a passage under it runs in the open).</summary>
        private static bool InsideFootprint(TileData t, double x, double z)
        {
            double lx = x - t.Tile.X0, lz = z - t.Tile.Z0;
            BuildingFootprints drawn = BuildingFootprints.For(t, RoadCorridorIndex.ForTile(t));
            for (int k = 0; k < t.Buildings.Count; k++)
            {
                if (drawn.Dropped(k)) continue;
                BuildingRecord b = drawn.Record(k);
                if (b.Rings == null || b.Rings.Length == 0 || b.Rings[0] == null) continue;
                int[] ring = b.Rings[0];
                int n = ring.Length / 2;
                bool inside = false;
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    double xi = ring[2 * i] / 100.0, zi = ring[2 * i + 1] / 100.0, xj = ring[2 * j] / 100.0, zj = ring[2 * j + 1] / 100.0;
                    if ((zi > lz) != (zj > lz) && lx < (xj - xi) * (lz - zi) / (zj - zi) + xi) inside = !inside;
                }
                if (inside) return true;
            }
            return false;
        }
    }
}
