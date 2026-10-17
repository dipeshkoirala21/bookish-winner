using System;
using Ghumante.Core.Driving;
using Ghumante.Core.Save;

namespace Ghumante.Core.Characters
{
    /// <summary>
    /// The numbers of one chase-camera rig (docs/research/w2/ref_camera.md §5, which supersedes W2_DESIGN 6.4 after owner
    /// playtest #5). The boom hangs from a pivot over the <b>middle of the body</b> (the ground point moved forward by the
    /// body-centre offset), its length and elevation give where the camera sits, and the view pins the body's ground point
    /// at <see cref="FootRestNdc"/> on screen (lower with speed, so more road ahead shows).
    /// </summary>
    public struct RigParams
    {
        /// <summary>Distance from the pivot to the camera, metres.</summary>
        public float DistanceM;

        /// <summary>Elevation of the camera above the pivot, degrees.</summary>
        public float PitchDeg;

        /// <summary>Pivot height above the body's ground point, metres.</summary>
        public float PivotM;

        /// <summary>Look-ahead: speed × this many seconds, up to <see cref="LookAheadMaxM"/> (moves the body down the screen
        /// from <see cref="FootRestNdc"/> towards <see cref="FootFastNdc"/>).</summary>
        public float LookAheadS, LookAheadMaxM;

        /// <summary>Minimum horizontal field of view, degrees: binds on tall (portrait) screens.</summary>
        public float MinHFovDeg;

        /// <summary>Minimum vertical field of view, degrees: binds on wide (landscape) screens (Hor+: a wider phone sees
        /// more to the sides, not less above and below). 0: none.</summary>
        public float MinVFovDeg;

        /// <summary>Yaw follow smoothing time, seconds (critically damped: lags a steady turn by rate × this).</summary>
        public float FollowS;

        /// <summary>Speed FOV kick, degrees, reached at <see cref="KickFullMps"/> (above <see cref="KickFromMps"/>).</summary>
        public float FovKickDeg, KickFromMps, KickFullMps;

        /// <summary>Collision minimum distance (walk 2.5 m, vehicles 3–6 m).</summary>
        public float MinDistanceM;

        /// <summary>Sideways offset of the pivot, metres, right positive (0 except the over-the-shoulder view).</summary>
        public float ShoulderM;

        /// <summary>The camera looks through the pivot instead of pinning the player's ground point on screen (the
        /// over-the-shoulder view, whose pivot is the shoulder).</summary>
        public bool AimAtPivot;

        /// <summary>Screen height (NDC: −1 bottom, +1 top) of the body's ground point at rest and at full look-ahead.</summary>
        public float FootRestNdc, FootFastNdc;

        /// <summary>Smoothing of the followed point: across the ground (0.07–0.14 s, a slight trail at speed) and up and
        /// down (0.2–0.4 s with the road's slope fed forward, so kerbs, seams and bumps never shake the view while hills
        /// are followed without lag).</summary>
        public float PositionS, HeightS;

        /// <summary>The boom grows by this much at <see cref="PullFullMps"/> (smoothed speed): a little sense of speed.</summary>
        public float PullBackM, PullFullMps;

        /// <summary>Below <see cref="FollowFromMps"/> the camera does not swing round with the body (parking, turning on
        /// the spot); from <see cref="FollowFullMps"/> it follows fully.</summary>
        public float FollowFromMps, FollowFullMps;

        /// <summary>The body's middle ahead of its ground point when the target does not say (vehicles stand on the rear
        /// axle), metres.</summary>
        public float BodyAheadM;
    }

    /// <summary>
    /// The chase rig per class and orientation (docs/research/w2/ref_camera.md §5): the camera sits low and close behind,
    /// about a metre over the roof of a car (2.4 m up, 6.5 m behind its tail in landscape), looking nearly level so the
    /// street runs to a horizon at about two thirds of the screen; two-wheelers 2.5 m up and 6.7 m behind the rear wheel;
    /// buses and trucks 4.4 m up and 11.5 m behind. Landscape keeps a vertical FOV of 46–48° (Hor+), portrait a horizontal
    /// FOV of 55–62° (the vertical then opens to about 100°), so portrait sits higher and steeper and shows more road.
    /// Passengers orbit at 1.2× the driver rig of their vehicle. Engine-free; the Unity rig reads it through
    /// <see cref="ChaseCamera"/>.
    /// </summary>
    public static class CameraRigTable
    {
        public const float PassengerScale = 1.2f;

        /// <summary>Passenger ride-along auto-yaw towards points of interest within 300 m, degrees per second.</summary>
        public const float PassengerAutoYawDegPerS = 6f;

