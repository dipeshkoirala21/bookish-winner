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
                    uint t = PigeonBaseTint;
                    s = Shape(0.17f, 0.055f, 0.05f, 18f, 0.045f, 0.022f, 15f, 0f, 0.019f, 0.022f, 0.016f, 0.0045f, 0f, 0f, 0.002f, 0.1f, 0.04f, 0f, 5f, 0f,
                              0.045f, 0.004f, 0.018f, 0.022f, 0.66f, 0.11f, 0.16f, 0.0055f);
                    s.Body = Rel(0x8C909AFFu, t);
                    s.Back = Rel(0x9CA1AAFFu, t);
                    s.Belly = Rel(0x9095A0FFu, t);
                    s.Head = Rel(0x707582FFu, t);
                    s.Neck = Rel(0x6E7480FFu, t);
                    s.Wing = Rel(0xA3A8B1FFu, t);
                    s.WingTip = 0x3A3A4200u;
                    s.Tail = Rel(0x8A8F99FFu, t);
                    s.TailTip = FaunaPalette.PigeonTailBand;
                    s.Bill = FaunaPalette.PigeonBill;
                    s.BillTip = FaunaPalette.PigeonBill;
                    s.Leg = FaunaPalette.PigeonFeet;
                    s.Iris = FaunaPalette.PigeonEye;
                    s.BodyTinted = true;
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
                    s.Toes = true;
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
                        if (part == FaunaPart.Neck || (part == FaunaPart.Body && p.Z > L.NeckBase.Z - 0.02f && p.Y > L.NeckBase.Y - 0.03f))
                        {
                            // Iridescent neck: green on the sides, purple at the front and back.
                            float side = Math.Abs(n.X);
                            col = side > 0.55f ? FaunaPalette.PigeonNeckGreen : FaunaPalette.PigeonNeckPurple;
                        }
                        if (f.Bone0[v] == (byte)FaunaBone.FoldL || f.Bone0[v] == (byte)FaunaBone.FoldR)
                        {
                            // Two dark bars across the folded wing.
                            float along = L.Shoulder.Z - p.Z;
                            float u = along / s.FoldLen;
                            if (Math.Abs(u - 0.42f) < 0.05f || Math.Abs(u - 0.6f) < 0.05f) col = FaunaPalette.PigeonBar;
                        }
                        if ((f.Bone0[v] == (byte)FaunaBone.WingL || f.Bone0[v] == (byte)FaunaBone.WingR) && Math.Abs(p.X) > 0.08f && Math.Abs(p.X) < 0.12f &&
                            n.Y > 0f)
                            col = FaunaPalette.PigeonBar;
                        if (part == FaunaPart.Body && p.Z < L.BodyC.Z - 0.04f && n.Y > 0.5f) col = Rel(0xBFC2C8FFu, PigeonBaseTint); // pale rump
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
                        if ((f.Bone0[v] == (byte)FaunaBone.FoldL || f.Bone0[v] == (byte)FaunaBone.FoldR) && L.Shoulder.Z - p.Z > 0.55f * s.FoldLen &&
                            L.Shoulder.Z - p.Z < 0.7f * s.FoldLen)
                            col = 0xF2F0EAu << 8;
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
                        if ((f.Bone0[v] == (byte)FaunaBone.FoldL || f.Bone0[v] == (byte)FaunaBone.FoldR) && Math.Abs(L.Shoulder.Z - p.Z - 0.25f * s.FoldLen) < 0.006f)
                            col = 0xF0EDE6u << 8;
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
                case FaunaSpecies.Pigeon:
                    // The white cere at the base of the bill.
                    if (d.Fine)
                        b.Ellipsoid(L.BillBase + new Fv3(0f, 0.002f, 0f), Fv3.Forward, Fv3.Up, 0.0045f, 0.0035f, 0.0055f, 2, 4, FaunaBone.Head,
                                    FaunaPalette.PigeonCere, FaunaPalette.SkinChannel, FaunaPart.Beak);
                    break;
                case FaunaSpecies.Myna:
                    // Bare yellow skin behind the eye.
                    if (d.Coarse) break;
                    m = c.StartSide();
                    b.Ellipsoid(L.HeadC + new Fv3(-0.8f * s.HeadR, 0.15f * s.HeadR, 0.05f * s.HeadLen), new Fv3(-1f, 0f, 0.3f).Normalized, Fv3.Up,
                                0.009f, 0.0065f, 0.003f, 2, 4, FaunaBone.Head, FaunaPalette.Yellow, FaunaPalette.SkinChannel, FaunaPart.Face);
                    c.EndSide(m);
                    break;
                case FaunaSpecies.BlackKite:
                    // Yellow cere.
                    if (d.Coarse) break;
                    b.Ellipsoid(L.BillBase, Fv3.Forward, Fv3.Up, 0.011f, 0.009f, 0.008f, 2, 4, FaunaBone.Head, 0xE8C040u << 8,
                                FaunaPalette.SkinChannel, FaunaPart.Beak);
                    break;
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // Paper birds

        /// <summary>Triangles of a paper bird: two wing quads in a V, each with a top and a bottom face.</summary>
        public const int PaperBirdTris = 8;

        /// <summary>
        /// The far flock bird (W2_DESIGN 5.6): two flat wing quads in a V meeting along the body line, both faces
        /// (seen from below as they pass overhead and from above from the hills), 8 triangles. <paramref name="flap"/>
        /// in [−1, 1] lifts (1) or lowers (−1) the wing tips; the presenters bake a few flap frames and pick one per
        /// bird. Colour of the species' wing (tinted for pigeons), UV0 = (feather channel, AO 1).
        /// </summary>
        public static void PaperBird(FaunaSpecies species, float flap, MeshData dst)
        {
            dst.Clear();
            AvianSpec s = Spec(species);
            float half = 0.5f * s.Span, chord = s.Chord;
            float dihedral = FMath.Clamp(flap, -1f, 1f) * 0.75f;
            float tipY = (float)Math.Sin(dihedral) * half, tipX = (float)Math.Cos(dihedral) * half;
            uint top = s.Wing | 0xFFu;
            uint under = FMath.Shade(s.BodyTinted ? s.Belly : s.Wing | 0xFFu, 0.85f) | 0xFFu;
            if (!s.BodyTinted)
            {
                top &= 0xFFFFFF00u;
                under &= 0xFFFFFF00u;
            }
            float ch = (float)FaunaPalette.FeatherChannel;
            float zf = 0.5f * s.BodyLen + s.HeadLen + s.BillLen, zb = -0.5f * s.BodyLen - s.TailLen;
            for (int face = 0; face < 2; face++)
            {
                float ny = face == 0 ? 1f : -1f;
                uint col = face == 0 ? top : under;
                for (int side = -1; side <= 1; side += 2)
                {
                    var p0 = new Fv3(0f, 0f, zf);
                    var p1 = new Fv3(0f, 0f, zb);
                    var p2 = new Fv3(side * tipX, tipY, 0.15f * chord);
                    var p3 = new Fv3(side * tipX * 0.9f, tipY * 0.9f, -0.55f * chord);
                    Fv3 nn = Fv3.Cross(p3 - p0, p1 - p0).Normalized;
                    if (nn.Y * ny < 0f) nn = -nn;
                    int a = dst.AddVertex(p0.X, p0.Y, p0.Z, nn.X, nn.Y, nn.Z, col, ch, 1f);
                    int b = dst.AddVertex(p1.X, p1.Y, p1.Z, nn.X, nn.Y, nn.Z, col, ch, 1f);
                    int t0 = dst.AddVertex(p2.X, p2.Y, p2.Z, nn.X, nn.Y, nn.Z, col, ch, 1f);
                    int t1 = dst.AddVertex(p3.X, p3.Y, p3.Z, nn.X, nn.Y, nn.Z, col, ch, 1f);
                    // Front side along nn: cross(b − a, c − a) · nn > 0.
                    Tri(dst, a, b, t1, nn);
                    Tri(dst, a, t1, t0, nn);
                }
            }
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
