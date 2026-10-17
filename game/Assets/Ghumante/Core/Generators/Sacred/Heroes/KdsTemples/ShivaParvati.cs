using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsTemples
{
    /// <summary>
    /// The Shiva-Parvati temple (Nava Yogini, late 18th c.; kds-temples.md T7; OSM w185882281 and its parts: plinth 14.7 ×
    /// 9.8 to 1.2 m and 11.8 × 7.0 to 1.4 m, ground storey 11.5 × 6.6 to 5.5 m, cornice band 12.1 × 7.2, upper storey
    /// 10.8 × 5.9 to 8 m, hipped roof 15.7 × 10.8 from 7 to 10 m, finials to 11.3 m): a long brick house-temple facing
    /// south-south-west toward Maju Dega. A stepped plinth with a broad central stair and two stone lions on the lower
    /// stage; a ground storey of five carved cusped (multifoil) arches on the front, the centre one the door, the others
    /// carved lattice windows, with carved pilasters and a deep carved lintel band; the upper storey's band of carved
    /// windows with, at the centre, the wide window where the painted figures of Shiva and Parvati look out side by side;
    /// the hipped tile roof with its red valance and five gilt finials on the ridge; a small white domed shrine at the
    /// plinth's west corner.
    /// </summary>
    internal static class ShivaParvati
    {
        public const float TopM = 11.3f;

        public static double Build(KdsCtx x)
        {
            int lod = x.Lod;
            MeshData m = x.M;
            double hu0 = x.PlanHu > 1 ? x.PlanHu : 7.9, hw0 = x.PlanHw > 1 ? x.PlanHw : 5.45;
            if (lod >= 3)
            {
                KdsKit.Lod3(x, hu0, hw0, 1.4, 5.75, 3.3, new[] { 7.85 }, new[] { 7.0 }, new double[0], 10.0, KdsLooks.BrickPlinth, KdsLooks.BrickRed, KdsLooks.TileOld);
                return 10.0;
            }
            // Plinth: the OSM outline, then the two stages (OSM parts w420291393, w420291396).
            if (x.OutU != null) KdsKit.OutlineLevel(x, x.OutU, x.OutW, x.OutU.Length, 0, 0.5, 0.25, KdsLooks.BrickPlinth, KdsLooks.Nosing, GenColliders.Brick);
            else KdsKit.Stage(x, hu0, hw0, 0, 0.5, 0, 0, 0, KdsLooks.BrickPlinth, KdsLooks.Nosing, 0.1, 0.25, GenColliders.Brick);
            const double stairHalf = 1.6;
            KdsKit.Stage(x, 7.35, 4.9, 0.5, 1.2, 5.9, 3.5, stairHalf, KdsLooks.BrickPlinth, KdsLooks.Nosing, 0.12, 0, GenColliders.Brick, 3.5);
            KdsKit.Stage(x, 5.9, 3.5, 1.2, 1.4, 0, 0, 0, KdsLooks.BrickPlinth, KdsLooks.Nosing, 0.08, 0, GenColliders.Brick);
            KdsKit.Stair(x, stairHalf, 3.5, Math.Max(1.6, hw0 - 3.5 + 0.6), 0, 1.4, 0.25, 0.25, KdsLooks.Nosing, KdsLooks.BrickPlinth, 6);
            if (x.Stats != null)
            {
                x.Stats.PlinthLevels = 3;
                x.Stats.PlinthTopM = 1.4f;
            }
            // The two stone lions on the lower stage either side of the stair.
            KdsKit.Lion(x, -(stairHalf + 0.75), 4.3, 0.3, 1.45, -1, false, 0.32);
            KdsKit.Lion(x, stairHalf + 0.75, 4.3, 0.3, 1.45, 1, false, 0.32);
            // A small white domed shrine at the west corner of the plinth.
            if (lod <= 1) SmallDomeShrine(x, -hu0 + 1.1, hw0 - 1.1, 0.5);

            // Ground storey with the arcade.
            const double gu = 5.75, gw = 3.3, g0 = 1.4, g1 = 5.15;
            SacredParts.BandedWall(m, x.K, gu, gw, g0, g1, KdsLooks.BrickRed, KdsLooks.Timber, lod, true, false);
            x.Solid(-gu, gu, -gw, gw, g0, g1, GenColliders.Brick);
            for (int s = 0; s < 4; s++)
            {
                Affine3 sk = KdsKit.Side(x.K, s);
                double a = KdsKit.Along(s, gu, gw), d = KdsKit.Depth(s, gu, gw);
                int arches = s % 2 == 0 ? 5 : 2;
                double pitch = 2 * a / arches;
                for (int i = 0; i < arches; i++)
                {
                    double u = -a + pitch * (i + 0.5);
                    bool door = s == 0 && i == arches / 2;
                    CuspedArch(x, sk, u, g0 + 0.15, d, Math.Min(1.25, 0.72 * pitch), 2.25, door);
                }
                if (lod <= 1)
                {
                    // Carved pilasters between the arches.
                    for (int i = 0; i <= arches; i++)
                    {
                        double u = -a + pitch * i;
                        SacredDraw.Box(m, sk, u - 0.11, u + 0.11, g0 + 0.15, g1 - 0.2, d, d + 0.08, KdsLooks.Timber, BoxFaces.Wall);
                    }
                }
            }
            // The deep carved lintel band and cornice between the storeys (OSM w420291386: 12.1 × 7.2 m).
            KdsKit.Cornice(x, 6.05, 3.6, g1, g1 + 0.55, 0.3, KdsLooks.Timber);
            if (lod == 0) CarvedBand(x, 6.05, 3.6, g1 + 0.06, 0.28);

            // Upper storey: carved windows, the couple at the centre front.
            const double uu = 5.4, uw = 2.95, u0 = g1 + 0.55;
            double soffit = 7.35;
            SacredParts.BandedWall(m, x.K, uu, uw, u0, 8.0, KdsLooks.BrickRed, KdsLooks.Timber, lod, false, false);
            x.Solid(-uu, uu, -uw, uw, u0, 8.0, GenColliders.Brick);
            for (int s = 0; s < 4; s++)
            {
                Affine3 sk = KdsKit.Side(x.K, s);
                double a = KdsKit.Along(s, uu, uw), d = KdsKit.Depth(s, uu, uw);
                int n = s % 2 == 0 ? 5 : 2;
                for (int i = 0; i < n; i++)
                {
                    double u = -a + 2 * a * (i + 0.5) / n;
                    if (s == 0 && i == n / 2)
                    {
                        CoupleWindow(x, sk, u, u0 + 0.15, d);
                        continue;
                    }
                    if (i % 2 == 0 && lod <= 1) Rosette(x, sk, u, 0.5 * (u0 + soffit), d, 0.32);
                    else KdsKit.BlackWindow(m, sk, u, 0.5 * (u0 + soffit), d, 0.62, 0.62, lod, 0.12, 3);
                }
            }

            // The hipped roof with its ridge along the long side, red valance, five gilt finials.
            const double eu = 7.85, ew = 5.4, ev = 7.0, rv = 10.0;
            RoofSpec r = KdsKit.Roof(x, eu, ew, ev, eu - ew + 0.12, 0.12, rv, uu, uw, soffit, KdsLooks.TileOld, 0.44);
            if (lod <= 1)
            {
                var ridge = new Path3(2);
                ridge.Add(-(eu - ew + 0.12), rv + 0.06, 0).Add(eu - ew + 0.12, rv + 0.06, 0);
                Shapes.Tube(m, x.K, Looks.Of(MeshColor.Scale(KdsLooks.TileOld.Color, 0.7f), MaterialChannel.RoofTile), ridge, 0.1, 5, true);
            }
            KdsKit.Struts(x, r, u0 - 0.2, 8, 4, false, 0x53505Au);
            if (lod == 0 && x.Stats != null) x.Stats.Bells += KdsKit.Bells(x, r, 1.2, 1.0);
            double tip = rv;
            for (int i = 0; i < 5; i++)
            {
                double u = -2.2 + 1.1 * i;
                double h = i == 2 ? TopM - rv + 0.1 : TopM - rv - 0.15;
                tip = Math.Max(tip, Finial(x, u, rv - 0.1, h));
            }
            if (x.Stats != null)
            {
                x.Stats.Tiers = 1;
                x.Stats.Pinnacles = 5;
            }
            return tip;
        }

        /// <summary>A gilt ridge finial at u: a kalasha finial with two leaf/flag ornaments.</summary>
        private static double Finial(KdsCtx x, double u, double v0, double h)
        {
            Affine3 k = x.K;
            x.K = SacredDraw.At(k, u, 0, 0);
            double tip = KdsKit.GiltFinial(x, v0, h, 0.15);
            if (x.Lod == 0)
            {
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    Shapes.Bar(x.M, x.K, KdsLooks.Gilt, 0, v0 + 0.35 * h, 0, sgn * 0.3, v0 + 0.85 * h, 0, 0.015, 4);
                    Shapes.Ellipsoid(x.M, SacredDraw.At(x.K, sgn * 0.32, v0 + 0.9 * h, 0), Looks.GiltHi, 0.09, 0.12, 0.02, 6, false);
                }
            }
            x.K = k;
            return tip;
        }

        /// <summary>A cusped multifoil arch on side frame sk at u (opening width w, jamb height h from v, on the wall plane
        /// d): the carved frame, a lobed arch head with a carved tympanum, and a dark door or a carved lattice window.</summary>
        private static void CuspedArch(KdsCtx x, in Affine3 sk, double u, double v, double d, double w, double h, bool door)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            double hw = 0.5 * w, archH = 0.55 * w;
            ShapeBrush fill = door ? Looks.Dark : Looks.Recess;
            SacredDraw.Panel(m, sk, u - hw, v, u + hw, v + h, d + 0.01, fill);
            // The lobed arch opening above the jambs.
            int seg = lod == 0 ? 18 : 8;
            double[] px = ShapeScratch.U, pz = ShapeScratch.W;
            int n = 0;
            px[n] = -hw;
            pz[n++] = 0;
            for (int i = 0; i <= seg; i++)
            {
                double a = Math.PI * i / seg, r = 1 - 0.12 * Math.Abs(Math.Sin(5 * a));
                px[n] = -hw * Math.Cos(a) * r;
                pz[n++] = -archH * Math.Sin(a) * r;
            }
            px[n] = hw;
            pz[n++] = 0;
            Shapes.BevelExtrude(m, SacredDraw.Basis(sk, u, v + h, d + 0.01, 1, 0, 0, 0, 0, 1), fill, px, pz, n, 0.01, 0, 0, BevelStyle.Chamfer, true, false);
            if (lod >= 2) return;
            // Frame and the carved tympanum surround (a raised cusped ring).
            ShapeBrush fr = KdsLooks.Timber;
            SacredDraw.Box(m, sk, u - hw - 0.12, u - hw, v, v + h, d, d + 0.1, fr, BoxFaces.Wall);
            SacredDraw.Box(m, sk, u + hw, u + hw + 0.12, v, v + h, d, d + 0.1, fr, BoxFaces.Wall);
            for (int i = 0; i < n; i++)
            {
                px[i] *= 1.18;
                pz[i] = pz[i] * 1.22;
            }
            Shapes.BevelExtrude(m, SacredDraw.Basis(sk, u, v + h, d + 0.005, 1, 0, 0, 0, 0, 1), Looks.Of(MeshColor.FromHex(0x5A3A2A), MaterialChannel.WoodCarved),
                                px, pz, n, 0.06, lod == 0 ? 0.02 : 0, lod == 0 ? 1 : 0, BevelStyle.Chamfer, true, false);
            // Lattice for the windows.
            if (!door && lod == 0)
            {
                for (int i = 1; i <= 3; i++)
                {
                    double uu = u - hw + w * i / 4;
                    SacredDraw.Box(m, sk, uu - 0.02, uu + 0.02, v, v + h, d + 0.01, d + 0.05, fr, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                }
                for (int i = 1; i <= 5; i++)
                {
                    double vv = v + h * i / 6;
                    SacredDraw.Box(m, sk, u - hw, u + hw, vv - 0.02, vv + 0.02, d + 0.01, d + 0.05, fr, BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom);
                }
            }
        }

        /// <summary>The carved band under the cornice: a row of small carved figure bosses.</summary>
        private static void CarvedBand(KdsCtx x, double hu, double hw, double v, double h)
        {
            for (int s = 0; s < 4; s++)
            {
                Affine3 sk = KdsKit.Side(x.K, s);
                double a = KdsKit.Along(s, hu, hw), d = KdsKit.Depth(s, hu, hw);
                int n = (int)(2 * a / 0.55);
                for (int i = 0; i < n; i++)
                {
                    double u = -a + 2 * a * (i + 0.5) / n;
                    Shapes.Ellipsoid(x.M, SacredDraw.At(sk, u, v + 0.5 * h, d + 0.32), Looks.Of(MeshColor.FromHex(0x5A3A2A), MaterialChannel.WoodCarved), 0.09, 0.12,
                                     0.05, 6, false);
                }
            }
        }

        /// <summary>A round carved rosette window (a dark disc in a carved ring).</summary>
        private static void Rosette(KdsCtx x, in Affine3 sk, double u, double v, double d, double r)
        {
            Affine3 f = SacredDraw.Basis(sk, u, v, d, 1, 0, 0, 0, 0, 1);
            Shapes.Cylinder(x.M, f, Looks.Recess, r, 0.02, x.Lod == 0 ? 12 : 8, 0, 0, false, true);
            Shapes.Torus(x.M, f * Affine3.Translation(0, 0.04, 0), KdsLooks.Timber, r, 0.06, x.Lod == 0 ? 14 : 8, 4);
            if (x.Lod == 0)
            {
                for (int i = 0; i < 6; i++)
                {
                    double a = Math.PI * i / 6;
                    Shapes.Bar(x.M, f, KdsLooks.Timber, -r * Math.Cos(a), 0.03, -r * Math.Sin(a), r * Math.Cos(a), 0.03, r * Math.Sin(a), 0.018, 4);
                }
            }
        }

        /// <summary>The temple's signature: the wide central upper window where the painted figures of Shiva and Parvati
        /// look out side by side (white faces, black hair, gilt crowns and jewellery, coloured garments), arms on the sill.</summary>
        private static void CoupleWindow(KdsCtx x, in Affine3 sk, double u, double v, double d)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            const double w = 1.5, h = 1.35;
            SacredDraw.Panel(m, sk, u - 0.5 * w, v, u + 0.5 * w, v + h, d + 0.01, Looks.Recess);
            ShapeBrush fr = KdsLooks.Timber;
            SacredDraw.Box(m, sk, u - 0.5 * w - 0.14, u - 0.5 * w, v, v + h, d, d + 0.12, fr, BoxFaces.Wall);
            SacredDraw.Box(m, sk, u + 0.5 * w, u + 0.5 * w + 0.14, v, v + h, d, d + 0.12, fr, BoxFaces.Wall);
            SacredDraw.Box(m, sk, u - 0.5 * w - 0.3, u + 0.5 * w + 0.3, v + h, v + h + 0.16, d, d + 0.14, fr, BoxFaces.Wall | BoxFaces.Bottom);
            SacredDraw.Box(m, sk, u - 0.5 * w - 0.25, u + 0.5 * w + 0.25, v - 0.08, v + 0.06, d, d + 0.22, fr, BoxFaces.Wall | BoxFaces.Bottom);
            if (lod >= 2) return;
            // Shiva (left seen from the front) and Parvati.
            for (int i = 0; i < 2; i++)
            {
                double cu = u + (i == 0 ? -0.36 : 0.36);
                ShapeBrush cloth = i == 0 ? KdsLooks.ClothBlue : KdsLooks.ClothRed;
                int seg = lod == 0 ? 10 : 6;
                // Shoulders and chest leaning on the sill, arms forward.
                Shapes.Ellipsoid(m, SacredDraw.At(sk, cu, v + 0.3, d + 0.02), cloth, 0.24, 0.25, 0.14, seg, false);
                if (lod == 0)
                {
                    Shapes.Capsule(m, SacredDraw.Basis(sk, cu - 0.14, v + 0.12, d + 0.04, 1, 0, 0, 0, 0, 1), KdsLooks.Skin, 0.045, 0.24, 6);
                    Shapes.Capsule(m, SacredDraw.Basis(sk, cu + 0.14, v + 0.12, d + 0.04, 1, 0, 0, 0, 0, 1), KdsLooks.Skin, 0.045, 0.24, 6);
                }
                // Head, hair and crown.
                Shapes.Ellipsoid(m, SacredDraw.At(sk, cu, v + 0.68, d + 0.06), KdsLooks.Skin, 0.12, 0.15, 0.12, seg, false);
                Shapes.Ellipsoid(m, SacredDraw.At(sk, cu, v + 0.72, d + 0.0), KdsLooks.HairBlack, 0.13, 0.15, 0.1, seg, false);
                Shapes.Cone(m, SacredDraw.At(sk, cu, v + 0.8, d + 0.04), KdsLooks.Gilt, 0.11, 0.24, lod == 0 ? 8 : 5, 0, 0, true);
                if (lod == 0)
                {
                    // Eyes and the red mouth.
                    Shapes.Sphere(m, SacredDraw.At(sk, cu - 0.045, v + 0.7, d + 0.17), KdsLooks.HairBlack, 0.018, 4);
                    Shapes.Sphere(m, SacredDraw.At(sk, cu + 0.045, v + 0.7, d + 0.17), KdsLooks.HairBlack, 0.018, 4);
                    Shapes.Ellipsoid(m, SacredDraw.At(sk, cu, v + 0.62, d + 0.17), KdsLooks.PaintRed, 0.03, 0.012, 0.01, 4, false);
                    Shapes.Torus(m, SacredDraw.At(sk, cu, v + 0.48, d + 0.06), KdsLooks.Gilt, 0.13, 0.02, 10, 4);
                }
            }
        }

        /// <summary>A small white plastered domed shrine at (u, w) on the plinth (top v).</summary>
        private static void SmallDomeShrine(KdsCtx x, double u, double w, double v)
        {
            MeshData m = x.M;
            SacredDraw.Box(m, x.K, u - 0.55, u + 0.55, v, v + 0.9, w - 0.55, w + 0.55, KdsLooks.Plaster, BoxFaces.All & ~BoxFaces.Bottom);
            Shapes.Dome(m, SacredDraw.At(x.K, u, v + 0.9, w), KdsLooks.Plaster, 0.5, 0.55, x.Lod == 0 ? 12 : 8);
            Shapes.Cone(m, SacredDraw.At(x.K, u, v + 1.4, w), KdsLooks.Gilt, 0.07, 0.3, 6, 0, 0, false);
            SacredDraw.Panel(m, KdsKit.Side(x.K, 0), u - 0.2, v + 0.1, u + 0.2, v + 0.6, w + 0.56, Looks.Dark);
            x.Solid(u - 0.55, u + 0.55, w - 0.55, w + 0.55, v, v + 1.7, GenColliders.Stone);
        }
    }
}
