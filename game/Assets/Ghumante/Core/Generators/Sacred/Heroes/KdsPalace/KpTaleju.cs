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

        /// <summary>The stages: centre u, centre w, half u, half w, top. Stage 8 is the compound wall (see <see cref="Wall"/>).</summary>
        private static readonly double[] Stages =
        {
            0.0, 0.0, 23.85, 22.65, 0.5,
            0.2, -0.4, 22.9, 22.1, 1.0,
            0.4, -1.0, 21.6, 21.2, 2.0,
            RingU, RingW, 16.1, 15.8, 2.5,
            RingU, RingW, 15.6, 15.3, 3.0,
            RingU, RingW, 15.1, 14.8, 3.5,
            RingU, RingW, 14.6, 14.3, 4.0,
            RingU, RingW, 14.2, 13.9, 4.4,
            TU, TW, 11.0, 10.6, 5.0,
            TU, TW, 10.6, 10.2, 5.6,
            TU, TW, 10.3, 9.9, 6.2,
            TU, TW, 10.0, 9.6, PlinthTopM,
        };

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
                Kp.Box(m, at, -1.25 * s, 1.25 * s, v, v + 1.0 * s, -1.2 * s, 1.2 * s, Kp.Brick, BoxFaces.All & ~BoxFaces.Bottom);
                Kp.Box(m, at, -0.7 * s, 0.7 * s, v + 1.0 * s, v + 2.2 * s, -0.7 * s, 0.7 * s, Kp.Brick, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
                KpLod3.Pyramid(m, at, 0, 0, 1.25 * s, 1.25 * s, v + 2.2 * s, v + 3.9 * s, Kp.TileMoss);
                return;
            }
            KpStack.Stage(m, at, 0, 0, 1.6 * s, 1.5 * s, v, v + 0.5 * s, SacredParts.PlinthBrick, Kp.BrickPave, lod);
            KpStack.Stage(m, at, 0, 0, 1.2 * s, 1.15 * s, v + 0.5 * s, v + 1.0 * s, Kp.Brick, Kp.BrickPave, lod);
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
            Kp.Finial(m, at, 0, v + 3.7 * s, 0, 0.5 * s, 0.1 * s, lod);
            if (f.C == null) return;
            double x, y;
            Local(u, w, out x, out y);
            f.Box(x, y, 1.6 * s, 1.5 * s, Bearing, v, v + 3.9 * s, GenColliderFlags.NoClimb, GenColliders.Brick);
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
            look.StrutPitch = 1.15;
            KpStack.Build(m, t, Temple, 3, Kp.Brick, look, 1301u, lod);
            Kp.Finial(m, t, 0, 30.95, 0, 4.05, 0.62, lod, true);
            // The west door with its gilt torana, and the pataka banners hanging from the top.
            Affine3 door = KpStack.Face(t, 180, Temple[0].WallU);
            Kp.Doorway(m, door, 0, PlinthTopM + 0.1, 1.5, 2.6, Kp.Gilt, true, lod);
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
            Affine3 roofAt = SacredDraw.At(wall, gu, 0, -0.3);
            var t = new KpTier[2];
            t[0] = new KpTier { Base = gh + 0.5, WallU = 1.7, WallW = 1.1, Eave = gh + 1.6, EaveU = 2.7, EaveW = 2.0, Top = gh + 2.2, Walls = KpWalls.GiltPanels, Windows = 1 };
            t[1] = new KpTier { Base = gh + 2.2, WallU = 1.2, WallW = 0.8, Eave = gh + 2.9, EaveU = 2.0, EaveW = 1.5, Top = gh + 3.6, ApexU = 0.5, ApexW = 0.12, Walls = KpWalls.Plain };
            KpStack.Build(m, roofAt, t, 2, Kp.RedOchre, KpRoofLook.CopperBells(Kp.CopperDark), 1311u, Math.Max(1, lod));
            Kp.Finial(m, roofAt, 0, gh + 3.55, 0, 0.6, 0.14, lod);
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
            ShikharaGenerator.Arch(m, face, gu, 0.2 + dh, 0.0, dw + 0.7, 0.9, 0.35, 0.22, true, Kp.Wood, lod);
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
            f.Box(gx, gy, 0.9, gw, Bearing, 0, gh + 3.6, GenColliderFlags.NoClimb, GenColliders.Brick);
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

        /// <summary>The long single-storey shop row on the plinth's north foot: a red-brown gabled tile roof over a row of open
        /// shopfronts with red cloth awnings, facing Makhan Tole.</summary>
        private static void ShopRow(ref KpFrame f, MeshData m, in Affine3 k)
        {
            int lod = f.Lod;
            double cu = 0.5 * (ShopU0 + ShopU1), cw = 0.5 * (ShopW0 + ShopW1), hu = 0.5 * (ShopU1 - ShopU0), hw = 0.5 * (ShopW1 - ShopW0);
            Affine3 at = SacredDraw.At(k, cu, 0, cw);
            Kp.Box(m, at, -hu, hu, -0.2, ShopEave + 0.15, -hw, hw, Kp.Brick, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
            Kp.Ridge(m, at, hu, hw, ShopEave, ShopRidge, 0.9, Kp.GaddiRoof, false, lod);
            Affine3 front = KpStack.Face(at, 90, hw);
            int shops = 18;
            double bay = 2 * hu / shops;
            for (int i = 0; i < shops; i++)
            {
                double u = -hu + bay * (i + 0.5);
                Kp.Panel(m, front, u - 0.5 * bay + 0.15, 0.1, u + 0.5 * bay - 0.15, ShopEave - 0.3, 0.01, Kp.Ao(Kp.Recess, 0.9f));
                if (lod >= 2) continue;
                // A red cloth awning sloping out over the shopfront.
                SacredDraw.Quad(m, front, u - 0.5 * bay + 0.1, ShopEave - 0.2, 0.05, u + 0.5 * bay - 0.1, ShopEave - 0.2, 0.05, u + 0.5 * bay - 0.1, ShopEave - 0.75, 1.4,
                                u - 0.5 * bay + 0.1, ShopEave - 0.75, 1.4, 0, 0.93, 0.37, i % 3 == 2 ? Kp.Valance : Kp.Cloak);
                if (lod == 0) Kp.Box(m, front, u - 0.5 * bay + 0.2, u + 0.5 * bay - 0.2, 0, 0.8, 0.0, 0.6, i % 2 == 0 ? Kp.Cream : Kp.Leaves, BoxFaces.Front | BoxFaces.Top);
            }
            for (int i = 0; i <= shops; i++)
                Kp.Box(m, front, -hu + bay * i - 0.09, -hu + bay * i + 0.09, 0, ShopEave, 0, 0.12, Kp.Wood, BoxFaces.Front | BoxFaces.Left | BoxFaces.Right);
            if (f.C == null) return;
            double x, y;
            Local(cu, cw, out x, out y);
            f.Box(x, y, hu, hw, Bearing, 0, ShopRidge, GenColliderFlags.NoClimb, GenColliders.Brick);
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
