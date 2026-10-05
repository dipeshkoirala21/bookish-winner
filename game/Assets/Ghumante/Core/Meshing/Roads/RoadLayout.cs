using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing
{
    /// <summary>Where a junction cap cuts one road piece: the carriageway edge points of the ribbon's end section
    /// (tile-local metres), shared exactly by the ribbon and the cap.</summary>
    public struct RoadCut
    {
        public double S;
        public double CX, CZ;

        /// <summary>Unit left normal of the piece at the cut (the section direction).</summary>
        public double UX, UZ;

        public double Half, Shift;
    }

    /// <summary>A junction cap (W2_DESIGN 4.7): a polygon around the node from the game widths of every arm, with
    /// kerb-radius fillets between neighbouring arms. Ribbons stop at the cap edge.</summary>
    public sealed class JunctionCap
    {
        public double X, Z;

        /// <summary>Counter-clockwise outline (seen from above), tile-local metres; fan-triangulated from (X, Z).</summary>
        public double[] PolyX, PolyZ;

        public int Count;
        public int[] ArmRoads;
        public bool[] ArmForward;
        public double[] ArmSetback;

        /// <summary>The piece drawn on top (highest class): the cap takes its surface colour and lift.</summary>
        public int TopRoad;

        public JunctionKind Kind;
        public byte Flags;
        public float WidestArmM;
    }

    /// <summary>What a road island is.</summary>
    public enum IslandKind : byte
    {
        Roundabout = 0,
        Mini = 1,
        Synthetic = 2,
    }

    /// <summary>A raised road island (roundabout centre, synthetic chowk island) or a painted mini roundabout.</summary>
    public struct RoadIsland
    {
        public double X, Z;
        public float RadiusM;

        /// <summary>Mountable apron inside the island edge (buses), 0 for none.</summary>
        public float ApronM;

        public IslandKind Kind;
        public bool PolicePodium;
        public int TopRoad;
    }

    /// <summary>
    /// The per-tile road layout shared by <see cref="RoadMesher"/>, <see cref="JunctionMesher"/> and
    /// <see cref="MarkingMesher"/>: each piece's attributes (RATR, or on W1 packs the tile's own area type and building
    /// corridor), its width profile from <see cref="RoadWidthModel"/>, junction caps with the ribbon cuts they imply,
    /// and road islands (mapped rings from JNCT or closed ring ways, synthetic islands). Built once per decoded tile and
    /// cached (<see cref="For"/>); immutable afterwards, so worker threads may share it. Deterministic.
    /// </summary>
    public sealed class RoadLayout
    {
        /// <summary>Junction nodes closer than this to the tile border get no cap (the arms would cross into the
        /// neighbour, which meshes its own ribbons there).</summary>
        public const double CapBorderMarginM = 20.0;

        /// <summary>Largest cap setback along an arm.</summary>
        public const double MaxSetbackM = 30.0;

        private static readonly ConditionalWeakTable<TileData, RoadLayout> Cache = new ConditionalWeakTable<TileData, RoadLayout>();

        public readonly TileData Tile;
        public readonly bool FromRatr;
        public readonly RoadAttrRecord[] Attrs;
        public readonly RoadWidthProfile[] Profiles;
        public readonly List<JunctionCap> Caps = new List<JunctionCap>();
        public readonly List<RoadIsland> Islands = new List<RoadIsland>();

        /// <summary>Per road: the cuts at junction caps in ascending S, in pairs (gap start, gap end); null = none.</summary>
        public readonly RoadCut[][] Cuts;

        /// <summary>Per road: cumulative along distance of each point from the first rendered point (context points
        /// get negative or beyond-length values).</summary>
        private readonly double[][] _along;

        /// <summary>The layout of a tile (built on first use, then cached for the tile's lifetime).</summary>
        public static RoadLayout For(TileData t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            return Cache.GetValue(t, k => new RoadLayout(k));
        }

        private RoadLayout(TileData t)
        {
            Tile = t;
            int n = t.Roads.Count;
            Attrs = new RoadAttrRecord[n];
            Profiles = new RoadWidthProfile[n];
            Cuts = new RoadCut[n][];
            _along = new double[n][];
            FromRatr = t.HasRoadAttrs;
            for (int i = 0; i < n; i++) _along[i] = AlongOf(t.Roads[i]);
            if (FromRatr)
            {
                for (int i = 0; i < n; i++) Attrs[i] = t.RoadAttrs[i];
                TightenCorridors(t);
            }
            else
            {
                LocalAttrs(t);
            }
            for (int i = 0; i < n; i++)
            {
                Profiles[i] = new RoadWidthProfile();
                RoadWidthModel.BuildProfile(t.Roads[i], Attrs[i], Profiles[i]);
            }
            Rings(t);
            Junctions(t);
        }

        /// <summary>The carriageway half width of road <paramref name="road"/> at <paramref name="alongM"/>.</summary>
        public float HalfWidthAt(int road, double alongM)
        {
            return 0.5f * Profiles[road].WidthAt(alongM);
        }

        /// <summary>The full cross-section of road <paramref name="road"/> at <paramref name="alongM"/>.</summary>
        public RoadProfile ProfileAt(int road, double alongM)
        {
            return RoadWidthModel.ProfileFrom(Tile.Roads[road], Attrs[road], Profiles[road], alongM);
        }

        /// <summary>Along distance of point <paramref name="pointIndex"/> of road <paramref name="road"/>.</summary>
        public double AlongAt(int road, int pointIndex)
        {
            return _along[road][pointIndex];
        }

        /// <summary>True when <paramref name="alongM"/> lies inside a junction cap of the road.</summary>
        public bool InGap(int road, double alongM)
        {
            RoadCut[] c = Cuts[road];
            if (c == null) return false;
            for (int k = 0; k + 1 < c.Length; k += 2)
                if (alongM > c[k].S && alongM < c[k + 1].S) return true;
            return false;
        }

        private static double[] AlongOf(RoadRecord r)
        {
            int[] p = r.Points;
            int count = p.Length / 2;
            int first = r.HasPrevContext ? 1 : 0;
            var s = new double[count];
            for (int i = first + 1; i < count; i++)
            {
                double dx = (p[2 * i] - p[2 * i - 2]) / 100.0, dz = (p[2 * i + 1] - p[2 * i - 1]) / 100.0;
                s[i] = s[i - 1] + Math.Sqrt(dx * dx + dz * dz);
            }
            if (first == 1)
            {
                double dx = (p[2] - p[0]) / 100.0, dz = (p[3] - p[1]) / 100.0;
                s[0] = -Math.Sqrt(dx * dx + dz * dz);
            }
            return s;
        }

        // -------------------------------------------------------------------------------------------------------
        // W1 packs: area type and corridor from the tile itself
        // -------------------------------------------------------------------------------------------------------

        private const int AreaCells = 8;

        /// <summary>
        /// RATR corridors are building-to-building totals, but the ribbon is centred on the way: a building 3 m from
        /// the centreline on one side with open ground on the other still reads as a wide corridor. Take, per
        /// sample, the smaller of the RATR value and twice the nearer side's distance to this tile's buildings, so
        /// a centred ribbon never covers a building (V2). Within the border margin the local value never goes
        /// under the width the cut is pinned to, so both tiles still meet at the same width.
        /// </summary>
        private void TightenCorridors(TileData t)
        {
            if (t.Buildings.Count == 0) return;
            var corridor = new RoadCorridor(t);
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                RoadAttrRecord a = Attrs[i];
                double radius = 0.5 * RoadWidthModel.NominalGameWidthM(r, a) + 5.5; // + footpath reach
                float pinned = RoadWidthModel.BorderWidthM(r);
                int[] local = corridor.Sample(r, radius, -(int)Math.Ceiling((pinned + RoadWidthModel.CorridorClearanceM) * 10)); // seams: see Sample
                int[] src = a.CorridorDm ?? new int[0];
                int len = Math.Max(src.Length, local.Length);
                if (len == 0) continue;
                var merged = new int[len];
                bool changed = false;
                for (int k = 0; k < len; k++)
                {
                    int x = k < src.Length ? src[k] : 0, y = k < local.Length ? local[k] : 0;
                    int m = x == 0 ? y : y == 0 ? x : Math.Min(x, y);
                    merged[k] = m;
                    if (k >= src.Length || m != x) changed = true;
                }
                if (!changed) continue;
                a.CorridorDm = merged;
                Attrs[i] = a;
            }
        }

        private void LocalAttrs(TileData t)
        {
            AreaType[] cells = AreaTypeGrid.ClassifyTile(t, AreaCells);
            RoadCorridor corridor = t.Buildings.Count > 0 ? new RoadCorridor(t) : null;
            double cell = t.Tile.Size / AreaCells;
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                int[] p = r.Points;
                int mid = p.Length / 4;
                int ci = (int)(p[2 * mid] / 100.0 / cell), cj = (int)(p[2 * mid + 1] / 100.0 / cell);
                ci = ci < 0 ? 0 : ci >= AreaCells ? AreaCells - 1 : ci;
                cj = cj < 0 ? 0 : cj >= AreaCells ? AreaCells - 1 : cj;
                var a = new RoadAttrRecord { Area = cells[cj * AreaCells + ci], CorridorDm = new int[0] };
                if (a.Area == AreaType.Unknown) a.Area = AreaType.Rural;
                if (corridor != null)
                {
                    float nominal = RoadWidthModel.NominalGameWidthM(r, a);
                    float real = RoadWidthModel.RealWidthM(r, a);
                    double radius = 0.5 * nominal + 1.5 + 2.0; // + footpath reach
                    int borderDm = -(int)Math.Ceiling((RoadWidthModel.BorderWidthM(r) + RoadWidthModel.CorridorClearanceM) * 10); // seams
                    a.CorridorDm = corridor.Sample(r, radius, borderDm);
                }
                Attrs[i] = a;
            }
        }

        // -------------------------------------------------------------------------------------------------------
        // Roundabouts and islands
        // -------------------------------------------------------------------------------------------------------

        private static bool CapClass(RoadClass c)
        {
            return RoadWidthModel.IsMotor(c) && c != RoadClass.Track;
        }

        private void Rings(TileData t)
        {
            var ringRoads = new List<int>();
            Dictionary<long, int> nodeCount = null;
            // Mapped rings from JNCT.
            foreach (JunctionRecord j in t.Junctions)
            {
                double jx = j.XCm / 100.0, jz = j.ZCm / 100.0;
                if (j.Kind == JunctionKind.MiniRoundabout)
                {
                    float widest = WidestNear(t, jx, jz, 12.0, null);
                    Islands.Add(new RoadIsland { X = jx, Z = jz, RadiusM = (float)Math.Max(2.0, Math.Min(3.0, 0.25 * widest)), Kind = IslandKind.Mini, TopRoad = TopNear(t, jx, jz, 12.0) });
                    continue;
                }
                if ((j.Kind == JunctionKind.Roundabout || j.Kind == JunctionKind.Circular) && j.RingDiameterCm > 0)
                {
                    double ringR = j.RingDiameterCm / 200.0;
                    ringRoads.Clear();
                    for (int i = 0; i < t.Roads.Count; i++)
                        if (Attrs[i].Has(RoadAttrFlags.RingMember) && NearRing(t.Roads[i], jx, jz, ringR)) ringRoads.Add(i);
                    AddRing(t, jx, jz, 2 * ringR, j.IslandDiameterCm / 100.0, ringRoads, j.Kind == JunctionKind.Roundabout && j.Has(JunctionFlags.HasPolice));
                    continue;
                }
                if (j.Kind == JunctionKind.SyntheticIsland)
                {
                    float widest = WidestNear(t, jx, jz, 15.0, null);
                    double d = Math.Max(6.0, Math.Min(16.0, 0.7 * widest));
                    if (j.IslandDiameterCm > 0) d = j.IslandDiameterCm / 100.0;
                    // Lanes circulate round the island (LaneGraph synthesises its ring), but only the chowk node's own
                    // arms are re-routed: a second junction node or another carriageway inside the disc (split dual
                    // carriageway chowks) would run its lanes through it, so the island shrinks clear of them, and
                    // below the 6 m minimum the chowk keeps its podium without an island.
                    if (nodeCount == null) nodeCount = NodeCounts(t);
                    d = Math.Min(d, 2.0 * ClearRadius(t, j.XCm, j.ZCm, 0.5 * d, nodeCount));
                    if (d >= 6.0)
                        Islands.Add(new RoadIsland { X = jx, Z = jz, RadiusM = (float)(0.5 * d), Kind = IslandKind.Synthetic, PolicePodium = true, TopRoad = TopNear(t, jx, jz, 15.0) });
                }
            }
            // W2 packs (RATR present) carry every real ring in JNCT and RING_MEMBER (W2_DESIGN 4.7: mapped rings, mini
            // nodes and curated synthetic islands only), and most of their tiles have no JNCT record at all: never guess
            // there, or parking and campus service loops become fake roundabouts with no ring lanes.
            if (t.Junctions.Count > 0 || FromRatr) return;
            // W1 packs: a closed motor way under 80 m across is a roundabout ring.
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                if (!CapClass(r.RoadClass) || r.HasPrevContext || r.HasNextContext) continue;
                int[] p = r.Points;
                int n = p.Length / 2;
                if (n < 5 || p[0] != p[2 * n - 2] || p[1] != p[2 * n - 1]) continue;
                double cx = 0, cz = 0;
                for (int k = 0; k < n - 1; k++)
                {
                    cx += p[2 * k] / 100.0;
                    cz += p[2 * k + 1] / 100.0;
                }
                cx /= n - 1;
                cz /= n - 1;
                double meanR = 0, maxR = 0;
                for (int k = 0; k < n - 1; k++)
                {
                    double d = Math.Sqrt(Sq(p[2 * k] / 100.0 - cx) + Sq(p[2 * k + 1] / 100.0 - cz));
                    meanR += d;
                    maxR = Math.Max(maxR, d);
                }
                meanR /= n - 1;
                if (maxR > 40 || meanR < 4 || maxR > 1.6 * meanR) continue;
                ringRoads.Clear();
                ringRoads.Add(i);
                AddRing(t, cx, cz, 2 * meanR, 0, ringRoads, false);
            }
        }

        private static double Sq(double v)
        {
            return v * v;
        }

        private static bool NearRing(RoadRecord r, double cx, double cz, double ringR)
        {
            int[] p = r.Points;
            int mid = p.Length / 4;
            double d = Math.Sqrt(Sq(p[2 * mid] / 100.0 - cx) + Sq(p[2 * mid + 1] / 100.0 - cz));
            return Math.Abs(d - ringR) < Math.Max(6.0, 0.3 * ringR);
        }

        /// <summary>Widest game carriageway among motor pieces (not in <paramref name="exclude"/>) passing within
        /// <paramref name="radius"/> of a point.</summary>
        /// <summary>How many cap-class roads pass through each point (keys as in <see cref="Junctions"/>).</summary>
        private static Dictionary<long, int> NodeCounts(TileData t)
        {
            var count = new Dictionary<long, int>();
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                if (!CapClass(r.RoadClass)) continue;
                int[] p = r.Points;
                for (int k = 0; k < p.Length / 2; k++)
                {
                    long key = (long)p[2 * k] << 32 ^ (uint)p[2 * k + 1];
                    int c;
                    count.TryGetValue(key, out c);
                    count[key] = c + 1;
                }
            }
            return count;
        }

        /// <summary>
        /// The largest island radius at the junction node (xcm, zcm) that keeps 0.5 m clear of every carriageway not
        /// re-routed round it: roads that do not pass through the node, and other junction nodes (shared points) of the
        /// roads that do. Starts from <paramref name="maxR"/>.
        /// </summary>
        private double ClearRadius(TileData t, int xcm, int zcm, double maxR, Dictionary<long, int> nodeCount)
        {
            double jx = xcm / 100.0, jz = zcm / 100.0, best = maxR;
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                if (!CapClass(r.RoadClass) || (r.Flags & (RoadFlags.Bridge | RoadFlags.Tunnel)) != 0) continue;
                int[] p = r.Points;
                int n = p.Length / 2;
                bool through = false;
                for (int k = 0; k < n && !through; k++) through = p[2 * k] == xcm && p[2 * k + 1] == zcm;
                for (int k = 0; k < n; k++)
                {
                    if (p[2 * k] == xcm && p[2 * k + 1] == zcm) continue;
                    double d = Math.Sqrt(Sq(p[2 * k] / 100.0 - jx) + Sq(p[2 * k + 1] / 100.0 - jz));
                    if (d > best + 20.0) continue;
                    if (through)
                    {
                        int c;
                        nodeCount.TryGetValue((long)p[2 * k] << 32 ^ (uint)p[2 * k + 1], out c);
                        if (c < 2) continue; // the node's own arm: its lanes are trimmed back to the ring
                    }
                    double half = 0.5 * Profiles[i].WidthAt(Math.Max(0, _along[i][k]));
                    best = Math.Min(best, d - half - 0.5);
                }
            }
            return Math.Max(0.0, best);
        }

        private float WidestNear(TileData t, double x, double z, double radius, List<int> exclude)
        {
            float best = 0f;
            for (int i = 0; i < t.Roads.Count; i++)
            {
                if (exclude != null && exclude.Contains(i)) continue;
                RoadRecord r = t.Roads[i];
                if (!CapClass(r.RoadClass)) continue;
                int[] p = r.Points;
                for (int k = 0; k < p.Length / 2; k++)
                {
                    if (Sq(p[2 * k] / 100.0 - x) + Sq(p[2 * k + 1] / 100.0 - z) > radius * radius) continue;
                    best = Math.Max(best, Profiles[i].WidthAt(Math.Max(0, _along[i][k])));
                }
            }
            return best;
        }

        private int TopNear(TileData t, double x, double z, double radius)
        {
            int best = -1, prio = -1;
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                if (!CapClass(r.RoadClass)) continue;
                int[] p = r.Points;
                for (int k = 0; k < p.Length / 2; k++)
                {
                    if (Sq(p[2 * k] / 100.0 - x) + Sq(p[2 * k + 1] / 100.0 - z) > radius * radius) continue;
                    int pr = RoadStyle.Priority(r.RoadClass);
                    if (pr > prio)
                    {
                        prio = pr;
                        best = i;
                    }
                    break;
                }
            }
            return best;
        }

        /// <summary>
        /// A roundabout (W2_DESIGN 4.7): ring width = widest approach (game) + 1 m, at least 7 m, applied to the ring
        /// pieces; the island fills the inside of the ring (centreline diameter − ring width) unless mapped, with a
        /// 1 m mountable apron.
        /// </summary>
        private void AddRing(TileData t, double cx, double cz, double ringDiameter, double islandDiameter, List<int> ringRoads, bool police)
        {
            float approach = WidestNear(t, cx, cz, 0.5 * ringDiameter + 12.0, ringRoads);
            float ringWidth = Math.Max(7f, approach + 1f);
            double island = islandDiameter > 0 ? islandDiameter : ringDiameter - ringWidth;
            foreach (int i in ringRoads)
            {
                RoadWidthProfile p = Profiles[i];
                for (int k = 0; k < p.Count; k++)
                {
                    // Widen to the ring width where the corridor allows (never over a building).
                    p.Width[k] = Math.Max(p.Width[k], Math.Min(ringWidth, p.Limit[k]));
                    p.FootLeft[k] = p.FootRight[k] = 0f;
                }
            }
            // The island (its 1 m apron is inside its radius) stays inside the ring carriageway's inner edge: a mapped
            // ring is rarely a circle (Maitighar's ring ways come within 22 m of a 34 m equal-area radius), and the raised
            // disc must never cover a ring lane.
            double inner = ringRoads.Count > 0 ? InnerRingRadius(t, cx, cz, ringRoads) : 0.5 * ringDiameter - 0.5 * ringWidth;
            island = Math.Min(island, 2.0 * (inner - 0.1));
            int top = ringRoads.Count > 0 ? ringRoads[0] : TopNear(t, cx, cz, 0.5 * ringDiameter + 5);
            if (island >= 2.0)
                Islands.Add(new RoadIsland { X = cx, Z = cz, RadiusM = (float)(0.5 * island), ApronM = 1.0f, Kind = IslandKind.Roundabout, PolicePodium = police, TopRoad = top });
        }

        /// <summary>Nearest approach of the ring carriageways' inner edges (centreline distance minus the drawn half width)
        /// to (cx, cz).</summary>
        private double InnerRingRadius(TileData t, double cx, double cz, List<int> ringRoads)
        {
            double best = double.MaxValue;
            foreach (int i in ringRoads)
            {
                int[] p = t.Roads[i].Points;
                RoadWidthProfile prof = Profiles[i];
                double along = 0;
                for (int k = 0; k < p.Length / 2; k++)
                {
                    double x = p[2 * k] / 100.0, z = p[2 * k + 1] / 100.0;
                    if (k > 0) along += Math.Sqrt(Sq(x - p[2 * k - 2] / 100.0) + Sq(z - p[2 * k - 1] / 100.0));
                    double d = Math.Sqrt(Sq(x - cx) + Sq(z - cz)) - 0.5 * prof.WidthAt(along);
                    if (d < best) best = d;
                }
            }
            return best;
        }

        // -------------------------------------------------------------------------------------------------------
        // Junction caps
        // -------------------------------------------------------------------------------------------------------

        private struct Arm
        {
            public int Road, Point;
            public bool Forward;
            public double DX, DZ, Angle, SNode, Avail, OffL, OffR, Setback;
        }

        private void Junctions(TileData t)
        {
            double size = t.Tile.Size;
            // Rendered points of cap-class pieces by exact position.
            var nodes = new Dictionary<long, List<int>>();
            var order = new List<long>();
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                if (!CapClass(r.RoadClass) || (r.Flags & (RoadFlags.Bridge | RoadFlags.Tunnel)) != 0 || r.Layer != 0) continue;
                int[] p = r.Points;
                int count = p.Length / 2, first = r.HasPrevContext ? 1 : 0, last = r.HasNextContext ? count - 2 : count - 1;
                for (int k = first; k <= last; k++)
                {
                    long key = (long)p[2 * k] << 32 ^ (uint)p[2 * k + 1];
                    List<int> l;
                    if (!nodes.TryGetValue(key, out l))
                    {
                        nodes[key] = l = new List<int>(2);
                        order.Add(key);
                    }
                    l.Add(i << 16 | (k & 0xFFFF));
                }
            }
            // Junction nodes per road (for arm lengths).
            var nodeAlong = new List<double>[t.Roads.Count];
            var junctionKeys = new List<long>();
            foreach (long key in order)
            {
                List<int> l = nodes[key];
                if (l.Count < 2) continue;
                if (ArmCount(t, l) < 3) continue;
                junctionKeys.Add(key);
                foreach (int e in l)
                {
                    int ri = e >> 16, k = e & 0xFFFF;
                    if (nodeAlong[ri] == null) nodeAlong[ri] = new List<double>(2);
                    nodeAlong[ri].Add(_along[ri][k]);
                }
            }
            var cutLists = new List<RoadCut>[t.Roads.Count];
            var arms = new List<Arm>(6);
            RoadCorridor footprints = t.Buildings.Count > 0 && junctionKeys.Count > 0 ? new RoadCorridor(t) : null;
            foreach (long key in junctionKeys)
            {
                int xcm = (int)(key >> 32), zcm = (int)(uint)key;
                double nx = xcm / 100.0, nz = zcm / 100.0;
                if (nx < CapBorderMarginM || nz < CapBorderMarginM || nx > size - CapBorderMarginM || nz > size - CapBorderMarginM) continue;
                arms.Clear();
                foreach (int e in nodes[key]) CollectArms(t, e >> 16, e & 0xFFFF, nodeAlong, arms);
                if (arms.Count < 3) continue;
                arms.Sort((a, b) => a.Angle.CompareTo(b.Angle));
                JunctionCap cap = BuildCap(t, nx, nz, arms, footprints);
                if (cap == null) continue;
                Caps.Add(cap);
                for (int k = 0; k < arms.Count; k++)
                {
                    Arm a = arms[k];
                    double s0 = a.Forward ? a.SNode : a.SNode - a.Setback, s1 = a.Forward ? a.SNode + a.Setback : a.SNode;
                    if (cutLists[a.Road] == null) cutLists[a.Road] = new List<RoadCut>();
                    cutLists[a.Road].Add(CutAt(t, a.Road, s0));
                    cutLists[a.Road].Add(CutAt(t, a.Road, s1));
                }
            }
            for (int i = 0; i < cutLists.Length; i++)
            {
                if (cutLists[i] == null) continue;
                List<RoadCut> l = cutLists[i];
                // Pairs are (start, end); sort pairs by start and drop overlapping ones.
                var pairs = new List<RoadCut[]>();
                for (int k = 0; k + 1 < l.Count; k += 2) pairs.Add(new[] { l[k], l[k + 1] });
                pairs.Sort((a, b) => a[0].S.CompareTo(b[0].S));
                var merged = new List<RoadCut>();
                foreach (RoadCut[] pr in pairs)
                {
                    if (merged.Count > 0 && pr[0].S <= merged[merged.Count - 1].S)
                    {
                        if (pr[1].S > merged[merged.Count - 1].S) merged[merged.Count - 1] = pr[1];
                        continue;
                    }
                    merged.Add(pr[0]);
                    merged.Add(pr[1]);
                }
                Cuts[i] = merged.ToArray();
            }
            MatchJunctionRecords(t);
        }

        private int ArmCount(TileData t, List<int> entries)
        {
            int n = 0;
            foreach (int e in entries)
            {
                RoadRecord r = t.Roads[e >> 16];
                int k = e & 0xFFFF, count = r.Points.Length / 2;
                int first = r.HasPrevContext ? 1 : 0, last = r.HasNextContext ? count - 2 : count - 1;
                if (k > first) n++;
                if (k < last) n++;
            }
            return n;
        }

        private void CollectArms(TileData t, int ri, int k, List<double>[] nodeAlong, List<Arm> arms)
        {
            RoadRecord r = t.Roads[ri];
            int[] p = r.Points;
            int count = p.Length / 2, first = r.HasPrevContext ? 1 : 0, last = r.HasNextContext ? count - 2 : count - 1;
            double s = _along[ri][k];
            double length = Profiles[ri].LengthM;
            for (int dir = 0; dir < 2; dir++)
            {
                bool forward = dir == 0;
                if (forward ? k >= last : k <= first) continue;
                int step = forward ? 1 : -1, q = k + step;
                // Direction to the next point at least 0.3 m away.
                while (q > first && q < last && Sq((p[2 * q] - p[2 * k]) / 100.0) + Sq((p[2 * q + 1] - p[2 * k + 1]) / 100.0) < 0.09) q += step;
                double dx = (p[2 * q] - p[2 * k]) / 100.0, dz = (p[2 * q + 1] - p[2 * k + 1]) / 100.0, l = Math.Sqrt(dx * dx + dz * dz);
                if (l < 1e-6) continue;
                dx /= l;
                dz /= l;
                // Room along the arm: to the next junction node (shared) or the piece end.
                double room = forward ? length - s : s;
                bool shared = false;
                if (nodeAlong[ri] != null)
                    foreach (double o in nodeAlong[ri])
                    {
                        double d = forward ? o - s : s - o;
                        if (d > 1e-6 && d < room)
                        {
                            room = d;
                            shared = true;
                        }
                    }
                float half = 0.5f * Profiles[ri].WidthAt(s);
                RoadProfile pr = RoadWidthModel.ProfileFrom(r, Attrs[ri], Profiles[ri], s);
                double shift = pr.CentreShiftM;
                arms.Add(new Arm
                {
                    Road = ri, Point = k, Forward = forward, DX = dx, DZ = dz, Angle = Math.Atan2(dz, dx), SNode = s,
                    Avail = Math.Min(MaxSetbackM, room * (shared ? 0.45 : 0.9)),
                    OffL = forward ? half + shift : half - shift, OffR = forward ? half - shift : half + shift,
                });
            }
        }

        /// <summary>Clearance of a kerb fillet from the corner buildings (G7 / V2: a cap never covers a building).</summary>
        public const double FilletClearanceM = 0.1;

        private static readonly double[] FilletScales = { 1.0, 0.5, 0.25, 0.0 };

        private JunctionCap BuildCap(TileData t, double nx, double nz, List<Arm> arms, RoadCorridor footprints)
        {
            int n = arms.Count;
            float widest = 0f;
            int top = arms[0].Road;
            bool oldCore = false;
            for (int k = 0; k < n; k++)
            {
                Arm a = arms[k];
                widest = Math.Max(widest, (float)(a.OffL + a.OffR));
                if (RoadStyle.Priority(t.Roads[a.Road].RoadClass) > RoadStyle.Priority(t.Roads[top].RoadClass)) top = a.Road;
                if (RoadWidthModel.AreaOf(Attrs[a.Road]) == AreaType.OldCore) oldCore = true;
                a.Setback = 0.5;
                arms[k] = a;
            }
            var px = new double[n];
            var pz = new double[n];
            var hasP = new bool[n];
            for (int k = 0; k < n; k++)
            {
                Arm a = arms[k], b = arms[(k + 1) % n];
                double theta = b.Angle - a.Angle;
                if (theta <= 0) theta += 2 * Math.PI;
                if (n == 1 || theta > 170.0 * Math.PI / 180.0) continue;
                // Left edge of a: N + nA·offL + ta·dA ; right edge of b: N − nB·offR + tb·dB ; n = (−dz, dx).
                double ax = a.DX, az = a.DZ, bx = -b.DX, bz = -b.DZ;
                double cx = -(-b.DZ) * b.OffR - (-a.DZ) * a.OffL, cz = -(b.DX) * b.OffR - (a.DX) * a.OffL;
                double den = ax * bz - az * bx;
                if (Math.Abs(den) < 1e-9) continue;
                double ta = (cx * bz - cz * bx) / den, tb = (ax * cz - az * cx) / den;
                if (ta < 0) ta = 0;
                if (tb < 0) tb = 0;
                double kerbR = oldCore ? 0.5 : (a.OffL + a.OffR >= 6.5 && b.OffL + b.OffR >= 6.5 ? 6.0 : 3.0);
                a.Setback = Math.Max(a.Setback, ta + kerbR);
                b.Setback = Math.Max(b.Setback, tb + kerbR);
                arms[k] = a;
                arms[(k + 1) % n] = b;
                px[k] = nx + a.DX * ta - a.DZ * a.OffL;
                pz[k] = nz + a.DZ * ta + a.DX * a.OffL;
                hasP[k] = true;
            }
            for (int k = 0; k < n; k++)
            {
                Arm a = arms[k];
                a.Setback = Math.Min(a.Setback, a.Avail);
                if (a.Setback < 0.5) return null; // no room: leave this node to plain overlapping ribbons
                arms[k] = a;
            }
            var xs = new List<double>(n * 6);
            var zs = new List<double>(n * 6);
            for (int k = 0; k < n; k++)
            {
                Arm a = arms[k], b = arms[(k + 1) % n];
                RoadCut ca = CutAt(t, a.Road, a.Forward ? a.SNode + a.Setback : a.SNode - a.Setback);
                double sgn = a.Forward ? 1 : -1;
                // Arm-left = piece-left for a forward arm.
                double lx = ca.CX + sgn * ca.UX * (ca.Half + sgn * ca.Shift), lz = ca.CZ + sgn * ca.UZ * (ca.Half + sgn * ca.Shift);
                double rx = ca.CX - sgn * ca.UX * (ca.Half - sgn * ca.Shift), rz = ca.CZ - sgn * ca.UZ * (ca.Half - sgn * ca.Shift);
                xs.Add(rx);
                zs.Add(rz);
                xs.Add(lx);
                zs.Add(lz);
                if (!hasP[k] || n < 2) continue;
                RoadCut cb = CutAt(t, b.Road, b.Forward ? b.SNode + b.Setback : b.SNode - b.Setback);
                double sb = b.Forward ? 1 : -1;
                double brx = cb.CX - sb * cb.UX * (cb.Half - sb * cb.Shift), brz = cb.CZ - sb * cb.UZ * (cb.Half - sb * cb.Shift);
                // Quadratic Bézier fillet from a's left edge to b's right edge, control at their intersection. The fillet
                // bulges into the corner quadrant where the corner houses stand: it shrinks toward the sharp corner
                // (along both edges) until it clears their footprints, and is the sharp corner when nothing fits.
                double g = 0;
                foreach (double scale in FilletScales)
                {
                    g = scale;
                    if (footprints == null || scale == 0 || FilletClear(footprints, lx, lz, px[k], pz[k], brx, brz, scale)) break;
                }
                if (g == 0)
                {
                    xs.Add(px[k]);
                    zs.Add(pz[k]);
                    continue;
                }
                double l2x = px[k] + (lx - px[k]) * g, l2z = pz[k] + (lz - pz[k]) * g;
                double b2x = px[k] + (brx - px[k]) * g, b2z = pz[k] + (brz - pz[k]) * g;
                if (g < 1)
                {
                    xs.Add(l2x);
                    zs.Add(l2z);
                }
                for (int q = 1; q <= 3; q++)
                {
                    double u = q / 4.0, w0 = (1 - u) * (1 - u), w1 = 2 * (1 - u) * u, w2 = u * u;
                    xs.Add(w0 * l2x + w1 * px[k] + w2 * b2x);
                    zs.Add(w0 * l2z + w1 * pz[k] + w2 * b2z);
                }
                if (g < 1)
                {
                    xs.Add(b2x);
                    zs.Add(b2z);
                }
            }
            var cap = new JunctionCap
            {
                X = nx, Z = nz, PolyX = xs.ToArray(), PolyZ = zs.ToArray(), Count = xs.Count, ArmRoads = new int[n],
                ArmForward = new bool[n], ArmSetback = new double[n], TopRoad = top, WidestArmM = widest,
            };
            for (int k = 0; k < n; k++)
            {
                cap.ArmRoads[k] = arms[k].Road;
                cap.ArmForward[k] = arms[k].Forward;
                cap.ArmSetback[k] = arms[k].Setback;
            }
            return cap;
        }

        /// <summary>True when the fillet scaled by <paramref name="g"/> toward the corner (l, p, b) keeps
        /// <see cref="FilletClearanceM"/> from every building edge.</summary>
        private static bool FilletClear(RoadCorridor footprints, double lx, double lz, double cx, double cz, double bx, double bz, double g)
        {
            double ax = cx + (lx - cx) * g, az = cz + (lz - cz) * g, ex = cx + (bx - cx) * g, ez = cz + (bz - cz) * g;
            double prevX = ax, prevZ = az;
            for (int q = 1; q <= 4; q++)
            {
                double u = q / 4.0, w0 = (1 - u) * (1 - u), w1 = 2 * (1 - u) * u, w2 = u * u;
                double x = w0 * ax + w1 * cx + w2 * ex, z = w0 * az + w1 * cz + w2 * ez;
                if (!footprints.IsClear(prevX, prevZ, x, z, FilletClearanceM)) return false;
                prevX = x;
                prevZ = z;
            }
            return true;
        }

        /// <summary>The cut section of road <paramref name="ri"/> at <paramref name="s"/>: the point on the polyline and
        /// the left normal of the segment containing it (the segment toward the piece's middle at a vertex).</summary>
        public RoadCut CutAt(TileData t, int ri, double s)
        {
            RoadRecord r = t.Roads[ri];
            int[] p = r.Points;
            double[] al = _along[ri];
            int count = p.Length / 2, first = r.HasPrevContext ? 1 : 0, last = r.HasNextContext ? count - 2 : count - 1;
            int seg = first;
            for (int k = first; k < last; k++)
            {
                seg = k;
                if (al[k + 1] >= s) break;
            }
            double ax = p[2 * seg] / 100.0, az = p[2 * seg + 1] / 100.0, bx = p[2 * seg + 2] / 100.0, bz = p[2 * seg + 3] / 100.0;
            double len = al[seg + 1] - al[seg];
            double f = len > 1e-9 ? (s - al[seg]) / len : 0;
            f = f < 0 ? 0 : f > 1 ? 1 : f;
            double dx = bx - ax, dz = bz - az, l = Math.Sqrt(dx * dx + dz * dz);
            if (l < 1e-9)
            {
                dx = 1;
                dz = 0;
                l = 1;
            }
            RoadProfile pr = RoadWidthModel.ProfileFrom(r, Attrs[ri], Profiles[ri], s);
            return new RoadCut
            {
                S = s, CX = ax + (bx - ax) * f, CZ = az + (bz - az) * f, UX = -dz / l, UZ = dx / l,
                Half = 0.5 * pr.CarriagewayM, Shift = pr.CentreShiftM,
            };
        }

        private void MatchJunctionRecords(TileData t)
        {
            foreach (JunctionCap c in Caps)
            {
                foreach (JunctionRecord j in t.Junctions)
                {
                    if (Sq(j.XCm / 100.0 - c.X) + Sq(j.ZCm / 100.0 - c.Z) > 9.0) continue;
                    c.Kind = j.Kind;
                    c.Flags = j.Flags;
                    break;
                }
            }
        }
    }
}
