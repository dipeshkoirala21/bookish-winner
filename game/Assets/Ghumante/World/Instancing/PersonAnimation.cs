using System;
using Ghumante.Core.Traffic;

namespace Ghumante.World.Instancing
{
    /// <summary>The joint angles of a rigid-part person for one frame (degrees; forward swing positive).</summary>
    public struct PersonPose
    {
        public float LegLeft, LegRight, ArmLeft, ArmRight;

        /// <summary>Arms raised sideways (degrees outwards), for the traffic officer's signals.</summary>
        public float ArmOutLeft, ArmOutRight;

        /// <summary>Vertical bob of the body (metres) and the seat drop when sitting.</summary>
        public float BobM, DropM;

        /// <summary>Forward lean of the body (degrees), e.g. bowing in prayer.</summary>
        public float LeanDeg;

        /// <summary>The foot that struck the ground this frame: 0 none, 1 left, 2 right (footstep sounds).</summary>
        public byte Strike;
    }

    /// <summary>
    /// Procedural animation of the rigid-part crowd (W2_DESIGN 5.4; the VAT clips of ASSET_MANIFEST 9.5 as joint
    /// curves): a walk and run cycle whose stride follows the speed (step = 0.42 leg + 0.18 v, as the player's gait),
    /// idle sway, sitting on plinths and chautari, chatting, praying with a bow, namaste with the palms together, the
    /// vendor's call and the traffic officer's five hand signals. Pure functions of (clip, time, speed): deterministic,
    /// engine-free, allocation-free.
    /// </summary>
    public static class PersonAnimation
    {
        public const float LegLengthM = 0.82f;

        /// <summary>Steps per second at a speed (two steps per cycle).</summary>
        public static float StepRate(float speedMps)
        {
            float v = Math.Max(0f, speedMps);
            float step = 0.42f * LegLengthM + 0.18f * v;
            return v < 0.05f ? 0f : v / Math.Max(0.3f, step);
        }

        /// <summary>The pose of <paramref name="clip"/> at <paramref name="timeS"/>; <paramref name="prevTimeS"/> lets
        /// the walk report the foot that struck since then.</summary>
        public static PersonPose Pose(PedClip clip, float timeS, float prevTimeS, float speedMps, int seed)
        {
            var p = new PersonPose();
            float sway = (float)Math.Sin(timeS * 1.3f + seed * 0.7f);
            switch (clip)
            {
                case PedClip.Walk:
                case PedClip.Run:
                case PedClip.Carry:
                {
                    bool run = clip == PedClip.Run || speedMps > 2.2f;
                    float rate = StepRate(speedMps < 0.2f ? (run ? 3f : 1.3f) : speedMps);
                    double phase = timeS * rate * 0.5 * 2.0 * Math.PI; // one cycle = two steps
                    float amp = run ? 42f : 28f;
                    float s = (float)Math.Sin(phase);
                    p.LegLeft = amp * s;
                    p.LegRight = -amp * s;
                    float arms = clip == PedClip.Carry ? 0.25f : 0.8f;
                    p.ArmLeft = -amp * arms * s;
                    p.ArmRight = amp * arms * s;
                    p.BobM = (float)Math.Abs(Math.Cos(phase)) * (run ? 0.06f : 0.03f);
                    p.LeanDeg = run ? 8f : clip == PedClip.Carry ? 10f : 2f;
                    // A foot strikes each time the swing crosses zero going one way or the other.
                    double prevPhase = prevTimeS * rate * 0.5 * 2.0 * Math.PI;
                    long a = (long)Math.Floor(prevPhase / Math.PI), b = (long)Math.Floor(phase / Math.PI);
                    if (b != a && timeS > prevTimeS) p.Strike = (byte)((b & 1) == 0 ? 1 : 2);
                    break;
                }
                case PedClip.Sit:
                    p.LegLeft = 85f;
                    p.LegRight = 80f;
                    p.ArmLeft = 25f;
                    p.ArmRight = 30f;
                    p.DropM = LegLengthM - 0.45f;
                    p.LeanDeg = -4f + sway;
                    break;
                case PedClip.Chat:
                    p.ArmLeft = 10f + 6f * sway;
                    p.ArmRight = 35f + 20f * (float)Math.Sin(timeS * 3.1f + seed);
                    p.LeanDeg = 2f;
                    break;
                case PedClip.Pray:
                    p.ArmLeft = 70f;
                    p.ArmRight = 70f;
                    p.LeanDeg = 15f + 8f * (float)Math.Max(0, Math.Sin(timeS * 0.8f));
                    break;
                case PedClip.Namaste:
                    // Palms at the sternum, head bow 15° + spine 8° (W2_DESIGN 6.1).
                    p.ArmLeft = 65f;
                    p.ArmRight = 65f;
                    p.ArmOutLeft = -18f;
                    p.ArmOutRight = -18f;
                    p.LeanDeg = 8f + 15f * (float)Math.Max(0, Math.Sin(timeS * 1.5f));
                    break;
                case PedClip.Vendor:
                case PedClip.Cheer:
                    p.ArmRight = 60f + 50f * (float)Math.Max(0, Math.Sin(timeS * 2.2f + seed));
                    p.ArmLeft = 10f;
                    break;
                case PedClip.Pick:
                    p.LeanDeg = 35f;
                    p.ArmLeft = 50f;
                    p.ArmRight = 55f;
                    break;
                case PedClip.Kite:
                    p.ArmLeft = 120f;
                    p.ArmRight = 110f;
                    break;
                default:
                    p.ArmLeft = 3f * sway;
                    p.ArmRight = -3f * sway;
                    p.LeanDeg = sway;
                    break;
            }
            return p;
        }

        /// <summary>The five traffic-officer hand signals (W2_DESIGN 5.1 POLICE controller): 0 stop front (right arm up
        /// forward), 1 stop behind (left arm out back), 2 go (right arm waving across), 3 slow (left arm patting down),
        /// 4 right turn allowed (right arm out sideways). Officers change signal every phase.</summary>
        public static PersonPose Officer(int signal, float timeS)
        {
            var p = new PersonPose();
            float wave = (float)Math.Sin(timeS * 4.0);
            switch (((signal % 5) + 5) % 5)
            {
                case 0:
                    p.ArmRight = 165f;
                    break;
                case 1:
                    p.ArmOutLeft = 90f;
                    p.ArmLeft = -20f;
                    break;
                case 2:
                    p.ArmRight = 90f;
                    p.ArmOutRight = 40f + 40f * wave;
                    break;
                case 3:
                    p.ArmOutLeft = 45f;
                    p.ArmLeft = 20f + 15f * wave;
                    break;
                default:
                    p.ArmOutRight = 90f;
                    break;
            }
            return p;
        }
    }
}
