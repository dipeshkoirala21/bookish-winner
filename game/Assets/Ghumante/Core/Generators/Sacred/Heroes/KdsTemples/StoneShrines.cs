using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsTemples
{
    /// <summary>
    /// The small shrines of the square (kds-temples.md T5, T22, T30, T31): the all-white plastered shikhara at the foot of
    /// Maju Dega's stair (OSM w1028854863, white parts stepping to 5.9 m); Kotilingeshwar Mahadev (1566; DANAM KAT1530), a
    /// grey stone cruciform shrine on a three-stage stone plinth 10.73 m square with recessed corners, porches with dark red
    /// grilles on all four sides, a red cloth valance under the stone cornice and a large hemispherical stone dome with small
    /// domes on the porches; and the two small Shiva temples by Kasthamandap.
    /// </summary>
    internal static class StoneShrines
    {
        public const float WhiteShikharaTopM = 7.0f;
        public const float KotilingeshwarTopM = 9.6f;
        public const float ShivaNorthTopM = 7.6f;
        public const float ShivaWestTopM = 6.4f;

        public static double WhiteShikhara(KdsCtx x)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            ShapeBrush white = KdsLooks.Plaster;
            double h0 = x.PlanHu > 0.5 ? Math.Max(1.2, x.PlanHu) : 1.35;
            if (lod >= 3)
            {
                SacredDraw.Box(m, x.K, -h0, h0, 0, 2.0, -h0, h0, white, BoxFaces.All & ~BoxFaces.Bottom);
                Shapes.Cone(m, SacredDraw.At(x.K, 0, 2.0, 0), white, 0.85, WhiteShikharaTopM - 2.0, 6, 0, 0, false);
                return WhiteShikharaTopM;
            }
            // Two white steps, the cella with its small door, the stepped curvilinear tower, amalaka and gilt tip.
            KdsKit.Stage(x, h0, h0, 0, 0.3, h0 - 0.18, h0 - 0.18, 0, white, white, 0.06, 0.2, GenColliders.Stone);
            KdsKit.Stage(x, h0 - 0.18, h0 - 0.18, 0.3, 0.6, 0, 0, 0, white, white, 0.06, 0, GenColliders.Stone);
            double c = h0 - 0.42;
            SacredParts.BandedWall(m, x.K, c, c, 0.6, 2.0, white, KdsLooks.PlasterShade, lod, false, true);
            x.Solid(-c, c, -c, c, 0.6, 2.0, GenColliders.Stone);
            Affine3 front = KdsKit.Side(x.K, 0);
            SacredDraw.Panel(m, front, -0.32, 0.65, 0.32, 1.6, c + 0.01, Looks.Dark);
            if (lod <= 1)
            {
                // The little porch before the door: two dark columns under a white lintel slab (KAT1910 10).
                SacredDraw.Box(m, front, -0.52, 0.52, 1.66, 1.8, c, c + 0.3, KdsLooks.PlasterShade, BoxFaces.All & ~BoxFaces.Back);
                for (int sg = -1; sg <= 1; sg += 2)
                    Shapes.Cylinder(m, SacredDraw.At(front, sg * 0.4, 0.6, c + 0.2), KdsLooks.Timber, 0.05, 1.06, lod == 0 ? 6 : 4, 0, 0, false, false);
                // Tiered mouldings between the cella and the tower (the OSM parts step in at 2.0-3.0 m).
                for (int i = 0; i < 3; i++)
                {
                    double hh = c + 0.12 - 0.12 * i, v = 2.0 + 0.18 * i;
                    SacredDraw.RectRing(m, x.K, 0, 0, hh, hh, SacredDraw.M.Add(0, v, white).Add(0, v + 0.14), true, white);
                }
            }
            double top = ShikharaGenerator.Spire(m, x.K, c - 0.05, 2.5, 3.4, white, KdsLooks.PlasterShade, 3, lod);
            KdsKit.SpireBands(x, c - 0.05, 2.5, 3.4, 3, KdsLooks.PlasterShade);
            double cap = KdsKit.Amalaka(x, top, 5.9, white);
            // The white plastered pot (kalasha) on the amalaka and a small gilt tip (KAT1910 10, c_shivaparvati 14).
            double kh = WhiteShikharaTopM - cap - 0.22;
            var kp = new Profile2(10);
            kp.Add(0, 0, true).Add(0.17, 0, true).Add(0.23, 0.28 * kh).Add(0.19, 0.6 * kh).Add(0.08, 0.76 * kh).Add(0.13, 0.84 * kh).Add(0.06, kh).Add(0, kh, true);
            Shapes.Lathe(m, SacredDraw.At(x.K, 0, cap - 0.03, 0), white, kp, KdsKit.Seg(10, lod));
            Shapes.Cone(m, SacredDraw.At(x.K, 0, cap - 0.03 + kh, 0), KdsLooks.Gilt, 0.05, 0.25, 6, 0, 0, false);
            return cap - 0.03 + kh + 0.25;
        }

        public static double Kotilingeshwar(KdsCtx x)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            // Weathered buff-grey sandstone, warmer than the square's flags (KAT1530 02, 03, 06).
            ShapeBrush stone = Looks.Of(MeshColor.FromHex(0x9C9080), MaterialChannel.Stone), stoneDark = Looks.Of(MeshColor.FromHex(0x857A6C), MaterialChannel.Stone);
            if (lod >= 3)
            {
                SacredDraw.Box(m, x.K, -5.35, 5.35, 0, 1.8, -5.35, 5.35, stone, BoxFaces.All & ~BoxFaces.Bottom);
                SacredDraw.Box(m, x.K, -2.4, 2.4, 1.8, 5.6, -2.4, 2.4, stone, BoxFaces.All & ~BoxFaces.Bottom);
                Shapes.Dome(m, SacredDraw.At(x.K, 0, 5.6, 0), stone, 2.2, 2.6, 8);
                return 8.2;
            }
            // Three cruciform stages (squares with recessed corners), all of grey stone, steps on every face.
            double[] half = { 5.36, 4.55, 3.8 };
            for (int i = 0; i < 3; i++)
            {
                double v0 = 0.6 * i, v1 = 0.6 * (i + 1);
                Cruciform(x, half[i], 0.18 * half[i], v0, v1, i == 0 ? 0.2 : 0, stone, KdsLooks.StonePlinth);
                x.Floor(-half[i], half[i], -half[i], half[i], v0, v1, GenColliders.Stone);
            }
            for (int s = 0; s < 4; s++) KdsKit.SideStair(x, s, 0, 0.9, 3.8, 1.6, 0, 1.8, 0, 0, KdsLooks.StonePlinth, stone);
            if (x.Stats != null)
            {
                x.Stats.PlinthLevels = 3;
                x.Stats.PlinthTopM = 1.8f;
            }
            // The cella and its four porches.
            const double cell = 2.35, v = 1.8, cv = 5.4;
            SacredParts.BandedWall(m, x.K, cell, cell, v, cv, stone, stoneDark, lod, false, false);
            x.Solid(-cell, cell, -cell, cell, v, cv, GenColliders.Stone);
            for (int s = 0; s < 4; s++)
            {
                Affine3 sk = KdsKit.Side(x.K, s);
                // Porch: a projecting stone frame with pilasters and an arched opening with a dark red grille.
                SacredDraw.Box(m, sk, -1.25, 1.25, v, cv - 0.4, cell, cell + 0.7, stone, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                SacredDraw.Panel(m, sk, -0.62, v + 0.25, 0.62, v + 2.2, cell + 0.71, Looks.Recess);
                ArchHead(m, sk, 0, v + 2.2, cell + 0.71, 1.24, 0.5, lod);
                if (lod <= 1)
                {
                    ShapeBrush grille = Looks.Of(MeshColor.FromHex(0x7A1E1C), MaterialChannel.Paint);
                    for (int i = 1; i <= 4; i++)
                    {
                        double u = -0.62 + 1.24 * i / 5;
                        SacredDraw.Box(m, sk, u - 0.025, u + 0.025, v + 0.25, v + 2.2, cell + 0.71, cell + 0.75, grille, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
                    }
                    for (int i = 1; i <= 5; i++)
                    {
                        double vv = v + 0.25 + 1.95 * i / 6;
                        SacredDraw.Box(m, sk, -0.62, 0.62, vv - 0.025, vv + 0.025, cell + 0.71, cell + 0.75, grille, BoxFaces.Front | BoxFaces.Top | BoxFaces.Bottom);
                    }
                    // Carved niches with deities beside the porch.
                    for (int sgn = -1; sgn <= 1; sgn += 2)
                    {
                        SacredDraw.Panel(m, sk, sgn * 1.0 - 0.18, v + 0.5, sgn * 1.0 + 0.18, v + 1.3, cell + 0.71, Looks.Ao(stoneDark, 0.7f));
                        if (lod == 0) Shapes.Ellipsoid(m, SacredDraw.At(sk, sgn * 1.0, v + 0.9, cell + 0.72), stone, 0.1, 0.25, 0.04, 6, false);
                    }
                }
                // Porch cornice and the small dome over it.
                SacredDraw.Box(m, sk, -1.4, 1.4, cv - 0.4, cv - 0.15, cell - 0.1, cell + 0.85, stoneDark, BoxFaces.All & ~BoxFaces.Bottom);
                Shapes.Dome(m, SacredDraw.At(sk, 0, cv - 0.15, cell + 0.4), stone, 0.62, 0.62, lod == 0 ? 12 : 8);
                if (lod <= 1) Shapes.Cone(m, SacredDraw.At(sk, 0, cv + 0.45, cell + 0.4), stoneDark, 0.08, 0.3, 6, 0, 0, false);
            }
            // The stone cornice with the red cloth valance under it.
            KdsKit.Cornice(x, cell + 0.3, cell + 0.3, cv - 0.15, cv + 0.25, 0.15, stoneDark);
            if (lod <= 2)
            {
                var spec = new RoofSpec
                {
                    EaveU = cell + 0.42, EaveW = cell + 0.42, EaveV = cv + 0.05, TopU = cell, TopW = cell, TopV = cv + 0.25, FasciaH = 0.0, Clear = x.Clear,
                    GroundY = x.F.GroundY,
                };
                KdsKit.Valance(x, spec, 0.42);
            }
            // The great hemispherical dome on a short drum, a small finial.
            Shapes.Cylinder(m, SacredDraw.At(x.K, 0, cv + 0.25, 0), stoneDark, 2.15, 0.35, KdsKit.Seg(20, lod), 0.03, 1, false, false);
            Shapes.Dome(m, SacredDraw.At(x.K, 0, cv + 0.6, 0), stone, 2.15, 2.15, KdsKit.Seg(24, lod));
            if (lod == 0)
            {
                // Faint ribs on the dome.
                for (int i = 0; i < 8; i++)
                {
                    double a = Math.PI * i / 4;
                    Affine3 rib = SacredDraw.At(x.K, 0, cv + 0.6, 0) * Affine3.RotationY(a);
                    Shapes.Torus(m, rib * Affine3.RotationZ(Math.PI / 2), stoneDark, 2.16, 0.03, 12, 3, default, 0, 180);
                }
            }
            var fin = new Profile2(8);
            double f0 = cv + 0.6 + 2.12;
            fin.Add(0, 0, true).Add(0.28, 0, true).Add(0.2, 0.2).Add(0.26, 0.35).Add(0.1, 0.55).Add(0.14, 0.65).Add(0.03, KotilingeshwarTopM - f0).Add(0, KotilingeshwarTopM - f0);
            Shapes.Lathe(m, SacredDraw.At(x.K, 0, f0, 0), stoneDark, fin, KdsKit.Seg(10, lod));
            x.Cylinder(0, 0, cv, cv + 2.7, 2.15);
            // The low wooden railing round the plinth (clear of the corridors).
            if (lod <= 1)
            {
                double r = 5.9;
                for (int s = 0; s < 4; s++)
                {
                    double au, aw, bu, bw, cu, cw, du, dw;
                    SacredParts.OnRect(s, -1, r, r, out au, out aw);
                    SacredParts.OnRect(s, -0.22, r, r, out bu, out bw);
                    SacredParts.OnRect(s, 0.22, r, r, out cu, out cw);
                    SacredParts.OnRect(s, 1, r, r, out du, out dw);
                    Rail(x, au, aw, bu, bw);
                    Rail(x, cu, cw, du, dw);
                }
            }
            return KotilingeshwarTopM;
        }

        /// <summary>A rail fence of thin wooden posts and two rails.</summary>
        private static void Rail(KdsCtx x, double u0, double w0, double u1, double w1)
        {
            double du = u1 - u0, dw = w1 - w0, len = Math.Sqrt(du * du + dw * dw);
            if (len < 0.5) return;
            if (x.CorridorDistance(u0, w0) < SacredClearance.MarginM || x.CorridorDistance(u1, w1) < SacredClearance.MarginM) return;
            Affine3 seg = SacredDraw.Basis(x.K, u0, 0, w0, du / len, 0, dw / len, 0, 1, 0);
            ShapeBrush wood = KdsLooks.Fence;
            int posts = Math.Max(1, (int)(len / 1.5));
            for (int i = 0; i <= posts; i++)
            {
                double p = len * i / posts;
                SacredDraw.Box(x.M, seg, p - 0.05, p + 0.05, 0, 0.9, -0.05, 0.05, wood, BoxFaces.All & ~BoxFaces.Bottom);
            }
            SacredDraw.Box(x.M, seg, 0, len, 0.75, 0.83, -0.03, 0.03, wood, BoxFaces.All);
            SacredDraw.Box(x.M, seg, 0, len, 0.35, 0.42, -0.03, 0.03, wood, BoxFaces.All);
            x.Wall(u0, w0, u1, w1, 0, 0.9, 0.05);
        }

        /// <summary>A cruciform stage: a square of half size h with recessed corners (inset by r) from v0 to v1, capped.</summary>
        private static void Cruciform(KdsCtx x, double h, double r, double v0, double v1, double sink, in ShapeBrush face, in ShapeBrush top)
        {
            double[] pu = ShapeScratch.U2, pw = ShapeScratch.W2;
            double a = h - r;
            // A 12-corner cross: walk the outline counter-clockwise.
            int n = 0;
            pu[n] = h; pw[n++] = -a;
            pu[n] = h; pw[n++] = a;
            pu[n] = a; pw[n++] = a;
            pu[n] = a; pw[n++] = h;
            pu[n] = -a; pw[n++] = h;
            pu[n] = -a; pw[n++] = a;
            pu[n] = -h; pw[n++] = a;
            pu[n] = -h; pw[n++] = -a;
            pu[n] = -a; pw[n++] = -a;
            pu[n] = -a; pw[n++] = -h;
            pu[n] = a; pw[n++] = -h;
            pu[n] = a; pw[n++] = -a;
            Mould p = SacredDraw.M;
            double nh = Math.Min(0.1, 0.3 * (v1 - v0));
            p.Add(0, v0 - sink, face).Add(0, v1 - nh).Add(0.03, v1 - nh, top).Add(0.03, v1);
            SacredDraw.Ring(x.M, x.K, pu, pw, n, p);
            // Cap: the centre square and four arms (the cross is not star-shaped from its centroid at the re-entrant corners).
            SacredDraw.CapRect(x.M, x.K, 0, 0, a, a, v1, true, top);
            SacredDraw.CapRect(x.M, x.K, 0, 0.5 * (a + h), a, 0.5 * (h - a) + 0.03, v1, true, top);
            SacredDraw.CapRect(x.M, x.K, 0, -0.5 * (a + h), a, 0.5 * (h - a) + 0.03, v1, true, top);
            SacredDraw.CapRect(x.M, x.K, 0.5 * (a + h), 0, 0.5 * (h - a) + 0.03, a, v1, true, top);
            SacredDraw.CapRect(x.M, x.K, -0.5 * (a + h), 0, 0.5 * (h - a) + 0.03, a, v1, true, top);
        }

        /// <summary>A round-arched head (dark) over an opening on the plane d.</summary>
        private static void ArchHead(MeshData m, in Affine3 sk, double u, double v, double d, double w, double rise, int lod)
        {
            int seg = lod == 0 ? 10 : 5;
            double[] px = ShapeScratch.U, pz = ShapeScratch.W;
            int n = 0;
            for (int i = 0; i <= seg; i++)
            {
                double a = Math.PI * i / seg;
                px[n] = -0.5 * w * Math.Cos(a);
                pz[n++] = -rise * Math.Sin(a);
            }
            Shapes.BevelExtrude(m, SacredDraw.Basis(sk, u, v, d, 1, 0, 0, 0, 0, 1), Looks.Recess, px, pz, n, 0.01, 0, 0, BevelStyle.Chamfer, true, false);
        }

        /// <summary>The small Shiva temple north of Kasthamandap (OSM w196809462): a three-stage brick plinth and a small
        /// two-roof temple with a gilt finial.</summary>
        public static double ShivaNorth(KdsCtx x)
        {
            return SmallPagoda(x, new[] { 8.0, 6.6, 5.2 }, 1.5, 1.7, 2.75, 3.95, 1.2, 1.95, 5.9, 7.05, ShivaNorthTopM, 0x53484Eu);
        }

        /// <summary>The small Shiva temple west of Kasthamandap (OSM w196809346).</summary>
        public static double ShivaWest(KdsCtx x)
        {
            return SmallPagoda(x, new[] { 5.6, 4.6 }, 0.9, 1.45, 2.4, 3.2, 1.0, 1.7, 4.9, 5.85, ShivaWestTopM, 0x534857u);
        }

        private static double SmallPagoda(KdsCtx x, double[] st, double plinth, double wall, double eave1, double ev1, double wall2, double eave2, double ev2, double apex,
                                          float top, uint seed)
        {
            int lod = x.Lod;
            if (x.PlanHu > 1)
            {
                double f = Math.Min(1.1, Math.Max(0.85, 2 * Math.Min(x.PlanHu, x.PlanHw) / st[0]));
                for (int i = 0; i < st.Length; i++) st[i] *= f;
            }
            if (lod >= 3)
            {
                KdsKit.Lod3(x, 0.5 * st[0], 0.5 * st[0], plinth, wall, wall, new[] { eave1, eave2 }, new[] { ev1, ev2 }, new[] { ev1 + 1.4 }, apex, KdsLooks.BrickPlinth,
                            KdsLooks.Brick, KdsLooks.TileOld);
                return apex;
            }
            KdsPagoda.StageStack(x, st, st, plinth, 0.5, KdsLooks.BrickPlinth, KdsLooks.Nosing, 0.08);
            KdsKit.Stair(x, 0.5, 0.5 * st[st.Length - 1], Math.Max(0.9, 0.5 * (st[0] - st[st.Length - 1]) + 0.4), 0, plinth, 0, 0, KdsLooks.StonePlinth,
                         KdsLooks.StonePlinth, (int)Math.Ceiling(plinth / 0.22));
            double v1 = plinth + 2.0;
            SacredParts.BandedWall(x.M, x.K, wall, wall, plinth, v1, KdsLooks.Brick, KdsLooks.Timber, lod, true, true);
            x.Solid(-wall, wall, -wall, wall, plinth, v1, GenColliders.Brick);
            KdsKit.Door(x.M, KdsKit.Side(x.K, 0), 0, plinth + 0.1, wall, 0.7, 1.4, KdsLooks.Timber, lod, true, v1 - 0.05);
            var tiers = new[]
            {
                new KdsTier
                {
                    WallHu = wall - 0.05, WallHw = wall - 0.05, WallV0 = v1, EaveHu = eave1, EaveHw = eave1, EaveV = ev1, TopHu = wall2, TopHw = wall2, TopV = ev1 + 1.4,
                    StrutsU = 2, StrutsW = 2, StrutFootV = v1 - 0.1, ValanceH = 0.3, Wall = KdsLooks.Brick, Tile = KdsLooks.TileOld, Bells = true,
                },
                new KdsTier
                {
                    WallHu = wall2, WallHw = wall2, WallV0 = ev1 + 1.4, EaveHu = eave2, EaveHw = eave2, EaveV = ev2, TopHu = 0.15, TopHw = 0.15, TopV = apex, StrutsU = 1,
                    StrutsW = 1, ValanceH = 0.24, Wall = KdsLooks.Brick, Tile = KdsLooks.TileOld,
                },
            };
            double ap = KdsPagoda.Tiers(x, tiers, seed);
            if (x.Stats != null) x.Stats.Tiers = 2;
            return KdsKit.GiltFinial(x, ap - 0.1, top - ap + 0.1, 0.16);
        }
    }
}