        public static RigParams For(RigClass rig, bool portrait)
        {
            if (rig == RigClass.Passenger) return For(rig, portrait, RigClass.Car);
            RigParams r;
            switch (rig)
            {
                case RigClass.Walk:
                    r = portrait ? R(5.6f, 27.5f, 1.0f, -0.44f, 55f, 0f) : R(6.0f, 12.5f, 1.0f, -0.55f, 55f, 48f);
                    Look(ref r, portrait ? 0.6f : 0.3f, portrait ? 4.5f : 2.5f, -0.04f);
                    Smooth(ref r, 0.9f, 0.08f, 0.20f, 0f, 0f, 0f, 2.5f);
                    return r;
                case RigClass.Bicycle:
                    r = portrait ? R(6.6f, 28f, 1.0f, -0.40f, 62f, 0f) : R(7.2f, 10.5f, 1.0f, -0.46f, 62f, 46f);
                    Look(ref r, portrait ? 0.8f : 0.4f, portrait ? 10f : 6f, -0.06f);
                    Smooth(ref r, 0.32f, 0.07f, 0.24f, 0.55f, 0.6f, 8f, 3f);
                    Kick(ref r, 3f, 3f, 8.3f);
                    return r;
                case RigClass.TwoWheeler:
                    r = portrait ? R(7.0f, 28.5f, 1.0f, -0.41f, 62f, 0f) : R(7.8f, 11f, 1.0f, -0.46f, 62f, 46f);
                    Look(ref r, portrait ? 0.9f : 0.45f, portrait ? 16f : 9f, -0.08f);
                    Smooth(ref r, 0.30f, 0.07f, 0.25f, 0.6f, 1.0f, 20f, 3f);
                    Kick(ref r, 6f, 16.7f, 25f);
                    return r;
                case RigClass.Car:
                    r = portrait ? R(8.1f, 25.5f, 1.0f, -0.33f, 62f, 0f) : R(8.5f, 9f, 1.0f, -0.37f, 62f, 46f);
                    Look(ref r, portrait ? 1.0f : 0.5f, portrait ? 20f : 12f, -0.08f);
                    Smooth(ref r, 0.40f, 0.09f, 0.28f, 1.3f, 1.2f, 22f, 3.5f);
                    Kick(ref r, 4f, 8f, 25f);
                    return r;
                case RigClass.Van:
                    r = portrait ? R(10.0f, 26.5f, 1.1f, -0.34f, 62f, 0f) : R(10.3f, 10f, 1.1f, -0.38f, 62f, 46f);
                    Look(ref r, portrait ? 1.0f : 0.5f, portrait ? 20f : 12f, -0.08f);
                    Smooth(ref r, 0.45f, 0.10f, 0.30f, 1.5f, 1.2f, 20f, 4f);
                    Kick(ref r, 3f, 8f, 22f);
                    return r;
                case RigClass.Bus:
                    r = portrait ? R(15.9f, 22f, 1.8f, -0.29f, 64f, 0f) : R(16.9f, 9f, 1.8f, -0.32f, 64f, 48f);
                    Look(ref r, portrait ? 1.2f : 0.6f, portrait ? 28f : 18f, -0.07f);
                    Smooth(ref r, 0.60f, 0.14f, 0.38f, 2.6f, 1.5f, 16f, 6f);
                    Kick(ref r, 2f, 5f, 17f);
                    return r;
                case RigClass.Truck:
                    r = portrait ? R(14.8f, 24.5f, 1.8f, -0.33f, 64f, 0f) : R(15.6f, 10f, 1.8f, -0.37f, 64f, 48f);
                    Look(ref r, portrait ? 1.2f : 0.6f, portrait ? 26f : 16f, -0.07f);
                    Smooth(ref r, 0.55f, 0.13f, 0.36f, 2.6f, 1.5f, 15f, 6f);
                    Kick(ref r, 2f, 5f, 15f);
                    return r;
                case RigClass.Tractor:
                    r = portrait ? R(9.4f, 27.5f, 1.2f, -0.36f, 62f, 0f) : R(10.0f, 10.5f, 1.2f, -0.42f, 62f, 48f);
                    Look(ref r, portrait ? 0.6f : 0.3f, portrait ? 10f : 6f, -0.05f);
                    Smooth(ref r, 0.50f, 0.12f, 0.32f, 1.0f, 0.5f, 8f, 4f);
                    return r;
                default:
                    return Passenger(For(RigClass.Car, portrait));
            }
        }

        /// <summary>
        /// As <see cref="For(RigClass, bool)"/>, with the class of the vehicle ridden along: a passenger orbits at 1.2×
        /// the driver rig of <paramref name="passengerOf"/> (a bus ride frames like a bus, not a car). Other rigs ignore it.
        /// </summary>
        public static RigParams For(RigClass rig, bool portrait, RigClass passengerOf)
        {
            if (rig != RigClass.Passenger) return For(rig, portrait);
            if (passengerOf == RigClass.Walk || passengerOf == RigClass.Passenger) passengerOf = RigClass.Car;
            return Passenger(For(passengerOf, portrait));
        }

        /// <summary>The ride-along rig for a driver rig: 1.2× the distance, no look-ahead, no FOV kick, no pull-back.</summary>
        public static RigParams Passenger(in RigParams driver)
        {
            RigParams p = driver;
            p.DistanceM *= PassengerScale;
            p.LookAheadS = 0f;
            p.LookAheadMaxM = 0f;
            p.FovKickDeg = 0f;
            p.PullBackM = 0f;
            p.FollowS = Math.Max(driver.FollowS, 0.6f);
            return p;
        }

