using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Traffic;

namespace Ghumante.Core.Characters
{
    /// <summary>
    /// Which character mesh level each drawn person gets (W2_DESIGN 10.4: characters slice 21 k / 50 k / 95 k triangles
    /// on Low / Mid / High; people caps 1 / 4 / 13, 3 / 10 / 39, 6 / 20 / 78 in the near ≤ 15 m, mid ≤ 40 m and far ≤ 90 m
    /// bands). Levels (<see cref="HumanoidMesher.Budget(int)"/>): LOD0 12 k (the light LOD0 of the player on Low 7 k),
    /// LOD1 3 k, LOD2 0.72 k (the mid-distance skinned body: eye whites, a nine-sided head, eight-sided arms, thumbs, soles) and the far level
    /// 0.5 k (<see cref="HumanoidMesher.FarLod"/>, baked poses of the person's own body shape, <see cref="CrowdBaker"/>).
    /// Every tier draws the near band at LOD1 or better, so the person brushing past is never the far body: High gives the
    /// nearest person LOD0. The mid band is LOD2 with the nearest at LOD1 on Mid and High. The player keeps the full
    /// LOD0 on Mid and High and the light LOD0 (five-finger hands, the full face and topi) on Low. Worst cases at the caps:
    /// Low 19.4 k, Mid 50.0 k, High 94.7 k; <see cref="WorstCaseTriangles"/> checks a tier against its slice.
    /// </summary>
    public static class CrowdLodPlan
    {
        /// <summary>Character triangle slices per tier (W2_DESIGN 10.4).</summary>
        public static readonly int[] SliceTris = { 21000, 50000, 95000 };

        /// <summary>People drawn per band (near, mid, far) per tier (W2_DESIGN 10.4, LifeLod).</summary>
        public static readonly int[][] Caps = { new[] { 1, 4, 13 }, new[] { 3, 10, 39 }, new[] { 6, 20, 78 } };

        /// <summary>Per tier: how many of the near band are LOD0 (the rest LOD1) and how many of the mid band are LOD1
        /// (the rest LOD2).</summary>
        private static readonly int[] NearLod0 = { 0, 0, 1 };
        private static readonly int[] MidLod1 = { 0, 1, 1 };

        /// <summary>The player's mesh level on a tier (always LOD0; see <see cref="PlayerLight"/>).</summary>
        public static int PlayerLod(int tier)
        {
            return 0;
        }

        /// <summary>True when the player wears the light LOD0 (<see cref="CharacterMeshOptions.Light"/>): Low devices.</summary>
        public static bool PlayerLight(int tier)
        {
            return Tier(tier) == 0;
        }

        /// <summary>The player's triangle cap on a tier.</summary>
        public static int PlayerBudget(int tier)
        {
            return PlayerLight(tier) ? HumanoidMesher.Lod0LightBudget : HumanoidMesher.Budget(0);
        }

        private static int Tier(int tier)
        {
            return tier <= 0 ? 0 : tier >= 2 ? 2 : 1;
        }

        /// <summary>The mesh level of the person ranked <paramref name="rank"/> (0 = nearest) in band
        /// <paramref name="band"/> (0 near, 1 mid, 2 far); the far band is always the baked
        /// <see cref="HumanoidMesher.FarLod"/>.</summary>
        public static int MeshLod(int tier, int band, int rank)
        {
            int t = Tier(tier);
            if (band <= 0) return rank < NearLod0[t] ? 0 : 1;
            if (band == 1) return rank < MidLod1[t] ? 1 : 2;
            return HumanoidMesher.FarLod;
        }

        /// <summary>True when the band draws skinned bodies (near and mid); the far band draws baked poses.</summary>
        public static bool Skinned(int band)
        {
            return band <= 1;
        }

        /// <summary>The most triangles the characters of a tier can draw with full bands, given the worst triangle counts
        /// of each mesh level (LOD0, LOD1, LOD2, far) and the player's.</summary>
        public static int WorstCaseTriangles(int tier, int lod0, int lod1, int lod2, int far, int player)
        {
            int t = Tier(tier);
            int[] caps = Caps[t];
            int[] perLod = { lod0, lod1, lod2, far };
            int sum = player;
            for (int band = 0; band < 3; band++)
                for (int rank = 0; rank < caps[band]; rank++) sum += perLod[MeshLod(t, band, rank)];
            return sum;
        }

