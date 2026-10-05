using System;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;

namespace Ghumante.Core.Driving
{
    /// <summary>
    /// Arcade vehicle and walker model (ARCHITECTURE 7.6, M1_PLAN contracts): a kinematic bicycle model with
    /// lateral grip and drift, speed-dependent steering, per-surface grip and top speed, wetness, slope gravity
    /// and a maximum climbable slope, terrain following with smoothed pitch and roll, small airborne hops over
    /// crests, and automatic stuck recovery. Pure C#: the Unity side feeds <see cref="DriveInput"/> and an
    /// <see cref="IGroundQuery"/>, and copies the pose to a transform.
    ///
    /// <para><b>Frames.</b> Game metres: X east, Z north, Y up. <see cref="HeadingRad"/> 0 faces north (+Z) and
    /// grows clockwise seen from above (π/2 faces east), so the forward vector is (sin h, cos h) and positive
    /// Steer turns right. <see cref="Pitch"/> is nose-up positive, <see cref="Roll"/> and <see cref="Lean"/> are
    /// right-side-down positive. A Unity rotation is
    /// <c>Quaternion.Euler(-Pitch°, HeadingRad°, -(Roll + Lean)°)</c> (Unity applies Z, then X, then Y).</para>
    ///
    /// <para><b>Stepping.</b> <see cref="Step"/> sub-steps internally to at most <see cref="MaxSubStepS"/>
    /// (1/120 s) of equal length, so 30 and 60 fps give the same trajectory up to rounding. Frames longer than
    /// <see cref="MaxFrameS"/> are clamped (a hitch slows the world rather than teleporting the vehicle). The
    /// state never becomes NaN or infinite: bad input counts as zero, and a step that would produce a
    /// non-finite value is rolled back. Steps do not allocate.</para>
    ///
    /// <para><b>Model.</b> Vehicles: the steering wheel angle follows the input at
    /// <see cref="VehicleSpec.SteerDegPerSec"/>; the yaw rate is <c>v·tan(δ)/L</c>, capped at
    /// <c>|δ/δmax|·SteerLateral/|v|</c> (speed-dependent steering). The direction of travel turns at most at
    /// <c>grip/|v|</c> rad/s; when the steering asks for more, the body over-rotates by a drift angle that relaxes
    /// towards <c>MaxSlip × min(1, asked/possible − 1)</c> and back to 0 when the turn eases, scrubbing speed while
    /// it slides. Throttle drives towards <c>throttle × top speed</c> with an acceleration that tapers as
    /// (1 − (v/target)²); slope gravity, rolling resistance, brakes and engine braking above the surface's top
    /// speed act as forces; rolling resistance and brakes hold a stopped vehicle where they outweigh the slope.
    /// Slopes steeper than <c>tan(MaxSlope) × grip</c> cannot be driven up. The walker
    /// (<see cref="VehicleSpec.TurnInPlace"/>) turns in place, its speed follows the stick (walk at half stick, run
    /// at full, sprint with Boost), it slows uphill and is blocked by slopes over its maximum; it never drifts or
    /// launches.</para>
    ///
    /// <para><b>Ground.</b> Where the ground query is unknown (no loaded tile) the vehicle stops as at a wall. When
    /// the query is an <see cref="ILayeredGroundQuery"/> the current height picks between a bridge deck and what
    /// lies under it; when it is an <see cref="IRoadQuery"/> stuck recovery hops onto the nearest road.</para>
    /// </summary>
    public sealed class ArcadeVehicle
    {
        public const float MaxSubStepS = 1f / 120f;
        public const float MaxFrameS = 0.25f;
        public const float Gravity = 9.81f;

        /// <summary>A ground drop larger than this below the ballistic path within one sub-step means falling
        /// (a cliff or a bridge edge), not following the ground down.</summary>
        public const float StepDownM = 0.6f;

        private const float Deg2Rad = (float)(Math.PI / 180.0);
        private const float Pi = (float)Math.PI;
        private const float TwoPi = (float)(2.0 * Math.PI);
        private const float BumpCooldownS = 0.15f;
        private const float BumpMinSpeedMps = 0.5f;
        private const float LandedMinAirS = 0.12f;
        private const float RoadAssistMaxSteer = 0.3f;
        private const float RoadAssistMaxAngle = 25f * Deg2Rad;
        private const float RoadAssistMinSpeedMps = 3f;
        private const float StuckIntentThrottle = 0.3f;
        private const float StuckMaxBrake = 0.3f;
        private const float RecoveryTurnRadPerS = 6f;
        private const float StopEpsMps = 1e-3f;

        private readonly VehicleSpec _spec;

        /// <summary>Position in game metres (X east, Z north; Y absolute height of the contact point).</summary>
        public double X, Z;

        public float Y;

