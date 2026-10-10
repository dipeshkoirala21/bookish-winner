using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Traffic;

namespace Ghumante.Core.Characters
{
    /// <summary>
    /// Which character mesh level each drawn person gets (W2_DESIGN 10.4: characters slice 21 k / 50 k / 95 k triangles
    /// on Low / Mid / High; people caps 1 / 4 / 13, 3 / 10 / 39, 6 / 20 / 78 in the near ≤ 15 m, mid ≤ 40 m and far ≤ 90 m
    /// bands). The player keeps LOD0 (≤ 12 k) on every tier. High: the nearest person is skinned LOD0, the rest of the
    /// near band and the two nearest of the mid band LOD1, the rest skinned LOD2. Mid: the near band and the nearest of
    /// the mid band LOD1, the rest LOD2. Low: everyone else LOD2. The far band always draws baked LOD2 poses
    /// (<see cref="CrowdBaker"/>), instanced with a per-person tint. Worst cases at the caps (12 k / 3 k / 0.5 k):
    /// Low 21.0 k, Mid 48.0 k, High 93.0 k; <see cref="WorstCaseTriangles"/> checks a tier against its slice.
    /// </summary>
    public static class CrowdLodPlan
    {
        /// <summary>Character triangle slices per tier (W2_DESIGN 10.4).</summary>
        public static readonly int[] SliceTris = { 21000, 50000, 95000 };

        /// <summary>People drawn per band (near, mid, far) per tier (W2_DESIGN 10.4, LifeLod).</summary>
        public static readonly int[][] Caps = { new[] { 1, 4, 13 }, new[] { 3, 10, 39 }, new[] { 6, 20, 78 } };

        /// <summary>Per tier: how many of the near band are LOD0 (the rest LOD1, or LOD2 on Low) and how many of the mid
        /// band are LOD1 (the rest LOD2).</summary>
        private static readonly int[] NearLod0 = { 0, 0, 1 };
        private static readonly int[] NearLod = { 2, 1, 1 };
        private static readonly int[] MidLod1 = { 0, 1, 2 };

        /// <summary>The player's mesh level on a tier.</summary>
        public static int PlayerLod(int tier)
        {
            return 0;
        }

        private static int Tier(int tier)
        {
            return tier <= 0 ? 0 : tier >= 2 ? 2 : 1;
        }

        /// <summary>The mesh level of the person ranked <paramref name="rank"/> (0 = nearest) in band
        /// <paramref name="band"/> (0 near, 1 mid, 2 far); the far band is always the baked LOD2.</summary>
        public static int MeshLod(int tier, int band, int rank)
        {
            int t = Tier(tier);
            if (band <= 0) return rank < NearLod0[t] ? 0 : NearLod[t];
            if (band == 1) return rank < MidLod1[t] ? 1 : 2;
            return 2;
        }

        /// <summary>True when the band draws skinned bodies (near and mid); the far band draws baked poses.</summary>
        public static bool Skinned(int band)
        {
            return band <= 1;
        }

        /// <summary>The most triangles the characters of a tier can draw with full bands, given the worst triangle counts
        /// of each mesh level and the player's.</summary>
        public static int WorstCaseTriangles(int tier, int lod0, int lod1, int lod2, int player)
        {
            int t = Tier(tier);
            int[] caps = Caps[t];
            int[] perLod = { lod0, lod1, lod2 };
            int sum = player;
            for (int band = 0; band < 3; band++)
                for (int rank = 0; rank < caps[band]; rank++) sum += perLod[MeshLod(t, band, rank)];
            return sum;
        }
    }

    /// <summary>The baked far-crowd poses: six walk phases, standing, sitting, palms together and an arm raised.</summary>
    public enum FarFrame : byte
    {
        Walk0 = 0,
        Walk1 = 1,
        Walk2 = 2,
        Walk3 = 3,
        Walk4 = 4,
        Walk5 = 5,
        Stand = 6,
        Sit = 7,
        Pray = 8,
        ArmUp = 9,
    }

