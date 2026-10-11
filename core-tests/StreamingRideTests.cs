using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Generators.Fauna;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Routing;
using Ghumante.Core.Streaming;
using Ghumante.Core.Synth;
using Ghumante.World.Buildings;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Owner playtest #4 ("buildings constantly flicker and stutter"), the streaming side: a scooter ridden at 50 km/h
    /// from Thamel Chowk through Asan and Indra Chowk down New Road to the New Road Gate on the motorbike route, with the
    /// game's streamer as it runs per frame (<see cref="StreamingScheduler"/> with the tier's
    /// <see cref="StreamingConfig.ForTier"/>, <see cref="MeshingSettings.ForTier"/>, the real builds of the sample) and
    /// the main-thread work of a detail tile being shown (the handlers that index it). Builds run on two virtual workers:
    /// a build takes its measured desktop time times a phone factor, so tiles arrive as late as they would on a phone.
    /// Per frame the harness records the scheduler's main-thread milliseconds, the mesh bytes it uploads (the copy a phone
    /// pays for) and the handlers' milliseconds, and checks that every street of the route within the B1 radius of the
    /// rider (where the full road kit is drawn) is drawn: no road block pops in or vanishes near the player.
    /// </summary>
    public class StreamingRideTests
    {
        /// <summary>50 km/h.</summary>
        internal const double SpeedMps = 50.0 / 3.6;

        /// <summary>Desktop-to-phone factor for worker build times (a Low or Mid phone core is several times slower than
        /// this machine's; 4 is conservative for the Snapdragon 6/7 and Helio G class in ref_quality.md).</summary>
        internal const double PhoneFactor = 4.0;

        private static readonly string[] TierName = { "Low", "Mid", "High" };

        /// <summary>Rough single-thread slowdown of the tier's phones against this desktop for the main thread's own work
        /// (ref_quality.md: Low below Snapdragon 7 Gen 1, e.g. Helio G85/G99 and Snapdragon 680; Mid Snapdragon 7 Gen 1 /
        /// 7s Gen 2; High from Snapdragon 7 Gen 3).</summary>
        internal static readonly double[] MainThreadFactor = { 4.0, 3.0, 2.0 };

        /// <summary>Thamel Chowk, Asan Tol, Indra Chowk, New Road Peepal Bot, New Road Gate (the sample's search index).</summary>
        private static readonly double[,] Waypoints =
        {
            { 85.31138, 27.71434 }, { 85.31252, 27.70755 }, { 85.30961, 27.70602 }, { 85.31014, 27.70344 }, { 85.31309, 27.70327 },
        };

        private static List<double[]> _route;

        /// <summary>The motorbike route through the waypoints (game metres), legs joined.</summary>
        internal static List<double[]> Route()
        {
            if (_route != null) return _route;
            string path = Path.Combine(GoldenFiles.RepoRoot, "shared", "sample-regions", "kathmandu_core", "kathmandu_core.route.ghrg");
            RouteGraph g = RouteGraphReader.Read(File.ReadAllBytes(path));
            var astar = new AStar(g);
            var pts = new List<double[]>();
            for (int w = 0; w + 1 < Waypoints.GetLength(0); w++)
            {
                double ax, az, bx, bz;
                WorldFrame.LonLatToGame(Waypoints[w, 0], Waypoints[w, 1], out ax, out az);
                WorldFrame.LonLatToGame(Waypoints[w + 1, 0], Waypoints[w + 1, 1], out bx, out bz);
                int src = new NearestNode(g, Travel.Motorbike, false).Find(ax, az);
                int dst = new NearestNode(g, Travel.Motorbike, true).Find(bx, bz);
                Ghumante.Core.Routing.Route r = astar.FindRoute(src, dst, Travel.Motorbike);
                Assert.That(r, Is.Not.Null, "leg " + w);
                foreach (int n in r.Nodes)
                {
                    var p = new[] { 0.1 * g.NodeXDm[n], 0.1 * g.NodeZDm[n] };
                    if (pts.Count > 0 && Math.Abs(pts[pts.Count - 1][0] - p[0]) < 1e-6 && Math.Abs(pts[pts.Count - 1][1] - p[1]) < 1e-6) continue;
                    pts.Add(p);
                }
            }
            _route = pts;
            return pts;
        }

        /// <summary>The route resampled every <paramref name="stepM"/> metres: x, z and the distance along it.</summary>
        internal static List<double[]> Resample(List<double[]> route, double stepM)
        {
            var o = new List<double[]>();
            double along = 0, next = 0;
            for (int i = 0; i + 1 < route.Count; i++)
            {
                double dx = route[i + 1][0] - route[i][0], dz = route[i + 1][1] - route[i][1], len = Math.Sqrt(dx * dx + dz * dz);
                while (next <= along + len)
                {
                    double t = len > 0 ? (next - along) / len : 0;
                    o.Add(new[] { route[i][0] + dx * t, route[i][1] + dz * t, next });
                    next += stepM;
                }
                along += len;
            }
            return o;
        }

        /// <summary>
        /// Milliseconds of CPU the calling thread has used (<c>clock_gettime(CLOCK_THREAD_CPUTIME_ID)</c>), so a frame's
        /// main-thread work is measured without the time the thread spent descheduled on this shared machine; wall
        /// milliseconds where the call is not available.
        /// </summary>
        internal static class ThreadClock
        {
            private struct Timespec
            {
                public long Sec, Nsec;
            }

            [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "clock_gettime")]
            private static extern int ClockGetTime(int clock, out Timespec ts);

            private const int ThreadCpuTimeId = 3;
            private static readonly bool Available = Probe();

            private static bool Probe()
            {
                try
                {
                    Timespec ts;
                    return ClockGetTime(ThreadCpuTimeId, out ts) == 0;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            public static bool IsCpuTime
            {
                get { return Available; }
            }

            public static double Ms()
            {
                if (!Available) return Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;
                Timespec ts;
                ClockGetTime(ThreadCpuTimeId, out ts);
                return ts.Sec * 1000.0 + ts.Nsec / 1e6;
            }
        }

        /// <summary>Runs the queued builds as two workers would: at pick-up a build gets a due time (its measured desktop
        /// time times the phone factor), and it runs, and lands for the scheduler, when the clock reaches it.</summary>
        private sealed class VirtualWorkers : ITileJobRunner
        {
            public readonly List<Action> Queued = new List<Action>();
            private readonly List<KeyValuePair<double, Action>> _running = new List<KeyValuePair<double, Action>>();
            private readonly double[] _busyUntil = new double[2];
            public readonly Dictionary<string, double> BuildMs;
            public readonly double Factor;
            public double WorstBuildMs;
            public int Builds;

            public VirtualWorkers(Dictionary<string, double> buildMs, double factor)
            {
                BuildMs = buildMs;
                Factor = factor;
            }

            public void Run(Action work)
            {
                Queued.Add(work);
            }

            /// <summary>Builds whose due time has come (untimed: worker threads).</summary>
            public void Complete(double nowS)
            {
                for (int i = 0; i < _running.Count; i++)
                {
                    if (_running[i].Key > nowS) continue;
                    Execute(_running[i].Value);
                    _running.RemoveAt(i--);
                }
            }

            /// <summary>Free workers take queued builds (<paramref name="instant"/>: every queued build runs at once, as on
            /// the arrival screen).</summary>
            public void PickUp(double nowS, bool instant = false)
            {
                for (int k = 0; k < _busyUntil.Length && Queued.Count > 0; k++)
                {
                    if (_busyUntil[k] > nowS && !instant) continue;
                    Action work = Queued[0];
                    Queued.RemoveAt(0);
                    if (Factor <= 0 || instant)
                    {
                        Execute(work);
                        k--;
                        continue;
                    }
                    double ms;
                    if (!BuildMs.TryGetValue(Key(work), out ms)) ms = 200;
                    double due = nowS + ms * Factor / 1000.0;
                    _busyUntil[k] = due;
                    _running.Add(new KeyValuePair<double, Action>(due, work));
                }
            }

            private void Execute(Action work)
            {
                var b = (TileBuild)work.Target;
                string key = Key(work);
                work();
                Builds++;
                WorstBuildMs = Math.Max(WorstBuildMs, b.WorkMs);
                if (Factor <= 0) BuildMs[key] = b.WorkMs;
            }

            private static string Key(Action work)
            {
                return ((TileBuild)work.Target).Node.ToString();
            }
        }

        /// <summary>The main-thread side: copies every chunk as a mesh upload would (the vertex streams and 16-bit
        /// indices into a staging buffer), and runs the indexing a shown detail tile triggers.</summary>
        private sealed class RideSink : ITileSink
        {
            public readonly HashSet<SelectedNode> Visible = new HashSet<SelectedNode>();
            private readonly Dictionary<SelectedNode, TileExtras> _extras = new Dictionary<SelectedNode, TileExtras>();
            private double _t0;
            private float[] _f = new float[1 << 18];
            private byte[] _b = new byte[1 << 18];
            private ushort[] _s = new ushort[1 << 18];

            // The indexes the game's tile-shown handlers build (WorldRoot.OnDetailShown and its subscribers).
            private readonly SacredZoneIndex _zones = new SacredZoneIndex();
            private readonly AreaTypeGrid _areaTypes = new AreaTypeGrid();
            private readonly PlaceAudioIndex _audio = new PlaceAudioIndex();
            private readonly TileGroundQuery _ground;
            private readonly CuratedDb _db;
            private readonly Func<TileData, Ghumante.Core.Meshing.Roads.IRoadCorridorQuery> _corridors;

            public long FrameBytes;
            public int FrameChunks;
            public double CopyMs;

            /// <summary>What a shown detail tile still costs the main thread in the merged game: the sacred zones
            /// (LifeHost, one tile per Tick in perf-life's branch), the audio index joining a prepared tile, the heroes'
            /// colliders and the static set's link.</summary>
            public double MainMs;

            /// <summary>What used to run on the main thread when a tile showed and now runs on workers (timed here, on
            /// this thread, to show what it cost before): area types (perf-life's build job), the audio index, the static
            /// colliders' index (the tile build) and the fauna ground.</summary>
            public double MovedMs, AreaTypesMs, AudioPrepareMs, StaticPrepareMs, FaunaMs;
            public int Shown;

            /// <summary>Bytes the copy buffers and the handlers allocated this frame (not the scheduler's).</summary>
            public long HandlerAllocBytes;

            private long _a0;

            public RideSink(TileGroundQuery ground, CuratedDb db, Func<TileData, Ghumante.Core.Meshing.Roads.IRoadCorridorQuery> corridors)
            {
                _ground = ground;
                _db = db;
                _corridors = corridors;
            }

            public void BeginFrame()
            {
                FrameBytes = 0;
                FrameChunks = 0;
                CopyMs = MainMs = MovedMs = AreaTypesMs = AudioPrepareMs = StaticPrepareMs = FaunaMs = 0;
                HandlerAllocBytes = 0;
            }

            public void Upload(TileBuild build, int chunk)
            {
                _a0 = GC.GetAllocatedBytesForCurrentThread();
                _t0 = ThreadClock.Ms();
                UploadChunk c = build.Chunks[chunk];
                MeshData m = build.Layers[c.Layer];
                int n = c.VertexCount;
                if (_f.Length < n * 3) _f = new float[n * 3];
                if (_b.Length < n * 4) _b = new byte[n * 4];
                Array.Copy(m.Positions, c.FirstVertex * 3, _f, 0, n * 3);
                Array.Copy(m.Normals, c.FirstVertex * 3, _f, 0, n * 3);
                Array.Copy(m.Colors, c.FirstVertex * 4, _b, 0, n * 4);
                if (m.HasUv0) Array.Copy(m.Uv0, c.FirstVertex * 2, _f, 0, n * 2);
                if (c.UsesShortIndices)
                {
                    if (_s.Length < c.IndexCount) _s = new ushort[c.IndexCount];
                    Array.Copy(build.ShortIndices[c.Layer], c.FirstIndex, _s, 0, c.IndexCount);
                }
                CopyMs += ThreadClock.Ms() - _t0;
                HandlerAllocBytes += GC.GetAllocatedBytesForCurrentThread() - _a0;
                FrameBytes += build.ChunkBytes(chunk);
                FrameChunks++;
            }

            public void SetVisible(SelectedNode node, bool visible)
            {
                if (!visible)
                {
                    Visible.Remove(node);
                    if (node.DrawsDetail)
                    {
                        _a0 = GC.GetAllocatedBytesForCurrentThread();
                        _zones.RemoveTile(node.Area);
                        _areaTypes.RemoveTile(node.Area);
                        _audio.RemoveTile(node.Area);
                        _ground.Unregister(node.Area.Key);
                        _ground.Unregister(node.Area.Key | TileGroundQuery.StaticSetKeyBit);
                        HandlerAllocBytes += GC.GetAllocatedBytesForCurrentThread() - _a0;
                    }
                    return;
                }
                Visible.Add(node);
                TileExtras e;
                if (!node.DrawsDetail || !_extras.TryGetValue(node, out e) || e.Source == null) return;
                Shown++;
                _a0 = GC.GetAllocatedBytesForCurrentThread();
                // Main thread. LifeHost: the tile's sacred zones (perf-life joins them one tile per Tick).
                _t0 = ThreadClock.Ms();
                _zones.AddTile(node.Area, e.Source, _db);
                MainMs += ThreadClock.Ms() - _t0;
                // Moved. LifeHost's area types (perf-life's background build job).
                _t0 = ThreadClock.Ms();
                _areaTypes.AddTile(node.Area, e.Source);
                AreaTypesMs += ThreadClock.Ms() - _t0;
                // Moved, then main. WorldAudioPlace: the tile indexed on a worker, joined on the main thread.
                _t0 = ThreadClock.Ms();
                PlaceAudioIndex.PreparedTile audio = PlaceAudioIndex.Prepare(node.Area, e.Source);
                AudioPrepareMs += ThreadClock.Ms() - _t0;
                _t0 = ThreadClock.Ms();
                _audio.AddTile(audio);
                MainMs += ThreadClock.Ms() - _t0;
                // Moved. The static dressing set's index, made by the tile build (TileExtras.PreparedColliders); timed
                // again here for the record.
                if (e.Colliders != null && e.Colliders.Count > 0)
                {
                    _t0 = ThreadClock.Ms();
                    TileGroundQuery.PrepareColliders(node.Area.Key | TileGroundQuery.StaticSetKeyBit, e.Colliders);
                    StaticPrepareMs += ThreadClock.Ms() - _t0;
                }
                // Main thread. WorldStreamer.RegisterColliders: the heroes' set, and the static set's link (RegisterStatic).
                _t0 = ThreadClock.Ms();
                if (e.HeroColliders != null && e.HeroColliders.Count > 0) _ground.Register(node.Area.Key, e.HeroColliders);
                if (e.PreparedColliders != null) _ground.Register(e.PreparedColliders);
                else if (e.Colliders != null && e.Colliders.Count > 0) _ground.Register(node.Area.Key | TileGroundQuery.StaticSetKeyBit, e.Colliders);
                MainMs += ThreadClock.Ms() - _t0;
                // Moved. WildlifeDirector.OnShown: the fauna ground (perf-roads: a worker thread).
                _t0 = ThreadClock.Ms();
                new FaunaGroundIndex { CorridorFactory = _corridors }.Add(e.Source);
                FaunaMs += ThreadClock.Ms() - _t0;
                MovedMs = AreaTypesMs + AudioPrepareMs + StaticPrepareMs + FaunaMs;
                HandlerAllocBytes += GC.GetAllocatedBytesForCurrentThread() - _a0;
            }

            public void Release(SelectedNode node)
            {
                if (Visible.Contains(node)) SetVisible(node, false);
                _extras.Remove(node);
            }

            public void Ready(SelectedNode node, TileExtras extras)
            {
                _extras[node] = extras;
            }
        }

        internal sealed class RideResult
        {
            public int Tier, Frames, Builds, Shown;
            public double Fps, LengthM;
            public double WorstTickMs, WorstSchedulerMs, P99SchedulerMs, WorstCopyMs, WorstBuildMs, WorstWorkMs;

            /// <summary>Worst frame of the scheduler, the upload copy and the shown-tile handlers left on the main thread.</summary>
            public double WorstMainMs;

            /// <summary>The 99.9th percentile of the scheduler and upload copy per frame (the worst frames but a handful,
            /// so a single frame whose caches a co-running process on this shared machine flushed does not decide).</summary>
            public double P999WorkMs;
            public long WorstBytes;
            public int WorstChunks;
            public long WorstAllocBytes;
            public int GcFrames;
            public double WorstGcFrameMs;
            public double WorstMainHandlersMs, WorstMovedMs, WorstAreaTypesMs, WorstAudioPrepareMs, WorstStaticPrepareMs, WorstFaunaMs;
            public double NearestGapM = double.PositiveInfinity;
            public int GapFrames;
            public string FirstGap, WorstFrame;

            public string Line()
            {
                return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{0,-4} {1} fps, {2:0} m in {3} frames, {4} builds (worst {5:0} ms desktop), {6} detail tiles shown | per frame worst: " +
                    "scheduler + upload copy {20:0.00} ms (about {21:0.0} ms on the tier's phones; 99.9th percentile {26:0.00} ms, about {27:0.0}), " +
                    "with the shown-tile handlers {24:0.00} ms " +
                    "(about {25:0.0} ms), " +
                    "scheduler {7:0.00} ms (p99 {8:0.00}; {17} frames paused by a GC, worst {18:0.0} ms), allocated {19} B, upload {9:0.000} MB in {10} chunks (copy {11:0.00} ms) | on show, worst: " +
                    "still on the main thread {12:0.00} ms (zones, audio join, colliders); moved to workers {13:0.00} ms (area types {22:0.00}, " +
                    "audio index {23:0.00}, static colliders {14:0.00}, fauna ground {15:0.00}) | " +
                    "nearest undrawn route street {16}",
                    TierName[Tier], Fps, LengthM, Frames, Builds, WorstBuildMs, Shown, WorstSchedulerMs, P99SchedulerMs, WorstBytes / 1048576.0,
                    WorstChunks, WorstCopyMs, WorstMainHandlersMs, WorstMovedMs, WorstStaticPrepareMs, WorstFaunaMs,
                    double.IsInfinity(NearestGapM) ? "none" : NearestGapM.ToString("0") + " m (" + GapFrames + " frames; first " + FirstGap + ")",
                    GcFrames, WorstGcFrameMs, WorstAllocBytes, WorstWorkMs, WorstWorkMs * MainThreadFactor[Tier], WorstAreaTypesMs, WorstAudioPrepareMs,
                    WorstMainMs, WorstMainMs * MainThreadFactor[Tier], P999WorkMs, P999WorkMs * MainThreadFactor[Tier]);
            }
        }

        private static CuratedDb _db;

        private static CuratedDb Db()
        {
            if (_db == null)
                _db = CuratedDb.Read(File.ReadAllBytes(Path.Combine(GoldenFiles.RepoRoot, "shared", "sample-regions", "kathmandu_core",
                                                                    "kathmandu_core.curated.ghcd")));
            return _db;
        }

        /// <summary>
        /// One ride at the tier's frame rate (Low and Mid 30 fps, High 60). The scheduler first settles at Thamel Chowk (the
        /// arrival screen), then the rider moves along the route every frame. Build times come from
        /// <paramref name="buildMs"/> (desktop milliseconds per node) times <paramref name="factor"/>; with factor 0 builds
        /// land the frame after they were dispatched and their desktop times are recorded into <paramref name="buildMs"/>.
        /// Streets of the route within <paramref name="checkM"/> of the rider (along the route, both ways) must be drawn:
        /// their level-10 tile visible as a detail node.
        /// </summary>
        internal static RideResult Ride(int tier, double factor, Dictionary<string, double> buildMs, double checkM, StreamingConfig config = null)
        {
            List<double[]> route = Route();
            List<double[]> pts = Resample(route, 5.0);
            double length = pts[pts.Count - 1][2];
            StreamingConfig cfg = config ?? StreamingConfig.ForTier(tier);
            MeshingSettings meshing = StreamingScene.Settings(tier, Ghumante.Core.Meshing.BiomePalette.DefaultMonth);
            TileGroundQuery ground = StreamingScene.Ground(meshing);
            TileSelector selector = TileSelector.ForPack(cfg, StreamingSampleRegion.Pack);
            var sink = new RideSink(ground, Db(), meshing.Corridors);
            var workers = new VirtualWorkers(buildMs, factor);
            double fps = tier == StreamingConfig.TierHigh ? 60 : 30;
            var detailTiles = new Dictionary<ulong, bool>();
            var visibleDetail = new HashSet<ulong>();
            var r = new RideResult { Tier = tier, Fps = fps, LengthM = length };
            var schedulerMs = new List<double>();
            var workMs = new List<double>();
            using (var s = new StreamingScheduler(StreamingSampleRegion.Pack, cfg, selector, ground, sink, workers,
                                                  ThreadClock.Ms, meshing))
            {
                s.UploadBudgetMs = fps >= 50 ? 1.5 : 2.0;
                // Arrival: settle at the start (the loading screen), unmeasured.
                double t = 0;
                for (int i = 0; i < 20000 && !s.IsSettled; i++)
                {
                    s.Tick(pts[0][0], pts[0][1]);
                    workers.PickUp(0, true);
                }
                Assert.That(s.IsSettled, Is.True, "settles at Thamel Chowk: " + s.Stats.Format());
                int seg = 0;
                for (int frame = 0; ; frame++)
                {
                    t = frame / fps;
                    double along = SpeedMps * t;
                    if (along > length) break;
                    while (seg + 1 < pts.Count && pts[seg + 1][2] <= along) seg++;
                    double[] a = pts[seg], b = pts[Math.Min(seg + 1, pts.Count - 1)];
                    double f = b[2] > a[2] ? (along - a[2]) / (b[2] - a[2]) : 0;
                    double x = a[0] + (b[0] - a[0]) * f, z = a[1] + (b[1] - a[1]) * f;

                    workers.Complete(t);
                    sink.BeginFrame();
                    int gc = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
                    long alloc0 = GC.GetAllocatedBytesForCurrentThread();
                    double t0 = ThreadClock.Ms();
                    s.Tick(x, z);
                    double tick = ThreadClock.Ms() - t0;
                    long alloc = GC.GetAllocatedBytesForCurrentThread() - alloc0 - sink.HandlerAllocBytes;
                    bool collected = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2) != gc;
                    workers.PickUp(t);

                    double handlers = sink.MainMs + sink.MovedMs;
                    double sched = tick - sink.CopyMs - handlers;
                    r.WorstAllocBytes = Math.Max(r.WorstAllocBytes, alloc);
                    r.Frames++;
                    if (collected)
                    {
                        // A collection paused this thread for the workers' garbage (the builds allocate): reported apart,
                        // since it is not the main thread's own work.
                        r.GcFrames++;
                        r.WorstGcFrameMs = Math.Max(r.WorstGcFrameMs, sched);
                        sched = 0;
                    }
                    schedulerMs.Add(sched);
                    if (!collected)
                    {
                        workMs.Add(sched + sink.CopyMs);
                        r.WorstWorkMs = Math.Max(r.WorstWorkMs, sched + sink.CopyMs);
                        r.WorstMainMs = Math.Max(r.WorstMainMs, sched + sink.CopyMs + sink.MainMs);
                    }
                    if (sched > r.WorstSchedulerMs)
                        r.WorstFrame = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            "frame {0} at {1:0} m: scheduler {2:0.00} ms (select {9:0.00}, collect {10:0.00}, resolve {11:0.00} of which ground {14:0.00} and handlers {15:0.00}, dispatch {12:0.00}{13}), " +
                            "copy {3:0.00} ms for {4} chunks, {5} selections so far, {6} visible, {7} queued, {8} pending uploads",
                            frame, along, sched, sink.CopyMs, sink.FrameChunks, s.Stats.Selections, s.Stats.Visible, s.Stats.Queued, s.Stats.PendingUploads,
                            s.Stats.SelectMs, s.Stats.CollectMs, s.Stats.ResolveMs, s.Stats.DispatchMs, collected ? ", a GC ran" : "", s.Stats.GroundMs, handlers);
                    r.WorstTickMs = Math.Max(r.WorstTickMs, tick - sink.MovedMs);
                    r.WorstSchedulerMs = Math.Max(r.WorstSchedulerMs, sched);
                    r.WorstCopyMs = Math.Max(r.WorstCopyMs, sink.CopyMs);
                    if (sink.FrameBytes > r.WorstBytes)
                    {
                        r.WorstBytes = sink.FrameBytes;
                        r.WorstChunks = sink.FrameChunks;
                    }
                    r.WorstMainHandlersMs = Math.Max(r.WorstMainHandlersMs, sink.MainMs);
                    r.WorstMovedMs = Math.Max(r.WorstMovedMs, sink.MovedMs);
                    r.WorstAreaTypesMs = Math.Max(r.WorstAreaTypesMs, sink.AreaTypesMs);
                    r.WorstAudioPrepareMs = Math.Max(r.WorstAudioPrepareMs, sink.AudioPrepareMs);
                    r.WorstStaticPrepareMs = Math.Max(r.WorstStaticPrepareMs, sink.StaticPrepareMs);
                    r.WorstFaunaMs = Math.Max(r.WorstFaunaMs, sink.FaunaMs);

                    // Streets near the rider: drawn when their level-10 tile shows as a detail node.
                    visibleDetail.Clear();
                    foreach (SelectedNode n in sink.Visible)
                        if (n.DrawsDetail) visibleDetail.Add(n.Area.Key);
                    double gap = double.PositiveInfinity;
                    for (int i = 0; i < pts.Count; i++)
                    {
                        double d = Math.Abs(pts[i][2] - along);
                        if (d > checkM) continue;
                        TileId tile = TileId.At(10, pts[i][0], pts[i][1]);
                        bool hasDetail;
                        if (!detailTiles.TryGetValue(tile.Key, out hasDetail))
                        {
                            hasDetail = StreamingSampleRegion.Pack.Contains(tile) && StreamingSampleRegion.Tile(tile).HasDetail;
                            detailTiles[tile.Key] = hasDetail;
                        }
                        if (!hasDetail || visibleDetail.Contains(tile.Key)) continue;
                        gap = Math.Min(gap, d);
                    }
                    if (!double.IsInfinity(gap))
                    {
                        if (r.GapFrames == 0) r.FirstGap = "frame " + frame + " at " + along.ToString("0") + " m, " + gap.ToString("0") + " m away";
                        r.GapFrames++;
                        r.NearestGapM = Math.Min(r.NearestGapM, gap);
                    }
                }
                Assert.That(s.Stats.Failed, Is.EqualTo(0));
            }
            schedulerMs.Sort();
            r.P99SchedulerMs = schedulerMs[(int)(0.99 * (schedulerMs.Count - 1))];
            workMs.Sort();
            r.P999WorkMs = workMs.Count > 0 ? workMs[(int)(0.999 * (workMs.Count - 1))] : 0;
            r.Builds = workers.Builds;
            r.WorstBuildMs = workers.WorstBuildMs;
            r.Shown = sink.Shown;
            return r;
        }

        private static readonly Dictionary<int, Dictionary<string, double>> BuildTimes = new Dictionary<int, Dictionary<string, double>>();

        /// <summary>Desktop build times per node of the tier's ride (from a ride whose builds land at once).</summary>
        private static Dictionary<string, double> TimesFor(int tier, out RideResult instant)
        {
            var times = new Dictionary<string, double>();
            instant = Ride(tier, 0, times, BandConfig.ForTier(tier).B1OuterM);
            BuildTimes[tier] = times;
            return times;
        }

        [Test]
        public void RideThroughThamelAsanAndNewRoadStaysWithinTheFrameBudget([Values(0, 1, 2)] int tier)
        {
            float nearM = BandConfig.ForTier(tier).B1OuterM;
            RideResult warm;
            Dictionary<string, double> times = TimesFor(tier, out warm); // also compiles every path once (no JIT below)
            RideResult instant = Ride(tier, 0, new Dictionary<string, double>(), nearM);
            TestContext.WriteLine("instant builds: " + instant.Line());
            TestContext.WriteLine("  worst frame: " + instant.WorstFrame);
            RideResult phone = Ride(tier, PhoneFactor, times, nearM);
            TestContext.WriteLine("phone builds:   " + phone.Line());
            TestContext.WriteLine("  worst frame: " + phone.WorstFrame);
            double budgetMs = tier == StreamingConfig.TierHigh ? 1.5 : 2.0; // W2_DESIGN 10.4: streaming upload per frame
            foreach (RideResult r in new[] { instant, phone })
            {
                Assert.That(r.WorstBytes, Is.LessThanOrEqualTo(1L << 20), "a frame uploads at most 1 MiB of tile meshes");
                Assert.That(r.P999WorkMs * MainThreadFactor[tier], Is.LessThanOrEqualTo(budgetMs),
                            "the worst frames' streaming work (scheduler and upload copy, CPU ms of this thread) on the tier's phones");
                Assert.That(r.WorstWorkMs * MainThreadFactor[tier], Is.LessThanOrEqualTo(budgetMs * 1.5),
                            "no single frame far over it either");
                Assert.That(r.NearestGapM, Is.GreaterThan(nearM), "every street of the route within the B1 radius (" + nearM + " m) is drawn");
            }
        }

        /// <summary>
        /// The repro of the pop-in, kept as a guard on the reasoning behind Low's detail radius: with the 100 m it had until
        /// the playtest-fix pass and builds as slow as a phone's, the rider reaches streets whose tile still draws terrain
        /// only (no roads, no houses) well inside the B1 radius, and they pop in around the rider. If this ever stops
        /// failing, builds got fast enough to revisit <see cref="StreamingConfig.DetailRadiusM"/> on Low.
        /// </summary>
        [Test]
        public void TheOldLowDetailRadiusLetsStreetsPopInAroundTheRider()
        {
            float nearM = BandConfig.ForTier(StreamingConfig.TierLow).B1OuterM;
            StreamingConfig old = StreamingConfig.ForTier(StreamingConfig.TierLow);
            old.DetailRadiusM = 100;
            var times = new Dictionary<string, double>();
            Ride(StreamingConfig.TierLow, 0, times, nearM, old);
            RideResult phone = Ride(StreamingConfig.TierLow, PhoneFactor, times, nearM, old);
            TestContext.WriteLine("Low with the old 100 m detail radius, phone builds: " + phone.Line());
            Assert.That(phone.NearestGapM, Is.LessThan(nearM), "streets within the B1 radius are drawn as bare terrain for a while");
        }
    }
}
