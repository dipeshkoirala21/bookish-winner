using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Streaming;
using Ghumante.World.Buildings;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Perf pass (perf-roads): the triangles the streamer draws in one view, per tier, against the W2_DESIGN 10.4 slices
    /// (roads + decals 8 k / 20 k / 30 k, bridges counted with the roads as the streamer's counters do; terrain 30 k / 70 k /
    /// 110 k). The real selection of the sample around Kathmandu places is built exactly as the streamer builds it
    /// (<see cref="TileSelector"/>, <see cref="StreamingConfig.TerrainStep(SelectedNode, int)"/>, <see cref="TileBuild.Execute"/>
    /// with <see cref="MeshingSettings.ForTier"/>), every chunk is switched as <c>WorldStreamer.UpdateBands</c> switches it
    /// (road blocks by <see cref="RoadBlocks"/>, bridge pieces by <see cref="PieceLod.Choose"/>), and a chunk counts when its
    /// bounds meet the camera's horizontal view: a 70 degree wedge (the flying camera; the chase rigs use 55 to 62 degrees
    /// plus a small speed kick), with no far clip, worst of 16 headings.
    /// </summary>
    public class StreamingViewBudgetTests
    {
        private struct Place
        {
            public string Name;
            public double Lon, Lat;

            public Place(string name, double lon, double lat)
            {
                Name = name;
                Lon = lon;
                Lat = lat;
            }
        }

        private static readonly Place Asan = new Place("Asan", 85.3122, 27.7074);
        private static readonly Place Basantapur = new Place("Basantapur", 85.3070, 27.7043);
        private static readonly Place Boudha = new Place("Boudha", 85.3620, 27.7215);
        private static readonly Place Thamel = new Place("Thamel", 85.3103, 27.7152);
        private static readonly Place Chabahil = new Place("Chabahil (Ring Road)", 85.3466, 27.7174);

        /// <summary>Half the horizontal field of view of the in-view sums (degrees).</summary>
        internal const double HalfFovDeg = 35.0;

        internal const int Headings = 16;

        /// <summary>W2_DESIGN 10.4: roads + decals per tier in view.</summary>
        internal static readonly int[] RoadSlice = { 8000, 20000, 30000 };

        /// <summary>W2_DESIGN 10.4: terrain per tier in view.</summary>
        internal static readonly int[] TerrainSlice = { 30000, 70000, 110000 };

        private static readonly string[] TierName = { "Low", "Mid", "High" };

        private static readonly Dictionary<(ulong, int), TileBuild> RoadBuilds = new Dictionary<(ulong, int), TileBuild>();
        private static readonly Dictionary<(ulong, ulong, int), int> TerrainTris = new Dictionary<(ulong, ulong, int), int>();

        [Test]
        public void RoadsDecalsAndBridgesInViewFitTheTierSliceAtAsanAndChabahil()
        {
            var report = new StringBuilder();
            var failures = new StringBuilder();
            for (int tier = 0; tier <= 2; tier++)
            {
                StreamingConfig cfg = StreamingConfig.ForTier(tier);
                TileSelector sel = TileSelector.ForPack(cfg, StreamingSampleRegion.Pack);
                float nearM, farM;
                RoadBlocks.Radii(BandConfig.ForTier(tier), out nearM, out farM);
                foreach (Place p in new[] { Asan, Chabahil })
                {
                    double x, z;
                    WorldFrame.LonLatToGame(p.Lon, p.Lat, out x, out z);
                    var nodes = new List<SelectedNode>();
                    sel.ResetHysteresis();
                    sel.Select(x, z, nodes);
                    List<TileBuild> builds = RoadsOf(nodes, cfg, tier);
                    Assert.That(builds.Count, Is.GreaterThan(0), p.Name + " has detail tiles");
                    long whole = 0;
                    foreach (TileBuild b in builds)
                        foreach (UploadChunk c in b.Chunks)
                            if (c.Layer == TileLayers.Roads && !RoadBlocks.IsFar(c.Part) || c.Layer == TileLayers.RoadDecals) whole += c.IndexCount / 3;
                    int worst = 0, worstRoads = 0, worstDecals = 0, worstBridges = 0, worstNear = 0, worstDraws = 0;
                    for (int h = 0; h < Headings; h++)
                    {
                        double heading = h * 2 * Math.PI / Headings;
                        int roads = 0, decals = 0, bridges = 0, near = 0, draws = 0;
                        foreach (TileBuild b in builds)
                        {
                            TileId area = b.Node.Area;
                            double cx = x - area.X0, cz = z - area.Z0;
                            foreach (UploadChunk c in b.Chunks)
                            {
                                if (!InView(area.X0 + c.MinX, area.Z0 + c.MinZ, area.X0 + c.MaxX, area.Z0 + c.MaxZ, x, z, heading)) continue;
                                int tris = c.IndexCount / 3;
                                switch (c.Layer)
                                {
                                    case TileLayers.Roads:
                                    {
                                        bool inNear = BlockWithin(b, c.Part, cx, cz, nearM), inFar = BlockWithin(b, c.Part, cx, cz, farM);
                                        bool on = RoadBlocks.IsFar(c.Part) ? !inNear && inFar : inNear;
                                        if (!on) break;
                                        roads += tris;
                                        draws++;
                                        if (!RoadBlocks.IsFar(c.Part)) near += tris;
                                        break;
                                    }
                                    case TileLayers.RoadDecals:
                                        if (!BlockWithin(b, c.Part, cx, cz, nearM)) break;
                                        decals += tris;
                                        draws++;
                                        break;
                                    case TileLayers.Bridges:
                                        if (PieceShown(b, c, tier, cx, cz)) bridges += tris;
                                        break;
                                }
                            }
                        }
                        int sum = roads + decals + bridges;
                        if (sum <= worst) continue;
                        worst = sum;
                        worstRoads = roads;
                        worstDecals = decals;
                        worstBridges = bridges;
                        worstNear = near;
                        worstDraws = draws;
                    }
                    report.AppendFormat("{0,-4} {1,-21} detail tiles {2}: in view {3} (roads {4} of which full kit {5}, decals {6}, bridges {7}; " +
                                        "{8} road and decal draws); full kit + decals of the whole tiles {9}\n", TierName[tier], p.Name, builds.Count, worst,
                                        worstRoads, worstNear, worstDecals, worstBridges, worstDraws, whole);
                    TestContext.WriteLine(report.ToString());
                    report.Clear();
                    if (worstRoads + worstDecals > RoadSlice[tier]) failures.Append(TierName[tier] + " " + p.Name + ": " + (worstRoads + worstDecals) + "; ");
                    Assert.That(worstNear, Is.GreaterThan(0), "the full kit is drawn round the player");
                }
            }
            Assert.That(failures.ToString(), Is.Empty, "roads + decals in view over the tier's slice");
        }

        [Test]
        public void TerrainInViewFitsTheTierSlice()
        {
            var report = new StringBuilder();
            foreach (Place p in new[] { Asan, Basantapur, Boudha, Thamel, Chabahil })
            {
                for (int tier = 0; tier <= 2; tier++)
                {
                    StreamingConfig cfg = StreamingConfig.ForTier(tier);
                    TileSelector sel = TileSelector.ForPack(cfg, StreamingSampleRegion.Pack);
                    double x, z;
                    WorldFrame.LonLatToGame(p.Lon, p.Lat, out x, out z);
                    var nodes = new List<SelectedNode>();
                    sel.ResetHysteresis();
                    sel.Select(x, z, nodes);
                    var tris = new int[nodes.Count];
                    long whole = 0;
                    for (int i = 0; i < nodes.Count; i++)
                    {
                        SelectedNode n = nodes[i];
                        TileData src = StreamingSampleRegion.Tile(n.Source);
                        int step = cfg.TerrainStep(n, src.HeightsN);
                        if (n.DrawsDetail && n.Area.Level == cfg.FinestLevel)
                            Assert.That((src.HeightsN - 1) / step, Is.EqualTo(cfg.DetailTerrainQuads), "detail tiles keep their resolution");
                        tris[i] = TerrainTriangles(src, n, step);
                        whole += tris[i];
                    }
                    int worst = 0;
                    for (int h = 0; h < Headings; h++)
                    {
                        double heading = h * 2 * Math.PI / Headings;
                        int sum = 0;
                        for (int i = 0; i < nodes.Count; i++)
                        {
                            TileId a = nodes[i].Area;
                            if (InView(a.X0, a.Z0, a.X0 + a.Size, a.Z0 + a.Size, x, z, heading)) sum += tris[i];
                        }
                        worst = Math.Max(worst, sum);
                    }
                    report.AppendFormat("{0,-4} {1,-21} nodes {2,3}: terrain in view {3} of {4} resident\n", TierName[tier], p.Name, nodes.Count, worst, whole);
                    TestContext.WriteLine(report.ToString());
                    report.Clear();
                    Assert.That(worst, Is.LessThanOrEqualTo(TerrainSlice[tier]), TierName[tier] + " " + p.Name);
                }
            }
            TestContext.WriteLine(report.ToString());
        }

        [Test]
        public void RoadBlocksSwitchWithHysteresisAtTheBandRadii()
        {
            float nearM, farM;
            for (int tier = 0; tier <= 2; tier++)
            {
                BandConfig bands = BandConfig.ForTier(tier);
                RoadBlocks.Radii(bands, out nearM, out farM);
                Assert.That(nearM, Is.EqualTo(new[] { 120f, 200f, 250f }[tier]), "full kit within the B1 radius");
                Assert.That(farM, Is.EqualTo(bands.B2OuterM), "the cheap copy out to the B2 radius");
                Assert.That(farM, Is.GreaterThan(nearM));
            }
            Assert.That(RoadBlocks.Inside(false, 120, 120f), Is.True);
            Assert.That(RoadBlocks.Inside(false, 121, 120f), Is.False);
            Assert.That(RoadBlocks.Inside(true, 121, 120f), Is.True, "a near block stays near just past the radius");
            Assert.That(RoadBlocks.Inside(true, 120 + RoadBlocks.HysteresisM + 0.5, 120f), Is.False);
            Assert.That(RoadBlocks.IsFar(RoadBlocks.FarPart + 5), Is.True);
            Assert.That(RoadBlocks.BlockOfPart(RoadBlocks.FarPart + 5), Is.EqualTo(5));
            Assert.That(RoadBlocks.BlockOfPart(5), Is.EqualTo(5));
            double x0, z0, x1, z1;
            RoadBlocks.Square(9, 1024, out x0, out z0, out x1, out z1);
            Assert.That(new[] { x0, z0, x1, z1 }, Is.EqualTo(new[] { 128.0, 128.0, 256.0, 256.0 }));
            RoadOptions far = RoadBlocks.FarOptions(new RoadOptions { LiftM = 0.3f, KerbHeightM = 0.2f });
            Assert.That(far.Detail, Is.False);
            Assert.That(far.IncludeTrails, Is.False);
            Assert.That(far.LiftM, Is.EqualTo(0.3f), "the same heights, so both copies meet");
            Assert.That(far.KerbHeightM, Is.EqualTo(0.2f));
            Assert.That(far.CrossSections, Is.False, "ribbon only: no footpaths, medians or shoulders that far");
            Assert.That(far.JunctionCaps && far.WidthModel && far.SmoothProfile, Is.True, "same carriageways, caps (no holes) and profile");
        }

        [Test]
        public void ClippedBlocksCoverEveryTriangleOnceInsideItsBlock()
        {
            // One big triangle across four blocks of a 512 m tile (128 m blocks), with colours and UV0 to interpolate.
            var m = new MeshData();
            m.AddVertex(10, 1, 10, 0, 1, 0, 0xFF0000FFu, 1f, 0.2f);
            m.AddVertex(300, 3, 40, 0, 1, 0, 0x00FF00FFu, 1f, 0.6f);
            m.AddVertex(60, 2, 290, 0, 1, 0, 0x0000FFFFu, 1f, 1f);
            m.AddTriangle(0, 2, 1); // clockwise seen from above: Unity front faces up
            // And a triangle wholly inside block 0 sharing a vertex.
            m.AddVertex(20, 1, 30, 0, 1, 0, 0xFFFFFFFFu, 1f, 1f);
            m.AddVertex(30, 1, 20, 0, 1, 0, 0xFFFFFFFFu, 1f, 1f);
            m.AddTriangle(0, 3, 4);
            var dst = new MeshData();
            var parts = new List<PartRange>();
            int added = RoadBlocks.Append(m, 512, 128, 7, dst, parts, new RoadBlocks.Scratch());
            Assert.That(added, Is.EqualTo(parts.Count));
            Assert.That(dst.HasUv0, Is.True);
            Assert.That(Area(dst, 0, dst.IndexCount), Is.EqualTo(Area(m, 0, m.IndexCount)).Within(1e-4).Percent, "the pieces cover the triangles once");
            var blocks = new HashSet<int>();
            foreach (PartRange pr in parts)
            {
                int block = pr.Part - 7;
                Assert.That(blocks.Add(block), Is.True, "one part per block");
                AssertInsideBlock(dst, pr, block, 512, 128);
                for (int i = pr.FirstIndex; i < pr.FirstIndex + pr.IndexCount; i += 3)
                    Assert.That(FaceNormalY(dst, i), Is.GreaterThan(0), "winding kept (front faces up)");
            }
            CollectionAssert.IsSubsetOf(new[] { 0, 1, 4, 5 }, blocks);
            // A vertex made on the line x = 128 between the red and green corners carries the interpolated colour and AO.
            bool found = false;
            for (int v = 0; v < dst.VertexCount; v++)
            {
                float px = dst.Positions[3 * v], pz = dst.Positions[3 * v + 2];
                if (Math.Abs(px - 128f) > 1e-4f) continue;
                double t = (128.0 - 10.0) / (300.0 - 10.0);
                double ez = 10 + (40 - 10) * t;
                if (Math.Abs(pz - ez) > 1e-3) continue;
                found = true;
                Assert.That(dst.Colors[4 * v], Is.EqualTo(255 * (1 - t)).Within(1.0));
                Assert.That(dst.Colors[4 * v + 1], Is.EqualTo(255 * t).Within(1.0));
                Assert.That(dst.Uv0[2 * v + 1], Is.EqualTo(0.2 + 0.4 * t).Within(1e-4));
                Assert.That(dst.Positions[3 * v + 1], Is.EqualTo(1 + 2 * t).Within(1e-4));
            }
            Assert.That(found, Is.True, "the red-green edge is cut on x = 128");
        }

        [Test]
        public void StreamedRoadLayersKeepTheSurfaceAndCarryACheaperFarCopy()
        {
            TileId id = TileId.At(10, Game(Asan).Item1, Game(Asan).Item2);
            for (int tier = 1; tier <= 2; tier += 1)
            {
                TileBuild b = RoadBuild(new SelectedNode(id, id), StreamingConfig.ForTier(tier), tier);
                TileData src = b.Source;
                MeshingSettings s = MeshingSettings.ForTier(tier);
                var reference = new MeshData();
                RoadMesher.Build(src, b.Sampler, s.Roads, reference);
                var farReference = new MeshData();
                RoadMesher.Build(src, b.Sampler, RoadBlocks.FarOptions(s.Roads), farReference);
                MeshData near = StreamingScene.Layer(b, TileLayers.Roads, p => !RoadBlocks.IsFar(p));
                MeshData far = StreamingScene.Layer(b, TileLayers.Roads, RoadBlocks.IsFar);
                Assert.That(Area(near, 0, near.IndexCount), Is.EqualTo(Area(reference, 0, reference.IndexCount)).Within(1e-4).Percent,
                            "the full kit's blocks cover the mesher's surface exactly once");
                Assert.That(Area(far, 0, far.IndexCount), Is.EqualTo(Area(farReference, 0, farReference.IndexCount)).Within(1e-4).Percent);
                Assert.That(far.TriangleCount, Is.LessThan(near.TriangleCount * 0.85), "the far copy is cheaper");
                Assert.That(near.TriangleCount, Is.LessThan(reference.TriangleCount * 1.12), "clipping at the block lines adds little");
                // Every chunk lies in its block; the decals too.
                foreach (UploadChunk c in b.Chunks)
                {
                    if (c.Layer != TileLayers.Roads && c.Layer != TileLayers.RoadDecals) continue;
                    double x0, z0, x1, z1;
                    RoadBlocks.Square(RoadBlocks.BlockOfPart(c.Part), id.Size, out x0, out z0, out x1, out z1);
                    // Outer blocks are open towards the outside (ribbons may end a little past the tile border).
                    if (x0 > 0) Assert.That(c.MinX, Is.GreaterThanOrEqualTo(x0 - 1e-3));
                    if (z0 > 0) Assert.That(c.MinZ, Is.GreaterThanOrEqualTo(z0 - 1e-3));
                    if (x1 < id.Size) Assert.That(c.MaxX, Is.LessThanOrEqualTo(x1 + 1e-3));
                    if (z1 < id.Size) Assert.That(c.MaxZ, Is.LessThanOrEqualTo(z1 + 1e-3));
                    Assert.That(b.ChunkBytes(b.Chunks.IndexOf(c)), Is.LessThanOrEqualTo(TileBuild.UploadChunkBytes));
                }
                TestContext.WriteLine("{0} {1}: mesher {2} tris; streamed full kit {3}, far copy {4}", TierName[tier], id, reference.TriangleCount,
                                      near.TriangleCount, far.TriangleCount);
            }
        }

        [Test]
        public void UploadChunksStayWithinTheByteCap()
        {
            TileId id = StreamingSampleRegion.DensestLeaf();
            var b = new TileBuild { Node = new SelectedNode(id, id), Source = StreamingSampleRegion.Tile(id) };
            TileBuild.Execute(b, StreamingSampleRegion.Pack, StreamingConfig.ForTier(1), MeshingSettings.ForTier(1, 10, StreamingScene.Heroes), null);
            long largest = 0;
            for (int k = 0; k < b.Chunks.Count; k++)
            {
                UploadChunk c = b.Chunks[k];
                long bytes = b.ChunkBytes(k);
                largest = Math.Max(largest, bytes);
                Assert.That(bytes, Is.LessThanOrEqualTo(TileBuild.UploadChunkBytes), TileLayers.Name(c.Layer) + " chunk " + k);
                Assert.That(c.VertexCount, Is.LessThanOrEqualTo(TileBuild.UploadChunkVertices));
                Assert.That(c.UsesShortIndices, Is.True);
            }
            Assert.That(b.Layers[TileLayers.Buildings].HasUv0, Is.True, "the W2 layers carry UV0 (36-byte vertices)");
            Assert.That(largest, Is.GreaterThan(TileBuild.UploadChunkBytes * 3 / 4), "dense layers fill their chunks");
            // The byte cap on its own: with a vertex cap that never binds, chunks still end at the byte cap, and they
            // cover the layer in order.
            var m = new MeshData();
            BuildingMesher.Build(b.Source, b.Sampler, MeshingSettings.ForTier(1).Buildings, m);
            Assert.That(m.HasUv0, Is.True);
            int indices = m.IndexCount;
            var chunks = new List<UploadChunk>();
            ushort[] shorts = null;
            TileBuild.SplitLayer(m, TileLayers.Buildings, 1 << 20, TileBuild.UploadChunkBytes, chunks, ref shorts);
            int expected = 0;
            foreach (UploadChunk c in chunks)
            {
                Assert.That(c.FirstIndex, Is.EqualTo(expected));
                Assert.That(c.Bytes(true), Is.LessThanOrEqualTo(TileBuild.UploadChunkBytes));
                Assert.That(c.VertexCount, Is.LessThan(TileBuild.UploadChunkBytes / 36), "36-byte vertices end a chunk near 29 000 vertices");
                expected += c.IndexCount;
            }
            Assert.That(expected, Is.EqualTo(indices));
            TestContext.WriteLine("{0}: {1} chunks, largest {2} bytes; the B1 layer alone in {3} chunks of at most {4} bytes", id, b.Chunks.Count,
                                  largest, chunks.Count, TileBuild.UploadChunkBytes);
        }

        /// <summary>
        /// Visual self-check (GHUMANTE_PREVIEW_DIR): the roads of the tile under a place as the streamer shows them to a
        /// camera there, per tier: <c>near.obj</c> (the full kit of the blocks within the B1 radius), <c>far.obj</c> (the
        /// cheap copy out to the B2 radius), <c>far_tinted.obj</c> (the same, tinted, to see the block switch), the
        /// markings and the terrain; plus <c>full.obj</c>, the whole tile's full kit as before the perf pass.
        /// </summary>
        [Test]
        public void DumpRoadBlocks()
        {
            if (!ObjDump.Enabled) Assert.Ignore("set " + ObjDump.EnvVar + " to dump");
            foreach (Place p in new[] { Asan, Chabahil })
            {
                double x, z;
                WorldFrame.LonLatToGame(p.Lon, p.Lat, out x, out z);
                TileId id = TileId.At(10, x, z);
                for (int tier = 0; tier <= 2; tier++)
                {
                    StreamingConfig cfg = StreamingConfig.ForTier(tier);
                    TileBuild b = RoadBuild(new SelectedNode(id, id), cfg, tier);
                    double cx = x - id.X0, cz = z - id.Z0;
                    string dir = "perf-roads/" + p.Name.Split(' ')[0].ToLowerInvariant() + "_" + TierName[tier].ToLowerInvariant() + "/";
                    var directives = new[] { "perf-roads " + p.Name + " " + TierName[tier] + ": camera at tile-local " + cx.ToString("0") + ", " + cz.ToString("0") };
                    MeshData near = StreamingScene.Roads(b, tier, cx, cz);
                    float nearM, farM;
                    RoadBlocks.Radii(BandConfig.ForTier(tier), out nearM, out farM);
                    MeshData nearOnly = StreamingScene.Layer(b, TileLayers.Roads, q => !RoadBlocks.IsFar(q) && BlockWithin(b, q, cx, cz, nearM));
                    MeshData farOnly = StreamingScene.Layer(b, TileLayers.Roads, q => RoadBlocks.IsFar(q) && !BlockWithin(b, q, cx, cz, nearM) && BlockWithin(b, q, cx, cz, farM));
                    Assert.That(near.TriangleCount, Is.EqualTo(nearOnly.TriangleCount + farOnly.TriangleCount));
                    ObjDump.Write(nearOnly, dir + "near.obj", directives);
                    ObjDump.Write(farOnly, dir + "far.obj", directives);
                    MeshData tinted = StreamingScene.Layer(b, TileLayers.Roads, q => RoadBlocks.IsFar(q) && !BlockWithin(b, q, cx, cz, nearM) && BlockWithin(b, q, cx, cz, farM));
                    for (int v = 0; v < tinted.VertexCount; v++)
                    {
                        tinted.Colors[4 * v] = (byte)Math.Min(255, tinted.Colors[4 * v] / 2 + 120);
                        tinted.Colors[4 * v + 2] = (byte)(tinted.Colors[4 * v + 2] / 2);
                    }
                    ObjDump.Write(tinted, dir + "far_tinted.obj", directives);
                    ObjDump.Write(StreamingScene.Decals(b, tier, cx, cz), dir + "decals.obj", directives);
                    ObjDump.Write(StreamingScene.Layer(b, TileLayers.Terrain), dir + "terrain.obj", directives);
                    ObjDump.Write(StreamingScene.Layer(b, TileLayers.Roads, q => !RoadBlocks.IsFar(q)), dir + "full.obj", directives);
                    ObjDump.Write(StreamingScene.Layer(b, TileLayers.RoadDecals), dir + "decals_all.obj", directives);
                    TestContext.WriteLine("{0} {1}: shown {2} tris (full kit {3}, cheap copy {4}); whole tile full kit {5}", p.Name, TierName[tier],
                                          near.TriangleCount, nearOnly.TriangleCount, farOnly.TriangleCount,
                                          StreamingScene.Layer(b, TileLayers.Roads, q => !RoadBlocks.IsFar(q)).TriangleCount);
                }
            }
        }

        /// <summary>
        /// Visual self-check (GHUMANTE_PREVIEW_DIR): the terrain of the Mid selection within 3 km of Swayambhu, every node
        /// meshed at its step and placed in one frame (metres from the focus tile's south-west corner), with the quads
        /// before the perf pass (<c>before.obj</c>: L10 64, L9 and L8 32) and now (<c>after.obj</c>), to check the
        /// hills and the seams between steps (skirts, no holes).
        /// </summary>
        [Test]
        public void DumpTerrainSteps()
        {
            if (!ObjDump.Enabled) Assert.Ignore("set " + ObjDump.EnvVar + " to dump");
            var swayambhu = new Place("Swayambhu", 85.2900, 27.7130);
            double x, z;
            WorldFrame.LonLatToGame(swayambhu.Lon, swayambhu.Lat, out x, out z);
            // The focus east of the hill, on the Bishnumati: the hill fills the view to the west.
            x += 1100;
            TileId focus = TileId.At(10, x, z);
            StreamingConfig after = StreamingConfig.ForTier(1);
            StreamingConfig before = after.Clone();
            before.DetailTerrainQuads = 0;
            before.Rings[0].TerrainQuads = 64;
            before.Rings[1].TerrainQuads = 32;
            before.Rings[2].TerrainQuads = 32;
            before.Rings[4].TerrainQuads = 16;
            foreach (var v in new[] { Tuple.Create("before", before), Tuple.Create("after", after) })
            {
                TileSelector sel = TileSelector.ForPack(v.Item2, StreamingSampleRegion.Pack);
                var nodes = new List<SelectedNode>();
                sel.Select(x, z, nodes);
                var all = new MeshData();
                int count = 0;
                foreach (SelectedNode n in nodes)
                {
                    TileId a = n.Area;
                    if (BandConfig.NearestDistance(a.X0, a.Z0, a.X0 + a.Size, a.Z0 + a.Size, x, z) > 3000) continue;
                    TileData src = StreamingSampleRegion.Tile(n.Source);
                    var m = new MeshData();
                    TerrainMesher.Build(src, a, new TerrainOptions { Step = v.Item2.TerrainStep(n, src.HeightsN) }, m);
                    ObjDump.Append(all, m, (float)(a.X0 - focus.X0), 0f, (float)(a.Z0 - focus.Z0));
                    count++;
                }
                ObjDump.Write(all, "perf-roads/terrain_swayambhu/" + v.Item1 + ".obj",
                              new[] { "perf-roads terrain " + v.Item1 + ": " + count + " nodes, focus at " + (x - focus.X0).ToString("0") + ", " + (z - focus.Z0).ToString("0") });
                TestContext.WriteLine("{0}: {1} nodes within 3 km, {2} triangles", v.Item1, count, all.TriangleCount);
            }
        }

        // ---------------------------------------------------------------------------------------------------------

        private static Tuple<double, double> Game(Place p)
        {
            double x, z;
            WorldFrame.LonLatToGame(p.Lon, p.Lat, out x, out z);
            return Tuple.Create(x, z);
        }

        /// <summary>The roads, decals and bridges of the detail nodes, built as the streamer builds them for the tier
        /// (cached per tile and tier).</summary>
        private static List<TileBuild> RoadsOf(List<SelectedNode> nodes, StreamingConfig cfg, int tier)
        {
            var detail = new List<SelectedNode>();
            foreach (SelectedNode n in nodes)
                if (n.DrawsDetail && StreamingSampleRegion.Pack.Contains(n.Source) && StreamingSampleRegion.Tile(n.Source).HasDetail) detail.Add(n);
            var builds = new TileBuild[detail.Count];
            Parallel.For(0, detail.Count, new ParallelOptions { MaxDegreeOfParallelism = 2 }, i => builds[i] = RoadBuild(detail[i], cfg, tier));
            return new List<TileBuild>(builds);
        }

        private static TileBuild RoadBuild(SelectedNode n, StreamingConfig cfg, int tier)
        {
            lock (RoadBuilds)
            {
                TileBuild cached;
                if (RoadBuilds.TryGetValue((n.Area.Key, tier), out cached)) return cached;
            }
            MeshingSettings s = MeshingSettings.ForTier(tier);
            s.DrawBuildings = false;
            s.DrawAreas = false;
            s.DrawInstances = false;
            s.DrawOrnaments = false;
            var b = new TileBuild { Node = n, Source = StreamingSampleRegion.Tile(n.Source) };
            lock (b.Source) TileBuild.Execute(b, StreamingSampleRegion.Pack, cfg, s, null);
            lock (RoadBuilds) RoadBuilds[(n.Area.Key, tier)] = b;
            return b;
        }

        private static int TerrainTriangles(TileData src, SelectedNode n, int step)
        {
            var key = (n.Area.Key, n.Source.Key, step);
            int t;
            if (TerrainTris.TryGetValue(key, out t)) return t;
            var m = new MeshData();
            TerrainMesher.Build(src, n.Area, new TerrainOptions { Step = step }, m);
            TerrainTris[key] = m.TriangleCount;
            return m.TriangleCount;
        }

        /// <summary>Whether a road or decal block lies within <paramref name="radius"/> of the camera (tile-local).</summary>
        private static bool BlockWithin(TileBuild b, int part, double cx, double cz, float radius)
        {
            double x0, z0, x1, z1;
            RoadBlocks.Square(RoadBlocks.BlockOfPart(part), b.Node.Area.Size, out x0, out z0, out x1, out z1);
            return BandConfig.NearestDistance(x0, z0, x1, z1, cx, cz) <= radius;
        }

        private static bool PieceShown(TileBuild b, UploadChunk c, int tier, double cx, double cz)
        {
            foreach (LodPiece p in b.Pieces)
            {
                if (p.Layer != c.Layer || p.Index != c.Part / 4) continue;
                return c.Part == p.Index * 4 + PieceLod.Choose(p, tier, p.DistanceTo(cx, cz), -1);
            }
            return false;
        }

        /// <summary>True when the box [x0, x1] × [z0, z1] meets the horizontal view wedge from (px, pz) along the compass
        /// <paramref name="heading"/> (half angle <see cref="HalfFovDeg"/>, no far limit): the camera inside the box, a
        /// corner inside the wedge, or one of the wedge's edge rays crossing the box.</summary>
        internal static bool InView(double x0, double z0, double x1, double z1, double px, double pz, double heading)
        {
            if (px >= x0 && px <= x1 && pz >= z0 && pz <= z1) return true;
            double hx = Math.Sin(heading), hz = Math.Cos(heading), cosHalf = Math.Cos(HalfFovDeg * Math.PI / 180.0);
            for (int k = 0; k < 4; k++)
            {
                double qx = (k & 1) == 0 ? x0 : x1, qz = (k & 2) == 0 ? z0 : z1;
                double dx = qx - px, dz = qz - pz, len = Math.Sqrt(dx * dx + dz * dz);
                if (len < 1e-9 || (dx * hx + dz * hz) / len >= cosHalf) return true;
            }
            double half = HalfFovDeg * Math.PI / 180.0;
            return RayHitsBox(px, pz, Math.Sin(heading - half), Math.Cos(heading - half), x0, z0, x1, z1) ||
                   RayHitsBox(px, pz, Math.Sin(heading + half), Math.Cos(heading + half), x0, z0, x1, z1);
        }

        private static bool RayHitsBox(double px, double pz, double dx, double dz, double x0, double z0, double x1, double z1)
        {
            double t0 = 0, t1 = double.PositiveInfinity;
            if (!Slab(px, dx, x0, x1, ref t0, ref t1)) return false;
            return Slab(pz, dz, z0, z1, ref t0, ref t1);
        }

        private static bool Slab(double p, double d, double lo, double hi, ref double t0, ref double t1)
        {
            if (Math.Abs(d) < 1e-12) return p >= lo && p <= hi;
            double a = (lo - p) / d, b = (hi - p) / d;
            if (a > b)
            {
                double t = a;
                a = b;
                b = t;
            }
            t0 = Math.Max(t0, a);
            t1 = Math.Min(t1, b);
            return t0 <= t1;
        }

        private static double Area(MeshData m, int first, int count)
        {
            double sum = 0;
            for (int i = first; i < first + count; i += 3)
            {
                int a = 3 * m.Indices[i], b = 3 * m.Indices[i + 1], c = 3 * m.Indices[i + 2];
                double ux = m.Positions[b] - m.Positions[a], uy = m.Positions[b + 1] - m.Positions[a + 1], uz = m.Positions[b + 2] - m.Positions[a + 2];
                double vx = m.Positions[c] - m.Positions[a], vy = m.Positions[c + 1] - m.Positions[a + 1], vz = m.Positions[c + 2] - m.Positions[a + 2];
                double cx = uy * vz - uz * vy, cy = uz * vx - ux * vz, cz = ux * vy - uy * vx;
                sum += 0.5 * Math.Sqrt(cx * cx + cy * cy + cz * cz);
            }
            return sum;
        }

        /// <summary>Y of the face normal of the triangle at index <paramref name="i"/> (Unity winding: cross(b - a, c - a)).</summary>
        private static double FaceNormalY(MeshData m, int i)
        {
            int a = 3 * m.Indices[i], b = 3 * m.Indices[i + 1], c = 3 * m.Indices[i + 2];
            double ux = m.Positions[b] - m.Positions[a], uz = m.Positions[b + 2] - m.Positions[a + 2];
            double vx = m.Positions[c] - m.Positions[a], vz = m.Positions[c + 2] - m.Positions[a + 2];
            return uz * vx - ux * vz;
        }

        private static void AssertInsideBlock(MeshData m, PartRange pr, int block, double size, double blockM)
        {
            int per = MeshParts.BlocksPerSide(size, blockM);
            double x0 = block % per * blockM, z0 = block / per * blockM;
            for (int i = pr.FirstIndex; i < pr.FirstIndex + pr.IndexCount; i++)
            {
                int v = m.Indices[i];
                double x = m.Positions[3 * v], z = m.Positions[3 * v + 2];
                if (block % per > 0) Assert.That(x, Is.GreaterThanOrEqualTo(x0 - 1e-4));
                if (block % per < per - 1) Assert.That(x, Is.LessThanOrEqualTo(x0 + blockM + 1e-4));
                if (block / per > 0) Assert.That(z, Is.GreaterThanOrEqualTo(z0 - 1e-4));
                if (block / per < per - 1) Assert.That(z, Is.LessThanOrEqualTo(z0 + blockM + 1e-4));
            }
        }
    }
}
