using System;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// The cross-section rows the ribbon of one piece is drawn with (<see cref="RibbonMesher.BuildFrames"/>): position,
    /// section vector (oblique on a tile-border cut, so offsets land on the border line), carriageway edges after the
    /// tight-curve pinch, footpath widths, the grade row, and the carriageway columns with their surface heights. The
    /// markings sample exactly these rows (<see cref="MarkingMesher"/>), so paint lies on the drawn surface.
    /// </summary>
    internal sealed class RibbonFrames
    {
        public const int MaxCols = 5;

        public int Count;
        public double[] S = new double[64], X = new double[64], Z = new double[64], Ux = new double[64], Uz = new double[64];
        public double[] Tx = new double[64], Tz = new double[64];
        public float[] K = new float[64];

        /// <summary>The segment from row k to row k + 1 lies in a junction gap (not drawn).</summary>
        public bool[] GapAfter = new bool[64];

        public double[] Left = new double[64], Right = new double[64], Shift = new double[64];
        public float[] FootL = new float[64], FootR = new float[64];
        public RoadGrade.Row[] Grade = new RoadGrade.Row[64];
        public float[] Nx = new float[64], Ny = new float[64], Nz = new float[64];

        /// <summary>Carriageway columns of every row: offsets (left of the centreline positive, descending) and heights.</summary>
        public double[] ColOff = new double[64 * MaxCols];

        public float[] ColY = new float[64 * MaxCols];
        public int Cols;

        /// <summary>Column index of the crown (−1 without one), of the left and right AO columns (−1 without).</summary>
        public int CrownCol, AoLeftCol, AoRightCol;

        public void Ensure(int n)
        {
            if (S.Length >= n) return;
            int c = Math.Max(n, S.Length * 2);
            Array.Resize(ref S, c);
            Array.Resize(ref X, c);
            Array.Resize(ref Z, c);
            Array.Resize(ref Ux, c);
            Array.Resize(ref Uz, c);
            Array.Resize(ref Tx, c);
            Array.Resize(ref Tz, c);
            Array.Resize(ref K, c);
            Array.Resize(ref GapAfter, c);
            Array.Resize(ref Left, c);
            Array.Resize(ref Right, c);
            Array.Resize(ref Shift, c);
            Array.Resize(ref FootL, c);
            Array.Resize(ref FootR, c);
            Array.Resize(ref Grade, c);
            Array.Resize(ref Nx, c);
            Array.Resize(ref Ny, c);
            Array.Resize(ref Nz, c);
            Array.Resize(ref ColOff, c * MaxCols);
            Array.Resize(ref ColY, c * MaxCols);
        }

        /// <summary>Surface height of row k at lateral offset <paramref name="off"/>: piecewise linear across its columns
        /// (clamped to the carriageway).</summary>
        public float HeightAcross(int k, double off)
        {
            int b = k * MaxCols;
            if (off >= ColOff[b]) return ColY[b];
            for (int c = 1; c < Cols; c++)
            {
                double o0 = ColOff[b + c - 1], o1 = ColOff[b + c];
                if (off >= o1)
                {
                    double f = o0 - o1 > 1e-9 ? (off - o1) / (o0 - o1) : 0;
                    return (float)(ColY[b + c] + (ColY[b + c - 1] - ColY[b + c]) * f);
                }
            }
            return ColY[b + Cols - 1];
        }

        /// <summary>The row segment holding raw along <paramref name="s"/> (clamped), or −1 for an empty frame set.</summary>
        public int SegmentAt(double s)
        {
            if (Count < 2) return -1;
            if (s <= S[0]) return 0;
            if (s >= S[Count - 1]) return Count - 2;
            int lo = 0, hi = Count - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (S[mid] <= s) lo = mid;
                else hi = mid;
            }
            return lo;
        }

        /// <summary>
        /// The drawn surface point at raw along <paramref name="s"/> and lateral offset <paramref name="off"/>: between the
        /// two rows around <paramref name="s"/>, interpolated linearly along and across exactly as the ribbon's quads (to
        /// within their twist). False inside a junction gap.
        /// </summary>
        public bool PointAt(double s, double off, out double x, out double y, out double z)
        {
            x = y = z = 0;
            int k = SegmentAt(s);
            if (k < 0 || GapAfter[k]) return false;
            int j = k + 1;
            double ds = S[j] - S[k];
            double f = ds > 1e-12 ? (s - S[k]) / ds : 0;
            f = f < 0 ? 0 : f > 1 ? 1 : f;
            double xa = X[k] + Ux[k] * off, za = Z[k] + Uz[k] * off, xb = X[j] + Ux[j] * off, zb = Z[j] + Uz[j] * off;
            x = xa + (xb - xa) * f;
            z = za + (zb - za) * f;
            float ya = HeightAcross(k, off), yb = HeightAcross(j, off);
            y = ya + (yb - ya) * f;
            return true;
        }
    }

    /// <summary>
    /// The W2 road ribbons (detail pass): every drawn piece swept along its smoothed centreline
    /// (<see cref="RoadCentreline"/>) at the heights of <see cref="RoadGrade"/>, with the full cross-section of
    /// <see cref="RoadWidthModel"/>: the carriageway (lighter crown on asphalt and concrete, AO toward kerbs), barrier kerbs
    /// with a rounded nose and raised paver footpaths where the profile has them, the half median of a dual carriageway on
    /// its offside, shoulders, and a rounded skirt (an embankment where the road stands high) elsewhere, so the road reads
    /// as a smooth cartoon slab instead of a flat ribbon. Heritage lanes take their stone or brick paving
    /// (<see cref="RoadLayout.Paving"/>). Rows sit on every arc point, every junction cut and every seam; on straights they
    /// are only as dense as the profile needs (at most <see cref="MaxRowSpacingM"/> apart, within
    /// <see cref="RowToleranceM"/> of the true surface between them). Ribbons stop at junction caps and ring entries
    /// (<see cref="RoadLayout.Cuts"/>); a cut end on a tile border lies on the border line so both tiles meet vertex for
    /// vertex. Tight inner curves pinch the inner edge to 92 % of the arc radius so nothing folds. Elevated pieces draw only
    /// the deck surface and its footpaths (the bridges package draws slab, kerbs and railings); lowered sections get
    /// retaining walls. Kerbed runs are closed by end faces. UV0 = (material channel, baked AO). Thread-safe for distinct
    /// meshes.
    /// </summary>
    internal static class RibbonMesher
    {
        /// <summary>Longest straight run between two rows.</summary>
        public const double MaxRowSpacingM = 24.0;

        /// <summary>Largest height error of the linear surface between rows on a straight (checked every station).</summary>
        public const float RowToleranceM = 0.05f;

        /// <summary>Largest width error of the linear edges between rows on a straight.</summary>
        public const float WidthToleranceM = 0.04f;

        private sealed class Scratch
        {
            public double[] S = new double[128], X = new double[128], Z = new double[128], Ux = new double[128], Uz = new double[128];
            public double[] Tx = new double[128], Tz = new double[128];
            public float[] K = new float[128];
            public bool[] Gap = new bool[128], Must = new bool[128];
            public int Count;
            public readonly RoadRow Row = new RoadRow(), Left = new RoadRow(), Right = new RoadRow();
            public readonly RibbonFrames Frames = new RibbonFrames();

            public void Add(double s, double x, double z, double ux, double uz, double tx, double tz, float k, bool must)
            {
                if (Count == S.Length)
                {
                    int c = Count * 2;
                    Array.Resize(ref S, c);
                    Array.Resize(ref X, c);
                    Array.Resize(ref Z, c);
                    Array.Resize(ref Ux, c);
                    Array.Resize(ref Uz, c);
                    Array.Resize(ref Tx, c);
                    Array.Resize(ref Tz, c);
                    Array.Resize(ref K, c);
                    Array.Resize(ref Gap, c);
                    Array.Resize(ref Must, c);
                }
                S[Count] = s;
                X[Count] = x;
                Z[Count] = z;
                Ux[Count] = ux;
                Uz[Count] = uz;
                Tx[Count] = tx;
                Tz[Count] = tz;
                K[Count] = k;
                Gap[Count] = false;
                Must[Count] = must;
                Count++;
            }
        }

        [ThreadStatic] private static Scratch _scratch;
        [ThreadStatic] private static RibbonFrames _markFrames;

        private const double MinCutSine = 1.0 / 3.0;

        /// <summary>Per-piece drawing parameters.</summary>
        private struct Style
        {
            public RoadPaving Paving;
            public uint Crown, Paver, MedianTop;
            public MaterialChannel MedianCh;
            public float Median;
            public bool Dual, Detail, Absolute, Synthetic, Trail, Cross;
        }

        /// <summary>Segment side treatments.</summary>
        private struct Kinds
        {
            public RoadSweep.Side L, R;
            public bool Deck;

            /// <summary>Shoulder width where a side is <see cref="RoadSweep.Side.Shoulder"/>.</summary>
            public float Shoulder;

            public bool Same(in Kinds o)
            {
                return L == o.L && R == o.R && Deck == o.Deck && Shoulder == o.Shoulder;
            }
        }

        /// <summary>Mesh one piece; false when it draws nothing.</summary>
        public static bool Piece(TileData t, int ri, RoadLayout layout, RoadGrade grade, RoadOptions o, MeshData m)
        {
            if (!RoadMesher.IsDrawn(t, ri, o) || !layout.DrawsRibbon(ri) || !grade.Has(ri)) return false;
            Scratch sc = _scratch ?? (_scratch = new Scratch());
            RibbonFrames f = sc.Frames;
            if (!BuildFrames(t, ri, layout, grade, o, f, sc)) return false;
            RoadRecord r = t.Roads[ri];
            RoadAttrRecord a = layout.Attrs[ri];
            var st = new Style
            {
                Paving = layout.Paving[ri], Dual = a.Has(RoadAttrFlags.Dual), Detail = o.Detail, Absolute = grade.IsAbsolute(ri), Cross = o.CrossSections,
                Synthetic = grade.IsSyntheticDeck(ri), Trail = RoadWidthModel.IsFootClass(r.RoadClass),
                Paver = RoadMesher.FootpathRgba(r),
            };
            st.Median = st.Dual ? Math.Max(RoadWidthModel.MinMedianM, a.MedianCm / 100f) : 0f;
            st.Crown = st.Paving.LightCrown ? MeshColor.Lighten(st.Paving.Rgba, o.CentreLighten) : st.Paving.Rgba;
            st.MedianTop = st.Median >= 1.6f ? RoadStyle.IslandGrass : RoadMaterials.MedianConcrete;
            st.MedianCh = st.Median >= 1.6f ? MaterialChannel.Grass : MaterialChannel.Concrete;
            int prevBase = -1;
            var prevKinds = new Kinds();
            int nL = 0, nR = 0;
            bool any = false;
            for (int k = 0; k + 1 < f.Count; k++)
            {
                if (f.GapAfter[k])
                {
                    if (prevBase >= 0) CloseRun(m, prevBase, nL, sc.Row.Count, nR, prevKinds, f, k, true);
                    prevBase = -1;
                    continue;
                }
                int j = k + 1;
                Kinds kinds = SegmentKinds(layout, grade, ri, f, k, st);
                if (prevBase < 0 || !kinds.Same(prevKinds))
                {
                    if (prevBase >= 0) CloseRun(m, prevBase, nL, sc.Row.Count, nR, prevKinds, f, k, true);
                    BuildRow(f, k, kinds, st, grade, o, sc, out nL, out nR);
                    prevBase = RoadSweep.Emit(m, sc.Row);
                    CloseRun(m, prevBase, nL, sc.Row.Count, nR, kinds, f, k, false);
                }
                BuildRow(f, j, kinds, st, grade, o, sc, out nL, out nR);
                int cur = RoadSweep.Emit(m, sc.Row);
                RoadSweep.Join(m, prevBase, cur, sc.Row);
                prevBase = cur;
                prevKinds = kinds;
                any = true;
            }
            if (prevBase >= 0) CloseRun(m, prevBase, nL, sc.Row.Count, nR, prevKinds, f, f.Count - 1, true);
            if (any && !st.Absolute) EndCaps(t, ri, layout, grade, f, st, m);
            return any;
        }

        /// <summary>The frames of a piece for the markings (thread-static; valid until the next call on this thread).</summary>
        internal static RibbonFrames FramesFor(TileData t, int ri, RoadLayout layout, RoadGrade grade, RoadOptions o)
        {
            if (!RoadMesher.IsDrawn(t, ri, o) || !layout.DrawsRibbon(ri) || !grade.Has(ri)) return null;
            Scratch sc = _scratch ?? (_scratch = new Scratch());
            RibbonFrames f = _markFrames ?? (_markFrames = new RibbonFrames());
            return BuildFrames(t, ri, layout, grade, o, f, sc) ? f : null;
        }

        // -------------------------------------------------------------------------------------------------------
        // Frames
        // -------------------------------------------------------------------------------------------------------

        /// <summary>
        /// The rows of a piece: candidates are every centreline point (arcs and straights), every junction or ring cut and
        /// the tile-border ends (turned onto the border line); arc points, cuts, gap boundaries and ends are always kept,
        /// straight points only where the linear surface between kept rows would leave the profile or the width profile by
        /// more than the tolerances, or the run would exceed <see cref="MaxRowSpacingM"/>.
        /// </summary>
        private static bool BuildFrames(TileData t, int ri, RoadLayout layout, RoadGrade grade, RoadOptions o, RibbonFrames f, Scratch sc)
        {
            RoadCentreline c = layout.Centres[ri];
            if (c == null || c.Count < 2) return false;
            Candidates(t, ri, layout, c, sc);
            if (sc.Count < 2) return false;
            RoadWidthProfile prof = layout.Profiles[ri];
            bool dual = layout.Attrs[ri].Has(RoadAttrFlags.Dual);
            // Columns of the carriageway (piece constants, so every row joins).
            bool crown, aoL, aoR;
            ColumnFlags(t, ri, layout, o, out crown, out aoL, out aoR);
            f.Cols = 2 + (crown ? 1 : 0) + (aoL ? 1 : 0) + (aoR ? 1 : 0);
            f.AoLeftCol = aoL ? 1 : -1;
            f.CrownCol = crown ? (aoL ? 2 : 1) : -1;
            f.AoRightCol = aoR ? f.Cols - 2 : -1;
            // Keep decisions.
            int n = sc.Count;
            f.Ensure(n);
            f.Count = 0;
            int last = 0;
            double lod = o.Detail ? 1.0 : LowDetailToleranceScale;
            Keep(f, sc, 0, layout, grade, ri, prof, dual);
            for (int j = 1; j < n; j++)
            {
                bool must = sc.Must[j] || j == n - 1 || sc.Gap[j - 1] || j + 1 < n && sc.Gap[j] != sc.Gap[j - 1];
                if (!must && j + 1 < n && !sc.Gap[j] && CanSkip(sc, last, j + 1, layout, grade, ri, prof, dual, lod)) continue;
                Keep(f, sc, j, layout, grade, ri, prof, dual);
                last = j;
            }
            // Gap flags per kept segment: the candidates between two kept rows share one gap state.
            for (int k = 0; k + 1 < f.Count; k++) f.GapAfter[k] = layout.InGap(ri, 0.5 * (f.S[k] + f.S[k + 1]));
            return f.Count >= 2;
        }

        /// <summary>Which carriageway columns a piece's rows carry besides the two edges: the crown (asphalt and concrete,
        /// or any street at least 5.5 m wide) and the AO columns beside kerbs (detail only).</summary>
        /// <summary>Streets whose way-level width (<see cref="RoadWidthModel.BorderWidthM"/>) is at least this get a crown
        /// column (lighter, cambered centre) and, with <see cref="RoadOptions.Detail"/>, gutter AO columns near both
        /// edges.</summary>
        public const float CrownMinWidthM = 5.9f;

        private static void ColumnFlags(TileData t, int ri, RoadLayout layout, RoadOptions o, out bool crown, out bool aoL, out bool aoR)
        {
            // From the way alone (class and width tag, never this tile's corridor or area), so both pieces of a way cut
            // at a tile border draw the same columns.
            RoadRecord r = t.Roads[ri];
            bool wide = !RoadWidthModel.IsFootClass(r.RoadClass) && RoadWidthModel.BorderWidthM(r) >= CrownMinWidthM;
            crown = wide;
            aoL = aoR = o.Detail && wide;
        }

        /// <summary>
        /// The carriageway column offsets of the piece's row at a junction or ring cut (raw along <paramref name="s"/>),
        /// left to right (descending), exactly as the ribbon draws them; the cap and ring surfaces insert them along the
        /// cut edge so both meshes share every vertex there. Returns the count (at most <see cref="RibbonFrames.MaxCols"/>).
        /// </summary>
        internal static int CutColumns(TileData t, int ri, RoadLayout layout, RoadOptions o, double s, double[] offs)
        {
            RoadWidthProfile prof = layout.Profiles[ri];
            bool dual = layout.Attrs[ri].Has(RoadAttrFlags.Dual);
            bool crown, aoL, aoR;
            ColumnFlags(t, ri, layout, o, out crown, out aoL, out aoR);
            double w = prof.DrawnAt(s);
            double shift = dual ? 0.5 * (w - prof.RealM) : 0.0;
            double left = shift + 0.5 * w, right = shift - 0.5 * w;
            int n = 0;
            offs[n++] = left;
            if (aoL) offs[n++] = Math.Max(shift + 0.05, left - 0.35);
            if (crown) offs[n++] = shift;
            if (aoR) offs[n++] = Math.Min(shift - 0.05, right + 0.35);
            offs[n++] = right;
            return n;
        }

        /// <summary>The candidate sections: every centreline point plus the junction and ring cuts (which take their stored
        /// geometry), with tile-border cut ends turned onto the border line.</summary>
        private static void Candidates(TileData t, int ri, RoadLayout layout, RoadCentreline c, Scratch sc)
        {
            sc.Count = 0;
            RoadCut[] cuts = layout.Cuts[ri];
            int nextCut = 0;
            int sizeCm = (int)Math.Round(t.Tile.Size * 100.0);
            for (int k = 0; k < c.Count; k++)
            {
                double s = c.S[k];
                while (cuts != null && nextCut < cuts.Length && cuts[nextCut].S < s - 1e-6)
                {
                    RoadCut cu = cuts[nextCut++];
                    if (sc.Count > 0 && cu.S <= sc.S[sc.Count - 1] + 1e-6) continue;
                    AddCut(sc, cu);
                }
                if (cuts != null && nextCut < cuts.Length && Math.Abs(cuts[nextCut].S - s) <= 1e-6)
                {
                    AddCut(sc, cuts[nextCut++]);
                    continue;
                }
                if (sc.Count > 0 && s <= sc.S[sc.Count - 1] + 1e-9) continue;
                double tx = c.Tx[k], tz = c.Tz[k];
                double ux = -tz, uz = tx;
                bool end = k == 0 && c.StartCut || k == c.Count - 1 && c.EndCut;
                bool must = k == 0 || k == c.Count - 1;
                if (end)
                {
                    RoadRecord r = t.Roads[ri];
                    int pi = k == 0 ? 1 : r.PointCount - 2;
                    int border = BorderOf(r.Points[2 * pi], r.Points[2 * pi + 1], sizeCm);
                    if (border >= 0)
                    {
                        // Cut end: the section lies on the border line, where both tiles' ribbons meet.
                        double ex = border == 0 ? 0 : 1, ez = border == 0 ? 1 : 0;
                        double crs = ex * tz - ez * tx;
                        if (Math.Abs(crs) < MinCutSine) crs = crs >= 0 ? MinCutSine : -MinCutSine;
                        ux = -ex / crs;
                        uz = -ez / crs;
                    }
                }
                sc.Add(s, c.X[k], c.Z[k], ux, uz, tx, tz, c.Curv[k], must);
            }
            while (cuts != null && nextCut < cuts.Length)
            {
                RoadCut cu = cuts[nextCut++];
                if (sc.Count > 0 && cu.S <= sc.S[sc.Count - 1] + 1e-6) continue;
                AddCut(sc, cu);
            }
            for (int k = 0; k + 1 < sc.Count; k++) sc.Gap[k] = layout.InGap(ri, 0.5 * (sc.S[k] + sc.S[k + 1]));
            if (sc.Count > 0) sc.Gap[sc.Count - 1] = false;
        }

        private static void AddCut(Scratch sc, in RoadCut cu)
        {
            sc.Add(cu.S, cu.CX, cu.CZ, cu.UX, cu.UZ, cu.UZ, -cu.UX, 0f, true);
        }

        private static int BorderOf(int xCm, int zCm, int sizeCm)
        {
            if (xCm == 0 || xCm == sizeCm) return 0;
            if (zCm == 0 || zCm == sizeCm) return 1;
            return -1;
        }

        /// <summary>Largest turn of the section direction between two rows.</summary>
        public const double MaxRowTurnDeg = 15.0;

        /// <summary>With <see cref="RoadOptions.Detail"/> off (the low tier): the row tolerances (plan, surface, width)
        /// times this, the row turn up to 30 degrees and the row spacing up to 1.5 times.</summary>
        public const double LowDetailToleranceScale = 3.0;

        /// <summary>Largest plan error of the quads between two rows at any skipped candidate (centre and both edges).</summary>
        public const double PlanToleranceM = 0.05;

        /// <summary>
        /// True when the rows at candidates i and e can stand for everything between them: no junction gap, at most
        /// <see cref="MaxRowSpacingM"/> apart, the section turning at most <see cref="MaxRowTurnDeg"/>, every skipped
        /// candidate's centre and edges within <see cref="PlanToleranceM"/> of the quads between the two rows (so arcs and
        /// runs of small fillets thin out to what the curve needs), and the linear surface and edges within the tolerances
        /// at every station.
        /// </summary>
        private static bool CanSkip(Scratch sc, int i, int e, RoadLayout layout, RoadGrade grade, int ri, RoadWidthProfile prof, bool dual, double lod)
        {
            double si = sc.S[i], se = sc.S[e];
            if (se - si > MaxRowSpacingM * Math.Min(lod, 1.5) || sc.Gap[i]) return false;
            double cosTurn = Math.Cos(Math.Min(MaxRowTurnDeg * lod, 30.0) * Math.PI / 180.0);
            double planTol = PlanToleranceM * lod;
            float rowTol = (float)(RowToleranceM * lod), widthTol = (float)(WidthToleranceM * lod);
            if (sc.Tx[i] * sc.Tx[e] + sc.Tz[i] * sc.Tz[e] < cosTurn) return false;
            float wi = prof.DrawnAt(si), we = prof.DrawnAt(se);
            double shi = dual ? 0.5 * (wi - prof.RealM) : 0, she = dual ? 0.5 * (we - prof.RealM) : 0;
            double len = se - si;
            for (int k = i + 1; k < e; k++)
            {
                if (sc.Gap[k - 1]) return false;
                if (sc.Tx[i] * sc.Tx[k] + sc.Tz[i] * sc.Tz[k] < cosTurn) return false;
                double f = len > 1e-9 ? (sc.S[k] - si) / len : 0;
                float wk = prof.DrawnAt(sc.S[k]);
                double shk = dual ? 0.5 * (wk - prof.RealM) : 0;
                for (int q = -1; q <= 1; q++)
                {
                    double oi = shi + 0.5 * wi * q, oe = she + 0.5 * we * q, ok = shk + 0.5 * wk * q;
                    double ax = sc.X[i] + sc.Ux[i] * oi, az = sc.Z[i] + sc.Uz[i] * oi;
                    double bx = sc.X[e] + sc.Ux[e] * oe, bz = sc.Z[e] + sc.Uz[e] * oe;
                    double px = sc.X[k] + sc.Ux[k] * ok, pz = sc.Z[k] + sc.Uz[k] * ok;
                    double lx = ax + (bx - ax) * f - px, lz = az + (bz - az) * f - pz;
                    if (lx * lx + lz * lz > planTol * planTol) return false;
                }
            }
            // Widths, footpaths and the surface along the run.
            float fli = prof.Sample(prof.FootLeft, si), fle = prof.Sample(prof.FootLeft, se);
            float fri = prof.Sample(prof.FootRight, si), fre = prof.Sample(prof.FootRight, se);
            int steps = Math.Max(1, (int)Math.Ceiling(len / RoadGrade.StationM));
            RoadGrade.Row gi = grade.RowAt(ri, si), ge = grade.RowAt(ri, se);
            float yli = grade.Y(gi, shi + 0.5 * wi, sc.X[i] + sc.Ux[i] * (shi + 0.5 * wi), sc.Z[i] + sc.Uz[i] * (shi + 0.5 * wi));
            float yci = grade.Y(gi, shi, sc.X[i] + sc.Ux[i] * shi, sc.Z[i] + sc.Uz[i] * shi);
            float yri = grade.Y(gi, shi - 0.5 * wi, sc.X[i] + sc.Ux[i] * (shi - 0.5 * wi), sc.Z[i] + sc.Uz[i] * (shi - 0.5 * wi));
            float yle = grade.Y(ge, she + 0.5 * we, sc.X[e] + sc.Ux[e] * (she + 0.5 * we), sc.Z[e] + sc.Uz[e] * (she + 0.5 * we));
            float yce = grade.Y(ge, she, sc.X[e] + sc.Ux[e] * she, sc.Z[e] + sc.Uz[e] * she);
            float yre = grade.Y(ge, she - 0.5 * we, sc.X[e] + sc.Ux[e] * (she - 0.5 * we), sc.Z[e] + sc.Uz[e] * (she - 0.5 * we));
            RoadCentreline c = layout.Centres[ri];
            for (int q = 1; q < steps; q++)
            {
                double fq = (double)q / steps, s = si + len * fq;
                float w = prof.DrawnAt(s);
                if (Math.Abs(w - (wi + (we - wi) * fq)) > widthTol) return false;
                float fl = prof.Sample(prof.FootLeft, s), fr = prof.Sample(prof.FootRight, s);
                if ((fl > 0f) != (fli > 0f) || (fl > 0f) != (fle > 0f) || (fr > 0f) != (fri > 0f) || (fr > 0f) != (fre > 0f)) return false;
                if (Math.Abs(fl - (fli + (fle - fli) * fq)) > widthTol || Math.Abs(fr - (fri + (fre - fri) * fq)) > widthTol) return false;
                double cx, cz, tx, tz;
                c.At(s, out cx, out cz, out tx, out tz);
                double ux = -tz, uz = tx;
                double sh = dual ? 0.5 * (w - prof.RealM) : 0;
                RoadGrade.Row g = grade.RowAt(ri, s);
                if (g.DeckW > 0f && g.DeckW < 1f) return false; // a ramp off a deck: keep its rows
                float yl = grade.Y(g, sh + 0.5 * w, cx + ux * (sh + 0.5 * w), cz + uz * (sh + 0.5 * w));
                float yc = grade.Y(g, sh, cx + ux * sh, cz + uz * sh);
                float yr = grade.Y(g, sh - 0.5 * w, cx + ux * (sh - 0.5 * w), cz + uz * (sh - 0.5 * w));
                float t = (float)fq;
                if (Math.Abs(yl - (yli + (yle - yli) * t)) > rowTol || Math.Abs(yc - (yci + (yce - yci) * t)) > rowTol ||
                    Math.Abs(yr - (yri + (yre - yri) * t)) > rowTol) return false;
            }
            return true;
        }

        /// <summary>Append candidate j as a row: edges (pinched on tight arcs), footpaths, grade, normal and column heights.</summary>
        private static void Keep(RibbonFrames f, Scratch sc, int j, RoadLayout layout, RoadGrade grade, int ri, RoadWidthProfile prof, bool dual)
        {
            int k = f.Count++;
            double s = sc.S[j];
            f.S[k] = s;
            f.X[k] = sc.X[j];
            f.Z[k] = sc.Z[j];
            f.Ux[k] = sc.Ux[j];
            f.Uz[k] = sc.Uz[j];
            f.Tx[k] = sc.Tx[j];
            f.Tz[k] = sc.Tz[j];
            f.K[k] = sc.K[j];
            f.GapAfter[k] = false;
            double w = prof.DrawnAt(s);
            double shift = dual ? 0.5 * (w - prof.RealM) : 0.0;
            double left = shift + 0.5 * w, right = shift - 0.5 * w;
            float footL = prof.Sample(prof.FootLeft, s), footR = prof.Sample(prof.FootRight, s);
            // Pinch the inside of tight arcs so the ribbon never folds over itself.
            float kap = sc.K[j];
            if (kap > 1e-6f)
            {
                double lim = 0.92 / kap;
                if (left > lim) left = lim;
                if (left + footL + 0.3 > lim) footL = (float)Math.Max(0, lim - left - 0.3);
            }
            else if (kap < -1e-6f)
            {
                double lim = 0.92 / -kap;
                if (-right > lim) right = -lim;
                if (-right + footR + 0.3 > lim) footR = (float)Math.Max(0, lim + right - 0.3);
            }
            if (shift > left - 0.3) shift = 0.5 * (left + right);
            f.Left[k] = left;
            f.Right[k] = right;
            f.Shift[k] = shift;
            f.FootL[k] = footL;
            f.FootR[k] = footR;
            RoadGrade.Row g = grade.RowAt(ri, s);
            f.Grade[k] = g;
            float nx, ny, nz;
            grade.Normal(g, f.Tx[k], f.Tz[k], f.X[k] + f.Ux[k] * shift, f.Z[k] + f.Uz[k] * shift, out nx, out ny, out nz);
            f.Nx[k] = nx;
            f.Ny[k] = ny;
            f.Nz[k] = nz;
            int b = k * RibbonFrames.MaxCols, cc = 0;
            f.ColOff[b + cc++] = left;
            if (f.AoLeftCol >= 0) f.ColOff[b + cc++] = Math.Max(shift + 0.05, left - 0.35);
            if (f.CrownCol >= 0) f.ColOff[b + cc++] = shift;
            if (f.AoRightCol >= 0) f.ColOff[b + cc++] = Math.Min(shift - 0.05, right + 0.35);
            f.ColOff[b + cc++] = right;
            for (int q = 0; q < cc; q++)
            {
                double off = f.ColOff[b + q];
                f.ColY[b + q] = grade.Y(g, off, f.X[k] + f.Ux[k] * off, f.Z[k] + f.Uz[k] * off);
            }
        }

        // -------------------------------------------------------------------------------------------------------
        // Rows
        // -------------------------------------------------------------------------------------------------------

        private static Kinds SegmentKinds(RoadLayout layout, RoadGrade grade, int ri, RibbonFrames f, int k, in Style st)
        {
            RoadWidthProfile prof = layout.Profiles[ri];
            double sm = 0.5 * (f.S[k] + f.S[k + 1]);
            var kd = new Kinds();
            bool deck = st.Absolute || grade.OnDeck(ri, sm);
            kd.Deck = deck;
            if (!st.Cross)
            {
                kd.L = kd.R = deck && !st.Synthetic ? RoadSweep.Side.None : RoadSweep.Side.Skirt;
                return kd;
            }
            bool footL = prof.Sample(prof.FootLeft, sm) > 0f && (f.FootL[k] > 0f || f.FootL[k + 1] > 0f);
            bool footR = !st.Dual && prof.Sample(prof.FootRight, sm) > 0f && (f.FootR[k] > 0f || f.FootR[k + 1] > 0f);
            if (deck && !st.Synthetic)
            {
                kd.L = footL ? RoadSweep.Side.Footpath : RoadSweep.Side.None;
                kd.R = st.Dual ? RoadSweep.Side.Median : footR ? RoadSweep.Side.Footpath : RoadSweep.Side.None;
                return kd;
            }
            float shoulder = layout.ShoulderAt(ri, sm);
            kd.L = footL ? RoadSweep.Side.Footpath : shoulder > 0 && !st.Dual ? RoadSweep.Side.Shoulder : RoadSweep.Side.Skirt;
            kd.R = st.Dual ? RoadSweep.Side.Median : footR ? RoadSweep.Side.Footpath : shoulder > 0 ? RoadSweep.Side.Shoulder : RoadSweep.Side.Skirt;
            if (kd.L == RoadSweep.Side.Shoulder || kd.R == RoadSweep.Side.Shoulder) kd.Shoulder = shoulder;
            if (!deck && grade.IsLowered(ri))
            {
                // Retaining walls where the lowered surface is well under the terrain at the edge.
                if (Lowered(grade, f, k, true) || Lowered(grade, f, k + 1, true)) kd.L = RoadSweep.Side.Wall;
                if (Lowered(grade, f, k, false) || Lowered(grade, f, k + 1, false)) kd.R = RoadSweep.Side.Wall;
            }
            return kd;
        }

        private static bool Lowered(RoadGrade grade, RibbonFrames f, int k, bool left)
        {
            int b = k * RibbonFrames.MaxCols + (left ? 0 : f.Cols - 1);
            double off = f.ColOff[b];
            float ter = grade.Terrain(f.X[k] + f.Ux[k] * off, f.Z[k] + f.Uz[k] * off);
            return f.ColY[b] < ter - 0.25f;
        }

        /// <summary>One full cross-section row at frame k (left side reversed, carriageway columns, right side).</summary>
        private static void BuildRow(RibbonFrames f, int k, in Kinds kd, in Style st, RoadGrade grade, RoadOptions o, Scratch sc, out int nL, out int nR)
        {
            RoadRow row = sc.Row;
            row.Clear();
            int b = k * RibbonFrames.MaxCols;
            double cx = f.X[k], cz = f.Z[k], ux = f.Ux[k], uz = f.Uz[k];
            // Unit lateral (shading) direction: perpendicular to the tangent.
            double lx = -f.Tz[k], lz = f.Tx[k];
            float nx = f.Nx[k], ny = f.Ny[k], nz = f.Nz[k];
            bool kerbL = kd.L == RoadSweep.Side.Footpath || kd.L == RoadSweep.Side.Median;
            bool kerbR = kd.R == RoadSweep.Side.Footpath || kd.R == RoadSweep.Side.Median;
            float aoL = kerbL ? 0.72f : kd.L == RoadSweep.Side.Wall ? 0.7f : 0.92f, aoR = kerbR ? 0.72f : kd.R == RoadSweep.Side.Wall ? 0.7f : 0.92f;
            uint edge = st.Paving.Rgba;
            MaterialChannel ch = st.Paving.Channel;
            double left = f.ColOff[b], right = f.ColOff[b + f.Cols - 1];
            double ex = cx + ux * left, ez = cz + uz * left;
            double footL = kd.L == RoadSweep.Side.Footpath ? Math.Max(f.FootL[k], RoadWidthModel.MinFootpathM) : kd.L == RoadSweep.Side.Shoulder ? kd.Shoulder : 0.0;
            RoadSweep.BuildSide(sc.Left, kd.L, st.Detail, ex, ez, f.ColY[b], ux, uz, lx, lz, footL, edge, ch, aoL, nx, ny, nz, st.Paver,
                                MaterialChannel.Flagstone, grade, o.KerbHeightM, o.MedianHeightM, kd.Deck);
            row.AppendReversed(sc.Left);
            nL = sc.Left.Count;
            for (int c = 1; c + 1 < f.Cols; c++)
            {
                double off = f.ColOff[b + c];
                bool crown = c == f.CrownCol;
                row.Add(cx + ux * off, f.ColY[b + c], cz + uz * off, nx, ny, nz, crown ? st.Crown : edge, ch, 1f);
            }
            double rx = cx + ux * right, rz = cz + uz * right;
            double outW = kd.R == RoadSweep.Side.Median ? 0.5 * st.Median : kd.R == RoadSweep.Side.Shoulder ? kd.Shoulder
                : kd.R == RoadSweep.Side.Footpath ? Math.Max(f.FootR[k], RoadWidthModel.MinFootpathM) : 0.0;
            RoadSweep.BuildSide(sc.Right, kd.R, st.Detail, rx, rz, f.ColY[b + f.Cols - 1], -ux, -uz, -lx, -lz, outW, edge, ch, aoR, nx, ny, nz,
                                kd.R == RoadSweep.Side.Median ? st.MedianTop : st.Paver, kd.R == RoadSweep.Side.Median ? st.MedianCh : MaterialChannel.Flagstone,
                                grade, o.KerbHeightM, o.MedianHeightM, kd.Deck);
            row.Append(sc.Right, 0);
            nR = sc.Right.Count;
        }

        /// <summary>End faces of the raised sides (footpaths, medians, walls) at the start (<paramref name="end"/> false) or
        /// end of a run, on the emitted row at <paramref name="baseIdx"/>.</summary>
        private static void CloseRun(MeshData m, int baseIdx, int nL, int count, int nR, in Kinds kd, RibbonFrames f, int k, bool end)
        {
            double dx = f.Tx[k] * (end ? 1 : -1), dz = f.Tz[k] * (end ? 1 : -1);
            if (Raised(kd.L)) RoadSweep.EndFace(m, baseIdx, 0, nL - 1, dx, dz);
            if (Raised(kd.R)) RoadSweep.EndFace(m, baseIdx, count - nR, count - 1, dx, dz);
        }

        private static bool Raised(RoadSweep.Side s)
        {
            return s == RoadSweep.Side.Footpath || s == RoadSweep.Side.Median || s == RoadSweep.Side.Wall;
        }

        /// <summary>
        /// Free piece ends (not a tile cut, a knee, a cap or a ring entry): a rounded skirt across the end so the slab does
        /// not show a gap under its last section.
        /// </summary>
        private static void EndCaps(TileData t, int ri, RoadLayout layout, RoadGrade grade, RibbonFrames f, in Style st, MeshData m)
        {
            RoadCentreline c = layout.Centres[ri];
            for (int e = 0; e < 2; e++)
            {
                bool start = e == 0;
                if (start ? c.StartCut : c.EndCut) continue;
                if (layout.Knees[2 * ri + e].Has) continue;
                int k = start ? 0 : f.Count - 1;
                if (start ? f.GapAfter[0] : f.GapAfter[f.Count - 2]) continue;
                double ox = start ? -f.Tx[k] : f.Tx[k], oz = start ? -f.Tz[k] : f.Tz[k];
                int b = k * RibbonFrames.MaxCols;
                int top = m.VertexCount;
                for (int q = 0; q < f.Cols; q++)
                {
                    double off = f.ColOff[b + q];
                    double x = f.X[k] + f.Ux[k] * off, z = f.Z[k] + f.Uz[k] * off;
                    m.AddVertex((float)x, f.ColY[b + q], (float)z, f.Nx[k], f.Ny[k], f.Nz[k], st.Paving.Rgba, RoadMaterials.U(st.Paving.Channel), 0.92f);
                }
                int foot = m.VertexCount;
                float fnx, fny, fnz;
                RoadSweep.Normalise((float)ox * 0.8f, 0.6f, (float)oz * 0.8f, out fnx, out fny, out fnz);
                for (int q = 0; q < f.Cols; q++)
                {
                    double off = f.ColOff[b + q];
                    double x = f.X[k] + f.Ux[k] * off + ox * 0.28, z = f.Z[k] + f.Uz[k] * off + oz * 0.28;
                    float y = Math.Min(f.ColY[b + q] - RoadSweep.SkirtDropM, grade.Terrain(x, z) - 0.06f);
                    m.AddVertex((float)x, y, (float)z, fnx, fny, fnz, st.Paving.Rgba, RoadMaterials.U(st.Paving.Channel), 0.55f);
                }
                for (int q = 0; q + 1 < f.Cols; q++) RoadSweep.Quad(m, top + q, foot + q, foot + q + 1, top + q + 1);
            }
        }
    }
}
