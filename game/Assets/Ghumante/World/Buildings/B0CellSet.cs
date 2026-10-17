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

        /// <summary>Per B1 block (<see cref="BandConfig.B1BlockM"/>): B0 draws every house the band draws in it, so the
        /// block hands its inner range to B0; false while the block stands in for houses B0 does not draw.</summary>
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
    /// The B0 band (W2_DESIGN 2.4; the detail pass band table) around the band centre (<see cref="BandAnchor"/>: the
    /// player, not the swaying chase camera), engine-free: which cells are drawn, which houses each draws and at which
    /// level, when cells are (re)built, uploaded, shown, cached and evicted, and their structure colliders. DetailCells
    /// puts it on screen through <see cref="IB0CellSink"/>; the budget and stability tests drive it directly
    /// (core-tests MeshingBuildingB0CellTests, MeshingBuildingB0StabilityTests).
    /// <list type="bullet">
    /// <item><b>Each house once.</b> A 32 m cell draws the houses of its <see cref="B0HouseIndex"/> list whose geometry meets
    /// B0 and its fade into B1, each at one level (<see cref="BandConfig.B0Level"/>): the full grammar near the centre, the
    /// lite level for the street fronts beyond, the flat level for the rest; a house changes level only when its cell is
    /// rebuilt, with hysteresis. There is no near or lite ring to overlap: B0 is one band with one material, and its only
    /// cross-fade is the planned one into B1 at the band radius.</item>
    /// <item><b>Never a hole, never late.</b> Every house the band draws (its geometry within the radius plus half the
    /// fade) must be in its cell's shown version. A cell is rebuilt, before anything else, once a house it lacks comes
    /// within <see cref="BandConfig.B0LeadM"/> of the fade or within the fade around where the centre will be in
    /// <see cref="BandConfig.B0NeedLeadS"/> (its smoothed velocity); the build takes the houses within
    /// <see cref="BandConfig.B0BuildPadM"/> of the fade around the centre and around where it will be in
    /// <see cref="BandConfig.B0NeedLeadS"/> and <see cref="BandConfig.B0BuildLeadS"/>, so it is up before the house is drawn
    /// and the cell is not rebuilt again for several metres. The old version shows until the new one is up and is
    /// swapped in the same frame.</item>
    /// <item><b>Never two houses.</b> If a house the band draws is not drawn by its cell anyway (a teleport, the first
    /// frames on a tile, a failed build), the B1 blocks it lies in stand in (<see cref="BlockReady"/>: B1 draws from the
    /// centre out) and every cell with a house in a standing-in block hides, so B0 and the standing-in B1 never draw the
    /// same house; this settles to a fixed point each frame (B1 splits triangles into blocks by their centroid, so a
    /// house at a block edge has parts in two blocks).</item>
    /// <item><b>Hysteresis.</b> A cell becomes wanted when a house it has comes within the lead, and stays wanted (shown,
    /// with its colliders and its CPU copy for the next rebuild) until all of them are <see cref="ExitPadM"/> beyond the
    /// build pad; a rebuild keeps the houses it has within that distance. Levels keep
    /// <see cref="BandConfig.B0LevelHysteresisM"/>. A standing rider (and, through <see cref="BandAnchor"/>, a swinging
    /// camera) changes nothing.</item>
    /// <item><b>Builds</b> run on a worker in slices of at most <see cref="SliceMs"/> (one house at least; one slice in
    /// flight; the worker busy at most <see cref="WorkerDuty"/> of the time). One build is at most about
    /// <see cref="JobBudgetMs"/> of estimated work (<see cref="EstimateMs"/>): houses the cell lacks are built at the flat
    /// level beyond it and level changes wait for the next rebuild, so an urgent build never waits long. The last slice
    /// splits the cell into chunks of at most <see cref="ChunkVertices"/> vertices with 16-bit indices.</item>
    /// <item><b>Budget</b>: a house is built at, or raised to, the near or lite level only while the band (every wanted
    /// cell's live version and the builds on their way) stays within <see cref="RaiseCapTris"/> of the tier's budget with
    /// the raise (<see cref="EstimatedTris"/>, learnt from the houses built); otherwise it is drawn at the flat level until
    /// houses leaving the band make room, so a rider circling a dense chowk never gathers near-level houses on every side.</item>
    /// <item><b>Uploads</b> go chunk by chunk through <see cref="Upload"/> within the bytes the caller gives (the
    /// streamer shares its per-frame upload cap between tiles and B0); a build the worker finished is queued for upload as
    /// soon as the main thread looks, so a build dispatched in one frame is on screen the next.</item>
    /// <item><b>Cache</b>: uploaded cells stay resident up to <see cref="BandConfig.CellCacheBytes"/> of GPU mesh, the
    /// least recently needed evicted first; cells needed this frame are never evicted.</item>
    /// <item><b>Colliders</b> (pikha aprons, flat roofs, stair cabins, temple plinths) of a shown cell are registered with
    /// the ground under the key of the quadtree tile the cell coincides with, in that tile's frame (<see cref="CellKey"/>),
    /// so a cell showing or hiding registers or drops only its own small set, and a hidden cell has none: the colliders
    /// are always those of what is drawn.</item>
    /// </list>
    /// Main thread, apart from the slice jobs. Allocation-free per frame once warm (cells and their versions are
    /// allocated when built).
    /// </summary>
    public sealed class B0CellSet : IDisposable
    {
        /// <summary>Worker milliseconds one build slice takes at least (W2_DESIGN 10.4: 4 ms per cell): a slice runs for
        /// <see cref="WorkerDuty"/> of the last frame's time, between this and <see cref="MaxSliceMs"/> (8.3 ms at 30 fps,
        /// 4.2 ms at 60 fps: one slice starts per frame at most), and always builds at least one house.</summary>
        public const double SliceMs = 4.0, MaxSliceMs = 10.0;

        /// <summary>Share of one worker's time the B0 slices may take: after a slice the next waits three times its
        /// length.</summary>
        public const double WorkerDuty = 0.25;

        /// <summary>Most vertices of one B0 upload chunk: about 0.5 MB with UV0 and 16-bit indices, half the per-frame
        /// upload cap, so a chunk and a tile chunk share a frame.</summary>
        public const int ChunkVertices = 12288;

        /// <summary>Built cells waiting for upload before no new build starts.</summary>
        public const int MaxReady = 2;

        /// <summary>A cell holding at least a third of houses it no longer needs is rebuilt (lazily) once the centre has
        /// moved this far from where it was built.</summary>
        public const double WasteMoveM = 8.0;

        /// <summary>While the band is near its budget, a cell sheds its unneeded houses once the centre has moved this far
        /// (m) from where it was built.</summary>
        public const double PruneMoveM = 2.0;

        /// <summary>A cell the lead brought in stays wanted this long (ms) after the lead last reached it, so a rider
        /// braking to a stop (the lead shrinks before the centre counts as standing) keeps it.</summary>
        public const double LeadHoldMs = 3000.0;

        /// <summary>Most metres the lead reaches ahead of the centre (a vehicle at 60 km/h; faster, B1 may stand in).</summary>
        public const double MaxLeadM = 40.0;

        /// <summary>Time constant (s) of the centre's smoothed velocity, and the jump (m) taken for a teleport (no lead).</summary>
        public const double VelocityTauS = 0.3, TeleportM = 50.0;

        /// <summary>A wanted cell stays wanted (shown, with its colliders and the CPU copy its next rebuild copies from)
        /// until all its houses are this far (m) beyond the build pad.</summary>
        public const double ExitPadM = 4.0;

        /// <summary>Below this smoothed speed (m/s) the centre stands: no lead, no cell stops being wanted and nothing is
        /// pruned unless the band is near its budget, so a rider waiting at a junction changes nothing once settled.</summary>
        public const double StandingMps = 0.5;

        /// <summary>Estimated worker milliseconds one build may take (see <see cref="EstimateMs"/>).</summary>
        public const double JobBudgetMs = 60.0;

        /// <summary>Estimated worker milliseconds per house built at the near, lite and flat level (BuildingDetailMesher.One
        /// at Asan, about a phone's release build), and per house copied from the previous version.</summary>
        public static readonly double[] EstimateMs = { 24.0, 5.0, 3.5 };

        public const double CopyEstimateMs = 0.12;

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

            /// <summary>The B1 blocks its houses touch (<see cref="B0HouseIndex.Blocks"/>; none when BlockX1 &lt; BlockX0):
            /// it is drawn only while all of them hand over to B0.</summary>
            public int BlockX0, BlockZ0, BlockX1 = -1, BlockZ1 = -1;

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

            /// <summary>How many triangles the band gains when this version replaces the cell's live one: estimated when it
            /// is dispatched (<see cref="EstimatedTris"/>), exact once built.</summary>
            public double Delta;

            /// <summary>Triangles of position <paramref name="k"/>'s house in this version (0 before it is built).</summary>
            public int HouseTris(int k)
            {
                return Ranges == null || Next <= k && !Built ? 0 : (Ranges[6 * k + 7] - Ranges[6 * k + 1]) / 3;
            }

            /// <summary>True when position <paramref name="p"/> (in the cell's house list) is drawn by this version.</summary>
            public bool Has(int p)
            {
                return Mask != null && p >> 6 < Mask.Length && (Mask[p >> 6] >> (p & 63) & 1UL) != 0;
            }
        }

        internal sealed class Cell
        {
            public B0Tile Tile;
            public int X, Z;
            public Version Live, Pending;
            public double LastWantedMs, HoldUntilMs;

            /// <summary>Last frame the cell was wanted (it is kept, shown when ready, and built ahead).</summary>
            public long WantedFrame = -1;

            public bool Shown, Failed, Registered;
        }

        private const int ReasonHole = 0, ReasonEdge = 1, ReasonLeaveNear = 2, ReasonPrune = 3, ReasonNear = 4, ReasonLite = 5, ReasonLazy = 6;

        /// <summary>Share of the tier's B0 budget (<see cref="BandConfig.B0BudgetTris"/>) above which cells shed the houses
        /// they no longer need before any level is raised.</summary>
        public const double PruneShare = 0.85;

        /// <summary>
        /// Triangles of the tier's B0 budget kept free of raised levels: a house is built at, or raised to, the near or
        /// lite level only while the band's triangles (the wanted cells, shown or about to be, plus the builds on their
        /// way, <see cref="Version.Delta"/>) stay within <see cref="RaiseCapTris"/> with the raise
        /// (<see cref="EstimatedTris"/>); otherwise it is drawn at the flat level until houses leaving the band make room
        /// (a rider circling the densest chowk would otherwise gather near-level houses on every side of their path). The
        /// headroom takes the houses riding brings in at the band's edge before the cells behind shed theirs: rolling to
        /// and fro by Asan on Mid brings about 7 k (the band peaks at 100.7 k of its 105.75 k budget; 108.5 k with 4 k of
        /// headroom, 127 k without the gate), at the cost of 11 of the 229 houses by Asan drawn one level poorer than
        /// their distance asks on Mid while the rider stands there, none on High (core-tests
        /// MeshingBuildingB0StabilityTests, MeshingBuildingB0CellTests). Levels are never lowered for the budget: a house near the rider never pops to a
        /// poorer level while it stays near.
        /// </summary>
        public const int RaiseHeadroomTris = 10000;

        /// <summary>The band's triangles up to which levels are raised: the budget less <see cref="RaiseHeadroomTris"/>,
        /// and at least 80% of it.</summary>
        public double RaiseCapTris
        {
            get { return Math.Max(0.8 * _bands.B0BudgetTris, _bands.B0BudgetTris - (double)RaiseHeadroomTris); }
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
        private readonly List<Cell> _wanted = new List<Cell>(64);
        private readonly List<Version> _ready = new List<Version>(MaxReady + 1);
        private readonly List<int> _select = new List<int>(64);
        private readonly Stack<MeshData> _meshPool = new Stack<MeshData>(4);
        private readonly GenColliders _gen = new GenColliders();
        private readonly Action _runSlice, _runIndex;
        private long _frame;
        private int _unindexed;
        private double _nextSliceMs = double.NegativeInfinity, _sliceMs = SliceMs;
        private volatile bool _running;
        /// <summary>The builds in progress: [0] urgent (a house the band draws, or will within the lead, is missing),
        /// [1] the rest (levels, pruning), paused between slices while an urgent build runs. <see cref="_job"/> is the one
        /// the running slice works on.</summary>
        private readonly Version[] _jobs = new Version[2];

        private Version _job;
        private TileData _indexJob;
        private Exception _indexError;
        private B0Tile _bestTile;
        private int _bestX, _bestZ, _bestReason;
        private double _bestScore;
        private bool _hasLast, _overBudget;
        private double _projectedTris;
        private readonly double[] _levelTris = new double[3];
        private readonly int[] _levelSamples = new int[3];
        private double _lastX, _lastZ, _lastMs, _vx, _vz, _needX, _needZ, _buildX, _buildZ;
        private bool _disposed;

        /// <summary>Reports a cell whose build threw (it is left to its B1 block).</summary>
        public Action<string> Warning;

        /// <param name="bands">The tier's radii, levels and cache budget.</param>
        /// <param name="shared">The streamer's B0 options (corridors, hide set, sink), copied per level.</param>
        /// <param name="sink">Uploads and shows the chunks.</param>
        /// <param name="ground">Takes the cells' structure colliders (null: none).</param>
        /// <param name="runner">Runs the build slices (<see cref="ThreadPoolJobRunner"/> in the game).</param>
        /// <param name="clockMs">Milliseconds (pacing, slice timing and the centre's velocity).</param>
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
            _levelTris[BandConfig.LevelNear] = Math.Max(1000, 0.6 * bands.B0CapTris);
            _levelTris[BandConfig.LevelLite] = 450;
            _levelTris[BandConfig.LevelFlat] = 380;
        }

        /// <summary>The triangles a house is expected to take at a level: the mean of the houses built at that level so far
        /// (the latest 64), from a first guess per tier (near: 60% of the tier's cap; lite and flat: what the old core's
        /// houses take). Decides whether a raise fits the budget (<see cref="RaiseCapTris"/>).</summary>
        public double EstimatedTris(int level)
        {
            return _levelTris[level];
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

        /// <summary>Builds started per reason: a house B0 draws was missing (B1 stood in), a house within the lead was
        /// missing, a near-level house left the near radius, houses no longer needed while the band is near its budget, a
        /// house wanted the near level, a street front wanted the lite level, lazy (other demotions, waste).</summary>
        public readonly int[] BuildsByReason = new int[7];

        /// <summary>Houses copied from a cell's previous version instead of built (same house, same level).</summary>
        public int HousesReused { get; private set; }

        public int Slices { get; private set; }

        public double WorkMs { get; private set; }

        public long UploadedBytes { get; private set; }

        /// <summary>Collider sets registered with the ground so far (each one cell's).</summary>
        public int ColliderRegistrations { get; private set; }

        /// <summary>B1 blocks standing in for B0 this frame (a house the band draws is not drawn by its cell).</summary>
        public int BlocksWaiting { get; private set; }

        /// <summary>The centre's smoothed velocity (m/s), from which the lead is taken.</summary>
        public double VelocityX
        {
            get { return _vx; }
        }

        public double VelocityZ
        {
            get { return _vz; }
        }

        /// <summary>True while a build slice runs on a worker.</summary>
        public bool Busy
        {
            get { return _running; }
        }

        /// <summary>The version being built (null when none): the stability bench charges its worker cost model per house
        /// from it (core-tests MeshingBuildingB0StabilityTests).</summary>
        internal Version Job
        {
            get { return _job; }
        }

        /// <summary>The number of <see cref="Update"/> calls so far (a cell is wanted this frame when its
        /// <see cref="Cell.WantedFrame"/> equals it).</summary>
        internal long Frame
        {
            get { return _frame; }
        }

        /// <summary>True when B0 has nothing left to build or upload for the centre of the last <see cref="Update"/>.</summary>
        public bool Settled
        {
            get { return !_running && _jobs[0] == null && _jobs[1] == null && _ready.Count == 0 && _bestTile == null && BlocksWaiting == 0 && _unindexed == 0; }
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

        /// <summary>Per frame, with the band centre (game metres; <see cref="BandAnchor"/>): find the cells the band
        /// wants, decide which B1 blocks stand in, show the drawable cells, start or continue a build slice and evict
        /// beyond the cache budget.</summary>
        public void Update(double camX, double camZ)
        {
            if (_disposed) return;
            _frame++;
            double now = _clockMs();
            _overBudget = ShownTris > PruneShare * _bands.B0BudgetTris;
            _projectedTris = WantedTris() + PendingDelta();
            if (_hasLast) _sliceMs = Math.Max(SliceMs, Math.Min(MaxSliceMs, WorkerDuty * (now - _lastMs)));
            Track(camX, camZ, now);
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
                double ox = t.Id.X0, oz = t.Id.Z0;
                _wanted.Clear();
                Mark(t, camX - ox, camZ - oz, _needX - ox, _needZ - oz, _buildX - ox, _buildZ - oz, now);
                StandIn(t, camX - ox, camZ - oz);
                for (int b = 0; b < t.BlockReady.Length; b++)
                    if (!t.BlockReady[b]) BlocksWaiting++;
            }
            _wanted.Clear();

            for (int i = 0; i < _all.Count; i++)
            {
                Cell c = _all[i];
                // A cell no longer wanted keeps only its GPU mesh (cached); its CPU copy is for the next rebuild.
                if (c.WantedFrame != _frame && c.Live != null && c.Live.Data != null) ReleaseData(c.Live);
                bool on = c.Tile.Visible && c.WantedFrame == _frame && Drawable(c);
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

        /// <summary>The centre's smoothed velocity and the lead centres (where it will be in
        /// <see cref="BandConfig.B0NeedLeadS"/> and <see cref="BandConfig.B0BuildLeadS"/>).</summary>
        private void Track(double x, double z, double now)
        {
            if (_hasLast)
            {
                double dx = x - _lastX, dz = z - _lastZ, dt = (now - _lastMs) * 0.001;
                if (dx * dx + dz * dz > TeleportM * TeleportM)
                {
                    _vx = 0;
                    _vz = 0;
                }
                else if (dt > 1e-4)
                {
                    double a = 1.0 - Math.Exp(-dt / VelocityTauS);
                    _vx += (dx / dt - _vx) * a;
                    _vz += (dz / dt - _vz) * a;
                }
            }
            _hasLast = true;
            _lastX = x;
            _lastZ = z;
            _lastMs = now;
            Lead(x, z, _bands.B0NeedLeadS, out _needX, out _needZ);
            Lead(x, z, _bands.B0BuildLeadS, out _buildX, out _buildZ);
        }

        private void Lead(double x, double z, double seconds, out double lx, out double lz)
        {
            double ox = _vx * seconds, oz = _vz * seconds, d = Math.Sqrt(ox * ox + oz * oz);
            if (d < 0.5)
            {
                // Standing (or creeping): no lead, so nothing is built ahead of a rider who is not going anywhere.
                ox = 0;
                oz = 0;
            }
            else if (d > MaxLeadM)
            {
                ox *= MaxLeadM / d;
                oz *= MaxLeadM / d;
            }
            lx = x + ox;
            lz = z + oz;
        }

        /// <summary>
        /// The wanted cells of a tile (tile-local centre (cx, cz), need lead (nx, nz), build lead (bx, bz)): marks them,
        /// collects them in <see cref="_wanted"/> and picks the most urgent (re)build.
        /// </summary>
        private void Mark(B0Tile t, double cx, double cz, double nx, double nz, double bx, double bz, double now)
        {
            B0HouseIndex idx = t.Index;
            float outer = _bands.B0OuterM;
            double lead = _bands.B0LeadM, pad = _bands.B0BuildPadM, exit = pad + ExitPadM;
            int per = idx.PerSide(_fine);
            double side = idx.CellSide(_fine);
            double reach = outer + BandConfig.FadeM * 0.5 + idx.Spill(_fine) + exit;
            double minX = Math.Min(cx, Math.Min(nx, bx)) - reach, maxX = Math.Max(cx, Math.Max(nx, bx)) + reach;
            double minZ = Math.Min(cz, Math.Min(nz, bz)) - reach, maxZ = Math.Max(cz, Math.Max(nz, bz)) + reach;
            int x0 = Math.Max(0, (int)Math.Floor(minX / side)), x1 = Math.Min(per - 1, (int)Math.Floor(maxX / side));
            int z0 = Math.Max(0, (int)Math.Floor(minZ / side)), z1 = Math.Min(per - 1, (int)Math.Floor(maxZ / side));
            for (int iz = z0; iz <= z1; iz++)
                for (int ix = x0; ix <= x1; ix++)
                {
                    int key = iz * 4096 + ix;
                    Cell c;
                    t.Cells.TryGetValue(key, out c);
                    // Wanted once a house comes within the lead; kept until all are past the exit pad (a cell the lead
                    // brought in for a while longer; every wanted cell while the centre stands).
                    bool near = idx.Select(_fine, ix, iz, outer, cx, cz, lead, null) > 0;
                    bool ahead = !near && idx.Select(_fine, ix, iz, outer, nx, nz, 0, null) > 0;
                    bool soon = near || ahead;
                    bool keep = !soon && c != null && c.WantedFrame == _frame - 1 &&
                                (Standing || now < c.HoldUntilMs || idx.Select(_fine, ix, iz, outer, cx, cz, exit, null) > 0 ||
                                 idx.Select(_fine, ix, iz, outer, nx, nz, pad, null) > 0 || idx.Select(_fine, ix, iz, outer, bx, bz, pad, null) > 0);
                    if (!soon && !keep) continue;
                    if (c == null)
                    {
                        c = new Cell { Tile = t, X = ix, Z = iz };
                        t.Cells.Add(key, c);
                        _all.Add(c);
                    }
                    c.WantedFrame = _frame;
                    c.LastWantedMs = now;
                    if (ahead) c.HoldUntilMs = now + LeadHoldMs;
                    _wanted.Add(c);
                    if (c.Failed) continue;
                    int reason;
                    double urgency;
                    if (c.Pending != null)
                    {
                        // A paused level build of this cell gives way to a missing house: it is dropped (by the next
                        // Dispatch, never under a running slice) and the urgent rebuild starts next frame.
                        Version v = c.Pending;
                        if (v == _jobs[1] && !(_running && _job == v) && Reason(idx, c, cx, cz, nx, nz, bx, bz, out reason, out urgency) && reason <= ReasonEdge)
                            v.Abandoned = true;
                        continue;
                    }
                    if (!Reason(idx, c, cx, cz, nx, nz, bx, bz, out reason, out urgency)) continue;
                    double score = reason * 1e6 + urgency;
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

        /// <summary>
        /// Why a wanted cell needs a (re)build, most urgent first, and how urgent within the reason (metres from the
        /// centre): a house the band draws now is missing (B1 stands in for it), a house within the lead is missing, a
        /// near-level house left the near radius (the costly level: demoted before anything is upgraded, so the band holds
        /// its budget while riding), a house wants the near level, a street front wants the lite level, or (lazily) houses
        /// want a poorer level or many are no longer needed. False when the cell is as it should be.
        /// </summary>
        private bool Reason(B0HouseIndex idx, Cell c, double cx, double cz, double nx, double nz, double bx, double bz, out int reason, out double urgency)
        {
            float outer = _bands.B0OuterM;
            double lead = _bands.B0LeadM;
            Version live = c.Live;
            int[] houses = idx.Houses(_fine, c.X, c.Z);
            double hole = double.MaxValue, edge = double.MaxValue;
            for (int p = 0; p < houses.Length; p++)
            {
                if (live != null && live.Has(p)) continue;
                int i = houses[p];
                if (idx.Meets(i, outer, cx, cz, 0)) hole = Math.Min(hole, idx.Distance(i, cx, cz));
                else if (idx.Meets(i, outer, cx, cz, lead) || idx.Meets(i, outer, nx, nz, 0)) edge = Math.Min(edge, idx.Distance(i, cx, cz));
            }
            reason = 0;
            urgency = 0;
            if (hole < double.MaxValue)
            {
                reason = ReasonHole;
                urgency = hole;
                return true;
            }
            if (edge < double.MaxValue)
            {
                reason = ReasonEdge;
                urgency = edge;
                return true;
            }
            if (live == null) return false;
            float x0, z0, x1, z1;
            idx.CellBounds(_fine, c.X, c.Z, out x0, out z0, out x1, out z1);
            urgency = BandConfig.NearestDistance(x0, z0, x1, z1, cx, cz);
            int richest;
            bool poorer, nearPoorer;
            double cheapest;
            Levels(idx, live, cx, cz, out richest, out poorer, out nearPoorer, out cheapest);
            if (nearPoorer) reason = ReasonLeaveNear;
            else if (richest >= 0 && cheapest <= RaiseRoom(idx, live, houses, cx, cz, nx, nz, bx, bz)) reason = richest == BandConfig.LevelNear ? ReasonNear : ReasonLite;
            else
            {
                // Waste, once the centre has moved on: a third of the houses no longer needed, or any while the band is
                // near its budget (then before any level is raised, standing too).
                double mx = cx - live.CamX, mz = cz - live.CamZ, moved = _overBudget ? PruneMoveM : WasteMoveM;
                int unneeded = (_overBudget || !Standing) && mx * mx + mz * mz > moved * moved ? Unneeded(idx, live, cx, cz, nx, nz, bx, bz) : 0;
                if (_overBudget && unneeded > 0) reason = ReasonPrune;
                else if (poorer || 3 * unneeded >= Math.Max(3, live.Positions.Length)) reason = ReasonLazy;
                else return false;
            }
            return true;
        }

        /// <summary>The richest level a house of a version wants above the one it is built at (-1: none), the triangles the
        /// cheapest of those raises would add (<paramref name="cheapest"/>), whether any house wants a poorer level, and
        /// whether a house built at the near level (the costly one) does.</summary>
        private void Levels(B0HouseIndex idx, Version v, double cx, double cz, out int richest, out bool poorer, out bool nearPoorer, out double cheapest)
        {
            richest = -1;
            poorer = false;
            nearPoorer = false;
            cheapest = double.MaxValue;
            int[] houses = idx.Houses(_fine, v.Cell.X, v.Cell.Z);
            for (int k = 0; k < v.Positions.Length; k++)
            {
                int i = houses[v.Positions[k]];
                int built = v.Levels[k];
                int want = _bands.B0Level(idx.CentroidDistance(i, cx, cz), idx.Street(i), built);
                if (want < built)
                {
                    if (richest < 0 || want < richest) richest = want;
                    cheapest = Math.Min(cheapest, Math.Max(0, _levelTris[want] - v.HouseTris(k)));
                }
                else if (want > built)
                {
                    poorer = true;
                    if (built == BandConfig.LevelNear) nearPoorer = true;
                }
            }
        }

        /// <summary>
        /// The triangles a rebuild of a cell may add by raising levels and stay within <see cref="RaiseCapTris"/>: the band's projected triangles (the wanted cells and the builds on their way), less the houses the
        /// rebuild drops, plus the houses it adds at the flat level. <see cref="Reason"/> asks for a raise only when the
        /// cheapest one fits this, and <see cref="AssignLevels"/> starts from the same room (adding what its demotions
        /// free), so a raise asked for is always made: no rebuild that changes nothing.
        /// </summary>
        private double RaiseRoom(B0HouseIndex idx, Version live, int[] houses, double cx, double cz, double nx, double nz, double bx, double bz)
        {
            double room = RaiseCapTris - _projectedTris;
            for (int p = 0; p < houses.Length; p++)
            {
                bool has = live != null && live.Has(p), inBuild = InBuild(idx, houses[p], cx, cz, nx, nz, bx, bz);
                if (has && !inBuild) room += live.HouseTris(Array.BinarySearch(live.Positions, p));
                else if (!has && inBuild) room -= _levelTris[BandConfig.LevelFlat];
            }
            return room;
        }

        /// <summary>What the band draws once no block stands in: the live versions of the cells wanted last frame (called
        /// after <see cref="Update"/> counted this frame), as they are now (a version swapped in by <see cref="Upload"/> since then counted as itself, no longer as a pending
        /// <see cref="Version.Delta"/>, so the projection never dips at a swap).</summary>
        private double WantedTris()
        {
            double t = 0;
            for (int i = 0; i < _all.Count; i++)
            {
                Cell c = _all[i];
                if (c.Tile != null && c.Tile.Visible && c.WantedFrame == _frame - 1 && c.Live != null && !c.Failed) t += c.Live.Tris;
            }
            return t;
        }

        /// <summary>Triangles the builds not yet shown add to the band when they replace their cells' versions.</summary>
        private double PendingDelta()
        {
            double d = 0;
            for (int i = 0; i < _ready.Count; i++)
                if (!_ready[i].Abandoned) d += _ready[i].Delta;
            for (int lane = 0; lane < _jobs.Length; lane++)
                if (_jobs[lane] != null && !_jobs[lane].Abandoned) d += _jobs[lane].Delta; // a finished job leaves _jobs for _ready
            return d;
        }

        /// <summary>True when house <paramref name="i"/> goes into a build: within the build pad of the fade around the
        /// centre or either lead centre.</summary>
        private bool InBuild(B0HouseIndex idx, int i, double cx, double cz, double nx, double nz, double bx, double bz)
        {
            float outer = _bands.B0OuterM;
            double pad = _bands.B0BuildPadM;
            return idx.Meets(i, outer, cx, cz, pad) || idx.Meets(i, outer, nx, nz, pad) || idx.Meets(i, outer, bx, bz, pad);
        }

        /// <summary>True while the centre stands (<see cref="StandingMps"/>).</summary>
        private bool Standing
        {
            get { return _vx * _vx + _vz * _vz < StandingMps * StandingMps; }
        }

        /// <summary>Houses of a version a rebuild would drop (not in a build).</summary>
        private int Unneeded(B0HouseIndex idx, Version v, double cx, double cz, double nx, double nz, double bx, double bz)
        {
            int[] houses = idx.Houses(_fine, v.Cell.X, v.Cell.Z);
            int n = 0;
            for (int k = 0; k < v.Positions.Length; k++)
                if (!InBuild(idx, houses[v.Positions[k]], cx, cz, nx, nz, bx, bz)) n++;
            return n;
        }

        /// <summary>
        /// Which B1 blocks of a tile stand in this frame (the fixed point): a block stands in when a house the band draws
        /// in it (its geometry within the radius plus half the fade) is not drawn by its cell; a cell is drawn only while
        /// none of the blocks its version's houses touch stands in, so B0 and a standing-in block never draw the same
        /// house, and every house the band draws is drawn by one of them.
        /// </summary>
        private void StandIn(B0Tile t, double cx, double cz)
        {
            B0HouseIndex idx = t.Index;
            float outer = _bands.B0OuterM;
            int per = t.BlocksPerSide;
            for (int pass = 0; pass < 256; pass++)
            {
                bool changed = false;
                for (int w = 0; w < _wanted.Count; w++)
                {
                    Cell c = _wanted[w];
                    if (!idx.CellMeets(_fine, c.X, c.Z, outer, cx, cz, 0)) continue;
                    Version live = c.Failed ? null : c.Live;
                    bool drawn = live != null && BlocksReady(t, live);
                    int[] houses = idx.Houses(_fine, c.X, c.Z);
                    for (int p = 0; p < houses.Length; p++)
                    {
                        if (drawn && live.Has(p)) continue;
                        int i = houses[p];
                        if (!idx.Meets(i, outer, cx, cz, 0)) continue;
                        int kx0, kz0, kx1, kz1;
                        idx.HouseBlocks(i, per, out kx0, out kz0, out kx1, out kz1);
                        for (int kz = kz0; kz <= kz1; kz++)
                            for (int kx = kx0; kx <= kx1; kx++)
                            {
                                if (!t.BlockReady[kz * per + kx]) continue;
                                t.BlockReady[kz * per + kx] = false;
                                changed = true;
                            }
                    }
                }
                if (!changed) return;
            }
        }

        /// <summary>True when none of the blocks a version's houses touch stands in.</summary>
        private static bool BlocksReady(B0Tile t, Version v)
        {
            int per = t.BlocksPerSide;
            for (int kz = v.BlockZ0; kz <= v.BlockZ1; kz++)
                for (int kx = v.BlockX0; kx <= v.BlockX1; kx++)
                    if (!t.BlockReady[kz * per + kx]) return false;
            return true;
        }

        /// <summary>True when a cell's live version may be drawn (built, not failed, none of its blocks standing in).</summary>
        private static bool Drawable(Cell c)
        {
            return c.Live != null && !c.Failed && c.Tile.Index != null && BlocksReady(c.Tile, c.Live);
        }

        private void SetShown(Cell c, bool on)
        {
            if (c.Shown == on) return;
            c.Shown = on;
            if (c.Live != null && c.Live.Handles != null)
                for (int k = 0; k < c.Live.Uploaded; k++) _sink.SetShown(c.Live.Handles[k], on);
            RegisterColliders(c);
        }

        /// <summary>True when B1 block <paramref name="block"/> of a tile (part index of the B1 layer) hands its inner
        /// range to B0 this frame (set by <see cref="Update"/>); false while it stands in.</summary>
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
            _job = null;
            for (int lane = 0; lane < _jobs.Length; lane++)
            {
                Version v = _jobs[lane];
                if (v == null) continue;
                if (v.Abandoned || v.Cell.Tile == null || v.Cell.WantedFrame != _frame || v.Cell.Pending != v)
                {
                    // Released, or no longer wanted: dropped between slices.
                    _jobs[lane] = null;
                    if (v.Cell.Pending == v) v.Cell.Pending = null;
                    Drop(v);
                }
                else if (v.Built || v.Error != null)
                {
                    _jobs[lane] = null;
                    Finish(v);
                }
            }
            if (now < _nextSliceMs) return;
            // An urgent build runs first; a missing house starts one even while a level build is half done (it pauses).
            bool urgent = _bestTile != null && _bestReason <= ReasonEdge && _ready.Count < MaxReady;
            if (_jobs[0] != null || !urgent && _jobs[1] != null)
            {
                _job = _jobs[0] ?? _jobs[1];
                Run(_runSlice);
                return;
            }

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
            if (!urgent && _jobs[1] != null) return;
            BuildsByReason[_bestReason]++;
            B0Tile bt = _bestTile;
            Cell cell;
            if (!bt.Cells.TryGetValue(_bestZ * 4096 + _bestX, out cell)) return;
            B0HouseIndex idx = bt.Index;
            double ox = bt.Id.X0, oz = bt.Id.Z0;
            double cx = camX - ox, cz = camZ - oz, nx = _needX - ox, nz = _needZ - oz, bx = _buildX - ox, bz = _buildZ - oz;
            int[] houses = idx.Houses(_fine, _bestX, _bestZ);
            Version live = cell.Live;
            _select.Clear();
            float hx0 = float.MaxValue, hz0 = float.MaxValue, hx1 = float.MinValue, hz1 = float.MinValue;
            for (int p = 0; p < houses.Length; p++)
            {
                int i = houses[p];
                if (!InBuild(idx, i, cx, cz, nx, nz, bx, bz)) continue;
                _select.Add(p);
                float ax, az, qx, qz;
                idx.HouseBounds(i, out ax, out az, out qx, out qz);
                hx0 = Math.Min(hx0, ax);
                hz0 = Math.Min(hz0, az);
                hx1 = Math.Max(hx1, qx);
                hz1 = Math.Max(hz1, qz);
            }
            var nv = new Version
            {
                Cell = cell, Positions = _select.ToArray(), CamX = cx, CamZ = cz, Mask = new ulong[(houses.Length + 63) >> 6],
                Colliders = new StructureColliders(), Data = _meshPool.Count > 0 ? _meshPool.Pop() : new MeshData(4096, 12288),
                Options = new[] { _bands.B0Options(BandConfig.LevelNear, _shared), _bands.B0Options(BandConfig.LevelLite, _shared),
                                  _bands.B0Options(BandConfig.LevelFlat, _shared) },
            };
            if (nv.Positions.Length > 0)
                B0HouseIndex.Blocks(hx0, hz0, hx1, hz1, bt.BlocksPerSide, out nv.BlockX0, out nv.BlockZ0, out nv.BlockX1, out nv.BlockZ1);
            int n = nv.Positions.Length;
            nv.Levels = new byte[n];
            nv.From = new int[n];
            nv.Ranges = new int[6 * (n + 1)];
            AssignLevels(idx, nv, houses, live, cx, cz, RaiseRoom(idx, live, houses, cx, cz, nx, nz, bx, bz));
            bool reusable = live != null && live.Data != null && live.Ranges != null;
            for (int k = 0; k < n; k++)
            {
                int p = nv.Positions[k];
                nv.Mask[p >> 6] |= 1UL << (p & 63);
                int from = -1;
                if (reusable && live.Has(p))
                {
                    from = Array.BinarySearch(live.Positions, p);
                    if (live.Levels[from] != nv.Levels[k]) from = -1;
                }
                nv.From[k] = from;
                if (from < 0) HousesBuilt++;
                else HousesReused++;
                nv.Delta += from >= 0 ? live.HouseTris(from) : _levelTris[nv.Levels[k]];
            }
            if (live != null) nv.Delta -= live.Tris;
            if (reusable)
            {
                nv.Reuse = live;
                nv.ReuseData = live.Data;
            }
            nv.Data.Clear();
            cell.Pending = nv;
            _jobs[urgent ? 0 : 1] = nv;
            _job = nv;
            Builds++;
            Run(_runSlice);
        }

        /// <summary>
        /// The level of each house of a new version, within <see cref="JobBudgetMs"/> of estimated work and the band's
        /// triangle budget (<paramref name="room"/>, <see cref="RaiseRoom"/>): a house the cell has keeps its level (copied)
        /// unless it wants another; a house it lacks is built at the flat level at least; demotions go first (cheap, and
        /// they make room), then the raises nearest first, each made while the work and the room allow it (a near house
        /// that does not fit may still fit at the lite level when it fronts a street); the others wait for a later rebuild.
        /// </summary>
        private void AssignLevels(B0HouseIndex idx, Version nv, int[] houses, Version live, double cx, double cz, double room)
        {
            int n = nv.Positions.Length;
            double cost = 0;
            const byte undecided = 255;
            for (int k = 0; k < n; k++)
            {
                int i = houses[nv.Positions[k]];
                int current = Current(live, nv.Positions[k]);
                int want = _bands.B0Level(idx.CentroidDistance(i, cx, cz), idx.Street(i), current);
                if (current < 0)
                {
                    // Every house the cell lacks is drawn, at the flat level at least (RaiseRoom counted it so).
                    nv.Levels[k] = BandConfig.LevelFlat;
                    cost += EstimateMs[BandConfig.LevelFlat];
                    if (want < BandConfig.LevelFlat) nv.Levels[k] = undecided;
                }
                else if (want == current)
                {
                    nv.Levels[k] = (byte)current;
                    cost += CopyEstimateMs;
                }
                else nv.Levels[k] = undecided;
            }
            while (true)
            {
                int best = -1;
                double bestKey = double.MaxValue;
                for (int k = 0; k < n; k++)
                {
                    if (nv.Levels[k] != undecided) continue;
                    int h = houses[nv.Positions[k]];
                    double d = idx.CentroidDistance(h, cx, cz);
                    int was = Current(live, nv.Positions[k]);
                    double key = was >= 0 && _bands.B0Level(d, idx.Street(h), was) > was ? -1e6 - d : d;
                    if (key < bestKey)
                    {
                        bestKey = key;
                        best = k;
                    }
                }
                if (best < 0) return;
                int i = houses[nv.Positions[best]];
                int current = Current(live, nv.Positions[best]);
                int want = _bands.B0Level(idx.CentroidDistance(i, cx, cz), idx.Street(i), current);
                // What the house costs now: its triangles in the live version, or the flat level it is built at otherwise.
                int from = current >= 0 ? current : BandConfig.LevelFlat;
                double now = current >= 0 ? live.HouseTris(Array.BinarySearch(live.Positions, nv.Positions[best])) : _levelTris[BandConfig.LevelFlat];
                double ms = current >= 0 ? 0 : -EstimateMs[BandConfig.LevelFlat]; // a new house is already charged at flat
                int level = from;
                if (want > from)
                {
                    // A demotion: always made while the work allows; it frees room for the raises.
                    if (cost + ms + EstimateMs[want] <= JobBudgetMs) level = want;
                }
                else
                {
                    for (int l = want; l < from && level == from; l++)
                    {
                        if (l == BandConfig.LevelLite && !(idx.Street(i) && _bands.B0LiteOuterM > 0f)) continue;
                        if (cost + ms + EstimateMs[l] <= JobBudgetMs && _levelTris[l] - now <= room) level = l;
                    }
                }
                nv.Levels[best] = (byte)level;
                if (level != from)
                {
                    cost += ms + EstimateMs[level];
                    room -= _levelTris[level] - now;
                }
                else if (current >= 0) cost += CopyEstimateMs;
            }
        }

        /// <summary>The level position <paramref name="p"/>'s house is drawn at in <paramref name="live"/> (-1: not in it).</summary>
        private static int Current(Version live, int p)
        {
            return live != null && live.Has(p) ? live.Levels[Array.BinarySearch(live.Positions, p)] : -1;
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
                        if (_clockMs() - start >= _sliceMs) break;
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

        /// <summary>
        /// Queues the builds the worker has finished for upload as soon as the main thread looks (the streamer's upload in
        /// <c>Tick</c> comes before the cells' <see cref="Update"/>): a build dispatched in one frame is on screen the next,
        /// not a frame later, which is what the lead (<see cref="BandConfig.B0LeadM"/>) has to cover. Builds of a cell no
        /// longer wanted are left to <see cref="Dispatch"/>, which drops them.
        /// </summary>
        private void Harvest()
        {
            if (_running || _disposed) return;
            for (int lane = 0; lane < _jobs.Length; lane++)
            {
                Version v = _jobs[lane];
                if (v == null || v.Abandoned || v.Cell.Tile == null || v.Cell.Pending != v || !(v.Built || v.Error != null)) continue;
                _jobs[lane] = null;
                Finish(v);
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
            v.Delta = v.Tris - (c.Live != null ? c.Live.Tris : 0);
            for (int k = 0; k < v.Positions.Length; k++)
            {
                if (v.From[k] >= 0) continue;
                int level = v.Levels[k], n = Math.Min(++_levelSamples[level], 64);
                _levelTris[level] += (v.HouseTris(k) - _levelTris[level]) / n;
            }
            _ready.Add(v);
        }

        // ------------------------------------------------------------------------------------------------------------
        // Uploads

        /// <summary>GPU bytes of the next chunk waiting for upload (0 when none), a build the worker finished since the last
        /// <see cref="Update"/> included (main thread).</summary>
        public long NextUploadBytes
        {
            get
            {
                Harvest();
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
            Harvest();
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
            if ((_jobs[0] != null && _jobs[0].Reuse == v) || (_jobs[1] != null && _jobs[1].Reuse == v))
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
            if (_jobs[0] == v || _jobs[1] == v) return; // a running or paused build is dropped by Dispatch
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