        /// <summary>Heading in radians, 0 = north, clockwise positive, kept in [−π, π).</summary>
        public float HeadingRad;

        /// <summary>Signed speed in m/s along the direction of travel (negative when reversing).</summary>
        public float SpeedMps;

        /// <summary>Smoothed body pitch (nose up positive) and roll (right side down positive), radians.</summary>
        public float Pitch, Roll;

        /// <summary>The effective surface under the wheels (after wetness), updated while grounded.</summary>
        public SurfaceGroup Surface;

        /// <summary>Motorbike lean into turns, radians, right positive (0 for specs without lean).</summary>
        public float Lean;

        /// <summary>Road magnet assist (<see cref="VehicleSpec.RoadAssistPerS"/>); gameplay settings may switch it off.</summary>
        public bool RoadAssist = true;

        private float _velHeading; // direction of travel
        private float _steer; // wheel angle, radians
        private float _vy; // vertical speed (airborne: ballistic; grounded: terrain-implied)
        private float _airTime;
        private float _bumpCooldown;
        private float _yawRate;
        private float _pathYawRate; // turn rate of the direction of travel (sets the lean)
        private bool _airborne;
        private bool _onRoad;
        private bool _hasGround;
        private bool _recoveryHop;
        private float _recoveryHeading;
        private float _stuckTimer;
        private bool _stuckArmed;
        private double _stuckX, _stuckZ;
        private float _lastWetness;
        private int _intentSign = 1; // sign of the last non-zero throttle
        private GroundSample _ground;

        public ArcadeVehicle(VehicleSpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            spec.Validate();
            Surface = SurfaceGroup.Paved;
        }

        public VehicleSpec Spec
        {
            get { return _spec; }
        }

        public WorldPos Position
        {
            get { return new WorldPos(X, Y, Z); }
        }

        /// <summary>Unit forward vector of the heading in game X/Z.</summary>
        public float ForwardX
        {
            get { return MathF.Sin(HeadingRad); }
        }

        public float ForwardZ
        {
            get { return MathF.Cos(HeadingRad); }
        }

        public bool Airborne
        {
            get { return _airborne; }
        }

        public bool OnRoad
        {
            get { return _onRoad; }
        }

        /// <summary>False until the ground under the vehicle is known (after <see cref="Teleport"/> into a loaded
        /// tile, or a step that found ground).</summary>
        public bool HasGround
        {
            get { return _hasGround; }
        }

        /// <summary>The last ground sample under the vehicle.</summary>
        public GroundSample Ground
        {
            get { return _ground; }
        }

        /// <summary>Vertical speed in m/s (ballistic while airborne).</summary>
        public float VerticalSpeedMps
        {
            get { return _vy; }
        }

        /// <summary>Drift angle: heading minus direction of travel, radians (0 for the walker).</summary>
        public float SlipRad
        {
            get { return WrapAngle(HeadingRad - _velHeading); }
        }

        /// <summary>Front-wheel angle in radians (positive right).</summary>
        public float SteerAngleRad
        {
            get { return _steer; }
        }

        public float YawRateRadPerS
        {
            get { return _yawRate; }
        }

        /// <summary>Seconds of throttle without progress so far (stuck recovery fires at
        /// <see cref="VehicleSpec.StuckSeconds"/>).</summary>
        public float StuckSeconds
        {
            get { return _stuckArmed ? _stuckTimer : 0f; }
        }

        /// <summary>Strength of the last <see cref="StepEvents.Bump"/>: the vertical speed jump in m/s.</summary>
        public float LastBumpStrength { get; private set; }

        /// <summary>Downward speed at the last <see cref="StepEvents.Landed"/>, m/s.</summary>
        public float LastLandingSpeedMps { get; private set; }

        /// <summary>Forward top speed on the current surface (wetness and boost of the last step applied).</summary>
        public float TopSpeedMps { get; private set; }

        /// <summary>Places the vehicle at rest at (x, z) facing <paramref name="headingRad"/>, on the ground when
        /// <paramref name="g"/> knows it there (otherwise <see cref="HasGround"/> stays false and Y is kept until
        /// a step finds ground).</summary>
        public void Teleport(double x, double z, float headingRad, IGroundQuery g)
        {
            if (double.IsNaN(x) || double.IsNaN(z) || double.IsInfinity(x) || double.IsInfinity(z))
                throw new ArgumentException("teleport target must be finite");
            X = x;
            Z = z;
            HeadingRad = WrapAngle(Finite(headingRad));
            _velHeading = HeadingRad;
            SpeedMps = 0f;
            _steer = 0f;
            _vy = 0f;
            _yawRate = 0f;
            _pathYawRate = 0f;
            _airborne = false;
            _airTime = 0f;
            _recoveryHop = false;
            _bumpCooldown = 0f;
            Lean = 0f;
            ResetStuck();
            GroundSample s;
            if (g != null && g.TrySample(x, z, out s))
            {
                _ground = s;
                _hasGround = true;
                Y = s.Height;
                Surface = VehicleSpec.Effective(s.Surface, _lastWetness);
                _onRoad = s.OnRoad;
                float pt, rt;
                PoseTarget(ref s, out pt, out rt);
                Pitch = _spec.PitchRollResponse > 0f ? pt : 0f;
                Roll = _spec.PitchRollResponse > 0f ? rt : 0f;
            }
            else
            {
                _hasGround = false;
                Pitch = 0f;
                Roll = 0f;
            }
            TopSpeedMps = _spec.TopSpeedMps(_ground.Surface, _lastWetness);
        }

