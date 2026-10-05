using System;

namespace Ghumante.Core.Characters
{
    /// <summary>How the character sits (or stands) on a vehicle (P §4.6). Append-only.</summary>
    public enum SeatPose : byte
    {
        None = 0,
        Bicycle = 1,
        Scooter = 2,
        Motorbike = 3,
        Cruiser = 4,
        Pillion = 5,
        CarDriver = 6,
        CarPassenger = 7,
        BusDriver = 8,
        TruckDriver = 9,
        TractorDriver = 10,
        BusSeated = 11,
        BusStanding = 12,
        Rickshaw = 13,
    }

    /// <summary>One frame of what the character is doing, from the controller (engine-free).</summary>
    public struct PoseInput
    {
        public float Dt;

        /// <summary>Forward speed on foot, m/s (negative backs up).</summary>
        public float SpeedMps;

        /// <summary>Yaw rate, rad/s (+ clockwise from above), for the lean into turns.</summary>
        public float YawRateRadPerS;

        /// <summary>Forward acceleration, m/s², for the run lean and the start/stop overshoot.</summary>
        public float AccelMps2;

        /// <summary>In the air (a jump or a fall); the poser tucks the knees and raises the arms.</summary>
        public bool Airborne;

        /// <summary>Vertical speed in the air (+ up).</summary>
        public float VerticalMps;

        /// <summary>The seat pose; <see cref="SeatPose.None"/> on foot.</summary>
        public SeatPose Seat;

        /// <summary>Mount blend: 0 standing, 1 seated (the enter and exit clips sweep it).</summary>
        public float Seated01;

        /// <summary>Steering in [−1, 1] (handlebars and the steering wheel).</summary>
        public float Steer;

        /// <summary>Pedal crank angle, radians (bicycles).</summary>
        public float CrankRad;

        /// <summary>A two-wheeler's foot is down (stopped).</summary>
        public bool FootDown;

        /// <summary>Lateral acceleration of the vehicle, m/s² (passenger sway, rider counter-lean).</summary>
        public float LateralMps2;

        public Emote Emote;

        /// <summary>Seconds since the emote started.</summary>
        public float EmoteTime;

        public Fidget Fidget;
        public float FidgetTime;

        /// <summary>Head look-around offsets, degrees (look-at points of interest).</summary>
        public float LookYawDeg, LookPitchDeg;

        /// <summary>Calm mode (sacred areas): smaller gestures, no comic overshoot.</summary>
        public bool Calm;

        /// <summary>Reduce motion: no bob, overshoot or squash.</summary>
        public bool ReducedMotion;
    }

    /// <summary>
    /// The engine-free animation solver of the player (W2_DESIGN 6.1, P §4–5): from a <see cref="PoseInput"/> it writes
    /// local rotations for the 37 <c>hum</c> bones, a hips offset and a squash value, about 70% procedural:
    /// <list type="bullet">
    /// <item>On foot: the <see cref="GaitSolver"/> places the feet (two-bone IK, feet flat with a heel-to-toe roll),
    /// pelvis bob and roll, arm swing, torso twist; the body leans into turns (atan(v·ω/g), ±15°) and forward when it
    /// runs or speeds up (8° + 2° per m/s², 14° sprinting), with springs; breathing and a head micro-sway at rest.</item>
    /// <item>Air: knees tuck 30°, arms up 40°. Landings squash by clamp(1 − 0.06·v, 0.75, 1) and recover with a spring
    /// (f 5, ζ 0.35); every heel strike gives a small squash (0.97 walking, 0.93 running); the squash stays in
    /// [0.72, 1.18].</item>
    /// <item>Seated: per <see cref="SeatPose"/>, the hips at the seat and IK to pegs, floorboards, pedals (crank radius
    /// 0.17 m) or the floor, hands to grips, the rider's waist, the steering wheel (15:1 on cars, 20:1 on buses and
    /// trucks, hands clamped at ±100°) or the knees; motorbike riders lean 15° and add 20% counter-lean; standing bus
    /// passengers hold a rail and sway with the bus (f 1.5, ζ 0.4, 3° per m/s²).</item>
    /// <item>Emotes and fidgets: namaste (palms at the sternum, head bow 15° + spine 8°; namaskar 22° and higher hands),
    /// wave, cheer, ring bell, and the idle fidgets of <see cref="IdleFidgets"/>; calm mode keeps them small.</item>
    /// </list>
    /// Allocation-free after construction; under 0.1 ms per frame on desktop (budget 0.15 ms on Low).
    /// </summary>
    public sealed class CharacterPoser
    {
        public const float SquashMin = 0.72f, SquashMax = 1.18f;
        public const float NamasteBowDeg = 15f, NamasteSpineDeg = 8f, NamaskarBowDeg = 22f, NamaskarHandLiftM = 0.05f;
        public const float NamasteS = 40f / 30f, WaveS = 1f, CheerS = 1.2f, RingBellS = 1f;
        public const float MaxFootLeanDeg = 15f, MotorbikeLeanDeg = 15f, BicycleLeanDeg = 25f;

