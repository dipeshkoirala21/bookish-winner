using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Characters
{
    public static partial class HumanoidMesher
    {
        private sealed partial class Builder
        {
            // ----- Limbs as lofts -----------------------------------------------------------------------------------

            /// <summary>
            /// A limb segment as a loft: the covered part (garment colour, a little fatter, with soft folds near the
            /// joint) up to <paramref name="cover"/> of its length, then bare skin, with a crisp edge and an optional cuff
            /// (1 rib band, 2 rolled, 3 shirt cuff, 4 hem band) where the cover ends.
            /// </summary>
            private void Limb(V3 a, V3 b, float ra, float rb, float cover, uint coverRgb, uint bareRgb, float extra, Bone bone, Bone parent, bool bulge,
                              LoftCap startCap, LoftCap endCap, float[] radiusCurve = null, byte cuff = 0, uint cuffRgb = 0, bool foldsAtEnd = false,
                              bool foldsAtStart = false)
            {
                // The mid-distance body keeps round limbs: eight sides on the arms, seven on the legs (the far body four).
                bool leg = bone >= Bone.ThighL && bone <= Bone.ToeR;
                int seg = _k.Seg(11, Mid ? (leg ? 7 : 8) : Lod >= 2 ? 4 : 5), rings = Lod >= 2 ? 2 : Lod == 1 ? 3 : 7;
                if (Lod >= 2)
                {
                    // The crowd body: joints hide the ends (shoulders in the torso, knees in the bulges, ankles in shoes).
                    startCap = LoftCap.None;
                    endCap = LoftCap.None;
                    cuff = 0;
                }
                V3 dir = b - a;
                V3 side = V3.Right;
                cover = CharMath.Clamp01(cover);
                _k.BeginLoft();
                float cuffLen = cuff == 2 ? 0.1f : cuff == 3 ? 0.11f : cuff == 1 ? 0.08f : cuff == 4 ? 0.05f : 0f;
                for (int i = 0; i < rings; i++)
                {
                    float t = i / (float)(rings - 1);
                    if (cover > 0.01f && cover < 0.999f && i > 0)
                    {
                        float tp = (i - 1) / (float)(rings - 1);
                        if (tp < cover && t >= cover)
                        {
                            if (cuffLen > 0f && Lod == 0)
                            {
                                float c0 = Math.Max(tp + 0.002f, cover - cuffLen);
                                RingAt(a, dir, side, ra, rb, c0 - 0.002f, true, coverRgb, bareRgb, extra, bone, parent, bulge, radiusCurve, 0f, foldsAtEnd, foldsAtStart);
                                RingAt(a, dir, side, ra, rb, c0, true, cuffRgb, bareRgb, extra, bone, parent, bulge, radiusCurve, cuff == 2 ? 0.006f : 0.002f, false, false);
                                RingAt(a, dir, side, ra, rb, cover - 0.003f, true, cuffRgb, bareRgb, extra, bone, parent, bulge, radiusCurve, cuff == 2 ? 0.007f : 0.003f, false, false);
                                RingAt(a, dir, side, ra, rb, cover - 0.0005f, true, CharacterPalette.Shade(cuffRgb, 0.8f), bareRgb, extra, bone, parent, bulge, radiusCurve, 0.0f, false, false);
                            }
                            if (Lod >= 2)
                            {
                                // The far body: one ring at the hem; the colour blends across the next span.
                                RingAt(a, dir, side, ra, rb, cover, true, coverRgb, bareRgb, extra, bone, parent, bulge, radiusCurve, 0f, false, false);
                            }
                            else
                            {
                                if (!(cuffLen > 0f && Lod == 0))
                                    RingAt(a, dir, side, ra, rb, cover - 0.001f, true, coverRgb, bareRgb, extra, bone, parent, bulge, radiusCurve, 0f, false, false);
                                RingAt(a, dir, side, ra, rb, cover, false, coverRgb, bareRgb, extra, bone, parent, bulge, radiusCurve, 0f, false, false);
                            }
                        }
                    }
                    bool covered = t < cover || cover >= 0.999f;
                    uint col = covered ? coverRgb : bareRgb;
                    if (covered && cover >= 0.999f && cuffLen > 0f && t > 1f - cuffLen && Lod == 0) col = cuffRgb;
                    RingAt(a, dir, side, ra, rb, t, covered, col, bareRgb, extra, bone, parent, bulge, radiusCurve, 0f, foldsAtEnd, foldsAtStart);
                }
                _k.EndLoft(seg, startCap, endCap);
            }

            private void RingAt(V3 a, V3 dir, V3 side, float ra, float rb, float t, bool covered, uint rgb, uint bareRgb, float extra, Bone bone,
                                Bone parent, bool bulge, float[] radiusCurve, float proud, bool foldsAtEnd, bool foldsAtStart)
            {
                float r = ra + (rb - ra) * t;
                if (radiusCurve != null)
                {
                    float f = t * (radiusCurve.Length - 1);
                    int i = Math.Min(radiusCurve.Length - 2, (int)f);
                    r *= CharMath.Lerp(radiusCurve[i], radiusCurve[i + 1], f - i);
                }
                if (bulge && t > 0.8f) r *= 1f + 0.15f * MathF.Sin((t - 0.8f) / 0.2f * 1.5707963f);
                if (covered)
                {
                    r += extra + proud;
                    // Soft cloth folds gather at the bent joint (inside of the elbow, back of the knee).
                    if (Lod == 0 && (foldsAtEnd && t > 0.6f || foldsAtStart && t < 0.35f)) r *= 1f + 0.035f * MathF.Sin(t * 37f);
                }
                float w = t < 0.25f ? 0.5f + 2f * t : 1f;
                _k.Ring(a + dir * t, dir, side, r, r, rgb, bone, parent, w);
            }

            // ----- Arms and hands -----------------------------------------------------------------------------------

            private void Arms()
            {
                float s0 = 0.056f * (_m.ShoulderW / 0.42f);
                bool gloves = (_r.Accents & CharacterAccents.Gloves) != 0;
                for (int side = 0; side < 2; side++)
                {
                    float s = side == 0 ? -1f : 1f;
                    int o = side * 4;
                    float upperCover = side == 0 ? _g.UpperL : _g.UpperR, foreCover = side == 0 ? _g.ForeL : _g.ForeR;
                    if (upperCover < 0.999f) foreCover = 0f;
                    uint sleeve = side == 0 ? _g.SleeveL : _g.SleeveR;
                    V3 shoulder = _sk.BindPosition[8 + o], elbow = _sk.BindPosition[9 + o], wrist = _sk.BindPosition[10 + o];
                    Bone upper = (Bone)(8 + o), fore = (Bone)(9 + o);
                    Mat(upperCover > 0f ? MaterialChannel.Fabric : MaterialChannel.Skin);
                    // A round shoulder joins the arm to the torso.
                    if (Lod < 2)
                        _k.Sphere(shoulder + new V3(-s * 0.006f, 0.004f, 0f), s0 + (upperCover > 0f ? 0.012f + _g.Bulk : 0f), upperCover > 0f ? sleeve : _skin,
                                  _k.Seg(10, 6), Lod == 0 ? 6 : 3, Bone.Chest);
                    float extra = 0.012f + _g.Bulk;
                    byte upperCuff = foreCover <= 0f && upperCover > 0f && upperCover < 0.999f ? _g.Cuff : (byte)0;
                    if (upperCuff == 3) upperCuff = 4;
                    Limb(shoulder, elbow, s0, 0.045f, upperCover, sleeve, _skin, extra, upper, Bone.Chest, true, LoftCap.None, LoftCap.None, null,
                         upperCuff, _g.CuffRgb, true);
                    Mat(foreCover > 0f ? MaterialChannel.Fabric : MaterialChannel.Skin);
                    Limb(elbow, wrist + new V3(0f, -0.006f, 0.002f), 0.047f, 0.031f, foreCover, sleeve, _skin, extra, fore, upper, false, LoftCap.Round, LoftCap.Round,
                         null, foreCover > 0f ? _g.Cuff : (byte)0, _g.CuffRgb, false, true);
                    if (_g.Baffles && Lod == 0 && upperCover > 0.99f)
                    {
                        // Quilted sleeves: soft rings along the arm.
                        for (int k = 1; k <= 2; k++)
                        {
                            V3 p = V3.Lerp(shoulder, elbow, k / 3f);
                            _k.BeginLoft();
                            for (int q = 0; q < 3; q++)
                            {
                                float rr = (s0 + (0.045f - s0) * k / 3f + extra) * (q == 1 ? 1.06f : 1f);
                                _k.Ring(p + (elbow - shoulder).Normalized * ((q - 1) * 0.012f), elbow - shoulder, V3.Right, rr, rr, sleeve, upper, Bone.Chest, 1f);
                            }
                            _k.EndLoft(_k.Seg(12, 5), LoftCap.None, LoftCap.None);
                        }
                    }
                    // The hands keep the build's level (the light LOD0 keeps its five-finger hands on LOD1 arms).
                    int armLod = _k.Lod;
                    if (_light) _k.Lod = _lod;
                    Hand(side, gloves);
                    _k.Lod = armLod;
                }
            }

            /// <summary>
            /// A hand hanging at the side, palm toward the body (P §2.3: 0.13 × 0.09 m, ×1.4 real). LOD0: a rounded palm,
            /// four two-joint fingers with a gentle rest curl and nails, and a two-joint thumb pointing forward, each
            /// skinned to its finger or thumb bones. LOD1: a mitten with a thumb. LOD2: one rounded shape.
            /// </summary>
            private void Hand(int side, bool gloves)
            {
                float s = side == 0 ? -1f : 1f;
                int o = side * 4;
                Bone hand = (Bone)(10 + o), f1 = (Bone)(17 + o), f2 = (Bone)(18 + o), t1 = (Bone)(15 + o), t2 = (Bone)(16 + o);
                V3 w = _sk.BindPosition[10 + o];
                float hs = _m.HandScale;
                uint col = gloves ? CharacterPalette.Glove : _skin;
                Mat(gloves ? MaterialChannel.Fabric : MaterialChannel.Skin);
                if (Lod >= 2)
                {
                    _k.Ellipsoid(w + new V3(s * 0.003f, -0.055f * hs, 0.006f * hs), new V3(0.022f, 0.06f * hs, 0.044f), Quat.Identity, col, Mid ? 6 : 5, 2, hand, 2.2f, f1, 0.7f);
                    // Mid-distance body: the thumb stands off the mitten, so the hand reads as a hand.
                    if (Mid)
                        _k.Ellipsoid(w + new V3(-s * 0.01f * hs, -0.04f * hs, 0.04f * hs), new V3(0.011f, 0.022f, 0.011f) * hs, Quat.Euler(-35f, 0f, 0f), col, 4, 2, t1, 2f, hand, 0.5f);
                    return;
                }
                V3 inward = new V3(-s, 0f, 0f);
                if (Lod == 1)
                {
                    _k.Ellipsoid(w + new V3(s * 0.002f, -0.062f * hs, 0.004f * hs), new V3(0.02f * hs, 0.064f * hs, 0.043f * hs), Quat.Euler(0f, 0f, s * -6f), col,
                                 _k.Seg(12, 6), _k.Seg(8, 5), hand, 2.6f, f1, 0.65f);
                    _k.Capsule(w + new V3(-s * 0.008f * hs, -0.024f * hs, 0.026f * hs), w + new V3(-s * 0.016f * hs, -0.066f * hs, 0.056f * hs), 0.0125f * hs,
                               0.011f * hs, col, t1, hand, 5, 2, false, LoftCap.None, LoftCap.Round);
                    return;
                }
                // Palm: a rounded pad from the wrist to the knuckles.
                _k.Ellipsoid(w + new V3(s * 0.001f, -0.038f * hs, 0.003f * hs), new V3(0.0175f * hs, 0.037f * hs, 0.037f * hs), Quat.Identity, col, 10, 7, hand, 2.5f,
                             Bone.Root, 1f);
                uint nail = gloves ? col : CharacterPalette.Mix(_skin, 0xFFE4DCu, 0.45f);
                // Fingers: index (front) to little finger (back), fanning out a little, with a gentle rest curl.
                for (int f = 0; f < 4; f++)
                {
                    float zf = (f == 0 ? 0.026f : f == 1 ? 0.0088f : f == 2 ? -0.0088f : -0.025f) * hs;
                    float len = (f == 0 ? 0.054f : f == 1 ? 0.06f : f == 2 ? 0.056f : 0.046f) * hs;
                    float r = (f == 0 ? 0.0102f : f == 1 ? 0.0106f : f == 2 ? 0.0100f : 0.0090f) * hs;
                    float knuckleY = -(f == 1 || f == 2 ? 0.07f : 0.067f) * hs;
                    float fan = (f == 0 ? 6f : f == 1 ? 1f : f == 2 ? -4f : -10f) * Quat.Deg2Rad;
                    V3 k0 = w + new V3(s * 0.001f, knuckleY + 0.012f * hs, zf);
                    float curl1 = 8f + 3f * f, curl2 = 16f + 4f * f;
                    V3 d1 = Fan(Curl(curl1, inward), fan), d2 = Fan(Curl(curl1 + curl2, inward), fan);
                    V3 kn = k0 + V3.Down * (0.012f * hs);
                    V3 p1 = kn + d1 * (0.54f * len), p2 = p1 + d2 * (0.46f * len);
                    _k.BeginLoft();
                    RingAlong(k0, V3.Down, r * 1.05f, col, hand, f1, 0.7f, inward);
                    RingAlong(kn, d1, r * 1.02f, col, f1, hand, 0.6f, inward);
                    RingAlong(p1, (d1 + d2).Normalized, r * 0.96f, col, f1, f2, 0.55f, inward);
                    RingAlong(p2, d2, r * 0.86f, col, f2, f2, 1f, inward);
                    _k.EndLoft(7, LoftCap.None, LoftCap.Round);
                    // Nail on the back of the fingertip.
                    V3 back = -inward;
                    V3 nc = p2 - d2 * (0.003f * hs) + back * (r * 0.8f);
                    Quat nr = Quat.FromTo(V3.Up, -d2);
                    _k.Ellipsoid(nc, new V3(r * 0.62f, 0.0055f * hs, r * 0.25f), nr * Quat.Euler(0f, s * 90f, 0f), nail, 4, 2, f2);
                }
                // Thumb: forward and down from the front of the palm, curling toward the fingers.
                V3 tb = w + new V3(-s * 0.008f * hs, -0.022f * hs, 0.024f * hs);
                V3 td1 = new V3(-s * 0.18f, -0.5f, 0.85f).Normalized, td2 = new V3(-s * 0.3f, -0.85f, 0.42f).Normalized;
                V3 tp1 = tb + td1 * (0.03f * hs), tp2 = tp1 + td2 * (0.028f * hs);
                float tr = 0.0118f * hs;
                _k.BeginLoft();
                RingAlong(tb - td1 * 0.004f, td1, tr * 1.2f, col, hand, t1, 0.6f, inward);
                RingAlong(tb + td1 * (0.015f * hs), td1, tr * 1.08f, col, t1, hand, 0.8f, inward);
                RingAlong(tp1, (td1 + td2).Normalized, tr * 0.98f, col, t1, t2, 0.55f, inward);
                RingAlong(tp2, td2, tr * 0.86f, col, t2, t2, 1f, inward);
                _k.EndLoft(7, LoftCap.Round, LoftCap.Round);
                V3 tn = tp2 - td2 * (0.003f * hs) + new V3(s, 0f, 0.3f).Normalized * (tr * 0.78f);
                _k.Ellipsoid(tn, new V3(tr * 0.6f, 0.006f * hs, tr * 0.22f), Quat.FromTo(V3.Up, -td2) * Quat.Euler(0f, s * 70f, 0f), nail, 4, 2, t2);
            }

            /// <summary>Turns a finger direction about the hand's side axis to fan the fingers apart.</summary>
            private static V3 Fan(V3 d, float rad)
            {
                float c = MathF.Cos(rad), sn = MathF.Sin(rad);
                return new V3(d.X, d.Y * c + d.Z * sn, -d.Y * sn + d.Z * c).Normalized;
            }

            /// <summary>The finger direction: straight down, curled by <paramref name="deg"/> toward the palm side.</summary>
            private static V3 Curl(float deg, V3 inward)
            {
                float a = deg * Quat.Deg2Rad;
                return (V3.Down * MathF.Cos(a) + inward * MathF.Sin(a)).Normalized;
            }

            private void RingAlong(V3 c, V3 dir, float r, uint rgb, Bone b0, Bone b1, float w0, V3 inward)
            {
                V3 d = dir.Normalized;
                V3 x = V3.Cross(d, inward).Normalized;
                if (x.LengthSq < 0.5f) x = V3.Forward;
                V3 z = V3.Cross(x, d);
                _k.Ring(new LoftRing { C = c, AxisX = x, AxisZ = z, Rx = r, Rz = r * 0.94f, Exp = 2.2f, Rgb = rgb, B0 = b0, B1 = b1, W0 = w0 });
            }

            // ----- Legs ---------------------------------------------------------------------------------------------

            private void Legs()
            {
                OutfitItem t = _torso.Item, l = _legs.Item;
                bool hidden = CharacterRecipe.IsFullLength(t);
                float thighCover = _g.ThighCover, shinCover = _g.ShinCover;
                uint col = _g.Lower;
                if (hidden)
                {
                    // Under an ankle-length hem the legs still move: plain cloth tubes in the hem colour.
                    thighCover = 1f;
                    shinCover = 1f;
                }
                float[] curveT = null, curveS = null;
                if (_g.ThighLoose > 1.01f) curveT = new[] { _g.ThighLoose, _g.ThighLoose * 0.95f, 1f + (_g.ThighLoose - 1f) * 0.4f };
                if (_g.AnkleFit < 0.99f) curveS = new[] { 1.05f, 0.95f, _g.AnkleFit };
                byte cuff = _g.LegCuff == 1 ? (byte)1 : _g.LegCuff == 2 ? (byte)2 : (byte)0;
                uint cuffRgb = _g.LegCuff == 2 ? CharacterPalette.Shade(col, 1.12f) : CharacterPalette.Shade(col, 0.8f);
                MaterialChannel cloth = l == OutfitItem.Jeans ? MaterialChannel.Fabric : MaterialChannel.Fabric;
                for (int side = 0; side < 2; side++)
                {
                    int o = side * 4;
                    float s = side == 0 ? -1f : 1f;
                    V3 hipJ = _sk.BindPosition[23 + o], knee = _sk.BindPosition[24 + o], ankle = _sk.BindPosition[25 + o];
                    Bone thigh = (Bone)(23 + o), shin = (Bone)(24 + o);
                    float legScale = _m.ShinM / 0.27f;
                    float thighR = 0.076f * (_m.HipW / 0.34f), kneeR = 0.054f * Math.Min(1.05f, legScale);
                    Mat(thighCover > 0f ? cloth : MaterialChannel.Skin);
                    Limb(hipJ + new V3(0f, 0.03f, 0f), knee, thighR, kneeR, thighCover, col, _skin, 0.008f, thigh, Bone.Hips, true, LoftCap.Round, LoftCap.None, curveT,
                         thighCover < 0.999f && shinCover <= 0f ? cuff : (byte)0, cuffRgb, true);
                    Mat(shinCover > 0f ? cloth : MaterialChannel.Skin);
                    Limb(knee, ankle + new V3(0f, 0.02f, 0f), kneeR, 0.04f, shinCover, col, _skin, 0.008f, shin, thigh, false, LoftCap.Round, LoftCap.Round, curveS,
                         shinCover > 0f ? cuff : (byte)0, cuffRgb, false, true);
                    if (hidden || Lod > 0) continue;
                    LegDetails(side, s, hipJ, knee, ankle, thigh, shin, col);
                }
                if (Lod == 0 && !hidden) HipDetails(l, col);
            }

            /// <summary>Seams, creases, cargo pockets and the suruwal's gathered ankle on one leg.</summary>
            private void LegDetails(int side, float s, V3 hipJ, V3 knee, V3 ankle, Bone thigh, Bone shin, uint col)
            {
                OutfitItem l = _legs.Item;
                Mat(MaterialChannel.Fabric);
                float thighR = 0.076f * (_m.HipW / 0.34f);
                if (l == OutfitItem.Jeans && _g.ShinCover > 0.99f)
                {
                    // Outer side seams in a lighter thread.
                    uint seam = CharacterPalette.Mix(col, 0xE8C27Au, 0.35f);
                    V3 a = hipJ + new V3(s * (thighR + 0.009f), 0.0f, 0f), b = knee + new V3(s * 0.063f, 0f, 0f), c = ankle + new V3(s * 0.05f, 0.03f, 0f);
                    _k.Capsule(a, b, 0.0022f, 0.0022f, seam, thigh, Bone.Hips, 4, 2, false, LoftCap.None, LoftCap.None);
                    _k.Capsule(b, c, 0.0022f, 0.0022f, seam, shin, thigh, 4, 2, false, LoftCap.None, LoftCap.None);
                }
                if (_g.Crease)
                {
                    uint crease = CharacterPalette.Shade(col, 1.15f);
                    V3 a = hipJ + new V3(0f, -0.05f, thighR + 0.008f), b = knee + new V3(0f, 0f, 0.064f), c = ankle + new V3(0f, 0.04f, 0.052f);
                    _k.Capsule(a, b, 0.0025f, 0.0025f, crease, thigh, Bone.Hips, 4, 2, false, LoftCap.None, LoftCap.None);
                    _k.Capsule(b, c, 0.0025f, 0.0025f, crease, shin, thigh, 4, 2, false, LoftCap.None, LoftCap.None);
                }
                if (l == OutfitItem.TrekTrousers)
                {
                    // Cargo pockets on the outer thighs and stitched knee panels.
                    V3 p = V3.Lerp(hipJ, knee, 0.55f) + new V3(s * (thighR + 0.012f), 0f, 0.005f);
                    _k.Ellipsoid(p, new V3(0.012f, 0.05f, 0.042f), Quat.Identity, CharacterPalette.Shade(col, 0.9f), 8, 5, thigh, 4f);
                    _k.Ellipsoid(p + new V3(s * 0.006f, 0.04f, 0f), new V3(0.012f, 0.012f, 0.045f), Quat.Identity, CharacterPalette.Shade(col, 0.85f), 8, 4, thigh, 4f);
                    _k.Ellipsoid(knee + new V3(0f, 0.01f, 0.066f), new V3(0.04f, 0.035f, 0.006f), Quat.Identity, CharacterPalette.Shade(col, 0.88f), 8, 4, shin, 3f, thigh, 0.6f);
                }
                if (_g.LegCuff == 3 && _g.ShinCover > 0.99f)
                {
                    // The suruwal (or churidar) gathers in soft rings above the ankle.
                    for (int k = 0; k < 3; k++)
                    {
                        V3 c = ankle + new V3(0f, 0.035f + 0.022f * k, 0.002f);
                        float rr = 0.036f + 0.002f * k;
                        _k.BeginLoft();
                        for (int q = 0; q < 3; q++)
                            _k.Ring(c + new V3(0f, (q - 1) * 0.006f, 0f), V3.Up, V3.Right, rr * (q == 1 ? 1.12f : 1f), rr * (q == 1 ? 1.12f : 1f), CharacterPalette.Shade(col, q == 1 ? 1.04f : 0.92f),
                                    shin, shin, 1f);
                        _k.EndLoft(10, LoftCap.None, LoftCap.None);
                    }
                }
            }

            /// <summary>Front pockets, belt loops and back pockets of jeans and trousers.</summary>
            private void HipDetails(OutfitItem l, uint col)
            {
                if (_g.HemY > _m.HipJointY + 0.1f || l == OutfitItem.Jeans || l == OutfitItem.Trousers || l == OutfitItem.TrekTrousers)
                {
                    if (!(l == OutfitItem.Jeans || l == OutfitItem.Trousers || l == OutfitItem.TrekTrousers)) return;
                    if (_g.HemY < _m.HipJointY + 0.05f && _torso.Item != OutfitItem.None) return; // covered by the top
                    float hip = _m.HipJointY;
                    uint seam = l == OutfitItem.Jeans ? CharacterPalette.Mix(col, 0xE8C27Au, 0.35f) : CharacterPalette.Shade(col, 0.82f);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        int n = 4;
                        V3[] path = _k.PathBuffer(n, out float[] rx, out float[] rz);
                        for (int i = 0; i < n; i++)
                        {
                            float tt = i / (float)(n - 1);
                            path[i] = TorsoPoint(s * (0.42f + 0.32f * tt * tt), hip + 0.08f - 0.1f * tt, 0.003f, out V3 nn);
                            rx[i] = 0.0022f;
                            rz[i] = 0.0022f;
                        }
                        _k.Sweep(path, n, rx, rz, V3.Up, seam, 4, Bone.Hips, LoftCap.None, LoftCap.None);
                        TorsoPanel(s * 2.75f, hip - 0.01f, 0.08f, 0.085f, 0.003f, CharacterPalette.Shade(col, 0.9f));
                    }
                }
            }

            // ----- Feet ---------------------------------------------------------------------------------------------

            /// <summary>Foot length scale relative to the adult 0.27 m shoe.</summary>
            private float FootScale
            {
                get { return _m.FootL / 0.27f; }
            }

            private void Feet()
            {
                OutfitItem f = _feet.Item;
                uint upper = CharacterPalette.Colour(f, _feet.Colour, _skin);
                if (CharacterRecipe.IsFullLength(_torso.Item) && f == OutfitItem.None) f = OutfitItem.Chappal;
                for (int side = 0; side < 2; side++)
                {
                    int o = side * 4;
                    float s = side == 0 ? -1f : 1f;
                    V3 ankle = _sk.BindPosition[25 + o];
                    Bone foot = (Bone)(25 + o), toe = (Bone)(26 + o);
                    if (Lod >= 2)
                    {
                        // The crowd body: one shoe or foot each (the mid-distance body adds a sole in the shoe's sole colour or
                        // the chappal's strap colour).
                        uint shoe = f == OutfitItem.Barefoot || f == OutfitItem.None ? _skin : f == OutfitItem.Chappal ? _skin : upper;
                        Mat(f == OutfitItem.Barefoot || f == OutfitItem.Chappal ? MaterialChannel.Skin : MaterialChannel.Leather);
                        float fs = FootScale;
                        _k.Ellipsoid(new V3(ankle.X, 0.042f * fs, ankle.Z + 0.045f * fs), new V3(0.05f, 0.042f, 0.13f) * fs, Quat.Euler(-4f, 0f, 0f), shoe, Mid ? 7 : 6, 2, foot,
                                     2.4f, toe, 0.7f);
                        if (Mid && f != OutfitItem.Barefoot && f != OutfitItem.None)
                        {
                            Mat(MaterialChannel.Rubber);
                            uint sole = f == OutfitItem.Sneakers ? CharacterPalette.SoleWhite : f == OutfitItem.Chappal ? upper : f == OutfitItem.TrekBoots ? 0x2E2824u
                                : CharacterPalette.Shade(upper, 0.6f);
                            _k.Ellipsoid(new V3(ankle.X, 0.008f * fs, ankle.Z + 0.045f * fs), new V3(0.054f, 0.009f, 0.135f) * fs, Quat.Identity, sole, 6, 2, foot, 3f, toe, 0.7f);
                        }
                        continue;
                    }
                    switch (f)
                    {
                        case OutfitItem.Chappal:
                            Chappal(ankle, s, foot, toe, upper);
                            break;
                        case OutfitItem.Barefoot:
                        case OutfitItem.None:
                            BareFoot(ankle, s, foot, toe, 0f);
                            break;
                        case OutfitItem.TrekBoots:
                            Shoe(ankle, s, foot, toe, upper, 0x2E2824u, 0.022f, 0.14f, true, false);
                            break;
                        case OutfitItem.LeatherShoes:
                            Shoe(ankle, s, foot, toe, upper, CharacterPalette.Shade(upper, 0.6f), 0.012f, 0.075f, true, false);
                            break;
                        default:
                        {
                            uint sole = CharacterPalette.SoleWhite;
                            Shoe(ankle, s, foot, toe, upper, sole, 0.02f, 0.085f, true, true);
                            break;
                        }
                    }
                }
            }

            /// <summary>Ring positions along a shoe: dense round the heel and toe so the plan outline closes in a curve. The
            /// first and last rings sit a fraction of a millimetre inside the ends, where the outline is about an eighth of
            /// its width, so the end caps are tiny and the surface has already turned to face backward or forward.</summary>
            private static readonly float[] ShoeT = { 0.0011f, 0.006f, 0.02f, 0.045f, 0.08f, 0.13f, 0.21f, 0.33f, 0.46f, 0.58f, 0.7f, 0.8f, 0.88f, 0.94f, 0.975f, 0.993f, 0.9988f };
            private static readonly float[] ShoeT1 = { 0.0011f, 0.025f, 0.2f, 0.6f, 0.93f, 0.9988f };

            /// <summary>The ring Z axis of lofts that run forward along the foot (X right): with it the loft's caps face out
            /// of the ends (BodyKit's caps face along Z × X, the loft direction).</summary>
            private static readonly V3 LoftDown = V3.Down;

            /// <summary>Shoe length the profile is drawn for (the adult cartoon shoe).</summary>
            private const float ShoeLenM = 0.25f;

            /// <summary>
            /// Shoe shape along its length (t 0 heel → 1 toe tip), in metres for the adult 0.25 m shoe: the half width of
            /// the plan outline, the half height of the upper and the height of the upper's centre above the sole top. The
            /// plan is widest at the ball (t ≈ 0.62), 72% of that at the heel and 62% at the toe, and closes round both
            /// ends in circles of those half widths, so the heel is a rounded counter (never a knife edge) and the toe a
            /// rounded cap. The upper is high at the heel and instep and falls to the toe.
            /// </summary>
            private static void ShoeProfile(float t, out float w, out float h, out float y)
            {
                const float ball = 0.051f, heelW = 0.72f * ball, toeW = 0.62f * ball;
                float baseW = t < 0.62f ? CharMath.Lerp(heelW, ball, CharMath.SmoothStep(t / 0.62f))
                                        : CharMath.Lerp(ball, toeW, CharMath.SmoothStep((t - 0.62f) / 0.38f));
                float zh = t * ShoeLenM, zt = (1f - t) * ShoeLenM;
                float kh = zh < heelW ? MathF.Sqrt(Math.Max(0f, 1f - (1f - zh / heelW) * (1f - zh / heelW))) : 1f;
                float kt = zt < toeW ? MathF.Sqrt(Math.Max(0f, 1f - (1f - zt / toeW) * (1f - zt / toeW))) : 1f;
                w = baseW * (0.9f + 0.12f * t) * Math.Min(kh, kt);
                // Height: high at the heel and instep, falling to the toe; the heel counter stays upright.
                float rise = t < 0.35f ? 1f : 1f - 0.6f * (t - 0.35f) / 0.65f;
                float tip = t > 0.9f ? 1f - 0.45f * (t - 0.9f) / 0.1f : 1f;
                float heel = 0.9f + 0.1f * Math.Min(1f, t / 0.06f);
                h = 0.034f * rise * tip * heel;
                y = h * 0.95f;
            }

            /// <summary>
            /// A shoe as lofts along the foot: the sole (white midsole for sneakers, a dark lugged sole for trek boots, a
            /// thin welted sole for leather shoes) following the plan outline, the rounded upper with a toe cap, a padded
            /// collar seated on the upper's top edge round the ankle, a tongue, laces across the instep and a stitched side
            /// curve; a high shaft with lace hooks for boots. A gentle toe spring lifts the tip (P §2.3).
            /// </summary>
            private void Shoe(V3 ankle, float s, Bone foot, Bone toe, uint upper, uint sole, float soleH, float collarY, bool laces, bool sneaker)
            {
                float fs = FootScale;
                float heelZ = ankle.Z - 0.07f * fs, len = ShoeLenM * fs;
                bool boot = collarY > 0.12f;
                float[] ts = Lod == 0 ? ShoeT : ShoeT1;
                int seg = _k.Seg(10, 6);
                float sh = soleH * fs;
                // Sole.
                Mat(sneaker || boot ? MaterialChannel.Rubber : MaterialChannel.Leather);
                _k.BeginLoft();
                for (int i = 0; i < ts.Length; i++)
                {
                    float t = ts[i];
                    ShoeProfile(t, out float w, out float h, out float yc);
                    float spring = t > 0.75f ? (t - 0.75f) / 0.25f * 0.01f * fs : 0f;
                    _k.Ring(new LoftRing
                    {
                        C = new V3(ankle.X + s * 0.004f * t, spring + sh * 0.5f, heelZ + len * t), AxisX = V3.Right, AxisZ = LoftDown,
                        Rx = Math.Max(0.002f, w * fs + 0.0035f * Math.Min(1f, w * 60f)), Rz = sh * 0.5f, Exp = 3.2f, Rgb = sole,
                        B0 = t > 0.65f ? toe : foot, B1 = foot, W0 = t > 0.65f ? 0.7f : 1f,
                    });
                }
                _k.EndLoft(seg, LoftCap.Flat, LoftCap.Flat);
                // Upper: the toe cap in a lighter or contrasting colour.
                Mat(sneaker ? MaterialChannel.Fabric : MaterialChannel.Leather);
                uint cap = sneaker ? (CharacterPalette.Luma(upper) > 0.7f ? 0xE2E0DAu : CharacterPalette.Mix(upper, 0xFFFFFFu, 0.18f)) : CharacterPalette.Shade(upper, 0.94f);
                _k.BeginLoft();
                for (int i = 0; i < ts.Length; i++)
                {
                    float t = ts[i];
                    ShoeProfile(t, out float w, out float h, out float yc);
                    float spring = t > 0.75f ? (t - 0.75f) / 0.25f * 0.01f * fs : 0f;
                    _k.Ring(new LoftRing
                    {
                        C = new V3(ankle.X + s * 0.004f * t, sh + yc * fs + spring - 0.003f * fs, heelZ + len * t), AxisX = V3.Right, AxisZ = LoftDown,
                        Rx = Math.Max(0.002f, w * fs), Rz = Math.Max(0.002f, h * fs), Exp = 2.5f, Rgb = t > 0.8f ? cap : upper,
                        B0 = t > 0.65f ? toe : foot, B1 = foot, W0 = t > 0.65f ? 0.7f : 1f,
                    });
                }
                _k.EndLoft(seg, LoftCap.Flat, LoftCap.Flat);
                // The collar round the ankle opening: a padded lip seated on the upper's top edge (a boot shaft rises higher).
                if (Lod > 0 && !boot) return;
                ShoeProfile(0.2f, out float wc, out float hc, out float ycc);
                float rim = sh + (ycc + hc) * fs - 0.003f * fs;
                float rxC = Math.Min(wc * fs, 0.044f * fs), rzC = 0.047f * fs;
                _k.BeginLoft();
                int collarRings = boot ? 3 : 3;
                for (int k = 0; k < collarRings; k++)
                {
                    float y = boot ? rim - 0.01f * fs + (collarY - rim + 0.01f * fs) * k / 2f : k == 0 ? rim - 0.012f * fs : k == 1 ? rim + 0.003f * fs : rim + 0.007f * fs;
                    float grow = boot ? (k == 2 ? 1.06f : 1.02f) : k == 1 ? 1.06f : k == 2 ? 0.97f : 0.99f;
                    _k.Ring(new LoftRing
                    {
                        C = new V3(ankle.X, y, ankle.Z - 0.017f * fs), AxisX = V3.Right, AxisZ = V3.Forward, Rx = rxC * grow, Rz = rzC * grow, Exp = 2.2f,
                        Rgb = k == 0 ? upper : CharacterPalette.Shade(upper, 0.72f), B0 = boot && k == 2 ? (Bone)((int)foot - 1) : foot, B1 = foot,
                        W0 = boot && k == 2 ? 0.6f : 1f,
                    });
                }
                _k.EndLoft(seg, LoftCap.None, LoftCap.None);
                if (Lod > 0) return;
                // Tongue under the laces, laces across the instep.
                ShoeProfile(0.42f, out float w0, out float h0, out float y0);
                float instepZ = heelZ + len * 0.42f;
                float instepTop = sh + (y0 + h0) * fs - 0.003f * fs;
                _k.Ellipsoid(new V3(ankle.X + s * 0.002f, instepTop - 0.002f * fs, instepZ - 0.012f * fs), new V3(0.02f, 0.007f, 0.036f) * fs, Quat.Euler(-20f, 0f, 0f),
                             CharacterPalette.Shade(upper, 0.9f), 6, 4, foot, 2.4f);
                if (laces)
                {
                    uint lace = sneaker ? CharacterPalette.Lace : CharacterPalette.Shade(upper, 0.55f);
                    for (int k = 0; k < 3; k++)
                    {
                        float t = 0.4f + 0.09f * k;
                        ShoeProfile(t, out float w, out float h, out float yc);
                        float z = heelZ + len * t, y = sh + (yc + h) * fs - 0.0045f * fs;
                        _k.Capsule(new V3(ankle.X - 0.02f * fs, y, z), new V3(ankle.X + 0.02f * fs, y, z + 0.004f), 0.0028f * fs, 0.0028f * fs, lace, foot, foot,
                                   4, 2, false, LoftCap.Flat, LoftCap.Flat);
                    }
                }
                if (sneaker)
                {
                    // A stitched side curve (a plain line, never a brand mark).
                    uint panel = CharacterPalette.Luma(upper) > 0.6f ? 0x9AA0A8u : CharacterPalette.Mix(upper, 0xFFFFFFu, 0.35f);
                    for (int sideOut = -1; sideOut <= 1; sideOut += 2)
                    {
                        int n = 4;
                        V3[] path = _k.PathBuffer(n, out float[] rx, out float[] rz);
                        for (int i = 0; i < n; i++)
                        {
                            float t = 0.12f + 0.6f * i / (n - 1);
                            ShoeProfile(t, out float w, out float h, out float yc);
                            float bump = 1f - 4f * (i / (float)(n - 1) - 0.5f) * (i / (float)(n - 1) - 0.5f);
                            // On the upper's side at its own height (the section is a superellipse of exponent 2.5).
                            float dy = -0.25f * h + 0.008f * bump;
                            float across = MathF.Pow(Math.Max(0f, 1f - MathF.Pow(Math.Min(1f, MathF.Abs(dy) / Math.Max(1e-4f, h)), 2.5f)), 0.4f);
                            path[i] = new V3(ankle.X + s * 0.004f * t + sideOut * (w * across * fs + 0.0008f), sh + (yc + dy) * fs - 0.003f * fs, heelZ + len * t);
                            rx[i] = 0.0028f * fs;
                            rz[i] = 0.0014f;
                        }
                        _k.Sweep(path, n, rx, rz, V3.Up, panel, 3, foot, LoftCap.Flat, LoftCap.Flat);
                    }
                }
                else if (boot)
                {
                    // Lace hooks up the shaft.
                    for (int k = 0; k < 3; k++)
                        for (int q = -1; q <= 1; q += 2)
                            _k.Sphere(new V3(ankle.X + q * 0.03f * fs, 0.1f + 0.018f * k, ankle.Z + 0.03f * fs), 0.004f, 0xB8B4A8u, 5, 3, (Bone)((int)foot - 1));
                }
            }

            // ----- The bare foot and the chappal ---------------------------------------------------------------------------

            /// <summary>Ring positions along the bare foot (heel to the toes' base), LOD0 and LOD1. LOD1 rounds the heel and the
            /// toes with a smaller ring past each end instead of domes.</summary>
            private static readonly float[] FootT = { 0f, 0.07f, 0.17f, 0.3f, 0.44f, 0.57f, 0.68f, 0.76f, 0.8f };
            private static readonly float[] FootT1 = { -0.045f, 0.02f, 0.3f, 0.6f, 0.8f, 0.86f };

            /// <summary>The bare foot's length (heel to the toes' base line, adult, at <see cref="ToeBaseT"/> = 0.8 of it), the
            /// station where the toes begin, and the toe line's slant back from the big toe to the little toe.</summary>
            private const float FootLenM = 0.228f, ToeBaseT = 0.8f, ToeLineDeg = 24f;

            /// <summary>
            /// The bare foot's section at <paramref name="t"/> (0 heel → <see cref="ToeBaseT"/> the toes' base), in metres
            /// for the adult foot: half width (narrow heel, widest across the ball), half height (the instep high behind,
            /// thinning to the toes) and the centre's shift toward the outside (the inner arch curves in).
            /// </summary>
            private static void FootSection(float t, out float w, out float h, out float outward)
            {
                float u = Math.Min(1f, t / ToeBaseT);
                w = u < 0.78f ? CharMath.Lerp(0.027f, 0.046f, CharMath.SmoothStep(u / 0.78f)) : CharMath.Lerp(0.046f, 0.04f, (u - 0.78f) / 0.22f);
                h = u < 0.35f ? CharMath.Lerp(0.025f, 0.036f, CharMath.SmoothStep(u / 0.35f)) : CharMath.Lerp(0.036f, 0.0115f, CharMath.SmoothStep((u - 0.35f) / 0.65f));
                // The inner arch curves in through the middle; the toes' end narrows toward the big toe's side.
                outward = 0.005f * MathF.Sin(MathF.PI * u) - (u > 0.78f ? 0.006f * (u - 0.78f) / 0.22f : 0f);
            }

            /// <summary>The bare foot's surface at <paramref name="t"/> and across-position <paramref name="across"/> (−1 the
            /// inner edge, 1 the outer, 0 the middle) on its upper side, for a foot standing <paramref name="lift"/> above
            /// the ground (the same section the foot is lofted from, so straps lie on it).</summary>
            private V3 FootTop(V3 ankle, float s, float lift, float t, float across)
            {
                float fs = FootScale;
                FootSection(t, out float w, out float h, out float outward);
                float heelZ = ankle.Z - 0.052f * fs;
                float a = Math.Max(-1f, Math.Min(1f, across));
                const float e = FootExp;
                float up = MathF.Pow(Math.Max(0f, 1f - MathF.Pow(MathF.Abs(a), e)), 1f / e);
                float x = ankle.X + s * (outward * fs) + s * a * w * fs;
                return new V3(x, lift + h * fs + up * h * fs + 0.002f, heelZ + FootLenM * fs * t);
            }

            /// <summary>The bare foot's outward surface normal at <paramref name="t"/> and <paramref name="across"/> on its upper
            /// side (from the cross-section's superellipse; the slope along the foot is small and left out).</summary>
            private V3 FootNormal(float s, float t, float across)
            {
                FootSection(t, out float w, out float h, out _);
                const float e = FootExp;
                float a = Math.Max(-1f, Math.Min(1f, across));
                float up = MathF.Pow(Math.Max(0f, 1f - MathF.Pow(MathF.Abs(a), e)), 1f / e);
                float nx = s * Math.Sign(a) * MathF.Pow(MathF.Abs(a), e - 1f) / w, ny = MathF.Pow(up, e - 1f) / h;
                var nrm = new V3(nx, ny, 0f);
                return nrm.LengthSq > 1e-12f ? nrm.Normalized : V3.Up;
            }

            private readonly V3[] _strapNrm = new V3[16];

            /// <summary>Superellipse exponent of the bare foot's cross-section (2 = round; a little fuller for the sole).</summary>
            private const float FootExp = 2.2f;

            /// <summary>
            /// A bare foot: a rounded lofted foot (narrow round heel, inner arch, high instep, wide ball) closed at both ends,
            /// the ankle with its two ankle bones (LOD0), and five toes seated into the front of the foot, the big toe biggest,
            /// their tips stepping back to the little toe, with nails on the first three. <paramref name="lift"/> raises it
            /// onto a sandal's footbed.
            /// </summary>
            private void BareFoot(V3 ankle, float s, Bone foot, Bone toe, float lift)
            {
                Mat(MaterialChannel.Skin);
                float fs = FootScale;
                float heelZ = ankle.Z - 0.052f * fs, len = FootLenM * fs;
                float[] ts = Lod == 0 ? FootT : FootT1;
                _k.BeginLoft();
                for (int i = 0; i < ts.Length; i++)
                {
                    float t = ts[i];
                    FootSection(Math.Max(0f, Math.Min(ToeBaseT, t)), out float w, out float h, out float outward);
                    if (t < 0f)
                    {
                        w *= 0.55f;
                        h *= 0.7f;
                    }
                    else if (t > ToeBaseT)
                    {
                        w *= 0.7f;
                        h *= 0.65f;
                    }
                    // The front turns oblique, the outer side further back, along the toe line.
                    float yaw = ToeLineDeg * Quat.Deg2Rad * CharMath.Clamp01((t - 0.7f) / (ToeBaseT - 0.7f));
                    _k.Ring(new LoftRing
                    {
                        C = new V3(ankle.X + s * outward * fs, lift + h * fs + 0.002f, heelZ + len * t), AxisX = new V3(MathF.Cos(yaw), 0f, -s * MathF.Sin(yaw)),
                        AxisZ = LoftDown, Rx = w * fs, Rz = h * fs, Exp = FootExp, Rgb = _skin, B0 = t > 0.62f ? toe : foot, B1 = foot, W0 = t > 0.62f ? 0.7f : 1f,
                    });
                }
                // Closed at both ends: a round heel, and a flat front the toes sit against (LOD1, which has no toes, closes
                // both ends on its small end rings).
                _k.EndLoft(_k.Seg(11, 7), Lod == 0 ? LoftCap.Round : LoftCap.Flat, LoftCap.Flat);
                // Ankle into the shin.
                _k.Capsule(ankle + new V3(0f, 0.03f, 0f), new V3(ankle.X, lift + 0.03f * fs, ankle.Z - 0.01f), 0.036f, 0.038f * fs, _skin, foot, (Bone)((int)foot - 1), 8, 2,
                           false, LoftCap.None, LoftCap.None);
                if (Lod > 0) return;
                // The ankle bones: the inner one higher and further forward than the outer.
                uint bone = CharacterPalette.Shade(_skin, 1.04f);
                _k.Ellipsoid(new V3(ankle.X - s * 0.031f * fs, ankle.Y + 0.004f * fs, ankle.Z + 0.004f * fs), new V3(0.007f, 0.012f, 0.01f) * fs, Quat.Identity, bone, 5, 3, foot);
                _k.Ellipsoid(new V3(ankle.X + s * 0.032f * fs, ankle.Y - 0.006f * fs, ankle.Z - 0.008f * fs), new V3(0.007f, 0.011f, 0.0095f) * fs, Quat.Identity, bone, 5, 3, foot);
                // Toes across the front, the big toe on the inner side (toward the body).
                FootSection(ToeBaseT, out float wf, out _, out float of);
                float baseZ = heelZ + len * ToeBaseT, cx = ankle.X + s * of * fs, tanLine = MathF.Tan(ToeLineDeg * Quat.Deg2Rad);
                float inner = cx - s * wf * fs;
                uint nail = CharacterPalette.Mix(_skin, 0xFFE4DCu, 0.45f);
                float acc = 0f;
                for (int k = 0; k < 5; k++)
                {
                    float rx = (k == 0 ? 0.0132f : 0.0095f - 0.0008f * k) * fs;
                    float ry = rx * (k == 0 ? 0.9f : 0.95f);
                    float rz = (k == 0 ? 0.022f : 0.0165f - 0.0013f * k) * fs;
                    acc += rx;
                    float x = inner + s * acc;
                    acc += rx + 0.001f * fs;
                    // Along the oblique front (the toe line steps back to the little toe), the back of each toe sunk into
                    // the foot's front, the big toe reaching furthest.
                    float lineZ = baseZ - s * (x - cx) * tanLine;
                    var c = new V3(x, lift + ry + 0.0006f, lineZ + (k == 0 ? 0.6f : 0.5f) * rz);
                    _k.Ellipsoid(c, new V3(rx, ry, rz), Quat.Euler(4f, 0f, 0f), _skin, k == 0 ? 7 : 6, k == 0 ? 4 : 3, toe);
                    if (k < 3)
                        _k.Ellipsoid(c + new V3(0f, ry * 0.62f, rz * 0.5f), new V3(rx * 0.62f, ry * 0.22f, rz * 0.42f), Quat.Euler(-14f, 0f, 0f), nail, 5, 2, toe);
                }
            }

            /// <summary>The chappal's sole outline: half width along its length (rounded heel and toe in plan).</summary>
            private static float ChappalWidth(float t)
            {
                float w = t < 0.65f ? CharMath.Lerp(0.039f, 0.05f, CharMath.SmoothStep(t / 0.65f)) : CharMath.Lerp(0.05f, 0.04f, (t - 0.65f) / 0.35f);
                const float lenM = 0.25f;
                float zh = t * lenM, zt = (1f - t) * lenM;
                float kh = zh < 0.039f ? MathF.Sqrt(Math.Max(0f, 1f - (1f - zh / 0.039f) * (1f - zh / 0.039f))) : 1f;
                float kt = zt < 0.04f ? MathF.Sqrt(Math.Max(0f, 1f - (1f - zt / 0.04f) * (1f - zt / 0.04f))) : 1f;
                return w * Math.Min(kh, kt);
            }

            /// <summary>Ring positions along the chappal (dense at the rounded ends).</summary>
            private static readonly float[] ChappalT = { 0.012f, 0.05f, 0.12f, 0.3f, 0.5f, 0.68f, 0.84f, 0.94f, 0.988f };
            private static readonly float[] ChappalT1 = { 0.012f, 0.12f, 0.5f, 0.84f, 0.988f };

            /// <summary>
            /// The everyday rubber chappal (the valley's commonest footwear): a coloured sole under a white footbed, both with
            /// a rounded heel and toe, the bare foot on top, and the Y strap lying on the foot: the post rises between the
            /// first two toes and the two straps run back over the top of the forefoot to the sole's sides at the arch.
            /// </summary>
            private void Chappal(V3 ankle, float s, Bone foot, Bone toe, uint strap)
            {
                float fs = FootScale;
                float heelZ = ankle.Z - 0.083f * fs, len = 0.265f * fs;
                Mat(MaterialChannel.Rubber);
                float[] ts = Lod == 0 ? ChappalT : ChappalT1;
                const float soleTop = 0.017f;
                for (int layer = Lod == 0 ? 0 : 1; layer < 2; layer++)
                {
                    _k.BeginLoft();
                    for (int i = 0; i < ts.Length; i++)
                    {
                        float t = ts[i];
                        float w = ChappalWidth(t) * fs;
                        float y = layer == 0 ? 0.006f : 0.0145f;
                        _k.Ring(new LoftRing
                        {
                            C = new V3(ankle.X + s * 0.005f * t, y, heelZ + len * t), AxisX = V3.Right, AxisZ = LoftDown, Rx = layer == 0 ? w + 0.0015f : w,
                            Rz = layer == 0 ? 0.006f : 0.0025f, Exp = 3.4f, Rgb = layer == 0 ? strap : 0xF2F0EAu, B0 = t > 0.65f ? toe : foot, B1 = foot,
                            W0 = t > 0.65f ? 0.7f : 1f,
                        });
                    }
                    _k.EndLoft(_k.Seg(8, 6), LoftCap.Flat, LoftCap.Flat);
                }
                BareFoot(ankle, s, foot, toe, soleTop);
                // The Y strap on the foot's surface.
                Mat(MaterialChannel.Rubber);
                float half = 0.0055f * fs, thick = 0.0022f * fs;
                // The post: between the big toe and the second toe, from the footbed up to the top of the toes' base.
                FootSection(ToeBaseT, out float wf, out _, out _);
                float acrossPost = -1f + (2f * 0.0128f + 0.0006f) / wf;
                V3 postTop = FootTop(ankle, s, soleTop, ToeBaseT - 0.02f, acrossPost) + new V3(0f, thick, 0f);
                V3 postBase = new V3(postTop.X, soleTop + 0.002f, postTop.Z + 0.004f * fs);
                int n = Lod == 0 ? 6 : 2;
                V3[] path = _k.PathBuffer(Math.Max(n + 1, 3), out float[] rx, out float[] rz);
                path[0] = postBase;
                path[1] = postTop;
                rx[0] = rx[1] = 0.003f * fs;
                rz[0] = rz[1] = 0.003f * fs;
                if (Lod == 0) _k.Sweep(path, 2, rx, rz, V3.Forward, strap, 5, toe, LoftCap.Flat, LoftCap.Round, foot, 0.6f);
                // The two straps: from the post over the forefoot to the sole's sides at the arch, each a flat band whose
                // thickness stands along the foot's surface normal (on top over the instep, outward down the sides).
                const float endT = 0.46f;
                int seg = Lod == 0 ? 5 : 4;
                for (int sideOut = -1; sideOut <= 1; sideOut += 2)
                {
                    _k.BeginLoft();
                    V3 prev = V3.Zero;
                    for (int i = 0; i <= n; i++)
                    {
                        V3 p, nrm;
                        if (i < n)
                        {
                            float v = i / (float)(n - 1);
                            float t = CharMath.Lerp(ToeBaseT - 0.02f, endT, v);
                            float across = CharMath.Lerp(acrossPost, sideOut * 1f, MathF.Pow(v, 0.75f));
                            nrm = FootNormal(s, t, across);
                            p = FootTop(ankle, s, soleTop, t, across) + nrm * (thick + 0.0008f);
                        }
                        else
                        {
                            // The last point drops onto the sole's edge.
                            FootSection(endT, out float we, out _, out float oe);
                            nrm = new V3(s * sideOut, 0.35f, 0f).Normalized;
                            p = new V3(ankle.X + s * oe * fs + s * sideOut * (we * fs + thick + 0.0015f), soleTop + 0.0025f, ankle.Z - 0.052f * fs + FootLenM * fs * endT);
                        }
                        path[i] = p;
                        _strapNrm[i] = nrm;
                    }
                    for (int i = 0; i <= n; i++)
                    {
                        V3 d = i == 0 ? path[1] - path[0] : i == n ? path[n] - path[n - 1] : path[i + 1] - path[i - 1];
                        _k.Ring(path[i], d, _strapNrm[i], thick, half, strap, i < n / 2 ? toe : foot, foot, i < n / 2 ? 0.7f : 1f, 4f);
                    }
                    _k.EndLoft(seg, Lod == 0 ? LoftCap.Round : LoftCap.Flat, LoftCap.Flat);
                }
            }
        }
    }
}
