using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Driving
{
    /// <summary>
    /// Motorbikes (ref_vehicles.md §1.1-1.4): the Pulsar type (bikini fairing with wolf-eye pilot lamps, tank shrouds,
    /// split seat, rising black silencer), the Shine/SP type (trapezoid lamp with a visor, slim tank, long flat seat,
    /// chain case, chrome silencer and carrier), the streetfighter (compact LED lamp, big shrouded tank, short sharp
    /// tail, belly pan, stubby silencer) and the retro Classic type (round lamp in a nacelle with pilot lamps, teardrop
    /// tank, sprung saddle, valanced fenders, peashooter, wire wheels). Parts come in three tiers: the silhouette at
    /// every level, the medium parts from LOD1 and the small ones (fins, covers, springs, chain, levers, pegs, frame)
    /// at LOD0 only.
    /// </summary>
    public static partial class VehicleMesher
    {
        private const int Pulsar = 0, Shine = 1, Street = 2, Classic = 3;

        /// <summary>A graphic colour that reads on the tank: vivid on black or grey bikes, white on coloured ones.</summary>
        private static uint Graphic(in Livery p)
        {
            int lum = MeshColor.R(p.Body) * 3 + MeshColor.G(p.Body) * 6 + MeshColor.B(p.Body);
            if (lum < 900)
            {
                switch (p.Index % 3)
                {
                    case 0: return MeshColor.FromHex(0xD32F2F);
                    case 1: return MeshColor.FromHex(0xB0BEC5);
                    default: return MeshColor.FromHex(0x1E88E5);
                }
            }
            if (lum > 2200) return MeshColor.FromHex(0x263238);
            return MeshColor.FromHex(0xF5F5F5);
        }

        /// <summary>An amber indicator lamp (an elongated lens along the vehicle Z) at (x, y, z); a plain box beyond LOD0.</summary>
        private static void Blinker(ref Ctx c, double x, double y, double z, double r)
        {
            if (c.L0) Ell(ref c, Glass(AmberC), x, y, z, r, r * 0.9, r * 1.6, 0.8, 0.9, 8);
            else OBox(ref c, Glass(AmberC), T(x, y, z), 2 * r, 1.6 * r, 3 * r);
        }

        /// <summary>A small rounded part: a superellipsoid at LOD0, a plain box beyond (same extents).</summary>
        private static void Lump(ref Ctx c, in ShapeBrush b, in Affine3 xf, double rx, double ry, double rz, double e = 0.35)
        {
            if (c.L0) Ell(ref c, b, xf, rx, ry, rz, e, e, 8);
            else OBox(ref c, b, xf, 2 * rx, 2 * ry, 2 * rz);
        }

        private static void Motorbike(ref Ctx c, in VehicleCatalogEntry e, Dims d, in Livery p)
        {
            int model = e.Shape == BodyShape.Cruiser ? Classic : c.Model;
            double wb = d.Wheelbase, r = d.WheelR;
            uint body = p.Body, gfx = Graphic(p);
            bool classic = model == Classic;
            double rake = classic ? 0.47 : 0.44, fl = classic ? 0.74 : 0.70;
            double topY = r + fl * Math.Cos(rake), topZ = wb - fl * Math.Sin(rake);
            double seatY = 0.80, seatZ = 0.42 * wb;
            ShapeBrush black = Plain(EngineC);

            if (c.L0)
            {
                // Frame: head tube, backbone under the tank, down tube, seat rails (mostly hidden, seen through the gaps).
                double hy0 = r + 0.56 * fl * Math.Cos(rake), hz0 = wb - 0.56 * fl * Math.Sin(rake);
                Cyl(ref c, black, 0, hy0, hz0, 0, topY - 0.03, topZ + 0.01, 0.035, 10);
                Path3 bb = P;
                bb.Add(0, topY - 0.06, topZ - 0.02).Add(0, 0.84, 0.80).Add(0, 0.78, 0.50).Add(0, 0.74, 0.30);
                Tube(ref c, black, bb, 0.022, 6);
                Path3 dt = P;
                dt.Add(0, hy0 + 0.02, hz0 - 0.02).Add(0, 0.50, 0.92).Add(0, 0.30, 0.78);
                Tube(ref c, black, dt, 0.022, 6);
                for (int s = -1; s <= 1; s += 2)
                {
                    Path3 rail = P;
                    rail.Add(s * 0.07, 0.48, 0.55).Add(s * 0.075, 0.74, 0.32).Add(s * 0.07, 0.80, -0.05);
                    Tube(ref c, black, rail, 0.016, 5);
                }
                // Footpegs, gear lever and brake pedal; the folded side stand.
                for (int s = -1; s <= 1; s += 2)
                {
                    Rod(ref c, black, s * 0.10, 0.34, 0.47, s * 0.20, 0.34, 0.45, 0.014, 4);
                    Rod(ref c, black, s * 0.10, 0.44, 0.10, s * 0.19, 0.44, 0.08, 0.012, 4);
                    Cyl(ref c, Rubber(TyreC), s * 0.15, 0.34, 0.462, s * 0.21, 0.34, 0.452, 0.019, 6);
                    Rod(ref c, Plain(SteelC), s * 0.15, 0.33, 0.52, s * 0.16, 0.36, 0.62, 0.008, 4);
                }
                Rod(ref c, black, -0.09, 0.30, 0.52, -0.12, 0.27, 0.25, 0.012, 4);
            }
            if (c.Lod == VehicleLod.Lod2) MotoFarFrame(ref c, model, r, topY, topZ, body);
            MotoEngine(ref c, model);
            MotoBody(ref c, model, body, gfx, seatY);
            MotoFront(ref c, model, wb, r, rake, fl, body, gfx);
            MotoRear(ref c, model, r, body);
            MotoExhaust(ref c, model);

            SetPlates(classic ? 0.60f : 0.62f, classic ? -0.38f : -0.43f, (float)(r + 0.27), (float)(wb + 0.05), classic ? 0.6f : 0.30f, 0.45f);
            s_rider = new RiderRef
            {
                HipY = seatY + 0.06, HipZ = seatZ, GripX = classic ? 0.34 : 0.31, GripY = topY + (classic ? 0.08 : 0.06), GripZ = topZ - (classic ? 0.16 : 0.10),
                PegX = 0.20, PegY = 0.36, PegZ = 0.46,
            };
            _ = d;
        }

        /// <summary>
        /// LOD2 skeleton that ties the far bike together (the LOD0 tube frame and the LOD1 side covers are too fine to
        /// draw this far): one black side-profile plate from the headstock along the backbone to the seat rails and down
        /// round the engine to the swingarm pivot, a box swingarm to the rear axle, footpegs for the rider's feet and a
        /// riser under the handlebar. Everything the far bike shows is attached to it (no daylight between the parts).
        /// </summary>
        private static void MotoFarFrame(ref Ctx c, int model, double r, double topY, double topZ, uint body)
        {
            ShapeBrush black = Plain(EngineC);
            bool classic = model == Classic;
            Profile2 fp = Prof.Clear(true);
            fp.Add(topZ + 0.03, topY - 0.03, true).Add(topZ - 0.05, topY + 0.02, true).Add(classic ? 0.62 : 0.56, classic ? 0.90 : 0.86, true)
              .Add(classic ? 0.30 : 0.20, 0.81, true).Add(-0.14, 0.80, true).Add(-0.14, 0.73, true).Add(0.30, 0.60, true).Add(0.46, 0.28, true)
              .Add(0.88, 0.26, true).Add(topZ - 0.05, topY - 0.32, true);
            SideExtrude(ref c, black, fp, -0.055, 0.055, 0.02, 1);
            for (int s = -1; s <= 1; s += 2) OBar(ref c, Plain(classic ? EngineC : AlloyDarkC), s * 0.10, 0.46, 0.52, s * 0.10, r, 0.0, 0.035, 0.05);
            OBox(ref c, black, T(0, 0.34, 0.46), 0.44, 0.03, 0.05); // footpegs (one bar through the frame)
            double hbY = topY + (classic ? 0.08 : 0.06), hbZ = topZ - (classic ? 0.05 : 0.03);
            OBar(ref c, black, 0, topY - 0.04, topZ + 0.02, 0, hbY, hbZ, 0.05, 0.05);
            _ = body;
        }

        private static void MotoEngine(ref Ctx c, int model)
        {
            bool classic = model == Classic, silver = model == Shine || classic;
            ShapeBrush crank = Metal(CrankC), black = Plain(EngineC), fin = Metal(FinC, 0.8f);
            double cz = classic ? 0.68 : 0.64, cy = 0.40;
            // Crankcase: a rounded, slightly egg-shaped block.
            Ell(ref c, silver ? crank : black, 0, cy, cz, 0.12, 0.13, 0.20, 0.45, 0.6, 12);
            // Cylinder (tilted forward; upright on the Classic) and its head.
            double tilt = classic ? 0.12 : 0.55, uy = Math.Cos(tilt), uz = Math.Sin(tilt);
            double by = cy + 0.08, bz = cz + 0.10, cl = classic ? 0.36 : 0.26;
            Cyl(ref c, black, 0, by, bz, 0, by + uy * cl, bz + uz * cl, classic ? 0.075 : 0.065, 10);
            if (c.L1)
                Lump(ref c, silver ? crank : black, Affine3.Translation(0, by + uy * cl, bz + uz * cl) * Affine3.RotationX(tilt), 0.085, 0.04, 0.08, 0.5);
            if (!c.L0) return;
            // Cooling fins, clutch and magneto covers, carburettor, gearbox casing.
            int fins = classic ? 8 : 5;
            for (int i = 0; i < fins; i++)
            {
                double t = (i + 0.7) / (fins + 0.5) * cl;
                double size = (classic ? 0.20 : 0.17) * (1 - 0.15 * (double)i / fins);
                OBox(ref c, classic ? crank : fin, T(0, by + uy * t, bz + uz * t) * Affine3.RotationX(tilt), size, 0.012, size * 0.95);
            }
            for (int s = -1; s <= 1; s += 2)
            {
                Affine3 xf = Affine3.FromBasis(0, -s, 0, s, 0, 0, 0, 0, 1, s * 0.12, cy + 0.01, cz + (s > 0 ? 0.04 : -0.04));
                Shapes.Dome(c.M, xf, crank, s > 0 ? 0.10 : 0.085, 0.035, 14, false, c.S);
            }
            Ell(ref c, black, 0.02, cy + 0.20, cz - 0.10, 0.06, 0.06, 0.07, 0.6, 0.8, 8);
            if (classic) Ell(ref c, crank, 0.0, cy + 0.07, cz - 0.24, 0.10, 0.11, 0.10, 0.5, 0.6, 8);
        }

        private static void MotoBody(ref Ctx c, int model, uint body, uint gfx, double seatY)
        {
            ShapeBrush paint = Paint(body), seat = Leather(SeatC), gpaint = Paint(gfx);
            bool far = c.Lod == VehicleLod.Lod2;
            int v;
            switch (model)
            {
                case Classic:
                {
                    // Teardrop tank with rubber knee pads, a graphic and a chrome filler cap.
                    v = c.M.VertexCount;
                    Ell(ref c, paint, Pitched(0, 0.955, 0.87, -0.10), 0.155, 0.13, 0.28, 0.55, 0.85, 16);
                    TaperByZ(c.M, v, 0.90, 0.60, 0.62);
                    if (c.L0)
                    {
                        for (int s = -1; s <= 1; s += 2)
                        {
                            Decal(ref c, Rubber(TyreC), s * 0.142, 0.93, 0.72, s, 0, -0.35, 0.10, 0.07, 0.02, 0.004);
                            Decal(ref c, gpaint, s * 0.150, 0.99, 0.93, s, 0.25, 0.25, 0.14, 0.05, 0.022, 0.004);
                        }
                        Cyl(ref c, Chrome(), 0, 1.07, 0.92, 0, 1.09, 0.93, 0.035, 10);
                    }
                    // Sprung single saddle and a separate pillion pad; side boxes.
                    Ell(ref c, seat, 0, seatY + 0.03, 0.56, 0.15, 0.05, 0.16, 0.4, 0.55, far ? 8 : 12);
                    if (c.L1) Ell(ref c, seat, 0, 0.87, 0.18, 0.12, 0.045, 0.13, 0.4, 0.6, 8);
                    if (c.L0)
                        for (int s = -1; s <= 1; s += 2)
                            Spring(ref c, Chrome(), s * 0.07, 0.75, 0.46, s * 0.07, 0.83, 0.47, 0.022, 0.004, 3);
                    for (int s = -1; s <= 1; s += 2)
                        Lump(ref c, paint, T(s * 0.115, 0.62, 0.40), 0.03, 0.085, 0.10, 0.3);
                    break;
                }
                case Shine:
                {
                    // Slim rounded tank with a pinstripe; long flat seat on a body-coloured tail; black side panels.
                    v = c.M.VertexCount;
                    Ell(ref c, paint, Pitched(0, 0.94, 0.84, -0.14), 0.14, 0.12, 0.25, 0.5, 0.75, 16);
                    TaperByZ(c.M, v, 0.86, 0.60, 0.7);
                    if (c.L0)
                        for (int s = -1; s <= 1; s += 2)
                            Decal(ref c, gpaint, s * 0.134, 0.95, 0.86, s, 0.25, 0.15, 0.30, 0.03, 0.012, 0.004);
                    Ell(ref c, seat, 0, seatY + 0.01, 0.30, 0.135, 0.055, 0.36, 0.35, 0.55, far ? 8 : 12);
                    Profile2 sp = Prof.Clear(true);
                    sp.Add(0.12, 0.55, true).Add(0.55, 0.56, true).Add(0.62, 0.74, true).Add(0.15, 0.80, true).Add(-0.30, 0.86, true).Add(-0.36, 0.80, true);
                    Fil(ref c, sp, 0.05, 2);
                    SideExtrude(ref c, paint, sp, -0.12, 0.12, 0.04, 2);
                    if (c.L1)
                        for (int s = -1; s <= 1; s += 2)
                            Decal(ref c, Plain(TrimC), s * 0.122, 0.66, 0.36, s, 0, 0, 0.30, 0.14, 0.04, 0.004);
                    break;
                }
                default:
                {
                    // Pulsar type and streetfighter: muscular tank over side covers, shrouds, split seat, sharp tail.
                    bool street = model == Street;
                    v = c.M.VertexCount;
                    Ell(ref c, paint, Pitched(0, street ? 0.96 : 0.95, 0.83, street ? -0.18 : -0.16), street ? 0.175 : 0.165, 0.14, 0.26, street ? 0.4 : 0.45,
                        street ? 0.5 : 0.6, 16);
                    TaperByZ(c.M, v, 0.84, 0.58, street ? 0.55 : 0.6);
                    if (c.L1) MotoShroud(ref c, gpaint, street ? 1.18 : 1.0);
                    if (c.L0 && !street)
                        for (int s = -1; s <= 1; s += 2)
                            Decal(ref c, gpaint, s * 0.150, 1.0, 0.80, s, 0.45, 0, 0.26, 0.035, 0.015, 0.004);
                    // Side covers under the seat (far away one painted silhouette of side covers and tail).
                    Profile2 sc = Prof.Clear(true);
                    if (far)
                    {
                        sc.Add(0.62, 0.56, true).Add(0.66, 0.82, true).Add(0.20, 0.84, true).Add(-0.10, street ? 0.92 : 0.90, true).Add(street ? -0.35 : -0.38, street ? 0.97 : 0.95, true)
                          .Add(street ? -0.33 : -0.36, 0.88, true).Add(-0.10, 0.80, true).Add(0.10, 0.74, true).Add(0.22, 0.56, true);
                        v = c.M.VertexCount;
                        SideExtrude(ref c, paint, sc, -0.105, 0.105, 0.03, 1);
                        TaperByZ(c.M, v, 0.10, -0.38, 0.5);
                    }
                    else
                    {
                        sc.Add(0.22, 0.56, true).Add(0.62, 0.58, true).Add(0.66, 0.80, true).Add(0.28, 0.83, true).Add(0.16, 0.72, true);
                        Fil(ref c, sc, 0.03, 2);
                        SideExtrude(ref c, street ? paint : Plain(TrimC), sc, -0.115, 0.115, 0.03, 2);
                    }
                    Ell(ref c, seat, 0, seatY + 0.005, 0.47, street ? 0.13 : 0.135, 0.05, street ? 0.15 : 0.17, 0.35, 0.5, far ? 8 : 12);
                    if (c.L1) Ell(ref c, seat, 0, seatY + (street ? 0.09 : 0.07), street ? 0.19 : 0.17, street ? 0.10 : 0.11, 0.045, street ? 0.12 : 0.14, 0.35, 0.5, 8);
                    if (!far)
                    {
                        // The tail cowl (far away part of the one silhouette above).
                        Profile2 tp = Prof.Clear(true);
                        if (street)
                            tp.Add(0.30, 0.74, true).Add(0.08, 0.80, true).Add(-0.34, 0.93, true).Add(-0.36, 0.97, true).Add(-0.10, 0.92, true).Add(0.32, 0.84, true);
                        else
                            tp.Add(0.36, 0.70, true).Add(0.06, 0.78, true).Add(-0.36, 0.90, true).Add(-0.38, 0.95, true).Add(-0.10, 0.90, true).Add(0.20, 0.84, true)
                              .Add(0.38, 0.82, true);
                        Fil(ref c, tp, 0.03, 2);
                        v = c.M.VertexCount;
                        SideExtrude(ref c, paint, tp, street ? -0.11 : -0.12, street ? 0.11 : 0.12, 0.035, 2);
                        TaperByZ(c.M, v, street ? 0.10 : 0.15, street ? -0.36 : -0.38, street ? 0.45 : 0.5);
                    }
                    if (c.L0)
                        for (int s = -1; s <= 1; s += 2)
                            Decal(ref c, gpaint, s * 0.108, street ? 0.86 : 0.84, -0.05, s, 0.2, 0, 0.30, 0.025, 0.012, 0.003);
                    if (street && c.L1)
                    {
                        // Belly pan under the engine.
                        Profile2 bp = Prof.Clear(true);
                        bp.Add(0.44, 0.28, true).Add(0.84, 0.20, true).Add(0.92, 0.30, true).Add(0.70, 0.36, true).Add(0.48, 0.36, true);
                        Fil(ref c, bp, 0.03, 2);
                        SideExtrude(ref c, paint, bp, -0.13, 0.13, 0.03, 1);
                    }
                    break;
                }
            }
        }

        /// <summary>Tank shrouds: angular blades sweeping down and forward from the tank sides.</summary>
        private static void MotoShroud(ref Ctx c, in ShapeBrush b, double scale)
        {
            Profile2 sp = Prof.Clear(true);
            sp.Add(0.70, 0.93, true).Add(0.98, 0.99, true).Add(1.06, 0.90, true).Add(1.00, 0.70, true).Add(0.86, 0.72, true).Add(0.74, 0.82, true);
            Fil(ref c, sp, 0.025, 2);
            int v0 = c.M.VertexCount, i0 = c.M.IndexCount;
            SideExtrude(ref c, b, sp, 0.12, 0.12 + 0.06 * scale, 0.02, 1);
            // Lean the blade in toward the top so it hugs the tank.
            TaperByY(c.M, v0, 0.70, 1.0, 0.84);
            MirrorX(ref c, v0, i0);
        }

        private static void MotoFront(ref Ctx c, int model, double wb, double r, double rake, double fl, uint body, uint gfx)
        {
            double cr = Math.Cos(rake), sr = Math.Sin(rake);
            double topY = r + fl * cr, topZ = wb - fl * sr;
            ShapeBrush black = Plain(EngineC), chrome = Chrome(), paint = Paint(body);
            bool classic = model == Classic;
            // Fork legs: sliders at the bottom, chrome stanchions above; the retro bike has body-coloured shrouds.
            for (int s = -1; s <= 1; s += 2)
            {
                double x = s * 0.085, my = r + 0.42 * fl * cr, mz = wb - 0.42 * fl * sr;
                if (c.Lod == VehicleLod.Lod2)
                {
                    OBar(ref c, Metal(AlloyC), x, r, wb, x, topY, topZ, 0.045, 0.045);
                    continue;
                }
                Cyl(ref c, Metal(classic ? ChromeC : AlloyC), x, r - 0.01 * cr, wb + 0.01 * sr, x, my, mz, 0.03, 10);
                Cyl(ref c, chrome, x, my, mz, x, topY, topZ, 0.019, 8);
                if (classic && c.L1) Cyl(ref c, paint, x, r + 0.62 * fl * cr, wb - 0.62 * fl * sr, x, topY - 0.02, topZ + 0.01, 0.034, 10);
            }
            if (c.L1)
            {
                double ly = r + 0.80 * fl * cr, lz = wb - 0.80 * fl * sr;
                Lump(ref c, Plain(classic ? ChromeC : EngineC), Pitched(0, ly, lz, -rake), 0.12, 0.018, 0.035, 0.3);
                Lump(ref c, Plain(classic ? ChromeC : AlloyDarkC), Pitched(0, topY, topZ, -rake), 0.12, 0.016, 0.04, 0.3);
            }
            // Front mudguard hugging the top of the wheel.
            if (classic) FenderArc(ref c, paint, 0, r, wb, r + 0.045, 0.14, 0.012, 20, 140, 12);
            else FenderArc(ref c, model == Shine ? paint : Plain(TrimC), 0, r, wb, r + 0.03, 0.10, 0.01, 45, 95, 20);

            // Handlebar: a bent tube over the top yoke, grips, levers, switch pods, mirrors.
            double hbY = topY + (classic ? 0.08 : 0.06), hbZ = topZ - (classic ? 0.05 : 0.03), hx = classic ? 0.38 : 0.34;
            double back = classic ? 0.10 : 0.06;
            Path3 hb = P;
            hb.Add(-hx, hbY + 0.01, hbZ - back).Add(-0.18, hbY, hbZ - 0.01).Add(0.18, hbY, hbZ - 0.01).Add(hx, hbY + 0.01, hbZ - back);
            Tube(ref c, classic ? chrome : black, hb, 0.012, 6);
            if (c.L0 && !classic) OBox(ref c, Plain(AlloyDarkC), T(0, topY + 0.035, topZ - 0.01), 0.07, 0.05, 0.05); // riser clamp
            for (int s = -1; s <= 1; s += 2)
            {
                double gz = hbZ - back + 0.015;
                if (c.L1) Cyl(ref c, Rubber(TyreC), s * (hx - 0.115), hbY + 0.01, gz + 0.012, s * (hx + 0.005), hbY + 0.012, gz - 0.006, 0.019, 8);
                if (c.L0)
                {
                    OBox(ref c, Plain(TrimC), T(s * (hx - 0.14), hbY + 0.012, gz + 0.02), 0.05, 0.045, 0.055);
                    Path3 lv = P;
                    lv.Add(s * (hx - 0.15), hbY + 0.01, gz + 0.04).Add(s * (hx - 0.08), hbY + 0.0, gz + 0.075).Add(s * (hx + 0.01), hbY - 0.005, gz + 0.06);
                    Tube(ref c, Plain(SteelC), lv, 0.006, 4);
                }
                if (c.L1)
                {
                    // Mirror: a short stalk and a head (round and chromed on the Classic).
                    double mx = s * (hx - 0.12), tx = s * (hx - 0.06), ty = hbY + (classic ? 0.10 : 0.12), tz = hbZ - 0.03;
                    Rod(ref c, classic ? chrome : black, mx, hbY + 0.01, gz + 0.02, tx, ty, tz, 0.006, 4);
                    if (classic) Shapes.Cylinder(c.M, AlongMinusZ(tx, ty + 0.03, tz + 0.012), Metal(ChromeC), 0.045, 0.025, c.L0 ? 10 : 8, 0, 0, true, true, c.S);
                    else Lump(ref c, black, Affine3.Translation(tx + s * 0.02, ty + 0.02, tz) * Affine3.RotationY(s * 0.3), 0.06, 0.032, 0.02, 0.6);
                    if (c.L0)
                        Decal(ref c, Glass(MirrorC), tx + s * (classic ? 0 : 0.02), ty + (classic ? 0.03 : 0.02), tz - (classic ? 0.002 : 0.022), classic ? 0 : -s * 0.3, 0, -1,
                              classic ? 0.075 : 0.10, classic ? 0.075 : 0.05, classic ? 0.037 : 0.02, 0.003);
                }
            }

            // Headlamp unit by model.
            double hlY = topY - 0.06, hlZ = topZ + 0.15;
            switch (model)
            {
                case Classic:
                {
                    // Round headlamp in a chrome bowl, the nacelle above it, two pilot lamps on its flanks.
                    double cy = topY - 0.02, cz = topZ + 0.17;
                    // The chrome bowl reaches back to the fork crown (far away one deeper rim instead of rim and bowl).
                    if (c.L1) Shapes.Dome(c.M, AlongMinusZ(0, cy, cz - 0.02), Metal(ChromeC), 0.105, 0.11, 12, false, c.S);
                    Cyl(ref c, Metal(ChromeC), 0, cy, cz - (c.L1 ? 0.03 : 0.14), 0, cy, cz + 0.005, 0.112, 14);
                    if (c.L1) Shapes.Dome(c.M, AlongZ(0, cy, cz), Glass(LensC), 0.095, 0.03, 12, false, c.S);
                    else Cyl(ref c, Glass(LensC), 0, cy, cz, 0, cy, cz + 0.012, 0.095, 12);
                    if (c.L1)
                    {
                        Ell(ref c, paint, Pitched(0, topY + 0.05, topZ + 0.04, -0.3), 0.10, 0.05, 0.11, 0.6, 0.7, 8);
                        for (int s = -1; s <= 1; s += 2) Lamp(ref c, LensC, ChromeC, s * 0.15, cy - 0.05, cz - 0.08, 0, 0, 1, 0.03, 0.03, 10);
                        Cyl(ref c, Metal(ChromeC), 0, topY + 0.09, topZ + 0.02, 0, topY + 0.12, topZ, 0.05, 12);
                    }
                    if (c.L0) Decal(ref c, Glass(ScreenC), 0, topY + 0.122, topZ, 0, 0.8, -0.6, 0.07, 0.07, 0.035, 0.002);
                    break;
                }
                case Shine:
                {
                    // Trapezoid lamp in a cowl with a small visor; indicators on stalks.
                    Profile2 cw = Prof.Clear(true);
                    cw.Add(-0.13, -0.08, true).Add(0.13, -0.08, true).Add(0.11, 0.09, true).Add(-0.11, 0.09, true);
                    Fil(ref c, cw, 0.03, 2);
                    int v0 = c.M.VertexCount;
                    FrontExtrude(ref c, paint, cw, hlZ - 0.12, hlZ, 0.03, 1);
                    Transform(c.M, v0, T(0, hlY, 0));
                    Panel(ref c, Glass(LensC), 0, hlY - 0.01, hlZ, 0, 0.05, 1, 0.20, 0.10, 0.03, 0.01);
                    if (c.L1)
                    {
                        Decal(ref c, Glass(ScreenC), 0, hlY + 0.105, hlZ - 0.05, 0, 0.6, 0.8, 0.19, 0.08, 0.03, 0.006);
                        for (int s = -1; s <= 1; s += 2)
                        {
                            if (c.L0) Rod(ref c, Plain(TrimC), s * 0.10, hlY - 0.02, hlZ - 0.08, s * 0.18, hlY - 0.02, hlZ - 0.06, 0.008, 4);
                            Blinker(ref c, s * 0.18, hlY - 0.02, hlZ - 0.05, 0.02);
                        }
                    }
                    break;
                }
                case Street:
                {
                    // Compact LED headlamp with claw DRLs under a short tinted flyscreen.
                    Profile2 cw = Prof.Clear(true);
                    cw.Add(-0.10, -0.06, true).Add(0.10, -0.06, true).Add(0.13, 0.04, true).Add(0.06, 0.10, true).Add(-0.06, 0.10, true).Add(-0.13, 0.04, true);
                    Fil(ref c, cw, 0.02, 2);
                    int v0 = c.M.VertexCount;
                    FrontExtrude(ref c, Plain(TrimC), cw, hlZ - 0.12, hlZ - 0.02, 0.025, 1);
                    Transform(c.M, v0, T(0, hlY, 0));
                    Panel(ref c, Glass(LensC), 0, hlY - 0.005, hlZ - 0.02, 0, 0.1, 1, 0.13, 0.07, 0.02, 0.008);
                    if (c.L1)
                    {
                        for (int s = -1; s <= 1; s += 2)
                        {
                            Decal(ref c, Glass(DrlC), s * 0.085, hlY + 0.035, hlZ - 0.02, s * 0.3, 0.1, 1, 0.07, 0.016, 0.006, 0.004);
                            Blinker(ref c, s * 0.155, hlY - 0.01, hlZ - 0.075, 0.016);
                        }
                        Decal(ref c, Glass(ScreenC), 0, hlY + 0.12, hlZ - 0.085, 0, 0.5, 0.86, 0.13, 0.08, 0.03, 0.006);
                    }
                    break;
                }
                default:
                {
                    // Pulsar type: bikini fairing with the main lamp and two wolf-eye pilot lamps, tinted visor.
                    Profile2 cw = Prof.Clear(true);
                    cw.Add(-0.11, -0.09, true).Add(0.11, -0.09, true).Add(0.15, 0.02, true).Add(0.09, 0.12, true).Add(-0.09, 0.12, true).Add(-0.15, 0.02, true);
                    Fil(ref c, cw, 0.03, 2);
                    int v0 = c.M.VertexCount;
                    FrontExtrude(ref c, paint, cw, hlZ - 0.13, hlZ, 0.03, 1);
                    BendZ(c.M, v0, 1.6);
                    Transform(c.M, v0, T(0, hlY, 0));
                    Panel(ref c, Glass(LensC), 0, hlY - 0.035, hlZ + 0.002, 0, 0.1, 1, 0.11, 0.07, 0.03, 0.01);
                    if (c.L1)
                    {
                        for (int s = -1; s <= 1; s += 2)
                        {
                            Decal(ref c, Glass(DrlC), s * 0.09, hlY + 0.025, hlZ - 0.012, s * 0.45, 0.15, 1, 0.065, 0.028, 0.012, 0.004);
                            Blinker(ref c, s * 0.165, hlY - 0.04, hlZ - 0.085, 0.016);
                        }
                        Decal(ref c, Glass(ScreenC), 0, hlY + 0.135, hlZ - 0.065, 0, 0.55, 0.84, 0.15, 0.09, 0.035, 0.006);
                    }
                    break;
                }
            }
            if (!classic && c.L1)
            {
                // Instrument cluster facing the rider.
                Lump(ref c, Plain(TrimC), Pitched(0, topY + 0.10, topZ + 0.03, 0.6), 0.08, 0.0125, 0.045, 0.3);
                if (c.L0) Decal(ref c, Glass(ScreenC), 0, topY + 0.106, topZ + 0.024, 0, 0.83, -0.55, 0.13, 0.065, 0.012, 0.003);
            }
            _ = gfx;
        }

        private static void MotoRear(ref Ctx c, int model, double r, uint body)
        {
            ShapeBrush black = Plain(EngineC), chrome = Chrome(), paint = Paint(body);
            bool classic = model == Classic;
            if (c.L1)
            {
                // Swingarm: two box-section arms from the pivot to the axle.
                for (int s = -1; s <= 1; s += 2)
                {
                    double x = s * 0.10, len;
                    Affine3 xf = Affine3.Along(x, 0.44, 0.52, x, r, 0, out len);
                    Lump(ref c, Plain(classic ? EngineC : AlloyDarkC), xf * T(0, 0.5 * len, 0), 0.016, 0.5 * len, 0.026, 0.3);
                }
                // Twin shocks: a chrome damper and a coloured spring (the streetfighter's monoshock is hidden).
                if (model != Street)
                    for (int s = -1; s <= 1; s += 2)
                    {
                        double x = s * 0.115;
                        Cyl(ref c, chrome, x, r + 0.03, 0.06, x, 0.80, 0.20, 0.012, 6);
                        uint sc = classic ? ChromeC : model == Pulsar ? MeshColor.FromHex(0xC62828) : EngineC;
                        if (c.L0) Spring(ref c, Paint(sc), x, r + 0.12, 0.09, x, 0.74, 0.18, 0.028, 0.006, 3);
                        else Cyl(ref c, Paint(sc), x, r + 0.12, 0.09, x, 0.74, 0.18, 0.026, 6);
                        if (c.L0) Cyl(ref c, Plain(classic ? ChromeC : EngineC), x, 0.73, 0.18, x, 0.81, 0.20, 0.03, 8);
                    }
            }
            if (c.L0)
            {
                // Chain run on the left: rear sprocket, chain and guard (a full chain case on the Shine type).
                double x = -0.09;
                Cyl(ref c, Metal(SteelC), -0.13, r, 0, 0.13, r, 0, 0.012, 6);
                Shapes.Cylinder(c.M, AcrossX(x - 0.004, r, 0), Metal(SteelC), 0.105, 0.008, 16, 0, 0, true, true, c.S);
                if (model == Shine)
                {
                    Profile2 cc = Prof.Clear(true);
                    cc.Add(-0.10, r - 0.06, true).Add(0.58, 0.30, true).Add(0.58, 0.42, true).Add(-0.10, r + 0.12, true);
                    Fil(ref c, cc, 0.05, 2);
                    SideExtrude(ref c, Plain(TrimC), cc, x - 0.03, x + 0.01, 0.01, 1);
                }
                else
                {
                    Path3 ch = P;
                    ch.Add(x, r + 0.10, 0.0).Add(x, 0.42, 0.56).Add(x, 0.30, 0.56).Add(x, r - 0.10, 0.0);
                    Tube(ref c, Plain(MeshColor.FromHex(0x50555B)), ch, 0.009, 4);
                    Profile2 cg = Prof.Clear(true);
                    cg.Add(0.08, r + 0.13, true).Add(0.50, 0.46, true).Add(0.52, 0.49, true).Add(0.08, r + 0.17, true);
                    SideExtrude(ref c, Plain(TrimC), cg, x - 0.02, x + 0.02, 0.008, 1);
                }
            }
            // Rear mudguard: a big valanced one on the Classic, a hugger and a slim plate hanger on the others.
            if (classic) FenderArc(ref c, paint, 0, r, 0, r + 0.05, 0.16, 0.012, 25, 150, 12);
            else if (c.L1)
            {
                FenderArc(ref c, Plain(TrimC), 0, r, 0, r + 0.03, 0.12, 0.008, 70, 60, 16);
                Profile2 tt = Prof.Clear(true);
                tt.Add(-0.24, 0.81, true).Add(-0.40, 0.72, true).Add(-0.44, 0.55, true).Add(-0.40, 0.55, true).Add(-0.36, 0.69, true).Add(-0.22, 0.77, true);
                Fil(ref c, tt, 0.015, 1);
                SideExtrude(ref c, Plain(TrimC), tt, -0.04, 0.04, 0.012, 1);
            }
            // Tail lamp, grab rails, rear indicators.
            switch (model)
            {
                case Classic:
                    // Round tail lamp on top of the valanced rear mudguard (its 125° point), with the plate under it.
                    Lamp(ref c, TailC, ChromeC, 0, r + (r + 0.062) * Math.Sin(125 * Math.PI / 180), (r + 0.062) * Math.Cos(125 * Math.PI / 180), 0, 0.55, -0.83,
                         0.045, 0.03, 12);
                    if (c.L0)
                    {
                        Path3 gr = P;
                        gr.Add(-0.13, 0.84, 0.30).Add(-0.15, 0.85, 0.02).Add(-0.10, 0.86, -0.12).Add(0.10, 0.86, -0.12).Add(0.15, 0.85, 0.02).Add(0.13, 0.84, 0.30);
                        Tube(ref c, chrome, gr, 0.012, 6);
                    }
                    break;
                case Shine:
                    Panel(ref c, Glass(TailC), 0, 0.84, -0.375, 0, 0.3, -1, 0.15, 0.06, 0.025, 0.012);
                    if (c.L0)
                    {
                        Path3 gr = P;
                        gr.Add(-0.13, 0.82, 0.20).Add(-0.14, 0.86, -0.10).Add(-0.06, 0.87, -0.30).Add(0.06, 0.87, -0.30).Add(0.14, 0.86, -0.10).Add(0.13, 0.82, 0.20);
                        Tube(ref c, chrome, gr, 0.012, 6);
                    }
                    break;
                default:
                    Panel(ref c, Glass(TailC), 0, model == Street ? 0.935 : 0.90, model == Street ? -0.365 : -0.385, 0, 0.35, -1, 0.12, 0.04, 0.012, 0.012);
                    if (c.L0)
                        for (int s = -1; s <= 1; s += 2)
                        {
                            Path3 gr = P;
                            gr.Add(s * 0.12, 0.84, 0.18).Add(s * 0.13, 0.90, 0.0).Add(s * 0.09, 0.93, -0.16);
                            Tube(ref c, Plain(AlloyDarkC), gr, 0.012, 5);
                        }
                    break;
            }
            if (c.L1)
                for (int s = -1; s <= 1; s += 2)
                {
                    double z = classic ? -0.30 : -0.33;
                    if (c.L0) Rod(ref c, Plain(TrimC), s * 0.04, 0.72, z + 0.02, s * 0.12, 0.72, z, 0.007, 4);
                    Blinker(ref c, s * 0.13, 0.72, z - 0.01, 0.017);
                }
        }

        private static void MotoExhaust(ref Ctx c, int model)
        {
            ShapeBrush black = Plain(EngineC), chrome = Chrome();
            switch (model)
            {
                case Classic:
                {
                    // Chrome header and the long "peashooter" silencer.
                    if (c.L1)
                    {
                        Path3 h = P;
                        h.Add(0.05, 0.66, 0.86).Add(0.10, 0.52, 0.90).Add(0.13, 0.36, 0.80).Add(0.15, 0.32, 0.55);
                        Tube(ref c, chrome, h, 0.022, 8);
                    }
                    Cone(ref c, chrome, 0.15, 0.32, 0.56, 0.165, 0.37, -0.42, 0.032, 0.048, 12);
                    if (c.L0) Cyl(ref c, Plain(EngineC), 0.165, 0.372, -0.43, 0.166, 0.373, -0.44, 0.03, 10);
                    break;
                }
                case Shine:
                {
                    if (c.L1)
                    {
                        Path3 h = P;
                        h.Add(0.04, 0.55, 0.84).Add(0.08, 0.34, 0.80).Add(0.12, 0.27, 0.55).Add(0.14, 0.30, 0.38);
                        Tube(ref c, chrome, h, 0.02, 6);
                    }
                    Cone(ref c, chrome, 0.14, 0.30, 0.40, 0.155, 0.40, -0.26, 0.045, 0.05, 12, 0.012);
                    if (c.L0) Decal(ref c, Metal(CrankC), 0.20, 0.37, 0.05, 1, 0.3, 0, 0.30, 0.06, 0.025, 0.004);
                    break;
                }
                case Street:
                {
                    // Short stubby silencer ending under the footpeg.
                    if (c.L1)
                    {
                        Path3 h = P;
                        h.Add(0.04, 0.55, 0.84).Add(0.06, 0.30, 0.84).Add(0.10, 0.24, 0.62);
                        Tube(ref c, black, h, 0.02, 6);
                    }
                    Ell(ref c, black, Pitched(0.13, 0.30, 0.40, -0.25), 0.06, 0.065, 0.14, 0.35, 0.45, 12);
                    if (c.L0)
                    {
                        Cyl(ref c, chrome, 0.15, 0.36, 0.25, 0.16, 0.39, 0.18, 0.03, 10);
                        Decal(ref c, Metal(AlloyDarkC), 0.192, 0.32, 0.42, 1, 0.1, 0, 0.22, 0.08, 0.03, 0.003);
                    }
                    break;
                }
                default:
                {
                    // Pulsar type: a black silencer rising to the rear, silver heat shield and tip.
                    if (c.L1)
                    {
                        Path3 h = P;
                        h.Add(0.04, 0.56, 0.86).Add(0.08, 0.32, 0.82).Add(0.12, 0.26, 0.58).Add(0.15, 0.32, 0.36);
                        Tube(ref c, black, h, 0.02, 6);
                    }
                    Cone(ref c, black, 0.15, 0.32, 0.38, 0.175, 0.50, -0.20, 0.05, 0.062, 12, 0.015);
                    if (c.L0)
                    {
                        Cone(ref c, chrome, 0.175, 0.50, -0.20, 0.177, 0.515, -0.25, 0.05, 0.04, 10);
                        Decal(ref c, Metal(CrankC), 0.222, 0.43, 0.06, 1, 0.1, 0.05, 0.30, 0.07, 0.03, 0.004);
                    }
                    break;
                }
            }
        }
    }
}
