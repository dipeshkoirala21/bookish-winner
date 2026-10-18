using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Sacred.Heroes.KdsPalace
{
    /// <summary>
    /// The Hanuman Dhoka gate group (kds-palace.md §3.1, §3.2, §3.3b, §3.5): the white west front of the gate block on the
    /// forecourt (palace grid a −11.6..−3.5, b −9.6..6.0; OSM w1374620542) with its blind cusped arches, red dado, dark
    /// windows and the continuous black timber gallery under a low tile roof; the gate set flush in that wall at
    /// (−12.4, −2.4) facing 282° (stone steps, gilt jambs and lintel, blue painted pilasters with eyes, the three-niche
    /// panel, the carved wooden canopy and struts, two painted lions with riders); Hanuman (1672) on his red pillar under
    /// the red-fringed parasol 3.5 m north of the door; the Narasimha niche just inside the passage; the Pratap Malla
    /// inscription slab; and Agamchhen's three red-valanced roofs rising from the block (built in its traditional form,
    /// as restored).
    /// </summary>
    internal static class KpGate
    {
        /// <summary>The gate block in palace-grid metres.</summary>
        public const double A0 = -11.6, A1 = -3.5, B0 = -9.6, B1 = 6.0;

        /// <summary>Storey levels of the white front: first floor, gallery floor, eave and ridge.</summary>
        public const double Floor1 = 3.7, GalleryFloor = 7.4, EaveV = 11.0, RidgeV = 13.4;

        /// <summary>The door's centre on the west face (palace grid b) and Hanuman's (b and his distance out of the wall).</summary>
        public const double DoorB = -4.9, HanumanB = -1.4, HanumanOut = 1.3;

        /// <summary>The door opening: its sill above the square, clear width and height (m).</summary>
        public const double DoorSill = 0.45, DoorW = 1.45, DoorH = 2.6;

        /// <summary>Agamchhen's centre on the block (palace grid) and its finial top.</summary>
        public const double AgamA = -7.0, AgamB = 0.6, AgamTopM = 23.0;

        public static void Build(ref KpFrame f, MeshData m)
        {
            int lod = f.Lod;
            if (lod >= 3)
            {
                Lod3(ref f, m);
                return;
            }
            double half;
            Affine3 west = f.GridSide(A0, A1, B0, B1, 0, 3, out half);
            double bm = 0.5 * (B0 + B1);
            double uDoor = -(DoorB - bm), uHan = -(HanumanB - bm);
            // The block: walls (the west front dressed below), the roof, the gallery.
            Affine3 g = f.Grid;
            if (lod >= 2) Kp.Box(m, g, A0, A1, -0.2, EaveV - 0.3, B0, B1, Kp.White, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
            else
            {
                // The west face is left open at the door so the passage (and Narasimha) behind it shows.
                Kp.Box(m, g, A0, A1, -0.2, EaveV - 0.3, B0, B1, Kp.White, BoxFaces.Front | BoxFaces.Back | BoxFaces.Right);
                double dl = uDoor - 0.5 * DoorW, dr = uDoor + 0.5 * DoorW, top = EaveV - 0.3;
                Kp.Panel(m, west, -half, -0.2, dl, top, 0, Kp.White);
                Kp.Panel(m, west, dr, -0.2, half, top, 0, Kp.White);
                Kp.Panel(m, west, dl, DoorSill + DoorH, dr, top, 0, Kp.White);
                Kp.Panel(m, west, dl, -0.2, dr, DoorSill, 0, Kp.White);
            }
            Kp.Ridge(m, SacredDraw.At(g, 0.5 * (A0 + A1), 0, bm), 0.5 * (A1 - A0), 0.5 * (B1 - B0), EaveV, RidgeV, 1.05, Kp.TileMoss, false, lod);
            WestFront(ref f, m, west, half, uDoor, uHan);
            Gate(ref f, m, west, uDoor);
            Hanuman(ref f, m, west, uHan);
            Agamchhen(ref f, m);
            Forecourt(ref f, m);
            Colliders(ref f, uDoor, uHan);
        }

        /// <summary>
        /// The forecourt's floor (hanuman_gate c_07, c_14): grey stone flags from wall to wall over the forecourt in front of
        /// the gate and the white Dakha fronts (<see cref="KpGround.ForecourtA0"/>.. in palace-grid metres), a hair over the
        /// level, in cells of 3 × 3 flags with a slight variation of tone (the material draws the 0.6 × 0.4 m flags and their
        /// joints), clipped round every building of the tile (the sibling temples' plinths) and the Dakha platform's steps;
        /// walkable stone. LOD1 coarser cells, LOD2 a few big ones.
        /// </summary>
        private static void Forecourt(ref KpFrame f, MeshData m)
        {
            int lod = f.Lod;
            double ca = lod == 0 ? 1.8 : lod == 1 ? 3.6 : 7.2, cb = lod == 0 ? 1.2 : lod == 1 ? 2.4 : 4.8;
            int na = (int)Math.Ceiling((KpGround.ForecourtA1 - KpGround.ForecourtA0) / ca), nb = (int)Math.Ceiling((KpGround.ForecourtB1 - KpGround.ForecourtB0) / cb);
            ca = (KpGround.ForecourtA1 - KpGround.ForecourtA0) / na;
            cb = (KpGround.ForecourtB1 - KpGround.ForecourtB0) / nb;
            // The tile's building rings near the forecourt (centimetres, tile-local), for the clip.
            TileData t = f.Tile;
            double minX = double.MaxValue, maxX = double.MinValue, minZ = double.MaxValue, maxZ = double.MinValue;
            for (int q = 0; q < 4; q++)
            {
                double x, y, tx, tz;
                KpFrame.FromGrid(q % 2 == 0 ? KpGround.ForecourtA0 : KpGround.ForecourtA1, q < 2 ? KpGround.ForecourtB0 : KpGround.ForecourtB1, out x, out y);
                f.ToTile(x, y, out tx, out tz);
                minX = Math.Min(minX, tx);
                maxX = Math.Max(maxX, tx);
                minZ = Math.Min(minZ, tz);
                maxZ = Math.Max(maxZ, tz);
            }
            List<int> near = null;
            if (t != null)
            {
                near = new List<int>();
                for (int bi = 0; bi < t.Buildings.Count; bi++)
                {
                    int[] r = t.Buildings[bi].Rings != null && t.Buildings[bi].Rings.Length > 0 ? t.Buildings[bi].Rings[0] : null;
                    if (r == null || r.Length < 6) continue;
                    bool any = false;
                    for (int i = 0; i < r.Length / 2 && !any; i++)
                        any = r[2 * i] >= 100 * minX - 2000 && r[2 * i] <= 100 * maxX + 2000 && r[2 * i + 1] >= 100 * minZ - 2000 && r[2 * i + 1] <= 100 * maxZ + 2000;
                    if (any) near.Add(bi);
                }
            }
            Affine3 g = f.Grid;
            for (int j = 0; j < nb; j++)
            {
                for (int i = 0; i < na; i++)
                {
                    double a0 = KpGround.ForecourtA0 + ca * i, b0 = KpGround.ForecourtB0 + cb * j, am = a0 + 0.5 * ca, bm = b0 + 0.5 * cb;
                    // The Dakha front's stone platform and its steps stand here.
                    if (am >= KpRanges.DA0 && am <= KpRanges.DA1 && bm < KpRanges.DB1 + KpRanges.PlatformDepth + 4 * KpRanges.StepRun) continue;
                    double x, y, tx, tz;
                    KpFrame.FromGrid(am, bm, out x, out y);
                    f.ToTile(x, y, out tx, out tz);
                    if (near != null && InBuilding(t, near, tx, tz)) continue;
                    uint h = ShapeNoise.Hash(i, j, 5, 0x4F1Au);
                    float tone = 0.93f + 0.12f * (h % 100) / 100f;
                    Kp.Floor(m, g, a0, a0 + ca, b0, b0 + cb, 0.02, Kp.Tint(Kp.ForecourtFlags, tone));
                }
            }
            if (f.C != null) ForecourtColliders(ref f);
        }

        /// <summary>
        /// The forecourt's walkable stone floor as colliders: rows of 2.4 m cells across the paved area, each run of cells
        /// clear of the drawn roads (more than 0.2 m from every drawn road edge, <see cref="SacredDrawnRoads"/>) merged into
        /// one box. Where the road mesher still draws its footway and pedestrian ribbons across the paving (open issue for the
        /// roads package, which can clip them with <see cref="KpGround.Paved"/>) the road's own surface is the floor.
        /// </summary>
        private static void ForecourtColliders(ref KpFrame f)
        {
            const double Cell = 2.4, Clear = 0.2;
            double a0 = KpGround.ForecourtA0, a1 = KpGround.ForecourtA1, b0 = KpGround.ForecourtB0, b1 = KpGround.ForecourtB1;
            if (f.Tile == null)
            {
                f.GridBox(a0, a1, b0, b1, -0.3, 0.02, GenColliderFlags.Walkable, GenColliders.Stone);
                return;
            }
            SacredDrawnRoads drawn = SacredDrawnRoads.For(f.Tile);
            int na = Math.Max(1, (int)Math.Round((a1 - a0) / Cell)), nb = Math.Max(1, (int)Math.Round((b1 - b0) / Cell));
            double ca = (a1 - a0) / na, cb = (b1 - b0) / nb;
            for (int j = 0; j < nb; j++)
            {
                double rb0 = b0 + cb * j, rb1 = rb0 + cb;
                int run = -1;
                for (int i = 0; i <= na; i++)
                {
                    bool clear = i < na && CellClear(ref f, drawn, a0 + ca * i, a0 + ca * (i + 1), rb0, rb1, Clear);
                    if (clear && run < 0) run = i;
                    if (clear || run < 0) continue;
                    f.GridBox(a0 + ca * run, a0 + ca * i, rb0, rb1, -0.3, 0.02, GenColliderFlags.Walkable, GenColliders.Stone);
                    run = -1;
                }
            }
        }

        /// <summary>True when the grid cell (a0..a1, b0..b1) keeps more than <paramref name="clear"/> metres from every drawn
        /// road (its corners, edge midpoints and centre sampled).</summary>
        private static bool CellClear(ref KpFrame f, SacredDrawnRoads drawn, double a0, double a1, double b0, double b1, double clear)
        {
            for (int q = 0; q < 9; q++)
            {
                double a = a0 + 0.5 * (q % 3) * (a1 - a0), b = b0 + 0.5 * (q / 3) * (b1 - b0), x, y, tx, tz;
                KpFrame.FromGrid(a, b, out x, out y);
                f.ToTile(x, y, out tx, out tz);
                if (drawn.SignedDistance(tx, tz) < clear) return false;
            }
            return true;
        }

        /// <summary>True when tile-local (x, z) lies inside one of the listed buildings' outlines (rings in centimetres).</summary>
        private static bool InBuilding(TileData t, List<int> near, double x, double z)
        {
            double cx = 100 * x, cz = 100 * z;
            foreach (int bi in near)
            {
                int[] r = t.Buildings[bi].Rings[0];
                int n = r.Length / 2;
                bool inside = false;
                for (int i = 0, k = n - 1; i < n; k = i++)
                {
                    double xi = r[2 * i], zi = r[2 * i + 1], xk = r[2 * k], zk = r[2 * k + 1];
                    if ((zi > cz) != (zk > cz) && cx < (xk - xi) * (cz - zi) / (zk - zi) + xi) inside = !inside;
                }
                if (inside) return true;
            }
            return false;
        }

        /// <summary>The white west front: dado, blind arches, windows, the black gallery and its struts.</summary>
        private static void WestFront(ref KpFrame f, MeshData m, in Affine3 k, double half, double uDoor, double uHan)
        {
            int lod = f.Lod;
            double g0 = uDoor - 2.25, g1 = uDoor + 2.25;
            // Red dado and the red bench ledge along the foot (not under the gate).
            Kp.Band(m, k, -half, g0 - 0.15, 0, 0.55, 0.05, Kp.Dado);
            Kp.Band(m, k, g1 + 0.15, half, 0, 0.55, 0.05, Kp.Dado);
            if (lod <= 1) Kp.Box(m, k, -half, uHan - 0.8, 0, 0.42, 0, 0.38, Kp.Dado, BoxFaces.Wall);
            // Blind cusped arches at ground level, a string course, first-floor windows.
            for (double u = -half + 1.1; u < g0 - 0.9; u += 2.2)
            {
                if (Math.Abs(u - uHan) < 0.5) continue;
                Kp.BlindArch(m, k, u, 0.7, 1.7, 2.7, lod);
            }
            if (g1 + 1.1 < half) Kp.BlindArch(m, k, 0.5 * (g1 + half), 0.7, Math.Min(1.7, half - g1 - 0.4), 2.7, lod);
            Kp.Band(m, k, -half, half, Floor1 - 0.12, Floor1 + 0.06, 0.08, Kp.WhiteTrim);
            // The first-floor windows (hanuman_gate c_14): dark lattice with white louvred leaves folded open north of the
            // gate, the white jali south of it, each under its cusped stucco hood.
            double[] wins = { -half + 1.4, -half + 3.9, uHan + 0.1, half - 1.3 };
            for (int i = 0; i < wins.Length; i++)
                Kp.RanaWindow(m, k, wins[i], Floor1 + 0.75, 0.95, 1.65, Kp.ShutterWhite, false, lod, wins[i] > uDoor, true);
            // The continuous black gallery on its diagonal struts under the eave.
            Kp.Gallery(m, k, -half - 0.05, half + 0.05, GalleryFloor, EaveV - GalleryFloor - 0.35, 0.9, 0.95, lod);
            // Pratap Malla's inscription (1664): a dark slab low in the wall north of Hanuman, with lines of script and a spout.
            double ui = uHan - 3.6;
            Kp.Box(m, k, ui - 1.25, ui + 1.25, 0.12, 0.92, 0, 0.09, Kp.BlackStone, BoxFaces.Wall);
            if (lod == 0)
            {
                for (int r = 0; r < 5; r++)
                    Kp.Box(m, k, ui - 1.1, ui + 1.1, 0.24 + 0.13 * r, 0.27 + 0.13 * r, 0.09, 0.1, Kp.StoneGrey, BoxFaces.Front);
                Kp.Box(m, k, ui - 0.08, ui + 0.08, 0.12, 0.3, 0.09, 0.24, Kp.BlackStone, BoxFaces.Wall | BoxFaces.Bottom);
            }
        }

        /// <summary>
        /// The gate (§3.1): steps, gilt jambs and lintel round the dark passage, blue pilaster panels with eyes and roundels,
        /// the three-niche panel, the carved wooden canopy, struts up to the gallery, the brass plaques and the lions.
        /// </summary>
        private static void Gate(ref KpFrame f, MeshData m, in Affine3 k, double u)
        {
            int lod = f.Lod;
            // Three stone steps across the gate.
            for (int s = 0; s < 3; s++)
            {
                double depth = 1.6 - 0.5 * s, top = 0.15 * (s + 1);
                if (lod >= 2) Kp.Box(m, k, u - 2.4, u + 2.4, 0, top, 0, depth, Kp.Step, BoxFaces.Front | BoxFaces.Top | BoxFaces.Left | BoxFaces.Right);
                else Shapes.RoundedBox(m, SacredDraw.At(k, u, top - 0.08, 0.5 * depth), Kp.Ao(Kp.Step, s == 2 ? 1f : 0.9f), 4.8, 0.16, depth, 0.025, 1);
            }
            double sill = DoorSill, dw = DoorW, dh = DoorH, top0 = sill + dh, jamb = 0.3;
            // The passage behind the door (the Narasimha niche is on its north wall, the court's light far at its end).
            Passage(ref f, m, k, u, sill, dw, dh);
            // The door frame: three stepped gilt repoussé mouldings (c_06, c_14), the inner one most proud.
            ShapeBrush gilt = Kp.Gilt, giltDeep = Kp.GiltDeep;
            for (int st = 0; st < 3; st++)
            {
                double i0 = 0.5 * dw + 0.1 * st, i1 = i0 + 0.1, pr = 0.2 - 0.05 * st, tt = top0 + 0.1 * st;
                ShapeBrush b = st == 1 ? gilt : giltDeep;
                if (lod >= 2 && st > 0) b = giltDeep;
                Kp.Box(m, k, u - i1, u - i0, sill, tt + 0.1, 0, pr, b, BoxFaces.Wall);
                Kp.Box(m, k, u + i0, u + i1, sill, tt + 0.1, 0, pr, b, BoxFaces.Wall);
                Kp.Box(m, k, u - i1, u + i1, tt, tt + 0.1, 0, pr, b, BoxFaces.Wall | BoxFaces.Bottom);
            }
            if (lod == 0)
            {
                // Small repoussé figures stacked up the jambs and along the lintel.
                for (int i = 0; i < 5; i++)
                {
                    double v = sill + 0.3 + i * 0.5;
                    for (int sd = -1; sd <= 1; sd += 2)
                    {
                        double uj = u + sd * (0.5 * dw + 0.5 * jamb);
                        Shapes.Ellipsoid(m, SacredDraw.At(k, uj, v, 0.15), giltDeep, 0.08, 0.13, 0.04, 6, false);
                        Shapes.Sphere(m, SacredDraw.At(k, uj, v + 0.17, 0.15), giltDeep, 0.05, 5);
                    }
                }
                for (int i = 0; i < 4; i++)
                    Shapes.Ellipsoid(m, SacredDraw.At(k, u - 0.66 + 0.44 * i, top0 + 0.15, 0.15), giltDeep, 0.12, 0.09, 0.04, 6, false);
            }
            // Narrow pale grey-blue painted pilasters (c_06, c_14) with a column of round deity medallions and the painted
            // eyes, framed by slim dark wooden pilasters.
            double p0 = 0.5 * dw + jamb, p1 = p0 + 0.62, ptop = top0 + jamb + 0.45;
            for (int s = -1; s <= 1; s += 2)
            {
                double ua = u + s * p0, ub = u + s * p1;
                Kp.Box(m, k, Math.Min(ua, ub), Math.Max(ua, ub), sill, ptop, 0, 0.08, Kp.GatePale, BoxFaces.Wall);
                double ue = u + s * (p1 + 0.1);
                Kp.Box(m, k, ue - 0.1, ue + 0.1, 0.0, ptop + 1.6, 0, 0.14, Kp.Wood, BoxFaces.Wall);
                if (lod > 1) continue;
                double uc = u + s * 0.5 * (p0 + p1);
                // The eyes.
                for (int e = -1; e <= 1; e += 2)
                {
                    Shapes.Ellipsoid(m, SacredDraw.At(k, uc + e * 0.13, 2.3, 0.09), Kp.WhiteTrim, 0.1, 0.055, 0.02, lod == 0 ? 8 : 5, false);
                    Shapes.Sphere(m, SacredDraw.At(k, uc + e * 0.13, 2.3, 0.105), Kp.BlackStone, 0.035, lod == 0 ? 5 : 4);
                    if (lod == 0) Kp.Box(m, k, uc + e * 0.13 - 0.11, uc + e * 0.13 + 0.11, 2.4, 2.43, 0.08, 0.1, Kp.BlackStone, BoxFaces.Front);
                }
                // Four round medallions with a deity in each (a white rim, a coloured field, a small gilt figure).
                double[] mv = { 0.95, 1.55, 2.9, 3.45 };
                for (int q = 0; q < 4; q++)
                {
                    ShapeBrush field = q % 2 == 0 ? Kp.Dado : Kp.Green;
                    Shapes.Cylinder(m, SacredDraw.Basis(k, uc, mv[q], 0.08, 1, 0, 0, 0, 0, 1), Kp.GiltDeep, 0.21, 0.03, lod == 0 ? 12 : 6);
                    Shapes.Cylinder(m, SacredDraw.Basis(k, uc, mv[q], 0.11, 1, 0, 0, 0, 0, 1), field, 0.16, 0.02, lod == 0 ? 12 : 6);
                    if (lod == 0) Shapes.Ellipsoid(m, SacredDraw.At(k, uc, mv[q], 0.14), Kp.Gilt, 0.06, 0.11, 0.035, 5, false);
                }
            }
            // The frieze over the lintel: a red and green carved band with its row of round medallions.
            double t0 = top0 + jamb, t1 = t0 + 0.45;
            Kp.Box(m, k, u - p1, u + p1, t0, t1, 0, 0.15, Kp.Dado, BoxFaces.Wall | BoxFaces.Bottom);
            Kp.Box(m, k, u - p1 - 0.05, u + p1 + 0.05, t1 - 0.1, t1, 0, 0.18, Kp.Green, BoxFaces.Wall | BoxFaces.Bottom);
            if (lod <= 1)
                for (int i = 0; i < 7; i++)
                {
                    double ua = u - p1 + 2 * p1 * (i + 0.5) / 7;
                    Shapes.Cylinder(m, SacredDraw.Basis(k, ua, t0 + 0.17, 0.15, 1, 0, 0, 0, 0, 1), i % 2 == 0 ? Kp.Gilt : Kp.Green, 0.13, 0.03, lod == 0 ? 10 : 6);
                    if (lod == 0) Shapes.Sphere(m, SacredDraw.At(k, ua, t0 + 0.17, 0.18), Kp.Tint(Kp.Cream, 0.95f), 0.05, 4);
                }
            // The blue panel above (c_14) with its figures in the round under three carved wooden toranas: Krishna with
            // the gopinis on the left, the large multi-armed deity in a gilt aureole in the middle, two seated figures on
            // the right.
            double n0 = t1 + 0.05, n1 = n0 + 1.25;
            Kp.Box(m, k, u - p1 - 0.1, u + p1 + 0.1, n0, n1, 0, 0.1, Kp.GateBlue, BoxFaces.Wall);
            for (int q = -1; q <= 1; q++)
            {
                double uq = u + q * 0.82;
                if (lod >= 2) continue;
                Kp.Box(m, k, uq - 0.4, uq - 0.34, n0 + 0.05, n1 - 0.05, 0.1, 0.24, Kp.Wood, BoxFaces.Wall);
                Kp.Box(m, k, uq + 0.34, uq + 0.4, n0 + 0.05, n1 - 0.05, 0.1, 0.24, Kp.Wood, BoxFaces.Wall);
                // The carved wooden torana over the niche.
                ShikharaGenerator.Arch(m, k, uq, n1 - 0.42, 0.11, 0.7, 0.3, 0.09, 0.1, true, Kp.WoodMid, lod);
                if (lod > 0) continue;
                Shapes.Sphere(m, SacredDraw.At(k, uq, n1 - 0.05, 0.16), Kp.Wood, 0.06, 4);
                if (q == 0)
                {
                    Shapes.Cylinder(m, SacredDraw.Basis(k, uq, n0 + 0.55, 0.11, 1, 0, 0, 0, 0, 1), Kp.Gilt, 0.3, 0.03, 12);
                    Kp.SeatedFigure(m, k, uq, n0 + 0.05, 0.24, 0.8, Kp.GateBlueDeep, Kp.Dado, 8, lod);
                }
                else if (q < 0)
                {
                    Kp.SeatedFigure(m, k, uq, n0 + 0.05, 0.22, 0.75, Kp.GateBlueDeep, Kp.Cloak, 2, lod);
                    for (int e = -1; e <= 1; e += 2) Kp.SeatedFigure(m, k, uq + e * 0.22, n0 + 0.05, 0.17, 0.45, Kp.Cream, e < 0 ? Kp.Green : Kp.Dado, 2, lod);
                }
                else
                    for (int e = -1; e <= 1; e += 2) Kp.SeatedFigure(m, k, uq + e * 0.15, n0 + 0.05, 0.2, 0.6, Kp.Cream, e < 0 ? Kp.Cloak : Kp.Green, 2, lod);
            }
            // The carved wooden canopy: a projecting hood with a frieze of small cusped arches and a sloping top.
            double c0 = n1 + 0.1, c1 = c0 + 0.85, cw = p1 + 0.55;
            Kp.Box(m, k, u - cw, u + cw, c0, c1, 0, 0.42, Kp.Wood, BoxFaces.Wall | BoxFaces.Bottom);
            SacredDraw.Quad(m, k, u - cw - 0.1, c1, 0.5, u + cw + 0.1, c1, 0.5, u + cw + 0.1, c1 + 0.35, 0, u - cw - 0.1, c1 + 0.35, 0, 0, 0.8, 0.6, Kp.WoodBeam);
            if (lod <= 1)
            {
                int arches = lod == 0 ? 6 : 3;
                for (int i = 0; i < arches; i++)
                {
                    double ua = u - cw + 0.15 + (2 * cw - 0.3) * (i + 0.5) / arches;
                    ShikharaGenerator.Arch(m, k, ua, c0 + 0.16, 0.43, (2 * cw - 0.3) / arches - 0.08, 0.3, 0.05, 0.05, true, Kp.WoodMid, lod + 1);
                }
                // Struts from the canopy up to the gallery floor.
                for (int i = 0; i < 4; i++)
                    Kp.Strut(m, k, u - 1.5 + 1.0 * i, c1 + 0.1, 0.05, GalleryFloor - 0.25, 0.75, 0.065, Kp.Wood);
                // Brass museum plaques either side of the canopy.
                Kp.Box(m, k, u - cw - 0.6, u - cw - 0.12, c0 + 0.05, c0 + 0.4, 0, 0.03, Kp.Gilt, BoxFaces.Wall);
                Kp.Box(m, k, u + cw + 0.12, u + cw + 0.6, c0 + 0.05, c0 + 0.4, 0, 0.03, Kp.Gilt, BoxFaces.Wall);
            }
            // The two painted lions (white with red stripes, no green) with their riders (Shiva and Parvati) and marigold
            // garlands, on pedestals, one each side, facing the square.
            for (int s = -1; s <= 1; s += 2)
            {
                double ul = u + s * (p1 + 0.75);
                int lf = m.VertexCount;
                Kp.Lion(m, k, ul, 0, 0.72, 0.85, 1.45, true, s, lod, SacredFigures.Hero);
                Kp.Recolor(m, lf, 0x2F6E4F, 0xEDE7DA);
                if (lod <= 1)
                {
                    // The rider seated on the lion's back, and the marigold garland round the lion's neck.
                    double vr = 0.85 + 0.6 * 1.45;
                    Kp.SeatedFigure(m, k, ul, vr - 0.05, 0.6, 0.85, Kp.Ao(Kp.Cream, 0.95f), s < 0 ? Kp.GateBlue : Kp.Cloak, 4, lod);
                    Shapes.Torus(m, SacredDraw.Basis(k, ul, 0.85 + 0.72 * 1.45, 0.72 + 0.22, 1, 0, 0, 0, 0.5, 0.866), Kp.Marigold, 0.24, 0.05, lod == 0 ? 14 : 8,
                                 lod == 0 ? 5 : 3);
                }
            }
        }

        /// <summary>The first metres of the passage through the block: dark walls and ceiling, a stone floor, the Narasimha
        /// group in its silver arched niche on the north wall (LOD0), and the court's daylight at the end.</summary>
        private static void Passage(ref KpFrame f, MeshData m, in Affine3 k, double u, double sill, double dw, double dh)
        {
            int lod = f.Lod;
            double hu = 0.5 * dw, depth = 4.5;
            if (lod >= 2)
            {
                Kp.Panel(m, k, u - hu, sill, u + hu, sill + dh, 0.012, Kp.Recess);
                return;
            }
            ShapeBrush wall = Kp.Ao(Kp.WhiteRelief, 0.55f);
            Kp.Floor(m, k, u - hu, u + hu, -depth, 0, sill, Kp.Ao(Kp.Step, 0.6f));
            SacredDraw.Quad(m, k, u - hu, sill, -depth, u - hu, sill, 0, u - hu, sill + dh, 0, u - hu, sill + dh, -depth, 1, 0, 0, wall);
            SacredDraw.Quad(m, k, u + hu, sill, -depth, u + hu, sill, 0, u + hu, sill + dh, 0, u + hu, sill + dh, -depth, -1, 0, 0, wall);
            SacredDraw.Quad(m, k, u - hu, sill + dh, -depth, u + hu, sill + dh, -depth, u + hu, sill + dh, 0, u - hu, sill + dh, 0, 0, -1, 0, Kp.Ao(Kp.Wood, 0.5f));
            Kp.Panel(m, k, u - hu, sill, u + hu, sill + dh, -depth, Kp.Ao(Kp.BrickNew, 0.85f)); // the sunlit court at the end
            // The carved door leaves stand open against the passage walls.
            SacredDraw.Quad(m, k, u - hu + 0.06, sill, -0.15, u - hu + 0.06, sill, -0.15 - hu, u - hu + 0.06, sill + dh, -0.15 - hu, u - hu + 0.06, sill + dh, -0.15, 1, 0, 0,
                            Kp.Ao(Kp.WoodMid, 0.6f));
            SacredDraw.Quad(m, k, u + hu - 0.06, sill, -0.15, u + hu - 0.06, sill, -0.15 - hu, u + hu - 0.06, sill + dh, -0.15 - hu, u + hu - 0.06, sill + dh, -0.15, -1, 0, 0,
                            Kp.Ao(Kp.WoodMid, 0.6f));
            if (lod > 0) return;
            // Narasimha (1673) on the north (u−) wall: a silver repoussé arch with two sun discs round a dark stone group.
            Affine3 nw = SacredDraw.Basis(k, u - hu, 0, -1.7, 0, 0, -1, 0, 1, 0);
            Kp.Box(m, nw, -0.6, 0.6, sill + 0.3, sill + 2.25, 0, 0.06, Kp.Silver, BoxFaces.Wall);
            ShikharaGenerator.Arch(m, nw, 0, sill + 1.95, 0.06, 1.1, 0.35, 0.08, 0.06, true, Kp.Silver, 0);
            for (int s = -1; s <= 1; s += 2) Shapes.Cylinder(m, SacredDraw.Basis(nw, s * 0.45, sill + 2.45, 0.06, 1, 0, 0, 0, 0, 1), Kp.Silver, 0.11, 0.03, 8);
            Kp.Panel(m, nw, -0.48, sill + 0.35, 0.48, sill + 2.0, 0.065, Kp.Ao(Kp.Recess, 0.9f));
            // The seated lion-headed guardian with a figure across his knees (a general reading; no gore).
            Shapes.Ellipsoid(m, SacredDraw.At(nw, 0, sill + 0.95, 0.3), Kp.BlackStone, 0.3, 0.42, 0.2, 8, false);
            Shapes.Ellipsoid(m, SacredDraw.At(nw, 0, sill + 1.55, 0.32), Kp.BlackStone, 0.19, 0.2, 0.17, 8, false);
            Shapes.Ellipsoid(m, SacredDraw.At(nw, 0, sill + 0.72, 0.42), Kp.Tint(Kp.BlackStone, 1.25f), 0.38, 0.1, 0.12, 6, false);
            Shapes.Cone(m, SacredDraw.At(nw, 0, sill + 1.7, 0.3), Kp.Silver, 0.14, 0.25, 6, 0, 0, false);
        }

        /// <summary>
        /// Hanuman (1672; photos hanuman_gate c_14, hanuman ref_02, ref_08): a tall (1.8 m) red-smeared stepped pedestal with
        /// a lotus cornice; on it the draped figure: a rounded head (about 0.5 m) under the hood of the red-orange cloak with
        /// the darker red sindoor-smeared face, the cloak bulging over the shoulders (about 0.9 m) and falling to a base of
        /// about 1 m, marigold garlands; the grey metal parasol with its deep red valance on a pole, and two dark metal
        /// pataka banners on thin poles either side of it.
        /// </summary>
        private static void Hanuman(ref KpFrame f, MeshData m, in Affine3 k, double u)
        {
            int lod = f.Lod;
            double w = HanumanOut;
            ShapeLod sl = f.SL;
            const double Ped = 1.8, pv = 4.25, pr = 0.98;
            if (lod >= 2)
            {
                Kp.Box(m, k, u - 0.48, u + 0.48, 0, Ped, w - 0.48, w + 0.48, Kp.Sindoor);
                Shapes.Cone(m, SacredDraw.At(k, u, Ped, w), Kp.Cloak, 0.5, 1.75, 6, 0, 0, false);
                Shapes.Cylinder(m, SacredDraw.At(k, u, pv - 0.45, w), Kp.Valance, 1.0, 0.45, 8, 0, 0, false, false);
                Shapes.Cone(m, SacredDraw.At(k, u, pv, w), Kp.ParasolTop, 1.0, 0.55, 8, 0, 0, false);
                return;
            }
            // The stepped pedestal: a stone step, two red-smeared stages, the lotus cornice.
            Shapes.RoundedSlab(m, SacredDraw.At(k, u, 0, w), Kp.Step, 1.45, 1.45, 0.2, 0.06, 0.03, 2, sl);
            Shapes.RoundedBox(m, SacredDraw.At(k, u, 0.55, w), Kp.Tint(Kp.Sindoor, 0.8f), 1.1, 0.7, 1.1, 0.05, 2, sl);
            Shapes.RoundedBox(m, SacredDraw.At(k, u, 1.25, w), Kp.Sindoor, 0.92, 0.7, 0.92, 0.05, 2, sl);
            var lotus = new Profile2(10);
            lotus.Add(0, 0, true).Add(0.46, 0, true).Add(0.56, 0.08).Add(0.62, 0.18).Add(0.6, 0.24, true).Add(0.52, 0.24, true).Add(0, 0.24, true);
            Shapes.Lathe(m, SacredDraw.At(k, u, Ped - 0.2, w), Kp.Tint(Kp.Sindoor, 1.25f), lotus, lod == 0 ? 16 : 8, sl);
            if (lod == 0)
                for (int i = 0; i < 12; i++)
                {
                    double a = 2 * Math.PI * (i + 0.5) / 12;
                    Shapes.Ellipsoid(m, SacredDraw.At(k, u + 0.57 * Math.Sin(a), Ped - 0.08, w + 0.57 * Math.Cos(a)), Kp.Tint(Kp.Sindoor, 1.4f), 0.09, 0.1, 0.09, 5, false);
                }
            // The draped figure: the cloak lathed from the hem over the bulging shoulders to the neck, the hooded head.
            const double fv = Ped + 0.04;
            var p = new Profile2(16);
            p.Add(0, 0, true).Add(0.5, 0, true).Add(0.52, 0.08).Add(0.49, 0.35).Add(0.45, 0.7).Add(0.45, 0.95).Add(0.46, 1.08).Add(0.4, 1.2).Add(0.24, 1.27)
             .Add(0.15, 1.3, true).Add(0, 1.3, true);
            Shapes.Lathe(m, SacredDraw.At(k, u, fv, w), Kp.Cloak, p.Smooth(lod == 0 ? 2 : 1), lod == 0 ? 20 : 12, sl);
            Shapes.Ellipsoid(m, SacredDraw.At(k, u, fv + 1.5, w), Kp.Cloak, 0.25, 0.28, 0.25, lod == 0 ? 12 : 8, false, sl);
            // The face: the sindoor-smeared mask in the hood's opening.
            Shapes.Ellipsoid(m, SacredDraw.At(k, u, fv + 1.48, w + 0.12), Kp.Sindoor, 0.17, 0.19, 0.12, lod == 0 ? 10 : 6, false, sl);
            // Marigold garlands round the neck and down the front.
            Shapes.Torus(m, SacredDraw.Basis(k, u, fv + 1.2, w + 0.04, 1, 0, 0, 0, 0.94, 0.34), Kp.Marigold, 0.36, 0.045, lod == 0 ? 16 : 8, lod == 0 ? 5 : 3, sl);
            if (lod == 0)
            {
                Shapes.Torus(m, SacredDraw.Basis(k, u, fv + 0.95, w + 0.1, 1, 0, 0, 0, 0.87, 0.5), Kp.Tint(Kp.Marigold, 0.9f), 0.4, 0.04, 16, 4, sl);
                // A few deep folds down the cloak.
                for (int i = -2; i <= 2; i++)
                {
                    double a = i * 0.35;
                    Shapes.Bar(m, SacredDraw.At(k, u, fv, w), Kp.Tint(Kp.Cloak, 0.8f), 0.5 * Math.Sin(a), 0.08, 0.5 * Math.Cos(a), 0.44 * Math.Sin(a), 0.95,
                               0.44 * Math.Cos(a), 0.028, 5);
                }
            }
            // The parasol: a pole behind the figure, the grey metal canopy and the deep red valance with its white band.
            Shapes.Bar(m, SacredDraw.At(k, u, 0, w), Kp.Iron, 0, Ped, -0.55, 0, pv, -0.55, 0.03, 6);
            Shapes.Bar(m, SacredDraw.At(k, u, 0, w), Kp.Iron, 0, pv - 0.1, -0.55, 0, pv + 0.02, 0, 0.025, 5);
            Shapes.Dome(m, SacredDraw.At(k, u, pv - 0.02, w), Kp.ParasolTop, pr, 0.55, 16, true, sl);
            Shapes.Sphere(m, SacredDraw.At(k, u, pv + 0.57, w), Kp.Gilt, 0.08, 8, sl);
            int seg = lod == 0 ? 28 : 14;
            int first = m.VertexCount;
            for (int i = 0; i <= seg; i++)
            {
                double a = 2 * Math.PI * i / seg, ca = Math.Cos(a), sa = Math.Sin(a), drop = i % 2 == 0 ? 0.48 : 0.4, r = pr + 0.02;
                SacredDraw.V(m, k, u + r * sa, pv, w + r * ca, sa, 0, ca, Kp.ValanceWhite);
                SacredDraw.V(m, k, u + r * sa, pv - 0.1, w + r * ca, sa, 0, ca, Kp.Valance);
                SacredDraw.V(m, k, u + (r + 0.04) * sa, pv - drop, w + (r + 0.04) * ca, sa, -0.1, ca, Kp.Valance);
            }
            for (int i = 0; i < seg; i++)
            {
                int a = first + 3 * i, b = a + 3;
                SacredDraw.Quad(m, a, b, b + 1, a + 1);
                SacredDraw.Quad(m, a + 1, b + 1, b + 2, a + 2);
            }
            // The two dark metal pataka banners on thin poles either side of the parasol (c_14): flag-shaped plates with a
            // wavy fly edge, facing the square.
            for (int s = -1; s <= 1; s += 2)
            {
                double pu = u + s * 1.25;
                Shapes.Bar(m, k, Kp.Iron, pu, 0, w - 0.2, pu, 4.15, w - 0.2, 0.025, 5);
                Shapes.Sphere(m, SacredDraw.At(k, pu, 4.2, w - 0.2), Kp.Gilt, 0.05, 5);
                double[] px = ShapeScratch.U, pz = ShapeScratch.W;
                int n = 0;
                // The plate in its own plane (x across from the pole, z = −height): a pennant with a wavy fly.
                px[n] = 0; pz[n++] = -3.95;
                px[n] = s * 0.55; pz[n++] = -3.85;
                px[n] = s * 0.45; pz[n++] = -3.6;
                px[n] = s * 0.6; pz[n++] = -3.35;
                px[n] = s * 0.42; pz[n++] = -3.1;
                px[n] = s * 0.5; pz[n++] = -2.85;
                px[n] = 0; pz[n++] = -2.75;
                Affine3 fp = SacredDraw.Basis(k, pu, 0, w - 0.18, 1, 0, 0, 0, 0, 1);
                Shapes.BevelExtrude(m, fp, Kp.Ao(Kp.ParasolTop, 0.85f), px, pz, n, 0.03, 0, 0, BevelStyle.Chamfer, true, true);
            }
        }

        /// <summary>Agamchhen (§3.5): a slim three-roof temple on a brick storey with a carved lattice gallery, rising from
        /// the block behind the gate; red fringed valances on all three eaves; gilt finial at 23 m.</summary>
        private static void Agamchhen(ref KpFrame f, MeshData m)
        {
            int lod = f.Lod;
            double x, y;
            KpFrame.FromGrid(AgamA, AgamB, out x, out y);
            Affine3 k = f.Plan(x, y, 0, 102 + 180);
            // The brick base storey through the block's roof, its upper part a carved lattice gallery.
            var tiers = new KpTier[4];
            tiers[0] = new KpTier { Base = EaveV - 1.0, WallU = 3.0, WallW = 3.0, Eave = 14.1, EaveU = 3.7, EaveW = 3.7, Top = 15.5, Walls = KpWalls.Brick, Windows = 2 };
            tiers[1] = new KpTier { Base = 15.5, WallU = 2.35, WallW = 2.35, Eave = 17.4, EaveU = 3.0, EaveW = 3.0, Top = 18.6, Walls = KpWalls.Lattice };
            tiers[2] = new KpTier { Base = 18.6, WallU = 1.75, WallW = 1.75, Eave = 20.2, EaveU = 2.3, EaveW = 2.3, Top = 21.65, ApexU = 0.3, ApexW = 0.3, Walls = KpWalls.Brick, Windows = 1 };
            // A carved lattice gallery band round the base storey's top (behind the lowest roof).
            KpRoofLook look = KpRoofLook.TileFringe(Kp.Tint(Kp.Tile, 0.8f));
            KpStack.Build(m, k, tiers, 3, Kp.Brick, look, 31u, lod);
            Kp.Finial(m, k, 0, 21.6, 0, AgamTopM - 21.6, 0.3, lod);
            if (lod == 0)
            {
                // Pataka: a gilt banner hanging from the finial down the west face.
                double[] pu = { 0, 0, 0, 0 }, pv = { 21.5, 20.3, 18.9, 17.6 }, pw = { 0.35, 1.9, 2.4, 3.05 };
                SacredParts.Pataka(m, k, pu, pv, pw, 4, 0.3, lod);
            }
        }

        private static void Colliders(ref KpFrame f, double uDoor, double uHan)
        {
            if (f.C == null) return;
            f.GridBox(A0, A1, B0, B1, 0, EaveV, GenColliderFlags.NoClimb, GenColliders.Brick);
            // Steps up to the threshold (a walkable stair), the lions' pedestals and Hanuman's pillar.
            double bDoor = DoorB, a = A0;
            f.GridBox(a - 1.6, a, bDoor - 2.75, bDoor + 2.75, 0, 0.15, GenColliderFlags.Walkable, GenColliders.Stone);
            f.GridBox(a - 1.1, a, bDoor - 2.75, bDoor + 2.75, 0, 0.3, GenColliderFlags.Walkable, GenColliders.Stone);
            f.GridBox(a - 0.6, a, bDoor - 2.75, bDoor + 2.75, 0, 0.45, GenColliderFlags.Walkable, GenColliders.Stone);
            for (int s = -1; s <= 1; s += 2)
            {
                double x, y;
                KpFrame.FromGrid(a - 0.75, bDoor - s * 2.75, out x, out y);
                f.Cylinder(x, y, 0, 0.95 + 1.3, 0.55);
            }
            double hx, hy;
            KpFrame.FromGrid(a - HanumanOut, HanumanB, out hx, out hy);
            f.Cylinder(hx, hy, 0, 3.4, 0.55);
            f.Cylinder(hx, hy, 0, 4.5, 0.06, true);
            // Agamchhen above the roof.
            double ax, ay;
            KpFrame.FromGrid(AgamA, AgamB, out ax, out ay);
            f.Box(ax, ay, 3.7, 3.7, 102, EaveV, AgamTopM - 1.5, GenColliderFlags.NoClimb, GenColliders.Brick);
        }

        /// <summary>LOD3: the block, its roof, the gate's colours and Agamchhen as a stack (≤ 200 triangles).</summary>
        private static void Lod3(ref KpFrame f, MeshData m)
        {
            Affine3 g = f.Grid;
            Kp.Box(m, g, A0, A1, 0, EaveV, B0, B1, Kp.White, BoxFaces.All & ~BoxFaces.Bottom);
            KpLod3.Pyramid(m, g, 0.5 * (A0 + A1), 0.5 * (B0 + B1), 0.5 * (A1 - A0) + 0.9, 0.5 * (B1 - B0) + 0.9, EaveV, RidgeV, Kp.Tile);
            double half;
            Affine3 west = f.GridSide(A0, A1, B0, B1, 0, 3, out half);
            double u = -(DoorB - 0.5 * (B0 + B1));
            Kp.Panel(m, west, u - 2.25, 0.4, u + 2.25, 4.3, 0.06, Kp.GateBlue);
            Kp.Panel(m, west, u - 0.9, 0.45, u + 0.9, 3.4, 0.08, Kp.Gilt);
            double x, y;
            KpFrame.FromGrid(AgamA, AgamB, out x, out y);
            Affine3 k = f.Plan(x, y, 0, 102);
            Kp.Box(m, k, -3, 3, EaveV, 15.5, -3, 3, Kp.Brick, BoxFaces.Front | BoxFaces.Back | BoxFaces.Left | BoxFaces.Right);
            KpLod3.Pyramid(m, k, 0, 0, 3.7, 3.7, 14.1, AgamTopM, Kp.Tile);
        }
    }

    /// <summary>LOD3 helpers: plain masses of a few triangles.</summary>
    internal static class KpLod3
    {
        /// <summary>A four-sided roof (a ridge on a long plan) over a rectangle centred at (cu, cw) of a frame.</summary>
        public static void Pyramid(MeshData m, in Affine3 k, double cu, double cw, double hu, double hw, double eave, double apex, in ShapeBrush b)
        {
            double ru = Math.Max(0, hu - hw), rw = Math.Max(0, hw - hu);
            for (int s = 0; s < 4; s++)
            {
                double au, aw, bu, bw, ou, ow;
                SacredParts.OnRect(s, -1, hu, hw, out au, out aw);
                SacredParts.OnRect(s, 1, hu, hw, out bu, out bw);
                SacredParts.Out(s, out ou, out ow);
                double ta, tb, tc, td;
                SacredParts.OnRect(s, -1, ru, rw, out ta, out tb);
                SacredParts.OnRect(s, 1, ru, rw, out tc, out td);
                if (Math.Abs(ta - tc) + Math.Abs(tb - td) < 1e-6)
                    SacredDraw.Tri(m, k, cu + au, eave, cw + aw, cu + bu, eave, cw + bw, cu + ta, apex, cw + tb, ou, 1, ow, b);
                else
                    SacredDraw.Quad(m, k, cu + au, eave, cw + aw, cu + bu, eave, cw + bw, cu + tc, apex, cw + td, cu + ta, apex, cw + tb, ou, 1, ow, b);
            }
        }
    }
}