        /// <summary>
        /// Advances the simulation by <paramref name="dt"/> seconds (sub-stepped to ≤ 1/120 s) and reports what
        /// happened. <paramref name="wetness01"/>: 0 dry .. 1 monsoon (DIRT behaves like MUD when wet).
        /// </summary>
        public StepEvents Step(in DriveInput input, float dt, IGroundQuery g, float wetness01)
        {
            if (g == null) throw new ArgumentNullException(nameof(g));
            if (!(dt > 0f) || float.IsInfinity(dt)) return StepEvents.None;
            if (dt > MaxFrameS) dt = MaxFrameS;
            float throttle = Clamp(Finite(input.Throttle), -1f, 1f);
            float brake = Clamp(Finite(input.Brake), 0f, 1f);
            float steer = Clamp(Finite(input.Steer), -1f, 1f);
            float wet = Clamp(Finite(wetness01), 0f, 1f);
            _lastWetness = wet;
            if (throttle > 0f) _intentSign = 1;
            else if (throttle < 0f) _intentSign = -1;

            int n = (int)Math.Ceiling(dt / MaxSubStepS - 1e-3);
            if (n < 1) n = 1;
            float h = dt / n;
            StepEvents ev = StepEvents.None;
            for (int k = 0; k < n; k++)
            {
                Snapshot snap = Save();
                StepEvents e = SubStep(throttle, brake, steer, input.Boost, h, g, wet);
                if (!IsFinite())
                {
                    Restore(ref snap);
                    SpeedMps = 0f;
                    _vy = 0f;
                    continue;
                }
                ev |= e;
            }
            return ev;
        }

