using System;

namespace Ghumante.Core.Driving
{
    /// <summary>
    /// The pilot's controls for one frame. Sticks in [−1, 1]: <see cref="Pitch"/> positive pulls the nose up,
    /// <see cref="Roll"/> positive banks right (right wing down), <see cref="Yaw"/> positive yaws right (rudder, and the
    /// nose wheel on the ground). <see cref="Throttle"/> is the lever position in [0, 1] (the helicopter's collective).
    /// <see cref="Brake"/> in [0, 1] (wheel brakes). Flap stage changes are one-shot per frame.
    /// </summary>
    public struct FlightInput
    {
        public float Pitch, Roll, Yaw, Throttle, Brake;

        /// <summary>Retract (up) or extend (down) the flaps by one stage this frame.</summary>
        public bool FlapsUp, FlapsDown;

        public FlightInput(float pitch, float roll, float yaw, float throttle, float brake = 0f)
        {
            Pitch = pitch;
            Roll = roll;
            Yaw = yaw;
            Throttle = throttle;
            Brake = brake;
            FlapsUp = false;
            FlapsDown = false;
        }
    }

    /// <summary>What happened during a step (several can happen in one frame).</summary>
    [Flags]
    public enum FlightEvents : ushort
    {
        None = 0,
        LiftOff = 1,
        Touchdown = 2,

        /// <summary>Touched terrain, water or a building in a way that is not a landing: the soft bounce began.</summary>
        Bounce = 4,

        /// <summary>After the bounce: back on the nearest runway, lined up.</summary>
        ResetRunway = 8,

        /// <summary>After the bounce: back up at a safe altitude, heading for the airport.</summary>
        ResetAir = 16,

        /// <summary>Entered the turn-back band at the edge of the region.</summary>
        TurnBack = 32,

        StallWarning = 64,
        Stall = 128,
        GearUp = 256,
        GearDown = 512,
        Flaps = 1024,

        /// <summary>A firm landing that bounced back into the air, or a taxi nudge against an obstacle.</summary>
        Bump = 2048,
    }

    /// <summary>Why the last bounce happened (for the friendly toast).</summary>
    public enum FlightBounceCause : byte
    {
        None = 0,
        Terrain = 1,
        Building = 2,
        Water = 3,

        /// <summary>A landing too steep, too fast or too banked.</summary>
        HardLanding = 4,
    }

    /// <summary>One pose of the aircraft: game metres (X east, Z north, Y absolute up at the wheels' contact point under
    /// the fuselage centre), heading (0 north, clockwise), pitch (nose up +), bank (right wing down +), airspeed.</summary>
    public struct FlightPose
    {
        public double X, Y, Z;
        public float Heading, Pitch, Bank, Speed;

        public static FlightPose Lerp(in FlightPose a, in FlightPose b, float t)
        {
            return new FlightPose
            {
                X = a.X + (b.X - a.X) * t, Y = a.Y + (b.Y - a.Y) * t, Z = a.Z + (b.Z - a.Z) * t,
                Heading = a.Heading + ArcadeVehicle.WrapAngle(b.Heading - a.Heading) * t, Pitch = a.Pitch + (b.Pitch - a.Pitch) * t,
                Bank = a.Bank + (b.Bank - a.Bank) * t, Speed = a.Speed + (b.Speed - a.Speed) * t,
            };
        }
    }

    /// <summary>The instrument readings the HUD shows.</summary>
    public struct FlightInstruments
    {
        public FlightCraft Craft;
        public float AirspeedKt, GroundSpeedMps, AltitudeM, AglM, VerticalSpeedMps, HeadingDeg, PitchDeg, BankDeg;
        public float Throttle, Power, GearDown, RotorRpm;
        public int Flaps;
        public bool OnGround, StallWarning, Stalled, TurningBack, Bouncing, CanExit;

        /// <summary>Speeds for the HUD's speed tape (knots): stall with the current flaps and rotate.</summary>
        public float StallKt, RotateKt;
    }

    /// <summary>
    /// Arcade but believable flight (docs/research/w2/ref_flight.md §6) for the aircraft the player boards at TIA. One fixed
    /// step of <see cref="StepS"/>, deterministic, no allocation.
    /// <list type="bullet">
    /// <item><b>Ground</b>: thrust from the turboprops (static thrust, then power × efficiency / speed, thinner air with
    /// altitude), rolling friction (runway and paved 0.02, grass 0.06), wheel brakes and a parking hold at idle, nose-wheel
    /// steering (60° at walking pace, narrowing with speed; the roll stick steers too while taxiing) blending into the
    /// rudder, rotation at <see cref="FlightSpec.RotateMps"/> limited by the tail-strike attitude, lift-off when lift
    /// exceeds weight.</item>
    /// <item><b>Air</b>: lift from the angle of attack (lift slope, flap stages that raise it, a soft stall that keeps
    /// half the lift), drag (parasite, gear, flaps, induced), ground effect, side force that kills sideslip and a yaw
    /// that follows the flight path (coordinated turns: bank and the lift turns the aircraft), pitch and roll rates from
    /// the stick. Hands off, the nose holds the flight path (auto-trim) and the wings level slowly. Stall warning past 85%
    /// of the stall angle or below 1.12 × stall speed; in a stall the nose drops gently and the stick cannot pull further
    /// (gentle recovery). Retractable gear goes up above 60 m climbing and comes down below 180 m descending or slow.</item>
    /// <item><b>Landing</b>: a touchdown at ≤ 4 m/s sink, ≤ 15° bank (the wings level themselves below 15 m), a sane attitude and speed on the runway, paved or
    /// open level ground is a landing (with the flare helped by ground effect); up to 6.5 m/s it bounces back up (a
    /// "bump"); anything harder, water, a hillside, a building or a wingtip in the ground starts the <b>soft bounce</b>
    /// (never a crash): the aircraft hops up and slows for <see cref="BounceS"/>, then is reset to the nearest runway
    /// (within <see cref="ResetRunwayRadiusM"/> or when slow) or to a safe altitude above the ground, heading for the
    /// airport.</item>
    /// <item><b>Bounds</b>: past the turn-back band the bank is steered towards the region's centre, and the hard edge
    /// and the ceiling cannot be crossed.</item>
    /// <item><b>Helicopter</b>: collective (the throttle) against hover power, cyclic attitude targets that tilt the
    /// rotor thrust, tail-rotor yaw, quadratic drag to its top speed, skids that land anywhere level.</item>
    /// </list>
    /// </summary>
    public sealed class FlightModel
    {
        public const float StepS = 1f / 60f;

