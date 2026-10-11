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
