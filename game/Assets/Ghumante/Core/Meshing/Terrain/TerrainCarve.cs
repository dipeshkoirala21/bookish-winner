using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// The terrain after every road cut and fill (owner playtest at Gaurighat: "Fix the roads on slopes. Make them flat like
    /// how they make them, cutting the hills"): the single carved surface the terrain mesh draws and that the samplers, the
    /// ground query and every placer read. Roads stay level across (<see cref="RoadGrade"/>); the terrain is carved to meet
    /// them as hill roads are built in Nepal:
    /// <list type="bullet">
    /// <item>under each road's corridor (carriageway, footpaths or shoulders, and a verge as wide as one terrain sub-cell
    /// diagonal, the side drain or gravel shoulder) the terrain is held within <see cref="CutToleranceM"/> /
    /// <see cref="FillToleranceM"/> of the level bench (the road surface less its lift), and under a junction cap or a
    /// roundabout within the same band of the junction's levelled platform (<see cref="RoadPads"/>);</item>
    /// <item>beyond the corridor the uphill side is cut back at 1 : 1 (<see cref="CutSlope"/>, soil) and the downhill side
    /// filled at 1 : 1.5 (<see cref="FillSlope"/>, an embankment), both steepening beyond <see cref="SteepenStartM"/> into a
    /// rock face or a retaining wall where the hillside is too steep for an open slope, until they meet the natural ground
    /// (the daylight line);</item>
    /// <item>where several roads reach a point (junctions, switchbacks, parallel pieces) the tightest cut and fill bounds
    /// win; where they contradict (one road's fill above another's cut) the surface blends between them by distance;</item>
    /// <item>lowered underpass stretches are just another cut, with near-vertical trench walls at their edges.</item>
    /// </list>
    /// <para>
    /// Tile seams are continuous: a tile-border crossing's profile is level over <see cref="RoadGrade.SeamLandingM"/> on both
    /// sides and continued straight beyond the border, so both tiles carve the same bench there, and every other road's
    /// cut and fill relaxes to the natural ground on the tile border (<see cref="BorderFadeM"/>).
    /// </para>
    /// <para>
    /// The drawn surface is the natural terrain grid (<see cref="TerrainGrid"/>) with the cells the carve touches
    /// (<see cref="DeltaEpsilonM"/>) refined into <see cref="Sub"/> × <see cref="Sub"/> sub-quads of about
    /// <see cref="SubCellM"/>, whose vertices carry the carved heights (vertices that move less than
    /// <see cref="DeltaEpsilonM"/> keep the natural height, so a refined cell meets its unrefined neighbours on their
    /// straight edges without cracks and both tiles of a seam agree vertex for vertex). <see cref="TerrainMesher"/> draws
    /// exactly these triangles and <see cref="TileHeightSampler"/> interpolates exactly them, so the ground query matches
    /// what is drawn. Built once per (tile, area, step) and cached; immutable and thread-safe afterwards.
    /// </para>
    /// </summary>
    public sealed partial class TerrainCarve
    {
        /// <summary>How far the carved terrain under a road may stay above the bench (the road surface less its lift, so the
        /// terrain stays at least 13 cm under a 25 cm lifted road).</summary>
        public const float CutToleranceM = RoadGrade.MaxBuryM;

        /// <summary>How far the carved terrain under a road may stay below the bench (the ribbon's skirt covers it).</summary>
        public const float FillToleranceM = RoadGrade.BandM;

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

        /// <summary>Side of the refined sub-quads (the grid cell is split into a power of two of them).</summary>
        public const float SubCellM = 2f;

        /// <summary>Carved heights closer than this to the natural terrain are not drawn (the cell keeps its natural
        /// triangles).</summary>
        public const float DeltaEpsilonM = 0.02f;

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

        /// <summary>One station of a road run: centreline point, bench (centre height less lift, cross-fall), drawn edges,
        /// corridor extents with the verge and the reach of its slopes.</summary>
        private struct Station
        {
            public double X, Z;
            public float A0, A1, EL, ER, CL, CR, RL, RR, G;

            /// <summary>Retaining wall height on each side: positive a breast wall holding a cut, negative a wall holding a
            /// fill; 0 none.</summary>
            public float WL, WR;

            /// <summary>A lowered underpass stretch: trench walls at the corridor edges.</summary>
            public bool Wall;

            public int Road;
        }

        // Segments (structure of arrays).
        private int _segCount;
        private double[] _ax = new double[0], _az = new double[0], _len = new double[0], _dx = new double[0], _dz = new double[0];
        private float[] _a0a = new float[0], _a0b = new float[0], _a1a = new float[0], _a1b = new float[0];
        private float[] _ela = new float[0], _elb = new float[0], _era = new float[0], _erb = new float[0];
        private float[] _cla = new float[0], _clb = new float[0], _cra = new float[0], _crb = new float[0];
        private float[] _rl = new float[0], _rr = new float[0], _ga = new float[0], _gb = new float[0], _kc = new float[0], _kf = new float[0];
        private float[] _wla = new float[0], _wlb = new float[0], _wra = new float[0], _wrb = new float[0];
        private int[] _road = new int[0];
        private byte[] _flags = new byte[0];

        private const byte ClipStart = 1, ClipEnd = 2;

        private readonly List<RoadPad> _pads = new List<RoadPad>();
        private float _padVerge;

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

        /// <summary>Verge beside each corridor (one sub-quad diagonal plus a margin).</summary>
        public readonly float VergeM;

        private readonly int[] _block; // per grid cell (row-major from the south-west): start into _heights, or -1
        private float[] _heights;
        private readonly double _offX, _offZ; // area origin in tile-local metres

        /// <summary>Grid cells refined (drawn with carved sub-quads).</summary>
        public int RefinedCells { get; private set; }

        /// <summary>Road segments and junction platforms the carve follows.</summary>
        public int ElementCount
        {
            get { return _segCount + _pads.Count; }
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
                    slot.Carve = c.RefinedCells > 0 ? c : null;
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
            int sub = 1;
            while (Grid.CellM / (sub * 2) >= SubCellM - 1e-6 && sub < 32) sub *= 2;
            Sub = sub;
            SubM = Grid.CellM / sub;
            VergeM = (float)(SubM * Math.Sqrt(2.0) + 0.3);
            _offX = Grid.Area.X0 - src.Tile.X0;
            _offZ = Grid.Area.Z0 - src.Tile.Z0;
            int q = Grid.Quads;
            _block = new int[q * q];
            for (int k = 0; k < _block.Length; k++) _block[k] = -1;
            _heights = new float[0];
            RoadGrade grade = RoadGrade.For(src, sampler, new RoadOptions());
            CollectSegments(grade);
            CollectPads(grade);
            IndexElements();
            Rasterise();
            BuildMs = sw.Elapsed.TotalMilliseconds;
        }

        private float Natural(double lx, double lz)
        {
            double cm = Grid.CellM, du, dv;
            return (float)Grid.Surface(Source, (lx - _offX) / cm, (lz - _offZ) / cm, out du, out dv);
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
            float verge = wall ? 0.3f : VergeM;
            float y0 = grade.BenchY(row, 0), y1 = grade.BenchY(row, 1);
            st = new Station
            {
                X = x, Z = z, A0 = y0 - lift, A1 = y1 - y0, EL = shift + half, ER = shift - half,
                CL = Math.Max(left, shift + half) + verge, CR = Math.Max(right, -(shift - half)) + verge, Wall = wall, Road = i,
            };
            // Seam landing: no border fade within the level landing (both tiles carve it alike), ramping in beyond.
            double l = c.ArcAt(s);
            double fromSeam = double.MaxValue;
            if (seamStart) fromSeam = Math.Min(fromSeam, l);
            if (seamEnd) fromSeam = Math.Min(fromSeam, arcLen - l);
            st.G = fromSeam <= RoadGrade.SeamLandingM ? 0f : (float)Math.Min(1.0, (fromSeam - RoadGrade.SeamLandingM) / 4.0);
            Reach(ref st, tx, tz);
            return true;
        }

        /// <summary>
        /// Retaining walls and reach of a station on each side. A wall is built where the open slope (1 : 1 cut, 1 : 1.5
        /// fill) would not meet the natural ground within <see cref="SteepenStartM"/> of the corridor: a breast wall holding
        /// the cut, or a gabion or stone wall holding the fill, as tall as that needs (from <see cref="WallMinM"/> to
        /// <see cref="WallMaxM"/>). The reach is how far beyond the corridor the cut and fill (with the wall) still bite:
        /// the natural ground is sampled outward until it lies inside the bounds for good (plus a margin), at most
        /// <see cref="MaxReachM"/>.
        /// </summary>
        private void Reach(ref Station st, double tx, double tz)
        {
            double ux = -tz, uz = tx;
            for (int side = 0; side < 2; side++)
            {
                double sign = side == 0 ? 1 : -1;
                float ext = side == 0 ? st.CL : st.CR;
                float edge = side == 0 ? st.EL : st.ER;
                float bench = st.A0 + edge * st.A1;
                float kc = st.Wall ? WallSlope : CutSlope, kf = st.Wall ? WallSlope : FillSlope;
                float wall = 0f;
                if (!st.Wall)
                {
                    float ec = float.NegativeInfinity, ef = float.NegativeInfinity, ec6 = 0f, ef6 = 0f;
                    for (int d = 0; d <= (int)SteepenStartM; d++)
                    {
                        double off = sign * (ext + d);
                        float rel = Natural(st.X + ux * off, st.Z + uz * off) - bench;
                        float c = rel - CutToleranceM - kc * d, f = -rel - FillToleranceM - kf * d;
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
                    float steep = Math.Max(0f, d - SteepenStartM), qd = SteepenRate * steep * steep;
                    float lo = bench - FillToleranceM - kf * d - qd + Math.Min(0f, wall), hi = bench + CutToleranceM + kc * d + qd + Math.Max(0f, wall);
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
                // Bridge short gaps.
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
                // Drop short walls.
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
            float cl = (flip ? right : left), cr = (flip ? left : right);
            var a = new Station
            {
                X = x, Z = z, A0 = y0 - lift, A1 = 0f, EL = el, ER = er, CL = Math.Max(cl, el) + VergeM, CR = Math.Max(cr, -er) + VergeM, G = 0f, Road = i,
            };
            Reach(ref a, tx, tz);
            double len = RoadGrade.SeamLandingM + 1.0;
            Station b = a;
            b.X = x + tx * len;
            b.Z = z + tz * len;
            AddSegment(a, b, ClipStart | ClipEnd);
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
            SmoothWalls(run);
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
            run.Clear();
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
            _kc[k] = a.Wall || b.Wall ? WallSlope : CutSlope;
            _kf[k] = a.Wall || b.Wall ? WallSlope : FillSlope;
            _wla[k] = a.WL;
            _wlb[k] = b.WL;
            _wra[k] = a.WR;
            _wrb[k] = b.WR;
            _road[k] = a.Road;
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
            Array.Resize(ref _flags, cap);
            Array.Resize(ref _wla, cap);
            Array.Resize(ref _wlb, cap);
            Array.Resize(ref _wra, cap);
            Array.Resize(ref _wrb, cap);
            Array.Resize(ref _road, cap);
        }

        private void CollectPads(RoadGrade grade)
        {
            _padVerge = VergeM;
            foreach (RoadPad p in grade.Pads.Pads) _pads.Add(p);
        }

        /// <summary>Buckets every element into the 8 m element grid by its box grown by its corridor and reach.</summary>
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
                    double x0, z0, x1, z1;
                    Box(e, out x0, out z0, out x1, out z1);
                    int i0 = ECell(x0), i1 = ECell(x1), j0 = ECell(z0), j1 = ECell(z1);
                    if (x1 < 0 || z1 < 0 || x0 > size || z0 > size) continue;
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
            double m = _padVerge + MaxReachM + 0.5;
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

        /// <summary>
        /// The carved height at tile-local (<paramref name="lx"/>, <paramref name="lz"/>) over the natural height
        /// <paramref name="natural"/> there: the natural ground clamped into every road's cut and fill bounds (blended by
        /// distance where they contradict). Returns <paramref name="natural"/> where no road reaches.
        /// </summary>
        public float CarvedAt(double lx, double lz, float natural)
        {
            int cell = (lx < 0 || lz < 0 || lx >= _ecells * ElemCellM || lz >= _ecells * ElemCellM) ? -1 : ECell(lz) * _ecells + ECell(lx);
            if (cell < 0)
            {
                double cx = Math.Max(0, Math.Min(_ecells * ElemCellM - 1e-6, lx)), cz = Math.Max(0, Math.Min(_ecells * ElemCellM - 1e-6, lz));
                cell = ECell(cz) * _ecells + ECell(cx);
            }
            float lo = float.NegativeInfinity, hi = float.PositiveInfinity, dLo = 0f, dHi = 0f;
            bool any = false;
            double size = Source.Tile.Size;
            double b = Math.Min(Math.Min(lx, lz), Math.Min(size - lx, size - lz));
            float border = b >= BorderFadeM ? 0f : (float)(BorderFadeK * (BorderFadeM - Math.Max(0, b)) * (BorderFadeM - Math.Max(0, b)));
            for (int q = _estart[cell]; q < _estart[cell + 1]; q++)
            {
                int e = _eitems[q];
                float elo, ehi, dist;
                if (!Bounds(e, lx, lz, border, out elo, out ehi, out dist)) continue;
                any = true;
                if (elo > lo)
                {
                    lo = elo;
                    dLo = dist;
                }
                if (ehi < hi)
                {
                    hi = ehi;
                    dHi = dist;
                }
            }
            if (!any) return natural;
            if (lo <= hi) return natural < lo ? lo : natural > hi ? hi : natural;
            // Contradicting roads (one's fill above another's cut): between them, by distance.
            float sum = dLo + dHi;
            float t = sum > 1e-4f ? dHi / sum : 0.5f;
            return hi + (lo - hi) * t;
        }

        /// <summary>Cut and fill bounds of element <paramref name="e"/> at (lx, lz); false when it does not reach.</summary>
        private bool Bounds(int e, double lx, double lz, float border, out float lo, out float hi, out float dist)
        {
            lo = hi = dist = 0f;
            float bench, kc, kf, g, reach, wallLift = 0f;
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
                double d = Math.Abs(off) - ext;
                dist = d > 0 ? (float)d : 0f;
                reach = left ? _rl[e] : _rr[e];
                if (dist > reach) return false;
                float el = _ela[e] + (_elb[e] - _ela[e]) * t, er = _era[e] + (_erb[e] - _era[e]) * t;
                double co = off > el ? el : off < er ? er : off;
                bench = _a0a[e] + (_a0b[e] - _a0a[e]) * t + (float)co * (_a1a[e] + (_a1b[e] - _a1a[e]) * t);
                kc = _kc[e];
                kf = _kf[e];
                g = _ga[e] + (_gb[e] - _ga[e]) * t;
                float wall = left ? _wla[e] + (_wlb[e] - _wla[e]) * t : _wra[e] + (_wrb[e] - _wra[e]) * t;
                if (wall != 0f)
                {
                    float wf = dist >= WallFaceM ? 1f : dist / WallFaceM;
                    wallLift = wall * wf;
                }
            }
            else
            {
                RoadPad p = _pads[e - _segCount];
                if (lx < p.MinX - _padVerge - MaxReachM || lx > p.MaxX + _padVerge + MaxReachM ||
                    lz < p.MinZ - _padVerge - MaxReachM || lz > p.MaxZ + _padVerge + MaxReachM) return false;
                double d = p.Distance(lx, lz) - _padVerge;
                dist = d > 0 ? (float)d : 0f;
                if (dist > MaxReachM) return false;
                bench = p.Height(lx, lz);
                kc = CutSlope;
                kf = FillSlope;
                g = 1f;
            }
            float steep = dist > SteepenStartM ? dist - SteepenStartM : 0f;
            float qd = SteepenRate * steep * steep;
            float w = border > 0f && g > 0f ? border * g * Math.Min(1f, dist * 0.5f) : 0f;
            lo = bench - FillToleranceM - kf * dist - qd - w + Math.Min(0f, wallLift);
            hi = bench + CutToleranceM + kc * dist + qd + w + Math.Max(0f, wallLift);
            return true;
        }

        // ------------------------------------------------------------------------------------------------------------
        // Rasterise onto the refined grid
        // ------------------------------------------------------------------------------------------------------------

        private sealed class Scratch
        {
            public float[] Val = new float[0];
            public bool[] Moved = new bool[0];
            public int[] Stamp = new int[0];
            public int Mark;

            public void Ensure(int n)
            {
                if (Val.Length >= n) return;
                Val = new float[n];
                Moved = new bool[n];
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
                double x0, z0, x1, z1;
                Box(e, out x0, out z0, out x1, out z1);
                int i0 = Math.Max(0, (int)Math.Floor((x0 - _offX) / cm)), i1 = Math.Min(q - 1, (int)Math.Floor((x1 - _offX) / cm));
                int j0 = Math.Max(0, (int)Math.Floor((z0 - _offZ) / cm)), j1 = Math.Min(q - 1, (int)Math.Floor((z1 - _offZ) / cm));
                for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++) candidate[j * q + i] = true;
            }
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
                        float nat = (float)Grid.Surface(Source, gi / (double)r, gj / (double)r, out du, out dv);
                        float v = CarvedAt(lx, lz, nat);
                        bool m = Math.Abs(v - nat) > DeltaEpsilonM;
                        sc.Val[idx] = m ? v : nat;
                        sc.Moved[idx] = m;
                        sc.Stamp[idx] = mark;
                    }
                    if (sc.Moved[idx]) moved = true;
                }
                if (moved) refined.Add(cell);
            }
            int per = (r + 1) * (r + 1);
            _heights = new float[refined.Count * per];
            for (int k = 0; k < refined.Count; k++)
            {
                int cell = refined[k], ci = cell % q, cj = cell / q, start = k * per;
                _block[cell] = start;
                for (int b = 0; b <= r; b++)
                for (int a = 0; a <= r; a++)
                    _heights[start + b * (r + 1) + a] = sc.Val[(cj * r + b) * side + ci * r + a];
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
                double h = Grid.Surface(Source, u, v, out du, out dv);
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
