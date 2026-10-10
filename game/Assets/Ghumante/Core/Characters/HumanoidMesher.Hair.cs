using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Characters
{
    public static partial class HumanoidMesher
    {
        private sealed partial class Builder
        {
            // ----- Hair ---------------------------------------------------------------------------------------------

            /// <summary>Hair colour with a soft shine band where the surface faces up and forward (the toon highlight).</summary>
            private uint HairShine(V3 n)
            {
                float band = G(n.Y - 0.62f, 0.02f, n.Z - 0.35f, 0.25f);
                return band > 0.5f ? CharacterPalette.Mix(_hair, 0xFFFFFFu, CharacterPalette.Luma(_hair) > 0.6f ? 0.12f : 0.2f) : _hair;
            }

            /// <summary>
            /// An offset shell of the head above a hairline: front, side and back hairline heights in metres from the
            /// head centre, a crown scale (volume), an optional side parting at <paramref name="partX"/> (0 = none) and a
            /// lift of the front (quiff). Rows run from the crown down to the hairline.
            /// </summary>
            private void HairCap(float offset, float frontY, float sideY, float backY, float crownScale, float partX = 0f, float frontLift = 0f,
                                 uint? colour = null)
            {
                Mat(MaterialChannel.Hair);
                int cols = Lod == 0 ? 24 : Lod == 1 ? 12 : 7, rows = Lod == 0 ? 9 : Lod == 1 ? 5 : 2;
                float e = HeadExponent, pe = 2f / e;
                var r = new V3(_hr.X + offset, (_hr.Y + offset) * crownScale, _hr.Z + offset);
                V3 c = _hc + new V3(0f, (_hr.Y + offset) * (1f - crownScale) * 0.5f, 0f);
                uint baseRgb = colour ?? _hair;
                _k.BeginGrid(rows + 1, cols);
                for (int row = 0; row <= rows; row++)
                {
                    for (int j = 0; j < cols; j++)
                    {
                        float a = 6.28318530718f * j / cols;
                        float sa = MathF.Sin(a), ca = MathF.Cos(a);
                        float fw = MathF.Pow(Math.Max(0f, ca), 1.4f), bw = MathF.Pow(Math.Max(0f, -ca), 1.4f);
                        float lineY = sideY + (frontY - sideY) * fw + (backY - sideY) * bw;
                        if (Lod == 0 && !colour.HasValue && row == rows)
                        {
                            // A tufted edge: locks at the fringe and short points round the sides and back.
                            float tuft = 0.5f + 0.5f * MathF.Sin(a * 13f + _v0 * 6.28f);
                            lineY -= (fw * 0.016f + (1f - fw) * 0.008f) * _hs * tuft * tuft;
                        }
                        float yN = Math.Max(-0.98f, Math.Min(0.98f, lineY / r.Y));
                        float phiMax = MathF.Acos(BodyKit.SPow(yN, e / 2f));
                        float phi = row == 0 ? 0.03f : phiMax * row / rows;
                        float sp = MathF.Sin(phi), cp = MathF.Cos(phi);
                        float x = r.X * BodyKit.SPow(sp, pe) * BodyKit.SPow(sa, pe);
                        float y = r.Y * BodyKit.SPow(cp, pe);
                        float z = r.Z * BodyKit.SPow(sp, pe) * BodyKit.SPow(ca, pe);
                        if (frontLift != 0f && ca > 0f)
                        {
                            float k = fw * MathF.Sin(phi / Math.Max(0.2f, phiMax) * MathF.PI * 0.85f);
                            y += frontLift * k;
                            z += frontLift * 0.5f * k;
                        }
                        // Jaw taper also narrows the sides of long hair below the cheeks.
                        float ny = y / r.Y;
                        x *= JawX(ny);
                        var p = c + new V3(x, y, z);
                        var n = new V3(BodyKit.SPow(x / r.X, e - 1f) / r.X, BodyKit.SPow(y / r.Y, e - 1f) / r.Y, BodyKit.SPow(z / r.Z, e - 1f) / r.Z).Normalized;
                        uint col = Lod < 2 ? (colour.HasValue ? baseRgb : HairShine(n)) : baseRgb;
                        if (partX != 0f && Lod == 0 && row > 0 && ca > -0.2f && MathF.Abs(x - partX) < 0.006f * _hs) col = CharacterPalette.Shade(baseRgb, 0.7f);
                        _k.GridSet(row, j, cols, p, col, Bone.Head, Bone.Head, 1f, row == rows ? 0.8f : 1f);
                    }
                }
                _k.EndGrid(rows + 1, cols, true, c, V3.Zero, null, true, false);
            }

            private void Hair()
            {
                int style = _r.Hair;
                bool covered = HasHeadwear;
                bool helmet = _o.Headwear == HeadwearMode.Helmet;
                float hs = _hs;
                V3 c = _hc;
                if (style == CharacterRecipe.HairShaved)
                {
                    if (covered) return;
                    // The shaved head: a faint shadow of stubble over the scalp.
                    HairCap(0.0015f * hs, 0.11f * hs, 0.0f, -0.07f * hs, 1f, 0f, 0f, CharacterPalette.Mix(_skin, _hair, 0.22f));
                    return;
                }
                if (style == CharacterRecipe.HairReceding)
                {
                    // A horseshoe of short hair round the sides and back; the crown is bare.
                    HairBand(0.006f * hs, 0.03f * hs, -0.045f * hs, -0.1f * hs);
                    return;
                }
                if (style == 2 && covered) return; // a buzz cut disappears under a hat
                float offset = covered ? 0.004f : style == 2 ? 0.005f : style == CharacterRecipe.HairQuiff ? 0.01f : 0.011f;
                bool longBack = style == 5 || style == 6 || style == 11 || style == CharacterRecipe.HairDreadlocks;
                float front = style == 2 ? 0.115f * hs : 0.095f * hs;
                float side = style == 5 || style == 11 ? -0.12f * hs : -0.03f * hs;
                float back = longBack ? -0.17f * hs : -0.125f * hs;
                bool pulledBack = style == 7 || style == 8 || style == 9 || style == 10 || style == 12 || style == CharacterRecipe.HairTwoPlaits;
                if (pulledBack) front = 0.105f * hs;
                // Under a namlo the strap presses the hair flat across the forehead and temples: no curls, spikes, fringe
                // locks or quiff stand out through it.
                bool pressed = !covered && CharacterRecipe.UsesNamlo(_back.Item) && Lod < 2;
                float crown = covered ? 0.9f : pressed ? 1f : style == 3 ? 1.04f : style == CharacterRecipe.HairQuiff ? 0.98f : 1f;
                float partX = style == 1 ? 0.045f * hs : pulledBack || style == 6 || style == 5 ? 0.0001f : 0f;
                float lift = style == CharacterRecipe.HairQuiff && !covered && !pressed ? 0.03f * hs : 0f;
                uint? capColour = style == 2 ? CharacterPalette.Mix(_hair, _skin, 0.3f) : (uint?)null;
                HairCap(offset, front, side, back, crown, partX, lift, capColour);
                if (style == CharacterRecipe.HairQuiff && Lod < 2)
                {
                    // Faded short sides: a thin lighter band above the ears.
                    HairBand(0.004f * hs, -0.005f * hs, -0.06f * hs, -0.13f * hs, CharacterPalette.Mix(_hair, _skin, 0.35f), true);
                }
                if ((_r.Accents & CharacterAccents.Pote) != 0 && pulledBack && !covered && Lod == 0)
                {
                    // Sindoor along the centre parting.
                    Mat(MaterialChannel.Plain);
                    for (int i = 0; i < 4; i++)
                    {
                        float t = i / 3f;
                        V3 p = c + new V3(0f, (0.17f - 0.04f * t) * hs + offset, (0.1f - 0.1f * t) * hs);
                        _k.Sphere(p, 0.006f * hs, CharacterPalette.Sindoor, 5, 3, Bone.Head);
                    }
                }
                if (Lod >= 2 && (FarDrop(2) || !(style == 9 || style == 10 || style == 12 || style == 7 || style == 6 || style == CharacterRecipe.HairDreadlocks))) return;
                Mat(MaterialChannel.Hair);
                switch (style)
                {
                    case 1:
                        if (!covered && !pressed) SideFringe();
                        break;
                    case 3:
                        if (covered || pressed) break;
                        int curls = Lod == 0 ? 18 : 10;
                        for (int i = 0; i < curls; i++)
                        {
                            float t = (i + 0.5f) / curls;
                            float phi = MathF.Acos(1f - 0.95f * t);
                            float th = i * 2.39996323f;
                            var d = new V3(MathF.Sin(phi) * MathF.Cos(th), MathF.Cos(phi), MathF.Sin(phi) * MathF.Sin(th));
                            if (d.Z > 0.55f && d.Y < 0.55f) d = new V3(d.X, d.Y + 0.25f, d.Z * 0.7f);
                            V3 p = c + new V3(d.X * (_hr.X + 0.016f), d.Y * (_hr.Y + 0.016f), d.Z * (_hr.Z + 0.016f));
                            _k.Sphere(p, (Lod == 0 ? 0.032f : 0.04f) * hs, HairShine(d), _k.Seg(7, 4), _k.Seg(5, 3), Bone.Head);
                        }
                        break;
                    case 4:
                        if (covered || pressed) break;
                        for (int i = 0; i < 9; i++)
                        {
                            float th = -1.4f + i * 0.35f;
                            var d = new V3(MathF.Sin(th) * 0.6f, 0.78f, -MathF.Cos(th) * 0.3f + 0.15f).Normalized;
                            V3 p = c + new V3(d.X * _hr.X, d.Y * _hr.Y, d.Z * _hr.Z);
                            _k.Cone(p - d * 0.02f, p + d * (0.07f + 0.015f * (i % 3)) * hs, 0.034f * hs, 0.002f, _hair, _k.Seg(7, 4), Bone.Head);
                        }
                        break;
                    case 5:
                        // A bob to the jaw: a skirt round the sides and back, and a straight fringe.
                        BackSheet(-0.02f, -0.2f, 0.17f, 0.04f, false);
                        if (!covered && !pressed) StraightFringe(0.07f * hs);
                        break;
                    case 6:
                        BackSheet(-0.04f, -0.5f, 0.16f, 0.045f, true);
                        break;
                    case 7:
                        Braid(new V3(0f, c.Y - 0.08f * hs, c.Z - _hr.Z - 0.01f), 0.4f, 11, true);
                        break;
                    case 8:
                        if (covered) break;
                        {
                            V3 tie = c + new V3(0f, 0.12f * hs, -0.18f * hs);
                            _k.Sphere(tie, 0.026f * hs, CharacterPalette.Shade(_hair, 0.7f), _k.Seg(7, 4), _k.Seg(5, 3), Bone.Head);
                            V3 a = tie + new V3(0f, 0.01f, -0.025f) * hs, b = c + new V3(0f, -0.22f * hs, -0.3f * hs);
                            _k.Curve(a, a + new V3(0f, 0.02f, -0.11f) * hs, b, _k.Seg(6, 3), 0.045f * hs, 0.012f * hs, 0.036f * hs, 0.01f * hs, V3.Right, _hair,
                                     _k.Seg(8, 5), Bone.Head, LoftCap.Round, LoftCap.Round, Bone.Neck, 0.7f);
                        }
                        break;
                    case 9:
                        if (helmet) break;
                        {
                            V3 bun = c + new V3(0f, -0.075f * hs, -_hr.Z - 0.035f * hs);
                            _k.Ellipsoid(bun, new V3(0.06f, 0.05f, 0.045f) * hs, Quat.Identity, HairShine(new V3(0f, 0.6f, -0.2f)), _k.Seg(10, 5), _k.Seg(7, 4), Bone.Head);
                            if (Lod == 0)
                            {
                                // A wrap of hair round the bun and a hairpin.
                                _k.BeginLoft();
                                _k.Ring(bun + new V3(0f, 0f, 0.012f) * hs, V3.Forward, V3.Right, 0.055f * hs, 0.045f * hs, CharacterPalette.Shade(_hair, 0.8f), Bone.Head, Bone.Head, 1f);
                                _k.Ring(bun + new V3(0f, 0f, -0.002f) * hs, V3.Forward, V3.Right, 0.061f * hs, 0.051f * hs, CharacterPalette.Shade(_hair, 0.8f), Bone.Head, Bone.Head, 1f);
                                _k.EndLoft(10, LoftCap.None, LoftCap.None);
                                Mat(MaterialChannel.Metal);
                                _k.Capsule(bun + new V3(-0.07f, 0.02f, -0.01f) * hs, bun + new V3(0.07f, 0.035f, -0.01f) * hs, 0.003f * hs, 0.003f * hs, CharacterPalette.Gold,
                                           Bone.Head, Bone.Head, 5, 2, false);
                            }
                        }
                        break;
                    case 10:
                        if (covered) break;
                        _k.Sphere(c + new V3(0f, 0.21f * hs, -0.03f * hs), 0.055f * hs, HairShine(V3.Up), _k.Seg(9, 4), _k.Seg(6, 3), Bone.Head);
                        _k.Ellipsoid(c + new V3(0f, 0.17f * hs, -0.025f * hs), new V3(0.028f, 0.012f, 0.028f) * hs, Quat.Identity, CharacterPalette.Ribbon, 8, 3, Bone.Head);
                        break;
                    case 11:
                    {
                        int waves = Lod == 0 ? 7 : 4;
                        for (int i = 0; i < waves; i++)
                        {
                            // Round the sides and back of the head (azimuth 0 is the face).
                            float th = 1.85f + i * (2.58f / (waves - 1));
                            var p0 = new V3(c.X + MathF.Sin(th) * 0.16f * hs, c.Y - 0.02f * hs, c.Z + MathF.Cos(th) * 0.17f * hs - 0.01f);
                            var p1 = p0 + new V3(MathF.Sin(th) * 0.03f, -0.12f, MathF.Cos(th) * 0.02f) * hs;
                            var p2 = p0 + new V3(MathF.Sin(th) * 0.01f, -0.2f, MathF.Cos(th) * 0.03f) * hs;
                            _k.Curve(p0, p1, p2, _k.Seg(5, 3), 0.035f * hs, 0.018f * hs, 0.02f * hs, 0.012f * hs, new V3(MathF.Cos(th), 0f, -MathF.Sin(th)), _hair,
                                     _k.Seg(6, 4), Bone.Head, LoftCap.Round, Lod == 0 ? LoftCap.Round : LoftCap.Flat, Bone.Neck, 0.8f);
                        }
                        break;
                    }
                    case 12:
                        if (covered) break;
                        for (int s = -1; s <= 1; s += 2)
                            _k.Sphere(c + new V3(s * 0.13f * hs, 0.14f * hs, -0.04f * hs), 0.066f * hs, HairShine(new V3(s * 0.3f, 0.8f, 0f)), _k.Seg(9, 4), _k.Seg(6, 3), Bone.Head);
                        break;
                    case CharacterRecipe.HairDreadlocks:
                        Dreadlocks(covered);
                        break;
                    case CharacterRecipe.HairTwoPlaits:
                        for (int s = -1; s <= 1; s += 2)
                            Braid(new V3(s * 0.12f * hs, c.Y - 0.1f * hs, c.Z - 0.1f * hs), 0.24f, 6, false, s);
                        break;
                    case CharacterRecipe.HairQuiff:
                        break;
                }
            }

            /// <summary>Swept locks of a side-parted fringe falling across the forehead.</summary>
            private void SideFringe()
            {
                V3 c = _hc;
                float hs = _hs;
                int n = Lod == 0 ? 4 : 2;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Math.Max(1, n - 1);
                    var a = c + new V3((0.045f - 0.01f * i) * hs, (0.19f - 0.01f * i) * hs, (0.04f + 0.03f * t) * hs);
                    var b = c + new V3((-0.11f + 0.02f * i) * hs, (0.1f + 0.012f * i) * hs, (0.16f - 0.01f * t) * hs);
                    var m = (a + b) * 0.5f + new V3(0f, 0.025f, 0.03f) * hs;
                    _k.Curve(a, m, b, _k.Seg(6, 3), 0.024f * hs, 0.012f * hs, 0.012f * hs, 0.006f * hs, V3.Forward, HairShine(new V3(0f, 0.7f, 0.5f)),
                             _k.Seg(7, 4), Bone.Head);
                }
            }

            /// <summary>A straight fringe across the forehead (bob).</summary>
            private void StraightFringe(float drop)
            {
                V3 c = _hc;
                _k.Ellipsoid(c + new V3(0f, 0.115f * _hs, 0.15f * _hs), new V3(0.14f, drop * 0.5f, 0.05f) * _hs * (1f / Math.Max(0.5f, _hs)),
                             Quat.Euler(-20f, 0f, 0f), HairShine(new V3(0f, 0.6f, 0.6f)), _k.Seg(12, 6), _k.Seg(6, 4), Bone.Head);
            }

            /// <summary>Long hair falling down the back: a curved lofted sheet from the back of the head to
            /// <paramref name="bottom"/> (metres below the head centre), wrapping round to the sides when
            /// <paramref name="sides"/>.</summary>
            private void BackSheet(float top, float bottom, float halfWidth, float thick, bool sides)
            {
                V3 c = _hc;
                float hs = _hs;
                int rings = Lod == 0 ? 6 : Lod == 1 ? 4 : 3;
                _k.BeginLoft();
                for (int i = 0; i < rings; i++)
                {
                    float t = i / (float)(rings - 1);
                    float y = c.Y + (top + (bottom - top) * t) * hs;
                    float z = c.Z - (_hr.Z * (1f - 0.35f * t) + 0.01f) * (1f - 0.1f * t) - 0.02f * t;
                    float w = halfWidth * hs * (1f - 0.15f * t * t) * (sides ? 1.05f : 1f);
                    // Rings are wide flat ellipses (axis along Y), weighted from the head into the chest down the back.
                    Bone b0 = t < 0.4f ? Bone.Head : Bone.Chest, b1 = t < 0.4f ? Bone.Neck : Bone.Neck;
                    float w0 = t < 0.4f ? 1f - t : 0.5f + 0.5f * t;
                    _k.Ring(new LoftRing
                    {
                        C = new V3(0f, y, z), AxisX = V3.Right, AxisZ = V3.Forward, Rx = w, Rz = thick * hs * (1f - 0.3f * t), Exp = 2.2f,
                        Rgb = t < 0.15f ? HairShine(new V3(0f, 0.5f, -0.5f)) : _hair, B0 = b0, B1 = b1, W0 = w0,
                    });
                }
                _k.EndLoft(_k.Seg(14, 6), LoftCap.Round, LoftCap.Round);
            }

            /// <summary>A braid of interlocking lobes from <paramref name="start"/> down <paramref name="length"/>
            /// metres, ending in a red tassel (chulthi with parandi) or a ribbon bow (school plaits).</summary>
            private void Braid(V3 start, float length, int lobes, bool tassel, int side = 0)
            {
                float hs = _hs;
                int n = Lod == 0 ? lobes : Lod == 1 ? (lobes + 1) / 2 : 3;
                V3 dir = side == 0 ? new V3(0f, -1f, -0.12f).Normalized : new V3(side * 0.05f, -1f, 0.25f).Normalized;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Math.Max(1, n - 1);
                    V3 p = start + dir * (length * t * hs);
                    if (side == 0) p = p + new V3(0f, 0f, -0.04f * t * t);
                    float rr = (0.03f - 0.012f * t) * hs * (side != 0 ? 0.8f : 1f);
                    Bone b0 = side == 0 ? (t < 0.3f ? Bone.Head : Bone.Chest) : Bone.Head;
                    _k.Ellipsoid(p, new V3(rr * 1.15f, rr * 1.1f, rr), Quat.Euler(0f, 0f, i % 2 == 0 ? 22f : -22f), i % 2 == 0 ? _hair : CharacterPalette.Shade(_hair, 0.85f),
                                 _k.Seg(7, 4), _k.Seg(5, 3), b0, 2f, Bone.Neck, side == 0 ? 0.6f + 0.4f * (1f - t) : 1f);
                }
                V3 end = start + dir * (length * hs) + (side == 0 ? new V3(0f, 0f, -0.04f) : V3.Zero);
                Bone eb = side == 0 ? Bone.Chest : Bone.Head;
                Mat(MaterialChannel.Fabric);
                if (tassel)
                {
                    if (Lod < 2) _k.Sphere(end + dir * 0.01f, 0.014f * hs, CharacterPalette.Gold, _k.Seg(6, 4), Lod == 0 ? 4 : 3, eb);
                    _k.Cone(end + dir * 0.015f, end + dir * 0.12f * hs, 0.012f * hs, 0.026f * hs, CharacterPalette.Tassel, _k.Seg(8, 4), eb);
                }
                else
                {
                    uint ribbon = (_r.Seed & 1) == 0 ? CharacterPalette.Ribbon : 0xF5F5F2u;
                    for (int s = -1; s <= 1; s += 2)
                        _k.Ellipsoid(end + new V3(s * 0.018f, 0.004f, 0f) * hs, new V3(0.018f, 0.011f, 0.007f) * hs, Quat.Euler(0f, 0f, s * 20f), ribbon, _k.Seg(7, 4),
                                     Lod == 0 ? 4 : 2, eb);
                }
                Mat(MaterialChannel.Hair);
            }

            /// <summary>The sadhu's jata: dreadlocks piled in a bun on the crown with ropes hanging down the back.</summary>
            private void Dreadlocks(bool covered)
            {
                V3 c = _hc;
                float hs = _hs;
                uint lock0 = CharacterPalette.Mix(_hair, 0x6A5236u, 0.25f);
                if (!covered)
                {
                    V3 top = c + new V3(0f, 0.2f * hs, -0.04f * hs);
                    _k.BeginLoft();
                    for (int i = 0; i < 4; i++)
                    {
                        float t = i / 3f;
                        float rr = (0.07f - 0.025f * t * t) * hs;
                        _k.Ring(top + new V3(0f, 0.06f * t * hs, 0f), V3.Up, V3.Right, rr, rr * 0.9f, i % 2 == 0 ? lock0 : CharacterPalette.Shade(lock0, 0.85f),
                                Bone.Head, Bone.Head, 1f, 2f);
                    }
                    _k.EndLoft(_k.Seg(10, 5), LoftCap.None, LoftCap.Round);
                }
                int ropes = Lod == 0 ? 7 : Lod == 1 ? 4 : 2;
                for (int i = 0; i < ropes; i++)
                {
                    float th = 2.2f + i * (1.9f / Math.Max(1, ropes - 1));
                    var p0 = new V3(c.X + MathF.Cos(th) * 0.15f * hs, c.Y - 0.02f * hs, c.Z + MathF.Sin(th) * 0.16f * hs - 0.02f);
                    var p2 = p0 + new V3(MathF.Cos(th) * 0.05f, -0.34f - 0.04f * (i % 2), MathF.Sin(th) * 0.06f) * hs;
                    var p1 = (p0 + p2) * 0.5f + new V3(MathF.Cos(th) * 0.04f, 0f, MathF.Sin(th) * 0.05f) * hs;
                    _k.Curve(p0, p1, p2, _k.Seg(6, 3), 0.016f * hs, 0.012f * hs, 0.016f * hs, 0.012f * hs, V3.Right, i % 2 == 0 ? lock0 : CharacterPalette.Shade(lock0, 0.85f),
                             _k.Seg(6, 4), Bone.Head, Lod >= 2 ? LoftCap.None : LoftCap.Round, Lod >= 2 ? LoftCap.Flat : LoftCap.Round, Bone.Chest, 0.6f);
                }
            }

            /// <summary>A band of short hair round the sides and back between two heights (receding hair, faded sides).</summary>
            private void HairBand(float offset, float topY, float lowY, float backLowY, uint? colour = null, bool full = false)
            {
                Mat(MaterialChannel.Hair);
                int cols = Lod == 0 ? 24 : Lod == 1 ? 12 : 8, rows = Lod == 0 ? 3 : 2;
                float e = HeadExponent, pe = 2f / e;
                var r = new V3(_hr.X + offset, _hr.Y + offset, _hr.Z + offset);
                uint rgb = colour ?? _hair;
                // From the temples (−60°) round the back to the other temple; or all round.
                float a0 = full ? 0f : 1.05f, a1 = full ? 6.28318530718f : 6.28318530718f - 1.05f;
                int segCols = full ? cols : cols + 1;
                _k.BeginGrid(rows, segCols);
                for (int i = 0; i < rows; i++)
                {
                    for (int j = 0; j < segCols; j++)
                    {
                        float a = a0 + (a1 - a0) * j / (full ? cols : cols);
                        float sa = MathF.Sin(a), ca = MathF.Cos(a);
                        float bw = MathF.Pow(Math.Max(0f, -ca), 1.4f);
                        float low = lowY + (backLowY - lowY) * bw;
                        float y = low + (topY - low) * i / (rows - 1);
                        float yN = CharMath.Clamp(y / r.Y, -0.98f, 0.98f);
                        float ring = MathF.Pow(Math.Max(0f, 1f - MathF.Pow(MathF.Abs(yN), e)), 1f / e);
                        float x = r.X * ring * BodyKit.SPow(sa, pe) * JawX(yN), z = r.Z * ring * BodyKit.SPow(ca, pe);
                        _k.GridSet(i, j, segCols, _hc + new V3(x, y, z), rgb, Bone.Head, Bone.Head, 1f);
                    }
                }
                _k.EndGrid(rows, segCols, full, _hc, V3.Up);
            }

            // ----- Facial hair --------------------------------------------------------------------------------------

            private void FacialHairMesh()
            {
                FacialHair f = _r.Beard;
                if (f == FacialHair.None || f == FacialHair.Stubble || Lod >= 2) return;
                Mat(MaterialChannel.Hair);
                uint col = CharacterPalette.Shade(_hair, 0.95f);
                if (f == FacialHair.ShortBeard || f == FacialHair.LongBeard) Beard(col, -0.32f, 0.006f);
                if (f == FacialHair.Goatee) Goatee(col);
                // Moustache over the upper lip.
                bool bushy = f == FacialHair.BushyMoustache || f == FacialHair.LongBeard;
                int n = Lod == 0 ? 6 : 3;
                V3[] path = _k.PathBuffer(n, out float[] rx, out float[] rz);
                float my = MouthY + (_expr.Corner * 0.3f + 0.009f) * _hs;
                for (int s = -1; s <= 1; s += 2)
                {
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)(n - 1);
                        float x = s * (0.002f + 0.042f * t) * _hs * (bushy ? 1.12f : 1f);
                        float y = my - (bushy ? 0.012f : 0.005f) * t * t * _hs + 0.004f * (1f - t) * t * _hs;
                        int k = s < 0 ? n - 1 - i : i;
                        path[k] = FacePoint(x, y, 0.004f * _hs, out V3 nn);
                        rx[k] = (bushy ? 0.009f : 0.0058f) * (1f - 0.55f * t) * _hs;
                        rz[k] = (bushy ? 0.0055f : 0.0035f) * _hs;
                    }
                    _k.Sweep(path, n, rx, rz, V3.Up, col, _k.Seg(6, 4), Bone.Head);
                }
                if (f == FacialHair.LongBeard)
                {
                    // The long sadhu beard falls from the chin onto the chest.
                    V3 chin = FacePoint(0f, -0.92f * _hr.Y, 0f, out V3 cn);
                    V3 end = chin + new V3(0f, -0.2f * _hs, 0.03f);
                    _k.BeginLoft();
                    int rings = Lod == 0 ? 6 : 3;
                    for (int i = 0; i < rings; i++)
                    {
                        float t = i / (float)(rings - 1);
                        V3 p = V3.Lerp(chin + new V3(0f, 0.02f, -0.025f), end, t);
                        float w = (0.065f - 0.045f * t) * _hs;
                        _k.Ring(new LoftRing
                        {
                            C = p, AxisX = V3.Right, AxisZ = V3.Forward, Rx = w, Rz = w * 0.6f, Exp = 2f, Rgb = i % 2 == 0 ? col : CharacterPalette.Shade(col, 0.9f),
                            B0 = t < 0.5f ? Bone.Head : Bone.Neck, B1 = Bone.Chest, W0 = 1f - 0.4f * t,
                        });
                    }
                    _k.EndLoft(_k.Seg(10, 5), LoftCap.None, LoftCap.Round);
                }
            }

            /// <summary>A beard shell over the jaw and chin from ear to ear, below the mouth in the middle.</summary>
            private void Beard(uint col, float sideTop, float offset)
            {
                int cols = Lod == 0 ? 15 : 7, rows = Lod == 0 ? 5 : 3;
                float pe = 2f / HeadExponent;
                _k.BeginGrid(rows, cols);
                for (int j = 0; j < cols; j++)
                {
                    float a = -1.75f + 3.5f * j / (cols - 1); // azimuth from the right ear round the chin to the left
                    float front = MathF.Cos(a);
                    float topN = sideTop + ((MouthY / _hr.Y) - 0.07f - sideTop) * MathF.Pow(Math.Max(0f, front), 2f);
                    for (int i = 0; i < rows; i++)
                    {
                        float t = i / (float)(rows - 1);
                        float ny = -0.995f + (topN + 0.995f) * t; // rows run upward
                        float ring = MathF.Pow(Math.Max(0f, 1f - MathF.Pow(MathF.Abs(ny), HeadExponent)), 1f / HeadExponent);
                        float nx = ring * BodyKit.SPow(MathF.Sin(a), pe), nz = ring * BodyKit.SPow(MathF.Cos(a), pe);
                        V3 p = HeadDeform(nx, ny, nz);
                        V3 radial = (p - _hc).Normalized;
                        float o = offset * _hs * (1f + 0.6f * MathF.Max(0f, front) * (1f - t));
                        _k.GridSet(i, j, cols, p + radial * o, col, Bone.Head, Bone.Head, 1f);
                    }
                }
                _k.EndGrid(rows, cols, false, _hc, V3.Zero);
            }

            private void Goatee(uint col)
            {
                V3 chin = FacePoint(0f, -0.83f * _hr.Y, 0.006f * _hs, out V3 n);
                _k.Ellipsoid(chin, new V3(0.025f, 0.03f, 0.012f) * _hs, Quat.Euler(-25f, 0f, 0f), col, _k.Seg(9, 5), _k.Seg(6, 3), Bone.Head);
            }
        }
    }
}
