using System;

namespace Ghumante.Core.Characters
{
    /// <summary>
    /// A critically damped spring on one value (docs/research/w2/ref_camera.md §3; Lowe, Game Programming Gems 4 1.10;
    /// Juckett, "Damped Springs"), stepped with the <b>exact</b> solution for a goal that moves in a straight line during
    /// the step, so the result does not depend on how the frame time is cut up: the same ride at 30 fps, 60 fps or with an
    /// uneven frame clock gives the same curve (Unity's <c>SmoothDamp</c> holds the goal still during the step and so
    /// lags by an amount that changes with every frame time, which shakes a chase camera). It never overshoots a resting
    /// goal. <c>ω = 2 / smoothTime</c>: a step settles in about one smooth time.
    /// <para><b>Feed-forward</b> (0 to 1): 0 is the plain spring, which trails a goal moving at speed v by
    /// <c>v · smoothTime</c>; 1 follows the goal's own velocity, so it trails only while the goal accelerates (and by
    /// <c>a · smoothTime² / 4</c>). A mutable struct: keep it in a field. No allocation.</para>
    /// </summary>
    public struct CameraSpring
    {
        /// <summary>The spring's value and its rate of change (per second).</summary>
        public float Value, Velocity;

        private float _goal;
        private bool _hasGoal;

        /// <summary>Jumps to <paramref name="value"/> at rest; the next goal's motion is measured from it.</summary>
        public void Reset(float value)
        {
            Value = value;
            Velocity = 0f;
            _goal = value;
            _hasGoal = true;
        }

        /// <summary>Moves every remembered value by <paramref name="delta"/> (a floating-origin rebase).</summary>
        public void Shift(float delta)
        {
            Value += delta;
            _goal += delta;
        }

        /// <summary>
        /// Follows <paramref name="goal"/> (where it is at the end of this step; it moved in a straight line from the last
        /// goal) for <paramref name="dt"/> seconds with <paramref name="smoothTimeS"/> and <paramref name="feedForward"/>.
        /// A first call (or after <see cref="Reset"/> with another value) starts from rest at the goal.
        /// </summary>
        public float Step(float goal, float smoothTimeS, float feedForward, float dt)
        {
            if (float.IsNaN(goal) || float.IsInfinity(goal)) return Value;
            if (!_hasGoal)
            {
                Reset(goal);
                return Value;
            }
            if (!(dt > 0f) || float.IsInfinity(dt))
            {
                return Value;
            }
            float u = (goal - _goal) / dt;
            _goal = goal;
            Advance(goal, u, smoothTimeS, feedForward, dt, ref Value, ref Velocity);
            return Value;
        }

        /// <summary>
        /// Follows <paramref name="goal"/> with full feed-forward of a velocity the caller estimates
        /// (<paramref name="goalVelocity"/>, e.g. a slow average of the goal's own motion) instead of the goal's motion over
        /// this step: a steady climb is followed without lag, while a sudden step of the goal (a kerb, a seam between two
        /// surfaces) is taken in smoothly over about <paramref name="smoothTimeS"/>.
        /// </summary>
        public float StepTracking(float goal, float goalVelocity, float smoothTimeS, float dt)
        {
            if (float.IsNaN(goal) || float.IsInfinity(goal)) return Value;
            if (float.IsNaN(goalVelocity) || float.IsInfinity(goalVelocity)) goalVelocity = 0f;
            if (!_hasGoal)
            {
                Reset(goal);
                return Value;
            }
            if (!(dt > 0f) || float.IsInfinity(dt)) return Value;
            _goal = goal;
            Advance(goal, goalVelocity, smoothTimeS, 1f, dt, ref Value, ref Velocity);
            return Value;
        }

        /// <summary>The exact critically damped step towards a goal at <paramref name="goal"/> moving at
        /// <paramref name="u"/> (it was at goal − u·dt when the step began).</summary>
        internal static void Advance(float goal, float u, float smoothTimeS, float feedForward, float dt, ref float x, ref float v)
        {
            if (!(smoothTimeS > 1e-4f))
            {
                x = goal;
                v = u;
                return;
            }
            float k = feedForward < 0f ? 0f : feedForward > 1f ? 1f : feedForward;
            float w = 2f / smoothTimeS;
            float g0 = goal - u * dt;
            float e0 = x - g0, ed0 = v - u;
            float ep = -2f * (1f - k) * u / w;
            float a = e0 - ep;
            float b = ed0 + w * a;
            float decay = MathF.Exp(-w * dt);
            float f = (a + b * dt) * decay;
            float fd = (b - w * (a + b * dt)) * decay;
            x = goal + ep + f;
            v = u + fd;
            if (float.IsNaN(x) || float.IsInfinity(x))
            {
                x = goal;
                v = u;
            }
        }
    }

    /// <summary>
    /// <see cref="CameraSpring"/> on an angle in degrees: errors and the goal's motion are taken along the shorter arc,
    /// and the value stays in [−180, 180). No allocation.
    /// </summary>
    public struct CameraAngleSpring
    {
        /// <summary>The angle, degrees in [−180, 180), and its rate, degrees per second.</summary>
        public float Value, Velocity;

        private float _goal;
        private bool _hasGoal;

        public void Reset(float valueDeg)
        {
            Value = Wrap(valueDeg);
            Velocity = 0f;
            _goal = Value;
            _hasGoal = true;
        }

