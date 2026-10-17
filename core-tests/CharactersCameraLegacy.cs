using System;
using Ghumante.Core.Characters;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The chase camera as it was before the playtest-fix round (commit 5a7668a: <c>ChaseCameraRig</c> on Unity math, the
    /// rig numbers of W2_DESIGN 6.4 and the collision boom of the W2 detail pass), ported line by line to engine-free C#
    /// with Unity's <c>SmoothDamp</c>/<c>SmoothDampAngle</c> (Game Programming Gems 4, 1.10, as Unity implements it). It is
    /// the "before" of owner playtest #5 ("the camera constantly keeps moving"): the camera tests drive it over the same
    /// recorded rides as the new <see cref="ChaseCamera"/> and show where it fails the thresholds. Chase view only (the
    /// default "near" view of each class, landscape, no player input, driving forward).
    /// </summary>
    internal sealed class LegacyChaseRig
    {
        private struct Old
        {
            public float D, Pitch, Pivot, LaS, LaMax, HFov, Follow, Kick, KickFrom, KickFull, MinD, FootRest, FootFast;
        }

        private static Old For(RigClass rig, bool portrait)
        {
            if (portrait)
            {
                // The old portrait rows (10-15 % farther, 8-10 degrees steeper, foot higher on the tall screen).
                switch (rig)
                {
                    case RigClass.Walk: return new Old { D = 6.8f, Pitch = 20f, Pivot = 0.9f, LaS = 0.6f, LaMax = 4.5f, HFov = 55f, Follow = 1.0f, KickFull = 1f, MinD = 2.5f, FootRest = -0.42f, FootFast = -0.5f };
                    case RigClass.Bicycle: return new Old { D = 8.5f, Pitch = 20f, Pivot = 1.0f, LaS = 0.8f, LaMax = 10f, HFov = 62f, Follow = 0.30f, Kick = 3f, KickFrom = 3f, KickFull = 8.3f, MinD = 4f, FootRest = -0.45f, FootFast = -0.6f };
                    case RigClass.TwoWheeler: return new Old { D = 9.2f, Pitch = 21f, Pivot = 1.0f, LaS = 0.9f, LaMax = 16f, HFov = 62f, Follow = 0.32f, Kick = 6f, KickFrom = 16.7f, KickFull = 25f, MinD = 4f, FootRest = -0.45f, FootFast = -0.6f };
                    case RigClass.Van: return new Old { D = 12.0f, Pitch = 23f, Pivot = 1.6f, LaS = 1.0f, LaMax = 20f, HFov = 62f, Follow = 0.40f, Kick = 4f, KickFrom = 5f, KickFull = 22f, MinD = 4f, FootRest = -0.45f, FootFast = -0.6f };
                    case RigClass.Bus: return new Old { D = 18.5f, Pitch = 25f, Pivot = 2.6f, LaS = 1.2f, LaMax = 28f, HFov = 64f, Follow = 0.60f, Kick = 2f, KickFrom = 5f, KickFull = 17f, MinD = 4f, FootRest = -0.45f, FootFast = -0.6f };
                    case RigClass.Truck: return new Old { D = 16.5f, Pitch = 24f, Pivot = 2.4f, LaS = 1.2f, LaMax = 26f, HFov = 64f, Follow = 0.55f, Kick = 2f, KickFrom = 5f, KickFull = 15f, MinD = 4f, FootRest = -0.45f, FootFast = -0.6f };
                    default: return new Old { D = 10.8f, Pitch = 22f, Pivot = 1.3f, LaS = 1.0f, LaMax = 20f, HFov = 62f, Follow = 0.35f, Kick = 5f, KickFrom = 5f, KickFull = 25f, MinD = 4f, FootRest = -0.45f, FootFast = -0.6f };
                }
            }
            switch (rig)
            {
                case RigClass.Walk: return new Old { D = 8.4f, Pitch = 12f, Pivot = 1.0f, LaS = 0.3f, LaMax = 2.5f, HFov = 55f, Follow = 0.9f, KickFull = 1f, MinD = 2.5f, FootRest = -0.58f, FootFast = -0.62f };
                case RigClass.Bicycle: return new Old { D = 7.5f, Pitch = 12f, Pivot = 1.0f, LaS = 0.4f, LaMax = 6f, HFov = 62f, Follow = 0.30f, Kick = 3f, KickFrom = 3f, KickFull = 8.3f, MinD = 4f, FootRest = -0.48f, FootFast = -0.6f };
                case RigClass.TwoWheeler: return new Old { D = 8.0f, Pitch = 13f, Pivot = 1.0f, LaS = 0.45f, LaMax = 9f, HFov = 62f, Follow = 0.28f, Kick = 6f, KickFrom = 16.7f, KickFull = 25f, MinD = 4f, FootRest = -0.48f, FootFast = -0.6f };
                case RigClass.Van: return new Old { D = 10.5f, Pitch = 15f, Pivot = 1.6f, LaS = 0.5f, LaMax = 12f, HFov = 62f, Follow = 0.40f, Kick = 4f, KickFrom = 5f, KickFull = 22f, MinD = 4f, FootRest = -0.48f, FootFast = -0.6f };
                case RigClass.Bus: return new Old { D = 16.5f, Pitch = 17f, Pivot = 2.6f, LaS = 0.6f, LaMax = 18f, HFov = 64f, Follow = 0.60f, Kick = 2f, KickFrom = 5f, KickFull = 17f, MinD = 4f, FootRest = -0.48f, FootFast = -0.6f };
                case RigClass.Truck: return new Old { D = 14.5f, Pitch = 16f, Pivot = 2.4f, LaS = 0.6f, LaMax = 16f, HFov = 64f, Follow = 0.55f, Kick = 2f, KickFrom = 5f, KickFull = 15f, MinD = 4f, FootRest = -0.48f, FootFast = -0.6f };
                default: return new Old { D = 9.5f, Pitch = 14f, Pivot = 1.3f, LaS = 0.5f, LaMax = 12f, HFov = 62f, Follow = 0.35f, Kick = 5f, KickFrom = 5f, KickFull = 25f, MinD = 4f, FootRest = -0.48f, FootFast = -0.6f };
            }
        }

        private const float GroundClearanceM = 0.8f;
        private readonly LegacyChaseBoom _boom = new LegacyChaseBoom();
        private bool _snap = true;
        private float _yaw, _yawVelocity, _footY, _fov;
        private V3 _aim, _aimVelocity;

        public IViewObstacleQuery Obstacles;
        public float Aspect = 2400f / 1080f;

        /// <summary>Use the old portrait rows (and the portrait overhead look-down).</summary>
        public bool Portrait;

        public V3 Position { get; private set; }
        public Quat Rotation { get; private set; }
        public float VerticalFovDeg { get; private set; }

        public LegacyChaseBoom Boom
        {
            get { return _boom; }
        }

        /// <summary>One LateUpdate of the old rig: <paramref name="ground"/> is the explorer's ground point in scene metres
        /// (<paramref name="origin"/> + scene = game metres).</summary>
        public void Tick(float dt, V3 ground, float headingRad, float speedMps, float leanRad, RigClass rigClass, IGroundQuery groundQuery,
                         WorldPos origin)
        {
            if (!(dt >= 0f) || float.IsInfinity(dt)) dt = 0f;
            dt = Math.Min(dt, 0.1f);
            Old rig = For(rigClass, Portrait);
            float headingDeg = headingRad * Quat.Rad2Deg;
            if (_snap)
            {
                _yaw = headingDeg;
                _yawVelocity = 0f;
            }
            else
            {
                _yaw = SmoothDampAngle(_yaw, headingDeg, ref _yawVelocity, rig.Follow, float.PositiveInfinity, dt);
            }
            V3 pivot = ground + new V3(0f, rig.Pivot, 0f);
            if (_snap)
            {
                _aim = pivot;
                _aimVelocity = V3.Zero;
            }
            else
            {
                _aim = SmoothDamp(_aim, pivot, ref _aimVelocity, 0.06f, dt);
            }
            float kick = 0f;
            if (rig.Kick > 0f && rig.KickFull > rig.KickFrom) kick = rig.Kick * CharMath.Clamp01((Math.Abs(speedMps) - rig.KickFrom) / (rig.KickFull - rig.KickFrom));
            float fov = VerticalFromHorizontal(Math.Max(1f, Math.Min(179f, rig.HFov + kick)), Math.Max(0.1f, Aspect));
            _fov = _snap ? fov : CharMath.Lerp(_fov, fov, 1f - MathF.Exp(-8f * dt));

            // ChasePose.
            float yaw = _yaw;
            float pitch = CharMath.Clamp(rig.Pitch, -5f, 75f);
            float distance = rig.D;
            if (_snap) _boom.Snap();
            float min = Math.Min(rig.MinD, distance);
            _boom.Solve(Obstacles, _aim.X + origin.X, _aim.Y + origin.Y, _aim.Z + origin.Z, yaw, pitch, distance, min, dt);
            double ox, oy, oz;
            _boom.CameraOffset(yaw, out ox, out oy, out oz);
            V3 position = _aim + new V3((float)ox, (float)oy, (float)oz);
            GroundSample s;
            if (groundQuery != null && groundQuery.TrySample(position.X + origin.X, position.Z + origin.Z, out s))
            {
                float floor = s.Height - origin.Y + GroundClearanceM;
                if (position.Y < floor) position.Y = floor;
            }
            float la = speedMps > 0f ? Math.Min(rig.LaMax, speedMps * rig.LaS) : 0f;
            float footY = rig.FootRest + (rig.FootFast - rig.FootRest) * (rig.LaMax > 0f ? la / rig.LaMax : 0f);
            footY = Math.Min(0.2f, footY);
            _footY = _snap ? footY : CharMath.Lerp(_footY, footY, 1f - MathF.Exp(-3f * dt));
            Quat rotation = Quat.Euler(_boom.PitchDeg, yaw, 0f);
            float footX = _aim.X - position.X, footZ = _aim.Z - position.Z;
            float horizontal = MathF.Sqrt(footX * footX + footZ * footZ);
            if (horizontal > 1e-3f)
            {
                float aboveFoot = position.Y - (_aim.Y - rig.Pivot);
                float viewPitch = ViewPitchDeg(aboveFoot, horizontal, _footY, _fov);
                float viewYaw = MathF.Atan2(footX, footZ) * Quat.Rad2Deg;
                rotation = Quat.Euler(CharMath.Clamp(viewPitch, -80f, 89f), viewYaw, 0f);
            }
            float overhead = _boom.OverheadLook01;
            if (overhead > 1e-3f) rotation = Quat.Slerp(rotation, Quat.Euler(LegacyChaseBoom.OverheadLookDownDeg(Portrait ? 1f : 0f), yaw, 0f), overhead);
            if (rigClass == RigClass.TwoWheeler || rigClass == RigClass.Bicycle) rotation = rotation * Quat.Euler(0f, 0f, -leanRad * Quat.Rad2Deg * 0.12f);
            Position = position;
            Rotation = rotation;
            VerticalFovDeg = _fov;
            _snap = false;
        }

        // ---- Unity math, as UnityEngine implements it ----

        internal static float SmoothDamp(float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed, float deltaTime)
        {
            smoothTime = Math.Max(0.0001f, smoothTime);
            float omega = 2f / smoothTime;
            float x = omega * deltaTime;
            float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            float change = current - target;
            float originalTo = target;
            float maxChange = maxSpeed * smoothTime;
            change = CharMath.Clamp(change, -maxChange, maxChange);
            target = current - change;
            float temp = (currentVelocity + omega * change) * deltaTime;
            currentVelocity = (currentVelocity - omega * temp) * exp;
            float output = target + (change + temp) * exp;
            if (originalTo - current > 0f == output > originalTo)
            {
                output = originalTo;
                currentVelocity = deltaTime > 0f ? (output - originalTo) / deltaTime : 0f;
            }
            return output;
        }

        internal static float SmoothDampAngle(float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed, float deltaTime)
        {
            target = current + DeltaAngle(current, target);
            return SmoothDamp(current, target, ref currentVelocity, smoothTime, maxSpeed, deltaTime);
        }

        internal static float DeltaAngle(float current, float target)
        {
            float delta = Repeat(target - current, 360f);
            if (delta > 180f) delta -= 360f;
            return delta;
        }

        private static float Repeat(float t, float length)
        {
            return CharMath.Clamp(t - MathF.Floor(t / length) * length, 0f, length);
        }

        internal static V3 SmoothDamp(V3 current, V3 target, ref V3 currentVelocity, float smoothTime, float deltaTime)
        {
            smoothTime = Math.Max(0.0001f, smoothTime);
            float omega = 2f / smoothTime;
            float x = omega * deltaTime;
            float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            V3 change = current - target;
            V3 originalTo = target;
            target = current - change;
            V3 temp = (currentVelocity + omega * change) * deltaTime;
            currentVelocity = (currentVelocity - omega * temp) * exp;
            V3 output = target + (change + temp) * exp;
            V3 origMinusCurrent = originalTo - current, outMinusOrig = output - originalTo;
            if (V3.Dot(origMinusCurrent, outMinusOrig) > 0f)
            {
                output = originalTo;
                currentVelocity = deltaTime > 0f ? (output - originalTo) * (1f / deltaTime) : V3.Zero;
            }
            return output;
        }

        private static float VerticalFromHorizontal(float h, float aspect)
        {
            double halfH = h * (Math.PI / 360.0);
            double vertical = 2.0 * Math.Atan(Math.Tan(halfH) / aspect) * (180.0 / Math.PI);
            return (float)Math.Min(vertical, 100.0);
        }

        private static float ViewPitchDeg(float cameraAbovePointM, float horizontalDistanceM, float screenY, float verticalFovDeg)
        {
            double tanHalf = Math.Tan(verticalFovDeg * 0.5 * Math.PI / 180.0);
            double below = Math.Atan2(cameraAbovePointM, Math.Max(1e-3, horizontalDistanceM));
            return (float)((below + Math.Atan(screenY * tanHalf)) * 180.0 / Math.PI);
        }
    }

    /// <summary>The collision boom as it was before the playtest-fix round (Core/Characters/CameraRigTable.cs at 5a7668a),
    /// kept verbatim for <see cref="LegacyChaseRig"/>.</summary>
    internal sealed class LegacyChaseBoom
    {
        public const float ProbeRadiusM = 0.3f;

        /// <summary>Every sweep starts this far back along its direction, on the player's side of its origin, so a wall
        /// the origin is backed against (closer than <see cref="ProbeRadiusM"/>) is met, not started in.</summary>
        public const float BackOffM = 0.35f;

        /// <summary>When the backed-off sweep meets something before it reaches its origin, a thin sweep of this radius
        /// from the origin itself (always clear of the player's 0.3 m body) tells a wall right behind from something on
        /// the player's side.</summary>
        public const float CoreRadiusM = 0.12f;

        public const float SkinM = 0.1f;

        /// <summary>A thin sweep that meets something within this of its start started inside it: blocked at once.</summary>
        public const float StartInsideM = 0.02f;

        public const float EaseOutSeconds = 0.6f;
        public const float HoldSeconds = 0.2f;
        public const float LiftStepDeg = 15f;
        public const int LiftSteps = 3;
        public const float MaxPitchDeg = 80f;

        /// <summary>Lift rises and falls at these rates, per second (exponential approach).</summary>
        public const float LiftRiseRate = 10f, LiftFallRate = 2.5f;

        public const float LaneProbeM = 3f;
        public const float LanePitchDeg = 15f;
        public const float LaneDistanceScale = 0.85f;
        public const float LaneBlendSeconds = 0.5f;

        /// <summary>The side probes run at 10 Hz (the lane blend is slow anyway).</summary>
        public const float LaneIntervalS = 0.1f;

        /// <summary>The overhead fallback starts when the clear boom (after lifting) is shorter than this: about the
        /// player's own size, where the camera would sit inside the rider.</summary>
        public const float FallbackClearM = 1.2f;

        /// <summary>... and ends once the boom from the pivot has this much room again for <see cref="FallbackExitS"/>.</summary>
        public const float FallbackExitM = 1.8f;

        public const float FallbackExitS = 0.25f;

        /// <summary>The fallback raises the pivot this far straight up (less under a ceiling or an eave).</summary>
        public const float OverheadRiseM = 2.4f;

        /// <summary>The fallback blends in and out over this long.</summary>
        public const float OverheadSeconds = 0.3f;

        /// <summary>In the fallback the camera looks this far down from level along the boom's heading, degrees: the lane
        /// ahead with the vehicle's front (landscape) or the whole rider (portrait's tall view) at the bottom.</summary>
        public const float OverheadLookDownLandscapeDeg = 58f, OverheadLookDownPortraitDeg = 48f;

        private const double Deg2Rad = Math.PI / 180.0;

        private float _distance = -1f;
        private float _lift;
        private float _hold;
        private float _lane;
        private float _laneTimer;
        private bool _laneWalls;
        private bool _overhead;
        private float _over;
        private float _overExit;

        /// <summary>The boom length this frame, metres (from the raised pivot in the fallback).</summary>
        public float DistanceM { get; private set; }

        /// <summary>The boom pitch this frame (wanted pitch + lane + lift), degrees.</summary>
        public float PitchDeg { get; private set; }

        /// <summary>How far the overhead fallback raised the pivot this frame, metres (0 outside it).</summary>
        public float RiseM { get; private set; }

        /// <summary>True while the overhead fallback is wanted (the boom has no room behind the player).</summary>
        public bool Overhead
        {
            get { return _overhead; }
        }

        /// <summary>Overhead fallback blend, 0 to 1 (eased): the camera's aim turns to look down the way ahead.</summary>
        public float Overhead01
        {
            get { return CharMath.SmoothStep(_over); }
        }

        /// <summary>How far the camera's aim turns to the overhead look (<see cref="OverheadLookDownDeg"/>), 0 to 1: the
        /// fallback's blend, fading out again where the boom from the raised pivot finds room (backed against a low
        /// garden wall the camera rises over it and looks back at the player as usual).</summary>
        public float OverheadLook01
        {
            get { return Overhead01 * (1f - CharMath.Clamp01((DistanceM - 0.3f) / 1.5f)); }
        }

        /// <summary>True while the fallback is wanted or still blending: only then may the camera come closer to the
        /// pivot than <see cref="FallbackClearM"/> (crossing over the rider's head).</summary>
        public bool FallbackActive
        {
            get { return _overhead || _over > 0f; }
        }

        /// <summary>Extra pitch to clear a low wall, degrees (smoothed).</summary>
        public float LiftDeg
        {
            get { return _lift; }
        }

        /// <summary>Lane frame blend, 0 to 1 (eased).</summary>
        public float Lane01
        {
            get { return CharMath.SmoothStep(_lane); }
        }

        /// <summary>Free length along this frame's boom direction (with the skin taken off).</summary>
        public float ClearM { get; private set; }

        /// <summary>Something cut the boom short this frame.</summary>
        public bool Blocked { get; private set; }

        /// <summary>Sweeps made by the last <see cref="Solve"/>.</summary>
        public int Casts { get; private set; }

        /// <summary>The overhead fallback's downward look for an orientation blend (0 landscape, 1 portrait), degrees.</summary>
        public static float OverheadLookDownDeg(float portrait01)
        {
            float t = CharMath.Clamp01(portrait01);
            return OverheadLookDownLandscapeDeg + (OverheadLookDownPortraitDeg - OverheadLookDownLandscapeDeg) * t;
        }

        /// <summary>Next solve starts fresh (spawn, teleport): no smoothing from the old length, lift, lane or fallback.</summary>
        public void Snap()
        {
            _distance = -1f;
            _lift = 0f;
            _hold = 0f;
            _laneTimer = 0f;
            _overhead = false;
            _over = 0f;
            _overExit = 0f;
        }

        /// <summary>
        /// Places the boom for this frame: pivot (<paramref name="px"/>, <paramref name="py"/>, <paramref name="pz"/>) in
        /// game metres, boom yaw (degrees, 0 = north, clockwise: the camera sits behind, at −forward) and pitch
        /// (degrees, camera above the pivot), the wanted length and the rig's minimum. Read <see cref="CameraOffset"/>
        /// (or <see cref="RiseM"/>, <see cref="DistanceM"/> and <see cref="PitchDeg"/>) afterwards.
        /// </summary>
        public void Solve(IViewObstacleQuery query, double px, double py, double pz, float yawDeg, float pitchDeg, float wantedM,
                          float minM, float dt)
        {
            Casts = 0;
            if (!(dt >= 0f) || float.IsInfinity(dt)) dt = 0f;
            if (float.IsNaN(wantedM) || wantedM < 0f) wantedM = 0f;
            if (float.IsNaN(pitchDeg)) pitchDeg = 0f;
            bool snap = _distance < 0f;

            UpdateLane(query, px, py, pz, yawDeg, dt, snap);
            float lane = Lane01;
            float pitch = Math.Min(MaxPitchDeg, pitchDeg + LanePitchDeg * lane);
            float want = wantedM * (1f + (LaneDistanceScale - 1f) * lane);
            float min = Math.Min(Math.Max(0f, minM), want);

            float liftTarget = 0f;
            float clear0 = query != null ? Clear(query, px, py, pz, yawDeg, pitch, want) : want;
            if (query != null && clear0 < min - 1e-3f)
            {
                float best = clear0, bestLift = 0f;
                for (int k = 1; k <= LiftSteps; k++)
                {
                    float p = Math.Min(MaxPitchDeg, pitch + k * LiftStepDeg);
                    if (p <= pitch + 1e-3f) break;
                    float c = Clear(query, px, py, pz, yawDeg, p, want);
                    if (c > best + 0.25f)
                    {
                        best = c;
                        bestLift = p - pitch;
                    }
                    if (c >= min) break;
                }
                liftTarget = bestLift;
            }
            if (snap) _lift = liftTarget;
            else _lift = CharMath.Approach(_lift, liftTarget, liftTarget > _lift ? LiftRiseRate : LiftFallRate, dt);
            if (_lift < 0.01f && liftTarget <= 0f) _lift = 0f;

            float finalPitch = Math.Min(MaxPitchDeg, pitch + _lift);
            float clearBase = clear0;
            if (query != null && _lift > 0f) clearBase = Clear(query, px, py, pz, yawDeg, finalPitch, want);

            // The overhead fallback: no room for a boom behind the player. In at once, out after a short hold.
            UpdateOverhead(query != null, clearBase, want, dt, snap);
            float rise = 0f, clear = clearBase;
            if (_over > 0f && query != null)
            {
                rise = Reach(query, px, py, pz, 0.0, 1.0, 0.0, OverheadRiseM) * CharMath.SmoothStep(_over);
                if (rise > 1e-3f) clear = Clear(query, px, py + rise, pz, yawDeg, finalPitch, want);
                else rise = 0f;
            }

            float target = Math.Min(want, clear);
            if (snap)
            {
                _distance = target;
                _hold = 0f;
            }
            else if (target < _distance)
            {
                // In at once: never inside or behind a wall.
                _distance = target;
                _hold = HoldSeconds;
            }
            else if (_hold > 0f)
            {
                _hold -= dt;
            }
            else
            {
                _distance = CharMath.Approach(_distance, target, 3f / EaseOutSeconds, dt);
                if (target - _distance < 1e-3f) _distance = target;
            }
            _distance = Math.Min(_distance, clear);
            Blocked = clear < want - 1e-3f;
            ClearM = clear;
            DistanceM = _distance;
            PitchDeg = finalPitch;
            RiseM = rise;
        }

        /// <summary>The camera's offset from the pivot this frame (game metres, absolute y): up by <see cref="RiseM"/>,
        /// then <see cref="DistanceM"/> along the boom at <paramref name="yawDeg"/> and <see cref="PitchDeg"/>.</summary>
        public void CameraOffset(float yawDeg, out double ox, out double oy, out double oz)
        {
            double dx, dy, dz;
            Direction(yawDeg, PitchDeg, out dx, out dy, out dz);
            ox = dx * DistanceM;
            oy = RiseM + dy * DistanceM;
            oz = dz * DistanceM;
        }

        /// <summary>
        /// Free length (minus <see cref="SkinM"/>) for a <see cref="ProbeRadiusM"/> sphere moved from a point along a unit
        /// direction, up to <paramref name="length"/>. The sweep starts <see cref="BackOffM"/> back, on the player's side
        /// of the point, so a wall the point is backed against is met instead of started in (a sphere that starts in a
        /// solid ignores it, which would put the camera inside the house). When it meets something before reaching the
        /// point, a thin <see cref="CoreRadiusM"/> sweep from the point itself decides: a wall right behind gives 0, a thing
        /// on the player's side is ignored, and a thin sweep that starts inside something is blocked at once. Never
        /// takes a start inside as clear.
        /// </summary>
        public float Reach(IViewObstacleQuery query, double ox, double oy, double oz, double dx, double dy, double dz, float length)
        {
            if (query == null || !(length > 0f)) return Math.Max(0f, length);
            Casts++;
            double hit;
            const double back = BackOffM;
            if (!query.SphereCast(ox - dx * back, oy - dy * back, oz - dz * back, dx, dy, dz, ProbeRadiusM, length + back + SkinM, out hit))
                return length;
            if (double.IsNaN(hit)) hit = 0.0;
            if (hit >= back - 1e-6) return Free(hit - back, length);

            // Contact before the probe reached the point: a wall closer behind it than the probe radius, or something on
            // the player's side of it. The thin sweep from the point sees only what lies ahead along the direction.
            Casts++;
            const double widen = ProbeRadiusM - CoreRadiusM;
            if (!query.SphereCast(ox, oy, oz, dx, dy, dz, CoreRadiusM, length + SkinM + widen, out hit)) return length;
            if (double.IsNaN(hit) || hit < StartInsideM) return 0f;
            return Free(hit - widen, length);
        }

        /// <summary>The camera offset from the pivot for a boom of unit length at (<paramref name="yawDeg"/>,
        /// <paramref name="pitchDeg"/>): behind and above.</summary>
        public static void Direction(float yawDeg, float pitchDeg, out double dx, out double dy, out double dz)
        {
            double yaw = yawDeg * Deg2Rad, pitch = pitchDeg * Deg2Rad;
            double c = Math.Cos(pitch);
            dx = -Math.Sin(yaw) * c;
            dy = Math.Sin(pitch);
            dz = -Math.Cos(yaw) * c;
        }

        private static float Free(double travelled, float length)
        {
            return (float)Math.Max(0.0, Math.Min(length, travelled - SkinM));
        }

        private float Clear(IViewObstacleQuery query, double px, double py, double pz, float yawDeg, float pitchDeg, float length)
        {
            double dx, dy, dz;
            Direction(yawDeg, pitchDeg, out dx, out dy, out dz);
            return Reach(query, px, py, pz, dx, dy, dz, length);
        }

        private void UpdateOverhead(bool hasQuery, float clearBase, float want, float dt, bool snap)
        {
            if (!hasQuery)
            {
                _overhead = false;
                _overExit = 0f;
            }
            else if (!_overhead)
            {
                _overhead = clearBase < Math.Min(FallbackClearM, want - 1e-3f);
                _overExit = 0f;
            }
            else if (clearBase < Math.Min(FallbackExitM, want - 1e-3f))
            {
                _overExit = 0f;
            }
            else
            {
                _overExit += dt;
                if (_overExit >= FallbackExitS || snap) _overhead = false;
            }
            float goal = _overhead ? 1f : 0f;
            if (snap) _over = goal;
            else
            {
                float step = dt / OverheadSeconds;
                _over = _over < goal ? Math.Min(goal, _over + step) : Math.Max(goal, _over - step);
            }
        }

        private void UpdateLane(IViewObstacleQuery query, double px, double py, double pz, float yawDeg, float dt, bool snap)
        {
            if (query == null)
            {
                _laneWalls = false;
            }
            else
            {
                _laneTimer -= dt;
                if (snap || _laneTimer <= 0f)
                {
                    _laneTimer = LaneIntervalS;
                    double yaw = yawDeg * Deg2Rad;
                    double rx = Math.Cos(yaw), rz = -Math.Sin(yaw);
                    bool right = Reach(query, px, py, pz, rx, 0.0, rz, LaneProbeM) < LaneProbeM - 1e-3f;
                    bool left = right && Reach(query, px, py, pz, -rx, 0.0, -rz, LaneProbeM) < LaneProbeM - 1e-3f;
                    _laneWalls = right && left;
                }
            }
            float goal = _laneWalls ? 1f : 0f;
            if (snap) _lane = goal;
            else
            {
                float step = dt / LaneBlendSeconds;
                _lane = _lane < goal ? Math.Min(goal, _lane + step) : Math.Max(goal, _lane - step);
            }
        }
    }
}
