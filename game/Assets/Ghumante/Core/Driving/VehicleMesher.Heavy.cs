using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Driving
{
    /// <summary>
    /// Vans, buses, trucks, the Safa tempo and the tractor (ref_vehicles.md §3-4): one-box bodies are smooth hulls
    /// whose bottoms rise over every axle (real wheel arches) and whose tops follow a side profile; windows, doors,
    /// lamps, grilles, bumpers, decorations and the class fittings (route boards, roof racks, battery pods, truck art,
    /// tassels, ladders) are panels and parts on top.
    /// </summary>
    public static partial class VehicleMesher
    {
        // -----------------------------------------------------------------------------------------------------------
        // Generic one-box hull
        // -----------------------------------------------------------------------------------------------------------

        /// <summary>Height of a z-ascending polyline (Prof2 x = z, y = height) at z.</summary>
        private static double SampleTop(Profile2 top, double z)
        {
            if (z <= top.X[0]) return top.Y[0];
            for (int i = 1; i < top.Count; i++)
                if (z <= top.X[i])
                    return Lerp(top.Y[i - 1], top.Y[i], (z - top.X[i - 1]) / Math.Max(1e-6, top.X[i] - top.X[i - 1]));
            return top.Y[top.Count - 1];
        }

        /// <summary>
        /// A one-box body hull from zr to zf: tops from <paramref name="top"/> (z ascending), bottoms at the sill with arches
        /// cut over each wheel axle (radius + gap) and bumpers at the ends, rounded ends, rounded roof edges.
        /// </summary>
        private static void BoxBody(ref Ctx c, in ShapeBrush b, Profile2 top, double zr, double zf, double sill, double hw, double rTop, double rBot,
                                    WheelSocket[] wheels, double archGap, double rEndF, double rEndR, double bumperF, double bumperR)
        {
            int n = 0, archN = c.L0 ? (zf - zr > 6 ? 8 : 10) : c.L1 ? 5 : 3;
            double step = (c.L0 ? 0.30 : c.L1 ? 0.55 : 1.1) * (zf - zr > 6 ? 1.8 : 1.0);
            int endN = c.L0 ? 3 : c.L1 ? 2 : 1;
            for (int i = 0; i <= endN; i++)
            {
                double ph = 0.5 * Math.PI * i / endN;
                AddStation(ref n, zr + rEndR * (1 - Math.Cos(ph)));
                AddStation(ref n, zf - rEndF * (1 - Math.Cos(ph)));
            }
            for (int w = 0; w < wheels.Length; w++)
            {
                if (wheels[w].X < 0) continue;
                double A = wheels[w].Radius + archGap;
                for (int i = 0; i <= archN; i++) AddStation(ref n, wheels[w].Z - A + 2 * A * i / archN);
            }
            for (double z = zr + rEndR + step; z < zf - rEndF; z += step) AddStation(ref n, z);
            for (int i = 0; i < top.Count; i++) AddStation(ref n, top.X[i]);
            n = SortStations(n);
            HullBegin();
            double zFirstArch = double.MaxValue, zLastArch = double.MinValue;
            for (int w = 0; w < wheels.Length; w++)
            {
                double A = wheels[w].Radius + archGap;
                zFirstArch = Math.Min(zFirstArch, wheels[w].Z - A);
                zLastArch = Math.Max(zLastArch, wheels[w].Z + A);
            }
            for (int i = 0; i < n; i++)
            {
                double z = s_st[i];
                if (z < zr - 1e-6 || z > zf + 1e-6) continue;
                double yb = sill;
                bool inArch = false;
                for (int w = 0; w < wheels.Length; w++)
                {
                    double A = wheels[w].Radius + archGap, dz = z - wheels[w].Z;
                    if (Math.Abs(dz) < A)
                    {
                        yb = Math.Max(yb, wheels[w].Y + Math.Sqrt(A * A - dz * dz));
                        inArch = true;
                    }
                }
                if (!inArch)
                {
                    if (z < zFirstArch) yb = Lerp(bumperR, sill, (z - zr) / Math.Max(1e-3, zFirstArch - zr));
                    else if (z > zLastArch) yb = Lerp(sill, bumperF, (z - zLastArch) / Math.Max(1e-3, zf - zLastArch));
                }
                double yt = SampleTop(top, z), hu = hw;
                double eF = zf - z < rEndF ? rEndF - Math.Sqrt(Math.Max(0, rEndF * rEndF - (rEndF - (zf - z)) * (rEndF - (zf - z)))) : 0;
                double eR = z - zr < rEndR ? rEndR - Math.Sqrt(Math.Max(0, rEndR * rEndR - (rEndR - (z - zr)) * (rEndR - (z - zr)))) : 0;
                double e = Math.Max(eF, eR);
                hu -= e;
                yb += 0.5 * e;
                yt -= 0.5 * e;
                if (yt < yb + 0.04) yt = yb + 0.04;
                double hv = 0.5 * (yt - yb);
                HullAdd(z, 0, 0.5 * (yt + yb), hu, hv, Math.Min(rTop, 0.95 * hv), Math.Min(rBot, 0.9 * hv));
            }
            HullEmit(ref c, b, 3, true, true);
        }

        /// <summary>A side window row: glass panels with a black frame band behind them on both sides, from z0 to z1 split
        /// into <paramref name="n"/> panes.</summary>
        private static void WindowRow(ref Ctx c, double x, double z0, double z1, double y0, double y1, int n, double gap, uint glass, bool frame = true,
                                      bool flat = false)
        {
            double pane = (z1 - z0 - (n - 1) * gap) / n;
            for (int side = -1; side <= 1; side += 2)
            {
                double xs = side * x;
                if (frame && c.L1) Decal(ref c, Plain(TrimC), xs, 0.5 * (y0 + y1), 0.5 * (z0 + z1), side, 0, 0, z1 - z0 + 0.06, y1 - y0 + 0.06, 0.04, 0.003);
                for (int i = 0; i < n; i++)
                {
                    double zc = z0 + pane * 0.5 + i * (pane + gap);
                    if (flat) Decal(ref c, Glass(glass), xs, 0.5 * (y0 + y1), zc, side, 0, 0, pane, y1 - y0, 0.05, 0.006);
                    else Panel(ref c, Glass(glass), xs + side * 0.004, 0.5 * (y0 + y1), zc, side, 0, 0, pane, y1 - y0, 0.05, 0.008);
                }
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        // Microbus and ambulance (Hiace H200 type; electric micro)
        // -----------------------------------------------------------------------------------------------------------

        private static void Van(ref Ctx c, in VehicleCatalogEntry e, Dims d, in Livery p)
        {
            double zr = -d.Rear, zf = d.Wheelbase + d.Front, hw = d.Half, h = d.Height, wb = d.Wheelbase;
            bool ambulance = e.TrafficClass == Traffic.VehicleClass.Service, electric = !ambulance && c.Model == 1;
            uint body = p.Body, stripe = p.Accent;
            ShapeBrush paint = Paint(body), black = Plain(TrimC);
            double roof = h - (ambulance ? 0.06 : 0.04), noseY = 0.62, hoodY = 0.95, sb = zf - 0.42, st = zf - 0.98;
            int v0 = c.M.VertexCount;
            Profile2 top = Prof2.Clear(false);
            top.Add(zr, roof - 0.06, true).Add(zr + 0.15, roof, false).Add(st, roof, false).Add(sb, hoodY, false).Add(zf - 0.10, noseY + 0.14, false)
               .Add(zf, noseY, false);
            WheelSocket[] ws = Wheels(e);
            BoxBody(ref c, paint, top, zr, zf, 0.36, hw, 0.16, 0.06, ws, 0.05, 0.10, 0.06, 0.26, 0.30);
            OBox(ref c, Plain(UnderC, 0.3f), T(0, 0.55, 0.5 * wb), 2 * (hw - ws[0].Width - 0.03), 0.6, wb + 1.0);

            // Windscreen (raked, wide) and the glass all round.
            double sy0 = hoodY + 0.04, sy1 = roof - 0.10, sl = Math.Sqrt((sb - st) * (sb - st) + (sy1 - sy0) * (sy1 - sy0));
            double snz = (sy1 - sy0) / sl, sny = (sb - st) / sl;
            Panel(ref c, Glass(ScreenC), 0, 0.5 * (sy0 + sy1), 0.5 * (sb + st) + 0.03, 0, sny, snz, 2 * hw - 0.20, sl - 0.06, 0.08, 0.01);
            double wy0 = 1.06, wy1 = roof - 0.16;
            // Front door glass, sliding door / side windows, rear quarter.
            WindowRow(ref c, hw + 0.002, st - 0.55, st + 0.05, wy0, wy1, 1, 0.08, GlassC);
            WindowRow(ref c, hw + 0.002, zr + 0.25, st - 0.70, wy0, wy1, ambulance ? 1 : 3, 0.07, GlassC);
            Panel(ref c, Glass(GlassC), 0, 0.5 * (wy0 + wy1), zr - 0.004, 0, 0, -1, 2 * hw - 0.30, wy1 - wy0, 0.06, 0.01);
            if (ambulance) Decal(ref c, Paint(MeshColor.FromHex(0xF2F2F2)), 0, 0.5 * (wy0 + wy1), zr - 0.016, 0, 0, -1, 2 * hw - 0.36, wy1 - wy0 - 0.06, 0.05, 0.002);

            // Belt stripe (micro: livery accent; ambulance: red band) and the sliding door line on the left (kerb) side.
            for (int side = -1; side <= 1; side += 2)
            {
                double x = side * (hw + 0.003);
                Decal(ref c, Paint(stripe), x, 0.92, 0.5 * (zr + zf) - 0.1, side, 0, 0, zf - zr - 0.5, ambulance ? 0.16 : 0.07, 0.03, 0.003);
                if (c.L1)
                {
                    Decal(ref c, Plain(Shade(body, 0.6f)), x, 0.70, st - 0.62, side, 0, 0, 0.008, 0.70, 0.003, 0.004);
                    if (side < 0) Decal(ref c, Plain(Shade(body, 0.6f)), x, 0.72, st - 1.55, side, 0, 0, 0.008, 0.74, 0.003, 0.004);
                    OBox(ref c, Chrome(), T(x + side * 0.006, 0.98, st - 0.70), 0.012, 0.03, 0.12);
                }
            }
            // Front: grille band under the screen, high corner lamps, bumper, indicator, plate.
            if (electric)
            {
                Decal(ref c, Glass(DrlC), 0, noseY + 0.17, zf - 0.07, 0, 0.55, 0.84, 2 * hw - 0.30, 0.035, 0.015, 0.004);
                Panel(ref c, Paint(stripe), 0, noseY - 0.08, zf + 0.003, 0, 0, 1, 1.0, 0.10, 0.04, 0.008);
            }
            else
            {
                Panel(ref c, black, 0, noseY - 0.05, zf + 0.003, 0, 0, 1, 0.80, 0.16, 0.03, 0.01);
                if (c.L1)
                    for (int k = 0; k < 3; k++) Decal(ref c, Chrome(), 0, noseY - 0.10 + 0.05 * k, zf + 0.014, 0, 0, 1, 0.72, 0.012, 0.004, 0.002);
            }
            for (int k = -1; k <= 1; k += 2)
            {
                Panel(ref c, Glass(LensC), k * (hw - 0.22), noseY + (electric ? 0.06 : 0.03), zf - 0.01, k * 0.3, 0.2, 1, 0.24, 0.12, 0.04, 0.01);
                if (c.L1) Decal(ref c, Glass(AmberC), k * (hw - 0.07), noseY + 0.03, zf - 0.04, k * 0.8, 0, 0.6, 0.06, 0.08, 0.015, 0.004);
                Panel(ref c, Glass(TailC), k * (hw - 0.10), 0.80, zr - 0.004, 0, 0, -1, 0.10, 0.36, 0.03, 0.01);
            }
            OBox(ref c, Plain(TrimC), T(0, 0.30, zf - 0.02), 2 * hw - 0.10, 0.14, 0.10);
            OBox(ref c, Plain(TrimC), T(0, 0.33, zr + 0.03), 2 * hw - 0.10, 0.14, 0.10);
            // Mirrors on arms, wipers.
            if (c.L1)
                for (int side = -1; side <= 1; side += 2)
                {
                    Rod(ref c, black, side * (hw - 0.02), hoodY + 0.02, sb - 0.12, side * (hw + 0.07), hoodY + 0.10, sb - 0.06, 0.012, 5);
                    Lump(ref c, black, T(side * (hw + 0.085), hoodY + 0.12, sb - 0.06), 0.03, 0.10, 0.03, 0.5);
                    if (c.L0) Decal(ref c, Glass(MirrorC), side * (hw + 0.085), hoodY + 0.12, sb - 0.092, 0, 0, -1, 0.05, 0.18, 0.02, 0.002);
                }
            if (c.L0)
                for (int k = -1; k <= 1; k += 2) OBar(ref c, black, k * 0.02, sy0 + 0.02, sb - 0.04, k * 0.02 - 0.55, sy0 + 0.08, sb - 0.10, 0.012, 0.012);

            // Roof: route board (micros), roof rack, beacon (ambulance).
            if (ambulance)
            {
                RBox(ref c, Plain(TrimC), 0, roof + 0.03, st - 0.25, 0.95, 0.04, 0.22, 0.015, 1);
                for (int k = -1; k <= 1; k += 2)
                    RBox(ref c, Glass(k < 0 ? MeshColor.FromHex(0xD32F2F) : MeshColor.FromHex(0x1E88E5)), k * 0.22, roof + 0.08, st - 0.25, 0.42, 0.07, 0.18, 0.03, 2);
                // Plain red "AMBULANCE" bands front and rear (no emblem).
                Decal(ref c, Paint(MeshColor.FromHex(0xD32F2F)), 0, roof - 0.06, zr - 0.012, 0, 0, -1, 0.9, 0.08, 0.02, 0.002);
            }
            else
            {
                // Devanagari route board on the roof front: cream board with red lettering bars.
                double bz = st - 0.10;
                RBox(ref c, Paint(p.Sign), 0, roof + 0.12, bz, 1.10, 0.22, 0.05, 0.02, 1);
                if (c.L1)
                    for (int k = 0; k < 2; k++)
                        Decal(ref c, Paint(MeshColor.FromHex(0xB71C1C)), 0, roof + 0.07 + 0.09 * k, bz + 0.027, 0, 0, 1, 0.90 - 0.2 * k, 0.04, 0.01, 0.002);
                for (int k = -1; k <= 1; k += 2) Rod(ref c, black, k * 0.45, roof, bz, k * 0.45, roof + 0.02, bz, 0.015, 4);
                if (c.L1 && (p.Index & 1) == 0)
                {
                    // Roof rack: two rails and cross bars.
                    for (int k = -1; k <= 1; k += 2) Rod(ref c, Plain(AlloyDarkC), k * (hw - 0.14), roof + 0.06, zr + 0.25, k * (hw - 0.14), roof + 0.06, st - 0.35, 0.015, 5);
                    for (int i = 0; i < 4; i++)
                    {
                        double z = zr + 0.30 + i * (st - 0.40 - zr - 0.30) / 3;
                        Rod(ref c, Plain(AlloyDarkC), -(hw - 0.14), roof + 0.06, z, hw - 0.14, roof + 0.06, z, 0.012, 4);
                    }
                }
            }
            TaperByZ(c.M, v0, wb + 0.30, zf, 0.94);
            CurveEnd(c.M, v0, zf - 0.30, zf, 0.12);
            SetPlates(0.50f, (float)zr - 0.012f, 0.40f, (float)zf + 0.012f);
        }

        // -----------------------------------------------------------------------------------------------------------
        // Buses: Sajha green (diesel / electric), minibus (Tata 709 type), long-distance coach, school bus
        // -----------------------------------------------------------------------------------------------------------

        private static void Bus(ref Ctx c, in VehicleCatalogEntry e, Dims d, in Livery p)
        {
            double zr = -d.Rear, zf = d.Wheelbase + d.Front, hw = d.Half, h = d.Height, wb = d.Wheelbase;
            bool mini = e.TrafficClass == Traffic.VehicleClass.Minibus, coach = e.HandlingPreset == (byte)HandlingPreset.Coach,
                 school = e.HandlingPreset == (byte)HandlingPreset.SchoolBus, electric = !mini && !coach && !school && c.Model == 1;
            bool hood = mini || coach || school; // front-engine, short hood ahead of the screen (Tata 709 / truck chassis)
            uint body = p.Body, band = p.Accent, roofC = p.Roof;
            ShapeBrush paint = Paint(body), black = Plain(TrimC);
            double roof = h - (coach ? 0.35 : electric ? 0.22 : 0.06);
            double floorY = electric ? 0.38 : 0.62, noseY = hood ? 0.95 : 0.85;
            double hoodLen = hood ? (mini ? 0.55 : 0.35) : 0, sb = zf - hoodLen - 0.06, st = sb - (hood ? 0.30 : 0.18);
            int v0 = c.M.VertexCount;
            Profile2 top = Prof2.Clear(false);
            top.Add(zr, roof - 0.08, true).Add(zr + 0.20, roof, false).Add(st, roof, false);
            if (hood) top.Add(sb, 1.35, false).Add(zf - 0.10, noseY + 0.15, false).Add(zf, noseY, false);
            else top.Add(sb, roof - 0.25, false).Add(zf, roof - 0.40, false);
            WheelSocket[] ws = Wheels(e);
            BoxBody(ref c, paint, top, zr, zf, electric ? 0.30 : 0.45, hw, 0.24, 0.06, ws, 0.06, hood ? 0.12 : 0.16, 0.14, 0.32, 0.38);
            OBox(ref c, Plain(UnderC, 0.3f), T(0, 0.70, 0.5 * wb), 2 * (hw - ws[0].Width - 0.05), 0.8, wb + 1.4);
            // Roof: a white or light cap on the green city bus.
            if (roofC != body) RoofSkinBox(c.M, v0, roof - 0.15, roofC);

            // Paint: lower band and window band (generic stripe layout, no operator livery), coach art bands.
            for (int side = -1; side <= 1; side += 2)
            {
                double x = side * (hw + 0.003), len = zf - zr - (hood ? 0.9 : 0.5), zc = 0.5 * (zr + zf) - (hood ? 0.35 : 0.1);
                Decal(ref c, Paint(band), x, floorY + 0.30, zc, side, 0, 0, len, school ? 0.14 : 0.20, 0.03, 0.003);
                if (!school) Decal(ref c, Paint(Shade(band, 0.75f)), x, floorY + 0.48, zc, side, 0, 0, len, 0.05, 0.02, 0.003);
                if (coach && c.L1)
                    ArtBand(ref c, x, side, floorY + 0.14, zc, len);
            }
            // Windows: driver's, passenger row, the front door (left, kerb side) and on the electric bus a centre door.
            double wy0 = Math.Max(floorY + 0.62, 1.20), wy1 = roof - 0.18;
            double frontWin = st - 0.10, doorZ = frontWin - 0.55;
            int panes = Math.Max(3, (int)Math.Round((frontWin - 1.0 - zr - 0.4) / 1.05));
            WindowRow(ref c, hw + 0.002, zr + 0.40, frontWin - 1.05, wy0, wy1, panes, 0.10, GlassC, true, true);
            for (int side = -1; side <= 1; side += 2)
            {
                double x = side * (hw + 0.006);
                if (side < 0)
                {
                    // Front door: tall glass with a frame.
                    Decal(ref c, Plain(TrimC), x, 0.5 * (floorY + 0.1 + wy1), doorZ, side, 0, 0, 0.86, wy1 - floorY - 0.06, 0.03, 0.003);
                    Panel(ref c, Glass(GlassC), x + side * 0.002, 0.5 * (floorY + 0.25 + wy1), doorZ, side, 0, 0, 0.36, wy1 - floorY - 0.30, 0.03, 0.006);
                    Panel(ref c, Glass(GlassC), x + side * 0.002, 0.5 * (floorY + 0.25 + wy1), doorZ, side, 0, 0, 0.78, 0.01, 0.003, 0.004);
                    if (electric)
                    {
                        double z2 = 0.5 * wb - 0.2;
                        Decal(ref c, Plain(TrimC), x, 0.5 * (floorY + 0.1 + wy1), z2, side, 0, 0, 1.10, wy1 - floorY - 0.06, 0.03, 0.003);
                        Panel(ref c, Glass(GlassC), x + side * 0.002, 0.5 * (floorY + 0.25 + wy1), z2, side, 0, 0, 1.0, wy1 - floorY - 0.30, 0.03, 0.006);
                    }
                }
                else Panel(ref c, Glass(GlassC), x + side * 0.002, 0.5 * (wy0 + wy1), frontWin - 0.4, side, 0, 0, 0.6, wy1 - wy0, 0.05, 0.008);
            }
            // Windscreen, destination display, rear window.
            double sy0, sy1, szb, szt, sny, snz, sl, sw;
            if (hood)
            {
                sy0 = 1.40;
                sy1 = roof - 0.22;
                szb = sb + 0.02;
                szt = st;
                sl = Math.Sqrt((szb - szt) * (szb - szt) + (sy1 - sy0) * (sy1 - sy0));
                snz = (sy1 - sy0) / sl;
                sny = (szb - szt) / sl;
                sw = 2 * hw - 0.26;
            }
            else
            {
                // Flat-fronted city buses: a tall screen filling the flat front face (inside its rounded edges: the face
                // ends 0.16 m in from the sides and 0.48 m under the roof line).
                sy0 = floorY + 0.55;
                sy1 = roof - 0.56;
                szb = szt = zf - 0.035;
                sl = sy1 - sy0;
                sny = 0;
                snz = 1;
                sw = 2 * hw - 0.50;
            }
            double scy = 0.5 * (sy0 + sy1), scz = 0.5 * (szb + szt) + 0.035;
            if (hood) Panel(ref c, Glass(ScreenC), 0, scy, scz, 0, sny, snz, sw, sl, 0.10, 0.01);
            else
            {
                // Flat-fronted city buses: the screen lies on the flat front face of the hull (the front is not curved in
                // plan, so the glass and the face stay one plane): a black rubber frame, the glass just proud of it, the
                // two-piece screen of the diesel split by a centre bar, the one-piece screen of the electric bus.
                Panel(ref c, Plain(TrimC), 0, scy, zf, 0, 0, 1, sw + 0.10, sl + 0.10, 0.12, 0.008);
                Panel(ref c, Glass(ScreenC), 0, scy, zf + 0.008, 0, 0, 1, sw, sl, 0.10, 0.01);
            }
            if (!electric && c.L1) OBox(ref c, Plain(TrimC), Facing(0, scy, scz + (hood ? 0.005 : 0.02), 0, sny, snz), 0.05, 0.012, sl);
            // Destination display over the screen.
            double dy = hood ? roof - 0.12 : roof - 0.32, dz = hood ? st + 0.02 : zf - 0.03, dny = hood ? 0.3 : 0;
            Panel(ref c, Plain(TrimC), 0, dy, dz, 0, dny, 1, 2 * hw - 0.50, 0.17, 0.03, 0.01);
            Decal(ref c, Glass(p.Sign), 0, dy, dz + 0.014, 0, dny, 1, 2 * hw - 0.70, 0.11, 0.02, 0.002);
            Panel(ref c, Glass(GlassC), 0, 0.5 * (wy0 + wy1), zr - 0.004, 0, 0, -1, 2 * hw - 0.40, wy1 - wy0, 0.06, 0.01);

            // Front: grille, lamps, bumper, wipers, mirrors on arms.
            if (hood)
            {
                Panel(ref c, Chrome(), 0, noseY - 0.18, zf + 0.004, 0, 0, 1, 1.05, 0.34, 0.05, 0.012);
                if (c.L1)
                    for (int k = 0; k < 4; k++) OBox(ref c, Plain(TrimC), T(0, noseY - 0.31 + 0.085 * k, zf + 0.02), 0.95, 0.03, 0.012);
                for (int k = -1; k <= 1; k += 2) Lamp(ref c, LensC, ChromeC, k * (hw - 0.28), noseY - 0.20, zf + 0.01, 0, 0, 1, 0.10, 0.04, 14);
                if (coach || mini) FrontArt(ref c, hw, noseY, zf, sb);
            }
            else
            {
                Panel(ref c, black, 0, 0.55, zf + 0.004, 0, 0, 1, 2 * hw - 0.40, 0.20, 0.05, 0.01);
                for (int k = -1; k <= 1; k += 2)
                {
                    Panel(ref c, Glass(LensC), k * (hw - 0.30), 0.62, zf + 0.004, 0, 0, 1, 0.34, 0.14, 0.05, 0.01);
                    if (electric && c.L1) Decal(ref c, Glass(DrlC), k * (hw - 0.30), 0.73, zf + 0.008, 0, 0, 1, 0.34, 0.03, 0.012, 0.003);
                }
            }
            OBox(ref c, hood ? Chrome() : Plain(TrimC), T(0, 0.38, zf - 0.02), 2 * hw - 0.04, 0.22, 0.12);
            OBox(ref c, Plain(TrimC), T(0, 0.42, zr + 0.04), 2 * hw - 0.04, 0.22, 0.12);
            for (int k = -1; k <= 1; k += 2)
            {
                Panel(ref c, Glass(TailC), k * (hw - 0.14), 0.95, zr - 0.004, 0, 0, -1, 0.16, 0.40, 0.04, 0.01);
                if (c.L1) Decal(ref c, Glass(AmberC), k * (hw - 0.14), 0.70, zr - 0.008, 0, 0, -1, 0.14, 0.08, 0.02, 0.004);
                if (c.L1)
                {
                    // "Rabbit-ear" mirrors on curved arms.
                    Path3 arm = P;
                    double ax = k * (hw - 0.05);
                    double az = hood ? st : zf - 0.30;
                    arm.Add(ax, roof - 0.30, az - 0.05).Add(k * (hw + 0.07), roof - 0.20, az + 0.15).Add(k * (hw + 0.09), roof - 0.45, az + 0.35);
                    Tube(ref c, black, arm, 0.016, 5);
                    Lump(ref c, black, T(k * (hw + 0.09), roof - 0.62, az + 0.36), 0.035, 0.16, 0.035, 0.6);
                }
            }
            if (c.L0)
                for (int k = -1; k <= 1; k += 2) OBar(ref c, black, k * 0.45, sy0 + 0.03, scz + 0.02, k * 0.45 - 0.5, sy0 + 0.25, scz + 0.02 - 0.2 * sny, 0.014, 0.014);

            // Roof equipment: battery pods (electric), roof rack with tarp-covered luggage and a ladder (coach).
            if (electric)
            {
                for (int k = 0; k < 2; k++)
                    RBox(ref c, Paint(MeshColor.FromHex(0xE8ECEF)), 0, roof + 0.10, 0.6 + 2.4 * k, 1.7, 0.20, 2.0, 0.08, 2);
            }
            else if (!coach && c.L1)
                RBox(ref c, Paint(MeshColor.FromHex(0xE8ECEF)), 0, roof + 0.06, 0.4 * wb, 1.1, 0.12, 1.2, 0.05, 2); // roof vent / AC
            if (coach)
            {
                double rz0 = zr + 0.4, rz1 = st - 0.3;
                for (int k = -1; k <= 1; k += 2) Rod(ref c, Metal(SteelC), k * (hw - 0.10), roof + 0.25, rz0, k * (hw - 0.10), roof + 0.25, rz1, 0.02, 5);
                if (c.L1)
                    for (int i = 0; i < 7; i++)
                    {
                        double z = rz0 + (rz1 - rz0) * i / 6;
                        for (int k = -1; k <= 1; k += 2) Rod(ref c, Metal(SteelC), k * (hw - 0.10), roof, z, k * (hw - 0.10), roof + 0.25, z, 0.015, 4);
                    }
                // Luggage under a blue tarp.
                RBox(ref c, Fabric(MeshColor.FromHex((p.Index & 1) == 0 ? 0x1565C0u : 0xEF6C00u)), 0, roof + 0.16, 0.5 * (rz0 + rz1) - 0.3, 2 * hw - 0.40, 0.30,
                     0.55 * (rz1 - rz0), 0.10, 2);
                if (c.L1)
                {
                    // Ladder at the back.
                    for (int k = -1; k <= 1; k += 2) Rod(ref c, Metal(SteelC), k * 0.20, 0.9, zr - 0.04, k * 0.20, roof + 0.25, zr - 0.04, 0.015, 4);
                    for (int i = 0; i < 6; i++) Rod(ref c, Metal(SteelC), -0.20, 1.0 + i * 0.30, zr - 0.04, 0.20, 1.0 + i * 0.30, zr - 0.04, 0.012, 4);
                }
            }
            if (school && c.L1)
            {
                // "SCHOOL BUS" boards front and rear (plain boards with dark lettering bars).
                RBox(ref c, Paint(MeshColor.FromHex(0xFFFFFF)), 0, roof + 0.10, st + 0.05, 1.2, 0.22, 0.04, 0.02, 1);
                Decal(ref c, Paint(MeshColor.FromHex(0x212121)), 0, roof + 0.10, st + 0.072, 0, 0, 1, 1.0, 0.06, 0.02, 0.002);
                Decal(ref c, Paint(MeshColor.FromHex(0x212121)), 0, roof - 0.30, zr - 0.012, 0, 0, -1, 1.0, 0.10, 0.02, 0.002);
            }
            // Mud flaps.
            if (c.L1)
                foreach (WheelSocket w in ws)
                    if (w.Z < 0.5 * wb) OBox(ref c, Rubber(TyreC), T(w.X, 0.28, w.Z - w.Radius - 0.12), w.Width, 0.40, 0.02);
            TaperByZ(c.M, v0, zf - 0.6, zf, hood ? 0.93 : 0.97);
            if (hood) CurveEnd(c.M, v0, zf - 0.40, zf, 0.10);
            SetPlates(0.55f, (float)zr - 0.012f, 0.42f, (float)zf + 0.012f);
        }

        /// <summary>Paints the vertices from <paramref name="from"/> above <paramref name="y"/> that face up (a roof cap).</summary>
        private static void RoofSkinBox(MeshData m, int from, double y, uint color)
        {
            for (int v = from; v < m.VertexCount; v++)
            {
                if (m.Positions[3 * v + 1] < y || m.Normals[3 * v + 1] < 0.5) continue;
                m.Colors[4 * v] = (byte)(color >> 24);
                m.Colors[4 * v + 1] = (byte)(color >> 16);
                m.Colors[4 * v + 2] = (byte)(color >> 8);
            }
        }

        /// <summary>Long-distance bus and truck art: a row of colourful painted panels (flowers, waves) on a side.</summary>
        private static readonly uint[] ArtC = { 0xD32F2F, 0xFBC02D, 0x1976D2, 0x388E3C, 0xE91E63, 0xFF8F00 };

        private static void ArtBand(ref Ctx c, double x, int side, double y, double zc, double len)
        {
            uint[] cols = ArtC;
            int n = Math.Max(3, (int)(len / 0.9));
            double w = len / n;
            for (int i = 0; i < n; i++)
            {
                double z = zc - 0.5 * len + (i + 0.5) * w;
                Decal(ref c, Paint(MeshColor.FromHex(cols[i % cols.Length])), x, y, z, side, 0, 0, w - 0.06, 0.14, 0.06, 0.004);
                if (c.L0)
                    Decal(ref c, Paint(MeshColor.FromHex(cols[(i + 2) % cols.Length])), x + side * 0.001, y, z, side, 0, 0, 0.12, 0.08, 0.04, 0.006, 0.785);
            }
        }

        /// <summary>The painted front of decorated buses and trucks: a visor board over the windscreen with lettering bars and
        /// flowers, a red reflector triangle on the grille.</summary>
        private static void FrontArt(ref Ctx c, double hw, double noseY, double zf, double sb)
        {
            if (!c.L1) return;
            Decal(ref c, Glass(MeshColor.FromHex(0xD32F2F)), 0, noseY + 0.03, zf + 0.018, 0, 0, 1, 0.20, 0.17, 0.02, 0.004, 0.0);
            for (int k = -1; k <= 1; k += 2)
                Decal(ref c, Paint(MeshColor.FromHex(0xFBC02D)), k * (hw - 0.45), noseY + 0.04, zf - 0.03, 0, 0.5, 0.86, 0.22, 0.10, 0.05, 0.004);
            _ = sb;
        }

        // -----------------------------------------------------------------------------------------------------------
        // Trucks: decorated Tata LPT type, tipper, water tanker
        // -----------------------------------------------------------------------------------------------------------

        private static readonly uint[] TruckPanelC = { 0xFF8F00, 0x00897B, 0xD32F2F, 0x1976D2, 0xFBC02D, 0x7B1FA2 };

        private static void Truck(ref Ctx c, in VehicleCatalogEntry e, Dims d, in Livery p)
        {
            double zr = -d.Rear, zf = d.Wheelbase + d.Front, hw = d.Half, h = d.Height, wb = d.Wheelbase, R = d.WheelR;
            bool tipper = e.Shape == BodyShape.Tipper, tanker = e.Shape == BodyShape.Tanker, painted = e.Shape == BodyShape.Truck;
            uint cab = tanker ? p.Accent : p.Body, accent = p.Accent;
            ShapeBrush paint = Paint(cab), black = Plain(TrimC);
            double cabLen = 1.75, cab0 = zf - cabLen, cabRoof = Math.Min(h - 0.35, 2.75), chassisY = R + 0.18;
            WheelSocket[] ws = Wheels(e);

            // ---- Chassis rails, fuel tank, battery box, rear bumper, mud flaps.
            for (int k = -1; k <= 1; k += 2)
                OBox(ref c, Plain(MeshColor.FromHex(0x2B2E33)), T(k * 0.42, chassisY, 0.5 * (zr + cab0) + 0.3), 0.10, 0.22, cab0 - zr + 0.3);
            if (c.L1)
            {
                Shapes.Cylinder(c.M, AlongZ(hw - 0.30, chassisY - 0.05, cab0 - 1.2), Metal(SteelC), 0.24, 0.9, 14, 0.04, 2, true, true, c.S);
                OBox(ref c, Plain(TrimC), T(-(hw - 0.28), chassisY - 0.05, cab0 - 0.9), 0.36, 0.30, 0.5);
                foreach (WheelSocket w in ws)
                    if (w.Z < wb * 0.5) OBox(ref c, Rubber(TyreC), T(w.X, R * 0.6, w.Z - R - 0.10), w.Width, 0.5, 0.02);
            }
            OBox(ref c, Plain(TrimC), T(0, R * 0.9, zr + 0.06), 2 * hw - 0.2, 0.14, 0.12);

            // ---- Cab: the rounded Tata front (a hull with arches over the front wheels), split windscreen, round lamps
            // in a chrome grille, painted bumper.
            // The tipper has the newer flat-fronted cab-over cab (an upright face, the screen nearly vertical); the
            // decorated truck and the tanker the classic rounded Tata front with its raked screen.
            Profile2 top = Prof2.Clear(false);
            double fy = 1.55, rake0 = tipper ? 0.03 : 0.10, rake1 = tipper ? 0.14 : 0.35;
            top.Add(cab0, cabRoof - 0.02, true).Add(cab0 + 0.4, cabRoof, false).Add(zf - rake1, cabRoof - 0.04, false).Add(zf - rake0, fy + 0.30, false)
               .Add(zf, fy, false);
            var front = new WheelSocket[2];
            int nf = 0;
            foreach (WheelSocket w in ws)
                if (w.Steers && nf < 2) front[nf++] = w;
            BoxBody(ref c, paint, top, cab0, zf, R + 0.12, hw, 0.22, 0.06, front, 0.06, 0.16, 0.04, R + 0.10, R + 0.3);
            // Split windscreen on the raked upper front: both panes lie in the plane of the cab's front slope (from
            // (zf - 0.10, fy + 0.30) up to (zf - 0.35, roof - 0.04)) inside its flat part (clear of the rounded corners), a
            // few millimetres proud, each in a black rubber frame; no sideways tilt or roll.
            double sy0 = fy + 0.36, sy1 = cabRoof - 0.18, slopeY0 = fy + 0.30, slopeY1 = cabRoof - 0.04, slopeZ0 = zf - rake0, slopeZ1 = zf - rake1;
            double sdz = slopeZ1 - slopeZ0, sdy = slopeY1 - slopeY0, slen = Math.Sqrt(sdz * sdz + sdy * sdy);
            double pny = -sdz / slen, pnz = sdy / slen, pcy = 0.5 * (sy0 + sy1), pcz = slopeZ0 + sdz * (pcy - slopeY0) / sdy;
            double paneH = (sy1 - sy0) * slen / sdy, paneX0 = 0.05, paneX1 = hw - 0.22 - 0.05, paneW = paneX1 - paneX0;
            for (int k = -1; k <= 1; k += 2)
            {
                double px = k * 0.5 * (paneX0 + paneX1);
                Panel(ref c, Plain(TrimC), px, pcy + pny * 0.002, pcz + pnz * 0.002, 0, pny, pnz, paneW + 0.05, paneH + 0.05, 0.07, 0.006);
                Panel(ref c, Glass(ScreenC), px, pcy + pny * 0.008, pcz + pnz * 0.008, 0, pny, pnz, paneW, paneH, 0.05, 0.008);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                double x = side * (hw + 0.003);
                Panel(ref c, Glass(GlassC), x, 0.5 * (sy0 + sy1), zf - 0.80, side, 0, 0, 0.62, sy1 - sy0, 0.05, 0.008);
                if (c.L1)
                {
                    Decal(ref c, Plain(Shade(cab, 0.6f)), x, 0.5 * (R + 0.4 + sy1), cab0 + 0.10, side, 0, 0, 0.01, sy1 - R - 0.3, 0.004, 0.003);
                    OBox(ref c, Chrome(), T(x + side * 0.008, 1.45, cab0 + 0.30), 0.016, 0.03, 0.14);
                    // Steps under the door.
                    OBox(ref c, Plain(SteelC), T(side * (hw - 0.12), R + 0.05, zf - 0.85), 0.24, 0.04, 0.40);
                }
                // Big mirror on an arm.
                if (c.L1)
                {
                    Rod(ref c, black, side * (hw - 0.02), cabRoof - 0.30, zf - 0.30, side * (hw + 0.06), cabRoof - 0.30, zf - 0.25, 0.014, 5);
                    Lump(ref c, black, T(side * (hw + 0.07), cabRoof - 0.48, zf - 0.25), 0.03, 0.20, 0.035, 0.6);
                }
            }
            // Grille, lamps, bumper: the Tata front's chrome grille with round lamps; the tipper's black slatted grille
            // band under the screen with rectangular lamps in the bumper corners.
            if (tipper)
            {
                Panel(ref c, black, 0, fy - 0.12, zf + 0.004, 0, 0, 1, 2 * hw - 0.60, 0.36, 0.04, 0.012);
                if (c.L1)
                    for (int k = 0; k < 4; k++) OBox(ref c, Plain(MeshColor.FromHex(0x4A4F55)), T(0, fy - 0.24 + 0.08 * k, zf + 0.018), 2 * hw - 0.70, 0.03, 0.012);
                for (int k = -1; k <= 1; k += 2)
                {
                    Panel(ref c, black, k * (hw - 0.28), R + 0.38, zf + 0.10, 0, 0, 1, 0.34, 0.16, 0.03, 0.012);
                    Panel(ref c, Glass(LensC), k * (hw - 0.28), R + 0.38, zf + 0.112, 0, 0, 1, 0.28, 0.11, 0.03, 0.008);
                    if (c.L1) Decal(ref c, Glass(AmberC), k * (hw - 0.07), R + 0.38, zf + 0.10, 0, 0, 1, 0.07, 0.11, 0.015, 0.012);
                }
            }
            else
            {
                Panel(ref c, Chrome(), 0, fy - 0.42, zf + 0.004, 0, 0, 1, 1.15, 0.50, 0.06, 0.012);
                if (c.L1)
                    for (int k = 0; k < 5; k++) OBox(ref c, Plain(TrimC), T(0, fy - 0.62 + 0.10 * k, zf + 0.02), 1.05, 0.035, 0.012);
                for (int k = -1; k <= 1; k += 2)
                    Lamp(ref c, LensC, ChromeC, k * (hw - 0.30), fy - 0.45, zf + 0.012, 0, 0, 1, 0.11, 0.04, 14);
            }
            uint bumperC = painted ? MeshColor.FromHex(TruckPanelC[(p.Index + 2) % TruckPanelC.Length]) : MeshColor.FromHex(0x37474F);
            RBox(ref c, Paint(bumperC), 0, R + 0.20, zf + 0.02, 2 * hw + 0.02, 0.26, 0.16, 0.04, 2);
            if (painted)
            {
                // Truck art on the cab: a red reflector triangle, painted visor board, stripes on the bumper, tassels.
                FrontArt(ref c, hw, fy - 0.42, zf + 0.004, zf);
                if (c.L1)
                {
                    for (int k = 0; k < 3; k++)
                        Decal(ref c, Paint(MeshColor.FromHex(TruckPanelC[(p.Index + k) % TruckPanelC.Length])), 0, R + 0.12 + 0.07 * k, zf + 0.102, 0, 0, 1,
                              2 * hw - 0.2, 0.03, 0.01, 0.002);
                    // Tassels hanging from the bumper.
                    if (c.L0)
                        for (int i = 0; i < 9; i++)
                        {
                            double x = -hw + 0.15 + i * (2 * hw - 0.3) / 8;
                            Cone(ref c, Fabric(MeshColor.FromHex(TruckPanelC[i % TruckPanelC.Length])), x, R + 0.06, zf + 0.10, x, R - 0.10, zf + 0.10, 0.012, 0.03, 6);
                        }
                }
            }
            else if (c.L1)
            {
                Decal(ref c, Glass(MeshColor.FromHex(0xD32F2F)), 0, fy - 0.10, zf - 0.02, 0, 0.3, 1, 0.16, 0.14, 0.02, 0.004);
            }

            // ---- Load body by type.
            double bed0 = zr, bed1 = cab0 - 0.08, deck = chassisY + 0.16;
            if (tanker)
            {
                // Elliptical water tank on cradles, manhole, ladder, rear valve, a cream lettering band.
                double ty = deck + 0.62, len = bed1 - bed0 - 0.2;
                HullBegin();
                int ring = c.L0 ? 8 : 4;
                for (int i = 0; i <= ring; i++)
                {
                    double ph = 0.5 * Math.PI * i / ring, inset = 0.12 * (1 - Math.Sin(ph));
                    HullAdd(bed0 + 0.1 + 0.12 * (1 - Math.Cos(ph)), 0, ty, hw - 0.08 - inset, 0.62 - inset, 0.55, 0.55);
                }
                for (int i = ring; i >= 0; i--)
                {
                    double ph = 0.5 * Math.PI * i / ring, inset = 0.12 * (1 - Math.Sin(ph));
                    HullAdd(bed1 - 0.1 - 0.12 * (1 - Math.Cos(ph)), 0, ty, hw - 0.08 - inset, 0.62 - inset, 0.55, 0.55);
                }
                HullEmit(ref c, Paint(p.Body), 4, true, true);
                for (int side = -1; side <= 1; side += 2)
                {
                    Decal(ref c, Paint(p.Sign == 0 ? MeshColor.FromHex(0xFFF3E0) : p.Sign), side * (hw - 0.075), ty + 0.05, 0.5 * (bed0 + bed1), side, 0, 0, len - 0.6, 0.30,
                          0.08, 0.004);
                    if (c.L1)
                        for (int k = 0; k < 2; k++)
                            Decal(ref c, Paint(MeshColor.FromHex(0xC62828)), side * (hw - 0.071), ty + 0.12 - 0.12 * k, 0.5 * (bed0 + bed1), side, 0, 0, len - 1.0 - 0.6 * k, 0.05,
                                  0.02, 0.004);
                    Decal(ref c, Paint(Shade(p.Body, 0.7f)), side * (hw - 0.07), ty - 0.32, 0.5 * (bed0 + bed1), side, 0, 0, len - 0.4, 0.06, 0.02, 0.004);
                }
                if (c.L1)
                {
                    Cyl(ref c, Metal(SteelC), 0, ty + 0.55, 0.5 * (bed0 + bed1), 0, ty + 0.70, 0.5 * (bed0 + bed1), 0.22, 14, 0.02);
                    for (int k = -1; k <= 1; k += 2) Rod(ref c, Metal(SteelC), k * 0.18, deck, bed0 + 0.04, k * 0.18, ty + 0.55, bed0 + 0.06, 0.015, 4);
                    Cyl(ref c, Metal(SteelC), 0, ty - 0.45, bed0 + 0.12, 0, ty - 0.45, bed0 - 0.08, 0.06, 10);
                    for (int i = 0; i < 2; i++) OBox(ref c, Plain(TrimC), T(0, deck + 0.04, bed0 + 0.8 + i * (len - 1.4)), 2 * hw - 0.3, 0.12, 0.14);
                }
            }
            else if (tipper)
            {
                // Steel tipper box (ref_vehicles.md §4.2, photos refs/vehicles/c_tipper): a rectangular box whose rear
                // bottom corner is cut away, box-section ribs and a top rail along the sides, a tall front wall that carries
                // the canopy over the cab roof, a top-hinged tailgate, the hoist ram under the front; a heap of sand inside
                // (the valley's tippers haul sand and gravel).
                // The box rides on a subframe over the chassis; its floor rises toward the tail (the "boat" bottom of a
                // tipper body), so the side is a long trapezoid, not a plank wall.
                double sub = deck + 0.16, top0 = Math.Min(h - 0.05, sub + 1.15), b0 = bed0 + 0.05, b1 = bed1, wall = 0.07, wallTop = cabRoof + 0.12;
                uint boxC = accent;
                ShapeBrush box = Paint(boxC), rib = Paint(Shade(boxC, 0.82f));
                for (int k = -1; k <= 1; k += 2) OBox(ref c, Plain(MeshColor.FromHex(0x2B2E33)), T(k * 0.42, deck + 0.08, 0.5 * (b0 + b1)), 0.12, 0.16, b1 - b0 - 0.2);
                double rise = 0.42, flat0 = b0 + 1.3;
                deck = sub;
                Slab(ref c, box, 0, deck + 0.05, 0.5 * (flat0 + b1), 2 * hw - 0.04, 0.10, b1 - flat0, 0.02, 1);
                Profile2 sp = Prof2.Clear(true);
                sp.Add(flat0, deck, true).Add(b1, deck, true).Add(b1, top0, true).Add(b0, top0, true).Add(b0, deck + rise, true);
                for (int k = -1; k <= 1; k += 2)
                    SideExtrude(ref c, box, sp, k < 0 ? -hw : hw - wall, k < 0 ? -hw + wall : hw, 0.02, 1);
                // Front wall up to the canopy; the canopy reaches forward over the cab roof.
                Slab(ref c, box, 0, 0.5 * (deck + wallTop), b1 - 0.05, 2 * hw, wallTop - deck, 0.10, 0.03, 1);
                Slab(ref c, box, 0, wallTop - 0.03, b1 + 0.42, 2 * hw - 0.06, 0.06, 0.94, 0.02, 1);
                // The rising floor under the tail, and the tailgate a little proud of the sides' rear edge, hinged at the top.
                Panel(ref c, box, 0, deck + 0.5 * rise, 0.5 * (b0 + flat0), 0, -(flat0 - b0), -rise, 2 * hw - 0.04, Math.Sqrt(rise * rise + (flat0 - b0) * (flat0 - b0)), 0.02, 0.06);
                Slab(ref c, box, 0, 0.5 * (deck + rise + top0 + 0.04), b0 - 0.03, 2 * hw + 0.02, top0 + 0.04 - deck - rise, 0.08, 0.02, 1);
                if (c.L1)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        double x = side * (hw + 0.02);
                        // Vertical box ribs, the top rail and a mid rail.
                        for (int i = 0; i < 6; i++)
                        {
                            double z = b0 + 0.55 + i * (b1 - b0 - 0.75) / 5;
                            OBox(ref c, rib, T(x, 0.5 * (deck + top0), z), 0.05, top0 - deck, 0.07);
                        }
                        OBox(ref c, rib, T(side * (hw + 0.025), top0 - 0.04, 0.5 * (b0 + b1)), 0.07, 0.09, b1 - b0 + 0.02);
                        OBox(ref c, rib, T(side * (hw + 0.012), deck + 0.55, 0.5 * (b0 + b1) + 0.2), 0.04, 0.06, b1 - b0 - 0.5);
                        // Hinge eyes of the tailgate.
                        Cyl(ref c, Metal(SteelC), side * (hw - 0.10), top0 + 0.02, b0 - 0.06, side * (hw + 0.03), top0 + 0.02, b0 - 0.06, 0.04, 8);
                    }
                    // Canopy ribs underneath and the tailgate's latch bar.
                    for (int i = 0; i < 3; i++) OBox(ref c, rib, T(-0.6 + 0.6 * i, wallTop - 0.09, b1 + 0.42), 0.06, 0.08, 0.90);
                    OBox(ref c, Plain(TrimC), T(0, deck + rise + 0.12, b0 - 0.08), 2 * hw - 0.3, 0.05, 0.04);
                    RBox(ref c, Metal(SteelC), 0, deck + 0.3, b1 + 0.02, 0.18, 0.6, 0.18, 0.04, 1); // hoist ram
                    // The load: a heap of sand just over the rim.
                    Ell(ref c, new ShapeBrush(MeshColor.FromHex(0xB59B74), MaterialChannel.Dirt, 0.9f), 0, top0 - 0.20, 0.5 * (b0 + b1) + 0.1, hw - wall - 0.02, 0.32,
                        0.5 * (b1 - b0) - 0.25, 0.6, 0.5, c.L0 ? 14 : 8);
                }
            }
            else
            {
                // Decorated cargo body: plank sides in panel colours, a tall painted "crown" headboard over the cab roof.
                double top0 = deck + 1.30, crown = Math.Min(h * 1.1, 3.55);
                uint panelC = MeshColor.FromHex(TruckPanelC[p.Index % TruckPanelC.Length]), plankC = MeshColor.FromHex(0xA0522D);
                Slab(ref c, Paint(plankC), 0, deck + 0.05, 0.5 * (bed0 + bed1), 2 * hw, 0.10, bed1 - bed0, 0.02, 1);
                for (int k = -1; k <= 1; k += 2)
                    Slab(ref c, Paint(plankC), k * (hw - 0.04), 0.5 * (deck + top0), 0.5 * (bed0 + bed1), 0.08, top0 - deck, bed1 - bed0, 0.02, 1);
                Slab(ref c, Paint(plankC), 0, 0.5 * (deck + top0), bed0 + 0.04, 2 * hw, top0 - deck, 0.08, 0.02, 1);
                // Headboard: the cargo body's front wall rises over the cab roof and carries the canopy forward to the
                // crown, the painted board over the windscreen (ref photos refs/vehicles/truck_art 01, tata_truck 00/05).
                double crownZ = zf - 0.40, cb0 = cabRoof - 0.06;
                RBox(ref c, Paint(panelC), 0, 0.5 * (deck + crown - 0.25), bed1 - 0.05, 2 * hw, crown - 0.25 - deck, 0.10, 0.03, 2);
                Slab(ref c, Paint(panelC), 0, cabRoof + 0.10, 0.5 * (bed1 + crownZ), 2 * hw - 0.06, 0.08, crownZ - bed1, 0.02, 1);
                TruckCrown(ref c, hw, cb0, crown, crownZ, p.Index);
                if (c.L1)
                {
                    // Coloured bands and art panels on the plank sides.
                    for (int side = -1; side <= 1; side += 2)
                    {
                        double x = side * (hw + 0.002);
                        for (int k = 0; k < 3; k++)
                            Decal(ref c, Paint(MeshColor.FromHex(TruckPanelC[(p.Index + k + 1) % TruckPanelC.Length])), x, deck + 0.25 + 0.38 * k,
                                  0.5 * (bed0 + bed1), side, 0, 0, bed1 - bed0 - 0.1, 0.10, 0.02, 0.003);
                        ArtBand(ref c, x + side * 0.002, side, deck + 0.82, 0.5 * (bed0 + bed1), bed1 - bed0 - 0.5);
                    }
                    HornPlease(ref c, hw, deck, bed0, p.Index);
                    for (int k = -1; k <= 1; k += 2)
                        Panel(ref c, Glass(TailC), k * (hw - 0.20), R * 0.9 + 0.18, zr - 0.01, 0, 0, -1, 0.20, 0.12, 0.03, 0.01);
                }
            }
            if (!painted)
                for (int k = -1; k <= 1; k += 2)
                    Panel(ref c, Glass(TailC), k * (hw - 0.20), R * 0.9 + 0.12, zr - 0.006, 0, 0, -1, 0.20, 0.12, 0.03, 0.01);
            // The Tata-type cab is flat in plan with big rounded corners (the hull's end rounding): no plan curve, so the
            // windscreen, grille and lamps stay in the planes of the faces they sit on.
            SetPlates((float)(R * 0.9 + 0.18), (float)zr - 0.02f, (float)(R + 0.20), (float)zf + 0.11f);
        }

        /// <summary>
        /// The painted crown of a decorated truck: an arched board over the windscreen as wide as the cab (shoulders
        /// stepping up to a centre peak), facing forward at <paramref name="z"/>, painted with a multicolour border, two
        /// cream name boards with red script (a headline and strokes, no real words) either side of a gold medallion, small
        /// amber lamps on its corners and, at LOD0, a fringe of coloured tassels along its lower edge.
        /// </summary>
        private static void TruckCrown(ref Ctx c, double hw, double y0, double y1, double z, int index)
        {
            uint baseC = MeshColor.FromHex(TruckPanelC[(index + 3) % TruckPanelC.Length]);
            Profile2 pp = Prof2.Clear(true);
            double ys = y1 - 0.26;
            pp.Add(hw, y0, true).Add(hw, ys, true).Add(hw - 0.14, ys + 0.05, true).Add(0.75, ys + 0.10, false).Add(0.36, y1 - 0.04, false).Add(0, y1, true)
              .Add(-0.36, y1 - 0.04, false).Add(-0.75, ys + 0.10, false).Add(-(hw - 0.14), ys + 0.05, true).Add(-hw, ys, true).Add(-hw, y0, true);
            FrontExtrude(ref c, Paint(baseC), pp, z - 0.08, z, 0.02, 1);
            double fz = z + 0.003;
            // Border bands along the bottom edge (over the screen) and the name boards.
            for (int k = 0; k < 3; k++)
                Decal(ref c, Paint(MeshColor.FromHex(ArtC[(index + k) % ArtC.Length])), 0, y0 + 0.05 + 0.05 * k, fz, 0, 0, 1, 2 * hw - 0.10, 0.04, 0.01, 0.003);
            double ny = 0.5 * (y0 + 0.20 + ys);
            for (int side = -1; side <= 1; side += 2)
            {
                double nx = side * 0.62;
                Decal(ref c, Paint(MeshColor.FromHex(0xFFF3D6)), nx, ny, fz, 0, 0, 1, 0.74, 0.26, 0.04, 0.004);
                if (c.L1)
                {
                    // Script: a red headline with letter strokes hanging from it.
                    Decal(ref c, Paint(MeshColor.FromHex(0xC62828)), nx, ny + 0.06, fz, 0, 0, 1, 0.58, 0.022, 0.005, 0.006);
                    if (c.L0)
                        for (int i = 0; i < 5; i++)
                        {
                            double lx = nx - 0.24 + 0.12 * i;
                            Decal(ref c, Paint(MeshColor.FromHex(0xC62828)), lx, ny - 0.01, fz, 0, 0, 1, 0.022, 0.13, 0.005, 0.006);
                            Decal(ref c, Paint(MeshColor.FromHex(0xC62828)), lx - 0.035, ny - 0.02, fz, 0, 0, 1, 0.05, 0.022, 0.008, 0.006, 0.6);
                        }
                }
                // Amber lamps on the board's corners.
                if (c.L1) Lamp(ref c, AmberC, ChromeC, side * (hw - 0.10), ys - 0.04, z + 0.004, 0, 0, 1, 0.045, 0.03, 10);
            }
            // Gold medallion with a blue centre.
            Decal(ref c, Paint(MeshColor.FromHex(0xF9A825)), 0, ny + 0.02, fz, 0, 0, 1, 0.30, 0.30, 0.15, 0.004);
            if (c.L1) Decal(ref c, Paint(MeshColor.FromHex(0x1565C0)), 0, ny + 0.02, fz, 0, 0, 1, 0.18, 0.18, 0.09, 0.006);
            if (c.L0)
                for (int i = 0; i < 15; i++)
                {
                    double x = -hw + 0.12 + i * (2 * hw - 0.24) / 14;
                    Cone(ref c, Fabric(MeshColor.FromHex(TruckPanelC[(i + index) % TruckPanelC.Length])), x, y0 + 0.01, z + 0.02, x, y0 - 0.10, z + 0.03, 0.010, 0.022, 6);
                }
        }

        /// <summary>
        /// The "HORN PLEASE" tailgate of a decorated truck (ref photo refs/vehicles/horn_please 00): a painted board in a
        /// strong colour, the words in cream stroke capitals between flower roundels at LOD0 (cream bars at LOD1).
        /// </summary>
        private static void HornPlease(ref Ctx c, double hw, double deck, double bed0, int index)
        {
            uint board = MeshColor.FromHex(index % 2 == 0 ? 0xE65100u : 0xC62828u), cream = MeshColor.FromHex(0xFFF3D6);
            double y = deck + 0.72, z = bed0 - 0.004;
            Decal(ref c, Paint(board), 0, y, z, 0, 0, -1, 2 * hw - 0.16, 0.56, 0.04, 0.002);
            if (c.L0)
            {
                Text(c.M, "HORN PLEASE", (float)(z - 0.004), false, (float)y, (float)(2 * hw - 0.70), 0.30f, cream);
                for (int side = -1; side <= 1; side += 2)
                {
                    double fx = side * (hw - 0.22);
                    Decal(ref c, Paint(MeshColor.FromHex(0xF9A825)), fx, y, z - 0.004, 0, 0, -1, 0.22, 0.22, 0.11, 0.002);
                    Decal(ref c, Paint(MeshColor.FromHex(0x2E7D32)), fx, y, z - 0.006, 0, 0, -1, 0.10, 0.10, 0.05, 0.002);
                }
            }
            else
                for (int k = 0; k < 2; k++)
                    Decal(ref c, Paint(cream), k == 0 ? -0.45 : 0.40, y, z - 0.004, 0, 0, -1, k == 0 ? 0.60 : 0.85, 0.20, 0.02, 0.002);
        }

        // -----------------------------------------------------------------------------------------------------------
        // Safa tempo (electric three-wheeler)
        // -----------------------------------------------------------------------------------------------------------

        /// <summary>
        /// The Safa tempo (ref_vehicles.md §3.2; photos refs/vehicles/c_safa, safa2): one cream box on three wheels, the
        /// driver's cab a little narrower than the passenger box behind it. The near-vertical front is mostly a big flat
        /// windscreen in a black rubber frame under a rounded roof edge with the route board; below it the cream front
        /// panel with the green belt stripe, two round headlamps low in its corners, amber indicators and the plate; the
        /// single front wheel stands under it on a short fork with a green mudguard, out in the open from the side. Open
        /// doorways either side of the cab, three windows a side on the passenger box, green belt stripes, the open rear
        /// entry with a step and a grab rail.
        /// </summary>
        private static void Tempo(ref Ctx c, in VehicleCatalogEntry e, Dims d, in Livery p)
        {
            double zr = -d.Rear, zf = d.Wheelbase + d.Front, hw = d.Half, h = d.Height, wb = d.Wheelbase, R = d.WheelR;
            uint body = p.Body, green = p.Accent, skirt = p.Trim;
            ShapeBrush paint = Paint(body), black = Plain(TrimC), dark = Plain(MeshColor.FromHex(0x232A30), 0.5f);
            double cab0 = zf - 1.15, floorY = R + 0.22, roof = h - 0.04, sill = floorY - 0.10, cabNarrow = 0.92;
            int v0 = c.M.VertexCount;
            WheelSocket[] ws = Wheels(e);

            // ---- One body hull from the rear step to the flat front: arches over the rear wheels and over the front
            // wheel (open under the cab), a flat roof rounding over at the front into the windscreen header.
            Profile2 top = Prof2.Clear(false);
            top.Add(zr, roof - 0.03, true).Add(zr + 0.10, roof, false).Add(zf - 0.14, roof, false).Add(zf, roof - 0.07, true);
            // The front panel ends a little above the front tyre so the wheel and its mudguard show under it.
            BoxBody(ref c, paint, top, zr, zf, sill, hw, 0.10, 0.04, ws, 0.06, 0.07, 0.05, 2 * R - 0.06, sill);
            // Everything is placed at the full width; the driver's cab narrows toward the front at the end (TaperByZ), so
            // the screen, lamps and the doorways follow the narrowing body.
            double cw = hw, zfF = zf + 0.004;

            // ---- Front: windscreen in a black rubber frame, wipers, route board on the roof edge.
            double sy0 = floorY + 0.52, sy1 = roof - 0.13, sw = 2 * cw - 0.16;
            Panel(ref c, black, 0, 0.5 * (sy0 + sy1), zfF, 0, 0, 1, sw + 0.07, sy1 - sy0 + 0.07, 0.07, 0.006);
            Panel(ref c, Glass(ScreenC), 0, 0.5 * (sy0 + sy1), zfF + 0.006, 0, 0, 1, sw, sy1 - sy0, 0.05, 0.008);
            if (c.L0)
                for (int k = -1; k <= 1; k += 2)
                    OBar(ref c, black, k * 0.06, sy0 + 0.03, zfF + 0.016, k * 0.06 - 0.40, sy0 + 0.08, zfF + 0.016, 0.012, 0.010);
            if (c.L1)
            {
                RBox(ref c, Paint(p.Sign), 0, roof + 0.07, zf - 0.20, 0.92, 0.15, 0.05, 0.02, 1);
                Decal(ref c, Paint(MeshColor.FromHex(0xB71C1C)), 0, roof + 0.07, zf - 0.172, 0, 0, 1, 0.72, 0.05, 0.02, 0.002);
                for (int k = -1; k <= 1; k += 2) Rod(ref c, black, k * 0.38, roof - 0.01, zf - 0.20, k * 0.38, roof + 0.01, zf - 0.20, 0.012, 4);
            }
            // Front panel: green belt stripe across, two round lamps low in the corners, indicators, a centre vent.
            Decal(ref c, Paint(green), 0, floorY + 0.38, zfF, 0, 0, 1, 2 * cw - 0.06, 0.08, 0.02, 0.003);
            if (c.L1) Decal(ref c, Paint(green), 0, floorY + 0.47, zfF, 0, 0, 1, 2 * cw - 0.06, 0.025, 0.01, 0.003);
            for (int k = -1; k <= 1; k += 2)
            {
                Lamp(ref c, LensC, ChromeC, k * (cw - 0.20), floorY + 0.15, zfF, 0, 0, 1, 0.075, 0.04, 14);
                if (c.L1) Decal(ref c, Glass(AmberC), k * (cw - 0.08), floorY + 0.15, zfF, 0, 0, 1, 0.05, 0.07, 0.015, 0.004);
            }
            // ---- Front wheel: a short fork from under the cab, a green mudguard over the tyre.
            WheelSocket fw = ws[2];
            FenderArc(ref c, Paint(green), 0, R, fw.Z, R + 0.05, fw.Width + 0.10, 0.02, 10, 150, 18);
            for (int k = -1; k <= 1; k += 2)
                Rod(ref c, black, k * (0.5 * fw.Width + 0.03), 2 * R + 0.12, fw.Z - 0.06, k * (0.5 * fw.Width + 0.03), R, fw.Z, 0.022, 6);
            if (c.L1) OBox(ref c, black, T(0, 2 * R + 0.12, fw.Z - 0.06), fw.Width + 0.12, 0.05, 0.10); // fork crown

            // ---- Sides: belt stripes and the dark skirt; the open doorway of the cab; three windows on the passenger box.
            for (int side = -1; side <= 1; side += 2)
            {
                double x = side * (hw + 0.003), xc = side * (cw + 0.004);
                Decal(ref c, Paint(skirt), x, sill + 0.05, 0.5 * (zr + cab0), side, 0, 0, cab0 - zr - 0.25, 0.08, 0.02, 0.003);
                Decal(ref c, Paint(green), x, floorY + 0.38, 0.5 * (zr + cab0), side, 0, 0, cab0 - zr - 0.12, 0.08, 0.02, 0.003);
                if (c.L1) Decal(ref c, Paint(green), x, floorY + 0.29, 0.5 * (zr + cab0), side, 0, 0, cab0 - zr - 0.12, 0.025, 0.01, 0.003);
                Decal(ref c, Paint(green), xc, floorY + 0.38, zf - 0.25, side, 0, 0, 0.36, 0.08, 0.02, 0.003);
                // Doorway: a dark opening with a chrome grab handle at its back edge.
                double dz0 = cab0 + 0.12, dz1 = zf - 0.45;
                Decal(ref c, dark, xc, 0.5 * (floorY + roof - 0.12), 0.5 * (dz0 + dz1), side, 0, 0, dz1 - dz0, roof - 0.12 - floorY, 0.06, 0.004);
                if (c.L1) Rod(ref c, Chrome(), side * (cw + 0.02), floorY + 0.25, dz0 + 0.04, side * (cw + 0.02), roof - 0.30, dz0 + 0.04, 0.013, 5);
                // A small fixed window ahead of the doorway, beside the windscreen.
                Panel(ref c, Glass(GlassC), xc, 0.5 * (sy0 + sy1), zf - 0.24, side, 0, 0, 0.30, sy1 - sy0, 0.04, 0.006);
            }
            WindowRow(ref c, hw + 0.002, zr + 0.20, cab0 - 0.08, floorY + 0.55, roof - 0.18, 3, 0.10, MeshColor.FromHex(0x2A3640));
            if (c.L0)
            {
                // Roof luggage lip along both edges.
                for (int k = -1; k <= 1; k += 2) OBox(ref c, Plain(SteelC), T(k * (hw - 0.06), roof + 0.025, 0.5 * (zr + cab0)), 0.025, 0.05, cab0 - zr - 0.2);
            }

            // ---- Rear: the open entry with a step and a vertical grab rail; tail lamps; plate under the step.
            Decal(ref c, dark, 0, 0.5 * (floorY + roof - 0.10), zr - 0.004, 0, 0, -1, 2 * hw - 0.30, roof - 0.10 - floorY, 0.05, 0.003);
            OBox(ref c, Plain(SteelC), T(0, sill - 0.04, zr - 0.10), 2 * hw - 0.3, 0.04, 0.22);
            if (c.L1)
            {
                Rod(ref c, Chrome(), -0.12, floorY, zr - 0.02, -0.12, roof - 0.15, zr - 0.02, 0.016, 5);
                for (int k = -1; k <= 1; k += 2) Panel(ref c, Glass(TailC), k * (hw - 0.08), floorY + 0.15, zr - 0.006, 0, 0, -1, 0.07, 0.16, 0.02, 0.008);
                // Mirrors on arms from the cab corners.
                for (int k = -1; k <= 1; k += 2)
                {
                    Rod(ref c, black, k * (cw - 0.02), sy0 + 0.05, zf - 0.12, k * (cw + 0.09), sy0 + 0.15, zf - 0.10, 0.01, 4);
                    Lump(ref c, black, T(k * (cw + 0.10), sy0 + 0.18, zf - 0.10), 0.05, 0.07, 0.02, 0.6);
                }
            }
            // The driver's cab narrows toward the front (the passenger box is the wider part), and its corners curve back a
            // little in plan (the face stays flat where the screen sits).
            TaperByZ(c.M, v0, cab0, zf, cabNarrow);
            CurveEnd(c.M, v0, zf - 0.20, zf, 0.04);
            SetPlates((float)(sill - 0.17), (float)zr - 0.215f, (float)(floorY + 0.26), (float)zf + 0.012f);
        }

        // -----------------------------------------------------------------------------------------------------------
        // Tractor (Mahindra 575 type)
        // -----------------------------------------------------------------------------------------------------------

        private static void Tractor(ref Ctx c, in VehicleCatalogEntry e, Dims d, in Livery p)
        {
            double zf = d.Wheelbase + d.Front, hw = d.Half, wb = d.Wheelbase;
            WheelSocket[] ws = Wheels(e);
            double rr = ws[0].Radius, rf = ws[2].Radius;
            uint body = p.Body;
            ShapeBrush paint = Paint(body), black = Plain(TrimC), grey = Plain(MeshColor.FromHex(0x9EA4AA)), steel = Metal(SteelC);
            // Bonnet: a hull from the dashboard to the grille, rounded top, on the engine block.
            HullBegin();
            double b0 = 0.55, b1 = zf - 0.02;
            int nb = c.L0 ? 6 : 3;
            for (int i = 0; i <= nb; i++)
            {
                double t = (double)i / nb, z = b0 + (b1 - b0) * t, topY = 1.38 - 0.10 * t;
                double inset = i == nb ? 0.04 : 0;
                HullAdd(z, 0, 0.5 * (topY + 0.78), 0.30 - inset, 0.5 * (topY - 0.78) - inset, 0.12, 0.03);
            }
            HullEmit(ref c, paint, 3, true, true);
            // Engine block and front axle under the bonnet; grille with lamps; front weight frame.
            OBox(ref c, Plain(MeshColor.FromHex(0x37474F)), T(0, 0.62, 0.5 * (b0 + b1)), 0.44, 0.40, b1 - b0 - 0.1);
            Panel(ref c, grey, 0, 1.06, b1 + 0.002, 0, 0, 1, 0.48, 0.48, 0.05, 0.01);
            if (c.L1)
                for (int k = 0; k < 6; k++) OBox(ref c, Plain(TrimC), T(0, 0.88 + 0.07 * k, b1 + 0.015), 0.40, 0.025, 0.01);
            for (int k = -1; k <= 1; k += 2) Lamp(ref c, LensC, ChromeC, k * 0.21, 1.22, b1 - 0.02, k * 0.3, 0, 1, 0.055, 0.04, 12);
            RBox(ref c, Plain(TrimC), 0, rf + 0.05, wb + 0.10, 2 * hw - 0.55, 0.10, 0.12, 0.03, 1);
            OBox(ref c, Plain(TrimC), T(0, 0.55, zf - 0.04), 0.70, 0.30, 0.10);
            // Exhaust stack with a curved rain cap, air intake.
            Cyl(ref c, black, 0.18, 1.30, b1 - 0.45, 0.18, 2.02, b1 - 0.45, 0.04, 10);
            if (c.L1)
            {
                Path3 ex = P;
                ex.Add(0.18, 2.0, b1 - 0.45).Add(0.18, 2.07, b1 - 0.42).Add(0.18, 2.06, b1 - 0.36);
                Tube(ref c, black, ex, 0.04, 8);
                Cyl(ref c, black, -0.18, 1.32, b1 - 0.55, -0.18, 1.68, b1 - 0.55, 0.05, 10);
            }
            // Big rear fenders over the drive wheels with lamps; platform; seat with backrest; steering wheel.
            for (int s = -1; s <= 1; s += 2)
            {
                FenderArc(ref c, paint, ws[s < 0 ? 0 : 1].X, rr, 0, rr + 0.06, ws[0].Width + 0.10, 0.02, 15, 150, 24);
                if (c.L1) Lamp(ref c, LensC, TrimC, s * (hw - 0.05), rr * 1.55, rr * 0.45, 0, 0.3, 1, 0.045, 0.03, 10);
            }
            OBox(ref c, Plain(MeshColor.FromHex(0x37474F)), T(0, 0.86, 0.15), 2 * hw - 2 * ws[0].Width - 0.1, 0.06, 1.0);
            RBox(ref c, Plain(MeshColor.FromHex(0x37474F)), 0, 0.70, 0.05, 0.50, 0.40, 0.70, 0.06, 2); // gearbox / rear axle housing
            RBox(ref c, Leather(SeatC), 0, 1.30, 0.12, 0.42, 0.10, 0.40, 0.04, 2);
            RBox(ref c, Leather(SeatC), 0, 1.50, -0.08, 0.42, 0.35, 0.08, 0.04, 2);
            Cyl(ref c, steel, 0, 0.95, 0.10, 0, 1.25, 0.12, 0.04, 8);
            Cyl(ref c, black, 0, 1.30, b0 - 0.05, 0, 1.62, b0 - 0.22, 0.025, 8);
            Shapes.Torus(c.M, Pitched(0, 1.64, b0 - 0.24, -0.55), Plain(TrimC), 0.19, 0.018, 20, 6, c.S);
            if (c.L1)
            {
                // Dashboard, levers, hitch, a red tow hook; ROPS-free open platform.
                RBox(ref c, paint, 0, 1.36, b0 - 0.02, 0.62, 0.20, 0.12, 0.04, 1);
                for (int k = -1; k <= 1; k += 2) Rod(ref c, Plain(SteelC), k * 0.12, 1.0, 0.30, k * 0.16, 1.40, 0.36, 0.012, 4);
                OBox(ref c, Plain(SteelC), T(0, 0.48, -0.55), 0.12, 0.08, 0.40);
                Rod(ref c, Plain(TrimC), -0.10, 0.48, -0.70, 0.10, 0.48, -0.70, 0.03, 6);
                Decal(ref c, Paint(MeshColor.FromHex(0xF5F5F5)), 0, 1.20, b1 - 0.30, 1, 0, 0, 0.40, 0.08, 0.02, 0.003);
                for (int s = -1; s <= 1; s += 2)
                    Decal(ref c, Paint(MeshColor.FromHex(0xF5F5F5)), s * 0.302, 1.20, 0.5 * (b0 + b1), s, 0, 0, 0.50, 0.06, 0.02, 0.003);
            }
            SetPlates(0.80f, -0.62f, 0f, 0f, 0f, 0f, true);
        }
    }
}
