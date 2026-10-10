using System;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;

namespace Ghumante.Characters.Rides
{
    /// <summary>Which cockpit a body gets (<see cref="CockpitMesher"/>).</summary>
    public enum CockpitKind : byte
    {
        /// <summary>Open vehicles (two-wheelers, the tractor, the rickshaw): the body itself is the view.</summary>
        None = 0,

        /// <summary>Hatchbacks, taxis, SUVs and pickups: a bonnet ahead and a raked windscreen.</summary>
        Car = 1,

        /// <summary>Microvans and Safa tempos: cab-forward, the windscreen near the nose, bench rows behind.</summary>
        Van = 2,

        /// <summary>Buses: a wide flat dash, a split windscreen, the saloon with its seat rows behind.</summary>
        Bus = 3,

        /// <summary>Trucks, tippers and tankers: a high cab-forward cab with a back wall.</summary>
        Truck = 4,
    }

    /// <summary>The body a cockpit is fitted to, in the vehicle frame (origin on the ground under the rear axle, +Z
    /// forward, +X right, +Y up, metres) with the driver's seat socket and the livery's colours.</summary>
    public struct CockpitSpec
    {
        public CockpitKind Kind;

        /// <summary>Half the body width, the roof height, the nose and the tail of the body.</summary>
        public float HalfWidth, Height, NoseZ, TailZ;

        /// <summary>The driver's seat socket (<see cref="VehicleSeats"/>).</summary>
        public float SeatX, SeatY, SeatZ;

        /// <summary>The livery's body and trim colours, 0xRRGGBB.</summary>
        public uint BodyRgb, TrimRgb;

        /// <summary>No doors (the Safa tempo's open cab).</summary>
        public bool OpenSides;

        /// <summary>The cockpit of catalogue entry <paramref name="e"/> in livery <paramref name="livery"/> (allocates the
        /// seat list; call once when the cockpit is built).</summary>
        public static CockpitSpec For(in VehicleCatalogEntry e, int livery)
        {
            VehicleMesher.Dims d = VehicleMesher.DimsOf(e);
            SeatSocket[] seats = VehicleSeats.For(e);
            VehicleLivery l = e.LiveryAt(Math.Max(0, Math.Min(e.LiveryCount - 1, livery)));
            var s = new CockpitSpec
            {
                Kind = CockpitMesher.KindOf(e.Shape), HalfWidth = d.Half, Height = d.Height, NoseZ = d.Wheelbase + d.Front, TailZ = -d.Rear,
                                            BodyRgb = l.Body, TrimRgb = l.Trim, OpenSides = e.Shape == BodyShape.Tempo, };
            if (seats.Length > 0)
            {
                s.SeatX = seats[0].X;
                s.SeatY = seats[0].Y;
                s.SeatZ = seats[0].Z;
            }
            return s;
        }
    }

    /// <summary>
    /// A stand-in cabin for the player's own closed vehicle while a mounted camera sits inside its body (the car's
    /// <c>Interior</c> view, the bus and truck <c>DriverSeat</c>): the vehicle meshes have no interior, and from inside a
    /// back-face-culled shell shows nothing but the driver's floating arms (and the outline pass would wrap the camera in
    /// the outline colour). Built from the body's size and the driver's seat: the dashboard with an instrument binnacle,
    /// gauges, vents and a centre console, the steering column, A-pillars, the windscreen header and headliner, roof rails,
    /// belt lines, door cards and B-pillars, sun visors, the mirror with a little mala, seats; buses add the split
    /// windscreen's centre pillar, the destination-board housing, a tinsel garland and prayer flags, a framed picture,
    /// the saloon's window pillars, grab rails and rows of seats with white covers; trucks a back wall. The steering wheel
    /// is a separate mesh (<see cref="BuildWheel"/>) that turns with the steering (<see cref="WheelAngleDeg"/>). Rounded
    /// throughout, UV0 = material channel and baked AO (docs/W2_DETAIL_CONTRACT.md §5). Engine-free and deterministic;
    /// the vehicles package's interior LOD will replace it (open issue).
    /// </summary>
    public static class CockpitMesher
    {
        /// <summary>The camera's eye above the seat socket (head bone 0.55 m + <c>CameraViews.EyeUpM</c>), for layout.</summary>
        public const float EyeAboveSeatM = 0.77f;

        /// <summary>The camera's eye ahead of the seat socket (head bone −0.05 m + <c>CameraViews.EyeForwardM</c>).</summary>
        public const float EyeAheadOfSeatM = 0.27f;

        /// <summary>The dashboard's rear top edge sits this far below the eye's level (seen from the eye), degrees: inside
        /// the bottom of a landscape interior view (the eye views look 5-6° down with a ±18° vertical field).</summary>
        public const float DashDownDeg = 19f;

        /// <summary>The windscreen header sits this far above the eye line (seen from the eye), degrees: the top edge of a
        /// landscape interior view (buses and trucks, with taller screens, go higher).</summary>
        public const float HeaderUpDeg = 9f;

        /// <summary>Most triangles the cabin may have (one instance, only while the camera is inside).</summary>
        public const int MaxTriangles = 24000;

        // Car steering wheel (CharacterPoser.PoseSeated: CarDriver; BusDriver and TruckDriver for heavy).
        private static readonly float[] WheelRadius = { 0f, 0.19f, 0.19f, 0.25f, 0.25f };
        private static readonly float[] WheelTilt = { 0f, 25f, 25f, 60f, 60f };
        private static readonly float[] WheelRatio = { 0f, 15f, 15f, 20f, 20f };

        private const uint Dash = 0x3B3F46, DashPad = 0x2E3137, Gauge = 0xF3EEDC, Needle = 0xE0452B, Chrome = 0xC9CDD2, Headliner = 0xD6D0C4;
        private const uint Rubber = 0x26282C, Screen = 0x2D4C5E, Fabric = 0x8C8478, BusSeat = 0x8E2B2B, Cover = 0xF4F1EA, Floor = 0x4A4C50;
        private const uint Mala = 0xC2412F, Bead = 0xF0B23A, Tinsel = 0xE8B83A, TinselRed = 0xD23A3A, GiltFrame = 0xC99A3E;

        private static readonly uint[] FlagColours = { 0x2F6FD0, 0xF2F2EE, 0xD7362F, 0x3E9A4A, 0xF2C232 };

        /// <summary>The cockpit kind of a body shape.</summary>
        public static CockpitKind KindOf(BodyShape shape)
        {
            switch (shape)
            {
                case BodyShape.Hatchback:
                case BodyShape.Suv:
                case BodyShape.Pickup: return CockpitKind.Car;
                case BodyShape.Van:
                case BodyShape.Tempo: return CockpitKind.Van;
                case BodyShape.Bus: return CockpitKind.Bus;
                case BodyShape.Truck:
                case BodyShape.Tipper:
                case BodyShape.Tanker: return CockpitKind.Truck;
                default: return CockpitKind.None;
            }
        }

        /// <summary>Where the steering wheel sits: its hub in the vehicle frame, its tilt back from vertical (degrees) and
        /// rim radius, as the driver's hands hold it (CharacterPoser).</summary>
        public static void WheelPlacement(in CockpitSpec s, out float x, out float y, out float z, out float tiltDeg, out float radius)
        {
            bool heavy = s.Kind == CockpitKind.Bus || s.Kind == CockpitKind.Truck;
            x = s.SeatX;
            y = s.SeatY + (heavy ? 0.30f : 0.34f);
            z = s.SeatZ + (heavy ? 0.30f : 0.27f);
            tiltDeg = WheelTilt[(int)s.Kind];
            radius = WheelRadius[(int)s.Kind];
        }

        /// <summary>The wheel's turn for a steering input in [−1, 1] (positive right), degrees clockwise as the driver sees
        /// it: steer × 35° × the steering ratio, held to ±100° like the hands (hand over hand beyond).</summary>
        public static float WheelAngleDeg(in CockpitSpec s, float steer)
        {
            if (float.IsNaN(steer)) steer = 0f;
            steer = Math.Max(-1f, Math.Min(1f, steer));
            return Math.Max(-100f, Math.Min(100f, steer * 35f * WheelRatio[(int)s.Kind]));
        }