        private StepEvents SubStep(float throttle, float brake, float steer, bool boost, float h, IGroundQuery g, float wet)
        {
            StepEvents ev = StepEvents.None;
            if (!_hasGround)
            {
                GroundSample s0;
                if (!g.TrySample(X, Z, out s0)) return ev; // nothing under us yet: wait for the world
                // First ground after construction or a teleport into an unloaded spot: stand on it.
                _ground = s0;
                _hasGround = true;
                if (!_airborne) Y = s0.Height;
                _onRoad = s0.OnRoad;
            }
            if (_bumpCooldown > 0f) _bumpCooldown -= h;

            SurfaceGroup raw = _ground.Surface;
            float grip = _spec.GripOf(raw, wet);
            float topFactor = _spec.TopSpeedFactorOf(raw, wet);
            float vTop = _spec.MaxSpeedMps * topFactor * (boost ? _spec.BoostSpeedFactor : 1f);
            float vRev = _spec.ReverseSpeedMps * topFactor;
            TopSpeedMps = vTop;

            if (_spec.TurnInPlace) WalkerDrive(throttle, brake, steer, boost, topFactor, h);
            else VehicleDrive(throttle, brake, steer, grip, vTop, vRev, h);

            // ---- move ----
            bool moved = false;
            GroundSample s;
            float v = SpeedMps;
            if (v != 0f)
            {
                double dist = v * h;
                double nx = X + dist * Math.Sin(_velHeading), nz = Z + dist * Math.Cos(_velHeading);
                if (Sample(g, nx, nz, out s) && !WalkerBlocked(ref s, v))
                {
                    X = nx;
                    Z = nz;
                    moved = true;
                }
                else
                {
                    // No ground ahead (edge of the loaded world) or too steep on foot: a wall.
                    SpeedMps = 0f;
                    if (!Sample(g, X, Z, out s)) s = _ground;
                }
            }
            else if (!Sample(g, X, Z, out s))
            {
                s = _ground;
            }

            // ---- vertical ----
            float vyTerrain = SpeedMps * GradeAlong(ref s, _velHeading);
            if (_airborne)
            {
                _airTime += h;
                _vy -= Gravity * h;
                Y += _vy * h;
                if (_recoveryHop) HeadingRad = MoveTowardsAngle(HeadingRad, _recoveryHeading, RecoveryTurnRadPerS * h);
                if (Y <= s.Height)
                {
                    LastLandingSpeedMps = -_vy;
                    if (_airTime >= LandedMinAirS || -_vy > 2f) ev |= StepEvents.Landed;
                    Y = s.Height;
                    _airborne = false;
                    _airTime = 0f;
                    if (_recoveryHop)
                    {
                        _recoveryHop = false;
                        HeadingRad = _recoveryHeading;
                        _velHeading = HeadingRad;
                        SpeedMps = 0f;
                        vyTerrain = 0f;
                    }
                    _vy = vyTerrain;
                }
            }
            else
            {
                float yBallistic = Y + _vy * h - 0.5f * Gravity * h * h;
                bool launch = _spec.LaunchDeltaVyMps > 0f && moved && _vy >= _spec.LaunchMinVyMps
                              && _vy - vyTerrain >= _spec.LaunchDeltaVyMps && s.Height < yBallistic;
                bool fall = s.Height < yBallistic - StepDownM;
                if (launch || fall)
                {
                    _airborne = true;
                    _airTime = 0f;
                    Y = yBallistic;
                    _vy -= Gravity * h;
                }
                else
                {
                    float yPrev = Y;
                    Y = s.Height;
                    if (moved && _spec.BumpThresholdMps > 0f && Math.Abs(SpeedMps) >= BumpMinSpeedMps && _bumpCooldown <= 0f)
                    {
                        float jolt = Math.Abs((Y - yPrev) / h - vyTerrain);
                        if (jolt >= _spec.BumpThresholdMps)
                        {
                            ev |= StepEvents.Bump;
                            LastBumpStrength = jolt;
                            _bumpCooldown = BumpCooldownS;
                        }
                    }
                    _vy = vyTerrain;
                }
            }
            _ground = s;

            // ---- surface, road and pose ----
            if (!_airborne)
            {
                SurfaceGroup eff = VehicleSpec.Effective(s.Surface, wet);
                if (eff != Surface)
                {
                    Surface = eff;
                    ev |= StepEvents.SurfaceChanged;
                }
                if (_onRoad && !s.OnRoad) ev |= StepEvents.OffRoad;
                _onRoad = s.OnRoad;
            }
            UpdatePose(ref s, h);

            // ---- stuck recovery ----
            if (_spec.StuckSeconds > 0f && !_airborne)
            {
                bool intent = Math.Abs(throttle) >= StuckIntentThrottle && brake < StuckMaxBrake;
                if (!intent)
                {
                    ResetStuck();
                }
                else if (!_stuckArmed)
                {
                    _stuckArmed = true;
                    _stuckTimer = 0f;
                    _stuckX = X;
                    _stuckZ = Z;
                }
                else
                {
                    _stuckTimer += h;
                    double dx = X - _stuckX, dz = Z - _stuckZ;
                    if (dx * dx + dz * dz >= (double)_spec.StuckProgressM * _spec.StuckProgressM)
                    {
                        _stuckTimer = 0f;
                        _stuckX = X;
                        _stuckZ = Z;
                    }
                    else if (_stuckTimer >= _spec.StuckSeconds)
                    {
                        if (Recover(g)) ev |= StepEvents.StuckRecovered;
                        ResetStuck();
                    }
                }
            }
            return ev;
        }