        private readonly HumanoidSkeleton _sk;
        private readonly GaitSolver _gait;
        private readonly Quat[] _local = new Quat[HumanoidSkeleton.BoneCount];
        private readonly Quat[] _seatLocal = new Quat[HumanoidSkeleton.BoneCount];
        private readonly float _thigh, _shin, _upperArm, _forearm;

        private SecondOrder _forwardLean = SecondOrder.BodyLean();
        private SecondOrder _sideLean = SecondOrder.BodyLean();
        private SecondOrder _topi = SecondOrder.Topi();
        private SecondOrder _topiSide = SecondOrder.Topi();
        private SecondOrder _pack = SecondOrder.Backpack();
        private SecondOrder _hem = SecondOrder.Hem();
        private SecondOrder _headYaw = SecondOrder.LookAt();
        private SecondOrder _headPitch = SecondOrder.LookAt();
        private SecondOrder _sway = new SecondOrder(1.5f, 0.4f, 0f, 0f);
        private SecondOrder _squash = new SecondOrder(5f, 0.35f, 0f, 1f);
        private float _time;
        private float _prevSpeed;
        private float _prevVy;

        public CharacterPoser(BodyBuild build) : this(new HumanoidSkeleton(build))
        {
        }

        public CharacterPoser(HumanoidSkeleton skeleton)
        {
            _sk = skeleton ?? throw new ArgumentNullException(nameof(skeleton));
            _gait = new GaitSolver(skeleton.Metrics.LegM);
            _thigh = V3.Distance(_sk[Bone.ThighL], _sk[Bone.ShinL]);
            _shin = V3.Distance(_sk[Bone.ShinL], _sk[Bone.FootL]);
            _upperArm = V3.Distance(_sk[Bone.UpperArmL], _sk[Bone.ForearmL]);
            _forearm = V3.Distance(_sk[Bone.ForearmL], _sk[Bone.HandL]);
            for (int i = 0; i < _local.Length; i++) _local[i] = Quat.Identity;
            Squash = 1f;
        }

        public HumanoidSkeleton Skeleton
        {
            get { return _sk; }
        }

        public GaitSolver Gait
        {
            get { return _gait; }
        }

        /// <summary>Local rotation per bone relative to the bind pose (index by <see cref="Bone"/>).</summary>
        public Quat[] Local
        {
            get { return _local; }
        }

        /// <summary>Offset of the hips from their bind position (root frame).</summary>
        public V3 HipsOffset { get; private set; }

        /// <summary>Squash of the squash bone (1 = none; volume preserving 1/√s, s, 1/√s).</summary>
        public float Squash { get; private set; }

        /// <summary>A heel struck the ground this frame (footsteps, dust).</summary>
        public bool LeftStrike { get; private set; }

        public bool RightStrike { get; private set; }

        /// <summary>The gait is running (for the footstep set).</summary>
        public bool Running
        {
            get { return _gait.Run > 0.5f; }
        }

        /// <summary>Squashes (positive kick: a landing) or stretches (negative: a take-off) the body.</summary>
        public void KickSquash(float velocity)
        {
            _squash.Kick(velocity);
        }

        /// <summary>A landing at <paramref name="impactMps"/>: squash to clamp(1 − 0.06·v, 0.75, 1), then a bounce.</summary>
        public void Land(float impactMps)
        {
            float s = JumpModel.LandSquash(impactMps);
            _squash.Reset(s);
        }

