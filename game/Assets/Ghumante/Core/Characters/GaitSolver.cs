using System;

namespace Ghumante.Core.Characters
{
    /// <summary>What one foot does this frame (relative to the hip, along the body's forward axis).</summary>
    public struct FootState
    {
        /// <summary>Forward offset of the ankle from under the hip, metres (+ ahead).</summary>
        public float Forward;

        /// <summary>Lift of the foot above the ground, metres.</summary>
        public float Lift;

        /// <summary>Toe pitch, degrees (+ toe up), for the heel-to-toe roll.</summary>
        public float PitchDeg;

        /// <summary>True during stance (the foot is planted in the world).</summary>
        public bool Planted;
    }

    /// <summary>
    /// The phase-driven gait generator (W2_DESIGN 6.1, P §4.1 and §5.2). One cycle is two steps; the left foot runs at
    /// phase φ, the right at φ + 0.5. Step length is <c>0.42·leg + 0.18·v</c> walking and <c>0.55·leg + 0.16·v</c>
    /// running, blended between 1.6 and 2.0 m/s (the Froude walk–run switch is near 1.76 m/s for a 0.63 m leg); cadence
    /// is v / step (2.9 steps/s at 1.6 m/s). Stance lasts 60% of the cycle walking and 35% running, and the stance foot
    /// travels back at exactly the body's speed, so feet do not slide. Swing arcs lift 0.07 m walking and 0.16 m running
    /// with a sin² ease. Heel strikes are reported (<see cref="LeftStrike"/>, <see cref="RightStrike"/>) for footsteps,
    /// squash and dust. Pelvis bob (0.025 m at twice the step rate), pelvis roll (±4°), arm swing (±22° walking, ±35°
    /// running) and torso twist (±6°) come out with it. Engine-free, allocation-free.
    /// </summary>
    public sealed class GaitSolver
    {
        public const float WalkStanceFrac = 0.6f, RunStanceFrac = 0.35f;
        public const float WalkLiftM = 0.07f, RunLiftM = 0.16f;
        public const float RunBlendStart = 1.6f, RunBlendEnd = 2.0f;
        public const float BobM = 0.025f, PelvisRollMaxDeg = 4f, WalkArmSwingDeg = 22f, RunArmSwingDeg = 35f, TorsoTwistMaxDeg = 6f;

        /// <summary>Cadence changes are eased over this time (no popping when the speed changes).</summary>
        public const float CadenceEaseS = 0.15f;

        private readonly float _leg;
        private float _phase;
        private float _cadence;
        private float _amount;

        public GaitSolver(float legLengthM = 0.63f)
        {
            _leg = legLengthM > 0.1f ? legLengthM : 0.63f;
        }

        /// <summary>The cycle phase in [0, 1) (left foot).</summary>
        public float Phase
        {
            get { return _phase; }
        }

        /// <summary>Steps per second now.</summary>
        public float Cadence
        {
            get { return _cadence; }
        }

        /// <summary>0 standing, 1 walking or running (eases in and out so starting and stopping blend).</summary>
        public float Amount
        {
            get { return _amount; }
        }

        /// <summary>0 walking, 1 running.</summary>
        public float Run { get; private set; }

        public float StepLengthM { get; private set; }

        public FootState Left, Right;

        /// <summary>A heel struck the ground this step.</summary>
        public bool LeftStrike, RightStrike;

        public float PelvisBobM { get; private set; }
        public float PelvisRollDeg { get; private set; }
        public float ArmSwingDeg { get; private set; }
        public float TorsoTwistDeg { get; private set; }

        /// <summary>Step length for a speed (walk–run blended).</summary>
        public float StepLength(float speedMps)
        {
            float v = Math.Abs(speedMps);
            float walk = 0.42f * _leg + 0.18f * v, run = 0.55f * _leg + 0.16f * v;
            float t = CharMath.SmoothStep((v - RunBlendStart) / (RunBlendEnd - RunBlendStart));
            return CharMath.Lerp(walk, run, t);
        }

