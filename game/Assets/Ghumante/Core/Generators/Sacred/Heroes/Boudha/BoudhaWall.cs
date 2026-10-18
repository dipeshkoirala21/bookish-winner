using System;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.Boudha
{
    /// <summary>
    /// S2, the compound (kora) wall round the terraces (OSM w56688296, 18 sides, r 45-55 m, 312 m; boudha.md 3.2): a
    /// free-standing white lime wall 2.6 m to its rounded coping and 0.7 m thick on the OSM line, the 147 prayer-wheel
    /// niches in its outer face (each a dark recess 1.6 × 0.55 m on a 0.9 m sill with four copper wheels and a yellow
    /// striped valance), a small white chorten finial over every pier, white buttress chortens with sooty mouths, the
    /// darchor flagpoles with their vertical five-colour banners, and the south gate pavilion with its timber door, gilt
    /// torana and the juniper kiln in front. The wall is open only at the north entrance (<see cref="BoudhaNorth"/>) and
    /// the south pavilion. Each quarter of the kora builds its own stretch (<see cref="BoudhaQuarter"/>).
    /// </summary>
    internal static class BoudhaWall
    {
        /// <summary>The compound wall in true (east, north) metres from the T1 centroid (OSM w56688296, boudha.md 2.2).</summary>
        private static readonly double[] TrueEN =
        {
            -47.3, 6.1, -40.8, 24.8, -28.6, 37.8, -9.8, 47.4, 0.7, 49.0, 10.5, 47.2, 22.3, 42.9, 37.9, 27.6, 42.0, 16.3, 48.4, 6.7, 55.3, 0.8,
            53.6, -12.6, 46.4, -19.1, 27.5, -41.6, 7.2, -49.4, -24.0, -45.6, -40.3, -29.7, -47.5, -12.8,
        };

        public const double Height = 2.6, HalfThick = 0.35, PoleTop = 9.0, PoleTipH = 0.45;
        public const int NicheCount = 147;

        /// <summary>Niche opening (m): width, sill height, height (boudha.md 3.2).</summary>
        public const double NicheW = 1.3, NicheSill = 0.9, NicheH = 0.55;

        /// <summary>The north entrance gap (site u, either side of the axis) and the south pavilion gap.</summary>
        public const double NorthGapHalf = 7.6, SouthGapHalf = 2.1;

        [ThreadStatic] private static double[] _pu, _pv;
        [ThreadStatic] private static int _n;

        /// <summary>The OSM wall centre line in the site frame, counter-clockwise (shared scratch, do not modify).</summary>
        private static int OsmLine(out double[] pu, out double[] pv)
        {
            if (_pu == null)
            {
                _pu = new double[32];
                _pv = new double[32];
                _n = TrueEN.Length / 2;
                for (int i = 0; i < _n; i++) BoudhaFrame.FromTrue(TrueEN[2 * i], TrueEN[2 * i + 1], out _pu[i], out _pv[i]);
                BoudhaKit.Ccw(_pu, _pv, _n);
            }
            pu = _pu;
            pv = _pv;
            return _n;
        }

        /// <summary>The wall centre line of the frame (fitted to the drawn roads), counter-clockwise; do not modify.</summary>
        public static int Line(in BoudhaFrame f, out double[] pu, out double[] pv)
        {
            if (f.WallU != null)
            {
                pu = f.WallU;
                pv = f.WallV;
                return f.WallN;
            }
            return OsmLine(out pu, out pv);
        }

        /// <summary>Clearance of the wall's centre line from a drawn road's edge (m): half the wall and a hand's width, so
        /// the kora walk the road mesher draws runs beside the wall, not through it.</summary>
        public const double RoadClearM = HalfThick + 0.2;

        /// <summary>Longest wall segment of the fitted line (m).</summary>
        private const double FitSegM = 7.0;

        /// <summary>
        /// Fit the compound wall to the tile's drawn roads (decision 1, rideability beats exact footprints): the OSM line
        /// densified to <see cref="FitSegM"/> segments and every point pushed toward the stupa until the line keeps
        /// <see cref="RoadClearM"/> from the drawn kora ways (the OSM kora centre lines with their 4-8 m widths run over
        /// the wall line in the east and south; the wall moves back by up to about 3 m there). The result is stored in
        /// the frame.
        /// </summary>
        internal static void Fit(TileData tile, ref BoudhaFrame f)
        {
            double[] ou, ov;
            int on = OsmLine(out ou, out ov);
            double per = BoudhaKit.Perimeter(ou, ov, on);
            int cap = on + (int)Math.Ceiling(per / FitSegM) + 4;
            var u = new double[cap];
            var v = new double[cap];
            int n = 0;
            for (int i = 0; i < on; i++)
            {
                int j = (i + 1) % on;
                double du = ou[j] - ou[i], dv = ov[j] - ov[i], l = Math.Sqrt(du * du + dv * dv);
                int k = Math.Max(1, (int)Math.Ceiling(l / FitSegM));
                for (int q = 0; q < k && n < cap; q++)
                {
                    u[n] = ou[i] + du * q / k;
                    v[n] = ov[i] + dv * q / k;
                    n++;
                }
            }
            if (tile != null && tile.Roads.Count > 0)
            {
                SacredDrawnRoads roads = SacredDrawnRoads.For(tile);
                var push = new double[n];
                for (int it = 0; it < 24; it++)
                {
                    Array.Clear(push, 0, n);
                    bool any = false;
                    for (int i = 0; i < n; i++)
                    {
                        // Samples along the edge i → i + 1; each deficit pushes both ends by their share.
                        int j = (i + 1) % n;
                        double du = u[j] - u[i], dv = v[j] - v[i], l = Math.Sqrt(du * du + dv * dv);
                        int k = Math.Max(1, (int)Math.Ceiling(l / 0.5));
                        for (int q = 0; q <= k; q++)
                        {
                            double t = (double)q / k, x, z;
                            f.ToTile(u[i] + du * t, v[i] + dv * t, out x, out z);
                            double d = RoadClearM - roads.SignedDistance(x, z);
                            if (d <= 0) continue;
                            any = true;
                            push[i] = Math.Max(push[i], d * (1 - t));
                            push[j] = Math.Max(push[j], d * t);
                        }
                    }
                    if (!any) break;
                    for (int i = 0; i < n; i++)
                    {
                        if (push[i] <= 0) continue;
                        double r = Math.Sqrt(u[i] * u[i] + v[i] * v[i]), step = Math.Min(0.5, 0.6 * push[i] + 0.05);
                        // Never more than 4 m back from the OSM line, never into the lowest terrace's reach.
                        double nr = Math.Max(r - step, 42.0);
                        u[i] *= nr / r;
                        v[i] *= nr / r;
                    }
                }
            }
            BoudhaKit.Ccw(u, v, n);
            f.WallU = u;
            f.WallV = v;
            f.WallN = n;
        }

        /// <summary>Distance from the axis to the wall line along the site direction <paramref name="ang"/> (radians from
        /// +v toward +u).</summary>
        public static double RadiusAt(in BoudhaFrame f, double ang)
        {
            double[] pu, pv;
            int n = Line(f, out pu, out pv);
            double du = Math.Sin(ang), dv = Math.Cos(ang), best = 50;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                // Ray (0,0) + t (du, dv) against segment j → i.
                double ex = pu[i] - pu[j], ey = pv[i] - pv[j];
                double den = du * ey - dv * ex;
                if (Math.Abs(den) < 1e-12) continue;
                double t = (pu[j] * ey - pv[j] * ex) / den, s = (pu[j] * dv - pv[j] * du) / den;
                if (t > 0 && s >= 0 && s <= 1)
                {
                    best = t;
                    break;
                }
            }
            return best;
        }

        /// <summary>The wall pieces (from, to as arc lengths along the line from vertex 0) between the openings.</summary>
        private static int Runs(double[] pu, double[] pv, int n, double[] s0, double[] s1)
        {
            // Arc lengths of the gap centres: where the line crosses the north and south axes.
            double per = BoudhaKit.Perimeter(pu, pv, n);
            double sN = CrossingLength(pu, pv, n, 0), sS = CrossingLength(pu, pv, n, Math.PI);
            int k = 0;
            double a0 = sN + NorthGapHalf, a1 = sS - SouthGapHalf;
            if (a1 < a0) a1 += per;
            s0[k] = a0;
            s1[k++] = a1;
            double b0 = sS + SouthGapHalf, b1 = sN - NorthGapHalf;
            if (b1 < b0) b1 += per;
            s0[k] = b0;
            s1[k++] = b1;
            return k;
        }

        /// <summary>Arc length along the line where it crosses the site direction <paramref name="ang"/>.</summary>
        private static double CrossingLength(double[] pu, double[] pv, int n, double ang)
        {
            double du = Math.Sin(ang), dv = Math.Cos(ang), acc = 0;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                double ex = pu[j] - pu[i], ey = pv[j] - pv[i], l = Math.Sqrt(ex * ex + ey * ey);
                double den = du * ey - dv * ex;
                if (Math.Abs(den) > 1e-12)
                {
                    double t = (pu[i] * ey - pv[i] * ex) / den, s = (pu[i] * dv - pv[i] * du) / den;
                    if (t > 0 && s >= 0 && s <= 1) return acc + s * l;
                }
                acc += l;
            }
            return 0;
        }

        /// <summary>Arc lengths along the wall line where it crosses the four quarter boundaries (bearings 45°, 135°,
        /// 225° and 315° from the site's north axis), in that order.</summary>
        private static void QuarterCuts(double[] pu, double[] pv, int n, double[] cuts)
        {
            for (int i = 0; i < 4; i++) cuts[i] = CrossingLength(pu, pv, n, (45 + 90 * i) * Math.PI / 180.0);
        }

        /// <summary>The wall pieces of one quarter of the kora (<paramref name="part"/> North, East, South or West): the wall
        /// body, its niches, the finials over the piers, the two darchor poles of the quarter and, in the south, the gate
        /// pavilion with its juniper kiln. Returns the niches drawn (the quarter's share of the 147).</summary>
        public static int Build(MeshData m, GenColliders c, in BoudhaFrame f, BoudhaPart part, int lod)
        {
            int v0 = m.VertexCount, i0 = m.IndexCount;
            Affine3 k = f.Site;
            double[] pu, pv;
            int n = Line(f, out pu, out pv);
            double per = BoudhaKit.Perimeter(pu, pv, n);
            double[] s0 = new double[2], s1 = new double[2], cuts = new double[4];
            int runs = Runs(pu, pv, n, s0, s1);
            QuarterCuts(pu, pv, n, cuts);
            double total = 0;
            for (int r = 0; r < runs; r++) total += s1[r] - s0[r];
            ShapeBrush w = BoudhaKit.B(BoudhaKit.LimeWhite, MaterialChannel.Plaster), foot = BoudhaKit.B(BoudhaKit.Weathered, MaterialChannel.Plaster);
            if (lod >= 3)
            {
                // The far wall (the hero slice steps a quarter down to this from the rooftops and the air): the white faces,
                // the coping and the band of dark niches under their yellow valances, no solids.
                for (int r = 0; r < runs; r++) WallRun(m, null, f, k, pu, pv, n, per, s0[r], s1[r], 0, cuts, part, w, foot, 3);
                SacredDraw.Bake(m, v0, i0, f.Level);
                return 0;
            }
            int niche = 0, drawn = 0;
            for (int r = 0; r < runs; r++)
            {
                // Walk the run edge by edge: straight wall pieces along the polygon, cut at the run ends and the quarter cuts.
                int count = (int)Math.Round((s1[r] - s0[r]) / (total / NicheCount));
                if (r == runs - 1) count = NicheCount - niche;
                WallRun(m, c, f, k, pu, pv, n, per, s0[r], s1[r], count, cuts, part, w, foot, lod);
                for (int q = 0; q < count && niche < NicheCount; q++, niche++)
                {
                    double s = s0[r] + (q + 0.5) * (s1[r] - s0[r]) / count;
                    double u, v, tu, tv;
                    BoudhaKit.AtLength(pu, pv, n, s, out u, out v, out tu, out tv);
                    if (BoudhaSite.QuarterOf(u, v) != part) continue;
                    // Outward of the counter-clockwise line: the edge turned clockwise.
                    double ou = tv, ov = -tu;
                    Affine3 nf = BoudhaKit.Facing(k, u + HalfThick * ou, 0, v + HalfThick * ov, ou, ov);
                    Niche(m, nf, lod, q);
                    drawn++;
                    if (lod <= 1 && (lod == 0 || q % 4 == 0) && q < count - 1)
                    {
                        // A finial over the pier after this niche.
                        double sp = s0[r] + (q + 1.0) * (s1[r] - s0[r]) / count;
                        double pu2, pv2, t2u, t2v;
                        BoudhaKit.AtLength(pu, pv, n, sp, out pu2, out pv2, out t2u, out t2v);
                        BoudhaKit.FinialChorten(m, SacredDraw.At(k, pu2, Height + 0.08, pv2), 0.72, 1);
                    }
                }
            }
            // The darchor poles at the eight intermediate bearings, just inside the wall.
            for (int b = 0; b < 8; b++)
            {
                double ang = (22.5 + 45 * b) * Math.PI / 180, rad = RadiusAt(f, ang), u = (rad - 0.75) * Math.Sin(ang), v = (rad - 0.75) * Math.Cos(ang);
                if (BoudhaSite.QuarterOf(u, v) == part) Pole(m, c, f, k, u, v, lod);
            }
            if (part == BoudhaPart.South) SouthPavilion(m, c, f, k, lod);
            SacredDraw.Bake(m, v0, i0, f.Level);
            return drawn;
        }

        /// <summary>The wall body between arc lengths a and b along the line: a piece per polygon edge with a base
        /// course, the white faces (the outer one opened by the <paramref name="count"/> evenly spaced niches of the run)
        /// and the rounded coping, end caps at the openings, and thin wall colliders.</summary>
        private static void WallRun(MeshData m, GenColliders c, in BoudhaFrame f, in Affine3 k, double[] pu, double[] pv, int n, double per, double a, double b,
                                    int count, double[] cuts, BoudhaPart part, in ShapeBrush w, in ShapeBrush foot, int lod)
        {
            // Collect the points of the run (start, every vertex inside it, end) with their arc lengths.
            double[] ru = BoudhaKit.U, rv = BoudhaKit.V, ra = BoudhaKit.U2;
            int cnt = 0;
            double u, v, tu, tv;
            BoudhaKit.AtLength(pu, pv, n, a, out u, out v, out tu, out tv);
            ru[cnt] = u;
            rv[cnt] = v;
            ra[cnt++] = a;
            for (int pass = 0; pass < 2; pass++)
            {
                double acc = pass * per;
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    double du = pu[j] - pu[i], dv = pv[j] - pv[i];
                    acc += Math.Sqrt(du * du + dv * dv);
                    if (acc > a && acc < b)
                    {
                        ru[cnt] = pu[j];
                        rv[cnt] = pv[j];
                        ra[cnt++] = acc;
                    }
                }
            }
            // The quarter cuts inside the run (at their arc length, also one perimeter on for a run that wraps past vertex 0).
            for (int q = 0; q < 4; q++)
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    double sc = cuts[q] + pass * per;
                    if (sc <= a + 0.01 || sc >= b - 0.01) continue;
                    BoudhaKit.AtLength(pu, pv, n, sc, out u, out v, out tu, out tv);
                    ru[cnt] = u;
                    rv[cnt] = v;
                    ra[cnt++] = sc;
                }
            }
            BoudhaKit.AtLength(pu, pv, n, b, out u, out v, out tu, out tv);
            ru[cnt] = u;
            rv[cnt] = v;
            ra[cnt++] = b;
            // Keep the points in arc-length order (the cuts were appended after the vertices).
            for (int i = 1; i < cnt; i++)
            {
                double xa = ra[i], xu = ru[i], xv = rv[i];
                int j = i - 1;
                while (j >= 0 && ra[j] > xa)
                {
                    ra[j + 1] = ra[j];
                    ru[j + 1] = ru[j];
                    rv[j + 1] = rv[j];
                    j--;
                }
                ra[j + 1] = xa;
                ru[j + 1] = xu;
                rv[j + 1] = xv;
            }
            double y0 = -0.4, h = Height, nw = NicheW, sill = NicheSill, nh = NicheH;
            ShapeBrush cope = BoudhaKit.B(MeshColor.FromHex(0xF7F4EE), MaterialChannel.Plaster);
            double step = count > 0 ? (b - a) / count : 0;
            for (int i = 0; i + 1 < cnt; i++)
            {
                double ax = ru[i], az = rv[i], bx = ru[i + 1], bz = rv[i + 1], dx = bx - ax, dz = bz - az, l = Math.Sqrt(dx * dx + dz * dz);
                if (l < 0.05) continue;
                if (BoudhaSite.QuarterOf(0.5 * (ax + bx), 0.5 * (az + bz)) != part) continue;
                // A local frame on the centre line: x along the edge, z toward the stupa (the kora face is z < 0).
                Affine3 e = SacredDraw.Basis(k, ax, 0, az, dx / l, 0, dz / l, 0, 1, 0);
                double x0 = i == 0 ? 0 : -0.12, x1 = i + 2 == cnt ? l : l + 0.12;
                if (lod >= 3)
                {
                    for (int s = -1; s <= 1; s += 2)
                    {
                        double z = s * HalfThick;
                        SacredDraw.Quad(m, e, x0, y0, z, x1, y0, z, x1, h + 0.1, z, x0, h + 0.1, z, 0, 0, s, w);
                    }
                    SacredDraw.Quad(m, e, x0, h + 0.1, -HalfThick, x1, h + 0.1, -HalfThick, x1, h + 0.1, HalfThick, x0, h + 0.1, HalfThick, 0, 1, 0, cope);
                    double zb = -HalfThick - 0.015, xa = x0 + 0.3, xb = x1 - 0.3;
                    if (xb > xa)
                    {
                        SacredDraw.Quad(m, e, xa, sill, zb, xb, sill, zb, xb, sill + nh, zb, xa, sill + nh, zb, 0, 0, -1, BoudhaKit.B(BoudhaKit.Recess, MaterialChannel.Plain, 0.6f));
                        SacredDraw.Quad(m, e, xa, sill + nh, zb, xb, sill + nh, zb, xb, sill + nh + 0.24, zb, xa, sill + nh + 0.24, zb, 0, 0, -1,
                                        BoudhaKit.B(BoudhaKit.NicheValance, MaterialChannel.Fabric));
                    }
                    continue;
                }
                if (lod >= 1)
                {
                    // Far: each face one quad from the foot to the coping (the niches are drawn proud of it), the coping
                    // band and its top.
                    for (int s = -1; s <= 1; s += 2)
                    {
                        double z = s * HalfThick;
                        SacredDraw.Quad(m, e, x0, y0, z, x1, y0, z, x1, h - 0.12, z, x0, h - 0.12, z, 0, 0, s, w);
                        if (lod == 1) SacredDraw.Quad(m, e, x0, y0, z + s * 0.01, x1, y0, z + s * 0.01, x1, 0.32, z + s * 0.01, x0, 0.32, z + s * 0.01, 0, 0, s, foot);
                        SacredDraw.Quad(m, e, x0, h - 0.12, z, x1, h - 0.12, z, x1, h + 0.1, z, x0, h + 0.1, z, 0, 0, s, cope);
                    }
                    SacredDraw.Quad(m, e, x0, h + 0.1, -HalfThick, x1, h + 0.1, -HalfThick, x1, h + 0.1, HalfThick, x0, h + 0.1, HalfThick, 0, 1, 0, cope);
                    if (c != null)
                    {
                        double wx0, wy0, wz0, wx1, wy1, wz1;
                        k.Point(ax, 0, az, out wx0, out wy0, out wz0);
                        k.Point(bx, 0, bz, out wx1, out wy1, out wz1);
                        c.AddWall(wx0, wz0, wx1, wz1, f.Level - 0.4, f.Level + h + 0.1, HalfThick + 0.05);
                    }
                    continue;
                }
                // Inner face (plain) and the base course on both faces.
                Face(m, e, x0, x1, HalfThick, 1, 0.34, h - 0.2, w);
                for (int s = -1; s <= 1; s += 2)
                {
                    double z = s * HalfThick, zz = z + s * 0.06, zc = z + s * 0.09;
                    SacredDraw.Quad(m, e, x0, y0, zz, x1, y0, zz, x1, 0.3, zz, x0, 0.3, zz, 0, 0, s, foot);
                    SacredDraw.Quad(m, e, x0, 0.3, zz, x1, 0.3, zz, x1, 0.34, z, x0, 0.34, z, 0, 1, s, foot);
                    SacredDraw.Quad(m, e, x0, h - 0.2, z, x1, h - 0.2, z, x1, h - 0.12, zc, x0, h - 0.12, zc, 0, -0.5, s, cope);
                    SacredDraw.Quad(m, e, x0, h - 0.12, zc, x1, h - 0.12, zc, x1, h + 0.02, zc, x0, h + 0.02, zc, 0, 0, s, cope);
                    SacredDraw.Quad(m, e, x0, h + 0.02, zc, x1, h + 0.02, zc, x1, h + 0.1, z + s * 0.04, x0, h + 0.1, z + s * 0.04, 0, 0.7, s, cope);
                }
                SacredDraw.Quad(m, e, x0, h + 0.1, -HalfThick - 0.04, x1, h + 0.1, -HalfThick - 0.04, x1, h + 0.1, HalfThick + 0.04, x0, h + 0.1, HalfThick + 0.04, 0, 1, 0,
                                cope);
                // Outer face: full below the sills and above the niches, piers between the niche openings.
                Face(m, e, x0, x1, -HalfThick, -1, 0.34, sill, w);
                Face(m, e, x0, x1, -HalfThick, -1, sill + nh, h - 0.2, w);
                double cursor = x0;
                if (step > 0)
                {
                    int k0 = Math.Max(0, (int)Math.Floor((ra[i] - a) / step) - 1), k1 = Math.Min(count - 1, (int)Math.Ceiling((ra[i + 1] - a) / step) + 1);
                    for (int q = k0; q <= k1; q++)
                    {
                        double cs = a + (q + 0.5) * step - ra[i], o0 = cs - 0.5 * nw, o1 = cs + 0.5 * nw;
                        if (o1 <= x0 || o0 >= x1) continue;
                        o0 = Math.Max(o0, x0);
                        o1 = Math.Min(o1, x1);
                        if (o0 > cursor) Face(m, e, cursor, o0, -HalfThick, -1, sill, sill + nh, w);
                        cursor = Math.Max(cursor, o1);
                    }
                }
                if (cursor < x1) Face(m, e, cursor, x1, -HalfThick, -1, sill, sill + nh, w);
                if (c != null)
                {
                    double wx0, wy0, wz0, wx1, wy1, wz1;
                    k.Point(ax, 0, az, out wx0, out wy0, out wz0);
                    k.Point(bx, 0, bz, out wx1, out wy1, out wz1);
                    c.AddWall(wx0, wz0, wx1, wz1, f.Level - 0.4, f.Level + h + 0.1, HalfThick + 0.05);
                }
            }
            // End caps at the openings (drawn by the quarter that holds the end).
            for (int end = 0; end < 2; end++)
            {
                int i = end == 0 ? 0 : cnt - 1, j = end == 0 ? 1 : cnt - 2;
                if (BoudhaSite.QuarterOf(ru[i], rv[i]) != part) continue;
                double dx = ru[j] - ru[i], dz = rv[j] - rv[i], l = Math.Sqrt(dx * dx + dz * dz);
                if (l < 1e-6) continue;
                Affine3 e = BoudhaKit.Facing(k, ru[i], 0, rv[i], -dx / l, -dz / l);
                SacredDraw.Panel(m, e, -HalfThick - 0.06, y0, HalfThick + 0.06, h + 0.1, 0.0, w);
            }
        }

        /// <summary>A flat wall face in frame e at z (facing side s) from x0 to x1 and ya to yb.</summary>
        private static void Face(MeshData m, in Affine3 e, double x0, double x1, double z, int s, double ya, double yb, in ShapeBrush b)
        {
            if (x1 - x0 < 1e-4 || yb - ya < 1e-4) return;
            SacredDraw.Quad(m, e, x0, ya, z, x1, ya, z, x1, yb, z, x0, yb, z, 0, 0, s, b);
        }

        /// <summary>One prayer-wheel niche in frame nf (origin on the wall's outer face at the niche centre, +z outward).</summary>
        private static void Niche(MeshData m, in Affine3 nf, int lod, int q)
        {
            double nw = NicheW, nh = NicheH, sill = NicheSill, depth = 0.22;
            ShapeBrush dark = BoudhaKit.B(BoudhaKit.Recess, MaterialChannel.Plain, 0.5f), frame = BoudhaKit.B(MeshColor.FromHex(0xE9E4DA), MaterialChannel.Plaster);
            ShapeBrush grille = BoudhaKit.B(MeshColor.FromHex(0x1C1814), MaterialChannel.Metal);
            if (lod >= 2)
            {
                SacredDraw.Panel(m, nf, -0.5 * nw, sill, 0.5 * nw, sill + nh + 0.2, 0.01, dark);
                return;
            }
            if (lod == 1)
            {
                // Flat: the dark opening, the copper band of wheels and the yellow valance, all just proud of the face.
                SacredDraw.Panel(m, nf, -0.5 * nw, sill, 0.5 * nw, sill + nh, 0.01, dark);
                SacredDraw.Panel(m, nf, -0.5 * nw + 0.05, sill + 0.06, 0.5 * nw - 0.05, sill + 0.48, 0.015,
                                 BoudhaKit.B(q % 3 == 0 ? BoudhaKit.Brass : BoudhaKit.Copper, MaterialChannel.Metal, 0.5f));
                SacredDraw.Panel(m, nf, -0.5 * nw - 0.1, sill + nh - 0.02, 0.5 * nw + 0.1, sill + nh + 0.24, 0.02, BoudhaKit.B(BoudhaKit.NicheValance, MaterialChannel.Fabric));
                return;
            }
            // The recess: back, sides, top and sill (seen into), a raised frame round it.
            SacredDraw.Panel(m, nf, -0.5 * nw, sill, 0.5 * nw, sill + nh, -depth, dark);
            SacredDraw.Quad(m, nf, -0.5 * nw, sill, -depth, 0.5 * nw, sill, -depth, 0.5 * nw, sill, 0.0, -0.5 * nw, sill, 0.0, 0, 1, 0, BoudhaKit.B(BoudhaKit.Weathered, MaterialChannel.Plaster, 0.7f));
            SacredDraw.Quad(m, nf, -0.5 * nw, sill + nh, -depth, 0.5 * nw, sill + nh, -depth, 0.5 * nw, sill + nh, 0.0, -0.5 * nw, sill + nh, 0.0, 0, -1, 0, dark);
            SacredDraw.Quad(m, nf, -0.5 * nw, sill, -depth, -0.5 * nw, sill, 0, -0.5 * nw, sill + nh, 0, -0.5 * nw, sill + nh, -depth, 1, 0, 0, dark);
            SacredDraw.Quad(m, nf, 0.5 * nw, sill, -depth, 0.5 * nw, sill, 0, 0.5 * nw, sill + nh, 0, 0.5 * nw, sill + nh, -depth, -1, 0, 0, dark);
            // Copper wheels on their spindles (four), and the black lattice band across the lower half.
            int wheels = 4;
            // The wheels sit in the shade of the niche: they read dark from the kora (commons_late/c03).
            ShapeBrush cu = BoudhaKit.B(q % 3 == 0 ? BoudhaKit.Brass : BoudhaKit.Copper, MaterialChannel.Metal, 0.5f);
            for (int i = 0; i < wheels; i++)
            {
                double x = -0.5 * nw + nw * (i + 0.5) / wheels;
                Shapes.Cylinder(m, SacredDraw.At(nf, x, sill + 0.06, -0.11), cu, 0.13, 0.42, 5, 0, 0, false, false);
            }
            if (lod == 0)
            {
                SacredDraw.Box(m, nf, -0.5 * nw, 0.5 * nw, sill, sill + 0.05, -0.02, 0.02, grille, BoxFaces.Front | BoxFaces.Top);
                for (int i = 0; i <= wheels; i++)
                {
                    double x = -0.5 * nw + nw * i / wheels;
                    SacredDraw.Box(m, nf, x - 0.025, x + 0.025, sill, sill + nh, -0.02, 0.02, grille, BoxFaces.Front);
                }
            }
            // The cloth valance over the niche: yellow with thin white and red stripes (commons_late/c03).
            uint[] stripes = { BoudhaKit.ClothRed, BoudhaKit.ClothWhite };
            BoudhaKit.Valance(m, nf, -0.5 * nw - 0.12, 0.5 * nw + 0.12, 0.04, sill + nh + 0.26, stripes, 0.04, BoudhaKit.NicheValance, 0.2, 0.2, lod == 0 ? 1 : 2);
            // A sooty lamp niche at the foot of every other pier.
            if (lod == 0 && q % 2 == 0)
                SacredDraw.Panel(m, nf, 0.5 * nw + 0.12, 0.12, 0.5 * nw + 0.36, 0.42, 0.006, BoudhaKit.B(BoudhaKit.Recess, MaterialChannel.Plain, 0.6f));
        }

        /// <summary>
        /// A white juniper kiln (sang-khang) in frame bf (+z outward; corner/02): a broad two-tier base (a plinth slab and a
        /// sloping chamfered tier), a square firebox with its sooty arched mouth on the front and a black smoke stain, a
        /// broad overhanging hipped white eave over it, a lotus collar, a bell-shaped bulb, a small square harmika and a
        /// short spike. <paramref name="baseW"/> is the plinth width and <paramref name="h"/> the total height (about 1.5
        /// and 2.3 m).
        /// </summary>
        internal static void Kiln(MeshData m, in Affine3 bf, double baseW, double h, int lod)
        {
            ShapeBrush w = BoudhaKit.White, sh = BoudhaKit.WhiteShade, soot = BoudhaKit.B(MeshColor.FromHex(0x24201C), MaterialChannel.Plain, 0.5f);
            double s = h / 2.3, hw = 0.5 * baseW;
            // The base: a plinth slab, then a sloping tier up to the firebox.
            Mould mo = SacredDraw.M;
            if (lod >= 2) mo.Add(0, -0.2, sh).Add(0, 0.25 * s, w).Add(-0.3 * hw, 0.5 * s);
            else mo.Add(0, -0.2, sh).Add(0, 0.2 * s, w).Add(-0.04 * hw, 0.25 * s).Add(-0.1 * hw, 0.27 * s).Add(-0.3 * hw, 0.5 * s).Add(-0.33 * hw, 0.52 * s);
            SacredDraw.RectRing(m, bf, 0, 0, hw, hw, mo, true, w);
            // The firebox and its mouth.
            double bw = 0.6 * hw, y0 = 0.52 * s, y1 = 1.18 * s;
            SacredDraw.Box(m, bf, -bw, bw, y0 - 0.05, y1, -bw, bw, w, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
            double mw = 0.12 * s, mh = 0.22 * s, my = y0 + 0.1 * s;
            SacredDraw.Panel(m, bf, -mw, my, mw, my + mh, bw + 0.005, soot);
            if (lod <= 1) Shapes.Dome(m, SacredDraw.Basis(bf, 0, my + mh, bw + 0.006, 1, 0, 0, 0, 0, 1), soot, mw, 0.11 * s, lod == 0 ? 8 : 5);
            // The smoke stain: a soot streak from the mouth down over the base.
            if (lod == 0) SacredDraw.Quad(m, bf, -0.07 * s, 0.05 * s, hw * 0.98 + 0.01, 0.06 * s, 0.05 * s, hw * 0.98 + 0.01, 0.1 * s, my, bw + 0.01, -0.1 * s, my, bw + 0.01, 0, 0.5, 1,
                                          BoudhaKit.B(MeshColor.FromHex(0x3A3430), MaterialChannel.Plain, 0.7f));
            // The overhanging hipped eave (a shallow white pyramid frustum with a thick lip).
            double ew = 1.0 * hw + 0.05, ey = y1 + 0.05 * s;
            SacredDraw.Box(m, bf, -ew, ew, y1 - 0.02, ey, -ew, ew, sh, BoxFaces.All & ~BoxFaces.Bottom & ~BoxFaces.Top);
            Shapes.Frustum(m, SacredDraw.At(bf * Affine3.Yaw(45), 0, ey, 0), w, ew * 1.414, 0.42 * hw * 1.414, 0.26 * s, 4, 0, 0, true, true);
            // Lotus collar, the bell-shaped bulb, the harmika and the spike.
            double yb = ey + 0.26 * s, r = 0.4 * hw;
            Profile2 p = BoudhaKit.Prof;
            if (lod >= 2) p.Add(0, yb).Add(r, yb, true).Add(0.9 * r, yb + 0.35 * s).Add(0.45 * r, yb + 0.55 * s).Add(0, yb + 0.6 * s);
            else p.Add(0, yb).Add(1.08 * r, yb, true).Add(1.08 * r, yb + 0.05 * s, true).Add(0.95 * r, yb + 0.08 * s, true).Add(0.98 * r, yb + 0.22 * s)
                  .Add(0.88 * r, yb + 0.4 * s).Add(0.62 * r, yb + 0.53 * s).Add(0.25 * r, yb + 0.6 * s).Add(0, yb + 0.61 * s);
            Shapes.Lathe(m, bf, w, p, lod == 0 ? 12 : lod == 1 ? 8 : 6);
            double yh = yb + 0.58 * s;
            SacredDraw.Box(m, bf, -0.11 * s, 0.11 * s, yh, yh + 0.12 * s, -0.11 * s, 0.11 * s, w, BoxFaces.All & ~BoxFaces.Bottom);
            Shapes.Cone(m, SacredDraw.At(bf, 0, yh + 0.12 * s, 0), w, 0.07 * s, h - yh - 0.12 * s, lod == 0 ? 6 : 4, 0, 0, false);
        }

        /// <summary>A darchor: a tall wooden pole with a vertical five-colour banner and a gilt tip.</summary>
        private static void Pole(MeshData m, GenColliders c, in BoudhaFrame f, in Affine3 k, double u, double v, int lod)
        {
            ShapeBrush wood = BoudhaKit.B(MeshColor.FromHex(0x6B4A2E), MaterialChannel.Wood);
            Shapes.Cylinder(m, SacredDraw.At(k, u, -0.2, v), wood, 0.09, PoleTop + 0.2, lod == 0 ? 8 : 5, 0, 0, false, true);
            Shapes.Cone(m, SacredDraw.At(k, u, PoleTop, v), BoudhaKit.Gilt, 0.12, PoleTipH, lod == 0 ? 8 : 4, 0, 0, true);
            if (lod <= 1)
            {
                // The banner: five colour bands down a long cloth on the pole's leeward side.
                Affine3 pf = BoudhaKit.Facing(k, u, 0, v, 1, 0);
                double top = PoleTop - 0.3, bh = 1.0;
                for (int i = 0; i < 5; i++)
                {
                    ShapeBrush col = BoudhaKit.B(SacredPalette.Flags[i], MaterialChannel.Fabric);
                    double ya = top - i * bh, yb = ya - bh;
                    for (int s = -1; s <= 1; s += 2)
                        SacredDraw.Quad(m, pf, 0.08, yb, 0, 0.08 + 0.55, yb + 0.05, 0, 0.08 + 0.55, ya + 0.05, 0, 0.08, ya, 0, 0, 0, s, col);
                }
            }
            if (c != null)
            {
                double x, z;
                f.ToTile(u, v, out x, out z);
                c.AddCylinder(x, z, f.Level - 0.2, f.Level + PoleTop, 0.12, true);
            }
        }

        /// <summary>The south gate pavilion in the wall line on the south axis (corner/02, seen through the main gate): a
        /// white plastered block with an arched passage, a timber door under a gilt torana, small brass wheels and lamp
        /// niches either side, a moulded cornice with chorten finials; the juniper kiln stands in front on the kora.</summary>
        private static void SouthPavilion(MeshData m, GenColliders c, in BoudhaFrame f, in Affine3 k, int lod)
        {
            double rad = RadiusAt(f, Math.PI);
            // Frame facing out (south) at the wall line.
            // The pavilion stands back into the compound so its front is flush with the wall's kora face (the kora walk the
            // road mesher draws runs along the wall).
            const double d = 2.4;
            Affine3 p = BoudhaKit.Facing(k, 0, 0, -(rad - 0.5 * d + HalfThick + 0.05), 0, -1);
            Pavilion(m, c, f, p, 4.0, d, 3.3, false, lod);
            // The juniper kiln on the kora just in front of the door, a little to the west (corner/02); solid only where
            // it keeps off the drawn kora walk.
            double ku = -0.8, kv = -rad - HalfThick - 0.1 - 0.85;
            Affine3 kf = BoudhaKit.Facing(k, ku, BoudhaQuarter.PaveTop, kv, 0, -1);
            Kiln(m, kf, 1.6, 2.3, lod);
            if (c != null && BoudhaQuarter.Clear(f.Tile, f, ku, kv, 0.85))
            {
                double x, z;
                f.ToTile(ku, kv, out x, out z);
                c.AddCylinder(x, z, f.Level - 0.2, f.Level + BoudhaQuarter.PaveTop + 2.3, 0.85);
            }
        }

        /// <summary>
        /// A white gate pavilion of the compound wall in frame p (origin at the wall line, +z outward toward the kora):
        /// <paramref name="w"/> wide, <paramref name="d"/> deep, <paramref name="h"/> to its cornice; an arched opening
        /// (open passage when <paramref name="passage"/>, else a closed timber door) with a gilt torana panel and a
        /// yellow striped valance, pilasters, a moulded cornice with small chorten finials at its corners. Walls round the
        /// passage are colliders.
        /// </summary>
        internal static void Pavilion(MeshData m, GenColliders c, in BoudhaFrame f, in Affine3 p, double w, double d, double h, bool passage, int lod)
        {
            ShapeBrush wh = BoudhaKit.White, sh = BoudhaKit.WhiteShade, cope = BoudhaKit.B(MeshColor.FromHex(0xF7F4EE), MaterialChannel.Plaster);
            // The north passages (y2019/06-08) are door-high to the springing with a gilt torana filling the round head; the
            // south pavilion's closed door (corner/02) sits under a lower round-headed recess.
            double hw = 0.5 * w, hd = 0.5 * d, aw = 0.85, ah = passage ? 3.0 : 2.5;
            // Two piers either side of the arch, the wall over it with the round head cut out.
            for (int s = -1; s <= 1; s += 2)
            {
                double xi = s * aw, xo = s * hw;
                SacredDraw.Box(m, p, Math.Min(xi, xo), Math.Max(xi, xo), -0.3, h, -hd, hd, wh, BoxFaces.All & ~BoxFaces.Bottom);
                // Pilaster strips on the front.
                if (lod <= 1) SacredDraw.Box(m, p, s * (aw + 0.08) - 0.12, s * (aw + 0.08) + 0.12, 0, h - 0.1, hd, hd + 0.06, sh, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                if (c != null)
                {
                    double x0, y0, z0, x1, y1, z1;
                    p.Point(s * 0.5 * (aw + hw), 0, -hd, out x0, out y0, out z0);
                    p.Point(s * 0.5 * (aw + hw), 0, hd, out x1, out y1, out z1);
                    c.AddWall(x0, z0, x1, z1, f.Level - 0.3, f.Level + h, 0.5 * (hw - aw));
                }
            }
            // The arch head: an extruded wall piece with a round cut.
            double[] x = BoudhaKit.U2, z = BoudhaKit.V2;
            int n = 0, seg = lod == 0 ? 10 : lod == 1 ? 5 : 3;
            double spring = ah - aw;
            for (int i = 0; i <= seg; i++)
            {
                double t = Math.PI * i / seg;
                x[n] = -aw * Math.Cos(t);
                z[n++] = -(spring + aw * Math.Sin(t));
            }
            x[n] = aw;
            z[n++] = -h;
            x[n] = -aw;
            z[n++] = -h;
            Shapes.BevelExtrude(m, SacredDraw.Basis(p, 0, 0, -hd, 1, 0, 0, 0, 0, 1), wh, x, z, n, d, 0.03, 1, BevelStyle.Chamfer, true, true);
            if (!passage)
            {
                // A closed, carved timber door under the round-headed recess (corner/02): the door leaf in panels, its
                // frame, the white tympanum filling the arch head, and the door is solid.
                ShapeBrush door = BoudhaKit.B(MeshColor.FromHex(0x5A3A22), MaterialChannel.WoodCarved), doorFrame = BoudhaKit.B(MeshColor.FromHex(0x3E2616), MaterialChannel.Wood);
                SacredDraw.Panel(m, p, -aw, 0, aw, spring, 0.12, door);
                if (lod == 0) BoudhaKit.GridLines(m, p, -aw, aw, 0.1, spring - 0.05, 0.125, 0.42, 0.38, 0.05, doorFrame);
                if (lod <= 1)
                {
                    SacredDraw.Box(m, p, -aw, aw, spring, spring + 0.1, 0.1, 0.2, doorFrame, BoxFaces.Front | BoxFaces.Bottom);
                    int tf = m.VertexCount;
                    int tc = SacredDraw.V(m, p, 0, spring + 0.1, 0.1, 0, 0, 1, sh);
                    for (int i = 0; i <= seg; i++)
                    {
                        double t = Math.PI * i / seg;
                        SacredDraw.V(m, p, -aw * Math.Cos(t), spring + 0.1 + (aw - 0.1) * Math.Sin(t), 0.1, 0, 0, 1, sh);
                    }
                    for (int i = 0; i < seg; i++) SacredDraw.Tri(m, tc, tf + 1 + i, tf + 2 + i);
                }
                if (c != null)
                {
                    double x0, y0, z0, x1, y1, z1;
                    p.Point(-aw, 0, 0.1, out x0, out y0, out z0);
                    p.Point(aw, 0, 0.1, out x1, out y1, out z1);
                    c.AddWall(x0, z0, x1, z1, f.Level - 0.3, f.Level + h, 0.15);
                }
            }
            else
            {
                // The passage floor and a dark inner shade.
                SacredDraw.Quad(m, p, -aw, 0.02, -hd, aw, 0.02, -hd, aw, 0.02, hd, -aw, 0.02, hd, 0, 1, 0, BoudhaKit.B(BoudhaKit.StoneFlag, MaterialChannel.Flagstone));
            }
            ShapeBrush gilt = BoudhaKit.Gilt;
            if (passage)
            {
                // The gilt torana filling the round arch head (a fan of carved gilt over the passage: a solid half disc
                // with a raised rim and a central boss) and the yellow striped valance hanging at the springing.
                ShapeBrush giltS = BoudhaKit.B(BoudhaKit.GiltShade, MaterialChannel.Gilt, 0.85f);
                int ts = lod == 0 ? 10 : lod == 1 ? 6 : 3;
                for (int face = -1; face <= 1; face += 2)
                {
                    Affine3 tf = face > 0 ? p : SacredDraw.Turn(p, 0, 0, 0, Math.PI);
                    double tz = hd - 0.12;
                    int fc = m.VertexCount;
                    SacredDraw.V(m, tf, 0, spring, tz, 0, 0, 1, gilt);
                    for (int i = 0; i <= ts; i++)
                    {
                        double t = Math.PI * i / ts;
                        SacredDraw.V(m, tf, -aw * Math.Cos(t), spring + aw * Math.Sin(t), tz, 0, 0, 1, gilt);
                    }
                    for (int i = 0; i < ts; i++) SacredDraw.Tri(m, fc, fc + 1 + i, fc + 2 + i);
                    if (lod <= 1 && face > 0)
                    {
                        Shapes.Torus(m, SacredDraw.Basis(tf, 0, spring, tz + 0.03, 1, 0, 0, 0, 0, 1) * Affine3.Scaling(1, 0.6, 1), giltS, aw - 0.08, 0.05, lod == 0 ? 14 : 8, 3,
                                     default, 90, 180, false);
                        Shapes.Sphere(m, SacredDraw.At(tf, 0, spring + 0.38, tz + 0.04), giltS, 0.14, lod == 0 ? 8 : 5);
                    }
                }
                if (lod <= 1)
                {
                    uint[] stripes = { BoudhaKit.ClothRed, BoudhaKit.ClothWhite, BoudhaKit.ClothBlue };
                    BoudhaKit.Valance(m, p, -aw - 0.05, aw + 0.05, hd + 0.03, spring + 0.02, stripes, 0.04, BoudhaKit.NicheValance, 0.22, 0.2, lod);
                }
            }
            // Cornice and the flat roof.
            Mould mo = SacredDraw.M;
            if (lod >= 2) mo.Add(0, h, cope).Add(0.16, h + 0.08).Add(0.16, h + 0.36);
            else mo.Add(0, h, cope).Add(0.08, h + 0.08).Add(0.16, h + 0.2).Add(0.16, h + 0.32).Add(0.0, h + 0.36);
            SacredDraw.RectRing(m, p, 0, 0, hw, hd, mo, true, cope);
            if (lod <= 1)
            {
                for (int q = 0; q < 4; q++)
                {
                    double cx = (q % 2 == 0 ? -1 : 1) * (hw - 0.25), cz = (q < 2 ? -1 : 1) * (hd - 0.25);
                    BoudhaKit.FinialChorten(m, SacredDraw.At(p, cx, h + 0.36, cz), 0.8, lod);
                }
                BoudhaKit.FinialChorten(m, SacredDraw.At(p, 0, h + 0.36, hd - 0.3), 1.2, lod);
                for (int s = -1; s <= 1; s += 2)
                {
                    double nx = s * (aw + 0.5 * (hw - aw));
                    if (passage)
                    {
                        // Brass wheels in lantern housings and lamp niches either side of the arch.
                        SacredDraw.Panel(m, p, nx - 0.25, 0.9, nx + 0.25, 1.5, hd + 0.005, BoudhaKit.B(BoudhaKit.Recess, MaterialChannel.Plain, 0.6f));
                        Shapes.Cylinder(m, SacredDraw.At(p, nx, 0.95, hd + 0.18), BoudhaKit.B(BoudhaKit.Brass, MaterialChannel.Metal), 0.16, 0.5, lod == 0 ? 8 : 5, 0, 0, true,
                                        true);
                    }
                    else
                    {
                        // Small round-headed lamp niches either side of the door, blackened by the lamps (corner/02).
                        ShapeBrush sootN = BoudhaKit.B(BoudhaKit.Recess, MaterialChannel.Plain, 0.55f);
                        SacredDraw.Panel(m, p, nx - 0.17, 1.45, nx + 0.17, 1.82, hd + 0.005, sootN);
                        Shapes.Dome(m, SacredDraw.Basis(p, nx, 1.82, hd + 0.006, 1, 0, 0, 0, 0, 1), sootN, 0.17, 0.15, lod == 0 ? 6 : 4);
                        if (lod == 0) SacredDraw.Quad(m, p, nx - 0.14, 1.96, hd + 0.007, nx + 0.14, 1.96, hd + 0.007, nx + 0.08, 2.2, hd + 0.007, nx - 0.08, 2.2, hd + 0.007, 0, 0, 1,
                                                      BoudhaKit.B(MeshColor.FromHex(0x6E6862), MaterialChannel.Plaster, 0.8f));
                    }
                }
            }
        }
    }
}
