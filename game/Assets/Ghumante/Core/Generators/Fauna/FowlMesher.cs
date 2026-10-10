using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Generators.Fauna
{
    /// <summary>
    /// Village fowl (docs/research/w2/ref_animals.md §6): the local hen (red-brown, buff, black or white, a small single
    /// comb and wattles, tail held up), the rooster (tall serrated comb, long wattles, a golden-orange cape of hackles
    /// over the neck and shoulders, black breast and the arching green-black sickle tail; or all white) and the
    /// pond duck (upright, white or brown, flat orange bill and webbed orange feet). Built on the shared avian recipe;
    /// the coat is the instance tint.
    /// </summary>
    public static class FowlMesher
    {
        /// <summary>Builds a hen, rooster or duck.</summary>
        public static void Build(FaunaSpecies species, int lod, CoatPattern pattern, FaunaMesh target)
        {
            var b = new FaunaBuilder(target, species, lod, pattern);
            var c = new FaunaSketch { B = b, D = FaunaDetail.Fowl(lod), S = 1f };
            FaunaDetail d = c.D;
            AvianSpec s;
            uint coat = FaunaPalette.Coat;
            switch (species)
            {
                case FaunaSpecies.Rooster:
                {
                    s = BirdMesher.Shape(0.28f, 0.085f, 0.09f, 28f, 0.11f, 0.034f, 14f, 0f, 0.028f, 0.034f, 0.022f, 0.0075f, 0f, 0f, 0.006f, 0.16f, 0.07f, 0f,
                                         -50f, 0f, 0.13f, 0.008f, 0.032f, 0.05f, 0.8f, 0.18f, 0.22f, 0.006f);
                    bool white = pattern == CoatPattern.Patched;
                    s.Body = white ? coat : 0x2A2220u << 8;
                    s.Belly = white ? FaunaPalette.CoatBelly : 0x221C1Au << 8;
                    s.Back = coat;
                    s.Head = coat;
                    s.Neck = white ? coat : 0xE0A030u << 8;
                    s.Wing = coat;
                    s.WingTip = white ? FaunaPalette.CoatLeg : 0x3A2A1Eu << 8;
                    s.Tail = white ? coat : FaunaPalette.RoosterTail;
                    s.TailTip = white ? FaunaPalette.CoatLeg : 0x1A2E24u << 8;
                    break;
                }
                case FaunaSpecies.Duck:
                    s = BirdMesher.Shape(0.3f, 0.1f, 0.085f, 12f, 0.085f, 0.03f, 12f, 0f, 0.03f, 0.04f, 0.055f, 0.012f, 0f, 0.9f, 0.004f, 0.06f, 0.05f, 0f,
                                         -20f, 0f, 0.055f, 0.008f, 0.035f, 0.04f, 0.85f, 0.16f, 0.2f, 0.006f);
                    s.Body = coat;
                    s.Belly = FaunaPalette.CoatBelly;
                    s.Back = pattern == CoatPattern.Saddle ? 0xA8A4A0FFu : coat;
                    s.Head = pattern == CoatPattern.Saddle ? 0xB4AEA8FFu : coat;
                    s.Neck = coat;
                    s.Wing = pattern == CoatPattern.Saddle ? 0x9A948EFFu : FaunaPalette.CoatBelly;
                    s.WingTip = FaunaPalette.CoatLeg;
                    s.Tail = coat;
                    s.TailTip = FaunaPalette.CoatLeg;
                    break;
                default: // Hen
                    s = BirdMesher.Shape(0.25f, 0.08f, 0.085f, 14f, 0.075f, 0.031f, 16f, 0f, 0.026f, 0.031f, 0.02f, 0.007f, 0f, 0f, 0.006f, 0.11f, 0.06f, 0f,
                                         -48f, 0f, 0.1f, 0.0075f, 0.03f, 0.045f, 0.7f, 0.16f, 0.2f, 0.0058f);
                    s.Body = coat;
                    s.Belly = FaunaPalette.CoatBelly;
                    s.Back = FaunaPalette.CoatDark;
                    s.Head = coat;
                    s.Neck = 0xF6E8D0FFu;
                    s.Wing = FaunaPalette.CoatDark;
                    s.WingTip = 0xA09890FFu;
                    s.Tail = 0x2A242000u;
                    s.TailTip = 0x1E1A1800u;
                    break;
            }
            bool duck = species == FaunaSpecies.Duck;
            s.Bill = duck ? FaunaPalette.DuckBill : FaunaPalette.FowlBeak;
            s.BillTip = duck ? 0xE08A2000u : 0xC8A04Au << 8;
            s.Leg = duck ? FaunaPalette.DuckBill : FaunaPalette.FowlLeg;
            s.Iris = duck ? 0x2A1A1000u : 0xD87A2000u;
            s.BodyTinted = true;
            s.Toes = !duck;
            s.FoldOnCoarse = true;
            AvianMesher.Landmarks L = AvianMesher.Build(c, s, FaunaPalette.FeatherChannel);

            if (duck)
            {
                AvianMesher.WebbedFeet(c, s);
                if (pattern == CoatPattern.Saddle && d.Fine)
                {
                    // Mottled brown duck: a dark eye stripe and the blue speculum on the folded wing.
                    SideMark m = c.StartSide();
                    b.Ellipsoid(L.HeadC + new Fv3(-0.86f * s.HeadR, 0.1f * s.HeadR, 0.1f * s.HeadLen), new Fv3(-1f, 0f, 0.2f).Normalized, Fv3.Up,
                                0.022f, 0.004f, 0.003f, 3, 5, FaunaBone.Head, 0x3A302800u, FaunaPalette.FeatherChannel, FaunaPart.Face);
                    b.Ellipsoid(L.Shoulder + new Fv3(-0.86f * s.BodyW, -0.2f * s.BodyH, -0.45f * s.FoldLen), Fv3.Forward, Fv3.Up, 0.006f, 0.012f, 0.03f, 3,
                                5, FaunaBone.FoldL, 0x2E4A9A00u, FaunaPalette.FeatherChannel, FaunaPart.Wing);
                    c.EndSide(m);
                }
            }
            else
            {
                Comb(c, species, s, L);
                if (species == FaunaSpecies.Rooster) RoosterPlumes(c, s, L, pattern == CoatPattern.Patched);
                else HenTail(c, s, L);
            }
            b.GroundAoHeightM = Math.Max(0.03f, s.LegLen * 0.7f);
            b.OcclusionStrength = 0.8f;
            target.EyeHeightM = L.HeadC.Y;
            b.Finish();
        }

        /// <summary>Single comb (serrated points), wattles and the red earlobes.</summary>
        private static void Comb(FaunaSketch c, FaunaSpecies species, in AvianSpec s, in AvianMesher.Landmarks L)
        {
            FaunaBuilder b = c.B;
            FaunaDetail d = c.D;
            bool big = species == FaunaSpecies.Rooster;
            float h = big ? 0.038f : 0.016f, len = big ? 0.055f : 0.03f;
            Fv3 top = L.HeadC + new Fv3(0f, 0.85f * s.HeadR, 0.1f * s.HeadLen);
            // Blade of the comb.
            b.Ellipsoid(top + new Fv3(0f, 0.3f * h, 0.1f * len), Fv3.Forward, Fv3.Up, 0.004f, 0.55f * h, 0.5f * len, d.Coarse ? 2 : 3, d.Coarse ? 4 : d.SmallSegs + 2,
                        FaunaBone.Head, FaunaPalette.Comb, FaunaPalette.SkinChannel, FaunaPart.Comb);
            if (d.Fine)
            {
                int points = big ? 5 : 3;
                for (int k = 0; k < points; k++)
                {
                    float u = (k + 0.5f) / points - 0.5f;
                    float ph = h * (0.85f - 0.9f * u * u) * (big ? 1f : 0.9f);
                    var p = top + new Fv3(0f, 0.5f * h + 0.45f * ph, u * len * 0.9f + 0.1f * len);
                    b.Ellipsoid(p, Fv3.Forward, new Fv3(0f, 1f, -0.25f * u), 0.0035f, 0.45f * ph, 0.12f * len, 2, 4, FaunaBone.Head, FaunaPalette.Comb,
                                FaunaPalette.SkinChannel, FaunaPart.Comb);
                }
            }
            // Wattles under the beak.
            if (d.Far) return;
            float wl = big ? 0.03f : 0.014f;
            SideMark m = c.StartSide();
            b.Ellipsoid(L.BillBase + new Fv3(-0.006f, -0.5f * wl - 0.006f, -0.006f), Fv3.Forward, Fv3.Up, 0.004f, 0.5f * wl, 0.35f * wl, d.Coarse ? 2 : 3,
                        d.SmallSegs, FaunaBone.Head, FaunaPalette.Comb, FaunaPalette.SkinChannel, FaunaPart.Comb);
            // Earlobe.
            if (d.Fine)
                b.Ellipsoid(L.HeadC + new Fv3(-0.82f * s.HeadR, -0.35f * s.HeadR, -0.15f * s.HeadLen), new Fv3(-1f, 0f, 0f), Fv3.Up, 0.004f, 0.007f, 0.006f,
                            2, 4, FaunaBone.Head, 0xD0404000u, FaunaPalette.SkinChannel, FaunaPart.Comb);
            c.EndSide(m);
        }

        /// <summary>The hen's tail: a short upright fan of dark feathers.</summary>
        private static void HenTail(FaunaSketch c, in AvianSpec s, in AvianMesher.Landmarks L)
        {
            if (!c.D.Fine) return;
            for (int k = -1; k <= 1; k++)
            {
                var dir = new Fv3(0.25f * k, 0.85f, -0.45f).Normalized;
                Fv3 p0 = L.TailBase + new Fv3(0.01f * k, 0.01f, 0f);
                c.Begin();
                Add(c, p0, 0.016f, 0.004f, FaunaBone.Tail0, s.Tail);
                Add(c, p0 + dir * (0.6f * s.TailLen), 0.02f, 0.004f, FaunaBone.Tail0, s.Tail);
                Add(c, p0 + dir * s.TailLen + new Fv3(0f, -0.01f, -0.015f), 0.008f, 0.003f, FaunaBone.Tail0, s.TailTip);
                c.Tube(4, 1, FaunaPart.Tail, FaunaPalette.FeatherChannel, Fv3.Right, 0.4f, 0.4f);
            }
        }

        /// <summary>The rooster's hackle cape, saddle and the arching sickle feathers.</summary>
        private static void RoosterPlumes(FaunaSketch c, in AvianSpec s, in AvianMesher.Landmarks L, bool white)
        {
            FaunaBuilder b = c.B;
            FaunaDetail d = c.D;
            uint hackle = white ? FaunaPalette.Coat : 0xE8A838u << 8;
            uint saddle = white ? FaunaPalette.Coat : 0xD8822Cu << 8;
            // Hackle cape: a flared shell from the nape over the shoulders.
            Fv3 mid = (L.NeckTop + L.NeckBase) * 0.5f;
            b.Ellipsoid(mid + new Fv3(0f, -0.01f, -0.012f), new Fv3(0f, 0.95f, -0.3f).Normalized, Fv3.Forward, 1.45f * s.NeckR, 0.032f, 0.6f * s.NeckLen + 0.02f,
                        d.Coarse ? 2 : 4, d.BodySegs, FaunaBone.Neck, hackle, FaunaPalette.FeatherChannel, FaunaPart.Neck);
            // Saddle feathers over the back towards the tail.
            if (d.Fine)
                b.Ellipsoid(L.BodyC + L.Up * (0.75f * s.BodyH) - L.Fwd * (0.25f * s.BodyLen), L.Fwd, L.Up, 0.75f * s.BodyW, 0.25f * s.BodyH, 0.35f * s.BodyLen, 3,
                            d.BodySegs - 2, FaunaBone.Pelvis, saddle, FaunaPalette.FeatherChannel, FaunaPart.Body);
            // Sickles: long arching feathers up and over, falling behind.
            int n = d.Fine ? 3 : 1;
            for (int k = 0; k < n; k++)
            {
                float u = n == 1 ? 0f : k / (float)(n - 1) - 0.5f;
                Fv3 p0 = L.TailBase + new Fv3(u * 0.04f, 0.0f, 0.0f);
                float hgt = 0.16f - 0.03f * Math.Abs(u) * 2f, back = 0.14f + 0.04f * (0.5f - Math.Abs(u));
                c.Begin();
                Add(c, p0, 0.016f, 0.005f, FaunaBone.Tail0, s.Tail);
                Add(c, p0 + new Fv3(u * 0.05f, 0.7f * hgt, -0.25f * back), 0.017f, 0.004f, FaunaBone.Tail1, s.Tail);
                Add(c, p0 + new Fv3(u * 0.07f, hgt, -0.6f * back), 0.014f, 0.004f, FaunaBone.Tail1, s.Tail);
                Add(c, p0 + new Fv3(u * 0.08f, 0.75f * hgt, -back), 0.011f, 0.003f, FaunaBone.Tail2, s.TailTip);
                Add(c, p0 + new Fv3(u * 0.08f, 0.35f * hgt, -1.12f * back), 0.006f, 0.002f, FaunaBone.Tail2, s.TailTip);
                c.Tube(Math.Max(3, d.SmallSegs), 1, FaunaPart.Tail, FaunaPalette.FeatherChannel, Fv3.Right, 0.4f, 0.4f);
            }
        }

        private static void Add(FaunaSketch c, Fv3 p, float w, float t, FaunaBone bone, uint col)
        {
            c.K[c.N++] = new Knot(p.X, p.Y, p.Z, w, t, t, bone, col);
        }
    }
}
