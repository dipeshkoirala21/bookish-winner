using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Driving
{
    public static partial class VehicleMesher
    {
        // -----------------------------------------------------------------------------------------------------------
        // Rider reference points (set by the two-wheeler builders, read by the rider)
        // -----------------------------------------------------------------------------------------------------------

        /// <summary>Where the rider's hips, hands and feet go on the two-wheeler being built.</summary>
        internal struct RiderRef
        {
            public double HipY, HipZ, GripX, GripY, GripZ, PegX, PegY, PegZ;
            public bool Pedals;
        }

        [ThreadStatic] private static RiderRef s_rider;

        // -----------------------------------------------------------------------------------------------------------
        // Scooters (ref_vehicles.md §1.5-1.6)
        // -----------------------------------------------------------------------------------------------------------

        private const int Dio = 0, Activa = 1, Ntorq = 2;

        private static void Scooter(ref Ctx c, in VehicleCatalogEntry e, Dims d, in Livery p)
        {
            ScooterCore(ref c, d, p, c.Model, false);
        }

        private static void EScooter(ref Ctx c, in VehicleCatalogEntry e, Dims d, in Livery p)
        {
            ScooterCore(ref c, d, p, c.Model == 0 ? 10 : 11, true);
        }

        /// <summary>The step-through scooter: rear body over the engine, floorboard, front apron and nose, handlebar
        /// cover, long seat. Models: Dio (sharp V nose lamp, sporty tail), Activa (round family shape, chrome lamp
        /// garnish), Ntorq (jet LED lamps, sharp tail), and the e-scooters NIU (10: round ring lamp on the handlebar,
        /// boxy-soft panels, hub motor) and Ather (11: slim apron lamp, exposed rear frame).</summary>
        private static void ScooterCore(ref Ctx c, Dims d, in Livery p, int model, bool electric)
        {
            double wb = d.Wheelbase, r = d.WheelR;
            uint body = p.Body;
            bool niu = model == 10, ather = model == 11, activa = model == Activa;
            uint inner = electric ? MeshColor.FromHex(niu ? 0x3A3F45u : 0x2B2E33u) : activa ? Shade(body, 0.85f) : TrimC;
            ShapeBrush paint = Paint(body), black = Plain(TrimC), seat = Leather(SeatC), inr = Paint(inner);
            double seatY = 0.80;

            // ---- Rear body: a smooth hull from the tail tip to the floorboard (sharp tails on the sporty types,
            // round on the family type, soft-boxy on the NIU type).
            HullBegin();
            if (niu)
            {
                HullAdd(-0.45, 0, 0.70, 0.13, 0.10, 0.05, 0.04);
                HullAdd(-0.38, 0, 0.69, 0.17, 0.13, 0.06, 0.05);
                HullAdd(0.00, 0, 0.66, 0.19, 0.15, 0.07, 0.05);
                HullAdd(0.40, 0, 0.60, 0.19, 0.18, 0.07, 0.05);
                HullAdd(0.64, 0, 0.50, 0.17, 0.15, 0.05, 0.04);
            }
            else if (activa)
            {
                HullAdd(-0.46, 0, 0.70, 0.10, 0.08, 0.07, 0.06);
                HullAdd(-0.40, 0, 0.70, 0.15, 0.11, 0.10, 0.08);
                HullAdd(-0.15, 0, 0.67, 0.19, 0.15, 0.14, 0.11);
                HullAdd(0.20, 0, 0.63, 0.20, 0.17, 0.15, 0.11);
                HullAdd(0.45, 0, 0.58, 0.19, 0.19, 0.12, 0.07);
                HullAdd(0.64, 0, 0.50, 0.17, 0.15, 0.08, 0.05);
            }
            else
            {
                double tip = ather ? 0.80 : 0.81;
                HullAdd(-0.50, 0, tip, 0.025, 0.02, 0.02, 0.02);
                HullAdd(-0.44, 0, tip - 0.03, 0.08, 0.06, 0.05, 0.05);
                HullAdd(-0.30, 0, tip - 0.08, 0.14, 0.10, 0.09, 0.08);
                HullAdd(-0.12, 0, 0.69, 0.175, 0.13, 0.12, 0.10);
                HullAdd(0.10, 0, 0.65, 0.19, 0.155, 0.13, 0.10);
                HullAdd(0.32, 0, 0.61, 0.19, 0.175, 0.13, 0.09);
                HullAdd(0.50, 0, 0.56, 0.18, 0.19, 0.11, 0.07);
                HullAdd(0.64, 0, 0.50, 0.16, 0.15, 0.08, 0.05);
            }
            HullEmit(ref c, paint, 3, true, true);
            if (c.L0 && !electric)
            {
                // Side graphic / chrome strip on the rear body.
                for (int s = -1; s <= 1; s += 2)
                    if (activa) Decal(ref c, Chrome(), s * 0.195, 0.62, 0.20, s, 0, -0.05, 0.40, 0.015, 0.007, 0.004);
                    else Decal(ref c, Paint(Graphic(p)), s * 0.183, 0.64, 0.02, s, 0.05, -0.1, 0.46, 0.035, 0.015, 0.004, s * 0.08);
            }

            // ---- Floorboard with a rubber mat, and the step under it.
            Profile2 fb = Prof.Clear(true);
            fb.Add(-0.17, 0.56, true).Add(0.17, 0.56, true).Add(0.17, 0.98, true).Add(-0.17, 0.98, true);
            Fil(ref c, fb, 0.05, 2);
            PlanExtrude(ref c, inr, fb, 0.27, 0.34, 0.02, 0.02, 1);
            if (c.L0) Decal(ref c, Rubber(TyreC), 0, 0.34, 0.77, 0, 1, 0, 0.30, 0.38, 0.04, 0.003);

            // ---- Front: the apron (leg shield behind, nose over the front wheel) as a hull up the Y axis.
            HullBegin();
            double nose = niu ? 0.86 : activa ? 0.90 : 1.0; // how far the nose pushes over the wheel
            HullAdd(0.33, 0, 0.96, 0.17, 0.045, 0.03, 0.02);
            HullAdd(0.46, 0, 0.99, 0.21, 0.075, 0.06, 0.03);
            HullAdd(0.58, 0, 0.91 + 0.17 * nose, 0.23, 0.17 * nose, activa ? 0.14 : 0.11, 0.03);
            HullAdd(0.68, 0, 0.91 + 0.205 * nose, 0.23, 0.205 * nose, activa ? 0.16 : 0.12, 0.03);
            HullAdd(0.84, 0, 0.92 + 0.175 * nose, niu ? 0.22 : 0.21, 0.175 * nose, activa ? 0.14 : 0.11, 0.03);
            HullAdd(0.96, 0, 0.95 + 0.095 * nose, niu ? 0.18 : 0.15, 0.095 * nose, 0.08, 0.03);
            HullAdd(1.02, 0, 0.97 + 0.05 * nose, niu ? 0.12 : 0.09, 0.05 * nose, 0.045, 0.03);
            HullEmit(ref c, paint, 3, true, true, true);
            // Inner leg shield in the trim colour.
            if (c.L1) Decal(ref c, inr, 0, 0.66, 0.913, 0, 0.05, -1, 0.36, 0.48, 0.08, 0.004);

            // ---- Front fork, fender, wheel arch.
            for (int s = -1; s <= 1; s += 2)
                Cyl(ref c, Metal(AlloyDarkC), s * 0.075, r, wb, s * 0.07, 0.66, wb - 0.12, 0.024, 8);
            FenderArc(ref c, paint, 0, r, wb, r + 0.035, 0.13, 0.012, 35, 105, 20);

            // ---- Handlebar cover with the speedo, grips, mirrors.
            double hbY = 1.06, hbZ = 0.98;
            HullBegin();
            HullAdd(hbZ - 0.08, 0, hbY - 0.005, 0.12, 0.04, 0.03, 0.03);
            HullAdd(hbZ - 0.01, 0, hbY + 0.005, 0.20, 0.06, 0.04, 0.035);
            HullAdd(hbZ + 0.08, 0, hbY, 0.17, 0.055, 0.04, 0.035);
            HullAdd(hbZ + (niu ? 0.11 : 0.15), 0, hbY - 0.02, niu ? 0.13 : 0.07, 0.035, 0.03, 0.025);
            HullEmit(ref c, niu ? inr : paint, 2, true, true);
            if (c.L1) Decal(ref c, Glass(ScreenC), 0, hbY + 0.052, hbZ - 0.03, 0, 0.85, -0.5, niu ? 0.12 : 0.15, 0.07, 0.025, 0.003);
            if (!c.L1) Rod(ref c, Rubber(TyreC), -0.35, hbY, hbZ - 0.03, 0.35, hbY, hbZ - 0.03, 0.018, 4);
            for (int s = -1; s <= 1 && c.L1; s += 2)
            {
                Cyl(ref c, Plain(TrimC), s * 0.19, hbY, hbZ - 0.01, s * 0.33, hbY + 0.005, hbZ - 0.03, 0.013, 6);
                Cyl(ref c, Rubber(TyreC), s * 0.24, hbY + 0.003, hbZ - 0.02, s * 0.35, hbY + 0.006, hbZ - 0.035, 0.02, 8);
                {
                    Rod(ref c, black, s * 0.24, hbY + 0.02, hbZ - 0.0, s * 0.29, hbY + 0.16, hbZ - 0.02, 0.006, 4);
                    Lump(ref c, black, Affine3.Translation(s * 0.31, hbY + 0.18, hbZ - 0.02) * Affine3.RotationY(s * 0.3), 0.06, 0.035, 0.02, 0.6);
                    if (c.L0)
                    {
                        Path3 lv = P;
                        lv.Add(s * 0.20, hbY + 0.01, hbZ + 0.02).Add(s * 0.28, hbY + 0.005, hbZ + 0.05).Add(s * 0.34, hbY - 0.005, hbZ + 0.03);
                        Tube(ref c, Plain(SteelC), lv, 0.006, 4);
                    }
                }
            }

            // ---- Lamps by model.
            if (niu)
            {
                // Round LED ring headlamp centred on the handlebar cover.
                Cyl(ref c, Plain(TrimC), 0, hbY + 0.0, hbZ + 0.08, 0, hbY + 0.0, hbZ + 0.12, 0.085, 16);
                if (c.L0) Shapes.Torus(c.M, AlongZ(0, hbY, hbZ + 0.122), Glass(DrlC), 0.07, 0.009, 16, 4, c.S);
                Shapes.Dome(c.M, AlongZ(0, hbY, hbZ + 0.12), Glass(LensC), 0.055, 0.02, 14, false, c.S);
                if (c.L1) Decal(ref c, Glass(AmberC), 0, 0.80, 1.235, 0, 0.1, 1, 0.24, 0.025, 0.01, 0.004);
            }
            else if (ather)
            {
                Panel(ref c, Glass(LensC), 0, 0.78, 1.30, 0, 0.25, 1, 0.20, 0.06, 0.025, 0.012);
                if (c.L1)
                    for (int s = -1; s <= 1; s += 2)
                        Decal(ref c, Glass(DrlC), s * 0.11, 0.87, 1.26, s * 0.5, 0.4, 1, 0.09, 0.015, 0.006, 0.004, -s * 0.3);
            }
            else if (model == Dio)
            {
                // Sharp V LED headlamp on the nose.
                for (int s = -1; s <= 1; s += 2)
                    Panel(ref c, Glass(LensC), s * 0.06, 0.75, 1.30, s * 0.35, 0.25, 1, 0.12, 0.045, 0.015, 0.012, -s * 0.45);
                if (c.L1)
                {
                    if (c.L0) Decal(ref c, Paint(Graphic(p)), 0, 0.86, 1.265, 0, 0.45, 0.9, 0.08, 0.14, 0.03, 0.004);
                    for (int s = -1; s <= 1; s += 2)
                        Decal(ref c, Glass(AmberC), s * 0.16, hbY - 0.01, hbZ + 0.10, s * 0.6, 0, 1, 0.05, 0.025, 0.01, 0.004);
                }
            }
            else if (activa)
            {
                // Lamp on the apron with a chrome garnish.
                Panel(ref c, Metal(ChromeC), 0, 0.78, 1.268, 0, 0.2, 1, 0.20, 0.13, 0.05, 0.01);
                Panel(ref c, Glass(LensC), 0, 0.78, 1.278, 0, 0.2, 1, 0.17, 0.10, 0.045, 0.01);
                if (c.L1)
                    for (int s = -1; s <= 1; s += 2)
                        Decal(ref c, Glass(AmberC), s * 0.19, 0.80, 1.20, s * 0.8, 0, 0.6, 0.05, 0.035, 0.012, 0.004);
            }
            else
            {
                // Ntorq type: "jet" LED lamps and DRL stripes.
                Panel(ref c, Glass(LensC), 0, 0.76, 1.31, 0, 0.2, 1, 0.12, 0.07, 0.02, 0.012);
                if (c.L1)
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Decal(ref c, Glass(DrlC), s * 0.10, 0.84, 1.27, s * 0.5, 0.3, 1, 0.09, 0.018, 0.008, 0.004, -s * 0.35);
                        Decal(ref c, Glass(AmberC), s * 0.15, hbY - 0.01, hbZ + 0.10, s * 0.6, 0, 1, 0.05, 0.025, 0.01, 0.004);
                    }
            }

            // ---- Seat (long, two-step) and grab rail.
            if (c.L1)
            {
                Ell(ref c, seat, 0, seatY + 0.01, 0.47, 0.16, 0.055, 0.16, 0.35, 0.55, 12);
                Ell(ref c, seat, 0, seatY + 0.03, 0.17, 0.15, 0.055, 0.18, 0.35, 0.55, 12);
            }
            else Ell(ref c, seat, 0, seatY + 0.02, 0.32, 0.16, 0.055, 0.32, 0.35, 0.55, 8);
            if (c.L0)
            {
                Path3 gr = P;
                if (ather)
                    gr.Add(-0.12, 0.84, 0.0).Add(-0.13, 0.86, -0.28).Add(0.13, 0.86, -0.28).Add(0.12, 0.84, 0.0);
                else
                    gr.Add(-0.14, 0.80, 0.02).Add(-0.15, 0.84, -0.18).Add(-0.08, 0.86, -0.30).Add(0.08, 0.86, -0.30).Add(0.15, 0.84, -0.18).Add(0.14, 0.80, 0.02);
                Tube(ref c, Plain(activa ? ChromeC : AlloyDarkC), gr, 0.012, 6);
            }

            // ---- Engine / CVT case on the left (petrol) or a hub motor (electric); silencer on the right.
            if (!electric)
            {
                Profile2 cv = Prof.Clear(true);
                cv.Add(-0.06, 0.20, true).Add(0.52, 0.24, true).Add(0.56, 0.38, true).Add(0.10, 0.44, true).Add(-0.06, 0.38, true);
                Fil(ref c, cv, 0.06, 2);
                SideExtrude(ref c, Metal(CrankC), cv, -0.15, -0.06, 0.03, 1);
                if (c.L0) Ell(ref c, Plain(TrimC), -0.10, 0.48, 0.30, 0.06, 0.05, 0.12, 0.6, 0.6, 8); // air box
                if (c.L1)
                {
                    Path3 h = P;
                    h.Add(0.02, 0.24, 0.48).Add(0.10, 0.22, 0.30).Add(0.13, 0.28, 0.16);
                    Tube(ref c, Plain(EngineC), h, 0.018, 6);
                }
                Cone(ref c, Plain(EngineC), 0.13, 0.28, 0.18, 0.15, 0.36, -0.20, 0.05, 0.045, 10, 0.012);
                if (c.L0)
                {
                    Decal(ref c, Metal(CrankC), 0.196, 0.34, 0.0, 1, 0.15, 0, 0.26, 0.06, 0.025, 0.004);
                    Cyl(ref c, Chrome(), 0.15, 0.36, -0.20, 0.152, 0.37, -0.235, 0.035, 10);
                }
            }
            else
            {
                // Swingarm and a motor hub (hub drawn by the wheel); exposed frame tube on the Ather type.
                if (c.L1) Lump(ref c, Plain(AlloyDarkC), T(-0.11, 0.30, 0.22), 0.025, 0.04, 0.22, 0.3);
                if (ather && c.L1)
                {
                    Path3 fr = P;
                    fr.Add(-0.10, 0.45, 0.50).Add(-0.10, 0.62, 0.10).Add(-0.08, 0.72, -0.30);
                    Tube(ref c, Plain(MeshColor.FromHex(0x2E7D32)), fr, 0.015, 6);
                }
            }
            if (c.L1)
                Spring(ref c, Paint(MeshColor.FromHex(niu ? 0xC62828u : 0x37474Fu)), 0.13, 0.30, 0.0, 0.13, 0.58, 0.12, 0.025, 0.005, 4);

            // ---- Rear: hugger, tail lamp, indicators.
            if (c.L1) FenderArc(ref c, Plain(TrimC), 0, r, 0, r + 0.03, 0.12, 0.008, 95, 70, 16);
            double tz = niu || activa ? -0.455 : -0.49, ty = niu || activa ? 0.66 : 0.74;
            Panel(ref c, Glass(TailC), 0, ty, tz, 0, 0.25, -1, niu ? 0.20 : 0.16, 0.045, 0.02, 0.012);
            if (c.L1)
                for (int s = -1; s <= 1; s += 2)
                    Blinker(ref c, s * 0.12, ty - 0.06, tz + 0.03, 0.016);

            SetPlates(0.50f, -0.47f, 0.60f, (float)(wb + 0.10), 0.20f, 0.15f);
            s_rider = new RiderRef
            {
                HipY = seatY + 0.07, HipZ = 0.42 * wb, GripX = 0.30, GripY = hbY + 0.005, GripZ = hbZ - 0.03, PegX = 0.11, PegY = 0.36, PegZ = 0.80,
            };
        }

        // -----------------------------------------------------------------------------------------------------------
        // Bicycles (ref_vehicles.md §1.7)
        // -----------------------------------------------------------------------------------------------------------

        private static void Bicycle(ref Ctx c, in VehicleCatalogEntry e, Dims d, in Livery p)
        {
            double wb = d.Wheelbase, r = d.WheelR;
            bool roadster = c.Model == 1;
            uint frameC = roadster ? MeshColor.FromHex(0x1B1D20) : p.Body;
            ShapeBrush frame = Paint(frameC), black = Plain(EngineC), steel = Metal(SteelC), chrome = Chrome();
            double bbY = 0.30, bbZ = 0.48;
            double seatY = 0.95, saddleZ = 0.38;
            double headTopY = roadster ? 0.98 : 0.92, headTopZ = wb - 0.24, headBotY = roadster ? 0.80 : 0.74, headBotZ = wb - 0.18;
            double tube = roadster ? 0.016 : 0.02;
            // Frame: seat tube, top tube, down tube, chain stays, seat stays.
            double stTopY = roadster ? 0.84 : 0.78, stTopZ = bbZ - (stTopY - bbY) * 0.2;
            Rod(ref c, frame, 0, bbY, bbZ, 0, stTopY, stTopZ, tube, 6);
            Rod(ref c, frame, 0, stTopY - 0.02, stTopZ, 0, headTopY - 0.03, headTopZ, roadster ? tube : tube * 1.1, 6);
            Rod(ref c, frame, 0, bbY + 0.02, bbZ + 0.02, 0, headBotY + 0.02, headBotZ, tube * 1.15, 6);
            if (roadster) Rod(ref c, frame, 0, bbY + 0.03, bbZ + 0.03, 0, headBotY - 0.06, headBotZ + 0.02, tube, 6); // twin down tube
            Rod(ref c, frame, 0, headBotY, headBotZ, 0, headTopY, headTopZ, 0.024, 8); // head tube
            for (int s = -1; s <= 1; s += 2)
            {
                Rod(ref c, frame, s * 0.04, bbY, bbZ, s * 0.06, r, 0, 0.011, 5);
                Rod(ref c, frame, s * 0.03, stTopY - 0.03, stTopZ, s * 0.06, r, 0, 0.010, 5);
            }
            // Fork: suspension legs on the MTB, curved rigid blades on the roadster.
            for (int s = -1; s <= 1; s += 2)
            {
                if (roadster)
                {
                    Path3 f = P;
                    f.Add(s * 0.05, headBotY, headBotZ).Add(s * 0.055, (headBotY + r) * 0.5, headBotZ + 0.04).Add(s * 0.06, r, wb);
                    Tube(ref c, frame, f, 0.011, 5);
                }
                else
                {
                    Cyl(ref c, Plain(EngineC), s * 0.055, r, wb, s * 0.055, r + 0.24, wb - 0.08, 0.02, 8);
                    Cyl(ref c, Metal(AlloyC), s * 0.055, r + 0.24, wb - 0.08, s * 0.05, headBotY, headBotZ, 0.014, 6);
                }
            }
            Ell(ref c, Plain(roadster ? frameC : EngineC), Pitched(0, headBotY - 0.01, headBotZ + 0.01, -0.33), 0.07, 0.015, 0.025, 0.3, 0.3, 8);
            // Handlebar and stem.
            double hbY = roadster ? 1.08 : 1.02, hbZ = roadster ? wb - 0.36 : wb - 0.28;
            Rod(ref c, roadster ? chrome : black, 0, headTopY, headTopZ, 0, hbY, roadster ? headTopZ - 0.04 : headTopZ - 0.02, 0.013, 6);
            Path3 hb = P;
            if (roadster) hb.Add(-0.28, hbY, hbZ - 0.12).Add(-0.22, hbY + 0.02, hbZ + 0.02).Add(0.22, hbY + 0.02, hbZ + 0.02).Add(0.28, hbY, hbZ - 0.12);
            else hb.Add(-0.33, hbY, hbZ).Add(0.33, hbY, hbZ);
            Tube(ref c, roadster ? chrome : black, hb, 0.011, 6);
            for (int s = -1; s <= 1; s += 2)
            {
                double gx = roadster ? s * 0.27 : s * 0.30, gz = roadster ? hbZ - 0.10 : hbZ;
                Cyl(ref c, Rubber(roadster ? MeshColor.FromHex(0x4E342E) : TyreC), gx - s * 0.04, hbY, gz + (roadster ? 0.03 : 0), gx + s * 0.06, hbY, gz - (roadster ? 0.04 : 0),
                    0.016, 6);
                if (c.L0)
                {
                    Path3 lv = P;
                    lv.Add(s * 0.18, hbY + 0.01, hbZ + 0.01).Add(gx, hbY - 0.01, gz + 0.06);
                    Tube(ref c, Plain(SteelC), lv, 0.005, 3);
                }
            }
            if (roadster && c.L1)
            {
                // Bell, rod brakes along the frame, chain guard, carrier, mudguards, stand.
                Shapes.Dome(c.M, T(-0.17, hbY + 0.015, hbZ + 0.02), Metal(ChromeC), 0.03, 0.025, 10, true, c.S);
                Rod(ref c, chrome, 0.03, headTopY - 0.05, headTopZ - 0.02, 0.03, stTopY - 0.06, stTopZ + 0.05, 0.006, 4);
                Profile2 cg = Prof.Clear(true);
                cg.Add(-0.02, r + 0.06, true).Add(0.52, bbY + 0.10, true).Add(0.58, bbY, true).Add(0.52, bbY - 0.08, true).Add(-0.02, r - 0.03, true);
                cg.FilletCorners(0.04, 1, true);
                SideExtrude(ref c, frame, cg, -0.095, -0.075, 0.006, 1);
                OBox(ref c, Metal(SteelC), T(0, r + 0.12, -0.12), 0.16, 0.015, 0.36);
                for (int s = -1; s <= 1; s += 2) Rod(ref c, steel, s * 0.07, r + 0.11, -0.25, s * 0.06, r, -0.01, 0.007, 4);
                FenderArc(ref c, frame, 0, r, 0, r + 0.03, 0.065, 0.008, 40, 150, 20);
                FenderArc(ref c, frame, 0, r, wb, r + 0.03, 0.065, 0.008, 20, 150, 20);
                Lamp(ref c, LensC, ChromeC, 0, headBotY + 0.06, headBotZ + 0.08, 0, 0, 1, 0.03, 0.03, 10);
                Decal(ref c, Glass(ReflectC), 0, r + 0.135, -0.31, 0, 0, -1, 0.05, 0.03, 0.008, 0.004);
            }
            else if (c.L1)
            {
                // MTB: disc brakes, knobbly look from the tyres, bottle cage, small reflector.
                for (int k = 0; k < 2; k++)
                {
                    double z = k == 0 ? 0 : wb;
                    Shapes.Cylinder(c.M, AcrossX(-0.045, r, z), Metal(DiscC), 0.08, 0.004, 14, 0, 0, true, true, c.S);
                    OBox(ref c, black, T(-0.05, r + 0.07, z + (k == 0 ? 0.04 : -0.04)), 0.03, 0.05, 0.06);
                }
                Cyl(ref c, Plain(MeshColor.FromHex(0x37474F)), 0, 0.50, 0.64, 0, 0.62, 0.60, 0.025, 8);
                Decal(ref c, Glass(TailC), 0, 0.82, 0.25, 0, 0, -1, 0.04, 0.025, 0.008, 0.004);
            }
            // Saddle and seat post.
            Rod(ref c, steel, 0, stTopY - 0.02, stTopZ, 0, seatY - 0.03, saddleZ + 0.02, 0.012, 6);
            Ell(ref c, Leather(roadster ? MeshColor.FromHex(0x3E2723) : SeatC), Pitched(0, seatY, saddleZ, 0.05), 0.08, 0.035, 0.13, 0.5, 0.8, 8);
            if (roadster && c.L1)
                for (int s = -1; s <= 1; s += 2)
                    Spring(ref c, chrome, s * 0.05, seatY - 0.07, saddleZ - 0.08, s * 0.05, seatY - 0.02, saddleZ - 0.08, 0.016, 0.003, 3);
            // Crankset: chainring, cranks, pedals; chain.
            Shapes.Cylinder(c.M, AcrossX(0.05, bbY, bbZ), Metal(roadster ? ChromeC : AlloyDarkC), 0.10, 0.006, 16, 0, 0, true, true, c.S);
            for (int s = -1; s <= 1; s += 2)
            {
                double a = s > 0 ? 0.9 : 0.9 + Math.PI, cy = bbY + Math.Cos(a) * 0.165, cz = bbZ + Math.Sin(a) * 0.165;
                OBox(ref c, Metal(AlloyDarkC), Affine3.Translation(s * 0.075, (bbY + cy) * 0.5, (bbZ + cz) * 0.5) * Affine3.RotationX(-a), 0.015, 0.18, 0.03);
                if (c.L1) OBox(ref c, Plain(EngineC), T(s * 0.12, cy, cz), 0.09, 0.02, 0.06);
            }
            if (c.L1)
            {
                Path3 ch = P;
                ch.Add(0.05, bbY + 0.10, bbZ).Add(0.06, r + 0.04, 0.0).Add(0.06, r - 0.04, 0.0).Add(0.05, bbY - 0.10, bbZ);
                Tube(ref c, Plain(MeshColor.FromHex(0x50555B)), ch, 0.005, 3);
            }
            SetPlates(0f, 0f, 0f, 0f, 0f, 0f, true);
            s_rider = new RiderRef
            {
                HipY = seatY + 0.05, HipZ = saddleZ + 0.04, GripX = roadster ? 0.27 : 0.30, GripY = hbY, GripZ = roadster ? hbZ - 0.10 : hbZ, PegX = 0.13, PegY = bbY,
                PegZ = bbZ, Pedals = true,
            };
            _ = e;
        }

        // -----------------------------------------------------------------------------------------------------------
        // Cycle rickshaw (ref_vehicles.md, vehicles_traffic.md: Thamel–Asan tourist rickshaws)
        // -----------------------------------------------------------------------------------------------------------

        private static void Rickshaw(ref Ctx c, in VehicleCatalogEntry e, Dims d, in Livery p, bool puller)
        {
            double wb = d.Wheelbase, r = d.WheelR, hw = d.Half;
            ShapeBrush body = Paint(p.Body), hood = Fabric(p.Accent), black = Plain(EngineC), chrome = Chrome(), seat = Leather(MeshColor.FromHex(0x8E1B1B));
            // Rear axle carriage: a painted box under the bench, footboard in front, bench with a backrest.
            Ell(ref c, body, 0, 0.62, 0.02, hw - 0.11, 0.10, 0.31, 0.25, 0.25, 12);
            Lump(ref c, Paint(Shade(p.Body, 0.8f)), T(0, 0.50, 0.42), 0.31, 0.02, 0.17, 0.3);
            if (c.L1) Ell(ref c, seat, 0, 0.78, 0.0, hw - 0.13, 0.06, 0.25, 0.3, 0.3, 12);
            Ell(ref c, seat, 0, 1.02, -0.26, hw - 0.13, 0.21, 0.05, 0.3, 0.3, 12);
            if (c.L1)
            {
                // Painted back panel with a flower and side guards; tassels on the hood edge.
                Decal(ref c, Paint(MeshColor.FromHex(0xFBC02D)), 0, 0.95, -0.312, 0, 0, -1, 0.58, 0.32, 0.05, 0.004);
                Decal(ref c, Paint(MeshColor.FromHex(0xE91E63)), 0, 0.95, -0.312, 0, 0, -1, 0.20, 0.16, 0.08, 0.008);
                for (int s = -1; s <= 1; s += 2) Rod(ref c, chrome, s * (hw - 0.14), 0.84, 0.25, s * (hw - 0.14), 0.92, -0.20, 0.012, 6);
            }
            // Folding hood: a half-dome of fabric ribs over the bench.
            Shapes.Lathe(c.M, Affine3.Translation(0, 1.18, -0.18) * Affine3.RotationZ(Math.PI / 2) * Affine3.RotationY(Math.PI / 2), hood,
                         HoodProfile(0.70, 0.55), 22, c.S, 0, 180, true);
            // Frame from the carriage to the front: the puller's bicycle half.
            Rod(ref c, body, 0, 0.62, 0.30, 0, 0.82, wb - 0.35, 0.03, 10);
            Rod(ref c, body, 0, 0.55, 0.30, 0, 0.45, wb - 0.50, 0.025, 10);
            Rod(ref c, black, 0, 0.45, wb - 0.50, 0, 0.82, wb - 0.35, 0.02, 8);
            for (int s = -1; s <= 1; s += 2) Rod(ref c, body, s * 0.05, 0.82, wb - 0.12, s * 0.06, r, wb, 0.014, 8);
            Rod(ref c, body, 0, 0.82, wb - 0.35, 0, 0.98, wb - 0.17, 0.022, 8);
            Path3 hb = P;
            hb.Add(-0.27, 1.06, wb - 0.30).Add(-0.2, 1.08, wb - 0.16).Add(0.2, 1.08, wb - 0.16).Add(0.27, 1.06, wb - 0.30);
            Tube(ref c, chrome, hb, 0.011, 8);
            if (c.L1)
            {
                // Saddle, seat post, mudguards and the chainring (hidden by the puller or too small far away).
                Ell(ref c, Leather(SeatC), 0, 0.98, wb - 0.62, 0.08, 0.035, 0.13, 0.5, 0.8, 12);
                Rod(ref c, Metal(SteelC), 0, 0.84, wb - 0.55, 0, 0.95, wb - 0.62, 0.012, 6);
                FenderArc(ref c, body, 0, r, wb, r + 0.03, 0.065, 0.008, 20, 140, 24);
                for (int s = -1; s <= 1; s += 2) FenderArc(ref c, body, s * (hw - 0.08), r, 0, r + 0.03, 0.07, 0.008, 40, 110, 24);
                Shapes.Cylinder(c.M, AcrossX(0.04, 0.42, wb - 0.50), Metal(AlloyDarkC), 0.09, 0.006, 20, 0, 0, true, true, c.S);
            }
            if (puller)
            {
                s_rider = new RiderRef
                {
                    HipY = 1.03, HipZ = wb - 0.60, GripX = 0.25, GripY = 1.07, GripZ = wb - 0.27, PegX = 0.12, PegY = 0.42, PegZ = wb - 0.50, Pedals = true,
                };
                Rider(ref c, e, d, p);
            }
            SetPlates(0.55f, -0.30f, 0f, 0f, 0f, 0f, true);
            _ = e;
        }

        /// <summary>A quarter-ellipse hood profile (ribbed canvas) for the half-lathe over the rickshaw bench.</summary>
        private static Profile2 HoodProfile(double w, double h)
        {
            Profile2 p = Prof.Clear(false);
            int n = 7;
            for (int i = 0; i <= n; i++)
            {
                double a = 0.5 * Math.PI * i / n;
                p.Add(Math.Cos(a) * w * 0.5 + 0.0, Math.Sin(a) * h - 0.0, false);
            }
            return p;
        }

        // -----------------------------------------------------------------------------------------------------------
        // Rider (traffic two-wheelers and the rickshaw puller): helmet always (W2_DESIGN 5.2)
        // -----------------------------------------------------------------------------------------------------------

        private static readonly uint[] JacketC = { 0x212121, 0x263A5C, 0x8E2424, 0x4E5B31, 0x5D5F63, 0x1565C0, 0x6D4C41 };
        private static readonly uint[] HelmetC = { 0xFAFAFA, 0x1A1A1A, 0xC62828, 0x1E88E5, 0xFDD835, 0x9E9E9E };

        private static void Rider(ref Ctx c, in VehicleCatalogEntry e, Dims d, in Livery p)
        {
            RiderRef k = s_rider;
            if (k.HipY <= 0) return;
            uint h = VehicleCatalogEntry.Mix((uint)p.Index * 7919u + (uint)c.Model * 104729u + (uint)e.Shape);
            bool bicycle = e.Shape == BodyShape.Bicycle || e.Shape == BodyShape.Rickshaw;
            uint jacket = MeshColor.FromHex(JacketC[h % (uint)JacketC.Length]), helmet = MeshColor.FromHex(HelmetC[(h >> 8) % (uint)HelmetC.Length]);
            uint jeans = MeshColor.FromHex(bicycle && (h & 1) == 0 ? 0x3E3A36u : 0x2F4A6Bu), skin = MeshColor.FromHex(0xB98A62), shoe = MeshColor.FromHex(0x2A2522);
            ShapeBrush cloth = Fabric(jacket), legs = Fabric(jeans), sk = new ShapeBrush(skin, MaterialChannel.Skin), sh = Leather(shoe);
            bool far = c.Lod == VehicleLod.Lod2;
            double hy = k.HipY, hz = k.HipZ, lean = bicycle ? 0.30 : 0.18;
            double shY = hy + 0.50 * Math.Cos(lean), shZ = hz + 0.50 * Math.Sin(lean);
            double len;
            // Pelvis and a torso leaning toward the bars.
            if (!far) Ell(ref c, legs, 0, hy, hz, 0.16, 0.10, 0.12, 0.7, 0.8, 8);
            Affine3 torso = Affine3.Along(0, hy + 0.04, hz, 0, shY, shZ, out len);
            if (far) OBox(ref c, cloth, torso * T(0, 0.5 * len - 0.02, 0), 0.34, len + 0.12, 0.22);
            else Ell(ref c, cloth, torso * T(0, 0.5 * len, 0), 0.19, 0.5 * len + 0.04, 0.12, 0.6, 0.7, c.L0 ? 12 : 8);
            // Neck, head in a helmet with a visor and chin guard.
            double nY = shY + 0.07, nZ = shZ + 0.02, headY = nY + 0.12, headZ = nZ + 0.03;
            if (c.L0) Cyl(ref c, sk, 0, shY - 0.02, shZ, 0, nY + 0.03, nZ + 0.01, 0.045, 6);
            Ell(ref c, Paint(helmet), Pitched(0, headY + 0.02, headZ - 0.005, 0.15), 0.135, 0.14, 0.15, 0.8, 0.9, c.L0 ? 12 : far ? 4 : 8);
            if (c.L1) Decal(ref c, Glass(MeshColor.FromHex(0x1E2A33)), 0, headY + 0.0, headZ + 0.145, 0, 0.05, 1, 0.17, 0.08, 0.03, 0.004);
            if (c.L0) Ell(ref c, Paint(Shade(helmet, 0.85f)), 0, headY - 0.08, headZ + 0.09, 0.10, 0.045, 0.05, 0.7, 0.8, 8);
            for (int s = -1; s <= 1; s += 2)
            {
                // Arms: shoulder → elbow → hand on the grip.
                double sx = s * 0.17, sy = shY - 0.04, sz = shZ;
                double gx = s * k.GripX, gy = k.GripY + 0.02, gz = k.GripZ;
                double ex = s * (Math.Abs(gx) * 0.5 + 0.13), ey = (sy + gy) * 0.5 - 0.08, ez = (sz + gz) * 0.5 - 0.02;
                // Legs: hip → knee → foot on the peg (or the pedal).
                double px = s * 0.10, py = hy - 0.02, pz = hz + 0.04;
                double fx = s * k.PegX, fy = k.PegY + 0.04, fz = k.PegZ;
                if (k.Pedals)
                {
                    double a = s > 0 ? 0.9 : 0.9 + Math.PI;
                    fy = k.PegY + Math.Cos(a) * 0.165 + 0.04;
                    fz = k.PegZ + Math.Sin(a) * 0.165;
                    fx = s * 0.13;
                }
                double kx = s * (Math.Abs(fx) * 0.5 + 0.12), ky = Math.Max(py, fy) + (bicycle ? 0.10 : 0.06), kz = Math.Max(pz, fz) + 0.10;
                if (c.L0)
                {
                    Caps(ref c, cloth, sx, sy, sz, ex, ey, ez, 0.05, 6);
                    Caps(ref c, cloth, ex, ey, ez, gx, gy, gz - 0.03, 0.042, 6);
                    Ell(ref c, Fabric(MeshColor.FromHex(0x2B2B2B)), gx, gy, gz, 0.04, 0.035, 0.045, 0.8, 0.8, 4);
                    Caps(ref c, legs, px, py, pz, kx, ky, kz, 0.065, 6);
                    Caps(ref c, legs, kx, ky, kz, fx, fy + 0.04, fz - 0.02, 0.05, 6);
                    Ell(ref c, sh, fx, fy - 0.01, fz + 0.04, 0.045, 0.04, 0.10, 0.7, 0.8, 8);
                }
                else if (c.L1)
                {
                    Rod(ref c, cloth, sx, sy, sz, ex, ey, ez, 0.05, 4);
                    Rod(ref c, cloth, ex, ey, ez, gx, gy, gz, 0.042, 4);
                    Rod(ref c, legs, px, py, pz, kx, ky, kz, 0.065, 4);
                    Rod(ref c, legs, kx, ky, kz, fx, fy + 0.04, fz - 0.02, 0.05, 4);
                    OBox(ref c, sh, T(fx, fy - 0.01, fz + 0.04), 0.09, 0.08, 0.2);
                }
                else
                {
                    OBar(ref c, cloth, sx, sy, sz, gx, gy, gz, 0.08, 0.08);
                    OBar(ref c, legs, px, py, pz, kx, ky, kz, 0.12, 0.12);
                    OBar(ref c, legs, kx, ky, kz, fx, fy, fz, 0.10, 0.10);
                }
            }
            _ = d;
        }
    }
}
