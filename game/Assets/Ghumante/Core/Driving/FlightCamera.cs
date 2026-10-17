using System;

namespace Ghumante.Core.Driving
{
    /// <summary>The two flight views (C, the right-stick press or the HUD camera button switches).</summary>
    public enum FlightView : byte
    {
        /// <summary>Behind and above, the horizon kept level (the default).</summary>
        Chase = 0,

        /// <summary>The pilot's eye: banks and pitches with the aircraft.</summary>
        Cockpit = 1,
    }

    /// <summary>A camera pose: position (game metres, absolute height) and yaw / pitch / roll in the flight model's
    /// conventions (yaw 0 north clockwise, pitch nose up +, roll right wing down +) and the field of view.</summary>
    public struct FlightCameraPose
    {
        public double X, Y, Z;
        public float Yaw, Pitch, Roll, FovDeg;
    }

    /// <summary>
    /// Flight cameras (docs/research/w2/ref_flight.md §8): a <b>chase</b> view that sits behind the aircraft along its
    /// flight path at a distance scaled to its size (1.25 × its length or span + 8 m, a fifth of that above), its yaw
    /// easing after the track (0.6 s) and its pitch after a third of the climb angle, never rolling with the bank, so the
    /// horizon is steady and the camera does not swing about; looking around (drag, right stick) orbits it and springs
    /// back; it is kept above the ground. A <b>cockpit</b> view puts the eye at the pilot's seat and moves with the
    /// aircraft exactly (look-around turns the head). Engine-free; positions are recomputed from the current pose every
    /// frame (only angles are smoothed), so nothing lags at 140 m/s.
    /// </summary>
    public sealed class FlightCamera
    {
        public const float ChaseFovDeg = 60f, CockpitFovDeg = 72f;

        /// <summary>Time constants of the chase yaw and pitch (s) and of the look-around spring back.</summary>
        public const float YawTauS = 0.6f, PitchTauS = 0.9f, LookReturnS = 1.2f;

        private float _yaw, _pitch, _lookYaw, _lookPitch, _lookIdle;
        private bool _init;

        /// <summary>Jump to the target next update (boarding, a reset).</summary>
        public void Snap()
        {
            _init = false;
        }

        /// <summary>Chase distance behind the aircraft for a spec (m).</summary>
        public static float ChaseDistance(FlightSpec s)
        {
            return s.Rotorcraft ? 20f : 1.25f * s.Size + 8f;
        }