        /// <summary>Solves the pose for this frame.</summary>
        public void Update(in PoseInput input)
        {
            float dt = input.Dt > 0f && !float.IsInfinity(input.Dt) ? Math.Min(input.Dt, 0.1f) : 0f;
            _time += dt;
            for (int i = 0; i < _local.Length; i++) _local[i] = Quat.Identity;
            float seated = CharMath.Clamp01(input.Seated01);
            bool onFoot = input.Seat == SeatPose.None || seated < 1f || input.Seat == SeatPose.BusStanding;
            float footSpeed = input.Seat == SeatPose.None ? CharMath.Finite(input.SpeedMps) : 0f;
            _gait.Step(dt, footSpeed);
            LeftStrike = _gait.LeftStrike && input.Seat == SeatPose.None && !input.Airborne;
            RightStrike = _gait.RightStrike && input.Seat == SeatPose.None && !input.Airborne;

            // Springs for the leans (on foot).
            float accel = dt > 0f ? (footSpeed - _prevSpeed) / dt : 0f;
            _prevSpeed = footSpeed;
            accel = CharMath.Clamp(CharMath.Finite(input.AccelMps2 != 0f ? input.AccelMps2 : accel), -12f, 12f);
            float speed = Math.Abs(footSpeed);
            float sprint = CharMath.Clamp01((speed - 4.6f) / 1.2f);
            float fwdTarget = (8f * _gait.Run + 6f * sprint) + 2f * CharMath.Clamp(accel, -2f, 3f);
            float sideTarget = MathF.Atan(speed * CharMath.Finite(input.YawRateRadPerS) / 9.81f) * Quat.Rad2Deg;
            sideTarget = CharMath.Clamp(sideTarget, -MaxFootLeanDeg, MaxFootLeanDeg);
            if (input.ReducedMotion)
            {
                fwdTarget *= 0.5f;
                sideTarget *= 0.5f;
            }
            float fwd = _forwardLean.Update(dt, fwdTarget);
            float side = _sideLean.Update(dt, sideTarget);

            HipsOffset = V3.Zero;
            if (onFoot) PoseOnFoot(input, dt, fwd, side);
            if (input.Seat != SeatPose.None && input.Seat != SeatPose.BusStanding && seated > 0f)
            {
                // Seated pose blended over the standing one by the mount clip.
                for (int i = 0; i < _local.Length; i++) _seatLocal[i] = _local[i];
                V3 standHips = HipsOffset;
                for (int i = 0; i < _local.Length; i++) _local[i] = Quat.Identity;
                PoseSeated(input, dt);
                if (seated < 1f)
                {
                    float t = CharMath.SmoothStep(seated);
                    for (int i = 0; i < _local.Length; i++) _local[i] = Quat.Slerp(_seatLocal[i], _local[i], t);
                    HipsOffset = V3.Lerp(standHips, HipsOffset, t);
                }
            }
            else if (input.Seat == SeatPose.BusStanding)
            {
                float sway = _sway.Update(dt, CharMath.Clamp(3f * CharMath.Finite(input.LateralMps2), -12f, 12f));
                _local[(int)Bone.Hips] = Quat.Euler(0f, 0f, sway * 0.5f) * _local[(int)Bone.Hips];
                _local[(int)Bone.Spine] = Quat.Euler(0f, 0f, sway * 0.5f) * _local[(int)Bone.Spine];
                // The right hand holds the overhead rail.
                V3 rail = new V3(0.16f, _sk.Metrics.ShoulderY + 0.42f, 0.06f) + HipsOffset;
                ArmIk(1, rail, new V3(1f, 0f, -0.3f));
            }

            Secondary(input, dt);
            SquashUpdate(input, dt);
        }

        // ----- On foot ----------------------------------------------------------------------------------------------