    /// <summary>
    /// The crowd's body variants (one recipe per archetype, place, carry prop and variant number, shared by everyone who
    /// draws that variant), the far frame a pose shows, and the tint key of the far body. Engine-free.
    /// </summary>
    public static class CrowdVariants
    {
        /// <summary>Body variants per archetype, place and carry prop: four looks, two far shapes (the far tint keeps
        /// the colours of all four).</summary>
        public const int Variants = 4, FarVariants = 2, FrameCount = 10;

        /// <summary>The variant an agent draws.</summary>
        public static int VariantOf(int agentId)
        {
            return (int)(CharMath.Hash((uint)agentId, 0xB0D1u) % Variants);
        }

        /// <summary>A stable key of a variant body (archetype, place, carry prop, variant).</summary>
        public static int Key(PedArchetype archetype, StreetStyle style, int carry, int variant)
        {
            return ((int)archetype & 0xFF) | ((int)style & 0xFF) << 8 | (carry & 0xF) << 16 | (variant & 0xF) << 20;
        }

        /// <summary>Splits a <see cref="Key"/>.</summary>
        public static void Split(int key, out PedArchetype archetype, out StreetStyle style, out int carry, out int variant)
        {
            archetype = (PedArchetype)(key & 0xFF);
            style = (StreetStyle)((key >> 8) & 0xFF);
            carry = (key >> 16) & 0xF;
            variant = (key >> 20) & 0xF;
        }

        /// <summary>The recipe of a variant body.</summary>
        public static CharacterRecipe Recipe(int key)
        {
            Split(key, out PedArchetype a, out StreetStyle style, out int carry, out int variant);
            uint seed = CharMath.Hash((uint)a + 1u, (uint)style * 31u + (uint)carry, (uint)variant * 0x9E37u + 7u);
            return CharacterRecipe.ForPedestrian(a, variant * 37 + (int)(seed & 0x1F), seed, style, carry);
        }

        /// <summary>The far body that stands in for <paramref name="key"/> (variants share two far shapes).</summary>
        public static int FarKey(int key)
        {
            Split(key, out PedArchetype a, out StreetStyle style, out int carry, out int variant);
            return Key(a, style, carry, variant % FarVariants);
        }

        /// <summary>The hand prop of a carry prop (5 = umbrella) or the recipe's prayer wheel.</summary>
        public static CrowdHold HoldOf(CharacterRecipe r)
        {
            if (r == null) return CrowdHold.None;
            if (r.Has(CharacterAccents.Umbrella)) return CrowdHold.Umbrella;
            if (r.Has(CharacterAccents.PrayerWheel)) return CrowdHold.PrayerWheel;
            return CrowdHold.None;
        }

        /// <summary>The far frame of a clip at a walk phase (radians).</summary>
        public static FarFrame FrameOf(PedClip clip, double walkPhase)
        {
            switch (clip)
            {
                case PedClip.Walk:
                case PedClip.Run:
                case PedClip.Carry:
                {
                    double t = walkPhase / (2.0 * Math.PI);
                    t -= Math.Floor(t);
                    return (FarFrame)Math.Min(5, (int)(t * 6.0));
                }
                case PedClip.Sit: return FarFrame.Sit;
                case PedClip.Pray:
                case PedClip.Namaste: return FarFrame.Pray;
                case PedClip.Vendor:
                case PedClip.Cheer:
                case PedClip.Kite: return FarFrame.ArmUp;
                default: return FarFrame.Stand;
            }
        }

