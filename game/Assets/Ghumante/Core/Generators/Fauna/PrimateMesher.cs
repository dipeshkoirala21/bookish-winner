using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Generators.Fauna
{
    /// <summary>
    /// The rhesus macaque of Swayambhu and Pashupati (docs/research/w2/ref_animals.md §5): a stocky, thick-furred
    /// monkey, bind pose on all fours (top of the back about 42 cm), grey-brown head, shoulders and arms turning
    /// golden-orange over the lower back, the rump and the thighs, a paler belly, a bare pink face framed by a pale
    /// ruff of fur on the cheeks and crown, close-set amber eyes under a heavy brow, a short muzzle, small rounded ears
    /// close to the head, dark grey-pink hands and feet with fingers, and a medium tail hanging in a curve. Every limb
    /// is two rigid tubes with a rounded elbow or knee ball at the joint pivot, so a sitting monkey's folded knees and
    /// bent elbows stay round (no folded, sawn-off joints). The baby is half the size with a big head, darker fur and
    /// a pinker face; it rides its mother's back (the <see cref="FaunaBone.Rider"/> pivot) or sits close by. Sitting,
    /// climbing and grooming are poses (<see cref="FaunaAnimator"/>).
    /// </summary>
    public static class PrimateMesher
    {
        /// <summary>Pale buff-grey of the cheek and crown ruff round the face (fixed colour).</summary>
        public const uint Ruff = 0xB3A48E00u;

        /// <summary>Builds the macaque or its baby at <paramref name="lod"/>.</summary>
        public static void Build(FaunaSpecies species, int lod, FaunaMesh target)
        {
            var b = new FaunaBuilder(target, species, lod, CoatPattern.Plain);
            bool baby = species == FaunaSpecies.MacaqueBaby;
            var c = new FaunaSketch { B = b, D = FaunaDetail.Mammal(lod), S = baby ? 0.5f : 1f };
            FaunaDetail d = c.D;
            uint fur = baby ? 0x8A8078FFu : FaunaPalette.Coat;
            uint rump = baby ? FaunaPalette.Coat : FaunaPalette.MacaqueRump;
            uint mid = FMath.LerpColour(rump, fur, 0.3f);
            uint belly = FaunaPalette.CoatBelly;
            uint face = baby ? 0xE9B3A400u : 0xD8907E00u;
            uint ruff = FMath.LerpColour(fur, baby ? FMath.LerpColour(Ruff, 0x8A807800u, 0.4f) : Ruff, 0.6f);
            uint hand = 0x6E5A5000u;
            MaterialChannel hair = FaunaPalette.FurChannel;
            float hs = baby ? 1.7f : 1.16f; // head scale (big-headed cartoon; the baby's bigger still)

            // Joints: shoulder, elbow, wrist; hip, knee, ankle (the limb tubes are rigid between them).
            var shoulder = new Fv3(-0.072f, 0.375f, 0.135f);
            var elbow = new Fv3(-0.082f, 0.205f, 0.115f);
            var wrist = new Fv3(-0.084f, 0.036f, 0.14f);
            var hip = new Fv3(-0.064f, 0.355f, -0.155f);
            var knee = new Fv3(-0.082f, 0.22f, -0.075f);
            var ankle = new Fv3(-0.08f, 0.04f, -0.15f);

            b.Pivot(FaunaBone.Root, Fv3.Zero, false);
            b.Pivot(FaunaBone.Pelvis, c.P(0f, 0.36f, -0.13f), false);
            b.Pivot(FaunaBone.Chest, c.P(0f, 0.365f, 0.07f), false);
            b.Pivot(FaunaBone.Neck, c.P(0f, 0.395f, 0.18f), false);
            b.Pivot(FaunaBone.Head, c.P(0f, 0.43f, 0.225f), false);
            b.Pivot(FaunaBone.Jaw, c.P(0f, 0.405f, 0.265f), false);
            b.Pivot(FaunaBone.EarL, c.P(-0.05f * hs, 0.445f, 0.235f), true);
            b.Pivot(FaunaBone.Tail0, c.P(0f, 0.38f, -0.23f), false);
            b.Pivot(FaunaBone.Tail1, c.P(0f, 0.35f, -0.295f), false);
            b.Pivot(FaunaBone.Tail2, c.P(0f, 0.28f, -0.34f), false);
            b.Pivot(FaunaBone.FrontUpperL, c.P(shoulder.X, shoulder.Y, shoulder.Z), true);
            b.Pivot(FaunaBone.FrontLowerL, c.P(elbow.X, elbow.Y, elbow.Z), true);
            b.Pivot(FaunaBone.FrontFootL, c.P(wrist.X, wrist.Y, wrist.Z), true);
            b.Pivot(FaunaBone.HindUpperL, c.P(hip.X, hip.Y, hip.Z), true);
            b.Pivot(FaunaBone.HindLowerL, c.P(knee.X, knee.Y, knee.Z), true);
            b.Pivot(FaunaBone.HindFootL, c.P(ankle.X, ankle.Y, ankle.Z), true);
            b.Pivot(FaunaBone.Rider, c.P(0f, 0.445f, -0.04f), false);

            // Torso: a stocky barrel under thick fur, the rump round and a little higher than the shoulders' line;
            // grey-brown over the shoulders, golden-orange from the small of the back to the tail.
            c.Begin();
            c.Add(0f, 0.375f, -0.232f, 0.055f, 0.05f, 0.055f, FaunaBone.Pelvis, rump);
            c.Add(0f, 0.372f, -0.19f, 0.1f, 0.078f, 0.09f, FaunaBone.Pelvis, rump);
            c.Add(0f, 0.362f, -0.1f, 0.11f, 0.077f, 0.1f, FaunaBone.Pelvis, FMath.LerpColour(rump, fur, 0.25f));
            c.Add(0f, 0.36f, -0.005f, 0.106f, 0.079f, 0.104f, FaunaBone.Chest, FMath.LerpColour(rump, fur, 0.8f));
            c.Add(0f, 0.368f, 0.08f, 0.098f, 0.082f, 0.098f, FaunaBone.Chest, fur);
            c.Add(0f, 0.38f, 0.15f, 0.08f, 0.074f, 0.078f, FaunaBone.Chest, fur);
            c.Add(0f, 0.392f, 0.2f, 0.052f, 0.048f, 0.05f, FaunaBone.Chest, fur);
            int vBody = c.Tube(d.BodySegs, d.Rings, FaunaPart.Body, hair, Fv3.Up, 0.7f, 0.6f);
            for (int v = vBody; v < b.VertexCount; v++)
            {
                float down = -b.NormalOf(v).Y;
                if (down > 0.25f) b.SetColour(v, FMath.LerpColour(b.ColourAt(v), belly, 0.65f * FMath.SmoothStep(0.25f, 0.7f, down)));
            }

            // Short thick neck.
            c.Begin();
            c.Add(0f, 0.38f, 0.13f, 0.068f, 0.06f, 0.066f, FaunaBone.Chest, fur);
            c.Add(0f, 0.405f, 0.195f, 0.054f, 0.05f, 0.054f, FaunaBone.Neck, fur);
            c.Add(0f, 0.425f, 0.23f, 0.046f, 0.045f, 0.046f, FaunaBone.Head, fur);
            c.Tube(d.BodySegs - 3, 1, FaunaPart.Neck, hair, Fv3.Up, 0.5f, 0.5f);

            // Head: round cranium, heavy brow, short muzzle; the bare pink face sits in a pale ruff of fur that frames
            // the cheeks and the crown.
            float hy = 0.432f, hz = 0.245f;
            c.Ell(0f, hy + 0.012f * hs, hz - 0.006f, 0.054f * hs, 0.05f * hs, 0.052f * hs, Fv3.Forward, Fv3.Up, d.Fine ? 6 : d.Far ? 3 : 4, d.BodySegs,
                  FaunaBone.Head, fur, hair, FaunaPart.Head);
            if (!d.Far)
            {
                // The ruff: a flattened halo behind the face, wider than the skull at the cheeks.
                c.Ell(0f, hy - 0.002f * hs, hz + 0.018f * hs, 0.06f * hs, 0.056f * hs, 0.024f * hs, Fv3.Forward, Fv3.Up, d.Fine ? 4 : 3, d.BodySegs,
                      FaunaBone.Head, ruff, hair, FaunaPart.Head);
            }
            // Face disc (bare skin).
            c.Ell(0f, hy, hz + 0.034f * hs, 0.04f * hs, 0.044f * hs, 0.026f * hs, Fv3.Forward, Fv3.Up, d.Fine ? 4 : 3, Math.Max(4, d.BodySegs - 2),
                  FaunaBone.Head, face, FaunaPalette.SkinChannel, FaunaPart.Face);
            // Brow ridge.
            if (!d.Far)
                c.Ell(0f, hy + 0.022f * hs, hz + 0.05f * hs, 0.035f * hs, 0.01f * hs, 0.014f * hs, Fv3.Forward, Fv3.Up, 3, d.SmallSegs + 2,
                      FaunaBone.Head, FMath.LerpColour(face, 0x8A5A4A00u, 0.3f), FaunaPalette.SkinChannel, FaunaPart.Face);
            // Muzzle and the jaw.
            c.Ell(0f, hy - 0.022f * hs, hz + 0.058f * hs, 0.026f * hs, 0.022f * hs, 0.024f * hs, Fv3.Forward, Fv3.Up, d.Fine ? 4 : 3, d.SmallSegs + 2,
                  FaunaBone.Head, face, FaunaPalette.SkinChannel, FaunaPart.Muzzle);
            if (!d.Far)
                c.Ell(0f, hy - 0.04f * hs, hz + 0.048f * hs, 0.022f * hs, 0.012f * hs, 0.02f * hs, Fv3.Forward, Fv3.Up, 3, d.SmallSegs + 1,
                      FaunaBone.Jaw, face, FaunaPalette.SkinChannel, FaunaPart.Mouth);
            if (d.Fine)
            {
                SideMark n = c.StartSide();
                c.Ell(-0.006f * hs, hy - 0.016f * hs, hz + 0.08f * hs, 0.004f * hs, 0.003f * hs, 0.003f * hs, Fv3.Forward, Fv3.Up, 2, 4, FaunaBone.Head,
                      0x5A2E2A00u, FaunaPalette.NoseChannel, FaunaPart.Nose);
                c.EndSide(n);
            }
            {
                SideMark m = c.StartSide();
                // Close-set amber eyes looking forward.
                Fv3 ec = c.P(-0.016f * hs, hy + 0.008f * hs, hz + 0.055f * hs);
                b.Eye(ec, 0.0095f * hs * c.S, new Fv3(-0.25f, 0.05f, 1f).Normalized, Fv3.Up, FaunaBone.Head, 0x8A5A2600u, FaunaPalette.EyeBlack,
                      d.Eyes);
                // Small round ear close to the head, half hidden in the ruff.
                c.Ell(-0.054f * hs, hy + 0.01f * hs, hz - 0.008f, 0.008f * hs, 0.016f * hs, 0.014f * hs, new Fv3(-1f, 0f, 0.3f).Normalized, Fv3.Up,
                      3, d.SmallSegs + 1, FaunaBone.EarL, FMath.LerpColour(face, 0x9A7A6A00u, 0.5f), FaunaPalette.SkinChannel, FaunaPart.Ear);
                c.EndSide(m);
            }
            // Fur crown over the brow.
            if (!d.Far)
            {
                c.Ell(0f, hy + 0.042f * hs, hz - 0.008f * hs, 0.044f * hs, 0.02f * hs, 0.042f * hs, Fv3.Forward, new Fv3(0f, 1f, -0.4f), 3,
                      d.SmallSegs + 2, FaunaBone.Head, FMath.LerpColour(fur, ruff, 0.35f), hair, FaunaPart.Head);
            }

            // Limbs, left side then mirrored: each segment a rigid tube between joint pivots, a ball at the elbow and
            // the knee; thick upper arms and thighs, shorter forearms and shins, hands and feet with fingers.
            {
                SideMark m = c.StartSide();
                if (d.Far)
                {
                    // Far away: one smooth tube per limb, bending across the joints (the folds are too small to see).
                    Limb(c, shoulder + new Fv3(0.01f, 0.025f, 0.005f), 0.036f, elbow, 0.026f, wrist, 0.017f, FaunaBone.FrontUpperL, FaunaBone.FrontLowerL,
                         FaunaBone.FrontFootL, fur, hand);
                    Limb(c, hip + new Fv3(0.012f, 0.02f, -0.01f), 0.055f, knee, 0.033f, ankle, 0.02f, FaunaBone.HindUpperL, FaunaBone.HindLowerL,
                         FaunaBone.HindFootL, rump, hand);
                }
                else
                {
                    int jr = d.Coarse ? 4 : 5;
                    // Upper arm.
                    Seg(c, shoulder + new Fv3(0.01f, 0.025f, 0.005f), 0.038f, Mid(shoulder, elbow, 0.5f), 0.031f, elbow, 0.025f, FaunaBone.FrontUpperL, fur, fur,
                        0.5f);
                    Joint(c, elbow, 0.025f, FaunaBone.FrontUpperL, fur, jr);
                    // Forearm to the wrist.
                    Seg(c, elbow, 0.024f, Mid(elbow, wrist, 0.5f), 0.021f, wrist, 0.016f, FaunaBone.FrontLowerL, fur, FMath.LerpColour(fur, hand, 0.35f), 0.3f);
                    Hand(c, wrist, 0.016f, FaunaBone.FrontFootL, hand, 0.06f);
                    // Thigh: full and golden.
                    Seg(c, hip + new Fv3(0.012f, 0.02f, -0.01f), 0.058f, Mid(hip, knee, 0.5f), 0.048f, knee, 0.035f, FaunaBone.HindUpperL, rump, mid, 0.5f);
                    Joint(c, knee, 0.034f, FaunaBone.HindUpperL, mid, jr);
                    // Shin to the ankle.
                    Seg(c, knee, 0.032f, Mid(knee, ankle, 0.5f), 0.026f, ankle, 0.019f, FaunaBone.HindLowerL, mid, FMath.LerpColour(fur, hand, 0.3f), 0.3f);
                    Hand(c, ankle, 0.019f, FaunaBone.HindFootL, hand, 0.075f);
                }
                c.EndSide(m);
            }

            // Tail hanging in a curve, golden at the root.
            c.Begin();
            c.Add(0f, 0.385f, -0.225f, 0.022f, 0.022f, 0.022f, FaunaBone.Tail0, rump);
            c.Add(0f, 0.375f, -0.28f, 0.018f, 0.018f, 0.018f, FaunaBone.Tail0, rump);
            c.Add(0f, 0.34f, -0.32f, 0.015f, 0.015f, 0.015f, FaunaBone.Tail1, mid);
            c.Add(0f, 0.28f, -0.345f, 0.012f, 0.012f, 0.012f, FaunaBone.Tail2, fur);
            c.Add(0f, 0.22f, -0.35f, 0.009f, 0.009f, 0.009f, FaunaBone.Tail2, fur);
            c.Tube(d.LimbSegs - 2, d.LimbRings, FaunaPart.Tail, hair, Fv3.Forward, 0.5f, 0.6f);

            b.GroundAoHeightM = 0.15f * c.S;
            target.EyeHeightM = (hy + 0.008f * hs) * c.S;
            b.Finish();
        }

        private static Fv3 Mid(Fv3 a, Fv3 b, float t)
        {
            return a + (b - a) * t;
        }

        /// <summary>A rigid limb segment through three points (species units) on one bone, closed by rounded caps.</summary>
        private static void Seg(FaunaSketch c, Fv3 a, float ra, Fv3 m, float rm, Fv3 e, float re, FaunaBone bone, uint colA, uint colE, float capEnd)
        {
            c.Begin();
            c.Add(a.X, a.Y, a.Z, ra, bone, colA);
            c.Add(m.X, m.Y, m.Z, rm, bone, FMath.LerpColour(colA, colE, 0.5f));
            c.Add(e.X, e.Y, e.Z, re, bone, colE);
            c.Tube(c.D.LimbSegs, c.D.LimbRings, FaunaPart.Leg, FaunaPalette.FurChannel, Fv3.Forward, 0.6f, capEnd);
        }

        /// <summary>A whole limb as one tube for the far level: upper bone, lower bone, and a short foot on the ground.</summary>
        private static void Limb(FaunaSketch c, Fv3 top, float rt, Fv3 joint, float rj, Fv3 end, float re, FaunaBone upper, FaunaBone lower, FaunaBone foot,
                                 uint colTop, uint colEnd)
        {
            c.Begin();
            c.Add(top.X, top.Y, top.Z, rt, upper, colTop);
            c.Add(joint.X, joint.Y, joint.Z, rj, lower, FMath.LerpColour(colTop, colEnd, 0.3f));
            c.Add(end.X, end.Y, end.Z, re, foot, colEnd);
            c.Add(end.X, 0.012f, end.Z + 0.04f, 0.6f * re, foot, colEnd);
            c.Tube(c.D.LimbSegs, 1, FaunaPart.Leg, FaunaPalette.FurChannel, Fv3.Forward, 0.6f, 0.5f);
        }

        /// <summary>A round joint (elbow, knee) centred on its pivot, so the limb stays round however far it bends.</summary>
        private static void Joint(FaunaSketch c, Fv3 p, float r, FaunaBone bone, uint col, int segs)
        {
            c.Ell(p.X, p.Y, p.Z, r, r, r, Fv3.Forward, Fv3.Up, 3, segs, bone, col, FaunaPalette.FurChannel, FaunaPart.Leg);
        }

        /// <summary>
        /// A hand or foot hanging from its <paramref name="wrist"/> pivot (species units): the heel of the hand runs down
        /// and forward from the wrist into a flat palm on the ground, so it stays joined to the arm however the wrist
        /// turns; four fingers and a thumb on the near level.
        /// </summary>
        private static void Hand(FaunaSketch c, Fv3 wrist, float r, FaunaBone bone, uint col, float len)
        {
            FaunaDetail d = c.D;
            if (d.Far) return;
            float y = 0.011f;
            var palm = new Fv3(wrist.X, y, wrist.Z + 0.45f * len);
            c.Begin();
            c.Add(wrist.X, wrist.Y, wrist.Z, r, r, r, bone, col);
            c.Add(wrist.X, 0.5f * (wrist.Y + y) + 0.002f, wrist.Z + 0.2f * len, 0.95f * r, 0.75f * r, 0.75f * r, bone, col);
            c.Add(palm.X, palm.Y, palm.Z, 1.05f * r, 0.55f * r, 0.6f * r, bone, col);
            c.Add(palm.X, y - 0.001f, wrist.Z + 0.68f * len, 0.95f * r, 0.4f * r, 0.45f * r, bone, col);
            c.Tube(d.SmallSegs + 1, 1, FaunaPart.Hand, FaunaPalette.SkinChannel, Fv3.Up, 0.6f, 0.5f);
            if (!d.Fine) return;
            for (int k = 0; k < 4; k++)
            {
                float fx = wrist.X - 0.6f * r + k * 0.4f * r;
                c.Begin();
                c.Add(fx, y, wrist.Z + 0.62f * len, 0.0045f, bone, col);
                c.Add(fx + (k - 1.5f) * 0.002f, y - 0.002f, wrist.Z + 0.62f * len + 0.018f, 0.0036f, bone, col);
                c.Tube(4, 1, FaunaPart.Hand, FaunaPalette.SkinChannel, Fv3.Up, 0.5f, 0.6f);
            }
            c.Begin();
            c.Add(wrist.X + 0.7f * r, y + 0.002f, wrist.Z + 0.4f * len, 0.0048f, bone, col);
            c.Add(wrist.X + 1.3f * r, y, wrist.Z + 0.4f * len + 0.014f, 0.0036f, bone, col);
            c.Tube(4, 1, FaunaPart.Hand, FaunaPalette.SkinChannel, Fv3.Up, 0.5f, 0.6f);
        }
    }
}
