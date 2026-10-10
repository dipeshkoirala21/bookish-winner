using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Generators.Fauna
{
    /// <summary>
    /// Kathmandu street cattle and water buffalo (docs/research/w2/ref_animals.md §1-§2): the humped zebu cow of the
    /// Ring Road (hump over the withers, loose dewlap from chin to brisket, short horns curving up and out, drooping
    /// leaf ears, black-rimmed eyes, dark muzzle, sloping rump, long tail with a dark switch, bony legs and cloven
    /// hooves), its crossbred cousins (smaller hump, pink muzzle, black-and-white patches), the calf (big head, long
    /// legs, horn buds), the zebu ox (tall hump, heavy neck, thick horns) and the water buffalo (broad slate-grey
    /// barrel, low head, swept-back ridged crescent horns, ears under the horns, pale throat chevrons). One recipe per
    /// species at three levels; vertices are skinned to the shared rig (<see cref="FaunaRig"/>).
    /// </summary>
    public static class BovineMesher
    {
        /// <summary>Proportions of a zebu-type animal (cow units: a 1.2 m cow).</summary>
        private struct Zebu
        {
            public float Scale, Hump, Head, HeadLength, Eye, Leg, Horn, HornThick, Dewlap, Neck, Body, Length;
            public bool Udder, Crossbred;
        }

        /// <summary>Builds <paramref name="species"/> (cow, calf, bull or buffalo) at <paramref name="lod"/>.</summary>
        public static void Build(FaunaSpecies species, int lod, CoatPattern pattern, FaunaMesh target)
        {
            var b = new FaunaBuilder(target, species, lod, pattern);
            var c = new FaunaSketch { B = b, D = FaunaDetail.Mammal(lod) };
            if (species == FaunaSpecies.Buffalo)
            {
                BuildBuffalo(c, pattern);
            }
            else
            {
                var z = new Zebu { Scale = 1f, Hump = 0.85f, Head = 1.1f, HeadLength = 0.74f, Eye = 1.05f, Leg = 1f, Length = 1f, Horn = 0.8f, HornThick = 1f, Dewlap = 1.1f, Neck = 1.18f, Body = 1.04f, Udder = true };
                if (species == FaunaSpecies.Calf)
                {
                    z = new Zebu { Scale = 0.62f, Hump = 0.25f, Head = 1.25f, HeadLength = 0.62f, Eye = 1.3f, Leg = 1.18f, Length = 0.86f, Horn = 0.22f, HornThick = 0.8f, Dewlap = 0.35f, Neck = 0.9f, Body = 0.88f };
                }
                else if (species == FaunaSpecies.Bull)
                {
                    z = new Zebu { Scale = 1.08f, Hump = 1.9f, Head = 1.06f, HeadLength = 0.92f, Eye = 0.95f, Leg = 1f, Length = 1.04f, Horn = 1.15f, HornThick = 1.45f, Dewlap = 1.45f, Neck = 1.25f, Body = 1.06f };
                }
                // Crossbred dairy cows (Holstein and Jersey crosses) are the black-and-white ones: low hump, small dewlap,
                // pink muzzle, horns often short.
                z.Crossbred = pattern == CoatPattern.Patched && species != FaunaSpecies.Bull;
                if (z.Crossbred)
                {
                    z.Hump *= 0.35f;
                    z.Dewlap *= 0.45f;
                    z.Horn *= 0.75f;
                }
                BuildZebu(c, z, pattern);
            }
            b.GroundAoHeightM = 0.35f * c.S;
            b.Finish();
        }

        // ------------------------------------------------------------------------------------------------------------
        // Zebu cow, calf and ox

        private static void BuildZebu(FaunaSketch c, Zebu z, CoatPattern pattern)
        {
            FaunaBuilder b = c.B;
            FaunaDetail d = c.D;
            c.S = z.Scale;
            c.Zb = z.Length;
            uint coat = FaunaPalette.Coat;
            float L = z.Leg, H = z.Head, W = z.Body, HL = z.Head * z.HeadLength;
            float e = 2.0f;
            // Legs lift the body: the barrel sits at the cow's leg length.
            float lift = 0.62f * (L - 1f);

            // Pivots (cow units, scaled).
            c.B.Pivot(FaunaBone.Root, Fv3.Zero, false);
            b.Pivot(FaunaBone.Pelvis, c.P(0f, 0.98f + lift, -0.40f), false);
            b.Pivot(FaunaBone.Chest, c.P(0f, 0.98f + lift, 0.30f), false);
            b.Pivot(FaunaBone.Neck, c.P(0f, 1.00f + lift, 0.56f), false);
            b.Pivot(FaunaBone.Head, c.P(0f, 1.12f + lift, 0.98f), false);
            b.Pivot(FaunaBone.Jaw, c.P(0f, 0.97f + lift, 1.05f), false);
            b.Pivot(FaunaBone.EarL, c.P(-0.085f * H, 1.13f + lift, 1.01f), true);
            b.Pivot(FaunaBone.Tail0, c.P(0f, 1.07f + lift, -0.84f), false);
            b.Pivot(FaunaBone.Tail1, c.P(0f, 0.95f + lift, -0.90f), false);
            b.Pivot(FaunaBone.Tail2, c.P(0f, 0.62f + lift * 0.5f, -0.89f), false);
            b.Pivot(FaunaBone.FrontUpperL, c.P(-0.14f, 0.90f + lift, 0.48f), true);
            b.Pivot(FaunaBone.FrontLowerL, c.P(-0.15f, 0.40f * L, 0.51f), true);
            b.Pivot(FaunaBone.FrontFootL, c.P(-0.15f, 0.13f * L, 0.52f), true);
            b.Pivot(FaunaBone.HindUpperL, c.P(-0.16f, 0.92f + lift, -0.52f), true);
            b.Pivot(FaunaBone.HindLowerL, c.P(-0.16f, 0.46f * L, -0.70f), true);
            b.Pivot(FaunaBone.HindFootL, c.P(-0.16f, 0.13f * L, -0.62f), true);

            // Body barrel: sloping rump, deep chest, belly hanging a little; the hump over the withers is part of the
            // same loft (the top radius of the withers knots rises into it), so it grows out of the back line behind
            // and runs down into the crest of the neck in front, as on a real zebu (12-15 cm on a cow, 25-30 cm on
            // an ox, a low rise on the crossbreds).
            float hu = z.Hump;
            float hump = 0.12f * hu;
            c.Begin();
            c.Add(0f, 0.99f + lift, -0.90f, 0.05f * W, 0.05f, 0.07f, FaunaBone.Pelvis, coat, e);
            c.Add(0f, 1.00f + lift, -0.84f, 0.13f * W, 0.11f, 0.16f, FaunaBone.Pelvis, coat, e);
            c.Add(0f, 0.98f + lift, -0.72f, 0.205f * W, 0.15f, 0.24f, FaunaBone.Pelvis, coat, e);
            c.Add(0f, 0.95f + lift, -0.50f, 0.24f * W, 0.18f, 0.28f, FaunaBone.Pelvis, coat, e);
            c.Add(0f, 0.92f + lift, -0.30f, 0.235f * W, 0.17f, 0.31f, FaunaBone.Pelvis, coat, e);
            c.Add(0f, 0.89f + lift, -0.08f, 0.27f * W, 0.21f, 0.36f, FaunaBone.Pelvis, coat, e);
            c.Add(0f, 0.89f + lift, 0.14f, 0.265f * W, 0.21f + 0.06f * hump, 0.34f, FaunaBone.Chest, coat, e);
            c.Add(0f, 0.89f + lift, 0.27f, 0.25f * W, 0.205f + 0.62f * hump, 0.32f, FaunaBone.Chest, coat, e);
            c.Add(0f, 0.895f + lift, 0.37f, 0.235f * W, 0.20f + hump, 0.30f, FaunaBone.Chest, coat, e);
            c.Add(0f, 0.90f + lift, 0.46f, 0.22f * W, 0.195f + 0.72f * hump, 0.30f, FaunaBone.Chest, coat, e);
            c.Add(0f, 0.87f + lift, 0.61f, 0.175f * W, 0.16f + 0.12f * hump, 0.27f, FaunaBone.Chest, coat, e);
            c.Add(0f, 0.86f + lift, 0.70f, 0.09f * W, 0.08f, 0.13f, FaunaBone.Chest, coat, e);
            int vBody = c.Tube(d.BodySegs, d.Rings, FaunaPart.Body, FaunaPalette.FurChannel, Fv3.Up, 0.7f, 0.7f);
            // The hump stays a part of its own for the coat painter (the dark hump of a grey ox) and the AO.
            if (hump > 0.005f)
            {
                for (int v = vBody; v < b.VertexCount; v++)
                {
                    Fv3 q = b.PositionOf(v) * (1f / c.S);
                    if (q.Z > 0.2f && q.Z < 0.56f && q.Y > 1.06f + lift) b.Target.Part[v] = (byte)FaunaPart.Hump;
                }
            }

            // Neck: from inside the shoulders forward and up to the poll, deep at the throat, its crest running down
            // from the hump.
            float nk = z.Neck;
            c.Begin();
            c.Add(0f, 0.93f + lift, 0.30f, 0.16f * nk, 0.16f, 0.22f * nk, FaunaBone.Chest, coat);
            c.Add(0f, 0.97f + lift, 0.52f, 0.125f * nk, 0.12f + 0.045f * hu, 0.20f * nk, FaunaBone.Neck, coat);
            c.Add(0f, 0.99f + lift, 0.71f, 0.115f * nk, 0.12f * nk, 0.17f * nk, FaunaBone.Neck, coat);
            c.Add(0f, 1.02f + lift, 0.86f, 0.10f, 0.11f, 0.14f, FaunaBone.Neck, coat);
            c.Add(0f, 1.04f + lift, 0.97f, 0.085f, 0.10f, 0.12f, FaunaBone.Head, coat);
            c.Tube(d.BodySegs - 1, 1, FaunaPart.Neck, FaunaPalette.FurChannel, Fv3.Up, 0.5f, 0.5f);

            // Dewlap: a loose fold hanging from the throat to the brisket.
            if (z.Dewlap > 0.05f && !d.Far)
            {
                float dw = z.Dewlap;
                c.Begin();
                c.Add(0f, 0.93f + lift, 0.90f, 0.018f, 0.02f, 0.02f, FaunaBone.Neck, coat);
                c.Add(0f, 0.87f + lift, 0.85f, 0.026f, 0.04f, 0.06f * dw, FaunaBone.Neck, coat);
                c.Add(0f, 0.79f + lift, 0.75f, 0.03f, 0.05f, 0.13f * dw, FaunaBone.Neck, coat);
                c.Add(0f, 0.70f + lift, 0.63f, 0.034f, 0.06f, 0.11f * dw, FaunaBone.Chest, coat);
                c.Add(0f, 0.63f + lift, 0.53f, 0.035f, 0.05f, 0.045f, FaunaBone.Chest, coat);
                c.Tube(d.LimbSegs - 1, d.LimbRings, FaunaPart.Dewlap, FaunaPalette.FurChannel, Fv3.Up, 0.5f, 0.6f);
            }

            // Head: poll to muzzle, held forward and down; broad flat forehead, long face, wide dark muzzle.
            uint muzzle = z.Crossbred ? FaunaPalette.MuzzlePink : FaunaPalette.MuzzleDark;
            c.Begin();
            float hy = 1.0f + lift;
            c.Add(0f, hy + 0.17f * H, 0.985f, 0.065f * H, 0.05f * H, 0.06f * H, FaunaBone.Head, coat, 2.4f);
            c.Add(0f, hy + 0.125f * H, 0.98f + 0.04f * HL, 0.112f * H, 0.07f * H, 0.09f * H, FaunaBone.Head, coat, 2.5f);
            c.Add(0f, hy + 0.13f * H - 0.08f * HL, 0.98f + 0.11f * HL, 0.118f * H, 0.075f * H, 0.095f * H, FaunaBone.Head, coat, 2.4f);
            c.Add(0f, hy + 0.13f * H - 0.18f * HL, 0.98f + 0.19f * HL, 0.088f * H, 0.06f * H, 0.08f * H, FaunaBone.Head, coat, 2.3f);
            c.Add(0f, hy + 0.13f * H - 0.27f * HL, 0.98f + 0.26f * HL, 0.072f * H, 0.055f * H, 0.07f * H, FaunaBone.Head, FMath.LerpColour(coat, muzzle, 0.35f), 2.2f);
            c.Add(0f, hy + 0.13f * H - 0.34f * HL, 0.98f + 0.31f * HL, 0.09f * H, 0.062f * H, 0.064f * H, FaunaBone.Head, muzzle, 2.5f);
            c.Add(0f, hy + 0.13f * H - 0.375f * HL, 0.98f + 0.335f * HL, 0.07f * H, 0.045f * H, 0.048f * H, FaunaBone.Head, muzzle, 2.4f);
            int vHead = c.Tube(d.BodySegs, d.Rings, FaunaPart.Head, FaunaPalette.FurChannel, Fv3.Up, 0.5f, 0.45f);
            // Muzzle part (for the painter and the nose channel): everything past the muzzle knot.
            MarkMuzzle(b, vHead, c.P(0f, hy + 0.13f * H - 0.30f * HL, 0.98f + 0.29f * HL), new Fv3(0f, -0.57f, 0.80f).Normalized, FaunaPart.Muzzle);

            // Lower jaw and chin (chews the cud).
            if (!d.Far)
            {
            c.Begin();
            c.Add(0f, hy + 0.13f * H - 0.16f * HL, 0.98f + 0.10f * HL, 0.06f * H, 0.04f * H, 0.045f * H, FaunaBone.Jaw, coat);
            c.Add(0f, hy + 0.13f * H - 0.28f * HL, 0.98f + 0.21f * HL, 0.055f * H, 0.035f * H, 0.035f * H, FaunaBone.Jaw, coat);
            c.Add(0f, hy + 0.13f * H - 0.365f * HL, 0.98f + 0.285f * HL, 0.05f * H, 0.03f * H, 0.03f * H, FaunaBone.Jaw, muzzle);
            c.Tube(d.LimbSegs, d.LimbRings, FaunaPart.Mouth, FaunaPalette.FurChannel, Fv3.Up, 0.5f, 0.6f);
            }

            // Nostrils.
            Fv3 snout = new Fv3(0f, -0.62f, 0.78f).Normalized;
            if (d.Fine)
            {
                int v0 = b.VertexCount, i0 = b.IndexCount, o0 = b.OccluderCount;
                b.Ellipsoid(c.P(-0.034f * H, hy + 0.13f * H - 0.355f * HL, 0.98f + 0.343f * HL), snout, Fv3.Up, 0.017f * H * c.S, 0.012f * H * c.S, 0.012f * H * c.S, 2,
                            d.SmallSegs, FaunaBone.Head, FaunaPalette.Nose, FaunaPalette.NoseChannel, FaunaPart.Nose);
                b.Mirror(v0, i0, o0);
            }

            // Eyes on the sides of the face, black-rimmed on the zebu, a calm half-closed lid.
            {
                int v0 = b.VertexCount, i0 = b.IndexCount, o0 = b.OccluderCount;
                Fv3 ec = c.P(-0.100f * H, hy + 0.13f * H - 0.065f * HL, 0.98f + 0.10f * HL);
                Fv3 eo = new Fv3(-1f, 0.12f, 0.45f).Normalized;
                float er = 0.029f * H * z.Eye * c.S;
                if (d.Eyes >= 2)
                {
                    b.Ellipsoid(ec - eo * (er * 0.3f), eo, Fv3.Up, er * 1.4f, er * 1.22f, er * 0.5f, 3, d.SmallSegs, FaunaBone.Head,
                                z.Crossbred ? FaunaPalette.Coat : FaunaPalette.PatchBlack, FaunaPalette.SkinChannel, FaunaPart.Face);
                }
                b.Eye(ec, er, eo, Fv3.Up, FaunaBone.Head, FaunaPalette.EyeDark, FaunaPalette.EyeBlack, d.Eyes);
                if (d.Eyes >= 2)
                {
                    // Upper lid: a coat-coloured shell over the top of the eyeball (the calm, sleepy look).
                    b.Ellipsoid(ec + Fv3.Up * (er * 0.42f) + eo * (er * 0.05f), eo, new Fv3(0f, 1f, -0.25f), er * 1.12f, er * 0.6f, er * 0.98f, 3,
                                d.SmallSegs, FaunaBone.Head, coat, FaunaPalette.FurChannel, FaunaPart.Face);
                }
                // Ear: a drooping leaf hanging out and down under the horn, pale pink inside.
                c.Begin();
                float ex = -0.085f * H, ey = hy + 0.11f * H, ez = 0.98f + 0.035f * HL;
                c.Add(ex, ey, ez, 0.018f * H, 0.013f, 0.013f, FaunaBone.EarL, coat);
                c.Add(ex - 0.045f * H, ey - 0.018f * H, ez + 0.006f, 0.034f * H, 0.011f, 0.01f, FaunaBone.EarL, coat);
                c.Add(ex - 0.095f * H, ey - 0.04f * H, ez + 0.012f, 0.036f * H, 0.01f, 0.008f, FaunaBone.EarL, coat);
                c.Add(ex - 0.14f * H, ey - 0.065f * H, ez + 0.018f, 0.017f * H, 0.007f, 0.006f, FaunaBone.EarL, coat);
                int vEar = c.Tube(d.Far ? 3 : d.LimbSegs - 1, d.LimbRings, FaunaPart.Ear, FaunaPalette.FurChannel, new Fv3(0f, 0.3f, 1f), 0.4f, 0.7f);
                PaintEarInside(b, vEar, new Fv3(0.1f, 0.2f, 1f).Normalized, FaunaPalette.EarInner);

                // Horn: short, rising up and out, the tips curving forward and in (the lyre of the hill zebu; buds on
                // the calf).
                if (z.Horn > 0.05f)
                {
                    float hs = z.Horn, ht = z.HornThick;
                    float bx = -0.065f * H, by = hy + 0.165f * H, bz = 0.98f + 0.03f * HL;
                    float[] hp =
                    {
                        bx + 0.012f, by - 0.014f, bz, bx - 0.03f * hs, by + 0.025f * hs, bz - 0.006f * hs, bx - 0.052f * hs, by + 0.07f * hs, bz,
                        bx - 0.048f * hs, by + 0.112f * hs, bz + 0.025f * hs, bx - 0.032f * hs, by + 0.138f * hs, bz + 0.048f * hs,
                    };
                    FaunaShapes.Horn(c, hp, 5, 0.026f * ht, 0.0035f * ht, FaunaPalette.HornBase, FaunaPalette.HornTip, FaunaBone.Head, 300f);
                }
                b.Mirror(v0, i0, o0);
            }

            // Poll: the bony ridge between the horns with a tuft of hair.
            if (d.Fine)
            {
                b.Ellipsoid(c.P(0f, hy + 0.16f * H, 0.98f + 0.03f * HL), Fv3.Right, new Fv3(0f, 1f, 0.3f), 0.035f * H * c.S, 0.03f * H * c.S, 0.065f * H * c.S, 3,
                            d.SmallSegs + 1, FaunaBone.Head, coat, FaunaPalette.FurChannel, FaunaPart.Head);
            }

            // Legs (left, then mirrored): the tops hide in the body; bony, with knee and hock knobs, fetlocks and
            // cloven hooves.
            {
                int v0 = b.VertexCount, i0 = b.IndexCount, o0 = b.OccluderCount;
                c.Begin();
                c.Add(-0.11f, 0.86f + lift, 0.47f, 0.07f, 0.085f, 0.085f, FaunaBone.FrontUpperL, coat);
                c.Add(-0.14f, 0.64f * L + lift * 0.4f, 0.48f, 0.074f, 0.08f, 0.08f, FaunaBone.FrontUpperL, coat);
                c.Add(-0.15f, 0.49f * L, 0.50f, 0.05f, 0.054f, 0.054f, FaunaBone.FrontUpperL, coat);
                c.Add(-0.15f, 0.40f * L, 0.51f, 0.047f, 0.054f, 0.05f, FaunaBone.FrontLowerL, FaunaPalette.CoatLeg);
                c.Add(-0.15f, 0.26f * L, 0.515f, 0.036f, 0.04f, 0.04f, FaunaBone.FrontLowerL, FaunaPalette.CoatLeg);
                c.Add(-0.15f, 0.13f * L, 0.52f, 0.035f, 0.04f, 0.042f, FaunaBone.FrontFootL, FaunaPalette.CoatLeg);
                c.Add(-0.15f, 0.075f, 0.53f, 0.032f, 0.035f, 0.035f, FaunaBone.FrontFootL, FaunaPalette.CoatLeg);
                c.Tube(d.LimbSegs, d.LimbRings, FaunaPart.Leg, FaunaPalette.FurChannel, Fv3.Forward, 0.5f, 0.4f);
                if (!d.Far) FaunaShapes.Hoof(c, -0.15f, 0.53f, 0.036f, 0.085f, FaunaBone.FrontFootL, FaunaPalette.Hoof);

                c.Begin();
                c.Add(-0.12f, 0.92f + lift, -0.50f, 0.095f, 0.11f, 0.11f, FaunaBone.HindUpperL, coat);
                c.Add(-0.165f, 0.70f * L + lift * 0.5f, -0.56f, 0.08f, 0.10f, 0.09f, FaunaBone.HindUpperL, coat);
                c.Add(-0.165f, 0.56f * L, -0.655f, 0.055f, 0.064f, 0.058f, FaunaBone.HindUpperL, coat);
                c.Add(-0.16f, 0.46f * L, -0.70f, 0.041f, 0.05f, 0.05f, FaunaBone.HindLowerL, FaunaPalette.CoatLeg);
                c.Add(-0.16f, 0.30f * L, -0.665f, 0.036f, 0.04f, 0.04f, FaunaBone.HindLowerL, FaunaPalette.CoatLeg);
                c.Add(-0.16f, 0.13f * L, -0.625f, 0.035f, 0.04f, 0.042f, FaunaBone.HindFootL, FaunaPalette.CoatLeg);
                c.Add(-0.16f, 0.075f, -0.61f, 0.032f, 0.035f, 0.035f, FaunaBone.HindFootL, FaunaPalette.CoatLeg);
                c.Tube(d.LimbSegs, d.LimbRings, FaunaPart.Leg, FaunaPalette.FurChannel, Fv3.Forward, 0.5f, 0.4f);
                if (!d.Far) FaunaShapes.Hoof(c, -0.16f, -0.61f, 0.036f, 0.085f, FaunaBone.HindFootL, FaunaPalette.Hoof);
                if (d.Fine)
                {
                    // Knee (carpus) and the point of the hock.
                    b.Ellipsoid(c.P(-0.15f, 0.40f * L, 0.525f), Fv3.Forward, Fv3.Up, 0.045f * c.S, 0.04f * c.S, 0.03f * c.S, 3, d.SmallSegs - 1,
                                FaunaBone.FrontLowerL, FaunaPalette.CoatLeg, FaunaPalette.FurChannel, FaunaPart.Leg);
                    b.Ellipsoid(c.P(-0.16f, 0.475f * L, -0.73f), Fv3.Forward, Fv3.Up, 0.026f * c.S, 0.034f * c.S, 0.024f * c.S, 3, d.SmallSegs - 1,
                                FaunaBone.HindLowerL, FaunaPalette.CoatLeg, FaunaPalette.FurChannel, FaunaPart.Leg);
                }
                b.Mirror(v0, i0, o0);
            }

            // Udder with four teats (cows only).
            if (z.Udder && !d.Far)
            {
                b.Ellipsoid(c.P(0f, 0.57f + lift, -0.38f), Fv3.Forward, Fv3.Up, 0.085f * c.S, 0.07f * c.S, 0.11f * c.S, 3, d.SmallSegs + 1,
                            FaunaBone.Pelvis, FaunaPalette.Udder, FaunaPalette.SkinChannel, FaunaPart.Udder);
                if (d.Fine)
                {
                    int v0 = b.VertexCount, i0 = b.IndexCount, o0 = b.OccluderCount;
                    for (int t = 0; t < 2; t++)
                    {
                        c.Begin();
                        float tz = t == 0 ? -0.33f : -0.44f;
                        c.Add(-0.04f, 0.53f + lift, tz, 0.012f, FaunaBone.Pelvis, FaunaPalette.Udder);
                        c.Add(-0.045f, 0.47f + lift, tz, 0.008f, FaunaBone.Pelvis, FaunaPalette.Udder);
                        c.Tube(3, 1, FaunaPart.Udder, FaunaPalette.SkinChannel, Fv3.Forward, -1f, 0.6f);
                    }
                    b.Mirror(v0, i0, o0);
                }
            }

            // Tail: from the tailhead down past the hocks, with a dark switch.
            c.Begin();
            c.Add(0f, 1.08f + lift, -0.80f, 0.04f, 0.035f, 0.035f, FaunaBone.Tail0, coat);
            c.Add(0f, 1.055f + lift, -0.885f, 0.028f, 0.026f, 0.026f, FaunaBone.Tail0, coat);
            c.Add(0f, 0.93f + lift, -0.905f, 0.02f, 0.02f, 0.02f, FaunaBone.Tail1, coat);
            c.Add(0f, 0.72f + lift * 0.6f, -0.895f, 0.016f, 0.016f, 0.016f, FaunaBone.Tail1, coat);
            c.Add(0f, 0.52f * L, -0.885f, 0.015f, 0.015f, 0.015f, FaunaBone.Tail2, coat);
            c.Add(0f, 0.43f * L, -0.88f, 0.018f, 0.018f, 0.018f, FaunaBone.Tail2, FaunaPalette.Switch);
            c.Tube(Math.Max(3, d.LimbSegs - 3), d.LimbRings, FaunaPart.Tail, FaunaPalette.FurChannel, Fv3.Forward, 0.5f, d.Far ? 0.8f : 0.4f);
            if (!d.Far)
                b.Ellipsoid(c.P(0f, 0.34f * L, -0.878f), Fv3.Forward, Fv3.Up, 0.04f * c.S, 0.11f * c.S, 0.038f * c.S, d.Fine ? 4 : 3, d.SmallSegs,
                            FaunaBone.Tail2, FaunaPalette.Switch, FaunaPalette.FurChannel, FaunaPart.Tail, 1.8f);

            PaintZebuCoat(b, pattern, z, c.S, lift, vBody);
            b.Target.EyeHeightM = (1.07f + lift) * c.S;
        }

        /// <summary>Marks the vertices of a head tube in front of <paramref name="plane"/> (along <paramref name="dir"/>) as
        /// <paramref name="part"/>.</summary>
        internal static void MarkMuzzle(FaunaBuilder b, int v0, Fv3 plane, Fv3 dir, FaunaPart part)
        {
            for (int v = v0; v < b.VertexCount; v++)
            {
                if (Fv3.Dot(b.PositionOf(v) - plane, dir) > 0f) b.Target.Part[v] = (byte)part;
            }
        }

        /// <summary>Paints the inside of an ear (vertices facing <paramref name="inward"/>) with <paramref name="colour"/>,
        /// leaving a coat-coloured rim.</summary>
        internal static void PaintEarInside(FaunaBuilder b, int v0, Fv3 inward, uint colour)
        {
            for (int v = v0; v < b.VertexCount; v++)
            {
                float f = Fv3.Dot(b.NormalOf(v), inward);
                if (f > 0.45f) b.SetColour(v, FMath.LerpColour(b.ColourAt(v), colour, FMath.SmoothStep(0.45f, 0.8f, f)));
            }
        }

        /// <summary>
        /// Coat painting of the zebu family: a paler belly and inner legs, a darker top line on the ox's neck and hump
        /// (Saddle), the irregular white map of the crossbreds (Patched), a white face and socks (Socks).
        /// </summary>
        private static void PaintZebuCoat(FaunaBuilder b, CoatPattern pattern, Zebu z, float s, float lift, int vBody)
        {
            FaunaMesh f = b.Target;
            uint seed = (uint)f.Species * 7919u + (uint)pattern;
            for (int v = 0; v < b.VertexCount; v++)
            {
                uint col = b.ColourAt(v);
                if ((col & 0xFFu) == 0u) continue; // fixed colours stay
                FaunaPart part = (FaunaPart)f.Part[v];
                Fv3 p = b.PositionOf(v) * (1f / s);
                Fv3 n = b.NormalOf(v);
                // Shading of the coat itself: paler underneath, slightly darker along the spine.
                float under = FMath.SmoothStep(-0.2f, -0.8f, n.Y);
                col = FMath.LerpColour(col, FaunaPalette.CoatBelly, 0.8f * under);
                switch (pattern)
                {
                    case CoatPattern.Saddle:
                    {
                        // Grey to near-black neck, hump and shoulders of a mature zebu ox or a grey cow.
                        float fore = FMath.SmoothStep(0.05f, 0.45f, p.Z) * FMath.SmoothStep(0.75f + lift, 1.05f + lift, p.Y);
                        if (part == FaunaPart.Hump || part == FaunaPart.Neck) fore = Math.Max(fore, 0.75f);
                        if (part == FaunaPart.Head || part == FaunaPart.Muzzle || part == FaunaPart.Mouth) fore = 0.35f;
                        col = FMath.LerpColour(col, 0x5E5C5AFFu, 0.85f * fore);
                        break;
                    }
                    case CoatPattern.Patched:
                    {
                        float w = FaunaPalette.Patches(p, 2.3f, seed);
                        bool white = w > 0.52f;
                        if (part == FaunaPart.Leg && p.Y < 0.42f) white = true; // white socks
                        if ((part == FaunaPart.Head || part == FaunaPart.Muzzle) && Math.Abs(p.X) < 0.045f) white = true; // blaze
                        if (part == FaunaPart.Udder) white = true;
                        if (white) col = FaunaPalette.PatchWhite;
                        break;
                    }
                    case CoatPattern.Socks:
                    {
                        bool white = part == FaunaPart.Leg && p.Y < 0.36f;
                        if (part == FaunaPart.Head && Math.Abs(p.X) < 0.06f && n.Z > 0f) white = true;
                        if (part == FaunaPart.Tail && p.Y < 0.5f) white = true;
                        if (white) col = FaunaPalette.PatchWhite;
                        break;
                    }
                }
                // Knees and fetlocks pick up road dust.
                if (part == FaunaPart.Leg && p.Y < 0.2f && (col & 0xFFu) != 0u) col = FMath.LerpColour(col, 0xC9BBA6FFu, 0.25f);
                b.SetColour(v, col);
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // Water buffalo

        private static void BuildBuffalo(FaunaSketch c, CoatPattern pattern)
        {
            FaunaBuilder b = c.B;
            FaunaDetail d = c.D;
            c.S = 1f;
            uint coat = FaunaPalette.Coat;
            b.Pivot(FaunaBone.Root, Fv3.Zero, false);
            b.Pivot(FaunaBone.Pelvis, c.P(0f, 0.98f, -0.45f), false);
            b.Pivot(FaunaBone.Chest, c.P(0f, 0.98f, 0.35f), false);
            b.Pivot(FaunaBone.Neck, c.P(0f, 0.98f, 0.62f), false);
            b.Pivot(FaunaBone.Head, c.P(0f, 0.98f, 0.98f), false);
            b.Pivot(FaunaBone.Jaw, c.P(0f, 0.86f, 1.08f), false);
            b.Pivot(FaunaBone.EarL, c.P(-0.13f, 1.0f, 1.02f), true);
            b.Pivot(FaunaBone.Tail0, c.P(0f, 1.10f, -0.95f), false);
            b.Pivot(FaunaBone.Tail1, c.P(0f, 0.95f, -1.0f), false);
            b.Pivot(FaunaBone.Tail2, c.P(0f, 0.62f, -1.0f), false);
            b.Pivot(FaunaBone.FrontUpperL, c.P(-0.19f, 0.86f, 0.52f), true);
            b.Pivot(FaunaBone.FrontLowerL, c.P(-0.2f, 0.36f, 0.55f), true);
            b.Pivot(FaunaBone.FrontFootL, c.P(-0.2f, 0.12f, 0.56f), true);
            b.Pivot(FaunaBone.HindUpperL, c.P(-0.2f, 0.92f, -0.6f), true);
            b.Pivot(FaunaBone.HindLowerL, c.P(-0.2f, 0.42f, -0.8f), true);
            b.Pivot(FaunaBone.HindFootL, c.P(-0.2f, 0.12f, -0.72f), true);

            // A broad, deep barrel with a straight back and a slight rise over the withers.
            c.Begin();
            float e = 2.4f;
            c.Add(0f, 1.02f, -0.98f, 0.12f, 0.1f, 0.14f, FaunaBone.Pelvis, coat, e);
            c.Add(0f, 0.99f, -0.84f, 0.27f, 0.16f, 0.28f, FaunaBone.Pelvis, coat, e);
            c.Add(0f, 0.93f, -0.52f, 0.33f, 0.21f, 0.36f, FaunaBone.Pelvis, coat, e);
            c.Add(0f, 0.9f, -0.15f, 0.35f, 0.23f, 0.38f, FaunaBone.Pelvis, coat, e);
            c.Add(0f, 0.92f, 0.2f, 0.33f, 0.25f, 0.35f, FaunaBone.Chest, coat, e);
            c.Add(0f, 0.94f, 0.48f, 0.28f, 0.25f, 0.32f, FaunaBone.Chest, coat, e);
            c.Add(0f, 0.9f, 0.68f, 0.2f, 0.18f, 0.27f, FaunaBone.Chest, coat, e);
            c.Add(0f, 0.88f, 0.77f, 0.11f, 0.1f, 0.15f, FaunaBone.Chest, coat, e);
            c.Tube(d.BodySegs, d.Rings, FaunaPart.Body, FaunaPalette.FurChannel, Fv3.Up, 0.7f, 0.7f);

            // Short thick neck carrying the head low and level.
            c.Begin();
            c.Add(0f, 0.97f, 0.45f, 0.2f, 0.2f, 0.25f, FaunaBone.Chest, coat);
            c.Add(0f, 0.97f, 0.7f, 0.16f, 0.16f, 0.2f, FaunaBone.Neck, coat);
            c.Add(0f, 0.97f, 0.9f, 0.13f, 0.13f, 0.15f, FaunaBone.Neck, coat);
            c.Add(0f, 0.97f, 1.0f, 0.11f, 0.11f, 0.12f, FaunaBone.Head, coat);
            c.Tube(d.BodySegs - 2, d.Rings, FaunaPart.Neck, FaunaPalette.FurChannel, Fv3.Up, 0.5f, 0.5f);

            // Head: long, held almost horizontal, nose a little up; wide wet muzzle.
            c.Begin();
            c.Add(0f, 1.06f, 0.97f, 0.1f, 0.06f, 0.08f, FaunaBone.Head, coat, 2.4f);
            c.Add(0f, 1.03f, 1.05f, 0.13f, 0.08f, 0.1f, FaunaBone.Head, coat, 2.4f);
            c.Add(0f, 0.96f, 1.16f, 0.12f, 0.08f, 0.1f, FaunaBone.Head, coat, 2.3f);
            c.Add(0f, 0.88f, 1.28f, 0.09f, 0.065f, 0.085f, FaunaBone.Head, coat, 2.2f);
            c.Add(0f, 0.82f, 1.37f, 0.095f, 0.065f, 0.07f, FaunaBone.Head, FaunaPalette.Nose, 2.4f);
            c.Add(0f, 0.8f, 1.41f, 0.08f, 0.05f, 0.05f, FaunaBone.Head, FaunaPalette.Nose, 2.4f);
            int vHead = c.Tube(d.BodySegs, d.Rings, FaunaPart.Head, FaunaPalette.FurChannel, Fv3.Up, 0.5f, 0.45f);
            MarkMuzzle(b, vHead, c.P(0f, 0.84f, 1.33f), new Fv3(0f, -0.5f, 0.86f), FaunaPart.Muzzle);
            if (!d.Far)
            {
                c.Begin();
                c.Add(0f, 0.9f, 1.1f, 0.07f, 0.045f, 0.05f, FaunaBone.Jaw, coat);
                c.Add(0f, 0.82f, 1.25f, 0.065f, 0.04f, 0.04f, FaunaBone.Jaw, coat);
                c.Add(0f, 0.77f, 1.36f, 0.06f, 0.03f, 0.03f, FaunaBone.Jaw, FaunaPalette.Nose);
                c.Tube(d.LimbSegs, d.LimbRings, FaunaPart.Mouth, FaunaPalette.FurChannel, Fv3.Up, 0.5f, 0.6f);
            }
            {
                int v0 = b.VertexCount, i0 = b.IndexCount, o0 = b.OccluderCount;
                Fv3 ec = c.P(-0.115f, 1.03f, 1.12f);
                Fv3 eo = new Fv3(-1f, 0.15f, 0.35f).Normalized;
                b.Eye(ec, 0.022f, eo, Fv3.Up, FaunaBone.Head, FaunaPalette.EyeDark, FaunaPalette.EyeBlack, d.Eyes);
                if (d.Eyes >= 2)
                    b.Ellipsoid(ec + Fv3.Up * 0.009f, eo, new Fv3(0f, 1f, -0.25f), 0.024f, 0.014f, 0.021f, 3, d.SmallSegs + 1, FaunaBone.Head, coat,
                                FaunaPalette.FurChannel, FaunaPart.Face);
                if (!d.Far)
                    b.Ellipsoid(c.P(-0.04f, 0.805f, 1.415f), new Fv3(0f, -0.3f, 1f).Normalized, Fv3.Up, 0.02f, 0.012f, 0.012f, 3, d.SmallSegs,
                                FaunaBone.Head, 0x0A0A0A00u, FaunaPalette.NoseChannel, FaunaPart.Nose);
                // Ears stick out sideways below the horns.
                c.Begin();
                c.Add(-0.11f, 1.0f, 1.02f, 0.025f, 0.015f, 0.015f, FaunaBone.EarL, coat);
                c.Add(-0.17f, 0.985f, 1.03f, 0.045f, 0.013f, 0.011f, FaunaBone.EarL, coat);
                c.Add(-0.24f, 0.97f, 1.045f, 0.045f, 0.011f, 0.01f, FaunaBone.EarL, coat);
                c.Add(-0.29f, 0.955f, 1.06f, 0.022f, 0.008f, 0.008f, FaunaBone.EarL, coat);
                int vEar = c.Tube(d.Far ? 3 : d.LimbSegs, d.LimbRings, FaunaPart.Ear, FaunaPalette.FurChannel, Fv3.Forward, 0.4f, 0.7f);
                PaintEarInside(b, vEar, Fv3.Forward, 0x8E7A7000u);
                // Horn: flat, ridged, sweeping out and back from the top of the head, tips curling up and in.
                c.Begin();
                c.Add(-0.06f, 1.09f, 1.0f, 0.04f, 0.035f, 0.035f, FaunaBone.Head, FaunaPalette.BuffaloHorn, 2.6f);
                c.Add(-0.16f, 1.11f, 0.97f, 0.04f, 0.032f, 0.03f, FaunaBone.Head, FaunaPalette.BuffaloHorn, 2.6f);
                c.Add(-0.27f, 1.12f, 0.9f, 0.034f, 0.026f, 0.024f, FaunaBone.Head, FaunaPalette.BuffaloHorn, 2.4f);
                c.Add(-0.33f, 1.15f, 0.8f, 0.026f, 0.02f, 0.02f, FaunaBone.Head, FaunaPalette.BuffaloHorn, 2.2f);
                c.Add(-0.32f, 1.2f, 0.71f, 0.016f, 0.014f, 0.014f, FaunaBone.Head, 0x3A363300u);
                c.Add(-0.27f, 1.24f, 0.66f, 0.006f, 0.006f, 0.006f, FaunaBone.Head, 0x2E2B2900u);
                int vHorn = c.Tube(d.LimbSegs, d.LimbRings + (d.Fine ? 1 : 0), FaunaPart.Horn, FaunaPalette.KeratinChannel, Fv3.Up, 0.3f, 0.5f);
                if (d.Fine) RidgeHorn(b, vHorn);
                b.Mirror(v0, i0, o0);
            }

            // Legs: short and stout with big splayed hooves.
            {
                int v0 = b.VertexCount, i0 = b.IndexCount, o0 = b.OccluderCount;
                c.Begin();
                c.Add(-0.19f, 0.86f, 0.52f, 0.11f, 0.12f, 0.12f, FaunaBone.FrontUpperL, coat);
                c.Add(-0.2f, 0.58f, 0.53f, 0.085f, 0.095f, 0.095f, FaunaBone.FrontUpperL, coat);
                c.Add(-0.2f, 0.42f, 0.545f, 0.06f, 0.065f, 0.065f, FaunaBone.FrontUpperL, coat);
                c.Add(-0.2f, 0.35f, 0.55f, 0.055f, 0.06f, 0.058f, FaunaBone.FrontLowerL, coat);
                c.Add(-0.2f, 0.22f, 0.555f, 0.042f, 0.046f, 0.046f, FaunaBone.FrontLowerL, coat);
                c.Add(-0.2f, 0.12f, 0.56f, 0.045f, 0.05f, 0.05f, FaunaBone.FrontFootL, coat);
                c.Add(-0.2f, 0.07f, 0.57f, 0.043f, 0.046f, 0.046f, FaunaBone.FrontFootL, coat);
                c.Tube(d.LimbSegs, d.LimbRings, FaunaPart.Leg, FaunaPalette.FurChannel, Fv3.Forward, 0.5f, 0.4f);
                if (!d.Far) FaunaShapes.Hoof(c, -0.2f, 0.57f, 0.05f, 0.085f, FaunaBone.FrontFootL, FaunaPalette.Hoof);
                c.Begin();
                c.Add(-0.2f, 0.94f, -0.58f, 0.13f, 0.14f, 0.14f, FaunaBone.HindUpperL, coat);
                c.Add(-0.22f, 0.7f, -0.64f, 0.1f, 0.11f, 0.1f, FaunaBone.HindUpperL, coat);
                c.Add(-0.21f, 0.52f, -0.76f, 0.068f, 0.07f, 0.068f, FaunaBone.HindUpperL, coat);
                c.Add(-0.2f, 0.42f, -0.8f, 0.052f, 0.058f, 0.058f, FaunaBone.HindLowerL, coat);
                c.Add(-0.2f, 0.27f, -0.76f, 0.042f, 0.046f, 0.046f, FaunaBone.HindLowerL, coat);
                c.Add(-0.2f, 0.12f, -0.72f, 0.045f, 0.05f, 0.05f, FaunaBone.HindFootL, coat);
                c.Add(-0.2f, 0.07f, -0.705f, 0.043f, 0.046f, 0.046f, FaunaBone.HindFootL, coat);
                c.Tube(d.LimbSegs, d.LimbRings, FaunaPart.Leg, FaunaPalette.FurChannel, Fv3.Forward, 0.5f, 0.4f);
                if (!d.Far) FaunaShapes.Hoof(c, -0.2f, -0.70f, 0.05f, 0.085f, FaunaBone.HindFootL, FaunaPalette.Hoof);
                b.Mirror(v0, i0, o0);
            }

            // Tail to the hocks with a small dark tuft.
            c.Begin();
            c.Add(0f, 1.1f, -0.93f, 0.045f, 0.04f, 0.04f, FaunaBone.Tail0, coat);
            c.Add(0f, 1.06f, -1.0f, 0.03f, 0.028f, 0.028f, FaunaBone.Tail0, coat);
            c.Add(0f, 0.9f, -1.02f, 0.022f, 0.022f, 0.022f, FaunaBone.Tail1, coat);
            c.Add(0f, 0.65f, -1.01f, 0.017f, 0.017f, 0.017f, FaunaBone.Tail2, coat);
            c.Add(0f, 0.48f, -1.0f, 0.016f, 0.016f, 0.016f, FaunaBone.Tail2, FaunaPalette.Switch);
            c.Tube(d.LimbSegs - 2, d.LimbRings + (d.Far ? 0 : 1), FaunaPart.Tail, FaunaPalette.FurChannel, Fv3.Forward, 0.5f, 0.4f);
            b.Ellipsoid(c.P(0f, 0.42f, -1.0f), Fv3.Forward, Fv3.Up, 0.03f, 0.075f, 0.03f, 3, d.SmallSegs, FaunaBone.Tail2, FaunaPalette.Switch,
                        FaunaPalette.FurChannel, FaunaPart.Tail);

            // Rope collar and brass bell of a village buffalo.
            if (!d.Coarse) FaunaShapes.CollarBell(c, new Fv3(0f, 0.95f, 0.86f), 0.145f, 0f, 0.05f, FaunaBone.Neck);

            // Pale chevrons on the throat and brisket, a paler belly and (Socks) the pale lower legs of many buffalo.
            for (int v = 0; v < b.VertexCount; v++)
            {
                uint col = b.ColourAt(v);
                if ((col & 0xFFu) == 0u) continue;
                Fv3 p = b.PositionOf(v), n = b.NormalOf(v);
                FaunaPart part = (FaunaPart)b.Target.Part[v];
                if (part == FaunaPart.Neck && n.Y < -0.35f)
                {
                    float u = p.Z;
                    if (Math.Abs(u - 0.86f) < 0.025f || Math.Abs(u - 0.66f) < 0.03f) col = 0xB9B1A600u;
                }
                if (pattern == CoatPattern.Socks && part == FaunaPart.Leg && p.Y < 0.3f) col = FMath.LerpColour(0xA79F9300u, col, FMath.SmoothStep(0.2f, 0.3f, p.Y));
                if (n.Y < -0.5f && part == FaunaPart.Body) col = FMath.LerpColour(col, 0xBDB7B0FFu, 0.4f);
                b.SetColour(v, col);
            }
            b.Target.EyeHeightM = 1.03f;
        }

        /// <summary>Cross ridges on a buffalo horn: alternating light and dark bands along it.</summary>
        private static void RidgeHorn(FaunaBuilder b, int v0)
        {
            Fv3 root = b.PositionOf(v0);
            for (int v = v0; v < b.VertexCount; v++)
            {
                float t = Fv3.Distance(b.PositionOf(v), root);
                float band = 0.5f + 0.5f * FMath.Sin(t * 120f);
                b.SetColour(v, FMath.LerpColour(b.ColourAt(v), 0x6E686200u, 0.35f * band * FMath.SmoothStep(0.25f, 0.05f, t)));
            }
        }
    }
}
