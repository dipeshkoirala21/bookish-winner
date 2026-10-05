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
    /// The Explore chase camera (M1 track D; ARCHITECTURE.md 7.10a): it trails the explorer with spring smoothing, tilts
    /// up the road with speed while keeping the explorer in the lower part of the frame, and frames by mode and orientation (<see cref="ChaseRigProfile"/>): in portrait it
    /// rises and pulls back, and a rotation mid-ride blends the rig over 0.3 s. The vertical field of view keeps the rig's
    /// minimum horizontal FOV at any aspect (<see cref="CameraFov"/>: driving 62°, walking 55°). The player zooms (wheel,
    /// pinch, shoulders) and pitches or looks around (right-drag, two-finger drag, right stick); looking around springs
    /// back behind the explorer when let go. It never dips under the ground, and follows floating-origin shifts.
    /// <para>Clip planes come from <see cref="WorldRoot.ConfigureCamera"/> (far = the tier's view radius, up to 120 km on
    /// High, plus margin) with a 0.4 m near plane: reversed-Z depth on Metal and Vulkan keeps that precise.</para>
    /// A plain class driven by its owner: <see cref="Attach"/>, then <see cref="Tick"/> every LateUpdate, then
    /// <see cref="Detach"/> (restores the camera).
    /// </summary>
    public sealed class ChaseCameraRig
    {
        public const float NearClipM = 0.4f;
        public const float OrientationBlendSeconds = 0.3f;
        public const float ModeBlendSeconds = 0.45f;
        public const float MinZoom = 0.55f, MaxZoom = 2.6f;
        public const float MinPitchOffsetDeg = -9f, MaxPitchOffsetDeg = 40f;

        /// <summary>Lowest the camera may get above the ground.</summary>
        public const float GroundClearanceM = 0.8f;

        /// <summary>Seconds after the last look input before the camera swings back behind the rider.</summary>
        public const float LookReturnDelayS = 1.4f;

        /// <summary>On foot the camera swings in behind only while the walker heads within this angle of its view.</summary>
        public const float WalkFollowConeDeg = 35f;

        private Camera _camera;
        private Vector3 _savedPosition;
        private Quaternion _savedRotation;
        private float _savedFov;
        private float _savedNear;
        private bool _snap = true;

        private float _portrait;
        private RigClass _rigClass = RigClass.TwoWheeler;
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

        public Camera Camera
        {
            get { return _camera; }
        }

        /// <summary>The camera's heading (radians, 0 = north, clockwise; Unity yaw): walking is relative to it, and the
        /// HUD compass shows it.</summary>
        public float YawRad
        {
            get { return (_yaw + _orbit) * Mathf.Deg2Rad; }
        }

        /// <summary>0 in landscape, 1 in portrait (blending between them after a rotation).</summary>
        public float PortraitBlend
        {
            get { return _portrait; }
        }

        /// <summary>No roll, speed FOV kick or shake (Settings, Reduce motion).</summary>
        public bool ReducedMotion { get; set; }

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

        /// <summary>
        /// Places the camera for this frame. <paramref name="target"/> is the explorer's ground point in scene space,
        /// <paramref name="riding"/> picks the rig, <paramref name="controls"/> carries zoom and look input, and
        /// <paramref name="ground"/> with <paramref name="origin"/> keeps the camera above the terrain.
        /// </summary>
        public void Tick(float dt, Vector3 target, float headingRad, float speedMps, float leanRad, bool riding,
                         in ControlFrame controls, IGroundQuery ground, WorldPos origin)
        {
            Tick(dt, target, headingRad, speedMps, leanRad, riding ? RigClass.TwoWheeler : RigClass.Walk, controls, ground, origin);
        }

        /// <summary>The rig the camera is on (or blending to).</summary>
        public RigClass Rig
        {
            get { return _rigClass; }
        }

        /// <summary>The driver rig of the vehicle ridden along: <see cref="RigClass.Passenger"/> orbits at 1.2× it
        /// (W2_DESIGN 6.4), so a bus ride frames the bus. Car by default.</summary>
        public RigClass PassengerOf { get; set; } = RigClass.Car;

        /// <summary>
        /// Places the camera for this frame with the rig of <paramref name="rigClass"/> (W2_DESIGN 6.4: walk, bicycle,
        /// two-wheeler, car, van, bus, truck, tractor, passenger). A change of rig blends over about 0.45 s (on) and
        /// 0.35 s (off); a rotation blends over 0.3 s.
        /// </summary>
        public void Tick(float dt, Vector3 target, float headingRad, float speedMps, float leanRad, RigClass rigClass,
                         in ControlFrame controls, IGroundQuery ground, WorldPos origin)
        {
            if (_camera == null) return;
            if (!(dt >= 0f) || float.IsInfinity(dt)) dt = 0f;
            dt = Mathf.Min(dt, 0.1f);
            bool riding = rigClass != RigClass.Walk;

            // Rig blends: orientation from the camera's own aspect (what the framing is for), class from the controller.
            float portraitTarget = _camera.aspect < 1f ? 1f : 0f;
            _portrait = _snap ? portraitTarget : Mathf.MoveTowards(_portrait, portraitTarget, dt / OrientationBlendSeconds);
            ChaseRigProfile want = ChaseRigProfile.For(rigClass, Smooth(_portrait), PassengerOf);
            float blendS = rigClass == RigClass.Walk ? 0.35f : ModeBlendSeconds;
            _rigClass = rigClass;
            _profile = _snap ? want : ChaseRigProfile.Lerp(_profile, want, 1f - Mathf.Exp(-3f * dt / blendS));
            ChaseRigProfile rig = _profile;

            // Player zoom (log scale, so each notch feels the same), pitch and look-around. On the scooter looking around
            // is a glance that swings back behind the rider; on foot it turns the camera for good (walking is relative
            // to it).
            _zoomLog = Mathf.Clamp(_zoomLog - controls.ZoomSteps * 0.12f, Mathf.Log(MinZoom), Mathf.Log(MaxZoom));
            _pitchOffset = Mathf.Clamp(_pitchOffset + controls.LookPitchDeg, MinPitchOffsetDeg, MaxPitchOffsetDeg);
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

            // Follow the heading: tightly on the scooter. On foot only while walking roughly away from the camera, so
            // walking towards it or sideways (the stick is camera-relative) never sends camera and walker chasing each
            // other in circles.
            float headingDeg = headingRad * Mathf.Rad2Deg;
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

            // The camera sits behind the explorer's pivot (never ahead of it); the look-ahead only tilts the view up the
            // road, by pinning the explorer's ground point at the rig's screen height for this speed.
            float footY = rig.FootScreenY(speedMps);
            _footY = _snap ? footY : Mathf.Lerp(_footY, footY, 1f - Mathf.Exp(-3f * dt));
            Vector3 pivot = target + new Vector3(0f, rig.AimHeightM, 0f);
            if (_snap)
            {
                _aim = pivot;
                _aimVelocity = Vector3.zero;
            }
            else
            {
                _aim = Vector3.SmoothDamp(_aim, pivot, ref _aimVelocity, 0.06f, Mathf.Infinity, dt);
            }

            float yaw = _yaw + _orbit;
            float pitch = Mathf.Clamp(rig.PitchDeg + _pitchOffset, -5f, 75f);
            float distance = rig.DistanceM * Mathf.Exp(_zoomLog);
            Quaternion look = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 position = _aim - look * Vector3.forward * distance;

            // Never under the ground (a hillside behind, a dip): lift and keep framing the explorer.
            GroundSample s;
            if (ground != null && ground.TrySample(position.x + origin.X, position.z + origin.Z, out s))
            {
                float floor = s.Height - origin.Y + GroundClearanceM;
                if (position.y < floor) position.y = floor;
            }

            // Vertical FOV from the rig's minimum horizontal FOV at this aspect, plus a little speed kick on the scooter.
            float kick = !ReducedMotion && riding ? CameraRigTable.FovKick(CameraRigTable.For(rigClass, _portrait > 0.5f), speedMps) : 0f;
            float aspect = Mathf.Max(0.1f, _camera.aspect);
            float fov = CameraFov.VerticalFromHorizontal(rig.MinHorizontalFovDeg + kick, aspect);
            _fov = _snap ? fov : Mathf.Lerp(_fov, fov, 1f - Mathf.Exp(-8f * dt));
            _camera.fieldOfView = _fov;

            // Face the explorer and pitch so its (smoothed) ground point stands at the framing height.
            float footX = _aim.x - position.x;
            float footZ = _aim.z - position.z;
            float horizontal = Mathf.Sqrt(footX * footX + footZ * footZ);
            Quaternion rotation = look;
            if (horizontal > 1e-3f)
            {
                float aboveFoot = position.y - (_aim.y - rig.AimHeightM);
                float viewPitch = ChaseRigProfile.ViewPitchDeg(aboveFoot, horizontal, _footY, _fov);
                float viewYaw = Mathf.Atan2(footX, footZ) * Mathf.Rad2Deg;
                rotation = Quaternion.Euler(Mathf.Clamp(viewPitch, -80f, 89f), viewYaw, 0f);
            }
            if (!ReducedMotion && (rigClass == RigClass.TwoWheeler || rigClass == RigClass.Bicycle))
            {
                rotation *= Quaternion.Euler(0f, 0f, -leanRad * Mathf.Rad2Deg * 0.12f);
            }
            _camera.transform.SetPositionAndRotation(position, rotation);
            _snap = false;
        }

        private static float Smooth(float t)
        {
            return t * t * (3f - 2f * t);
        }
    }
}
