using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Generators.Flora
{
    /// <summary>Resolution of one flora LOD (W2_DESIGN 5.8 bands): LOD0 is the detailed near tree (≤ 1,600
    /// triangles: 80-triangle clumps with a fringe of leaf cards), LOD1 the simple mid tree (≤ 240: 20-triangle clumps).</summary>
    internal readonly struct FloraDetail
    {
        public readonly int Lod;

        /// <summary>Puff resolution (<see cref="FloraBuilder.Puff"/>: 1 = 80 triangles, 0 = 20).</summary>
        public readonly int PuffRes;

        public readonly int TrunkSides, TrunkSegs, LimbSides, LimbSegs;

        private FloraDetail(int lod, int puffRes, int trunkSides, int trunkSegs, int limbSides, int limbSegs)
        {
            Lod = lod;
            PuffRes = puffRes;
            TrunkSides = trunkSides;
            TrunkSegs = trunkSegs;
            LimbSides = limbSides;
            LimbSegs = limbSegs;
        }

        public static FloraDetail For(int lod)
        {
            return lod <= 0 ? new FloraDetail(0, 1, 8, 6, 5, 3) : new FloraDetail(1, 0, 5, 2, 4, 1);
        }

        public bool Near
        {
            get { return Lod == 0; }
        }
    }

    /// <summary>
    /// A rounded broadleaf tree described by numbers (W2_DESIGN 5.8 shapes; docs/research/w2/ref_nature.md per
    /// species): a trunk with buttress flutes and lean, limbs that fork from it and run into the lower clumps, twigs,
    /// and a crown of lumpy foliage clumps with a fringe of leaves, either on an ellipsoid shell (round crowns) or in
    /// two rings and a top (umbrella crowns: a wide lower ring, an inner upper ring), layered: flatter, wider clumps
    /// low, rounder ones on top, with gaps that show the limbs. Sizes are metres at the catalogue's model size.
    /// </summary>
    internal struct BroadleafForm
    {
        public float H, W;

        // Trunk.
        public float TrunkR, ForkFrac, LeanM, LobeDepth, TrunkTopR;
        public int Lobes, Stems;
        public FloraBuilder.TubeStyle Bark;

        // Limbs (counts at LOD0; LOD1 keeps three).
        public int Limbs;
        public float LimbR;
        public bool Twigs;

        // Crown: centre height and radii as fractions of H and W; umbrella crowns use rings.
        public float CrownY, CrownRx, CrownRy;
        public bool Umbrella;
        public int Clumps;
        public float ClumpR, ClumpFlat, Lumps;

        /// <summary>Leaf cards per clump at LOD0 (the leafy fringe) and how much they hang (0 up and out, 1 down:
        /// weeping bottlebrush, drooping eucalyptus).</summary>
        public int Fringe;

        public float FringeDroop;

        /// <summary>Colour of the fringe leaves (0: the clump's): the coppery flush of new leaves at the branch tips.</summary>
        public uint FringeRgb;

        /// <summary>How far out the clumps sit on the crown shell (fraction of the crown radius): about 0.68 for
        /// dense crowns, up to 0.9 for open ones (eucalyptus) where sky shows between them.</summary>
        public float Shell;

        public uint Leaf, LeafTop;

        public static BroadleafForm Default(float h, float w, uint leaf, uint bark)
        {
            return new BroadleafForm
            {
                H = h, W = w, TrunkR = 0.03f * h, ForkFrac = 0.36f, LeanM = 0.03f * h, LobeDepth = 0.25f, TrunkTopR = 0.55f, Lobes = 4, Stems = 1,
                Bark = FloraBuilder.TubeStyle.Bark(bark), Limbs = 5, LimbR = 0.5f, Twigs = true, CrownY = 0.64f, CrownRx = 0.5f, CrownRy = 0.32f,
                Clumps = 10, ClumpR = 0.3f, ClumpFlat = 0.82f, Lumps = 0.2f, Fringe = 6, Shell = 0.68f, Leaf = leaf,
            };
        }
    }

    /// <summary>
    /// The shared tree-building steps (trunk, limbs, twigs, clump shells, leaf fringes, blooms) every species recipe
    /// in <see cref="TreeForms"/> composes. Holds the clump and limb-tip positions of the tree being built so blooms
    /// can sit on the clumps' outer surfaces. Deterministic for a seed; one instance per build.
    /// </summary>
    internal sealed class TreeKit
    {
        public readonly FloraBuilder B;
        public readonly FloraDetail D;
        public FloraRng Rng;
        public readonly uint Seed;

        /// <summary>Foliage clumps of the current tree: centre, radii and yaw (for blooms).</summary>
        public readonly Vec3[] ClumpC = new Vec3[96], ClumpR = new Vec3[96];

        public readonly float[] ClumpYaw = new float[96];

        public int ClumpCount;

        /// <summary>Limb tips of the current tree.</summary>
        public readonly Vec3[] Tips = new Vec3[32];

        public int TipCount;

        /// <summary>The crown colour as seen from afar (leaf mixed with the bloom; 0 out of bloom): the mid-distance
        /// tree shows its bloom as colour instead of flower geometry.</summary>
        public uint FarRgb;

        /// <summary>Crown centre and mean radius of the current tree.</summary>
        public Vec3 CrownC;

        public float CrownRad;

        public TreeKit(FloraBuilder b, FloraDetail d, uint seed)
        {
            B = b;
            D = d;
            Seed = seed;
            Rng = new FloraRng(seed, 0x54524545);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Trunk and limbs

        /// <summary>Point at fraction <paramref name="t"/> up a trunk of height <paramref name="h"/> leaning by
        /// <paramref name="lean"/> (quadratic: straight at the foot, bending toward the top) with a gentle S wiggle.</summary>
        public static Vec3 TrunkPoint(Vec3 foot, float h, Vec3 lean, float t, float wiggle, uint seed)
        {
            float wx = wiggle * FloraNoise.Value(t * 2.3, 0.5, seed), wz = wiggle * FloraNoise.Value(t * 2.3, 4.5, seed ^ 0x1234u);
            return new Vec3(foot.X + lean.X * t * t + wx * t, foot.Y + h * t, foot.Z + lean.Z * t * t + wz * t);
        }

        /// <summary>A trunk from <paramref name="foot"/> up <paramref name="h"/> metres, radius <paramref name="r0"/>
        /// tapering to <paramref name="r1"/>, with buttress flutes, a flared foot sunk below the ground and ground AO.</summary>
        public void Trunk(Vec3 foot, float h, float r0, float r1, Vec3 lean, int lobes, float lobeDepth, in FloraBuilder.TubeStyle st, float wiggle, bool cap = false)
        {
            int segs = D.TrunkSegs;
            Vec3[] p = FloraBuilder.Pts(segs + 2);
            float[] r = FloraBuilder.Radii(segs + 2);
            p[0] = foot - new Vec3(0f, Math.Min(0.25f, 0.04f * h), 0f);
            r[0] = r0 * 1.25f;
            for (int i = 1; i <= segs + 1; i++)
            {
                float t = (float)(i - 1) / segs;
                p[i] = TrunkPoint(foot, h, lean, t, wiggle, Seed);
                float flare = t < 0.12f ? 1f + 0.35f * (1f - t / 0.12f) : 1f;
                r[i] = (r0 + (r1 - r0) * (float)Math.Pow(t, 0.8)) * flare;
            }
            B.Tube(p, r, segs + 2, D.TrunkSides, st, 0.45f, 0.85f, cap, D.Near ? lobes : 0, lobeDepth, Seed);
        }

        /// <summary>A limb from <paramref name="a"/> to <paramref name="b"/>, rising in the middle by
        /// <paramref name="rise"/> (an upward arc), radius <paramref name="ra"/> to <paramref name="rb"/>.</summary>
        public void Limb(Vec3 a, Vec3 b, float ra, float rb, float rise, in FloraBuilder.TubeStyle st, float aoA = 0.7f, float aoB = 0.6f, int segs = -1, int sides = -1)
        {
            if (segs < 0) segs = D.LimbSegs;
            if (sides < 0) sides = D.LimbSides;
            Vec3[] p = FloraBuilder.Pts(segs + 1);
            float[] r = FloraBuilder.Radii(segs + 1);
            for (int i = 0; i <= segs; i++)
            {
                float f = (float)i / segs;
                p[i] = Vec3.Lerp(a, b, f) + Vec3.Up * (rise * 4f * f * (1f - f));
                r[i] = ra + (rb - ra) * f;
            }
            B.Tube(p, r, segs + 1, sides, st, aoA, aoB, false, 0, 0f, Seed ^ (uint)(a.X * 100) ^ (uint)(b.Z * 37));
        }

        // ---------------------------------------------------------------------------------------------------------
        // Clumps

        /// <summary>Add a foliage clump (remembered for blooms) with <paramref name="fringe"/> leaf cards at LOD0.</summary>
        public void Clump(Vec3 c, Vec3 r, float lumps, in FloraBuilder.PuffStyle st, int fringe = 0, int res = -1, float droop = 0f, float leafLen = 0.48f, float leafWide = 0.66f, uint fringeRgb = 0)
        {
            uint s = Seed ^ (uint)(ClumpCount * 7919 + 13);
            float yaw = Rng.Range(0f, 6.283f);
            B.Puff(c, r, res < 0 ? D.PuffRes : res, lumps, s, st, yaw);
            if (ClumpCount < ClumpC.Length)
            {
                ClumpC[ClumpCount] = c;
                ClumpR[ClumpCount] = r;
                ClumpYaw[ClumpCount++] = yaw;
            }
            if (!D.Near || fringe <= 0) return;
            float mean = (r.X + r.Y + r.Z) / 3f;
            Vec3 away = (c - st.CrownCentre);
            away = away.Length > 1e-3f ? away.Normalized : Vec3.Up;
            uint top = st.TopRgb != 0 ? st.TopRgb : st.Rgb;
            for (int i = 0; i < fringe; i++)
            {
                // Upper and outer part of the clump (toward the crown's outside), leaves leaning up and out.
                Vec3 d = Vec3.Zero;
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    double a = Rng.Range(0f, 6.283f);
                    float y = Rng.Range(-0.35f, 0.7f);
                    float rr = (float)Math.Sqrt(1.0 - y * y);
                    d = new Vec3((float)Math.Cos(a) * rr, y, (float)Math.Sin(a) * rr);
                    if (Vec3.Dot(d, away) > -0.1f) break;
                }
                Vec3 p = c + new Vec3(d.X * r.X, d.Y * r.Y, d.Z * r.Z) * 0.92f;
                Vec3 axis = (d + Vec3.Up * (0.35f - 1.4f * droop) + new Vec3(Rng.Jitter(0.3f), 0f, Rng.Jitter(0.3f))).Normalized;
                float len = mean * leafLen * Rng.Range(0.8f, 1.15f);
                uint rgb = fringeRgb != 0 ? FloraBuilder.Mix(fringeRgb, top, 0.15f) : FloraBuilder.Mix(st.Rgb, top, 0.25f + 0.45f * Math.Max(0f, d.Y));
                uint rgba = st.Tinted ? FloraBuilder.Leaf(rgb, Rng.Range(0.92f, 1.05f)) : FloraBuilder.Fixed(rgb, Rng.Range(0.92f, 1.05f));
                B.LeafCard(p, axis, d, len, len * leafWide, rgba, 0.75f + 0.25f * Math.Max(0f, d.Y));
            }
        }

        /// <summary>The style of the current crown's clumps.</summary>
        public FloraBuilder.PuffStyle CrownStyle(uint leaf, uint top)
        {
            var st = FloraBuilder.PuffStyle.Foliage(leaf, CrownC, Math.Max(CrownRad, 0.5f));
            st.TopRgb = top;
            return st;
        }

        /// <summary>
        /// A broadleaf tree from <paramref name="f"/>: trunk (or several stems), limbs running into the lower clumps,
        /// twigs and a layered crown of clumps with leaf fringes. <paramref name="clumpRgb"/> may recolour clump k
        /// (bloom). Fills <see cref="ClumpC"/> and <see cref="Tips"/>.
        /// </summary>
        public void Broadleaf(in BroadleafForm f, Func<int, int, uint> clumpRgb = null)
        {
            ClumpCount = 0;
            TipCount = 0;
            float H = f.H, rx = f.CrownRx * f.W, ry = f.CrownRy * H;
            var crown = new Vec3(0f, f.CrownY * H, 0f);
            CrownC = crown;
            CrownRad = 0.5f * (rx + ry);
            double leanA = Rng.Range(0f, 6.283f);
            var leanV = new Vec3((float)Math.Cos(leanA) * f.LeanM, 0f, (float)Math.Sin(leanA) * f.LeanM);
            float forkH = f.ForkFrac * H;

            if (!D.Near)
            {
                SimpleCrown(f, crown, rx, ry, leanV, forkH, clumpRgb);
                return;
            }

            // 1. Clump centres (layered), computed first so the limbs can run into them.
            int clumps = D.Near ? f.Clumps : Math.Max(4, Math.Min(6, f.Clumps / 2 + 1));
            float cr = f.ClumpR * rx * (D.Near ? 1f : 1.32f);
            var centres = new Vec3[clumps];
            var sizes = new Vec3[clumps];
            double b0 = Rng.Range(0f, 6.283f);
            if (f.Umbrella)
            {
                int outer = Math.Max(3, (int)Math.Round(clumps * 0.55)), inner = Math.Max(1, clumps - outer - 1);
                for (int k = 0; k < clumps; k++)
                {
                    Vec3 c;
                    float size, flat;
                    if (k < outer)
                    {
                        double a = b0 + k * 2 * Math.PI / outer + Rng.Jitter(0.25f);
                        float rad = rx * Rng.Range(0.62f, 0.74f);
                        c = crown + new Vec3((float)Math.Cos(a) * rad, -0.12f * ry + Rng.Jitter(0.12f) * ry, (float)Math.Sin(a) * rad);
                        size = cr * Rng.Range(0.95f, 1.12f);
                        flat = f.ClumpFlat * 0.85f;
                    }
                    else if (k < outer + inner)
                    {
                        double a = b0 + 0.5 + (k - outer) * 2 * Math.PI / inner + Rng.Jitter(0.3f);
                        float rad = rx * Rng.Range(0.28f, 0.4f);
                        c = crown + new Vec3((float)Math.Cos(a) * rad, 0.32f * ry + Rng.Jitter(0.1f) * ry, (float)Math.Sin(a) * rad);
                        size = cr * Rng.Range(1f, 1.15f);
                        flat = f.ClumpFlat;
                    }
                    else
                    {
                        c = crown + new Vec3(Rng.Jitter(0.1f) * rx, 0.52f * ry, Rng.Jitter(0.1f) * rx);
                        size = cr * 1.05f;
                        flat = f.ClumpFlat * 1.05f;
                    }
                    centres[k] = c;
                    sizes[k] = new Vec3(size, size * flat, size * Rng.Range(0.9f, 1.1f));
                }
            }
            else
            {
                for (int k = 0; k < clumps; k++)
                {
                    // Golden-angle spiral over the shell from the top down to below the equator.
                    float yy = 0.95f - 1.55f * (k + 0.5f) / clumps;
                    double a = k * 2.39996 + b0;
                    float rr = (float)Math.Sqrt(Math.Max(0.0, 1.0 - yy * yy));
                    float shell = f.Shell * Rng.Range(0.92f, 1.08f);
                    Vec3 c = crown + new Vec3((float)Math.Cos(a) * rr * rx * shell, yy * ry * shell, (float)Math.Sin(a) * rr * rx * shell);
                    float u = (yy + 1f) * 0.5f;
                    float size = cr * Rng.Range(0.88f, 1.12f) * (0.9f + 0.2f * (1f - Math.Abs(u - 0.6f)));
                    float flat = f.ClumpFlat * (0.82f + 0.3f * u);
                    centres[k] = c;
                    sizes[k] = new Vec3(size, size * flat, size * Rng.Range(0.9f, 1.1f));
                }
            }

            // 2. Stems: one trunk, or several leaning out from a shared foot (rhododendron, bottlebrush, bar).
            int stems = Math.Max(1, D.Near ? f.Stems : 1);
            var stemTops = new Vec3[stems];
            for (int s = 0; s < stems; s++)
            {
                Vec3 foot = Vec3.Zero, lv = leanV;
                float r0 = f.TrunkR, hS = forkH;
                if (stems > 1)
                {
                    double a = leanA + s * 2 * Math.PI / stems + Rng.Jitter(0.4f);
                    float spread = f.TrunkR * 0.9f;
                    foot = new Vec3((float)Math.Cos(a) * spread, 0f, (float)Math.Sin(a) * spread);
                    lv = new Vec3((float)Math.Cos(a) * (0.1f * H), 0f, (float)Math.Sin(a) * (0.1f * H));
                    r0 = f.TrunkR * (s == 0 ? 0.85f : 0.65f);
                    hS = forkH * (s == 0 ? 1.1f : Rng.Range(0.75f, 1f));
                }
                Trunk(foot, hS, r0, r0 * f.TrunkTopR, lv, s == 0 ? f.Lobes : 0, f.LobeDepth, f.Bark, 0.04f * H);
                stemTops[s] = TrunkPoint(foot, hS, lv, 1f, 0.04f * H, Seed);
            }

            // 3. Limbs from the stem tops into the lowest, outermost clumps (visible under the crown).
            int limbs = Math.Min(clumps, D.Near ? f.Limbs : Math.Min(3, f.Limbs));
            float limbR0 = f.TrunkR * f.TrunkTopR * f.LimbR * 1.6f;
            var order = new int[clumps];
            var key = new float[clumps];
            for (int k = 0; k < clumps; k++)
            {
                order[k] = k;
                Vec3 d = centres[k] - crown;
                key[k] = d.Y - 0.6f * (float)Math.Sqrt(d.X * d.X + d.Z * d.Z); // low and wide first
            }
            Array.Sort(key, order);
            // Spread the chosen limbs round the trunk: take every clump in turn, skipping ones too close in bearing.
            var used = new bool[clumps];
            for (int i = 0, picked = 0; i < clumps && picked < limbs; i++)
            {
                int k = order[i];
                Vec3 target = centres[k] - new Vec3(0f, 0.25f * sizes[k].Y, 0f);
                Vec3 from = stemTops[picked % stems] - new Vec3(0f, Rng.Range(0f, 0.1f) * forkH, 0f);
                Limb(from, target, limbR0, limbR0 * 0.4f, 0.06f * (target - from).Length, f.Bark, 0.7f, 0.55f);
                used[k] = true;
                if (TipCount < Tips.Length) Tips[TipCount++] = target;
                picked++;
                if (D.Near && f.Twigs)
                {
                    // Twigs fork off the limb toward the neighbouring clumps (seen in the gaps between clumps).
                    for (int q = 0; q < clumps; q++)
                    {
                        if (q == k || used[q]) continue;
                        Vec3 to = centres[q];
                        if ((to - target).Length > 1.6f * rx || to.Y < target.Y - 0.2f * ry) continue;
                        Vec3 a = Vec3.Lerp(from, target, 0.6f);
                        Limb(a, Vec3.Lerp(a, to, 0.8f), limbR0 * 0.45f, limbR0 * 0.15f, 0.04f * rx, f.Bark, 0.7f, 0.6f, 1, 3);
                        used[q] = true;
                        break;
                    }
                }
            }
            // A central leader up into the crown keeps the middle from looking hollow.
            if (!f.Umbrella && D.Near)
                Limb(stemTops[0], crown + new Vec3(0f, 0.35f * ry, 0f), limbR0 * 0.9f, limbR0 * 0.3f, 0f, f.Bark, 0.7f, 0.6f);

            // 4. A dark inner core fills the gaps between the clumps (seen only as depth), then the clumps.
            var st = FloraBuilder.PuffStyle.Foliage(f.Leaf, crown, Math.Max(rx, ry));
            st.TopRgb = f.LeafTop;
            var core = st;
            core.ShadeLow = 0.55f;
            core.ShadeHigh = 0.72f;
            core.TopRgb = 0;
            B.Puff(crown + new Vec3(0f, (f.Umbrella ? 0.05f : -0.05f) * ry, 0f), new Vec3(rx * 0.55f, ry * 0.55f, rx * 0.55f), 0, 0.12f, Seed ^ 0xC0u, core);
            for (int k = 0; k < clumps; k++)
            {
                var cs = st;
                if (clumpRgb != null)
                {
                    uint rgb = clumpRgb(k, clumps);
                    if (rgb != 0)
                    {
                        cs.Rgb = rgb;
                        cs.TopRgb = FloraBuilder.Mix(rgb, 0xFFFFFFu, 0.12f);
                    }
                }
                cs.ShadeLow = st.ShadeLow * Rng.Range(0.94f, 1.04f);
                Clump(centres[k], sizes[k], f.Lumps, cs, f.Fringe, -1, f.FringeDroop, 0.48f, 0.66f, f.FringeRgb);
            }
        }

        /// <summary>
        /// The mid-distance tree (LOD1): the same trunk, two limbs and a crown of one big smooth lumpy mass with a
        /// second lobe on top (both 80-triangle puffs, so the silhouette stays round, never faceted), coloured like the
        /// near tree's clumps (bloom included).
        /// </summary>
        private void SimpleCrown(in BroadleafForm f, Vec3 crown, float rx, float ry, Vec3 leanV, float forkH, Func<int, int, uint> clumpRgb)
        {
            Trunk(Vec3.Zero, forkH, f.TrunkR, f.TrunkR * f.TrunkTopR, leanV, 0, 0f, f.Bark, 0.04f * f.H);
            Vec3 top = TrunkPoint(Vec3.Zero, forkH, leanV, 1f, 0.04f * f.H, Seed);
            float limbR0 = f.TrunkR * f.TrunkTopR * f.LimbR * 1.6f;
            double b0 = Rng.Range(0f, 6.283f);
            for (int i = 0; i < 2; i++)
            {
                double a = b0 + i * Math.PI;
                var target = crown + new Vec3((float)Math.Cos(a) * 0.45f * rx, -0.35f * ry, (float)Math.Sin(a) * 0.45f * rx);
                Limb(top, target, limbR0, limbR0 * 0.4f, 0.05f * rx, f.Bark, 0.7f, 0.55f);
                if (TipCount < Tips.Length) Tips[TipCount++] = target;
            }
            var st = FloraBuilder.PuffStyle.Foliage(f.Leaf, crown, Math.Max(rx, ry));
            st.TopRgb = f.LeafTop;
            st.Bend = 0.15f;
            uint main = clumpRgb != null ? clumpRgb(0, 2) : 0u, lobe = clumpRgb != null ? clumpRgb(1, 2) : 0u;
            if (FarRgb != 0 && clumpRgb == null)
            {
                main = FloraBuilder.Mix(f.Leaf, FarRgb, 0.55f);
                lobe = FarRgb;
            }
            var ms = st;
            if (main != 0)
            {
                ms.Rgb = main;
                ms.TopRgb = FloraBuilder.Mix(main, 0xFFFFFFu, 0.12f);
            }
            float lump = Math.Max(0.26f, f.Lumps + 0.08f);
            Clump(crown + new Vec3(0f, 0.12f * ry, 0f), new Vec3(rx * 0.92f, ry * 0.8f, rx * 0.92f), lump, ms, 0, 1);
            var ls = st;
            if (lobe != 0)
            {
                ls.Rgb = lobe;
                ls.TopRgb = FloraBuilder.Mix(lobe, 0xFFFFFFu, 0.12f);
            }
            // The second mass bulges out of the upper side of the dome (not stacked on top: no snowman at mid range).
            float lr = f.Umbrella ? 0.55f : 0.52f;
            double la = b0 + 0.5 * Math.PI + Rng.Jitter(0.6f);
            Clump(crown + new Vec3((float)Math.Cos(la) * 0.34f * rx, 0.3f * ry, (float)Math.Sin(la) * 0.34f * rx), new Vec3(rx * lr, ry * 0.55f, rx * lr), lump, ls, 0, 1);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Blooms on the clump surfaces

        /// <summary>A point on the outer, upper surface of a random clump with its outward direction (blooms, fruit).</summary>
        public Vec3 OnClumps(out Vec3 outward, float minUp = -0.2f)
        {
            if (ClumpCount == 0)
            {
                outward = Vec3.Up;
                return CrownC;
            }
            for (int attempt = 0; attempt < 8; attempt++)
            {
                int k = Rng.Int(0, ClumpCount - 1);
                double a = Rng.Range(0f, 6.283f);
                float y = Rng.Range(minUp, 1f);
                float rr = (float)Math.Sqrt(Math.Max(0.0, 1.0 - y * y));
                Vec3 d = new Vec3((float)Math.Cos(a) * rr, y, (float)Math.Sin(a) * rr);
                Vec3 p = ClumpC[k] + new Vec3(d.X * ClumpR[k].X, d.Y * ClumpR[k].Y, d.Z * ClumpR[k].Z) * 0.95f;
                // Keep blooms on the outside of the crown (not buried between clumps).
                if (Vec3.Dot(p - CrownC, d) < 0f && attempt < 7) continue;
                outward = d;
                return p;
            }
            outward = Vec3.Up;
            return ClumpC[0] + new Vec3(0f, ClumpR[0].Y, 0f);
        }
    }
}
