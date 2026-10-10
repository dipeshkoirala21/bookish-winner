using System;
using Ghumante.Core.Traffic;

namespace Ghumante.Core.Characters
{
    /// <summary>What a crowd person holds in the right hand (the pose keeps the hand level so the prop stands up).</summary>
    public enum CrowdHold : byte
    {
        None = 0,

        /// <summary>An open umbrella (rain, May sun).</summary>
        Umbrella = 1,

        /// <summary>A hand prayer wheel (kora walkers).</summary>
        PrayerWheel = 2,
    }

    /// <summary>
    /// The joint angles of a crowd person for one frame (degrees; metres for the offsets), on the shared <c>hum</c> rig:
    /// forward swing of the thighs and arms, knee and elbow bends, arms raised sideways, the spine's lean, the head's
    /// nod and turn, the walking bob and the seat drop, the foot that struck the ground this frame and the hand prop.
    /// <see cref="CrowdPoser"/> turns it into bone rotations for the skinned near crowd and the baked far frames.
    /// </summary>
    public struct CrowdPose
    {
        public float ThighL, ThighR, KneeL, KneeR;
        public float ArmL, ArmR, ArmOutL, ArmOutR, ElbowL, ElbowR;
        public float Lean, HeadPitch, HeadYaw;
        public float BobM, DropM;

        /// <summary>0 none, 1 left, 2 right (footstep sounds).</summary>
        public byte Strike;

        public CrowdHold Hold;
    }

    /// <summary>
    /// Procedural animation of the crowd (W2_DESIGN 5.4; the VAT clips of ASSET_MANIFEST 9.5 as joint curves): a walk and
    /// run cycle whose stride follows the speed (step = 0.42 leg + 0.18 v, as the player's gait) with knees bending in
    /// the swing and arms swinging opposite, a porter's slower loaded walk with the hands on the straps, idle sway,
    /// sitting on plinths and chautari, chatting with gestures, praying with a bow, namaste with the palms together at
    /// the sternum (W2_DESIGN 6.1: bow 15° + spine 8°), the vendor's call, picking, kite flying, holding an umbrella or a
    /// prayer wheel, and the traffic officer's five hand signals. Pure functions of (clip, time, speed, seed):
    /// deterministic, engine-free, allocation-free.
    /// </summary>
    public static class CrowdAnimation
    {
        /// <summary>Leg length of the stride formula (the adult hip height).</summary>
        public const float LegLengthM = 0.66f;

        /// <summary>Steps per second at a speed (two steps per cycle).</summary>
        public static float StepRate(float speedMps)
        {
            float v = Math.Max(0f, speedMps);
            float step = 0.42f * LegLengthM + 0.18f * v;
            return v < 0.05f ? 0f : v / Math.Max(0.25f, step);
        }

        /// <summary>The walk cycle phase (radians, one cycle per two steps) at <paramref name="timeS"/>.</summary>
        public static double WalkPhase(float timeS, float speedMps, bool run)
        {
            float rate = StepRate(speedMps < 0.2f ? (run ? 3f : 1.3f) : speedMps);
            return timeS * rate * Math.PI;
        }

