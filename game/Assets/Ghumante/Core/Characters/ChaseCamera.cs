using System;
using Ghumante.Core.Driving;

namespace Ghumante.Core.Characters
{
    /// <summary>What the chase camera follows this frame (scene metres: game metres minus the floating origin).</summary>
    public struct ChaseTarget
    {
        /// <summary>The controlled body's ground point: the walker's feet, a vehicle's point under its rear axle.</summary>
        public V3 Ground;

        /// <summary>Body heading, radians (0 = north, clockwise).</summary>
        public float HeadingRad;

        /// <summary>Signed speed along the heading, m/s (negative reversing).</summary>
        public float SpeedMps;

        public float LeanRad;
        public RigClass Rig;

        /// <summary>The driver rig of the vehicle ridden along (<see cref="RigClass.Passenger"/> orbits at 1.2× it).</summary>
        public RigClass PassengerOf;

        /// <summary>With <see cref="HasBody"/>: the body's middle ahead of <see cref="Ground"/>, metres
        /// (<see cref="ChaseCamera.BodyAheadOf"/>); otherwise the class's <see cref="RigParams.BodyAheadM"/>.</summary>
        public float BodyAheadM;

        public bool HasBody;

        /// <summary>An aircraft (the flight package): the rig is <see cref="CameraRigTable.ForAircraft"/> of
        /// <see cref="AircraftLengthM"/>, the boom tilts with the flight path and the view rolls with a little of
        /// <see cref="BankRad"/>.</summary>
        public bool Aircraft;

        public float AircraftLengthM;

        /// <summary>Aircraft only: bank, radians, right wing down positive.</summary>
        public float BankRad;
    }

    /// <summary>The player's camera input this frame (zoom notches, look-around in degrees).</summary>
    public struct ChaseControls
    {
        public float ZoomSteps, LookYawDeg, LookPitchDeg;

        /// <summary>A finger or the right stick is on the look control (even if it did not move this frame).</summary>
        public bool Looking;
    }

    /// <summary>The chase camera's pose this frame (scene metres).</summary>
    public struct ChasePose
    {
        public V3 Position;
        public Quat Rotation;

        /// <summary>Vertical field of view, degrees.</summary>
        public float VerticalFovDeg;

        /// <summary>The camera's heading (degrees, 0 = north, clockwise): walking is relative to it; the compass shows it.</summary>
        public float YawDeg;
    }

    /// <summary>
    /// The chase camera, engine-free (owner playtest #5: "camera angle is bad; it constantly keeps moving, which makes the
    /// driving experience really bad"; docs/research/w2/ref_camera.md). The Unity rig (<c>ChaseCameraRig</c>) hands it
    /// the explorer every LateUpdate and copies the pose to the camera; the core tests drive it over recorded rides on
    /// real streets. What keeps it steady:
    /// <list type="bullet">
    /// <item><b>Exact critically damped springs</b> (<see cref="CameraSpring"/>) on everything that moves, stepped with
    /// the closed-form solution, so an uneven frame clock (30 fps ± jitter, hitches) never changes where the camera sits
    /// relative to the vehicle.</item>
    /// <item><b>The followed point</b> is the middle of the body on the ground, lightly smoothed across the ground
    /// (<see cref="RigParams.PositionS"/>) and smoothed up and down with the slope fed forward
    /// (<see cref="RigParams.HeightS"/>): kerbs, seams and the 60 Hz physics never shake the view; hills are followed
    /// without lag.</item>
    /// <item><b>Yaw</b> follows the body's heading with a gentle lag (<see cref="RigParams.FollowS"/>; it shows the
    /// inside of a turn and recentres on the straight), not at all below <see cref="RigParams.FollowFromMps"/> (no
    /// swinging while parking); <b>pitch and roll never follow</b> the suspension, a two-wheeler's lean or bumps (the
    /// horizon stays level).</item>
    /// <item><b>Speed</b> effects (look-ahead, FOV kick, pull-back) read a speed smoothed over
    /// <see cref="SpeedSmoothS"/>, so they breathe slowly instead of pumping.</item>
    /// <item><b>Collision</b> through <see cref="ChaseBoom"/>: predictive (it looks where the pivot and the yaw spring will
    /// be, <see cref="ChaseBoom.PredictS"/>), hysteretic, raises or lowers before it pulls in and then pulls in no faster
    /// than <see cref="ChaseBoom.MaxLengthSharePerS"/>, never inside a wall; the ground clamp lifts the camera
    /// smoothly.</item>
    /// <item><b>Look-around</b> springs back behind the vehicle <see cref="LookReturnDelayS"/> after the player lets go.</item>
    /// </list>
    /// Floating-origin rebases shift the state (<see cref="ShiftOrigin"/>); a jump of the body by more than
    /// <see cref="SnapDistanceM"/> (a teleport) starts afresh. No allocation per frame.
    /// </summary>
    public sealed class ChaseCamera
    {
        public const float MinZoom = 0.55f, MaxZoom = 2.6f;
        public const float MinPitchOffsetDeg = -9f, MaxPitchOffsetDeg = 40f;

