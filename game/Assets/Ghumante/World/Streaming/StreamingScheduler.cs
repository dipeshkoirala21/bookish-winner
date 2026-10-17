using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;
using Ghumante.Core.Streaming;

namespace Ghumante.World.Streaming
{
    /// <summary>What the scheduler drives on the main thread: the Unity tile views (WorldStreamer) or a test fake.</summary>
    public interface ITileSink
    {
        /// <summary>Upload one chunk of a build (<c>build.Chunks[chunk]</c>, never empty) to the node's view, creating
        /// the view (hidden) on its first chunk.</summary>
        void Upload(TileBuild build, int chunk);

        /// <summary>Show or hide the node's view (only called for nodes that uploaded at least one chunk).</summary>
        void SetVisible(SelectedNode node, bool visible);

        /// <summary>Destroy the node's view and meshes (also for partially uploaded nodes).</summary>
        void Release(SelectedNode node);

        /// <summary>Every chunk of the node is up and it is ready (still hidden): the view takes the build's
        /// non-mesh results (decoded tile, sampler, instances, heroes, colliders). Only for nodes with a view.</summary>
        void Ready(SelectedNode node, TileExtras extras);
    }

    /// <summary>
    /// The optional side of an <see cref="ITileSink"/> that replaces layers of a view in place
    /// (<see cref="StreamingScheduler.RefreshLayers"/>, the season change): the new chunks are staged hidden and swapped
    /// in at once, so the old layers stay on screen until the new ones are complete.
    /// </summary>
    public interface ITileRefreshSink
    {
        /// <summary>Stage one chunk of a refresh build (<c>build.Chunks[chunk]</c>) for the node's existing view.</summary>
        void UploadRefresh(TileBuild build, int chunk);

        /// <summary>Every chunk of the refresh is staged: destroy the view's chunks of the layers in
        /// <paramref name="layerMask"/> (<see cref="TileLayers"/> bits) and show the staged ones instead.</summary>
        void CommitRefresh(SelectedNode node, int layerMask);

        /// <summary>Drop the node's staged chunks (the refresh was abandoned).</summary>
        void AbortRefresh(SelectedNode node);
    }

    /// <summary>Runs tile builds off the main thread.</summary>
    public interface ITileJobRunner
    {
        void Run(Action work);
    }

    /// <summary>The thread pool (<see cref="Task.Run(Action)"/>).</summary>
    public sealed class ThreadPoolJobRunner : ITileJobRunner
    {
        public void Run(Action work)
        {
            Task.Run(work);
        }
    }

    /// <summary>Runs builds synchronously on the calling thread (tests, tools).</summary>
    public sealed class InlineJobRunner : ITileJobRunner
    {
        public void Run(Action work)
        {
            work();
        }
    }

    /// <summary>Streaming counters for the debug HUD.</summary>
    public struct StreamingStats
    {
        public int Desired;
        public int ReadyDesired;
        public int Visible;
        public int Ready;
        public int Loading;
        public int Queued;
        public int Jobs;
        public int PendingUploads;
        public int Selections;
        public int Failed;
        public int CachedTiles;
        public long CachedBytes;
        public long CacheBudgetBytes;

        /// <summary>Estimated GPU bytes of every uploaded view.</summary>
        public long MeshBytes;

        /// <summary>Main-thread upload milliseconds in the last tick, and the worst tick of the last second.</summary>
        public double UploadMs;

        /// <summary>Mesh bytes and chunks the last tick uploaded (tile chunks only; the bytes a caller reserved for its
        /// own uploads, <see cref="StreamingScheduler.Tick(double, double, long)"/>, are not included).</summary>
        public long TickUploadBytes;

        public int TickUploadChunks;

        /// <summary>Main-thread milliseconds of the last tick's phases besides the uploads: the selection (only when the
        /// focus moved far enough), collecting finished builds, the swap rule with the ground query, and dispatching.</summary>
        public double SelectMs, CollectMs, ResolveMs, DispatchMs;

        /// <summary>Of <see cref="ResolveMs"/>: milliseconds spent adding areas to and removing them from the ground query.</summary>
        public double GroundMs;

        /// <summary>Main-thread milliseconds of the whole last tick (selection, collection, uploads, swap rule, dispatch):
        /// what it took of the frame's streaming budget (<see cref="StreamingScheduler.UploadBudgetMs"/>), so a caller that
        /// uploads after it (the B0 cells) knows what is left.</summary>
        public double TickMs;

        public double WorstUploadMs;

        /// <summary>Worker milliseconds of the last finished build.</summary>
        public double LastBuildMs;

        /// <summary>Build objects created so far. Builds are recycled through a pool (their grown mesh buffers with
        /// them), so after the first few this stays put; each new one costs the main thread about half a megabyte of
        /// fresh buffers and, once its grown buffers are dropped, the collector a few megabytes of garbage.</summary>
        public int BuildsCreated;