        /// <summary>The worst case of a tier at the level caps (<see cref="HumanoidMesher.Budget(int)"/>).</summary>
        public static int WorstCaseAtCaps(int tier)
        {
            return WorstCaseTriangles(tier, HumanoidMesher.Budget(0), HumanoidMesher.Budget(1), HumanoidMesher.Budget(2),
                                      HumanoidMesher.Budget(HumanoidMesher.FarLod), PlayerBudget(tier));
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
    /// The crowd's looks. A person is a body shape (<see cref="Variants"/> per archetype, place and carry prop, each with
    /// its own age, figure, height, skin, hair, garments and accessories) in one of <see cref="Colours"/> garment colours
    /// (the sim's per-agent tint, <c>PedPose.Tint</c>): the main garment (and a matching suruwal, skirt or pallu) takes
    /// <see cref="GarmentColour"/>, everything else keeps the shape's colours. Every band draws the same tint-masked
    /// build of the shape (<see cref="CharacterMeshOptions.TintMask"/>): the skinned near and mid bodies are recoloured on
    /// the CPU (<see cref="Recolour"/>), the far band instances the shape's baked frames with the same colour as an
    /// instance tint, which the shader applies with the same formula. So nobody changes clothes, age, skin or colour when
    /// they cross a band edge, and the far crowd still shares one batch per shape and frame. Also: the far frame a pose
    /// shows and the hand prop a body holds. Engine-free.
    /// </summary>
    public static class CrowdVariants
    {
        /// <summary>Body shapes per archetype, place and carry prop; garment colours per shape (the sim's tint, 0..15); baked
        /// far frames per shape.</summary>
        public const int Variants = 12, Colours = 16, FrameCount = 10;

        /// <summary>The body shape an agent draws (from its id).</summary>
        public static int VariantOf(int agentId)
        {
            return (int)(CharMath.Hash((uint)agentId, 0xB0D1u) % Variants);
        }

        /// <summary>The look key of an agent: its archetype, the place it walks in, its carry prop (6 and up count as none),
        /// its body shape (<see cref="VariantOf"/>) and its garment colour (the sim's tint).</summary>
        public static int KeyOf(PedArchetype archetype, StreetStyle style, int carryProp, int agentId, int tint)
        {
            int carry = carryProp >= 0 && carryProp <= 5 ? carryProp : 0;
            return Key(archetype, style, carry, VariantOf(agentId), tint);
        }

        /// <summary>A stable key of a look (archetype, place, carry prop, body shape, garment colour).</summary>
        public static int Key(PedArchetype archetype, StreetStyle style, int carry, int variant, int colour = 0)
        {
            return ((int)archetype & 0xFF) | ((int)style & 0xFF) << 8 | (carry & 0xF) << 16 | (variant & 0xF) << 20 | (colour & 0xF) << 24;
        }

        /// <summary>Splits a <see cref="Key"/> (without its colour, see <see cref="ColourOf"/>).</summary>
        public static void Split(int key, out PedArchetype archetype, out StreetStyle style, out int carry, out int variant)
        {
            archetype = (PedArchetype)(key & 0xFF);
            style = (StreetStyle)((key >> 8) & 0xFF);
            carry = (key >> 16) & 0xF;
            variant = (key >> 20) & 0xF;
        }

        /// <summary>The garment colour number (0..15) of a look; 0 is the shape's own colour.</summary>
        public static int ColourOf(int key)
        {
            return (key >> 24) & 0xF;
        }

        /// <summary>The body shape of a look (its key with colour 0): every band draws this shape's tint-masked build, so
        /// all the looks of a shape share its skinned builds and its baked far frames.</summary>
        public static int ShapeKey(int key)
        {
            return key & 0x00FFFFFF;
        }

        /// <summary>The recipe of a body shape (the colour of a look does not change it).</summary>
        public static CharacterRecipe Recipe(int key)
        {
            Split(key, out PedArchetype a, out StreetStyle style, out int carry, out int variant);
            uint seed = CharMath.Hash((uint)a + 1u, (uint)style * 31u + (uint)carry, (uint)variant * 0x9E37u + 7u);
            return CharacterRecipe.ForPedestrian(a, variant * 37 + (int)(seed & 0x1F), seed, style, carry);
        }

        /// <summary>The build options of a crowd body: the recipe's headwear, and the tint mask when the shape has a garment
        /// colour to vary (<see cref="HumanoidMesher.TintKeyOf"/>).</summary>
        public static CharacterMeshOptions Options(CharacterRecipe shape)
        {
            CharacterMeshOptions o = CharacterMeshOptions.For(HeadwearMode.Outfit);
            o.TintMask = HumanoidMesher.TintKeyOf(shape) != 0;
            return o;
        }

        /// <summary>
        /// The 0xRRGGBB main-garment colour of look colour <paramref name="colour"/> on <paramref name="shape"/>: 0 keeps
        /// the shape's own colour; the others step through the garment's own palette (a kurta stays in kurta colours,
        /// a daura in daura cloth), skipping near-black cloth whose folds could not be told apart. 0 when the shape has no
        /// colour to vary (uniforms, monks' robes, the black haku patasi, dark garments; see
        /// <see cref="HumanoidMesher.TintKeyOf"/>).
        /// </summary>
        public static uint GarmentColour(CharacterRecipe shape, int colour)
        {
            uint key = HumanoidMesher.TintKeyOf(shape);
            if (key == 0) return 0;
            OutfitSlot t = shape[OutfitSlotKind.Torso];
            uint[] p = CharacterPalette.Of(t.Item);
            int c = colour & 0xF;
            if (c == 0 || p == null || p.Length < 2) return key;
            for (int k = 0; k < p.Length; k++)
            {
                uint rgb = p[(t.Colour + c + k) % p.Length];
                if (HumanoidMesher.Tintable(rgb)) return rgb;
            }
            return key;
        }

        /// <summary>The smallest look colour number showing the same garment colour as <paramref name="colour"/> on
        /// <paramref name="shape"/> (a short palette repeats; an untintable shape has one look, 0), so identical looks
        /// share one skinned mesh.</summary>
        public static int CanonicalColour(CharacterRecipe shape, int colour)
        {
            colour &= 0xF;
            uint rgb = GarmentColour(shape, colour);
            if (rgb == 0) return 0;
            for (int c = 0; c < colour; c++)
                if (GarmentColour(shape, c) == rgb) return c;
            return colour;
        }

        /// <summary>
        /// Writes the colours of a tint-masked build (<see cref="CharacterMeshOptions.TintMask"/>) recoloured with the
        /// garment colour <paramref name="rgb"/> into <paramref name="dst"/> (RGBA bytes, one per vertex of
        /// <paramref name="src"/>): masked vertices (alpha 255, grey = the shade of the garment) become
        /// shade × <paramref name="rgb"/>, multiplied in linear light like the instanced shader's tint, so a recoloured
        /// near body and a tinted far frame show the same colours; every other vertex keeps its colour. Alpha is kept.
        /// <paramref name="rgb"/> = 0 copies the colours unchanged. No allocation.
        /// </summary>
        public static void Recolour(MeshData src, uint rgb, byte[] dst)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null || dst.Length < src.VertexCount * 4) throw new ArgumentException("one RGBA per vertex", nameof(dst));
            byte[] c = src.Colors;
            int n = src.VertexCount * 4;
            if (rgb == 0)
            {
                Array.Copy(c, dst, n);
                return;
            }
            float tr = ToLinear[(rgb >> 16) & 0xFF], tg = ToLinear[(rgb >> 8) & 0xFF], tb = ToLinear[rgb & 0xFF];
            for (int i = 0; i < n; i += 4)
            {
                if (c[i + 3] != 255)
                {
                    dst[i] = c[i];
                    dst[i + 1] = c[i + 1];
                    dst[i + 2] = c[i + 2];
                    dst[i + 3] = c[i + 3];
                    continue;
                }
                dst[i] = TintChannel(c[i], tr);
                dst[i + 1] = TintChannel(c[i + 1], tg);
                dst[i + 2] = TintChannel(c[i + 2], tb);
                dst[i + 3] = 255;
            }
        }

