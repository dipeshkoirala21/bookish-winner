using System;
using System.Collections.Generic;
using System.Linq;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;
using Ghumante.Core.Traffic;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>W2_DESIGN 10.6 V5 (sacred rules) and V6 (traffic correctness) on the sample region, headless.</summary>
    public class TrafficTests
    {
        private const float Dt = 1f / 20f;

        // ---- V6: lane graph ----

        [Test]
        public void TwoWayLanesKeepLeftEverywhere()
        {
            LaneGraph g = TrafficData.Graph;
            int ok = 0, bad = 0;
            foreach (LaneId l in g.AllLanes())
            {
                LaneInfo i = g.Info(l);
                if (i.Kind != LaneKind.Road || !i.TwoWay) continue;
                // Offsets are measured to the right of the way's digitised direction: the forward lanes are left of it.
                if (i.Forward ? i.OffsetM < 0f : i.OffsetM > 0f) ok++;
                else bad++;
            }
            Assert.That(ok, Is.GreaterThan(5000));
            Assert.That(bad, Is.EqualTo(0), "LHT on 100% of two-way lanes");
        }

        [Test]
        public void RoundaboutsCirculateClockwiseAndJunctionsHaveControllers()
        {
            LaneGraph g = TrafficData.Graph;
            int rings = 0, anticlockwise = 0;
            var controllers = new Dictionary<ControllerKind, int>();
            var seen = new HashSet<int>();
            foreach (LaneId l in g.AllLanes())
            {
                LaneInfo i = g.Info(l);
                if (i.ControllerId >= 0 && seen.Add(i.ControllerId))
                {
                    ControllerKind k = g.ControllerKindOf(i.ControllerId);
                    int n;
                    controllers.TryGetValue(k, out n);
                    controllers[k] = n + 1;
                }
                // Ring pieces (synthesised rings and OSM roundabout ways); the sub-metre joints between them carry no turn.
                if (!i.Ring || i.Kind == LaneKind.Connector || i.PointCount < 3 || i.LengthM < 3f) continue;
                // Signed turning along the lane: clockwise seen from above turns right (negative in X east, Z north).
                double turn = 0, ax = 0, az = 0;
                for (int k = 1; k < i.PointCount; k++)
                {
                    double bx = i.X[k] - i.X[k - 1], bz = i.Z[k] - i.Z[k - 1];
                    if (bx * bx + bz * bz < 0.0025) continue; // coincident points (sub-metre connectors)
                    if (ax != 0 || az != 0) turn += Math.Atan2(ax * bz - az * bx, ax * bx + az * bz);
                    ax = bx;
                    az = bz;
                }
                if (Math.Abs(turn) < 0.05) continue; // straight piece
                rings++;
                if (turn > 0) anticlockwise++;
            }
            Assert.That(rings, Is.GreaterThan(30));
            Assert.That(anticlockwise, Is.EqualTo(0), "every roundabout turns clockwise");
            foreach (ControllerKind k in new[] { ControllerKind.Police, ControllerKind.Signal, ControllerKind.Roundabout, ControllerKind.Priority })
                Assert.That(controllers.ContainsKey(k) && controllers[k] > 0, Is.True, k + " controllers");
        }

        // ---- V6: yield at a roundabout entry ----

        private struct Entry
        {
            public LaneId Approach, Connector, Spawn;

            /// <summary>The ring lanes from <see cref="Spawn"/> down to the merge with the entry.</summary>
            public HashSet<int> Upstream;
        }

        private static List<Entry> RoundaboutEntries(LaneGraph g)
        {
            var r = new List<Entry>();
            foreach (LaneId a in g.AllLanes())
            {
                LaneInfo ai = g.Info(a);
                if (ai.Ring || ai.Kind != LaneKind.Road || ai.ControllerId < 0 || ai.LengthM < 25f) continue;
                if (g.ControllerKindOf(ai.ControllerId) != ControllerKind.Roundabout) continue;
                if ((ai.ClassMask & VehicleClasses.Bit(VehicleClass.Car)) == 0) continue;
                for (int k = 0; k < ai.SuccessorCount; k++)
                {
                    LaneId c = g.Successor(a, k);
                    LaneInfo ci = g.Info(c);
                    if (ci.SuccessorCount == 0) continue;
                    LaneId ring = g.Successor(c, 0);
                    LaneInfo ri = g.Info(ring);
                    if (!ri.Ring) continue;
                    for (int p = 0; p < ri.PredecessorCount; p++)
                    {
                        LaneId f = g.Predecessor(ring, p);
                        if (f.Value == c.Value || !g.Info(f).Ring) continue;
                        // Walk up the ring to a lane long enough to start a circulating car on.
                        var up = new HashSet<int>();
                        LaneId cur = f;
                        bool found = false;
                        for (int depth = 0; depth < 6 && up.Add(cur.Value); depth++)
                        {
                            LaneInfo li = g.Info(cur);
                            if (li.LengthM >= 8f && (li.ClassMask & VehicleClasses.Bit(VehicleClass.Car)) != 0)
                            {
                                found = true;
                                break;
                            }
                            LaneId next = default(LaneId);
                            bool any = false;
                            for (int q = 0; q < li.PredecessorCount; q++)
                            {
                                LaneId pq = g.Predecessor(cur, q);
                                if (!g.Info(pq).Ring) continue;
                                next = pq;
                                any = true;
                                break;
                            }
                            if (!any) break;
                            cur = next;
                        }
                        if (found) r.Add(new Entry { Approach = a, Connector = c, Spawn = cur, Upstream = up });
                        break;
                    }
                    break;
                }
            }
            return r;
        }

        /// <summary>Times (s) at which the entering car reaches the connector and the circulating car leaves the ring lanes
        /// before the merge (+∞ when it never happens), and whether the bodies ever overlapped. The circulating car
        /// appears at <paramref name="circulateAtS"/> (negative: never).</summary>
        private static void RunEntry(Entry e, bool entering, float circulateAtS, out float tEnter, out float tPass, out bool overlap)
        {
            LaneGraph g = TrafficData.Graph;
            TrafficSettings s = TrafficSettings.High();
            s.SpawnAttempts = 0;
            var sim = new TrafficSim(g, s, 11);
            double x, z;
            float y, h;
            g.PointAt(e.Approach, g.Info(e.Approach).LengthM, out x, out z, out y, out h);
            sim.SetFocus(x, z, 0f);
            int idE = -1, idC = -1;
            // The circulating car is spawned first so it gets the same id (and route) with or without the other car.
            if (circulateAtS == 0f)
                Assert.That(sim.TrySpawnOnLane(e.Spawn, 0f, VehicleCatalog.Hatchback, 0, out idC), Is.True); // as far up as it fits
            if (entering)
                Assert.That(sim.TrySpawnOnLane(e.Approach, g.Info(e.Approach).LengthM - 3f, VehicleCatalog.Hatchback, 0, out idE), Is.True);
            tEnter = tPass = float.PositiveInfinity;
            overlap = false;
            var b = new BodyBox[16];
            for (int i = 1; i <= 20 * 20; i++)
            {
                if (circulateAtS >= 0f && idC < 0 && i * Dt >= circulateAtS)
                    Assert.That(sim.TrySpawnOnLane(e.Spawn, 0f, VehicleCatalog.Hatchback, 0, out idC), Is.True); // as far up as the body fits
                sim.Step(Dt, 10f, 0f);
                int n = sim.CopyBodies(b);
                for (int k = 0; k < n; k++)
                {
                    if (b[k].AgentId == idE && float.IsPositiveInfinity(tEnter) && b[k].Lane.Value != e.Approach.Value) tEnter = i * Dt;
                    if (b[k].AgentId == idC && float.IsPositiveInfinity(tPass) && !e.Upstream.Contains(b[k].Lane.Value)) tPass = i * Dt;
                }
                if (TrafficData.Overlaps(b, n, 0.15f) > 0) overlap = true;
            }
        }

        [Test]
        public void RoundaboutEntriesYieldToCirculatingTraffic()
        {
            List<Entry> entries = RoundaboutEntries(TrafficData.Graph);
            Assert.That(entries.Count, Is.GreaterThan(0));
            int tested = 0;
            int held = 0;
            foreach (Entry e in entries)
            {
                // The entering car starts at its stop line (it decides at once) while a circulating car approaches.
                float alone, pass, ignore;
                bool ov;
                RunEntry(e, true, -1f, out alone, out ignore, out ov);
                Assert.That(ov, Is.False);
                RunEntry(e, false, 0f, out ignore, out pass, out ov);
                // Only where it matters: alone, the entering car would be in before the circulating one passes.
                if (float.IsInfinity(alone) || float.IsInfinity(pass) || alone >= pass) continue;
                float tEnter, tPass;
                RunEntry(e, true, 0f, out tEnter, out tPass, out ov);
                Assert.That(ov, Is.False, "no contact at the entry (lane " + e.Approach.Value + ")");
                Assert.That(tEnter, Is.LessThan(20f), "the entering car gets in eventually");
                // Held at the line until the ring car is by, unless that car was outside the gap (3 s for cars).
                if (tEnter > alone + 1f)
                {
                    Assert.That(tPass, Is.LessThan(tEnter), "it waited for the circulating car");
                    held++;
                }
                tested++;
            }
            Assert.That(held, Is.GreaterThan(0), "an entry held for circulating traffic");
            Assert.That(tested, Is.GreaterThan(0), "a roundabout entry where the yield matters");
        }

        // ---- V6: no collisions on the Ring Road, cows never touched ----

        [Test]
        public void RingRoadRunsHalfAnHourWithoutContact()
        {
            LaneGraph g = TrafficData.Graph;
            double x, z;
            TrafficData.Game(TrafficData.RingRoadChabahil, out x, out z);
            var sim = new TrafficSim(g, TrafficSettings.High(), 7) { Sacred = TrafficData.Zones };
            var animals = new AnimalSim(g, AnimalSettings.High(), 7) { Traffic = sim };
            var bus = new BusRouteRunner(TrafficData.Routes, g, sim);
            sim.SetFocus(x, z, 0f);
            animals.SetFocus(x, z, 0f);
            var bodies = new BodyBox[256];
            var ap = new AnimalPose[256];
            int overlaps = 0, cowHits = 0, maxAgents = 0, cowSamples = 0;
            for (int i = 0; i < 20 * 60 * 30; i++)
            {
                float hour = 8f + i * Dt / 3600f;
                animals.Step(Dt, hour, 0f);
                bus.Step(Dt, hour);
                sim.Step(Dt, hour, 0f);
                if (i % 5 != 0) continue;
                int n = sim.CopyBodies(bodies);
                maxAgents = Math.Max(maxAgents, n);
                overlaps += TrafficData.Overlaps(bodies, n, 0.15f);
                int na = animals.CopyPoses(ap);
                for (int k = 0; k < na; k++)
                {
                    if (ap[k].Kind != AnimalKind.Cow) continue;
                    cowSamples++;
                    for (int j = 0; j < n; j++)
                        if (!bodies[j].Ghost && TrafficData.CircleHits(bodies[j], ap[k].X, ap[k].Z, 1.0)) cowHits++;
                }
            }
            Assert.That(maxAgents, Is.GreaterThan(30), "busy road");
            Assert.That(cowSamples, Is.GreaterThan(0), "cows on the road");
            Assert.That(overlaps, Is.EqualTo(0), "IDM: no collision in 30 minutes");
            Assert.That(cowHits, Is.EqualTo(0), "cow collider overlap frames (G5)");
        }

        // ---- V5: sacred rules ----

        [Test]
        public void SacredZonesNeverSpawnMotorTraffic()
        {
            SacredZoneIndex zones = TrafficData.Zones;
            var sim = new TrafficSim(TrafficData.Graph, TrafficSettings.High(), 3) { Sacred = zones };
            var rng = new Random(1234);
            var motor = new List<int>();
            for (int v = 0; v < VehicleCatalog.Count; v++)
                if (VehicleClasses.IsMotor(VehicleCatalog.At(v).TrafficClass)) motor.Add(v);
            double[][] sites = { TrafficData.Boudha, TrafficData.Swayambhu, TrafficData.Basantapur, TrafficData.Pashupati };
            int inside = 0, spawned = 0, attempts = 0;
            while (inside < 1000 && attempts < 200000)
            {
                attempts++;
                double cx, cz;
                TrafficData.Game(sites[attempts % sites.Length], out cx, out cz);
                double x = cx + (rng.NextDouble() * 2 - 1) * 300, z = cz + (rng.NextDouble() * 2 - 1) * 300;
                if (!zones.Contains(x, z)) continue;
                inside++;
                int id;
                if (sim.TrySpawnAt(x, z, motor[rng.Next(motor.Count)], out id)) spawned++;
            }
            Assert.That(inside, Is.EqualTo(1000));
            Assert.That(spawned, Is.EqualTo(0), "motor agents spawned inside SACRED_NO_VEHICLE");

            // Running traffic around Boudha never puts a motor vehicle inside a zone either.
            double bx, bz;
            TrafficData.Game(TrafficData.Boudha, out bx, out bz);
            sim.SetFocus(bx, bz, 0f);
            var poses = new AgentPose[256];
            int motorInside = 0, seen = 0;
            for (int i = 0; i < 20 * 180; i++)
            {
                sim.Step(Dt, 9f, 0f);
                if (i % 10 != 0) continue;
                int n = sim.CopyPoses(poses);
                for (int k = 0; k < n; k++)
                {
                    if (!VehicleClasses.IsMotor(poses[k].Class)) continue;
                    seen++;
                    if (zones.Contains(poses[k].X, poses[k].Z)) motorInside++;
                }
            }
            Assert.That(seen, Is.GreaterThan(0));
            Assert.That(motorInside, Is.EqualTo(0));
        }

        [TestCase(85.3620, 27.7214, "Boudha")]
        [TestCase(85.2904, 27.7149, "Swayambhu")]
        public void KoraWalkersCircleClockwise(double lon, double lat, string site)
        {
            double x, z;
            TrafficData.Game(new[] { lon, lat }, out x, out z);
            PedestrianSim peds = TrafficData.Pedestrians(PedestrianSettings.High(), 3, null);
            peds.SetFocus(x, z, 0f);
            var kora = new KoraState[256];
            int samples = 0, anticlockwise = 0, maxWalkers = 0;
            for (int i = 0; i < 20 * 120; i++)
            {
                peds.Step(Dt, 6.5f, 0f);
                int n = peds.CopyKora(kora);
                maxWalkers = Math.Max(maxWalkers, n);
                for (int k = 0; k < n; k++)
                {
                    samples++;
                    if (kora[k].AngularVelocity >= 0f) anticlockwise++;
                    // Independent check: the walker's motion about the stupa centre.
                    double rx = kora[k].X - kora[k].CX, rz = kora[k].Z - kora[k].CZ;
                    Assert.That(rx * rx + rz * rz, Is.GreaterThan(4.0), site + ": walkers stay off the stupa");
                }
            }
            Assert.That(maxWalkers, Is.GreaterThan(10), site + " kora walkers");
            Assert.That(anticlockwise, Is.EqualTo(0), site + ": angular velocity is always clockwise (" + samples + " samples)");
        }

        [Test]
        public void NoLaneRunsThroughADrawnIsland()
        {
            // Track A draws raised islands (mapped rings, synthetic police islands); Track B's lanes must go round them
            // (Kalanki, Balkhu, Chabahil...). Lane centrelines keep at least 1 m from the island edge.
            LaneGraph g = TrafficData.Graph;
            var islands = new List<double[]>(); // x, z, radius (game metres)
            int synthetic = 0;
            foreach (TileData t in DrivingData.SampleTiles().Values)
            {
                if (t.Tile.Level != 10) continue;
                foreach (RoadIsland isl in RoadLayout.For(t).Islands)
                {
                    if (isl.Kind == IslandKind.Mini) continue; // painted or mountable: driven over
                    islands.Add(new[] { t.Tile.X0 + isl.X, t.Tile.Z0 + isl.Z, (double)isl.RadiusM, (double)isl.Kind, t.Tile.Tx, t.Tile.Ty, isl.X, isl.Z });
                    if (isl.Kind == IslandKind.Synthetic) synthetic++;
                }
            }
            Assert.That(synthetic, Is.GreaterThan(0), "the sample has synthetic police islands");
            var bad = new List<string>();
            foreach (LaneId id in g.AllLanes())
            {
                LaneInfo l = g.Info(id);
                for (int k = 0; k < l.PointCount; k++)
                    foreach (double[] isl in islands)
                    {
                        double dx = l.X[k] - isl[0], dz = l.Z[k] - isl[1];
                        if (dx * dx + dz * dz < Math.Pow(Math.Max(0.0, isl[2] - 1.0), 2))
                            bad.Add(l.Kind + " lane " + id.Value + " way " + l.WayId + " at " + Math.Sqrt(dx * dx + dz * dz).ToString("F1") + " m in r " + isl[2].ToString("F1") + " kind " + isl[3] + " tile " + isl[4] + "/" + isl[5] + " at " + isl[6].ToString("F1") + "," + isl[7].ToString("F1"));
                    }
            }
            Assert.That(bad, Is.Empty, string.Join("; ", bad.Take(20)));
        }
    }
}
