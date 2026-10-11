using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Generators;
using Ghumante.Core.Meshing;
using Ghumante.World.Streaming;

namespace Ghumante.World.Buildings
{
    /// <summary>A detail tile the B0 cells draw on (DetailCells maps the streamer's tile view to one).</summary>
    public sealed class B0Tile
    {
        public B0Tile(TileId id, TileData source, IHeightSampler sampler, object owner)
        {
            Id = id;
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
            Owner = owner;
            BlocksPerSide = MeshParts.BlocksPerSide(id.Size, BandConfig.B1BlockM);
            BlockReady = new bool[BlocksPerSide * BlocksPerSide];
        }

        public readonly TileId Id;
        public readonly TileData Source;
        public readonly IHeightSampler Sampler;

        /// <summary>The presenter's view of the tile (its cells' meshes are parented to it).</summary>
        public readonly object Owner;

        /// <summary>True while the tile is drawn (its cells may show and register colliders).</summary>
        public bool Visible { get; internal set; }

        internal B0HouseIndex Index;
        internal bool Removed;
        internal readonly Dictionary<int, B0CellSet.Cell> Cells = new Dictionary<int, B0CellSet.Cell>();
        internal readonly int BlocksPerSide;

        /// <summary>Per B1 block (<see cref="BandConfig.B1BlockM"/>): every B0 cell the camera needs in it draws every house
        /// only B0 draws, so the block may hand its inner range to B0.</summary>
        internal readonly bool[] BlockReady;
    }

    /// <summary>Uploads and shows the meshes of finished B0 cells (DetailCells: Unity meshes and renderers).</summary>
    public interface IB0CellSink
    {
        /// <summary>One upload chunk of a cell (positions tile-local; indices relative to the chunk's first vertex, 16-bit
        /// copies in <paramref name="shortIndices"/> when <see cref="UploadChunk.UsesShortIndices"/>), hidden until
        /// <see cref="SetShown"/>. Returns its handle. Main thread.</summary>
        object Upload(B0Tile tile, MeshData data, in UploadChunk chunk, ushort[] shortIndices);

        void SetShown(object handle, bool shown);

        /// <summary>Frees a chunk (its mesh; its renderer may be pooled).</summary>
        void Destroy(object handle);
    }

    /// <summary>
    /// The B0 band (W2_DESIGN 2.4; the detail pass band table) around the camera, engine-free: which cells are drawn,
    /// which houses each draws and at which level, when cells are (re)built, uploaded, shown, cached and evicted, and
    /// their structure colliders. DetailCells puts it on screen through <see cref="IB0CellSink"/>; the budget tests
    /// drive it directly.
    /// <list type="bullet">
    /// <item><b>Each house once.</b> A cell (32 m on Mid and High, 64 m on Low: <see cref="BandConfig.B0FineCells"/>)
    /// draws the houses of its <see cref="B0HouseIndex"/> list whose geometry meets B0 and its fade into B1, each at one
    /// level (<see cref="BandConfig.B0Level"/>): the full grammar near the camera, the lite level for the street fronts
    /// beyond, the flat level for the rest; a house changes level only when its cell is rebuilt, with hysteresis. There
    /// is no near or lite ring to overlap: B0 is one band with one material.</item>
    /// <item><b>Selection</b> (<see cref="B0HouseIndex.Select"/>): a cell is built with the houses that meet B0 plus
    /// <see cref="BandConfig.B0BuildPadM"/> from where the camera is. It must draw every house nearer than B0's fade into B1
    /// (<see cref="B0HouseIndex.Meets"/> with a pad of -<see cref="BandConfig.FadeM"/>); a house only in the fade may be
    /// missing for a moment (B1 dithers it in). A cell is rebuilt, nearest first, once a house it lacks enters the fade,
    /// a house needs a richer level, or (lazily) it holds many houses it no longer needs; the old version shows until
    /// the new one is up. A B1 block hands its inner range to B0 only while every needed cell in it draws its houses
    /// (<see cref="BlockReady"/>); otherwise the block's B1 stands in, so there is never a hole.</item>
    /// <item><b>Builds</b> run on a worker in slices of at most <see cref="SliceMs"/> (one house at least; one slice in
    /// flight; the worker busy at most <see cref="WorkerDuty"/> of the time), so a dense cell never holds a worker for
    /// long and a stale build is dropped between slices. The last slice splits the cell into chunks of at most
    /// <see cref="ChunkVertices"/> vertices with 16-bit indices.</item>
    /// <item><b>Uploads</b> go chunk by chunk through <see cref="Upload"/> within the bytes the caller gives (the
    /// streamer shares its per-frame upload cap between tiles and B0).</item>
    /// <item><b>Cache</b>: uploaded cells stay resident up to <see cref="BandConfig.CellCacheBytes"/> of GPU mesh, the
    /// least recently needed evicted first; cells needed this frame are never evicted.</item>
    /// <item><b>Colliders</b> (pikha aprons, flat roofs, stair cabins, temple plinths) of a shown cell are registered with
    /// the ground under the key of the quadtree tile the cell coincides with, in that tile's frame (<see cref="CellKey"/>),
    /// so a cell showing or hiding registers or drops only its own small set.</item>
    /// </list>
    /// Main thread, apart from the slice jobs. Allocation-free per frame once warm (cells and their versions are
    /// allocated when built).
    /// </summary>
    public sealed class B0CellSet : IDisposable
    {
        /// <summary>Most worker milliseconds one build slice takes (W2_DESIGN 10.4: 4 ms); a slice always builds at least
        /// one house.</summary>
        public const double SliceMs = 4.0;