        /// <summary>The camera's eye in the vehicle frame (an estimate for layout and previews).</summary>
        public static void Eye(in CockpitSpec s, out float x, out float y, out float z)
        {
            x = s.SeatX;
            y = s.SeatY + EyeAboveSeatM;
            z = s.SeatZ + EyeAheadOfSeatM;
        }

        /// <summary>True when (x, y, z) in the vehicle frame lies inside the body's box (a camera there sees the cabin).</summary>
        public static bool Inside(in CockpitSpec s, float x, float y, float z)
        {
            return Math.Abs(x) < s.HalfWidth + 0.02f && y > 0.25f && y < s.Height + 0.05f && z > s.TailZ - 0.02f && z < s.NoseZ + 0.02f;
        }

        /// <summary>Appends the cabin (everything but the steering wheel) to <paramref name="m"/> in the vehicle frame.</summary>
        public static void Build(in CockpitSpec s, MeshData m)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            if (s.Kind == CockpitKind.None) return;
            var g = new Layout(s);
            Dashboard(s, g, m);
            Column(s, g, m);
            WindscreenFrame(s, g, m);
            Roof(s, g, m);
            Sides(s, g, m);
            Mirror(s, g, m);
            DashTop(s, g, m);
            Seats(s, g, m);
            if (s.Kind == CockpitKind.Bus) BusSaloon(s, g, m);
            if (s.Kind == CockpitKind.Bus || s.Kind == CockpitKind.Truck) Decorations(s, g, m);
            if (s.Kind == CockpitKind.Truck) BackWall(s, g, m);
        }

