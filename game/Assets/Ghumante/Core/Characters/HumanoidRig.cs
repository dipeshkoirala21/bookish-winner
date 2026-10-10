using System;

namespace Ghumante.Core.Characters
{
    /// <summary>
    /// The 37 bones of the shared humanoid rig <c>hum</c> (ASSET_MANIFEST 9.1), in a fixed order that the mesher, the
    /// poser and the Unity skinned renderer share: root and squash, the spine to the head, shoulders to hands, thumbs and
    /// mitten fingers, thighs to toes, the prop and attach points and the two hem bones. Append-only.
    /// </summary>
    public enum Bone : byte
    {
        Root = 0,
        Squash = 1,
        Hips = 2,
        Spine = 3,
        Chest = 4,
        Neck = 5,
        Head = 6,
        ShoulderL = 7,
        UpperArmL = 8,
        ForearmL = 9,
        HandL = 10,
        ShoulderR = 11,
        UpperArmR = 12,
        ForearmR = 13,
        HandR = 14,
        ThumbL1 = 15,
        ThumbL2 = 16,
        FingerL1 = 17,
        FingerL2 = 18,
        ThumbR1 = 19,
        ThumbR2 = 20,
        FingerR1 = 21,
        FingerR2 = 22,
        ThighL = 23,
        ShinL = 24,
        FootL = 25,
        ToeL = 26,
        ThighR = 27,
        ShinR = 28,
        FootR = 29,
        ToeR = 30,
        PropR = 31,
        PropL = 32,
        BackAttach = 33,
        HeadAttach = 34,
        SkirtF = 35,
        SkirtB = 36,
    }

    /// <summary>
    /// Body measurements of a build (P §2.3; metres, model space: origin on the ground between the feet, +Y up, +Z
    /// forward). The head is the same size on every build (0.40 m tall, 0.36 wide, 0.38 deep); builds change the height,
    /// shoulders, hips, belly and legs.
    /// </summary>
    public struct BodyMetrics
    {
        public BodyBuild Build;
        public float HeightM;
        public float HeadH, HeadW, HeadD;
        public float ShoulderW, HipW, ChestDepth, Belly;
        public float AnkleY, ShinM, ThighM, LegM;
        public float TorsoM, NeckM;
        public float UpperArmM, ForearmM, HandM, HandW;
        public float FootL, FootW, FootH;

        /// <summary>Body outline (<see cref="CharacterRecipe.Figure"/>): 0 straight, 1 soft.</summary>
        public byte Figure;

        /// <summary>Height of the hip joints above the ground.</summary>
        public float HipJointY
        {
            get { return AnkleY + ShinM + ThighM; }
        }

        /// <summary>Height of the shoulder line.</summary>
        public float ShoulderY
        {
            get { return HipJointY + TorsoM; }
        }

        /// <summary>Height of the base of the skull (the head bone).</summary>
        public float HeadBaseY
        {
            get { return ShoulderY + NeckM; }
        }

        /// <summary>Head size relative to the adult head (0.40 m): face features scale with it.</summary>
        public float HeadScale
        {
            get { return HeadH / 0.40f; }
        }

        /// <summary>Hand size relative to the adult hand (0.13 m).</summary>
        public float HandScale
        {
            get { return HandM / 0.13f; }
        }

        /// <summary>The measurements of <paramref name="build"/> (P §2.3 builds table) for an adult.</summary>
        public static BodyMetrics For(BodyBuild build)
        {
            return For(build, AgeGroup.Adult, 0);
        }

        /// <summary>The measurements of a recipe: its build, age group and height nudge.</summary>
        public static BodyMetrics For(CharacterRecipe r)
        {
            return r == null ? For(BodyBuild.B) : For(r.Build, r.Age, r.HeightPct, r.Figure);
        }

        /// <summary>
        /// The measurements of <paramref name="build"/> at an age group with a height nudge of
        /// <paramref name="heightPct"/> percent (−6..+6). Adults follow P §2.3; a child is about 1.22 m and 3.4 heads
        /// tall (head 0.36 m), a teen 1.45 m, an elder 1.5% shorter with a little belly. The head size is fixed per age
        /// group; the nudge goes to the legs and torso.
        /// </summary>
        public static BodyMetrics For(BodyBuild build, AgeGroup age, int heightPct)
        {
            return For(build, age, heightPct, 0);
        }

