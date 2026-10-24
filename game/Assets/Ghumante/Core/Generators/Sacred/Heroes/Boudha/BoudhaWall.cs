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

        /// <summary>The south pavilion stands where the axis of the lane from the main gate (OSM w4847306, site u −5 to −8
        /// on its way in; the map board n5162055056 at u −5.5) meets the wall, so it closes the vista through the gate arch
        /// (Commons Boudhanath_entrance_gate, motp/03): its centre at site u <see cref="PavilionU"/> on the wall line, site
        /// bearing <see cref="PavilionBearingDeg"/>.</summary>
        public const double PavilionU = -5.3, PavilionBearingDeg = 186.14;

        /// <summary>The south pavilion's site bearing in radians.</summary>
        public const double PavilionAngle = PavilionBearingDeg * Math.PI / 180.0;

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
            double sN = CrossingLength(pu, pv, n, 0), sS = CrossingLength(pu, pv, n, PavilionAngle);
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

        /// <summary>Arc lengths along the wall line where it crosses the kora sectors' inner boundaries (their start
        /// bearings from the site's north axis), in sector order; returns the count.</summary>
        private static int SectorCuts(double[] pu, double[] pv, int n, double[] cuts)
        {
            int k = 0;
            for (int i = 0; i < BoudhaSite.Sectors.Length; i++)
                if (BoudhaSite.Sectors[i].HasInner) cuts[k++] = CrossingLength(pu, pv, n, BoudhaSite.Sectors[i].In0 * Math.PI / 180.0);
            return k;
        }

        /// <summary>True when the wall-line point (u, v) belongs to the kora sector <paramref name="part"/>.</summary>
        private static bool Mine(BoudhaPart part, double u, double v)
        {
            return BoudhaSite.InnerAt(u, v) == part;
        }

        /// <summary>The wall pieces of one kora sector (<paramref name="part"/>, by its inner bearings): the wall body, its
        /// niches, the finials over the piers, the darchor poles that stand in it and, where it holds the south axis, the
        /// south pavilion with its forecourt. Returns the niches drawn (the sector's share of the 147).</summary>
        public static int Build(MeshData m, GenColliders c, in BoudhaFrame f, BoudhaPart part, int lod)
        {
            int v0 = m.VertexCount, i0 = m.IndexCount;
            Affine3 k = f.Site;
            double[] pu, pv;
            int n = Line(f, out pu, out pv);
            double per = BoudhaKit.Perimeter(pu, pv, n);
            double[] s0 = new double[2], s1 = new double[2], cuts = new double[BoudhaSite.Sectors.Length];
            int runs = Runs(pu, pv, n, s0, s1);
            int ncuts = SectorCuts(pu, pv, n, cuts);
            double total = 0;
            for (int r = 0; r < runs; r++) total += s1[r] - s0[r];
            ShapeBrush w = BoudhaKit.B(BoudhaKit.LimeWhite, MaterialChannel.Plaster), foot = BoudhaKit.B(BoudhaKit.Weathered, MaterialChannel.Plaster);
            if (lod >= 3)
            {
                // The far wall (the hero slice steps a quarter down to this from the rooftops and the air): the white faces,
                // the coping and the band of dark niches under their yellow valances, no solids.
                for (int r = 0; r < runs; r++) WallRun(m, null, f, k, pu, pv, n, per, s0[r], s1[r], 0, cuts, ncuts, part, w, foot, 3);
                if (Mine(part, Math.Sin(PavilionAngle), Math.Cos(PavilionAngle))) SouthPavilion(m, null, f, k, 3);
                SacredDraw.Bake(m, v0, i0, f.Level);
                return 0;
            }
            int niche = 0, drawn = 0;
            for (int r = 0; r < runs; r++)
            {
                // Walk the run edge by edge: straight wall pieces along the polygon, cut at the run ends and the quarter cuts.
                int count = (int)Math.Round((s1[r] - s0[r]) / (total / NicheCount));
                if (r == runs - 1) count = NicheCount - niche;
                WallRun(m, c, f, k, pu, pv, n, per, s0[r], s1[r], count, cuts, ncuts, part, w, foot, lod);
                for (int q = 0; q < count && niche < NicheCount; q++, niche++)
                {
                    double s = s0[r] + (q + 0.5) * (s1[r] - s0[r]) / count;
                    double u, v, tu, tv;
                    BoudhaKit.AtLength(pu, pv, n, s, out u, out v, out tu, out tv);
                    if (!Mine(part, u, v)) continue;
                    // Outward of the counter-clockwise line: the edge turned clockwise.
                    double ou = tv, ov = -tu;
                    Affine3 nf = BoudhaKit.Facing(k, u + HalfThick * ou, 0, v + HalfThick * ov, ou, ov);
                    Niche(m, nf, lod, q);
                    drawn++;
                    if (lod <= 1 && q % (lod == 0 ? 4 : 8) == 3 && q < count - 1)
                    {
                        // A small white chorten on the coping over every fourth pier (spire/ref_00-01, prayer_wheels_wall/07).
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
                if (Mine(part, u, v)) Pole(m, c, f, k, u, v, lod);
            }
            if (Mine(part, Math.Sin(PavilionAngle), Math.Cos(PavilionAngle))) SouthPavilion(m, c, f, k, lod);
            SacredDraw.Bake(m, v0, i0, f.Level);
            return drawn;
        }

        /// <summary>The wall body between arc lengths a and b along the line: a piece per polygon edge with a base
        /// course, the white faces (the outer one opened by the <paramref name="count"/> evenly spaced niches of the run)
        /// and the rounded coping, end caps at the openings, and thin wall colliders.</summary>
        private static void WallRun(MeshData m, GenColliders c, in BoudhaFrame f, in Affine3 k, double[] pu, double[] pv, int n, double per, double a, double b,
                                    int count, double[] cuts, int ncuts, BoudhaPart part, in ShapeBrush w, in ShapeBrush foot, int lod)
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
            // The sector cuts inside the run (at their arc length, also one perimeter on for a run that wraps past vertex 0).
            for (int q = 0; q < ncuts; q++)
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
                if (!Mine(part, 0.5 * (ax + bx), 0.5 * (az + bz))) continue;
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
                        // The niche band: the dark openings with the copper wheels' glint, the orange-yellow cloth over them.
                        double vb = sill + 0.6 * nh;
                        SacredDraw.Quad(m, e, xa, sill - 0.08, zb, xb, sill - 0.08, zb, xb, vb, zb, xa, vb, zb, 0, 0, -1, BoudhaKit.B(MeshColor.FromHex(0x4A3424), MaterialChannel.Metal, 0.7f));
                        SacredDraw.Quad(m, e, xa, vb, zb, xb, vb, zb, xb, sill + nh + 0.08, zb, xa, sill + nh + 0.08, zb, 0, 0, -1, BoudhaKit.B(ValanceCloth, MaterialChannel.Fabric));
                    }
                    // The small chortens on the coping as a row of white teeth (one every four piers).
                    int teeth = (int)((x1 - x0) / 8.4);
                    for (int t = 0; t < teeth; t++)
                    {
                        double tx = x0 + (t + 0.5) * (x1 - x0) / teeth;
                        SacredDraw.Tri(m, e, tx - 0.28, h + 0.1, 0, tx + 0.28, h + 0.1, 0, tx, h + 0.85, 0, 0, 0, -1, w);
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
                }
                // The thick rounded white coping (spire/ref_00-01): a chamfer out from each face, then a cap rounded at its
                // two edges (0.12 m radius) over the flat top.
                CopeProfile(m, e, x0, x1, h, cope);
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
                if (!Mine(part, ru[i], rv[i])) continue;
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

        /// <summary>The rounded coping in a wall piece's frame e (x along the wall from x0 to x1, z across with the faces at
        /// ±<see cref="HalfThick"/>): a chamfer from each face out to the cap's overhang, the cap's sides, its two edges rounded
        /// with a 0.12 m radius and the flat top, the faces shading round the profile.</summary>
        private static void CopeProfile(MeshData m, in Affine3 e, double x0, double x1, double h, in ShapeBrush cope)
        {
            const double over = 0.09, r = 0.12;
            double hz = HalfThick + over, yb = h - 0.12, yt = h + 0.14;
            // Profile points (z, y) from the outer face round to the inner face, with outward normals (nz, ny).
            double[] pz = new double[12], py = new double[12], nz = new double[12], ny = new double[12];
            int n = 0;
            pz[n] = -HalfThick; py[n] = h - 0.2; nz[n] = -0.7; ny[n] = -0.7; n++;
            pz[n] = -hz; py[n] = yb; nz[n] = -1; ny[n] = 0; n++;
            pz[n] = -hz; py[n] = yt - r; nz[n] = -1; ny[n] = 0; n++;
            for (int i = 1; i <= 3; i++)
            {
                double a = 0.5 * Math.PI * i / 3;
                pz[n] = -hz + r - r * Math.Cos(a); py[n] = yt - r + r * Math.Sin(a); nz[n] = -Math.Cos(a); ny[n] = Math.Sin(a); n++;
            }
            for (int i = 0; i <= 3; i++)
            {
                double a = 0.5 * Math.PI * i / 3;
                pz[n] = hz - r + r * Math.Sin(a); py[n] = yt - r + r * Math.Cos(a); nz[n] = Math.Sin(a); ny[n] = Math.Cos(a); n++;
            }
            pz[n] = hz; py[n] = yb; nz[n] = 1; ny[n] = 0; n++;
            pz[n] = HalfThick; py[n] = h - 0.2; nz[n] = 0.7; ny[n] = -0.7; n++;
            for (int i = 0; i + 1 < n; i++)
            {
                int a = SacredDraw.V(m, e, x0, py[i], pz[i], 0, ny[i], nz[i], cope), b = SacredDraw.V(m, e, x1, py[i], pz[i], 0, ny[i], nz[i], cope);
                int c = SacredDraw.V(m, e, x1, py[i + 1], pz[i + 1], 0, ny[i + 1], nz[i + 1], cope), d = SacredDraw.V(m, e, x0, py[i + 1], pz[i + 1], 0, ny[i + 1], nz[i + 1], cope);
                SacredDraw.Quad(m, a, b, c, d);
            }
        }

        /// <summary>Niche frame (dark wood), valance cloth and its hem (spire/ref_00-01, shankar s. 2020).</summary>
        private static readonly uint NicheFrame = MeshColor.FromHex(0x3A2A1E), ValanceCloth = MeshColor.FromHex(0xE8A830), ValanceHem = MeshColor.FromHex(0x9E2A20);

        /// <summary>
        /// One prayer-wheel niche in frame nf (origin on the wall's outer face at the niche centre, +z outward; spire/ref_00-01):
        /// the dark recess with its copper wheels and the dark rail at their feet, a dark wooden frame 0.08 m wide round the
        /// opening, the orange-yellow pleated cloth hanging inside the top 40 % of the opening with a dark red hem, half
        /// covering the wheels, and the small dark lamp slots in the pier after it at mid-height and near the foot.
        /// </summary>
        private static void Niche(MeshData m, in Affine3 nf, int lod, int q)
        {
            double nw = NicheW, nh = NicheH, sill = NicheSill, depth = 0.22, vb = sill + 0.6 * nh;
            ShapeBrush dark = BoudhaKit.B(BoudhaKit.Recess, MaterialChannel.Plain, 0.5f), frame = BoudhaKit.B(NicheFrame, MaterialChannel.Wood);
            ShapeBrush cloth = BoudhaKit.B(ValanceCloth, MaterialChannel.Fabric), hem = BoudhaKit.B(ValanceHem, MaterialChannel.Fabric);
            ShapeBrush cu = BoudhaKit.B(q % 3 == 0 ? BoudhaKit.Brass : BoudhaKit.Copper, MaterialChannel.Metal, 0.5f);
            if (lod >= 2)
            {
                // Far: the dark opening with the cloth band over its top.
                SacredDraw.Panel(m, nf, -0.5 * nw - 0.08, sill - 0.08, 0.5 * nw + 0.08, sill + nh + 0.08, 0.01, frame);
                SacredDraw.Panel(m, nf, -0.5 * nw, sill, 0.5 * nw, vb, 0.015, dark);
                SacredDraw.Panel(m, nf, -0.5 * nw, vb, 0.5 * nw, sill + nh, 0.015, cloth);
                return;
            }
            // The frame: four dark bars round the opening.
            SacredDraw.Panel(m, nf, -0.5 * nw - 0.08, sill - 0.08, 0.5 * nw + 0.08, sill, 0.012, frame);
            SacredDraw.Panel(m, nf, -0.5 * nw - 0.08, sill + nh, 0.5 * nw + 0.08, sill + nh + 0.08, 0.012, frame);
            SacredDraw.Panel(m, nf, -0.5 * nw - 0.08, sill, -0.5 * nw, sill + nh, 0.012, frame);
            SacredDraw.Panel(m, nf, 0.5 * nw, sill, 0.5 * nw + 0.08, sill + nh, 0.012, frame);
            if (lod == 1)
            {
                // Flat: the dark opening, the copper band of wheels under the cloth, the cloth and its hem.
                SacredDraw.Panel(m, nf, -0.5 * nw, sill, 0.5 * nw, vb, 0.01, dark);
                SacredDraw.Panel(m, nf, -0.5 * nw + 0.05, sill + 0.06, 0.5 * nw - 0.05, vb, 0.012, cu);
                SacredDraw.Panel(m, nf, -0.5 * nw, vb + 0.05, 0.5 * nw, sill + nh, 0.014, cloth);
                SacredDraw.Panel(m, nf, -0.5 * nw, vb, 0.5 * nw, vb + 0.05, 0.014, hem);
                return;
            }
            // The recess: back, sides, top and sill (seen into).
            SacredDraw.Panel(m, nf, -0.5 * nw, sill, 0.5 * nw, sill + nh, -depth, dark);
            SacredDraw.Quad(m, nf, -0.5 * nw, sill, -depth, 0.5 * nw, sill, -depth, 0.5 * nw, sill, 0.0, -0.5 * nw, sill, 0.0, 0, 1, 0, BoudhaKit.B(BoudhaKit.Weathered, MaterialChannel.Plaster, 0.7f));
            SacredDraw.Quad(m, nf, -0.5 * nw, sill + nh, -depth, 0.5 * nw, sill + nh, -depth, 0.5 * nw, sill + nh, 0.0, -0.5 * nw, sill + nh, 0.0, 0, -1, 0, dark);
            SacredDraw.Quad(m, nf, -0.5 * nw, sill, -depth, -0.5 * nw, sill, 0, -0.5 * nw, sill + nh, 0, -0.5 * nw, sill + nh, -depth, 1, 0, 0, dark);
            SacredDraw.Quad(m, nf, 0.5 * nw, sill, -depth, 0.5 * nw, sill, 0, 0.5 * nw, sill + nh, 0, 0.5 * nw, sill + nh, -depth, -1, 0, 0, dark);
            // Four copper wheels on their spindles in the shade of the niche (they read dark from the kora) and the dark rail.
            for (int i = 0; i < 4; i++)
            {
                double x = -0.5 * nw + nw * (i + 0.5) / 4;
                Shapes.Cylinder(m, SacredDraw.At(nf, x, sill + 0.06, -0.11), cu, 0.13, 0.42, 5, 0, 0, false, false);
            }
            SacredDraw.Box(m, nf, -0.5 * nw, 0.5 * nw, sill, sill + 0.05, -0.02, 0.02, BoudhaKit.B(MeshColor.FromHex(0x1C1814), MaterialChannel.Metal), BoxFaces.Front | BoxFaces.Top);
            // The pleated cloth inside the opening: eight soft pleats from the opening's top to 0.4 of its height, its dark red
            // hem.
            int pleats = 8, first = m.VertexCount;
            for (int r = 0; r <= 1; r++)
            {
                double yy = r == 0 ? sill + nh : vb + 0.04;
                for (int c = 0; c <= 2 * pleats; c++)
                {
                    double x = -0.5 * nw + nw * c / (2 * pleats), zz = -0.03 + (c % 2 == 0 ? 0.0 : 0.025) * (r == 0 ? 0.4 : 1.0);
                    SacredDraw.V(m, nf, x, yy, zz, 0, 0, 1, cloth);
                }
            }
            SacredDraw.Grid(m, first, 2, 2 * pleats + 1, false, null);
            SacredDraw.Quad(m, nf, -0.5 * nw, vb, -0.02, 0.5 * nw, vb, -0.02, 0.5 * nw, vb + 0.045, -0.02, -0.5 * nw, vb + 0.045, -0.02, 0, 0, 1, hem);
            // The lamp slots in the pier after this niche (0.08 × 0.18 m) at mid-height and near the foot.
            ShapeBrush slot = BoudhaKit.B(BoudhaKit.Recess, MaterialChannel.Plain, 0.55f);
            double sx = 0.5 * nw + 0.38;
            SacredDraw.Panel(m, nf, sx - 0.04, 1.21, sx + 0.04, 1.39, 0.006, slot);
            SacredDraw.Panel(m, nf, sx - 0.04, 0.11, sx + 0.04, 0.29, 0.006, slot);
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

        /// <summary>
        /// The south pavilion set into the wall where the lane from the main gate meets it (motp/03-04, Commons
        /// Boudhanath_entrance_gate, corner/02), facing down the lane: a white block 4.4 m wide whose closed, carved timber
        /// door stands in a cusped arch between carved pilasters, small sooty lamp niches either side, a moulded cornice
        /// and over it a scrolled stucco crest with three small chorten finials; and its forecourt on the kora (motp/04,
        /// 07-1): the black stone Dhyani-Buddha chaitya on its three-step plinth on the door axis, the white juniper kiln
        /// to its west, a brass prayer wheel on a post to the east and a glazed lamp case on legs beyond the kiln. The
        /// forecourt pieces standing on the drawn kora walk are drawn but walk-through.
        /// </summary>
        private static void SouthPavilion(MeshData m, GenColliders c, in BoudhaFrame f, in Affine3 k, int lod)
        {
            double ang = PavilionAngle, rad = RadiusAt(f, ang);
            double cu = rad * Math.Sin(ang), cv = rad * Math.Cos(ang);
            // The wall's direction there and its outward normal (away from the stupa).
            double ra = RadiusAt(f, ang - 0.04), rb = RadiusAt(f, ang + 0.04);
            double tu = rb * Math.Sin(ang + 0.04) - ra * Math.Sin(ang - 0.04), tv = rb * Math.Cos(ang + 0.04) - ra * Math.Cos(ang - 0.04);
            double tl = Math.Max(1e-6, Math.Sqrt(tu * tu + tv * tv));
            double nu = tv / tl, nv = -tu / tl;
            if (nu * cu + nv * cv < 0)
            {
                nu = -nu;
                nv = -nv;
            }
            // Set back into the compound so its front is flush with the wall's kora face.
            const double d = 2.4, w = 4.4, h = 3.3;
            double back = 0.5 * d - HalfThick - 0.05;
            Affine3 p = BoudhaKit.Facing(k, cu - nu * back, 0, cv - nv * back, nu, nv);
            double hw = 0.5 * w, hd = 0.5 * d;
            ShapeBrush wh = BoudhaKit.White, sh = BoudhaKit.WhiteShade, cope = BoudhaKit.B(MeshColor.FromHex(0xF7F4EE), MaterialChannel.Plaster);
            if (lod >= 3)
            {
                SacredDraw.Box(m, p, -hw, hw, -0.3, h + 0.36, -hd, hd, wh, BoxFaces.All & ~BoxFaces.Bottom);
                SacredDraw.Panel(m, p, -0.75, 0.3, 0.75, 2.35, hd + 0.01, BoudhaKit.B(MeshColor.FromHex(0x4A3020), MaterialChannel.WoodCarved));
                Shapes.Cone(m, SacredDraw.At(p, 0, h + 0.36, hd - 0.4), wh, 0.45, 0.9, 4, 0, 0, false);
                return;
            }
            // The door opening: a cusped arch (three lobes a side rising to a small point) on the door jambs.
            double aw = 0.8, spring = 2.05, point = 0.18, cusp = lod == 0 ? 0.09 : 0.06;
            int lobes = 3;
            // Piers either side and the wall over the arch with the cusped head cut out of it.
            for (int s = -1; s <= 1; s += 2)
            {
                double xi = s * aw, xo = s * hw;
                SacredDraw.Box(m, p, Math.Min(xi, xo), Math.Max(xi, xo), -0.3, h, -hd, hd, wh, BoxFaces.All & ~BoxFaces.Bottom);
                if (c != null)
                {
                    double x0, y0, z0, x1, y1, z1;
                    p.Point(s * 0.5 * (aw + hw), 0, -hd, out x0, out y0, out z0);
                    p.Point(s * 0.5 * (aw + hw), 0, hd, out x1, out y1, out z1);
                    c.AddWall(x0, z0, x1, z1, f.Level - 0.3, f.Level + h, 0.5 * (hw - aw));
                }
            }
            double[] x = BoudhaKit.U2, z = BoudhaKit.V2;
            int seg = lod == 0 ? 24 : lod == 1 ? 12 : 6, n = 0;
            x[n] = -aw;
            z[n++] = -h;
            for (int i = 0; i <= seg; i++)
            {
                double ax, ay;
                BoudhaKit.CuspedArch((double)i / seg, aw, spring, point, lod >= 2 ? 1 : lobes, lod >= 2 ? 0 : cusp, out ax, out ay);
                x[n] = ax;
                z[n++] = -ay;
            }
            x[n] = aw;
            z[n++] = -h;
            Array.Reverse(x, 0, n);
            Array.Reverse(z, 0, n);
            Shapes.BevelExtrude(m, SacredDraw.Basis(p, 0, 0, -hd, 1, 0, 0, 0, 0, 1), wh, x, z, n, d, 0.03, 1, BevelStyle.Chamfer, true, true);
            // The closed, carved timber door in the arch (its leaf in panels, a lintel and the carved tympanum over it), set
            // back in the reveal; the door is solid.
            ShapeBrush door = BoudhaKit.B(MeshColor.FromHex(0x4A3020), MaterialChannel.WoodCarved), doorFrame = BoudhaKit.B(MeshColor.FromHex(0x2E1C10), MaterialChannel.Wood);
            double dz = hd - 0.25;
            SacredDraw.Panel(m, p, -aw, 0, aw, spring + 0.6, dz, door);
            if (lod == 0) BoudhaKit.GridLines(m, p, -aw, aw, 0.1, spring - 0.05, dz + 0.005, 0.4, 0.36, 0.05, doorFrame);
            if (lod <= 1) SacredDraw.Box(m, p, -aw, aw, spring - 0.08, spring + 0.02, dz, dz + 0.1, doorFrame, BoxFaces.Front | BoxFaces.Bottom);
            if (c != null)
            {
                double x0, y0, z0, x1, y1, z1;
                p.Point(-aw, 0, dz, out x0, out y0, out z0);
                p.Point(aw, 0, dz, out x1, out y1, out z1);
                c.AddWall(x0, z0, x1, z1, f.Level - 0.3, f.Level + h, 0.15);
            }
            if (lod <= 1)
            {
                // The gilt-edged cusped arch roll (a raised white roll round the opening).
                int rs = lod == 0 ? 18 : 8;
                double px = -aw, py = spring;
                for (int i = 1; i <= rs; i++)
                {
                    double qx, qy;
                    BoudhaKit.CuspedArch((double)i / rs, aw + 0.05, spring, point, lobes, cusp, out qx, out qy);
                    Shapes.Bar(m, p, sh, px, py, hd + 0.03, qx, qy, hd + 0.03, 0.05, 4);
                    px = qx;
                    py = qy;
                }
                // Carved pilasters either side of the door: a base block, the fluted shaft and a capital.
                for (int s = -1; s <= 1; s += 2)
                {
                    double xc = s * (aw + 0.28);
                    SacredDraw.Box(m, p, xc - 0.2, xc + 0.2, 0, 0.35, hd, hd + 0.14, sh, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right | BoxFaces.Top);
                    SacredDraw.Box(m, p, xc - 0.14, xc + 0.14, 0.35, spring + 0.1, hd, hd + 0.1, wh, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                    SacredDraw.Box(m, p, xc - 0.22, xc + 0.22, spring + 0.1, spring + 0.38, hd, hd + 0.16, sh, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right | BoxFaces.Top | BoxFaces.Bottom);
                    if (lod == 0)
                    {
                        for (int q = -1; q <= 1; q++) SacredDraw.Panel(m, p, xc + q * 0.08 - 0.015, 0.45, xc + q * 0.08 + 0.015, spring, hd + 0.105, BoudhaKit.WhiteShade);
                        // A carved boss on the capital.
                        Shapes.Sphere(m, SacredDraw.At(p, xc, spring + 0.24, hd + 0.17), wh, 0.09, 6);
                    }
                    // Small round-headed lamp niches either side, blackened by the lamps (corner/02), and a brass wheel in the
                    // east one (motp/04).
                    double nx = s * (aw + 0.5 * (hw - aw) + 0.2);
                    ShapeBrush sootN = BoudhaKit.B(BoudhaKit.Recess, MaterialChannel.Plain, 0.55f);
                    SacredDraw.Panel(m, p, nx - 0.17, 1.15, nx + 0.17, 1.55, hd + 0.005, sootN);
                    Shapes.Dome(m, SacredDraw.Basis(p, nx, 1.55, hd + 0.006, 1, 0, 0, 0, 0, 1), sootN, 0.17, 0.15, lod == 0 ? 6 : 4);
                    if (s > 0) Shapes.Cylinder(m, SacredDraw.At(p, nx, 1.18, hd + 0.02), BoudhaKit.B(BoudhaKit.Brass, MaterialChannel.Metal, 0.8f), 0.12, 0.32, lod == 0 ? 8 : 5, 0, 0, true, true);
                }
            }
            // Cornice and the flat roof.
            Mould mo = SacredDraw.M;
            if (lod >= 2) mo.Add(0, h, cope).Add(0.16, h + 0.08).Add(0.16, h + 0.36);
            else mo.Add(0, h, cope).Add(0.08, h + 0.08).Add(0.16, h + 0.2).Add(0.16, h + 0.32).Add(0.0, h + 0.36);
            SacredDraw.RectRing(m, p, 0, 0, hw, hd, mo, true, cope);
            // The scrolled stucco crest over the front (0.8 m): a stepped central panel between two S-scrolls, with three
            // small chorten finials (the middle one larger) and the corner finials.
            double cy = h + 0.36, cz = hd - 0.25;
            SacredDraw.Box(m, p, -0.75, 0.75, cy, cy + 0.42, cz - 0.12, cz + 0.12, wh, BoxFaces.All & ~BoxFaces.Bottom);
            SacredDraw.Box(m, p, -0.4, 0.4, cy + 0.42, cy + 0.62, cz - 0.1, cz + 0.1, wh, BoxFaces.All & ~BoxFaces.Bottom);
            if (lod <= 1)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    // The S-scroll: an upper curl rising from the panel's foot outward, a lower curl at the outer end.
                    Affine3 sf = SacredDraw.Basis(p, s * 1.05, cy + 0.28, cz, 1, 0, 0, 0, 0, 1);
                    Shapes.Torus(m, sf, wh, 0.24, 0.06, lod == 0 ? 12 : 6, lod == 0 ? 5 : 3, default, s > 0 ? 120 : 330, 270, false);
                    Shapes.Torus(m, SacredDraw.Basis(p, s * 1.5, cy + 0.16, cz, 1, 0, 0, 0, 0, 1), wh, 0.13, 0.05, lod == 0 ? 10 : 5, 3, default, s > 0 ? 300 : 150, 270, false);
                    BoudhaKit.FinialChorten(m, SacredDraw.At(p, s * 0.6, cy + 0.42, cz), 0.4, lod);
                }
                BoudhaKit.FinialChorten(m, SacredDraw.At(p, 0, cy + 0.62, cz), 0.55, lod);
                for (int q = 0; q < 4; q++)
                {
                    double fx = (q % 2 == 0 ? -1 : 1) * (hw - 0.25), fz = (q < 2 ? -1 : 1) * (hd - 0.25);
                    BoudhaKit.FinialChorten(m, SacredDraw.At(p, fx, cy, fz), 0.7, lod);
                }
            }
            Forecourt(m, c, f, p, hd, lod);
        }

        /// <summary>The south pavilion's forecourt on the kora (motp/04, 07-1) in the pavilion's frame (front at z = hd).</summary>
        private static void Forecourt(MeshData m, GenColliders c, in BoudhaFrame f, in Affine3 p, double hd, int lod)
        {
            double y = BoudhaQuarter.PaveTop;
            // The black stone chaitya on the door axis, 1.4 m out.
            StoneChaitya(m, c, f, SacredDraw.At(p, 0, y, hd + 1.4), lod);
            // The white juniper kiln to the west, on the left as one faces the door (2.2 m off the axis, 1.6 m out; the
            // pavilion's frame faces south, so its +x runs west); solid only off the drawn kora walk.
            Affine3 kf = SacredDraw.At(p, 2.2, y, hd + 1.6);
            Kiln(m, kf, 1.5, 2.2, lod);
            Solid(c, f, kf, 0.8, 2.2);
            if (lod >= 2) return;
            // The brass prayer wheel on its post to the east (the right).
            Affine3 wf = SacredDraw.At(p, -1.8, y, hd + 0.7);
            ShapeBrush brass = BoudhaKit.B(BoudhaKit.Brass, MaterialChannel.Metal), post = BoudhaKit.B(MeshColor.FromHex(0x3A2A1E), MaterialChannel.Wood);
            Shapes.Cylinder(m, wf, post, 0.05, 1.1, lod == 0 ? 8 : 5, 0, 0, false, false);
            Shapes.Cylinder(m, SacredDraw.At(wf, 0, 1.1, 0), brass, 0.175, 0.42, lod == 0 ? 12 : 6, 0.02, 1, true, true);
            if (lod == 0)
            {
                Shapes.Torus(m, SacredDraw.At(wf, 0, 1.16, 0), BoudhaKit.B(BoudhaKit.ClothRed, MaterialChannel.Paint), 0.178, 0.015, 12, 3);
                Shapes.Torus(m, SacredDraw.At(wf, 0, 1.46, 0), BoudhaKit.B(BoudhaKit.ClothRed, MaterialChannel.Paint), 0.178, 0.015, 12, 3);
                Shapes.Cone(m, SacredDraw.At(wf, 0, 1.52, 0), brass, 0.06, 0.14, 6, 0, 0, false);
            }
            // The glazed lamp case on its legs beyond the kiln (0.5 × 0.4 × 0.6 on 0.8 m legs).
            Affine3 lf = SacredDraw.At(p, 3.4, y, hd + 1.2);
            ShapeBrush wood = BoudhaKit.B(MeshColor.FromHex(0x5A3A22), MaterialChannel.Wood), glass = BoudhaKit.B(MeshColor.FromHex(0xC9DCE0), MaterialChannel.Glass, 0.9f);
            for (int q = 0; q < 4; q++)
            {
                double lx = (q % 2 == 0 ? -1 : 1) * 0.22, lz = (q < 2 ? -1 : 1) * 0.17;
                Shapes.Bar(m, lf, wood, lx, 0, lz, lx, 0.8, lz, 0.025, 4);
            }
            SacredDraw.Box(m, lf, -0.26, 0.26, 0.8, 0.84, -0.21, 0.21, wood, BoxFaces.All);
            SacredDraw.Box(m, lf, -0.25, 0.25, 0.84, 1.42, -0.2, 0.2, glass, BoxFaces.All & ~BoxFaces.Bottom);
            SacredDraw.Box(m, lf, -0.27, 0.27, 1.42, 1.47, -0.22, 0.22, wood, BoxFaces.All);
            if (lod == 0)
                for (int q = -1; q <= 1; q++) Shapes.Frustum(m, SacredDraw.At(lf, q * 0.13, 0.84, 0), brass, 0.04, 0.06, 0.07, 5, 0, 0, false, true);
        }

        /// <summary>A cylinder collider of radius r at the origin of frame <paramref name="a"/> when it keeps off the
        /// drawn roads (forecourt pieces on the kora walk stay walk-through).</summary>
        private static void Solid(GenColliders c, in BoudhaFrame f, in Affine3 a, double r, double h)
        {
            if (c == null) return;
            double x, y, z;
            a.Point(0, 0, 0, out x, out y, out z);
            if (f.Tile != null && SacredDrawnRoads.For(f.Tile).SignedDistance(x, z) < r + 0.1) return;
            c.AddCylinder(x, z, y - 0.3, y + h, r);
        }

        /// <summary>
        /// The black stone chaitya before the south pavilion's door (motp/07-1, about 1.6 m): a three-step plinth 1.1 m
        /// square, the shaft with four seated Dhyani Buddhas facing out, a round cap and a small finial, smeared with
        /// vermilion and hung with marigolds. Standing on y = 0 of <paramref name="a"/>.
        /// </summary>
        private static void StoneChaitya(MeshData m, GenColliders c, in BoudhaFrame f, in Affine3 a, int lod)
        {
            ShapeBrush stone = BoudhaKit.B(MeshColor.FromHex(0x2E2A28), MaterialChannel.Stone), red = BoudhaKit.B(MeshColor.FromHex(0xC8202A), MaterialChannel.Paint);
            ShapeBrush marigold = BoudhaKit.B(MeshColor.FromHex(0xF2A01E), MaterialChannel.Foliage);
            for (int i = 0; i < 3; i++)
            {
                double hw = 0.55 - 0.1 * i, y0 = 0.22 * i;
                if (lod == 0) Shapes.RoundedBox(m, SacredDraw.At(a, 0, y0 + 0.11, 0), stone, 2 * hw, 0.22, 2 * hw, 0.02, 1);
                else SacredDraw.Box(m, a, -hw, hw, y0 - (i == 0 ? 0.2 : 0), y0 + 0.22, -hw, hw, stone, BoxFaces.All & ~BoxFaces.Bottom);
            }
            double sy = 0.66, sw = 0.24;
            SacredDraw.Box(m, a, -sw, sw, sy, sy + 0.55, -sw, sw, stone, BoxFaces.All & ~BoxFaces.Bottom);
            if (lod <= 1)
            {
                for (int q = 0; q < 4; q++)
                {
                    Affine3 bf = a * Affine3.Yaw(90 * q);
                    // A seated Buddha on the face: crossed legs, body, head with its ushnisha.
                    Shapes.Ellipsoid(m, SacredDraw.At(bf, 0, sy + 0.08, sw + 0.04), stone, 0.2, 0.07, 0.1, lod == 0 ? 6 : 4, false);
                    Shapes.Ellipsoid(m, SacredDraw.At(bf, 0, sy + 0.25, sw + 0.02), stone, 0.12, 0.15, 0.07, lod == 0 ? 6 : 4, false);
                    Shapes.Sphere(m, SacredDraw.At(bf, 0, sy + 0.45, sw + 0.02), stone, 0.07, lod == 0 ? 6 : 4);
                    if (lod == 0)
                    {
                        // Vermilion on the face and a marigold garland across the lap.
                        SacredDraw.Panel(m, bf, -0.04, sy + 0.42, 0.04, sy + 0.48, sw + 0.09, red);
                        for (int g = -2; g <= 2; g++)
                            Shapes.Sphere(m, SacredDraw.At(bf, g * 0.07, sy + 0.17 - 0.02 * Math.Abs(g), sw + 0.13), marigold, 0.035, 4);
                    }
                }
            }
            Profile2 cap = BoudhaKit.Prof;
            double cy = sy + 0.55;
            cap.Add(0, cy).Add(0.32, cy, true).Add(0.3, cy + 0.08).Add(0.22, cy + 0.2).Add(0.08, cy + 0.27, true).Add(0.06, cy + 0.36).Add(0.1, cy + 0.42, true)
               .Add(0.03, cy + 0.6).Add(0, cy + 0.62);
            Shapes.Lathe(m, a, stone, cap, lod == 0 ? 10 : lod == 1 ? 6 : 4);
            if (lod == 0) Shapes.Torus(m, SacredDraw.At(a, 0, cy + 0.02, 0), marigold, 0.3, 0.035, 12, 3);
            Solid(c, f, a, 0.6, 1.6);
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
