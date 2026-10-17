using System;
using Ghumante.Core.Driving;

namespace Ghumante.Core.Characters
{
    /// <summary>
    /// The collision-aware boom of the chase camera (ref_camera.md §4; owner feedback: "houses block the view", "camera
    /// view gets blocked if I try to back up my motorbike in the narrower streets", playtest #5: "it constantly keeps
    /// moving"). Each frame it sweeps a <see cref="ProbeRadiusM"/> sphere from the pivot over the player towards where the
    /// camera wants to be (<see cref="IViewObstacleQuery"/>, game metres with absolute heights; see <see cref="Reach"/>):
    /// <list type="bullet">
    /// <item><b>Never inside or behind a wall</b>: the length is clamped to the free length of this frame's boom.</item>
    /// <item><b>No snaps</b>: the boom also sweeps from where the pivot will be in <see cref="PredictS"/> (and at the yaw
    /// it is turning to), and shortens towards the nearer of the two with a critically damped spring
    /// (<see cref="InSeconds"/>), so a wall coming up is met smoothly before it would cut the boom. It lengthens again only
    /// after <see cref="HoldSeconds"/> and once the room exceeds the boom by <see cref="OutMarginM"/> (hysteresis), over
    /// about <see cref="OutSeconds"/>.</item>
    /// <item><b>Raise (or lower) rather than pull in</b>: when the boom, now or where the pivot will be in
    /// <see cref="PredictS"/>, is cut short by more than <see cref="RaiseWhenShortShare"/>, steeper booms
    /// (<see cref="LiftStepDeg"/> steps) and flatter ones (<see cref="LowerStepDeg"/> steps) are tried, smallest change
    /// first and none that moves the camera more than <see cref="MaxPitchSwingM"/>: it rises over a low wall, a parked
    /// truck or a railing, or passes under an overhanging upper floor or a foot overbridge, instead of coming in, but
    /// shortens at a tall corner; the change goes back only after <see cref="LiftHoldSeconds"/> of room. A long boom looks
    /// further ahead and turns more slowly (<see cref="LongBoomScale"/>).</item>
    /// <item><b>Lanes</b>: walls within <see cref="LaneProbeM"/> on both sides for <see cref="LaneEnterS"/> blend in the
    /// lane frame (<see cref="LanePitchDeg"/> steeper, <see cref="LaneDistanceScale"/> as long) over
    /// <see cref="LaneBlendSeconds"/>; it leaves only after <see cref="LaneExitS"/> without them, so the gaps between
    /// houses along an old-core lane never pump the camera.</item>
    /// <item><b>Overhead fallback</b>: with no room behind at all (backed against a house) the pivot rises straight up by
    /// up to <see cref="OverheadRiseM"/> and the camera looks down the way ahead (<see cref="OverheadLook01"/>); it returns
    /// once the boom has <see cref="FallbackExitM"/> of room for <see cref="FallbackExitS"/>.</item>
    /// </list>
    /// A sweep that starts inside or touching something is never taken as clear. Without a query it only eases towards the
    /// wanted length. Engine-free; no allocation; usually two to four sweeps per frame.
    /// </summary>
    public sealed class ChaseBoom
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

        /// <summary>Shortening smoothing time, seconds (critically damped).</summary>
        public const float InSeconds = 0.16f;

        /// <summary>Lengthening smoothing time, seconds.</summary>
        public const float OutSeconds = 0.45f;

        /// <summary>The boom waits this long after shortening before it lengthens again.</summary>
        public const float HoldSeconds = 0.35f;

        /// <summary>... and lengthens only when the room exceeds it by this much (hysteresis).</summary>
        public const float OutMarginM = 0.3f;

        /// <summary>Kept for the old name: the boom eases back out over about this long.</summary>
        public const float EaseOutSeconds = 1.0f;

        /// <summary>Sweeps also run from where the pivot (and the boom's yaw) will be this far ahead.</summary>
        public const float PredictS = 0.35f;

        /// <summary>For a wall the boom never shortens faster than this share of its length per second (2.8 % in a 1/30 s
        /// frame): a longer step in one frame is a snap. Only the hard limit (a wall the early look did not see) may cut it
        /// faster.</summary>
        public const float MaxLengthSharePerS = 0.85f;

        /// <summary>Booms longer than this (the far views, buses) look further ahead and change their pitch more slowly,
        /// by √(length / this) up to <see cref="MaxLongBoomScale"/>: the camera at the end of a long boom moves that much
        /// farther for the same change of angle, so it needs the warning earlier to move as gently.</summary>
        public const float LongBoomM = 8f;