        /// <summary>Share of one worker's time the B0 slices may take: after a slice the next waits three times its
        /// length.</summary>
        public const double WorkerDuty = 0.25;

        /// <summary>Most vertices of one B0 upload chunk: about 0.5 MB with UV0 and 16-bit indices, half the per-frame
        /// upload cap, so a chunk and a tile chunk share a frame.</summary>
        public const int ChunkVertices = 12288;

        /// <summary>Built cells waiting for upload before no new build starts.</summary>
        public const int MaxReady = 2;

        /// <summary>A cell holding at least a third of houses it no longer needs is rebuilt (lazily) once the camera has
        /// moved this far from where it was built.</summary>
        public const double WasteMoveM = 16.0;

        private const int RequiredLevel = 0;

        internal sealed class Version
        {
            public Cell Cell;
            public int[] Positions;
            public byte[] Levels;
            public ulong[] Mask;
            public double CamX, CamZ;
            public BuildingOptions[] Options;

            /// <summary>Per position: the position in <see cref="Reuse"/> whose house is copied (same house, same level), or
            /// -1 to build it.</summary>
            public int[] From;

            /// <summary>The cell's live version whose houses are copied, and its mesh (kept while the cell is needed; the
            /// worker holds this reference even if the main thread lets the version go).</summary>
            public Version Reuse;

            public MeshData ReuseData;

            /// <summary>Per position k, 6 entries from 6k: first vertex, first index, first box, ramp, cylinder and wall
            /// (and the ends at 6 × count).</summary>
            public int[] Ranges;

            public MeshData Data;
            public readonly List<UploadChunk> Chunks = new List<UploadChunk>(4);
            public ushort[] Shorts;
            public StructureColliders Colliders;
            public object[] Handles;
            public int Uploaded;
            public long Bytes, UploadedBytes;
            public int Tris, Next;
            public bool Built, Abandoned;
            public Exception Error;
            public double WorkMs;
        }

        internal sealed class Cell
        {
            public B0Tile Tile;
            public int X, Z, Block;
            public Version Live, Pending;
            public double LastWantedMs;

            /// <summary>Last frame the cell had houses in B0 (it is kept, shown when ready, and built ahead).</summary>
            public long WantedFrame = -1;

            public bool Shown, Failed, Registered;
        }

        private readonly BandConfig _bands;
        private readonly BuildingOptions _shared;
        private readonly IB0CellSink _sink;
        private readonly IStructureGround _ground;
        private readonly ITileJobRunner _runner;
        private readonly Func<double> _clockMs;
        private readonly bool _fine;
        private readonly List<B0Tile> _tiles = new List<B0Tile>(16);
        private readonly List<Cell> _all = new List<Cell>(128);
        private readonly List<Version> _ready = new List<Version>(MaxReady + 1);
        private readonly List<int> _select = new List<int>(64);
        private readonly Stack<MeshData> _meshPool = new Stack<MeshData>(4);
        private readonly GenColliders _gen = new GenColliders();
        private readonly Action _runSlice, _runIndex;
        private long _frame;
        private int _unindexed;
        private double _nextSliceMs = double.NegativeInfinity;
        private volatile bool _running;
        private Version _job;
        private TileData _indexJob;
        private Exception _indexError;
        private B0Tile _bestTile;
        private int _bestX, _bestZ, _bestReason;
        private double _bestScore;
        private bool _disposed;

        /// <summary>Reports a cell whose build threw (it is left to its B1 block).</summary>
        public Action<string> Warning;

        /// <param name="bands">The tier's radii, levels and cache budget.</param>
        /// <param name="shared">The streamer's B0 options (corridors, hide set, sink), copied per level.</param>
        /// <param name="sink">Uploads and shows the chunks.</param>
        /// <param name="ground">Takes the cells' structure colliders (null: none).</param>
        /// <param name="runner">Runs the build slices (<see cref="ThreadPoolJobRunner"/> in the game).</param>
        /// <param name="clockMs">Milliseconds (pacing and slice timing).</param>
        public B0CellSet(BandConfig bands, BuildingOptions shared, IB0CellSink sink, IStructureGround ground, ITileJobRunner runner, Func<double> clockMs)
        {
            _bands = bands ?? throw new ArgumentNullException(nameof(bands));
            _shared = shared ?? new BuildingOptions { Band = BuildingBand.B0KitLite };
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _ground = ground;
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _clockMs = clockMs ?? throw new ArgumentNullException(nameof(clockMs));
            _fine = bands.B0FineCells;
            _runSlice = RunSlice;
            _runIndex = RunIndex;
        }