        /// <summary>
        /// The chase rig of an aircraft <paramref name="lengthM"/> long (the flight package's camera; ref_camera.md §5.9):
        /// about 7 m + 1.15 × the length behind, low over the tail, a wide 56° vertical FOV in landscape, the yaw following the
        /// heading in 0.55 s. <see cref="ChaseCamera"/> keeps the horizon level apart from <see cref="AircraftBankShare"/> of
        /// the bank and tilts the boom with half the flight-path angle.
        /// </summary>
        public static RigParams ForAircraft(float lengthM, bool portrait)
        {
            float l = float.IsNaN(lengthM) ? 8f : Math.Max(3f, Math.Min(80f, lengthM));
            float d = 7f + 1.15f * l;
            float pivot = Math.Max(1.2f, 0.18f * l);
            RigParams r = portrait ? R(d * 1.08f, 16f, pivot, -0.26f, 64f, 0f) : R(d, 6.5f, pivot, -0.28f, 64f, 56f);
            Look(ref r, 0f, 0f, 0f);
            Smooth(ref r, 0.55f, 0.12f, 0.25f, 0f, 0.08f * l, 80f, 0f);
            Kick(ref r, 4f, 40f, 80f);
            r.MinDistanceM = Math.Min(d, 4f + 0.3f * l);
            r.FollowFromMps = 0f;
            r.FollowFullMps = 0f;
            return r;
        }

        /// <summary>Share of an aircraft's bank the chase view rolls with (0 with reduced motion).</summary>
        public const float AircraftBankShare = 0.2f;

        /// <summary>Share of an aircraft's flight-path angle the boom tilts by (looks along a climb or a descent).</summary>
        public const float AircraftPathShare = 0.5f;

        /// <summary>The FOV kick at a speed (none below the threshold, full at the top).</summary>
        public static float FovKick(in RigParams r, float speedMps)
        {
            if (r.FovKickDeg <= 0f || r.KickFullMps <= r.KickFromMps) return 0f;
            float t = (Math.Abs(speedMps) - r.KickFromMps) / (r.KickFullMps - r.KickFromMps);
            return r.FovKickDeg * CharMath.Clamp01(t);
        }

        /// <summary>The boom's speed pull-back at a (smoothed) speed, metres (eased in).</summary>
        public static float PullBack(in RigParams r, float speedMps)
        {
            if (r.PullBackM <= 0f || !(r.PullFullMps > 0f)) return 0f;
            return r.PullBackM * CharMath.SmoothStep(Math.Abs(speedMps) / r.PullFullMps);
        }

        /// <summary>
        /// The vertical field of view of a rig at <paramref name="aspect"/> (width / height) plus a speed
        /// <paramref name="kickDeg"/>: whichever is wider of the minimum horizontal FOV (as vertical) and the minimum
        /// vertical FOV (landscape only), clamped to 100° (CameraFov).
        /// </summary>
        public static float VerticalFovDeg(in RigParams r, float aspect, float kickDeg)
        {
            if (!(aspect > 0f) || float.IsInfinity(aspect)) aspect = 1f;
            float h = CharMath.Clamp(r.MinHFovDeg + kickDeg, 1f, 179f);
            double v = 2.0 * Math.Atan(Math.Tan(h * Math.PI / 360.0) / aspect) * 180.0 / Math.PI;
            if (aspect >= 1f && r.MinVFovDeg > 0f) v = Math.Max(v, r.MinVFovDeg + 0.7f * kickDeg);
            return (float)Math.Min(100.0, Math.Max(1.0, v));
        }

        /// <summary>Where the body's ground point stands on screen (NDC y) at <paramref name="speedMps"/> (smoothed).</summary>
        public static float FootNdc(in RigParams r, float speedMps)
        {
            float t = 0f;
            if (r.LookAheadMaxM > 0f && speedMps > 0f) t = Math.Min(1f, speedMps * r.LookAheadS / r.LookAheadMaxM);
            return r.FootRestNdc + (r.FootFastNdc - r.FootRestNdc) * t;
        }

        private static RigParams R(float d, float pitch, float pivot, float foot, float minH, float minV)
        {
            return new RigParams
            {
                DistanceM = d, PitchDeg = pitch, PivotM = pivot, FootRestNdc = foot, FootFastNdc = foot, MinHFovDeg = minH, MinVFovDeg = minV,
                MinDistanceM = Math.Min(d, 3.5f), KickFullMps = 1f,
            };
        }

        private static void Look(ref RigParams r, float s, float maxM, float footShift)
        {
            r.LookAheadS = s;
            r.LookAheadMaxM = maxM;
            r.FootFastNdc = r.FootRestNdc + footShift;
        }

