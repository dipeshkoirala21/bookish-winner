using System;
using Ghumante.Core.Driving;

namespace Ghumante.Characters
{
    /// <summary>The input device that produced the latest control input (touch controls hide for the others).</summary>
    public enum ControlDevice : byte
    {
        None = 0,
        KeyboardMouse = 1,
        Gamepad = 2,
        Touch = 3,
    }

    /// <summary>
    /// One frame of explorer controls, whatever produced them (keyboard, gamepad, the HUD's touch controls): the
    /// common currency between input sources and <see cref="ControlMapper"/>. Engine-free.
    /// <list type="bullet">
    /// <item><see cref="MoveX"/>/<see cref="MoveY"/>: a stick in [−1, 1] (right/forward positive): WASD or the arrows, the
    /// gamepad's left stick, a touch stick or the portrait drag. Steering on the scooter (X only), camera-relative
    /// direction on foot.</item>
    /// <item><see cref="Throttle"/>, <see cref="Reverse"/> in [0, 1]: W / up / right trigger / the throttle pedal; S / down /
    /// left trigger / the brake-reverse pedal (it brakes, then reverses once stopped).</item>
    /// <item><see cref="Brake"/> in [0, 1]: Space / B, brakes without reversing. <see cref="Boost"/>: Shift / X.</item>
    /// <item>Buttons pressed this frame (<see cref="ToggleMode"/>, <see cref="Map"/>, <see cref="Pause"/>,
    /// <see cref="Search"/>, <see cref="CameraCycle"/>) and camera deltas (<see cref="ZoomSteps"/>, <see cref="LookYawDeg"/>,
    /// <see cref="LookPitchDeg"/>).</item>
    /// </list>
    /// </summary>
    public struct ControlFrame
    {
        public float MoveX, MoveY;
        public float Throttle, Reverse, Brake;
        public bool Boost;

        /// <summary>E / gamepad A / the HUD's Action button pressed this frame: Hop on (the offered seat), Hop off, or
        /// Jump on foot when nothing is offered (W2_DESIGN 6.2, 6.5).</summary>
        public bool ToggleMode;

        /// <summary>The Action input is held (E, gamepad A, the HUD's Action button): a 0.35 s hold rides as a passenger,
        /// a 0.4 s hold with nothing offered hails a taxi.</summary>
        public bool ActionHeld;

        /// <summary>Space on foot / the HUD's Jump: jump (on foot only; Space brakes in a vehicle).</summary>
        public bool Jump;

        /// <summary>H / left-stick press / the HUD's Horn (held: a long horn).</summary>
        public bool Horn;

        /// <summary>N / D-pad up / the HUD's Namaste button this frame.</summary>
        public bool Namaste;

        /// <summary>G / D-pad down / the HUD's Garage whistle this frame: summon the selected garage vehicle.</summary>
        public bool Whistle;

        /// <summary>B key / D-pad right / the HUD's Stop bell this frame (riding as a passenger: stop at the next stop).</summary>
        public bool Bell;

        /// <summary>The HUD's small "Ride as passenger" button this frame.</summary>
        public bool RidePassenger;

        /// <summary>M / gamepad Select (the map arrives in wave 2).</summary>
        public bool Map;

        /// <summary>Gamepad Start (Escape and Android back arrive through the UI as a cancel).</summary>
        public bool Pause;

        /// <summary>/ or gamepad Y: open search.</summary>
        public bool Search;

        /// <summary>C, the gamepad's right-stick press or the HUD camera button this frame: the next camera angle of the
        /// current class (<c>CameraViews</c>).</summary>
        public bool CameraCycle;

        /// <summary>Camera zoom this frame in notches: positive moves in, negative out.</summary>
        public float ZoomSteps;

        /// <summary>Camera orbit (yaw, degrees, positive to the right) and pitch (degrees, positive looks down more)
        /// this frame.</summary>
        public float LookYawDeg, LookPitchDeg;

        /// <summary>True while a look input is held (the camera does not swing back behind the explorer).</summary>
        public bool Looking;

        /// <summary>The device that produced input this frame (None when nothing was touched).</summary>
        public ControlDevice Device;

        /// <summary>
        /// Adds another source (the touch controls) to this one: sticks and pedals keep the stronger value, buttons and
        /// looking combine, camera deltas add up, and the device becomes the other's when it produced input.
        /// </summary>
        public void Merge(in ControlFrame other)
        {
            MoveX = Stronger(MoveX, other.MoveX);
            MoveY = Stronger(MoveY, other.MoveY);
            Throttle = Math.Max(Throttle, other.Throttle);
            Reverse = Math.Max(Reverse, other.Reverse);
            Brake = Math.Max(Brake, other.Brake);
            Boost |= other.Boost;
            ToggleMode |= other.ToggleMode;
            ActionHeld |= other.ActionHeld;
            Jump |= other.Jump;
            Horn |= other.Horn;
            Namaste |= other.Namaste;
            Whistle |= other.Whistle;
            Bell |= other.Bell;
            RidePassenger |= other.RidePassenger;
            Map |= other.Map;
            Pause |= other.Pause;
            Search |= other.Search;
            CameraCycle |= other.CameraCycle;
            ZoomSteps += other.ZoomSteps;
            LookYawDeg += other.LookYawDeg;
            LookPitchDeg += other.LookPitchDeg;
            Looking |= other.Looking;
            if (other.Device != ControlDevice.None) Device = other.Device;
        }