        private void VehicleDrive(float throttle, float brake, float steer, float grip, float vTop, float vRev, float h)
        {
            VehicleSpec sp = _spec;
            float maxSteer = sp.MaxSteerDeg * Deg2Rad;
            _steer = MoveTowards(_steer, steer * maxSteer, sp.SteerDegPerSec * Deg2Rad * h);
            float v = SpeedMps, absV = Math.Abs(v);
            float slip = WrapAngle(HeadingRad - _velHeading);

            if (!_airborne)
            {
                float maxSlip = sp.MaxSlipDeg * Deg2Rad;
                if (Math.Abs(slip) > maxSlip + 1e-3f || absV < 0.1f)
                {
                    // At rest, or the heading was set from outside: travel where we face.
                    _velHeading = HeadingRad;
                    slip = 0f;
                }

                // Yaw: kinematic bicycle, capped by the speed-dependent steering limit.
                float sigma = _steer / maxSteer;
                float wGeo = v * MathF.Tan(_steer) / sp.WheelbaseM;
                float wCap = sp.SteerLateralMps2 * Math.Abs(sigma) / Math.Max(absV, 0.5f);
                float w = Clamp(wGeo, -wCap, wCap);

                // Road magnet: gentle steering on a road lines the heading up with the road.
                if (RoadAssist && sp.RoadAssistPerS > 0f && _onRoad && v > RoadAssistMinSpeedMps
                    && Math.Abs(steer) < RoadAssistMaxSteer && (_ground.RoadDirX != 0f || _ground.RoadDirZ != 0f))
                {
                    float roadHeading = MathF.Atan2(_ground.RoadDirX, _ground.RoadDirZ);
                    float diff = WrapAngle(roadHeading - HeadingRad);
                    if (Math.Abs(diff) > 0.5f * Pi) diff = WrapAngle(diff + Pi);
                    if (Math.Abs(diff) < RoadAssistMaxAngle)
                        w += diff * sp.RoadAssistPerS * (1f - Math.Abs(steer) / RoadAssistMaxSteer);
                }
                float wMax = sp.MaxYawRateDegPerSec * Deg2Rad;
                w = Clamp(w, -wMax, wMax);

                // Grip: the path turns as fast as lateral grip allows; the body turns further by the drift angle,
                // which relaxes towards a target set by how far the asked-for turn exceeds the grip.
                float wGrip = sp.LateralGripMps2 * grip / Math.Max(absV, 0.5f);
                float wPath = Clamp(w, -wGrip, wGrip);
                float over = Math.Abs(w) / wGrip - 1f;
                float slipTarget = over > 0f ? Math.Sign(w) * maxSlip * Math.Min(1f, over) : 0f;
                slip += (slipTarget - slip) * (1f - MathF.Exp(-sp.DriftResponse * h));
                float before = HeadingRad;
                _velHeading = WrapAngle(_velHeading + wPath * h);
                HeadingRad = WrapAngle(_velHeading + slip);
                _yawRate = WrapAngle(HeadingRad - before) / h;
                _pathYawRate = wPath;
            }
            else
            {
                _yawRate = 0f;
                _pathYawRate = 0f;
                return; // ballistic: no drive, no grip
            }

            // Longitudinal forces along the direction of travel.
            float fx = MathF.Sin(_velHeading), fz = MathF.Cos(_velHeading);
            float nd = _ground.Nx * fx + _ground.Nz * fz;
            float ny = Math.Max(_ground.Ny, 1e-3f);
            float aSlope = Gravity * nd / MathF.Sqrt(ny * ny + nd * nd);
            float gradeFwd = -nd / ny;
            float traction = Clamp(grip, 0.2f, 1f);
            float maxGrade = MathF.Tan(sp.MaxSlopeDeg * Deg2Rad) * Math.Min(grip, 1f);

            float aDrive = 0f, resist = 0f;
            if (throttle > 0f)
            {
                if (v >= -0.5f)
                {
                    float target = throttle * vTop;
                    if (v < target)
                    {
                        if (gradeFwd <= maxGrade)
                        {
                            float r = Math.Max(v, 0f) / target;
                            aDrive = sp.AccelMps2 * traction * (1f - r * r);
                        }
                    }
                    else
                    {
                        resist += sp.CoastDecelMps2;
                    }
                }
                else
                {
                    resist += throttle * sp.BrakeMps2 * traction;
                }
            }
            else if (throttle < 0f)
            {
                if (v <= 0.5f)
                {
                    float target = -throttle * vRev;
                    if (-v < target)
                    {
                        if (-gradeFwd <= maxGrade)
                        {
                            float r = Math.Max(-v, 0f) / target;
                            aDrive = -sp.AccelMps2 * sp.ReverseAccelFactor * traction * (1f - r * r);
                        }
                    }
                    else
                    {
                        resist += sp.CoastDecelMps2;
                    }
                }
                else
                {
                    resist += -throttle * sp.BrakeMps2 * traction;
                }
            }
            else
            {
                resist += sp.CoastDecelMps2;
            }
            if (brake > 0f) resist += brake * sp.BrakeMps2 * traction;
            if (v > vTop || v < -vRev) resist += sp.OverSpeedDecelMps2;
            resist += sp.SlideDecelMps2 * Math.Abs(MathF.Sin(slip));

            SpeedMps = ApplyForces(v, aDrive + aSlope, resist, h);
            float cap = 2f * Math.Max(vTop, vRev) + 10f;
            SpeedMps = Clamp(SpeedMps, -cap, cap);
        }

        /// <summary>Coulomb-style integration: <paramref name="active"/> pushes, <paramref name="resist"/> (≥ 0)
        /// always opposes motion and can hold the vehicle still, but never reverses it.</summary>
        private static float ApplyForces(float v, float active, float resist, float h)
        {
            if (Math.Abs(v) < StopEpsMps)
            {
                if (Math.Abs(active) <= resist) return 0f;
                return (active - Math.Sign(active) * resist) * h;
            }
            float nv = v + (active - Math.Sign(v) * resist) * h;
            if (v > 0f && nv < 0f || v < 0f && nv > 0f)
            {
                // Crossing zero this step: friction stops it there unless the push alone exceeds it.
                if (Math.Abs(active) <= resist) return 0f;
            }
            return nv;
        }

