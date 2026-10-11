using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using Ghumante.Core.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class StreamingConfigTests
    {
        [Test]
        public void TiersFollowArchitecture72And10()
        {
            double[] near = { 750, 1250, 1750 }, mid = { 2500, 4000, 6000 }, far = { 6000, 10000, 14000 };
            double[] horizon = { 50000, 80000, 120000 };
            long[] cacheMb = { 30, 60, 90 };
            for (int tier = 0; tier <= 2; tier++)
            {
                StreamingConfig c = StreamingConfig.ForTier(tier);
                c.Validate();
                Assert.That(c.Rings.Length, Is.EqualTo(6));
                for (int k = 0; k < 6; k++) Assert.That(c.Rings[k].Level, Is.EqualTo(10 - k));
                Assert.That(c.Rings[0].RadiusM, Is.EqualTo(near[tier]));
                Assert.That(c.Rings[1].RadiusM, Is.EqualTo(mid[tier]));
                Assert.That(c.Rings[2].RadiusM, Is.EqualTo(far[tier]));
                Assert.That(c.ViewRadiusM, Is.EqualTo(horizon[tier]));
                Assert.That(c.CacheBudgetBytes, Is.EqualTo(cacheMb[tier] << 20));
                Assert.That(c.FinestLevel, Is.EqualTo(10));
                Assert.That(c.DetailRadiusM, Is.EqualTo(new[] { 350.0, 500, 900 }[tier]), "the lead a 50 km/h rider needs (StreamingRideTests)");
                Assert.That(c.DetailRadiusM, Is.LessThan(c.Rings[0].RadiusM));
                Assert.That(c.CoarsestLevel, Is.EqualTo(5));
                if (tier > 0)
                {
                    StreamingConfig lower = StreamingConfig.ForTier(tier - 1);
                    Assert.That(c.MaxResidentTiles, Is.GreaterThan(lower.MaxResidentTiles));
                    for (int k = 0; k < 6; k++) Assert.That(c.Rings[k].RadiusM, Is.GreaterThan(lower.Rings[k].RadiusM));
                }
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => StreamingConfig.ForTier(3));
            Assert.That(StreamingConfig.ForTier(1), Is.Not.SameAs(StreamingConfig.ForTier(1)));
        }

        [Test]
        public void ValidateRejectsBadRings()
        {
            var c = StreamingConfig.ForTier(0);
            c.Rings = new[] { new LodRing(9, 1000), new LodRing(10, 2000) };
            Assert.Throws<ArgumentException>(() => c.Validate());
            c.Rings = new[] { new LodRing(10, 2000), new LodRing(9, 1000) };
            Assert.Throws<ArgumentException>(() => c.Validate());
            c.Rings = new[] { new LodRing(10, -1) };
            Assert.Throws<ArgumentException>(() => c.Validate());
            c.Rings = new LodRing[0];
            Assert.Throws<ArgumentException>(() => c.Validate());
            c.Rings = new[] { new LodRing(10, 1000) };
            c.MaxResidentTiles = 0;
            Assert.Throws<ArgumentException>(() => c.Validate());
            c.MaxResidentTiles = 10;
            c.DetailRadiusM = double.NaN;
            Assert.Throws<ArgumentException>(() => c.Validate());
            c.DetailRadiusM = 100;
            c.HysteresisFraction = -0.1;
            Assert.Throws<ArgumentException>(() => c.Validate());
            c.HysteresisFraction = 0.1;
            c.Validate();
            Assert.That(c.Clone().DetailRadiusM, Is.EqualTo(100));
            Assert.That(c.Clone().HysteresisFraction, Is.EqualTo(0.1));
            Assert.That(new StreamingConfig().DetailRadiusM, Is.EqualTo(double.PositiveInfinity), "hand-made configs draw all detail");
        }

        [Test]
        public void SplitRadiusAndRingLookup()
        {
            var c = new StreamingConfig
            {
                Rings = new[] { new LodRing(10, 1000, 64), new LodRing(9, 3000, 32), new LodRing(8, 8000, 32), new LodRing(6, 40000, 16), new LodRing(5, 90000, 16) },
                MaxResidentTiles = 100,
            };
            c.Validate();
            Assert.That(c.SplitRadius(11), Is.EqualTo(0));
            Assert.That(c.SplitRadius(10), Is.EqualTo(1000));
            Assert.That(c.SplitRadius(8), Is.EqualTo(8000));
            Assert.That(c.SplitRadius(7), Is.EqualTo(8000), "an unlisted level takes the next finer ring's radius");
            Assert.That(c.SplitRadius(6), Is.EqualTo(40000));
            Assert.That(c.SplitRadius(4), Is.EqualTo(90000), "coarser than every ring: the view radius");
            Assert.That(c.RingFor(7).Level, Is.EqualTo(6));
            Assert.That(c.RingFor(12).Level, Is.EqualTo(10));
            Assert.That(c.RingFor(3).Level, Is.EqualTo(5));
        }

        [Test]
        public void TerrainStepFollowsTheRingQuads()
        {
            // Mid: L10 32 quads (detail tiles 64), L9 and L8 16, L7 16, L6 8, L5 16 (perf pass: the W2 terrain slice in view).
            StreamingConfig c = StreamingConfig.ForTier(StreamingConfig.TierMid);
            var leaf = new TileId(10, 516, 161);
            Assert.That(c.TerrainStep(leaf, leaf, 129), Is.EqualTo(4));
            Assert.That(c.TerrainStep(leaf, leaf.Parent(), 129), Is.EqualTo(2)); // 64 source quads cover the leaf
            Assert.That(c.TerrainStep(leaf, TileArea.AncestorAt(leaf, 8), 129), Is.EqualTo(1));
            var l6 = TileArea.AncestorAt(leaf, 6);
            Assert.That(c.TerrainStep(l6, l6, 129), Is.EqualTo(16), "Mid L6: 8 quads, the 2 km spacing of its L5");
            var l5 = TileArea.AncestorAt(leaf, 5);
            Assert.That(c.TerrainStep(l5, l5, 129), Is.EqualTo(8));
            var l9 = leaf.Parent();
            Assert.That(c.TerrainStep(l9, l9, 129), Is.EqualTo(8));
            Assert.That(c.TerrainStep(leaf, TileArea.AncestorAt(leaf, 2), 129), Is.EqualTo(1));
            Assert.That(c.TerrainStep(leaf, leaf, 65), Is.EqualTo(2));
            // Nodes: the detail tiles round the player keep the detail quads, the others take the ring's.
            Assert.That(c.TerrainStep(new SelectedNode(leaf, leaf, true), 129), Is.EqualTo(2), "detail tile: 64 quads");
            Assert.That(c.TerrainStep(new SelectedNode(leaf, leaf, false), 129), Is.EqualTo(4), "terrain-only tile: 32 quads");
            Assert.That(c.TerrainStep(new SelectedNode(leaf, l9, true), 129), Is.EqualTo(2), "a cropped node never draws detail");
            Assert.That(c.TerrainStep(new SelectedNode(l9, l9, true), 129), Is.EqualTo(8), "detail quads only on the finest level");
            StreamingConfig high = StreamingConfig.ForTier(StreamingConfig.TierHigh);
            Assert.That(high.TerrainStep(leaf, leaf, 129), Is.EqualTo(4));
            Assert.That(high.TerrainStep(new SelectedNode(leaf, leaf, true), 129), Is.EqualTo(2));
            Assert.That(high.TerrainStep(l9, l9, 129), Is.EqualTo(4), "High keeps 32 quads on L9");
            StreamingConfig low = StreamingConfig.ForTier(StreamingConfig.TierLow);
            Assert.That(low.TerrainStep(leaf, leaf, 129), Is.EqualTo(4));
            Assert.That(low.TerrainStep(new SelectedNode(leaf, leaf, true), 129), Is.EqualTo(4), "Low detail tiles: 32 quads");
            Assert.That(low.TerrainStep(l9, l9, 129), Is.EqualTo(8));
            var hand = new StreamingConfig { Rings = new[] { new LodRing(10, 1000, 16) }, MaxResidentTiles = 4 };
            Assert.That(hand.TerrainStep(new SelectedNode(leaf, leaf, true), 129), Is.EqualTo(8), "no detail quads: the ring's");
            Assert.That(c.Clone().DetailTerrainQuads, Is.EqualTo(64));
            hand.DetailTerrainQuads = -1;
            Assert.Throws<ArgumentException>(() => hand.Validate());
        }

        /// <summary>A pure (no hysteresis) copy of <paramref name="cfg"/> with every radius stretched by the hysteresis
        /// margin: its selection is a superset of the splits and detail nodes any hysteresis selection can hold, so
        /// its worst case bounds the tier's worst case while moving.</summary>
        internal static StreamingConfig HysteresisBound(StreamingConfig cfg)
        {
            StreamingConfig b = cfg.Clone();
            double k = 1 + cfg.HysteresisFraction;
            for (int i = 0; i + 1 < b.Rings.Length; i++) b.Rings[i].RadiusM *= k; // nothing beyond the view radius is selected
            b.DetailRadiusM *= k;
            b.HysteresisFraction = 0;
            return b;
        }

        /// <summary>
        /// Worst-case selections of each tier on a pack that has every level everywhere (all of Nepal will look like
        /// this): the node count with radii stretched by the hysteresis margin stays well inside MaxResidentTiles
        /// (room for nodes kept while their replacements load), and the terrain of a whole plain selection stays
        /// within the visible-triangle estimate.
        /// </summary>
        [Test]
        public void MaxResidentTilesCoversTheWorstCaseSelection()
        {
            var worstNodes = new int[3];
            var trianglesPerTier = new long[3];
            for (int tier = 0; tier <= 2; tier++)
            {
                StreamingConfig plain = StreamingConfig.ForTier(tier);
                StreamingConfig cfg = HysteresisBound(plain);
                double cx = 531000, cz = 166000, half = cfg.ViewRadiusM + 40000;
                List<TileId> tiles = StreamingSelectorTests.FullCoverage(new[] { 5, 6, 7, 8, 9, 10 }, cx - half, cz - half, cx + half, cz + half);
                var sel = new TileSelector(cfg, tiles);
                var plainSel = new TileSelector(plain, tiles);
                var result = new List<SelectedNode>();
                var rng = new Random(tier);
                int worst = 0;
                long worstTris = 0;
                var perLevel = new int[11];
                for (int f = 0; f < 200; f++)
                {
                    double fx = cx + rng.NextDouble() * 32768, fz = cz + rng.NextDouble() * 32768;
                    // Terrain of the plain selection (the hysteresis margin only adds a thin shell while moving).
                    plainSel.ResetHysteresis();
                    plainSel.Select(fx, fz, result);
                    long tris = 0;
                    foreach (SelectedNode n in result)
                    {
                        int step = plain.TerrainStep(n, 129);
                        int quads = (128 >> n.CropDepth) / step;
                        tris += 2L * quads * quads;
                    }
                    sel.Select(fx, fz, result);
                    if (result.Count > worst)
                    {
                        worst = result.Count;
                        Array.Clear(perLevel, 0, perLevel.Length);
                        foreach (SelectedNode n in result) perLevel[n.Area.Level]++;
                    }
                    worstTris = Math.Max(worstTris, tris);
                }
                TestContext.WriteLine("tier {0}: worst selection {1} nodes (L10..L5: {2} {3} {4} {5} {6} {7}), terrain triangles {8}",
                                      tier, worst, perLevel[10], perLevel[9], perLevel[8], perLevel[7], perLevel[6], perLevel[5], worstTris);
                worstNodes[tier] = worst;
                trianglesPerTier[tier] = worstTris;
            }
            for (int tier = 0; tier <= 2; tier++)
            {
                StreamingConfig cfg = StreamingConfig.ForTier(tier);
                Assert.That(worstNodes[tier] * 1.25, Is.LessThanOrEqualTo(cfg.MaxResidentTiles), "tier " + tier);
                // Terrain of a whole selection (roughly a third is in view) within ARCHITECTURE 10's visible
                // triangle estimate: 150 k / 400 k / 700 k.
                Assert.That(trianglesPerTier[tier], Is.LessThanOrEqualTo(VisibleTriangles[tier]), "tier " + tier);
            }
        }

        /// <summary>ARCHITECTURE 10's visible-triangle estimate per tier.</summary>
        private static readonly long[] VisibleTriangles = { 150000, 400000, 700000 };

        /// <summary>
        /// All four layers of whole selections on the real sample, meshed the way TileBuild does with the game's
        /// options (<c>MeshingSettings.ForTier</c>: terrain at the tier's step from the node's source; for nodes that draw
        /// detail the tier's road kit plus the bridge and roundabout pieces (triangles at their coarsest LOD, mesh bytes of
        /// every LOD the tile uploads), the band table's B1 layer with its
        /// front detail and the roads package's corridors, and the tier's area options), for foci over every level-10
        /// tile of the sample including the densest (Thamel) and radii stretched by the hysteresis margin. Roughly a
        /// third of a selection is in view, so the whole selection must stay within three times ARCHITECTURE 10's
        /// visible-triangle estimate, and its meshes within the tier's mesh budget (100 / 180 / 250 MB). The B1 count
        /// here is the whole tile's: the streamer shows B1 only inside its band (120 / 200 / 250 m) and B2/B3 or B0
        /// elsewhere, which MeshingBuildingGrammarTests budgets exactly.
        /// </summary>
        [Test]
        public void WholeSelectionsWithDetailFitTheTriangleAndMeshBudgets()
        {
            long[] meshBudget = { 100L << 20, 180L << 20, 250L << 20 };
            var detailCache = new Dictionary<(ulong, int, int), long[]>();
            var terrainCache = new Dictionary<(ulong, ulong, int), long[]>();
            List<TileId> leaves = StreamingSampleRegion.TilesAt(10);
            for (int tier = 0; tier <= 2; tier++)
            {
                StreamingConfig cfg = HysteresisBound(StreamingConfig.ForTier(tier));
                TileSelector sel = TileSelector.ForPack(cfg, StreamingSampleRegion.Pack);
                var result = new List<SelectedNode>();
                long worstTris = 0, worstBytes = 0;
                string worstAt = "";
                long[] worstLayers = new long[4];
                foreach (TileId leaf in leaves)
                {
                    for (int q = 0; q < 5; q++)
                    {
                        // The leaf's centre and four points near its corners.
                        double fx = leaf.X0 + leaf.Size * (q == 0 ? 0.5 : (q & 1) != 0 ? 0.03 : 0.97);
                        double fz = leaf.Z0 + leaf.Size * (q == 0 ? 0.5 : (q & 2) != 0 ? 0.03 : 0.97);
                        sel.Select(fx, fz, result);
                        var layers = new long[4];
                        long bytes = 0;
                        foreach (SelectedNode n in result)
                        {
                            TileData src = StreamingSampleRegion.Tile(n.Source);
                            int step = cfg.TerrainStep(n, src.HeightsN);
                            long[] c = NodeCost(src, n, step, tier, cfg, terrainCache, detailCache);
                            for (int k = 0; k < 4; k++) layers[k] += c[k];
                            bytes += c[4];
                        }
                        long tris = layers[0] + layers[1] + layers[2] + layers[3];
                        if (tris > worstTris)
                        {
                            worstTris = tris;
                            worstAt = leaf + (q == 0 ? " centre" : " corner " + q);
                            Array.Copy(layers, worstLayers, 4);
                        }
                        worstBytes = Math.Max(worstBytes, bytes);
                    }
                }
                TestContext.WriteLine("tier {0}: worst whole selection {1} triangles at {2} (terrain {3}, roads {4}, buildings {5}, areas {6}), meshes {7:0.0} MB",
                                      tier, worstTris, worstAt, worstLayers[0], worstLayers[1], worstLayers[2], worstLayers[3], worstBytes / 1048576.0);
                // Low: four dense Thamel tiles meeting at the focus exceed the plain estimate because every one of their
                // houses is counted as B1 here (the bands draw B1 only within 120 m on Low); the rebased bound (the
                // detail pass's styled B1 fronts and widened roads, measured 2026-10) keeps it from growing back.
                double allowance = 3 * VisibleTriangles[tier] * AllowanceFactor[tier];
                Assert.That(worstTris, Is.LessThanOrEqualTo(allowance), "tier " + tier);
                Assert.That(worstBytes, Is.LessThanOrEqualTo(meshBudget[tier]), "tier " + tier);
            }
        }

        /// <summary>Factor on three times the visible estimate per tier for a whole selection (see the test). Rebased by the
        /// integration of the detail pass (styled B1 fronts, widened roads, bridges at LOD2): worst cases measured at 824 k
        /// (Low, four Thamel/Asan tiles at a corner), 1.21 M (Mid) and 1.83 M (High) triangles.</summary>
        private static readonly double[] AllowanceFactor = { 1.9, 1.05, 1.05 };

        /// <summary>Terrain, roads, buildings and areas triangles of a node, then its mesh bytes (TileBuild.LayerBytes:
        /// position, normal, colour, optional uv0, 16- or 32-bit indices), meshed with the tier's game options.</summary>
        private static long[] NodeCost(TileData src, SelectedNode n, int step, int tier, StreamingConfig cfg,
                                       Dictionary<(ulong, ulong, int), long[]> terrainCache, Dictionary<(ulong, int, int), long[]> detailCache)
        {
            World.Streaming.MeshingSettings game = World.Streaming.MeshingSettings.ForTier(tier);
            var cost = new long[5];
            long[] tc;
            if (!terrainCache.TryGetValue((n.Area.Key, n.Source.Key, step), out tc))
            {
                var terrain = new MeshData();
                TerrainMesher.Build(src, n.Area, new TerrainOptions { Step = step }, terrain);
                tc = new long[] { terrain.TriangleCount, Bytes(terrain) };
                terrainCache[(n.Area.Key, n.Source.Key, step)] = tc;
            }
            cost[0] = tc[0];
            cost[4] = tc[1];
            if (n.DrawsDetail && src.HasDetail)
            {
                long[] d;
                if (!detailCache.TryGetValue((n.Area.Key, step, tier), out d))
                {
                    d = new long[4];
                    TileHeightSampler h = TileHeightSampler.ForArea(src, n.Area, step);
                    // Roads, bridges and ornaments as the streamer builds them (TileBuild, the tier's settings): the
                    // triangles of the ribbons' full kit (the cheap far copy stands in for it beyond the B1 radius,
                    // RoadBlocks) plus every bridge and ornament piece at its coarsest built LOD (most pieces are far),
                    // the bytes of everything the tile uploads (both road copies, every LOD).
                    World.Streaming.MeshingSettings roadsOnly = World.Streaming.MeshingSettings.ForTier(tier);
                    roadsOnly.DrawBuildings = false;
                    roadsOnly.DrawAreas = false;
                    roadsOnly.DrawInstances = false;
                    roadsOnly.DrawRoadDecals = false;
                    var tb = new World.Streaming.TileBuild { Node = n, Source = src };
                    World.Streaming.TileBuild.Execute(tb, StreamingSampleRegion.Pack, cfg, roadsOnly, null);
                    d[0] = FullKitRoadTris(tb) + CoarsestPieceTris(tb, World.Streaming.TileLayers.Bridges)
                           + CoarsestPieceTris(tb, World.Streaming.TileLayers.Ornaments);
                    d[3] += tb.LayerBytes(World.Streaming.TileLayers.Roads) + tb.LayerBytes(World.Streaming.TileLayers.Bridges)
                            + tb.LayerBytes(World.Streaming.TileLayers.Ornaments);
                    var m = new MeshData();
                    BuildingMesher.Build(src, h, game.Buildings, m);
                    d[1] = m.TriangleCount;
                    d[3] += Bytes(m);
                    m = new MeshData();
                    AreaMesher.Build(src, h, game.Areas, m);
                    d[2] = m.TriangleCount;
                    d[3] += Bytes(m);
                    detailCache[(n.Area.Key, step, tier)] = d;
                }
                cost[1] = d[0];
                cost[2] = d[1];
                cost[3] = d[2];
                cost[4] += d[3];
            }
            return cost;
        }

        /// <summary>Triangles of the road layer's full kit (parts below <see cref="World.Streaming.RoadBlocks.FarPart"/>).</summary>
        private static long FullKitRoadTris(World.Streaming.TileBuild b)
        {
            long tris = 0;
            foreach (World.Streaming.UploadChunk c in b.Chunks)
                if (c.Layer == World.Streaming.TileLayers.Roads && !World.Streaming.RoadBlocks.IsFar(c.Part)) tris += c.IndexCount / 3;
            return tris;
        }

        /// <summary>Triangles of a layer's pieces (<see cref="World.Streaming.LodPiece"/>), each at its coarsest built LOD.</summary>
        private static long CoarsestPieceTris(World.Streaming.TileBuild b, int layer)
        {
            var coarsest = new Dictionary<int, int>();
            foreach (World.Streaming.LodPiece p in b.Pieces)
            {
                if (p.Layer != layer) continue;
                for (int l = 3; l >= 0; l--)
                {
                    if ((p.LodMask & (1 << l)) == 0) continue;
                    coarsest[p.Index * 4 + l] = 1;
                    break;
                }
            }
            long tris = 0;
            foreach (World.Streaming.UploadChunk c in b.Chunks)
                if (c.Layer == layer && coarsest.ContainsKey(c.Part)) tris += c.IndexCount / 3;
            return tris;
        }

        private static long Bytes(MeshData m)
        {
            long v = (long)m.VertexCount * (12 + 12 + 4 + (m.HasUv0 ? 8 : 0));
            return v + (long)m.IndexCount * (m.VertexCount <= 65535 ? 2 : 4);
        }
    }
}
