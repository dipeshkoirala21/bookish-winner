using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// The levelled platform under a junction cap or a roundabout (owner playtest, Gaurighat: "make them flat like how they
    /// make them, cutting the hills"): a plane through the natural terrain around the junction, its gradient held to
    /// <see cref="RoadPads.MaxGrade"/>, over the junction's outline (one polygon for a cap; the ring's circle and its entry
    /// patches for a roundabout). Heights are the terrain under the road surface (no lift). Immutable.
    /// </summary>
    public sealed class RoadPad
    {
        /// <summary>Plane origin (tile-local metres).</summary>
        public readonly double CX, CZ;

        /// <summary>Plane height at the origin and its gradient (dh/dx, dh/dz).</summary>
        public readonly float H0, Gx, Gz;

        /// <summary>The outline polygons (tile-local, closed, either orientation); the pad is their union.</summary>
        public readonly double[][] PolyX, PolyZ;

        /// <summary>Bounds of the outline.</summary>
        public readonly double MinX, MinZ, MaxX, MaxZ;

        /// <summary>The cap's or ring's top road (its lift is the junction's).</summary>
        public readonly int TopRoad;

        /// <summary>A roundabout (else a junction cap).</summary>
        public readonly bool Ring;

        /// <summary>The junction sits on a hillside (<see cref="RoadPads.IsHillside"/>): the terrain around it is cut and
        /// filled to its plane with slopes and walls like a hill road's. Every pad, hillside or gentle street corner, is the
        /// surface under its cap or ring (<see cref="TerrainCarve.ExtentAt"/>) and the plane its arms' end rows lie in.</summary>
        public readonly bool Hill;

        internal RoadPad(double cx, double cz, float h0, float gx, float gz, double[][] px, double[][] pz, int top, bool ring, bool hill)
        {
            Hill = hill;
            CX = cx;
            CZ = cz;
            H0 = h0;
            Gx = gx;
            Gz = gz;
            PolyX = px;
            PolyZ = pz;
            TopRoad = top;
            Ring = ring;
            double x0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, z1 = double.MinValue;
            for (int p = 0; p < px.Length; p++)
            for (int k = 0; k < px[p].Length; k++)
            {
                x0 = Math.Min(x0, px[p][k]);
                x1 = Math.Max(x1, px[p][k]);
                z0 = Math.Min(z0, pz[p][k]);
                z1 = Math.Max(z1, pz[p][k]);
            }
            MinX = x0;
            MinZ = z0;
            MaxX = x1;
            MaxZ = z1;
        }

        /// <summary>Plane height (terrain under the junction surface) at tile-local (x, z).</summary>
        public float Height(double x, double z)
        {
            return (float)(H0 + Gx * (x - CX) + Gz * (z - CZ));
        }

        /// <summary>Distance from tile-local (x, z) to the outline: 0 inside, else to the nearest edge.</summary>
        public double Distance(double x, double z)
        {
            double best = double.PositiveInfinity;
            for (int p = 0; p < PolyX.Length; p++)
            {
                double[] px = PolyX[p], pz = PolyZ[p];
                int n = px.Length;
                if (n < 3) continue;
                bool inside = false;
                double d2 = double.PositiveInfinity;
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    double ax = px[j], az = pz[j], bx = px[i], bz = pz[i];
                    if ((az > z) != (bz > z) && x < (bx - ax) * (z - az) / (bz - az) + ax) inside = !inside;
                    double ex = bx - ax, ez = bz - az, l2 = ex * ex + ez * ez;
                    double t = l2 > 1e-12 ? ((x - ax) * ex + (z - az) * ez) / l2 : 0;
                    t = t < 0 ? 0 : t > 1 ? 1 : t;
                    double dx = ax + ex * t - x, dz = az + ez * t - z, dd = dx * dx + dz * dz;
                    if (dd < d2) d2 = dd;
                }
                if (inside) return 0;
                if (d2 < best) best = d2;
            }
            return Math.Sqrt(best);
        }
    }

    /// <summary>
    /// Every junction platform of a tile (<see cref="RoadPad"/>): one per junction cap and one per roundabout of the
    /// <see cref="RoadLayout"/>, and which pad owns each ribbon cut (the arms of a cap or ring end on its pad, their end rows
    /// lying in its plane). Built from the natural (uncarved) terrain; deterministic; immutable afterwards.
    /// </summary>
    public sealed class RoadPads
    {
        /// <summary>Steepest gradient of a junction platform (the carriageway cross-fall limit: a junction on a hillside is
        /// levelled to this, cutting and filling the slope around it).</summary>
        public const float MaxGrade = 0.06f;

        public readonly List<RoadPad> Pads = new List<RoadPad>();
        private readonly Dictionary<long, int> _cutPad = new Dictionary<long, int>();

        private static long CutKey(int road, double s)
        {
            return (long)road << 32 ^ (uint)(long)Math.Round(s * 1000.0);
        }

        /// <summary>Index into <see cref="Pads"/> of the pad whose outline road <paramref name="road"/>'s cut at raw along
        /// <paramref name="s"/> ends on, or -1.</summary>
        public int PadOfCut(int road, double s)
        {
            int p;
            return _cutPad.TryGetValue(CutKey(road, s), out p) ? p : -1;
        }

        /// <summary>
        /// A junction sits on a hillside (rather than a gentle street corner, or the bumps of the city's surface model) when
        /// the natural ground departs from its levelled plane by at least <see cref="TerrainCarve.CarveStartM"/> and the
        /// ground itself falls at least 8 % across it.
        /// </summary>
        public static bool IsHillside(double deviationM, double naturalGrade)
        {
            return deviationM >= TerrainCarve.CarveStartM && naturalGrade >= 0.08;
        }

        /// <summary>The pads of a layout over the natural terrain <paramref name="natural"/> (tile-local metres in, height
        /// out).</summary>
        public static RoadPads Build(RoadLayout lay, Func<double, double, float> natural)
        {
            if (lay == null) throw new ArgumentNullException(nameof(lay));
            if (natural == null) throw new ArgumentNullException(nameof(natural));
            var pads = new RoadPads();
            var xs = new List<double>();
            var zs = new List<double>();
            foreach (JunctionCap cap in lay.Caps)
            {
                if (cap.Count < 3) continue;
                var px = new double[cap.Count];
                var pz = new double[cap.Count];
                Array.Copy(cap.PolyX, px, cap.Count);
                Array.Copy(cap.PolyZ, pz, cap.Count);
                int index = pads.Pads.Count;
                pads.Pads.Add(Fit(cap.X, cap.Z, new[] { px }, new[] { pz }, cap.TopRoad, false, natural));
                for (int k = 0; k < cap.ArmRoads.Length; k++) pads._cutPad[CutKey(cap.ArmRoads[k], cap.ArmCutS[k])] = index;
            }
            pads.MergeSteepNeighbours(lay, natural);
            foreach (RoadRing ring in lay.Rings)
            {
                var polysX = new List<double[]>();
                var polysZ = new List<double[]>();
                int segs = ring.CircleSegments;
                var cx = new double[segs];
                var cz = new double[segs];
                for (int k = 0; k < segs; k++)
                {
                    double a = 2 * Math.PI * k / segs;
                    cx[k] = ring.X + ring.OuterRadiusM * Math.Cos(a);
                    cz[k] = ring.Z + ring.OuterRadiusM * Math.Sin(a);
                }
                polysX.Add(cx);
                polysZ.Add(cz);
                for (int a = 0; a < ring.Arms.Length; a++)
                {
                    xs.Clear();
                    zs.Clear();
                    ring.Patch(a, xs, zs);
                    if (xs.Count < 3) continue;
                    polysX.Add(xs.ToArray());
                    polysZ.Add(zs.ToArray());
                }
                int index = pads.Pads.Count;
                pads.Pads.Add(Fit(ring.X, ring.Z, polysX.ToArray(), polysZ.ToArray(), ring.TopRoad, true, natural));
                foreach (RingArm arm in ring.Arms) pads._cutPad[CutKey(arm.Road, arm.Cut.S)] = index;
            }
            return pads;
        }

        /// <summary>Steepest grade of the stretch of road between two neighbouring junction platforms (a steep street between
        /// two junctions; the platforms themselves stay within <see cref="MaxGrade"/>).</summary>
        public const float MaxArmGrade = 0.15f;

        /// <summary>
        /// Junction caps joined by a stretch of road too short to climb from one platform to the other within
        /// <see cref="MaxArmGrade"/> (two junctions a few metres apart on a hillside, each levelled on its own) share one
        /// platform: the plane through the natural ground of all their outlines. Before this the stub between them rose
        /// up to 1.5 m in half a metre, a cliff in the road (cut/fill package, Kalimati and Swayambhu lanes).
        /// </summary>
        private void MergeSteepNeighbours(RoadLayout lay, Func<double, double, float> natural)
        {
            int n = Pads.Count;
            if (n < 2) return;
            var parent = new int[n];
            for (int k = 0; k < n; k++) parent[k] = k;
            bool any = false;
            for (int i = 0; i < lay.Cuts.Length; i++)
            {
                RoadCut[] cuts = lay.Cuts[i];
                if (cuts == null) continue;
                // Drawn stretches run from the end of one gap (cuts[2m + 1]) to the start of the next (cuts[2m + 2]).
                for (int k = 1; k + 1 < cuts.Length; k += 2)
                {
                    RoadCut a = cuts[k], b = cuts[k + 1];
                    int pa = PadOfCut(i, a.S), pb = PadOfCut(i, b.S);
                    if (pa < 0 || pb < 0 || pa == pb) continue;
                    double len = Math.Max(0.0, b.S - a.S);
                    float ha = Pads[pa].Height(a.CX, a.CZ), hb = Pads[pb].Height(b.CX, b.CZ);
                    if (Math.Abs(ha - hb) <= MaxArmGrade * len + 0.05) continue;
                    int ra = Find(parent, pa), rb = Find(parent, pb);
                    if (ra == rb) continue;
                    parent[Math.Max(ra, rb)] = Math.Min(ra, rb);
                    any = true;
                }
            }
            if (!any) return;
            var groups = new Dictionary<int, List<int>>();
            for (int k = 0; k < n; k++)
            {
                int r = Find(parent, k);
                List<int> l;
                if (!groups.TryGetValue(r, out l)) groups[r] = l = new List<int>(2);
                l.Add(k);
            }
            foreach (KeyValuePair<int, List<int>> g in groups)
            {
                List<int> members = g.Value;
                if (members.Count < 2) continue;
                var px = new List<double[]>();
                var pz = new List<double[]>();
                double cx = 0, cz = 0;
                int top = -1, topPrio = int.MinValue;
                foreach (int m in members)
                {
                    RoadPad p = Pads[m];
                    px.AddRange(p.PolyX);
                    pz.AddRange(p.PolyZ);
                    cx += p.CX;
                    cz += p.CZ;
                    int prio = p.TopRoad >= 0 && p.TopRoad < lay.Tile.Roads.Count ? RoadStyle.Priority(lay.Tile.Roads[p.TopRoad].RoadClass) : int.MinValue;
                    if (prio > topPrio)
                    {
                        topPrio = prio;
                        top = p.TopRoad;
                    }
                }
                cx /= members.Count;
                cz /= members.Count;
                RoadPad merged = Fit(cx, cz, px.ToArray(), pz.ToArray(), top, false, natural);
                // Every member answers with the shared platform (indices, and so every cut's pad, stay as they are).
                foreach (int m in members) Pads[m] = merged;
            }
        }

        private static int Find(int[] parent, int k)
        {
            while (parent[k] != k)
            {
                parent[k] = parent[parent[k]];
                k = parent[k];
            }
            return k;
        }

        /// <summary>The least-squares plane through the natural terrain at the outline's points and centre, its gradient
        /// clamped to <see cref="MaxGrade"/>, then shifted so it balances cut and fill over those points.</summary>
        private static RoadPad Fit(double cx, double cz, double[][] px, double[][] pz, int top, bool ring, Func<double, double, float> natural)
        {
            // Normal equations of h = a + b dx + c dz.
            double s1 = 0, sx = 0, sz = 0, sxx = 0, sxz = 0, szz = 0, sh = 0, sxh = 0, szh = 0;
            int n = 0;
            for (int p = -1; p < px.Length; p++)
            {
                int count = p < 0 ? 1 : px[p].Length;
                for (int k = 0; k < count; k++)
                {
                    double x = p < 0 ? cx : px[p][k], z = p < 0 ? cz : pz[p][k];
                    double h = natural(x, z), dx = x - cx, dz = z - cz;
                    s1 += 1;
                    sx += dx;
                    sz += dz;
                    sxx += dx * dx;
                    sxz += dx * dz;
                    szz += dz * dz;
                    sh += h;
                    sxh += dx * h;
                    szh += dz * h;
                    n++;
                }
            }
            double gx = 0, gz = 0;
            // Solve the 3x3 system by Cramer's rule (well conditioned: points spread around the centre).
            double det = s1 * (sxx * szz - sxz * sxz) - sx * (sx * szz - sxz * sz) + sz * (sx * sxz - sxx * sz);
            if (Math.Abs(det) > 1e-9)
            {
                gx = (s1 * (sxh * szz - sxz * szh) - sh * (sx * szz - sxz * sz) + sz * (sx * szh - sxh * sz)) / det;
                gz = (s1 * (sxx * szh - sxh * sxz) - sx * (sx * szh - sxh * sz) + sh * (sx * sxz - sxx * sz)) / det;
            }
            if (double.IsNaN(gx) || double.IsNaN(gz))
            {
                gx = 0;
                gz = 0;
            }
            double g = Math.Sqrt(gx * gx + gz * gz);
            double g0x = gx, g0z = gz; // before the clamp: the hillside's own gradient
            if (g > MaxGrade)
            {
                gx *= MaxGrade / g;
                gz *= MaxGrade / g;
            }
            double h0 = n > 0 ? (sh - gx * sx - gz * sz) / s1 : natural(cx, cz);
            // How far the natural ground departs from the levelled plane over the outline and the centre.
            double dev = 0;
            for (int p = -1; p < px.Length; p++)
            {
                int count = p < 0 ? 1 : px[p].Length;
                for (int k = 0; k < count; k++)
                {
                    double x = p < 0 ? cx : px[p][k], z = p < 0 ? cz : pz[p][k];
                    dev = Math.Max(dev, Math.Abs(natural(x, z) - (h0 + gx * (x - cx) + gz * (z - cz))));
                    if (p < 0) continue;
                    double mx = 0.5 * (x + cx), mz = 0.5 * (z + cz);
                    dev = Math.Max(dev, Math.Abs(natural(mx, mz) - (h0 + gx * (mx - cx) + gz * (mz - cz))));
                }
            }
            // The hillside's fall over the outline and HillSpanM beyond (the surface model's building bumps average out).
            double reach = 0;
            for (int p = 0; p < px.Length; p++)
                for (int k = 0; k < px[p].Length; k++)
                    reach = Math.Max(reach, Math.Sqrt((px[p][k] - cx) * (px[p][k] - cx) + (pz[p][k] - cz) * (pz[p][k] - cz)));
            double span = reach + TerrainCarve.HillSpanM;
            double fx = (natural(cx + span, cz) - natural(cx - span, cz)) / (2 * span), fz = (natural(cx, cz + span) - natural(cx, cz - span)) / (2 * span);
            bool hill = IsHillside(dev, Math.Min(Math.Sqrt(g0x * g0x + g0z * g0z), Math.Sqrt(fx * fx + fz * fz)));
            return new RoadPad(cx, cz, (float)h0, (float)gx, (float)gz, px, pz, top, ring, hill);
        }
    }
}