        private void PoseOnFoot(in PoseInput input, float dt, float fwdLean, float sideLean)
        {
            GaitSolver g = _gait;
            bool calm = input.Calm;
            float rm = input.ReducedMotion ? 0f : 1f;
            float breathe = MathF.Sin(_time * 6.28318530718f * 0.25f);
            float sway = MathF.Sin(_time * 6.28318530718f * 0.3f + 1.3f);
            float rest = 1f - g.Amount;
            float bob = g.PelvisBobM * rm - 0.012f * g.Amount * (1f + g.Run);
            var hips = new V3(0f, bob, 0f);
            float hipsRoll = g.PelvisRollDeg * rm, hipsYaw = -0.5f * g.TorsoTwistDeg * rm;
            float spinePitch = 0.5f * fwdLean + rest * 0.8f * breathe * rm, spineYaw = g.TorsoTwistDeg * rm, spineRoll = -0.5f * sideLean;
            float chestPitch = 0.5f * fwdLean + rest * 1.0f * breathe * rm;
            float headPitch = -0.6f * fwdLean + rest * 1.5f * sway * rm;
            float headYaw = -0.5f * g.TorsoTwistDeg * rm, headRoll = 0.4f * sideLean;

            // Fidgets and emotes as layers on the base pose.
            Fidget f = input.Fidget;
            float ft = input.FidgetTime, fd = IdleFidgets.DurationOf(f);
            float fe = fd > 0f ? Envelope(ft, fd, 0.35f) : 0f;
            if (calm && f != Fidget.HandsTogetherRest) fe *= 0.6f;
            switch (f)
            {
                case Fidget.LookAround:
                    headYaw += 50f * MathF.Sin(ft / fd * 6.28318530718f) * fe;
                    headPitch += -8f * fe;
                    break;
                case Fidget.ShiftWeight:
                    hips = hips + new V3(0.03f * fe, 0f, 0f);
                    hipsRoll += 4f * fe;
                    spineRoll -= 3f * fe;
                    break;
                case Fidget.Yawn:
                    headPitch -= 18f * fe;
                    spinePitch -= 4f * fe;
                    break;
                case Fidget.Stretch:
                    hips = hips + new V3(0f, 0.035f * fe, 0f);
                    spinePitch -= 6f * fe;
                    break;
                case Fidget.CheckPhone:
                    headPitch += 22f * fe;
                    break;
                case Fidget.FlickHair:
                    headRoll += 15f * fe * MathF.Sin(ft * 9f);
                    break;
            }
            Emote em = input.Emote;
            float et = input.EmoteTime;
            float emoteEnv = 0f;
            if (em == Emote.Namaste || em == Emote.Namaskar)
            {
                emoteEnv = Envelope(et, NamasteS, 0.3f);
                float bow = em == Emote.Namaskar ? NamaskarBowDeg : NamasteBowDeg;
                // The eyes close at the bottom of the bow (the face is geometry; the bow carries it).
                headPitch += bow * emoteEnv;
                spinePitch += NamasteSpineDeg * emoteEnv;
            }
            else if (em == Emote.Wave)
            {
                emoteEnv = Envelope(et, WaveS, 0.2f);
                hips = hips + new V3(0f, 0.02f * MathF.Abs(MathF.Sin(et * 15.7f)) * emoteEnv * rm, 0f);
            }
            else if (em == Emote.Cheer && !calm)
            {
                emoteEnv = Envelope(et, CheerS, 0.15f);
                hips = hips + new V3(0f, 0.06f * MathF.Max(0f, MathF.Sin(et * 10.5f)) * emoteEnv * rm, 0f);
                headPitch -= 10f * emoteEnv;
            }
            else if (em == Emote.RingBell)
            {
                emoteEnv = Envelope(et, RingBellS, 0.2f);
                headPitch -= 12f * emoteEnv;
            }

            HipsOffset = hips;
            _local[(int)Bone.Hips] = Quat.Euler(0f, hipsYaw, hipsRoll);
            _local[(int)Bone.Spine] = Quat.Euler(spinePitch, spineYaw * 0.6f, spineRoll);
            _local[(int)Bone.Chest] = Quat.Euler(chestPitch, spineYaw * 0.4f, 0f);
            _local[(int)Bone.Neck] = Quat.Euler(0.3f * headPitch, 0.3f * headYaw, 0f);
            _local[(int)Bone.Head] = Quat.Euler(0.7f * headPitch, 0.7f * headYaw, headRoll);

            // Legs: the feet on the ground under the hips (IK), lifted in swing; in the air the knees tuck 30°.
            bool air = input.Airborne;
            for (int s = 0; s < 2; s++)
            {
                FootState foot = s == 0 ? g.Left : g.Right;
                float hipX = _sk[s == 0 ? Bone.ThighL : Bone.ThighR].X;
                float forward = foot.Forward, lift = foot.Lift, pitch = foot.PitchDeg * rm;
                if (air)
                {
                    forward = 0.08f + (s == 0 ? 0.04f : -0.04f);
                    lift = 0.16f;
                    pitch = -10f;
                }
                if (f == Fidget.ToeTap && s == 1) lift += 0.03f * fe * MathF.Max(0f, MathF.Sin(ft * 12.6f));
                if (f == Fidget.Stretch) lift = Math.Max(lift, 0f);
                var ankle = new V3(hipX * 1.05f, _sk.Metrics.AnkleY + lift + (f == Fidget.Stretch ? 0.03f * fe : 0f), forward);
                LegIk(s, ankle, new V3(0f, 0f, 1f), pitch);
            }

            // Arms: swing opposite to the legs, slightly out; bent at the elbow (15° walking, 85° running).
            float swing = g.ArmSwingDeg * rm;
            float bend = CharMath.Lerp(12f, 85f, g.Run) * g.Amount + 6f * rest;
            for (int s = 0; s < 2; s++)
            {
                float sign = s == 0 ? -1f : 1f;
                float pitch = s == 0 ? swing : -swing;
                float outDeg = sign * (6f + 4f * g.Run);
                if (air)
                {
                    pitch = -40f;
                    outDeg = sign * 25f;
                }
                int o = s * 4;
                _local[8 + o] = Quat.Euler(pitch, 0f, outDeg);
                _local[9 + o] = Quat.Euler(-bend, 0f, 0f);
            }

            // Hand gestures (IK over the swing), strongest first.
            float shoulderY = _sk.Metrics.ShoulderY;
            V3 chestFront = new V3(0f, shoulderY - 0.14f, 0.2f) + hips;
            if (emoteEnv > 0f && (em == Emote.Namaste || em == Emote.Namaskar))
            {
                float lift = em == Emote.Namaskar ? NamaskarHandLiftM : 0f;
                BlendArmIk(0, chestFront + new V3(-0.018f, lift, 0f), new V3(-1f, -0.3f, -0.2f), emoteEnv);
                BlendArmIk(1, chestFront + new V3(0.018f, lift, 0f), new V3(1f, -0.3f, -0.2f), emoteEnv);
            }
            else if (emoteEnv > 0f && em == Emote.Wave)
            {
                float swingX = 0.1f * MathF.Sin(et * 15.7f);
                BlendArmIk(1, new V3(0.3f + swingX, shoulderY + 0.38f, 0.08f) + hips, new V3(1f, -0.5f, 0f), emoteEnv);
            }
            else if (emoteEnv > 0f && em == Emote.Cheer)
            {
                BlendArmIk(0, new V3(-0.25f, shoulderY + 0.48f, 0.05f) + hips, new V3(-1f, 0f, 0f), emoteEnv);
                BlendArmIk(1, new V3(0.25f, shoulderY + 0.48f, 0.05f) + hips, new V3(1f, 0f, 0f), emoteEnv);
            }
            else if (emoteEnv > 0f && em == Emote.RingBell)
            {
                float strike = MathF.Sin(Math.Min(1f, et / RingBellS) * 6.28318530718f) * 0.06f;
                BlendArmIk(1, new V3(0.15f, shoulderY + 0.3f, 0.35f + strike) + hips, new V3(1f, -0.6f, 0f), emoteEnv);
            }
            else if (fe > 0f)
            {
                switch (f)
                {
                    case Fidget.HandsTogetherRest:
                        BlendArmIk(0, chestFront + new V3(-0.02f, -0.12f, -0.02f), new V3(-1f, -0.4f, 0f), fe);
                        BlendArmIk(1, chestFront + new V3(0.02f, -0.12f, -0.02f), new V3(1f, -0.4f, 0f), fe);
                        break;
                    case Fidget.Stretch:
                        BlendArmIk(0, new V3(-0.12f, shoulderY + 0.5f, 0.02f) + hips, new V3(-1f, 0f, 0f), fe);
                        BlendArmIk(1, new V3(0.12f, shoulderY + 0.5f, 0.02f) + hips, new V3(1f, 0f, 0f), fe);
                        break;
                    case Fidget.CheckPhone:
                        BlendArmIk(1, new V3(0.06f, shoulderY - 0.2f, 0.25f) + hips, new V3(1f, -0.5f, -0.3f), fe);
                        break;
                    case Fidget.AdjustTopi:
                    {
                        float tug = 0.015f * MathF.Sin(ft * 12f);
                        V3 top = new V3(0f, _sk.Metrics.HeadBaseY + 0.42f + tug, 0.02f) + hips;
                        BlendArmIk(0, top + new V3(-0.15f, 0f, 0f), new V3(-1f, -0.2f, 0f), fe);
                        BlendArmIk(1, top + new V3(0.15f, 0f, 0f), new V3(1f, -0.2f, 0f), fe);
                        break;
                    }
                    case Fidget.Yawn:
                        BlendArmIk(1, new V3(0.05f, _sk.Metrics.HeadBaseY + 0.1f, 0.22f) + hips, new V3(1f, -0.4f, 0f), fe);
                        break;
                    case Fidget.FlickHair:
                        BlendArmIk(1, new V3(0.18f, _sk.Metrics.HeadBaseY + 0.25f, -0.05f) + hips, new V3(1f, 0f, 0f), fe);
                        break;
                }
            }
        }