        private static void Smooth(ref RigParams r, float follow, float position, float height, float bodyAhead, float pullBack, float pullFull,
                                   float minDistance)
        {
            r.FollowS = follow;
            r.PositionS = position;
            r.HeightS = height;
            r.BodyAheadM = bodyAhead;
            r.PullBackM = pullBack;
            r.PullFullMps = pullFull;
            r.MinDistanceM = Math.Min(r.DistanceM, minDistance);
            r.FollowFromMps = 0.8f;
            r.FollowFullMps = 3f;
        }

        private static void Kick(ref RigParams r, float deg, float from, float full)
        {
            r.FovKickDeg = deg;
            r.KickFromMps = from;
            r.KickFullMps = full;
        }
    }

    /// <summary>
    /// The camera angles the player cycles through with C, the gamepad's right-stick press or the HUD camera button
    /// (docs/W2_DETAIL_CONTRACT.md, owner feedback: "multiple camera angles for bike, cycle, car, bus"). Each vehicle
    /// class offers its own list (<see cref="CameraViews"/>) and remembers its choice (<see cref="CameraViewMemory"/>).
    /// Stored by name in the save, so values may be appended but never renumbered.
    /// </summary>
    public enum CameraView : byte
    {
        /// <summary>The class's chase rig (<see cref="CameraRigTable.For(RigClass, bool)"/>).</summary>
        Near = 0,

        /// <summary>Pulled back and a little higher: more of the street.</summary>
        Far = 1,

        /// <summary>On foot: close behind the right shoulder, looking where the walker looks.</summary>
        OverShoulder = 2,

        /// <summary>Two-wheelers: low behind the rear wheel, nearly level with the road.</summary>
        LowCinematic = 3,

        /// <summary>Two-wheelers: first person over the handlebar.</summary>
        Handlebar = 4,

        /// <summary>Cars, taxis, SUVs and micros: on the bonnet.</summary>
        Hood = 5,

        /// <summary>Cars, taxis, SUVs and micros: from the driver's eyes.</summary>
        Interior = 6,

        /// <summary>Bus, truck and tractor: the class's chase rig, which already sits high behind the body.</summary>
        HighChase = 7,

        /// <summary>Bus, truck and tractor: from the driver's seat.</summary>
        DriverSeat = 8,

        /// <summary>Riding along: from the passenger's seat, leaning out of its window to look ahead along the flank
        /// (<see cref="CameraViewKind.Window"/>; the save name stays "passenger_seat").</summary>
        PassengerSeat = 9,
    }

    /// <summary>How a <see cref="CameraView"/> places the camera.</summary>
    public enum CameraViewKind : byte
    {
        /// <summary>A boom behind a pivot over the player, with collision (<see cref="ChaseBoom"/>).</summary>
        Chase = 0,

        /// <summary>The rider's eyes: the head bone plus <see cref="CameraViews.EyeUpM"/> up and
        /// <see cref="CameraViews.EyeForwardM"/> forward in the body frame, so the own head stays behind the camera.</summary>
        Eye = 1,

        /// <summary>A fixed point on the vehicle body (<see cref="CameraViews.HoodEye"/>).</summary>
        Hood = 2,

        /// <summary>Riding along: at the passenger's eye height just outside the window beside their seat
        /// (<see cref="CameraViews.WindowX"/>), looking ahead along the flank. The ridden vehicle is drawn by traffic,
        /// whose body cannot be hidden from inside (a culled shell shows floating passengers, the outline pass a dark
        /// hull), so the camera leans out of the open window instead.</summary>
        Window = 3,
    }

    /// <summary>The numbers of one <see cref="CameraView"/>: chase views modify the class rig, mounted views (eye, hood,
    /// window) carry their own field of view, downward tilt and how much of the body's roll or lean they follow.</summary>
    public struct CameraViewSpec
    {
        public CameraView View;
        public CameraViewKind Kind;

        /// <summary>Chase: the class distance times this ...</summary>
        public float DistanceScale;

        /// <summary>... or, when positive, this many metres whatever the class.</summary>
        public float DistanceM;

        /// <summary>Chase: added to the class pitch, degrees.</summary>
        public float PitchAddDeg;

        /// <summary>Chase: pivot height = class pivot × <see cref="PivotScale"/> + <see cref="PivotAddM"/>.</summary>
        public float PivotScale, PivotAddM;

        /// <summary>Chase: sideways pivot offset (right positive), metres.</summary>
        public float ShoulderM;

        /// <summary>Chase: look-ahead time and reach times this.</summary>
        public float LookAheadScale;

        /// <summary>Chase: when positive, the collision minimum distance (the class's otherwise).</summary>
        public float MinDistanceM;

        /// <summary>Chase: look through the pivot instead of pinning the ground point on screen.</summary>
        public bool AimAtPivot;

        /// <summary>Chase: added to the class's minimum horizontal FOV, degrees.</summary>
        public float FovAddDeg;

        /// <summary>Mounted: minimum horizontal FOV, degrees (CameraFov rules: the vertical FOV follows the aspect,
        /// clamped at 100°).</summary>
        public float MinHFovDeg;