        /// <summary>Lowest the camera may get above the ground (the clamp lifts it smoothly; never less than
        /// <see cref="GroundHardM"/>).</summary>
        public const float GroundClearanceM = 0.8f;

        public const float GroundHardM = 0.45f;

        /// <summary>Seconds after the last look input before the camera swings back behind the rider ...</summary>
        public const float LookReturnDelayS = 1.4f;

        /// <summary>... over about this long (critically damped).</summary>
        public const float LookReturnS = 0.55f;

        /// <summary>On foot the camera swings in behind only while the walker heads within this angle of its view.</summary>
        public const float WalkFollowConeDeg = 35f;

        /// <summary>Share of <see cref="ReverseFraming.FootRaise"/> used in landscape (the view is short there).</summary>
        public const float LandscapeFootRaiseShare = 0.35f;

        public const float OrientationBlendSeconds = 0.3f;

        /// <summary>A change of class or chase view blends the rig numbers over about this long.</summary>
        public const float RigBlendSeconds = 0.45f;

        /// <summary>Speed for the framing (look-ahead, FOV kick, pull-back) is smoothed over this long.</summary>
        public const float SpeedSmoothS = 0.8f;

        /// <summary>The slope fed forward into the height follow is the climb rate averaged over this long.</summary>
        public const float ClimbSmoothS = 0.6f;

        /// <summary>Averaged climb rates below this (m/s) are not fed forward, fading in fully at three times it: the
        /// seams and patches of a road average to a few cm/s of noise that would otherwise leak into the view, while a
        /// real slope climbs faster (2 % at 7 m/s is 0.14 m/s). A gentler climb trails by at most a centimetre or two.</summary>
        public const float ClimbGateMps = 0.05f;

        /// <summary>A body that jumps farther than this in one frame was teleported: start afresh.</summary>
        public const float SnapDistanceM = 12f;

        /// <summary>In a lane the yaw follows this much tighter (the boom swings less into the walls).</summary>
        public const float LaneFollowTighten = 0.4f;

        /// <summary>The body's turn rate is smoothed over this long (a wall knock jolts the heading) before the boom uses it
        /// to see where its yaw is going.</summary>
        public const float TurnRateSmoothS = 0.12f;

        private readonly ChaseBoom _boom = new ChaseBoom();
        private readonly ReverseFraming _reverse = new ReverseFraming();

        private bool _snap = true;
        private float _portrait;
        private RigParams _rig;
        private float _aim01;
        private float _zoomLog;
        private float _pitchOffset;
        private float _orbit;
        private CameraSpring _orbitReturn;
        private float _lookIdle = 999f;
        private CameraAngleSpring _yaw;
        private float _lastHeading, _headingRate;
        private float _yawGoal, _yawGoalRate, _yawSmooth;
        private CameraSpring _ax, _ay, _az;
        private float _lastGoalY;
        private float _climb;
        private float _speed;
        private float _foot;
        private float _fov;
        private float _shoulder;
        private CameraSpring _lift;
        private float _pathDeg;

        /// <summary>No speed FOV kick and no aircraft bank roll (Settings, Reduce motion).</summary>
        public bool ReducedMotion { get; set; }