        // ----- Seated -----------------------------------------------------------------------------------------------

        /// <summary>
        /// The seated pose: the root is the seat point (the controller puts it on the vehicle's seat socket), the hips
        /// sit just above it, feet and hands go to the class's targets with IK.
        /// </summary>
        private void PoseSeated(in PoseInput input, float dt)
        {
            V3 hipsBind = _sk[Bone.Hips];
            HipsOffset = new V3(0f, 0.05f - hipsBind.Y, -0.03f);
            float lean = 0f, counter = 0f;
            float steer = CharMath.Clamp(CharMath.Finite(input.Steer), -1f, 1f);
            SeatPose seat = input.Seat;
            V3 footL, footR, handL, handR;
            V3 kneePole = new V3(0f, 0.3f, 1f), elbowPoleL = new V3(-1f, -0.6f, -0.3f), elbowPoleR = new V3(1f, -0.6f, -0.3f);
            float toePitch = 0f;
            switch (seat)
            {
                case SeatPose.Bicycle:
                {
                    lean = BicycleLeanDeg;
                    // Pedals on a cartoon crank (0.14 m) below and ahead of the saddle, opposite each other.
                    float c = input.CrankRad;
                    var centre = new V3(0f, -0.36f, 0.14f);
                    footL = centre + new V3(-0.1f, 0.14f * MathF.Cos(c), 0.14f * MathF.Sin(c));
                    footR = centre + new V3(0.1f, -0.14f * MathF.Cos(c), -0.14f * MathF.Sin(c));
                    Bars(0.26f, 0.26f, 0.32f, steer * 20f, out handL, out handR);
                    break;
                }
                case SeatPose.Scooter:
                    lean = -5f;
                    footL = new V3(-0.11f, -0.34f, 0.26f);
                    footR = new V3(0.11f, -0.34f, 0.26f);
                    Bars(0.27f, 0.36f, 0.26f, steer * 18f, out handL, out handR);
                    break;
                case SeatPose.Cruiser:
                    lean = 5f;
                    footL = new V3(-0.2f, -0.33f, 0.3f);
                    footR = new V3(0.2f, -0.33f, 0.3f);
                    Bars(0.32f, 0.34f, 0.28f, steer * 15f, out handL, out handR);
                    counter = 0.2f;
                    break;
                case SeatPose.Motorbike:
                    lean = MotorbikeLeanDeg;
                    footL = new V3(-0.17f, -0.36f, 0.06f);
                    footR = new V3(0.17f, -0.36f, 0.06f);
                    Bars(0.29f, 0.3f, 0.3f, steer * 15f, out handL, out handR);
                    counter = 0.2f;
                    break;
                case SeatPose.Pillion:
                    lean = 8f;
                    footL = new V3(-0.17f, -0.36f, -0.04f);
                    footR = new V3(0.17f, -0.36f, -0.04f);
                    handL = new V3(-0.15f, 0.2f, 0.24f);
                    handR = new V3(0.15f, 0.2f, 0.24f);
                    break;
                case SeatPose.CarDriver:
                case SeatPose.BusDriver:
                case SeatPose.TruckDriver:
                case SeatPose.TractorDriver:
                {
                    bool heavy = seat == SeatPose.BusDriver || seat == SeatPose.TruckDriver;
                    float radius = heavy ? 0.25f : seat == SeatPose.TractorDriver ? 0.2f : 0.19f;
                    float ratio = heavy ? 20f : 15f;
                    float tilt = heavy ? 60f : 25f; // degrees from vertical (a bus wheel lies flatter)
                    lean = seat == SeatPose.TractorDriver ? 4f : -6f;
                    // Wheel angle = steer angle × ratio; the hands follow it up to ±100° (hand-over-hand beyond).
                    float wheel = CharMath.Clamp(steer * 35f * ratio, -100f, 100f);
                    var centre = new V3(0f, heavy ? 0.3f : 0.34f, heavy ? 0.3f : 0.27f);
                    handL = WheelPoint(centre, radius, tilt, -60f + wheel);
                    handR = WheelPoint(centre, radius, tilt, 60f + wheel);
                    footL = new V3(-0.13f, -0.32f, 0.36f);
                    footR = new V3(0.13f, -0.32f, 0.36f);
                    toePitch = 15f;
                    break;
                }
                default:
                    // Passengers: hands on the knees, feet on the floor.
                    lean = seat == SeatPose.Rickshaw ? -10f : -4f;
                    footL = new V3(-0.12f, -0.32f, 0.34f);
                    footR = new V3(0.12f, -0.32f, 0.34f);
                    handL = new V3(-0.13f, 0.07f, 0.25f);
                    handR = new V3(0.13f, 0.07f, 0.25f);
                    elbowPoleL = new V3(-1f, 0f, -0.5f);
                    elbowPoleR = new V3(1f, 0f, -0.5f);
                    break;
            }
            if (input.FootDown && (seat == SeatPose.Motorbike || seat == SeatPose.Scooter || seat == SeatPose.Cruiser || seat == SeatPose.Bicycle))
            {
                // Stopped: the left foot goes down to the ground.
                footL = new V3(-0.3f, -0.76f + (seat == SeatPose.Bicycle ? -0.1f : 0f), 0.1f);
            }
            // Passenger sway, rider counter-lean (racing style on motorbikes).
            float swayDeg = _sway.Update(dt, CharMath.Clamp(3f * CharMath.Finite(input.LateralMps2), -12f, 12f));
            float roll = seat == SeatPose.CarPassenger || seat == SeatPose.BusSeated || seat == SeatPose.Rickshaw ? swayDeg : -counter * swayDeg;
            _local[(int)Bone.Hips] = Quat.Euler(lean * 0.3f, 0f, 0f);
            _local[(int)Bone.Spine] = Quat.Euler(lean * 0.4f, 0f, roll * 0.5f);
            _local[(int)Bone.Chest] = Quat.Euler(lean * 0.3f, 0f, roll * 0.5f);
            _local[(int)Bone.Head] = Quat.Euler(-lean * 0.8f, 0f, -roll * 0.6f);
            // Targets are relative to the seat point (the root).
            LegIk(0, footL, kneePole, toePitch);
            LegIk(1, footR, kneePole, toePitch);
            ArmIk(0, handL, elbowPoleL);
            ArmIk(1, handR, elbowPoleR);
        }

