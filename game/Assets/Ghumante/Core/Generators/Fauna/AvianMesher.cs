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

        /// <summary>Colour of a cere (the bare swelling at the base of the bill: white on pigeons, yellow on kites), or 0.</summary>
        public uint Cere;

        /// <summary>Model separate toes on the near level (fowl and the long-legged egret); others get a foot pad.</summary>
        public bool Toes;

        /// <summary>Separate fingered primaries at the wing tips (soaring raptors), near level only.</summary>
        public int Fingers;

        /// <summary>Keep the folded wings on the mid level (fowl walk close by; small birds read without them).</summary>
        public bool FoldOnCoarse;

        /// <summary>A duck's bill: a broad, flat spatula about as wide at the rounded tip as at the base, its tip
        /// turned up a little and capped by a nail (<see cref="BillR"/> is then its half-width).</summary>
        public bool Spatula;

        /// <summary>How far (fraction of <see cref="BodyW"/>) the folded wing is pulled in towards the flank: 0 for the
        /// slim birds, more for the broad-breasted duck, whose wings lie flush along its sides.</summary>
        public float FoldInset;

        /// <summary>Thickness of the folded wing as a fraction of the default shell (0 = 1).</summary>
        public float FoldThin;

        /// <summary>Rings of the folded wing (0 = wrist at the shoulder, 1 = primary tips over the rump) and their colours:
        /// the pigeon's two black bars, the sparrow's white bar, the myna's white patch. Null: plain (wing, wing tip).</summary>
        public float[] FoldU;

        public uint[] FoldCol;

        public uint Body, Back, Belly, Head, Neck, Wing, WingTip, Tail, TailTip, Bill, BillTip, Leg, Iris;
        public bool BodyTinted;
    }

    /// <summary>
    /// The shared bird recipe: one smooth lofted tube from the rump through the egg-shaped body, the breast and the
    /// neck into the round head (no separate head or collar: the head flows into the neck as on a real pigeon), a bill,
    /// cartoon eyes with a pupil, a tail fan (forked or with streamers), folded wings lying along the body
    /// (<see cref="FaunaBone.FoldL"/>, shown at rest) and spread wings for flight (<see cref="FaunaBone.WingL"/> and
    /// <see cref="FaunaBone.WingTipL"/>, shown in the air; the poses scale the other set to zero), and legs with toes.
    /// The triangles go where they read: 8 segments round the body of a near bird (300 triangles in all for the
    /// flying birds, W2_DESIGN 5.6), a 5-sided loft without eyes or legs for the light bird (80). Returns landmarks the
    /// species add their extras to (iridescent neck bands, combs, wattles, sickle tails, hackles).
    /// </summary>
    internal static class AvianMesher
    {
        /// <summary>Landmarks of a built bird for the species extras.</summary>
        internal struct Landmarks
        {
            public Fv3 HeadC, BillBase, BillTip, NeckTop, NeckBase, TailBase, TailTip, Shoulder, BodyC;

            /// <summary>The outer surface of the left folded wing halfway along it (the duck's speculum sits there).</summary>
            public Fv3 FoldSide;
            public Fv3 Fwd, Up, NeckDir, BillDir;
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
            var billDir = new Fv3(0f, -s.BillDrop / Math.Max(1e-3f, s.BillLen) * 0.6f - 0.12f, 1f).Normalized;
            L.NeckBase = neckBase;
            L.NeckTop = neckTop;
            L.HeadC = headC;
            L.NeckDir = neckDir;
            L.BillDir = billDir;
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

            // Body, breast, neck and head: one loft, so the head grows out of the neck and the neck out of the breast.
            uint body = s.Body, head = s.Head, neck = s.Neck;
            float nr = s.NeckR, hr = s.HeadR;
            bool longNeck = s.NeckS > 0f;
            c.Begin();
            AddK(c, rear - fwd * (0.02f * s.BodyLen) + up * (0.05f * s.BodyH), 0.32f * s.BodyW, 0.3f * s.BodyH, 0.26f * s.BodyH, FaunaBone.Pelvis, body);
            if (d.Coarse)
            {
                AddK(c, bc, s.BodyW, 0.95f * s.BodyH, s.BodyH, FaunaBone.Pelvis, body);
                AddK(c, neckBase + neckDir * (0.45f * s.NeckLen), 1.25f * nr + 0.15f * s.BodyW, 1.25f * nr + 0.1f * s.BodyH, 1.25f * nr + 0.2f * s.BodyH,
                     FaunaBone.Neck, neck);
                AddK(c, headC + billDir * (0.15f * s.HeadLen), 0.92f * hr, hr, 0.85f * hr, FaunaBone.Head, head);
            }
            else
            {
                // An egg: widest a little ahead of the middle, tapering smoothly back to the rump.
                AddK(c, bc + fwd * (0.06f * s.BodyLen) + up * (0.01f * s.BodyH), s.BodyW, 0.9f * s.BodyH, s.BodyH, FaunaBone.Chest, body);
                // The full, rounded breast pushed forward and down (the pigeon's proud chest; the egret and the kite are slim).
                if (!longNeck && s.Fingers == 0)
                    AddK(c, bc + fwd * (0.33f * s.BodyLen) + up * (0.08f * s.BodyH), 0.86f * s.BodyW, 0.66f * s.BodyH, 0.9f * s.BodyH, FaunaBone.Chest, body);
                // Upper breast: the full crop that narrows into the neck.
                AddK(c, neckBase - neckDir * (0.05f * s.NeckLen) + fwd * (0.06f * s.BodyLen), 0.5f * (s.BodyW + 1.6f * nr), 0.5f * (0.55f * s.BodyH + 1.5f * nr),
                     0.5f * (0.8f * s.BodyH + 1.8f * nr), FaunaBone.Chest, FMath.LerpColour(body, neck, 0.5f));
                if (longNeck)
                {
                    // The egret's S: forward low, back high.
                    AddK(c, neckBase + neckDir * (0.35f * s.NeckLen) + new Fv3(0f, 0f, s.NeckS * s.NeckLen), 1.05f * nr, nr, nr, FaunaBone.Neck, neck);
                    AddK(c, neckBase + neckDir * (0.78f * s.NeckLen) - new Fv3(0f, 0f, 0.6f * s.NeckS * s.NeckLen), 0.95f * nr, 0.9f * nr, 0.9f * nr, FaunaBone.Neck,
                         neck);
                }
                else
                {
                    AddK(c, neckBase + neckDir * (0.55f * s.NeckLen), 1.05f * nr, nr, 1.05f * nr, FaunaBone.Neck, neck);
                }
                // The round head over the neck, then the face narrowing to the bill.
                AddK(c, headC - billDir * (0.05f * s.HeadLen), 0.92f * hr, hr, 0.86f * hr, FaunaBone.Head, head);
                AddK(c, headC + billDir * (0.62f * s.HeadLen) - up * (0.08f * hr), 0.5f * hr, 0.48f * hr, 0.45f * hr, FaunaBone.Head, head);
            }
            int iLoft = b.IndexCount;
            int vLoft = b.Tube(c.K, c.N, 1, d.BodySegs, up, feathers, FaunaPart.Body, 0.55f, d.Coarse ? 0.8f : 0.45f);
            int iLoftEnd = b.IndexCount;
            for (int v = vLoft; v < b.VertexCount; v++)
            {
                FaunaBone bone = b.BoneOf(v);
                if (bone == FaunaBone.Neck)
                {
                    b.Target.Part[v] = (byte)FaunaPart.Neck;
                    continue;
                }
                if (bone == FaunaBone.Head)
                {
                    b.Target.Part[v] = (byte)FaunaPart.Head;
                    continue;
                }
                // Body: darker back, paler belly.
                Fv3 n = b.NormalOf(v);
                float t = Fv3.Dot(n, up);
                uint col = b.ColourAt(v);
                col = t > 0f ? FMath.LerpColour(col, s.Back, FMath.SmoothStep(0.15f, 0.85f, t) * BodyShare(col, body))
                    : FMath.LerpColour(col, s.Belly, FMath.SmoothStep(-0.1f, -0.7f, t) * BodyShare(col, body));
                b.SetColour(v, col);
            }

            // Bill: a short tapered cone (hooked for the kite, flat and wide for the duck).
            Fv3 billBase = headC + billDir * (0.82f * s.HeadLen) + new Fv3(0f, -0.15f * hr - 0.3f * s.BillDrop, 0f);
            Fv3 billTip = billBase + new Fv3(0f, -s.BillDrop, s.BillLen);
            L.BillBase = billBase;
            L.BillTip = billTip;
            float flatW = 1f + s.BillFlat;
            float flatH = 1f - 0.45f * s.BillFlat;
            if (s.Spatula)
            {
                SpatulaBill(c, s, billBase, billTip);
            }
            else if (d.Coarse)
            {
                Fv3 mid = (billBase + billTip) * 0.5f;
                b.Ellipsoid(mid, (billTip - billBase).Normalized, Fv3.Up, s.BillR * flatW, s.BillR * flatH, 0.5f * Fv3.Distance(billBase, billTip) + s.BillR * 0.3f, 2,
                            3, FaunaBone.Head, s.Bill, FaunaPalette.KeratinChannel, FaunaPart.Beak);
            }
            else
            {
                c.Begin();
                AddK(c, billBase - (billTip - billBase).Normalized * (s.BillR * 0.8f), s.BillR * flatW, s.BillR * flatH, s.BillR * flatH, FaunaBone.Head,
                     s.Cere != 0 ? s.Cere : s.Bill);
                if (s.Cere != 0)
                {
                    // The bare cere swells over the base of the bill; the bill itself is dark from just ahead of it.
                    AddK(c, billBase + (billTip - billBase) * 0.2f + new Fv3(0f, 0.3f * s.BillR, 0f), 1.1f * s.BillR * flatW, 1.2f * s.BillR * flatH,
                         0.75f * s.BillR * flatH, FaunaBone.Head, FMath.LerpColour(s.Cere, s.Bill, 0.35f));
                }
                if (s.BillHook > 0f)
                {
                    AddK(c, billTip + new Fv3(0f, 0.2f * s.BillHook, -0.2f * s.BillHook), s.BillR * 0.45f, s.BillR * 0.45f, s.BillR * 0.4f, FaunaBone.Head,
                         FMath.LerpColour(s.Bill, s.BillTip, 0.5f));
                    AddK(c, billTip + new Fv3(0f, -s.BillHook, 0f), s.BillR * 0.15f, s.BillR * 0.15f, s.BillR * 0.15f, FaunaBone.Head, s.BillTip);
                }
                else
                {
                    AddK(c, billTip, s.BillR * 0.25f * flatW, s.BillR * 0.22f * flatH, s.BillR * 0.2f * flatH, FaunaBone.Head, s.BillTip);
                }
                b.Tube(c.K, c.N, 1, s.FoldOnCoarse ? Math.Max(4, d.SmallSegs) : 3 + (s.BillFlat > 0f ? 1 : 0), Fv3.Up, FaunaPalette.KeratinChannel, FaunaPart.Beak,
                       -1f, 0.5f);
            }

            // Eyes on the sides of the head: fowl get the full cartoon eye; the small birds an iris with a dark pupil
            // (the light bird has none: too small to see).
            if (!d.Coarse)
            {
                SideMark m = c.StartSide();
                Fv3 eo = new Fv3(-1f, 0.12f, 0.25f).Normalized;
                Fv3 ec = headC + new Fv3(-0.78f * hr, 0.08f * hr, 0.18f * s.HeadLen);
                if (d.Eyes >= 2)
                {
                    b.Eye(ec, s.EyeR, eo, Fv3.Up, FaunaBone.Head, s.Iris, FaunaPalette.EyeBlack, d.Eyes);
                }
                else
                {
                    // A round, slightly domed iris set flush in the side of the head, its centre painted as the pupil
                    // (one primitive: the pole vertex dark, fading to the iris colour at the first ring).
                    Fv3 at = ec - eo * (0.1f * s.EyeR);
                    int ve = b.VertexCount;
                    // Four vertices round, turned 45° so the outline is a soft square rather than a diamond.
                    Fv3 eyeUp = (Fv3.Up + Fv3.Cross(eo, Fv3.Up).Normalized).Normalized;
                    b.Ellipsoid(at, eo, eyeUp, s.EyeR, s.EyeR, 0.45f * s.EyeR, 4, 4, FaunaBone.Head, s.Iris, FaunaPalette.EyeChannel, FaunaPart.Eye);
                    for (int v = ve; v < b.VertexCount; v++)
                        if (Fv3.Dot(b.PositionOf(v) - at, eo) > 0.25f * s.EyeR) b.SetColour(v, FaunaPalette.EyeBlack);
                }
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
            if (!d.Coarse && s.TailLen > 0.12f)
                AddK(c, tailBase + tailDir * (0.5f * s.TailLen), s.TailW * 0.85f, s.TailW * 0.07f, s.TailW * 0.06f, FaunaBone.Tail0, s.Tail);
            AddK(c, tailTip, s.TailW * (1f - 0.6f * s.TailFork), s.TailW * 0.05f, s.TailW * 0.04f, FaunaBone.Tail0, FMath.LerpColour(s.Tail, s.TailTip, 0.8f));
            int vTail = b.Tube(c.K, c.N, 1, d.Coarse ? 3 : d.LimbSegs, up, feathers, FaunaPart.Tail, -1f, s.FoldOnCoarse || d.Coarse ? 0.15f : -1f);
            // The tip band (pigeons: a dark terminal band).
            for (int v = vTail; v < b.VertexCount; v++)
            {
                float along = Fv3.Dot(b.PositionOf(v) - tailBase, tailDir) / s.TailLen;
                if (along > 0.72f) b.SetColour(v, s.TailTip);
            }
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
                AddK(c, tailTip + new Fv3(-0.8f * s.TailW, -0.18f * s.Streamer, -s.Streamer), 0.0012f, 0.001f, 0.001f, FaunaBone.Tail0, s.TailTip);
                b.Tube(c.K, c.N, 1, 3, Fv3.Up, feathers, FaunaPart.Tail, -1f, 0.3f);
                c.EndSide(m);
            }

            // Folded wings lying along the body: the wrist at the shoulder, the primaries crossing over the rump; the
            // rings sit where the species' wing marks are.
            if (d.Fine || s.FoldOnCoarse)
            {
                SideMark m = c.StartSide();
                c.Begin();
                Fv3 f0 = shoulder + new Fv3(-(0.76f - s.FoldInset) * s.BodyW, -0.16f * s.BodyH, -0.04f * s.BodyLen);
                // Fowl, seen up close in the yards, fit their wings to the flank they lie on.
                var fit = new FlankFit(b, s.FoldOnCoarse ? iLoft : 0, s.FoldOnCoarse ? iLoftEnd : 0);
                if (s.FoldU != null && d.Fine)
                {
                    for (int k = 0; k < s.FoldU.Length; k++) FoldKnot(c, s, f0, fwd, s.FoldU[k], s.FoldCol[k], fit);
                }
                else
                {
                    FoldKnot(c, s, f0, fwd, 0f, s.Wing, fit);
                    FoldKnot(c, s, f0, fwd, 0.5f, s.Wing, fit);
                    if (d.Fine && s.FoldOnCoarse) FoldKnot(c, s, f0, fwd, 0.85f, FMath.LerpColour(s.Wing, s.WingTip, 0.6f), fit);
                    FoldKnot(c, s, f0, fwd, 1f, s.WingTip, fit);
                }
                Knot mid = c.K[Math.Min(1, c.N - 1)];
                L.FoldSide = new Fv3(mid.C.X - mid.A, mid.C.Y, mid.C.Z);
                // Small birds: three sides round (a shell with its ridge outwards, the two hidden faces against the body);
                // the fowl, seen up close in the yards, get the rounder section.
                b.Tube(c.K, c.N, 1, d.Coarse || !s.FoldOnCoarse ? 3 : d.LimbSegs, Fv3.Up, feathers, FaunaPart.Wing, s.FoldOnCoarse ? 0.35f : -1f, 0.3f);
                c.EndSide(m);
            }

            // Spread wings (flight): inner arm and the outer hand, a cambered plate; the kite's fingered primaries.
            {
                SideMark m = c.StartSide();
                float half = 0.5f * s.Span;
                float x0 = 0.45f * s.BodyW, x1 = 0.55f * s.BodyW + 0.42f * half, x2 = half;
                Fv3 sh = shoulder;
                c.Begin();
                AddK(c, sh + new Fv3(-x0, 0f, -0.15f * s.Chord), 0.5f * s.Chord, 0.022f * s.Chord + 0.004f, 0.01f * s.Chord + 0.002f, FaunaBone.WingL, s.Wing);
                if (!d.Coarse)
                {
                    AddK(c, sh + new Fv3(-(x0 + x1) * 0.5f, 0.01f * s.Span, -0.2f * s.Chord), 0.56f * s.Chord, 0.028f * s.Chord + 0.003f, 0.01f * s.Chord + 0.002f,
                         FaunaBone.WingL, s.Wing);
                    AddK(c, sh + new Fv3(-x1, 0.015f * s.Span, -0.22f * s.Chord), 0.48f * s.Chord, 0.022f * s.Chord + 0.003f, 0.008f * s.Chord + 0.002f,
                         FaunaBone.WingTipL, FMath.LerpColour(s.Wing, s.WingTip, 0.4f));
                }
                AddK(c, sh + new Fv3(-x2, 0.005f * s.Span, -0.36f * s.Chord), (s.Fingers > 0 ? 0.3f : 0.16f) * s.Chord, 0.012f * s.Chord + 0.002f,
                     0.006f * s.Chord + 0.002f, FaunaBone.WingTipL, s.WingTip);
                b.Tube(c.K, c.N, 1, d.Coarse ? 3 : d.LimbSegs, Fv3.Up, feathers, FaunaPart.Wing, -1f, 0.4f);
                if (s.Fingers > 0 && d.Fine)
                {
                    // Fingered primaries of the soaring kite.
                    int fingers = Math.Min(3, s.Fingers);
                    for (int k = 0; k < fingers; k++)
                    {
                        float u = k / (float)Math.Max(1, fingers - 1);
                        Fv3 r0 = sh + new Fv3(-x2 + 0.03f * s.Span, 0.005f * s.Span, -0.2f * s.Chord - 0.3f * s.Chord * u);
                        Fv3 r1 = r0 + new Fv3(-0.09f * s.Span * (1f - 0.3f * u), 0.002f * s.Span, -0.08f * s.Chord * u);
                        c.Begin();
                        AddK(c, r0, 0.07f * s.Chord, 0.006f, 0.003f, FaunaBone.WingTipL, s.WingTip);
                        AddK(c, r1, 0.025f * s.Chord, 0.003f, 0.002f, FaunaBone.WingTipL, s.WingTip);
                        b.Tube(c.K, c.N, 1, 3, Fv3.Up, feathers, FaunaPart.Wing, -1f, -1f);
                    }
                }
                c.EndSide(m);
            }

            // Legs: a bare tarsus (the feathered thigh of the long-legged birds above it) and toes, three forward and
            // the hind one where it shows. The lighter levels (the light bird, the mid duck and every far bird) keep a
            // plain stick leg from inside the body down to the ground, so no level ever hovers over the yard.
            bool longLegs = s.LegLen >= 0.08f;
            if (d.Fine || (longLegs && !d.Far))
            {
                SideMark m = c.StartSide();
                c.Begin();
                // The feathered thigh joins the long bare leg to the body (fowl, egret).
                if (longLegs)
                    AddK(c, new Fv3(-s.LegSpread, hipY + 0.2f * s.BodyH, 0.02f * s.BodyLen), s.LegR * 2.2f, s.LegR * 2.2f, s.LegR * 2.2f, FaunaBone.HindUpperL,
                         FMath.LerpColour(body, s.Leg, 0.2f));
                AddK(c, new Fv3(-s.LegSpread, hipY * (longLegs ? 0.62f : 0.95f), 0.0f), s.LegR * 1.1f, s.LegR, s.LegR, longLegs ? FaunaBone.HindLowerL : FaunaBone.HindUpperL,
                     s.Leg);
                AddK(c, new Fv3(-s.LegSpread * 1.05f, 0.008f, 0.008f), s.LegR * 0.95f, s.LegR * 0.9f, s.LegR * 0.9f, FaunaBone.HindFootL, s.Leg);
                b.Tube(c.K, c.N, 1, d.Fine && s.FoldOnCoarse ? Math.Max(3, d.LimbSegs - 1) : 3, Fv3.Forward, FaunaPalette.SkinChannel, FaunaPart.Leg, -1f,
                       -1f);
                var p0 = new Fv3(-s.LegSpread * 1.05f, 0.007f, 0.008f);
                if (d.Fine && s.LegLen >= 0.02f)
                {
                    int toes = s.Toes ? 4 : 3;
                    for (int t = 0; t < toes; t++)
                    {
                        float ang = t == 3 ? FMath.Pi : (t - 1) * 0.42f;
                        float len = t == 3 ? 0.5f * s.ToeLen : t == 1 ? s.ToeLen : 0.85f * s.ToeLen;
                        var dir = new Fv3((float)Math.Sin(ang), 0f, (float)Math.Cos(ang));
                        c.Begin();
                        AddK(c, p0, s.LegR * 0.7f, s.LegR * 0.6f, s.LegR * 0.6f, FaunaBone.HindFootL, s.Leg);
                        AddK(c, p0 + dir * len + new Fv3(0f, -0.004f, 0f), s.LegR * 0.42f, s.LegR * 0.4f, s.LegR * 0.35f, FaunaBone.HindFootL,
                             FMath.Shade(s.Leg, 0.85f));
                        b.Tube(c.K, c.N, 1, 3, Fv3.Up, FaunaPalette.SkinChannel, FaunaPart.Feet, -1f, s.FoldOnCoarse ? 0.5f : -1f);
                    }
                }
                c.EndSide(m);
            }
            else
            {
                StickLegs(c, s, hipY);
            }
            return L;
        }

        /// <summary>
        /// The lighter levels' legs: one 3-sided tapered tube per leg (6 triangles) from the hip, hidden in the body, to
        /// the ground (its open lower end lies flat on y = 0, where it cannot be seen), in the leg colour. The hip, knee
        /// and foot bones blend along it, so the walk and peck clips still move it.
        /// </summary>
        private static void StickLegs(FaunaSketch c, in AvianSpec s, float hipY)
        {
            SideMark m = c.StartSide();
            c.Begin();
            float r = Math.Max(s.LegR, 0.004f);
            AddK(c, new Fv3(-s.LegSpread, hipY + 0.1f * s.BodyH, 0.02f * s.BodyLen), 1.5f * r, 1.5f * r, 1.5f * r, FaunaBone.HindUpperL, s.Leg);
            AddK(c, new Fv3(-s.LegSpread * 1.05f, 0f, 0.008f), 1.1f * r, 1.1f * r, 1.1f * r, FaunaBone.HindFootL, s.Leg);
            c.B.Tube(c.K, c.N, 1, 3, Fv3.Forward, FaunaPalette.SkinChannel, FaunaPart.Leg, -1f, -1f);
            c.EndSide(m);
        }

        /// <summary>
        /// The duck's bill (ref_animals.md §6, Pekin and village ducks): a broad, flat spatula as long as the head, about
        /// as wide at the rounded tip as at the base with a slight waist, thick where it joins the face and thin at the
        /// end, the tip turned up a little and capped by a nail. Near level: a rounded-rectangle loft with a nail; mid:
        /// the same spatula with fewer sides; far: one flat oval of the same width.
        /// </summary>
        private static void SpatulaBill(FaunaSketch c, in AvianSpec s, Fv3 billBase, Fv3 billTip)
        {
            FaunaBuilder b = c.B;
            FaunaDetail d = c.D;
            Fv3 dir = (billTip - billBase).Normalized;
            Fv3 up = Fv3.Cross(Fv3.Cross(dir, Fv3.Up), dir).Normalized;
            if (up.Y < 0f) up = -up;
            float w = s.BillR, len = Fv3.Distance(billBase, billTip);
            uint nail = FMath.LerpColour(s.Bill, s.BillTip, 0.85f);
            if (d.Far)
            {
                // Far: one flat oval as wide as the spatula, rounded at the end.
                b.Ellipsoid((billBase + billTip) * 0.5f + up * (0.1f * w), dir, up, 0.95f * w, 0.4f * w, 0.5f * len + 0.3f * w, 2, 4, FaunaBone.Head, s.Bill,
                            FaunaPalette.KeratinChannel, FaunaPart.Beak);
                return;
            }
            c.Begin();
            // Root, sunk into the face: as tall as it is wide where the feathers meet it.
            c.K[c.N++] = Spk(billBase - dir * (0.3f * w) + up * (0.1f * w), 1.05f * w, 1f * w, 0.75f * w, s.Bill, 2.4f);
            if (d.Fine)
            {
                // A slight waist and the dip of the culmen, then the widest part before the tip.
                c.K[c.N++] = Spk(billBase + dir * (0.35f * len) - up * (0.02f * w), 0.92f * w, 0.56f * w, 0.46f * w, s.Bill, 3f);
                c.K[c.N++] = Spk(billBase + dir * (0.78f * len) + up * (0.02f * w), 1.02f * w, 0.32f * w, 0.28f * w, s.Bill, 3f);
            }
            else
            {
                c.K[c.N++] = Spk(billBase + dir * (0.6f * len), 0.96f * w, 0.44f * w, 0.36f * w, s.Bill, 3f);
            }
            // The rounded end, turned up a little.
            c.K[c.N++] = Spk(billTip + up * (0.12f * w), 0.72f * w, 0.24f * w, 0.2f * w, FMath.LerpColour(s.Bill, s.BillTip, 0.4f), 2.2f);
            b.Tube(c.K, c.N, 1, d.Fine ? 8 : 4, up, FaunaPalette.KeratinChannel, FaunaPart.Beak, -1f, 0.7f);
            if (d.Fine)
            {
                // The nail over the tip.
                b.Ellipsoid(billTip + up * (0.2f * w) - dir * (0.12f * w), dir, up, 0.36f * w, 0.1f * w, 0.26f * w, 2, 5, FaunaBone.Head, nail,
                            FaunaPalette.KeratinChannel, FaunaPart.Beak);
            }
        }

        private static Knot Spk(Fv3 p, float a, float bt, float bb, uint col, float e)
        {
            return new Knot(p.X, p.Y, p.Z, a, bt, bb, FaunaBone.Head, col, e);
        }

        /// <summary>A ring of the folded wing at <paramref name="u"/> of its length: full over the coverts, narrowing to
        /// the primary tips that cross over the rump.</summary>
        private static void FoldKnot(FaunaSketch c, in AvianSpec s, Fv3 f0, Fv3 fwd, float u, uint col, in FlankFit fit)
        {
            // A shell lying on the upper flank, following the body's contour back to the rump, where the primaries
            // converge and cross over the tail.
            float bump = 4f * u * (1f - u); // fullest in the middle
            float thin = s.FoldThin > 0f ? s.FoldThin : 1f;
            float a = (FMath.Lerp(0.26f, 0.08f, u * u) + 0.03f * bump) * s.BodyW * thin;
            float bt = (FMath.Lerp(0.3f, 0.06f, u) + 0.1f * bump) * s.BodyH;
            float bb = (FMath.Lerp(0.36f, 0.05f, u) + 0.14f * bump) * s.BodyH;
            float x = u < 0.55f ? -0.05f * s.BodyW * u / 0.55f : FMath.Lerp(-0.05f, 0.66f, FMath.SmoothStep((u - 0.55f) / 0.45f)) * s.BodyW;
            float y = (0.03f * bump + 0.05f * u * u) * s.BodyH;
            Fv3 p = f0 - fwd * (u * s.FoldLen) + new Fv3(x, y, 0f);
            if (fit.Active)
            {
                // Lie on the flank: the shell's inner half sunk into the body, its outer half standing proud by a
                // little less than half its thickness; over the narrowing rump the primaries close in towards the
                // midline (never crossing it).
                float flank = fit.HalfWidth(p.Y, p.Z);
                p = new Fv3(Math.Min(-(flank - 0.6f * a), -0.35f * a), p.Y, p.Z);
            }
            AddK(c, p, a, bt, bb, FaunaBone.FoldL, col);
        }

        /// <summary>
        /// The half-width of the body loft along a horizontal line across the bird: where a ray along +X at
        /// (<c>y</c>, <c>z</c>) first enters the loft's triangles (0 when it misses). Inactive for an empty range.
        /// </summary>
        private readonly struct FlankFit
        {
            private readonly MeshData _m;
            private readonly int _i0, _i1;

            public FlankFit(FaunaBuilder b, int i0, int i1)
            {
                _m = b.Target.Mesh;
                _i0 = i0;
                _i1 = i1;
            }

            public bool Active
            {
                get { return _i1 > _i0; }
            }

            public float HalfWidth(float y, float z)
            {
                float best = 0f;
                float[] pos = _m.Positions;
                int[] idx = _m.Indices;
                for (int i = _i0; i + 2 < _i1; i += 3)
                {
                    int a = 3 * idx[i], b = 3 * idx[i + 1], c = 3 * idx[i + 2];
                    float ya = pos[a + 1], za = pos[a + 2], yb = pos[b + 1], zb = pos[b + 2], yc = pos[c + 1], zc = pos[c + 2];
                    float det = (yb - ya) * (zc - za) - (yc - ya) * (zb - za);
                    if (Math.Abs(det) < 1e-12f) continue;
                    float l1 = ((y - ya) * (zc - za) - (yc - ya) * (z - za)) / det;
                    float l2 = ((yb - ya) * (z - za) - (y - ya) * (zb - za)) / det;
                    if (l1 < 0f || l2 < 0f || l1 + l2 > 1f) continue;
                    float xh = pos[a] + l1 * (pos[b] - pos[a]) + l2 * (pos[c] - pos[a]);
                    if (-xh > best) best = -xh;
                }
                return best;
            }
        }

        /// <summary>How much of a loft vertex's colour is the plain body colour (the back and belly shading applies to it,
        /// not to the neck blend at the crop).</summary>
        private static float BodyShare(uint col, uint body)
        {
            int dr = (int)((col >> 24) & 0xFF) - (int)((body >> 24) & 0xFF);
            int dg = (int)((col >> 16) & 0xFF) - (int)((body >> 16) & 0xFF);
            int db = (int)((col >> 8) & 0xFF) - (int)((body >> 8) & 0xFF);
            float diff = (Math.Abs(dr) + Math.Abs(dg) + Math.Abs(db)) / 120f;
            return FMath.Clamp01(1f - diff);
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