        private void WalkerDrive(float throttle, float brake, float steer, bool boost, float topFactor, float h)
        {
            VehicleSpec sp = _spec;
            if (_airborne)
            {
                _yawRate = 0f;
                return;
            }
            float w = Clamp(steer * sp.TurnRateDegPerSec, -sp.MaxYawRateDegPerSec, sp.MaxYawRateDegPerSec) * Deg2Rad;
            HeadingRad = WrapAngle(HeadingRad + w * h);
            _yawRate = w;
            _pathYawRate = w;
            _velHeading = HeadingRad;

            // Gait: 0..0.5 of the stick walks, 0.5..1 blends to the run (or sprint with Boost).
            float a = Math.Abs(throttle);
            float run = sp.MaxSpeedMps * (boost ? sp.BoostSpeedFactor : 1f);
            float walk = Math.Min(sp.WalkSpeedMps, run);
            float speed = a <= 0.5f ? walk * (a / 0.5f) : walk + (run - walk) * ((a - 0.5f) / 0.5f);
            if (throttle < 0f) speed = Math.Min(speed, sp.ReverseSpeedMps);
            speed *= topFactor * (1f - brake);
            float grade = GradeAlong(ref _ground, _velHeading) * (throttle < 0f ? -1f : 1f);
            if (grade > 0f) speed *= Math.Max(0.35f, 1f - 1.2f * grade);
            float target = throttle < 0f ? -speed : speed;
            float v = SpeedMps;
            bool speedingUp = Math.Abs(target) > Math.Abs(v) && (v == 0f || Math.Sign(target) == Math.Sign(v));
            float rate = speedingUp ? sp.AccelMps2 : sp.BrakeMps2;
            SpeedMps = MoveTowards(v, target, rate * h);
        }

        /// <summary>The ground at (x, z) as seen from the current height: a layered query keeps the vehicle under a
        /// bridge when it is under it and on the deck when it is on it.</summary>
        private bool Sample(IGroundQuery g, double x, double z, out GroundSample s)
        {
            var layered = g as ILayeredGroundQuery;
            return layered != null ? layered.TrySample(x, z, Y, out s) : g.TrySample(x, z, out s);
        }

        /// <summary>On foot, moving uphill onto ground steeper than the spec's maximum is blocked.</summary>
        private bool WalkerBlocked(ref GroundSample s, float v)
        {
            if (!_spec.TurnInPlace || _airborne) return false;
            float ny = Math.Max(s.Ny, 1e-3f);
            if (ny >= MathF.Cos(_spec.MaxSlopeDeg * Deg2Rad)) return false;
            float uphill = GradeAlong(ref s, _velHeading) * Math.Sign(v);
            return uphill > 0f;
        }