        /// <summary>Length of the soft bounce before the reset (s).</summary>
        public const float BounceS = 1.3f;

        /// <summary>Within this distance of the runway (or slower than the stall) a reset puts the aircraft back on it.</summary>
        public const float ResetRunwayRadiusM = 3500f;

        /// <summary>Height above the highest nearby ground of an air reset (m).</summary>
        public const float ResetAglM = 350f;

        /// <summary>Landing limits: sink rate (m/s) for a landing and for a bounced "bump", bank (rad), slope of open ground.</summary>
        public const float LandSinkMps = 4f, BumpSinkMps = 6.5f, LandBank = 0.26f, LandSlope = 0.11f;

        /// <summary>Wing incidence on the ground (rad): the angle of attack at zero attitude.</summary>
        public const float Incidence = 0.035f;

        /// <summary>Below this height above ground the model sweeps for buildings (m).</summary>
        public const float SolidCheckAglM = 120f;

        private const float Deg = (float)(Math.PI / 180.0);

        private readonly FlightSpec _s;
        private IFlightWorld _world;
        private double _x, _y, _z;
        private double _vx, _vy, _vz;
        private float _heading, _pitch, _bank;
        private float _groundSpeed, _rotation;
        private float _throttle, _power, _gear = 1f, _rotor;
        private int _flaps;
        private bool _onGround = true, _bouncing, _stalled, _stallWarn, _turningBack, _engineRunning;
        private float _bounceT, _bounceSpeed;
        private float _accumulator;
        private FlightPose _prev, _cur;
        private float _alpha01;
        private float _lastSinkMps;
        private FlightBounceCause _cause;
        private float _agl;
        private FlightSurfaceKind _surface;
        private int _resets;

        /// <summary>Region bounds and ceiling (disabled by default).</summary>
        public FlightBounds Bounds;