        /// <summary>Triangles of the cells shown this frame.</summary>
        public int ShownTris { get; private set; }

        /// <summary>Cells and upload chunks (draw calls) shown this frame.</summary>
        public int ShownCount { get; private set; }

        public int ShownChunks { get; private set; }

        /// <summary>GPU bytes of the resident cell meshes (shown, cached and partly uploaded).</summary>
        public long ResidentBytes { get; private set; }

        /// <summary>Cells alive (resident, building or needed).</summary>
        public int CellCount
        {
            get { return _all.Count; }
        }

        /// <summary>Cell versions built, houses built, slices run, worker milliseconds and bytes uploaded so far.</summary>
        public int Builds { get; private set; }

        public int HousesBuilt { get; private set; }

        /// <summary>Builds started per reason: a house only B0 draws was missing (the cell's block stood in), a house wanted
        /// the near level, a house entered the fade into B1, a near-level house left the near radius, a street front wanted
        /// the lite level, lazy (other demotions, waste).</summary>
        public readonly int[] BuildsByReason = new int[6];

        /// <summary>Houses copied from a cell's previous version instead of built (same house, same level).</summary>
        public int HousesReused { get; private set; }

        public int Slices { get; private set; }

        public double WorkMs { get; private set; }

        public long UploadedBytes { get; private set; }

        /// <summary>Collider sets registered with the ground so far (each one cell's).</summary>
        public int ColliderRegistrations { get; private set; }

        /// <summary>B1 blocks standing in for B0 cells this frame (a needed cell lacks a house only B0 draws).</summary>
        public int BlocksWaiting { get; private set; }

        /// <summary>True while a build slice runs on a worker.</summary>
        public bool Busy
        {
            get { return _running; }
        }

        /// <summary>True when B0 has nothing left to build or upload for the camera of the last <see cref="Update"/>.</summary>
        public bool Settled
        {
            get { return !_running && _job == null && _ready.Count == 0 && _bestTile == null && BlocksWaiting == 0 && _unindexed == 0; }
        }

        // ------------------------------------------------------------------------------------------------------------
        // Tiles

        /// <summary>Starts drawing B0 on a detail tile (visible from now on).</summary>
        public B0Tile Add(TileId id, TileData source, IHeightSampler sampler, object owner)
        {
            var t = new B0Tile(id, source, sampler, owner) { Visible = true };
            _tiles.Add(t);
            return t;
        }

        /// <summary>Shows or hides a tile's cells (hidden: they stay cached, their colliders leave the ground).</summary>
        public void SetVisible(B0Tile t, bool visible)
        {
            if (t == null || t.Removed || t.Visible == visible) return;
            t.Visible = visible;
            if (visible) return;
            foreach (Cell c in t.Cells.Values) SetShown(c, false);
        }

        /// <summary>The tile is released: its cells are destroyed.</summary>
        public void Remove(B0Tile t)
        {
            if (t == null || t.Removed) return;
            SetVisible(t, false);
            t.Removed = true;
            foreach (Cell c in t.Cells.Values)
            {
                Drop(c.Live);
                c.Live = null;
                Drop(c.Pending);
                c.Pending = null;
                c.Tile = null;
            }
            t.Cells.Clear();
            for (int i = _all.Count - 1; i >= 0; i--)
                if (_all[i].Tile == null) _all.RemoveAt(i);
            _tiles.Remove(t);
        }

        // ------------------------------------------------------------------------------------------------------------
        // Per frame

        /// <summary>Per frame: find the cells the camera (game metres) needs, show the ready ones, start or continue a
        /// build slice and evict beyond the cache budget.</summary>
        public void Update(double camX, double camZ)
        {
            if (_disposed) return;
            _frame++;
            double now = _clockMs();
            ShownTris = 0;
            ShownCount = 0;
            ShownChunks = 0;
            BlocksWaiting = 0;
            _unindexed = 0;
            _bestTile = null;
            _bestScore = double.MaxValue;
            for (int i = 0; i < _tiles.Count; i++)
            {
                B0Tile t = _tiles[i];
                // Until its house index is built (on a worker), the tile's B1 stands in everywhere.
                bool indexed = t.Visible && (t.Index != null || B0HouseIndex.TryGet(t.Source, out t.Index));
                for (int b = 0; b < t.BlockReady.Length; b++) t.BlockReady[b] = indexed;
                if (!indexed)
                {
                    if (t.Visible) _unindexed++;
                    continue;
                }
                Mark(t, camX - t.Id.X0, camZ - t.Id.Z0, now);
                for (int b = 0; b < t.BlockReady.Length; b++)
                    if (!t.BlockReady[b]) BlocksWaiting++;
            }

            for (int i = 0; i < _all.Count; i++)
            {
                Cell c = _all[i];
                // A cell no longer needed keeps only its GPU mesh (cached); its CPU copy is for the next rebuild.
                if (c.WantedFrame != _frame && c.Live != null && c.Live.Data != null) ReleaseData(c.Live);
                bool on = c.Tile.Visible && c.WantedFrame == _frame && c.Live != null && !c.Failed && c.Tile.BlockReady[c.Block];
                // (A needed cell without a version yet and no house only B0 draws leaves its block to B0: nothing is lost.)
                if (on != c.Shown) SetShown(c, on);
                if (on)
                {
                    ShownTris += c.Live.Tris;
                    ShownCount++;
                    ShownChunks += c.Live.Uploaded;
                }
            }

            Dispatch(camX, camZ, now);
            Evict();
        }

