using System;
using Ghumante.Core.Characters;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using Ghumante.Core.Streaming;
using Ghumante.World;
using Ghumante.World.Cameras;
using UnityEngine;

namespace Ghumante.Characters.Cameras
{
    /// <summary>
    /// Where a mounted camera (<see cref="CameraViewKind.Eye"/>, <see cref="CameraViewKind.Hood"/>) sits this frame, from
    /// <see cref="ExplorerController.TryGetCameraMount"/>: the rider's head bone, the body frame of the vehicle (its
    /// reference point on the ground under the rear axle, heading, nose-up pitch and roll as drawn) and the bonnet point.
    /// </summary>
    public struct CameraMount
    {
        /// <summary>The head bone (base of the skull), scene space.</summary>
        public Vector3 Head;

        /// <summary>The vehicle's reference point, scene space.</summary>
        public Vector3 Origin;

        /// <summary>Body heading (0 = north, clockwise), nose-up pitch and roll (Unity z, as the body is drawn), degrees.</summary>
        public float HeadingDeg, PitchDeg, RollDeg;

        /// <summary>The bonnet camera in the body frame (<see cref="CameraViews.HoodEye"/>); valid with <see cref="HasHood"/>.</summary>
        public Vector3 HoodLocal;

        public bool HasHood;
    }

    /// <summary>What the camera follows this frame (<see cref="ExplorerController.GetCameraTarget"/> plus the rig class
    /// and, when seated, the mount).</summary>
    public struct CameraTarget
    {
        /// <summary>The controlled body's ground point, scene space.</summary>
        public Vector3 Ground;

        public float HeadingRad;

        /// <summary>Signed speed along the heading, m/s (negative reversing).</summary>
        public float SpeedMps;

        public float LeanRad;
        public RigClass Rig;

        /// <summary>The driver rig of the vehicle ridden along (<see cref="RigClass.Passenger"/> orbits at 1.2× it).</summary>
        public RigClass PassengerOf;

        /// <summary>Seated in a vehicle: the eye and hood views can be used.</summary>
        public bool HasMount;

        public CameraMount Mount;
    }

    /// <summary>
    /// The Explore camera (M1 track D, W2 detail pass; ARCHITECTURE.md 7.10a): a chase camera that trails the explorer
    /// with spring smoothing, tilts up the road with speed while keeping the explorer in the lower part of the frame, and
    /// frames by class, view and orientation (<see cref="ChaseRigProfile"/>, <see cref="CameraViews"/>); or a mounted
    /// camera on the handlebar, the bonnet, the driver's or a passenger's seat.
    /// <list type="bullet">
    /// <item><b>Views</b>: each class remembers its view in <see cref="Views"/> (<see cref="CameraViewMemory"/>, saved);
    /// a change of view or class blends over <see cref="CameraViews.BlendSeconds"/>, a rotation over 0.3 s.</item>
    /// <item><b>Collision</b>: the boom is swept against <see cref="Obstacles"/> (<see cref="ChaseBoom"/>): it pulls in
    /// at once, never inside or behind a wall, eases back out, lifts over low walls and goes steeper in narrow lanes; the
    /// over-the-shoulder pivot is kept out of walls too.</item>
    /// <item><b>Reversing</b> (<see cref="ReverseFraming"/>): the camera rises and shortens so the lane behind shows,
    /// then swings round to look along the travel.</item>
    /// <item><b>Lens</b>: the vertical field of view keeps the view's minimum horizontal FOV at any aspect
    /// (<see cref="CameraFov"/>), plus a small speed kick; the near plane is 0.4 m behind the player and 0.1 m on board.</item>
    /// <item><b>Player input</b>: zoom (wheel, pinch, shoulders), pitch and look-around (right-drag, two-finger drag,
    /// right stick); riding, looking around springs back behind the explorer when let go.</item>
    /// </list>
    /// It never dips under the ground, and follows floating-origin shifts. Clip planes come from
    /// <see cref="WorldRoot.ConfigureCamera"/> (far = the tier's view radius plus margin). A plain class driven by its
    /// owner: <see cref="Attach"/>, then <see cref="Tick(float, in CameraTarget, in ControlFrame, IGroundQuery, WorldPos)"/>
    /// every LateUpdate, then <see cref="Detach"/> (restores the camera). No allocation per frame.
    /// </summary>
    public sealed class ChaseCameraRig
    {
        public const float NearClipM = CameraViews.ChaseNearClipM;
        public const float OrientationBlendSeconds = 0.3f;
        public const float ModeBlendSeconds = 0.45f;
        public const float MinZoom = 0.55f, MaxZoom = 2.6f;
        public const float MinPitchOffsetDeg = -9f, MaxPitchOffsetDeg = 40f;

