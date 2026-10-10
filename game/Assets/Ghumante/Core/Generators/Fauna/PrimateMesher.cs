using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Generators.Fauna
{
    /// <summary>
    /// The rhesus macaque of Swayambhu and Pashupati (docs/research/w2/ref_animals.md §5): bind pose on all fours
    /// (shoulders about 42 cm), brown-grey shoulders and arms turning golden-orange over the lower back and rump, a
    /// paler belly, a bare pink face with close-set amber eyes under a heavy brow, a short muzzle, small rounded ears
    /// close to the head, dark grey-pink hands and feet with fingers, and a medium tail hanging in a curve. The baby is
    /// half the size with a big head, darker fur and a pinker face; it rides its mother's back (the
    /// <see cref="FaunaBone.Rider"/> pivot) or sits close by. Sitting, climbing and grooming are poses.
    /// </summary>
    public static class PrimateMesher
    {
        /// <summary>Builds the macaque or its baby at <paramref name="lod"/>.</summary>
        public static void Build(FaunaSpecies species, int lod, FaunaMesh target)
        {
            var b = new FaunaBuilder(target, species, lod, CoatPattern.Plain);
            bool baby = species == FaunaSpecies.MacaqueBaby;
            var c = new FaunaSketch { B = b, D = FaunaDetail.Mammal(lod), S = baby ? 0.5f : 1f };
            FaunaDetail d = c.D;
            uint fur = baby ? 0x8A8078FFu : FaunaPalette.Coat;
            uint rump = baby ? FaunaPalette.Coat : FaunaPalette.MacaqueRump;
            uint belly = FaunaPalette.CoatBelly;
            uint face = baby ? 0xE9B3A400u : 0xD8907E00u;
            uint hand = 0x6E5A5000u;
            MaterialChannel hair = FaunaPalette.FurChannel;
            float hs = baby ? 1.7f : 1f; // head scale

            b.Pivot(FaunaBone.Root, Fv3.Zero, false);
            b.Pivot(FaunaBone.Pelvis, c.P(0f, 0.40f, -0.14f), false);
            b.Pivot(FaunaBone.Chest, c.P(0f, 0.42f, 0.08f), false);
            b.Pivot(FaunaBone.Neck, c.P(0f, 0.45f, 0.19f), false);
            b.Pivot(FaunaBone.Head, c.P(0f, 0.48f, 0.23f), false);
            b.Pivot(FaunaBone.Jaw, c.P(0f, 0.455f, 0.27f), false);
            b.Pivot(FaunaBone.EarL, c.P(-0.045f * hs, 0.50f, 0.24f), true);
            b.Pivot(FaunaBone.Tail0, c.P(0f, 0.42f, -0.24f), false);
            b.Pivot(FaunaBone.Tail1, c.P(0f, 0.39f, -0.31f), false);
            b.Pivot(FaunaBone.Tail2, c.P(0f, 0.31f, -0.36f), false);
            b.Pivot(FaunaBone.FrontUpperL, c.P(-0.075f, 0.42f, 0.14f), true);
            b.Pivot(FaunaBone.FrontLowerL, c.P(-0.085f, 0.23f, 0.12f), true);
            b.Pivot(FaunaBone.FrontFootL, c.P(-0.085f, 0.04f, 0.15f), true);
            b.Pivot(FaunaBone.HindUpperL, c.P(-0.07f, 0.38f, -0.16f), true);
            b.Pivot(FaunaBone.HindLowerL, c.P(-0.085f, 0.23f, -0.07f), true);
            b.Pivot(FaunaBone.HindFootL, c.P(-0.08f, 0.05f, -0.17f), true);
            b.Pivot(FaunaBone.Rider, c.P(0f, 0.50f, -0.04f), false);

            // Torso: deep chest, slim waist, the rump a little higher than the shoulders' line when walking.
            c.Begin();
            c.Add(0f, 0.415f, -0.225f, 0.05f, 0.045f, 0.05f, FaunaBone.Pelvis, rump);
            c.Add(0f, 0.41f, -0.175f, 0.085f, 0.066f, 0.078f, FaunaBone.Pelvis, rump);
            c.Add(0f, 0.405f, -0.085f, 0.088f, 0.066f, 0.088f, FaunaBone.Pelvis, FMath.LerpColour(rump, fur, 0.5f));
            c.Add(0f, 0.41f, 0.02f, 0.095f, 0.07f, 0.095f, FaunaBone.Chest, fur);
            c.Add(0f, 0.42f, 0.1f, 0.093f, 0.074f, 0.088f, FaunaBone.Chest, fur);
            c.Add(0f, 0.43f, 0.165f, 0.072f, 0.062f, 0.07f, FaunaBone.Chest, fur);
            c.Add(0f, 0.44f, 0.205f, 0.04f, 0.035f, 0.04f, FaunaBone.Chest, fur);
            int vBody = c.Tube(d.BodySegs, d.Rings, FaunaPart.Body, hair, Fv3.Up, 0.7f, 0.6f);
            for (int v = vBody; v < b.VertexCount; v++)
                if (b.NormalOf(v).Y < -0.3f) b.SetColour(v, FMath.LerpColour(b.ColourAt(v), belly, 0.7f));

            // Head: round cranium, heavy brow, short muzzle; the face is bare pink skin ringed with fur.
            float hy = 0.485f, hz = 0.255f;
            c.Ell(0f, hy + 0.012f * hs, hz - 0.005f, 0.05f * hs, 0.048f * hs, 0.05f * hs, Fv3.Forward, Fv3.Up, d.Fine ? 6 : d.Far ? 3 : 4, d.BodySegs,
                  FaunaBone.Head, fur, hair, FaunaPart.Head);
            // Face disc (bare skin).
            c.Ell(0f, hy + 0.0f * hs, hz + 0.03f * hs, 0.038f * hs, 0.042f * hs, 0.028f * hs, Fv3.Forward, Fv3.Up, d.Fine ? 4 : 3, Math.Max(4, d.BodySegs - 2),
                  FaunaBone.Head, face, FaunaPalette.SkinChannel, FaunaPart.Face);
            // Brow ridge.
            if (!d.Far)
                c.Ell(0f, hy + 0.022f * hs, hz + 0.045f * hs, 0.034f * hs, 0.01f * hs, 0.014f * hs, Fv3.Forward, Fv3.Up, 3, d.SmallSegs + 2,
                      FaunaBone.Head, FMath.LerpColour(face, 0x8A5A4A00u, 0.3f), FaunaPalette.SkinChannel, FaunaPart.Face);
            // Muzzle and the jaw.
            c.Ell(0f, hy - 0.022f * hs, hz + 0.055f * hs, 0.026f * hs, 0.022f * hs, 0.024f * hs, Fv3.Forward, Fv3.Up, d.Fine ? 4 : 3, d.SmallSegs + 2,
                  FaunaBone.Head, face, FaunaPalette.SkinChannel, FaunaPart.Muzzle);
            if (!d.Far)
                c.Ell(0f, hy - 0.04f * hs, hz + 0.045f * hs, 0.022f * hs, 0.012f * hs, 0.02f * hs, Fv3.Forward, Fv3.Up, 3, d.SmallSegs + 1,
                      FaunaBone.Jaw, face, FaunaPalette.SkinChannel, FaunaPart.Mouth);
            if (d.Fine)
            {
                SideMark n = c.StartSide();
                c.Ell(-0.006f * hs, hy - 0.016f * hs, hz + 0.077f * hs, 0.004f * hs, 0.003f * hs, 0.003f * hs, Fv3.Forward, Fv3.Up, 2, 4, FaunaBone.Head,
                      0x5A2E2A00u, FaunaPalette.NoseChannel, FaunaPart.Nose);
                c.EndSide(n);
            }
            {
                SideMark m = c.StartSide();
                // Close-set amber eyes looking forward.
                Fv3 ec = c.P(-0.016f * hs, hy + 0.008f * hs, hz + 0.052f * hs);
                b.Eye(ec, 0.0095f * hs * c.S, new Fv3(-0.25f, 0.05f, 1f).Normalized, Fv3.Up, FaunaBone.Head, 0x8A5A2600u, FaunaPalette.EyeBlack,
                      d.Eyes);
                // Small round ear close to the head.
                c.Ell(-0.05f * hs, hy + 0.008f * hs, hz - 0.005f, 0.008f * hs, 0.016f * hs, 0.014f * hs, new Fv3(-1f, 0f, 0.3f).Normalized, Fv3.Up,
                      3, d.SmallSegs + 1, FaunaBone.EarL, FMath.LerpColour(face, 0x9A7A6A00u, 0.5f), FaunaPalette.SkinChannel, FaunaPart.Ear);
                c.EndSide(m);
            }
            // Fur cheeks and crown framing the face.
            if (!d.Far)
            {
                c.Ell(0f, hy + 0.04f * hs, hz - 0.01f * hs, 0.042f * hs, 0.02f * hs, 0.04f * hs, Fv3.Forward, new Fv3(0f, 1f, -0.4f), 3,
                      d.SmallSegs + 2, FaunaBone.Head, fur, hair, FaunaPart.Head);
            }

            // Neck.
            c.Begin();
            c.Add(0f, 0.435f, 0.15f, 0.055f, 0.05f, 0.055f, FaunaBone.Chest, fur);
            c.Add(0f, 0.46f, 0.21f, 0.04f, 0.04f, 0.042f, FaunaBone.Neck, fur);
            c.Add(0f, 0.475f, 0.24f, 0.038f, 0.038f, 0.04f, FaunaBone.Head, fur);
            c.Tube(d.BodySegs - 3, 1, FaunaPart.Neck, hair, Fv3.Up, 0.5f, 0.5f);

            // Limbs: arms with elbows bent back, legs with knees forward; hands and feet with fingers.
            {
                SideMark m = c.StartSide();
                c.Begin();
                c.Add(-0.06f, 0.43f, 0.14f, 0.035f, 0.035f, 0.035f, FaunaBone.FrontUpperL, fur);
                c.Add(-0.08f, 0.32f, 0.13f, 0.027f, 0.028f, 0.028f, FaunaBone.FrontUpperL, fur);
                c.Add(-0.085f, 0.23f, 0.12f, 0.022f, 0.022f, 0.022f, FaunaBone.FrontLowerL, fur);
                c.Add(-0.085f, 0.12f, 0.14f, 0.017f, 0.017f, 0.017f, FaunaBone.FrontLowerL, fur);
                c.Add(-0.085f, 0.05f, 0.152f, 0.014f, 0.014f, 0.014f, FaunaBone.FrontFootL, hand);
                c.Add(-0.085f, 0.022f, 0.162f, 0.014f, 0.012f, 0.012f, FaunaBone.FrontFootL, hand);
                c.Tube(d.LimbSegs, d.LimbRings, FaunaPart.Leg, hair, Fv3.Forward, 0.5f, 0.3f);
                Hand(c, -0.085f, 0.015f, 0.17f, FaunaBone.FrontFootL, hand, 0.03f);
                c.Begin();
                c.Add(-0.055f, 0.4f, -0.17f, 0.045f, 0.045f, 0.045f, FaunaBone.HindUpperL, rump);
                c.Add(-0.078f, 0.3f, -0.11f, 0.035f, 0.035f, 0.035f, FaunaBone.HindUpperL, FMath.LerpColour(rump, fur, 0.5f));
                c.Add(-0.085f, 0.23f, -0.07f, 0.025f, 0.026f, 0.026f, FaunaBone.HindLowerL, fur);
                c.Add(-0.083f, 0.13f, -0.13f, 0.02f, 0.02f, 0.02f, FaunaBone.HindLowerL, fur);
                c.Add(-0.08f, 0.06f, -0.17f, 0.016f, 0.016f, 0.016f, FaunaBone.HindFootL, hand);
                c.Add(-0.08f, 0.024f, -0.16f, 0.016f, 0.013f, 0.013f, FaunaBone.HindFootL, hand);
                c.Tube(d.LimbSegs, d.LimbRings, FaunaPart.Leg, hair, Fv3.Forward, 0.5f, 0.3f);
                Hand(c, -0.08f, 0.015f, -0.14f, FaunaBone.HindFootL, hand, 0.045f);
                c.EndSide(m);
            }

            // Tail hanging in a curve.
            c.Begin();
            c.Add(0f, 0.425f, -0.235f, 0.02f, 0.02f, 0.02f, FaunaBone.Tail0, rump);
            c.Add(0f, 0.415f, -0.29f, 0.016f, 0.016f, 0.016f, FaunaBone.Tail0, rump);
            c.Add(0f, 0.38f, -0.33f, 0.014f, 0.014f, 0.014f, FaunaBone.Tail1, fur);
            c.Add(0f, 0.32f, -0.36f, 0.012f, 0.012f, 0.012f, FaunaBone.Tail2, fur);
            c.Add(0f, 0.26f, -0.365f, 0.009f, 0.009f, 0.009f, FaunaBone.Tail2, fur);
            c.Tube(d.LimbSegs - 2, d.LimbRings, FaunaPart.Tail, hair, Fv3.Forward, 0.5f, 0.6f);

            b.GroundAoHeightM = 0.15f * c.S;
            target.EyeHeightM = (hy + 0.008f * hs) * c.S;
            b.Finish();
        }

        /// <summary>A hand or foot: a flat palm and (near level) four fingers and a thumb.</summary>
        private static void Hand(FaunaSketch c, float x, float y, float z, FaunaBone bone, uint col, float len)
        {
            FaunaDetail d = c.D;
            if (d.Far) return;
            c.Ell(x, y + 0.004f, z, 0.017f, 0.01f, len * 0.5f, Fv3.Forward, Fv3.Up, 3, d.SmallSegs + 1, bone, col, FaunaPalette.SkinChannel, FaunaPart.Hand);
            if (!d.Fine) return;
            for (int k = 0; k < 4; k++)
            {
                float fx = x - 0.012f + k * 0.008f;
                c.Begin();
                c.Add(fx, y + 0.004f, z + len * 0.35f, 0.0042f, bone, col);
                c.Add(fx + (k - 1.5f) * 0.002f, y + 0.002f, z + len * 0.35f + 0.016f, 0.0035f, bone, col);
                c.Tube(4, 1, FaunaPart.Hand, FaunaPalette.SkinChannel, Fv3.Up, 0.5f, 0.6f);
            }
            c.Begin();
            c.Add(x + 0.012f, y + 0.004f, z, 0.0045f, bone, col);
            c.Add(x + 0.022f, y + 0.003f, z + 0.012f, 0.0035f, bone, col);
            c.Tube(4, 1, FaunaPart.Hand, FaunaPalette.SkinChannel, Fv3.Up, 0.5f, 0.6f);
        }
    }
}