        /// <summary>Handlebar grips at half width <paramref name="half"/>, rotated about the steering axis.</summary>
        private static void Bars(float half, float y, float z, float steerDeg, out V3 left, out V3 right)
        {
            Quat q = Quat.Euler(0f, steerDeg, 0f);
            var pivot = new V3(0f, y, z - 0.05f);
            left = pivot + q * new V3(-half, 0f, 0.05f);
            right = pivot + q * new V3(half, 0f, 0.05f);
        }

        /// <summary>A point on a steering wheel rim: <paramref name="angleDeg"/> from the top, the wheel tilted back by
        /// <paramref name="tiltDeg"/> from vertical.</summary>
        private static V3 WheelPoint(V3 centre, float radius, float tiltDeg, float angleDeg)
        {
            float a = angleDeg * Quat.Deg2Rad;
            var p = new V3(radius * MathF.Sin(a), radius * MathF.Cos(a), 0f);
            return centre + Quat.Euler(-tiltDeg, 0f, 0f) * p;
        }

        // ----- IK ----------------------------------------------------------------------------------------------------

        /// <summary>Model-space position and rotation of a bone from the current locals (no squash).</summary>
        public void ModelOf(Bone bone, out V3 position, out Quat rotation)
        {
            // Walk from the root down to the bone (depth ≤ 8).
            int b = (int)bone;
            int depth = 0;
            Span<int> chain = stackalloc int[12];
            while (b >= 0 && depth < chain.Length)
            {
                chain[depth++] = b;
                b = HumanoidSkeleton.Parent[b];
            }
            V3 p = V3.Zero;
            Quat q = Quat.Identity;
            for (int i = depth - 1; i >= 0; i--)
            {
                int c = chain[i];
                V3 off = _sk.BindLocal[c];
                if (c == (int)Bone.Hips) off = off + HipsOffset;
                p = p + q * off;
                q = q * _local[c];
            }
            position = p;
            rotation = q;
        }

