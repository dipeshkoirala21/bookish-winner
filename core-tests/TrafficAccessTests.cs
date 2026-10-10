using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;
using Ghumante.Core.Traffic;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>Smarter traffic (docs/W2_DETAIL_CONTRACT.md decision 5, owner: "Add busses"): cars, taxis, buses and
    /// trucks keep to car-accessible roads while motorbikes and bicycles go anywhere; every real route runs buses at
    /// 4–8 minute peak headways that stop at the real stops, so several are near the player in the city.</summary>
    public class TrafficAccessTests
    {
        private const float Dt = 1f / 20f;
        private const long MainWay = 1001, GalliWay = 1002, LaneWay = 1003;

        private static readonly uint CarBits = VehicleClasses.All & ~VehicleClasses.NarrowStreet;

        /// <summary>A flat tile: a main street along z = 500 (two pieces meeting at x = 500), a galli north from that
        /// junction and a lane south from it.</summary>
        private static TileData Streets(double galliRealM, double laneRealM, bool structures, bool galliCarAccessible = true)
        {
            TileData t = DrivingData.Flat(10, 520, 160, 1300.0);
            RoadRecord a = DrivingData.Road(RoadClass.Residential, Surface.Asphalt, 8, RoadFlags.None, 100, 500, 500, 500);
            RoadRecord b = DrivingData.Road(RoadClass.Residential, Surface.Asphalt, 8, RoadFlags.None, 500, 500, 900, 500);
            RoadRecord galli = DrivingData.Road(RoadClass.Residential, Surface.Brick, galliRealM, RoadFlags.None, 500, 500, 500, 700, 500, 900);
            RoadRecord lane = DrivingData.Road(RoadClass.Residential, Surface.Asphalt, laneRealM, RoadFlags.None, 500, 500, 500, 300, 500, 100);
            a.OsmWayId = MainWay;
            b.OsmWayId = MainWay;
            galli.OsmWayId = GalliWay;
            lane.OsmWayId = LaneWay;
            t.Roads.Add(a);
            t.Roads.Add(b);
            t.Roads.Add(galli);
            t.Roads.Add(lane);
            if (structures)
            {
                RoadStructureRecord car = RoadStructureRecord.Absent;
                t.RoadStructures.Add(car);
                t.RoadStructures.Add(car);
                t.RoadStructures.Add(galliCarAccessible ? car : new RoadStructureRecord { Flags = RoadStructureFlags.None });
                t.RoadStructures.Add(car);
            }
            return t;
        }

        private static uint MaskOf(LaneGraph g, long way)
        {
            uint m = 0;
            foreach (LaneId l in g.LanesOfWay(way))
            {
                LaneInfo i = g.Info(l);
                if (i.Kind == LaneKind.Road) m |= i.ClassMask;
            }
            return m;
        }

        [Test]
        public void CarRoutesFollowTheAccessRule()
        {
            // Structure records decide: a 4 m galli the data marks as not car accessible gets motorbikes and bicycles only.
            var g = new LaneGraph();
            TileData t = Streets(4, 6, true, false);
            g.AddTile(t.Tile, t);
            Assert.That(MaskOf(g, GalliWay) & CarBits, Is.EqualTo(0u), "no car lanes in the galli");
            Assert.That(MaskOf(g, GalliWay) & VehicleClasses.Bit(VehicleClass.TwoWheeler), Is.Not.EqualTo(0u), "motorbikes may");
            Assert.That(MaskOf(g, GalliWay) & VehicleClasses.Bit(VehicleClass.Bicycle), Is.Not.EqualTo(0u), "bicycles may");
            Assert.That(MaskOf(g, MainWay) & VehicleClasses.Bit(VehicleClass.Car), Is.Not.EqualTo(0u));
            Assert.That(MaskOf(g, LaneWay) & VehicleClasses.Bit(VehicleClass.Car), Is.Not.EqualTo(0u));

            // Without records the real width decides: under 3 m no cars, however wide the game draws the street.
            var g2 = new LaneGraph();
            TileData t2 = Streets(2.5, 3.5, false);
            g2.AddTile(t2.Tile, t2);
            Assert.That(MaskOf(g2, GalliWay) & CarBits, Is.EqualTo(0u));
            Assert.That(MaskOf(g2, GalliWay) & VehicleClasses.Bit(VehicleClass.TwoWheeler), Is.Not.EqualTo(0u));
            Assert.That(RoadAccess.CarAllowed(t2, 2, 2.5f), Is.False);
            Assert.That(RoadAccess.CarAllowed(t2, 3, 3.5f), Is.True);
            RoadRecord foot = DrivingData.Road(RoadClass.Footway, Surface.Concrete, 6, RoadFlags.None, 0, 0, 10, 0);
            t2.Roads.Add(foot);
            Assert.That(RoadAccess.CarAllowed(t2, t2.Roads.Count - 1, 6f), Is.False, "a footway is no car street at any width");
        }

        [Test]
        public void AiCarsNeverEnterNarrowStreets()
        {
            var g = new LaneGraph();
            TileData t = Streets(4, 6, true, false);
            g.AddTile(t.Tile, t);
            var galliLanes = new HashSet<int>();
            foreach (LaneId l in g.LanesOfWay(GalliWay)) galliLanes.Add(l.Value);
            var sim = new TrafficSim(g, TrafficSettings.High(), 11);
            sim.SetFocus(t.Tile.X0 + 500, t.Tile.Z0 + 500, 0f);
            var poses = new AgentPose[256];
            int inGalli = 0, carsSeen = 0;
            for (int i = 0; i < 20 * 600; i++)
            {
                sim.Step(Dt, 9f, 0f);
                if (i % 4 != 0) continue;
                int n = sim.CopyPoses(poses);
                for (int k = 0; k < n; k++)
                {
                    LaneId lane;
                    float s, v;
                    if (!sim.TryGetAgent(poses[k].AgentId, out lane, out s, out v)) continue;
                    bool narrow = (VehicleClasses.NarrowStreet & VehicleClasses.Bit(poses[k].Class)) != 0;
                    if (!narrow) carsSeen++;
                    LaneInfo info = g.Info(lane);
                    bool galli = galliLanes.Contains(lane.Value) || info.WayId == GalliWay;
                    if (galli) inGalli++;
                    Assert.That(!galli || narrow, Is.True, poses[k].Class + " in the galli");
                    if (info.Kind == LaneKind.Connector) Assert.That(info.ClassMask & CarBits & VehicleClasses.Bit(poses[k].Class),
                                                                      Is.EqualTo(narrow ? 0u : VehicleClasses.Bit(poses[k].Class)));
                }
            }
            Assert.That(carsSeen, Is.GreaterThan(0), "cars about on the main street");
            Assert.That(inGalli, Is.GreaterThan(0), "motorbikes use the galli");
        }

        /// <summary>Ways of the sample whose every drawn piece is closed to cars (real width under 3 m, foot classes, access).</summary>
        private static HashSet<long> NoCarWays(out int pieces)
        {
            var noCar = new HashSet<long>();
            var car = new HashSet<long>();
            pieces = 0;
            foreach (TileData t in DrivingData.SampleTiles().Values)
            {
                if (t.Tile.Level != 10 || t.Roads.Count == 0) continue;
                RoadLayout lay = RoadLayout.For(t);
                for (int ri = 0; ri < t.Roads.Count; ri++)
                {
                    long w = (long)t.Roads[ri].OsmWayId;
                    if (RoadAccess.CarAllowed(t, ri, lay.Profiles[ri].RealM))
                    {
                        car.Add(w);
                    }
                    else
                    {
                        noCar.Add(w);
                        pieces++;
                    }
                }
            }
            noCar.ExceptWith(car);
            return noCar;
        }

        [Test]
        public void RealNarrowStreetsCarryNoCars()
        {
            int pieces;
            HashSet<long> noCar = NoCarWays(out pieces);
            Assert.That(noCar.Count, Is.GreaterThan(50), "Kathmandu's galli");
            LaneGraph g = TrafficData.Graph;
            int narrowLanes = 0;
            foreach (LaneId l in g.AllLanes())
            {
                LaneInfo i = g.Info(l);
                if (i.Kind != LaneKind.Road || !noCar.Contains(i.WayId)) continue;
                narrowLanes++;
                Assert.That(i.ClassMask & CarBits, Is.EqualTo(0u), "way " + i.WayId);
            }
            Assert.That(narrowLanes, Is.GreaterThan(20), "motorbike lanes in the galli");

            // Traffic around the galli nearest Thamel Marg: no car, taxi, bus or truck on them.
            double tx, tz, x = 0, z = 0, best = double.MaxValue;
            DrivingData.ThamelMarg(out tx, out tz);
            foreach (LaneId l in g.AllLanes())
            {
                LaneInfo i = g.Info(l);
                if (i.Kind != LaneKind.Road || !noCar.Contains(i.WayId) || (i.ClassMask & VehicleClasses.Bit(VehicleClass.TwoWheeler)) == 0) continue;
                int mid = i.PointCount / 2;
                double d = (i.X[mid] - tx) * (i.X[mid] - tx) + (i.Z[mid] - tz) * (i.Z[mid] - tz);
                if (d >= best) continue;
                best = d;
                x = i.X[mid];
                z = i.Z[mid];
            }
            var sim = new TrafficSim(g, TrafficSettings.High(), 5) { Sacred = TrafficData.Zones };
            sim.SetFocus(x, z, 0f);
            var poses = new AgentPose[256];
            int narrowSeen = 0;
            for (int i = 0; i < 20 * 300; i++)
            {
                sim.Step(Dt, 9f, 0f);
                if (i % 10 != 0) continue;
                int n = sim.CopyPoses(poses);
                for (int k = 0; k < n; k++)
                {
                    LaneId lane;
                    float s, v;
                    if (!sim.TryGetAgent(poses[k].AgentId, out lane, out s, out v)) continue;
                    if (!noCar.Contains(g.Info(lane).WayId)) continue;
                    narrowSeen++;
                    Assert.That(VehicleClasses.NarrowStreet & VehicleClasses.Bit(poses[k].Class), Is.Not.EqualTo(0u), poses[k].Class + " in a galli");
                }
            }
            Assert.That(narrowSeen, Is.GreaterThan(0), "two-wheelers in the galli");
        }

        [Test]
        public void EveryRouteRunsAtRealisticPeakHeadways()
        {
            RouteSet routes = TrafficData.Routes;
            int served = 0;
            foreach (TransitRoute r in routes.Routes)
            {
                if (r.Mode != TransitMode.Bus && r.Mode != TransitMode.Microbus && r.Mode != TransitMode.Tempo) continue;
                served++;
                float peak = BusRouteRunner.PeakHeadwayS(r);
                Assert.That(peak, Is.InRange(240f, 480f), r.Id);
                Assert.That(BusRouteRunner.HeadwayS(r, 9f), Is.EqualTo(peak), "peak");
                Assert.That(BusRouteRunner.HeadwayS(r, 17.5f), Is.EqualTo(peak), "evening peak");
                float noon = BusRouteRunner.HeadwayS(r, 13f);
                Assert.That(noon, Is.GreaterThan(peak).And.LessThanOrEqualTo(peak * BusRouteRunner.MaxOffPeakStretch + 1e-3f));
                Assert.That(BusRouteRunner.HeadwayS(r, 2f), Is.LessThanOrEqualTo(peak * BusRouteRunner.MaxOffPeakStretch + 1e-3f));
                Assert.That(BusRouteRunner.PeakHeadwayS(r), Is.EqualTo(peak), "stable per route");
            }
            Assert.That(served, Is.GreaterThan(30), "the valley's routes");
        }

        /// <summary>Routed vehicles within <paramref name="radius"/> of (x, z) in the last snapshot.</summary>
        private static int BusesNear(TrafficSim sim, AgentPose[] poses, double x, double z, double radius)
        {
            int n = sim.CopyPoses(poses), c = 0;
            for (int k = 0; k < n; k++)
            {
                VehicleClass cl = poses[k].Class;
                if (cl != VehicleClass.Bus && cl != VehicleClass.Minibus && cl != VehicleClass.Microbus && cl != VehicleClass.Tempo) continue;
                double dx = poses[k].X - x, dz = poses[k].Z - z;
                if (dx * dx + dz * dz <= radius * radius) c++;
            }
            return c;
        }

        [Test]
        public void SeveralBusesAreAlwaysNearThePlayerInTheCity()
        {
            LaneGraph g = TrafficData.Graph;
            foreach (double[] spot in new[] { TrafficData.RingRoadChabahil, RatnaPark })
            {
                double x, z;
                TrafficData.Game(spot, out x, out z);
                var sim = new TrafficSim(g, TrafficSettings.Mid(), 21) { Sacred = TrafficData.Zones };
                var bus = new BusRouteRunner(TrafficData.Routes, g, sim);
                sim.SetFocus(x, z, 0f);
                var poses = new AgentPose[256];
                int samples = 0, total = 0, min = int.MaxValue;
                for (int i = 0; i < 20 * 60 * 8; i++)
                {
                    float hour = 8.5f + i * Dt / 3600f;
                    bus.Step(Dt, hour);
                    sim.Step(Dt, hour, 0f);
                    if (i < 20 * 60 || i % 200 != 0) continue;
                    int c = BusesNear(sim, poses, x, z, sim.Settings.FullRadiusM);
                    samples++;
                    total += c;
                    min = Math.Min(min, c);
                }
                Assert.That(bus.SegmentCount, Is.GreaterThan(5), "routes resident around the spot");
                Assert.That(min, Is.GreaterThanOrEqualTo(2), "buses always about at " + spot[0] + "," + spot[1]);
                Assert.That(total / (double)samples, Is.GreaterThanOrEqualTo(3.0), "several on average");
                Assert.That(sim.AgentCount, Is.LessThanOrEqualTo(sim.Settings.MaxVehicles + 2), "the moving-vehicle cap holds");
            }
        }

        /// <summary>Ratna Park, the old bus park by Tundikhel.</summary>
        private static readonly double[] RatnaPark = { 85.3153, 27.7066 };

        [Test]
        public void BusesStopAtTheRealStops()
        {
            LaneGraph g = TrafficData.Graph;
            RouteSet routes = TrafficData.Routes;
            int stops = 0;
            double worst = 0;
            foreach (double[] spot in new[] { TrafficData.RingRoadChabahil, RatnaPark })
            {
                double x, z;
                TrafficData.Game(spot, out x, out z);
                var sim = new TrafficSim(g, TrafficSettings.High(), 4) { Sacred = TrafficData.Zones };
                var bus = new BusRouteRunner(routes, g, sim);
                sim.SetFocus(x, z, 0f);
                sim.StopReached += (agent, route, stop) =>
                {
                    LaneId lane;
                    float s, v;
                    if (!sim.TryGetAgent(agent, out lane, out s, out v)) return;
                    double px, pz;
                    float py, ph;
                    g.PointAt(lane, s, out px, out pz, out py, out ph);
                    TransitStop st = routes.Routes[route].Stops[stop];
                    worst = Math.Max(worst, Math.Sqrt((px - st.X) * (px - st.X) + (pz - st.Z) * (pz - st.Z)));
                    Assert.That(v, Is.LessThan(1f), "standing at the stop");
                    stops++;
                };
                for (int i = 0; i < 20 * 60 * 20 && stops < 6; i++)
                {
                    bus.Step(Dt, 9f);
                    sim.Step(Dt, 9f, 0f);
                }
            }
            Assert.That(stops, Is.GreaterThan(2), "buses stopped");
            Assert.That(worst, Is.LessThan(30.0), "at the real stops (within the 25 m projection)");
        }

        [Test]
        public void BusTimetablesSurviveStreaming()
        {
            // Rebuilding the graph (tiles streaming in and out) neither duplicates buses nor drops their routes.
            var g = new LaneGraph { Sacred = TrafficData.Zones };
            var tiles = new List<TileData>();
            foreach (TileData t in DrivingData.SampleTiles().Values)
                if (t.Tile.Level == 10) tiles.Add(t);
            tiles.Sort((a, b) => a.Tile.Key.CompareTo(b.Tile.Key));
            foreach (TileData t in tiles) g.AddTile(t.Tile, t);
            double x, z;
            TrafficData.Game(RatnaPark, out x, out z);
            var sim = new TrafficSim(g, TrafficSettings.Mid(), 9) { Sacred = TrafficData.Zones };
            var bus = new BusRouteRunner(TrafficData.Routes, g, sim);
            sim.SetFocus(x, z, 0f);
            for (int i = 0; i < 20 * 120; i++)
            {
                bus.Step(Dt, 9f);
                sim.Step(Dt, 9f, 0f);
            }
            int before = bus.RoutedAlive;
            Assert.That(before, Is.GreaterThan(0));
            // The farthest tile leaves and comes back.
            TileData far = tiles[0];
            double best = -1;
            foreach (TileData t in tiles)
            {
                double dx = t.Tile.X0 + 512 - x, dz = t.Tile.Z0 + 512 - z, d = dx * dx + dz * dz;
                if (d > best)
                {
                    best = d;
                    far = t;
                }
            }
            g.RemoveTile(far.Tile);
            bus.Step(Dt, 9f);
            sim.Step(Dt, 9f, 0f);
            g.AddTile(far.Tile, far);
            bus.Step(Dt, 9f);
            sim.Step(Dt, 9f, 0f);
            Assert.That(bus.RoutedAlive, Is.InRange(before - 1, before + 1), "the same buses keep their routes");
            var poses = new AgentPose[256];
            int n = sim.CopyPoses(poses);
            for (int a = 0; a < n; a++)
            for (int b = a + 1; b < n; b++)
            {
                if (poses[a].Class != poses[b].Class || !IsRouted(poses[a].Class)) continue;
                double dx = poses[a].X - poses[b].X, dz = poses[a].Z - poses[b].Z;
                Assert.That(dx * dx + dz * dz, Is.GreaterThan(4.0), "no doubled bus");
            }
        }

        private static bool IsRouted(VehicleClass c)
        {
            return c == VehicleClass.Bus || c == VehicleClass.Minibus || c == VehicleClass.Microbus || c == VehicleClass.Tempo;
        }
    }
}
