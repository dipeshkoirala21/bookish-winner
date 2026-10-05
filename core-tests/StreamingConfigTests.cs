using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
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
            StreamingConfig c = StreamingConfig.ForTier(StreamingConfig.TierMid); // L10 64 quads, L9 and L8 32, L7..L5 16
            var leaf = new TileId(10, 516, 161);
            Assert.That(c.TerrainStep(leaf, leaf, 129), Is.EqualTo(2));
            Assert.That(c.TerrainStep(leaf, leaf.Parent(), 129), Is.EqualTo(1)); // 64 source quads cover the leaf
            Assert.That(c.TerrainStep(leaf, TileArea.AncestorAt(leaf, 8), 129), Is.EqualTo(1));
            var l6 = TileArea.AncestorAt(leaf, 6);
            Assert.That(c.TerrainStep(l6, l6, 129), Is.EqualTo(8));
            var l9 = leaf.Parent();
            Assert.That(c.TerrainStep(l9, l9, 129), Is.EqualTo(4));
            Assert.That(c.TerrainStep(leaf, TileArea.AncestorAt(leaf, 2), 129), Is.EqualTo(1));
            Assert.That(c.TerrainStep(leaf, leaf, 65), Is.EqualTo(1));
            StreamingConfig high = StreamingConfig.ForTier(StreamingConfig.TierHigh);
            Assert.That(high.TerrainStep(leaf, leaf, 129), Is.EqualTo(2));
            StreamingConfig low = StreamingConfig.ForTier(StreamingConfig.TierLow);
            Assert.That(low.TerrainStep(leaf, leaf, 129), Is.EqualTo(4));
        }

        /// <summary>
        /// Worst-case selections of each tier on a pack that has every level everywhere (all of Nepal will look like
        /// this): the node count stays well inside MaxResidentTiles (room for nodes kept while their replacements
        /// load), and the terrain triangle count of a whole selection is printed for the budget review.
        /// </summary>
        [Test]
        public void MaxResidentTilesCoversTheWorstCaseSelection()
        {
            var worstNodes = new int[3];
            var trianglesPerTier = new long[3];
            for (int tier = 0; tier <= 2; tier++)
            {
                StreamingConfig cfg = StreamingConfig.ForTier(tier);
                double cx = 531000, cz = 166000, half = cfg.ViewRadiusM + 40000;
                List<TileId> tiles = StreamingSelectorTests.FullCoverage(new[] { 5, 6, 7, 8, 9, 10 }, cx - half, cz - half, cx + half, cz + half);
                var sel = new TileSelector(cfg, tiles);
                var result = new List<SelectedNode>();
                var rng = new Random(tier);
                int worst = 0;
                long worstTris = 0;
                var perLevel = new int[11];
                for (int f = 0; f < 200; f++)
                {
                    double fx = cx + rng.NextDouble() * 32768, fz = cz + rng.NextDouble() * 32768;
                    sel.Select(fx, fz, result);
                    long tris = 0;
                    foreach (SelectedNode n in result)
                    {
                        int step = cfg.TerrainStep(n.Area, n.Source, 129);
                        int quads = (128 >> n.CropDepth) / step;
                        tris += 2L * quads * quads;
                    }
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
                Assert.That(trianglesPerTier[tier], Is.LessThanOrEqualTo(new[] { 150000, 400000, 700000 }[tier]), "tier " + tier);
            }
        }
    }
}
