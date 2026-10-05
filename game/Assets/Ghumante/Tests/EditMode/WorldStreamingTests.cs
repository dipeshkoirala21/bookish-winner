using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;
using Ghumante.Core.Streaming;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Tests.EditMode
{
    /// <summary>
    /// The engine-free streaming core (TileResidency, StreamingScheduler, TileBuild) on synthetic nodes and on the
    /// real sample region, with an inline job runner and a fake clock (no Unity API, no frames).
    /// </summary>
    public class WorldStreamingTests
    {
        private static SelectedNode Exact(int level, int tx, int ty)
        {
            var t = new TileId(level, tx, ty);
            return new SelectedNode(t, t);
        }

        // ------------------------------------------------------------------------------------------------------
        // TileResidency

        [Test]
        public void ResidencyLoadsQueuedNodesNearestFirstAndShowsThemWhenReady()
        {
            var r = new TileResidency(16);
            var near = Exact(10, 100, 100);
            var far = Exact(10, 103, 100);
            double cx = near.Area.X0 + 10, cz = near.Area.Z0 + 10;
            r.SetDesired(new List<SelectedNode> { far, near }, cx, cz);
            Assert.AreEqual(2, r.QueuedCount);
            Assert.AreEqual(near, r.Queue[0], "nearest first");

            r.BeginLoad(near);
            Assert.AreEqual(1, r.QueuedCount);
            Assert.AreEqual(TileResidency.NodeState.Loading, r.StateOf(near));
            Assert.IsTrue(r.EndLoad(near, false));

            var show = new List<SelectedNode>();
            var hide = new List<SelectedNode>();
            var release = new List<SelectedNode>();
            r.Resolve(show, hide, release);
            CollectionAssert.AreEqual(new[] { near }, show);
            Assert.IsEmpty(hide);
            Assert.IsEmpty(release);
            Assert.IsTrue(r.IsVisible(near));
            Assert.AreEqual(1, r.ReadyDesiredCount);
        }

        [Test]
        public void RetiringParentStaysUntilEveryChildIsReady()
        {
            var r = new TileResidency(32);
            var parent = Exact(9, 50, 50);
            var children = new List<SelectedNode>();
            for (int q = 0; q < 4; q++)
            {
                TileId c = TileArea.Child(parent.Area, q);
                children.Add(new SelectedNode(c, c));
            }
            var show = new List<SelectedNode>();
            var hide = new List<SelectedNode>();
            var release = new List<SelectedNode>();
            double x = parent.Area.X0 + 100, z = parent.Area.Z0 + 100;

            r.SetDesired(new List<SelectedNode> { parent }, x, z);
            r.BeginLoad(parent);
            r.EndLoad(parent, false);
            r.Resolve(show, hide, release);
            Assert.IsTrue(r.IsVisible(parent));

            // Zoom in: the parent retires, its children load one by one.
            r.SetDesired(children, x, z);
            for (int i = 0; i < 3; i++)
            {
                r.BeginLoad(children[i]);
                r.EndLoad(children[i], false);
                r.Resolve(show, hide, release);
                Assert.IsTrue(r.IsVisible(parent), "parent keeps covering while a child is missing");
                Assert.IsEmpty(show, "ready children stay hidden under the parent");
                Assert.IsEmpty(release);
            }
            r.BeginLoad(children[3]);
            r.EndLoad(children[3], false);
            r.Resolve(show, hide, release);
            CollectionAssert.AreEqual(new[] { parent }, release);
            Assert.AreEqual(4, show.Count, "all children appear in the same step the parent goes");
            Assert.AreEqual(TileResidency.NodeState.None, r.StateOf(parent));
            Assert.AreEqual(4, r.VisibleCount);
        }

        [Test]
        public void ZoomingOutReleasesChildrenOnceTheParentIsReady()
        {
            var r = new TileResidency(32);
            var parent = Exact(9, 50, 50);
            var children = new List<SelectedNode>();
            for (int q = 0; q < 4; q++)
            {
                TileId c = TileArea.Child(parent.Area, q);
                children.Add(new SelectedNode(c, c));
            }
            var show = new List<SelectedNode>();
            var hide = new List<SelectedNode>();
            var release = new List<SelectedNode>();
            double x = parent.Area.X0, z = parent.Area.Z0;
            r.SetDesired(children, x, z);
            foreach (SelectedNode c in children)
            {
                r.BeginLoad(c);
                r.EndLoad(c, false);
            }
            r.Resolve(show, hide, release);
            Assert.AreEqual(4, r.VisibleCount);

            r.SetDesired(new List<SelectedNode> { parent }, x, z);
            r.Resolve(show, hide, release);
            Assert.AreEqual(4, r.VisibleCount, "children stay until the parent is ready");
            Assert.IsEmpty(release);
            r.BeginLoad(parent);
            r.EndLoad(parent, false);
            r.Resolve(show, hide, release);
            Assert.AreEqual(4, release.Count);
            CollectionAssert.AreEqual(new[] { parent }, show);
            Assert.AreEqual(1, r.VisibleCount);
        }

        [Test]
        public void LoadsThatEndAfterTheirNodeWasDeselectedAreDropped()
        {
            var r = new TileResidency(8);
            var a = Exact(10, 10, 10);
            r.SetDesired(new List<SelectedNode> { a }, a.Area.X0, a.Area.Z0);
            r.BeginLoad(a);
            r.SetDesired(new List<SelectedNode>(), 0, 0);
            Assert.AreEqual(1, r.LoadingCount);
            Assert.IsFalse(r.EndLoad(a, false), "no longer wanted");
            Assert.AreEqual(0, r.ResidentCount);
            Assert.AreEqual(TileResidency.NodeState.None, r.StateOf(a));
        }

        [Test]
        public void ResidentCapReleasesFarthestRetiringNodesWhenWorkWaits()
        {
            var r = new TileResidency(3);
            var show = new List<SelectedNode>();
            var hide = new List<SelectedNode>();
            var release = new List<SelectedNode>();
            var old = new List<SelectedNode> { Exact(10, 0, 0), Exact(10, 1, 0), Exact(10, 2, 0) };
            r.SetDesired(old, 0, 0);
            foreach (SelectedNode n in old)
            {
                r.BeginLoad(n);
                r.EndLoad(n, false);
            }
            r.Resolve(show, hide, release);
            Assert.AreEqual(3, r.VisibleCount);
            Assert.IsFalse(r.CanStartLoad);

            // A new selection elsewhere: old nodes do not overlap it, so they go at once.
            var fresh = new List<SelectedNode> { Exact(10, 0, 5) };
            r.SetDesired(fresh, 0, 5 * 1024);
            r.Resolve(show, hide, release);
            Assert.AreEqual(3, release.Count);
            Assert.IsTrue(r.CanStartLoad);

            // Overlapping retirees under the cap: the farthest is given up for the waiting node.
            var r2 = new TileResidency(2);
            var big = Exact(8, 0, 0);
            var farBig = Exact(8, 3, 0);
            r2.SetDesired(new List<SelectedNode> { big, farBig }, 0, 0);
            foreach (SelectedNode n in new[] { big, farBig })
            {
                r2.BeginLoad(n);
                r2.EndLoad(n, false);
            }
            r2.Resolve(show, hide, release);
            TileId c0 = TileArea.Child(big.Area, 0), f0 = TileArea.Child(farBig.Area, 0);
            r2.SetDesired(new List<SelectedNode> { new SelectedNode(c0, c0), new SelectedNode(f0, f0) }, 0, 0);
            r2.Resolve(show, hide, release);
            CollectionAssert.AreEqual(new[] { farBig }, release, "the farther retiree goes first");
            Assert.IsTrue(r2.CanStartLoad);
        }

        // ------------------------------------------------------------------------------------------------------
        // StreamingScheduler on the sample region

        private static StreamingScheduler NewScheduler(FakeTileSink sink, int tier, out TileGroundQuery ground)
        {
            var meshing = new MeshingSettings();
            ground = new TileGroundQuery(meshing.Roads);
            StreamingConfig config = StreamingConfig.ForTier(tier);
            var selector = TileSelector.ForPack(config, SampleRegion.Pack);
            return new StreamingScheduler(SampleRegion.Pack, config, selector, ground, sink, new InlineJobRunner(), sink.Clock, meshing)
            {
                UploadBudgetMs = 2.0,
            };
        }

        private static int TickUntilSettled(StreamingScheduler s, FakeTileSink sink, double x, double z, int maxTicks)
        {
            for (int i = 0; i < maxTicks; i++)
            {
                sink.UploadsThisTick = 0;
                s.Tick(x, z);
                Assert.LessOrEqual(sink.UploadsThisTick, 2, "a 2 ms budget at 1 ms per upload allows two uploads per tick");
                Assert.LessOrEqual(s.Residency.ResidentCount, s.Residency.MaxResident);
                sink.AssertNoOverlap();
                if (s.IsSettled) return i + 1;
            }
            Assert.Fail("streaming did not settle in " + maxTicks + " ticks: " + s.Stats.Format());
            return maxTicks;
        }

        [Test]
        public void SchedulerStreamsTheSampleRegionAroundThamel()
        {
            var sink = new FakeTileSink();
            TileGroundQuery ground;
            using (StreamingScheduler s = NewScheduler(sink, StreamingConfig.TierLow, out ground))
            {
                int ticks = TickUntilSettled(s, sink, SampleRegion.ThamelX, SampleRegion.ThamelZ, 4000);
                Assert.Greater(ticks, 1);
                Assert.AreEqual(1f, s.Progress);
                Assert.AreEqual(0, s.Stats.Failed);

                // Every selected node is visible (or has nothing to draw), and nothing else is.
                var visible = new HashSet<SelectedNode>(sink.Visible());
                foreach (SelectedNode n in s.Selection)
                    Assert.IsTrue(visible.Contains(n) || s.Residency.IsEmpty(n), n + " should be visible");
                Assert.AreEqual(s.Residency.VisibleCount, visible.Count);

                // The ground follows the screen: level-10 data under the focus, with roads near Thamel.
                GroundSample g;
                Assert.IsTrue(ground.TrySample(SampleRegion.ThamelX, SampleRegion.ThamelZ, out g));
                Assert.AreEqual(10, g.TileLevel);
                Assert.That(g.Height, Is.InRange(1250f, 1400f), "Thamel lies about 1 300 m above sea level");
                RoadHit hit;
                Assert.IsTrue(ground.TryNearestRoad(SampleRegion.ThamelX, SampleRegion.ThamelZ, 200, out hit));
                Assert.AreEqual(s.Stats.FinestVisibleLevel, 10);
                Assert.Greater(s.Stats.MeshBytes, 0);
            }
            Assert.IsEmpty(sink.Views, "dispose releases every view");
        }

        [Test]
        public void SchedulerSwapsLevelsWithoutOverlapsWhileTheFocusMoves()
        {
            var sink = new FakeTileSink();
            TileGroundQuery ground;
            using (StreamingScheduler s = NewScheduler(sink, StreamingConfig.TierLow, out ground))
            {
                TickUntilSettled(s, sink, SampleRegion.ThamelX, SampleRegion.ThamelZ, 4000);
                int groundBefore = s.GroundVersion;

                // Ride towards Boudhanath in 200 m steps, a few ticks per step, checking the swap rule every tick.
                double dx = SampleRegion.BoudhaX - SampleRegion.ThamelX, dz = SampleRegion.BoudhaZ - SampleRegion.ThamelZ;
                double len = Math.Sqrt(dx * dx + dz * dz);
                for (double d = 0; d <= len; d += 200)
                {
                    double x = SampleRegion.ThamelX + dx * d / len, z = SampleRegion.ThamelZ + dz * d / len;
                    for (int k = 0; k < 3; k++)
                    {
                        s.Tick(x, z);
                        sink.AssertNoOverlap();
                        Assert.LessOrEqual(s.Residency.ResidentCount, s.Residency.MaxResident);
                    }
                }
                TickUntilSettled(s, sink, SampleRegion.BoudhaX, SampleRegion.BoudhaZ, 4000);
                Assert.Greater(s.GroundVersion, groundBefore);
                GroundSample g;
                Assert.IsTrue(ground.TrySample(SampleRegion.BoudhaX, SampleRegion.BoudhaZ, out g));
                Assert.AreEqual(10, g.TileLevel);
                Assert.Greater(sink.Released.Count, 0, "tiles left behind were released");
            }
        }

        [Test]
        public void AFailingUploadLeavesAHoleButNeverStallsStreaming()
        {
            var sink = new FakeTileSink();
            TileGroundQuery ground;
            using (StreamingScheduler s = NewScheduler(sink, StreamingConfig.TierLow, out ground))
            {
                var warnings = new List<string>();
                s.Warning = warnings.Add;
                TileId leaf = TileId.At(10, SampleRegion.ThamelX, SampleRegion.ThamelZ);
                sink.ThrowFor = new SelectedNode(leaf, leaf);
                TickUntilSettled(s, sink, SampleRegion.ThamelX, SampleRegion.ThamelZ, 4000);
                Assert.AreEqual(1, s.Stats.Failed);
                Assert.AreEqual(1, warnings.Count);
                StringAssert.Contains("failed to upload", warnings[0]);
                Assert.IsTrue(s.Residency.IsEmpty(sink.ThrowFor.Value));
                Assert.IsFalse(sink.Views.ContainsKey(sink.ThrowFor.Value), "the broken view was released");
                Assert.IsFalse(ground.Contains(leaf));
            }
        }

        [Test]
        public void TeleportReplacesEverythingAndDisposeLeavesNoGround()
        {
            var sink = new FakeTileSink();
            TileGroundQuery ground;
            StreamingScheduler s = NewScheduler(sink, StreamingConfig.TierLow, out ground);
            TickUntilSettled(s, sink, SampleRegion.ThamelX, SampleRegion.ThamelZ, 4000);
            var before = new HashSet<SelectedNode>(sink.Visible());

            // 25 km away (still inside the horizon coverage): nothing visible before is selected now.
            double tx = SampleRegion.ThamelX + 25000, tz = SampleRegion.ThamelZ - 4000;
            TickUntilSettled(s, sink, tx, tz, 4000);
            foreach (SelectedNode n in sink.Visible())
                if (before.Contains(n)) Assert.IsTrue(s.Residency.IsDesired(n), n + " survived only because it is still selected");
            GroundSample g;
            Assert.IsTrue(ground.TrySample(tx, tz, out g), "coarse horizon terrain under the new focus");
            Assert.Less(g.TileLevel, 10);

            s.Dispose();
            Assert.AreEqual(0, ground.Count);
            Assert.IsEmpty(sink.Views);
            s.Tick(tx, tz); // ignored after dispose
            Assert.IsEmpty(sink.Views);
        }

        [Test]
        public void WorkerThreadsBuildInParallelAndTheOwnedPackClosesAfterTheLastBuild()
        {
            var sink = new FakeTileSink();
            var meshing = new MeshingSettings();
            var ground = new TileGroundQuery(meshing.Roads);
            var pack = new PackReader(new FileStream(SampleRegion.FilePath(".ghpk"), FileMode.Open, FileAccess.Read, FileShare.Read), true);
            var s = new StreamingScheduler(pack, StreamingConfig.ForTier(StreamingConfig.TierLow), null, ground, sink,
                                           new ThreadPoolJobRunner(), sink.Clock, meshing, true);
            int ticks = 0;
            while (!s.IsSettled && ticks++ < 20000)
            {
                s.Tick(SampleRegion.ThamelX, SampleRegion.ThamelZ);
                Assert.LessOrEqual(s.Stats.Jobs, s.MaxConcurrentJobs);
                sink.AssertNoOverlap();
                Thread.Sleep(1);
            }
            Assert.IsTrue(s.IsSettled, s.Stats.Format());
            Assert.AreEqual(0, s.Stats.Failed);

            // Leave while builds are running: dispose drops them and the pack closes after the last one.
            for (int i = 0; i < 5; i++) s.Tick(SampleRegion.BoudhaX, SampleRegion.BoudhaZ);
            s.Dispose();
            Assert.IsEmpty(sink.Views);
            ulong key = pack.Entries[0].Key;
            bool closed = false;
            for (int i = 0; i < 2000 && !closed; i++)
            {
                try
                {
                    pack.GetTileBytes(key);
                    Thread.Sleep(2);
                }
                catch (ObjectDisposedException)
                {
                    closed = true;
                }
            }
            Assert.IsTrue(closed, "the owned pack is closed once no build uses it");
        }

        [Test]
        public void BuildMeshesEveryLayerOfAnExactLeafAndCropsCoarseSources()
        {
            StreamingConfig config = StreamingConfig.ForTier(StreamingConfig.TierMid);
            var meshing = new MeshingSettings();
            var ground = new TileGroundQuery(meshing.Roads);
            TileId leaf = TileId.At(10, SampleRegion.ThamelX, SampleRegion.ThamelZ);
            Assert.IsTrue(SampleRegion.Pack.Contains(leaf));

            var b = new TileBuild();
            b.Node = new SelectedNode(leaf, leaf);
            TileBuild.Execute(b, SampleRegion.Pack, config, meshing, ground);
            Assert.IsTrue(b.DecodedSource);
            Assert.AreEqual(2, b.Step, "Mid tier: 64 quads over a 129-sample leaf");
            Assert.IsTrue(b.HasLayer(TileLayers.Terrain));
            Assert.IsTrue(b.HasLayer(TileLayers.Roads));
            Assert.IsTrue(b.HasLayer(TileLayers.Buildings));
            Assert.IsNotNull(b.Roads);
            Assert.IsTrue(b.Sampler.HasHeights);
            Assert.AreEqual(65 * 65 + 4 * 65, b.Layers[TileLayers.Terrain].VertexCount, "64x64 quads plus skirts");
            Assert.IsTrue(b.UsesShortIndices(TileLayers.Terrain));
            for (int k = 0; k < b.Layers[TileLayers.Terrain].IndexCount; k++)
                Assert.AreEqual(b.Layers[TileLayers.Terrain].Indices[k], b.ShortIndices[TileLayers.Terrain][k]);
            float x0, y0, z0, x1, y1, z1;
            b.GetBounds(TileLayers.Terrain, out x0, out y0, out z0, out x1, out y1, out z1);
            Assert.AreEqual(0f, x0, 1e-3f);
            Assert.AreEqual((float)leaf.Size, x1, 1e-3f);
            Assert.Greater(y1, 1200f);

            // A level-10 area outside the detail coverage, cropped from a coarse horizon tile: terrain only.
            TileId far = TileId.At(10, SampleRegion.ThamelX + 25000, SampleRegion.ThamelZ - 4000);
            TileId src = default(TileId);
            for (int l = 9; l >= 0; l--)
            {
                TileId a = TileArea.AncestorAt(far, l);
                if (SampleRegion.Pack.Contains(a))
                {
                    src = a;
                    break;
                }
            }
            Assert.Less(src.Level, 8, "a horizon source");
            var c = new TileBuild();
            c.Node = new SelectedNode(far, src);
            TileBuild.Execute(c, SampleRegion.Pack, config, meshing, ground);
            Assert.IsTrue(c.HasLayer(TileLayers.Terrain));
            Assert.IsFalse(c.HasLayer(TileLayers.Roads));
            Assert.IsFalse(c.HasLayer(TileLayers.Buildings));
            Assert.IsNull(c.Roads);
            Assert.AreEqual(far, c.Sampler.Grid.Area);
            Assert.Greater(c.CurvatureMarginM, 0f);
        }

        // ------------------------------------------------------------------------------------------------------
        // Upload chunks and build pooling (dense city tiles)

        /// <summary>The densest level-10 tiles of the sample: buildings layers of 170 000 to 290 000 vertices.</summary>
        private static readonly TileId DenseLeaf = new TileId(10, 516, 161);

        private static readonly TileId DenseLeaf2 = new TileId(10, 519, 162);

        [Test]
        public void DenseCityLayersAreSplitIntoUploadChunksThatRebuildTheSameTriangles()
        {
            Assert.IsTrue(SampleRegion.Pack.Contains(DenseLeaf));
            StreamingConfig config = StreamingConfig.ForTier(StreamingConfig.TierMid);
            var meshing = new MeshingSettings();
            var b = new TileBuild();
            b.Node = new SelectedNode(DenseLeaf, DenseLeaf);
            TileBuild.Execute(b, SampleRegion.Pack, config, meshing, null);
            MeshData buildings = b.Layers[TileLayers.Buildings];
            Assert.Greater(buildings.VertexCount, 4 * TileBuild.UploadChunkVertices, "a dense city layer");

            // The same layer meshed again without chunking: the reference triangles.
            var reference = new MeshData();
            BuildingMesher.Build(b.Source, b.Sampler, meshing.Buildings, reference);
            Assert.AreEqual(buildings.IndexCount, reference.IndexCount);

            int expectedIndex = 0, layer = -1, buildingChunks = 0;
            for (int k = 0; k < b.Chunks.Count; k++)
            {
                UploadChunk c = b.Chunks[k];
                if (c.Layer != layer)
                {
                    Assert.Greater(c.Layer, layer, "chunks in layer order");
                    if (layer >= 0) Assert.AreEqual(b.Layers[layer].IndexCount, expectedIndex, "chunks cover the whole layer");
                    layer = c.Layer;
                    expectedIndex = 0;
                }
                Assert.AreEqual(expectedIndex, c.FirstIndex, "chunks are consecutive runs of triangles");
                Assert.Greater(c.IndexCount, 0);
                Assert.AreEqual(0, c.IndexCount % 3);
                Assert.LessOrEqual(c.VertexCount, TileBuild.UploadChunkVertices);
                Assert.IsTrue(c.UsesShortIndices);
                Assert.Less(b.ChunkBytes(k), 1300000L, "about 1 MB per chunk at most");
                MeshData m = b.Layers[c.Layer];
                ushort[] s = b.ShortIndices[c.Layer];
                for (int i = c.FirstIndex; i < c.FirstIndex + c.IndexCount; i++)
                {
                    int v = m.Indices[i];
                    Assert.That(v, Is.InRange(0, c.VertexCount - 1));
                    Assert.AreEqual(v, s[i]);
                    int p = (c.FirstVertex + v) * 3;
                    Assert.That(m.Positions[p], Is.InRange(c.MinX, c.MaxX));
                    Assert.That(m.Positions[p + 1], Is.InRange(c.MinY, c.MaxY));
                    Assert.That(m.Positions[p + 2], Is.InRange(c.MinZ, c.MaxZ));
                    if (c.Layer == TileLayers.Buildings) Assert.AreEqual(reference.Indices[i], c.FirstVertex + v);
                }
                if (c.Layer == TileLayers.Buildings) buildingChunks++;
                expectedIndex += c.IndexCount;
            }
            Assert.AreEqual(b.Layers[layer].IndexCount, expectedIndex);
            Assert.GreaterOrEqual(buildingChunks, (buildings.VertexCount + TileBuild.UploadChunkVertices - 1) / TileBuild.UploadChunkVertices);
            Assert.IsTrue(b.UsesShortIndices(TileLayers.Buildings), "every chunk keeps 16-bit indices");
        }

        [Test]
        public void SplitLayerCutsAtTheVertexSpanAndIsolatesWideTriangles()
        {
            var m = new MeshData();
            for (int i = 0; i < 70000; i++) m.AddVertex(i, 0, 0, 0, 1, 0, 0xFFFFFFFFu);
            m.AddTriangle(0, 1, 2);
            m.AddTriangle(3, 4, 5);
            m.AddTriangle(6, 7, 8); // 0..8 spans 9 > 8 vertices: a new chunk
            m.AddTriangle(0, 69999, 1); // wider than any chunk: alone, with 32-bit indices
            m.AddTriangle(10, 11, 12);
            var chunks = new List<UploadChunk>();
            ushort[] s = null;
            TileBuild.SplitLayer(m, TileLayers.Roads, 8, chunks, ref s);

            Assert.AreEqual(4, chunks.Count);
            int[] first = { 0, 6, 9, 12 }, count = { 6, 3, 3, 3 }, v0 = { 0, 6, 0, 10 }, vc = { 6, 3, 70000, 3 };
            for (int k = 0; k < 4; k++)
            {
                Assert.AreEqual(TileLayers.Roads, chunks[k].Layer);
                Assert.AreEqual(first[k], chunks[k].FirstIndex, "chunk " + k);
                Assert.AreEqual(count[k], chunks[k].IndexCount, "chunk " + k);
                Assert.AreEqual(v0[k], chunks[k].FirstVertex, "chunk " + k);
                Assert.AreEqual(vc[k], chunks[k].VertexCount, "chunk " + k);
            }
            Assert.IsFalse(chunks[2].UsesShortIndices);
            Assert.AreEqual(69999f, chunks[2].MaxX);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5, 0, 1, 2, 0, 69999, 1, 0, 1, 2 },
                                      new ArraySegment<int>(m.Indices, 0, m.IndexCount), "indices rebased to their chunk");
            Assert.AreEqual(2, s[14]);

            // Empty layers add nothing.
            var empty = new MeshData();
            TileBuild.SplitLayer(empty, TileLayers.Areas, 8, chunks, ref s);
            Assert.AreEqual(4, chunks.Count);
        }

        [Test]
        public void UploadsStopAtTheByteBudgetAndSpreadDenseNodesOverSeveralTicks()
        {
            var sink = new FakeTileSink { MsPerUpload = 0 }; // the clock never runs out: only bytes limit a tick
            TileGroundQuery ground;
            using (StreamingScheduler s = NewScheduler(sink, StreamingConfig.TierMid, out ground))
            {
                s.UploadBudgetBytes = 1L << 20;
                int ticks = 0;
                while (!s.IsSettled)
                {
                    Assert.Less(ticks++, 6000, s.Stats.Format());
                    sink.BytesThisTick = 0;
                    sink.UploadsThisTick = 0;
                    s.Tick(SampleRegion.ThamelX, SampleRegion.ThamelZ);
                    if (sink.UploadsThisTick > 1)
                        Assert.Less(sink.BytesThisTick - sink.LastChunkBytes, s.UploadBudgetBytes, "no chunk starts once the tick's bytes are spent");
                }
                Assert.AreEqual(0, s.Stats.Failed);
                Assert.Greater(sink.LargestChunkBytes, s.UploadBudgetBytes / 2, "dense chunks were uploaded");
                Assert.Less(sink.LargestChunkBytes, 1300000L);
                Assert.Greater(sink.Uploads, s.Residency.ReadyCount, "dense nodes took several chunks");
            }
        }

        [Test]
        public void RecycledBuildsKeepTheBuffersOfDenseCityLayers()
        {
            Assert.IsTrue(SampleRegion.Pack.Contains(DenseLeaf2));
            StreamingConfig config = StreamingConfig.ForTier(StreamingConfig.TierMid);
            var meshing = new MeshingSettings();
            var b = new TileBuild();
            b.Node = new SelectedNode(DenseLeaf2, DenseLeaf2);
            TileBuild.Execute(b, SampleRegion.Pack, config, meshing, null);
            MeshData buildings = b.Layers[TileLayers.Buildings];
            Assert.Greater(buildings.VertexCount, 131072, "grew past the old 131 072-vertex pool cap");
            Assert.LessOrEqual(buildings.VertexCapacity, TileBuild.PoolKeepVertices);
            float[] positions = buildings.Positions;
            ushort[] shortIndices = b.ShortIndices[TileLayers.Buildings];

            b.Recycle();
            Assert.AreSame(buildings, b.Layers[TileLayers.Buildings], "the grown buffer is reused, not dropped as garbage");
            Assert.AreSame(positions, b.Layers[TileLayers.Buildings].Positions);
            Assert.AreSame(shortIndices, b.ShortIndices[TileLayers.Buildings]);
            Assert.AreEqual(0, b.Layers[TileLayers.Buildings].VertexCount);
            Assert.AreEqual(0, b.Chunks.Count);

            // Building the same node again does not grow anything.
            b.Node = new SelectedNode(DenseLeaf2, DenseLeaf2);
            b.Source = null;
            TileBuild.Execute(b, SampleRegion.Pack, config, meshing, null);
            Assert.AreSame(positions, b.Layers[TileLayers.Buildings].Positions);
        }
    }
}
