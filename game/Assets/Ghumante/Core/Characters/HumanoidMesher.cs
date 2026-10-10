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

        /// <summary>The main garment colour becomes an instance-tint mask (<see cref="BodyKit.TintMask"/>) for instanced copies
        /// recoloured per instance. The crowd does not use it: every far person draws the frames of their own body.</summary>
        public bool TintMask;

        /// <summary>Skip the ambient-occlusion bake (blend-shape targets reuse the base mesh's AO).</summary>
        public bool SkipAo;

        /// <summary>The light LOD0 of the player on Low devices (<see cref="CrowdLodPlan.PlayerLight"/>): the face, the
        /// five-finger hands and the headwear keep LOD0, the body, hair, ears, footwear and carried items take LOD1, so
        /// the cap is <see cref="HumanoidMesher.Lod0LightBudget"/>. Ignored at other levels.</summary>
        public bool Light;

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
    /// thumb and four two-joint fingers and nails at LOD0 (a mitten with a thumb at LOD1 and the mid-distance LOD2, a
    /// simple hand on the far body); feet in sneakers, leather shoes and trek boots with rounded heel counters, chappals
    /// with the Y strap lying on the foot, or bare (a lofted foot with an arch, ankle bones and five toes along an oblique
    /// toe line); the dhaka topi as photographed (worn high, upright walls pinched to a creased ridge from the 0.27 m
    /// front peak to the 0.19 m back on the cartoon head, the peak folded to one side, tilted 6° toward the left
    /// forehead, its dense Palpali weave as per-quad vertex colours in the Fabric channel) and the black Bhadgaunle topi;
    /// back items (daypack with straps on the torso, doko with its namlo over the cap or hair, sack, gas cylinder, baby
    /// sling) and accents (glasses, bindi, tika, tilak, pote, nose stud, earrings, shawl, mala, gloves, watch, umbrella,
    /// prayer wheel, mask). Everything is skinned to the 37-bone <c>hum</c> rig (<see cref="HumanoidSkeleton"/>, bind
    /// pose with the arms hanging) and carries UV0 = (material channel, baked AO) (docs/W2_DETAIL_CONTRACT.md §5).
    /// <para>Budgets (<see cref="Budget"/>): LOD0 ≤ 9,600 triangles plus 2,400 for accessories (the player and the
    /// nearest NPC on High), the light LOD0 ≤ 7,000 (the player on Low), LOD1 ≤ 3,000 (near NPCs), LOD2 ≤ 720 (the
    /// mid-distance skinned NPCs: eye whites and pupils, eight-sided limbs, mitten hands, shaped shoes) and the far level
    /// ≤ 500 (<see cref="FarLod"/>, the baked poses of the far crowd). A recipe that would
    /// go over its cap is rebuilt with its accessories one step plainer (<see cref="DetailDrop"/>; from step 2 the LOD2
    /// body also takes the far body's segments), so the caps hold for any combination; at least 95% of everyday crowd
    /// bodies need no drop. The same generator
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
        public const int Lod0Body = 9600, Lod0Accessories = 2400, Lod1Budget = 3000, Lod2Budget = 720, FarBudget = 500, Lod0LightBudget = 7000;

        /// <summary>The far crowd's mesh level (baked poses beyond 40 m): the plainest body.</summary>
        public const int FarLod = 3;

        /// <summary>The last step of the drop order (<see cref="DetailDrop"/>).</summary>
        public const int MaxDetailDrop = 3;

        /// <summary>Head superellipsoid exponent (slightly boxy-round, P §2.3).</summary>
        public const float HeadExponent = 2.4f;

        /// <summary>
        /// Nepali topi on the adult cartoon head (0.40 m, 1.74× a real head; ref_characters.md §2): the front peak and
        /// the back of the ridge above the rim, the rim's height above the head centre (a finger above the brows), the side
        /// tilt, the front fold's shift of the peak (a fraction of the rim's half-width), and the wall: near-vertical up to
        /// <c>TopiPinch</c> of its height, then pinched in to a crisp ridge (<c>TopiPinchExp</c>: the pinch's curve, meeting
        /// the ridge at an angle). Photographs of men wearing the topi show the front peak about as high above the rim as
        /// the rim is above the chin, the back about 0.7 of the front, the walls upright to about half their height and then
        /// leaning in almost straight to the peak, the rim narrower than the head's widest; on the cartoon head that is
        /// 0.27 / 0.19 m (W2_DESIGN 6.1's 0.11 / 0.075 m did not clear its own 0.40 m head).
        /// </summary>
        public const float TopiFrontM = 0.27f, TopiBackM = 0.19f, TopiRimY = 0.1f, TopiTiltDeg = 6f, TopiPeakShift = 0.12f, TopiPinch = 0.55f,
                           TopiPinchExp = 1.3f;

        /// <summary>Daura: tie strings (4 pairs) and pleats (W2_DESIGN 6.1, cultural checklist 10.7 #5).</summary>
        public const int DauraTies = 8, DauraPleats = 5;

        /// <summary>Fingers per hand at LOD0 (a thumb and four fingers).</summary>
        public const int FingersLod0 = 5;

        /// <summary>Triangle cap per level of detail (0..<see cref="FarLod"/>).</summary>
        public static int Budget(int lod)
        {
            return lod <= 0 ? Lod0Body + Lod0Accessories : lod == 1 ? Lod1Budget : lod == 2 ? Lod2Budget : FarBudget;
        }

        /// <summary>Triangle cap of a build with its options (the light LOD0 has its own).</summary>
        public static int Budget(int lod, in CharacterMeshOptions options)
        {
            return lod <= 0 && options.Light ? Lod0LightBudget : Budget(lod);
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
            lod = lod < 0 ? 0 : lod > FarLod ? FarLod : lod;
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
                // The mid-distance body's own drop: from step 2 it falls back to the far body's segments and dot eyes,
                // so an overloaded LOD2 (a baby sling, a sun hat and a sari) still holds its cap.
                var k = new BodyKit(m, w, Math.Min(2, lod)) { Far = lod >= FarLod || lod == 2 && d >= 2 };
                if (options.TintMask)
                {
                    k.TintMask = true;
                    k.TintKey = TintKeyOf(recipe);
                }
                var b = new Builder(k, sk, recipe, options, d);
                b.All();
                parts = b.PartTris;
                drop = d;
                if ((m.IndexCount - firstIndex) / 3 <= Budget(lod, options)) break;
            }
            if (!options.SkipAo) CharacterAo.Bake(m, first, sk, recipe, Math.Min(2, lod));
            return parts;
        }

        /// <summary>The detail drop a build needed to stay within <see cref="Budget"/> (0 = full detail; see
        /// <see cref="MaxDetailDrop"/>). Depends only on the recipe, level and headwear, never on the face state.</summary>
        public static int DetailDrop(CharacterRecipe r, int lod, HeadwearMode headwear)
        {
            return DetailDrop(r, lod, CharacterMeshOptions.For(headwear));
        }

        /// <summary>As <see cref="DetailDrop(CharacterRecipe, int, HeadwearMode)"/> with full options (the light LOD0).</summary>
        public static int DetailDrop(CharacterRecipe r, int lod, in CharacterMeshOptions options)
        {
            var m = new MeshData(16384, 49152);
            var o = options;
            o.SkipAo = true;
            BuildInto(r, lod, m, null, o, out int drop);
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

        /// <summary>
        /// The colour a crowd look varies (<see cref="CrowdVariants.GarmentColour"/>) and the tint mask replaces
        /// (<see cref="CharacterMeshOptions.TintMask"/>): the main colour of the top garment (the dress colour of a sari,
        /// kurta, daura or chuba, the colour of a shirt, T-shirt, hoodie or jacket). 0 (nothing tinted) for a uniform, a
        /// monk's or sadhu's robe, a bare chest and a near-black garment (the black haku patasi, a black jacket: their
        /// folds cannot be told apart from black trim). The mask covers the garment's own cloth only: trousers or a skirt
        /// of another cloth never take its colour, even when their colour is a shade of it.
        /// </summary>
        public static uint TintKeyOf(CharacterRecipe r)
        {
            if (r == null) return 0;
            OutfitSlot t = r[OutfitSlotKind.Torso];
            if (t.Item == OutfitItem.None || t.Item == OutfitItem.PoliceUniform || t.Item == OutfitItem.Robe) return 0;
            uint key = CharacterPalette.Colour(t.Item, t.Colour);
            return Tintable(key) ? key : 0;
        }

        /// <summary>True when a garment colour can carry the tint mask: light enough that its shades differ from black
        /// trim (channel sum at least 90).</summary>
        public static bool Tintable(uint rgb)
        {
            return ((rgb >> 16) & 0xFF) + ((rgb >> 8) & 0xFF) + (rgb & 0xFF) >= 90;
        }

        /// <summary>The colour of the legs' garment (or of the hem below the top) for a torso and legs slot and the dress
        /// colour: the full-length dress, the police trousers, the daura's matching suruwal, the kurta's suruwal by its
        /// pattern, otherwise the legs item's own colour.</summary>
        internal static uint LowerColour(in OutfitSlot torso, in OutfitSlot legs, uint dress)
        {
            OutfitItem t = torso.Item, l = legs.Item;
            if (CharacterRecipe.IsFullLength(t)) return dress;
            if (t == OutfitItem.PoliceUniform && (l == OutfitItem.Trousers || l == OutfitItem.None)) return CharacterPalette.PoliceTrousers;
            if (l == OutfitItem.None) return t == OutfitItem.Kurta || t == OutfitItem.Daura ? dress : 0x34383Eu;
            if (l == OutfitItem.Suruwal && t == OutfitItem.Daura) return dress;
            if (l == OutfitItem.Suruwal && t == OutfitItem.Kurta) return CharacterPalette.At(CharacterPalette.Suruwal, torso.Pattern);
            return CharacterPalette.Colour(l, legs.Colour);
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

            /// <summary>The build's kit level of detail (0..2; the far level builds at 2 with <see cref="BodyKit.Far"/>)
            /// and its detail drop (see <see cref="MaxDetailDrop"/>).</summary>
            private readonly int _lod, _drop;

            /// <summary>The light LOD0: body parts at LOD1, the face, hands and headwear at LOD0.</summary>
            private readonly bool _light;

            public Builder(BodyKit k, HumanoidSkeleton sk, CharacterRecipe r, in CharacterMeshOptions o, int drop)
            {
                _k = k;
                _lod = k.Lod;
                _drop = drop;
                _light = o.Light && k.Lod == 0;
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

            /// <summary>The far crowd body (the plainest level 2).</summary>
            private bool Far
            {
                get { return _k.Far; }
            }

            /// <summary>The mid-distance body: level 2 without <see cref="Far"/> (eye whites, rounder limbs, mitten hands).</summary>
            private bool Mid
            {
                get { return _k.Lod == 2 && !_k.Far; }
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
                // then headwear and footwear (3). The face, body, limbs and hands always keep the build's level. The light
                // LOD0 builds the parts marked "light" at LOD1 from the start.
                Head();
                Mark();
                Face();
                Mark();
                Plainer(int.MaxValue, true);
                Ears();
                Mark();
                Plainer(2, true);
                Hair();
                Mark();
                FacialHairMesh();
                Mark();
                Plainer(3);
                Headwear();
                Mark();
                Plainer(int.MaxValue, true);
                Neck();
                Mark();
                // The crowd's tint mask covers the garment: the top with its sleeves (and its own skirt), and the legs when
                // they wear the same cloth (a daura's suruwal, a sari).
                _k.TintScope = true;
                Torso();
                Mark();
                Arms();
                Mark();
                _k.TintScope = _g.Lower == _k.TintKey;
                Legs();
                Mark();
                _k.TintScope = false;
                Plainer(3, true);
                Feet();
                Mark();
                Plainer(2, true);
                Back();
                Mark();
                Plainer(1, true);
                Accents();
                Mark();
                Plainer(int.MaxValue);
                _k.Channel = MaterialChannel.Fabric;
                _k.Ao = 1f;
            }

            /// <summary>Builds the next parts one level plainer when the detail drop has reached <paramref name="step"/>, or
            /// when they are <paramref name="light"/> parts of the light LOD0.</summary>
            private void Plainer(int step, bool light = false)
            {
                _k.Lod = Math.Min(2, _lod + (_drop >= step || light && _light ? 1 : 0));
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
