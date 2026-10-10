using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.Core.Meshing.Bridges
{
    /// <summary>
    /// The road structure records of a tile (docs/W2_DETAIL_CONTRACT.md §3): the tile's own (written by the data
    /// pipeline) when it carries one per road, else records derived here from the W1 tags so bridges are built on
    /// packs without the structure chunk. The derivation is a per-tile, lighter version of the pipeline's rules
    /// (pipeline/ghumante_pipeline/structures.py):
    /// <list type="bullet">
    /// <item>A bridge deck starts as the W1 ribbon: straight between its lifted ends (terrain at the first and last
    /// rendered point plus the ribbon lift, which is the same in both tiles at a cut end), never below the lifted
    /// terrain under it. Ends stay on that line (the approach roads are not raised here).</item>
    /// <item>Every road it crosses in plan without a shared node and below it is a crossing. A vehicle bridge rises over
    /// a crossing to <c>lower road + </c><see cref="RoadClearance.MinUnderpassClearanceM"/><c> + </c>
    /// <see cref="BridgeStyle.CrossingDepthM"/> through its interior points, with ramps at the class grade
    /// (<see cref="BridgeStyle.GradeFor"/>) from its pinned ends; crossings it cannot clear that way are dropped
    /// (the layout leaves them open). A bridge over no water that clears a road is a <see cref="RoadStructureKind.Flyover"/>.</item>
    /// <item>Foot bridges over no water that cross a vehicle road are foot overbridges. Every foot bridge joined to
    /// one (at any shared point) belongs to the same structure, and the whole structure gets one flat deck at the
    /// highest <c>lower road + 5.5 m + </c><see cref="BridgeStyle.FootCrossingDepthM"/> (at a cut end: the border
    /// terrain + the same + <see cref="CutMarginM"/>, which both tiles compute alike). The <c>highway=steps</c>
    /// chains joined to it get stair records from the deck down to the ground at their far end.</item>
    /// </list>
    /// The derived records live in <see cref="BridgeLayout.Structures"/>; <see cref="For"/> publishes them so the
    /// roads and collide packages can draw and query the same heights on packs without the chunk (integration).
    /// Deterministic; allocates only per call.
    /// </summary>
    public static class BridgeStructures
    {
        /// <summary>Extra height of a foot overbridge deck cut by a tile border over the clearance at the border (the
        /// border's terrain stands in for the road below, which only one of the two tiles sees).</summary>
        public const float CutMarginM = 0.5f;

        /// <summary>Longest steps chain followed from a foot overbridge.</summary>
        public const double MaxStairChainM = 60.0;

        /// <summary>Steepest mean grade of a derived stair (rise over plan length, about 32°).</summary>
        public const double MaxStairGrade = 0.62;

        private static readonly RoadOptions DefaultRoadOptions = new RoadOptions();

        /// <summary>True when the tile carries one structure record per road (the data package's chunk).</summary>
        public static bool HasRecords(TileData t)
        {
            return t.RoadStructures.Count > 0 && t.RoadStructures.Count == t.Roads.Count;
        }

        /// <summary>The records the bridge layout of a tile uses (the tile's own, or derived), one per road: the
        /// heights every consumer should draw and query on a pack without the structure chunk. Cached with the layout;
        /// do not modify the deck arrays.</summary>
        public static IReadOnlyList<RoadStructureRecord> For(TileData t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            return BridgeLayout.For(t).Structures;
        }

        /// <summary>One record per road: the tile's own when present, else <see cref="Derive"/>.</summary>
        public static RoadStructureRecord[] Resolve(TileData t, IHeightSampler ground, out bool derived)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (HasRecords(t))
            {
                derived = false;
                var a = new RoadStructureRecord[t.Roads.Count];
                for (int i = 0; i < a.Length; i++) a[i] = t.RoadStructures[i];
                return a;
            }
            derived = true;
            return Derive(t, ground);
        }

        /// <summary>A road crossing under a bridge (plan crossing without a shared node).</summary>
        internal struct Crossing
        {
            public int Road;

            /// <summary>Along the bridge from its first rendered point, and the half length of the deck stretch over
            /// the lower road's corridor.</summary>
            public double S, Half;

            /// <summary>Surface of the lower road at the crossing.</summary>
            public float LowY;

            /// <summary>The lower road is a vehicle road (not a foot class).</summary>
            public bool LowMotor;

            /// <summary>Clearance kept over the lower road (<see cref="BridgeStyle.UnderClearanceM"/>).</summary>
            public float Clear
            {
                get { return LowMotor ? RoadClearance.MinUnderpassClearanceM : BridgeStyle.FootHeadroomM; }
            }
        }

        /// <summary>Structure records derived from <see cref="RoadFlags.Bridge"/>, the layer and the terrain (see the
        /// class summary). Roads that are not structures get <see cref="RoadStructureRecord.Absent"/> (car access by
        /// class).</summary>
        public static RoadStructureRecord[] Derive(TileData t, IHeightSampler ground)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (ground == null) throw new ArgumentNullException(nameof(ground));
            int n = t.Roads.Count;
            var recs = new RoadStructureRecord[n];
            RoadLayout layout = n > 0 ? RoadLayout.For(t) : null;
            var water = new bool[n];
            var bridge = new bool[n];
            for (int i = 0; i < n; i++)
            {
                RoadRecord r = t.Roads[i];
                var rec = RoadStructureRecord.Absent;
                rec.Layer = r.Layer;
                if (BridgeStyle.IsFoot(r.RoadClass)) rec.Flags = RoadStructureFlags.None;
                if ((r.Flags & RoadFlags.Tunnel) != 0)
                {
                    rec.Kind = RoadStructureKind.Tunnel;
                    rec.Flags &= ~RoadStructureFlags.CarAccessible;
                    recs[i] = rec;
                    continue;
                }
                if ((r.Flags & RoadFlags.Ford) != 0) rec.Kind = RoadStructureKind.Ford;
                if ((r.Flags & RoadFlags.Bridge) != 0 && RoadMesher.IsDrawn(r, null))
                {
                    bridge[i] = true;
                    water[i] = CrossesWater(t, r);
                    rec.Kind = RoadStructureKind.Bridge;
                    if (water[i]) rec.Flags |= RoadStructureFlags.WaterCrossing;
                    rec.RailingHeightM = BridgeStyle.IsFoot(r.RoadClass) ? 1.2f : BridgeStyle.RailHeightM;
                    rec.DeckY = BaseProfile(t, i, ground, layout);
                }
                recs[i] = rec;
            }

            var crossings = new List<Crossing>[n];
            for (int i = 0; i < n; i++)
                if (bridge[i]) crossings[i] = FindCrossings(t, i, recs, ground, layout);

            FootOverbridges(t, ground, recs, bridge, water, crossings);

            // Vehicle bridges: rise over the roads below through the interior points, as far as the grade allows.
            for (int i = 0; i < n; i++)
            {
                RoadRecord r = t.Roads[i];
                if (!bridge[i] || BridgeStyle.IsFoot(r.RoadClass) || crossings[i].Count == 0) continue;
                var active = new List<Crossing>(crossings[i]);
                float[] y = null;
                while (active.Count > 0)
                {
                    y = Raise(t, i, recs[i].DeckY, active, BridgeStyle.GradeFor(r.RoadClass));
                    int before = active.Count;
                    active.RemoveAll(c => !Clears(t.Roads[i], y, c, c.Clear + BridgeStyle.CrossingDepthM));
                    if (active.Count == before) break;
                }
                if (active.Count == 0) continue;
                RoadStructureRecord rec = recs[i];
                rec.DeckY = y;
                if (!water[i]) rec.Kind = RoadStructureKind.Flyover;
                recs[i] = rec;
            }
            return recs;
        }

        // -------------------------------------------------------------------------------------------------------------
        // Deck profiles
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>The W1 straight deck between the lifted ends, never below the lifted terrain, one height per point (the
        /// line the W1 road ribbon of a bridge follows).</summary>
        internal static float[] BaseProfile(TileData t, int ri, IHeightSampler ground, RoadLayout layout)
        {
            RoadRecord r = t.Roads[ri];
            int[] p = r.Points;
            int count = p.Length / 2;
            int first = r.HasPrevContext ? 1 : 0, last = r.HasNextContext ? count - 2 : count - 1;
            float lift = RoadMesher.LiftOf(r, DefaultRoadOptions);
            double[] s = Along(p, first, last);
            double total = s[last];
            float hStart = Ground(t, ground, p[2 * first] / 100.0, p[2 * first + 1] / 100.0) + lift;
            float hEnd = Ground(t, ground, p[2 * last] / 100.0, p[2 * last + 1] / 100.0) + lift;
            var y = new float[count];
            for (int k = first; k <= last; k++)
            {
                double f = total > 0 ? s[k] / total : 0;
                double cx = p[2 * k] / 100.0, cz = p[2 * k + 1] / 100.0;
                double tx, tz;
                Direction(p, count, k, out tx, out tz);
                double half = layout != null ? layout.HalfWidthAt(ri, s[k]) : 0.5 * RoadStyle.WidthM(r);
                // A cut end samples only its centre: it lies on the border, where both tiles share the heights.
                bool cut = k == first && r.HasPrevContext || k == last && r.HasNextContext;
                float g = Ground(t, ground, cx, cz);
                if (!cut) g = Math.Max(g, Math.Max(Ground(t, ground, cx - tz * half, cz + tx * half), Ground(t, ground, cx + tz * half, cz - tx * half)));
                y[k] = Math.Max((float)(hStart + (hEnd - hStart) * f), g + lift);
            }
            if (first > 0) y[0] = y[first];
            if (last < count - 1) y[count - 1] = y[last];
            return y;
        }

        /// <summary>The deck raised over the crossings: each crossing sets its requirement on the points inside the
        /// deck stretch over the lower corridor and the nearest point on either side, the requirement spreads outward
        /// as a cone at <paramref name="grade"/>, and cones from the pinned ends (the first and last rendered point
        /// stay on the base line) cap it. Never below the base.</summary>
        private static float[] Raise(TileData t, int ri, float[] baseY, List<Crossing> active, float grade)
        {
            RoadRecord r = t.Roads[ri];
            int[] p = r.Points;
            int count = p.Length / 2;
            int first = r.HasPrevContext ? 1 : 0, last = r.HasNextContext ? count - 2 : count - 1;
            double[] s = Along(p, first, last);
            double total = s[last];
            var req = new float[count];
            for (int k = 0; k < count; k++) req[k] = float.NegativeInfinity;
            foreach (Crossing c in active)
            {
                float need = c.LowY + c.Clear + BridgeStyle.CrossingDepthM;
                double e0 = Math.Max(0, c.S - c.Half), e1 = Math.Min(total, c.S + c.Half);
                // The points inside the stretch, and the nearest one before and after it (the deck is linear between).
                int kb = first, ka = last;
                for (int k = first; k <= last; k++)
                {
                    if (s[k] <= e0) kb = k;
                    if (s[k] >= e0 && s[k] <= e1) req[k] = Math.Max(req[k], need);
                }
                for (int k = last; k >= first; k--)
                    if (s[k] >= e1) ka = k;
                req[kb] = Math.Max(req[kb], need);
                req[ka] = Math.Max(req[ka], need);
            }
            var y = (float[])baseY.Clone();
            for (int k = first + 1; k < last; k++)
            {
                float v = y[k];
                for (int j = first; j <= last; j++)
                    if (!float.IsNegativeInfinity(req[j])) v = Math.Max(v, (float)(req[j] - grade * Math.Abs(s[k] - s[j])));
                float cap = (float)Math.Min(baseY[first] + grade * s[k], baseY[last] + grade * (total - s[k]));
                y[k] = Math.Max(baseY[k], Math.Min(v, cap));
            }
            if (first > 0) y[0] = y[first];
            if (last < count - 1) y[count - 1] = y[last];
            return y;
        }

        /// <summary>True when the deck <paramref name="y"/> stands at least <paramref name="above"/> over the crossing's
        /// lower road along the whole stretch over its corridor.</summary>
        private static bool Clears(RoadRecord r, float[] y, in Crossing c, float above)
        {
            return MinOver(r, y, c.S - c.Half, c.S + c.Half) - c.LowY >= above - 0.01f;
        }

        /// <summary>The lowest deck height on [s0, s1] (along from the first rendered point), interpolated linearly.</summary>
        internal static float MinOver(RoadRecord r, float[] y, double s0, double s1)
        {
            int[] p = r.Points;
            int count = p.Length / 2;
            int first = r.HasPrevContext ? 1 : 0, last = r.HasNextContext ? count - 2 : count - 1;
            double[] s = Along(p, first, last);
            s0 = Math.Max(0, s0);
            s1 = Math.Min(s[last], s1);
            float min = Math.Min(At(y, s, first, last, s0), At(y, s, first, last, s1));
            for (int k = first; k <= last; k++)
                if (s[k] > s0 && s[k] < s1) min = Math.Min(min, y[k]);
            return min;
        }

        private static float At(float[] y, double[] s, int first, int last, double at)
        {
            for (int k = first; k < last; k++)
            {
                if (at > s[k + 1]) continue;
                double len = s[k + 1] - s[k];
                double f = len > 1e-9 ? (at - s[k]) / len : 0;
                f = f < 0 ? 0 : f > 1 ? 1 : f;
                return (float)(y[k] + (y[k + 1] - y[k]) * f);
            }
            return y[last];
        }

        /// <summary>Along distances of the points from the first rendered point (context points extend it).</summary>
        private static double[] Along(int[] p, int first, int last)
        {
            int count = p.Length / 2;
            var s = new double[count];
            for (int k = first + 1; k < count; k++) s[k] = s[k - 1] + SegLen(p, k - 1);
            for (int k = first - 1; k >= 0; k--) s[k] = s[k + 1] - SegLen(p, k);
            return s;
        }

        // -------------------------------------------------------------------------------------------------------------
        // Crossings
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>Every drawn road that passes under bridge <paramref name="ri"/> in plan without sharing a node
        /// there (a lower layer, or not a bridge), whatever its height.</summary>
        private static List<Crossing> FindCrossings(TileData t, int ri, RoadStructureRecord[] recs, IHeightSampler ground, RoadLayout layout)
        {
            var list = new List<Crossing>();
            RoadRecord r = t.Roads[ri];
            int[] p = r.Points;
            int count = p.Length / 2;
            int first = r.HasPrevContext ? 1 : 0, last = r.HasNextContext ? count - 2 : count - 1;
            double[] along = Along(p, first, last);
            for (int j = 0; j < t.Roads.Count; j++)
            {
                if (j == ri) continue;
                RoadRecord o = t.Roads[j];
                if (!IsLower(o, r)) continue;
                int[] q = o.Points;
                int oc = q.Length / 2;
                double oAlong = 0;
                for (int b = 0; b + 1 < oc; b++) // context segments too: a crossing inside this tile counts here
                {
                    double blen = SegLen(q, b);
                    for (int a = first; a < last; a++)
                    {
                        if (!Crosses(p[2 * a], p[2 * a + 1], p[2 * a + 2], p[2 * a + 3], a == first, a + 1 == last, q[2 * b], q[2 * b + 1], q[2 * b + 2],
                                     q[2 * b + 3])) continue;
                        double ax = p[2 * a] / 100.0, az = p[2 * a + 1] / 100.0, bx = p[2 * a + 2] / 100.0, bz = p[2 * a + 3] / 100.0;
                        double cx = q[2 * b] / 100.0, cz = q[2 * b + 1] / 100.0, dx = q[2 * b + 2] / 100.0, dz = q[2 * b + 3] / 100.0;
                        double ta, tb;
                        Intersect(ax, az, bx, bz, cx, cz, dx, dz, out ta, out tb);
                        float lowY;
                        float[] od = recs[j].DeckY;
                        if (od != null && od.Length == oc) lowY = (float)(od[b] + (od[b + 1] - od[b]) * tb);
                        else lowY = Ground(t, ground, ax + (bx - ax) * ta, az + (bz - az) * ta) + RoadMesher.LiftOf(o, DefaultRoadOptions);
                        double sin, cos;
                        Angle(bx - ax, bz - az, dx - cx, dz - cz, out sin, out cos);
                        double lowHalf = 0.5 * CorridorWidth(layout, o, j, (layout != null ? layout.AlongAt(j, b) : oAlong) + blen * tb) + 0.5;
                        double deckHalf = 0.5 * RoadStyle.WidthM(r) + 2.5;
                        list.Add(new Crossing
                        {
                            Road = j, S = along[a] + (along[a + 1] - along[a]) * ta, Half = lowHalf / sin + deckHalf * cos / sin, LowY = lowY,
                            LowMotor = !BridgeStyle.IsFoot(o.RoadClass),
                        });
                    }
                    oAlong += blen;
                }
            }
            return list;
        }

        /// <summary>|sin| and |cos| of the angle between two plan directions, the sine held at 0.25 or more (a
        /// grazing crossing still gets a finite deck stretch).</summary>
        internal static void Angle(double ux, double uz, double vx, double vz, out double sin, out double cos)
        {
            double lu = Math.Sqrt(ux * ux + uz * uz), lv = Math.Sqrt(vx * vx + vz * vz);
            sin = lu > 0 && lv > 0 ? Math.Abs(ux * vz - uz * vx) / (lu * lv) : 1;
            cos = lu > 0 && lv > 0 ? Math.Abs(ux * vx + uz * vz) / (lu * lv) : 0;
            if (sin < 0.25) sin = 0.25;
        }

        /// <summary>Full width of a road's corridor (carriageway, footpaths and shoulders, at least
        /// <see cref="RoadClearance.MinCorridorM"/>; a foot way its own width and half a metre each side) at
        /// <paramref name="alongM"/> (from the road's first point).</summary>
        internal static double CorridorWidth(RoadLayout roads, RoadRecord o, int j, double alongM)
        {
            // A foot way (often a sidewalk mapped beside the road) keeps its own width and half a metre each side, not
            // a vehicle corridor.
            if (BridgeStyle.IsFoot(o.RoadClass)) return Math.Max(RoadStyle.WidthM(o) + 1.0, 2.5);
            if (roads == null) return Math.Max(RoadStyle.WidthM(o), RoadClearance.MinCorridorM);
            RoadProfile pr = roads.ProfileAt(j, alongM);
            return Math.Max(pr.CarriagewayM + pr.FootpathLeftM + pr.FootpathRightM + 2 * pr.ShoulderM, RoadClearance.MinCorridorM);
        }

        /// <summary>Width of the part of a road a stair may not stand on: the carriageway and shoulders of a road with
        /// traffic (a stair lands on its footpath, as overbridge stairs do), the drawn width of a foot way.</summary>
        internal static double CarriageWidth(RoadLayout roads, RoadRecord o, int j, double alongM)
        {
            if (BridgeStyle.IsFoot(o.RoadClass) || roads == null) return RoadStyle.WidthM(o);
            RoadProfile pr = roads.ProfileAt(j, alongM);
            return pr.CarriagewayM + 2 * pr.ShoulderM;
        }

        internal static void Intersect(double ax, double az, double bx, double bz, double cx, double cz, double dx, double dz, out double ta, out double tb)
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

        // -------------------------------------------------------------------------------------------------------------
        // Foot overbridges and their stairs
        // -------------------------------------------------------------------------------------------------------------

        private static void FootOverbridges(TileData t, IHeightSampler ground, RoadStructureRecord[] recs, bool[] bridge, bool[] water,
                                            List<Crossing>[] crossings)
        {
            int n = t.Roads.Count;
            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;
            // Foot decks over no water, joined at any shared point, form one structure.
            var at = new Dictionary<long, List<int>>();
            for (int i = 0; i < n; i++)
            {
                if (!IsFootDeck(t.Roads[i], bridge[i], water[i])) continue;
                ForRendered(t.Roads[i], (x, z) =>
                {
                    long key = Key(x, z);
                    List<int> l;
                    if (!at.TryGetValue(key, out l)) at[key] = l = new List<int>();
                    foreach (int j in l) Union(parent, i, j);
                    if (!l.Contains(i)) l.Add(i);
                });
            }
            var seeded = new bool[n];
            for (int i = 0; i < n; i++)
            {
                if (!IsFootDeck(t.Roads[i], bridge[i], water[i])) continue;
                foreach (Crossing c in crossings[i])
                    if (c.LowMotor) seeded[Find(parent, i)] = true;
            }
            var height = new float[n];
            var cutHeight = new float[n];
            for (int i = 0; i < n; i++)
            {
                height[i] = float.NegativeInfinity;
                cutHeight[i] = float.NegativeInfinity;
            }
            for (int i = 0; i < n; i++)
            {
                if (!IsFootDeck(t.Roads[i], bridge[i], water[i])) continue;
                int g = Find(parent, i);
                if (!seeded[g]) continue;
                RoadRecord r = t.Roads[i];
                float[] d = recs[i].DeckY;
                int count = r.PointCount;
                int first = r.HasPrevContext ? 1 : 0, last = r.HasNextContext ? count - 2 : count - 1;
                for (int k = first; k <= last; k++) height[g] = Math.Max(height[g], d[k]);
                foreach (Crossing c in crossings[i])
                    height[g] = Math.Max(height[g], c.LowY + c.Clear + BridgeStyle.FootCrossingDepthM);
                float lift = RoadMesher.LiftOf(r, DefaultRoadOptions);
                float above = RoadClearance.MinUnderpassClearanceM + BridgeStyle.FootCrossingDepthM + CutMarginM + lift;
                if (r.HasPrevContext)
                    cutHeight[g] = Math.Max(cutHeight[g], Ground(t, ground, r.Points[2] / 100.0, r.Points[3] / 100.0) + above);
                if (r.HasNextContext)
                    cutHeight[g] = Math.Max(cutHeight[g], Ground(t, ground, r.Points[2 * last] / 100.0, r.Points[2 * last + 1] / 100.0) + above);
            }
            var deckEnds = new List<(long key, float y)>();
            for (int i = 0; i < n; i++)
            {
                if (!IsFootDeck(t.Roads[i], bridge[i], water[i])) continue;
                int g = Find(parent, i);
                if (!seeded[g]) continue;
                // A structure cut by a border takes the border's height, which the neighbour computes alike.
                float h = float.IsNegativeInfinity(cutHeight[g]) ? height[g] : cutHeight[g];
                RoadStructureRecord rec = recs[i];
                rec.Kind = RoadStructureKind.Flyover;
                rec.Flags = RoadStructureFlags.FootOverbridge;
                rec.RailingHeightM = 1.2f;
                var y = new float[t.Roads[i].PointCount];
                for (int k = 0; k < y.Length; k++) y[k] = h;
                rec.DeckY = y;
                recs[i] = rec;
                RoadRecord r = t.Roads[i];
                ForRendered(r, (x, z) => deckEnds.Add((Key(x, z), h)));
            }
            // Steps chains from the deck down to the ground.
            var taken = new bool[n];
            foreach (var (key, h) in deckEnds)
            {
                for (int j = 0; j < n; j++)
                {
                    RoadRecord s = t.Roads[j];
                    if (taken[j] || s.RoadClass != RoadClass.Steps || recs[j].DeckY != null || !RoadMesher.IsDrawn(s, null)) continue;
                    if (EndKeyOf(s, true) != key && EndKeyOf(s, false) != key) continue;
                    StairChain(t, ground, recs, taken, j, key, h);
                }
            }
        }

        private static bool IsFootDeck(RoadRecord r, bool bridge, bool water)
        {
            return bridge && !water && BridgeStyle.IsFoot(r.RoadClass) && r.RoadClass != RoadClass.Steps;
        }

        /// <summary>Give the steps chain that starts with road <paramref name="j0"/> at point <paramref name="from"/>
        /// stair records: from the deck height at the top down to the lifted ground at the chain's far end, linear in
        /// the chain's length (the layout builds the flights from these heights).</summary>
        private static void StairChain(TileData t, IHeightSampler ground, RoadStructureRecord[] recs, bool[] taken, int j0, long from, float top)
        {
            var chain = new List<(int road, bool forward, double start)>();
            long cur = from;
            double len = 0;
            int j = j0;
            while (j >= 0 && len < MaxStairChainM)
            {
                RoadRecord s = t.Roads[j];
                bool forward = EndKeyOf(s, true) == cur;
                chain.Add((j, forward, len));
                taken[j] = true;
                len += RenderedLength(s);
                bool cut = forward ? s.HasNextContext : s.HasPrevContext;
                if (cut) break;
                cur = EndKeyOf(s, !forward);
                int next = -1;
                for (int k = 0; k < t.Roads.Count && next < 0; k++)
                {
                    RoadRecord o = t.Roads[k];
                    if (taken[k] || o.RoadClass != RoadClass.Steps || recs[k].DeckY != null || !RoadMesher.IsDrawn(o, null)) continue;
                    if (EndKeyOf(o, true) == cur || EndKeyOf(o, false) == cur) next = k;
                }
                j = next;
            }
            if (len < 1e-3) return;
            var (lastRoad, lastForward, _) = chain[chain.Count - 1];
            RoadRecord lr = t.Roads[lastRoad];
            int lc = lr.PointCount;
            int lfirst = lr.HasPrevContext ? 1 : 0, llast = lr.HasNextContext ? lc - 2 : lc - 1;
            int endPt = lastForward ? llast : lfirst;
            float bottom = Ground(t, ground, lr.Points[2 * endPt] / 100.0, lr.Points[2 * endPt + 1] / 100.0) + RoadMesher.LiftOf(lr, DefaultRoadOptions);
            bottom = Math.Min(bottom, top);
            // A chain mapped shorter than a walkable stair needs keeps a walkable grade: its far end stays above the
            // ground and the layout runs the last flight on down to it.
            double effective = Math.Max(len, (top - bottom) / MaxStairGrade);
            foreach (var (road, forward, start) in chain)
            {
                RoadRecord s = t.Roads[road];
                int[] p = s.Points;
                int count = p.Length / 2;
                int first = s.HasPrevContext ? 1 : 0, last = s.HasNextContext ? count - 2 : count - 1;
                double[] al = Along(p, first, last);
                double total = al[last];
                var y = new float[count];
                for (int k = 0; k < count; k++)
                {
                    double a = start + (forward ? al[k] : total - al[k]);
                    double f = a / effective;
                    f = f < 0 ? 0 : f > 1 ? 1 : f;
                    y[k] = (float)(top - (top - bottom) * f);
                }
                RoadStructureRecord rec = recs[road];
                rec.Kind = RoadStructureKind.Flyover;
                rec.Flags = RoadStructureFlags.FootOverbridge;
                rec.Layer = s.Layer;
                rec.RailingHeightM = 1.0f;
                rec.DeckY = y;
                recs[road] = rec;
            }
        }

        private static double RenderedLength(RoadRecord r)
        {
            int count = r.PointCount;
            int first = r.HasPrevContext ? 1 : 0, last = r.HasNextContext ? count - 2 : count - 1;
            double len = 0;
            for (int k = first; k < last; k++) len += SegLen(r.Points, k);
            return len;
        }

        /// <summary>Key of the first (or last) rendered point of a road.</summary>
        private static long EndKeyOf(RoadRecord r, bool start)
        {
            int count = r.PointCount;
            int k = start ? (r.HasPrevContext ? 1 : 0) : (r.HasNextContext ? count - 2 : count - 1);
            return Key(r.Points[2 * k], r.Points[2 * k + 1]);
        }

        private static void ForRendered(RoadRecord r, Action<int, int> f)
        {
            int count = r.PointCount;
            int first = r.HasPrevContext ? 1 : 0, last = r.HasNextContext ? count - 2 : count - 1;
            for (int k = first; k <= last; k++) f(r.Points[2 * k], r.Points[2 * k + 1]);
        }

        /// <summary>A plan point in centimetres as one key.</summary>
        internal static long Key(int xCm, int zCm)
        {
            return ((long)xCm << 32) ^ (uint)zCm;
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

        // -------------------------------------------------------------------------------------------------------------
        // Geometry helpers
        // -------------------------------------------------------------------------------------------------------------

        private static void Direction(int[] p, int count, int k, out double tx, out double tz)
        {
            int a = Math.Max(0, k - 1), b = Math.Min(count - 1, k + 1);
            double dx = (p[2 * b] - p[2 * a]) / 100.0, dz = (p[2 * b + 1] - p[2 * a + 1]) / 100.0;
            double l = Math.Sqrt(dx * dx + dz * dz);
            if (l < 1e-9)
            {
                tx = 1;
                tz = 0;
                return;
            }
            tx = dx / l;
            tz = dz / l;
        }

        internal static double SegLen(int[] p, int i)
        {
            double dx = (p[2 * i + 2] - p[2 * i]) / 100.0, dz = (p[2 * i + 3] - p[2 * i + 1]) / 100.0;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Terrain height at tile-local metres, clamped onto the tile (0 without heights).</summary>
        internal static float Ground(TileData t, IHeightSampler ground, double lx, double lz)
        {
            float h;
            double size = t.Tile.Size;
            double cx = lx < 0 ? 0 : lx > size ? size : lx, cz = lz < 0 ? 0 : lz > size ? size : lz;
            if (ground.TryHeight(t.Tile.X0 + lx, t.Tile.Z0 + lz, out h)) return h;
            if (ground.TryHeight(t.Tile.X0 + cx, t.Tile.Z0 + cz, out h)) return h;
            return 0f;
        }

        /// <summary>True when a rendered segment of <paramref name="r"/> crosses a river, stream, canal or ditch line,
        /// or a rendered point or segment midpoint lies in a water area.</summary>
        public static bool CrossesWater(TileData t, RoadRecord r)
        {
            int[] p = r.Points;
            int count = p.Length / 2;
            int first = r.HasPrevContext ? 1 : 0, last = r.HasNextContext ? count - 2 : count - 1;
            foreach (LineRecord l in t.Lines)
            {
                if (l.Kind != LineKind.River && l.Kind != LineKind.Stream && l.Kind != LineKind.Canal && l.Kind != LineKind.Ditch) continue;
                int[] q = l.Points;
                for (int a = first; a < last; a++)
                for (int b = 0; b + 1 < q.Length / 2; b++)
                    if (SegmentsCross(p[2 * a], p[2 * a + 1], p[2 * a + 2], p[2 * a + 3], q[2 * b], q[2 * b + 1], q[2 * b + 2], q[2 * b + 3]))
                        return true;
            }
            foreach (AreaRecord ar in t.Areas)
            {
                if (ar.Kind != AreaKind.WaterRiver && ar.Kind != AreaKind.WaterLake && ar.Kind != AreaKind.WaterPond) continue;
                for (int a = first; a <= last; a++)
                {
                    if (InArea(ar, p[2 * a], p[2 * a + 1])) return true;
                    if (a < last && InArea(ar, (p[2 * a] + p[2 * a + 2]) / 2, (p[2 * a + 1] + p[2 * a + 3]) / 2)) return true;
                }
            }
            return false;
        }

        /// <summary>True when road <paramref name="ri"/> crosses (in plan) a drawn road that is lower: not a bridge, or
        /// a bridge on a lower layer.</summary>
        public static bool CrossesLowerRoad(TileData t, int ri)
        {
            RoadRecord r = t.Roads[ri];
            int[] p = r.Points;
            int count = p.Length / 2;
            int first = r.HasPrevContext ? 1 : 0, last = r.HasNextContext ? count - 2 : count - 1;
            for (int j = 0; j < t.Roads.Count; j++)
            {
                if (j == ri) continue;
                RoadRecord o = t.Roads[j];
                if (!IsLower(o, r)) continue;
                int[] q = o.Points;
                int oc = q.Length / 2;
                for (int a = first; a < last; a++)
                for (int b = 0; b + 1 < oc; b++)
                    if (Crosses(p[2 * a], p[2 * a + 1], p[2 * a + 2], p[2 * a + 3], a == first, a + 1 == last, q[2 * b], q[2 * b + 1], q[2 * b + 2],
                                q[2 * b + 3]))
                        return true;
            }
            return false;
        }

        /// <summary>True when <paramref name="o"/> is a drawn road that passes under the bridge <paramref name="r"/>.</summary>
        internal static bool IsLower(RoadRecord o, RoadRecord r)
        {
            if (!RoadMesher.IsDrawn(o, null)) return false;
            if ((o.Flags & RoadFlags.Bridge) == 0) return true;
            return o.Layer < r.Layer;
        }

        /// <summary>Segment a-b of a bridge (its end points a and b flagged when they are the bridge's own first or last
        /// rendered point) crosses segment c-d of another road: a proper crossing, or an interior bridge vertex lying
        /// on c-d (a touch at a bridge end is the junction with its approach and does not count). A touch at b is left
        /// to the next segment, so a vertex is counted once.</summary>
        internal static bool Crosses(long ax, long az, long bx, long bz, bool aIsEnd, bool bIsEnd, long cx, long cz, long dx, long dz)
        {
            if (SegmentsCross(ax, az, bx, bz, cx, cz, dx, dz)) return true;
            if (aIsEnd) return false;
            // a on c-d (collinear and within the segment's box), with b off the line so the bridge passes over.
            if (Orient(cx, cz, dx, dz, ax, az) != 0) return false;
            if (ax < Math.Min(cx, dx) || ax > Math.Max(cx, dx) || az < Math.Min(cz, dz) || az > Math.Max(cz, dz)) return false;
            if (ax == cx && az == cz || ax == dx && az == dz) return false; // a shared node: an at-grade junction
            return Orient(cx, cz, dx, dz, bx, bz) != 0;
        }

        /// <summary>Proper crossing of segments a-b and c-d (shared end points do not count).</summary>
        internal static bool SegmentsCross(long ax, long az, long bx, long bz, long cx, long cz, long dx, long dz)
        {
            long d1 = Orient(cx, cz, dx, dz, ax, az), d2 = Orient(cx, cz, dx, dz, bx, bz);
            long d3 = Orient(ax, az, bx, bz, cx, cz), d4 = Orient(ax, az, bx, bz, dx, dz);
            return d1 != 0 && d2 != 0 && d3 != 0 && d4 != 0 && (d1 > 0) != (d2 > 0) && (d3 > 0) != (d4 > 0);
        }

        private static long Orient(long ax, long az, long bx, long bz, long cx, long cz)
        {
            return (bx - ax) * (cz - az) - (bz - az) * (cx - ax);
        }

        private static bool InArea(AreaRecord a, long x, long z)
        {
            int[] v = a.Vertices, idx = a.Indices;
            for (int k = 0; k + 2 < idx.Length; k += 3)
            {
                int i0 = idx[k], i1 = idx[k + 1], i2 = idx[k + 2];
                long ax = v[2 * i0], az = v[2 * i0 + 1], bx = v[2 * i1], bz = v[2 * i1 + 1], cx = v[2 * i2], cz = v[2 * i2 + 1];
                if (x < Math.Min(ax, Math.Min(bx, cx)) || x > Math.Max(ax, Math.Max(bx, cx)) || z < Math.Min(az, Math.Min(bz, cz)) ||
                    z > Math.Max(az, Math.Max(bz, cz))) continue;
                long d1 = Orient(ax, az, bx, bz, x, z), d2 = Orient(bx, bz, cx, cz, x, z), d3 = Orient(cx, cz, ax, az, x, z);
                bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                if (!(neg && pos)) return true;
            }
            return false;
        }
    }
}
