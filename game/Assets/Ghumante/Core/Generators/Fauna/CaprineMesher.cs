using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Generators.Fauna
{
    /// <summary>
    /// The Khari hill goat of the valley's villages and pre-Dashain goat markets (docs/research/w2/ref_animals.md
    /// §4): small and compact (about 60 cm), a deep rumen barrel on thin legs, a straight face sloping down to a
    /// narrow muzzle, amber eyes with a horizontal pupil, leaf ears held out and a little down, short horns sweeping
    /// back, a chin beard and a short tail flicked up. Coats: black (most common), brown, white, pied, and a tan coat
    /// with dark face stripes, legs and dorsal line.
    /// </summary>
    public static class CaprineMesher
    {
        /// <summary>Builds the goat at <paramref name="lod"/> with <paramref name="pattern"/>.</summary>
        public static void Build(int lod, CoatPattern pattern, FaunaMesh target)
        {
            var b = new FaunaBuilder(target, FaunaSpecies.Goat, lod, pattern);
            var c = new FaunaSketch { B = b, D = FaunaDetail.Mammal(lod), S = 1f };
            FaunaDetail d = c.D;
            uint coat = FaunaPalette.Coat;
            MaterialChannel fur = FaunaPalette.FurChannel;

            b.Pivot(FaunaBone.Root, Fv3.Zero, false);
            b.Pivot(FaunaBone.Pelvis, c.P(0f, 0.55f, -0.24f), false);
            b.Pivot(FaunaBone.Chest, c.P(0f, 0.55f, 0.10f), false);
            b.Pivot(FaunaBone.Neck, c.P(0f, 0.62f, 0.24f), false);
            b.Pivot(FaunaBone.Head, c.P(0f, 0.77f, 0.35f), false);
            b.Pivot(FaunaBone.Jaw, c.P(0f, 0.73f, 0.40f), false);
            b.Pivot(FaunaBone.EarL, c.P(-0.04f, 0.80f, 0.365f), true);
            b.Pivot(FaunaBone.Tail0, c.P(0f, 0.62f, -0.39f), false);
            b.Pivot(FaunaBone.Tail1, c.P(0f, 0.65f, -0.415f), false);
            b.Pivot(FaunaBone.Tail2, c.P(0f, 0.69f, -0.42f), false);
            b.Pivot(FaunaBone.FrontUpperL, c.P(-0.065f, 0.50f, 0.15f), true);
            b.Pivot(FaunaBone.FrontLowerL, c.P(-0.07f, 0.21f, 0.17f), true);
            b.Pivot(FaunaBone.FrontFootL, c.P(-0.07f, 0.075f, 0.175f), true);
            b.Pivot(FaunaBone.HindUpperL, c.P(-0.07f, 0.54f, -0.26f), true);
            b.Pivot(FaunaBone.HindLowerL, c.P(-0.075f, 0.25f, -0.34f), true);
            b.Pivot(FaunaBone.HindFootL, c.P(-0.075f, 0.075f, -0.31f), true);

            // A deep, round barrel (the rumen), straight back, narrow chest.
            c.Begin();
            c.Add(0f, 0.585f, -0.405f, 0.045f, 0.04f, 0.05f, FaunaBone.Pelvis, coat);
            c.Add(0f, 0.58f, -0.34f, 0.09f, 0.07f, 0.1f, FaunaBone.Pelvis, coat);
            c.Add(0f, 0.565f, -0.19f, 0.115f, 0.085f, 0.145f, FaunaBone.Pelvis, coat);
            c.Add(0f, 0.56f, -0.02f, 0.125f, 0.09f, 0.165f, FaunaBone.Chest, coat);
            c.Add(0f, 0.57f, 0.13f, 0.105f, 0.09f, 0.15f, FaunaBone.Chest, coat);
            c.Add(0f, 0.585f, 0.23f, 0.08f, 0.075f, 0.115f, FaunaBone.Chest, coat);
            c.Add(0f, 0.6f, 0.285f, 0.04f, 0.04f, 0.05f, FaunaBone.Chest, coat);
            c.Tube(d.BodySegs, d.Rings, FaunaPart.Body, fur, Fv3.Up, 0.7f, 0.7f);

            // Neck, held up.
            c.Begin();
            c.Add(0f, 0.6f, 0.14f, 0.07f, 0.07f, 0.09f, FaunaBone.Chest, coat);
            c.Add(0f, 0.68f, 0.265f, 0.052f, 0.05f, 0.062f, FaunaBone.Neck, coat);
            c.Add(0f, 0.75f, 0.33f, 0.045f, 0.045f, 0.05f, FaunaBone.Neck, coat);
            c.Add(0f, 0.785f, 0.36f, 0.042f, 0.044f, 0.046f, FaunaBone.Head, coat);
            c.Tube(d.BodySegs - 2, d.Rings, FaunaPart.Neck, fur, Fv3.Up, 0.5f, 0.5f);

            // Head: narrow skull, straight face sloping down to a small muzzle.
            c.Begin();
            c.Add(0f, 0.825f, 0.34f, 0.036f, 0.03f, 0.03f, FaunaBone.Head, coat);
            c.Add(0f, 0.82f, 0.375f, 0.05f, 0.043f, 0.045f, FaunaBone.Head, coat);
            c.Add(0f, 0.795f, 0.42f, 0.044f, 0.037f, 0.045f, FaunaBone.Head, coat);
            c.Add(0f, 0.765f, 0.465f, 0.034f, 0.03f, 0.036f, FaunaBone.Head, coat);
            c.Add(0f, 0.738f, 0.505f, 0.027f, 0.024f, 0.027f, FaunaBone.Head, coat);
            c.Add(0f, 0.725f, 0.527f, 0.02f, 0.017f, 0.018f, FaunaBone.Head, FaunaPalette.MuzzleDark);
            int vHead = c.Tube(d.BodySegs, d.Rings, FaunaPart.Head, fur, Fv3.Up, 0.5f, 0.5f);
            BovineMesher.MarkMuzzle(b, vHead, c.P(0f, 0.745f, 0.49f), new Fv3(0f, -0.6f, 0.8f), FaunaPart.Muzzle);
            c.Begin();
            c.Add(0f, 0.75f, 0.42f, 0.028f, 0.018f, 0.02f, FaunaBone.Jaw, coat);
            c.Add(0f, 0.722f, 0.48f, 0.021f, 0.013f, 0.014f, FaunaBone.Jaw, coat);
            c.Add(0f, 0.712f, 0.51f, 0.016f, 0.01f, 0.01f, FaunaBone.Jaw, FaunaPalette.MuzzleDark);
            c.Tube(d.LimbSegs, d.LimbRings, FaunaPart.Mouth, fur, Fv3.Up, 0.5f, 0.6f);
            // Chin beard.
            if (!d.Far)
            {
                c.Begin();
                c.Add(0f, 0.72f, 0.45f, 0.012f, 0.01f, 0.012f, FaunaBone.Jaw, coat);
                c.Add(0f, 0.69f, 0.445f, 0.014f, 0.012f, 0.014f, FaunaBone.Jaw, coat);
                c.Add(0f, 0.655f, 0.435f, 0.008f, 0.006f, 0.006f, FaunaBone.Jaw, coat);
                c.Tube(d.SmallSegs, 1, FaunaPart.Beard, fur, Fv3.Forward, 0.4f, 0.6f);
            }

            {
                SideMark m = c.StartSide();
                // Amber eye with the horizontal slot pupil.
                Fv3 ec = c.P(-0.04f, 0.805f, 0.415f);
                Fv3 eo = new Fv3(-1f, 0.15f, 0.45f).Normalized;
                const float er = 0.0145f;
                b.Ellipsoid(ec, eo, Fv3.Up, er, er, er * 0.8f, d.Eyes >= 1 ? 3 : 2, d.Eyes >= 2 ? 7 : 4, FaunaBone.Head, 0xC8902A00u,
                            MaterialChannel.Paint, FaunaPart.Eye);
                if (d.Eyes > 0)
                {
                    b.Ellipsoid(ec + eo * (er * 0.66f), eo, Fv3.Up, er * 0.62f, er * 0.24f, er * 0.25f, 3, 5, FaunaBone.Head, FaunaPalette.EyeBlack,
                                MaterialChannel.Paint, FaunaPart.Eye);
                    Fv3 side = Fv3.Cross(Fv3.Up, eo).Normalized;
                    b.Ellipsoid(ec + eo * (er * 0.8f) + Fv3.Up * (er * 0.4f) + side * (er * 0.2f), eo, Fv3.Up, er * 0.22f, er * 0.22f, er * 0.1f, 2, 4,
                                FaunaBone.Head, 0xFFFFFF00u, MaterialChannel.Glass, FaunaPart.Eye);
                }
                if (d.Eyes >= 2)
                    b.Ellipsoid(ec + Fv3.Up * (er * 0.45f), eo, new Fv3(0f, 1f, -0.2f), er * 1.12f, er * 0.55f, er * 0.95f, 3, d.SmallSegs,
                                FaunaBone.Head, coat, fur, FaunaPart.Face);
                // Leaf ear held out and a little down.
                c.Begin();
                c.Add(-0.038f, 0.80f, 0.365f, 0.014f, 0.007f, 0.007f, FaunaBone.EarL, coat);
                c.Add(-0.07f, 0.79f, 0.37f, 0.024f, 0.006f, 0.006f, FaunaBone.EarL, coat);
                c.Add(-0.11f, 0.772f, 0.378f, 0.022f, 0.005f, 0.005f, FaunaBone.EarL, coat);
                c.Add(-0.14f, 0.755f, 0.386f, 0.008f, 0.004f, 0.004f, FaunaBone.EarL, coat);
                int vEar = c.Tube(d.LimbSegs, d.LimbRings, FaunaPart.Ear, fur, new Fv3(0f, 0.6f, 1f), 0.4f, 0.6f);
                BovineMesher.PaintEarInside(b, vEar, new Fv3(0f, 0.6f, 0.8f), 0xD8A89C00u);
                // Short horn sweeping back and out, ridged.
                float[] hp = { -0.022f, 0.84f, 0.362f, -0.028f, 0.876f, 0.342f, -0.037f, 0.897f, 0.31f, -0.046f, 0.901f, 0.278f, -0.053f, 0.892f, 0.252f };
                FaunaShapes.Horn(c, hp, 5, 0.0125f, 0.002f, 0x8E827200u, 0x4A423A00u, FaunaBone.Head, 700f);
                // Nostril.
                if (d.Fine)
                    c.Ell(-0.011f, 0.728f, 0.532f, 0.006f, 0.004f, 0.004f, new Fv3(0f, -0.4f, 1f).Normalized, Fv3.Up, 3, 5, FaunaBone.Head,
                          FaunaPalette.Nose, FaunaPalette.NoseChannel, FaunaPart.Nose);

                // Legs: thin, knobbly knees and small cloven hooves.
                c.Begin();
                c.Add(-0.055f, 0.53f, 0.14f, 0.045f, 0.05f, 0.05f, FaunaBone.FrontUpperL, coat);
                c.Add(-0.068f, 0.38f, 0.145f, 0.035f, 0.038f, 0.04f, FaunaBone.FrontUpperL, coat);
                c.Add(-0.07f, 0.27f, 0.16f, 0.022f, 0.024f, 0.024f, FaunaBone.FrontUpperL, FaunaPalette.CoatLeg);
                c.Add(-0.07f, 0.21f, 0.17f, 0.02f, 0.023f, 0.022f, FaunaBone.FrontLowerL, FaunaPalette.CoatLeg);
                c.Add(-0.07f, 0.13f, 0.172f, 0.014f, 0.015f, 0.015f, FaunaBone.FrontLowerL, FaunaPalette.CoatLeg);
                c.Add(-0.07f, 0.075f, 0.175f, 0.016f, 0.017f, 0.017f, FaunaBone.FrontFootL, FaunaPalette.CoatLeg);
                c.Add(-0.07f, 0.04f, 0.18f, 0.015f, 0.016f, 0.016f, FaunaBone.FrontFootL, FaunaPalette.CoatLeg);
                c.Tube(d.LimbSegs - 1, d.LimbRings, FaunaPart.Leg, fur, Fv3.Forward, 0.5f, 0.3f);
                if (!d.Far) FaunaShapes.Hoof(c, -0.07f, 0.18f, 0.0165f, 0.045f, FaunaBone.FrontFootL, FaunaPalette.Hoof);
                c.Begin();
                c.Add(-0.06f, 0.56f, -0.25f, 0.06f, 0.07f, 0.07f, FaunaBone.HindUpperL, coat);
                c.Add(-0.075f, 0.42f, -0.24f, 0.048f, 0.055f, 0.052f, FaunaBone.HindUpperL, coat);
                c.Add(-0.075f, 0.31f, -0.31f, 0.026f, 0.03f, 0.03f, FaunaBone.HindUpperL, coat);
                c.Add(-0.075f, 0.25f, -0.34f, 0.019f, 0.024f, 0.022f, FaunaBone.HindLowerL, FaunaPalette.CoatLeg);
                c.Add(-0.075f, 0.15f, -0.32f, 0.014f, 0.015f, 0.015f, FaunaBone.HindLowerL, FaunaPalette.CoatLeg);
                c.Add(-0.075f, 0.075f, -0.31f, 0.016f, 0.017f, 0.017f, FaunaBone.HindFootL, FaunaPalette.CoatLeg);
                c.Add(-0.075f, 0.04f, -0.305f, 0.015f, 0.016f, 0.016f, FaunaBone.HindFootL, FaunaPalette.CoatLeg);
                c.Tube(d.LimbSegs - 1, d.LimbRings, FaunaPart.Leg, fur, Fv3.Forward, 0.5f, 0.3f);
                if (!d.Far) FaunaShapes.Hoof(c, -0.075f, -0.305f, 0.0165f, 0.045f, FaunaBone.HindFootL, FaunaPalette.Hoof);
                c.EndSide(m);
            }

            // Short tail flicked up.
            c.Begin();
            c.Add(0f, 0.615f, -0.39f, 0.02f, 0.018f, 0.018f, FaunaBone.Tail0, coat);
            c.Add(0f, 0.65f, -0.42f, 0.02f, 0.012f, 0.012f, FaunaBone.Tail1, coat);
            c.Add(0f, 0.695f, -0.425f, 0.016f, 0.01f, 0.01f, FaunaBone.Tail2, coat);
            c.Add(0f, 0.72f, -0.418f, 0.008f, 0.006f, 0.006f, FaunaBone.Tail2, coat);
            c.Tube(d.LimbSegs - 2, d.LimbRings, FaunaPart.Tail, fur, Fv3.Forward, 0.5f, 0.6f);

            // A rope collar with a little bell: village goats are owned and tethered.
            if (!d.Coarse) FaunaShapes.CollarBell(c, new Fv3(0f, 0.695f, 0.283f), 0.05f, 56f, 0.026f, FaunaBone.Neck);

            PaintCoat(b, pattern);
            b.GroundAoHeightM = 0.2f;
            target.EyeHeightM = 0.80f;
            b.Finish();
        }

        /// <summary>Coat: paler belly (Plain); white patches (Patched); dark face stripes, legs and dorsal line on a tan
        /// coat (Saddle).</summary>
        private static void PaintCoat(FaunaBuilder b, CoatPattern pattern)
        {
            FaunaMesh f = b.Target;
            for (int v = 0; v < b.VertexCount; v++)
            {
                uint col = b.ColourAt(v);
                if ((col & 0xFFu) == 0u) continue;
                FaunaPart part = (FaunaPart)f.Part[v];
                Fv3 p = b.PositionOf(v), n = b.NormalOf(v);
                float under = FMath.SmoothStep(-0.2f, -0.8f, n.Y);
                col = FMath.LerpColour(col, FaunaPalette.CoatBelly, 0.5f * under);
                if (pattern == CoatPattern.Patched)
                {
                    float w = FaunaPalette.Patches(p, 7f, 0x60A7u);
                    if (w > 0.55f || (part == FaunaPart.Leg && p.Y < 0.14f)) col = FaunaPalette.PatchWhite;
                }
                else if (pattern == CoatPattern.Saddle)
                {
                    bool dark = part == FaunaPart.Leg && p.Y < 0.3f || part == FaunaPart.Beard ||
                                ((part == FaunaPart.Body || part == FaunaPart.Neck) && n.Y > 0.85f) ||
                                ((part == FaunaPart.Head || part == FaunaPart.Muzzle) && Math.Abs(Math.Abs(p.X) - 0.022f) < 0.009f && n.Y > -0.2f);
                    if (dark) col = 0x2A262400u;
                }
                b.SetColour(v, col);
            }
        }
    }
}