        /// <summary>The needed cells of a tile: marks them, the blocks whose cells lack a house, and the best cell to
        /// (re)build.</summary>
        private void Mark(B0Tile t, double cx, double cz, double now)
        {
            B0HouseIndex idx = t.Index;
            float outer = _bands.B0OuterM;
            int per = idx.PerSide(_fine);
            double side = idx.CellSide(_fine);
            double reach = outer + BandConfig.FadeM * 0.5 + idx.Spill(_fine);
            int x0 = Math.Max(0, (int)Math.Floor((cx - reach) / side)), x1 = Math.Min(per - 1, (int)Math.Floor((cx + reach) / side));
            int z0 = Math.Max(0, (int)Math.Floor((cz - reach) / side)), z1 = Math.Min(per - 1, (int)Math.Floor((cz + reach) / side));
            for (int iz = z0; iz <= z1; iz++)
                for (int ix = x0; ix <= x1; ix++)
                {
                    if (idx.Select(_fine, ix, iz, outer, cx, cz, 0, null) == 0) continue;
                    int key = iz * 4096 + ix;
                    Cell c;
                    if (!t.Cells.TryGetValue(key, out c))
                    {
                        c = new Cell
                        {
                            Tile = t, X = ix, Z = iz, Block = MeshParts.BlockOf((ix + 0.5) * side, (iz + 0.5) * side, BandConfig.B1BlockM, t.BlocksPerSide),
                        };
                        t.Cells.Add(key, c);
                        _all.Add(c);
                    }
                    c.WantedFrame = _frame;
                    c.LastWantedMs = now;
                    Version live = c.Live;
                    // Every house nearer than the fade into B1 must be drawn; one in the fade may wait for the rebuild.
                    bool covering = idx.Covers(_fine, ix, iz, outer, cx, cz, -BandConfig.FadeM, live != null ? live.Mask : null);
                    if (!covering || c.Failed) t.BlockReady[c.Block] = false;
                    if (c.Pending != null || c.Failed) continue;
                    // Why the cell needs a (re)build, most urgent first: it leaves its B1 block standing in (a house only
                    // B0 draws is missing), a house wants the near level, a house entered the fade, a street front wants
                    // the lite level, or (lazily) houses want a poorer level or many are no longer needed.
                    int reason;
                    if (!covering) reason = ReasonHole;
                    else if (live == null || !idx.Covers(_fine, ix, iz, outer, cx, cz, 0, live.Mask)) reason = ReasonFade;
                    else
                    {
                        int richest;
                        bool poorer, nearPoorer;
                        Levels(idx, live, cx, cz, out richest, out poorer, out nearPoorer);
                        double mx = cx - live.CamX, mz = cz - live.CamZ;
                        if (richest == BandConfig.LevelNear) reason = ReasonNear;
                        else if (nearPoorer) reason = ReasonLeaveNear;
                        else if (richest == BandConfig.LevelLite) reason = ReasonLite;
                        else if (poorer || mx * mx + mz * mz > WasteMoveM * WasteMoveM &&
                                 3 * idx.Unneeded(_fine, ix, iz, outer, cx, cz, _bands.B0BuildPadM, live.Mask) >= Math.Max(3, live.Positions.Length))
                            reason = ReasonLazy;
                        else continue;
                    }
                    float bx0, bz0, bx1, bz1;
                    idx.CellBounds(_fine, ix, iz, out bx0, out bz0, out bx1, out bz1);
                    double score = BandConfig.NearestDistance(bx0, bz0, bx1, bz1, cx, cz) + ReasonCost[reason];
                    if (score < _bestScore)
                    {
                        _bestScore = score;
                        _bestTile = t;
                        _bestX = ix;
                        _bestZ = iz;
                        _bestReason = reason;
                    }
                }
        }

        /// <summary>The richest level a house of a version wants above the one it is built at (-1: none), whether any wants
        /// a poorer one, and whether a house built at the near level (the costly one) does.</summary>
        private void Levels(B0HouseIndex idx, Version v, double cx, double cz, out int richest, out bool poorer, out bool nearPoorer)
        {
            richest = -1;
            poorer = false;
            nearPoorer = false;
            int[] houses = idx.Houses(_fine, v.Cell.X, v.Cell.Z);
            for (int k = 0; k < v.Positions.Length; k++)
            {
                int i = houses[v.Positions[k]];
                int built = v.Levels[k];
                int want = _bands.B0Level(idx.CentroidDistance(i, cx, cz), idx.Street(i), built);
                if (want < built && (richest < 0 || want < richest)) richest = want;
                else if (want > built)
                {
                    poorer = true;
                    if (built == BandConfig.LevelNear) nearPoorer = true;
                }
            }
        }

