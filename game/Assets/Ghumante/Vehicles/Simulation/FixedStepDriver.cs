using System;
using Ghumante.Core.Driving;

namespace Ghumante.Vehicles
{
    /// <summary>
    /// One rendered snapshot of an <see cref="ArcadeVehicle"/>: what a visual model needs. Game metres (X east, Z north,
    /// Y absolute), angles in radians with <see cref="ArcadeVehicle"/>'s conventions (heading 0 = north, clockwise;
    /// pitch nose-up, roll and lean right-side-down positive).
    /// </summary>
    public struct VehiclePose
    {
        public double X, Z;
        public float Y;
        public float HeadingRad, Pitch, Roll, Lean;

        /// <summary>Signed speed along the direction of travel, m/s.</summary>
        public float SpeedMps;

        /// <summary>Front-wheel angle, radians, positive right.</summary>
        public float SteerRad;

        /// <summary>Rolling angle of the wheels in [−π, π): advances by distance / wheel radius.</summary>
        public float WheelAngleRad;

        /// <summary>Distance travelled since the driver was created (drives walk cycles).</summary>
        public double OdometerM;

        public bool Airborne;
        public bool HasGround;

        /// <summary>Visual body roll and pitch from the accelerations (<see cref="ArcadeVehicle.VisualRoll"/>), radians,
        /// drawn on top of <see cref="Roll"/> and <see cref="Pitch"/>.</summary>
        public float VisualRoll, VisualPitch;

        /// <summary>A two-wheeler's foot is down (<see cref="ArcadeVehicle.FootDown"/>).</summary>
        public bool FootDown;

        /// <summary>What the feet or tyres touch, after wetness (<see cref="ArcadeVehicle.Foot"/>).</summary>
        public FootSurface Foot;

        /// <summary>
        /// Interpolation between two fixed steps: positions and the odometer linearly, angles (heading, wheel) along the
        /// shorter arc, flags from <paramref name="b"/>. <paramref name="t"/> is clamped to [0, 1].
        /// </summary>
        public static VehiclePose Lerp(in VehiclePose a, in VehiclePose b, float t)
        {
            if (!(t > 0f)) t = 0f;
            else if (t > 1f) t = 1f;
            VehiclePose p;
            p.X = a.X + (b.X - a.X) * t;
            p.Z = a.Z + (b.Z - a.Z) * t;
            p.Y = a.Y + (b.Y - a.Y) * t;
            p.HeadingRad = ArcadeVehicle.WrapAngle(a.HeadingRad + ArcadeVehicle.WrapAngle(b.HeadingRad - a.HeadingRad) * t);
            p.Pitch = a.Pitch + (b.Pitch - a.Pitch) * t;
            p.Roll = a.Roll + (b.Roll - a.Roll) * t;
            p.Lean = a.Lean + (b.Lean - a.Lean) * t;
            p.SpeedMps = a.SpeedMps + (b.SpeedMps - a.SpeedMps) * t;
            p.SteerRad = a.SteerRad + (b.SteerRad - a.SteerRad) * t;
            p.WheelAngleRad = ArcadeVehicle.WrapAngle(a.WheelAngleRad + ArcadeVehicle.WrapAngle(b.WheelAngleRad - a.WheelAngleRad) * t);
            p.OdometerM = a.OdometerM + (b.OdometerM - a.OdometerM) * t;
            p.Airborne = b.Airborne;
            p.HasGround = b.HasGround;
            p.VisualRoll = a.VisualRoll + (b.VisualRoll - a.VisualRoll) * t;
            p.VisualPitch = a.VisualPitch + (b.VisualPitch - a.VisualPitch) * t;
            p.FootDown = b.FootDown;
            p.Foot = b.Foot;
            return p;
        }
    }

    /// <summary>
    /// Runs an <see cref="ArcadeVehicle"/> at a fixed step (default 1/60 s) whatever the frame rate, and interpolates the
    /// last two steps for rendering (M1 track D, ARCHITECTURE.md 7.6). Frames longer than <see cref="MaxFrameS"/> are
    /// clamped (a hitch slows the world instead of teleporting the vehicle) and at most <see cref="MaxStepsPerFrame"/>
    /// steps run per frame. The same inputs and frame times always give the same trajectory, and 30 fps and 60 fps
    /// give the same trajectory at step boundaries. Engine-free; steps do not allocate.
    /// </summary>
    public sealed class FixedStepDriver
    {
        public const float DefaultStepS = 1f / 60f;
        public const float MaxFrameS = 0.25f;
        public const int MaxStepsPerFrame = 16;

        private readonly ArcadeVehicle _vehicle;
        private readonly float _step;
        private readonly float _wheelRadius;
        private VehiclePose _previous;
        private VehiclePose _current;
        private float _accumulator;