        /// <summary>Solid world geometry the boom must not enter (game metres). Null: no collision but the ground clamp.</summary>
        public IViewObstacleQuery Obstacles { get; set; }

        /// <summary>The boom (pull-in, lift, lane mode), for diagnostics and tests.</summary>
        public ChaseBoom Boom
        {
            get { return _boom; }
        }

        /// <summary>The reversing frame, for diagnostics and tests.</summary>
        public ReverseFraming Reverse
        {
            get { return _reverse; }
        }

        /// <summary>The rig numbers in use (blended after a change of class, view or orientation).</summary>
        public RigParams Rig
        {
            get { return _rig; }
        }

        /// <summary>0 in landscape, 1 in portrait (blending between them after a rotation).</summary>
        public float Portrait01
        {
            get { return _portrait; }
        }

        /// <summary>The followed point (the body's middle on the ground, smoothed), scene metres.</summary>
        public V3 Anchor
        {
            get { return new V3(_ax.Value, _ay.Value, _az.Value); }
        }

        /// <summary>The camera's heading this frame, degrees.</summary>
        public float YawDeg { get; private set; }

        /// <summary>Jump to the target next tick (spawn, teleport): no smoothing from the old place.</summary>
        public void Snap()
        {
            _snap = true;
        }

        /// <summary>The floating origin moved by (<paramref name="dx"/>, <paramref name="dy"/>, <paramref name="dz"/>)
        /// (new − old, metres): every scene position the camera remembers moves by the opposite.</summary>
        public void ShiftOrigin(double dx, double dy, double dz)
        {
            _ax.Shift((float)-dx);
            _ay.Shift((float)-dy);
            _az.Shift((float)-dz);
            _lastGoalY -= (float)dy;
        }

        /// <summary>The middle of a catalogue vehicle's body ahead of its ground point (the point under the rear axle that
        /// <see cref="ArcadeVehicle"/> and traffic poses use), metres.</summary>
        public static float BodyAheadOf(in VehicleCatalogEntry e)
        {
            VehicleMesher.Dims d = VehicleMesher.DimsOf(e);
            return Math.Max(0f, 0.5f * d.Length - d.Rear);
        }