        /// <summary>Points leg <paramref name="side"/> (0 left, 1 right) at an ankle target in the root frame with the
        /// knee towards <paramref name="pole"/>; the foot is flat with <paramref name="toePitchDeg"/> (+ toe up).</summary>
        private void LegIk(int side, V3 ankle, V3 pole, float toePitchDeg)
        {
            int o = side * 4;
            Bone thigh = (Bone)(23 + o), shin = (Bone)(24 + o), foot = (Bone)(25 + o);
            V3 hip;
            Quat hipsWorld;
            ModelOf(Bone.Hips, out _, out hipsWorld);
            ModelOf(thigh, out hip, out _);
            V3 knee, end;
            TwoBoneIk.Solve(hip, _thigh, _shin, ankle, pole, out knee, out end);
            V3 thighBind = _sk[shin] - _sk[thigh], shinBind = _sk[foot] - _sk[shin];
            Quat thighLocal = TwoBoneIk.Aim(hipsWorld, thighBind, knee - hip);
            _local[(int)thigh] = thighLocal;
            Quat thighWorld = hipsWorld * thighLocal;
            Quat shinLocal = TwoBoneIk.Aim(thighWorld, shinBind, end - knee);
            _local[(int)shin] = shinLocal;
            Quat shinWorld = thighWorld * shinLocal;
            Quat footWorld = Quat.Euler(-toePitchDeg, 0f, 0f);
            _local[(int)foot] = (shinWorld.Inverse * footWorld).Normalized;
        }