        /// <summary>Mounted views: the player may tilt the view this far up or down, degrees.</summary>
        public const float MountedPitchLimitDeg = 40f;

        /// <summary>Lowest the camera may get above the ground.</summary>
        public const float GroundClearanceM = 0.8f;

        /// <summary>Seconds after the last look input before the camera swings back behind the rider.</summary>
        public const float LookReturnDelayS = 1.4f;

        /// <summary>On foot the camera swings in behind only while the walker heads within this angle of its view.</summary>
        public const float WalkFollowConeDeg = 35f;

        /// <summary>Share of <see cref="ReverseFraming.FootRaise"/> used in landscape (the view is short there).</summary>
        public const float LandscapeFootRaiseShare = 0.35f;

        /// <summary>Head-bone jitter (gait, bumps) is smoothed out of the eye views over about this long.</summary>
        public const float EyeSmoothingS = 0.06f;

        private Camera _camera;
        private Vector3 _savedPosition;
        private Quaternion _savedRotation;
        private float _savedFov;
        private float _savedNear;
        private bool _snap = true;

        private float _portrait;
        private RigClass _rigClass = RigClass.TwoWheeler;
        private CameraView _view = CameraView.Near;
        private bool _mounted;
        private ChaseRigProfile _profile = ChaseRigProfile.RideLandscape;
        private float _zoomLog;
        private float _pitchOffset;
        private float _orbit;
        private float _lookIdle = 999f;
        private float _yaw;
        private float _yawVelocity;
        private Vector3 _aim;
        private Vector3 _aimVelocity;
        private float _footY;
        private float _fov;
        private float _shoulder;
        private Vector3 _eyeLocal;
        private bool _eyeValid;
        private float _cameraYawDeg;

        private readonly ChaseBoom _boom = new ChaseBoom();
        private readonly ReverseFraming _reverse = new ReverseFraming();

        // Pose blend after a change of view or class: the old pose relative to the target's ground and heading.
        private bool _blending;
        private float _blendT;
        private Vector3 _blendFromLocal;
        private Quaternion _blendFromRot;
        private float _blendFromNear;

        public Camera Camera
        {
            get { return _camera; }
        }

        /// <summary>The camera's heading (radians, 0 = north, clockwise; Unity yaw): walking is relative to it, and the
        /// HUD compass shows it.</summary>
        public float YawRad
        {
            get { return _cameraYawDeg * Mathf.Deg2Rad; }
        }

        /// <summary>0 in landscape, 1 in portrait (blending between them after a rotation).</summary>
        public float PortraitBlend
        {
            get { return _portrait; }
        }

        /// <summary>No roll, speed FOV kick or shake (Settings, Reduce motion).</summary>
        public bool ReducedMotion { get; set; }

        /// <summary>Solid world geometry the boom must not enter (Track COLLIDE's <see cref="IViewObstacleQuery"/>, game
        /// metres). Null: no collision but the ground clamp.</summary>
        public IViewObstacleQuery Obstacles { get; set; }

        /// <summary>The view per class (the save's <c>settings.camera_views</c>). Null: every class uses its default.</summary>
        public CameraViewMemory Views { get; set; }

        /// <summary>The rig the camera is on (or blending to).</summary>
        public RigClass Rig
        {
            get { return _rigClass; }
        }

        /// <summary>The view the camera shows now (a mounted view falls back to the class default while not seated).</summary>
        public CameraView View
        {
            get { return _view; }
        }

        /// <summary>True while on a mounted (eye or bonnet) view.</summary>
        public bool Mounted
        {
            get { return _mounted; }
        }

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

        /// <summary>The driver rig of the vehicle ridden along: <see cref="RigClass.Passenger"/> orbits at 1.2× it
        /// (W2_DESIGN 6.4), so a bus ride frames the bus. Car by default.</summary>
        public RigClass PassengerOf { get; set; } = RigClass.Car;