        /// <summary>Follows <paramref name="goalDeg"/> (it turned along the shorter arc from the last goal during the step).</summary>
        public float Step(float goalDeg, float smoothTimeS, float feedForward, float dt)
        {
            if (float.IsNaN(goalDeg) || float.IsInfinity(goalDeg)) return Value;
            goalDeg = Wrap(goalDeg);
            if (!_hasGoal)
            {
                Reset(goalDeg);
                return Value;
            }
            if (!(dt > 0f) || float.IsInfinity(dt)) return Value;
            float turned = Delta(_goal, goalDeg);
            float u = turned / dt;
            _goal = goalDeg;
            // Work in a frame where the goal sits at 0: the value is its signed offset along the shorter arc.
            float x = Delta(goalDeg, Value), v = Velocity;
            CameraSpring.Advance(0f, u, smoothTimeS, feedForward, dt, ref x, ref v);
            Value = Wrap(goalDeg + x);
            Velocity = v;
            return Value;
        }

        /// <summary>How far the angle will have turned in <paramref name="t"/> seconds (degrees, signed) while following a goal
        /// that is at <paramref name="goalDeg"/> now and turns at <paramref name="goalRateDegPerS"/> (the exact solution;
        /// the spring is not changed): the chase camera's boom looks where its yaw is going in a turn.</summary>
        public float PredictTurn(float goalDeg, float goalRateDegPerS, float smoothTimeS, float t)
        {
            if (!(t > 0f) || float.IsInfinity(t) || float.IsNaN(goalDeg) || float.IsNaN(goalRateDegPerS) || float.IsInfinity(goalRateDegPerS)) return 0f;
            float end = goalDeg + goalRateDegPerS * t;
            float x = Delta(end, Value), v = Velocity;
            CameraSpring.Advance(0f, goalRateDegPerS, smoothTimeS, 0f, t, ref x, ref v);
            return Delta(Value, Wrap(end + x));
        }

        /// <summary>Turns the goal and the value by <paramref name="deg"/> without motion (a hard re-aim).</summary>
        public void Rotate(float deg)
        {
            Value = Wrap(Value + deg);
            _goal = Wrap(_goal + deg);
        }

        /// <summary>Signed shortest turn from <paramref name="from"/> to <paramref name="to"/>, degrees in [−180, 180).</summary>
        public static float Delta(float from, float to)
        {
            return Wrap(to - from);
        }

        /// <summary>Wraps degrees into [−180, 180).</summary>
        public static float Wrap(float deg)
        {
            if (float.IsNaN(deg) || float.IsInfinity(deg)) return 0f;
            deg %= 360f;
            if (deg >= 180f) deg -= 360f;
            else if (deg < -180f) deg += 360f;
            return deg;
        }
    }

    /// <summary>
    /// The frame clock for the player and the camera (owner playtest #5; ref_camera.md §3): Unity's measured frame
    /// time wanders by a few milliseconds around a steady display interval on phones (and alternates 16.7 / 33.3 ms when
    /// a 40 fps target meets a 60 Hz screen), which makes the world judder even when everything else is smooth. This turns
    /// the raw unscaled frame time into an even one: an average over about ten frames plus a slow repayment of what the
    /// average left out, so over any stretch the game time still equals the real time (within a few milliseconds) and a
    /// hitch is spread over the next half second instead of jumping. Frames longer than <see cref="MaxFrameS"/> count as
    /// that long (as the explorer clamps them). Engine-free; no allocation.
    /// </summary>
    public sealed class CameraClock
    {
        public const float MaxFrameS = 0.1f;

        /// <summary>Weight of each new frame in the running average.</summary>
        public const float AverageWeight = 0.1f;

        /// <summary>Share of the outstanding time repaid each frame.</summary>
        public const float RepayShare = 0.1f;

        private float _average = -1f;
        private double _backlog;

        /// <summary>The last smoothed frame time, seconds.</summary>
        public float Dt { get; private set; }

        /// <summary>Real time not yet handed out (positive) or handed out early (negative), seconds.</summary>
        public double Backlog
        {
            get { return _backlog; }
        }

        /// <summary>Forget the history (after a pause or a load: the next frame starts the average afresh).</summary>
        public void Reset()
        {
            _average = -1f;
            _backlog = 0.0;
            Dt = 0f;
        }

        /// <summary>The smoothed time of a frame that really took <paramref name="rawDt"/> seconds (unscaled).</summary>
        public float Tick(float rawDt)
        {
            if (!(rawDt > 0f) || float.IsInfinity(rawDt)) rawDt = 0f;
            if (rawDt > MaxFrameS) rawDt = MaxFrameS;
            if (_average < 0f) _average = rawDt;
            else _average += (rawDt - _average) * AverageWeight;
            float dt = _average + (float)(RepayShare * _backlog);
            float lo = 0.25f * _average, hi = Math.Min(MaxFrameS, 2.5f * _average + 1e-3f);
            if (dt < lo) dt = lo;
            if (dt > hi) dt = hi;
            _backlog += rawDt - dt;
            // Never owe more than a few frames: a long stall is dropped, not replayed.
            if (_backlog > 0.25) _backlog = 0.25;
            if (_backlog < -0.25) _backlog = -0.25;
            Dt = dt;
            return dt;
        }
    }
}
