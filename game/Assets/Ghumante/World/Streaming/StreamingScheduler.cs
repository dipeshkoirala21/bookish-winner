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

        public double WorstUploadMs;

        /// <summary>Worker milliseconds of the last finished build.</summary>
        public double LastBuildMs;

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
    /// <see cref="TileBuild.UploadChunkVertices"/> vertices each) through the <see cref="ITileSink"/> until
    /// <see cref="UploadBudgetMs"/> or <see cref="UploadBudgetBytes"/> is spent (at least one chunk per tick so
    /// streaming never stalls);</item>
    /// <item>applies the swap rule (<see cref="TileResidency.Resolve"/>) to views and to the ground query, which therefore
    /// always describes exactly what is drawn;</item>
    /// <item>dispatches the nearest queued nodes to <see cref="ITileJobRunner"/>, at most <see cref="MaxConcurrentJobs"/>
    /// at once, never decoding one source tile twice at the same time.</item>
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
        private int _groundVersion;

        /// <summary>What a resident node keeps after its build was uploaded.</summary>
        private sealed class Content
        {
            public TileHeightSampler Sampler;
            public RoadSpatialIndex Roads;
            public bool HasView;
            public bool InGround;
            public long Bytes;
            public int Level;
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

        /// <summary>Main-thread milliseconds per tick for mesh uploads (2 ms at 30 fps, 1.5 ms at 60 fps).</summary>
        public double UploadBudgetMs = 2.0;

        /// <summary>Mesh bytes per tick: no further chunk starts once this many were uploaded in the tick (the copy and
        /// GPU upload cost grows with bytes, and the clock alone only notices after the fact).</summary>
        public long UploadBudgetBytes = 1L << 20;

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

        /// <summary>Recompute the selection on the next tick even if the focus did not move.</summary>
        public void ForceReselect()
        {
            _dirty = true;
            _resolveDirty = true;
        }

        /// <summary>One main-thread step around focus (x, z).</summary>
        public void Tick(double x, double z)
        {
            if (_disposed) return;
            if (double.IsNaN(x) || double.IsNaN(z) || double.IsInfinity(x) || double.IsInfinity(z)) return;
            double dx = x - _selX, dz = z - _selZ;
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
            }

            TileBuild done;
            while (_finished.TryDequeue(out done))
            {
                _inFlight--;
                Collect(done);
            }

            Upload();
            if (_resolveDirty)
            {
                // Only when the selection changed or a load ended: the swap rule cannot change otherwise.
                _resolveDirty = false;
                Resolve();
                UpdateFinestLevel();
            }
            Dispatch();
            UpdateStats();
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

        private void Upload()
        {
            double start = _clockMs();
            bool uploaded = false;
            long bytes = 0;
            while (_uploads.Count > 0)
            {
                TileBuild b = _uploads[0];
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
                    if (uploaded && (bytes >= UploadBudgetBytes || _clockMs() - start >= UploadBudgetMs)) break;
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
                    bytes += chunkBytes;
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
            if (start - _worstWindowStart > 1000.0)
            {
                _stats.WorstUploadMs = _worstInWindow;
                _worstInWindow = 0;
                _worstWindowStart = start;
            }
            if (ms > _worstInWindow) _worstInWindow = ms;
            if (ms > _stats.WorstUploadMs) _stats.WorstUploadMs = ms;
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
                    _ground.Remove(n.Area);
                    _groundVersion++;
                }
                if (c.HasView) _sink.Release(n);
                _stats.MeshBytes -= c.Bytes;
                _content.Remove(n);
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
                    _ground.Remove(n.Area);
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
                    _ground.Add(n.Area, c.Sampler, c.Roads);
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
                TileBuild b = _pool.Count > 0 ? _pool.Pop() : new TileBuild();
                b.Reset(this, n, cached ? source : null, _generation);
                if (!cached) _decoding.Add(n.Source);
                _residency.BeginLoad(n); // removes n from the queue
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
            c.Level = 0;
            _contentPool.Push(c);
        }

        private void ReturnToPool(TileBuild b)
        {
            b.Recycle();
            if (_pool.Count < MaxConcurrentJobs + 1) _pool.Push(b);
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
