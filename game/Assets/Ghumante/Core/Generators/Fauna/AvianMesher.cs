using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Generators.Fauna
{
    /// <summary>
    /// Proportions and colours of a bird (metres; standing bind pose). Filled per species by
    /// <see cref="BirdMesher"/> and <see cref="FowlMesher"/>.
    /// </summary>
    internal struct AvianSpec
    {
        public float BodyLen, BodyW, BodyH, BodyPitchDeg;
        public float NeckLen, NeckR, NeckLeanDeg, NeckS;
        public float HeadR, HeadLen;
        public float BillLen, BillR, BillHook, BillFlat, BillDrop;
        public float TailLen, TailW, TailFork, TailDropDeg, Streamer;
        public float LegLen, LegR, LegSpread, ToeLen;
        public float Span, Chord, FoldLen;
        public float EyeR;

        /// <summary>Model separate toes on the near level (fowl and the long-legged egret); others get a foot pad.</summary>
        public bool Toes;

        /// <summary>Separate fingered primaries at the wing tips (soaring raptors), near level only.</summary>
        public int Fingers;

        /// <summary>Keep the folded wings on the mid level (fowl walk close by; small birds read without them).</summary>
        public bool FoldOnCoarse;

        public uint Body, Back, Belly, Head, Neck, Wing, WingTip, Tail, TailTip, Bill, BillTip, Leg, Iris;
        public bool BodyTinted;
    }

    /// <summary>
    /// The shared bird recipe: an egg-shaped body tilted on its legs, a neck and round head with the bill, cartoon eyes,
    /// a flat tail fan (forked or with streamers), folded wings lying along the body (<see cref="FaunaBone.FoldL"/>,
    /// shown at rest) and spread wings for flight (<see cref="FaunaBone.WingL"/> and <see cref="FaunaBone.WingTipL"/>,
    /// shown in the air; the poses scale the other set to zero), and legs with three toes forward and one back. Returns
    /// landmarks the species add their extras to (combs, wattles, sickle tails, hackles).
    /// </summary>
    internal static class AvianMesher
    {
        /// <summary>Landmarks of a built bird for the species extras.</summary>
        internal struct Landmarks
        {
            public Fv3 HeadC, BillBase, BillTip, NeckTop, NeckBase, TailBase, TailTip, Shoulder, BodyC;
            public Fv3 Fwd, Up;
        }

        public static Landmarks Build(FaunaSketch c, in AvianSpec s, MaterialChannel feathers)
        {
            FaunaBuilder b = c.B;
            FaunaDetail d = c.D;
            var L = new Landmarks();
            float pitch = s.BodyPitchDeg * FMath.Deg;
            var fwd = new Fv3(0f, (float)Math.Sin(pitch), (float)Math.Cos(pitch));
            Fv3 up = Fv3.Cross(fwd, Fv3.Right).Normalized;
            if (up.Y < 0f) up = -up;
            float hipY = s.LegLen;
            var bc = new Fv3(0f, hipY + s.BodyH * 0.72f, 0f);
            L.BodyC = bc;
            L.Fwd = fwd;
            L.Up = up;
            Fv3 rear = bc - fwd * (0.5f * s.BodyLen);
            Fv3 front = bc + fwd * (0.5f * s.BodyLen);

            // Pivots.
            b.Pivot(FaunaBone.Root, Fv3.Zero, false);
            b.Pivot(FaunaBone.Pelvis, bc - fwd * (0.15f * s.BodyLen), false);
            b.Pivot(FaunaBone.Chest, bc + fwd * (0.12f * s.BodyLen), false);
            Fv3 neckBase = front - fwd * (0.15f * s.BodyLen) + up * (0.35f * s.BodyH);
            float lean = s.NeckLeanDeg * FMath.Deg;
            var neckDir = new Fv3(0f, (float)Math.Cos(lean), (float)Math.Sin(lean));
            Fv3 neckTop = neckBase + neckDir * s.NeckLen;
            Fv3 headC = neckTop + new Fv3(0f, s.HeadR * 0.35f, s.HeadR * 0.35f);
            L.NeckBase = neckBase;
            L.NeckTop = neckTop;
            L.HeadC = headC;
            b.Pivot(FaunaBone.Neck, neckBase, false);
            b.Pivot(FaunaBone.Head, neckTop, false);
            b.Pivot(FaunaBone.Jaw, headC + new Fv3(0f, -0.2f * s.HeadR, 0.8f * s.HeadR), false);
            Fv3 tailBase = rear + fwd * (0.1f * s.BodyLen) + up * (0.15f * s.BodyH);
            b.Pivot(FaunaBone.Tail0, tailBase, false);
            b.Pivot(FaunaBone.Tail1, tailBase, false);
            b.Pivot(FaunaBone.Tail2, tailBase, false);
            Fv3 shoulder = front - fwd * (0.18f * s.BodyLen) + up * (0.45f * s.BodyH);
            L.Shoulder = shoulder;
            b.Pivot(FaunaBone.WingL, shoulder + new Fv3(-0.55f * s.BodyW, 0f, 0f), true);
            b.Pivot(FaunaBone.WingTipL, shoulder + new Fv3(-0.55f * s.BodyW - 0.42f * s.Span * 0.5f, 0f, 0f), true);
            b.Pivot(FaunaBone.FoldL, shoulder + new Fv3(-0.8f * s.BodyW, 0f, 0f), true);
            var hip = new Fv3(-s.LegSpread, hipY + 0.25f * s.BodyH, 0.02f * s.BodyLen);
            b.Pivot(FaunaBone.HindUpperL, hip, true);
            b.Pivot(FaunaBone.HindLowerL, new Fv3(-s.LegSpread, hipY * 0.62f, 0.0f), true);
            b.Pivot(FaunaBone.HindFootL, new Fv3(-s.LegSpread, 0.012f, 0.01f), true);
            b.Pivot(FaunaBone.Rider, bc + up * s.BodyH, false);

            uint body = s.Body, back = s.Back, belly = s.Belly;
            // Body: an egg, fuller at the breast.
            c.Begin();
            AddK(c, rear - fwd * (0.02f * s.BodyLen), 0.35f * s.BodyW, 0.35f * s.BodyH, 0.3f * s.BodyH, FaunaBone.Pelvis, back);
            AddK(c, bc - fwd * (0.3f * s.BodyLen), 0.82f * s.BodyW, 0.75f * s.BodyH, 0.72f * s.BodyH, FaunaBone.Pelvis, body);
            AddK(c, bc, s.BodyW, 0.95f * s.BodyH, 0.95f * s.BodyH, FaunaBone.Pelvis, body);
            AddK(c, bc + fwd * (0.25f * s.BodyLen), 0.95f * s.BodyW, 0.9f * s.BodyH, s.BodyH, FaunaBone.Chest, body);
            AddK(c, front - fwd * (0.06f * s.BodyLen) + up * (0.12f * s.BodyH), 0.62f * s.BodyW, 0.62f * s.BodyH, 0.7f * s.BodyH, FaunaBone.Chest, body);
            AddK(c, front + up * (0.25f * s.BodyH), 0.3f * s.BodyW, 0.3f * s.BodyH, 0.3f * s.BodyH, FaunaBone.Chest, body);
            int vBody = c.Tube(d.BodySegs, d.Rings, FaunaPart.Body, feathers, up, 0.6f, 0.5f);
            for (int v = vBody; v < b.VertexCount; v++)
            {
                Fv3 n = b.NormalOf(v);
                float t = Fv3.Dot(n, up);
                uint col = t > 0f ? FMath.LerpColour(body, back, FMath.SmoothStep(0.1f, 0.8f, t)) : FMath.LerpColour(body, belly, FMath.SmoothStep(-0.1f, -0.7f, t));
                b.SetColour(v, col);
            }

            // Neck (an S on the egret) into the head.
            c.Begin();
            AddK(c, neckBase - neckDir * (0.25f * s.NeckLen) - fwd * (0.05f * s.BodyLen), s.NeckR * 1.5f, s.NeckR * 1.5f, s.NeckR * 1.5f, FaunaBone.Chest, s.Neck);
            AddK(c, neckBase + neckDir * (0.35f * s.NeckLen) + new Fv3(0f, 0f, s.NeckS * s.NeckLen), s.NeckR * 1.05f, s.NeckR, s.NeckR, FaunaBone.Neck, s.Neck);
            AddK(c, neckBase + neckDir * (0.75f * s.NeckLen) - new Fv3(0f, 0f, s.NeckS * 0.6f * s.NeckLen), s.NeckR * 0.95f, s.NeckR * 0.9f, s.NeckR * 0.9f, FaunaBone.Neck, s.Neck);
            AddK(c, neckTop, s.NeckR * 0.95f, s.NeckR * 0.95f, s.NeckR * 0.95f, FaunaBone.Head, s.Head);
            c.Tube(Math.Max(3, d.BodySegs - 2), d.Rings, FaunaPart.Neck, feathers, Fv3.Forward, -1f, -1f);
            c.B.Ellipsoid(headC, Fv3.Forward, Fv3.Up, s.HeadR * 0.92f, s.HeadR, s.HeadLen, d.Fine ? 3 : 2, d.BodySegs, FaunaBone.Head, s.Head,
                          feathers, FaunaPart.Head);

            // Bill: a tapered cone (hooked for the kite, flat and wide for the duck).
            Fv3 billBase = headC + new Fv3(0f, -0.15f * s.HeadR - s.BillDrop * 0.3f, s.HeadLen * 0.82f);
            Fv3 billTip = billBase + new Fv3(0f, -s.BillDrop, s.BillLen);
            L.BillBase = billBase;
            L.BillTip = billTip;
            float flatW = 1f + s.BillFlat;
            float flatH = 1f - 0.45f * s.BillFlat;
            if (d.Coarse)
            {
                // A single small cone-ish ellipsoid on the mid and far levels.
                Fv3 mid = (billBase + billTip) * 0.5f;
                b.Ellipsoid(mid, (billTip - billBase).Normalized, Fv3.Up, s.BillR * flatW, s.BillR * flatH, 0.5f * Fv3.Distance(billBase, billTip) + s.BillR * 0.3f, 2,
                            3, FaunaBone.Head, s.Bill, FaunaPalette.KeratinChannel, FaunaPart.Beak);
            }
            else
            {
                c.Begin();
                AddK(c, billBase - new Fv3(0f, 0f, s.BillR * 0.6f), s.BillR * flatW, s.BillR * flatH, s.BillR * flatH, FaunaBone.Head, s.Bill);
                AddK(c, billBase + (billTip - billBase) * 0.5f, s.BillR * 0.7f * flatW, s.BillR * 0.6f * flatH, s.BillR * 0.6f * flatH, FaunaBone.Head, FMath.LerpColour(s.Bill, s.BillTip, 0.4f));
                if (s.BillHook > 0f)
                {
                    AddK(c, billTip + new Fv3(0f, 0.2f * s.BillHook, -0.2f * s.BillHook), s.BillR * 0.4f, s.BillR * 0.4f, s.BillR * 0.4f, FaunaBone.Head, s.BillTip);
                    AddK(c, billTip + new Fv3(0f, -s.BillHook, 0f), s.BillR * 0.15f, s.BillR * 0.15f, s.BillR * 0.15f, FaunaBone.Head, s.BillTip);
                }
                else
                {
                    AddK(c, billTip, s.BillR * 0.22f * flatW, s.BillR * 0.2f * flatH, s.BillR * 0.2f * flatH, FaunaBone.Head, s.BillTip);
                }
                c.Tube(d.SmallSegs, 1, FaunaPart.Beak, FaunaPalette.KeratinChannel, Fv3.Up, 0.4f, 0.5f);
            }

            // Eyes on the sides of the head (none on the mid level: too small to see).
            if (!d.Coarse)
            {
                SideMark m = c.StartSide();
                Fv3 eo = new Fv3(-1f, 0.12f, 0.25f).Normalized;
                Fv3 ec = headC + new Fv3(-0.78f * s.HeadR, 0.18f * s.HeadR, 0.25f * s.HeadLen);
                // Birds' eyes read as a coloured dot at their size (fowl get the full eye with pupil and catch-light).
                b.Eye(ec, s.EyeR, eo, Fv3.Up, FaunaBone.Head, s.Iris, d.Eyes > 0 ? FaunaPalette.EyeBlack : s.Iris, d.Eyes);
                c.EndSide(m);
            }

            // Tail fan (fork or streamers).
            Fv3 tailDir = -fwd;
            float drop = s.TailDropDeg * FMath.Deg;
            tailDir = new Fv3(0f, tailDir.Y * (float)Math.Cos(drop) - (float)Math.Sin(drop) * 0.9f, tailDir.Z).Normalized;
            Fv3 tailTip = tailBase + tailDir * s.TailLen;
            L.TailBase = tailBase;
            L.TailTip = tailTip;
            c.Begin();
            AddK(c, tailBase + tailDir * (-0.1f * s.TailLen), s.TailW * 0.45f, s.TailW * 0.12f, s.TailW * 0.1f, FaunaBone.Tail0, s.Tail);
            AddK(c, tailBase + tailDir * (0.45f * s.TailLen), s.TailW * 0.8f, s.TailW * 0.07f, s.TailW * 0.06f, FaunaBone.Tail0, s.Tail);
            AddK(c, tailBase + tailDir * (0.85f * s.TailLen), s.TailW, s.TailW * 0.06f, s.TailW * 0.05f, FaunaBone.Tail0, FMath.LerpColour(s.Tail, s.TailTip, 0.6f));
            AddK(c, tailTip, s.TailW * (1f - 0.6f * s.TailFork), s.TailW * 0.05f, s.TailW * 0.04f, FaunaBone.Tail0, s.TailTip);
            int vTail = c.Tube(d.LimbSegs, 1, FaunaPart.Tail, feathers, up, -1f, 0.15f);
            if (s.TailFork > 0f)
            {
                // Pull the centre of the tail end forward: a shallow fork.
                for (int v = vTail; v < b.VertexCount; v++)
                {
                    Fv3 p = b.PositionOf(v);
                    float along = Fv3.Dot(p - tailBase, tailDir) / s.TailLen;
                    if (along < 0.7f) continue;
                    float k = 1f - Math.Min(1f, Math.Abs(p.X) / (0.6f * s.TailW));
                    Fv3 q = p - tailDir * (s.TailFork * s.TailLen * 0.35f * k * FMath.SmoothStep(0.7f, 1f, along));
                    SetPos(b, v, q);
                }
            }
            if (s.Streamer > 0f && !d.Coarse)
            {
                SideMark m = c.StartSide();
                c.Begin();
                AddK(c, tailTip + new Fv3(-0.6f * s.TailW, 0f, 0.15f * s.TailLen), 0.004f, 0.0015f, 0.0015f, FaunaBone.Tail0, s.TailTip);
                AddK(c, tailTip + new Fv3(-0.75f * s.TailW, -0.1f * s.Streamer, -0.6f * s.Streamer), 0.0025f, 0.0012f, 0.0012f, FaunaBone.Tail0, s.TailTip);
                AddK(c, tailTip + new Fv3(-0.8f * s.TailW, -0.18f * s.Streamer, -s.Streamer), 0.001f, 0.001f, 0.001f, FaunaBone.Tail0, s.TailTip);
                c.Tube(3, 1, FaunaPart.Tail, feathers, Fv3.Up, 0.3f, 0.3f);
                c.EndSide(m);
            }

            // Folded wings lying along the body.
            if (d.Fine || s.FoldOnCoarse)
            {
                SideMark m = c.StartSide();
                c.Begin();
                // Lying flat along the flank: the wrist at the shoulder, the primaries crossing over the rump.
                Fv3 f0 = shoulder + new Fv3(-0.62f * s.BodyW, -0.08f * s.BodyH, -0.02f * s.BodyLen);
                Fv3 f1 = f0 - fwd * (0.45f * s.FoldLen) + new Fv3(-0.08f * s.BodyW, -0.12f * s.BodyH, 0f);
                Fv3 f2 = f0 - fwd * (0.85f * s.FoldLen) + new Fv3(0.25f * s.BodyW, -0.02f * s.BodyH, 0f);
                Fv3 f3 = f0 - fwd * s.FoldLen + new Fv3(0.5f * s.BodyW, 0.04f * s.BodyH, 0f);
                AddK(c, f0, 0.16f * s.BodyW, 0.45f * s.BodyH, 0.35f * s.BodyH, FaunaBone.FoldL, s.Wing);
                AddK(c, f1, 0.13f * s.BodyW, 0.55f * s.BodyH, 0.35f * s.BodyH, FaunaBone.FoldL, s.Wing);
                AddK(c, f2, 0.09f * s.BodyW, 0.3f * s.BodyH, 0.2f * s.BodyH, FaunaBone.FoldL, FMath.LerpColour(s.Wing, s.WingTip, 0.6f));
                AddK(c, f3, 0.04f * s.BodyW, 0.08f * s.BodyH, 0.06f * s.BodyH, FaunaBone.FoldL, s.WingTip);
                c.Tube(d.LimbSegs, 1, FaunaPart.Wing, feathers, Fv3.Up, 0.4f, 0.3f);
                c.EndSide(m);
            }

            // Spread wings (flight): inner arm and the outer hand with its primaries, a cambered plate.
            {
                SideMark m = c.StartSide();
                float half = 0.5f * s.Span;
                float x0 = 0.45f * s.BodyW, x1 = 0.55f * s.BodyW + 0.42f * half, x2 = half;
                Fv3 sh = shoulder;
                c.Begin();
                AddK(c, sh + new Fv3(-x0, 0f, -0.15f * s.Chord), 0.5f * s.Chord, 0.022f * s.Chord + 0.004f, 0.01f * s.Chord + 0.002f, FaunaBone.WingL, s.Wing);
                AddK(c, sh + new Fv3(-(x0 + x1) * 0.5f, 0.01f * s.Span, -0.2f * s.Chord), 0.55f * s.Chord, 0.028f * s.Chord + 0.003f, 0.01f * s.Chord + 0.002f, FaunaBone.WingL, s.Wing);
                AddK(c, sh + new Fv3(-x1, 0.015f * s.Span, -0.2f * s.Chord), 0.5f * s.Chord, 0.022f * s.Chord + 0.003f, 0.008f * s.Chord + 0.002f, FaunaBone.WingTipL, FMath.LerpColour(s.Wing, s.WingTip, 0.4f));
                AddK(c, sh + new Fv3(-(x1 + x2) * 0.5f, 0.012f * s.Span, -0.25f * s.Chord), 0.38f * s.Chord, 0.016f * s.Chord + 0.002f, 0.006f * s.Chord + 0.0015f, FaunaBone.WingTipL, s.WingTip);
                AddK(c, sh + new Fv3(-x2, 0.005f * s.Span, -0.38f * s.Chord), 0.14f * s.Chord, 0.012f * s.Chord + 0.002f, 0.006f * s.Chord + 0.002f, FaunaBone.WingTipL, s.WingTip);
                c.Tube(d.LimbSegs, 1, FaunaPart.Wing, feathers, Fv3.Up, -1f, 0.4f);
                if (s.Fingers > 0 && d.Fine)
                {
                    // Fingered primaries of the soaring kite.
                    for (int k = 0; k < s.Fingers; k++)
                    {
                        float u = k / (float)(s.Fingers - 1);
                        Fv3 r0 = sh + new Fv3(-(x1 + x2) * 0.5f - 0.05f * s.Span, 0.01f * s.Span, -0.05f * s.Chord - 0.35f * s.Chord * u);
                        Fv3 r1 = r0 + new Fv3(-0.16f * s.Span * (1f - 0.35f * u), 0.004f * s.Span, -0.12f * s.Chord * u);
                        c.Begin();
                        AddK(c, r0, 0.07f * s.Chord, 0.006f, 0.003f, FaunaBone.WingTipL, s.WingTip);
                        AddK(c, r1, 0.03f * s.Chord, 0.003f, 0.002f, FaunaBone.WingTipL, s.WingTip);
                        c.Tube(3, 1, FaunaPart.Wing, feathers, Fv3.Up, 0.3f, 0.5f);
                    }
                }
                c.EndSide(m);
            }

            // Legs: thigh feathered into the body, a bare tarsus, three toes forward and one back (fowl, egret) or a
            // foot pad; dropped on the far level and for short-legged birds on the mid level.
            if (!d.Far && (d.Fine || s.LegLen >= 0.08f))
            {
                SideMark m = c.StartSide();
                c.Begin();
                AddK(c, new Fv3(-s.LegSpread, hipY + 0.3f * s.BodyH, 0.02f * s.BodyLen), s.LegR * 2.4f, s.LegR * 2.4f, s.LegR * 2.4f, FaunaBone.HindUpperL, body);
                AddK(c, new Fv3(-s.LegSpread, hipY * 0.9f, 0.0f), s.LegR * 1.6f, s.LegR * 1.6f, s.LegR * 1.6f, FaunaBone.HindUpperL, FMath.LerpColour(body, s.Leg, 0.5f));
                AddK(c, new Fv3(-s.LegSpread, hipY * 0.62f, 0.0f), s.LegR * 1.05f, s.LegR, s.LegR, FaunaBone.HindLowerL, s.Leg);
                AddK(c, new Fv3(-s.LegSpread * 1.05f, 0.006f, 0.008f), s.LegR * 0.95f, s.LegR * 0.9f, s.LegR * 0.9f, FaunaBone.HindFootL, s.Leg);
                c.Tube(Math.Max(3, d.LimbSegs - 1), 1, FaunaPart.Leg, FaunaPalette.SkinChannel, Fv3.Forward, 0.5f, 0.5f);
                var p0 = new Fv3(-s.LegSpread * 1.05f, 0.008f, 0.008f);
                if (d.Fine && s.Toes)
                {
                    for (int t = 0; t < 4; t++)
                    {
                        float ang = t == 3 ? FMath.Pi : (t - 1) * 0.42f;
                        float len = t == 3 ? 0.55f * s.ToeLen : s.ToeLen;
                        var dir = new Fv3((float)Math.Sin(ang), 0f, (float)Math.Cos(ang));
                        c.Begin();
                        AddK(c, p0, s.LegR * 0.7f, s.LegR * 0.6f, s.LegR * 0.6f, FaunaBone.HindFootL, s.Leg);
                        AddK(c, p0 + dir * len + new Fv3(0f, -0.003f, 0f), s.LegR * 0.4f, s.LegR * 0.4f, s.LegR * 0.35f, FaunaBone.HindFootL, s.Leg);
                        c.Tube(3, 1, FaunaPart.Feet, FaunaPalette.SkinChannel, Fv3.Up, 0.4f, 0.5f);
                    }
                }
                else if (d.Fine)
                {
                    b.Ellipsoid(p0 + new Fv3(0f, -0.004f, 0.25f * s.ToeLen), Fv3.Forward, Fv3.Up, 0.3f * s.ToeLen, Math.Max(0.002f, 0.8f * s.LegR), 0.45f * s.ToeLen, 2,
                                4, FaunaBone.HindFootL, s.Leg, FaunaPalette.SkinChannel, FaunaPart.Feet);
                }
                c.EndSide(m);
            }
            return L;
        }

        private static void AddK(FaunaSketch c, Fv3 p, float a, float bt, float bb, FaunaBone bone, uint col)
        {
            c.K[c.N++] = new Knot(p.X, p.Y, p.Z, a, bt, bb, bone, col);
        }

        internal static void SetPos(FaunaBuilder b, int v, Fv3 p)
        {
            float[] pos = b.Target.Mesh.Positions;
            pos[3 * v] = p.X;
            pos[3 * v + 1] = p.Y;
            pos[3 * v + 2] = p.Z;
        }

        /// <summary>Webbed feet for ducks: a flat triangle fan between the toes (near and mid levels).</summary>
        internal static void WebbedFeet(FaunaSketch c, in AvianSpec s)
        {
            if (c.D.Far) return;
            SideMark m = c.StartSide();
            var p0 = new Fv3(-s.LegSpread * 1.05f, 0.006f, 0.008f);
            c.B.Ellipsoid(p0 + new Fv3(0f, 0f, 0.55f * s.ToeLen), Fv3.Forward, Fv3.Up, 0.55f * s.ToeLen, 0.004f, 0.55f * s.ToeLen, 3, c.D.SmallSegs + 2,
                          FaunaBone.HindFootL, s.Leg, FaunaPalette.SkinChannel, FaunaPart.Feet);
            c.EndSide(m);
        }
    }
}
