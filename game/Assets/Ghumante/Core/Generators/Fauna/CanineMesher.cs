using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Generators.Fauna
{
    /// <summary>
    /// The Kathmandu street dog (docs/research/w2/ref_animals.md §3): a medium, lean pariah-type dog about 52 cm at
    /// the shoulder with a short smooth coat, a wedge-shaped head with a black nose, amber eyes, pricked triangular
    /// ears (one often tipped over), a deep chest and tucked belly, and the sickle tail curled over the back. Coats:
    /// tan with a dark muzzle (plain), black-and-tan saddle, white socks and blaze, white with patches.
    /// </summary>
    public static class CanineMesher
    {
        /// <summary>Builds the dog at <paramref name="lod"/> with <paramref name="pattern"/>.</summary>
        public static void Build(int lod, CoatPattern pattern, FaunaMesh target)
        {
            var b = new FaunaBuilder(target, FaunaSpecies.Dog, lod, pattern);
            var c = new FaunaSketch { B = b, D = FaunaDetail.Mammal(lod), S = 1f };
            FaunaDetail d = c.D;
            uint coat = FaunaPalette.Coat;
            MaterialChannel fur = FaunaPalette.FurChannel;

            b.Pivot(FaunaBone.Root, Fv3.Zero, false);
            b.Pivot(FaunaBone.Pelvis, c.P(0f, 0.44f, -0.20f), false);
            b.Pivot(FaunaBone.Chest, c.P(0f, 0.42f, 0.08f), false);
            b.Pivot(FaunaBone.Neck, c.P(0f, 0.47f, 0.20f), false);
            b.Pivot(FaunaBone.Head, c.P(0f, 0.60f, 0.31f), false);
            b.Pivot(FaunaBone.Jaw, c.P(0f, 0.585f, 0.37f), false);
            b.Pivot(FaunaBone.EarL, c.P(-0.04f, 0.66f, 0.33f), true);
            b.Pivot(FaunaBone.Tail0, c.P(0f, 0.47f, -0.31f), false);
            b.Pivot(FaunaBone.Tail1, c.P(0f, 0.56f, -0.36f), false);
            b.Pivot(FaunaBone.Tail2, c.P(0f, 0.64f, -0.32f), false);
            b.Pivot(FaunaBone.FrontUpperL, c.P(-0.06f, 0.42f, 0.13f), true);
            b.Pivot(FaunaBone.FrontLowerL, c.P(-0.068f, 0.11f, 0.15f), true);
            b.Pivot(FaunaBone.FrontFootL, c.P(-0.068f, 0.04f, 0.16f), true);
            b.Pivot(FaunaBone.HindUpperL, c.P(-0.065f, 0.43f, -0.22f), true);
            b.Pivot(FaunaBone.HindLowerL, c.P(-0.072f, 0.15f, -0.29f), true);
            b.Pivot(FaunaBone.HindFootL, c.P(-0.072f, 0.045f, -0.27f), true);

            // Body: a deep chest, a tucked waist, a sturdy rump under a thick coat.
            c.Begin();
            c.Add(0f, 0.45f, -0.335f, 0.05f, 0.045f, 0.05f, FaunaBone.Pelvis, coat);
            c.Add(0f, 0.44f, -0.285f, 0.088f, 0.07f, 0.1f, FaunaBone.Pelvis, coat);
            c.Add(0f, 0.44f, -0.15f, 0.09f, 0.072f, 0.09f, FaunaBone.Pelvis, coat);
            c.Add(0f, 0.425f, -0.02f, 0.105f, 0.085f, 0.135f, FaunaBone.Chest, coat);
            c.Add(0f, 0.42f, 0.10f, 0.112f, 0.09f, 0.155f, FaunaBone.Chest, coat);
            c.Add(0f, 0.43f, 0.19f, 0.1f, 0.085f, 0.14f, FaunaBone.Chest, coat);
            c.Add(0f, 0.45f, 0.25f, 0.052f, 0.05f, 0.07f, FaunaBone.Chest, coat);
            c.Tube(d.BodySegs, d.Rings, FaunaPart.Body, fur, Fv3.Up, 0.7f, 0.7f);

            // Neck, carried up and forward, thick with the winter ruff: the ruff is the neck loft itself, full at the
            // shoulders and tapering to the head, so it flows into the withers and the cheeks (no separate collar).
            c.Begin();
            c.Add(0f, 0.44f, 0.10f, 0.085f, 0.085f, 0.11f, FaunaBone.Chest, coat);
            c.Add(0f, 0.49f, 0.19f, 0.09f, 0.082f, 0.098f, FaunaBone.Neck, coat);
            c.Add(0f, 0.535f, 0.25f, 0.074f, 0.07f, 0.08f, FaunaBone.Neck, coat);
            c.Add(0f, 0.58f, 0.295f, 0.06f, 0.06f, 0.064f, FaunaBone.Neck, coat);
            c.Add(0f, 0.6f, 0.32f, 0.054f, 0.055f, 0.056f, FaunaBone.Head, coat);
            c.Tube(d.BodySegs - 2, d.Rings, FaunaPart.Neck, fur, Fv3.Up, 0.5f, 0.5f);

            // Head: a broad skull and cheeks, a clear stop, a medium muzzle and a black nose.
            c.Begin();
            c.Add(0f, 0.628f, 0.298f, 0.045f, 0.035f, 0.035f, FaunaBone.Head, coat);
            c.Add(0f, 0.632f, 0.33f, 0.063f, 0.055f, 0.05f, FaunaBone.Head, coat);
            c.Add(0f, 0.624f, 0.372f, 0.064f, 0.05f, 0.052f, FaunaBone.Head, coat);
            c.Add(0f, 0.607f, 0.412f, 0.043f, 0.034f, 0.04f, FaunaBone.Head, coat);
            c.Add(0f, 0.593f, 0.452f, 0.031f, 0.026f, 0.03f, FaunaBone.Head, coat);
            c.Add(0f, 0.588f, 0.478f, 0.024f, 0.021f, 0.022f, FaunaBone.Head, coat);
            int vHead = c.Tube(d.BodySegs, d.Rings, FaunaPart.Head, fur, Fv3.Up, 0.5f, 0.5f);
            BovineMesher.MarkMuzzle(b, vHead, c.P(0f, 0.6f, 0.415f), Fv3.Forward, FaunaPart.Muzzle);
            // Nose leather.
            c.Ell(0f, 0.597f, 0.49f, 0.017f, 0.012f, 0.011f, Fv3.Forward, Fv3.Up, 3, d.SmallSegs, FaunaBone.Head, FaunaPalette.Nose,
                  FaunaPalette.NoseChannel, FaunaPart.Nose);
            // Lower jaw with the dark lip line.
            if (!d.Far)
            {
                c.Begin();
                c.Add(0f, 0.592f, 0.37f, 0.036f, 0.022f, 0.026f, FaunaBone.Jaw, coat);
                c.Add(0f, 0.574f, 0.425f, 0.027f, 0.016f, 0.018f, FaunaBone.Jaw, coat);
                c.Add(0f, 0.57f, 0.468f, 0.018f, 0.012f, 0.012f, FaunaBone.Jaw, coat);
                c.Tube(d.LimbSegs, d.LimbRings, FaunaPart.Mouth, fur, Fv3.Up, 0.5f, 0.6f);
            }
            if (d.Fine)
            {
                SideMark m = c.StartSide();
                c.Begin();
                c.Add(-0.028f, 0.584f, 0.41f, 0.004f, FaunaBone.Head, FaunaPalette.Nose);
                c.Add(-0.022f, 0.58f, 0.44f, 0.004f, FaunaBone.Head, FaunaPalette.Nose);
                c.Add(-0.012f, 0.58f, 0.47f, 0.0035f, FaunaBone.Head, FaunaPalette.Nose);
                c.Tube(4, 1, FaunaPart.Mouth, FaunaPalette.SkinChannel, Fv3.Up, 0.5f, 0.5f);
                c.EndSide(m);
            }

            // Eyes (amber, set forward) with a brow, and small pricked ears (the right one tipped over at the end).
            {
                SideMark m = c.StartSide();
                Fv3 ec = c.P(-0.035f, 0.64f, 0.398f);
                Fv3 eo = new Fv3(-0.55f, 0.12f, 0.83f).Normalized;
                b.Eye(ec, 0.0145f, eo, Fv3.Up, FaunaBone.Head, 0x7A4A1C00u, FaunaPalette.EyeBlack, d.Eyes);
                if (d.Eyes >= 2)
                {
                    b.Ellipsoid(ec + Fv3.Up * 0.008f - eo * 0.002f, eo, new Fv3(0f, 1f, -0.2f), 0.016f, 0.008f, 0.014f, 3, d.SmallSegs,
                                FaunaBone.Head, coat, fur, FaunaPart.Face);
                }
                c.EndSide(m);
            }
            for (int side = 0; side < 2; side++)
            {
                float sx = side == 0 ? -1f : 1f;
                FaunaBone ear = side == 0 ? FaunaBone.EarL : FaunaBone.EarR;
                c.Begin();
                c.Add(sx * 0.04f, 0.655f, 0.335f, 0.03f, 0.008f, 0.009f, ear, coat);
                c.Add(sx * 0.048f, 0.685f, 0.33f, 0.023f, 0.007f, 0.007f, ear, coat);
                if (side == 1)
                {
                    c.Add(sx * 0.054f, 0.708f, 0.335f, 0.013f, 0.005f, 0.005f, ear, coat);
                    c.Add(sx * 0.056f, 0.71f, 0.353f, 0.006f, 0.004f, 0.004f, ear, coat);
                }
                else
                {
                    c.Add(sx * 0.054f, 0.71f, 0.325f, 0.012f, 0.005f, 0.005f, ear, coat);
                    c.Add(sx * 0.058f, 0.727f, 0.32f, 0.003f, 0.003f, 0.003f, ear, coat);
                }
                int vEar = c.Tube(d.LimbSegs, d.LimbRings, FaunaPart.Ear, fur, new Fv3(sx, 0f, 0f), 0.4f, 0.5f);
                BovineMesher.PaintEarInside(b, vEar, new Fv3(0.15f * sx, 0.1f, 1f).Normalized, 0xD9A39A00u);
            }

            // Legs: sturdy, straight in front with a pastern angle, an angulated hock behind; round paws with toes.
            {
                SideMark m = c.StartSide();
                c.Begin();
                c.Add(-0.052f, 0.43f, 0.13f, 0.046f, 0.054f, 0.054f, FaunaBone.FrontUpperL, coat);
                c.Add(-0.066f, 0.3f, 0.12f, 0.04f, 0.044f, 0.046f, FaunaBone.FrontUpperL, coat);
                c.Add(-0.068f, 0.2f, 0.135f, 0.028f, 0.03f, 0.03f, FaunaBone.FrontUpperL, FaunaPalette.CoatLeg);
                c.Add(-0.068f, 0.11f, 0.15f, 0.023f, 0.024f, 0.024f, FaunaBone.FrontLowerL, FaunaPalette.CoatLeg);
                c.Add(-0.068f, 0.05f, 0.16f, 0.022f, 0.022f, 0.022f, FaunaBone.FrontFootL, FaunaPalette.CoatLeg);
                c.Add(-0.068f, 0.03f, 0.17f, 0.022f, 0.022f, 0.022f, FaunaBone.FrontFootL, FaunaPalette.CoatLeg);
                c.Tube(d.LimbSegs, d.LimbRings, FaunaPart.Leg, fur, Fv3.Forward, 0.5f, 0.3f);
                Paw(c, -0.068f, 0.18f, FaunaBone.FrontFootL);
                c.Begin();
                c.Add(-0.052f, 0.45f, -0.215f, 0.06f, 0.07f, 0.072f, FaunaBone.HindUpperL, coat);
                c.Add(-0.074f, 0.32f, -0.18f, 0.052f, 0.058f, 0.056f, FaunaBone.HindUpperL, coat);
                c.Add(-0.074f, 0.22f, -0.25f, 0.034f, 0.036f, 0.036f, FaunaBone.HindUpperL, coat);
                c.Add(-0.072f, 0.15f, -0.29f, 0.024f, 0.027f, 0.025f, FaunaBone.HindLowerL, FaunaPalette.CoatLeg);
                c.Add(-0.072f, 0.09f, -0.28f, 0.022f, 0.022f, 0.022f, FaunaBone.HindLowerL, FaunaPalette.CoatLeg);
                c.Add(-0.072f, 0.05f, -0.27f, 0.022f, 0.022f, 0.022f, FaunaBone.HindFootL, FaunaPalette.CoatLeg);
                c.Add(-0.072f, 0.03f, -0.262f, 0.022f, 0.022f, 0.022f, FaunaBone.HindFootL, FaunaPalette.CoatLeg);
                c.Tube(d.LimbSegs, d.LimbRings, FaunaPart.Leg, fur, Fv3.Forward, 0.5f, 0.3f);
                Paw(c, -0.072f, -0.25f, FaunaBone.HindFootL);
                c.EndSide(m);
            }

            // Plumed sickle tail curled up over the back, a little to one side, fullest in the middle.
            c.Begin();
            c.Add(0f, 0.46f, -0.3f, 0.028f, 0.028f, 0.028f, FaunaBone.Tail0, coat);
            c.Add(0f, 0.505f, -0.345f, 0.033f, 0.032f, 0.032f, FaunaBone.Tail0, coat);
            c.Add(0.008f, 0.575f, -0.37f, 0.04f, 0.038f, 0.038f, FaunaBone.Tail1, coat);
            c.Add(0.02f, 0.635f, -0.345f, 0.042f, 0.04f, 0.038f, FaunaBone.Tail1, coat);
            c.Add(0.032f, 0.662f, -0.285f, 0.036f, 0.032f, 0.03f, FaunaBone.Tail2, coat);
            c.Add(0.042f, 0.645f, -0.225f, 0.018f, 0.015f, 0.015f, FaunaBone.Tail2, coat);
            c.Tube(d.LimbSegs + 1, d.LimbRings, FaunaPart.Tail, fur, Fv3.Right, 0.5f, 0.8f);

            PaintCoat(b, pattern);
            b.GroundAoHeightM = 0.18f;
            target.EyeHeightM = 0.64f;
            b.Finish();
        }

        /// <summary>An oval paw with two toe bumps on the near level.</summary>
        private static void Paw(FaunaSketch c, float x, float z, FaunaBone bone)
        {
            FaunaDetail d = c.D;
            if (d.Far) return;
            c.Ell(x, 0.022f, z, 0.026f, 0.022f, 0.034f, Fv3.Forward, Fv3.Up, d.Fine ? 4 : 3, d.SmallSegs, bone, FaunaPalette.CoatLeg,
                  FaunaPalette.FurChannel, FaunaPart.Feet);
            if (d.Fine)
            {
                for (int k = -1; k <= 1; k += 2)
                    c.Ell(x + k * 0.012f, 0.014f, z + 0.028f, 0.011f, 0.012f, 0.012f, Fv3.Forward, Fv3.Up, 3, 4, bone, FaunaPalette.CoatLeg,
                          FaunaPalette.FurChannel, FaunaPart.Feet);
            }
        }

        /// <summary>
        /// Coat colours: Plain = one colour with a darker muzzle mask and paler underside (tan dogs show the classic black
        /// mask); Saddle = black back, neck top, ears and tail over the tinted tan; Socks = white feet, chest blaze and
        /// tail tip; Patched = a white dog with tinted patches over the ears, back and rump.
        /// </summary>
        private static void PaintCoat(FaunaBuilder b, CoatPattern pattern)
        {
            FaunaMesh f = b.Target;
            for (int v = 0; v < b.VertexCount; v++)
            {
                uint col = b.ColourAt(v);
                if ((col & 0xFFu) == 0u) continue;
                FaunaPart part = (FaunaPart)f.Part[v];
                Fv3 p = b.PositionOf(v), n = b.NormalOf(v);
                float under = FMath.SmoothStep(-0.1f, -0.7f, n.Y);
                bool chest = (part == FaunaPart.Body || part == FaunaPart.Neck) && p.Z > 0.1f && n.Z > 0.2f && n.Y < 0.2f;
                switch (pattern)
                {
                    case CoatPattern.Plain:
                        col = FMath.LerpColour(col, FaunaPalette.CoatBelly, 0.9f * under);
                        if (part == FaunaPart.Muzzle || part == FaunaPart.Mouth) col = FMath.LerpColour(col, 0x4A4440FFu, 0.75f);
                        if (part == FaunaPart.Ear && n.Z < 0.3f) col = FMath.LerpColour(col, 0x8A847EFFu, 0.5f);
                        break;
                    case CoatPattern.Saddle:
                    {
                        bool black = part == FaunaPart.Ear || (part == FaunaPart.Tail && n.Y > -0.2f) ||
                                     ((part == FaunaPart.Body || part == FaunaPart.Neck) && n.Y > 0.15f && p.Y > 0.45f) ||
                                     (part == FaunaPart.Head && n.Y > 0.5f && p.Z < 0.4f) || (part == FaunaPart.Muzzle && n.Y > 0.6f);
                        if (black) col = FaunaPalette.PatchBlack;
                        else col = FMath.LerpColour(col, FaunaPalette.CoatBelly, 0.5f * under);
                        break;
                    }
                    case CoatPattern.Socks:
                    {
                        bool white = (part == FaunaPart.Leg && p.Y < 0.15f) || part == FaunaPart.Feet || chest || (part == FaunaPart.Tail && p.Z > -0.26f) ||
                                     (part == FaunaPart.Muzzle && Math.Abs(p.X) < 0.012f);
                        if (white) col = FaunaPalette.PatchWhite;
                        else col = FMath.LerpColour(col, FaunaPalette.CoatBelly, 0.6f * under);
                        break;
                    }
                    case CoatPattern.Patched:
                    {
                        float w = FaunaPalette.Patches(p, 9f, 0xD06u);
                        bool patch = w > 0.56f || part == FaunaPart.Ear || (part == FaunaPart.Head && p.X < 0f && n.Y > 0f);
                        if (!patch) col = FaunaPalette.PatchWhite;
                        break;
                    }
                }
                b.SetColour(v, col);
            }
        }
    }
}