        /// <summary>
        /// Hop and nudge out of a stuck spot (ARCHITECTURE 7.6). With a road within
        /// <see cref="VehicleSpec.RecoverySearchM"/> (when the ground query is also an <see cref="IRoadQuery"/>)
        /// the vehicle hops onto it, facing along it (or, already on it, a few metres further along); otherwise it
        /// hops <see cref="VehicleSpec.RecoveryNudgeM"/> towards the first climbable direction, trying the intended
        /// one first. Returns false without ground under the vehicle. Gameplay may call it for a "reset" button.
        /// </summary>
        public bool Recover(IGroundQuery g)
        {
            if (g == null) throw new ArgumentNullException(nameof(g));
            GroundSample here;
            if (!Sample(g, X, Z, out here)) return false;
            VehicleSpec sp = _spec;
            float baseHeading = _intentSign >= 0 ? HeadingRad : WrapAngle(HeadingRad + Pi);
            double tx = X, tz = Z;
            float faceHeading = HeadingRad;
            bool found = false;

            var roads = g as IRoadQuery;
            RoadHit hit;
            if (roads != null && roads.TryNearestRoad(X, Z, sp.RecoverySearchM, out hit))
            {
                float roadHeading = MathF.Atan2(hit.DirX, hit.DirZ);
                if (Math.Abs(WrapAngle(roadHeading - HeadingRad)) > 0.5f * Pi) roadHeading = WrapAngle(roadHeading + Pi);
                double cx = hit.X, cz = hit.Z;
                if (hit.DistanceM <= 0.5f * sp.RecoveryNudgeM)
                {
                    // Already on the road: move along it in the intended direction.
                    float along = _intentSign >= 0 ? roadHeading : WrapAngle(roadHeading + Pi);
                    cx += sp.RecoveryNudgeM * MathF.Sin(along);
                    cz += sp.RecoveryNudgeM * MathF.Cos(along);
                }
                GroundSample t;
                if (Sample(g, cx, cz, out t))
                {
                    tx = cx;
                    tz = cz;
                    faceHeading = roadHeading;
                    found = true;
                }
            }
            float targetHeight = here.Height;
            GroundSample landing;
            if (found && Sample(g, tx, tz, out landing)) targetHeight = landing.Height;
            if (!found)
            {
                float climb = sp.RecoveryNudgeM * MathF.Tan(sp.MaxSlopeDeg * Deg2Rad) * 0.8f;
                float cosMax = MathF.Cos(sp.MaxSlopeDeg * Deg2Rad);
                for (int k = 0; k < 8 && !found; k++)
                {
                    // 0, +45, -45, +90, -90, +135, -135, 180 degrees from the intended direction.
                    int m = (k + 1) / 2;
                    float off = (k == 7 ? 4 : m) * (k % 2 == 1 ? 1f : -1f) * 0.25f * Pi;
                    float dir = WrapAngle(baseHeading + off);
                    double cx = X + sp.RecoveryNudgeM * MathF.Sin(dir), cz = Z + sp.RecoveryNudgeM * MathF.Cos(dir);
                    GroundSample t;
                    if (!Sample(g, cx, cz, out t)) continue;
                    if (t.Height - here.Height > climb || t.Ny < cosMax) continue;
                    tx = cx;
                    tz = cz;
                    targetHeight = t.Height;
                    faceHeading = _intentSign >= 0 ? dir : WrapAngle(dir + Pi);
                    found = true;
                }
            }
            if (!found) faceHeading = WrapAngle(HeadingRad + Pi); // boxed in: hop in place and turn around

            // Flight time to come down at the target's height (hop harder when the target is above the apex).
            float y0 = Math.Max(Y, here.Height), drop = y0 - targetHeight;
            float hop = sp.RecoveryHopMps;
            if (hop * hop + 2f * Gravity * drop < 1f) hop = MathF.Sqrt(1f - 2f * Gravity * drop);
            float flight = (hop + MathF.Sqrt(hop * hop + 2f * Gravity * drop)) / Gravity;
            double dxT = tx - X, dzT = tz - Z;
            double dist = Math.Sqrt(dxT * dxT + dzT * dzT);
            _velHeading = dist > 1e-3 ? WrapAngle((float)Math.Atan2(dxT, dzT)) : HeadingRad;
            SpeedMps = flight > 1e-3f ? (float)(dist / flight) : 0f;
            _recoveryHeading = faceHeading;
            _recoveryHop = true;
            _airborne = true;
            _airTime = 0f;
            _vy = hop;
            _steer = 0f;
            _ground = here;
            _hasGround = true;
            if (Y < here.Height) Y = here.Height;
            ResetStuck();
            return true;
        }

        /// <summary>A Steer value in [−1, 1] that turns towards <paramref name="targetHeadingRad"/> (camera-relative
        /// walking, AI). For the walker it reaches the heading within the frame when it can; for vehicles it is
        /// proportional, full lock from 30° off.</summary>
        public float SteerToward(float targetHeadingRad, float dt)
        {
            float diff = WrapAngle(Finite(targetHeadingRad) - HeadingRad);
            if (_spec.TurnInPlace)
            {
                float perFrame = _spec.TurnRateDegPerSec * Deg2Rad * Math.Max(Finite(dt), 1e-4f);
                return Clamp(diff / perFrame, -1f, 1f);
            }
            float s = Clamp(diff / (30f * Deg2Rad), -1f, 1f);
            return SpeedMps < 0f ? -s : s;
        }

        private void UpdatePose(ref GroundSample s, float h)
        {
            float pt, rt;
            if (!_airborne)
            {
                PoseTarget(ref s, out pt, out rt);
            }
            else
            {
                // In the air the nose follows the trajectory a little; roll holds.
                pt = Clamp(0.5f * MathF.Atan2(_vy, Math.Max(Math.Abs(SpeedMps), 1f)), -0.5f, 0.5f);
                rt = Roll;
            }
            if (_spec.PitchRollResponse > 0f)
            {
                float k = 1f - MathF.Exp(-_spec.PitchRollResponse * h);
                Pitch += (pt - Pitch) * k;
                Roll += (rt - Roll) * k;
            }
            else
            {
                Pitch = 0f;
                Roll = 0f;
            }
            if (_spec.MaxLeanDeg > 0f)
            {
                float maxLean = _spec.MaxLeanDeg * Deg2Rad;
                float lt = _airborne ? Lean : Clamp(MathF.Atan(SpeedMps * _pathYawRate / Gravity), -maxLean, maxLean);
                Lean += (lt - Lean) * (1f - MathF.Exp(-_spec.LeanResponse * h));
            }
            else
            {
                Lean = 0f;
            }
        }

