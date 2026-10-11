using System;
using System.Collections.Generic;
using System.IO;
using Ghumante.Core.Characters;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using Ghumante.Core.Routing;
using Ghumante.Core.Search;
using Ghumante.Core.Traffic;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The player's rides on the real traffic simulation of the committed <c>kathmandu_core</c> sample (game/README.md
    /// "Take a bus"): a hailed taxi pulls over with its passenger door beside the player and stays until they climb in;
    /// the bell lets a taxi passenger out within seconds; a bus, minibus, micro or tempo stops for a player waiting at
    /// its kerb with its doors open; the bell on a route vehicle stops it at its next stop (or ahead) with its doors open.
    /// Each test runs <see cref="TrafficSim"/> exactly as Explore does: the session posts the player as a traffic obstacle
    /// and the ride's pull-over command (<see cref="TrafficSim.PullOver"/>) about ten times a second.
    /// </summary>
    public class TrafficPullOverTests
    {
        private const float Dt = 1f / 30f;
        private const int PostEvery = 3; // the session posts at 10 Hz
        private const int PlayerObstacleId = 0x50_4C_41_59;

        /// <summary>Kerb lanes (the leftmost lane of their direction) of class <paramref name="c"/>, at least
        /// <paramref name="minLength"/> long, starting within 600 m of the spot, nearest first.</summary>
        private static List<LaneId> LanesNear(double lon, double lat, VehicleClass c, float minLength)
        {
            LaneGraph g = TrafficData.Graph;
            WorldFrame.LonLatToGame(lon, lat, out double fx, out double fz);
            uint bit = VehicleClasses.Bit(c);
            var cands = new List<KeyValuePair<double, LaneId>>();
            foreach (LaneId l in g.AllLanes())
            {
                LaneInfo a = g.Info(l);
                if (a.Kind != LaneKind.Road || a.Index != 0 || a.LengthM < minLength || (a.ClassMask & bit) == 0) continue;
                double dx = a.X[0] - fx, dz = a.Z[0] - fz;
                double d = Math.Sqrt(dx * dx + dz * dz);
                if (d <= 600) cands.Add(new KeyValuePair<double, LaneId>(d, l));
            }
            cands.Sort((p, q) => p.Key != q.Key ? p.Key.CompareTo(q.Key) : p.Value.Value.CompareTo(q.Value.Value));
            var r = new List<LaneId>();
            foreach (var kv in cands) r.Add(kv.Value);
            return r;
        }

        /// <summary>A point at the left (kerb) side of a lane, <paramref name="s"/> metres along it, standing
        /// <paramref name="beyondM"/> beyond the lane's edge.</summary>
        private static void Kerb(LaneId lane, float s, double beyondM, out double x, out double z)
        {
            LaneGraph g = TrafficData.Graph;
            LaneInfo li = g.Info(lane);
            g.PointAt(lane, s, out double px, out double pz, out float py, out float h);
            double off = 0.5 * li.WidthM + beyondM;
            // Left of travel: (-cos h, +sin h) in (x, z) for heading h measured clockwise from north.
            x = px - Math.Cos(h) * off;
            z = pz + Math.Sin(h) * off;
        }

        private static double Dist(in AgentPose a, double x, double z)
        {
            double dx = a.X - x, dz = a.Z - z;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>How far the agent's kerb-side passenger door (its first passenger seat's entry point) is from (x, z).</summary>
        private static double DoorDistance(in AgentPose a, double x, double z)
        {
            SeatSocket[] seats = VehicleSeats.For(a.Variant);
            SeatSocket seat = seats[0];
            foreach (SeatSocket s in seats)
                if (s.Kind == SeatKind.Passenger || s.Kind == SeatKind.Pillion)
                {
                    seat = s;
                    break;
                }
            VehicleSeats.ToWorld(a.X, a.Z, a.Y, a.HeadingRad, seat.EntryX, 0f, seat.EntryZ, out double ex, out double ez, out float ey);
            return Math.Sqrt((ex - x) * (ex - x) + (ez - z) * (ez - z));
        }

        /// <summary>
        /// Hails the taxi <paramref name="agent"/> for a player standing at (px, pz) and runs the simulation as the session
        /// does (player obstacle and the fixed pickup posted at 10 Hz, the focus on the player). Returns the seconds until
        /// the taxi stands boardable (<see cref="PassengerRide.IsBoardable"/>) within 20 m with its passenger door within
        /// 5 m of the player, or −1 within <paramref name="limitS"/>.
        /// </summary>
        private static float HailAndWait(TrafficSim sim, int agent, double px, double pz, float limitS, out double doorM)
        {
            var poses = new AgentPose[512];
            doorM = double.MaxValue;
            sim.SetFocus(px, pz, 0f);
            for (int i = 0; i * Dt < limitS; i++)
            {
                if (i % PostEvery == 0)
                {
                    sim.AddObstacle(PlayerObstacleId, px, pz, 0.6f, ObstacleKind.Player);
                    sim.PullOver(agent, PullOverKind.Pickup, px, pz);
                }
                sim.Step(Dt, 10f, 0f);
                int n = sim.CopyPoses(poses);
                int k = PassengerRide.Find(poses, n, agent);
                if (k < 0) return -1f;
                AgentPose taxi = poses[k];
                if (!PassengerRide.IsBoardable(taxi) || Dist(taxi, px, pz) > 20.0) continue;
                doorM = DoorDistance(taxi, px, pz);
                if (doorM <= 5.0) return (i + 1) * Dt;
            }
            return -1f;
        }

        private static readonly double[][] Spots =
        {
            new[] { 85.3153, 27.7066 }, // Ratna Park
            new[] { 85.3462, 27.7120 }, // Chabahil, Ring Road
            new[] { 85.3190, 27.6950 }, // Maitighar
            new[] { 85.3300, 27.7050 }, // Dillibazar
            new[] { 85.3110, 27.7150 }, // Thamel
            new[] { 85.3050, 27.7200 }, // Lainchaur and Balaju road
        };

        [Test]
        public void AHailedTaxiOnThePlayersStreetPullsOverBesideThem([Range(0, 5)] int spot, [Values(0, 1)] int pick)
        {
            // The finding: with a pull-over point re-derived from the taxi's moving pose, the taxi crept past at 0.6 m/s.
            List<LaneId> lanes = LanesNear(Spots[spot][0], Spots[spot][1], VehicleClass.Taxi, 140f);
            Assume.That(lanes.Count, Is.GreaterThan(pick));
            LaneId lane = lanes[pick];
            Kerb(lane, 100f, 1.2, out double px, out double pz);
            var sim = new TrafficSim(TrafficData.Graph, TrafficSettings.Mid(), (ulong)(77 + spot * 10 + pick)) { Sacred = TrafficData.Zones };
            sim.SetFocus(px, pz, 0f);
            Assert.That(sim.TrySpawnOnLane(lane, 20f, VehicleCatalog.Taxi, 0, out int agent), Is.True, "taxi spawned 80 m up the street");
            for (int k = 0; k < 3; k++) sim.Step(Dt, 10f, 0f);
            float t = HailAndWait(sim, agent, px, pz, WaveLimitS, out double door);
            TestContext.WriteLine("spot " + spot + " lane " + lane.Value + ": boardable after " + t.ToString("0.0") + " s, door " + door.ToString("0.0") + " m; " + sim.Describe(agent));
            Assert.That(t, Is.GreaterThan(0f), "the hailed taxi stands with its door beside the player within " + WaveLimitS + " s");
            // It waits there while the player walks to the door (the session keeps the pickup, then holds it while they
            // climb in).
            var poses = new AgentPose[512];
            for (int i = 0; i < 90; i++)
            {
                if (i % PostEvery == 0)
                {
                    sim.AddObstacle(PlayerObstacleId, px, pz, 0.6f, ObstacleKind.Player);
                    sim.PullOver(agent, i < 45 ? PullOverKind.Pickup : PullOverKind.Hold, px, pz);
                }
                sim.Step(Dt, 10f, 0f);
            }
            Assert.That(sim.PullOverStateOf(agent), Is.EqualTo(PullOverState.Holding), "it waits at the kerb");
            int n = sim.CopyPoses(poses);
            Assert.That(PassengerRide.IsBoardable(poses[PassengerRide.Find(poses, n, agent)]), Is.True, "still standing 3 s later");
        }

        [Test]
        public void AWaveBringsATaxiBesideThePlayerBeforeTheHailTimesOut([Range(0, 5)] int spot, [Values(0, 1)] int side)
        {
            // Hold to hail: the simulation answers with the taxi nearby that has the shortest way to the player (often on
            // another street, or heading away: it finds its way round through the lane graph), else it sends one from up
            // the player's street, out of view. Either way it pulls over beside the player before PassengerRide gives up.
            List<LaneId> lanes = LanesNear(Spots[spot][0], Spots[spot][1], VehicleClass.Taxi, 60f);
            Assume.That(lanes.Count, Is.GreaterThan(side));
            Kerb(lanes[side], 30f, 1.2, out double px, out double pz);
            var sim = new TrafficSim(TrafficData.Graph, TrafficSettings.Mid(), (ulong)(300 + spot * 7 + side)) { Sacred = TrafficData.Zones };
            sim.SetFocus(px, pz, 0f);
            for (int i = 0; i * Dt < 60f; i++)
            {
                if (i % PostEvery == 0) sim.AddObstacle(PlayerObstacleId, px, pz, 0.6f, ObstacleKind.Player);
                sim.Step(Dt, 10f, 0f);
            }
            var poses = new AgentPose[512];
            int taxis = 0, n0 = sim.CopyPoses(poses);
            for (int k = 0; k < n0; k++)
                if (poses[k].Class == VehicleClass.Taxi && Dist(poses[k], px, pz) <= TrafficSim.HailRadiusM) taxis++;
            int agent = 0;
            float t = -1f;
            double door = double.MaxValue;
            for (int i = 0; i * Dt < WaveLimitS; i++)
            {
                if (i % PostEvery == 0)
                {
                    sim.AddObstacle(PlayerObstacleId, px, pz, 0.6f, ObstacleKind.Player);
                    if (agent <= 0) sim.RequestTaxi(px, pz);
                    else sim.PullOver(agent, PullOverKind.Pickup, px, pz);
                }
                sim.Step(Dt, 10f, 0f);
                if (agent <= 0)
                {
                    agent = sim.RequestedTaxi;
                    Assert.That(agent, Is.GreaterThanOrEqualTo(0), "a taxi can come to this kerb");
                    if (agent > 0) sim.CancelTaxiRequest();
                    continue;
                }
                int n = sim.CopyPoses(poses);
                int k = PassengerRide.Find(poses, n, agent);
                Assert.That(k, Is.GreaterThanOrEqualTo(0), "the taxi stays in the simulation");
                AgentPose taxi = poses[k];
                if (!PassengerRide.IsBoardable(taxi) || Dist(taxi, px, pz) > 20.0) continue;
                door = DoorDistance(taxi, px, pz);
                if (door > 8.0) continue;
                t = (i + 1) * Dt;
                break;
            }
            TestContext.WriteLine("spot " + spot + " side " + side + ": " + taxis + " taxis within 150 m; taxi " + agent + " boardable after " + t.ToString("0.0") +
                                  " s, door " + door.ToString("0.0") + " m; " + sim.Describe(agent));
            Assert.That(agent, Is.GreaterThan(0), "a taxi answers the wave");
            Assert.That(t, Is.GreaterThan(0f), "the taxi stands with its door beside the player within " + WaveLimitS + " s");
        }

        /// <summary>The explorer keeps waving while the taxi is on its way (the controller re-arms PassengerRide's 45 s
        /// hail timeout while the taxi approaches) and gives up after this long.</summary>
        private const float WaveLimitS = 60f;

        /// <summary>Rides along in <paramref name="agent"/> for <paramref name="rideS"/> (the focus follows), rings the
        /// bell with <paramref name="bell"/> posted at 10 Hz as the session does, and returns the seconds until the ride
        /// reaches <see cref="RideState.Alight"/> (−1 within <paramref name="limitS"/>).</summary>
        private static float RideAndRing(TrafficSim sim, BusRouteRunner bus, int agent, float rideS, PullOverKind bell, float limitS, out string why)
        {
            var poses = new AgentPose[512];
            AgentPose a = default(AgentPose);
            why = "";
            int n;
            for (int k = 0; k * Dt < rideS; k++)
            {
                if (bus != null) bus.Step(Dt, 10f);
                sim.Step(Dt, 10f, 0f);
                n = sim.CopyPoses(poses);
                int i = PassengerRide.Find(poses, n, agent);
                if (i < 0)
                {
                    why = "left the simulation before the bell";
                    return -1f;
                }
                a = poses[i];
                sim.SetFocus(a.X, a.Z, 0f);
            }
            var ride = new PassengerRide();
            ride.Board(a, 1);
            ride.RequestStop();
            float v0 = a.SpeedMps;
            for (int k = 0; k * Dt < limitS; k++)
            {
                if (k % PostEvery == 0) sim.PullOver(agent, bell, 0, 0);
                if (bus != null) bus.Step(Dt, 10f);
                sim.Step(Dt, 10f, 0f);
                n = sim.CopyPoses(poses);
                int i = PassengerRide.Find(poses, n, agent);
                if (i >= 0)
                {
                    a = poses[i];
                    sim.SetFocus(a.X, a.Z, 0f);
                }
                RideState s = ride.Update(Dt, i >= 0, a, a.X, a.Z);
                if (i < 0)
                {
                    why = "left the simulation";
                    return -1f;
                }
                if (s == RideState.Alight)
                {
                    why = "bell at " + v0.ToString("0.0") + " m/s";
                    return (k + 1) * Dt;
                }
            }
            why = "still " + ride.State + " at " + a.SpeedMps.ToString("0.0") + " m/s (" + sim.PullOverStateOf(agent) + ")";
            return -1f;
        }

        [Test]
        public void TheBellLetsATaxiPassengerOutWithinSeconds([Range(0, 5)] int spot, [Values(0, 1)] int pick)
        {
            // The finding: a stop point 14 m ahead of the taxi's current pose kept moving with it; half the rides never stopped.
            List<LaneId> lanes = LanesNear(Spots[spot][0], Spots[spot][1], VehicleClass.Taxi, 140f);
            Assume.That(lanes.Count, Is.GreaterThan(pick));
            LaneId lane = lanes[pick];
            LaneInfo li = TrafficData.Graph.Info(lane);
            var sim = new TrafficSim(TrafficData.Graph, TrafficSettings.Mid(), (ulong)(91 + spot * 10 + pick)) { Sacred = TrafficData.Zones };
            sim.SetFocus(li.X[0], li.Z[0], 0f);
            Assert.That(sim.TrySpawnOnLane(lane, 20f, VehicleCatalog.Taxi, 0, out int agent), Is.True);
            float t = RideAndRing(sim, null, agent, 8f, PullOverKind.Ahead, 30f, out string why);
            TestContext.WriteLine("spot " + spot + " lane " + lane.Value + ": " + (t > 0 ? "out after " + t.ToString("0.0") + " s" : "not out") + ", " + why);
            Assert.That(t, Is.GreaterThan(0f), "the taxi pulls over and lets the passenger out within 30 s of the bell: " + why);
        }

        // -------------------------------------------------------------------------------------------------------------
        // Route vehicles

        /// <summary>Where the README's teleport to <paramref name="query"/> puts the player: the first search hit snapped
        /// onto the motorbike road network as <c>ExploreSpawn.TryRoadSpawn</c> does (6 m into the best edge of the nearest
        /// node).</summary>
        internal static void TeleportPoint(string query, out double x, out double z)
        {
            string root = GoldenFiles.RepoRoot;
            string dir = Path.Combine(root, "shared", "sample-regions", "kathmandu_core");
            var engine = new SearchEngine(SearchIndexReader.Read(File.ReadAllBytes(Path.Combine(dir, "kathmandu_core.search.ghsi"))));
            RouteGraph rg = RouteGraphReader.Read(File.ReadAllBytes(Path.Combine(dir, "kathmandu_core.route.ghrg")));
            List<SearchResult> hits = engine.Search(query, 3);
            Assert.That(hits.Count, Is.GreaterThan(0), query);
            SearchEntry hit = hits[0].Entry;
            for (int i = 0; i < hits.Count; i++)
                if (hits[i].Entry.IsPlace)
                {
                    hit = hits[i].Entry;
                    break;
                }
            int node = new NearestNode(rg, Travel.Motorbike, false).Find(hit.X, hit.Z);
            Assert.That(node, Is.GreaterThanOrEqualTo(0));
            TravelProfile profile = TravelProfiles.Get(Travel.Motorbike);
            int best = -1, bestRank = int.MaxValue;
            double bestLength = -1;
            for (int e = rg.Offsets[node]; e < rg.Offsets[node + 1]; e++)
            {
                if (double.IsPositiveInfinity(profile.EdgeTimeS(rg, e))) continue;
                int rank = rg.EdgeClass[e] == RoadClass.Unknown ? 100 : (int)rg.EdgeClass[e];
                double length = rg.EdgeLengthM(e);
                if (rank < bestRank || rank == bestRank && length > bestLength)
                {
                    best = e;
                    bestRank = rank;
                    bestLength = length;
                }
            }
            x = rg.NodeX(node);
            z = rg.NodeZ(node);
            if (best < 0) return;
            double[] geo = rg.EdgeGeometry(best);
            double left = Math.Min(6.0, 0.4 * rg.EdgeLengthM(best));
            for (int i = 0; i + 1 < geo.Length / 2; i++)
            {
                double ax = geo[2 * i], az = geo[2 * i + 1], dx = geo[2 * i + 2] - ax, dz = geo[2 * i + 3] - az;
                double len = Math.Sqrt(dx * dx + dz * dz);
                if (len < 1e-9) continue;
                double t = Math.Min(1.0, left / len);
                x = ax + dx * t;
                z = az + dz * t;
                if (left <= len) return;
                left -= len;
            }
        }

        private static bool RouteClass(VehicleClass c)
        {
            return c == VehicleClass.Bus || c == VehicleClass.Minibus || c == VehicleClass.Microbus || c == VehicleClass.Tempo;
        }

        [Test]
        public void ABoardableBusOrMicroStandsAtRatnaParkWithinFiveMinutesOnMid()
        {
            // README "Take a bus": teleport to Ratna Park bus station on a Mid phone and wait at the roadside. The finding:
            // in 15 game-minutes not one route vehicle stood there with its doors open.
            TeleportPoint("Ratna Park bus station", out double px, out double pz);
            LaneGraph g = TrafficData.Graph;
            var sim = new TrafficSim(g, TrafficSettings.Mid(), 5) { Sacred = TrafficData.Zones };
            var bus = new BusRouteRunner(TrafficData.Routes, g, sim);
            sim.SetFocus(px, pz, 0f);
            var poses = new AgentPose[512];
            float first = -1f;
            int boardable = 0, seen = 0;
            var ids = new HashSet<int>();
            for (int i = 0; i * Dt < 300f; i++)
            {
                if (i % PostEvery == 0)
                {
                    sim.AddObstacle(PlayerObstacleId, px, pz, 0.6f, ObstacleKind.Player);
                    sim.SetWaitingPassenger(true, px, pz);
                }
                float hour = 10f + i * Dt / 3600f;
                bus.Step(Dt, hour);
                sim.Step(Dt, hour, 0f);
                int n = sim.CopyPoses(poses);
                for (int k = 0; k < n; k++)
                {
                    AgentPose a = poses[k];
                    if (!RouteClass(a.Class) || Dist(a, px, pz) > 20.0) continue;
                    if (ids.Add(a.AgentId)) seen++;
                    if (!PassengerRide.IsBoardable(a)) continue;
                    boardable++;
                    if (first < 0f) first = (i + 1) * Dt;
                }
            }
            TestContext.WriteLine("route vehicles within 20 m: " + seen + "; first boardable after " + first.ToString("0") + " s; boardable frames " + boardable);
            Assert.That(first, Is.GreaterThan(0f), "a bus, minibus, micro or tempo stands boardable within 20 m within 5 game-minutes");
        }

        [Test]
        public void RouteVehiclesDwellAtTheirRealStopsOnMid()
        {
            // The finding: on Mid, 0 stops in 15 minutes at Ratna Park; stops were only seen when on the current or next lane.
            LaneGraph g = TrafficData.Graph;
            int events = 0, atStopVehicles = 0;
            foreach (double[] spot in new[] { Spots[0], Spots[1], new[] { 85.3170, 27.7000 } })
            {
                WorldFrame.LonLatToGame(spot[0], spot[1], out double x, out double z);
                var sim = new TrafficSim(g, TrafficSettings.Mid(), 9) { Sacred = TrafficData.Zones };
                var bus = new BusRouteRunner(TrafficData.Routes, g, sim);
                sim.StopReached += (a, r, s) => events++;
                sim.SetFocus(x, z, 0f);
                var poses = new AgentPose[512];
                var dwellers = new HashSet<int>();
                for (int i = 0; i * 0.1f < 600f; i++)
                {
                    float h = 9f + i * 0.1f / 3600f;
                    bus.Step(0.1f, h);
                    sim.Step(0.1f, h, 0f);
                    int n = sim.CopyPoses(poses);
                    for (int k = 0; k < n; k++)
                        if ((poses[k].AnimState & (byte)AgentAnim.AtStop) != 0 && RouteClass(poses[k].Class))
                            dwellers.Add(poses[k].AgentId);
                }
                atStopVehicles += dwellers.Count;
            }
            TestContext.WriteLine("10 min at three sites on Mid: " + events + " real stops served, " + atStopVehicles + " route vehicles stood with doors open");
            Assert.That(events, Is.GreaterThanOrEqualTo(3), "route vehicles dwell at real stops near the player");
            Assert.That(atStopVehicles, Is.GreaterThanOrEqualTo(6), "and micros and tempos make their random stops too");
        }

        [Test]
        public void TheBellOnARouteVehicleLetsThePassengerOut([Range(0, 3)] int seed)
        {
            // Ride a routed bus or micro near Ratna Park; the bell stops it at its next stop (or pulls it over) with its
            // doors open, so the ride reaches Alight.
            TeleportPoint("Ratna Park bus station", out double px, out double pz);
            LaneGraph g = TrafficData.Graph;
            var sim = new TrafficSim(g, TrafficSettings.Mid(), (ulong)(40 + seed)) { Sacred = TrafficData.Zones };
            var bus = new BusRouteRunner(TrafficData.Routes, g, sim);
            sim.SetFocus(px, pz, 0f);
            var poses = new AgentPose[512];
            int agent = -1;
            for (int i = 0; i * Dt < 240f && agent < 0; i++)
            {
                bus.Step(Dt, 10f);
                sim.Step(Dt, 10f, 0f);
                int n = sim.CopyPoses(poses);
                for (int k = 0; k < n && agent < 0; k++)
                    if (RouteClass(poses[k].Class) && Dist(poses[k], px, pz) < 120.0 && poses[k].SpeedMps > 2f && (seed & 1) == 0 == (poses[k].Class == VehicleClass.Bus))
                        agent = poses[k].AgentId;
            }
            Assume.That(agent, Is.GreaterThan(0), "a route vehicle came by");
            float t = RideAndRing(sim, bus, agent, 5f, PullOverKind.NextStop, 150f, out string why);
            TestContext.WriteLine("seed " + seed + ": " + (t > 0 ? "out after " + t.ToString("0.0") + " s" : "not out") + ", " + why + "; " + sim.Describe(agent));
            if (t < 0f && sim.TryGetAgent(agent, out LaneId at, out float atS, out float atV))
            {
                TestContext.WriteLine(sim.DescribeLane(at.Value));
                foreach (int nx in TrafficData.Graph.Lanes[at.Value].Next)
                {
                    TestContext.WriteLine(sim.DescribeLane(nx));
                    foreach (int nn in TrafficData.Graph.Lanes[nx].Next) TestContext.WriteLine(sim.DescribeLane(nn));
                    foreach (int c in TrafficData.Graph.Lanes[nx].Conflicts)
                    {
                        TestContext.WriteLine("  conflict " + sim.DescribeLane(c));
                        foreach (int cn in TrafficData.Graph.Lanes[c].Next) TestContext.WriteLine("    exit " + sim.DescribeLane(cn));
                    }
                }
            }
            Assert.That(t, Is.GreaterThan(0f), "the bell lets the passenger off within 150 s: " + why);
        }
    }
}
