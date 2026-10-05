using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class StreamingSelectorTests
    {
        // Kathmandu in game metres (the sample region's centre).
        private const double KtmX = 531000, KtmZ = 166000;

        /// <summary>Every tile of the given levels intersecting the box [x0, x1) × [z0, z1).</summary>
        internal static List<TileId> FullCoverage(int[] levels, double x0, double z0, double x1, double z1)
        {
            var list = new List<TileId>();
            foreach (int l in levels)
            {
                double s = TileId.SizeAt(l);
                int tx0 = (int)Math.Floor(x0 / s), tx1 = (int)Math.Floor((x1 - 1e-6) / s);
                int ty0 = (int)Math.Floor(z0 / s), ty1 = (int)Math.Floor((z1 - 1e-6) / s);
                for (int ty = ty0; ty <= ty1; ty++)
                    for (int tx = tx0; tx <= tx1; tx++) list.Add(new TileId(l, tx, ty));
            }
            return list;
        }

        /// <summary>
        /// Checks every contract property of a selection: sources exist and are the finest existing
        /// ancestor-or-self; no two areas overlap; the split rule holds for each area and its parent; every random
        /// point of the view disc that has data is covered by exactly one area, and points without data by none.
        /// </summary>
        internal static void AssertValidSelection(List<SelectedNode> sel, HashSet<ulong> exists, StreamingConfig cfg,
                                                  double fx, double fz, Random rng, int points, string what)
        {
            int finest = cfg.FinestLevel;
            double view = cfg.ViewRadiusM, keep = 1 + cfg.HysteresisFraction + 1e-12;
            Func<TileId, bool> has = t => t.Level <= finest && exists.Contains(t.Key);
            int coarsest = int.MaxValue;
            foreach (ulong k in exists)
            {
                TileId t = TileId.FromKey(k);
                if (t.Level <= finest) coarsest = Math.Min(coarsest, t.Level);
            }
            // Every strict ancestor of an existing tile (within the finest ring) has data below it.
            var ancestors = new HashSet<ulong>();
            foreach (ulong k in exists)
            {
                TileId t = TileId.FromKey(k);
                if (t.Level > finest) continue;
                for (int l = t.Level - 1; l >= 0; l--)
                    if (!ancestors.Add(TileArea.AncestorAt(t, l).Key)) break;
            }
            Func<TileId, bool> hasDataBelow = a => ancestors.Contains(a.Key);

            double prevD = -1;
            ulong prevKey = 0;
            foreach (SelectedNode n in sel)
            {
                Assert.That(has(n.Source), Is.True, what + ": source " + n.Source + " does not exist");
                Assert.That(TileArea.Contains(n.Source, n.Area), Is.True, what + ": " + n);
                for (int l = n.Source.Level + 1; l <= n.Area.Level; l++)
                    Assert.That(has(TileArea.AncestorAt(n.Area, l)), Is.False, what + ": a finer source exists for " + n);
                double d = TileArea.ClosestDistance(n.Area, fx, fz);
                Assert.That(d, Is.LessThanOrEqualTo(view), what + ": " + n + " beyond the view radius");
                Assert.That(d > prevD || d == prevD && n.Area.Key > prevKey, Is.True, what + ": order at " + n);
                prevD = d;
                prevKey = n.Area.Key;

                // Split rule: a node finer data could refine stays unsplit only beyond the next ring; a split survives
                // (hysteresis) out to the ring times 1 + HysteresisFraction.
                if (n.Area.Level < finest && hasDataBelow(n.Area))
                    Assert.That(d, Is.GreaterThan(cfg.SplitRadius(n.Area.Level + 1)), what + ": " + n + " should have split");
                // Detail: always inside the detail radius, never beyond it plus the hysteresis margin, exact nodes only.
                if (n.IsExact && d <= cfg.DetailRadiusM) Assert.That(n.DrawsDetail, Is.True, what + ": " + n + " should draw detail");
                if (n.DrawsDetail)
                {
                    Assert.That(n.IsExact, Is.True, what + ": " + n);
                    Assert.That(d, Is.LessThanOrEqualTo(cfg.DetailRadiusM * keep), what + ": " + n + " should not draw detail");
                }
                if (n.Area.Level > coarsest)
                {
                    TileId parent = n.Area.Parent();
                    bool parentHasSource = false;
                    for (int l = coarsest; l <= parent.Level; l++) parentHasSource |= has(TileArea.AncestorAt(parent, l));
                    if (parentHasSource)
                        Assert.That(TileArea.ClosestDistance(parent, fx, fz), Is.LessThanOrEqualTo(cfg.SplitRadius(n.Area.Level) * keep),
                                    what + ": parent of " + n + " should not have split");
                }
            }

            for (int i = 0; i < sel.Count; i++)
                for (int j = i + 1; j < sel.Count; j++)
                    Assert.That(TileArea.Overlaps(sel[i].Area, sel[j].Area), Is.False, what + ": " + sel[i] + " overlaps " + sel[j]);

            for (int k = 0; k < points; k++)
            {
                double r = view * Math.Sqrt(rng.NextDouble()), a = rng.NextDouble() * 2 * Math.PI;
                double px = fx + r * Math.Cos(a), pz = fz + r * Math.Sin(a);
                if (px < 0 || pz < 0 || px >= TileId.RootSizeM || pz >= TileId.RootSizeM) continue;
                bool data = false;
                for (int l = 0; l <= finest && !data; l++) data = has(TileId.At(l, px, pz));
                int covered = 0;
                foreach (SelectedNode n in sel)
                    if (n.Area.Contains(px, pz)) covered++;
                Assert.That(covered, Is.EqualTo(data ? 1 : 0), what + ": point (" + px + ", " + pz + ")");
            }
        }

        private static HashSet<ulong> KeySet(IEnumerable<TileId> tiles)
        {
            var s = new HashSet<ulong>();
            foreach (TileId t in tiles) s.Add(t.Key);
            return s;
        }

        [Test]
        public void FullCoverageSelectionIsExactAndFinerNearTheFocus()
        {
            for (int tier = 0; tier <= 2; tier++)
            {
                StreamingConfig cfg = StreamingConfig.ForTier(tier);
                double half = cfg.ViewRadiusM + 40000;
                List<TileId> tiles = FullCoverage(new[] { 5, 6, 7, 8, 9, 10 }, KtmX - half, KtmZ - half, KtmX + half, KtmZ + half);
                HashSet<ulong> keys = KeySet(tiles);
                var sel = new TileSelector(cfg, tiles);
                var rng = new Random(1234 + tier);
                var result = new List<SelectedNode>();
                for (int f = 0; f < 6; f++)
                {
                    double fx = KtmX + (rng.NextDouble() - 0.5) * 20000, fz = KtmZ + (rng.NextDouble() - 0.5) * 20000;
                    sel.Select(fx, fz, result);
                    AssertValidSelection(result, keys, cfg, fx, fz, rng, 1500, "tier " + tier);

                    // Full coverage: every node is exact, the focus sits in a finest-level node.
                    foreach (SelectedNode n in result) Assert.That(n.IsExact, Is.True);
                    Assert.That(result[0].Area.Contains(fx, fz), Is.True);
                    Assert.That(result[0].Area.Level, Is.EqualTo(cfg.FinestLevel));

                    // Ring behaviour at random points: at least as fine as the ring for the distance, and never finer
                    // than the closest-point rule allows.
                    for (int k = 0; k < 500; k++)
                    {
                        double r = cfg.ViewRadiusM * Math.Sqrt(rng.NextDouble()), a = rng.NextDouble() * 2 * Math.PI;
                        double px = fx + r * Math.Cos(a), pz = fz + r * Math.Sin(a);
                        SelectedNode hit = result.Find(n => n.Area.Contains(px, pz));
                        int l = hit.Area.Level;
                        if (l < cfg.FinestLevel) Assert.That(cfg.SplitRadius(l + 1), Is.LessThan(r));
                        if (l > cfg.CoarsestLevel)
                            Assert.That(r, Is.LessThanOrEqualTo(cfg.SplitRadius(l) * (1 + cfg.HysteresisFraction) + Math.Sqrt(2) * TileId.SizeAt(l - 1) + 1e-6));
                    }
                }
            }
        }

        /// <summary>
        /// Edge-adjacent areas differ by at most one level on Mid and High. Low's ARCHITECTURE 7.2 radii (0.75, 2.5,
        /// 6 km) are too close together to guarantee it (a strict one-level balance needs
        /// R(L) &gt;= R(L+1) + sqrt(2)·S(L)), so two levels can meet there; terrain skirts hide the cracks.
        /// </summary>
        [Test]
        public void NeighbourLevelDifferenceIsBounded()
        {
            var worst = new int[3];
            for (int tier = 0; tier <= 2; tier++)
            {
                StreamingConfig cfg = StreamingConfig.ForTier(tier);
                double half = cfg.ViewRadiusM + 40000;
                List<TileId> tiles = FullCoverage(new[] { 5, 6, 7, 8, 9, 10 }, KtmX - half, KtmZ - half, KtmX + half, KtmZ + half);
                var sel = new TileSelector(cfg, tiles);
                var result = new List<SelectedNode>();
                var rng = new Random(77);
                for (int f = 0; f < 40; f++)
                {
                    sel.Select(KtmX + rng.NextDouble() * 20000, KtmZ + rng.NextDouble() * 20000, result);
                    for (int i = 0; i < result.Count; i++)
                    {
                        TileId a = result[i].Area;
                        for (int j = i + 1; j < result.Count; j++)
                        {
                            TileId b = result[j].Area;
                            double ax0 = a.X0, ax1 = a.X0 + a.Size, az0 = a.Z0, az1 = a.Z0 + a.Size;
                            double bx0 = b.X0, bx1 = b.X0 + b.Size, bz0 = b.Z0, bz1 = b.Z0 + b.Size;
                            bool shareX = (ax1 == bx0 || bx1 == ax0) && Math.Min(az1, bz1) > Math.Max(az0, bz0);
                            bool shareZ = (az1 == bz0 || bz1 == az0) && Math.Min(ax1, bx1) > Math.Max(ax0, bx0);
                            if (shareX || shareZ) worst[tier] = Math.Max(worst[tier], Math.Abs(a.Level - b.Level));
                        }
                    }
                }
            }
            TestContext.WriteLine("largest level step between edge neighbours: Low {0}, Mid {1}, High {2}", worst[0], worst[1], worst[2]);
            Assert.That(worst[2], Is.LessThanOrEqualTo(1));
            Assert.That(worst[1], Is.LessThanOrEqualTo(1));
            Assert.That(worst[0], Is.LessThanOrEqualTo(2));
        }

        /// <summary>
        /// A focus that wanders back and forth across a ring edge (or the detail radius) must not flip a parent and
        /// its four children, or a tile's detail, on every reselection: with the default hysteresis the selection
        /// settles after the first crossing and only changes again once the focus is beyond the margin. Without
        /// hysteresis it flips every time (the old behaviour).
        /// </summary>
        [Test]
        public void HysteresisStopsRingEdgeFlipping()
        {
            StreamingConfig cfg = StreamingConfig.ForTier(StreamingConfig.TierLow); // L10 ring 750 m, detail 100 m
            Assert.That(cfg.HysteresisFraction, Is.EqualTo(StreamingConfig.DefaultHysteresisFraction));
            double half = cfg.ViewRadiusM + 40000;
            List<TileId> tiles = FullCoverage(new[] { 5, 6, 7, 8, 9, 10 }, KtmX - half, KtmZ - half, KtmX + half, KtmZ + half);
            HashSet<ulong> keys = KeySet(tiles);
            TileId parent = TileId.At(9, KtmX, KtmZ);
            TileId leaf = TileId.At(10, KtmX, KtmZ);
            double z = parent.Z0 + parent.Size * 0.5;
            double ringEdge = parent.X0 + parent.Size + 750; // the parent splits within 750 m of its east edge
            double detailEdge = leaf.X0 - 100;               // the leaf draws detail within 100 m of its west edge

            foreach (double edge in new[] { ringEdge, detailEdge })
            {
                double zz = edge == ringEdge ? z : leaf.Z0 + leaf.Size * 0.5;
                double inward = edge == ringEdge ? -1 : 1; // the side of the edge where the split / detail applies
                var with = new TileSelector(cfg, tiles);
                StreamingConfig pureCfg = cfg.Clone();
                pureCfg.HysteresisFraction = 0;
                var pure = new TileSelector(pureCfg, tiles);
                var a = new List<SelectedNode>();
                var b = new List<SelectedNode>();
                var settled = new List<SelectedNode>();

                // Out, in (split / detail on), then back and forth by 5 m either side of the edge.
                with.Select(edge - 5 * inward, zz, a);
                with.Select(edge + 5 * inward, zz, a);
                AssertValidSelection(a, keys, cfg, edge + 5 * inward, zz, new Random(3), 500, "inside");
                settled.AddRange(a);
                for (int k = 0; k < 6; k++)
                {
                    double fx = edge + (k % 2 == 0 ? 5 : -5);
                    with.Select(fx, zz, a);
                    AssertValidSelection(a, keys, cfg, fx, zz, new Random(k), 300, "oscillation " + k);
                    a.Sort();
                    var sortedSettled = new List<SelectedNode>(settled);
                    sortedSettled.Sort();
                    Assert.That(a, Is.EqualTo(sortedSettled), "selection flipped at oscillation " + k);
                }

                pure.Select(edge - 5, zz, a);
                pure.Select(edge + 5, zz, b);
                a.Sort();
                b.Sort();
                Assert.That(b, Is.Not.EqualTo(a), "without hysteresis the edge flips the selection");

                // Beyond the margin the merge / detail drop happens.
                double far = edge == ringEdge ? 750 * 0.1 + 10 : 100 * 0.1 + 10;
                with.Select(edge - far * inward, zz, a);
                pure.Select(edge - far * inward, zz, b);
                a.Sort();
                b.Sort();
                Assert.That(a, Is.EqualTo(b), "beyond the margin the hysteresis selection matches the plain one");
            }

            // Detail is kept inside the margin, and ResetHysteresis forgets it.
            var sel = new TileSelector(cfg, tiles);
            var r = new List<SelectedNode>();
            sel.Select(detailEdge + 5, leaf.Z0 + leaf.Size * 0.5, r);
            Assert.That(r.Find(n => n.Area == leaf).DrawsDetail, Is.True);
            sel.Select(detailEdge - 5, leaf.Z0 + leaf.Size * 0.5, r);
            Assert.That(r.Find(n => n.Area == leaf).DrawsDetail, Is.True, "detail kept inside the margin");
            sel.ResetHysteresis();
            sel.Select(detailEdge - 5, leaf.Z0 + leaf.Size * 0.5, r);
            Assert.That(r.Find(n => n.Area == leaf).DrawsDetail, Is.False, "ResetHysteresis forgets the previous selection");
        }

        [Test]
        public void MissingLevelIsSkippedAndCroppedFromTheCoarserSource()
        {
            StreamingConfig cfg = StreamingConfig.ForTier(StreamingConfig.TierHigh);
            double half = cfg.ViewRadiusM + 40000;
            List<TileId> tiles = FullCoverage(new[] { 5, 6, 8, 9, 10 }, KtmX - half, KtmZ - half, KtmX + half, KtmZ + half);
            HashSet<ulong> keys = KeySet(tiles);
            var sel = new TileSelector(cfg, tiles);
            var result = new List<SelectedNode>();
            sel.Select(KtmX, KtmZ, result);
            AssertValidSelection(result, keys, cfg, KtmX, KtmZ, new Random(7), 3000, "no L7");

            int l7Areas = 0, l8Exact = 0;
            foreach (SelectedNode n in result)
            {
                Assert.That(n.Source.Level, Is.Not.EqualTo(7));
                if (n.Area.Level == 7)
                {
                    l7Areas++;
                    Assert.That(n.Source.Level, Is.EqualTo(6));
                    Assert.That(n.IsExact, Is.False);
                }
                if (n.Area.Level == 8 && n.IsExact) l8Exact++;
            }
            Assert.That(l7Areas, Is.GreaterThan(0), "the L7 ring is drawn from cropped L6 sources");
            Assert.That(l8Exact, Is.GreaterThan(0));
        }

        [Test]
        public void SparseRandomPacksAreCoveredExactlyOnce()
        {
            var rng = new Random(99);
            StreamingConfig cfg = StreamingConfig.ForTier(StreamingConfig.TierLow);
            for (int trial = 0; trial < 12; trial++)
            {
                // Random tiles at random levels around Kathmandu, with holes, overlaps between levels and gaps.
                var tiles = new List<TileId>();
                int count = 30 + rng.Next(120);
                for (int k = 0; k < count; k++)
                {
                    int l = 4 + rng.Next(8); // 4..11: coarser than the coarsest ring and finer than the finest too
                    double s = TileId.SizeAt(l);
                    double spread = Math.Min(80000, 40 * s);
                    double x = KtmX + (rng.NextDouble() - 0.5) * spread, z = KtmZ + (rng.NextDouble() - 0.5) * spread;
                    tiles.Add(TileId.At(l, x, z));
                }
                HashSet<ulong> keys = KeySet(tiles);
                var sel = new TileSelector(cfg, tiles);
                var result = new List<SelectedNode>();
                for (int f = 0; f < 3; f++)
                {
                    double fx = KtmX + (rng.NextDouble() - 0.5) * 30000, fz = KtmZ + (rng.NextDouble() - 0.5) * 30000;
                    sel.Select(fx, fz, result);
                    AssertValidSelection(result, keys, cfg, fx, fz, rng, 800, "trial " + trial);
                }
            }
        }

        [Test]
        public void SampleRegionSelectionCoversItsDataExactlyOnce()
        {
            List<TileId> tiles = StreamingSampleRegion.Tiles;
            HashSet<ulong> keys = KeySet(tiles);
            var rng = new Random(2026);
            var result = new List<SelectedNode>();
            for (int tier = 0; tier <= 2; tier++)
            {
                StreamingConfig cfg = StreamingConfig.ForTier(tier);
                TileSelector sel = TileSelector.ForPack(cfg, StreamingSampleRegion.Pack);
                for (int f = 0; f < 12; f++)
                {
                    // Focus points in and around the detail region, some well outside it.
                    double fx = 520000 + rng.NextDouble() * 22000, fz = 158000 + rng.NextDouble() * 16000;
                    if (f % 4 == 3)
                    {
                        fx += (rng.NextDouble() - 0.5) * 120000;
                        fz += (rng.NextDouble() - 0.5) * 120000;
                    }
                    sel.Select(fx, fz, result);
                    Assert.That(result.Count, Is.GreaterThan(0));
                    AssertValidSelection(result, keys, cfg, fx, fz, rng, 1500, "sample tier " + tier);
                    foreach (SelectedNode n in result)
                    {
                        Assert.That(n.Source.Level, Is.Not.EqualTo(7), "the sample has no level 7");
                        Assert.That(n.Area.Level, Is.LessThanOrEqualTo(10));
                    }
                }
            }
        }

        [Test]
        public void SampleRegionIsFinestAtTheFocus()
        {
            StreamingConfig cfg = StreamingConfig.ForTier(StreamingConfig.TierMid);
            TileSelector sel = TileSelector.ForPack(cfg, StreamingSampleRegion.Pack);
            var result = new List<SelectedNode>();
            var rng = new Random(5);
            foreach (TileId leaf in StreamingSampleRegion.TilesAt(10))
            {
                double fx = leaf.X0 + rng.NextDouble() * leaf.Size, fz = leaf.Z0 + rng.NextDouble() * leaf.Size;
                sel.Select(fx, fz, result);
                SelectedNode first = result[0];
                Assert.That(first.Area, Is.EqualTo(leaf), "the focus tile is drawn first");
                Assert.That(first.IsExact, Is.True);
                // Detail falls off with distance: the mean level of the nearest nodes beats that of the farthest.
                Assert.That(result[result.Count - 1].Area.Level, Is.LessThan(10));
            }
        }

        [Test]
        public void FuncAndSetIndexesAgree()
        {
            List<TileId> tiles = StreamingSampleRegion.Tiles;
            HashSet<ulong> keys = KeySet(tiles);
            var rng = new Random(11);
            for (int tier = 0; tier <= 2; tier++)
            {
                StreamingConfig cfg = StreamingConfig.ForTier(tier);
                var a = new TileSelector(cfg, tiles);
                var b = new TileSelector(cfg, t => keys.Contains(t.Key));
                var ra = new List<SelectedNode>();
                var rb = new List<SelectedNode>();
                for (int f = 0; f < 10; f++)
                {
                    double fx = 500000 + rng.NextDouble() * 60000, fz = 140000 + rng.NextDouble() * 50000;
                    a.Select(fx, fz, ra);
                    b.Select(fx, fz, rb);
                    Assert.That(rb, Is.EqualTo(ra));
                }
            }
        }

        [Test]
        public void SelectionIsDeterministicAndAllocationFreeAfterWarmUp()
        {
            StreamingConfig cfg = StreamingConfig.ForTier(StreamingConfig.TierHigh);
            double half = cfg.ViewRadiusM + 20000;
            List<TileId> tiles = FullCoverage(new[] { 5, 6, 7, 8, 9, 10 }, KtmX - half, KtmZ - half, KtmX + half, KtmZ + half);
            var s1 = new TileSelector(cfg, tiles);
            tiles.Reverse();
            var s2 = new TileSelector(cfg, tiles);
            var r1 = new List<SelectedNode>(1024);
            var r2 = new List<SelectedNode>(1024);
            double[] fx = { KtmX, KtmX + 731.5, KtmX - 4021.25, KtmX + 12000 };
            double[] fz = { KtmZ, KtmZ - 250.75, KtmZ + 3333.0, KtmZ - 9000 };
            for (int i = 0; i < fx.Length; i++)
            {
                s1.Select(fx[i], fz[i], r1);
                s2.Select(fx[i], fz[i], r2);
                Assert.That(r2, Is.EqualTo(r1));
            }

            // Warm up, then measure.
            for (int i = 0; i < fx.Length; i++) s1.Select(fx[i], fz[i], r1);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int rep = 0; rep < 10; rep++)
                for (int i = 0; i < fx.Length; i++) s1.Select(fx[i], fz[i], r1);
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.That(after - before, Is.EqualTo(0), "Select allocated after warm-up");
        }

        [Test]
        public void FarFocusAndInvalidInputSelectNothing()
        {
            StreamingConfig cfg = StreamingConfig.ForTier(StreamingConfig.TierLow);
            TileSelector sel = TileSelector.ForPack(cfg, StreamingSampleRegion.Pack);
            var result = new List<SelectedNode> { new SelectedNode(new TileId(5, 1, 1), new TileId(5, 1, 1)) };
            sel.Select(10000, 10000, result); // far west of the sample's horizon tiles
            Assert.That(result, Is.Empty);
            sel.Select(double.NaN, KtmZ, result);
            Assert.That(result, Is.Empty);
            Assert.Throws<ArgumentNullException>(() => sel.Select(KtmX, KtmZ, null));
            Assert.Throws<ArgumentNullException>(() => new TileSelector(cfg, (Func<TileId, bool>)null));
        }

        [Test]
        public void SelectedNodeRejectsASourceOutsideTheArea()
        {
            var area = new TileId(10, 516, 161);
            Assert.Throws<ArgumentException>(() => new SelectedNode(area, new TileId(9, 200, 80)));
            Assert.Throws<ArgumentException>(() => new SelectedNode(new TileId(9, 258, 80), area));
            var n = new SelectedNode(area, new TileId(8, 129, 40));
            Assert.That(n.CropDepth, Is.EqualTo(2));
            Assert.That(n.IsExact, Is.False);
            Assert.That(n, Is.EqualTo(new SelectedNode(area, new TileId(8, 129, 40))));
            Assert.That(n.GetHashCode(), Is.EqualTo(new SelectedNode(area, new TileId(8, 129, 40)).GetHashCode()));
            Assert.That(n, Is.Not.EqualTo(new SelectedNode(area, area)));
            // DrawsDetail is part of the identity and only ever set on exact nodes.
            Assert.That(new SelectedNode(area, area).DrawsDetail, Is.True);
            Assert.That(new SelectedNode(area, area, false), Is.Not.EqualTo(new SelectedNode(area, area)));
            Assert.That(new SelectedNode(area, new TileId(8, 129, 40), true).DrawsDetail, Is.False);
            Assert.That(new SelectedNode(area, area, false).CompareTo(new SelectedNode(area, area)), Is.LessThan(0));
        }

        [Test]
        public void TileAreaHelpers()
        {
            var t = new TileId(10, 516, 161);
            Assert.That(TileArea.Contains(new TileId(8, 129, 40), t), Is.True);
            Assert.That(TileArea.Contains(t, new TileId(8, 129, 40)), Is.False);
            Assert.That(TileArea.Overlaps(t, new TileId(8, 129, 40)), Is.True);
            Assert.That(TileArea.Overlaps(t, new TileId(10, 517, 161)), Is.False);
            Assert.That(TileArea.AncestorAt(t, 8), Is.EqualTo(new TileId(8, 129, 40)));
            Assert.That(TileArea.ClosestDistance(t, t.X0 + 10, t.Z0 + 10), Is.EqualTo(0));
            Assert.That(TileArea.ClosestDistance(t, t.X0 - 3, t.Z0 - 4), Is.EqualTo(5).Within(1e-9));
            TileId[] kids = t.Children();
            for (int q = 0; q < 4; q++) Assert.That(TileArea.Child(t, q), Is.EqualTo(kids[q]));
        }
    }
}