        /// <summary>Mounted: the view tilts down this much from the body's forward, degrees (dashboard, handlebar).</summary>
        public float LookDownDeg;

        /// <summary>Mounted: share of the body's roll (or a two-wheeler's lean) the view follows (0 stays level).</summary>
        public float RollShare;

        /// <summary>Near clip plane, metres (0.4 behind the player, 0.1 on board).</summary>
        public float NearClipM;

        /// <summary>True for the eye, hood and window views (no boom, no reverse framing).</summary>
        public bool Mounted
        {
            get { return Kind != CameraViewKind.Chase; }
        }
    }

    /// <summary>
    /// The camera angles per rig class (W2 detail pass): walking near, far and over the shoulder; bicycle, scooter and
    /// motorbike near chase, far chase, low cinematic and handlebar first person; car, taxi, SUV and micro near, far,
    /// bonnet and interior; bus, truck and tractor high chase, far and driver's seat; riding along near, far and the
    /// passenger's seat. <see cref="Chase"/> turns a chase view into rig numbers, <see cref="Spec"/> describes every view.
    /// Engine-free and allocation-free.
    /// </summary>
    public static class CameraViews
    {
        public const float ChaseNearClipM = 0.4f;
        public const float MountedNearClipM = 0.1f;

        /// <summary>The eye sits this far above the head bone (the base of the 0.40 m skull) ...</summary>
        public const float EyeUpM = 0.22f;

        /// <summary>... and this far in front of it, 0.13 m in front of the face, so the own head stays behind the
        /// near plane.</summary>
        public const float EyeForwardM = 0.32f;

        /// <summary>A change of view blends over this long (smoothstep).</summary>
        public const float BlendSeconds = 0.5f;

        /// <summary>The window view sits this far outside the body's side ...</summary>
        public const float WindowOutM = 0.22f;

        /// <summary>... and turns this far in towards the body, degrees, so the flank runs along the edge of the frame.</summary>
        public const float WindowInYawDeg = 4f;

        /// <summary>Mounted views tilt down this share of their <see cref="CameraViewSpec.LookDownDeg"/> in portrait (the
        /// tall view already reaches the dashboard and the handlebar; looking level keeps the road in its middle).</summary>
        public const float PortraitLookDownShare = 0.3f;

        /// <summary>Over-the-shoulder distance in portrait relative to landscape (the tall screen is narrower).</summary>
        public const float PortraitAbsoluteScale = 0.92f;

        public const int RigCount = 9;

        private static readonly CameraView[] WalkViews = { CameraView.Near, CameraView.Far, CameraView.OverShoulder };

        private static readonly CameraView[] TwoWheelViews =
        {
            CameraView.Near, CameraView.Far, CameraView.LowCinematic, CameraView.Handlebar,
        };

        private static readonly CameraView[] CarViews = { CameraView.Near, CameraView.Far, CameraView.Hood, CameraView.Interior };
        private static readonly CameraView[] HeavyViews = { CameraView.HighChase, CameraView.Far, CameraView.DriverSeat };
        private static readonly CameraView[] PassengerViews = { CameraView.Near, CameraView.Far, CameraView.PassengerSeat };

        private static readonly string[] ViewNames =
        {
            "near", "far", "over_shoulder", "low_cinematic", "handlebar", "hood", "interior", "high_chase", "driver_seat",
            "passenger_seat",
        };

        private static readonly string[] ViewKeys =
        {
            "hud.camera.near", "hud.camera.far", "hud.camera.over_shoulder", "hud.camera.low_cinematic", "hud.camera.handlebar",
            "hud.camera.hood", "hud.camera.interior", "hud.camera.high_chase", "hud.camera.driver_seat", "hud.camera.passenger_seat",
        };

        private static readonly string[] RigNames =
        {
            "walk", "bicycle", "two_wheeler", "car", "van", "bus", "truck", "tractor", "passenger",
        };

        private static CameraView[] ViewsOf(RigClass rig)
        {
            switch (rig)
            {
                case RigClass.Walk: return WalkViews;
                case RigClass.Bicycle:
                case RigClass.TwoWheeler: return TwoWheelViews;
                case RigClass.Car:
                case RigClass.Van: return CarViews;
                case RigClass.Bus:
                case RigClass.Truck:
                case RigClass.Tractor: return HeavyViews;
                default: return PassengerViews;
            }
        }

        /// <summary>How many views <paramref name="rig"/> offers (3 or 4).</summary>
        public static int Count(RigClass rig)
        {
            return ViewsOf(rig).Length;
        }

        /// <summary>The <paramref name="index"/>-th view of <paramref name="rig"/> (wrapping in both directions).</summary>
        public static CameraView At(RigClass rig, int index)
        {
            CameraView[] v = ViewsOf(rig);
            int i = index % v.Length;
            if (i < 0) i += v.Length;
            return v[i];
        }

