using System;
using System.Collections.Generic;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// The side-by-side rules of <see cref="RoadLayout"/> (W2 detail pass review): carriageways and footpaths stop where an
    /// unconnected parallel piece begins (two one-way carriageways of Durbar Marg, the Ring Road's main and service lanes
    /// at Kalanki), separately mapped sidewalks become the footpath of the street they run along instead of a second
    /// ribbon across its asphalt, footway crossings are not drawn over the carriageway, and building passages are drawn
    /// as narrow gateways. All decisions come from the raw mapped polylines and the first-pass width profiles, so they are
    /// deterministic and independent of the order of the pieces.
    /// </summary>
    public sealed partial class RoadLayout
    {
        /// <summary>Largest angle between two pieces that lie side by side (either direction).</summary>
        public const double ParallelMaxDeg = 30.0;

        /// <summary>Near a node two pieces share they meet (a fork, a slip road): within this many times their combined
        /// half extents of such a node they are not clamped against each other.</summary>
        public const double SharedNodeReach = 3.0;

        /// <summary>Spacing of the sidewalk test along footways and paths.</summary>
        public const double SidewalkStepM = 2.0;

        /// <summary>No footway stretch closer than this to a tile-border cut is absorbed: both tiles keep drawing it there,
        /// so the seam agrees whatever each tile knows about the street beside it.</summary>
        public const double AbsorbBorderMarginM = 10.0;

        /// <summary>Shortest absorbed footway stretch (shorter runs and holes are merged into their neighbours).</summary>
        public const double MinAbsorbedM = 4.0;

        /// <summary>Widest footpath an absorbed sidewalk widens its street's footpath to.</summary>
        public const float MaxAbsorbedFootpathM = 3.5f;

        /// <summary>
        /// The value of <c>RoadStructureKind.Passage</c> (shared/enums.json ENUMS_VERSION 4, docs/DATA_FORMATS.md 1.15: a way
        /// under a building that stays intact). The base stub of <see cref="RoadStructureKind"/> predates it; the numeric
        /// value is the format's, so this keeps working when the data package's enum lands.
        /// </summary>
        public const RoadStructureKind PassageKind = (RoadStructureKind)6;

        /// <summary>A passage is drawn this wide at most (the gateway, never the 4.8 m rideability floor).</summary>
        public const float MaxPassageWidthM = 3.5f;

        /// <summary>... and at least this wide.</summary>
        public const float MinPassageWidthM = 1.5f;

        /// <summary>True when road <paramref name="road"/> is a building passage (RSTR kind Passage): drawn as a narrow
        /// gateway, without footpaths, and outside the clear corridors (the house over it stays whole).</summary>
        public bool IsPassage(int road)
        {
            return Structures[road].Kind == PassageKind;
        }

        /// <summary>True when raw along <paramref name="alongM"/> of road <paramref name="road"/> lies in a stretch absorbed
        /// into a neighbouring street (a mapped sidewalk drawn as that street's footpath, or a footway crossing its
        /// carriageway): not drawn as its own ribbon and outside the clear corridors.</summary>
        public bool InAbsorbed(int road, double alongM)
        {
            double[] a = Absorbed[road];
            if (a == null) return false;
            for (int k = 0; k + 1 < a.Length; k += 2)
                if (alongM >= a[k] && alongM <= a[k + 1]) return true;
            return false;
        }

        // -------------------------------------------------------------------------------------------------------
        // Raw segment index
        // -------------------------------------------------------------------------------------------------------

        /// <summary>The rendered raw segments of a set of pieces in a uniform grid.</summary>
        private sealed class SegIndex
        {
            private const double Cell = 16.0;
            public readonly List<int> Road = new List<int>();
            public readonly List<double> Ax = new List<double>(), Az = new List<double>(), Bx = new List<double>(), Bz = new List<double>();
            public readonly List<double> S0 = new List<double>(), S1 = new List<double>();
            private readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();
            private int[] _stamp = new int[0];
            private int _query;

            private static long Key(int i, int j)
            {
                return (long)i << 32 ^ (uint)j;
            }

            public void Add(int road, double ax, double az, double bx, double bz, double s0, double s1)
            {
                int id = Road.Count;
                Road.Add(road);
                Ax.Add(ax);
                Az.Add(az);
                Bx.Add(bx);
                Bz.Add(bz);
                S0.Add(s0);
                S1.Add(s1);
                int i0 = (int)Math.Floor(Math.Min(ax, bx) / Cell), i1 = (int)Math.Floor(Math.Max(ax, bx) / Cell);
                int j0 = (int)Math.Floor(Math.Min(az, bz) / Cell), j1 = (int)Math.Floor(Math.Max(az, bz) / Cell);
                for (int j = j0; j <= j1; j++)
                {
                    for (int i = i0; i <= i1; i++)
                    {
                        List<int> l;
                        if (!_cells.TryGetValue(Key(i, j), out l)) _cells[Key(i, j)] = l = new List<int>(4);
                        l.Add(id);
                    }
                }
            }

            /// <summary>Segments whose cells lie within <paramref name="r"/> of (x, z), each once.</summary>
            public void Near(double x, double z, double r, List<int> result)
            {
                result.Clear();
                if (_stamp.Length < Road.Count) _stamp = new int[Math.Max(Road.Count, 2 * _stamp.Length)];
                _query++;
                int i0 = (int)Math.Floor((x - r) / Cell), i1 = (int)Math.Floor((x + r) / Cell);
                int j0 = (int)Math.Floor((z - r) / Cell), j1 = (int)Math.Floor((z + r) / Cell);
                for (int j = j0; j <= j1; j++)
                {
                    for (int i = i0; i <= i1; i++)
                    {
                        List<int> l;
                        if (!_cells.TryGetValue(Key(i, j), out l)) continue;
                        foreach (int id in l)
                        {
                            if (_stamp[id] == _query) continue;
                            _stamp[id] = _query;
                            result.Add(id);
                        }
                    }
                }
            }
        }

        /// <summary>A piece that takes part in the side-by-side rules: drawn, on the ground (no deck, bridge, tunnel or
        /// passage) and not a roundabout ring way.</summary>
        private bool SideCandidate(TileData t, int i)
        {
            RoadRecord r = t.Roads[i];
            if (!RoadMesher.IsDrawn(r, null) || (r.Flags & (RoadFlags.Bridge | RoadFlags.Tunnel)) != 0) return false;
            if (Structures[i].IsElevated || Structures[i].Kind == RoadStructureKind.Tunnel || IsPassage(i)) return false;
            return !Attrs[i].Has(RoadAttrFlags.RingMember);
        }

        private static bool SidewalkClass(RoadClass c)
        {
            return c == RoadClass.Footway || c == RoadClass.Path || c == RoadClass.Cycleway || c == RoadClass.Bridleway;
        }

        private static bool StreetClass(RoadClass c)
        {
            return RoadWidthModel.IsMotor(c) || c == RoadClass.Pedestrian;
        }

        /// <summary>Position and unit direction of the raw rendered polyline of road <paramref name="ri"/> at raw along
        /// <paramref name="s"/> (clamped); false for a degenerate piece.</summary>
        private bool RawAt(TileData t, int ri, double s, out double x, out double z, out double dx, out double dz)
        {
            x = z = dx = dz = 0;
            RoadRecord r = t.Roads[ri];
            int first, last;
            Rendered(r, out first, out last);
            if (last <= first) return false;
            int[] p = r.Points;
            double[] al = _along[ri];
            int seg = first;
            while (seg + 1 < last && al[seg + 1] < s) seg++;
            // Skip zero-length segments (repeated points) for the direction.
            int a = seg, b = seg + 1;
            double ex = (p[2 * b] - p[2 * a]) / 100.0, ez = (p[2 * b + 1] - p[2 * a + 1]) / 100.0, l = Math.Sqrt(ex * ex + ez * ez);
            if (l < 1e-6)
            {
                for (int q = first; q < last; q++)
                {
                    ex = (p[2 * q + 2] - p[2 * q]) / 100.0;
                    ez = (p[2 * q + 3] - p[2 * q + 1]) / 100.0;
                    l = Math.Sqrt(ex * ex + ez * ez);
                    if (l >= 1e-6)
                    {
                        a = q;
                        b = q + 1;
                        break;
                    }
                }
                if (l < 1e-6) return false;
            }
            double span = al[b] - al[a];
            double f = span > 1e-9 ? (s - al[a]) / span : 0;
            f = f < 0 ? 0 : f > 1 ? 1 : f;
            x = p[2 * a] / 100.0 + ex * f;
            z = p[2 * a + 1] / 100.0 + ez * f;
            dx = ex / l;
            dz = ez / l;
            return true;
        }

        private SegIndex BuildIndex(TileData t, bool[] member)
        {
            var idx = new SegIndex();
            for (int i = 0; i < t.Roads.Count; i++)
            {
                if (!member[i]) continue;
                RoadRecord r = t.Roads[i];
                int first, last;
                Rendered(r, out first, out last);
                int[] p = r.Points;
                for (int k = first; k < last; k++)
                {
                    double ax = p[2 * k] / 100.0, az = p[2 * k + 1] / 100.0, bx = p[2 * k + 2] / 100.0, bz = p[2 * k + 3] / 100.0;
                    if (Sq(bx - ax) + Sq(bz - az) < 1e-6) continue;
                    idx.Add(i, ax, az, bx, bz, _along[i][k], _along[i][k + 1]);
                }
            }
            return idx;
        }

        /// <summary>
        /// How far the drawn piece reaches from its centreline on one side at <paramref name="s"/>: the carriageway
        /// (<paramref name="carriage"/>, the dual carriageway's outward shift included) and what lies beyond it
        /// (<paramref name="beyond"/>: the footpath as drawn, the median half or the shoulder).
        /// </summary>
        private void Extent(TileData t, int road, double s, bool left, out float carriage, out float beyond)
        {
            RoadWidthProfile p = Profiles[road];
            float w = p.DrawnAt(s), shift = p.ShiftAt(s);
            carriage = left ? shift + 0.5f * w : 0.5f * w - shift;
            float foot = p.Sample(left ? p.FootLeft : p.FootRight, s);
            if (foot > 0f) foot = Math.Max(foot, RoadWidthModel.MinFootpathM);
            if (p.Dual && !left)
            {
                beyond = 0.5f * Math.Max(RoadWidthModel.MinMedianM, Attrs[road].MedianCm / 100f);
                return;
            }
            beyond = Math.Max(foot, RoadWidthModel.ShoulderM(t.Roads[road].RoadClass, RoadWidthModel.AreaOf(Attrs[road])));
        }

        private readonly Dictionary<int, HashSet<long>> _nodeSets = new Dictionary<int, HashSet<long>>();

        private HashSet<long> NodesOf(TileData t, int road)
        {
            HashSet<long> set;
            if (_nodeSets.TryGetValue(road, out set)) return set;
            set = new HashSet<long>();
            int[] p = t.Roads[road].Points;
            for (int k = 0; k < p.Length / 2; k++) set.Add(Key(p[2 * k], p[2 * k + 1]));
            _nodeSets[road] = set;
            return set;
        }

        /// <summary>True when (x, z) lies within <paramref name="reach"/> of a node roads i and j share.</summary>
        private bool NearSharedNode(TileData t, int i, int j, double x, double z, double reach)
        {
            HashSet<long> nj = NodesOf(t, j);
            int[] p = t.Roads[i].Points;
            for (int k = 0; k < p.Length / 2; k++)
            {
                if (!nj.Contains(Key(p[2 * k], p[2 * k + 1]))) continue;
                if (Sq(p[2 * k] / 100.0 - x) + Sq(p[2 * k + 1] / 100.0 - z) < reach * reach) return true;
            }
            return false;
        }

        // -------------------------------------------------------------------------------------------------------
        // Parallel pieces
        // -------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Carriageways (and their footpaths) of streets that run side by side without sharing a node near the place
        /// (the one-way pair of Durbar Marg and Jamal, the Ring Road's main and service carriageways) stop where they would
        /// overlap: at every width sample each side may reach at most its share of the gap between the two centrelines, in
        /// proportion to what both want there. A side that has to give way narrows its carriageway first, down to the real
        /// width (at least the rideability floor), so the footpath survives (two footpaths back to back read as the
        /// separating kerbed strip); only when even that does not fit is the footpath dropped, and where the mapped
        /// centrelines are too close even for the real widths the carriageway moves away by up to <see cref="MaxPushM"/> and
        /// narrows for the rest (never under the rideability floor, nor a major road under <see cref="MinSqueezedMajorM"/>).
        /// The two carriageways of a dual road (RATR partners, median sides facing) mapped closer than their real widths and
        /// the median allow move apart outward by up to <see cref="MaxPushM"/> each and narrow on the median side for the
        /// rest (never under the rideability floor). Narrowing and moves are tapered at 1 : 20 and tile-border cut ends keep
        /// the width and position both tiles agree on.
        /// </summary>
        private void ClampParallel(TileData t)
        {
            int n = t.Roads.Count;
            var member = new bool[n];
            bool any = false;
            for (int i = 0; i < n; i++)
            {
                member[i] = SideCandidate(t, i) && StreetClass(t.Roads[i].RoadClass);
                any |= member[i];
            }
            if (!any) return;
            SegIndex idx = BuildIndex(t, member);
            double cosPar = Math.Cos(ParallelMaxDeg * Math.PI / 180.0);
            var near = new List<int>();
            var limL = new float[n][];
            var limR = new float[n][];
            var pair = new float[n][];
            for (int i = 0; i < n; i++)
            {
                if (!member[i]) continue;
                RoadRecord ri = t.Roads[i];
                RoadWidthProfile p = Profiles[i];
                for (int k = 0; k < p.Count; k++)
                {
                    double s = Math.Min(k * (double)p.StepM, p.LengthM);
                    double px, pz, dx, dz;
                    if (!RawAt(t, i, s, out px, out pz, out dx, out dz)) break;
                    double ux = -dz, uz = dx;
                    float cL, bL, cR, bR;
                    Extent(t, i, s, true, out cL, out bL);
                    Extent(t, i, s, false, out cR, out bR);
                    double reach = Math.Max(cL + bL, cR + bR) + 16.0;
                    idx.Near(px, pz, reach, near);
                    foreach (int q in near)
                    {
                        int j = idx.Road[q];
                        if (j == i) continue;
                        RoadRecord rj = t.Roads[j];
                        if (rj.OsmWayId == ri.OsmWayId || rj.Layer != ri.Layer) continue;
                        bool partner = Attrs[i].PartnerWayId != 0 && (ulong)Attrs[i].PartnerWayId == rj.OsmWayId ||
                                       Attrs[j].PartnerWayId != 0 && (ulong)Attrs[j].PartnerWayId == ri.OsmWayId;
                        double ax = idx.Ax[q], az = idx.Az[q], ex = idx.Bx[q] - ax, ez = idx.Bz[q] - az;
                        double len = Math.Sqrt(ex * ex + ez * ez);
                        ex /= len;
                        ez /= len;
                        if (Math.Abs(dx * ex + dz * ez) < cosPar) continue;
                        // P + U·t = A + E·u.
                        double det = ex * uz - ux * ez;
                        if (Math.Abs(det) < 1e-9) continue;
                        double wx = ax - px, wz = az - pz;
                        double tt = (ex * wz - ez * wx) / det, uu = (ux * wz - uz * wx) / det;
                        if (uu < 0 || uu > len || Math.Abs(tt) > reach) continue;
                        double sj = idx.S0[q] + (idx.S1[q] - idx.S0[q]) * uu / len;
                        bool left = tt > 0;
                        // Which side of j faces P.
                        double qx = ax + ex * uu, qz = az + ez * uu;
                        bool jLeft = ex * (pz - qz) - ez * (px - qx) > 0;
                        float cj, bj;
                        Extent(t, j, sj, jLeft, out cj, out bj);
                        float needI = left ? cL + bL : cR + bR, needJ = cj + bj;
                        double d = Math.Abs(tt);
                        if (needI + needJ <= d + 0.05) continue;
                        if (NearSharedNode(t, i, j, px, pz, SharedNodeReach * (needI + needJ))) continue;
                        if (partner)
                        {
                            // The other carriageway of a dual road (median sides facing) mapped closer than both real
                            // widths and the median allow: each takes half the overlap.
                            if (!left && !jLeft && p.Dual)
                            {
                                if (pair[i] == null) pair[i] = new float[p.Count];
                                pair[i][k] = Math.Max(pair[i][k], 0.5f * (float)(needI + needJ - d));
                            }
                            continue;
                        }
                        float share = (float)(d * needI / Math.Max(1e-3f, needI + needJ));
                        float[] lim = left ? limL[i] : limR[i];
                        if (lim == null)
                        {
                            lim = new float[p.Count];
                            for (int m = 0; m < lim.Length; m++) lim[m] = float.PositiveInfinity;
                            if (left) limL[i] = lim;
                            else limR[i] = lim;
                        }
                        if (share < lim[k]) lim[k] = share;
                    }
                }
            }
            for (int i = 0; i < n; i++)
            {
                if (limL[i] == null && limR[i] == null && pair[i] == null) continue;
                RoadWidthProfile p = Profiles[i];
                RoadRecord r = t.Roads[i];
                float real = p.RealM, rideable = RoadWidthModel.RideableMinM(r.RoadClass);
                float wMin = Math.Max(real, rideable);
                float wSqueeze = Math.Max(rideable, RoadWidthModel.IsMajor(r.RoadClass) ? MinSqueezedMajorM : 0f);
                float end0 = p.Drawn[0], end1 = p.Drawn[p.Count - 1];
                var move = new float[p.Count];
                for (int k = 0; k < p.Count; k++)
                {
                    float w = p.Drawn[k];
                    float cL = p.Dual ? w - 0.5f * real : 0.5f * w, cR = p.Dual ? 0.5f * real : 0.5f * w;
                    float minL = Math.Min(cL, p.Dual ? wMin - 0.5f * real : 0.5f * wMin), minR = Math.Min(cR, 0.5f * wMin);
                    float nL = cL, nR = cR;
                    float fL = p.FootLeft[k], fR = p.FootRight[k];
                    float resL = 0f, resR = 0f;
                    if (limL[i] != null && !float.IsPositiveInfinity(limL[i][k])) resL = GiveWay(limL[i][k], cL, minL, ref fL, out nL);
                    if (limR[i] != null && !float.IsPositiveInfinity(limR[i][k]) && !p.Dual) resR = GiveWay(limR[i][k], cR, minR, ref fR, out nR);
                    // What the real widths cannot give way (the mapped centrelines are too close for them, usually a width
                    // tag that counts both carriageways): the carriageway moves away from the tighter side by up to
                    // MaxPushM and narrows for the rest, never under the rideability floor (nor under a bus width on the
                    // major classes, so a short squeeze never takes buses off the whole piece).
                    float m = Math.Min(resR, MaxPushM) - Math.Min(resL, MaxPushM);
                    float floorL = p.Dual ? Math.Min(nL, wSqueeze - 0.5f * real) : Math.Min(nL, 0.5f * wSqueeze);
                    float floorR = Math.Min(nR, 0.5f * wSqueeze);
                    nL = Math.Max(floorL, nL - Math.Max(0f, resL - MaxPushM));
                    nR = Math.Max(floorR, nR - Math.Max(0f, resR - MaxPushM));
                    float drawn = p.Dual ? nL + 0.5f * real : 2f * Math.Min(nL, nR);
                    if (pair[i] != null && pair[i][k] > 0f)
                    {
                        // Dual partners: move outward by up to MaxPushM, the rest narrows the median side (down to the
                        // rideability floor); the shift is the whole half overlap so the median edge comes back by it.
                        float h = pair[i][k];
                        float narrow = Math.Min(Math.Max(0f, h - MaxPushM), Math.Max(0f, drawn - rideable));
                        drawn -= narrow;
                        m += Math.Min(h, MaxPushM + narrow);
                    }
                    move[k] = m;
                    p.Drawn[k] = drawn;
                    p.FootLeft[k] = fL;
                    p.FootRight[k] = fR;
                }
                Envelope(p.Drawn, p.Count, p.StepM / RoadWidthModel.TaperRatio);
                float kk = p.StepM / RoadWidthModel.TaperRatio;
                for (int k = 0; k < p.Count; k++)
                {
                    if (r.HasPrevContext) p.Drawn[k] = Math.Max(p.Drawn[k], end0 - kk * k);
                    if (r.HasNextContext) p.Drawn[k] = Math.Max(p.Drawn[k], end1 - kk * (p.Count - 1 - k));
                }
                AddShift(r, p, move, Knees[2 * i].Has, Knees[2 * i + 1].Has);
            }
        }

        /// <summary>Largest sideways move of a carriageway whose real width cannot give way to a parallel neighbour.</summary>
        public const float MaxPushM = 1.0f;

        /// <summary>A major road (trunk to tertiary) squeezed by a parallel neighbour below its real width keeps at least
        /// this (a bus width, W2_DESIGN 4.4), so access masks of the whole piece do not change.</summary>
        public const float MinSqueezedMajorM = 6.0f;

        /// <summary>Add a sideways move (positive = left) to a profile's carriageway shift: each direction tapered at 1 : 20
        /// on both sides of where it is needed, and none at tile-border cut ends (both tiles keep the cut where it is) or at
        /// knee ends (the joined piece meets it there).</summary>
        private static void AddShift(RoadRecord r, RoadWidthProfile p, float[] move, bool kneeStart, bool kneeEnd)
        {
            int n = p.Count;
            float k = p.StepM / RoadWidthModel.TaperRatio;
            var pos = new float[n];
            var neg = new float[n];
            for (int i = 0; i < n; i++)
            {
                pos[i] = Math.Max(0f, move[i]);
                neg[i] = Math.Max(0f, -move[i]);
            }
            Raise(pos, n, k);
            Raise(neg, n, k);
            bool any = false;
            for (int i = 0; i < n; i++)
            {
                float v = pos[i] - neg[i];
                float cap = float.PositiveInfinity;
                if (r.HasPrevContext || kneeStart) cap = Math.Min(cap, k * i);
                if (r.HasNextContext || kneeEnd) cap = Math.Min(cap, k * (n - 1 - i));
                v = Math.Max(-cap, Math.Min(cap, v));
                move[i] = v;
                any |= Math.Abs(v) > 1e-3f;
            }
            if (!any) return;
            if (p.Shift == null) p.Shift = new float[n];
            for (int i = 0; i < n; i++) p.Shift[i] += move[i];
        }

        /// <summary>Upper envelope with a maximum slope: v[i] = max_j (v[j] − k·|i − j|).</summary>
        private static void Raise(float[] v, int n, float k)
        {
            for (int i = 1; i < n; i++)
                if (v[i] < v[i - 1] - k) v[i] = v[i - 1] - k;
            for (int i = n - 2; i >= 0; i--)
                if (v[i] < v[i + 1] - k) v[i] = v[i + 1] - k;
        }

        /// <summary>One side of a width sample giving way to a parallel neighbour: it may reach <paramref name="room"/>
        /// from the centreline; the carriageway extent <paramref name="c"/> narrows first (down to <paramref name="cMin"/>)
        /// so the footpath <paramref name="foot"/> keeps at least <see cref="RoadWidthModel.MinFootpathM"/>, else the
        /// footpath goes. Returns how far the carriageway still reaches beyond <paramref name="room"/> (0 when it fits).</summary>
        private static float GiveWay(float room, float c, float cMin, ref float foot, out float cNew)
        {
            float drawnFoot = foot > 0f ? Math.Max(foot, RoadWidthModel.MinFootpathM) : 0f;
            cNew = c;
            if (c + drawnFoot <= room + 0.05f) return 0f;
            if (foot > 0f && room - cMin >= RoadWidthModel.MinFootpathM)
            {
                float f = Math.Min(drawnFoot, room - cMin);
                foot = f;
                cNew = Math.Min(c, room - f);
                return 0f;
            }
            foot = 0f;
            cNew = Math.Max(cMin, Math.Min(c, room));
            return Math.Max(0f, cNew - room);
        }

        /// <summary>Lower envelope with a maximum slope: v[i] = min_j (v[j] + k·|i − j|).</summary>
        private static void Envelope(float[] v, int n, float k)
        {
            for (int i = 1; i < n; i++)
                if (v[i] > v[i - 1] + k) v[i] = v[i - 1] + k;
            for (int i = n - 2; i >= 0; i--)
                if (v[i] > v[i + 1] + k) v[i] = v[i + 1] + k;
        }

        // -------------------------------------------------------------------------------------------------------
        // Mapped sidewalks and footway crossings
        // -------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Footways, paths, cycleways and bridleways mapped along a street (OSM sidewalks) or across its carriageway are not
        /// drawn as their own ribbon where they lie on that street: a stretch running parallel inside the carriageway plus
        /// its footpath (or just beyond the carriageway edge) becomes that side's footpath (widened to cover the mapped
        /// sidewalk where the corridor leaves room), and a stretch crossing the carriageway is the carriageway itself. Both
        /// become gaps of the footway (<see cref="Cuts"/>, pinned to the street's lift) and are listed in
        /// <see cref="Absorbed"/>; stretches near tile-border cuts stay drawn so both tiles agree.
        /// </summary>
        private void AbsorbSidewalks(TileData t)
        {
            int n = t.Roads.Count;
            var street = new bool[n];
            bool anyStreet = false, anyFoot = false;
            for (int i = 0; i < n; i++)
            {
                bool cand = SideCandidate(t, i);
                street[i] = cand && StreetClass(t.Roads[i].RoadClass);
                anyStreet |= street[i];
                anyFoot |= cand && SidewalkClass(t.Roads[i].RoadClass);
            }
            if (!anyStreet || !anyFoot) return;
            SegIndex idx = BuildIndex(t, street);
            double cosPar = Math.Cos(ParallelMaxDeg * Math.PI / 180.0);
            var near = new List<int>();
            var flags = new List<int>();   // per sample: street road index + 1 when absorbed, 0 when drawn
            var wantFoot = new Dictionary<long, float>(); // (road, side, sample) → wanted footpath width
            for (int f = 0; f < n; f++)
            {
                RoadRecord rf = t.Roads[f];
                if (!SidewalkClass(rf.RoadClass) || !SideCandidate(t, f)) continue;
                double len = Profiles[f].LengthM;
                int samples = (int)Math.Floor(len / SidewalkStepM);
                if (samples < 2) continue;
                flags.Clear();
                bool anyAbsorbed = false;
                for (int k = 0; k < samples; k++)
                {
                    double s = (k + 0.5) * SidewalkStepM;
                    flags.Add(0);
                    if (rf.HasPrevContext && s < AbsorbBorderMarginM || rf.HasNextContext && s > len - AbsorbBorderMarginM) continue;
                    double px, pz, dx, dz;
                    if (!RawAt(t, f, s, out px, out pz, out dx, out dz)) break;
                    idx.Near(px, pz, 24.0, near);
                    int best = -1;
                    double bestScore = double.MaxValue;
                    long bestKey = 0;
                    float bestWant = 0f;
                    foreach (int q in near)
                    {
                        int m = idx.Road[q];
                        if (t.Roads[m].Layer != rf.Layer) continue;
                        double ax = idx.Ax[q], az = idx.Az[q], ex = idx.Bx[q] - ax, ez = idx.Bz[q] - az;
                        double sl = Math.Sqrt(ex * ex + ez * ez);
                        ex /= sl;
                        ez /= sl;
                        double u = (px - ax) * ex + (pz - az) * ez;
                        if (u < 0 || u > sl) continue;
                        double lat = ex * (pz - az) - ez * (px - ax); // positive: left of the street
                        double sm = idx.S0[q] + (idx.S1[q] - idx.S0[q]) * u / sl;
                        bool left = lat > 0;
                        float c, beyond;
                        Extent(t, m, sm, left, out c, out beyond);
                        RoadWidthProfile pm = Profiles[m];
                        float foot = pm.Sample(left ? pm.FootLeft : pm.FootRight, sm);
                        if (foot > 0f) foot = Math.Max(foot, RoadWidthModel.MinFootpathM);
                        double edge = Math.Abs(lat) - c; // beyond the carriageway edge (negative: on it)
                        bool parallel = Math.Abs(dx * ex + dz * ez) >= cosPar;
                        float want = 0f;
                        bool take;
                        if (!parallel) take = edge < -0.3; // a footway crossing the carriageway
                        else if (edge <= foot + 0.5) take = true; // on the carriageway or its footpath
                        else if (edge < foot + RoadWidthModel.FootRideableM * 0.5 + 0.25)
                        {
                            // Just beyond the footpath (or the bare edge): the street's footpath is widened over it where
                            // the corridor leaves room, else the sidewalk keeps its own ribbon.
                            want = (float)Math.Min(MaxAbsorbedFootpathM, edge + 1.0);
                            float room = FootRoom(t, m, sm, left);
                            take = want <= room;
                        }
                        else take = false;
                        if (!take) continue;
                        if (parallel && edge > 0.3 && foot <= 0f && want <= 0f)
                        {
                            want = (float)Math.Min(MaxAbsorbedFootpathM, Math.Max(RoadWidthModel.MinFootpathM, edge + 1.0));
                            want = Math.Min(want, FootRoom(t, m, sm, left));
                            if (want < RoadWidthModel.MinFootpathM) want = 0f;
                        }
                        double score = Math.Abs(edge);
                        if (score >= bestScore) continue;
                        bestScore = score;
                        best = m;
                        bestWant = want;
                        int sample = (int)Math.Round(sm / Math.Max(1e-3, pm.StepM));
                        sample = sample < 0 ? 0 : sample >= pm.Count ? pm.Count - 1 : sample;
                        bestKey = (long)m << 32 | (long)(left ? 0 : 1) << 31 | (uint)sample;
                    }
                    if (best < 0) continue;
                    flags[k] = best + 1;
                    anyAbsorbed = true;
                    if (bestWant > 0f)
                    {
                        float old;
                        if (!wantFoot.TryGetValue(bestKey, out old) || old < bestWant) wantFoot[bestKey] = bestWant;
                    }
                }
                if (!anyAbsorbed) continue;
                RunsToGaps(t, f, flags, len);
            }
            // Widen (or give) the streets' footpaths where absorbed sidewalks asked for them.
            foreach (KeyValuePair<long, float> kv in wantFoot)
            {
                int m = (int)(kv.Key >> 32);
                bool right = (kv.Key >> 31 & 1) != 0;
                int k = (int)(kv.Key & 0x7FFFFFFF);
                RoadWidthProfile pm = Profiles[m];
                float[] side = right ? pm.FootRight : pm.FootLeft;
                if (pm.Dual && right) continue;
                // The neighbouring samples too, so a short absorbed stretch still makes a footpath run.
                for (int q = Math.Max(0, k - 1); q <= Math.Min(pm.Count - 1, k + 1); q++)
                    if (side[q] < kv.Value) side[q] = kv.Value;
            }
        }

        /// <summary>The footpath width the corridor leaves beside road <paramref name="m"/>'s carriageway on one side at
        /// <paramref name="s"/> (unbounded corridors give the largest absorbed footpath).</summary>
        private float FootRoom(TileData t, int m, double s, bool left)
        {
            RoadWidthProfile p = Profiles[m];
            // The tighter of the two samples around s (open samples are +∞, which must not be interpolated).
            int k0 = (int)Math.Floor(s / Math.Max(1e-3, p.StepM));
            k0 = k0 < 0 ? 0 : k0 > p.Count - 1 ? p.Count - 1 : k0;
            float lim = Math.Min(p.Limit[k0], p.Limit[Math.Min(p.Count - 1, k0 + 1)]);
            if (float.IsPositiveInfinity(lim)) return MaxAbsorbedFootpathM;
            float c, beyond;
            Extent(t, m, s, left, out c, out beyond);
            float sh = RoadWidthModel.ShoulderM(t.Roads[m].RoadClass, RoadWidthModel.AreaOf(Attrs[m]));
            return 0.5f * lim - c - sh;
        }

        /// <summary>Turn the absorbed samples of footway <paramref name="f"/> into gap pairs (holes and runs shorter than
        /// <see cref="MinAbsorbedM"/> merged away), recorded in <see cref="Cuts"/> and <see cref="Absorbed"/>.</summary>
        private void RunsToGaps(TileData t, int f, List<int> flags, double len)
        {
            int n = flags.Count;
            int minRun = (int)Math.Ceiling(MinAbsorbedM / SidewalkStepM);
            // Fill short holes between absorbed samples.
            for (int k = 0; k < n; k++)
            {
                if (flags[k] != 0) continue;
                int e = k;
                while (e < n && flags[e] == 0) e++;
                if (k > 0 && e < n && e - k < minRun)
                    for (int q = k; q < e; q++) flags[q] = flags[k - 1];
                k = e;
            }
            var spans = new List<double>();
            for (int k = 0; k < n; k++)
            {
                if (flags[k] == 0) continue;
                int e = k;
                while (e < n && flags[e] != 0) e++;
                if (e - k >= minRun)
                {
                    double s0 = k * SidewalkStepM, s1 = e >= n ? len : e * SidewalkStepM;
                    if (k == 0) s0 = 0;
                    s0 = Math.Max(0, s0);
                    s1 = Math.Min(len, s1);
                    if (s1 - s0 >= MinAbsorbedM)
                    {
                        spans.Add(s0);
                        spans.Add(s1);
                        int top = flags[k] - 1;
                        AddCutPair(f, DrawnCutAt(t, f, s0), DrawnCutAt(t, f, s1), top);
                    }
                }
                k = e;
            }
            if (spans.Count > 0) Absorbed[f] = spans.ToArray();
        }

        // -------------------------------------------------------------------------------------------------------
        // Building passages
        // -------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Building passages (RSTR Passage: a dhoka under a house that stays whole) are drawn as the gateway: their real
        /// width within [<see cref="MinPassageWidthM"/>, <see cref="MaxPassageWidthM"/>], no footpaths, never the 4.8 m
        /// floor (it would cut the house); its lanes (<see cref="RoadWidthProfile.Width"/>) never wider than the gateway.
        /// Tile-border cut ends keep the width both tiles agree on and ramp down to the gateway at 1 : 20.
        /// </summary>
        private void Passages(TileData t)
        {
            for (int i = 0; i < t.Roads.Count; i++)
            {
                if (!IsPassage(i)) continue;
                RoadRecord r = t.Roads[i];
                RoadWidthProfile p = Profiles[i];
                float gate = Math.Max(MinPassageWidthM, Math.Min(MaxPassageWidthM, p.RealM));
                float end0 = p.Drawn[0], end1 = p.Drawn[p.Count - 1];
                float k = p.StepM / RoadWidthModel.TaperRatio;
                for (int j = 0; j < p.Count; j++)
                {
                    float w = gate;
                    if (r.HasPrevContext) w = Math.Max(w, end0 - k * j);
                    if (r.HasNextContext) w = Math.Max(w, end1 - k * (p.Count - 1 - j));
                    p.Drawn[j] = Math.Min(p.Drawn[j], w);
                    p.Width[j] = Math.Min(p.Width[j], p.Drawn[j]);
                    p.FootLeft[j] = p.FootRight[j] = 0f;
                }
            }
        }

        // -------------------------------------------------------------------------------------------------------
        // Corridor shift
        // -------------------------------------------------------------------------------------------------------

        /// <summary>
        /// The corridor shift of road <paramref name="road"/> (metres, positive = left, one sample every
        /// <see cref="CorridorShiftSpacingM"/> from the first rendered point), or null. The data package's RSTR record
        /// carries it as <c>RoadStructureRecord.CorridorShiftCm</c> (docs/DATA_FORMATS.md 1.15), which the base stub of
        /// <see cref="RoadStructureRecord"/> does not have yet: integration wires it here (open issue in
        /// docs/research/w2/ref_roads.md §8); until then only <see cref="WithCorridorShifts"/> sets shifts.
        /// </summary>
        private float[] CorridorShiftOf(int road)
        {
            return _shiftOverride != null && road < _shiftOverride.Length ? _shiftOverride[road] : null;
        }

        /// <summary>Spacing of the RSTR corridor shift samples.</summary>
        public const double CorridorShiftSpacingM = 5.0;

        private readonly float[][] _shiftOverride;

        /// <summary>Resample a corridor shift (samples every <see cref="CorridorShiftSpacingM"/>, metres) onto the width
        /// profile of road <paramref name="road"/>.</summary>
        private void ApplyCorridorShift(int road)
        {
            float[] src = CorridorShiftOf(road);
            RoadWidthProfile p = Profiles[road];
            if (src == null || src.Length == 0)
            {
                p.Shift = null;
                return;
            }
            var dst = new float[p.Count];
            bool any = false;
            for (int k = 0; k < p.Count; k++)
            {
                double f = k * (double)p.StepM / CorridorShiftSpacingM;
                int i = (int)Math.Floor(f);
                float v;
                if (i <= 0 && f <= 0) v = src[0];
                else if (i >= src.Length - 1) v = src[src.Length - 1];
                else v = (float)(src[i] + (src[i + 1] - src[i]) * (f - i));
                dst[k] = v;
                any |= Math.Abs(v) > 1e-4f;
            }
            p.Shift = any ? dst : null;
        }

        /// <summary>
        /// Build the layout of <paramref name="t"/> with the given corridor shifts (per road: samples every
        /// <see cref="CorridorShiftSpacingM"/>, metres, positive = left; null entries = none) and make it the tile's cached
        /// layout. For tests and for integration until the RSTR record carries the shift (see <see cref="CorridorShiftOf"/>).
        /// </summary>
        internal static RoadLayout WithCorridorShifts(TileData t, float[][] shiftM)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            var layout = new RoadLayout(t, shiftM);
            lock (Cache)
            {
                Cache.Remove(t);
                Cache.Add(t, layout);
            }
            return layout;
        }
    }
}