        /// <summary>Finest level among visible nodes (-1 when none).</summary>
        public int FinestVisibleLevel;

        /// <summary>One line for the debug HUD.</summary>
        public string Format()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "tiles {0}/{1} vis {2} L{3}  queue {4}  jobs {5}  up {6}\nupload {7:0.0} ms (max {8:0.0})  build {9:0} ms  " +
                "cache {10}/{11} MB ({12})  mesh {13} MB{14}",
                ReadyDesired, Desired, Visible, FinestVisibleLevel < 0 ? 0 : FinestVisibleLevel, Queued, Jobs, PendingUploads,
                UploadMs, WorstUploadMs, LastBuildMs, CachedBytes >> 20, CacheBudgetBytes >> 20, CachedTiles, MeshBytes >> 20,
                Failed > 0 ? "  failed " + Failed : "");
        }
    }

    /// <summary>
    /// The engine-free core of the world streamer (ARCHITECTURE.md 7.2). Every <see cref="Tick"/> on the main thread:
    /// <list type="number">
    /// <item>re-selects when the focus moved (<see cref="TileSelector.Select"/> into <see cref="TileResidency"/>);</item>
    /// <item>collects finished builds (decoded source tiles go into an <see cref="LruByteCache{TKey,TValue}"/> sized by
    /// <see cref="StreamingConfig.CacheBudgetBytes"/>);</item>
    /// <item>uploads finished builds chunk by chunk (<see cref="TileBuild.Chunks"/>, at most
    /// <see cref="TileBuild.UploadChunkVertices"/> vertices and <see cref="TileBuild.UploadChunkBytes"/> bytes each)
    /// through the <see cref="ITileSink"/> until <see cref="UploadBudgetMs"/> is spent (counted from the start of the
    /// tick, the selection and collection included) or the next chunk would exceed
    /// <see cref="UploadBudgetBytes"/> less the bytes other uploaders reserved in the frame
    /// (<see cref="Tick(double, double, long)"/>; a chunk bigger than a whole frame goes alone after
    /// <see cref="MaxHeldTicks"/>, so streaming never stalls);</item>
    /// <item>applies the swap rule (<see cref="TileResidency.Resolve"/>) to views and to the ground query, which therefore
    /// always describes exactly what is drawn;</item>
    /// <item>dispatches the nearest queued nodes to <see cref="ITileJobRunner"/>, at most <see cref="MaxConcurrentJobs"/>
    /// at once, never decoding one source tile twice at the same time;</item>
    /// <item>when nothing is queued, rebuilds the layers <see cref="RefreshLayers"/> marked out of date (the season of
    /// the terrain and areas) for the nearest ready nodes, swapping them in place through the sink's
    /// <see cref="ITileRefreshSink"/> side, so nothing disappears while the world changes month.</item>
    /// </list>
    /// Builds (<see cref="TileBuild.Execute"/>) touch no Unity API. Main thread only, apart from the workers.
    /// <see cref="Dispose"/> releases all views and, when the scheduler owns the pack, closes it once the last running
    /// build has finished.
    /// </summary>
    public sealed class StreamingScheduler : IDisposable
    {
        private readonly StreamingConfig _config;
        private readonly TileSelector _selector;
        private readonly TileResidency _residency;
        private readonly TileGroundQuery _ground;
        private readonly MeshingSettings _meshing;
        private readonly PackReader _pack;
        private readonly bool _ownsPack;
        private readonly ITileSink _sink;
        private readonly ITileRefreshSink _refreshSink;
        private readonly List<SelectedNode> _stale = new List<SelectedNode>(64);
        private readonly HashSet<SelectedNode> _staleSet = new HashSet<SelectedNode>();
        private int _settingsVersion, _refreshMask;
        private readonly ITileJobRunner _runner;
        private readonly Func<double> _clockMs;
        private readonly LruByteCache<TileId, TileData> _cache;
        private readonly HashSet<TileId> _decoding = new HashSet<TileId>();
        private readonly ConcurrentQueue<TileBuild> _finished = new ConcurrentQueue<TileBuild>();
        private readonly List<TileBuild> _uploads = new List<TileBuild>(8);
        private readonly Stack<TileBuild> _pool = new Stack<TileBuild>(4);
        private readonly Dictionary<SelectedNode, Content> _content = new Dictionary<SelectedNode, Content>(512);
        private readonly Stack<Content> _contentPool = new Stack<Content>(64);
        private readonly HashSet<SelectedNode> _failed = new HashSet<SelectedNode>();
        private readonly List<SelectedNode> _selection = new List<SelectedNode>(512);
        private readonly List<SelectedNode> _show = new List<SelectedNode>(64);
        private readonly List<SelectedNode> _hide = new List<SelectedNode>(16);
        private readonly List<SelectedNode> _release = new List<SelectedNode>(64);
        private readonly Dictionary<SelectedNode, Content> _partial = new Dictionary<SelectedNode, Content>();

        private long _generation;
        private int _running; // builds handed to the runner and not finished (workers decrement)
        private int _inFlight; // builds handed out and not yet collected (main thread)
        private int _packDisposed;
        private volatile bool _disposed;
        private bool _hasSelection, _dirty = true, _resolveDirty = true;
        private double _selX, _selZ;
        private StreamingStats _stats;
        private double _worstWindowStart, _worstInWindow;
        private int _heldTicks; // ticks in a row whose first chunk did not fit the frame
        private bool _held;
        private int _groundVersion;

        /// <summary>What a resident node keeps after its build was uploaded.</summary>
        private sealed class Content
        {
            public TileHeightSampler Sampler;
            public RoadSpatialIndex Roads;
            public bool HasView;
            public bool InGround;
            public long Bytes;
            public readonly long[] LayerBytes = new long[TileLayers.Count];
            public int Level;

            /// <summary>Settings version the shown layers were built with, and whether a refresh build is under way.</summary>
            public int Version;

            public bool Refreshing;
        }

        /// <param name="pack">Region pack the builds decode from.</param>
        /// <param name="config">Streaming settings of the device tier (copied).</param>
        /// <param name="selector">Selection over the pack (<see cref="TileSelector.ForPack"/>, may be built on a worker).</param>
        /// <param name="ground">Ground query fed with what is drawn; build it with <paramref name="meshing"/>'s road options.</param>
        /// <param name="sink">Main-thread view side.</param>
        /// <param name="runner">Where builds run (<see cref="ThreadPoolJobRunner"/> in the game).</param>
        /// <param name="clockMs">Monotonic milliseconds for the upload budget.</param>
        /// <param name="meshing">Meshing options (null: defaults).</param>
        /// <param name="ownsPack">Dispose the pack with the scheduler (after the last running build).</param>
        public StreamingScheduler(PackReader pack, StreamingConfig config, TileSelector selector, TileGroundQuery ground,
                                  ITileSink sink, ITileJobRunner runner, Func<double> clockMs, MeshingSettings meshing = null,
                                  bool ownsPack = false)
        {
            _pack = pack ?? throw new ArgumentNullException(nameof(pack));
            if (config == null) throw new ArgumentNullException(nameof(config));
            _config = config.Clone();
            _config.Validate();
            _selector = selector ?? TileSelector.ForPack(_config, pack);
            _ground = ground ?? throw new ArgumentNullException(nameof(ground));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _refreshSink = sink as ITileRefreshSink;
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _clockMs = clockMs ?? throw new ArgumentNullException(nameof(clockMs));
            _meshing = meshing ?? new MeshingSettings();
            _ownsPack = ownsPack;
            _residency = new TileResidency(_config.MaxResidentTiles);
            _cache = new LruByteCache<TileId, TileData>(_config.CacheBudgetBytes, 128);
            _stats.CacheBudgetBytes = _config.CacheBudgetBytes;
            _stats.FinestVisibleLevel = -1;
        }

        /// <summary>Builds running at once (ARCHITECTURE 7.2: decode and mesh on at most two workers).</summary>
        public int MaxConcurrentJobs = 2;

        /// <summary>Finished builds allowed to wait for upload; dispatch pauses beyond it (bounds build memory).</summary>
        public int MaxPendingUploads = 2;

        /// <summary>Main-thread milliseconds per tick for streaming (2 ms at 30 fps, 1.5 ms at 60 fps; W2_DESIGN 10.4's
        /// streaming upload line). The tick's own work before the uploads (the selection, collecting finished builds)
        /// counts against it: no chunk after the first starts once the tick has run this long, so a frame that reselects
        /// (0.8 to 1 ms of a Low phone every 4 m) uploads less instead of adding the selection on top of a full budget of
        /// chunks (perf pass, owner playtest #4 "stutters").</summary>
        public double UploadBudgetMs = 2.0;

        /// <summary>Mesh bytes per frame: a chunk starts only while the frame's bytes including it stay within this (the
        /// copy and GPU upload cost grows with bytes, and the clock alone only notices after the fact). The bytes other
        /// uploaders already copied in the frame (the B0 building cells) count against it
        /// (<see cref="Tick(double, double, long)"/>). Chunks are at most <see cref="TileBuild.UploadChunkBytes"/> (the
        /// same 1 MiB), so with the default budget every chunk fits a frame of its own and a frame never copies more than
        /// the budget; a budget lowered below a chunk holds it back (<see cref="MaxHeldTicks"/>).</summary>
        public long UploadBudgetBytes = 1L << 20;

        /// <summary>A chunk too big for a whole frame's <see cref="UploadBudgetBytes"/> (a budget lowered below
        /// <see cref="TileBuild.UploadChunkBytes"/>, nothing reserved) goes anyway, alone, once it has waited this many
        /// ticks, so streaming never stalls. Never with bytes reserved: a frame that shares its budget stays within it.</summary>
        public int MaxHeldTicks = 8;

        /// <summary>The focus must move this far before the selection is recomputed.</summary>
        public double ReselectDistanceM = 4.0;

        /// <summary>Receives problems (failed builds), at most once per node. May be null.</summary>
        public Action<string> Warning;

        internal PackReader Pack
        {
            get { return _pack; }
        }

        internal StreamingConfig Config
        {
            get { return _config; }
        }

        internal MeshingSettings Meshing
        {
            get { return _meshing; }
        }

        internal TileGroundQuery GroundQuery
        {
            get { return _ground; }
        }

        public TileResidency Residency
        {
            get { return _residency; }
        }

        /// <summary>The selection most recently fed to <see cref="Residency"/>.</summary>
        public IReadOnlyList<SelectedNode> Selection
        {
            get { return _selection; }
        }

        /// <summary>Changes whenever the ground query gained or lost an area (route ribbons resample on change).</summary>
        public int GroundVersion
        {
            get { return _groundVersion; }
        }

        public StreamingStats Stats
        {
            get { return _stats; }
        }

        /// <summary>Fraction of the selection that is ready (1 when nothing is selected).</summary>
        public float Progress
        {
            get { return _residency.DesiredCount == 0 ? (_hasSelection ? 1f : 0f) : (float)_residency.ReadyDesiredCount / _residency.DesiredCount; }
        }

        /// <summary>True when the whole selection is loaded and shown.</summary>
        public bool IsSettled
        {
            get
            {
                return _hasSelection && _residency.QueuedCount == 0 && _residency.LoadingCount == 0 &&
                       _residency.ReadyDesiredCount == _residency.DesiredCount;
            }
        }

        public bool IsDisposed
        {
            get { return _disposed; }
        }

        /// <summary>Settings version (<see cref="RefreshLayers"/> bumps it).</summary>
        public int SettingsVersion
        {
            get { return _settingsVersion; }
        }

        /// <summary>Ready nodes waiting for a refresh build.</summary>
        public int StaleCount
        {
            get { return _stale.Count; }
        }

        /// <summary>
        /// The meshing settings of the layers in <paramref name="layerMask"/> (<see cref="TileLayers"/> bits, e.g.
        /// <see cref="MeshingSettings.SeasonalLayers"/>) changed: every node shown or loading is rebuilt for them, the
        /// nearest first and only while nothing else is queued, and swapped in place (the old layers stay until the new
        /// ones are up). Builds already running finish with what they read and are refreshed again once up. Needs a sink
        /// that implements <see cref="ITileRefreshSink"/> (otherwise the change shows on tiles loaded from now on).
        /// Returns the nodes marked. Main thread.
        /// </summary>
        public int RefreshLayers(int layerMask)
        {
            if (_disposed || layerMask == 0) return 0;
            _settingsVersion++;
            _refreshMask |= layerMask;
            if (_refreshSink == null) return 0;
            int n = 0;
            foreach (KeyValuePair<SelectedNode, Content> kv in _content)
            {
                if (!kv.Value.HasView) continue;
                MarkStale(kv.Key);
                n++;
            }
            return n;
        }

        private void MarkStale(SelectedNode n)
        {
            if (_refreshSink != null && _staleSet.Add(n)) _stale.Add(n);
        }

        private void Unstale(SelectedNode n)
        {
            if (_staleSet.Remove(n)) _stale.Remove(n);
        }

        /// <summary>Recompute the selection on the next tick even if the focus did not move.</summary>
        public void ForceReselect()
        {
            _dirty = true;
            _resolveDirty = true;
        }

        /// <summary>Bytes of the chunk waiting at the head of the upload queue (0 when nothing waits): what a caller sharing
        /// the frame's byte budget must leave free for the tiles to make progress (<see cref="Tick(double, double, long)"/>).</summary>
        public long NextChunkBytes
        {
            get
            {
                for (int i = 0; i < _uploads.Count; i++)
                {
                    TileBuild b = _uploads[i];
                    if (b.NextChunk < b.Chunks.Count) return b.ChunkBytes(b.NextChunk);
                }
                return 0;
            }
        }

        /// <summary>One main-thread step around focus (x, z), the frame's whole <see cref="UploadBudgetBytes"/> for the
        /// tiles.</summary>
        public void Tick(double x, double z)
        {
            Tick(x, z, 0);
        }

        /// <summary>
        /// One main-thread step around focus (x, z) in a frame where other uploaders (the B0 building cells) already
        /// copied <paramref name="reservedBytes"/> of mesh data. The tile chunks of this tick then fit in what is left of
        /// <see cref="UploadBudgetBytes"/>, so the frame's uploads together stay within it: a chunk that does not fit
        /// waits for a later frame (<see cref="NextChunkBytes"/> says how much room it needs; chunks are at most
        /// <see cref="TileBuild.UploadChunkBytes"/>, so it fits any frame with nothing reserved). A caller that uploads
        /// after the tick has <c>UploadBudgetBytes - reservedBytes - Stats.TickUploadBytes</c> left.
        /// </summary>
        public void Tick(double x, double z, long reservedBytes)
        {
            if (_disposed) return;
            if (double.IsNaN(x) || double.IsNaN(z) || double.IsInfinity(x) || double.IsInfinity(z)) return;
            double dx = x - _selX, dz = z - _selZ;
            double t0 = _clockMs();
            _stats.SelectMs = 0;
            if (_dirty || !_hasSelection || dx * dx + dz * dz > ReselectDistanceM * ReselectDistanceM)
            {
                _selector.Select(x, z, _selection);
                _residency.SetDesired(_selection, x, z);
                _resolveDirty = true;
                _selX = x;
                _selZ = z;
                _dirty = false;
                _hasSelection = true;
                _stats.Selections++;
                _stats.SelectMs = _clockMs() - t0;
            }

            double t1 = _clockMs();
            TileBuild done;
            while (_finished.TryDequeue(out done))
            {
                _inFlight--;
                Collect(done);
            }
            _stats.CollectMs = _clockMs() - t1;

            Upload(reservedBytes > 0 ? reservedBytes : 0, t0);
            double t2 = _clockMs();
            _stats.ResolveMs = 0;
            _stats.GroundMs = 0;
            if (_resolveDirty)
            {
                // Only when the selection changed or a load ended: the swap rule cannot change otherwise.
                _resolveDirty = false;
                Resolve();
                UpdateFinestLevel();
                _stats.ResolveMs = _clockMs() - t2;
            }
            double t3 = _clockMs();
            Dispatch();
            UpdateStats();
            double t4 = _clockMs();
            _stats.DispatchMs = t4 - t3;
            _stats.TickMs = t4 - t0;
        }

        // ---------------------------------------------------------------------------------------------------------

        private void Collect(TileBuild b)
        {
            if (b.Generation != _generation)
            {
                ReturnToPool(b);
                return;
            }
            if (b.DecodedSource)
            {
                _decoding.Remove(b.Node.Source);
                if (b.Error == null && b.Source != null) _cache.Put(b.Node.Source, b.Source, TileMemory.EstimateBytes(b.Source));
            }
            else if (b.Source == null)
            {
                _decoding.Remove(b.Node.Source); // decode failed before the flag was set
            }
            _stats.LastBuildMs = b.WorkMs;
            if (b.RefreshMask != 0)
            {
                Content rc;
                bool live = _content.TryGetValue(b.Node, out rc) && rc.HasView;
                if (b.Error != null || !live)
                {
                    if (b.Error != null && _failed.Add(b.Node) && Warning != null) Warning("tile " + b.Node + " failed to refresh: " + b.Error.Message);
                    if (rc != null) rc.Refreshing = false;
                    ReturnToPool(b);
                    return;
                }
                b.NextChunk = 0;
                _uploads.Add(b);
                return;
            }
            if (b.Error != null)
            {
                if (_failed.Add(b.Node) && Warning != null) Warning("tile " + b.Node + " failed to build: " + b.Error.Message);
                _residency.EndLoad(b.Node, true);
                _resolveDirty = true;
                ReturnToPool(b);
                return;
            }
            if (!_residency.IsDesired(b.Node))
            {
                _residency.EndLoad(b.Node, true);
                _resolveDirty = true;
                ReturnToPool(b);
                return;
            }
            b.NextChunk = 0;
            _uploads.Add(b);
        }

        /// <summary>Whether the next chunk (<paramref name="chunkBytes"/>) may start in this tick: within the frame's bytes
        /// (<see cref="UploadBudgetBytes"/> less <paramref name="reserved"/>) and, after the first, within the time budget
        /// counted from <paramref name="tickStart"/> (the tick's own work included); a first chunk that does not fit is held
        /// (<see cref="MaxHeldTicks"/>).</summary>
        private bool MayUpload(long chunkBytes, bool uploaded, long bytes, long reserved, double tickStart)
        {
            if (!uploaded)
            {
                if (reserved + chunkBytes <= UploadBudgetBytes || reserved <= 0 && _heldTicks >= MaxHeldTicks) return true;
                _held = true;
                return false;
            }
            return reserved + bytes + chunkBytes <= UploadBudgetBytes && _clockMs() - tickStart < UploadBudgetMs;
        }

        /// <summary>Uploads chunks within the frame's bytes (less <paramref name="reserved"/>) and the time left of the tick
        /// that started at <paramref name="tickStart"/>.</summary>
        private void Upload(long reserved, double tickStart)
        {
            double start = _clockMs();
            bool uploaded = false;
            long bytes = 0;
            int chunks = 0;
            _held = false;
            while (_uploads.Count > 0)
            {
                TileBuild b = _uploads[0];
                if (b.RefreshMask != 0)
                {
                    int step = UploadRefresh(b, uploaded, bytes, reserved, tickStart);
                    if (step < 0) break;
                    if (step > 0)
                    {
                        bytes += b.ChunkBytes(b.NextChunk - 1);
                        chunks++;
                        uploaded = true;
                    }
                    continue;
                }
                if (!_residency.IsDesired(b.Node))
                {
                    // Dropped out of the selection mid-upload.
                    Content partial;
                    if (_partial.TryGetValue(b.Node, out partial))
                    {
                        if (partial.HasView) _sink.Release(b.Node);
                        _partial.Remove(b.Node);
                        ReturnContent(partial);
                    }
                    _residency.EndLoad(b.Node, true);
                    _resolveDirty = true;
                    _uploads.RemoveAt(0);
                    ReturnToPool(b);
                    continue;
                }
                if (b.NextChunk < b.Chunks.Count)
                {
                    if (!MayUpload(b.ChunkBytes(b.NextChunk), uploaded, bytes, reserved, tickStart)) break;
                    try
                    {
                        _sink.Upload(b, b.NextChunk);
                    }
                    catch (Exception e)
                    {
                        // A broken upload must not stall the queue: drop the node (it shows as a hole) and go on.
                        if (_failed.Add(b.Node) && Warning != null) Warning("tile " + b.Node + " failed to upload: " + e.Message);
                        Content broken;
                        if (_partial.TryGetValue(b.Node, out broken))
                        {
                            _partial.Remove(b.Node);
                            ReturnContent(broken);
                        }
                        _sink.Release(b.Node);
                        _residency.EndLoad(b.Node, true);
                        _resolveDirty = true;
                        _uploads.RemoveAt(0);
                        ReturnToPool(b);
                        continue;
                    }
                    Content c;
                    if (!_partial.TryGetValue(b.Node, out c))
                    {
                        c = RentContent();
                        _partial[b.Node] = c;
                    }
                    c.HasView = true;
                    long chunkBytes = b.ChunkBytes(b.NextChunk);
                    c.Bytes += chunkBytes;
                    c.LayerBytes[b.Chunks[b.NextChunk].Layer] += chunkBytes;
                    bytes += chunkBytes;
                    chunks++;
                    b.NextChunk++;
                    uploaded = true;
                    continue;
                }

                // Every chunk is up: the node is ready.
                Content content;
                if (!_partial.TryGetValue(b.Node, out content)) content = RentContent();
                _partial.Remove(b.Node);
                content.Sampler = b.Sampler;
                content.Roads = b.Roads;
                content.Level = b.Node.Area.Level;
                bool empty = !content.HasView && (b.Sampler == null || !b.Sampler.HasHeights);
                _resolveDirty = true;
                if (_residency.EndLoad(b.Node, empty))
                {
                    _content[b.Node] = content;
                    _stats.MeshBytes += content.Bytes;
                    content.Version = b.SettingsVersion;
                    if (content.HasView) _sink.Ready(b.Node, b.TakeExtras());
                    // Built before the settings last changed: refresh what changed once nothing else waits.
                    if (content.HasView && b.SettingsVersion != _settingsVersion) MarkStale(b.Node);
                }
                else
                {
                    if (content.HasView) _sink.Release(b.Node);
                    ReturnContent(content);
                }
                _uploads.RemoveAt(0);
                ReturnToPool(b);
            }
            double ms = _clockMs() - start;
            _stats.UploadMs = ms;
            _stats.TickUploadBytes = bytes;
            _stats.TickUploadChunks = chunks;
            if (chunks > 0) _heldTicks = 0;
            else if (_held) _heldTicks++;
            if (start - _worstWindowStart > 1000.0)
            {
                _stats.WorstUploadMs = _worstInWindow;
                _worstInWindow = 0;
                _worstWindowStart = start;
            }
            if (ms > _worstInWindow) _worstInWindow = ms;
            if (ms > _stats.WorstUploadMs) _stats.WorstUploadMs = ms;
        }

        /// <summary>One step of a refresh build's upload: -1 = the tick's budget is spent (stop), 1 = a chunk was staged,
        /// 0 = the build was committed or dropped (nothing uploaded).</summary>
        private int UploadRefresh(TileBuild b, bool uploaded, long bytes, long reserved, double tickStart)
        {
            Content rc;
            if (!_content.TryGetValue(b.Node, out rc) || !rc.HasView || _refreshSink == null)
            {
                // Released meanwhile (its view, staged chunks included, is gone).
                _uploads.RemoveAt(0);
                ReturnToPool(b);
                return 0;
            }
            if (b.NextChunk < b.Chunks.Count)
            {
                if (!MayUpload(b.ChunkBytes(b.NextChunk), uploaded, bytes, reserved, tickStart)) return -1;
                try
                {
                    _refreshSink.UploadRefresh(b, b.NextChunk);
                }
                catch (Exception e)
                {
                    if (_failed.Add(b.Node) && Warning != null) Warning("tile " + b.Node + " failed to refresh: " + e.Message);
                    _refreshSink.AbortRefresh(b.Node);
                    rc.Refreshing = false;
                    _uploads.RemoveAt(0);
                    ReturnToPool(b);
                    return 0;
                }
                b.NextChunk++;
                return 1;
            }
            _refreshSink.CommitRefresh(b.Node, b.RefreshMask);
            for (int l = 0; l < TileLayers.Count; l++)
            {
                if ((b.RefreshMask & (1 << l)) == 0) continue;
                long nb = b.LayerBytes(l);
                _stats.MeshBytes += nb - rc.LayerBytes[l];
                rc.Bytes += nb - rc.LayerBytes[l];
                rc.LayerBytes[l] = nb;
            }
            rc.Refreshing = false;
            rc.Version = b.SettingsVersion;
            if (rc.Version != _settingsVersion) MarkStale(b.Node);
            _uploads.RemoveAt(0);
            ReturnToPool(b);
            return 0;
        }

        private void Resolve()
        {
            _residency.Resolve(_show, _hide, _release);
            for (int i = 0; i < _release.Count; i++)
            {
                SelectedNode n = _release[i];
                Content c;
                if (!_content.TryGetValue(n, out c)) continue;
                if (c.InGround)
                {
                    double g0 = _clockMs();
                    _ground.Remove(n.Area);
                    _stats.GroundMs += _clockMs() - g0;
                    _groundVersion++;
                }
                if (c.HasView) _sink.Release(n);
                _stats.MeshBytes -= c.Bytes;
                _content.Remove(n);
                Unstale(n);
                ReturnContent(c);
            }
            for (int i = 0; i < _hide.Count; i++)
            {
                SelectedNode n = _hide[i];
                Content c;
                if (!_content.TryGetValue(n, out c)) continue;
                if (c.HasView) _sink.SetVisible(n, false);
                if (c.InGround)
                {
                    double g0 = _clockMs();
                    _ground.Remove(n.Area);
                    _stats.GroundMs += _clockMs() - g0;
                    c.InGround = false;
                    _groundVersion++;
                }
            }
            for (int i = 0; i < _show.Count; i++)
            {
                SelectedNode n = _show[i];
                Content c;
                if (!_content.TryGetValue(n, out c)) continue;
                if (c.HasView) _sink.SetVisible(n, true);
                if (c.Sampler != null && c.Sampler.HasHeights)
                {
                    double g0 = _clockMs();
                    _ground.Add(n.Area, c.Sampler, c.Roads);
                    _stats.GroundMs += _clockMs() - g0;
                    c.InGround = true;
                    _groundVersion++;
                }
            }
        }

        private void Dispatch()
        {
            IReadOnlyList<SelectedNode> queue = _residency.Queue;
            int i = 0;
            while (i < queue.Count && _inFlight < MaxConcurrentJobs && _uploads.Count + _inFlight < MaxPendingUploads + MaxConcurrentJobs &&
                   _residency.CanStartLoad)
            {
                SelectedNode n = queue[i];
                TileData source;
                bool cached = _cache.TryGet(n.Source, out source);
                if (!cached && _decoding.Contains(n.Source))
                {
                    i++; // another build is decoding this source; it will be cached shortly
                    continue;
                }
                TileBuild b = RentBuild();
                b.Reset(this, n, cached ? source : null, _generation);
                if (!cached) _decoding.Add(n.Source);
                _residency.BeginLoad(n); // removes n from the queue
                b.SettingsVersion = _settingsVersion;
                _inFlight++;
                Interlocked.Increment(ref _running);
                _runner.Run(b.Work);
            }
            if (_stale.Count > 0 && _residency.QueuedCount == 0) DispatchRefresh();
        }

        /// <summary>Refresh builds for the nearest stale nodes while the job and upload limits allow.</summary>
        private void DispatchRefresh()
        {
            while (_inFlight < MaxConcurrentJobs && _uploads.Count + _inFlight < MaxPendingUploads + MaxConcurrentJobs)
            {
                int best = -1;
                double bestD = double.MaxValue;
                for (int i = 0; i < _stale.Count; i++)
                {
                    SelectedNode s = _stale[i];
                    Content sc;
                    if (!_content.TryGetValue(s, out sc) || !sc.HasView)
                    {
                        _staleSet.Remove(s);
                        _stale.RemoveAt(i--);
                        continue;
                    }
                    if (sc.Refreshing || !_residency.IsDesired(s)) continue;
                    double cx = s.Area.X0 + 0.5 * s.Area.Size - _selX, cz = s.Area.Z0 + 0.5 * s.Area.Size - _selZ;
                    double d = cx * cx + cz * cz;
                    if (d < bestD)
                    {
                        bestD = d;
                        best = i;
                    }
                }
                if (best < 0) return;
                SelectedNode n = _stale[best];
                TileData source;
                bool cached = _cache.TryGet(n.Source, out source);
                if (!cached && _decoding.Contains(n.Source)) return; // wait for the decode in flight
                _staleSet.Remove(n);
                _stale.RemoveAt(best);
                Content c = _content[n];
                c.Refreshing = true;
                TileBuild b = RentBuild();
                b.Reset(this, n, cached ? source : null, _generation);
                b.RefreshMask = _refreshMask;
                b.SettingsVersion = _settingsVersion;
                if (!cached) _decoding.Add(n.Source);
                _inFlight++;
                Interlocked.Increment(ref _running);
                _runner.Run(b.Work);
            }
        }

        /// <summary>Worker thread: a build finished (or failed).</summary>
        internal void OnWorkerDone(TileBuild b)
        {
            _finished.Enqueue(b);
            if (Interlocked.Decrement(ref _running) == 0 && _disposed) DisposePackOnce();
        }

        private void UpdateStats()
        {
            _stats.Desired = _residency.DesiredCount;
            _stats.ReadyDesired = _residency.ReadyDesiredCount;
            _stats.Visible = _residency.VisibleCount;
            _stats.Ready = _residency.ReadyCount;
            _stats.Loading = _residency.LoadingCount;
            _stats.Queued = _residency.QueuedCount;
            _stats.Jobs = _inFlight;
            _stats.PendingUploads = _uploads.Count;
            _stats.Failed = _failed.Count;
            _stats.CachedTiles = _cache.Count;
            _stats.CachedBytes = _cache.Bytes;
        }

        private void UpdateFinestLevel()
        {
            int finest = -1;
            foreach (KeyValuePair<SelectedNode, Content> kv in _content)
                if (kv.Value.HasView && kv.Value.Level > finest && _residency.IsVisible(kv.Key)) finest = kv.Value.Level;
            _stats.FinestVisibleLevel = finest;
        }

        private Content RentContent()
        {
            return _contentPool.Count > 0 ? _contentPool.Pop() : new Content();
        }

        private void ReturnContent(Content c)
        {
            c.Sampler = null;
            c.Roads = null;
            c.HasView = false;
            c.InGround = false;
            c.Bytes = 0;
            Array.Clear(c.LayerBytes, 0, c.LayerBytes.Length);
            c.Level = 0;
            c.Version = 0;
            c.Refreshing = false;
            _contentPool.Push(c);
        }

        /// <summary>A recycled build, or a new one when the pool is empty (<see cref="StreamingStats.BuildsCreated"/>).</summary>
        private TileBuild RentBuild()
        {
            if (_pool.Count > 0) return _pool.Pop();
            _stats.BuildsCreated++;
            return new TileBuild();
        }

        /// <summary>Recycles a build into the pool, which keeps as many as can be alive at once (in flight and waiting to
        /// upload), so none is ever dropped with its grown buffers and riding creates no new ones (perf pass: with room
        /// for one fewer, every fourth build went to the collector and a new one was made a moment later).</summary>
        private void ReturnToPool(TileBuild b)
        {
            b.Recycle();
            if (_pool.Count < MaxConcurrentJobs + MaxPendingUploads) _pool.Push(b);
        }

        /// <summary>A short multi-line summary (allocates; for the debug HUD a few times a second).</summary>
        public string Describe()
        {
            var sb = new StringBuilder(160);
            sb.Append(_stats.Format());
            return sb.ToString();
        }

        /// <summary>
        /// Release every view and ground entry and stop streaming. Builds still running finish on their workers and are
        /// dropped; an owned pack is closed after the last of them.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _generation++;
            _residency.Clear(_release);
            foreach (KeyValuePair<SelectedNode, Content> kv in _content)
            {
                if (kv.Value.InGround) _ground.Remove(kv.Key.Area);
                if (kv.Value.HasView) _sink.Release(kv.Key);
            }
            _content.Clear();
            foreach (KeyValuePair<SelectedNode, Content> kv in _partial)
                if (kv.Value.HasView) _sink.Release(kv.Key);
            _partial.Clear();
            for (int i = 0; i < _uploads.Count; i++) _uploads[i].Recycle();
            _uploads.Clear();
            _cache.Clear();
            _decoding.Clear();
            _stale.Clear();
            _staleSet.Clear();
            _stats.MeshBytes = 0;
            _groundVersion++;
            if (Volatile.Read(ref _running) == 0) DisposePackOnce();
        }

        private void DisposePackOnce()
        {
            if (!_ownsPack) return;
            if (Interlocked.Exchange(ref _packDisposed, 1) == 0) _pack.Dispose();
        }
    }
}
