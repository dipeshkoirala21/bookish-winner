using System;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.Core.Meshing.Bridges
{
    /// <summary>
    /// The road structure records of a tile (docs/W2_DETAIL_CONTRACT.md §3): the tile's own (written by the data
    /// pipeline) when it carries one per road, else records derived here from the W1 tags so bridges are built on
    /// packs without the structure chunk. The derivation mirrors the W1 road mesher, so the ribbon and the structure
    /// agree until the data package lands: a bridge deck runs straight between its lifted ends (terrain at the first
    /// and last rendered point plus the ribbon lift, which is the same in both tiles at a cut end), never below the
    /// lifted terrain under it; a bridge that crosses a lower drawn road is a flyover (a foot overbridge for foot
    /// classes) whose interior points are raised toward the underpass clearance as far as a 10% grade from the ends
    /// allows; a bridge over a waterway line or water area is flagged <see cref="RoadStructureFlags.WaterCrossing"/>.
    /// Deterministic; allocates only the result arrays.
    /// </summary>
    public static class BridgeStructures
    {
        /// <summary>Steepest grade the derivation uses to lift a flyover's interior points.</summary>
        public const float DerivedMaxGrade = 0.10f;

        private static readonly RoadOptions DefaultRoadOptions = new RoadOptions();

        /// <summary>True when the tile carries one structure record per road (the data package's chunk).</summary>
        public static bool HasRecords(TileData t)
        {
            return t.RoadStructures.Count > 0 && t.RoadStructures.Count == t.Roads.Count;
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

        /// <summary>Structure records derived from <see cref="RoadFlags.Bridge"/>, the layer and the terrain (see the
        /// class summary). Roads that are not bridges get <see cref="RoadStructureRecord.Absent"/> (car access by
        /// class).</summary>
        public static RoadStructureRecord[] Derive(TileData t, IHeightSampler ground)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (ground == null) throw new ArgumentNullException(nameof(ground));
            int n = t.Roads.Count;
            var recs = new RoadStructureRecord[n];
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            RoadLayout layout = n > 0 ? RoadLayout.For(t) : null;
            for (int i = 0; i < n; i++)
            {
                RoadRecord r = t.Roads[i];
                var rec = RoadStructureRecord.Absent;
                rec.Layer = r.Layer;
                if (BridgeStyle.IsFoot(r.RoadClass) || r.RoadClass == RoadClass.Steps) rec.Flags = RoadStructureFlags.None;
                if ((r.Flags & RoadFlags.Tunnel) != 0)
                {
                    rec.Kind = RoadStructureKind.Tunnel;
                    rec.Flags &= ~RoadStructureFlags.CarAccessible;
                    recs[i] = rec;
                    continue;
                }
                if ((r.Flags & RoadFlags.Ford) != 0) rec.Kind = RoadStructureKind.Ford;
                if ((r.Flags & RoadFlags.Bridge) == 0 || !RoadMesher.IsDrawn(r, null))
                {
                    recs[i] = rec;
                    continue;
                }
                bool water = CrossesWater(t, r);
                bool overRoad = CrossesLowerRoad(t, i);
                bool foot = BridgeStyle.IsFoot(r.RoadClass);
                rec.Kind = overRoad && !water ? RoadStructureKind.Flyover : RoadStructureKind.Bridge;
                if (water) rec.Flags |= RoadStructureFlags.WaterCrossing;
                if (foot && overRoad && !water) rec.Flags |= RoadStructureFlags.FootOverbridge;
                rec.RailingHeightM = foot ? 1.2f : BridgeStyle.RailHeightM;
                rec.DeckY = DeckProfile(t, i, ground, layout, x0, z0, overRoad);
                recs[i] = rec;
            }
            return recs;
        }

        /// <summary>The W1 straight deck between the lifted ends (plus the flyover raise), one height per point.</summary>
        private static float[] DeckProfile(TileData t, int ri, IHeightSampler ground, RoadLayout layout, double x0, double z0, bool overRoad)
        {
            RoadRecord r = t.Roads[ri];
            int[] p = r.Points;
            int count = p.Length / 2;
            int first = r.HasPrevContext ? 1 : 0, last = r.HasNextContext ? count - 2 : count - 1;
            float lift = RoadMesher.LiftOf(r, DefaultRoadOptions);
            var s = new double[count];
            for (int k = first + 1; k <= last; k++) s[k] = s[k - 1] + SegLen(p, k - 1);
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
            if (overRoad && last - first >= 2)
            {
                // Raise the interior points toward the clearance over the roads below, within the grade limit.
                float need = (float)(RoadClearance.MinUnderpassClearanceM + (BridgeStyle.IsFoot(r.RoadClass) ? BridgeStyle.FootDepthM : BridgeStyle.FlyoverDepthM));
                for (int k = first + 1; k < last; k++)
                {
                    double cx = p[2 * k] / 100.0, cz = p[2 * k + 1] / 100.0;
                    float want = Ground(t, ground, cx, cz) + lift + need;
                    float cap = (float)Math.Min(hStart + DerivedMaxGrade * s[k], hEnd + DerivedMaxGrade * (total - s[k]));
                    float v = Math.Min(want, cap);
                    if (v > y[k]) y[k] = v;
                }
            }
            if (first > 0) y[0] = y[first];
            if (last < count - 1) y[count - 1] = y[last];
            return y;
        }

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

        private static double SegLen(int[] p, int i)
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
                int of = o.HasPrevContext ? 1 : 0, ol = o.HasNextContext ? oc - 2 : oc - 1;
                for (int a = first; a < last; a++)
                for (int b = of; b < ol; b++)
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