        /// <summary>As <see cref="For(BodyBuild, AgeGroup, int)"/> with a figure: the soft figure has 6% narrower
        /// shoulders and 6% fuller hips (the chest curve is the mesher's).</summary>
        public static BodyMetrics For(BodyBuild build, AgeGroup age, int heightPct, int figure)
        {
            float height, shoulder, hip, belly, leg;
            switch (build)
            {
                case BodyBuild.A:
                    height = 1.52f;
                    shoulder = 0.92f;
                    hip = 0.95f;
                    belly = 0f;
                    leg = 1.00f;
                    break;
                case BodyBuild.C:
                    height = 1.56f;
                    shoulder = 1.12f;
                    hip = 1.12f;
                    belly = 0.04f;
                    leg = 0.97f;
                    break;
                case BodyBuild.D:
                    height = 1.66f;
                    shoulder = 1.0f;
                    hip = 1.0f;
                    belly = 0.01f;
                    leg = 1.10f;
                    break;
                default:
                    height = 1.55f;
                    shoulder = 1.0f;
                    hip = 1.0f;
                    belly = 0.01f;
                    leg = 1.0f;
                    break;
            }
            float headH = 0.40f, headW = 0.36f, headD = 0.38f, neck = 0.04f, ankle = 0.075f, hand = 0.13f, foot = 0.27f;
            switch (age)
            {
                case AgeGroup.Child:
                    height = 1.22f * (height / 1.55f);
                    headH = 0.36f;
                    headW = 0.33f;
                    headD = 0.345f;
                    neck = 0.035f;
                    ankle = 0.06f;
                    shoulder *= 0.78f;
                    hip *= 0.80f;
                    belly = 0.01f;
                    leg *= 0.78f;
                    hand = 0.105f;
                    foot = 0.215f;
                    break;
                case AgeGroup.Teen:
                    height = 1.45f * (height / 1.55f);
                    headH = 0.39f;
                    headW = 0.35f;
                    headD = 0.37f;
                    shoulder *= 0.92f;
                    hip *= 0.93f;
                    leg *= 0.95f;
                    hand = 0.123f;
                    foot = 0.25f;
                    break;
                case AgeGroup.Elder:
                    height *= 0.985f;
                    shoulder *= 0.98f;
                    belly += 0.012f;
                    break;
            }
            if (figure != 0)
            {
                shoulder *= 0.94f;
                hip *= 1.06f;
            }
            int pct = heightPct < -6 ? -6 : heightPct > 6 ? 6 : heightPct;
            float nudge = 1f + pct / 100f;
            height *= nudge;
            leg *= nudge;
            var m = new BodyMetrics
            {
                Build = build,
                Figure = (byte)(figure != 0 ? 1 : 0),
                HeightM = height,
                HeadH = headH,
                HeadW = headW,
                HeadD = headD,
                ShoulderW = 0.42f * shoulder,
                HipW = 0.34f * hip,
                ChestDepth = 0.24f * (0.5f + 0.5f * hip) * (age == AgeGroup.Child ? 0.82f : 1f),
                Belly = belly,
                AnkleY = ankle,
                ShinM = 0.27f * leg,
                ThighM = 0.30f * leg,
                NeckM = neck,
                FootL = foot,
                FootW = 0.11f * foot / 0.27f,
                FootH = 0.09f * foot / 0.27f,
                HandM = hand,
                HandW = 0.09f * hand / 0.13f,
            };
            m.LegM = m.AnkleY + m.ShinM + m.ThighM;
            m.TorsoM = height - m.HeadH - m.NeckM - m.LegM;
            float arm = 0.58f * (height / 1.55f) * (age == AgeGroup.Child ? 1.04f : 1f);
            m.UpperArmM = 0.40f * (arm - m.HandM);
            m.ForearmM = arm - m.HandM - m.UpperArmM;
            return m;
        }
    }