        /// <summary>The pose of <paramref name="clip"/> at <paramref name="timeS"/>; <paramref name="prevTimeS"/> lets the
        /// walk report the foot that struck since then.</summary>
        public static CrowdPose Pose(PedClip clip, float timeS, float prevTimeS, float speedMps, int seed, CrowdHold hold = CrowdHold.None)
        {
            var p = new CrowdPose { Hold = hold };
            float sway = (float)Math.Sin(timeS * 1.3f + seed * 0.7f);
            float look = (float)Math.Sin(timeS * 0.37f + seed * 1.9f);
            switch (clip)
            {
                case PedClip.Walk:
                case PedClip.Run:
                case PedClip.Carry:
                {
                    bool run = clip == PedClip.Run || speedMps > 2.2f;
                    bool carry = clip == PedClip.Carry;
                    double phase = WalkPhase(timeS, speedMps, run);
                    float amp = run ? 40f : carry ? 20f : 26f;
                    float s = (float)Math.Sin(phase), c = (float)Math.Cos(phase);
                    p.ThighL = amp * s;
                    p.ThighR = -amp * s;
                    // The knee bends while the leg swings through, and a little at the heel strike.
                    float kneeAmp = run ? 70f : carry ? 32f : 36f;
                    p.KneeL = 6f + kneeAmp * Math.Max(0f, c) * Math.Max(0f, c);
                    p.KneeR = 6f + kneeAmp * Math.Max(0f, -c) * Math.Max(0f, -c);
                    float arms = carry ? 0f : run ? 0.9f : 0.75f;
                    p.ArmL = -amp * arms * s;
                    p.ArmR = amp * arms * s;
                    p.ElbowL = p.ElbowR = run ? 80f : 14f;
                    p.ArmOutL = p.ArmOutR = run ? 8f : 5f;
                    if (carry)
                    {
                        // Hands up on the shoulder straps or the namlo.
                        p.ArmL = p.ArmR = 28f;
                        p.ElbowL = p.ElbowR = 105f;
                        p.ArmOutL = p.ArmOutR = 10f;
                    }
                    p.BobM = (float)Math.Abs(c) * (run ? 0.05f : 0.022f);
                    p.Lean = run ? 9f : carry ? 14f : 3f;
                    p.HeadPitch = carry ? -8f : 0f;
                    p.HeadYaw = 8f * look;
                    // A foot strikes each time the swing crosses zero.
                    double prevPhase = WalkPhase(prevTimeS, speedMps, run);
                    long a = (long)Math.Floor(prevPhase / Math.PI), b = (long)Math.Floor(phase / Math.PI);
                    if (b != a && timeS > prevTimeS) p.Strike = (byte)((b & 1) == 0 ? 1 : 2);
                    break;
                }
                case PedClip.Sit:
                    p.ThighL = 85f;
                    p.ThighR = 80f;
                    p.KneeL = 86f;
                    p.KneeR = 82f;
                    p.ArmL = 30f;
                    p.ArmR = 34f;
                    p.ElbowL = p.ElbowR = 40f;
                    p.DropM = 0.24f;
                    p.Lean = 4f + sway;
                    p.HeadYaw = 20f * look;
                    break;
                case PedClip.Chat:
                    p.ArmL = 6f + 4f * sway;
                    p.ArmR = 30f + 18f * (float)Math.Sin(timeS * 3.1f + seed);
                    p.ElbowR = 70f + 15f * (float)Math.Sin(timeS * 2.3f + seed);
                    p.ElbowL = 10f;
                    p.Lean = 2f;
                    p.HeadPitch = 4f * (float)Math.Sin(timeS * 2f + seed);
                    p.HeadYaw = 15f * look;
                    break;
                case PedClip.Pray:
                {
                    float bow = (float)Math.Max(0, Math.Sin(timeS * 0.8f));
                    p.ArmL = p.ArmR = 48f;
                    p.ArmOutL = p.ArmOutR = -16f;
                    p.ElbowL = p.ElbowR = 98f;
                    p.Lean = 10f + 14f * bow;
                    p.HeadPitch = 12f + 10f * bow;
                    break;
                }
                case PedClip.Namaste:
                {
                    // Palms at the sternum, head bow 15° + spine 8° (W2_DESIGN 6.1).
                    float bow = (float)Math.Max(0, Math.Sin(timeS * 1.5f));
                    p.ArmL = p.ArmR = 50f;
                    p.ArmOutL = p.ArmOutR = -18f;
                    p.ElbowL = p.ElbowR = 100f;
                    p.Lean = 8f * bow;
                    p.HeadPitch = 15f * bow;
                    break;
                }
                case PedClip.Vendor:
                case PedClip.Cheer:
                    p.ArmR = 70f + 45f * (float)Math.Max(0, Math.Sin(timeS * 2.2f + seed));
                    p.ElbowR = 30f;
                    p.ArmL = 10f;
                    p.ElbowL = 20f;
                    p.HeadYaw = 12f * look;
                    break;
                case PedClip.Pick:
                    p.Lean = 38f;
                    p.ThighL = p.ThighR = 12f;
                    p.KneeL = p.KneeR = 24f;
                    p.ArmL = 55f;
                    p.ArmR = 60f;
                    p.ElbowL = p.ElbowR = 20f;
                    p.HeadPitch = 10f;
                    break;
                case PedClip.Kite:
                    p.ArmL = 130f;
                    p.ArmR = 120f;
                    p.ElbowL = p.ElbowR = 25f;
                    p.HeadPitch = -30f;
                    break;
                default:
                    p.ArmL = 3f * sway;
                    p.ArmR = -3f * sway;
                    p.ElbowL = p.ElbowR = 10f;
                    p.ArmOutL = p.ArmOutR = 4f;
                    p.Lean = sway;
                    p.HeadYaw = 25f * look;
                    p.ThighL = 2f;
                    p.ThighR = -2f;
                    p.KneeL = p.KneeR = 3f;
                    break;
            }
            if (hold != CrowdHold.None)
            {
                // The right hand in front of the chest; the prop stands up from it.
                p.ArmR = hold == CrowdHold.Umbrella ? 38f : 32f;
                p.ArmOutR = hold == CrowdHold.Umbrella ? -6f : 4f;
                p.ElbowR = hold == CrowdHold.Umbrella ? 92f : 80f;
                if (hold == CrowdHold.PrayerWheel) p.ElbowR += 6f * (float)Math.Sin(timeS * 6f + seed);
            }
            return p;
        }