        /// <summary>Steps per second for a speed (v / step).</summary>
        public float CadenceFor(float speedMps)
        {
            float v = Math.Abs(speedMps);
            return v < 1e-3f ? 0f : v / StepLength(v);
        }

        /// <summary>Advances the gait by <paramref name="dt"/> at <paramref name="speedMps"/> (the body's forward speed;
        /// negative walks backwards). Does not allocate.</summary>
        public void Step(float dt, float speedMps)
        {
            LeftStrike = RightStrike = false;
            if (!(dt > 0f) || float.IsInfinity(dt)) return;
            float v = CharMath.Finite(speedMps);
            float a = Math.Abs(v);
            float targetAmount = CharMath.Clamp01(a / 0.3f);
            _amount = CharMath.Approach(_amount, targetAmount, 10f, dt);
            float target = CadenceFor(a);
            _cadence = CharMath.Approach(_cadence, target, 1f / CadenceEaseS, dt);
            if (target <= 0f && _cadence < 0.3f) _cadence = 0f;
            Run = CharMath.SmoothStep((a - RunBlendStart) / (RunBlendEnd - RunBlendStart));
            float step = StepLength(a);
            StepLengthM = step;
            float prev = _phase;
            if (_cadence > 0f)
            {
                _phase += _cadence * dt * 0.5f;
                _phase -= MathF.Floor(_phase);
            }
            else if (_amount < 0.05f)
            {
                // Settle the feet together at rest.
                _phase = CharMath.Approach(_phase, _phase < 0.5f ? 0.25f : 0.75f, 6f, dt);
            }
            // Heel strike at phase 0 (left) and 0.5 (right).
            if (_cadence > 0f)
            {
                if (prev > _phase) LeftStrike = true;
                if (prev < 0.5f && _phase >= 0.5f) RightStrike = true;
            }
            float stance = CharMath.Lerp(WalkStanceFrac, RunStanceFrac, Run);
            float lift = CharMath.Lerp(WalkLiftM, RunLiftM, Run);
            float sign = v < 0f ? -1f : 1f;
            Left = Foot(_phase, stance, step, lift, sign);
            Right = Foot((_phase + 0.5f) % 1f, stance, step, lift, sign);
            float k = _amount;
            Left.Forward *= k;
            Left.Lift *= k;
            Right.Forward *= k;
            Right.Lift *= k;
            float twoPi = 6.28318530718f;
            PelvisBobM = BobM * (1f + Run) * k * (0.5f - 0.5f * MathF.Cos(2f * twoPi * _phase));
            PelvisRollDeg = PelvisRollMaxDeg * k * MathF.Sin(twoPi * _phase);
            ArmSwingDeg = CharMath.Lerp(WalkArmSwingDeg, RunArmSwingDeg, Run) * k * MathF.Cos(twoPi * _phase) * sign;
            TorsoTwistDeg = TorsoTwistMaxDeg * k * MathF.Cos(twoPi * _phase);
        }

        /// <summary>
        /// One foot at cycle phase <paramref name="p"/>: stance from +A to −A (A = stance × step, so the planted foot moves
        /// back at the body's speed), then a swing arc from −A to +A with a sin² lift.
        /// </summary>
        public static FootState Foot(float p, float stance, float step, float lift, float sign)
        {
            float amp = stance * step;
            var f = new FootState();
            if (p < stance)
            {
                float t = p / stance;
                f.Forward = sign * amp * (1f - 2f * t);
                f.Lift = 0f;
                f.Planted = true;
                f.PitchDeg = t < 0.15f ? 12f * (1f - t / 0.15f) : t > 0.85f ? -18f * (t - 0.85f) / 0.15f : 0f;
            }
            else
            {
                float t = (p - stance) / (1f - stance);
                float e = t * t * (3f - 2f * t);
                f.Forward = sign * amp * (-1f + 2f * e);
                float s = MathF.Sin(MathF.PI * t);
                f.Lift = lift * s * s;
                f.Planted = false;
                f.PitchDeg = -10f * MathF.Sin(MathF.PI * t) + 8f * t * t;
            }
            return f;
        }
    }

