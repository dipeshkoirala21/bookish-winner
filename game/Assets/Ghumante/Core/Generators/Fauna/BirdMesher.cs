using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Generators.Fauna
{
    /// <summary>
    /// The valley's common birds (docs/research/w2/ref_animals.md §7; Bird Conservation Nepal's urban counts: rock
    /// pigeon, house crow, house sparrow, barn swallow, common myna, black kite, cattle egret): the shared avian recipe
    /// (<see cref="AvianMesher"/>) with each species' proportions and plumage. Pigeons are tinted per instance (blue-bar
    /// grey, dark chequer, pale); the others carry fixed colours. Plus the 4-triangle paper bird for far flocks
    /// (<see cref="PaperBird"/>).
    /// </summary>
    public static class BirdMesher
    {
        /// <summary>The tint the pigeon mesh colours are relative to (a typical blue-bar bird).</summary>
        public const uint PigeonBaseTint = 0xA7ABB4u;

        /// <summary>Builds a flying or perching bird at <paramref name="lod"/>.</summary>
        public static void Build(FaunaSpecies species, int lod, FaunaMesh target)
        {
            Build(species, lod, CoatPattern.Plain, target);
        }

        /// <summary>Builds a bird; <see cref="CoatPattern.Saddle"/> gives the cattle egret its breeding buff.</summary>
        public static void Build(FaunaSpecies species, int lod, CoatPattern pattern, FaunaMesh target)
        {
            var b = new FaunaBuilder(target, species, lod, pattern);
            var c = new FaunaSketch { B = b, D = FaunaDetail.Avian(lod), S = 1f };
            AvianSpec s = Spec(species);
            AvianMesher.Landmarks L = AvianMesher.Build(c, s, FaunaPalette.FeatherChannel);
            Extras(c, species, pattern, s, L);
            b.GroundAoHeightM = Math.Max(0.02f, s.LegLen * 0.8f);
            b.OcclusionStrength = 0.8f;
            target.EyeHeightM = L.HeadC.Y;
            b.Finish();
        }

        /// <summary>The proportions and colours of a species.</summary>
        internal static AvianSpec Spec(FaunaSpecies species)
        {
            var s = new AvianSpec();
            switch (species)
            {
                case FaunaSpecies.Pigeon:
                {
                    // Rock pigeon of the squares: pale blue-grey back and wings, slate head flowing into an iridescent
                    // neck band, grey breast, two black wing bars, a dark tail band, an orange eye with a black pupil,
                    // a dark bill with a white cere, red feet (ref_animals.md 7).
                    uint t = PigeonBaseTint;
                    s = Shape(0.17f, 0.052f, 0.05f, 18f, 0.045f, 0.024f, 12f, 0f, 0.02f, 0.023f, 0.016f, 0.0045f, 0f, 0f, 0.002f, 0.1f, 0.03f, 0f, 5f, 0f,
                              0.045f, 0.0042f, 0.018f, 0.02f, 0.66f, 0.11f, 0.15f, 0.0048f);
                    s.Body = Rel(0x8C919CFFu, t);
                    s.Back = Rel(0xA0A6B0FFu, t);
                    s.Belly = Rel(0x959AA4FFu, t);
                    s.Head = Rel(0x5E6370FFu, t);
                    s.Neck = Rel(0x646A76FFu, t);
                    s.Wing = Rel(0xB2B7C0FFu, t);
                    s.WingTip = Rel(0x55585FFFu, t);
                    s.Tail = Rel(0x8C919AFFu, t);
                    s.TailTip = FaunaPalette.PigeonTailBand;
                    s.Bill = FaunaPalette.PigeonBill;
                    s.BillTip = FaunaPalette.PigeonBill;
                    s.Leg = FaunaPalette.PigeonFeet;
                    s.Iris = FaunaPalette.PigeonEye;
                    s.Cere = FaunaPalette.PigeonCere;
                    s.BodyTinted = true;
                    // Two black bars across the inner wing, pale grey between and behind them, dark primary tips.
                    s.FoldU = new[] { 0f, 0.36f, 0.46f, 0.56f, 0.68f, 1f };
                    s.FoldCol = new[] { s.Wing, FaunaPalette.PigeonBar, s.Wing, FaunaPalette.PigeonBar, s.Wing, s.WingTip };
                    break;
                }
                case FaunaSpecies.Crow:
                    s = Shape(0.2f, 0.062f, 0.058f, 22f, 0.045f, 0.03f, 15f, 0f, 0.028f, 0.033f, 0.045f, 0.0095f, 0f, 0f, 0.007f, 0.16f, 0.045f, 0f, 8f, 0f,
                              0.06f, 0.0045f, 0.02f, 0.03f, 0.8f, 0.14f, 0.2f, 0.006f);
                    s.Body = 0x1E1E2200u;
                    s.Back = 0x18181D00u;
                    s.Belly = 0x2A2A2E00u;
                    s.Head = 0x16161800u;
                    s.Neck = FaunaPalette.CrowGrey;
                    s.Wing = 0x1A1A2000u;
                    s.WingTip = 0x121216u << 8;
                    s.Tail = 0x18181D00u;
                    s.TailTip = 0x141418u << 8;
                    s.Bill = 0x111111u << 8;
                    s.BillTip = s.Bill;
                    s.Leg = 0x1A1A1Au << 8;
                    s.Iris = 0x3A2010u << 8;
                    break;
                case FaunaSpecies.BlackKite:
                    s = Shape(0.26f, 0.075f, 0.07f, 30f, 0.04f, 0.038f, 10f, 0f, 0.035f, 0.04f, 0.03f, 0.011f, 0.012f, 0f, 0.004f, 0.27f, 0.07f, 0.35f, 5f, 0f,
                              0.07f, 0.007f, 0.028f, 0.035f, 1.5f, 0.3f, 0.36f, 0.0065f);
                    s.Body = FaunaPalette.KiteBrown;
                    s.Back = 0x4E3B2Au << 8;
                    s.Belly = 0x6A503Au << 8;
                    s.Head = FaunaPalette.KitePale;
                    s.Neck = 0x7A6248u << 8;
                    s.Wing = 0x4E3B2Au << 8;
                    s.WingTip = 0x2A2018u << 8;
                    s.Tail = 0x5A4430u << 8;
                    s.TailTip = 0x4A3826u << 8;
                    s.Bill = 0x2A2420u << 8;
                    s.BillTip = 0x1A1612u << 8;
                    s.Leg = 0xE0B830u << 8;
                    s.Iris = 0x9A6A20u << 8;
                    s.Cere = 0xE8C040u << 8;
                    s.Fingers = 5;
                    break;
                case FaunaSpecies.Myna:
                    s = Shape(0.12f, 0.04f, 0.038f, 18f, 0.035f, 0.019f, 15f, 0f, 0.018f, 0.02f, 0.02f, 0.0045f, 0f, 0f, 0.003f, 0.075f, 0.028f, 0f, 5f, 0f,
                              0.05f, 0.0035f, 0.014f, 0.018f, 0.42f, 0.08f, 0.11f, 0.005f);
                    s.Body = FaunaPalette.MynaBrown;
                    s.Back = 0x553A28u << 8;
                    s.Belly = 0x6A4A36u << 8;
                    s.Head = FaunaPalette.MynaHead;
                    s.Neck = 0x2A221Cu << 8;
                    s.Wing = 0x4A3424u << 8;
                    s.WingTip = 0x1A1A1Au << 8;
                    s.Tail = 0x1C1C1Cu << 8;
                    s.TailTip = 0xE8E4DCu << 8;
                    s.Bill = FaunaPalette.Yellow;
                    s.BillTip = FaunaPalette.Yellow;
                    s.Leg = 0xE8B820u << 8;
                    s.Iris = 0x7A2A10u << 8;
                    s.FoldU = new[] { 0f, 0.55f, 0.72f, 1f };
                    s.FoldCol = new[] { s.Wing, s.Wing, 0xF2F0EAu << 8, s.WingTip };
                    break;
                case FaunaSpecies.Sparrow:
                    s = Shape(0.075f, 0.026f, 0.025f, 20f, 0.015f, 0.014f, 15f, 0f, 0.0145f, 0.015f, 0.009f, 0.0042f, 0f, 0f, 0.002f, 0.05f, 0.018f, 0f, 5f, 0f,
                              0.02f, 0.0022f, 0.009f, 0.011f, 0.24f, 0.055f, 0.065f, 0.0036f);
                    s.Body = 0xB8B0A0u << 8;
                    s.Back = FaunaPalette.SparrowBrown;
                    s.Belly = 0xCBC4B6u << 8;
                    s.Head = FaunaPalette.SparrowCrown;
                    s.Neck = 0x8A4A2Au << 8;
                    s.Wing = 0x7A5A3Au << 8;
                    s.WingTip = 0x4A3424u << 8;
                    s.Tail = 0x5A4434u << 8;
                    s.TailTip = 0x4A3828u << 8;
                    s.Bill = FaunaPalette.SparrowBill;
                    s.BillTip = s.Bill;
                    s.Leg = 0xB09080u << 8;
                    s.Iris = 0x1A1210u << 8;
                    s.FoldU = new[] { 0f, 0.28f, 0.55f, 1f };
                    s.FoldCol = new[] { s.Wing, 0xF0EDE6u << 8, s.Wing, s.WingTip };
                    break;
                case FaunaSpecies.Egret:
                    s = Shape(0.2f, 0.055f, 0.06f, 35f, 0.14f, 0.018f, 22f, 0.05f, 0.022f, 0.03f, 0.06f, 0.006f, 0f, 0f, 0.004f, 0.08f, 0.04f, 0f, 25f, 0f,
                              0.2f, 0.005f, 0.025f, 0.055f, 0.92f, 0.18f, 0.2f, 0.0055f);
                    s.Body = FaunaPalette.EgretWhite;
                    s.Back = 0xF2F2ECu << 8;
                    s.Belly = 0xF7F7F2u << 8;
                    s.Head = FaunaPalette.EgretWhite;
                    s.Neck = FaunaPalette.EgretWhite;
                    s.Wing = 0xF4F4EEu << 8;
                    s.WingTip = 0xEDEDE6u << 8;
                    s.Tail = 0xF2F2ECu << 8;
                    s.TailTip = 0xEDEDE6u << 8;
                    s.Bill = FaunaPalette.Yellow;
                    s.BillTip = 0xE8B414u << 8;
                    s.Leg = FaunaPalette.EgretLeg;
                    s.Iris = 0xF2D03Au << 8;
                    break;
                default: // Swallow
                    s = Shape(0.085f, 0.028f, 0.025f, 15f, 0.012f, 0.014f, 15f, 0f, 0.0145f, 0.015f, 0.006f, 0.004f, 0f, 0.5f, 0.001f, 0.05f, 0.022f, 0.6f, 0f, 0.05f,
                              0.012f, 0.002f, 0.008f, 0.008f, 0.33f, 0.055f, 0.09f, 0.0035f);
                    s.Body = FaunaPalette.SwallowBelly;
                    s.Back = FaunaPalette.SwallowBlue;
                    s.Belly = 0xF2EADEu << 8;
                    s.Head = FaunaPalette.SwallowBlue;
                    s.Neck = FaunaPalette.SwallowThroat;
                    s.Wing = 0x1A2030u << 8;
                    s.WingTip = 0x141824u << 8;
                    s.Tail = FaunaPalette.SwallowBlue;
                    s.TailTip = 0x141824u << 8;
                    s.Bill = 0x111111u << 8;
                    s.BillTip = s.Bill;
                    s.Leg = 0x2A2420u << 8;
                    s.Iris = 0x1A1210u << 8;
                    break;
            }
            return s;
        }

        internal static AvianSpec Shape(float bodyLen, float bodyW, float bodyH, float pitch, float neckLen, float neckR, float neckLean, float neckS,
                                        float headR, float headLen, float billLen, float billR, float billHook, float billFlat, float billDrop, float tailLen,
                                        float tailW, float tailFork, float tailDrop, float streamer, float legLen, float legR, float legSpread, float toeLen,
                                        float span, float chord, float foldLen, float eyeR)
        {
            return new AvianSpec
            {
                BodyLen = bodyLen, BodyW = bodyW, BodyH = bodyH, BodyPitchDeg = pitch, NeckLen = neckLen, NeckR = neckR, NeckLeanDeg = neckLean,
                NeckS = neckS, HeadR = headR, HeadLen = headLen, BillLen = billLen, BillR = billR, BillHook = billHook, BillFlat = billFlat,
                BillDrop = billDrop, TailLen = tailLen, TailW = tailW, TailFork = tailFork, TailDropDeg = tailDrop, Streamer = streamer, LegLen = legLen,
                LegR = legR, LegSpread = legSpread, ToeLen = toeLen, Span = span, Chord = chord, FoldLen = foldLen, EyeR = eyeR,
            };
        }

        /// <summary>A tinted vertex colour that renders as <paramref name="desired"/> under the instance tint
        /// <paramref name="tint"/> (0xRRGGBB): each channel divided by the tint's, alpha 255.</summary>
        internal static uint Rel(uint desired, uint tint)
        {
            uint r = 0;
            for (int k = 0; k < 3; k++)
            {
                float dv = (desired >> (24 - 8 * k)) & 0xFF;
                float tv = Math.Max(1u, (tint >> (16 - 8 * k)) & 0xFF);
                r |= (uint)FMath.Clamp(dv * 255f / tv, 0f, 255f) << (24 - 8 * k);
            }
            return r | 0xFFu;
        }

        /// <summary>Species markings painted over the shared recipe, and small extra parts.</summary>
        private static void Extras(FaunaSketch c, FaunaSpecies sp, CoatPattern pattern, in AvianSpec s, in AvianMesher.Landmarks L)
        {
            FaunaBuilder b = c.B;
            FaunaDetail d = c.D;
            FaunaMesh f = b.Target;
            for (int v = 0; v < b.VertexCount; v++)
            {
                FaunaPart part = (FaunaPart)f.Part[v];
                Fv3 p = b.PositionOf(v), n = b.NormalOf(v);
                uint col = b.ColourAt(v);
                switch (sp)
                {
                    case FaunaSpecies.Pigeon:
                    {
                        if (part == FaunaPart.Neck || part == FaunaPart.Body)
                        {
                            // The iridescent band round the neck: green on the sides, purple-violet at the front and the
                            // nape, fading into the slate head above and the grey breast below; relative to the instance
                            // tint, so dark and pale birds keep a matching sheen.
                            float along = Fv3.Dot(p - L.NeckBase, L.NeckDir) / s.NeckLen;
                            float band = FMath.SmoothStep(-0.25f, 0.2f, along) * (1f - FMath.SmoothStep(0.75f, 1.05f, along));
                            band *= FMath.SmoothStep(L.NeckBase.Y - 0.3f * s.BodyH, L.NeckBase.Y, p.Y);
                            if (band > 0f)
                            {
                                uint irid = FMath.LerpColour(Rel(0x7B5C8EFFu, PigeonBaseTint), Rel(0x4E7A6AFFu, PigeonBaseTint), FMath.SmoothStep(0.35f, 0.8f, Math.Abs(n.X)));
                                col = FMath.LerpColour(col, irid, 0.9f * band);
                            }
                        }
                        // The two bars across the inner wing in flight (trailing half of the arm).
                        if ((f.Bone0[v] == (byte)FaunaBone.WingL || f.Bone0[v] == (byte)FaunaBone.WingR) && p.Z < L.Shoulder.Z - 0.3f * s.Chord && n.Y > 0f)
                            col = FaunaPalette.PigeonBar;
                        // Pale rump above the tail.
                        if (part == FaunaPart.Body && Fv3.Dot(p - L.BodyC, L.Fwd) < -0.3f * s.BodyLen && Fv3.Dot(n, L.Up) > 0.4f)
                            col = Rel(0xC3C6CCFFu, PigeonBaseTint);
                        break;
                    }
                    case FaunaSpecies.Crow:
                    {
                        // Grey collar: nape, neck sides and upper breast; black face, crown and throat.
                        if (part == FaunaPart.Head && n.Z < -0.2f) col = FaunaPalette.CrowGrey;
                        if (part == FaunaPart.Neck && n.Z > 0.5f && n.Y < 0.3f) col = 0x2A2A2Eu << 8;
                        if (part == FaunaPart.Body && p.Z > L.BodyC.Z + 0.04f && n.Y > -0.3f && p.Y > L.BodyC.Y) col = FMath.LerpColour(col, FaunaPalette.CrowGrey, 0.6f);
                        break;
                    }
                    case FaunaSpecies.BlackKite:
                    {
                        // Streaked pale head, barred tail, pale panel on the underwing.
                        if (part == FaunaPart.Tail && ((int)((L.TailBase.Z - p.Z) * 60f) & 1) == 0) col = FMath.LerpColour(col, 0x3A2C1Eu << 8, 0.5f);
                        if ((f.Bone0[v] == (byte)FaunaBone.WingTipL || f.Bone0[v] == (byte)FaunaBone.WingTipR) && n.Y < -0.3f && Math.Abs(p.X) < 0.5f)
                            col = 0x9A8468u << 8;
                        break;
                    }
                    case FaunaSpecies.Myna:
                    {
                        // White vent and undertail, white patch at the base of the primaries.
                        if (part == FaunaPart.Body && n.Y < -0.4f && p.Z < L.BodyC.Z - 0.02f) col = 0xEDE8E0u << 8;
                        if ((f.Bone0[v] == (byte)FaunaBone.WingTipL || f.Bone0[v] == (byte)FaunaBone.WingTipR) && Math.Abs(p.X) < 0.13f) col = 0xF2F0EAu << 8;
                        break;
                    }
                    case FaunaSpecies.Sparrow:
                    {
                        // Black bib, pale cheeks, chestnut nape, streaked back, white wing bar.
                        if ((part == FaunaPart.Neck || part == FaunaPart.Body) && n.Z > 0.4f && p.Y > L.BodyC.Y && n.Y < 0.6f) col = FaunaPalette.SparrowBib;
                        if (part == FaunaPart.Head)
                        {
                            if (n.Y < 0.3f && n.Z > -0.2f && Math.Abs(n.X) > 0.5f) col = FaunaPalette.SparrowCheek;
                            else if (n.Z < -0.3f) col = 0x8A4A2Au << 8;
                            if (n.Z > 0.6f && n.Y < 0.1f) col = FaunaPalette.SparrowBib;
                        }
                        if (part == FaunaPart.Body && n.Y > 0.4f && ((int)(p.X * 400f + p.Z * 120f) & 1) == 0) col = FaunaPalette.SparrowStreak;
                        break;
                    }
                    case FaunaSpecies.Egret:
                    {
                        if (pattern == CoatPattern.Saddle)
                        {
                            // Breeding plumage: buff crown, breast and back plumes.
                            if (part == FaunaPart.Head && n.Y > 0f) col = FaunaPalette.EgretBuff;
                            if (part == FaunaPart.Neck && n.Z > 0.3f) col = FMath.LerpColour(col, FaunaPalette.EgretBuff, 0.8f);
                            if (part == FaunaPart.Body && n.Y > 0.6f) col = FMath.LerpColour(col, FaunaPalette.EgretBuff, 0.7f);
                        }
                        break;
                    }
                    case FaunaSpecies.Swallow:
                    {
                        // Rufous forehead and throat, a dark breast band, white spots in the tail.
                        if (part == FaunaPart.Head && n.Z > 0.5f) col = FaunaPalette.SwallowThroat;
                        if (part == FaunaPart.Neck) col = n.Z > -0.2f && n.Y < 0.5f ? FaunaPalette.SwallowThroat : FaunaPalette.SwallowBlue;
                        if (part == FaunaPart.Body && n.Y > 0.25f) col = FaunaPalette.SwallowBlue;
                        if (part == FaunaPart.Body && p.Z > L.BodyC.Z + 0.02f && n.Y > -0.1f) col = FaunaPalette.SwallowBlue;
                        break;
                    }
                }
                b.SetColour(v, col);
            }
            SideMark m;
            switch (sp)
            {
                case FaunaSpecies.Myna:
                    // Bare yellow skin behind the eye.
                    if (d.Coarse) break;
                    m = c.StartSide();
                    b.Ellipsoid(L.HeadC + new Fv3(-0.8f * s.HeadR, 0.15f * s.HeadR, 0.05f * s.HeadLen), new Fv3(-1f, 0f, 0.3f).Normalized, Fv3.Up,
                                0.009f, 0.0065f, 0.003f, 2, 3, FaunaBone.Head, FaunaPalette.Yellow, FaunaPalette.SkinChannel, FaunaPart.Face);
                    c.EndSide(m);
                    break;
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // Paper birds

        /// <summary>Triangles of a paper bird: two wing quads in a V (W2_DESIGN 5.6), drawn with culling off.</summary>
        public const int PaperBirdTris = 4;

        /// <summary>
        /// The far flock bird (W2_DESIGN 5.6): two flat wing quads in a V meeting along the body line, 4 triangles,
        /// single-sided (the presenters draw paper birds with back-face culling off, so one face serves from above and
        /// from below). <paramref name="flap"/> in [−1, 1] lifts (1) or lowers (−1) the wing tips; the presenters bake
        /// a few flap frames and pick one per bird. Colour between the species' wing and underside (tinted for
        /// pigeons), normals up, UV0 = (feather channel, AO 1).
        /// </summary>
        public static void PaperBird(FaunaSpecies species, float flap, MeshData dst)
        {
            dst.Clear();
            AvianSpec s = Spec(species);
            float half = 0.5f * s.Span, chord = s.Chord;
            float dihedral = FMath.Clamp(flap, -1f, 1f) * 0.75f;
            float tipY = (float)Math.Sin(dihedral) * half, tipX = (float)Math.Cos(dihedral) * half;
            uint col = PaperColour(s);
            float ch = (float)FaunaPalette.FeatherChannel;
            float zf = 0.5f * s.BodyLen + s.HeadLen + s.BillLen, zb = -0.5f * s.BodyLen - s.TailLen;
            for (int side = -1; side <= 1; side += 2)
            {
                var p0 = new Fv3(0f, 0f, zf);
                var p1 = new Fv3(0f, 0f, zb);
                var p2 = new Fv3(side * tipX, tipY, 0.15f * chord);
                var p3 = new Fv3(side * tipX * 0.9f, tipY * 0.9f, -0.55f * chord);
                Fv3 nn = Fv3.Cross(p3 - p0, p1 - p0).Normalized;
                if (nn.Y < 0f) nn = -nn;
                // Lean the normals towards straight up so the top and the underside light alike.
                nn = (nn + Fv3.Up).Normalized;
                int a = dst.AddVertex(p0.X, p0.Y, p0.Z, nn.X, nn.Y, nn.Z, col, ch, 1f);
                int b = dst.AddVertex(p1.X, p1.Y, p1.Z, nn.X, nn.Y, nn.Z, col, ch, 1f);
                int t0 = dst.AddVertex(p2.X, p2.Y, p2.Z, nn.X, nn.Y, nn.Z, col, ch, 1f);
                int t1 = dst.AddVertex(p3.X, p3.Y, p3.Z, nn.X, nn.Y, nn.Z, col, ch, 1f);
                Tri(dst, a, b, t1, nn);
                Tri(dst, a, t1, t0, nn);
            }
        }

        /// <summary>
        /// The far bird on the ground (25–70 m from the camera, beyond the light-bird cap): a folded paper bird, two
        /// quads as a tent along the body with the head end higher, 4 triangles at the species' standing size, so a
        /// square's whole flock stays on the ground and none pops into the air out of nothing when it bursts.
        /// </summary>
        public static void PaperBirdSitting(FaunaSpecies species, MeshData dst)
        {
            dst.Clear();
            AvianSpec s = Spec(species);
            FaunaSpeciesInfo info = FaunaCatalog.Info(species);
            float len = info.LengthM, h = info.HeightM;
            float w = 1.1f * s.BodyW;
            uint col = PaperColour(s);
            float ch = (float)FaunaPalette.FeatherChannel;
            var head = new Fv3(0f, 0.8f * h, 0.42f * len);
            var tail = new Fv3(0f, Math.Max(0.3f * h, s.LegLen + 0.3f * s.BodyH), -0.5f * len);
            for (int side = -1; side <= 1; side += 2)
            {
                var f = new Fv3(side * w, Math.Min(0.25f * h, s.LegLen), 0.18f * len);
                var r = new Fv3(side * w * 0.8f, Math.Min(0.25f * h, s.LegLen), -0.22f * len);
                Fv3 nn = Fv3.Cross(tail - head, f - head).Normalized;
                if (nn.X * side < 0f) nn = -nn;
                nn = (nn + Fv3.Up * 0.6f).Normalized;
                int a = dst.AddVertex(head.X, head.Y, head.Z, nn.X, nn.Y, nn.Z, col, ch, 1f);
                int b = dst.AddVertex(tail.X, tail.Y, tail.Z, nn.X, nn.Y, nn.Z, col, ch, 0.85f);
                int c = dst.AddVertex(f.X, f.Y, f.Z, nn.X, nn.Y, nn.Z, col, ch, 0.7f);
                int d = dst.AddVertex(r.X, r.Y, r.Z, nn.X, nn.Y, nn.Z, col, ch, 0.7f);
                Tri(dst, a, c, d, nn);
                Tri(dst, a, d, b, nn);
            }
        }

        /// <summary>Colour of a paper bird: the wing, a little towards the underside (alpha 255 = tinted for the
        /// pigeons, 0 = fixed colour).</summary>
        private static uint PaperColour(in AvianSpec s)
        {
            uint top = s.Wing | 0xFFu;
            uint under = (s.BodyTinted ? s.Belly : s.Wing) | 0xFFu;
            uint c = FMath.LerpColour(top, FMath.Shade(under, 0.85f) | 0xFFu, 0.35f) | 0xFFu;
            return s.BodyTinted ? c : c & 0xFFFFFF00u;
        }

        private static void Tri(MeshData m, int a, int b, int c, Fv3 n)
        {
            Fv3 pa = V(m, a), pb = V(m, b), pc = V(m, c);
            if (Fv3.Dot(Fv3.Cross(pb - pa, pc - pa), n) >= 0f) m.AddTriangle(a, b, c);
            else m.AddTriangle(a, c, b);
        }

        private static Fv3 V(MeshData m, int v)
        {
            return new Fv3(m.Positions[3 * v], m.Positions[3 * v + 1], m.Positions[3 * v + 2]);
        }
    }
}
