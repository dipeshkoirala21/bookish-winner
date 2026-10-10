using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Characters
{
    /// <summary>What the character wears on the head for this mesh.</summary>
    public enum HeadwearMode : byte
    {
        /// <summary>The recipe's head item (topi, cap, scarf, sun hat or nothing).</summary>
        Outfit = 0,

        /// <summary>The open-face helmet in the recipe's helmet colour (auto-equipped on every two-wheeler; P §2.7).
        /// Hair is squashed under it and ponytails, braids and buns stay out.</summary>
        Helmet = 1,

        /// <summary>Bare head.</summary>
        None = 2,
    }

    /// <summary>How a character mesh is built besides its recipe and level of detail.</summary>
    public struct CharacterMeshOptions
    {
        public HeadwearMode Headwear;

        /// <summary>The face to sculpt (expression, blink, gaze). Every face state has the same topology.</summary>
        public FaceState Face;

        /// <summary>Far crowd body: the main garment colour becomes an instance-tint mask (<see cref="BodyKit.TintMask"/>).</summary>
        public bool TintMask;

        /// <summary>Skip the ambient-occlusion bake (blend-shape targets reuse the base mesh's AO).</summary>
        public bool SkipAo;

        public static CharacterMeshOptions For(HeadwearMode headwear)
        {
            return new CharacterMeshOptions { Headwear = headwear, Face = FaceState.Default };
        }
    }

    /// <summary>
    /// Builds a detailed cartoon person from a <see cref="CharacterRecipe"/> (W2_DESIGN 6.1 and 10.3, P §2.3–2.8,
    /// docs/research/w2/ref_characters.md): a sculpted head (jaw, cheeks, brow, chin) with a cartoon face (eye whites,
    /// irises with pupils and two highlights, upper and lower lids with a lash line, eyebrows, a nose with nostril wings,
    /// a mouth whose shape follows the expression with teeth and tongue, lips, cheek blush, ears with an inner fold);
    /// hair caps with one of 17 styles and facial hair; a neck; a lofted torso with the clothing layers, collars,
    /// plackets, pockets, hems and folds of each garment; tapered limbs with joint bulges and sleeves; hands with a
    /// thumb and four two-joint fingers and nails at LOD0 (a mitten at LOD1, a simple hand at LOD2); feet in sneakers,
    /// chappals, leather shoes, trek boots or bare with toes; the dhaka topi (asymmetric, front 0.20 m and back
    /// 0.14 m on the cartoon head, a pinched crown ridge, tilted 6° toward the left forehead, its Palpali weave as per-quad vertex colours in the
    /// Fabric channel) and the black Bhadgaunle topi; back items (daypack, doko with its namlo, sack, gas cylinder, baby
    /// sling) and accents (glasses, bindi, tika, tilak, pote, nose stud, earrings, shawl, mala, gloves, watch, umbrella,
    /// prayer wheel, mask). Everything is skinned to the 37-bone <c>hum</c> rig (<see cref="HumanoidSkeleton"/>, bind
    /// pose with the arms hanging) and carries UV0 = (material channel, baked AO) (docs/W2_DETAIL_CONTRACT.md §5).
    /// <para>Budgets (<see cref="Budget"/>): LOD0 ≤ 9,600 triangles plus 2,400 for accessories (the player and the
    /// nearest NPC), LOD1 ≤ 3,000, LOD2 ≤ 500 (mid-distance skinned NPCs and the baked far crowd). A recipe that would
    /// go over its cap is rebuilt with its accessories one step plainer (<see cref="DetailDrop"/>), so the caps hold for
    /// any combination; everyday recipes never need it. The same generator
    /// builds the crowd (<see cref="CharacterRecipe.ForPedestrian(Traffic.PedArchetype, int, uint, StreetStyle, int)"/>),
    /// so the player and the people walking by share one style. No brands, no alpha. Deterministic for a recipe;
    /// allocation only through MeshData growth and small scratch arrays.</para>
    /// </summary>
    public static partial class HumanoidMesher
    {
        /// <summary>
        /// Triangle caps (<see cref="Budget"/>): LOD0 body (head, face, hair, neck, torso, limbs with five-fingered hands,
        /// feet) plus accessories (facial hair, headwear, back items, accents); LOD1; LOD2. Sized so that the character
        /// slices of W2_DESIGN 10.4 hold the player at LOD0 with full bands of NPCs (<see cref="CrowdLodPlan"/>).
        /// </summary>
        public const int Lod0Body = 9600, Lod0Accessories = 2400, Lod1Budget = 3000, Lod2Budget = 500;

        /// <summary>The last step of the drop order (<see cref="DetailDrop"/>).</summary>
        public const int MaxDetailDrop = 3;

        /// <summary>Head superellipsoid exponent (slightly boxy-round, P §2.3).</summary>
        public const float HeadExponent = 2.4f;

        /// <summary>
        /// Nepali topi on the adult cartoon head (0.40 m, 1.74× a real head): wall height at the front and the back above
        /// the rim, the rim's height above the head centre (about 4 cm over the brows), the side tilt, the trough of the
        /// crown ridge and the fraction of the wall height where the leaning walls round over into the ridge (ref_characters.md §2: a real topi stands 9–10 cm at
        /// the front over a 23 cm head; W2_DESIGN 6.1's 0.11 / 0.075 m did not clear its own 0.40 m head, so the cap
        /// scales with the head and keeps the 3 : 2 front-to-back ratio and the 6° tilt).
        /// </summary>
        public const float TopiFrontM = 0.2f, TopiBackM = 0.14f, TopiRimY = 0.068f, TopiTiltDeg = 6f, TopiFoldDeg = 8f, TopiPinchStart = 0.84f;

        /// <summary>Daura: tie strings (4 pairs) and pleats (W2_DESIGN 6.1, cultural checklist 10.7 #5).</summary>
        public const int DauraTies = 8, DauraPleats = 5;

        /// <summary>Fingers per hand at LOD0 (a thumb and four fingers).</summary>
        public const int FingersLod0 = 5;

        /// <summary>Triangle cap per level of detail.</summary>
        public static int Budget(int lod)
        {
            return lod <= 0 ? Lod0Body + Lod0Accessories : lod == 1 ? Lod1Budget : Lod2Budget;
        }

        /// <summary>Builds the skinned character into <paramref name="m"/> and <paramref name="w"/> (appending) with the
        /// recipe's own headwear.</summary>
        public static void Build(CharacterRecipe r, int lod, MeshData m, SkinWeights w)
        {
            Build(r, lod, m, w, CharacterMeshOptions.For(HeadwearMode.Outfit));
        }

        /// <summary>Builds the skinned character with the given headwear (the player keeps an Outfit and a Helmet mesh and
        /// swaps them on mount). <paramref name="w"/> may be null for a static mesh.</summary>
        public static void Build(CharacterRecipe r, int lod, MeshData m, SkinWeights w, HeadwearMode headwear)
        {
            Build(r, lod, m, w, CharacterMeshOptions.For(headwear));
        }

        /// <summary>Names of the parts counted by <see cref="Breakdown"/>.</summary>
        public static readonly string[] PartNames =
        {
            "head", "face", "ears", "hair", "facial hair", "headwear", "neck", "torso", "arms", "legs", "feet", "back", "accents",
        };

        /// <summary>Triangles per part (in <see cref="PartNames"/> order) of a build (diagnostics and budget tests).</summary>
        public static int[] Breakdown(CharacterRecipe r, int lod, in CharacterMeshOptions options)
        {
            var m = new MeshData(16384, 49152);
            var o = options;
            o.SkipAo = true;
            return BuildInto(r, lod, m, null, o);
        }

        /// <summary>Builds the character with full options (face state, tint mask).</summary>
        public static void Build(CharacterRecipe r, int lod, MeshData m, SkinWeights w, in CharacterMeshOptions options)
        {
            BuildInto(r, lod, m, w, options);
        }

        private static int[] BuildInto(CharacterRecipe r, int lod, MeshData m, SkinWeights w, in CharacterMeshOptions options)
        {
            return BuildInto(r, lod, m, w, options, out _);
        }

        private static int[] BuildInto(CharacterRecipe r, int lod, MeshData m, SkinWeights w, in CharacterMeshOptions options, out int drop)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            if (m == null) throw new ArgumentNullException(nameof(m));
            CharacterRecipe recipe = r.Clone().Validate();
            if (w != null && w.Count != m.VertexCount)
                throw new ArgumentException("skin weights must start in step with the mesh (same vertex count)", nameof(w));
            m.HasUv0 = true;
            int first = m.VertexCount, firstIndex = m.IndexCount;
            var sk = new HumanoidSkeleton(recipe);
            int[] parts = null;
            drop = 0;
            for (int d = 0; d <= MaxDetailDrop; d++)
            {
                if (d > 0)
                {
                    // Over the cap: roll back and rebuild with the accessories one step plainer (the drop order).
                    m.VertexCount = first;
                    m.IndexCount = firstIndex;
                    if (w != null) w.Count = first;
                }
                var k = new BodyKit(m, w, lod);
                if (options.TintMask)
                {
                    k.TintMask = true;
                    k.TintKey = TintKeyOf(recipe);
                }
                var b = new Builder(k, sk, recipe, options, d);
                b.All();
                parts = b.PartTris;
                drop = d;
                if ((m.IndexCount - firstIndex) / 3 <= Budget(lod)) break;
            }
            if (!options.SkipAo) CharacterAo.Bake(m, first, sk, recipe, lod);
            return parts;
        }

        /// <summary>The detail drop a build needed to stay within <see cref="Budget"/> (0 = full detail; see
        /// <see cref="MaxDetailDrop"/>). Depends only on the recipe, level and headwear, never on the face state.</summary>
        public static int DetailDrop(CharacterRecipe r, int lod, HeadwearMode headwear)
        {
            var m = new MeshData(16384, 49152);
            BuildInto(r, lod, m, null, CharacterMeshOptions.For(headwear), out int drop);
            return drop;
        }

        /// <summary>A static (unskinned) body in the bind pose: previews, tests.</summary>
        public static void BuildStatic(CharacterRecipe r, int lod, MeshData m)
        {
            Build(r, lod, m, null, CharacterMeshOptions.For(HeadwearMode.Outfit));
        }

        /// <summary>True when a traffic officer wears the high-visibility vest over the light blue shirt (about half do,
        /// ref_characters.md §6).</summary>
        public static bool WearsPoliceVest(CharacterRecipe r)
        {
            return r != null && (r.Seed & 8u) != 0;
        }

        /// <summary>The colour the far crowd's instance tint replaces: the main colour of the top garment (the dress
        /// colour of a sari, kurta, daura or robe); 0 (nothing tinted) for a uniform.</summary>
        public static uint TintKeyOf(CharacterRecipe r)
        {
            if (r == null) return 0;
            OutfitSlot t = r[OutfitSlotKind.Torso];
            if (t.Item == OutfitItem.None) return CharacterPalette.SkinBase[Math.Max(0, Math.Min(9, r.Skin - 1))];
            if (t.Item == OutfitItem.PoliceUniform) return 0; // a uniform is never tinted
            return CharacterPalette.Colour(t.Item, t.Colour);
        }

        /// <summary>The head's centre and radii for a build (face features and headwear sit on it).</summary>
        public static void HeadShape(in BodyMetrics m, out V3 centre, out V3 radii)
        {
            centre = new V3(0f, m.HeadBaseY + 0.5f * m.HeadH - 0.01f, 0.01f);
            radii = new V3(0.5f * m.HeadW, 0.5f * m.HeadH, 0.5f * m.HeadD);
        }

        // ---------------------------------------------------------------------------------------------------------------

        private sealed partial class Builder
        {
            private readonly BodyKit _k;
            private readonly HumanoidSkeleton _sk;
            private readonly BodyMetrics _m;
            private readonly CharacterRecipe _r;
            private readonly CharacterMeshOptions _o;
            private readonly uint _skin, _skinShade, _blush, _lip, _hair;
            private readonly OutfitSlot _head, _torso, _legs, _feet, _back;
            private readonly V3 _hc, _hr;

            /// <summary>Head size relative to the adult head (face features scale with it).</summary>
            private readonly float _hs;

            /// <summary>Per-character variation from the seed (0..1).</summary>
            private readonly float _v0, _v1, _v2, _v3;

            /// <summary>The build's level of detail and its detail drop (see <see cref="MaxDetailDrop"/>).</summary>
            private readonly int _lod, _drop;

            public Builder(BodyKit k, HumanoidSkeleton sk, CharacterRecipe r, in CharacterMeshOptions o, int drop)
            {
                _k = k;
                _lod = k.Lod;
                _drop = drop;
                _sk = sk;
                _m = sk.Metrics;
                _r = r;
                _o = o;
                int s = Math.Max(0, Math.Min(9, r.Skin - 1));
                _skin = CharacterPalette.SkinBase[s];
                _skinShade = CharacterPalette.SkinShadow[s];
                _blush = CharacterPalette.SkinBlush[s];
                _lip = CharacterPalette.SkinLip[s];
                _hair = CharacterPalette.Hair[r.HairColour % CharacterPalette.Hair.Length];
                _head = r[OutfitSlotKind.Head];
                _torso = r[OutfitSlotKind.Torso];
                _legs = r[OutfitSlotKind.Legs];
                _feet = r[OutfitSlotKind.Feet];
                _back = r[OutfitSlotKind.Back];
                HeadShape(_m, out _hc, out _hr);
                _hs = _m.HeadScale;
                uint h = CharMath.Hash(r.Seed, 0xFACEu, (uint)r.Skin);
                _v0 = CharMath.Unit(h);
                _v1 = CharMath.Unit(CharMath.Hash(h, 1u));
                _v2 = CharMath.Unit(CharMath.Hash(h, 2u));
                _v3 = CharMath.Unit(CharMath.Hash(h, 3u));
                _expr = Expression.For(o.Face, r);
            }

            private int Lod
            {
                get { return _k.Lod; }
            }

            private bool HasHeadwear
            {
                get
                {
                    if (_o.Headwear == HeadwearMode.Helmet) return true;
                    return _o.Headwear == HeadwearMode.Outfit && _head.Item != OutfitItem.None;
                }
            }

            /// <summary>The head item actually worn (none when bare-headed, a helmet is handled separately).</summary>
            private OutfitItem HeadItem
            {
                get { return _o.Headwear == HeadwearMode.Outfit ? _head.Item : OutfitItem.None; }
            }

            /// <summary>Triangles per part of the last build (head, face, ears, hair, facial hair, headwear, neck,
            /// torso, arms, legs, feet, back, accents), for budgets and diagnostics.</summary>
            public readonly int[] PartTris = new int[PartNames.Length];

            public void All()
            {
                int i = 0, t0 = _k.M.TriangleCount;
                void Mark()
                {
                    int t = _k.M.TriangleCount;
                    PartTris[i++] = t - t0;
                    t0 = t;
                }
                // The drop order over the cap: accents one level plainer (1), then hair, facial hair and back items (2),
                // then headwear and footwear (3). The face, body, limbs and hands always keep the build's level.
                Head();
                Mark();
                Face();
                Mark();
                Ears();
                Mark();
                Plainer(2);
                Hair();
                Mark();
                FacialHairMesh();
                Mark();
                Plainer(3);
                Headwear();
                Mark();
                Plainer(int.MaxValue);
                Neck();
                Mark();
                Torso();
                Mark();
                Arms();
                Mark();
                Legs();
                Mark();
                Plainer(3);
                Feet();
                Mark();
                Plainer(2);
                Back();
                Mark();
                Plainer(1);
                Accents();
                Mark();
                Plainer(int.MaxValue);
                _k.Channel = MaterialChannel.Fabric;
                _k.Ao = 1f;
            }

            /// <summary>Builds the next parts one level plainer when the detail drop has reached <paramref name="step"/>.</summary>
            private void Plainer(int step)
            {
                _k.Lod = Math.Min(2, _lod + (_drop >= step ? 1 : 0));
            }

            /// <summary>True when the far body (LOD2, which has no plainer level) leaves out a part at this drop step.</summary>
            private bool FarDrop(int step)
            {
                return _lod >= 2 && _drop >= step;
            }

            private void Mat(MaterialChannel c, float ao = 1f)
            {
                _k.Channel = c;
                _k.Ao = ao;
            }

            /// <summary>A deterministic variation in [0, 1) for a feature key.</summary>
            private float Var(uint key)
            {
                return CharMath.Unit(CharMath.Hash(_r.Seed, key, 0x7E57u));
            }
        }
    }
}
