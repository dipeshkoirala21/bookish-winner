using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.Core.Meshing.Bridges
{
    /// <summary>A lower road passing under a span with the clearance kept: where, how far along the span its corridor
    /// reaches, and its surface height there.</summary>
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

    /// <summary>An along interval [S0, S1] of a span (an opening over a road, a railing gap, a stair stretch), with
    /// the road that causes it (−1 when none: a branch deck or a stair leaves there).</summary>
    public struct BridgeGap
    {
        public double S0, S1;
        public int Road;
    }

    /// <summary>A lamp post along a span: along distance and side (+1 left, −1 right).</summary>
    public struct BridgeLamp
    {
        public double S;
        public int Side;
    }

    /// <summary>An abutment (end support) of a span: along position, the direction of the span from it (+1 the span
    /// lies at greater S), whether it is a full bank seat with wing walls (a real end) or an end wall moved back from
    /// a road the deck leaves open, and the wing wall lengths kept clear of other roads.</summary>
    public struct BridgeAbutment
    {
        public double S;
        public int Dir;
        public bool Wings;
        public double WingL, WingR;
    }

    /// <summary>What happens at a span end.</summary>
    public enum SpanEnd : byte
    {
        /// <summary>A real end on the ground: abutment, end pillars, deck cap (foot overbridges: a stair).</summary>
        Ground = 0,

        /// <summary>On a tile border: the structure continues in the neighbour.</summary>
        Cut = 1,

        /// <summary>Another deck or a stair continues from here: nothing closes the end.</summary>
        Junction = 2,

        /// <summary>A landing where stairs leave to the side: an end railing closes the deck, the side railing opens
        /// for the stair.</summary>
        Landing = 3,
    }

    /// <summary>A deck that does not keep the clearance over a road below (a structure record to fix): the structure
    /// leaves that road open instead of thinning below <see cref="BridgeStyle.MinDepthM"/> (foot decks
    /// <see cref="BridgeStyle.MinFootDepthM"/>).</summary>
    public struct BridgeIssue
    {
        public ulong Way, LowerWay;

        /// <summary>Lowest deck surface over the lower road's corridor, above that road.</summary>
        public float DeckAboveM;
    }

    /// <summary>
    /// One elevated road piece (or a lowered underpass, or a mapped stair) of a tile as the bridge meshers and the deck
    /// index see it: a densified centreline (<see cref="Path"/>) with the absolute deck height of the structure
    /// record, per-station ground, carriageway edges (the road ribbon), walkways, deck edges and structure depth, the
    /// roads passing under it (kept clear) or left open, railing gaps, its ends, and its supports, lamps and stairs.
    /// Built by <see cref="BridgeLayout"/>; read-only afterwards.
    /// </summary>
    public sealed class BridgeSpan
    {
        public int Road;
        public RoadRecord Record;
        public RoadStructureKind Kind;
        public bool Foot, Overbridge, Water, Heritage, Lit;

        /// <summary>A heritage parapet in grey stone (the Bagmati ghats) rather than brick.</summary>
        public bool StoneParapet;
        public RailingStyle Railing;
        public BridgeForm Form;
        public uint Steel;
        public float RailHeight;

        /// <summary>A mapped stair (a steps way climbing to a deck): drawn as flights only (<see cref="Stairs"/>).</summary>
        public bool Stair;

        /// <summary>The lowest way id of the connected structure this span belongs to (paint and roof choices).</summary>
        public ulong GroupWay;

        /// <summary>The road points the span covers (a run of points with heights).</summary>
        public int FirstPoint, LastPoint;

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

        /// <summary>Walkways are raised kerbs (motor roads) rather than flush with the deck (foot classes).</summary>
        public bool RaisedWalk;

        /// <summary>The side runs against a parallel deck: a median barrier replaces the railing and the deck edge
        /// stops at the middle between the two centrelines.</summary>
        public bool SharedL, SharedR;

        /// <summary>The span on the shared side (−1 when none).</summary>
        public int PartnerL = -1, PartnerR = -1;

        /// <summary>Per station: the deck edge clamp on a shared side (<see cref="BridgeLayout.NoClamp"/> elsewhere).</summary>
        public double[] ClampL, ClampR;

        /// <summary>The ends lie on a tile border (the structure continues in the neighbour).</summary>
        public bool StartCut, EndCut;

        /// <summary>What closes each end.</summary>
        public SpanEnd StartEnd, EndEnd;

        /// <summary>Along distance from each end inside another deck's footprint (a branch): no slab or railing there.</summary>
        public double TrimStart, TrimEnd;

        /// <summary>How far a foot overbridge deck was extended beyond its mapped end so the end (and the stairs that
        /// leave it) lies outside the widened corridors of the roads it crosses.</summary>
        public double ExtStart, ExtEnd;

        /// <summary>A mapped stair moved with the deck end it leaves (see <see cref="ExtStart"/>): plan offset of all its
        /// points.</summary>
        public double ShiftX, ShiftZ;

        /// <summary>Context points beyond the cut ends (for queries next to the border).</summary>
        public bool HasPre, HasPost;
        public double PreX, PreZ, PostX, PostZ;
        public float PreY, PostY;

        /// <summary>Roads passing under the deck with the clearance kept (the structure thins over them).</summary>
        public readonly List<BridgeCrossing> Crossings = new List<BridgeCrossing>();

        /// <summary>Stretches over roads the deck does not clear: no structure there (no slab, kerbs, railings, walls),
        /// the road below stays open and no ceiling is reported.</summary>
        public readonly List<BridgeGap> Openings = new List<BridgeGap>();

        /// <summary>Railing gaps on each side (a branch deck or a stair leaves there).</summary>
        public readonly List<BridgeGap> GapsL = new List<BridgeGap>(), GapsR = new List<BridgeGap>();

        /// <summary>Stretches drawn as stair flights (steep foot segments, or the whole of a stair span): no slab or
        /// railing; the flights in <see cref="Stairs"/> carry the surface.</summary>
        public readonly List<BridgeGap> StairZones = new List<BridgeGap>();

        public readonly List<double> Piers = new List<double>();

        /// <summary>Along positions of end walls where an open span meets a fill ramp.</summary>
        public readonly List<double> Bents = new List<double>();

        public readonly List<BridgeAbutment> Abutments = new List<BridgeAbutment>();

        /// <summary>A suspension span's main run (its towers' along positions; NaN when it has none), whether each
        /// tower stands (not across a tile border, not in another road) and each backstay's length to its anchor block
        /// (0: none fits clear of the roads around).</summary>
        public double SuspA = double.NaN, SuspB = double.NaN;
        public bool TowerA, TowerB;
        public double BackstayA, BackstayB;

        public readonly List<BridgeLamp> Lamps = new List<BridgeLamp>();

        /// <summary>What may stand at each real end of each side's railing (index end × 2 + (left ? 1 : 0)): 2 the full
        /// end pillar with its finial, 1 a plain end post of railing height, 0 nothing (a road beside the bridge head
        /// lies within the taller part's reach).</summary>
        public readonly byte[] EndPost = { 2, 2, 2, 2 };
        public readonly List<BridgeStair> Stairs = new List<BridgeStair>();

        /// <summary>Along intervals (pairs) where the deck structure (slab, kerbs, walkways, fill) is drawn.</summary>
        public readonly List<double> DeckRuns = new List<double>();

        /// <summary>Along intervals (pairs) where each side's railing is drawn.</summary>
        public readonly List<double> RailRunsL = new List<double>(), RailRunsR = new List<double>();

        /// <summary>Station index of each covered road point (FirstPoint..LastPoint).</summary>
        internal int[] PointStation;

        /// <summary>The openings over roads crossing the centreline (before the side clearance adds its own).</summary>
        internal List<BridgeGap> BaseOpenings;

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

        /// <summary>True when along <paramref name="s"/> lies in an opening (the structure leaves a road open).</summary>
        public bool InOpening(double s)
        {
            return In(Openings, s);
        }

        /// <summary>True when along <paramref name="s"/> lies on a stair stretch.</summary>
        public bool InStairZone(double s)
        {
            return In(StairZones, s);
        }

        /// <summary>True when along <paramref name="s"/> lies strictly inside a stair stretch (its edges belong to the
        /// deck, where the flight starts at the deck height).</summary>
        public bool InStairZoneInterior(double s)
        {
            for (int i = 0; i < StairZones.Count; i++)
                if (s > StairZones[i].S0 + 1e-4 && s < StairZones[i].S1 - 1e-4) return true;
            return false;
        }

        /// <summary>True when along <paramref name="s"/> lies in a railing gap of <paramref name="side"/>.</summary>
        public bool InGap(int side, double s)
        {
            return In(side > 0 ? GapsL : GapsR, s);
        }

        /// <summary>True when the deck structure is drawn at along <paramref name="s"/>.</summary>
        public bool HasDeck(double s)
        {
            return InRuns(DeckRuns, s);
        }

        /// <summary>True when the railing of <paramref name="side"/> is drawn at along <paramref name="s"/>.</summary>
        public bool HasRail(int side, double s)
        {
            return InRuns(side > 0 ? RailRunsL : RailRunsR, s);
        }

        private static bool In(List<BridgeGap> l, double s)
        {
            for (int i = 0; i < l.Count; i++)
                if (s >= l[i].S0 && s <= l[i].S1) return true;
            return false;
        }

        private static bool InRuns(List<double> runs, double s)
        {
            for (int i = 0; i + 1 < runs.Count; i += 2)
                if (s >= runs[i] - 1e-6 && s <= runs[i + 1] + 1e-6) return true;
            return false;
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
    /// The bridges, flyovers, foot overbridges, stairs and underpass trenches of one tile, shared by
    /// <see cref="BridgeMesher"/> and <see cref="BridgeDeckIndex"/> so the drawn structure and the ground queries
    /// agree. Built once per decoded tile and cached for its lifetime (<see cref="For"/>); immutable afterwards, so
    /// worker threads may share it. Deck heights come from the tile's structure records (or
    /// <see cref="BridgeStructures.Derive"/> on packs without them); widths from the tile's <see cref="RoadLayout"/>
    /// (the same widened corridor the road ribbon draws), so a span cut by a tile border meets its neighbour exactly:
    /// both tiles see the same border width and deck height there, and every world-anchored detail (piers on cut
    /// spans, posts, lamps, kerb stripes) is placed by world position, so each is drawn by exactly one tile.
    /// <para>
    /// Every road a deck passes over in plan without a shared node is examined: where the deck keeps
    /// <see cref="RoadClearance.MinUnderpassClearanceM"/> over it (thinning down to <see cref="BridgeStyle.MinDepthM"/>,
    /// foot decks <see cref="BridgeStyle.MinFootDepthM"/>)
    /// it is a <see cref="BridgeCrossing"/>; a record's deck at most <see cref="BridgeStyle.IssueToleranceM"/> short of
    /// that keeps its thinnest structure and is reported (<see cref="Issues"/>); a deck further short leaves an opening
    /// over the road's corridor (an at-grade crossing on derived decks; on the data package's records also reported).
    /// Abutments, wing walls and piers stay out of every other road's corridor. Decks joined at a point form one
    /// structure: no end closes a junction, a branch opens the railing it leaves through, stairs leave from landings
    /// (an end railing closes the deck when they leave to the side), and the steel paint is chosen per structure. End
    /// pillars and lamps stand only where no road beside the deck reaches them. Each LOD sweeps through its own thinned
    /// key stations (<see cref="BridgePath.KeysFor"/>).
    /// </para>
    /// Deterministic.
    /// </summary>
    public sealed class BridgeLayout
    {
        /// <summary>Longest station spacing along a span (deck heights interpolate linearly between road points;
        /// the extra stations carry depth, walls and AO).</summary>
        public const double MaxStationM = 8.0;

        /// <summary>The deck edge clamp of a side that is not shared (finite, so per-station values interpolate).</summary>
        public const double NoClamp = 1e6;

        /// <summary>Margin added on each side of an opening over a road the deck does not clear.</summary>
        public const double OpeningMarginM = 0.3;

        /// <summary>A derived deck over no water that never stands this high above the ground and clears no road is an
        /// at-grade road (no structure).</summary>
        public const float AtGradeM = 1.0f;

        private static readonly ConditionalWeakTable<TileData, BridgeLayout> Cache = new ConditionalWeakTable<TileData, BridgeLayout>();
        private static readonly RoadOptions DefaultRoadOptions = new RoadOptions();

        public readonly TileData Tile;

        /// <summary>One structure record per road (the tile's own or derived).</summary>
        public readonly RoadStructureRecord[] Structures;

        /// <summary>True when <see cref="Structures"/> were derived from the W1 tags.</summary>
        public readonly bool Derived;

        /// <summary>Elevated spans (bridges, flyovers, foot overbridges) and mapped stairs.</summary>
        public readonly List<BridgeSpan> Spans = new List<BridgeSpan>();

        /// <summary>Lowered underpass pieces with a trench (record kind Underpass with deck heights).</summary>
        public readonly List<BridgeSpan> Trenches = new List<BridgeSpan>();

        /// <summary>Decks from the tile's records that do not keep the clearance over a road below (left open; the
        /// data package should raise them).</summary>
        public readonly List<BridgeIssue> Issues = new List<BridgeIssue>();

        private readonly LazyGround _ground;
        private readonly RoadLayout _roads;
        private readonly Dictionary<long, int> _footDeckPoints = new Dictionary<long, int>();
        private CorridorGrid _corridors;

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
            _roads = t.Roads.Count > 0 ? RoadLayout.For(t) : null;
            // Points shared by two foot decks (a junction of one structure: those ends are never extended).
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadStructureRecord s = Structures[i];
                RoadRecord r = t.Roads[i];
                if (!s.IsElevated || s.DeckY.Length != r.PointCount || !BridgeStyle.IsFoot(r.RoadClass) || r.RoadClass == RoadClass.Steps) continue;
                int count = r.PointCount;
                int first = r.HasPrevContext ? 1 : 0, last = r.HasNextContext ? count - 2 : count - 1;
                for (int k = first; k <= last; k++)
                {
                    long key = BridgeStructures.Key(r.Points[2 * k], r.Points[2 * k + 1]);
                    int c;
                    _footDeckPoints.TryGetValue(key, out c);
                    _footDeckPoints[key] = c + 1;
                }
            }
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadStructureRecord s = Structures[i];
                RoadRecord r = t.Roads[i];
                if (s.DeckY == null || s.DeckY.Length != r.PointCount || !RoadMesher.IsDrawn(r, null)) continue;
                bool foot = BridgeStyle.IsFoot(r.RoadClass);
                bool elevated = s.IsElevated;
                bool approach = s.Kind == RoadStructureKind.None && foot; // a stair or ramp climbing to a deck
                bool trench = s.Kind == RoadStructureKind.Underpass;
                if (!elevated && !approach && !trench) continue;
                // One span per run of points with heights (a NaN height is a draped point).
                int count = r.PointCount;
                int first = r.HasPrevContext ? 1 : 0, last = r.HasNextContext ? count - 2 : count - 1;
                int k = first;
                while (k <= last)
                {
                    if (float.IsNaN(s.DeckY[k]))
                    {
                        k++;
                        continue;
                    }
                    int a = k;
                    while (k + 1 <= last && !float.IsNaN(s.DeckY[k + 1])) k++;
                    int b = k;
                    k++;
                    if (b <= a) continue;
                    bool stair = foot && (r.RoadClass == RoadClass.Steps || approach || AllSteep(r, s.DeckY, a, b));
                    BridgeSpan span = Build(t, i, s, a, b, stair, trench);
                    if (span == null) continue;
                    if (trench) Trenches.Add(span);
                    else if (Keep(span)) Spans.Add(span);
                }
            }
            foreach (BridgeSpan sp in Spans) sp.BaseOpenings = new List<BridgeGap>(sp.Openings);
            // Connected structures, sides, supports and stairs. A mapped stair that would stand in a vehicle road
            // (the roads are widened beyond the mapped pavements) is dropped, and the pass repeats so its deck end
            // gets a stair that keeps clear of the roads instead.
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (BridgeSpan sp in Spans) Reset(sp);
                SharedSides();
                Structure();
                Styles();
                foreach (BridgeSpan sp in Spans)
                {
                    KeyStations(sp);
                    Thin(sp, sp.Path.Near, NearTolerance);
                    Thin(sp, sp.Path.Mid, MidTolerance);
                    Thin(sp, sp.Path.Far, FarTolerance);
                }
                foreach (BridgeSpan sp in Spans)
                    if (!sp.Stair) ClearSides(sp);
                foreach (BridgeSpan sp in Spans) Runs(sp);
                foreach (BridgeSpan sp in Spans)
                    if (!sp.Stair) EndPosts(sp);
                foreach (BridgeSpan sp in Spans)
                    if (!sp.Stair) Supports(t, sp);
                foreach (BridgeSpan sp in Spans)
                    if (sp.Stair) Flights(sp);
                List<BridgeSpan> bad = BlockedStairs();
                if (bad.Count == 0) break;
                Spans.RemoveAll(bad.Contains);
            }
        }

        /// <summary>The sweeps ring the deck at key stations only and are linear between them: a station where the
        /// cross-section (widths, walkways, a shared side's clamp, depth) leaves that line becomes a key station
        /// (finding: a walkway narrowing to a shared side was drawn as one long taper, the median barrier off the
        /// meeting line).</summary>
        private static void KeyStations(BridgeSpan sp)
        {
            BridgePath path = sp.Path;
            int n = path.Count;
            int a = 0;
            for (int k = 1; k + 1 < n; k++)
            {
                if (path.Key[k])
                {
                    a = k;
                    continue;
                }
                int b = k + 1;
                while (b < n - 1 && !path.Key[b]) b++;
                double span = path.S[b] - path.S[a];
                double f = span > 1e-9 ? (path.S[k] - path.S[a]) / span : 0;
                if (Off(sp.HalfL, a, b, k, f) || Off(sp.HalfR, a, b, k, f) || Off(sp.WalkL, a, b, k, f) || Off(sp.WalkR, a, b, k, f) ||
                    Off(sp.ClampL, a, b, k, f) || Off(sp.ClampR, a, b, k, f) || Math.Abs(sp.Depth[k] - (sp.Depth[a] + (sp.Depth[b] - sp.Depth[a]) * f)) > 0.02)
                {
                    path.Key[k] = true;
                    a = k;
                }
            }
        }

        /// <summary>How far a thinned sweep may leave the full one: the deck surface above (up) and below (down) its
        /// line, the centreline in plan, and the widths and structure depth.</summary>
        private struct Tolerance
        {
            public double Up, Down, Plan, Width;
        }

        /// <summary>LOD0: only (nearly) collinear stations go (the deck heights are linear between road points, which
        /// the data package densifies to a few metres even on straight grades).</summary>
        private static readonly Tolerance NearTolerance = new Tolerance { Up = 0.004, Down = 0.01, Plan = 0.01, Width = 0.01 };

        /// <summary>LOD1: a few centimetres (the slab top stays under the road surfacing).</summary>
        private static readonly Tolerance MidTolerance = new Tolerance { Up = 0.02, Down = 0.05, Plan = 0.06, Width = 0.04 };

        /// <summary>LOD2 (beyond 80 m on Low, 250 m on Mid): the far slab's top sits <see cref="BridgeMesher.FarTopDropM"/>
        /// lower, so it may rise that much; a decimetre or two elsewhere.</summary>
        private static readonly Tolerance FarTolerance = new Tolerance { Up = 0.06, Down = 0.25, Plan = 0.3, Width = 0.2 };

        /// <summary>Thin the key stations into <paramref name="keep"/> (Douglas-Peucker over the key stations): a key
        /// station stays when dropping it would move the deck surface, the centreline, a width, a clamp or the depth
        /// between its kept neighbours by more than the tolerance. The ends always stay.</summary>
        private static void Thin(BridgeSpan sp, bool[] keep, in Tolerance tol)
        {
            BridgePath path = sp.Path;
            int n = path.Count;
            var keys = new List<int>(n);
            for (int k = 0; k < n; k++)
            {
                keep[k] = false;
                if (path.Key[k] || k == 0 || k == n - 1) keys.Add(k);
            }
            if (keys.Count == 0) return;
            keep[keys[0]] = true;
            keep[keys[keys.Count - 1]] = true;
            var stack = new Stack<(int, int)>();
            stack.Push((0, keys.Count - 1));
            while (stack.Count > 0)
            {
                var (ia, ib) = stack.Pop();
                if (ib - ia < 2) continue;
                int a = keys[ia], b = keys[ib];
                double worst = 1.0;
                int at = -1;
                for (int i = ia + 1; i < ib; i++)
                {
                    double e = ThinError(sp, a, b, keys[i], tol);
                    if (e > worst)
                    {
                        worst = e;
                        at = i;
                    }
                }
                if (at < 0) continue;
                keep[keys[at]] = true;
                stack.Push((ia, at));
                stack.Push((at, ib));
            }
        }

        /// <summary>The largest deviation of station k from the line between stations a and b, as a multiple of the
        /// tolerance (above 1: k must stay).</summary>
        private static double ThinError(BridgeSpan sp, int a, int b, int k, in Tolerance tol)
        {
            BridgePath p = sp.Path;
            double span = p.S[b] - p.S[a];
            double f = span > 1e-9 ? (p.S[k] - p.S[a]) / span : 0;
            double dy = p.Y[a] + (p.Y[b] - p.Y[a]) * f - p.Y[k]; // > 0: the chord lies above the deck
            double e = dy > 0 ? dy / tol.Up : -dy / tol.Down;
            double ex = p.X[a] + (p.X[b] - p.X[a]) * f - p.X[k], ez = p.Z[a] + (p.Z[b] - p.Z[a]) * f - p.Z[k];
            e = Math.Max(e, Math.Sqrt(ex * ex + ez * ez) / tol.Plan);
            // The offsets the sweeps draw: carriageway edges, railing inner faces and deck edges (a shared side's clamp
            // and a squeezed walkway included).
            e = Math.Max(e, Dev(sp.HalfL[a], sp.HalfL[b], sp.HalfL[k], f) / tol.Width);
            e = Math.Max(e, Dev(sp.HalfR[a], sp.HalfR[b], sp.HalfR[k], f) / tol.Width);
            e = Math.Max(e, Dev(sp.InnerL(a), sp.InnerL(b), sp.InnerL(k), f) / tol.Width);
            e = Math.Max(e, Dev(sp.InnerR(a), sp.InnerR(b), sp.InnerR(k), f) / tol.Width);
            e = Math.Max(e, Dev(sp.EdgeL(a), sp.EdgeL(b), sp.EdgeL(k), f) / tol.Width);
            e = Math.Max(e, Dev(sp.EdgeR(a), sp.EdgeR(b), sp.EdgeR(k), f) / tol.Width);
            e = Math.Max(e, Dev(sp.Depth[a], sp.Depth[b], sp.Depth[k], f) / tol.Width);
            // A change between open span and fill ramp is a boundary the deck sweeps start and end on.
            if (sp.Fill[k] != sp.Fill[a] || sp.Fill[k] != sp.Fill[b]) e = Math.Max(e, 2.0);
            return e;
        }

        private static double Dev(double va, double vb, double vk, double f)
        {
            return Math.Abs(va + (vb - va) * f - vk);
        }

        private static bool Off(double[] v, int a, int b, int k, double f)
        {
            bool na = v[a] >= NoClamp * 0.5, nb = v[b] >= NoClamp * 0.5, nk = v[k] >= NoClamp * 0.5;
            if (na || nb || nk) return !(na && nb && nk);
            return Math.Abs(v[k] - (v[a] + (v[b] - v[a]) * f)) > 0.02;
        }

        /// <summary>Clears what the structure passes derive (so they can run again after stairs were dropped).</summary>
        private static void Reset(BridgeSpan sp)
        {
            sp.Openings.Clear();
            sp.Openings.AddRange(sp.BaseOpenings);
            sp.GapsL.Clear();
            sp.GapsR.Clear();
            sp.StartEnd = sp.StartCut ? SpanEnd.Cut : SpanEnd.Ground;
            sp.EndEnd = sp.EndCut ? SpanEnd.Cut : SpanEnd.Ground;
            sp.TrimStart = sp.TrimEnd = 0;
            sp.ShiftX = sp.ShiftZ = 0;
            sp.Stairs.Clear();
            sp.Piers.Clear();
            sp.Bents.Clear();
            sp.Lamps.Clear();
            sp.Abutments.Clear();
            sp.SuspA = sp.SuspB = double.NaN;
            sp.TowerA = sp.TowerB = false;
            sp.BackstayA = sp.BackstayB = 0;
            sp.GroupWay = sp.Record.OsmWayId;
            for (int i = 0; i < 4; i++) sp.EndPost[i] = 2;
        }

        /// <summary>Mapped stairs (with every stair chained to them) whose flights stand in a vehicle road's clearance
        /// envelope.</summary>
        private List<BridgeSpan> BlockedStairs()
        {
            var bad = new List<BridgeSpan>();
            foreach (BridgeSpan sp in Spans)
            {
                if (!sp.Stair || bad.Contains(sp)) continue;
                int[] p = sp.Record.Points;
                bool[] exclude = StairExclusions(sp.GroupWay, BridgeStructures.Key(p[2 * sp.FirstPoint], p[2 * sp.FirstPoint + 1]),
                                                 BridgeStructures.Key(p[2 * sp.LastPoint], p[2 * sp.LastPoint + 1]));
                foreach (BridgeStair st in sp.Stairs)
                {
                    if (!StairClear(st, exclude))
                    {
                        Chain(sp, bad);
                        break;
                    }
                }
            }
            return bad;
        }

        /// <summary>Add <paramref name="sp"/> and the stairs joined to it (transitively) to <paramref name="list"/>.</summary>
        private void Chain(BridgeSpan sp, List<BridgeSpan> list)
        {
            var todo = new Stack<BridgeSpan>();
            todo.Push(sp);
            while (todo.Count > 0)
            {
                BridgeSpan c = todo.Pop();
                if (list.Contains(c)) continue;
                list.Add(c);
                int[] pc = c.Record.Points;
                foreach (BridgeSpan o in Spans)
                {
                    if (!o.Stair || list.Contains(o)) continue;
                    int[] po = o.Record.Points;
                    bool joined = false;
                    for (int e = 0; e < 2 && !joined; e++)
                    {
                        int a = e == 0 ? c.FirstPoint : c.LastPoint;
                        for (int f = 0; f < 2 && !joined; f++)
                        {
                            int b = f == 0 ? o.FirstPoint : o.LastPoint;
                            joined = pc[2 * a] == po[2 * b] && pc[2 * a + 1] == po[2 * b + 1];
                        }
                    }
                    if (joined) todo.Push(o);
                }
            }
        }

        /// <summary>Roads a stair of the structure <paramref name="group"/> may stand in: its own pieces and the foot
        /// ways meeting it at <paramref name="nodeA"/> or <paramref name="nodeB"/> (the pavement it lands on); every other
        /// road, a footway passing under it included, must stay clear.</summary>
        private bool[] StairExclusions(ulong group, long nodeA, long nodeB)
        {
            var exclude = new bool[Tile.Roads.Count];
            foreach (BridgeSpan o in Spans)
                if (o.GroupWay == group) exclude[o.Road] = true;
            for (int j = 0; j < Tile.Roads.Count; j++)
            {
                if (exclude[j] || !BridgeStyle.IsFoot(Tile.Roads[j].RoadClass)) continue;
                int[] q = Tile.Roads[j].Points;
                for (int i = 0; i < q.Length && !exclude[j]; i += 2)
                {
                    long key = BridgeStructures.Key(q[i], q[i + 1]);
                    if (key == nodeA || key == nodeB) exclude[j] = true;
                }
            }
            return exclude;
        }

        /// <summary>True when no part of a flight (centre and both edges, from its soffit to a handrail above its
        /// surface) stands in a road not marked in <paramref name="exclude"/>.</summary>
        private bool StairClear(in BridgeStair st, bool[] exclude)
        {
            double nx = -st.Dz, nz = st.Dx;
            int na = Math.Max(1, (int)Math.Ceiling(st.Length / 0.4));
            for (int ia = 0; ia <= na; ia++)
            {
                double a = st.Length * ia / na; // (both ends of the flight exactly)
                float y = st.SurfaceAt(a);
                for (int i = -2; i <= 2; i++)
                {
                    // (Out to the stringer plates beyond the treads.)
                    double u = 0.5 * i * (st.HalfWidth + 0.15);
                    double x = st.X + st.Dx * a + nx * u, z = st.Z + st.Dz * a + nz * u;
                    if (Corridors.Conflict(x, z, y - 0.35f, y + 1.0f, exclude, true)) return false;
                }
            }
            return true;
        }

        /// <summary>A derived deck over no water that clears no road and stays near the ground is an at-grade road
        /// (the roads package draws it) unless a tile border cuts it; a span left without any structure is dropped.</summary>
        private bool Keep(BridgeSpan sp)
        {
            if (sp.Stair) return true;
            // (A piece cut by a tile border is kept: the water or the road it bridges may lie in the neighbour.)
            if (Derived && !sp.Water && !sp.Overbridge && sp.Crossings.Count == 0 && !sp.StartCut && !sp.EndCut)
            {
                float high = 0;
                for (int k = 0; k < sp.Count; k++) high = Math.Max(high, sp.Path.Y[k] - sp.Ground[k]);
                if (high < AtGradeM) return false;
            }
            double open = 0;
            foreach (BridgeGap g in sp.Openings) open += Math.Min(g.S1, sp.Path.End) - Math.Max(g.S0, sp.Path.Start);
            return sp.Length - open > 1.0;
        }

        private static bool AllSteep(RoadRecord r, float[] y, int a, int b)
        {
            for (int k = a; k < b; k++)
            {
                double len = BridgeStructures.SegLen(r.Points, k);
                if (len < 1e-6) continue;
                if (Math.Abs(y[k + 1] - y[k]) / len <= BridgeStyle.StairGrade) return false;
            }
            return true;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Span construction
        // ---------------------------------------------------------------------------------------------------------

        private BridgeSpan Build(TileData t, int ri, RoadStructureRecord rec, int k0, int k1, bool stair, bool trench)
        {
            RoadRecord r = t.Roads[ri];
            int[] p = r.Points;
            int count = p.Length / 2;
            int rf = r.HasPrevContext ? 1 : 0, rl = r.HasNextContext ? count - 2 : count - 1;
            bool preCtx = k0 == rf && r.HasPrevContext, postCtx = k1 == rl && r.HasNextContext;
            var along = new double[count];
            for (int k = k0 + 1; k < count; k++) along[k] = along[k - 1] + BridgeStructures.SegLen(p, k - 1);
            for (int k = k0 - 1; k >= 0; k--) along[k] = along[k + 1] - BridgeStructures.SegLen(p, k);
            if (along[k1] - along[k0] < (stair ? 0.8 : 2.0)) return null;

            var sp = new BridgeSpan { Road = ri, Record = r, Kind = rec.Kind, FirstPoint = k0, LastPoint = k1, Stair = stair };
            sp.Foot = BridgeStyle.IsFoot(r.RoadClass);
            sp.Overbridge = rec.Has(RoadStructureFlags.FootOverbridge) && !stair;
            sp.Water = rec.Has(RoadStructureFlags.WaterCrossing);
            RoadAttrRecord attr = _roads != null ? _roads.Attrs[ri] : default(RoadAttrRecord);
            double mx = t.Tile.X0 + 0.5 * (p[2 * k0] + p[2 * k1]) / 100.0, mz = t.Tile.Z0 + 0.5 * (p[2 * k0 + 1] + p[2 * k1 + 1]) / 100.0;
            sp.Heritage = sp.Foot && (attr.Has(RoadAttrFlags.HeritagePedestrian) || BridgeStyle.InHeritageZone(mx, mz));
            sp.StoneParapet = sp.Heritage && BridgeStyle.AtGhat(mx, mz);
            sp.Lit = !sp.Foot && (attr.Has(RoadAttrFlags.Lit) || RoadWidthModel.IsMajor(r.RoadClass) || rec.Kind == RoadStructureKind.Flyover);
            sp.Railing = BridgeStyle.RailingFor(r, rec, sp.Heritage);
            sp.Form = sp.Overbridge || stair || trench ? BridgeForm.Girder : BridgeStyle.FormOf(r.OsmWayId);
            if (sp.Form == BridgeForm.Suspension) sp.Railing = RailingStyle.SteelTruss;
            sp.Steel = BridgeStyle.SteelFor(r);
            sp.GroupWay = r.OsmWayId;
            sp.RailHeight = rec.RailingHeightM > 0.5f ? rec.RailingHeightM : sp.Foot ? 1.2f : BridgeStyle.RailHeightM;
            sp.RaisedWalk = !sp.Foot;
            int sizeCm = (int)Math.Round(t.Tile.Size * 100.0);
            sp.StartCut = preCtx && BorderOf(p[2 * k0], p[2 * k0 + 1], sizeCm) >= 0;
            sp.EndCut = postCtx && BorderOf(p[2 * k1], p[2 * k1 + 1], sizeCm) >= 0;
            sp.StartEnd = sp.StartCut ? SpanEnd.Cut : SpanEnd.Ground;
            sp.EndEnd = sp.EndCut ? SpanEnd.Cut : SpanEnd.Ground;
            float[] deck = rec.DeckY;
            if (preCtx && !float.IsNaN(deck[k0 - 1]))
            {
                sp.HasPre = true;
                sp.PreX = p[2 * k0 - 2] / 100.0;
                sp.PreZ = p[2 * k0 - 1] / 100.0;
                sp.PreY = deck[k0 - 1];
            }
            if (postCtx && !float.IsNaN(deck[k1 + 1]))
            {
                sp.HasPost = true;
                sp.PostX = p[2 * k1 + 2] / 100.0;
                sp.PostZ = p[2 * k1 + 3] / 100.0;
                sp.PostY = deck[k1 + 1];
            }

            // Crossings and openings first (their zone edges become stations).
            if (!trench && !stair) FindCrossings(t, ri, rec, along, k0, k1, sp);
            if (sp.Foot)
                for (int k = k0; k < k1; k++)
                {
                    double len = along[k + 1] - along[k];
                    if (stair || len > 1e-6 && Math.Abs(deck[k + 1] - deck[k]) / len > BridgeStyle.StairGrade)
                        AddMerged(sp.StairZones, along[k], along[k + 1], -1);
                }

            // Foot overbridges: an end inside the widened corridor of a road it crosses moves out of it (with room for
            // a stair landing), so neither the deck end nor its stairs stand in that road.
            if (sp.Overbridge)
            {
                if (!sp.StartCut && !FootJunction(p, k0)) sp.ExtStart = Extension(sp, along[k0], -1, p, k0, k0 + 1, t);
                if (!sp.EndCut && !FootJunction(p, k1)) sp.ExtEnd = Extension(sp, along[k1], +1, p, k1, k1 - 1, t);
            }

            // Stations: covered points, cut ends on the border, subdivisions and zone edges.
            BridgePath path = sp.Path;
            path.Clear();
            sp.PointStation = new int[k1 - k0 + 1];
            if (sp.ExtStart > 0)
            {
                AddExtension(path, p, k0, k0 + 1, along[k0] - sp.ExtStart, deck[k0], sp.ExtStart);
                ExtensionEdges(path, sp, p, k0, k0 + 1, along[k0], along[k0] - sp.ExtStart, deck[k0]);
            }
            for (int i = k0; i <= k1; i++)
            {
                double cx = p[2 * i] / 100.0, cz = p[2 * i + 1] / 100.0;
                if (i > k0)
                {
                    double ax = p[2 * i - 2] / 100.0, az = p[2 * i - 1] / 100.0;
                    double len = along[i] - along[i - 1];
                    if (len < 1e-6)
                    {
                        sp.PointStation[i - k0] = path.Count - 1;
                        continue;
                    }
                    double dx = (cx - ax) / len, dz = (cz - az) / len;
                    double sa = along[i - 1], sb = along[i];
                    int nsub = (int)Math.Ceiling(len / MaxStationM);
                    var cand = Scratch(nsub + 2 * (sp.Crossings.Count + sp.Openings.Count) + 2);
                    int nc = 0;
                    for (int k = 1; k < nsub; k++) cand[nc++] = -(sa + len * k / nsub); // negative: a subdivision
                    foreach (BridgeCrossing c in sp.Crossings)
                    {
                        AddEdge(cand, ref nc, c.S - c.HalfAlong, sa, sb);
                        AddEdge(cand, ref nc, c.S + c.HalfAlong, sa, sb);
                    }
                    foreach (BridgeGap g in sp.Openings)
                    {
                        AddEdge(cand, ref nc, g.S0, sa, sb);
                        AddEdge(cand, ref nc, g.S1, sa, sb);
                    }
                    // Sort by position (subdivisions are stored negated so they stay distinguishable).
                    Array.Sort(cand, 0, nc, AbsComparer.Instance);
                    double lastS = sa;
                    for (int k = 0; k < nc; k++)
                    {
                        double sc = Math.Abs(cand[k]);
                        bool key = cand[k] > 0;
                        // (Zone edges come within a centimetre of their neighbours: the structure thins exactly there.)
                        double near = key ? 0.01 : 0.05;
                        if (sc - lastS < near || sb - sc < near) continue;
                        double f = (sc - sa) / len;
                        float y = (float)(deck[i - 1] + (deck[i] - deck[i - 1]) * f);
                        path.Add(ax + (cx - ax) * f, az + (cz - az) * f, -dz, dx, -dz, dx, sc, y, key);
                        lastS = sc;
                    }
                }
                double tx, tz, miter;
                Tangent(p, count, i, k0 == rf ? 0 : k0, k1 == rl ? count - 1 : k1, out tx, out tz, out miter);
                double nx = -tz, nz = tx;
                bool cutEnd = i == k0 && sp.StartCut || i == k1 && sp.EndCut;
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
                sp.PointStation[i - k0] = path.Count - 1;
            }
            if (sp.ExtEnd > 0)
            {
                ExtensionEdges(path, sp, p, k1, k1 - 1, along[k1], along[k1] + sp.ExtEnd, deck[k1]);
                AddExtension(path, p, k1, k1 - 1, along[k1] + sp.ExtEnd, deck[k1], sp.ExtEnd);
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
            sp.ClampL = new double[n];
            sp.ClampR = new double[n];
            RoadWidthProfile prof = _roads != null ? _roads.Profiles[ri] : null;
            bool dual = attr.Has(RoadAttrFlags.Dual);
            double baseWalk = WalkFor(r.RoadClass, sp);
            float baseDepth = sp.Foot ? BridgeStyle.FootDepthM : rec.Kind == RoadStructureKind.Flyover ? BridgeStyle.FlyoverDepthM : BridgeStyle.RoadDepthM;
            double minClear = sp.Overbridge || sp.Foot && sp.Form == BridgeForm.Suspension ? BridgeStyle.FootOverbridgeWidthM
                : stair ? 1.2 : RoadClearance.MinCorridorM;
            for (int k = 0; k < n; k++)
            {
                double s = path.S[k];
                sp.Ground[k] = BridgeStructures.Ground(t, Ground, path.X[k], path.Z[k]);
                double w = prof != null ? prof.WidthAt(s + _roads.AlongAt(ri, k0)) : RoadStyle.WidthM(r);
                double shift = prof != null && dual ? 0.5 * (w - prof.RealM) : 0;
                if (sp.Overbridge || sp.Foot && sp.Form == BridgeForm.Suspension) w = Math.Max(Math.Min(w, 3.0), 1.6);
                if (stair) w = Math.Max(Math.Min(w, 2.4), 1.2);
                sp.HalfL[k] = 0.5 * w + shift;
                sp.HalfR[k] = 0.5 * w - shift;
                double extra = Math.Max(0, 0.5 * (minClear - (w + 2 * baseWalk)));
                sp.WalkL[k] = sp.WalkR[k] = baseWalk + extra;
                sp.ClampL[k] = sp.ClampR[k] = NoClamp;
                float depth = baseDepth;
                foreach (BridgeCrossing c in sp.Crossings)
                {
                    if (Math.Abs(s - c.S) > c.HalfAlong + 1e-3) continue;
                    float room = path.Y[k] - c.LowY - BridgeStyle.UnderClearanceM(Tile.Roads[c.Road].RoadClass);
                    depth = Math.Min(depth, Math.Max(BridgeStyle.MinDepthFor(sp.Foot), room));
                }
                sp.Depth[k] = depth;
            }
            for (int k = 0; k < n; k++)
            {
                double s = path.S[k];
                bool inCrossing = false;
                foreach (BridgeCrossing c in sp.Crossings)
                    if (Math.Abs(s - c.S) <= c.HalfAlong + 1e-3) inCrossing = true;
                sp.Fill[k] = rec.Kind == RoadStructureKind.Flyover && !sp.Foot && !inCrossing && !sp.InOpening(s) && path.Y[k] - sp.Ground[k] < BridgeStyle.FillMaxM;
            }
            return sp;
        }

        private static void AddEdge(double[] cand, ref int nc, double e, double sa, double sb)
        {
            if (e > sa + 0.01 && e < sb - 0.01) cand[nc++] = e;
        }

        /// <summary>True when road point k is shared with another foot deck (a junction of one structure).</summary>
        private bool FootJunction(int[] p, int k)
        {
            int c;
            return _footDeckPoints.TryGetValue(BridgeStructures.Key(p[2 * k], p[2 * k + 1]), out c) && c > 1;
        }

        /// <summary>The length a foot overbridge end at along <paramref name="sEnd"/> (dir −1 start, +1 end) must grow
        /// so it lies outside the corridor stretch of every road it crosses, plus room for a stair landing (0 when it
        /// already does), kept inside the tile.</summary>
        private static double Extension(BridgeSpan sp, double sEnd, int dir, int[] p, int k, int kIn, TileData t)
        {
            const double Landing = 2.8, MaxExt = 15.0;
            double need = double.NegativeInfinity;
            foreach (BridgeCrossing c in sp.Crossings)
                need = Math.Max(need, dir > 0 ? c.S + c.HalfAlong - sEnd : sEnd - (c.S - c.HalfAlong));
            if (double.IsNegativeInfinity(need) || need + Landing <= 0) return 0;
            double ext = Math.Min(MaxExt, need + Landing);
            // Stay inside the tile (the neighbour cannot answer for a deck it does not know).
            double ex = p[2 * k] / 100.0, ez = p[2 * k + 1] / 100.0;
            double dx = ex - p[2 * kIn] / 100.0, dz = ez - p[2 * kIn + 1] / 100.0, dl = Math.Sqrt(dx * dx + dz * dz);
            if (dl < 1e-6) return 0;
            dx /= dl;
            dz /= dl;
            double size = t.Tile.Size, room = MaxExt;
            if (dx > 1e-6) room = Math.Min(room, (size - 0.5 - ex) / dx);
            if (dx < -1e-6) room = Math.Min(room, (0.5 - ex) / dx);
            if (dz > 1e-6) room = Math.Min(room, (size - 0.5 - ez) / dz);
            if (dz < -1e-6) room = Math.Min(room, (0.5 - ez) / dz);
            return Math.Max(0, Math.Min(ext, room));
        }

        /// <summary>A key station <paramref name="ext"/> metres beyond road point k, straight on from point kIn, at the
        /// deck height of point k (a flat extension of a foot overbridge end).</summary>
        private static void AddExtension(BridgePath path, int[] p, int k, int kIn, double s, float y, double ext)
        {
            double ex = p[2 * k] / 100.0, ez = p[2 * k + 1] / 100.0;
            double dx = ex - p[2 * kIn] / 100.0, dz = ez - p[2 * kIn + 1] / 100.0, dl = Math.Sqrt(dx * dx + dz * dz);
            dx /= dl;
            dz /= dl;
            // The left normal of the travel direction (from the first point toward the last).
            double tx = kIn > k ? -dx : dx, tz = kIn > k ? -dz : dz;
            path.Add(ex + dx * ext, ez + dz * ext, -tz, tx, -tz, tx, s, y);
        }

        /// <summary>Key stations on a flat extension beyond road point k (from along sPoint toward sEnd) at the edges of
        /// the crossing and opening zones inside it, in path order, so the structure thins exactly over the roads
        /// there.</summary>
        private static void ExtensionEdges(BridgePath path, BridgeSpan sp, int[] p, int k, int kIn, double sPoint, double sEnd, float y)
        {
            var edges = new List<double>();
            double lo = Math.Min(sPoint, sEnd) + 0.01, hi = Math.Max(sPoint, sEnd) - 0.01;
            foreach (BridgeCrossing c in sp.Crossings)
            {
                if (c.S - c.HalfAlong > lo && c.S - c.HalfAlong < hi) edges.Add(c.S - c.HalfAlong);
                if (c.S + c.HalfAlong > lo && c.S + c.HalfAlong < hi) edges.Add(c.S + c.HalfAlong);
            }
            foreach (BridgeGap g in sp.Openings)
            {
                if (g.S0 > lo && g.S0 < hi) edges.Add(g.S0);
                if (g.S1 > lo && g.S1 < hi) edges.Add(g.S1);
            }
            edges.Sort(); // (path order: along grows from the start extension's far end through the points)
            foreach (double e in edges) AddExtension(path, p, k, kIn, e, y, Math.Abs(e - sPoint));
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

        /// <summary>Every drawn road the deck passes over in plan without a shared node (a lower road, whatever its
        /// height): a crossing where the deck clears it (structure thinned to keep the clearance), an opening where it
        /// does not (no structure over its corridor; on the tile's own records also a <see cref="BridgeIssue"/>).</summary>
        private void FindCrossings(TileData t, int ri, RoadStructureRecord rec, double[] along, int k0, int k1, BridgeSpan sp)
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
                double oAlong = 0;
                for (int b = 0; b + 1 < oc; b++) // context segments too: a crossing inside this tile counts here
                {
                    double blen = BridgeStructures.SegLen(q, b);
                    for (int a = k0; a < k1; a++)
                    {
                        if (!BridgeStructures.Crosses(p[2 * a], p[2 * a + 1], p[2 * a + 2], p[2 * a + 3], a == k0, a + 1 == k1, q[2 * b], q[2 * b + 1],
                                                      q[2 * b + 2], q[2 * b + 3])) continue;
                        double ax = p[2 * a] / 100.0, az = p[2 * a + 1] / 100.0, bx = p[2 * a + 2] / 100.0, bz = p[2 * a + 3] / 100.0;
                        double cx = q[2 * b] / 100.0, cz = q[2 * b + 1] / 100.0, dx = q[2 * b + 2] / 100.0, dz = q[2 * b + 3] / 100.0;
                        double ta, tb;
                        BridgeStructures.Intersect(ax, az, bx, bz, cx, cz, dx, dz, out ta, out tb);
                        double sAt = along[a] + (along[a + 1] - along[a]) * ta;
                        float deckY = (float)(deck[a] + (deck[a + 1] - deck[a]) * ta);
                        double ix = ax + (bx - ax) * ta, iz = az + (bz - az) * ta;
                        float lowY;
                        bool otherDeck = os.DeckY != null && os.DeckY.Length == oc && !float.IsNaN(os.DeckY[b]) && !float.IsNaN(os.DeckY[b + 1]);
                        if (otherDeck) lowY = (float)(os.DeckY[b] + (os.DeckY[b + 1] - os.DeckY[b]) * tb);
                        else lowY = BridgeStructures.Ground(t, Ground, ix, iz) + RoadMesher.LiftOf(o, DefaultRoadOptions);
                        // A deck passing over this one: its own crossing.
                        if (otherDeck && os.IsElevated && lowY > deckY) continue;
                        double sin, cos;
                        BridgeStructures.Angle(bx - ax, bz - az, dx - cx, dz - cz, out sin, out cos);
                        double lowHalf = 0.5 * BridgeStructures.CorridorWidth(_roads, o, j, (_roads != null ? _roads.AlongAt(j, b) : oAlong) + blen * tb) + 0.5;
                        double deckHalf = DeckHalf(ri, r, sAt, k0) + 0.3;
                        double half = lowHalf / sin + deckHalf * cos / sin;
                        float minDeck = MinDeck(deck, along, k0, k1, sAt - half, sAt + half);
                        float need = BridgeStyle.UnderClearanceM(o.RoadClass) + BridgeStyle.MinDepthFor(BridgeStyle.IsFoot(r.RoadClass));
                        // A record's deck a little short of the clearance (the pipeline measures from the terrain, the
                        // ribbon floats a lift above it) keeps its thinnest structure and is reported; a deck far short
                        // of it leaves the road open.
                        bool shortfall = minDeck - lowY < need - 1e-3f;
                        if (!shortfall || !Derived && minDeck - lowY >= need - BridgeStyle.IssueToleranceM)
                        {
                            sp.Crossings.Add(new BridgeCrossing { Road = j, S = sAt, HalfAlong = half, LowY = lowY, X = ix, Z = iz, LowHalfM = lowHalf });
                        }
                        else
                        {
                            double s0 = Math.Max(along[k0], sAt - half - OpeningMarginM), s1 = Math.Min(along[k1], sAt + half + OpeningMarginM);
                            AddMerged(sp.Openings, s0, s1, j);
                        }
                        if (shortfall && !Derived) Issues.Add(new BridgeIssue { Way = r.OsmWayId, LowerWay = o.OsmWayId, DeckAboveM = minDeck - lowY });
                    }
                    oAlong += blen;
                }
            }
        }

        /// <summary>Half the full deck width (carriageway, walkways, railing base and overhang) of road ri at along s
        /// from point k0, from the road's width profile, before the span's widths exist.</summary>
        private double DeckHalf(int ri, RoadRecord r, double s, int k0)
        {
            RoadWidthProfile prof = _roads != null ? _roads.Profiles[ri] : null;
            double w = prof != null ? prof.WidthAt(s + _roads.AlongAt(ri, k0)) : RoadStyle.WidthM(r);
            double walk = BridgeStyle.IsFoot(r.RoadClass) ? 0 : r.RoadClass <= RoadClass.Secondary ? 1.5 : r.RoadClass == RoadClass.Tertiary ? 1.2 : BridgeStyle.SafetyKerbM;
            double half = 0.5 * w + walk;
            if (!BridgeStyle.IsFoot(r.RoadClass)) half = Math.Max(half, 0.5 * RoadClearance.MinCorridorM);
            return half + BridgeStyle.RailBaseM + BridgeStyle.OverhangM;
        }

        private static float MinDeck(float[] deck, double[] along, int k0, int k1, double s0, double s1)
        {
            s0 = Math.Max(along[k0], s0);
            s1 = Math.Min(along[k1], s1);
            float min = float.PositiveInfinity;
            for (int k = k0; k < k1; k++)
            {
                double a = along[k], b = along[k + 1];
                if (b < s0 || a > s1 || b - a < 1e-9) continue;
                double f0 = Math.Max(0, (s0 - a) / (b - a)), f1 = Math.Min(1, (s1 - a) / (b - a));
                min = Math.Min(min, (float)(deck[k] + (deck[k + 1] - deck[k]) * f0));
                min = Math.Min(min, (float)(deck[k] + (deck[k + 1] - deck[k]) * f1));
            }
            return float.IsPositiveInfinity(min) ? deck[k0] : min;
        }

        /// <summary>Add [s0, s1] to an interval list, merging overlaps (kept sorted).</summary>
        private static void AddMerged(List<BridgeGap> l, double s0, double s1, int road)
        {
            if (s1 - s0 < 1e-6) return;
            for (int i = 0; i < l.Count; i++)
            {
                BridgeGap g = l[i];
                if (s1 < g.S0 - 1e-6 || s0 > g.S1 + 1e-6) continue;
                l.RemoveAt(i);
                AddMerged(l, Math.Min(s0, g.S0), Math.Max(s1, g.S1), g.Road >= 0 ? g.Road : road);
                return;
            }
            int at = 0;
            while (at < l.Count && l[at].S0 < s0) at++;
            l.Insert(at, new BridgeGap { S0 = s0, S1 = s1, Road = road });
        }

        // ---------------------------------------------------------------------------------------------------------
        // Connected structures: junctions, branches, stairs
        // ---------------------------------------------------------------------------------------------------------

        private struct PointRef
        {
            public int Span, Point;
        }

        /// <summary>Spans joined at a point form one structure: ends that meet another deck stay open, a branch opens
        /// the railing it leaves through and starts at the other deck's edge, a stair leaves from a landing (side
        /// gap and end railing when it leaves sideways), and foot overbridge ends with nothing mapped get a stair.</summary>
        private void Structure()
        {
            var at = new Dictionary<long, List<PointRef>>();
            for (int si = 0; si < Spans.Count; si++)
            {
                BridgeSpan sp = Spans[si];
                int[] p = sp.Record.Points;
                for (int k = sp.FirstPoint; k <= sp.LastPoint; k++)
                {
                    long key = BridgeStructures.Key(p[2 * k], p[2 * k + 1]);
                    List<PointRef> l;
                    if (!at.TryGetValue(key, out l)) at[key] = l = new List<PointRef>();
                    l.Add(new PointRef { Span = si, Point = k });
                }
            }
            // Group paint: the lowest way id of each connected structure.
            var parent = new int[Spans.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            foreach (List<PointRef> l in at.Values)
                for (int i = 1; i < l.Count; i++) Union(parent, l[0].Span, l[i].Span);
            for (int i = 0; i < Spans.Count; i++)
            {
                int g = Find(parent, i);
                if (Spans[i].Record.OsmWayId < Spans[g].GroupWay) Spans[g].GroupWay = Spans[i].Record.OsmWayId;
            }
            for (int i = 0; i < Spans.Count; i++)
            {
                BridgeSpan sp = Spans[i];
                sp.GroupWay = Spans[Find(parent, i)].GroupWay;
                sp.Steel = BridgeStyle.SteelFor(sp.GroupWay);
            }

            // Attachments: span b ends at a point of span a.
            var axial = new bool[Spans.Count, 2];
            var entries = new int[Spans.Count, 2];
            for (int bi = 0; bi < Spans.Count; bi++)
            {
                BridgeSpan b = Spans[bi];
                for (int end = 0; end < 2; end++)
                {
                    if (end == 0 ? b.StartCut : b.EndCut) continue;
                    int bp = end == 0 ? b.FirstPoint : b.LastPoint;
                    int[] pb = b.Record.Points;
                    List<PointRef> l;
                    if (!at.TryGetValue(BridgeStructures.Key(pb[2 * bp], pb[2 * bp + 1]), out l)) continue;
                    foreach (PointRef pr in l)
                    {
                        if (pr.Span == bi) continue;
                        BridgeSpan a = Spans[pr.Span];
                        if (a.Stair && b.Stair) continue; // chained flights
                        bool aEnd = pr.Point == a.FirstPoint && !a.StartCut || pr.Point == a.LastPoint && !a.EndCut;
                        double dx, dz;
                        LeaveDirection(b, end, out dx, out dz);
                        if (aEnd && !a.Stair && !b.Stair)
                        {
                            // End to end: both open (the other end is marked when its turn comes).
                            SetEnd(b, end, SpanEnd.Junction);
                            continue;
                        }
                        if (a.Stair) continue; // a deck ending on a stair: handled from the stair's side
                        int ka = a.PointStation[pr.Point - a.FirstPoint];
                        int aSide = !aEnd ? 0 : pr.Point == a.FirstPoint ? -1 : +1;
                        // An extended end: the stair leaves from the new end and moves with it.
                        if (aSide < 0 && a.ExtStart > 0 || aSide > 0 && a.ExtEnd > 0)
                        {
                            ka = aSide < 0 ? 0 : a.Count - 1;
                            if (b.Stair)
                            {
                                b.ShiftX = a.Path.X[ka] - pb[2 * bp] / 100.0;
                                b.ShiftZ = a.Path.Z[ka] - pb[2 * bp + 1] / 100.0;
                            }
                        }
                        double halfB = b.Stair ? 0.5 * (b.HalfL[0] + b.HalfR[0]) : Math.Max(b.EdgeL(end == 0 ? 0 : b.Count - 1), b.EdgeR(end == 0 ? 0 : b.Count - 1));
                        Entry entry = EntryAt(a, ka, aSide, dx, dz, halfB);
                        if (entry.Axial)
                        {
                            axial[pr.Span, aSide < 0 ? 0 : 1] = true;
                        }
                        else
                        {
                            AddMerged(entry.Side > 0 ? a.GapsL : a.GapsR, entry.S - entry.GapHalf, entry.S + entry.GapHalf, b.Road);
                        }
                        if (aSide != 0) entries[pr.Span, aSide < 0 ? 0 : 1]++;
                        SetEnd(b, end, SpanEnd.Junction);
                        if (b.Stair)
                        {
                            b.Stairs.Add(new BridgeStair { X = entry.X, Z = entry.Z, TopY = entry.Y, Steps = -1, Landings = end }); // top override (see Flights)
                        }
                        else if (!entry.Axial)
                        {
                            if (end == 0) b.TrimStart = Math.Min(entry.Exit, 0.45 * b.Length);
                            else b.TrimEnd = Math.Min(entry.Exit, 0.45 * b.Length);
                        }
                    }
                }
            }
            for (int ai = 0; ai < Spans.Count; ai++)
            {
                BridgeSpan a = Spans[ai];
                for (int end = 0; end < 2; end++)
                {
                    if (entries[ai, end] == 0 || (end == 0 ? a.StartEnd : a.EndEnd) == SpanEnd.Junction) continue;
                    SetEnd(a, end, axial[ai, end] ? SpanEnd.Junction : SpanEnd.Landing);
                }
            }
            // A moved stair moves the rest of its chain with it.
            for (int changed = 1, pass = 0; changed > 0 && pass < 8; pass++)
            {
                changed = 0;
                for (int bi = 0; bi < Spans.Count; bi++)
                {
                    BridgeSpan b = Spans[bi];
                    if (!b.Stair || b.ShiftX == 0 && b.ShiftZ == 0) continue;
                    int[] pb = b.Record.Points;
                    for (int end = 0; end < 2; end++)
                    {
                        int bp = end == 0 ? b.FirstPoint : b.LastPoint;
                        List<PointRef> l;
                        if (!at.TryGetValue(BridgeStructures.Key(pb[2 * bp], pb[2 * bp + 1]), out l)) continue;
                        foreach (PointRef pr in l)
                        {
                            BridgeSpan c = Spans[pr.Span];
                            if (!c.Stair || pr.Span == bi || c.ShiftX != 0 || c.ShiftZ != 0) continue;
                            c.ShiftX = b.ShiftX;
                            c.ShiftZ = b.ShiftZ;
                            changed++;
                        }
                    }
                }
            }
        }

        private static void SetEnd(BridgeSpan s, int end, SpanEnd e)
        {
            if (end == 0) s.StartEnd = e;
            else s.EndEnd = e;
        }

        /// <summary>Unit plan direction in which span b leaves its end (into the span).</summary>
        private static void LeaveDirection(BridgeSpan b, int end, out double dx, out double dz)
        {
            BridgePath p = b.Path;
            int k = end == 0 ? 0 : p.Count - 1, j = end == 0 ? 1 : p.Count - 2;
            dx = p.X[j] - p.X[k];
            dz = p.Z[j] - p.Z[k];
            double l = Math.Sqrt(dx * dx + dz * dz);
            if (l < 1e-9)
            {
                dx = 1;
                dz = 0;
                return;
            }
            dx /= l;
            dz /= l;
        }

        /// <summary>Where something leaving deck a at station k in direction d (half width hw) passes its edge.</summary>
        private struct Entry
        {
            public bool Axial;
            public int Side;

            /// <summary>Along a, the gap centre and half length.</summary>
            public double S, GapHalf;

            /// <summary>The point on a's deck edge (or end face) where it leaves, and the deck height there.</summary>
            public double X, Z;
            public float Y;

            /// <summary>Distance from the junction point along d to the deck edge.</summary>
            public double Exit;
        }

        /// <summary>
        /// The entry through which a stair or branch leaves deck <paramref name="a"/>: at an end (aEnd −1 start, +1
        /// end) it leaves through the open end face when it points outward along the deck (axial); otherwise through
        /// a gap in the side railing it heads for, centred where its line crosses the deck edge and moved back from
        /// the end so the whole width fits along the side.
        /// </summary>
        private Entry EntryAt(BridgeSpan a, int k, int aEnd, double dx, double dz, double hw)
        {
            var e = new Entry();
            double s = a.Path.S[k];
            double px, pz, ux, uz, nx, nz, tx, tz;
            float y;
            a.Path.Frame(s, out px, out pz, out y, out ux, out uz, out nx, out nz, out tx, out tz);
            float walk = a.RaisedWalk ? BridgeStyle.KerbHeightM : 0f;
            if (aEnd != 0)
            {
                double outAxis = (dx * tx + dz * tz) * aEnd;
                if (outAxis >= 0.7)
                {
                    e.Axial = true;
                    e.S = s;
                    e.X = px;
                    e.Z = pz;
                    e.Y = y + walk;
                    return e;
                }
            }
            double lat = dx * nx + dz * nz, lon = dx * tx + dz * tz;
            e.Side = lat >= 0 ? 1 : -1;
            double alat = Math.Max(Math.Abs(lat), 0.35);
            double edge = e.Side > 0 ? a.EdgeL(k) : a.EdgeR(k);
            e.Exit = edge / alat;
            e.GapHalf = Math.Min(3.0, hw / alat) + 0.1;
            double sc = s + lon * e.Exit;
            double lo = a.Path.Start + e.GapHalf + 0.05, hi = a.Path.End - e.GapHalf - 0.05;
            sc = lo <= hi ? Math.Max(lo, Math.Min(hi, sc)) : 0.5 * (a.Path.Start + a.Path.End);
            e.S = sc;
            double cx, cz, cux, cuz, cnx, cnz, ctx, ctz;
            float cy;
            a.Path.Frame(sc, out cx, out cz, out cy, out cux, out cuz, out cnx, out cnz, out ctx, out ctz);
            double edgeC = e.Side > 0 ? a.Lerp(a.HalfL, sc) + a.Lerp(a.WalkL, sc) : a.Lerp(a.HalfR, sc) + a.Lerp(a.WalkR, sc);
            edgeC += BridgeStyle.RailBaseM + BridgeStyle.OverhangM;
            e.X = cx + cnx * e.Side * edgeC;
            e.Z = cz + cnz * e.Side * edgeC;
            e.Y = cy + walk;
            return e;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Parallel decks, styles
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Twin decks (the two carriageways of a dual road mapped as separate bridges, or a sidewalk bridge
        /// beside a road bridge) whose edges would meet or overlap share that side: the deck stops halfway between the
        /// centrelines and a median barrier replaces the railing, so no railing stands in the other deck's lanes.</summary>
        private void SharedSides()
        {
            for (int a = 0; a < Spans.Count; a++)
            {
                BridgeSpan sa = Spans[a];
                if (sa.Stair) continue;
                int sharedL = 0, sharedR = 0, partnerL = -1, partnerR = -1;
                var midL = new double[sa.Count];
                var midR = new double[sa.Count];
                for (int k = 0; k < sa.Count; k++)
                {
                    midL[k] = midR[k] = double.PositiveInfinity;
                    double x = sa.Path.X[k], z = sa.Path.Z[k], nx = sa.Path.Nx[k], nz = sa.Path.Nz[k];
                    for (int b = 0; b < Spans.Count; b++)
                    {
                        if (b == a || Spans[b].Stair) continue;
                        BridgeSpan sb = Spans[b];
                        double d, side, otherEdge;
                        float oy;
                        if (!Nearest(sb, x, z, nx, nz, out d, out side, out otherEdge, out oy)) continue;
                        if (Math.Abs(oy - sa.Path.Y[k]) > 1.5f) continue;
                        double mine = side > 0 ? sa.HalfL[k] + sa.WalkL[k] + BridgeStyle.RailBaseM + BridgeStyle.OverhangM
                            : sa.HalfR[k] + sa.WalkR[k] + BridgeStyle.RailBaseM + BridgeStyle.OverhangM;
                        if (d > mine + otherEdge + 0.6) continue;
                        if (side > 0 && 0.5 * d < midL[k])
                        {
                            midL[k] = 0.5 * d;
                            partnerL = b;
                        }
                        else if (side < 0 && 0.5 * d < midR[k])
                        {
                            midR[k] = 0.5 * d;
                            partnerR = b;
                        }
                    }
                    if (!double.IsPositiveInfinity(midL[k])) sharedL++;
                    if (!double.IsPositiveInfinity(midR[k])) sharedR++;
                }
                sa.SharedL = sharedL * 2 >= sa.Count && sharedL > 0;
                sa.SharedR = sharedR * 2 >= sa.Count && sharedR > 0;
                sa.PartnerL = sa.SharedL ? partnerL : -1;
                sa.PartnerR = sa.SharedR ? partnerR : -1;
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

        /// <summary>A sidewalk deck that shares a side with a vehicle deck takes that deck's railing and paint.</summary>
        private void Styles()
        {
            foreach (BridgeSpan sp in Spans)
            {
                if (!sp.Foot || sp.Stair || sp.Overbridge) continue;
                int partner = sp.PartnerL >= 0 && !Spans[sp.PartnerL].Foot ? sp.PartnerL : sp.PartnerR >= 0 && !Spans[sp.PartnerR].Foot ? sp.PartnerR : -1;
                if (partner < 0) continue;
                sp.Railing = Spans[partner].Railing;
                sp.Steel = Spans[partner].Steel;
                sp.Form = BridgeForm.Girder;
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
                double par = Math.Abs(sx * -nz + sz * nx) / sl;
                if (par < 0.85) continue;
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
                double bnx = -sz / sl, bnz = sx / sl;
                bool facingLeft = (bnx * -nx + bnz * -nz) * side > 0;
                otherEdge = (facingLeft ? el + wl : er + wr) + BridgeStyle.RailBaseM + BridgeStyle.OverhangM;
                oy = (float)(p.Y[k] + (p.Y[j] - p.Y[k]) * u);
            }
            return !double.IsPositiveInfinity(best);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Other roads beside and under the structure
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Keeps the structure out of every other road's clearance envelope (its corridor, from its surface to
        /// <see cref="RoadClearance.MinOverheadClearanceM"/> above), not only the roads crossing the centreline: where
        /// a road beside the deck (a street meeting the approach at the bridge head, a riverside road under the deck
        /// edge) reaches the railing line at grade, that side's railing and walkway open (a gap); where the deck slab
        /// itself overhangs it, the structure opens over it entirely. The span's own approaches, the other pieces of
        /// its structure and a twin deck beside it do not count.
        /// </summary>
        private void ClearSides(BridgeSpan sp)
        {
            bool[] exclude = SideExclusions(sp);
            // Sampled every quarter metre; an interval runs from the last free sample before it to the first free one
            // after it, padded by the footprint of a railing post, so nothing at its edges stands in the road.
            // The edges of the openings already there are sampled too, so a structure end at one is checked.
            const double Step = 0.25, Pad = 0.2;
            float top = sp.RaisedWalk ? BridgeStyle.KerbHeightM : 0f;
            var samples = new List<double>();
            for (double s = sp.Path.Start; s < sp.Path.End; s += Step) samples.Add(s);
            foreach (BridgeGap g in sp.Openings)
            {
                if (g.S0 - 0.01 > sp.Path.Start) samples.Add(g.S0 - 0.01);
                if (g.S1 + 0.01 < sp.Path.End) samples.Add(g.S1 + 0.01);
            }
            samples.Add(sp.Path.End);
            samples.Sort();
            double openFrom = double.NaN, prev = sp.Path.Start - Step;
            double[] gapFrom = { double.NaN, double.NaN };
            int[] gapRoad = { -1, -1 };
            for (int si0 = 0; si0 < samples.Count; si0++)
            {
                double s = samples[si0];
                bool last = si0 == samples.Count - 1;
                bool open = false, gapL = false, gapR = false;
                if (!sp.InOpening(s)) Ring(sp, s, exclude, top, false, out open, out gapL, out gapR);
                if (gapL && gapRoad[1] < 0) gapRoad[1] = _ringRoadL;
                if (gapR && gapRoad[0] < 0) gapRoad[0] = _ringRoadR;
                if (open && double.IsNaN(openFrom)) openFrom = prev - Pad;
                if ((!open || last) && !double.IsNaN(openFrom))
                {
                    AddMerged(sp.Openings, Math.Max(sp.Path.Start, openFrom), Math.Min(sp.Path.End, s + Pad), -1);
                    openFrom = double.NaN;
                }
                for (int si = 0; si < 2; si++)
                {
                    bool g = (si == 0 ? gapR : gapL) && !open;
                    if (g && double.IsNaN(gapFrom[si])) gapFrom[si] = prev - Pad;
                    if ((!g || last) && !double.IsNaN(gapFrom[si]))
                    {
                        AddMerged(si == 0 ? sp.GapsR : sp.GapsL, Math.Max(sp.Path.Start, gapFrom[si]), Math.Min(sp.Path.End, s + Pad), gapRoad[si]);
                        gapFrom[si] = double.NaN;
                        gapRoad[si] = -1;
                    }
                }
                prev = s;
            }
            // Then every boundary (and each span end) is checked across the whole ring, densely: a structure end standing
            // in a road between two samples, or where a bend mitres the end ring, moves on until its ring is clear.
            Polish(sp, sp.Openings, 0, exclude, top);
            Polish(sp, sp.GapsL, 1, exclude, top);
            Polish(sp, sp.GapsR, -1, exclude, top);
        }

        /// <summary>The roads a span's structure may touch: its own, the other pieces of its structure, a twin deck on
        /// a shared side and the approaches (roads meeting it at a real end).</summary>
        private bool[] SideExclusions(BridgeSpan sp)
        {
            var exclude = new bool[Tile.Roads.Count];
            exclude[sp.Road] = true;
            foreach (BridgeSpan o in Spans)
                if (o.GroupWay == sp.GroupWay) exclude[o.Road] = true;
            if (sp.PartnerL >= 0) exclude[Spans[sp.PartnerL].Road] = true;
            if (sp.PartnerR >= 0) exclude[Spans[sp.PartnerR].Road] = true;
            int[] p = sp.Record.Points;
            long k0 = BridgeStructures.Key(p[2 * sp.FirstPoint], p[2 * sp.FirstPoint + 1]), k1 = BridgeStructures.Key(p[2 * sp.LastPoint], p[2 * sp.LastPoint + 1]);
            for (int j = 0; j < Tile.Roads.Count; j++)
            {
                if (exclude[j]) continue;
                int[] q = Tile.Roads[j].Points;
                for (int i = 0; i < q.Length; i += 2)
                {
                    long key = BridgeStructures.Key(q[i], q[i + 1]);
                    if (key == k0 && !sp.StartCut || key == k1 && !sp.EndCut) exclude[j] = true;
                }
            }
            return exclude;
        }

        /// <summary>Height above the walkway of the tallest railing part along a run (posts and caps reach a little
        /// above the top rail), checked against the roads beside the deck.</summary>
        internal static float RailReach(BridgeSpan sp)
        {
            return sp.RailHeight + (sp.Railing == RailingStyle.CrashBarrier ? 0.15f : 0.18f);
        }

        /// <summary>Height above the walkway of a real end's pillar with its cap and finial.</summary>
        internal static float PillarReach(BridgeSpan sp)
        {
            return sp.RailHeight + 0.35f + 0.42f;
        }

        /// <summary>Plan position of the railing line of a side at along s (the railing base centre), as the mesher
        /// places posts.</summary>
        internal static void RailPoint(BridgeSpan sp, double s, int side, out double x, out double z, out float y)
        {
            double px, pz, ux, uz, nx, nz, tx, tz;
            sp.Path.Frame(s, out px, out pz, out y, out ux, out uz, out nx, out nz, out tx, out tz);
            double half = side > 0 ? sp.Lerp(sp.HalfL, s) : sp.Lerp(sp.HalfR, s);
            double walk = side > 0 ? sp.Lerp(sp.WalkL, s) : sp.Lerp(sp.WalkR, s);
            double clamp = side > 0 ? sp.Lerp(sp.ClampL, s) : sp.Lerp(sp.ClampR, s);
            double u = side * (Math.Min(half + walk, clamp - BridgeStyle.RailBaseM) + 0.5 * BridgeStyle.RailBaseM);
            x = px + nx * u;
            z = pz + nz * u;
        }

        /// <summary>True when a vertical element of plan half size <paramref name="half"/> at (x, z), from y0 to y1,
        /// stands in a road's clearance envelope (not in <paramref name="exclude"/>).</summary>
        private bool ElementBlocked(double x, double z, double half, float y0, float y1, bool[] exclude)
        {
            for (int i = -1; i <= 1; i++)
            for (int j = -1; j <= 1; j++)
                if (Corridors.Conflict(x + i * half, z + j * half, y0, y1, exclude)) return true;
            return false;
        }

        /// <summary>The end pillars of the railings (at real ends): the full pillar where it keeps out of every road
        /// beside the bridge head, else a plain post of railing height, else none.</summary>
        private void EndPosts(BridgeSpan sp)
        {
            bool[] exclude = null;
            float top = sp.RaisedWalk ? BridgeStyle.KerbHeightM : 0f;
            for (int end = 0; end < 2; end++)
            {
                SpanEnd e = end == 0 ? sp.StartEnd : sp.EndEnd;
                if (e != SpanEnd.Ground && e != SpanEnd.Landing) continue;
                double se = end == 0 ? sp.Path.Start : sp.Path.End;
                double s = end == 0 ? se + 0.3 : se - 0.3;
                for (int side = -1; side <= 1; side += 2)
                {
                    if (!sp.HasRail(side, s)) continue;
                    if (exclude == null) exclude = SideExclusions(sp);
                    double x, z;
                    float y;
                    RailPoint(sp, s, side, out x, out z, out y);
                    int i = end * 2 + (side > 0 ? 1 : 0);
                    if (!ElementBlocked(x, z, 0.3, y + top, y + top + PillarReach(sp), exclude)) sp.EndPost[i] = 2;
                    else if (!ElementBlocked(x, z, 0.2, y + top, y + top + RailReach(sp), exclude)) sp.EndPost[i] = 1;
                    else sp.EndPost[i] = 0;
                }
            }
        }

        /// <summary>Clear-side state of the ring at along s: the deck (soffit to surface) or the railing (walkway to
        /// handrail) in another road's clearance envelope on either side. Sampled at the carriageway edge, the railing
        /// line and just past the deck edge, or (dense) every quarter metre across, along the mitred offset the sweeps
        /// use.</summary>
        private int _ringRoadL = -1, _ringRoadR = -1;

        private void Ring(BridgeSpan sp, double s, bool[] exclude, float top, bool dense, out bool open, out bool gapL, out bool gapR)
        {
            open = gapL = gapR = false;
            _ringRoadL = _ringRoadR = -1;
            double px, pz, ux, uz, nx, nz, tx, tz;
            float y;
            sp.Path.Frame(s, out px, out pz, out y, out ux, out uz, out nx, out nz, out tx, out tz);
            float soffit = y - Math.Max(0.35f, sp.Lerp(sp.Depth, s));
            for (int si = 0; si < 2; si++)
            {
                int side = si == 0 ? -1 : 1;
                double c = side > 0 ? sp.Lerp(sp.HalfL, s) : sp.Lerp(sp.HalfR, s);
                double walk = side > 0 ? sp.Lerp(sp.WalkL, s) : sp.Lerp(sp.WalkR, s);
                double clamp = side > 0 ? sp.Lerp(sp.ClampL, s) : sp.Lerp(sp.ClampR, s);
                double w = Math.Min(c + walk, clamp - BridgeStyle.RailBaseM), e = Math.Min(c + walk + BridgeStyle.RailBaseM + BridgeStyle.OverhangM, clamp);
                int n = dense ? Math.Max(3, (int)Math.Ceiling((e + 0.05) / 0.25)) : 3;
                for (int i = 0; i < n; i++)
                {
                    double u = dense ? (e + 0.05) * (i + 1) / n : i == 0 ? c + 0.15 : i == 1 ? w + 0.5 * BridgeStyle.RailBaseM : e + 0.05;
                    double qx = px + ux * side * u, qz = pz + uz * side * u;
                    if (Corridors.Conflict(qx, qz, soffit, y - 0.03f, exclude)) open = true;
                    else if (u > c && Corridors.Conflict(qx, qz, y + 0.02f, y + top + RailReach(sp), exclude))
                    {
                        if (side > 0)
                        {
                            gapL = true;
                            _ringRoadL = Corridors.LastRoad;
                        }
                        else
                        {
                            gapR = true;
                            _ringRoadR = Corridors.LastRoad;
                        }
                    }
                }
            }
        }

        /// <summary>Grow each interval of <paramref name="list"/> (openings for side 0, else that side's gaps) while
        /// the ring at its ends is still in a road, and start one at a span end that is.</summary>
        private void Polish(BridgeSpan sp, List<BridgeGap> list, int side, bool[] exclude, float top)
        {
            const double Step = 0.1, MaxGrow = 3.0, Pad = 0.15;
            var grown = new List<BridgeGap>();
            foreach (BridgeGap g in list)
            {
                double a = g.S0, b = g.S1;
                for (double d = 0; d < MaxGrow && a > sp.Path.Start && Blocked(sp, a, side, exclude, top); d += Step) a = Math.Max(sp.Path.Start, a - Step);
                for (double d = 0; d < MaxGrow && b < sp.Path.End && Blocked(sp, b, side, exclude, top); d += Step) b = Math.Min(sp.Path.End, b + Step);
                if (a > g.S0 - 1e-9 && b < g.S1 + 1e-9) grown.Add(g);
                else grown.Add(new BridgeGap { S0 = Math.Max(sp.Path.Start, a < g.S0 ? a - Pad : a), S1 = Math.Min(sp.Path.End, b > g.S1 ? b + Pad : b), Road = g.Road });
            }
            for (int end = 0; end < 2; end++)
            {
                double s0 = end == 0 ? sp.Path.Start : sp.Path.End;
                if (sp.InOpening(s0) || side != 0 && sp.InGap(side, s0) || !Blocked(sp, s0, side, exclude, top)) continue;
                double s1 = s0;
                int dir = end == 0 ? 1 : -1;
                for (double d = 0; d < MaxGrow && Blocked(sp, s1, side, exclude, top); d += Step) s1 += dir * Step;
                s1 += dir * Pad;
                grown.Add(new BridgeGap { S0 = Math.Min(s0, s1), S1 = Math.Max(s0, s1), Road = -1 });
            }
            list.Clear();
            foreach (BridgeGap g in grown) AddMerged(list, g.S0, g.S1, g.Road);
        }

        private bool Blocked(BridgeSpan sp, double s, int side, bool[] exclude, float top)
        {
            bool open, gapL, gapR;
            Ring(sp, s, exclude, top, true, out open, out gapL, out gapR);
            return side == 0 ? open : !open && (side > 0 ? gapL : gapR);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Intervals
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>The deck runs (the span minus openings, stair stretches and junction trims) and each side's
        /// railing runs (the deck runs minus that side's gaps).</summary>
        private static void Runs(BridgeSpan sp)
        {
            sp.DeckRuns.Clear();
            sp.RailRunsL.Clear();
            sp.RailRunsR.Clear();
            if (sp.Stair) return;
            var cuts = new List<BridgeGap>(sp.Openings);
            foreach (BridgeGap g in sp.StairZones) AddMerged(cuts, g.S0, g.S1, -1);
            if (sp.TrimStart > 0) AddMerged(cuts, sp.Path.Start - 1, sp.Path.Start + sp.TrimStart, -1);
            if (sp.TrimEnd > 0) AddMerged(cuts, sp.Path.End - sp.TrimEnd, sp.Path.End + 1, -1);
            // (A sliver of deck between two openings is no structure: runs shorter than 1.5 m are dropped.)
            Subtract(sp.Path.Start, sp.Path.End, cuts, sp.DeckRuns, 1.5);
            for (int side = -1; side <= 1; side += 2)
            {
                List<double> rails = side > 0 ? sp.RailRunsL : sp.RailRunsR;
                List<BridgeGap> gaps = side > 0 ? sp.GapsL : sp.GapsR;
                for (int i = 0; i + 1 < sp.DeckRuns.Count; i += 2)
                {
                    var tmp = new List<double>();
                    Subtract(sp.DeckRuns[i], sp.DeckRuns[i + 1], gaps, tmp, 0.3);
                    rails.AddRange(tmp);
                }
            }
        }

        /// <summary>[s0, s1] minus the (sorted, merged) intervals, keeping pieces of at least <paramref name="min"/>.</summary>
        private static void Subtract(double s0, double s1, List<BridgeGap> cuts, List<double> result, double min)
        {
            double a = s0;
            foreach (BridgeGap g in cuts)
            {
                if (g.S1 <= a || g.S0 >= s1) continue;
                if (g.S0 - a >= min)
                {
                    result.Add(a);
                    result.Add(g.S0);
                }
                a = Math.Max(a, g.S1);
            }
            if (s1 - a >= min)
            {
                result.Add(a);
                result.Add(s1);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Supports, lamps and stairs
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Steep stretches inside a foot deck (the records step a deck between two crossing levels): a flight
        /// across the deck's clear width from the higher end down to the lower one, so the walk stays continuous.</summary>
        private void DeckFlights(BridgeSpan sp)
        {
            bool[] exclude = null;
            foreach (BridgeGap g in sp.StairZones)
            {
                // (Not where the structure leaves a road open, nor where a flight would stand in another road.)
                bool open = false;
                foreach (BridgeGap o in sp.Openings)
                    if (o.S1 > g.S0 && o.S0 < g.S1) open = true;
                if (open) continue;
                float y0 = sp.Path.YAt(g.S0), y1 = sp.Path.YAt(g.S1);
                bool down = y0 >= y1;
                double sTop = down ? g.S0 : g.S1, len = g.S1 - g.S0;
                if (len < 0.2) continue;
                double px, pz, ux, uz, nx, nz, tx, tz;
                float y;
                sp.Path.Frame(sTop, out px, out pz, out y, out ux, out uz, out nx, out nz, out tx, out tz);
                int k = sp.StationAt(0.5 * (g.S0 + g.S1));
                double il = sp.InnerL(k), ir = sp.InnerR(k), mid = 0.5 * (il - ir), hw = Math.Max(0.5, 0.5 * (il + ir));
                double dx = down ? tx : -tx, dz = down ? tz : -tz;
                BridgeStair st = BridgeStair.Fit(px + nx * mid, pz + nz * mid, dx, dz, down ? y0 : y1, down ? y1 : y0, len, hw);
                if (exclude == null) exclude = SideExclusions(sp);
                if (StairClear(st, exclude)) sp.Stairs.Add(st);
            }
        }

        private void Supports(TileData t, BridgeSpan sp)
        {
            BridgePath path = sp.Path;
            int n = path.Count;
            if (sp.Foot) DeckFlights(sp);
            double wx0 = t.Tile.X0, wz0 = t.Tile.Z0;
            bool piers = sp.Form == BridgeForm.Girder;
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
                if (fillBefore && sp.HasDeck(s0) && !BentBlocked(sp, s0)) sp.Bents.Add(s0);
                if (fillAfter && sp.HasDeck(s1) && !BentBlocked(sp, s1)) sp.Bents.Add(s1);
                if (!piers) continue;
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
            Abutments(sp);
            if (sp.Form == BridgeForm.Suspension) SuspensionSupports(sp);
            // Lamps: alternate sides every half spacing (both sides on wide decks), never on a shared side or a gap.
            if (sp.Lit && !sp.Overbridge)
            {
                bool[] exclude = SideExclusions(sp);
                float top = sp.RaisedWalk ? BridgeStyle.KerbHeightM : 0f;
                float pole = (float)LampHeightM(sp) + 1.6f;
                double spacing = BridgeStyle.LampSpacingM * 0.5;
                AnchoredStations(path, path.Start, path.End, spacing, wx0, wz0, _anchors);
                bool wide = sp.HalfL[0] + sp.HalfR[0] > 14;
                foreach (double s in _anchors)
                {
                    if (!sp.StartCut && s - path.Start < 4) continue;
                    if (!sp.EndCut && path.End - s < 4) continue;
                    long idx = BridgeKit.StripeIndex(path, s + 1e-3, spacing, wx0, wz0);
                    int side = (idx & 1) == 0 ? 1 : -1;
                    bool okThis = (side > 0 ? !sp.SharedL : !sp.SharedR) && sp.HasRail(side, s) && LampClear(sp, s, side, top, pole, exclude);
                    bool okOther = (side > 0 ? !sp.SharedR : !sp.SharedL) && sp.HasRail(-side, s) && LampClear(sp, s, -side, top, pole, exclude);
                    if (okThis) sp.Lamps.Add(new BridgeLamp { S = s, Side = side });
                    if (okOther && (wide || !okThis)) sp.Lamps.Add(new BridgeLamp { S = s, Side = -side });
                }
            }
            // Foot overbridge ends with nothing mapped: a stair down to the ground.
            if (sp.Overbridge)
            {
                if (sp.StartEnd == SpanEnd.Ground) GeneratedStair(sp, 0);
                if (sp.EndEnd == SpanEnd.Ground) GeneratedStair(sp, 1);
            }
        }

        /// <summary>Height of a span's lamp poles above their pilaster (8 m on flyovers and major roads, else 6.5 m).</summary>
        internal static double LampHeightM(BridgeSpan sp)
        {
            return sp.Kind == RoadStructureKind.Flyover || RoadWidthModel.IsMajor(sp.Record.RoadClass) ? 8.0 : 6.5;
        }

        /// <summary>True when a lamp's pilaster and pole at along s on a side keep out of the roads beside the deck.</summary>
        private bool LampClear(BridgeSpan sp, double s, int side, float top, float pole, bool[] exclude)
        {
            double x, z;
            float y;
            RailPoint(sp, s, side, out x, out z, out y);
            return !ElementBlocked(x, z, 0.25, y + top, y + top + pole, exclude);
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
            // Keep out of the corridors of the roads below (crossings and openings): move to the nearer zone edge, or drop.
            for (int it = 0; it < 2; it++)
            {
                foreach (BridgeCrossing c in sp.Crossings)
                {
                    double half = c.HalfAlong + 1.2;
                    if (Math.Abs(s - c.S) >= half) continue;
                    double lo = c.S - half, hi = c.S + half;
                    s = s - lo < hi - s ? lo : hi;
                }
                foreach (BridgeGap g in sp.Openings)
                {
                    double lo = g.S0 - 1.2, hi = g.S1 + 1.2;
                    if (s <= lo || s >= hi) continue;
                    s = s - lo < hi - s ? lo : hi;
                }
            }
            foreach (BridgeCrossing c in sp.Crossings)
                if (Math.Abs(s - c.S) < c.HalfAlong + 1.1) return;
            foreach (BridgeGap g in sp.Openings)
                if (s > g.S0 - 1.1 && s < g.S1 + 1.1) return;
            if (!sp.HasDeck(s)) return;
            if (s <= s0 + MinGap * 0.5 || s >= s1 - MinGap * 0.5) return;
            if (s < path.Start || s > path.End) return;
            foreach (double q in sp.Piers)
                if (Math.Abs(q - s) < MinGap) return;
            float y = path.YAt(s), g0 = sp.Lerp(sp.Ground, s);
            if (y - sp.Lerp(sp.Depth, s) - g0 < 1.0f) return; // the deck sits on the ground here
            // The pier footprint (at most the deck's width, 1.6 m along) must not stand in another road.
            double px, pz, ux, uz, nx, nz, tx, tz;
            float py;
            path.Frame(s, out px, out pz, out py, out ux, out uz, out nx, out nz, out tx, out tz);
            int k = sp.StationAt(s);
            double el = sp.EdgeL(k), er = sp.EdgeR(k);
            int across = Math.Max(2, (int)Math.Ceiling((el + er) / 0.5));
            for (int a = -1; a <= 1; a++)
            for (int i = 0; i <= across; i++)
            {
                double u = -er + (el + er) * i / across, along = 0.8 * a;
                if (Corridors.Blocks(px + nx * u + tx * along, pz + nz * u + tz * along, g0 - 1, y - 0.5f, sp.Road, -1)) return;
            }
            sp.Piers.Add(s);
            sp.Piers.Sort();
        }

        /// <summary>Abutments at the real ends and end walls at the edges of openings, each moved back or dropped so
        /// it stands in no other road; wing walls cut short before another road's corridor.</summary>
        private void Abutments(BridgeSpan sp)
        {
            if (sp.Overbridge || sp.Form == BridgeForm.Suspension) return;
            for (int i = 0; i + 1 < sp.DeckRuns.Count; i += 2)
            {
                for (int e = 0; e < 2; e++)
                {
                    double s = sp.DeckRuns[i + e];
                    int dir = e == 0 ? 1 : -1;
                    bool spanStart = Math.Abs(s - sp.Path.Start) < 1e-6, spanEnd = Math.Abs(s - sp.Path.End) < 1e-6;
                    bool real = spanStart && sp.StartEnd == SpanEnd.Ground || spanEnd && sp.EndEnd == SpanEnd.Ground;
                    if ((spanStart || spanEnd) && !real) continue; // cut, junction or landing
                    float y = sp.Path.YAt(s), depth = sp.Lerp(sp.Depth, s);
                    float g = sp.Lerp(sp.Ground, s);
                    if (!real && !sp.InOpening(s - dir * 0.5)) continue; // a stair stretch or a junction trim
                    if (!real && y - depth - g < 0.6f) continue; // the deck rests on the ground at an opening edge
                    int endPoint = spanStart ? sp.FirstPoint : spanEnd ? sp.LastPoint : -1;
                    if (FootprintBlocked(sp, s, dir, endPoint)) continue;
                    var ab = new BridgeAbutment { S = s, Dir = dir, Wings = real };
                    if (real)
                    {
                        ab.WingL = WingLength(sp, s, dir, +1, endPoint);
                        ab.WingR = WingLength(sp, s, dir, -1, endPoint);
                    }
                    sp.Abutments.Add(ab);
                }
            }
        }

        /// <summary>True when the abutment block at along s (1.1 m deep toward the span) stands in another road.</summary>
        private bool FootprintBlocked(BridgeSpan sp, double s, int dir, int endPoint)
        {
            double px, pz, ux, uz, nx, nz, tx, tz;
            float y;
            sp.Path.Frame(s, out px, out pz, out y, out ux, out uz, out nx, out nz, out tx, out tz);
            int k = sp.StationAt(s);
            double el = sp.EdgeL(k), er = sp.EdgeR(k);
            float g = sp.Lerp(sp.Ground, s);
            // The block and its bearing shelf: from 0.15 m behind the deck end to 1.25 m under the span, 0.35 m past
            // the deck edges.
            for (int a = 0; a <= 3; a++)
            for (int c = -2; c <= 2; c++)
            {
                double along = dir * (-0.15 + 0.4667 * a), lat = c >= 0 ? (el + 0.35) * c / 2.0 : (er + 0.35) * c / 2.0;
                if (Corridors.Blocks(px + tx * along + nx * lat, pz + tz * along + nz * lat, g - 1.5f, y, sp.Road, endPoint >= 0 ? EndKey(sp, endPoint) : -1))
                    return true;
            }
            return false;
        }

        /// <summary>Tower insets from a suspension run end tried (nearest first) and backstay lengths tried (longest
        /// first).</summary>
        private static readonly double[] TowerInsetM = { 0.4, 1.0, 1.6, 2.2, 2.8 };
        private static readonly double[] BackstayM = { 8.0, 6.0, 4.0 };

        /// <summary>A suspension footbridge's towers and anchors: over the deck run holding the lowest ground (the
        /// river; consistent between two tiles cutting the span), a tower pair near each end of it where it stands clear
        /// of other roads, a backstay behind it to an anchor block as long as the roads around allow. SuspA/SuspB end
        /// up at the towers (the run ends where there is none).</summary>
        private void SuspensionSupports(BridgeSpan sp)
        {
            double a = double.NaN, b = double.NaN;
            float lowest = float.MaxValue;
            for (int k = 0; k < sp.Count; k++)
            {
                double s = sp.Path.S[k];
                if (sp.Ground[k] >= lowest) continue;
                for (int r = 0; r + 1 < sp.DeckRuns.Count; r += 2)
                {
                    if (s < sp.DeckRuns[r] - 1e-6 || s > sp.DeckRuns[r + 1] + 1e-6 || sp.DeckRuns[r + 1] - sp.DeckRuns[r] < 8.0) continue;
                    lowest = sp.Ground[k];
                    a = sp.DeckRuns[r];
                    b = sp.DeckRuns[r + 1];
                }
            }
            if (double.IsNaN(a)) return;
            sp.SuspA = a;
            sp.SuspB = b;
            for (int end = 0; end < 2; end++)
            {
                double se = end == 0 ? a : b;
                bool cut = end == 0 ? Math.Abs(a - sp.Path.Start) < 1e-6 && sp.StartCut : Math.Abs(b - sp.Path.End) < 1e-6 && sp.EndCut;
                if (cut) continue;
                int dir = end == 0 ? -1 : 1; // outward
                foreach (double inset in TowerInsetM)
                {
                    double ts = se - dir * inset;
                    if (b - a - 2 * inset < 6.0) break;
                    double px, pz, ux, uz, nx, nz, tx, tz;
                    float y;
                    sp.Path.Frame(ts, out px, out pz, out y, out ux, out uz, out nx, out nz, out tx, out tz);
                    int k = sp.StationAt(ts);
                    float g = sp.Lerp(sp.Ground, ts);
                    bool tower = true;
                    for (int side = -1; side <= 1 && tower; side += 2)
                    {
                        double off = side > 0 ? sp.EdgeL(k) + 0.18 : -(sp.EdgeR(k) + 0.18);
                        double ox = px + nx * off, oz = pz + nz * off;
                        for (int i = -1; i <= 1 && tower; i++)
                        for (int j = -1; j <= 1 && tower; j++)
                            if (Corridors.Blocks(ox + tx * 0.5 * i + nx * 0.5 * j, oz + tz * 0.5 * i + nz * 0.5 * j, g - 0.8f, y + 13f, sp.Road, -1))
                                tower = false;
                    }
                    if (!tower) continue;
                    double stay = 0;
                    foreach (double len in BackstayM)
                    {
                        bool clear = true;
                        for (int side = -1; side <= 1 && clear; side += 2)
                        {
                            double off = side > 0 ? sp.EdgeL(k) + 0.18 : -(sp.EdgeR(k) + 0.18);
                            double ox = px + nx * off, oz = pz + nz * off;
                            double bx = ox + tx * dir * len + nx * side * 0.6, bz = oz + tz * dir * len + nz * side * 0.6;
                            float gb = BridgeStructures.Ground(Tile, Ground, bx, bz);
                            // The anchor block and the low half of the stay.
                            for (int i = -1; i <= 1 && clear; i++)
                            for (int j = -1; j <= 1 && clear; j++)
                                if (Corridors.Blocks(bx + tx * 0.7 * i + nx * 0.6 * j, bz + tz * 0.7 * i + nz * 0.6 * j, gb - 0.6f, gb + 0.5f, sp.Road, -1))
                                    clear = false;
                            for (double f = 0.5; f < 1.0 && clear; f += 0.1)
                            {
                                float cy = (float)(y + 12.0 * (1 - f) + (gb + 0.5 - y) * f);
                                if (Corridors.Blocks(ox + (bx - ox) * f, oz + (bz - oz) * f, cy - 0.1f, cy + 0.1f, sp.Road, -1)) clear = false;
                            }
                        }
                        if (!clear) continue;
                        stay = len;
                        break;
                    }
                    if (end == 0)
                    {
                        sp.SuspA = ts;
                        sp.TowerA = true;
                        sp.BackstayA = stay;
                    }
                    else
                    {
                        sp.SuspB = ts;
                        sp.TowerB = true;
                        sp.BackstayB = stay;
                    }
                    break;
                }
            }
        }

        /// <summary>True when the bent (a cross wall under the deck where the fill ends) at along s stands in another
        /// road.</summary>
        private bool BentBlocked(BridgeSpan sp, double s)
        {
            double px, pz, ux, uz, nx, nz, tx, tz;
            float y;
            sp.Path.Frame(s, out px, out pz, out y, out ux, out uz, out nx, out nz, out tx, out tz);
            int k = sp.StationAt(s);
            double el = sp.EdgeL(k), er = sp.EdgeR(k);
            float g = sp.Lerp(sp.Ground, s);
            for (int a = -1; a <= 1; a++)
            for (int c = -2; c <= 2; c++)
            {
                double along = 0.5 * a, lat = c >= 0 ? (el + 0.1) * c / 2.0 : (er + 0.1) * c / 2.0;
                if (Corridors.Blocks(px + tx * along + nx * lat, pz + tz * along + nz * lat, g - 0.6f, y - sp.Lerp(sp.Depth, s), sp.Road, -1))
                    return true;
            }
            return false;
        }

        /// <summary>Length of a wing wall (up to 10 m, 30° splayed back along the approach) before it would enter
        /// another road's corridor.</summary>
        private double WingLength(BridgeSpan sp, double s, int dir, int side, int endPoint)
        {
            double px, pz, ux, uz, nx, nz, tx, tz;
            float y;
            sp.Path.Frame(s, out px, out pz, out y, out ux, out uz, out nx, out nz, out tx, out tz);
            int k = sp.StationAt(s);
            double off = side > 0 ? sp.EdgeL(k) + 0.05 : -(sp.EdgeR(k) + 0.05);
            double sx = px + nx * off, sz = pz + nz * off;
            double dx = -tx * dir * 0.866 + nx * side * 0.5, dz = -tz * dir * 0.866 + nz * side * 0.5;
            float gFront = sp.Lerp(sp.Ground, s + dir * 3.0);
            double len = Math.Max(2.0, Math.Min(10.0, 1.6 * (y - gFront)));
            long key = endPoint >= 0 ? EndKey(sp, endPoint) : -1;
            float g = sp.Lerp(sp.Ground, s);
            for (double d = 0.5; d <= len; d += 0.5)
                if (Corridors.Blocks(sx + dx * d, sz + dz * d, g - 1.5f, y, sp.Road, key))
                    return Math.Max(0, d - 0.6);
            return len;
        }

        private static long EndKey(BridgeSpan sp, int point)
        {
            return BridgeStructures.Key(sp.Record.Points[2 * point], sp.Record.Points[2 * point + 1]);
        }

        /// <summary>A stair from a foot overbridge end with nothing mapped: along the first at-grade foot way leaving
        /// the end (it leads down to the pavement), else straight on along the deck.</summary>
        private void GeneratedStair(BridgeSpan sp, int end)
        {
            BridgePath path = sp.Path;
            int k = end == 0 ? 0 : path.Count - 1;
            float g = sp.Ground[k];
            float top = path.Y[k];
            if (top - g < 1.0f) return;
            double ox = end == 0 ? path.X[0] - path.X[1] : path.X[k] - path.X[k - 1], oz = end == 0 ? path.Z[0] - path.Z[1] : path.Z[k] - path.Z[k - 1];
            double ol = Math.Sqrt(ox * ox + oz * oz);
            if (ol < 1e-6) return;
            ox /= ol;
            oz /= ol;
            // Candidate directions: along the first at-grade foot way leaving the end (it leads down to the
            // pavement), to either side, straight on. The first whose flight keeps out of the vehicle roads wins.
            var dirs = new List<double>();
            int pt = end == 0 ? sp.FirstPoint : sp.LastPoint;
            int[] p = sp.Record.Points;
            int px = p[2 * pt], pz = p[2 * pt + 1];
            for (int j = 0; j < Tile.Roads.Count && dirs.Count == 0; j++)
            {
                RoadRecord o = Tile.Roads[j];
                if (j == sp.Road || !BridgeStyle.IsFoot(o.RoadClass) || Structures[j].DeckY != null || !RoadMesher.IsDrawn(o, null)) continue;
                int[] q = o.Points;
                int oc = q.Length / 2;
                for (int i = 0; i < oc && dirs.Count == 0; i++)
                {
                    if (q[2 * i] != px || q[2 * i + 1] != pz) continue;
                    int nb = i + 1 < oc ? i + 1 : i - 1;
                    double fx = (q[2 * nb] - px) / 100.0, fz = (q[2 * nb + 1] - pz) / 100.0, fl = Math.Sqrt(fx * fx + fz * fz);
                    if (fl < 0.5) continue;
                    dirs.Add(fx / fl);
                    dirs.Add(fz / fl);
                }
            }
            dirs.Add(-oz);
            dirs.Add(ox);
            dirs.Add(oz);
            dirs.Add(-ox);
            dirs.Add(ox);
            dirs.Add(oz);
            double hw = Math.Max(0.6, Math.Min(Math.Min(sp.InnerL(k), sp.InnerR(k)), 1.2));
            bool[] exclude = StairExclusions(sp.GroupWay, BridgeStructures.Key(px, pz), -1);
            Entry best = default(Entry);
            BridgeStair bestStair = default(BridgeStair);
            bool found = false;
            for (int c = 0; c + 1 < dirs.Count && !found; c += 2)
            {
                double dx = dirs[c], dz = dirs[c + 1];
                Entry e = EntryAt(sp, k, end == 0 ? -1 : 1, dx, dz, hw);
                // Iterate the bottom height: the ground where the flight lands.
                float bottom = g;
                BridgeStair st = default(BridgeStair);
                for (int it = 0; it < 3; it++)
                {
                    st = BridgeStair.Standard(e.X, e.Z, dx, dz, e.Y, Math.Max(0.3f, e.Y - bottom), hw);
                    bottom = BridgeStructures.Ground(Tile, Ground, e.X + dx * st.Length, e.Z + dz * st.Length);
                }
                if (c == 0 || StairClear(st, exclude))
                {
                    best = e;
                    bestStair = st;
                }
                found = StairClear(st, exclude);
            }
            if (!best.Axial)
            {
                AddMerged(best.Side > 0 ? sp.GapsL : sp.GapsR, best.S - best.GapHalf, best.S + best.GapHalf, -1);
                SetEnd(sp, end, SpanEnd.Landing);
                Runs(sp);
            }
            sp.Stairs.Add(bestStair);
        }

        /// <summary>The flights of a mapped stair: one per segment from its higher to its lower point, the top of the
        /// flight that leaves a deck moved to the deck edge where it leaves (at the deck height), landing pads at
        /// turns.</summary>
        private void Flights(BridgeSpan sp)
        {
            // Top overrides recorded by Structure() (Steps = −1 markers): Landings holds the end (0 start, 1 end).
            bool hasTop0 = false, hasTop1 = false;
            BridgeStair top0 = default(BridgeStair), top1 = default(BridgeStair);
            foreach (BridgeStair m in sp.Stairs)
            {
                if (m.Steps != -1) continue;
                if (m.Landings == 0 && (!hasTop0 || m.TopY > top0.TopY))
                {
                    top0 = m;
                    hasTop0 = true;
                }
                if (m.Landings == 1 && (!hasTop1 || m.TopY > top1.TopY))
                {
                    top1 = m;
                    hasTop1 = true;
                }
            }
            sp.Stairs.Clear();
            int[] p = sp.Record.Points;
            float[] y = Structures[sp.Road].DeckY;
            double hw = Math.Max(0.6, Math.Min(1.2, 0.5 * (sp.HalfL[0] + sp.HalfR[0])));
            double pdx = 0, pdz = 0;
            bool hasPrev = false;
            bool[] exclude = null;
            for (int k = sp.FirstPoint; k < sp.LastPoint; k++)
            {
                double ax = p[2 * k] / 100.0 + sp.ShiftX, az = p[2 * k + 1] / 100.0 + sp.ShiftZ;
                double bx = p[2 * k + 2] / 100.0 + sp.ShiftX, bz = p[2 * k + 3] / 100.0 + sp.ShiftZ;
                float ya = y[k], yb = y[k + 1];
                if (k == sp.FirstPoint && hasTop0)
                {
                    ax = top0.X;
                    az = top0.Z;
                    ya = top0.TopY;
                }
                if (k + 1 == sp.LastPoint && hasTop1)
                {
                    bx = top1.X;
                    bz = top1.Z;
                    yb = top1.TopY;
                }
                bool down = ya >= yb;
                double tx = down ? ax : bx, tz = down ? az : bz, ex = down ? bx : ax, ez = down ? bz : az;
                float hi = down ? ya : yb, lo = down ? yb : ya;
                double dx = ex - tx, dz = ez - tz, l = Math.Sqrt(dx * dx + dz * dz);
                if (l < 0.2) continue;
                dx /= l;
                dz /= l;
                // The free bottom end of the stair still above the ground (a chain mapped too short for a walkable
                // grade): run the flight on at its grade down to the ground, or, where that would run into a road,
                // turn back on a landing into a return flight beside it (a dog-leg, as overbridge stairs are built).
                int lowPoint = down ? k + 1 : k;
                bool dog = false;
                BridgeStair f1 = default(BridgeStair), pad = default(BridgeStair), f2 = default(BridgeStair);
                if ((lowPoint == sp.LastPoint && !hasTop1 || lowPoint == sp.FirstPoint && !hasTop0) && !Chained(sp, lowPoint))
                {
                    if (exclude == null)
                        exclude = StairExclusions(sp.GroupWay, BridgeStructures.Key(p[2 * sp.FirstPoint], p[2 * sp.FirstPoint + 1]),
                                                  BridgeStructures.Key(p[2 * sp.LastPoint], p[2 * sp.LastPoint + 1]));
                    float lift = RoadMesher.LiftOf(sp.Record, DefaultRoadOptions);
                    float g = BridgeStructures.Ground(Tile, Ground, ex, ez) + lift;
                    double grade = (hi - lo) / l;
                    if (lo - g > 0.1f && grade > 0.05)
                    {
                        double extra = Math.Min(20.0, (lo - g) / grade);
                        float lo2 = (float)Math.Max(g, lo - grade * extra);
                        BridgeStair straight = BridgeStair.Fit(tx, tz, dx, dz, hi, lo2, l + extra, hw);
                        dog = !StairClear(straight, exclude) && DogLeg(tx, tz, dx, dz, hi, lo, l, hw, lift, exclude, out f1, out pad, out f2);
                        if (!dog)
                        {
                            l += extra;
                            lo = lo2;
                        }
                    }
                    // A foot that lands on a carriageway (the steps mapped to the road's centre line or kerb) is pulled
                    // back to its edge, onto the footpath.
                    double back = 0;
                    while (!dog && back < 0.4 * l && OnCarriageway(tx + dx * (l - back), tz + dz * (l - back), -dz, dx, hw, lo, exclude)) back += 0.25;
                    if (!dog && back > 0 && back < 0.4 * l)
                    {
                        l -= back;
                        lo = (float)Math.Min(hi - 0.2, BridgeStructures.Ground(Tile, Ground, tx + dx * l, tz + dz * l) + lift);
                    }
                }
                // A landing pad at a turn between flights.
                if (hasPrev && Math.Abs(pdx * dz - pdz * dx) > 0.34) sp.Stairs.Add(BridgeStair.Pad(ax, az, pdx, pdz, ya, hw));
                if (dog)
                {
                    sp.Stairs.Add(f1);
                    sp.Stairs.Add(pad);
                    sp.Stairs.Add(f2);
                }
                else sp.Stairs.Add(BridgeStair.Fit(tx, tz, dx, dz, hi, lo, l, hw));
                pdx = down ? dx : -dx;
                pdz = down ? dz : -dz;
                hasPrev = true;
            }
        }

        /// <summary>A dog-leg for a mapped flight whose free foot (at <paramref name="lo"/>, l along (dx, dz) from its top)
        /// is still above the ground and cannot run on: the flight ends a landing short of its mapped foot, the landing
        /// fills the last stretch of the mapped way, and a standard return flight goes back down beside the first (on
        /// whichever side keeps clear of the roads around).</summary>
        private bool DogLeg(double tx, double tz, double dx, double dz, float hi, float lo, double l, double hw, float lift, bool[] exclude,
                            out BridgeStair f1, out BridgeStair pad, out BridgeStair f2)
        {
            f1 = pad = f2 = default(BridgeStair);
            double land = BridgeStyle.StairLandingM;
            if (l < land + 1.0) return false;
            double nx = -dz, nz = dx;
            double fx = tx + dx * (l - land), fz = tz + dz * (l - land); // the end of the first flight
            for (int side = -1; side <= 1; side += 2)
            {
                double off = 2 * hw + 0.1;
                double sx = fx + nx * side * off, sz = fz + nz * side * off;
                float bottom = BridgeStructures.Ground(Tile, Ground, sx, sz) + lift;
                BridgeStair back = default(BridgeStair);
                for (int it = 0; it < 3; it++)
                {
                    back = BridgeStair.Standard(sx, sz, -dx, -dz, lo, Math.Max(0.3f, lo - bottom), hw);
                    bottom = BridgeStructures.Ground(Tile, Ground, sx - dx * back.Length, sz - dz * back.Length) + lift;
                }
                back.Mapped = true;
                var landing = new BridgeStair
                {
                    X = fx + nx * side * 0.5 * off, Z = fz + nz * side * 0.5 * off, Dx = dx, Dz = dz, TopY = lo, BottomY = lo, HalfWidth = hw + 0.5 * off,
                    Length = land, Mapped = true,
                };
                if (!StairClear(back, exclude) || !StairClear(landing, exclude)) continue;
                f1 = BridgeStair.Fit(tx, tz, dx, dz, hi, lo, l - land, hw);
                pad = landing;
                f2 = back;
                return true;
            }
            return false;
        }

        /// <summary>True when the foot of a flight at (x, z) (its centre or either edge, half width hw, across
        /// (nx, nz)) stands on the carriageway of a road with traffic not in <paramref name="exclude"/>.</summary>
        private bool OnCarriageway(double x, double z, double nx, double nz, double hw, float y, bool[] exclude)
        {
            for (int i = -1; i <= 1; i++)
                if (Corridors.Conflict(x + nx * i * hw, z + nz * i * hw, y - 0.35f, y + 1.0f, exclude, true)) return true;
            return false;
        }

        /// <summary>True when another stair continues from point <paramref name="k"/> of stair span sp.</summary>
        private bool Chained(BridgeSpan sp, int k)
        {
            int x = sp.Record.Points[2 * k], z = sp.Record.Points[2 * k + 1];
            foreach (BridgeSpan o in Spans)
            {
                if (o == sp || !o.Stair) continue;
                int[] q = o.Record.Points;
                if (q[2 * o.FirstPoint] == x && q[2 * o.FirstPoint + 1] == z || q[2 * o.LastPoint] == x && q[2 * o.LastPoint + 1] == z) return true;
            }
            return false;
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

        // ---------------------------------------------------------------------------------------------------------
        // Other roads' corridors (keeping supports out of them)
        // ---------------------------------------------------------------------------------------------------------

        private CorridorGrid Corridors
        {
            get { return _corridors ?? (_corridors = new CorridorGrid(this)); }
        }

        /// <summary>The drawn roads' corridors (carriageway, footpaths and shoulders, at least
        /// <see cref="RoadClearance.MinCorridorM"/>) in a coarse grid, with their surface heights, for intrusion tests.
        /// Built on first use by the support placement (inside the layout's constructor, so single-threaded).</summary>
        private sealed class CorridorGrid
        {
            private const double CellM = 32.0;
            private readonly int _cells;
            private readonly List<int>[] _grid;
            private readonly List<double> _seg = new List<double>(); // ax, az, bx, bz, half, carriageway half
            private readonly List<float> _y = new List<float>(); // y at a and b
            private readonly List<int> _road = new List<int>();
            private readonly List<bool> _deck = new List<bool>();
            private readonly List<float> _env = new List<float>(); // envelope height over the surface
            private readonly List<float> _lift = new List<float>(); // draped segments: ribbon lift over the ground (NaN: deck)
            private readonly BridgeLayout _l;
            private readonly TileData _t;

            public CorridorGrid(BridgeLayout l)
            {
                _t = l.Tile;
                _l = l;
                _cells = Math.Max(1, (int)Math.Ceiling(_t.Tile.Size / CellM));
                _grid = new List<int>[_cells * _cells];
                for (int j = 0; j < _t.Roads.Count; j++)
                {
                    RoadRecord o = _t.Roads[j];
                    if ((o.Flags & RoadFlags.Tunnel) != 0 || !RoadMesher.IsDrawn(o, null)) continue;
                    RoadStructureRecord os = l.Structures[j];
                    int[] q = o.Points;
                    int oc = q.Length / 2;
                    float lift = RoadMesher.LiftOf(o, DefaultRoadOptions);
                    double along = 0;
                    // Every segment, the context ones reaching over the tile border included: the part of a road's
                    // corridor inside this tile is there whichever tile renders its ribbon.
                    for (int b = 0; b + 1 < oc; b++)
                    {
                        double ax = q[2 * b] / 100.0, az = q[2 * b + 1] / 100.0, bx = q[2 * b + 2] / 100.0, bz = q[2 * b + 3] / 100.0;
                        double len = BridgeStructures.SegLen(q, b);
                        double mid = (l._roads != null ? l._roads.AlongAt(j, b) : along) + 0.5 * len;
                        double half = 0.5 * BridgeStructures.CorridorWidth(l._roads, o, j, mid);
                        double carriage = 0.5 * BridgeStructures.CarriageWidth(l._roads, o, j, mid);
                        along += len;
                        float ya, yb, drape;
                        bool deck = os.IsElevated && os.DeckY.Length == oc && !float.IsNaN(os.DeckY[b]) && !float.IsNaN(os.DeckY[b + 1]);
                        if (os.DeckY != null && os.DeckY.Length == oc && !float.IsNaN(os.DeckY[b]) && !float.IsNaN(os.DeckY[b + 1]))
                        {
                            ya = os.DeckY[b];
                            yb = os.DeckY[b + 1];
                            drape = float.NaN;
                        }
                        else
                        {
                            // A draped ribbon follows the ground between its points: its surface is sampled where asked.
                            ya = yb = 0;
                            drape = lift;
                        }
                        int id = _road.Count;
                        _seg.Add(ax);
                        _seg.Add(az);
                        _seg.Add(bx);
                        _seg.Add(bz);
                        _seg.Add(half);
                        _seg.Add(carriage);
                        _env.Add(BridgeStyle.EnvelopeM(o.RoadClass));
                        _lift.Add(drape);
                        _y.Add(ya);
                        _y.Add(yb);
                        _road.Add(j);
                        _deck.Add(deck);
                        int i0 = Clamp((int)Math.Floor((Math.Min(ax, bx) - half) / CellM)), i1 = Clamp((int)Math.Floor((Math.Max(ax, bx) + half) / CellM));
                        int j0 = Clamp((int)Math.Floor((Math.Min(az, bz) - half) / CellM)), j1 = Clamp((int)Math.Floor((Math.Max(az, bz) + half) / CellM));
                        for (int cj = j0; cj <= j1; cj++)
                        for (int ci = i0; ci <= i1; ci++)
                        {
                            int c = cj * _cells + ci;
                            if (_grid[c] == null) _grid[c] = new List<int>();
                            _grid[c].Add(id);
                        }
                    }
                }
            }

            private int Clamp(int c)
            {
                return c < 0 ? 0 : c >= _cells ? _cells - 1 : c;
            }

            /// <summary>True when (x, z) lies in the corridor of a road other than <paramref name="self"/> (and other than
            /// the roads ending or passing at the node <paramref name="node"/>, the approaches) whose clearance
            /// envelope (from just under its surface to <see cref="BridgeStyle.EnvelopeM"/> above it) overlaps the
            /// element from <paramref name="y0"/> to <paramref name="y1"/>.</summary>
            public bool Blocks(double x, double z, float y0, float y1, int self, long node)
            {
                int ci = Clamp((int)Math.Floor(x / CellM)), cj = Clamp((int)Math.Floor(z / CellM));
                List<int> l = _grid[cj * _cells + ci];
                if (l == null) return false;
                foreach (int id in l)
                {
                    int j = _road[id];
                    if (j == self) continue;
                    if (node >= 0 && Touches(j, node)) continue;
                    float f;
                    if (!Inside(id, x, z, false, out f)) continue;
                    float low = Low(id, f);
                    // A parallel deck at the same level is a structure beside this one, not a road to keep clear.
                    if (_deck[id] && Math.Abs(low - y1) < 2.0f) continue;
                    if (y1 > low - 0.5f && y0 < low + _env[id]) return true;
                }
                return false;
            }

            /// <summary>True when (x, z) lies in the corridor of a road not marked in <paramref name="exclude"/> whose
            /// clearance envelope, from 0.3 m over its surface (an at-grade meeting is no obstruction) to
            /// <see cref="BridgeStyle.EnvelopeM"/>, overlaps <paramref name="y0"/>..<paramref name="y1"/>. With
            /// <paramref name="carriageway"/> only the carriageway and shoulders of a road with traffic count (a stair
            /// may land on its footpath).</summary>
            public bool Conflict(double x, double z, float y0, float y1, bool[] exclude, bool carriageway = false)
            {
                int ci = Clamp((int)Math.Floor(x / CellM)), cj = Clamp((int)Math.Floor(z / CellM));
                List<int> l = _grid[cj * _cells + ci];
                if (l == null) return false;
                foreach (int id in l)
                {
                    if (exclude[_road[id]]) continue;
                    float f;
                    if (!Inside(id, x, z, carriageway, out f)) continue;
                    float low = Low(id, f);
                    if (y1 > low + 0.3f && y0 < low + _env[id])
                    {
                        LastRoad = _road[id];
                        return true;
                    }
                }
                return false;
            }

            /// <summary>The road the last <see cref="Conflict"/> that returned true found (the layout is built on one
            /// thread).</summary>
            public int LastRoad = -1;

            /// <summary>Surface of segment id at position f along it: the deck, or the ground under a draped ribbon.</summary>
            private float Low(int id, float f)
            {
                float drape = _lift[id];
                if (float.IsNaN(drape)) return _y[2 * id] + (_y[2 * id + 1] - _y[2 * id]) * f;
                double ax = _seg[6 * id], az = _seg[6 * id + 1], bx = _seg[6 * id + 2], bz = _seg[6 * id + 3];
                return BridgeStructures.Ground(_t, _l.Ground, ax + (bx - ax) * f, az + (bz - az) * f) + drape;
            }

            /// <summary>(x, z) within the corridor (or carriageway) of segment id; f is the position along it.</summary>
            private bool Inside(int id, double x, double z, bool carriageway, out float f)
            {
                double ax = _seg[6 * id], az = _seg[6 * id + 1], bx = _seg[6 * id + 2], bz = _seg[6 * id + 3];
                double half = carriageway ? _seg[6 * id + 5] : _seg[6 * id + 4];
                double dx = bx - ax, dz = bz - az, l2 = dx * dx + dz * dz;
                double t = l2 > 1e-12 ? ((x - ax) * dx + (z - az) * dz) / l2 : 0;
                t = t < 0 ? 0 : t > 1 ? 1 : t;
                f = (float)t;
                double qx = ax + dx * t - x, qz = az + dz * t - z;
                return qx * qx + qz * qz <= half * half;
            }

            /// <summary>Road j has a point at <paramref name="node"/> (an approach or a road meeting there).</summary>
            private bool Touches(int j, long node)
            {
                int[] q = _t.Roads[j].Points;
                for (int i = 0; i < q.Length; i += 2)
                    if (BridgeStructures.Key(q[i], q[i + 1]) == node) return true;
                return false;
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------------------------------------------

        [ThreadStatic] private static double[] _scratch;

        /// <summary>Orders station candidates by |value| (subdivisions are stored negated).</summary>
        private sealed class AbsComparer : IComparer<double>
        {
            public static readonly AbsComparer Instance = new AbsComparer();

            public int Compare(double a, double b)
            {
                return Math.Abs(a).CompareTo(Math.Abs(b));
            }
        }

        private static double[] Scratch(int n)
        {
            if (_scratch == null || _scratch.Length < n) _scratch = new double[Math.Max(n, 64)];
            return _scratch;
        }

        private static int Find(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        private static void Union(int[] parent, int a, int b)
        {
            int ra = Find(parent, a), rb = Find(parent, b);
            if (ra == rb) return;
            if (ra < rb) parent[rb] = ra;
            else parent[ra] = rb;
        }

        /// <summary>Unit tangent (bisector) at point i with the miter scale (capped at 2), using the neighbours
        /// within [lo, hi] (a run's own points, or the context points beyond a cut).</summary>
        private static void Tangent(int[] p, int count, int i, int lo, int hi, out double tx, out double tz, out double miter)
        {
            double ix = 0, iz = 0, ox = 0, oz = 0;
            bool hasIn = false, hasOut = false;
            if (i > lo)
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
            if (i < hi)
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
