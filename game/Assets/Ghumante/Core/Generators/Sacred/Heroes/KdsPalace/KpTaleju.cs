using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsPalace
{
    /// <summary>
    /// Taleju (1564; kds-palace.md §3.10), in its own plan frame (u to bearing 103°, w to 13°, the plinth's OSM centre at
    /// the origin): the twelve-stage plinth (three broad low terraces to 2 m with grass in the joints, the ring of twelve
    /// two-roof mini shrines on the third at their OSM places, four more stages, the eighth a brick wall round the raised
    /// compound with four inner corner shrines, four upper stages to 6.8 m and the iron railing round the base storey),
    /// the temple with its three copper roofs at the real spans (33 / 25 / 18 m, eaves 12 / 19.5 / 26.5 m: standing seams,
    /// grey-green on top, dark struts, rows of bells, gilt only on the window panels, torana and finial; no valances) and
    /// the gilt finial to 35 m with pataka banners; the red-ochre west wall with the Singha Dhoka (black studded door in a
    /// carved torana, painted eyes and roundels, two white lions with red stripes) and the long shop row with red
    /// awnings along the north foot.
    /// </summary>
    internal static class KpTaleju
    {
        /// <summary>The plinth's top under the temple (above the square).</summary>
        public const double PlinthTopM = 6.8;

        /// <summary>The plinth's OSM centre (local metres) and the frame's x bearing.</summary>
        public const double CX = 45.1, CY = 44.4, Bearing = 103;

        /// <summary>The middle stages' centre (the shrine ring's centre, south of the plinth centre) and the temple's (OSM,
        /// nudged 0.3 m east to clear the inner shrines).</summary>
        public const double RingU = 0.5, RingW = -2.75, TU = 0.4, TW = 0.4;

        /// <summary>
        /// The twelve stages: centre u, centre w, half u, half w, top. Stages 1-7 are the broad grassy terraces with low
        /// brick risers (the first two at the OSM outlines w1414600808 and w420797418, the seventh the shelf the twelve outer
        /// shrines stand on); stage 8 is the compound wall (<see cref="Wall"/>, its top the compound floor); stages 9-12 are
        /// the brick upper plinth round the temple, the last with the iron railing.
        /// </summary>
        private static readonly double[] Stages =
        {
            0.10, 0.00, 23.85, 22.80, 0.45,
            0.15, -0.15, 22.05, 21.10, 0.80,
            0.22, -0.35, 21.75, 21.15, 1.10,
            0.28, -0.55, 21.45, 21.20, 1.40,
            0.34, -0.75, 21.15, 21.25, 1.65,
            0.40, -0.90, 20.90, 21.25, 1.85,
            RingU, -1.00, 20.70, 21.20, 2.00,
            RingU, RingW, WallHu, WallHw, CompoundFloor,
            TU, TW, 12.40, 12.00, 4.80,
            TU, TW, 11.80, 11.40, 5.45,
            TU, TW, 11.20, 10.80, 6.10,
            TU, TW, 10.60, 10.30, PlinthTopM,
        };

        /// <summary>The compound wall: half sizes round the ring centre, thickness, the half width of the south stair gap,
        /// the compound floor (the wall's top) and the coping on it.</summary>
        public const double WallHu = 16.35, WallHw = 15.85, WallT = 0.6, WallGap = 1.55, CompoundFloor = 4.1, WallTop = CompoundFloor + 0.28;

        /// <summary>The twelve outer two-roof shrines on the seventh stage (frame u, w; OSM w1414610434..445, regularised round
        /// the ring centre: corners at ±18.1 / ±17.65, the middle pair of each side ±5.3 from its centre).</summary>
        private static readonly double[] Outer =
        {
            RingU - 18.1, RingW - 17.65, RingU - 5.3, RingW - 17.65, RingU + 5.3, RingW - 17.65, RingU + 18.1, RingW - 17.65,
            RingU + 18.1, RingW - 5.3, RingU + 18.1, RingW + 5.3, RingU + 18.1, RingW + 17.65, RingU + 5.3, RingW + 17.65,
            RingU - 5.3, RingW + 17.65, RingU - 18.1, RingW + 17.65, RingU - 18.1, RingW + 5.3, RingU - 18.1, RingW - 5.3,
        };

        /// <summary>The four smaller shrines inside the wall at the compound's corners (OSM w1415152802..805).</summary>
        private static readonly double[] Inner =
        {
            RingU - 14.2, RingW - 12.4, RingU + 14.2, RingW - 12.4, RingU + 14.2, RingW + 12.9, RingU - 14.2, RingW + 12.9,
        };

        /// <summary>The temple's three storeys and roofs at the OSM spans: roof 1 33.2 × 32.4 m with its eave at 12 m rising to
        /// 19 m, roof 2 24.9 × 24.3 m from 19.5 to 25 m, roof 3 18.4 × 17.9 m from 26.5 to 31 m; the bodies 16.8, 14.6 and 9.7 m
        /// across (OSM w1414024864, w1413440908, w1413440906).</summary>
        private static readonly KpTier[] Temple =
        {
            // The ground body (OSM w1414024864, 19.8 × 19.2 m) under the very wide lowest roof: three gilt windows close
            // together in the middle of each face (photo taleju c_25), the long struts and the deep soffit above them.
            new KpTier
            {
                Base = PlinthTopM, WallU = 9.6, WallW = 9.35, Eave = 12.0, EaveU = 16.6, EaveW = 16.2, Top = 19.0, Walls = KpWalls.GiltPanels, Windows = 3, WinSill = 0.9,
                WinH = 2.8, WinSpread = 0.33,
            },
            new KpTier
            {
                Base = 19.0, WallU = 6.6, WallW = 6.4, Eave = 19.6, EaveU = 12.45, EaveW = 12.15, Top = 25.0, Walls = KpWalls.GiltPanels, Windows = 3, WinSill = 0.6,
                WinH = 1.9, WinSpread = 0.45,
            },
            new KpTier
            {
                Base = 25.0, WallU = 4.2, WallW = 4.1, Eave = 26.5, EaveU = 9.2, EaveW = 8.95, Top = 31.0, ApexU = 0.35, ApexW = 0.35, Walls = KpWalls.GiltPanels,
                Windows = 1, WinSill = 0.6, WinH = 1.6,
            },
        };

        /// <summary>The red-ochre precinct wall on the west (frame u of its outer face, its w extent, height) and the Singha
        /// Dhoka's place on it (local ≈ (17.5, 45), §3.10).</summary>
        public const double GateU = -26.6, WallW0 = -24.0, WallW1 = 21.0, GateW = -5.4, RedWallH = 3.9;

        /// <summary>The shop row on the north foot (OSM w1374620543, 55.8 × 7.2 m, 3 m).</summary>
        public const double ShopU0 = -32.0, ShopU1 = 22.8, ShopW0 = 21.3, ShopW1 = 28.5, ShopEave = 2.45, ShopRidge = 3.3;

        /// <summary>Local site metres of frame (u, w).</summary>
        public static void Local(double u, double w, out double x, out double y)
        {
            double r = Bearing * Math.PI / 180.0, s = Math.Sin(r), c = Math.Cos(r);
            double r2 = (Bearing - 90) * Math.PI / 180.0, s2 = Math.Sin(r2), c2 = Math.Cos(r2);
            x = CX + u * s + w * s2;
            y = CY + u * c + w * c2;
        }

        public static void Build(ref KpFrame f, MeshData m)
        {
            Affine3 k = f.Plan(CX, CY, 0, Bearing);
            if (f.Lod >= 3)
            {
                Lod3(ref f, m, k);
                return;
            }
            Plinth(ref f, m, k);
            Wall(ref f, m, k, Stages[6 * 5 + 4]);
            Shrines(ref f, m, k);
            // The south stair: from the square up the terraces to the seventh stage, then through the wall's gap to the
            // compound (the way up from Trishul Chowk).
            Steps(ref f, m, k, RingU, Stages[1] - Stages[3] - 0.9, RingW - WallHw - 2.6, 0, Stages[6 * 5 + 4], 1.6);
            Steps(ref f, m, k, RingU, RingW - WallHw - 2.6, RingW - WallHw + WallT + 0.1, Stages[6 * 5 + 4], CompoundFloor, WallGap);
            Steps(ref f, m, k, TU, TW - Stages[8 * 5 + 3] - 2.0, TW - Stages[11 * 5 + 3], CompoundFloor, PlinthTopM, 1.3);
            TempleBody(ref f, m, k);
            SinghaDhoka(ref f, m, k);
            ShopRow(ref f, m, k);
        }

        /// <summary>Stages 1-7 and 9-12 with their walkable colliders; grass tufts in the joints of the terraces.</summary>
        private static void Plinth(ref KpFrame f, MeshData m, in Affine3 k)
        {
            int lod = f.Lod;
            double below = 0.3;
            for (int i = 0; i < 12; i++)
            {
                if (i == 7) continue;
                double cu = Stages[5 * i], cw = Stages[5 * i + 1], hu = Stages[5 * i + 2], hw = Stages[5 * i + 3], top = Stages[5 * i + 4];
                double v0 = i == 0 ? -below : i == 8 ? CompoundFloor - 0.1 : Stages[5 * (i - 1) + 4] - 0.1;
                bool terrace = i < 7;
                if (lod >= 2 && terrace && i % 2 == 1 && i != 6) continue;
                KpStack.Stage(m, k, cu, cw, hu, hw, v0, top, Kp.Ao(Kp.Brick, 0.95f), terrace ? Kp.Grass : Kp.BrickPave, lod);
                if (terrace && lod == 0)
                {
                    // Moss and grass along the foot of the next riser.
                    if (i < 6)
                    {
                        double nu = Stages[5 * (i + 1) + 2], nw = Stages[5 * (i + 1) + 3], ncu = Stages[5 * (i + 1)], ncw = Stages[5 * (i + 1) + 1];
                        Kp.Box(m, k, ncu - nu - 0.18, ncu + nu + 0.18, top, top + 0.06, ncw - nw - 0.18, ncw - nw, Kp.Tint(Kp.Grass, 0.85f), BoxFaces.Top | BoxFaces.Front);
                        Kp.Box(m, k, ncu - nu - 0.18, ncu + nu + 0.18, top, top + 0.06, ncw + nw, ncw + nw + 0.18, Kp.Tint(Kp.Grass, 0.85f), BoxFaces.Top | BoxFaces.Back);
                    }
                }
                if (f.C != null)
                {
                    double x, y;
                    Local(cu, cw, out x, out y);
                    f.Box(x, y, hu, hw, Bearing, 0, top, GenColliderFlags.Walkable, terrace ? GenColliders.Grass : GenColliders.Brick);
                }
            }
            // The iron railing round the top stage (gap at the south stair).
            if (lod <= 1)
            {
                double cu = TU, cw = TW, hu = Stages[11 * 5 + 2] - 0.25, hw = Stages[11 * 5 + 3] - 0.25;
                for (int s = 0; s < 4; s++)
                {
                    Affine3 face = KpStack.Face(SacredDraw.At(k, cu, 0, cw), 90 * s, s % 2 == 0 ? hu : hw);
                    double half = s % 2 == 0 ? hw : hu;
                    if (s == 3)
                    {
                        Kp.Railing(m, face, -half, -1.5, 0, PlinthTopM, 1.0, lod);
                        Kp.Railing(m, face, 1.5, half, 0, PlinthTopM, 1.0, lod);
                    }
                    else Kp.Railing(m, face, -half, half, 0, PlinthTopM, 1.0, lod);
                }
            }
        }

        /// <summary>The compound wall (stage 8): four brick walls with a coping round the raised compound (its floor at
        /// <see cref="CompoundFloor"/>), a gap in the south wall where the stair comes up.</summary>
        private static void Wall(ref KpFrame f, MeshData m, in Affine3 k, double v0)
        {
            int lod = f.Lod;
            double u0 = RingU - WallHu, u1 = RingU + WallHu, w0 = RingW - WallHw, w1 = RingW + WallHw, t = WallT;
            Kp.Floor(m, k, u0 + t, u1 - t, w0 + t, w1 - t, CompoundFloor, Kp.BrickPave);
            WallPiece(ref f, m, k, u0, u1, w1 - t, w1, v0);
            WallPiece(ref f, m, k, u0, u0 + t, w0 + t, w1 - t, v0);
            WallPiece(ref f, m, k, u1 - t, u1, w0 + t, w1 - t, v0);
            WallPiece(ref f, m, k, u0, RingU - WallGap, w0, w0 + t, v0);
            WallPiece(ref f, m, k, RingU + WallGap, u1, w0, w0 + t, v0);
            // The carved doorway in the middle of the north wall (photo taleju c_25): a proud brick frame, a dark door, a
            // carved timber frame and crest.
            Affine3 nf = KpStack.Face(SacredDraw.At(k, RingU, 0, RingW), 90, WallHw);
            Kp.Box(m, nf, -1.7, 1.7, v0 - 0.1, WallTop + 0.75, 0, 0.45, Kp.Tint(Kp.Brick, 1.06f), BoxFaces.Wall | BoxFaces.Top);
            Affine3 nd = SacredDraw.At(nf, 0, 0, 0.45);
            double top = Kp.Doorway(m, nd, 0, v0 + 0.25, 0.95, 1.9, Kp.Wood, false, lod);
            Kp.Crest(m, nd, 0, top + 0.02, 0.02, 1.9, 1.0, Kp.Wood, lod);
            if (f.C == null) return;
            double x, y;
            Local(RingU, RingW, out x, out y);
            f.Box(x, y, WallHu - t, WallHw - t, Bearing, 0, CompoundFloor, GenColliderFlags.Walkable, GenColliders.Brick);
        }

        private static void WallPiece(ref KpFrame f, MeshData m, in Affine3 k, double u0, double u1, double w0, double w1, double v0)
        {
            Kp.Box(m, k, u0, u1, v0 - 0.1, WallTop - 0.18, w0, w1, Kp.Brick, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
            Kp.Box(m, k, u0 - 0.06, u1 + 0.06, WallTop - 0.18, WallTop, w0 - 0.06, w1 + 0.06, Kp.Step, BoxFaces.All & ~BoxFaces.Bottom);
            if (f.C == null) return;
            double x, y;
            Local(0.5 * (u0 + u1), 0.5 * (w0 + w1), out x, out y);
            f.Box(x, y, 0.5 * (u1 - u0), 0.5 * (w1 - w0), Bearing, 0, WallTop, GenColliderFlags.NoClimb, GenColliders.Brick);
        }

        /// <summary>A flight of brick steps on the south side, centred at frame u, from w0 (the foot, at v0) to w1 (the top,
        /// at v1), half width <paramref name="hw"/>, with its walkable ramp.</summary>
        private static void Steps(ref KpFrame f, MeshData m, in Affine3 k, double u, double w0, double w1, double v0, double v1, double hw)
        {
            int lod = f.Lod;
            int n = Math.Max(2, (int)Math.Ceiling((v1 - v0) / 0.22));
            double run = (w1 - w0) / n, rise = (v1 - v0) / n;
            for (int i = 0; i < n; i++)
            {
                double top = v0 + rise * (i + 1), a = w0 + run * i;
                if (lod >= 2 && i % 2 == 1) continue;
                Kp.Box(m, k, u - hw, u + hw, v0 - 0.1, top, a, w1, Kp.Ao(Kp.Step, 0.95f), BoxFaces.Back | BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
            }
            if (f.C == null) return;
            double x0, y0, x1, y1;
            Local(u, w0, out x0, out y0);
            Local(u, w1, out x1, out y1);
            f.Ramp(x0, y0, v0, x1, y1, v1, hw, GenColliders.Stone);
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Mini shrines
        // ---------------------------------------------------------------------------------------------------------------

        private static void Shrines(ref KpFrame f, MeshData m, in Affine3 k)
        {
            int lod = f.Lod;
            for (int i = 0; i < Outer.Length / 2; i++)
            {
                double x = Outer[2 * i], y = Outer[2 * i + 1];
                Mini(ref f, m, k, x, y, 2.0, 1.0, lod);
            }
            for (int i = 0; i < Inner.Length / 2; i++) Mini(ref f, m, k, Inner[2 * i], Inner[2 * i + 1], CompoundFloor, 0.75, lod);
        }

        /// <summary>A two-roof mini shrine at frame (u, w) on level v: a two-step brick pedestal, a brick shaft with a dark
        /// niche facing out, two small mossy tile roofs and a gilt finial; s scales it (1 = the 3.2 m outer shrines).</summary>
        private static void Mini(ref KpFrame f, MeshData m, in Affine3 k, double u, double w, double v, double s, int lod)
        {
            Affine3 at = SacredDraw.At(k, u, 0, w);
            if (lod >= 2)
            {
                Kp.Box(m, at, -1.3 * s, 1.3 * s, v, v + 1.0 * s, -1.3 * s, 1.3 * s, Kp.Brick, BoxFaces.All & ~BoxFaces.Bottom);
                Kp.Box(m, at, -0.7 * s, 0.7 * s, v + 1.0 * s, v + 2.2 * s, -0.7 * s, 0.7 * s, Kp.Brick, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
                KpLod3.Pyramid(m, at, 0, 0, 1.25 * s, 1.25 * s, v + 2.2 * s, v + 3.9 * s, Kp.TileMoss);
                return;
            }
            KpStack.Stage(m, at, 0, 0, 1.45 * s, 1.45 * s, v, v + 0.5 * s, Kp.Tint(Kp.Brick, 1.08f), Kp.BrickPave, lod);
            KpStack.Stage(m, at, 0, 0, 1.15 * s, 1.15 * s, v + 0.5 * s, v + 1.0 * s, Kp.Brick, Kp.BrickPave, lod);
            var t = new KpTier[2];
            t[0] = new KpTier { Base = v + 1.0 * s, WallU = 0.72 * s, WallW = 0.72 * s, Eave = v + 2.2 * s, EaveU = 1.3 * s, EaveW = 1.3 * s, Top = v + 2.75 * s, Walls = KpWalls.Plain };
            t[1] = new KpTier { Base = v + 2.75 * s, WallU = 0.5 * s, WallW = 0.5 * s, Eave = v + 3.15 * s, EaveU = 0.95 * s, EaveW = 0.95 * s, Top = v + 3.75 * s, ApexU = 0.1, ApexW = 0.1, Walls = KpWalls.Plain };
            KpRoofLook look = KpRoofLook.TilePlain(Kp.TileMoss);
            look.Struts = false;
            KpStack.Build(m, at, t, 2, Kp.Brick, look, 0u, Math.Max(1, lod));
            // The niche faces away from the temple.
            double a = Math.Atan2(w - RingW, u - RingU) * 180 / Math.PI;
            double snap = Math.Round(a / 90.0) * 90.0;
            Affine3 fc = KpStack.Face(at, snap, 0.72 * s);
            Kp.Panel(m, fc, -0.28 * s, v + 1.15 * s, 0.28 * s, v + 1.95 * s, 0.01, Kp.Recess);
            if (lod == 0) ShikharaGenerator.Arch(m, fc, 0, v + 1.75 * s, 0.01, 0.56 * s, 0.2 * s, 0.05, 0.04, true, Kp.Wood, 1);
            Kp.Finial(m, at, 0, v + 3.7 * s, 0, 0.5 * s, 0.1 * s, Math.Max(2, lod));
            if (f.C == null) return;
            double x, y;
            Local(u, w, out x, out y);
            f.Box(x, y, 1.45 * s, 1.45 * s, Bearing, v, v + 3.9 * s, GenColliderFlags.NoClimb, GenColliders.Brick);
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Temple
        // ---------------------------------------------------------------------------------------------------------------

        private static void TempleBody(ref KpFrame f, MeshData m, in Affine3 k)
        {
            int lod = f.Lod;
            Affine3 t = SacredDraw.At(k, TU, 0, TW);
            KpRoofLook look = KpRoofLook.CopperBells(Kp.Copper);
            look.Fascia = Kp.Wood;
            look.BellSpacing = 1.1;
            look.StrutPitch = 1.3;
            look.Horns = false;
            // The copper slopes are concave: shallow at the eave (from the street the lowest roof shows only its edge over
            // the deep soffit, photo taleju c_02), steep near the storey above.
            look.Sag = 0.075;
            KpStack.Build(m, t, Temple, 3, Kp.Brick, look, 1301u, lod);
            // The deep soffits of the two lower roofs: dense dark rafters (the lattice ceiling seen from the street).
            for (int i = 0; i < 2; i++)
            {
                KpTier ti = Temple[i];
                double tu = Temple[i + 1].WallU, tw = Temple[i + 1].WallW;
                Kp.SoffitRafters(m, t, ti.EaveU, ti.EaveW, ti.Eave, tu, tw, ti.Top, ti.WallU, ti.WallW, i == 0 ? 0.32 : 0.45, lod);
            }
            BodyDress(m, t, lod);
            Kp.Finial(m, t, 0, 30.95, 0, 4.05, 0.62, lod, true);
            // The pataka banners hanging from the top down the west and east faces.
            if (lod == 0)
                for (int s = 0; s < 4; s += 2)
                {
                    Affine3 fc = KpStack.Face(t, 90 * s + 180, 0);
                    double[] pu = { 0, 0, 0, 0 }, pv = { 30.8, 29.0, 25.9, 24.2 }, pw = { 0.6, 4.6, 7.9, 9.8 };
                    SacredParts.Pataka(m, fc, pu, pv, pw, 4, 0.42, lod);
                }
            if (f.C != null)
            {
                double x, y;
                Local(TU, TW, out x, out y);
                f.Box(x, y, Temple[0].WallU, Temple[0].WallW, Bearing, PlinthTopM, 31.0, GenColliderFlags.NoClimb, GenColliders.Brick);
            }
        }

        /// <summary>
        /// The ground body above its gilt windows (photos taleju c_02, c_25, c_26): a carved timber frieze of small panels
        /// over the windows and their torana, and the dark timber zone under the deep soffit where the long struts stand
        /// (from the street that whole band reads dark, not as bare brick).
        /// </summary>
        private static void BodyDress(MeshData m, in Affine3 t, int lod)
        {
            KpTier ti = Temple[0];
            double frac = (ti.EaveU - ti.WallU) / Math.Max(1e-6, ti.EaveU - Temple[1].WallU);
            double wallTop = ti.Eave + frac * (ti.Top - ti.Eave) - 0.15;
            // The frieze clears the gilt torana over the middle window (its top about 1.15 m over the window).
            double f0 = ti.Base + ti.WinSill + ti.WinH + 1.3, dark = f0 + 0.62;
            for (int s = 0; s < 4; s++)
            {
                double half = s % 2 == 0 ? ti.WallU : ti.WallW, depth = s % 2 == 0 ? ti.WallW : ti.WallU;
                Affine3 face = SacredDraw.At(SacredDraw.Turn(t, 0, 0, 0, s * 0.5 * Math.PI), 0, 0, depth);
                // Either side of the gilt windows a dark carved window, and a small one near each corner (c_25).
                for (int e = -1; e <= 1; e += 2)
                {
                    Kp.FramedWindow(m, face, e * 0.56 * half, ti.Base + ti.WinSill + 0.55, 0.9, 1.35, lod <= 1, lod);
                    Kp.Lattice(m, face, e * 0.84 * half, ti.Base + ti.WinSill + 1.2, 0.6, 0.75, lod, 2);
                }
                // The carved frieze over the windows.
                Kp.Band(m, face, -half, half, f0, f0 + 0.32, 0.15, Kp.Wood);
                Kp.Band(m, face, -half, half, f0 + 0.32, f0 + 0.46, 0.09, Kp.WoodMid);
                if (lod == 0)
                {
                    int n = Math.Max(4, (int)Math.Round(2 * half / 0.95));
                    for (int i = 0; i < n; i++)
                    {
                        double u = -half + 2 * half * (i + 0.5) / n;
                        Kp.Box(m, face, u - 0.24, u + 0.24, f0 + 0.05, f0 + 0.27, 0.15, 0.2, Kp.WoodMid, BoxFaces.Front | BoxFaces.Top);
                    }
                }
                // The dark timber zone behind the struts up to the soffit, with its beam.
                Kp.Band(m, face, -half, half, dark - 0.18, dark, 0.12, Kp.WoodBeam);
                Kp.Panel(m, face, -half, dark, half, wallTop, 0.015, Kp.Ao(Kp.WoodBeam, 0.75f));
            }
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Singha Dhoka and the red west wall
        // ---------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// The precinct's red-ochre west wall with its white stepped crest, and the Singha Dhoka set in a taller gate block:
        /// a black studded door in a dark carved torana arch, painted eyes and roundels either side, a small two-tier roof
        /// above, and two white lions with red stripes on pedestals facing the Kot side (283°).
        /// </summary>
        private static void SinghaDhoka(ref KpFrame f, MeshData m, in Affine3 k)
        {
            int lod = f.Lod;
            // The wall's outer (west) face frame: w out to 283°, u along it (viewer's right = north-ish).
            Affine3 wall = KpStack.Face(SacredDraw.At(k, GateU, 0, 0.5 * (WallW0 + WallW1)), 180, 0);
            double half = 0.5 * (WallW1 - WallW0), gu = GateW - 0.5 * (WallW0 + WallW1);
            Kp.Box(m, wall, -half, half, -0.2, RedWallH, -0.6, 0, Kp.RedOchre, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
            Crest(m, wall, -half, half, RedWallH, 0.0, lod);
            // The gate block, a little proud and taller, with its roof.
            const double gw = 3.6, gh = 8.2, proud = 0.6;
            Kp.Box(m, wall, gu - gw, gu + gw, -0.2, gh, -1.2, proud, Kp.RedOchre, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
            Crest(m, wall, gu - gw, gu + gw, gh, proud, lod);
            // A teal band under the cornice, then the white stepped crest rising in three steps to a small gilt kalash.
            Kp.Box(m, wall, gu - gw, gu + gw, gh - 0.75, gh - 0.55, proud, proud + 0.05, Kp.Green, BoxFaces.Front | BoxFaces.Bottom);
            for (int i = 0; i < 3; i++)
            {
                double hw2 = gw - 0.15 - 0.75 * (i + 1), v0 = gh + 0.6 + 0.42 * i;
                Kp.Box(m, wall, gu - hw2, gu + hw2, v0, v0 + 0.42, -1.0 + 0.1 * i, proud - 0.1 - 0.12 * i, Kp.WhiteTrim, BoxFaces.All & ~BoxFaces.Bottom);
            }
            Affine3 top = SacredDraw.At(wall, gu, 0, -0.2);
            Kp.Finial(m, top, 0, gh + 0.6 + 1.26, 0, 0.7, 0.16, lod);
            // The door: black studded leaves in a dark carved torana arch.
            Affine3 face = SacredDraw.At(wall, 0, 0, proud);
            double dw = 1.7, dh = 3.0;
            Kp.Box(m, face, gu - 0.5 * dw, gu + 0.5 * dw, 0.2, 0.2 + dh, -0.02, 0.02, Kp.Iron, BoxFaces.Front);
            if (lod == 0)
                for (int r = 0; r < 6; r++)
                    for (int c = 0; c < 4; c++)
                        Shapes.Sphere(m, SacredDraw.At(face, gu - 0.6 + 0.4 * c, 0.6 + 0.45 * r, 0.03), Kp.GiltDeep, 0.035, 4);
            Kp.Box(m, face, gu - 0.5 * dw - 0.35, gu - 0.5 * dw, 0.2, 0.2 + dh, 0, 0.2, Kp.Wood, BoxFaces.Wall);
            Kp.Box(m, face, gu + 0.5 * dw, gu + 0.5 * dw + 0.35, 0.2, 0.2 + dh, 0, 0.2, Kp.Wood, BoxFaces.Wall);
            ShikharaGenerator.Arch(m, face, gu, 0.2 + dh, 0.012, dw + 0.7, 0.9, 0.35, 0.22, true, Kp.Wood, lod);
            if (lod <= 1)
            {
                // The painted eyes and roundels on the red either side.
                for (int s = -1; s <= 1; s += 2)
                {
                    double uc = gu + s * (0.5 * dw + 0.75);
                    Shapes.Cylinder(m, SacredDraw.Basis(face, uc, 2.3, 0.0, 1, 0, 0, 0, 0, 1), Kp.WhiteTrim, 0.32, 0.03, 10);
                    Shapes.Cylinder(m, SacredDraw.Basis(face, uc, 2.3, 0.03, 1, 0, 0, 0, 0, 1), Kp.GateBlue, 0.2, 0.02, 10);
                    for (int e = -1; e <= 1; e += 2)
                    {
                        Shapes.Ellipsoid(m, SacredDraw.At(face, uc + e * 0.16, 3.3, 0.01), Kp.WhiteTrim, 0.12, 0.06, 0.02, 6, false);
                        Shapes.Sphere(m, SacredDraw.At(face, uc + e * 0.16, 3.3, 0.025), Kp.BlackStone, 0.04, 4);
                    }
                    Shapes.Cylinder(m, SacredDraw.Basis(face, uc, 4.4, 0.0, 1, 0, 0, 0, 0, 1), Kp.Gilt, 0.26, 0.03, 10);
                }
            }
            // The white lions with red stripes.
            for (int s = -1; s <= 1; s += 2)
                Kp.Lion(m, face, gu + s * (0.5 * dw + 1.15), 0, 0.9, 0.75, 1.35, true, s, lod);
            if (f.C == null) return;
            double x0, y0, x1, y1;
            Local(GateU + 0.3, WallW0, out x0, out y0);
            Local(GateU + 0.3, WallW1, out x1, out y1);
            f.Wall(x0, y0, x1, y1, 0, RedWallH, 0.35);
            double gx, gy;
            Local(GateU + 0.3, GateW, out gx, out gy);
            f.Box(gx, gy, 0.9, gw, Bearing, 0, gh + 1.9, GenColliderFlags.NoClimb, GenColliders.Brick);
            for (int s = -1; s <= 1; s += 2)
            {
                double lx, ly;
                Local(GateU - proud - 0.9, GateW + s * (0.5 * dw + 1.15), out lx, out ly);
                f.Cylinder(lx, ly, 0, 2.2, 0.55);
            }
        }

        /// <summary>The white stepped crest on a red wall from u0 to u1 at height v (the wall face at w).</summary>
        private static void Crest(MeshData m, in Affine3 k, double u0, double u1, double v, double w, int lod)
        {
            Kp.Box(m, k, u0 - 0.1, u1 + 0.1, v, v + 0.25, w - 0.75, w + 0.12, Kp.WhiteTrim, BoxFaces.All & ~BoxFaces.Bottom);
            Kp.Box(m, k, u0, u1, v + 0.25, v + 0.45, w - 0.7, w + 0.04, Kp.WhiteTrim, BoxFaces.All & ~BoxFaces.Bottom);
            if (lod >= 2) return;
            Kp.Box(m, k, u0 + 0.1, u1 - 0.1, v + 0.45, v + 0.6, w - 0.65, w - 0.05, Kp.WhiteTrim, BoxFaces.All & ~BoxFaces.Bottom);
            Kp.Box(m, k, u0, u1, v - 0.35, v - 0.25, w, w + 0.06, Kp.WhiteTrim, BoxFaces.Front | BoxFaces.Bottom);
        }

        // ---------------------------------------------------------------------------------------------------------------
        // The north shop row
        // ---------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// The long single-storey shop row on the plinth's north foot (OSM w1374620543; photos taleju c_02, c_25): a brick
        /// back wall against the plinth and a lean-to roof of terracotta tiles sloping down to the street, its fascia over a
        /// row of open shopfronts between timber posts, the goods (cloth, shoes, pots) hanging in the openings, facing
        /// Makhan Tole.
        /// </summary>
        private static void ShopRow(ref KpFrame f, MeshData m, in Affine3 k)
        {
            int lod = f.Lod;
            double cu = 0.5 * (ShopU0 + ShopU1), cw = 0.5 * (ShopW0 + ShopW1), hu = 0.5 * (ShopU1 - ShopU0), hw = 0.5 * (ShopW1 - ShopW0);
            Affine3 at = SacredDraw.At(k, cu, 0, cw);
            ShapeBrush tile = B2(0xA4593F, MaterialChannel.RoofTile);
            // The block under the roof.
            Kp.Box(m, at, -hu, hu, -0.2, ShopEave, -hw, hw, Kp.Brick, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
            // The lean-to: the back edge against the plinth at the ridge height, the eave 0.9 m out over the street.
            const double over = 0.9;
            double wb = -hw, wf = hw + over, vb = ShopRidge, vf = ShopEave;
            SacredDraw.Quad(m, at, -hu - 0.3, vf, wf, hu + 0.3, vf, wf, hu + 0.3, vb, wb, -hu - 0.3, vb, wb, 0, wf - wb, vb - vf, tile);
            // The gable ends and the fascia board.
            for (int e = -1; e <= 1; e += 2)
            {
                double ue = e * (hu + 0.01);
                SacredDraw.Quad(m, at, ue, -0.2, -hw, ue, -0.2, hw, ue, vf + (vb - vf) * over / (wf - wb), hw, ue, vb, wb, e, 0, 0, Kp.Brick);
            }
            Kp.Box(m, at, -hu - 0.3, hu + 0.3, vf - 0.22, vf, wf - 0.06, wf, Kp.Wood, BoxFaces.Front | BoxFaces.Bottom | BoxFaces.Left | BoxFaces.Right);
            SacredDraw.Quad(m, at, -hu - 0.3, vf - 0.22, wf - 0.06, hu + 0.3, vf - 0.22, wf - 0.06, hu + 0.3, vf + 0.1, hw, -hu - 0.3, vf + 0.1, hw, 0, -1, 0,
                            Kp.Ao(Kp.Wood, 0.55f));
            if (lod == 0)
            {
                // Tile courses: low ridges across the slope.
                int courses = 9;
                for (int i = 1; i < courses; i++)
                {
                    double tt = (double)i / courses, w = wf + (wb - wf) * tt, v = vf + (vb - vf) * tt;
                    Kp.Box(m, at, -hu - 0.3, hu + 0.3, v - 0.01, v + 0.05, w - 0.05, w + 0.05, Kp.Tint(tile, 0.8f), BoxFaces.Top | BoxFaces.Front);
                }
            }
            Affine3 front = KpStack.Face(at, 90, hw);
            int shops = 22;
            double bay = 2 * hu / shops;
            for (int i = 0; i < shops; i++)
            {
                double u = -hu + bay * (i + 0.5);
                Kp.Panel(m, front, u - 0.5 * bay + 0.12, 0.05, u + 0.5 * bay - 0.12, ShopEave - 0.35, 0.01, Kp.Ao(Kp.Recess, 0.9f));
                if (lod >= 2) continue;
                // Goods in the opening (taleju c_02, c_25): shawls, cloth and pictures hanging in narrow strips of mixed
                // colours with the dark shop showing between them, over a low counter.
                int strips = lod == 0 ? 4 : 2;
                double sw = (bay - 0.5) / strips;
                for (int q = 0; q < strips; q++)
                {
                    uint hq = ShapeNoise.Hash(i, q, 3, 977u);
                    if (hq % 5 == 0) continue;
                    ShapeBrush goods = GoodsColour(hq);
                    double u0 = u - 0.5 * bay + 0.25 + sw * q + 0.04, drop = 0.9 + 0.5 * ((hq >> 8) % 3) / 2.0;
                    Kp.Panel(m, front, u0, drop, u0 + sw - 0.08, ShopEave - 0.45, 0.06 + 0.005 * q, goods);
                }
                if (lod == 0) Kp.Box(m, front, u - 0.5 * bay + 0.2, u + 0.5 * bay - 0.2, 0, 0.75, 0.0, 0.55, Kp.WoodMid, BoxFaces.Front | BoxFaces.Top);
            }
            for (int i = 0; i <= shops; i++)
                Kp.Box(m, front, -hu + bay * i - 0.09, -hu + bay * i + 0.09, 0, ShopEave - 0.2, 0, 0.12, Kp.Wood, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
            Kp.Band(m, front, -hu, hu, ShopEave - 0.45, ShopEave - 0.2, 0.16, Kp.WoodBeam);
            if (f.C == null) return;
            // The solid stops 1.3 m inside the open shopfronts: Makhan Tole's drawn carriageway runs up to the front.
            const double FrontGap = 1.3;
            double x, y;
            Local(cu, cw - 0.5 * FrontGap, out x, out y);
            f.Box(x, y, hu, hw - 0.5 * FrontGap, Bearing, 0, ShopRidge, GenColliderFlags.NoClimb, GenColliders.Brick);
        }

        /// <summary>A muted mixed colour for hanging goods (cloth, shawls, pictures), never a flat block of one colour.</summary>
        private static ShapeBrush GoodsColour(uint h)
        {
            switch (h % 7)
            {
                case 0: return B2(0xB8473A, MaterialChannel.Fabric);
                case 1: return B2(0xD8B25A, MaterialChannel.Fabric);
                case 2: return B2(0x3E6E8E, MaterialChannel.Fabric);
                case 3: return B2(0x7A3F6A, MaterialChannel.Fabric);
                case 4: return B2(0xC9C2B2, MaterialChannel.Fabric);
                case 5: return B2(0x5F7F4A, MaterialChannel.Fabric);
                default: return B2(0xD97A3A, MaterialChannel.Fabric);
            }
        }

        private static ShapeBrush B2(uint rgb, MaterialChannel ch)
        {
            return new ShapeBrush(MeshColor.FromHex(rgb), ch, 1f);
        }

        private static void Lod3(ref KpFrame f, MeshData m, in Affine3 k)
        {
            Kp.Box(m, k, -23.85, 23.85, 0, 2.0, -22.65, 22.65, Kp.Grass, BoxFaces.All & ~BoxFaces.Bottom);
            Kp.Box(m, SacredDraw.At(k, RingU, 0, RingW), -WallHu, WallHu, 2.0, WallTop, -WallHw, WallHw, Kp.Brick, BoxFaces.All & ~BoxFaces.Bottom);
            Affine3 t = SacredDraw.At(k, TU, 0, TW);
            KpTowers.Lod3(m, t, Temple, 3, Kp.Brick, Kp.Copper);
            Kp.Box(m, SacredDraw.At(k, GateU, 0, 0.5 * (WallW0 + WallW1)), -0.6, 0, 0, RedWallH, -0.5 * (WallW1 - WallW0), 0.5 * (WallW1 - WallW0), Kp.RedOchre,
                   BoxFaces.All & ~BoxFaces.Bottom);
            Kp.Box(m, SacredDraw.At(k, 0.5 * (ShopU0 + ShopU1), 0, 0.5 * (ShopW0 + ShopW1)), -0.5 * (ShopU1 - ShopU0), 0.5 * (ShopU1 - ShopU0), 0, ShopRidge,
                   -0.5 * (ShopW1 - ShopW0), 0.5 * (ShopW1 - ShopW0), Kp.GaddiRoof, BoxFaces.All & ~BoxFaces.Bottom);
        }
    }
}
