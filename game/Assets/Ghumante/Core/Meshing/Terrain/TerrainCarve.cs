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

        /// <summary>Side of the refined sub-quads on the fine terrain steps (16 m cells and finer); coarser cells use
        /// <see cref="CoarseSubCellM"/>.</summary>
        public const float SubCellM = 2f;

        /// <summary>Side of the refined sub-quads on cells coarser than 16 m (the Low tier's 32 m detail tiles).</summary>
        public const float CoarseSubCellM = 4f;

        /// <summary>A cell is refined (drawn with carved sub-quads) where the carve moves a sub-vertex under a road's drawn
        /// extent or its verge by more than this (the bench of a hill road; the cut that keeps the ground of a gentle street
        /// under its lifted surface).</summary>
        public const float RefineCutM = 0.05f;

        /// <summary>A cell is also refined where the carve moves a sub-vertex anywhere by more than this (the visible cut
        /// slopes, embankments and walls of hill roads); smaller differences beside gentle city streets are not worth the
        /// triangles, so those keep their natural ground.</summary>
        public const float RefineMoveM = 0.5f;

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
        public const float WallMinM = 1.0f;

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
        private readonly List<float> _padS = new List<float>();
        private int[] _road = new int[0], _run = new int[0];
        private byte[] _flags = new byte[0];

        /// <summary>First and last segment of each run (consecutive segments of one road between two breaks).</summary>
        private readonly List<int> _runFirst = new List<int>(), _runLast = new List<int>();

        private const byte ClipStart = 1, ClipEnd = 2;

        private readonly List<RoadPad> _pads = new List<RoadPad>();

        // Element grid (tile-local).
        private const double ElemCellM = 8.0;
        private int _ecells;
        private int[] _estart, _eitems;

        // ------------------------------------------------------------------------------------------------------------
        // Drawn surface
        // ------------------------------------------------------------------------------------------------------------

        public readonly TileData Source;
        public readonly TerrainGrid Grid;

        /// <summary>Sub-quads per grid cell side in refined cells.</summary>
        public readonly int Sub;

        /// <summary>Side of a sub-quad in metres.</summary>
        public readonly double SubM;

        /// <summary>Level verge between a road's drawn extent and its cut slope (one sub-quad diagonal plus a margin).</summary>
        public readonly float VergeM;

        private readonly int[] _block; // per grid cell (row-major from the south-west): start into _heights, or -1

        /// <summary>The coarse grid's vertex heights after the gentle streets' cut (row-major from the south-west).</summary>
        private readonly float[] _coarse;

        private readonly bool[] _lowered;
        private float[] _heights;
        private readonly double _offX, _offZ; // area origin in tile-local metres

        /// <summary>Grid cells refined (drawn with carved sub-quads).</summary>
        public int RefinedCells { get; private set; }

        /// <summary>Coarse grid vertices lowered under gentle streets (<see cref="StreetCutTolM"/>).</summary>
        public int LoweredVertices { get; private set; }

        /// <summary>The most a coarse vertex is lowered under a gentle street (deeper bumps of the surface model are left).</summary>
        public const float MaxLowerM = 1.5f;

        /// <summary>Road segments and junction platforms the carve follows.</summary>
        public int ElementCount
        {
            get { return _segCount + _pads.Count; }
        }

        /// <summary>Diagnostics (tests): record why cells are refined.</summary>
        internal static bool DiagOn;

        internal readonly int[] DiagBuckets = new int[4];
        internal int DiagCutOnly, DiagNatDiff;

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
                if (IsLowered(k, l))
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
            public readonly object Gate = new object();
        }

        private static readonly ConditionalWeakTable<TileData, List<Slot>> Cache = new ConditionalWeakTable<TileData, List<Slot>>();
        private static readonly object CacheLock = new object();

        [ThreadStatic] private static int _building;

        /// <summary>Carving on (default). Off: every sampler and terrain mesh is the natural surface (diagnostics only).</summary>
        public static bool Enabled = true;

        /// <summary>True while a carve is being built on this thread (samplers answer the natural surface meanwhile).</summary>
        public static bool Building
        {
            get { return _building > 0; }
        }

        /// <summary>
        /// The carve of <paramref name="sampler"/>'s tile, area and step, or null when nothing is carved there (no roads, an
        /// area cropped from an ancestor, no heights, or carving off). Built on first use and cached per tile; the grade it
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
                    if (s.Area == sampler.Area && s.Step == sampler.Step)
                    {
                        slot = s;
                        break;
                    }
                if (slot == null)
                {
                    slot = new Slot { Area = sampler.Area, Step = sampler.Step };
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
                    var c = new TerrainCarve(src, sampler);
                    slot.Carve = c.RefinedCells > 0 || c.LoweredVertices > 0 ? c : null;
                }
                finally
                {
                    _building--;
                }
                slot.Done = true;
                return slot.Carve;
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // Build
        // ------------------------------------------------------------------------------------------------------------

        private TerrainCarve(TileData src, TileHeightSampler sampler)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Source = src;
            Grid = sampler.Grid;
            double target = Grid.CellM > 16.0 + 1e-6 ? CoarseSubCellM : SubCellM;
            int sub = 1;
            while (Grid.CellM / (sub * 2) >= target - 1e-6 && sub < 32) sub *= 2;
            Sub = sub;
            SubM = Grid.CellM / sub;
            VergeM = (float)(SubM * Math.Sqrt(2.0) + 0.3);
            _offX = Grid.Area.X0 - src.Tile.X0;
            _offZ = Grid.Area.Z0 - src.Tile.Z0;
            int q = Grid.Quads;
            _block = new int[q * q];
            for (int k = 0; k < _block.Length; k++) _block[k] = -1;
            _heights = new float[0];
            int side = q + 1;
            _coarse = new float[side * side];
            _lowered = new bool[side * side];
            for (int k = 0; k <= q; k++)
            for (int l = 0; l <= q; l++) _coarse[k * side + l] = Grid.VertexHeight(src, k, l);
            RoadGrade grade = RoadGrade.For(src, sampler, new RoadOptions());
            CollectSegments(grade);
            foreach (RoadPad p in grade.Pads.Pads)
            {
                _pads.Add(p);
                _padS.Add(p.Hill ? 1f : 0f);
            }
            CoarseLower();
            IndexElements();
            Rasterise();
            BuildMs = sw.Elapsed.TotalMilliseconds;
        }

        /// <summary>The natural ground as this area's grid draws it (tile-local metres).</summary>
        private float Natural(double lx, double lz)
        {
            double cm = Grid.CellM, du, dv;
            return (float)CoarseSurface((lx - _offX) / cm, (lz - _offZ) / cm, out du, out dv);
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
                // Sample positions: the profile's stations and every cut (runs end exactly where the ribbon stops).
                pos.Clear();
                for (int k = 0; k < n; k++) pos.Add(c.AlongAtArc(k * step));
                RoadCut[] cs = lay.Cuts[i];
                if (cs != null)
                    foreach (RoadCut cut in cs) pos.Add(cut.S);
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
            float left, right;
            lay.CorridorSides(i, s, out left, out right);
            bool wall = grade.LoweringAt(i, s) > 0.01f;
            float y0 = grade.BenchY(row, 0), y1 = grade.BenchY(row, 1);
            st = new Station
            {
                X = x, Z = z, A0 = y0 - lift, A1 = y1 - y0, EL = shift + half, ER = shift - half, CL = Math.Max(left, shift + half),
                CR = Math.Max(right, -(shift - half)), Wall = wall, Road = i,
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
            float verge = st.Wall ? 0.3f : VergeM * st.S;
            float tolC = weak * StreetCutTolM, tolF = weak * StreetFillTolM;
            for (int side = 0; side < 2; side++)
            {
                double sign = side == 0 ? 1 : -1;
                float ext = side == 0 ? st.CL : st.CR;
                float edge = side == 0 ? st.EL : st.ER;
                float bench = st.A0 + edge * st.A1;
                float kc = (st.Wall ? WallSlope : CutSlope) + weak * WeakSlope, kf = (st.Wall ? WallSlope : FillSlope) + weak * WeakSlope;
                float wall = 0f;
                if (!st.Wall && st.S >= 0.5f)
                {
                    float ec = float.NegativeInfinity, ef = float.NegativeInfinity, ec6 = 0f, ef6 = 0f;
                    for (int d = 0; d <= (int)SteepenStartM; d++)
                    {
                        float rc = Natural(st.X + ux * sign * (ext + verge + d), st.Z + uz * sign * (ext + verge + d)) - bench;
                        float rf = Natural(st.X + ux * sign * (ext + d), st.Z + uz * sign * (ext + d)) - bench;
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
                    float lo = bench - tolF - kf * d - st.S * SteepenRate * sf * sf + Math.Min(0f, wall);
                    float hi = bench + tolC + kc * dc + st.S * SteepenRate * sc * sc + Math.Max(0f, wall);
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
            lay.CorridorSides(i, s, out left, out right);
            float y0 = grade.BenchY(row, 0);
            // Going outward the travel direction is reversed at the start: left and right swap.
            bool flip = k == 0;
            float el = flip ? -(shift - half) : shift + half, er = flip ? -(shift + half) : shift - half;
            float cl = flip ? right : left, cr = flip ? left : right;
            var a = new Station
            {
                X = x, Z = z, A0 = y0 - lift, A1 = 0f, EL = el, ER = er, CL = Math.Max(cl, el), CR = Math.Max(cr, -er), G = 0f, Road = i, Tx = tx, Tz = tz,
            };
            a.S = Strength(a);
            Reach(ref a, tx, tz);
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
            _verge[k] = wall ? 0.3f : VergeM;
            _sa[k] = a.S;
            _sb[k] = b.S;
            _wla[k] = a.WL;
            _wlb[k] = b.WL;
            _wra[k] = a.WR;
            _wrb[k] = b.WR;
            _road[k] = a.Road;
            _run[k] = -1;
            _flags[k] = flags;
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
        }

        /// <summary>
        /// The gentle streets' cut: wherever the coarse terrain triangles would rise more than <see cref="StreetCutTolM"/>
        /// above the bench under the drawn extent of a street on gentle ground (or a junction cap on one), the corners of
        /// that triangle are lowered (never on the tile border, at most <see cref="MaxLowerM"/>), so no ground shows
        /// through a road without refining the grid. Hill roads are held by the fine carve instead.
        /// </summary>
        private void CoarseLower()
        {
            const double Step = 1.5;
            for (int pass = 0; pass < 4; pass++)
            {
                bool any = false;
                for (int e = 0; e < _segCount; e++)
                {
                    if (Math.Min(_sa[e], _sb[e]) >= 0.99f || _kc[e] == WallSlope) continue;
                    double len = _len[e];
                    int nu = Math.Max(1, (int)Math.Ceiling(len / Step));
                    double ux = -_dz[e], uz = _dx[e];
                    for (int iu = 0; iu <= nu; iu++)
                    {
                        float t = (float)iu / nu;
                        float cl = _cla[e] + (_clb[e] - _cla[e]) * t, cr = _cra[e] + (_crb[e] - _cra[e]) * t;
                        float el = _ela[e] + (_elb[e] - _ela[e]) * t, er = _era[e] + (_erb[e] - _era[e]) * t;
                        float a0 = _a0a[e] + (_a0b[e] - _a0a[e]) * t, a1 = _a1a[e] + (_a1b[e] - _a1a[e]) * t;
                        double x = _ax[e] + _dx[e] * len * t, z = _az[e] + _dz[e] * len * t;
                        int na = Math.Max(1, (int)Math.Ceiling((cl + cr) / Step));
                        for (int ia = 0; ia <= na; ia++)
                        {
                            double off = -cr + (cl + cr) * ia / na;
                            double co = off > el ? el : off < er ? er : off;
                            if (LowerAt(x + ux * off, z + uz * off, a0 + (float)co * a1 + StreetCutTolM)) any = true;
                        }
                    }
                }
                for (int pi = 0; pi < _pads.Count; pi++)
                {
                    RoadPad p = _pads[pi];
                    if (p.Hill) continue;
                    for (double z = p.MinZ; z <= p.MaxZ + 1e-6; z += Step)
                    for (double x = p.MinX; x <= p.MaxX + 1e-6; x += Step)
                        if (p.Distance(x, z) <= 0 && LowerAt(x, z, p.Height(x, z) + StreetCutTolM)) any = true;
                }
                if (!any) break;
            }
            int n = 0;
            foreach (bool b in _lowered)
                if (b) n++;
            LoweredVertices = n;
        }

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

        private bool Lower(int idx, float by, int l, int k)
        {
            int q = Grid.Quads;
            if (l <= 0 || k <= 0 || l >= q || k >= q) return false; // the tile border stays (the neighbour does not know)
            float floor = Grid.VertexHeight(Source, k, l) - MaxLowerM;
            float v = Math.Max(floor, _coarse[idx] - by);
            if (v >= _coarse[idx]) return false;
            _coarse[idx] = v;
            _lowered[idx] = true;
            return true;
        }

        /// <summary>Height of coarse grid vertex (row k, column l): the natural one, lowered under gentle streets.</summary>
        public float CoarseVertex(int k, int l)
        {
            return _coarse[k * (Grid.Quads + 1) + l];
        }

        /// <summary>True when coarse vertex (row k, column l) was lowered under a gentle street.</summary>
        public bool IsLowered(int k, int l)
        {
            return _lowered[k * (Grid.Quads + 1) + l];
        }

        /// <summary>The coarse surface (natural, lowered under gentle streets) at area-local fractional grid coordinates
        /// (u, v), clamped to the grid, with its gradient per quad, split as <see cref="TerrainGrid.Surface"/>.</summary>
        public double CoarseSurface(double u, double v, out double dhdu, out double dhdv)
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
            double h00 = _coarse[b], h10 = _coarse[b + 1], h01 = _coarse[b + side], h11 = _coarse[b + side + 1];
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

        /// <summary>True when element <paramref name="e"/> cuts and fills a hillside (gentle streets only lower the coarse
        /// grid).</summary>
        private bool Strong(int e)
        {
            return e < _segCount ? _sa[e] > 0f || _sb[e] > 0f : _padS[e - _segCount] > 0f;
        }

        /// <summary>Buckets every element into the 8 m element grid by its box grown by its extent and reach.</summary>
        private void IndexElements()
        {
            double size = Source.Tile.Size;
            _ecells = Math.Max(1, (int)Math.Ceiling(size / ElemCellM));
            int nc = _ecells * _ecells;
            var count = new int[nc];
            int total = _segCount + _pads.Count;
            for (int pass = 0; pass < 2; pass++)
            {
                int[] fill = null;
                if (pass == 1)
                {
                    _estart = new int[nc + 1];
                    for (int k = 0; k < nc; k++) _estart[k + 1] = _estart[k] + count[k];
                    _eitems = new int[_estart[nc]];
                    fill = new int[nc];
                    Array.Copy(_estart, fill, nc);
                }
                for (int e = 0; e < total; e++)
                {
                    if (!Strong(e)) continue;
                    double x0, z0, x1, z1;
                    Box(e, out x0, out z0, out x1, out z1);
                    if (x1 < 0 || z1 < 0 || x0 > size || z0 > size) continue;
                    int i0 = ECell(x0), i1 = ECell(x1), j0 = ECell(z0), j1 = ECell(z1);
                    for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                    {
                        int cell = j * _ecells + i;
                        if (pass == 0) count[cell]++;
                        else _eitems[fill[cell]++] = e;
                    }
                }
            }
        }

        private void Box(int e, out double x0, out double z0, out double x1, out double z1)
        {
            if (e < _segCount)
            {
                double r = Math.Max(Math.Max(_cla[e], _clb[e]) + _rl[e], Math.Max(_cra[e], _crb[e]) + _rr[e]) + 0.5;
                double bx = _ax[e] + _dx[e] * _len[e], bz = _az[e] + _dz[e] * _len[e];
                x0 = Math.Min(_ax[e], bx) - r;
                x1 = Math.Max(_ax[e], bx) + r;
                z0 = Math.Min(_az[e], bz) - r;
                z1 = Math.Max(_az[e], bz) + r;
                return;
            }
            RoadPad p = _pads[e - _segCount];
            double m = MaxReachM + 0.5;
            x0 = p.MinX - m;
            x1 = p.MaxX + m;
            z0 = p.MinZ - m;
            z1 = p.MaxZ + m;
        }

        private int ECell(double v)
        {
            int c = (int)Math.Floor(v / ElemCellM);
            return c < 0 ? 0 : c >= _ecells ? _ecells - 1 : c;
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
            double px = lx - _ax[e], pz = lz - _az[e];
            double u = px * _dx[e] + pz * _dz[e];
            double lat = _dx[e] * pz - _dz[e] * px;
            if (u < 0)
            {
                if ((_flags[e] & ClipStart) != 0) return double.PositiveInfinity;
                return lat * lat + u * u;
            }
            if (u > _len[e])
            {
                if ((_flags[e] & ClipEnd) != 0) return double.PositiveInfinity;
                double o = u - _len[e];
                return lat * lat + o * o;
            }
            return lat * lat;
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
                  .Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>Cut and fill bounds of element <paramref name="e"/> at (lx, lz); false when it does not reach.</summary>
        private bool Bounds(int e, double lx, double lz, float border, out Say say)
        {
            say = default(Say);
            float bench, kc, kf, g, reach, wallLift = 0f, dF, dC, str;
            if (e < _segCount)
            {
                double px = lx - _ax[e], pz = lz - _az[e];
                double u = px * _dx[e] + pz * _dz[e];
                double lat = _dx[e] * pz - _dz[e] * px;
                double len = _len[e];
                byte f = _flags[e];
                double off;
                float t;
                if (u < 0)
                {
                    if ((f & ClipStart) != 0) return false;
                    t = 0f;
                    off = Math.Sqrt(lat * lat + u * u);
                    if (lat < 0) off = -off;
                }
                else if (u > len)
                {
                    if ((f & ClipEnd) != 0) return false;
                    t = 1f;
                    double o = u - len;
                    off = Math.Sqrt(lat * lat + o * o);
                    if (lat < 0) off = -off;
                }
                else
                {
                    t = (float)(u / len);
                    off = lat;
                }
                bool left = off >= 0;
                float ext = left ? _cla[e] + (_clb[e] - _cla[e]) * t : _cra[e] + (_crb[e] - _cra[e]) * t;
                float el = _ela[e] + (_elb[e] - _ela[e]) * t, er = _era[e] + (_erb[e] - _era[e]) * t;
                float a = (float)Math.Abs(off);
                dF = a > ext ? a - ext : 0f;
                say.Ext = a - ext;
                reach = left ? _rl[e] : _rr[e];
                if (dF > reach) return false;
                str = _sa[e] + (_sb[e] - _sa[e]) * t;
                if (str <= 0f) return false;
                float verge = _verge[e] * str;
                dC = a > ext + verge ? a - ext - verge : 0f;
                say.Pd = a - (left ? el : -er);
                double co = off > el ? el : off < er ? er : off;
                bench = _a0a[e] + (_a0b[e] - _a0a[e]) * t + (float)co * (_a1a[e] + (_a1b[e] - _a1a[e]) * t);
                kc = _kc[e];
                kf = _kf[e];
                g = _ga[e] + (_gb[e] - _ga[e]) * t;
                float wall = left ? _wla[e] + (_wlb[e] - _wla[e]) * t : _wra[e] + (_wrb[e] - _wra[e]) * t;
                if (wall > 0f) wallLift = wall * (dC >= WallFaceM ? 1f : dC / WallFaceM);
                else if (wall < 0f) wallLift = wall * (dF >= WallFaceM ? 1f : dF / WallFaceM);
            }
            else
            {
                RoadPad p = _pads[e - _segCount];
                if (lx < p.MinX - MaxReachM || lx > p.MaxX + MaxReachM || lz < p.MinZ - MaxReachM || lz > p.MaxZ + MaxReachM) return false;
                str = _padS[e - _segCount];
                if (str <= 0f) return false;
                double d = p.Distance(lx, lz);
                if (d > MaxReachM) return false;
                dF = (float)d;
                say.Ext = dF;
                float pv = VergeM * str;
                dC = dF > pv ? dF - pv : 0f;
                say.Pd = dF;
                bench = p.Height(lx, lz);
                kc = CutSlope;
                kf = FillSlope;
                g = 1f;
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
            public float[] Val = new float[0], Nat = new float[0], Ext = new float[0];
            public int[] Stamp = new int[0];
            public int Mark;

            public void Ensure(int n)
            {
                if (Val.Length >= n) return;
                Val = new float[n];
                Nat = new float[n];
                Ext = new float[n];
                Stamp = new int[n];
                Mark = 0;
            }
        }

        [ThreadStatic] private static Scratch _scratch;

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
            // Candidate cells: under some element's reach.
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
            // Pass 1: carved and natural heights at every sub-vertex of the candidate cells; a cell is refined where the
            // carve cuts one under a road (or its verge) by more than RefineCutM or moves it anywhere by more than RefineMoveM.
            var refinedFlag = new bool[q * q];
            var refined = new List<int>(256);
            for (int cell = 0; cell < q * q; cell++)
            {
                if (!candidate[cell]) continue;
                int ci = cell % q, cj = cell / q;
                bool moved = false;
                for (int b = 0; b <= r; b++)
                for (int a = 0; a <= r; a++)
                {
                    int gi = ci * r + a, gj = cj * r + b, idx = gj * side + gi;
                    if (sc.Stamp[idx] != mark)
                    {
                        double lx = _offX + gi * SubM, lz = _offZ + gj * SubM, du, dv;
                        float nat = (float)CoarseSurface(gi / (double)r, gj / (double)r, out du, out dv);
                        sc.Nat[idx] = nat;
                        float ext;
                        sc.Val[idx] = CarvedAt(lx, lz, nat, out ext);
                        sc.Ext[idx] = ext;
                        sc.Stamp[idx] = mark;
                    }
                    float move = sc.Val[idx] - sc.Nat[idx];
                    if (Math.Abs(move) > (sc.Ext[idx] <= VergeM ? RefineCutM : RefineMoveM)) moved = true;
                }
                if (moved)
                {
                    refined.Add(cell);
                    refinedFlag[cell] = true;
                    if (DiagOn)
                    {
                        float maxCut = 0f, maxMove = 0f, maxNatDiff = 0f;
                        for (int b = 0; b <= r; b++)
                        for (int a = 0; a <= r; a++)
                        {
                            int idx = (cj * r + b) * side + ci * r + a;
                            float mv = sc.Val[idx] - sc.Nat[idx];
                            if (sc.Ext[idx] <= VergeM && -mv > maxCut) maxCut = -mv;
                            if (Math.Abs(mv) > maxMove) maxMove = Math.Abs(mv);
                            double lx = _offX + (ci * r + a) * SubM, lz = _offZ + (cj * r + b) * SubM;
                            float n1 = TerrainGrid.SourceSurface(Source, Source.Tile.X0 + lx, Source.Tile.Z0 + lz);
                            maxNatDiff = Math.Max(maxNatDiff, Math.Abs(n1 - sc.Nat[idx]));
                        }
                        int bucket = maxMove < 0.5f ? 0 : maxMove < 1f ? 1 : maxMove < 2f ? 2 : 3;
                        DiagBuckets[bucket]++;
                        if (maxCut > 0f && maxMove <= RefineMoveM) DiagCutOnly++;
                        if (maxNatDiff > 0.3f) DiagNatDiff++;
                    }
                }
            }
            // Pass 2: a sub-vertex takes the carved height only when every cell around it is refined (else the natural
            // height, on the unrefined neighbour's straight edge).
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
                    bool all = true;
                    if (a == 0 || a == r || b == 0 || b == r)
                    {
                        int ia = a == 0 ? ci - 1 : ci, ib = a == r ? ci + 1 : ci, ja = b == 0 ? cj - 1 : cj, jb = b == r ? cj + 1 : cj;
                        for (int jj = ja; jj <= jb && all; jj++)
                        for (int ii = ia; ii <= ib && all; ii++)
                        {
                            // Cells off the grid count as refined (the tile border: both tiles keep their own carve there,
                            // which the border fade holds to the natural ground).
                            if (ii < 0 || jj < 0 || ii >= q || jj >= q) continue;
                            if (!refinedFlag[jj * q + ii]) all = false;
                        }
                    }
                    _heights[start + b * (r + 1) + a] = all ? sc.Val[idx] : sc.Nat[idx];
                }
            }
            RefinedCells = refined.Count;
        }

        // ------------------------------------------------------------------------------------------------------------
        // Queries (the drawn surface)
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
        /// The drawn surface at area-local fractional grid coordinates (u, v) in quads, clamped to the grid, with the facet
        /// gradient per metre: the carved sub-quads in refined cells (each split along its (a, b)-(a+1, b+1) diagonal, as
        /// the coarse cells are), the natural grid elsewhere.
        /// </summary>
        public double Surface(double u, double v, out double gx, out double gz)
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
