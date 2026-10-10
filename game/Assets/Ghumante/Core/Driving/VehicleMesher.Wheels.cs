using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Driving
{
    public static partial class VehicleMesher
    {
        private static readonly uint RimSilverC = MeshColor.FromHex(0xC9CED3), HubDarkC = MeshColor.FromHex(0x34383E),
                                      TractorRimC = MeshColor.FromHex(0xFDD835), TruckRimC = MeshColor.FromHex(0xE8E8E2),
                                      DiscC = MeshColor.FromHex(0xA4A9AE), CapC = MeshColor.FromHex(0xD5D9DD);

        /// <summary>Rim radius as a fraction of the outer radius, per style (tyre aspect of the real class).</summary>
        private static double RimFrac(WheelStyle s)
        {
            switch (s)
            {
                case WheelStyle.MotoAlloy: return 0.70;
                case WheelStyle.MotoSpoked: return 0.74;
                case WheelStyle.Scooter: return 0.60;
                case WheelStyle.Bicycle: return 0.89;
                case WheelStyle.Truck: return 0.58;
                case WheelStyle.TractorRear: return 0.55;
                case WheelStyle.TractorFront: return 0.55;
                case WheelStyle.CarAlloy: return 0.66;
                default: return 0.62;
            }
        }

        /// <summary>
        /// A wheel of <paramref name="style"/>, outer radius <paramref name="R"/> and width <paramref name="w"/>, centred at
        /// the origin of <paramref name="at"/> with its axle along the vehicle X. Built in a wheel space whose +Y is the
        /// axle (lathe axis): a lathed tyre with a real profile (round motorbike section, square car shoulder, tall
        /// truck sidewall), the rim lip, the rim barrel seen through the spokes, spokes or a disc and a hub, all
        /// symmetric so the same mesh serves the left and right wheels.
        /// </summary>
        private static void Wheel(ref Ctx c, WheelStyle style, float R, float w, in Affine3 at)
        {
            Affine3 W = at * AcrossX(0, 0, 0);
            double rr = R * RimFrac(style), hw = 0.5 * w;
            if (c.Lod == VehicleLod.Lod2)
            {
                // Far: a dark tyre drum with a shaded rim disc standing just proud of both faces (cars, trucks,
                // scooters, motorbikes) or, on bicycles, a closed tyre ring round open daylight with two crossed spokes, so
                // a far bicycle wheel reads as a wheel and not as a plate.
                bool spoked = style == WheelStyle.Bicycle, small = IsSmall(style);
                const int far = 9;
                if (spoked)
                {
                    double ri = R - Math.Max(0.03, 1.2 * w);
                    Profile2 lp = Prof.Clear(true);
                    lp.Add(ri, -hw, true).Add(ri, hw, true).Add(R, 0.6 * hw, false).Add(R, -0.6 * hw, false);
                    Shapes.Lathe(c.M, W, Rubber(TyreC), lp, far, ShapeLod.Lod0);
                    for (int i = 0; i < 2; i++)
                        OBox(ref c, Metal(SpokeC()), W * Affine3.RotationY(Math.PI * (i + 0.25) / 2), 0.014, 0.014, 2 * ri);
                    return;
                }
                Shapes.Cylinder(c.M, W * T(0, -hw, 0), Rubber(TyreC), R, w, far, 0, 0, true, true, ShapeLod.Lod0);
                uint rim = small ? HubDarkC : Shade(RimColor(style), 0.8f);
                for (int s = -1; s <= 1; s += 2) Disc(ref c, Metal(rim, 0.8f), W, rr, s * (hw + 0.006), s, far);
                return;
            }
            if (c.Lod == VehicleLod.Lod1)
            {
                MediumWheel(ref c, style, R, rr, w, W);
                return;
            }
            bool l0 = c.L0;
            if (style == WheelStyle.Truck && w > 0.4f)
            {
                // Dual rear wheels: two tyres with a gap, one shared hub.
                double tw = 0.47 * w, off = 0.5 * w - 0.5 * tw;
                Tyre(ref c, style, R, rr, tw, W * T(0, -off, 0));
                Tyre(ref c, style, R, rr, tw, W * T(0, off, 0));
                // One disc per face: the outer wheel's on +Y, the inner wheel's on −Y (the faces between are hidden).
                TruckDisc(ref c, W * T(0, off, 0), rr, tw, true);
                TruckDisc(ref c, W * T(0, -off, 0), rr, tw, false);
                Shapes.Cylinder(c.M, W * T(0, -off, 0), Plain(UnderC, 0.4f), rr - 0.02, 2 * off, 12, 0, 0, false, false, c.S);
                return;
            }
            Tyre(ref c, style, R, rr, w, W);
            switch (style)
            {
                case WheelStyle.Truck:
                    TruckDisc(ref c, W, rr, w, true);
                    TruckDisc(ref c, W, rr, w, false);
                    break;
                case WheelStyle.TractorRear:
                case WheelStyle.TractorFront:
                    TractorRim(ref c, W, rr, w, style == WheelStyle.TractorRear);
                    break;
                case WheelStyle.Bicycle:
                case WheelStyle.MotoSpoked:
                    SpokedRim(ref c, W, R, rr, w, style == WheelStyle.Bicycle);
                    break;
                case WheelStyle.Car:
                    RimShell(ref c, W, rr, w, SteelC, 18);
                    Hubcap(ref c, W, rr, w);
                    break;
                default:
                {
                    // Alloys: car five-spoke, motorbike six-spoke (with a brake disc), scooter five-spoke.
                    uint rim = style == WheelStyle.MotoAlloy ? AlloyDarkC : style == WheelStyle.Scooter ? AlloyDarkC : RimSilverC;
                    RimShell(ref c, W, rr, w, rim, style == WheelStyle.CarAlloy ? 18 : 24);
                    int n = style == WheelStyle.MotoAlloy ? 6 : 5;
                    AlloySpokes(ref c, W, rr, w, n, rim, style != WheelStyle.CarAlloy);
                    if (style != WheelStyle.CarAlloy && l0)
                    {
                        // Brake disc on the left of the wheel (local −Y = vehicle −X).
                        double dr = style == WheelStyle.Scooter ? 0.78 * rr : 0.72 * rr;
                        Shapes.Cylinder(c.M, W * T(0, -0.36 * w, 0), Metal(DiscC), dr, 0.006, 20, 0, 0, true, true, c.S);
                        Shapes.Cylinder(c.M, W * T(0, -0.36 * w - 0.002, 0), Plain(HubDarkC), 0.45 * dr, 0.010, 10, 0, 0, true, true, c.S);
                    }
                    break;
                }
            }
        }

        /// <summary>A flat disc of radius <paramref name="r"/> in wheel space at axial offset <paramref name="y"/>, facing
        /// ±Y (<paramref name="facing"/>): a triangle fan of <paramref name="n"/> triangles (far rims).</summary>
        private static void Disc(ref Ctx c, in ShapeBrush b, in Affine3 W, double r, double y, int facing, int n)
        {
            double cx, cy, cz, nx, ny, nz;
            W.Point(0, y, 0, out cx, out cy, out cz);
            W.Normal(0, facing, 0, out nx, out ny, out nz);
            ShapeEmit.Normalize(ref nx, ref ny, ref nz);
            int centre = c.M.AddVertex((float)cx, (float)cy, (float)cz, (float)nx, (float)ny, (float)nz, b.Color, (float)b.Channel, b.Ao);
            for (int i = 0; i < n; i++)
            {
                double a = 2 * Math.PI * i / n, px, py, pz;
                W.Point(r * Math.Sin(a), y, r * Math.Cos(a), out px, out py, out pz);
                c.M.AddVertex((float)px, (float)py, (float)pz, (float)nx, (float)ny, (float)nz, b.Color, (float)b.Channel, b.Ao);
            }
            for (int i = 0; i < n; i++) ShapeEmit.TriOriented(c.M, centre, centre + 1 + i, centre + 1 + (i + 1) % n);
        }

        /// <summary>
        /// The LOD1 wheel (about 150-250 triangles): a lathed tyre with a five-point section, a rim face through the
        /// wheel, and the style's signature (alloy spokes, a few wire spokes, truck disc holes, tractor lugs).
        /// </summary>
        private static void MediumWheel(ref Ctx c, WheelStyle style, double R, double rr, double w, in Affine3 W)
        {
            double hw = 0.5 * w;
            bool round = IsSmall(style);
            Profile2 tp = Prof.Clear(false);
            tp.Add(rr, -0.42 * w, true).Add(R - 0.12 * (R - rr), -hw, false).Add(R, round ? 0 : -0.25 * w, false);
            if (!round) tp.Add(R, 0.25 * w, false);
            tp.Add(R - 0.12 * (R - rr), hw, false).Add(rr, 0.42 * w, true);
            int radial = style == WheelStyle.Bicycle ? 16 : round ? 14 : style == WheelStyle.TractorRear ? 12 : 10;
            Shapes.Lathe(c.M, W, Rubber(TyreC), tp, radial, ShapeLod.Lod0);
            uint rim = RimColor(style);
            if (style == WheelStyle.Bicycle || style == WheelStyle.MotoSpoked)
            {
                // A thin rim ring and a few spokes.
                Profile2 rp = Prof.Clear(false);
                rp.Add(rr + 0.004, 0.02, true).Add(rr - 0.02, 0.02, true).Add(rr - 0.02, -0.02, true).Add(rr + 0.004, -0.02, true);
                Shapes.Lathe(c.M, W, Metal(rim), rp, radial, ShapeLod.Lod0);
                for (int i = 0; i < 8; i++)
                {
                    double a = Math.PI * i / 8;
                    OBox(ref c, Metal(SpokeC()), W * Affine3.RotationY(a), 0.006, 0.006, 2 * (rr - 0.02));
                }
                return;
            }
            // Rim faces (both sides) with a recessed centre.
            Profile2 dp = Prof.Clear(false);
            double face = 0.36 * w, dish = Math.Min(0.04, 0.2 * w);
            dp.Add(0, -face + dish, true).Add(rr, -face, true).Add(rr, face, true).Add(0, face - dish, true);
            Shapes.Lathe(c.M, W, Metal(rim, 0.85f), dp, radial, ShapeLod.Lod0);
            if (style == WheelStyle.CarAlloy || style == WheelStyle.MotoAlloy || style == WheelStyle.Scooter)
            {
                int n = style == WheelStyle.MotoAlloy ? 6 : 5;
                for (int i = 0; i < n; i++)
                {
                    double a = 2 * Math.PI * i / n;
                    OBox(ref c, Metal(AlloyDarkC), W * Affine3.RotationY(a) * T(0, 0, 0.55 * rr), 0.14 * rr, 2 * face + 0.004, 0.75 * rr);
                }
            }
            else if (style == WheelStyle.TractorRear)
            {
                for (int i = 0; i < 8; i++)
                    for (int s = -1; s <= 1; s += 2)
                        OBox(ref c, Rubber(TyreC), W * Affine3.RotationY(2 * Math.PI * i / 8 + (s > 0 ? Math.PI / 8 : 0)) * T(0, s * 0.25 * w, R + 0.01) * Affine3.RotationZ(s * 0.5),
                             0.09 * R, 0.46 * w, 0.05 * R);
            }
        }

        private static bool IsSmall(WheelStyle s)
        {
            return s == WheelStyle.Bicycle || s == WheelStyle.MotoAlloy || s == WheelStyle.MotoSpoked || s == WheelStyle.Scooter;
        }

        private static uint RimColor(WheelStyle s)
        {
            switch (s)
            {
                case WheelStyle.TractorRear:
                case WheelStyle.TractorFront:
                    return TractorRimC;
                case WheelStyle.Truck:
                    return TruckRimC;
                case WheelStyle.Car:
                    return CapC;
                case WheelStyle.MotoAlloy:
                case WheelStyle.Scooter:
                    return AlloyDarkC;
                default:
                    return RimSilverC;
            }
        }

        /// <summary>The tyre: a lathed section (inner bead → sidewall → shoulder → tread → mirrored) in rubber.</summary>
        private static void Tyre(ref Ctx c, WheelStyle style, double R, double rr, double w, in Affine3 W)
        {
            double hw = 0.5 * w, h = R - rr;
            Profile2 p = Prof.Clear(false);
            switch (style)
            {
                case WheelStyle.MotoAlloy:
                case WheelStyle.MotoSpoked:
                case WheelStyle.Scooter:
                case WheelStyle.Bicycle:
                    // Round section: the tread is a half circle.
                    p.Add(rr + 0.01 * R, -0.36 * w, true).Add(rr + 0.3 * h, -0.49 * w, false).Add(R - 0.42 * h, -0.5 * w, false)
                     .Add(R - 0.12 * h, -0.33 * w, false).Add(R, 0, false).Add(R - 0.12 * h, 0.33 * w, false).Add(R - 0.42 * h, 0.5 * w, false)
                     .Add(rr + 0.3 * h, 0.49 * w, false).Add(rr + 0.01 * R, 0.36 * w, true);
                    break;
                case WheelStyle.TractorFront:
                    // Ribbed steering tyre: three circumferential ribs across the tread.
                    p.Add(rr + 0.01 * R, -0.42 * w, true).Add(rr + 0.3 * h, -0.5 * w, false).Add(R - 0.25 * h, -0.48 * w, false).Add(R, -0.32 * w, false)
                     .Add(R - 0.05 * h, -0.16 * w, false).Add(R, 0, false).Add(R - 0.05 * h, 0.16 * w, false).Add(R, 0.32 * w, false).Add(R - 0.25 * h, 0.48 * w, false)
                     .Add(rr + 0.3 * h, 0.5 * w, false).Add(rr + 0.01 * R, 0.42 * w, true);
                    break;
                default:
                    // Square shoulder: flat tread, rounded shoulders, bulging sidewalls.
                    p.Add(rr + 0.01 * R, -0.42 * w, true).Add(rr + 0.4 * h, -0.5 * w, false).Add(R - 0.08 * h, -0.45 * w, false).Add(R, -0.28 * w, false)
                     .Add(R, 0.28 * w, false).Add(R - 0.08 * h, 0.45 * w, false).Add(rr + 0.4 * h, 0.5 * w, false).Add(rr + 0.01 * R, 0.42 * w, true);
                    break;
            }
            int radial = style == WheelStyle.Bicycle ? 28 : IsSmall(style) ? 20 : style == WheelStyle.Truck ? 14 : 20;
            Shapes.Lathe(c.M, W, Rubber(TyreC), p, radial, c.S);
            if (style == WheelStyle.TractorRear && c.L1) Lugs(ref c, W, R, w);
        }

        /// <summary>Chevron lugs on a tractor drive tyre.</summary>
        private static void Lugs(ref Ctx c, in Affine3 W, double R, double w)
        {
            int n = c.L0 ? 16 : 10;
            double lh = 0.05 * R, len = 0.46 * w, th = 0.09 * R;
            for (int i = 0; i < n; i++)
            {
                double a = 2 * Math.PI * i / n;
                for (int s = -1; s <= 1; s += 2)
                {
                    double ao = a + (s > 0 ? Math.PI / n : 0);
                    Affine3 xf = W * Affine3.RotationY(ao) * T(0, s * 0.25 * w, R + 0.4 * lh) * Affine3.RotationZ(s * 0.5);
                    OBox(ref c, Rubber(TyreC), xf, th, len, lh);
                }
            }
        }

        /// <summary>The rim: a lip face on each side and the barrel seen through the spokes (facing the axle), one
        /// lathe with creased corners.</summary>
        private static void RimShell(ref Ctx c, in Affine3 W, double rr, double w, uint color, int segments)
        {
            Profile2 p = Prof.Clear(false);
            double lip = Math.Min(0.035, 0.12 * rr);
            p.Add(rr + 0.006, 0.42 * w, true).Add(rr - lip, 0.42 * w, true).Add(rr - lip, -0.42 * w, true).Add(rr + 0.006, -0.42 * w, true);
            Shapes.Lathe(c.M, W, Metal(color, 0.85f), p, segments, c.S);
        }

        /// <summary>A plastic hubcap on both faces of a steel wheel: a shallow dome with a chrome centre.</summary>
        private static void Hubcap(ref Ctx c, in Affine3 W, double rr, double w)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                Profile2 p = Prof.Clear(false);
                // Outward face: from the rim inward (normal along ±Y).
                double y = s * 0.36 * w;
                if (s > 0) p.Add(rr - 0.02, y, true).Add(0.72 * rr, y + 0.02, false).Add(0.3 * rr, y + 0.035, false).Add(0, y + 0.04, false);
                else p.Add(0, y - 0.04, false).Add(0.3 * rr, y - 0.035, false).Add(0.72 * rr, y - 0.02, false).Add(rr - 0.02, y, true);
                Shapes.Lathe(c.M, W, Metal(CapC), p, 14, c.S);
                if (c.L0)
                {
                    Affine3 f = W * T(0, y + s * 0.03, 0) * (s > 0 ? Affine3.Identity : Affine3.RotationX(Math.PI));
                    Shapes.Cylinder(c.M, f, Metal(ChromeC), 0.16 * rr, 0.015, 10, 0, 0, false, true, c.S);
                    for (int i = 0; i < 4; i++)
                    {
                        double a = 2 * Math.PI * (i + 0.5) / 4;
                        double px, py, pz, nx, ny, nz;
                        W.Point(Math.Sin(a) * 0.55 * rr, y + s * 0.03, Math.Cos(a) * 0.55 * rr, out px, out py, out pz);
                        W.Normal(0, s, 0, out nx, out ny, out nz);
                        Decal(ref c, Plain(HubDarkC), px, py, pz, nx, ny, nz, 0.10 * rr, 0.10 * rr, 0.05 * rr, 0.002);
                    }
                }
            }
        }

        /// <summary>Radial alloy spokes from a hub to the rim, centred in the width (reads from both sides).</summary>
        private static void AlloySpokes(ref Ctx c, in Affine3 W, double rr, double w, int n, uint color, bool moto)
        {
            double hubR = moto ? 0.22 * rr : 0.26 * rr, len = rr - 0.025 - hubR * 0.6, th = moto ? 0.11 * rr : 0.17 * rr;
            double ax = moto ? 0.26 * w : 0.42 * w;
            // Hub.
            Shapes.Cylinder(c.M, W * T(0, -0.5 * ax - 0.01, 0), Metal(color), hubR, ax + 0.02, 12, 0, 0, true, true, c.S);
            if (c.L0)
                Shapes.Cylinder(c.M, W * T(0, -0.5 * ax - 0.016, 0), Metal(ChromeC), 0.35 * hubR, ax + 0.032, 8, 0, 0, true, true, c.S);
            int v0 = c.M.VertexCount, i0 = c.M.IndexCount;
            // One spoke along local +Z from the hub, tapering visually via a split "Y" on motorbikes.
            double mid = hubR * 0.6 + 0.5 * len;
            if (moto && c.L0)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    Affine3 xf = W * Affine3.RotationY(s * 0.11) * T(0, 0, mid);
                    OBox(ref c, Metal(color), xf, 0.55 * th, ax * 0.8, len);
                }
            }
            else
            {
                if (c.L0) Ell(ref c, Metal(color), W * T(0, 0, mid), 0.5 * th, 0.5 * ax, 0.5 * len, 0.3, 0.25, 8);
                else OBox(ref c, Metal(color), W * T(0, 0, mid), th, ax, len);
            }
            int nv = c.M.VertexCount - v0, ni = c.M.IndexCount - i0;
            for (int k = 1; k < n; k++)
                Shapes.CopyTransformed(c.M, c.M, W * Affine3.RotationY(2 * Math.PI * k / n) * W.Inverse(), 0xFFFFFFFFu, v0, nv, i0, ni);
        }

        /// <summary>Wire spokes (bicycle, retro motorbike) between a hub barrel and a chromed rim.</summary>
        private static void SpokedRim(ref Ctx c, in Affine3 W, double R, double rr, double w, bool bicycle)
        {
            uint rimC = bicycle ? RimSilverC : ChromeC;
            // Rim: a box-section ring.
            Profile2 p = Prof.Clear(true);
            double rw = bicycle ? 0.022 : 0.05, rd = bicycle ? 0.018 : 0.03;
            p.Add(rr + 0.006, -0.5 * rw, true).Add(rr + 0.006, 0.5 * rw, true).Add(rr - rd, 0.35 * rw, true).Add(rr - rd, -0.35 * rw, true);
            Shapes.Lathe(c.M, W, Metal(rimC), p, 20, c.S);
            // Hub barrel with flanges.
            double hubR = bicycle ? 0.022 : 0.075, hubL = bicycle ? 0.09 : 0.16;
            Shapes.Cylinder(c.M, W * T(0, -0.5 * hubL, 0), Metal(bicycle ? SteelC : AlloyC), hubR, hubL, 10, 0, 0, true, true, c.S);
            if (!bicycle)
                for (int s = -1; s <= 1; s += 2)
                    Shapes.Cylinder(c.M, W * T(0, s * 0.5 * hubL - 0.01, 0), Metal(ChromeC), hubR * 1.25, 0.02, 12, 0, 0, true, true, c.S);
            int n = c.L0 ? (bicycle ? 28 : 24) : 10;
            double sr = bicycle ? 0.0045 : 0.006;
            for (int i = 0; i < n; i++)
            {
                double a = 2 * Math.PI * i / n, side = (i & 1) == 0 ? 1 : -1;
                double a2 = a + (side > 0 ? 0.35 : -0.35); // tangential lacing
                double hx = Math.Sin(a) * hubR * 1.1, hz = Math.Cos(a) * hubR * 1.1, hy = side * 0.45 * hubL;
                double ex = Math.Sin(a2) * (rr - rd), ez = Math.Cos(a2) * (rr - rd);
                double x0, y0, z0, x1, y1, z1;
                W.Point(hx, hy, hz, out x0, out y0, out z0);
                W.Point(ex, 0, ez, out x1, out y1, out z1);
                Shapes.Bar(c.M, Affine3.Identity, Metal(SpokeC()), x0, y0, z0, x1, y1, z1, sr, 3, ShapeLod.Lod0);
            }
            if (!bicycle && c.L0)
            {
                // Drum brake plate on the left.
                Shapes.Cylinder(c.M, W * T(0, -0.5 * hubL - 0.03, 0), Metal(AlloyC), 0.42 * rr, 0.03, 14, 0, 0, true, true, c.S);
            }
            _ = R;
        }

        private static uint SpokeC()
        {
            return MeshColor.FromHex(0xE0E3E6);
        }

        /// <summary>A deep steel truck disc on one face (outer = +Y side when <paramref name="outer"/>): dish, hand holes,
        /// hub boss and wheel nuts.</summary>
        private static void TruckDisc(ref Ctx c, in Affine3 W, double rr, double w, bool outer)
        {
            double s = outer ? 1 : -1, y = s * 0.38 * w;
            Profile2 p = Prof.Clear(false);
            p.Add(rr + 0.01, y, true).Add(0.72 * rr, y - s * 0.07, false).Add(0.55 * rr, y - s * 0.05, true)
             .Add(0.3 * rr, y + s * 0.02, true).Add(0.2 * rr, y + s * 0.05, true).Add(0, y + s * 0.06, false);
            if (s < 0) p.Reverse();
            Shapes.Lathe(c.M, W, Metal(TruckRimC, 0.9f), p, 14, c.S);
            if (!c.L0) return;
            // Hand holes (dark ovals) and wheel nuts.
            for (int i = 0; i < 6; i++)
            {
                double a = 2 * Math.PI * (i + 0.5) / 6, px, py, pz, nx, ny, nz;
                W.Point(Math.Sin(a) * 0.64 * rr, y - s * 0.058, Math.Cos(a) * 0.64 * rr, out px, out py, out pz);
                W.Normal(0, s, 0, out nx, out ny, out nz);
                Decal(ref c, Plain(HubDarkC, 0.4f), px, py, pz, nx, ny, nz, 0.10 * rr, 0.16 * rr, 0.05 * rr, 0.004);
            }
            for (int i = 0; i < 6; i++)
            {
                double a = 2 * Math.PI * i / 6, x = Math.Sin(a) * 0.3 * rr, z = Math.Cos(a) * 0.3 * rr;
                double yy = y + s * 0.025;
                Shapes.Cylinder(c.M, W * T(x, outer ? yy : yy - 0.03, z), Metal(ChromeC), 0.022, 0.03, 5, 0, 0, false, true, c.S);
            }
        }

        /// <summary>A yellow tractor rim: deep dish, wheel centre with bolts, valve.</summary>
        private static void TractorRim(ref Ctx c, in Affine3 W, double rr, double w, bool rear)
        {
            for (int k = -1; k <= 1; k += 2)
            {
                double y = k * (rear ? 0.12 : 0.2) * w;
                Profile2 p = Prof.Clear(false);
                p.Add(rr + 0.01, k * 0.44 * w, true).Add(rr - 0.03, k * 0.40 * w, false).Add(rr - 0.05, y, true).Add(0.45 * rr, y, true)
                 .Add(0.4 * rr, y + k * 0.03, true).Add(0, y + k * 0.03, false);
                if (k < 0) p.Reverse();
                Shapes.Lathe(c.M, W, Metal(TractorRimC), p, 18, c.S);
                if (c.L0)
                    for (int i = 0; i < 6; i++)
                    {
                        double a = 2 * Math.PI * i / 6, x = Math.Sin(a) * 0.3 * rr, z = Math.Cos(a) * 0.3 * rr;
                        Shapes.Cylinder(c.M, W * T(x, y + (k > 0 ? 0.02 : -0.05), z), Metal(SteelC), 0.025, 0.03, 5, 0, 0, false, true, c.S);
                    }
            }
            Shapes.Cylinder(c.M, W * T(0, -0.25 * w, 0), Metal(SteelC, 0.8f), 0.18 * rr, 0.5 * w, 10, 0, 0, true, true, c.S);
        }
    }
}