        private const int ReasonHole = 0, ReasonNear = 1, ReasonFade = 2, ReasonLeaveNear = 3, ReasonLite = 4, ReasonLazy = 5;

        /// <summary>Metres added to a cell's distance per reason when picking the next build.</summary>
        private static readonly double[] ReasonCost = { 0, 12, 24, 60, 200, 1000 };

        private void SetShown(Cell c, bool on)
        {
            if (c.Shown == on) return;
            c.Shown = on;
            if (c.Live != null && c.Live.Handles != null)
                for (int k = 0; k < c.Live.Uploaded; k++) _sink.SetShown(c.Live.Handles[k], on);
            RegisterColliders(c);
        }

        /// <summary>True when B1 block <paramref name="block"/> of a tile (part index of the B1 layer) may hand its inner
        /// range to B0: every cell the camera needs in it draws every house only B0 draws (set by <see cref="Update"/>).</summary>
        public bool BlockReady(B0Tile t, int block)
        {
            if (t == null || !t.Visible || t.Index == null) return false;
            return block < 0 || block >= t.BlockReady.Length || t.BlockReady[block];
        }

        // ------------------------------------------------------------------------------------------------------------
        // Builds

        private void Dispatch(double camX, double camZ, double now)
        {
            if (_running) return;
            if (_indexError != null)
            {
                Warning?.Invoke("B0 house index failed: " + _indexError.Message);
                _indexError = null;
            }
            _indexJob = null;
            if (_job != null)
            {
                Version v = _job;
                if (v.Abandoned || v.Cell.Tile == null || v.Cell.WantedFrame != _frame || v.Cell.Pending != v)
                {
                    // Released, or no longer needed: dropped between slices.
                    _job = null;
                    if (v.Cell.Pending == v) v.Cell.Pending = null;
                    Drop(v);
                }
                else if (v.Built || v.Error != null)
                {
                    _job = null;
                    Finish(v);
                }
                else
                {
                    if (now >= _nextSliceMs) Run(_runSlice);
                    return;
                }
            }
            if (now < _nextSliceMs) return;

            // A visible tile without its house index builds it first (one job).
            for (int i = 0; i < _tiles.Count; i++)
            {
                B0Tile t = _tiles[i];
                if (!t.Visible || t.Index != null) continue;
                _indexJob = t.Source;
                Run(_runIndex);
                return;
            }

            if (_ready.Count >= MaxReady || _bestTile == null) return;
            BuildsByReason[_bestReason]++;
            B0Tile bt = _bestTile;
            Cell cell;
            if (!bt.Cells.TryGetValue(_bestZ * 4096 + _bestX, out cell)) return;
            B0HouseIndex idx = bt.Index;
            double cx = camX - bt.Id.X0, cz = camZ - bt.Id.Z0;
            _select.Clear();
            idx.Select(_fine, _bestX, _bestZ, _bands.B0OuterM, cx, cz, _bands.B0BuildPadM, _select);
            int[] houses = idx.Houses(_fine, _bestX, _bestZ);
            var nv = new Version
            {
                Cell = cell, Positions = _select.ToArray(), CamX = cx, CamZ = cz, Mask = new ulong[(houses.Length + 63) >> 6],
                Colliders = new StructureColliders(), Data = _meshPool.Count > 0 ? _meshPool.Pop() : new MeshData(4096, 12288),
                Options = new[] { _bands.B0Options(BandConfig.LevelNear, _shared), _bands.B0Options(BandConfig.LevelLite, _shared),
                                  _bands.B0Options(BandConfig.LevelFlat, _shared) },
            };
            nv.Levels = new byte[nv.Positions.Length];
            nv.From = new int[nv.Positions.Length];
            nv.Ranges = new int[6 * (nv.Positions.Length + 1)];
            Version live = cell.Live;
            bool reusable = live != null && live.Data != null && live.Ranges != null;
            for (int k = 0; k < nv.Positions.Length; k++)
            {
                int p = nv.Positions[k];
                nv.Mask[p >> 6] |= 1UL << (p & 63);
                int current = -1, from = -1;
                if (live != null && live.Mask != null && (live.Mask[p >> 6] >> (p & 63) & 1UL) != 0)
                {
                    from = Array.BinarySearch(live.Positions, p);
                    current = live.Levels[from];
                }
                int i = houses[p];
                nv.Levels[k] = (byte)_bands.B0Level(idx.CentroidDistance(i, cx, cz), idx.Street(i), current);
                nv.From[k] = reusable && current == nv.Levels[k] ? from : -1;
                if (nv.From[k] < 0) HousesBuilt++;
                else HousesReused++;
            }
            if (reusable)
            {
                nv.Reuse = live;
                nv.ReuseData = live.Data;
            }
            nv.Data.Clear();
            cell.Pending = nv;
            _job = nv;
            Builds++;
            Run(_runSlice);
        }

