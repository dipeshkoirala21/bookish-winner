using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Driving
{
    /// <summary>Detail levels of a vehicle model (W2_DESIGN 5.2 and 10.4).</summary>
    public enum VehicleLod : byte
    {
        /// <summary>The player's vehicle and the nearest traffic: windows, lights, mirrors, plates with numbers.</summary>
        Lod0 = 0,

        /// <summary>Near traffic: windows and lights, blank plates.</summary>
        Lod1 = 1,

        /// <summary>Far traffic and near parked vehicles (≈ 600 triangles): body, cabin, wheels.</summary>
        Lod2 = 2,

        /// <summary>Parked block-out (≤ 80 triangles): body box, cabin or seat box, wheel boxes.</summary>
        Block = 3,

        /// <summary>A single palette-tinted box (12 triangles), parked 80–150 m.</summary>
        Box = 4,
    }

    /// <summary>Where a wheel sits in the vehicle frame (origin on the ground under the rear axle, +X right, +Z forward).</summary>
    public struct WheelSocket
    {
        public float X, Y, Z, Radius, Width;

        /// <summary>A steered (front) wheel.</summary>
        public bool Steers;
    }

    /// <summary>
    /// Procedural cartoon vehicles (W2_DESIGN 5.2): every catalogue asset is built in code from its body family, the
    /// catalogue dimensions (cartoon overlay: wheels × 1.15, cab or head × 1.1, length × 0.95, the wheelbase kept so
    /// steering reads right), a generic livery and a fictional Nepali number plate drawn with <see cref="PlateGlyphs"/>.
    /// No brand, badge, grille pattern, logo, operator livery or emblem (the ambulance has no Red Cross, the police jeep no
    /// weapons). Frame: origin on the ground under the rear axle, +X right, +Y up, +Z forward (the
    /// <see cref="ArcadeVehicle"/> and <see cref="Traffic.AgentPose"/> reference point). Vertex colours only (one ToonLit
    /// material). Wheels are separate (<see cref="BuildWheel"/>, placed at <see cref="Wheels"/>) from LOD0 to LOD1 so they
    /// can spin and steer; LOD2 and the parked levels bake them in. Deterministic; allocation only through
    /// <see cref="MeshData"/> growth.
    /// </summary>
    public static class VehicleMesher
    {
        /// <summary>Triangle caps per level: the moving budgets of W2_DESIGN 10.4 (player 6 k, LOD1 2 k, LOD2 600) and the
        /// parked block-out (80) and box (12).</summary>
        public static int Budget(VehicleLod lod)
        {
            switch (lod)
            {
                case VehicleLod.Lod0: return 6000;
                case VehicleLod.Lod1: return 2000;
                case VehicleLod.Lod2: return 600;
                case VehicleLod.Block: return 80;
                default: return 12;
            }
        }

        public const float WheelScale = 1.15f, CabScale = 1.1f, LengthScale = 0.95f;

        /// <summary>Offset of painted panels (windows, stripes, displays) off the body, as the road decal lift.</summary>
        public const float Decal = 0.012f;

        private static readonly uint Tyre = MeshColor.FromHex(0x23262B), Hub = MeshColor.FromHex(0xB0B6BC), Glass = MeshColor.FromHex(0x2B3A48),
                                     Trim = MeshColor.FromHex(0x2A2D33), Head = MeshColor.FromHex(0xFFF6D8), Tail = MeshColor.FromHex(0xD7263D),
                                     Seat = MeshColor.FromHex(0x3A2E2A), Chrome = MeshColor.FromHex(0xCFD8DC), Skin = MeshColor.FromHex(0xC69C76),
                                     Shirt = MeshColor.FromHex(0x4F6D8F), Trousers = MeshColor.FromHex(0x34383E);

        /// <summary>Body measurements of an entry in the cartoon overlay.</summary>
        public struct Dims
        {
            public float Length, Width, Height, Wheelbase, WheelR, Rear, Front;

            /// <summary>Half width of the body.</summary>
            public float Half
            {
                get { return 0.5f * Width; }
            }
        }

        public static Dims DimsOf(in VehicleCatalogEntry e)
        {
            float l = e.LengthM, wb = e.WheelbaseM, ro = e.RearOverhangM;
            float fo = Math.Max(0.05f, l - wb - ro);
            float k = l - wb > 0.05f ? (LengthScale * l - wb) / (l - wb) : 1f;
            k = Math.Max(0.6f, Math.Min(1f, k));
            return new Dims
            {
                Length = wb + (ro + fo) * k, Width = e.WidthM, Height = e.HeightM, Wheelbase = wb, WheelR = 0.5f * e.WheelDiaM * WheelScale,
                Rear = ro * k, Front = fo * k,
            };
        }

        /// <summary>The wheels of an entry (rear axle at z = 0, front at the wheelbase; tandem rear on the tipper; three
        /// wheels on the tempo and rickshaw; big rear wheels on the tractor).</summary>
        public static WheelSocket[] Wheels(in VehicleCatalogEntry e)
        {
            Dims d = DimsOf(e);
            float r = d.WheelR, w = Math.Max(0.06f, Math.Min(0.45f, 0.13f * d.Width + (IsTwo(e.Shape) ? 0f : 0.08f)));
            float x = d.Half - 0.5f * w + 0.02f;
            switch (e.Shape)
            {
                case BodyShape.Scooter:
                case BodyShape.Motorbike:
                case BodyShape.Cruiser:
                case BodyShape.Bicycle:
                {
                    float tw = e.Shape == BodyShape.Bicycle ? 0.05f : 0.11f;
                    return new[]
                    {
                        new WheelSocket { X = 0f, Y = r, Z = 0f, Radius = r, Width = tw },
                        new WheelSocket { X = 0f, Y = r, Z = d.Wheelbase, Radius = r, Width = tw, Steers = true },
                    };
                }
                case BodyShape.Tempo:
                case BodyShape.Rickshaw:
                    return new[]
                    {
                        new WheelSocket { X = -x, Y = r, Z = 0f, Radius = r, Width = w },
                        new WheelSocket { X = x, Y = r, Z = 0f, Radius = r, Width = w },
                        new WheelSocket { X = 0f, Y = r, Z = d.Wheelbase, Radius = r, Width = w, Steers = true },
                    };
                case BodyShape.Tractor:
                {
                    float rr = 0.5f * 1.40f * WheelScale, rf = 0.5f * 0.80f * WheelScale, ww = 0.42f;
                    return new[]
                    {
                        new WheelSocket { X = -(d.Half - 0.5f * ww), Y = rr, Z = 0f, Radius = rr, Width = ww },
                        new WheelSocket { X = d.Half - 0.5f * ww, Y = rr, Z = 0f, Radius = rr, Width = ww },
                        new WheelSocket { X = -(d.Half - 0.32f), Y = rf, Z = d.Wheelbase, Radius = rf, Width = 0.22f, Steers = true },
                        new WheelSocket { X = d.Half - 0.32f, Y = rf, Z = d.Wheelbase, Radius = rf, Width = 0.22f, Steers = true },
                    };
                }
                case BodyShape.Tipper:
                    return new[]
                    {
                        new WheelSocket { X = -x, Y = r, Z = -0.675f, Radius = r, Width = w },
                        new WheelSocket { X = x, Y = r, Z = -0.675f, Radius = r, Width = w },
                        new WheelSocket { X = -x, Y = r, Z = 0.675f, Radius = r, Width = w },
                        new WheelSocket { X = x, Y = r, Z = 0.675f, Radius = r, Width = w },
                        new WheelSocket { X = -x, Y = r, Z = d.Wheelbase, Radius = r, Width = w, Steers = true },
                        new WheelSocket { X = x, Y = r, Z = d.Wheelbase, Radius = r, Width = w, Steers = true },
                    };
                default:
                    return new[]
                    {
                        new WheelSocket { X = -x, Y = r, Z = 0f, Radius = r, Width = w },
                        new WheelSocket { X = x, Y = r, Z = 0f, Radius = r, Width = w },
                        new WheelSocket { X = -x, Y = r, Z = d.Wheelbase, Radius = r, Width = w, Steers = true },
                        new WheelSocket { X = x, Y = r, Z = d.Wheelbase, Radius = r, Width = w, Steers = true },
                    };
            }
        }

        private static bool IsTwo(BodyShape s)
        {
            return s == BodyShape.Scooter || s == BodyShape.Motorbike || s == BodyShape.Cruiser || s == BodyShape.Bicycle;
        }

        /// <summary>
        /// Builds catalogue entry <paramref name="variant"/> with livery <paramref name="livery"/> at <paramref name="lod"/>
        /// into <paramref name="m"/> (appending). LOD0 and LOD1 leave the wheels out (draw them with
        /// <see cref="BuildWheel"/>); LOD0 numbers the plates from <paramref name="plateSeed"/>. With
        /// <paramref name="rider"/> a two-wheeler gets a simple helmeted rider (traffic; the player's own character comes
        /// from Track D). Returns the triangles added.
        /// </summary>
        public static int Build(int variant, byte livery, VehicleLod lod, MeshData m, uint plateSeed = 0, bool rider = false)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            VehicleCatalogEntry e = VehicleCatalog.At(variant);
            VehicleLivery lv = e.LiveryAt(livery);
            int before = m.IndexCount / 3;
            Dims d = DimsOf(e);
            uint body = MeshColor.FromHex(lv.Body), accent = MeshColor.FromHex(lv.Accent), roof = MeshColor.FromHex(lv.Roof),
                 trim = MeshColor.FromHex(lv.Trim);
            if (lod == VehicleLod.Box)
            {
                Box(m, -d.Rear, d.Wheelbase + d.Front, 0.1f, d.Height, -d.Half, d.Half, body);
                return m.IndexCount / 3 - before;
            }
            if (lod == VehicleLod.Block)
            {
                BuildBlock(e, d, body, roof, m);
                return m.IndexCount / 3 - before;
            }
            var k = new Kit { M = m, Lod = lod };
            switch (e.Shape)
            {
                case BodyShape.Scooter:
                case BodyShape.Motorbike:
                case BodyShape.Cruiser:
                case BodyShape.Bicycle:
                    TwoWheeler(ref k, e, d, body, accent);
                    if (rider) Rider(ref k, e, d);
                    break;
                case BodyShape.Rickshaw:
                    Rickshaw(ref k, e, d, body, accent);
                    break;
                case BodyShape.Tempo:
                    Tempo(ref k, e, d, body, accent, trim, MeshColor.FromHex(lv.Sign));
                    break;
                case BodyShape.Bus:
                    Bus(ref k, e, d, body, accent, roof, MeshColor.FromHex(lv.Sign));
                    break;
                case BodyShape.Truck:
                case BodyShape.Tipper:
                case BodyShape.Tanker:
                    Truck(ref k, e, d, body, accent, MeshColor.FromHex(lv.Sign));
                    break;
                case BodyShape.Tractor:
                    Tractor(ref k, e, d, body, accent);
                    break;
                default:
                    Car(ref k, e, d, body, accent, roof, MeshColor.FromHex(lv.Sign));
                    break;
            }
            if (lod == VehicleLod.Lod2)
                foreach (WheelSocket w in Wheels(e))
                    Wheel(m, w.X, w.Y, w.Z, w.Radius, w.Width, 6, false);
            if (lod <= VehicleLod.Lod1) Plates(ref k, e, d, plateSeed);
            return m.IndexCount / 3 - before;
        }

        /// <summary>One wheel at the origin (axis along X): tyre sides, both caps with a hub on the outer one. LOD0 14
        /// sides, LOD1 10.</summary>
        public static int BuildWheel(float radius, float width, VehicleLod lod, MeshData m)
        {
            int sides = lod == VehicleLod.Lod0 ? 14 : lod == VehicleLod.Lod1 ? 10 : 6;
            return Wheel(m, 0f, 0f, 0f, radius, width, sides, lod == VehicleLod.Lod0);
        }

        // -----------------------------------------------------------------------------------------------------------
        // Primitives (vehicle frame)
        // -----------------------------------------------------------------------------------------------------------

        private struct Kit
        {
            public MeshData M;
            public VehicleLod Lod;

            public bool Detail
            {
                get { return Lod <= VehicleLod.Lod1; }
            }
        }

        /// <summary>An axis-aligned box [z0, z1] × [y0, y1] × [x0, x1] (U = +Z, W = +X).</summary>
        private static int Box(MeshData m, float z0, float z1, float y0, float y1, float x0, float x1, uint c, BoxFaces faces = BoxFaces.All)
        {
            var f = new KitFrame(0, 0, 0, 0, 1);
            return MeshKit.Box(m, f, z0, z1, y0, y1, x0, x1, c, faces);
        }

        private static int Quad(MeshData m, float x0, float y0, float z0, float x1, float y1, float z1, float x2, float y2, float z2,
                                float x3, float y3, float z3, float hx, float hy, float hz, uint c)
        {
            return MeshKit.Quad(m, x0, y0, z0, x1, y1, z1, x2, y2, z2, x3, y3, z3, hx, hy, hz, c);
        }

        /// <summary>A convex side profile (z, y pairs, counter-clockwise seen from +X) extruded across [x0, x1].</summary>
        private static int Prism(MeshData m, float[] zy, float x0, float x1, uint c)
        {
            int n = zy.Length / 2, t = 0;
            float cz = 0f, cy = 0f;
            for (int i = 0; i < n; i++)
            {
                cz += zy[2 * i];
                cy += zy[2 * i + 1];
            }
            cz /= n;
            cy /= n;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                float za = zy[2 * i], ya = zy[2 * i + 1], zb = zy[2 * j], yb = zy[2 * j + 1];
                // Outward normal of the edge in the (z, y) plane.
                float nz = yb - ya, ny = -(zb - za);
                if (nz * (0.5f * (za + zb) - cz) + ny * (0.5f * (ya + yb) - cy) < 0f)
                {
                    nz = -nz;
                    ny = -ny;
                }
                t += Quad(m, x0, ya, za, x1, ya, za, x1, yb, zb, x0, yb, zb, 0f, ny, nz, c);
            }
            for (int i = 1; i + 1 < n; i++)
            {
                t += MeshKit.Tri(m, x1, zy[1], zy[0], x1, zy[2 * i + 1], zy[2 * i], x1, zy[2 * i + 3], zy[2 * i + 2], 1, 0, 0, c);
                t += MeshKit.Tri(m, x0, zy[1], zy[0], x0, zy[2 * i + 1], zy[2 * i], x0, zy[2 * i + 3], zy[2 * i + 2], -1, 0, 0, c);
            }
            return t;
        }

        /// <summary>A wheel: n-sided tyre along X centred at (x, y, z) with caps; a lighter hub on the outer cap.</summary>
        private static int Wheel(MeshData m, float x, float y, float z, float r, float w, int n, bool hub)
        {
            int t = 0;
            float x0 = x - 0.5f * w, x1 = x + 0.5f * w;
            for (int i = 0; i < n; i++)
            {
                double a0 = 2 * Math.PI * i / n, a1 = 2 * Math.PI * (i + 1) / n;
                float y0 = y + r * (float)Math.Cos(a0), z0 = z + r * (float)Math.Sin(a0);
                float y1 = y + r * (float)Math.Cos(a1), z1 = z + r * (float)Math.Sin(a1);
                float hy = (float)Math.Cos(0.5 * (a0 + a1)), hz = (float)Math.Sin(0.5 * (a0 + a1));
                t += Quad(m, x0, y0, z0, x1, y0, z0, x1, y1, z1, x0, y1, z1, 0f, hy, hz, Tyre);
                t += MeshKit.Tri(m, x1, y, z, x1, y0, z0, x1, y1, z1, 1, 0, 0, hub && x >= 0f ? Hub : Tyre);
                t += MeshKit.Tri(m, x0, y, z, x0, y0, z0, x0, y1, z1, -1, 0, 0, hub && x <= 0f ? Hub : Tyre);
            }
            if (hub)
            {
                // A hub disc slightly proud of both faces (a cartoon wheel reads from either side).
                float hr = 0.55f * r;
                for (int s = -1; s <= 1; s += 2)
                    for (int i = 0; i < 6; i++)
                    {
                        double a0 = 2 * Math.PI * i / 6, a1 = 2 * Math.PI * (i + 1) / 6;
                        float px = s > 0 ? x1 + 0.01f : x0 - 0.01f;
                        t += MeshKit.Tri(m, px, y, z, px, y + hr * (float)Math.Cos(a0), z + hr * (float)Math.Sin(a0), px,
                                         y + hr * (float)Math.Cos(a1), z + hr * (float)Math.Sin(a1), s, 0, 0, Hub);
                    }
            }
            return t;
        }

        /// <summary>A rectangle on a side of the body (window, stripe), facing ±X at x, from (z0, y0) to (z1, y1).</summary>
        private static int SidePanel(MeshData m, float x, float z0, float z1, float y0, float y1, uint c)
        {
            float s = x >= 0f ? 1f : -1f;
            return Quad(m, x, y0, z0, x, y0, z1, x, y1, z1, x, y1, z0, s, 0, 0, c);
        }

        /// <summary>A rectangle on the front (+Z) or back (−Z) at z, from (x0, y0) to (x1, y1).</summary>
        private static int EndPanel(MeshData m, float z, bool front, float x0, float x1, float y0, float y1, uint c)
        {
            return Quad(m, x0, y0, z, x1, y0, z, x1, y1, z, x0, y1, z, 0, 0, front ? 1 : -1, c);
        }

        // -----------------------------------------------------------------------------------------------------------
        // Families
        // -----------------------------------------------------------------------------------------------------------

        private static void Car(ref Kit k, in VehicleCatalogEntry e, Dims d, uint body, uint accent, uint roof, uint sign)
        {
            MeshData m = k.M;
            float zr = -d.Rear, zf = d.Wheelbase + d.Front, h = d.Height, hw = d.Half, r = d.WheelR;
            bool van = e.Shape == BodyShape.Van, suv = e.Shape == BodyShape.Suv, pickup = e.Shape == BodyShape.Pickup;
            float floor = 0.55f * r, belt = van ? 0.42f * h : suv ? 0.52f * h : 0.55f * h;
            float hood = van ? belt : belt - 0.04f * h;
            // Lower body: a wedge with a rounded-off nose and tail.
            float[] lower =
            {
                zr, floor, zf - 0.12f, floor, zf, floor + 0.25f * (hood - floor), zf, hood - 0.06f, zf - 0.25f, hood, zr + 0.1f, belt, zr,
                belt - 0.08f,
            };
            Prism(m, lower, -hw, hw, body);
            // Cabin (greenhouse), × 1.1 tall: vans run it to the nose, pickups only over the front seats.
            float cabTop = Math.Min(h * 1.0f, belt + (h - belt) * CabScale);
            float c0 = van ? zr + 0.15f : pickup ? d.Wheelbase * 0.35f : zr + 0.25f * (zf - zr) * 0.6f;
            float c1 = van ? zf - 0.1f : zf - 0.38f * (zf - zr) * (suv ? 0.75f : 0.9f);
            float rake = van ? 0.25f : 0.45f * (cabTop - belt), back = van ? 0.05f : suv ? 0.12f : 0.35f * (cabTop - belt);
            float cw = hw * 0.9f;
            float[] cab = { c0, belt, c1, belt, c1 - rake, cabTop, c0 + back, cabTop };
            Prism(m, cab, -cw, cw, roof);
            if (k.Detail)
            {
                // Windows: windscreen, rear window, side glass (a pillar band left between).
                float wy0 = belt + 0.08f * (cabTop - belt), wy1 = cabTop - 0.08f * (cabTop - belt);
                Quad(m, -cw + 0.08f, wy0, c1 - 0.02f + 0.02f, cw - 0.08f, wy0, c1 + 0.0f, cw - 0.08f, wy1, c1 - rake * 0.92f, -cw + 0.08f, wy1,
                     c1 - rake * 0.92f, 0, rake, cabTop - belt, Glass);
                Quad(m, -cw + 0.1f, wy0, c0 + 0.01f, cw - 0.1f, wy0, c0 + 0.01f, cw - 0.1f, wy1, c0 + back * 0.9f, -cw + 0.1f, wy1, c0 + back * 0.9f, 0,
                     back, -(cabTop - belt), Glass);
                float sz0 = c0 + back + 0.12f, sz1 = c1 - rake - 0.1f, pillar = 0.5f * (sz0 + sz1);
                for (int s = -1; s <= 1; s += 2)
                {
                    float x = s * (cw + Decal);
                    SidePanel(m, x, sz0, pillar - 0.05f, wy0, wy1 - 0.04f, Glass);
                    SidePanel(m, x, pillar + 0.05f, sz1, wy0, wy1 - 0.04f, Glass);
                    // Accent stripe along the belt (taxis and microbuses carry their belt colour here).
                    if (accent != body) SidePanel(m, s * (hw + Decal), zr + 0.15f, zf - 0.2f, belt - 0.16f, belt - 0.04f, accent);
                }
                // Lights and bumpers.
                for (int s = -1; s <= 1; s += 2)
                {
                    Box(m, zf - 0.04f, zf + 0.02f, hood - 0.22f, hood - 0.08f, s * (hw - 0.35f) - 0.12f, s * (hw - 0.35f) + 0.12f, Head);
                    Box(m, zr - 0.02f, zr + 0.04f, belt - 0.24f, belt - 0.1f, s * (hw - 0.25f) - 0.1f, s * (hw - 0.25f) + 0.1f, Tail);
                }
                Box(m, zf - 0.05f, zf + 0.06f, floor, floor + 0.18f, -hw + 0.05f, hw - 0.05f, Trim);
                Box(m, zr - 0.06f, zr + 0.05f, floor, floor + 0.18f, -hw + 0.05f, hw - 0.05f, Trim);
                if (k.Lod == VehicleLod.Lod0)
                    for (int s = -1; s <= 1; s += 2) // mirrors
                        Box(m, c1 - 0.15f, c1 - 0.02f, belt + 0.05f, belt + 0.17f, s * cw, s * (cw + 0.16f) + (s < 0 ? 0f : 0f), Trim);
            }
            if (pickup)
            {
                // Open bed behind the cab.
                float b0 = zr + 0.05f, b1 = c0 - 0.05f, bt = belt + 0.15f;
                Box(m, b0, b1, belt, bt, -hw, -hw + 0.08f, body);
                Box(m, b0, b1, belt, bt, hw - 0.08f, hw, body);
                Box(m, b0, b0 + 0.08f, belt, bt, -hw, hw, body);
            }
            switch (e.TrafficClass)
            {
                case Traffic.VehicleClass.Taxi:
                    // Roof sign (generic "TAXI" box in the sign colour).
                    Box(m, 0.5f * (c0 + c1) - 0.25f, 0.5f * (c0 + c1) + 0.25f, cabTop, cabTop + 0.16f, -0.3f, 0.3f, sign);
                    break;
                case Traffic.VehicleClass.Service:
                    // Police jeep: blue light bar; ambulance: red and white light box. No emblems, no weapons.
                    Box(m, 0.5f * (c0 + c1) - 0.2f, 0.5f * (c0 + c1) + 0.2f, cabTop, cabTop + 0.12f, -0.55f, 0.55f, sign);
                    break;
                case Traffic.VehicleClass.Microbus:
                    if (van) Box(m, c1 - 0.6f, c1 - 0.1f, cabTop, cabTop + 0.28f, -0.6f, 0.6f, sign); // route board
                    break;
            }
        }

        private static void Bus(ref Kit k, in VehicleCatalogEntry e, Dims d, uint body, uint accent, uint roof, uint sign)
        {
            MeshData m = k.M;
            float zr = -d.Rear, zf = d.Wheelbase + d.Front, h = d.Height, hw = d.Half, r = d.WheelR;
            float floor = 0.5f * r;
            float[] side = { zr, floor, zf, floor, zf, h - 0.35f, zf - 0.35f, h, zr + 0.25f, h, zr, h - 0.25f };
            Prism(m, side, -hw, hw, body);
            Box(m, zr + 0.3f, zf - 0.45f, h - 0.02f, h + 0.06f, -hw + 0.2f, hw - 0.2f, roof); // roof cap
            float wy0 = 0.52f * h, wy1 = 0.86f * h;
            if (k.Detail)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    float x = s * (hw + Decal);
                    SidePanel(m, x, zr + 0.4f, zf - 1.2f, wy0, wy1, Glass);
                    SidePanel(m, x, zr + 0.2f, zf - 0.2f, wy0 - 0.25f, wy0 - 0.08f, accent); // band below the windows
                    if (s < 0) SidePanel(m, x - 0.002f, zf - 1.15f, zf - 0.35f, floor + 0.1f, wy1, Glass); // front-left door
                }
                EndPanel(m, zf + Decal, true, -hw + 0.12f, hw - 0.12f, wy0 - 0.05f, h - 0.45f, Glass);
                EndPanel(m, zr - Decal, false, -hw + 0.2f, hw - 0.2f, wy0, wy1, Glass);
                EndPanel(m, zf + 0.01f, true, -hw + 0.3f, hw - 0.3f, h - 0.42f, h - 0.12f, sign); // destination display
                for (int s = -1; s <= 1; s += 2)
                {
                    Box(m, zf - 0.03f, zf + 0.03f, floor + 0.35f, floor + 0.55f, s * (hw - 0.35f) - 0.15f, s * (hw - 0.35f) + 0.15f, Head);
                    Box(m, zr - 0.03f, zr + 0.03f, floor + 0.45f, floor + 0.75f, s * (hw - 0.2f) - 0.1f, s * (hw - 0.2f) + 0.1f, Tail);
                }
                Box(m, zf - 0.06f, zf + 0.08f, floor, floor + 0.25f, -hw, hw, Trim);
            }
            else
            {
                for (int s = -1; s <= 1; s += 2) SidePanel(m, s * (hw + Decal), zr + 0.4f, zf - 0.4f, wy0, wy1, Glass);
            }
        }

        private static void Truck(ref Kit k, in VehicleCatalogEntry e, Dims d, uint body, uint accent, uint sign)
        {
            MeshData m = k.M;
            float zr = -d.Rear, zf = d.Wheelbase + d.Front, h = d.Height, hw = d.Half, r = d.WheelR;
            float floor = 0.75f * r, cabLen = 1.7f, cab0 = zf - cabLen, cabTop = Math.Min(h, 2.6f * CabScale);
            // Chassis rail and cab (cab colour = livery body).
            Box(m, zr + 0.2f, zf - 0.3f, floor, floor + 0.25f, -hw + 0.35f, hw - 0.35f, Trim);
            float[] cab = { cab0, floor, zf, floor, zf, cabTop - 0.5f, zf - 0.35f, cabTop, cab0, cabTop };
            Prism(m, cab, -hw, hw, body);
            if (k.Detail)
            {
                EndPanel(m, zf + Decal, true, -hw + 0.12f, hw - 0.12f, cabTop - 1.15f, cabTop - 0.6f, Glass);
                for (int s = -1; s <= 1; s += 2)
                {
                    SidePanel(m, s * (hw + Decal), cab0 + 0.25f, zf - 0.55f, cabTop - 1.1f, cabTop - 0.3f, Glass);
                    Box(m, zf - 0.02f, zf + 0.04f, floor + 0.3f, floor + 0.5f, s * (hw - 0.3f) - 0.15f, s * (hw - 0.3f) + 0.15f, Head);
                    Box(m, zr - 0.02f, zr + 0.04f, floor + 0.15f, floor + 0.35f, s * (hw - 0.2f) - 0.1f, s * (hw - 0.2f) + 0.1f, Tail);
                }
                Box(m, zf - 0.05f, zf + 0.1f, floor - 0.1f, floor + 0.2f, -hw, hw, Trim);
            }
            float bed0 = zr, bed1 = cab0 - 0.1f, deck = floor + 0.3f;
            switch (e.Shape)
            {
                case BodyShape.Tanker:
                {
                    // A water tank along Z: an octagonal prism in the tank colour with a white band.
                    float tr = Math.Min(hw - 0.05f, 0.5f * (h - deck)), ty = deck + tr;
                    int n = k.Lod == VehicleLod.Lod2 ? 6 : 10;
                    for (int i = 0; i < n; i++)
                    {
                        double a0 = 2 * Math.PI * i / n, a1 = 2 * Math.PI * (i + 1) / n;
                        float x0 = tr * (float)Math.Sin(a0), y0 = ty + tr * (float)Math.Cos(a0), x1 = tr * (float)Math.Sin(a1), y1 = ty + tr * (float)Math.Cos(a1);
                        Quad(m, x0, y0, bed0, x1, y1, bed0, x1, y1, bed1, x0, y0, bed1, (float)Math.Sin(0.5 * (a0 + a1)), (float)Math.Cos(0.5 * (a0 + a1)), 0,
                             body);
                        MeshKit.Tri(m, 0, ty, bed1, x0, y0, bed1, x1, y1, bed1, 0, 0, 1, body);
                        MeshKit.Tri(m, 0, ty, bed0, x0, y0, bed0, x1, y1, bed0, 0, 0, -1, body);
                    }
                    if (k.Detail)
                        for (int s = -1; s <= 1; s += 2)
                            SidePanel(m, s * (tr * 0.98f + 0.01f), bed0 + 0.6f, bed1 - 0.6f, ty - 0.18f, ty + 0.18f, sign);
                    break;
                }
                case BodyShape.Tipper:
                {
                    float top = Math.Min(h, deck + 1.1f);
                    Box(m, bed0, bed1, deck, deck + 0.12f, -hw, hw, accent);
                    Box(m, bed0, bed1, deck, top, -hw, -hw + 0.1f, accent);
                    Box(m, bed0, bed1, deck, top, hw - 0.1f, hw, accent);
                    Box(m, bed1 - 0.1f, bed1, deck, top + 0.3f, -hw, hw, accent); // headboard over the cab
                    Box(m, bed0, bed0 + 0.1f, deck, top, -hw, hw, accent);
                    break;
                }
                default:
                {
                    // Painted cargo body with a tall headboard (3.6 m) in the panel colour; body colour trim.
                    float top = Math.Min(h, deck + 1.4f), headTop = Math.Max(top, 3.6f * 0.95f);
                    Box(m, bed0, bed1, deck, deck + 0.12f, -hw, hw, body);
                    Box(m, bed0, bed1, deck, top, -hw, -hw + 0.08f, accent);
                    Box(m, bed0, bed1, deck, top, hw - 0.08f, hw, accent);
                    Box(m, bed0, bed0 + 0.08f, deck, top, -hw, hw, accent);
                    Box(m, bed1 - 0.12f, bed1, deck, headTop, -hw + 0.1f, hw - 0.1f, accent);
                    if (k.Detail)
                        for (int s = -1; s <= 1; s += 2)
                            SidePanel(m, s * (hw + Decal), bed0 + 0.3f, bed1 - 0.3f, deck + 0.3f, top - 0.25f, body); // art panel
                    break;
                }
            }
        }

        private static void Tempo(ref Kit k, in VehicleCatalogEntry e, Dims d, uint body, uint accent, uint trim, uint sign)
        {
            MeshData m = k.M;
            float zr = -d.Rear, zf = d.Wheelbase + d.Front, h = d.Height, hw = d.Half, r = d.WheelR;
            float floor = 0.8f * r, cab0 = d.Wheelbase - 0.6f;
            // Passenger box (white) with a green belt, black skirt, open rear step.
            Box(m, zr, cab0, floor, floor + 0.25f, -hw, hw, trim);
            Box(m, zr, cab0, floor + 0.25f, h, -hw, hw, body, BoxFaces.All & ~BoxFaces.Back);
            Box(m, zr, zr + 0.06f, floor + 0.25f, floor + 0.55f, -hw, hw, body);
            // Driver's cab: narrower, rounded nose.
            float cw = hw * 0.85f;
            float[] cab = { cab0, floor, zf - 0.1f, floor, zf, floor + 0.5f, zf - 0.25f, h - 0.15f, cab0, h - 0.05f };
            Prism(m, cab, -cw, cw, body);
            if (k.Detail)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    SidePanel(m, s * (hw + Decal), zr + 0.1f, cab0 - 0.1f, floor + 0.45f, floor + 0.62f, accent);
                    SidePanel(m, s * (hw + Decal), zr + 0.3f, cab0 - 0.3f, 0.62f * h + 0.2f, h - 0.25f, Glass);
                }
                Quad(m, -cw + 0.08f, floor + 0.6f, zf - 0.12f, cw - 0.08f, floor + 0.6f, zf - 0.12f, cw - 0.08f, h - 0.3f, zf - 0.27f, -cw + 0.08f,
                     h - 0.3f, zf - 0.27f, 0, 0.2f, 1, Glass);
                Box(m, zf - 0.04f, zf + 0.02f, floor + 0.3f, floor + 0.45f, -0.12f, 0.12f, Head);
                EndPanel(m, zf - 0.2f, true, -0.45f, 0.45f, h - 0.12f, h + 0.1f, sign); // route board
            }
        }

        private static void Tractor(ref Kit k, in VehicleCatalogEntry e, Dims d, uint body, uint accent)
        {
            MeshData m = k.M;
            float zf = d.Wheelbase + d.Front, hw = d.Half;
            float rr = 0.5f * 1.40f * WheelScale;
            // Engine hood running forward from the seat, mudguards over the big rear wheels (rims in the accent).
            Box(m, 0.35f, zf, 0.55f, 1.35f, -0.36f, 0.36f, body);
            Box(m, zf - 0.08f, zf, 0.6f, 1.25f, -0.38f, 0.38f, Trim);
            Box(m, -0.4f, 0.4f, 0.8f, 0.9f, -0.45f, 0.45f, body); // platform
            Box(m, -0.25f, 0.15f, 1.15f, 1.3f, -0.25f, 0.25f, Seat);
            Box(m, -0.3f, -0.22f, 1.25f, 1.6f, -0.25f, 0.25f, Seat);
            for (int s = -1; s <= 1; s += 2)
                Box(m, -rr * 0.9f, rr * 0.9f, 2f * rr - 0.05f, 2f * rr + 0.05f, s * (hw - 0.45f), s * hw + (s < 0 ? 0f : 0f), body);
            if (k.Detail)
            {
                Box(m, 0.9f, 1.0f, 1.35f, 2.1f, 0.18f, 0.26f, Trim); // exhaust stack
                Box(m, zf, zf + 0.03f, 1.0f, 1.15f, -0.25f, 0.25f, Head);
                Box(m, 0.75f, 0.85f, 1.3f, 1.65f, -0.03f, 0.03f, Trim); // steering column
            }
        }

        private static void TwoWheeler(ref Kit k, in VehicleCatalogEntry e, Dims d, uint body, uint accent)
        {
            MeshData m = k.M;
            float wb = d.Wheelbase, r = d.WheelR;
            float seatY = e.Shape == BodyShape.Bicycle ? 0.95f : 0.8f;
            switch (e.Shape)
            {
                case BodyShape.Scooter:
                {
                    Box(m, 0.15f, wb - 0.35f, r * 0.6f, r * 0.6f + 0.08f, -0.2f, 0.2f, body); // floorboard
                    Box(m, -0.15f, 0.5f, r * 0.6f, seatY - 0.05f, -0.27f, 0.27f, body); // rear body over the engine
                    Box(m, -0.05f, 0.55f, seatY - 0.05f, seatY + 0.07f, -0.17f, 0.17f, Seat);
                    Box(m, wb - 0.45f, wb - 0.25f, r * 0.6f, 1.0f, -0.27f, 0.27f, body); // leg shield
                    Box(m, wb - 0.32f, wb - 0.2f, 0.95f, 1.1f, -0.34f, 0.34f, Trim); // handlebar
                    break;
                }
                case BodyShape.Bicycle:
                {
                    float tube = 0.045f;
                    MeshKit.Bar(m, 0, r, 0, 0, seatY - 0.1f, 0.3f * wb, tube, body); // seat stay
                    MeshKit.Bar(m, 0, seatY - 0.1f, 0.3f * wb, 0, r, 0.42f * wb, tube, body); // seat tube to the crank
                    MeshKit.Bar(m, 0, r, 0.42f * wb, 0, seatY - 0.05f, wb * 0.85f, tube, body); // down tube
                    MeshKit.Bar(m, 0, seatY - 0.1f, 0.3f * wb, 0, seatY - 0.05f, wb * 0.85f, tube, body); // top tube
                    MeshKit.Bar(m, 0, seatY + 0.05f, wb * 0.85f, 0, r, wb, tube, body); // fork
                    Box(m, 0.2f * wb, 0.36f * wb, seatY, seatY + 0.05f, -0.08f, 0.08f, Seat);
                    Box(m, wb * 0.82f, wb * 0.9f, seatY + 0.08f, seatY + 0.13f, -0.28f, 0.28f, Trim);
                    break;
                }
                default:
                {
                    bool cruiser = e.Shape == BodyShape.Cruiser;
                    Box(m, 0.25f, 0.75f, r * 0.7f, r * 0.7f + 0.35f, -0.16f, 0.16f, Trim); // engine
                    Box(m, 0.55f, wb - 0.3f, seatY - 0.05f, seatY + 0.15f, -0.17f, 0.17f, body); // tank
                    Box(m, -0.05f, 0.6f, seatY - 0.02f, seatY + 0.08f, -0.15f, 0.15f, Seat);
                    Box(m, -0.25f, 0.1f, seatY - 0.12f, seatY, -0.13f, 0.13f, body); // tail
                    MeshKit.Bar(m, 0.2f, r * 0.7f, 0.18f, 0.22f, r * 0.75f, -0.2f, 0.07f, Chrome); // exhaust
                    MeshKit.Bar(m, 0, seatY + 0.25f, wb - 0.15f, 0, r, wb, 0.06f, Chrome); // fork
                    Box(m, wb - 0.22f, wb - 0.12f, seatY + 0.22f, seatY + 0.28f, cruiser ? -0.4f : -0.35f, cruiser ? 0.4f : 0.35f, Trim);
                    if (k.Detail) Box(m, wb - 0.12f, wb, seatY + 0.05f, seatY + 0.2f, -0.08f, 0.08f, Head); // headlamp
                    break;
                }
            }
            if (k.Detail)
            {
                Box(m, -d.Rear, -d.Rear + 0.05f, r + 0.15f, r + 0.25f, -0.06f, 0.06f, Tail);
                // Mudguards in the accent.
                Box(m, -0.3f, 0.3f, 2f * r, 2f * r + 0.04f, -0.07f, 0.07f, accent);
                Box(m, wb - 0.25f, wb + 0.25f, 2f * r, 2f * r + 0.04f, -0.07f, 0.07f, accent);
            }
        }

        /// <summary>A simple helmeted rider for traffic two-wheelers (W2_DESIGN 5.2: riders always wear helmets).</summary>
        private static void Rider(ref Kit k, in VehicleCatalogEntry e, Dims d)
        {
            MeshData m = k.M;
            float seatY = e.Shape == BodyShape.Bicycle ? 0.98f : 0.86f, z = 0.25f;
            Box(m, z - 0.12f, z + 0.12f, seatY, seatY + 0.55f, -0.2f, 0.2f, Shirt); // torso
            Box(m, z + 0.05f, z + 0.55f, seatY - 0.05f, seatY + 0.12f, -0.2f, -0.06f, Trousers); // thighs
            Box(m, z + 0.05f, z + 0.55f, seatY - 0.05f, seatY + 0.12f, 0.06f, 0.2f, Trousers);
            Box(m, z - 0.1f, z + 0.12f, seatY + 0.6f, seatY + 0.84f, -0.12f, 0.12f, Skin); // head
            Box(m, z - 0.13f, z + 0.15f, seatY + 0.72f, seatY + 0.9f, -0.15f, 0.15f, MeshColor.FromHex(0xE53935)); // helmet
            MeshKit.Bar(m, -0.2f, seatY + 0.45f, z, -0.3f, seatY + 0.25f, d.Wheelbase - 0.25f, 0.07f, Shirt); // arms
            MeshKit.Bar(m, 0.2f, seatY + 0.45f, z, 0.3f, seatY + 0.25f, d.Wheelbase - 0.25f, 0.07f, Shirt);
        }

        private static void Rickshaw(ref Kit k, in VehicleCatalogEntry e, Dims d, uint body, uint accent)
        {
            MeshData m = k.M;
            float wb = d.Wheelbase, hw = d.Half;
            Box(m, -0.4f, 0.4f, 0.55f, 0.95f, -hw, hw, body); // passenger box
            Box(m, -0.35f, 0.3f, 0.95f, 1.05f, -hw + 0.05f, hw - 0.05f, Seat);
            Box(m, -0.45f, -0.35f, 0.95f, 1.4f, -hw, hw, body); // backrest
            // Folding hood over the bench in the accent colour.
            float[] hood = { -0.5f, 1.2f, 0.45f, 1.2f, 0.2f, 1.9f, -0.45f, 1.9f };
            Prism(m, hood, -hw, hw, accent);
            MeshKit.Bar(m, 0, 0.6f, 0.4f, 0, 0.95f, wb - 0.2f, 0.06f, Trim); // frame to the front
            Box(m, wb - 0.6f, wb - 0.35f, 0.95f, 1.0f, -0.1f, 0.1f, Seat); // puller's saddle
            Box(m, wb - 0.15f, wb - 0.05f, 1.05f, 1.1f, -0.28f, 0.28f, Trim);
            // Rear luggage carrier and front basket fill the catalogue length.
            float zr = -Math.Max(d.Rear, 0.55f), zf = wb + Math.Max(d.Front, 0.25f);
            Box(m, zr, -0.45f, 0.5f, 0.58f, -hw * 0.8f, hw * 0.8f, Trim);
            Box(m, wb + 0.05f, zf, 0.75f, 0.95f, -0.16f, 0.16f, accent);
        }

        private static void BuildBlock(in VehicleCatalogEntry e, Dims d, uint body, uint roof, MeshData m)
        {
            float zr = -d.Rear, zf = d.Wheelbase + d.Front, hw = d.Half;
            if (IsTwo(e.Shape) || e.Shape == BodyShape.Rickshaw)
            {
                Box(m, zr, zf, d.WheelR, d.WheelR + 0.4f, -0.18f, 0.18f, body);
                Box(m, -0.05f, 0.55f, 0.75f, 0.9f, -0.17f, 0.17f, Seat);
            }
            else
            {
                float belt = 0.5f * d.Height;
                Box(m, zr, zf, 0.45f * d.WheelR, belt, -hw, hw, body);
                Box(m, zr + 0.15f * (zf - zr), zf - 0.25f * (zf - zr), belt, d.Height, -hw * 0.9f, hw * 0.9f, roof);
            }
            // Wheel boxes; a tandem pair on one side becomes one box (the tipper stays within 80 triangles).
            WheelSocket[] ws = Wheels(e);
            for (int i = 0; i < ws.Length; i++)
            {
                WheelSocket w = ws[i];
                float z0 = w.Z - w.Radius * 0.85f, z1 = w.Z + w.Radius * 0.85f;
                bool merged = false;
                for (int j = 0; j < ws.Length; j++)
                {
                    if (j == i || ws[j].X != w.X || Math.Abs(ws[j].Z - w.Z) > 1.5f) continue;
                    if (j < i)
                    {
                        merged = true;
                        break;
                    }
                    z0 = Math.Min(z0, ws[j].Z - ws[j].Radius * 0.85f);
                    z1 = Math.Max(z1, ws[j].Z + ws[j].Radius * 0.85f);
                }
                if (merged) continue;
                Box(m, z0, z1, 0f, 2f * w.Radius * 0.92f, w.X - 0.5f * w.Width, w.X + 0.5f * w.Width, Tyre);
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        // Plates
        // -----------------------------------------------------------------------------------------------------------

        private static void Plates(ref Kit k, in VehicleCatalogEntry e, Dims d, uint seed)
        {
            PlateText p = VehiclePlates.For(e, seed);
            bool numbers = k.Lod == VehicleLod.Lod0;
            float zr = -d.Rear, zf = d.Wheelbase + d.Front;
            float rearY = IsTwo(e.Shape) ? d.WheelR + 0.3f : Math.Max(0.35f, 0.75f * d.WheelR + 0.2f);
            Plate(k.M, p, zr - 0.07f, rearY, false, p.WidthM, p.HeightM, true, numbers);
            if (p.FrontWidthM > 0f) Plate(k.M, p, zf + 0.07f, Math.Max(0.3f, 0.6f * d.WheelR + 0.15f), true, p.FrontWidthM, p.FrontHeightM, false, numbers);
        }

        /// <summary>A plate on the front (+Z) or rear at z, centred at height y: the background and, at LOD0, the number
        /// in strokes (two lines on rear plates, one on front plates).</summary>
        public static int Plate(MeshData m, PlateText p, float z, float y, bool front, float w, float h, bool twoLines, bool numbers)
        {
            uint bg = MeshColor.FromHex(p.Background), ink = MeshColor.FromHex(p.Ink);
            int t = EndPanel(m, z, front, -0.5f * w, 0.5f * w, y - 0.5f * h, y + 0.5f * h, bg);
            if (!numbers) return t;
            float dz = front ? 0.004f : -0.004f;
            if (twoLines && p.HeightM >= 0.12f)
            {
                t += Text(m, p.Line1, z + dz, front, y + 0.22f * h, w * 0.9f, h * 0.36f, ink);
                t += Text(m, p.Line2, z + dz, front, y - 0.22f * h, w * 0.9f, h * 0.36f, ink);
            }
            else
            {
                t += Text(m, p.Full, z + dz, front, y, w * 0.92f, h * 0.7f, ink);
            }
            return t;
        }

        /// <summary>A line of glyph strokes centred at (0, y) on the plane z, fitted into maxW × maxH.</summary>
        private static int Text(MeshData m, string s, float z, bool front, float y, float maxW, float maxH, uint ink)
        {
            float units = PlateGlyphs.Width(s);
            if (units <= 0f) return 0;
            float scale = Math.Min(maxW / units, maxH / PlateGlyphs.CellH);
            float stroke = 0.65f * scale;
            float xu = -0.5f * units; // glyph units
            int t = 0;
            // Seen from the front a plate's +X is the vehicle's left on the rear plate: mirror the text so it reads.
            float dir = front ? -1f : 1f;
            foreach (char c in s)
            {
                float[] st = PlateGlyphs.Of(c);
                for (int i = 0; i + 3 < st.Length; i += 4)
                {
                    float x0 = dir * (xu + st[i]) * scale, y0 = y + (st[i + 1] - 0.5f * PlateGlyphs.CellH) * scale;
                    float x1 = dir * (xu + st[i + 2]) * scale, y1 = y + (st[i + 3] - 0.5f * PlateGlyphs.CellH) * scale;
                    float dx = x1 - x0, dy = y1 - y0, l = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (l < 1e-6f)
                    {
                        dx = 1f;
                        dy = 0f;
                        l = 1f;
                    }
                    float nx = -dy / l * 0.5f * stroke, ny = dx / l * 0.5f * stroke;
                    float ex = dx / l * 0.5f * stroke, ey = dy / l * 0.5f * stroke; // square caps
                    t += Quad(m, x0 - ex + nx, y0 - ey + ny, z, x1 + ex + nx, y1 + ey + ny, z, x1 + ex - nx, y1 + ey - ny, z, x0 - ex - nx,
                              y0 - ey - ny, z, 0, 0, front ? 1 : -1, ink);
                }
                xu += PlateGlyphs.AdvanceOf(c);
            }
            return t;
        }
    }
}
