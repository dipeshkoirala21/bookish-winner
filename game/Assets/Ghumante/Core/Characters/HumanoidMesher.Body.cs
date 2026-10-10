using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Characters
{
    public static partial class HumanoidMesher
    {
        /// <summary>How the worn garments shape and colour the body (computed once per build).</summary>
        private struct GarmentPlan
        {
            /// <summary>Torso surface colour above the hem; the cloth under an open coat; the coat or waistcoat.</summary>
            public uint Top, Dress, Coat;

            /// <summary>What a daura is worn under: 0 nothing, 1 a waistcoat (istakot), 2 an open coat.</summary>
            public byte Layer;

            /// <summary>Legs garment colour (also the torso below the hem).</summary>
            public uint Lower;

            /// <summary>Hem height of the top garment on the torso, an optional rib band above it and its colour.</summary>
            public float HemY, BandH;
            public uint BandRgb;

            /// <summary>Extra bulk of the top garment (puffer jackets) and quilting baffles.</summary>
            public float Bulk;
            public bool Baffles;

            /// <summary>A long hem below the torso (kurta and daura to the knee; sari, robe, chuba to the ankle).</summary>
            public float SkirtHemY, SkirtFlare;
            public uint SkirtRgb, SkirtBorder;
            public byte Pleats;
            public bool SkirtSlits;

            /// <summary>Sleeve cover per side (0 left, 1 right) on the upper arm and forearm, sleeve colours, and the cuff:
            /// 0 none, 1 rib band, 2 rolled, 3 shirt cuff, 4 hem band.</summary>
            public float UpperL, UpperR, ForeL, ForeR;
            public uint SleeveL, SleeveR, CuffRgb;
            public byte Cuff;

            /// <summary>A belt at the waist and its colour.</summary>
            public bool Belt;
            public uint BeltRgb;

            /// <summary>Legs: thigh and shin cover, a looser thigh (suruwal), the leg cuff (0 none, 1 band, 2 rolled,
            /// 3 gathered ankle), and a front crease.</summary>
            public float ThighCover, ShinCover, ThighLoose, AnkleFit;
            public byte LegCuff;
            public bool Crease;
        }

        private sealed partial class Builder
        {
            private GarmentPlan _g;

            // Torso profile: key heights, half widths, half depths and forward offsets (bottom to top).
            private readonly float[] _ty = new float[7], _trx = new float[7], _trz = new float[7], _tzo = new float[7];

            // Scratch for ring heights and colours.
            private readonly float[] _ringY = new float[48];
            private readonly uint[] _ringC = new uint[48];

            private uint DressColour
            {
                get { return _torso.Item == OutfitItem.None ? _skin : CharacterPalette.Colour(_torso.Item, _torso.Colour); }
            }

            /// <summary>Plans the garments: colours, hems, sleeves, cuffs, belts, skirts and legs.</summary>
            private void Plan()
            {
                OutfitItem t = _torso.Item, l = _legs.Item;
                float hip = _m.HipJointY, T = _m.TorsoM;
                float knee = _m.AnkleY + _m.ShinM;
                var g = new GarmentPlan
                {
                    Dress = DressColour, UpperL = 1f, UpperR = 1f, ForeL = 0.93f, ForeR = 0.93f, HemY = hip + 0.05f, ThighCover = 1f, ShinCover = 1f,
                    ThighLoose = 1f, AnkleFit = 1f, BeltRgb = 0x2A2220u,
                };
                g.Top = g.Dress;
                g.Coat = g.Dress;
                switch (t)
                {
                    case OutfitItem.None:
                        g.Top = _skin;
                        g.UpperL = g.UpperR = g.ForeL = g.ForeR = 0f;
                        g.HemY = hip + 0.3f * T;
                        break;
                    case OutfitItem.TShirt:
                        g.UpperL = g.UpperR = 0.5f;
                        g.ForeL = g.ForeR = 0f;
                        g.Cuff = 4;
                        g.HemY = hip + 0.03f;
                        break;
                    case OutfitItem.Vest:
                        g.UpperL = g.UpperR = 0f;
                        g.ForeL = g.ForeR = 0f;
                        g.HemY = hip + 0.04f;
                        break;
                    case OutfitItem.Hoodie:
                        g.Cuff = 1;
                        g.HemY = hip + 0.0f;
                        g.BandH = 0.045f;
                        g.Bulk = 0.006f;
                        break;
                    case OutfitItem.DhakaJacket:
                        g.Cuff = 1;
                        g.HemY = hip + 0.02f;
                        g.BandH = 0.04f;
                        g.Bulk = 0.006f;
                        break;
                    case OutfitItem.Jacket:
                        g.Cuff = 1;
                        g.HemY = hip - 0.01f;
                        g.BandH = 0.03f;
                        g.Bulk = 0.018f;
                        g.Baffles = true;
                        break;
                    case OutfitItem.Fleece:
                        g.Cuff = 1;
                        g.HemY = hip + 0.01f;
                        g.BandH = 0.02f;
                        g.Bulk = 0.008f;
                        break;
                    case OutfitItem.Shirt:
                    case OutfitItem.Uniform:
                    {
                        bool office = _legs.Item == OutfitItem.Trousers && (_feet.Item == OutfitItem.LeatherShoes);
                        g.Cuff = office || Var(11u) < 0.4f ? (byte)3 : (byte)2;
                        if (g.Cuff == 2)
                        {
                            g.ForeL = g.ForeR = 0.35f; // sleeves rolled to the forearm
                        }
                        g.HemY = office ? hip + 0.16f : hip + 0.0f;
                        g.Belt = office;
                        break;
                    }
                    case OutfitItem.SchoolShirt:
                        g.Cuff = Var(12u) < 0.5f ? (byte)4 : (byte)3;
                        if (g.Cuff == 4)
                        {
                            g.UpperL = g.UpperR = 0.55f;
                            g.ForeL = g.ForeR = 0f;
                        }
                        g.HemY = hip + 0.16f;
                        g.Belt = true;
                        break;
                    case OutfitItem.PoliceUniform:
                        g.Cuff = 3;
                        g.HemY = hip + 0.16f;
                        g.Belt = true;
                        g.BeltRgb = 0x1A1A1Cu;
                        break;
                    case OutfitItem.Blazer:
                        g.Coat = g.Dress;
                        g.Top = g.Dress;
                        g.Dress = CharacterPalette.At(CharacterPalette.Shirt, (int)(_r.Seed % 3));
                        g.Cuff = 3;
                        g.CuffRgb = g.Dress;
                        g.HemY = hip - 0.07f;
                        g.Bulk = 0.008f;
                        break;
                    case OutfitItem.Kurta:
                        g.ForeL = g.ForeR = 0.72f;
                        g.Cuff = 4;
                        g.HemY = hip - 0.05f;
                        g.SkirtHemY = knee + 0.01f;
                        g.SkirtFlare = 1.14f;
                        g.SkirtRgb = g.Dress;
                        g.SkirtBorder = g.Dress;
                        g.SkirtSlits = true;
                        break;
                    case OutfitItem.Daura:
                    {
                        // The daura (light cloth) under a dark coat (half), a waistcoat (a quarter) or on its own with its ties
                        // showing (ref_characters.md §3).
                        bool coat = (_r.Seed & 2) == 0;
                        g.Layer = coat ? (byte)2 : (_r.Seed & 4) == 0 ? (byte)1 : (byte)0;
                        g.Coat = g.Layer == 0 ? g.Dress : CharacterPalette.At(CharacterPalette.Waistcoat, _torso.Pattern);
                        g.Top = g.Coat;
                        g.Cuff = coat ? (byte)3 : (byte)0;
                        g.CuffRgb = g.Dress;
                        g.HemY = hip - (coat ? 0.08f : 0.02f);
                        g.Bulk = 0.008f;
                        g.SkirtHemY = knee - 0.03f;
                        g.SkirtFlare = 1.1f;
                        g.SkirtRgb = g.Dress;
                        g.SkirtBorder = g.Dress;
                        g.Pleats = DauraPleats;
                        if (!coat)
                        {
                            g.SleeveL = g.SleeveR = g.Dress; // waistcoat: the daura's own sleeves show
                        }
                        break;
                    }
                    case OutfitItem.Sari:
                    {
                        bool haku = _torso.Colour == CharacterPalette.HakuSari;
                        // Haku patasi: a dark printed blouse with the draped pallu, or a red or coloured blouse with the patuka.
                        uint blouse = haku ? (_torso.Pattern % 2 == 1 ? CharacterPalette.HakuBlouse[0] : CharacterPalette.At(CharacterPalette.HakuBlouse, 1 + (int)(_r.Seed % 3)))
                                           : CharacterPalette.Shade(g.Dress, 0.82f);
                        g.Top = blouse;
                        g.UpperL = g.UpperR = haku ? 1f : 0.42f;
                        g.ForeL = g.ForeR = haku ? 0.85f : 0f;
                        g.Cuff = 4;
                        g.SleeveL = g.SleeveR = blouse;
                        g.HemY = hip + 0.18f * T;
                        g.SkirtHemY = haku ? _m.AnkleY + 0.11f : _m.AnkleY + 0.012f;
                        g.SkirtFlare = 1.12f;
                        g.SkirtRgb = g.Dress;
                        g.SkirtBorder = haku ? CharacterPalette.SariBorder[0] : CharacterPalette.At(CharacterPalette.SariBorder, _torso.Pattern);
                        g.Pleats = 9;
                        break;
                    }
                    case OutfitItem.Robe:
                    {
                        bool monk = _torso.Colour == 0;
                        if (monk)
                        {
                            // Maroon zen over the left shoulder, the yellow sleeveless dhonka; the right arm bare.
                            g.UpperL = 0.9f;
                            g.ForeL = 0.1f;
                            g.UpperR = 0.22f;
                            g.ForeR = 0f;
                            g.SleeveL = g.Dress;
                            g.SleeveR = CharacterPalette.Dhonka;
                            g.HemY = hip - 0.05f;
                        }
                        else
                        {
                            // Sadhu: saffron dhoti and a shawl over one shoulder; arms bare.
                            g.UpperL = 0.75f;
                            g.ForeL = 0f;
                            g.UpperR = 0f;
                            g.ForeR = 0f;
                            g.SleeveL = g.Dress;
                            g.Top = CharacterPalette.Mix(_skin, CharacterPalette.Ash, 0.28f);
                            g.HemY = hip + 0.12f;
                        }
                        g.SkirtHemY = _m.AnkleY + (monk ? 0.03f : 0.06f);
                        g.SkirtFlare = 1.12f;
                        g.SkirtRgb = g.Dress;
                        g.SkirtBorder = CharacterPalette.Shade(g.Dress, 0.8f);
                        g.Pleats = (byte)(monk ? 6 : 0);
                        break;
                    }
                    case OutfitItem.Chuba:
                    {
                        uint blouse = CharacterPalette.At(CharacterPalette.ChubaBlouse, _torso.Pattern);
                        g.SleeveL = g.SleeveR = blouse;
                        g.Cuff = 4;
                        g.CuffRgb = CharacterPalette.Shade(blouse, 0.9f);
                        g.HemY = hip - 0.05f;
                        g.SkirtHemY = _m.AnkleY + 0.02f;
                        g.SkirtFlare = 1.1f;
                        g.SkirtRgb = g.Dress;
                        g.SkirtBorder = g.Dress;
                        g.Belt = true;
                        g.BeltRgb = CharacterPalette.Shade(blouse, 0.85f);
                        break;
                    }
                }
                if (g.SleeveL == 0) g.SleeveL = g.Top;
                if (g.SleeveR == 0) g.SleeveR = g.Top;
                if (t == OutfitItem.Daura && g.Cuff == 3) g.SleeveL = g.SleeveR = g.Coat;
                if (t == OutfitItem.Blazer) g.SleeveL = g.SleeveR = g.Coat;
                if (g.CuffRgb == 0) g.CuffRgb = g.Cuff == 2 || g.Cuff == 3 ? CharacterPalette.Shade(g.SleeveR, 0.95f) : CharacterPalette.Shade(g.SleeveR, 0.8f);
                if (g.BandH > 0f) g.BandRgb = CharacterPalette.Shade(g.Top, 0.8f);

                // Legs.
                g.Lower = LegColour(t, l, g);
                if (!CharacterRecipe.IsFullLength(t))
                {
                    switch (l)
                    {
                        case OutfitItem.None:
                            if (t != OutfitItem.Kurta && t != OutfitItem.Daura)
                            {
                                g.ThighCover = 0.35f;
                                g.ShinCover = 0f;
                            }
                            break;
                        case OutfitItem.Shorts:
                            g.ThighCover = 0.6f;
                            g.ShinCover = 0f;
                            g.LegCuff = 1;
                            break;
                        case OutfitItem.Skirt:
                            g.ThighCover = 0f;
                            g.ShinCover = 0f;
                            g.SkirtHemY = knee + 0.03f;
                            g.SkirtFlare = 1.3f;
                            g.SkirtRgb = g.Lower;
                            g.SkirtBorder = g.Lower;
                            g.Pleats = 12;
                            break;
                        case OutfitItem.Suruwal:
                            g.ThighLoose = t == OutfitItem.Daura ? 1.38f : t == OutfitItem.Kurta ? 1.1f : 1.3f;
                            g.AnkleFit = 0.82f;
                            g.LegCuff = 3;
                            break;
                        case OutfitItem.Joggers:
                            g.LegCuff = 1;
                            g.AnkleFit = 0.85f;
                            break;
                        case OutfitItem.RolledTrousers:
                            g.ShinCover = 0.55f;
                            g.LegCuff = 2;
                            break;
                        case OutfitItem.Trousers:
                            g.Crease = true;
                            break;
                        case OutfitItem.Jeans:
                        case OutfitItem.TrekTrousers:
                            break;
                    }
                    if ((l == OutfitItem.Jeans || l == OutfitItem.Trousers || l == OutfitItem.TrekTrousers) && g.HemY > hip + 0.1f) g.Belt = true;
                }
                _g = g;
            }

            private uint LegColour(OutfitItem t, OutfitItem l, in GarmentPlan g)
            {
                if (CharacterRecipe.IsFullLength(t)) return g.SkirtRgb != 0 ? g.SkirtRgb : g.Dress;
                if (t == OutfitItem.PoliceUniform && (l == OutfitItem.Trousers || l == OutfitItem.None)) return CharacterPalette.PoliceTrousers;
                if (l == OutfitItem.None) return t == OutfitItem.Kurta || t == OutfitItem.Daura ? g.Dress : 0x34383Eu;
                if (l == OutfitItem.Suruwal && t == OutfitItem.Daura) return g.Dress;
                if (l == OutfitItem.Suruwal && t == OutfitItem.Kurta) return CharacterPalette.At(CharacterPalette.Suruwal, _torso.Pattern);
                return CharacterPalette.Colour(l, _legs.Colour);
            }

            // ----- Neck ---------------------------------------------------------------------------------------------

            private void Neck()
            {
                Mat(MaterialChannel.Skin);
                float r = 0.052f * (_m.HeadW / 0.36f);
                var a = new V3(0f, _m.ShoulderY - 0.03f, -0.012f);
                var b = new V3(0f, _m.HeadBaseY + 0.07f * _hs, 0.0f);
                _k.Capsule(a, b, r, r * 0.92f, _skin, Bone.Neck, Bone.Chest, _k.Seg(12, 6), Lod == 0 ? 4 : 2, false, LoftCap.None, LoftCap.None);
            }

            // ----- Torso --------------------------------------------------------------------------------------------

            private void TorsoProfile()
            {
                float hip = _m.HipJointY, sh = _m.ShoulderY, T = _m.TorsoM;
                float hw = 0.5f * _m.HipW, sw = 0.5f * _m.ShoulderW, cd = 0.5f * _m.ChestDepth, belly = _m.Belly;
                bool soft = _r.Figure != 0;
                float bulk = _g.Bulk;
                float child = _r.Age == AgeGroup.Child ? 0.9f : 1f;
                _ty[0] = hip - 0.045f;
                _ty[1] = hip + 0.02f;
                _ty[2] = hip + 0.3f * T;
                _ty[3] = hip + 0.55f * T;
                _ty[4] = hip + 0.78f * T;
                _ty[5] = sh - 0.005f;
                _ty[6] = sh + 0.035f;
                _trx[0] = 0.55f * hw;
                _trx[1] = hw * (soft ? 1.02f : 1f);
                _trx[2] = (0.92f * hw + 0.3f * belly) * (soft ? 0.9f : 1f);
                _trx[3] = 0.88f * sw + 0.3f * belly;
                _trx[4] = 0.93f * sw;
                _trx[5] = 0.84f * sw;
                _trx[6] = 0.36f * sw;
                _trz[0] = 0.075f;
                _trz[1] = 0.105f + 0.3f * belly;
                _trz[2] = 0.1f + belly;
                _trz[3] = cd * 0.95f + 0.6f * belly;
                _trz[4] = cd + (soft ? 0.012f : 0f);
                _trz[5] = cd * 0.8f;
                _trz[6] = 0.065f;
                _tzo[0] = 0f;
                _tzo[1] = -0.005f;
                _tzo[2] = 0.5f * belly;
                _tzo[3] = 0.01f + 0.3f * belly;
                _tzo[4] = 0.01f + (soft ? 0.014f : 0f);
                _tzo[5] = -0.005f;
                _tzo[6] = -0.01f;
                for (int i = 0; i < 7; i++)
                {
                    _trz[i] *= child;
                    if (i >= 1 && i <= 5)
                    {
                        _trx[i] += bulk;
                        _trz[i] += bulk;
                    }
                }
            }

            /// <summary>Torso half width, half depth and forward offset at height <paramref name="y"/> (smooth).</summary>
            private void TorsoAt(float y, out float rx, out float rz, out float zo)
            {
                int i = 0;
                while (i + 2 < _ty.Length && y > _ty[i + 1]) i++;
                float t = _ty[i + 1] > _ty[i] ? CharMath.Clamp01((y - _ty[i]) / (_ty[i + 1] - _ty[i])) : 0f;
                float s = CharMath.SmoothStep(t);
                float u = 0.5f * (t + s); // between linear and smooth: keeps the shape but rounds the joins
                rx = CharMath.Lerp(_trx[i], _trx[i + 1], u);
                rz = CharMath.Lerp(_trz[i], _trz[i + 1], u);
                zo = CharMath.Lerp(_tzo[i], _tzo[i + 1], u);
            }

            private const float TorsoExp = 2.6f;

            /// <summary>The torso surface at angle <paramref name="a"/> (0 front, +π/2 the wearer's right) and height
            /// <paramref name="y"/>, lifted along the normal.</summary>
            private V3 TorsoPoint(float a, float y, float lift, out V3 n)
            {
                V3 p = TorsoRaw(a, y);
                V3 pa = TorsoRaw(a + 0.01f, y), py = TorsoRaw(a, y + 0.003f);
                n = V3.Cross(py - p, pa - p).Normalized;
                V3 radial = new V3(p.X, 0f, p.Z - CenterZ(y));
                if (V3.Dot(n, radial) < 0f) n = -n;
                if (n.LengthSq < 0.5f) n = radial.Normalized;
                return p + n * lift;
            }

            private float CenterZ(float y)
            {
                TorsoAt(y, out float rx, out float rz, out float zo);
                return zo;
            }

            private V3 TorsoRaw(float a, float y)
            {
                TorsoAt(y, out float rx, out float rz, out float zo);
                float pe = 2f / TorsoExp;
                return new V3(rx * BodyKit.SPow(MathF.Sin(a), pe), y, zo + rz * BodyKit.SPow(MathF.Cos(a), pe));
            }

            private void TorsoBones(float y, out Bone b0, out Bone b1, out float w0)
            {
                float h = (y - _ty[0]) / (_ty[_ty.Length - 1] - _ty[0]);
                if (h < 0.3f)
                {
                    b0 = Bone.Hips;
                    b1 = Bone.Spine;
                    w0 = 1f - 0.5f * CharMath.Clamp01(h / 0.3f);
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
            }

            private void Torso()
            {
                Plan();
                TorsoProfile();
                Mat(MaterialChannel.Fabric);
                float hip = _m.HipJointY;
                OutfitItem t = _torso.Item;
                // Ring heights: the keys, midpoints at LOD0, and doubled rings at every colour edge.
                int n = 0;
                int sub = Lod == 0 ? 2 : 1;
                for (int i = 0; i < _ty.Length; i++)
                {
                    if (i > 0)
                        for (int k = 1; k < sub; k++) n = AddRingY(n, _ty[i - 1] + (_ty[i] - _ty[i - 1]) * k / sub);
                    if (Lod >= 2 && (i == 1 || i == 3)) continue; // the far body keeps the silhouette rings only
                    n = AddRingY(n, _ty[i]);
                }
                if (_g.Baffles && Lod == 0)
                    for (float y = _g.HemY + _g.BandH + 0.06f; y < _ty[5] - 0.02f; y += 0.075f) n = AddRingY(n, y);
                float hem = _g.HemY, band = _g.BandH;
                if (Lod < 2)
                {
                    n = AddEdge(n, hem);
                    if (band > 0f) n = AddEdge(n, hem + band);
                }
                else n = AddEdge(n, hem);
                Array.Sort(_ringY, 0, n);
                _k.BeginLoft();
                for (int i = 0; i < n; i++)
                {
                    float y = _ringY[i];
                    bool edge = i + 1 < n && MathF.Abs(_ringY[i + 1] - y) < 0.0015f;
                    uint col = TorsoColour(edge ? y - 0.003f : y + 0.0005f);
                    TorsoRing(y, col);
                }
                _k.EndLoft(_k.Seg(20, 6), Lod >= 2 ? LoftCap.Flat : LoftCap.Round, LoftCap.None);
                if (Lod < 2 && band > 0f)
                {
                    // The rib band stands a little proud of the body.
                    BandRing(hem + 0.004f, hem + band - 0.004f, 0.006f, _g.BandRgb);
                }
                if (_g.SkirtHemY > 0f) Skirt();
                if (_g.Belt) Belt();
                TorsoDetails();
            }

            private int AddRingY(int n, float y)
            {
                if (n >= _ringY.Length) return n;
                if (y < _ty[0] - 1e-4f || y > _ty[_ty.Length - 1] + 1e-4f) return n;
                for (int i = 0; i < n; i++)
                    if (MathF.Abs(_ringY[i] - y) < 0.004f) return n;
                _ringY[n] = y;
                return n + 1;
            }

            /// <summary>Adds a crisp colour edge (two rings 1 mm apart) at <paramref name="y"/>.</summary>
            private int AddEdge(int n, float y)
            {
                if (y <= _ty[0] + 0.01f || y >= _ty[_ty.Length - 1] - 0.01f || n + 2 > _ringY.Length) return n;
                // Drop key rings that would crowd the edge.
                for (int i = n - 1; i >= 0; i--)
                {
                    if (MathF.Abs(_ringY[i] - y) < 0.012f)
                    {
                        _ringY[i] = _ringY[n - 1];
                        n--;
                    }
                }
                _ringY[n++] = y - 0.001f;
                _ringY[n++] = y;
                return n;
            }

            private uint TorsoColour(float y)
            {
                if (y < _g.HemY) return _g.SkirtHemY > 0f && _g.SkirtRgb != 0 ? _g.SkirtRgb : _g.Lower;
                if (_g.BandH > 0f && y < _g.HemY + _g.BandH) return _g.BandRgb;
                return _g.Top;
            }

            private void TorsoRing(float y, uint rgb)
            {
                TorsoAt(y, out float rx, out float rz, out float zo);
                if (_g.Baffles && y > _g.HemY + _g.BandH && y < _ty[5])
                {
                    float ph = (y - (_g.HemY + _g.BandH)) / 0.075f;
                    float puff = 0.006f * MathF.Abs(MathF.Sin(ph * MathF.PI));
                    rx += puff;
                    rz += puff;
                }
                TorsoBones(y, out Bone b0, out Bone b1, out float w0);
                _k.FlatRing(new V3(0f, y, zo), rx, rz, rgb, b0, b1, w0, TorsoExp);
            }

            /// <summary>A band (rib hem, belt) standing <paramref name="proud"/> metres off the torso between two heights.</summary>
            private void BandRing(float y0, float y1, float proud, uint rgb)
            {
                _k.BeginLoft();
                for (int k = 0; k < 4; k++)
                {
                    float y = k == 0 ? y0 - 0.002f : k == 3 ? y1 + 0.002f : k == 1 ? y0 : y1;
                    float p = k == 0 || k == 3 ? 0f : proud;
                    TorsoAt(y, out float rx, out float rz, out float zo);
                    TorsoBones(y, out Bone b0, out Bone b1, out float w0);
                    _k.FlatRing(new V3(0f, y, zo), rx + p + 0.001f, rz + p + 0.001f, rgb, b0, b1, w0, TorsoExp);
                }
                _k.EndLoft(_k.Seg(20, 7), LoftCap.None, LoftCap.None);
            }

            private void Belt()
            {
                if (Lod >= 2) return;
                float y = _m.HipJointY + 0.1f;
                Mat(MaterialChannel.Leather);
                BandRing(y - 0.016f, y + 0.016f, 0.005f, _g.BeltRgb);
                Mat(MaterialChannel.Metal);
                V3 p = TorsoPoint(0f, y, 0.008f, out V3 n);
                _k.Ellipsoid(p, new V3(0.022f, 0.018f, 0.005f), Quat.Identity, _torso.Item == OutfitItem.Chuba ? _g.BeltRgb : 0xB8B4A8u, 8, 4, Bone.Hips, 4f);
                Mat(MaterialChannel.Fabric);
            }

            /// <summary>
            /// A patch lying on the torso: rows run upward (heights <paramref name="ys"/>) and columns across from
            /// <paramref name="aLo"/>[row] to <paramref name="aHi"/>[row] (radians, 0 front, increasing toward the wearer's
            /// right), lifted along the normal; with <paramref name="rowRgb"/> each band of quads has its own colour.
            /// </summary>
            private void TorsoPatch(int rows, int cols, float[] ys, float[] aLo, float[] aHi, float lift, uint rgb, uint[] rowRgb = null)
            {
                _k.BeginGrid(rows, cols);
                for (int i = 0; i < rows; i++)
                {
                    TorsoBones(ys[i], out Bone b0, out Bone b1, out float w0);
                    for (int j = 0; j < cols; j++)
                    {
                        float a = aLo[i] + (aHi[i] - aLo[i]) * j / (cols - 1);
                        V3 p = TorsoPoint(a, ys[i], lift, out V3 nn);
                        _k.GridSet(i, j, cols, p, rgb, b0, b1, w0);
                    }
                }
                if (rowRgb != null) _k.EndGridColours(rows, cols, false, new V3(0f, ys[0], 0f), V3.Up, null, rowRgb);
                else _k.EndGrid(rows, cols, false, new V3(0f, ys[0], 0f), V3.Up);
            }

            // Scratch for patches.
            private readonly float[] _py = new float[24], _pa = new float[24], _pb = new float[24];

            /// <summary>A V-shaped opening on the chest from the neck down to <paramref name="bottomY"/>: the shirt or
            /// daura showing between the fronts of a coat, blazer or waistcoat, with lapel strips along its edges.</summary>
            private void ChestV(float bottomY, float topHalfAngle, float bottomHalfAngle, uint inner, uint lapel, bool lapels)
            {
                float topY = _m.ShoulderY + 0.008f;
                int rows = Lod == 0 ? 6 : 3;
                for (int i = 0; i < rows; i++)
                {
                    float t = i / (float)(rows - 1);
                    _py[i] = bottomY + (topY - bottomY) * t;
                    float half = bottomHalfAngle + (topHalfAngle - bottomHalfAngle) * t;
                    _pa[i] = -half;
                    _pb[i] = half;
                }
                TorsoPatch(rows, Lod == 0 ? 5 : 3, _py, _pa, _pb, 0.0015f, inner);
                if (!lapels || Lod >= 2) return;
                // Lapels: folded strips along both edges of the V, wider at the chest.
                for (int s = -1; s <= 1; s += 2)
                {
                    for (int i = 0; i < rows; i++)
                    {
                        float t = i / (float)(rows - 1);
                        float half = bottomHalfAngle + (topHalfAngle - bottomHalfAngle) * t;
                        float w = 0.05f + 0.2f * MathF.Sin(t * MathF.PI * 0.85f);
                        float a0 = s * half, a1 = s * (half + w);
                        _pa[i] = Math.Min(a0, a1);
                        _pb[i] = Math.Max(a0, a1);
                    }
                    TorsoPatch(rows, 3, _py, _pa, _pb, 0.005f, lapel);
                }
            }

            /// <summary>A rounded panel (pocket, patch) on the torso front at angle <paramref name="a"/>, height
            /// <paramref name="y"/>.</summary>
            private void TorsoPanel(float a, float y, float w, float h, float lift, uint rgb, float exp = 4f)
            {
                V3 p = TorsoPoint(a, y, lift, out V3 n);
                float yaw = MathF.Atan2(n.X, n.Z) * Quat.Rad2Deg;
                TorsoBones(y, out Bone b0, out Bone b1, out float w0);
                _k.Ellipsoid(p, new V3(w * 0.5f, h * 0.5f, 0.004f), Quat.Euler(0f, yaw, 0f), rgb, _k.Seg(10, 6), _k.Seg(6, 4), b0, exp, b1, w0);
            }

            /// <summary>A small button or stud on the torso.</summary>
            private void Button(float a, float y, uint rgb, float r = 0.0055f)
            {
                V3 p = TorsoPoint(a, y, 0.003f, out V3 n);
                TorsoBones(y, out Bone b0, out Bone b1, out float w0);
                _k.Ellipsoid(p, new V3(r, r, r * 0.5f), Quat.Euler(0f, MathF.Atan2(n.X, n.Z) * Quat.Rad2Deg, 0f), rgb, 6, 3, b0, 2f, b1, w0);
            }

            /// <summary>A line (seam, zip, placket, tape) running up the torso at angle <paramref name="a"/>.</summary>
            private void TorsoLine(float a, float y0, float y1, float halfWidth, float lift, uint rgb)
            {
                int rows = Lod == 0 ? 5 : 2;
                for (int i = 0; i < rows; i++)
                {
                    _py[i] = y0 + (y1 - y0) * i / (rows - 1);
                    TorsoAt(_py[i], out float rx, out float rz, out float zo);
                    float da = halfWidth / Math.Max(0.05f, (rx + rz) * 0.5f);
                    _pa[i] = a - da;
                    _pb[i] = a + da;
                }
                TorsoPatch(rows, 2, _py, _pa, _pb, lift, rgb);
            }

            /// <summary>A collar: a ring band round the base of the neck (rib, band or stand collar).</summary>
            private void CollarRing(float h, float proud, uint rgb, float open = 0f)
            {
                float y = _m.ShoulderY + 0.002f;
                _k.BeginLoft();
                for (int k = 0; k < 3; k++)
                {
                    float yy = y + h * k / 2f;
                    float r = 0.068f * (_m.HeadW / 0.36f) + proud * (k == 1 ? 1f : 0.6f);
                    _k.FlatRing(new V3(0f, yy, -0.008f), r, r * 0.92f, rgb, k == 0 ? Bone.Chest : Bone.Neck, Bone.Chest, k == 0 ? 1f : 0.6f, 2.2f);
                }
                _k.EndLoft(_k.Seg(16, 7), LoftCap.None, LoftCap.None);
            }

            /// <summary>Shirt collar points folded down on the chest.</summary>
            private void CollarPoints(uint rgb, float spread)
            {
                if (Lod >= 2) return;
                float y = _m.ShoulderY - 0.005f;
                for (int s = -1; s <= 1; s += 2)
                {
                    V3 a = TorsoPoint(s * 0.12f, y + 0.02f, 0.004f, out V3 n0);
                    V3 b = TorsoPoint(s * (0.12f + spread), y - 0.06f, 0.006f, out V3 n1);
                    V3 c = TorsoPoint(s * (0.45f + spread * 0.5f), y + 0.01f, 0.004f, out V3 n2);
                    int ia = _k.Vertex(a, n0, rgb, Bone.Chest, Bone.Neck, 0.8f);
                    int ib = _k.Vertex(b, n1, rgb, Bone.Chest, Bone.Chest, 1f);
                    int ic = _k.Vertex(c, n2, rgb, Bone.Chest, Bone.Neck, 0.8f);
                    _k.Tri(ia, ib, ic);
                    // A back face so the point reads from every side.
                    int ja = _k.Vertex(a - n0 * 0.002f, -n0, rgb, Bone.Chest, Bone.Neck, 0.8f);
                    int jb = _k.Vertex(b - n1 * 0.002f, -n1, rgb, Bone.Chest, Bone.Chest, 1f);
                    int jc = _k.Vertex(c - n2 * 0.002f, -n2, rgb, Bone.Chest, Bone.Neck, 0.8f);
                    _k.Tri(ja, jc, jb);
                }
            }

            private void TorsoDetails()
            {
                OutfitItem t = _torso.Item;
                float hip = _m.HipJointY, sh = _m.ShoulderY, T = _m.TorsoM;
                if (Lod >= 2)
                {
                    if (t == OutfitItem.PoliceUniform && WearsPoliceVest(_r)) PoliceVest();
                    return;
                }
                Mat(MaterialChannel.Fabric);
                uint top = _g.Top;
                switch (t)
                {
                    case OutfitItem.None:
                        break;
                    case OutfitItem.TShirt:
                        CollarRing(0.012f, 0.006f, CharacterPalette.Shade(top, 0.85f));
                        if (Lod == 0 && Var(20u) < 0.5f)
                        {
                            // A plain chest band print (no text, no logo).
                            uint print = CharacterPalette.Luma(top) > 0.5f ? CharacterPalette.Shade(top, 0.6f) : CharacterPalette.Mix(top, 0xFFFFFFu, 0.55f);
                            for (int i = 0; i < 2; i++) _py[i] = hip + 0.52f * T + 0.04f * i;
                            _pa[0] = _pa[1] = -0.55f;
                            _pb[0] = _pb[1] = 0.55f;
                            TorsoPatch(2, 7, _py, _pa, _pb, 0.0012f, print);
                        }
                        break;
                    case OutfitItem.Vest:
                    {
                        // A singlet: a scoop neck showing skin.
                        for (int i = 0; i < 3; i++)
                        {
                            float tt = i / 2f;
                            _py[i] = sh - 0.07f + 0.08f * tt;
                            _pa[i] = -0.25f - 0.35f * tt;
                            _pb[i] = 0.25f + 0.35f * tt;
                        }
                        TorsoPatch(3, 5, _py, _pa, _pb, 0.001f, _skin);
                        break;
                    }
                    case OutfitItem.Hoodie:
                        Hood(top);
                        CollarRing(0.01f, 0.006f, CharacterPalette.Shade(top, 0.85f));
                        TorsoPanel(0f, hip + 0.14f, 0.2f, 0.11f, 0.004f, CharacterPalette.Shade(top, 0.9f), 5f);
                        if (Lod == 0)
                            for (int s = -1; s <= 1; s += 2)
                            {
                                V3 a = TorsoPoint(s * 0.16f, sh - 0.01f, 0.006f, out V3 n0);
                                V3 b = TorsoPoint(s * 0.17f, sh - 0.15f, 0.006f, out V3 n1);
                                _k.Capsule(a, b, 0.0035f, 0.0035f, CharacterPalette.Lace, Bone.Chest, Bone.Chest, 4, 2, false, LoftCap.None, LoftCap.Round);
                            }
                        break;
                    case OutfitItem.DhakaJacket:
                    {
                        CollarRing(0.035f, 0.01f, CharacterPalette.Shade(top, 0.8f));
                        Mat(MaterialChannel.Metal);
                        TorsoLine(0f, _g.HemY, sh - 0.01f, 0.004f, 0.004f, CharacterPalette.Zip);
                        Mat(MaterialChannel.Fabric);
                        int weave = _torso.Pattern;
                        for (int s = -1; s <= 1; s += 2)
                        {
                            DhakaPatch(s * 0.18f, s * 0.75f, sh - 0.11f, sh - 0.025f, weave);
                            if (Lod == 0) DhakaPatch(s * 0.32f, s * 0.68f, hip + 0.08f, hip + 0.15f, weave);
                        }
                        break;
                    }
                    case OutfitItem.Jacket:
                        CollarRing(0.045f, 0.018f, top);
                        Mat(MaterialChannel.Metal);
                        TorsoLine(0f, _g.HemY, sh + 0.01f, 0.004f, 0.006f + _g.Bulk * 0.3f, 0x2A2A2Eu);
                        break;
                    case OutfitItem.Fleece:
                    {
                        CollarRing(0.04f, 0.01f, top);
                        Mat(MaterialChannel.Metal);
                        TorsoLine(0f, sh - 0.2f, sh + 0.02f, 0.0035f, 0.004f, 0x2A2A2Eu);
                        Mat(MaterialChannel.Fabric);
                        // Contrast shoulder yoke.
                        uint yoke = CharacterPalette.Luma(top) > 0.35f ? 0x3A3D42u : 0x6E7076u;
                        for (int i = 0; i < 3; i++)
                        {
                            _py[i] = sh - 0.09f + 0.045f * i;
                            _pa[i] = -2.4f;
                            _pb[i] = 2.4f;
                        }
                        TorsoPatch(3, Lod == 0 ? 14 : 7, _py, _pa, _pb, 0.004f, yoke);
                        break;
                    }
                    case OutfitItem.Shirt:
                    case OutfitItem.Uniform:
                    case OutfitItem.SchoolShirt:
                    case OutfitItem.PoliceUniform:
                        ShirtDetails(top);
                        break;
                    case OutfitItem.Blazer:
                        ChestV(hip + 0.24f, 0.36f, 0.06f, _g.Dress, CharacterPalette.Shade(top, 0.9f), true);
                        CollarPoints(_g.Dress, 0.05f);
                        Button(0f, hip + 0.18f, 0x1E1E20u, 0.007f);
                        Button(0f, hip + 0.1f, 0x1E1E20u, 0.007f);
                        for (int s = -1; s <= 1; s += 2) TorsoPanel(s * 0.48f, hip + 0.02f, 0.11f, 0.022f, 0.004f, CharacterPalette.Shade(top, 0.92f));
                        TorsoPanel(-0.42f, sh - 0.12f, 0.08f, 0.012f, 0.004f, CharacterPalette.Shade(top, 0.92f));
                        break;
                    case OutfitItem.Kurta:
                        KurtaDetails();
                        break;
                    case OutfitItem.Daura:
                        DauraDetails();
                        break;
                    case OutfitItem.Sari:
                        SariDetails();
                        break;
                    case OutfitItem.Robe:
                        RobeDetails();
                        break;
                    case OutfitItem.Chuba:
                        ChubaDetails();
                        break;
                }
                if (t == OutfitItem.PoliceUniform && WearsPoliceVest(_r)) PoliceVest();
            }

            /// <summary>A patch of dhaka weave on the torso (jacket yoke and pockets).</summary>
            private void DhakaPatch(float a0, float a1, float y0, float y1, int weave)
            {
                int cols = Lod == 0 ? 8 : 4, rows = Lod == 0 ? 5 : 3;
                float lo = Math.Min(a0, a1), hi = Math.Max(a0, a1);
                for (int i = 0; i < rows; i++)
                {
                    _py[i] = y0 + (y1 - y0) * i / (rows - 1);
                    _pa[i] = lo;
                    _pb[i] = hi;
                }
                uint ground = CharacterPalette.TopiBase[0];
                var q = new uint[(rows - 1) * (cols - 1)];
                for (int i = 0; i < rows - 1; i++)
                    for (int j = 0; j < cols - 1; j++)
                    {
                        int cell = DhakaCell(weave % 2 == 0 ? 1 : 3, j, i);
                        q[i * (cols - 1) + j] = cell > 0 ? CharacterPalette.DhakaMotif(weave, cell - 1) : ground;
                    }
                _k.BeginGrid(rows, cols);
                for (int i = 0; i < rows; i++)
                {
                    TorsoBones(_py[i], out Bone b0, out Bone b1, out float w0);
                    for (int j = 0; j < cols; j++)
                    {
                        float a = _pa[i] + (_pb[i] - _pa[i]) * j / (cols - 1);
                        _k.GridSet(i, j, cols, TorsoPoint(a, _py[i], 0.002f, out V3 nn), ground, b0, b1, w0);
                    }
                }
                _k.EndGridColours(rows, cols, false, new V3(0f, _py[0], 0f), V3.Up, q, null);
            }

            private void Hood(uint rgb)
            {
                float sh = _m.ShoulderY;
                var c = new V3(0f, sh + 0.02f, -0.1f);
                _k.BeginLoft();
                for (int i = 0; i < 4; i++)
                {
                    float t = i / 3f;
                    _k.Ring(new LoftRing
                    {
                        C = c + new V3(0f, 0.05f * t, -0.03f * t), AxisX = V3.Right, AxisZ = V3.Forward, Rx = 0.12f - 0.03f * t * t, Rz = 0.045f + 0.015f * MathF.Sin(t * 3f),
                        Exp = 2.2f, Rgb = i == 3 ? CharacterPalette.Shade(rgb, 0.85f) : rgb, B0 = Bone.Chest, B1 = Bone.Neck, W0 = 0.8f,
                    });
                }
                _k.EndLoft(_k.Seg(14, 6), LoftCap.Round, LoftCap.Round);
            }

            private void ShirtDetails(uint top)
            {
                float hip = _m.HipJointY, sh = _m.ShoulderY;
                OutfitItem t = _torso.Item;
                uint seam = CharacterPalette.Shade(top, 0.86f);
                CollarRing(0.022f, 0.006f, top);
                if (Lod > 0) return;
                CollarPoints(top, t == OutfitItem.PoliceUniform ? 0.06f : 0.04f);
                // Placket with buttons, a chest pocket.
                TorsoLine(0f, _g.HemY + 0.01f, sh - 0.02f, 0.012f, 0.0015f, seam);
                if (Lod == 0)
                {
                    uint btn = CharacterPalette.Luma(top) > 0.6f ? 0xE8E4DAu : CharacterPalette.Shade(top, 0.7f);
                    for (int i = 0; i < 4; i++) Button(0f, sh - 0.06f - 0.07f * i, btn, 0.0045f);
                }
                TorsoPanel(-0.38f, sh - 0.12f, 0.075f, 0.08f, 0.003f, seam);
                if (t == OutfitItem.PoliceUniform)
                {
                    TorsoPanel(0.38f, sh - 0.12f, 0.075f, 0.08f, 0.003f, seam);
                    // Navy epaulettes (plain; no rank insignia).
                    for (int s = -1; s <= 1; s += 2)
                    {
                        V3 a = new V3(s * 0.07f, sh + 0.01f, -0.005f), b = new V3(s * (_m.ShoulderW * 0.5f - 0.02f), sh - 0.005f, 0f);
                        _k.Capsule(a, b, 0.012f, 0.012f, CharacterPalette.PoliceTrousers, Bone.Chest, Bone.Chest, 5, 2, false, LoftCap.Round, LoftCap.Round);
                    }
                }
                if (t == OutfitItem.SchoolShirt)
                {
                    // The tie: knot and blade with diagonal stripes.
                    uint tie = CharacterPalette.At(CharacterPalette.SchoolTie, _torso.Pattern);
                    uint stripe = 0xE8C440u;
                    V3 knot = TorsoPoint(0f, sh - 0.015f, 0.008f, out V3 kn);
                    _k.Ellipsoid(knot, new V3(0.016f, 0.014f, 0.008f), Quat.Identity, tie, 7, 4, Bone.Chest);
                    int rows = Lod == 0 ? 7 : 3;
                    var rowRgb = new uint[rows - 1];
                    for (int i = 0; i < rows; i++)
                    {
                        float tt = i / (float)(rows - 1);
                        _py[i] = sh - 0.02f - 0.2f * (1f - tt);
                        float half = 0.06f + 0.04f * (1f - tt);
                        _pa[i] = -half;
                        _pb[i] = half;
                        if (i < rows - 1) rowRgb[i] = i % 2 == 1 ? stripe : tie;
                    }
                    TorsoPatch(rows, 3, _py, _pa, _pb, 0.006f, tie, rowRgb);
                }
            }

            /// <summary>The fluorescent traffic vest with silver reflective tape round the body and over the shoulders
            /// (generic; no lettering, no insignia).</summary>
            private void PoliceVest()
            {
                float hip = _m.HipJointY, sh = _m.ShoulderY;
                Mat(MaterialChannel.Fabric);
                uint vest = CharacterPalette.PoliceVest, tape = CharacterPalette.PoliceTape;
                float y0 = hip + 0.12f, y1 = sh - 0.005f;
                int rows = Lod == 0 ? 9 : Lod == 1 ? 5 : 3;
                var rowRgb = new uint[rows - 1];
                _k.BeginLoft();
                for (int i = 0; i < rows; i++)
                {
                    float y = y0 + (y1 - y0) * i / (rows - 1);
                    TorsoAt(y, out float rx, out float rz, out float zo);
                    TorsoBones(y, out Bone b0, out Bone b1, out float w0);
                    float h = (y - y0) / (y1 - y0);
                    bool band = Lod < 2 && (MathF.Abs(h - 0.18f) < 0.07f || MathF.Abs(h - 0.48f) < 0.07f);
                    _k.FlatRing(new V3(0f, y, zo), rx + 0.008f, rz + 0.008f, band ? tape : vest, b0, b1, w0, TorsoExp);
                }
                _k.EndLoft(_k.Seg(20, 7), LoftCap.None, LoftCap.None);
                if (Lod >= 1) return;
                // V neck showing the shirt, tape over the shoulders.
                ChestV(sh - 0.17f, 0.45f, 0.12f, _g.Top, vest, false);
                for (int s = -1; s <= 1; s += 2)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        float tt = i / 3f;
                        _py[i] = y0 + 0.08f + (y1 - y0 - 0.08f) * tt;
                        _pa[i] = s * 0.42f - 0.06f;
                        _pb[i] = s * 0.42f + 0.06f;
                    }
                    TorsoPatch(4, 2, _py, _pa, _pb, 0.0105f, tape);
                }
            }

            private void KurtaDetails()
            {
                float sh = _m.ShoulderY;
                uint dress = _g.Dress;
                // A round neck with a placket and an embroidered yoke band in a contrast colour.
                uint trim = CharacterPalette.Luma(dress) > 0.6f ? CharacterPalette.Shade(dress, 0.7f) : CharacterPalette.Mix(dress, 0xF2C230u, 0.5f);
                CollarRing(0.012f, 0.005f, trim);
                TorsoLine(0f, sh - 0.17f, sh - 0.005f, 0.014f, 0.0015f, trim);
                if (Lod == 0)
                {
                    for (int i = 0; i < 3; i++) Button(0f, sh - 0.04f - 0.045f * i, CharacterPalette.Gold, 0.004f);
                    for (int i = 0; i < 2; i++)
                    {
                        _py[i] = sh - 0.075f + 0.012f * i;
                        _pa[i] = -0.6f;
                        _pb[i] = 0.6f;
                    }
                    TorsoPatch(2, 9, _py, _pa, _pb, 0.0012f, trim);
                }
            }

            /// <summary>The daura: its closed round neck, the left-over-right overlap edge from the neck down to the wearer's
            /// right and its eight tie strings in four pairs (W2_DESIGN 6.1, checklist 10.7 #5), as much of them as shows
            /// in the V of a coat or waistcoat.</summary>
            private void DauraDetails()
            {
                float hip = _m.HipJointY, sh = _m.ShoulderY;
                int layer = _g.Layer;
                uint dress = _g.Dress, tie = CharacterPalette.Shade(dress, 0.72f), lapel = CharacterPalette.Shade(_g.Coat, 0.9f);
                // The V of the outer layer (bottom height, half-angles at the top and the bottom).
                float vBottom = layer == 2 ? hip + 0.22f : sh - 0.17f, vTop = layer == 2 ? 0.34f : 0.24f, vLow = layer == 2 ? 0.05f : 0.015f;
                if (layer > 0) ChestV(vBottom, vTop, vLow, dress, lapel, layer == 2);
                // The closed round neck stands above any outer layer.
                CollarRing(0.02f, 0.003f, dress);
                if (layer == 1 && Lod == 0)
                    for (int i = 0; i < 4; i++) Button(0f, hip + 0.03f + 0.055f * i, 0x1E1E20u, 0.0055f);
                if (layer == 2 && Lod == 0)
                {
                    Button(0f, hip + 0.16f, 0x1E1E20u, 0.007f);
                    for (int s = -1; s <= 1; s += 2) TorsoPanel(s * 0.48f, hip + 0.0f, 0.1f, 0.02f, 0.004f, lapel);
                }
                bool Shows(float a, float y)
                {
                    if (layer == 0) return true;
                    if (y < vBottom) return false;
                    float t = (y - vBottom) / Math.Max(0.01f, sh + 0.008f - vBottom);
                    return Math.Abs(a) < vLow + (vTop - vLow) * t - 0.02f;
                }
                // The overlap edge: from the neck across to the wearer's right side at the chest, then down the side.
                if (Lod == 0)
                {
                    int n = layer == 0 ? 7 : 5;
                    V3[] path = _k.PathBuffer(n, out float[] rx, out float[] rz);
                    int used = 0;
                    for (int i = 0; i < n; i++)
                    {
                        float tt = i / (float)(n - 1);
                        float a = layer == 0 ? -0.02f + 0.62f * tt : -0.02f + 0.3f * tt;
                        float y = layer == 0 ? sh - 0.02f - 0.24f * tt : sh - 0.02f - 0.13f * tt;
                        if (!Shows(a, y)) break;
                        path[used] = TorsoPoint(a, y, 0.0035f, out V3 nn);
                        rx[used] = 0.0025f;
                        rz[used] = 0.0025f;
                        used++;
                    }
                    if (used >= 2) _k.Sweep(path, used, rx, rz, V3.Up, tie, 4, Bone.Chest);
                }
                // Four tie pairs (8 strings): at the neck and the chest in the V, and two at the waist on the right side.
                var knots = new[] { new V3(0.1f, sh - 0.045f, 0f), new V3(0.26f, sh - 0.13f, 0f), new V3(0.75f, hip + 0.16f, 0f), new V3(0.85f, hip + 0.06f, 0f) };
                for (int i = 0; i < knots.Length; i++)
                {
                    float a = knots[i].X, y = knots[i].Y;
                    if (!Shows(a, y)) continue;
                    V3 k0 = TorsoPoint(a, y, 0.004f, out V3 kn);
                    TorsoBones(y, out Bone b0, out Bone b1, out float w0);
                    if (Lod == 0) _k.Sphere(k0, 0.007f, tie, 5, 3, b0);
                    for (int s = 0; s < 2; s++)
                    {
                        V3 end = k0 + new V3(s == 0 ? -0.01f : 0.007f, -0.04f, 0f) + kn * 0.004f;
                        _k.Capsule(k0, end, 0.0028f, 0.0024f, tie, b0, b1, 4, 2, false, LoftCap.None, Lod == 0 ? LoftCap.Round : LoftCap.None);
                    }
                }
            }

            private void SariDetails()
            {
                float hip = _m.HipJointY, sh = _m.ShoulderY;
                uint body = _g.Dress, border = _g.SkirtBorder;
                bool haku = _torso.Colour == CharacterPalette.HakuSari;
                bool drape = !haku || _torso.Pattern % 2 == 1;
                if (drape)
                {
                    // The pallu: from the right hip across the chest over the left shoulder, then down the back, with its
                    // border along the lower edge (the haku patasi's wide red border across the chest).
                    Pallu(body, border, hip + 0.06f, haku);
                }
                else
                {
                    // Haku patasi: the white patuka sash at the waist and a round-neck blouse.
                    Mat(MaterialChannel.Fabric);
                    BandRing(hip + 0.16f, hip + 0.22f, 0.012f, 0xF2EFE6u);
                    CollarRing(0.012f, 0.004f, CharacterPalette.Shade(_g.Top, 0.85f));
                }
                if (Lod == 0) CollarRing(0.008f, 0.003f, CharacterPalette.Shade(_g.Top, 0.85f));
            }

            /// <summary>A draped sash from the right hip over the left shoulder down to <paramref name="backEndY"/> behind
            /// (sari pallu, monk's zen, sadhu shawl).</summary>
            private void Pallu(uint body, uint border, float backEndY, bool frontBorder = false)
            {
                float hip = _m.HipJointY, sh = _m.ShoulderY;
                Mat(MaterialChannel.Fabric);
                int n = Lod == 0 ? 7 : 4;
                // Front: hip right → left shoulder.
                _k.BeginLoft();
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)(n - 1);
                    float a = 0.85f - 1.5f * t;
                    float y = hip + 0.06f + (sh + 0.005f - (hip + 0.06f)) * t;
                    V3 p = TorsoPoint(a, y, 0.012f + _g.Bulk, out V3 nn);
                    if (t > 0.92f) p = p + new V3(0f, 0.012f, -0.015f);
                    TorsoBones(y, out Bone b0, out Bone b1, out float w0);
                    _k.Ring(new LoftRing
                    {
                        C = p, AxisX = V3.Cross(nn, new V3(-0.75f, 1f, 0f).Normalized).Normalized, AxisZ = nn, Rx = 0.075f, Rz = 0.007f, Exp = 2.6f,
                        Rgb = body, B0 = b0, B1 = b1, W0 = w0,
                    });
                }
                _k.EndLoft(_k.Seg(8, 4), LoftCap.Flat, LoftCap.None);
                if (frontBorder && border != body && Lod < 2)
                {
                    // The border along the lower edge of the front band.
                    _k.BeginLoft();
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)(n - 1);
                        float a = 0.85f - 1.5f * t;
                        float y = hip + 0.06f + (sh + 0.005f - (hip + 0.06f)) * t;
                        V3 p = TorsoPoint(a, y, 0.0135f + _g.Bulk, out V3 nn);
                        if (t > 0.92f) p = p + new V3(0f, 0.012f, -0.015f);
                        V3 across = V3.Cross(nn, new V3(-0.75f, 1f, 0f).Normalized).Normalized;
                        TorsoBones(y, out Bone b0, out Bone b1, out float w0);
                        _k.Ring(new LoftRing
                        {
                            C = p + across * 0.058f, AxisX = across, AxisZ = nn, Rx = 0.019f, Rz = 0.0072f, Exp = 2.6f, Rgb = border, B0 = b0, B1 = b1, W0 = w0,
                        });
                    }
                    _k.EndLoft(_k.Seg(6, 4), LoftCap.Flat, LoftCap.None);
                }
                // Back: over the left shoulder down to the hem end, with the border at the bottom.
                var c = new V3(-0.1f, sh + 0.02f, -0.03f);
                var d = new V3(-0.08f, backEndY, -_trz[2] - 0.035f);
                _k.BeginLoft();
                int m = Lod == 0 ? 5 : 3;
                for (int i = 0; i < m; i++)
                {
                    float t = i / (float)(m - 1);
                    float y = c.Y + (d.Y - c.Y) * t;
                    TorsoAt(Math.Min(y, _ty[5]), out float rx, out float rz, out float zo);
                    var p = new V3(c.X + (d.X - c.X) * t, y, Math.Min(-rz - 0.012f - _g.Bulk, -0.03f - 0.08f * t) + zo);
                    TorsoBones(y, out Bone b0, out Bone b1, out float w0);
                    _k.Ring(new LoftRing { C = p, AxisX = V3.Right, AxisZ = V3.Forward, Rx = 0.085f + 0.02f * t, Rz = 0.007f, Exp = 2.6f, Rgb = body, B0 = b0, B1 = b1, W0 = w0 });
                    if (i == m - 2 && border != body)
                    {
                        float y2 = y + (d.Y - y) * 0.6f;
                        var p2 = new V3(p.X, y2, p.Z - 0.004f);
                        _k.Ring(new LoftRing { C = p2, AxisX = V3.Right, AxisZ = V3.Forward, Rx = 0.098f, Rz = 0.007f, Exp = 2.6f, Rgb = body, B0 = b0, B1 = b1, W0 = w0 });
                        _k.Ring(new LoftRing { C = p2 + new V3(0f, -0.001f, 0f), AxisX = V3.Right, AxisZ = V3.Forward, Rx = 0.098f, Rz = 0.007f, Exp = 2.6f, Rgb = border, B0 = b0, B1 = b1, W0 = w0 });
                    }
                }
                _k.EndLoft(_k.Seg(8, 4), LoftCap.None, LoftCap.Flat);
            }

            private void RobeDetails()
            {
                float hip = _m.HipJointY, sh = _m.ShoulderY;
                bool monk = _torso.Colour == 0;
                uint body = _g.Dress;
                if (monk)
                {
                    // The yellow dhonka on the right chest and shoulder, piped in blue, under the maroon zen.
                    for (int i = 0; i < 4; i++)
                    {
                        float tt = i / 3f;
                        _py[i] = sh - 0.2f + 0.2f * tt;
                        _pa[i] = 0.15f + 0.1f * tt;
                        _pb[i] = 1.55f;
                    }
                    TorsoPatch(4, Lod == 0 ? 6 : 3, _py, _pa, _pb, 0.0012f, CharacterPalette.Dhonka);
                    if (Lod == 0) TorsoLine(0.17f, sh - 0.2f, sh, 0.004f, 0.0018f, CharacterPalette.DhonkaPiping);
                    Pallu(body, CharacterPalette.Shade(body, 0.85f), hip - 0.1f);
                }
                else
                {
                    // Sadhu: a saffron shawl over the left shoulder; a rudraksha mala is an accent.
                    Pallu(CharacterPalette.Shade(body, 1.05f), CharacterPalette.Shade(body, 0.85f), hip + 0.05f);
                    BandRing(_g.HemY - 0.01f, _g.HemY + 0.03f, 0.008f, CharacterPalette.Shade(body, 0.9f));
                }
            }

            private void ChubaDetails()
            {
                float hip = _m.HipJointY, sh = _m.ShoulderY;
                uint blouse = _g.SleeveR;
                // The chuba is sleeveless: the blouse shows at the shoulders and in a V at the neck; the wrap crosses to
                // the wearer's right.
                ChestV(sh - 0.14f, 0.32f, 0.08f, blouse, _g.Dress, false);
                CollarRing(0.02f, 0.004f, blouse);
                if (Lod == 0)
                {
                    int n = 5;
                    V3[] path = _k.PathBuffer(n, out float[] rx, out float[] rz);
                    for (int i = 0; i < n; i++)
                    {
                        float tt = i / (float)(n - 1);
                        path[i] = TorsoPoint(-0.08f + 0.9f * tt, sh - 0.12f - 0.12f * tt, 0.003f, out V3 nn);
                        rx[i] = 0.003f;
                        rz[i] = 0.003f;
                    }
                    _k.Sweep(path, n, rx, rz, V3.Up, CharacterPalette.Shade(_g.Dress, 0.75f), 4, Bone.Chest);
                }
            }

            /// <summary>
            /// The hem below the torso: a flared loft from the waist down to the knee or ankle, weighted to the hips and the
            /// skirt bones; pleats as radial ridges (daura 5, sari front pleats, school skirt), a border band at the hem
            /// (sari, haku patasi's red border), side slits on a kurta; the sari's pleated front and the chuba's striped
            /// pangden apron are panels on it.
            /// </summary>
            private void Skirt()
            {
                OutfitItem t = _torso.Item;
                float hip = _m.HipJointY, T = _m.TorsoM;
                float hemY = _g.SkirtHemY;
                float waist = hip + 0.17f * T;
                float hw = 0.5f * _m.HipW;
                float flare = _g.SkirtFlare;
                bool ankle = hemY < _m.AnkleY + _m.ShinM * 0.5f;
                byte pleats = _g.Pleats;
                uint dress = _g.SkirtRgb, border = _g.SkirtBorder;
                Mat(MaterialChannel.Fabric);
                int rings = Lod == 0 ? 7 : Lod == 1 ? 4 : 2;
                float bulk = _g.Bulk;
                _k.BeginLoft();
                TorsoAt(waist, out float wrx, out float wrz, out float wzo);
                _k.FlatRing(new V3(0f, waist, wzo), wrx + 0.006f + bulk, wrz + 0.006f + bulk, dress, Bone.Spine, Bone.Hips, 0.5f, TorsoExp);
                _k.FlatRing(new V3(0f, hip, -0.002f), hw + 0.018f + bulk, 0.118f + 0.3f * _m.Belly + bulk, dress, Bone.Hips, Bone.Hips, 1f, 2.6f);
                for (int i = 1; i <= rings; i++)
                {
                    float f = i / (float)rings;
                    float y = hip + (hemY - hip) * f;
                    float grow = 1f + (flare - 1f) * MathF.Pow(f, 0.8f);
                    float sx = (hw + 0.022f + bulk) * grow, sz = (0.12f + 0.3f * _m.Belly + bulk) * grow + (ankle ? 0.012f * f : 0f);
                    float w0 = 1f - 0.7f * f;
                    bool last = i == rings;
                    float bh = ankle ? 0.05f : 0.03f;
                    if (last && border != dress && Lod < 2)
                    {
                        float yb = y + bh;
                        _k.FlatRing(new V3(0f, yb + 0.002f, 0.004f), sx * 0.985f, sz * 0.985f, dress, Bone.Hips, Bone.Hips, w0, 2.3f, true, pleats);
                        _k.FlatRing(new V3(0f, yb, 0.004f), sx * 0.986f, sz * 0.986f, border, Bone.Hips, Bone.Hips, w0, 2.3f, true, pleats);
                    }
                    _k.FlatRing(new V3(0f, y, 0.004f), sx, sz, last ? border : dress, Bone.Hips, Bone.Hips, w0, 2.3f, true, pleats);
                }
                _k.EndLoft(_k.Seg(22, 6), LoftCap.None, LoftCap.None);
                // A rolled hem lip so the edge reads.
                if (Lod < 2)
                {
                    float grow = flare;
                    float sx = (hw + 0.022f + bulk) * grow, sz = (0.12f + 0.3f * _m.Belly + bulk) * grow + (ankle ? 0.012f : 0f);
                    _k.BeginLoft();
                    _k.FlatRing(new V3(0f, hemY + 0.006f, 0.004f), sx + 0.003f, sz + 0.003f, border, Bone.Hips, Bone.Hips, 0.3f, 2.3f, true, pleats);
                    _k.FlatRing(new V3(0f, hemY, 0.004f), sx + 0.004f, sz + 0.004f, CharacterPalette.Shade(border, 0.85f), Bone.Hips, Bone.Hips, 0.3f, 2.3f, true, pleats);
                    _k.FlatRing(new V3(0f, hemY - 0.004f, 0.004f), sx - 0.004f, sz - 0.004f, CharacterPalette.Shade(border, 0.7f), Bone.Hips, Bone.Hips, 0.3f, 2.3f, true, pleats);
                    _k.EndLoft(_k.Seg(22, 6), LoftCap.None, LoftCap.None);
                }
                if (Lod >= 2) return;
                if (_g.SkirtSlits)
                {
                    // Kurta side slits: darker lines up from the hem.
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float yTop = hip - 0.06f;
                        V3 a = new V3(s * ((hw + 0.022f) * flare + 0.004f), hemY + 0.005f, 0.004f);
                        V3 b = new V3(s * (hw + 0.024f + 0.004f), yTop, 0f);
                        _k.Capsule(a, b, 0.0035f, 0.0035f, CharacterPalette.Shade(dress, 0.6f), Bone.Hips, Bone.Hips, 4, 2, false, LoftCap.None, LoftCap.None);
                    }
                }
                if (t == OutfitItem.Chuba)
                {
                    // The pangden: a striped apron from the waist to the shins (seven colours, repeating).
                    int rows = Lod == 0 ? 15 : 6;
                    var rowRgb = new uint[rows - 1];
                    float y0 = hemY + 0.12f, y1 = waist - 0.01f;
                    _k.BeginGrid(rows, 5);
                    for (int i = 0; i < rows; i++)
                    {
                        float tt = i / (float)(rows - 1);
                        float y = y0 + (y1 - y0) * tt;
                        float f = CharMath.Clamp01((hip - y) / Math.Max(0.1f, hip - hemY));
                        float grow = 1f + (flare - 1f) * MathF.Pow(f, 0.8f);
                        float z = (0.12f + 0.3f * _m.Belly) * (y > hip ? 1f : grow) + 0.01f + (y > hip ? 0.0f : 0.012f * f);
                        float half = 0.1f + 0.03f * (1f - tt);
                        for (int j = 0; j < 5; j++)
                        {
                            float x = -half + 2f * half * j / 4f;
                            float zz = z - 0.25f * x * x / Math.Max(0.1f, half);
                            _k.GridSet(i, j, 5, new V3(x, y, zz), dress, Bone.Hips, Bone.SkirtF, 0.6f);
                        }
                        if (i < rows - 1) rowRgb[i] = CharacterPalette.Pangden[(i + _torso.Pattern) % CharacterPalette.Pangden.Length];
                    }
                    _k.EndGridColours(rows, 5, false, new V3(0f, y0, 0f), V3.Up, null, rowRgb);
                }
            }
        }
    }
}