        /// <summary>True when any stick, pedal or button is in use (camera deltas do not count).</summary>
        public bool HasDriveInput
        {
            get
            {
                return MoveX != 0f || MoveY != 0f || Throttle > 0f || Reverse > 0f || Brake > 0f || Boost;
            }
        }

        private static float Stronger(float a, float b)
        {
            return Math.Abs(b) > Math.Abs(a) ? b : a;
        }
    }

    /// <summary>
    /// Turns a <see cref="ControlFrame"/> into <see cref="DriveInput"/> for Core's <see cref="ArcadeVehicle"/>: on the
    /// scooter the stick steers and the pedals drive; on foot the stick is a direction relative to the camera, its
    /// length the gait (Core's walker walks up to half stick and runs at full). A radial dead zone keeps resting
    /// thumbs and worn sticks from creeping. Engine-free.
    /// </summary>
    public static class ControlMapper
    {
        /// <summary>Stick deflection that counts as none (radial).</summary>
        public const float StickDeadZone = 0.15f;

        /// <summary>
        /// Radial dead zone: inside <paramref name="deadZone"/> the stick reads (0, 0); outside, the length is rescaled
        /// so it starts from 0 at the edge of the dead zone and reaches 1 at full deflection (the direction is kept,
        /// longer vectors are clamped to 1).
        /// </summary>
        public static void ApplyDeadZone(float x, float y, float deadZone, out float outX, out float outY)
        {
            if (float.IsNaN(x) || float.IsInfinity(x)) x = 0f;
            if (float.IsNaN(y) || float.IsInfinity(y)) y = 0f;
            float length = (float)Math.Sqrt(x * x + y * y);
            if (length <= deadZone || length < 1e-6f)
            {
                outX = 0f;
                outY = 0f;
                return;
            }
            float scaled = Math.Min(1f, (length - deadZone) / (1f - deadZone));
            outX = x / length * scaled;
            outY = y / length * scaled;
        }

        /// <summary>Scooter: stick X steers, Throttle drives, Reverse brakes and then reverses once stopped, Brake
        /// brakes, Boost boosts. Reverse held together with Throttle (both triggers, W and S, the drive zone and the Brake
        /// pedal) is a brake that cuts the throttle, never a coast.</summary>
        public static DriveInput Ride(in ControlFrame f)
        {
            float steer, unused;
            ApplyDeadZone(f.MoveX, 0f, StickDeadZone, out steer, out unused);
            float forward = Clamp(Finite(f.Throttle), 0f, 1f);
            float reverse = Clamp(Finite(f.Reverse), 0f, 1f);
            float brake = Clamp(Finite(f.Brake), 0f, 1f);
            float throttle;
            if (forward > 0f && reverse > 0f)
            {
                throttle = 0f;
                brake = Math.Max(brake, reverse);
            }
            else
            {
                throttle = forward - reverse;
            }
            return new DriveInput(throttle, brake, steer, f.Boost);
        }

        /// <summary>
        /// On foot: the direction to walk in and how fast. Returns false (and throttle 0) when the stick rests. The stick
        /// is relative to the camera: up walks away from it. <paramref name="cameraYawRad"/> uses
        /// <see cref="ArcadeVehicle"/>'s heading convention (0 = north, clockwise).
        /// </summary>
        public static bool Walk(in ControlFrame f, float cameraYawRad, out float targetHeadingRad, out float throttle)
        {
            float x, y;
            ApplyDeadZone(f.MoveX, f.MoveY, StickDeadZone, out x, out y);
            float length = (float)Math.Sqrt(x * x + y * y);
            if (length < 1e-4f)
            {
                targetHeadingRad = 0f;
                throttle = 0f;
                return false;
            }
            targetHeadingRad = ArcadeVehicle.WrapAngle(Finite(cameraYawRad) + (float)Math.Atan2(x, y));
            throttle = Math.Min(1f, length);
            return true;
        }

        /// <summary>The walker's drive input towards <paramref name="targetHeadingRad"/>: steer from
        /// <see cref="ArcadeVehicle.SteerToward"/> so it turns there within the frame, then walks.</summary>
        public static DriveInput WalkInput(ArcadeVehicle walker, in ControlFrame f, float cameraYawRad, float dt)
        {
            if (walker == null) throw new ArgumentNullException(nameof(walker));
            float heading, throttle;
            if (!Walk(f, cameraYawRad, out heading, out throttle)) return new DriveInput(0f, 0f, 0f, false);
            return new DriveInput(throttle, 0f, walker.SteerToward(heading, dt), f.Boost);
        }

        private static float Clamp(float v, float min, float max)
        {
            return v < min ? min : v > max ? max : v;
        }

        private static float Finite(float v)
        {
            return float.IsNaN(v) || float.IsInfinity(v) ? 0f : v;
        }
    }
}