        /// <summary>Position of <paramref name="view"/> in the cycle of <paramref name="rig"/>, or −1.</summary>
        public static int IndexOf(RigClass rig, CameraView view)
        {
            CameraView[] v = ViewsOf(rig);
            for (int i = 0; i < v.Length; i++)
                if (v[i] == view) return i;
            return -1;
        }

        /// <summary>The first view of a class: its chase rig.</summary>
        public static CameraView Default(RigClass rig)
        {
            return ViewsOf(rig)[0];
        }

        public static bool Offers(RigClass rig, CameraView view)
        {
            return IndexOf(rig, view) >= 0;
        }

        /// <summary><paramref name="view"/> when the class offers it, else the class default.</summary>
        public static CameraView Valid(RigClass rig, CameraView view)
        {
            return Offers(rig, view) ? view : Default(rig);
        }

        /// <summary>The view after <paramref name="view"/> in the class's cycle (the default after the last one, or after
        /// a view the class does not offer).</summary>
        public static CameraView Next(RigClass rig, CameraView view)
        {
            int i = IndexOf(rig, view);
            return i < 0 ? Default(rig) : At(rig, i + 1);
        }

        /// <summary>The numbers of a view.</summary>
        public static CameraViewSpec Spec(CameraView view)
        {
            var s = new CameraViewSpec
            {
                View = view, Kind = CameraViewKind.Chase, DistanceScale = 1f, PivotScale = 1f, LookAheadScale = 1f, NearClipM = ChaseNearClipM,
                RollShare = 1f,
            };
            switch (view)
            {
                case CameraView.Far:
                    s.DistanceScale = 1.45f;
                    s.PitchAddDeg = 5f;
                    s.PivotAddM = 0.2f;
                    s.LookAheadScale = 1.25f;
                    break;
                case CameraView.OverShoulder:
                    s.DistanceM = 3.2f;
                    s.PitchAddDeg = -4f;
                    s.PivotAddM = 0.55f;
                    s.ShoulderM = 0.55f;
                    s.LookAheadScale = 0.5f;
                    s.MinDistanceM = 1.0f;
                    s.AimAtPivot = true;
                    s.FovAddDeg = -3f;
                    break;
                case CameraView.LowCinematic:
                    s.DistanceScale = 0.62f;
                    s.PitchAddDeg = -9f;
                    s.PivotScale = 0.6f;
                    s.LookAheadScale = 1.3f;
                    s.MinDistanceM = 2.0f;
                    s.FovAddDeg = 4f;
                    break;
                case CameraView.Handlebar:
                    Mount(ref s, CameraViewKind.Eye, 72f, 9f, 0.5f);
                    break;
                case CameraView.Hood:
                    Mount(ref s, CameraViewKind.Hood, 66f, 3f, 1f);
                    break;
                case CameraView.Interior:
                    Mount(ref s, CameraViewKind.Eye, 70f, 6f, 1f);
                    break;
                case CameraView.DriverSeat:
                    Mount(ref s, CameraViewKind.Eye, 70f, 5f, 1f);
                    break;
                case CameraView.PassengerSeat:
                    Mount(ref s, CameraViewKind.Window, 70f, 2f, 0.6f);
                    break;
            }
            return s;
        }

        private static void Mount(ref CameraViewSpec s, CameraViewKind kind, float minHFov, float lookDown, float roll)
        {
            s.Kind = kind;
            s.MinHFovDeg = minHFov;
            s.LookDownDeg = lookDown;
            s.RollShare = roll;
            s.NearClipM = MountedNearClipM;
            s.LookAheadScale = 0f;
        }

        /// <summary>True for the eye, hood and window views.</summary>
        public static bool IsMounted(CameraView view)
        {
            return Spec(view).Mounted;
        }

        /// <summary>
        /// The chase rig of <paramref name="rig"/> seen through <paramref name="view"/>: the class rig of
        /// <see cref="CameraRigTable"/> modified by the view (a mounted view returns the class rig unchanged, which the
        /// camera uses only to blend). The collision minimum never exceeds the view's own distance.
        /// </summary>
        public static RigParams Chase(RigClass rig, CameraView view, bool portrait, RigClass passengerOf)
        {
            RigParams r = CameraRigTable.For(rig, portrait, passengerOf);
            CameraViewSpec s = Spec(view);
            if (s.Kind != CameraViewKind.Chase) return r;
            r.DistanceM = s.DistanceM > 0f ? s.DistanceM * (portrait ? PortraitAbsoluteScale : 1f) : r.DistanceM * s.DistanceScale;
            r.PitchDeg += s.PitchAddDeg;
            r.PivotM = r.PivotM * s.PivotScale + s.PivotAddM;
            r.LookAheadS *= s.LookAheadScale;
            r.LookAheadMaxM *= s.LookAheadScale;
            r.MinHFovDeg += s.FovAddDeg;
            if (s.MinDistanceM > 0f) r.MinDistanceM = Math.Min(r.MinDistanceM, s.MinDistanceM);
            r.MinDistanceM = Math.Min(r.MinDistanceM, r.DistanceM);
            r.ShoulderM = s.ShoulderM;
            r.AimAtPivot = s.AimAtPivot;
            return r;
        }

