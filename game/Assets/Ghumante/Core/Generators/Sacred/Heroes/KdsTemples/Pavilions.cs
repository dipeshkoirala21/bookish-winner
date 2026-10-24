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
        public const float AshokTopM = 4.0f;
        public const float LaxmiSattalTopM = 9.6f;
        public const float BellTopM = 8.5f;
        public const float SaraswatiTopM = 6.6f;
        public const float DrumsTopM = 6.9f;

        /// <summary>Ashok Binayak's gilt copper, darkened by age and soot (KAT1930 05-08).</summary>
        private static readonly ShapeBrush AgedGilt = Looks.Of(MeshColor.FromHex(0xA8873A), MaterialChannel.Gilt);

        /// <summary>Ashok Binayak's brass cladding, dark and oily with age; only the carved edges catch the light (KAT1930 02, 04).</summary>
        private static readonly ShapeBrush AgedBrass = Looks.Of(MeshColor.FromHex(0x8E763C), MaterialChannel.Metal);

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
            const double p0 = 0.3, g1 = 3.25, par0 = 3.72, par1 = 4.62, gal = 5.2, postTop = 7.25, eave = 7.5, ridge = 10.0;
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
            x.Floor(-hu - 0.15, hu + 0.15, -hw - 0.15, hw + 0.15, par0, gal, GenColliders.Wood);
            // The gallery floor stands level with the crest (KAT1990 10): behind the merlons the white parapet rises to it,
            // so the gaps between them show white wall, and the open veranda starts over it.
            SacredDraw.RectRing(m, x.K, 0, 0, hu - 0.06, hw - 0.06, SacredDraw.M.Add(0, par1 - 0.05, Looks.Ao(KdsLooks.PlasterShade, 0.7f)).Add(0, gal), true, KdsLooks.Beam);
            if (lod <= 1)
            {
                for (int s = 0; s < 4; s++)
                {
                    Affine3 sk = KdsKit.Side(x.K, s);
                    // The crest (KAT1990 06, 10): square white merlons about 1.2 m apart with moulded caps, half-round
                    // arches cut deep between them.
                    double a = KdsKit.Along(s, hu, hw) - 0.4, d = KdsKit.Depth(s, hu, hw) + 0.17;
                    int n = Math.Max(2, (int)Math.Round(2 * a / 1.2));
                    ScallopRow(m, sk, a, par1 - 0.02, d - 0.22, 0.62, n, 0.42);
                    if (lod == 0)
                    {
                        for (int i = 0; i < n; i++)
                        {
                            double c = -a + 2 * a * (i + 0.5) / n;
                            // The merlon's cap: a projecting slab and a small rounded knob.
                            SacredDraw.Box(m, sk, c - 0.27, c + 0.27, par1 + 0.6, par1 + 0.7, d - 0.27, d + 0.07, KdsLooks.Plaster, BoxFaces.All & ~BoxFaces.Bottom);
                            Shapes.Cone(m, SacredDraw.At(sk, c, par1 + 0.7, d - 0.1) * Affine3.RotationY(0.25 * Math.PI), KdsLooks.Plaster, 0.17, 0.2, 4, 0, 0, false);
                        }
                    }
                }
            }
            x.Wall(-hu - 0.15, hw + 0.15, hu + 0.15, hw + 0.15, gal, gal + 0.35, 0.1);
            for (int cu = -1; cu <= 1; cu += 2)
            {
                for (int cw = -1; cw <= 1; cw += 2)
                {
                    double u = cu * (hu + 0.1), w = cw * (hw + 0.1);
                    Mould pier = SacredDraw.M;
                    pier.Add(0.4, par0 - 0.05, KdsLooks.Plaster).Add(0.4, gal + 0.25);
                    if (lod <= 1) pier.Add(0.48, gal + 0.32, KdsLooks.PlasterShade).Add(0.48, gal + 0.45);
                    SacredDraw.RectRing(m, x.K, u, w, 0, 0, pier, true, KdsLooks.Plaster);
                    x.Solid(u - 0.44, u + 0.44, w - 0.44, w + 0.44, par0, gal + 0.45, GenColliders.Brick);
                    int first = m.VertexCount;
                    // The gilt winged lions are big (KAT1990 06, 10): about 1.15 m, rearing on the corner piers under the eaves.
                    Affine3 lk = SacredDraw.Turn(x.K, u, lod <= 1 ? gal + 0.45 : gal + 0.25, w, Math.Atan2(-cu, cw));
                    SacredFigures.Guardian(m, lk, 0, 0, 0, 1.15, GuardianKind.Griffin, true, SacredFigures.Generic, Math.Min(2, lod + 1), 0);
                    KdsKit.Recolour(m, first, KdsLooks.Gilt);
                }
            }
            // The open veranda storey: carved posts, the balcony rail and the closed inner wall with lattice windows.
            double vu = hu - 0.15, vw = hw - 0.15, iu = hu - 1.05, iw = hw - 1.05;
            int wasPosts = x.Coarser();
            KdsKit.PostRing(x, vu, vw, gal, postTop, 9, 5, 0.2, KdsLooks.Timber);
            x.Restore(wasPosts);
            if (lod <= 1)
            {
                KdsKit.Balustrade(x, -vu, vw, vu, vw, gal, 0.82, KdsLooks.Timber);
                KdsKit.Balustrade(x, vu, -vw, -vu, -vw, gal, 0.82, KdsLooks.Timber);
                KdsKit.Balustrade(x, -vu, -vw, -vu, vw, gal, 0.82, KdsLooks.Timber);
                KdsKit.Balustrade(x, vu, vw, vu, -vw, gal, 0.82, KdsLooks.Timber);
            }
            // The gallery's back walls are white lime plaster with dark doors and lattice windows (KAT1990 02, 10).
            KdsKit.Walls(x, iu, iw, gal, eave + 0.3, KdsLooks.Plaster, true, false, GenColliders.Wood);
            for (int s = 0; s < 4; s++)
            {
                Affine3 sk = KdsKit.Side(x.K, s);
                double a = KdsKit.Along(s, iu, iw), d = KdsKit.Depth(s, iu, iw);
                int n = s % 2 == 0 ? 4 : 2;
                if (lod >= 2) n = 1;
                for (int i = 0; i < n; i++)
                    SacredParts.LatticeWindow(m, sk, -a + 2 * a * (i + 0.5) / n, gal + 1.05, d, 0.75, 1.1, KdsLooks.WindowBlack, lod);
            }
            // Veranda ceiling and the beam over the posts.
            SacredDraw.Quad(m, x.K, -vu, postTop, -vw, vu, postTop, -vw, vu, postTop, vw, -vu, postTop, vw, 0, -1, 0, Looks.Ao(KdsLooks.Beam, 0.5f));
            KdsKit.Cornice(x, vu + 0.05, vw + 0.05, postTop, postTop + 0.25, 0.1, KdsLooks.Timber);
            // The broad hipped roof, struts, bells and the gilt flame-arch pinnacle on the ridge.
            RoofSpec r = KdsKit.Roof(x, hu + 1.5, hw + 1.5, eave, Math.Max(0.3, hu - hw + 0.12), 0.12, ridge, vu, vw, postTop + 0.25, KdsLooks.TileOld, 0.42);
            int st = KdsKit.Struts(x, r, gal + 1.0, 9, 5, false, 0x53494C59u);
            if (x.Stats != null) x.Stats.Struts += st;
            if (lod == 0)
            {
                int b = KdsKit.Bells(x, r, 1.3, 1.0);
                if (x.Stats != null) x.Stats.Bells += b;
            }
            return KdsKit.FlamePinnacle(x, ridge - 0.12, SilyanTopM - ridge + 0.12);
        }

        /// <summary>The scalloped crest of one parapet side (KAT1990 06, 10): one extruded white band from −a to a whose top
        /// runs flat over each of the n merlons (mw wide) and dips in a notch between them.</summary>
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
                KdsKit.Lod3(x, cu, cw, b0, hu, hw, new[] { 2.05 }, new[] { 2.85 }, new double[0], AshokTopM, KdsLooks.StonePlinth, AgedBrass, AgedGilt);
                return AshokTopM;
            }
            KdsKit.Stage(x, cu + 0.12, cw + 0.12, 0, b0, 0, 0, 0, KdsLooks.StonePlinth, KdsLooks.Nosing, 0.05, 0.15, GenColliders.Stone);
            KdsPagoda.Base(x, hu + 0.15, hw + 0.15, b0, w0, AgedBrass, AgedGilt);
            SacredParts.BandedWall(m, x.K, hu, hw, w0, w1, AgedBrass, AgedGilt, lod, true, true);
            x.Solid(-hu, hu, -hw, hw, w0, w1, GenColliders.Metal);
            for (int s = 0; s < 4; s++)
            {
                Affine3 sk = KdsKit.Side(x.K, s);
                double a = KdsKit.Along(s, hu, hw), d = KdsKit.Depth(s, hu, hw);
                // Carved brass pilasters at the corners.
                if (lod <= 1)
                {
                    SacredDraw.Box(m, sk, -a, -a + 0.16, w0, w1, d, d + 0.06, AgedGilt, BoxFaces.Wall);
                    SacredDraw.Box(m, sk, a - 0.16, a, w0, w1, d, d + 0.06, AgedGilt, BoxFaces.Wall);
                }
                if (s == 0)
                {
                    double top = KdsKit.Door(m, sk, 0, w0, d, 0.8, 1.55, AgedBrass, lod, false, w1);
                    if (lod <= 1) SacredParts.Torana(m, sk, 0, top + 0.02, d + 0.02, 1.15, Looks.Silver, lod);
                }
                else if (s == 2)
                {
                    // The back (north) wall is plain dark brick, against the house behind (KAT1930 03).
                    SacredDraw.Panel(m, sk, -a + 0.16, w0, a - 0.16, w1, d + 0.01, Looks.Of(MeshColor.FromHex(0x6A3E2E), MaterialChannel.Brick));
                }
                else
                {
                    SacredParts.LatticeWindow(m, sk, 0, 1.5, d, 0.85, 0.9, AgedGilt, lod);
                }
            }
            // The single gilt copper roof, darkened with age, at a moderate pitch with its corners curled up into horns and the
            // red valance, no finial (KAT1930 05, 06).
            KdsKit.Roof(x, 2.05, 2.15, 2.85, 0.12, 0.12, AshokTopM, hu, hw, w1, AgedGilt, 0.28, true, 0.18, null, null, true);
            // A pair of small stone lions at the door, inside the cage.
            if (lod <= 1)
            {
                // Small and behind the cage bars: drawn one LOD coarser.
                int wasL = x.Coarser();
                KdsKit.Lion(x, -0.85, hw + 0.55, 0.12, 0.6, -1, false, 0.18);
                KdsKit.Lion(x, 0.85, hw + 0.55, 0.12, 0.6, 1, false, 0.18);
                x.Restore(wasL);
            }
            // The brass-bar cage with its rails, and inside it on the east the iron rack of small offering bells in three
            // rows (KAT1930 03, 07).
            Cage(x, cu, cw, b0, cageH);
            if (lod <= 1) BellRack(x, hu + 0.55, b0);
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
                double x0, z0, x1, z1;
                KdsCorner(s, cu, cw, out x0, out z0, out x1, out z1);
                x.Wall(x0, z0, x1, z1, 0, v0 + h, 0.05);
            }
        }

        /// <summary>The iron rack of small bells on the east (+u) side at u, standing on the cage floor at v0: two posts, three
        /// rails, seven bells a row (two rows; one at LOD1).</summary>
        private static void BellRack(KdsCtx x, double u, double v0)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            ShapeBrush iron = Looks.Of(MeshColor.FromHex(0x34302C), MaterialChannel.Metal);
            Affine3 sk = KdsKit.Side(x.K, 3);
            // In the east side frame the rack runs along u' (the frame's along-axis) at depth u from the shrine's centre.
            const double half = 0.75;
            for (int sg = -1; sg <= 1; sg += 2)
                SacredDraw.Box(m, sk, sg * half - 0.025, sg * half + 0.025, v0, v0 + 1.95, u - 0.025, u + 0.025, iron, BoxFaces.Wall);
            int rows = lod == 0 ? 2 : 1;
            for (int r = 0; r < rows; r++)
            {
                double rv = v0 + 1.9 - 0.42 * r;
                SacredDraw.Box(m, sk, -half, half, rv - 0.02, rv + 0.02, u - 0.02, u + 0.02, iron, BoxFaces.Front | BoxFaces.Back | BoxFaces.Top | BoxFaces.Bottom);
                for (int i = 0; i < 7; i++) SmallBell(m, sk, -half + 2 * half * (i + 0.5) / 7, rv - 0.03, u, 0.075);
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

        /// <summary>The Taleju Bell's limewashed pillars and their red dado (the photos; judge round 1).</summary>
        private static readonly ShapeBrush BellPillar = Looks.Of(MeshColor.FromHex(0xEEEBE3), MaterialChannel.Plaster);

        private static readonly ShapeBrush BellDado = Looks.Of(MeshColor.FromHex(0xA9232C), MaterialChannel.Paint);

        /// <summary>The Taleju Bell roof's dark old tiles.</summary>
        private static readonly ShapeBrush BellTile = Looks.Of(MeshColor.FromHex(0x5E4A40), MaterialChannel.RoofTile);

        private static double BellAt(KdsCtx x, double hu, double hw)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            const double top = 3.5, pil = 1.55, beamV = 6.55;
            if (lod >= 3)
            {
                KdsKit.Lod3(x, hu, hw, top, 1.95, 0.45, new[] { 2.45 }, new[] { 6.95 }, new double[0], 8.3, KdsLooks.Plaster, BellPillar, BellTile);
                return 8.3;
            }
            // The white block (sunk a little into the ground), its walls battered about 4 % (wider at the foot; the photos),
            // its paved top.
            Mould bm = SacredDraw.M;
            bm.Add(0.04 * (top + 0.2), -0.2, KdsLooks.Plaster).Add(0.04 * 0.3, top - 0.3).Add(0.02, top - 0.3, KdsLooks.PlasterShade).Add(0.02, top - 0.12)
              .Add(0, top - 0.12, KdsLooks.Plaster).Add(0, top);
            double[] bpu = ShapeScratch.U2, bpw = ShapeScratch.W2;
            SacredDraw.Ring(m, x.K, bpu, bpw, SacredDraw.Rect(0, 0, hu, hw, bpu, bpw), bm);
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
            // The two pillars, limewashed white with a red dado over their lower 0.9 m, stepping out in a three-step capital
            // under the stone beam (the photos; judge round 1).
            const double dado = 0.9, capH = 0.45;
            for (int sg = -1; sg <= 1; sg += 2)
            {
                double u = sg * pil;
                if (lod <= 1)
                {
                    Shapes.RoundedBox(m, SacredDraw.At(x.K, u, top + 0.12, 0), BellPillar, 0.95, 0.24, 0.95, 0.03, 1);
                    Shapes.RoundedBox(m, SacredDraw.At(x.K, u, top + 0.24 + 0.5 * dado, 0), BellDado, 0.81, dado, 0.81, 0.04, 1);
                    double sv = top + 0.24 + dado, ev = beamV - capH;
                    Shapes.RoundedBox(m, SacredDraw.At(x.K, u, 0.5 * (sv + ev), 0), BellPillar, 0.8, ev - sv, 0.8, 0.04, 1);
                }
                else
                {
                    SacredDraw.Box(m, x.K, u - 0.4, u + 0.4, top, top + 0.24 + dado, -0.4, 0.4, BellDado, BoxFaces.Wall);
                    SacredDraw.Box(m, x.K, u - 0.4, u + 0.4, top + 0.24 + dado, beamV - capH, -0.4, 0.4, BellPillar, BoxFaces.Wall);
                }
                // Three-step capital: each step wider along the beam and a little deeper.
                int steps = lod <= 1 ? 3 : 1;
                for (int i = 0; i < steps; i++)
                {
                    double v0 = beamV - capH + capH * i / steps, v1 = beamV - capH + capH * (i + 1) / steps;
                    double cu = steps == 1 ? 0.62 : 0.46 + 0.08 * i, cw = steps == 1 ? 0.44 : 0.41 + 0.015 * i;
                    SacredDraw.Box(m, x.K, u - cu, u + cu, v0, v1, -cw, cw, i % 2 == 0 ? BellPillar : Looks.Ao(BellPillar, 0.88f), BoxFaces.All & ~BoxFaces.Top);
                }
                x.Solid(u - 0.4, u + 0.4, -0.4, 0.4, top, beamV, GenColliders.Stone);
            }
            SacredDraw.Box(m, x.K, -pil - 0.75, pil + 0.75, beamV, beamV + 0.35, -0.38, 0.38, BellPillar, BoxFaces.All);
            KdsKit.BigBell(x, 0, beamV - 0.08, 0, 1.9);
            x.Cylinder(0, 0, beamV - 1.9, beamV, 0.95, true);
            // The small hipped roof over the pillars.
            // The broad dark roof (tiles #5E4A40, black fascia, corners lifted 0.3 m) with its thin crimson valance.
            RoofSpec r = KdsKit.Roof(x, pil + 0.95, 1.45, beamV + 0.4, pil - 0.4, 0.12, 8.2, pil + 0.75, 0.38, beamV + 0.35, BellTile, 0.22, false, 0.3,
                                     KdsLooks.WindowBlack);
            if (lod == 0) KdsKit.Bells(x, r, 1.2, 0.8);
            // The ridge cap over the hip's short ridge.
            SacredDraw.Box(m, x.K, -pil + 0.3, pil - 0.3, 8.1, 8.3, -0.2, 0.2, Looks.Of(MeshColor.Scale(BellTile.Color, 0.7f), MaterialChannel.RoofTile),
                           BoxFaces.All & ~BoxFaces.Bottom);
            if (lod <= 1) SacredParts.Gajur(m, x.K, 8.25, BellTopM - 8.25, 0.08, KdsLooks.Gilt, lod);
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

        /// <summary>The Big Drums' ochre-orange limewash over the brick (the photos; judge round 1: #C9603D).</summary>
        private static readonly ShapeBrush DrumWash = Looks.Of(MeshColor.FromHex(0xC9603D), MaterialChannel.Brick);

        /// <summary>The pale cotton fringe under the Big Drums' eave (judge round 1: no crimson valance here).</summary>
        private static readonly ShapeBrush DrumFringe = Looks.Of(MeshColor.FromHex(0xD8D2C4), MaterialChannel.Fabric);

        /// <summary>The drums' leather cover and lacing, and their dark iron hoops.</summary>
        private static readonly ShapeBrush DrumLeather = Looks.Of(MeshColor.FromHex(0x4A4744), MaterialChannel.Leather);

        private static readonly ShapeBrush DrumHoop = Looks.Of(MeshColor.FromHex(0x2E2B28), MaterialChannel.Metal);

        /// <summary>The Big Drums (Nagara, 1800; OSM w907080046): a small two-storey brick house washed ochre-orange, a small
        /// door on the ground floor, the wide open upper bay (about four fifths of the frontage, on a low sill) between slim
        /// brick pilasters and carved brick cornices, with the two huge drums lying side by side, their axes along the facade
        /// so the square sees their dark laced barrels (judge round 1; c_bigdrum 03, 06), and a hipped tile roof with a pale
        /// cotton fringe under its eave.</summary>
        public static double Drums(KdsCtx x)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            double hu = x.PlanHu > 1 ? Clamp(x.PlanHu - 0.1, 1.9, 3.0) : 2.4, hw = x.PlanHw > 1 ? Clamp(x.PlanHw - 0.15, 2.0, 4.0) : 3.0;
            const double p0 = 0.25, g1 = 2.7, f1 = 2.95, c1 = 5.55, eave = 5.75;
            ShapeBrush brick = DrumWash;
            if (lod >= 3)
            {
                KdsKit.Lod3(x, hu, hw, p0, hu - 0.05, hw - 0.05, new[] { hu + 0.85 }, new[] { eave }, new double[0], DrumsTopM, brick, brick, KdsLooks.TileOld);
                return DrumsTopM;
            }
            KdsKit.Stage(x, hu + 0.15, hw + 0.15, 0, p0, 0, 0, 0, KdsLooks.BrickPlinth, KdsLooks.Nosing, 0.06, 0.15, GenColliders.Brick);
            SacredParts.BandedWall(m, x.K, hu, hw, p0, f1, brick, KdsLooks.Timber, lod, true, false);
            x.Solid(-hu, hu, -hw, hw, p0, f1, GenColliders.Brick);
            Affine3 front = KdsKit.Side(x.K, 0);
            KdsKit.Door(m, front, 0, p0 + 0.05, hw, 0.8, 1.7, KdsLooks.Timber, lod, false);
            // Carved brick cornice under the bay (the low sill).
            KdsKit.Cornice(x, hu + 0.02, hw + 0.02, g1, f1 + 0.1, 0.12, Looks.Ao(brick, 0.92f));
            // The upper storey: three closed sides and the wide open bay on the front between slim pilasters (the opening
            // about 80 % of the frontage).
            double t = 0.35, pw = Math.Max(t + 0.05, 0.1 * 2 * hu);
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
            // Pilaster capitals and bases, the carved cornice over the bay; the joisted ceiling.
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
            // The two great drums (about 1.6 m long, 1.3 m across) lying side by side on low cradles, their axes along the
            // facade: from the square one sees the laced barrels, the painted heads face the side walls.
            double r = 0.65, len = Math.Min(1.6, hu - t - 0.12), dv = f1 + 0.1 + 0.12 + r, dw = Math.Min(hw - t - 0.15 - r, hw - 0.55 - r);
            for (int sg = -1; sg <= 1; sg += 2)
            {
                double du = sg * (0.5 * len + 0.06);
                Drum(m, x.K, du, dv, dw, r, len, lod);
                if (lod <= 1)
                {
                    for (int e = -1; e <= 1; e += 2)
                        SacredDraw.Box(m, x.K, du + e * 0.32 * len - 0.07, du + e * 0.32 * len + 0.07, f1 + 0.1, f1 + 0.1 + 0.12 + 0.25 * r, dw - 0.7 * r, dw + 0.7 * r,
                                       KdsLooks.Beam, BoxFaces.All & ~BoxFaces.Bottom);
                }
            }
            // The small hipped roof, no struts (the photos), and the pale fringe under its eave.
            RoofSpec rs = KdsKit.Roof(x, hu + 0.6, hw + 0.6, eave, 0.12, Math.Max(0.12, hw - hu + 0.12), DrumsTopM - 0.05, hu, hw, c1 + 0.22, KdsLooks.TileOld, 0);
            KdsKit.Valance(x, rs, 0.26, DrumFringe);
            return DrumsTopM;
        }

        /// <summary>A great drum of radius r and length len lying on its side at (u, v, w) with its axis along u: a barrel
        /// covered in dark leather, cream painted heads, dark iron hoops at both rims and 14 leather lacing cords
        /// zig-zagging from hoop to hoop over the barrel (LOD0; tubes of 0.015 m radius).</summary>
        private static void Drum(MeshData m, in Affine3 k, double u, double v, double w, double r, double len, int lod)
        {
            // A frame whose y is the drum axis (+u), x along +w, z down.
            Affine3 f = SacredDraw.Basis(k, u - 0.5 * len, v, w, 0, 0, 1, 1, 0, 0);
            int seg = Kseg(lod);
            var p = new Profile2(10);
            p.Add(0, 0, true).Add(0.94 * r, 0, true).Add(r, 0.12 * len).Add(1.03 * r, 0.5 * len).Add(r, 0.88 * len).Add(0.94 * r, len, true).Add(0, len, true);
            Shapes.Lathe(m, f, Looks.Ao(DrumLeather, 0.82f), p, seg);
            // The painted heads (seen aslant from the square through the bay) and the hoops.
            ShapeBrush skin = Looks.Of(MeshColor.FromHex(0xD9CBAA), MaterialChannel.Paint);
            Shapes.Cylinder(m, SacredDraw.At(f, 0, len, 0), skin, 0.92 * r, 0.02, seg);
            Shapes.Cylinder(m, SacredDraw.At(f, 0, -0.02, 0), skin, 0.92 * r, 0.02, seg);
            if (lod <= 1)
            {
                Shapes.Torus(m, SacredDraw.At(f, 0, len, 0), DrumHoop, 0.96 * r, 0.045, seg, 4);
                Shapes.Torus(m, SacredDraw.At(f, 0, 0, 0), DrumHoop, 0.96 * r, 0.045, seg, 4);
            }
            if (lod != 0) return;
            for (int e = 0; e < 2; e++)
            {
                double y = e == 0 ? len + 0.02 : -0.026;
                Shapes.Cylinder(m, SacredDraw.At(f, 0, y, 0), Looks.Of(MeshColor.FromHex(0xA8473A), MaterialChannel.Paint), 0.62 * r, 0.006, seg);
                Shapes.Cylinder(m, SacredDraw.At(f, 0, e == 0 ? y + 0.006 : y - 0.006, 0), Looks.Of(MeshColor.FromHex(0xE3D6B8), MaterialChannel.Paint), 0.5 * r, 0.006, seg);
            }
            // The lacing: each cord runs from one hoop to the other and back (a zig-zag round the barrel), in two straight
            // pieces per run that pass over the barrel's belly.
            const int laces = 14;
            for (int i = 0; i < laces; i++)
            {
                double a0 = 2 * Math.PI * i / laces, a1 = a0 + Math.PI / laces, a2 = a0 + 2 * Math.PI / laces;
                LaceRun(m, f, r, len, a0, a1, true);
                LaceRun(m, f, r, len, a1, a2, false);
            }
        }

        /// <summary>One lacing run of a drum from the hoop at angle a (y = 0, or y = len when not <paramref name="out0"/>) to
        /// the other hoop at angle b, over the belly (two straight tubes).</summary>
        private static void LaceRun(MeshData m, in Affine3 f, double r, double len, double a, double b, bool out0)
        {
            double y0 = out0 ? 0.05 * len : 0.95 * len, y1 = out0 ? 0.95 * len : 0.05 * len, am = 0.5 * (a + b), rr = 1.0 * r, rm = 1.055 * r;
            Shapes.Bar(m, f, DrumLeather, rr * Math.Cos(a), y0, rr * Math.Sin(a), rm * Math.Cos(am), 0.5 * len, rm * Math.Sin(am), 0.015, 3);
            Shapes.Bar(m, f, DrumLeather, rm * Math.Cos(am), 0.5 * len, rm * Math.Sin(am), rr * Math.Cos(b), y1, rr * Math.Sin(b), 0.015, 3);
        }

        private static int Kseg(int lod)
        {
            return lod == 0 ? 18 : lod == 1 ? 10 : 6;
        }
    }
}