        /// <summary>The five traffic-officer hand signals (W2_DESIGN 5.1 POLICE controller): 0 stop front (right arm up),
        /// 1 stop behind (left arm out back), 2 go (right arm waving across), 3 slow (left arm patting down), 4 right turn
        /// allowed (right arm out sideways).</summary>
        public static CrowdPose Officer(int signal, float timeS)
        {
            var p = new CrowdPose();
            float wave = (float)Math.Sin(timeS * 4.0);
            p.ElbowL = p.ElbowR = 8f;
            switch (((signal % 5) + 5) % 5)
            {
                case 0:
                    p.ArmR = 165f;
                    p.ElbowR = 5f;
                    break;
                case 1:
                    p.ArmOutL = 85f;
                    p.ArmL = -20f;
                    break;
                case 2:
                    p.ArmR = 80f;
                    p.ArmOutR = 30f + 35f * wave;
                    p.ElbowR = 20f + 30f * Math.Max(0f, wave);
                    break;
                case 3:
                    p.ArmOutL = 45f;
                    p.ArmL = 20f + 15f * wave;
                    p.ElbowL = 30f;
                    break;
                default:
                    p.ArmOutR = 88f;
                    break;
            }
            p.HeadYaw = 10f * (float)Math.Sin(timeS * 0.5f);
            return p;
        }
    }

    /// <summary>
    /// Turns a <see cref="CrowdPose"/> into local bone rotations of the <c>hum</c> rig (relative to the bind pose, arms
    /// hanging) and a hips offset: thighs and knees, upper arms with abduction and elbows, the spine's lean shared by
    /// hips, spine and chest, the head's nod and turn, the hem bones following the legs; with a hand prop the right hand
    /// stays level in the world, so an umbrella or prayer wheel stands upright. Writes into the caller's array.
    /// </summary>
    public static class CrowdPoser
    {
        public static void Solve(in CrowdPose p, HumanoidSkeleton sk, Quat[] local, out V3 hipsOffset)
        {
            if (local == null || local.Length < HumanoidSkeleton.BoneCount) throw new ArgumentException("local needs 37 entries", nameof(local));
            for (int i = 0; i < HumanoidSkeleton.BoneCount; i++) local[i] = Quat.Identity;
            // Positive X turns down-pointing limbs backward (and the spine and head forward).
            local[(int)Bone.Hips] = Quat.Euler(p.Lean * 0.25f, 0f, 0f);
            local[(int)Bone.Spine] = Quat.Euler(p.Lean * 0.45f, p.HeadYaw * 0.1f, 0f);
            local[(int)Bone.Chest] = Quat.Euler(p.Lean * 0.3f, p.HeadYaw * 0.1f, 0f);
            local[(int)Bone.Neck] = Quat.Euler(p.HeadPitch * 0.35f, p.HeadYaw * 0.3f, 0f);
            local[(int)Bone.Head] = Quat.Euler(p.HeadPitch * 0.65f - p.Lean * 0.4f, p.HeadYaw * 0.5f, 0f);
            // The legs counter the hips' share of the lean so the feet stay under the body.
            float hipLean = p.Lean * 0.25f;
            local[(int)Bone.ThighL] = Quat.Euler(-p.ThighL - hipLean, 0f, 0f);
            local[(int)Bone.ThighR] = Quat.Euler(-p.ThighR - hipLean, 0f, 0f);
            local[(int)Bone.ShinL] = Quat.Euler(p.KneeL, 0f, 0f);
            local[(int)Bone.ShinR] = Quat.Euler(p.KneeR, 0f, 0f);
            // Feet stay roughly level: undo the thigh and shin pitch.
            local[(int)Bone.FootL] = Quat.Euler(p.ThighL + hipLean - p.KneeL * 0.8f, 0f, 0f);
            local[(int)Bone.FootR] = Quat.Euler(p.ThighR + hipLean - p.KneeR * 0.8f, 0f, 0f);
            local[(int)Bone.UpperArmL] = Quat.Euler(-p.ArmL, 0f, -p.ArmOutL);
            local[(int)Bone.UpperArmR] = Quat.Euler(-p.ArmR, 0f, p.ArmOutR);
            local[(int)Bone.ForearmL] = Quat.Euler(-p.ElbowL, 0f, 0f);
            local[(int)Bone.ForearmR] = Quat.Euler(-p.ElbowR, 0f, 0f);
            // Relaxed fingers curl a little.
            local[(int)Bone.FingerL1] = local[(int)Bone.FingerR1] = Quat.Euler(0f, 0f, 0f);
            float hem = Math.Max(p.ThighL, p.ThighR), back = Math.Min(p.ThighL, p.ThighR);
            local[(int)Bone.SkirtF] = Quat.Euler(-Math.Max(0f, hem) * 0.55f, 0f, 0f);
            local[(int)Bone.SkirtB] = Quat.Euler(-Math.Min(0f, back) * 0.45f, 0f, 0f);
            if (p.Hold != CrowdHold.None)
            {
                // Keep the right hand level in the world: its local rotation undoes the chain above it.
                Quat chain = local[(int)Bone.Root] * local[(int)Bone.Squash] * local[(int)Bone.Hips] * local[(int)Bone.Spine] * local[(int)Bone.Chest] *
                             local[(int)Bone.ShoulderR] * local[(int)Bone.UpperArmR] * local[(int)Bone.ForearmR];
                local[(int)Bone.HandR] = chain.Inverse;
            }
            hipsOffset = new V3(0f, p.BobM - p.DropM, 0f);
        }
    }
}