        /// <summary>
        /// Places the camera for this frame. <paramref name="view"/> is a chase view of the target's class (a mounted view
        /// gives the class rig); <paramref name="aspect"/> is the camera's width / height; <paramref name="ground"/> with the
        /// floating origin (<paramref name="ox"/>, <paramref name="oy"/>, <paramref name="oz"/>) keeps the camera above the
        /// terrain and turns scene positions into the game metres <see cref="Obstacles"/> answers in.
        /// </summary>
        public ChasePose Tick(float dt, in ChaseTarget target, CameraView view, in ChaseControls controls, float aspect, IGroundQuery ground,
                              double ox, double oy, double oz)
        {
            if (!(dt >= 0f) || float.IsInfinity(dt)) dt = 0f;
            if (dt > 0.1f) dt = 0.1f;
            if (!(aspect > 0f) || float.IsInfinity(aspect)) aspect = 16f / 9f;
            RigClass rigClass = target.Rig;
            bool aircraft = target.Aircraft;
            bool riding = rigClass != RigClass.Walk || aircraft;
            float speedMps = CharMath.Finite(target.SpeedMps);
            float headingDeg = CharMath.Finite(target.HeadingRad) * Quat.Rad2Deg;

            // Rig numbers: class, chase view and orientation, blended after a change.
            float portraitGoal = aspect < 1f ? 1f : 0f;
            _portrait = _snap ? portraitGoal : MoveTowards(_portrait, portraitGoal, dt / OrientationBlendSeconds);
            float pt = CharMath.SmoothStep(_portrait);
            RigParams want = aircraft
                ? Lerp(CameraRigTable.ForAircraft(target.AircraftLengthM, false), CameraRigTable.ForAircraft(target.AircraftLengthM, true), pt)
                : Lerp(CameraViews.Chase(rigClass, view, false, target.PassengerOf), CameraViews.Chase(rigClass, view, true, target.PassengerOf), pt);
            if (aircraft && view == CameraView.Far)
            {
                want.DistanceM *= 1.45f;
                want.PitchDeg += 4f;
            }
            float aimWant = !aircraft && CameraViews.Spec(view).AimAtPivot && !CameraViews.Spec(view).Mounted ? 1f : 0f;
            float blend = 1f - MathF.Exp(-3f * dt / (riding ? RigBlendSeconds : 0.35f));
            _rig = _snap ? want : Lerp(_rig, want, blend);
            _aim01 = _snap ? aimWant : CharMath.Lerp(_aim01, aimWant, blend);
            RigParams rig = _rig;

            // The followed point: the body's middle on the ground.
            float ahead = target.HasBody ? Math.Max(0f, CharMath.Finite(target.BodyAheadM)) : rig.BodyAheadM;
            float hr = headingDeg * Quat.Deg2Rad;
            float gx = target.Ground.X + MathF.Sin(hr) * ahead, gy = target.Ground.Y, gz = target.Ground.Z + MathF.Cos(hr) * ahead;
            if (!_snap)
            {
                float jx = gx - _ax.Value, jy = gy - _ay.Value, jz = gz - _az.Value;
                if (jx * jx + jy * jy + jz * jz > SnapDistanceM * SnapDistanceM) _snap = true;
            }
            if (_snap)
            {
                _ax.Reset(gx);
                _ay.Reset(gy);
                _az.Reset(gz);
                _lastGoalY = gy;
                _climb = 0f;
                _speed = Math.Max(0f, speedMps);
                _yaw.Reset(headingDeg);
                _lastHeading = headingDeg;
                _headingRate = 0f;
                _yawGoal = headingDeg;
                _yawGoalRate = 0f;
                _yawSmooth = rig.FollowS;
                _orbit = 0f;
                _orbitReturn.Reset(0f);
                _reverse.Reset();
                _boom.Snap();
                _lift.Reset(0f);
                _pathDeg = 0f;
            }
            else if (dt > 0f)
            {
                _ax.Step(gx, rig.PositionS, 0f, dt);
                _az.Step(gz, rig.PositionS, 0f, dt);
                float climbRaw = (gy - _lastGoalY) / dt;
                _lastGoalY = gy;
                _climb += (climbRaw - _climb) * (1f - MathF.Exp(-dt / ClimbSmoothS));
                // Aircraft climb and descend for real: no smoothing beyond the light one.
                float fed = aircraft ? _climb : _climb * CharMath.SmoothStep((Math.Abs(_climb) - ClimbGateMps) / (2f * ClimbGateMps));
                _ay.StepTracking(gy, fed, aircraft ? Math.Min(rig.HeightS, 0.15f) : rig.HeightS, dt);
                _speed += (Math.Max(0f, speedMps) - _speed) * (1f - MathF.Exp(-dt / SpeedSmoothS));
            }

            // Player zoom (log scale, so each notch feels the same), pitch and look-around. Riding, looking around is a
            // glance that springs back behind the rider; on foot it turns the camera for good (walking is relative to it).
            _zoomLog = CharMath.Clamp(_zoomLog - controls.ZoomSteps * 0.12f, MathF.Log(MinZoom), MathF.Log(MaxZoom));
            _pitchOffset = CharMath.Clamp(_pitchOffset + controls.LookPitchDeg, MinPitchOffsetDeg, MaxPitchOffsetDeg);
            bool looking = controls.LookYawDeg != 0f || controls.Looking;
            if (looking) _lookIdle = 0f;
            else _lookIdle += dt;
            if (riding)
            {
                _orbit = CameraAngleSpring.Wrap(_orbit + controls.LookYawDeg);
                if (looking || _lookIdle <= LookReturnDelayS)
                {
                    _orbitReturn.Reset(_orbit);
                }
                else
                {
                    _orbit = _orbitReturn.Step(0f, LookReturnS, 0f, dt);
                    if (Math.Abs(_orbit) < 0.01f) _orbit = 0f;
                }
            }
            else
            {
                if (controls.LookYawDeg != 0f) _yaw.Rotate(controls.LookYawDeg);
                _orbit *= MathF.Exp(-6f * dt);
            }

            // Yaw: behind the body, with a gentle lag in turns, not swinging round below walking pace; tighter in a lane.
            // (The goal, its turn rate and the smoothing are kept: the boom predicts where the yaw is going.)
            if (!_snap && dt > 0f)
            {
                float turn = CharMath.Clamp(CameraAngleSpring.Delta(_lastHeading, headingDeg) / dt, -360f, 360f);
                _headingRate += (turn - _headingRate) * (1f - MathF.Exp(-dt / TurnRateSmoothS));
                _lastHeading = headingDeg;
                if (riding)
                {
                    float follow = 1f;
                    if (!aircraft && rig.FollowFullMps > rig.FollowFromMps)
                        follow = CharMath.SmoothStep((Math.Abs(speedMps) - rig.FollowFromMps) / (rig.FollowFullMps - rig.FollowFromMps));
                    float goal = _yaw.Value + follow * CameraAngleSpring.Delta(_yaw.Value, headingDeg);
                    float smooth = rig.FollowS * (1f - LaneFollowTighten * _boom.Lane01);
                    _yaw.Step(goal, smooth, 0f, dt);
                    SetYawGoal(goal, follow * _headingRate, smooth);
                }
                else if (!looking && Math.Abs(speedMps) > 0.4f && Math.Abs(CameraAngleSpring.Delta(_yaw.Value, headingDeg)) < WalkFollowConeDeg)
                {
                    _yaw.Step(headingDeg, rig.FollowS, 0f, dt);
                    SetYawGoal(headingDeg, _headingRate, rig.FollowS);
                }
                else
                {
                    _yaw.Step(_yaw.Value, 0.3f, 0f, dt);
                    SetYawGoal(_yaw.Value, 0f, 0.3f);
                }
            }

            // Reversing a vehicle the player drives: raise, shorten, then swing round.
            bool drives = riding && !aircraft && rigClass != RigClass.Passenger;
            _reverse.Update(dt, drives ? speedMps : 0f, drives && !looking && _lookIdle > LookReturnDelayS);

            // Lens: the rig's FOV at this aspect plus a slow speed kick.
            float kick = !ReducedMotion && riding ? CameraRigTable.FovKick(rig, _speed) : 0f;
            float fov = CameraRigTable.VerticalFovDeg(rig, aspect, kick);
            _fov = _snap ? fov : CharMath.Lerp(_fov, fov, 1f - MathF.Exp(-8f * dt));

            // The boom from the pivot over the body's middle.
            float yaw = CameraAngleSpring.Wrap(_yaw.Value + _orbit + _reverse.SwingYawDeg);
            float pitch = rig.PitchDeg + _pitchOffset + _reverse.PitchAddDeg;
            if (aircraft)
            {
                float path = Math.Abs(speedMps) > 5f ? MathF.Atan2(_ay.Velocity, Math.Abs(speedMps)) * Quat.Rad2Deg : 0f;
                _pathDeg += (path - _pathDeg) * (1f - MathF.Exp(-dt / 0.5f));
                pitch -= CameraRigTable.AircraftPathShare * _pathDeg;
            }
            pitch = CharMath.Clamp(pitch, -5f, 75f);
            float distance = rig.DistanceM * MathF.Exp(_zoomLog) * _reverse.DistanceScale + (riding ? CameraRigTable.PullBack(rig, _speed) : 0f);
            var pivot = new V3(_ax.Value, _ay.Value + rig.PivotM, _az.Value);

            // Over the shoulder: the pivot steps right, but never into a wall beside the walker.
            IViewObstacleQuery obstacles = Obstacles;
            if (rig.ShoulderM > 1e-3f)
            {
                float yr = yaw * Quat.Deg2Rad;
                double rx = Math.Cos(yr), rz = -Math.Sin(yr);
                float reach = _boom.Reach(obstacles, pivot.X + ox, pivot.Y + oy, pivot.Z + oz, rx, 0.0, rz, rig.ShoulderM);
                _shoulder = _snap || reach < _shoulder ? reach : MoveTowards(_shoulder, reach, dt * 1.5f);
                pivot = pivot + new V3((float)rx, 0f, (float)rz) * _shoulder;
            }
            else
            {
                _shoulder = 0f;
            }

            float min = Math.Min(rig.MinDistanceM > 0f ? rig.MinDistanceM : (riding ? 4f : 2.5f), distance);
            // Where the pivot and the boom's yaw will be shortly (further ahead for a long boom), for the boom's early look.
            float horizon = ChaseBoom.PredictS * ChaseBoom.LongBoomScale(distance);
            float leadX = _ax.Velocity * horizon, leadZ = _az.Velocity * horizon;
            float leadYaw = CharMath.Clamp(_yaw.PredictTurn(_yawGoal, _yawGoalRate, _yawSmooth, horizon), -60f, 60f);
            _boom.Solve(obstacles, pivot.X + ox, pivot.Y + oy, pivot.Z + oz, yaw, pitch, distance, min, dt, leadX, leadZ, leadYaw);
            double bx, by, bz;
            _boom.CameraOffset(yaw, out bx, out by, out bz);
            var position = new V3(pivot.X + (float)bx, pivot.Y + (float)by, pivot.Z + (float)bz);

            // Never under the ground (a hillside behind, a dip): lifted smoothly, and never closer than GroundHardM.
            float need = 0f, floor = float.NegativeInfinity;
            GroundSample s;
            if (ground != null && SampleBelow(ground, position.X + ox, position.Y + oy, position.Z + oz, out s))
            {
                floor = (float)(s.Height - oy);
                need = Math.Max(0f, floor + GroundClearanceM - position.Y);
            }
            if (_snap) _lift.Reset(need);
            else _lift.Step(need, need > _lift.Value ? 0.08f : 0.4f, 0f, dt);
            position.Y += Math.Max(0f, _lift.Value);
            if (position.Y < floor + GroundHardM) position.Y = floor + GroundHardM;

            // Face the body's middle and pitch so its ground point stands at the framing height; the reversing frame lifts
            // it so the lane behind shows underneath. The shoulder view looks through its pivot instead.
            float foot = Math.Min(0.2f, CameraRigTable.FootNdc(rig, _speed) + _reverse.FootRaise * CharMath.Lerp(LandscapeFootRaiseShare, 1f, _portrait));
            _foot = _snap ? foot : CharMath.Lerp(_foot, foot, 1f - MathF.Exp(-4f * dt));
            Quat look = Quat.Euler(_boom.PitchDeg, yaw, 0f);
            float fx = _ax.Value - position.X, fz = _az.Value - position.Z;
            float horizontal = MathF.Sqrt(fx * fx + fz * fz);
            Quat rotation = look;
            if (horizontal > 1e-3f)
            {
                float above = position.Y - _ay.Value;
                float viewPitch = ViewPitchDeg(above, horizontal, _foot, _fov);
                float viewYaw = MathF.Atan2(fx, fz) * Quat.Rad2Deg;
                rotation = Quat.Euler(CharMath.Clamp(viewPitch, -80f, 89f), viewYaw, 0f);
            }
            if (_aim01 > 1e-3f)
            {
                V3 to = pivot - position;
                Quat through = to.LengthSq > 1e-6f ? LookRotation(to) : look;
                rotation = Quat.Slerp(rotation, through, CharMath.Clamp01(_aim01));
            }
            // No room behind (backed against a house): look down the way ahead from over the player's head.
            float overhead = _boom.OverheadLook01;
            if (overhead > 1e-3f) rotation = Quat.Slerp(rotation, Quat.Euler(ChaseBoom.OverheadLookDownDeg(_portrait), yaw, 0f), overhead);
            // An aircraft's view banks a little with it (the horizon otherwise stays level, as for every vehicle).
            if (aircraft && !ReducedMotion) rotation = rotation * Quat.Euler(0f, 0f, -CharMath.Finite(target.BankRad) * Quat.Rad2Deg * CameraRigTable.AircraftBankShare);

            YawDeg = yaw;
            _snap = false;
            return new ChasePose { Position = position, Rotation = rotation, VerticalFovDeg = _fov, YawDeg = yaw };
        }