        public const float MaxLongBoomScale = 1.8f;

        /// <summary>A boom cut short is first raised in <see cref="LiftSteps"/> steps of this many degrees (45° in all:
        /// steeper, a short boom over the player's head frames worse than the overhead fallback).</summary>
        public const float LiftStepDeg = 11.25f;

        public const int LiftSteps = 4;
        public const float MaxPitchDeg = 78f;

        /// <summary>... or lowered by up to <see cref="LowerSteps"/> steps of this many degrees, under an overhang: an
        /// upper floor or a balcony over the street (above <c>RoadClearance.MinOverheadClearanceM</c>), a foot overbridge
        /// or a flyover deck cuts a high boom (portrait, the far view), and a flatter one passes beneath it.</summary>
        public const float LowerStepDeg = 7.5f;

        public const int LowerSteps = 2;

        /// <summary>A lowered boom stays at least this steep (the camera above the pivot, the ground clamp idle).</summary>
        public const float MinLoweredPitchDeg = 4f;

        /// <summary>Steeper (or flatter) booms are tried when the free boom, now or where the pivot will be in
        /// <see cref="PredictS"/>, is shorter than this share of the wanted one.</summary>
        public const float RaiseWhenShortShare = 0.85f;

        /// <summary>A change of pitch is good enough when it frees this share of the wanted boom, now and where the pivot
        /// will be.</summary>
        public const float RaiseGoodShare = 0.92f;

        /// <summary>A change of pitch may move the camera at most this far (the arc the boom's end swings through, metres):
        /// 4 steps of 11.25° on a 6 m walking boom, 2 on a scooter's or a car's, 1 on a far view's; beyond that the boom
        /// shortens instead, so a long boom never swoops up and over a tall corner.</summary>
        public const float MaxPitchSwingM = 4f;

        /// <summary>Lift smoothing: away from the rig's pitch, back to it (seconds), and the hold before it goes back.</summary>
        public const float LiftUpSeconds = 0.22f, LiftDownSeconds = 0.9f, LiftHoldSeconds = 0.6f;

        // The pitch offsets tried (LowerSteps flatter, LiftSteps steeper), smallest change first, lowering first at equal
        // size: -7.5, +11.25, -15, +22.5, +33.75, +45 degrees.
        private static readonly float[] LiftOffsets = PitchOffsets();

        public const float LaneProbeM = 3f;
        public const float LanePitchDeg = 4f;
        public const float LaneDistanceScale = 0.9f;
        public const float LaneBlendSeconds = 1.6f;

        /// <summary>Walls on both sides for this long enter the lane frame ...</summary>
        public const float LaneEnterS = 0.5f;

        /// <summary>... and no walls for this long leave it.</summary>
        public const float LaneExitS = 2.5f;

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

        private CameraSpring _len;
        private bool _started;
        private float _hold;
        private bool _easing;
        private CameraSpring _lift;
        private float _liftTarget;
        private float _liftDrop;
        private float _lane;
        private float _laneTimer;
        private bool _laneWalls, _laneOn;
        private float _laneSeen, _laneGone;
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

        /// <summary>Pitch added to clear a low wall (positive) or to pass under an overhang (negative), degrees (smoothed).</summary>
        public float LiftDeg
        {
            get { return _lift.Value; }
        }

        /// <summary>Lane frame blend, 0 to 1 (eased).</summary>
        public float Lane01
        {
            get { return CharMath.SmoothStep(_lane); }
        }

        /// <summary>Free length along this frame's boom direction (with the skin taken off).</summary>
        public float ClearM { get; private set; }

        /// <summary>The length the boom is heading for this frame (the nearer of this frame's room and the room where the
        /// pivot will be), metres, for diagnostics and tests.</summary>
        public float TargetM { get; private set; }

        /// <summary>Something cut the boom short this frame.</summary>
        public bool Blocked { get; private set; }

        /// <summary>The hard limit cut the smoothed boom this frame (the prediction missed a wall): a snap.</summary>
        public bool Clamped { get; private set; }

