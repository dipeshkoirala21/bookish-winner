using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class StreamingCacheTests
    {
        [Test]
        public void LruEvictsLeastRecentlyUsedToTheBudget()
        {
            var evicted = new List<string>();
            var c = new LruByteCache<string, int>(100, 2) { Evicted = (k, v) => evicted.Add(k + "=" + v) };
            c.Put("a", 1, 40);
            c.Put("b", 2, 40);
            Assert.That(c.Bytes, Is.EqualTo(80));
            Assert.That(c.Get("a"), Is.EqualTo(1)); // a is now most recent
            c.Put("c", 3, 40);                      // 120 > 100: evict b
            Assert.That(evicted, Is.EqualTo(new[] { "b=2" }));
            Assert.That(c.Contains("b"), Is.False);
            Assert.That(c.KeysByRecency(), Is.EqualTo(new[] { "c", "a" }));
            Assert.That(c.Count, Is.EqualTo(2));
            Assert.That(c.Bytes, Is.EqualTo(80));

            // Contains does not touch recency.
            Assert.That(c.Contains("a"), Is.True);
            c.Put("d", 4, 30); // 110: evict a (least recent)
            Assert.That(evicted[1], Is.EqualTo("a=1"));
            Assert.That(c.KeysByRecency(), Is.EqualTo(new[] { "d", "c" }));

            int v;
            Assert.That(c.TryGet("zz", out v), Is.False);
            Assert.Throws<KeyNotFoundException>(() => c.Get("zz"));
        }

        [Test]
        public void LruReplaceRemoveTrimAndClear()
        {
            var evicted = new List<string>();
            var c = new LruByteCache<int, string>(1000) { Evicted = (k, v) => evicted.Add(v) };
            c.Put(1, "one", 100);
            c.Put(2, "two", 100);
            c.Put(1, "uno", 300); // replacement: old value reported, size updated, moved to front
            Assert.That(evicted, Is.EqualTo(new[] { "one" }));
            Assert.That(c.Bytes, Is.EqualTo(400));
            Assert.That(c.KeysByRecency(), Is.EqualTo(new[] { 1, 2 }));
            c.Put(1, "uno", 50); // same value: no eviction report
            Assert.That(evicted.Count, Is.EqualTo(1));

            string removed;
            Assert.That(c.Remove(2, out removed), Is.True);
            Assert.That(removed, Is.EqualTo("two"));
            Assert.That(evicted.Count, Is.EqualTo(1), "Remove hands the value back without Evicted");
            Assert.That(c.Remove(2), Is.False);
            Assert.That(c.Bytes, Is.EqualTo(50));

            for (int k = 10; k < 20; k++) c.Put(k, "v" + k, 10);
            c.Trim(60); // keep the most recent ones within 60 bytes
            Assert.That(c.Bytes, Is.LessThanOrEqualTo(60));
            Assert.That(c.Contains(19), Is.True);
            Assert.That(c.Contains(1), Is.False);

            c.BudgetBytes = 20;
            Assert.That(c.Bytes, Is.LessThanOrEqualTo(20));
            Assert.That(c.KeysByRecency(), Is.EqualTo(new[] { 19, 18 }));

            int before = evicted.Count;
            c.Clear();
            Assert.That(c.Count, Is.EqualTo(0));
            Assert.That(c.Bytes, Is.EqualTo(0));
            Assert.That(evicted.Count - before, Is.EqualTo(2));
            Assert.That(evicted[evicted.Count - 1], Is.EqualTo("v19"), "Clear reports least recent first");
        }

        [Test]
        public void LruKeepsAnOversizedEntryUntilTheNextPut()
        {
            var c = new LruByteCache<int, int>(100);
            c.Put(1, 1, 30);
            c.Put(2, 2, 500);
            Assert.That(c.Contains(2), Is.True);
            Assert.That(c.Contains(1), Is.False);
            c.Put(3, 3, 10);
            Assert.That(c.Contains(2), Is.False);
            Assert.That(c.Contains(3), Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() => c.Put(4, 4, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LruByteCache<int, int>(-1));
        }

        [Test]
        public void LruMatchesAReferenceModelAndStopsAllocating()
        {
            var rng = new Random(3);
            var c = new LruByteCache<int, int>(5000, 8);
            var order = new List<int>(); // most recent first
            var size = new Dictionary<int, long>();
            for (int step = 0; step < 20000; step++)
            {
                int k = rng.Next(200);
                if (rng.Next(3) == 0)
                {
                    int v;
                    bool hit = c.TryGet(k, out v);
                    Assert.That(hit, Is.EqualTo(size.ContainsKey(k)));
                    if (hit)
                    {
                        Assert.That(v, Is.EqualTo(k * 7));
                        order.Remove(k);
                        order.Insert(0, k);
                    }
                }
                else
                {
                    long bytes = 10 + rng.Next(300);
                    c.Put(k, k * 7, bytes);
                    order.Remove(k);
                    order.Insert(0, k);
                    size[k] = bytes;
                    long total = 0;
                    foreach (long b in size.Values) total += b;
                    while (total > 5000 && order.Count > 1)
                    {
                        int lru = order[order.Count - 1];
                        order.RemoveAt(order.Count - 1);
                        total -= size[lru];
                        size.Remove(lru);
                    }
                }
                if (step % 997 == 0) Assert.That(c.KeysByRecency(), Is.EqualTo(order));
            }

            // Steady state (after one warm-up pass of the same workload): puts, evictions and gets allocate nothing.
            int sink = 0;
            long before = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 1) before = GC.GetAllocatedBytesForCurrentThread();
                for (int rep = 0; rep < 5000; rep++)
                {
                    int k = rep % 300;
                    c.Put(k, k, 20);
                    int v;
                    if (c.TryGet((k * 13) % 300, out v)) sink += v;
                }
            }
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.That(after - before, Is.EqualTo(0));
            Assert.That(sink, Is.GreaterThan(0));
        }

        private static SelectedNode N(int level, int tx, int ty)
        {
            var t = new TileId(level, tx, ty);
            return new SelectedNode(t, t);
        }

        [Test]
        public void PlannerDiffsAndOrdersByDistance()
        {
            var p = new TileLoadPlanner();
            double fx = 516.5 * 1024, fz = 161.5 * 1024; // centre of 10/516/161
            var desired = new List<SelectedNode>
            {
                N(10, 516, 161), N(10, 517, 161), N(10, 515, 160), N(9, 259, 80),
                new SelectedNode(new TileId(10, 518, 162), new TileId(9, 259, 81)),
            };
            var resident = new HashSet<SelectedNode> { N(10, 517, 161), N(9, 300, 80), N(10, 400, 100), N(8, 129, 40) };
            var load = new List<SelectedNode>();
            var unload = new List<SelectedNode>();
            p.Plan(desired, resident, fx, fz, load, unload);
            Assert.That(load, Is.EqualTo(new[]
            {
                N(10, 516, 161), N(10, 515, 160), N(9, 259, 80), new SelectedNode(new TileId(10, 518, 162), new TileId(9, 259, 81)),
            }), "0 m, 724 m, 1536 m, 1619 m");
            Assert.That(unload, Is.EqualTo(new[] { N(10, 400, 100), N(9, 300, 80), N(8, 129, 40) }), "farthest first");

            // Same answer from a List, duplicates reported once; a node with another source is a different node.
            var residentList = new List<SelectedNode> { N(10, 517, 161), N(9, 300, 80), N(9, 300, 80), N(10, 400, 100), N(8, 129, 40) };
            p.Plan(desired, residentList, fx, fz, load, unload);
            Assert.That(unload, Is.EqualTo(new[] { N(10, 400, 100), N(9, 300, 80), N(8, 129, 40) }));
            Assert.That(load.Count, Is.EqualTo(4));
        }

        [Test]
        public void PlannerTileOverloadAndSources()
        {
            var p = new TileLoadPlanner();
            var desired = new List<TileId> { new TileId(10, 516, 161), new TileId(9, 258, 80), new TileId(10, 516, 161) };
            var resident = new HashSet<TileId> { new TileId(9, 258, 80), new TileId(5, 16, 5) };
            var load = new List<TileId>();
            var unload = new List<TileId>();
            p.Plan(desired, resident, 516.5 * 1024, 161.5 * 1024, load, unload);
            Assert.That(load, Is.EqualTo(new[] { new TileId(10, 516, 161) }));
            Assert.That(unload, Is.EqualTo(new[] { new TileId(5, 16, 5) }));

            var nodes = new List<SelectedNode>
            {
                N(10, 516, 161), new SelectedNode(new TileId(10, 514, 158), new TileId(9, 257, 79)),
                new SelectedNode(new TileId(10, 515, 158), new TileId(9, 257, 79)), N(9, 260, 80),
            };
            var sources = new List<TileId>();
            p.Sources(nodes, sources);
            Assert.That(sources, Is.EqualTo(new[] { new TileId(10, 516, 161), new TileId(9, 257, 79), new TileId(9, 260, 80) }));
        }

        [Test]
        public void PlannerStopsAllocatingAfterWarmUp()
        {
            StreamingConfig cfg = StreamingConfig.ForTier(StreamingConfig.TierMid);
            TileSelector sel = TileSelector.ForPack(cfg, StreamingSampleRegion.Pack);
            var a = new List<SelectedNode>(512);
            var b = new List<SelectedNode>(512);
            sel.Select(528000, 165000, a);
            sel.Select(532000, 166500, b);
            var resident = new HashSet<SelectedNode>(a);
            var p = new TileLoadPlanner();
            var load = new List<SelectedNode>(512);
            var unload = new List<SelectedNode>(512);
            p.Plan(b, resident, 532000, 166500, load, unload);
            Assert.That(load.Count + (a.Count - unload.Count), Is.EqualTo(b.Count), "kept + loaded = desired");
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int rep = 0; rep < 20; rep++) p.Plan(b, resident, 532000, 166500, load, unload);
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }

        [Test]
        public void TileMemoryEstimatesGrowWithContent()
        {
            TileData leaf = StreamingSampleRegion.Tile(StreamingSampleRegion.DensestLeaf());
            TileData horizon = StreamingSampleRegion.Tile(StreamingSampleRegion.TilesAt(5)[0]);
            long a = TileMemory.EstimateBytes(leaf), b = TileMemory.EstimateBytes(horizon);
            Assert.That(b, Is.GreaterThan(2 * 129 * 129));
            Assert.That(a, Is.GreaterThan(b * 5));
            Assert.That(a, Is.LessThan(64L << 20));
            Assert.That(TileMemory.EstimateBytes(null), Is.EqualTo(0));
            TestContext.WriteLine("densest leaf ~{0:0.0} MB decoded, horizon tile ~{1:0.0} KB", a / 1048576.0, b / 1024.0);
        }
    }
}
