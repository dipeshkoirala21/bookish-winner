using System;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.Boudha
{
    /// <summary>
    /// S6, the main gate (Boudha Gate, OSM n5302729521) at the mouth of the paved lane (w4847306) on Boudha Main Road,
    /// facing the road (boudha.md 3.6; Commons "Boudhanath entrance gate" 2019-2022): a Tibetan ceremonial arch about
    /// 7.5 m wide with its lions and 8.6 m to the crest:
    /// <list type="bullet">
    /// <item>two piers whose faces carry the eight auspicious symbols on dark teal panels in yellow-gilt frames;</item>
    /// <item>a cusped ogee arch with a gilt edge, its spandrels painted with dragons and clouds in blue, teal and gold;</item>
    /// <item>a painted frieze with a dark sign panel and the broad yellow Tibetan roof with upturned corners;</item>
    /// <item>on the crest a white bulb chorten with a gilt lotus finial between two gilt dragons, kalash finials at the
    /// roof ends;</item>
    /// <item>outside the piers two white snow lions with turquoise manes on lotus pedestals atop white round columns;
    /// steel bollards inside.</item>
    /// </list>
    /// It stands where the lane meets the road's kerb (the 2019-2022 photos: the gate on the road frontage, an ATM booth
    /// against its east pier), across the lane.
    /// </summary>
    internal static class BoudhaGate
    {
        /// <summary>Crest finial tip above the ground (m).</summary>
        public const double Top = 8.9;

        /// <summary>Height of the white columns the snow lions sit on (m; the lion's base, at about the arch's springing).</summary>
        public const double LionColumnH = 2.7;

        /// <summary>The lane to the stupa (OSM) and the gate's distance back from the lane's end on the road's centre line.</summary>
        public const ulong LaneWay = 4847306;

        public const double BackFromRoadM = 8.6;

        /// <summary>Half the clear opening, the pier width, the springing and apex of the arch, the frieze and the eave.</summary>
        public const double HalfOpen = 2.35, PierW = 1.4, Spring = 2.7, Apex = 4.95, FriezeY = 5.9, EaveY = 6.7, RoofY = 7.25;

        /// <summary>Gate frame in the site: centre on the ground and the direction it faces (site u, v).</summary>
        internal static void Place(TileData tile, in BoudhaFrame f, out double u, out double v, out double du, out double dv)
        {
            // Fallback: the photo position on the lane at r 85, bearing 195°.
            u = -8.2;
            v = -84.0;
            du = -0.14;
            dv = -0.99;
            for (int i = 0; i < tile.Roads.Count; i++)
            {
                RoadRecord r = tile.Roads[i];
                if (r.OsmWayId != LaneWay) continue;
                int n = r.Points.Length / 2;
                if (n < 2) break;
                // The lane's end that lies farther from the stupa is on the main road.
                double ax, av, bx, bv;
                f.ToSite(r.Points[0] / 100.0, r.Points[1] / 100.0, out ax, out av);
                f.ToSite(r.Points[2 * (n - 1)] / 100.0, r.Points[2 * (n - 1) + 1] / 100.0, out bx, out bv);
                bool fromEnd = bx * bx + bv * bv > ax * ax + av * av;
                double left = BackFromRoadM;
                for (int s = 0; s < n - 1; s++)
                {
                    int i0 = fromEnd ? n - 1 - s : s, i1 = fromEnd ? n - 2 - s : s + 1;
                    double pu0, pv0, pu1, pv1;
                    f.ToSite(r.Points[2 * i0] / 100.0, r.Points[2 * i0 + 1] / 100.0, out pu0, out pv0);
                    f.ToSite(r.Points[2 * i1] / 100.0, r.Points[2 * i1 + 1] / 100.0, out pu1, out pv1);
                    double eu = pu1 - pu0, ev = pv1 - pv0, l = Math.Sqrt(eu * eu + ev * ev);
                    if (l < 1e-6) continue;
                    if (l >= left || s == n - 2)
                    {
                        double t = Math.Min(1.0, left / l);
                        u = pu0 + eu * t;
                        v = pv0 + ev * t;
                        // Facing out to the road: back along the walk toward the road end.
                        du = -eu / l;
                        dv = -ev / l;
                        Centre(tile, f, ref u, ref v, du, dv);
                        return;
                    }
                    left -= l;
                }
                break;
            }
        }

        /// <summary>Moves the gate across the lane so its opening is centred on the walk as the road mesher draws it (the
        /// drawn walk sits off the OSM centreline), which keeps both piers off it.</summary>
        private static void Centre(TileData tile, in BoudhaFrame f, ref double u, ref double v, double du, double dv)
        {
            SacredDrawnRoads roads = SacredDrawnRoads.For(tile);
            double xu = dv, xv = -du, lo = 0, hi = 0;
            bool any = false;
            for (int q = -1; q <= 1; q++)
            {
                double zu = u + du * 0.6 * q, zv = v + dv * 0.6 * q;
                if (DrawnAt(roads, f, zu, zv) >= 0) continue;
                double a = 0, b = 0;
                while (a > -6 && DrawnAt(roads, f, zu + xu * (a - 0.05), zv + xv * (a - 0.05)) < 0) a -= 0.05;
                while (b < 6 && DrawnAt(roads, f, zu + xu * (b + 0.05), zv + xv * (b + 0.05)) < 0) b += 0.05;
                lo = any ? Math.Min(lo, a) : a;
                hi = any ? Math.Max(hi, b) : b;
                any = true;
            }
            if (!any) return;
            double mid = 0.5 * (lo + hi);
            u += xu * mid;
            v += xv * mid;
        }

        private static double DrawnAt(SacredDrawnRoads roads, in BoudhaFrame f, double u, double v)
        {
            double x, z;
            f.ToTile(u, v, out x, out z);
            return roads.SignedDistance(x, z);
        }

        public static void Build(MeshData m, GenColliders c, TileData tile, in BoudhaFrame f, int lod)
        {
            int v0 = m.VertexCount, i0 = m.IndexCount;
            double u, v, du, dv;
            Place(tile, f, out u, out v, out du, out dv);
            Affine3 g = BoudhaKit.Facing(f.Site, u, 0, v, du, dv);
            double px0 = HalfOpen, px1 = HalfOpen + PierW, hd = 0.55;
            ShapeBrush white = BoudhaKit.White, teal = BoudhaKit.B(BoudhaKit.Teal, MaterialChannel.Paint), yellow = BoudhaKit.B(MeshColor.FromHex(0xD9B53A), MaterialChannel.Gilt);
            ShapeBrush roof = BoudhaKit.B(BoudhaKit.TibetanYellow, MaterialChannel.Paint), blue = BoudhaKit.B(MeshColor.FromHex(0x2C5A7A), MaterialChannel.Paint);
            ShapeBrush gilt = BoudhaKit.Gilt;
            if (lod >= 3)
            {
                for (int s = -1; s <= 1; s += 2) SacredDraw.Box(m, g, s > 0 ? px0 : -px1, s > 0 ? px1 : -px0, -0.3, EaveY, -hd, hd, teal, BoxFaces.All & ~BoxFaces.Bottom);
                SacredDraw.Box(m, g, -px1 - 0.7, px1 + 0.7, EaveY, RoofY, -1.2, 1.2, roof, BoxFaces.All);
                Shapes.Cone(m, SacredDraw.At(g, 0, RoofY, 0), white, 0.4, Top - RoofY, 4, 0, 0, false);
                SacredDraw.Bake(m, v0, i0, f.Level);
                return;
            }
            // Piers: teal faces framed in yellow, a white base and a moulded capital.
            for (int s = -1; s <= 1; s += 2)
            {
                double xa = s > 0 ? px0 : -px1, xb = s > 0 ? px1 : -px0, xc = 0.5 * (xa + xb);
                Mould mo = SacredDraw.M;
                mo.Add(0.06, -0.3, white).Add(0.06, 0.45).Add(0, 0.5, yellow).Add(0, FriezeY - 0.25).Add(0.05, FriezeY - 0.2, gilt).Add(0.05, FriezeY);
                SacredDraw.RectRing(m, g, xc, 0, 0.5 * PierW, hd, mo, false, yellow);
                if (lod <= 1)
                {
                    // Two teal panels per face (front and back), two of the eight auspicious symbols stacked on each.
                    for (int face = 0; face < 2; face++)
                    {
                        double z = face == 0 ? hd + 0.01 : -hd - 0.01;
                        Affine3 fz = face == 0 ? g : SacredDraw.Turn(g, 0, 0, 0, Math.PI);
                        double x = face == 0 ? xc : -xc;
                        for (int p = 0; p < 2; p++)
                        {
                            double ya = 0.75 + p * 2.45, yb = ya + 2.2;
                            SacredDraw.Panel(m, fz, x - 0.5 * PierW + 0.13, ya, x + 0.5 * PierW - 0.13, yb, Math.Abs(z), teal);
                            if (lod == 0)
                                for (int k = 0; k < 2; k++)
                                    Symbol(m, fz, x, ya + 0.55 + 1.1 * k, Math.Abs(z) + 0.01, (s + 1) * 2 + 2 * p + k + face * 3, 0.55);
                        }
                    }
                }
            }
            // The arch wall: spandrels over a cusped ogee opening, between the piers.
            ArchWall(m, g, blue, gilt, lod);
            // Frieze over the whole gate (commons gate photos): a yellow-gilt strip, a teal band of cloud medallions, the
            // blue cornice band with the dark sign panel in its middle, and a row of small yellow corbels under the eave.
            double fx = px1 + 0.15;
            SacredDraw.Box(m, g, -fx, fx, FriezeY, FriezeY + 0.1, -hd - 0.06, hd + 0.06, yellow, BoxFaces.All & ~BoxFaces.Bottom);
            SacredDraw.Box(m, g, -fx, fx, FriezeY + 0.1, FriezeY + 0.36, -hd - 0.08, hd + 0.08, BoudhaKit.B(MeshColor.FromHex(0x2E6F78), MaterialChannel.Paint), BoxFaces.All & ~BoxFaces.Bottom);
            SacredDraw.Box(m, g, -fx - 0.08, fx + 0.08, FriezeY + 0.36, EaveY, -hd - 0.14, hd + 0.14, BoudhaKit.B(MeshColor.FromHex(0x2E5DA8), MaterialChannel.Paint), BoxFaces.All & ~BoxFaces.Bottom);
            if (lod <= 1)
            {
                SacredDraw.Panel(m, g, -1.3, FriezeY + 0.4, 1.3, EaveY - 0.08, hd + 0.145, BoudhaKit.B(MeshColor.FromHex(0x2A2622), MaterialChannel.Paint));
                if (lod == 0)
                {
                    for (int q = 0; q < 14; q++)
                    {
                        double x = -fx + 0.2 + (2 * fx - 0.4) * q / 13.0;
                        Shapes.Cylinder(m, SacredDraw.Basis(g, x, FriezeY + 0.23, hd + 0.08, 1, 0, 0, 0, 0, 1), q % 2 == 0 ? gilt : BoudhaKit.B(MeshColor.FromHex(0xEDEFE8), MaterialChannel.Paint),
                                        0.09, 0.02, 8, 0, 0, false, true);
                        if (Math.Abs(x) > 1.35)
                            SacredDraw.Box(m, g, x - 0.08, x + 0.08, EaveY - 0.18, EaveY, hd + 0.14, hd + 0.24, yellow, BoxFaces.Front | BoxFaces.Bottom | BoxFaces.Left | BoxFaces.Right);
                    }
                }
            }
            // The broad yellow Tibetan roof with a moulded edge and upturned corners.
            Roof(m, g, fx + 0.6, 1.35, roof, lod);
            // Crest: the white chorten with its gilt lotus finial between two gilt dragons; kalash at the ends.
            Chorten(m, g, 0, RoofY, 0, lod);
            if (lod <= 1)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    Dragon(m, g, s, lod);
                    Kalash(m, g, s * (fx + 0.3), RoofY - 0.12, 0, lod);
                }
            }
            // Snow lions on their columns outside the piers: the lions sit at about the arch's springing (gate/commons).
            for (int s = -1; s <= 1; s += 2)
            {
                double x = s * (px1 + 0.45), ch = LionColumnH;
                Shapes.Cylinder(m, SacredDraw.At(g, x, -0.3, 0.1), white, 0.34, ch + 0.3, lod == 0 ? 14 : 8, 0.04, 1, false, true);
                Profile2 lotus = BoudhaKit.Prof;
                lotus.Add(0, ch - 0.3).Add(0.36, ch - 0.3, true).Add(0.45, ch - 0.18).Add(0.5, ch - 0.05).Add(0.38, ch, true).Add(0, ch);
                Shapes.Lathe(m, SacredDraw.At(g, x, 0, 0.1), BoudhaKit.B(MeshColor.FromHex(0x3A7FC0), MaterialChannel.Paint), lotus, lod == 0 ? 14 : 8);
                SacredFigures.Guardian(m, g, x, ch, 0.1, 1.15, GuardianKind.Lion, true, lod == 0 ? SacredFigures.Hero : 2, lod, s);
                if (c != null)
                {
                    double tx, ty, tz;
                    g.Point(x, 0, 0.1, out tx, out ty, out tz);
                    c.AddCylinder(tx, tz, f.Level - 0.3, f.Level + ch + 1.2, 0.5);
                }
            }
            // Steel bollards just inside.
            if (lod <= 1)
            {
                ShapeBrush steel = BoudhaKit.B(MeshColor.FromHex(0xB8BCC2), MaterialChannel.Metal);
                // Drawn only: the lane is a drawn road, and its bollards let walkers and riders through (decision 1).
                for (int q = -1; q <= 1; q++) Shapes.Cylinder(m, SacredDraw.At(g, q * 1.3, 0, -2.2), steel, 0.07, 0.9, 8, 0.03, 1, false, true);
            }
            if (c != null)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    double tx, ty, tz, ux, uy, uz;
                    g.Point(s * 0.5 * (px0 + px1), 0, 0, out tx, out ty, out tz);
                    g.Vector(1, 0, 0, out ux, out uy, out uz);
                    c.AddBox(tx, tz, f.Level - 0.3, f.Level + EaveY, 0.5 * PierW, hd, ux, uz, GenColliderFlags.NoClimb, GenColliders.Stone);
                }
            }
            SacredDraw.Bake(m, v0, i0, f.Level);
        }

        /// <summary>The wall over the arch (0.5 m thick): a cusped ogee opening (lobed jambs rising to a pointed apex),
        /// the spandrels painted blue with gold and teal clouds and dragons, a gilt edge along the arch.</summary>
        private static void ArchWall(MeshData m, in Affine3 g, in ShapeBrush blue, in ShapeBrush gilt, int lod)
        {
            double[] x = BoudhaKit.U2, z = BoudhaKit.V2;
            int seg = lod == 0 ? 14 : lod == 1 ? 8 : 4, n = 0;
            // The outline in (across, −up) for a frame whose local z points down (see SacredDraw.Basis below).
            x[n] = -HalfOpen;
            z[n++] = -FriezeY;
            x[n] = -HalfOpen;
            z[n++] = -Spring;
            for (int i = 1; i < seg; i++)
            {
                double t = (double)i / seg;
                double ax, ay;
                ArchPoint(t, out ax, out ay);
                x[n] = ax;
                z[n++] = -ay;
            }
            x[n] = HalfOpen;
            z[n++] = -Spring;
            x[n] = HalfOpen;
            z[n++] = -FriezeY;
            Affine3 af = SacredDraw.Basis(g, 0, 0, -0.25, 1, 0, 0, 0, 0, 1);
            Shapes.BevelExtrude(m, af, blue, x, z, n, 0.5, 0.0, 1, BevelStyle.Chamfer, true, true, 0, 20);
            if (lod >= 2) return;
            // The gilt edge along the arch, both faces.
            for (int face = -1; face <= 1; face += 2)
            {
                double zz = face * 0.26;
                double lx = -HalfOpen, ly = Spring;
                for (int i = 1; i <= seg; i++)
                {
                    double ax, ay;
                    ArchPoint((double)i / seg, out ax, out ay);
                    if (i == seg)
                    {
                        ax = HalfOpen;
                        ay = Spring;
                    }
                    Shapes.Bar(m, g, gilt, lx, ly, zz, ax, ay, zz, 0.07, 4);
                    lx = ax;
                    ly = ay;
                }
            }
            // The carved archivolt: a band of colourful relief blocks (gold, teal, red, green, sky blue) round the arch and
            // down the jambs (commons/Boudhanath_entrance_gate).
            uint[] relief = { 0x2E5D6E, 0xD9B53A, 0x2C4F7A, 0x3E9C94, 0xD9B53A, 0x2E5D6E, 0x5FA8D8, 0xC8382E };
            for (int face = -1; face <= 1; face += 2)
            {
                Affine3 ff = face > 0 ? g : SacredDraw.Turn(g, 0, 0, 0, Math.PI);
                int k = 0;
                for (int s = -1; s <= 1; s += 2)
                {
                    // Jamb blocks from the column base to the springing.
                    int rows = lod == 0 ? 3 : 1;
                    for (int r = 0; r < rows; r++)
                    {
                        double ya = 1.0 + (Spring - 1.0) * r / rows, yb = 1.0 + (Spring - 1.0) * (r + 1) / rows;
                        double xa = s * (HalfOpen + 0.12), xb = s * (HalfOpen + 0.5);
                        SacredDraw.Quad(m, ff, Math.Min(xa, xb), ya, 0.265, Math.Max(xa, xb), ya, 0.265, Math.Max(xa, xb), yb, 0.265, Math.Min(xa, xb), yb, 0.265, 0, 0, 1,
                                        BoudhaKit.B(MeshColor.FromHex(relief[(k++ + r) % relief.Length]), MaterialChannel.Paint));
                    }
                }
                double px, py;
                ArchPoint(0, out px, out py);
                for (int i = 1; i <= seg; i++)
                {
                    double qx, qy;
                    ArchPoint((double)i / seg, out qx, out qy);
                    double tx = qx - px, ty = qy - py, tl = Math.Max(1e-6, Math.Sqrt(tx * tx + ty * ty)), nx = -ty / tl, ny = tx / tl;
                    // The band 0.12-0.5 m outside the opening, one block per arch segment.
                    SacredDraw.Quad(m, ff, px + 0.12 * nx, py + 0.12 * ny, 0.265, qx + 0.12 * nx, qy + 0.12 * ny, 0.265, qx + 0.5 * nx, qy + 0.5 * ny, 0.265,
                                    px + 0.5 * nx, py + 0.5 * ny, 0.265, 0, 0, 1, BoudhaKit.B(MeshColor.FromHex(relief[(i + face + 5) % relief.Length]), MaterialChannel.Paint));
                    px = qx;
                    py = qy;
                }
            }
            if (lod == 0)
            {
                // The carved and painted spandrels (commons/Boudhanath_stupa_entrance_gate): a teal dragon either side
                // writhing down along the archivolt from a flaming jewel over the apex, with teal fins, and white, teal and
                // gold cloud scrolls filling the corners; the lane face (z < 0) has the dragons only.
                ShapeBrush gold = BoudhaKit.B(MeshColor.FromHex(0xE2B54A), MaterialChannel.Gilt), tealL = BoudhaKit.B(MeshColor.FromHex(0x3E9C94), MaterialChannel.Paint);
                ShapeBrush cloud = BoudhaKit.B(MeshColor.FromHex(0xEDEFE8), MaterialChannel.Paint), red = BoudhaKit.B(BoudhaKit.ClothRed, MaterialChannel.Paint);
                ShapeBrush dragon = BoudhaKit.B(MeshColor.FromHex(0x2F8580), MaterialChannel.Paint);
                for (int face = -1; face <= 1; face += 2)
                {
                    Affine3 ff = face > 0 ? g : SacredDraw.Turn(g, 0, 0, 0, Math.PI);
                    // The flaming jewel over the apex.
                    Shapes.Cylinder(m, SacredDraw.Basis(ff, 0, Apex + 0.45, 0.26, 1, 0, 0, 0, 0, 1), red, 0.17, 0.03, 10, 0, 0, false, true);
                    Shapes.Cone(m, SacredDraw.At(ff, 0, Apex + 0.58, 0.28), gold, 0.1, 0.3, 5, 0, 0, false);
                    for (int sd = -1; sd <= 1; sd += 2)
                    {
                        // The dragon's body follows the carved band round the arch (0.32 m outside the opening) from the
                        // jewel down to the springing, writhing in and out of it.
                        var ctrl = new Path3(8);
                        for (int q = 0; q <= 5; q++)
                        {
                            double t = 0.5 + sd * (0.06 + 0.42 * q / 5.0), ax, ay, bx, by;
                            ArchPoint(t, out ax, out ay);
                            ArchPoint(Math.Max(0, Math.Min(1, t + 0.01)), out bx, out by);
                            double tx = bx - ax, ty = by - ay, tl = Math.Max(1e-6, Math.Sqrt(tx * tx + ty * ty)), nx = ty / tl, ny = -tx / tl;
                            if (nx * ax + ny * (ay - Spring) < 0)
                            {
                                nx = -nx;
                                ny = -ny;
                            }
                            double off = 0.32 + (q % 2 == 0 ? 0.08 : -0.08);
                            ctrl.Add(ax + nx * off, ay + ny * off, 0.3);
                        }
                        Path3 path = Curves.CatmullRom(ctrl, new Path3(32), 3);
                        // A teal body in the blue field (the carving reads blue-green with gold highlights), gilt scales
                        // along its back.
                        Shapes.Tube(m, ff, dragon, path, 0.065, 5, true, false, default, 0.03);
                        for (int q = 2; q < path.Count - 1; q += 3) Shapes.Sphere(m, SacredDraw.At(ff, path.X[q], path.Y[q] + 0.05, path.Z[q] + 0.04), gold, 0.035, 4);
                        // The head at the jewel: a snout, the teal mane.
                        Affine3 hd = SacredDraw.At(ff, sd * 0.36, Apex + 0.4, 0.32);
                        Shapes.Ellipsoid(m, hd, gold, 0.14, 0.11, 0.06, 6, false);
                        Shapes.Ellipsoid(m, SacredDraw.At(hd, sd * 0.12, 0.08, -0.02), tealL, 0.13, 0.12, 0.04, 5, false);
                        if (face < 0) continue;
                        // Cloud scrolls in the corner above the arch (three-quarter curls).
                        double[] cs = { HalfOpen - 0.25, FriezeY - 0.3, HalfOpen - 0.75, FriezeY - 0.25, HalfOpen - 0.2, FriezeY - 0.85, 0.75, FriezeY - 0.22 };
                        for (int q = 0; q < cs.Length; q += 2)
                        {
                            ShapeBrush cb = q % 4 == 0 ? cloud : q == 2 ? tealL : gold;
                            Shapes.Torus(m, SacredDraw.Basis(ff, sd * cs[q], cs[q + 1], 0.29, 1, 0, 0, 0, 0, 1), cb, 0.12, 0.028, 8, 3, default, sd > 0 ? 30 : 60, 270, false);
                        }
                    }
                }
            }
        }

        /// <summary>A point of the arch at t ∈ [0, 1] from the left springing over the apex to the right one: round-headed,
        /// a touch taller than a semicircle.</summary>
        private static void ArchPoint(double t, out double x, out double y)
        {
            double s = 2 * t - 1, a = Math.Abs(s);
            // A round-headed arch, a touch taller than a semicircle with the slightest point at the apex (the gate photos).
            double ang = (1 - a) * 0.5 * Math.PI;
            double bx = HalfOpen * Math.Cos(ang), by = (Apex - Spring) * Math.Pow(Math.Sin(ang), 0.92);
            x = Math.Sign(s) * bx;
            y = Spring + by;
        }

        /// <summary>One of the eight auspicious symbols on a pier panel (index picks it): conch, endless knot, wheel,
        /// lotus, parasol, golden fish, victory banner, treasure vase, as bold coloured shapes.</summary>
        private static void Symbol(MeshData m, in Affine3 g, double x, double y, double z, int index, double s)
        {
            ShapeBrush gold = BoudhaKit.Gilt, white = BoudhaKit.B(MeshColor.FromHex(0xF4F1EA), MaterialChannel.Paint), red = BoudhaKit.B(BoudhaKit.ClothRed, MaterialChannel.Paint);
            ShapeBrush pink = BoudhaKit.B(MeshColor.FromHex(0xE88AA0), MaterialChannel.Paint), green = BoudhaKit.B(MeshColor.FromHex(0x3E9C5A), MaterialChannel.Paint);
            Affine3 f = SacredDraw.Basis(g, x, y, z, 1, 0, 0, 0, 0, 1);
            switch (index % 8)
            {
                case 0: // conch: a white spiral shell
                    Shapes.Ellipsoid(m, SacredDraw.At(f, 0, 0.02, 0), white, 0.45 * s, 0.03, 0.65 * s, 8, false);
                    Shapes.Cylinder(m, SacredDraw.At(f, 0, 0.03, -0.2 * s), red, 0.12 * s, 0.02, 8, 0, 0, false, true);
                    break;
                case 1: // endless knot: interlaced gold loops
                    for (int q = 0; q < 4; q++)
                        Shapes.Torus(m, SacredDraw.At(f, (q % 2 == 0 ? -1 : 1) * 0.18 * s, 0.03, (q < 2 ? -1 : 1) * 0.18 * s), gold, 0.2 * s, 0.03, 8, 3);
                    break;
                case 2: // wheel
                    Shapes.Torus(m, SacredDraw.At(f, 0, 0.03, 0), gold, 0.42 * s, 0.05, 14, 3);
                    for (int q = 0; q < 4; q++)
                    {
                        double a = Math.PI * q / 4;
                        Shapes.Bar(m, f, gold, -0.4 * s * Math.Cos(a), 0.03, -0.4 * s * Math.Sin(a), 0.4 * s * Math.Cos(a), 0.03, 0.4 * s * Math.Sin(a), 0.03, 3);
                    }
                    break;
                case 3: // lotus
                    for (int q = 0; q < 5; q++)
                    {
                        double a = Math.PI * (q - 2) / 6;
                        Shapes.Ellipsoid(m, SacredDraw.At(f, 0.28 * s * Math.Sin(a), 0.03, -0.1 * s - 0.28 * s * Math.Cos(a)), pink, 0.13 * s, 0.03, 0.28 * s, 6, false);
                    }
                    break;
                case 4: // parasol
                    Shapes.Ellipsoid(m, SacredDraw.At(f, 0, 0.03, -0.25 * s), gold, 0.5 * s, 0.03, 0.22 * s, 8, false);
                    Shapes.Bar(m, f, red, 0, 0.03, -0.1 * s, 0, 0.03, 0.55 * s, 0.04, 3);
                    break;
                case 5: // golden fish
                    for (int q = -1; q <= 1; q += 2) Shapes.Ellipsoid(m, SacredDraw.At(f, q * 0.16 * s, 0.03, 0), gold, 0.12 * s, 0.03, 0.48 * s, 8, false);
                    break;
                case 6: // victory banner
                    Shapes.Ellipsoid(m, SacredDraw.At(f, 0, 0.03, 0), red, 0.2 * s, 0.03, 0.55 * s, 8, false);
                    Shapes.Ellipsoid(m, SacredDraw.At(f, 0, 0.04, -0.55 * s), gold, 0.16 * s, 0.03, 0.1 * s, 6, false);
                    break;
                default: // treasure vase
                    Shapes.Ellipsoid(m, SacredDraw.At(f, 0, 0.03, 0.12 * s), gold, 0.36 * s, 0.03, 0.42 * s, 8, false);
                    Shapes.Ellipsoid(m, SacredDraw.At(f, 0, 0.04, -0.42 * s), green, 0.2 * s, 0.03, 0.14 * s, 6, false);
                    break;
            }
        }

        /// <summary>The yellow Tibetan roof: an eave slab with a moulded, slightly upturned edge and a shallow hipped top.</summary>
        private static void Roof(MeshData m, in Affine3 g, double hx, double hz, in ShapeBrush roof, int lod)
        {
            ShapeBrush under = BoudhaKit.B(MeshColor.FromHex(0xC2A42E), MaterialChannel.Paint, 0.7f), edge = BoudhaKit.B(MeshColor.FromHex(0xEAD35A), MaterialChannel.Paint);
            double y0 = EaveY, y1 = RoofY, kick = lod == 0 ? 0.18 : 0.0;
            // The eave rim (moulded band) and the underside.
            Mould mo = SacredDraw.M;
            mo.Add(-0.25, y0, under).Add(0, y0 + 0.05, edge).Add(0.06, y0 + 0.2).Add(0, y0 + 0.32, roof);
            SacredDraw.RectRing(m, g, 0, 0, hx, hz, mo, false, roof);
            SacredDraw.CapRect(m, g, 0, 0, hx - 0.25, hz - 0.25, y0, false, under);
            // Shallow hipped top: corners lifted by the kick.
            double ry = y1 + 0.25, rh = Math.Max(0, hx - hz);
            for (int s = -1; s <= 1; s += 2)
            {
                SacredDraw.Quad(m, g, -hx, y0 + 0.32 + kick, s * hz, hx, y0 + 0.32 + kick, s * hz, rh, ry, 0, -rh, ry, 0, 0, 0.95, s * 0.3, roof);
                SacredDraw.Tri(m, g, s * hx, y0 + 0.32 + kick, -hz, s * hx, y0 + 0.32 + kick, hz, s * rh, ry, 0, s * 0.3, 0.95, 0, roof);
            }
            if (kick > 0)
            {
                // The corner horns that curl up the roof's edge.
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        Shapes.Bar(m, g, edge, sx * (hx - 0.4), y0 + 0.3, sz * (hz - 0.3), sx * (hx + 0.1), y0 + 0.62, sz * (hz + 0.12), 0.07, 4);
            }
        }

        /// <summary>The white bulb chorten on the crest with its gilt lotus finial (top at <see cref="Top"/>).</summary>
        private static void Chorten(MeshData m, in Affine3 g, double x, double y, double z, int lod)
        {
            ShapeBrush white = BoudhaKit.White, gilt = BoudhaKit.Gilt;
            Mould mo = SacredDraw.M;
            mo.Add(0, y - 0.05, white).Add(0, y + 0.12).Add(-0.08, y + 0.14).Add(-0.08, y + 0.25);
            SacredDraw.RectRing(m, g, x, z, 0.42, 0.42, mo, true, white);
            Profile2 p = BoudhaKit.Prof;
            double b = y + 0.25;
            p.Add(0, b).Add(0.3, b, true).Add(0.42, b + 0.18).Add(0.44, b + 0.38).Add(0.36, b + 0.6).Add(0.16, b + 0.72).Add(0, b + 0.75);
            if (lod == 0) p.Smooth(1);
            Shapes.Lathe(m, SacredDraw.At(g, x, 0, z), white, p, lod == 0 ? 14 : 8);
            SacredDraw.Box(m, g, x - 0.14, x + 0.14, b + 0.7, b + 0.85, z - 0.14, z + 0.14, white, BoxFaces.All & ~BoxFaces.Bottom);
            Profile2 q = BoudhaKit.Prof;
            double fb = b + 0.85;
            q.Add(0, fb).Add(0.12, fb, true).Add(0.17, fb + 0.08).Add(0.1, fb + 0.16).Add(0.13, fb + 0.24).Add(0.05, fb + 0.36).Add(0.02, fb + 0.45).Add(0, Top);
            if (lod == 0) q.Smooth(1);
            Shapes.Lathe(m, SacredDraw.At(g, x, 0, z), gilt, q, lod == 0 ? 10 : 6);
        }

        /// <summary>A gilt dragon on the roof crest (side s), its sinuous body rising from the roof toward the chorten,
        /// head raised with horns and an open jaw.</summary>
        private static void Dragon(MeshData m, in Affine3 g, int s, int lod)
        {
            ShapeBrush gilt = BoudhaKit.Gilt, teal = BoudhaKit.B(MeshColor.FromHex(0x3E9C94), MaterialChannel.Paint);
            var ctrl = new Path3(8);
            double y = RoofY + 0.25;
            // The commons photos: big gilt dragons whose humps rise as high as the chorten's bulb, heads raised to it.
            ctrl.Add(s * 2.6, y + 0.05, 0.1).Add(s * 2.15, y + 0.62, -0.1).Add(s * 1.65, y + 0.18, 0.1).Add(s * 1.2, y + 0.68, 0.0).Add(s * 0.86, y + 0.8, 0.0);
            Path3 path = lod == 0 ? Curves.CatmullRom(ctrl, new Path3(32), 4) : ctrl;
            Shapes.Tube(m, g, gilt, path, 0.2, lod == 0 ? 8 : 5, true, false, default, 0.07);
            if (lod == 0)
            {
                // Teal dorsal fins along the humps and two clawed legs gripping the roof.
                double[] fin = { 2.15, 0.84, 1.65, 0.38, 1.2, 0.9 };
                for (int q = 0; q < fin.Length; q += 2)
                    Shapes.Cone(m, SacredDraw.At(g, s * fin[q], y + fin[q + 1], 0), teal, 0.09, 0.22, 5, 0, 0, false);
                for (int q = 0; q < 2; q++)
                {
                    double lx = s * (q == 0 ? 2.25 : 1.3);
                    Shapes.Bar(m, g, gilt, lx, y + (q == 0 ? 0.45 : 0.3), 0.05, lx + s * 0.12, y - 0.2, 0.18, 0.06, 4);
                }
            }
            // Head: a snout and jaw, horns, a teal crest.
            Affine3 h = SacredDraw.At(g, s * 0.7, y + 0.92, 0) * Affine3.Scaling(1.3);
            Shapes.Ellipsoid(m, h, gilt, 0.2, 0.16, 0.16, lod == 0 ? 8 : 5, false);
            Shapes.Bar(m, h, gilt, -s * 0.05, 0.02, 0, -s * 0.38, 0.1, 0, 0.08, 5);
            if (lod == 0)
            {
                Shapes.Bar(m, h, gilt, -s * 0.05, -0.08, 0, -s * 0.3, -0.12, 0, 0.05, 4);
                for (int q = -1; q <= 1; q += 2) Shapes.Bar(m, h, gilt, s * 0.05, 0.12, q * 0.06, s * 0.28, 0.35, q * 0.12, 0.025, 3);
                Shapes.Ellipsoid(m, SacredDraw.At(h, s * 0.12, 0.08, 0), teal, 0.12, 0.14, 0.05, 6, false);
            }
        }

        /// <summary>A small gilt kalash (vase) finial at a roof end.</summary>
        private static void Kalash(MeshData m, in Affine3 g, double x, double y, double z, int lod)
        {
            Profile2 p = BoudhaKit.Prof;
            p.Add(0, y).Add(0.1, y, true).Add(0.16, y + 0.12).Add(0.12, y + 0.24).Add(0.05, y + 0.3).Add(0.08, y + 0.36).Add(0, y + 0.48);
            if (lod == 0) p.Smooth(1);
            Shapes.Lathe(m, SacredDraw.At(g, x, 0, z), BoudhaKit.Gilt, p, lod == 0 ? 8 : 5);
        }
    }
}
