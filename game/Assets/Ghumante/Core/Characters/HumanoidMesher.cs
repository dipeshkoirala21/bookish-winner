using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Characters
{
    /// <summary>What the character wears on the head for this mesh.</summary>
    public enum HeadwearMode : byte
    {
        /// <summary>The recipe's head item (topi, beanie, sun hat or nothing).</summary>
        Outfit = 0,

        /// <summary>The open-face helmet in the recipe's helmet colour (auto-equipped on every two-wheeler; P §2.7).
        /// Hair is squashed under it and ponytails, braids and buns stay out.</summary>
        Helmet = 1,

        /// <summary>Bare head.</summary>
        None = 2,
    }

    /// <summary>
    /// Builds a cartoon character from a <see cref="CharacterRecipe"/> (W2_DESIGN 6.1 and 10.3, P §2.3–2.8): about 22
    /// parametric primitives (superellipsoid head with eyes, brows, nose, smile and ears; a hair cap and one of 12
    /// hairstyles; neck; a lofted torso; tapered-capsule limbs with 1.15× joint bulges; mitten hands with thumbs; shoes;
    /// garment shells and hems; headwear; a back item), skinned to the 37-bone <c>hum</c> rig
    /// (<see cref="HumanoidSkeleton"/>, bind pose with the arms hanging). Outfits are parameter sets on the same
    /// primitives: the dhaka topi is asymmetric (front 0.11 m, back 0.075 m, tilted 6° with an off-centre crown fold and a
    /// faceted diamond lattice), the daura has 8 tie strings in 4 pairs and 5 pleats with a closed round neck, the sari a
    /// pallu and a border, sneakers a white sole and a generic stitch (no brand marks). No alpha anywhere.
    /// <para>Budgets (<see cref="Budget"/>): LOD0 ≤ 5,000 triangles plus 600 for accessories, LOD1 ≤ 2,000, LOD2 ≤ 600
    /// (the crowd body Track C bakes into its VAT). The same generator builds NPC bodies
    /// (<see cref="CharacterRecipe.ForPedestrian"/>, static with <see cref="BuildStatic"/>), so the player and the crowd
    /// share one style. Deterministic for a recipe; allocation only through MeshData growth.</para>
    /// </summary>
    public static class HumanoidMesher
    {
        public const int Lod0Body = 5000, Lod0Accessories = 600, Lod1Budget = 2000, Lod2Budget = 600;

        /// <summary>Head superellipsoid exponent (slightly boxy-round, P §2.3).</summary>
        public const float HeadExponent = 2.4f;

        /// <summary>Dhaka topi: front and back heights and the side tilt (W2_DESIGN 6.1).</summary>
        public const float TopiFrontM = 0.11f, TopiBackM = 0.075f, TopiTiltDeg = 6f, TopiFoldDeg = 8f;

        /// <summary>Daura: tie strings (4 pairs) and pleats (W2_DESIGN 6.1, cultural checklist 10.7 #5).</summary>
        public const int DauraTies = 8, DauraPleats = 5;

        /// <summary>Triangle cap per level of detail.</summary>
        public static int Budget(int lod)
        {
            return lod <= 0 ? Lod0Body + Lod0Accessories : lod == 1 ? Lod1Budget : Lod2Budget;
        }

        /// <summary>Builds the skinned character into <paramref name="m"/> and <paramref name="w"/> (appending) with the
        /// recipe's own headwear.</summary>
        public static void Build(CharacterRecipe r, int lod, MeshData m, SkinWeights w)
        {
            Build(r, lod, m, w, HeadwearMode.Outfit);
        }

        /// <summary>Builds the skinned character with the given headwear (the player keeps an Outfit and a Helmet mesh and
        /// swaps them on mount). <paramref name="w"/> may be null for a static mesh.</summary>
        public static void Build(CharacterRecipe r, int lod, MeshData m, SkinWeights w, HeadwearMode headwear)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            if (m == null) throw new ArgumentNullException(nameof(m));
            CharacterRecipe recipe = r.Clone().Validate();
            if (w != null && w.Count != m.VertexCount)
                throw new ArgumentException("skin weights must start in step with the mesh (same vertex count)", nameof(w));
            var k = new BodyKit(m, w, lod);
            var sk = new HumanoidSkeleton(recipe.Build);
            var b = new Builder(k, sk, recipe, headwear);
            b.All();
        }

        /// <summary>A static (unskinned) body in the bind pose: NPC crowds, previews.</summary>
        public static void BuildStatic(CharacterRecipe r, int lod, MeshData m)
        {
            Build(r, lod, m, null, HeadwearMode.Outfit);
        }

        /// <summary>The head's centre and radii for a build (face features and headwear sit on it).</summary>
        public static void HeadShape(in BodyMetrics m, out V3 centre, out V3 radii)
        {
            centre = new V3(0f, m.HeadBaseY + 0.5f * m.HeadH - 0.01f, 0.01f);
            radii = new V3(0.5f * m.HeadW, 0.5f * m.HeadH, 0.5f * m.HeadD);
        }

        // ---------------------------------------------------------------------------------------------------------------

        private sealed class Builder
        {
            private readonly BodyKit _k;
            private readonly HumanoidSkeleton _sk;
            private readonly BodyMetrics _m;
            private readonly CharacterRecipe _r;
            private readonly HeadwearMode _headwear;
            private readonly uint _skin, _skinShade, _blush, _hair;
            private readonly OutfitSlot _head, _torso, _legs, _feet, _back;
            private readonly V3 _hc, _hr;

            public Builder(BodyKit k, HumanoidSkeleton sk, CharacterRecipe r, HeadwearMode headwear)
            {
                _k = k;
                _sk = sk;
                _m = sk.Metrics;
                _r = r;
                _headwear = headwear;
                _skin = CharacterPalette.SkinBase[r.Skin - 1];
                _skinShade = CharacterPalette.SkinShadow[r.Skin - 1];
                _blush = CharacterPalette.SkinBlush[r.Skin - 1];
                _hair = CharacterPalette.Hair[r.HairColour];
                _head = r[OutfitSlotKind.Head];
                _torso = r[OutfitSlotKind.Torso];
                _legs = r[OutfitSlotKind.Legs];
                _feet = r[OutfitSlotKind.Feet];
                _back = r[OutfitSlotKind.Back];
                HeadShape(_m, out _hc, out _hr);
            }

            private int Lod
            {
                get { return _k.Lod; }
            }

            private bool HasHeadwear
            {
                get { return _headwear == HeadwearMode.Helmet || _headwear == HeadwearMode.Outfit && _head.Item != OutfitItem.None; }
            }

            public void All()
            {
                Head();
                Face();
                Hair();
                Headwear();
                Neck();
                Torso();
                Arms();
                Legs();
                Feet();
                Back();
            }

            // ----- Head and face ----------------------------------------------------------------------------------

            private void Head()
            {
                _k.Ellipsoid(_hc, _hr, Quat.Identity, _skin, _k.Seg(20, 6), _k.Seg(14, 5), Bone.Head, HeadExponent);
                if (Lod >= 2) return;
                // Ears: flattened half-ellipsoids at the sides.
                for (int s = -1; s <= 1; s += 2)
                {
                    var c = new V3(_hc.X + s * (_hr.X - 0.004f), _hc.Y - 0.02f, _hc.Z - 0.01f);
                    _k.Ellipsoid(c, new V3(0.02f, 0.045f, 0.034f), Quat.Euler(0f, s * 12f, 0f), _skinShade, _k.Seg(8, 4), _k.Seg(4, 3), Bone.Head);
                }
            }

            /// <summary>The head surface's forward depth at (x, y) relative to its centre.</summary>
            private float FrontZ(float x, float y)
            {
                float e = HeadExponent;
                float t = 1f - MathF.Pow(MathF.Abs(x) / _hr.X, e) - MathF.Pow(MathF.Abs(y) / _hr.Y, e);
                return t <= 0f ? 0f : _hr.Z * MathF.Pow(t, 1f / e);
            }

            private V3 OnFace(float x, float y, float lift)
            {
                return new V3(_hc.X + x, _hc.Y + y, _hc.Z + FrontZ(x, y) + lift);
            }

            private void Face()
            {
                // Eyes at 45% of the head height, 0.11 m apart, 0.055 m tall (14% of the head; readable at 48 px).
                float ey = -_hr.Y + 0.45f * 2f * _hr.Y; // 45% up from the chin
                for (int s = -1; s <= 1; s += 2)
                {
                    float ex = s * 0.055f;
                    Quat yaw = Quat.Euler(0f, s * 14f, 0f);
                    if (Lod >= 2)
                    {
                        _k.Ellipsoid(OnFace(ex, ey, -0.002f), new V3(0.016f, 0.022f, 0.008f), yaw, CharacterPalette.Pupil, 4, 3, Bone.Head);
                        continue;
                    }
                    _k.Ellipsoid(OnFace(ex, ey, -0.004f), new V3(0.023f, 0.0275f, 0.009f), yaw, CharacterPalette.EyeWhite, _k.Seg(10, 6), _k.Seg(6, 4), Bone.Head);
                    _k.Ellipsoid(OnFace(ex + s * 0.002f, ey - 0.003f, 0.002f), new V3(0.0145f, 0.019f, 0.007f), yaw, CharacterPalette.Pupil, _k.Seg(8, 5), _k.Seg(5, 3), Bone.Head);
                    if (Lod == 0)
                        _k.Ellipsoid(OnFace(ex - s * 0.004f, ey + 0.007f, 0.007f), new V3(0.005f, 0.005f, 0.003f), Quat.Identity, CharacterPalette.EyeWhite, 4, 3, Bone.Head);
                    // Brows in the hair colour, a little tilted.
                    _k.Ellipsoid(OnFace(ex, ey + 0.05f, 0.0f), new V3(0.025f, 0.0065f, 0.007f), Quat.Euler(0f, s * 14f, -s * 8f), _hair, _k.Seg(8, 4), 3, Bone.Head);
                    if (Lod == 0)
                        _k.Ellipsoid(OnFace(s * 0.095f, ey - 0.055f, -0.003f), new V3(0.022f, 0.012f, 0.005f), Quat.Euler(0f, s * 30f, 0f), _blush, 6, 3, Bone.Head);
                }
                if (Lod >= 2) return;
                // Nose bump and a gentle smile.
                _k.Sphere(OnFace(0f, ey - 0.04f, -0.006f), 0.0175f, CharacterPalette.Shade(_skin, 0.94f), _k.Seg(8, 4), _k.Seg(6, 3), Bone.Head);
                _k.Ellipsoid(OnFace(0f, ey - 0.088f, -0.003f), new V3(0.03f, 0.0075f, 0.006f), Quat.Identity, CharacterPalette.Mouth, _k.Seg(8, 4), 3, Bone.Head);
                if (Lod == 0)
                {
                    for (int s = -1; s <= 1; s += 2)
                        _k.Ellipsoid(OnFace(s * 0.029f, ey - 0.083f, -0.004f), new V3(0.007f, 0.006f, 0.005f), Quat.Euler(0f, 0f, s * 35f), CharacterPalette.Mouth, 4, 3, Bone.Head);
                }
            }

            // ----- Hair -------------------------------------------------------------------------------------------

            private void Hair()
            {
                int style = _r.Hair;
                bool covered = HasHeadwear;
                if (style == 2 && covered) return; // a buzz cut disappears under a hat
                float offset = covered ? 0.004f : style == 2 ? 0.006f : 0.015f;
                float front = style == 2 ? 0.11f : 0.09f, back = style == 6 || style == 7 || style == 5 || style == 11 ? -0.17f : -0.12f;
                HairCap(offset, front, -0.03f, back, covered ? 0.9f : 1f);
                if (Lod >= 2 && !(style == 9 || style == 10 || style == 12)) return;
                V3 c = _hc;
                switch (style)
                {
                    case 1:
                        if (!covered)
                            _k.Ellipsoid(c + new V3(0.03f, 0.11f, 0.15f), new V3(0.13f, 0.035f, 0.055f), Quat.Euler(-10f, 0f, -12f), _hair, _k.Seg(10, 5), _k.Seg(6, 3), Bone.Head);
                        break;
                    case 3:
                        if (covered) break;
                        for (int i = 0; i < 14; i++)
                        {
                            // Golden-angle spiral over the upper head.
                            float t = (i + 0.5f) / 14f;
                            float phi = MathF.Acos(1f - 0.85f * t);
                            float th = i * 2.39996323f;
                            var d = new V3(MathF.Sin(phi) * MathF.Cos(th), MathF.Cos(phi), MathF.Sin(phi) * MathF.Sin(th));
                            if (d.Z > 0.6f && d.Y < 0.5f) d = new V3(d.X, d.Y + 0.3f, d.Z * 0.5f);
                            V3 p = c + new V3(d.X * (_hr.X + 0.01f), d.Y * (_hr.Y + 0.01f), d.Z * (_hr.Z + 0.01f));
                            _k.Sphere(p, 0.036f, _hair, _k.Seg(6, 4), _k.Seg(4, 3), Bone.Head);
                        }
                        break;
                    case 4:
                        if (covered) break;
                        for (int i = 0; i < 7; i++)
                        {
                            float th = -1.2f + i * 0.4f;
                            var d = new V3(MathF.Sin(th) * 0.55f, 0.8f, -MathF.Cos(th) * 0.35f + 0.1f).Normalized;
                            V3 p = c + new V3(d.X * _hr.X, d.Y * _hr.Y, d.Z * _hr.Z);
                            _k.Cone(p - d * 0.02f, p + d * 0.075f, 0.035f, 0f, _hair, _k.Seg(6, 4), Bone.Head);
                        }
                        break;
                    case 5:
                        for (int s = -1; s <= 1; s += 2)
                            _k.Ellipsoid(c + new V3(s * 0.165f, -0.06f, -0.02f), new V3(0.05f, 0.13f, 0.15f), Quat.Identity, _hair, _k.Seg(8, 4), _k.Seg(6, 3), Bone.Head);
                        _k.Ellipsoid(c + new V3(0f, -0.06f, -0.16f), new V3(0.16f, 0.13f, 0.06f), Quat.Identity, _hair, _k.Seg(10, 5), _k.Seg(6, 3), Bone.Head);
                        break;
                    case 6:
                        _k.Ellipsoid(c + new V3(0f, -0.2f, -0.165f), new V3(0.15f, 0.24f, 0.05f), Quat.Euler(8f, 0f, 0f), _hair, _k.Seg(10, 5), _k.Seg(8, 4), Bone.Head, 2f, Bone.Chest, 0.7f);
                        break;
                    case 7:
                        if (covered) break;
                        for (int i = 0; i < 9; i++)
                        {
                            float t = i / 8f;
                            var p = new V3((i % 2 == 0 ? -0.008f : 0.008f), c.Y - 0.06f - 0.38f * t, c.Z - 0.19f - 0.03f * t);
                            float w0 = 1f - 0.6f * t;
                            _k.Ellipsoid(p, new V3(0.034f - 0.01f * t, 0.03f, 0.03f - 0.006f * t), Quat.Euler(0f, 0f, i % 2 == 0 ? 20f : -20f), _hair,
                                         _k.Seg(6, 4), _k.Seg(4, 3), Bone.Head, 2f, Bone.Chest, w0);
                        }
                        _k.Cone(new V3(0f, c.Y - 0.47f, c.Z - 0.22f), new V3(0f, c.Y - 0.56f, c.Z - 0.22f), 0.02f, 0.035f, CharacterPalette.Tassel, _k.Seg(6, 4), Bone.Chest);
                        break;
                    case 8:
                        if (covered) break;
                        _k.Sphere(c + new V3(0f, 0.12f, -0.17f), 0.03f, CharacterPalette.Tassel, _k.Seg(6, 4), _k.Seg(4, 3), Bone.Head);
                        _k.Capsule(c + new V3(0f, 0.12f, -0.19f), c + new V3(0f, -0.16f, -0.28f), 0.05f, 0.018f, _hair, Bone.Head, Bone.Head,
                                   _k.Seg(8, 4), _k.Seg(4, 2), false);
                        break;
                    case 9:
                        if (covered) break;
                        _k.Sphere(c + new V3(0f, -0.07f, -0.2f), 0.065f, _hair, _k.Seg(8, 4), _k.Seg(6, 3), Bone.Head);
                        break;
                    case 10:
                        if (covered) break;
                        _k.Sphere(c + new V3(0f, 0.21f, -0.03f), 0.06f, _hair, _k.Seg(8, 4), _k.Seg(6, 3), Bone.Head);
                        break;
                    case 11:
                        for (int i = 0; i < 5; i++)
                        {
                            float th = 2.0f + i * 0.55f; // from one side round the back to the other
                            var p = new V3(c.X + MathF.Cos(th) * 0.17f, c.Y - 0.09f, c.Z + MathF.Sin(th) * 0.17f - 0.02f);
                            _k.Ellipsoid(p, new V3(0.045f, 0.12f, 0.03f), Quat.Euler(0f, -th * Quat.Rad2Deg + 90f, (i % 2 == 0 ? 8f : -8f)), _hair,
                                         _k.Seg(6, 4), _k.Seg(6, 3), Bone.Head);
                        }
                        break;
                    case 12:
                        if (covered) break;
                        for (int s = -1; s <= 1; s += 2)
                            _k.Sphere(c + new V3(s * 0.12f, 0.15f, -0.03f), 0.07f, _hair, _k.Seg(8, 4), _k.Seg(6, 3), Bone.Head);
                        break;
                }
            }

            /// <summary>An offset shell of the head above a hairline (front, side and back heights relative to the head
            /// centre), open at the hairline.</summary>
            private void HairCap(float offset, float frontY, float sideY, float backY, float crownScale)
            {
                int u = _k.Seg(20, 6), rows = _k.Seg(10, 3);
                float e = HeadExponent, pe = 2f / e;
                var r = new V3(_hr.X + offset, (_hr.Y + offset) * crownScale, _hr.Z + offset);
                V3 c = _hc + new V3(0f, (_hr.Y + offset) * (1f - crownScale) * 0.5f, 0f);
                int pole = _k.Vertex(c + new V3(0f, r.Y, 0f), V3.Up, _hair, Bone.Head, Bone.Head, 1f);
                int first = _k.M.VertexCount;
                for (int row = 1; row <= rows; row++)
                {
                    for (int j = 0; j < u; j++)
                    {
                        float th = 6.28318530718f * j / u;
                        float ct = MathF.Cos(th), st = MathF.Sin(th);
                        // Hairline height around the head: front (st = 1), sides, back (st = −1).
                        float lineY = st >= 0f ? sideY + (frontY - sideY) * st : sideY + (backY - sideY) * -st;
                        float yN = Math.Max(-0.98f, Math.Min(0.98f, lineY / r.Y));
                        float phiMax = MathF.Acos(BodyKit.SPow(yN, e / 2f));
                        float phi = phiMax * row / rows;
                        float sp = MathF.Sin(phi), cp = MathF.Cos(phi);
                        float x = r.X * BodyKit.SPow(sp, pe) * BodyKit.SPow(ct, pe);
                        float y = r.Y * BodyKit.SPow(cp, pe);
                        float z = r.Z * BodyKit.SPow(sp, pe) * BodyKit.SPow(st, pe);
                        var n = new V3(BodyKit.SPow(x / r.X, e - 1f) / r.X, BodyKit.SPow(y / r.Y, e - 1f) / r.Y, BodyKit.SPow(z / r.Z, e - 1f) / r.Z);
                        _k.Vertex(c + new V3(x, y, z), n, _hair, Bone.Head, Bone.Head, 1f);
                    }
                }
                for (int j = 0; j < u; j++)
                {
                    int j1 = (j + 1) % u;
                    _k.Tri(pole, first + j1, first + j);
                    for (int row = 0; row + 1 < rows; row++)
                    {
                        int a = first + row * u + j, b = first + row * u + j1;
                        _k.Tri(a, b, b + u);
                        _k.Tri(a, b + u, a + u);
                    }
                }
            }

            // ----- Headwear ---------------------------------------------------------------------------------------

            private void Headwear()
            {
                if (_headwear == HeadwearMode.None) return;
                if (_headwear == HeadwearMode.Helmet)
                {
                    Helmet();
                    return;
                }
                switch (_head.Item)
                {
                    case OutfitItem.DhakaTopi:
                        Topi(CharacterPalette.Colour(OutfitItem.DhakaTopi, _head.Colour), true);
                        break;
                    case OutfitItem.BhadgaunleTopi:
                        Topi(CharacterPalette.Bhadgaunle[0], false);
                        break;
                    case OutfitItem.Beanie:
                        Beanie(CharacterPalette.Colour(OutfitItem.Beanie, _head.Colour));
                        break;
                    case OutfitItem.SunHat:
                        SunHat(CharacterPalette.Colour(OutfitItem.SunHat, _head.Colour));
                        break;
                }
                if (_back.Item == OutfitItem.Doko && _head.Item != OutfitItem.SunHat)
                {
                    // The namlo strap across the forehead (porters).
                    _k.BeginLoft();
                    for (int i = 0; i < 2; i++)
                        _k.FlatRing(_hc + new V3(0f, 0.08f + 0.025f * i, 0.005f), _hr.X * 0.97f + 0.006f, _hr.Z * 0.97f + 0.006f,
                                    CharacterPalette.Strap, Bone.Head, Bone.Head, 1f, HeadExponent);
                    _k.EndLoft(_k.Seg(16, 6), LoftCap.None, LoftCap.None);
                }
            }

            /// <summary>The dhaka topi (W2_DESIGN 6.1): a soft cap on a round base, front 0.11 m and back 0.075 m tall,
            /// the crown fold 8° off centre, worn tilted 6° to one side. LOD0 and LOD1 are faceted with a diamond lattice
            /// of pattern dots on the base colour; LOD2 is plain.</summary>
            private void Topi(uint baseRgb, bool pattern)
            {
                int u = _k.Seg(12, 6), rows = _k.Seg(6, 2);
                // The rim sits round the upper head where the circumference fits (×1.05); the wall stands on it.
                const float baseY = 0.13f, wallLift = 0.05f;
                float fit = MathF.Pow(1f - MathF.Pow(baseY / _hr.Y, HeadExponent), 1f / HeadExponent);
                float rx = _hr.X * fit * 1.05f, rz = _hr.Z * fit * 1.05f;
                V3 pivot = _hc + new V3(0f, baseY, 0f);
                Quat tilt = Quat.Euler(0f, 0f, -TopiTiltDeg);
                float fold = MathF.Tan(TopiFoldDeg * Quat.Deg2Rad) * 0.06f;
                uint dot0 = pattern ? CharacterPalette.TopiPatterns[_head.Pattern % CharacterPalette.TopiPatterns.Length] : baseRgb;
                uint dot1 = pattern ? CharacterPalette.TopiPatterns[(_head.Pattern + 1) % CharacterPalette.TopiPatterns.Length] : baseRgb;
                bool faceted = Lod < 2;
                // Grid of points: rows from the base ring (0) to the top ring (rows), then a crown centre.
                int cols = u;
                int count = (rows + 1) * cols;
                var pts = new V3[count];
                for (int row = 0; row <= rows; row++)
                {
                    float t = row / (float)rows;
                    for (int j = 0; j < cols; j++)
                    {
                        float th = 6.28318530718f * j / cols;
                        float st = MathF.Sin(th), ct = MathF.Cos(th);
                        float h = wallLift + 0.5f * (TopiFrontM + TopiBackM) + 0.5f * (TopiFrontM - TopiBackM) * st; // front is +Z (st = 1)
                        float shrink = 1f + 0.06f * t - 0.1f * t * t;
                        var p = new V3(ct * rx * shrink, h * t, st * rz * shrink);
                        pts[row * cols + j] = pivot + tilt * p;
                    }
                }
                V3 crown = pivot + tilt * new V3(fold, wallLift + 0.5f * (TopiFrontM + TopiBackM) - 0.012f, -0.01f);
                if (!faceted)
                {
                    int first = _k.M.VertexCount;
                    for (int i = 0; i < count; i++)
                        _k.Vertex(pts[i], pts[i] - (pivot + tilt * new V3(0f, 0.03f, 0f)), baseRgb, Bone.HeadAttach, Bone.HeadAttach, 1f);
                    int top = _k.Vertex(crown, tilt * V3.Up, baseRgb, Bone.HeadAttach, Bone.HeadAttach, 1f);
                    for (int row = 0; row < rows; row++)
                        for (int j = 0; j < cols; j++)
                        {
                            int j1 = (j + 1) % cols;
                            int a = first + row * cols + j, b = first + row * cols + j1;
                            _k.Tri(a, b, b + cols);
                            _k.Tri(a, b + cols, a + cols);
                        }
                    for (int j = 0; j < cols; j++) _k.Tri(top, first + rows * cols + j, first + rows * cols + (j + 1) % cols);
                    return;
                }
                V3 centre = pivot + tilt * new V3(0f, 0.04f, 0f);
                for (int row = 0; row < rows; row++)
                {
                    for (int j = 0; j < cols; j++)
                    {
                        int j1 = (j + 1) % cols;
                        V3 a = pts[row * cols + j], b = pts[row * cols + j1], c = pts[(row + 1) * cols + j1], d = pts[(row + 1) * cols + j];
                        V3 mid = 0.25f * (a + b + c + d);
                        uint col = baseRgb;
                        // The lattice: diamonds of small dots on the base cloth.
                        if (pattern && row > 0 && (row + j) % 2 == 0 && (j / 2 + row) % 2 == 0) col = (j % 4 == 0) ? dot0 : dot1;
                        if (row == 0) col = CharacterPalette.Shade(baseRgb, 0.85f); // the band at the base
                        _k.Quad(a, b, c, d, mid - centre, col, Bone.HeadAttach);
                    }
                }
                // The crown: a fan of flat facets to the off-centre fold.
                for (int j = 0; j < cols; j++)
                {
                    V3 a = pts[rows * cols + j], b = pts[rows * cols + (j + 1) % cols];
                    V3 n = V3.Cross(b - a, crown - a);
                    if (V3.Dot(n, tilt * V3.Up) < 0f) n = -n;
                    uint col = pattern && j % 3 == 0 ? dot0 : baseRgb;
                    int ia = _k.Vertex(a, n, col, Bone.HeadAttach, Bone.HeadAttach, 1f);
                    int ib = _k.Vertex(b, n, col, Bone.HeadAttach, Bone.HeadAttach, 1f);
                    int ic = _k.Vertex(crown, n, col, Bone.HeadAttach, Bone.HeadAttach, 1f);
                    _k.Tri(ia, ib, ic);
                }
            }

            private void Helmet()
            {
                uint shell = CharacterPalette.Helmet[_r.HelmetColour];
                uint stripe = _r.HelmetColour == 3 ? 0x1E88E5u : 0xFAFAFAu;
                // An open-face shell over the crown, cut above the brows and over the ears, with a white stripe and a
                // short visor.
                const float offset = 0.04f, lift = 0.035f, browY = 0.075f;
                HairCapLike(offset, browY, -0.07f, -0.13f, shell, lift);
                if (Lod >= 2) return;
                // A stripe over the crown, from above the visor to the back.
                _k.Ellipsoid(_hc + new V3(0f, _hr.Y * 0.62f, -0.02f), new V3(0.026f, 0.5f * _hr.Y + offset, _hr.Z * 0.85f + offset), Quat.Euler(-12f, 0f, 0f), stripe,
                             _k.Seg(6, 4), _k.Seg(8, 4), Bone.HeadAttach);
                _k.Ellipsoid(_hc + new V3(0f, browY + 0.01f, _hr.Z + offset - 0.01f), new V3(0.14f, 0.012f, 0.06f), Quat.Euler(-14f, 0f, 0f), 0x2B3A48u,
                             _k.Seg(10, 5), 3, Bone.HeadAttach);
            }

            private void Beanie(uint rgb)
            {
                HairCapLike(0.022f, 0.07f, -0.02f, -0.1f, rgb);
                _k.BeginLoft();
                _k.FlatRing(_hc + new V3(0f, 0.06f, 0f), _hr.X + 0.03f, _hr.Z + 0.03f, CharacterPalette.Shade(rgb, 0.85f), Bone.HeadAttach, Bone.HeadAttach, 1f, HeadExponent);
                _k.FlatRing(_hc + new V3(0f, 0.11f, 0f), _hr.X + 0.02f, _hr.Z + 0.02f, CharacterPalette.Shade(rgb, 0.85f), Bone.HeadAttach, Bone.HeadAttach, 1f, HeadExponent);
                _k.EndLoft(_k.Seg(16, 6), LoftCap.None, LoftCap.None);
            }

            private void SunHat(uint rgb)
            {
                // A soft crown standing on the brim, and the brim.
                float y0 = 0.07f;
                float fit = MathF.Pow(1f - MathF.Pow(y0 / _hr.Y, HeadExponent), 1f / HeadExponent);
                _k.BeginLoft();
                _k.FlatRing(_hc + new V3(0f, y0, 0f), _hr.X * fit + 0.02f, _hr.Z * fit + 0.02f, CharacterPalette.Shade(rgb, 0.8f), Bone.HeadAttach, Bone.HeadAttach, 1f, 2.4f);
                _k.FlatRing(_hc + new V3(0f, y0 + 0.03f, 0f), _hr.X * fit + 0.018f, _hr.Z * fit + 0.018f, rgb, Bone.HeadAttach, Bone.HeadAttach, 1f, 2.4f);
                _k.FlatRing(_hc + new V3(0f, _hr.Y + 0.05f, 0f), _hr.X * 0.75f, _hr.Z * 0.75f, rgb, Bone.HeadAttach, Bone.HeadAttach, 1f, 2.4f);
                _k.EndLoft(_k.Seg(14, 6), LoftCap.None, Lod >= 2 ? LoftCap.Flat : LoftCap.Round);
                _k.Ellipsoid(_hc + new V3(0f, y0, 0.01f), new V3(0.33f, 0.012f, 0.34f), Quat.Identity, CharacterPalette.Shade(rgb, 0.95f), _k.Seg(18, 6), 3, Bone.HeadAttach);
            }

            /// <summary>A hat crown shaped like the hair cap, bound to head_attach.</summary>
            private void HairCapLike(float offset, float frontY, float sideY, float backY, uint rgb, float lift = 0f)
            {
                int u = _k.Seg(16, 6), rows = _k.Seg(6, 2);
                var r = new V3(_hr.X + offset, _hr.Y + offset + lift, _hr.Z + offset);
                int pole = _k.Vertex(_hc + new V3(0f, r.Y, 0f), V3.Up, rgb, Bone.HeadAttach, Bone.HeadAttach, 1f);
                int first = _k.M.VertexCount;
                for (int row = 1; row <= rows; row++)
                    for (int j = 0; j < u; j++)
                    {
                        float th = 6.28318530718f * j / u;
                        float st = MathF.Sin(th), ct = MathF.Cos(th);
                        float lineY = st >= 0f ? sideY + (frontY - sideY) * st : sideY + (backY - sideY) * -st;
                        float phiMax = MathF.Acos(Math.Max(-0.98f, Math.Min(0.98f, lineY / r.Y)));
                        float phi = phiMax * row / rows;
                        var p = new V3(r.X * MathF.Sin(phi) * ct, r.Y * MathF.Cos(phi), r.Z * MathF.Sin(phi) * st);
                        _k.Vertex(_hc + p, new V3(p.X / r.X, p.Y / r.Y, p.Z / r.Z), rgb, Bone.HeadAttach, Bone.HeadAttach, 1f);
                    }
                for (int j = 0; j < u; j++)
                {
                    int j1 = (j + 1) % u;
                    _k.Tri(pole, first + j1, first + j);
                    for (int row = 0; row + 1 < rows; row++)
                    {
                        int a = first + row * u + j, b = first + row * u + j1;
                        _k.Tri(a, b, b + u);
                        _k.Tri(a, b + u, a + u);
                    }
                }
            }

            // ----- Neck and torso ---------------------------------------------------------------------------------

            private void Neck()
            {
                var a = new V3(0f, _m.ShoulderY - 0.02f, -0.01f);
                var b = new V3(0f, _m.HeadBaseY + 0.05f, 0f);
                _k.Capsule(a, b, 0.05f, 0.048f, _skin, Bone.Neck, Bone.Chest, _k.Seg(10, 5), 3, false, LoftCap.None, LoftCap.None);
            }

            private OutfitItem TopItem
            {
                get { return _torso.Item; }
            }

            private uint TopColour
            {
                get
                {
                    switch (_torso.Item)
                    {
                        case OutfitItem.None: return _skin;
                        case OutfitItem.Daura: return CharacterPalette.Waistcoat[_torso.Colour % CharacterPalette.Waistcoat.Length];
                        case OutfitItem.Sari: return CharacterPalette.Shade(CharacterPalette.Colour(OutfitItem.Sari, _torso.Colour), 0.8f);
                        default: return CharacterPalette.Colour(_torso.Item, _torso.Colour);
                    }
                }
            }

            /// <summary>The garment colour of the skirt, sleeves and collar (the daura under its waistcoat).</summary>
            private uint DressColour
            {
                get { return _torso.Item == OutfitItem.None ? _skin : CharacterPalette.Colour(_torso.Item, _torso.Colour); }
            }

            private uint LegColour
            {
                get
                {
                    if (CharacterRecipe.IsFullLength(_torso.Item)) return DressColour;
                    if (_legs.Item == OutfitItem.None) return _torso.Item == OutfitItem.Kurta || _torso.Item == OutfitItem.Daura ? DressColour : 0x34383E;
                    return CharacterPalette.Colour(_legs.Item, _legs.Colour);
                }
            }

            private bool LongTop
            {
                get
                {
                    OutfitItem t = _torso.Item;
                    return t == OutfitItem.Kurta || t == OutfitItem.Daura || t == OutfitItem.Sari || t == OutfitItem.Robe;
                }
            }

            private void Torso()
            {
                float hip = _m.HipJointY, sh = _m.ShoulderY, T = _m.TorsoM;
                float hw = 0.5f * _m.HipW, sw = 0.5f * _m.ShoulderW, cd = 0.5f * _m.ChestDepth, belly = _m.Belly;
                // Key rings: (height, half width, half depth, z offset), bottom to top.
                var ys = new[] { hip - 0.075f, hip + 0.02f, hip + 0.3f * T, hip + 0.55f * T, hip + 0.8f * T, sh - 0.005f, sh + 0.035f };
                var rx = new[] { 0.36f * hw, 1.0f * hw, 0.92f * hw + 0.3f * belly, 0.88f * sw + 0.3f * belly, 0.92f * sw, 0.84f * sw, 0.36f * sw };
                var rz = new[] { 0.085f, 0.105f + 0.3f * belly, 0.1f + belly, cd * 0.95f + 0.6f * belly, cd, cd * 0.8f, 0.065f };
                var zo = new[] { 0.0f, -0.005f, 0.0f + 0.5f * belly, 0.01f + 0.3f * belly, 0.01f, -0.005f, -0.01f };
                // Where the top garment starts (below it the legs garment), and a darker hem band.
                OutfitItem t = TopItem;
                float hem = LongTop ? hip - 0.1f : t == OutfitItem.DhakaJacket ? hip + 0.0f : t == OutfitItem.None ? hip + 0.3f * T : hip + 0.06f;
                bool band = Lod < 2 && (t == OutfitItem.DhakaJacket || t == OutfitItem.Hoodie || t == OutfitItem.Uniform);
                uint top = TopColour, low = LegColour;
                uint bandRgb = t == OutfitItem.Uniform ? 0x2A2420u : CharacterPalette.Shade(top, 0.78f);
                int seg = _k.Seg(16, 6);
                float[] extras = band ? new[] { hem, hem + 0.045f } : new[] { hem };
                _k.BeginLoft();
                int key = 0;
                float y = ys[0];
                while (true)
                {
                    // Emit key rings with the hem and band inserted in order.
                    float next = key < ys.Length ? ys[key] : float.MaxValue;
                    for (int e = 0; e < extras.Length; e++)
                    {
                        float hy = extras[e];
                        if (hy > y - 1e-4f && hy < next - 1e-3f && hy > ys[0] + 1e-3f)
                        {
                            bool isBandTop = band && e == 1;
                            uint below = isBandTop ? bandRgb : low, above = isBandTop ? top : band ? bandRgb : top;
                            TorsoRing(ys, rx, rz, zo, hy - 0.002f, below);
                            TorsoRing(ys, rx, rz, zo, hy, above);
                        }
                    }
                    if (key >= ys.Length) break;
                    y = ys[key];
                    uint col = y < hem ? low : band && y < hem + 0.045f ? bandRgb : top;
                    TorsoRing(ys, rx, rz, zo, y, col);
                    key++;
                }
                _k.EndLoft(seg, Lod >= 2 ? LoftCap.Flat : LoftCap.Round, LoftCap.None);
                TorsoExtras(hip, sh, T);
                if (LongTop) Skirt(hip, T);
            }

            private void TorsoRing(float[] ys, float[] rx, float[] rz, float[] zo, float y, uint rgb)
            {
                int i = 0;
                while (i + 2 < ys.Length && y > ys[i + 1]) i++;
                float t = ys[i + 1] > ys[i] ? (y - ys[i]) / (ys[i + 1] - ys[i]) : 0f;
                t = CharMath.Clamp01(t);
                float x = CharMath.Lerp(rx[i], rx[i + 1], t), z = CharMath.Lerp(rz[i], rz[i + 1], t), off = CharMath.Lerp(zo[i], zo[i + 1], t);
                float h = (y - ys[0]) / (ys[ys.Length - 1] - ys[0]);
                Bone b0, b1;
                float w0;
                if (h < 0.3f)
                {
                    b0 = Bone.Hips;
                    b1 = Bone.Spine;
                    w0 = 1f - 0.5f * h / 0.3f;
                }
                else if (h < 0.65f)
                {
                    b0 = Bone.Spine;
                    b1 = Bone.Chest;
                    w0 = 1f - 0.5f * (h - 0.3f) / 0.35f;
                }
                else
                {
                    b0 = Bone.Chest;
                    b1 = h > 0.95f ? Bone.Neck : Bone.Chest;
                    w0 = h > 0.95f ? 0.7f : 1f;
                }
                _k.FlatRing(new V3(0f, y, off), x, z, rgb, b0, b1, w0, 2.6f);
            }

            /// <summary>Front surface of the torso at height <paramref name="y"/> (for collars, zips and panels).</summary>
            private float ChestFrontZ(float y)
            {
                float hip = _m.HipJointY, T = _m.TorsoM, cd = 0.5f * _m.ChestDepth, belly = _m.Belly;
                float t = CharMath.Clamp01((y - hip) / T);
                float front = t < 0.55f ? CharMath.Lerp(0.1f + belly, cd * 0.95f + 0.6f * belly + 0.01f, t / 0.55f) : CharMath.Lerp(cd + 0.01f, cd * 0.8f, (t - 0.55f) / 0.45f);
                return front + 0.004f;
            }

            private void TorsoExtras(float hip, float sh, float T)
            {
                OutfitItem t = TopItem;
                uint dress = DressColour, top = TopColour;
                int seg = _k.Seg(14, 6);
                // Collars: a closed round neck (daura, kurta band collar), a jacket collar, a T-shirt neckline.
                if (t != OutfitItem.None && t != OutfitItem.Robe && Lod < 2)
                {
                    uint collar = t == OutfitItem.Daura || t == OutfitItem.Kurta ? dress : CharacterPalette.Shade(top, 0.8f);
                    float h = t == OutfitItem.DhakaJacket || t == OutfitItem.Daura || t == OutfitItem.Uniform ? 0.04f : 0.018f;
                    _k.BeginLoft();
                    _k.FlatRing(new V3(0f, sh - 0.005f, -0.008f), 0.072f, 0.064f, collar, Bone.Chest, Bone.Neck, 0.7f);
                    _k.FlatRing(new V3(0f, sh - 0.005f + h, -0.006f), 0.064f, 0.058f, collar, Bone.Neck, Bone.Chest, 0.8f);
                    _k.EndLoft(seg, LoftCap.None, LoftCap.None);
                }
                if (Lod >= 2) return;
                switch (t)
                {
                    case OutfitItem.DhakaJacket:
                    {
                        // Zip, two dhaka yoke panels and two patterned pockets (30% of the area carries the pattern).
                        float y0 = hip + 0.02f, y1 = sh - 0.01f;
                        _k.Ellipsoid(new V3(0f, 0.5f * (y0 + y1), ChestFrontZ(0.5f * (y0 + y1)) + 0.002f), new V3(0.006f, 0.5f * (y1 - y0), 0.004f),
                                     Quat.Identity, CharacterPalette.Zip, 4, _k.Seg(6, 3), Bone.Chest, 2f, Bone.Spine, 0.6f);
                        uint p0 = CharacterPalette.TopiPatterns[0], p1 = CharacterPalette.TopiBase[0];
                        for (int s = -1; s <= 1; s += 2)
                        {
                            DhakaPanel(new V3(s * 0.085f, sh - 0.075f, 0f), 0.11f, 0.06f, p1, p0, Bone.Chest);
                            if (Lod == 0) DhakaPanel(new V3(s * 0.09f, hip + 0.1f, 0f), 0.08f, 0.05f, p1, p0, Bone.Spine);
                        }
                        break;
                    }
                    case OutfitItem.Hoodie:
                        _k.Ellipsoid(new V3(0f, sh + 0.0f, -0.1f), new V3(0.13f, 0.06f, 0.075f), Quat.Euler(-20f, 0f, 0f), CharacterPalette.Shade(top, 0.9f),
                                     _k.Seg(10, 5), _k.Seg(6, 3), Bone.Chest);
                        _k.Ellipsoid(new V3(0f, hip + 0.14f, ChestFrontZ(hip + 0.14f)), new V3(0.1f, 0.05f, 0.012f), Quat.Identity,
                                     CharacterPalette.Shade(top, 0.88f), _k.Seg(8, 4), 4, Bone.Spine);
                        for (int s = -1; s <= 1; s += 2)
                            _k.Capsule(new V3(s * 0.035f, sh - 0.02f, ChestFrontZ(sh - 0.02f)), new V3(s * 0.038f, sh - 0.13f, ChestFrontZ(sh - 0.13f) + 0.004f),
                                       0.0045f, 0.0045f, CharacterPalette.Lace, Bone.Chest, Bone.Chest, 4, 2, false, LoftCap.None, LoftCap.None);
                        break;
                    case OutfitItem.Kurta:
                    case OutfitItem.Uniform:
                    {
                        // Placket (kurta) or a button line (shirt).
                        float y0 = t == OutfitItem.Kurta ? sh - 0.2f : hip + 0.05f, y1 = sh - 0.01f;
                        _k.Ellipsoid(new V3(0f, 0.5f * (y0 + y1), ChestFrontZ(0.5f * (y0 + y1)) + 0.002f), new V3(0.012f, 0.5f * (y1 - y0), 0.004f),
                                     Quat.Identity, CharacterPalette.Shade(DressColour, 0.85f), 4, _k.Seg(6, 3), Bone.Chest);
                        break;
                    }
                    case OutfitItem.Daura:
                        DauraDetails(hip, sh);
                        break;
                    case OutfitItem.Sari:
                    case OutfitItem.Robe:
                        Sash(hip, sh, t == OutfitItem.Sari);
                        break;
                }
            }

            /// <summary>A small panel of faceted quads with the dhaka diamond lattice, on the torso front.</summary>
            private void DhakaPanel(V3 centre, float w, float h, uint baseRgb, uint dot, Bone bone)
            {
                int nx = Lod == 0 ? 4 : 2, ny = Lod == 0 ? 3 : 2;
                for (int i = 0; i < nx; i++)
                    for (int j = 0; j < ny; j++)
                    {
                        float x0 = centre.X - 0.5f * w + w * i / nx, x1 = x0 + w / nx;
                        float y0 = centre.Y - 0.5f * h + h * j / ny, y1 = y0 + h / ny;
                        float z0 = ChestFrontZ(y0) + 0.006f, z1 = ChestFrontZ(y1) + 0.006f;
                        uint col = (i + j) % 2 == 0 ? dot : baseRgb;
                        _k.Quad(new V3(x0, y0, z0), new V3(x1, y0, z0), new V3(x1, y1, z1), new V3(x0, y1, z1), V3.Forward, col, bone);
                    }
            }

            /// <summary>The daura's crossing wrap (left over right), its 8 tie strings in 4 pairs (two at the neck and
            /// shoulder, two at the waist, on the wearer's right) and the waistcoat's open front.</summary>
            private void DauraDetails(float hip, float sh)
            {
                uint dress = DressColour, tie = CharacterPalette.Shade(dress, 0.75f);
                // The daura shows in a V between the waistcoat fronts.
                float yTop = sh - 0.01f, yLow = hip + 0.12f;
                _k.Ellipsoid(new V3(0f, 0.5f * (yTop + yLow), ChestFrontZ(0.5f * (yTop + yLow)) + 0.001f), new V3(0.035f, 0.5f * (yTop - yLow), 0.004f),
                             Quat.Identity, dress, _k.Seg(6, 4), _k.Seg(6, 3), Bone.Chest, 2f, Bone.Spine, 0.6f);
                // Wrap edge from the neck down to the wearer's right side (left panel over right).
                V3 wa = new V3(0.015f, sh - 0.03f, ChestFrontZ(sh - 0.03f) + 0.005f), wb = new V3(-0.11f, hip + 0.08f, ChestFrontZ(hip + 0.08f) + 0.003f);
                _k.Capsule(wa, wb, 0.004f, 0.004f, tie, Bone.Chest, Bone.Spine, 4, 2, false, LoftCap.None, LoftCap.None);
                // Four tie pairs: a knot and two strings each.
                var knots = new[]
                {
                    new V3(-0.045f, sh - 0.035f, 0f), new V3(-0.1f, sh - 0.11f, 0f), new V3(-0.115f, hip + 0.16f, 0f), new V3(-0.12f, hip + 0.08f, 0f),
                };
                for (int i = 0; i < knots.Length; i++)
                {
                    V3 k0 = knots[i];
                    k0.Z = ChestFrontZ(k0.Y) + 0.004f;
                    Bone bone = i < 2 ? Bone.Chest : Bone.Spine;
                    _k.Sphere(k0, 0.009f, tie, 5, 3, bone);
                    for (int s = 0; s < 2; s++)
                    {
                        V3 end = k0 + new V3(s == 0 ? -0.012f : 0.006f, -0.045f, 0.004f);
                        _k.Capsule(k0, end, 0.0035f, 0.003f, tie, bone, bone, 4, 2, false, LoftCap.None, LoftCap.None);
                    }
                }
            }

            /// <summary>The sari's pallu over the left shoulder (border coloured) or a monk's robe sash.</summary>
            private void Sash(float hip, float sh, bool sari)
            {
                uint body = DressColour;
                uint border = sari ? CharacterPalette.SariBorder[_torso.Colour == 7 ? 0 : _torso.Pattern % CharacterPalette.SariBorder.Length] : CharacterPalette.Shade(body, 0.85f);
                var a = new V3(-0.13f, sh + 0.02f, -0.02f);
                var b = new V3(0.12f, hip + 0.05f, ChestFrontZ(hip + 0.05f) + 0.01f);
                var mid = new V3(-0.02f, 0.5f * (sh + hip) + 0.05f, ChestFrontZ(0.5f * (sh + hip) + 0.05f) + 0.015f);
                _k.BeginLoft();
                _k.Ring(a, mid - a, new V3(0f, 0f, 1f), 0.07f, 0.012f, body, Bone.Chest, Bone.Chest, 1f);
                _k.Ring(mid, b - a, new V3(0f, 0f, 1f), 0.075f, 0.012f, body, Bone.Chest, Bone.Spine, 0.6f);
                _k.Ring(b, b - mid, new V3(0f, 0f, 1f), 0.07f, 0.012f, sari ? border : body, Bone.Spine, Bone.Hips, 0.7f);
                _k.EndLoft(_k.Seg(8, 4), LoftCap.Flat, LoftCap.Flat);
                if (!sari) return;
                // The pallu's end falls down the back from the left shoulder.
                var c = new V3(-0.12f, sh + 0.01f, -0.09f);
                var d = new V3(-0.1f, hip + 0.05f, -0.14f);
                _k.BeginLoft();
                _k.Ring(c, d - c, V3.Right, 0.08f, 0.012f, body, Bone.Chest, Bone.Chest, 1f);
                _k.Ring(V3.Lerp(c, d, 0.85f), d - c, V3.Right, 0.085f, 0.012f, body, Bone.Spine, Bone.Chest, 0.7f);
                _k.Ring(V3.Lerp(c, d, 0.86f), d - c, V3.Right, 0.085f, 0.012f, border, Bone.Spine, Bone.Chest, 0.7f);
                _k.Ring(d, d - c, V3.Right, 0.09f, 0.012f, border, Bone.Spine, Bone.Hips, 0.6f);
                _k.EndLoft(_k.Seg(8, 4), LoftCap.Flat, LoftCap.Flat);
            }

            /// <summary>The hem of a long garment: from the waist to the knee (kurta, daura with 5 pleats) or the ankle
            /// (sari with its border, robe), weighted to the hips and the skirt bones.</summary>
            private void Skirt(float hip, float T)
            {
                OutfitItem t = TopItem;
                uint dress = DressColour;
                float knee = _m.AnkleY + _m.ShinM;
                float hemY = t == OutfitItem.Kurta ? knee + 0.02f : t == OutfitItem.Daura ? knee - 0.02f : _m.AnkleY + 0.015f;
                float waist = hip + 0.18f * T;
                float hw = 0.5f * _m.HipW;
                bool ankle = hemY < knee - 0.1f;
                float flare = ankle ? 1.18f : 1.12f;
                byte pleats = (byte)(t == OutfitItem.Daura ? DauraPleats : 0);
                uint border = t == OutfitItem.Sari ? CharacterPalette.SariBorder[_torso.Colour == 7 ? 0 : _torso.Pattern % CharacterPalette.SariBorder.Length] : dress;
                int rings = _k.Seg(6, 3);
                _k.BeginLoft();
                _k.FlatRing(new V3(0f, waist, 0f), hw * 0.98f + 0.012f + 0.3f * _m.Belly, 0.105f + _m.Belly + 0.012f, dress, Bone.Spine, Bone.Hips, 0.5f, 2.6f);
                _k.FlatRing(new V3(0f, hip, -0.002f), hw + 0.016f, 0.115f + 0.3f * _m.Belly, dress, Bone.Hips, Bone.Hips, 1f, 2.6f);
                for (int i = 1; i <= rings; i++)
                {
                    float f = i / (float)rings;
                    float y = hip + (hemY - hip) * f;
                    float sx = (hw + 0.02f) * (1f + (flare - 1f) * f), sz = (0.12f + 0.3f * _m.Belly) * (1f + (flare - 1f) * f) + (ankle ? 0.01f : 0f);
                    float w0 = 1f - 0.7f * f;
                    bool last = i == rings;
                    if (last && border != dress)
                    {
                        float yb = y + 0.05f;
                        _k.FlatRing(new V3(0f, yb + 0.002f, 0f), sx * 0.995f, sz * 0.995f, dress, Bone.Hips, Bone.Hips, w0, 2.3f, true, pleats);
                        _k.FlatRing(new V3(0f, yb, 0f), sx * 0.996f, sz * 0.996f, border, Bone.Hips, Bone.Hips, w0, 2.3f, true, pleats);
                    }
                    _k.FlatRing(new V3(0f, y, 0f), sx, sz, last ? border : dress, Bone.Hips, Bone.Hips, w0, 2.3f, true, pleats);
                }
                _k.EndLoft(_k.Seg(18, 6), LoftCap.None, LoftCap.None);
            }

            // ----- Limbs ------------------------------------------------------------------------------------------

            /// <summary>A limb segment as a loft: the covered part (garment colour, a little fatter) up to
            /// <paramref name="cover"/> of its length, then the bare part, with a crisp edge between.</summary>
            private void Limb(V3 a, V3 b, float ra, float rb, float cover, uint coverRgb, uint bareRgb, float extra, Bone bone, Bone parent,
                              bool bulge, LoftCap startCap, LoftCap endCap, float[] radiusCurve = null, uint cuffRgb = 0)
            {
                int seg = _k.Seg(10, 4), rings = Lod >= 2 ? 2 : _k.Seg(6, 3);
                if (Lod >= 2)
                {
                    // The crowd body: joints hide the ends (shoulders in the torso, knees in the bulges, ankles in shoes).
                    startCap = LoftCap.None;
                    endCap = LoftCap.None;
                    cuffRgb = 0;
                }
                V3 dir = b - a;
                V3 side = V3.Right;
                _k.BeginLoft();
                cover = CharMath.Clamp01(cover);
                for (int i = 0; i < rings; i++)
                {
                    float t = i / (float)(rings - 1);
                    if (cover > 0.01f && cover < 0.99f && i > 0)
                    {
                        float tp = (i - 1) / (float)(rings - 1);
                        if (tp < cover && t >= cover)
                        {
                            // Cuff edge: covered then bare at the same place.
                            RingAt(a, dir, side, ra, rb, cover - 0.001f, true, coverRgb, bareRgb, extra, bone, parent, bulge, radiusCurve);
                            if (cuffRgb != 0) RingAt(a, dir, side, ra, rb, cover - 0.0005f, true, cuffRgb, bareRgb, extra, bone, parent, bulge, radiusCurve);
                            RingAt(a, dir, side, ra, rb, cover, false, coverRgb, bareRgb, extra, bone, parent, bulge, radiusCurve);
                        }
                    }
                    RingAt(a, dir, side, ra, rb, t, t < cover || cover >= 0.999f, coverRgb, bareRgb, extra, bone, parent, bulge, radiusCurve);
                }
                _k.EndLoft(seg, startCap, endCap);
            }

            private void RingAt(V3 a, V3 dir, V3 side, float ra, float rb, float t, bool covered, uint coverRgb, uint bareRgb, float extra, Bone bone,
                                Bone parent, bool bulge, float[] radiusCurve)
            {
                float r = ra + (rb - ra) * t;
                if (radiusCurve != null)
                {
                    float f = t * (radiusCurve.Length - 1);
                    int i = Math.Min(radiusCurve.Length - 2, (int)f);
                    r *= CharMath.Lerp(radiusCurve[i], radiusCurve[i + 1], f - i);
                }
                if (bulge && t > 0.8f) r *= 1f + 0.15f * MathF.Sin((t - 0.8f) / 0.2f * 1.5707963f);
                if (covered) r += extra;
                float w = t < 0.25f ? 0.5f + 2f * t : 1f;
                _k.Ring(a + dir * t, dir, side, r, r, covered ? coverRgb : bareRgb, bone, parent, w);
            }

            private void Arms()
            {
                OutfitItem t = TopItem;
                float upperCover, foreCover;
                switch (t)
                {
                    case OutfitItem.None:
                        upperCover = 0f;
                        foreCover = 0f;
                        break;
                    case OutfitItem.TShirt:
                        upperCover = 0.45f;
                        foreCover = 0f;
                        break;
                    case OutfitItem.Sari:
                        upperCover = 0.35f;
                        foreCover = 0f;
                        break;
                    case OutfitItem.Robe:
                        upperCover = 0.6f;
                        foreCover = 0f;
                        break;
                    case OutfitItem.Kurta:
                        upperCover = 1f;
                        foreCover = 0.75f;
                        break;
                    default:
                        upperCover = 1f;
                        foreCover = 0.93f;
                        break;
                }
                uint sleeve = t == OutfitItem.Daura || t == OutfitItem.Sari ? CharacterPalette.Shade(DressColour, t == OutfitItem.Sari ? 0.8f : 1f) : TopColour;
                uint cuff = t == OutfitItem.DhakaJacket || t == OutfitItem.Hoodie ? CharacterPalette.Shade(sleeve, 0.78f) : 0u;
                float s0 = 0.056f * (_m.ShoulderW / 0.42f);
                for (int side = 0; side < 2; side++)
                {
                    float s = side == 0 ? -1f : 1f;
                    int o = side * 4;
                    V3 shoulder = _sk.BindPosition[8 + o], elbow = _sk.BindPosition[9 + o], wrist = _sk.BindPosition[10 + o];
                    Bone upper = (Bone)(8 + o), fore = (Bone)(9 + o), hand = (Bone)(10 + o);
                    // A round shoulder joins the arm to the torso.
                    if (Lod < 2)
                        _k.Sphere(shoulder + new V3(-s * 0.005f, 0.005f, 0f), s0 + (upperCover > 0f ? 0.01f : 0f), upperCover > 0f ? sleeve : _skin,
                              _k.Seg(10, 5), _k.Seg(6, 4), Bone.Chest);
                    Limb(shoulder, elbow, s0, 0.045f, upperCover, sleeve, _skin, 0.012f, upper, Bone.Chest, true, LoftCap.None, LoftCap.None);
                    Limb(elbow, wrist + new V3(0f, 0.01f, 0f), 0.047f, 0.037f, foreCover, sleeve, _skin, 0.012f, fore, upper, false, LoftCap.Round, LoftCap.None,
                         null, cuff);
                    // Mitten hand and thumb (palms face the body; the thumb points forward).
                    float hs = _m.HandM / 0.13f;
                    _k.Ellipsoid(wrist + new V3(s * 0.003f, -0.055f * hs, 0.004f), new V3(0.024f, 0.062f * hs, 0.045f), Quat.Identity, _skin,
                                 _k.Seg(10, 4), _k.Seg(6, 3), hand, 2.2f, (Bone)(17 + o), 0.65f);
                    if (Lod < 2)
                        _k.Capsule(wrist + new V3(0f, -0.025f, 0.03f), wrist + new V3(s * -0.005f, -0.065f, 0.052f), 0.015f, 0.013f, _skin,
                               (Bone)(15 + o), hand, _k.Seg(6, 4), _k.Seg(3, 2), false);
                }
            }

            private void Legs()
            {
                OutfitItem t = TopItem, l = _legs.Item;
                if (CharacterRecipe.IsFullLength(t)) return; // the hem reaches the ankles; the legs move inside
                float thighCover = 1f, shinCover = 1f;
                float[] curveT = null, curveS = null;
                uint col = LegColour, cuff = 0u;
                switch (l)
                {
                    case OutfitItem.None:
                        if (t != OutfitItem.Kurta && t != OutfitItem.Daura)
                        {
                            thighCover = 0.35f;
                            shinCover = 0f;
                        }
                        break;
                    case OutfitItem.Shorts:
                        thighCover = 0.55f;
                        shinCover = 0f;
                        break;
                    case OutfitItem.Suruwal:
                        curveT = new[] { 1.35f, 1.3f, 1.1f };
                        curveS = new[] { 1.1f, 0.95f, 0.85f };
                        break;
                    case OutfitItem.Joggers:
                        cuff = CharacterPalette.Shade(col, 0.75f);
                        shinCover = 0.95f;
                        break;
                }
                if (t == OutfitItem.Kurta || t == OutfitItem.Daura)
                {
                    // Suruwal under a long top: fitted at the ankle.
                    curveT = curveT ?? new[] { 1.25f, 1.2f, 1.05f };
                    curveS = curveS ?? new[] { 1.05f, 0.92f, 0.85f };
                }
                for (int side = 0; side < 2; side++)
                {
                    int o = side * 4;
                    V3 hipJ = _sk.BindPosition[23 + o], knee = _sk.BindPosition[24 + o], ankle = _sk.BindPosition[25 + o];
                    Bone thigh = (Bone)(23 + o), shin = (Bone)(24 + o);
                    float legScale = _m.ShinM / 0.27f;
                    Limb(hipJ + new V3(0f, 0.03f, 0f), knee, 0.076f * (_m.HipW / 0.34f), 0.054f, thighCover, col, _skin, 0.008f, thigh, Bone.Hips, true,
                         LoftCap.Round, LoftCap.None, curveT);
                    Limb(knee, ankle + new V3(0f, 0.02f, 0f), 0.054f * Math.Min(1.05f, legScale), 0.04f, shinCover, col, _skin, 0.008f, shin, thigh, false,
                         LoftCap.Round, LoftCap.Round, curveS, cuff);
                }
            }

            private void Feet()
            {
                OutfitItem f = _feet.Item;
                uint upper = CharacterPalette.Colour(f, _feet.Colour, _skin);
                for (int side = 0; side < 2; side++)
                {
                    int o = side * 4;
                    V3 ankle = _sk.BindPosition[25 + o];
                    Bone foot = (Bone)(25 + o), toe = (Bone)(26 + o);
                    float x = ankle.X, z = ankle.Z;
                    // Shoes are 0.27 × 0.11 × 0.09 m with a 15° toe spring.
                    Quat spring = Quat.Euler(-4f, 0f, 0f);
                    if (Lod >= 2)
                    {
                        // The crowd body: one shoe or foot each.
                        uint shoe = f == OutfitItem.Barefoot || f == OutfitItem.None ? _skin : upper;
                        _k.Ellipsoid(new V3(x, 0.045f, z + 0.04f), new V3(0.052f, 0.045f, 0.13f), spring, shoe, 5, 3, foot, 2.4f, toe, 0.7f);
                        continue;
                    }
                    switch (f)
                    {
                        case OutfitItem.Chappal:
                            _k.Ellipsoid(new V3(x, 0.01f, z + 0.045f), new V3(0.05f, 0.01f, 0.13f), Quat.Identity, upper, _k.Seg(10, 5), 3, foot, 3f, toe, 0.7f);
                            _k.Ellipsoid(new V3(x, 0.045f, z + 0.04f), new V3(0.045f, 0.035f, 0.11f), spring, _skin, _k.Seg(10, 5), _k.Seg(6, 3), foot, 2.2f, toe, 0.65f);
                            if (Lod < 2)
                                _k.Ellipsoid(new V3(x, 0.07f, z + 0.07f), new V3(0.047f, 0.008f, 0.012f), Quat.Identity, upper, 6, 3, foot);
                            break;
                        case OutfitItem.Barefoot:
                        case OutfitItem.None:
                            _k.Ellipsoid(new V3(x, 0.035f, z + 0.04f), new V3(0.045f, 0.035f, 0.115f), spring, _skin, _k.Seg(10, 5), _k.Seg(6, 3), foot, 2.2f, toe, 0.65f);
                            break;
                        case OutfitItem.TrekBoots:
                            _k.Ellipsoid(new V3(x, 0.02f, z + 0.045f), new V3(0.058f, 0.02f, 0.135f), Quat.Identity, 0x3A2E2Au, _k.Seg(10, 5), 3, foot, 3.5f, toe, 0.7f);
                            _k.Ellipsoid(new V3(x, 0.07f, z + 0.03f), new V3(0.055f, 0.07f, 0.12f), spring, upper, _k.Seg(10, 5), _k.Seg(6, 4), foot, 2.4f, toe, 0.75f);
                            break;
                        default:
                            // Sneakers: a white sole, the upper and a lace stripe; a plain stitch, never a brand mark.
                            _k.Ellipsoid(new V3(x, 0.018f, z + 0.045f), new V3(0.056f, 0.018f, 0.135f), Quat.Identity, CharacterPalette.SoleWhite,
                                         _k.Seg(10, 5), 3, foot, 3.5f, toe, 0.7f);
                            _k.Ellipsoid(new V3(x, 0.057f, z + 0.035f), new V3(0.05f, 0.045f, 0.118f), spring, upper, _k.Seg(12, 5), _k.Seg(6, 4), foot, 2.4f, toe, 0.7f);
                            if (Lod < 2)
                                _k.Ellipsoid(new V3(x, 0.098f, z + 0.065f), new V3(0.018f, 0.006f, 0.045f), Quat.Euler(-14f, 0f, 0f), CharacterPalette.Lace,
                                             _k.Seg(6, 4), 3, foot, 2f, toe, 0.6f);
                            break;
                    }
                }
            }

            // ----- Back -------------------------------------------------------------------------------------------

            private void Back()
            {
                V3 attach = _sk.BindPosition[(int)Bone.BackAttach];
                uint rgb = CharacterPalette.Colour(_back.Item, _back.Colour);
                switch (_back.Item)
                {
                    case OutfitItem.Daypack:
                    {
                        // A rounded box 0.30 × 0.40 × 0.15 m with two shoulder straps.
                        _k.Ellipsoid(attach + new V3(0f, -0.04f, -0.085f), new V3(0.15f, 0.19f, 0.075f), Quat.Identity, rgb, _k.Seg(12, 6), _k.Seg(8, 4),
                                     Bone.BackAttach, 4f);
                        if (Lod >= 2) break;
                        _k.Ellipsoid(attach + new V3(0f, -0.1f, -0.16f), new V3(0.1f, 0.06f, 0.02f), Quat.Identity, CharacterPalette.Shade(rgb, 0.8f),
                                     _k.Seg(8, 4), 4, Bone.BackAttach, 3f);
                        float sh = _m.ShoulderY;
                        for (int s = -1; s <= 1; s += 2)
                        {
                            var p0 = new V3(s * 0.09f, sh - 0.03f, -0.12f);
                            var p1 = new V3(s * 0.1f, sh + 0.015f, -0.01f);
                            var p2 = new V3(s * 0.1f, sh - 0.2f, ChestFrontZ(sh - 0.2f) + 0.008f);
                            _k.Capsule(p0, p1, 0.013f, 0.013f, CharacterPalette.Strap, Bone.Chest, Bone.Chest, 4, 2, false, LoftCap.None, LoftCap.None);
                            _k.Capsule(p1, p2, 0.013f, 0.013f, CharacterPalette.Strap, Bone.Chest, Bone.Chest, 4, 2, false, LoftCap.None, LoftCap.Flat);
                        }
                        break;
                    }
                    case OutfitItem.Doko:
                    {
                        // A bamboo basket, wider at the top, woven in bands.
                        int bands = _k.Seg(6, 2);
                        _k.BeginLoft();
                        for (int i = 0; i <= bands; i++)
                        {
                            float t = i / (float)bands;
                            uint c = i % 2 == 0 ? rgb : CharacterPalette.Shade(rgb, 0.82f);
                            _k.FlatRing(attach + new V3(0f, -0.34f + 0.62f * t, -0.13f - 0.07f * t), 0.1f + 0.12f * t, 0.08f + 0.1f * t, c,
                                        Bone.BackAttach, Bone.BackAttach, 1f);
                        }
                        _k.EndLoft(_k.Seg(12, 6), LoftCap.Flat, LoftCap.None);
                        break;
                    }
                }
            }
        }
    }
}