        /// <summary>The bonnet camera in the vehicle frame (origin on the ground under the rear axle, +Z forward): just
        /// behind the nose, a little above the bonnet line of a body <paramref name="heightM"/> tall.</summary>
        public static void HoodEye(float wheelbaseM, float frontM, float heightM, out float y, out float z)
        {
            z = wheelbaseM + Math.Max(0f, frontM) * 0.35f;
            y = 0.62f * Math.Max(0.8f, heightM) + 0.22f;
        }

        /// <summary>The window view's sideways place in the vehicle frame (x, metres, right positive) for a head at
        /// <paramref name="headX"/> in a body <paramref name="halfWidthM"/> wide on each side: out through the nearer side,
        /// the kerb (left) side for a seat in the middle. <paramref name="side"/> is +1 right, −1 left.</summary>
        public static float WindowX(float headX, float halfWidthM, out float side)
        {
            side = headX > 0.1f ? 1f : -1f;
            if (float.IsNaN(halfWidthM)) halfWidthM = 0f;
            return side * (Math.Max(0.3f, halfWidthM) + WindowOutM);
        }

        /// <summary>A mounted view's downward tilt at an orientation blend (0 landscape, 1 portrait), degrees.</summary>
        public static float LookDownDeg(in CameraViewSpec spec, float portrait01)
        {
            float t = CharMath.Clamp01(portrait01);
            return spec.LookDownDeg * (1f + (PortraitLookDownShare - 1f) * t);
        }

        /// <summary>The localisation key of a view's name ("hud.camera.far").</summary>
        public static string Key(CameraView view)
        {
            int i = (int)view;
            return i >= 0 && i < ViewKeys.Length ? ViewKeys[i] : ViewKeys[0];
        }

        /// <summary>The save name of a view ("far").</summary>
        public static string Name(CameraView view)
        {
            int i = (int)view;
            return i >= 0 && i < ViewNames.Length ? ViewNames[i] : ViewNames[0];
        }

        public static bool TryParse(string name, out CameraView view)
        {
            for (int i = 0; i < ViewNames.Length; i++)
            {
                if (string.Equals(ViewNames[i], name, StringComparison.Ordinal))
                {
                    view = (CameraView)i;
                    return true;
                }
            }
            view = CameraView.Near;
            return false;
        }

        /// <summary>The save name of a rig class ("two_wheeler").</summary>
        public static string RigName(RigClass rig)
        {
            int i = (int)rig;
            return i >= 0 && i < RigNames.Length ? RigNames[i] : RigNames[0];
        }
    }

    /// <summary>
    /// The chosen <see cref="CameraView"/> per rig class, kept in the save under
    /// <c>settings.camera_views</c> (<see cref="SaveData.SettingsSection.Extra"/>, so older builds keep it) as
    /// <c>{"two_wheeler": "handlebar", ...}</c>. Unknown names fall back to the class default.
    /// </summary>
    public sealed class CameraViewMemory
    {
        public const string SaveKey = "camera_views";

        private readonly CameraView[] _views = new CameraView[CameraViews.RigCount];

        public CameraViewMemory()
        {
            for (int i = 0; i < _views.Length; i++) _views[i] = CameraViews.Default((RigClass)i);
        }

        /// <summary>The view of <paramref name="rig"/> (always one the class offers).</summary>
        public CameraView Get(RigClass rig)
        {
            int i = (int)rig;
            if (i < 0 || i >= _views.Length) return CameraViews.Default(rig);
            return CameraViews.Valid(rig, _views[i]);
        }

        /// <summary>Chooses <paramref name="view"/> for <paramref name="rig"/>; false (and no change) when the class does
        /// not offer it.</summary>
        public bool Set(RigClass rig, CameraView view)
        {
            int i = (int)rig;
            if (i < 0 || i >= _views.Length || !CameraViews.Offers(rig, view)) return false;
            _views[i] = view;
            return true;
        }

        /// <summary>Moves <paramref name="rig"/> to its next view and returns it.</summary>
        public CameraView Cycle(RigClass rig)
        {
            CameraView next = CameraViews.Next(rig, Get(rig));
            Set(rig, next);
            return next;
        }

        public JsonObject ToJson()
        {
            var o = new JsonObject();
            for (int i = 0; i < _views.Length; i++) o.Set(CameraViews.RigName((RigClass)i), CameraViews.Name(Get((RigClass)i)));
            return o;
        }

        public static CameraViewMemory FromJson(JsonObject o)
        {
            var m = new CameraViewMemory();
            if (o == null) return m;
            for (int i = 0; i < CameraViews.RigCount; i++)
            {
                var rig = (RigClass)i;
                CameraView v;
                if (CameraViews.TryParse(o.GetString(CameraViews.RigName(rig)), out v)) m.Set(rig, v);
            }
            return m;
        }