        /// <summary>The steering wheel in its own frame: hub at the origin, rim in the XY plane (+Y up at rest), the driver
        /// on −Z. Place it at <see cref="WheelPlacement"/> tilted back by the tilt about X, then turn it about its own Z by
        /// −<see cref="WheelAngleDeg"/>.</summary>
        public static void BuildWheel(in CockpitSpec s, MeshData m)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            if (s.Kind == CockpitKind.None) return;
            float r = WheelRadius[(int)s.Kind];
            bool heavy = s.Kind == CockpitKind.Bus || s.Kind == CockpitKind.Truck;
            float tube = heavy ? 0.026f : 0.022f;
            Frame f = Frame.Identity;
            Torus(m, f, r, tube, 40, 10, Rubber, MaterialChannel.Rubber, 0.95f);
            // Hub with the horn pad, a little chrome ring and the spokes (three on a car, two on a bus or truck).
            Lathe(m, f, 0f, 0f, -0.035f, 0f, 0f, 1f, new[]
            {
                P(0f, 0f, 0f, -1f), P(0.055f, 0f, 0f, -1f), P(0.065f, 0.012f, 0.9f, -0.4f), P(0.065f, 0.05f, 1f, 0f), P(0.05f, 0.06f, 0.6f, 0.8f),
                  P(0f, 0.06f, 0f, 1f), }, 20, Dash, MaterialChannel.Plain, 0.9f);
            Lathe(m, f, 0f, 0f, -0.04f, 0f, 0f, 1f, new[] { P(0.045f, 0f, 0f, -1f), P(0.052f, 0.006f, 1f, 0f), P(0.045f, 0.012f, 0f, 1f) }, 20,
                  Chrome, MaterialChannel.Metal, 1f);
            int spokes = heavy ? 2 : 3;
            for (int i = 0; i < spokes; i++)
            {
                double a = heavy ? (i == 0 ? Math.PI / 2 : -Math.PI / 2) : (i == 0 ? Math.PI / 2 : i == 1 ? -Math.PI / 2 : Math.PI);
                float ex = (float)Math.Sin(a) * (r - 0.01f), ey = (float)Math.Cos(a) * (r - 0.01f);
                Capsule(m, f, 0.4f * ex, 0.4f * ey, -0.01f, ex, ey, 0f, 0.016f, 8, 2, Dash, MaterialChannel.Plain, 0.9f);
            }
        }

        // ----- Layout -------------------------------------------------------------------------------------------------

        /// <summary>The cabin's key lines, derived once from the spec and the eye: the dashboard's visible edge sits
        /// <see cref="DashDownDeg"/> below the eye line (a car's padded rear edge; a cab-forward body's windscreen base, its
        /// shelf falling away towards the driver), the windscreen header <see cref="HeaderUpDeg"/> above it (more on buses
        /// and trucks, whose screens are tall), and the roof lining at least a hand above the header even where the
        /// cartoon body is lower (from inside the shell's roof is never seen).</summary>
        private readonly struct Layout
        {
            public readonly bool Heavy, CabForward;
            public readonly float Wi, RoofIn, EyeY, EyeZ;
            public readonly float DashRearZ, DashFrontZ, DashTopY, DashFrontY, DashBottomY;
            public readonly float HeaderY, HeaderZ, BeltY, FloorY;

            /// <summary>The back of the cabin: the tail, or a truck cab's back wall (the load bed is outside).</summary>
            public readonly float RearZ;

            public Layout(in CockpitSpec s)
            {
                Heavy = s.Kind == CockpitKind.Bus || s.Kind == CockpitKind.Truck;
                CabForward = s.Kind != CockpitKind.Car;
                float wall = Heavy ? 0.09f : 0.07f;
                Wi = s.HalfWidth - wall;
                EyeY = s.SeatY + EyeAboveSeatM;
                EyeZ = s.SeatZ + EyeAheadOfSeatM;
                float tan = (float)Math.Tan(DashDownDeg * Math.PI / 180.0);
                float depth = s.Kind == CockpitKind.Car ? 0.45f : s.Kind == CockpitKind.Van ? 0.3f : 0.4f;
                if (!CabForward)
                {
                    // A bonnet ahead: the padded rear edge at the sight line, the top falling a little to the glass.
                    float front = s.NoseZ - 0.45f;
                    DashRearZ = Math.Max(EyeZ + 0.3f, Math.Min(EyeZ + 0.62f, front - depth));
                    DashFrontZ = Math.Max(DashRearZ + 0.2f, Math.Min(front, DashRearZ + depth));
                    DashTopY = EyeY - (DashRearZ - EyeZ) * tan;
                    DashFrontY = DashTopY - 0.05f;
                }
                else
                {
                    // Cab-forward: the windscreen base at the sight line, the shelf falling away towards the driver.
                    float front = s.NoseZ - 0.08f;
                    DashRearZ = Math.Max(EyeZ + 0.22f, front - depth);
                    DashFrontZ = Math.Max(DashRearZ + 0.2f, front);
                    DashFrontY = EyeY - (DashFrontZ - EyeZ) * tan;
                    DashTopY = Math.Min(DashFrontY - 0.06f, EyeY - (Heavy ? 0.3f : 0.26f));
                }
                DashBottomY = Math.Max(s.SeatY + 0.05f, DashTopY - (Heavy ? 0.55f : 0.42f));
                float rake = s.Kind == CockpitKind.Car ? 0.45f : s.Kind == CockpitKind.Van ? 0.12f : s.Kind == CockpitKind.Truck ? 0.1f : 0.05f;
                HeaderZ = Math.Max(EyeZ + 0.18f, DashFrontZ - rake);
                float up = (float)Math.Tan((s.Kind == CockpitKind.Bus ? 30f : s.Kind == CockpitKind.Truck ? 25f : HeaderUpDeg) * Math.PI / 180.0);
                HeaderY = EyeY + (HeaderZ - EyeZ) * up;
                RoofIn = Math.Max(s.Height - (Heavy ? 0.08f : 0.05f), HeaderY + (s.Kind == CockpitKind.Bus ? 0.3f : 0.06f));
                BeltY = Math.Max(DashTopY, DashFrontY) + (Heavy ? 0.05f : 0.02f);
                FloorY = Math.Max(0.3f, s.SeatY - (Heavy ? 0.5f : 0.38f));
                RearZ = s.Kind == CockpitKind.Truck ? Math.Max(s.TailZ, s.SeatZ - 0.58f) : s.TailZ;
            }
        }

        // ----- Parts --------------------------------------------------------------------------------------------------

        private static void Dashboard(in CockpitSpec s, in Layout g, MeshData m)
        {
            float wi = g.Wi;
            // The dash: a rounded slab whose top runs from the rear edge to the windscreen base, a soft pad along the rear.
            float dz = g.DashFrontZ - g.DashRearZ, dy = g.DashFrontY - g.DashTopY;
            float len = (float)Math.Sqrt(dz * dz + dy * dy);
            float slope = (float)(Math.Atan2(dy, dz) * 180.0 / Math.PI);
            float thick = Math.Max(0.12f, Math.Min(g.DashTopY, g.DashFrontY) - g.DashBottomY);
            // Local frame: +Z up the top surface, +Y its normal; the slab hangs below the top surface.
            Frame top = Frame.Tilted(0f, 0.5f * (g.DashTopY + g.DashFrontY), 0.5f * (g.DashRearZ + g.DashFrontZ), -slope);
            RoundedBox(m, top, 0f, -0.5f * thick, 0f, wi, 0.5f * thick, 0.5f * len, 0.05f, 2, Dash, MaterialChannel.Plain, 0.85f);
            Capsule(m, Frame.Identity, -wi + 0.04f, g.DashTopY - 0.025f, g.DashRearZ + 0.02f, wi - 0.04f, g.DashTopY - 0.025f, g.DashRearZ + 0.02f,
                    0.045f, 10, 2, DashPad, MaterialChannel.Leather, 0.9f);
            if (g.CabForward)
            {
                // The lower face under the shelf, down to the floor line, where the controls are.
                RoundedBox(m, Frame.Identity, 0f, 0.5f * (g.DashTopY + g.DashBottomY), g.DashRearZ + 0.1f, wi, 0.5f * (g.DashTopY - g.DashBottomY),
                           0.1f, 0.04f, 2, Dash, MaterialChannel.Plain, 0.8f);
            }
            Frame f = Frame.Identity;
            // Instrument binnacle in front of the driver: on the dash top in a car; a cowl on the rear face of a
            // cab-forward dash (below the windscreen line, as in vans, buses and trucks). Gauges face the driver.
            float bx = s.SeatX, bw = g.Heavy ? 0.24f : 0.18f, bh = g.Heavy ? 0.075f : 0.06f;
            float by = g.CabForward ? g.DashTopY - bh - 0.035f : g.DashTopY + bh - 0.01f;
            float bz = g.CabForward ? g.DashRearZ - 0.02f : g.DashRearZ + 0.11f;
            RoundedBox(m, f, bx, by, bz, bw, bh, 0.09f, 0.04f, 2, DashPad, MaterialChannel.Leather, 0.9f);
            int gauges = g.Heavy ? 4 : 2;
            for (int i = 0; i < gauges; i++)
            {
                float gx = bx + (i - 0.5f * (gauges - 1)) * (g.Heavy ? 0.105f : 0.15f);
                float gr = g.Heavy ? 0.045f : 0.055f;
                Frame face = Frame.Facing(gx, by - 0.005f, bz - 0.092f, 0f, 0f, -1f);
                Disc(m, face, gr, 0.008f, 20, Gauge, MaterialChannel.Plain, 1f);
                Lathe(m, face, 0f, 0f, -0.002f, 0f, 0f, 1f, new[] { P(gr, 0f, 0f, -1f), P(gr + 0.008f, 0.004f, 1f, 0f), P(gr, 0.012f, 0f, 1f) },
                      20, Chrome, MaterialChannel.Metal, 1f);
                // Needle: a thin rounded bar from the centre up and to the left (idle).
                Capsule(m, face, 0f, 0f, 0.012f, -0.6f * gr, 0.55f * gr, 0.012f, 0.0045f, 6, 2, Needle, MaterialChannel.Paint, 1f);
            }
            // Centre console: a rounded stack below the dash in the middle with a screen and knobs.
            float cz = g.DashRearZ + (g.CabForward ? -0.02f : 0.04f), cTop = g.DashTopY - 0.08f, cBottom = g.FloorY + 0.05f;
            RoundedBox(m, f, 0f, 0.5f * (cTop + cBottom), cz + 0.12f, g.Heavy ? 0.2f : 0.13f, 0.5f * (cTop - cBottom), 0.14f, 0.04f, 2, Dash,
                       MaterialChannel.Plain, 0.75f);
            Frame screen = Frame.Facing(0f, cTop - 0.09f, cz - 0.022f, 0f, 0f, -1f);
            RoundedBox(m, screen, 0f, 0f, 0f, g.Heavy ? 0.12f : 0.085f, 0.05f, 0.01f, 0.008f, 2, Screen, MaterialChannel.Glass, 1f);
            for (int k = -1; k <= 1; k += 2)
            {
                Frame knob = Frame.Facing(k * 0.06f, cTop - 0.19f, cz - 0.02f, 0f, 0f, -1f);
                Disc(m, knob, 0.018f, 0.02f, 12, Chrome, MaterialChannel.Metal, 1f);
            }
            // Air vents at both ends and either side of the console: slatted rounded slots on the dash's rear face.
            float ventY = g.DashTopY - 0.075f;
            float[] vx = { -wi + 0.16f, -0.21f, 0.21f, wi - 0.16f };
            for (int i = 0; i < vx.Length; i++)
            {
                if (Math.Abs(vx[i] - bx) < bw + 0.08f) continue; // not behind the binnacle
                Frame vent = Frame.Facing(vx[i], ventY, g.DashRearZ - 0.005f, 0f, 0f, -1f);
                RoundedBox(m, vent, 0f, 0f, 0f, 0.07f, 0.03f, 0.012f, 0.01f, 2, Rubber, MaterialChannel.Rubber, 0.7f);
                for (int sl = -1; sl <= 1; sl++)
                    RoundedBox(m, vent, 0f, sl * 0.017f, 0.014f, 0.062f, 0.0035f, 0.004f, 0f, 1, Chrome, MaterialChannel.Metal, 0.9f);
            }
            // The glovebox line on the passenger side: a raised rounded lid.
            RoundedBox(m, f, -0.5f * wi, g.DashTopY - 0.2f, g.DashRearZ - 0.005f, 0.24f, 0.08f, 0.012f, 0.012f, 2, DashPad, MaterialChannel.Plain,
                       0.85f);
        }

        private static void Column(in CockpitSpec s, in Layout g, MeshData m)
        {
            float x, y, z, tilt, r;
            WheelPlacement(s, out x, out y, out z, out tilt, out r);
            double t = tilt * Math.PI / 180.0;
            float ay = (float)Math.Sin(t), az = (float)Math.Cos(t); // the wheel's normal, away from the driver
            // The column runs along the wheel's axis until it is level with the shroud under the dash's rear edge (or
            // reaches the dash); a rounded shroud carries it on into the dash, so it never stands free.
            float shroudY = g.DashTopY - 0.13f;
            float len = 0.08f;
            for (int i = 0; i < 60; i++)
            {
                float py = y + ay * len, pz = z + az * len;
                if (pz >= g.DashRearZ || py >= shroudY) break;
                len += 0.02f;
            }
            float ex = x, ey = y + ay * len, ez = z + az * len;
            Frame f = Frame.Identity;
            float cr = g.Heavy ? 0.045f : 0.035f;
            Capsule(m, f, x, y + ay * 0.05f, z + az * 0.05f, ex, ey, ez, cr, 10, 2, Rubber, MaterialChannel.Rubber, 0.8f);
            if (ez < g.DashRearZ + 0.05f)
            {
                float sz0 = ez - 0.06f, sz1 = g.DashRearZ + 0.08f;
                RoundedBox(m, f, ex, ey, 0.5f * (sz0 + sz1), cr + 0.035f, cr + 0.03f, 0.5f * (sz1 - sz0), 0.04f, 2, Dash, MaterialChannel.Plain,
                           0.8f);
            }
            // Stalks (indicator and wipers) either side of the column.
            for (int k = -1; k <= 1; k += 2)
            {
                float sx = x + k * 0.05f, sy = y + ay * 0.1f, sz = z + az * 0.1f;
                Capsule(m, f, sx, sy, sz, x + k * 0.17f, sy - 0.01f, sz - 0.02f, 0.008f, 6, 2, Rubber, MaterialChannel.Rubber, 0.85f);
            }
        }

        private static void WindscreenFrame(in CockpitSpec s, in Layout g, MeshData m)
        {
            Frame f = Frame.Identity;
            float pr = g.Heavy ? 0.06f : 0.045f;
            uint pillar = MeshColor.Lerp(MeshColor.FromHex(Dash), MeshColor.FromHex(s.BodyRgb), 0.35f) >> 8;
            for (int k = -1; k <= 1; k += 2)
            {
                // A-pillar from the dash corner up to the roof rail, tapering a little.
                Tube(m, f, k * (g.Wi - 0.02f), g.DashFrontY - 0.02f, g.DashFrontZ - 0.02f, k * (g.Wi - 0.05f), g.HeaderY + 0.02f, g.HeaderZ, pr,
                     pr * 0.85f, 14, pillar, MaterialChannel.Paint, 0.9f);
            }
            // Header rail across the top of the windscreen.
            Capsule(m, f, -(g.Wi - 0.05f), g.HeaderY + 0.01f, g.HeaderZ, g.Wi - 0.05f, g.HeaderY + 0.01f, g.HeaderZ, g.Heavy ? 0.05f : 0.04f, 10,
                    2, pillar, MaterialChannel.Paint, 0.9f);
            // A slim sill along the windscreen base where it meets the dash.
            Capsule(m, f, -(g.Wi - 0.04f), g.DashFrontY + 0.005f, g.DashFrontZ - 0.03f, g.Wi - 0.04f, g.DashFrontY + 0.005f, g.DashFrontZ - 0.03f,
                    0.02f, 10, 3, Rubber, MaterialChannel.Rubber, 0.8f);
            // The split windscreen's centre pillar on a bus.
            if (s.Kind == CockpitKind.Bus)
                Tube(m, f, 0f, g.DashFrontY, g.DashFrontZ - 0.03f, 0f, g.HeaderY + 0.02f, g.HeaderZ, 0.04f, 0.04f, 10, pillar,
                     MaterialChannel.Paint, 0.9f);
            // A tall cab (bus, truck, van) closes the space between the screen's top and the roof: the destination board's
            // housing on a bus, a header panel elsewhere.
            if (g.RoofIn - g.HeaderY > 0.15f)
            {
                uint panel = s.Kind == CockpitKind.Bus
                    ? MeshColor.Lerp(MeshColor.FromHex(Headliner), MeshColor.FromHex(s.TrimRgb), 0.25f) >> 8
                    : Headliner;
                RoundedBox(m, f, 0f, 0.5f * (g.HeaderY + g.RoofIn), g.HeaderZ - 0.1f, g.Wi - 0.02f, 0.5f * (g.RoofIn - g.HeaderY), 0.14f, 0.05f, 2,
                           panel, MaterialChannel.Plaster, 0.85f);
            }
        }

        private static void Roof(in CockpitSpec s, in Layout g, MeshData m)
        {
            Frame f = Frame.Identity;
            float rear = g.RearZ + (s.Kind == CockpitKind.Truck ? 0.03f : 0.12f), front = g.HeaderZ + 0.02f;
            float y = g.RoofIn + 0.015f;
            if (s.Kind == CockpitKind.Car && front - rear > 1.6f)
            {
                // Cars get a big sunroof: a lining band behind the header, one over the back seats and strips along the
                // sides, round an opening rimmed in rubber (a tall portrait view looks up at the sky, not at cloth).
                float bandF = 0.3f, bandR = 0.55f, side = Math.Min(0.2f, 0.25f * g.Wi);
                float oz0 = rear + bandR, oz1 = front - bandF, ox = g.Wi - side;
                RoundedBox(m, f, 0f, y, front - 0.5f * bandF, g.Wi, 0.02f, 0.5f * bandF, 0.015f, 2, Headliner, MaterialChannel.Fabric, 0.9f);
                RoundedBox(m, f, 0f, y, rear + 0.5f * bandR, g.Wi, 0.02f, 0.5f * bandR, 0.015f, 2, Headliner, MaterialChannel.Fabric, 0.9f);
                for (int k = -1; k <= 1; k += 2)
                    RoundedBox(m, f, k * (g.Wi - 0.5f * side), y, 0.5f * (oz0 + oz1), 0.5f * side, 0.02f, 0.5f * (oz1 - oz0), 0.015f, 2, Headliner,
                               MaterialChannel.Fabric, 0.9f);
                float ry = y - 0.02f;
                Capsule(m, f, -ox, ry, oz0, ox, ry, oz0, 0.018f, 8, 2, Rubber, MaterialChannel.Rubber, 0.85f);
                Capsule(m, f, -ox, ry, oz1, ox, ry, oz1, 0.018f, 8, 2, Rubber, MaterialChannel.Rubber, 0.85f);
                for (int k = -1; k <= 1; k += 2)
                    Capsule(m, f, k * ox, ry, oz0, k * ox, ry, oz1, 0.018f, 8, 2, Rubber, MaterialChannel.Rubber, 0.85f);
            }
            else
            {
                // Headliner: a soft thin panel under the roof, the whole length.
                RoundedBox(m, f, 0f, y, 0.5f * (rear + front), g.Wi, 0.02f, 0.5f * (front - rear), 0.015f, 2, Headliner, MaterialChannel.Fabric,
                           0.9f);
            }
            // Roof rails along both sides.
            uint rail = MeshColor.Lerp(MeshColor.FromHex(Headliner), MeshColor.FromHex(Dash), 0.3f) >> 8;
            for (int k = -1; k <= 1; k += 2)
                Capsule(m, f, k * (g.Wi - 0.03f), g.RoofIn - 0.03f, rear + 0.05f, k * (g.Wi - 0.03f), g.RoofIn - 0.03f, front, 0.035f, 8, 2, rail,
                        MaterialChannel.Fabric, 0.85f);
            if (s.Kind != CockpitKind.Bus)
            {
                // Sun visors folded up under the header, a dome light behind them.
                for (int k = -1; k <= 1; k += 2)
                {
                    float vx = k * Math.Min(g.Wi - 0.3f, 0.36f);
                    RoundedBox(m, f, vx, g.RoofIn - 0.035f, front - 0.14f, 0.17f, 0.018f, 0.1f, 0.016f, 2, Headliner, MaterialChannel.Fabric, 0.8f);
                }
                float lz = s.Kind == CockpitKind.Car ? rear + 0.3f : 0.5f * (g.HeaderZ + s.SeatZ) - 0.2f;
                RoundedBox(m, f, 0f, g.RoofIn - 0.01f, lz, 0.07f, 0.015f, 0.045f, 0.012f, 2, Cover, MaterialChannel.Glass, 1f);
            }
        }

        private static void Sides(in CockpitSpec s, in Layout g, MeshData m)
        {
            Frame f = Frame.Identity;
            uint panel = MeshColor.Lerp(MeshColor.FromHex(s.BodyRgb), MeshColor.FromHex(Dash), 0.45f) >> 8;
            uint rail = MeshColor.Lerp(MeshColor.FromHex(Dash), MeshColor.FromHex(s.BodyRgb), 0.2f) >> 8;
            float rear = g.RearZ + (s.Kind == CockpitKind.Truck ? 0.05f : 0.2f), front = g.DashFrontZ - 0.04f;
            float pillarR = g.Heavy ? 0.05f : 0.04f;
            for (int k = -1; k <= 1; k += 2)
            {
                float x = k * (g.Wi - 0.01f);
                // Belt line (the window sill), the full length of the cabin.
                Capsule(m, f, x, g.BeltY, rear, x, g.BeltY, front, g.Heavy ? 0.045f : 0.038f, 8, 2, rail, MaterialChannel.Paint, 0.9f);
                if (!s.OpenSides)
                {
                    // Door cards / side panels under the belt, with an armrest by the front seats.
                    float bottom = g.FloorY + 0.02f;
                    RoundedBox(m, f, k * (g.Wi - 0.035f), 0.5f * (g.BeltY + bottom), 0.5f * (rear + front), 0.03f, 0.5f * (g.BeltY - bottom),
                               0.5f * (front - rear), 0.015f, 2, panel, MaterialChannel.Paint, 0.85f);
                    RoundedBox(m, f, k * (g.Wi - 0.08f), g.BeltY - 0.2f, s.SeatZ + 0.1f, 0.04f, 0.03f, 0.25f, 0.018f, 2, DashPad,
                               MaterialChannel.Leather, 0.8f);
                }
                // B-pillar behind the front seats (cars, vans, trucks); a bus has its own window pillars.
                if (s.Kind != CockpitKind.Bus)
                {
                    float bz = s.SeatZ - 0.42f;
                    if (bz > g.RearZ + 0.3f)
                        Capsule(m, f, x, g.BeltY, bz, k * (g.Wi - 0.04f), g.RoofIn - 0.03f, bz - 0.03f, pillarR, 8, 2, rail, MaterialChannel.Paint,
                                0.85f);
                    float cz = g.RearZ + 0.3f;
                    if (s.Kind != CockpitKind.Truck && cz < bz - 0.5f)
                        Capsule(m, f, x, g.BeltY, cz + 0.08f, k * (g.Wi - 0.06f), g.RoofIn - 0.03f, cz + 0.25f, pillarR * 1.3f, 8, 2, rail,
                                MaterialChannel.Paint, 0.85f);
                }
                // A side mirror outside each door, seen through the side glass.
                float mz = g.DashFrontZ - 0.12f, my = g.BeltY + 0.1f, mx = k * (s.HalfWidth + (g.Heavy ? 0.2f : 0.1f));
                Capsule(m, f, k * (s.HalfWidth - 0.02f), my - 0.02f, mz, mx - k * 0.06f, my, mz, 0.015f, 8, 2, Rubber, MaterialChannel.Rubber,
                        0.9f);
                RoundedBox(m, f, mx, my + (g.Heavy ? 0.1f : 0f), mz, g.Heavy ? 0.06f : 0.08f, g.Heavy ? 0.16f : 0.05f, 0.035f, 0.018f, 2, Rubber,
                           MaterialChannel.Rubber, 0.9f);
                Frame glass = Frame.Facing(mx, my + (g.Heavy ? 0.1f : 0f), mz - 0.036f, 0f, 0f, -1f);
                RoundedBox(m, glass, 0f, 0f, 0f, g.Heavy ? 0.05f : 0.07f, g.Heavy ? 0.15f : 0.04f, 0.004f, 0.004f, 1, Chrome,
                           MaterialChannel.Glass, 1f);
            }
        }

        private static void Mirror(in CockpitSpec s, in Layout g, MeshData m)
        {
            Frame f = Frame.Identity;
            // In the middle of the header; a driver who sits in the middle (the tempo) has it to the left instead.
            float mx = Math.Abs(s.SeatX) < 0.15f ? -0.45f * g.Wi : 0f;
            float mz = g.HeaderZ - 0.08f, my = g.HeaderY - (s.Kind == CockpitKind.Bus ? 0.04f : 0.09f);
            Capsule(m, f, mx, g.HeaderY + 0.01f, g.HeaderZ - 0.02f, mx, my + 0.02f, mz + 0.01f, 0.012f, 8, 2, Rubber, MaterialChannel.Rubber, 0.9f);
            RoundedBox(m, f, mx, my, mz, 0.13f, 0.035f, 0.02f, 0.018f, 2, Rubber, MaterialChannel.Rubber, 0.9f);
            Frame glass = Frame.Facing(mx, my, mz - 0.021f, 0f, 0f, -1f);
            RoundedBox(m, glass, 0f, 0f, 0f, 0.12f, 0.028f, 0.003f, 0.003f, 1, Chrome, MaterialChannel.Glass, 1f);
            if (s.Kind == CockpitKind.Bus) return;
            // A little mala hangs from the mirror: a string of beads and a tassel (Nepali taxis and micros).
            int beads = 7;
            for (int i = 0; i < beads; i++)
            {
                float t = (i + 1f) / beads;
                float bx = mx + 0.03f * (float)Math.Sin(t * Math.PI), by = my - 0.035f - 0.11f * t, bz = mz + 0.005f;
                Sphere(m, f, bx, by, bz, 0.0105f, 6, 4, i % 3 == 2 ? Bead : Mala, MaterialChannel.Paint, 0.95f);
            }
            Tube(m, f, mx + 0.012f, my - 0.15f, mz + 0.005f, mx + 0.012f, my - 0.2f, mz + 0.005f, 0.006f, 0.016f, 8, Mala, MaterialChannel.Fabric,
                 0.9f);
        }

        /// <summary>Things on the dash by the glass: the wiper arms resting at the screen's foot, and a string of marigolds
        /// (sayapatri) along it in cars and micros.</summary>
        private static void DashTop(in CockpitSpec s, in Layout g, MeshData m)
        {
            Frame f = Frame.Identity;
            float y = g.DashFrontY + 0.03f, z = g.DashFrontZ + 0.03f;
            // Two wiper arms lying along the bottom of the screen, pivots at the left of each half.
            for (int k = 0; k < 2; k++)
            {
                float px = (k == 0 ? -0.75f : 0.05f) * g.Wi, len = 0.62f * g.Wi;
                Capsule(m, f, px, y, z, px + len, y + 0.035f, z - 0.01f, g.Heavy ? 0.012f : 0.009f, 6, 2, Rubber, MaterialChannel.Rubber, 0.9f);
                Sphere(m, f, px, y, z, 0.022f, 8, 4, Rubber, MaterialChannel.Rubber, 0.85f);
            }
            if (g.Heavy) return;
            int flowers = g.CabForward ? 38 : 26;
            float left = -(g.Wi - 0.12f), right = g.Wi - 0.12f, fy = Math.Max(g.DashTopY, g.DashFrontY) + 0.012f;
            for (int i = 0; i < flowers; i++)
            {
                float t = (i + 0.5f) / flowers;
                float x = left + (right - left) * t;
                float fz = g.DashFrontZ - (g.CabForward ? 0.035f : 0.08f) - 0.02f * (float)Math.Sin(t * Math.PI * 3.0);
                Sphere(m, f, x, fy + 0.006f * (i % 2), fz, g.CabForward ? 0.0105f : 0.021f, 7, 4, i % 4 == 3 ? Bead : 0xEE7A16,
                       MaterialChannel.Foliage, 0.95f);
            }
        }

        private static void Seats(in CockpitSpec s, in Layout g, MeshData m)
        {
            if (s.Kind == CockpitKind.Bus) // the driver's seat; the saloon's rows come with the saloon
            {
                Seat(m, s.SeatX, s.SeatY, s.SeatZ, 0.25f, Dash, Dash, 0.6f);
                return;
            }
            uint fabric = s.Kind == CockpitKind.Truck ? 0x6E5A4A : Fabric;
            if (s.Kind == CockpitKind.Truck)
            {
                // One bench across the cab.
                Seat(m, 0f, s.SeatY, s.SeatZ, g.Wi - 0.1f, fabric, Cover, 0.62f);
                return;
            }
            Seat(m, s.SeatX, s.SeatY, s.SeatZ, 0.25f, fabric, fabric, 0.62f);
            Seat(m, -s.SeatX, s.SeatY, s.SeatZ, 0.25f, fabric, fabric, 0.62f);
            // Rear bench rows (cars and micros) while they fit in the body.
            for (int row = 1; row <= 3; row++)
            {
                float z = s.SeatZ - 0.85f * row;
                if (z - 0.3f < g.RearZ + 0.1f) break;
                Seat(m, 0f, s.SeatY, z, g.Wi - 0.1f, fabric, fabric, 0.55f, 1);
            }
        }

        /// <summary>A seat: a rounded cushion and a backrest (with a cover over its top when <paramref name="top"/>
        /// differs) for a sitter at the socket (x, y, z), <paramref name="halfWidth"/> wide.</summary>
        private static void Seat(MeshData m, float x, float y, float z, float halfWidth, uint fabric, uint top, float back, int k = 2)
        {
            Frame f = Frame.Identity;
            RoundedBox(m, f, x, y - 0.06f, z + 0.05f, halfWidth, 0.065f, 0.24f, 0.05f, k, fabric, MaterialChannel.Fabric, 0.85f);
            // Backrest leaning back 12°.
            Frame b = Frame.Tilted(x, y + 0.5f * back - 0.02f, z - 0.2f, -12f);
            RoundedBox(m, b, 0f, 0f, 0f, halfWidth, 0.5f * back, 0.06f, 0.05f, k, fabric, MaterialChannel.Fabric, 0.85f);
            if (top != fabric) RoundedBox(m, b, 0f, 0.5f * back - 0.09f, 0f, halfWidth + 0.01f, 0.1f, 0.07f, 0.05f, k, top, MaterialChannel.Fabric,
                0.95f);
        }

        private static void BusSaloon(in CockpitSpec s, in Layout g, MeshData m)
        {
            Frame f = Frame.Identity;
            uint rail = MeshColor.Lerp(MeshColor.FromHex(Dash), MeshColor.FromHex(s.BodyRgb), 0.2f) >> 8;
            float front = g.DashFrontZ - 0.9f, rear = s.TailZ + 0.3f;
            // Window pillars every 1.15 m down both sides, belt to roof rail.
            for (float z = front; z > rear; z -= 1.15f)
            {
                for (int k = -1; k <= 1; k += 2)
                    Capsule(m, f, k * (g.Wi - 0.01f), g.BeltY, z, k * (g.Wi - 0.04f), g.RoofIn - 0.03f, z, 0.05f, 8, 2, rail,
                            MaterialChannel.Paint, 0.85f);
            }
            // The floor, two grab rails along the ceiling with hanging straps.
            RoundedBox(m, f, 0f, g.FloorY - 0.02f, 0.5f * (rear + g.DashRearZ), g.Wi, 0.03f, 0.5f * (g.DashRearZ - rear), 0.02f, 2, Floor,
                       MaterialChannel.Rubber, 0.6f);
            for (int k = -1; k <= 1; k += 2)
            {
                float rx = k * 0.38f, ry = g.RoofIn - 0.2f;
                Capsule(m, f, rx, ry, rear + 0.3f, rx, ry, front + 0.4f, 0.018f, 8, 2, Chrome, MaterialChannel.Metal, 0.95f);
                for (float z = front; z > rear + 0.5f; z -= 1.2f)
                {
                    Capsule(m, f, rx, ry, z, rx, ry - 0.16f, z, 0.008f, 6, 2, Rubber, MaterialChannel.Rubber, 0.9f);
                    Torus(m, Frame.Facing(rx, ry - 0.2f, z, 1f, 0f, 0f), 0.04f, 0.009f, 10, 5, Rubber, MaterialChannel.Rubber, 0.9f);
                }
            }
            // Seat rows of 2 + 2 (VehicleSeats: 0.8 m pitch from 1.6 m behind the driver's front), red with white covers.
            float half = s.HalfWidth;
            for (int row = 0; ; row++)
            {
                float z = s.SeatZ + 0.3f - 1.6f - row * 0.8f;
                if (z - 0.35f < s.TailZ + 0.1f) break;
                Seat(m, -(half - 0.575f), s.SeatY, z, 0.4f, BusSeat, Cover, 0.62f, 1);
                Seat(m, half - 0.575f, s.SeatY, z, 0.4f, BusSeat, Cover, 0.62f, 1);
            }
            // The driver's partition: a low rounded rail behind the driver's seat.
            Capsule(m, f, s.SeatX - 0.4f, s.SeatY + 0.35f, s.SeatZ - 0.45f, g.Wi - 0.02f, s.SeatY + 0.35f, s.SeatZ - 0.45f, 0.025f, 8, 2, Chrome,
                    MaterialChannel.Metal, 0.95f);
        }

        private static void Decorations(in CockpitSpec s, in Layout g, MeshData m)
        {
            Frame f = Frame.Identity;
            float left = -(g.Wi - 0.08f), right = g.Wi - 0.08f;
            float y = g.HeaderY - 0.02f, z = g.HeaderZ - 0.06f;
            // A tinsel garland that sags in two swags along the top of the windscreen (red and gold, tassels at the knots).
            int beads = 56;
            for (int i = 0; i <= beads; i++)
            {
                float t = (float)i / beads;
                float x = left + (right - left) * t;
                float swag = (float)Math.Sin((t * 2f % 1f) * Math.PI);
                Sphere(m, f, x, y - 0.18f * swag, z, 0.011f, 6, 4, i % 3 == 0 ? TinselRed : Tinsel, MaterialChannel.Gilt, 0.95f);
            }
            for (int k = 0; k <= 2; k++)
            {
                float x = left + (right - left) * 0.5f * k;
                Tube(m, f, x, y - 0.01f, z, x, y - 0.16f, z, 0.008f, 0.024f, 8, TinselRed, MaterialChannel.Fabric, 0.9f);
            }
            // A string of prayer flags along the header (blue, white, red, green, yellow).
            int flags = 15;
            for (int i = 0; i < flags; i++)
            {
                float t = (i + 0.5f) / flags;
                float x = left + (right - left) * t;
                float sag = 0.03f * (float)Math.Sin(t * Math.PI);
                Frame flag = Frame.Facing(x, y + 0.075f - sag, z + 0.02f, 0f, 0f, -1f);
                RoundedBox(m, flag, 0f, -0.035f, 0f, 0.028f, 0.035f, 0.002f, 0f, 1, FlagColours[i % FlagColours.Length], MaterialChannel.Fabric,
                           1f);
            }
            Capsule(m, f, left, y + 0.08f, z + 0.02f, right, y + 0.08f, z + 0.02f, 0.003f, 5, 1, Cover, MaterialChannel.Fabric, 0.9f);
            // A small framed picture on the header above the driver (gilt frame, bright colours).
            Frame pic = Frame.Facing(s.SeatX * 0.6f, y + 0.02f, z + 0.03f, 0f, 0f, -1f);
            RoundedBox(m, pic, 0f, 0f, 0f, 0.07f, 0.09f, 0.008f, 0.008f, 2, GiltFrame, MaterialChannel.Gilt, 1f);
            RoundedBox(m, pic, 0f, 0f, -0.006f, 0.055f, 0.075f, 0.004f, 0.004f, 1, 0xE9762A, MaterialChannel.Paint, 1f);
            Sphere(m, pic, 0f, 0.015f, -0.012f, 0.026f, 10, 8, 0x3B7BD8, MaterialChannel.Paint, 1f);
            Sphere(m, pic, 0f, -0.04f, -0.012f, 0.018f, 8, 6, 0xF2C232, MaterialChannel.Paint, 1f);
        }

        private static void BackWall(in CockpitSpec s, in Layout g, MeshData m)
        {
            Frame f = Frame.Identity;
            float z = g.RearZ + 0.03f;
            uint panel = MeshColor.Lerp(MeshColor.FromHex(s.BodyRgb), MeshColor.FromHex(Dash), 0.45f) >> 8;
            float bottom = g.FloorY, top = g.RoofIn;
            // Below the rear window, above it, and either side: a wall with a rounded window opening.
            RoundedBox(m, f, 0f, 0.5f * (bottom + g.BeltY + 0.1f), z, g.Wi, 0.5f * (g.BeltY + 0.1f - bottom), 0.03f, 0.02f, 2, panel,
                       MaterialChannel.Paint, 0.8f);
            RoundedBox(m, f, 0f, 0.5f * (top + g.BeltY + 0.45f), z, g.Wi, 0.5f * (top - g.BeltY - 0.45f), 0.03f, 0.02f, 2, panel,
                       MaterialChannel.Paint, 0.85f);
            for (int k = -1; k <= 1; k += 2)
                RoundedBox(m, f, k * (0.5f * (g.Wi + 0.45f)), g.BeltY + 0.275f, z, 0.5f * (g.Wi - 0.45f), 0.18f, 0.03f, 0.02f, 2, panel,
                           MaterialChannel.Paint, 0.85f);
            Capsule(m, f, -0.45f, g.BeltY + 0.1f, z - 0.03f, 0.45f, g.BeltY + 0.1f, z - 0.03f, 0.02f, 8, 2, Rubber, MaterialChannel.Rubber, 0.8f);
            RoundedBox(m, f, 0f, 0.5f * (bottom + s.SeatY), 0.5f * (z + s.SeatZ), g.Wi, 0.5f * (s.SeatY - bottom), 0.5f * (s.SeatZ - z), 0.02f, 2,
                       Floor, MaterialChannel.Rubber, 0.6f);
        }

        // ----- Geometry -----------------------------------------------------------------------------------------------

        /// <summary>A rigid placement: origin and orthonormal axes (a proper rotation, so windings stay right).</summary>
        private readonly struct Frame
        {
            public readonly float Ox, Oy, Oz;
            public readonly float Xx, Xy, Xz, Yx, Yy, Yz, Zx, Zy, Zz;

            private Frame(float ox, float oy, float oz, float xx, float xy, float xz, float yx, float yy, float yz, float zx, float zy, float zz)
            {
                Ox = ox;
                Oy = oy;
                Oz = oz;
                Xx = xx;
                Xy = xy;
                Xz = xz;
                Yx = yx;
                Yy = yy;
                Yz = yz;
                Zx = zx;
                Zy = zy;
                Zz = zz;
            }

            public static Frame Identity
            {
                get { return new Frame(0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f); }
            }

            /// <summary>Origin at (x, y, z), local +Z along (dx, dy, dz), local +Y as close to world up as possible.</summary>
            public static Frame Facing(float x, float y, float z, float dx, float dy, float dz)
            {
                float l = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (l < 1e-6f) return new Frame(x, y, z, 1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f);
                dx /= l;
                dy /= l;
                dz /= l;
                // X = up × Z (component-wise cross), Y = Z × X.
                float ux = 0f, uy = 1f, uz = 0f;
                if (Math.Abs(dy) > 0.95f)
                {
                    uy = 0f;
                    uz = 1f;
                }
                float xx = uy * dz - uz * dy, xy = uz * dx - ux * dz, xz = ux * dy - uy * dx;
                float xl = (float)Math.Sqrt(xx * xx + xy * xy + xz * xz);
                xx /= xl;
                xy /= xl;
                xz /= xl;
                float yx = dy * xz - dz * xy, yy = dz * xx - dx * xz, yz = dx * xy - dy * xx;
                return new Frame(x, y, z, xx, xy, xz, yx, yy, yz, dx, dy, dz);
            }

            /// <summary>Origin at (x, y, z), turned about X by <paramref name="pitchDeg"/> (positive tips +Y towards +Z).</summary>
            public static Frame Tilted(float x, float y, float z, float pitchDeg)
            {
                double a = pitchDeg * Math.PI / 180.0;
                float c = (float)Math.Cos(a), sn = (float)Math.Sin(a);
                return new Frame(x, y, z, 1f, 0f, 0f, 0f, c, sn, 0f, -sn, c);
            }

            public void Point(float x, float y, float z, out float wx, out float wy, out float wz)
            {
                wx = Ox + Xx * x + Yx * y + Zx * z;
                wy = Oy + Xy * x + Yy * y + Zy * z;
                wz = Oz + Xz * x + Yz * y + Zz * z;
            }

            public void Dir(float x, float y, float z, out float wx, out float wy, out float wz)
            {
                wx = Xx * x + Yx * y + Zx * z;
                wy = Xy * x + Yy * y + Zy * z;
                wz = Xz * x + Yz * y + Zz * z;
            }
        }

        /// <summary>A profile point of a lathe: radius, distance along the axis and the normal (radial, axial); a point
        /// with <see cref="Break"/> starts a new strip (a crease: the same place, another normal).</summary>
        private struct ProfilePoint
        {
            public float R, A, Nr, Na;
            public bool Break;
        }

        private static ProfilePoint P(float r, float a, float nr, float na, bool brk = false)
        {
            float l = (float)Math.Sqrt(nr * nr + na * na);
            if (l < 1e-6f) l = 1f;
            return new ProfilePoint { R = r, A = a, Nr = nr / l, Na = na / l, Break = brk };
        }

        private static int Vertex(MeshData m, in Frame f, float x, float y, float z, float nx, float ny, float nz, uint rgb, MaterialChannel ch,
                                  float ao)
        {
            float wx, wy, wz, vx, vy, vz;
            f.Point(x, y, z, out wx, out wy, out wz);
            f.Dir(nx, ny, nz, out vx, out vy, out vz);
            // Cheap AO: undersides darker.
            float a = ao * (vy < 0f ? 1f + 0.3f * vy : 1f);
            return m.AddVertex(wx, wy, wz, vx, vy, vz, MeshColor.FromHex(rgb), (float)ch, Math.Max(0f, Math.Min(1f, a)));
        }

        /// <summary>A surface of revolution about the axis from (ax, ay, az) along (dx, dy, dz) in frame <paramref name="f"/>,
        /// through the profile points in order (each strip runs forward along the axis, so the outside faces out).</summary>
        private static void Lathe(MeshData m, in Frame f, float ax, float ay, float az, float dx, float dy, float dz, ProfilePoint[] profile,
                                  int sides, uint rgb, MaterialChannel ch, float ao)
        {
            float l = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            dx /= l;
            dy /= l;
            dz /= l;
            // u, v perpendicular with u × v = w (component-wise).
            float ux, uy, uz;
            if (Math.Abs(dy) < 0.9f)
            {
                ux = dz;
                uy = 0f;
                uz = -dx; // (0,1,0) × w
            }
            else
            {
                ux = 0f;
                uy = -dz;
                uz = dy; // (1,0,0) × w
            }
            float ul = (float)Math.Sqrt(ux * ux + uy * uy + uz * uz);
            ux /= ul;
            uy /= ul;
            uz /= ul;
            float vx = dy * uz - dz * uy, vy = dz * ux - dx * uz, vz = dx * uy - dy * ux;
            // Make u × v = w: v = w × u gives u × v = w.
            int ring = sides + 1;
            m.Reserve(profile.Length * ring, profile.Length * sides * 6);
            int prev = -1;
            for (int p = 0; p < profile.Length; p++)
            {
                ProfilePoint q = profile[p];
                int start = m.VertexCount;
                for (int i = 0; i <= sides; i++)
                {
                    double t = 2.0 * Math.PI * i / sides;
                    float c = (float)Math.Cos(t), sn = (float)Math.Sin(t);
                    float rx = c * ux + sn * vx, ry = c * uy + sn * vy, rz = c * uz + sn * vz;
                    Vertex(m, f, ax + dx * q.A + rx * q.R, ay + dy * q.A + ry * q.R, az + dz * q.A + rz * q.R, rx * q.Nr + dx * q.Na,
                           ry * q.Nr + dy * q.Na, rz * q.Nr + dz * q.Na, rgb, ch, ao);
                }
                if (prev >= 0 && !q.Break)
                {
                    for (int i = 0; i < sides; i++)
                    {
                        int a0 = prev + i, a1 = prev + i + 1, b0 = start + i, b1 = start + i + 1;
                        m.AddTriangle(a0, a1, b0);
                        m.AddTriangle(a1, b1, b0);
                    }
                }
                prev = start;
            }
        }

        /// <summary>A capsule (hemispherical ends) from a to b, radius r.</summary>
        private static void Capsule(MeshData m, in Frame f, float ax, float ay, float az, float bx, float by, float bz, float r, int sides,
                                    int capRings, uint rgb, MaterialChannel ch, float ao)
        {
            float dx = bx - ax, dy = by - ay, dz = bz - az;
            float len = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (len < 1e-5f)
            {
                Sphere(m, f, ax, ay, az, r, sides, Math.Max(3, capRings * 2), rgb, ch, ao);
                return;
            }
            capRings = Math.Max(1, capRings);
            var prof = new ProfilePoint[2 * (capRings + 1)];
            int k = 0;
            for (int i = 0; i <= capRings; i++)
            {
                double t = -Math.PI / 2 + Math.PI / 2 * i / capRings;
                prof[k++] = P(r * (float)Math.Cos(t), r * (float)Math.Sin(t), (float)Math.Cos(t), (float)Math.Sin(t));
            }
            for (int i = 0; i <= capRings; i++)
            {
                double t = Math.PI / 2 * i / capRings;
                prof[k++] = P(r * (float)Math.Cos(t), len + r * (float)Math.Sin(t), (float)Math.Cos(t), (float)Math.Sin(t));
            }
            Lathe(m, f, ax, ay, az, dx, dy, dz, prof, sides, rgb, ch, ao);
        }

        /// <summary>A tapered tube from a (radius r0) to b (radius r1) with rounded ends.</summary>
        private static void Tube(MeshData m, in Frame f, float ax, float ay, float az, float bx, float by, float bz, float r0, float r1, int sides,
                                 uint rgb, MaterialChannel ch, float ao)
        {
            float dx = bx - ax, dy = by - ay, dz = bz - az;
            float len = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (len < 1e-5f) return;
            float slope = (r0 - r1) / len;
            var prof = new[]
            {
                P(0f, -r0 * 0.6f, 0f, -1f), P(r0 * 0.7f, -r0 * 0.45f, 0.6f, -0.8f), P(r0, 0f, 1f, slope), P(r1, len, 1f, slope), P(r1 * 0.7f,
                  len + r1 * 0.45f, 0.6f, 0.8f), P(0f, len + r1 * 0.6f, 0f, 1f), };
            Lathe(m, f, ax, ay, az, dx, dy, dz, prof, sides, rgb, ch, ao);
        }

        private static void Sphere(MeshData m, in Frame f, float x, float y, float z, float r, int sides, int rings, uint rgb, MaterialChannel ch,
                                   float ao)
        {
            rings = Math.Max(2, rings);
            var prof = new ProfilePoint[rings + 1];
            for (int i = 0; i <= rings; i++)
            {
                double t = -Math.PI / 2 + Math.PI * i / rings;
                prof[i] = P(r * (float)Math.Cos(t), r * (float)Math.Sin(t), (float)Math.Cos(t), (float)Math.Sin(t));
            }
            Lathe(m, f, x, y, z, 0f, 1f, 0f, prof, sides, rgb, ch, ao);
        }

        /// <summary>A flat disc facing the frame's −Z (towards the viewer), thickness <paramref name="depth"/>, with a
        /// rounded rim.</summary>
        private static void Disc(MeshData m, in Frame f, float r, float depth, int sides, uint rgb, MaterialChannel ch, float ao)
        {
            float e = Math.Min(0.004f, 0.4f * depth);
            Lathe(m, f, 0f, 0f, 0f, 0f, 0f, 1f, new[]
            {
                P(0f, 0f, 0f, -1f), P(r - e, 0f, 0f, -1f), P(r, e, 1f, -0.3f), P(r, depth - e, 1f, 0.3f), P(r - e, depth, 0f, 1f), P(0f, depth, 0f,
                  1f), }, sides, rgb, ch, ao);
        }

        /// <summary>A torus in the frame's XY plane around its origin: main radius <paramref name="R"/>, tube radius
        /// <paramref name="r"/>.</summary>
        private static void Torus(MeshData m, in Frame f, float R, float r, int segU, int segV, uint rgb, MaterialChannel ch, float ao)
        {
            int start = m.VertexCount;
            m.Reserve((segU + 1) * (segV + 1), segU * segV * 6);
            for (int i = 0; i <= segU; i++)
            {
                double th = 2.0 * Math.PI * i / segU;
                float st = (float)Math.Sin(th), ct = (float)Math.Cos(th);
                for (int j = 0; j <= segV; j++)
                {
                    double ph = 2.0 * Math.PI * j / segV;
                    float cp = (float)Math.Cos(ph), sp = (float)Math.Sin(ph);
                    float nx = cp * st, ny = cp * ct, nz = sp;
                    Vertex(m, f, (R + r * cp) * st, (R + r * cp) * ct, r * sp, nx, ny, nz, rgb, ch, ao);
                }
            }
            int row = segV + 1;
            for (int i = 0; i < segU; i++)
            {
                for (int j = 0; j < segV; j++)
                {
                    int a = start + i * row + j, b = a + 1, c = a + row, d = c + 1;
                    m.AddTriangle(a, b, c);
                    m.AddTriangle(b, d, c);
                }
            }
        }

        /// <summary>A box with rounded edges and corners (radius <paramref name="r"/>, <paramref name="k"/> segments per
        /// quarter round): the grid of each face is pushed out from the inner box, so normals are smooth.</summary>
        private static void RoundedBox(MeshData m, in Frame f, float cx, float cy, float cz, float hx, float hy, float hz, float r, int k,
                                       uint rgb, MaterialChannel ch, float ao)
        {
            hx = Math.Max(1e-3f, hx);
            hy = Math.Max(1e-3f, hy);
            hz = Math.Max(1e-3f, hz);
            r = Math.Max(0f, Math.Min(r, Math.Min(hx, Math.Min(hy, hz))));
            k = r < 0.02f ? 1 : Math.Max(1, k); // a small round needs no more than one segment
            // Six faces: normal axis, then the two tangent axes with u × v = n.
            for (int face = 0; face < 6; face++)
            {
                int na, ua, va;
                float sign;
                switch (face)
                {
                    case 0: na = 0; ua = 1; va = 2; sign = 1f; break;
                    case 1: na = 0; ua = 2; va = 1; sign = -1f; break;
                    case 2: na = 1; ua = 2; va = 0; sign = 1f; break;
                    case 3: na = 1; ua = 0; va = 2; sign = -1f; break;
                    case 4: na = 2; ua = 0; va = 1; sign = 1f; break;
                    default: na = 2; ua = 1; va = 0; sign = -1f; break;
                }
                float hu = Half(ua, hx, hy, hz), hv = Half(va, hx, hy, hz), hn = Half(na, hx, hy, hz);
                int n = r < 1e-4f ? 2 : 2 * k + 2; // a sharp box: one quad per face
                int start = m.VertexCount;
                m.Reserve(n * n, (n - 1) * (n - 1) * 6);
                for (int i = 0; i < n; i++)
                {
                    float u = Grid(i, k, hu, r);
                    for (int j = 0; j < n; j++)
                    {
                        float v = Grid(j, k, hv, r);
                        float px = 0f, py = 0f, pz = 0f;
                        Set(ref px, ref py, ref pz, na, sign * hn);
                        Set(ref px, ref py, ref pz, ua, u);
                        Set(ref px, ref py, ref pz, va, v);
                        float ix = Clamp(px, hx - r), iy = Clamp(py, hy - r), iz = Clamp(pz, hz - r);
                        float dx = px - ix, dy = py - iy, dz = pz - iz;
                        float dl = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                        float nx, ny, nz;
                        if (dl < 1e-6f)
                        {
                            nx = na == 0 ? sign : 0f;
                            ny = na == 1 ? sign : 0f;
                            nz = na == 2 ? sign : 0f;
                        }
                        else
                        {
                            nx = dx / dl;
                            ny = dy / dl;
                            nz = dz / dl;
                        }
                        Vertex(m, f, cx + ix + nx * r, cy + iy + ny * r, cz + iz + nz * r, nx, ny, nz, rgb, ch, ao);
                    }
                }
                // (i, j) → (i + 1, j) runs along u, (i, j + 1) along v; u × v = n: triangle (a, b, c) faces out.
                for (int i = 0; i < n - 1; i++)
                {
                    for (int j = 0; j < n - 1; j++)
                    {
                        int a = start + i * n + j, b = start + (i + 1) * n + j, c = start + (i + 1) * n + j + 1, d = start + i * n + j + 1;
                        m.AddTriangle(a, b, c);
                        m.AddTriangle(a, c, d);
                    }
                }
            }
        }

        private static float Half(int axis, float hx, float hy, float hz)
        {
            return axis == 0 ? hx : axis == 1 ? hy : hz;
        }

        private static void Set(ref float x, ref float y, ref float z, int axis, float value)
        {
            if (axis == 0) x = value;
            else if (axis == 1) y = value;
            else z = value;
        }

        private static float Clamp(float v, float h)
        {
            return v < -h ? -h : v > h ? h : v;
        }

        /// <summary>Grid coordinate <paramref name="i"/> of 2k + 2 across [−h, h]: k + 1 points over the rounded strip on
        /// each side (denser towards the edge), one flat span between.</summary>
        private static float Grid(int i, int k, float h, float r)
        {
            if (r < 1e-4f) return i == 0 ? -h : h;
            float a = h - r;
            if (i <= k)
            {
                double t = (Math.PI / 2) * (k - i) / k; // 90° at the edge
                return -a - r * (float)Math.Sin(t);
            }
            double s = (Math.PI / 2) * (i - k - 1) / k;
            return a + r * (float)Math.Sin(s);
        }
    }
}