        /// <summary>Takes over <paramref name="camera"/>: saves its transform, field of view and near plane (restored by
        /// <see cref="Detach"/>) and sets the world clip planes for <paramref name="config"/>.</summary>
        public void Attach(Camera camera, StreamingConfig config)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            Detach();
            _camera = camera;
            Transform t = camera.transform;
            _savedPosition = t.position;
            _savedRotation = t.rotation;
            _savedFov = camera.fieldOfView;
            _savedNear = camera.nearClipPlane;
            WorldRoot.ConfigureCamera(camera, config);
            camera.nearClipPlane = NearClipM;
            _snap = true;
        }

        /// <summary>Gives the camera back as it was before <see cref="Attach"/>.</summary>
        public void Detach()
        {
            if (_camera == null) return;
            _camera.transform.SetPositionAndRotation(_savedPosition, _savedRotation);
            _camera.fieldOfView = _savedFov;
            _camera.nearClipPlane = _savedNear;
            _camera = null;
        }

        /// <summary>Jump to the target next tick (spawn, teleport): no smoothing from the old place.</summary>
        public void Snap()
        {
            _snap = true;
        }

        /// <summary>The floating origin moved by <paramref name="delta"/> (new − old): shift the smoothed state.</summary>
        public void ShiftOrigin(WorldPos delta)
        {
            var d = new Vector3((float)delta.X, delta.Y, (float)delta.Z);
            _aim -= d;
            if (_camera != null) _camera.transform.position -= d;
        }

        /// <summary>M1 overload: the scooter rig while <paramref name="riding"/>, else the walking rig.</summary>
        public void Tick(float dt, Vector3 target, float headingRad, float speedMps, float leanRad, bool riding,
                         in ControlFrame controls, IGroundQuery ground, WorldPos origin)
        {
            Tick(dt, target, headingRad, speedMps, leanRad, riding ? RigClass.TwoWheeler : RigClass.Walk, controls, ground, origin);
        }

        /// <summary>W2 overload: a chase view of <paramref name="rigClass"/> (no mount).</summary>
        public void Tick(float dt, Vector3 target, float headingRad, float speedMps, float leanRad, RigClass rigClass,
                         in ControlFrame controls, IGroundQuery ground, WorldPos origin)
        {
            var t = new CameraTarget
            {
                Ground = target, HeadingRad = headingRad, SpeedMps = speedMps, LeanRad = leanRad, Rig = rigClass, PassengerOf = PassengerOf,
            };
            Tick(dt, t, controls, ground, origin);
        }

