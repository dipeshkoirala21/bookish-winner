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
            /// <summary>Small rectangular lamps (old 800 / Alto).</summary>
            Small = 0,

            /// <summary>Swept wraparound lamps (Swift).</summary>
            Swept = 1,

            /// <summary>Boomerang DRL over a lamp (i10).</summary>
            Boomerang = 2,

            /// <summary>Slim LED bar on top, main lamps lower (Nexon, Creta).</summary>
            Split = 3,

            /// <summary>One wide lamp bar across the nose (Atto 3).</summary>
            Bar = 4,

            /// <summary>Upright lamps either side of a tall grille (Scorpio, Bolero, Prado).</summary>
            Upright = 5,

            /// <summary>Slim lamps over a closed nose (Dolphin).</summary>
            Slim = 6,
        }

        private enum GrilleKind : byte
        {
            Slot = 0,
            Oval = 1,
            Cascade = 2,
            Closed = 3,
            VerticalSlats = 4,
            Chrome = 5,
        }

        /// <summary>A car model type: side profile heights (m), stations along z (m from the rear axle), widths and the
        /// detail choices.</summary>
        private struct CarSpec
        {
            public double BumperF, BumperR, Sill, ArchGap, NoseY, HoodFrontY, CowlY, Belt, BeltRear, Roof, TailTop;
            public double ScreenBase, ScreenTop, RoofBack, BackBase, NoseDepth;
            public double Bevel, Tumble, NoseTaper, TailTaper, NoseCurve, TailCurve;
            public RearKind Rear;
            public LampKind Lamp;
            public GrilleKind Grille;
            public bool FloatingRoof, BlackPillars, RoofRails, Cladding, SpareWheel, SideStep, Ev, FourDoor;
            public double BedFront; // pickups: z where the cab starts
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
                NoseCurve = 0.26, TailCurve = 0.10, FourDoor = true,
            };
            switch (e.TrafficClass)
            {
                case Traffic.VehicleClass.Taxi:
                    if (model == 0)
                    {
                        // Old 800 / Alto taxi: tiny, tall boxy greenhouse, short hood.
                        Profile(ref s, zf, zr, 0.58, 0.74, 0.08, 0.88, 0.90, h - 0.01, 0.80, 1.38, 0.12, 0.03);
                        s.Bevel = 0.10;
                        s.Tumble = 0.86;
                        s.Lamp = LampKind.Small;
                        s.Grille = GrilleKind.Slot;
                    }
                    else
                    {
                        // Tall-boy (Santro / i10 class).
                        Profile(ref s, zf, zr, 0.60, 0.78, 0.10, 0.92, 0.94, h - 0.01, 0.88, 1.50, 0.10, 0.03);
                        s.Bevel = 0.12;
                        s.Lamp = LampKind.Boomerang;
                        s.Grille = GrilleKind.Cascade;
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
                        // Swift type: cab-forward, raked screen, rising belt, black pillars, wraparound lamps.
                        Profile(ref s, zf, zr, 0.58, 0.82, 0.18, 0.99, 1.07, h - 0.02, 1.05, 1.88, 0.52, 0.10);
                        s.Bevel = 0.17;
                        s.Tumble = 0.78;
                        s.BlackPillars = true;
                        s.Lamp = LampKind.Swept;
                        s.Grille = GrilleKind.Oval;
                    }
                    else if (model == 1)
                    {
                        // i10 / Tiago type: tall-boy, cascading grille, boomerang DRLs.
                        Profile(ref s, zf, zr, 0.62, 0.82, 0.12, 0.97, 1.00, h - 0.02, 1.00, 1.74, 0.20, 0.06);
                        s.Lamp = LampKind.Boomerang;
                        s.Grille = GrilleKind.Cascade;
                    }
                    else
                    {
                        // Alto type: small, short hood, tall glasshouse.
                        Profile(ref s, zf, zr, 0.62, 0.80, 0.10, 0.94, 0.96, h - 0.02, 0.92, 1.60, 0.16, 0.05);
                        s.Bevel = 0.12;
                        s.Tumble = 0.84;
                        s.Lamp = LampKind.Small;
                        s.Grille = GrilleKind.Slot;
                    }
                    return s;
                case BodyShape.Suv:
                    if (e.Engine == EngineSound.CarEv)
                    {
                        // Compact EVs: Dolphin (rounded hatch, closed nose), Nexon/Punch (compact SUV), Atto 3 (crossover).
                        s.Ev = true;
                        s.Grille = GrilleKind.Closed;
                        s.FloatingRoof = model != 0;
                        s.BlackPillars = true;
                        s.Sill = 0.34;
                        if (model == 0)
                        {
                            Profile(ref s, zf, zr, 0.58, 0.82, 0.20, 1.00, 1.06, h - 0.03, 1.15, 2.00, 0.45, 0.10);
                            s.Bevel = 0.18;
                            s.Tumble = 0.78;
                            s.Lamp = LampKind.Slim;
                        }
                        else if (model == 1)
                        {
                            Profile(ref s, zf, zr, 0.72, 0.90, 0.14, 1.04, 1.08, h - 0.06, 1.10, 1.82, 0.22, 0.06);
                            s.Lamp = LampKind.Split;
                            s.Cladding = true;
                            s.RoofRails = true;
                        }
                        else
                        {
                            Profile(ref s, zf, zr, 0.66, 0.88, 0.18, 1.02, 1.08, h - 0.03, 1.15, 1.92, 0.34, 0.08);
                            s.Bevel = 0.16;
                            s.Tumble = 0.80;
                            s.Lamp = LampKind.Bar;
                            s.Cladding = true;
                        }
                        return s;
                    }
                    if (model == 1)
                    {
                        // Creta / Seltos crossover.
                        Profile(ref s, zf, zr, 0.80, 0.98, 0.14, 1.10, 1.14, 1.68, 1.22, 1.92, 0.26, 0.06);
                        s.Lamp = LampKind.Split;
                        s.Grille = GrilleKind.Chrome;
                        s.Cladding = true;
                        s.RoofRails = true;
                        s.FloatingRoof = true;
                        s.BlackPillars = true;
                        s.Sill = 0.38;
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
                    s.Lamp = LampKind.Upright;
                    s.Grille = model == 0 ? GrilleKind.VerticalSlats : GrilleKind.Chrome;
                    s.Cladding = model == 1;
                    s.SideStep = model == 1;
                    s.Roof = h - 0.06;
                    s.BedFront = model == 0 ? wb - 1.45 : wb - 2.05;
                    s.ScreenBase = zf - 1.30;
                    s.ScreenTop = zf - 1.75;
                    s.RoofBack = s.BedFront + 0.12;
                    s.BackBase = s.BedFront + 0.02;
                    s.FourDoor = model == 1;
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
            _ = wb;
            return s;
        }

        private static void Car(ref Ctx c, in VehicleCatalogEntry e, Dims d, in Livery p)
        {
            CarSpec s = SpecFor(e, d, c.Model);
            double zr = -d.Rear, zf = d.Wheelbase + d.Front, hw = d.Half, wb = d.Wheelbase, R = d.WheelR;
            uint body = p.Body, roofC = s.FloatingRoof ? MeshColor.FromHex(0x1F2226) : p.Roof;
            ShapeBrush paint = Paint(body), black = Plain(TrimC);
            bool far = c.Lod == VehicleLod.Lod2, taxi = e.TrafficClass == Traffic.VehicleClass.Taxi, police = e.TrafficClass == Traffic.VehicleClass.Service;
            double A = R + s.ArchGap;
            int v0 = c.M.VertexCount;

            // ---- Lower body: a smooth hull through rounded sections whose bottoms rise over the wheels (the arches)
            // and whose tops follow the bumper, nose, hood, belt and tail.
            bool bed = s.Rear == RearKind.Pickup || s.Rear == RearKind.DoubleCab;
            CarBodyHull(ref c, s, zr, zf, hw, R, wb, A, bed, paint);

            // Dark wheel-house tub between the arches (seen through them behind the tyres).
            double wW = WheelsOfWidth(e);
            OBox(ref c, Plain(UnderC, 0.3f), T(0, 0.5 * (s.Sill + R + A) - 0.05, 0.5 * wb), 2 * (hw - wW - 0.03), R + A - s.Sill + 0.1, wb + 2 * A);

            // ---- Front: grille, intake, lamps, fog lamps, bumper lip.
            CarFront(ref c, s, zf, hw, body);
            // ---- Rear: lamps, bumper, reflectors, spoiler, exhaust.
            CarRear(ref c, s, zr, hw, body);
            // ---- Sides: door shut lines, handles, sill trim, cladding.
            if (c.L1) CarSides(ref c, s, e, d, body);

            // ---- Glasshouse, roof, pillars (tumblehome applied to all of it).
            int g0 = c.M.VertexCount;
            double belt = s.Belt - 0.02, hwc = hw - 0.06, roofY = s.Roof - 0.025;
            double backBase = bed ? s.BackBase : s.BackBase;
            double crown = s.Rear == RearKind.Box ? 0.01 : 0.03, roofMid = 0.5 * (s.ScreenTop + s.RoofBack);
            CabinHull(ref c, s, belt, roofY, crown, hwc, bed, backBase);
            // The roof skin: everything on top between the screen header and the rear header takes the roof colour.
            RoofSkin(c.M, g0, s.ScreenTop + 0.03, s.RoofBack - 0.02, belt + 0.6 * (roofY - belt), roofC);
            if (c.L1)
            {
                // C pillars (body colour) and the black B pillar on each side, before the tumblehome so they lean with it.
                for (int side = -1; side <= 1; side += 2)
                {
                    double x0 = side * (hwc + 0.002), x1 = side * (hwc + 0.014);
                    double zB = 0.5 * (s.ScreenTop + s.RoofBack) + (s.FourDoor ? 0.05 : -0.15);
                    OBox(ref c, Plain(TrimC), T(0.5 * (x0 + x1), 0.5 * (belt + roofY), zB), Math.Abs(x1 - x0), roofY - belt, 0.07);
                    if (!bed)
                    {
                        Profile2 cp = Prof2.Clear(true);
                        double zC = s.RoofBack + (s.Rear == RearKind.Box ? 0.30 : 0.52);
                        cp.Add(zC, belt, true).Add(backBase + 0.02, s.BeltRear - 0.02, true).Add(s.RoofBack + 0.01, roofY, true).Add(zC + 0.12, roofY, true);
                        SideExtrude(ref c, Paint(s.FloatingRoof ? body : body), cp, Math.Min(x0, x1), Math.Max(x0, x1), 0.004, 1);
                    }
                }
            }
            TaperByY(c.M, g0, belt, roofY, s.Tumble);
            // A pillars, roof rails.
            double hwr = hwc * s.Tumble;
            if (c.L1)
                for (int side = -1; side <= 1; side += 2)
                {
                    Rod(ref c, s.BlackPillars ? black : paint, side * (hwc - 0.03), belt + 0.01, s.ScreenBase - 0.02, side * (hwr - 0.02), roofY + 0.005, s.ScreenTop + 0.02,
                        0.032, 6);
                    if (s.RoofRails)
                    {
                        Rod(ref c, Plain(AlloyDarkC), side * (hwr - 0.08), roofY + 0.07, s.ScreenTop + 0.05, side * (hwr - 0.08), roofY + 0.07, s.RoofBack + 0.02, 0.018, 6);
                        if (c.L0)
                            for (int k = 0; k < 2; k++)
                            {
                                double z = k == 0 ? s.ScreenTop + 0.08 : s.RoofBack + 0.06;
                                OBox(ref c, Plain(AlloyDarkC), T(side * (hwr - 0.08), roofY + 0.045, z), 0.03, 0.05, 0.06);
                            }
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

        /// <summary>The glasshouse as a hull: windscreen, crowned roof and back glass, rounded roof edges.</summary>
        private static void CabinHull(ref Ctx c, in CarSpec s, double belt, double roofY, double crown, double hwc, bool bed, double backBase)
        {
            Profile2 tp = Prof2.Clear(false);
            double roofMid = 0.5 * (s.ScreenTop + s.RoofBack);
            tp.Add(backBase, bed ? belt : s.BeltRear - 0.02, true).Add(s.RoofBack, roofY - 1.5 * crown, false).Add(roofMid, roofY, false)
              .Add(s.ScreenTop, roofY - crown, false).Add(s.ScreenBase - 0.03, belt, true);
            tp.Smooth(c.L0 ? 4 : c.L1 ? 2 : 1);
            HullBegin();
            for (int i = 0; i < tp.Count; i++)
            {
                double z = tp.X[i], yt = tp.Y[i], yb = belt - 0.05;
                if (i > 0 && z - tp.X[i - 1] < 0.01) continue;
                double hv = Math.Max(0.02, 0.5 * (yt - yb));
                HullAdd(z, 0, 0.5 * (yt + yb), hwc, hv, Math.Min(0.14, 0.95 * hv), 0.01);
            }
            HullEmit(ref c, Glass(GlassC), 3, true, true);
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

        private static double WheelsOfWidth(in VehicleCatalogEntry e)
        {
            WheelSocket[] w = Wheels(e);
            return w.Length > 0 ? w[0].Width : 0.2;
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
                if (z > zFront || z < zBack || y < yMin || ny < 0.45) continue;
                m.Colors[4 * v] = (byte)(color >> 24);
                m.Colors[4 * v + 1] = (byte)(color >> 16);
                m.Colors[4 * v + 2] = (byte)(color >> 8);
                m.Uv0[2 * v] = (float)MaterialChannel.Paint;
            }
        }

        private static void CarFront(ref Ctx c, in CarSpec s, double zf, double hw, uint body)
        {
            ShapeBrush black = Plain(TrimC), lens = Glass(LensC), drl = Glass(DrlC);
            bool upright = s.Lamp == LampKind.Upright;
            double ly = upright ? s.NoseY - 0.10 : 0.5 * (s.NoseY + s.HoodFrontY) - 0.01, lx = hw - (upright ? 0.26 : 0.30);
            double lz, lny, lnz;
            NoseAt(s, zf, ly, out lz, out lny, out lnz);
            lz += 0.004;
            double gy = upright ? s.NoseY - 0.12 : s.NoseY - 0.10, z = zf + 0.004;
            // Grille.
            switch (s.Grille)
            {
                case GrilleKind.Oval:
                    Panel(ref c, black, 0, gy - 0.04, z, 0, 0, 1, 0.74, 0.22, 0.10, 0.012);
                    if (c.L0) Decal(ref c, Chrome(), 0, gy - 0.04, z + 0.012, 0, 0, 1, 0.60, 0.022, 0.011, 0.002);
                    break;
                case GrilleKind.Cascade:
                    Panel(ref c, black, 0, gy - 0.04, z, 0, 0, 1, 0.62, 0.24, 0.06, 0.012);
                    if (c.L0)
                        for (int k = 0; k < 3; k++)
                            Decal(ref c, Plain(MeshColor.FromHex(0x3A3D42)), 0, gy - 0.11 + 0.07 * k, z + 0.012, 0, 0, 1, 0.52 - 0.04 * k, 0.012, 0.004, 0.002);
                    break;
                case GrilleKind.Closed:
                    // EVs: a closed, body-coloured nose with a slim lower intake.
                    break;
                case GrilleKind.VerticalSlats:
                    Panel(ref c, black, 0, gy, z, 0, 0, 1, 0.70, 0.30, 0.05, 0.015);
                    if (c.L1)
                        for (int k = -3; k <= 3; k++) OBox(ref c, Chrome(), T(k * 0.085, gy, z + 0.018), 0.022, 0.26, 0.012);
                    break;
                case GrilleKind.Chrome:
                    Panel(ref c, Chrome(), 0, gy, z, 0, 0, 1, 0.80, 0.30, 0.06, 0.012);
                    Panel(ref c, black, 0, gy, z + 0.012, 0, 0, 1, 0.70, 0.22, 0.05, 0.006);
                    if (c.L0)
                        for (int k = 0; k < 4; k++) Decal(ref c, Chrome(), 0, gy - 0.08 + 0.055 * k, z + 0.02, 0, 0, 1, 0.66, 0.012, 0.004, 0.002);
                    break;
                default:
                    Panel(ref c, black, 0, gy, z, 0, 0, 1, 0.50, 0.07, 0.03, 0.01);
                    break;
            }
            // Lower intake in the bumper.
            Panel(ref c, black, 0, s.BumperF + 0.13, z, 0, 0, 1, s.Ev ? 0.70 : 0.84, s.Ev ? 0.07 : 0.10, 0.04, 0.01);
            // Headlamps (on the sloped nose for cars, on the fascia for the upright SUVs).
            for (int k = -1; k <= 1; k += 2)
            {
                double x = k * lx, tilt = k * 0.12;
                switch (s.Lamp)
                {
                    case LampKind.Swept:
                        Panel(ref c, black, x, ly, lz, tilt, lny, lnz, 0.36, 0.13, 0.05, 0.01, k * 0.15);
                        Panel(ref c, lens, x - k * 0.03, ly, lz + 0.01 * lnz, tilt, lny, lnz, 0.24, 0.085, 0.04, 0.008, k * 0.15);
                        if (c.L1) Decal(ref c, drl, x + k * 0.08, ly + 0.02, lz + 0.015 * lnz, tilt, lny, lnz, 0.13, 0.02, 0.008, 0.002, k * 0.15);
                        break;
                    case LampKind.Boomerang:
                        Panel(ref c, lens, x, ly, lz, tilt, lny, lnz, 0.24, 0.10, 0.04, 0.01, k * 0.1);
                        if (c.L1) Decal(ref c, drl, x - k * 0.02, gy - 0.08, z + 0.004, 0, 0, 1, 0.14, 0.02, 0.008, 0.002, -k * 0.6);
                        break;
                    case LampKind.Split:
                        Decal(ref c, drl, x + k * 0.02, s.HoodFrontY - 0.03, zf - s.NoseDepth + 0.01, tilt, lny, lnz, 0.30, 0.025, 0.012, 0.004);
                        Panel(ref c, black, x + k * 0.03, gy, z, 0, 0, 1, 0.22, 0.12, 0.04, 0.01);
                        Panel(ref c, lens, x + k * 0.03, gy, z + 0.01, 0, 0, 1, 0.15, 0.07, 0.03, 0.008);
                        break;
                    case LampKind.Bar:
                        if (k < 0) Decal(ref c, drl, 0, ly + 0.02, lz + 0.004, 0, lny, lnz, 2 * lx + 0.32, 0.03, 0.012, 0.004);
                        Panel(ref c, lens, x, ly - 0.02, lz, tilt, lny, lnz, 0.26, 0.07, 0.03, 0.01, k * 0.08);
                        break;
                    case LampKind.Slim:
                        Panel(ref c, lens, x, ly + 0.02, lz, tilt, lny, lnz, 0.32, 0.06, 0.03, 0.01, k * 0.2);
                        if (c.L1) Decal(ref c, drl, x - k * 0.08, ly - 0.02, lz + 0.006, tilt, lny, lnz, 0.16, 0.015, 0.006, 0.002, k * 0.2);
                        break;
                    case LampKind.Upright:
                        Panel(ref c, Chrome(), x, ly, z, 0, 0, 1, 0.26, 0.20, 0.04, 0.01);
                        Panel(ref c, lens, x, ly, z + 0.01, 0, 0, 1, 0.20, 0.15, 0.04, 0.008);
                        break;
                    default:
                        Panel(ref c, lens, x, ly, lz, 0, lny, lnz, 0.22, 0.10, 0.025, 0.01);
                        break;
                }
                // Indicator and fog lamp.
                if (c.L1) Decal(ref c, Glass(AmberC), k * (hw - 0.11), upright ? ly : gy, z, k * 0.6, 0, 1, 0.06, 0.04, 0.012, 0.004);
                if (c.L0) Lamp(ref c, LensC, TrimC, k * (hw - 0.27), s.BumperF + 0.14, z + 0.005, 0, 0, 1, 0.035, 0.02, 10);
            }
            // Bumper lip.
            if (c.L1) OBox(ref c, Plain(TrimC), T(0, s.BumperF + 0.03, zf - 0.05), 2 * hw - 0.30, 0.06, 0.10);
            _ = body;
        }

        private static void CarRear(ref Ctx c, in CarSpec s, double zr, double hw, uint body)
        {
            ShapeBrush black = Plain(TrimC), tail = Glass(TailC);
            double z = zr - 0.004, ty = (s.Rear == RearKind.Pickup || s.Rear == RearKind.DoubleCab) ? s.Belt - 0.12 : s.BeltRear - 0.10;
            for (int k = -1; k <= 1; k += 2)
            {
                double x = k * (hw - 0.18);
                if (s.Rear == RearKind.Box || s.Rear == RearKind.Pickup || s.Rear == RearKind.DoubleCab)
                {
                    // Upright lamps on the rear corners.
                    Panel(ref c, tail, x + k * 0.04, ty - 0.10, z, 0, 0, -1, 0.10, 0.30, 0.03, 0.01);
                }
                else
                {
                    Panel(ref c, tail, x, ty, z, k * 0.2, 0.1, -1, 0.26, 0.11, 0.04, 0.01, k * 0.1);
                    if (c.L1) Decal(ref c, Glass(MeshColor.FromHex(0xF5F5F5)), x - k * 0.05, ty - 0.01, z - 0.012, k * 0.2, 0.1, -1, 0.06, 0.04, 0.01, 0.002);
                }
                if (c.L0) Decal(ref c, Glass(ReflectC), k * (hw - 0.16), s.BumperR + 0.12, z, 0, 0, -1, 0.10, 0.03, 0.01, 0.003);
            }
            if (c.L1) OBox(ref c, Plain(TrimC), T(0, s.BumperR + 0.03, zr + 0.04), 2 * hw - 0.26, 0.07, 0.10);
            if (!s.Ev && c.L0) Cyl(ref c, Metal(SteelC), hw - 0.40, s.BumperR - 0.02, zr + 0.10, hw - 0.40, s.BumperR - 0.02, zr - 0.03, 0.025, 8);
            if (s.Rear == RearKind.Hatch && c.L1)
                RBox(ref c, Paint(body), 0, s.Roof - 0.06, s.RoofBack - 0.02, 2 * hw * s.Tumble - 0.16, 0.04, 0.12, 0.02, 1); // roof spoiler
        }

        private static void CarSides(ref Ctx c, in CarSpec s, in VehicleCatalogEntry e, Dims d, uint body)
        {
            double hw = d.Half, wb = d.Wheelbase, R = d.WheelR, A = R + s.ArchGap;
            uint line = Shade(body, 0.55f);
            for (int side = -1; side <= 1; side += 2)
            {
                double x = side * (hw + 0.001);
                // Door shut lines (front door, B pillar, rear door) and handles.
                double z1 = s.ScreenBase - 0.06, zB = 0.5 * (s.ScreenTop + s.RoofBack) + (s.FourDoor ? 0.05 : -0.15), z3 = A + 0.06;
                double y0 = s.Sill + 0.06, y1 = s.Belt - 0.03;
                Decal(ref c, Plain(line), x, 0.5 * (y0 + y1), z1, side, 0, 0, 0.008, y1 - y0, 0.002, 0.002);
                if (s.FourDoor)
                {
                    Decal(ref c, Plain(line), x, 0.5 * (y0 + y1), zB, side, 0, 0, 0.008, y1 - y0, 0.002, 0.002);
                    if (s.Rear != RearKind.Pickup) Decal(ref c, Plain(line), x, 0.5 * (y0 + y1) + 0.05, z3, side, 0, 0, 0.008, y1 - y0 - 0.1, 0.002, 0.002);
                }
                else Decal(ref c, Plain(line), x, 0.5 * (y0 + y1), zB, side, 0, 0, 0.008, y1 - y0, 0.002, 0.002);
                if (c.L0)
                {
                    OBox(ref c, Chrome(), T(x + side * 0.006, y1 - 0.06, zB + 0.22), 0.012, 0.025, 0.11);
                    if (s.FourDoor) OBox(ref c, Chrome(), T(x + side * 0.006, y1 - 0.06, z3 + 0.25), 0.012, 0.025, 0.11);
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
                if (s.SideStep)
                    OBox(ref c, Plain(TrimC), T(side * (hw - 0.02), s.Sill - 0.10, 0.5 * wb), 0.12, 0.035, wb - 2 * A - 0.05);
            }
            _ = e;
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
