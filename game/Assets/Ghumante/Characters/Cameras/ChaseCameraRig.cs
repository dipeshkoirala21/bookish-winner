using System;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using Ghumante.Core.Streaming;
using Ghumante.World;
using Ghumante.World.Cameras;
using UnityEngine;

namespace Ghumante.Characters.Cameras
{
    /// <summary>
    /// The Explore chase camera (M1 track D; ARCHITECTURE.md 7.10a): it trails the explorer with spring smoothing, aims a
    /// little ahead along the motion, and frames by mode and orientation (<see cref="ChaseRigProfile"/>): in portrait it
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
        public const float ModeBlendSeconds = 0.5f;
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
        private float _ride = 1f;
        private float _zoomLog;
        private float _pitchOffset;
        private float _orbit;
        private float _lookIdle = 999f;
        private float _yaw;
        private float _yawVelocity;
        private Vector3 _aim;
        private Vector3 _aimVelocity;
        private float _lookAhead;
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
            if (_camera == null) return;
            if (!(dt >= 0f) || float.IsInfinity(dt)) dt = 0f;
            dt = Mathf.Min(dt, 0.1f);

            // Rig blends: orientation from the camera's own aspect (what the framing is for), mode from the controller.
            float portraitTarget = _camera.aspect < 1f ? 1f : 0f;
            float rideTarget = riding ? 1f : 0f;
            if (_snap)
            {
                _portrait = portraitTarget;
                _ride = rideTarget;
            }
            else
            {
                _portrait = Mathf.MoveTowards(_portrait, portraitTarget, dt / OrientationBlendSeconds);
                _ride = Mathf.MoveTowards(_ride, rideTarget, dt / ModeBlendSeconds);
            }
            ChaseRigProfile rig = ChaseRigProfile.Blend(Smooth(_ride), Smooth(_portrait));

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

            float lookAhead = rig.LookAhead(speedMps);
            _lookAhead = _snap ? lookAhead : Mathf.Lerp(_lookAhead, lookAhead, 1f - Mathf.Exp(-3f * dt));
            var forward = new Vector3(Mathf.Sin(headingRad), 0f, Mathf.Cos(headingRad));
            Vector3 aim = target + new Vector3(0f, rig.AimHeightM, 0f) + forward * _lookAhead;
            if (_snap)
            {
                _aim = aim;
                _aimVelocity = Vector3.zero;
            }
            else
            {
                _aim = Vector3.SmoothDamp(_aim, aim, ref _aimVelocity, 0.06f, Mathf.Infinity, dt);
            }

            float yaw = _yaw + _orbit;
            float pitch = Mathf.Clamp(rig.PitchDeg + _pitchOffset, -5f, 75f);
            float distance = rig.DistanceM * Mathf.Exp(_zoomLog);
            Quaternion look = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 position = _aim - look * Vector3.forward * distance;

            // Never under the ground (a hillside behind, a dip): lift and keep aiming at the explorer.
            GroundSample s;
            if (ground != null && ground.TrySample(position.x + origin.X, position.z + origin.Z, out s))
            {
                float floor = s.Height - origin.Y + GroundClearanceM;
                if (position.y < floor) position.y = floor;
            }
            Vector3 toAim = _aim - position;
            Quaternion rotation = toAim.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(toAim, Vector3.up) : look;
            if (!ReducedMotion && riding)
            {
                rotation *= Quaternion.Euler(0f, 0f, -leanRad * Mathf.Rad2Deg * 0.12f);
            }
            _camera.transform.SetPositionAndRotation(position, rotation);

            // Vertical FOV from the rig's minimum horizontal FOV at this aspect, plus a little speed kick on the scooter.
            float kick = !ReducedMotion && riding ? 5f * Mathf.Clamp01(Mathf.Abs(speedMps) / 25f) : 0f;
            float aspect = Mathf.Max(0.1f, _camera.aspect);
            float fov = CameraFov.VerticalFromHorizontal(rig.MinHorizontalFovDeg + kick, aspect);
            _fov = _snap ? fov : Mathf.Lerp(_fov, fov, 1f - Mathf.Exp(-8f * dt));
            _camera.fieldOfView = _fov;
            _snap = false;
        }

        private static float Smooth(float t)
        {
            return t * t * (3f - 2f * t);
        }
    }
}