        /// <summary>
        /// Places the camera for this frame. <paramref name="target"/> carries the explorer's ground point (scene space),
        /// heading, signed speed, lean, rig class and, when seated, the mount; <paramref name="controls"/> carries zoom and
        /// look input; <paramref name="ground"/> with <paramref name="origin"/> keeps the camera above the terrain and
        /// turns scene positions into the game metres <see cref="Obstacles"/> answers in.
        /// </summary>
        public void Tick(float dt, in CameraTarget target, in ControlFrame controls, IGroundQuery ground, WorldPos origin)
        {
            if (_camera == null) return;
            if (!(dt >= 0f) || float.IsInfinity(dt)) dt = 0f;
            dt = Mathf.Min(dt, 0.1f);
            RigClass rigClass = target.Rig;
            bool riding = rigClass != RigClass.Walk;
            PassengerOf = target.PassengerOf;

            // The view: the class's remembered choice; a mounted view needs a seat (else the class default).
            CameraView chosen = Views != null ? Views.Get(rigClass) : CameraViews.Default(rigClass);
            CameraViewSpec spec = CameraViews.Spec(chosen);
            bool mounted = spec.Mounted && target.HasMount && (spec.Kind != CameraViewKind.Hood || target.Mount.HasHood);
            if (spec.Mounted && !mounted)
            {
                chosen = CameraViews.Default(rigClass);
                spec = CameraViews.Spec(chosen);
            }
            if (!_snap && (mounted != _mounted || mounted && chosen != _view)) BeginBlend(target);
            _view = chosen;
            _mounted = mounted;
            if (_snap) _blending = false;

            // Rig blends: orientation from the camera's own aspect (what the framing is for), class and chase view from the
            // controller and the memory.
            float portraitTarget = _camera.aspect < 1f ? 1f : 0f;
            _portrait = _snap ? portraitTarget : Mathf.MoveTowards(_portrait, portraitTarget, dt / OrientationBlendSeconds);
            CameraView chaseView = mounted ? CameraViews.Default(rigClass) : chosen;
            ChaseRigProfile want = ChaseRigProfile.For(rigClass, Smooth(_portrait), PassengerOf, chaseView);
            float blendS = rigClass == RigClass.Walk ? 0.35f : ModeBlendSeconds;
            _rigClass = rigClass;
            _profile = _snap ? want : ChaseRigProfile.Lerp(_profile, want, 1f - Mathf.Exp(-3f * dt / blendS));
            ChaseRigProfile rig = _profile;

            // Player zoom (log scale, so each notch feels the same), pitch and look-around. Riding, looking around is a
            // glance that swings back behind the rider; on foot it turns the camera for good (walking is relative to it).
            _zoomLog = Mathf.Clamp(_zoomLog - controls.ZoomSteps * 0.12f, Mathf.Log(MinZoom), Mathf.Log(MaxZoom));
            _pitchOffset = Mathf.Clamp(_pitchOffset + controls.LookPitchDeg, mounted ? -MountedPitchLimitDeg : MinPitchOffsetDeg,
                                       mounted ? MountedPitchLimitDeg : MaxPitchOffsetDeg);
            bool looking = controls.LookYawDeg != 0f || controls.Looking;
            if (looking) _lookIdle = 0f;
            else _lookIdle += dt;
            if (riding)
            {
                _orbit = Mathf.Repeat(_orbit + controls.LookYawDeg + 180f, 360f) - 180f;
                if (_lookIdle > LookReturnDelayS) _orbit *= Mathf.Exp(-2.5f * dt);
            }
            else
            {
                _yaw += controls.LookYawDeg;
                _orbit *= Mathf.Exp(-6f * dt);
            }

            // Follow the heading: tightly riding. On foot only while walking roughly away from the camera, so walking
            // towards it or sideways (the stick is camera-relative) never sends camera and walker chasing each other.
            float headingDeg = target.HeadingRad * Mathf.Rad2Deg;
            float speedMps = target.SpeedMps;
            if (_snap)
            {
                _yaw = headingDeg;
                _yawVelocity = 0f;
            }
            else if (riding)
            {
                _yaw = Mathf.SmoothDampAngle(_yaw, headingDeg, ref _yawVelocity, rig.FollowSeconds, Mathf.Infinity, dt);
            }
            else if (!looking && Mathf.Abs(speedMps) > 0.4f && Mathf.Abs(Mathf.DeltaAngle(_yaw, headingDeg)) < WalkFollowConeDeg)
            {
                _yaw = Mathf.SmoothDampAngle(_yaw, headingDeg, ref _yawVelocity, rig.FollowSeconds, 90f, dt);
            }
            else
            {
                _yawVelocity = 0f;
            }

            // Reversing a vehicle the player drives: raise, shorten, then swing round (chase views only).
            if (_snap) _reverse.Reset();
            bool drives = riding && rigClass != RigClass.Passenger && !mounted;
            _reverse.Update(dt, drives ? speedMps : 0f, drives && !looking && _lookIdle > LookReturnDelayS);

            // The pivot follows the explorer with a little lag (always behind it, never ahead).
            Vector3 pivot = target.Ground + new Vector3(0f, rig.AimHeightM, 0f);
            if (_snap)
            {
                _aim = pivot;
                _aimVelocity = Vector3.zero;
            }
            else
            {
                _aim = Vector3.SmoothDamp(_aim, pivot, ref _aimVelocity, 0.06f, Mathf.Infinity, dt);
            }

            // Vertical FOV from the view's minimum horizontal FOV at this aspect, plus a little speed kick riding.
            float fovH = mounted ? spec.MinHFovDeg : rig.MinHorizontalFovDeg;
            float kick = !ReducedMotion && riding ? CameraRigTable.FovKick(CameraRigTable.For(rigClass, _portrait > 0.5f, PassengerOf), speedMps) : 0f;
            float aspect = Mathf.Max(0.1f, _camera.aspect);
            float fov = CameraFov.VerticalFromHorizontal(Mathf.Clamp(fovH + kick, 1f, 179f), aspect);
            _fov = _snap ? fov : Mathf.Lerp(_fov, fov, 1f - Mathf.Exp(-8f * dt));

            Vector3 position;
            Quaternion rotation;
            float near;
            if (mounted)
            {
                MountedPose(dt, target, spec, out position, out rotation);
                near = spec.NearClipM;
                _cameraYawDeg = target.Mount.HeadingDeg + _orbit;
            }
            else
            {
                ChasePose(dt, target, rig, riding, rigClass, origin, ground, out position, out rotation);
                near = NearClipM;
                _cameraYawDeg = _yaw + _orbit + _reverse.SwingYawDeg;
            }

            // A change of view or class: ease from the old pose (kept relative to the explorer) to the new one.
            if (_blending)
            {
                _blendT += dt / CameraViews.BlendSeconds;
                if (_blendT >= 1f)
                {
                    _blending = false;
                }
                else
                {
                    float s = Smooth(_blendT);
                    Quaternion frame = Quaternion.Euler(0f, headingDeg, 0f);
                    Vector3 from = target.Ground + frame * _blendFromLocal;
                    Quaternion fromRot = frame * _blendFromRot;
                    position = Vector3.Lerp(from, position, s);
                    rotation = Quaternion.Slerp(fromRot, rotation, s);
                    near = Mathf.Min(near, _blendFromNear);
                }
            }

            _camera.fieldOfView = _fov;
            if (!Mathf.Approximately(_camera.nearClipPlane, near)) _camera.nearClipPlane = near;
            _camera.transform.SetPositionAndRotation(position, rotation);
            _snap = false;
        }

