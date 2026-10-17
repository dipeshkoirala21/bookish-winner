using System;
using System.Collections.Generic;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing.Bridges
{
    /// <summary>
    /// The collision solids of a tile's bridges, flyovers, foot overbridges, stairs and underpass trenches, taken from the
    /// same <see cref="BridgeLayout"/> and the same cross-sections <see cref="BridgeMesher"/> draws, so what stops a
    /// walker or a rider is exactly what the player sees (owner playtest #2 and #15, docs/W2_PLAYTEST_1.md):
    /// <list type="bullet">
    /// <item><b>Railings</b> (and the median barrier between twin decks) along each side's drawn railing runs, on the
    /// railing base outside the walkway: they open where the drawn railing opens (a stair or a branch leaves, a street
    /// meets the deck), never stand on a walkway or across a stair landing, and hold whoever walks or rides on the deck.</item>
    /// <item><b>End railings</b> across landing ends.</item>
    /// <item><b>Stair handrails</b> along both sides of every flight.</item>
    /// <item><b>Supports</b> under the deck: abutments and their wing walls, bents, piers, overbridge columns and the side
    /// walls of fill ramps, up to the deck soffit, so nobody walks into a bank seat or a ramp from beside it.</item>
    /// <item><b>Trench walls</b> along the drawn retaining walls of every lowered underpass (<see cref="BridgeSpan.RailRunsL"/>
    /// and <see cref="BridgeSpan.RailRunsR"/> of a trench), which open wherever another road's corridor lies (a twin
    /// carriageway beside it, a street joining).</item>
    /// </list>
    /// Nothing reaches below the soffit of a deck over another road: a road under a bridge or flyover stays open.
    /// Coordinates are tile-local metres with absolute heights. Deterministic; built once per tile on a worker.
    /// </summary>
    public sealed class BridgeColliders
    {
        /// <summary>A thin wall from (X0, Z0) to (X1, Z1), <see cref="HalfThickness"/> to each side, from
        /// <see cref="Bottom"/> to <see cref="Top"/>.</summary>
        public struct Wall
        {
            public double X0, Z0, X1, Z1;
            public float HalfThickness, Bottom, Top;
        }

        /// <summary>An oriented block: centre, half extents along its axis (<see cref="Yaw"/>, counter-clockwise from +X
        /// seen from above) and across it.</summary>
        public struct Block
        {
            public double CX, CZ, HalfAlong, HalfAcross, Yaw;
            public float Bottom, Top;
        }

        /// <summary>A vertical round column.</summary>
        public struct Column
        {
            public double CX, CZ;
            public float Radius, Bottom, Top;
        }

        /// <summary>Railing pieces follow the deck at most this far in height (a piece spans its lowest bottom to its
        /// highest top).</summary>
        public const float MaxPieceRiseM = 0.25f;

        /// <summary>Longest straight piece.</summary>
        public const double MaxPieceM = 24.0;

        /// <summary>A railing reaches this far below the walkway, into the slab (never under it).</summary>
        public const float RailingBelowM = 0.4f;

        /// <summary>Stair handrails stand this high over the walking line (as drawn).</summary>
        public const float HandrailM = 0.95f;

        public readonly List<Wall> Walls = new List<Wall>();
        public readonly List<Block> Blocks = new List<Block>();
        public readonly List<Column> Columns = new List<Column>();

        /// <summary>Per road of the tile: true when its railings and retaining walls come from here (a span or trench of
        /// the layout covers it), so road-based guesses must leave it out.</summary>
        public readonly bool[] Covered;

        public int Count
        {
            get { return Walls.Count + Blocks.Count + Columns.Count; }
        }

        private readonly BridgeLayout _l;

        private BridgeColliders(BridgeLayout l)
        {
            _l = l;
            Covered = new bool[l.Tile.Roads.Count];
        }

        /// <summary>The solids of a layout (built on every call; the streamer builds it once per tile on a worker).</summary>
        public static BridgeColliders For(BridgeLayout layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            var c = new BridgeColliders(layout);
            foreach (BridgeSpan sp in layout.Spans)
            {
                c.Covered[sp.Road] = true;
                for (int i = 0; i < sp.Stairs.Count; i++) c.Handrails(sp, i);
                if (sp.Stair) continue;
                for (int side = -1; side <= 1; side += 2)
                {
                    List<double> runs = side > 0 ? sp.RailRunsL : sp.RailRunsR;
                    for (int r = 0; r + 1 < runs.Count; r += 2) c.RailingRun(sp, side, runs[r], runs[r + 1]);
                }
                c.EndRailings(sp);
                c.Supports(sp);
            }
            foreach (BridgeSpan sp in layout.Trenches)
            {
                c.Covered[sp.Road] = true;
                for (int side = -1; side <= 1; side += 2)
                {
                    List<double> runs = side > 0 ? sp.RailRunsL : sp.RailRunsR;
                    for (int r = 0; r + 1 < runs.Count; r += 2) c.TrenchRun(sp, side, runs[r], runs[r + 1]);
                }
            }
            return c;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Cross-section (as BridgeMesher.Side)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Carriageway edge c, railing inner face w and deck edge e on one side at along s (positive).</summary>
        internal static void Side(BridgeSpan sp, double s, int side, out double c, out double w, out double e)
        {
            double half = side > 0 ? sp.Lerp(sp.HalfL, s) : sp.Lerp(sp.HalfR, s);
            double walk = side > 0 ? sp.Lerp(sp.WalkL, s) : sp.Lerp(sp.WalkR, s);
            double clamp = side > 0 ? sp.Lerp(sp.ClampL, s) : sp.Lerp(sp.ClampR, s);
            c = half;
            w = Math.Min(half + walk, clamp - BridgeStyle.RailBaseM);
            e = Math.Min(half + walk + BridgeStyle.RailBaseM + BridgeStyle.OverhangM, clamp);
        }

        /// <summary>Height of the walkway over the deck surface (a raised kerb on motor decks).</summary>
        internal static float WalkTop(BridgeSpan sp)
        {
            return sp.RaisedWalk ? BridgeStyle.KerbHeightM : 0f;
        }

        /// <summary>The drawn point at signed offset u (left positive) from the centreline at along s, as the sweeps place
        /// their rings (the mitred offset vector of the path frame).</summary>
        private static void At(BridgeSpan sp, double s, double u, out double x, out double z, out float y)
        {
            double px, pz, ux, uz, nx, nz, tx, tz;
            sp.Path.Frame(s, out px, out pz, out y, out ux, out uz, out nx, out nz, out tx, out tz);
            x = px + ux * u;
            z = pz + uz * u;
        }

        private float Ground(double lx, double lz)
        {
            return BridgeStructures.Ground(_l.Tile, _l.Ground, lx, lz);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Walls along a span
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>The along positions of a run: the range ends, every path station inside and at most every
        /// <paramref name="step"/> metres.</summary>
        private static List<double> Stations(BridgeSpan sp, double a, double b, double step)
        {
            var sl = new List<double> { a };
            BridgePath p = sp.Path;
            for (int k = 0; k < p.Count; k++)
                if (p.S[k] > a + 1e-4 && p.S[k] < b - 1e-4) sl.Add(p.S[k]);
            sl.Add(b);
            sl.Sort();
            var o = new List<double>(sl.Count * 2);
            for (int i = 0; i + 1 < sl.Count; i++)
            {
                o.Add(sl[i]);
                double len = sl[i + 1] - sl[i];
                int n = (int)Math.Ceiling(len / step);
                for (int j = 1; j < n; j++) o.Add(sl[i] + len * j / n);
            }
            o.Add(b);
            return o;
        }

        /// <summary>Merges consecutive points of a wall line into straight pieces whose bottoms and tops each stay within
        /// <see cref="MaxPieceRiseM"/>; each piece spans its lowest bottom to its highest top, and the next piece starts
        /// where the last one ended.</summary>
        private sealed class WallLine
        {
            private readonly List<Wall> _out;
            private readonly float _half;
            private bool _has;
            private double _sx, _sz, _ex, _ez, _dx, _dz, _len;
            private float _eB, _eT, _loB, _hiB, _loT, _hiT;

            public WallLine(List<Wall> output, float half)
            {
                _out = output;
                _half = half;
            }

            private void Start(double x, double z, float bottom, float top)
            {
                _has = true;
                _sx = _ex = x;
                _sz = _ez = z;
                _eB = bottom;
                _eT = top;
                _dx = _dz = 0;
                _len = 0;
                _loB = _hiB = bottom;
                _loT = _hiT = top;
            }

            public void Add(double x, double z, float bottom, float top)
            {
                if (!_has)
                {
                    Start(x, z, bottom, top);
                    return;
                }
                double dx = x - _ex, dz = z - _ez, l = Math.Sqrt(dx * dx + dz * dz);
                float loB = Math.Min(_loB, bottom), hiB = Math.Max(_hiB, bottom), loT = Math.Min(_loT, top), hiT = Math.Max(_hiT, top);
                if (l < 1e-4)
                {
                    _loB = loB;
                    _hiB = hiB;
                    _loT = loT;
                    _hiT = hiT;
                    return;
                }
                dx /= l;
                dz /= l;
                bool first = _len < 1e-6;
                bool straight = first || dx * _dx + dz * _dz > 0.9986; // within about 3 degrees
                if (first || straight && hiB - loB <= MaxPieceRiseM && hiT - loT <= MaxPieceRiseM && _len + l <= MaxPieceM)
                {
                    if (first)
                    {
                        _dx = dx;
                        _dz = dz;
                    }
                    _ex = x;
                    _ez = z;
                    _eB = bottom;
                    _eT = top;
                    _len += l;
                    _loB = loB;
                    _hiB = hiB;
                    _loT = loT;
                    _hiT = hiT;
                    return;
                }
                Emit();
                Start(_ex, _ez, _eB, _eT);
                Add(x, z, bottom, top);
            }

            private void Emit()
            {
                if (_has && _len > 1e-4)
                    _out.Add(new Wall { X0 = _sx, Z0 = _sz, X1 = _ex, Z1 = _ez, HalfThickness = _half, Bottom = _loB, Top = _hiT });
            }

            public void End()
            {
                Emit();
                _has = false;
            }
        }

        /// <summary>One side's railing (or median barrier) from along a to b, on the drawn railing base.</summary>
        private void RailingRun(BridgeSpan sp, int side, double a, double b)
        {
            if (b - a < 0.05) return;
            float top = WalkTop(sp) + BridgeLayout.RailReach(sp);
            bool shared = side > 0 ? sp.SharedL : sp.SharedR;
            if (shared) top = WalkTop(sp) + 0.85f; // the jersey barrier between twin decks
            var line = new WallLine(Walls, 0.5f * BridgeStyle.RailBaseM);
            foreach (double s in Stations(sp, a, b, 1.5))
            {
                double c, w, e;
                Side(sp, s, side, out c, out w, out e);
                double x, z;
                float y;
                At(sp, s, side * (w + 0.5 * BridgeStyle.RailBaseM), out x, out z, out y);
                line.Add(x, z, y - RailingBelowM, y + top);
            }
            line.End();
        }

        /// <summary>The railing across a landing end (stairs leave it to the side), as drawn.</summary>
        private void EndRailings(BridgeSpan sp)
        {
            for (int end = 0; end < 2; end++)
            {
                if ((end == 0 ? sp.StartEnd : sp.EndEnd) != SpanEnd.Landing) continue;
                double s = end == 0 ? sp.Path.Start : sp.Path.End;
                if (!sp.HasDeck(s + (end == 0 ? 0.3 : -0.3))) continue;
                double px, pz, ux, uz, nx, nz, tx, tz;
                float y;
                sp.Path.Frame(s, out px, out pz, out y, out ux, out uz, out nx, out nz, out tx, out tz);
                int dir = end == 0 ? 1 : -1;
                double inset = 0.5 * BridgeStyle.RailBaseM * dir;
                double cl, wl, el, cr, wr, er;
                Side(sp, s, 1, out cl, out wl, out el);
                Side(sp, s, -1, out cr, out wr, out er);
                double mid = 0.5 * (wl - wr), half = 0.5 * (wl + wr) + BridgeStyle.RailBaseM;
                double cx = px + tx * inset + nx * mid, cz = pz + tz * inset + nz * mid;
                Walls.Add(new Wall
                {
                    X0 = cx - nx * half, Z0 = cz - nz * half, X1 = cx + nx * half, Z1 = cz + nz * half, HalfThickness = 0.5f * BridgeStyle.RailBaseM,
                    Bottom = y - RailingBelowM, Top = y + WalkTop(sp) + BridgeLayout.RailReach(sp),
                });
            }
        }

        /// <summary>Handrails along both sides of flight <paramref name="index"/> of <paramref name="sp"/> on its drawn
        /// handrail runs (<see cref="BridgeSpan.HandrailRuns"/>: open where another flight or a landing pad meets the side;
        /// none along a flat landing pad, whose sides the flights leave from).</summary>
        private void Handrails(BridgeSpan sp, int index)
        {
            BridgeStair st = sp.Stairs[index];
            if (st.Steps <= 0 || st.Length < 0.2) return;
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                int ri = 2 * index + (sgn > 0 ? 1 : 0);
                if (ri >= sp.HandrailRuns.Count) continue;
                List<double> runs = sp.HandrailRuns[ri];
                // On the flight's outer edge (the drawn rail stands just inside it): the full tread width stays walkable.
                double nx = -st.Dz * sgn, nz = st.Dx * sgn, off = st.HalfWidth + 0.04;
                for (int r = 0; r + 1 < runs.Count; r += 2)
                {
                    var line = new WallLine(Walls, 0.04f);
                    double a0 = runs[r], a1 = runs[r + 1];
                    int n = Math.Max(1, (int)Math.Ceiling((a1 - a0) / 0.3));
                    for (int i = 0; i <= n; i++)
                    {
                        double a = a0 + (a1 - a0) * i / n;
                        float y = st.SurfaceAt(a);
                        line.Add(st.X + st.Dx * a + nx * off, st.Z + st.Dz * a + nz * off, y - 0.3f, y + HandrailM);
                    }
                    line.End();
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Supports
        // ---------------------------------------------------------------------------------------------------------

        private void Supports(BridgeSpan sp)
        {
            // Abutments and their wing walls (BridgeMesher.Abutments).
            foreach (BridgeAbutment ab in sp.Abutments)
            {
                double px, pz, ux, uz, nx, nz, tx, tz;
                float y;
                sp.Path.Frame(ab.S, out px, out pz, out y, out ux, out uz, out nx, out nz, out tx, out tz);
                double cl, wl, el, cr, wr, er;
                Side(sp, ab.S, 1, out cl, out wl, out el);
                Side(sp, ab.S, -1, out cr, out wr, out er);
                float depth = sp.Lerp(sp.Depth, ab.S);
                double mid = 0.5 * (el - er), half = 0.5 * (el + er) + 0.2;
                float gHere = Ground(px + nx * mid, pz + nz * mid);
                float topY = y - depth;
                if (topY - gHere > 0.3f)
                {
                    Blocks.Add(new Block
                    {
                        CX = px + nx * mid + tx * ab.Dir * 0.55, CZ = pz + nz * mid + tz * ab.Dir * 0.55, HalfAlong = 0.65, HalfAcross = half + 0.12,
                        Yaw = Math.Atan2(tz, tx), Bottom = gHere - 1.2f, Top = topY,
                    });
                }
                if (!ab.Wings) continue;
                for (int side = -1; side <= 1; side += 2)
                {
                    double len = side > 0 ? ab.WingL : ab.WingR;
                    if (len < 1.0) continue;
                    double off = side > 0 ? el + 0.05 : -(er + 0.05);
                    double sx = px + nx * off, sz = pz + nz * off;
                    double dx = -tx * ab.Dir * 0.866 + nx * side * 0.5, dz = -tz * ab.Dir * 0.866 + nz * side * 0.5;
                    double ex = sx + dx * len, ez = sz + dz * len;
                    float gS = Ground(sx, sz), gE = Ground(ex, ez);
                    float topS = y - 0.05f, topE = Math.Max(gE + 0.45f, Math.Min(topS, gE + 0.45f));
                    if (topS - Math.Min(gS, gE) < 0.25f) continue;
                    // Split into pieces along the sloping coping so each blocks only up to its own top.
                    int pieces = Math.Max(1, (int)Math.Ceiling(len / 1.0));
                    for (int i = 0; i < pieces; i++)
                    {
                        double f0 = i / (double)pieces, f1 = (i + 1) / (double)pieces;
                        Walls.Add(new Wall
                        {
                            X0 = sx + (ex - sx) * f0, Z0 = sz + (ez - sz) * f0, X1 = sx + (ex - sx) * f1, Z1 = sz + (ez - sz) * f1, HalfThickness = 0.225f,
                            Bottom = Math.Min(gS, gE) - 0.8f, Top = topS + (topE - topS) * (float)f1,
                        });
                    }
                }
            }
            // Bents where an open span meets a fill ramp.
            foreach (double sb in sp.Bents)
            {
                double px, pz, ux, uz, nx, nz, tx, tz, cl, wl, el, cr, wr, er;
                float y;
                sp.Path.Frame(sb, out px, out pz, out y, out ux, out uz, out nx, out nz, out tx, out tz);
                Side(sp, sb, 1, out cl, out wl, out el);
                Side(sp, sb, -1, out cr, out wr, out er);
                double mid = 0.5 * (el - er), half = 0.5 * (el + er);
                double cx = px + nx * mid, cz = pz + nz * mid;
                float g = Ground(cx, cz), topY = y - sp.Lerp(sp.Depth, sb);
                if (topY - g < 0.3f) continue;
                Blocks.Add(new Block { CX = cx, CZ = cz, HalfAlong = 0.45, HalfAcross = half, Yaw = Math.Atan2(tz, tx), Bottom = g - 0.6f, Top = topY });
            }
            Piers(sp);
            OverbridgeColumns(sp);
            FillWalls(sp);
        }

        private void Piers(BridgeSpan sp)
        {
            foreach (double s in sp.Piers)
            {
                double px, pz, ux, uz, nx, nz, tx, tz, cl, wl, el, cr, wr, er;
                float y;
                sp.Path.Frame(s, out px, out pz, out y, out ux, out uz, out nx, out nz, out tx, out tz);
                Side(sp, s, 1, out cl, out wl, out el);
                Side(sp, s, -1, out cr, out wr, out er);
                float soffit = y - sp.Lerp(sp.Depth, s);
                double mid = 0.5 * (el - er), deckHalf = 0.5 * (el + er);
                double cx = px + nx * mid, cz = pz + nz * mid, yaw = Math.Atan2(tz, tx);
                float g = Ground(cx, cz);
                for (int k = -1; k <= 1; k += 2) g = Math.Min(g, Ground(cx + nx * k * deckHalf * 0.7, cz + nz * k * deckHalf * 0.7));
                if (soffit - g < 1.0f) continue;
                float foot = g - 1.2f;
                double gird = sp.Foot ? deckHalf : Math.Max(0.45, deckHalf - Math.Min(1.4, 0.4 * deckHalf));
                if (sp.Water || sp.Foot && !sp.Overbridge)
                {
                    double t = sp.Foot ? 0.35 : 0.6;
                    Blocks.Add(new Block { CX = cx, CZ = cz, HalfAlong = t, HalfAcross = Math.Max(t, gird * 0.9) + 0.25, Yaw = yaw, Bottom = foot, Top = soffit });
                    continue;
                }
                int cols = sp.Overbridge ? 1 : deckHalf < 4.6 ? 1 : deckHalf < 8.5 ? 2 : 3;
                double r = sp.Overbridge ? 0.3 : cols == 1 ? 0.85 : 0.65;
                double capH = sp.Overbridge ? 0.35 : 1.0;
                double capHalf = cols == 1 ? Math.Max(r + 0.3, gird + 0.2) : gird + 0.3;
                for (int i = 0; i < cols; i++)
                {
                    double off = cols == 1 ? 0 : (i - 0.5 * (cols - 1)) * (2 * (capHalf - r - 0.4) / Math.Max(1, cols - 1));
                    Columns.Add(new Column { CX = cx + nx * off, CZ = cz + nz * off, Radius = (float)r, Bottom = foot, Top = soffit - (float)capH });
                }
                Blocks.Add(new Block { CX = cx, CZ = cz, HalfAlong = r + 0.25, HalfAcross = capHalf, Yaw = yaw, Bottom = soffit - (float)capH, Top = soffit });
            }
        }

        private void OverbridgeColumns(BridgeSpan sp)
        {
            if (!sp.Overbridge) return;
            for (int end = 0; end < 2; end++)
            {
                SpanEnd e = end == 0 ? sp.StartEnd : sp.EndEnd;
                if (e != SpanEnd.Ground && e != SpanEnd.Landing) continue;
                int dir = end == 0 ? 1 : -1;
                double se = end == 0 ? sp.Path.Start : sp.Path.End;
                if (!sp.HasDeck(se + dir * 0.6)) continue;
                double cs = se + dir * 0.6;
                double cl, wl, el, cr, wr, er;
                Side(sp, se, 1, out cl, out wl, out el);
                Side(sp, se, -1, out cr, out wr, out er);
                double mid = 0.5 * (el - er), half = 0.5 * (el + er) + 0.2;
                float depth = sp.Lerp(sp.Depth, se);
                for (int k = -1; k <= 1; k += 2)
                {
                    double px, pz, ux, uz, nx, nz, tx, tz;
                    float y;
                    sp.Path.Frame(cs, out px, out pz, out y, out ux, out uz, out nx, out nz, out tx, out tz);
                    double ox = px + nx * (mid + k * (half - 0.45)), oz = pz + nz * (mid + k * (half - 0.45));
                    float g = Ground(ox, oz), yt = sp.Path.YAt(cs) - depth;
                    if (yt - g > 0.4f) Columns.Add(new Column { CX = ox, CZ = oz, Radius = 0.22f, Bottom = g - 0.4f, Top = yt });
                }
            }
        }

        /// <summary>The side walls of solid fill ramps (from the deck edge down into the ground), on each deck run.</summary>
        private void FillWalls(BridgeSpan sp)
        {
            BridgePath path = sp.Path;
            int n = sp.Count, k = 0;
            while (k < n)
            {
                bool fill = sp.Fill[k];
                int a = k;
                while (k + 1 < n && sp.Fill[k + 1] == fill) k++;
                int b = k;
                k++;
                if (!fill) continue;
                double p0 = a > 0 ? path.S[a - 1] : path.S[a], p1 = b < n - 1 ? path.S[b + 1] : path.S[b];
                for (int r = 0; r + 1 < sp.DeckRuns.Count; r += 2)
                {
                    double q0 = Math.Max(p0, sp.DeckRuns[r]), q1 = Math.Min(p1, sp.DeckRuns[r + 1]);
                    if (q1 - q0 < 0.05) continue;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var line = new WallLine(Walls, 0.06f);
                        foreach (double s in Stations(sp, q0, q1, 1.0))
                        {
                            double c, w, e;
                            Side(sp, s, side, out c, out w, out e);
                            double x, z;
                            float y;
                            At(sp, s, side * (e + 0.035), out x, out z, out y);
                            float g = Ground(x, z);
                            line.Add(x, z, Math.Min(g, y) - 0.6f, y - 0.03f);
                        }
                        line.End();
                    }
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Underpass trenches
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>One side's retaining wall of a trench from along a to b (BridgeMesher's TrenchWall: from just below
        /// the road up to 1 m over the ground behind it).</summary>
        private void TrenchRun(BridgeSpan sp, int side, double a, double b)
        {
            if (b - a < 0.05) return;
            var line = new WallLine(Walls, 0.15f);
            foreach (double s in Stations(sp, a, b, 1.0))
            {
                double c, w, e;
                Side(sp, s, side, out c, out w, out e);
                double x, z, bx, bz;
                float y, by;
                At(sp, s, side * (w + 0.15), out x, out z, out y);
                At(sp, s, side * (w + 0.3), out bx, out bz, out by);
                float g = Math.Max(y + 0.4f, Ground(bx, bz));
                line.Add(x, z, y - 0.1f, g + 1.0f);
            }
            line.End();
        }
    }
}
