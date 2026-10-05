using System;
using System.Diagnostics;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class MeshingPerformanceTests
    {
        /// <summary>
        /// Benchmark-style check: meshing the densest level-10 tile of the sample (terrain at the Mid tier's step,
        /// roads, buildings and areas) must stay well inside the streaming budget. The target is under 60 ms in a
        /// Release build; the assertion is generous so Debug runs and slow CI machines pass, and the measured times
        /// are printed.
        /// </summary>
        [Test]
        public void DensestLeafTileMeshesQuickly()
        {
            TileId id = StreamingSampleRegion.DensestLeaf();
            TileData t = StreamingSampleRegion.Tile(id);
            var terrain = new MeshData();
            var roads = new MeshData();
            var buildings = new MeshData();
            var areas = new MeshData();
            var to = new TerrainOptions { Step = 2 };
            var ro = new RoadOptions();
            var bo = new BuildingOptions();
            var ao = new AreaOptions();

            const int Runs = 7;
            var tTerrain = new double[Runs];
            var tRoads = new double[Runs];
            var tBuildings = new double[Runs];
            var tAreas = new double[Runs];
            var tTotal = new double[Runs];
            var sw = new Stopwatch();
            for (int run = -2; run < Runs; run++) // two warm-up runs (JIT, buffer growth)
            {
                terrain.Clear();
                roads.Clear();
                buildings.Clear();
                areas.Clear();
                sw.Restart();
                var sampler = new TileHeightSampler(t, to.Step);
                TerrainMesher.Build(t, t.Tile, to, terrain);
                double a = sw.Elapsed.TotalMilliseconds;
                RoadMesher.Build(t, sampler, ro, roads);
                double b = sw.Elapsed.TotalMilliseconds;
                BuildingMesher.Build(t, sampler, bo, buildings);
                double c = sw.Elapsed.TotalMilliseconds;
                AreaMesher.Build(t, sampler, ao, areas);
                double d = sw.Elapsed.TotalMilliseconds;
                if (run < 0) continue;
                tTerrain[run] = a;
                tRoads[run] = b - a;
                tBuildings[run] = c - b;
                tAreas[run] = d - c;
                tTotal[run] = d;
            }
            double median = Median(tTotal);
            TestContext.WriteLine("{0}: {1} buildings, {2} roads, {3} areas", id, t.Buildings.Count, t.Roads.Count, t.Areas.Count);
            TestContext.WriteLine("median ms: terrain {0:0.0}, roads {1:0.0}, buildings {2:0.0}, areas {3:0.0}, total {4:0.0}",
                                  Median(tTerrain), Median(tRoads), Median(tBuildings), Median(tAreas), median);
            TestContext.WriteLine("triangles: terrain {0}, roads {1}, buildings {2}, areas {3}",
                                  terrain.TriangleCount, roads.TriangleCount, buildings.TriangleCount, areas.TriangleCount);
#if DEBUG
            const double Bound = 400; // unoptimised build on a shared CI machine
#else
            const double Bound = 120; // target is < 60 ms; generous for slow machines
#endif
            Assert.That(median, Is.LessThan(Bound));
        }

        private static MeshData[] MeshAll(TileData t)
        {
            var layers = new[] { new MeshData(), new MeshData(), new MeshData(), new MeshData() };
            var s = new TileHeightSampler(t, 2);
            TerrainMesher.Build(t, t.Tile, new TerrainOptions { Step = 2 }, layers[0]);
            RoadMesher.Build(t, s, new RoadOptions(), layers[1]);
            BuildingMesher.Build(t, s, new BuildingOptions(), layers[2]);
            AreaMesher.Build(t, s, new AreaOptions { IncludeSubtle = true }, layers[3]);
            return layers;
        }

        private static void AssertSame(MeshData a, MeshData b, string what)
        {
            Assert.That(b.VertexCount, Is.EqualTo(a.VertexCount), what);
            Assert.That(b.IndexCount, Is.EqualTo(a.IndexCount), what);
            for (int i = 0; i < a.VertexCount * 3; i++)
                if (a.Positions[i] != b.Positions[i] || a.Normals[i] != b.Normals[i]) Assert.Fail(what + ": vertex data differs at " + i);
            for (int i = 0; i < a.VertexCount * 4; i++)
                if (a.Colors[i] != b.Colors[i]) Assert.Fail(what + ": colour differs at " + i);
            for (int i = 0; i < a.IndexCount; i++)
                if (a.Indices[i] != b.Indices[i]) Assert.Fail(what + ": index differs at " + i);
        }

        /// <summary>Meshing is deterministic, and the thread-static scratch of the meshers keeps concurrent workers
        /// (the World meshes on several threads) from interfering.</summary>
        [Test]
        public void MeshingIsDeterministicAcrossThreads()
        {
            var tiles = StreamingSampleRegion.TilesAt(10).GetRange(10, 8);
            var data = new TileData[tiles.Count];
            for (int i = 0; i < tiles.Count; i++) data[i] = StreamingSampleRegion.Tile(tiles[i]);
            var reference = new MeshData[tiles.Count][];
            for (int i = 0; i < tiles.Count; i++) reference[i] = MeshAll(data[i]);
            var parallel = new MeshData[tiles.Count][];
            var threads = new System.Threading.Thread[tiles.Count];
            for (int i = 0; i < tiles.Count; i++)
            {
                int k = i;
                threads[i] = new System.Threading.Thread(() => parallel[k] = MeshAll(data[k]));
                threads[i].Start();
            }
            foreach (var th in threads) th.Join();
            for (int i = 0; i < tiles.Count; i++)
                for (int l = 0; l < 4; l++) AssertSame(reference[i][l], parallel[i][l], tiles[i] + " layer " + l);
        }

        private static double Median(double[] v)
        {
            var c = (double[])v.Clone();
            Array.Sort(c);
            return c[c.Length / 2];
        }
    }
}