        /// <summary>The chase camera: boom behind the (shoulder) pivot, reversing frame, collision, ground clamp, then the
        /// view that pins the explorer's ground point on screen (or looks through the pivot).</summary>
        private void ChasePose(float dt, in CameraTarget target, in ChaseRigProfile rig, bool riding, RigClass rigClass, WorldPos origin,
                               IGroundQuery ground, out Vector3 position, out Quaternion rotation)
        {
            float speedMps = target.SpeedMps;
            float yaw = _yaw + _orbit + _reverse.SwingYawDeg;
            float pitch = Mathf.Clamp(rig.PitchDeg + _pitchOffset + _reverse.PitchAddDeg, -5f, 75f);
            float distance = rig.DistanceM * Mathf.Exp(_zoomLog) * _reverse.DistanceScale;
            IViewObstacleQuery obstacles = Obstacles;

            // Over the shoulder: the pivot steps right, but never into a wall beside the walker.
            Vector3 pivot = _aim;
            float shoulderWant = rig.ShoulderM;
            if (shoulderWant > 1e-3f)
            {
                float yawRad = yaw * Mathf.Deg2Rad;
                double rx = Math.Cos(yawRad), rz = -Math.Sin(yawRad);
                float reach = _boom.Reach(obstacles, pivot.x + origin.X, pivot.y + origin.Y, pivot.z + origin.Z, rx, 0.0, rz, shoulderWant);
                _shoulder = _snap || reach < _shoulder ? reach : Mathf.MoveTowards(_shoulder, reach, dt * 1.5f);
                pivot += new Vector3((float)rx, 0f, (float)rz) * _shoulder;
            }
            else
            {
                _shoulder = 0f;
            }

            if (_snap) _boom.Snap();
            float min = Mathf.Min(rig.MinDistanceM > 0f ? rig.MinDistanceM : (riding ? 4f : 2.5f), distance);
            _boom.Solve(obstacles, pivot.x + origin.X, pivot.y + origin.Y, pivot.z + origin.Z, yaw, pitch, distance, min, dt);
            double dx, dy, dz;
            ChaseBoom.Direction(yaw, _boom.PitchDeg, out dx, out dy, out dz);
            float d = _boom.DistanceM;
            position = pivot + new Vector3((float)dx * d, (float)dy * d, (float)dz * d);

            // Never under the ground (a hillside behind, a dip): lift and keep framing the explorer.
            GroundSample s;
            if (ground != null && ground.TrySample(position.x + origin.X, position.z + origin.Z, out s))
            {
                float floor = s.Height - origin.Y + GroundClearanceM;
                if (position.y < floor) position.y = floor;
            }

            // Face the explorer and pitch so its (smoothed) ground point stands at the framing height; the reversing frame
            // lifts it so the lane behind shows underneath. The shoulder view looks through its pivot instead.
            // (Landscape's short vertical view raises it less, so the rider stays in frame.)
            float footY = Mathf.Min(0.2f, rig.FootScreenY(speedMps) + _reverse.FootRaise * Mathf.Lerp(LandscapeFootRaiseShare, 1f, _portrait));
            _footY = _snap ? footY : Mathf.Lerp(_footY, footY, 1f - Mathf.Exp(-3f * dt));
            Quaternion look = Quaternion.Euler(_boom.PitchDeg, yaw, 0f);
            float footX = _aim.x - position.x;
            float footZ = _aim.z - position.z;
            float horizontal = Mathf.Sqrt(footX * footX + footZ * footZ);
            Quaternion pin = look;
            if (horizontal > 1e-3f)
            {
                float aboveFoot = position.y - (_aim.y - rig.AimHeightM);
                float viewPitch = ChaseRigProfile.ViewPitchDeg(aboveFoot, horizontal, _footY, _fov);
                float viewYaw = Mathf.Atan2(footX, footZ) * Mathf.Rad2Deg;
                pin = Quaternion.Euler(Mathf.Clamp(viewPitch, -80f, 89f), viewYaw, 0f);
            }
            rotation = pin;
            if (rig.Aim01 > 1e-3f)
            {
                Vector3 toPivot = pivot - position;
                Quaternion through = toPivot.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(toPivot, Vector3.up) : look;
                rotation = Quaternion.Slerp(pin, through, Mathf.Clamp01(rig.Aim01));
            }
            if (!ReducedMotion && (rigClass == RigClass.TwoWheeler || rigClass == RigClass.Bicycle))
            {
                rotation *= Quaternion.Euler(0f, 0f, -target.LeanRad * Mathf.Rad2Deg * 0.12f);
            }
        }