        /// <summary>The pose baked for a far frame.</summary>
        public static CrowdPose FramePose(FarFrame f, CrowdHold hold)
        {
            switch (f)
            {
                case FarFrame.Stand: return CrowdAnimation.Pose(PedClip.Idle, 0f, 0f, 0f, 0, hold);
                case FarFrame.Sit: return CrowdAnimation.Pose(PedClip.Sit, 0f, 0f, 0f, 0, hold);
                case FarFrame.Pray: return CrowdAnimation.Pose(PedClip.Namaste, 0f, 0f, 0f, 0, hold);
                case FarFrame.ArmUp: return CrowdAnimation.Officer(0, 0f);
                default:
                {
                    // Time that puts the walk phase at the frame's centre for a 1.4 m/s walk.
                    float rate = CrowdAnimation.StepRate(1.4f);
                    double phase = ((int)f + 0.5) / 6.0 * 2.0 * Math.PI;
                    float t = (float)(phase / (rate * Math.PI));
                    return CrowdAnimation.Pose(PedClip.Walk, t, t, 1.4f, 0, hold);
                }
            }
        }
    }

    /// <summary>
    /// Bakes a skinned character mesh into a posed static one (linear blend skinning on the CPU with the rig's forward
    /// kinematics): the far crowd's frames and posed previews. Engine-free; scratch arrays are reused per instance.
    /// </summary>
    public sealed class CrowdBaker
    {
        private readonly Quat[] _local = new Quat[HumanoidSkeleton.BoneCount];
        private readonly V3[] _pos = new V3[HumanoidSkeleton.BoneCount];
        private readonly Quat[] _rot = new Quat[HumanoidSkeleton.BoneCount];

        /// <summary>Poses <paramref name="src"/> (skinned by <paramref name="w"/> to <paramref name="sk"/>) with
        /// <paramref name="pose"/> and appends the result to <paramref name="dst"/>.</summary>
        public void Bake(MeshData src, SkinWeights w, HumanoidSkeleton sk, in CrowdPose pose, MeshData dst)
        {
            if (src == null || w == null || sk == null || dst == null) throw new ArgumentNullException(nameof(src));
            if (w.Count != src.VertexCount) throw new ArgumentException("weights out of step with the mesh", nameof(w));
            CrowdPoser.Solve(pose, sk, _local, out V3 hips);
            sk.Solve(_local, hips, 1f, _pos, _rot);
            int baseV = dst.VertexCount;
            bool uv = src.HasUv0;
            if (uv) dst.HasUv0 = true;
            float[] p = src.Positions, n = src.Normals;
            for (int v = 0; v < src.VertexCount; v++)
            {
                var pv = new V3(p[v * 3], p[v * 3 + 1], p[v * 3 + 2]);
                var nv = new V3(n[v * 3], n[v * 3 + 1], n[v * 3 + 2]);
                int b0 = w.Bone0[v], b1 = w.Bone1[v];
                float w0 = w.Weight0[v], w1 = 1f - w0;
                V3 q0 = _pos[b0] + _rot[b0] * (pv - sk.BindPosition[b0]);
                V3 m0 = _rot[b0] * nv;
                V3 q, m;
                if (w1 > 1e-4f)
                {
                    V3 q1 = _pos[b1] + _rot[b1] * (pv - sk.BindPosition[b1]);
                    V3 m1 = _rot[b1] * nv;
                    q = q0 * w0 + q1 * w1;
                    m = (m0 * w0 + m1 * w1).Normalized;
                }
                else
                {
                    q = q0;
                    m = m0;
                }
                uint rgba = (uint)(src.Colors[v * 4] << 24 | src.Colors[v * 4 + 1] << 16 | src.Colors[v * 4 + 2] << 8 | src.Colors[v * 4 + 3]);
                if (uv) dst.AddVertex(q.X, q.Y, q.Z, m.X, m.Y, m.Z, rgba, src.Uv0[v * 2], src.Uv0[v * 2 + 1]);
                else dst.AddVertex(q.X, q.Y, q.Z, m.X, m.Y, m.Z, rgba);
            }
            for (int t = 0; t < src.IndexCount; t += 3)
                dst.AddTriangle(src.Indices[t] + baseV, src.Indices[t + 1] + baseV, src.Indices[t + 2] + baseV);
        }
    }
}
