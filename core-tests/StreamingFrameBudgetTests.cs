using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Streaming;
using Ghumante.Core.Synth;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Perf pass (perf-roads, playtest #4 "stutters"): the tile uploads and the B0 building-cell uploads share one byte
    /// budget per frame (<see cref="StreamingScheduler.UploadBudgetBytes"/>, 1 MiB). The B0 cells (perf-cells) upload
    /// first when they waited too long, and the tiles then get the rest of the frame through
    /// <see cref="StreamingScheduler.Tick(double, double, long)"/>'s reserved bytes.
    /// </summary>
    public class StreamingFrameBudgetTests
    {
        /// <summary>A sink that only counts: bytes of the frame, chunks, and the nodes with a view.</summary>
        internal sealed class CountingSink : ITileSink
        {
            public readonly Dictionary<SelectedNode, bool> Views = new Dictionary<SelectedNode, bool>();
            public long FrameBytes;
            public int Uploads;

            public void Upload(TileBuild build, int chunk)
            {
                FrameBytes += build.ChunkBytes(chunk);
                Uploads++;
                if (!Views.ContainsKey(build.Node)) Views[build.Node] = false;
            }

            public void SetVisible(SelectedNode node, bool visible)
            {
                Views[node] = visible;
            }

            public void Release(SelectedNode node)
            {
                Views.Remove(node);
            }

            public void Ready(SelectedNode node, TileExtras extras)
            {
            }
        }

        /// <summary>
        /// The B0 side of a frame as perf-cells' <c>WorldStreamer.Tick</c> runs it: cell meshes of about half a
        /// megabyte, uploaded after the tiles with what the frame has left, or first (up to half the cap, at least one)
        /// once they found no room for <see cref="StarvedTicks"/> frames in a row.
        /// </summary>
        private sealed class FakeCells
        {
            public const int StarvedTicks = 3;
            public readonly Queue<long> Waiting = new Queue<long>();
            public int Starved;

            public long Upload(long budget, bool atLeastOne)
            {
                long used = 0;
                while (Waiting.Count > 0 && (used + Waiting.Peek() <= budget || atLeastOne && used == 0)) used += Waiting.Dequeue();
                return used;
            }
        }

        private static StreamingScheduler NewScheduler(CountingSink sink, out double x, out double z)
        {
            var meshing = MeshingSettings.ForTier(StreamingConfig.TierLow);
            var ground = new TileGroundQuery(meshing.Roads);
            StreamingConfig config = StreamingConfig.ForTier(StreamingConfig.TierLow);
            WorldFrame.LonLatToGame(85.3122, 27.7074, out x, out z); // Asan
            var selector = TileSelector.ForPack(config, StreamingSampleRegion.Pack);
            double now = 0;
            return new StreamingScheduler(StreamingSampleRegion.Pack, config, selector, ground, sink, new InlineJobRunner(), () => now += 0.01, meshing)
            {
                UploadBudgetMs = 1e9, // only bytes limit a frame here
            };
        }

        /// <summary>
        /// Repro and fix: with the B0 cells first in a frame, perf-cells' first wiring lowered
        /// <see cref="StreamingScheduler.UploadBudgetBytes"/> to what they left, but the tick's first tile chunk always
        /// went whatever its size, so frames copied up to 0.5 + 1 MB (1.14 MB in 3 frames of this run before the fix).
        /// Now the reserved bytes handed to the tick, and a lowered budget too, keep every frame within the 1 MiB cap,
        /// and both the cells and the tiles still finish.
        /// </summary>
        [Test]
        public void B0CellsAndTileChunksTogetherStayWithinTheFrameCap()
        {
            const long cap = 1L << 20;
            long legacyWorst = 0, reservedWorst = 0;
            int legacyOver = 0, reservedFrames = 0;
            for (int mode = 0; mode < 2; mode++)
            {
                bool reserved = mode == 1;
                var sink = new CountingSink();
                double x, z;
                var cells = new FakeCells();
                int made = 0;
                using (StreamingScheduler s = NewScheduler(sink, out x, out z))
                {
                    int frame = 0;
                    while (!s.IsSettled || cells.Waiting.Count > 0 || made < 60)
                    {
                        Assert.That(frame++, Is.LessThan(4000), "streaming and the cells finish: " + s.Stats.Format());
                        // A B0 cell of 0.50 to 0.58 MB finishes every other frame while the player rides (perf-cells'
                        // time-sliced builds), so cells keep arriving while the dense detail tiles upload.
                        if (frame % 2 == 0 && made < 60) cells.Waiting.Enqueue(500000 + 20000 * (made++ % 5));
                        sink.FrameBytes = 0;
                        bool waiting = cells.Waiting.Count > 0;
                        long b0 = 0;
                        if (waiting && cells.Starved >= FakeCells.StarvedTicks) b0 = cells.Upload(cap / 2, true);
                        if (reserved)
                        {
                            s.Tick(x, z, b0);
                        }
                        else
                        {
                            s.UploadBudgetBytes = Math.Max(1, cap - b0);
                            s.Tick(x, z);
                            s.UploadBudgetBytes = cap;
                        }
                        Assert.That(s.Stats.TickUploadBytes, Is.EqualTo(sink.FrameBytes), "the tick reports what it uploaded");
                        long used = b0 + sink.FrameBytes;
                        if (b0 == 0 && waiting) b0 = cells.Upload(cap - used, false);
                        cells.Starved = waiting && b0 == 0 ? cells.Starved + 1 : 0;
                        long total = sink.FrameBytes + b0;
                        if (reserved)
                        {
                            reservedWorst = Math.Max(reservedWorst, total);
                            reservedFrames = frame;
                            Assert.That(total, Is.LessThanOrEqualTo(cap), "frame " + frame + ": tiles " + sink.FrameBytes + " + B0 " + b0);
                        }
                        else
                        {
                            legacyWorst = Math.Max(legacyWorst, total);
                            if (total > cap) legacyOver++;
                        }
                    }
                    Assert.That(s.Stats.Failed, Is.EqualTo(0));
                    Assert.That(s.NextChunkBytes, Is.EqualTo(0), "nothing waits once settled");
                }
            }
            TestContext.WriteLine("frame bytes, B0 first then tiles: lowered budget worst {0:0.00} MB, {1} frames over 1 MiB; " +
                                  "reserved bytes worst {2:0.00} MB, settled in {3} frames", legacyWorst / 1048576.0, legacyOver,
                                  reservedWorst / 1048576.0, reservedFrames);
            Assert.That(legacyOver, Is.EqualTo(0), "a lowered budget is a cap too (the tick's first chunk no longer always goes)");
            Assert.That(reservedWorst, Is.LessThanOrEqualTo(cap));
        }

        /// <summary>The reserved bytes on their own: a chunk that does not fit next to them waits for a later frame (no
        /// forced first chunk), one that fits goes; a budget lowered below every chunk holds it for
        /// <see cref="StreamingScheduler.MaxHeldTicks"/> ticks, then lets it go alone.</summary>
        [Test]
        public void ReservedBytesHoldBackAChunkThatDoesNotFit()
        {
            var sink = new CountingSink();
            double x, z;
            using (StreamingScheduler s = NewScheduler(sink, out x, out z))
            {
                // Build until a chunk waits.
                int guard = 0;
                while (s.NextChunkBytes == 0)
                {
                    Assert.That(guard++, Is.LessThan(50));
                    s.Tick(x, z, s.UploadBudgetBytes); // the whole frame reserved: builds land, nothing uploads
                    Assert.That(s.Stats.TickUploadBytes, Is.EqualTo(0));
                }
                long next = s.NextChunkBytes;
                Assert.That(next, Is.InRange(1L, TileBuild.UploadChunkBytes));
                sink.FrameBytes = 0;
                s.Tick(x, z, s.UploadBudgetBytes - next + 1);
                Assert.That(sink.FrameBytes, Is.EqualTo(0), "one byte short: the chunk waits");
                s.Tick(x, z, s.UploadBudgetBytes - next);
                Assert.That(sink.FrameBytes, Is.EqualTo(next), "exactly enough: only that chunk goes");
                // A budget lowered below every chunk: the chunk waits, then goes alone after MaxHeldTicks so nothing stalls.
                s.UploadBudgetBytes = 1;
                long before = sink.Uploads;
                for (int i = 0; i < s.MaxHeldTicks; i++)
                {
                    s.Tick(x, z);
                    Assert.That(sink.Uploads, Is.EqualTo(before), "held in tick " + i);
                }
                s.Tick(x, z);
                Assert.That(sink.Uploads - before, Is.EqualTo(1), "after MaxHeldTicks the chunk goes alone");
                s.Tick(x, z);
                Assert.That(sink.Uploads - before, Is.EqualTo(1), "and the next one waits again");
            }
        }

        /// <summary>
        /// Owner playtest #4 ("stutters"), the time side: <see cref="StreamingScheduler.UploadBudgetMs"/> is the frame's
        /// whole streaming budget, so the tick's own work before the uploads (the selection, about 0.25 ms of this desktop
        /// and 0.8 to 1 ms of a Low phone every 4 m, and collecting finished builds) counts against it. Repro: the budget
        /// clock used to start at the first upload, so a phone frame that reselected could spend the selection and then
        /// the whole budget on chunks (StreamingRideTests saw 1.01 ms of selection in one desktop frame). Now a tick whose
        /// own work already spent the budget uploads its first chunk only (streaming never stalls), and a tick with time
        /// left uploads several.
        /// </summary>
        [Test]
        public void TheTicksOwnWorkCountsAgainstTheUploadTime()
        {
            var sink = new CountingSink();
            double x, z, now = 0, jumpMs = 0;
            int reads = 0;
            var meshing = MeshingSettings.ForTier(StreamingConfig.TierLow);
            StreamingConfig config = StreamingConfig.ForTier(StreamingConfig.TierLow);
            WorldFrame.LonLatToGame(85.3122, 27.7074, out x, out z); // Asan
            // Every clock read advances 0.05 ms; the second read of a tick (the end of the selection) jumps by jumpMs, as a
            // slow selection would.
            Func<double> clock = () =>
            {
                if (++reads == 2 && jumpMs > 0)
                {
                    now += jumpMs;
                    jumpMs = 0;
                }
                return now += 0.05;
            };
            using (var s = new StreamingScheduler(StreamingSampleRegion.Pack, config, TileSelector.ForPack(config, StreamingSampleRegion.Pack),
                                                  new TileGroundQuery(meshing.Roads), sink, new InlineJobRunner(), clock, meshing))
            {
                s.UploadBudgetBytes = 1L << 30; // only time limits a tick here
                s.UploadBudgetMs = 1.0;
                int guard = 0;
                while (s.NextChunkBytes == 0)
                {
                    Assert.That(guard++, Is.LessThan(50));
                    reads = 0;
                    s.Tick(x, z, s.UploadBudgetBytes); // builds land, nothing uploads
                }
                reads = 0;
                jumpMs = 1.5; // the tick's own work took longer than the whole budget
                s.Tick(x, z);
                Assert.That(s.Stats.TickUploadChunks, Is.EqualTo(1), "only the first chunk goes once the tick's own work spent the budget");
                Assert.That(s.Stats.TickMs, Is.GreaterThan(1.5), "the whole tick is reported, so the B0 cells after it know nothing is left");
                Assert.That(s.Stats.UploadMs, Is.LessThan(s.Stats.TickMs - 1.5 + 1e-9), "the upload time alone leaves the selection out");
                reads = 0;
                jumpMs = 0.6; // most of the budget gone before the uploads
                s.Tick(x, z);
                int part = s.Stats.TickUploadChunks;
                reads = 0;
                s.Tick(x, z);
                int full = s.Stats.TickUploadChunks;
                TestContext.WriteLine("chunks per tick: budget spent 1, 0.4 ms left " + part + ", whole budget " + full);
                Assert.That(full, Is.GreaterThan(1), "a tick with time left uploads several chunks");
                Assert.That(part, Is.LessThan(full), "less time left, fewer chunks");
                Assert.That(part, Is.GreaterThanOrEqualTo(1));
            }
        }

        /// <summary>
        /// The static dressing colliders of a dense tile are indexed by the build's worker
        /// (<see cref="TileExtras.PreparedColliders"/>), and the ground query answers the same with the prepared set as
        /// with the raw one registered on the main thread (what WorldStreamer.RegisterStatic did each time the tile
        /// showed: 2 to 3 ms of a desktop's main thread at Asan, StreamingRideTests).
        /// </summary>
        [Test]
        public void PreparedStaticCollidersAnswerLikeTheRawSet()
        {
            MeshingSettings settings = StreamingScene.Settings(StreamingConfig.TierMid, Ghumante.Core.Meshing.BiomePalette.DefaultMonth);
            TileGroundQuery ground = StreamingScene.Ground(settings);
            TileId id = StreamingScene.TileAt(85.3122, 27.7074); // Asan
            TileBuild b = StreamingScene.Build(id, settings, ground);
            TileExtras e = b.TakeExtras();
            Assert.That(e.Colliders, Is.Not.Null);
            // Trunks, street-prop poles and the parked vehicles nobody can borrow. Since the first playtest (#3) every parked
            // vehicle the player can drive is a community-fleet one, registered round the player by ExploreSession rather
            // than in this static set (StreamingSceneTests.DressingCollidersAreASetOfTheirOwn): Asan's set went from 1695
            // colliders, nearly all parked-vehicle boxes, to 200 (198 trunk and pole cylinders, 2 boxes), so the probes below
            // run round the cylinders as well as the boxes.
            Assert.That(e.Colliders.Count, Is.GreaterThan(150), "a dense tile's dressing");
            Assert.That(e.PreparedColliders, Is.Not.Null, "indexed on the worker");
            ulong key = id.Key | TileGroundQuery.StaticSetKeyBit;
            Assert.That(e.PreparedColliders.TileKey, Is.EqualTo(key));

            // Linking a prepared set is a few list inserts (microseconds), so a single cold timing of it is the first call's
            // JIT and whatever collection lands in it: 0.12 ms alone and 0.45 ms in the full suite for the same 200-collider
            // set, against 0.33-0.76 ms for indexing it raw (warm, the link takes 0.001 ms and the raw index 0.24-0.46 ms).
            // Both overloads are warmed on throwaway queries first and each is timed as the best of three.
            var warm = new TileGroundQuery(settings.Roads);
            warm.Register(key, e.Colliders);
            warm.Unregister(key);
            warm.Register(e.PreparedColliders);
            warm.Unregister(key);
            TileGroundQuery raw = null, prepared = null;
            double rawMs = double.MaxValue, preparedMs = double.MaxValue;
            for (int rep = 0; rep < 3; rep++)
            {
                raw = new TileGroundQuery(settings.Roads);
                prepared = new TileGroundQuery(settings.Roads);
                double t0 = StreamingRideTests.ThreadClock.Ms();
                raw.Register(key, e.Colliders);
                rawMs = Math.Min(rawMs, StreamingRideTests.ThreadClock.Ms() - t0);
                t0 = StreamingRideTests.ThreadClock.Ms();
                prepared.Register(e.PreparedColliders);
                preparedMs = Math.Min(preparedMs, StreamingRideTests.ThreadClock.Ms() - t0);
            }
            Assert.That(prepared.StructureTileCount, Is.EqualTo(raw.StructureTileCount));
            int blocked = 0, hits = 0;
            for (int i = 0; i < e.Colliders.Boxes.Count; i += 3)
            {
                OrientedBox box = e.Colliders.Boxes[i];
                double x = id.X0 + box.CX, z = id.Z0 + box.CZ;
                for (int k = 0; k < 3; k++)
                {
                    double px = x + (k - 1) * 0.4, pz = z + (k - 1) * 0.3;
                    bool a = raw.IsBlocked(px, pz, box.Bottom + 0.05f), c = prepared.IsBlocked(px, pz, box.Bottom + 0.05f);
                    Assert.That(c, Is.EqualTo(a), "box " + i + " at " + px.ToString("0.0") + ", " + pz.ToString("0.0"));
                    if (a) blocked++;
                }
                double ha, hc;
                bool ra = raw.SphereCast(x - 3, box.CY, z - 3, 0.7071, 0, 0.7071, 0.3, 10, out ha);
                bool rc = prepared.SphereCast(x - 3, box.CY, z - 3, 0.7071, 0, 0.7071, 0.3, 10, out hc);
                Assert.That(rc, Is.EqualTo(ra));
                Assert.That(hc, Is.EqualTo(ha).Within(1e-9));
                if (ra) hits++;
            }
            for (int i = 0; i < e.Colliders.Cylinders.Count; i += 3)
            {
                ColliderCylinder cyl = e.Colliders.Cylinders[i];
                double x = id.X0 + cyl.CX, z = id.Z0 + cyl.CZ;
                for (int k = 0; k < 3; k++)
                {
                    double px = x + (k - 1) * 0.4, pz = z + (k - 1) * 0.3;
                    bool a = raw.IsBlocked(px, pz, cyl.Bottom + 0.05f), c = prepared.IsBlocked(px, pz, cyl.Bottom + 0.05f);
                    Assert.That(c, Is.EqualTo(a), "cylinder " + i + " at " + px.ToString("0.0") + ", " + pz.ToString("0.0"));
                    if (a) blocked++;
                }
                double ha, hc, y = 0.5 * (cyl.Bottom + cyl.Top);
                bool ra = raw.SphereCast(x - 3, y, z - 3, 0.7071, 0, 0.7071, 0.3, 10, out ha);
                bool rc = prepared.SphereCast(x - 3, y, z - 3, 0.7071, 0, 0.7071, 0.3, 10, out hc);
                Assert.That(rc, Is.EqualTo(ra), "cylinder " + i + " cast");
                Assert.That(hc, Is.EqualTo(ha).Within(1e-9), "cylinder " + i + " cast distance");
                if (ra) hits++;
            }
            prepared.Unregister(key);
            Assert.That(prepared.StructureTileCount, Is.EqualTo(0));
            TestContext.WriteLine("{0}: {1} static colliders; registering raw {2:0.00} ms, prepared {3:0.000} ms (main thread); {4} blocked probes, {5} cast hits agree",
                                  id, e.Colliders.Count, rawMs, preparedMs, blocked, hits);
            Assert.That(blocked, Is.GreaterThan(0));
            Assert.That(preparedMs, Is.LessThan(rawMs), "linking a prepared set is cheaper than indexing it");
        }

        /// <summary>
        /// Owner playtest #4 (stutters), main-thread work when a tile shows: every exact level-10 area joining the ground
        /// query (the frame its tile showed, in the scheduler's swap rule; the terrain-only tiles of the near ring too)
        /// indexed the tile's paved areas inside <see cref="TileGroundQuery.Add(TileId, TileHeightSampler, RoadSpatialIndex)"/>,
        /// up to 0.12 ms of a desktop per tile. The tile build now prepares that index on its worker
        /// (<see cref="TileGroundQuery.PreparePaving"/>), the main thread only looks it up, and the ground answers the same
        /// over every detail tile of the sample.
        /// </summary>
        [Test]
        public void PavingIndexedOnTheWorkerLeavesTheMainThreadOnlyALookup()
        {
            MeshingSettings settings = MeshingSettings.ForTier(StreamingConfig.TierMid);
            PackReader pack = StreamingSampleRegion.Pack;
            TileId first = StreamingSampleRegion.DensestLeaf();
            TileData warmUp = pack.ReadTile(first);
            TileGroundQuery.PreparePaving(warmUp); // compiles the indexing once
            new TileGroundQuery(settings.Roads).Add(first, TileHeightSampler.ForArea(warmUp, first, 16), null);

            double worstInline = 0, worstPrepared = 0, worstWorker = 0, sumInline = 0, sumPrepared = 0;
            TileId worst = first;
            int tiles = 0, probes = 0;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                // As before the perf pass: a freshly decoded tile nobody prepared is indexed inside Add.
                TileData cold = pack.ReadTile(id);
                if (!cold.HasDetail) continue;
                tiles++;
                var inline = new TileGroundQuery(settings.Roads);
                TileHeightSampler coldSampler = TileHeightSampler.ForArea(cold, id, 16);
                double t0 = StreamingRideTests.ThreadClock.Ms();
                inline.Add(id, coldSampler, null);
                double inlineMs = StreamingRideTests.ThreadClock.Ms() - t0;

                // Now: the build's worker prepared it (TileBuild.Execute), the main thread's Add looks it up.
                TileData built = pack.ReadTile(id);
                t0 = StreamingRideTests.ThreadClock.Ms();
                TileGroundQuery.PreparePaving(built);
                double workerMs = StreamingRideTests.ThreadClock.Ms() - t0;
                var prepared = new TileGroundQuery(settings.Roads);
                TileHeightSampler builtSampler = TileHeightSampler.ForArea(built, id, 16);
                t0 = StreamingRideTests.ThreadClock.Ms();
                prepared.Add(id, builtSampler, null);
                double preparedMs = StreamingRideTests.ThreadClock.Ms() - t0;

                for (double z = id.Z0 + 3; z < id.Z0 + id.Size; z += 11.3)
                for (double x = id.X0 + 3; x < id.X0 + id.Size; x += 11.3)
                {
                    GroundSample a, b;
                    Assert.That(prepared.TrySample(x, z, out b), Is.EqualTo(inline.TrySample(x, z, out a)));
                    Assert.That(b.Foot, Is.EqualTo(a.Foot), id + " at " + x.ToString("0.0") + ", " + z.ToString("0.0"));
                    Assert.That(b.Height, Is.EqualTo(a.Height));
                    probes++;
                }
                sumInline += inlineMs;
                sumPrepared += preparedMs;
                if (inlineMs > worstInline)
                {
                    worstInline = inlineMs;
                    worstPrepared = preparedMs;
                    worstWorker = workerMs;
                    worst = id;
                }
            }
            TestContext.WriteLine("{0} detail tiles, {1} probes agree; Add on the main thread: {2:0.00} ms in all with the paving indexed inline, " +
                                  "{3:0.00} ms prepared on the worker; worst tile {4}: {5:0.000} ms inline, {6:0.000} ms prepared (+ {7:0.000} ms on the worker)",
                                  tiles, probes, sumInline, sumPrepared, worst, worstInline, worstPrepared, worstWorker);
            Assert.That(tiles, Is.GreaterThan(3));
            Assert.That(sumPrepared, Is.LessThan(sumInline * 0.5), "the main thread no longer indexes the paving");
        }

        /// <summary>
        /// The audio index of a shown tile is built on a worker (<see cref="PlaceAudioIndex.Prepare"/>, as
        /// WorldAudioPlace now does) and joins on the main thread; it answers exactly as the index built on the main
        /// thread did, over two dense tiles (Asan and Thamel).
        /// </summary>
        [Test]
        public void AudioTilesIndexedOnAWorkerAnswerLikeTheMainThreadIndex()
        {
            TileId asan = StreamingScene.TileAt(85.3122, 27.7074), thamel = StreamingScene.TileAt(85.3110, 27.7150);
            var direct = new PlaceAudioIndex();
            var joined = new PlaceAudioIndex();
            var prepared = new List<PlaceAudioIndex.PreparedTile>();
            foreach (TileId id in new[] { asan, thamel })
            {
                TileData t = StreamingSampleRegion.Tile(id);
                double t0 = StreamingRideTests.ThreadClock.Ms();
                direct.AddTile(id, t);
                double mainMs = StreamingRideTests.ThreadClock.Ms() - t0;
                PlaceAudioIndex.PreparedTile p = System.Threading.Tasks.Task.Run(() => PlaceAudioIndex.Prepare(id, t)).Result;
                Assert.That(p.Id, Is.EqualTo(id));
                t0 = StreamingRideTests.ThreadClock.Ms();
                joined.AddTile(p);
                double joinMs = StreamingRideTests.ThreadClock.Ms() - t0;
                prepared.Add(p);
                TestContext.WriteLine("{0}: indexing on the main thread {1:0.00} ms, joining a prepared tile {2:0.000} ms", id, mainMs, joinMs);
            }
            Assert.That(joined.TileCount, Is.EqualTo(direct.TileCount));
            int occluded = 0, ring = 0;
            for (double x = asan.X0 + 5; x < asan.X0 + asan.Size; x += 37)
            for (double z = asan.Z0 + 5; z < thamel.Z0 + thamel.Size; z += 41)
            {
                PlaceSample a = direct.Sample(x, z), b = joined.Sample(x, z);
                Assert.That(b.RingRoad01, Is.EqualTo(a.RingRoad01));
                Assert.That(b.Park01, Is.EqualTo(a.Park01));
                Assert.That(b.Water01, Is.EqualTo(a.Water01));
                bool oa = direct.Occluded(x, 1.5f, z, x + 23, 1.5f, z + 17), ob = joined.Occluded(x, 1.5f, z, x + 23, 1.5f, z + 17);
                Assert.That(ob, Is.EqualTo(oa));
                Assert.That(joined.GalliWidth(x, z, 1f, 0f, 2f), Is.EqualTo(direct.GalliWidth(x, z, 1f, 0f, 2f)));
                if (oa) occluded++;
                if (a.RingRoad01 > 0) ring++;
            }
            Assert.That(occluded, Is.GreaterThan(0), "dense blocks occlude");
            // Replacing and removing work as with the main-thread adds.
            joined.AddTile(prepared[0]);
            Assert.That(joined.TileCount, Is.EqualTo(2));
            joined.RemoveTile(asan);
            Assert.That(joined.TileCount, Is.EqualTo(1));
            TestContext.WriteLine("{0} occluded probes and {1} arterial samples agree", occluded, ring);
        }

        /// <summary>
        /// WildlifeDirector now indexes each shown tile's fauna ground on a worker into an index of its own and asks the
        /// finest tile containing a point; that answers as the single shared index it used to fill on the main thread.
        /// </summary>
        [Test]
        public void PerTileFaunaGroundAnswersLikeTheSharedIndex()
        {
            TileId asan = StreamingScene.TileAt(85.3122, 27.7074), thamel = StreamingScene.TileAt(85.3110, 27.7150);
            Func<TileData, Ghumante.Core.Meshing.Roads.IRoadCorridorQuery> corridors = Ghumante.Core.Meshing.Roads.RoadCorridorIndex.ForTile;
            var shared = new Ghumante.Core.Generators.Fauna.FaunaGroundIndex { CorridorFactory = corridors };
            var own = new Dictionary<ulong, Ghumante.Core.Generators.Fauna.FaunaGroundIndex>();
            foreach (TileId id in new[] { asan, thamel })
            {
                TileData t = StreamingSampleRegion.Tile(id);
                shared.Add(t);
                own[id.Key] = System.Threading.Tasks.Task.Run(() =>
                {
                    var i = new Ghumante.Core.Generators.Fauna.FaunaGroundIndex { CorridorFactory = corridors };
                    i.Add(t);
                    return i;
                }).Result;
            }
            int checkedPoints = 0, buildings = 0;
            for (double x = asan.X0 + 3; x < asan.X0 + asan.Size; x += 19)
            for (double z = asan.Z0 + 3; z < thamel.Z0 + thamel.Size; z += 23)
            {
                TileId at = TileId.At(10, x, z);
                Ghumante.Core.Generators.Fauna.FaunaGroundIndex mine;
                if (!own.TryGetValue(at.Key, out mine)) continue;
                Ghumante.Core.Generators.Fauna.FaunaGround a = shared.At(x, z), b = mine.At(x, z);
                Assert.That(b, Is.EqualTo(a), "at " + x.ToString("0") + ", " + z.ToString("0"));
                checkedPoints++;
                if ((a & Ghumante.Core.Generators.Fauna.FaunaGround.Building) != 0) buildings++;
            }
            Assert.That(checkedPoints, Is.GreaterThan(2000));
            Assert.That(buildings, Is.GreaterThan(100));
        }
    }
}
