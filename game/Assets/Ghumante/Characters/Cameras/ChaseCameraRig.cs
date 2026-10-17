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

        /// <summary>Half the body's width, metres: the window view leans out just past it (<see cref="CameraViews.WindowX"/>).</summary>
        public float HalfWidthM;
    }

    /// <summary>What the camera follows this frame (<see cref="ExplorerController.GetCameraTarget"/> plus the rig class
    /// and, when seated, the mount).</summary>
    public struct CameraTarget
    {
        /// <summary>The controlled body's ground point, scene space (a vehicle's point under its rear axle).</summary>
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

        /// <summary>With <see cref="HasBody"/>: the middle of the body ahead of <see cref="Ground"/>, metres
        /// (<see cref="ChaseCamera.BodyAheadOf"/>); the chase camera orbits the middle of a bus, not its rear axle.</summary>
        public float BodyAheadM;

        public bool HasBody;

        /// <summary>An aircraft (for the flight package): the chase rig is <see cref="CameraRigTable.ForAircraft"/> of
        /// <see cref="AircraftLengthM"/> and the view banks with a little of <see cref="BankRad"/>.</summary>
        public bool Aircraft;

        public float AircraftLengthM;
        public float BankRad;
    }

    /// <summary>
    /// The Explore camera (M1 track D, W2 detail pass, playtest-fix round; ARCHITECTURE.md 7.10a): the Unity driver of
    /// the engine-free <see cref="ChaseCamera"/> (docs/research/w2/ref_camera.md), plus the mounted cameras on the
    /// handlebar, the bonnet or the driver's seat (inside a closed body the vehicle shows a stand-in cockpit,
    /// <see cref="Rides.DrivenVehicleView.UpdateCockpit"/>), or leaning out of a passenger's window.
    /// <list type="bullet">
    /// <item><b>Chase views</b> (near, far, over the shoulder, low cinematic, high chase): <see cref="ChaseCamera"/> places
    /// the camera with exact critically damped springs, follows the body's middle and heading (never its suspension, lean
    /// or bumps), and sweeps a predictive, hysteretic boom against <see cref="Obstacles"/>.</item>
    /// <item><b>Views</b>: each class remembers its view in <see cref="Views"/> (<see cref="CameraViewMemory"/>, saved);
    /// a change of view or class blends over <see cref="CameraViews.BlendSeconds"/>, a rotation over 0.3 s.</item>
    /// <item><b>Lens</b>: landscape keeps the rig's minimum vertical FOV (Hor+), portrait its minimum horizontal FOV
    /// (<see cref="CameraRigTable.VerticalFovDeg"/>), plus a slow speed kick; the near plane is 0.4 m behind the player and
    /// 0.1 m on board.</item>
    /// <item><b>Player input</b>: zoom (wheel, pinch, shoulders), pitch and look-around (right-drag, two-finger drag,
    /// right stick); riding, looking around springs back behind the explorer when let go.</item>
    /// </list>
    /// Clip planes come from <see cref="WorldRoot.ConfigureCamera"/> (far = the tier's view radius plus margin). A plain
    /// class driven by its owner: <see cref="Attach"/>, then <see cref="Tick(float, in CameraTarget, in ControlFrame, IGroundQuery, WorldPos)"/>
    /// every LateUpdate with the same (smoothed) frame time the explorer moved by (<see cref="CameraClock"/>), then
    /// <see cref="Detach"/> (restores the camera). No allocation per frame.
    /// </summary>
    public sealed class ChaseCameraRig
    {
        public const float NearClipM = CameraViews.ChaseNearClipM;
        public const float OrientationBlendSeconds = ChaseCamera.OrientationBlendSeconds;
        public const float MinZoom = ChaseCamera.MinZoom, MaxZoom = ChaseCamera.MaxZoom;
        public const float MinPitchOffsetDeg = ChaseCamera.MinPitchOffsetDeg, MaxPitchOffsetDeg = ChaseCamera.MaxPitchOffsetDeg;

        /// <summary>Mounted views: the player may tilt the view this far up or down, degrees.</summary>
        public const float MountedPitchLimitDeg = 40f;

        /// <summary>Lowest the camera may get above the ground.</summary>
        public const float GroundClearanceM = ChaseCamera.GroundClearanceM;

        /// <summary>Seconds after the last look input before the camera swings back behind the rider.</summary>
        public const float LookReturnDelayS = ChaseCamera.LookReturnDelayS;

        /// <summary>Head-bone jitter (gait, bumps) is smoothed out of the eye views over about this long.</summary>
        public const float EyeSmoothingS = 0.06f;

        private Camera _camera;
        private Vector3 _savedPosition;
        private Quaternion _savedRotation;
        private float _savedFov;
        private float _savedNear;
        private bool _snap = true;

        private readonly ChaseCamera _chase = new ChaseCamera();
        private RigClass _rigClass = RigClass.TwoWheeler;
        private CameraView _view = CameraView.Near;
        private bool _mounted;
        private float _mountPitch;
        private float _orbit;
        private float _lookIdle = 999f;
        private float _fov;
        private Vector3 _eyeLocal;
        private bool _eyeValid;
        private float _cameraYawDeg;

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
            get { return _chase.Portrait01; }
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

        /// <summary>The engine-free chase camera this rig drives (springs, framing, boom), for diagnostics and tests.</summary>
        public ChaseCamera Chase
        {
            get { return _chase; }
        }

        /// <summary>The boom (pull-in, lift, lane mode), for diagnostics and tests.</summary>
        public ChaseBoom Boom
        {
            get { return _chase.Boom; }
        }

        /// <summary>The reversing frame, for diagnostics and tests.</summary>
        public ReverseFraming Reverse
        {
            get { return _chase.Reverse; }
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
            Snap();
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
            _chase.Snap();
        }

        /// <summary>The floating origin moved by <paramref name="delta"/> (new − old): shift the smoothed state.</summary>
        public void ShiftOrigin(WorldPos delta)
        {
            _chase.ShiftOrigin(delta.X, delta.Y, delta.Z);
            var d = new Vector3((float)delta.X, delta.Y, (float)delta.Z);
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
        /// turns scene positions into the game metres <see cref="Obstacles"/> answers in. <paramref name="dt"/> must be the
        /// time the explorer moved by this frame (the same <see cref="CameraClock"/> tick).
        /// </summary>
        public void Tick(float dt, in CameraTarget target, in ControlFrame controls, IGroundQuery ground, WorldPos origin)
        {
            if (_camera == null) return;
            if (!(dt >= 0f) || float.IsInfinity(dt)) dt = 0f;
            dt = Mathf.Min(dt, 0.1f);
            RigClass rigClass = target.Rig;
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
            _rigClass = rigClass;
            if (_snap) _blending = false;

            // The chase camera runs every frame (also behind a mounted view), so leaving the seat finds it warm.
            float aspect = Mathf.Max(0.1f, _camera.aspect);
            CameraView chaseView = mounted ? CameraViews.Default(rigClass) : chosen;
            var chaseTarget = new ChaseTarget
            {
                Ground = new V3(target.Ground.x, target.Ground.y, target.Ground.z), HeadingRad = target.HeadingRad, SpeedMps = target.SpeedMps,
                LeanRad = target.LeanRad, Rig = rigClass, PassengerOf = target.PassengerOf, BodyAheadM = target.BodyAheadM, HasBody = target.HasBody,
                Aircraft = target.Aircraft, AircraftLengthM = target.AircraftLengthM, BankRad = target.BankRad,
            };
            var look = new ChaseControls
            {
                ZoomSteps = controls.ZoomSteps, LookYawDeg = mounted ? 0f : controls.LookYawDeg, LookPitchDeg = mounted ? 0f : controls.LookPitchDeg,
                Looking = !mounted && controls.Looking,
            };
            _chase.Obstacles = Obstacles;
            _chase.ReducedMotion = ReducedMotion;
            ChasePose chase = _chase.Tick(dt, chaseTarget, chaseView, look, aspect, ground, origin.X, origin.Y, origin.Z);

            float fov;
            Vector3 position;
            Quaternion rotation;
            float near;
            if (mounted)
            {
                // Mounted: the player's glance and tilt are the view's own (they spring back after a pause).
                bool looking = controls.LookYawDeg != 0f || controls.Looking;
                _lookIdle = looking ? 0f : _lookIdle + dt;
                _orbit = Mathf.Repeat(_orbit + controls.LookYawDeg + 180f, 360f) - 180f;
                if (_lookIdle > LookReturnDelayS) _orbit *= Mathf.Exp(-2.5f * dt);
                _mountPitch = Mathf.Clamp(_mountPitch + controls.LookPitchDeg, -MountedPitchLimitDeg, MountedPitchLimitDeg);
                float windowYaw;
                MountedPose(dt, target, spec, origin, out position, out rotation, out windowYaw);
                near = spec.NearClipM;
                _cameraYawDeg = target.Mount.HeadingDeg + _orbit + windowYaw;
                fov = CameraFov.VerticalFromHorizontal(Mathf.Clamp(spec.MinHFovDeg, 1f, 179f), aspect);
            }
            else
            {
                _orbit = 0f;
                _mountPitch = 0f;
                position = new Vector3(chase.Position.X, chase.Position.Y, chase.Position.Z);
                rotation = new Quaternion(chase.Rotation.X, chase.Rotation.Y, chase.Rotation.Z, chase.Rotation.W);
                near = NearClipM;
                _cameraYawDeg = chase.YawDeg;
                fov = chase.VerticalFovDeg;
            }
            _fov = _snap ? fov : Mathf.Lerp(_fov, fov, 1f - Mathf.Exp(-8f * dt));

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
                    Quaternion frame = Quaternion.Euler(0f, target.HeadingRad * Mathf.Rad2Deg, 0f);
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

        /// <summary>The eye, bonnet and window views: rigid to the body (heading and pitch, part of its roll), the head
        /// bone's jitter smoothed out, plus the player's glance. The window view leans out of the passenger's side just
        /// past the body (never through a wall beside the vehicle: the step is swept against <see cref="Obstacles"/>) and
        /// turns a little in, so the flank runs along the frame; <paramref name="yawAddDeg"/> returns that turn. Mounted
        /// views look levelled in portrait (<see cref="CameraViews.LookDownDeg"/>).</summary>
        private void MountedPose(float dt, in CameraTarget target, in CameraViewSpec spec, WorldPos origin, out Vector3 position, out Quaternion rotation,
                                 out float yawAddDeg)
        {
            CameraMount m = target.Mount;
            Quaternion body = Quaternion.Euler(-m.PitchDeg, m.HeadingDeg, m.RollDeg);
            Quaternion level = Quaternion.Euler(-m.PitchDeg, m.HeadingDeg, 0f);
            Vector3 local;
            yawAddDeg = 0f;
            if (spec.Kind == CameraViewKind.Hood)
            {
                local = m.HoodLocal;
            }
            else if (spec.Kind == CameraViewKind.Window)
            {
                Vector3 eye = m.Head + level * new Vector3(0f, CameraViews.EyeUpM, 0f);
                local = Quaternion.Inverse(body) * (eye - m.Origin);
                float side;
                float x = CameraViews.WindowX(local.x, m.HalfWidthM, out side);
                IViewObstacleQuery obstacles = Obstacles;
                if (obstacles != null)
                {
                    Vector3 from = m.Origin + body * local;
                    Vector3 dir = body * new Vector3(side, 0f, 0f);
                    float reach = _chase.Boom.Reach(obstacles, from.x + origin.X, from.y + origin.Y, from.z + origin.Z, dir.x, dir.y, dir.z,
                                                    Mathf.Abs(x - local.x));
                    x = local.x + side * reach;
                }
                local.x = x;
                yawAddDeg = -side * CameraViews.WindowInYawDeg;
            }
            else
            {
                Vector3 eye = m.Head + level * new Vector3(0f, CameraViews.EyeUpM, CameraViews.EyeForwardM);
                local = Quaternion.Inverse(body) * (eye - m.Origin);
            }
            if (_snap || !_eyeValid || spec.Kind == CameraViewKind.Hood) _eyeLocal = local;
            else _eyeLocal = Vector3.Lerp(_eyeLocal, local, 1f - Mathf.Exp(-dt / EyeSmoothingS));
            if (spec.Kind == CameraViewKind.Window) _eyeLocal.x = local.x; // a wall beside: in at once
            _eyeValid = true;
            position = m.Origin + body * _eyeLocal;
            float roll = ReducedMotion ? 0f : m.RollDeg * spec.RollShare;
            float lookDown = CameraViews.LookDownDeg(spec, _chase.Portrait01);
            rotation = Quaternion.Euler(-m.PitchDeg + lookDown + _mountPitch, m.HeadingDeg + _orbit + yawAddDeg, roll);
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