        private void Run(Action work)
        {
            _running = true;
            Slices++;
            _runner.Run(work);
        }

        private void RunIndex()
        {
            double start = _clockMs();
            try
            {
                // The tile's road corridor guard too (the first house of a tile would otherwise pay for it in its slice).
                BuildingFootprints.For(_indexJob, _shared.CorridorsFor(_indexJob));
                B0HouseIndex.Prepare(_indexJob);
            }
            catch (Exception e)
            {
                // Retried on the next dispatch; the tile's B1 stands in meanwhile.
                _indexError = e;
            }
            finally
            {
                Finished(start);
            }
        }

        /// <summary>One build slice on a worker: houses until <see cref="SliceMs"/> has passed (at least one); the last
        /// slice splits the mesh into upload chunks.</summary>
        private void RunSlice()
        {
            Version v = _job;
            double start = _clockMs();
            try
            {
                Cell c = v.Cell;
                B0Tile t = c.Tile;
                if (t != null && !v.Abandoned)
                {
                    int[] houses = t.Index.Houses(_fine, c.X, c.Z);
                    double ox = c.X * t.Index.CellSide(_fine), oz = c.Z * t.Index.CellSide(_fine);
                    while (v.Next < v.Positions.Length)
                    {
                        int k = v.Next;
                        Mark(v, k);
                        if (v.From[k] >= 0) CopyHouse(v.Reuse, v.ReuseData, v.From[k], v);
                        else
                        {
                            _gen.Clear();
                            BuildingDetailMesher.One(t.Source, houses[v.Positions[k]], t.Sampler, v.Options[v.Levels[k]], v.Data, _gen);
                            if (_gen.Boxes.Count + _gen.Ramps.Count + _gen.Cylinders.Count + _gen.Walls.Count > 0) AddColliders(v.Colliders, _gen, ox, oz);
                        }
                        v.Next++;
                        if (_clockMs() - start >= SliceMs) break;
                    }
                    if (v.Next >= v.Positions.Length)
                    {
                        Mark(v, v.Positions.Length);
                        v.Tris = v.Data.TriangleCount;
                        v.Chunks.Clear();
                        if (v.Tris > 0) TileBuild.SplitLayer(v.Data, 0, ChunkVertices, v.Chunks, ref v.Shorts);
                        for (int i = 0; i < v.Chunks.Count; i++) v.Bytes += v.Chunks[i].Bytes(v.Data.HasUv0);
                        v.Built = true;
                    }
                }
            }
            catch (Exception e)
            {
                v.Error = e;
            }
            finally
            {
                v.WorkMs += _clockMs() - start;
                Finished(start);
            }
        }

        private void Finished(double start)
        {
            double end = _clockMs(), ms = end - start;
            WorkMs += ms;
            _nextSliceMs = end + ms * (1.0 / WorkerDuty - 1.0);
            _running = false;
        }

        /// <summary>Where position <paramref name="k"/>'s house starts in the version's mesh and colliders.</summary>
        private static void Mark(Version v, int k)
        {
            int[] r = v.Ranges;
            r[6 * k] = v.Data.VertexCount;
            r[6 * k + 1] = v.Data.IndexCount;
            r[6 * k + 2] = v.Colliders.Boxes.Count;
            r[6 * k + 3] = v.Colliders.Ramps.Count;
            r[6 * k + 4] = v.Colliders.Cylinders.Count;
            r[6 * k + 5] = v.Colliders.Walls.Count;
        }

