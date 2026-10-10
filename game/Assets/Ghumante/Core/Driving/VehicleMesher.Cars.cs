using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Driving
{
    /// <summary>
    /// Cars, taxis, EVs, SUVs, pickups and the police jeep (ref_vehicles.md §2). Every model is one parametric body:
    /// a side profile with real wheel-arch cut-outs extruded across the car with rounded shoulders, narrowed and curved
    /// back at the nose and tail in plan; a glasshouse with tumblehome under a roof panel and pillars; lamps, grille,
    /// bumpers, mirrors, door lines and handles, roof rails, cladding and the class extras (taxi sign, light bar).
    /// </summary>
    public static partial class VehicleMesher
    {
        private enum RearKind : byte
        {
            Hatch = 0,
            Box = 1,
            Pickup = 2,
            DoubleCab = 3,
        }

        private enum LampKind : byte
        {
            /// <summary>Small rectangular lamps flush on the fascia (old 800).</summary>
            Small = 0,

            /// <summary>Big swept lamps wrapping from the hood line round the fender corners (Swift).</summary>
            Swept = 1,

            /// <summary>Swept lamps at the hood corners and boomerang DRLs in the grille's lower corners (i10 Nios).</summary>
            Boomerang = 2,

            /// <summary>Slim LED bar along the hood edge, the main lamps low in the bumper (Nexon, Creta).</summary>
            Split = 3,

            /// <summary>Slim lamps at the hood corners joined by one light bar across the nose (Atto 3).</summary>
            Bar = 4,

            /// <summary>Upright lamps either side of a tall grille on a vertical front (Scorpio, Bolero, Prado).</summary>
            Upright = 5,

            /// <summary>Thin swept lamps over a closed nose (Dolphin).</summary>
            Slim = 6,

            /// <summary>Compact swept lamps over a big lower grille (Alto K10).</summary>
            Compact = 7,
        }

        private enum GrilleKind : byte
        {
            Slot = 0,
            Oval = 1,
            Cascade = 2,
            Closed = 3,
            VerticalSlats = 4,
            Chrome = 5,
            Hex = 6,
            Smile = 7,
        }

        /// <summary>A car model type: side profile heights (m), stations along z (m from the rear axle), widths and the
        /// detail choices.</summary>
        private struct CarSpec
        {
            public double BumperF, BumperR, Sill, ArchGap, NoseY, HoodFrontY, CowlY, Belt, BeltRear, Roof, TailTop;
            public double ScreenBase, ScreenTop, RoofBack, BackBase, NoseDepth;
            public double Bevel, Tumble, NoseTaper, TailTaper, NoseCurve, TailCurve, Bulge;
            public RearKind Rear;
            public LampKind Lamp;
            public GrilleKind Grille;
            public bool FloatingRoof, BlackPillars, RoofRails, Cladding, SpareWheel, SideStep, Ev, FourDoor, Spoiler, ArchLips;
            public double BedFront; // pickups: z where the cab ends and the bed starts
        }

        /// <summary>
        /// The proportions of a car model type that differ from its catalogue entry (ref_vehicles.md §2): the body width
        /// (the Alto type is 1.50 m where the hatchback entry is 1.68 m, the Prado type 1.86 m), how much longer the
        /// front and rear overhangs are (the Dolphin and Atto 3 types are long, the Nexon and Alto types short), and the
        /// wheel look (alloys or steel with a hubcap). Cars, taxis, EVs, SUVs, pickups and the police jeep; other shapes
        /// keep the catalogue values.
        /// </summary>
        private static void CarProportions(in VehicleCatalogEntry e, int model, out double width, out double frontExt, out double rearExt, out WheelStyle style)
        {
            width = e.WidthM;
            frontExt = 0;
            rearExt = 0;
            style = WheelStyle.Car;
            switch (e.TrafficClass)
            {
                case Traffic.VehicleClass.Taxi:
                    width = 1.48;
                    return;
                case Traffic.VehicleClass.Service:
                    if (e.Shape == BodyShape.Van) return; // ambulance
                    width = 1.75;
                    return;
                case Traffic.VehicleClass.Microbus:
                    return;
            }
            switch (e.Shape)
            {
                case BodyShape.Hatchback:
                    if (model == 0)
                    {
                        width = 1.70;
                        frontExt = 0.05;
                        rearExt = 0.03;
                        style = WheelStyle.CarAlloy;
                    }
                    else if (model == 1) width = 1.68;
                    else
                    {
                        width = 1.50;
                        frontExt = -0.06;
                        rearExt = -0.04;
                    }
                    return;
                case BodyShape.Suv:
                    style = WheelStyle.CarAlloy;
                    if (e.Engine == EngineSound.CarEv)
                    {
                        if (model == 0)
                        {
                            width = 1.77;
                            frontExt = 0.10;
                            rearExt = 0.08;
                        }
                        else if (model == 1)
                        {
                            width = 1.78;
                            frontExt = -0.04;
                            rearExt = -0.03;
                        }
                        else
                        {
                            width = 1.79;
                            frontExt = 0.12;
                            rearExt = 0.10;
                        }
                        return;
                    }
                    if (model == 0) width = 1.84;
                    else if (model == 1)
                    {
                        width = 1.79;
                        frontExt = -0.03;
                        rearExt = -0.03;
                    }
                    else
                    {
                        width = 1.86;
                        frontExt = 0.08;
                        rearExt = 0.06;
                    }
                    return;
                case BodyShape.Pickup:
                    if (model == 0) width = 1.70;
                    else
                    {
                        width = 1.73;
                        style = WheelStyle.CarAlloy;
                    }
                    return;
            }
        }

        /// <summary>The catalogue dimensions with a car model type's own width and overhangs.</summary>
        private static Dims CarDims(in VehicleCatalogEntry e, int model)
        {
            Dims d = DimsOf(e);
            double w, fe, re;
            WheelStyle st;
            CarProportions(e, model, out w, out fe, out re, out st);
            d.Width = (float)w;
            d.Front = (float)Math.Max(0.3, d.Front + fe);
            d.Rear = (float)Math.Max(0.25, d.Rear + re);
            d.Length = d.Wheelbase + d.Front + d.Rear;
            return d;
        }

        /// <summary>Sets the side profile: nose (bumper bottom, nose top, hood front, nose depth), cowl and belt (front,
        /// rear), roof, tail top, and the glasshouse stations measured from the nose (screen base, screen top) and the
        /// tail (roof back, back glass base).</summary>
        private static void Profile(ref CarSpec s, double zf, double zr, double noseY, double hoodY, double noseDepth, double belt, double beltRear,
                                    double roof, double screenBase, double screenTop, double roofBack, double backBase)
        {
            s.NoseY = noseY;
            s.HoodFrontY = hoodY;
            s.NoseDepth = noseDepth;
            s.CowlY = belt;
            s.Belt = belt;
            s.BeltRear = beltRear;
            s.Roof = roof;
            s.TailTop = beltRear;
            s.ScreenBase = zf - screenBase;
            s.ScreenTop = zf - screenTop;
            s.RoofBack = zr + roofBack;
            s.BackBase = zr + backBase;
        }

        private static CarSpec SpecFor(in VehicleCatalogEntry e, Dims d, int model)
        {
            double zr = -d.Rear, zf = d.Wheelbase + d.Front, h = d.Height, wb = d.Wheelbase;
            var s = new CarSpec
            {
                BumperF = 0.24, BumperR = 0.28, Sill = 0.30, ArchGap = 0.05, Bevel = 0.14, Tumble = 0.82, NoseTaper = 0.90, TailTaper = 0.94,
                NoseCurve = 0.26, TailCurve = 0.10, FourDoor = true, Bulge = 0.025, ArchLips = true,
            };
            switch (e.TrafficClass)
            {
                case Traffic.VehicleClass.Taxi:
                    if (model == 0)
                    {
                        // Old 800 / Alto taxi: tiny, tall boxy greenhouse, short blunt hood, flush square lamps, slot grille.
                        Profile(ref s, zf, zr, 0.58, 0.76, 0.10, 0.88, 0.90, h - 0.01, 0.82, 1.40, 0.12, 0.03);
                        s.Bevel = 0.09;
                        s.Tumble = 0.88;
                        s.NoseCurve = 0.16;
                        s.Lamp = LampKind.Small;
                        s.Grille = GrilleKind.Slot;
                        s.ArchLips = false;
                    }
                    else
                    {
                        // Tall-boy (Santro / old i10 class): swept lamps, oval grille, tall upright glasshouse.
                        Profile(ref s, zf, zr, 0.60, 0.80, 0.14, 0.92, 0.94, h - 0.01, 0.92, 1.54, 0.10, 0.03);
                        s.Bevel = 0.12;
                        s.Tumble = 0.86;
                        s.Lamp = LampKind.Compact;
                        s.Grille = GrilleKind.Oval;
                    }
                    return s;
                case Traffic.VehicleClass.Service:
                    // Police jeep (Bolero class): upright box.
                    s = BoxSuv(s, zr, zf, h, wb);
                    s.Lamp = LampKind.Upright;
                    s.Grille = GrilleKind.VerticalSlats;
                    return s;
            }
            switch (e.Shape)
            {
                case BodyShape.Hatchback:
                    if (model == 0)
                    {
                        // Swift type: cab-forward, raked screen, rising belt and high rear haunches, black A/B pillars, big
                        // swept lamps wrapping into the fenders over a wide hexagonal grille, roof spoiler, alloys.
                        Profile(ref s, zf, zr, 0.52, 0.80, 0.34, 0.98, 1.07, h - 0.03, 1.14, 1.98, 0.32, 0.12);
                        s.Bevel = 0.17;
                        s.Tumble = 0.78;
                        s.Bulge = 0.035;
                        s.TailCurve = 0.14;
                        s.BlackPillars = true;
                        s.Spoiler = true;
                        s.Lamp = LampKind.Swept;
                        s.Grille = GrilleKind.Hex;
                    }
                    else if (model == 1)
                    {
                        // i10 Nios / Tiago type: tall-boy, upright screen, flat long roof, cascading grille with boomerang
                        // DRLs, steel wheels with full covers.
                        Profile(ref s, zf, zr, 0.56, 0.84, 0.28, 0.97, 1.00, h - 0.02, 1.06, 1.82, 0.20, 0.07);
                        s.Bevel = 0.14;
                        s.Tumble = 0.83;
                        s.Lamp = LampKind.Boomerang;
                        s.Grille = GrilleKind.Cascade;
                    }
                    else
                    {
                        // Alto K10 type: narrow (1.50 m), short hood, tall narrow greenhouse with little tumblehome, upright
                        // screen, compact lamps over a big "smiling" lower grille.
                        Profile(ref s, zf, zr, 0.54, 0.80, 0.24, 0.93, 0.95, h - 0.01, 1.00, 1.68, 0.15, 0.05);
                        s.Bevel = 0.11;
                        s.Tumble = 0.88;
                        s.NoseCurve = 0.20;
                        s.Lamp = LampKind.Compact;
                        s.Grille = GrilleKind.Smile;
                    }
                    return s;
                case BodyShape.Suv:
                    if (e.Engine == EngineSound.CarEv)
                    {
                        // Compact EVs: Dolphin (low rounded hatch, closed nose), Nexon/Punch (compact SUV), Atto 3 (crossover).
                        s.Ev = true;
                        s.Grille = GrilleKind.Closed;
                        s.FloatingRoof = model != 0;
                        s.BlackPillars = true;
                        s.Sill = 0.34;
                        if (model == 0)
                        {
                            // Dolphin type: long, low (1.52 m), smooth closed nose with slim swept lamps, fast hatch.
                            Profile(ref s, zf, zr, 0.50, 0.78, 0.36, 0.98, 1.05, 1.52, 1.22, 2.12, 0.40, 0.10);
                            s.Bevel = 0.18;
                            s.Tumble = 0.78;
                            s.Bulge = 0.035;
                            s.TailCurve = 0.14;
                            s.Spoiler = true;
                            s.Lamp = LampKind.Slim;
                        }
                        else if (model == 1)
                        {
                            // Nexon / Punch EV type: short, tall (1.61 m), upright, connected DRL bar on the hood edge.
                            Profile(ref s, zf, zr, 0.66, 0.90, 0.22, 1.06, 1.10, h + 0.03, 1.10, 1.82, 0.20, 0.06);
                            s.Sill = 0.38;
                            s.Lamp = LampKind.Split;
                            s.Cladding = true;
                            s.RoofRails = true;
                            s.ArchLips = false;
                        }
                        else
                        {
                            // Atto 3 type: long crossover, one light bar across the closed nose, chunky cladding.
                            Profile(ref s, zf, zr, 0.58, 0.86, 0.30, 1.04, 1.10, h, 1.20, 2.02, 0.30, 0.08);
                            s.Bevel = 0.16;
                            s.Tumble = 0.80;
                            s.Lamp = LampKind.Bar;
                            s.Cladding = true;
                            s.RoofRails = true;
                            s.ArchLips = false;
                        }
                        return s;
                    }
                    if (model == 1)
                    {
                        // Creta / Seltos crossover: big parametric grille, split lamps, floating roof.
                        Profile(ref s, zf, zr, 0.74, 0.96, 0.22, 1.10, 1.14, 1.68, 1.24, 1.96, 0.26, 0.06);
                        s.Lamp = LampKind.Split;
                        s.Grille = GrilleKind.Chrome;
                        s.Cladding = true;
                        s.RoofRails = true;
                        s.FloatingRoof = true;
                        s.BlackPillars = true;
                        s.Sill = 0.38;
                        s.ArchLips = false;
                        return s;
                    }
                    s = BoxSuv(s, zr, zf, h, wb);
                    s.Lamp = LampKind.Upright;
                    s.Grille = model == 0 ? GrilleKind.VerticalSlats : GrilleKind.Chrome;
                    s.SpareWheel = model == 2;
                    s.SideStep = true;
                    s.Cladding = true;
                    s.RoofRails = true;
                    return s;
                case BodyShape.Pickup:
                    s = BoxSuv(s, zr, zf, h, wb);
                    s.Rear = model == 0 ? RearKind.Pickup : RearKind.DoubleCab;
                    s.Lamp = model == 0 ? LampKind.Upright : LampKind.Swept; // the Hilux type's lamps sweep back into the fenders
                    s.Grille = model == 0 ? GrilleKind.VerticalSlats : GrilleKind.Chrome;
                    s.Cladding = model == 1;
                    s.SideStep = model == 1;
                    s.Roof = h - 0.06;
                    if (model == 0)
                    {
                        // Bolero Pik-Up type: flat-fronted single cab, long bed.
                        s.ScreenBase = zf - 1.25;
                        s.ScreenTop = zf - 1.70;
                        s.BedFront = s.ScreenTop - 0.62;
                        s.FourDoor = false;
                    }
                    else
                    {
                        // Hilux type double cab: two rows of doors under a long roof, a short bed (≈ 1.5 m) behind.
                        s.ScreenBase = zf - 1.12;
                        s.ScreenTop = zf - 1.62;
                        s.BedFront = zr + 1.55;
                        s.NoseY = 0.86;
                        s.HoodFrontY = 1.02;
                        s.NoseDepth = 0.14;
                        s.Belt = s.CowlY = s.BeltRear = s.TailTop = 1.10;
                        s.FourDoor = true;
                    }
                    s.RoofBack = s.BedFront + 0.10;
                    s.BackBase = s.BedFront + 0.02;
                    return s;
            }
            return BoxSuv(s, zr, zf, h, wb);
        }

        /// <summary>The upright box SUV profile (Scorpio / Bolero / Prado class).</summary>
        private static CarSpec BoxSuv(CarSpec s, double zr, double zf, double h, double wb)
        {
            s.Rear = RearKind.Box;
            s.BumperF = 0.30;
            s.BumperR = 0.34;
            s.Sill = 0.42;
            Profile(ref s, zf, zr, 0.94, 1.04, 0.06, 1.13, 1.15, h - 0.08, 1.30, 1.75, 0.06, 0.03);
            s.Bevel = 0.09;
            s.Tumble = 0.90;
            s.NoseTaper = 0.95;
            s.TailTaper = 0.97;
            s.NoseCurve = 0.10;
            s.TailCurve = 0.04;
            s.Bulge = 0.012;
            s.ArchLips = false;
            _ = wb;
            return s;
        }

        private static void Car(ref Ctx c, in VehicleCatalogEntry e, Dims d0, in Livery p)
        {
            Dims d = CarDims(e, c.Model);
            CarSpec s = SpecFor(e, d, c.Model);
            double zr = -d.Rear, zf = d.Wheelbase + d.Front, hw = d.Half, wb = d.Wheelbase, R = d.WheelR;
            uint body = p.Body, roofC = s.FloatingRoof ? MeshColor.FromHex(0x1F2226) : p.Roof;
            ShapeBrush paint = Paint(body), black = Plain(TrimC);
            bool taxi = e.TrafficClass == Traffic.VehicleClass.Taxi, police = e.TrafficClass == Traffic.VehicleClass.Service;
            double A = R + s.ArchGap;
            int v0 = c.M.VertexCount;
            _ = d0;

            // ---- Lower body: a smooth hull through rounded sections whose bottoms rise over the wheels (the arches)
            // and whose tops follow the bumper, nose, hood, belt and tail; the flanks bulge a little (shoulders and
            // haunches) instead of standing flat.
            bool bed = s.Rear == RearKind.Pickup || s.Rear == RearKind.DoubleCab;
            int h0 = c.M.VertexCount;
            CarBodyHull(ref c, s, zr, zf, hw - s.Bulge * hw, R, wb, A, bed, paint);

            // Dark wheel-house tub between the arches (seen through them behind the tyres), kept under the hood and the
            // tail at the arches' outer ends so it never pokes through a low bonnet (the Dolphin type's).
            double wW = CarWheelWidth(2 * hw), tubTop = Math.Min(R + A + 0.02, Math.Min(BodyTop(s, zr, zf, wb + A, bed), BodyTop(s, zr, zf, -A, bed)) - 0.05);
            double tubBot = s.Sill - 0.15;
            OBox(ref c, Plain(UnderC, 0.3f), T(0, 0.5 * (tubBot + tubTop), 0.5 * wb), 2 * (hw - wW - 0.03), tubTop - tubBot, wb + 2 * A);

            // ---- Front: grille, intake, lamps, fog lamps, bumper lip.
            CarFront(ref c, s, zf, hw, body);
            // ---- Rear: lamps, bumper, reflectors, spoiler, exhaust.
            CarRear(ref c, s, zr, hw, body);
            // ---- Sides: door shut lines, handles, sill trim, cladding, arch lips.
            if (c.L1) CarSides(ref c, s, d, body);
            // The flanks bulge a little (shoulders and haunches); everything on the lower body so far (lamps lying on the
            // nose, arch lips, door lines, cladding) bulges with them, so nothing sinks in or stands off.
            BulgeX(c.M, h0, s.Sill - 0.02, s.Belt + 0.08, s.Bulge * 1.15);

            // ---- Glasshouse, roof, pillars (tumblehome applied to all of it).
            int g0 = c.M.VertexCount;
            double belt = s.Belt - 0.02, hwc = hw - 0.06, roofY = s.Roof - 0.025;
            double backBase = s.BackBase;
            double crown = s.Rear == RearKind.Box || bed ? 0.01 : 0.03;
            CabinHull(ref c, s, belt, roofY, crown, hwc, bed, backBase, s.BlackPillars ? black : paint, c.L1);
            // The roof skin: everything on top between the headers (and the sections just inside them) takes the roof
            // colour.
            RoofSkin(c.M, g0, s.ScreenTop - 0.5 * HeaderInset, s.RoofBack + 0.5 * HeaderInset, belt + 0.6 * (roofY - belt), roofC);
            if (c.L1)
            {
                // C pillars (body colour, black on the floating-roof types), the black B pillar on each side (two on the
                // double cab), before the tumblehome so they lean with it. Both overlap the cabin side and stop where its
                // rounded roof edge begins (the roof colour takes over there), so nothing stands proud of the roof line.
                double sideTopB = CabinSideTop(roofY, belt), sideTopC = CabinSideTop(roofY - 1.5 * crown, belt);
                for (int side = -1; side <= 1; side += 2)
                {
                    double x0 = side * (hwc - 0.01), x1 = side * (hwc + 0.012);
                    double zB = BPillarZ(s);
                    OBox(ref c, Plain(TrimC), T(0.5 * (x0 + x1), 0.5 * (belt + sideTopB), zB), Math.Abs(x1 - x0), sideTopB - belt, 0.07);
                    if (!bed)
                    {
                        Profile2 cp = Prof2.Clear(true);
                        double zC = s.RoofBack + (s.Rear == RearKind.Box ? 0.30 : 0.52);
                        cp.Add(zC, belt, true).Add(backBase + 0.02, s.BeltRear - 0.02, true).Add(s.RoofBack + 0.01, sideTopC, true).Add(zC + 0.12, sideTopC, true);
                        SideExtrude(ref c, s.FloatingRoof ? black : paint, cp, Math.Min(x0, x1), Math.Max(x0, x1), 0.004, 1);
                    }
                }
            }
            TaperByY(c.M, g0, belt, roofY, s.Tumble);
            // Roof rails.
            double hwr = hwc * s.Tumble;
            if (c.L1 && s.RoofRails)
                for (int side = -1; side <= 1; side += 2)
                {
                    Rod(ref c, Plain(AlloyDarkC), side * (hwr - 0.08), roofY + 0.07, s.ScreenTop + 0.05, side * (hwr - 0.08), roofY + 0.07, s.RoofBack + 0.02, 0.018, 6);
                    if (c.L0)
                        for (int k = 0; k < 2; k++)
                        {
                            double z = k == 0 ? s.ScreenTop + 0.08 : s.RoofBack + 0.06;
                            OBox(ref c, Plain(AlloyDarkC), T(side * (hwr - 0.08), roofY + 0.045, z), 0.03, 0.05, 0.06);
                        }
                }
            if (c.L0)
            {
                // Wipers resting on the windscreen base; a rear wiper on hatches.
                for (int k = -1; k <= 1; k += 2)
                    OBar(ref c, Plain(TrimC), k * 0.05, belt + 0.03, s.ScreenBase - 0.06, k * 0.05 - 0.42, belt + 0.05, s.ScreenBase - 0.10, 0.012, 0.012);
                if (s.Rear == RearKind.Hatch) OBar(ref c, Plain(TrimC), 0, s.BeltRear + 0.04, backBase + 0.02, -0.25, s.BeltRear + 0.12, backBase + 0.06, 0.01, 0.01);
            }

            // ---- Class extras on the roof.
            if (taxi)
            {
                // Roof sign: a yellow box with a black band.
                double zs = 0.5 * (s.ScreenTop + s.RoofBack);
                RBox(ref c, Paint(p.Sign), 0, roofY + 0.11, zs, 0.52, 0.14, 0.20, 0.03, 2);
                if (c.L1)
                    for (int k = -1; k <= 1; k += 2) Decal(ref c, Plain(TrimC), 0, roofY + 0.11, zs + k * 0.101, 0, 0, k, 0.40, 0.05, 0.01, 0.002);
            }
            if (police)
            {
                double zs = 0.5 * (s.ScreenTop + s.RoofBack) + 0.15;
                RBox(ref c, Plain(TrimC), 0, roofY + 0.065, zs, 1.0, 0.04, 0.22, 0.015, 1);
                for (int k = -1; k <= 1; k += 2)
                    RBox(ref c, Glass(k < 0 ? MeshColor.FromHex(0xD32F2F) : MeshColor.FromHex(0x1E88E5)), k * 0.24, roofY + 0.11, zs, 0.44, 0.07, 0.18, 0.03, 2);
            }
            if (s.Rear == RearKind.Box && s.SpareWheel)
            {
                // Rear-mounted spare wheel with a cover.
                Shapes.Cylinder(c.M, AlongMinusZ(0, 0.80, zr), Fabric(MeshColor.FromHex(0x37474F)), R * 0.95, 0.22, 18, 0.05, 2, true, true, c.S);
            }
            if (bed) PickupBed(ref c, s, zr, hw, body, R, wb);

            // ---- Mirrors on the doors.
            if (c.L1)
                for (int side = -1; side <= 1; side += 2)
                {
                    double mx = side * (hw + 0.05), my = s.Belt + 0.07, mz = s.ScreenBase - 0.12;
                    OBox(ref c, Plain(TrimC), T(side * (hw + 0.01), my - 0.03, mz + 0.03), 0.04, 0.05, 0.08);
                    Lump(ref c, s.BlackPillars ? black : paint, T(mx, my, mz), 0.06, 0.05, 0.04, 0.5);
                    if (c.L0) Decal(ref c, Glass(MirrorC), mx, my, mz - 0.041, side * 0.1, 0, -1, 0.10, 0.08, 0.03, 0.003);
                }

            // ---- Plan shaping: narrow and curve the nose and the tail.
            TaperByZ(c.M, v0, wb + 0.25, zf, s.NoseTaper);
            TaperByZ(c.M, v0, -0.25, zr, s.TailTaper);
            CurveEnd(c.M, v0, zf - 0.45, zf, s.NoseCurve);
            CurveEnd(c.M, v0, zr + 0.35, zr, -s.TailCurve);
            SetPlates((float)(s.BumperR + 0.20), (float)zr - 0.012f, (float)(s.BumperF + 0.15), (float)zf + 0.012f);
        }

        /// <summary>Where the B pillar stands: between the doors (four-door cars) or behind the only door.</summary>
        private static double BPillarZ(in CarSpec s)
        {
            if (s.Rear == RearKind.DoubleCab) return 0.5 * (s.ScreenTop + s.RoofBack) + 0.10;
            return 0.5 * (s.ScreenTop + s.RoofBack) + (s.FourDoor ? 0.05 : -0.15);
        }

        /// <summary>Tyre width of a car or van wheel for a body of this width, on a 2 cm grid; alloys sit on the odd
        /// centimetre so a wheel's style follows from its size (<see cref="StyleFor"/>).</summary>
        internal static float CarWheelWidth(double bodyWidth, bool alloy = false)
        {
            double w = Math.Max(0.16, Math.Min(0.28, 0.11 * bodyWidth + 0.04));
            return (float)(0.02 * Math.Round(w * 50) + (alloy ? 0.01 : 0));
        }

        [ThreadStatic] private static double[] s_st;

        private static void AddStation(ref int n, double z)
        {
            if (s_st == null) s_st = new double[128];
            if (n == s_st.Length) Array.Resize(ref s_st, n * 2);
            s_st[n++] = z;
        }

        /// <summary>Sorts and de-duplicates the station list (within 1 cm).</summary>
        private static int SortStations(int n)
        {
            Array.Sort(s_st, 0, n);
            int k = 0;
            for (int i = 0; i < n; i++)
                if (k == 0 || s_st[i] - s_st[k - 1] > 0.01) s_st[k++] = s_st[i];
            return k;
        }

        /// <summary>Height of the lower body's top line at z: tail, belt, cowl, hood, curved nose.</summary>
        private static double BodyTop(in CarSpec s, double zr, double zf, double z, bool bed)
        {
            double ns = zf - s.NoseDepth;
            if (z >= ns)
            {
                double cth = Math.Max(-1, Math.Min(1, 1 - (zf - z) / Math.Max(1e-3, s.NoseDepth)));
                return s.NoseY + (s.HoodFrontY - s.NoseY) * Math.Sqrt(Math.Max(0, 1 - cth * cth));
            }
            if (z >= s.ScreenBase) return Lerp(s.CowlY, s.HoodFrontY, (z - s.ScreenBase) / Math.Max(1e-3, ns - s.ScreenBase));
            if (bed)
            {
                if (z >= s.BedFront) return Lerp(s.Belt, s.CowlY, (z - s.BedFront) / Math.Max(1e-3, s.ScreenBase - s.BedFront));
                return Lerp(s.Belt - 0.02, s.Belt, (z - zr) / Math.Max(1e-3, s.BedFront - zr));
            }
            if (z >= s.BackBase) return Lerp(s.BeltRear, s.CowlY, (z - s.BackBase) / Math.Max(1e-3, s.ScreenBase - s.BackBase));
            return Lerp(s.TailTop, s.BeltRear, (z - zr) / Math.Max(1e-3, s.BackBase - zr));
        }

        private static double Lerp(double a, double b, double t)
        {
            return a + (b - a) * (t < 0 ? 0 : t > 1 ? 1 : t);
        }

        /// <summary>The lower body as a hull: stations densest round the arches and the nose.</summary>
        private static void CarBodyHull(ref Ctx c, in CarSpec s, double zr, double zf, double hw, double R, double wb, double A, bool bed, in ShapeBrush paint)
        {
            int n = 0, archN = c.L0 ? 8 : c.L1 ? 4 : 3;
            double rEndF = 0.10, rEndR = 0.08, step = c.L0 ? 0.30 : c.L1 ? 0.55 : 0.9;
            int endN = c.L0 ? 3 : c.L1 ? 2 : 1;
            for (int i = 0; i <= endN; i++)
            {
                double ph = 0.5 * Math.PI * i / endN;
                AddStation(ref n, zr + rEndR * (1 - Math.Cos(ph)));
                AddStation(ref n, zf - rEndF * (1 - Math.Cos(ph)));
            }
            for (int w = 0; w < 2; w++)
            {
                double zw = w == 0 ? 0 : wb;
                for (int i = 0; i <= archN; i++) AddStation(ref n, zw - A + 2 * A * i / archN);
            }
            for (double z = zr + rEndR + step; z < -A; z += step) AddStation(ref n, z);
            for (double z = A + step; z < wb - A; z += step) AddStation(ref n, z);
            for (double z = wb + A + step; z < zf - rEndF; z += step) AddStation(ref n, z);
            AddStation(ref n, s.ScreenBase);
            if (!bed) AddStation(ref n, s.BackBase);
            else AddStation(ref n, s.BedFront);
            if (c.L1)
                for (int i = 0; i <= 3; i++) AddStation(ref n, zf - s.NoseDepth * (1 - Math.Cos(0.5 * Math.PI * i / 3)));
            n = SortStations(n);
            double sill = Math.Min(s.Sill, R - 0.01);
            HullBegin();
            for (int i = 0; i < n; i++)
            {
                double z = s_st[i];
                if (z < zr - 1e-6 || z > zf + 1e-6) continue;
                double yb = sill;
                double d0 = z, d1 = z - wb;
                if (Math.Abs(d0) < A) yb = Math.Max(yb, R + Math.Sqrt(A * A - d0 * d0));
                else if (Math.Abs(d1) < A) yb = Math.Max(yb, R + Math.Sqrt(A * A - d1 * d1));
                else if (z < -A) yb = Lerp(s.BumperR, sill, (z - zr) / Math.Max(1e-3, -A - zr));
                else if (z > wb + A) yb = Lerp(sill, s.BumperF, (z - wb - A) / Math.Max(1e-3, zf - wb - A));
                double yt = BodyTop(s, zr, zf, z, bed), hu = hw;
                // Rounded ends: the last stations shrink in plan and height.
                double eF = zf - z < rEndF ? rEndF - Math.Sqrt(Math.Max(0, rEndF * rEndF - (rEndF - (zf - z)) * (rEndF - (zf - z)))) : 0;
                double eR = z - zr < rEndR ? rEndR - Math.Sqrt(Math.Max(0, rEndR * rEndR - (rEndR - (z - zr)) * (rEndR - (z - zr)))) : 0;
                double e = Math.Max(eF, eR);
                hu -= e;
                yb += 0.6 * e;
                yt -= 0.3 * e;
                if (yt < yb + 0.04) yt = yb + 0.04;
                double hv = 0.5 * (yt - yb);
                HullAdd(z, 0, 0.5 * (yt + yb), hu, hv, Math.Min(s.Bevel * 1.25, 0.95 * hv), Math.Min(0.07, 0.9 * hv));
            }
            HullEmit(ref c, paint, 3, true, true);
        }

        /// <summary>Distance inside the roof headers of the extra cabin sections that carry the roof colour (so even the
        /// LOD2 roof is a painted panel between glass, not a glass ridge).</summary>
        private const double HeaderInset = 0.05;

        /// <summary>Corner segments of a hull emitted with <paramref name="seg"/> at the context's level (as
        /// <see cref="HullEmit"/> uses them).</summary>
        private static int HullCornerSegments(ref Ctx c, int seg)
        {
            return c.Lod == VehicleLod.Lod0 ? seg : c.Lod == VehicleLod.Lod1 ? Math.Max(1, seg / 3) : 1;
        }

        /// <summary>Top radius of a cabin section of half height <paramref name="hv"/> (the rounded roof edge).</summary>
        private static double CabinEdgeRadius(double hv)
        {
            return Math.Min(0.14, 0.95 * hv);
        }

        /// <summary>
        /// The glasshouse as a hull: windscreen, crowned roof and back glass, rounded roof edges; extra sections just
        /// inside both roof headers carry the roof colour at every level. With <paramref name="pillar"/> (LOD0/LOD1) the A
        /// pillars run along the hull's own rounded windscreen edge (the 45° point of each section's top corner, half sunk
        /// into the surface), so they follow the rake, the crown and the corner radius instead of standing off them.
        /// </summary>
        private static void CabinHull(ref Ctx c, in CarSpec s, double belt, double roofY, double crown, double hwc, bool bed, double backBase,
                                      in ShapeBrush pillar, bool pillars)
        {
            Profile2 tp = Prof2.Clear(false);
            double roofMid = 0.5 * (s.ScreenTop + s.RoofBack), yb = belt - 0.05;
            // The headers are creases: the spline rounds the crowned roof between them without humping over them.
            tp.Add(backBase, bed ? belt : s.BeltRear - 0.02, true).Add(s.RoofBack, roofY - 1.5 * crown, true).Add(roofMid, roofY, false)
              .Add(s.ScreenTop, roofY - crown, true).Add(s.ScreenBase - 0.03, belt, true);
            tp.Smooth(c.L0 ? 4 : c.L1 ? 2 : 1);
            int n = 0;
            for (int i = 0; i < tp.Count; i++) AddStation(ref n, tp.X[i]);
            AddStation(ref n, s.RoofBack + HeaderInset);
            AddStation(ref n, s.ScreenTop - HeaderInset);
            n = SortStations(n);
            HullBegin();
            for (int i = 0; i < n; i++)
            {
                double z = s_st[i], yt = SampleTop(tp, z);
                double hv = Math.Max(0.02, 0.5 * (yt - yb));
                HullAdd(z, 0, 0.5 * (yt + yb), hwc, hv, CabinEdgeRadius(hv), 0.01);
            }
            HullEmit(ref c, Glass(GlassC), 3, true, true);
            if (!pillars) return;
            // A pillars: the 45° point of the top corner of each section from the cowl to the screen header. A corner of
            // k segments has a vertex there (k even) or the middle of a chord (k odd).
            int k = HullCornerSegments(ref c, 3), np = c.L0 ? 6 : 3;
            double radial = (k & 1) == 0 ? 1 : Math.Cos(Math.PI / (4.0 * k)), rodR = c.L0 ? 0.026 : 0.03;
            for (int side = -1; side <= 1; side += 2)
            {
                Path3 path = P;
                for (int i = 0; i <= np; i++)
                {
                    double z = Lerp(s.ScreenBase - 0.03, s.ScreenTop, (double)i / np), yt = SampleTop(tp, z);
                    double hv = Math.Max(0.02, 0.5 * (yt - yb)), r = Math.Min(CabinEdgeRadius(hv), 0.999 * hv);
                    double d = r * radial + 0.35 * rodR;
                    path.Add(side * (hwc - r + 0.7071 * d), yt - r + 0.7071 * d, z);
                }
                Tube(ref c, pillar, path, rodR, c.L0 ? 8 : 5);
            }
        }

        /// <summary>Top of the vertical side of the cabin at a section whose top is <paramref name="yt"/> (where the
        /// rounded roof edge begins).</summary>
        private static double CabinSideTop(double yt, double belt)
        {
            double hv = Math.Max(0.02, 0.5 * (yt - (belt - 0.05)));
            return yt - Math.Min(CabinEdgeRadius(hv), 0.999 * hv);
        }

        /// <summary>Points of a wheel arch over the wheel at z = <paramref name="zw"/>: from the rear foot over the top to
        /// the front foot (smooth points).</summary>
        private static void ArchPoints(Profile2 p, double zw, double R, double A, double th0, int n)
        {
            for (int i = 0; i <= n; i++)
            {
                double a = Math.PI - th0 - (Math.PI - 2 * th0) * i / n;
                p.Add(zw + A * Math.Cos(a), R + A * Math.Sin(a), i == 0 || i == n);
            }
        }

        /// <summary>A point on the nose at height y: the vertical fascia below the nose top, the sloped nose above it.</summary>
        private static void NoseAt(in CarSpec s, double zf, double y, out double z, out double ny, out double nz)
        {
            double rise = s.HoodFrontY - s.NoseY;
            if (y <= s.NoseY || rise <= 1e-3)
            {
                z = zf;
                ny = 0;
                nz = 1;
                return;
            }
            double th = Math.Asin(Math.Min(1, (y - s.NoseY) / rise)), tz = s.NoseDepth * Math.Sin(th), ty = rise * Math.Cos(th);
            double l = Math.Sqrt(tz * tz + ty * ty);
            z = zf - s.NoseDepth * (1 - Math.Cos(th));
            ny = tz / l;
            nz = ty / l;
        }

        /// <summary>Paints the roof of a glasshouse: vertices from <paramref name="from"/> between the rear and front
        /// headers (z) that face up and lie above <paramref name="yMin"/> take the roof colour and the paint channel.</summary>
        private static void RoofSkin(MeshData m, int from, double zFront, double zBack, double yMin, uint color)
        {
            for (int v = from; v < m.VertexCount; v++)
            {
                double y = m.Positions[3 * v + 1], z = m.Positions[3 * v + 2], ny = m.Normals[3 * v + 1];
                if (z > zFront || z < zBack || y < yMin || ny < 0.4) continue;
                m.Colors[4 * v] = (byte)(color >> 24);
                m.Colors[4 * v + 1] = (byte)(color >> 16);
                m.Colors[4 * v + 2] = (byte)(color >> 8);
                m.Uv0[2 * v] = (float)MaterialChannel.Paint;
            }
        }

        /// <summary>Signed distance (m) from cross-plane point (x, y) to section <paramref name="i"/> of the current hull
        /// (a rounded rectangle; negative inside).</summary>
        private static double HullSectionSd(int i, double x, double y)
        {
            int o = 7 * i;
            double cu = s_hull[o + 1], cv = s_hull[o + 2], hu = s_hull[o + 3], hv = s_hull[o + 4], lim = 0.999 * Math.Min(hu, hv);
            double r = Math.Min(Math.Max(0, s_hull[y >= cv ? o + 5 : o + 6]), lim);
            double qx = Math.Abs(x - cu) - (hu - r), qy = Math.Abs(y - cv) - (hv - r), ox = Math.Max(qx, 0), oy = Math.Max(qy, 0);
            return Math.Sqrt(ox * ox + oy * oy) + Math.Min(Math.Max(qx, qy), 0) - r;
        }

        /// <summary>
        /// Where a ray along the current hull's axis, through cross-plane point (x, y), first meets the hull coming in from
        /// its front end (<paramref name="front"/>) or its back end: the end cap when the point lies inside the end section,
        /// otherwise the station (interpolated) where the sections grow to take the point in. NaN when it never does.
        /// </summary>
        internal static double HullEndT(double x, double y, bool front)
        {
            int n = s_hullN;
            if (n < 2) return double.NaN;
            int step = front ? -1 : 1, i = front ? n - 1 : 0;
            double prev = HullSectionSd(i, x, y);
            if (prev <= 0) return s_hull[7 * i];
            for (int j = i + step; j >= 0 && j < n; j += step)
            {
                double sd = HullSectionSd(j, x, y);
                if (sd <= 0)
                {
                    double t0 = s_hull[7 * (j - step)], t1 = s_hull[7 * j];
                    return t0 + (t1 - t0) * prev / Math.Max(1e-9, prev - sd);
                }
                prev = sd;
            }
            return double.NaN;
        }

        /// <summary>A vertex on the end surface of the current hull at (x, y), lifted <paramref name="lift"/> along the
        /// surface normal (finite differences of <see cref="HullEndT"/>).</summary>
        private static int HullSurfaceVertex(ref Ctx c, in ShapeBrush b, double x, double y, bool front, double lift)
        {
            double z = HullEndT(x, y, front);
            if (double.IsNaN(z)) z = front ? s_hull[7 * (s_hullN - 1)] : s_hull[0];
            const double e = 0.006;
            double dx = (OrZ(HullEndT(x + e, y, front), z) - OrZ(HullEndT(x - e, y, front), z)) / (2 * e);
            double dy = (OrZ(HullEndT(x, y + e, front), z) - OrZ(HullEndT(x, y - e, front), z)) / (2 * e);
            dx = Math.Max(-25, Math.Min(25, dx));
            dy = Math.Max(-25, Math.Min(25, dy));
            double sg = front ? 1 : -1, nx = -sg * dx, ny = -sg * dy, nz = sg;
            ShapeEmit.Normalize(ref nx, ref ny, ref nz);
            return c.M.AddVertex((float)(x + nx * lift), (float)(y + ny * lift), (float)(z + nz * lift), (float)nx, (float)ny, (float)nz, b.Color,
                                 (float)b.Channel, b.Ao);
        }

        private static double OrZ(double v, double z)
        {
            return double.IsNaN(v) ? z : v;
        }

        /// <summary>
        /// A flat graphic lying on the front (or back) end of the current hull, the way a lamp lens, a light bar or a
        /// grille insert follows a curved nose: the closed outline (x, y pairs, <paramref name="n"/> points, the vehicle's
        /// right side), scaled by <paramref name="grow"/> about its centroid, filled with <paramref name="rings"/>
        /// concentric rings, every vertex projected along the hull axis onto the surface and lifted
        /// <paramref name="lift"/> off it. Mirrored to the left side unless the outline straddles the centre line
        /// (<paramref name="mirror"/> false). Plan shaping applied afterwards bends it with the body.
        /// </summary>
        private static void HullPatch(ref Ctx c, in ShapeBrush b, double[] xy, int n, bool front, double lift, double grow, int rings, bool mirror = true)
        {
            double cx = 0, cy = 0;
            for (int i = 0; i < n; i++)
            {
                cx += xy[2 * i];
                cy += xy[2 * i + 1];
            }
            cx /= n;
            cy /= n;
            for (int side = mirror ? -1 : 1; side <= 1; side += 2)
            {
                int centre = HullSurfaceVertex(ref c, b, side * cx, cy, front, lift), first = c.M.VertexCount;
                for (int r = 1; r <= rings; r++)
                {
                    double f = grow * r / rings;
                    for (int i = 0; i < n; i++)
                        HullSurfaceVertex(ref c, b, side * (cx + f * (xy[2 * i] - cx)), cy + f * (xy[2 * i + 1] - cy), front, lift);
                }
                for (int i = 0; i < n; i++) ShapeEmit.TriOriented(c.M, centre, first + i, first + (i + 1) % n);
                for (int r = 1; r < rings; r++)
                {
                    int a = first + (r - 1) * n, o = first + r * n;
                    for (int i = 0; i < n; i++)
                    {
                        int i1 = (i + 1) % n;
                        ShapeEmit.TriOriented(c.M, a + i, a + i1, o + i1);
                        ShapeEmit.TriOriented(c.M, a + i, o + i1, o + i);
                    }
                }
            }
        }

        [ThreadStatic] private static double[] s_lamp;

        /// <summary>
        /// Maps a lamp outline given in nose units onto the car (u = fraction of the nose half width, v = 0 at the nose
        /// top line, 1 at the hood's front edge) and smooths it (Catmull-Rom, closed) at the context's level; returns the
        /// point count in <see cref="s_lamp"/>.
        /// </summary>
        private static int LampOutline(ref Ctx c, double[] uv, double halfW, double y0, double y1, double du = 0, double dv = 0)
        {
            Profile2 p = Prof2.Clear(true);
            for (int i = 0; i + 1 < uv.Length; i += 2) p.Add(halfW * (uv[i] + du), y0 + (y1 - y0) * (uv[i + 1] + dv), false);
            if (!c.L1)
            {
                // Far: the raw corner points.
            }
            else p.Smooth(c.L0 ? 2 : 1);
            int n = p.Count;
            if (s_lamp == null || s_lamp.Length < 2 * n) s_lamp = new double[Math.Max(128, 2 * n)];
            for (int i = 0; i < n; i++)
            {
                s_lamp[2 * i] = p.X[i];
                s_lamp[2 * i + 1] = p.Y[i];
            }
            return n;
        }

        /// <summary>
        /// A headlamp (or tail lamp) pair lying on the nose (or tail) of the body hull: a dark housing border, the lens,
        /// and at LOD0 a chrome reflector with a dark projector eye near the inner end and an LED strip along the lower
        /// edge. Outline in nose units (<see cref="LampOutline"/>).
        /// </summary>
        private static void HullLamps(ref Ctx c, double[] uv, double halfW, double y0, double y1, bool front, uint lens, bool eye, bool strip)
        {
            int n = LampOutline(ref c, uv, halfW, y0, y1);
            int rings = c.L1 ? 2 : 1;
            if (c.L1) HullPatch(ref c, Plain(MeshColor.FromHex(0x1B1E22)), s_lamp, n, front, 0.004, 1.10, rings);
            HullPatch(ref c, Glass(lens), s_lamp, n, front, 0.008, 1.0, rings);
            if (!c.L0) return;
            // Smoked inner housing (a smaller copy of the outline) and the projector: a chrome ring round a bright lens.
            HullPatch(ref c, Metal(front ? MeshColor.FromHex(0x5C6670) : Shade(lens, 0.72f)), s_lamp, n, front, 0.010, 0.70, 1);
            double ex = 0, ey = 0;
            for (int i = 0; i < n; i++)
            {
                ex += s_lamp[2 * i];
                ey += s_lamp[2 * i + 1];
            }
            ex /= n;
            ey /= n;
            if (eye)
            {
                // The projector sits toward the inner end.
                double ix = s_lamp[0], iy = s_lamp[1];
                for (int i = 0; i < n; i++)
                    if (s_lamp[2 * i] < ix)
                    {
                        ix = s_lamp[2 * i];
                        iy = s_lamp[2 * i + 1];
                    }
                double px = 0.55 * ix + 0.45 * ex, py = 0.55 * iy + 0.45 * ey, r = 0.036;
                int k = 0;
                double[] disc = s_disc ?? (s_disc = new double[24]);
                for (int i = 0; i < 12; i++)
                {
                    double a = 2 * Math.PI * i / 12;
                    disc[k++] = px + r * Math.Cos(a);
                    disc[k++] = py + r * Math.Sin(a);
                }
                HullPatch(ref c, Metal(ChromeC), disc, 12, front, 0.013, 1.0, 1);
                HullPatch(ref c, Glass(LensC), disc, 12, front, 0.016, 0.62, 1);
            }
            if (strip)
            {
                // LED strip along the lens's lower edge: the outline squashed onto its lowest quarter.
                double ymin = double.MaxValue;
                for (int i = 0; i < n; i++) ymin = Math.Min(ymin, s_lamp[2 * i + 1]);
                for (int i = 0; i < n; i++) s_lamp[2 * i + 1] = ymin + 0.008 + 0.22 * (s_lamp[2 * i + 1] - ymin);
                HullPatch(ref c, Glass(DrlC), s_lamp, n, front, 0.012, 0.92, 1);
            }
        }

        [ThreadStatic] private static double[] s_disc;

        /// <summary>A front polygon (x, y in the vehicle frame) extruded from <paramref name="z0"/> forward to
        /// <paramref name="z1"/> with rounded edges: grilles, intakes, lamp housings standing in the fascia.</summary>
        private static void FrontPoly(ref Ctx c, in ShapeBrush b, Profile2 xy, double z0, double z1, double bevel)
        {
            FrontExtrude(ref c, b, xy, z0, z1, bevel, c.L0 ? 2 : 1);
        }

        /// <summary>A symmetric polygon from its right half (x ≥ 0, listed top to bottom); the left half is mirrored.</summary>
        private static Profile2 Mirrored(Profile2 p, double[] half, double cx, double cy, double sx = 1, double sy = 1)
        {
            p.Clear(true);
            int n = half.Length / 2;
            // Counter-clockwise: right half bottom → top, then left half top → bottom.
            for (int i = n - 1; i >= 0; i--) p.Add(cx + sx * half[2 * i], cy + sy * half[2 * i + 1], true);
            for (int i = 0; i < n; i++)
                if (half[2 * i] > 1e-6)
                    p.Add(cx - sx * half[2 * i], cy + sy * half[2 * i + 1], true);
            return p;
        }

        /// <summary>A honeycomb of small diamonds in a grille centred at height <paramref name="gy"/> (three staggered
        /// rows, <paramref name="half"/> wide each side), on the plane z (LOD0).</summary>
        private static void Honeycomb(ref Ctx c, double gy, double half, double z)
        {
            ShapeBrush cell = Plain(MeshColor.FromHex(0x3A3F45));
            for (int r = 0; r < 3; r++)
                for (int k = -4; k <= 4; k++)
                {
                    double gx = k * 0.09 + (r & 1) * 0.045;
                    if (Math.Abs(gx) > half - 0.05 * r) continue;
                    Decal(ref c, cell, gx, gy + 0.07 - 0.065 * r, z, 0, 0, 1, 0.04, 0.04, 0.006, 0.002, Math.PI / 4);
                }
        }

        // Grille outlines: the right half from the top down (x, y about the grille centre), mirrored (photos
        // refs/vehicles/swift 03, i10 00, c_alto 00, c_dolphin 00).
        private static readonly double[] HexGrille = { 0.40, 0.14, 0.46, 0.0, 0.34, -0.15 };
        private static readonly double[] CascadeGrille = { 0.34, 0.17, 0.40, 0.08, 0.48, -0.08, 0.44, -0.15 };
        private static readonly double[] SmileGrille = { 0.36, 0.14, 0.44, 0.05, 0.40, -0.06, 0.24, -0.12, 0.0, -0.13 };
        private static readonly double[] EvIntakeLow = { 0.40, 0.07, 0.46, -0.06 };
        private static readonly double[] NexonIntake = { 0.30, 0.08, 0.40, -0.08 };

        /// <summary>A clear headlamp cover over a silver reflector: lighter and cooler than a plain lens.</summary>
        private static readonly uint LampLensC = MeshColor.FromHex(0xE3EAF0);

        private static void CarFront(ref Ctx c, in CarSpec s, double zf, double hw, uint body)
        {
            ShapeBrush black = Plain(TrimC), lens = Glass(LensC), drl = Glass(DrlC), mesh = Plain(MeshColor.FromHex(0x15171A), 0.6f);
            bool upright = s.Lamp == LampKind.Upright;
            double z = zf + 0.004, hy = s.HoodFrontY;
            Profile2 pp = Prof2;
            // ---- Grille and intakes.
            switch (s.Grille)
            {
                case GrilleKind.Hex:
                {
                    // Swift: a wide hexagonal mouth with a honeycomb right under the lamps, the plate on its lower part.
                    double gy = 0.5 * (s.BumperF + s.NoseY) + 0.06;
                    FrontPoly(ref c, black, Mirrored(pp, HexGrille, 0, gy, 1, 1.15), zf - 0.10, zf + 0.008, 0.012);
                    if (c.L1) FrontPoly(ref c, mesh, Mirrored(pp, HexGrille, 0, gy, 0.9, 0.95), zf - 0.10, zf + 0.011, 0.004);
                    if (c.L0) Honeycomb(ref c, gy, 0.36, zf + 0.013);
                    break;
                }
                case GrilleKind.Cascade:
                {
                    // i10 Nios: the big cascading grille widening downwards in a silver surround, horizontal slats,
                    // boomerang LED DRLs in its lower corners.
                    double gy = 0.5 * (s.BumperF + s.NoseY) + 0.06;
                    FrontPoly(ref c, Chrome(), Mirrored(pp, CascadeGrille, 0, gy, 1.05, 1.06), zf - 0.10, zf + 0.008, 0.012);
                    FrontPoly(ref c, black, Mirrored(pp, CascadeGrille, 0, gy), zf - 0.10, zf + 0.011, 0.010);
                    if (c.L1)
                    {
                        for (int k = 0; k < 5; k++)
                            Decal(ref c, Plain(MeshColor.FromHex(0x3A3F45)), 0, gy + 0.12 - 0.055 * k, zf + 0.014, 0, 0, 1, 0.64 + 0.06 * k, 0.014, 0.005, 0.002);
                        for (int side = -1; side <= 1; side += 2)
                        {
                            Decal(ref c, drl, side * 0.40, gy - 0.05, zf + 0.016, 0, 0, 1, 0.13, 0.022, 0.009, 0.003, side * 0.75);
                            Decal(ref c, drl, side * 0.33, gy - 0.11, zf + 0.016, 0, 0, 1, 0.14, 0.022, 0.009, 0.003, 0);
                        }
                    }
                    break;
                }
                case GrilleKind.Smile:
                {
                    // Alto K10: a big honeycomb grille between the lamps whose bottom curves up at the ends like a smile.
                    double gy = 0.5 * (s.BumperF + s.NoseY) + 0.05;
                    FrontPoly(ref c, black, Mirrored(pp, SmileGrille, 0, gy), zf - 0.10, zf + 0.008, 0.012);
                    if (c.L0) Honeycomb(ref c, gy + 0.01, 0.34, zf + 0.012);
                    else if (c.L1)
                        for (int k = 0; k < 3; k++)
                            Decal(ref c, Plain(MeshColor.FromHex(0x3A3F45)), 0, gy + 0.07 - 0.06 * k, zf + 0.012, 0, 0, 1, 0.56 - 0.08 * k, 0.012, 0.005, 0.002);
                    break;
                }
                case GrilleKind.Oval:
                    Panel(ref c, black, 0, s.NoseY - 0.08, z, 0, 0, 1, 0.56, 0.16, 0.07, 0.012);
                    if (c.L0) Decal(ref c, Chrome(), 0, s.NoseY - 0.08, z + 0.012, 0, 0, 1, 0.46, 0.02, 0.01, 0.002);
                    Panel(ref c, black, 0, s.BumperF + 0.12, z, 0, 0, 1, 0.70, 0.08, 0.04, 0.01);
                    break;
                case GrilleKind.Closed:
                {
                    // EVs: a closed, body-coloured nose; the intake low in the bumper (and the Nexon type's patterned one).
                    bool nexon = s.Lamp == LampKind.Split;
                    double gy = s.BumperF + (nexon ? 0.15 : 0.10);
                    FrontPoly(ref c, black, Mirrored(pp, nexon ? NexonIntake : EvIntakeLow, 0, gy, s.Lamp == LampKind.Bar ? 1.15 : 1, 1), zf - 0.10, zf + 0.008, 0.012);
                    if (nexon && c.L0)
                        for (int k = -3; k <= 3; k++)
                            Decal(ref c, Plain(MeshColor.FromHex(0x3A3F45)), k * 0.09, gy, zf + 0.012, 0, 0, 1, 0.05, 0.05, 0.006, 0.002, Math.PI / 4);
                    break;
                }
                case GrilleKind.VerticalSlats:
                    Panel(ref c, black, 0, s.NoseY - 0.12, z, 0, 0, 1, 0.70, 0.30, 0.05, 0.015);
                    if (c.L1)
                        for (int k = -3; k <= 3; k++) OBox(ref c, Chrome(), T(k * 0.085, s.NoseY - 0.12, z + 0.018), 0.022, 0.26, 0.012);
                    Panel(ref c, black, 0, s.BumperF + 0.13, z, 0, 0, 1, 0.84, 0.10, 0.04, 0.01);
                    break;
                case GrilleKind.Chrome:
                {
                    double gy = upright ? s.NoseY - 0.12 : s.NoseY - 0.13, gh = upright ? 0.30 : 0.34, gw = upright ? 0.80 : 0.90;
                    Panel(ref c, Chrome(), 0, gy, z, 0, 0, 1, gw, gh, 0.06, 0.012);
                    Panel(ref c, black, 0, gy, z + 0.012, 0, 0, 1, gw - 0.10, gh - 0.08, 0.05, 0.006);
                    if (c.L0)
                        for (int k = 0; k < 4; k++) Decal(ref c, Chrome(), 0, gy - 0.08 + 0.055 * k, z + 0.02, 0, 0, 1, gw - 0.14, 0.012, 0.004, 0.002);
                    Panel(ref c, black, 0, s.BumperF + 0.12, z, 0, 0, 1, 0.84, 0.10, 0.04, 0.01);
                    break;
                }
                default:
                    // Old 800: a narrow slot between the lamps, a small intake below.
                    Panel(ref c, black, 0, s.NoseY - 0.06, z, 0, 0, 1, 0.50, 0.07, 0.03, 0.01);
                    Panel(ref c, black, 0, s.BumperF + 0.12, z, 0, 0, 1, 0.60, 0.07, 0.03, 0.01);
                    break;
            }
            // ---- Headlamps: lenses lying on the curved nose (HullLamps), outlines in nose units (u across the nose half
            // width, v from the nose top line up to the hood's front edge).
            double nw = hw * (1 - s.Bulge), ny0 = s.NoseY, ny1 = hy;
            switch (s.Lamp)
            {
                case LampKind.Swept:
                    // Swift: big swept lamps from the grille's top corners up and out along the hood line, wrapping round the
                    // fender corner; projector and LED strip.
                    HullLamps(ref c, SweptLamp, nw, ny0, ny1, true, LampLensC, true, false);
                    break;
                case LampKind.Boomerang:
                    // i10 Nios: wide teardrop lamps at the corners, a little lower and flatter than the Swift's.
                    HullLamps(ref c, TeardropLamp, nw, ny0, ny1, true, LampLensC, true, false);
                    break;
                case LampKind.Compact:
                    // Alto K10 / tall-boy: rounded lamps on the corners over the big lower grille.
                    HullLamps(ref c, RoundLamp, nw, ny0, ny1, true, LampLensC, false, false);
                    break;
                case LampKind.Slim:
                {
                    // Dolphin: thin swept lamps high on the closed nose, with the vertical "wave" vents low in the bumper.
                    HullLamps(ref c, SlimLamp, nw, ny0, ny1, true, LampLensC, false, true);
                    if (c.L1)
                        for (int side = -1; side <= 1; side += 2)
                            Decal(ref c, Plain(TrimC), side * (hw - 0.30), s.BumperF + 0.24, z, side * 0.5, 0, 1, 0.07, 0.16, 0.03, 0.003, side * 0.3);
                    break;
                }
                case LampKind.Split:
                {
                    // A connected DRL bar right across the hood edge (Nexon type) or two slim DRLs (Creta type); the main
                    // lamps sit low in the bumper corners.
                    bool connected = s.Ev;
                    if (connected)
                    {
                        // A gloss-black band right across the nose with the DRL strip along its top edge.
                        int n = LampOutline(ref c, NexonBand, nw, ny0, ny1);
                        HullPatch(ref c, Plain(MeshColor.FromHex(0x1B1E22)), s_lamp, n, true, 0.005, 1.0, c.L1 ? 2 : 1, false);
                        n = LampOutline(ref c, NexonBar, nw, ny0, ny1);
                        HullPatch(ref c, Glass(DrlC), s_lamp, n, true, 0.009, 1.0, 1, false);
                    }
                    else HullLamps(ref c, SlimLamp, nw, ny0, ny1, true, DrlC, false, false);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        double lx = side * (hw - 0.24), ly = s.NoseY - (connected ? 0.17 : 0.13);
                        Panel(ref c, black, lx, ly, z, side * 0.25, 0, 1, 0.24, 0.15, 0.04, 0.012);
                        Panel(ref c, lens, lx, ly, z + 0.012, side * 0.25, 0, 1, 0.17, 0.08, 0.03, 0.008);
                    }
                    break;
                }
                case LampKind.Bar:
                {
                    // Atto 3: slim lamps joined by one light bar across the closed nose.
                    HullLamps(ref c, SlimLamp, nw, ny0, ny1, true, LampLensC, false, false);
                    int n = LampOutline(ref c, AttoBar, nw, ny0, ny1);
                    HullPatch(ref c, Glass(DrlC), s_lamp, n, true, 0.008, 1.0, 1, false);
                    break;
                }
                case LampKind.Upright:
                    for (int side = -1; side <= 1; side += 2)
                    {
                        double x = side * (hw - 0.26), ly = s.NoseY - 0.10;
                        Panel(ref c, Chrome(), x, ly, z, 0, 0, 1, 0.26, 0.20, 0.04, 0.01);
                        Panel(ref c, lens, x, ly, z + 0.01, 0, 0, 1, 0.20, 0.15, 0.04, 0.008);
                    }
                    break;
                default:
                {
                    // Old 800: square lamps flush on the fascia top.
                    double ly = s.NoseY - 0.04, lz, lny, lnz;
                    NoseAt(s, zf, ly, out lz, out lny, out lnz);
                    for (int side = -1; side <= 1; side += 2) Panel(ref c, lens, side * (hw - 0.28), ly, lz + 0.004, 0, lny, lnz, 0.24, 0.13, 0.025, 0.01);
                    break;
                }
            }
            // ---- Indicators, fog lamps, bumper lip.
            for (int side = -1; side <= 1; side += 2)
            {
                if (c.L1 && (upright || s.Lamp == LampKind.Small))
                    Decal(ref c, Glass(AmberC), side * (hw - 0.11), upright ? s.NoseY - 0.10 : s.NoseY - 0.06, z, side * 0.6, 0, 1, 0.06, 0.04, 0.012, 0.004);
                if (c.L0 && !s.Ev && s.Lamp != LampKind.Small)
                {
                    double fx = side * (hw - 0.24), fy = s.BumperF + 0.12;
                    Panel(ref c, black, fx, fy, z, side * 0.3, 0, 1, 0.16, 0.10, 0.04, 0.012);
                    Lamp(ref c, LensC, 0, fx, fy, z + 0.012, side * 0.3, 0, 1, 0.032, 0.01, 10);
                }
            }
            OBox(ref c, Plain(TrimC), T(0, s.BumperF + 0.03, zf - 0.05), 2 * hw - 0.30, 0.06, 0.10);
            _ = body;
        }

        // Lamp outlines in nose units (u = fraction of the nose half width, v = 0 at the nose top line, 1 at the hood's
        // front edge; the right lamp, listed round the outline).
        private static readonly double[] SweptLamp =
        {
            0.44, 0.08, 0.46, 0.46, 0.58, 0.72, 0.74, 0.88, 0.88, 0.90, 0.95, 0.70, 0.93, 0.42, 0.78, 0.26, 0.60, 0.12,
        };

        private static readonly double[] TeardropLamp = { 0.50, 0.30, 0.52, 0.55, 0.66, 0.80, 0.84, 0.92, 0.95, 0.78, 0.94, 0.52, 0.80, 0.36, 0.62, 0.26 };
        private static readonly double[] RoundLamp = { 0.52, 0.30, 0.53, 0.62, 0.66, 0.86, 0.82, 0.94, 0.94, 0.80, 0.94, 0.50, 0.80, 0.30, 0.64, 0.22 };
        private static readonly double[] SlimLamp = { 0.50, 0.60, 0.64, 0.76, 0.82, 0.82, 0.93, 0.74, 0.90, 0.62, 0.74, 0.60, 0.58, 0.54 };
        private static readonly double[] NexonBar = { -0.90, 0.80, 0.0, 0.84, 0.90, 0.80, 0.92, 0.88, 0.0, 0.92, -0.92, 0.88 };
        private static readonly double[] NexonBand = { -0.90, 0.56, 0.0, 0.62, 0.90, 0.56, 0.93, 0.90, 0.0, 0.94, -0.93, 0.90 };
        private static readonly double[] AttoBar = { -0.62, 0.66, 0.0, 0.68, 0.62, 0.66, 0.62, 0.71, 0.0, 0.73, -0.62, 0.71 };

        // Tail lamps in tail units (u = fraction of the tail half width, v = 0 at the rear bumper top, 1 at the hatch's
        // belt line).
        private static readonly double[] TailTall = { 0.70, 0.55, 0.70, 0.86, 0.80, 0.96, 0.92, 0.94, 0.95, 0.70, 0.92, 0.50, 0.82, 0.46 };
        private static readonly double[] TailWide = { 0.58, 0.62, 0.62, 0.88, 0.80, 0.94, 0.94, 0.90, 0.95, 0.70, 0.88, 0.58, 0.72, 0.56 };

        private static void CarRear(ref Ctx c, in CarSpec s, double zr, double hw, uint body)
        {
            ShapeBrush black = Plain(TrimC), tail = Glass(TailC);
            double z = zr - 0.004;
            bool box = s.Rear == RearKind.Box || s.Rear == RearKind.Pickup || s.Rear == RearKind.DoubleCab;
            if (box)
            {
                double ty = (s.Rear == RearKind.Box ? s.BeltRear : s.Belt) - 0.22;
                for (int k = -1; k <= 1; k += 2)
                    Panel(ref c, tail, k * (hw - 0.14), ty, z, 0, 0, -1, 0.10, 0.30, 0.03, 0.01);
            }
            else
            {
                // Hatches: tail lamps lying on the tail corners and wrapping round into the flanks (tall and upright on the
                // Swift and Alto types, wider on the i10 and the EVs), joined by a light bar on the EVs; tail units run from
                // the bumper top to the hatch's belt line.
                double nw = hw * (1 - s.Bulge), v0 = s.BumperR + 0.10, v1 = s.BeltRear;
                bool tall = s.Lamp == LampKind.Swept || s.Lamp == LampKind.Compact || s.Lamp == LampKind.Small;
                HullLamps(ref c, tall ? TailTall : TailWide, nw, v0, v1, false, TailC, false, false);
                if (s.Ev)
                {
                    int n = LampOutline(ref c, AttoBar, nw, v0, v1, 0, 0.12);
                    HullPatch(ref c, tail, s_lamp, n, false, 0.008, 1.0, 1, false);
                }
            }
            for (int k = -1; k <= 1; k += 2)
                if (c.L0) Decal(ref c, Glass(ReflectC), k * (hw - 0.16), s.BumperR + 0.12, z, 0, 0, -1, 0.10, 0.03, 0.01, 0.003);
            OBox(ref c, Plain(TrimC), T(0, s.BumperR + 0.03, zr + 0.04), 2 * hw - 0.26, 0.07, 0.10);
            if (!s.Ev && c.L0) Cyl(ref c, Metal(SteelC), hw - 0.40, s.BumperR - 0.02, zr + 0.10, hw - 0.40, s.BumperR - 0.02, zr - 0.03, 0.025, 8);
            if (s.Rear == RearKind.Hatch && s.Spoiler)
                RBox(ref c, Paint(body), 0, s.Roof - 0.06, s.RoofBack - 0.02, 2 * hw * s.Tumble - 0.16, 0.04, 0.12, 0.02, 1); // roof spoiler
        }

        private static void CarSides(ref Ctx c, in CarSpec s, Dims d, uint body)
        {
            double hw = d.Half, wb = d.Wheelbase, R = d.WheelR, A = R + s.ArchGap;
            bool bed = s.Rear == RearKind.Pickup || s.Rear == RearKind.DoubleCab;
            uint line = Shade(body, 0.55f);
            for (int side = -1; side <= 1; side += 2)
            {
                double x = side * (hw + 0.001);
                // Door shut lines (front door, B pillar, rear door) and handles.
                double z1 = s.ScreenBase - 0.06, zB = BPillarZ(s), z3 = bed ? s.BedFront + 0.05 : A + 0.06;
                double y0 = s.Sill + 0.06, y1 = s.Belt - 0.03;
                Decal(ref c, Plain(line), x, 0.5 * (y0 + y1), z1, side, 0, 0, 0.008, y1 - y0, 0.002, 0.002);
                Decal(ref c, Plain(line), x, 0.5 * (y0 + y1), zB, side, 0, 0, 0.008, y1 - y0, 0.002, 0.002);
                if (s.FourDoor) Decal(ref c, Plain(line), x, 0.5 * (y0 + y1) + (bed ? 0 : 0.05), z3, side, 0, 0, 0.008, y1 - y0 - (bed ? 0 : 0.1), 0.002, 0.002);
                if (c.L0)
                {
                    OBox(ref c, Chrome(), T(x + side * 0.006, y1 - 0.06, zB + 0.22), 0.012, 0.025, 0.11);
                    if (s.FourDoor) OBox(ref c, Chrome(), T(x + side * 0.006, y1 - 0.06, z3 + (bed ? 0.15 : 0.25)), 0.012, 0.025, 0.11);
                    // Belt moulding under the side glass.
                    OBox(ref c, Plain(TrimC), T(side * (hw - 0.035), s.Belt - 0.005, 0.5 * (s.ScreenBase + s.BackBase)), 0.02, 0.02, Math.Abs(s.ScreenBase - s.BackBase) - 0.2);
                }
                if (s.Cladding)
                {
                    // Black arch cladding and a sill strip.
                    FenderArc(ref c, Plain(TrimC), side * (hw - 0.02), R, 0, A + 0.055, 0.06, 0.065, -12, 204, 22);
                    FenderArc(ref c, Plain(TrimC), side * (hw - 0.02), R, wb, A + 0.055, 0.06, 0.065, -12, 204, 22);
                    OBox(ref c, Plain(TrimC), T(side * (hw - 0.01), s.Sill + 0.04, 0.5 * wb), 0.04, 0.08, wb - 2 * A - 0.05);
                }
                else if (s.ArchLips && c.L0)
                {
                    // Body-coloured arch lips: the flared wheel arches of the modern hatches.
                    FenderArc(ref c, Paint(body), side * (hw - 0.012), R, 0, A + 0.02, 0.045, 0.035, -8, 196, 10);
                    FenderArc(ref c, Paint(body), side * (hw - 0.012), R, wb, A + 0.02, 0.045, 0.035, -8, 196, 10);
                }
                if (s.SideStep)
                    OBox(ref c, Plain(TrimC), T(side * (hw - 0.02), s.Sill - 0.10, 0.5 * wb), 0.12, 0.035, wb - 2 * A - 0.05);
            }
        }

        /// <summary>The open bed of a pickup: floor, side walls, tailgate (and a roll bar on the double cab).</summary>
        private static void PickupBed(ref Ctx c, in CarSpec s, double zr, double hw, uint body, double R, double wb)
        {
            ShapeBrush paint = Paint(body);
            double z0 = zr + 0.02, z1 = s.BedFront - 0.02, top = s.Belt + 0.10, floor = s.Belt - 0.25;
            double len = z1 - z0, zc = 0.5 * (z0 + z1);
            if (c.L0)
            {
                RBox(ref c, paint, -hw + 0.04, 0.5 * (floor + top), zc, 0.08, top - floor, len, 0.03, 2);
                RBox(ref c, paint, hw - 0.04, 0.5 * (floor + top), zc, 0.08, top - floor, len, 0.03, 2);
                RBox(ref c, paint, 0, 0.5 * (floor + top), z0 + 0.04, 2 * hw - 0.02, top - floor, 0.08, 0.03, 2);
                RBox(ref c, paint, 0, 0.5 * (floor + top) + 0.05, z1 - 0.03, 2 * hw - 0.10, top - floor + 0.1, 0.06, 0.02, 1);
            }
            else
            {
                OBox(ref c, paint, T(-hw + 0.04, 0.5 * (floor + top), zc), 0.08, top - floor, len);
                OBox(ref c, paint, T(hw - 0.04, 0.5 * (floor + top), zc), 0.08, top - floor, len);
                OBox(ref c, paint, T(0, 0.5 * (floor + top), z0 + 0.04), 2 * hw - 0.02, top - floor, 0.08);
                OBox(ref c, paint, T(0, 0.5 * (floor + top) + 0.05, z1 - 0.03), 2 * hw - 0.10, top - floor + 0.1, 0.06);
            }
            OBox(ref c, Plain(MeshColor.FromHex(0x3A3D42), 0.6f), T(0, floor + 0.01, zc), 2 * hw - 0.14, 0.02, len - 0.12);
            if (s.Rear == RearKind.DoubleCab && c.L1)
            {
                // Roll bar behind the cab.
                Path3 rb = P;
                rb.Add(-hw + 0.08, top - 0.02, z1 - 0.12).Add(-hw + 0.12, top + 0.30, z1 - 0.12).Add(hw - 0.12, top + 0.30, z1 - 0.12).Add(hw - 0.08, top - 0.02, z1 - 0.12);
                Tube(ref c, Metal(AlloyC), rb, 0.03, 8);
            }
            _ = R;
            _ = wb;
        }
    }
}