        /// <summary>One channel of the instanced tint: srgb(linear(albedo) · linear(tint)), as ToonLit's _INSTANCE_TINT
        /// (table lookups, no allocation).</summary>
        public static byte TintChannel(byte albedo, float tintLinear)
        {
            float lin = ToLinear[albedo] * tintLinear;
            int i = (int)(lin * (ToSrgb.Length - 1) + 0.5f);
            return ToSrgb[i < 0 ? 0 : i >= ToSrgb.Length ? ToSrgb.Length - 1 : i];
        }

        /// <summary>The sRGB transfer curve as tables: byte → linear, and linear (4,096 steps) → byte.</summary>
        private static readonly float[] ToLinear = BuildToLinear();
        private static readonly byte[] ToSrgb = BuildToSrgb();

        private static float[] BuildToLinear()
        {
            var t = new float[256];
            for (int i = 0; i < 256; i++) t[i] = SrgbToLinear(i / 255f);
            return t;
        }

        private static byte[] BuildToSrgb()
        {
            var t = new byte[4097];
            for (int i = 0; i < t.Length; i++) t[i] = (byte)(LinearToSrgb(i / 4096f) * 255f + 0.5f);
            return t;
        }

        private static float SrgbToLinear(float c)
        {
            return c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
        }

        private static float LinearToSrgb(float c)
        {
            c = c < 0f ? 0f : c > 1f ? 1f : c;
            return c <= 0.0031308f ? c * 12.92f : 1.055f * MathF.Pow(c, 1f / 2.4f) - 0.055f;
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
