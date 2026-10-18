using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsTemples
{
    /// <summary>
    /// The open ground of the square (kds-temples.md T6, T10, T28): the ruined Narayan temple (destroyed in 2015 and not
    /// rebuilt: a broken four-stage brick plinth heaped with rubble and weeds behind a wire-mesh fence, a black water tank at
    /// its north-west corner); the Basantapur Dabali (the low red-sided, stone-capped dance platforms east of Trailokya
    /// Mohan, on their OSM outline); and the Basantapur square: the raised buff-pink brick paving with a grey stone border
    /// on the marketplace outline (OSM w218440319), kept out of the road corridors and draped on the terrain, with the grid
    /// of curio stalls (low tables and mats laden with bronzes, singing bowls and masks, red tarpaulins, patio umbrellas)
    /// and tall lamp posts.
    /// </summary>
    internal static class Basantapur
    {
        public const float RuinTopM = 2.9f;
        public const float DabaliTopM = 0.6f;
        /// <summary>The square's top: its lamp posts (5.6 m) on the rising east end of the paving.</summary>
        public const float SquareTopM = 6.6f;

        // The marketplace outline (w218440319) in the brief's local frame (kds-temples.md T28).
        private static readonly double[] MarketX = { -82.9, -4.4, -3.7, -86.2 }, MarketY = { -76.8, -92.9, -115.8, -95.9 };

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

        /// <summary>The raised brick paving and the curio stalls of the marketplace outline, draped on the terrain and kept out
        /// of the road corridors.</summary>
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
            // The brick pattern is the material's; the cells only drape the paving on the ground (the square is level to a
            // few centimetres over 3 m).
            double pitch = lod == 0 ? 2.8 : lod == 1 ? 4.0 : lod == 2 ? 5.0 : 6.0, lift = 0.22;
            int nu = (int)Math.Ceiling((u1 - u0) / pitch), nw = (int)Math.Ceiling((w1 - w0) / pitch);
            var keep = new bool[nu * nw];
            for (int j = 0; j < nw; j++)
            {
                for (int i = 0; i < nu; i++)
                {
                    double a0 = u0 + i * pitch, b0 = w0 + j * pitch;
                    bool ok = Inside(pu, pw, a0 + 0.5 * pitch, b0 + 0.5 * pitch);
                    for (int c = 0; c < 4 && ok; c++)
                    {
                        double cu = a0 + (c % 2) * pitch, cw = b0 + (c / 2) * pitch;
                        if (!Inside(pu, pw, cu, cw) || x.CorridorDistance(cu, cw) < SacredClearance.MarginM) ok = false;
                    }
                    keep[j * nu + i] = ok;
                }
            }
            ShapeBrush border = KdsLooks.Flag, kerb = Looks.Ao(KdsLooks.Flag, 0.85f);
            uint seed = 0x42415341u;
            for (int j = 0; j < nw; j++)
            {
                int runStart = -1;
                double runLo = 0, runHi = 0;
                for (int i = 0; i <= nu; i++)
                {
                    bool k = i < nu && keep[j * nu + i];
                    double a0 = u0 + i * pitch, b0 = w0 + j * pitch, a1 = a0 + pitch, b1 = b0 + pitch;
                    if (k)
                    {
                        bool edge = !Kept(keep, nu, nw, i - 1, j) || !Kept(keep, nu, nw, i + 1, j) || !Kept(keep, nu, nw, i, j - 1) || !Kept(keep, nu, nw, i, j + 1);
                        double g00 = x.GroundV(a0, b0), g10 = x.GroundV(a1, b0), g11 = x.GroundV(a1, b1), g01 = x.GroundV(a0, b1);
                        ShapeBrush top = Looks.Of(MeshColor.Scale(KdsLooks.BrickBuff.Color, 0.95f + 0.1f * (float)Rnd(ref seed)), MaterialChannel.Brick);
                        SacredDraw.Quad(m, x.K, a0, g00 + lift, b0, a1, g10 + lift, b0, a1, g11 + lift, b1, a0, g01 + lift, b1, 0, 1, 0, top);
                        // The grey stone kerb along the platform's edge: a narrow band on the cells that end it (c_dabli 00).
                        if (edge && lod <= 1)
                        {
                            const double kb = 0.35, kl = 0.012;
                            if (!Kept(keep, nu, nw, i - 1, j))
                                SacredDraw.Quad(m, x.K, a0, g00 + lift + kl, b0, a0 + kb, g00 + lift + kl, b0, a0 + kb, g01 + lift + kl, b1, a0, g01 + lift + kl, b1, 0, 1, 0, border);
                            if (!Kept(keep, nu, nw, i + 1, j))
                                SacredDraw.Quad(m, x.K, a1 - kb, g10 + lift + kl, b0, a1, g10 + lift + kl, b0, a1, g11 + lift + kl, b1, a1 - kb, g11 + lift + kl, b1, 0, 1, 0, border);
                            if (!Kept(keep, nu, nw, i, j - 1))
                                SacredDraw.Quad(m, x.K, a0, g00 + lift + kl, b0, a1, g10 + lift + kl, b0, a1, g10 + lift + kl, b0 + kb, a0, g00 + lift + kl, b0 + kb, 0, 1, 0, border);
                            if (!Kept(keep, nu, nw, i, j + 1))
                                SacredDraw.Quad(m, x.K, a0, g01 + lift + kl, b1 - kb, a1, g11 + lift + kl, b1 - kb, a1, g11 + lift + kl, b1, a0, g01 + lift + kl, b1, 0, 1, 0, border);
                        }
                        // Kerb faces where the paving ends.
                        if (lod >= 3) continue;
                        if (!Kept(keep, nu, nw, i - 1, j)) Kerb(m, x.K, a0, b1, g01, a0, b0, g00, lift, -1, 0, kerb);
                        if (!Kept(keep, nu, nw, i + 1, j)) Kerb(m, x.K, a1, b0, g10, a1, b1, g11, lift, 1, 0, kerb);
                        if (!Kept(keep, nu, nw, i, j - 1)) Kerb(m, x.K, a0, b0, g00, a1, b0, g10, lift, 0, -1, kerb);
                        if (!Kept(keep, nu, nw, i, j + 1)) Kerb(m, x.K, a1, b1, g11, a0, b1, g01, lift, 0, 1, kerb);
                        double lo = Math.Min(Math.Min(g00, g10), Math.Min(g11, g01)), hi = Math.Max(Math.Max(g00, g10), Math.Max(g11, g01));
                        if (runStart < 0)
                        {
                            runStart = i;
                            runLo = lo;
                            runHi = hi;
                        }
                        else if (Math.Max(runHi, hi) - Math.Min(runLo, lo) > 0.3)
                        {
                            Floor(x, u0 + runStart * pitch, a0, b0, b1, runLo, runHi, lift);
                            runStart = i;
                            runLo = lo;
                            runHi = hi;
                        }
                        else
                        {
                            runLo = Math.Min(runLo, lo);
                            runHi = Math.Max(runHi, hi);
                        }
                    }
                    else if (runStart >= 0)
                    {
                        Floor(x, u0 + runStart * pitch, a0, b0, b1, runLo, runHi, lift);
                        runStart = -1;
                    }
                }
            }
            if (lod <= 2) Stalls(x, pu, pw, u0, u1, w0, w1, lift);
            return SquareTopM;
        }

        private static bool Kept(bool[] keep, int nu, int nw, int i, int j)
        {
            return i >= 0 && j >= 0 && i < nu && j < nw && keep[j * nu + i];
        }

        private static void Kerb(MeshData m, in Affine3 k, double ua, double wa, double ga, double ub, double wb, double gb, double lift, double nu, double nw,
                                 in ShapeBrush b)
        {
            SacredDraw.Quad(m, k, ua, ga - 0.15, wa, ub, gb - 0.15, wb, ub, gb + lift, wb, ua, ga + lift, wa, nu, 0, nw, b);
        }

        private static void Floor(KdsCtx x, double a0, double a1, double b0, double b1, double lo, double hi, double lift)
        {
            x.Floor(a0, a1, b0, b1, lo - 0.3, 0.5 * (lo + hi) + lift, GenColliders.Brick);
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

        /// <summary>The curio market: rows of low tables and ground mats with goods, red tarpaulins on poles over some rows,
        /// patio umbrellas, vendors' stools; and lamp posts along the long edges. Stalls stand clear of the corridors.</summary>
        private static void Stalls(KdsCtx x, double[] pu, double[] pw, double u0, double u1, double w0, double w1, double lift)
        {
            MeshData m = x.M;
            int lod = x.Lod;
            ShapeBrush wood = Looks.Of(MeshColor.FromHex(0x6E4A30), MaterialChannel.Wood);
            ShapeBrush[] mats = { KdsLooks.ClothRed, KdsLooks.ClothBlue, KdsLooks.ClothGreen, KdsLooks.ClothYellow };
            ShapeBrush[] umb = { KdsLooks.PaintWhite, KdsLooks.ClothBlue, KdsLooks.PaintWhite };
            ShapeBrush bronze = Looks.Of(MeshColor.FromHex(0x9C7A3A), MaterialChannel.Metal);
            uint seed = 0x5354414Cu;
            // The stalls stand in long rows along the square (the aerial and tower photos c_basantapur_chowk 00, c_dabli
            // 05): low tables and ground mats side by side with gaps, aisles about 4.5 m apart; a patio umbrella or a red
            // tarpaulin over a few of them.
            const double du = 2.0, dw = 4.5;
            int row = 0;
            for (double w = w0 + 2.4; w < w1 - 1.5; w += dw, row++)
            {
                int col = 0;
                for (double u = u0 + 2.0; u < u1 - 1.5; u += du, col++)
                {
                    double r = Rnd(ref seed);
                    if (r < 0.3) continue; // a gap in the row
                    if (!Inside(pu, pw, u, w) || !Inside(pu, pw, u - 1.0, w - 0.8) || !Inside(pu, pw, u + 1.0, w + 0.8)) continue;
                    if (x.CorridorDistance(u, w) < 1.6 + SacredClearance.MarginM) continue;
                    double g = x.GroundV(u, w) + lift;
                    bool table = r > 0.48;
                    if (table)
                    {
                        // A low table laden with goods, its two leg frames.
                        SacredDraw.Box(m, x.K, u - 0.8, u + 0.8, g + 0.4, g + 0.47, w - 0.42, w + 0.42, wood, BoxFaces.All & ~BoxFaces.Bottom);
                        SacredDraw.Panel(m, x.K, u - 0.8, g + 0.47, u + 0.8, g + 0.475, w, mats[(int)(r * 13) % 4]);
                        if (lod == 0)
                        {
                            SacredDraw.Quad(m, x.K, u - 0.72, g, w + 0.36, u + 0.72, g, w + 0.36, u + 0.72, g + 0.4, w + 0.36, u - 0.72, g + 0.4, w + 0.36, 0, 0, 1,
                                            Looks.Ao(wood, 0.55f));
                            SacredDraw.Quad(m, x.K, u + 0.72, g, w - 0.36, u - 0.72, g, w - 0.36, u - 0.72, g + 0.4, w - 0.36, u + 0.72, g + 0.4, w - 0.36, 0, 0, -1,
                                            Looks.Ao(wood, 0.55f));
                        }
                        x.Solid(u - 0.8, u + 0.8, w - 0.42, w + 0.42, g, g + 0.47, GenColliders.Wood);
                    }
                    else
                    {
                        // A mat on the ground.
                        SacredDraw.Box(m, x.K, u - 0.85, u + 0.85, g, g + 0.03, w - 0.55, w + 0.55, mats[(int)(r * 7) % 4], lod == 0 ? BoxFaces.Top | BoxFaces.Wall : BoxFaces.Top);
                    }
                    double gv = table ? g + 0.47 : g + 0.03;
                    if (lod == 0)
                    {
                        // Every stall is laden (c_views 08-10, 15): bronze figures, singing bowls and painted masks or thangkas
                        // set out along the table or the mat.
                        int items = 3;
                        for (int q = 0; q < items; q++)
                        {
                            double gu = u - 0.6 + 1.2 * q / (items - 1), gw = w + ((q % 2) == 0 ? -0.14 : 0.16), rr = Rnd(ref seed);
                            if (rr < 0.45) Shapes.Cone(m, SacredDraw.At(x.K, gu, gv, gw), bronze, 0.07, 0.2 + 0.12 * rr, 4, 0, 0, false);
                            else if (rr < 0.75) SacredDraw.Box(m, x.K, gu - 0.1, gu + 0.1, gv, gv + 0.07, gw - 0.1, gw + 0.1, bronze, BoxFaces.Top | BoxFaces.Front | BoxFaces.Back);
                            else SacredDraw.Box(m, x.K, gu - 0.09, gu + 0.09, gv, gv + 0.24, gw - 0.02, gw + 0.02, mats[q % 4], BoxFaces.Front | BoxFaces.Top | BoxFaces.Back);
                        }
                    }
                    if (lod <= 1 && (row * 7 + col) % 9 == 4)
                    {
                        // A red tarpaulin on four poles over the stall.
                        double th = g + 2.25, tl = g + 2.0;
                        if (lod == 0)
                        {
                            for (int q = 0; q < 4; q++)
                            {
                                double lu = u + (q % 2 == 0 ? -1.0 : 1.0), lw = w + (q < 2 ? -0.9 : 0.9);
                                SacredDraw.Box(m, x.K, lu - 0.025, lu + 0.025, g, q < 2 ? tl : th, lw - 0.025, lw + 0.025, wood, BoxFaces.Front | BoxFaces.Left);
                            }
                        }
                        SacredDraw.Quad(m, x.K, u - 1.05, tl, w - 0.95, u + 1.05, tl, w - 0.95, u + 1.05, th, w + 0.95, u - 1.05, th, w + 0.95, 0, 1, -0.12, KdsLooks.Tarp);
                        SacredDraw.Quad(m, x.K, u - 1.05, th, w + 0.95, u + 1.05, th, w + 0.95, u + 1.05, tl, w - 0.95, u - 1.05, tl, w - 0.95, 0, -1, 0.12,
                                        Looks.Ao(KdsLooks.Tarp, 0.6f));
                    }
                    else if (lod == 0 && (row * 5 + col) % 11 == 2)
                    {
                        // A patio umbrella.
                        ShapeBrush c = umb[(int)(r * 9) % 3];
                        Shapes.Cylinder(m, SacredDraw.At(x.K, u + 0.9, g, w), KdsLooks.MetalGrey, 0.025, 2.2, 4);
                        Shapes.Cone(m, SacredDraw.At(x.K, u + 0.9, g + 1.95, w), c, 1.1, 0.5, lod == 0 ? 8 : 6, 0, 0, true);
                    }
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
                        if (x.CorridorDistance(u, w) < 0.4 + SacredClearance.MarginM) continue;
                        double g = x.GroundV(u, w);
                        Shapes.Cylinder(m, SacredDraw.At(x.K, u, g, w), pole, 0.07, 5.6, 6);
                        Shapes.Bar(m, x.K, pole, u, g + 5.4, w, u + 0.6, g + 5.5, w, 0.03, 4);
                        SacredDraw.Box(m, x.K, u + 0.48, u + 0.72, g + 5.15, g + 5.45, w - 0.12, w + 0.12, Looks.Of(MeshColor.FromHex(0xF2E6B8), MaterialChannel.Glass),
                                       BoxFaces.All);
                        x.Cylinder(u, w, g, g + 5.6, 0.1);
                    }
                }
            }
        }
    }
}
