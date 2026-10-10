using System;
using Ghumante.Core.Driving;
using Ghumante.Core.Save;

namespace Ghumante.Core.Characters
{
    /// <summary>The numbers of one chase-camera rig (W2_DESIGN 6.4).</summary>
    public struct RigParams
    {
        /// <summary>Distance from the pivot to the camera, metres.</summary>
        public float DistanceM;

        /// <summary>Elevation of the camera above the pivot, degrees.</summary>
        public float PitchDeg;

        /// <summary>Pivot height above the player's (or vehicle's) ground point.</summary>
        public float PivotM;

        /// <summary>Look-ahead: speed × this many seconds, up to <see cref="LookAheadMaxM"/>.</summary>
        public float LookAheadS, LookAheadMaxM;

        /// <summary>Minimum horizontal field of view, degrees (walk 55°, drive 62°, bus and truck 64°).</summary>
        public float MinHFovDeg;

        /// <summary>Heading follow smoothing, seconds.</summary>
        public float FollowS;

        /// <summary>Speed FOV kick, degrees, reached at <see cref="KickFullMps"/> (above <see cref="KickFromMps"/>).</summary>
        public float FovKickDeg, KickFromMps, KickFullMps;

        /// <summary>Collision minimum distance (walk 2.5 m, vehicles 4 m).</summary>
        public float MinDistanceM;

        /// <summary>Sideways offset of the pivot, metres, right positive (0 except the over-the-shoulder view).</summary>
        public float ShoulderM;

        /// <summary>The camera looks through the pivot instead of pinning the player's ground point on screen (the
        /// over-the-shoulder view, whose pivot is the shoulder).</summary>
        public bool AimAtPivot;
    }

    /// <summary>
    /// The camera rig per class and orientation (W2_DESIGN 6.4): distance ≈ 6 + 1 × vehicle length in landscape, portrait
    /// 10–15% farther with 8–10° more pitch and twice the look-ahead time. The portrait walking rig is 6.8 m / 20° / 0.9 m
    /// (W2 change from 8.6 / 22 / 1.0) so the player grows from about 187 px to about 240 px on a 1080 × 2340 phone.
    /// Passengers orbit at 1.2× the driver rig of their vehicle. Engine-free; the Unity rig reads it.
    /// </summary>
    public static class CameraRigTable
    {
        public const float PassengerScale = 1.2f;

        /// <summary>Passenger ride-along auto-yaw towards points of interest within 300 m, degrees per second.</summary>
        public const float PassengerAutoYawDegPerS = 6f;

