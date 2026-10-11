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

        internal RoadPad(double cx, double cz, float h0, float gx, float gz, double[][] px, double[][] pz, int top, bool ring)
        {
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
            if (g > MaxGrade)
            {
                gx *= MaxGrade / g;
                gz *= MaxGrade / g;
            }
            double h0 = n > 0 ? (sh - gx * sx - gz * sz) / s1 : natural(cx, cz);
            return new RoadPad(cx, cz, (float)h0, (float)gx, (float)gz, px, pz, top, ring);
        }
    }
}