        /// <param name="vehicle">The simulated vehicle (or walker).</param>
        /// <param name="stepSeconds">Fixed step; must be in (0, <see cref="MaxFrameS"/>].</param>
        /// <param name="wheelRadiusM">Radius used for <see cref="VehiclePose.WheelAngleRad"/>.</param>
        public FixedStepDriver(ArcadeVehicle vehicle, float stepSeconds = DefaultStepS, float wheelRadiusM = VehicleTuning.ScooterWheelRadiusM)
        {
            _vehicle = vehicle ?? throw new ArgumentNullException(nameof(vehicle));
            if (!(stepSeconds > 0f) || stepSeconds > MaxFrameS) throw new ArgumentOutOfRangeException(nameof(stepSeconds));
            if (!(wheelRadiusM > 0f) || float.IsInfinity(wheelRadiusM)) throw new ArgumentOutOfRangeException(nameof(wheelRadiusM));
            _step = stepSeconds;
            _wheelRadius = wheelRadiusM;
            Capture(ref _current, _current, false);
            _previous = _current;
        }

        public ArcadeVehicle Vehicle
        {
            get { return _vehicle; }
        }

        public float StepSeconds
        {
            get { return _step; }
        }

        /// <summary>Simulated time not yet stepped, in [0, <see cref="StepSeconds"/>).</summary>
        public float Accumulator
        {
            get { return _accumulator; }
        }

        /// <summary>Interpolation factor between <see cref="Previous"/> and <see cref="Current"/>.</summary>
        public float Alpha
        {
            get { return _accumulator / _step; }
        }

        /// <summary>Fixed steps taken since construction.</summary>
        public long Steps { get; private set; }

        /// <summary>The state before the last step.</summary>
        public VehiclePose Previous
        {
            get { return _previous; }
        }

        /// <summary>The state after the last step.</summary>
        public VehiclePose Current
        {
            get { return _current; }
        }

        /// <summary>The pose to draw this frame: <see cref="Previous"/> to <see cref="Current"/> at <see cref="Alpha"/>.</summary>
        public VehiclePose Interpolated
        {
            get { return VehiclePose.Lerp(_previous, _current, Alpha); }
        }

        /// <summary>
        /// Adds <paramref name="frameSeconds"/> of time and runs every fixed step that is due with the same input.
        /// Returns the events of all those steps together.
        /// </summary>
        public StepEvents Advance(float frameSeconds, in DriveInput input, IGroundQuery ground, float wetness01)
        {
            if (ground == null) throw new ArgumentNullException(nameof(ground));
            if (!(frameSeconds > 0f) || float.IsInfinity(frameSeconds)) return StepEvents.None;
            if (frameSeconds > MaxFrameS) frameSeconds = MaxFrameS;
            _accumulator += frameSeconds;
            StepEvents events = StepEvents.None;
            int steps = 0;
            while (_accumulator >= _step)
            {
                if (steps == MaxStepsPerFrame)
                {
                    _accumulator = 0f; // drop the backlog rather than spiral
                    break;
                }
                _previous = _current;
                events |= _vehicle.Step(input, _step, ground, wetness01);
                Capture(ref _current, _previous, true);
                _accumulator -= _step;
                steps++;
                Steps++;
            }
            return events;
        }

        /// <summary>Places the vehicle at rest (see <see cref="ArcadeVehicle.Teleport"/>) with no interpolation from
        /// where it was.</summary>
        public void Teleport(double x, double z, float headingRad, IGroundQuery ground)
        {
            _vehicle.Teleport(x, z, headingRad, ground);
            Snap();
        }

        /// <summary>Automatic or manual stuck recovery (<see cref="ArcadeVehicle.Recover"/>); the hop then plays out over
        /// the following steps.</summary>
        public bool Recover(IGroundQuery ground)
        {
            return _vehicle.Recover(ground);
        }

        /// <summary>Re-reads the vehicle after it was moved from outside and drops the interpolation history.</summary>
        public void Snap()
        {
            Capture(ref _current, _current, false);
            _previous = _current;
            _accumulator = 0f;
        }

        private void Capture(ref VehiclePose pose, in VehiclePose before, bool advanced)
        {
            ArcadeVehicle v = _vehicle;
            float wheel = before.WheelAngleRad;
            double odometer = before.OdometerM;
            if (advanced)
            {
                double dx = v.X - before.X, dz = v.Z - before.Z;
                double moved = Math.Sqrt(dx * dx + dz * dz);
                if (moved < 50.0) odometer += moved; // a recovery hop of a few metres counts, a teleport does not
                wheel = ArcadeVehicle.WrapAngle(wheel + v.SpeedMps * _step / _wheelRadius);
            }
            pose.X = v.X;
            pose.Z = v.Z;
            pose.Y = v.Y;
            pose.HeadingRad = v.HeadingRad;
            pose.Pitch = v.Pitch;
            pose.Roll = v.Roll;
            pose.Lean = v.Lean;
            pose.SpeedMps = v.SpeedMps;
            pose.SteerRad = v.SteerAngleRad;
            pose.WheelAngleRad = wheel;
            pose.OdometerM = odometer;
            pose.Airborne = v.Airborne;
            pose.HasGround = v.HasGround;
            pose.VisualRoll = v.VisualRoll;
            pose.VisualPitch = v.VisualPitch;
            pose.FootDown = v.FootDown;
            pose.Foot = v.Foot;
        }
    }
}