        public static RigParams For(RigClass rig, bool portrait)
        {
            if (rig == RigClass.Passenger) return For(rig, portrait, RigClass.Car);
            switch (rig)
            {
                case RigClass.Walk:
                    return portrait
                        ? R(6.8f, 20f, 0.9f, 0.6f, 4.5f, 55f, 1.0f, 0f, 0f, 1f, 2.5f)
                        : R(8.4f, 12f, 1.0f, 0.3f, 2.5f, 55f, 0.9f, 0f, 0f, 1f, 2.5f);
                case RigClass.Bicycle:
                    return portrait
                        ? R(8.5f, 20f, 1.0f, 0.8f, 10f, 62f, 0.30f, 3f, 3f, 8.3f, 4f)
                        : R(7.5f, 12f, 1.0f, 0.4f, 6f, 62f, 0.30f, 3f, 3f, 8.3f, 4f);
                case RigClass.TwoWheeler:
                    return portrait
                        ? R(9.2f, 21f, 1.0f, 0.9f, 16f, 62f, 0.32f, 6f, 16.7f, 25f, 4f)
                        : R(8.0f, 13f, 1.0f, 0.45f, 9f, 62f, 0.28f, 6f, 16.7f, 25f, 4f);
                case RigClass.Car:
                    return portrait
                        ? R(10.8f, 22f, 1.3f, 1.0f, 20f, 62f, 0.35f, 5f, 5f, 25f, 4f)
                        : R(9.5f, 14f, 1.3f, 0.5f, 12f, 62f, 0.35f, 5f, 5f, 25f, 4f);
                case RigClass.Van:
                    return portrait
                        ? R(12.0f, 23f, 1.6f, 1.0f, 20f, 62f, 0.40f, 4f, 5f, 22f, 4f)
                        : R(10.5f, 15f, 1.6f, 0.5f, 12f, 62f, 0.40f, 4f, 5f, 22f, 4f);
                case RigClass.Bus:
                    return portrait
                        ? R(18.5f, 25f, 2.6f, 1.2f, 28f, 64f, 0.60f, 2f, 5f, 17f, 4f)
                        : R(16.5f, 17f, 2.6f, 0.6f, 18f, 64f, 0.60f, 2f, 5f, 17f, 4f);
                case RigClass.Truck:
                    return portrait
                        ? R(16.5f, 24f, 2.4f, 1.2f, 26f, 64f, 0.55f, 2f, 5f, 15f, 4f)
                        : R(14.5f, 16f, 2.4f, 0.6f, 16f, 64f, 0.55f, 2f, 5f, 15f, 4f);
                case RigClass.Tractor:
                    return portrait
                        ? R(11.5f, 26f, 1.8f, 0.6f, 10f, 62f, 0.50f, 0f, 0f, 1f, 4f)
                        : R(10.0f, 18f, 1.8f, 0.3f, 6f, 62f, 0.50f, 0f, 0f, 1f, 4f);
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

        /// <summary>The ride-along rig for a driver rig: 1.2× the distance, no look-ahead, no FOV kick.</summary>
        public static RigParams Passenger(in RigParams driver)
        {
            RigParams p = driver;
            p.DistanceM *= PassengerScale;
            p.LookAheadS = 0f;
            p.LookAheadMaxM = 0f;
            p.FovKickDeg = 0f;
            p.FollowS = Math.Max(driver.FollowS, 0.6f);
            return p;
        }

        /// <summary>The FOV kick at a speed (none below the threshold, full at the top).</summary>
        public static float FovKick(in RigParams r, float speedMps)
        {
            if (r.FovKickDeg <= 0f || r.KickFullMps <= r.KickFromMps) return 0f;
            float t = (Math.Abs(speedMps) - r.KickFromMps) / (r.KickFullMps - r.KickFromMps);
            return r.FovKickDeg * CharMath.Clamp01(t);
        }

        private static RigParams R(float d, float pitch, float pivot, float laS, float laMax, float fov, float follow, float kick, float kickFrom,
                                   float kickFull, float minD)
        {
            return new RigParams
            {
                DistanceM = d, PitchDeg = pitch, PivotM = pivot, LookAheadS = laS, LookAheadMaxM = laMax, MinHFovDeg = fov, FollowS = follow,
                FovKickDeg = kick, KickFromMps = kickFrom, KickFullMps = kickFull, MinDistanceM = minD,
            };
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

    /// <summary>
    /// The collision-aware boom of the chase camera (W2_DESIGN 6.4; owner feedback: "houses block the view", "camera view
    /// gets blocked if I try to back up my motorbike in the narrower streets"). Each frame it sweeps a
    /// <see cref="ProbeRadiusM"/> sphere from the pivot over the player towards where the camera wants to be
    /// (<see cref="IViewObstacleQuery"/>, game metres with absolute heights; see <see cref="Reach"/>) and:
    /// <list type="bullet">
    /// <item>pulls in at once to stay <see cref="SkinM"/> short of the first hit, so the camera is never inside or behind a
    /// wall, then eases back out over about <see cref="EaseOutSeconds"/> after a short <see cref="HoldSeconds"/>;</item>
    /// <item>when that leaves less than the rig's minimum distance (a low wall right behind), tries steeper booms in
    /// <see cref="LiftStepDeg"/> steps and lifts over the wall smoothly;</item>
    /// <item>when even that leaves less than <see cref="FallbackClearM"/> (the player backed against a house, a scooter
    /// stopped at the end of a dead-end lane), where any boom would sit inside the player's own body, switches to the
    /// <b>overhead fallback</b>: the pivot rises straight up by up to <see cref="OverheadRiseM"/> (less under a ceiling)
    /// over <see cref="OverheadSeconds"/>, the boom is swept again from there, and the camera looks down the way ahead
    /// (<see cref="OverheadLook01"/>, <see cref="OverheadLookDownDeg"/>; over a low wall it looks back at the player);
    /// it returns once the boom has <see cref="FallbackExitM"/> of room again;</item>
    /// <item>in a lane with walls within <see cref="LaneProbeM"/> on both sides (old-core lanes, about 4.8 m wide) blends
    /// to the lane frame: <see cref="LanePitchDeg"/> steeper and <see cref="LaneDistanceScale"/> as long, so it looks
    /// over the eaves instead of into the walls.</item>
    /// </list>
    /// A sweep that starts inside or touching something is never taken as clear (<see cref="Reach"/>). The camera sits at
    /// the pivot plus <see cref="CameraOffset"/>. Without a query it only eases towards the wanted length. Engine-free; no
    /// allocation; usually one or two sweeps per frame (at most about sixteen while lifting into the fallback).
    /// </summary>
    public sealed class ChaseBoom
    {
        public const float ProbeRadiusM = 0.3f;

        /// <summary>Every sweep starts this far back along its direction, on the player's side of its origin, so a wall
        /// the origin is backed against (closer than <see cref="ProbeRadiusM"/>) is met, not started in.</summary>
        public const float BackOffM = 0.35f;

        /// <summary>When the backed-off sweep meets something before it reaches its origin, a thin sweep of this radius
        /// from the origin itself (always clear of the player's 0.3 m body) tells a wall right behind from something on
        /// the player's side.</summary>
        public const float CoreRadiusM = 0.12f;

        public const float SkinM = 0.1f;

        /// <summary>A thin sweep that meets something within this of its start started inside it: blocked at once.</summary>
        public const float StartInsideM = 0.02f;

        public const float EaseOutSeconds = 0.6f;
        public const float HoldSeconds = 0.2f;
        public const float LiftStepDeg = 15f;
        public const int LiftSteps = 3;
        public const float MaxPitchDeg = 80f;

        /// <summary>Lift rises and falls at these rates, per second (exponential approach).</summary>
        public const float LiftRiseRate = 10f, LiftFallRate = 2.5f;

        public const float LaneProbeM = 3f;
        public const float LanePitchDeg = 15f;
        public const float LaneDistanceScale = 0.85f;
        public const float LaneBlendSeconds = 0.5f;

        /// <summary>The side probes run at 10 Hz (the lane blend is slow anyway).</summary>
        public const float LaneIntervalS = 0.1f;

        /// <summary>The overhead fallback starts when the clear boom (after lifting) is shorter than this: about the
        /// player's own size, where the camera would sit inside the rider.</summary>
        public const float FallbackClearM = 1.2f;

        /// <summary>... and ends once the boom from the pivot has this much room again for <see cref="FallbackExitS"/>.</summary>
        public const float FallbackExitM = 1.8f;

        public const float FallbackExitS = 0.25f;

        /// <summary>The fallback raises the pivot this far straight up (less under a ceiling or an eave).</summary>
        public const float OverheadRiseM = 2.4f;

        /// <summary>The fallback blends in and out over this long.</summary>
        public const float OverheadSeconds = 0.3f;

        /// <summary>In the fallback the camera looks this far down from level along the boom's heading, degrees: the lane
        /// ahead with the vehicle's front (landscape) or the whole rider (portrait's tall view) at the bottom.</summary>
        public const float OverheadLookDownLandscapeDeg = 58f, OverheadLookDownPortraitDeg = 48f;

        private const double Deg2Rad = Math.PI / 180.0;

        private float _distance = -1f;
        private float _lift;
        private float _hold;
        private float _lane;
        private float _laneTimer;
        private bool _laneWalls;
        private bool _overhead;
        private float _over;
        private float _overExit;

        /// <summary>The boom length this frame, metres (from the raised pivot in the fallback).</summary>
        public float DistanceM { get; private set; }

        /// <summary>The boom pitch this frame (wanted pitch + lane + lift), degrees.</summary>
        public float PitchDeg { get; private set; }

        /// <summary>How far the overhead fallback raised the pivot this frame, metres (0 outside it).</summary>
        public float RiseM { get; private set; }

        /// <summary>True while the overhead fallback is wanted (the boom has no room behind the player).</summary>
        public bool Overhead
        {
            get { return _overhead; }
        }

        /// <summary>Overhead fallback blend, 0 to 1 (eased): the camera's aim turns to look down the way ahead.</summary>
        public float Overhead01
        {
            get { return CharMath.SmoothStep(_over); }
        }

        /// <summary>How far the camera's aim turns to the overhead look (<see cref="OverheadLookDownDeg"/>), 0 to 1: the
        /// fallback's blend, fading out again where the boom from the raised pivot finds room (backed against a low
        /// garden wall the camera rises over it and looks back at the player as usual).</summary>
        public float OverheadLook01
        {
            get { return Overhead01 * (1f - CharMath.Clamp01((DistanceM - 0.3f) / 1.5f)); }
        }

        /// <summary>True while the fallback is wanted or still blending: only then may the camera come closer to the
        /// pivot than <see cref="FallbackClearM"/> (crossing over the rider's head).</summary>
        public bool FallbackActive
        {
            get { return _overhead || _over > 0f; }
        }

        /// <summary>Extra pitch to clear a low wall, degrees (smoothed).</summary>
        public float LiftDeg
        {
            get { return _lift; }
        }

        /// <summary>Lane frame blend, 0 to 1 (eased).</summary>
        public float Lane01
        {
            get { return CharMath.SmoothStep(_lane); }
        }

        /// <summary>Free length along this frame's boom direction (with the skin taken off).</summary>
        public float ClearM { get; private set; }

        /// <summary>Something cut the boom short this frame.</summary>
        public bool Blocked { get; private set; }

        /// <summary>Sweeps made by the last <see cref="Solve"/>.</summary>
        public int Casts { get; private set; }

        /// <summary>The overhead fallback's downward look for an orientation blend (0 landscape, 1 portrait), degrees.</summary>
        public static float OverheadLookDownDeg(float portrait01)
        {
            float t = CharMath.Clamp01(portrait01);
            return OverheadLookDownLandscapeDeg + (OverheadLookDownPortraitDeg - OverheadLookDownLandscapeDeg) * t;
        }

        /// <summary>Next solve starts fresh (spawn, teleport): no smoothing from the old length, lift, lane or fallback.</summary>
        public void Snap()
        {
            _distance = -1f;
            _lift = 0f;
            _hold = 0f;
            _laneTimer = 0f;
            _overhead = false;
            _over = 0f;
            _overExit = 0f;
        }

        /// <summary>
        /// Places the boom for this frame: pivot (<paramref name="px"/>, <paramref name="py"/>, <paramref name="pz"/>) in
        /// game metres, boom yaw (degrees, 0 = north, clockwise: the camera sits behind, at −forward) and pitch
        /// (degrees, camera above the pivot), the wanted length and the rig's minimum. Read <see cref="CameraOffset"/>
        /// (or <see cref="RiseM"/>, <see cref="DistanceM"/> and <see cref="PitchDeg"/>) afterwards.
        /// </summary>
        public void Solve(IViewObstacleQuery query, double px, double py, double pz, float yawDeg, float pitchDeg, float wantedM,
                          float minM, float dt)
        {
            Casts = 0;
            if (!(dt >= 0f) || float.IsInfinity(dt)) dt = 0f;
            if (float.IsNaN(wantedM) || wantedM < 0f) wantedM = 0f;
            if (float.IsNaN(pitchDeg)) pitchDeg = 0f;
            bool snap = _distance < 0f;

            UpdateLane(query, px, py, pz, yawDeg, dt, snap);
            float lane = Lane01;
            float pitch = Math.Min(MaxPitchDeg, pitchDeg + LanePitchDeg * lane);
            float want = wantedM * (1f + (LaneDistanceScale - 1f) * lane);
            float min = Math.Min(Math.Max(0f, minM), want);

            float liftTarget = 0f;
            float clear0 = query != null ? Clear(query, px, py, pz, yawDeg, pitch, want) : want;
            if (query != null && clear0 < min - 1e-3f)
            {
                float best = clear0, bestLift = 0f;
                for (int k = 1; k <= LiftSteps; k++)
                {
                    float p = Math.Min(MaxPitchDeg, pitch + k * LiftStepDeg);
                    if (p <= pitch + 1e-3f) break;
                    float c = Clear(query, px, py, pz, yawDeg, p, want);
                    if (c > best + 0.25f)
                    {
                        best = c;
                        bestLift = p - pitch;
                    }
                    if (c >= min) break;
                }
                liftTarget = bestLift;
            }
            if (snap) _lift = liftTarget;
            else _lift = CharMath.Approach(_lift, liftTarget, liftTarget > _lift ? LiftRiseRate : LiftFallRate, dt);
            if (_lift < 0.01f && liftTarget <= 0f) _lift = 0f;

            float finalPitch = Math.Min(MaxPitchDeg, pitch + _lift);
            float clearBase = clear0;
            if (query != null && _lift > 0f) clearBase = Clear(query, px, py, pz, yawDeg, finalPitch, want);

            // The overhead fallback: no room for a boom behind the player. In at once, out after a short hold.
            UpdateOverhead(query != null, clearBase, want, dt, snap);
            float rise = 0f, clear = clearBase;
            if (_over > 0f && query != null)
            {
                rise = Reach(query, px, py, pz, 0.0, 1.0, 0.0, OverheadRiseM) * CharMath.SmoothStep(_over);
                if (rise > 1e-3f) clear = Clear(query, px, py + rise, pz, yawDeg, finalPitch, want);
                else rise = 0f;
            }

            float target = Math.Min(want, clear);
            if (snap)
            {
                _distance = target;
                _hold = 0f;
            }
            else if (target < _distance)
            {
                // In at once: never inside or behind a wall.
                _distance = target;
                _hold = HoldSeconds;
            }
            else if (_hold > 0f)
            {
                _hold -= dt;
            }
            else
            {
                _distance = CharMath.Approach(_distance, target, 3f / EaseOutSeconds, dt);
                if (target - _distance < 1e-3f) _distance = target;
            }
            _distance = Math.Min(_distance, clear);
            Blocked = clear < want - 1e-3f;
            ClearM = clear;
            DistanceM = _distance;
            PitchDeg = finalPitch;
            RiseM = rise;
        }

        /// <summary>The camera's offset from the pivot this frame (game metres, absolute y): up by <see cref="RiseM"/>,
        /// then <see cref="DistanceM"/> along the boom at <paramref name="yawDeg"/> and <see cref="PitchDeg"/>.</summary>
        public void CameraOffset(float yawDeg, out double ox, out double oy, out double oz)
        {
            double dx, dy, dz;
            Direction(yawDeg, PitchDeg, out dx, out dy, out dz);
            ox = dx * DistanceM;
            oy = RiseM + dy * DistanceM;
            oz = dz * DistanceM;
        }

        /// <summary>
        /// Free length (minus <see cref="SkinM"/>) for a <see cref="ProbeRadiusM"/> sphere moved from a point along a unit
        /// direction, up to <paramref name="length"/>. The sweep starts <see cref="BackOffM"/> back, on the player's side
        /// of the point, so a wall the point is backed against is met instead of started in (a sphere that starts in a
        /// solid ignores it, which would put the camera inside the house). When it meets something before reaching the
        /// point, a thin <see cref="CoreRadiusM"/> sweep from the point itself decides: a wall right behind gives 0, a thing
        /// on the player's side is ignored, and a thin sweep that starts inside something is blocked at once. Never
        /// takes a start inside as clear.
        /// </summary>
        public float Reach(IViewObstacleQuery query, double ox, double oy, double oz, double dx, double dy, double dz, float length)
        {
            if (query == null || !(length > 0f)) return Math.Max(0f, length);
            Casts++;
            double hit;
            const double back = BackOffM;
            if (!query.SphereCast(ox - dx * back, oy - dy * back, oz - dz * back, dx, dy, dz, ProbeRadiusM, length + back + SkinM, out hit))
                return length;
            if (double.IsNaN(hit)) hit = 0.0;
            if (hit >= back - 1e-6) return Free(hit - back, length);

            // Contact before the probe reached the point: a wall closer behind it than the probe radius, or something on
            // the player's side of it. The thin sweep from the point sees only what lies ahead along the direction.
            Casts++;
            const double widen = ProbeRadiusM - CoreRadiusM;
            if (!query.SphereCast(ox, oy, oz, dx, dy, dz, CoreRadiusM, length + SkinM + widen, out hit)) return length;
            if (double.IsNaN(hit) || hit < StartInsideM) return 0f;
            return Free(hit - widen, length);
        }

        /// <summary>The camera offset from the pivot for a boom of unit length at (<paramref name="yawDeg"/>,
        /// <paramref name="pitchDeg"/>): behind and above.</summary>
        public static void Direction(float yawDeg, float pitchDeg, out double dx, out double dy, out double dz)
        {
            double yaw = yawDeg * Deg2Rad, pitch = pitchDeg * Deg2Rad;
            double c = Math.Cos(pitch);
            dx = -Math.Sin(yaw) * c;
            dy = Math.Sin(pitch);
            dz = -Math.Cos(yaw) * c;
        }

        private static float Free(double travelled, float length)
        {
            return (float)Math.Max(0.0, Math.Min(length, travelled - SkinM));
        }

        private float Clear(IViewObstacleQuery query, double px, double py, double pz, float yawDeg, float pitchDeg, float length)
        {
            double dx, dy, dz;
            Direction(yawDeg, pitchDeg, out dx, out dy, out dz);
            return Reach(query, px, py, pz, dx, dy, dz, length);
        }

        private void UpdateOverhead(bool hasQuery, float clearBase, float want, float dt, bool snap)
        {
            if (!hasQuery)
            {
                _overhead = false;
                _overExit = 0f;
            }
            else if (!_overhead)
            {
                _overhead = clearBase < Math.Min(FallbackClearM, want - 1e-3f);
                _overExit = 0f;
            }
            else if (clearBase < Math.Min(FallbackExitM, want - 1e-3f))
            {
                _overExit = 0f;
            }
            else
            {
                _overExit += dt;
                if (_overExit >= FallbackExitS || snap) _overhead = false;
            }
            float goal = _overhead ? 1f : 0f;
            if (snap) _over = goal;
            else
            {
                float step = dt / OverheadSeconds;
                _over = _over < goal ? Math.Min(goal, _over + step) : Math.Max(goal, _over - step);
            }
        }

        private void UpdateLane(IViewObstacleQuery query, double px, double py, double pz, float yawDeg, float dt, bool snap)
        {
            if (query == null)
            {
                _laneWalls = false;
            }
            else
            {
                _laneTimer -= dt;
                if (snap || _laneTimer <= 0f)
                {
                    _laneTimer = LaneIntervalS;
                    double yaw = yawDeg * Deg2Rad;
                    double rx = Math.Cos(yaw), rz = -Math.Sin(yaw);
                    bool right = Reach(query, px, py, pz, rx, 0.0, rz, LaneProbeM) < LaneProbeM - 1e-3f;
                    bool left = right && Reach(query, px, py, pz, -rx, 0.0, -rz, LaneProbeM) < LaneProbeM - 1e-3f;
                    _laneWalls = right && left;
                }
            }
            float goal = _laneWalls ? 1f : 0f;
            if (snap) _lane = goal;
            else
            {
                float step = dt / LaneBlendSeconds;
                _lane = _lane < goal ? Math.Min(goal, _lane + step) : Math.Max(goal, _lane - step);
            }
        }
    }
}