    /// <summary>
    /// The bind pose of the <c>hum</c> rig for a build: every bone's parent and model-space position with identity bind
    /// rotations (arms hang down, slightly out). Local rotations from the poser are relative to this pose, and the Unity
    /// bind poses are the inverse translations. Engine-free, allocation only in the constructor.
    /// </summary>
    public sealed class HumanoidSkeleton
    {
        public const int BoneCount = 37;

        /// <summary>Distance from the wrist down to the finger knuckles and to the middle finger joints on an adult hand
        /// (scaled by <see cref="BodyMetrics.HandScale"/>).</summary>
        public const float HandKnuckleM = 0.074f, HandMidJointM = 0.102f;

        /// <summary>Parent of each bone (−1 for the root).</summary>
        public static readonly sbyte[] Parent =
        {
            -1, 0, 1, 2, 3, 4, 5, // root, squash, hips, spine, chest, neck, head
            4, 7, 8, 9, // left arm
            4, 11, 12, 13, // right arm
            10, 15, 10, 17, // left thumb and fingers
            14, 19, 14, 21, // right thumb and fingers
            2, 23, 24, 25, // left leg
            2, 27, 28, 29, // right leg
            14, 10, 4, 6, // prop_R, prop_L, back_attach, head_attach
            2, 2, // skirt_F, skirt_B
        };

        /// <summary>Unity transform names (the rig's bone names).</summary>
        public static readonly string[] Names =
        {
            "root", "squash", "hips", "spine", "chest", "neck", "head",
            "shoulder_L", "upperarm_L", "forearm_L", "hand_L",
            "shoulder_R", "upperarm_R", "forearm_R", "hand_R",
            "thumb1_L", "thumb2_L", "finger1_L", "finger2_L",
            "thumb1_R", "thumb2_R", "finger1_R", "finger2_R",
            "thigh_L", "shin_L", "foot_L", "toe_L",
            "thigh_R", "shin_R", "foot_R", "toe_R",
            "prop_R", "prop_L", "back_attach", "head_attach",
            "skirt_F", "skirt_B",
        };

        public readonly BodyMetrics Metrics;

        /// <summary>Model-space bind position per bone.</summary>
        public readonly V3[] BindPosition = new V3[BoneCount];

        /// <summary>Bind position relative to the parent (the Unity local position at bind).</summary>
        public readonly V3[] BindLocal = new V3[BoneCount];

        public HumanoidSkeleton(BodyBuild build) : this(BodyMetrics.For(build))
        {
        }

        /// <summary>The skeleton of a recipe's build, age group and height.</summary>
        public HumanoidSkeleton(CharacterRecipe recipe) : this(BodyMetrics.For(recipe))
        {
        }

