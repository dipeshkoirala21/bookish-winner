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
    /// a build takes its measured desktop CPU time times a phone factor, so tiles arrive as late as they would on a phone.
    /// Per frame the harness records the scheduler's main-thread milliseconds, the mesh bytes it uploads (the copy a phone
    /// pays for) and the handlers' milliseconds, and switches every 128 m road block of the detail views exactly as
    /// <c>WorldStreamer.UpdateBands</c> does (<see cref="RoadBlockWatch"/>: the full kit within the B1 radius, the cheap
    /// copy out to B2). It checks that every street of the route within the B1 radius is drawn, and that no road block
    /// appears late or vanishes within the B1 radius of the rider: no road pops in or out near the player.
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

            /// <summary>When positive, every detail node's build takes this many desktop milliseconds (times
            /// <see cref="Factor"/>) whatever was measured: a phone slower than the factor says (a build on an
            /// efficiency core, a throttled phone).</summary>
            public double DetailBuildMs;

            /// <summary>When each build was picked up and is due (s), for the timeline of the detail tiles.</summary>
            public readonly Dictionary<SelectedNode, double> PickedAt = new Dictionary<SelectedNode, double>(), DueAt = new Dictionary<SelectedNode, double>();

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
                    SelectedNode node = ((TileBuild)work.Target).Node;
                    PickedAt[node] = nowS;
                    if (Factor <= 0 || instant)
                    {
                        DueAt[node] = nowS;
                        Execute(work);
                        k--;
                        continue;
                    }
                    double ms;
                    if (DetailBuildMs > 0 && node.DrawsDetail) ms = DetailBuildMs;
                    else if (!BuildMs.TryGetValue(Key(work), out ms)) ms = 200;
                    double due = nowS + ms * Factor / 1000.0;
                    DueAt[node] = due;
                    _busyUntil[k] = due;
                    _running.Add(new KeyValuePair<double, Action>(due, work));
                }
            }

            /// <summary>Runs a build here. Its desktop time is the CPU time of this thread (a build is single-threaded),
            /// so a busy shared machine that deschedules the thread does not make the phone builds slower.</summary>
            private void Execute(Action work)
            {
                string key = Key(work);
                double c0 = ThreadClock.Ms();
                work();
                double ms = ThreadClock.Ms() - c0;
                Builds++;
                WorstBuildMs = Math.Max(WorstBuildMs, ms);
                if (Factor <= 0) BuildMs[key] = ms;
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

            /// <summary>Of <see cref="MainMs"/>: the sacred zones, the audio join and the colliders' registration.</summary>
            public double ZonesMs, AudioJoinMs, CollidersMs;

            /// <summary>The rider this frame (the ride sets it before the tick), and when each detail node showed or hid
            /// (distance along the route, and from the rider to the node's area).</summary>
            public double NowAlong, NowX, NowZ;

            public readonly Dictionary<SelectedNode, double[]> ShownAt = new Dictionary<SelectedNode, double[]>();
            public readonly Dictionary<SelectedNode, double[]> HiddenAt = new Dictionary<SelectedNode, double[]>();

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
                ZonesMs = AudioJoinMs = CollidersMs = 0;
                HandlerAllocBytes = 0;
            }

            public void Upload(TileBuild build, int chunk)
            {
                UploadChunk c = build.Chunks[chunk];
                if (c.Layer == TileLayers.Roads) Roads.Add(build.Node, c.Part, chunk == 0);
                else if (chunk == 0) Roads.Add(build.Node, -1, true);
                _a0 = GC.GetAllocatedBytesForCurrentThread();
                _t0 = ThreadClock.Ms();
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
                if (node.DrawsDetail)
                {
                    double[] when = { NowAlong, Math.Sqrt(TileArea.ClosestDistanceSquared(node.Area, NowX, NowZ)) };
                    if (visible && !ShownAt.ContainsKey(node)) ShownAt[node] = when;
                    if (!visible) HiddenAt[node] = when;
                }
                if (node.DrawsDetail) Roads.SetVisible(node, visible, NowAlong, NowX, NowZ);
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
                ZonesMs += ThreadClock.Ms() - _t0;
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
                AudioJoinMs += ThreadClock.Ms() - _t0;
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
                CollidersMs += ThreadClock.Ms() - _t0;
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
                Roads.Release(node);
            }

            /// <summary>The road blocks of the detail views, switched as <c>WorldStreamer.UpdateBands</c> switches them.</summary>
            public readonly RoadBlockWatch Roads = new RoadBlockWatch();

            public void Ready(SelectedNode node, TileExtras extras)
            {
                _extras[node] = extras;
            }
        }

        /// <summary>
        /// The road blocks of every detail view, switched as the game switches them (<c>WorldStreamer.UpdateBands</c>
        /// through <see cref="RoadBlocks.UpdateMasks"/> and <see cref="RoadBlocks.PartShown"/>, after the tick of the same
        /// frame): each 128 m block with road shows its full kit, its cheap far copy or nothing. Every switch between
        /// "some road" and "no road" is recorded with the rider's distance to the block: a block whose road appears (its
        /// view shown, or the far radius reached) pops in, one whose road disappears (its view hidden or released, or the
        /// far radius left) vanishes. A block within the near radius that does not show its full kit is a near-kit miss.
        /// </summary>
        internal sealed class RoadBlockWatch
        {
            private sealed class View
            {
                public readonly HashSet<int> Parts = new HashSet<int>();
                public readonly List<int> Blocks = new List<int>();
                public readonly Dictionary<int, int> State = new Dictionary<int, int>(); // 0 none, 1 cheap copy, 2 full kit
                public ulong Near, Far;
                public bool HasFar, Visible;
                public double X0, Z0, Size;
            }

            private readonly Dictionary<SelectedNode, View> _views = new Dictionary<SelectedNode, View>();

            /// <summary>The tier's road radii (<see cref="RoadBlocks.Radii"/>).</summary>
            public float NearM, FarM;

            /// <summary>Set once the ride is over: the scheduler's disposal hides every view, which is no switch the
            /// rider sees.</summary>
            public bool Frozen;

            /// <summary>The nearest block whose road appeared late (closer than the radius it is due at: the far radius
            /// for a block with a cheap copy, the near radius for one whose road is all trails, less one metre for the
            /// frame step), and the nearest whose road disappeared.</summary>
            public double NearestPopInM = double.PositiveInfinity, NearestVanishM = double.PositiveInfinity;
            public int PopIns, Vanishes, NearKitMisses, Switches;
            public string FirstPopIn, FirstVanish, FirstNearKitMiss;

            /// <summary>Road blocks drawn this frame (full kit and cheap copy) and the most of any frame.</summary>
            public int ShownFull, ShownFar, MostFull, MostFar;

            /// <summary>A chunk of <paramref name="node"/>'s build was uploaded (<paramref name="part"/> of the road layer,
            /// or -1 for another layer; <paramref name="first"/> for the build's first chunk, which starts its parts afresh).</summary>
            public void Add(SelectedNode node, int part, bool first)
            {
                View v;
                if (!_views.TryGetValue(node, out v))
                {
                    v = new View { X0 = node.Area.X0, Z0 = node.Area.Z0, Size = node.Area.Size };
                    _views[node] = v;
                }
                else if (first)
                {
                    v.Parts.Clear();
                    v.Blocks.Clear();
                    v.State.Clear();
                    v.HasFar = false;
                    v.Near = v.Far = 0;
                }
                if (part < 0) return;
                v.Parts.Add(part);
                if (RoadBlocks.IsFar(part)) v.HasFar = true;
                int b = RoadBlocks.BlockOfPart(part);
                if (!v.Blocks.Contains(b)) v.Blocks.Add(b);
            }

            public void SetVisible(SelectedNode node, bool visible, double along, double x, double z)
            {
                View v;
                if (Frozen || !_views.TryGetValue(node, out v) || v.Visible == visible) return;
                v.Visible = visible;
                if (visible) return; // the frame's Update decides what it shows, as UpdateView does after the tick
                for (int i = 0; i < v.Blocks.Count; i++)
                {
                    int b = v.Blocks[i], prev;
                    if (!v.State.TryGetValue(b, out prev) || prev == 0) continue;
                    Vanish(node, b, Distance(v, b, x - v.X0, z - v.Z0), along, "view hidden");
                    v.State[b] = 0;
                }
            }

            public void Release(SelectedNode node)
            {
                _views.Remove(node);
            }

            /// <summary>The road blocks drawn now: node, block and copy (1 the cheap copy, 2 the full kit).</summary>
            public List<Tuple<SelectedNode, int, int>> Snapshot()
            {
                var o = new List<Tuple<SelectedNode, int, int>>();
                foreach (KeyValuePair<SelectedNode, View> kv in _views)
                {
                    if (!kv.Value.Visible) continue;
                    foreach (KeyValuePair<int, int> s in kv.Value.State)
                        if (s.Value > 0) o.Add(Tuple.Create(kv.Key, s.Key, s.Value));
                }
                return o;
            }

            /// <summary>Forgets the switches so far (the arrival screen's).</summary>
            public void ResetEvents()
            {
                NearestPopInM = NearestVanishM = double.PositiveInfinity;
                PopIns = Vanishes = NearKitMisses = Switches = 0;
                FirstPopIn = FirstVanish = FirstNearKitMiss = null;
                MostFull = MostFar = 0;
            }

            /// <summary>One frame with the rider at (<paramref name="x"/>, <paramref name="z"/>).</summary>
            public void Update(double along, double x, double z)
            {
                ShownFull = ShownFar = 0;
                foreach (KeyValuePair<SelectedNode, View> kv in _views)
                {
                    View v = kv.Value;
                    if (!v.Visible) continue;
                    double cx = x - v.X0, cz = z - v.Z0;
                    RoadBlocks.UpdateMasks(v.Size, cx, cz, NearM, FarM, ref v.Near, ref v.Far);
                    for (int i = 0; i < v.Blocks.Count; i++)
                    {
                        int b = v.Blocks[i];
                        bool full = v.Parts.Contains(b) && RoadBlocks.PartShown(b, v.Near, v.Far, v.HasFar, v.Size, cx, cz, NearM, FarM);
                        int fp = RoadBlocks.FarPart + b;
                        bool far = v.Parts.Contains(fp) && RoadBlocks.PartShown(fp, v.Near, v.Far, v.HasFar, v.Size, cx, cz, NearM, FarM);
                        int state = full ? 2 : far ? 1 : 0, prev;
                        v.State.TryGetValue(b, out prev);
                        double d = Distance(v, b, cx, cz);
                        if (prev == 0 && state > 0)
                        {
                            Switches++;
                            double due = v.Parts.Contains(fp) ? FarM : NearM;
                            if (d < due - 1.0) // closer than the radius it is due at: its tile came late
                            {
                                PopIns++;
                                if (d < NearestPopInM) FirstPopIn = Describe(kv.Key, b, d, along) + " (due at " + due.ToString("0") + " m)";
                                NearestPopInM = Math.Min(NearestPopInM, d);
                            }
                        }
                        else if (prev > 0 && state == 0)
                        {
                            Vanish(kv.Key, b, d, along, "far radius left");
                        }
                        else if (prev != state) Switches++;
                        if (d <= NearM && state != 2)
                        {
                            NearKitMisses++;
                            if (FirstNearKitMiss == null) FirstNearKitMiss = Describe(kv.Key, b, d, along) + " shows " + (state == 1 ? "the cheap copy" : "nothing");
                        }
                        if (state == 2) ShownFull++;
                        else if (state == 1) ShownFar++;
                        v.State[b] = state;
                    }
                }
                MostFull = Math.Max(MostFull, ShownFull);
                MostFar = Math.Max(MostFar, ShownFar);
            }

            private void Vanish(SelectedNode node, int block, double d, double along, string why)
            {
                Switches++;
                if (d <= FarM)
                {
                    Vanishes++;
                    if (d < NearestVanishM) FirstVanish = Describe(node, block, d, along) + " (" + why + ")";
                }
                NearestVanishM = Math.Min(NearestVanishM, d);
            }

            private static double Distance(View v, int block, double cx, double cz)
            {
                double x0, z0, x1, z1;
                RoadBlocks.Square(block, v.Size, out x0, out z0, out x1, out z1);
                return BandConfig.NearestDistance(x0, z0, x1, z1, cx, cz);
            }

            private static string Describe(SelectedNode node, int block, double d, double along)
            {
                return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} block {1} at {2:0} m along, {3:0} m away", node, block, along, d);
            }
        }

        internal sealed class RideResult
        {
            public int Tier, Frames, Builds, Shown;

            /// <summary>Build objects the scheduler created during the ride, after the arrival screen
            /// (<see cref="StreamingStats.BuildsCreated"/>), and before it.</summary>
            public int BuildsCreatedRiding, BuildsCreatedArriving;

            /// <summary>Worst per frame of the main-thread shown-tile handlers, each: the sacred zones, the audio join and
            /// the colliders' registration (ms).</summary>
            public double WorstZonesMs, WorstAudioJoinMs, WorstCollidersMs;
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
            public string FirstGap, WorstFrame, WorstAllocFrame;

            /// <summary>The frame of <see cref="WorstMainMs"/>, phase by phase.</summary>
            public string WorstMainFrame;

            /// <summary>The road blocks over the ride (<see cref="RoadBlockWatch"/>): the nearest block whose road appeared
            /// or disappeared (m from the rider), how many did so inside the far radius, the frames x blocks within the
            /// near radius without their full kit, and the most blocks drawn at once with each copy.</summary>
            public double NearestRoadPopInM = double.PositiveInfinity, NearestRoadVanishM = double.PositiveInfinity;
            public int RoadPopIns, RoadVanishes, RoadNearKitMisses, RoadSwitches, MostFullBlocks, MostFarBlocks;
            public string FirstRoadPopIn, FirstRoadVanish, FirstRoadNearKitMiss;

            public string RoadLine()
            {
                return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "road blocks: {0} switches; nearest appearing late {1}, nearest disappearing {2}; {3} appeared late and {4} " +
                    "disappeared inside the far radius (nearest: {5}; {6}); {7} near-radius misses ({8}); at most {9} full-kit and {10} cheap blocks drawn",
                    RoadSwitches, Metres(NearestRoadPopInM), Metres(NearestRoadVanishM), RoadPopIns, RoadVanishes,
                    FirstRoadPopIn ?? "none", FirstRoadVanish ?? "none", RoadNearKitMisses, FirstRoadNearKitMiss ?? "none",
                    MostFullBlocks, MostFarBlocks);
            }

            private static string Metres(double m)
            {
                return double.IsInfinity(m) ? "none" : m.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " m";
            }

            /// <summary>Each detail node of the ride: when it was first selected (m along the route, and the rider's
            /// distance to it), picked up by a worker and due (s), and shown and hidden (m along, distance).</summary>
            public readonly List<string> Timeline = new List<string>();

            /// <summary>Per frame: metres along the route, the scheduler and upload copy (CPU ms of this thread, -1 for a
            /// frame a GC paused), the shown-tile handlers left on the main thread (ms), and the bytes uploaded.</summary>
            public readonly List<float> FrameAlong = new List<float>(), FrameWorkMs = new List<float>(), FrameHandlerMs = new List<float>();
            public readonly List<long> FrameBytes = new List<long>();

            /// <summary>Per frame: chunks uploaded and whether the selection ran (with <see cref="FrameBytes"/>, what a
            /// frame did, to pair the frames of two rides that did the same work, <see cref="Merge"/>).</summary>
            public readonly List<int> FrameChunks = new List<int>();

            public readonly List<bool> FrameSelected = new List<bool>();

            /// <summary>Frames of the two rides of a <see cref="Merge"/> that did different work (taken at their worse
            /// time), and the worst merged frame.</summary>
            public int UnpairedFrames;

            public string WorstMergedFrame;

            /// <summary>
            /// The timing of two runs of the same ride, frame by frame: a frame that did the same work in both (the same
            /// chunks and bytes, the selection or not) counts at the faster of its two times, so a frame that another
            /// process on this shared 4-core machine slowed down (its caches flushed, its core shared) does not decide the
            /// worst frame unless it was slow both times; a frame that did different work counts at the slower. Every
            /// other figure is <paramref name="a"/>'s. Both rides are deterministic (virtual build times, the same route,
            /// uploads limited by bytes only), so every frame pairs.
            /// </summary>
            public static RideResult Merge(RideResult a, RideResult b)
            {
                Assert.That(b.Frames, Is.EqualTo(a.Frames), "the same ride twice");
                a.WorstWorkMs = a.WorstMainMs = 0;
                a.UnpairedFrames = 0;
                var work = new List<double>();
                for (int i = 0; i < a.FrameWorkMs.Count; i++)
                {
                    bool paired = a.FrameBytes[i] == b.FrameBytes[i] && a.FrameChunks[i] == b.FrameChunks[i] && a.FrameSelected[i] == b.FrameSelected[i];
                    if (!paired) a.UnpairedFrames++;
                    float wa = a.FrameWorkMs[i], wb = b.FrameWorkMs[i]; // -1: a GC paused that frame
                    float w = wa < 0 ? wb : wb < 0 ? wa : paired ? Math.Min(wa, wb) : Math.Max(wa, wb);
                    float h = paired ? Math.Min(a.FrameHandlerMs[i], b.FrameHandlerMs[i]) : Math.Max(a.FrameHandlerMs[i], b.FrameHandlerMs[i]);
                    a.FrameWorkMs[i] = w;
                    a.FrameHandlerMs[i] = h;
                    if (w < 0) continue;
                    work.Add(w);
                    if (w > a.WorstWorkMs)
                        a.WorstMergedFrame = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            "frame {0} at {1:0} m: {2:0.00} ms (runs {3:0.00} and {4:0.00}; {5} chunks, {6} B{7})",
                            i, a.FrameAlong[i], w, wa, wb, a.FrameChunks[i], a.FrameBytes[i], a.FrameSelected[i] ? ", selection" : "");
                    a.WorstWorkMs = Math.Max(a.WorstWorkMs, w);
                    a.WorstMainMs = Math.Max(a.WorstMainMs, w + h);
                }
                work.Sort();
                a.P999WorkMs = work.Count > 0 ? work[(int)(0.999 * (work.Count - 1))] : 0;
                return a;
            }

            public string Line()
            {
                return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{0,-4} {1} fps, {2:0} m in {3} frames, {4} builds (worst {5:0} ms desktop), {6} detail tiles shown | per frame worst: " +
                    "scheduler + upload copy {20:0.00} ms (about {21:0.0} ms on the tier's phones; 99.9th percentile {26:0.00} ms, about {27:0.0}), " +
                    "with the shown-tile handlers {24:0.00} ms " +
                    "(about {25:0.0} ms), " +
                    "scheduler {7:0.00} ms (p99 {8:0.00}; {17} frames paused by a GC, worst {18:0.0} ms), allocated {19} B, upload {9:0.000} MB in {10} chunks (copy {11:0.00} ms) | on show, worst: " +
                    "still on the main thread {12:0.00} ms (zones {28:0.00}, audio join {29:0.00}, colliders {30:0.00}); moved to workers {13:0.00} ms (area types {22:0.00}, " +
                    "audio index {23:0.00}, static colliders {14:0.00}, fauna ground {15:0.00}) | " +
                    "nearest undrawn route street {16} | build objects created {31} arriving, {32} riding",
                    TierName[Tier], Fps, LengthM, Frames, Builds, WorstBuildMs, Shown, WorstSchedulerMs, P99SchedulerMs, WorstBytes / 1048576.0,
                    WorstChunks, WorstCopyMs, WorstMainHandlersMs, WorstMovedMs, WorstStaticPrepareMs, WorstFaunaMs,
                    double.IsInfinity(NearestGapM) ? "none" : NearestGapM.ToString("0") + " m (" + GapFrames + " frames; first " + FirstGap + ")",
                    GcFrames, WorstGcFrameMs, WorstAllocBytes, WorstWorkMs, WorstWorkMs * MainThreadFactor[Tier], WorstAreaTypesMs, WorstAudioPrepareMs,
                    WorstMainMs, WorstMainMs * MainThreadFactor[Tier], P999WorkMs, P999WorkMs * MainThreadFactor[Tier],
                    WorstZonesMs, WorstAudioJoinMs, WorstCollidersMs, BuildsCreatedArriving, BuildsCreatedRiding);
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
        /// their level-10 tile visible as a detail node. Every road block of the detail views is switched each frame as
        /// the game switches it (<see cref="RoadBlockWatch"/>), and <paramref name="onFrame"/> (optional) sees each frame
        /// after that: metres along the route, the rider's x and z, and the blocks. A positive
        /// <paramref name="detailBuildMs"/> makes every detail build take that long (desktop ms, times the factor).
        /// </summary>
        internal static RideResult Ride(int tier, double factor, Dictionary<string, double> buildMs, double checkM, StreamingConfig config = null,
                                        Action<double, double, double, RoadBlockWatch> onFrame = null, double detailBuildMs = 0)
        {
            List<double[]> route = Route();
            List<double[]> pts = Resample(route, 5.0);
            double length = pts[pts.Count - 1][2];
            StreamingConfig cfg = config ?? StreamingConfig.ForTier(tier);
            MeshingSettings meshing = StreamingScene.Settings(tier, Ghumante.Core.Meshing.BiomePalette.DefaultMonth);
            TileGroundQuery ground = StreamingScene.Ground(meshing);
            TileSelector selector = TileSelector.ForPack(cfg, StreamingSampleRegion.Pack);
            var sink = new RideSink(ground, Db(), meshing.Corridors);
            RoadBlocks.Radii(BandConfig.ForTier(tier), cfg, out sink.Roads.NearM, out sink.Roads.FarM);
            var workers = new VirtualWorkers(buildMs, factor) { DetailBuildMs = detailBuildMs };
            double fps = tier == StreamingConfig.TierHigh ? 60 : 30;
            var detailTiles = new Dictionary<ulong, bool>();
            var visibleDetail = new HashSet<ulong>();
            var r = new RideResult { Tier = tier, Fps = fps, LengthM = length };
            var schedulerMs = new List<double>();
            var workMs = new List<double>();
            var firstDesired = new Dictionary<SelectedNode, double[]>();
            using (var s = new StreamingScheduler(StreamingSampleRegion.Pack, cfg, selector, ground, sink, workers,
                                                  ThreadClock.Ms, meshing))
            {
                // Only the frame's bytes (1 MiB) limit the uploads here, not the scheduler's clock: this desktop never
                // spends the 2 ms in a tick except when another process slows a frame down, and a frame cut short that way
                // would make the two runs of RideResult.Merge do different work from then on. The frame work measured is
                // therefore what a phone would do if its time budget never bound (it binds earlier there, so this is the
                // worse case); the time budget itself is StreamingFrameBudgetTests.TheTicksOwnWorkCountsAgainstTheUploadTime.
                s.UploadBudgetMs = 1e9;
                // Arrival: settle at the start (the loading screen), unmeasured.
                double t = 0;
                sink.NowX = pts[0][0];
                sink.NowZ = pts[0][1];
                for (int i = 0; i < 20000 && !s.IsSettled; i++)
                {
                    s.Tick(pts[0][0], pts[0][1]);
                    workers.PickUp(0, true);
                }
                Assert.That(s.IsSettled, Is.True, "settles at Thamel Chowk: " + s.Stats.Format());
                sink.NowX = pts[0][0];
                sink.NowZ = pts[0][1];
                sink.Roads.Update(0, pts[0][0], pts[0][1]);
                sink.Roads.ResetEvents(); // what the arrival screen showed at once
                r.BuildsCreatedArriving = s.Stats.BuildsCreated;
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
                    sink.NowAlong = along;
                    sink.NowX = x;
                    sink.NowZ = z;
                    int gc = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
                    long alloc0 = GC.GetAllocatedBytesForCurrentThread();
                    int selections0 = s.Stats.Selections;
                    double t0 = ThreadClock.Ms();
                    s.Tick(x, z);
                    double tick = ThreadClock.Ms() - t0;
                    long alloc = GC.GetAllocatedBytesForCurrentThread() - alloc0 - sink.HandlerAllocBytes;
                    bool collected = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2) != gc;
                    workers.PickUp(t);
                    sink.Roads.Update(along, x, z); // WorldStreamer.UpdateView: after the tick, in the same frame
                    if (onFrame != null) onFrame(along, x, z, sink.Roads);
                    IReadOnlyList<SelectedNode> selection = s.Selection;
                    for (int i = 0; i < selection.Count; i++)
                    {
                        SelectedNode n = selection[i];
                        if (n.DrawsDetail && !firstDesired.ContainsKey(n))
                            firstDesired[n] = new[] { along, Math.Sqrt(TileArea.ClosestDistanceSquared(n.Area, x, z)), t };
                    }

                    double handlers = sink.MainMs + sink.MovedMs;
                    double sched = tick - sink.CopyMs - handlers;
                    if (alloc > r.WorstAllocBytes)
                    {
                        r.WorstAllocBytes = alloc;
                        r.WorstAllocFrame = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            "frame {0} at {1:0} m: {2} B (select {3:0.00} ms, collect {4:0.00}, resolve {5:0.00}, dispatch {6:0.00}), {7} chunks uploaded, {8} selections so far, {9} visible",
                            frame, along, alloc, s.Stats.SelectMs, s.Stats.CollectMs, s.Stats.ResolveMs, s.Stats.DispatchMs, sink.FrameChunks,
                            s.Stats.Selections, s.Stats.Visible);
                    }
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
                    r.FrameAlong.Add((float)along);
                    r.FrameWorkMs.Add(collected ? -1f : (float)(sched + sink.CopyMs));
                    r.FrameHandlerMs.Add((float)sink.MainMs);
                    r.FrameBytes.Add(sink.FrameBytes);
                    r.FrameChunks.Add(sink.FrameChunks);
                    r.FrameSelected.Add(s.Stats.Selections != selections0);
                    if (!collected)
                    {
                        workMs.Add(sched + sink.CopyMs);
                        r.WorstWorkMs = Math.Max(r.WorstWorkMs, sched + sink.CopyMs);
                        if (sched + sink.CopyMs + sink.MainMs > r.WorstMainMs)
                            r.WorstMainFrame = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                "frame {0} at {1:0} m: {2:0.00} ms = scheduler {3:0.00} (select {4:0.00}, collect {5:0.00}, resolve {6:0.00} less handlers, dispatch {7:0.00}) " +
                                "+ copy {8:0.00} for {9} chunks + handlers left on the main thread {10:0.00} (zones {11:0.00}, audio join {12:0.00}, colliders {13:0.00})",
                                frame, along, sched + sink.CopyMs + sink.MainMs, sched, s.Stats.SelectMs, s.Stats.CollectMs, s.Stats.ResolveMs - handlers,
                                s.Stats.DispatchMs, sink.CopyMs, sink.FrameChunks, sink.MainMs, sink.ZonesMs, sink.AudioJoinMs, sink.CollidersMs);
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
                    r.WorstZonesMs = Math.Max(r.WorstZonesMs, sink.ZonesMs);
                    r.WorstAudioJoinMs = Math.Max(r.WorstAudioJoinMs, sink.AudioJoinMs);
                    r.WorstCollidersMs = Math.Max(r.WorstCollidersMs, sink.CollidersMs);
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
                r.BuildsCreatedRiding = s.Stats.BuildsCreated - r.BuildsCreatedArriving;
                sink.Roads.Frozen = true; // disposing the scheduler hides every view
            }
            schedulerMs.Sort();
            r.P99SchedulerMs = schedulerMs[(int)(0.99 * (schedulerMs.Count - 1))];
            workMs.Sort();
            r.P999WorkMs = workMs.Count > 0 ? workMs[(int)(0.999 * (workMs.Count - 1))] : 0;
            r.Builds = workers.Builds;
            r.WorstBuildMs = workers.WorstBuildMs;
            r.Shown = sink.Shown;
            RoadBlockWatch w = sink.Roads;
            r.NearestRoadPopInM = w.NearestPopInM;
            r.NearestRoadVanishM = w.NearestVanishM;
            r.RoadPopIns = w.PopIns;
            r.RoadVanishes = w.Vanishes;
            r.RoadNearKitMisses = w.NearKitMisses;
            r.RoadSwitches = w.Switches;
            r.MostFullBlocks = w.MostFull;
            r.MostFarBlocks = w.MostFar;
            r.FirstRoadPopIn = w.FirstPopIn;
            r.FirstRoadVanish = w.FirstVanish;
            r.FirstRoadNearKitMiss = w.FirstNearKitMiss;
            foreach (KeyValuePair<SelectedNode, double[]> kv in firstDesired)
            {
                double picked, due, ms;
                double[] shown, hidden;
                var line = new StringBuilder();
                line.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0}: selected at {1:0} m ({2:0} m away, {3:0.0} s)",
                                  kv.Key, kv.Value[0], kv.Value[1], kv.Value[2]);
                if (workers.PickedAt.TryGetValue(kv.Key, out picked) && workers.DueAt.TryGetValue(kv.Key, out due))
                    line.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, ", built {0:0.0}-{1:0.0} s", picked, due);
                if (buildMs.TryGetValue(kv.Key.ToString(), out ms)) line.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, " ({0:0} ms desktop)", ms);
                if (sink.ShownAt.TryGetValue(kv.Key, out shown))
                    line.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, ", shown at {0:0} m ({1:0} m away)", shown[0], shown[1]);
                if (sink.HiddenAt.TryGetValue(kv.Key, out hidden))
                    line.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, ", hidden at {0:0} m ({1:0} m away)", hidden[0], hidden[1]);
                r.Timeline.Add(line.ToString());
            }
            return r;
        }

        private static readonly Dictionary<int, Dictionary<string, double>> BuildTimes = new Dictionary<int, Dictionary<string, double>>();
        private static readonly Dictionary<int, RideResult> InstantRides = new Dictionary<int, RideResult>();

        /// <summary>The slowest detail build this machine took (wall clock, Asan's tile on a loaded run of the merged base):
        /// with the phone factor, 18 s, a phone slower than the factor says.</summary>
        internal const double SlowDetailBuildMs = 4500;

        /// <summary>Desktop build times per node of the tier's ride (from a ride whose builds land at once; once per run).</summary>
        private static Dictionary<string, double> TimesFor(int tier, out RideResult instant)
        {
            Dictionary<string, double> times;
            if (BuildTimes.TryGetValue(tier, out times) && InstantRides.TryGetValue(tier, out instant)) return times;
            times = new Dictionary<string, double>();
            instant = Ride(tier, 0, times, BandConfig.ForTier(tier).B1OuterM);
            BuildTimes[tier] = times;
            InstantRides[tier] = instant;
            return times;
        }

        /// <summary>
        /// Owner playtest #4 ("stutters"): the ride at 50 km/h through Thamel, Asan and New Road, with builds as fast as
        /// this desktop makes them and as slow as a phone (<see cref="PhoneFactor"/>). Every frame's streaming work on the
        /// main thread (the scheduler's tick and the upload copy, CPU time of this thread times the tier's
        /// <see cref="MainThreadFactor"/>) stays within W2_DESIGN 10.4's streaming budget (2 / 2 / 1.5 ms), the worst
        /// frame included; each ride runs twice and a frame counts at the faster of its two identical runs
        /// (<see cref="RideResult.Merge"/>), so the machine's other load does not decide. No frame uploads more than
        /// 1 MiB, every street of the route within the B1 radius is drawn, and no road block appears, vanishes or lacks
        /// its full kit within the B1 radius.
        /// </summary>
        [Test]
        public void RideThroughThamelAsanAndNewRoadStaysWithinTheFrameBudget([Values(0, 1, 2)] int tier)
        {
            float nearM = BandConfig.ForTier(tier).B1OuterM;
            RideResult warm;
            Dictionary<string, double> times = TimesFor(tier, out warm); // also compiles every path once (no JIT below)
            // The warm-up ride is the same instant ride (its JIT frames lose to the second run's).
            RideResult instant = RideResult.Merge(Ride(tier, 0, new Dictionary<string, double>(), nearM), warm);
            TestContext.WriteLine("instant builds: " + instant.Line());
            TestContext.WriteLine("  worst frame (of the two runs): " + instant.WorstMergedFrame + "; " + instant.UnpairedFrames + " unpaired frames");
            TestContext.WriteLine("  " + instant.RoadLine());
            RideResult first = Ride(tier, PhoneFactor, times, nearM);
            TestContext.WriteLine("phone builds, first run: worst frame " + first.WorstFrame);
            TestContext.WriteLine("  worst frame with the handlers: " + first.WorstMainFrame);
            TestContext.WriteLine("  most allocated: " + first.WorstAllocFrame);
            RideResult phone = RideResult.Merge(first, Ride(tier, PhoneFactor, times, nearM));
            TestContext.WriteLine("phone builds:   " + phone.Line());
            TestContext.WriteLine("  worst frame (of the two runs): " + phone.WorstMergedFrame + "; " + phone.UnpairedFrames + " unpaired frames");
            TestContext.WriteLine("  " + phone.RoadLine());
            TestContext.WriteLine("  detail tiles:\n    " + string.Join("\n    ", phone.Timeline));
            double budgetMs = tier == StreamingConfig.TierHigh ? 1.5 : 2.0; // W2_DESIGN 10.4: streaming upload per frame
            foreach (RideResult r in new[] { instant, phone })
            {
                Assert.That(r.WorstBytes, Is.LessThanOrEqualTo(1L << 20), "a frame uploads at most 1 MiB of tile meshes");
                Assert.That(r.P999WorkMs * MainThreadFactor[tier], Is.LessThanOrEqualTo(budgetMs),
                            "the worst frames' streaming work (scheduler and upload copy, CPU ms of this thread) on the tier's phones");
                Assert.That(r.WorstWorkMs * MainThreadFactor[tier], Is.LessThanOrEqualTo(budgetMs),
                            "the worst frame's streaming work on the tier's phones: " + r.WorstMergedFrame);
                Assert.That(r.UnpairedFrames, Is.EqualTo(0), "the two runs did the same work frame by frame");
                Assert.That(r.NearestGapM, Is.GreaterThan(nearM), "every street of the route within the B1 radius (" + nearM + " m) is drawn");
                Assert.That(r.NearestRoadPopInM, Is.GreaterThan(nearM), "no road block appears within the B1 radius: " + r.FirstRoadPopIn);
                Assert.That(r.NearestRoadVanishM, Is.GreaterThan(nearM), "no road block disappears within the B1 radius: " + r.FirstRoadVanish);
                Assert.That(r.RoadNearKitMisses, Is.EqualTo(0), "every road block within the B1 radius shows its full kit: " + r.FirstRoadNearKitMiss);
            }
        }

        private static readonly Dictionary<(ulong, int), TileBuild> SnapshotBuilds = new Dictionary<(ulong, int), TileBuild>();

        /// <summary>A detail node's terrain, roads and markings as the ride's tier builds them (buildings and dressing off).</summary>
        private static TileBuild SnapshotBuild(SelectedNode n, int tier)
        {
            TileBuild b;
            if (SnapshotBuilds.TryGetValue((n.Area.Key, tier), out b)) return b;
            MeshingSettings s = StreamingScene.Settings(tier, Ghumante.Core.Meshing.BiomePalette.DefaultMonth);
            s.DrawBuildings = false;
            s.DrawAreas = false;
            s.DrawInstances = false;
            s.DrawOrnaments = false;
            b = new TileBuild { Node = n, Source = StreamingSampleRegion.Tile(n.Source) };
            TileBuild.Execute(b, StreamingSampleRegion.Pack, StreamingConfig.ForTier(tier), s, null);
            SnapshotBuilds[(n.Area.Key, tier)] = b;
            return b;
        }

        /// <summary>
        /// Visual self-check (GHUMANTE_PREVIEW_DIR): the ride with phone-slow builds, and the roads exactly as the game
        /// draws them at five points of the route (<see cref="RoadBlockWatch.Snapshot"/>): every block's full kit and its
        /// markings as built, the cheap far copy tinted blue, the terrain of the detail tiles, a red post at the rider
        /// and white rings at the near (B1) and far road radii (<c>rider.obj</c>, for the top view), in metres from the rider (<c>perf-roads/ride_&lt;tier&gt;/at_&lt;m&gt;.obj</c>);
        /// plus <c>perf-roads/ride_&lt;tier&gt;_frames.csv</c>, the per-frame streaming work and bytes for the charts.
        /// </summary>
        [Test]
        public void DumpRideSnapshots([Values(0, 1, 2)] int tier)
        {
            if (!ObjDump.Enabled) Assert.Ignore("set " + ObjDump.EnvVar + " to dump");
            float nearM, farM;
            RoadBlocks.Radii(BandConfig.ForTier(tier), StreamingConfig.ForTier(tier), out nearM, out farM);
            RideResult warm;
            Dictionary<string, double> times = TimesFor(tier, out warm);
            double length = warm.LengthM;
            var snaps = new List<RideSnapshot>();
            RideResult phone = RideResult.Merge(Ride(tier, PhoneFactor, times, nearM, null,
                                                     SnapshotTaker(new[] { 0.08 * length, 0.3 * length, 0.5 * length, 0.7 * length, 0.92 * length }, snaps)),
                                                Ride(tier, PhoneFactor, times, nearM)); // the frame chart: the faster of two identical runs
            TestContext.WriteLine(phone.Line());
            TestContext.WriteLine("  worst frame: " + phone.WorstFrame);
            TestContext.WriteLine(phone.RoadLine());

            var csv = new StringBuilder("along_m,work_ms,handlers_ms,bytes\n");
            for (int i = 0; i < phone.FrameAlong.Count; i++)
                csv.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0:0.0},{1:0.0000},{2:0.0000},{3}\n",
                                 phone.FrameAlong[i], phone.FrameWorkMs[i], phone.FrameHandlerMs[i], phone.FrameBytes[i]);
            string csvPath = Path.Combine(ObjDump.Dir, "perf-roads", "ride_" + TierName[tier].ToLowerInvariant() + "_frames.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(csvPath));
            File.WriteAllText(csvPath, csv.ToString());

            foreach (RideSnapshot snap in snaps)
                TestContext.WriteLine(WriteSnapshot("perf-roads/ride_" + TierName[tier].ToLowerInvariant() + "/at_" + snap.Along.ToString("0000") + "/",
                                                    "perf-roads ride " + TierName[tier], tier, snap, nearM, farM));
        }

        /// <summary>
        /// Visual self-check of the pop-in fix (GHUMANTE_PREVIEW_DIR): the Low ride with every detail build as slow as in
        /// <see cref="EvenSlowBuildsNeverLetARoadBlockPopInNearTheRider"/>, with the detail radius first tried in the perf
        /// pass (350 m; the build of the first Unity playtest had 100 m) and now (450 m), snapshotted at the same point of
        /// the route, 280 m from Thamel Chowk, where with
        /// 350 m the Asan tile's streets are still missing around the rider (<c>perf-roads/late_tile/r350</c> and
        /// <c>r450</c>).
        /// </summary>
        [Test]
        public void DumpLateTileBeforeAndAfter()
        {
            if (!ObjDump.Enabled) Assert.Ignore("set " + ObjDump.EnvVar + " to dump");
            const int tier = StreamingConfig.TierLow;
            float nearM, farM;
            RoadBlocks.Radii(BandConfig.ForTier(tier), StreamingConfig.ForTier(tier), out nearM, out farM);
            RideResult warm;
            Dictionary<string, double> times = TimesFor(tier, out warm);
            foreach (int radius in new[] { 350, 450 })
            {
                StreamingConfig cfg = StreamingConfig.ForTier(tier);
                cfg.DetailRadiusM = radius;
                var snaps = new List<RideSnapshot>();
                RideResult r = Ride(tier, PhoneFactor, times, nearM, cfg, SnapshotTaker(new[] { 280.0 }, snaps), SlowDetailBuildMs);
                TestContext.WriteLine("detail radius " + radius + " m: " + r.RoadLine());
                foreach (RideSnapshot snap in snaps)
                    TestContext.WriteLine(WriteSnapshot("perf-roads/late_tile/r" + radius + "/", "perf-roads Low, detail radius " + radius + " m, slow builds",
                                                        tier, snap, nearM, farM));
            }
        }

        /// <summary>The road blocks drawn at one frame of a ride, with the rider's place and heading.</summary>
        internal sealed class RideSnapshot
        {
            public double Along, X, Z, HeadingX, HeadingZ;
            public List<Tuple<SelectedNode, int, int>> Blocks;
        }

        /// <summary>A per-frame callback for <see cref="Ride"/> that snapshots the road blocks once the rider passes each of
        /// <paramref name="at"/> (m along the route), heading from the last 40 frames (about 18 m).</summary>
        internal static Action<double, double, double, RoadBlockWatch> SnapshotTaker(double[] at, List<RideSnapshot> into)
        {
            int next = 0;
            var back = new Queue<double[]>();
            return (along, x, z, w) =>
            {
                back.Enqueue(new[] { x, z });
                if (back.Count > 40) back.Dequeue();
                if (next >= at.Length || along < at[next]) return;
                double[] from = back.Peek();
                double hx = x - from[0], hz = z - from[1], len = Math.Sqrt(hx * hx + hz * hz);
                into.Add(new RideSnapshot
                {
                    Along = along, X = x, Z = z, Blocks = w.Snapshot(),
                    HeadingX = len > 1e-6 ? hx / len : 0, HeadingZ = len > 1e-6 ? hz / len : 1,
                });
                next++;
            };
        }

        /// <summary>Writes one snapshot under <paramref name="dir"/>: roads.obj (every block's full kit as built, the cheap
        /// copy tinted blue), decals.obj, terrain.obj (the detail tiles' terrain within the far radius) and rider.obj
        /// (a red post at the rider, white rings at the near and far road radii), in metres from the rider, with a chase
        /// eye 14 m behind and 7 m above, looking 60 m ahead. Returns the description line.</summary>
        internal static string WriteSnapshot(string dir, string title, int tier, RideSnapshot snap, float nearM, float farM)
        {
            double rx = snap.X, rz = snap.Z;
            var roads = new MeshData();
            var terrain = new MeshData();
            var marks = new MeshData();
            int full = 0, cheap = 0;
            var seen = new HashSet<SelectedNode>();
            float riderY = float.NaN;
            double best = double.MaxValue;
            foreach (var e in snap.Blocks)
            {
                TileBuild b = SnapshotBuild(e.Item1, tier);
                float dx = (float)(e.Item1.Area.X0 - rx), dz = (float)(e.Item1.Area.Z0 - rz);
                if (seen.Add(e.Item1))
                {
                    MeshData t = StreamingScene.Layer(b, TileLayers.Terrain);
                    var moved = new MeshData();
                    ObjDump.Append(moved, t, dx, -0.05f, dz);
                    ObjDump.Append(terrain, ObjDump.Crop(moved, -farM - 60, -farM - 60, farM + 60, farM + 60, true), 0, 0, 0);
                }
                int block = e.Item2;
                if (e.Item3 == 2)
                {
                    full++;
                    MeshData kit = StreamingScene.Layer(b, TileLayers.Roads, q => q == block);
                    ObjDump.Append(roads, kit, dx, 0, dz);
                    ObjDump.Append(marks, StreamingScene.Layer(b, TileLayers.RoadDecals, q => q == block), dx, 0, dz);
                    for (int v = 0; v < kit.VertexCount; v++)
                    {
                        double px = kit.Positions[3 * v] + dx, pz = kit.Positions[3 * v + 2] + dz, d = px * px + pz * pz;
                        if (d < best)
                        {
                            best = d;
                            riderY = kit.Positions[3 * v + 1];
                        }
                    }
                }
                else
                {
                    cheap++;
                    MeshData far = StreamingScene.Layer(b, TileLayers.Roads, q => q == RoadBlocks.FarPart + block);
                    for (int v = 0; v < far.VertexCount; v++) // blue: no road surface is (brick paving reads orange)
                    {
                        far.Colors[4 * v] = (byte)(far.Colors[4 * v] / 4);
                        far.Colors[4 * v + 1] = (byte)(far.Colors[4 * v + 1] / 3 + 60);
                        far.Colors[4 * v + 2] = (byte)Math.Min(255, far.Colors[4 * v + 2] / 3 + 170);
                    }
                    ObjDump.Append(roads, far, dx, 0, dz);
                }
            }
            // The ground of the level-10 tiles around the rider that show no road (a tile not shown yet draws its terrain
            // from a coarser node in the game; the snapshot draws its own terrain there, without roads).
            double reach = farM + 60;
            for (double tz = rz - reach; tz < rz + reach + 1024; tz += 1024)
            for (double tx = rx - reach; tx < rx + reach + 1024; tx += 1024)
            {
                TileId id = TileId.At(10, Math.Min(tx, rx + reach), Math.Min(tz, rz + reach));
                var node = new SelectedNode(id, id);
                if (!StreamingSampleRegion.Pack.Contains(id) || seen.Contains(node)) continue;
                seen.Add(node);
                TileBuild b = SnapshotBuild(node, tier);
                var moved = new MeshData();
                ObjDump.Append(moved, StreamingScene.Layer(b, TileLayers.Terrain), (float)(id.X0 - rx), -0.05f, (float)(id.Z0 - rz));
                ObjDump.Append(terrain, ObjDump.Crop(moved, -farM - 60, -farM - 60, farM + 60, farM + 60, true), 0, 0, 0);
            }
            if (float.IsNaN(riderY))
            {
                // No full kit around the rider (a tile that came late): stand the marker on the terrain.
                for (int v = 0; v < terrain.VertexCount; v++)
                {
                    double px = terrain.Positions[3 * v], pz = terrain.Positions[3 * v + 2], d = px * px + pz * pz;
                    if (d < best)
                    {
                        best = d;
                        riderY = terrain.Positions[3 * v + 1];
                    }
                }
                if (float.IsNaN(riderY)) riderY = 0;
            }
            var rider = new MeshData();
            Ghumante.Core.Meshing.Shapes.Shapes.Cylinder(rider, Ghumante.Core.Meshing.Shapes.Affine3.Translation(0, riderY, 0),
                Ghumante.Core.Meshing.Shapes.ShapeBrush.Hex(0xE02020, MaterialChannel.Paint), 0.8, 12, 16);
            foreach (float radius in new[] { nearM, farM })
                Ghumante.Core.Meshing.Shapes.Shapes.Torus(rider, Ghumante.Core.Meshing.Shapes.Affine3.Translation(0, riderY + 1, 0),
                    Ghumante.Core.Meshing.Shapes.ShapeBrush.Hex(0xFFFFFF, MaterialChannel.Paint), radius, 1.5, 96, 6);
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            string eye = string.Format(ic, "{0:0.##},{1:0.##},{2:0.##}", -14 * snap.HeadingX, riderY + 7, 14 * snap.HeadingZ);
            string look = string.Format(ic, "{0:0.##},{1:0.##},{2:0.##}", 60 * snap.HeadingX, riderY + 1, -60 * snap.HeadingZ);
            var directives = new[]
            {
                string.Format(ic, "{0} at {1:0} m: {2} full-kit and {3} cheap road blocks drawn; rider at the origin (y {4:0.0})",
                              title, snap.Along, full, cheap, riderY),
                "meshpreview: grid=0 sun=160,62 eye=" + eye + " look=" + look,
            };
            ObjDump.Write(roads, dir + "roads.obj", directives);
            ObjDump.Write(marks, dir + "decals.obj", directives);
            ObjDump.Write(terrain, dir + "terrain.obj", directives);
            ObjDump.Write(rider, dir + "rider.obj", directives);
            return directives[0];
        }

        /// <summary>
        /// Owner playtest #4 with a margin: the same ride on a phone slower than <see cref="PhoneFactor"/> says (a build on
        /// an efficiency core or a throttled phone), every detail build taking <see cref="SlowDetailBuildMs"/> (18 s at
        /// the factor, 250 m at 50 km/h). The tier's detail radius still keeps every road block and every street of the
        /// route drawn before it comes within the B1 radius, and none vanishes there.
        /// </summary>
        [Test]
        public void EvenSlowBuildsNeverLetARoadBlockPopInNearTheRider([Values(0, 1, 2)] int tier)
        {
            float nearM = BandConfig.ForTier(tier).B1OuterM;
            RideResult warm;
            Dictionary<string, double> times = TimesFor(tier, out warm);
            RideResult slow = Ride(tier, PhoneFactor, times, nearM, null, null, SlowDetailBuildMs);
            TestContext.WriteLine("slow builds: " + slow.Line());
            TestContext.WriteLine("  most allocated: " + slow.WorstAllocFrame);
            TestContext.WriteLine("  " + slow.RoadLine());
            TestContext.WriteLine("  detail tiles:\n    " + string.Join("\n    ", slow.Timeline));
            Assert.That(slow.NearestGapM, Is.GreaterThan(nearM), "every street of the route within the B1 radius (" + nearM + " m) is drawn");
            Assert.That(slow.NearestRoadPopInM, Is.GreaterThan(nearM), "no road block appears within the B1 radius: " + slow.FirstRoadPopIn);
            Assert.That(slow.NearestRoadVanishM, Is.GreaterThan(nearM), "no road block disappears within the B1 radius: " + slow.FirstRoadVanish);
            Assert.That(slow.RoadNearKitMisses, Is.EqualTo(0), "every road block within the B1 radius shows its full kit: " + slow.FirstRoadNearKitMiss);
            Assert.That(slow.BuildsCreatedRiding, Is.EqualTo(0),
                        "riding creates no build objects: every build that can be alive at once is recycled with its grown buffers");
        }

        /// <summary>
        /// The repro of the pop-in, kept as a guard on the reasoning behind Low's detail radius: with the radius of the
        /// build the owner played at the first Unity playtest (100 m), or the 350 m tried first in the perf pass, and
        /// builds as slow as in
        /// <see cref="EvenSlowBuildsNeverLetARoadBlockPopInNearTheRider"/>, the rider reaches streets whose tile still
        /// draws terrain only (no roads, no houses) inside the B1 radius, and their road blocks pop in around the rider.
        /// If this ever stops failing, builds got fast enough to revisit <see cref="StreamingConfig.DetailRadiusM"/> on Low.
        /// </summary>
        [Test]
        public void TheOldLowDetailRadiiLetStreetsPopInAroundTheRider([Values(100, 350)] int radiusM)
        {
            float nearM = BandConfig.ForTier(StreamingConfig.TierLow).B1OuterM;
            StreamingConfig old = StreamingConfig.ForTier(StreamingConfig.TierLow);
            old.DetailRadiusM = radiusM;
            var times = new Dictionary<string, double>();
            Ride(StreamingConfig.TierLow, 0, times, nearM, old);
            RideResult phone = Ride(StreamingConfig.TierLow, PhoneFactor, times, nearM, old, null, SlowDetailBuildMs);
            TestContext.WriteLine("Low with the old " + radiusM + " m detail radius, slow builds: " + phone.Line());
            TestContext.WriteLine("  " + phone.RoadLine());
            Assert.That(phone.NearestRoadPopInM, Is.LessThan(nearM), "road blocks pop in near the rider: " + phone.FirstRoadPopIn);
            Assert.That(phone.NearestGapM, Is.LessThan(nearM), "streets of the route within the B1 radius are drawn as bare terrain for a while");
        }
    }
}
