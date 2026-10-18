using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// The terrain after every road cut and fill (owner playtest at Gaurighat: "Fix the roads on slopes. Make them flat like
    /// how they make them, cutting the hills"): the single carved surface the terrain mesh draws and that the samplers, the
    /// ground query, the drapes and every placer read. Roads stay level across (<see cref="RoadGrade"/>); the terrain is
    /// carved to meet them as hill roads are built in Nepal:
    /// <list type="bullet">
    /// <item>under each road's drawn extent (carriageway with its footpaths or shoulders) the terrain is the level bench (the
    /// road surface less its lift), and under a junction cap or a roundabout the junction's levelled platform
    /// (<see cref="RoadPads"/>);</item>
    /// <item>beyond it the uphill side is cut back at 1 : 1 (<see cref="CutSlope"/>, soil) after a level verge (the side
    /// drain: one terrain sub-quad diagonal, so no drawn triangle under the road rises above the bench) and the downhill
    /// side filled at 1 : 1.5 (<see cref="FillSlope"/>, an embankment), both steepening beyond <see cref="SteepenStartM"/>
    /// into a rock face, with a breast wall, gabion or stone retaining wall where the hillside is too steep for an open
    /// slope, until they meet the natural ground (the daylight line);</item>
    /// <item>each road contributes once, through the segment of its run nearest the point; where several roads reach a point
    /// (junctions, switchbacks, parallel pieces) every bound that agrees holds, nearer roads (measured from their drawn edge)
    /// win where bounds contradict, and the ground blends from one to the other across the gap between them, so a road's own
    /// carriageway is never overridden;</item>
    /// <item>lowered underpass stretches are just another cut, with near-vertical trench walls at their edges.</item>
    /// </list>
    /// <para>
    /// Benches come from <see cref="RoadGrade"/>, whose profiles are measured on the source tile's full-resolution grid, so
    /// every tier and every sampler of the tile carves the same road surface. Tile seams are continuous: a tile-border
    /// crossing's profile is level over <see cref="RoadGrade.SeamLandingM"/> on both sides and continued straight beyond the
    /// border, so both tiles carve the same bench there, and every other road's cut and fill relaxes to the natural ground on
    /// the tile border (<see cref="BorderFadeM"/>).
    /// </para>
    /// <para>
    /// The drawn surface is the natural terrain grid (<see cref="TerrainGrid"/>) with the cells where the carve moves the
    /// ground significantly (<see cref="RefineCutM"/>, <see cref="RefineMoveM"/>) refined into <see cref="Sub"/> × <see cref="Sub"/> sub-quads of about
    /// <see cref="SubCellM"/> (4 m on the coarse steps of the Low tier). A sub-vertex takes the carved height when every cell
    /// around it is refined, else the natural height (so a refined cell meets its unrefined neighbours on their straight
    /// edges without cracks, and the ground under gentle city streets keeps its natural triangles). Where the carve moves
    /// less than that the terrain stays natural, never more than <see cref="RefineCutM"/> above the bench (well under the
    /// 25 cm road lift). <see cref="TerrainMesher"/> draws exactly these triangles, <see cref="TileHeightSampler"/> interpolates exactly
    /// them and <see cref="GridDrape"/> drapes on them, so the ground query matches what is drawn. Built once per (tile,
    /// area, step) and cached; immutable and thread-safe afterwards.
    /// </para>
    /// </summary>
    public sealed partial class TerrainCarve
    {
        /// <summary>Cut slope beside a road (rise per metre): 1 : 1, soil.</summary>
        public const float CutSlope = 1.0f;

        /// <summary>Fill slope below a road (drop per metre): 1 : 1.5, an earth embankment.</summary>
        public const float FillSlope = 1f / 1.5f;

        /// <summary>Beyond this distance from the corridor the cut and fill faces steepen (rock cut, retaining wall).</summary>
        public const float SteepenStartM = 6f;

        /// <summary>Quadratic steepening of the faces beyond <see cref="SteepenStartM"/> (height per square metre).</summary>
        public const float SteepenRate = 0.5f;

        /// <summary>Farthest the cut and fill of a road reach beyond its corridor.</summary>
        public const float MaxReachM = 20f;

        /// <summary>Side of the refined sub-quads on the fine terrain steps (16 m cells and finer: the High and Mid tiers'
        /// detail tiles, the 8 m samplers of the lane graph and the solids).</summary>
        public const float SubCellM = 4f;

        /// <summary>Side of the refined sub-quads on cells coarser than 16 m (the Low tier's 32 m detail tiles); 0: those are
        /// not refined at all (the Low tier's terrain slice cannot afford it): their coarse grid is carved and lowered
        /// (<see cref="PreCarve"/>, <see cref="CoarseLower"/>) and the samplers still answer the bench under every road.</summary>
        public const float CoarseSubCellM = 0f;

        /// <summary>A cell is refined (drawn with carved sub-quads) where the carve moves a sub-vertex under a road's drawn
        /// extent or its verge by more than this (the bench of a hill road; the cut that keeps the ground of a gentle street
        /// under its lifted surface).</summary>
        public const float RefineCutM = 0.05f;

        /// <summary>A cell is also refined where the carve moves a sub-vertex anywhere by more than this (the visible cut
        /// slopes, embankments and walls of hill roads); smaller differences beside gentle city streets are not worth the
        /// triangles, so those keep their natural ground.</summary>
        public const float RefineMoveM = 0.8f;

        /// <summary>A road is cut and filled into the hillside (level bench, verge, slopes and walls) where the natural ground
        /// across its drawn extent departs from the level bench by more than this; below it the road is a street on gentle
        /// ground (or the bumps of the city's surface model) and the terrain is only cut where it would rise through the road
        /// (<see cref="StreetCutTolM"/>). Full strength from <see cref="CarveFullM"/>.</summary>
        public const float CarveStartM = 0.35f;

        /// <summary>How far ground under a street on gentle ground may lie below its bench (its skirt closes the edge).</summary>
        private const float StreetFillTolM = 10f;

        /// <summary>Extra steepness of a gentle street's cut beside it (practically vertical: nothing beyond the road moves).</summary>
        private const float WeakSlope = 20f;

        /// <summary>Departure from the level bench at which a road is fully cut into the hillside.</summary>
        public const float CarveFullM = 0.7f;

        /// <summary>A road is on a hillside where the ground falls at least this much across it (from
        /// <see cref="HillSpanM"/> beyond its drawn extent on one side to as far on the other), fully from
        /// <see cref="HillFallFull"/>.</summary>
        public const float HillFallStart = 0.08f;

        /// <summary>Fall across at which a road counts fully as a hill road.</summary>
        public const float HillFallFull = 0.15f;

        /// <summary>How far beyond each side the hillside's fall is measured: far enough that the building bumps of the city's
        /// surface model average out (a street between two rows of houses does not fall across), short enough to see the
        /// river terraces and hillsides the hill roads climb.</summary>
        public const float HillSpanM = 30f;

        /// <summary>On gentle ground the terrain under a road may stay this far above its bench (the road surface is lifted
        /// 25 cm above it), so city streets keep their natural ground.</summary>
        public const float StreetCutTolM = 0.2f;

        /// <summary>Within this distance of the tile border every cut and fill except a crossing road's seam landing relaxes
        /// to the natural ground (the neighbouring tile does not know this tile's roads).</summary>
        public const float BorderFadeM = 8f;

        private const float BorderFadeK = 4f;

        /// <summary>Slope of the trench walls beside a lowered underpass stretch.</summary>
        public const float WallSlope = 12f;

        /// <summary>Retaining walls lower than this are not built (an open slope does).</summary>
        public const float WallMinM = 1.5f;

        /// <summary>Tallest retaining wall; above it the face steepens into rock.</summary>
        public const float WallMaxM = 5.0f;

        /// <summary>Horizontal run of a retaining wall's face in the carve (the wall mesh covers it).</summary>
        public const float WallFaceM = 0.4f;

        // ------------------------------------------------------------------------------------------------------------
        // Elements
        // ------------------------------------------------------------------------------------------------------------

        /// <summary>One station of a road run: centreline point, bench (centre height less lift, cross-fall), drawn
        /// carriageway edges, drawn extent with footpaths or shoulders, the reach of its slopes and its walls.</summary>
        private struct Station
        {
            public double X, Z;

            /// <summary>Bench at the centreline (road surface less lift) and its cross-fall (per metre, left positive).</summary>
            public float A0, A1;

            /// <summary>Signed offsets of the carriageway edges (left, right) and the drawn extent on each side (positive
            /// distances from the centreline, footpaths and shoulders included).</summary>
            public float EL, ER, CL, CR;

            /// <summary>Reach of the slopes beyond the drawn extent on each side.</summary>
            public float RL, RR;

            /// <summary>Border relaxation gain (0 inside a seam landing, 1 elsewhere).</summary>
            public float G;

            /// <summary>Retaining wall height on each side: positive a breast wall holding a cut, negative a wall holding a
            /// fill; 0 none.</summary>
            public float WL, WR;

            /// <summary>A lowered underpass stretch: trench walls at the drawn edges.</summary>
            public bool Wall;

            /// <summary>Carve strength: 1 a hill road cut and filled into the slope, 0 a street on gentle ground (terrain cut
            /// only where it would rise through the road).</summary>
            public float S;

            /// <summary>Unit direction along the road (for the reach).</summary>
            public double Tx, Tz;

            /// <summary>The ribbon's section direction here (left of travel; not unit length): its rows stand on the
            /// centreline points and the junction cuts, each with its own direction (a cut's oblique section, a tile
            /// border's border line), and its quads blend linearly between them, so the drawn edges between two rows are
            /// straight chords from one row's edge points to the next. Offsets (<see cref="EL"/>, <see cref="CL"/>...)
            /// are measured along it, exactly as the ribbon places its vertices.</summary>
            public double WX, WZ;

            public int Road;
        }

        // Segments (structure of arrays).
        private int _segCount;
        private double[] _ax = new double[0], _az = new double[0], _len = new double[0], _dx = new double[0], _dz = new double[0];
        private float[] _a0a = new float[0], _a0b = new float[0], _a1a = new float[0], _a1b = new float[0];
        private float[] _ela = new float[0], _elb = new float[0], _era = new float[0], _erb = new float[0];
        private float[] _cla = new float[0], _clb = new float[0], _cra = new float[0], _crb = new float[0];
        private float[] _rl = new float[0], _rr = new float[0], _ga = new float[0], _gb = new float[0], _kc = new float[0], _kf = new float[0];
        private float[] _wla = new float[0], _wlb = new float[0], _wra = new float[0], _wrb = new float[0], _verge = new float[0];
        private float[] _sa = new float[0], _sb = new float[0];

        /// <summary>The ribbon's section direction at each end of a segment (<see cref="Station.WX"/>).</summary>
        private double[] _wxa = new double[0], _wza = new double[0], _wxb = new double[0], _wzb = new double[0];

        /// <summary>Longest section direction at either end of a segment (an oblique cut or border section is longer
        /// than 1), for the element boxes.</summary>
        private float[] _wmax = new float[0];
        private readonly List<float> _padS = new List<float>();
        private int[] _road = new int[0], _run = new int[0];
        private byte[] _flags = new byte[0];

        /// <summary>First and last segment of each run (consecutive segments of one road between two breaks).</summary>
        private readonly List<int> _runFirst = new List<int>(), _runLast = new List<int>();

        private const byte ClipStart = 1, ClipEnd = 2;

        private readonly List<RoadPad> _pads = new List<RoadPad>();

        /// <summary>A road crossing the tile border (its seam row, which lies on the border line): what the tile on the
        /// other side knows identically (<see cref="BorderCarved"/>).</summary>
        private struct Crossing
        {
            /// <summary>Centre of the seam row on the border (tile-local).</summary>
            public double X, Z;

            /// <summary>The seam row's section direction (along the border line; offsets along it are distances square to
            /// the road), outward from this tile, and the drawn extent on each side of it.</summary>
            public double WX, WZ;

            public float CL, CR;

            /// <summary>The level bench of the seam landing (the road surface less its lift).</summary>
            public float Bench;
        }

        private readonly List<Crossing> _crossings = new List<Crossing>();

        // Element grids (tile-local): every element by its box grown by its reach (strong) or verge (weak), and every
        // element by its drawn extent only (the analytic surface under roads, ExtentAt).
        private const double ElemCellM = 8.0;
        private int _ecells;
        private int[] _estart, _eitems;
        private int[] _xstart, _xitems;

        // ------------------------------------------------------------------------------------------------------------
        // Drawn surface
        // ------------------------------------------------------------------------------------------------------------

        public readonly TileData Source;
        public readonly TerrainGrid Grid;

        /// <summary>Sub-quads per grid cell side in refined cells.</summary>
        public readonly int Sub;

        /// <summary>Side of a sub-quad in metres.</summary>
        public readonly double SubM;

        /// <summary>
        /// Level verge (the side drain and shoulder) between a hill road's drawn extent and its cut slope: just wide enough
        /// that no sub-quad straddling the road's edge rises through the road surface (the bench plus a lift of at least
        /// 25 cm): a triangle from a sub-vertex on the bench inside the extent to one on a 1 : 1 slope outside it rises at
        /// most k (L - v)² / 4L over the edge for a sub-quad diagonal L and verge v, held to <see cref="PokeAllowM"/>.
        /// </summary>
        public readonly float VergeM;

        /// <summary>Verge of a street on gentle ground (one sub-quad diagonal plus a margin): the ground beside it in a
        /// refined cell stays within <see cref="StreetCutTolM"/> of its bench that far out, whatever the natural ground
        /// does beyond.</summary>
        public readonly float StreetVergeM;

        /// <summary>Where a breast wall stands beyond a hill road's drawn extent (one sub-quad diagonal plus a margin: its
        /// face rises metres within <see cref="WallFaceM"/>, so no sub-quad may reach from it over the road).</summary>
        public readonly float WallVergeM;

        /// <summary>Verge beside every road on a grid that refines nothing (the Low tier's 32 m cells): a side drain and
        /// shoulder.</summary>
        public const float UnrefinedVergeM = 1.5f;

        /// <summary>Most the terrain of a sub-quad straddling a road's edge may rise over the bench at the edge (the road
        /// surface stands its lift, 25 cm or more, above the bench).</summary>
        public const float PokeAllowM = 0.18f;

        private readonly int[] _block; // per grid cell (row-major from the south-west): start into _heights, or -1

        /// <summary>The coarse grid's vertex heights as drawn: carved beside the roads (<see cref="PreCarve"/>) and lowered
        /// under them (<see cref="CoarseLower"/>), row-major from the south-west.</summary>
        private readonly float[] _coarse;

        /// <summary>The coarse grid's natural vertex heights.</summary>
        private readonly float[] _natural;

        private readonly byte[] _moved;

        /// <summary>Per grid cell: the coarse lowering could not bring the ground under a road down to its bench (a
        /// deeper bump than <see cref="MaxLowerM"/>), so the cell is refined.</summary>
        private readonly bool[] _poke;

        private float[] _heights;
        private readonly double _offX, _offZ; // area origin in tile-local metres

        /// <summary>This grid refines cells at all (<see cref="CoarseSubCellM"/>).</summary>
        private readonly bool _refine;

        /// <summary>Grid cells refined (drawn with carved sub-quads).</summary>
        public int RefinedCells { get; private set; }

        /// <summary>Grid cells refined because the coarse lowering could not hide the ground under a road.</summary>
        public int PokedCells { get; private set; }

        /// <summary>Coarse grid vertices moved by the carve (carved beside a road or lowered under one).</summary>
        public int LoweredVertices { get; private set; }

        /// <summary>The most a coarse vertex is lowered under a road (deeper bumps of the surface model refine the cell).</summary>
        public const float MaxLowerM = 3f;

        /// <summary>Diagnostics: the sub-quad sides in use (tests tune them).</summary>
        internal static float TuneSubCellM = SubCellM, TuneCoarseSubCellM = CoarseSubCellM;

        /// <summary>Diagnostics: the refinement threshold and the lowering reach in use (tests tune them).</summary>
        internal static float TuneRefineMove = RefineMoveM, TuneMaxLower = MaxLowerM;

        /// <summary>Diagnostics: judge refinement on the hill roads' carve only (the gentle streets' verges beside a road
        /// do not refine a cell; they still shape a refined one).</summary>
        internal static bool TuneStrongCriterion = true;

        /// <summary>Carve the coarse grid vertices beside the roads too (diagnostics: off leaves them natural, so only
        /// refined cells show the cut and fill).</summary>
        internal static bool PreCarveOn = true;

        /// <summary>Road segments and junction platforms the carve follows.</summary>
        public int ElementCount
        {
            get { return _segCount + _pads.Count; }
        }

        /// <summary>Diagnostics (tests): record why cells are refined.</summary>
        internal static bool DiagOn;

        /// <summary>While pre-carving the coarse grid: only the hill roads' cut and fill count (not the gentle streets'
        /// verges).</summary>
        private bool _strongOnly;

        internal readonly int[] DiagBuckets = new int[4];
        internal int DiagCutOnly;

        /// <summary>Diagnostics: a PPM map of the tile (8 px per grid cell): refined cells red, lowered vertices blue, segments
        /// by strength (white strong, grey weak), pads (yellow hill, dark yellow gentle).</summary>
        internal void WriteMap(string path)
        {
            int q = Grid.Quads, px = 8, w = q * px;
            var img = new byte[w * w * 3];
            for (int j = 0; j < q; j++)
            for (int i = 0; i < q; i++)
            {
                bool rf = IsRefined(i, j);
                for (int b = 0; b < px; b++)
                for (int a = 0; a < px; a++)
                {
                    int x = i * px + a, y = w - 1 - (j * px + b), o = 3 * (y * w + x);
                    img[o] = (byte)(rf ? 150 : 40);
                    img[o + 1] = 40;
                    img[o + 2] = 40;
                }
            }
            for (int k = 0; k <= q; k++)
            for (int l = 0; l <= q; l++)
                if (IsMoved(k, l))
                {
                    int x = Math.Min(w - 1, l * px), y = w - 1 - Math.Min(w - 1, k * px);
                    for (int d = -2; d <= 2; d++)
                    {
                        int xx = Math.Max(0, Math.Min(w - 1, x + d)), o = 3 * (y * w + xx);
                        img[o] = 60;
                        img[o + 1] = 120;
                        img[o + 2] = 255;
                    }
                }
            double cm = Grid.CellM / px;
            for (int e = 0; e < _segCount; e++)
            {
                float st = Math.Max(_sa[e], _sb[e]);
                byte c = (byte)(st > 0f ? 255 : 130);
                int n = (int)Math.Ceiling(_len[e] / cm) + 1;
                for (int k = 0; k <= n; k++)
                {
                    double t = (double)k / n, x = _ax[e] + _dx[e] * _len[e] * t - _offX, z = _az[e] + _dz[e] * _len[e] * t - _offZ;
                    int ix = (int)(x / cm), iy = w - 1 - (int)(z / cm);
                    if (ix < 0 || iy < 0 || ix >= w || iy >= w) continue;
                    int o = 3 * (iy * w + ix);
                    img[o] = c;
                    img[o + 1] = c;
                    img[o + 2] = st > 0f ? (byte)(255 * (1 - st)) : c;
                }
            }
            foreach (RoadPad p in _pads)
            {
                int ix = (int)((p.CX - _offX) / cm), iy = w - 1 - (int)((p.CZ - _offZ) / cm);
                for (int d = -3; d <= 3; d++)
                for (int d2 = -3; d2 <= 3; d2++)
                {
                    int xx = ix + d, yy = iy + d2;
                    if (xx < 0 || yy < 0 || xx >= w || yy >= w) continue;
                    int o = 3 * (yy * w + xx);
                    img[o] = (byte)(p.Hill ? 255 : 120);
                    img[o + 1] = (byte)(p.Hill ? 230 : 100);
                    img[o + 2] = 0;
                }
            }
            using (var f = System.IO.File.Create(path))
            {
                byte[] head = System.Text.Encoding.ASCII.GetBytes("P6\n" + w + " " + w + "\n255\n");
                f.Write(head, 0, head.Length);
                f.Write(img, 0, img.Length);
            }
        }

        /// <summary>Diagnostics: segments and pads at full, partial and no carve strength.</summary>
        internal string StrengthStats()
        {
            int full = 0, part = 0, none = 0, pf = 0, pp = 0, pn = 0;
            for (int e = 0; e < _segCount; e++)
            {
                float m = Math.Max(_sa[e], _sb[e]);
                if (m >= 0.99f) full++;
                else if (m > 0f) part++;
                else none++;
            }
            foreach (float v in _padS)
            {
                if (v >= 0.99f) pf++;
                else if (v > 0f) pp++;
                else pn++;
            }
            return "segments full " + full + " partial " + part + " none " + none + "; pads full " + pf + " partial " + pp + " none " + pn;
        }

        /// <summary>Milliseconds the build took (diagnostics).</summary>
        public double BuildMs { get; private set; }

        // ------------------------------------------------------------------------------------------------------------
        // Cache
        // ------------------------------------------------------------------------------------------------------------

        private sealed class Slot
        {
            public TileId Area;
            public int Step;
            public TerrainCarve Carve;
            public bool Done;
            public int Generation;
            public readonly object Gate = new object();
        }

        private static readonly ConditionalWeakTable<TileData, List<Slot>> Cache = new ConditionalWeakTable<TileData, List<Slot>>();
        private static readonly object CacheLock = new object();

        [ThreadStatic] private static int _building;

        /// <summary>Diagnostics (tests that tune the carve): carves built before the generation changed are not reused by
        /// samplers created afterwards.</summary>
        internal static volatile int CacheGeneration;

        /// <summary>Carving on (default). Off: every sampler and terrain mesh is the natural surface (diagnostics only).</summary>
        public static bool Enabled = true;

        /// <summary>True while a carve is being built on this thread (samplers answer the natural surface meanwhile).</summary>
        public static bool Building
        {
            get { return _building > 0; }
        }

        /// <summary>
        /// The carve of <paramref name="sampler"/>'s tile, area and step, or null when nothing is carved there (no roads, an
        /// area cropped from an ancestor, no heights, a level tile (relief under <see cref="LevelReliefM"/>: the roads lie on
        /// the ground as drawn, nothing to cut or fill), or carving off). Built on first use and cached per tile; the grade it
        /// follows is <see cref="RoadGrade.For"/> on the sampler with default road options.
        /// </summary>
        public static TerrainCarve For(TileHeightSampler sampler)
        {
            if (sampler == null || !Enabled || _building > 0) return null;
            TileData src = sampler.SourceTile;
            if (src == null || !sampler.HasHeights || src.Roads.Count == 0 || sampler.Area != src.Tile || sampler.Grid.SubSample) return null;
            Slot slot = null;
            lock (CacheLock)
            {
                List<Slot> list;
                if (!Cache.TryGetValue(src, out list))
                {
                    list = new List<Slot>(2);
                    Cache.Add(src, list);
                }
                foreach (Slot s in list)
                    if (s.Area == sampler.Area && s.Step == sampler.Step && s.Generation == CacheGeneration)
                    {
                        slot = s;
                        break;
                    }
                if (slot == null)
                {
                    slot = new Slot { Area = sampler.Area, Step = sampler.Step, Generation = CacheGeneration };
                    if (list.Count >= 4) list.RemoveAt(0);
                    list.Add(slot);
                }
            }
            lock (slot.Gate)
            {
                if (slot.Done) return slot.Carve;
                _building++;
                try
                {
                    if (IsLevel(src, sampler.Grid))
                    {
                        slot.Carve = null;
                    }
                    else
                    {
                        var c = new TerrainCarve(src, sampler);
                        slot.Carve = c.ElementCount > 0 ? c : null;
                    }
                }
                finally
                {
                    _building--;
                }
                slot.Done = true;
                return slot.Carve;
            }
        }

        /// <summary>Relief (highest minus lowest grid vertex) under which a tile is level and carves nothing: its roads lie
        /// on the ground with their lift, so there is neither cut nor fill (and nothing reads the tile's roads early).</summary>
        public const float LevelReliefM = 0.01f;

        private static bool IsLevel(TileData src, TerrainGrid grid)
        {
            int q = grid.Quads;
            float lo = float.MaxValue, hi = float.MinValue;
            for (int k = 0; k <= q; k++)
            for (int l = 0; l <= q; l++)
            {
                float h = grid.VertexHeight(src, k, l);
                if (h < lo) lo = h;
                if (h > hi) hi = h;
                if (hi - lo >= LevelReliefM) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------------------------------------------------
        // Build
        // ------------------------------------------------------------------------------------------------------------

        private TerrainCarve(TileData src, TileHeightSampler sampler)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Source = src;
            Grid = sampler.Grid;
            double target = Grid.CellM > 16.0 + 1e-6 ? TuneCoarseSubCellM : TuneSubCellM;
            _refine = target > 0.0;
            if (!_refine) target = Grid.CellM;
            int sub = 1;
            while (Grid.CellM / (sub * 2) >= target - 1e-6 && sub < 32) sub *= 2;
            Sub = sub;
            SubM = Grid.CellM / sub;
            if (_refine)
            {
                double diag = SubM * Math.Sqrt(2.0);
                VergeM = (float)Math.Max(1.0, diag - Math.Sqrt(4.0 * diag * PokeAllowM / CutSlope) + 0.2);
                StreetVergeM = (float)(diag + 0.3);
                WallVergeM = (float)(diag + 0.3);
            }
            else
            {
                // Nothing is refined, so nothing can rise through a road but the coarse triangles the lowering handles:
                // the verges are the real drain and shoulder.
                VergeM = StreetVergeM = WallVergeM = UnrefinedVergeM;
            }
            _offX = Grid.Area.X0 - src.Tile.X0;
            _offZ = Grid.Area.Z0 - src.Tile.Z0;
            int q = Grid.Quads;
            _block = new int[q * q];
            for (int k = 0; k < _block.Length; k++) _block[k] = -1;
            _heights = new float[0];
            int side = q + 1;
            _coarse = new float[side * side];
            _natural = new float[side * side];
            _moved = new byte[side * side];
            _poke = new bool[q * q];
            for (int k = 0; k <= q; k++)
            for (int l = 0; l <= q; l++) _coarse[k * side + l] = _natural[k * side + l] = Grid.VertexHeight(src, k, l);
            RoadGrade grade = RoadGrade.For(src, sampler, new RoadOptions());
            CollectSegments(grade);
            foreach (RoadPad p in grade.Pads.Pads)
            {
                _pads.Add(p);
                _padS.Add(p.Hill ? 1f : 0f);
            }
            IndexElements();
            if (PreCarveOn) PreCarve();
            CoarseLower();
            if (_refine) Rasterise();
            else _heights = new float[0];
            BuildMs = sw.Elapsed.TotalMilliseconds;
        }

        /// <summary>The natural ground as this area's grid draws it (tile-local metres).</summary>
        private float Natural(double lx, double lz)
        {
            double cm = Grid.CellM;
            return (float)GridSurface(_natural, (lx - _offX) / cm, (lz - _offZ) / cm);
        }

        /// <summary>A surface of this area's grid at area-local fractional grid coordinates (u, v), clamped, split along the
        /// (i, j)-(i+1, j+1) diagonal as <see cref="TerrainGrid.Surface"/>.</summary>
        private double GridSurface(float[] heights, double u, double v)
        {
            double du, dv;
            return GridSurface(heights, u, v, out du, out dv);
        }

        private double GridSurface(float[] heights, double u, double v, out double dhdu, out double dhdv)
        {
            int q = Grid.Quads, side = q + 1;
            if (u < 0) u = 0;
            else if (u > q) u = q;
            if (v < 0) v = 0;
            else if (v > q) v = q;
            int i = (int)Math.Floor(u), j = (int)Math.Floor(v);
            if (i > q - 1) i = q - 1;
            if (j > q - 1) j = q - 1;
            double fu = u - i, fv = v - j;
            int b = j * side + i;
            double h00 = heights[b], h10 = heights[b + 1], h01 = heights[b + side], h11 = heights[b + side + 1];
            if (fu >= fv)
            {
                dhdu = h10 - h00;
                dhdv = h11 - h10;
            }
            else
            {
                dhdu = h11 - h01;
                dhdv = h01 - h00;
            }
            return h00 + fu * dhdu + fv * dhdv;
        }

        private void CollectSegments(RoadGrade grade)
        {
            RoadLayout lay = grade.Layout;
            TileData t = Source;
            var run = new List<Station>(64);
            var pos = new List<double>(256);
            for (int i = 0; i < t.Roads.Count; i++)
            {
                if (!grade.Has(i) || grade.IsAbsolute(i) || !lay.DrawsRibbon(i)) continue;
                RoadCentreline c = lay.Centres[i];
                if (c == null || c.Count < 2) continue;
                RoadRecord r = t.Roads[i];
                int n = grade.StationCount(i);
                double step = grade.StationStep(i);
                float lift = grade.LiftOf(i);
                bool seamStart = r.HasPrevContext, seamEnd = r.HasNextContext;
                double arcLen = c.LengthM;
                // Sample positions: the profile's stations, every centreline point (where the ribbon's rows stand, so
                // no segment spans a bend of the drawn edges) and every cut (runs end exactly where the ribbon stops).
                Rows(lay, i, c);
                pos.Clear();
                for (int k = 0; k < n; k++) pos.Add(c.AlongAtArc(k * step));
                for (int k = 0; k < _rowS.Count; k++) pos.Add(_rowS[k]);
                // Where an absorbed stretch (drawn as the other road's footpath) ends, the ribbon starts again: the run does
                // too, so no hole opens between the ribbon's first row and the next station.
                double[] ab = lay.Absorbed[i];
                if (ab != null)
                    for (int k = 0; k < ab.Length; k++)
                    {
                        double sa = (k & 1) == 0 ? ab[k] - 1e-3 : ab[k] + 1e-3;
                        if (sa > 0 && sa < c.RawLengthM) pos.Add(sa);
                    }
                pos.Sort();
                run.Clear();
                bool clipStart = seamStart;
                double prev = double.NegativeInfinity;
                foreach (double s in pos)
                {
                    if (s - prev < 1e-4) continue;
                    prev = s;
                    Station st;
                    if (!StationAt(grade, lay, i, s, lift, seamStart, seamEnd, arcLen, out st))
                    {
                        // Not drawn here (inside a junction cap or ring, absorbed, on a deck): the run ends clipped.
                        if (run.Count > 0) FlushRun(run, clipStart, true);
                        run.Clear();
                        clipStart = true;
                        continue;
                    }
                    run.Add(st);
                }
                // A tile-border end is clipped (the seam extension continues it); a free end or a knee is rounded.
                FlushRun(run, clipStart, seamEnd);
                // Seam extensions: the crossing continues straight and level beyond a tile-border cut, so both tiles carve
                // the same bench beside the border.
                if (seamStart) Extension(grade, lay, i, c, 0, lift);
                if (seamEnd) Extension(grade, lay, i, c, c.Count - 1, lift);
            }
        }

        // The ribbon's rows of the road being collected (RibbonMesher's candidates: every centreline point and junction
        // cut, a tile-border end's section on the border line): raw along and section direction.
        private readonly List<double> _rowS = new List<double>(64), _rowUx = new List<double>(64), _rowUz = new List<double>(64);

        /// <summary>A tile-border end's section is never flatter than this sine against the road (RibbonMesher).</summary>
        private const double MinCutSine = 1.0 / 3.0;

        /// <summary>The ribbon's rows of road <paramref name="i"/> as RibbonMesher stands them (before it thins them, within
        /// its 5 cm plan tolerance): every centreline point with the curve's left normal there, every junction cut with
        /// its section direction, and a tile-border end with its section on the border line.</summary>
        private void Rows(RoadLayout lay, int i, RoadCentreline c)
        {
            _rowS.Clear();
            _rowUx.Clear();
            _rowUz.Clear();
            RoadCut[] cuts = lay.Cuts[i];
            RoadRecord r = Source.Roads[i];
            int sizeCm = (int)Math.Round(Source.Tile.Size * 100.0);
            int next = 0;
            for (int k = 0; k < c.Count; k++)
            {
                double s = c.S[k];
                while (cuts != null && next < cuts.Length && cuts[next].S < s - 1e-6)
                {
                    RoadCut cu = cuts[next++];
                    if (_rowS.Count > 0 && cu.S <= _rowS[_rowS.Count - 1] + 1e-6) continue;
                    AddRow(cu.S, cu.UX, cu.UZ);
                }
                if (cuts != null && next < cuts.Length && Math.Abs(cuts[next].S - s) <= 1e-6)
                {
                    RoadCut cu = cuts[next++];
                    AddRow(cu.S, cu.UX, cu.UZ);
                    continue;
                }
                if (_rowS.Count > 0 && s <= _rowS[_rowS.Count - 1] + 1e-9) continue;
                double tx = c.Tx[k], tz = c.Tz[k], ux = -tz, uz = tx;
                if (k == 0 && c.StartCut || k == c.Count - 1 && c.EndCut)
                {
                    int pi = k == 0 ? 1 : r.PointCount - 2;
                    int xCm = r.Points[2 * pi], zCm = r.Points[2 * pi + 1];
                    int border = xCm == 0 || xCm == sizeCm ? 0 : zCm == 0 || zCm == sizeCm ? 1 : -1;
                    if (border >= 0)
                    {
                        // The section lies on the border line, where both tiles' ribbons meet.
                        double ex = border == 0 ? 0 : 1, ez = border == 0 ? 1 : 0;
                        double crs = ex * tz - ez * tx;
                        if (Math.Abs(crs) < MinCutSine) crs = crs >= 0 ? MinCutSine : -MinCutSine;
                        ux = -ex / crs;
                        uz = -ez / crs;
                    }
                }
                AddRow(s, ux, uz);
            }
            while (cuts != null && next < cuts.Length)
            {
                RoadCut cu = cuts[next++];
                if (_rowS.Count > 0 && cu.S <= _rowS[_rowS.Count - 1] + 1e-6) continue;
                AddRow(cu.S, cu.UX, cu.UZ);
            }
        }

        private void AddRow(double s, double ux, double uz)
        {
            _rowS.Add(s);
            _rowUx.Add(ux);
            _rowUz.Add(uz);
        }

        /// <summary>The ribbon's section direction at raw along <paramref name="s"/> of the road whose rows are loaded: the
        /// linear blend of the two rows around it (the ribbon's quads are bilinear between its rows).</summary>
        private void RowDirection(double s, out double wx, out double wz)
        {
            int n = _rowS.Count;
            if (n == 0)
            {
                wx = 0;
                wz = 1;
                return;
            }
            if (s <= _rowS[0] || n == 1)
            {
                wx = _rowUx[0];
                wz = _rowUz[0];
                return;
            }
            if (s >= _rowS[n - 1])
            {
                wx = _rowUx[n - 1];
                wz = _rowUz[n - 1];
                return;
            }
            int lo = 0, hi = n - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (_rowS[mid] <= s) lo = mid;
                else hi = mid;
            }
            double ds = _rowS[hi] - _rowS[lo], f = ds > 1e-12 ? (s - _rowS[lo]) / ds : 0;
            wx = _rowUx[lo] + (_rowUx[hi] - _rowUx[lo]) * f;
            wz = _rowUz[lo] + (_rowUz[hi] - _rowUz[lo]) * f;
        }

        /// <summary>How far the footpath of a profile side reaches at raw along <paramref name="s"/> where it is drawn (at
        /// least <see cref="RoadWidthModel.MinFootpathM"/>), 0 where none is (as RoadLayout.CorridorSides reads it).</summary>
        private static float FootReach(RoadWidthProfile p, float[] foot, double s)
        {
            if (p.Count <= 1) return p.Count == 1 && foot[0] > 0f ? Math.Max(foot[0], RoadWidthModel.MinFootpathM) : 0f;
            int i = (int)Math.Floor(s / Math.Max(1e-6, p.StepM));
            i = i < 0 ? 0 : i > p.Count - 2 ? p.Count - 2 : i;
            float v = Math.Max(foot[i], foot[i + 1]);
            return v > 0f ? Math.Max(v, RoadWidthModel.MinFootpathM) : 0f;
        }

        /// <summary>The extent reaches this far beyond the outer edge of a drawn footpath or shoulder: the ribbon and the
        /// road surface query level them on the bench from the kerb to the outer edge (they add the terrain's rise between
        /// the two, which is the bench's on the extent), so the outer edge must lie on it wherever the two place it.</summary>
        public const float SideMarginM = 0.15f;

        /// <summary>
        /// How far the drawn surface beside each carriageway edge of road <paramref name="i"/> reaches at raw along
        /// <paramref name="s"/>, as the ribbon's side treatments draw them (RibbonMesher, RoadSweep.Side): a footpath, else
        /// a shoulder (not on a dual carriageway's outer side, which drops in a skirt), each with <see cref="SideMarginM"/>,
        /// else nothing (a skirt); the median side of a dual carriageway reaches half the median; a lowered stretch's
        /// retaining walls stand at the edges.
        /// </summary>
        private static void DrawnSides(RoadLayout lay, int i, double s, bool lowered, out float left, out float right)
        {
            left = right = 0f;
            if (lowered) return;
            RoadWidthProfile prof = lay.Profiles[i];
            bool dual = prof.Dual;
            float shoulder = lay.ShoulderAt(i, s);
            float footL = FootReach(prof, prof.FootLeft, s), footR = dual ? 0f : FootReach(prof, prof.FootRight, s);
            left = footL > 0f ? footL + SideMarginM : shoulder > 0f && !dual ? shoulder + SideMarginM : 0f;
            right = dual ? 0.5f * Math.Max(RoadWidthModel.MinMedianM, lay.Attrs[i].MedianCm / 100f) : footR > 0f ? footR + SideMarginM :
                shoulder > 0f ? shoulder + SideMarginM : 0f;
        }

        /// <summary>The station of road <paramref name="i"/> at raw along <paramref name="s"/>; false where the ribbon is not
        /// drawn there (cap gaps, absorbed stretches, decks).</summary>
        private bool StationAt(RoadGrade grade, RoadLayout lay, int i, double s, float lift, bool seamStart, bool seamEnd, double arcLen, out Station st)
        {
            st = default(Station);
            if (lay.InGap(i, s) || lay.InAbsorbed(i, s) || grade.OnDeck(i, s)) return false;
            RoadGrade.Row row = grade.RowAt(i, s);
            if (row.DeckW > 0.01f) return false;
            RoadCentreline c = lay.Centres[i];
            double x, z, tx, tz;
            c.At(s, out x, out z, out tx, out tz);
            RoadWidthProfile prof = lay.Profiles[i];
            float half = 0.5f * prof.DrawnAt(s), shift = prof.ShiftAt(s);
            bool wall = grade.LoweringAt(i, s) > 0.01f;
            float left, right;
            DrawnSides(lay, i, s, wall, out left, out right);
            float y0 = grade.BenchY(row, 0), y1 = grade.BenchY(row, 1);
            double wx, wz;
            RowDirection(s, out wx, out wz);
            st = new Station
            {
                X = x, Z = z, A0 = y0 - lift, A1 = y1 - y0, EL = shift + half, ER = shift - half, CL = shift + half + left, CR = -(shift - half) + right,
                Wall = wall, Road = i, WX = wx, WZ = wz,
            };
            // Seam landing: no border fade within the level landing (both tiles carve it alike), ramping in beyond.
            double l = c.ArcAt(s);
            double fromSeam = double.MaxValue;
            if (seamStart) fromSeam = Math.Min(fromSeam, l);
            if (seamEnd) fromSeam = Math.Min(fromSeam, arcLen - l);
            st.G = fromSeam <= RoadGrade.SeamLandingM ? 0f : (float)Math.Min(1.0, (fromSeam - RoadGrade.SeamLandingM) / 4.0);
            st.Tx = tx;
            st.Tz = tz;
            st.S = wall ? 1f : Strength(st);
            return true;
        }

        /// <summary>
        /// How much a station is a hill road: the largest departure of the natural ground (the tile's full-resolution
        /// surface, the same on every tier) from the level bench across the drawn extent, mapped from
        /// <see cref="CarveStartM"/> (0) to <see cref="CarveFullM"/> (1).
        /// </summary>
        private float Strength(in Station st)
        {
            double ux = -st.Tz, uz = st.Tx;
            float dev = 0f;
            for (int k = 0; k <= 6; k++)
            {
                double off = -st.CR + (st.CL + st.CR) * k / 6.0;
                double co = off > st.EL ? st.EL : off < st.ER ? st.ER : off;
                float bench = st.A0 + (float)co * st.A1;
                dev = Math.Max(dev, Math.Abs(SourceNatural(st.X + ux * off, st.Z + uz * off) - bench));
            }
            // The hillside itself: the fall of the ground across the road and HillSpanM beyond each side (a city street
            // between two rows of the surface model's building bumps does not fall across).
            double ol = st.CL + HillSpanM, or = -(st.CR + HillSpanM);
            float fall = Math.Abs(SourceNatural(st.X + ux * ol, st.Z + uz * ol) - SourceNatural(st.X + ux * or, st.Z + uz * or)) / (float)(ol - or);
            // Only clear hill roads are cut into the slope; the rest of the transition is the gentle streets' coarse cut.
            return Smoothstep(0.4f, 0.8f, Smoothstep(CarveStartM, CarveFullM, dev) * Smoothstep(HillFallStart, HillFallFull, fall));
        }

        /// <summary>The tile's full-resolution natural ground at tile-local metres (the same on every tier).</summary>
        private float SourceNatural(double lx, double lz)
        {
            return TerrainGrid.SourceSurface(Source, Source.Tile.X0 + lx, Source.Tile.Z0 + lz);
        }

        private static float Smoothstep(float e0, float e1, float x)
        {
            float t = (x - e0) / (e1 - e0);
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return t * t * (3f - 2f * t);
        }

        /// <summary>The strengths along a run widened by two stations (a hill road is carved a little beyond where the slope
        /// demands it, so the cut does not flicker on and off) and softened, then the reach of every station.</summary>
        private void Strengthen(List<Station> run)
        {
            int n = run.Count;
            var tmp = new float[n];
            for (int k = 0; k < n; k++)
            {
                float m = 0f;
                for (int j = Math.Max(0, k - 2); j <= Math.Min(n - 1, k + 2); j++) m = Math.Max(m, run[j].S);
                tmp[k] = m;
            }
            for (int k = 0; k < n; k++)
            {
                Station st = run[k];
                float a = tmp[Math.Max(0, k - 1)], b = tmp[Math.Min(n - 1, k + 1)];
                st.S = Math.Max(st.S, 0.25f * a + 0.5f * tmp[k] + 0.25f * b);
                Reach(ref st, st.Tx, st.Tz);
                run[k] = st;
            }
        }

        /// <summary>
        /// Retaining walls and reach of a station on each side. A wall is built where the open slope (1 : 1 cut after the
        /// verge, 1 : 1.5 fill) would not meet the natural ground within <see cref="SteepenStartM"/>: a breast wall holding the
        /// cut, or a gabion or stone wall holding the fill, as tall as that needs (from <see cref="WallMinM"/> to
        /// <see cref="WallMaxM"/>). The reach is how far beyond the drawn extent the cut and fill (with the wall) still bite:
        /// the natural ground is sampled outward until it lies inside the bounds for good (plus a margin), at most
        /// <see cref="MaxReachM"/>.
        /// </summary>
        private void Reach(ref Station st, double tx, double tz)
        {
            double ux = -tz, uz = tx;
            float weak = 1f - st.S;
            float verge = st.Wall ? 0.3f : st.S >= 0.99f ? VergeM : StreetVergeM;
            if (st.S <= 0f && !st.Wall)
            {
                // A street on gentle ground: the bench under its drawn extent and a verge no higher than the bench plus
                // StreetCutTolM beside it, nothing beyond (no slopes, no walls).
                st.RL = st.RR = verge + 0.5f;
                st.WL = st.WR = 0f;
                return;
            }
            float tolC = weak * StreetCutTolM, tolF = weak * StreetFillTolM;
            for (int side = 0; side < 2; side++)
            {
                double sign = side == 0 ? 1 : -1;
                float ext = side == 0 ? st.CL : st.CR;
                float edge = side == 0 ? st.EL : st.ER;
                float bench = st.A0 + edge * st.A1;
                float kc = (st.Wall ? WallSlope : CutSlope) + weak * WeakSlope, kf = (st.Wall ? WallSlope : FillSlope) + weak * WeakSlope;
                float wall = 0f;
                // Retaining walls hold carriageways; footways, paths and steps keep open slopes.
                if (!st.Wall && st.S >= 0.5f && !RoadWidthModel.IsFootClass(Source.Roads[st.Road].RoadClass))
                {
                    float ec = float.NegativeInfinity, ef = float.NegativeInfinity, ec6 = 0f, ef6 = 0f;
                    for (int d = 0; d <= (int)SteepenStartM; d++)
                    {
                        // On the tile's full-resolution ground, so every tier (and the solids, built on the 8 m sampler)
                        // builds the same walls.
                        float rc = SourceNatural(st.X + ux * sign * (ext + verge + d), st.Z + uz * sign * (ext + verge + d)) - bench;
                        float rf = SourceNatural(st.X + ux * sign * (ext + d), st.Z + uz * sign * (ext + d)) - bench;
                        float c = rc - kc * d, f = -rf - kf * d;
                        if (c > ec) ec = c;
                        if (f > ef) ef = f;
                        if (d == (int)SteepenStartM)
                        {
                            ec6 = c;
                            ef6 = f;
                        }
                    }
                    if (ec6 > 0f && ec >= WallMinM && ec >= ef) wall = Math.Min(WallMaxM, ec);
                    else if (ef6 > 0f && ef >= WallMinM) wall = -Math.Min(WallMaxM, ef);
                }
                float last = -1f;
                for (int d = 0; d <= (int)MaxReachM; d += 2)
                {
                    double off = sign * (ext + d);
                    float nat = Natural(st.X + ux * off, st.Z + uz * off);
                    float dc = Math.Max(0f, d - verge);
                    float sf = Math.Max(0f, d - SteepenStartM), sc = Math.Max(0f, dc - SteepenStartM);
                    // The wall as Bounds raises it: a breast wall from its verge out, a fill wall from the drawn edge.
                    float lift = WallLift(wall, d);
                    float lo = bench - tolF - kf * d - st.S * SteepenRate * sf * sf + Math.Min(0f, lift);
                    float hi = bench + tolC + kc * dc + st.S * SteepenRate * sc * sc + Math.Max(0f, lift);
                    if (nat < lo - 0.005f || nat > hi + 0.005f) last = d;
                }
                float reach = last < 0f ? 1.0f : Math.Min(MaxReachM, last + 3f);
                if (side == 0)
                {
                    st.RL = reach;
                    st.WL = wall;
                }
                else
                {
                    st.RR = reach;
                    st.WR = wall;
                }
            }
        }

        /// <summary>Walls along a run read as continuous structures: gaps of up to two stations between walls of the same
        /// kind are bridged, and walls shorter than three stations are dropped (an open slope does).</summary>
        private static void SmoothWalls(List<Station> run)
        {
            for (int side = 0; side < 2; side++)
            {
                int n = run.Count;
                int lastWall = -1;
                for (int k = 0; k < n; k++)
                {
                    float w = side == 0 ? run[k].WL : run[k].WR;
                    if (w == 0f) continue;
                    if (lastWall >= 0 && k - lastWall > 1 && k - lastWall <= 3)
                    {
                        float w0 = side == 0 ? run[lastWall].WL : run[lastWall].WR;
                        if (Math.Sign(w0) == Math.Sign(w))
                            for (int j = lastWall + 1; j < k; j++)
                            {
                                Station st = run[j];
                                float f = (j - lastWall) / (float)(k - lastWall), v = w0 + (w - w0) * f;
                                if (side == 0) st.WL = v;
                                else st.WR = v;
                                run[j] = st;
                            }
                    }
                    lastWall = k;
                }
                int start = -1;
                for (int k = 0; k <= n; k++)
                {
                    bool on = k < n && (side == 0 ? run[k].WL : run[k].WR) != 0f;
                    if (on && start < 0) start = k;
                    if (!on && start >= 0)
                    {
                        if (k - start < 3)
                            for (int j = start; j < k; j++)
                            {
                                Station st = run[j];
                                if (side == 0) st.WL = 0f;
                                else st.WR = 0f;
                                run[j] = st;
                            }
                        start = -1;
                    }
                }
            }
        }

        private void Extension(RoadGrade grade, RoadLayout lay, int i, RoadCentreline c, int k, float lift)
        {
            double s = c.S[k];
            RoadGrade.Row row = grade.RowAt(i, s);
            if (row.DeckW > 0.01f) return;
            double x = c.X[k], z = c.Z[k], tx = c.Tx[k], tz = c.Tz[k];
            if (k == 0)
            {
                tx = -tx;
                tz = -tz;
            }
            RoadWidthProfile prof = lay.Profiles[i];
            float half = 0.5f * prof.DrawnAt(s), shift = prof.ShiftAt(s);
            float left, right;
            DrawnSides(lay, i, s, false, out left, out right);
            float y0 = grade.BenchY(row, 0);
            // Going outward the travel direction is reversed at the start: left and right swap.
            bool flip = k == 0;
            float el = flip ? -(shift - half) : shift + half, er = flip ? -(shift + half) : shift - half;
            float cl = flip ? right : left, cr = flip ? left : right;
            // The end row's section (on the border line), its left reversed with the travel direction at the start.
            double wx, wz;
            RowDirection(s, out wx, out wz);
            if (flip)
            {
                wx = -wx;
                wz = -wz;
            }
            var a = new Station
            {
                X = x, Z = z, A0 = y0 - lift, A1 = 0f, EL = el, ER = er, CL = el + cl, CR = -er + cr, G = 0f, Road = i, Tx = tx, Tz = tz, WX = wx, WZ = wz,
            };
            a.S = Strength(a);
            Reach(ref a, tx, tz);
            double size = Source.Tile.Size;
            if (Math.Abs(x) < 1e-3 || Math.Abs(z) < 1e-3 || Math.Abs(x - size) < 1e-3 || Math.Abs(z - size) < 1e-3)
                _crossings.Add(new Crossing { X = x, Z = z, WX = wx, WZ = wz, CL = a.CL, CR = a.CR, Bench = a.A0 });
            double len = RoadGrade.SeamLandingM + 1.0;
            Station b = a;
            b.X = x + tx * len;
            b.Z = z + tz * len;
            int r0 = _segCount;
            AddSegment(a, b, ClipStart | ClipEnd);
            CloseRun(r0);
        }

        /// <summary>Turns a run of stations into segments (merging nearly collinear stretches with a nearly linear bench),
        /// then clears it.</summary>
        private void FlushRun(List<Station> run, bool clipStart, bool clipEnd)
        {
            if (run.Count < 2)
            {
                run.Clear();
                return;
            }
            Strengthen(run);
            SmoothWalls(run);
            int r0 = _segCount;
            int a = 0;
            while (a < run.Count - 1)
            {
                int b = a + 1;
                // Extend while the chord stays within 3 cm of the stations and the bench interpolates within 1 cm.
                while (b + 1 < run.Count && Mergeable(run, a, b + 1)) b++;
                byte f = 0;
                if (a == 0 && clipStart) f |= ClipStart;
                if (b == run.Count - 1 && clipEnd) f |= ClipEnd;
                AddSegment(run[a], run[b], f);
                a = b;
            }
            CloseRun(r0);
            run.Clear();
        }

        /// <summary>Marks segments from <paramref name="first"/> to the last added as one run.</summary>
        private void CloseRun(int first)
        {
            if (_segCount <= first) return;
            int id = _runFirst.Count;
            _runFirst.Add(first);
            _runLast.Add(_segCount - 1);
            for (int e = first; e < _segCount; e++) _run[e] = id;
        }

        private static bool Mergeable(List<Station> run, int a, int b)
        {
            Station sa = run[a], sb = run[b];
            double dx = sb.X - sa.X, dz = sb.Z - sa.Z, len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-6 || len > 24.0) return false;
            if ((sa.WL != 0f || sa.WR != 0f || sb.WL != 0f || sb.WR != 0f) && len > 6.0) return false; // walls: short pieces
            if (sa.Wall != sb.Wall) return false;
            for (int k = a + 1; k < b; k++)
            {
                Station s = run[k];
                double f = ((s.X - sa.X) * dx + (s.Z - sa.Z) * dz) / (len * len);
                if (f <= 0 || f >= 1) return false;
                double px = sa.X + dx * f - s.X, pz = sa.Z + dz * f - s.Z;
                if (px * px + pz * pz > 0.03 * 0.03) return false;
                float t = (float)f;
                if (Math.Abs(sa.A0 + (sb.A0 - sa.A0) * t - s.A0) > 0.01f) return false;
                if (Math.Abs(sa.A1 + (sb.A1 - sa.A1) * t - s.A1) > 0.002f) return false;
                if (Math.Abs(sa.EL + (sb.EL - sa.EL) * t - s.EL) > 0.05f || Math.Abs(sa.ER + (sb.ER - sa.ER) * t - s.ER) > 0.05f) return false;
                if (Math.Abs(sa.CL + (sb.CL - sa.CL) * t - s.CL) > 0.05f || Math.Abs(sa.CR + (sb.CR - sa.CR) * t - s.CR) > 0.05f) return false;
                if (Math.Abs(sa.G + (sb.G - sa.G) * t - s.G) > 0.05f) return false;
                if (Math.Abs(sa.WL + (sb.WL - sa.WL) * t - s.WL) > 0.1f || Math.Abs(sa.WR + (sb.WR - sa.WR) * t - s.WR) > 0.1f) return false;
                if (s.RL > Math.Max(sa.RL, sb.RL) || s.RR > Math.Max(sa.RR, sb.RR)) return false;
                // The ribbon's section direction blends linearly (its edges stay where the ribbon draws them).
                if (Math.Abs(sa.WX + (sb.WX - sa.WX) * f - s.WX) > 0.004 || Math.Abs(sa.WZ + (sb.WZ - sa.WZ) * f - s.WZ) > 0.004) return false;
            }
            return true;
        }

        private void AddSegment(in Station a, in Station b, byte flags)
        {
            double dx = b.X - a.X, dz = b.Z - a.Z, len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-4) return;
            int k = _segCount++;
            if (k >= _ax.Length) GrowSegments(Math.Max(256, 2 * _ax.Length));
            _ax[k] = a.X;
            _az[k] = a.Z;
            _len[k] = len;
            _dx[k] = dx / len;
            _dz[k] = dz / len;
            _a0a[k] = a.A0;
            _a0b[k] = b.A0;
            _a1a[k] = a.A1;
            _a1b[k] = b.A1;
            _ela[k] = a.EL;
            _elb[k] = b.EL;
            _era[k] = a.ER;
            _erb[k] = b.ER;
            _cla[k] = a.CL;
            _clb[k] = b.CL;
            _cra[k] = a.CR;
            _crb[k] = b.CR;
            _rl[k] = Math.Max(a.RL, b.RL);
            _rr[k] = Math.Max(a.RR, b.RR);
            _ga[k] = a.G;
            _gb[k] = b.G;
            bool wall = a.Wall || b.Wall;
            _kc[k] = wall ? WallSlope : CutSlope;
            _kf[k] = wall ? WallSlope : FillSlope;
            _verge[k] = wall ? 0.3f : Math.Min(a.S, b.S) >= 0.99f ? VergeM : StreetVergeM;
            _sa[k] = a.S;
            _sb[k] = b.S;
            _wla[k] = a.WL;
            _wlb[k] = b.WL;
            _wra[k] = a.WR;
            _wrb[k] = b.WR;
            _road[k] = a.Road;
            _run[k] = -1;
            _flags[k] = flags;
            _wxa[k] = a.WX;
            _wza[k] = a.WZ;
            _wxb[k] = b.WX;
            _wzb[k] = b.WZ;
            _wmax[k] = (float)Math.Max(1.0, Math.Max(Math.Sqrt(a.WX * a.WX + a.WZ * a.WZ), Math.Sqrt(b.WX * b.WX + b.WZ * b.WZ)));
        }

        private void GrowSegments(int cap)
        {
            Array.Resize(ref _ax, cap);
            Array.Resize(ref _az, cap);
            Array.Resize(ref _len, cap);
            Array.Resize(ref _dx, cap);
            Array.Resize(ref _dz, cap);
            Array.Resize(ref _a0a, cap);
            Array.Resize(ref _a0b, cap);
            Array.Resize(ref _a1a, cap);
            Array.Resize(ref _a1b, cap);
            Array.Resize(ref _ela, cap);
            Array.Resize(ref _elb, cap);
            Array.Resize(ref _era, cap);
            Array.Resize(ref _erb, cap);
            Array.Resize(ref _cla, cap);
            Array.Resize(ref _clb, cap);
            Array.Resize(ref _cra, cap);
            Array.Resize(ref _crb, cap);
            Array.Resize(ref _rl, cap);
            Array.Resize(ref _rr, cap);
            Array.Resize(ref _ga, cap);
            Array.Resize(ref _gb, cap);
            Array.Resize(ref _kc, cap);
            Array.Resize(ref _kf, cap);
            Array.Resize(ref _verge, cap);
            Array.Resize(ref _sa, cap);
            Array.Resize(ref _sb, cap);
            Array.Resize(ref _flags, cap);
            Array.Resize(ref _wla, cap);
            Array.Resize(ref _wlb, cap);
            Array.Resize(ref _wra, cap);
            Array.Resize(ref _wrb, cap);
            Array.Resize(ref _road, cap);
            Array.Resize(ref _run, cap);
            Array.Resize(ref _wxa, cap);
            Array.Resize(ref _wza, cap);
            Array.Resize(ref _wxb, cap);
            Array.Resize(ref _wzb, cap);
            Array.Resize(ref _wmax, cap);
        }

        /// <summary>
        /// The coarse grid beside the hill roads: every coarse vertex outside all drawn extents (and off the tile border)
        /// takes the carved height of the strong (hill road) elements there, so the cells the carve does not refine still
        /// show the cut and fill at the grid's own resolution, an earthwork smoothed over one cell rather than none. Streets
        /// on gentle ground leave the ground beside them alone.
        /// </summary>
        private void PreCarve()
        {
            int q = Grid.Quads, side = q + 1;
            double cm = Grid.CellM;
            _strongOnly = true;
            try
            {
                for (int k = 1; k < q; k++)
                for (int l = 1; l < q; l++)
                {
                    double lx = _offX + l * cm, lz = _offZ + k * cm;
                    float b;
                    double gx, gz;
                    if (ExtentAt(lx, lz, out b, out gx, out gz)) continue;
                    int idx = k * side + l;
                    float v = CarvedAt(lx, lz, _natural[idx]);
                    if (Math.Abs(v - _natural[idx]) < 0.02f) continue;
                    _coarse[idx] = v;
                    _moved[idx] = MovedCarved;
                }
            }
            finally
            {
                _strongOnly = false;
            }
        }

        /// <summary>
        /// The ground under every road as the coarse grid draws it: wherever a coarse triangle would rise more than
        /// <see cref="StreetCutTolM"/> above the bench under a drawn extent (a street, a cap, a ring, a hill road), its
        /// corners are lowered (never on the tile border, at most <see cref="MaxLowerM"/>), so no ground shows through a
        /// road without refining the grid. Cells where that is not enough (a deeper bump of the city's surface model, a
        /// deep cutting) are marked for refinement (<see cref="_poke"/>). The ground under a road is hidden by it; what
        /// everyone stands on there is the bench itself (<see cref="ExtentAt"/>).
        /// </summary>
        private void CoarseLower()
        {
            int q = Grid.Quads, side = q + 1;
            double cm = Grid.CellM;
            // Coarse vertices under an extent: straight to the bench (plus the tolerance).
            for (int k = 1; k < q; k++)
            for (int l = 1; l < q; l++)
            {
                double lx = _offX + l * cm, lz = _offZ + k * cm;
                float b;
                double gx, gz;
                if (!ExtentAt(lx, lz, out b, out gx, out gz)) continue;
                Lower(k * side + l, _coarse[k * side + l] - (b + StreetCutTolM), l, k);
            }
            // Triangles reaching in under an extent from outside it: sampled along every extent's edges and across it.
            for (int pass = 0; pass < 5; pass++)
            {
                bool any = false;
                bool check = pass == 4;
                for (int e = 0; e < _segCount; e++)
                {
                    double len = _len[e];
                    int nu = Math.Max(1, (int)Math.Ceiling(len / LowerStepM));
                    for (int iu = 0; iu <= nu; iu++)
                    {
                        float t = (float)iu / nu;
                        double ux = _wxa[e] + (_wxb[e] - _wxa[e]) * t, uz = _wza[e] + (_wzb[e] - _wza[e]) * t;
                        float cl = _cla[e] + (_clb[e] - _cla[e]) * t, cr = _cra[e] + (_crb[e] - _cra[e]) * t;
                        float el = _ela[e] + (_elb[e] - _ela[e]) * t, er = _era[e] + (_erb[e] - _era[e]) * t;
                        float a0 = _a0a[e] + (_a0b[e] - _a0a[e]) * t, a1 = _a1a[e] + (_a1b[e] - _a1a[e]) * t;
                        double x = _ax[e] + _dx[e] * len * t, z = _az[e] + _dz[e] * len * t;
                        int na = Math.Max(1, (int)Math.Ceiling((cl + cr) / LowerStepM));
                        for (int ia = 0; ia <= na; ia++)
                        {
                            double off = -cr + (cl + cr) * ia / na;
                            double co = off > el ? el : off < er ? er : off;
                            float allowed = a0 + (float)co * a1 + StreetCutTolM;
                            if (check) Poke(x + ux * off, z + uz * off, allowed);
                            else if (LowerAt(x + ux * off, z + uz * off, allowed)) any = true;
                        }
                    }
                }
                for (int pi = 0; pi < _pads.Count; pi++)
                {
                    RoadPad p = _pads[pi];
                    for (int g = 0; g < p.PolyX.Length; g++)
                    {
                        double[] px = p.PolyX[g], pz = p.PolyZ[g];
                        int n = px.Length;
                        for (int i = 0, j = n - 1; i < n; j = i++)
                        {
                            double ex = px[i] - px[j], ez = pz[i] - pz[j];
                            int ns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(ex * ex + ez * ez) / LowerStepM));
                            for (int s = 0; s < ns; s++)
                            {
                                double x = px[j] + ex * s / ns, z = pz[j] + ez * s / ns;
                                float allowed = p.Height(x, z) + StreetCutTolM;
                                if (check) Poke(x, z, allowed);
                                else if (LowerAt(x, z, allowed)) any = true;
                            }
                        }
                    }
                    for (double z = p.MinZ; z <= p.MaxZ + 1e-6; z += LowerStepM)
                    for (double x = p.MinX; x <= p.MaxX + 1e-6; x += LowerStepM)
                    {
                        if (p.Distance(x, z) > 0) continue;
                        float allowed = p.Height(x, z) + StreetCutTolM;
                        if (check) Poke(x, z, allowed);
                        else if (LowerAt(x, z, allowed)) any = true;
                    }
                }
                if (!any && !check) pass = 3; // settled: go straight to the check pass
            }
            int moved = 0;
            foreach (byte b in _moved)
                if (b != 0) moved++;
            LoweredVertices = moved;
            int pokes = 0;
            foreach (bool b in _poke)
                if (b) pokes++;
            PokedCells = pokes;
        }

        /// <summary>Spacing of the samples the coarse lowering checks along and across every extent.</summary>
        private const double LowerStepM = 1.25;

        /// <summary>Lowers the coarse triangle under tile-local (x, z) so its surface there is at most
        /// <paramref name="allowed"/>; true when a vertex moved.</summary>
        private bool LowerAt(double x, double z, float allowed)
        {
            int q = Grid.Quads, side = q + 1;
            double u = (x - _offX) / Grid.CellM, v = (z - _offZ) / Grid.CellM;
            if (u < 0 || v < 0 || u > q || v > q) return false;
            int i = Math.Min(q - 1, (int)Math.Floor(u)), j = Math.Min(q - 1, (int)Math.Floor(v));
            double fu = u - i, fv = v - j;
            int v00 = j * side + i, v10 = v00 + 1, v01 = v00 + side, v11 = v01 + 1;
            int vb = fu >= fv ? v10 : v01;
            double h = fu >= fv ? _coarse[v00] + fu * (_coarse[v10] - _coarse[v00]) + fv * (_coarse[v11] - _coarse[v10])
                : _coarse[v00] + fu * (_coarse[v11] - _coarse[v01]) + fv * (_coarse[v01] - _coarse[v00]);
            if (h <= allowed + 0.005) return false;
            float excess = (float)(h - allowed) + 0.01f;
            bool moved = false;
            moved |= Lower(v00, excess, i, j);
            moved |= Lower(vb, excess, fu >= fv ? i + 1 : i, fu >= fv ? j : j + 1);
            moved |= Lower(v11, excess, i + 1, j + 1);
            return moved;
        }

        /// <summary>Marks the cell under tile-local (x, z) for refinement when the coarse surface there still rises above
        /// <paramref name="allowed"/> (the lowering hit <see cref="MaxLowerM"/> or the tile border).</summary>
        private void Poke(double x, double z, float allowed)
        {
            int q = Grid.Quads;
            double u = (x - _offX) / Grid.CellM, v = (z - _offZ) / Grid.CellM;
            if (u < 0 || v < 0 || u > q || v > q) return;
            if (GridSurface(_coarse, u, v) <= allowed + PokeTolM) return;
            int i = Math.Min(q - 1, (int)Math.Floor(u)), j = Math.Min(q - 1, (int)Math.Floor(v));
            _poke[j * q + i] = true;
            // The cells around every corner of the offending triangle that stands above the road: refined together, the
            // corner itself takes the carved height (a refined cell's corner shared with an unrefined one keeps it).
            int side = q + 1;
            double fu = u - i, fv = v - j;
            int bi = fu >= fv ? i + 1 : i, bj = fu >= fv ? j : j + 1;
            PokeAround(i, j, allowed, side);
            PokeAround(bi, bj, allowed, side);
            PokeAround(i + 1, j + 1, allowed, side);
        }

        private void PokeAround(int l, int k, float allowed, int side)
        {
            int q = Grid.Quads;
            if (_coarse[k * side + l] <= allowed + PokeTolM) return;
            for (int jj = k - 1; jj <= k; jj++)
            for (int ii = l - 1; ii <= l; ii++)
                if (ii >= 0 && jj >= 0 && ii < q && jj < q) _poke[jj * q + ii] = true;
        }

        /// <summary>How far the coarse ground under a road may still rise above the bench plus <see cref="StreetCutTolM"/>
        /// before its cell is refined (the road surface stands its lift, 25 cm or more, above the bench).</summary>
        private const float PokeTolM = 0.03f;

        private bool Lower(int idx, float by, int l, int k)
        {
            int q = Grid.Quads;
            if (by <= 0f) return false;
            if (l <= 0 || k <= 0 || l >= q || k >= q) return false; // the tile border stays (the neighbour does not know)
            float floor = _natural[idx] - TuneMaxLower;
            float v = Math.Max(floor, _coarse[idx] - by);
            if (v >= _coarse[idx]) return false;
            _coarse[idx] = v;
            if (_moved[idx] == 0) _moved[idx] = MovedLowered;
            return true;
        }

        private const byte MovedLowered = 1, MovedCarved = 2;

        /// <summary>Height of coarse grid vertex (row k, column l) as drawn: the natural one, carved beside hill roads and
        /// lowered under roads.</summary>
        public float CoarseVertex(int k, int l)
        {
            return _coarse[k * (Grid.Quads + 1) + l];
        }

        /// <summary>True when coarse vertex (row k, column l) was moved by the carve (lowered under a road or carved beside a
        /// hill road).</summary>
        public bool IsMoved(int k, int l)
        {
            return _moved[k * (Grid.Quads + 1) + l] != 0;
        }

        /// <summary>True when coarse vertex (row k, column l) was carved beside a hill road (visible earthwork: it takes the
        /// cut or fill colour), not only lowered under a road (hidden by it).</summary>
        public bool IsCarvedVertex(int k, int l)
        {
            return _moved[k * (Grid.Quads + 1) + l] == MovedCarved;
        }

        /// <summary>The natural height of coarse grid vertex (row k, column l).</summary>
        public float NaturalVertex(int k, int l)
        {
            return _natural[k * (Grid.Quads + 1) + l];
        }

        /// <summary>The natural surface of this area's grid at area-local fractional grid coordinates (u, v), clamped.</summary>
        public float NaturalSurface(double u, double v)
        {
            return (float)GridSurface(_natural, u, v);
        }

        /// <summary>The coarse surface as drawn (natural, carved beside hill roads, lowered under roads) at area-local
        /// fractional grid coordinates (u, v), clamped to the grid, with its gradient per quad, split as
        /// <see cref="TerrainGrid.Surface"/>.</summary>
        public double CoarseSurface(double u, double v, out double dhdu, out double dhdv)
        {
            return GridSurface(_coarse, u, v, out dhdu, out dhdv);
        }

        /// <summary>True when element <paramref name="e"/> cuts and fills a hillside (gentle streets only hold the bench
        /// under their extent and a verge beside it).</summary>
        private bool Strong(int e)
        {
            return e < _segCount ? _sa[e] > 0f || _sb[e] > 0f : _padS[e - _segCount] > 0f;
        }

        /// <summary>Buckets every element into the 8 m element grids: by its box grown by its extent and reach (or verge),
        /// and by its drawn extent alone.</summary>
        private void IndexElements()
        {
            double size = Source.Tile.Size;
            _ecells = Math.Max(1, (int)Math.Ceiling(size / ElemCellM));
            Index(false, out _estart, out _eitems);
            Index(true, out _xstart, out _xitems);
        }

        private void Index(bool extentOnly, out int[] start, out int[] items)
        {
            double size = Source.Tile.Size;
            int nc = _ecells * _ecells;
            var count = new int[nc];
            int total = _segCount + _pads.Count;
            start = new int[nc + 1];
            items = new int[0];
            for (int pass = 0; pass < 2; pass++)
            {
                int[] fill = null;
                if (pass == 1)
                {
                    for (int k = 0; k < nc; k++) start[k + 1] = start[k] + count[k];
                    items = new int[start[nc]];
                    fill = new int[nc];
                    Array.Copy(start, fill, nc);
                }
                for (int e = 0; e < total; e++)
                {
                    double x0, z0, x1, z1;
                    if (extentOnly) ExtentBox(e, out x0, out z0, out x1, out z1);
                    else Box(e, out x0, out z0, out x1, out z1);
                    if (x1 < 0 || z1 < 0 || x0 > size || z0 > size) continue;
                    int i0 = ECell(x0), i1 = ECell(x1), j0 = ECell(z0), j1 = ECell(z1);
                    for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                    {
                        int cell = j * _ecells + i;
                        if (pass == 0) count[cell]++;
                        else items[fill[cell]++] = e;
                    }
                }
            }
        }

        private void Box(int e, out double x0, out double z0, out double x1, out double z1)
        {
            if (e < _segCount)
            {
                double r = Math.Max(Math.Max(_cla[e], _clb[e]) * _wmax[e] + _rl[e], Math.Max(_cra[e], _crb[e]) * _wmax[e] + _rr[e]) + 0.5;
                double bx = _ax[e] + _dx[e] * _len[e], bz = _az[e] + _dz[e] * _len[e];
                x0 = Math.Min(_ax[e], bx) - r;
                x1 = Math.Max(_ax[e], bx) + r;
                z0 = Math.Min(_az[e], bz) - r;
                z1 = Math.Max(_az[e], bz) + r;
                return;
            }
            RoadPad p = _pads[e - _segCount];
            double m = (Strong(e) ? MaxReachM : StreetVergeM + 0.5) + 0.5;
            x0 = p.MinX - m;
            x1 = p.MaxX + m;
            z0 = p.MinZ - m;
            z1 = p.MaxZ + m;
        }

        /// <summary>The box of element <paramref name="e"/>'s drawn extent (tile-local, 0.25 m margin).</summary>
        private void ExtentBox(int e, out double x0, out double z0, out double x1, out double z1)
        {
            if (e < _segCount)
            {
                double r = Math.Max(Math.Max(_cla[e], _clb[e]), Math.Max(_cra[e], _crb[e])) * _wmax[e] + 0.25;
                double bx = _ax[e] + _dx[e] * _len[e], bz = _az[e] + _dz[e] * _len[e];
                x0 = Math.Min(_ax[e], bx) - r;
                x1 = Math.Max(_ax[e], bx) + r;
                z0 = Math.Min(_az[e], bz) - r;
                z1 = Math.Max(_az[e], bz) + r;
                return;
            }
            RoadPad p = _pads[e - _segCount];
            x0 = p.MinX - 0.25;
            x1 = p.MaxX + 0.25;
            z0 = p.MinZ - 0.25;
            z1 = p.MaxZ + 0.25;
        }

        private int ECell(double v)
        {
            int c = (int)Math.Floor(v / ElemCellM);
            return c < 0 ? 0 : c >= _ecells ? _ecells - 1 : c;
        }

        // ------------------------------------------------------------------------------------------------------------
        // The surface under the roads
        // ------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// The bench (the road surface less its lift) under tile-local (<paramref name="lx"/>, <paramref name="lz"/>) when the
        /// point lies on a road's drawn extent (a carriageway with its footpaths or shoulders between its cuts, a junction cap,
        /// a roundabout and its entries), with its gradient per metre; false elsewhere. Where extents overlap, a junction
        /// platform wins, then the road the point lies deepest inside (farthest in from its carriageway edge). This is what
        /// the samplers answer there for every road, gentle street or hill road, whatever the grid draws under it (the road
        /// hides it), so the ground query, the lane graph and every placer stand exactly on the road surface.
        /// </summary>
        public bool ExtentAt(double lx, double lz, out float bench, out double gx, out double gz)
        {
            bench = 0f;
            gx = gz = 0;
            if (_xstart == null) return false;
            double lim = _ecells * ElemCellM;
            if (lx < -0.5 || lz < -0.5 || lx > lim + 0.5 || lz > lim + 0.5) return false;
            int cell = ECell(lz) * _ecells + ECell(lx);
            float bestPd = float.PositiveInfinity;
            bool found = false;
            int end = _xstart[cell + 1];
            for (int q = _xstart[cell]; q < end; q++)
            {
                int e = _xitems[q];
                if (e >= _segCount)
                {
                    RoadPad p = _pads[e - _segCount];
                    if (lx < p.MinX - 1e-6 || lx > p.MaxX + 1e-6 || lz < p.MinZ - 1e-6 || lz > p.MaxZ + 1e-6) continue;
                    if (p.Distance(lx, lz) > 1e-6) continue;
                    bench = p.Height(lx, lz);
                    gx = p.Gx;
                    gz = p.Gz;
                    return true;
                }
                double len = _len[e], tp, off, wl;
                Param(e, lx, lz, out tp, out off, out wl);
                byte f = _flags[e];
                float t;
                bool radial = false;
                if (tp < 0 || tp > 1)
                {
                    // Beyond a free end the extent is rounded (a clipped end stops on its section, as the ribbon does).
                    bool start = tp < 0;
                    if ((f & (start ? ClipStart : ClipEnd)) != 0) continue;
                    t = start ? 0f : 1f;
                    double ex = lx - (start ? _ax[e] : _ax[e] + _dx[e] * len), ez = lz - (start ? _az[e] : _az[e] + _dz[e] * len);
                    double we = start ? Math.Sqrt(_wxa[e] * _wxa[e] + _wza[e] * _wza[e]) : Math.Sqrt(_wxb[e] * _wxb[e] + _wzb[e] * _wzb[e]);
                    off = Math.Sqrt(ex * ex + ez * ez) / Math.Max(1e-6, we) * (off < 0 ? -1 : 1);
                    wl = we;
                    radial = true;
                }
                else t = (float)tp;
                bool left = off >= 0;
                float ext = left ? _cla[e] + (_clb[e] - _cla[e]) * t : _cra[e] + (_crb[e] - _cra[e]) * t;
                float a = (float)Math.Abs(off);
                if (a > ext + 1e-4f) continue;
                float el = _ela[e] + (_elb[e] - _ela[e]) * t, er = _era[e] + (_erb[e] - _era[e]) * t;
                float pd = a - (left ? el : -er);
                if (pd >= bestPd) continue;
                bestPd = pd;
                found = true;
                float a0 = _a0a[e] + (_a0b[e] - _a0a[e]) * t, a1 = _a1a[e] + (_a1b[e] - _a1a[e]) * t;
                bool inside = off < el && off > er;
                double co = off > el ? el : off < er ? er : off;
                bench = a0 + (float)co * a1;
                double along = radial ? 0.0 : (_a0b[e] - _a0a[e] + co * (_a1b[e] - _a1a[e])) / len;
                double lateral = inside ? a1 / Math.Max(1e-6, wl) : 0.0;
                // Left of travel is the section direction there.
                double sx = (_wxa[e] + (_wxb[e] - _wxa[e]) * t) / Math.Max(1e-6, wl), sz = (_wza[e] + (_wzb[e] - _wza[e]) * t) / Math.Max(1e-6, wl);
                gx = _dx[e] * along + sx * lateral;
                gz = _dz[e] * along + sz * lateral;
            }
            return found;
        }

        /// <summary>True when area-local fractional grid point (u, v) lies on a drawn road extent or in a refined cell (the
        /// surface there is the carve's, with its facet normal).</summary>
        public bool Carves(double u, double v)
        {
            int q = Grid.Quads;
            int i = Math.Max(0, Math.Min(q - 1, (int)Math.Floor(u))), j = Math.Max(0, Math.Min(q - 1, (int)Math.Floor(v)));
            if (_block[j * q + i] >= 0) return true;
            float b;
            double gx, gz;
            return ExtentAt(_offX + u * Grid.CellM, _offZ + v * Grid.CellM, out b, out gx, out gz);
        }

        // ------------------------------------------------------------------------------------------------------------
        // Evaluation
        // ------------------------------------------------------------------------------------------------------------

        /// <summary>One element's say at a point: its cut (hi) and fill (lo) bounds and its priority distance (beyond its
        /// drawn edge; negative on its carriageway).</summary>
        private struct Say
        {
            public float Lo, Hi, Pd;

            /// <summary>Distance beyond the element's drawn extent (footpaths, shoulders, cap outline); 0 or less on it.</summary>
            public float Ext;
        }

        private sealed class EvalScratch
        {
            public Say[] Says = new Say[32];
        }

        [ThreadStatic] private static EvalScratch _eval;

        /// <summary>Squared distance from tile-local (lx, lz) to segment <paramref name="e"/> (infinite where the point
        /// projects beyond a clipped end).</summary>
        private double SegDist2(int e, double lx, double lz)
        {
            double t, off, wl;
            Param(e, lx, lz, out t, out off, out wl);
            if (t < 0)
            {
                if ((_flags[e] & ClipStart) != 0) return double.PositiveInfinity;
                double px = lx - _ax[e], pz = lz - _az[e];
                return px * px + pz * pz;
            }
            if (t > 1)
            {
                if ((_flags[e] & ClipEnd) != 0) return double.PositiveInfinity;
                double px = lx - _ax[e] - _dx[e] * _len[e], pz = lz - _az[e] - _dz[e] * _len[e];
                return px * px + pz * pz;
            }
            double d = off * wl;
            return d * d;
        }

        /// <summary>
        /// Where tile-local (<paramref name="px"/>, <paramref name="pz"/>) lies on segment <paramref name="e"/> as the ribbon
        /// draws it: the centre runs straight from the segment's start to its end and the section direction blends linearly
        /// from one end's to the other's (<see cref="Station.WX"/>), so the point is C(t) + off · W(t) for the fraction
        /// <paramref name="t"/> along (outside [0, 1] beyond the ends) and the offset <paramref name="off"/> (left positive,
        /// in the stations' offset units); <paramref name="wl"/> is the length of W(t) (metres per offset unit).
        /// </summary>
        private void Param(int e, double px, double pz, out double t, out double off, out double wl)
        {
            double len = _len[e], dx = _dx[e] * len, dz = _dz[e] * len;
            double wax = _wxa[e], waz = _wza[e], ex = _wxb[e] - wax, ez = _wzb[e] - waz;
            double qx = px - _ax[e], qz = pz - _az[e];
            // (Q - t D) x (Wa + t E) = 0: the point lies on the section through C(t).
            double c2 = -(dx * ez - dz * ex);
            double c1 = qx * ez - qz * ex - (dx * waz - dz * wax);
            double c0 = qx * waz - qz * wax;
            double lin = len > 1e-9 ? (qx * dx + qz * dz) / (len * len) : 0;
            if (Math.Abs(c2) <= 1e-9 * (Math.Abs(c1) + 1e-12)) t = Math.Abs(c1) > 1e-12 ? -c0 / c1 : lin;
            else
            {
                double disc = c1 * c1 - 4 * c2 * c0;
                if (disc < 0) t = lin;
                else
                {
                    double sq = Math.Sqrt(disc), q = -0.5 * (c1 + (c1 >= 0 ? sq : -sq));
                    double t1 = q / c2, t2 = Math.Abs(q) > 1e-18 ? c0 / q : t1;
                    double d1 = t1 < 0 ? -t1 : t1 > 1 ? t1 - 1 : 0, d2 = t2 < 0 ? -t2 : t2 > 1 ? t2 - 1 : 0;
                    t = d1 < d2 || d1 == d2 && Math.Abs(t1 - 0.5) <= Math.Abs(t2 - 0.5) ? t1 : t2;
                }
            }
            double wx = wax + ex * t, wz = waz + ez * t, w2 = wx * wx + wz * wz;
            wl = Math.Sqrt(w2);
            double rx = qx - dx * t, rz = qz - dz * t;
            off = w2 > 1e-12 ? (rx * wx + rz * wz) / w2 : 0;
        }

        /// <summary>
        /// The carved height at tile-local (<paramref name="lx"/>, <paramref name="lz"/>) over the natural height
        /// <paramref name="natural"/> there: the natural ground clamped into every road's cut and fill bounds (each road
        /// through the segment of its run nearest the point; nearer roads win where bounds contradict, blended across the gap).
        /// Returns <paramref name="natural"/> where no road reaches.
        /// </summary>
        public float CarvedAt(double lx, double lz, float natural)
        {
            float ext;
            return CarvedAt(lx, lz, natural, out ext);
        }

        /// <summary>As <see cref="CarvedAt(double, double, float)"/>, with the distance beyond the nearest drawn extent
        /// (<paramref name="extM"/>: 0 or less under a road or cap, infinite where no road reaches).</summary>
        private float CarvedAt(double lx, double lz, float natural, out float extM)
        {
            extM = float.PositiveInfinity;
            double lim = _ecells * ElemCellM - 1e-6;
            int cell = ECell(Math.Max(0, Math.Min(lim, lz))) * _ecells + ECell(Math.Max(0, Math.Min(lim, lx)));
            EvalScratch sc = _eval ?? (_eval = new EvalScratch());
            int n = 0;
            double size = Source.Tile.Size;
            double bd = Math.Min(Math.Min(lx, lz), Math.Min(size - lx, size - lz));
            float border = bd >= BorderFadeM ? 0f : (float)(BorderFadeK * (BorderFadeM - Math.Max(0, bd)) * (BorderFadeM - Math.Max(0, bd)));
            int end = _estart[cell + 1];
            for (int q = _estart[cell]; q < end; q++)
            {
                int e = _eitems[q];
                int best = e;
                if (_strongOnly && !Strong(e)) continue;
                if (e < _segCount)
                {
                    // Every listed segment of this run (they are contiguous in the list): the nearest, then walk along the
                    // run to a nearer unlisted neighbour.
                    int run = _run[e];
                    double bestD = SegDist2(e, lx, lz);
                    while (q + 1 < end && _eitems[q + 1] < _segCount && _run[_eitems[q + 1]] == run)
                    {
                        q++;
                        double d = SegDist2(_eitems[q], lx, lz);
                        if (d < bestD)
                        {
                            bestD = d;
                            best = _eitems[q];
                        }
                    }
                    int first = _runFirst[run], last = _runLast[run];
                    while (best > first)
                    {
                        double d = SegDist2(best - 1, lx, lz);
                        if (!(d < bestD)) break;
                        bestD = d;
                        best--;
                    }
                    while (best < last)
                    {
                        double d = SegDist2(best + 1, lx, lz);
                        if (!(d < bestD)) break;
                        bestD = d;
                        best++;
                    }
                    if (double.IsInfinity(bestD)) continue;
                }
                Say say;
                if (!Bounds(best, lx, lz, border, out say)) continue;
                if (say.Ext < extM) extM = say.Ext;
                if (n == sc.Says.Length) Array.Resize(ref sc.Says, 2 * n);
                // Insertion by priority (nearest drawn edge first).
                int at = n++;
                while (at > 0 && sc.Says[at - 1].Pd > say.Pd)
                {
                    sc.Says[at] = sc.Says[at - 1];
                    at--;
                }
                sc.Says[at] = say;
            }
            if (n == 0) return natural;
            float lo = float.NegativeInfinity, hi = float.PositiveInfinity, pdLo = 0f, pdHi = 0f;
            bool conflict = false;
            float target = 0f, pdConflict = 0f, pdBlock = 0f;
            for (int k = 0; k < n; k++)
            {
                Say s = sc.Says[k];
                if (s.Lo > hi || s.Hi < lo)
                {
                    if (!conflict)
                    {
                        conflict = true;
                        target = s.Lo > hi ? s.Lo : s.Hi;
                        pdConflict = s.Pd;
                        pdBlock = s.Lo > hi ? pdHi : pdLo;
                    }
                    continue;
                }
                if (s.Lo > lo)
                {
                    lo = s.Lo;
                    pdLo = s.Pd;
                }
                if (s.Hi < hi)
                {
                    hi = s.Hi;
                    pdHi = s.Pd;
                }
            }
            float v = natural < lo ? lo : natural > hi ? hi : natural;
            if (conflict)
            {
                // Between two roads at different heights: blend toward the other by how far this point is from the road
                // that holds it (none on that road's carriageway), so both sides meet halfway across the gap.
                float a = Math.Max(0f, pdBlock), b = Math.Max(0f, pdConflict);
                float t = a + b > 1e-4f ? a / (a + b) : 0f;
                v += (target - v) * t;
            }
            return v;
        }

        /// <summary>Diagnostics: every element's bounds at tile-local (lx, lz).</summary>
        internal string Explain(double lx, double lz)
        {
            var sb = new System.Text.StringBuilder();
            double lim = _ecells * ElemCellM - 1e-6;
            int cell = ECell(Math.Max(0, Math.Min(lim, lz))) * _ecells + ECell(Math.Max(0, Math.Min(lim, lx)));
            for (int q = _estart[cell]; q < _estart[cell + 1]; q++)
            {
                int e = _eitems[q];
                Say say;
                if (!Bounds(e, lx, lz, 0f, out say)) continue;
                sb.Append(e < _segCount ? "seg " + e + " run " + _run[e] + " road " + _road[e] + " d " + Math.Sqrt(SegDist2(e, lx, lz)).ToString("0.0") : "pad " + (e - _segCount))
                  .Append(" lo ").Append(say.Lo.ToString("0.00")).Append(" hi ").Append(say.Hi.ToString("0.00")).Append(" pd ").Append(say.Pd.ToString("0.00"))
                  .Append(" ext ").Append(say.Ext.ToString("0.00"));
                if (e < _segCount)
                    sb.Append(" S ").Append(_sa[e].ToString("0.00")).Append('/').Append(_sb[e].ToString("0.00")).Append(" verge ").Append(_verge[e].ToString("0.00"))
                      .Append(" kc ").Append(_kc[e].ToString("0.0")).Append(" cl ").Append(_cla[e].ToString("0.0")).Append(" el ").Append(_ela[e].ToString("0.0"))
                      .Append(" wl ").Append(_wla[e].ToString("0.0")).Append('/').Append(_wlb[e].ToString("0.0")).Append(" wr ").Append(_wra[e].ToString("0.0"))
                      .Append('/').Append(_wrb[e].ToString("0.0"));
                sb.Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>How far a retaining wall of height <paramref name="wall"/> (positive a breast wall, negative a fill wall)
        /// shifts the bounds at <paramref name="dF"/> metres beyond the drawn extent: a breast wall rises over
        /// <see cref="WallFaceM"/> from <see cref="WallVergeM"/> out, a fill wall drops over it from the drawn edge.</summary>
        private float WallLift(float wall, float dF)
        {
            if (wall > 0f)
            {
                float dw = dF - WallVergeM;
                return dw <= 0f ? 0f : wall * (dw >= WallFaceM ? 1f : dw / WallFaceM);
            }
            if (wall < 0f) return wall * (dF >= WallFaceM ? 1f : dF / WallFaceM);
            return 0f;
        }

        /// <summary>Cut and fill bounds of element <paramref name="e"/> at (lx, lz); false when it does not reach.</summary>
        private bool Bounds(int e, double lx, double lz, float border, out Say say)
        {
            say = default(Say);
            float bench, kc, kf, g, reach, wallLift = 0f, dF, dC, str;
            if (e < _segCount)
            {
                double len = _len[e], tp, off, wl;
                Param(e, lx, lz, out tp, out off, out wl);
                byte f = _flags[e];
                float t;
                if (tp < 0 || tp > 1)
                {
                    bool start = tp < 0;
                    if ((f & (start ? ClipStart : ClipEnd)) != 0) return false;
                    t = start ? 0f : 1f;
                    double ex = lx - (start ? _ax[e] : _ax[e] + _dx[e] * len), ez = lz - (start ? _az[e] : _az[e] + _dz[e] * len);
                    double we = start ? Math.Sqrt(_wxa[e] * _wxa[e] + _wza[e] * _wza[e]) : Math.Sqrt(_wxb[e] * _wxb[e] + _wzb[e] * _wzb[e]);
                    off = Math.Sqrt(ex * ex + ez * ez) / Math.Max(1e-6, we) * (off < 0 ? -1 : 1);
                    wl = we;
                }
                else t = (float)tp;
                bool left = off >= 0;
                float ext = left ? _cla[e] + (_clb[e] - _cla[e]) * t : _cra[e] + (_crb[e] - _cra[e]) * t;
                float el = _ela[e] + (_elb[e] - _ela[e]) * t, er = _era[e] + (_erb[e] - _era[e]) * t;
                float a = (float)Math.Abs(off), sw = (float)wl;
                // Distances in metres from the drawn extent's edge (the offsets are in section units).
                float beyond = (a - ext) * sw;
                dF = beyond > 0f ? beyond : 0f;
                say.Ext = beyond;
                reach = left ? _rl[e] : _rr[e];
                if (dF > reach) return false;
                str = _sa[e] + (_sb[e] - _sa[e]) * t;
                float verge = _verge[e];
                dC = beyond > verge ? beyond - verge : 0f;
                say.Pd = (a - (left ? el : -er)) * sw;
                double co = off > el ? el : off < er ? er : off;
                bench = _a0a[e] + (_a0b[e] - _a0a[e]) * t + (float)co * (_a1a[e] + (_a1b[e] - _a1a[e]) * t);
                kc = _kc[e];
                kf = _kf[e];
                g = _ga[e] + (_gb[e] - _ga[e]) * t;
                float wall = left ? _wla[e] + (_wlb[e] - _wla[e]) * t : _wra[e] + (_wrb[e] - _wra[e]) * t;
                wallLift = WallLift(wall, dF);
            }
            else
            {
                RoadPad p = _pads[e - _segCount];
                if (lx < p.MinX - MaxReachM || lx > p.MaxX + MaxReachM || lz < p.MinZ - MaxReachM || lz > p.MaxZ + MaxReachM) return false;
                str = _padS[e - _segCount];
                double d = p.Distance(lx, lz);
                if (d > (str > 0f ? MaxReachM : StreetVergeM + 0.5)) return false;
                dF = (float)d;
                say.Ext = dF;
                float pv = str >= 0.99f ? VergeM : StreetVergeM;
                dC = dF > pv ? dF - pv : 0f;
                say.Pd = dF;
                bench = p.Height(lx, lz);
                kc = CutSlope;
                kf = FillSlope;
                g = 1f;
            }
            if (dF <= 0f)
            {
                // Under the drawn extent (carriageway with its footpaths or shoulders, cap, ring): exactly the bench, for
                // every road, so the ground everyone stands on is the road surface less its lift.
                say.Lo = say.Hi = bench;
                return true;
            }
            float weak = 1f - str;
            kc += weak * WeakSlope;
            kf += weak * WeakSlope;
            float sf = dF > SteepenStartM ? dF - SteepenStartM : 0f, scut = dC > SteepenStartM ? dC - SteepenStartM : 0f;
            float w = border > 0f && g > 0f ? border * g * Math.Min(1f, dF * 0.5f) : 0f;
            say.Lo = bench - weak * StreetFillTolM - kf * dF - str * SteepenRate * sf * sf - w + Math.Min(0f, wallLift);
            say.Hi = bench + weak * StreetCutTolM + kc * dC + str * SteepenRate * scut * scut + w + Math.Max(0f, wallLift);
            return true;
        }

        // ------------------------------------------------------------------------------------------------------------
        // Rasterise onto the refined grid
        // ------------------------------------------------------------------------------------------------------------

        private sealed class Scratch
        {
            public float[] Val = new float[0], Coarse = new float[0], Judge = new float[0];
            public bool[] OnRoad = new bool[0];
            public int[] Stamp = new int[0];
            public int Mark;

            public void Ensure(int n)
            {
                if (Val.Length >= n) return;
                Val = new float[n];
                Coarse = new float[n];
                Judge = new float[n];
                OnRoad = new bool[n];
                Stamp = new int[n];
                Mark = 0;
            }
        }

        [ThreadStatic] private static Scratch _scratch;

        /// <summary>
        /// Refines the cells where the coarse grid cannot draw the carve: around the hill roads wherever the carved ground
        /// beside a road (its verge, cut slope, embankment or wall) departs from the coarse surface by more than
        /// <see cref="RefineMoveM"/>, and wherever the coarse lowering could not bring the ground under a road down to its
        /// bench (<see cref="_poke"/>). A sub-vertex takes the carved height (the bench under a road's extent, the cut and
        /// fill beside it) only when every cell around it is refined, else the coarse surface, so a refined cell meets its
        /// unrefined neighbours on their straight edges without cracks.
        /// </summary>
        /// <summary>The border profile moves the ground at least this much before its cells are refined.</summary>
        private const float BorderMoveM = 0.005f;

        /// <summary>
        /// The drawn ground on the tile border at tile-local (lx, lz) over the natural ground <paramref name="natural"/>
        /// there: what both tiles of a seam draw alike, from what both know identically. That is the natural border line,
        /// cut down to every crossing road's seam landing (level at the bench, both tiles pin it to the same height) under
        /// its drawn extent and its verge, and at the cut slope beyond (<see cref="CutSlope"/>, measured square to the
        /// road: the seam row lies on the border line). The fill below a crossing stays natural on the border (the road's
        /// skirt closes it), as does every other road's cut and fill (relaxed over <see cref="BorderFadeM"/>).
        /// </summary>
        private float BorderCarved(double lx, double lz, float natural)
        {
            float h = natural;
            double size = Source.Tile.Size;
            // A tile corner is shared by four tiles, each knowing only the crossings of its own two borders: natural.
            if (IsCorner(lx, lz)) return h;
            float verge = _refine ? StreetVergeM : UnrefinedVergeM;
            for (int k = 0; k < _crossings.Count; k++)
            {
                Crossing c = _crossings[k];
                // Only a crossing on the same border line as the point.
                bool same = Math.Abs(c.X - lx) < 1e-3 && (Math.Abs(lx) < 1e-3 || Math.Abs(lx - size) < 1e-3) ||
                            Math.Abs(c.Z - lz) < 1e-3 && (Math.Abs(lz) < 1e-3 || Math.Abs(lz - size) < 1e-3);
                if (!same) continue;
                double ww = c.WX * c.WX + c.WZ * c.WZ;
                if (ww < 1e-9) continue;
                double o = ((lx - c.X) * c.WX + (lz - c.Z) * c.WZ) / ww;
                double d = o >= 0 ? o - c.CL : -o - c.CR;
                if (d > MaxReachM + verge) continue;
                float cut = d <= verge ? c.Bench : c.Bench + CutSlope * (float)(d - verge);
                if (cut < h) h = cut;
            }
            return h;
        }

        private void Rasterise()
        {
            int q = Grid.Quads, r = Sub, side = q * r + 1;
            Scratch sc = _scratch ?? (_scratch = new Scratch());
            sc.Ensure(side * side);
            sc.Mark++;
            if (sc.Mark == int.MaxValue)
            {
                Array.Clear(sc.Stamp, 0, sc.Stamp.Length);
                sc.Mark = 1;
            }
            int mark = sc.Mark;
            // Candidate cells: under a hill road's reach, or poked.
            var candidate = new bool[q * q];
            double cm = Grid.CellM;
            int total = _segCount + _pads.Count;
            for (int e = 0; e < total; e++)
            {
                if (!Strong(e)) continue;
                double x0, z0, x1, z1;
                Box(e, out x0, out z0, out x1, out z1);
                int i0 = Math.Max(0, (int)Math.Floor((x0 - _offX) / cm)), i1 = Math.Min(q - 1, (int)Math.Floor((x1 - _offX) / cm));
                int j0 = Math.Max(0, (int)Math.Floor((z0 - _offZ) / cm)), j1 = Math.Min(q - 1, (int)Math.Floor((z1 - _offZ) / cm));
                for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++) candidate[j * q + i] = true;
            }
            for (int cell = 0; cell < q * q; cell++)
                if (_poke[cell]) candidate[cell] = true;
            // Border cells where a crossing road cuts the border line (BorderCarved): both tiles refine them.
            var forced = new bool[q * q];
            if (_crossings.Count > 0 && _offX == 0.0 && _offZ == 0.0)
            {
                for (int k = 0; k < q; k++)
                for (int e = 0; e < 4; e++)
                {
                    int ci = e == 0 ? 0 : e == 1 ? q - 1 : k, cj = e == 2 ? 0 : e == 3 ? q - 1 : k;
                    if (forced[cj * q + ci]) continue;
                    for (int a = 0; a <= r; a++)
                    {
                        int gi = e == 0 ? 0 : e == 1 ? q * r : ci * r + a, gj = e == 2 ? 0 : e == 3 ? q * r : cj * r + a;
                        double u = gi / (double)r, v = gj / (double)r;
                        float nat = (float)GridSurface(_natural, u, v);
                        if (BorderCarved(gi * SubM, gj * SubM, nat) < nat - BorderMoveM)
                        {
                            forced[cj * q + ci] = candidate[cj * q + ci] = true;
                            break;
                        }
                    }
                }
            }
            // Pass 1: the carved and the coarse surfaces at every sub-vertex of the candidate cells.
            var refinedFlag = new bool[q * q];
            var refined = new List<int>(256);
            for (int cell = 0; cell < q * q; cell++)
            {
                if (!candidate[cell]) continue;
                int ci = cell % q, cj = cell / q;
                bool moved = _poke[cell];
                float worst = 0f;
                for (int b = 0; b <= r; b++)
                for (int a = 0; a <= r; a++)
                {
                    int gi = ci * r + a, gj = cj * r + b, idx = gj * side + gi;
                    if (sc.Stamp[idx] != mark) Sample(sc, idx, gi, gj, r);
                    if (sc.OnRoad[idx]) continue; // hidden by the road (and answered analytically)
                    float d = Math.Abs(sc.Judge[idx] - sc.Coarse[idx]);
                    if (d > worst) worst = d;
                }
                if (worst > TuneRefineMove || forced[cell]) moved = true;
                if (!moved) continue;
                refined.Add(cell);
                refinedFlag[cell] = true;
                if (DiagOn)
                {
                    int bucket = worst < 0.5f ? 0 : worst < 1f ? 1 : worst < 2f ? 2 : 3;
                    DiagBuckets[bucket]++;
                    if (_poke[cell]) DiagCutOnly++;
                }
            }
            // Pass 2: a sub-vertex takes the carved height only when every cell around it is refined (else the coarse
            // surface, on the unrefined neighbour's straight edge).
            int per = (r + 1) * (r + 1);
            _heights = new float[refined.Count * per];
            for (int k = 0; k < refined.Count; k++)
            {
                int cell = refined[k], ci = cell % q, cj = cell / q, start = k * per;
                _block[cell] = start;
                for (int b = 0; b <= r; b++)
                for (int a = 0; a <= r; a++)
                {
                    int gi = ci * r + a, gj = cj * r + b, idx = gj * side + gi;
                    if (gi == 0 || gj == 0 || gi == side - 1 || gj == side - 1)
                    {
                        // On the tile border: the profile both tiles draw alike (the neighbour cannot see this carve).
                        _heights[start + b * (r + 1) + a] = BorderCarved(_offX + gi * SubM, _offZ + gj * SubM, (float)GridSurface(_natural, gi / (double)r, gj / (double)r));
                        continue;
                    }
                    bool all = true;
                    if (a == 0 || a == r || b == 0 || b == r)
                    {
                        int ia = a == 0 ? ci - 1 : ci, ib = a == r ? ci + 1 : ci, ja = b == 0 ? cj - 1 : cj, jb = b == r ? cj + 1 : cj;
                        for (int jj = ja; jj <= jb && all; jj++)
                        for (int ii = ia; ii <= ib && all; ii++)
                        {
                            if (ii < 0 || jj < 0 || ii >= q || jj >= q) continue;
                            if (!refinedFlag[jj * q + ii]) all = false;
                        }
                    }
                    _heights[start + b * (r + 1) + a] = all ? sc.Val[idx] : sc.Coarse[idx];
                }
            }
            RefinedCells = refined.Count;
            LowerRefined(sc, refined, side);
        }

        /// <summary>
        /// No refined sub-triangle rises through a road: where one has a corner on a road's drawn extent and another well
        /// above that road's bench (two roads at different heights within one sub-quad, as on a switchback or a lane
        /// beside a street a storey below it, or a road and the hillside above it), the extent is sampled inside the
        /// sub-quad and every corner above the bench plus <see cref="StreetCutTolM"/> there is lowered to it (a corner
        /// under the higher road is hidden by it; one on the slope between them becomes the foot of the cut). Corners
        /// shared with an unrefined cell or on the tile border stay, as the coarse grid and the neighbour need them.
        /// </summary>
        private void LowerRefined(Scratch sc, List<int> refined, int side)
        {
            int q = Grid.Quads, r = Sub, w = r + 1;
            const int Samples = 4;
            for (int pass = 0; pass < 3; pass++)
            {
                bool any = false;
                foreach (int cell in refined)
                {
                    int ci = cell % q, cj = cell / q, start = _block[cell];
                    for (int b = 0; b < r; b++)
                    for (int a = 0; a < r; a++)
                    {
                        int gi = ci * r + a, gj = cj * r + b;
                        float low = float.PositiveInfinity;
                        for (int k = 0; k < 4; k++)
                        {
                            int idx = (gj + (k >> 1)) * side + gi + (k & 1);
                            if (sc.OnRoad[idx] && sc.Val[idx] < low) low = sc.Val[idx];
                        }
                        if (float.IsPositiveInfinity(low)) continue;
                        int o = start + b * w + a;
                        float h00 = _heights[o], h10 = _heights[o + 1], h01 = _heights[o + w], h11 = _heights[o + w + 1];
                        if (Math.Max(Math.Max(h00, h10), Math.Max(h01, h11)) <= low + StreetCutTolM + 0.005f) continue;
                        for (int sv = 0; sv <= Samples; sv++)
                        for (int su = 0; su <= Samples; su++)
                        {
                            double fu = su / (double)Samples, fv = sv / (double)Samples;
                            float bench;
                            double gx, gz;
                            if (!ExtentAt(_offX + (gi + fu) * SubM, _offZ + (gj + fv) * SubM, out bench, out gx, out gz)) continue;
                            float allowed = bench + StreetCutTolM;
                            h00 = _heights[o];
                            h10 = _heights[o + 1];
                            h01 = _heights[o + w];
                            h11 = _heights[o + w + 1];
                            bool lower = fu >= fv;
                            double h = lower ? h00 + fu * (h10 - h00) + fv * (h11 - h10) : h00 + fu * (h11 - h01) + fv * (h01 - h00);
                            if (h <= allowed + 0.005) continue;
                            bool moved = LowerSub(gi, gj, allowed);
                            moved |= LowerSub(gi + 1, gj + 1, allowed);
                            moved |= lower ? LowerSub(gi + 1, gj, allowed) : LowerSub(gi, gj + 1, allowed);
                            if (moved) any = true;
                        }
                    }
                }
                if (!any) break;
            }
        }

        /// <summary>Lowers global sub-vertex (gi, gj) to at most <paramref name="to"/> in every refined cell that holds it;
        /// false when it may not move (on the tile border or shared with an unrefined cell) or is already low enough.</summary>
        private bool LowerSub(int gi, int gj, float to)
        {
            int q = Grid.Quads, r = Sub, last = q * r;
            if (gi <= 0 || gj <= 0 || gi >= last || gj >= last) return false;
            int ia = gi % r == 0 ? gi / r - 1 : gi / r, ib = gi / r, ja = gj % r == 0 ? gj / r - 1 : gj / r, jb = gj / r;
            for (int cj = ja; cj <= jb; cj++)
            for (int ci = ia; ci <= ib; ci++)
                if (_block[cj * q + ci] < 0) return false;
            bool changed = false;
            for (int cj = ja; cj <= jb; cj++)
            for (int ci = ia; ci <= ib; ci++)
            {
                int k = _block[cj * q + ci] + (gj - cj * r) * (r + 1) + gi - ci * r;
                if (_heights[k] <= to) continue;
                _heights[k] = to;
                changed = true;
            }
            return changed;
        }

        /// <summary>Fills the scratch at global sub-grid point (gi, gj): the carved height (the bench on a road's extent, the
        /// natural ground clamped into every road's cut and fill bounds elsewhere) and the coarse surface as drawn.</summary>
        private void Sample(Scratch sc, int idx, int gi, int gj, int r)
        {
            double u = gi / (double)r, v = gj / (double)r;
            double lx = _offX + gi * SubM, lz = _offZ + gj * SubM;
            sc.Coarse[idx] = (float)GridSurface(_coarse, u, v);
            float bench;
            double gx, gz;
            if (ExtentAt(lx, lz, out bench, out gx, out gz))
            {
                sc.Val[idx] = bench;
                sc.OnRoad[idx] = true;
            }
            else
            {
                float nat = (float)GridSurface(_natural, u, v);
                sc.Val[idx] = CarvedAt(lx, lz, nat);
                if (TuneStrongCriterion)
                {
                    _strongOnly = true;
                    sc.Judge[idx] = CarvedAt(lx, lz, nat);
                    _strongOnly = false;
                }
                else sc.Judge[idx] = sc.Val[idx];
                sc.OnRoad[idx] = false;
            }
            sc.Stamp[idx] = sc.Mark;
        }

        // ------------------------------------------------------------------------------------------------------------
        // Queries
        // ------------------------------------------------------------------------------------------------------------

        /// <summary>True when grid cell (i, j) (column, row from the south-west) is drawn with carved sub-quads.</summary>
        public bool IsRefined(int i, int j)
        {
            int q = Grid.Quads;
            return i >= 0 && j >= 0 && i < q && j < q && _block[j * q + i] >= 0;
        }

        /// <summary>Height of sub-vertex (a, b) (0..<see cref="Sub"/>) of refined cell (i, j).</summary>
        public float SubVertex(int i, int j, int a, int b)
        {
            int start = _block[j * Grid.Quads + i];
            return _heights[start + b * (Sub + 1) + a];
        }

        /// <summary>
        /// The carved surface everyone stands on at area-local fractional grid coordinates (u, v) in quads, clamped to the
        /// grid, with its gradient per metre: on a road's drawn extent the bench under it (<see cref="ExtentAt"/>: the road
        /// surface less its lift, whatever the grid draws hidden under the road), elsewhere exactly the drawn terrain
        /// (<see cref="DrawnSurface"/>). The samplers, the ground query, the lane graph and every placer read this.
        /// </summary>
        public double Surface(double u, double v, out double gx, out double gz)
        {
            int q = Grid.Quads;
            if (u < 0) u = 0;
            else if (u > q) u = q;
            if (v < 0) v = 0;
            else if (v > q) v = q;
            float bench;
            double lx = _offX + u * Grid.CellM, lz = _offZ + v * Grid.CellM;
            if (OnTileBorder(lx, lz))
            {
                // On the tile border only what both tiles know alike: the crossing roads' seam landings, else the border
                // profile both draw (BorderCarved), so everything standing on the seam stands alike on both sides of it.
                if (CrossingBench(lx, lz, out bench))
                {
                    gx = gz = 0.0;
                    return bench;
                }
                return DrawnSurface(u, v, out gx, out gz);
            }
            if (ExtentAt(lx, lz, out bench, out gx, out gz)) return bench;
            return DrawnSurface(u, v, out gx, out gz);
        }

        /// <summary>
        /// True when the carved surface at area-local fractional grid coordinates (u, v) is a road's bench (on a drawn
        /// extent, or on a crossing road's seam landing on the tile border): <see cref="Surface"/> answers
        /// <paramref name="bench"/> there with its gradient per metre, not the drawn terrain hidden under the road. The
        /// drapes put caps, rings and areas on it, so they lie exactly where the ground query and the ribbons are.
        /// </summary>
        public bool BenchAt(double u, double v, out float bench, out double gx, out double gz)
        {
            int q = Grid.Quads;
            if (u < 0) u = 0;
            else if (u > q) u = q;
            if (v < 0) v = 0;
            else if (v > q) v = q;
            double lx = _offX + u * Grid.CellM, lz = _offZ + v * Grid.CellM;
            if (OnTileBorder(lx, lz))
            {
                gx = gz = 0.0;
                return CrossingBench(lx, lz, out bench);
            }
            return ExtentAt(lx, lz, out bench, out gx, out gz);
        }

        /// <summary>True when tile-local (lx, lz) is a corner of the source tile.</summary>
        private bool IsCorner(double lx, double lz)
        {
            const double Eps = 1e-3;
            double size = Source.Tile.Size;
            return (Math.Abs(lx) < Eps || Math.Abs(lx - size) < Eps) && (Math.Abs(lz) < Eps || Math.Abs(lz - size) < Eps);
        }

        /// <summary>True when tile-local (lx, lz) lies on the source tile's border line.</summary>
        private bool OnTileBorder(double lx, double lz)
        {
            const double Eps = 1e-6;
            double size = Source.Tile.Size;
            return lx <= Eps || lz <= Eps || lx >= size - Eps || lz >= size - Eps;
        }

        /// <summary>The seam landing's bench of a road crossing the tile border at tile-local (lx, lz) on the border line
        /// (its drawn extent and <see cref="SideMarginM"/>, as <see cref="ExtentAt"/> reaches); false elsewhere.</summary>
        private bool CrossingBench(double lx, double lz, out float bench)
        {
            bench = 0f;
            bool found = false;
            double size = Source.Tile.Size;
            if (IsCorner(lx, lz)) return false;
            for (int k = 0; k < _crossings.Count; k++)
            {
                Crossing c = _crossings[k];
                bool same = Math.Abs(c.X - lx) < 1e-3 && (Math.Abs(lx) < 1e-3 || Math.Abs(lx - size) < 1e-3) ||
                            Math.Abs(c.Z - lz) < 1e-3 && (Math.Abs(lz) < 1e-3 || Math.Abs(lz - size) < 1e-3);
                if (!same) continue;
                double ww = c.WX * c.WX + c.WZ * c.WZ;
                if (ww < 1e-9) continue;
                double o = ((lx - c.X) * c.WX + (lz - c.Z) * c.WZ) / ww;
                if (o > c.CL + SideMarginM || -o > c.CR + SideMarginM) continue;
                // Two crossings overlapping on the border: the higher bench (the lower is under it).
                if (!found || c.Bench > bench) bench = c.Bench;
                found = true;
            }
            return found;
        }

        /// <summary>
        /// The drawn terrain at area-local fractional grid coordinates (u, v) in quads, clamped to the grid, with the facet
        /// gradient per metre: the carved sub-quads in refined cells (each split along its (a, b)-(a+1, b+1) diagonal, as
        /// the coarse cells are), the coarse grid (carved beside hill roads, lowered under roads) elsewhere.
        /// </summary>
        public double DrawnSurface(double u, double v, out double gx, out double gz)
        {
            int q = Grid.Quads;
            if (u < 0) u = 0;
            else if (u > q) u = q;
            if (v < 0) v = 0;
            else if (v > q) v = q;
            int i = (int)Math.Floor(u), j = (int)Math.Floor(v);
            if (i > q - 1) i = q - 1;
            if (j > q - 1) j = q - 1;
            int start = _block[j * q + i];
            if (start < 0)
            {
                double du, dv;
                double h = CoarseSurface(u, v, out du, out dv);
                gx = du / Grid.CellM;
                gz = dv / Grid.CellM;
                return h;
            }
            int r = Sub, w = r + 1;
            double su = (u - i) * r, sv = (v - j) * r;
            int a = (int)Math.Floor(su), b = (int)Math.Floor(sv);
            if (a > r - 1) a = r - 1;
            if (b > r - 1) b = r - 1;
            if (a < 0) a = 0;
            if (b < 0) b = 0;
            double fu = su - a, fv = sv - b;
            double h00 = _heights[start + b * w + a], h10 = _heights[start + b * w + a + 1];
            double h01 = _heights[start + (b + 1) * w + a], h11 = _heights[start + (b + 1) * w + a + 1];
            double dhu, dhv;
            if (fu >= fv)
            {
                dhu = h10 - h00;
                dhv = h11 - h10;
            }
            else
            {
                dhu = h11 - h01;
                dhv = h01 - h00;
            }
            gx = dhu / SubM;
            gz = dhv / SubM;
            return h00 + fu * dhu + fv * dhv;
        }
    }
}