        /// <summary>
        /// Appends position <paramref name="k"/>'s house of an earlier version of the cell to <paramref name="dst"/>: its
        /// vertices, its triangles (the earlier version's indices were made chunk-relative by its split, so each is moved
        /// back by its chunk's first vertex) and its colliders. The same house at the same level is the same mesh (the
        /// grammar is deterministic), so a rebuild only builds the houses that are new or change level.
        /// </summary>
        private static void CopyHouse(Version src, MeshData from, int k, Version dst)
        {
            MeshData to = dst.Data;
            int[] r = src.Ranges;
            int v0 = r[6 * k], v1 = r[6 * k + 6], i0 = r[6 * k + 1], i1 = r[6 * k + 7];
            int nv = v1 - v0, ni = i1 - i0, dv = to.VertexCount;
            to.Reserve(nv, ni);
            if (nv > 0)
            {
                Array.Copy(from.Positions, 3 * v0, to.Positions, 3 * dv, 3 * nv);
                Array.Copy(from.Normals, 3 * v0, to.Normals, 3 * dv, 3 * nv);
                Array.Copy(from.Colors, 4 * v0, to.Colors, 4 * dv, 4 * nv);
                if (from.HasUv0 && !to.HasUv0)
                {
                    Array.Clear(to.Uv0, 0, 2 * dv);
                    to.HasUv0 = true;
                }
                if (from.HasUv0) Array.Copy(from.Uv0, 2 * v0, to.Uv0, 2 * dv, 2 * nv);
                else if (to.HasUv0) Array.Clear(to.Uv0, 2 * dv, 2 * nv);
                to.VertexCount = dv + nv;
            }
            int chunk = 0, di = to.IndexCount;
            List<UploadChunk> chunks = src.Chunks;
            for (int i = i0; i < i1; i++)
            {
                while (chunk < chunks.Count && i >= chunks[chunk].FirstIndex + chunks[chunk].IndexCount) chunk++;
                int abs = from.Indices[i] + (chunk < chunks.Count ? chunks[chunk].FirstVertex : 0);
                to.Indices[di++] = abs - v0 + dv;
            }
            to.IndexCount = di;
            StructureColliders a = src.Colliders, b = dst.Colliders;
            for (int i = r[6 * k + 2]; i < r[6 * k + 8]; i++) b.Boxes.Add(a.Boxes[i]);
            for (int i = r[6 * k + 3]; i < r[6 * k + 9]; i++) b.Ramps.Add(a.Ramps[i]);
            for (int i = r[6 * k + 4]; i < r[6 * k + 10]; i++) b.Cylinders.Add(a.Cylinders[i]);
            for (int i = r[6 * k + 5]; i < r[6 * k + 11]; i++) b.Walls.Add(a.Walls[i]);
        }

        /// <summary>A house's generator colliders, moved from the tile's frame to the cell's (<see cref="CellKey"/>).</summary>
        private static void AddColliders(StructureColliders into, GenColliders g, double ox, double oz)
        {
            int b0 = into.Boxes.Count, r0 = into.Ramps.Count, y0 = into.Cylinders.Count, w0 = into.Walls.Count;
            into.AddFrom(g);
            for (int i = b0; i < into.Boxes.Count; i++)
            {
                OrientedBox b = into.Boxes[i];
                b.CX -= ox;
                b.CZ -= oz;
                into.Boxes[i] = b;
            }
            for (int i = r0; i < into.Ramps.Count; i++)
            {
                StepRamp r = into.Ramps[i];
                r.X0 -= ox;
                r.X1 -= ox;
                r.Z0 -= oz;
                r.Z1 -= oz;
                into.Ramps[i] = r;
            }
            for (int i = y0; i < into.Cylinders.Count; i++)
            {
                ColliderCylinder y = into.Cylinders[i];
                y.CX -= ox;
                y.CZ -= oz;
                into.Cylinders[i] = y;
            }
            for (int i = w0; i < into.Walls.Count; i++)
            {
                ColliderWall w = into.Walls[i];
                w.X0 -= ox;
                w.X1 -= ox;
                w.Z0 -= oz;
                w.Z1 -= oz;
                into.Walls[i] = w;
            }
        }

        /// <summary>A finished build: queued for upload, or the cell left to its B1 block when it threw.</summary>
        private void Finish(Version v)
        {
            Cell c = v.Cell;
            if (v.Error != null)
            {
                Warning?.Invoke("B0 cell " + c.X + "," + c.Z + " of " + c.Tile.Id + " failed: " + v.Error.Message);
                c.Failed = true;
                c.Pending = null;
                Drop(v);
                return;
            }
            _ready.Add(v);
        }

        // ------------------------------------------------------------------------------------------------------------
        // Uploads

        /// <summary>GPU bytes of the next chunk waiting for upload (0 when none).</summary>
        public long NextUploadBytes
        {
            get
            {
                for (int i = 0; i < _ready.Count; i++)
                {
                    Version v = _ready[i];
                    if (v.Abandoned || v.Cell.Pending != v || v.Uploaded >= v.Chunks.Count) continue;
                    return v.Chunks[v.Uploaded].Bytes(v.Data.HasUv0);
                }
                return 0;
            }
        }

        /// <summary>
        /// Uploads finished cells chunk by chunk while they fit in <paramref name="budgetBytes"/> (with
        /// <paramref name="atLeastOne"/>, one chunk even when it does not); a cell whose last chunk is up replaces its old
        /// version in the same call, shown if the old one was. Returns the bytes uploaded. Main thread.
        /// </summary>
        public long Upload(long budgetBytes, bool atLeastOne)
        {
            long used = 0;
            while (_ready.Count > 0)
            {
                Version v = _ready[0];
                Cell c = v.Cell;
                if (v.Abandoned || c.Tile == null || c.Pending != v)
                {
                    _ready.RemoveAt(0);
                    if (c.Pending == v) c.Pending = null;
                    Drop(v);
                    continue;
                }
                if (v.Uploaded >= v.Chunks.Count)
                {
                    _ready.RemoveAt(0);
                    Swap(c, v);
                    continue;
                }
                UploadChunk ch = v.Chunks[v.Uploaded];
                long b = ch.Bytes(v.Data.HasUv0);
                if (used + b > budgetBytes && !(atLeastOne && used == 0)) break;
                if (v.Handles == null) v.Handles = new object[v.Chunks.Count];
                v.Handles[v.Uploaded] = _sink.Upload(c.Tile, v.Data, ch, v.Shorts);
                v.Uploaded++;
                v.UploadedBytes += b;
                ResidentBytes += b;
                UploadedBytes += b;
                used += b;
            }
            return used;
        }

