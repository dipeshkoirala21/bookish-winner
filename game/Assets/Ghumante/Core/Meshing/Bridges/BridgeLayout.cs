using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.Core.Meshing.Bridges
{
    /// <summary>A lower road passing under a span: where, how far along the span its corridor reaches, and its
    /// surface height there.</summary>
    public struct BridgeCrossing
    {
        public int Road;

        /// <summary>Along the span (m) of the centreline crossing.</summary>
        public double S;

        /// <summary>Half the stretch of the span (m along) over which the deck overlaps the lower corridor.</summary>
        public double HalfAlong;

        /// <summary>Surface height of the lower road at the crossing.</summary>
        public float LowY;

        /// <summary>Tile-local plan position of the crossing.</summary>
        public double X, Z;

        /// <summary>Half width of the lower road corridor (carriageway, footpaths and a margin).</summary>
        public double LowHalfM;
    }

    /// <summary>A straight stair flight from a foot overbridge end down to the ground (with landings).</summary>
    public struct BridgeStair
    {
        /// <summary>Tile-local top of the flight (centre of its top edge) and the deck height there.</summary>
        public double X, Z;
        public float TopY, BottomY;

        /// <summary>Unit plan direction of descent.</summary>
        public double Dx, Dz;

        public double HalfWidth;

        /// <summary>Plan length of the flight including the landings.</summary>
        public double Length;

        public int Steps;
        public int Landings;
    }

    /// <summary>A lamp post along a span: along distance and side (+1 left, −1 right).</summary>
    public struct BridgeLamp
    {
        public double S;
        public int Side;
    }

    /// <summary>
    /// One elevated road piece (or a lowered underpass) of a tile as the bridge meshers and the deck index see it:
    /// a densified centreline (<see cref="Path"/>) with the absolute deck height of the structure record, per-station
    /// ground, carriageway edges (the road ribbon), walkways, deck edges and structure depth, the roads passing under
    /// it, and its supports (abutments at real ends, fill ramps, piers), lamps and stairs. Built by
    /// <see cref="BridgeLayout"/>; read-only afterwards.
    /// </summary>
    public sealed class BridgeSpan
    {
        public int Road;
        public RoadRecord Record;
        public RoadStructureKind Kind;
        public bool Foot, Overbridge, Water, Heritage, Lit;
        public RailingStyle Railing;
        public uint Steel;
        public float RailHeight;

        public readonly BridgePath Path = new BridgePath();

        /// <summary>Per station: terrain under the centreline.</summary>
        public float[] Ground;

        /// <summary>Per station: structure depth below the deck surface (thinned over lower roads).</summary>
        public float[] Depth;

        /// <summary>Per station: offsets of the carriageway edges from the centreline (left +HalfL, right −HalfR).</summary>
        public double[] HalfL, HalfR;

        /// <summary>Per station: walkway (or safety kerb) widths beyond the carriageway.</summary>
        public double[] WalkL, WalkR;

        /// <summary>Per station: solid fill between retaining walls (flyover ramps) instead of an open span.</summary>
        public bool[] Fill;

        /// <summary>Per segment (k, k + 1): a foot deck too steep to walk, drawn as steps.</summary>
        public bool[] Steps;

        /// <summary>Walkways are raised kerbs (motor roads) rather than flush with the deck (foot classes).</summary>
        public bool RaisedWalk;

        /// <summary>The side runs against a parallel deck: a median barrier replaces the railing and the deck edge
        /// stops at the middle between the two centrelines.</summary>
        public bool SharedL, SharedR;

        /// <summary>Per station: the deck edge clamp on a shared side (<see cref="BridgeLayout.NoClamp"/> elsewhere).</summary>
        public double[] ClampL, ClampR;

        /// <summary>The ends lie on a tile border (the structure continues in the neighbour).</summary>
        public bool StartCut, EndCut;

        /// <summary>Context points beyond the cut ends (for queries next to the border).</summary>
        public bool HasPre, HasPost;
        public double PreX, PreZ, PostX, PostZ;
        public float PreY, PostY;

        public readonly List<BridgeCrossing> Crossings = new List<BridgeCrossing>();
        public readonly List<double> Piers = new List<double>();

        /// <summary>Along positions of end walls where an open span meets a fill ramp.</summary>
        public readonly List<double> Bents = new List<double>();

        public readonly List<BridgeLamp> Lamps = new List<BridgeLamp>();
        public readonly List<BridgeStair> Stairs = new List<BridgeStair>();

        public int Count
        {
            get { return Path.Count; }
        }

        public double Length
        {
            get { return Path.End - Path.Start; }
        }

        /// <summary>Deck surface height of the walkway or carriageway at station k (no kerb).</summary>
        public float DeckY(int k)
        {
            return Path.Y[k];
        }

        /// <summary>Railing inner face offset on the left at station k (positive).</summary>
        public double InnerL(int k)
        {
            return Math.Min(HalfL[k] + WalkL[k], ClampL[k] - BridgeStyle.RailBaseM);
        }

        public double InnerR(int k)
        {
            return Math.Min(HalfR[k] + WalkR[k], ClampR[k] - BridgeStyle.RailBaseM);
        }

        /// <summary>Deck outer edge offset on the left at station k (positive).</summary>
        public double EdgeL(int k)
        {
            return Math.Min(HalfL[k] + WalkL[k] + BridgeStyle.RailBaseM + BridgeStyle.OverhangM, ClampL[k]);
        }

        public double EdgeR(int k)
        {
            return Math.Min(HalfR[k] + WalkR[k] + BridgeStyle.RailBaseM + BridgeStyle.OverhangM, ClampR[k]);
        }

        /// <summary>Index of the station at or just before along <paramref name="s"/>.</summary>
        public int StationAt(double s)
        {
            return Path.Segment(s);
        }

        /// <summary>Linear interpolation of a per-station array at along s.</summary>
        public double Lerp(double[] a, double s)
        {
            int k = Path.Segment(s);
            int j = Math.Min(k + 1, Path.Count - 1);
            double len = Path.S[j] - Path.S[k];
            double f = len > 1e-9 ? (s - Path.S[k]) / len : 0;
            f = f < 0 ? 0 : f > 1 ? 1 : f;
            return a[k] + (a[j] - a[k]) * f;
        }

        public float Lerp(float[] a, double s)
        {
            int k = Path.Segment(s);
            int j = Math.Min(k + 1, Path.Count - 1);
            double len = Path.S[j] - Path.S[k];
            double f = len > 1e-9 ? (s - Path.S[k]) / len : 0;
            f = f < 0 ? 0 : f > 1 ? 1 : f;
            return (float)(a[k] + (a[j] - a[k]) * f);
        }
    }

    /// <summary>
    /// The bridges, flyovers, foot overbridges and underpass trenches of one tile, shared by
    /// <see cref="BridgeMesher"/> and <see cref="BridgeDeckIndex"/> so the drawn structure and the ground queries
    /// agree. Built once per decoded tile and cached for its lifetime (<see cref="For"/>); immutable afterwards, so
    /// worker threads may share it. Deck heights come from the tile's structure records (or
    /// <see cref="BridgeStructures.Derive"/> on packs without them); widths from the tile's <see cref="RoadLayout"/>
    /// (the same widened corridor the road ribbon draws), so a span cut by a tile border meets its neighbour exactly:
    /// both tiles see the same border width and deck height there, and every world-anchored detail (piers on cut
    /// spans, posts, lamps, kerb stripes) is placed by world position, so each is drawn by exactly one tile.
    /// Deterministic.
    /// </summary>
    public sealed class BridgeLayout
    {
        /// <summary>Longest station spacing along a span (deck heights interpolate linearly between road points;
        /// the extra stations carry depth, walls and AO).</summary>
        public const double MaxStationM = 8.0;

        /// <summary>The deck edge clamp of a side that is not shared (finite, so per-station values interpolate).</summary>
        public const double NoClamp = 1e6;

        /// <summary>A crossing counts as a road passing under the deck only when the deck is at least this high above
        /// it (lower ones are at-grade junctions at a bridge end).</summary>
        public const float MinCrossingHeightM = 2.5f;

        private static readonly ConditionalWeakTable<TileData, BridgeLayout> Cache = new ConditionalWeakTable<TileData, BridgeLayout>();
        private static readonly RoadOptions DefaultRoadOptions = new RoadOptions();

        public readonly TileData Tile;

        /// <summary>One structure record per road (the tile's own or derived).</summary>
        public readonly RoadStructureRecord[] Structures;

        /// <summary>True when <see cref="Structures"/> were derived from the W1 tags.</summary>
        public readonly bool Derived;

        /// <summary>Elevated spans (bridges, flyovers, foot overbridges).</summary>
        public readonly List<BridgeSpan> Spans = new List<BridgeSpan>();

        /// <summary>Lowered underpass pieces with a trench (record kind Underpass with deck heights).</summary>
        public readonly List<BridgeSpan> Trenches = new List<BridgeSpan>();

        private readonly LazyGround _ground;

        /// <summary>The full-resolution terrain of the tile (used for derivation and supports), built on first use so
        /// tiles without bridges never pay for it.</summary>
        public TileHeightSampler Ground
        {
            get { return _ground.Sampler; }
        }

        /// <summary>A <see cref="TileHeightSampler"/> created on the first query (a benign race at worst builds two).</summary>
        private sealed class LazyGround : IHeightSampler
        {
            private readonly TileData _tile;
            private TileHeightSampler _sampler;

            public LazyGround(TileData t)
            {
                _tile = t;
            }

            public TileHeightSampler Sampler
            {
                get
                {
                    TileHeightSampler s = _sampler;
                    if (s == null)
                    {
                        s = new TileHeightSampler(_tile);
                        _sampler = s;
                    }
                    return s;
                }
            }

            public bool TryHeight(double x, double z, out float h)
            {
                return Sampler.TryHeight(x, z, out h);
            }
        }

        /// <summary>The layout of a tile (built on first use, then cached for the tile's lifetime).</summary>
        public static BridgeLayout For(TileData t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            return Cache.GetValue(t, k => new BridgeLayout(k));
        }

        private BridgeLayout(TileData t)
        {
            Tile = t;
            _ground = new LazyGround(t);
            bool derived;
            Structures = BridgeStructures.Resolve(t, _ground, out derived);
            Derived = derived;
            RoadLayout roads = t.Roads.Count > 0 ? RoadLayout.For(t) : null;
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadStructureRecord s = Structures[i];
                RoadRecord r = t.Roads[i];
                if (s.DeckY == null || s.DeckY.Length != r.PointCount || !RoadMesher.IsDrawn(r, null)) continue;
                if (s.IsElevated)
                {
                    BridgeSpan span = Build(t, roads, i, s);
                    if (span != null) Spans.Add(span);
                }
                else if (s.Kind == RoadStructureKind.Underpass)
                {
                    BridgeSpan span = Build(t, roads, i, s);
                    if (span != null) Trenches.Add(span);
                }
            }
            SharedSides();
            foreach (BridgeSpan sp in Spans) Supports(t, sp);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Span construction
        // ---------------------------------------------------------------------------------------------------------

        private BridgeSpan Build(TileData t, RoadLayout roads, int ri, RoadStructureRecord rec)
        {
            RoadRecord r = t.Roads[ri];
            int[] p = r.Points;
            int count = p.Length / 2;
            int first = r.HasPrevContext ? 1 : 0, last = r.HasNextContext ? count - 2 : count - 1;
            var along = new double[count];
            for (int k = first + 1; k < count; k++) along[k] = along[k - 1] + SegLen(p, k - 1);
            for (int k = first - 1; k >= 0; k--) along[k] = along[k + 1] - SegLen(p, k);
            if (along[last] - along[first] < 2.0) return null;

            var sp = new BridgeSpan { Road = ri, Record = r, Kind = rec.Kind };
            sp.Foot = BridgeStyle.IsFoot(r.RoadClass) || r.RoadClass == RoadClass.Steps;
            sp.Overbridge = rec.Has(RoadStructureFlags.FootOverbridge);
            sp.Water = rec.Has(RoadStructureFlags.WaterCrossing);
            RoadAttrRecord attr = roads != null ? roads.Attrs[ri] : default(RoadAttrRecord);
            sp.Heritage = attr.Has(RoadAttrFlags.HeritagePedestrian) || attr.Has(RoadAttrFlags.NoMotor) && sp.Foot && r.Surface == Surface.Brick;
            sp.Lit = !sp.Foot && (attr.Has(RoadAttrFlags.Lit) || RoadWidthModel.IsMajor(r.RoadClass) || rec.Kind == RoadStructureKind.Flyover);
            sp.Railing = BridgeStyle.RailingFor(r, rec, sp.Heritage);
            sp.Steel = BridgeStyle.SteelFor(r);
            sp.RailHeight = rec.RailingHeightM > 0.5f ? rec.RailingHeightM : sp.Foot ? 1.2f : BridgeStyle.RailHeightM;
            sp.RaisedWalk = !sp.Foot;
            int sizeCm = (int)Math.Round(t.Tile.Size * 100.0);
            sp.StartCut = r.HasPrevContext && BorderOf(p[2 * first], p[2 * first + 1], sizeCm) >= 0;
            sp.EndCut = r.HasNextContext && BorderOf(p[2 * last], p[2 * last + 1], sizeCm) >= 0;
            float[] deck = rec.DeckY;
            if (r.HasPrevContext)
            {
                sp.HasPre = true;
                sp.PreX = p[0] / 100.0;
                sp.PreZ = p[1] / 100.0;
                sp.PreY = deck[0];
            }
            if (r.HasNextContext)
            {
                sp.HasPost = true;
                sp.PostX = p[2 * count - 2] / 100.0;
                sp.PostZ = p[2 * count - 1] / 100.0;
                sp.PostY = deck[count - 1];
            }

            // Crossings first (their zone edges become stations).
            if (rec.Kind != RoadStructureKind.Underpass) FindCrossings(t, roads, ri, rec, along, first, last, sp);

            // Stations: rendered points, cut ends on the border, subdivisions and crossing zone edges.
            BridgePath path = sp.Path;
            path.Clear();
            for (int i = first; i <= last; i++)
            {
                double cx = p[2 * i] / 100.0, cz = p[2 * i + 1] / 100.0;
                if (i > first)
                {
                    double ax = p[2 * i - 2] / 100.0, az = p[2 * i - 1] / 100.0;
                    double len = along[i] - along[i - 1];
                    if (len < 1e-6) continue;
                    double dx = (cx - ax) / len, dz = (cz - az) / len;
                    double sa = along[i - 1], sb = along[i];
                    int nsub = (int)Math.Ceiling(len / MaxStationM);
                    // Candidate interior stations: uniform subdivisions plus zone edges, in order.
                    var cand = Scratch(nsub + 2 * sp.Crossings.Count + 2);
                    int nc = 0;
                    for (int k = 1; k < nsub; k++) cand[nc++] = sa + len * k / nsub;
                    foreach (BridgeCrossing c in sp.Crossings)
                    {
                        double e0 = c.S - c.HalfAlong, e1 = c.S + c.HalfAlong;
                        if (e0 > sa + 0.05 && e0 < sb - 0.05) cand[nc++] = e0;
                        if (e1 > sa + 0.05 && e1 < sb - 0.05) cand[nc++] = e1;
                    }
                    Array.Sort(cand, 0, nc);
                    double lastS = sa;
                    for (int k = 0; k < nc; k++)
                    {
                        if (cand[k] - lastS < 0.05 || sb - cand[k] < 0.05) continue;
                        double f = (cand[k] - sa) / len;
                        float y = (float)(deck[i - 1] + (deck[i] - deck[i - 1]) * f);
                        path.Add(ax + (cx - ax) * f, az + (cz - az) * f, -dz, dx, -dz, dx, cand[k], y);
                        lastS = cand[k];
                    }
                }
                double tx, tz, miter;
                Tangent(p, count, i, out tx, out tz, out miter);
                double nx = -tz, nz = tx;
                bool cutEnd = i == first && sp.StartCut || i == last && sp.EndCut;
                if (cutEnd)
                {
                    int border = BorderOf(p[2 * i], p[2 * i + 1], sizeCm);
                    double ex = border == 0 ? 0 : 1, ez = border == 0 ? 1 : 0;
                    double crs = ex * tz - ez * tx;
                    const double MinCutSine = 1.0 / 3.0;
                    if (Math.Abs(crs) < MinCutSine) crs = crs >= 0 ? MinCutSine : -MinCutSine;
                    path.Add(cx, cz, -ex / crs, -ez / crs, nx, nz, along[i], deck[i]);
                }
                else
                {
                    path.Add(cx, cz, nx * miter, nz * miter, nx, nz, along[i], deck[i]);
                }
            }
            if (path.Count < 2) return null;

            int n = path.Count;
            sp.Ground = new float[n];
            sp.Depth = new float[n];
            sp.HalfL = new double[n];
            sp.HalfR = new double[n];
            sp.WalkL = new double[n];
            sp.WalkR = new double[n];
            sp.Fill = new bool[n];
            sp.Steps = new bool[n];
            sp.ClampL = new double[n];
            sp.ClampR = new double[n];
            RoadWidthProfile prof = roads != null ? roads.Profiles[ri] : null;
            bool dual = attr.Has(RoadAttrFlags.Dual);
            double baseWalk = WalkFor(r.RoadClass, sp);
            float baseDepth = sp.Foot ? BridgeStyle.FootDepthM : rec.Kind == RoadStructureKind.Flyover ? BridgeStyle.FlyoverDepthM : BridgeStyle.RoadDepthM;
            double minClear = sp.Overbridge ? BridgeStyle.FootOverbridgeWidthM : RoadClearance.MinCorridorM;
            for (int k = 0; k < n; k++)
            {
                double s = path.S[k];
                sp.Ground[k] = BridgeStructures.Ground(t, Ground, path.X[k], path.Z[k]);
                double w = prof != null ? prof.WidthAt(s) : RoadStyle.WidthM(r);
                double shift = prof != null && dual ? 0.5 * (w - prof.RealM) : 0;
                if (sp.Overbridge) w = Math.Max(Math.Min(w, 3.0), 1.6);
                sp.HalfL[k] = 0.5 * w + shift;
                sp.HalfR[k] = 0.5 * w - shift;
                double extra = Math.Max(0, 0.5 * (minClear - (w + 2 * baseWalk)));
                sp.WalkL[k] = sp.WalkR[k] = baseWalk + extra;
                sp.ClampL[k] = sp.ClampR[k] = NoClamp;
                float depth = baseDepth;
                foreach (BridgeCrossing c in sp.Crossings)
                {
                    if (Math.Abs(s - c.S) > c.HalfAlong + 1e-3) continue;
                    float room = path.Y[k] - c.LowY - RoadClearance.MinUnderpassClearanceM;
                    depth = Math.Min(depth, Math.Max(BridgeStyle.MinDepthM, room));
                }
                sp.Depth[k] = depth;
            }
            for (int k = 0; k < n; k++)
            {
                bool inCrossing = false;
                foreach (BridgeCrossing c in sp.Crossings)
                    if (Math.Abs(path.S[k] - c.S) <= c.HalfAlong + 1e-3) inCrossing = true;
                sp.Fill[k] = rec.Kind == RoadStructureKind.Flyover && !sp.Foot && !inCrossing && path.Y[k] - sp.Ground[k] < BridgeStyle.FillMaxM;
            }
            for (int k = 0; k + 1 < n; k++)
            {
                double ds = path.S[k + 1] - path.S[k];
                sp.Steps[k] = sp.Foot && ds > 1e-6 && Math.Abs(path.Y[k + 1] - path.Y[k]) / ds > BridgeStyle.StairGrade;
            }
            return sp;
        }

        /// <summary>Walkway beyond the carriageway on each side: footpaths on major roads, a safety kerb on minor
        /// ones, none on foot decks (the whole deck is the walkway).</summary>
        private static double WalkFor(RoadClass c, BridgeSpan sp)
        {
            if (sp.Foot) return 0;
            switch (c)
            {
                case RoadClass.Motorway:
                case RoadClass.Trunk:
                case RoadClass.Primary:
                case RoadClass.Secondary:
                    return 1.5;
                case RoadClass.Tertiary:
                    return 1.2;
                default:
                    return BridgeStyle.SafetyKerbM;
            }
        }

        private void FindCrossings(TileData t, RoadLayout roads, int ri, RoadStructureRecord rec, double[] along, int first, int last, BridgeSpan sp)
        {
            RoadRecord r = t.Roads[ri];
            int[] p = r.Points;
            float[] deck = rec.DeckY;
            for (int j = 0; j < t.Roads.Count; j++)
            {
                if (j == ri) continue;
                RoadRecord o = t.Roads[j];
                if ((o.Flags & RoadFlags.Tunnel) != 0 || !RoadMesher.IsDrawn(o, null)) continue;
                RoadStructureRecord os = Structures[j];
                int[] q = o.Points;
                int oc = q.Length / 2;
                int of = o.HasPrevContext ? 1 : 0, ol = o.HasNextContext ? oc - 2 : oc - 1;
                double oAlong = 0;
                for (int b = of; b < ol; b++)
                {
                    double blen = SegLenD(q, b);
                    for (int a = first; a < last; a++)
                    {
                        if (!BridgeStructures.Crosses(p[2 * a], p[2 * a + 1], p[2 * a + 2], p[2 * a + 3], a == first, a + 1 == last, q[2 * b],
                                                      q[2 * b + 1], q[2 * b + 2], q[2 * b + 3])) continue;
                        double ax = p[2 * a] / 100.0, az = p[2 * a + 1] / 100.0, bx = p[2 * a + 2] / 100.0, bz = p[2 * a + 3] / 100.0;
                        double cx = q[2 * b] / 100.0, cz = q[2 * b + 1] / 100.0, dx = q[2 * b + 2] / 100.0, dz = q[2 * b + 3] / 100.0;
                        double ta, tb;
                        Intersect(ax, az, bx, bz, cx, cz, dx, dz, out ta, out tb);
                        double sAt = along[a] + (along[a + 1] - along[a]) * ta;
                        float deckY = (float)(deck[a] + (deck[a + 1] - deck[a]) * ta);
                        double ix = ax + (bx - ax) * ta, iz = az + (bz - az) * ta;
                        float lowY;
                        if (os.DeckY != null && os.DeckY.Length == oc)
                            lowY = (float)(os.DeckY[b] + (os.DeckY[b + 1] - os.DeckY[b]) * tb);
                        else
                            lowY = BridgeStructures.Ground(t, Ground, ix, iz) + RoadMesher.LiftOf(o, DefaultRoadOptions);
                        if (deckY - lowY < MinCrossingHeightM) continue;
                        // Angle between the two centrelines.
                        double ux = (bx - ax), uz = (bz - az), vx = (dx - cx), vz = (dz - cz);
                        double lu = Math.Sqrt(ux * ux + uz * uz), lv = Math.Sqrt(vx * vx + vz * vz);
                        double sin = lu > 0 && lv > 0 ? Math.Abs(ux * vz - uz * vx) / (lu * lv) : 1;
                        double cos = lu > 0 && lv > 0 ? Math.Abs(ux * vx + uz * vz) / (lu * lv) : 0;
                        if (sin < 0.25) sin = 0.25;
                        double lowHalf = 0.5 * LowerWidth(roads, o, j, oAlong + blen * tb) + 0.5;
                        double deckHalf = 0.5 * RoadStyle.WidthM(r) + 2.5;
                        sp.Crossings.Add(new BridgeCrossing
                        {
                            Road = j, S = sAt, HalfAlong = lowHalf / sin + deckHalf * cos / sin, LowY = lowY, X = ix, Z = iz, LowHalfM = lowHalf,
                        });
                    }
                    oAlong += blen;
                }
            }
        }

        private static double LowerWidth(RoadLayout roads, RoadRecord o, int j, double alongM)
        {
            if (roads == null) return RoadStyle.WidthM(o);
            RoadProfile pr = roads.ProfileAt(j, alongM);
            return Math.Max(pr.CarriagewayM + pr.FootpathLeftM + pr.FootpathRightM + 2 * pr.ShoulderM, RoadClearance.MinCorridorM);
        }

        private static void Intersect(double ax, double az, double bx, double bz, double cx, double cz, double dx, double dz, out double ta, out double tb)
        {
            double rx = bx - ax, rz = bz - az, sx = dx - cx, sz = dz - cz;
            double den = rx * sz - rz * sx;
            if (Math.Abs(den) < 1e-12)
            {
                ta = tb = 0.5;
                return;
            }
            ta = ((cx - ax) * sz - (cz - az) * sx) / den;
            tb = ((cx - ax) * rz - (cz - az) * rx) / den;
            ta = ta < 0 ? 0 : ta > 1 ? 1 : ta;
            tb = tb < 0 ? 0 : tb > 1 ? 1 : tb;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Parallel decks
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Twin decks (the two carriageways of a dual road mapped as separate bridges) whose edges would meet
        /// or overlap share that side: the deck stops halfway between the centrelines and a median barrier replaces
        /// the railing, so no railing stands in the other deck's lanes.</summary>
        private void SharedSides()
        {
            for (int a = 0; a < Spans.Count; a++)
            {
                BridgeSpan sa = Spans[a];
                int sharedL = 0, sharedR = 0;
                var midL = new double[sa.Count];
                var midR = new double[sa.Count];
                for (int k = 0; k < sa.Count; k++)
                {
                    midL[k] = midR[k] = double.PositiveInfinity;
                    double x = sa.Path.X[k], z = sa.Path.Z[k], nx = sa.Path.Nx[k], nz = sa.Path.Nz[k];
                    for (int b = 0; b < Spans.Count; b++)
                    {
                        if (b == a) continue;
                        BridgeSpan sb = Spans[b];
                        double d, side, otherEdge;
                        float oy;
                        if (!Nearest(sb, x, z, nx, nz, out d, out side, out otherEdge, out oy)) continue;
                        if (Math.Abs(oy - sa.Path.Y[k]) > 1.5f) continue;
                        double mine = side > 0 ? sa.HalfL[k] + sa.WalkL[k] + BridgeStyle.RailBaseM + BridgeStyle.OverhangM
                            : sa.HalfR[k] + sa.WalkR[k] + BridgeStyle.RailBaseM + BridgeStyle.OverhangM;
                        if (d > mine + otherEdge + 0.6) continue;
                        if (side > 0) midL[k] = Math.Min(midL[k], 0.5 * d);
                        else midR[k] = Math.Min(midR[k], 0.5 * d);
                    }
                    if (!double.IsPositiveInfinity(midL[k])) sharedL++;
                    if (!double.IsPositiveInfinity(midR[k])) sharedR++;
                }
                sa.SharedL = sharedL * 2 >= sa.Count && sharedL > 0;
                sa.SharedR = sharedR * 2 >= sa.Count && sharedR > 0;
                for (int k = 0; k < sa.Count; k++)
                {
                    if (sa.SharedL && !double.IsPositiveInfinity(midL[k]))
                    {
                        sa.ClampL[k] = Math.Max(midL[k], sa.HalfL[k] + BridgeStyle.RailBaseM);
                        sa.WalkL[k] = Math.Max(0, Math.Min(sa.WalkL[k], sa.ClampL[k] - BridgeStyle.RailBaseM - sa.HalfL[k]));
                    }
                    if (sa.SharedR && !double.IsPositiveInfinity(midR[k]))
                    {
                        sa.ClampR[k] = Math.Max(midR[k], sa.HalfR[k] + BridgeStyle.RailBaseM);
                        sa.WalkR[k] = Math.Max(0, Math.Min(sa.WalkR[k], sa.ClampR[k] - BridgeStyle.RailBaseM - sa.HalfR[k]));
                    }
                }
            }
        }

        /// <summary>Distance from (x, z) along the line (nx, nz) to span b's centreline (when the line meets it within
        /// 30 m and the two run roughly parallel), the side (+1 left), b's deck half width there and its deck y.</summary>
        private static bool Nearest(BridgeSpan b, double x, double z, double nx, double nz, out double d, out double side, out double otherEdge, out float oy)
        {
            d = side = otherEdge = 0;
            oy = 0;
            BridgePath p = b.Path;
            double best = double.PositiveInfinity;
            for (int k = 0; k + 1 < p.Count; k++)
            {
                double ax = p.X[k], az = p.Z[k], bx = p.X[k + 1], bz = p.Z[k + 1];
                double sx = bx - ax, sz = bz - az, sl = Math.Sqrt(sx * sx + sz * sz);
                if (sl < 1e-6) continue;
                // Parallel enough?
                double par = Math.Abs(sx * -nz + sz * nx) / sl;
                if (par < 0.85) continue;
                // Ray x + t·n meets the segment.
                double den = nx * sz - nz * sx;
                if (Math.Abs(den) < 1e-9) continue;
                double t = ((ax - x) * sz - (az - z) * sx) / den;
                double u = ((ax - x) * nz - (az - z) * nx) / den;
                if (u < -1e-6 || u > 1 + 1e-6 || Math.Abs(t) > 30 || Math.Abs(t) >= best) continue;
                best = Math.Abs(t);
                d = Math.Abs(t);
                side = t > 0 ? 1 : -1;
                int j = k + 1;
                double el = b.HalfL[k] + (b.HalfL[j] - b.HalfL[k]) * u, er = b.HalfR[k] + (b.HalfR[j] - b.HalfR[k]) * u;
                double wl = b.WalkL[k] + (b.WalkL[j] - b.WalkL[k]) * u, wr = b.WalkR[k] + (b.WalkR[j] - b.WalkR[k]) * u;
                // Which of b's sides faces us: b's left normal against the ray direction.
                double bnx = -sz / sl, bnz = sx / sl;
                bool facingLeft = (bnx * -nx + bnz * -nz) * side > 0;
                otherEdge = (facingLeft ? el + wl : er + wr) + BridgeStyle.RailBaseM + BridgeStyle.OverhangM;
                oy = (float)(p.Y[k] + (p.Y[j] - p.Y[k]) * u);
            }
            return !double.IsPositiveInfinity(best);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Supports, lamps and stairs
        // ---------------------------------------------------------------------------------------------------------

        private void Supports(TileData t, BridgeSpan sp)
        {
            BridgePath path = sp.Path;
            int n = path.Count;
            double wx0 = t.Tile.X0, wz0 = t.Tile.Z0;
            // Open stretches (maximal runs of non-fill stations) and their supports.
            int k = 0;
            while (k < n)
            {
                if (sp.Fill[k])
                {
                    k++;
                    continue;
                }
                int a = k;
                while (k + 1 < n && !sp.Fill[k + 1]) k++;
                int b = k;
                k++;
                double s0 = path.S[a], s1 = path.S[b];
                bool fillBefore = a > 0, fillAfter = b < n - 1;
                if (fillBefore) sp.Bents.Add(s0);
                if (fillAfter) sp.Bents.Add(s1);
                bool cut0 = a == 0 && sp.StartCut, cut1 = b == n - 1 && sp.EndCut;
                double target = sp.Kind == RoadStructureKind.Flyover ? BridgeStyle.FlyoverSpanM : BridgeStyle.RiverSpanM;
                if (sp.Foot && !sp.Overbridge) target = 18;
                double len = s1 - s0;
                if (!cut0 && !cut1)
                {
                    if (len <= BridgeStyle.PierFreeSpanM) continue;
                    int spans = (int)Math.Ceiling(len / target);
                    for (int i = 1; i < spans; i++) TryPier(sp, s0 + len * i / spans, s0, s1);
                }
                else
                {
                    // World-anchored stations, so both tiles of a cut span agree on every pier.
                    AnchoredStations(path, s0, s1, target, wx0, wz0, _anchors);
                    foreach (double s in _anchors)
                    {
                        if (!cut0 && s - s0 < 0.5 * target) continue;
                        if (!cut1 && s1 - s < 0.5 * target) continue;
                        TryPier(sp, s, cut0 ? double.NegativeInfinity : s0, cut1 ? double.PositiveInfinity : s1);
                    }
                }
            }
            // Lamps: alternate sides every half spacing (both sides on wide decks), never on a shared side.
            if (sp.Lit && !sp.Overbridge)
            {
                double spacing = BridgeStyle.LampSpacingM * 0.5;
                AnchoredStations(path, path.Start, path.End, spacing, wx0, wz0, _anchors);
                bool wide = sp.HalfL[0] + sp.HalfR[0] > 14;
                foreach (double s in _anchors)
                {
                    if (!sp.StartCut && s - path.Start < 4) continue;
                    if (!sp.EndCut && path.End - s < 4) continue;
                    long idx = BridgeKit.StripeIndex(path, s + 1e-3, spacing, wx0, wz0);
                    int side = (idx & 1) == 0 ? 1 : -1;
                    bool okThis = side > 0 ? !sp.SharedL : !sp.SharedR, okOther = side > 0 ? !sp.SharedR : !sp.SharedL;
                    if (okThis) sp.Lamps.Add(new BridgeLamp { S = s, Side = side });
                    if (okOther && (wide || !okThis)) sp.Lamps.Add(new BridgeLamp { S = s, Side = -side });
                }
            }
            // Foot overbridge stairs at real ends high above the ground.
            if (sp.Overbridge)
            {
                if (!sp.StartCut) AddStair(sp, 0, -1);
                if (!sp.EndCut) AddStair(sp, n - 1, +1);
            }
        }

        [ThreadStatic] private static List<double> _anchorsTs;

        private static List<double> _anchors
        {
            get { return _anchorsTs ?? (_anchorsTs = new List<double>()); }
        }

        private void TryPier(BridgeSpan sp, double s, double s0, double s1)
        {
            BridgePath path = sp.Path;
            const double MinGap = 6.0;
            // Keep out of the corridors of the roads below: move to the nearer zone edge, or drop.
            foreach (BridgeCrossing c in sp.Crossings)
            {
                double half = c.HalfAlong + 1.2;
                if (Math.Abs(s - c.S) >= half) continue;
                double lo = c.S - half, hi = c.S + half;
                s = s - lo < hi - s ? lo : hi;
            }
            foreach (BridgeCrossing c in sp.Crossings)
                if (Math.Abs(s - c.S) < c.HalfAlong + 1.1) return;
            if (s <= s0 + MinGap * 0.5 || s >= s1 - MinGap * 0.5) return;
            if (s < path.Start || s > path.End) return;
            foreach (double q in sp.Piers)
                if (Math.Abs(q - s) < MinGap) return;
            float y = path.YAt(s), g = sp.Lerp(sp.Ground, s);
            if (y - sp.Lerp(sp.Depth, s) - g < 1.0f) return; // the deck sits on the ground here
            sp.Piers.Add(s);
            sp.Piers.Sort();
        }

        /// <summary>World-anchored along positions every <paramref name="spacing"/> metres in [s0, s1] (boundaries of
        /// <see cref="BridgeKit.StripeIndex"/>), so tiles cutting the same span agree.</summary>
        internal static void AnchoredStations(BridgePath path, double s0, double s1, double spacing, double wx0, double wz0, List<double> result)
        {
            result.Clear();
            for (int k = 0; k + 1 < path.Count; k++)
            {
                double a0 = Math.Max(path.S[k], s0), a1 = Math.Min(path.S[k + 1], s1);
                if (a1 - a0 < 1e-6) continue;
                double dx = path.X[k + 1] - path.X[k], dz = path.Z[k + 1] - path.Z[k], dl = Math.Sqrt(dx * dx + dz * dz);
                if (dl < 1e-9) continue;
                double tx = dx / dl, tz = dz / dl;
                bool useX = Math.Abs(tx) >= Math.Abs(tz);
                double tt = useX ? tx : tz, at = Math.Max(Math.Abs(tt), 1e-6);
                double w = (useX ? wx0 + path.X[k] : wz0 + path.Z[k]) / at;
                double sign = tt >= 0 ? 1 : -1;
                double c0 = w + sign * (a0 - path.S[k]), c1 = w + sign * (a1 - path.S[k]);
                long j0 = (long)Math.Ceiling(Math.Min(c0, c1) / spacing), j1 = (long)Math.Floor(Math.Max(c0, c1) / spacing);
                for (long j = j0; j <= j1; j++)
                {
                    double sb = path.S[k] + (j * spacing - w) * sign;
                    if (sb < s0 || sb > s1) continue;
                    if (result.Count > 0 && Math.Abs(result[result.Count - 1] - sb) < 0.5 * spacing) continue;
                    result.Add(sb);
                }
            }
            result.Sort();
        }

        private void AddStair(BridgeSpan sp, int k, int dir)
        {
            BridgePath path = sp.Path;
            float g = BridgeStructures.Ground(Tile, Ground, path.X[k], path.Z[k]);
            float top = path.Y[k];
            if (top - g < 1.0f) return;
            int j = dir < 0 ? 1 : k - 1;
            double dx = path.X[k] - path.X[j], dz = path.Z[k] - path.Z[j], dl = Math.Sqrt(dx * dx + dz * dz);
            if (dl < 1e-6) return;
            dx /= dl;
            dz /= dl;
            // Iterate the bottom height: the ground where the flight lands.
            int steps = 0, landings = 0;
            double length = 0;
            float bottom = g;
            for (int it = 0; it < 3; it++)
            {
                steps = Math.Max(2, (int)Math.Round((top - bottom) / BridgeStyle.StairRiseM));
                landings = (steps - 1) / BridgeStyle.StairsPerFlight;
                length = steps * BridgeStyle.StairTreadM + landings * 1.5;
                bottom = BridgeStructures.Ground(Tile, Ground, path.X[k] + dx * length, path.Z[k] + dz * length);
            }
            sp.Stairs.Add(new BridgeStair
            {
                X = path.X[k], Z = path.Z[k], TopY = top, BottomY = bottom, Dx = dx, Dz = dz,
                HalfWidth = Math.Min(sp.HalfL[k] + sp.WalkL[k], sp.HalfR[k] + sp.WalkR[k]), Length = length, Steps = steps, Landings = landings,
            });
        }

        // ---------------------------------------------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------------------------------------------

        [ThreadStatic] private static double[] _scratch;

        private static double[] Scratch(int n)
        {
            if (_scratch == null || _scratch.Length < n) _scratch = new double[Math.Max(n, 64)];
            return _scratch;
        }

        private static double SegLen(int[] p, int i)
        {
            double dx = (p[2 * i + 2] - p[2 * i]) / 100.0, dz = (p[2 * i + 3] - p[2 * i + 1]) / 100.0;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        private static double SegLenD(int[] p, int i)
        {
            return SegLen(p, i);
        }

        /// <summary>Unit tangent (bisector) at point i with the miter scale (capped at 2).</summary>
        private static void Tangent(int[] p, int count, int i, out double tx, out double tz, out double miter)
        {
            double ix = 0, iz = 0, ox = 0, oz = 0;
            bool hasIn = false, hasOut = false;
            if (i > 0)
            {
                double dx = p[2 * i] - (double)p[2 * i - 2], dz = p[2 * i + 1] - (double)p[2 * i - 1];
                double l = Math.Sqrt(dx * dx + dz * dz);
                if (l > 0)
                {
                    ix = dx / l;
                    iz = dz / l;
                    hasIn = true;
                }
            }
            if (i < count - 1)
            {
                double dx = p[2 * i + 2] - (double)p[2 * i], dz = p[2 * i + 3] - (double)p[2 * i + 1];
                double l = Math.Sqrt(dx * dx + dz * dz);
                if (l > 0)
                {
                    ox = dx / l;
                    oz = dz / l;
                    hasOut = true;
                }
            }
            miter = 1;
            if (hasIn && hasOut)
            {
                double bx = ix + ox, bz = iz + oz, bl = Math.Sqrt(bx * bx + bz * bz);
                if (bl < 1e-9)
                {
                    tx = ox;
                    tz = oz;
                    return;
                }
                tx = bx / bl;
                tz = bz / bl;
                double cosHalf = tx * ox + tz * oz;
                miter = 1.0 / Math.Max(cosHalf, 0.5);
                return;
            }
            if (hasOut)
            {
                tx = ox;
                tz = oz;
            }
            else if (hasIn)
            {
                tx = ix;
                tz = iz;
            }
            else
            {
                tx = 1;
                tz = 0;
            }
        }

        /// <summary>Which tile border a cut point lies on: 0 vertical (x = 0 or size), 1 horizontal, -1 none.</summary>
        private static int BorderOf(int xCm, int zCm, int sizeCm)
        {
            if (xCm == 0 || xCm == sizeCm) return 0;
            if (zCm == 0 || zCm == sizeCm) return 1;
            return -1;
        }
    }
}
