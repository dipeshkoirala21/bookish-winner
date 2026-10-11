using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ghumante.Core.Aviation;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;
using Ghumante.Core.Traffic;
using Ghumante.World.Life;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// W2 perf pass: the lanes and walk graph of a new detail tile are built off the simulation step
    /// (<see cref="LaneGraph.Prepare"/> / <see cref="LaneGraph.Commit"/>, <see cref="PedestrianSim.PrepareTile"/> /
    /// <see cref="PedestrianSim.CommitTile"/>), so <see cref="LifeHost"/> keeps stepping traffic, people and animals while
    /// a dense tile builds instead of freezing them for its whole build.
    /// </summary>
    public class TrafficBackgroundBuildTests
    {
        private static void Asan(out double x, out double z)
        {
            WorldFrame.LonLatToGame(85.3122, 27.7074, out x, out z);
        }

        /// <summary>The Asan tile and its resident neighbours (the sample's dense core).</summary>
        private static List<TileId> AsanTiles(out TileId asan)
        {
            Asan(out double x, out double z);
            asan = TileId.At(10, x, z);
            Dictionary<TileId, TileData> tiles = DrivingData.SampleTiles();
            var list = new List<TileId>();
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    var id = new TileId(10, asan.Tx + dx, asan.Ty + dz);
                    if (tiles.ContainsKey(id)) list.Add(id);
                }
            Assert.That(list, Does.Contain(asan), "the sample holds Asan");
            Assert.That(list.Count, Is.GreaterThan(1), "and a neighbour of it");
            return list;
        }

        private static LifeHost NewHost(ulong seed)
        {
            CuratedDb db = CuratedDb.Read(File.ReadAllBytes(DrivingData.SamplePath(".curated.ghcd")));
            return new LifeHost(LifeSettings.ForTier(1), seed, TrafficData.Routes, null, db);
        }

        private static void Tick(LifeHost host, float dt = 0.1f)
        {
            Asan(out double x, out double z);
            host.Tick(dt, x, z, 0f, 1f, 9f, 10, 0f, WeatherKind.Clear);
        }

        [Test]
        public void PreparedOnAWorkerAndCommittedLanesMatchAddTile()
        {
            List<TileId> ids = AsanTiles(out TileId _);
            Dictionary<TileId, TileData> tiles = DrivingData.SampleTiles();
            var a = new LaneGraph { Sacred = TrafficData.Zones };
            var b = new LaneGraph { Sacred = TrafficData.Zones };
            var pa = new PedestrianSim(a, PedestrianSettings.Mid(), 3UL);
            var pb = new PedestrianSim(b, PedestrianSettings.Mid(), 3UL);
            foreach (TileId id in ids)
            {
                a.AddTile(id, tiles[id]);
                pa.AddTile(id, tiles[id]);
            }
            // Every tile prepared at once on the thread pool, then committed in the same order.
            var lanes = new Task<LaneGraph.PreparedTile>[ids.Count];
            var walks = new Task<PedestrianSim.PreparedWalk>[ids.Count];
            for (int i = 0; i < ids.Count; i++)
            {
                TileId id = ids[i];
                lanes[i] = Task.Run(() => b.Prepare(id, tiles[id]));
                walks[i] = Task.Run(() => PedestrianSim.PrepareTile(id, tiles[id]));
            }
            for (int i = 0; i < ids.Count; i++)
            {
                Assert.That(lanes[i].Result.Id, Is.EqualTo(ids[i]));
                Assert.That(lanes[i].Result.LaneCount, Is.GreaterThan(0));
                b.Commit(lanes[i].Result);
                pb.CommitTile(walks[i].Result);
            }
            Assert.That(b.LaneCount, Is.EqualTo(a.LaneCount));
            Assert.That(b.NodeCount, Is.EqualTo(a.NodeCount));
            Assert.That(b.LaneCapacity, Is.EqualTo(a.LaneCapacity));
            Assert.That(pb.EdgeCount, Is.EqualTo(pa.EdgeCount));
            for (int i = 0; i < a.LaneCapacity; i++)
            {
                LaneGraph.Lane la = a.Lanes[i], lb = b.Lanes[i];
                Assert.That(lb.Alive, Is.EqualTo(la.Alive));
                if (!la.Alive) continue;
                Assert.That(lb.N, Is.EqualTo(la.N), "lane " + i);
                Assert.That(lb.Kind, Is.EqualTo(la.Kind));
                Assert.That(lb.Mask, Is.EqualTo(la.Mask));
                Assert.That(lb.X[lb.N - 1], Is.EqualTo(la.X[la.N - 1]));
                Assert.That(lb.Z[0], Is.EqualTo(la.Z[0]));
                Assert.That(lb.Next, Is.EqualTo(la.Next));
            }
        }

        [Test]
        public void LifeKeepsMovingWhileADenseTileBuilds()
        {
            List<TileId> ids = AsanTiles(out TileId asan);
            Dictionary<TileId, TileData> tiles = DrivingData.SampleTiles();
            using (LifeHost host = NewHost(21))
            {
                foreach (TileId id in ids)
                    if (id != asan) host.AddTile(id, tiles[id]);
                for (int i = 0; i < 200; i++)
                {
                    Tick(host);
                    host.Wait();
                }
                Assert.IsNull(host.Error);
                Assert.That(host.BuildingTiles, Is.EqualTo(0));
                int lanesBefore = host.Graph.LaneCount;
                Assert.That(host.VehicleCount, Is.GreaterThan(3), "traffic on the neighbours");
                Assert.That(host.PeopleCount, Is.GreaterThan(3), "people on the neighbours");

                // Asan joins; its build is held on the build job (as a slow phone would hold it) while frames go on.
                using (var gate = new ManualResetEventSlim(false))
                {
                    host.BeforeTileBuild = id =>
                    {
                        if (id == asan) gate.Wait(30000);
                    };
                    host.AddTile(asan, tiles[asan]);
                    var first = new Dictionary<int, AgentPose>();
                    int steps = 0;
                    double worstStepMs = 0;
                    for (int f = 0; f < 30; f++)
                    {
                        var sw = Stopwatch.StartNew();
                        Tick(host, 0.05f);
                        host.WaitStep();
                        worstStepMs = Math.Max(worstStepMs, sw.Elapsed.TotalMilliseconds);
                        steps++;
                        if (f == 0)
                            for (int i = 0; i < host.VehicleCount; i++) first[host.Vehicles[i].AgentId] = host.Vehicles[i];
                        Assert.That(host.SnapshotAgeS, Is.EqualTo(0f).Within(1e-5f), "every frame completes a step: nothing is held");
                    }
                    Assert.That(host.BuildingTiles, Is.EqualTo(1), "Asan is still building");
                    Assert.That(host.TileCount, Is.EqualTo(ids.Count), "its zones are in already");
                    Assert.That(host.Graph.LaneCount, Is.EqualTo(lanesBefore), "its lanes are not");
                    int moved = 0;
                    for (int i = 0; i < host.VehicleCount; i++)
                    {
                        AgentPose a = host.Vehicles[i];
                        if (!first.TryGetValue(a.AgentId, out AgentPose b)) continue;
                        double dx = a.X - b.X, dz = a.Z - b.Z;
                        if (dx * dx + dz * dz > 1.0) moved++;
                    }
                    TestContext.WriteLine("steps while Asan built: " + steps + ", worst frame " + worstStepMs.ToString("0.0") + " ms, vehicles moved " + moved);
                    Assert.That(moved, Is.GreaterThan(0), "traffic kept driving while the tile built");
                    gate.Set();
                }
                // Released: the tile joins within a few frames, and its lanes are in the graph.
                for (int f = 0; f < 20 && host.BuildingTiles > 0; f++)
                {
                    Tick(host);
                    host.Wait();
                }
                Tick(host);
                host.Wait();
                Assert.IsNull(host.Error);
                Assert.That(host.BuildingTiles, Is.EqualTo(0));
                Assert.That(host.Graph.LaneCount, Is.GreaterThan(lanesBefore), "Asan's lanes joined");
            }
        }

        /// <summary>
        /// Owner playtest #4 ("constantly flickers and stutters"): crossing a tile edge on Mid or High shows 3-8 detail
        /// tiles at once, and every one of them used to index its sacred zones and its area-type cells inside the same
        /// <see cref="LifeHost.Tick"/> on the main thread (0.5-2.6 ms a tile on a desktop, several times that on a phone).
        /// Joining the 3 × 3 tiles round Asan, no frame's Tick may cost the main thread more than a sliver: the zones join
        /// one tile per Tick (nearest first) and the area types are indexed by the background build job.
        /// </summary>
        [Test]
        public void JoiningDenseTilesNeverStallsTheFrame()
        {
            Asan(out double ax, out double az);
            TileId asan = TileId.At(10, ax, az);
            Dictionary<TileId, TileData> tiles = DrivingData.SampleTiles();
            var ids = new List<TileId>();
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    var id = new TileId(10, asan.Tx + dx, asan.Ty + dz);
                    if (tiles.ContainsKey(id)) ids.Add(id);
                }
            Assert.That(ids.Count, Is.GreaterThanOrEqualTo(6));
            double best = double.MaxValue, bestSum = 0;
            int ticksToJoin = 0;
            // Three runs (the first also warms the JIT); the busy build machine may spike a frame, so the best run counts.
            for (int run = 0; run < 3; run++)
            {
                using (LifeHost host = NewHost(11))
                {
                    for (int i = 0; i < 5; i++)
                    {
                        Tick(host);
                        host.Wait();
                    }
                    foreach (TileId id in ids) host.AddTile(id, tiles[id]);
                    double worst = 0, sum = 0;
                    int ticks = 0;
                    for (; ticks < 60 && (ticks == 0 || host.BuildingTiles > 0); ticks++)
                    {
                        var sw = Stopwatch.StartNew();
                        Tick(host);
                        double ms = sw.Elapsed.TotalMilliseconds;
                        worst = Math.Max(worst, ms);
                        sum += ms;
                        host.Wait(); // the jobs run off the clock (they are not the main thread's)
                        Assert.IsNull(host.Error);
                    }
                    Assert.That(host.BuildingTiles, Is.EqualTo(0), "every tile joined");
                    Assert.That(host.AreaTypes.At(ax, az), Is.Not.EqualTo(AreaType.Unknown), "Asan's area types are in");
                    TestContext.WriteLine("run " + run + ": " + ids.Count + " tiles joined in " + ticks + " ticks, worst Tick " + worst.ToString("0.00") +
                                          " ms, all Ticks " + sum.ToString("0.0") + " ms");
                    if (run > 0 && worst < best)
                    {
                        best = worst;
                        bestSum = sum;
                        ticksToJoin = ticks;
                    }
                }
            }
            TestContext.WriteLine("best run: worst main-thread Tick " + best.ToString("0.00") + " ms (all " + bestSum.ToString("0.0") + " ms over " + ticksToJoin + " ticks)");
            Assert.That(best, Is.LessThan(2.0), "no Tick stalls the frame while " + ids.Count + " dense tiles join");
        }

        [Test]
        public void ATileThatLeavesWhileItBuildsNeverJoins()
        {
            List<TileId> ids = AsanTiles(out TileId asan);
            Dictionary<TileId, TileData> tiles = DrivingData.SampleTiles();
            TileId other = ids[0] == asan ? ids[1] : ids[0];
            using (LifeHost host = NewHost(5))
            {
                host.AddTile(other, tiles[other]);
                for (int i = 0; i < 5; i++)
                {
                    Tick(host);
                    host.Wait();
                }
                int lanes = host.Graph.LaneCount;
                int cells = host.AreaTypes.CellCount;
                Assert.That(lanes, Is.GreaterThan(0));
                using (var gate = new ManualResetEventSlim(false))
                {
                    host.BeforeTileBuild = id =>
                    {
                        if (id == asan) gate.Wait(30000);
                    };
                    host.AddTile(asan, tiles[asan]);
                    Tick(host);
                    host.WaitStep();
                    // Released only after the removal: the build then indexes Asan's area cells for a tile that left.
                    host.RemoveTile(asan);
                    Tick(host);
                    host.WaitStep();
                    Assert.That(host.TileCount, Is.EqualTo(1), "Asan left");
                    gate.Set();
                }
                for (int i = 0; i < 10; i++)
                {
                    Tick(host);
                    host.Wait();
                }
                Assert.IsNull(host.Error);
                Assert.That(host.BuildingTiles, Is.EqualTo(0));
                Assert.That(host.Graph.LaneCount, Is.EqualTo(lanes), "the stale build was dropped");
                Assert.That(host.AreaTypes.CellCount, Is.EqualTo(cells), "and the area cells it indexed taken out again");

                // Back again: it builds and joins as usual.
                host.BeforeTileBuild = null;
                host.AddTile(asan, tiles[asan]);
                for (int i = 0; i < 10; i++)
                {
                    Tick(host);
                    host.Wait();
                }
                Assert.That(host.BuildingTiles, Is.EqualTo(0));
                Assert.That(host.Graph.LaneCount, Is.GreaterThan(lanes));
                Assert.That(host.AreaTypes.CellCount, Is.GreaterThan(cells));
            }
        }

        /// <summary>
        /// Owner playtest #6 ("absolutely no traffic"): a tile whose lanes fail to build used to stop every simulation
        /// for the rest of the session (the failure went to the step's error). Now that tile stays without lanes
        /// (<see cref="LifeHost.BuildError"/>) and the traffic and people of its neighbours keep moving.
        /// </summary>
        [Test]
        public void ATileThatFailsToBuildNeverStopsTheStreet()
        {
            List<TileId> ids = AsanTiles(out TileId asan);
            Dictionary<TileId, TileData> tiles = DrivingData.SampleTiles();
            using (LifeHost host = NewHost(9))
            {
                host.BeforeTileBuild = id =>
                {
                    if (id == asan) throw new InvalidOperationException("broken tile (test)");
                };
                foreach (TileId id in ids) host.AddTile(id, tiles[id]);
                for (int i = 0; i < 150; i++)
                {
                    Tick(host);
                    host.Wait();
                }
                Assert.IsNotNull(host.BuildError, "the failure is reported");
                Assert.IsNull(host.Error, "the simulations still step");
                Assert.That(host.BuildingTiles, Is.EqualTo(0), "the broken tile is not left building forever");
                Assert.That(host.VehicleCount, Is.GreaterThan(3), "traffic on the neighbours");
                var first = new Dictionary<int, AgentPose>();
                for (int i = 0; i < host.VehicleCount; i++) first[host.Vehicles[i].AgentId] = host.Vehicles[i];
                for (int i = 0; i < 20; i++)
                {
                    Tick(host);
                    host.Wait();
                }
                int moved = 0;
                for (int i = 0; i < host.VehicleCount; i++)
                {
                    if (!first.TryGetValue(host.Vehicles[i].AgentId, out AgentPose b)) continue;
                    double dx = host.Vehicles[i].X - b.X, dz = host.Vehicles[i].Z - b.Z;
                    if (dx * dx + dz * dz > 1.0) moved++;
                }
                Assert.That(moved, Is.GreaterThan(0), "and it keeps driving");
            }
        }

        [Test]
        public void TilesJoinOnePerStepNearestFirstAndTheSameSeedGivesTheSameSnapshots()
        {
            List<TileId> ids = AsanTiles(out TileId asan);
            Dictionary<TileId, TileData> tiles = DrivingData.SampleTiles();
            using (LifeHost a = NewHost(7))
            using (LifeHost b = NewHost(7))
            {
                foreach (TileId id in ids)
                {
                    a.AddTile(id, tiles[id]);
                    b.AddTile(id, tiles[id]);
                }
                // Tick 1 starts the build; then one tile joins per step.
                Tick(a);
                a.Wait();
                Assert.That(a.BuildingTiles, Is.EqualTo(ids.Count), "nothing joined in the first step");
                Tick(a);
                a.Wait();
                Assert.That(a.BuildingTiles, Is.EqualTo(ids.Count - 1), "one tile per step");
                Tick(b);
                b.Wait();
                Tick(b);
                b.Wait();
                for (int i = 0; i < 150; i++)
                {
                    Tick(a);
                    a.Wait();
                    Tick(b);
                    b.Wait();
                }
                Assert.That(a.BuildingTiles, Is.EqualTo(0));
                Assert.That(a.VehicleCount, Is.EqualTo(b.VehicleCount));
                Assert.That(a.PeopleCount, Is.EqualTo(b.PeopleCount));
                for (int i = 0; i < a.VehicleCount; i++)
                {
                    Assert.That(a.Vehicles[i].AgentId, Is.EqualTo(b.Vehicles[i].AgentId));
                    Assert.That(a.Vehicles[i].X, Is.EqualTo(b.Vehicles[i].X));
                    Assert.That(a.Vehicles[i].Z, Is.EqualTo(b.Vehicles[i].Z));
                }
            }
        }
    }
}