        /// <summary>Pitch and roll that put the body's up axis on the ground normal at the current heading.</summary>
        private void PoseTarget(ref GroundSample s, out float pitch, out float roll)
        {
            float fx = MathF.Sin(HeadingRad), fz = MathF.Cos(HeadingRad);
            float nf = s.Nx * fx + s.Nz * fz; // along forward
            float nr = s.Nx * fz - s.Nz * fx; // along right = (cos h, -sin h)
            pitch = MathF.Atan2(-nf, Math.Max(s.Ny, 1e-3f));
            roll = MathF.Asin(Clamp(nr, -1f, 1f));
        }

        /// <summary>Rise per metre of the ground along heading <paramref name="heading"/> (dh/ds).</summary>
        private static float GradeAlong(ref GroundSample s, float heading)
        {
            float nd = s.Nx * MathF.Sin(heading) + s.Nz * MathF.Cos(heading);
            return -nd / Math.Max(s.Ny, 1e-3f);
        }

        private void ResetStuck()
        {
            _stuckArmed = false;
            _stuckTimer = 0f;
        }

        // ---- NaN guard ----

        private struct Snapshot
        {
            public double X, Z, StuckX, StuckZ;
            public float Y, Heading, Speed, Pitch, Roll, Lean, VelHeading, Steer, Vy, AirTime, YawRate, PathYawRate, StuckTimer, RecoveryHeading;
            public bool Airborne, OnRoad, HasGround, RecoveryHop, StuckArmed;
            public SurfaceGroup Surface;
            public GroundSample Ground;
        }

        private Snapshot Save()
        {
            return new Snapshot
            {
                X = X, Z = Z, StuckX = _stuckX, StuckZ = _stuckZ, Y = Y, Heading = HeadingRad, Speed = SpeedMps, Pitch = Pitch,
                Roll = Roll, Lean = Lean, VelHeading = _velHeading, Steer = _steer, Vy = _vy, AirTime = _airTime,
                YawRate = _yawRate, PathYawRate = _pathYawRate, StuckTimer = _stuckTimer, RecoveryHeading = _recoveryHeading, Airborne = _airborne,
                OnRoad = _onRoad, HasGround = _hasGround, RecoveryHop = _recoveryHop, StuckArmed = _stuckArmed,
                Surface = Surface, Ground = _ground,
            };
        }

        private void Restore(ref Snapshot s)
        {
            X = s.X;
            Z = s.Z;
            _stuckX = s.StuckX;
            _stuckZ = s.StuckZ;
            Y = s.Y;
            HeadingRad = s.Heading;
            SpeedMps = s.Speed;
            Pitch = s.Pitch;
            Roll = s.Roll;
            Lean = s.Lean;
            _velHeading = s.VelHeading;
            _steer = s.Steer;
            _vy = s.Vy;
            _airTime = s.AirTime;
            _yawRate = s.YawRate;
            _pathYawRate = s.PathYawRate;
            _stuckTimer = s.StuckTimer;
            _recoveryHeading = s.RecoveryHeading;
            _airborne = s.Airborne;
            _onRoad = s.OnRoad;
            _hasGround = s.HasGround;
            _recoveryHop = s.RecoveryHop;
            _stuckArmed = s.StuckArmed;
            Surface = s.Surface;
            _ground = s.Ground;
        }

        private bool IsFinite()
        {
            return Fin(X) && Fin(Z) && Fin(Y) && Fin(HeadingRad) && Fin(SpeedMps) && Fin(Pitch) && Fin(Roll) && Fin(Lean)
                   && Fin(_velHeading) && Fin(_steer) && Fin(_vy) && Fin(_yawRate) && Fin(_pathYawRate);
        }

        private static bool Fin(double v)
        {
            return !double.IsNaN(v) && !double.IsInfinity(v);
        }

        private static bool Fin(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }

        // ---- small maths ----

        private static float Finite(float v)
        {
            return float.IsNaN(v) || float.IsInfinity(v) ? 0f : v;
        }

        private static float Clamp(float v, float lo, float hi)
        {
            return v < lo ? lo : v > hi ? hi : v;
        }

        private static float MoveTowards(float v, float target, float maxDelta)
        {
            if (Math.Abs(target - v) <= maxDelta) return target;
            return v + Math.Sign(target - v) * maxDelta;
        }

        private static float MoveTowardsAngle(float a, float target, float maxDelta)
        {
            float d = WrapAngle(target - a);
            if (Math.Abs(d) <= maxDelta) return WrapAngle(target);
            return WrapAngle(a + Math.Sign(d) * maxDelta);
        }

        /// <summary>An angle wrapped into [−π, π).</summary>
        public static float WrapAngle(float a)
        {
            if (a >= -Pi && a < Pi) return a;
            if (float.IsNaN(a) || float.IsInfinity(a)) return 0f;
            float w = a - TwoPi * MathF.Floor((a + Pi) / TwoPi);
            return w >= Pi ? w - TwoPi : w;
        }
    }
}