        /// <summary>Sweeps made by the last <see cref="Solve(IViewObstacleQuery, double, double, double, float, float, float, float, float)"/>.</summary>
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
            _started = false;
            _hold = 0f;
            _easing = false;
            _lift.Reset(0f);
            _liftTarget = 0f;
            _liftDrop = 0f;
            _laneTimer = 0f;
            _laneSeen = _laneGone = 0f;
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
            Solve(query, px, py, pz, yawDeg, pitchDeg, wantedM, minM, dt, 0f, 0f, 0f);
        }

        /// <summary>
        /// As <see cref="Solve(IViewObstacleQuery, double, double, double, float, float, float, float, float)"/>, with
        /// where the pivot moves (<paramref name="leadX"/>, <paramref name="leadZ"/>, metres) and how far the boom's yaw
        /// turns (<paramref name="leadYawDeg"/>) over the next <see cref="PredictS"/> (times <see cref="LongBoomScale"/> of
        /// the wanted length): the boom then changes its pitch, or shortens, early and smoothly for a wall that is about
        /// to cut it.
        /// </summary>
        public void Solve(IViewObstacleQuery query, double px, double py, double pz, float yawDeg, float pitchDeg, float wantedM,
                          float minM, float dt, float leadX, float leadZ, float leadYawDeg)
        {
            Casts = 0;
            Clamped = false;
            if (!(dt >= 0f) || float.IsInfinity(dt)) dt = 0f;
            if (float.IsNaN(wantedM) || wantedM < 0f) wantedM = 0f;
            if (float.IsNaN(pitchDeg)) pitchDeg = 0f;
            if (float.IsNaN(leadX) || float.IsNaN(leadZ) || float.IsInfinity(leadX) || float.IsInfinity(leadZ)) leadX = leadZ = 0f;
            if (float.IsNaN(leadYawDeg) || float.IsInfinity(leadYawDeg)) leadYawDeg = 0f;
            bool snap = !_started;

            UpdateLane(query, px, py, pz, yawDeg, dt, snap);
            float lane = Lane01;
            float pitch = Math.Min(MaxPitchDeg, pitchDeg + LanePitchDeg * lane);
            float want = wantedM * (1f + (LaneDistanceScale - 1f) * lane);
            float min = Math.Min(Math.Max(0f, minM), want);
            float longScale = LongBoomScale(want);

            // Where the pivot and the boom's yaw will be shortly: walls to come are met early (the lift below and the
            // length's spring both look there). Only from a pivot the body can reach: a lead that runs into a wall is cut
            // short of it, so a house beside a bend never collapses the boom early.
            bool predict = false;
            double lpx = px, lpz = pz;
            float lyaw = yawDeg;
            if (query != null && (leadX * leadX + leadZ * leadZ > 0.04f || Math.Abs(leadYawDeg) > 1f))
            {
                float lead = MathF.Sqrt(leadX * leadX + leadZ * leadZ);
                float free = 1f;
                if (lead > 0.2f)
                {
                    Casts++;
                    double hit;
                    if (query.SphereCast(px, py, pz, leadX / lead, 0.0, leadZ / lead, CoreRadiusM, lead, out hit))
                        free = (float)Math.Max(0.0, (hit - ProbeRadiusM) / lead);
                }
                if (free > 0.05f)
                {
                    predict = true;
                    lpx = px + leadX * free;
                    lpz = pz + leadZ * free;
                    lyaw = yawDeg + leadYawDeg;
                }
            }

            // Change the pitch rather than pull in: a steeper boom over a low wall, a parked truck or a railing, or a flatter
            // one under an overhang, that keeps (most of) the length beats a shorter one. Decided on this frame's boom and
            // the predicted one together, so the pitch starts to move before the wall arrives instead of the length.
            float clear0 = query != null ? Clear(query, px, py, pz, yawDeg, pitch, want) : want;
            float soon0 = predict ? Clear(query, lpx, py, lpz, lyaw, pitch, want) : clear0;
            float worst0 = Math.Min(clear0, soon0);
            float wantedLift = 0f;
            if (query != null && worst0 < want * RaiseWhenShortShare)
            {
                // The smallest change of pitch that frees (nearly) the whole boom, now and where the pivot will be, and
                // moves the camera no farther than MaxPitchSwingM: a long boom does not swoop up 30° round a tall corner
                // (it shortens there instead), a short one still rises over a wall.
                float best = worst0, bestLift = 0f, good = 0f;
                bool found = false;
                float enough = Math.Max(min, want * RaiseGoodShare);
                // The side the boom already leans to first: swinging over to the other side would sweep the camera through
                // the obstacle it is avoiding (under an overhang, a steep boom clears it again once past its far edge).
                float side = Math.Sign(_liftTarget);
                for (int pass = side != 0f ? 0 : 1; pass < 2 && !found; pass++)
                {
                    for (int k = 0; k < LiftOffsets.Length; k++)
                    {
                        float o = LiftOffsets[k];
                        if (pass == 0 && Math.Sign(o) != side) continue;
                        if (pass == 1 && side != 0f && Math.Sign(o) == side) continue;
                        float p = o < 0f ? Math.Max(Math.Min(pitch, MinLoweredPitchDeg), pitch + o) : Math.Min(MaxPitchDeg, pitch + o);
                        if (Math.Abs(p - pitch) < 0.5f * Math.Abs(o)) continue; // clamped away (too flat already, or too steep)
                        if (want * Math.Abs(p - pitch) * (float)Deg2Rad > MaxPitchSwingM) continue;
                        float c = Clear(query, px, py, pz, yawDeg, p, want);
                        if (predict && c > best + 0.25f) c = Math.Min(c, Clear(query, lpx, py, lpz, lyaw, p, want));
                        if (c > best + 0.25f)
                        {
                            best = c;
                            bestLift = p - pitch;
                        }
                        if (c >= enough)
                        {
                            good = p - pitch;
                            found = true;
                            // Halfway back to the last smaller step the same way (or the rig's pitch) may be enough: the
                            // smallest change moves the camera least.
                            float prev = 0f;
                            for (int j = k - 1; j >= 0; j--)
                            {
                                if (Math.Sign(LiftOffsets[j]) != Math.Sign(o)) continue;
                                prev = LiftOffsets[j];
                                break;
                            }
                            float half = 0.5f * (good + prev);
                            if (Math.Abs(half) > 1f)
                            {
                                float ch = Clear(query, px, py, pz, yawDeg, pitch + half, want);
                                if (predict && ch >= enough) ch = Math.Min(ch, Clear(query, lpx, py, lpz, lyaw, pitch + half, want));
                                if (ch >= enough) good = half;
                            }
                            break;
                        }
                    }
                }
                // Change the pitch when it keeps the length, or when it at least restores the minimum.
                if (found) wantedLift = good;
                else if (worst0 < min - 1e-3f && best > worst0 + 0.25f) wantedLift = bestLift;
            }
            // (Room at the rig's pitch again: the lift is kept for LiftHoldSeconds below, so a gap in a wall never drops it.)
            if (snap)
            {
                _liftTarget = wantedLift;
                _liftDrop = 0f;
                _lift.Reset(wantedLift);
            }
            else
            {
                // A bigger change, or one the other way, is needed now and taken at once; going back towards the rig's
                // pitch waits for the hold.
                bool relax = wantedLift * _liftTarget >= 0f && Math.Abs(wantedLift) < Math.Abs(_liftTarget) - 1e-3f;
                if (!relax)
                {
                    _liftTarget = wantedLift;
                    _liftDrop = 0f;
                }
                else
                {
                    _liftDrop += dt;
                    if (_liftDrop >= LiftHoldSeconds)
                    {
                        _liftTarget = wantedLift;
                        _liftDrop = 0f;
                    }
                }
                bool away = Math.Abs(_liftTarget) > Math.Abs(_lift.Value) || _liftTarget * _lift.Value < 0f;
                _lift.Step(_liftTarget, (away ? LiftUpSeconds : LiftDownSeconds) * longScale, 0f, dt);
                if (Math.Abs(_lift.Value) < 0.01f && _liftTarget == 0f) _lift.Reset(0f);
            }

            float finalPitch = Math.Min(MaxPitchDeg, Math.Max(Math.Min(pitch, MinLoweredPitchDeg), pitch + _lift.Value));
            bool pitched = Math.Abs(finalPitch - pitch) > 0.01f;
            float clearBase = clear0;
            if (query != null && pitched) clearBase = Clear(query, px, py, pz, yawDeg, finalPitch, want);

            // The overhead fallback: no room for a boom behind the player. In at once, out after a short hold.
            UpdateOverhead(query != null, clearBase, want, dt, snap);
            float rise = 0f, hard = clearBase;
            if (_over > 0f && query != null)
            {
                rise = Reach(query, px, py, pz, 0.0, 1.0, 0.0, OverheadRiseM) * CharMath.SmoothStep(_over);
                if (rise > 1e-3f) hard = Clear(query, px, py + rise, pz, yawDeg, finalPitch, want);
                else rise = 0f;
            }

            // Walls to come: the boom from where the pivot and its yaw will be shortly, at the pitch it is moving to (the lift
            // spring gets most of the way there in that time, so a pitch change under way does not also pull the boom in).
            float soft = hard;
            if (predict && rise <= 0f)
            {
                float soonPitch = Math.Min(MaxPitchDeg, Math.Max(Math.Min(pitch, MinLoweredPitchDeg), pitch + _liftTarget));
                soft = Math.Min(soft, Math.Abs(soonPitch - pitch) > 0.01f ? Clear(query, lpx, py, lpz, lyaw, soonPitch, want) : soon0);
            }

            float target = Math.Min(want, soft);
            TargetM = target;
            float before = _len.Value, rateMax = MaxLengthSharePerS * Math.Max(1f, want);
            if (snap)
            {
                _len.Reset(Math.Min(want, hard));
                _hold = 0f;
                _easing = false;
                _started = true;
            }
            else if (target < _len.Value - 1e-3f)
            {
                _easing = false;
                _hold = HoldSeconds;
                _len.Step(target, InSeconds, 0f, dt);
            }
            else
            {
                if (_hold > 0f) _hold -= dt;
                // Out again after the hold; after a wall (not a zoom) only once there is clearly room (hysteresis).
                float margin = Blocked ? OutMarginM : 1e-3f;
                if (!_easing && _hold <= 0f && target > _len.Value + margin) _easing = true;
                if (_easing)
                {
                    _len.Step(target, OutSeconds, 0f, dt);
                    if (target - _len.Value < 1e-3f)
                    {
                        _len.Reset(target);
                        _easing = false;
                    }
                }
                else
                {
                    _len.Step(_len.Value, OutSeconds, 0f, dt);
                }
            }
            // Shortening for a wall: never faster than MaxLengthSharePerS of the boom per second (a change that big in one
            // frame is a snap the player sees); only the hard limit below may cut it shorter. (A zoom is the player's own
            // and follows at the spring's pace.)
            if (!snap && dt > 0f && target < want - 1e-3f && before - _len.Value > rateMax * dt)
            {
                _len.Value = before - rateMax * dt;
                _len.Velocity = -rateMax;
            }
            if (_len.Value > hard && hard < want - 1e-3f)
            {
                // The early look missed a wall: never inside or behind it. The boom stops there (no velocity carried on
                // past the wall, so the hold that follows really holds).
                Clamped = _len.Value - hard > 0.02f;
                _len.Value = hard;
                _len.Velocity = 0f;
            }
            if (_len.Value < 0f) _len.Value = 0f;
            Blocked = hard < want - 1e-3f;
            ClearM = hard;
            DistanceM = _len.Value;
            PitchDeg = finalPitch;
            RiseM = rise;
        }

        private static float[] PitchOffsets()
        {
            var o = new float[LowerSteps + LiftSteps];
            int n = 0;
            for (int k = 1; k <= LowerSteps; k++) o[n++] = -k * LowerStepDeg;
            for (int k = 1; k <= LiftSteps; k++) o[n++] = k * LiftStepDeg;
            Array.Sort(o, (a, b) =>
            {
                int c = Math.Abs(a).CompareTo(Math.Abs(b));
                return c != 0 ? c : a.CompareTo(b);
            });
            return o;
        }

        /// <summary>How much earlier and slower a boom of <paramref name="lengthM"/> reacts (<see cref="LongBoomM"/>): 1 up
        /// to 8 m, √(length / 8 m) beyond, at most <see cref="MaxLongBoomScale"/>.</summary>
        public static float LongBoomScale(float lengthM)
        {
            if (!(lengthM > LongBoomM)) return 1f;
            return Math.Min(MaxLongBoomScale, MathF.Sqrt(lengthM / LongBoomM));
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
                _laneOn = false;
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
                if (_laneWalls)
                {
                    _laneSeen += dt;
                    _laneGone = 0f;
                }
                else
                {
                    _laneGone += dt;
                    _laneSeen = 0f;
                }
                if (snap) _laneOn = _laneWalls;
                else if (!_laneOn && _laneSeen >= LaneEnterS) _laneOn = true;
                else if (_laneOn && _laneGone >= LaneExitS) _laneOn = false;
            }
            float goal = _laneOn ? 1f : 0f;
            if (snap) _lane = goal;
            else
            {
                float step = dt / LaneBlendSeconds;
                _lane = _lane < goal ? Math.Min(goal, _lane + step) : Math.Max(goal, _lane - step);
            }
        }
    }
}