        private void SetYawGoal(float goalDeg, float rateDegPerS, float smoothS)
        {
            _yawGoal = goalDeg;
            _yawGoalRate = rateDegPerS;
            _yawSmooth = smoothS;
        }

        /// <summary>
        /// Downward pitch of the view (degrees) that shows a point <paramref name="cameraAbovePointM"/> below the camera
        /// and <paramref name="horizontalDistanceM"/> in front of it at screen height <paramref name="screenY"/> (NDC)
        /// with a vertical field of view of <paramref name="verticalFovDeg"/>.
        /// </summary>
        public static float ViewPitchDeg(float cameraAbovePointM, float horizontalDistanceM, float screenY, float verticalFovDeg)
        {
            double tanHalf = Math.Tan(verticalFovDeg * 0.5 * Math.PI / 180.0);
            double below = Math.Atan2(cameraAbovePointM, Math.Max(1e-3, horizontalDistanceM));
            return (float)((below + Math.Atan(screenY * tanHalf)) * 180.0 / Math.PI);
        }

        /// <summary>Component-wise blend of two rigs; <paramref name="t"/> clamped to [0, 1] (flags from the nearer).</summary>
        public static RigParams Lerp(in RigParams a, in RigParams b, float t)
        {
            t = CharMath.Clamp01(t);
            RigParams r = t < 0.5f ? a : b;
            r.DistanceM = L(a.DistanceM, b.DistanceM, t);
            r.PitchDeg = L(a.PitchDeg, b.PitchDeg, t);
            r.PivotM = L(a.PivotM, b.PivotM, t);
            r.LookAheadS = L(a.LookAheadS, b.LookAheadS, t);
            r.LookAheadMaxM = L(a.LookAheadMaxM, b.LookAheadMaxM, t);
            r.MinHFovDeg = L(a.MinHFovDeg, b.MinHFovDeg, t);
            r.MinVFovDeg = L(a.MinVFovDeg, b.MinVFovDeg, t);
            r.FollowS = L(a.FollowS, b.FollowS, t);
            r.FovKickDeg = L(a.FovKickDeg, b.FovKickDeg, t);
            r.KickFromMps = L(a.KickFromMps, b.KickFromMps, t);
            r.KickFullMps = L(a.KickFullMps, b.KickFullMps, t);
            r.MinDistanceM = L(a.MinDistanceM, b.MinDistanceM, t);
            r.ShoulderM = L(a.ShoulderM, b.ShoulderM, t);
            r.FootRestNdc = L(a.FootRestNdc, b.FootRestNdc, t);
            r.FootFastNdc = L(a.FootFastNdc, b.FootFastNdc, t);
            r.PositionS = L(a.PositionS, b.PositionS, t);
            r.HeightS = L(a.HeightS, b.HeightS, t);
            r.PullBackM = L(a.PullBackM, b.PullBackM, t);
            r.PullFullMps = L(a.PullFullMps, b.PullFullMps, t);
            r.FollowFromMps = L(a.FollowFromMps, b.FollowFromMps, t);
            r.FollowFullMps = L(a.FollowFullMps, b.FollowFullMps, t);
            r.BodyAheadM = L(a.BodyAheadM, b.BodyAheadM, t);
            return r;
        }

        private static float L(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta) return target;
            return current + Math.Sign(target - current) * maxDelta;
        }

        /// <summary>The ground under the camera at its own level (under a flyover: the road below, not the deck above).</summary>
        private static bool SampleBelow(IGroundQuery ground, double x, double y, double z, out GroundSample s)
        {
            var layered = ground as ILayeredGroundQuery;
            if (layered != null) return layered.TrySample(x, z, (float)y, out s);
            return ground.TrySample(x, z, out s);
        }

        /// <summary>A rotation looking along <paramref name="forward"/> with world up (Unity's <c>LookRotation</c>).</summary>
        private static Quat LookRotation(V3 forward)
        {
            V3 f = forward.Normalized;
            float yaw = MathF.Atan2(f.X, f.Z) * Quat.Rad2Deg;
            float pitch = -MathF.Asin(CharMath.Clamp(f.Y, -1f, 1f)) * Quat.Rad2Deg;
            return Quat.Euler(pitch, yaw, 0f);
        }
    }
}
