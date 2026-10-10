using Ghumante.Core.Characters;
using Ghumante.Core.Traffic;

namespace Ghumante.World.Instancing
{
    /// <summary>
    /// The joint angles of a crowd person for one frame: the full <see cref="CrowdPose"/> of the skinned and baked crowd
    /// (<see cref="Body"/>) plus the coarse limb angles of the earlier rigid-part people (degrees; forward swing positive),
    /// kept for callers that only need them.
    /// </summary>
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

        /// <summary>The whole-body pose (knees, elbows, head, hand prop) on the <c>hum</c> rig.</summary>
        public CrowdPose Body;

        /// <summary>The coarse view of a whole-body pose.</summary>
        public static PersonPose From(in CrowdPose b)
        {
            return new PersonPose
            {
                LegLeft = b.ThighL, LegRight = b.ThighR, ArmLeft = b.ArmL, ArmRight = b.ArmR, ArmOutLeft = b.ArmOutL, ArmOutRight = b.ArmOutR,
                BobM = b.BobM, DropM = b.DropM, LeanDeg = b.Lean, Strike = b.Strike, Body = b,
            };
        }
    }

    /// <summary>
    /// Procedural animation of the crowd (W2_DESIGN 5.4): Core's <see cref="CrowdAnimation"/> (walk and run cycles with
    /// knee and elbow bends, the porter's loaded walk, idle, sitting on plinths and chautari, chatting, praying, namaste,
    /// vendor calls, picking, kite flying, umbrella and prayer-wheel holds, the traffic officer's five signals) wrapped
    /// for the world presenters. Pure functions: deterministic, engine-free, allocation-free.
    /// </summary>
    public static class PersonAnimation
    {
        public const float LegLengthM = CrowdAnimation.LegLengthM;

        /// <summary>Steps per second at a speed (two steps per cycle).</summary>
        public static float StepRate(float speedMps)
        {
            return CrowdAnimation.StepRate(speedMps);
        }

        /// <summary>The pose of <paramref name="clip"/> at <paramref name="timeS"/>; <paramref name="prevTimeS"/> lets
        /// the walk report the foot that struck since then.</summary>
        public static PersonPose Pose(PedClip clip, float timeS, float prevTimeS, float speedMps, int seed)
        {
            return PersonPose.From(CrowdAnimation.Pose(clip, timeS, prevTimeS, speedMps, seed));
        }

        /// <summary>As <see cref="Pose(PedClip, float, float, float, int)"/> holding a hand prop.</summary>
        public static PersonPose Pose(PedClip clip, float timeS, float prevTimeS, float speedMps, int seed, CrowdHold hold)
        {
            return PersonPose.From(CrowdAnimation.Pose(clip, timeS, prevTimeS, speedMps, seed, hold));
        }

        /// <summary>The five traffic-officer hand signals (see <see cref="CrowdAnimation.Officer"/>).</summary>
        public static PersonPose Officer(int signal, float timeS)
        {
            return PersonPose.From(CrowdAnimation.Officer(signal, timeS));
        }
    }
}