    /// <summary>
    /// The on-foot jump (W2_DESIGN 6.1): apex 1.0 m reached in 0.32 s (so take-off 6.25 m/s against a jump gravity of
    /// 19.5 m/s²), coyote time 0.10 s after leaving the ground and a 0.12 s input buffer before landing. The jump is a
    /// height above the walker's ground, so the walker's own physics (step-ups, slopes, blocks) stay in charge of where
    /// the player stands. Landing reports the impact speed for the squash and a "boing". Falls of more than 4 m end in
    /// a roll and a "ta-da", never damage.
    /// </summary>
    public sealed class JumpModel
    {
        public const float ApexM = 1.0f, ApexS = 0.32f, CoyoteS = 0.10f, BufferS = 0.12f, RollFallM = 4f;

        public static readonly float LaunchMps = 2f * ApexM / ApexS;
        public static readonly float GravityMps2 = 2f * ApexM / (ApexS * ApexS);

        private float _sinceGround = 999f;
        private float _buffer = 999f;
        private float _fallFrom;

        /// <summary>Height above the ground, metres (0 on the ground).</summary>
        public float Height { get; private set; }

        public float VerticalSpeed { get; private set; }

        public bool Airborne
        {
            get { return Height > 0f || VerticalSpeed > 0f; }
        }

        /// <summary>The jump left the ground this update.</summary>
        public bool TookOff { get; private set; }

        /// <summary>Landed this update; <see cref="LandingSpeed"/> says how hard.</summary>
        public bool Landed { get; private set; }

        public float LandingSpeed { get; private set; }

        /// <summary>The landing ended a fall of more than <see cref="RollFallM"/>: play the roll and "ta-da".</summary>
        public bool RollLanding { get; private set; }

        /// <summary>
        /// Steps the jump: <paramref name="pressed"/> is a jump press this frame, <paramref name="groundUnder"/> whether the
        /// walker stands on ground, <paramref name="allowed"/> false while mounting, seated or in an emote.
        /// </summary>
        public void Update(float dt, bool pressed, bool groundUnder, bool allowed = true)
        {
            TookOff = Landed = RollLanding = false;
            if (!(dt > 0f)) return;
            if (dt > 0.1f) dt = 0.1f;
            if (pressed) _buffer = 0f;
            else _buffer += dt;
            if (groundUnder && !Airborne) _sinceGround = 0f;
            else _sinceGround += dt;
            if (allowed && !Airborne && _buffer <= BufferS && _sinceGround <= CoyoteS)
            {
                VerticalSpeed = LaunchMps;
                _buffer = 999f;
                _sinceGround = 999f;
                _fallFrom = 0f;
                TookOff = true;
            }
            if (!Airborne) return;
            VerticalSpeed -= GravityMps2 * dt;
            Height += VerticalSpeed * dt;
            if (Height > _fallFrom) _fallFrom = Height;
            if (Height <= 0f)
            {
                Landed = true;
                LandingSpeed = -VerticalSpeed;
                RollLanding = _fallFrom > RollFallM;
                Height = 0f;
                VerticalSpeed = 0f;
            }
        }

        /// <summary>Ends any jump at once (teleport, mount).</summary>
        public void Cancel()
        {
            Height = 0f;
            VerticalSpeed = 0f;
            _buffer = 999f;
        }

        /// <summary>Landing squash for an impact speed: clamp(1 − 0.06·v, 0.75, 1) (P §4.3).</summary>
        public static float LandSquash(float impactMps)
        {
            return CharMath.Clamp(1f - 0.06f * Math.Abs(impactMps), 0.75f, 1f);
        }
    }
}
