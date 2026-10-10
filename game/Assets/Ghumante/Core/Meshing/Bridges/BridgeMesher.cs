using System;
using System.Collections.Generic;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing.Bridges
{
    /// <summary>Options for <see cref="BridgeMesher"/>.</summary>
    public sealed class BridgeOptions
    {
        /// <summary>Level of detail: 0 near (rounded profiles, every post, baluster and kerb stripe, detailed lamps),
        /// 1 mid (one bevel step, half the posts, plain kerbs, simple lamps), 2 far (deck box, solid parapets, plain
        /// piers, no lamps or posts).</summary>
        public int Lod;

        /// <summary>Draw lamp posts.</summary>
        public bool Lamps = true;

        /// <summary>Draw piers, abutments, wing walls and bents.</summary>
        public bool Supports = true;

        /// <summary>Draw roofs over foot overbridges (where the way's hash gives one).</summary>
        public bool Roofs = true;

        /// <summary>Draw underpass trench walls.</summary>
        public bool Trenches = true;
    }

    /// <summary>
    /// The structure of every elevated road piece of a tile (decision 3 and 4 of docs/W2_DETAIL_CONTRACT.md; the road
    /// mesher draws the driving surface at the deck height): a deck slab with rounded fascia and drip edges over
    /// T-girders or a box, raised walkways with painted kerbs, railings on both sides in the span's
    /// <see cref="RailingStyle"/> (RCC balustrade, pipe rails, crash parapet with handrail, steel truss with mesh,
    /// Newar brick and carved wood), median barriers where twin decks meet, end pillars, abutments with splayed wing
    /// walls at real ends, rounded piers (wall piers with cutwaters in rivers, hammerhead or multi-column piers over
    /// roads) kept out of the roads below, flyover ramps as fill between panelled retaining walls, lamp posts with
    /// curved arms, foot overbridge trusses, stairs and roofs, and underpass trench walls. Every vertex carries UV0 =
    /// (<see cref="MaterialChannel"/>, baked AO). Positions are tile-local metres with absolute Y (like the road
    /// mesher); a span cut by a tile border ends exactly on the border with the same cross-section the neighbour
    /// starts with. Deterministic; thread-safe for distinct meshes.
    /// </summary>
    public static class BridgeMesher
    {
        private const double KerbPaintM = 1.5;
        private const double WallPanelM = 4.5;

        private enum Part : byte
        {
            Deck,
            FillTop,
            KerbFace,
            Walk,
            Plinth,
            JerseyFoot,
            JerseyBody,
            FillWall,
            TrenchWall,
        }

        private struct Ctx
        {
            public BridgeLayout L;
            public IHeightSampler H;
            public double X0, Z0;
            public int Lod;
            public int Arc;
            public BridgeOptions O;
        }

        [ThreadStatic] private static BridgeProfile _p, _q;
        [ThreadStatic] private static double[] _sl;
        [ThreadStatic] private static List<double> _anchor;
        [ThreadStatic] private static double[] _tx, _ty, _tz;

        /// <summary>Mesh every span and trench of the tile into <paramref name="m"/>; returns the spans drawn.</summary>
        public static int Build(TileData t, IHeightSampler h, BridgeOptions o, MeshData m)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (m == null) throw new ArgumentNullException(nameof(m));
            BridgeLayout layout = BridgeLayout.For(t);
            int n = 0;
            for (int i = 0; i < layout.Spans.Count; i++)
                if (BuildSpan(layout, i, h, o, m)) n++;
            if (o == null || o.Trenches)
                for (int i = 0; i < layout.Trenches.Count; i++) Trench(Context(layout, h, o), layout.Trenches[i], m);
            return n;
        }

        /// <summary>Mesh one span of a layout (previews and tests).</summary>
        public static bool BuildSpan(BridgeLayout layout, int spanIndex, IHeightSampler h, BridgeOptions o, MeshData m)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            BridgeSpan sp = layout.Spans[spanIndex];
            if (sp.Count < 2) return false;
            Ctx x = Context(layout, h, o);
            if (_p == null)
            {
                _p = new BridgeProfile();
                _q = new BridgeProfile();
            }
            Deck(ref x, sp, m);
            for (int side = -1; side <= 1; side += 2)
            {
                Walkway(ref x, sp, side, m);
                Railing(ref x, sp, side, m);
            }
            if (x.O.Supports)
            {
                Abutments(ref x, sp, m);
                Bents(ref x, sp, m);
                Piers(ref x, sp, m);
            }
            if (x.O.Lamps && x.Lod < 2) Lamps(ref x, sp, m);
            if (sp.Overbridge)
            {
                if (x.O.Roofs && x.Lod < 2 && (BridgeStyle.Hash(sp.Record.OsmWayId, 0x524F4F46) & 1) == 0) Roof(ref x, sp, m);
                foreach (BridgeStair st in sp.Stairs) Stair(ref x, sp, st, m);
            }
            SteepSteps(ref x, sp, m);
            return true;
        }

        private static Ctx Context(BridgeLayout layout, IHeightSampler h, BridgeOptions o)
        {
            if (o == null) o = new BridgeOptions();
            int lod = o.Lod < 0 ? 0 : o.Lod > 2 ? 2 : o.Lod;
            return new Ctx
            {
                L = layout, H = h ?? layout.Ground, X0 = layout.Tile.Tile.X0, Z0 = layout.Tile.Tile.Z0, Lod = lod, Arc = lod == 0 ? 3 : lod == 1 ? 1 : 0, O = o,
            };
        }

        // ---------------------------------------------------------------------------------------------------------
        // Cross-section state
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Carriageway edge c, railing inner face w and deck edge e on one side at along s (positive).</summary>
        private static void Side(BridgeSpan sp, double s, int side, out double c, out double w, out double e)
        {
            double half = side > 0 ? sp.Lerp(sp.HalfL, s) : sp.Lerp(sp.HalfR, s);
            double walk = side > 0 ? sp.Lerp(sp.WalkL, s) : sp.Lerp(sp.WalkR, s);
            double clamp = side > 0 ? sp.Lerp(sp.ClampL, s) : sp.Lerp(sp.ClampR, s);
            c = half;
            w = Math.Min(half + walk, clamp - BridgeStyle.RailBaseM);
            e = Math.Min(half + walk + BridgeStyle.RailBaseM + BridgeStyle.OverhangM, clamp);
        }

        private static float Top(BridgeSpan sp)
        {
            return sp.RaisedWalk ? BridgeStyle.KerbHeightM : 0f;
        }

        private static bool Shared(BridgeSpan sp, int side)
        {
            return side > 0 ? sp.SharedL : sp.SharedR;
        }

        private static float GroundAt(ref Ctx x, double lx, double lz)
        {
            float g;
            if (x.H != null && x.H.TryHeight(x.X0 + lx, x.Z0 + lz, out g)) return g;
            return BridgeStructures.Ground(x.L.Tile, x.L.Ground, lx, lz);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Variable-profile sweeps
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Sweep a part whose cross-section is rebuilt at every ring from the span state (widths, depth,
        /// ground). Stripe boundaries (world-anchored) split the rings so alternate stripes take c2.</summary>
        private static void PartSweep(ref Ctx x, BridgeSpan sp, Part part, int side, double s0, double s1, double stripe, uint c, uint c2,
                                      MaterialChannel ch, MeshData m)
        {
            if (s1 - s0 < 1e-3) return;
            int ns = BridgeKit.BuildStations(sp.Path, s0, s1, 0, stripe, x.X0, x.Z0);
            if (_sl == null || _sl.Length < ns) _sl = new double[Math.Max(ns, 256)];
            Array.Copy(BridgeKit.Stations, _sl, ns);
            double[] sl = _sl;
            int prevRing = -1, n = 0;
            uint prevColour = c;
            for (int k = 0; k < ns; k++)
            {
                double s = sl[k];
                uint segColour = prevColour;
                if (k < ns - 1)
                {
                    segColour = c;
                    if (stripe > 0 && (BridgeKit.StripeIndex(sp.Path, 0.5 * (s + sl[k + 1]), stripe, x.X0, x.Z0) & 1) != 0) segColour = c2;
                }
                BridgeProfile p = Profile(ref x, sp, part, side, s);
                if (p.Count < 2) return;
                if (prevRing >= 0 && segColour != prevColour)
                {
                    int endRing = BridgeKit.Ring(m, sp.Path, s, p, 0, 0, prevColour, ch, 1f);
                    BridgeKit.Band(m, prevRing, endRing, p.Count);
                    prevRing = BridgeKit.Ring(m, sp.Path, s, p, 0, 0, segColour, ch, 1f);
                }
                else
                {
                    int ring = BridgeKit.Ring(m, sp.Path, s, p, 0, 0, segColour, ch, 1f);
                    if (prevRing >= 0 && p.Count == n) BridgeKit.Band(m, prevRing, ring, p.Count);
                    prevRing = ring;
                }
                n = p.Count;
                prevColour = segColour;
            }
        }

        /// <summary>The cross-section of a part at along s (mirrored for the right side).</summary>
        private static BridgeProfile Profile(ref Ctx x, BridgeSpan sp, Part part, int side, double s)
        {
            BridgeProfile p = _p.Clear();
            double c, w, e;
            Side(sp, s, side, out c, out w, out e);
            float top = Top(sp);
            int arc = x.Arc;
            switch (part)
            {
                case Part.Deck:
                {
                    double cl, wl, el, cr, wr, er;
                    Side(sp, s, 1, out cl, out wl, out el);
                    Side(sp, s, -1, out cr, out wr, out er);
                    DeckSection(p, el, er, sp.Lerp(sp.Depth, s), sp.Foot, arc, sp);
                    return p;
                }
                case Part.FillTop:
                {
                    double cl, wl, el, cr, wr, er;
                    Side(sp, s, 1, out cl, out wl, out el);
                    Side(sp, s, -1, out cr, out wr, out er);
                    p.Add(-er, -0.03, 0.95f, BridgeStyle.Concrete).Add(el, -0.03, 0.95f, BridgeStyle.Concrete);
                    return p.Finish();
                }
                case Part.KerbFace:
                {
                    double r = 0.05;
                    p.Add(c, -0.03, 0.75f);
                    if (arc > 0) p.Add(c, top - r, 0.9f).Arc(c + r, top - r, r, Math.PI, 0.5 * Math.PI, 1, 1f);
                    else p.Add(c, top, 1f).Hard();
                    p.Add(c + 0.14, top, 1f);
                    break;
                }
                case Part.Walk:
                    // Never reversed (a walkway squeezed to nothing on a shared side keeps facing up).
                    p.Add(c + 0.14, top, 1f).Add(Math.Max(w, c + 0.16), top, 0.78f);
                    break;
                case Part.Plinth:
                {
                    double hp = PlinthHeight(sp, x.Lod), rb = BridgeStyle.RailBaseM, r = Math.Min(0.06, 0.25 * hp);
                    p.Add(w, top, 0.75f);
                    if (arc > 0)
                    {
                        p.Add(w, top + hp - r, 0.95f).Arc(w + r, top + hp - r, r, Math.PI, 0.5 * Math.PI, arc, 1f);
                        p.Arc(w + rb - r, top + hp - r, r, 0.5 * Math.PI, 0, arc, 1f);
                    }
                    else
                    {
                        p.Add(w, top + hp, 1f).Hard().Add(w + rb, top + hp, 1f).Hard();
                    }
                    p.Add(w + rb, -0.03, 0.85f);
                    break;
                }
                case Part.JerseyFoot:
                    p.Add(w, top, 0.7f).Add(w, top + 0.075, 0.85f).Add(w + 0.055, top + 0.33, 0.95f);
                    break;
                case Part.JerseyBody:
                {
                    double h = 0.85, rb = BridgeStyle.RailBaseM, r = 0.05;
                    p.Add(w + 0.055, top + 0.33, 0.95f);
                    if (arc > 0)
                    {
                        p.Add(w + 0.15, top + h - r, 1f).Arc(w + 0.15 + r, top + h - r, r, Math.PI, 0.5 * Math.PI, arc, 1f);
                        p.Arc(w + rb - r, top + h - r, r, 0.5 * Math.PI, 0, arc, 1f);
                    }
                    else
                    {
                        p.Add(w + 0.15, top + h, 1f).Hard().Add(w + rb, top + h, 1f).Hard();
                    }
                    p.Add(w + rb, -0.03, 0.85f);
                    break;
                }
                case Part.FillWall:
                {
                    float y = sp.Path.YAt(s);
                    double lx, lz;
                    Offset(sp, s, side * e, out lx, out lz);
                    double g = GroundAt(ref x, lx, lz) - y;
                    p.Add(e, -0.03, 1f).Add(e + 0.07, -0.08, 1f).Hard().Add(e + 0.07, -0.28, 0.95f).Hard().Add(e, -0.33, 0.85f).Hard();
                    p.Add(e, Math.Min(-0.4, g - 0.6), 0.6f);
                    break;
                }
                case Part.TrenchWall:
                {
                    float y = sp.Path.YAt(s);
                    double lx, lz;
                    Offset(sp, s, side * (w + 0.15), out lx, out lz);
                    double g = Math.Max(0.4, GroundAt(ref x, lx, lz) - y), r = 0.06;
                    p.Add(w, -0.1, 0.6f).Add(w, g + 1.0 - r, 0.95f);
                    if (arc > 0)
                    {
                        p.Arc(w + r, g + 1.0 - r, r, Math.PI, 0.5 * Math.PI, arc, 1f);
                        p.Arc(w + 0.3 - r, g + 1.0 - r, r, 0.5 * Math.PI, 0, arc, 1f);
                    }
                    else
                    {
                        p.Hard().Add(w, g + 1.0, 1f).Hard().Add(w + 0.3, g + 1.0, 1f).Hard();
                    }
                    p.Add(w + 0.3, g - 0.4, 0.7f);
                    break;
                }
            }
            p.Finish();
            return side > 0 ? p : p.MirrorInto(_q);
        }

        /// <summary>Tile-local plan point at signed offset u (left positive) from the centreline at along s.</summary>
        private static void Offset(BridgeSpan sp, double s, double u, out double lx, out double lz)
        {
            double x, z, ux, uz, nx, nz, tx, tz;
            float y;
            sp.Path.Frame(s, out x, out z, out y, out ux, out uz, out nx, out nz, out tx, out tz);
            lx = x + nx * u;
            lz = z + nz * u;
        }

        /// <summary>The deck cross-section: top, fascia with a drip edge, cantilever soffits and a rounded girder (or a
        /// slim box for foot decks), as one outward loop from the right top edge round to the right fascia.</summary>
        private static void DeckSection(BridgeProfile p, double eL, double eR, double depth, bool foot, int arc, BridgeSpan sp)
        {
            double D = Math.Max(BridgeStyle.MinDepthM, depth);
            uint top = BridgeStyle.Concrete, face = BridgeStyle.Concrete, soffit = BridgeStyle.ConcreteSoffit;
            if (foot)
            {
                uint steel = sp.Overbridge ? sp.Steel : BridgeStyle.Concrete;
                int ch = sp.Overbridge ? (int)MaterialChannel.Metal : (int)MaterialChannel.Concrete;
                double r = Math.Min(0.12, 0.3 * D);
                p.Add(-eR, -0.03, 0.95f, top).Add(eL, -0.03, 0.95f, top).Hard();
                p.Paint(p.Count - 1, steel, ch);
                if (arc > 0) p.Add(eL, -D + r, 0.8f, steel, ch).Arc(eL - r, -D + r, r, 0, -0.5 * Math.PI, arc, 0.6f);
                else p.Add(eL, -D, 0.6f, steel, ch).Hard();
                int b = p.Count;
                if (arc > 0) p.Arc(-eR + r, -D + r, r, -0.5 * Math.PI, -Math.PI, arc, 0.6f);
                else p.Add(-eR, -D, 0.6f).Hard();
                p.Add(-eR, -0.03, 0.8f);
                p.Paint(b, steel, ch);
                p.Finish();
                return;
            }
            double fd = Math.Min(0.32, 0.45 * D), rd = arc > 0 ? Math.Min(0.06, 0.3 * fd) : 0;
            double ys = -Math.Min(0.42, 0.55 * D), yc = -Math.Min(0.55, 0.7 * D);
            double rb = Math.Min(0.28, 0.25 * (D + yc));
            double cantL = Math.Min(1.4, 0.2 * (eL + eR)), cantR = cantL;
            double gL = Math.Max(eL - cantL, 0.45), gR = Math.Max(eR - cantR, 0.45);
            // Top and the left fascia.
            p.Add(-eR, -0.03, 0.95f, top).Add(eL, -0.03, 0.95f, top).Hard();
            p.Paint(p.Count - 1, face);
            if (rd > 0) p.Add(eL, -fd + rd, 0.9f, face).Arc(eL - rd, -fd + rd, rd, 0, -0.5 * Math.PI, Math.Max(1, arc - 1), 0.75f);
            else p.Add(eL, -fd, 0.85f, face).Hard();
            int a = p.Count;
            // Left cantilever soffit into the girder, the girder side, its rounded bottom corner, the bottom.
            p.Add(gL + 0.12, ys, 0.5f).Add(gL, yc, 0.5f).Hard();
            if (arc > 0) p.Add(gL, -D + rb, 0.55f).Arc(gL - rb, -D + rb, rb, 0, -0.5 * Math.PI, arc, 0.5f);
            else p.Add(gL, -D, 0.5f).Hard();
            if (arc > 0) p.Arc(-gR + rb, -D + rb, rb, -0.5 * Math.PI, -Math.PI, arc, 0.5f);
            else p.Add(-gR, -D, 0.5f).Hard();
            p.Add(-gR, yc, 0.5f).Hard().Add(-gR - 0.12, ys, 0.5f);
            p.Paint(a, soffit);
            int f = p.Count;
            if (rd > 0) p.Arc(-eR + rd, -fd + rd, rd, -0.5 * Math.PI, -Math.PI, Math.Max(1, arc - 1), 0.75f);
            else p.Add(-eR, -fd, 0.85f).Hard();
            p.Add(-eR, -0.03, 0.9f);
            p.Paint(f, face);
            p.Finish();
        }

        private static double PlinthHeight(BridgeSpan sp, int lod)
        {
            if (lod >= 2) return sp.Railing == RailingStyle.SteelTruss ? 0.12 : Math.Max(0.6, sp.RailHeight * 0.85);
            switch (sp.Railing)
            {
                case RailingStyle.ConcreteRail: return 0.3;
                case RailingStyle.PipeRail: return 0.42;
                case RailingStyle.Newar: return 0.62;
                case RailingStyle.SteelTruss: return 0.12;
                default: return 0.3;
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Deck, walkways, railings
        // ---------------------------------------------------------------------------------------------------------

        private static void Deck(ref Ctx x, BridgeSpan sp, MeshData m)
        {
            BridgePath path = sp.Path;
            int n = sp.Count;
            int k = 0;
            while (k < n)
            {
                bool fill = sp.Fill[k];
                int a = k;
                while (k + 1 < n && sp.Fill[k + 1] == fill) k++;
                int b = k;
                k++;
                // Open runs reach one station into a neighbouring fill run so the bent closes them.
                double s0 = path.S[a], s1 = path.S[b];
                if (!fill)
                {
                    PartSweep(ref x, sp, Part.Deck, 1, s0, s1, 0, BridgeStyle.Concrete, 0, MaterialChannel.Concrete, m);
                    bool capStart = a > 0 || !sp.StartCut, capEnd = b < n - 1 || !sp.EndCut;
                    if (capStart) DeckCap(ref x, sp, s0, -1, m);
                    if (capEnd) DeckCap(ref x, sp, s1, +1, m);
                }
                else
                {
                    double f0 = a > 0 ? path.S[a - 1] : s0, f1 = b < n - 1 ? path.S[b + 1] : s1;
                    PartSweep(ref x, sp, Part.FillTop, 1, f0, f1, 0, BridgeStyle.Concrete, 0, MaterialChannel.Concrete, m);
                    for (int side = -1; side <= 1; side += 2)
                        PartSweep(ref x, sp, Part.FillWall, side, f0, f1, x.Lod == 0 ? WallPanelM : 0, BridgeStyle.Concrete,
                                  MeshColor.Scale(BridgeStyle.Concrete, 0.93f), MaterialChannel.Concrete, m);
                }
            }
        }

        /// <summary>Close the deck section at an end (a fan from a point inside the slab, which sees the whole
        /// T-section).</summary>
        private static void DeckCap(ref Ctx x, BridgeSpan sp, double s, int dir, MeshData m)
        {
            BridgeProfile p = Profile(ref x, sp, Part.Deck, 1, s);
            double px, pz, ux, uz, nx, nz, tx, tz;
            float y;
            sp.Path.Frame(s, out px, out pz, out y, out ux, out uz, out nx, out nz, out tx, out tz);
            double D = sp.Lerp(sp.Depth, s);
            double cu = 0;
            for (int i = 0; i < p.Count; i++) cu += p.U[i];
            cu /= p.Count;
            double cy = -Math.Min(0.3, 0.45 * D);
            int centre = BridgeKit.V(m, px + ux * cu, y + cy, pz + uz * cu, tx * dir, 0, tz * dir, BridgeStyle.ConcreteDark, MaterialChannel.Concrete, 0.7f);
            int first = m.VertexCount;
            for (int i = 0; i < p.Count; i++)
                BridgeKit.V(m, px + ux * p.U[i], y + p.Y[i], pz + uz * p.U[i], tx * dir, 0, tz * dir, BridgeStyle.ConcreteDark, MaterialChannel.Concrete, 0.7f);
            for (int i = 0; i + 1 < p.Count; i++) BridgeKit.Tri(m, centre, first + i, first + i + 1);
        }

        private static void Walkway(ref Ctx x, BridgeSpan sp, int side, MeshData m)
        {
            if (!sp.RaisedWalk) return;
            double s0 = sp.Path.Start, s1 = sp.Path.End;
            uint paint = BridgeStyle.TrafficYellow, paint2 = BridgeStyle.TrafficBlack;
            if (x.Lod == 0) PartSweep(ref x, sp, Part.KerbFace, side, s0, s1, KerbPaintM, paint, paint2, MaterialChannel.Paint, m);
            else PartSweep(ref x, sp, Part.KerbFace, side, s0, s1, 0, BridgeStyle.Whitewash, 0, MaterialChannel.Paint, m);
            PartSweep(ref x, sp, Part.Walk, side, s0, s1, 0, BridgeStyle.DeckPaver, 0, MaterialChannel.Concrete, m);
        }

        private static void Railing(ref Ctx x, BridgeSpan sp, int side, MeshData m)
        {
            double s0 = sp.Path.Start, s1 = sp.Path.End;
            if (Shared(sp, side))
            {
                // Median barrier where twin decks meet.
                PartSweep(ref x, sp, Part.JerseyFoot, side, s0, s1, x.Lod == 0 ? 2.0 : 0, BridgeStyle.TrafficYellow, BridgeStyle.TrafficBlack,
                          MaterialChannel.Paint, m);
                PartSweep(ref x, sp, Part.JerseyBody, side, s0, s1, 0, BridgeStyle.Whitewash, 0, MaterialChannel.Concrete, m);
                return;
            }
            RailingStyle style = sp.Railing;
            if (x.Lod >= 2)
            {
                PartSweep(ref x, sp, Part.Plinth, side, s0, s1, 0, style == RailingStyle.Newar ? BridgeStyle.Brick : BridgeStyle.Whitewash, 0,
                          style == RailingStyle.Newar ? MaterialChannel.Brick : MaterialChannel.Concrete, m);
                if (style == RailingStyle.SteelTruss) Rail(ref x, sp, side, 0.05, sp.RailHeight, 0.06, sp.Steel, MaterialChannel.Metal, m, 4);
                return;
            }
            float top = Top(sp);
            switch (style)
            {
                case RailingStyle.ConcreteRail:
                    PartSweep(ref x, sp, Part.Plinth, side, s0, s1, 0, BridgeStyle.Concrete, 0, MaterialChannel.Concrete, m);
                    if (x.Lod == 0)
                    {
                        RectRail(ref x, sp, side, 0.0, 0.47, 0.06, 0.065, BridgeStyle.Whitewash, MaterialChannel.Paint, m);
                        RectRail(ref x, sp, side, 0.0, 0.76, 0.06, 0.065, BridgeStyle.Whitewash, MaterialChannel.Paint, m);
                    }
                    RectRail(ref x, sp, side, 0.0, sp.RailHeight - 0.07, 0.11, 0.075, BridgeStyle.Whitewash, MaterialChannel.Paint, m);
                    Posts(ref x, sp, side, 3.0, 0.3, sp.RailHeight - 0.02, 0.12, BridgeStyle.TrafficYellow, BridgeStyle.TrafficBlack, MaterialChannel.Paint, true, m);
                    break;
                case RailingStyle.PipeRail:
                    PartSweep(ref x, sp, Part.Plinth, side, s0, s1, 0, BridgeStyle.Whitewash, 0, MaterialChannel.Concrete, m);
                    if (x.Lod == 0) Rail(ref x, sp, side, 0.0, 0.74, 0.038, sp.Steel, MaterialChannel.Metal, m);
                    Rail(ref x, sp, side, 0.0, sp.RailHeight - 0.03, 0.045, sp.Steel, MaterialChannel.Metal, m);
                    Posts(ref x, sp, side, 2.0, 0.42, sp.RailHeight + 0.04, 0.1, BridgeStyle.Whitewash, BridgeStyle.Whitewash, MaterialChannel.Concrete, true, m);
                    break;
                case RailingStyle.CrashBarrier:
                    PartSweep(ref x, sp, Part.JerseyFoot, side, s0, s1, x.Lod == 0 ? 2.0 : 0, BridgeStyle.TrafficYellow, BridgeStyle.TrafficBlack,
                              MaterialChannel.Paint, m);
                    PartSweep(ref x, sp, Part.JerseyBody, side, s0, s1, 0, BridgeStyle.Concrete, 0, MaterialChannel.Concrete, m);
                    Rail(ref x, sp, side, 0.04, 1.2, 0.042, BridgeStyle.Galvanised, MaterialChannel.Metal, m);
                    PostTubes(ref x, sp, side, 3.0, 0.85, 1.2, 0.03, 0.04, BridgeStyle.Galvanised, m);
                    break;
                case RailingStyle.SteelTruss:
                    PartSweep(ref x, sp, Part.Plinth, side, s0, s1, 0, BridgeStyle.Concrete, 0, MaterialChannel.Concrete, m);
                    Rail(ref x, sp, side, -0.08, sp.RailHeight, 0.045, sp.Steel, MaterialChannel.Metal, m);
                    Rail(ref x, sp, side, -0.08, 0.62, 0.028, sp.Steel, MaterialChannel.Metal, m);
                    Rail(ref x, sp, side, -0.08, 0.17, 0.035, sp.Steel, MaterialChannel.Metal, m);
                    PostTubes(ref x, sp, side, 2.0, 0.12, sp.RailHeight, -0.08, 0.04, sp.Steel, m);
                    Mesh(ref x, sp, side, 2.0, m);
                    break;
                case RailingStyle.Newar:
                    PartSweep(ref x, sp, Part.Plinth, side, s0, s1, 0, BridgeStyle.Brick, 0, MaterialChannel.Brick, m);
                    RectRail(ref x, sp, side, 0.0, sp.RailHeight - 0.06, 0.1, 0.07, BridgeStyle.Wood, MaterialChannel.WoodCarved, m);
                    Posts(ref x, sp, side, 1.8, 0.62, sp.RailHeight + 0.12, 0.09, BridgeStyle.Wood, BridgeStyle.Wood, MaterialChannel.WoodCarved, true, m);
                    if (x.Lod == 0) Balusters(ref x, sp, side, 0.62, sp.RailHeight - 0.13, m);
                    break;
            }
        }

        /// <summary>A closed rounded-rectangle rail along the whole span at height h above the walkway, centred on the
        /// railing base (offset du from its centre).</summary>
        private static void RectRail(ref Ctx x, BridgeSpan sp, int side, double du, double h, double hu, double hy, uint c, MaterialChannel ch, MeshData m)
        {
            BridgeProfile p = _p;
            double s0 = sp.Path.Start, s1 = sp.Path.End;
            // The rail follows the railing line, whose offset varies with the widths: sweep it segment by segment
            // with the offset of each station (rings at every station, so the rail bends with the deck).
            p.RoundRect(0, 0, hu, hy, Math.Min(hu, hy) * 0.5, x.Lod == 0 ? 1 : 0, 0.95f);
            LineSweep(ref x, sp, side, du, h, p, c, ch, m, s0, s1);
        }

        private static void Rail(ref Ctx x, BridgeSpan sp, int side, double du, double h, double r, uint c, MaterialChannel ch, MeshData m, int sides = 0)
        {
            int n = sides > 0 ? sides : x.Lod == 0 ? 8 : 4;
            _p.Circle(0, 0, r, n, 0.95f);
            LineSweep(ref x, sp, side, du, h, _p, c, ch, m, sp.Path.Start, sp.Path.End);
        }

        /// <summary>Sweep a fixed (small) profile along the railing line of one side: at every station the profile
        /// sits at the railing base centre + du (outward), h above the walkway.</summary>
        private static void LineSweep(ref Ctx x, BridgeSpan sp, int side, double du, double h, BridgeProfile p, uint c, MaterialChannel ch, MeshData m,
                                      double s0, double s1)
        {
            int ns = BridgeKit.BuildStations(sp.Path, s0, s1, 0, 0, x.X0, x.Z0);
            if (_sl == null || _sl.Length < ns) _sl = new double[Math.Max(ns, 256)];
            Array.Copy(BridgeKit.Stations, _sl, ns);
            float top = Top(sp);
            int prev = -1;
            BridgeProfile q = side > 0 ? p : p.MirrorInto(_q);
            for (int k = 0; k < ns; k++)
            {
                double s = _sl[k], cc, w, e;
                Side(sp, s, side, out cc, out w, out e);
                double u = side * (w + 0.5 * BridgeStyle.RailBaseM + du);
                int ring = BridgeKit.Ring(m, sp.Path, s, q, u, top + h, c, ch, 1f);
                if (prev >= 0) BridgeKit.Band(m, prev, ring, q.Count);
                prev = ring;
            }
        }

        /// <summary>Posts at world-anchored stations (rounded boxes with a cap), alternating colours c and c2,
        /// from y0 to y1 above the walkway; end pillars at real ends when <paramref name="endPillars"/>.</summary>
        private static void Posts(ref Ctx x, BridgeSpan sp, int side, double spacing, double y0, double y1, double half, uint c, uint c2, MaterialChannel ch,
                                  bool endPillars, MeshData m)
        {
            if (x.Lod == 1) spacing *= 2;
            List<double> st = Anchor();
            BridgeLayout.AnchoredStations(sp.Path, sp.Path.Start, sp.Path.End, spacing, x.X0, x.Z0, st);
            float top = Top(sp);
            int arc = Math.Max(1, x.Arc - 1);
            for (int i = 0; i < st.Count; i++)
            {
                double s = st[i];
                if (!sp.StartCut && s - sp.Path.Start < 0.8) continue;
                if (!sp.EndCut && sp.Path.End - s < 0.8) continue;
                double px, pz, yaw;
                float y;
                PostFrame(sp, s, side, 0, out px, out pz, out y, out yaw);
                long idx = BridgeKit.StripeIndex(sp.Path, s + 1e-3, spacing, x.X0, x.Z0);
                uint col = (idx & 1) == 0 ? c : c2;
                Post(m, px, pz, yaw, half, y + top + y0 - 0.02, y + top + y1, x.Lod, col, ch);
            }
            if (!endPillars) return;
            for (int end = 0; end < 2; end++)
            {
                bool cut = end == 0 ? sp.StartCut : sp.EndCut;
                if (cut) continue;
                double s = end == 0 ? sp.Path.Start + 0.3 : sp.Path.End - 0.3;
                double px, pz, yaw;
                float y;
                PostFrame(sp, s, side, 0.02, out px, out pz, out y, out yaw);
                uint pc = sp.Railing == RailingStyle.Newar ? BridgeStyle.Brick : BridgeStyle.Whitewash;
                MaterialChannel pch = sp.Railing == RailingStyle.Newar ? MaterialChannel.Brick : MaterialChannel.Concrete;
                double ph = sp.RailHeight + 0.35;
                BridgeKit.RoundedBox(m, px, pz, yaw, 0.26, 0.24, y + top - 0.05, y + top + ph, 0.05, arc, pc, pch, 0.75f, 1f);
                BridgeKit.RoundedBox(m, px, pz, yaw, 0.3, 0.28, y + top + ph, y + top + ph + 0.1, 0.04, arc, BridgeStyle.Concrete, MaterialChannel.Concrete, 0.95f, 1f);
                if (x.Lod == 0)
                {
                    // A turned finial on the pillar.
                    double[] ly, ins;
                    float[] lao;
                    BridgeKit.Levels(6, out ly, out ins, out lao);
                    double b = y + top + ph + 0.1;
                    double[] hh = { 0, 0.06, 0.12, 0.2, 0.27, 0.3 };
                    double[] rr = { 0.12, 0.13, 0.08, 0.11, 0.05, 0.0 };
                    for (int i = 0; i < 6; i++)
                    {
                        ly[i] = b + hh[i];
                        ins[i] = 0.13 - rr[i];
                        lao[i] = 1f;
                    }
                    uint fc = sp.Railing == RailingStyle.Newar ? BridgeStyle.StoneLight : BridgeStyle.TrafficYellow;
                    BridgeKit.Loft(m, px, pz, 0, 0.13, 0.13, 0.13, 2, ly, ins, lao, null, 6, false, false, fc,
                                   sp.Railing == RailingStyle.Newar ? MaterialChannel.Stone : MaterialChannel.Paint);
                }
            }
        }

        /// <summary>A rounded square post with a moulded cap (one loft: shaft, cap lip and bevelled top).</summary>
        private static void Post(MeshData m, double px, double pz, double yaw, double half, double y0, double y1, int lod, uint c, MaterialChannel ch)
        {
            double[] ly, ins;
            float[] lao;
            BridgeKit.Levels(5, out ly, out ins, out lao);
            int n = 0;
            ly[n] = y0;
            ins[n] = 0;
            lao[n++] = 0.75f;
            if (lod == 0)
            {
                // Shaft and the cap's bevelled (pyramid-like) top.
                ly[n] = y1 - 0.03;
                ins[n] = 0;
                lao[n++] = 0.95f;
                ly[n] = y1 + 0.04;
                ins[n] = 0.045;
                lao[n++] = 1f;
            }
            else
            {
                ly[n] = y1;
                ins[n] = 0;
                lao[n++] = 1f;
            }
            BridgeKit.Loft(m, px, pz, yaw, half, half, 0.035, 1, ly, ins, lao, null, n, false, true, c, ch);
        }

        /// <summary>Round post tubes on the railing line from y0 to y1 above the walkway.</summary>
        private static void PostTubes(ref Ctx x, BridgeSpan sp, int side, double spacing, double y0, double y1, double du, double r, uint c, MeshData m)
        {
            if (x.Lod == 1) spacing *= 2;
            List<double> st = Anchor();
            BridgeLayout.AnchoredStations(sp.Path, sp.Path.Start, sp.Path.End, spacing, x.X0, x.Z0, st);
            float top = Top(sp);
            Tube3(2);
            int sides = x.Lod == 0 ? 5 : 4;
            foreach (double s in st)
            {
                double px, pz, yaw;
                float y;
                PostFrame(sp, s, side, du, out px, out pz, out y, out yaw);
                _tx[0] = _tx[1] = px;
                _tz[0] = _tz[1] = pz;
                _ty[0] = y + top + y0;
                _ty[1] = y + top + y1;
                BridgeKit.Tube(m, _tx, _ty, _tz, 2, r, sides, false, c, MaterialChannel.Metal, 0.9f);
            }
        }

        /// <summary>Mesh panels between the posts of a steel railing (double-sided thin quads) and, on foot
        /// overbridges at LOD0, the Warren truss diagonals.</summary>
        private static void Mesh(ref Ctx x, BridgeSpan sp, int side, double spacing, MeshData m)
        {
            if (x.Lod == 1) spacing *= 2;
            List<double> st = Anchor();
            BridgeLayout.AnchoredStations(sp.Path, sp.Path.Start, sp.Path.End, spacing, x.X0, x.Z0, st);
            float top = Top(sp);
            uint panel = MeshColor.FromHex(0x3B4448);
            Tube3(2);
            // Include the span ends so the first and last bays are filled.
            for (int i = -1; i < st.Count; i++)
            {
                double a = i < 0 ? sp.Path.Start : st[i], b = i + 1 < st.Count ? st[i + 1] : sp.Path.End;
                if (b - a < 0.3) continue;
                double ax, az, bx, bz, yaw;
                float ya, yb;
                PostFrame(sp, a, side, -0.08, out ax, out az, out ya, out yaw);
                PostFrame(sp, b, side, -0.08, out bx, out bz, out yb, out yaw);
                double nx = bz - az, nz = -(bx - ax), nl = Math.Sqrt(nx * nx + nz * nz);
                if (nl < 1e-9) continue;
                nx /= nl;
                nz /= nl;
                double y0a = ya + top + 0.2, y1a = ya + top + sp.RailHeight - 0.08, y0b = yb + top + 0.2, y1b = yb + top + sp.RailHeight - 0.08;
                BridgeKit.Quad(m, ax, y0a, az, bx, y0b, bz, bx, y1b, bz, ax, y1a, az, nx, 0, nz, panel, MaterialChannel.Metal, 0.8f, 0.9f);
                BridgeKit.Quad(m, ax, y0a, az, bx, y0b, bz, bx, y1b, bz, ax, y1a, az, -nx, 0, -nz, panel, MaterialChannel.Metal, 0.8f, 0.9f);
                if (sp.Overbridge && x.Lod == 0)
                {
                    bool up = (BridgeKit.StripeIndex(sp.Path, a + 1e-3, spacing, x.X0, x.Z0) & 1) == 0;
                    double off = side * 0.03;
                    _tx[0] = ax + nx * off;
                    _tz[0] = az + nz * off;
                    _tx[1] = bx + nx * off;
                    _tz[1] = bz + nz * off;
                    _ty[0] = up ? ya + top + 0.17 : ya + top + sp.RailHeight;
                    _ty[1] = up ? yb + top + sp.RailHeight : yb + top + 0.17;
                    BridgeKit.Tube(m, _tx, _ty, _tz, 2, 0.032, 5, false, sp.Steel, MaterialChannel.Metal, 0.9f);
                }
            }
        }

        /// <summary>Turned balusters between the parapet and the top rail (Newar style, LOD0).</summary>
        private static void Balusters(ref Ctx x, BridgeSpan sp, int side, double y0, double y1, MeshData m)
        {
            List<double> st = Anchor();
            BridgeLayout.AnchoredStations(sp.Path, sp.Path.Start, sp.Path.End, 0.3, x.X0, x.Z0, st);
            float top = Top(sp);
            double h = y1 - y0;
            double[] hh = { 0, 0.08, 0.22, 0.5, 0.78, 0.92, 1 };
            double[] rr = { 0.045, 0.03, 0.05, 0.028, 0.05, 0.03, 0.045 };
            foreach (double s in st)
            {
                if (!sp.StartCut && s - sp.Path.Start < 0.6) continue;
                if (!sp.EndCut && sp.Path.End - s < 0.6) continue;
                // Leave room around the posts.
                double rem = Math.Abs(Math.IEEERemainder(BridgeKit.StripeIndex(sp.Path, s + 1e-3, 0.3, x.X0, x.Z0), 6));
                if (rem < 0.5) continue;
                double px, pz, yaw;
                float y;
                PostFrame(sp, s, side, 0, out px, out pz, out y, out yaw);
                double[] ly, ins;
                float[] lao;
                BridgeKit.Levels(7, out ly, out ins, out lao);
                for (int i = 0; i < 7; i++)
                {
                    ly[i] = y + top + y0 + hh[i] * h;
                    ins[i] = 0.05 - rr[i];
                    lao[i] = 0.85f + 0.15f * (float)hh[i];
                }
                BridgeKit.Loft(m, px, pz, 0, 0.05, 0.05, 0.05, 1, ly, ins, lao, null, 7, false, false, BridgeStyle.Wood, MaterialChannel.WoodCarved);
            }
        }

        /// <summary>Plan position and deck y of the railing line on one side at along s (offset du outward from the
        /// railing base centre), and the yaw of the deck direction.</summary>
        private static void PostFrame(BridgeSpan sp, double s, int side, double du, out double px, out double pz, out float y, out double yaw)
        {
            double x, z, ux, uz, nx, nz, tx, tz, c, w, e;
            sp.Path.Frame(s, out x, out z, out y, out ux, out uz, out nx, out nz, out tx, out tz);
            Side(sp, s, side, out c, out w, out e);
            double u = side * (w + 0.5 * BridgeStyle.RailBaseM + du);
            px = x + nx * u;
            pz = z + nz * u;
            yaw = Math.Atan2(tz, tx);
        }

        private static List<double> Anchor()
        {
            return _anchor ?? (_anchor = new List<double>());
        }

        private static void Tube3(int n)
        {
            if (_tx != null && _tx.Length >= n) return;
            int cap = Math.Max(n, 16);
            _tx = new double[cap];
            _ty = new double[cap];
            _tz = new double[cap];
        }

        // ---------------------------------------------------------------------------------------------------------
        // Supports
        // ---------------------------------------------------------------------------------------------------------

        private static void Abutments(ref Ctx x, BridgeSpan sp, MeshData m)
        {
            for (int end = 0; end < 2; end++)
            {
                bool cut = end == 0 ? sp.StartCut : sp.EndCut;
                if (cut) continue;
                int dir = end == 0 ? 1 : -1;
                double se = end == 0 ? sp.Path.Start : sp.Path.End;
                double px, pz, ux, uz, nx, nz, tx, tz;
                float y;
                sp.Path.Frame(se, out px, out pz, out y, out ux, out uz, out nx, out nz, out tx, out tz);
                double cl, wl, el, cr, wr, er;
                Side(sp, se, 1, out cl, out wl, out el);
                Side(sp, se, -1, out cr, out wr, out er);
                float depth = sp.Lerp(sp.Depth, se);
                double mid = 0.5 * (el - er);
                double half = 0.5 * (el + er) + 0.2;
                if (sp.Overbridge)
                {
                    // Two round columns under the deck end (the stairs start here).
                    double cs = se + dir * 0.6;
                    for (int k = -1; k <= 1; k += 2)
                    {
                        double ox, oz;
                        Offset(sp, cs, mid + k * (half - 0.45), out ox, out oz);
                        float g = GroundAt(ref x, ox, oz);
                        float yt = sp.Path.YAt(cs) - depth;
                        if (yt - g > 0.4f) BridgeKit.Column(m, ox, oz, 0.22, g - 0.4, yt, x.Lod == 0 ? 12 : 8, sp.Steel, MaterialChannel.Metal, 0.6f, 0.8f);
                    }
                    continue;
                }
                // Ground in front of the abutment (the bank falls away under the span).
                double fx, fz;
                Offset(sp, se + dir * 3.0, mid, out fx, out fz);
                float gFront = GroundAt(ref x, fx, fz);
                float gHere = GroundAt(ref x, px + nx * mid, pz + nz * mid);
                float bottom = Math.Min(gFront, gHere) - 1.2f;
                float topY = y - depth;
                double ax = px + nx * mid + tx * dir * 0.55, az = pz + nz * mid + tz * dir * 0.55;
                double yaw = Math.Atan2(tz, tx);
                int arc = Math.Max(1, x.Arc - 1);
                bool stone = sp.Water;
                if (topY - bottom > 0.3f)
                {
                    BridgeKit.RoundedBox(m, ax, az, yaw, 0.55, half, bottom, topY - 0.3, 0.08, arc, stone ? BridgeStyle.Stone : BridgeStyle.Concrete,
                                         stone ? MaterialChannel.Stone : MaterialChannel.Concrete, 0.55f, 0.6f);
                    // Bearing shelf (a wider concrete seat under the deck).
                    BridgeKit.RoundedBox(m, ax, az, yaw, 0.65, half + 0.12, topY - 0.3, topY, 0.06, arc, BridgeStyle.Concrete, MaterialChannel.Concrete, 0.5f, 0.55f);
                }
                // Splayed wing walls retaining the approach bank on both sides.
                float hFront = y - gFront;
                double len = Math.Max(2.0, Math.Min(10.0, 1.6 * hFront));
                for (int side = -1; side <= 1; side += 2)
                {
                    double off = side > 0 ? el + 0.05 : -(er + 0.05);
                    double sx = px + nx * off, sz = pz + nz * off;
                    // Back along the approach, splayed 30 degrees outward.
                    double dx = -tx * dir * 0.866 + nx * side * 0.5, dz = -tz * dir * 0.866 + nz * side * 0.5;
                    double ex = sx + dx * len, ez = sz + dz * len;
                    float gS = GroundAt(ref x, sx, sz), gE = GroundAt(ref x, ex, ez);
                    float topS = y - 0.05f, topE = Math.Max(gE + 0.45f, Math.Min(topS, gE + 0.45f));
                    if (topS - Math.Min(gS, gE) < 0.25f) continue;
                    Wall(ref x, m, sx, sz, ex, ez, 0.45, topS, topE, Math.Min(gS, bottom + 0.6f) - 0.6f, gE - 0.8f, side * dir,
                         stone ? BridgeStyle.Stone : BridgeStyle.Concrete, stone ? MaterialChannel.Stone : MaterialChannel.Concrete);
                }
            }
        }

        /// <summary>A wall slab from (ax, az) to (bx, bz) of the given thickness (offset to the side
        /// <paramref name="outSign"/> of a→b... grown symmetrically), with a sloping top (topA → topB) and a bottom
        /// (botA → botB): two faces, a chamfered coping and the far end.</summary>
        private static void Wall(ref Ctx x, MeshData m, double ax, double az, double bx, double bz, double thick, float topA, float topB, float botA,
                                 float botB, int outSign, uint c, MaterialChannel ch)
        {
            double dx = bx - ax, dz = bz - az, l = Math.Sqrt(dx * dx + dz * dz);
            if (l < 1e-6) return;
            dx /= l;
            dz /= l;
            double nx = -dz, nz = dx, h = 0.5 * thick, ch2 = 0.06;
            // Faces on both sides.
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                double ox = nx * h * sgn, oz = nz * h * sgn;
                BridgeKit.Quad(m, ax + ox, botA, az + oz, bx + ox, botB, bz + oz, bx + ox, topB - ch2, bz + oz, ax + ox, topA - ch2, az + oz,
                               nx * sgn, 0, nz * sgn, c, ch, 0.6f, 0.85f);
                // Chamfer up to the coping.
                double ix = nx * (h - ch2) * sgn, iz = nz * (h - ch2) * sgn;
                BridgeKit.Quad(m, ax + ox, topA - ch2, az + oz, bx + ox, topB - ch2, bz + oz, bx + ix, topB, bz + iz, ax + ix, topA, az + iz,
                               nx * sgn, 1, nz * sgn, BridgeStyle.Concrete, MaterialChannel.Concrete, 0.95f, 1f);
            }
            double cx = nx * (h - ch2), cz = nz * (h - ch2);
            BridgeKit.Quad(m, ax - cx, topA, az - cz, bx - cx, topB, bz - cz, bx + cx, topB, bz + cz, ax + cx, topA, az + cz, 0, 1, 0,
                           BridgeStyle.Concrete, MaterialChannel.Concrete, 1f, 1f);
            // Far end face.
            BridgeKit.Quad(m, bx - nx * h, botB, bz - nz * h, bx + nx * h, botB, bz + nz * h, bx + nx * h, topB - ch2, bz + nz * h, bx - nx * h, topB - ch2,
                           bz - nz * h, dx, 0, dz, c, ch, 0.6f, 0.9f);
        }

        /// <summary>End walls where an open span meets a fill ramp.</summary>
        private static void Bents(ref Ctx x, BridgeSpan sp, MeshData m)
        {
            foreach (double sb in sp.Bents)
            {
                double px, pz, ux, uz, nx, nz, tx, tz, cl, wl, el, cr, wr, er;
                float y;
                sp.Path.Frame(sb, out px, out pz, out y, out ux, out uz, out nx, out nz, out tx, out tz);
                Side(sp, sb, 1, out cl, out wl, out el);
                Side(sp, sb, -1, out cr, out wr, out er);
                double mid = 0.5 * (el - er), half = 0.5 * (el + er);
                double cx = px + nx * mid, cz = pz + nz * mid;
                float g = GroundAt(ref x, cx, cz);
                float topY = y - sp.Lerp(sp.Depth, sb);
                if (topY - g < 0.3f) continue;
                BridgeKit.RoundedBox(m, cx, cz, Math.Atan2(tz, tx), 0.45, half, g - 0.6, topY, 0.06, Math.Max(1, x.Arc - 1), BridgeStyle.Concrete,
                                     MaterialChannel.Concrete, 0.55f, 0.6f);
            }
        }

        private static void Piers(ref Ctx x, BridgeSpan sp, MeshData m)
        {
            foreach (double s in sp.Piers)
            {
                double px, pz, ux, uz, nx, nz, tx, tz, cl, wl, el, cr, wr, er;
                float y;
                sp.Path.Frame(s, out px, out pz, out y, out ux, out uz, out nx, out nz, out tx, out tz);
                Side(sp, s, 1, out cl, out wl, out el);
                Side(sp, s, -1, out cr, out wr, out er);
                float depth = sp.Lerp(sp.Depth, s);
                float soffit = y - depth;
                double mid = 0.5 * (el - er), deckHalf = 0.5 * (el + er);
                double cx = px + nx * mid, cz = pz + nz * mid;
                double yaw = Math.Atan2(tz, tx); // axis A along the deck, B across
                // Lowest ground under the pier footprint.
                float g = GroundAt(ref x, cx, cz);
                for (int k = -1; k <= 1; k += 2)
                    g = Math.Min(g, GroundAt(ref x, cx + nx * k * deckHalf * 0.7, cz + nz * k * deckHalf * 0.7));
                float foot = g - 1.2f;
                if (soffit - g < 1.0f) continue;
                int sides = x.Lod == 0 ? 16 : x.Lod == 1 ? 8 : 6;
                int arc = x.Lod == 0 ? 3 : x.Lod == 1 ? 2 : 1;
                double gird = sp.Foot ? deckHalf : Math.Max(0.45, deckHalf - Math.Min(1.4, 0.4 * deckHalf));
                if (sp.Water || sp.Foot && !sp.Overbridge)
                {
                    // Wall pier with rounded cutwaters and a cap beam.
                    double t = sp.Foot ? 0.35 : 0.6;
                    double capH = sp.Foot ? 0.3 : 0.7;
                    PierShaft(m, cx, cz, yaw, t, Math.Max(t, gird * 0.9), foot, soffit - capH, arc, 0.08);
                    BridgeKit.RoundedBox(m, cx, cz, yaw, t + 0.15, gird + 0.25, soffit - capH, soffit, 0.1, arc, BridgeStyle.Concrete, MaterialChannel.Concrete, 0.5f, 0.55f);
                }
                else
                {
                    int cols = sp.Overbridge ? 1 : deckHalf < 4.6 ? 1 : deckHalf < 8.5 ? 2 : 3;
                    double r = sp.Overbridge ? 0.3 : cols == 1 ? 0.85 : 0.65;
                    double capH = sp.Overbridge ? 0.35 : 1.0;
                    double capHalf = cols == 1 ? Math.Max(r + 0.3, gird + 0.2) : gird + 0.3;
                    for (int i = 0; i < cols; i++)
                    {
                        double off = cols == 1 ? 0 : (i - 0.5 * (cols - 1)) * (2 * (capHalf - r - 0.4) / Math.Max(1, cols - 1));
                        double ox = cx + nx * off, oz = cz + nz * off;
                        Column(m, ox, oz, r, foot, soffit - capH, sides, sp.Overbridge ? sp.Steel : BridgeStyle.Concrete,
                               sp.Overbridge ? MaterialChannel.Metal : MaterialChannel.Concrete, cols == 1 && !sp.Overbridge);
                    }
                    // Hammerhead or cap beam, its underside bevelled.
                    double[] ly, ins;
                    float[] lao;
                    BridgeKit.Levels(5, out ly, out ins, out lao);
                    double a = r + 0.25, bev = Math.Min(0.35, capH * 0.4);
                    ly[0] = soffit - capH;
                    ly[1] = soffit - capH + bev;
                    ly[2] = soffit - 0.08;
                    ly[3] = soffit - 0.02;
                    ly[4] = soffit;
                    ins[0] = bev;
                    ins[1] = 0;
                    ins[2] = 0;
                    ins[3] = 0.02;
                    ins[4] = 0.06;
                    lao[0] = 0.5f;
                    lao[1] = 0.6f;
                    lao[2] = 0.6f;
                    lao[3] = lao[4] = 0.5f;
                    BridgeKit.Loft(m, cx, cz, yaw, a, capHalf, Math.Min(0.15, a * 0.4), arc, ly, ins, lao, null, 5, true, true,
                                   sp.Overbridge ? sp.Steel : BridgeStyle.Concrete, sp.Overbridge ? MaterialChannel.Metal : MaterialChannel.Concrete);
                }
            }
        }

        /// <summary>A wall pier: a stadium section (semicircular cutwaters) with a footing.</summary>
        private static void PierShaft(MeshData m, double cx, double cz, double yaw, double halfT, double halfW, float y0, double y1, int arc, double bev)
        {
            double[] ly, ins;
            float[] lao;
            BridgeKit.Levels(4, out ly, out ins, out lao);
            ly[0] = y0;
            ly[1] = y0 + 0.8;
            ly[2] = y1 - 0.15;
            ly[3] = y1;
            ins[0] = -0.25;
            ins[1] = -0.05;
            ins[2] = 0;
            ins[3] = 0;
            lao[0] = 0.5f;
            lao[1] = 0.6f;
            lao[2] = 0.7f;
            lao[3] = 0.55f;
            bool[] hard = null;
            BridgeKit.Loft(m, cx, cz, yaw, halfT, halfW, halfT, arc + 1, ly, ins, lao, hard, 4, false, false, BridgeStyle.Concrete, MaterialChannel.Concrete);
        }

        /// <summary>A round column with a plinth and (single columns) a flared top.</summary>
        private static void Column(MeshData m, double cx, double cz, double r, float y0, double y1, int sides, uint c, MaterialChannel ch, bool flare)
        {
            double[] ly, ins;
            float[] lao;
            BridgeKit.Levels(6, out ly, out ins, out lao);
            double h = y1 - y0;
            int n = 0;
            ly[n] = y0;
            ins[n] = -0.12;
            lao[n++] = 0.5f;
            ly[n] = y0 + Math.Min(1.4, 0.2 * h);
            ins[n] = -0.08;
            lao[n++] = 0.55f;
            ly[n] = y0 + Math.Min(1.5, 0.2 * h) + 0.08;
            ins[n] = 0;
            lao[n++] = 0.6f;
            if (flare)
            {
                ly[n] = y1 - Math.Min(1.6, 0.3 * h);
                ins[n] = 0;
                lao[n++] = 0.75f;
                ly[n] = y1;
                ins[n] = -0.45;
                lao[n++] = 0.55f;
            }
            else
            {
                ly[n] = y1;
                ins[n] = 0;
                lao[n++] = 0.7f;
            }
            int seg = Math.Max(1, sides / 4 - 1);
            BridgeKit.Loft(m, cx, cz, 0, r, r, r, seg, ly, ins, lao, null, n, false, true, c, ch);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Lamps, roofs, stairs, trenches
        // ---------------------------------------------------------------------------------------------------------

        private static void Lamps(ref Ctx x, BridgeSpan sp, MeshData m)
        {
            float top = Top(sp);
            double height = sp.Kind == RoadStructureKind.Flyover || RoadWidthModel.IsMajor(sp.Record.RoadClass) ? 8.0 : 6.5;
            double baseH = sp.Railing == RailingStyle.CrashBarrier ? 0.85 : PlinthHeight(sp, x.Lod);
            Tube3(5);
            foreach (BridgeLamp lamp in sp.Lamps)
            {
                double px, pz, yaw, x0, z0, ux, uz, nx, nz, tx, tz;
                float y;
                PostFrame(sp, lamp.S, lamp.Side, 0, out px, out pz, out y, out yaw);
                sp.Path.Frame(lamp.S, out x0, out z0, out y, out ux, out uz, out nx, out nz, out tx, out tz);
                double ix = -nx * lamp.Side, iz = -nz * lamp.Side; // toward the carriageway
                // A concrete pilaster in the railing line (the rails run into it), then the pole on its cap.
                double pb = y + top - 0.02, pt = y + top + Math.Max(baseH, sp.Railing == RailingStyle.CrashBarrier ? 0.85 : sp.RailHeight) + 0.08;
                if (x.Lod == 0) Post(m, px, pz, yaw, 0.24, pb, pt, x.Lod, BridgeStyle.Whitewash, MaterialChannel.Concrete);
                double b = pt + (x.Lod == 0 ? 0.04 : 0);
                double[] ly, ins;
                float[] lao;
                BridgeKit.Levels(5, out ly, out ins, out lao);
                ly[0] = b;
                ly[1] = b + 0.3;
                ly[2] = b + 0.36;
                ly[3] = b + 1.0;
                ly[4] = b + height;
                ins[0] = 0;
                ins[1] = 0;
                ins[2] = 0.07;
                ins[3] = 0.08;
                ins[4] = 0.11;
                lao[0] = 0.85f;
                lao[1] = lao[2] = lao[3] = lao[4] = 1f;
                BridgeKit.Loft(m, px, pz, 0, 0.17, 0.17, 0.17, 1, ly, ins, lao, null, 5, false, true, BridgeStyle.LampGrey, MaterialChannel.Metal);
                // Curved arm over the carriageway and an LED head.
                double ht = b + height;
                int pts;
                if (x.Lod == 0)
                {
                    _tx[0] = px; _ty[0] = ht - 0.35; _tz[0] = pz;
                    _tx[1] = px + ix * 0.25; _ty[1] = ht + 0.05; _tz[1] = pz + iz * 0.25;
                    _tx[2] = px + ix * 0.8; _ty[2] = ht + 0.22; _tz[2] = pz + iz * 0.8;
                    _tx[3] = px + ix * 1.4; _ty[3] = ht + 0.26; _tz[3] = pz + iz * 1.4;
                    _tx[4] = px + ix * 1.85; _ty[4] = ht + 0.22; _tz[4] = pz + iz * 1.85;
                    pts = 5;
                }
                else
                {
                    _tx[0] = px; _ty[0] = ht - 0.1; _tz[0] = pz;
                    _tx[1] = px + ix * 1.85; _ty[1] = ht + 0.2; _tz[1] = pz + iz * 1.85;
                    pts = 2;
                }
                BridgeKit.Tube(m, _tx, _ty, _tz, pts, 0.05, x.Lod == 0 ? 6 : 4, false, BridgeStyle.LampGrey, MaterialChannel.Metal, 1f);
                double hx = px + ix * 2.1, hz = pz + iz * 2.1, hy = ht + 0.2;
                double hyaw = Math.Atan2(iz, ix);
                BridgeKit.RoundedBox(m, hx, hz, hyaw, 0.36, 0.15, hy - 0.05, hy + 0.09, 0.05, 1, BridgeStyle.LampGrey, MaterialChannel.Metal, 0.9f, 1f);
                // The LED lens: a flat panel under the head.
                double cy = Math.Cos(hyaw), sy = Math.Sin(hyaw);
                double ax = cy * 0.28, az = sy * 0.28, bx = -sy * 0.1, bz = cy * 0.1;
                BridgeKit.Quad(m, hx - ax - bx, hy - 0.055, hz - az - bz, hx + ax - bx, hy - 0.055, hz + az - bz, hx + ax + bx, hy - 0.055, hz + az + bz,
                               hx - ax + bx, hy - 0.055, hz - az + bz, 0, -1, 0, BridgeStyle.LampHead, MaterialChannel.Glass, 1f, 1f);
            }
        }

        /// <summary>A barrel roof on posts over a foot overbridge.</summary>
        private static void Roof(ref Ctx x, BridgeSpan sp, MeshData m)
        {
            float top = Top(sp);
            double h0 = 2.55, rise = 0.45;
            uint roof = (BridgeStyle.Hash(sp.Record.OsmWayId, 0x524F4F43) & 1) == 0 ? BridgeStyle.RoofBlue : BridgeStyle.RoofGreen;
            // Posts at every other truss post.
            List<double> st = Anchor();
            BridgeLayout.AnchoredStations(sp.Path, sp.Path.Start, sp.Path.End, 4.0, x.X0, x.Z0, st);
            Tube3(2);
            foreach (double s in st)
                for (int side = -1; side <= 1; side += 2)
                {
                    double px, pz, yaw;
                    float y;
                    PostFrame(sp, s, side, -0.08, out px, out pz, out y, out yaw);
                    _tx[0] = _tx[1] = px;
                    _tz[0] = _tz[1] = pz;
                    _ty[0] = y + top + sp.RailHeight;
                    _ty[1] = y + top + h0 + 0.05;
                    BridgeKit.Tube(m, _tx, _ty, _tz, 2, 0.045, x.Lod == 0 ? 6 : 4, false, sp.Steel, MaterialChannel.Metal, 0.9f);
                }
            // The roof sheet: a thin arched section swept along the deck (upper face out, lower face in).
            int ns = BridgeKit.BuildStations(sp.Path, sp.Path.Start, sp.Path.End, 0, 0, x.X0, x.Z0);
            if (_sl == null || _sl.Length < ns) _sl = new double[Math.Max(ns, 256)];
            Array.Copy(BridgeKit.Stations, _sl, ns);
            int seg = x.Lod == 0 ? 8 : 4;
            int prev = -1;
            for (int k = 0; k < ns; k++)
            {
                double s = _sl[k], cl, wl, el, cr, wr, er;
                Side(sp, s, 1, out cl, out wl, out el);
                Side(sp, s, -1, out cr, out wr, out er);
                double l = wl + BridgeStyle.RailBaseM + 0.3, r = -(wr + BridgeStyle.RailBaseM + 0.3);
                BridgeProfile p = _p.Clear();
                for (int i = 0; i <= seg; i++)
                {
                    double f = (double)i / seg;
                    p.Add(r + (l - r) * f, h0 + rise * Math.Sin(Math.PI * f), 1f);
                }
                p.Hard();
                for (int i = seg; i >= 0; i--)
                {
                    double f = (double)i / seg;
                    p.Add(r + (l - r) * f, h0 - 0.06 + rise * Math.Sin(Math.PI * f), 0.7f);
                }
                p.Hard().Add(r, h0, 1f);
                p.Finish();
                int ring = BridgeKit.Ring(m, sp.Path, s, p, 0, top, roof, MaterialChannel.Metal, 1f);
                if (prev >= 0) BridgeKit.Band(m, prev, ring, p.Count);
                prev = ring;
            }
        }

        /// <summary>A straight stair flight with landings, side stringers, soffits, handrails and landing columns.</summary>
        private static void Stair(ref Ctx x, BridgeSpan sp, BridgeStair st, MeshData m)
        {
            double dx = st.Dx, dz = st.Dz, nx = -dz, nz = dx, hw = st.HalfWidth;
            double rise = (st.TopY - st.BottomY) / Math.Max(1, st.Steps);
            double a = 0;
            uint conc = BridgeStyle.Concrete, side = sp.Steel;
            if (x.Lod >= 2)
            {
                // Far: the flight as one sloped slab with side plates.
                double ex = st.X + dx * st.Length, ez = st.Z + dz * st.Length;
                BridgeKit.Quad(m, st.X - nx * hw, st.TopY, st.Z - nz * hw, ex - nx * hw, st.BottomY, ez - nz * hw, ex + nx * hw, st.BottomY, ez + nz * hw,
                               st.X + nx * hw, st.TopY, st.Z + nz * hw, 0, 1, 0, conc, MaterialChannel.Concrete, 0.95f, 0.95f);
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    double ox = nx * hw * sgn, oz = nz * hw * sgn;
                    BridgeKit.Quad(m, st.X + ox, st.TopY - 0.35, st.Z + oz, ex + ox, st.BottomY - 0.35, ez + oz, ex + ox, st.BottomY + 0.9, ez + oz,
                                   st.X + ox, st.TopY + 0.9, st.Z + oz, nx * sgn, 0, nz * sgn, side, MaterialChannel.Metal, 0.8f, 0.9f);
                }
                BridgeKit.Quad(m, st.X - nx * hw, st.TopY - 0.35, st.Z - nz * hw, st.X + nx * hw, st.TopY - 0.35, st.Z + nz * hw, ex + nx * hw, st.BottomY - 0.35,
                               ez + nz * hw, ex - nx * hw, st.BottomY - 0.35, ez - nz * hw, 0, -1, 0, BridgeStyle.ConcreteSoffit, MaterialChannel.Concrete, 0.5f, 0.5f);
                return;
            }
            for (int i = 0; i < st.Steps; i++)
            {
                bool landing = (i + 1) % BridgeStyle.StairsPerFlight == 0 && i < st.Steps - 1;
                double len = landing ? 1.5 : BridgeStyle.StairTreadM;
                double yTop = st.TopY - (i + 1) * rise;
                double x0 = st.X + dx * a, z0 = st.Z + dz * a, x1 = st.X + dx * (a + len), z1 = st.Z + dz * (a + len);
                // Riser (faces back up the flight) and tread.
                BridgeKit.Quad(m, x0 - nx * hw, yTop, z0 - nz * hw, x0 + nx * hw, yTop, z0 + nz * hw, x0 + nx * hw, yTop + rise, z0 + nz * hw,
                               x0 - nx * hw, yTop + rise, z0 - nz * hw, -dx, 0, -dz, MeshColor.Scale(conc, 0.9f), MaterialChannel.Concrete, 0.8f, 0.95f);
                BridgeKit.Quad(m, x0 - nx * hw, yTop, z0 - nz * hw, x1 - nx * hw, yTop, z1 - nz * hw, x1 + nx * hw, yTop, z1 + nz * hw,
                               x0 + nx * hw, yTop, z0 + nz * hw, 0, 1, 0, conc, MaterialChannel.Concrete, 0.95f, 0.95f);
                // Stringers (side plates) and the soffit.
                double yb = yTop - 0.3;
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    double ox = nx * hw * sgn, oz = nz * hw * sgn;
                    BridgeKit.Quad(m, x0 + ox, yb, z0 + oz, x1 + ox, yb, z1 + oz, x1 + ox, yTop + rise, z1 + oz, x0 + ox, yTop + rise, z0 + oz, nx * sgn, 0, nz * sgn,
                                   side, MaterialChannel.Metal, 0.8f, 0.9f);
                }
                BridgeKit.Quad(m, x0 - nx * hw, yb, z0 - nz * hw, x0 + nx * hw, yb, z0 + nz * hw, x1 + nx * hw, yb, z1 + nz * hw, x1 - nx * hw, yb, z1 - nz * hw,
                               0, -1, 0, BridgeStyle.ConcreteSoffit, MaterialChannel.Concrete, 0.5f, 0.5f);
                if (landing && x.O.Supports)
                {
                    double cx = st.X + dx * (a + 0.75), cz = st.Z + dz * (a + 0.75);
                    float g = GroundAt(ref x, cx, cz);
                    if (yb - g > 0.5) BridgeKit.Column(m, cx, cz, 0.18, g - 0.3, yb, x.Lod == 0 ? 10 : 6, side, MaterialChannel.Metal, 0.6f, 0.8f);
                }
                a += len;
            }
            if (x.Lod >= 2) return;
            // Handrails on both sides, following the flight.
            Tube3(4);
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                double ox = nx * (hw - 0.05) * sgn, oz = nz * (hw - 0.05) * sgn;
                _tx[0] = st.X + ox;
                _tz[0] = st.Z + oz;
                _ty[0] = st.TopY + 1.0;
                _tx[1] = st.X + dx * st.Length + ox;
                _tz[1] = st.Z + dz * st.Length + oz;
                _ty[1] = st.BottomY + 1.0;
                BridgeKit.Tube(m, _tx, _ty, _tz, 2, 0.035, x.Lod == 0 ? 6 : 4, true, side, MaterialChannel.Metal, 0.95f);
                for (double t = 0.4; t < st.Length; t += 1.6)
                {
                    double yy = st.TopY + (st.BottomY - st.TopY) * t / st.Length;
                    _tx[0] = _tx[1] = st.X + dx * t + ox;
                    _tz[0] = _tz[1] = st.Z + dz * t + oz;
                    _ty[0] = yy - 0.05;
                    _ty[1] = yy + 1.0;
                    BridgeKit.Tube(m, _tx, _ty, _tz, 2, 0.025, 4, false, side, MaterialChannel.Metal, 0.9f);
                }
            }
        }

        /// <summary>Steps on the steep stretches of a foot deck (stairs mapped as part of the way).</summary>
        private static void SteepSteps(ref Ctx x, BridgeSpan sp, MeshData m)
        {
            if (!sp.Foot) return;
            BridgePath path = sp.Path;
            for (int k = 0; k + 1 < sp.Count; k++)
            {
                if (!sp.Steps[k]) continue;
                double ds = path.S[k + 1] - path.S[k];
                float dy = path.Y[k + 1] - path.Y[k];
                int n = Math.Max(2, (int)Math.Round(Math.Abs(dy) / BridgeStyle.StairRiseM));
                for (int i = 0; i < n; i++)
                {
                    double sa = path.S[k] + ds * i / n, sb = path.S[k] + ds * (i + 1) / n;
                    double yHigh = Math.Max(path.YAt(sa), path.YAt(sb));
                    double ax, az, bx, bz, ux, uz, nx, nz, tx, tz, cl, wl, el, cr, wr, er;
                    float y;
                    path.Frame(sa, out ax, out az, out y, out ux, out uz, out nx, out nz, out tx, out tz);
                    path.Frame(sb, out bx, out bz, out y, out ux, out uz, out nx, out nz, out tx, out tz);
                    Side(sp, sa, 1, out cl, out wl, out el);
                    Side(sp, sa, -1, out cr, out wr, out er);
                    BridgeKit.Quad(m, ax - nx * wr, yHigh, az - nz * wr, bx - nx * wr, yHigh, bz - nz * wr, bx + nx * wl, yHigh, bz + nz * wl,
                                   ax + nx * wl, yHigh, az + nz * wl, 0, 1, 0, BridgeStyle.Concrete, MaterialChannel.Concrete, 0.95f, 0.95f);
                }
            }
        }

        /// <summary>Retaining walls along a lowered underpass where it runs below the ground, with a parapet.</summary>
        private static void Trench(Ctx x, BridgeSpan sp, MeshData m)
        {
            if (_p == null)
            {
                _p = new BridgeProfile();
                _q = new BridgeProfile();
            }
            BridgePath path = sp.Path;
            int n = sp.Count;
            int k = 0;
            while (k < n)
            {
                if (sp.Ground[k] - path.Y[k] < 0.5f)
                {
                    k++;
                    continue;
                }
                int a = k;
                while (k + 1 < n && sp.Ground[k + 1] - path.Y[k + 1] >= 0.5f) k++;
                int b = k;
                k++;
                double s0 = a > 0 ? path.S[a - 1] : path.S[a], s1 = b < n - 1 ? path.S[b + 1] : path.S[b];
                for (int side = -1; side <= 1; side += 2)
                    PartSweep(ref x, sp, Part.TrenchWall, side, s0, s1, x.Lod == 0 ? WallPanelM : 0, BridgeStyle.Concrete,
                              MeshColor.Scale(BridgeStyle.Concrete, 0.93f), MaterialChannel.Concrete, m);
            }
        }
    }
}