        public FlightModel(FlightSpec spec, IFlightWorld world)
        {
            _s = spec ?? throw new ArgumentNullException(nameof(spec));
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public FlightSpec Spec
        {
            get { return _s; }
        }

        public IFlightWorld World
        {
            get { return _world; }
            set { _world = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        // ---------------------------------------------------------------------------------------------------- state

        public double X
        {
            get { return _x; }
        }

        public double Y
        {
            get { return _y; }
        }

        public double Z
        {
            get { return _z; }
        }

        public float Heading
        {
            get { return _heading; }
        }

        public float Pitch
        {
            get { return _pitch; }
        }

        public float Bank
        {
            get { return _bank; }
        }

        /// <summary>World velocity (m/s).</summary>
        public double Vx
        {
            get { return _vx; }
        }

        public double Vy
        {
            get { return _vy; }
        }

        public double Vz
        {
            get { return _vz; }
        }

        /// <summary>True airspeed (m/s; no wind).</summary>
        public float Airspeed
        {
            get { return (float)Math.Sqrt(_vx * _vx + _vy * _vy + _vz * _vz); }
        }

        public bool OnGround
        {
            get { return _onGround && !_bouncing; }
        }

        public bool Bouncing
        {
            get { return _bouncing; }
        }

        public bool Stalled
        {
            get { return _stalled; }
        }

        public bool StallWarning
        {
            get { return _stallWarn; }
        }

        public bool TurningBack
        {
            get { return _turningBack; }
        }

        /// <summary>The throttle lever (0–1) as last set, and the engines' actual power (spools after the lever).</summary>
        public float Throttle
        {
            get { return _throttle; }
        }

        public float Power
        {
            get { return _power; }
        }

        /// <summary>Flap stage 0 (up), 1 (take-off / approach), 2 (landing).</summary>
        public int Flaps
        {
            get { return _flaps; }
        }

        /// <summary>Gear extension 0 (up) – 1 (down).</summary>
        public float Gear
        {
            get { return _gear; }
        }

        /// <summary>Rotor speed 0–1 (helicopter; propellers use <see cref="Power"/>).</summary>
        public float RotorRpm
        {
            get { return _rotor; }
        }

        /// <summary>Height of the wheels above the ground under them (m).</summary>
        public float Agl
        {
            get { return _agl; }
        }

        /// <summary>What the aircraft is over.</summary>
        public FlightSurfaceKind Surface
        {
            get { return _surface; }
        }

        /// <summary>Sink rate of the last touchdown (m/s).</summary>
        public float LastTouchdownSinkMps
        {
            get { return _lastSinkMps; }
        }

        /// <summary>Why the last bounce began.</summary>
        public FlightBounceCause LastBounceCause
        {
            get { return _cause; }
        }

        /// <summary>Resets so far (diagnostics, tests).</summary>
        public int ResetCount
        {
            get { return _resets; }
        }

        /// <summary>The engines run (started when the pilot boards; propellers turn, the rotor spools).</summary>
        public bool EngineRunning
        {
            get { return _engineRunning; }
            set { _engineRunning = value; }
        }

        /// <summary>Standing still on the ground (the pilot may get out).</summary>
        public bool CanExit
        {
            get { return OnGround && Math.Abs(_groundSpeed) < 0.6f && Airspeed < 0.8f; }
        }

        /// <summary>The pose after the last step, the one before it, and the render pose between them.</summary>
        public FlightPose Current
        {
            get { return _cur; }
        }

        public FlightPose Previous
        {
            get { return _prev; }
        }

        public FlightPose Interpolated
        {
            get { return FlightPose.Lerp(_prev, _cur, _alpha01); }
        }

        // ---------------------------------------------------------------------------------------------- placement

        /// <summary>Puts the aircraft at rest on the ground at (x, z) facing <paramref name="headingRad"/> (engines idle,
        /// brakes on, flaps for take-off).</summary>
        public void PlaceOnGround(double x, double z, float headingRad)
        {
            FlightGroundSample g;
            _world.Ground(x, z, 1e6f, out g);
            _x = x;
            _z = z;
            _y = g.Height;
            _heading = ArcadeVehicle.WrapAngle(headingRad);
            _bank = 0f;
            _rotation = 0f;
            _pitch = GroundPitch(g, _heading);
            _vx = _vy = _vz = 0;
            _groundSpeed = 0f;
            _throttle = 0f;
            _power = 0f;
            _flaps = _s.Rotorcraft ? 0 : 1;
            _gear = 1f;
            _onGround = true;
            _bouncing = false;
            _stalled = _stallWarn = _turningBack = false;
            _surface = g.Kind;
            _agl = 0f;
            _accumulator = 0f;
            Snap();
        }

        /// <summary>Puts the aircraft in level flight at (x, y, z) heading <paramref name="headingRad"/> at
        /// <paramref name="speedMps"/>, power for level flight, gear up, flaps up.</summary>
        public void PlaceInAir(double x, double y, double z, float headingRad, float speedMps)
        {
            _x = x;
            _y = y;
            _z = z;
            _heading = ArcadeVehicle.WrapAngle(headingRad);
            _bank = 0f;
            _rotation = 0f;
            _onGround = false;
            _bouncing = false;
            _stalled = _stallWarn = false;
            _flaps = 0;
            _gear = _s.FixedGear ? 1f : 0f;
            _engineRunning = true;
            if (_s.Rotorcraft)
            {
                _rotor = 1f;
                _throttle = _power = HoverCollective;
                _pitch = 0f;
                _vx = Math.Sin(_heading) * speedMps;
                _vz = Math.Cos(_heading) * speedMps;
                _vy = 0;
            }
            else
            {
                float rho = FlightSpec.Density((float)y);
                float q = 0.5f * rho * speedMps * speedMps;
                float cl = _s.WeightN / Math.Max(1f, q * _s.WingAreaM2);
                float alpha = (cl - _s.Cl0(0)) / FlightSpec.ClAlpha;
                _pitch = Clamp(alpha, -0.05f, 0.25f);
                _vx = Math.Sin(_heading) * speedMps;
                _vz = Math.Cos(_heading) * speedMps;
                _vy = 0;
                float drag = q * _s.WingAreaM2 * (_s.Cd0 + _s.InducedK * cl * cl);
                _throttle = _power = Clamp(drag / Math.Max(1f, MaxThrust(speedMps, rho)), 0.2f, 1f);
            }
            _groundSpeed = 0f;
            _accumulator = 0f;
            _holdingPath = false;
            UpdateAgl();
            Snap();
        }

        /// <summary>Snaps the render pose to the current one (no interpolation from the old place).</summary>
        public void Snap()
        {
            _cur = PoseNow();
            _prev = _cur;
            _alpha01 = 0f;
        }

        // ------------------------------------------------------------------------------------------------- stepping

        /// <summary>
        /// Advances by <paramref name="dt"/> seconds (split into fixed steps; the remainder interpolates the render pose)
        /// with <paramref name="input"/>; returns everything that happened.
        /// </summary>
        public FlightEvents Advance(float dt, in FlightInput input)
        {
            if (!(dt > 0f) || float.IsInfinity(dt)) return FlightEvents.None;
            dt = Math.Min(dt, 0.25f);
            FlightEvents ev = FlightEvents.None;
            if (!_s.Rotorcraft && !_bouncing)
            {
                int before = _flaps;
                if (input.FlapsDown && _flaps < 2) _flaps++;
                if (input.FlapsUp && _flaps > 0) _flaps--;
                if (_flaps != before) ev |= FlightEvents.Flaps;
            }
            _accumulator += dt;
            while (_accumulator >= StepS)
            {
                _accumulator -= StepS;
                _prev = _cur;
                ev |= Step(StepS, input);
                _cur = PoseNow();
            }
            _alpha01 = _accumulator / StepS;
            return ev;
        }

        private FlightEvents Step(float dt, in FlightInput input)
        {
            _throttle = Clamp(Finite(input.Throttle), 0f, 1f);
            float target = _engineRunning ? _throttle : 0f;
            // Turboprops spool; the helicopter's collective follows the lever quickly (its rotor spools instead).
            float spool = dt / (_s.Rotorcraft ? 0.4f : Math.Max(0.2f, _s.SpoolS));
            _power += Clamp(target - _power, -spool, spool);
            if (_s.Rotorcraft) _rotor = Clamp(_rotor + (_engineRunning ? dt : -dt) / Math.Max(0.5f, _s.SpoolS), 0f, 1f);
            if (_bouncing) return StepBounce(dt);
            FlightInput c = Sanitize(input);
            if (_s.Rotorcraft) return StepHeli(dt, c);
            return _onGround ? StepGround(dt, c) : StepAir(dt, c);
        }

        private static FlightInput Sanitize(in FlightInput i)
        {
            return new FlightInput(Clamp(Finite(i.Pitch), -1f, 1f), Clamp(Finite(i.Roll), -1f, 1f), Clamp(Finite(i.Yaw), -1f, 1f),
                                   Clamp(Finite(i.Throttle), 0f, 1f), Clamp(Finite(i.Brake), 0f, 1f));
        }

        // ------------------------------------------------------------------------------------------ fixed wing: ground

        private FlightEvents StepGround(float dt, in FlightInput c)
        {
            FlightEvents ev = FlightEvents.None;
            float rho = FlightSpec.Density((float)_y);
            float u = _groundSpeed;
            float q = 0.5f * rho * u * u;
            float alpha = _rotation + Incidence;
            float cl = Math.Min(_s.ClMax(_flaps), _s.Cl0(_flaps) + FlightSpec.ClAlpha * alpha);
            float lift = q * _s.WingAreaM2 * cl;
            float drag = q * _s.WingAreaM2 * (Cd(cl));
            float thrust = Thrust(u, rho);
            float weight = _s.WeightN;
            float normal = Math.Max(0f, weight - lift);
            FlightGroundSample g;
            _world.Ground(_x, _z, (float)_y + 2f, out g);
            float mu = g.Kind == FlightSurfaceKind.Runway || g.Kind == FlightSurfaceKind.Paved || g.Kind == FlightSurfaceKind.Structure ? 0.02f : 0.06f;
            float brake = c.Brake;
            if (_throttle < 0.08f && u < 2.5f) brake = 1f; // parking hold at idle
            float slopeForce = -weight * (float)Math.Sin(GroundPitch(g, _heading));
            float resist = mu * normal + brake * 0.45f * normal;
            float drive = thrust - drag + slopeForce;
            float du;
            if (u > 0.05f) du = (drive - resist) / _s.MassKg;
            else du = drive > resist ? (drive - resist) / _s.MassKg : 0f;
            u = Math.Max(0f, u + du * dt);

            // Nose-wheel steering (the roll stick steers while taxiing), blending into the rudder at speed.
            float rollShare = u < 15f ? 1f : u > 40f ? 0.2f : 1f - 0.8f * (u - 15f) / 25f;
            float steer = Clamp(c.Yaw + c.Roll * rollShare, -1f, 1f);
            float lockScale = Clamp(1f - (u - 4f) / 30f, 0.12f, 1f);
            float delta = steer * _s.NoseWheelLock * lockScale;
            float yawRate = u * (float)Math.Tan(delta) / Math.Max(1f, _s.WheelbaseM);
            yawRate += c.Yaw * _s.RudderRate * Clamp((u - 15f) / 30f, 0f, 1f);
            // A slow pivot on the spot with power and full steering (turboprops turn round on the apron).
            if (u < 0.6f && Math.Abs(steer) > 0.6f && _power > 0.1f) yawRate += Math.Sign(steer) * 12f * Deg * Math.Min(1f, _power * 3f);
            _heading = ArcadeVehicle.WrapAngle(_heading + yawRate * dt);

            // Rotation: pull at rotate speed raises the nose up to the tail-strike attitude.
            if (c.Pitch > 0.15f && u > 0.85f * _s.RotateMps) _rotation += 3.5f * Deg * Math.Min(1f, c.Pitch) * dt;
            else _rotation -= 3f * Deg * dt;
            _rotation = Clamp(_rotation, 0f, _s.TailStrike);

            double nx = _x + Math.Sin(_heading) * u * dt, nz = _z + Math.Cos(_heading) * u * dt;
            // Taxiing into a building or a wall stops the aircraft with a nudge (no damage); at speed it is a bounce.
            float r = _s.FuselageRadiusM;
            float t;
            FlightObstacle hit;
            double cy = _y + _s.FuselageCentreM + 0.4;
            double fx = Math.Sin(_heading) * _s.LengthM * 0.45, fz = Math.Cos(_heading) * _s.LengthM * 0.45;
            if (u > 0.05f && _world.Sweep(_x + fx, cy, _z + fz, nx + fx, cy, nz + fz, r, out t, out hit) && hit == FlightObstacle.Structure)
            {
                if (u > 15f)
                {
                    _groundSpeed = u;
                    SetVelocityAlongHeading(u);
                    return ev | BeginBounce(FlightBounceCause.Building);
                }
                nx = _x - Math.Sin(_heading) * 1.0;
                nz = _z - Math.Cos(_heading) * 1.0;
                u = 0f;
                ev |= FlightEvents.Bump;
            }
            _x = nx;
            _z = nz;
            _world.Ground(_x, _z, (float)_y + 2f, out g);
            _y = g.Height;
            _surface = g.Kind;
            _agl = 0f;
            _pitch = GroundPitch(g, _heading) + _rotation;
            _bank *= (float)Math.Exp(-6f * dt);
            _groundSpeed = u;
            SetVelocityAlongHeading(u);
            _stalled = false;
            _stallWarn = false;
            ClampToBounds(ref ev);
            // Lift-off: more lift than weight.
            if (lift > weight && u > 0.8f * _s.RotateMps)
            {
                _onGround = false;
                _vy = Math.Max(_vy, 0.6);
                _y += 0.05;
                ev |= FlightEvents.LiftOff;
            }
            return ev;
        }

        private void SetVelocityAlongHeading(float u)
        {
            float cp = (float)Math.Cos(_pitch - _rotation);
            _vx = Math.Sin(_heading) * u * cp;
            _vz = Math.Cos(_heading) * u * cp;
            _vy = Math.Sin(_pitch - _rotation) * u;
        }

        // --------------------------------------------------------------------------------------------- fixed wing: air

        private FlightEvents StepAir(float dt, FlightInput c)
        {
            FlightEvents ev = FlightEvents.None;
            float rho = FlightSpec.Density((float)_y);
            double vx = _vx, vy = _vy, vz = _vz;
            double V = Math.Sqrt(vx * vx + vy * vy + vz * vz);
            if (V < 1.0)
            {
                // Practically stopped in the air (a reset can never do this, a test might): fall along the nose.
                vx = Math.Sin(_heading);
                vz = Math.Cos(_heading);
                vy = 0;
                V = 1.0;
                _vx = vx;
                _vz = vz;
                _vy = 0;
            }
            double ux = vx / V, uy = vy / V, uz = vz / V;

            // Body axes (yaw → pitch → roll).
            float sh = (float)Math.Sin(_heading), ch = (float)Math.Cos(_heading), sp = (float)Math.Sin(_pitch), cp = (float)Math.Cos(_pitch);
            float sb = (float)Math.Sin(_bank), cb = (float)Math.Cos(_bank);
            float fx = sh * cp, fy = sp, fz = ch * cp;
            float r0x = ch, r0z = -sh;
            float u0x = -sh * sp, u0y = cp, u0z = -ch * sp;
            float rx = r0x * cb - u0x * sb, ry = -u0y * sb, rz = r0z * cb - u0z * sb;
            float upx = u0x * cb + r0x * sb, upy = u0y * cb, upz = u0z * cb + r0z * sb;

            double vf = vx * fx + vy * fy + vz * fz, vu = vx * upx + vy * upy + vz * upz, vr = vx * rx + vy * ry + vz * rz;
            float alpha = (float)Math.Atan2(-vu, Math.Max(0.5, vf)) + Incidence;
            float beta = (float)Math.Atan2(vr, Math.Max(0.5, vf));

            // Turn back at the edge of the region and stay under the ceiling.
            bool wasBack = _turningBack;
            ApplyBounds(ref c);
            if (_turningBack && !wasBack) ev |= FlightEvents.TurnBack;

            // Lift with a soft stall.
            float stallA = _s.StallAlpha(_flaps);
            float cl = _s.Cl0(_flaps) + FlightSpec.ClAlpha * alpha;
            if (alpha > stallA) cl = _s.ClMax(_flaps) * (1f - 0.5f * Math.Min(1f, (alpha - stallA) / 0.15f));
            cl = Math.Max(-0.9f, cl);
            float halfSpan = 0.5f * _s.SpanM;
            float groundEffect = 1f + 0.25f * (float)Math.Exp(-Math.Max(0f, _agl) / halfSpan);
            float q = 0.5f * rho * (float)(V * V);
            float lift = q * _s.WingAreaM2 * cl * groundEffect;
            float drag = q * _s.WingAreaM2 * Cd(cl);
            float side = -q * _s.WingAreaM2 * 0.9f * beta;
            float thrust = Thrust((float)V, rho);

            // Lift acts perpendicular to the velocity, in the plane of the velocity and the wings' up.
            double dot = upx * ux + upy * uy + upz * uz;
            double lx = upx - dot * ux, ly = upy - dot * uy, lz = upz - dot * uz;
            double ll = Math.Sqrt(lx * lx + ly * ly + lz * lz);
            if (ll > 1e-6)
            {
                lx /= ll;
                ly /= ll;
                lz /= ll;
            }
            double m = _s.MassKg;
            double ax = (lift * lx - drag * ux + side * rx + thrust * fx) / m;
            double ay = (lift * ly - drag * uy + side * ry + thrust * fy) / m - FlightSpec.G;
            double az = (lift * lz - drag * uz + side * rz + thrust * fz) / m;
            _vx += ax * dt;
            _vy += ay * dt;
            _vz += az * dt;
            double ox = _x, oy = _y, oz = _z;
            _x += _vx * dt;
            _y += _vy * dt;
            _z += _vz * dt;

            // Attitude: stick rates, auto-trim holding the flight path hands off, gentle wing levelling, stall recovery.
            float gamma = (float)Math.Asin(Clamp((float)uy, -1f, 1f));
            _stalled = alpha > stallA;
            float warnSpeed = 1.12f * _s.StallSpeed((float)_y, _flaps);
            bool warn = alpha > 0.85f * stallA || V < warnSpeed;
            if (warn && !_stallWarn) ev |= FlightEvents.StallWarning;
            if (_stalled && !_stallWarnStalledLatch) ev |= FlightEvents.Stall;
            _stallWarnStalledLatch = _stalled;
            _stallWarn = warn;
            float pitchIn = c.Pitch;
            if (_stalled && pitchIn > 0f) pitchIn = 0f;
            float authority = Clamp((float)V / Math.Max(1f, _s.RotateMps), 0.3f, 1f);
            if (Math.Abs(pitchIn) > 0.05f)
            {
                _pitch += pitchIn * _s.PitchRate * authority * dt;
                _holdingPath = false;
            }
            else
            {
                // Hands off: hold the flight path the stick was released on (levelling off while turning back), pulling
                // as much as a banked turn needs, so the aircraft neither balloons nor spirals down.
                if (!_holdingPath)
                {
                    _gammaHold = Clamp(gamma, -12f * Deg, 12f * Deg);
                    _holdingPath = true;
                }
                if (_turningBack) _gammaHold -= _gammaHold * Math.Min(1f, 0.6f * dt);
                float vsHold = (float)V * (float)Math.Sin(_gammaHold);
                float aV = Clamp((vsHold - (float)_vy) * 1.2f, -3f, 3f);
                float liftReq = (_s.WeightN + (float)m * aV) / Math.Max(0.35f, cb * (float)Math.Cos(gamma));
                float clReq = liftReq / Math.Max(1f, q * _s.WingAreaM2 * groundEffect);
                float alphaReq = Clamp((clReq - _s.Cl0(_flaps)) / FlightSpec.ClAlpha, -0.08f, 0.85f * stallA);
                float thetaT = _pitch + (alphaReq - alpha) / Math.Max(0.5f, cb);
                float rate = 1.5f * _s.PitchRate;
                _pitch += Clamp((thetaT - _pitch) * 3f, -rate, rate) * dt;
            }
            if (_stalled)
            {
                _pitch -= 9f * Deg * dt;
                _bank -= _bank * Math.Min(1f, 1.2f * dt);
            }
            _pitch = Clamp(_pitch, -_s.MaxPitchDown, _s.MaxPitchUp);
            if (Math.Abs(c.Roll) > 0.05f) _bank += c.Roll * _s.RollRate * dt;
            else _bank -= _bank * Math.Min(1f, 0.35f * dt);
            // Landing assist: close to the ground the wings level themselves unless the stick rolls hard.
            if (_agl < 15f && Math.Abs(c.Roll) < 0.7f) _bank -= _bank * Math.Min(1f, 2.5f * (1f - _agl / 15f) * dt);
            _bank = Clamp(_bank, -_s.MaxBank, _s.MaxBank);
            _heading = ArcadeVehicle.WrapAngle(_heading + (3f * beta + c.Yaw * _s.RudderRate) * dt);

            // Gear: up when climbing away, down when low and descending or slow.
            UpdateAgl();
            if (!_s.FixedGear)
            {
                float vs = _s.StallSpeed((float)_y, _flaps);
                bool wantDown = _agl < 180f && (_vy < -1.0 || _flaps == 2 || V < 1.15f * vs) || _agl < 30f;
                bool wantUp = _agl > 60f && _vy > 1.0 && !wantDown;
                if (wantDown && _gear < 1f)
                {
                    if (_gear <= 0f) ev |= FlightEvents.GearDown;
                    _gear = Math.Min(1f, _gear + dt / 5f);
                    if (_agl < 30f) _gear = 1f; // never a belly landing: the cartoon gear snaps down
                }
                else if (wantUp && _gear > 0f)
                {
                    if (_gear >= 1f) ev |= FlightEvents.GearUp;
                    _gear = Math.Max(0f, _gear - dt / 6f);
                }
            }

            // Buildings and hillsides along the move (only low down: nothing stands taller).
            if (_agl < SolidCheckAglM)
            {
                float t;
                FlightObstacle hit;
                double cyo = oy + _s.FuselageCentreM, cyn = _y + _s.FuselageCentreM;
                if (_world.Sweep(ox, cyo, oz, _x, cyn, _z, _s.FuselageRadiusM, out t, out hit) && hit == FlightObstacle.Structure)
                {
                    _x = ox + (_x - ox) * t;
                    _y = oy + (_y - oy) * t;
                    _z = oz + (_z - oz) * t;
                    return ev | BeginBounce(FlightBounceCause.Building);
                }
            }

            // The ground: a landing, a bump, or the soft bounce. Wingtips count too.
            FlightGroundSample g;
            _world.Ground(_x, _z, (float)_y + 3f, out g);
            _surface = g.Kind;
            if (WingtipDown(rx, ry, rz)) return ev | BeginBounce(FlightBounceCause.Terrain);
            if (_y <= g.Height) ev |= Contact(g, (float)V);
            ClampToBounds(ref ev);
            return ev;
        }

        private bool _stallWarnStalledLatch;
        private bool _holdingPath;
        private float _gammaHold;

        private FlightEvents Contact(in FlightGroundSample g, float speed)
        {
            float sink = (float)Math.Max(0.0, -_vy);
            float groundPitch = GroundPitch(g, _heading);
            float attitude = _pitch - groundPitch;
            bool level = g.Kind == FlightSurfaceKind.Runway || g.Kind == FlightSurfaceKind.Paved ||
                         g.Kind == FlightSurfaceKind.Open && g.Slope <= LandSlope || g.Kind == FlightSurfaceKind.Structure && g.Slope <= LandSlope;
            bool sane = Math.Abs(_bank) <= LandBank && attitude > -4f * Deg && attitude < 16f * Deg &&
                        HorizontalSpeed() <= 1.8f * Math.Max(1f, _s.StallSpeed((float)_y, 0));
            _y = g.Height;
            if (g.Kind == FlightSurfaceKind.Water) return BeginBounce(FlightBounceCause.Water);
            if (!level || !sane || sink > BumpSinkMps) return BeginBounce(level ? FlightBounceCause.HardLanding : FlightBounceCause.Terrain);
            if (sink > LandSinkMps)
            {
                // A firm arrival bounces back up softly: still flying, try again.
                _vy = 0.3f * sink;
                _y = g.Height + 0.05;
                _lastSinkMps = sink;
                return FlightEvents.Bump;
            }
            // Touchdown: the wheels take the weight; any crab is taken out, the nose comes down on its own.
            _lastSinkMps = sink;
            _onGround = true;
            float u = HorizontalSpeed();
            double hx = _vx, hz = _vz;
            if (u > 0.5f) _heading = ArcadeVehicle.WrapAngle(_heading + 0.5f * ArcadeVehicle.WrapAngle((float)Math.Atan2(hx, hz) - _heading));
            _groundSpeed = u;
            _rotation = Clamp(attitude, 0f, _s.TailStrike);
            _bank = 0f;
            _stalled = _stallWarn = false;
            _gear = 1f;
            _agl = 0f;
            SetVelocityAlongHeading(u);
            return FlightEvents.Touchdown;
        }

        private bool WingtipDown(float rx, float ry, float rz)
        {
            if (_agl > 0.5f * _s.SpanM + 5f) return false;
            float half = 0.5f * _s.SpanM * 0.95f;
            float wingY = _s.Craft == FlightCraft.Commuter ? 1.0f : _s.FuselageCentreM + _s.FuselageRadiusM * 0.9f;
            for (int side = -1; side <= 1; side += 2)
            {
                double tx = _x + rx * half * side, ty = _y + wingY + ry * half * side, tz = _z + rz * half * side;
                FlightGroundSample g;
                _world.Ground(tx, tz, (float)ty, out g);
                if (ty < g.Height + 0.1) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------------------------------------ helicopter

        /// <summary>Collective (throttle) that holds a hover at the airport's elevation.</summary>
        public const float HoverCollective = 0.5f;

        private FlightEvents StepHeli(float dt, in FlightInput c)
        {
            FlightEvents ev = FlightEvents.None;
            float rpm = _rotor;
            float col = _power;
            float w = _s.WeightN;
            float k = col < HoverCollective ? 1f - 0.8f * (HoverCollective - col) / HoverCollective : 1f + 0.9f * (col - HoverCollective) / (1f - HoverCollective);
            float density = FlightSpec.Density((float)_y) / FlightSpec.Density(FlightSpec.ReferenceAltitudeM);
            float rotorThrust = w * k * rpm * rpm * Math.Min(1.1f, density);
            if (rpm < 0.85f) rotorThrust *= rpm * rpm; // no lift worth the name until the rotor is up to speed
            FlightInput ci = c;
            bool wasBack = _turningBack;
            if (!_onGround) ApplyBounds(ref ci);
            if (_turningBack && !wasBack) ev |= FlightEvents.TurnBack;

            // Cyclic attitude targets (pull = nose up = slow down / back), self-levelling when released.
            float targetPitch = ci.Pitch >= 0f ? ci.Pitch * _s.MaxPitchUp : ci.Pitch * _s.MaxPitchDown;
            float targetBank = ci.Roll * _s.MaxBank;
            if (_onGround)
            {
                targetPitch = 0f;
                targetBank = 0f;
            }
            _pitch += Clamp(targetPitch - _pitch, -_s.PitchRate * dt, _s.PitchRate * dt);
            _bank += Clamp(targetBank - _bank, -_s.RollRate * dt, _s.RollRate * dt);
            double hvx = _vx, hvz = _vz;
            double hv = Math.Sqrt(hvx * hvx + hvz * hvz);
            float beta = 0f;
            if (hv > 12)
            {
                float track = (float)Math.Atan2(hvx, hvz);
                beta = ArcadeVehicle.WrapAngle(track - _heading);
                if (Math.Abs(beta) > 1.2f) beta = 0f; // flying backwards or sideways: no weathervane
            }
            float yawRate = ci.Yaw * _s.RudderRate * (_onGround && rotorThrust < w * 0.5f ? 0f : 1f) + 1.2f * beta;
            _heading = ArcadeVehicle.WrapAngle(_heading + yawRate * dt);

            // Rotor thrust along the body's up axis.
            float sh = (float)Math.Sin(_heading), chh = (float)Math.Cos(_heading), sp = (float)Math.Sin(_pitch), cp = (float)Math.Cos(_pitch);
            float sb = (float)Math.Sin(_bank), cb = (float)Math.Cos(_bank);
            float u0x = -sh * sp, u0y = cp, u0z = -chh * sp;
            float r0x = chh, r0z = -sh;
            float upx = u0x * cb + r0x * sb, upy = u0y * cb, upz = u0z * cb + r0z * sb;
            // Pilot assist: the collective is topped up as the disc tilts, so tilting flies forward instead of sinking.
            if (!_onGround) rotorThrust /= Math.Max(0.75f, upy);
            double m = _s.MassKg;
            double kd = FlightSpec.G * Math.Tan(_s.MaxPitchDown) / (_s.MaxSpeedMps * (double)_s.MaxSpeedMps);
            double ax = rotorThrust * upx / m - kd * hv * hvx;
            double az = rotorThrust * upz / m - kd * hv * hvz;
            double ay = rotorThrust * upy / m - FlightSpec.G - 1.0 * _vy;
            if (_onGround)
            {
                if (rotorThrust * upy <= w * 1.01f)
                {
                    // Sitting on the skids: friction holds it.
                    _vx *= Math.Exp(-6.0 * dt);
                    _vz *= Math.Exp(-6.0 * dt);
                    _vy = 0;
                    FlightGroundSample g0;
                    _world.Ground(_x, _z, (float)_y + 2f, out g0);
                    _y = g0.Height;
                    _surface = g0.Kind;
                    _agl = 0f;
                    _pitch = GroundPitch(g0, _heading);
                    _bank *= (float)Math.Exp(-6f * dt);
                    _groundSpeed = 0f;
                    return ev;
                }
                _onGround = false;
                ev |= FlightEvents.LiftOff;
            }
            _vx += ax * dt;
            _vy += ay * dt;
            _vz += az * dt;
            double ox = _x, oy = _y, oz = _z;
            _x += _vx * dt;
            _y += _vy * dt;
            _z += _vz * dt;
            UpdateAgl();
            if (_agl < SolidCheckAglM)
            {
                float t;
                FlightObstacle hit;
                double cyo = oy + _s.FuselageCentreM, cyn = _y + _s.FuselageCentreM;
                if (_world.Sweep(ox, cyo, oz, _x, cyn, _z, _s.FuselageRadiusM, out t, out hit) && hit == FlightObstacle.Structure)
                {
                    _x = ox + (_x - ox) * t;
                    _y = oy + (_y - oy) * t;
                    _z = oz + (_z - oz) * t;
                    return ev | BeginBounce(FlightBounceCause.Building);
                }
            }
            FlightGroundSample g;
            _world.Ground(_x, _z, (float)_y + 2f, out g);
            _surface = g.Kind;
            if (_y <= g.Height)
            {
                float sink = (float)Math.Max(0.0, -_vy);
                bool level = g.Kind != FlightSurfaceKind.Water && g.Kind != FlightSurfaceKind.Unknown && g.Slope <= 0.21f;
                bool gentle = sink <= 3.5f && Math.Abs(_bank) <= 0.3f && Math.Abs(_pitch - GroundPitch(g, _heading)) <= 0.3f && hv < 12;
                _y = g.Height;
                if (!level || !gentle)
                    return ev | BeginBounce(g.Kind == FlightSurfaceKind.Water ? FlightBounceCause.Water : level ? FlightBounceCause.HardLanding : FlightBounceCause.Terrain);
                _onGround = true;
                _vy = 0;
                _lastSinkMps = sink;
                _agl = 0f;
                ev |= FlightEvents.Touchdown;
            }
            ClampToBounds(ref ev);
            return ev;
        }

        // ------------------------------------------------------------------------------------------ bounce and reset

        private FlightEvents BeginBounce(FlightBounceCause cause)
        {
            _bouncing = true;
            _bounceT = 0f;
            _bounceSpeed = HorizontalSpeed();
            _cause = cause;
            _onGround = false;
            // A soft cartoon hop up and away, much slower.
            double hx = _vx, hz = _vz;
            _vx = hx * 0.35;
            _vz = hz * 0.35;
            _vy = Clamp((float)Math.Abs(_vy) * 0.35f + 3.5f, 3.5f, 7f);
            FlightGroundSample g;
            _world.Ground(_x, _z, (float)_y + 2f, out g);
            if (_y < g.Height + 0.5) _y = g.Height + 0.5;
            _stalled = _stallWarn = false;
            return FlightEvents.Bounce;
        }

        private FlightEvents StepBounce(float dt)
        {
            _bounceT += dt;
            _vy -= 0.5f * FlightSpec.G * dt; // floaty
            double drag = Math.Exp(-1.5 * dt);
            _vx *= drag;
            _vz *= drag;
            _x += _vx * dt;
            _y += _vy * dt;
            _z += _vz * dt;
            FlightGroundSample g;
            _world.Ground(_x, _z, (float)_y + 2f, out g);
            if (_y < g.Height + 0.5)
            {
                _y = g.Height + 0.5;
                if (_vy < 0) _vy = -_vy * 0.4;
            }
            _bank -= _bank * Math.Min(1f, 3f * dt);
            _pitch += (6f * Deg - _pitch) * Math.Min(1f, 2.5f * dt);
            UpdateAgl();
            if (_bounceT < BounceS) return FlightEvents.None;
            return Reset();
        }

        /// <summary>Ends a bounce now: back on the nearest runway or up at a safe altitude (also usable as "reset").</summary>
        public FlightEvents Reset()
        {
            _bouncing = false;
            _resets++;
            // The speed before the bounce decides: a slow touch near the airport goes back to the runway.
            float speed = _bouncing || _bounceT > 0f ? _bounceSpeed : HorizontalSpeed();
            _bounceT = 0f;
            double rx, rz;
            float ry, rh;
            bool slow = _s.Rotorcraft || speed < _s.StallSpeed((float)_y, 0);
            if (_world.TryRunwayStart(_x, _z, out rx, out rz, out ry, out rh) &&
                (_world.DistanceToRunway(_x, _z) < ResetRunwayRadiusM || slow && _world.DistanceToRunway(_x, _z) < 3 * ResetRunwayRadiusM))
            {
                bool running = _engineRunning;
                PlaceOnGround(rx, rz, rh);
                _engineRunning = running;
                if (_s.Rotorcraft) _rotor = running ? 1f : _rotor;
                return FlightEvents.ResetRunway;
            }
            // Safe altitude: back off along the track, above the highest ground around, heading for the airport.
            double hx = _vx, hz = _vz;
            double hl = Math.Sqrt(hx * hx + hz * hz);
            double bx = hl > 0.5 ? -hx / hl : -Math.Sin(_heading), bz = hl > 0.5 ? -hz / hl : -Math.Cos(_heading);
            double px = _x + bx * 300.0, pz = _z + bz * 300.0;
            float top = float.NegativeInfinity;
            for (int i = 0; i < 9; i++)
            {
                double ox = i == 0 ? 0 : 500.0 * Math.Sin(i * Math.PI / 4), oz = i == 0 ? 0 : 500.0 * Math.Cos(i * Math.PI / 4);
                FlightGroundSample g;
                _world.Ground(px + ox, pz + oz, 1e6f, out g);
                top = Math.Max(top, g.Height);
            }
            float heading = _heading;
            if (_world.TryRunwayStart(px, pz, out rx, out rz, out ry, out rh)) heading = (float)Math.Atan2(rx - px, rz - pz);
            float y = top + ResetAglM;
            if (Bounds.Enabled) y = Math.Min(y, Bounds.CeilingM - 100f);
            PlaceInAir(px, y, pz, heading, _s.Rotorcraft ? 0f : 1.5f * _s.StallSpeed(y, 0));
            return FlightEvents.ResetAir;
        }

        // ---------------------------------------------------------------------------------------------------- bounds

        private void ApplyBounds(ref FlightInput c)
        {
            if (!Bounds.Enabled)
            {
                _turningBack = false;
                return;
            }
            double depth = Bounds.DepthIntoMargin(_x, _z);
            if (depth > 0)
            {
                // Steer for the region's centre: a coordinated turn the player can only add to, not undo.
                _turningBack = true;
                float w = Clamp((float)(depth / Bounds.MarginM) * 1.5f + 0.3f, 0.3f, 1f);
                float desired = (float)Math.Atan2(Bounds.CentreX - _x, Bounds.CentreZ - _z);
                float err = ArcadeVehicle.WrapAngle(desired - _heading);
                if (_s.Rotorcraft)
                {
                    c.Yaw = Lerp(c.Yaw, Clamp(err * 2f, -1f, 1f), w);
                    c.Roll = Lerp(c.Roll, 0f, w);
                }
                else
                {
                    float bankWant = Clamp(err * 1.2f, -35f * Deg, 35f * Deg);
                    float assist = Clamp((bankWant - _bank) * 3f, -1f, 1f);
                    c.Roll = Lerp(c.Roll, assist, w);
                }
            }
            else if (_turningBack && depth <= 0)
            {
                _turningBack = false;
            }
            if (_y > Bounds.CeilingM - 200f)
            {
                float over = Clamp((float)(_y - (Bounds.CeilingM - 200f)) / 200f, 0f, 1f);
                if (_s.Rotorcraft) c.Throttle = Math.Min(c.Throttle, HoverCollective - 0.2f * over);
                else c.Pitch = Math.Min(c.Pitch, -0.4f * over);
            }
        }

        private void ClampToBounds(ref FlightEvents ev)
        {
            if (!Bounds.Enabled) return;
            bool clamped = false;
            if (_x < Bounds.MinX)
            {
                _x = Bounds.MinX;
                if (_vx < 0) _vx = 0;
                clamped = true;
            }
            else if (_x > Bounds.MaxX)
            {
                _x = Bounds.MaxX;
                if (_vx > 0) _vx = 0;
                clamped = true;
            }
            if (_z < Bounds.MinZ)
            {
                _z = Bounds.MinZ;
                if (_vz < 0) _vz = 0;
                clamped = true;
            }
            else if (_z > Bounds.MaxZ)
            {
                _z = Bounds.MaxZ;
                if (_vz > 0) _vz = 0;
                clamped = true;
            }
            if (_y > Bounds.CeilingM)
            {
                _y = Bounds.CeilingM;
                if (_vy > 0) _vy = 0;
            }
            if (clamped && !_turningBack)
            {
                _turningBack = true;
                ev |= FlightEvents.TurnBack;
            }
        }

        // ------------------------------------------------------------------------------------------------- helpers

        private float Cd(float cl)
        {
            float flap = _flaps == 1 ? _s.CdFlap1 : _flaps == 2 ? _s.CdFlap2 : 0f;
            return _s.Cd0 + _gear * _s.CdGear + flap + _s.InducedK * cl * cl;
        }

        /// <summary>Full-power thrust at a speed and density (N).</summary>
        private float MaxThrust(float speed, float rho)
        {
            float sigma = (float)Math.Pow(rho / 1.225f, 0.7);
            return sigma * Math.Min(_s.StaticThrustN, _s.PowerEta / Math.Max(1f, speed));
        }

        private float Thrust(float speed, float rho)
        {
            // Idle still pushes a little (taxiing at idle on a level apron creeps).
            float p = _engineRunning ? Math.Max(0.04f, _power) : 0f;
            return p * MaxThrust(speed, rho);
        }

        private float HorizontalSpeed()
        {
            return (float)Math.Sqrt(_vx * _vx + _vz * _vz);
        }

        private void UpdateAgl()
        {
            FlightGroundSample g;
            _world.Ground(_x, _z, (float)_y + 2f, out g);
            _agl = (float)(_y - g.Height);
        }

        private static float GroundPitch(in FlightGroundSample g, float heading)
        {
            float ny = g.Ny > 0.05f ? g.Ny : 1f;
            float along = g.Nx * (float)Math.Sin(heading) + g.Nz * (float)Math.Cos(heading);
            return (float)Math.Atan(-along / ny);
        }

        private FlightPose PoseNow()
        {
            return new FlightPose { X = _x, Y = _y, Z = _z, Heading = _heading, Pitch = _pitch, Bank = _bank, Speed = Airspeed };
        }

        /// <summary>The HUD's readings now.</summary>
        public FlightInstruments Instruments()
        {
            float v = Airspeed;
            float stall = _s.StallSpeed((float)_y, _flaps);
            return new FlightInstruments
            {
                Craft = _s.Craft, AirspeedKt = v / 0.514444f, GroundSpeedMps = HorizontalSpeed(), AltitudeM = (float)_y, AglM = Math.Max(0f, _agl),
                VerticalSpeedMps = (float)_vy, HeadingDeg = (float)((_heading / Deg + 360.0) % 360.0), PitchDeg = _pitch / Deg, BankDeg = _bank / Deg,
                Throttle = _throttle, Power = _power, GearDown = _gear, RotorRpm = _rotor, Flaps = _flaps, OnGround = OnGround,
                StallWarning = _stallWarn, Stalled = _stalled, TurningBack = _turningBack, Bouncing = _bouncing, CanExit = CanExit,
                StallKt = stall / 0.514444f, RotateKt = _s.RotateMps / 0.514444f,
            };
        }

        private static float Clamp(float v, float a, float b)
        {
            return v < a ? a : v > b ? b : v;
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        private static float Finite(float v)
        {
            return float.IsNaN(v) || float.IsInfinity(v) ? 0f : v;
        }
    }
}