        /// <summary>
        /// The camera for this frame. <paramref name="lookYawDeg"/> / <paramref name="lookPitchDeg"/> are this frame's
        /// look deltas (degrees), <paramref name="looking"/> holds them while a finger or the mouse is down.
        /// </summary>
        public FlightCameraPose Update(float dt, in FlightPose p, double vx, double vy, double vz, FlightSpec spec, FlightView view, float lookYawDeg,
                                       float lookPitchDeg, bool looking, IFlightWorld world)
        {
            const float deg = (float)(Math.PI / 180.0);
            if (!(dt >= 0f) || float.IsInfinity(dt)) dt = 0f;
            dt = Math.Min(dt, 0.1f);
            _lookYaw = Wrap(_lookYaw + lookYawDeg * deg);
            _lookPitch = Clamp(_lookPitch + lookPitchDeg * deg, -60f * deg, 60f * deg);
            if (lookYawDeg != 0f || lookPitchDeg != 0f || looking) _lookIdle = 0f;
            else _lookIdle += dt;
            if (_lookIdle > LookReturnS)
            {
                float k = 1f - (float)Math.Exp(-2.5f * dt);
                _lookYaw -= _lookYaw * k;
                _lookPitch -= _lookPitch * k;
            }

            if (view == FlightView.Cockpit) return Cockpit(p, spec);

            // Chase: follow the track (or the nose when slow), a third of the climb angle, no roll.
            double hs = Math.Sqrt(vx * vx + vz * vz);
            float track = hs > 8.0 ? (float)Math.Atan2(vx, vz) : p.Heading;
            float climb = hs > 8.0 ? (float)Math.Atan2(vy, hs) : 0f;
            float wantPitch = Clamp(climb / 3f, -12f * deg, 12f * deg);
            if (!_init)
            {
                _yaw = track;
                _pitch = wantPitch;
                _init = true;
            }
            else
            {
                _yaw = Wrap(_yaw + Wrap(track - _yaw) * (1f - (float)Math.Exp(-dt / YawTauS)));
                _pitch += (wantPitch - _pitch) * (1f - (float)Math.Exp(-dt / PitchTauS));
            }
            float dist = ChaseDistance(spec);
            float height = spec.Rotorcraft ? 5.5f : 0.2f * dist;
            float yaw = Wrap(_yaw + _lookYaw);
            float elev = Clamp(_pitch + (float)Math.Atan2(height, dist) + _lookPitch, -20f * deg, 75f * deg);
            double cy0 = p.Y + spec.FuselageCentreM;
            double horiz = Math.Cos(elev) * Math.Sqrt(dist * dist + height * height);
            var cam = new FlightCameraPose
            {
                X = p.X - Math.Sin(yaw) * horiz,
                Z = p.Z - Math.Cos(yaw) * horiz,
                Y = cy0 + Math.Sin(elev) * Math.Sqrt(dist * dist + height * height),
                Yaw = yaw,
                FovDeg = ChaseFovDeg,
            };
            if (world != null)
            {
                FlightGroundSample g;
                world.Ground(cam.X, cam.Z, (float)cam.Y, out g);
                if (cam.Y < g.Height + 2.5) cam.Y = g.Height + 2.5;
            }
            // Look at a point a little ahead of the aircraft's centre.
            double ahead = 0.25 * spec.Size;
            double tx = p.X + Math.Sin(p.Heading) * ahead, tz = p.Z + Math.Cos(p.Heading) * ahead, ty = cy0;
            double dx = tx - cam.X, dy = ty - cam.Y, dz = tz - cam.Z;
            cam.Yaw = (float)Math.Atan2(dx, dz);
            cam.Pitch = (float)Math.Atan2(dy, Math.Sqrt(dx * dx + dz * dz));
            cam.Roll = 0f;
            return cam;
        }

        private FlightCameraPose Cockpit(in FlightPose p, FlightSpec s)
        {
            float sh = (float)Math.Sin(p.Heading), ch = (float)Math.Cos(p.Heading), sp = (float)Math.Sin(p.Pitch), cp = (float)Math.Cos(p.Pitch);
            float sb = (float)Math.Sin(p.Bank), cb = (float)Math.Cos(p.Bank);
            float fx = sh * cp, fy = sp, fz = ch * cp;
            float r0x = ch, r0z = -sh;
            float u0x = -sh * sp, u0y = cp, u0z = -ch * sp;
            float rx = r0x * cb - u0x * sb, ry = -u0y * sb, rz = r0z * cb - u0z * sb;
            float ux = u0x * cb + r0x * sb, uy = u0y * cb, uz = u0z * cb + r0z * sb;
            return new FlightCameraPose
            {
                X = p.X + rx * s.EyeX + ux * s.EyeY + fx * s.EyeZ,
                Y = p.Y + ry * s.EyeX + uy * s.EyeY + fy * s.EyeZ,
                Z = p.Z + rz * s.EyeX + uz * s.EyeY + fz * s.EyeZ,
                Yaw = Wrap(p.Heading + _lookYaw),
                Pitch = Clamp(p.Pitch - 4f * (float)(Math.PI / 180.0) - _lookPitch, -1.4f, 1.4f),
                Roll = p.Bank,
                FovDeg = CockpitFovDeg,
            };
        }

        private static float Wrap(float a)
        {
            return ArcadeVehicle.WrapAngle(a);
        }

        private static float Clamp(float v, float a, float b)
        {
            return v < a ? a : v > b ? b : v;
        }
    }
}
