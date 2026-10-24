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
    /// <item>two piers whose faces carry the eight auspicious symbols, each in a raised gilt ring on a deep navy
    /// (#1E2F5A) panel in a yellow-gilt frame;</item>
    /// <item>a Tibetan cusped arch: the jambs curl in at the springing and rise in two lobes a side to a pointed apex,
    /// edged in a thick gilt roll, its spandrels carved with dragons and clouds in blue, teal and gold;</item>
    /// <item>a painted frieze with a dark sign panel over a row of blue and yellow bracket blocks, and the broad yellow
    /// Tibetan roof whose corners kick up half a metre on curved hips;</item>
    /// <item>on the crest a white bulb chorten under a 13-ring gilt spire with its parasol and finial, between two gilt
    /// dragons, kalash finials at the roof ends;</item>
    /// <item>outside the piers two white snow lions with turquoise manes on lotus pedestals atop white round columns;
    /// steel bollards inside.</item>
    /// </list>
    /// It stands where the lane meets the road's kerb (the 2019-2022 photos: the gate on the road frontage, an ATM booth
    /// against its east pier), across the lane.
    /// </summary>
    internal static class BoudhaGate
    {
        /// <summary>Crest finial tip above the ground (m).</summary>
        public const double Top = 9.65;

        /// <summary>Height of the white columns the snow lions sit on (m; the lion's base, at about the arch's springing).</summary>
        public const double LionColumnH = 2.7;

        /// <summary>The lane to the stupa (OSM) and the gate's distance back from the lane's end on the road's centre line.</summary>
        public const ulong LaneWay = 4847306;

        public const double BackFromRoadM = 8.6;

        /// <summary>Half the clear opening, the pier width, the springing and apex of the arch, the frieze and the eave
        /// (commons gate photos scaled by the 7.5 m width: springing about 3.0 m, apex 5.0 m, frieze 5.9 m).</summary>
        public const double HalfOpen = 2.35, PierW = 1.4, Spring = 2.95, Apex = 5.1, FriezeY = 5.9, EaveY = 6.7, RoofY = 7.25;

        /// <summary>How far the cusps stand in from the arch line (the lobes between them, about 0.3 m radius) and the
        /// share of each side's arch the two lobes take, from the springing.</summary>
        private const double CuspIn = 0.28, LobeShare = 0.42;

        /// <summary>The navy of the pier panels and the medallions (commons gate photos).</summary>
        private static readonly uint Navy = MeshColor.FromHex(0x1E2F5A);

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
            ShapeBrush white = BoudhaKit.White, navy = BoudhaKit.B(Navy, MaterialChannel.Paint), yellow = BoudhaKit.B(MeshColor.FromHex(0xD9B53A), MaterialChannel.Gilt);
            ShapeBrush roof = BoudhaKit.B(BoudhaKit.TibetanYellow, MaterialChannel.Paint), blue = BoudhaKit.B(MeshColor.FromHex(0x2C5A7A), MaterialChannel.Paint);
            ShapeBrush gilt = BoudhaKit.Gilt;
            if (lod >= 3)
            {
                // Far: the navy piers, the arch wall with its cusped opening, the frieze, the roof with its kicked corners
                // and the white crest chorten with its gilt spire, the lions' columns.
                for (int s = -1; s <= 1; s += 2)
                {
                    SacredDraw.Box(m, g, s > 0 ? px0 : -px1, s > 0 ? px1 : -px0, -0.3, FriezeY, -hd, hd, navy, BoxFaces.All & ~BoxFaces.Bottom);
                    Shapes.Cylinder(m, SacredDraw.At(g, s * (px1 + 0.45), -0.3, 0.1), white, 0.4, LionColumnH + 1.2, 4, 0, 0, false, true);
                }
                ArchWall(m, g, blue, gilt, 2);
                SacredDraw.Box(m, g, -px1 - 0.15, px1 + 0.15, FriezeY, EaveY, -hd - 0.1, hd + 0.1, blue, BoxFaces.All & ~BoxFaces.Bottom);
                Roof(m, g, px1 + 0.75, 1.35, roof, 2);
                Shapes.Cone(m, SacredDraw.At(g, 0, RoofY, 0), white, 0.45, 1.0, 4, 0, 0, false);
                Shapes.Cone(m, SacredDraw.At(g, 0, RoofY + 0.9, 0), gilt, 0.16, Top - RoofY - 0.9, 4, 0, 0, false);
                SacredDraw.Bake(m, v0, i0, f.Level);
                return;
            }
            // Piers: navy faces framed in yellow, a white base and a moulded capital.
            for (int s = -1; s <= 1; s += 2)
            {
                double xa = s > 0 ? px0 : -px1, xb = s > 0 ? px1 : -px0, xc = 0.5 * (xa + xb);
                Mould mo = SacredDraw.M;
                mo.Add(0.06, -0.3, white).Add(0.06, 0.45).Add(0, 0.5, yellow).Add(0, FriezeY - 0.25).Add(0.05, FriezeY - 0.2, gilt).Add(0.05, FriezeY);
                SacredDraw.RectRing(m, g, xc, 0, 0.5 * PierW, hd, mo, false, yellow);
                if (lod <= 1)
                {
                    // Two navy panels per face (front and back), two of the eight auspicious symbols stacked on each, each
                    // in a raised gilt ring (the road face carved; the lane face and LOD1 keep the rings).
                    for (int face = 0; face < 2; face++)
                    {
                        double z = face == 0 ? hd + 0.01 : -hd - 0.01;
                        Affine3 fz = face == 0 ? g : SacredDraw.Turn(g, 0, 0, 0, Math.PI);
                        double x = face == 0 ? xc : -xc;
                        for (int p = 0; p < 2; p++)
                        {
                            double ya = 0.75 + p * 2.45, yb = ya + 2.2;
                            SacredDraw.Panel(m, fz, x - 0.5 * PierW + 0.13, ya, x + 0.5 * PierW - 0.13, yb, Math.Abs(z), navy);
                            for (int k = 0; k < 2; k++)
                                if (lod == 0 || face == 0)
                                    Medallion(m, fz, x, ya + 0.55 + 1.1 * k, Math.Abs(z) + 0.005, (s + 1) * 2 + 2 * p + k + face * 3, lod == 0 && face == 0 ? 0 : 1);
                        }
                    }
                }
            }
            // The arch wall: spandrels over the cusped opening, between the piers.
            ArchWall(m, g, blue, gilt, lod);
            // Frieze over the whole gate (commons gate photos): a yellow-gilt strip, a teal band of cloud medallions, the
            // blue cornice band with the dark sign panel in its middle, and a row of blue and yellow bracket blocks under
            // the eave.
            double fx = px1 + 0.15;
            SacredDraw.Box(m, g, -fx, fx, FriezeY, FriezeY + 0.1, -hd - 0.06, hd + 0.06, yellow, BoxFaces.All & ~BoxFaces.Bottom);
            SacredDraw.Box(m, g, -fx, fx, FriezeY + 0.1, FriezeY + 0.36, -hd - 0.08, hd + 0.08, BoudhaKit.B(MeshColor.FromHex(0x2E6F78), MaterialChannel.Paint), BoxFaces.All & ~BoxFaces.Bottom);
            SacredDraw.Box(m, g, -fx - 0.08, fx + 0.08, FriezeY + 0.36, EaveY, -hd - 0.14, hd + 0.14, BoudhaKit.B(MeshColor.FromHex(0x2E5DA8), MaterialChannel.Paint), BoxFaces.All & ~BoxFaces.Bottom);
            if (lod <= 1)
            {
                SacredDraw.Panel(m, g, -1.3, FriezeY + 0.4, 1.3, EaveY - 0.3, hd + 0.145, BoudhaKit.B(MeshColor.FromHex(0x2A2622), MaterialChannel.Paint));
                if (lod == 0)
                {
                    for (int q = 0; q < 14; q++)
                    {
                        double x = -fx + 0.2 + (2 * fx - 0.4) * q / 13.0;
                        Shapes.Cylinder(m, SacredDraw.Basis(g, x, FriezeY + 0.23, hd + 0.08, 1, 0, 0, 0, 0, 1), q % 2 == 0 ? gilt : BoudhaKit.B(MeshColor.FromHex(0xEDEFE8), MaterialChannel.Paint),
                                        0.09, 0.02, 6, 0, 0, false, true);
                    }
                }
                // The bracket row: 0.25 m blocks, blue and yellow by turns, along both faces under the eave.
                ShapeBrush bb = BoudhaKit.B(MeshColor.FromHex(0x2C4F9A), MaterialChannel.Paint);
                for (int face = -1; face <= 1; face += 2)
                {
                    if (lod > 0 && face < 0) continue;
                    int nb = lod == 0 && face > 0 ? 24 : 12;
                    for (int q = 0; q < nb; q++)
                    {
                        double x = -fx - 0.02 + (2 * fx + 0.04) * (q + 0.5) / nb, z0 = face * (hd + 0.14), z1 = face * (hd + 0.14 + 0.2);
                        const double w = 0.125;
                        SacredDraw.Box(m, g, x - w, x + w, EaveY - 0.26, EaveY, Math.Min(z0, z1), Math.Max(z0, z1), q % 2 == 0 ? bb : yellow,
                                       (face > 0 ? BoxFaces.Front : BoxFaces.Back) | BoxFaces.Bottom | BoxFaces.Left | BoxFaces.Right);
                    }
                }
            }
            // The broad yellow Tibetan roof, its corners kicked up on curved hips.
            Roof(m, g, fx + 0.6, 1.35, roof, lod);
            // Crest: the white chorten with its 13-ring gilt spire between two gilt dragons; kalash at the ends.
            Chorten(m, g, 0, RoofY, 0, lod);
            if (lod <= 1)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    Dragon(m, g, s, lod);
                    Kalash(m, g, s * (fx + 0.3), RoofY - 0.12 + RoofKick, 0, lod);
                }
            }
            // Snow lions on their columns outside the piers: the lions sit at about the arch's springing (gate/commons).
            for (int s = -1; s <= 1; s += 2)
            {
                double x = s * (px1 + 0.45), ch = LionColumnH;
                Shapes.Cylinder(m, SacredDraw.At(g, x, -0.3, 0.1), white, 0.34, ch + 0.3, lod == 0 ? 12 : 8, 0.04, 1, false, true);
                Profile2 lotus = BoudhaKit.Prof;
                lotus.Add(0, ch - 0.3).Add(0.36, ch - 0.3, true).Add(0.45, ch - 0.18).Add(0.5, ch - 0.05).Add(0.38, ch, true).Add(0, ch);
                Shapes.Lathe(m, SacredDraw.At(g, x, 0, 0.1), BoudhaKit.B(MeshColor.FromHex(0x3A7FC0), MaterialChannel.Paint), lotus, lod == 0 ? 12 : 8);
                SacredFigures.Guardian(m, g, x, ch, 0.1, 1.15, GuardianKind.Lion, true, SacredFigures.Generic, lod, s);
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
                for (int q = -1; q <= 1; q++) Shapes.Cylinder(m, SacredDraw.At(g, q * 1.3, 0, -2.2), steel, 0.07, 0.9, 6, 0.03, 1, false, true);
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

        /// <summary>
        /// The opening's edge from the left springing over the apex to the right one, into (x, y) (gate frame, across and
        /// up); returns the points. The jamb curls in to a cusp at the springing, two lobes a side (about 0.3 m radius,
        /// bowing back to the arch line between cusps <see cref="CuspIn"/> in from it) climb the lower
        /// <see cref="LobeShare"/> of each side, and the plain arch rises to the pointed apex.
        /// </summary>
        private static int Outline(double[] x, double[] y, int lod)
        {
            int lobeSeg = lod == 0 ? 5 : lod == 1 ? 3 : 1, archSeg = lod == 0 ? 6 : lod == 1 ? 4 : 2, n = 0;
            for (int side = -1; side <= 1; side += 2)
            {
                // Walk the left side up from the springing, the right side down to it.
                int count = 2 * lobeSeg + archSeg;
                for (int i = 0; i <= count; i++)
                {
                    int k = side < 0 ? i : count - i;
                    if (side > 0 && k == count) continue; // the apex once
                    double t, inset;
                    if (k < 2 * lobeSeg)
                    {
                        // Within a lobe: cusps at its ends, the arch line at its middle.
                        int lobe = k / lobeSeg, j = k % lobeSeg;
                        double w = (double)j / lobeSeg;
                        t = LobeShare * (lobe + w) / 2;
                        inset = CuspIn * (1 - Math.Sin(Math.PI * w));
                    }
                    else
                    {
                        double w = (double)(k - 2 * lobeSeg) / archSeg;
                        t = LobeShare + (1 - LobeShare) * w;
                        inset = k == 2 * lobeSeg ? CuspIn * 0.8 : 0;
                    }
                    // The cusps stand in from the arch line along its normal (into the opening); the right side mirrors.
                    double ax, ay, nx, ny;
                    ArchPoint(t, out ax, out ay, out nx, out ny);
                    x[n] = -side * (ax + nx * inset);
                    y[n] = ay + ny * inset;
                    n++;
                }
            }
            return n;
        }

        /// <summary>The wall over the arch (0.5 m thick): the cusped opening (<see cref="Outline"/>), the spandrels painted
        /// blue with gold and teal clouds and dragons, a thick gilt roll along the opening's edge down to the jambs.</summary>
        private static void ArchWall(MeshData m, in Affine3 g, in ShapeBrush blue, in ShapeBrush gilt, int lod)
        {
            double[] ox = BoudhaKit.U, oy = BoudhaKit.V;
            int no = Outline(ox, oy, lod);
            double[] x = BoudhaKit.U2, z = BoudhaKit.V2;
            int n = 0;
            // The wall outline in (across, −up) for a frame whose local z points down (see SacredDraw.Basis below).
            x[n] = -HalfOpen;
            z[n++] = -FriezeY;
            x[n] = -HalfOpen;
            z[n++] = -Spring;
            for (int i = 0; i < no; i++)
            {
                x[n] = ox[i];
                z[n++] = -oy[i];
            }
            x[n] = HalfOpen;
            z[n++] = -Spring;
            x[n] = HalfOpen;
            z[n++] = -FriezeY;
            Affine3 af = SacredDraw.Basis(g, 0, 0, -0.25, 1, 0, 0, 0, 0, 1);
            Shapes.BevelExtrude(m, af, blue, x, z, n, 0.5, 0.0, 1, BevelStyle.Chamfer, true, true, 0, 20);
            if (lod >= 2) return;
            // The gilt roll (0.12 m) along the edge, both faces, from the jamb's foot round the lobes and over the apex.
            var path = new Path3(64);
            for (int face = -1; face <= 1; face += 2)
            {
                // The road face round (LOD0), the lane face a three-sided roll (LOD1: the road face only).
                if (lod > 0 && face < 0) continue;
                double zz = face * 0.27;
                path.Clear();
                path.Add(-HalfOpen + 0.06, 0.55, zz).Add(-HalfOpen + 0.06, Spring - 0.04, zz);
                for (int i = 0; i < no; i++) path.Add(ox[i], oy[i], zz);
                path.Add(HalfOpen - 0.06, Spring - 0.04, zz).Add(HalfOpen - 0.06, 0.55, zz);
                Shapes.Tube(m, g, gilt, path, 0.06, lod == 0 && face > 0 ? 5 : 3, false, false);
            }
            // The carved archivolt: a band of colourful relief blocks (gold, teal, red, green, sky blue) round the arch and
            // down the jambs (commons/Boudhanath_entrance_gate).
            uint[] relief = { 0x2E5D6E, 0xD9B53A, 0x2C4F7A, 0x3E9C94, 0xD9B53A, 0x2E5D6E, 0x5FA8D8, 0xC8382E };
            int seg = lod == 0 ? 14 : 8;
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
                double px, py, pnx, pny;
                ArchPoint(0, out px, out py, out pnx, out pny);
                for (int i = 1; i <= seg; i++)
                {
                    double qx, qy, qnx, qny;
                    ArchPoint((double)i / seg, out qx, out qy, out qnx, out qny);
                    // The band 0.14-0.5 m outside the arch line, one block per segment, the right half mirrored.
                    for (int half = 0; half < 2; half++)
                    {
                        double sx = half == 0 ? 1 : -1, o0 = 0.14, o1 = 0.5;
                        double x0 = sx * (px - pnx * o0), y0 = py - pny * o0, x1 = sx * (qx - qnx * o0), y1 = qy - qny * o0;
                        double x2 = sx * (qx - qnx * o1), y2 = qy - qny * o1, x3 = sx * (px - pnx * o1), y3 = py - pny * o1;
                        SacredDraw.Quad(m, ff, x0, y0, 0.265, x1, y1, 0.265, x2, y2, 0.265, x3, y3, 0.265, 0, 0, 1,
                                        BoudhaKit.B(MeshColor.FromHex(relief[(i + face + (half == 0 ? 5 : 9)) % relief.Length]), MaterialChannel.Paint));
                    }
                    px = qx;
                    py = qy;
                    pnx = qnx;
                    pny = qny;
                }
            }
            if (lod == 0)
            {
                // The carved and painted spandrels (commons/Boudhanath_stupa_entrance_gate): a teal dragon either side
                // writhing down along the archivolt from a flaming jewel over the apex, and white, teal and gold cloud
                // scrolls filling the corners; the lane face (z < 0) has the dragons only.
                ShapeBrush gold = BoudhaKit.B(MeshColor.FromHex(0xE2B54A), MaterialChannel.Gilt), tealL = BoudhaKit.B(MeshColor.FromHex(0x3E9C94), MaterialChannel.Paint);
                ShapeBrush cloud = BoudhaKit.B(MeshColor.FromHex(0xEDEFE8), MaterialChannel.Paint), red = BoudhaKit.B(BoudhaKit.ClothRed, MaterialChannel.Paint);
                ShapeBrush dragon = BoudhaKit.B(MeshColor.FromHex(0x2F8580), MaterialChannel.Paint);
                var ctrl = new Path3(8);
                var body = new Path3(32);
                for (int face = -1; face <= 1; face += 2)
                {
                    Affine3 ff = face > 0 ? g : SacredDraw.Turn(g, 0, 0, 0, Math.PI);
                    if (face < 0) continue; // the lane face: the painted field and archivolt only
                    // The flaming jewel over the apex.
                    Shapes.Cylinder(m, SacredDraw.Basis(ff, 0, Apex + 0.42, 0.26, 1, 0, 0, 0, 0, 1), red, 0.15, 0.03, 8, 0, 0, false, true);
                    Shapes.Cone(m, SacredDraw.At(ff, 0, Apex + 0.52, 0.28), gold, 0.09, 0.26, 5, 0, 0, false);
                    for (int sd = -1; sd <= 1; sd += 2)
                    {
                        // The dragon's body follows the carved band (0.32 m outside the arch line) from the jewel down to
                        // the springing, writhing in and out of it.
                        ctrl.Clear();
                        for (int q = 0; q <= 5; q++)
                        {
                            double t = 0.06 + 0.9 * q / 5.0, ax, ay, nx, ny;
                            ArchPoint(1 - t, out ax, out ay, out nx, out ny);
                            double off = 0.32 + (q % 2 == 0 ? 0.08 : -0.08);
                            ctrl.Add(sd * -(ax - nx * off), ay - ny * off, 0.3);
                        }
                        Path3 path2 = Curves.CatmullRom(ctrl, body.Clear(), 3);
                        // A teal body in the blue field (the carving reads blue-green with gold highlights), gilt scales
                        // along its back.
                        Shapes.Tube(m, ff, dragon, path2, 0.065, 5, true, false, default, 0.03);
                        for (int q = 2; q < path2.Count - 1; q += 4) Shapes.Sphere(m, SacredDraw.At(ff, path2.X[q], path2.Y[q] + 0.05, path2.Z[q] + 0.04), gold, 0.035, 4);
                        // The head at the jewel: a snout, the teal mane.
                        Affine3 hd = SacredDraw.At(ff, sd * 0.36, Apex + 0.36, 0.32);
                        Shapes.Ellipsoid(m, hd, gold, 0.14, 0.11, 0.06, 6, false);
                        Shapes.Ellipsoid(m, SacredDraw.At(hd, sd * 0.12, 0.08, -0.02), tealL, 0.13, 0.12, 0.04, 5, false);
                        // Cloud scrolls in the corner above the arch (three-quarter curls).
                        double[] cs = { HalfOpen - 0.25, FriezeY - 0.3, HalfOpen - 0.75, FriezeY - 0.25, HalfOpen - 0.2, FriezeY - 0.85, 0.85, FriezeY - 0.2 };
                        for (int q = 0; q < cs.Length; q += 2)
                        {
                            ShapeBrush cb = q % 4 == 0 ? cloud : q == 2 ? tealL : gold;
                            Shapes.Torus(m, SacredDraw.Basis(ff, sd * cs[q], cs[q + 1], 0.29, 1, 0, 0, 0, 0, 1), cb, 0.12, 0.028, 7, 3, default, sd > 0 ? 30 : 60, 270, false);
                        }
                    }
                }
            }
        }

        /// <summary>A point of the left half of the arch line at t ∈ [0, 1] from the left springing (t = 0) to the apex
        /// (t = 1) with its unit normal pointing into the opening: vertical at the springing, rising to the pointed apex
        /// (a cubic, about 16° either side of level where the halves meet).</summary>
        private static void ArchPoint(double t, out double x, out double y, out double nx, out double ny)
        {
            t = Math.Max(0, Math.Min(1, t));
            double p0x = -HalfOpen, p0y = Spring, p1x = -HalfOpen, p1y = Spring + 0.95, p2x = -0.75, p2y = Apex - 0.32, p3x = 0, p3y = Apex;
            double a = 1 - t, b0 = a * a * a, b1 = 3 * a * a * t, b2 = 3 * a * t * t, b3 = t * t * t;
            x = b0 * p0x + b1 * p1x + b2 * p2x + b3 * p3x;
            y = b0 * p0y + b1 * p1y + b2 * p2y + b3 * p3y;
            double dx = 3 * a * a * (p1x - p0x) + 6 * a * t * (p2x - p1x) + 3 * t * t * (p3x - p2x);
            double dy = 3 * a * a * (p1y - p0y) + 6 * a * t * (p2y - p1y) + 3 * t * t * (p3y - p2y);
            double l = Math.Max(1e-9, Math.Sqrt(dx * dx + dy * dy));
            // The tangent turned clockwise points into the opening (right and down from the left half).
            nx = dy / l;
            ny = -dx / l;
        }

        /// <summary>One of the eight auspicious symbols (index picks it: conch, endless knot, wheel, lotus, parasol, golden
        /// fish, victory banner, treasure vase) in a raised gilt ring on a navy disc, centred at (x, y) on the face z of
        /// frame g; <paramref name="detail"/> 0 is the carved road face, 1 the painted lane face and LOD1.</summary>
        private static void Medallion(MeshData m, in Affine3 g, double x, double y, double z, int index, int detail)
        {
            Affine3 f = SacredDraw.Basis(g, x, y, z, 1, 0, 0, 0, 0, 1);
            ShapeBrush gold = BoudhaKit.Gilt;
            Shapes.Torus(m, f, gold, 0.29, 0.035, detail == 0 ? 10 : 6, 3);
            if (detail > 0) return;
            Shapes.Cylinder(m, f, BoudhaKit.B(Navy, MaterialChannel.Paint, 0.85f), 0.27, 0.025, 8, 0, 0, false, true);
            Symbol(m, SacredDraw.At(f, 0, 0.005, 0), index, 0.42, detail);
        }

        /// <summary>One of the eight auspicious symbols as bold coloured relief shapes, <paramref name="s"/> across.</summary>
        private static void Symbol(MeshData m, in Affine3 f, int index, double s, int detail)
        {
            ShapeBrush gold = BoudhaKit.Gilt, white = BoudhaKit.B(MeshColor.FromHex(0xF4F1EA), MaterialChannel.Paint), red = BoudhaKit.B(BoudhaKit.ClothRed, MaterialChannel.Paint);
            ShapeBrush pink = BoudhaKit.B(MeshColor.FromHex(0xE88AA0), MaterialChannel.Paint), green = BoudhaKit.B(MeshColor.FromHex(0x3E9C5A), MaterialChannel.Paint);
            int e = detail == 0 ? 6 : 4;
            switch (index % 8)
            {
                case 0: // conch: a white spiral shell
                    Shapes.Ellipsoid(m, SacredDraw.At(f, 0, 0.02, 0), white, 0.45 * s, 0.03, 0.65 * s, e, false);
                    if (detail == 0) Shapes.Cylinder(m, SacredDraw.At(f, 0, 0.03, -0.2 * s), red, 0.12 * s, 0.02, 6, 0, 0, false, true);
                    break;
                case 1: // endless knot: interlaced gold loops
                    for (int q = 0; q < (detail == 0 ? 4 : 2); q++)
                        Shapes.Torus(m, SacredDraw.At(f, (q % 2 == 0 ? -1 : 1) * 0.18 * s, 0.03, (q < 2 ? -1 : 1) * 0.18 * s), gold, 0.2 * s, 0.03, 6, 3);
                    break;
                case 2: // wheel
                    Shapes.Torus(m, SacredDraw.At(f, 0, 0.03, 0), gold, 0.42 * s, 0.05, detail == 0 ? 10 : 6, 3);
                    for (int q = 0; q < (detail == 0 ? 4 : 2); q++)
                    {
                        double a = Math.PI * q / (detail == 0 ? 4 : 2);
                        Shapes.Bar(m, f, gold, -0.4 * s * Math.Cos(a), 0.03, -0.4 * s * Math.Sin(a), 0.4 * s * Math.Cos(a), 0.03, 0.4 * s * Math.Sin(a), 0.03, 3);
                    }
                    break;
                case 3: // lotus
                    for (int q = 0; q < (detail == 0 ? 5 : 3); q++)
                    {
                        double a = Math.PI * (q - 0.5 * ((detail == 0 ? 5 : 3) - 1)) / 6;
                        Shapes.Ellipsoid(m, SacredDraw.At(f, 0.28 * s * Math.Sin(a), 0.03, -0.1 * s - 0.28 * s * Math.Cos(a)), pink, 0.13 * s, 0.03, 0.28 * s, 4, false);
                    }
                    break;
                case 4: // parasol
                    Shapes.Ellipsoid(m, SacredDraw.At(f, 0, 0.03, -0.25 * s), gold, 0.5 * s, 0.03, 0.22 * s, e, false);
                    Shapes.Bar(m, f, red, 0, 0.03, -0.1 * s, 0, 0.03, 0.55 * s, 0.04, 3);
                    break;
                case 5: // golden fish
                    for (int q = -1; q <= 1; q += 2) Shapes.Ellipsoid(m, SacredDraw.At(f, q * 0.16 * s, 0.03, 0), gold, 0.12 * s, 0.03, 0.48 * s, e, false);
                    break;
                case 6: // victory banner
                    Shapes.Ellipsoid(m, SacredDraw.At(f, 0, 0.03, 0), red, 0.2 * s, 0.03, 0.55 * s, e, false);
                    if (detail == 0) Shapes.Ellipsoid(m, SacredDraw.At(f, 0, 0.04, -0.55 * s), gold, 0.16 * s, 0.03, 0.1 * s, 4, false);
                    break;
                default: // treasure vase
                    Shapes.Ellipsoid(m, SacredDraw.At(f, 0, 0.03, 0.12 * s), gold, 0.36 * s, 0.03, 0.42 * s, e, false);
                    if (detail == 0) Shapes.Ellipsoid(m, SacredDraw.At(f, 0, 0.04, -0.42 * s), green, 0.2 * s, 0.03, 0.14 * s, 4, false);
                    break;
            }
        }

        /// <summary>How far the roof's corners kick up above its eave line (m).</summary>
        private const double RoofKick = 0.5;

        /// <summary>
        /// The yellow Tibetan roof (commons gate photos): a moulded fascia and soffit along an eave line that sweeps up
        /// <see cref="RoofKick"/> at the corners, and a shallow hipped top whose hips curve up with it to the corners.
        /// </summary>
        private static void Roof(MeshData m, in Affine3 g, double hx, double hz, in ShapeBrush roof, int lod)
        {
            ShapeBrush under = BoudhaKit.B(MeshColor.FromHex(0xC2A42E), MaterialChannel.Paint, 0.7f), edge = BoudhaKit.B(MeshColor.FromHex(0xEAD35A), MaterialChannel.Paint);
            double y0 = EaveY, fh = 0.32, ry = RoofY + 0.25, rh = Math.Max(0, hx - hz), inset = 0.55;
            int seg = lod == 0 ? 10 : lod == 1 ? 6 : 2;
            if (lod >= 2)
            {
                // A plain slab and hipped top.
                SacredDraw.Box(m, g, -hx, hx, y0, y0 + fh, -hz, hz, edge, BoxFaces.All);
                for (int s = -1; s <= 1; s += 2)
                {
                    SacredDraw.Quad(m, g, -hx, y0 + fh, s * hz, hx, y0 + fh, s * hz, rh, ry, 0, -rh, ry, 0, 0, 0.95, s * 0.3, roof);
                    SacredDraw.Tri(m, g, s * hx, y0 + fh, -hz, s * hx, y0 + fh, hz, s * rh, ry, 0, s * 0.3, 0.95, 0, roof);
                }
                return;
            }
            // The four edges, each from one corner to the next anticlockwise seen from above: (x, z) along, outward normal.
            for (int e = 0; e < 4; e++)
            {
                double ax, az, bx, bz, nx, nz;
                switch (e)
                {
                    case 0: ax = -hx; az = hz; bx = hx; bz = hz; nx = 0; nz = 1; break;
                    case 1: ax = hx; az = hz; bx = hx; bz = -hz; nx = 1; nz = 0; break;
                    case 2: ax = hx; az = -hz; bx = -hx; bz = -hz; nx = 0; nz = -1; break;
                    default: ax = -hx; az = -hz; bx = -hx; bz = hz; nx = -1; nz = 0; break;
                }
                for (int i = 0; i < seg; i++)
                {
                    double t0 = (double)i / seg, t1 = (double)(i + 1) / seg;
                    double x0, z0, l0, x1, z1, l1;
                    EdgePoint(ax, az, bx, bz, nx, nz, t0, out x0, out z0, out l0);
                    EdgePoint(ax, az, bx, bz, nx, nz, t1, out x1, out z1, out l1);
                    // Fascia (the moulded yellow edge).
                    SacredDraw.Quad(m, g, x0, y0 + l0, z0, x1, y0 + l1, z1, x1, y0 + l1 + fh, z1, x0, y0 + l0 + fh, z0, nx, 0, nz, edge);
                    // Soffit: from the fascia's foot in to the flat ceiling.
                    double ix0 = x0 - nx * inset - (e % 2 == 0 ? 0 : 0), iz0 = z0 - nz * inset, ix1 = x1 - nx * inset, iz1 = z1 - nz * inset;
                    ix0 = Math.Max(-hx + inset, Math.Min(hx - inset, ix0));
                    ix1 = Math.Max(-hx + inset, Math.Min(hx - inset, ix1));
                    iz0 = Math.Max(-hz + inset, Math.Min(hz - inset, iz0));
                    iz1 = Math.Max(-hz + inset, Math.Min(hz - inset, iz1));
                    SacredDraw.Quad(m, g, x1, y0 + l1, z1, x0, y0 + l0, z0, ix0, y0, iz0, ix1, y0, iz1, 0, -1, 0, under);
                    // Top: the fascia's head up to the ridge (the hips where the ridge's ends meet the corners).
                    double r0x = Math.Max(-rh, Math.Min(rh, x0)), r1x = Math.Max(-rh, Math.Min(rh, x1));
                    SacredDraw.Quad(m, g, x0, y0 + l0 + fh, z0, x1, y0 + l1 + fh, z1, r1x, ry, 0, r0x, ry, 0, nx * 0.3, 0.95, nz * 0.3, roof);
                }
            }
            SacredDraw.CapRect(m, g, 0, 0, hx - inset, hz - inset, y0, false, under);
            if (lod == 0)
            {
                // The gilt horn on each corner tip.
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        Shapes.Cone(m, SacredDraw.Basis(g, sx * (hx + 0.12), y0 + RoofKick + fh - 0.05, sz * (hz + 0.12), sx * 0.5, 0.75, sz * 0.5, 1, 0, 0), BoudhaKit.Gilt, 0.07, 0.3, 4, 0, 0, false);
            }
        }

        /// <summary>A point of a roof edge at t ∈ [0, 1] from corner a to corner b: pushed out a little and lifted towards
        /// both corners (quadratic over the outer 40 % of the edge, <see cref="RoofKick"/> at the tip).</summary>
        private static void EdgePoint(double ax, double az, double bx, double bz, double nx, double nz, double t, out double x, out double z, out double lift)
        {
            double c = Math.Abs(2 * t - 1), k = Math.Max(0, (c - 0.6) / 0.4);
            lift = RoofKick * k * k;
            x = ax + (bx - ax) * t + nx * 0.12 * k * k;
            z = az + (bz - az) * t + nz * 0.12 * k * k;
            // The corner flares out along both edges that meet there.
            double ex = bx - ax, ez = bz - az, el = Math.Max(1e-9, Math.Sqrt(ex * ex + ez * ez));
            double along = (2 * t - 1) * 0.12 * k * k;
            x += ex / el * along;
            z += ez / el * along;
        }

        /// <summary>The white bulb chorten on the crest (gate/commons): a stepped base, the bulb, a small gilt harmika and
        /// the 1.4 m gilt spire of 13 rings under a parasol and finial (top at <see cref="Top"/>).</summary>
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
            Shapes.Lathe(m, SacredDraw.At(g, x, 0, z), white, p, lod == 0 ? 12 : 8);
            double hb = b + 0.72;
            SacredDraw.Box(m, g, x - 0.15, x + 0.15, hb, hb + 0.16, z - 0.15, z + 0.15, gilt, BoxFaces.All & ~BoxFaces.Bottom);
            double sb = hb + 0.16, ringsTop = sb + 0.72, tip = y + (Top - RoofY);
            if (lod >= 1)
            {
                Shapes.Cone(m, SacredDraw.At(g, x, sb, z), gilt, 0.14, ringsTop - sb, lod == 1 ? 6 : 4, 0, 0, false);
            }
            else
            {
                // Thirteen rings tapering up, each a drum with its dark riser (sawtooth lathe).
                Profile2 q = BoudhaKit.Prof;
                q.Add(0, sb);
                for (int i = 0; i < 13; i++)
                {
                    double r = 0.14 - 0.07 * i / 12.0, ya = sb + (ringsTop - sb) * i / 13.0, yb = sb + (ringsTop - sb) * (i + 1) / 13.0;
                    q.Add(r, ya, true).Add(r * 0.86, yb, true);
                }
                q.Add(0, ringsTop);
                Shapes.Lathe(m, SacredDraw.At(g, x, 0, z), gilt, q, 6);
            }
            // Parasol and finial (a lotus bud on a spike).
            Profile2 u = BoudhaKit.Prof;
            u.Add(0, ringsTop).Add(0.06, ringsTop, true).Add(0.2, ringsTop + 0.05, true).Add(0.17, ringsTop + 0.1).Add(0.04, ringsTop + 0.14, true).Add(0.07, ringsTop + 0.22)
             .Add(0.06, ringsTop + 0.3).Add(0.02, ringsTop + 0.4).Add(0.01, tip - 0.08).Add(0, tip);
            Shapes.Lathe(m, SacredDraw.At(g, x, 0, z), gilt, u, lod == 0 ? 8 : 5);
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
            Path3 path = lod == 0 ? Curves.CatmullRom(ctrl, new Path3(32), 3) : ctrl;
            Shapes.Tube(m, g, gilt, path, 0.2, lod == 0 ? 6 : 5, true, false, default, 0.07);
            if (lod == 0)
            {
                // Teal dorsal fins along the humps and two clawed legs gripping the roof.
                double[] fin = { 2.15, 0.84, 1.65, 0.38, 1.2, 0.9 };
                for (int q = 0; q < fin.Length; q += 2)
                    Shapes.Cone(m, SacredDraw.At(g, s * fin[q], y + fin[q + 1], 0), teal, 0.09, 0.22, 4, 0, 0, false);
                for (int q = 0; q < 2; q++)
                {
                    double lx = s * (q == 0 ? 2.25 : 1.3);
                    Shapes.Bar(m, g, gilt, lx, y + (q == 0 ? 0.45 : 0.3), 0.05, lx + s * 0.12, y - 0.2, 0.18, 0.06, 4);
                }
            }
            // Head: a snout and jaw, horns, a teal crest.
            Affine3 h = SacredDraw.At(g, s * 0.7, y + 0.92, 0) * Affine3.Scaling(1.3);
            Shapes.Ellipsoid(m, h, gilt, 0.2, 0.16, 0.16, lod == 0 ? 6 : 5, false);
            Shapes.Bar(m, h, gilt, -s * 0.05, 0.02, 0, -s * 0.38, 0.1, 0, 0.08, 5);
            if (lod == 0)
            {
                Shapes.Bar(m, h, gilt, -s * 0.05, -0.08, 0, -s * 0.3, -0.12, 0, 0.05, 4);
                for (int q = -1; q <= 1; q += 2) Shapes.Bar(m, h, gilt, s * 0.05, 0.12, q * 0.06, s * 0.28, 0.35, q * 0.12, 0.025, 3);
                Shapes.Ellipsoid(m, SacredDraw.At(h, s * 0.12, 0.08, 0), teal, 0.12, 0.14, 0.05, 5, false);
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