        /// <summary>The new version of a cell is up: it replaces the old one in the same frame (shown, with its colliders,
        /// when the old one was).</summary>
        private void Swap(Cell c, Version v)
        {
            Version old = c.Live;
            c.Live = v;
            c.Pending = null;
            if (c.Shown && v.Handles != null)
                for (int k = 0; k < v.Uploaded; k++) _sink.SetShown(v.Handles[k], true);
            v.Reuse = null;
            v.ReuseData = null;
            v.Shorts = null;
            Drop(old); // the new version keeps its mesh while the cell is needed: its houses are copied into the next one
            if (c.Shown) RegisterColliders(c);
        }

        private void ReleaseData(Version v)
        {
            if (v.Data == null) return;
            if (_job != null && _job.Reuse == v)
            {
                // A build is copying from it: let it go without pooling (the worker holds its own reference).
                v.Data = null;
                v.Shorts = null;
                return;
            }
            MeshData m = v.Data;
            v.Data = null;
            v.Shorts = null;
            if (_meshPool.Count < 3 && m.VertexCapacity <= ChunkVertices * 8)
            {
                m.Clear();
                _meshPool.Push(m);
            }
        }

        /// <summary>Destroys a version's chunks and frees its buffers (never those a running slice writes).</summary>
        private void Drop(Version v)
        {
            if (v == null) return;
            v.Abandoned = true;
            if (v.Handles != null)
            {
                for (int k = 0; k < v.Uploaded; k++)
                    if (v.Handles[k] != null) _sink.Destroy(v.Handles[k]);
                v.Handles = null;
            }
            ResidentBytes -= v.UploadedBytes;
            v.UploadedBytes = 0;
            v.Uploaded = 0;
            if (_job == v) return; // the running or paused slice job is dropped by Dispatch
            ReleaseData(v);
        }

        // ------------------------------------------------------------------------------------------------------------
        // Cache

        private void Evict()
        {
            for (int i = _all.Count - 1; i >= 0; i--)
            {
                Cell c = _all[i];
                if (c.Live != null || c.Pending != null || c.WantedFrame == _frame) continue;
                c.Tile.Cells.Remove(c.Z * 4096 + c.X);
                _all.RemoveAt(i);
            }
            while (ResidentBytes > _bands.CellCacheBytes)
            {
                Cell oldest = null;
                for (int i = 0; i < _all.Count; i++)
                {
                    Cell c = _all[i];
                    if (c.WantedFrame == _frame || c.Pending != null || c.Live == null) continue;
                    if (oldest == null || c.LastWantedMs < oldest.LastWantedMs) oldest = c;
                }
                if (oldest == null) break;
                SetShown(oldest, false);
                Drop(oldest.Live);
                oldest.Live = null;
                oldest.Tile.Cells.Remove(oldest.Z * 4096 + oldest.X);
                _all.Remove(oldest);
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // Colliders

        /// <summary>
        /// The ground key of a cell's collider set: the quadtree tile the cell coincides with (a level-10 detail tile's
        /// 64 m cells are its level-14 tiles, its 32 m cells its level-15 tiles), so each set has its own key and frame and
        /// never touches the tile's own sets (heroes; static dressing).
        /// </summary>
        public static ulong CellKey(TileId tile, int perSide, int ix, int iz)
        {
            int bits = 0;
            while ((1 << bits) < perSide) bits++;
            if (1 << bits != perSide || tile.Level + bits > TileId.MaxLevel)
                throw new InvalidOperationException("B0 cells of " + tile + " do not align with the quadtree (" + perSide + " per side)");
            return new TileId(tile.Level + bits, tile.Tx * perSide + ix, tile.Ty * perSide + iz).Key;
        }

        /// <summary>Registers a shown cell's colliders (or drops them when it hides).</summary>
        private void RegisterColliders(Cell c)
        {
            if (_ground == null || c.Tile == null) return;
            bool want = c.Shown && c.Tile.Visible && c.Live != null && c.Live.Colliders != null && c.Live.Colliders.Count > 0;
            ulong key = CellKey(c.Tile.Id, c.Tile.Index.PerSide(_fine), c.X, c.Z);
            if (want)
            {
                _ground.Register(key, c.Live.Colliders);
                c.Registered = true;
                ColliderRegistrations++;
            }
            else if (c.Registered)
            {
                _ground.Unregister(key);
                c.Registered = false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            for (int i = _tiles.Count - 1; i >= 0; i--) Remove(_tiles[i]);
            _disposed = true;
            _ready.Clear();
            _meshPool.Clear();
        }
    }
}