        /// <summary>The views saved in <paramref name="save"/> (defaults without a save or an entry).</summary>
        public static CameraViewMemory Load(SaveData save)
        {
            return FromJson(save != null ? save.Settings.Extra.GetObject(SaveKey) : null);
        }

        /// <summary>Writes the views into <paramref name="save"/> (the caller persists it).</summary>
        public void Store(SaveData save)
        {
            if (save == null) return;
            save.Settings.Extra.Set(SaveKey, ToJson());
        }
    }

    /// <summary>
    /// Keeps a useful view while reversing (owner feedback: "camera view gets blocked if I try to back up my motorbike in
    /// the narrower streets"). After <see cref="EnterDelayS"/> of reversing the chase camera rises
    /// (<see cref="RaisePitchDeg"/>), shortens (<see cref="ShortenTo"/>) and lifts the player's ground point on screen
    /// (<see cref="FootRaiseNdc"/>, the camera scales it down for the short landscape view) so the lane behind the
    /// vehicle shows under it; after <see cref="SwingDelayS"/> of steady reversing it swings round to look along the
    /// direction of travel. Driving forward (or standing still for
    /// <see cref="StoppedReleaseS"/>) eases everything back. Engine-free; no allocation.
    /// </summary>
    public sealed class ReverseFraming
    {
        /// <summary>Slower than this either way counts as standing.</summary>
        public const float ReverseMps = 0.5f;

        public const float EnterDelayS = 0.3f;
        public const float SwingDelayS = 1.0f;

        /// <summary>The swing needs at least this reversing speed.</summary>
        public const float SwingMinMps = 1.0f;

        public const float ForwardReleaseS = 0.3f;
        public const float StoppedReleaseS = 2.0f;
        public const float RaisePitchDeg = 16f;
        public const float ShortenTo = 0.85f;
        public const float FootRaiseNdc = 0.3f;

        /// <summary>Raise and release rate, per second (exponential approach).</summary>
        public const float RaiseRate = 6f;

        /// <summary>The swing turns the camera round in this long.</summary>
        public const float SwingSeconds = 0.9f;

        private float _reverseS, _forwardS, _stoppedS;
        private bool _raised, _swung;

        /// <summary>0 driving forward, 1 fully in the reversing frame.</summary>
        public float Raise01 { get; private set; }

        /// <summary>0 behind the vehicle, 1 swung round to look along the travel (linear; see <see cref="SwingYawDeg"/>).</summary>
        public float Swing01 { get; private set; }

        /// <summary>True while the reversing frame is wanted.</summary>
        public bool Reversing
        {
            get { return _raised; }
        }

        public float PitchAddDeg
        {
            get { return RaisePitchDeg * Raise01; }
        }

        public float DistanceScale
        {
            get { return 1f + (ShortenTo - 1f) * Raise01; }
        }

        public float FootRaise
        {
            get { return FootRaiseNdc * Raise01; }
        }

        /// <summary>Yaw added to the chase camera, degrees (0 to 180, eased).</summary>
        public float SwingYawDeg
        {
            get { return 180f * CharMath.SmoothStep(Swing01); }
        }

        public void Reset()
        {
            _reverseS = _forwardS = _stoppedS = 0f;
            _raised = _swung = false;
            Raise01 = 0f;
            Swing01 = 0f;
        }

        /// <summary>One frame at <paramref name="signedSpeedMps"/> (negative reversing). <paramref name="allowSwing"/>
        /// false (the player is looking around, or a mounted view) cancels the swing.</summary>
        public void Update(float dt, float signedSpeedMps, bool allowSwing)
        {
            if (!(dt > 0f) || float.IsInfinity(dt)) return;
            if (float.IsNaN(signedSpeedMps) || float.IsInfinity(signedSpeedMps)) signedSpeedMps = 0f;
            if (signedSpeedMps < -ReverseMps)
            {
                _reverseS += dt;
                _forwardS = 0f;
                _stoppedS = 0f;
            }
            else if (signedSpeedMps > ReverseMps)
            {
                _forwardS += dt;
                _reverseS = 0f;
                _stoppedS = 0f;
            }
            else
            {
                _stoppedS += dt;
            }
            if (_reverseS >= EnterDelayS) _raised = true;
            if (_raised && (_forwardS >= ForwardReleaseS || _stoppedS >= StoppedReleaseS))
            {
                _raised = false;
                _swung = false;
            }
            if (_raised && allowSwing && _reverseS >= SwingDelayS && signedSpeedMps < -SwingMinMps) _swung = true;
            if (!allowSwing || _forwardS > 0f) _swung = false;
            Raise01 = CharMath.Approach(Raise01, _raised ? 1f : 0f, RaiseRate, dt);
            if (Raise01 < 1e-4f) Raise01 = 0f;
            if (Raise01 > 1f - 1e-4f) Raise01 = 1f;
            float step = dt / SwingSeconds;
            Swing01 = _swung ? Math.Min(1f, Swing01 + step) : Math.Max(0f, Swing01 - step);
        }
    }
}
