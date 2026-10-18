using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsTemples
{
    /// <summary>
    /// The rest houses, pavilions and small shrines of the square (kds-temples.md T2, T3, T11, T12, T13, T15): Silyan
    /// Sattal (the "lion rest house": closed timber ground floor under a valanced pent roof, the white scalloped parapet with
    /// gilt winged lions on its corner piers, the open veranda storey and the broad hipped roof with its gilt flame-arch
    /// pinnacle), Ashok Binayak (a brass cube under a gilt roof without a finial, inside a brass-bar cage hung with bells),
    /// the Laxmi Narayan Sattal (open arcade under a broad roof, brick upper storey, a small top roof), the Taleju Bell
    /// (white block, side stair, two buff stone pillars, the big bronze bell, a small valanced roof), the Saraswati phalcha
    /// (1.2 m stone plinth, eight posts, a wall at the back) and the Big Drums (red-ochre brick, an open upper bay with
    /// two great drums lying on their sides).
    /// </summary>
    internal static class Pavilions
    {
        public const float SilyanTopM = 11.2f;
        public const float AshokTopM = 4.4f;
        public const float LaxmiSattalTopM = 9.6f;
        public const float BellTopM = 8.5f;
        public const float SaraswatiTopM = 6.6f;
        public const float DrumsTopM = 6.9f;

        private static double Clamp(double v, double lo, double hi)
        {
            return v < lo ? lo : v > hi ? hi : v;
        }

        // -------------------------------------------------------------------------------------------------------------
        // T2 Silyan Sattal
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>Silyan Sattal (OSM w183559499; DANAM KAT1990 plan 13.10 × 7.37 m), long front to the south.</summary>
        public static double Silyan(KdsCtx x)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            double hu = x.PlanHu > 1 ? Clamp(x.PlanHu - 0.75, 5.0, 6.9) : 6.55, hw = x.PlanHw > 1 ? Clamp(x.PlanHw - 1.0, 3.0, 4.2) : 3.69;
            const double p0 = 0.3, g1 = 3.25, par0 = 3.72, par1 = 4.62, postTop = 7.25, eave = 7.5, ridge = 10.0;
            if (lod >= 3)
            {
                KdsKit.Lod3(x, hu + 0.3, hw + 0.3, p0, hu, hw, new[] { hu + 1.5 }, new[] { eave }, new double[0], ridge, KdsLooks.StonePlinth, KdsLooks.Timber,
                            KdsLooks.TileOld);
                return ridge;
            }
            // A low stone step round the house.
            KdsKit.Stage(x, hu + 0.35, hw + 0.35, 0, p0, 0, 0, 0, KdsLooks.StonePlinth, KdsLooks.Nosing, 0.06, 0.2, GenColliders.Stone);
            // Ground floor: closed dark timber shutters and shop fronts all round.
            KdsKit.Walls(x, hu, hw, p0, g1 + 0.35, KdsLooks.Timber, true, false, GenColliders.Wood);
            for (int s = 0; s < 4; s++)
                KdsKit.Shutters(m, KdsKit.Side(x.K, s), KdsKit.Along(s, hu, hw), KdsKit.Depth(s, hu, hw), p0 + 0.1, g1 - 0.05, 1.1, Math.Max(1, lod), KdsLooks.TimberRed);
            // The pent (skirt) roof with its valance.
            KdsKit.Roof(x, hu + 1.15, hw + 1.15, g1, hu + 0.1, hw + 0.1, par0, hu, hw, g1 + 0.3, KdsLooks.TileOld, 0.36);
            // The white parapet band with the scalloped crenellation; the white corner piers with the gilt winged lions.
            Mould pp = SacredDraw.M;
            if (lod >= 2) pp.Add(0.12, par0 - 0.05, KdsLooks.Plaster).Add(0.12, par1);
            else
                pp.Add(0.16, par0 - 0.05, KdsLooks.PlasterShade).Add(0.16, par0 + 0.08).Add(0.12, par0 + 0.1, KdsLooks.Plaster).Add(0.12, par1 - 0.12)
                  .Add(0.17, par1 - 0.1).Add(0.17, par1);
            SacredDraw.RectRing(m, x.K, 0, 0, hu, hw, pp, true, KdsLooks.Plaster);
            x.Floor(-hu - 0.15, hu + 0.15, -hw - 0.15, hw + 0.15, par0, par1, GenColliders.Wood);
            if (lod <= 1)
            {
                for (int s = 0; s < 4; s++)
                {
                    Affine3 sk = KdsKit.Side(x.K, s);
                    double a = KdsKit.Along(s, hu, hw) - 0.4, d = KdsKit.Depth(s, hu, hw) + 0.17;
                    int n = Math.Max(2, (int)Math.Round(2 * a / 0.78));
                    if (lod == 1) ScallopRow(m, sk, a, par1 - 0.02, d - 0.22, 0.42, n, 0.34);
                    else
                    {
                        for (int i = 0; i < n; i++)
                        {
                            double c = -a + 2 * a * (i + 0.5) / n;
                            Scallop(m, sk, c, par1 - 0.02, d - 0.22, 0.42, 2 * a / n, 0.34, lod);
                        }
                    }
                }
            }
            x.Wall(-hu - 0.15, hw + 0.15, hu + 0.15, hw + 0.15, par1, par1 + 0.35, 0.1);
            for (int cu = -1; cu <= 1; cu += 2)
            {
                for (int cw = -1; cw <= 1; cw += 2)
                {
                    double u = cu * (hu + 0.1), w = cw * (hw + 0.1);
                    Mould pier = SacredDraw.M;
                    pier.Add(0.4, par0 - 0.05, KdsLooks.Plaster).Add(0.4, 5.55);
                    if (lod <= 1) pier.Add(0.48, 5.62, KdsLooks.PlasterShade).Add(0.48, 5.75);
                    SacredDraw.RectRing(m, x.K, u, w, 0, 0, pier, true, KdsLooks.Plaster);
                    x.Solid(u - 0.44, u + 0.44, w - 0.44, w + 0.44, par0, 5.75, GenColliders.Brick);
                    int first = m.VertexCount;
                    // The gilt winged lions are big (KAT1990 06, 10): about 1.3 m, rearing over the corners.
                    Affine3 lk = SacredDraw.Turn(x.K, u, 5.75, w, Math.Atan2(-cu, cw));
                    SacredFigures.Guardian(m, lk, 0, 0, 0, 1.3, GuardianKind.Griffin, true, SacredFigures.Generic, Math.Min(2, lod + 1), 0);
                    KdsKit.Recolour(m, first, KdsLooks.Gilt);
                }
            }
            // The open veranda storey: carved posts, the balcony rail and the closed inner wall with lattice windows.
            double vu = hu - 0.15, vw = hw - 0.15, iu = hu - 1.05, iw = hw - 1.05;
            int wasPosts = x.Coarser();
            KdsKit.PostRing(x, vu, vw, par1, postTop, 9, 5, 0.2, KdsLooks.Timber);
            x.Restore(wasPosts);
            if (lod <= 1)
            {
                KdsKit.Balustrade(x, -vu, vw, vu, vw, par1, 0.82, KdsLooks.Timber);
                KdsKit.Balustrade(x, vu, -vw, -vu, -vw, par1, 0.82, KdsLooks.Timber);
                KdsKit.Balustrade(x, -vu, -vw, -vu, vw, par1, 0.82, KdsLooks.Timber);
                KdsKit.Balustrade(x, vu, vw, vu, -vw, par1, 0.82, KdsLooks.Timber);
            }
            // The gallery's back walls are white lime plaster with dark doors and lattice windows (KAT1990 02, 10).
            KdsKit.Walls(x, iu, iw, par1, eave + 0.3, KdsLooks.Plaster, true, false, GenColliders.Wood);
            for (int s = 0; s < 4; s++)
            {
                Affine3 sk = KdsKit.Side(x.K, s);
                double a = KdsKit.Along(s, iu, iw), d = KdsKit.Depth(s, iu, iw);
                int n = s % 2 == 0 ? 4 : 2;
                if (lod >= 2) n = 1;
                for (int i = 0; i < n; i++)
                    SacredParts.LatticeWindow(m, sk, -a + 2 * a * (i + 0.5) / n, par1 + 1.3, d, 0.75, 1.1, KdsLooks.WindowBlack, lod);
            }
            // Veranda ceiling and the beam over the posts.
            SacredDraw.Quad(m, x.K, -vu, postTop, -vw, vu, postTop, -vw, vu, postTop, vw, -vu, postTop, vw, 0, -1, 0, Looks.Ao(KdsLooks.Beam, 0.5f));
            KdsKit.Cornice(x, vu + 0.05, vw + 0.05, postTop, postTop + 0.25, 0.1, KdsLooks.Timber);
            // The broad hipped roof, struts, bells and the gilt flame-arch pinnacle on the ridge.
            RoofSpec r = KdsKit.Roof(x, hu + 1.5, hw + 1.5, eave, Math.Max(0.3, hu - hw + 0.12), 0.12, ridge, vu, vw, postTop + 0.25, KdsLooks.TileOld, 0.42);
            int st = KdsKit.Struts(x, r, par1 + 1.2, 9, 5, false, 0x53494C59u);
            if (x.Stats != null) x.Stats.Struts += st;
            if (lod == 0)
            {
                int b = KdsKit.Bells(x, r, 1.3, 1.0);
                if (x.Stats != null) x.Stats.Bells += b;
            }
            return KdsKit.FlamePinnacle(x, ridge - 0.12, SilyanTopM - ridge + 0.12);
        }

        /// <summary>One period (width p) of the scalloped parapet crest on the plane d of side frame sk (KAT1990 06, 10): a
        /// flat-topped white merlon of width mw between two half round scoops cut down into the wall top, from v0 up to h.</summary>
        /// <summary>The scalloped crest of one parapet side seen from further off (LOD1): one extruded band from −a to a whose
        /// top runs flat over each of the n merlons (mw wide) and dips in a notch between them.</summary>
        private static void ScallopRow(MeshData m, in Affine3 sk, double a, double v0, double d, double h, int n, double mw)
        {
            double[] px = ShapeScratch.U, pz = ShapeScratch.W;
            n = Math.Min(n, (px.Length - 3) / 3);
            double p = 2 * a / n, rn = 0.5 * (p - mw), top = v0 + h;
            int c = 0;
            px[c] = -a;
            pz[c++] = -v0;
            px[c] = a;
            pz[c++] = -v0;
            px[c] = a;
            pz[c++] = -(top - rn);
            for (int i = n - 1; i >= 0; i--)
            {
                double u = -a + p * (i + 0.5);
                px[c] = u + 0.5 * mw;
                pz[c++] = -top;
                px[c] = u - 0.5 * mw;
                pz[c++] = -top;
                px[c] = u - 0.5 * p;
                pz[c++] = -(top - rn);
            }
            Shapes.BevelExtrude(m, SacredDraw.Basis(sk, 0, 0, d, 1, 0, 0, 0, 0, 1), KdsLooks.Plaster, px, pz, c, 0.24, 0, 0, BevelStyle.Chamfer, true, false);
        }

        private static void Scallop(MeshData m, in Affine3 sk, double u, double v0, double d, double h, double p, double mw, int lod)
        {
            double[] px = ShapeScratch.U, pz = ShapeScratch.W;
            int seg = lod == 0 ? 3 : 2, n = 0;
            double rn = 0.5 * (p - mw), top = v0 + h;
            px[n] = u - 0.5 * p;
            pz[n++] = -v0;
            px[n] = u + 0.5 * p;
            pz[n++] = -v0;
            // Right scoop: from its bottom up to the merlon's right corner.
            for (int i = 0; i <= seg; i++)
            {
                double a = -0.5 * Math.PI - 0.5 * Math.PI * i / seg;
                px[n] = u + 0.5 * p + rn * Math.Cos(a);
                pz[n++] = -(top + rn * Math.Sin(a));
            }
            // Left scoop: from the merlon's left corner down to its bottom.
            for (int i = 0; i <= seg; i++)
            {
                double a = -0.5 * Math.PI * i / seg;
                px[n] = u - 0.5 * p + rn * Math.Cos(a);
                pz[n++] = -(top + rn * Math.Sin(a));
            }
            Shapes.BevelExtrude(m, SacredDraw.Basis(sk, 0, 0, d, 1, 0, 0, 0, 0, 1), KdsLooks.Plaster, px, pz, n, 0.24, 0, 0, BevelStyle.Chamfer, true, false);
        }

        // -------------------------------------------------------------------------------------------------------------
        // T3 Ashok Binayak
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>Ashok Binayak (Maru Ganesh; DANAM KAT1930): a 2.4 × 2.6 m brass-clad shrine under a single gilt roof with
        /// no pinnacle, door south with a silver torana, inside a 2.8 m brass-bar cage with rows of small bells; the big bell
        /// on its frame to the east.</summary>
        public static double AshokBinayak(KdsCtx x)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            const double hu = 1.2, hw = 1.3, cu = 2.25, cw = 2.35, cageH = 2.95, b0 = 0.18, w0 = 0.45, w1 = 2.55;
            if (lod >= 3)
            {
                KdsKit.Lod3(x, cu, cw, b0, hu, hw, new[] { 1.95 }, new[] { 2.75 }, new double[0], AshokTopM, KdsLooks.StonePlinth, KdsLooks.Brass, KdsLooks.Gilt);
                return AshokTopM;
            }
            KdsKit.Stage(x, cu + 0.12, cw + 0.12, 0, b0, 0, 0, 0, KdsLooks.StonePlinth, KdsLooks.Nosing, 0.05, 0.15, GenColliders.Stone);
            KdsPagoda.Base(x, hu + 0.15, hw + 0.15, b0, w0, KdsLooks.Brass, KdsLooks.Gilt);
            SacredParts.BandedWall(m, x.K, hu, hw, w0, w1, KdsLooks.Brass, KdsLooks.Gilt, lod, true, true);
            x.Solid(-hu, hu, -hw, hw, w0, w1, GenColliders.Metal);
            for (int s = 0; s < 4; s++)
            {
                Affine3 sk = KdsKit.Side(x.K, s);
                double a = KdsKit.Along(s, hu, hw), d = KdsKit.Depth(s, hu, hw);
                // Carved brass pilasters at the corners.
                if (lod <= 1)
                {
                    SacredDraw.Box(m, sk, -a, -a + 0.16, w0, w1, d, d + 0.06, KdsLooks.Gilt, BoxFaces.Wall);
                    SacredDraw.Box(m, sk, a - 0.16, a, w0, w1, d, d + 0.06, KdsLooks.Gilt, BoxFaces.Wall);
                }
                if (s == 0)
                {
                    double top = KdsKit.Door(m, sk, 0, w0, d, 0.8, 1.55, KdsLooks.Brass, lod, false, w1);
                    if (lod <= 1) SacredParts.Torana(m, sk, 0, top + 0.02, d + 0.02, 1.15, Looks.Silver, lod);
                }
                else
                {
                    SacredParts.LatticeWindow(m, sk, 0, 1.5, d, 0.85, 0.9, KdsLooks.Gilt, lod);
                }
            }
            // The single gilt roof with flared corners and the red valance, no finial.
            KdsKit.Roof(x, 1.95, 2.05, 2.75, 0.12, 0.12, AshokTopM, hu, hw, w1, KdsLooks.Gilt, 0.28, true, 0.12);
            // A pair of small stone lions at the door, inside the cage.
            if (lod <= 1)
            {
                // Small and behind the cage bars: drawn one LOD coarser.
                int wasL = x.Coarser();
                KdsKit.Lion(x, -0.85, hw + 0.55, 0.12, 0.6, -1, false, 0.18);
                KdsKit.Lion(x, 0.85, hw + 0.55, 0.12, 0.6, 1, false, 0.18);
                x.Restore(wasL);
            }
            // The brass-bar cage with its rails and rows of small bells.
            Cage(x, cu, cw, b0, cageH);
            // The big bell on its iron frame to the east (+u).
            KdsKit.BellFrame(x, cu + 0.85, 0, 0, 2.2, 0.55);
            x.Solid(cu + 0.35, cu + 1.35, -0.2, 0.2, 0, 2.3, GenColliders.Metal);
            return AshokTopM;
        }

        private static void Cage(KdsCtx x, double cu, double cw, double v0, double h)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            // Aged, darkened brass bars (KAT1930 02-07), denser than they look from afar.
            ShapeBrush brass = Looks.Of(MeshColor.FromHex(0x8C7A4C), MaterialChannel.Metal), rail = Looks.Ao(brass, 0.85f);
            double pitch = lod == 0 ? 0.22 : lod == 1 ? 0.45 : 1.2, t = 0.016;
            for (int s = 0; s < 4; s++)
            {
                Affine3 sk = KdsKit.Side(x.K, s);
                double a = KdsKit.Along(s, cu, cw), d = KdsKit.Depth(s, cu, cw);
                int n = Math.Max(2, (int)(2 * a / pitch));
                for (int i = 0; i < n; i++)
                {
                    double u = -a + 2 * a * i / n;
                    double tt = i == 0 ? 0.05 : t;
                    SacredDraw.Box(m, sk, u - tt, u + tt, v0, v0 + h, d - tt, d + tt, i == 0 ? rail : brass, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
                }
                foreach (double rv in new[] { v0 + 0.12, v0 + 1.35, v0 + h - 0.05 })
                    SacredDraw.Box(m, sk, -a, a, rv - 0.035, rv + 0.035, d - 0.03, d + 0.03, rail, BoxFaces.Front | BoxFaces.Back | BoxFaces.Top | BoxFaces.Bottom);
                if (lod == 0)
                {
                    // Rows of small bells hung along the top rail and the middle rail.
                    int bells = (int)(2 * a / 0.55);
                    for (int i = 0; i < bells; i++)
                    {
                        double u = -a + 2 * a * (i + 0.5) / bells;
                        SmallBell(m, sk, u, v0 + h - 0.1, d + 0.06, 0.07);
                        if (i % 2 == 0) SmallBell(m, sk, u, v0 + 1.3, d + 0.06, 0.06);
                    }
                }
                double x0, z0, x1, z1;
                KdsCorner(s, cu, cw, out x0, out z0, out x1, out z1);
                x.Wall(x0, z0, x1, z1, 0, v0 + h, 0.05);
            }
        }

        private static void KdsCorner(int s, double hu, double hw, out double u0, out double w0, out double u1, out double w1)
        {
            SacredParts.OnRect(s, -1, hu, hw, out u0, out w0);
            SacredParts.OnRect(s, 1, hu, hw, out u1, out w1);
        }

        private static void SmallBell(MeshData m, in Affine3 sk, double u, double v, double d, double r)
        {
            var p = new Profile2(6);
            p.Add(0, 0, true).Add(r, 0, true).Add(0.75 * r, 0.6 * r).Add(0.35 * r, 1.2 * r).Add(0, 1.3 * r);
            Shapes.Lathe(m, SacredDraw.At(sk, u, v - 1.3 * r, d), KdsLooks.BellBronze, p, 5);
        }

        // -------------------------------------------------------------------------------------------------------------
        // T11 Laxmi Narayan Sattal
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>The Laxmi Narayan Sattal (OSM w183559497): an open post arcade under a broad pent roof (vegetable and flower
        /// sellers under it), a brick upper storey with carved windows under a hipped roof, a small top lantern roof.</summary>
        public static double LaxmiSattal(KdsCtx x)
        {
            double hu = x.PlanHu > 1 ? Clamp(x.PlanHu - 1.0, 4.0, 6.2) : 5.6, hw = x.PlanHw > 1 ? Clamp(x.PlanHw - 1.1, 2.6, 4.0) : 3.5;
            // The lane from Maru to Basantapur runs along its south-east corner: the arcade gives way to the corridor (the
            // house is drawn about the fitted centre).
            // The fit keeps the arcade's corner posts with their bracket capitals and the cornice (0.4 m beyond the post
            // line) out of the corridor's margin too.
            double fcu = 0, fcw = 0, ring = 0.4;
            hu += ring;
            hw += ring;
            KdsKit.FitRect(x, ref fcu, ref fcw, ref hu, ref hw, 3.2 + ring, 2.4 + ring);
            hu -= ring;
            hw -= ring;
            KitFrame keepKf = x.Kf;
            Affine3 keepK = x.K;
            x.Kf = keepKf.Offset(fcu, 0, fcw);
            x.K = SacredDraw.Xf(x.Kf);
            try
            {
                return LaxmiSattalAt(x, hu, hw);
            }
            finally
            {
                x.Kf = keepKf;
                x.K = keepK;
            }
        }

        private static double LaxmiSattalAt(KdsCtx x, double hu, double hw)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            double hu2 = hu - 1.25, hw2 = hw - 1.15;
            const double p0 = 0.3, arc = 2.45, eave1 = 2.55, wall2 = 3.75, eave2 = 6.5;
            if (lod >= 3)
            {
                KdsKit.Lod3(x, hu + 0.3, hw + 0.3, p0, hu, hw, new[] { hu + 1.5, hu2 + 1.3 }, new[] { eave1, eave2 }, new[] { wall2 }, 8.6, KdsLooks.StonePlinth,
                            KdsLooks.Brick, KdsLooks.TileOld);
                return 8.6;
            }
            KdsKit.Stage(x, hu + 0.55, hw + 0.55, 0, p0, 0, 0, 0, KdsLooks.BrickPlinth, KdsLooks.Nosing, 0.07, 0.2, GenColliders.Brick);
            // The arcade: a ring of carved posts round a brick core of rooms with shop openings.
            KdsKit.PostRing(x, hu, hw, p0, arc, 6, 4, 0.22, KdsLooks.Timber);
            double cu = hu - 1.4, cw = hw - 1.3;
            KdsKit.Walls(x, cu, cw, p0, wall2, KdsLooks.Brick, true, false);
            for (int s = 0; s < 4; s++)
            {
                Affine3 sk = KdsKit.Side(x.K, s);
                double a = KdsKit.Along(s, cu, cw), d = KdsKit.Depth(s, cu, cw);
                int n = s % 2 == 0 ? 3 : 2;
                if (lod >= 2) n = 1;
                for (int i = 0; i < n; i++)
                {
                    double u = -a + 2 * a * (i + 0.5) / n;
                    SacredDraw.Panel(m, sk, u - 0.55, p0 + 0.05, u + 0.55, p0 + 1.85, d + 0.012, Looks.Dark);
                    if (lod <= 1)
                    {
                        SacredDraw.Box(m, sk, u - 0.65, u + 0.65, p0 + 1.85, p0 + 1.97, d, d + 0.08, KdsLooks.Timber, BoxFaces.Wall | BoxFaces.Bottom);
                        SacredDraw.Box(m, sk, u - 0.65, u - 0.55, p0, p0 + 1.85, d, d + 0.08, KdsLooks.Timber, BoxFaces.Wall);
                        SacredDraw.Box(m, sk, u + 0.55, u + 0.65, p0, p0 + 1.85, d, d + 0.08, KdsLooks.Timber, BoxFaces.Wall);
                    }
                }
            }
            SacredDraw.Quad(m, x.K, -hu, arc, -hw, hu, arc, -hw, hu, arc, hw, -hu, arc, hw, 0, -1, 0, Looks.Ao(KdsLooks.Beam, 0.5f));
            KdsKit.Cornice(x, hu + 0.05, hw + 0.05, arc, arc + 0.22, 0.1, KdsLooks.Timber);
            // The broad lower roof up to the upper storey.
            // The broad pent roof sits low (2.5 m) over the stalls: over the lane its overhang is pulled back instead.
            bool asym;
            RoofSpec r1 = KdsKit.RoofFit(x, hu + 1.5, hw + 1.4, eave1, hu2 + 0.08, hw2 + 0.08, wall2 + 0.25, hu, hw, arc + 0.22, KdsLooks.TileMoss, 0.34, out asym);
            // The brick upper storey with its carved windows.
            KdsKit.Walls(x, hu2, hw2, wall2, eave2 + 0.3, KdsLooks.Brick, false, false);
            KdsPagoda.WindowBand(x, hu2, hw2, wall2 + 0.3, eave2 - 0.2, lod == 0 ? 4 : 2, 0.75, 1.05);
            if (lod <= 1) SacredParts.BayWindow(m, KdsKit.Side(x.K, 0), 0, wall2 + 0.55, hw2, Math.Min(3.2, 1.3 * hu2), 1.6, false, lod);
            KdsKit.Cornice(x, hu2 + 0.02, hw2 + 0.02, eave2 - 0.35, eave2 - 0.1, 0.08, KdsLooks.Timber);
            // Upper hipped roof, the small lantern and its roof.
            double lu = Math.Min(1.0, 0.45 * hu2), lw = Math.Min(0.8, 0.45 * hw2);
            RoofSpec r2 = KdsKit.Roof(x, hu2 + 1.3, hw2 + 1.3, eave2, lu + 0.05, lw + 0.05, 8.0, hu2, hw2, eave2 - 0.1, KdsLooks.TileOld, 0.34);
            KdsKit.Struts(x, r2, wall2 + 0.9, 4, 3, false, 0x4C534132u);
            SacredParts.BandedWall(m, x.K, lu, lw, 7.9, 8.4, KdsLooks.Timber, KdsLooks.Timber, lod, false, false);
            KdsKit.Roof(x, lu + 0.6, lw + 0.6, 8.35, 0.1, 0.1, 9.1, lu, lw, 8.35, KdsLooks.TileOld, 0.2);
            return KdsKit.GiltFinial(x, 9.0, LaxmiSattalTopM - 9.0, 0.12);
        }

        // -------------------------------------------------------------------------------------------------------------
        // T12 Taleju Bell
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>The Taleju Bell (1797; OSM w183563646; DANAM KAT1840): a 3.5 m white plastered block with a brick-paved
        /// top and low parapet, a stair along its south-south-west side entered from the east-south-east end, two buff stone
        /// pillars with notched bracket capitals carrying a stone beam, the bronze bell (1.9 m mouth) and a small hipped tile
        /// roof with a red valance.</summary>
        public static double Bell(KdsCtx x)
        {
            // The OSM outline holds the block and its stair: the stair (with its outer wall, 1.22 m) runs inside the
            // outline's south-south-west (−u) edge, where the bell lane's corridor passes, and the block fills the rest.
            double full = x.PlanHu > 1 ? Clamp(x.PlanHu - 0.1, 3.0, 4.0) : 3.7, hw = x.PlanHw > 1 ? Clamp(x.PlanHw - 0.35, 2.4, 3.6) : 3.4;
            const double stairBand = 1.22;
            // The stair's outer wall follows the outline's −u edge where it runs (the box is a little wider than the
            // drawn outline there).
            double left = Math.Min(-0.7 * full, Math.Max(-full, OutlineLeft(x, -hw, hw) + 0.05)), right = full;
            KitFrame keepKf = x.Kf;
            Affine3 keepK = x.K;
            x.Kf = keepKf.Offset(0.5 * (left + stairBand + right), 0, 0);
            x.K = SacredDraw.Xf(x.Kf);
            try
            {
                return BellAt(x, 0.5 * (right - left - stairBand), hw);
            }
            finally
            {
                x.Kf = keepKf;
                x.K = keepK;
            }
        }

        /// <summary>The outline's −u boundary over w0..w1: the largest u at which a line of constant w first enters the
        /// outline (−∞ without an outline).</summary>
        private static double OutlineLeft(KdsCtx x, double w0, double w1)
        {
            if (x.OutU == null || x.OutU.Length < 3) return double.NegativeInfinity;
            double worst = double.NegativeInfinity;
            int n = x.OutU.Length;
            for (int q = 0; q <= 12; q++)
            {
                double w = w0 + (w1 - w0) * q / 12.0, best = double.PositiveInfinity;
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    double wa = x.OutW[j], wb = x.OutW[i];
                    if ((wa > w) == (wb > w)) continue;
                    double u = x.OutU[j] + (x.OutU[i] - x.OutU[j]) * (w - wa) / (wb - wa);
                    best = Math.Min(best, u);
                }
                if (!double.IsPositiveInfinity(best)) worst = Math.Max(worst, best);
            }
            return worst;
        }

        private static double BellAt(KdsCtx x, double hu, double hw)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            const double top = 3.5, pil = 1.55, beamV = 6.55;
            if (lod >= 3)
            {
                KdsKit.Lod3(x, hu, hw, top, 1.95, 0.45, new[] { 2.45 }, new[] { 6.95 }, new double[0], 8.3, KdsLooks.Plaster, KdsLooks.StoneBuff, KdsLooks.TileOld);
                return 8.3;
            }
            // The white block (sunk a little into the ground), its paved top.
            SacredParts.BandedWall(m, x.K, hu, hw, -0.2, top, KdsLooks.Plaster, KdsLooks.PlasterShade, lod, true, false);
            SacredDraw.CapRect(m, x.K, 0, 0, hu, hw, top, true, KdsLooks.BrickBuff);
            x.Floor(-hu, hu, -hw, hw, -0.2, top, GenColliders.Brick);
            // The low parapet, open over the stair landing at the west end of the south-south-west (−u) side.
            double landing = 1.2, pt = 0.2, ph = 0.55;
            ShapeBrush wht = KdsLooks.Plaster;
            BoxFaces pf = BoxFaces.All & ~BoxFaces.Bottom;
            SacredDraw.Box(m, x.K, -hu, hu, top, top + ph, hw - pt, hw, wht, pf);
            SacredDraw.Box(m, x.K, -hu, hu, top, top + ph, -hw, -hw + pt, wht, pf);
            SacredDraw.Box(m, x.K, hu - pt, hu, top, top + ph, -hw + pt, hw - pt, wht, pf);
            SacredDraw.Box(m, x.K, -hu, -hu + pt, top, top + ph, -hw + landing, hw - pt, wht, pf);
            x.Solid(-hu, hu, hw - pt, hw, top, top + ph, GenColliders.Brick);
            x.Solid(-hu, hu, -hw, -hw + pt, top, top + ph, GenColliders.Brick);
            x.Solid(hu - pt, hu, -hw, hw, top, top + ph, GenColliders.Brick);
            x.Solid(-hu, -hu + pt, -hw + landing, hw, top, top + ph, GenColliders.Brick);
            // The stair: 13 risers along the −u side, from the foot at the east-south-east (+w) up to a landing at the west end.
            double sx = -hu - 0.6, wTop = -hw + landing, run = 13 * 0.27;
            if (wTop + run > hw) run = hw - wTop;
            KitFrame skf = x.Kf.Offset(sx, 0, 0);
            SacredParts.Stair(m, x.C, skf, SacredDraw.Xf(skf), 0.5, wTop, run, 0, top, 0.12, 0.85, KdsLooks.BrickBuff, KdsLooks.Plaster, lod, GenColliders.Brick,
                              lod <= 1 ? 13 : 4);
            SacredDraw.Box(m, x.K, sx - 0.62, -hu, -0.2, top, -hw, wTop, wht, BoxFaces.All & ~BoxFaces.Bottom & ~BoxFaces.Right);
            SacredDraw.Box(m, x.K, sx - 0.62, sx - 0.5, top, top + 0.85, -hw, wTop, wht, pf);
            x.Floor(sx - 0.62, -hu, -hw, wTop, -0.2, top, GenColliders.Brick);
            x.Solid(sx - 0.62, sx - 0.5, -hw, wTop, top, top + 0.85, GenColliders.Brick);
            // The two buff stone pillars with notched bracket capitals and the stone beam.
            for (int sg = -1; sg <= 1; sg += 2)
            {
                double u = sg * pil;
                if (lod <= 1)
                {
                    Shapes.RoundedBox(m, SacredDraw.At(x.K, u, top + 0.12, 0), KdsLooks.StoneBuff, 0.95, 0.24, 0.95, 0.03, 1);
                    Shapes.RoundedBox(m, SacredDraw.At(x.K, u, 0.5 * (top + 0.24 + beamV - 0.45), 0), KdsLooks.StoneBuff, 0.8, beamV - 0.45 - top - 0.24, 0.8, 0.04, 1);
                    // Bracket capital: a notched block stepping out along the beam.
                    SacredDraw.Box(m, x.K, u - 0.48, u + 0.48, beamV - 0.45, beamV - 0.25, -0.42, 0.42, KdsLooks.StoneBuff, BoxFaces.All);
                    SacredDraw.Box(m, x.K, u - 0.62, u + 0.62, beamV - 0.25, beamV, -0.44, 0.44, KdsLooks.StoneBuff, BoxFaces.All);
                    if (lod == 0)
                    {
                        SacredDraw.Box(m, x.K, u - 0.41, u + 0.41, beamV - 1.05, beamV - 0.95, -0.41, 0.41, Looks.Ao(KdsLooks.StoneBuff, 0.8f), BoxFaces.Wall);
                        SacredDraw.Box(m, x.K, u - 0.41, u + 0.41, beamV - 0.7, beamV - 0.6, -0.41, 0.41, Looks.Ao(KdsLooks.StoneBuff, 0.8f), BoxFaces.Wall);
                    }
                }
                else
                {
                    SacredDraw.Box(m, x.K, u - 0.4, u + 0.4, top, beamV, -0.4, 0.4, KdsLooks.StoneBuff, BoxFaces.Wall);
                }
                x.Solid(u - 0.4, u + 0.4, -0.4, 0.4, top, beamV, GenColliders.Stone);
            }
            SacredDraw.Box(m, x.K, -pil - 0.75, pil + 0.75, beamV, beamV + 0.35, -0.38, 0.38, KdsLooks.StoneBuff, BoxFaces.All);
            KdsKit.BigBell(x, 0, beamV - 0.08, 0, 1.9);
            x.Cylinder(0, 0, beamV - 1.9, beamV, 0.95, true);
            // The small hipped roof over the pillars.
            RoofSpec r = KdsKit.Roof(x, pil + 0.95, 1.45, beamV + 0.4, pil - 0.4, 0.12, 8.2, pil + 0.75, 0.38, beamV + 0.35, KdsLooks.TileOld, 0.3);
            if (lod == 0) KdsKit.Bells(x, r, 1.2, 0.8);
            if (lod <= 1) SacredParts.Gajur(m, x.K, 8.1, BellTopM - 8.1, 0.08, KdsLooks.Gilt, lod);
            return BellTopM;
        }

        // -------------------------------------------------------------------------------------------------------------
        // T13 Saraswati phalcha
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>The Saraswati phalcha (DANAM KAT1820): a square rest platform on a 1.2 m stone plinth with a front stair,
        /// eight carved posts on its three open sides, a brick wall at the back holding the small walled sanctum, carved beams,
        /// a tile roof with plain struts and a metal finial.</summary>
        public static double Saraswati(KdsCtx x)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            double h = x.PlanHu > 1 ? Clamp(Math.Min(x.PlanHu, x.PlanHw) - 0.1, 2.3, 3.2) : 2.9;
            const double pl = 1.2, postTop = 3.45;
            if (lod >= 3)
            {
                KdsKit.Lod3(x, h, h, pl, h - 0.2, h - 0.2, new[] { h + 0.85 }, new[] { 3.75 }, new double[0], 5.7, KdsLooks.StonePlinth, KdsLooks.Timber, KdsLooks.TileOld);
                return 5.7;
            }
            KdsKit.Stage(x, h, h, 0, pl, 0, 0, 0, KdsLooks.StonePlinth, KdsLooks.Nosing, 0.12, 0.25, GenColliders.Stone);
            // The front stair, shortened out of the lane's corridor.
            double run = 1.7;
            while (run > 0.9 && x.CorridorDistance(0, h + run) < SacredClearance.MarginM) run -= 0.2;
            KdsKit.Stair(x, 0.6, h, run, 0, pl, 0.16, 0.3, KdsLooks.StonePlinth, KdsLooks.StonePlinth, 6);
            // The back wall with the sanctum.
            double pi = h - 0.2;
            Mould bw = SacredDraw.M;
            bw.Add(0, pl, KdsLooks.Brick).Add(0, postTop + 0.3);
            SacredDraw.RectRing(m, x.K, 0, -h + 0.2, h - 0.05, 0.2, bw, true, KdsLooks.Brick);
            x.Solid(-h, h, -h, -h + 0.4, pl, postTop + 0.3, GenColliders.Brick);
            SacredDraw.Box(m, x.K, -0.75, 0.75, pl, 2.95, -h + 0.4, -h + 1.15, KdsLooks.Brick, BoxFaces.All & ~BoxFaces.Bottom & ~BoxFaces.Back);
            x.Solid(-0.75, 0.75, -h + 0.4, -h + 1.15, pl, 2.95, GenColliders.Brick);
            KdsKit.Door(m, KdsKit.Side(x.K, 0), 0, pl + 0.1, -h + 1.15, 0.55, 1.1, KdsLooks.Timber, lod, true, 2.95);
            // Eight posts: four on the front, two on each side.
            for (int i = 0; i < 4; i++) KdsKit.Post(x, -pi + 2 * pi * i / 3, pi, pl, postTop, 0.2, KdsLooks.Timber);
            for (int sg = -1; sg <= 1; sg += 2)
            {
                KdsKit.Post(x, sg * pi, pi - (2 * pi - 0.4) / 3, pl, postTop, 0.2, KdsLooks.Timber);
                KdsKit.Post(x, sg * pi, pi - 2 * (2 * pi - 0.4) / 3, pl, postTop, 0.2, KdsLooks.Timber);
            }
            // Carved beams (ninah) on the posts and the ceiling.
            KdsKit.Cornice(x, pi + 0.08, pi + 0.08, postTop, postTop + 0.3, 0.1, KdsLooks.Timber);
            SacredDraw.Quad(m, x.K, -pi, postTop, -h + 0.4, pi, postTop, -h + 0.4, pi, postTop, pi, -pi, postTop, pi, 0, -1, 0, Looks.Ao(KdsLooks.Beam, 0.5f));
            // The tile roof, plain struts and the metal finial.
            RoofSpec r = KdsKit.Roof(x, h + 0.85, h + 0.85, 3.75, 0.18, 0.18, 5.65, pi + 0.08, pi + 0.08, postTop + 0.3, KdsLooks.TileOld, 0.3);
            KdsKit.Struts(x, r, 2.5, 3, 3, false, 0x53415253u);
            if (lod == 0) KdsKit.Bells(x, r, 1.1, 0.8);
            return SacredParts.Gajur(m, x.K, 5.55, SaraswatiTopM - 5.55, 0.11, KdsLooks.Brass, lod);
        }

        // -------------------------------------------------------------------------------------------------------------
        // T15 Big Drums
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>The Big Drums (Nagara, 1800; OSM w907080046): a small two-storey red-ochre brick house, a small door on
        /// the ground floor, the open upper bay between brick pilasters and carved brick cornices with two huge drums lying on
        /// their sides (cream skins painted in red and colours, red rims), a hipped tile roof with a red valance.</summary>
        public static double Drums(KdsCtx x)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            double hu = x.PlanHu > 1 ? Clamp(x.PlanHu - 0.1, 1.9, 3.0) : 2.4, hw = x.PlanHw > 1 ? Clamp(x.PlanHw - 0.15, 2.0, 4.0) : 3.0;
            const double p0 = 0.25, g1 = 3.2, f1 = 3.45, c1 = 5.55, eave = 5.75;
            if (lod >= 3)
            {
                KdsKit.Lod3(x, hu, hw, p0, hu - 0.05, hw - 0.05, new[] { hu + 0.85 }, new[] { eave }, new double[0], DrumsTopM, KdsLooks.BrickOchre,
                            KdsLooks.BrickOchre, KdsLooks.TileOld);
                return DrumsTopM;
            }
            ShapeBrush brick = KdsLooks.BrickOchre;
            KdsKit.Stage(x, hu + 0.15, hw + 0.15, 0, p0, 0, 0, 0, KdsLooks.BrickPlinth, KdsLooks.Nosing, 0.06, 0.15, GenColliders.Brick);
            SacredParts.BandedWall(m, x.K, hu, hw, p0, f1, brick, KdsLooks.Timber, lod, true, false);
            x.Solid(-hu, hu, -hw, hw, p0, f1, GenColliders.Brick);
            Affine3 front = KdsKit.Side(x.K, 0);
            KdsKit.Door(m, front, 0, p0 + 0.05, hw, 0.8, 1.7, KdsLooks.Timber, lod, false);
            // Carved brick cornice under the bay.
            KdsKit.Cornice(x, hu + 0.02, hw + 0.02, g1, f1 + 0.1, 0.12, Looks.Ao(brick, 0.92f));
            // The upper storey: three closed sides, the open bay on the front between pilasters.
            double t = 0.35, pw = Math.Min(0.6, 0.18 * hu * 2);
            BoxFaces wf = BoxFaces.All & ~BoxFaces.Bottom;
            SacredDraw.Box(m, x.K, -hu, hu, f1, c1, -hw, -hw + t, brick, wf);
            SacredDraw.Box(m, x.K, -hu, -hu + t, f1, c1, -hw + t, hw, brick, wf);
            SacredDraw.Box(m, x.K, hu - t, hu, f1, c1, -hw + t, hw, brick, wf);
            SacredDraw.Box(m, x.K, -hu + t, -hu + pw, f1, c1, hw - t, hw, brick, wf);
            SacredDraw.Box(m, x.K, hu - pw, hu - t, f1, c1, hw - t, hw, brick, wf);
            SacredDraw.CapRect(m, x.K, 0, 0, hu - t, hw - t, f1 + 0.1, true, KdsLooks.BrickBuff);
            x.Solid(-hu, hu, -hw, -hw + t, f1, c1, GenColliders.Brick);
            x.Solid(-hu, -hu + t, -hw, hw, f1, c1, GenColliders.Brick);
            x.Solid(hu - t, hu, -hw, hw, f1, c1, GenColliders.Brick);
            // Pilaster capitals and the carved cornice over the bay; the joisted ceiling.
            if (lod <= 1)
            {
                for (int sg = -1; sg <= 1; sg += 2)
                {
                    double pu = sg * (hu - 0.5 * pw);
                    SacredDraw.Box(m, front, pu - 0.5 * pw - 0.06, pu + 0.5 * pw + 0.06, c1 - 0.35, c1 - 0.15, hw, hw + 0.1, Looks.Ao(brick, 0.9f), BoxFaces.Wall | BoxFaces.Bottom);
                    SacredDraw.Box(m, front, pu - 0.5 * pw - 0.04, pu + 0.5 * pw + 0.04, f1 + 0.1, f1 + 0.3, hw, hw + 0.08, Looks.Ao(brick, 0.9f), BoxFaces.Wall);
                }
            }
            KdsKit.Cornice(x, hu + 0.02, hw + 0.02, c1, c1 + 0.22, 0.14, Looks.Ao(brick, 0.92f));
            SacredDraw.Quad(m, x.K, -hu + t, c1, -hw + t, hu - t, c1, -hw + t, hu - t, c1, hw, -hu + t, c1, hw, 0, -1, 0, Looks.Ao(KdsLooks.Beam, 0.45f));
            if (lod == 0)
            {
                for (int i = 0; i < 6; i++)
                {
                    double ju = -hu + t + (2 * hu - 2 * t) * (i + 0.5) / 6;
                    SacredDraw.Box(m, x.K, ju - 0.05, ju + 0.05, c1 - 0.14, c1, -hw + t, hw, KdsLooks.Beam, BoxFaces.Bottom | BoxFaces.Left | BoxFaces.Right);
                }
            }
            SacredDraw.Panel(m, KdsKit.Side(x.K, 0), -hu + t, f1 + 0.1, hu - t, c1, -hw + t + 0.01, Looks.Of(MeshColor.FromHex(0x8A4A36), MaterialChannel.Brick));
            // The two great drums lying on their sides, skins tilted up toward the square.
            double r = Math.Min(0.78, 0.42 * (hu - t)), len = Math.Min(1.25, 0.9 * (hw - t));
            // They lie side by side, skins turned in toward each other, so from the square one sees the laced shells and
            // the painted heads aslant (c_bigdrum 03, 06).
            for (int sg = -1; sg <= 1; sg += 2)
                Drum(m, x.K, sg * 0.5 * (hu - t), f1 + 0.1 + r * 0.9, hw - 0.45 - 0.5 * len, r, len, -sg * 1.0, lod);
            // The small hipped roof with its valance, no struts (the photos).
            KdsKit.Roof(x, hu + 0.6, hw + 0.6, eave, 0.12, Math.Max(0.12, hw - hu + 0.12), DrumsTopM - 0.05, hu, hw, c1 + 0.22, KdsLooks.TileOld, 0.3);
            return DrumsTopM;
        }

        /// <summary>A great drum of radius r and length len lying on its side at (u, v, w), its axis along +w tipped up a
        /// little and toed by <paramref name="toe"/> rad: a wooden shell, cream painted skins with red and coloured
        /// circles, red rims.</summary>
        private static void Drum(MeshData m, in Affine3 k, double u, double v, double w, double r, double len, double toe, int lod)
        {
            double tilt = 0.2, ax = Math.Sin(toe) * Math.Cos(tilt), ay = Math.Sin(tilt), az = Math.Cos(toe) * Math.Cos(tilt);
            // A frame whose y is the drum axis, x across it on the ground plane.
            double xl = Math.Sqrt(az * az + ax * ax), xu = az / xl, xw = -ax / xl;
            Affine3 f = SacredDraw.Basis(k, u - 0.5 * len * ax, v - 0.5 * len * ay, w - 0.5 * len * az, xu, 0, xw, ax, ay, az);
            int seg = Kseg(lod);
            var p = new Profile2(10);
            p.Add(0, 0, true).Add(0.94 * r, 0, true).Add(r, 0.12 * len).Add(1.03 * r, 0.5 * len).Add(r, 0.88 * len).Add(0.94 * r, len, true).Add(0, len, true);
            Shapes.Lathe(m, f, Looks.Of(MeshColor.FromHex(0x6B3A24), MaterialChannel.Wood), p, seg);
            // The front skin, painted, and the red rims.
            ShapeBrush skin = Looks.Of(MeshColor.FromHex(0xD9CBAA), MaterialChannel.Paint), red = KdsLooks.PaintRed;
            Shapes.Cylinder(m, SacredDraw.At(f, 0, len, 0), skin, 0.92 * r, 0.02, seg);
            Shapes.Cylinder(m, SacredDraw.At(f, 0, -0.02, 0), skin, 0.92 * r, 0.02, seg);
            if (lod <= 1)
            {
                Shapes.Torus(m, SacredDraw.At(f, 0, len, 0), red, 0.95 * r, 0.05, seg, 4);
                Shapes.Torus(m, SacredDraw.At(f, 0, 0, 0), red, 0.95 * r, 0.05, seg, 4);
            }
            if (lod == 0)
            {
                Shapes.Cylinder(m, SacredDraw.At(f, 0, len + 0.02, 0), red, 0.5 * r, 0.006, seg);
                Shapes.Cylinder(m, SacredDraw.At(f, 0, len + 0.026, 0), Looks.Of(MeshColor.FromHex(0xE0B437), MaterialChannel.Paint), 0.36 * r, 0.006, seg);
                Shapes.Cylinder(m, SacredDraw.At(f, 0, len + 0.032, 0), Looks.Of(MeshColor.FromHex(0x2E7D4F), MaterialChannel.Paint), 0.22 * r, 0.006, seg);
                Shapes.Cylinder(m, SacredDraw.At(f, 0, len + 0.038, 0), red, 0.1 * r, 0.006, seg);
            }
        }

        private static int Kseg(int lod)
        {
            return lod == 0 ? 18 : lod == 1 ? 10 : 6;
        }
    }
}