        /// <summary>Points arm <paramref name="side"/> at a wrist target in the root frame, elbow towards
        /// <paramref name="pole"/>. Returns the reach error.</summary>
        private float ArmIk(int side, V3 wrist, V3 pole)
        {
            int o = side * 4;
            Bone upper = (Bone)(8 + o), fore = (Bone)(9 + o), hand = (Bone)(10 + o), shoulder = (Bone)(7 + o);
            V3 sh;
            Quat parentWorld;
            ModelOf(shoulder, out _, out parentWorld);
            ModelOf(upper, out sh, out _);
            V3 elbow, end;
            float err = TwoBoneIk.Solve(sh, _upperArm, _forearm, wrist, pole, out elbow, out end);
            V3 upperBind = _sk[fore] - _sk[upper], foreBind = _sk[hand] - _sk[fore];
            Quat upperLocal = TwoBoneIk.Aim(parentWorld, upperBind, elbow - sh);
            _local[(int)upper] = upperLocal;
            Quat upperWorld = parentWorld * upperLocal;
            _local[(int)fore] = TwoBoneIk.Aim(upperWorld, foreBind, end - elbow);
            return err;
        }

        private void BlendArmIk(int side, V3 wrist, V3 pole, float weight)
        {
            if (weight <= 0f) return;
            int o = side * 4;
            Quat u0 = _local[8 + o], f0 = _local[9 + o];
            ArmIk(side, wrist, pole);
            if (weight >= 1f) return;
            _local[8 + o] = Quat.Slerp(u0, _local[8 + o], weight);
            _local[9 + o] = Quat.Slerp(f0, _local[9 + o], weight);
        }

        // ----- Secondary motion and squash --------------------------------------------------------------------------

        private void Secondary(in PoseInput input, float dt)
        {
            float rm = input.ReducedMotion ? 0f : 1f;
            // The topi (and helmet) tilt follows the head's vertical and forward motion, ±12°.
            float vy = CharMath.Finite(input.VerticalMps);
            float accelY = dt > 0f ? (vy - _prevVy) / dt : 0f;
            _prevVy = vy;
            float bobKick = _gait.PelvisBobM * 60f;
            float topiPitch = CharMath.Clamp(_topi.Update(dt, CharMath.Clamp(-0.6f * accelY - bobKick, -12f, 12f)), -12f, 12f) * rm;
            float topiRoll = CharMath.Clamp(_topiSide.Update(dt, 0.5f * _gait.PelvisRollDeg), -12f, 12f) * rm;
            _local[(int)Bone.HeadAttach] = Quat.Euler(topiPitch, 0f, topiRoll);
            float pack = CharMath.Clamp(_pack.Update(dt, 6f * _gait.Amount * (1f + _gait.Run) - 0.4f * accelY), -10f, 10f) * rm;
            _local[(int)Bone.BackAttach] = Quat.Euler(-pack, 0f, 0f);
            float hem = CharMath.Clamp(_hem.Update(dt, 20f * _gait.Amount * MathF.Sin(_gait.Phase * 6.28318530718f) + 6f * _gait.Run), -35f, 35f) * rm;
            _local[(int)Bone.SkirtF] = Quat.Euler(-Math.Abs(hem) * 0.6f, 0f, 0f);
            _local[(int)Bone.SkirtB] = Quat.Euler(Math.Abs(hem) * 0.4f + 4f * _gait.Run, 0f, 0f);
            // Head look-at (60% head, the neck takes a little; ±70° yaw, ±30° pitch).
            float yaw = _headYaw.Update(dt, CharMath.Clamp(CharMath.Finite(input.LookYawDeg), -70f, 70f));
            float pitch = _headPitch.Update(dt, CharMath.Clamp(CharMath.Finite(input.LookPitchDeg), -30f, 30f));
            if (Math.Abs(yaw) > 0.01f || Math.Abs(pitch) > 0.01f)
            {
                _local[(int)Bone.Head] = Quat.Euler(0.6f * pitch, 0.6f * yaw, 0f) * _local[(int)Bone.Head];
                _local[(int)Bone.Neck] = Quat.Euler(0.4f * pitch, 0.4f * yaw, 0f) * _local[(int)Bone.Neck];
            }
        }

        private void SquashUpdate(in PoseInput input, float dt)
        {
            if (input.ReducedMotion)
            {
                _squash.Reset(1f);
                Squash = 1f;
                return;
            }
            if (LeftStrike || RightStrike) _squash.Kick(_gait.Run > 0.5f ? -0.9f : -0.4f);
            float s = _squash.Update(dt, 1f);
            Squash = CharMath.Clamp(s, SquashMin, SquashMax);
        }

        /// <summary>0 → 1 → 0 over <paramref name="duration"/> with <paramref name="ease"/>-second ramps.</summary>
        public static float Envelope(float t, float duration, float ease)
        {
            if (!(t >= 0f) || t >= duration || duration <= 0f) return 0f;
            float e = Math.Min(ease, duration * 0.5f);
            float a = t < e ? t / e : t > duration - e ? (duration - t) / e : 1f;
            return CharMath.SmoothStep(a);
        }
    }
}
