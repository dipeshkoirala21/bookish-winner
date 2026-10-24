using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsTemples
{
    /// <summary>
    /// Chyasin Dega, the octagonal Krishna temple (1649, Pratap Malla; rebuilt after 2015; kds-temples.md T14; DANAM KAT1810
    /// KVPT section 17.31 m, 60.5 px/m): octagonal throughout. A stepped octagonal brick plinth (14.4 m across the flats,
    /// six steps with stone nosings) to 3.5 m; a colonnade of twenty-four slender carved wooden columns (three a side) with
    /// carved capitals and a lintel round an octagonal brick sanctum, 3.5-6.2 m; three octagonal roofs (12.7 / 9.2 / 5.7 m at
    /// 7.2 / 11.4 / 14.6 m) with crimson valances and struts; the gilt finial to 17.3 m. The stair runs up the
    /// east-south-east face between two stone pillars, one with a small deity on a lotus capital, the other with a lotus cup.
    /// </summary>
    internal static class ChyasinDega
    {
        public const float TopM = 17.3f;
        private const int N = 8;

        public static double Build(KdsCtx x)
        {
            int lod = x.Lod;
            MeshData m = x.M;
            // The first side faces the door: the octagon is turned by half a side so a flat face is square to +w.
            const double plinth = 3.5;
            if (lod >= 3)
            {
                PolyBlock(x, 7.2, 0, plinth, KdsLooks.BrickPlinth);
                PolyBlock(x, 2.8, plinth, 7.2, KdsLooks.Brick);
                KdsKit.PolyRoof(x, N, 6.35, 7.2, 0.2, 15.8, 2.8, 7.3, KdsLooks.TileNew, 0, 0, 0.012);
                return 15.8;
            }
            // Six plinth steps from 7.2 m apothem to the 5.0 m top.
            const int steps = 6;
            for (int i = 0; i < steps; i++)
            {
                double a = 7.2 - (7.2 - 5.0) * i / (steps - 1.0), v0 = plinth * i / steps, v1 = plinth * (i + 1) / steps;
                PolyStage(x, a, v0, v1, i == 0 ? 0.25 : 0, KdsLooks.BrickPlinth, KdsLooks.Nosing);
            }
            if (x.Stats != null)
            {
                x.Stats.PlinthLevels = steps;
                x.Stats.PlinthTopM = (float)plinth;
            }
            // The stair up the door face, kept clear of the lane's corridor (steeper rather than into the road).
            double topA = 5.0, run = 4.6;
            while (run > 0.95 * plinth && x.CorridorDistance(0, topA + run) < SacredClearance.MarginM) run -= 0.2;
            KdsKit.Stair(x, 0.75, topA, run, 0, plinth, 0.18, 0.12, KdsLooks.StonePlinth, KdsLooks.BrickPlinth, 16);
            if (lod <= 1) IronRails(x, 0.75, topA, run, plinth);
            // The two stone pillars flanking the stair foot (judge round 1: both stand, mirrored across the stair axis); they
            // keep their surveyed place over a mapped path's corridor (its trim is an open issue for roads) and only step
            // back off a drawn road surface.
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                double pu = sgn * 1.45, pw = topA + run - 0.1;
                while (pw > topA + 0.5 * run && x.DrawnDistance(pu, pw) < 0.45) pw -= 0.2;
                Pillar(x, pu, pw, sgn < 0);
            }

            // Colonnade and sanctum.
            const double colA = 4.35, sanA = 2.55, cv0 = plinth, cv1 = 6.2;
            PolyBlock(x, 4.75, plinth, plinth + 0.15, KdsLooks.Nosing);
            Columns(x, colA, cv0 + 0.15, cv1);
            PolyWall(x, sanA, cv0 + 0.15, cv1 + 0.4, KdsLooks.Brick);
            x.Cylinder(0, 0, cv0, cv1, sanA);
            // The carved lattice door on the front face; small black windows on the others.
            double sideHalf = sanA * Math.Tan(Math.PI / N);
            Affine3 front = KdsKit.Side(x.K, 0);
            KdsKit.LatticeDoor(m, front, 0, cv0 + 0.2, sanA, Math.Min(0.95, 1.6 * sideHalf), 1.9, lod);
            if (lod <= 1)
            {
                for (int s = 1; s < N; s++)
                {
                    Affine3 sk = SacredDraw.Turn(x.K, 0, 0, 0, 2 * Math.PI * s / N);
                    KdsKit.BlackWindow(m, sk, 0, cv0 + 1.4, sanA, 0.55, 0.75, lod, 0.06, 2);
                }
            }
            // The carved timber cornice over the columns (KAT1810 05: a deep moulded ring projecting over the arcade) and the
            // ambulatory ceiling.
            Mould cm = SacredDraw.M;
            if (lod >= 2) cm.Add(0, cv1, KdsLooks.Timber).Add(0.2, cv1 + 0.5);
            else
                cm.Add(0, cv1, Looks.Ao(KdsLooks.Timber, 0.75f)).Add(0.05, cv1 + 0.06).Add(0.05, cv1 + 0.2, KdsLooks.Timber).Add(0.13, cv1 + 0.25)
                  .Add(0.13, cv1 + 0.38, KdsLooks.TimberNew).Add(0.24, cv1 + 0.44).Add(0.24, cv1 + 0.52, KdsLooks.Timber).Add(0, cv1 + 0.56);
            {
                double[] cpu = ShapeScratch.U2, cpw = ShapeScratch.W2;
                int cn = Octagon(colA + 0.18, cpu, cpw);
                SacredDraw.Ring(m, x.K, cpu, cpw, cn, cm);
                SacredDraw.Cap(m, x.K, cpu, cpw, cn, 0, cv1, false, Looks.Ao(KdsLooks.Beam, 0.5f));
            }
            if (x.Stats != null)
            {
                x.Stats.SanctumHalfU = (float)sanA;
                x.Stats.SanctumHalfW = (float)sanA;
            }

            // Storeys and the three octagonal roofs: straight-edged with a small flick at each corner, struts springing from
            // the cornice (three a face on the first tier, two above), round-arched dark niches on the brick storeys.
            const double flick = 0.012;
            double r1w = 4.15, r2w = 2.65, r3w = 1.7;
            PolyWall(x, r1w, cv1 + 0.45, 9.2, KdsLooks.Brick);
            if (lod <= 1) PolyNiches(x, r1w, cv1 + 0.62, 0.42, 0.95);
            PolyStruts(x, r1w, colA + 0.3, cv1 + 0.56, 6.35, 7.2, 8.55, lod == 0 ? 3 : 1);
            KdsKit.PolyRoof(x, N, 6.35, 7.2, r2w + 0.05, 9.7, r1w, 8.55, KdsLooks.TileNew, 0.34, 0, flick);
            PolyWall(x, r2w, 9.6, 12.8, KdsLooks.Brick);
            if (lod <= 1) PolyNiches(x, r2w, 9.95, 0.32, 0.75);
            if (lod == 0) PolyStruts(x, r2w, r2w + 0.1, 9.75, 4.6, 11.4, 12.3, 2);
            KdsKit.PolyRoof(x, N, 4.6, 11.4, r3w + 0.05, 13.2, r2w, 12.3, KdsLooks.TileNew, 0.34, 0, flick);
            PolyWall(x, r3w, 13.1, 15.2, KdsLooks.Brick);
            if (lod <= 1) PolyWindows(x, r3w, 13.15, 14.5, 0.32, 0.3);
            if (lod == 0) PolyStruts(x, r3w, r3w + 0.1, 13.2, 2.85, 14.6, 15.1, 1);
            KdsKit.PolyRoof(x, N, 2.85, 14.6, 0.22, 15.8, r3w, 15.1, KdsLooks.TileNew, 0.3, 0, flick);
            double tip = KdsKit.GiltFinial(x, 15.7, TopM - 15.7, 0.3);
            if (x.Stats != null) x.Stats.Tiers = 3;
            return tip;
        }

        /// <summary>The octagon (apothem a, first face square to +w) into scratch arrays; returns N.</summary>
        private static int Octagon(double a, double[] pu, double[] pw)
        {
            double r = a / Math.Cos(Math.PI / N);
            for (int i = 0; i < N; i++)
            {
                // Corners at ±22.5° about +w, counter-clockwise seen with u right and w up.
                double ang = 2 * Math.PI * i / N - Math.PI / N;
                pu[i] = -r * Math.Sin(ang);
                pw[i] = r * Math.Cos(ang);
            }
            return N;
        }

        private static void PolyStage(KdsCtx x, double a, double v0, double v1, double sink, in ShapeBrush face, in ShapeBrush nose)
        {
            double[] pu = ShapeScratch.U2, pw = ShapeScratch.W2;
            int n = Octagon(a, pu, pw);
            Mould p = SacredDraw.M;
            double h = v1 - v0, nh = Math.Min(0.1, 0.3 * h);
            if (x.Lod >= 2) p.Add(0, v0 - sink, face).Add(0, v1);
            else p.Add(0, v0 - sink, face).Add(0, v1 - nh).Add(0.03, v1 - nh, nose).Add(0.03, v1);
            SacredDraw.Ring(x.M, x.K, pu, pw, n, p);
            SacredDraw.Cap(x.M, x.K, pu, pw, n, 0.03, v1, true, nose.Color == KdsLooks.Nosing.Color ? KdsLooks.Tread : nose);
            // The octagon as walkable boxes: the two cross bands through the flat faces and the square whose corners touch
            // the diagonal faces (a square box would reach 0.41 a past them).
            double s = a * Math.Tan(Math.PI / 8), c = 0.5 * (a + s);
            x.Floor(-a, a, -s, s, v0 - sink, v1, GenColliders.Brick);
            x.Floor(-s, s, -a, a, v0 - sink, v1, GenColliders.Brick);
            x.Floor(-c, c, -c, c, v0 - sink, v1, GenColliders.Brick);
        }

        /// <summary>An octagonal block (apothem a) from v0 to v1, capped.</summary>
        private static void PolyBlock(KdsCtx x, double a, double v0, double v1, in ShapeBrush b)
        {
            double[] pu = ShapeScratch.U2, pw = ShapeScratch.W2;
            int n = Octagon(a, pu, pw);
            Mould p = SacredDraw.M;
            p.Add(0, v0, b).Add(0, v1);
            SacredDraw.Ring(x.M, x.K, pu, pw, n, p);
            SacredDraw.Cap(x.M, x.K, pu, pw, n, 0, v1, true, b);
            SacredDraw.Cap(x.M, x.K, pu, pw, n, 0, v0, false, Looks.Ao(b, 0.5f));
        }

        /// <summary>An octagonal wall (apothem a) from v0 to v1 with a timber sill.</summary>
        private static void PolyWall(KdsCtx x, double a, double v0, double v1, in ShapeBrush b)
        {
            double[] pu = ShapeScratch.U2, pw = ShapeScratch.W2;
            int n = Octagon(a, pu, pw);
            Mould p = SacredDraw.M;
            if (x.Lod >= 2) p.Add(0, v0, b).Add(0, v1);
            else p.Add(0.05, v0, KdsLooks.Timber).Add(0.05, v0 + 0.15).Add(0, v0 + 0.15, b).Add(0, v1);
            SacredDraw.Ring(x.M, x.K, pu, pw, n, p);
        }

        private static void PolyWindows(KdsCtx x, double a, double v0, double v1, double ww, double wh)
        {
            double vc = 0.5 * (v0 + v1);
            for (int s = 0; s < N; s++)
            {
                Affine3 sk = SacredDraw.Turn(x.K, 0, 0, 0, 2 * Math.PI * s / N);
                KdsKit.BlackWindow(x.M, sk, 0, vc, a, ww, Math.Min(wh, 0.8 * (v1 - v0)), x.Lod, 0.06, 2);
            }
        }

        /// <summary>A dark round-arched niche with a carved timber arch frame on each face of an octagonal storey (apothem a),
        /// its foot at v0 (KAT1810 05, 13: one blind arched window a face above the cornice and on the second storey).</summary>
        private static void PolyNiches(KdsCtx x, double a, double v0, double halfW, double h)
        {
            int lod = Math.Max(1, x.Lod);
            for (int s = 0; s < N; s++)
            {
                Affine3 sk = SacredDraw.Turn(x.K, 0, 0, 0, 2 * Math.PI * s / N);
                // The carved frame behind (a slightly larger timber arch panel), the dark niche in it and the sill.
                KdsKit.ArchPanel(x.M, sk, 0, v0, a + 0.005, halfW + 0.1, h + 0.1, 0.02, KdsLooks.Timber, 2, false);
                KdsKit.ArchPanel(x.M, sk, 0, v0, a + 0.03, halfW, h, 0.01, Looks.Recess, lod, false);
                SacredDraw.Box(x.M, sk, -halfW - 0.14, halfW + 0.14, v0 - 0.08, v0, a, a + 0.09, KdsLooks.Timber, BoxFaces.Front | BoxFaces.Top);
            }
        }

        /// <summary>The 24 slender columns: three a side on the octagon of apothem a (the corners and two between).</summary>
        private static void Columns(KdsCtx x, double a, double v0, double v1)
        {
            int lod = x.Lod;
            MeshData m = x.M;
            double r = a / Math.Cos(Math.PI / N);
            for (int s = 0; s < N; s++)
            {
                double ang0 = 2 * Math.PI * s / N - Math.PI / N, ang1 = ang0 + 2 * Math.PI / N;
                double au = -r * Math.Sin(ang0), aw = r * Math.Cos(ang0), bu = -r * Math.Sin(ang1), bw = r * Math.Cos(ang1);
                for (int q = 0; q < 3; q++)
                {
                    if (lod >= 2 && q > 0) continue;
                    double t = q / 3.0, u = au + (bu - au) * t, w = aw + (bw - aw) * t;
                    Column(x, u, w, v0, v1);
                }
                if (lod == 0)
                {
                    // The carved cusped arch heads between the columns under the lintel (KAT1810 05, 11).
                    Affine3 sk = SacredDraw.Turn(x.K, 0, 0, 0, 2 * Math.PI * s / N);
                    double sideLen = 2 * a * Math.Tan(Math.PI / N), bay = sideLen / 3, hw = 0.5 * bay - 0.13;
                    for (int q = 0; q < 3; q++)
                    {
                        double c = -0.5 * sideLen + bay * (q + 0.5);
                        KdsKit.FlameArch(m, sk, c, v1 - hw - 0.55, a - 0.05, hw, hw + 0.42, 0.11, 0.1, KdsLooks.Timber, 2, false, true);
                        SacredDraw.Box(m, sk, c - hw - 0.12, c + hw + 0.12, v1 - 0.2, v1, a - 0.08, a + 0.06, Looks.Ao(KdsLooks.Timber, 0.9f), BoxFaces.Front | BoxFaces.Bottom);
                    }
                }
            }
        }

        /// <summary>A slender round carved column: square base, turned shaft with rings, a flared bracket capital.</summary>
        private static void Column(KdsCtx x, double u, double w, double v0, double v1)
        {
            int lod = x.Lod;
            MeshData m = x.M;
            if (lod >= 1)
            {
                SacredDraw.Box(m, x.K, u - 0.1, u + 0.1, v0, v1, w - 0.1, w + 0.1, KdsLooks.Timber, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
                if (lod == 1) x.Cylinder(u, w, v0, v1, 0.14, true);
                return;
            }
            double h = v1 - v0;
            // A square foot, the turned shaft with its ring and the flared bracket capital.
            var p = new Profile2(16);
            p.Add(0.14, 0, true).Add(0.14, 0.25, true).Add(0.09, 0.27).Add(0.085, 0.7 * h).Add(0.12, 0.74 * h).Add(0.1, 0.86 * h).Add(0.2, 0.96 * h, true)
             .Add(0.2, h, true);
            Shapes.Lathe(m, SacredDraw.At(x.K, u, v0, w), KdsLooks.Timber, p, 4);
            x.Cylinder(u, w, v0, v1, 0.14, true);
        }

        /// <summary>Struts round an octagonal tier: <paramref name="perSide"/> per face leaning from their foot (apothem
        /// <paramref name="footA"/> at footV: the wall, or the cornice ring under the first roof) up to the soffit of the roof
        /// whose eave is at apothem eaveA, height eaveV, and whose soffit meets the wall (apothem a) at wallTopV.</summary>
        private static void PolyStruts(KdsCtx x, double a, double footA, double footV, double eaveA, double eaveV, double wallTopV, int perSide)
        {
            if (x.Lod >= 2) return;
            // The head sits on the soffit (the line from the eave's underside back to the wall top) at 80 % of the overhang.
            double fascia = Math.Min(0.2, 0.08 + 0.012 * eaveA), depth = eaveA - a, headA = a + 0.8 * depth;
            double hv = (eaveV - fascia) + (wallTopV - (eaveV - fascia)) * (eaveA - headA) / Math.Max(0.1, depth) - 0.06;
            double dv = hv - footV, dw = headA - footA, len = Math.Sqrt(dv * dv + dw * dw);
            if (len < 0.4) return;
            double cy = dv / len, sy = dw / len, size = Math.Max(0.7, Math.Min(1.3, depth / 2.0));
            // Carved struts on the first tier; the small upper tiers' struts are plain (they read only as a rhythm).
            MeshData proto = SacredParts.StrutProto(len, size, perSide >= 3, false, KdsLooks.Strut, Looks.Of(MeshColor.FromHex(0x7A4A2E), MaterialChannel.WoodCarved),
                                                    x.Lod);
            double sideLen = 2 * a * Math.Tan(Math.PI / N);
            for (int s = 0; s < N; s++)
            {
                double phi = 2 * Math.PI * s / N;
                for (int q = 0; q < perSide; q++)
                {
                    double along = -0.5 * sideLen + sideLen * (q + 0.5) / perSide;
                    // Side frame: +w out of face s; place the strut foot on the wall.
                    Affine3 sk = SacredDraw.Turn(x.K, 0, 0, 0, phi);
                    Affine3 side = SacredDraw.Turn(sk, along * footA / a, footV, footA, 0);
                    Affine3 xf = Affine3.FromBasis(1, 0, 0, 0, cy, sy, 0, -sy, cy, 0, 0, 0).Then(side);
                    Shapes.CopyTransformed(x.M, proto, xf);
                    if (x.Stats != null) x.Stats.Struts++;
                }
            }
        }

        /// <summary>Thin iron handrails along both sides of the stair.</summary>
        private static void IronRails(KdsCtx x, double halfW, double wTop, double run, double rise)
        {
            ShapeBrush iron = Looks.Of(MeshColor.FromHex(0x2E2C2A), MaterialChannel.Metal);
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                double u = sgn * (halfW + 0.05);
                Shapes.Bar(x.M, x.K, iron, u, rise + 0.95, wTop + 0.2, u, 0.95, wTop + run, 0.025, 5);
                for (int i = 0; i <= 3; i++)
                {
                    double t = i / 3.0, w = wTop + 0.2 + (run - 0.2) * t;
                    double base0 = rise * (1 - (w - wTop) / run);
                    Shapes.Bar(x.M, x.K, iron, u, base0, w, u, base0 + 0.95, w, 0.02, 4);
                }
            }
        }

        /// <summary>The pillars' grey stone and the kneeling figure's (judge round 1: #6E6A63).</summary>
        private static readonly ShapeBrush PillarStone = Looks.Of(MeshColor.FromHex(0x6E6A63), MaterialChannel.Stone);

        /// <summary>A stone pillar at the stair foot (about 5 m; drawn one LOD coarser than the temple): stepped base,
        /// square-to-octagonal shaft, a lotus capital (two rings and a bell, 0.32 m), and on pillar A a small kneeling
        /// figure (0.6 m) facing the temple, on pillar B a lotus cup. Its collider is left out over a road corridor.</summary>
        private static void Pillar(KdsCtx x, double u, double w, bool deity)
        {
            MeshData m = x.M;
            int lod = Math.Min(2, x.Lod + 1);
            ShapeBrush stone = PillarStone;
            SacredDraw.Box(m, x.K, u - 0.35, u + 0.35, 0, 0.45, w - 0.35, w + 0.35, stone, BoxFaces.All & ~BoxFaces.Bottom);
            SacredDraw.Box(m, x.K, u - 0.2, u + 0.2, 0.45, 2.0, w - 0.2, w + 0.2, stone, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
            var shaft = new Profile2(6);
            shaft.Add(0, 0, true).Add(0.2, 0, true).Add(0.17, 2.4, true).Add(0, 2.4, true);
            Shapes.Lathe(m, SacredDraw.At(x.K, u, 2.0, w) * Affine3.RotationY(Math.PI / 8), stone, shaft, 8);
            // The lotus capital: two rings and the bell turned up to the abacus.
            int seg = KdsKit.Seg(12, lod);
            if (lod <= 1)
            {
                Shapes.Torus(m, SacredDraw.At(x.K, u, 4.42, w), stone, 0.2, 0.045, seg, 4);
                Shapes.Torus(m, SacredDraw.At(x.K, u, 4.52, w), stone, 0.22, 0.045, seg, 4);
            }
            var cap = new Profile2(8);
            cap.Add(0, 0, true).Add(0.17, 0, true).Add(0.22, 0.06).Add(0.3, 0.2).Add(0.32, 0.3, true).Add(0.32, 0.36, true).Add(0, 0.36, true);
            Shapes.Lathe(m, SacredDraw.At(x.K, u, 4.55, w), stone, cap, seg);
            if (deity && lod <= 1)
            {
                // The kneeling devotee facing the temple (−w): the body as a capsule, the head over it, joined hands.
                Affine3 f = SacredDraw.Turn(x.K, u, 4.91, w, Math.PI);
                Shapes.Ellipsoid(m, SacredDraw.At(f, 0, 0.09, 0.04), stone, 0.2, 0.09, 0.16, 2);
                Shapes.Capsule(m, SacredDraw.At(f, 0, 0.12, 0.02), stone, 0.11, 0.34, 6);
                Shapes.Ellipsoid(m, SacredDraw.At(f, 0, 0.52, 0.04), stone, 0.075, 0.09, 0.08, 2);
                Shapes.Ellipsoid(m, SacredDraw.At(f, 0, 0.36, 0.13), stone, 0.04, 0.07, 0.04, 2);
            }
            else if (!deity && lod <= 1)
            {
                Shapes.Frustum(m, SacredDraw.At(x.K, u, 4.91, w), stone, 0.16, 0.3, 0.2, 10, 0.02, 1, true, true);
            }
            if (x.CorridorDistance(u, w) > 0.4) x.Cylinder(u, w, 0, 5.2, 0.35);
        }
    }
}
