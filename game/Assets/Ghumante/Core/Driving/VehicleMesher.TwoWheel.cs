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
            // Rear indicators sit on the flanks of the tapering tail, sunk two thirds into it (sampled from the hull itself,
            // so they never hang in the air beside a narrow tail).
            double tz = niu || activa ? -0.455 : -0.49, ty = niu || activa ? 0.66 : 0.74, bz = tz + 0.13, bcv, bhu, bhv;
            HullSectionAt(bz, out bcv, out bhu, out bhv);
            const double blinkR = 0.016;
            double bx = bhu - 0.35 * blinkR, by = bcv + 0.2 * bhv;
            // Rear plate (scooter_ktm ref_04, the Dio, Activa and Ntorq types): under the tail lamp on a black plastic
            // holder that hangs from the underside of the tail, a little ahead of the tail end (sampled from this
            // model's own tail, so the plate never floats behind it); the sporty high tails hang it lower.
            double tailEnd = niu ? -0.45 : activa ? -0.46 : -0.50;
            double pz = tailEnd + (niu || activa ? 0.05 : 0.07), pcv, phu, phv;
            HullSectionAt(pz, out pcv, out phu, out phv);
            double under = pcv - phv, plateTop = under - (niu || activa ? 0.025 : 0.06);
            double py = plateTop - 0.5 * TwoWheelerPlateH * Math.Cos(ScooterPlateTilt);
            if (c.L1) PlateHolder(ref c, under, plateTop, py, pz);
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
                // The engine unit under the rear body (cylinder and crankcase between the CVT case and the silencer),
                // and the exhaust header that carries the silencer.
                RBox(ref c, Plain(EngineC), 0.02, 0.32, 0.36, 0.20, 0.16, 0.30, 0.05, 2);
                if (c.L1)
                {
                    Path3 h = P;
                    h.Add(0.06, 0.26, 0.46).Add(0.10, 0.22, 0.30).Add(0.13, 0.28, 0.16);
                    Tube(ref c, Plain(EngineC), h, 0.018, 6);
                }
                else OBar(ref c, Plain(EngineC), 0.06, 0.26, 0.40, 0.13, 0.28, 0.16, 0.035, 0.035);
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
            Panel(ref c, Glass(TailC), 0, ty, tz, 0, 0.25, -1, niu ? 0.20 : 0.16, 0.045, 0.02, 0.012);
            if (c.L1)
                for (int s = -1; s <= 1; s += 2)
                    Blinker(ref c, s * bx, by, bz, blinkR);

            SetPlates((float)py, (float)pz, 0.60f, (float)(wb + 0.10), (float)ScooterPlateTilt, 0.15f);
            s_rider = new RiderRef
            {
                HipY = seatY + 0.07, HipZ = 0.42 * wb, GripX = 0.30, GripY = hbY + 0.005, GripZ = hbZ - 0.03, PegX = 0.11, PegY = 0.36, PegZ = 0.80,
            };
        }

        /// <summary>Height of the two-wheeler rear plate (<see cref="VehiclePlates.Make"/>, 0.20 × 0.13 m) and the lean of a
        /// scooter's plate (radians, face up).</summary>
        internal const double TwoWheelerPlateH = 0.13, TwoWheelerPlateW = 0.20, ScooterPlateTilt = 0.20;

        /// <summary>
        /// The black plastic plate holder of a scooter: a stem from inside the tail's underside (<paramref name="under"/>)
        /// down to the plate's top edge, flaring into a backing a little larger than the plate, leaning with it; the plate
        /// (<see cref="Plates"/>) sits on the backing's rear face at <paramref name="pz"/>.
        /// </summary>
        private static void PlateHolder(ref Ctx c, double under, double plateTop, double py, double pz)
        {
            ShapeBrush black = Plain(TrimC);
            double stemTop = under + 0.02, stemBottom = plateTop - 0.012;
            RBox(ref c, black, 0, 0.5 * (stemTop + stemBottom), pz + 0.03, 0.12, stemTop - stemBottom, 0.045, 0.012, 2);
            Affine3 back = T(0, py, pz) * Affine3.RotationX(ScooterPlateTilt) * T(0, 0.004, 0.008);
            RBox(ref c, black, back, TwoWheelerPlateW + 0.02, TwoWheelerPlateH + 0.024, 0.016, 0.006, 2);
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
            // Stem up to the middle of the bar (the roadster's swept-back bar sits behind the head tube).
            Rod(ref c, roadster ? chrome : black, 0, headTopY, headTopZ, 0, hbY + 0.02, roadster ? hbZ + 0.02 : headTopZ - 0.02, 0.013, 6);
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
            ShapeBrush body = Paint(p.Body), black = Plain(EngineC), chrome = Chrome(), seat = Leather(MeshColor.FromHex(0x8E1B1B));
            // Rear axle carriage: a painted box under the bench, footboard in front, bench with a backrest.
            Ell(ref c, body, 0, 0.62, 0.02, hw - 0.11, 0.10, 0.31, 0.25, 0.25, 12);
            Lump(ref c, Paint(Shade(p.Body, 0.8f)), T(0, 0.50, 0.42), 0.31, 0.02, 0.17, 0.3);
            // Bench cushion on the carriage (every level: the backrest and the hood stand on it) and the backrest.
            Ell(ref c, seat, 0, 0.78, 0.0, hw - 0.13, 0.06, 0.25, 0.3, 0.3, c.L1 ? 12 : 6);
            Ell(ref c, seat, 0, 1.02, -0.26, hw - 0.13, 0.21, 0.05, 0.3, 0.3, 12);
            if (c.L1)
            {
                // Painted back panel with a flower and side guards; tassels on the hood edge.
                Decal(ref c, Paint(MeshColor.FromHex(0xFBC02D)), 0, 0.95, -0.312, 0, 0, -1, 0.58, 0.32, 0.05, 0.004);
                Decal(ref c, Paint(MeshColor.FromHex(0xE91E63)), 0, 0.95, -0.312, 0, 0, -1, 0.20, 0.16, 0.08, 0.008);
                for (int s = -1; s <= 1; s += 2) Rod(ref c, chrome, s * (hw - 0.14), 0.84, 0.25, s * (hw - 0.14), 0.92, -0.20, 0.012, 6);
            }
            RickshawHood(ref c, p, hw);
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

        /// <summary>Hood geometry of the cycle rickshaw: the pivot line of the bows (across the carriage at the sides of the
        /// backrest), the bows' reach from it, their elevation range (degrees above the forward horizontal: the front bow
        /// leaning forward over the bench, the rear one just behind the vertical), the canvas thickness and the radius of
        /// the arch's top corners.</summary>
        internal const double HoodPivotY = 1.0, HoodPivotZ = -0.18, HoodReach = 0.86, HoodFrontDeg = 52, HoodRearDeg = 96, HoodCanvas = 0.022,
                              HoodCorner = 0.17, HoodInset = 0.09, HoodStretch = 1.25;

        /// <summary>
        /// The folding hood of the Thamel and Asan cycle rickshaws (ref_vehicles.md, photos rickshaw_thamel ref_00,
        /// rickshaw_ktm ref_02/ref_07): a pram hood of canvas stretched over bows that pivot on the sides of the backrest,
        /// as wide as the bench and symmetric about the centre line: flat fan-shaped side panels, a rounded arch on top,
        /// open at the front and closed behind, its front edge leaning out over the passengers' heads. Built as the arch
        /// section (a closed band, so the canvas has an inside and an outside) revolved about the pivot line between the
        /// front and the rear bow, a back panel down to the pivot, raised bows under the canvas, a frilled valance in the
        /// trim colour along the front edge with a row of tassels, and the pivot hubs. Every level keeps the full
        /// silhouette; the small parts drop with the level.
        /// </summary>
        private static void RickshawHood(ref Ctx c, in Livery p, double hw)
        {
            double half = hw - HoodInset;
            uint canvas = p.Accent, trim = Luma(p.Accent) < 0.45f ? MeshColor.FromHex(0xF3E5D0) : MeshColor.FromHex(0xC2185B);
            // Lathe frame: the lathe axis (+Y) runs across the carriage along the pivot line, angle 0 points forward and
            // 90° straight up (local x → world up, local y → world x, local z → world forward). The bows of a real hood
            // pivot at different points, which flattens its top and carries the front edge out over the footboard: the
            // fan is stretched forward by HoodStretch.
            Affine3 xf = Affine3.FromBasis(0, 1, 0, 1, 0, 0, 0, 0, HoodStretch, 0, HoodPivotY, HoodPivotZ);
            int cs = c.L0 ? 4 : c.L1 ? 2 : 1;
            int radial = c.L0 ? 48 : c.L1 ? 24 : 9;
            // The canvas: a closed band (outer arch out, inner arch back), capped at the front and rear bows.
            HoodArch(Prof.Clear(true), half, HoodReach, HoodCorner, HoodCanvas, cs);
            Shapes.Lathe(c.M, xf, Fabric(canvas), Prof, radial, default, HoodFrontDeg, HoodRearDeg - HoodFrontDeg, true);
            // The back panel: the rear bow's arch filled down to the pivot line (a sliver of a lathe with its two caps).
            HoodArch(Prof.Clear(false), half, HoodReach, HoodCorner, 0, cs);
            Shapes.Lathe(c.M, xf, Fabric(Shade(canvas, 0.92f)), Prof, 720, default, HoodRearDeg - 0.6, 0.6, true);
            if (c.L1)
            {
                // The valance: a trim band over the front edge, its frill hanging a little below it.
                HoodArch(Prof.Clear(true), half + 0.006, HoodReach + 0.006, HoodCorner + 0.006, HoodCanvas + 0.012, cs);
                Shapes.Lathe(c.M, xf, Fabric(trim), Prof, radial, default, HoodFrontDeg - 4, 6, true);
                // Bows under the canvas (three near, one mid) and the hubs they pivot on.
                int bows = c.L0 ? 3 : 1;
                for (int k = 0; k < bows; k++)
                {
                    double a = HoodFrontDeg + (HoodRearDeg - HoodFrontDeg) * (k + 1) / (bows + 1);
                    HoodArch(Prof.Clear(true), half + 0.004, HoodReach + 0.004, HoodCorner + 0.004, HoodCanvas + 0.008, Math.Max(1, cs / 2));
                    Shapes.Lathe(c.M, xf, Fabric(Shade(canvas, 0.8f)), Prof, 360, default, a - 1, 2, true);
                }
                for (int s = -1; s <= 1; s += 2)
                    Shapes.Cylinder(c.M, AcrossX(s > 0 ? half - 0.004 : -half - 0.026, HoodPivotY, HoodPivotZ), Metal(ChromeC), 0.035, 0.03, c.L0 ? 12 : 8, 0.008,
                                    1, true, true, c.S);
            }
            if (c.L0)
            {
                // Tassels hanging from the valance round the front opening.
                double fa = HoodFrontDeg * Math.PI / 180, ca = Math.Cos(fa), sa = Math.Sin(fa);
                for (int k = 0; k < 13; k++)
                {
                    double u = k / 12.0;
                    // Along the arch: up the left side, across the top, down the right side (reach r, across x).
                    double r, x;
                    if (u < 0.25) { r = 0.30 + (HoodReach - 0.30) * u / 0.25; x = -half; }
                    else if (u > 0.75) { r = 0.30 + (HoodReach - 0.30) * (1 - u) / 0.25; x = half; }
                    else { r = HoodReach; x = -half + 2 * half * (u - 0.25) / 0.5; }
                    if (Math.Abs(x) > half - HoodCorner && r > HoodReach - HoodCorner)
                    {
                        // Round the arch corner.
                        double cx = Math.Sign(x) * (half - HoodCorner), cr = HoodReach - HoodCorner, dx = x - cx, dr = r - cr, l = Math.Sqrt(dx * dx + dr * dr);
                        if (l > 1e-6)
                        {
                            x = cx + dx / l * HoodCorner;
                            r = cr + dr / l * HoodCorner;
                        }
                    }
                    Ball(ref c, Fabric(k % 2 == 0 ? trim : MeshColor.FromHex(0xFBC02D)), x * 1.01, HoodPivotY + r * sa - 0.035, HoodPivotZ + HoodStretch * r * ca + 0.01, 0.018,
                         6);
                }
            }
        }

        /// <summary>
        /// The hood's arch section in lathe coordinates (x = reach from the pivot line, y = across): an inverted U of half
        /// width <paramref name="half"/> standing from just off the pivot to <paramref name="reach"/>, its top corners
        /// rounded by <paramref name="corner"/>. With a <paramref name="thick"/>ness it is a closed band (the canvas),
        /// otherwise the open outer arch (closed along the axis by the lathe caps: the back panel).
        /// </summary>
        private static void HoodArch(Profile2 pr, double half, double reach, double corner, double thick, int seg)
        {
            const double root = 0.08;
            ArchSide(pr, half, reach, corner, seg, root, false);
            if (thick > 0)
                ArchSide(pr, half - thick, reach - thick, Math.Max(0.02, corner - thick), seg, root, true);
        }

        private static void ArchSide(Profile2 pr, double half, double reach, double corner, int seg, double root, bool inner)
        {
            // Outer: up the −y side, over the top, down the +y side; inner: the same path back.
            int n = 2 * (seg + 1) + 2;
            for (int i = 0; i < n; i++)
            {
                int k = inner ? n - 1 - i : i;
                double x, y;
                bool crease;
                if (k == 0)
                {
                    x = root;
                    y = -half;
                    crease = true;
                }
                else if (k == n - 1)
                {
                    x = root;
                    y = half;
                    crease = true;
                }
                else
                {
                    // Corner arcs: k = 1..seg+1 the −y corner (from the side to the top), then the +y corner.
                    int j = k - 1;
                    bool second = j > seg;
                    if (second) j -= seg + 1;
                    double a = 0.5 * Math.PI * j / seg;
                    double cy = (second ? 1 : -1) * (half - corner), cx = reach - corner;
                    if (!second)
                    {
                        x = cx + corner * Math.Sin(a);
                        y = cy - corner * Math.Cos(a);
                    }
                    else
                    {
                        x = cx + corner * Math.Cos(a);
                        y = cy + corner * Math.Sin(a);
                    }
                    crease = false;
                }
                pr.Add(x, y, crease);
            }
        }

        /// <summary>Perceived brightness of a packed colour (0..1).</summary>
        private static float Luma(uint rgba)
        {
            float r = (rgba >> 24) & 0xFF, g = (rgba >> 16) & 0xFF, b = (rgba >> 8) & 0xFF;
            return (0.299f * r + 0.587f * g + 0.114f * b) / 255f;
        }
    }
}
