using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Characters
{
    public static partial class HumanoidMesher
    {
        private sealed partial class Builder
        {
            // ----- Back items ---------------------------------------------------------------------------------------

            private void Back()
            {
                V3 attach = _sk.BindPosition[(int)Bone.BackAttach];
                uint rgb = CharacterPalette.Colour(_back.Item, _back.Colour);
                float back = attach.Z - _g.Bulk;
                switch (_back.Item)
                {
                    case OutfitItem.Daypack:
                        Daypack(attach, rgb, back);
                        break;
                    case OutfitItem.Doko:
                        Doko(attach, rgb, back);
                        break;
                    case OutfitItem.Sack:
                    {
                        Mat(MaterialChannel.Fabric);
                        V3 c = new V3(0f, attach.Y - 0.06f, back - 0.14f);
                        _k.Ellipsoid(c, new V3(0.17f, 0.24f, 0.13f), Quat.Euler(8f, 0f, 0f), rgb, _k.Seg(14, 6), _k.Seg(10, 5), Bone.BackAttach, 2.6f);
                        if (Lod < 2)
                        {
                            _k.Cone(c + new V3(0f, 0.22f, -0.02f), c + new V3(0f, 0.3f, -0.03f), 0.05f, 0.03f, CharacterPalette.Shade(rgb, 0.85f), 8, Bone.BackAttach);
                            _k.Capsule(c + new V3(-0.06f, 0.22f, -0.02f), c + new V3(0.06f, 0.22f, -0.02f), 0.008f, 0.008f, 0x8A6A44u, Bone.BackAttach, Bone.BackAttach, 5, 2, false);
                        }
                        break;
                    }
                    case OutfitItem.GasCylinder:
                    {
                        Mat(MaterialChannel.Paint);
                        V3 c = new V3(0f, attach.Y - 0.12f, back - 0.16f);
                        V3 axis = new V3(0f, 1f, -0.25f).Normalized;
                        _k.BeginLoft();
                        float[] hy = { -0.27f, -0.25f, 0.18f, 0.24f, 0.27f };
                        float[] hr = { 0.13f, 0.155f, 0.155f, 0.11f, 0.05f };
                        for (int i = 0; i < hy.Length; i++)
                            if (Lod < 2 || i == 0 || i == 2 || i == 4)
                                _k.Ring(c + axis * hy[i], axis, V3.Right, hr[i], hr[i], i == 0 ? 0x5A5A5Eu : rgb, Bone.BackAttach, Bone.BackAttach, 1f);
                        _k.EndLoft(_k.Seg(14, 6), LoftCap.Flat, LoftCap.Flat);
                        if (Lod < 2)
                        {
                            Mat(MaterialChannel.Metal);
                            _k.Capsule(c + axis * 0.27f, c + axis * 0.33f, 0.02f, 0.02f, 0x9A9EA6u, Bone.BackAttach, Bone.BackAttach, 8, 2, false);
                            _k.BeginLoft();
                            for (int k = 0; k < 3; k++)
                                _k.Ring(c + axis * (0.3f + 0.025f * k), axis, V3.Right, 0.085f + 0.004f * (k == 1 ? 1f : 0f), 0.085f, rgb, Bone.BackAttach, Bone.BackAttach, 1f);
                            _k.EndLoft(_k.Seg(14, 6), LoftCap.None, LoftCap.None);
                        }
                        break;
                    }
                    case OutfitItem.BabySling:
                        BabySling(attach, rgb, back);
                        break;
                }
            }

            private void Daypack(V3 attach, uint rgb, float back)
            {
                Mat(MaterialChannel.Fabric);
                float sc = _r.Age == AgeGroup.Child ? 0.8f : 1f;
                V3 c = new V3(0f, attach.Y - 0.05f * sc, back - 0.08f * sc);
                _k.Ellipsoid(c, new V3(0.15f, 0.2f, 0.075f) * sc, Quat.Identity, rgb, _k.Seg(12, Lod >= 2 ? 6 : 7), _k.Seg(8, 4), Bone.BackAttach, 4f);
                if (Lod >= 2) return;
                uint dark = CharacterPalette.Shade(rgb, 0.78f);
                _k.Ellipsoid(c + new V3(0f, -0.08f, -0.07f) * sc, new V3(0.11f, 0.075f, 0.03f) * sc, Quat.Identity, dark, _k.Seg(10, 5), _k.Seg(6, 4), Bone.BackAttach, 3.5f);
                if (Lod == 0)
                {
                    Mat(MaterialChannel.Metal);
                    _k.Capsule(c + new V3(-0.1f, -0.03f, -0.1f) * sc, c + new V3(0.1f, -0.03f, -0.1f) * sc, 0.003f, 0.003f, 0x2A2A2Eu, Bone.BackAttach, Bone.BackAttach, 4, 2, false);
                    Mat(MaterialChannel.Fabric);
                    _k.Curve(c + new V3(-0.04f, 0.19f, -0.02f) * sc, c + new V3(0f, 0.25f, -0.02f) * sc, c + new V3(0.04f, 0.19f, -0.02f) * sc, 4, 0.006f, 0.006f, 0.004f, 0.004f,
                             V3.Forward, dark, 4, Bone.BackAttach);
                }
                // Straps from the top of the pack over the shoulders, down the chest and back under the arms.
                Straps(back - 0.02f, dark);
            }

            /// <summary>Two shoulder straps (daypack): over the shoulder tops, down the front of the chest and round under
            /// the arms to the sides.</summary>
            private void Straps(float backZ, uint rgb)
            {
                float sh = _m.ShoulderY, lift = 0.011f + _g.Bulk;
                Mat(MaterialChannel.Fabric);
                for (int s = -1; s <= 1; s += 2)
                {
                    V3[] path = _k.PathBuffer(5, out float[] rx, out float[] rz);
                    path[0] = new V3(s * 0.075f, sh - 0.03f, backZ);
                    path[1] = new V3(s * 0.105f, sh + 0.03f + _g.Bulk, -0.012f);
                    path[2] = TorsoPoint(s * 0.4f, sh - 0.035f, lift, out V3 n2);
                    path[3] = TorsoPoint(s * 0.48f, sh - 0.12f, lift, out V3 n3);
                    path[4] = TorsoPoint(s * 0.55f, sh - 0.19f, lift, out V3 n4);
                    for (int i = 0; i < 5; i++)
                    {
                        rx[i] = 0.019f;
                        rz[i] = 0.0045f;
                    }
                    _k.Sweep(path, 5, rx, rz, V3.Right, rgb, 5, Bone.Chest, LoftCap.Flat, LoftCap.None);
                    if (Lod > 0) continue;
                    V3 a = path[4];
                    V3 b = TorsoPoint(s * 1.25f, sh - 0.26f, lift, out V3 nb);
                    V3[] p2 = _k.PathBuffer(3, out rx, out rz);
                    p2[0] = a;
                    p2[1] = TorsoPoint(s * 0.85f, sh - 0.235f, lift, out V3 nm);
                    p2[2] = b;
                    for (int i = 0; i < 3; i++)
                    {
                        rx[i] = 0.016f;
                        rz[i] = 0.004f;
                    }
                    _k.Sweep(p2, 3, rx, rz, V3.Up, CharacterPalette.Shade(rgb, 0.9f), 5, Bone.Chest, LoftCap.None, LoftCap.Flat);
                    // The buckle where the adjuster sits.
                    Mat(MaterialChannel.Metal);
                    _k.Ellipsoid(path[3] + n3 * 0.004f, new V3(0.014f, 0.008f, 0.004f), Quat.Euler(0f, MathF.Atan2(n3.X, n3.Z) * Quat.Rad2Deg, 0f), 0x2A2A2Eu, 6, 3, Bone.Chest, 4f);
                    Mat(MaterialChannel.Fabric);
                }
            }

            /// <summary>The doko: a conical bamboo basket wider at the top, woven in a check of light and dark strips
            /// with a bound rim, leaning back from the porter's back; its load (vegetables or fodder) mounded on top.</summary>
            private void Doko(V3 attach, uint rgb, float back)
            {
                Mat(MaterialChannel.Wood);
                V3 bottom = new V3(0f, attach.Y - 0.42f, back - 0.12f), top = new V3(0f, attach.Y + 0.24f, back - 0.24f);
                V3 axis = (top - bottom).Normalized;
                int cols = Lod == 0 ? 20 : Lod == 1 ? 10 : 7, rows = Lod == 0 ? 9 : Lod == 1 ? 4 : 2;
                uint dark = CharacterPalette.Shade(rgb, 0.78f);
                _k.BeginGrid(rows + 1, cols);
                for (int i = 0; i <= rows; i++)
                {
                    float t = i / (float)rows;
                    V3 c = V3.Lerp(bottom, top, t);
                    float r = 0.11f + 0.15f * t;
                    for (int j = 0; j < cols; j++)
                    {
                        float a = 6.28318530718f * j / cols;
                        var p = c + new V3(MathF.Sin(a) * r, 0f, MathF.Cos(a) * r * 0.9f);
                        _k.GridSet(i, j, cols, p, rgb, Bone.BackAttach, Bone.BackAttach, 1f, i == 0 ? 0.7f : 1f);
                    }
                }
                var q = new uint[rows * cols];
                for (int i = 0; i < rows; i++)
                    for (int j = 0; j < cols; j++)
                        q[i * cols + j] = Lod < 2 && (i + j) % 2 == 0 ? dark : rgb;
                _k.EndGridColours(rows + 1, cols, true, bottom, axis, q, null);
                // Bottom.
                _k.Ellipsoid(bottom, new V3(0.11f, 0.012f, 0.1f), Quat.Identity, dark, _k.Seg(10, 6), 3, Bone.BackAttach, 2f);
                if (Lod >= 2) return;
                // Bound rim and the load.
                _k.BeginLoft();
                for (int k = 0; k < 3; k++)
                {
                    float r = 0.26f + (k == 1 ? 0.012f : 0f);
                    _k.Ring(new LoftRing
                    {
                        C = top + axis * ((k - 1) * 0.012f), AxisX = V3.Right, AxisZ = V3.Forward, Rx = r, Rz = r * 0.9f, Exp = 2f, Rgb = dark, B0 = Bone.BackAttach,
                        B1 = Bone.BackAttach, W0 = 1f,
                    });
                }
                _k.EndLoft(_k.Seg(20, 8), LoftCap.None, LoftCap.None);
                int load = _back.Pattern % 3;
                Mat(MaterialChannel.Foliage);
                if (load == 0)
                {
                    // Leafy greens (saag) and a few cauliflower heads and tomatoes.
                    _k.Ellipsoid(top + axis * 0.04f, new V3(0.23f, 0.08f, 0.21f), Quat.Identity, 0x4E8A3Au, _k.Seg(12, 6), _k.Seg(7, 4), Bone.BackAttach);
                    if (Lod == 0)
                        for (int k = 0; k < 5; k++)
                        {
                            float a = k * 1.3f;
                            V3 p = top + axis * 0.09f + new V3(MathF.Sin(a) * 0.12f, 0f, MathF.Cos(a) * 0.1f);
                            _k.Sphere(p, 0.045f, k % 2 == 0 ? 0xEDE6CFu : 0xD9452Bu, 7, 4, Bone.BackAttach);
                        }
                }
                else if (load == 1)
                {
                    // Fodder grass spilling over the rim.
                    _k.Ellipsoid(top + axis * 0.06f, new V3(0.26f, 0.12f, 0.24f), Quat.Identity, 0x7FA14Au, _k.Seg(12, 6), _k.Seg(7, 4), Bone.BackAttach);
                    if (Lod == 0)
                        for (int k = 0; k < 6; k++)
                        {
                            float a = k * 1.05f;
                            V3 p = top + new V3(MathF.Sin(a) * 0.22f, 0.02f, MathF.Cos(a) * 0.2f);
                            _k.Cone(p, p + new V3(MathF.Sin(a) * 0.05f, -0.1f, MathF.Cos(a) * 0.05f), 0.03f, 0.005f, 0x6E9440u, 5, Bone.BackAttach);
                        }
                }
                else
                {
                    // Firewood bundle.
                    Mat(MaterialChannel.Bark);
                    for (int k = 0; k < (Lod == 0 ? 6 : 3); k++)
                    {
                        float x = -0.12f + 0.05f * k;
                        _k.Capsule(top + new V3(x, -0.05f, 0.04f), top + new V3(x * 1.3f, 0.2f, -0.04f), 0.022f, 0.02f, k % 2 == 0 ? 0x7A5A3Cu : 0x8E6A46u,
                                   Bone.BackAttach, Bone.BackAttach, 6, 2, false);
                    }
                }
            }

            private void BabySling(V3 attach, uint rgb, float back)
            {
                Mat(MaterialChannel.Fabric);
                // The shawl pouch on the back, tied across the chest.
                V3 c = new V3(0f, attach.Y - 0.08f, back - 0.1f);
                _k.Ellipsoid(c, new V3(0.15f, 0.17f, 0.1f), Quat.Euler(5f, 0f, 0f), rgb, _k.Seg(12, 6), _k.Seg(9, Lod >= 2 ? 4 : 5), Bone.BackAttach, 2.4f);
                // The baby's head peeks over the top in a knitted cap.
                V3 head = c + new V3(0.02f, 0.2f, -0.02f);
                Mat(MaterialChannel.Skin);
                _k.Sphere(head, 0.075f, _skin, _k.Seg(10, Lod >= 2 ? 5 : 6), _k.Seg(7, 3), Bone.BackAttach);
                if (Lod < 2)
                {
                    Mat(MaterialChannel.Fabric);
                    _k.Ellipsoid(head + new V3(0f, 0.035f, -0.01f), new V3(0.078f, 0.05f, 0.078f), Quat.Identity, 0xE86A9Au, _k.Seg(10, 5), _k.Seg(6, 3), Bone.BackAttach);
                    if (Lod == 0)
                    {
                        Mat(MaterialChannel.Plain);
                        for (int s = -1; s <= 1; s += 2)
                            _k.Sphere(head + new V3(s * 0.024f, -0.005f, 0.066f), 0.008f, CharacterPalette.Pupil, 5, 3, Bone.BackAttach);
                    }
                }
                if (Lod >= 2) return;
                Mat(MaterialChannel.Fabric);
                float sh = _m.ShoulderY;
                // The knot of the shawl on the chest, from both shoulders.
                V3 knot = TorsoPoint(0f, sh - 0.1f, 0.02f + _g.Bulk, out V3 kn);
                for (int s = -1; s <= 1; s += 2)
                {
                    V3 p0 = new V3(s * 0.1f, sh - 0.02f, back - 0.05f);
                    V3 p1 = TorsoPoint(s * 1.2f, sh + 0.01f, 0.014f + _g.Bulk, out V3 n1);
                    // The ends tuck into the pouch, over the shoulder and into the knot: no caps.
                    _k.Curve(p0, (p0 + p1) * 0.5f + new V3(0f, 0.04f, 0f), p1, 3, 0.03f, 0.03f, 0.006f, 0.006f, V3.Right, rgb, 5, Bone.Chest, LoftCap.None,
                             LoftCap.None);
                    _k.Curve(p1, (p1 + knot) * 0.5f + n1 * 0.02f, knot, Lod == 0 ? 4 : 3, 0.03f, 0.022f, 0.006f, 0.006f, V3.Right, rgb, 5, Bone.Chest, LoftCap.None,
                             LoftCap.None);
                }
                _k.Sphere(knot, 0.025f, CharacterPalette.Shade(rgb, 0.85f), _k.Seg(7, 5), Lod == 0 ? 4 : 3, Bone.Chest);
            }

            // ----- Accents ------------------------------------------------------------------------------------------

            private void Accents()
            {
                CharacterAccents a = _r.Accents;
                if (a == CharacterAccents.None || FarDrop(2)) return;
                if (FarDrop(1)) a &= CharacterAccents.Umbrella;
                if (Lod < 2)
                {
                    if ((a & CharacterAccents.Bindi) != 0)
                    {
                        Mat(MaterialChannel.Plain);
                        FaceDisc(0f, EyeY + 0.052f * _hs, 0.0055f * _hs, 0.0055f * _hs, 0.0012f * _hs, CharacterPalette.Bindi, 1, 8);
                    }
                    if ((a & CharacterAccents.Tika) != 0)
                    {
                        Mat(MaterialChannel.Plain);
                        FaceDisc(0f, EyeY + 0.075f * _hs, 0.008f * _hs, 0.022f * _hs, 0.0012f * _hs, 0xD2001Fu, 1, 8);
                    }
                    if ((a & CharacterAccents.Tilak) != 0)
                    {
                        Mat(MaterialChannel.Plain);
                        for (int k = 0; k < 3; k++)
                            FaceDisc(0f, EyeY + (0.06f + 0.014f * k) * _hs, 0.045f * _hs, 0.0035f * _hs, 0.0012f * _hs, CharacterPalette.Ash, 1, 10);
                        FaceDisc(0f, EyeY + 0.074f * _hs, 0.0055f * _hs, 0.007f * _hs, 0.0018f * _hs, 0xD2001Fu, 1, 8);
                    }
                    if ((a & CharacterAccents.NoseStud) != 0 && Lod == 0)
                    {
                        Mat(MaterialChannel.Gilt);
                        V3 p = FacePoint(-0.0165f * _hs, NoseY - 0.002f * _hs, 0.007f * _hs, out V3 n);
                        _k.Sphere(p, 0.0032f * _hs, CharacterPalette.Gold, 5, 3, Bone.Head);
                    }
                    if ((a & CharacterAccents.Earrings) != 0 && Lod == 0) Earrings();
                    if ((a & (CharacterAccents.Glasses | CharacterAccents.Sunglasses)) != 0) Glasses((a & CharacterAccents.Sunglasses) != 0);
                    if ((a & CharacterAccents.Mask) != 0) FaceMask();
                    if ((a & CharacterAccents.Pote) != 0)
                    {
                        // Married women's green pote; with the haku patasi the red bead necklace and its gold pendant.
                        bool haku = _torso.Item == OutfitItem.Sari && _torso.Colour == CharacterPalette.HakuSari;
                        Necklace(haku ? CharacterPalette.RedBead : CharacterPalette.PoteGreen, haku ? 0.24f : 0.2f, true, haku);
                    }
                    if ((a & CharacterAccents.Mala) != 0) Necklace(_torso.Item == OutfitItem.Robe && _torso.Colour == 1 ? 0x5A3A22u : CharacterPalette.MalaBead, 0.26f, false);
                    if ((a & CharacterAccents.Watch) != 0) Watch();
                }
                if ((a & CharacterAccents.Shawl) != 0) Shawl();
                if ((a & CharacterAccents.Umbrella) != 0) Umbrella();
                else if ((a & CharacterAccents.PrayerWheel) != 0) PrayerWheel();
            }

            private void Earrings()
            {
                Mat(MaterialChannel.Gilt);
                float ny = -0.17f;
                for (int s = -1; s <= 1; s += 2)
                {
                    float x = _hr.X * JawX(ny) - 0.004f * _hs;
                    var c = new V3(_hc.X + s * x, _hc.Y + ny * _hr.Y, _hc.Z - 0.012f * _hs);
                    Quat rot = Quat.Euler(0f, s * 72f, s * -6f);
                    V3 lobe = c + rot * new V3(0.006f, -0.044f, 0.004f) * _hs;
                    _k.BeginLoft();
                    for (int k = 0; k <= 8; k++)
                    {
                        float a = 6.28318530718f * k / 8;
                        V3 p = lobe + new V3(0f, -0.012f * _hs - MathF.Cos(a) * 0.01f * _hs, MathF.Sin(a) * 0.01f * _hs);
                        V3 d = new V3(0f, MathF.Sin(a), MathF.Cos(a));
                        _k.Ring(p, d, V3.Right, 0.0018f * _hs, 0.0018f * _hs, CharacterPalette.Gold, Bone.Head, Bone.Head, 1f);
                    }
                    _k.EndLoft(4, LoftCap.None, LoftCap.None);
                }
            }

            private void Glasses(bool sun)
            {
                Mat(MaterialChannel.Metal);
                uint frame = CharacterPalette.GlassesFrame;
                for (int s = -1; s <= 1; s += 2)
                {
                    EyeFrame(s, out V3 c, out V3 r, out Quat rot);
                    V3 centre = c + rot * new V3(0f, 0f, r.Z + 0.008f * _hs);
                    int n = Lod == 0 ? 12 : 6;
                    V3[] path = _k.PathBuffer(n + 1, out float[] rx, out float[] rz);
                    for (int k = 0; k <= n; k++)
                    {
                        float a = 6.28318530718f * k / n;
                        path[k] = centre + rot * new V3(MathF.Cos(a) * 0.036f * _hs, MathF.Sin(a) * 0.029f * _hs, 0f);
                        rx[k] = 0.0024f * _hs;
                        rz[k] = 0.0024f * _hs;
                    }
                    _k.Sweep(path, n + 1, rx, rz, V3.Forward, frame, Lod == 0 ? 4 : 3, Bone.Head, LoftCap.None, LoftCap.None);
                    if (sun)
                    {
                        Mat(MaterialChannel.Glass);
                        _k.Ellipsoid(centre - rot * new V3(0f, 0f, 0.001f), new V3(0.034f, 0.027f, 0.004f) * _hs, rot, CharacterPalette.SunLens, _k.Seg(8, 6), 3, Bone.Head);
                        Mat(MaterialChannel.Metal);
                    }
                    // Temple arm back to the ear.
                    if (Lod > 0) continue;
                    V3 hinge = centre + rot * new V3(s * 0.036f * _hs, 0.004f * _hs, -0.004f);
                    V3 ear = new V3(_hc.X + s * (_hr.X * JawX(-0.12f) + 0.004f), _hc.Y - 0.01f * _hs, _hc.Z - 0.03f * _hs);
                    _k.Capsule(hinge, ear, 0.0022f * _hs, 0.0022f * _hs, frame, Bone.Head, Bone.Head, 4, 2, false, LoftCap.None, LoftCap.Round);
                }
                EyeFrame(-1, out V3 lc, out V3 lr, out Quat lrot);
                EyeFrame(1, out V3 rc, out V3 rr, out Quat rrot);
                V3 bl = lc + lrot * new V3(0.036f * _hs, 0.006f * _hs, lr.Z + 0.008f * _hs), br = rc + rrot * new V3(-0.036f * _hs, 0.006f * _hs, rr.Z + 0.008f * _hs);
                _k.Curve(bl, (bl + br) * 0.5f + new V3(0f, 0.006f * _hs, 0.004f), br, 3, 0.0022f * _hs, 0.0022f * _hs, 0.0022f * _hs, 0.0022f * _hs, V3.Up, frame, 4, Bone.Head);
            }

            private void FaceMask()
            {
                Mat(MaterialChannel.Fabric);
                int cols = Lod == 0 ? 9 : 5, rows = Lod == 0 ? 5 : 3;
                float x0 = -0.085f * _hs, y0 = MouthY - 0.045f * _hs, y1 = NoseY + 0.012f * _hs;
                _k.BeginGrid(rows, cols);
                for (int i = 0; i < rows; i++)
                    for (int j = 0; j < cols; j++)
                    {
                        float u = j / (float)(cols - 1), v = i / (float)(rows - 1);
                        float x = x0 + 2f * -x0 * u;
                        float y = y0 + (y1 - y0) * v;
                        float bulge = 0.016f * _hs * MathF.Sin(u * MathF.PI) * MathF.Sin(v * MathF.PI * 0.9f + 0.1f);
                        V3 p = FacePoint(x, y, 0.004f * _hs + bulge, out V3 n);
                        _k.GridSet(i, j, cols, p, CharacterPalette.Mask, Bone.Head, Bone.Head, 1f);
                    }
                var rowRgb = new uint[rows - 1];
                for (int i = 0; i < rowRgb.Length; i++) rowRgb[i] = i % 2 == 0 ? CharacterPalette.Mask : CharacterPalette.Shade(CharacterPalette.Mask, 0.94f);
                _k.EndGridColours(rows, cols, false, _hc, V3.Zero, null, rowRgb);
                if (Lod > 0) return;
                for (int s = -1; s <= 1; s += 2)
                {
                    V3 a = FacePoint(s * -x0, (y0 + y1) * 0.5f, 0.004f * _hs, out V3 n);
                    V3 b = new V3(_hc.X + s * (_hr.X * JawX(-0.2f) + 0.002f), _hc.Y - 0.04f * _hs, _hc.Z - 0.01f * _hs);
                    _k.Curve(a, (a + b) * 0.5f + new V3(s * 0.01f, 0f, 0f), b, 3, 0.0015f, 0.0015f, 0.0015f, 0.0015f, V3.Up, 0xF2F2F2u, 3, Bone.Head);
                }
            }

            /// <summary>A bead necklace hanging from the base of the neck to <paramref name="drop"/> metres down the chest
            /// (the green pote with its gold tilhari, a monk's or a sadhu's mala).</summary>
            private void Necklace(uint bead, float drop, bool pendant, bool flatPendant = false)
            {
                Mat(pendant ? MaterialChannel.Plain : MaterialChannel.Wood);
                float sh = _m.ShoulderY;
                // Over a draped pallu or zen the beads lie on the drape.
                float over = _torso.Item == OutfitItem.Sari || _torso.Item == OutfitItem.Robe ? 0.018f : 0f;
                // A strand of beads round the back of the neck and down the chest: LOD0 bead by bead (alternating bead and
                // gap radii along one sweep), LOD1 a smooth strand.
                int n = Lod == 0 ? 29 : 5;
                V3[] path = _k.PathBuffer(n, out float[] rx, out float[] rz);
                for (int k = 0; k < n; k++)
                {
                    float t = k / (float)(n - 1);
                    float a = Lod == 0 ? -1.25f + 2.5f * t : -0.95f + 1.9f * t;
                    float y = sh + 0.005f - drop * MathF.Pow(MathF.Cos(a * 1.2f), 2f) * 0.9f;
                    V3 p = TorsoPoint(a * 0.55f, Math.Max(y, _ty[3]), 0.008f + _g.Bulk + over, out V3 nn);
                    if (MathF.Abs(a) > 0.95f)
                    {
                        // Round the neck at the shoulders.
                        p = new V3(MathF.Sin(a) * 0.075f * (_m.HeadW / 0.36f), sh + 0.015f, MathF.Cos(a) * 0.07f - 0.01f);
                    }
                    path[k] = p;
                    float r = Lod == 0 ? (k % 2 == 0 ? 0.0098f : 0.0072f) : 0.009f;
                    rx[k] = r;
                    rz[k] = r;
                }
                _k.Sweep(path, n, rx, rz, V3.Up, bead, Lod == 0 ? 5 : 4, Bone.Chest, LoftCap.None, LoftCap.None);
                if (!pendant) return;
                Mat(MaterialChannel.Gilt);
                V3 c = TorsoPoint(0f, sh - drop * 0.95f, 0.012f + _g.Bulk + over, out V3 cn);
                float yaw = MathF.Atan2(cn.X, cn.Z) * Quat.Rad2Deg;
                if (flatPendant)
                {
                    // A flat gold ornament with two side lobes under a small bead.
                    _k.Ellipsoid(c - new V3(0f, 0.012f, 0f), new V3(0.014f, 0.012f, 0.004f), Quat.Euler(0f, yaw, 0f), CharacterPalette.Gold, _k.Seg(8, 5), 3, Bone.Chest);
                    for (int s = -1; s <= 1; s += 2)
                        _k.Ellipsoid(c + new V3(s * 0.017f, -0.006f, -0.001f), new V3(0.009f, 0.005f, 0.003f), Quat.Euler(0f, yaw, s * -20f), CharacterPalette.Gold,
                                     _k.Seg(6, 4), 2, Bone.Chest);
                    _k.Sphere(c + new V3(0f, 0.004f, 0f), 0.005f, CharacterPalette.Gold, 5, 3, Bone.Chest);
                }
                else
                {
                    // The tilhari: a long gold cylinder.
                    _k.Capsule(c + new V3(0f, 0.012f, 0f), c - new V3(0f, 0.024f, 0f), 0.008f, 0.008f, CharacterPalette.Gold, Bone.Chest, Bone.Chest, 7, 2, false);
                }
            }

            private void Watch()
            {
                V3 w = _sk.BindPosition[(int)Bone.HandL];
                Mat(MaterialChannel.Leather);
                _k.BeginLoft();
                for (int k = 0; k < 3; k++)
                    _k.Ring(w + new V3(0f, 0.03f + 0.007f * k, 0f), V3.Up, V3.Right, 0.034f + (k == 1 ? 0.003f : 0f), 0.034f + (k == 1 ? 0.003f : 0f), CharacterPalette.WatchStrap,
                            Bone.ForearmL, Bone.ForearmL, 1f);
                _k.EndLoft(10, LoftCap.None, LoftCap.None);
                Mat(MaterialChannel.Metal);
                _k.Ellipsoid(w + new V3(-0.035f, 0.037f, 0f), new V3(0.004f, 0.012f, 0.012f), Quat.Identity, 0xC9CCD2u, 8, 4, Bone.ForearmL);
            }

            /// <summary>A shawl or dupatta over both shoulders, falling down the back with the ends over the chest.</summary>
            private void Shawl()
            {
                Mat(MaterialChannel.Fabric);
                uint rgb = CharacterPalette.At(CharacterPalette.Shawl, (int)(CharMath.Hash(_r.Seed, 0x5A3Au) % 7));
                if (rgb == _g.Top) rgb = CharacterPalette.At(CharacterPalette.Shawl, (int)(CharMath.Hash(_r.Seed, 0x5A3Bu) % 7) + 1);
                float sh = _m.ShoulderY;
                // Over the shoulders and the upper arms, open at the front, hanging down the back.
                int rows = Lod == 0 ? 5 : 3, cols = Lod == 0 ? 15 : Lod == 1 ? 9 : 7;
                float a0 = 0.55f, a1 = 6.28318530718f - 0.55f;
                float bottom = sh - 0.27f, top = sh + 0.03f;
                _k.BeginGrid(rows, cols);
                for (int i = 0; i < rows; i++)
                {
                    float t = i / (float)(rows - 1);
                    for (int j = 0; j < cols; j++)
                    {
                        float a = a0 + (a1 - a0) * j / (cols - 1);
                        float side = MathF.Pow(MathF.Abs(MathF.Sin(a)), 1.5f);
                        // The sides ride up over the arms; the back hangs lowest.
                        float yLow = bottom + 0.12f * side;
                        float y = yLow + (top - yLow) * t;
                        float over = 0.075f * side * CharMath.SmoothStep(CharMath.Clamp01((y - (sh - 0.16f)) / 0.12f));
                        V3 p = TorsoPoint(a, Math.Min(y, _ty[6] - 0.002f), 0.012f + _g.Bulk + over, out V3 nn);
                        TorsoBones(y, out Bone b0, out Bone b1, out float w0);
                        _k.GridSet(i, j, cols, p, i == 0 ? CharacterPalette.Shade(rgb, 0.85f) : rgb, b0, b1, w0);
                    }
                }
                var rowRgb = new uint[rows - 1];
                for (int i = 0; i < rowRgb.Length; i++) rowRgb[i] = i == 0 && Lod == 0 ? CharacterPalette.Shade(rgb, 0.82f) : rgb;
                _k.EndGridColours(rows, cols, false, new V3(0f, bottom, 0f), V3.Up, null, rowRgb);
                if (Lod >= 2) return;
                // The two ends hanging in front.
                for (int s = -1; s <= 1; s += 2)
                {
                    V3 a = TorsoPoint(s * 0.55f, sh - 0.02f, 0.015f + _g.Bulk, out V3 na);
                    V3 b = TorsoPoint(s * 0.45f, _m.HipJointY + 0.05f, 0.018f + _g.Bulk, out V3 nb);
                    _k.Curve(a, (a + b) * 0.5f + na * 0.01f, b, 4, 0.045f, 0.05f, 0.006f, 0.006f, V3.Right, rgb, 5, Bone.Chest, LoftCap.Flat, LoftCap.Flat, Bone.Spine, 0.7f);
                }
            }

            /// <summary>An open umbrella held in the right hand above the head: shaft, eight ribs and a scalloped canopy.</summary>
            private void Umbrella()
            {
                V3 hand = _sk.BindPosition[(int)Bone.PropR];
                uint canopy = CharacterPalette.At(CharacterPalette.Umbrella, (int)(_r.Seed % 5));
                Mat(MaterialChannel.Metal);
                V3 tip = new V3(hand.X - 0.05f, _m.HeightM + 0.42f, 0.05f);
                _k.Capsule(hand, tip, 0.008f, 0.007f, 0x3A3A3Eu, Bone.PropR, Bone.PropR, Lod >= 2 ? 3 : 6, 2, false, Lod >= 2 ? LoftCap.None : LoftCap.Round,
                           Lod >= 2 ? LoftCap.None : LoftCap.Round);
                Mat(MaterialChannel.Fabric);
                int ribs = Lod == 0 ? 8 : 6;
                int cols = ribs * (Lod == 0 ? 3 : Lod == 1 ? 2 : 1);
                int rows = Lod == 0 ? 4 : 2;
                V3 top = tip - new V3(0f, 0.04f, 0f);
                _k.BeginGrid(rows + 1, cols);
                for (int i = 0; i <= rows; i++)
                {
                    float t = i / (float)rows;
                    for (int j = 0; j < cols; j++)
                    {
                        float a = 6.28318530718f * j / cols;
                        float rib = MathF.Abs(MathF.Cos(a * ribs * 0.5f));
                        float r = (0.02f + 0.48f * t) * (Lod == 0 ? 0.94f + 0.06f * rib : 1f);
                        float y = -0.22f * t * t - (Lod == 0 ? 0.02f * t * (1f - rib) : 0f);
                        _k.GridSet(i, j, cols, top + new V3(MathF.Sin(a) * r, y, MathF.Cos(a) * r), canopy, Bone.PropR, Bone.PropR, 1f);
                    }
                }
                _k.EndGrid(rows + 1, cols, true, top - new V3(0f, 0.5f, 0f), V3.Zero, null, true, false);
                if (Lod >= 2) return;
                // The underside so the canopy reads from below.
                _k.BeginGrid(2, cols);
                for (int i = 0; i < 2; i++)
                    for (int j = 0; j < cols; j++)
                    {
                        float a = -6.28318530718f * j / cols;
                        float r = i == 0 ? 0.48f : 0.04f;
                        float y = i == 0 ? -0.225f : -0.01f;
                        _k.GridSet(i, j, cols, top + new V3(MathF.Sin(a) * r, y, MathF.Cos(a) * r), CharacterPalette.Shade(canopy, 0.7f), Bone.PropR, Bone.PropR, 1f, 0.6f);
                    }
                _k.EndGrid(2, cols, true, top + new V3(0f, 0.5f, 0f), V3.Zero);
                Mat(MaterialChannel.Wood);
                _k.Curve(hand, hand + new V3(0f, -0.08f, 0f), hand + new V3(0.05f, -0.09f, 0f), 3, 0.011f, 0.011f, 0.011f, 0.011f, V3.Forward, 0x5A3A22u, 6, Bone.PropR);
            }

            /// <summary>A hand prayer wheel (mani wheel): a wooden handle and a brass drum with a weight on a chain.</summary>
            private void PrayerWheel()
            {
                V3 hand = _sk.BindPosition[(int)Bone.PropR];
                Mat(MaterialChannel.Wood);
                V3 a = hand + new V3(0f, -0.02f, 0.02f), b = hand + new V3(0f, 0.12f, 0.03f);
                Mat(MaterialChannel.Gilt);
                V3 c = b + new V3(0f, 0.045f, 0f);
                if (Lod >= 2)
                {
                    // The far body: just the drum on a stub of handle.
                    _k.BeginLoft();
                    _k.Ring(c + new V3(0f, -0.06f, 0f), V3.Up, V3.Right, 0.012f, 0.012f, 0x6B4A2Eu, Bone.PropR, Bone.PropR, 1f);
                    _k.Ring(c + new V3(0f, -0.04f, 0f), V3.Up, V3.Right, 0.038f, 0.038f, CharacterPalette.Copper, Bone.PropR, Bone.PropR, 1f);
                    _k.Ring(c + new V3(0f, 0.045f, 0f), V3.Up, V3.Right, 0.034f, 0.034f, CharacterPalette.Brass, Bone.PropR, Bone.PropR, 1f);
                    _k.EndLoft(5, LoftCap.None, LoftCap.Flat);
                    return;
                }
                Mat(MaterialChannel.Wood);
                _k.Capsule(a, b, 0.011f, 0.009f, 0x6B4A2Eu, Bone.PropR, Bone.PropR, 6, 2, false);
                Mat(MaterialChannel.Gilt);
                _k.BeginLoft();
                float[] hy = { -0.04f, -0.036f, 0.036f, 0.04f, 0.055f };
                float[] hr = { 0.022f, 0.038f, 0.038f, 0.03f, 0.008f };
                for (int i = 0; i < hy.Length; i++)
                    _k.Ring(c + new V3(0f, hy[i], 0f), V3.Up, V3.Right, hr[i], hr[i], i == 2 || i == 1 ? CharacterPalette.Copper : CharacterPalette.Brass, Bone.PropR, Bone.PropR, 1f);
                _k.EndLoft(_k.Seg(12, 6), LoftCap.Flat, LoftCap.Round);
                if (Lod == 0) _k.Sphere(c + new V3(0.06f, -0.01f, 0f), 0.01f, CharacterPalette.Brass, 6, 4, Bone.PropR);
            }
        }
    }
}
