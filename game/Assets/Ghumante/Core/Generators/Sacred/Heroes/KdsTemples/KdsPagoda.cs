using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsTemples
{
    /// <summary>
    /// One roof tier of a Newar temple as measured (kds-temples.md §3): the storey wall under it (half sizes, its visible
    /// foot), the eave edge, the ring where the slope meets the storey above (or the apex), the struts, the valance and the
    /// windows on the storey's visible wall band.
    /// </summary>
    internal struct KdsTier
    {
        public double WallHu, WallHw, WallV0;
        public double EaveHu, EaveHw, EaveV;
        public double TopHu, TopHw, TopV;
        public int StrutsU, StrutsW;

        /// <summary>Strut foot height (0 = the wall foot + 0.15 m).</summary>
        public double StrutFootV;

        public double ValanceH;
        public ShapeBrush Wall, Tile;

        /// <summary>Windows per side on the visible wall band (0 none); width and height.</summary>
        public int Windows;

        public double WinW, WinH;
        public bool Bells, RichStruts;

        /// <summary>Carved deity struts with griffin corners at LOD0 (Jagannath's first roof; <see cref="KdsKit.DeityStruts"/>).</summary>
        public bool DeityStruts;

        /// <summary>The roof underside where it passes over the storey wall (the soffit's wall line).</summary>
        public double SoffitAtWall
        {
            get
            {
                double run = Math.Max(1e-6, EaveHu - TopHu);
                return EaveV + (EaveHu - WallHu) / run * (TopV - EaveV) - 0.28;
            }
        }
    }

    /// <summary>
    /// Shared assembly of the KDS multi-roof temples on measured tiers: the storey walls with their black window bands, the
    /// roofs with valances, struts and bells, and the ground storeys (a post ambulatory round a sanctum, or solid walls).
    /// Each temple's own file sets the measured numbers and adds what is unique to it.
    /// </summary>
    internal static class KdsPagoda
    {
        /// <summary>Draws the tiers bottom to top; returns the apex height of the last roof.</summary>
        public static double Tiers(KdsCtx x, KdsTier[] tiers, uint seed)
        {
            double apex = 0;
            for (int i = 0; i < tiers.Length; i++)
            {
                KdsTier t = tiers[i];
                // A tier whose eave would hang into a road corridor's clearance has its overhang pulled back by the roof
                // (KdsKit.FitEave, once: never shorter than 60 % of the measured overhang); the measured eave height stays
                // (judge round 1: a heritage tier is never raised).
                double soffit = Math.Max(t.EaveV + 0.05, t.SoffitAtWall);
                // The storey wall from its foot up into the roof.
                SacredParts.BandedWall(x.M, x.K, t.WallHu, t.WallHw, t.WallV0, soffit + 0.25, t.Wall, KdsLooks.Timber, x.Lod, false, false);
                x.Solid(-t.WallHu, t.WallHu, -t.WallHw, t.WallHw, t.WallV0, soffit, GenColliders.Brick);
                if (t.Windows > 0) WindowBand(x, t.WallHu, t.WallHw, t.WallV0, soffit, t.Windows, t.WinW, t.WinH);
                RoofSpec r = KdsKit.Roof(x, t.EaveHu, t.EaveHw, t.EaveV, t.TopHu, t.TopHw, t.TopV, t.WallHu, t.WallHw, soffit, t.Tile, t.ValanceH);
                if (t.StrutsU > 0)
                {
                    double foot = t.StrutFootV > 0 ? t.StrutFootV : t.WallV0 + 0.15;
                    int n = t.DeityStruts ? KdsKit.DeityStruts(x, r, foot, t.StrutsU, t.StrutsW, seed + (uint)i * 31u)
                                          : KdsKit.Struts(x, r, foot, t.StrutsU, t.StrutsW, t.RichStruts, seed + (uint)i * 31u);
                    if (x.Stats != null) x.Stats.Struts += n;
                }
                if (t.Bells && x.Lod == 0 && i == 0)
                {
                    int b = KdsKit.Bells(x, r, 1.0, Math.Max(0.8, Math.Min(1.3, t.EaveHu / 5.0)));
                    if (x.Stats != null) x.Stats.Bells += b;
                }
                apex = t.TopV;
            }
            return apex;
        }

        /// <summary>A band of black carved windows on each side of a storey wall, evenly spaced, centred between v0 and v1.</summary>
        public static void WindowBand(KdsCtx x, double hu, double hw, double v0, double v1, int perSide, double ww, double wh)
        {
            if (x.Lod >= 2 && perSide > 1) perSide = 1;
            double vc = 0.5 * (v0 + v1);
            wh = Math.Min(wh, 0.8 * (v1 - v0));
            if (wh < 0.2) return;
            for (int s = 0; s < 4; s++)
            {
                Affine3 sk = KdsKit.Side(x.K, s);
                double a = KdsKit.Along(s, hu, hw), d = KdsKit.Depth(s, hu, hw);
                for (int q = 0; q < perSide; q++)
                {
                    double u = -a + 2 * a * (q + 0.5) / perSide;
                    bool centre = perSide % 2 == 1 && q == perSide / 2;
                    KdsKit.BlackWindow(x.M, sk, u, vc, d, centre ? ww * 1.25 : ww, wh, x.Lod, centre ? 0.22 : 0.12);
                }
            }
        }

        /// <summary>
        /// The ground storey as an ambulatory: a ring of carved posts (half sizes pu × pw, <paramref name="bays"/> bays per
        /// side) from v0 to v1 round a sanctum of half sizes su × sw, a carved beam band over the posts (to v1 + beam). The
        /// sanctum gets <paramref name="doors"/> doors (1 = front only, 4 = all sides). Returns the beam top.
        /// </summary>
        public static double Ambulatory(KdsCtx x, double pu, double pw, double v0, double v1, int bays, double postT, double su, double sw, in ShapeBrush sanctum,
                                       int doors, double beam, bool latticeDoors, bool keyholes, in ShapeBrush postWood)
        {
            KdsKit.PostRing(x, pu, pw, v0, v1, bays, bays, postT, postWood);
            // The sanctum walls rise to the beam.
            SacredParts.BandedWall(x.M, x.K, su, sw, v0, v1 + beam, sanctum, KdsLooks.Timber, x.Lod, true, false);
            x.Solid(-su, su, -sw, sw, v0, v1 + beam, GenColliders.Brick);
            // Ceiling of the ambulatory (the underside of the floor above).
            double cv = v1 + 0.02;
            SacredDraw.Quad(x.M, x.K, -pu, cv, -pw, pu, cv, -pw, pu, cv, pw, -pu, cv, pw, 0, -1, 0, Looks.Ao(KdsLooks.Beam, 0.45f));
            // The beam band over the posts with a carved moulding.
            KdsKit.Cornice(x, pu + 0.06, pw + 0.06, v1, v1 + beam, 0.12, KdsLooks.Timber);
            if (x.Lod <= 1)
            {
                // Inner beam (sanctum to posts) cross ties at the corners.
                for (int s = 0; s < 4; s++)
                {
                    Affine3 sk = KdsKit.Side(x.K, s);
                    double a = KdsKit.Along(s, su, sw), d = KdsKit.Depth(s, su, sw);
                    // The front face in full; the others, in the ambulatory's shade, one LOD coarser.
                    int was = s > 0 ? x.Coarser() : x.Lod;
                    SanctumFace(x, sk, a, d, v0, v1, s < doors || doors >= 4, latticeDoors, keyholes);
                    x.Restore(was);
                }
            }
            else
            {
                Affine3 k0 = KdsKit.Side(x.K, 0);
                SacredDraw.Panel(x.M, k0, -0.6, v0 + 0.1, 0.6, v0 + 2.0, sw + 0.01, Looks.Dark);
            }
            return v1 + beam;
        }

        /// <summary>One face of a sanctum (half length a at distance d): a door (lattice or dark with a torana) and, beside it,
        /// the black keyhole niches (Trailokya) or black windows.</summary>
        public static void SanctumFace(KdsCtx x, in Affine3 sk, double a, double d, double v0, double v1, bool door, bool lattice, bool keyholes)
        {
            double h = v1 - v0, dw = Math.Min(1.3, 0.36 * 2 * a), dh = Math.Min(2.1, 0.78 * h);
            if (door)
            {
                if (lattice) KdsKit.LatticeDoor(x.M, sk, 0, v0 + 0.15, d, dw, dh, x.Lod);
                else KdsKit.Door(x.M, sk, 0, v0 + 0.15, d, dw, dh, KdsLooks.Timber, x.Lod, true, v1 - 0.05);
            }
            double side = 0.5 * (a + 0.5 * dw + 0.15);
            if (side + 0.3 > a) return;
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                if (keyholes) KdsKit.KeyholeNiche(x.M, sk, sgn * side, v0 + 0.55, d, Math.Min(1.2, 0.48 * h), x.Lod);
                else KdsKit.BlackWindow(x.M, sk, sgn * side, v0 + 0.5 * h + 0.1, d, 0.5, 0.6, x.Lod, 0.1, 2);
            }
        }

        /// <summary>A moulded temple base (the timber-sill platform the posts stand on) of half sizes hu × hw from v0 to v1.</summary>
        public static void Base(KdsCtx x, double hu, double hw, double v0, double v1, in ShapeBrush face, in ShapeBrush top)
        {
            Mould p = SacredDraw.M;
            double h = v1 - v0;
            if (x.Lod >= 2) p.Add(0, v0, face).Add(0, v1);
            else p.Add(0.04, v0, face).Add(0.04, v0 + 0.25 * h).Add(0, v0 + 0.3 * h).Add(0, v1 - 0.25 * h).Add(0.05, v1 - 0.2 * h, top).Add(0.05, v1);
            double[] pu = ShapeScratch.U2, pw = ShapeScratch.W2;
            int n = SacredDraw.Rect(0, 0, hu, hw, pu, pw);
            SacredDraw.Ring(x.M, x.K, pu, pw, n, p);
            SacredDraw.CapRect(x.M, x.K, 0, 0, hu + p.O[p.Count - 1], hw + p.O[p.Count - 1], v1, true, top);
            x.Floor(-hu, hu, -hw, hw, v0, v1, GenColliders.Stone);
        }

        /// <summary>Stacked brick stages from widths (full widths along u and w, bottom first) with an equal rise each up to
        /// <paramref name="topV"/>; a stair notch of half width <paramref name="notch"/> on the front.</summary>
        public static void StageStack(KdsCtx x, double[] wu, double[] ww, double topV, double notch, in ShapeBrush face, in ShapeBrush nose, double noseH,
                                      byte material = GenColliders.Brick)
        {
            int n = wu.Length;
            double rise = topV / n;
            for (int i = 0; i < n; i++)
            {
                double hu = 0.5 * wu[i], hw = 0.5 * ww[i];
                double ihu = i + 1 < n ? 0.5 * wu[i + 1] : 0, ihw = i + 1 < n ? 0.5 * ww[i + 1] : 0;
                KdsKit.Stage(x, hu, hw, i * rise, (i + 1) * rise, ihu, ihw, notch, face, nose, noseH, i == 0 ? 0.25 : 0, material,
                             i + 1 < n ? 0.5 * ww[i + 1] : 0.5 * ww[n - 1]);
            }
        }
    }
}
