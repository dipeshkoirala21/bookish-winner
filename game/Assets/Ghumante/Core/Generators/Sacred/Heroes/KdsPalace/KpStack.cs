using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsPalace
{
    /// <summary>
    /// One storey and the roof over it in a Newar temple or tower stack, in the stack's plan frame (u, w half sizes): the
    /// storey's walls (half sizes <see cref="WallU"/> × <see cref="WallW"/>) rise from <see cref="Base"/> under the roof;
    /// the roof's eave (half sizes <see cref="EaveU"/> × <see cref="EaveW"/>) hangs at <see cref="Eave"/> and its slope
    /// meets the next storey at <see cref="Top"/>, where the next storey's walls stand (or, on the top tier, closes on a
    /// small apex block or ridge of half size <see cref="ApexU"/> × <see cref="ApexW"/>).
    /// </summary>
    internal struct KpTier
    {
        public double Base, WallU, WallW, Eave, EaveU, EaveW, Top, ApexU, ApexW;

        /// <summary>How the storey's walls are dressed (see <see cref="KpStack"/>).</summary>
        public KpWalls Walls;

        /// <summary>Windows per face (0 = none).</summary>
        public int Windows;

        /// <summary>The windows' sill above the storey's base and their height (0 = 0.35 m and up to 1.6 m).</summary>
        public double WinSill, WinH;

        /// <summary>How far the outer windows of a face sit from its middle, as a fraction of the half width (0 = 0.62).</summary>
        public double WinSpread;

        public static KpTier Square(double baseV, double wall, double eave, double eaveHalf, double top, double apex = 0.3)
        {
            return new KpTier { Base = baseV, WallU = wall, WallW = wall, Eave = eave, EaveU = eaveHalf, EaveW = eaveHalf, Top = top, ApexU = apex, ApexW = apex };
        }
    }

    /// <summary>The dressing of a stack storey.</summary>
    internal enum KpWalls : byte
    {
        /// <summary>Brick with carved lattice windows and a wooden cornice.</summary>
        Brick = 0,

        /// <summary>A timber storey of continuous carved lattice (belvederes, galleries).</summary>
        Lattice = 1,

        /// <summary>Brick with gilt window panels and a gilt torana (Taleju, Degu Taleju).</summary>
        GiltPanels = 2,

        /// <summary>Plain brick (hidden or tiny).</summary>
        Plain = 3,
    }

    /// <summary>The look of a stack's roofs.</summary>
    internal struct KpRoofLook
    {
        public ShapeBrush Roof, Fascia;
        public bool Metal, Fringe, Struts, Bells, Horns;

        /// <summary>Carved figure struts under the lowest roof (the ones seen close); the roofs above get plain struts.</summary>
        public bool RichStruts;

        public double StrutPitch, BellSpacing;

        /// <summary>How much the slopes sag (concave, shallow at the eave and steep at the top), as a fraction of the roof's
        /// run (0 = the kit's 0.015).</summary>
        public double Sag;

        public static KpRoofLook TileFringe(in ShapeBrush tile)
        {
            return new KpRoofLook { Roof = tile, Fascia = Kp.Wood, Fringe = true, Struts = true, RichStruts = true, Bells = true, Horns = true, StrutPitch = 1.2, BellSpacing = 0.85 };
        }

        public static KpRoofLook TilePlain(in ShapeBrush tile)
        {
            return new KpRoofLook { Roof = tile, Fascia = Kp.Wood, Struts = true, RichStruts = true, Bells = false, Horns = false, StrutPitch = 1.3, BellSpacing = 0.85 };
        }

        public static KpRoofLook CopperBells(in ShapeBrush copper)
        {
            return new KpRoofLook { Roof = copper, Fascia = Kp.Wood, Metal = true, Struts = true, RichStruts = true, Bells = true, Horns = true, StrutPitch = 1.2, BellSpacing = 0.85 };
        }
    }

    /// <summary>
    /// Builds the storey-and-roof stacks of the palace's temples and towers (Agamchhen, Degu Taleju, Taleju, the mini
    /// shrines, the Basantapur, Bhaktapur and Lalitpur towers) from explicit measured tiers, with the shared roof,
    /// strut, bell and window parts of the sacred kit.
    /// </summary>
    internal static class KpStack
    {
        /// <summary>
        /// Draw <paramref name="tiers"/> in plan frame <paramref name="k"/> (origin at the stack's centre on the square's
        /// level) and return the top of the last roof. The walls take <paramref name="wall"/>; LOD2 and coarser keep the
        /// roofs and plain walls.
        /// </summary>
        public static double Build(MeshData m, in Affine3 k, KpTier[] tiers, int count, in ShapeBrush wall, in KpRoofLook look, uint seed, int lod, double[] eaveOut = null)
        {
            double apex = 0;
            for (int i = 0; i < count; i++)
            {
                KpTier t = tiers[i];
                bool last = i == count - 1;
                double tu = last ? t.ApexU : tiers[i + 1].WallU, tw = last ? t.ApexW : tiers[i + 1].WallW;
                // The wall rises to just under the roof's soffit.
                double frac = (t.EaveU - t.WallU) / Math.Max(1e-6, t.EaveU - tu);
                double wallTop = t.Eave + frac * (t.Top - t.Eave) - 0.15;
                Storey(m, k, t, wallTop, wall, lod, seed + (uint)i);
                RoofSpec r = Kp.Tier(m, k, t.EaveU, t.EaveW, t.Eave, tu, tw, t.Top, t.WallU, t.WallW, look.Roof, look.Fascia, look.Metal, look.Fringe, lod,
                                     look.Horns ? 0.05 : 0.015, look.Sag > 0 ? look.Sag : 0.015);
                if (eaveOut != null && i < eaveOut.Length) eaveOut[i] = t.EaveU;
                if (look.Struts && lod <= 1)
                    Kp.Struts(m, k, r, Math.Max(t.Base + 0.4, wallTop - Math.Min(2.2, 0.9 * (t.EaveU - t.WallU) + 0.6)), look.StrutPitch, look.RichStruts && i == 0, seed * 7u + (uint)i, lod);
                if (look.Bells) Kp.Bells(m, k, r, look.BellSpacing, 0.9, lod);
                if (last && lod <= 1 && t.ApexU > 0.15 && Math.Abs(t.ApexU - t.ApexW) > 0.05)
                {
                    var p = new Path3(2);
                    if (t.ApexU > t.ApexW) p.Add(-t.ApexU, t.Top + 0.05, 0).Add(t.ApexU, t.Top + 0.05, 0);
                    else p.Add(0, t.Top + 0.05, -t.ApexW).Add(0, t.Top + 0.05, t.ApexW);
                    Shapes.Tube(m, k, r.Ridge, p, 0.12, 5, true);
                }
                apex = t.Top;
            }
            return apex;
        }

        /// <summary>
        /// A face frame on a plan frame <paramref name="k"/>: its w axis points out along the plan direction
        /// <paramref name="alphaDeg"/> (0 = +u, 90 = +w), its origin <paramref name="dist"/> out from the centre at height
        /// <paramref name="v"/>.
        /// </summary>
        public static Affine3 Face(in Affine3 k, double alphaDeg, double dist, double v = 0)
        {
            double a = alphaDeg * Math.PI / 180.0, c = Math.Cos(a), s = Math.Sin(a);
            return SacredDraw.Basis(k, dist * c, v, dist * s, s, 0, -c, 0, 1, 0);
        }

        /// <summary>One storey's walls from its base to <paramref name="top"/>.</summary>
        public static void Storey(MeshData m, in Affine3 k, in KpTier t, double top, in ShapeBrush wall, int lod, uint seed)
        {
            double h = top - t.Base;
            if (h < 0.2) return;
            ShapeBrush b = t.Walls == KpWalls.Lattice ? Kp.Wood : wall;
            if (lod >= 3 || t.Walls == KpWalls.Plain)
            {
                SacredDraw.Box(m, k, -t.WallU, t.WallU, t.Base, top, -t.WallW, t.WallW, b, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
                return;
            }
            SacredParts.BandedWall(m, k, t.WallU, t.WallW, t.Base, top, b, Kp.Wood, Math.Min(2, lod), true, h > 1.2);
            if (lod >= 2 && t.Walls != KpWalls.Lattice && t.Windows <= 0) return;
            for (int s = 0; s < 4; s++)
            {
                double half = s % 2 == 0 ? t.WallU : t.WallW, depth = s % 2 == 0 ? t.WallW : t.WallU;
                Affine3 side = SacredDraw.Turn(k, 0, 0, 0, s * 0.5 * Math.PI);
                Affine3 face = SacredDraw.At(side, 0, 0, depth);
                double sill = t.Base + (t.WinSill > 0 ? t.WinSill : 0.35), wh = t.WinH > 0 ? Math.Min(t.WinH, h - 0.6 - (sill - t.Base)) : Math.Min(1.6, h - 0.9);
                if (wh < 0.4) continue;
                switch (t.Walls)
                {
                    case KpWalls.Lattice:
                    {
                        // A continuous band of carved lattice panels between posts.
                        int n = Math.Max(2, (int)Math.Round(2 * half / 1.15));
                        if (lod >= 2)
                        {
                            Kp.Panel(m, face, -half + 0.1, sill, half - 0.1, sill + wh, 0.02, Kp.Recess);
                            break;
                        }
                        for (int q = 0; q < n; q++)
                        {
                            double u = -half + 2 * half * (q + 0.5) / n;
                            Kp.Lattice(m, face, u, sill + 0.5 * wh, 2 * half / n - 0.22, wh, lod, lod == 0 ? 2 : 0);
                        }
                        break;
                    }
                    case KpWalls.GiltPanels:
                    {
                        int n = Math.Max(1, t.Windows);
                        double spread = t.WinSpread > 0 ? t.WinSpread : 0.62;
                        for (int q = 0; q < n; q++)
                        {
                            double u = n == 1 ? 0 : -spread * half + 2 * spread * half * q / (n - 1), ww = Math.Min(t.WinH > 0 ? 0.62 * t.WinH : 1.1, 1.1 * half / n);
                            if (lod >= 2)
                            {
                                Kp.Panel(m, face, u - 0.5 * ww, sill, u + 0.5 * ww, sill + wh, 0.03, Kp.GiltDeep);
                                continue;
                            }
                            // A gilt window (Taleju, Degu Taleju): a carved dark frame, a dark lattice behind gilt bars and a
                            // gilt cusped arch over it; the middle one carries the gilt torana.
                            double arch = 0.3 * ww, rect = wh - arch;
                            Kp.Box(m, face, u - 0.5 * ww - 0.14, u + 0.5 * ww + 0.14, sill - 0.12, sill + wh + 0.1, 0, 0.08, Kp.Wood, BoxFaces.Wall | BoxFaces.Bottom);
                            Kp.Panel(m, face, u - 0.5 * ww, sill, u + 0.5 * ww, sill + wh, 0.085, Kp.Recess);
                            Kp.Box(m, face, u - 0.5 * ww, u - 0.5 * ww + 0.08, sill, sill + rect, 0.08, 0.14, Kp.Gilt, BoxFaces.Front | BoxFaces.Right);
                            Kp.Box(m, face, u + 0.5 * ww - 0.08, u + 0.5 * ww, sill, sill + rect, 0.08, 0.14, Kp.Gilt, BoxFaces.Front | BoxFaces.Left);
                            Kp.Box(m, face, u - 0.5 * ww, u + 0.5 * ww, sill, sill + 0.1, 0.08, 0.14, Kp.Gilt, BoxFaces.Front | BoxFaces.Top);
                            ShikharaGenerator.Arch(m, face, u, sill + rect, 0.1, ww, arch, 0.08, 0.04, true, Kp.Gilt, lod);
                            if (lod == 0)
                            {
                                for (int i = 1; i < 4; i++)
                                {
                                    double uu = u - 0.5 * ww + ww * i / 4;
                                    Kp.Box(m, face, uu - 0.02, uu + 0.02, sill + 0.1, sill + rect, 0.09, 0.12, Kp.GiltDeep, BoxFaces.Front);
                                }
                                for (int i = 1; i < 5; i++)
                                {
                                    double vv = sill + 0.1 + (rect - 0.1) * i / 5;
                                    Kp.Box(m, face, u - 0.5 * ww + 0.08, u + 0.5 * ww - 0.08, vv - 0.02, vv + 0.02, 0.09, 0.12, Kp.GiltDeep, BoxFaces.Front);
                                }
                            }
                            if (q == n / 2 && lod <= 1) SacredParts.Torana(m, face, u, sill + wh + 0.14, 0.04, ww + 0.7, Kp.Gilt, 1);
                        }
                        break;
                    }
                    default:
                    {
                        int n = t.Windows > 0 ? t.Windows : Math.Max(1, (int)(2 * half / 2.2));
                        for (int q = 0; q < n; q++)
                        {
                            double u = n == 1 ? 0 : -0.66 * half + 1.32 * half * q / (n - 1);
                            Kp.Lattice(m, face, u, sill + 0.5 * wh, Math.Min(0.9, 1.2 * half / n), wh, lod, 2);
                        }
                        break;
                    }
                }
            }
        }

        /// <summary>A plinth stage: a moulded brick (or stone) level from v0 to v1 round a rectangle of half sizes, its top
        /// capped (walkable when the caller adds the collider).</summary>
        public static void Stage(MeshData m, in Affine3 k, double cu, double cw, double hu, double hw, double v0, double v1, in ShapeBrush wall, in ShapeBrush top,
                                 int lod)
        {
            double[] pu = ShapeScratch.U2, pw = ShapeScratch.W2;
            int n = SacredDraw.Rect(cu, cw, hu, hw, pu, pw);
            Mould p = SacredDraw.M;
            double rise = v1 - v0;
            if (lod >= 2 || rise < 0.15) p.Add(0, v0 - 0.2, wall).Add(0, v1);
            else
                p.Add(0.03, v0 - 0.2, wall).Add(0.03, v0 + 0.08).Add(0, v0 + 0.08).Add(0, v1 - Math.Min(0.12, 0.3 * rise)).Add(0.05, v1 - Math.Min(0.12, 0.3 * rise), Kp.Step)
                 .Add(0.05, v1);
            SacredDraw.Ring(m, k, pu, pw, n, p);
            double o = p.O[p.Count - 1];
            SacredDraw.CapRect(m, k, cu, cw, hu + o, hw + o, v1, true, top);
        }
    }
}
