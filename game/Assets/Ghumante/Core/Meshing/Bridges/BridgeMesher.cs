using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.Core.Meshing.Bridges
{
    /// <summary>Options for <see cref="BridgeMesher"/>.</summary>
    public sealed class BridgeOptions
    {
        /// <summary>Level of detail: 0 near (rounded profiles, every post and kerb stripe near the ends, detailed lamps,
        /// lattice towers), 1 mid (plain corners, a quarter of the posts, plain kerbs, simple lamps), 2 far (deck box,
        /// solid parapets, plain piers, no lamps or posts). The sweeps ring the deck at the key stations thinned for
        /// the LOD (<see cref="BridgePath.KeysFor"/>). Pick it per tier and distance with <see cref="LodFor"/>.</summary>
        public int Lod;

        /// <summary>Draw lamp posts.</summary>
        public bool Lamps = true;

        /// <summary>Draw piers, abutments, wing walls and bents.</summary>
        public bool Supports = true;

        /// <summary>Draw roofs over foot overbridges (where the structure's hash gives one).</summary>
        public bool Roofs = true;

        /// <summary>Draw underpass trench walls.</summary>
        public bool Trenches = true;

        /// <summary>Distance (m, camera to the span) within which a span is drawn at LOD0 on Mid and High; Low never
        /// draws LOD0.</summary>
        public static readonly double[] Lod0RadiusM = { 0, 30, 90 };

        /// <summary>Distance within which a span is drawn at LOD1 (beyond: LOD2), per tier Low / Mid / High.</summary>
        public static readonly double[] Lod1RadiusM = { 60, 250, 400 };

        /// <summary>The LOD of a span <paramref name="distanceM"/> from the camera on tier 0 (Low), 1 (Mid) or 2
        /// (High), so the bridges stay inside their share of the W2_DESIGN §10.4 roads slice (docs/research/w2/
        /// bridges_flyovers.md §5): Low never builds LOD0.</summary>
        public static int LodFor(int tier, double distanceM)
        {
            tier = tier < 0 ? 0 : tier > 2 ? 2 : tier;
            if (distanceM < Lod0RadiusM[tier]) return 0;
            return distanceM < Lod1RadiusM[tier] ? 1 : 2;
        }
    }

    /// <summary>
    /// The structure of every elevated road piece of a tile (decisions 3 and 4 of docs/W2_DETAIL_CONTRACT.md; the road
    /// mesher draws the driving surface at the deck height): a deck slab with rounded fascia and drip edges over
    /// T-girders or a box, raised walkways with painted kerbs, railings on both sides in the span's
    /// <see cref="RailingStyle"/> (RCC balustrade, pipe rails, crash parapet with handrail, steel truss with mesh,
    /// heritage stone or brick parapet), median barriers where twin decks meet, end pillars, abutments with splayed wing
    /// walls at real ends (kept out of other roads), rounded piers (wall piers with cutwaters in rivers, hammerhead or
    /// multi-column piers over roads) kept out of the roads below, flyover ramps as fill between panelled retaining
    /// walls, lamp posts with curved arms, foot overbridge trusses, roofs, landings with end railings and stairs (the
    /// mapped steps or a generated flight), suspension footbridges (towers, cables, hangers) and through arches, and
    /// underpass trench walls. Nothing is drawn over a road the deck leaves open (<see cref="BridgeSpan.Openings"/>)
    /// and no railing crosses a junction or a stair entry. Every vertex carries UV0 = (<see cref="MaterialChannel"/>,
    /// baked AO). Positions are tile-local metres with absolute Y (like the road mesher); a span cut by a tile border
    /// ends exactly on the border with the same cross-section the neighbour starts with. Deterministic; thread-safe
    /// for distinct meshes.
    /// </summary>
    public static class BridgeMesher
    {
        /// <summary>At LOD2 the slab top sits this much lower than the deck surface (under the road surfacing), so the
        /// thinned far sweep (<see cref="BridgePath.Far"/>) never pokes through the road.</summary>
        public const float FarTopDropM = 0.06f;

        private const double KerbPaintM = 1.5;
        private const double WallPanelM = 4.5;

        /// <summary>Diagnostics hook (tests and budget probes): called with a part name and the triangles it added.
        /// Null in the game.</summary>
        internal static Action<string, int> Trace = null;

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
            public bool KeyOnly;
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

        /// <summary>Plan distance (m) from tile-local point (x, z) to a span's centreline, for <see cref="BridgeOptions.LodFor"/>.</summary>
        public static double Distance(BridgeSpan sp, double x, double z)
        {
            double best = double.PositiveInfinity;
            BridgePath p = sp.Path;
            for (int k = 0; k + 1 < p.Count; k++)
            {
                double ax = p.X[k], az = p.Z[k], dx = p.X[k + 1] - ax, dz = p.Z[k + 1] - az, l2 = dx * dx + dz * dz;
                double f = l2 > 1e-12 ? ((x - ax) * dx + (z - az) * dz) / l2 : 0;
                f = f < 0 ? 0 : f > 1 ? 1 : f;
                double qx = ax + dx * f - x, qz = az + dz * f - z;
                best = Math.Min(best, qx * qx + qz * qz);
            }
            return Math.Sqrt(best);
        }

        /// <summary>Mesh one span of a layout (previews, tests, and per-span LOD selection).</summary>
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
            int t0 = m.TriangleCount;
            if (sp.Stair)
            {
                foreach (BridgeStair st in sp.Stairs) Stair(ref x, sp, st, m);
                Mark("stairs", ref t0, m);
                return true;
            }
            Deck(ref x, sp, m);
            Mark("deck", ref t0, m);
            for (int side = -1; side <= 1; side += 2) Walkway(ref x, sp, side, m);
            Mark("walkway", ref t0, m);
            for (int side = -1; side <= 1; side += 2) Railing(ref x, sp, side, m);
            Mark("railing", ref t0, m);
            EndRailings(ref x, sp, m);
            Mark("endrail", ref t0, m);
            if (x.O.Supports)
            {
                Abutments(ref x, sp, m);
                Bents(ref x, sp, m);
                Mark("abutments", ref t0, m);
                Piers(ref x, sp, m);
                OverbridgeColumns(ref x, sp, m);
                Mark("piers", ref t0, m);
            }
            if (x.O.Lamps && x.Lod < 2) Lamps(ref x, sp, m);
            Mark("lamps", ref t0, m);
            if (sp.Overbridge && x.O.Roofs && x.Lod < 2 && (BridgeStyle.Hash(sp.GroupWay, 0x524F4F46) & 1) == 0) Roof(ref x, sp, m);
            Mark("roof", ref t0, m);
            foreach (BridgeStair st in sp.Stairs) Stair(ref x, sp, st, m);
            Mark("stairs", ref t0, m);
            if (sp.Form == BridgeForm.Suspension) Suspension(ref x, sp, m);
            else if (sp.Form == BridgeForm.Arch) Arch(ref x, sp, m);
            Mark("superstructure", ref t0, m);
            return true;
        }

        private static void Mark(string part, ref int t0, MeshData m)
        {
            Action<string, int> trace = Trace;
            if (trace != null) trace(part, m.TriangleCount - t0);
            t0 = m.TriangleCount;
        }

        private static Ctx Context(BridgeLayout layout, IHeightSampler h, BridgeOptions o)
        {
            if (o == null) o = new BridgeOptions();
            int lod = o.Lod < 0 ? 0 : o.Lod > 2 ? 2 : o.Lod;
            return new Ctx
            {
                L = layout, H = h ?? layout.Ground, X0 = layout.Tile.Tile.X0, Z0 = layout.Tile.Tile.Z0, Lod = lod, Arc = lod == 0 ? 2 : lod == 1 ? 1 : 0,
                KeyOnly = true, O = o,
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

        /// <summary>True when the span end at along <paramref name="s"/> continues into a neighbour tile or another
        /// deck (no pillar, no cap, posts run on).</summary>
        private static bool OpenEnd(BridgeSpan sp, double s)
        {
            if (Math.Abs(s - sp.Path.Start) < 1e-6) return sp.StartEnd == SpanEnd.Cut || sp.StartEnd == SpanEnd.Junction;
            if (Math.Abs(s - sp.Path.End) < 1e-6) return sp.EndEnd == SpanEnd.Cut || sp.EndEnd == SpanEnd.Junction;
            if (sp.TrimStart > 0 && Math.Abs(s - (sp.Path.Start + sp.TrimStart)) < 1e-6) return true;
            if (sp.TrimEnd > 0 && Math.Abs(s - (sp.Path.End - sp.TrimEnd)) < 1e-6) return true;
            return false;
        }

        /// <summary>True when the end at <paramref name="end"/> (0 start, 1 end) is a real end of the structure (the
        /// approach warning paint and end pillars belong there).</summary>
        private static bool RealEnd(BridgeSpan sp, int end)
        {
            SpanEnd e = end == 0 ? sp.StartEnd : sp.EndEnd;
            return e == SpanEnd.Ground || e == SpanEnd.Landing;
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
            // Deck heights and widths are linear between key stations; only the walls follow the ground in between.
            bool keyOnly = part == Part.FillWall || part == Part.TrenchWall ? x.Lod >= 2 : x.KeyOnly;
            int ns = BridgeKit.BuildStations(sp.Path, s0, s1, 0, stripe, x.X0, x.Z0, keyOnly, x.Lod);
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
                    DeckSection(p, el, er, sp.Lerp(sp.Depth, s), sp.Foot, x.Lod, sp);
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
                    if (arc > 1)
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
        /// slim box for foot decks; a plain box at LOD2), as one outward loop from the right top edge round to the
        /// right fascia.</summary>
        private static void DeckSection(BridgeProfile p, double eL, double eR, double depth, bool foot, int lod, BridgeSpan sp)
        {
            double D = Math.Max(BridgeStyle.MinDepthFor(foot), depth);
            // Rounded fascia and girder near; the mid and far sections keep the silhouette with plain corners (the
            // deck is the costliest sweep, and at 40 m and beyond the bevels are below a pixel or two).
            int arc = lod == 0 ? 2 : 0;
            uint top = BridgeStyle.Concrete, face = BridgeStyle.Concrete, soffit = BridgeStyle.ConcreteSoffit;
            if (foot || lod >= 2)
            {
                bool steel = foot && (sp.Overbridge || sp.Form == BridgeForm.Suspension);
                uint col = steel ? sp.Steel : BridgeStyle.Concrete;
                int ch = steel ? (int)MaterialChannel.Metal : (int)MaterialChannel.Concrete;
                double r = Math.Min(0.12, 0.3 * D);
                double yt = lod >= 2 ? -0.03 - FarTopDropM : -0.03;
                p.Add(-eR, yt, 0.95f, top).Add(eL, yt, 0.95f, top).Hard();
                p.Paint(p.Count - 1, col, ch);
                if (arc > 0) p.Add(eL, -D + r, 0.8f, col, ch).Arc(eL - r, -D + r, r, 0, -0.5 * Math.PI, arc, 0.6f);
                else p.Add(eL, -D, 0.6f, col, ch).Hard();
                int b = p.Count;
                if (arc > 0) p.Arc(-eR + r, -D + r, r, -0.5 * Math.PI, -Math.PI, arc, 0.6f);
                else p.Add(-eR, -D, 0.6f).Hard();
                p.Add(-eR, yt, 0.8f);
                p.Paint(b, col, ch);
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
            if (rd > 0) p.Add(eL, -fd + rd, 0.9f, face).Arc(eL - rd, -fd + rd, rd, 0, -0.5 * Math.PI, 1, 0.75f);
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
            if (rd > 0) p.Arc(-eR + rd, -fd + rd, rd, -0.5 * Math.PI, -Math.PI, 1, 0.75f);
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
                case RailingStyle.Newar: return 0.78;
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
                // Fill pieces reach one station into the open neighbours so the bent closes them.
                double p0 = fill && a > 0 ? path.S[a - 1] : path.S[a], p1 = fill && b < n - 1 ? path.S[b + 1] : path.S[b];
                for (int r = 0; r + 1 < sp.DeckRuns.Count; r += 2)
                {
                    double q0 = Math.Max(p0, sp.DeckRuns[r]), q1 = Math.Min(p1, sp.DeckRuns[r + 1]);
                    if (q1 - q0 < 0.05) continue;
                    if (!fill)
                    {
                        PartSweep(ref x, sp, Part.Deck, 1, q0, q1, 0, BridgeStyle.Concrete, 0, MaterialChannel.Concrete, m);
                        if (!OpenEnd(sp, q0)) DeckCap(ref x, sp, q0, -1, m);
                        if (!OpenEnd(sp, q1)) DeckCap(ref x, sp, q1, +1, m);
                    }
                    else
                    {
                        PartSweep(ref x, sp, Part.FillTop, 1, q0, q1, 0, BridgeStyle.Concrete, 0, MaterialChannel.Concrete, m);
                        for (int side = -1; side <= 1; side += 2)
                            PartSweep(ref x, sp, Part.FillWall, side, q0, q1, x.Lod == 0 ? WallPanelM : 0, BridgeStyle.Concrete,
                                      MeshColor.Scale(BridgeStyle.Concrete, 0.93f), MaterialChannel.Concrete, m);
                    }
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
            if (!sp.RaisedWalk || x.Lod >= 2) return;
            // Black-and-yellow kerb paint near the real ends (the approach warning), plain whitewash beyond.
            double paintTo = RealEnd(sp, 0) ? sp.Path.Start + BridgeStyle.KerbStripeReachM : double.NegativeInfinity;
            double paintFrom = RealEnd(sp, 1) ? sp.Path.End - BridgeStyle.KerbStripeReachM : double.PositiveInfinity;
            // The walkway follows the railing runs: it opens where the railing does (a road beside the deck).
            List<double> runs = side > 0 ? sp.RailRunsL : sp.RailRunsR;
            for (int r = 0; r + 1 < runs.Count; r += 2)
            {
                double a = runs[r], b = runs[r + 1];
                if (x.Lod == 0 && paintTo < paintFrom)
                {
                    Kerb(ref x, sp, side, a, Math.Min(b, paintTo), true, m);
                    Kerb(ref x, sp, side, Math.Max(a, paintTo), Math.Min(b, paintFrom), false, m);
                    Kerb(ref x, sp, side, Math.Max(a, paintFrom), b, true, m);
                }
                else
                {
                    Kerb(ref x, sp, side, a, b, x.Lod == 0, m);
                }
                PartSweep(ref x, sp, Part.Walk, side, a, b, 0, BridgeStyle.DeckPaver, 0, MaterialChannel.Concrete, m);
            }
        }

        private static void Kerb(ref Ctx x, BridgeSpan sp, int side, double a, double b, bool striped, MeshData m)
        {
            if (b - a < 1e-3) return;
            if (striped) PartSweep(ref x, sp, Part.KerbFace, side, a, b, KerbPaintM, BridgeStyle.TrafficYellow, BridgeStyle.TrafficBlack, MaterialChannel.Paint, m);
            else PartSweep(ref x, sp, Part.KerbFace, side, a, b, 0, BridgeStyle.Whitewash, 0, MaterialChannel.Paint, m);
        }

        private static void Railing(ref Ctx x, BridgeSpan sp, int side, MeshData m)
        {
            if (Shared(sp, side))
            {
                // Median barrier where twin decks meet.
                List<double> mruns = side > 0 ? sp.RailRunsL : sp.RailRunsR;
                for (int r = 0; r + 1 < mruns.Count; r += 2)
                {
                    double a = mruns[r], b = mruns[r + 1];
                    PartSweep(ref x, sp, Part.JerseyFoot, side, a, b, 0, BridgeStyle.TrafficYellow, 0, MaterialChannel.Paint, m);
                    PartSweep(ref x, sp, Part.JerseyBody, side, a, b, 0, BridgeStyle.Whitewash, 0, MaterialChannel.Concrete, m);
                }
                return;
            }
            List<double> runs = side > 0 ? sp.RailRunsL : sp.RailRunsR;
            for (int r = 0; r + 1 < runs.Count; r += 2) RailingRun(ref x, sp, side, runs[r], runs[r + 1], m);
        }

        /// <summary>One continuous stretch of railing on one side, in the span's style.</summary>
        private static void RailingRun(ref Ctx x, BridgeSpan sp, int side, double a, double b, MeshData m)
        {
            RailingStyle style = sp.Railing;
            if (x.Lod >= 2)
            {
                // Far: a steel truss is one see-through panel (two faces per bay); the solid styles keep their parapet.
                if (style == RailingStyle.SteelTruss) Fence(ref x, sp, side, a, b, m);
                else
                    PartSweep(ref x, sp, Part.Plinth, side, a, b, 0, style == RailingStyle.Newar ? ParapetColour(sp) : BridgeStyle.Whitewash, 0,
                              style == RailingStyle.Newar ? ParapetChannel(sp) : MaterialChannel.Concrete, m);
                return;
            }
            int t0 = m.TriangleCount;
            switch (style)
            {
                case RailingStyle.ConcreteRail:
                    PartSweep(ref x, sp, Part.Plinth, side, a, b, 0, BridgeStyle.Concrete, 0, MaterialChannel.Concrete, m);
                    if (x.Lod == 0) RectRail(ref x, sp, side, a, b, 0.0, 0.62, 0.06, 0.065, BridgeStyle.Whitewash, MaterialChannel.Paint, m);
                    RectRail(ref x, sp, side, a, b, 0.0, sp.RailHeight - 0.07, 0.11, 0.075, BridgeStyle.Whitewash, MaterialChannel.Paint, m);
                    Posts(ref x, sp, side, a, b, 3.6, 0.3, sp.RailHeight - 0.02, 0.12, BridgeStyle.TrafficYellow, BridgeStyle.TrafficBlack, MaterialChannel.Paint,
                          true, m);
                    break;
                case RailingStyle.PipeRail:
                    PartSweep(ref x, sp, Part.Plinth, side, a, b, 0, BridgeStyle.Whitewash, 0, MaterialChannel.Concrete, m);
                    if (x.Lod == 0) Rail(ref x, sp, side, a, b, 0.0, 0.74, 0.038, sp.Steel, MaterialChannel.Metal, m);
                    Rail(ref x, sp, side, a, b, 0.0, sp.RailHeight - 0.03, 0.045, sp.Steel, MaterialChannel.Metal, m);
                    Posts(ref x, sp, side, a, b, 3.6, 0.42, sp.RailHeight + 0.04, 0.1, BridgeStyle.Whitewash, BridgeStyle.Whitewash, MaterialChannel.Concrete,
                          true, m);
                    break;
                case RailingStyle.CrashBarrier:
                    PartSweep(ref x, sp, Part.JerseyFoot, side, a, b, 0, BridgeStyle.TrafficYellow, 0, MaterialChannel.Paint, m);
                    PartSweep(ref x, sp, Part.JerseyBody, side, a, b, 0, BridgeStyle.Concrete, 0, MaterialChannel.Concrete, m);
                    Rail(ref x, sp, side, a, b, 0.04, 1.2, 0.042, BridgeStyle.Galvanised, MaterialChannel.Metal, m);
                    PostTubes(ref x, sp, side, a, b, 3.0, 0.85, 1.2, 0.03, 0.04, BridgeStyle.Galvanised, m);
                    break;
                case RailingStyle.SteelTruss:
                    PartSweep(ref x, sp, Part.Plinth, side, a, b, 0, BridgeStyle.Concrete, 0, MaterialChannel.Concrete, m);
                    Rail(ref x, sp, side, a, b, -0.08, sp.RailHeight, 0.045, sp.Steel, MaterialChannel.Metal, m);
                    if (x.Lod == 0) Rail(ref x, sp, side, a, b, -0.08, 0.62, 0.028, sp.Steel, MaterialChannel.Metal, m);
                    Rail(ref x, sp, side, a, b, -0.08, 0.17, 0.035, sp.Steel, MaterialChannel.Metal, m);
                    PostTubes(ref x, sp, side, a, b, 2.0, 0.12, sp.RailHeight, -0.08, 0.04, sp.Steel, m);
                    Mesh(ref x, sp, side, a, b, 2.0, m);
                    break;
                case RailingStyle.Newar:
                {
                    // A solid parapet with a flat stone coping (slightly wider than the wall), pilasters every few metres
                    // and end pillars with finials.
                    double hp = PlinthHeight(sp, x.Lod);
                    PartSweep(ref x, sp, Part.Plinth, side, a, b, 0, ParapetColour(sp), 0, ParapetChannel(sp), m);
                    RectRail(ref x, sp, side, a, b, 0.0, hp + 0.04, 0.2, 0.045, BridgeStyle.StoneLight, MaterialChannel.Stone, m);
                    Posts(ref x, sp, side, a, b, 4.8, hp - 0.1, hp + 0.16, 0.2, ParapetColour(sp), ParapetColour(sp), ParapetChannel(sp), true, m);
                    break;
                }
            }
            Mark("railing." + style, ref t0, m);
        }

        /// <summary>A closed rounded-rectangle rail from a to b at height h above the walkway, centred on the railing
        /// base (offset du from its centre).</summary>
        private static void RectRail(ref Ctx x, BridgeSpan sp, int side, double a, double b, double du, double h, double hu, double hy, uint c,
                                     MaterialChannel ch, MeshData m)
        {
            BridgeProfile p = _p;
            p.RoundRect(0, 0, hu, hy, Math.Min(hu, hy) * 0.5, x.Lod == 0 ? 1 : 0, 0.95f);
            LineSweep(ref x, sp, side, du, h, p, c, ch, m, a, b);
        }

        private static void Rail(ref Ctx x, BridgeSpan sp, int side, double a, double b, double du, double h, double r, uint c, MaterialChannel ch, MeshData m,
                                 int sides = 0)
        {
            int n = sides > 0 ? sides : x.Lod == 0 ? 6 : 4;
            _p.Circle(0, 0, r, n, 0.95f);
            LineSweep(ref x, sp, side, du, h, _p, c, ch, m, a, b);
        }

        /// <summary>Sweep a fixed (small) profile along the railing line of one side: at every station the profile
        /// sits at the railing base centre + du (outward), h above the walkway.</summary>
        private static void LineSweep(ref Ctx x, BridgeSpan sp, int side, double du, double h, BridgeProfile p, uint c, MaterialChannel ch, MeshData m,
                                      double s0, double s1)
        {
            int ns = BridgeKit.BuildStations(sp.Path, s0, s1, 0, 0, x.X0, x.Z0, x.KeyOnly, x.Lod);
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

        /// <summary>Posts at world-anchored stations in [a, b] (rounded boxes with a cap), alternating colours c and
        /// c2, from y0 to y1 above the walkway; pillars at the run ends that close the railing (not at a cut or a
        /// junction, where the railing runs on) when <paramref name="endPillars"/>.</summary>
        private static void Posts(ref Ctx x, BridgeSpan sp, int side, double a, double b, double spacing, double y0, double y1, double half, uint c,
                                  uint c2, MaterialChannel ch, bool endPillars, MeshData m)
        {
            int t0 = m.TriangleCount;
            PostsCore(ref x, sp, side, a, b, spacing, y0, y1, half, c, c2, ch, endPillars, m);
            Mark("railing.posts", ref t0, m);
        }

        private static void PostsCore(ref Ctx x, BridgeSpan sp, int side, double a, double b, double spacing, double y0, double y1, double half, uint c,
                                      uint c2, MaterialChannel ch, bool endPillars, MeshData m)
        {
            if (x.Lod == 1) spacing *= 4;
            List<double> st = Anchor();
            BridgeLayout.AnchoredStations(sp.Path, a, b, spacing, x.X0, x.Z0, st);
            float top = Top(sp);
            bool openA = OpenEnd(sp, a), openB = OpenEnd(sp, b);
            for (int i = 0; i < st.Count; i++)
            {
                double s = st[i];
                if (!openA && s - a < 0.8) continue;
                if (!openB && b - s < 0.8) continue;
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
                double se = end == 0 ? a : b;
                if (OpenEnd(sp, se)) continue;
                double s = end == 0 ? a + 0.3 : b - 0.3;
                double px, pz, yaw;
                float y;
                PostFrame(sp, s, side, 0.02, out px, out pz, out y, out yaw);
                bool realEnd = Math.Abs(se - sp.Path.Start) < 1e-6 || Math.Abs(se - sp.Path.End) < 1e-6;
                // (A road beside the bridge head may leave room for a plain post only, or none: BridgeSpan.EndPost.)
                byte room = realEnd ? sp.EndPost[(Math.Abs(se - sp.Path.Start) < 1e-6 ? 0 : 2) + (side > 0 ? 1 : 0)] : (byte)1;
                if (room == 0) continue;
                if (!realEnd || room == 1)
                {
                    // Where the railing stops for a road, a branch or a stair: a plain end post.
                    Post(m, px, pz, yaw, half * 1.3, y + top + y0 - 0.02, y + top + y1 + 0.05, x.Lod, c, ch);
                    continue;
                }
                uint pc = sp.Railing == RailingStyle.Newar ? ParapetColour(sp) : BridgeStyle.Whitewash;
                MaterialChannel pch = sp.Railing == RailingStyle.Newar ? ParapetChannel(sp) : MaterialChannel.Concrete;
                // (A heritage parapet's end pillar rises a little over the wall, the others over the top rail.)
                double ph = sp.Railing == RailingStyle.Newar ? PlinthHeight(sp, x.Lod) + 0.3 : sp.RailHeight + 0.35;
                if (x.Lod >= 1)
                {
                    BridgeKit.Box(m, px, pz, yaw, 0.26, 0.24, y + top - 0.05, y + top + ph + 0.1, pc, pch, 0.75f, 1f);
                    continue;
                }
                BridgeKit.CappedPost(m, px, pz, yaw, 0.25, y + top - 0.05, y + top + ph, 0.0, 0.0, pc, pch, 0.75f, 1f);
                BridgeKit.CappedPost(m, px, pz, yaw, 0.3, y + top + ph, y + top + ph + 0.06, 0.05, 0.03, BridgeStyle.Concrete, MaterialChannel.Concrete, 0.95f,
                                     1f);
                {
                    // A turned finial on the pillar.
                    double[] ly, ins;
                    float[] lao;
                    BridgeKit.Levels(4, out ly, out ins, out lao);
                    double bb = y + top + ph + 0.11;
                    double[] hh = { 0, 0.1, 0.22, 0.3 };
                    double[] rr = { 0.12, 0.08, 0.1, 0.0 };
                    for (int i = 0; i < 4; i++)
                    {
                        ly[i] = bb + hh[i];
                        ins[i] = 0.13 - rr[i];
                        lao[i] = 1f;
                    }
                    uint fc = sp.Railing == RailingStyle.Newar ? BridgeStyle.StoneLight : BridgeStyle.TrafficYellow;
                    BridgeKit.Loft(m, px, pz, 0, 0.13, 0.13, 0.13, 1, ly, ins, lao, null, 4, false, false, fc,
                                   sp.Railing == RailingStyle.Newar ? MaterialChannel.Stone : MaterialChannel.Paint);
                }
            }
        }

        /// <summary>A rounded square post with a moulded cap (one loft: shaft, cap lip and bevelled top; LOD1 a plain
        /// shaft).</summary>
        private static void Post(MeshData m, double px, double pz, double yaw, double half, double y0, double y1, int lod, uint c, MaterialChannel ch)
        {
            if (lod >= 1)
            {
                BridgeKit.Box(m, px, pz, yaw, half, half, y0, y1, c, ch, 0.75f, 1f);
                return;
            }
            BridgeKit.CappedPost(m, px, pz, yaw, half, y0, y1 - 0.05, 0.09, half, c, ch, 0.75f, 1f);
        }

        /// <summary>A rounded post with a bevelled cap (lamp pilasters, seen from close by): one loft.</summary>
        private static void RoundPost(MeshData m, double px, double pz, double yaw, double half, double y0, double y1, uint c, MaterialChannel ch)
        {
            double[] ly, ins;
            float[] lao;
            BridgeKit.Levels(3, out ly, out ins, out lao);
            ly[0] = y0;
            ins[0] = 0;
            lao[0] = 0.75f;
            ly[1] = y1 - 0.03;
            ins[1] = 0;
            lao[1] = 0.95f;
            ly[2] = y1 + 0.04;
            ins[2] = 0.045;
            lao[2] = 1f;
            BridgeKit.Loft(m, px, pz, yaw, half, half, 0.035, 1, ly, ins, lao, null, 3, false, true, c, ch);
        }

        /// <summary>Round post tubes on the railing line in [a, b], from y0 to y1 above the walkway.</summary>
        private static void PostTubes(ref Ctx x, BridgeSpan sp, int side, double a, double b, double spacing, double y0, double y1, double du, double r,
                                      uint c, MeshData m)
        {
            if (x.Lod == 1) spacing *= 3;
            List<double> st = Anchor();
            BridgeLayout.AnchoredStations(sp.Path, a, b, spacing, x.X0, x.Z0, st);
            // The run ends get a post too (where the railing stops at a gap or an end).
            if (!OpenEnd(sp, a) && (st.Count == 0 || st[0] - a > 0.4)) st.Insert(0, a + 0.05);
            if (!OpenEnd(sp, b) && (st.Count == 0 || b - st[st.Count - 1] > 0.4)) st.Add(b - 0.05);
            float top = Top(sp);
            Tube3(2);
            int sides = x.Lod == 0 ? 5 : 3;
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

        /// <summary>Mesh panels between the posts of a steel railing in [a, b] (double-sided thin quads) and, on foot
        /// overbridges at LOD0, the Warren truss diagonals.</summary>
        private static void Mesh(ref Ctx x, BridgeSpan sp, int side, double a0, double b0, double spacing, MeshData m)
        {
            if (x.Lod == 1) spacing *= 2;
            List<double> st = Anchor();
            BridgeLayout.AnchoredStations(sp.Path, a0, b0, spacing, x.X0, x.Z0, st);
            float top = Top(sp);
            uint panel = PanelColour(sp);
            Tube3(2);
            // Include the run ends so the first and last bays are filled.
            for (int i = -1; i < st.Count; i++)
            {
                double a = i < 0 ? a0 : st[i], b = i + 1 < st.Count ? st[i + 1] : b0;
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
                    BridgeKit.Tube(m, _tx, _ty, _tz, 2, 0.032, 4, false, sp.Steel, MaterialChannel.Metal, 0.9f);
                }
            }
        }

        /// <summary>The heritage parapet's wall: grey stone at the ghats, Newar brick in the old town cores.</summary>
        private static uint ParapetColour(BridgeSpan sp)
        {
            return sp.StoneParapet ? BridgeStyle.Stone : BridgeStyle.Brick;
        }

        private static MaterialChannel ParapetChannel(BridgeSpan sp)
        {
            return sp.StoneParapet ? MaterialChannel.Stone : MaterialChannel.Brick;
        }

        /// <summary>Mesh panel colour: galvanised chain-link on suspension footbridges, dark painted mesh elsewhere.</summary>
        private static uint PanelColour(BridgeSpan sp)
        {
            return sp.Form == BridgeForm.Suspension ? BridgeStyle.MeshGalvanised : BridgeStyle.MeshPanel;
        }

        /// <summary>The far railing of a steel truss in [a0, b0]: a double-sided panel from the walkway to the top
        /// rail, one bay per key station of the path.</summary>
        private static void Fence(ref Ctx x, BridgeSpan sp, int side, double a0, double b0, MeshData m)
        {
            List<double> st = Anchor();
            st.Clear();
            st.Add(a0);
            bool[] keys = sp.Path.KeysFor(x.Lod);
            for (int k = 0; k < sp.Path.Count; k++)
            {
                double s = sp.Path.S[k];
                if (s > a0 + 0.3 && s < b0 - 0.3 && keys[k]) st.Add(s);
            }
            st.Add(b0);
            float top = Top(sp);
            for (int i = 0; i + 1 < st.Count; i++)
            {
                double ax, az, bx, bz, yaw;
                float ya, yb;
                PostFrame(sp, st[i], side, -0.08, out ax, out az, out ya, out yaw);
                PostFrame(sp, st[i + 1], side, -0.08, out bx, out bz, out yb, out yaw);
                double nx = bz - az, nz = -(bx - ax), nl = Math.Sqrt(nx * nx + nz * nz);
                if (nl < 1e-9) continue;
                nx /= nl;
                nz /= nl;
                double y0a = ya + top, y1a = ya + top + sp.RailHeight, y0b = yb + top, y1b = yb + top + sp.RailHeight;
                BridgeKit.Quad(m, ax, y0a, az, bx, y0b, bz, bx, y1b, bz, ax, y1a, az, nx, 0, nz, PanelColour(sp), MaterialChannel.Metal, 0.8f, 0.9f);
                BridgeKit.Quad(m, ax, y0a, az, bx, y0b, bz, bx, y1b, bz, ax, y1a, az, -nx, 0, -nz, PanelColour(sp), MaterialChannel.Metal, 0.8f, 0.9f);
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

        /// <summary>A railing across a landing end (stairs leave it to the side): plinth, rails and corner posts in
        /// the span's style, from one side railing to the other.</summary>
        private static void EndRailings(ref Ctx x, BridgeSpan sp, MeshData m)
        {
            for (int end = 0; end < 2; end++)
            {
                if ((end == 0 ? sp.StartEnd : sp.EndEnd) != SpanEnd.Landing) continue;
                double s = end == 0 ? sp.Path.Start : sp.Path.End;
                if (!sp.HasDeck(s + (end == 0 ? 0.3 : -0.3))) continue; // the end lies in an opening
                double px, pz, ux, uz, nx, nz, tx, tz;
                float y;
                sp.Path.Frame(s, out px, out pz, out y, out ux, out uz, out nx, out nz, out tx, out tz);
                int dir = end == 0 ? 1 : -1;
                double inset = 0.5 * BridgeStyle.RailBaseM * dir;
                double cl, wl, el, cr, wr, er;
                Side(sp, s, 1, out cl, out wl, out el);
                Side(sp, s, -1, out cr, out wr, out er);
                double mid = 0.5 * (wl - wr), half = 0.5 * (wl + wr) + BridgeStyle.RailBaseM;
                double cx = px + tx * inset + nx * mid, cz = pz + tz * inset + nz * mid;
                double yaw = Math.Atan2(tz, tx);
                float top = Top(sp);
                double hp = PlinthHeight(sp, x.Lod);
                bool newar = sp.Railing == RailingStyle.Newar;
                bool jersey = sp.Railing == RailingStyle.CrashBarrier;
                if (jersey) hp = 0.85;
                if (x.Lod == 0)
                    BridgeKit.RoundedBox(m, cx, cz, yaw, 0.5 * BridgeStyle.RailBaseM, half, y + top - 0.03, y + top + hp, 0.05, 1,
                                         newar ? ParapetColour(sp) : BridgeStyle.Concrete, newar ? ParapetChannel(sp) : MaterialChannel.Concrete, 0.8f, 1f);
                else
                    BridgeKit.Box(m, cx, cz, yaw, 0.5 * BridgeStyle.RailBaseM, half, y + top - 0.03, y + top + hp,
                                  newar ? ParapetColour(sp) : BridgeStyle.Concrete, newar ? ParapetChannel(sp) : MaterialChannel.Concrete, 0.8f, 1f);
                if (x.Lod >= 2 || newar) continue; // (the heritage parapet runs across the landing end solid)
                double ax = cx - nx * half, az = cz - nz * half, bx = cx + nx * half, bz = cz + nz * half;
                uint rc = sp.Railing == RailingStyle.ConcreteRail ? BridgeStyle.Whitewash : sp.Railing == RailingStyle.CrashBarrier
                    ? BridgeStyle.Galvanised : sp.Steel;
                MaterialChannel rch = sp.Railing == RailingStyle.ConcreteRail ? MaterialChannel.Paint : MaterialChannel.Metal;
                Tube3(2);
                double[] heights = { jersey ? 1.2 : sp.RailHeight - 0.03, 0.62 };
                int rails = x.Lod == 0 && !jersey ? 2 : 1;
                for (int i = 0; i < rails; i++)
                {
                    _tx[0] = ax;
                    _tz[0] = az;
                    _tx[1] = bx;
                    _tz[1] = bz;
                    _ty[0] = _ty[1] = y + top + heights[i];
                    BridgeKit.Tube(m, _tx, _ty, _tz, 2, 0.045, x.Lod == 0 ? 6 : 3, x.Lod == 0, rc, rch, 0.95f);
                }
                if (sp.Railing == RailingStyle.SteelTruss)
                {
                    double y0 = y + top + 0.2, y1 = y + top + sp.RailHeight - 0.08;
                    BridgeKit.Quad(m, ax, y0, az, bx, y0, bz, bx, y1, bz, ax, y1, az, tx * dir, 0, tz * dir, PanelColour(sp), MaterialChannel.Metal, 0.8f, 0.9f);
                    BridgeKit.Quad(m, ax, y0, az, bx, y0, bz, bx, y1, bz, ax, y1, az, -tx * dir, 0, -tz * dir, PanelColour(sp), MaterialChannel.Metal, 0.8f,
                                   0.9f);
                }
                // Corner posts.
                for (int k = -1; k <= 1; k += 2)
                {
                    double qx = cx + nx * k * (half - 0.12), qz = cz + nz * k * (half - 0.12);
                    if (jersey) continue;
                    Post(m, qx, qz, yaw, 0.1, y + top + hp - 0.02, y + top + sp.RailHeight + 0.06, x.Lod, BridgeStyle.Whitewash, MaterialChannel.Concrete);
                }
            }
        }

        private static List<double> Anchor()
        {
            return _anchor ?? (_anchor = new List<double>());
        }

        private static void Tube3(int n)
        {
            if (_tx != null && _tx.Length >= n) return;
            int cap = Math.Max(n, 32);
            _tx = new double[cap];
            _ty = new double[cap];
            _tz = new double[cap];
        }

        // ---------------------------------------------------------------------------------------------------------
        // Supports
        // ---------------------------------------------------------------------------------------------------------

        private static void Abutments(ref Ctx x, BridgeSpan sp, MeshData m)
        {
            foreach (BridgeAbutment ab in sp.Abutments)
            {
                int dir = ab.Dir;
                double se = ab.S;
                double px, pz, ux, uz, nx, nz, tx, tz;
                float y;
                sp.Path.Frame(se, out px, out pz, out y, out ux, out uz, out nx, out nz, out tx, out tz);
                double cl, wl, el, cr, wr, er;
                Side(sp, se, 1, out cl, out wl, out el);
                Side(sp, se, -1, out cr, out wr, out er);
                float depth = sp.Lerp(sp.Depth, se);
                double mid = 0.5 * (el - er);
                double half = 0.5 * (el + er) + 0.2;
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
                    if (x.Lod >= 2)
                    {
                        BridgeKit.Box(m, ax, az, yaw, 0.6, half + 0.1, bottom, topY, stone ? BridgeStyle.Stone : BridgeStyle.Concrete,
                                      stone ? MaterialChannel.Stone : MaterialChannel.Concrete, 0.55f, 0.6f);
                        continue;
                    }
                    if (x.Lod == 0)
                        BridgeKit.CappedBlock(m, ax, az, yaw, 0.55, half, bottom, topY - 0.36, 0.06, 0.05, stone ? BridgeStyle.Stone : BridgeStyle.Concrete,
                                              stone ? MaterialChannel.Stone : MaterialChannel.Concrete, 0.55f, 0.6f);
                    else
                        BridgeKit.Box(m, ax, az, yaw, 0.55, half, bottom, topY - 0.3, stone ? BridgeStyle.Stone : BridgeStyle.Concrete,
                                      stone ? MaterialChannel.Stone : MaterialChannel.Concrete, 0.55f, 0.6f);
                    // Bearing shelf (a wider concrete seat under the deck).
                    BridgeKit.Box(m, ax, az, yaw, 0.65, half + 0.12, topY - 0.3, topY, BridgeStyle.Concrete, MaterialChannel.Concrete, 0.5f, 0.55f);
                }
                if (!ab.Wings || x.Lod >= 2) continue;
                // Splayed wing walls retaining the approach bank on both sides (as long as other roads allow).
                for (int side = -1; side <= 1; side += 2)
                {
                    double len = side > 0 ? ab.WingL : ab.WingR;
                    if (len < 1.0) continue;
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

        /// <summary>A wall slab from (ax, az) to (bx, bz) of the given thickness (grown symmetrically about the line),
        /// with a sloping top (topA → topB) and a bottom (botA → botB): two faces, a chamfered coping and the far end.</summary>
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
                if (x.Lod == 0)
                    BridgeKit.RoundedBox(m, cx, cz, Math.Atan2(tz, tx), 0.45, half, g - 0.6, topY, 0.06, 1, BridgeStyle.Concrete, MaterialChannel.Concrete, 0.55f, 0.6f);
                else
                    BridgeKit.Box(m, cx, cz, Math.Atan2(tz, tx), 0.45, half, g - 0.6, topY, BridgeStyle.Concrete, MaterialChannel.Concrete, 0.55f, 0.6f);
            }
        }

        /// <summary>Two round columns under each foot overbridge end that is not a junction (the stairs leave here).</summary>
        private static void OverbridgeColumns(ref Ctx x, BridgeSpan sp, MeshData m)
        {
            if (!sp.Overbridge) return;
            for (int end = 0; end < 2; end++)
            {
                if (!RealEnd(sp, end)) continue;
                int dir = end == 0 ? 1 : -1;
                double se = end == 0 ? sp.Path.Start : sp.Path.End;
                if (!sp.HasDeck(se + dir * 0.6)) continue; // the end lies in an opening
                double cs = se + dir * 0.6;
                double cl, wl, el, cr, wr, er;
                Side(sp, se, 1, out cl, out wl, out el);
                Side(sp, se, -1, out cr, out wr, out er);
                double mid = 0.5 * (el - er), half = 0.5 * (el + er) + 0.2;
                float depth = sp.Lerp(sp.Depth, se);
                for (int k = -1; k <= 1; k += 2)
                {
                    double ox, oz;
                    Offset(sp, cs, mid + k * (half - 0.45), out ox, out oz);
                    float g = GroundAt(ref x, ox, oz);
                    float yt = sp.Path.YAt(cs) - depth;
                    if (yt - g > 0.4f) BridgeKit.Column(m, ox, oz, 0.22, g - 0.4, yt, x.Lod == 0 ? 10 : x.Lod == 1 ? 6 : 4, sp.Steel, MaterialChannel.Metal, 0.6f, 0.8f);
                }
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
                int sides = x.Lod == 0 ? 12 : x.Lod == 1 ? 8 : 6;
                int arc = x.Lod == 0 ? 2 : 1;
                double gird = sp.Foot ? deckHalf : Math.Max(0.45, deckHalf - Math.Min(1.4, 0.4 * deckHalf));
                if (sp.Water || sp.Foot && !sp.Overbridge)
                {
                    // Wall pier with rounded cutwaters and a cap beam.
                    double t = sp.Foot ? 0.35 : 0.6;
                    double capH = sp.Foot ? 0.3 : 0.7;
                    if (x.Lod >= 2)
                    {
                        BridgeKit.Box(m, cx, cz, yaw, t, Math.Max(t, gird * 0.9) + 0.25, foot, soffit, BridgeStyle.Concrete, MaterialChannel.Concrete, 0.5f, 0.6f);
                        continue;
                    }
                    PierShaft(m, cx, cz, yaw, t, Math.Max(t, gird * 0.9), foot, soffit - capH, arc, x.Lod);
                    // The cap beam: chamfered on top at LOD0.
                    if (x.Lod == 0)
                        BridgeKit.CappedBlock(m, cx, cz, yaw, t + 0.15, gird + 0.25, soffit - capH, soffit - 0.1, 0.1, 0.08, BridgeStyle.Concrete,
                                              MaterialChannel.Concrete, 0.5f, 0.55f);
                    else
                        BridgeKit.Box(m, cx, cz, yaw, t + 0.15, gird + 0.25, soffit - capH, soffit, BridgeStyle.Concrete, MaterialChannel.Concrete, 0.5f, 0.55f);
                }
                else
                {
                    int cols = sp.Overbridge ? 1 : deckHalf < 4.6 ? 1 : deckHalf < 8.5 ? 2 : 3;
                    double r = sp.Overbridge ? 0.3 : cols == 1 ? 0.85 : 0.65;
                    double capH = sp.Overbridge ? 0.35 : 1.0;
                    double capHalf = cols == 1 ? Math.Max(r + 0.3, gird + 0.2) : gird + 0.3;
                    uint pc = sp.Overbridge ? sp.Steel : BridgeStyle.Concrete;
                    MaterialChannel pch = sp.Overbridge ? MaterialChannel.Metal : MaterialChannel.Concrete;
                    for (int i = 0; i < cols; i++)
                    {
                        double off = cols == 1 ? 0 : (i - 0.5 * (cols - 1)) * (2 * (capHalf - r - 0.4) / Math.Max(1, cols - 1));
                        double ox = cx + nx * off, oz = cz + nz * off;
                        if (x.Lod >= 2) BridgeKit.Box(m, ox, oz, yaw, r, r, foot, soffit - capH, pc, pch, 0.5f, 0.7f);
                        else if (x.Lod == 1) BridgeKit.Column(m, ox, oz, r, foot, soffit - capH, 8, pc, pch, 0.5f, 0.7f);
                        else Column(m, ox, oz, r, foot, soffit - capH, sides, pc, pch, cols == 1 && !sp.Overbridge);
                    }
                    if (x.Lod >= 1)
                    {
                        BridgeKit.Box(m, cx, cz, yaw, r + 0.25, capHalf, soffit - capH, soffit, pc, pch, 0.5f, 0.6f);
                        continue;
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

        /// <summary>A wall pier: a stadium section (semicircular cutwaters) with a footing (LOD1: the shaft alone).</summary>
        private static void PierShaft(MeshData m, double cx, double cz, double yaw, double halfT, double halfW, float y0, double y1, int arc, int lod)
        {
            double[] ly, ins;
            float[] lao;
            if (lod >= 1)
            {
                BridgeKit.Levels(2, out ly, out ins, out lao);
                ly[0] = y0;
                ly[1] = y1;
                ins[0] = ins[1] = 0;
                lao[0] = 0.5f;
                lao[1] = 0.65f;
                BridgeKit.Loft(m, cx, cz, yaw, halfT, halfW, halfT, 1, ly, ins, lao, null, 2, false, false, BridgeStyle.Concrete, MaterialChannel.Concrete);
                return;
            }
            BridgeKit.Levels(3, out ly, out ins, out lao);
            ly[0] = y0;
            ly[1] = y0 + 0.8;
            ly[2] = y1;
            ins[0] = -0.25;
            ins[1] = 0;
            ins[2] = 0;
            lao[0] = 0.5f;
            lao[1] = 0.6f;
            lao[2] = 0.65f;
            BridgeKit.Loft(m, cx, cz, yaw, halfT, halfW, halfT, 2, ly, ins, lao, null, 3, false, false, BridgeStyle.Concrete, MaterialChannel.Concrete);
        }

        /// <summary>A round column with a plinth and (single columns) a flared top.</summary>
        private static void Column(MeshData m, double cx, double cz, double r, float y0, double y1, int sides, uint c, MaterialChannel ch, bool flare)
        {
            double[] ly, ins;
            float[] lao;
            BridgeKit.Levels(6, out ly, out ins, out lao);
            double h = y1 - y0;
            int n = 0;
            // A footing collar, the shaft and (single columns) a flared head under the cap.
            ly[n] = y0;
            ins[n] = -0.1;
            lao[n++] = 0.5f;
            ly[n] = y0 + Math.Min(1.5, 0.2 * h);
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
        // Lamps, roofs, stairs, superstructures, trenches
        // ---------------------------------------------------------------------------------------------------------

        private static void Lamps(ref Ctx x, BridgeSpan sp, MeshData m)
        {
            float top = Top(sp);
            double height = BridgeLayout.LampHeightM(sp);
            double baseH = sp.Railing == RailingStyle.CrashBarrier ? 0.85 : PlinthHeight(sp, x.Lod);
            Tube3(5);
            foreach (BridgeLamp lamp in sp.Lamps)
            {
                double px, pz, yaw, x0, z0, ux, uz, nx, nz, tx, tz;
                float y;
                PostFrame(sp, lamp.S, lamp.Side, 0, out px, out pz, out y, out yaw);
                sp.Path.Frame(lamp.S, out x0, out z0, out y, out ux, out uz, out nx, out nz, out tx, out tz);
                double ix = -nx * lamp.Side, iz = -nz * lamp.Side; // toward the carriageway
                // A concrete pilaster in the railing line (the rails run into it), then the pole on its cap: rounded at
                // LOD0, a plain four-sided block at LOD1, so the pole never floats at rail height.
                double pb = y + top - 0.02, pt = y + top + Math.Max(baseH, sp.Railing == RailingStyle.CrashBarrier ? 0.85 : sp.RailHeight) + 0.08;
                if (x.Lod == 0) RoundPost(m, px, pz, yaw, 0.24, pb, pt, BridgeStyle.Whitewash, MaterialChannel.Concrete);
                else Post(m, px, pz, yaw, 0.24, pb, pt, x.Lod, BridgeStyle.Whitewash, MaterialChannel.Concrete);
                double b = pt + (x.Lod == 0 ? 0.04 : 0);
                double[] ly, ins;
                float[] lao;
                int lv = 2;
                BridgeKit.Levels(5, out ly, out ins, out lao);
                {
                    ly[0] = b;
                    ly[1] = b + height;
                    ins[0] = 0.06;
                    ins[1] = 0.11;
                    lao[0] = 0.85f;
                    lao[1] = 1f;
                }
                BridgeKit.Loft(m, px, pz, 0, 0.17, 0.17, 0.17, 1, ly, ins, lao, null, lv, false, true, BridgeStyle.LampGrey, MaterialChannel.Metal);
                // Curved arm over the carriageway and an LED head.
                double ht = b + height;
                int pts;
                if (x.Lod == 0)
                {
                    _tx[0] = px; _ty[0] = ht - 0.35; _tz[0] = pz;
                    _tx[1] = px + ix * 0.35; _ty[1] = ht + 0.1; _tz[1] = pz + iz * 0.35;
                    _tx[2] = px + ix * 1.1; _ty[2] = ht + 0.26; _tz[2] = pz + iz * 1.1;
                    _tx[3] = px + ix * 1.85; _ty[3] = ht + 0.22; _tz[3] = pz + iz * 1.85;
                    pts = 4;
                }
                else
                {
                    _tx[0] = px; _ty[0] = ht - 0.1; _tz[0] = pz;
                    _tx[1] = px + ix * 1.85; _ty[1] = ht + 0.2; _tz[1] = pz + iz * 1.85;
                    pts = 2;
                }
                BridgeKit.Tube(m, _tx, _ty, _tz, pts, 0.05, x.Lod == 0 ? 5 : 3, false, BridgeStyle.LampGrey, MaterialChannel.Metal, 1f);
                double hx = px + ix * 2.1, hz = pz + iz * 2.1, hy = ht + 0.2;
                double hyaw = Math.Atan2(iz, ix);
                if (x.Lod == 0) BridgeKit.CappedPost(m, hx, hz, hyaw, 0.17, hy - 0.05, hy + 0.04, 0.05, 0.06, BridgeStyle.LampGrey, MaterialChannel.Metal, 0.9f, 1f);
                else BridgeKit.Box(m, hx, hz, hyaw, 0.36, 0.15, hy - 0.05, hy + 0.09, BridgeStyle.LampGrey, MaterialChannel.Metal, 0.9f, 1f);
                // The LED lens: a flat panel under the head.
                double cy = Math.Cos(hyaw), sy = Math.Sin(hyaw);
                double ax = cy * 0.28, az = sy * 0.28, bx = -sy * 0.1, bz = cy * 0.1;
                BridgeKit.Quad(m, hx - ax - bx, hy - 0.055, hz - az - bz, hx + ax - bx, hy - 0.055, hz + az - bz, hx + ax + bx, hy - 0.055, hz + az + bz,
                               hx - ax + bx, hy - 0.055, hz - az + bz, 0, -1, 0, BridgeStyle.LampHead, MaterialChannel.Glass, 1f, 1f);
            }
        }

        /// <summary>A barrel roof on posts over a foot overbridge (over its deck runs).</summary>
        private static void Roof(ref Ctx x, BridgeSpan sp, MeshData m)
        {
            float top = Top(sp);
            double h0 = 2.55, rise = 0.45;
            uint roof = (BridgeStyle.Hash(sp.GroupWay, 0x524F4F43) & 1) == 0 ? BridgeStyle.RoofBlue : BridgeStyle.RoofGreen;
            int seg = x.Lod == 0 ? 6 : 3;
            for (int r = 0; r + 1 < sp.DeckRuns.Count; r += 2)
            {
                double a = sp.DeckRuns[r], b = sp.DeckRuns[r + 1];
                // Posts every 4 m on both sides.
                List<double> st = Anchor();
                BridgeLayout.AnchoredStations(sp.Path, a, b, 4.0, x.X0, x.Z0, st);
                Tube3(2);
                foreach (double s in st)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        if (!sp.HasRail(side, s)) continue;
                        double px, pz, yaw;
                        float y;
                        PostFrame(sp, s, side, -0.08, out px, out pz, out y, out yaw);
                        _tx[0] = _tx[1] = px;
                        _tz[0] = _tz[1] = pz;
                        _ty[0] = y + top + sp.RailHeight;
                        _ty[1] = y + top + h0 + 0.05;
                        BridgeKit.Tube(m, _tx, _ty, _tz, 2, 0.045, 4, false, sp.Steel, MaterialChannel.Metal, 0.9f);
                    }
                // The roof sheet: a thin arched section swept along the deck (upper face out, lower face in).
                int ns = BridgeKit.BuildStations(sp.Path, a, b, 0, 0, x.X0, x.Z0, x.KeyOnly, x.Lod);
                if (_sl == null || _sl.Length < ns) _sl = new double[Math.Max(ns, 256)];
                Array.Copy(BridgeKit.Stations, _sl, ns);
                int prev = -1;
                for (int k = 0; k < ns; k++)
                {
                    double s = _sl[k], cl, wl, el, cr, wr, er;
                    Side(sp, s, 1, out cl, out wl, out el);
                    Side(sp, s, -1, out cr, out wr, out er);
                    double l = wl + BridgeStyle.RailBaseM + 0.3, rr = -(wr + BridgeStyle.RailBaseM + 0.3);
                    BridgeProfile p = _p.Clear();
                    for (int i = 0; i <= seg; i++)
                    {
                        double f = (double)i / seg;
                        p.Add(rr + (l - rr) * f, h0 + rise * Math.Sin(Math.PI * f), 1f);
                    }
                    p.Hard();
                    for (int i = seg; i >= 0; i--)
                    {
                        double f = (double)i / seg;
                        p.Add(rr + (l - rr) * f, h0 - 0.06 + rise * Math.Sin(Math.PI * f), 0.7f);
                    }
                    p.Hard().Add(rr, h0, 1f);
                    p.Finish();
                    int ring = BridgeKit.Ring(m, sp.Path, s, p, 0, top, roof, MaterialChannel.Metal, 1f);
                    if (prev >= 0) BridgeKit.Band(m, prev, ring, p.Count);
                    prev = ring;
                }
            }
        }

        /// <summary>A stair flight from its <see cref="BridgeStair"/> profile (risers, treads and landings exactly
        /// where <see cref="BridgeStair.SurfaceAt"/> puts the walkable surface), with side stringers, soffits,
        /// handrails following the nosing line and columns under landings; a landing pad is a slab on a column.</summary>
        private static void Stair(ref Ctx x, BridgeSpan sp, BridgeStair st, MeshData m)
        {
            double dx = st.Dx, dz = st.Dz, nx = -dz, nz = dx, hw = st.HalfWidth;
            uint conc = BridgeStyle.Concrete, side = sp.Steel;
            if (st.Steps == 0)
            {
                double cx = st.X + dx * 0.5 * st.Length, cz = st.Z + dz * 0.5 * st.Length;
                BridgeKit.RoundedBox(m, cx, cz, Math.Atan2(dz, dx), 0.5 * st.Length, hw, st.TopY - 0.3, st.TopY, 0.04, 1, conc, MaterialChannel.Concrete, 0.6f,
                                     0.95f);
                float g = GroundAt(ref x, cx, cz);
                if (x.O.Supports && st.TopY - 0.3f - g > 0.5f)
                    BridgeKit.Column(m, cx, cz, 0.18, g - 0.3, st.TopY - 0.3, x.Lod == 0 ? 8 : x.Lod == 1 ? 6 : 4, side, MaterialChannel.Metal, 0.6f, 0.8f);
                return;
            }
            if (x.Lod >= 1)
            {
                // Mid and far: the flight as one sloped slab with side plates (the steps cannot be told apart there).
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
            float rise = st.Rise;
            double a = 0;
            for (int i = 0; i < st.Steps; i++)
            {
                double len = st.TreadLength(i);
                bool landing = st.IsLanding(i);
                double yTop = st.TreadY(i);
                double x0 = st.X + dx * a, z0 = st.Z + dz * a, x1 = st.X + dx * (a + len), z1 = st.Z + dz * (a + len);
                // Riser (faces back up the flight) and tread.
                BridgeKit.Quad(m, x0 - nx * hw, yTop, z0 - nz * hw, x0 + nx * hw, yTop, z0 + nz * hw, x0 + nx * hw, yTop + rise, z0 + nz * hw,
                               x0 - nx * hw, yTop + rise, z0 - nz * hw, -dx, 0, -dz, MeshColor.Scale(conc, 0.9f), MaterialChannel.Concrete, 0.8f, 0.95f);
                BridgeKit.Quad(m, x0 - nx * hw, yTop, z0 - nz * hw, x1 - nx * hw, yTop, z1 - nz * hw, x1 + nx * hw, yTop, z1 + nz * hw,
                               x0 + nx * hw, yTop, z0 + nz * hw, 0, 1, 0, conc, MaterialChannel.Concrete, 0.95f, 0.95f);
                bool column = landing || st.Landings == 0 && i == st.Steps / 2 && st.Length > 4.0;
                if (column && x.O.Supports)
                {
                    double cx = st.X + dx * (a + 0.5 * len), cz = st.Z + dz * (a + 0.5 * len);
                    float g = GroundAt(ref x, cx, cz);
                    double yb = yTop - 0.3;
                    if (yb - g > 0.5) BridgeKit.Column(m, cx, cz, 0.18, g - 0.3, yb, x.Lod == 0 ? 8 : x.Lod == 1 ? 6 : 4, side, MaterialChannel.Metal, 0.6f, 0.8f);
                }
                a += len;
            }
            // Stringer plates on both sides and the soffit, one sloped piece per run between landings (from the
            // nosing line down to 0.3 m under it).
            double ra = 0;
            int stepsDone = 0;
            while (stepsDone < st.Steps)
            {
                int i0 = stepsDone;
                double a0 = ra;
                // A run of ordinary treads up to and including the next landing or the last tread.
                int i1 = i0;
                double a1 = a0;
                while (i1 < st.Steps)
                {
                    a1 += st.TreadLength(i1);
                    bool stop = st.IsLanding(i1) || i1 == st.Steps - 1;
                    i1++;
                    if (stop) break;
                }
                stepsDone = i1;
                ra = a1;
                float ya = st.SurfaceAt(a0), yb = st.TreadY(i1 - 1);
                double xa = st.X + dx * a0, za = st.Z + dz * a0, xb = st.X + dx * a1, zb = st.Z + dz * a1;
                if (i1 == st.Steps && yb - GroundAt(ref x, xb, zb) < 0.25) yb = Math.Max(yb, ya - 0.01f); // the last tread is the ground
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    double ox = nx * hw * sgn, oz = nz * hw * sgn;
                    BridgeKit.Quad(m, xa + ox, ya - 0.3, za + oz, xb + ox, yb - 0.3, zb + oz, xb + ox, yb + 0.02, zb + oz, xa + ox, ya + 0.02, za + oz, nx * sgn, 0,
                                   nz * sgn, side, MaterialChannel.Metal, 0.8f, 0.9f);
                }
                BridgeKit.Quad(m, xa - nx * hw, ya - 0.3, za - nz * hw, xa + nx * hw, ya - 0.3, za + nz * hw, xb + nx * hw, yb - 0.3, zb + nz * hw, xb - nx * hw,
                               yb - 0.3, zb - nz * hw, 0, -1, 0, BridgeStyle.ConcreteSoffit, MaterialChannel.Concrete, 0.5f, 0.5f);
            }
            // Handrails on both sides, 0.95 m over the walking line (through the landing corners).
            int np = 0;
            Tube3(4 + 2 * st.Landings + 4);
            double at = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                double ox = nx * (hw - 0.05) * (pass == 0 ? -1 : 1), oz = nz * (hw - 0.05) * (pass == 0 ? -1 : 1);
                np = 0;
                at = 0;
                AddRailPoint(st, ox, oz, 0, ref np);
                for (int i = 0; i < st.Steps; i++)
                {
                    double len = st.TreadLength(i);
                    if (len > st.Tread + 1e-6)
                    {
                        AddRailPoint(st, ox, oz, at + st.Tread, ref np);
                        AddRailPoint(st, ox, oz, at + len, ref np);
                    }
                    at += len;
                }
                if (np < 2 || Math.Abs(_tx[np - 1] - (st.X + dx * st.Length + ox)) + Math.Abs(_tz[np - 1] - (st.Z + dz * st.Length + oz)) > 1e-3)
                    AddRailPoint(st, ox, oz, st.Length, ref np);
                BridgeKit.Tube(m, _tx, _ty, _tz, np, 0.035, x.Lod == 0 ? 6 : 4, true, side, MaterialChannel.Metal, 0.95f);
            }
            // Balusters every 2.4 m.
            double step = 2.4;
            for (int pass = 0; pass < 2; pass++)
            {
                double ox = nx * (hw - 0.05) * (pass == 0 ? -1 : 1), oz = nz * (hw - 0.05) * (pass == 0 ? -1 : 1);
                for (double t = 0.4; t < st.Length; t += step)
                {
                    double yy = st.SurfaceAt(t);
                    _tx[0] = _tx[1] = st.X + dx * t + ox;
                    _tz[0] = _tz[1] = st.Z + dz * t + oz;
                    _ty[0] = yy - 0.05;
                    _ty[1] = yy + 0.95;
                    BridgeKit.Tube(m, _tx, _ty, _tz, 2, 0.025, 4, false, side, MaterialChannel.Metal, 0.9f);
                }
            }
        }

        private static void AddRailPoint(BridgeStair st, double ox, double oz, double along, ref int np)
        {
            if (np >= _tx.Length) return;
            _tx[np] = st.X + st.Dx * along + ox;
            _tz[np] = st.Z + st.Dz * along + oz;
            _ty[np] = st.SurfaceAt(along) + 0.95;
            np++;
        }

        /// <summary>
        /// A suspension footbridge (jhulunge pul) over each deck run (the river span between the banks; a road left
        /// open at a bank ends it): a tower portal at each end of the run that is not a tile cut (two steel columns on
        /// concrete bases with a cross beam), a main cable each side hanging from the tower tops to handrail height
        /// along the middle of the span, vertical hangers down to the deck edge, and backstays to anchor blocks behind
        /// the towers. No river piers (the layout places none). The cable shape near a tower depends only on the
        /// distance to that tower, so the halves of a span cut by a tile border meet when the border lies more than
        /// <c>RiseM</c> from both towers.
        /// </summary>
        /// <summary>A suspension footbridge over its main run (<see cref="BridgeSpan.SuspA"/>..<see cref="BridgeSpan.SuspB"/>,
        /// at the towers where the layout found room for them): main cables and hangers, towers, backstays as the
        /// layout allows.</summary>
        private static void Suspension(ref Ctx x, BridgeSpan sp, MeshData m)
        {
            if (double.IsNaN(sp.SuspA)) return;
            SuspensionRun(ref x, sp, sp.SuspA, sp.SuspB, sp.TowerA, sp.TowerB, m);
        }

        private static void SuspensionRun(ref Ctx x, BridgeSpan sp, double a, double b, bool towerA, bool towerB, MeshData m)
        {
            const double RiseM = 25.0;
            const double LowM = 1.15;
            double len = b - a;
            // The valley's towers stand tall over the deck (about 9 to 12 m for 60 to 130 m spans).
            double towerH = Math.Max(5.0, Math.Min(11.0, 5.5 + 0.045 * len));
            Tube3(128);
            for (int side = -1; side <= 1; side += 2)
            {
                // Main cable: sampled every few metres (LOD2: the ends and the middle only).
                int np = 0;
                double step = x.Lod == 2 ? Math.Max(1, len / 2) : x.Lod == 0 ? 3.0 : 6.0;
                for (double s = a; ; s += step)
                {
                    if (s > b) s = b;
                    double cx, cz;
                    float cy;
                    CablePoint(sp, s, side, a, b, towerA, towerB, RiseM, LowM, towerH, out cx, out cz, out cy);
                    if (np < _tx.Length)
                    {
                        _tx[np] = cx;
                        _ty[np] = cy;
                        _tz[np] = cz;
                        np++;
                    }
                    if (s >= b) break;
                }
                BridgeKit.Tube(m, _tx, _ty, _tz, np, 0.05, x.Lod == 0 ? 6 : 4, false, BridgeStyle.CableGrey, MaterialChannel.Metal, 0.95f);
                // Hangers (flat double-sided straps, cheap) from the cable to the deck edge.
                if (x.Lod < 2)
                {
                    List<double> st = Anchor();
                    BridgeLayout.AnchoredStations(sp.Path, a + 1.0, b - 1.0, x.Lod == 0 ? 1.5 : 3.0, x.X0, x.Z0, st);
                    foreach (double s in st)
                    {
                        double cx, cz, px, pz, yaw;
                        float cy, y;
                        CablePoint(sp, s, side, a, b, towerA, towerB, RiseM, LowM, towerH, out cx, out cz, out cy);
                        PostFrame(sp, s, side, 0, out px, out pz, out y, out yaw);
                        double tx = Math.Cos(yaw), tz = Math.Sin(yaw);
                        for (int f = -1; f <= 1; f += 2)
                            BridgeKit.Quad(m, cx - tx * 0.012, cy, cz - tz * 0.012, cx + tx * 0.012, cy, cz + tz * 0.012, px + tx * 0.012, y + 0.05, pz + tz * 0.012,
                                           px - tx * 0.012, y + 0.05, pz - tz * 0.012, -tz * f * side, 0, tx * f * side, BridgeStyle.CableGrey, MaterialChannel.Metal,
                                           0.9f, 0.9f);
                    }
                }
            }
            // Towers, backstays and anchors.
            for (int end = 0; end < 2; end++)
            {
                if (end == 0 ? !towerA : !towerB) continue;
                double s = end == 0 ? a : b;
                int dir = end == 0 ? -1 : 1; // outward
                double px, pz, ux, uz, nx, nz, tx, tz;
                float y;
                sp.Path.Frame(s, out px, out pz, out y, out ux, out uz, out nx, out nz, out tx, out tz);
                double cl, wl, el, cr, wr, er;
                Side(sp, s, 1, out cl, out wl, out el);
                Side(sp, s, -1, out cr, out wr, out er);
                float top = (float)(y + towerH + LowM);
                for (int side = -1; side <= 1; side += 2)
                {
                    double off = side > 0 ? el + 0.18 : -(er + 0.18);
                    double ox = px + nx * off, oz = pz + nz * off;
                    float g = GroundAt(ref x, ox, oz);
                    float baseTop = Math.Max(g + 0.6f, y - 0.4f);
                    double yawT = Math.Atan2(tz, tx);
                    BridgeKit.CappedBlock(m, ox, oz, yawT, 0.5, 0.5, g - 0.8, baseTop - 0.08, 0.08, 0.08, BridgeStyle.Concrete, MaterialChannel.Concrete, 0.55f,
                                          0.8f);
                    if (x.Lod >= 2)
                    {
                        BridgeKit.Box(m, ox, oz, yawT, 0.16, 0.16, baseTop, top + 0.25, sp.Steel, MaterialChannel.Metal, 0.8f, 1f);
                        continue;
                    }
                    // The valley's suspension footbridges stand on painted steel lattice towers (a portal of two lattice
                    // legs); mid and far a plain column.
                    if (x.Lod == 0) LatticeLeg(m, ox, oz, yawT, baseTop, top + 0.25, 0.42, 0.2, sp.Steel);
                    else BridgeKit.Column(m, ox, oz, 0.16, baseTop, top + 0.25, 8, sp.Steel, MaterialChannel.Metal, 0.8f, 1f);
                    // Backstay to an anchor block behind the tower (as long as the roads around allow, else none).
                    double stay = end == 0 ? sp.BackstayA : sp.BackstayB;
                    if (stay <= 0) continue;
                    double bx = ox + tx * dir * stay + nx * side * 0.6, bz = oz + tz * dir * stay + nz * side * 0.6;
                    float gb = GroundAt(ref x, bx, bz);
                    _tx[0] = ox;
                    _ty[0] = top;
                    _tz[0] = oz;
                    _tx[1] = bx;
                    _ty[1] = gb + 0.5;
                    _tz[1] = bz;
                    BridgeKit.Tube(m, _tx, _ty, _tz, 2, 0.045, 4, false, BridgeStyle.CableGrey, MaterialChannel.Metal, 0.9f);
                    BridgeKit.CappedBlock(m, bx, bz, yawT, 0.7, 0.6, gb - 0.6, gb + 0.5, 0.1, 0.1, BridgeStyle.Concrete, MaterialChannel.Concrete, 0.6f, 0.85f);
                }
                // Portal beam across the tower tops, and (LOD0, where it clears a walker) a tie beam lower down.
                double mid = 0.5 * (el - er);
                double bxm = px + nx * mid, bzm = pz + nz * mid;
                BridgeKit.Box(m, bxm, bzm, Math.Atan2(tz, tx), 0.16, 0.5 * (el + er) + 0.4, top + 0.25, top + 0.55, sp.Steel, MaterialChannel.Metal, 0.9f, 1f);
                if (x.Lod == 0 && top - y > 4.2f)
                {
                    BridgeKit.Box(m, bxm, bzm, Math.Atan2(tz, tx), 0.1, 0.5 * (el + er) + 0.3, y + 3.2, y + 3.4, sp.Steel, MaterialChannel.Metal, 0.9f, 1f);
                    // The big X of the portal between the tie and the top beam (flat bars, both faces).
                    double lx = px + nx * (el + 0.18), lz = pz + nz * (el + 0.18), rx = px - nx * (er + 0.18), rz = pz - nz * (er + 0.18);
                    for (int d = 0; d < 2; d++)
                    {
                        double ax = d == 0 ? lx : rx, az = d == 0 ? lz : rz, bx2 = d == 0 ? rx : lx, bz2 = d == 0 ? rz : lz;
                        double ya = y + 3.4, yb = top + 0.25;
                        double dx = bx2 - ax, dy = yb - ya, dz = bz2 - az, dl = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                        // Across the bar in the portal plane (the plane holds the deck normal and the vertical).
                        double wx = -dy * nx / dl * 0.035, wy = (dx * nx + dz * nz) / dl * 0.035, wz = -dy * nz / dl * 0.035;
                        for (int f = -1; f <= 1; f += 2)
                            BridgeKit.Quad(m, ax - wx, ya - wy, az - wz, bx2 - wx, yb - wy, bz2 - wz, bx2 + wx, yb + wy, bz2 + wz, ax + wx, ya + wy, az + wz, tx * f, 0,
                                           tz * f, sp.Steel, MaterialChannel.Metal, 0.85f, 0.9f);
                    }
                }
            }
        }

        /// <summary>Corners of a lattice leg's section in order around it: (+A, +B), (+A, −B), (−A, −B), (−A, +B); B is
        /// A turned +90°.</summary>
        private static readonly double[] LegCornerA = { 1, 1, -1, -1 }, LegCornerB = { 1, -1, -1, 1 };

        /// <summary>A steel lattice tower leg about (cx, cz): four corner angles on a square section tapering from
        /// <paramref name="half0"/> at y0 to <paramref name="half1"/> at y1 (axis A at <paramref name="yaw"/>), and zig-zag
        /// lacing on all four faces every 0.9 m (flat bars facing out).</summary>
        private static void LatticeLeg(MeshData m, double cx, double cz, double yaw, double y0, double y1, double half0, double half1, uint c)
        {
            double ca = Math.Cos(yaw), sa = Math.Sin(yaw);
            double[] ka = LegCornerA, kb = LegCornerB;
            Tube3(2);
            for (int i = 0; i < 4; i++)
            {
                _tx[0] = cx + (ka[i] * ca - kb[i] * sa) * half0;
                _tz[0] = cz + (ka[i] * sa + kb[i] * ca) * half0;
                _tx[1] = cx + (ka[i] * ca - kb[i] * sa) * half1;
                _tz[1] = cz + (ka[i] * sa + kb[i] * ca) * half1;
                _ty[0] = y0;
                _ty[1] = y1;
                BridgeKit.Tube(m, _tx, _ty, _tz, 2, 0.04, 4, false, c, MaterialChannel.Metal, 0.85f);
            }
            int levels = Math.Max(2, (int)Math.Round((y1 - y0) / 0.9));
            for (int f = 0; f < 4; f++)
            {
                int i = f, j = (f + 1) % 4;
                // Outward normal of the face: the mean of its two corner directions.
                double fa = 0.5 * (ka[i] + ka[j]), fb = 0.5 * (kb[i] + kb[j]);
                double onx = fa * ca - fb * sa, onz = fa * sa + fb * ca;
                for (int l = 0; l < levels; l++)
                {
                    double t0 = (double)l / levels, t1 = (double)(l + 1) / levels;
                    double h0 = half0 + (half1 - half0) * t0, h1 = half0 + (half1 - half0) * t1;
                    int p = (l & 1) == 0 ? i : j, q = (l & 1) == 0 ? j : i;
                    double ax = cx + (ka[p] * ca - kb[p] * sa) * h0, az = cz + (ka[p] * sa + kb[p] * ca) * h0;
                    double bx = cx + (ka[q] * ca - kb[q] * sa) * h1, bz = cz + (ka[q] * sa + kb[q] * ca) * h1;
                    double ya = y0 + (y1 - y0) * t0, yb = y0 + (y1 - y0) * t1;
                    // A flat bar 5 cm wide along the diagonal, in the face plane.
                    double dx = bx - ax, dy = yb - ya, dz = bz - az, dl = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    if (dl < 1e-6) continue;
                    // Across the bar within the face: face normal × bar direction.
                    double wx = onz * dy, wy = onx * dz - onz * dx, wz = -onx * dy;
                    double wl = Math.Sqrt(wx * wx + wy * wy + wz * wz);
                    if (wl < 1e-9) continue;
                    wx *= 0.025 / wl;
                    wy *= 0.025 / wl;
                    wz *= 0.025 / wl;
                    BridgeKit.Quad(m, ax - wx, ya - wy, az - wz, bx - wx, yb - wy, bz - wz, bx + wx, yb + wy, bz + wz, ax + wx, ya + wy, az + wz, onx, 0, onz, c,
                                   MaterialChannel.Metal, 0.85f, 0.9f);
                }
            }
        }

        /// <summary>A point of the main cable at along s on one side: handrail height along the middle of the run,
        /// rising as a parabola to the tower top within <paramref name="rise"/> metres of a tower.</summary>
        private static void CablePoint(BridgeSpan sp, double s, int side, double a, double b, bool towerA, bool towerB, double rise, double low,
                                       double towerH, out double cx, out double cz, out float cy)
        {
            double c, w, e, fx, fz, ux, uz, nx, nz, tx, tz;
            float y;
            Side(sp, s, side, out c, out w, out e);
            sp.Path.Frame(s, out fx, out fz, out y, out ux, out uz, out nx, out nz, out tx, out tz);
            double off = side * (e + 0.18);
            cx = fx + nx * off;
            cz = fz + nz * off;
            double d = double.PositiveInfinity;
            if (towerA) d = Math.Min(d, s - a);
            if (towerB) d = Math.Min(d, b - s);
            double r = Math.Min(rise, 0.5 * (b - a));
            double k = d < r ? (r - d) / r : 0;
            cy = (float)(y + low + towerH * k * k);
        }

        /// <summary>A through arch over the longest deck run (both its ends in this tile): a rib each side outside the
        /// railing, springing from the run ends and rising a fifth of the run (foot bridges a sixth), vertical hangers
        /// to the deck edge and, where the ribs stand clear of traffic, cross struts between them.</summary>
        private static void Arch(ref Ctx x, BridgeSpan sp, MeshData m)
        {
            double a = 0, b = 0;
            for (int r = 0; r + 1 < sp.DeckRuns.Count; r += 2)
                if (sp.DeckRuns[r + 1] - sp.DeckRuns[r] > b - a)
                {
                    a = sp.DeckRuns[r];
                    b = sp.DeckRuns[r + 1];
                }
            if (b - a < 8.0) return;
            if (Math.Abs(a - sp.Path.Start) < 1e-6 && sp.StartCut || Math.Abs(b - sp.Path.End) < 1e-6 && sp.EndCut) return;
            if (sp.Foot)
            {
                // Footbridges tagged arch (the Pashupati ghat crossings) are masonry or concrete deck arches.
                DeckArch(ref x, sp, a, b, m);
                return;
            }
            double L = b - a;
            double riseM = Math.Max(3.0, L * (sp.Foot ? 1.0 / 6.0 : 0.2));
            double r0 = sp.Foot ? 0.13 : 0.32;
            int n = x.Lod == 0 ? 17 : x.Lod == 1 ? 9 : 5;
            Tube3(n + 2);
            uint col = sp.Foot ? sp.Steel : BridgeStyle.Whitewash;
            MaterialChannel ch = sp.Foot ? MaterialChannel.Metal : MaterialChannel.Paint;
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < n; i++)
                {
                    double s = a + L * i / (n - 1);
                    double ax, az;
                    float ay;
                    ArchPoint(sp, s, side, a, L, riseM, r0, out ax, out az, out ay);
                    _tx[i] = ax;
                    _ty[i] = ay;
                    _tz[i] = az;
                }
                BridgeKit.Tube(m, _tx, _ty, _tz, n, r0, x.Lod == 0 ? 8 : 5, true, col, ch, 0.9f);
                if (x.Lod == 2) continue;
                List<double> st = Anchor();
                BridgeLayout.AnchoredStations(sp.Path, a + 0.15 * L, b - 0.15 * L, x.Lod == 0 ? 3.0 : 6.0, x.X0, x.Z0, st);
                foreach (double s in st)
                {
                    double ax, az, px, pz, yaw;
                    float ay, y;
                    ArchPoint(sp, s, side, a, L, riseM, r0, out ax, out az, out ay);
                    PostFrame(sp, s, side, 0.1, out px, out pz, out y, out yaw);
                    _tx[0] = ax;
                    _ty[0] = ay;
                    _tz[0] = az;
                    _tx[1] = ax;
                    _ty[1] = y + Top(sp) + 0.1;
                    _tz[1] = az;
                    BridgeKit.Tube(m, _tx, _ty, _tz, 2, sp.Foot ? 0.025 : 0.05, 4, false, BridgeStyle.CableGrey, MaterialChannel.Metal, 0.9f);
                }
            }
            // Cross struts where the ribs stand high enough over the deck.
            double clear = sp.Foot ? 2.6 : RoadClearance.MinOverheadClearanceM + 0.5;
            if (riseM < clear + 0.3 || x.Lod == 2) return;
            for (int i = 0; i < 3; i++)
            {
                double s = a + L * (0.35 + 0.15 * i);
                double lx, lz, rx, rz;
                float ly, ry;
                ArchPoint(sp, s, 1, a, L, riseM, r0, out lx, out lz, out ly);
                ArchPoint(sp, s, -1, a, L, riseM, r0, out rx, out rz, out ry);
                if (ly - sp.Path.YAt(s) < clear) continue;
                _tx[0] = lx;
                _ty[0] = ly;
                _tz[0] = lz;
                _tx[1] = rx;
                _ty[1] = ry;
                _tz[1] = rz;
                BridgeKit.Tube(m, _tx, _ty, _tz, 2, r0 * 0.6, 5, false, col, ch, 0.9f);
            }
        }

        /// <summary>A deck arch under a footbridge run [a, b]: spandrel walls on both faces from the deck down to a
        /// parabolic intrados (crown 0.55 m under the deck, rising from springings near the ground at the run ends) and
        /// the curved soffit between them; stone in heritage places, else concrete.</summary>
        private static void DeckArch(ref Ctx x, BridgeSpan sp, double a, double b, MeshData m)
        {
            double L = b - a, mid = 0.5 * (a + b);
            float yMid = sp.Path.YAt(mid);
            float gMin = float.MaxValue;
            for (int k = 0; k < sp.Count; k++)
                if (sp.Path.S[k] >= a - 1e-6 && sp.Path.S[k] <= b + 1e-6) gMin = Math.Min(gMin, sp.Ground[k]);
            double crown = yMid - 0.55;
            double rise = Math.Min(0.22 * L, crown - gMin - 0.3);
            if (rise < 0.8) return;
            uint col = sp.Heritage ? BridgeStyle.StoneLight : BridgeStyle.Concrete;
            MaterialChannel ch = sp.Heritage ? MaterialChannel.Stone : MaterialChannel.Concrete;
            int n = x.Lod == 0 ? 16 : x.Lod == 1 ? 8 : 4;
            double pyPrev = 0, pPrevLx = 0, pPrevLz = 0, pPrevRx = 0, pPrevRz = 0, topPrev = 0;
            for (int i = 0; i <= n; i++)
            {
                double s = a + L * i / n, u = (s - mid) / (0.5 * L);
                double px, pz, ux, uz, nx, nz, tx, tz;
                float y;
                sp.Path.Frame(s, out px, out pz, out y, out ux, out uz, out nx, out nz, out tx, out tz);
                int k = sp.StationAt(s);
                double el = sp.EdgeL(k) - 0.02, er = sp.EdgeR(k) - 0.02;
                double lx = px + nx * el, lz = pz + nz * el, rx = px - nx * er, rz = pz - nz * er;
                double yi = crown - rise * u * u, top = y - 0.05;
                if (i > 0)
                {
                    float ao = (float)(0.55 + 0.35 * Math.Min(1.0, (topPrev - pyPrev) / 2.0));
                    // Spandrel faces (left faces +n, right faces −n) and the soffit facing down.
                    BridgeKit.Quad(m, pPrevLx, pyPrev, pPrevLz, lx, yi, lz, lx, top, lz, pPrevLx, topPrev, pPrevLz, nx, 0, nz, col, ch, 0.6f, 0.9f);
                    BridgeKit.Quad(m, pPrevRx, pyPrev, pPrevRz, rx, yi, rz, rx, top, rz, pPrevRx, topPrev, pPrevRz, -nx, 0, -nz, col, ch, 0.6f, 0.9f);
                    BridgeKit.Quad(m, pPrevLx, pyPrev, pPrevLz, lx, yi, lz, rx, yi, rz, pPrevRx, pyPrev, pPrevRz, 0, -1, 0, BridgeStyle.ConcreteSoffit, ch, ao * 0.7f,
                                   ao * 0.7f);
                }
                pyPrev = yi;
                topPrev = top;
                pPrevLx = lx;
                pPrevLz = lz;
                pPrevRx = rx;
                pPrevRz = rz;
            }
        }

        private static void ArchPoint(BridgeSpan sp, double s, int side, double a, double L, double rise, double r, out double ax, out double az, out float ay)
        {
            double c, w, e, fx, fz, ux, uz, nx, nz, tx, tz;
            float y;
            Side(sp, s, side, out c, out w, out e);
            sp.Path.Frame(s, out fx, out fz, out y, out ux, out uz, out nx, out nz, out tx, out tz);
            double off = side * (e + r + 0.05);
            ax = fx + nx * off;
            az = fz + nz * off;
            double f = L > 1e-6 ? (s - a) / L : 0;
            ay = (float)(y + Top(sp) + 4 * rise * f * (1 - f));
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