        public HumanoidSkeleton(BodyMetrics m)
        {
            Metrics = m;
            float hipY = m.HipJointY, shoulderY = m.ShoulderY;
            float pelvisY = hipY + 0.02f;
            float hx = 0.25f * m.HipW;
            float sx = 0.5f * m.ShoulderW - 0.04f;
            float armX = sx + 0.015f;
            Set(Bone.Root, 0f, 0f, 0f);
            Set(Bone.Squash, 0f, 0f, 0f);
            Set(Bone.Hips, 0f, pelvisY, 0f);
            Set(Bone.Spine, 0f, pelvisY + 0.32f * m.TorsoM, 0f);
            Set(Bone.Chest, 0f, pelvisY + 0.62f * m.TorsoM, 0f);
            Set(Bone.Neck, 0f, shoulderY, -0.01f);
            Set(Bone.Head, 0f, m.HeadBaseY, 0f);
            for (int side = 0; side < 2; side++)
            {
                float s = side == 0 ? -1f : 1f;
                int o = side * 4;
                Set((Bone)(7 + o), s * 0.06f, shoulderY - 0.03f, -0.01f);
                Set((Bone)(8 + o), s * sx, shoulderY - 0.04f, -0.01f);
                float elbowY = shoulderY - 0.04f - m.UpperArmM;
                Set((Bone)(9 + o), s * (armX + 0.01f), elbowY, -0.015f);
                float wristY = elbowY - m.ForearmM;
                Set((Bone)(10 + o), s * (armX + 0.02f), wristY, 0f);
                int f = side * 4;
                // Thumb base and joint, and the knuckle and middle joints of the four fingers (they bend together),
                // where HumanoidMesher's hand puts them (wrist-relative, scaled with the hand).
                float hs = m.HandScale;
                float wx = s * (armX + 0.02f);
                Set((Bone)(15 + f), wx - s * 0.008f * hs, wristY - 0.024f * hs, 0.026f * hs);
                Set((Bone)(16 + f), wx - s * 0.012f * hs, wristY - 0.05f * hs, 0.044f * hs);
                Set((Bone)(17 + f), wx, wristY - HandKnuckleM * hs, 0.004f * hs);
                Set((Bone)(18 + f), wx - s * 0.004f * hs, wristY - HandMidJointM * hs, 0.004f * hs);
                int l = side * 4;
                Set((Bone)(23 + l), s * hx, hipY, 0f);
                Set((Bone)(24 + l), s * hx, m.AnkleY + m.ShinM, 0.005f);
                Set((Bone)(25 + l), s * hx, m.AnkleY, 0f);
                Set((Bone)(26 + l), s * hx, 0.03f, 0.13f);
            }
            float handY = BindPosition[(int)Bone.HandR].Y - 0.07f;
            Set(Bone.PropR, BindPosition[(int)Bone.HandR].X, handY, 0.02f);
            Set(Bone.PropL, BindPosition[(int)Bone.HandL].X, handY, 0.02f);
            Set(Bone.BackAttach, 0f, pelvisY + 0.62f * m.TorsoM, -0.5f * m.ChestDepth);
            Set(Bone.HeadAttach, 0f, m.HeadBaseY + 0.86f * m.HeadH, 0f);
            Set(Bone.SkirtF, 0f, pelvisY - 0.02f, 0.06f);
            Set(Bone.SkirtB, 0f, pelvisY - 0.02f, -0.06f);
            for (int i = 0; i < BoneCount; i++)
            {
                int p = Parent[i];
                BindLocal[i] = p < 0 ? BindPosition[i] : BindPosition[i] - BindPosition[p];
            }
        }

        private void Set(Bone b, float x, float y, float z)
        {
            BindPosition[(int)b] = new V3(x, y, z);
        }

        public V3 this[Bone b]
        {
            get { return BindPosition[(int)b]; }
        }

        /// <summary>True when <paramref name="bone"/> is <paramref name="ancestor"/> or one of its descendants.</summary>
        public static bool IsUnder(Bone bone, Bone ancestor)
        {
            int b = (int)bone;
            while (b >= 0)
            {
                if (b == (int)ancestor) return true;
                b = Parent[b];
            }
            return false;
        }

        /// <summary>
        /// Forward kinematics: model-space positions and rotations of every bone for the given local rotations (relative
        /// to bind) and a hips offset. <paramref name="squash"/> scales the squash bone volume-preservingly (1/√s, s,
        /// 1/√s). Writes into the caller's arrays (no allocation).
        /// </summary>
        public void Solve(Quat[] local, V3 hipsOffset, float squash, V3[] worldPos, Quat[] worldRot)
        {
            if (local == null || worldPos == null || worldRot == null) throw new ArgumentNullException(nameof(local));
            float s = squash > 0f ? squash : 1f;
            float side = 1f / MathF.Sqrt(s);
            for (int i = 0; i < BoneCount; i++)
            {
                int p = Parent[i];
                V3 offset = BindLocal[i];
                if (i == (int)Bone.Hips) offset = offset + hipsOffset;
                if (p < 0)
                {
                    worldPos[i] = offset;
                    worldRot[i] = local[i];
                    continue;
                }
                V3 o = worldRot[p] * offset;
                if (i > (int)Bone.Squash) o = new V3(o.X * side, o.Y * s, o.Z * side); // the squash bone scales everything under it
                worldPos[i] = worldPos[p] + o;
                worldRot[i] = worldRot[p] * local[i];
            }
        }
    }
}