        /// <summary>The eye and bonnet views: rigid to the body (heading and pitch, part of its roll), the head bone's
        /// jitter smoothed out, plus the player's glance.</summary>
        private void MountedPose(float dt, in CameraTarget target, in CameraViewSpec spec, out Vector3 position, out Quaternion rotation)
        {
            CameraMount m = target.Mount;
            Quaternion body = Quaternion.Euler(-m.PitchDeg, m.HeadingDeg, m.RollDeg);
            Vector3 local;
            if (spec.Kind == CameraViewKind.Hood)
            {
                local = m.HoodLocal;
            }
            else
            {
                Quaternion level = Quaternion.Euler(-m.PitchDeg, m.HeadingDeg, 0f);
                Vector3 eye = m.Head + level * new Vector3(0f, CameraViews.EyeUpM, CameraViews.EyeForwardM);
                local = Quaternion.Inverse(body) * (eye - m.Origin);
            }
            if (_snap || !_eyeValid || spec.Kind == CameraViewKind.Hood) _eyeLocal = local;
            else _eyeLocal = Vector3.Lerp(_eyeLocal, local, 1f - Mathf.Exp(-dt / EyeSmoothingS));
            _eyeValid = true;
            position = m.Origin + body * _eyeLocal;
            float roll = ReducedMotion ? 0f : m.RollDeg * spec.RollShare;
            rotation = Quaternion.Euler(-m.PitchDeg + spec.LookDownDeg + _pitchOffset, m.HeadingDeg + _orbit, roll);
        }

        private void BeginBlend(in CameraTarget target)
        {
            Transform t = _camera.transform;
            Quaternion frame = Quaternion.Euler(0f, target.HeadingRad * Mathf.Rad2Deg, 0f);
            Quaternion inverse = Quaternion.Inverse(frame);
            _blendFromLocal = inverse * (t.position - target.Ground);
            _blendFromRot = inverse * t.rotation;
            _blendFromNear = _camera.nearClipPlane;
            _blendT = 0f;
            _blending = true;
            _eyeValid = false;
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
