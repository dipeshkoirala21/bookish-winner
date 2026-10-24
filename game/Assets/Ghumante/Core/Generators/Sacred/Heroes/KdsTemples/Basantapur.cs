using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsTemples
{
    /// <summary>
    /// The open ground of the square (kds-temples.md T6, T10, T28): the ruined Narayan temple (destroyed in 2015 and not
    /// rebuilt: a broken four-stage brick plinth heaped with rubble and weeds behind a wire-mesh fence, a black water tank at
    /// its north-west corner); the Basantapur Dabali (the low red-sided, stone-capped dance platforms east of Trailokya
    /// Mohan, on their OSM outline); and the Basantapur square: the raised red-brown herringbone brick platform with a grey
    /// stone kerb on the marketplace outline (OSM w218440319), paved wall to wall on the levelled ground, with the curio
    /// market (long runs of low tables draped to the ground in royal blue and crimson, laden with bronzes, singing bowls,
    /// masks and khukuris, a rare umbrella; judge round 1) and tall lamp posts.
    /// </summary>
    internal static class Basantapur
    {
        public const float RuinTopM = 2.9f;
        public const float DabaliTopM = 0.6f;
        /// <summary>The square's top: its lamp posts (5.6 m) on the rising east end of the paving.</summary>
        public const float SquareTopM = 6.6f;

        // The marketplace outline (w218440319) in the brief's local frame (kds-temples.md T28).
        internal static readonly double[] MarketX = { -82.9, -4.4, -3.7, -86.2 }, MarketY = { -76.8, -92.9, -115.8, -95.9 };

        // ---------------------------------------------------------------------------------------------------------------
        // T6 Narayan ruin
        // ---------------------------------------------------------------------------------------------------------------

        public static double NarayanRuin(KdsCtx x)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            double[] st = { 8.9, 7.5, 6.3, 5.1 };
            const double top = 2.5;
            if (lod >= 3)
            {
                SacredDraw.Box(m, x.K, -0.5 * st[0], 0.5 * st[0], 0, 1.2, -0.5 * st[0], 0.5 * st[0], KdsLooks.Rubble, BoxFaces.All & ~BoxFaces.Bottom);
                SacredDraw.Box(m, x.K, -0.5 * st[2], 0.5 * st[2], 1.2, top, -0.5 * st[2], 0.5 * st[2], KdsLooks.Rubble, BoxFaces.All & ~BoxFaces.Bottom);
                return top;
            }
            // The broken stages (no stair: the temple is gone), weathered.
            int first = m.VertexCount;
            KdsPagoda.StageStack(x, st, st, top, 0, KdsLooks.BrickPlinth, KdsLooks.Rubble, 0.08);
            // Weathered and grimy since 2015: darker, duller brick with a green-grey cast (KAT1890 02, 07).
            for (int v = first; v < m.VertexCount; v++)
            {
                int c = 4 * v;
                m.Colors[c] = (byte)(m.Colors[c] * 0.72f + 10);
                m.Colors[c + 1] = (byte)(m.Colors[c + 1] * 0.74f + 14);
                m.Colors[c + 2] = (byte)(m.Colors[c + 2] * 0.72f + 10);
            }
            if (lod <= 1) ShapeColor.JitterByPosition(m, first, m.VertexCount - first, 0.12f, 0x4E415255u, 0.6);
            // Rubble heaps, loose bricks, timber fragments and weeds on the top and the treads.
            uint seed = 0x52554Eu;
            int heaps = lod == 0 ? 7 : lod == 1 ? 4 : 2;
            double h3 = 0.5 * st[3];
            for (int i = 0; i < heaps; i++)
            {
                double u = (Rnd(ref seed) - 0.5) * 1.5 * h3, w = (Rnd(ref seed) - 0.5) * 1.5 * h3;
                double rx = 0.5 + 0.7 * Rnd(ref seed), rz = 0.4 + 0.6 * Rnd(ref seed);
                Shapes.Ellipsoid(m, Affine3.RotationY(Rnd(ref seed) * Math.PI).Then(SacredDraw.At(x.K, u, top - 0.05, w)), KdsLooks.Rubble, rx, 0.25 + 0.25 * Rnd(ref seed), rz,
                                 lod == 0 ? 7 : 5, false);
            }
            if (lod == 0)
            {
                for (int i = 0; i < 40; i++)
                {
                    double u = (Rnd(ref seed) - 0.5) * 1.9 * h3, w = (Rnd(ref seed) - 0.5) * 1.9 * h3;
                    Affine3 b = Affine3.RotationY(Rnd(ref seed) * Math.PI).Then(SacredDraw.At(x.K, u, top + 0.03, w));
                    if (i % 5 == 0)
                        SacredDraw.Box(m, b, -0.6, 0.6, 0, 0.1, -0.06, 0.06, Looks.Of(MeshColor.FromHex(0x4E3626), MaterialChannel.Wood), BoxFaces.All & ~BoxFaces.Bottom);
                    else SacredDraw.Box(m, b, -0.11, 0.11, 0, 0.06, -0.05, 0.05, i % 2 == 0 ? KdsLooks.Brick : KdsLooks.Rubble, BoxFaces.All & ~BoxFaces.Bottom);
                }
            }
            if (lod <= 1)
            {
                // Grass and weeds grow over the top and along the treads (KAT1890 02, 03, 07): flat green mats of
                // mixed tones lying on the brick, and a few taller tufts at LOD0.
                ShapeBrush[] greens =
                {
                    KdsLooks.Grass, Looks.Of(MeshColor.FromHex(0x5E7A35), MaterialChannel.Grass), Looks.Of(MeshColor.FromHex(0x7D8A48), MaterialChannel.Grass),
                };
                SacredDraw.Quad(m, x.K, -0.8 * h3, top + 0.02, -0.75 * h3, 0.78 * h3, top + 0.02, -0.8 * h3, 0.82 * h3, top + 0.02, 0.7 * h3, -0.7 * h3, top + 0.02,
                                0.8 * h3, 0, 1, 0, greens[1]);
                int per = lod == 0 ? 5 : 2;
                for (int sIdx = 0; sIdx < st.Length - 1; sIdx++)
                {
                    double outer = 0.5 * st[sIdx] - 0.08, inner = 0.5 * st[sIdx + 1] + 0.02, v = (sIdx + 1) * top / st.Length + 0.015;
                    for (int side = 0; side < 4; side++)
                    {
                        for (int q = 0; q < per; q++)
                        {
                            double a0 = -1 + 2 * (q + 0.15 * Rnd(ref seed)) / per, a1 = a0 + (1.2 + 0.6 * Rnd(ref seed)) / per;
                            a1 = Math.Min(1, a1);
                            double u0, w0, u1, w1, u2, w2, u3, w3;
                            SacredParts.OnRect(side, a0, outer, outer, out u0, out w0);
                            SacredParts.OnRect(side, a1, outer, outer, out u1, out w1);
                            SacredParts.OnRect(side, a1, inner, inner, out u2, out w2);
                            SacredParts.OnRect(side, a0, inner, inner, out u3, out w3);
                            SacredDraw.Quad(m, x.K, u0, v, w0, u1, v, w1, u2, v, w2, u3, v, w3, 0, 1, 0, greens[(sIdx + side + q) % 3]);
                        }
                    }
                }
                // Weeds hang down the broken risers in patches (KAT1890 02, 03): green curtains over part of each face.
                for (int sIdx = 0; sIdx < st.Length; sIdx++)
                {
                    double half = 0.5 * st[sIdx] + 0.012, v1 = (sIdx + 1) * top / st.Length, v0 = v1 - top / st.Length;
                    for (int side = 0; side < 4; side++)
                    {
                        Affine3 sk = KdsKit.Side(x.K, side);
                        for (int q = 0; q < per / 2 + 1; q++)
                        {
                            double c = (Rnd(ref seed) - 0.5) * 1.4 * half, wq = (0.25 + 0.35 * Rnd(ref seed)) * half;
                            double drop = v0 + (0.15 + 0.5 * Rnd(ref seed)) * (v1 - v0);
                            SacredDraw.Quad(m, sk, c - wq, drop, half, c + wq, drop + 0.08, half, c + wq, v1, half, c - wq, v1, half, 0, 0, 1,
                                            greens[(sIdx + side + q + 1) % 3]);
                        }
                    }
                }
                if (lod == 0)
                {
                    for (int i = 0; i < 14; i++)
                    {
                        int sIdx = i % 4;
                        double a = 0.5 * st[sIdx] - 0.25, v = (sIdx + 1) * top / st.Length;
                        double u, w;
                        SacredParts.OnRect((int)(Rnd(ref seed) * 4) % 4, (Rnd(ref seed) - 0.5) * 1.8, a, a, out u, out w);
                        Shapes.Cone(m, SacredDraw.At(x.K, u, v, w), greens[i % 3], 0.2 + 0.12 * Rnd(ref seed), 0.3 + 0.25 * Rnd(ref seed), 5, 0, 0, false);
                    }
                }
            }
            // The wire-mesh fence round the outline (or a square) and the water tank at the north-west.
            double[] fu = ShapeScratch.U2, fw = ShapeScratch.W2;
            int n;
            if (x.OutU != null && x.OutU.Length >= 3 && x.OutU.Length <= 32)
            {
                n = x.OutU.Length;
                double cu = 0, cw = 0;
                for (int i = 0; i < n; i++)
                {
                    cu += x.OutU[i] / n;
                    cw += x.OutW[i] / n;
                }
                for (int i = 0; i < n; i++)
                {
                    double du = x.OutU[i] - cu, dw = x.OutW[i] - cw, l = Math.Sqrt(du * du + dw * dw);
                    fu[i] = x.OutU[i] - (l > 1e-6 ? 0.35 * du / l : 0);
                    fw[i] = x.OutW[i] - (l > 1e-6 ? 0.35 * dw / l : 0);
                }
            }
            else
            {
                n = KdsKit.Rect(6.0, 6.0, fu, fw);
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                KdsKit.WireFence(x, fu[i], fw[i], fu[j], fw[j], 1.2);
            }
            // The tank: north-west in the frame (yaw ≈ 200°: −u is west-north-west, −w north-north-east).
            double tu = -0.5 * st[0] - 0.85, tw = -0.5 * st[0] + 0.6;
            if (x.CorridorDistance(tu, tw) > 1.0 + SacredClearance.MarginM)
            {
                ShapeBrush tank = Looks.Of(MeshColor.FromHex(0x23252A), MaterialChannel.Plain);
                var p = new Profile2(10);
                p.Add(0, 0, true).Add(0.7, 0, true).Add(0.75, 0.15).Add(0.72, 0.9).Add(0.75, 1.5).Add(0.62, 1.68).Add(0.25, 1.75).Add(0, 1.75, true);
                Shapes.Lathe(m, SacredDraw.At(x.K, tu, 0, tw), tank, p, KdsKit.Seg(12, lod));
                x.Cylinder(tu, tw, 0, 1.75, 0.75);
            }
            return RuinTopM;
        }

        private static double Rnd(ref uint s)
        {
            s = s * 1664525u + 1013904223u;
            return (s >> 8) / 16777216.0;
        }

        // ---------------------------------------------------------------------------------------------------------------
        // T10 Dabali
        // ---------------------------------------------------------------------------------------------------------------

        /// <summary>The Basantapur Dabali: the dance platforms on the OSM outline w906786371 split into its two rectangles
        /// (the main stage and the strip north of it), red-painted brick sides with a grey stone capping and corner blocks;
        /// the carved stone face block with its iron chain on the main stage's front.</summary>
        public static double Dabali(KdsCtx x)
        {
            double au0, au1, aw0, aw1, bu0, bu1, bw0, bw1;
            if (!SplitL(x, out au0, out au1, out aw0, out aw1, out bu0, out bu1, out bw0, out bw1))
            {
                au0 = -2.9;
                au1 = 2.9;
                aw0 = -3.15;
                aw1 = 3.15;
                bu0 = bu1 = bw0 = bw1 = 0;
            }
            Platform(x, au0, au1, aw0, aw1, 0.55);
            if (bu1 - bu0 > 0.5 && bw1 - bw0 > 0.5) Platform(x, bu0, bu1, bw0, bw1, 0.45);
            if (x.Lod == 0)
            {
                // The carved stone face block and the chain, on the larger platform's front (+w) face.
                double fu = 0.5 * (au0 + au1), fw = aw1;
                SacredDraw.Box(x.M, x.K, fu - 0.3, fu + 0.3, 0.05, 0.5, fw, fw + 0.12, KdsLooks.StoneSoot, BoxFaces.All & ~BoxFaces.Bottom & ~BoxFaces.Back);
                Shapes.Ellipsoid(x.M, SacredDraw.At(x.K, fu, 0.28, fw + 0.13), KdsLooks.StoneBlack, 0.18, 0.15, 0.05, 6, false);
                ShapeBrush iron = Looks.Of(MeshColor.FromHex(0x2E2B28), MaterialChannel.Metal);
                for (int i = 0; i < 6; i++)
                    Shapes.Torus(x.M, Affine3.RotationZ(0.5 * Math.PI * (i % 2)).Then(SacredDraw.At(x.K, fu + 0.32 + 0.07 * i, 0.4 - 0.05 * i, fw + 0.15)), iron, 0.035, 0.008,
                                 6, 3);
            }
            return DabaliTopM;
        }

        /// <summary>One platform (frame rectangle u0..u1 × w0..w1, height h): red-painted brick sides, grey stone capping
        /// slabs overhanging a little and grey corner blocks; walkable.</summary>
        private static void Platform(KdsCtx x, double u0, double u1, double w0, double w1, double h)
        {
            MeshData m = x.M;
            double cu = 0.5 * (u0 + u1), cw = 0.5 * (w0 + w1), hu = 0.5 * (u1 - u0), hw = 0.5 * (w1 - w0);
            Mould p = SacredDraw.M;
            if (x.Lod >= 2) p.Add(0, -0.25, KdsLooks.BrickPainted).Add(0, h - 0.12).Add(0.04, h - 0.12, KdsLooks.Flag).Add(0.04, h);
            else
                p.Add(0.03, -0.25, KdsLooks.Flag).Add(0.03, 0.06).Add(0, 0.06, KdsLooks.BrickPainted).Add(0, h - 0.14).Add(0.05, h - 0.14, KdsLooks.Flag).Add(0.05, h);
            SacredDraw.RectRing(m, x.K, cu, cw, hu, hw, p, true, KdsLooks.Flag);
            if (x.Lod <= 1)
            {
                for (int a = -1; a <= 1; a += 2)
                    for (int b = -1; b <= 1; b += 2)
                    {
                        double u = cu + a * (hu - 0.14), w = cw + b * (hw - 0.14);
                        SacredDraw.Box(m, x.K, u - 0.17, u + 0.17, -0.1, h - 0.13, w - 0.17, w + 0.17, KdsLooks.StoneGrey, BoxFaces.Wall);
                    }
                if (x.Lod == 0)
                {
                    // Joints between the capping slabs.
                    int n = Math.Max(2, (int)(2 * hu / 0.9));
                    for (int i = 1; i < n; i++)
                    {
                        double u = u0 + (u1 - u0) * i / n;
                        SacredDraw.Box(m, x.K, u - 0.01, u + 0.01, h, h + 0.004, w0 - 0.04, w1 + 0.04, KdsLooks.StoneSoot, BoxFaces.Top);
                    }
                }
            }
            x.Floor(u0 - 0.05, u1 + 0.05, w0 - 0.05, w1 + 0.05, -0.25, h, GenColliders.Stone);
        }

        /// <summary>Splits an L-shaped outline (in frame coordinates) into its two rectangles: the bounding box minus the
        /// missing corner. False when the outline is missing or not an L.</summary>
        private static bool SplitL(KdsCtx x, out double au0, out double au1, out double aw0, out double aw1, out double bu0, out double bu1, out double bw0,
                                   out double bw1)
        {
            au0 = au1 = aw0 = aw1 = bu0 = bu1 = bw0 = bw1 = 0;
            if (x.OutU == null || x.OutU.Length < 4) return false;
            int n = x.OutU.Length;
            double u0 = double.MaxValue, u1 = double.MinValue, w0 = double.MaxValue, w1 = double.MinValue;
            for (int i = 0; i < n; i++)
            {
                u0 = Math.Min(u0, x.OutU[i]);
                u1 = Math.Max(u1, x.OutU[i]);
                w0 = Math.Min(w0, x.OutW[i]);
                w1 = Math.Max(w1, x.OutW[i]);
            }
            // The bounding-box corner farthest from the outline is the missing one.
            int best = -1;
            double far = 0;
            for (int c = 0; c < 4; c++)
            {
                double cu = c % 2 == 0 ? u0 : u1, cw = c < 2 ? w0 : w1, near = double.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    double du = x.OutU[i] - cu, dw = x.OutW[i] - cw;
                    near = Math.Min(near, du * du + dw * dw);
                }
                if (near > far)
                {
                    far = near;
                    best = c;
                }
            }
            if (best < 0 || far < 1.0)
            {
                au0 = u0;
                au1 = u1;
                aw0 = w0;
                aw1 = w1;
                return true;
            }
            double mu = best % 2 == 0 ? u0 : u1, mw = best < 2 ? w0 : w1;
            // The reflex vertex (the outline turns clockwise there; the outline runs counter-clockwise) nearest the missing
            // corner.
            double ru = 0, rw = 0, rn = double.MaxValue;
            for (int i = 0; i < n; i++)
            {
                int a = (i + n - 1) % n, b = (i + 1) % n;
                double cross = (x.OutU[i] - x.OutU[a]) * (x.OutW[b] - x.OutW[i]) - (x.OutW[i] - x.OutW[a]) * (x.OutU[b] - x.OutU[i]);
                if (cross >= 0) continue;
                double du = x.OutU[i] - mu, dw = x.OutW[i] - mw, d = du * du + dw * dw;
                if (d < rn)
                {
                    rn = d;
                    ru = x.OutU[i];
                    rw = x.OutW[i];
                }
            }
            if (rn == double.MaxValue)
            {
                au0 = u0;
                au1 = u1;
                aw0 = w0;
                aw1 = w1;
                return true;
            }
            // A: the full w span on the side of the reflex vertex away from the missing corner; B: the rest.
            if (best % 2 == 0)
            {
                au0 = ru;
                au1 = u1;
                bu0 = u0;
                bu1 = ru;
            }
            else
            {
                au0 = u0;
                au1 = ru;
                bu0 = ru;
                bu1 = u1;
            }
            aw0 = w0;
            aw1 = w1;
            if (best < 2)
            {
                bw0 = rw;
                bw1 = w1;
            }
            else
            {
                bw0 = w0;
                bw1 = rw;
            }
            // Keep the larger piece as A.
            if ((bu1 - bu0) * (bw1 - bw0) > (au1 - au0) * (aw1 - aw0))
            {
                double t;
                t = au0; au0 = bu0; bu0 = t;
                t = au1; au1 = bu1; bu1 = t;
                t = aw0; aw0 = bw0; bw0 = t;
                t = aw1; aw1 = bw1; bw1 = t;
            }
            return true;
        }

        // ---------------------------------------------------------------------------------------------------------------
        // T28 Basantapur square
        // ---------------------------------------------------------------------------------------------------------------

        /// <summary>The market's brick (red-brown herringbone, sun-faded; judge round 1 against basantapur2 c_03, c_06,
        /// 2023-25), its joints and the grey stone kerb and border.</summary>
        private static readonly ShapeBrush MarketBrick = Looks.Of(MeshColor.FromHex(0x9C4E3A), MaterialChannel.Brick);

        private static readonly ShapeBrush MarketJoint = Looks.Of(MeshColor.FromHex(0x6E5A4E), MaterialChannel.Brick);

        /// <summary>The stall cloths: royal blue and red, draped to the ground (basantapur2 c_00-c_07).</summary>
        private static readonly ShapeBrush ClothRoyal = Looks.Of(MeshColor.FromHex(0x2D4FB0), MaterialChannel.Fabric);

        private static readonly ShapeBrush ClothCrimson = Looks.Of(MeshColor.FromHex(0xB3262B), MaterialChannel.Fabric);

        /// <summary>The goods: bronze, brass, copper and dark (wood, iron, horn) curios (judge round 1 colours).</summary>
        private static readonly ShapeBrush[] Goods =
        {
            Looks.Of(MeshColor.FromHex(0x8C6A32), MaterialChannel.Metal), Looks.Of(MeshColor.FromHex(0xC9A13B), MaterialChannel.Gilt),
            Looks.Of(MeshColor.FromHex(0x9A5B3A), MaterialChannel.Metal), Looks.Of(MeshColor.FromHex(0x3A2E28), MaterialChannel.Wood),
        };

        /// <summary>Height (m) of the raised market platform over the square.</summary>
        public const double PlatformM = 0.22;

        /// <summary>
        /// The raised brick platform of the marketplace outline (OSM w218440319), level on the square (KdsGround) and paved
        /// wall to wall (judge round 1: no holes where the square's footway corridors cross it), edged by a grey stone kerb
        /// and border; the curio market on it in long runs of skirted tables, and tall lamp posts.
        /// </summary>
        public static double Square(KdsCtx x)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            // The marketplace corners in the frame (the frame was placed against the Dabali's outline: apply the same shift).
            double ox, oz;
            KdsTemplesSite.LocalToTile(x.Tile, -45.0, -96.0, out ox, out oz);
            double sx = x.Kf.OX - ox, sz = x.Kf.OZ - oz;
            double[] pu = new double[4], pw = new double[4];
            double u0 = double.MaxValue, u1 = double.MinValue, w0 = double.MaxValue, w1 = double.MinValue;
            for (int i = 0; i < 4; i++)
            {
                double tx, tz;
                KdsTemplesSite.LocalToTile(x.Tile, MarketX[i], MarketY[i], out tx, out tz);
                x.Local(tx + sx, tz + sz, out pu[i], out pw[i]);
                u0 = Math.Min(u0, pu[i]);
                u1 = Math.Max(u1, pu[i]);
                w0 = Math.Min(w0, pw[i]);
                w1 = Math.Max(w1, pw[i]);
            }
            double lift = PlatformM;
            // The brick field in cells (their tone varies a little, the brick pattern is the material's), clipped to the
            // outline; the grey stone border band and the kerb faces along the outline.
            double pitch = lod == 0 ? 2.4 : lod == 1 ? 4.8 : lod == 2 ? 9.6 : 0;
            uint seed = 0x42415341u;
            if (pitch > 0)
            {
                int nu = (int)Math.Ceiling((u1 - u0) / pitch), nw = (int)Math.Ceiling((w1 - w0) / pitch);
                for (int j = 0; j < nw; j++)
                {
                    for (int i = 0; i < nu; i++)
                    {
                        double a0 = u0 + i * pitch, b0 = w0 + j * pitch, a1 = a0 + pitch, b1 = b0 + pitch;
                        if (!ClipCell(pu, pw, ref a0, ref a1, ref b0, ref b1)) continue;
                        float tone = 0.93f + 0.14f * (float)Rnd(ref seed);
                        ShapeBrush top = Looks.Of(MeshColor.Scale(MarketBrick.Color, tone), MaterialChannel.Brick);
                        SacredDraw.Quad(m, x.K, a0, x.GroundV(a0, b0) + lift, b0, a1, x.GroundV(a1, b0) + lift, b0, a1, x.GroundV(a1, b1) + lift, b1, a0,
                                        x.GroundV(a0, b1) + lift, b1, 0, 1, 0, top);
                    }
                }
                // Joint lines between the brick panels at LOD0 (the dark mortar bands of the herringbone fields).
                if (lod == 0)
                {
                    for (double a = u0 + 4.8; a < u1 - 1.0; a += 4.8)
                    {
                        double lo, hi;
                        if (!SpanAtU(pu, pw, a, out lo, out hi)) continue;
                        SacredDraw.Quad(m, x.K, a - 0.04, lift + 0.004, lo + 0.4, a + 0.04, lift + 0.004, lo + 0.4, a + 0.04, lift + 0.004, hi - 0.4, a - 0.04,
                                        lift + 0.004, hi - 0.4, 0, 1, 0, MarketJoint);
                    }
                }
            }
            else
            {
                SacredDraw.Quad(m, x.K, pu[0], lift, pw[0], pu[1], lift, pw[1], pu[2], lift, pw[2], pu[3], lift, pw[3], 0, 1, 0, MarketBrick);
            }
            // Kerb faces and the grey flag border round the outline (c_dabli 00).
            ShapeBrush border = KdsLooks.Flag, kerb = Looks.Ao(KdsLooks.StoneGrey, 0.85f);
            double cu = 0.25 * (pu[0] + pu[1] + pu[2] + pu[3]), cw = 0.25 * (pw[0] + pw[1] + pw[2] + pw[3]);
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                double au = pu[i], aw = pw[i], bu = pu[j], bw = pw[j];
                double nu = bw - aw, nw = -(bu - au), l = Math.Sqrt(nu * nu + nw * nw);
                nu /= l;
                nw /= l;
                if ((cu - au) * nu + (cw - aw) * nw > 0)
                {
                    nu = -nu;
                    nw = -nw;
                }
                SacredDraw.Quad(m, x.K, au, -0.1, aw, bu, -0.1, bw, bu, lift, bw, au, lift, aw, nu, 0, nw, kerb);
                if (lod <= 1)
                {
                    const double kb = 0.45;
                    SacredDraw.Quad(m, x.K, au, lift + 0.008, aw, bu, lift + 0.008, bw, bu - kb * nu, lift + 0.008, bw - kb * nw, au - kb * nu, lift + 0.008,
                                    aw - kb * nw, 0, 1, 0, border);
                }
            }
            // Walkable: the platform's top in strips across it.
            for (int k = 0; k < 4; k++)
            {
                double a0 = u0 + (u1 - u0) * k / 4, a1 = u0 + (u1 - u0) * (k + 1) / 4;
                double lo = double.MaxValue, hi = double.MinValue;
                for (int q = 0; q <= 4; q++)
                {
                    double l2, h2;
                    if (!SpanAtU(pu, pw, a0 + (a1 - a0) * q / 4, out l2, out h2)) continue;
                    lo = Math.Min(lo, l2);
                    hi = Math.Max(hi, h2);
                }
                if (lo < hi) x.Floor(a0, a1, lo + 0.3, hi - 0.3, -0.3, lift, GenColliders.Brick);
            }
            Stalls(x, pu, pw, u0, u1, w0, w1, lift);
            return SquareTopM;
        }

        /// <summary>Point in the convex quadrilateral (either winding).</summary>
        private static bool Inside(double[] pu, double[] pw, double u, double w)
        {
            int pos = 0, neg = 0;
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                double c = (pu[j] - pu[i]) * (w - pw[i]) - (pw[j] - pw[i]) * (u - pu[i]);
                if (c > 0) pos++;
                else if (c < 0) neg++;
            }
            return pos == 0 || neg == 0;
        }

        /// <summary>Clips a frame cell to the (convex) outline's w span at its u range; false when nothing is left.</summary>
        private static bool ClipCell(double[] pu, double[] pw, ref double a0, ref double a1, ref double b0, ref double b1)
        {
            double l0, h0, l1, h1;
            if (!SpanAtU(pu, pw, a0, out l0, out h0) | !SpanAtU(pu, pw, a1, out l1, out h1))
            {
                // Trim the cell's u range to where the outline is.
                double umin = Math.Min(Math.Min(pu[0], pu[1]), Math.Min(pu[2], pu[3])), umax = Math.Max(Math.Max(pu[0], pu[1]), Math.Max(pu[2], pu[3]));
                a0 = Math.Max(a0, umin + 0.01);
                a1 = Math.Min(a1, umax - 0.01);
                if (a1 - a0 < 0.1 || !SpanAtU(pu, pw, a0, out l0, out h0) || !SpanAtU(pu, pw, a1, out l1, out h1)) return false;
            }
            double lo = Math.Max(l0, l1), hi = Math.Min(h0, h1);
            b0 = Math.Max(b0, lo);
            b1 = Math.Min(b1, hi);
            return b1 - b0 > 0.05;
        }

        /// <summary>The (convex) outline's w span at u.</summary>
        private static bool SpanAtU(double[] pu, double[] pw, double u, out double lo, out double hi)
        {
            lo = double.MaxValue;
            hi = double.MinValue;
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                double ua = pu[i], ub = pu[j];
                if ((u < Math.Min(ua, ub)) || (u > Math.Max(ua, ub)) || Math.Abs(ub - ua) < 1e-9) continue;
                double t = (u - ua) / (ub - ua), w = pw[i] + (pw[j] - pw[i]) * t;
                lo = Math.Min(lo, w);
                hi = Math.Max(hi, w);
            }
            return hi > lo;
        }

        /// <summary>
        /// The curio market (judge round 1 against basantapur2 c_00-c_07, 2023-25): long rows of low trestle tables
        /// (2.4 × 0.9 m, 0.5 m high) end to end in runs of three to five along the platform, with 4 m aisles; each draped to
        /// the ground in royal-blue or red cloth and heaped with brass, bronze and copper statues, masks, singing bowls,
        /// khukuris and beads (a dense carpet of small pieces at LOD0, one bumpy slab of goods beyond); at most one patio
        /// umbrella to a run; tall lamp posts along the long edges. Tables keep out of the drawn roads and the vehicle
        /// roads' corridors.
        /// </summary>
        private static void Stalls(KdsCtx x, double[] pu, double[] pw, double u0, double u1, double w0, double w1, double lift)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            uint seed = 0x5354414Cu;
            const double tl = 2.4, td = 0.9, th = 0.5, aisle = 4.0;
            KdsVehicleRoads vehicles = x.Tile != null ? KdsVehicleRoads.For(x.Tile) : null;
            int row = 0;
            for (double w = w0 + 2.6; w < w1 - 2.0; w += td + aisle, row++)
            {
                double u = u0 + 2.0 + 1.5 * Rnd(ref seed);
                int run = 0;
                while (u < u1 - 2.0)
                {
                    int n = 3 + (int)(Rnd(ref seed) * 3);
                    double len = n * tl;
                    // Skip runs that would leave the outline or touch a drawn road or a vehicle road's corridor; about one in
                    // four gaps is a wider opening.
                    bool ok = Rnd(ref seed) > 0.22;
                    for (int q = 0; q <= n && ok; q++)
                    {
                        double a = u + Math.Min(q * tl, len);
                        for (int e = -1; e <= 1 && ok; e += 2)
                        {
                            double b = w + e * 0.5 * td;
                            if (!Inside(pu, pw, a, b)) ok = false;
                            else if (x.DrawnDistance(a, b) < 0.4) ok = false;
                            else if (vehicles != null)
                            {
                                double px, py, pz;
                                x.World(a, 0, b, out px, out py, out pz);
                                if (vehicles.Distance(px, pz) < SacredClearance.MarginM) ok = false;
                            }
                        }
                    }
                    if (ok && lod >= 3)
                    {
                        // Far off a run is one cloth-coloured top.
                        double tv = lift + th;
                        SacredDraw.Quad(m, x.K, u, tv, w - 0.5 * td, u + len, tv, w - 0.5 * td, u + len, tv, w + 0.5 * td, u, tv, w + 0.5 * td, 0, 1, 0,
                                        run % 5 < 3 ? ClothRoyal : ClothCrimson);
                        run++;
                    }
                    else if (ok)
                    {
                        double g = lift;
                        for (int q = 0; q < n; q++)
                        {
                            double cu = u + (q + 0.5) * tl;
                            Table(x, cu, w, tl, td, g, th, Rnd(ref seed) < 0.6 ? ClothRoyal : ClothCrimson, ref seed);
                        }
                        x.Solid(u, u + len, w - 0.5 * td, w + 0.5 * td, g - 0.2, g + th, GenColliders.Wood);
                        // At most one patio umbrella to a run, and only on some runs (the 2023-25 photos show almost none).
                        if (lod <= 1 && (row * 5 + run) % 4 == 1)
                        {
                            double au = u + tl * (0.5 + (int)(Rnd(ref seed) * n)), aw = w - 0.5 * td - 0.4;
                            ShapeBrush c = run % 2 == 0 ? KdsLooks.PaintWhite : ClothRoyal;
                            Shapes.Cylinder(m, SacredDraw.At(x.K, au, g, aw), KdsLooks.MetalGrey, 0.025, 2.2, 4);
                            Shapes.Cone(m, SacredDraw.At(x.K, au, g + 1.95, aw), c, 1.2, 0.5, lod == 0 ? 8 : 6, 0, 0, true);
                        }
                        run++;
                    }
                    u += len + 3.5 + 1.5 * Rnd(ref seed);
                }
            }
            // Lamp posts along the two long edges.
            if (lod <= 1)
            {
                ShapeBrush pole = Looks.Of(MeshColor.FromHex(0x2C2D30), MaterialChannel.Metal);
                for (int e = 0; e < 2; e++)
                {
                    int a = e == 0 ? 0 : 2, b = e == 0 ? 1 : 3;
                    for (int q = 1; q <= 3; q++)
                    {
                        double t = q / 4.0, u = pu[a] + (pu[b] - pu[a]) * t, w = pw[a] + (pw[b] - pw[a]) * t;
                        // Step 1 m inside the outline.
                        double cu = 0.25 * (pu[0] + pu[1] + pu[2] + pu[3]), cw = 0.25 * (pw[0] + pw[1] + pw[2] + pw[3]), dx = cu - u, dz = cw - w, l = Math.Sqrt(dx * dx + dz * dz);
                        u += dx / l;
                        w += dz / l;
                        if (x.DrawnDistance(u, w) < 0.4) continue;
                        double g = lift;
                        Shapes.Cylinder(m, SacredDraw.At(x.K, u, g, w), pole, 0.07, 5.4, 6);
                        Shapes.Bar(m, x.K, pole, u, g + 5.2, w, u + 0.6, g + 5.3, w, 0.03, 4);
                        SacredDraw.Box(m, x.K, u + 0.48, u + 0.72, g + 4.95, g + 5.25, w - 0.12, w + 0.12, Looks.Of(MeshColor.FromHex(0xF2E6B8), MaterialChannel.Glass),
                                       BoxFaces.All);
                        x.Cylinder(u, w, g, g + 5.4, 0.1);
                    }
                }
            }
        }

        /// <summary>
        /// One trestle table (centre u, w; length tl along u, depth td; top at g + th) draped in cloth to 0.05 m over the
        /// platform, with its goods: at LOD0 a dense carpet of small brass, bronze, copper and dark pieces (upright figures,
        /// bowls and discs, masks leaning back, khukuris and strings of beads); at LOD1 a bumpy slab of goods; at LOD2 a
        /// flat bronze top.
        /// </summary>
        private static void Table(KdsCtx x, double u, double w, double tl, double td, double g, double th, in ShapeBrush cloth, ref uint seed)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            double hu = 0.5 * tl - 0.02, hw = 0.5 * td, top = g + th;
            // The cloth: top and skirts (no legs show under a skirt to the ground).
            SacredDraw.Box(m, x.K, u - hu, u + hu, g + 0.05, top, w - hw, w + hw, cloth, lod == 0 ? BoxFaces.All & ~BoxFaces.Bottom : BoxFaces.Top | BoxFaces.Wall);
            if (lod == 0)
            {
                // A dense carpet of goods, 25 pieces of 0.08-0.35 m, each a few triangles (the site's LOD0 budget).
                const int n = 25;
                for (int i = 0; i < n; i++)
                {
                    double gu = u - hu + 0.12 + (2 * hu - 0.24) * Rnd(ref seed), gw = w - hw + 0.1 + (2 * hw - 0.2) * Rnd(ref seed);
                    double r = Rnd(ref seed), sz = 0.08 + 0.27 * Rnd(ref seed) * Rnd(ref seed);
                    ShapeBrush b = Goods[(int)(Rnd(ref seed) * 4) & 3];
                    if (r < 0.34)
                    {
                        // An upright figure: a tapering four-sided body with a small head.
                        Shapes.Cone(m, SacredDraw.At(x.K, gu, top, gw), b, 0.35 * sz + 0.03, sz + 0.06, 3, 0, 0, false);
                    }
                    else if (r < 0.6)
                    {
                        // A singing bowl or a plate: a low wide cone.
                        Shapes.Cone(m, SacredDraw.At(x.K, gu, top, gw), b, 0.5 * sz + 0.04, 0.05 + 0.1 * sz, 4, 0, 0, false);
                    }
                    else if (r < 0.82)
                    {
                        // A mask leaning back against the goods behind it.
                        double mh = 0.6 * sz + 0.1, mw = 0.4 * sz + 0.08;
                        SacredDraw.Box(m, x.K, gu - 0.5 * mw, gu + 0.5 * mw, top, top + mh, gw - 0.03, gw + 0.03, b, BoxFaces.Front | BoxFaces.Top);
                    }
                    else
                    {
                        // A khukuri or a string of beads lying flat.
                        double ll = 0.5 * sz + 0.15;
                        SacredDraw.Box(m, x.K, gu - 0.5 * ll, gu + 0.5 * ll, top, top + 0.025, gw - 0.025, gw + 0.025, b, BoxFaces.Top);
                    }
                }
            }
            else if (lod == 1)
            {
                // One bumpy slab of goods.
                SacredDraw.Box(m, x.K, u - hu + 0.1, u + hu - 0.1, top, top + 0.12, w - hw + 0.08, w + hw - 0.08, Goods[0], BoxFaces.Top | BoxFaces.Front | BoxFaces.Back);
                SacredDraw.Box(m, x.K, u - 0.4 * hu, u + 0.2 * hu, top + 0.12, top + 0.22, w - 0.3 * hw, w + 0.2 * hw, Goods[1], BoxFaces.Top | BoxFaces.Front);
            }
            else
            {
                double tv = top + 0.01;
                SacredDraw.Quad(m, x.K, u - hu + 0.1, tv, w - hw + 0.08, u + hu - 0.1, tv, w - hw + 0.08, u + hu - 0.1, tv, w + hw - 0.08, u - hu + 0.1, tv,
                                w + hw - 0.08, 0, 1, 0, Goods[0]);
            }
        }
    }
}
