using System;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing
{
    /// <summary>Options for <see cref="RoadMesher"/>.</summary>
    public sealed class RoadOptions
    {
        /// <summary>Height of the ribbon above the sampled terrain.</summary>
        public float LiftM = 0.25f;

        /// <summary>Extra lift per class priority step (<see cref="RoadStyle.Priority"/>, 0..8), so a major road
        /// draws over a minor one where ribbons overlap at junctions. At most 8 steps.</summary>
        public float ClassLiftStepM = 0.008f;

        /// <summary>Extra lift per piece rank inside a class (<see cref="RoadMesher.PieceRank"/>, 0 to
        /// <see cref="PieceLiftLevels"/> - 1), so two overlapping ribbons of the same class are not coplanar at a
        /// junction. Keep <c>(PieceLiftLevels - 1) × PieceLiftStepM</c> below <see cref="ClassLiftStepM"/> so the
        /// class order still holds.</summary>
        public float PieceLiftStepM = 0.001f;

        /// <summary>Number of piece ranks inside a class (1 turns the piece lift off).</summary>
        public int PieceLiftLevels = 7;

        /// <summary>Longest ribbon segment in metres; 0 uses the sampler's grid spacing
        /// (<see cref="TileHeightSampler.SpacingM"/>), or 8 m for other samplers.</summary>
        public float MaxSegmentM = 0f;

        /// <summary>Drape ribbons exactly onto the terrain triangles when the sampler is a
        /// <see cref="TileHeightSampler"/> (about four times the triangles of plain cross-sections, which only
        /// touch the surface at their vertices). Off: plain cross-sections at sampled heights.</summary>
        public bool Drape = true;

        /// <summary>Also draw trails (footway, path, steps, track, cycleway, bridleway).</summary>
        public bool IncludeTrails = true;

        /// <summary>How much lighter the centre line is than the edges (0..1 toward white).</summary>
        public float CentreLighten = 0.12f;

        /// <summary>Cap on the miter scale at sharp corners.</summary>
        public float MaxMiter = 2f;

        /// <summary>W2 widths from <see cref="RoadWidthModel"/> (real widths × 1.25, corridor clamp, tapers) via the
        /// tile's <see cref="RoadLayout"/>. Off: the W1 constant widths of <see cref="RoadStyle.WidthM"/>, and none of
        /// the W2 cross-section, junction or island geometry.</summary>
        public bool WidthModel = true;

        /// <summary>With <see cref="WidthModel"/>: footpaths (raised 150 mm on a kerb), median halves of dual
        /// carriageways and shoulders.</summary>
        public bool CrossSections = true;

        /// <summary>With <see cref="WidthModel"/>: stop the ribbons at junction caps and draw the caps and road islands
        /// (<see cref="JunctionMesher"/>) into the same mesh.</summary>
        public bool JunctionCaps = true;

        /// <summary>Kerb height of footpaths (W2_DESIGN 4.5: raised 150 mm).</summary>
        public float KerbHeightM = 0.15f;

        /// <summary>Height of mountable median and island kerbs.</summary>
        public float MedianHeightM = 0.10f;
    }

    /// <summary>Road widths, colours and draw priority by class and surface.</summary>
    public static class RoadStyle
    {
        public const float MinWidthM = 1f;

        /// <summary>Widest W1 ribbon. W2 game widths reach <see cref="MaxGameWidthM"/>.</summary>
        public const float MaxWidthM = 40f;

        /// <summary>Widest W2 game carriageway: real 40 m + <see cref="RoadWidthModel.MaxGainM"/>.</summary>
        public const float MaxGameWidthM = RoadWidthModel.MaxRealM + RoadWidthModel.MaxGainM;

        // W2 cross-section colours (W2_DESIGN 4.5).
        public static readonly uint PaverRed = MeshColor.FromHex(0xB5655A);
        public static readonly uint PaverGrey = MeshColor.FromHex(0x9C9A94);
        public static readonly uint Kerb = MeshColor.FromHex(0xBDB8AE);
        public static readonly uint Shoulder = MeshColor.FromHex(0xBBAF96);
        public static readonly uint IslandGrass = MeshColor.FromHex(0x6FAE4A);
        public static readonly uint Podium = MeshColor.FromHex(0xF4F2EC);

        /// <summary>W1 default carriageway width when the way has no width tag. W2 meshing uses
        /// <see cref="RoadWidthModel"/> instead; this stays for the W1 path and the driving index until it moves to
        /// <see cref="RoadLayout.HalfWidthAt"/>.</summary>
        public static float DefaultWidthM(RoadClass c)
        {
            switch (c)
            {
                case RoadClass.Motorway: return 12f;
                case RoadClass.Trunk: return 10f;
                case RoadClass.Primary: return 8f;
                case RoadClass.Secondary: return 7f;
                case RoadClass.Tertiary: return 6f;
                case RoadClass.Unclassified:
                case RoadClass.Residential:
                case RoadClass.Road: return 5f;
                case RoadClass.LivingStreet:
                case RoadClass.Pedestrian:
                case RoadClass.Service:
                case RoadClass.Track: return 4f;
                case RoadClass.Footway:
                case RoadClass.Path:
                case RoadClass.Steps:
                case RoadClass.Cycleway:
                case RoadClass.Bridleway: return 2f;
                default: return 4f;
            }
        }

        /// <summary>The ribbon width: the tagged width when present, else the class default, clamped to
        /// [<see cref="MinWidthM"/>, <see cref="MaxWidthM"/>].</summary>
        public static float WidthM(RoadRecord r)
        {
            float w = r.WidthCm > 0 ? (float)(r.WidthCm / 100.0) : DefaultWidthM(r.RoadClass);
            return w < MinWidthM ? MinWidthM : w > MaxWidthM ? MaxWidthM : w;
        }

        /// <summary>True for walking and riding trails (drawn thinner, skippable with
        /// <see cref="RoadOptions.IncludeTrails"/>).</summary>
        public static bool IsTrail(RoadClass c)
        {
            return c == RoadClass.Footway || c == RoadClass.Path || c == RoadClass.Steps || c == RoadClass.Track ||
                   c == RoadClass.Cycleway || c == RoadClass.Bridleway;
        }

        /// <summary>Draw priority: 8 for motorway/trunk down to 1 for footpaths, 0 unknown.</summary>
        public static int Priority(RoadClass c)
        {
            switch (c)
            {
                case RoadClass.Motorway:
                case RoadClass.Trunk: return 8;
                case RoadClass.Primary: return 7;
                case RoadClass.Secondary: return 6;
                case RoadClass.Tertiary: return 5;
                case RoadClass.Unclassified:
                case RoadClass.Residential:
                case RoadClass.LivingStreet:
                case RoadClass.Road: return 4;
                case RoadClass.Service:
                case RoadClass.Pedestrian: return 3;
                case RoadClass.Track: return 2;
                case RoadClass.Footway:
                case RoadClass.Path:
                case RoadClass.Steps:
                case RoadClass.Cycleway:
                case RoadClass.Bridleway: return 1;
                default: return 0;
            }
        }

        /// <summary>Running-surface colour (0xRRGGBBAA), after ASSET_MANIFEST.md 3.1; unknown surfaces read as dirt.</summary>
        public static uint SurfaceRgba(Surface s)
        {
            switch (s)
            {
                case Surface.Asphalt: return MeshColor.FromHex(0x55585F);   // dark blue-grey
                case Surface.Concrete: return MeshColor.FromHex(0xB9B7B0);  // light grey
                case Surface.Brick: return MeshColor.FromHex(0xA9533B);     // Newar brick red
                case Surface.Cobble: return MeshColor.FromHex(0x8A6F55);    // brown setts
                case Surface.Gravel: return MeshColor.FromHex(0xBBAF96);    // tan
                case Surface.Compacted: return MeshColor.FromHex(0xB5A27A); // khaki
                case Surface.Dirt: return MeshColor.FromHex(0xB0703E);      // orange-brown
                case Surface.Mud: return MeshColor.FromHex(0x6B4A2E);       // dark brown
                case Surface.Sand: return MeshColor.FromHex(0xE0D3B0);
                case Surface.Grass: return MeshColor.FromHex(0x7DB24E);
                case Surface.Rock: return MeshColor.FromHex(0x6E737B);      // slate
                case Surface.SnowIce: return MeshColor.FromHex(0xF2F6FA);
                case Surface.Wood: return MeshColor.FromHex(0x8A5A33);
                case Surface.Metal: return MeshColor.FromHex(0x7D8791);
                default: return MeshColor.FromHex(0xB0703E);
            }
        }
    }

    /// <summary>
    /// Road and trail ribbons from ROAD (ARCHITECTURE.md 7.4). Each piece becomes a three-column ribbon (left edge,
    /// a slightly lighter centre line, right edge) of <see cref="RoadStyle.WidthM"/>, with cross-sections densified
    /// so no segment is longer than the terrain spacing. Context points are never drawn but set the end tangents;
    /// a cut end's cross-section lies on the tile border line itself, so the ribbons of both tiles meet there
    /// exactly and neither overhangs into the other tile (its width along the border is capped at 3x for roads
    /// nearly parallel to the border). At sharp corners between short segments the width narrows so the ribbon
    /// never folds over itself.
    /// <para>
    /// Heights: with a <see cref="TileHeightSampler"/> the ribbon is draped exactly on the rendered terrain
    /// triangles (every ribbon triangle clipped against the terrain grid), <see cref="RoadOptions.LiftM"/> plus a
    /// small class lift above it everywhere, shaded with the terrain's own normals. With another sampler each
    /// cross-section vertex takes the sampled height plus the lift. Bridges run straight between their lifted end
    /// heights (never below the lifted terrain); tunnels are skipped. Overlapping ribbons at junctions are separated
    /// by the lift (<see cref="LiftOf"/>): the class lift puts a major road over a minor one, and a deterministic
    /// piece rank from the way id, surface and width (identical in every tile the way crosses) puts one of two
    /// same-class ribbons a millimetre or more above the other, so they do not z-fight (pieces whose ranks
    /// collide, about one pair in <see cref="RoadOptions.PieceLiftLevels"/>, are still coplanar).
    /// </para>
    /// <para>Positions are relative to the tile's south-west corner (draw roads only for exact nodes). UV0: U across
    /// (0, 0.5, 1), V along at one unit per 4 m. Appends to <see cref="MeshData"/>; returns the number of pieces
    /// drawn. Thread-safe for distinct meshes.</para>
    /// </summary>
    public static class RoadMesher
    {
        private const double MetresPerV = 4.0;
        private const double MinCutSine = 1.0 / 3.0;

        /// <summary>Plan-view cross-sections of one piece (tile-local metres): the mapped centreline point C, the
        /// left offset vector U per metre (unit normal × miter, or the border direction at a cut end), the carriageway
        /// half width and centre shift, the footpath widths and the arc length. The carriageway's left edge is
        /// <c>C + U·(shift + half)</c>, its right edge <c>C + U·(shift − half)</c>.</summary>
        private sealed class Sections
        {
            public double[] Cx = new double[64], Cz = new double[64], Ux = new double[64], Uz = new double[64];
            public double[] Half = new double[64], Shift = new double[64], FootL = new double[64], FootR = new double[64];
            public double[] S = new double[64];
            public bool[] Gap = new bool[64];
            public int Count;

            public void Add(double cx, double cz, double ux, double uz, double half, double shift, double footL, double footR, double s)
            {
                if (Count == Cx.Length)
                {
                    int cap = Count * 2;
                    Array.Resize(ref Cx, cap);
                    Array.Resize(ref Cz, cap);
                    Array.Resize(ref Ux, cap);
                    Array.Resize(ref Uz, cap);
                    Array.Resize(ref Half, cap);
                    Array.Resize(ref Shift, cap);
                    Array.Resize(ref FootL, cap);
                    Array.Resize(ref FootR, cap);
                    Array.Resize(ref S, cap);
                    Array.Resize(ref Gap, cap);
                }
                Cx[Count] = cx;
                Cz[Count] = cz;
                Ux[Count] = ux;
                Uz[Count] = uz;
                Half[Count] = half;
                Shift[Count] = shift;
                FootL[Count] = footL;
                FootR[Count] = footR;
                S[Count] = s;
                Gap[Count] = false;
                Count++;
            }

            /// <summary>Plan position at signed offset <paramref name="off"/> (left positive) of section k.</summary>
            public double X(int k, double off)
            {
                return Cx[k] + Ux[k] * off;
            }

            public double Z(int k, double off)
            {
                return Cz[k] + Uz[k] * off;
            }

            public double Left(int k)
            {
                return Shift[k] + Half[k];
            }

            public double Centre(int k)
            {
                return Shift[k];
            }

            public double Right(int k)
            {
                return Shift[k] - Half[k];
            }
        }

        [ThreadStatic] private static Sections _sections;
        [ThreadStatic] private static double[] _splits;
        [ThreadStatic] private static int[] _splitCut;

        private struct Ctx
        {
            public IHeightSampler Sampler;
            public TileHeightSampler TileSampler;
            public double X0, Z0, Size;
            public float LastH;
        }

        public static int Build(TileData t, IHeightSampler h, RoadOptions o, MeshData m)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (h == null) throw new ArgumentNullException(nameof(h));
            if (m == null) throw new ArgumentNullException(nameof(m));
            if (o == null) o = new RoadOptions();
            var ctx = new Ctx
            {
                Sampler = h, TileSampler = h as TileHeightSampler, X0 = t.Tile.X0, Z0 = t.Tile.Z0, Size = t.Tile.Size,
            };
            double maxSeg = o.MaxSegmentM > 0f ? o.MaxSegmentM
                : ctx.TileSampler != null && ctx.TileSampler.SpacingM > 0 ? ctx.TileSampler.SpacingM : 8.0;
            Sections sec = _sections ?? (_sections = new Sections());
            RoadLayout layout = o.WidthModel && t.Roads.Count > 0 ? RoadLayout.For(t) : null;
            int drawn = 0;
            for (int r = 0; r < t.Roads.Count; r++)
                if (Piece(ref ctx, t.Roads[r], r, layout, o, maxSeg, sec, m)) drawn++;
            if (layout != null && o.JunctionCaps) JunctionMesher.Build(t, h, o, m, null);
            return drawn;
        }

        /// <summary>True when <see cref="Build"/> would draw the piece (not a tunnel, a trail only when trails are
        /// included, and at least two drawable points).</summary>
        public static bool IsDrawn(RoadRecord r, RoadOptions o)
        {
            if ((r.Flags & RoadFlags.Tunnel) != 0) return false;
            if (o != null && !o.IncludeTrails && RoadStyle.IsTrail(r.RoadClass)) return false;
            int count = r.PointCount;
            int first = r.HasPrevContext ? 1 : 0, last = r.HasNextContext ? count - 2 : count - 1;
            return last - first >= 1;
        }

        /// <summary>Height of a piece's ribbon above the terrain: <see cref="RoadOptions.LiftM"/> plus its class lift
        /// plus its piece lift (<see cref="PieceRank"/> × <see cref="RoadOptions.PieceLiftStepM"/>).</summary>
        public static float LiftOf(RoadRecord r, RoadOptions o)
        {
            if (o == null) o = new RoadOptions();
            int prio = RoadStyle.Priority(r.RoadClass);
            return o.LiftM + (prio > 8 ? 8 : prio) * o.ClassLiftStepM + PieceRank(r, o) * o.PieceLiftStepM;
        }

        /// <summary>
        /// The piece's rank inside its class lift band, 0 to <see cref="RoadOptions.PieceLiftLevels"/> - 1: a hash of
        /// the way id, surface and width, so every piece of a way gets the same rank in every tile and the ground
        /// query (which uses <see cref="LiftOf"/>) agrees with the drawn ribbon.
        /// </summary>
        public static int PieceRank(RoadRecord r, RoadOptions o)
        {
            int levels = o == null ? new RoadOptions().PieceLiftLevels : o.PieceLiftLevels;
            if (levels <= 1) return 0;
            unchecked
            {
                ulong h = r.OsmWayId * 0x9E3779B97F4A7C15UL ^ ((ulong)r.Surface << 48) ^ r.WidthCm * 0xC2B2AE3D27D4EB4FUL;
                h ^= h >> 31;
                h *= 0xBF58476D1CE4E5B9UL;
                h ^= h >> 29;
                return (int)(h % (ulong)levels);
            }
        }

        /// <summary>Paver colour of a piece's footpaths (red or grey, by way).</summary>
        internal static uint FootpathRgba(RoadRecord r)
        {
            return (RoadWidthModel.Hash(r.OsmWayId, 0x50415652) & 1) == 0 ? RoadStyle.PaverRed : RoadStyle.PaverGrey;
        }

        private static bool Piece(ref Ctx ctx, RoadRecord r, int ri, RoadLayout layout, RoadOptions o, double maxSeg, Sections sec, MeshData m)
        {
            if (!IsDrawn(r, o)) return false;
            int[] p = r.Points;
            int count = p.Length / 2;
            int first = r.HasPrevContext ? 1 : 0, last = r.HasNextContext ? count - 2 : count - 1;
            int sizeCm = (int)Math.Round(ctx.Size * 100.0);
            RoadWidthProfile prof = layout == null ? null : layout.Profiles[ri];
            RoadCut[] cuts = layout == null ? null : layout.Cuts[ri];
            bool cross = layout != null && o.CrossSections;
            double w1Half = RoadStyle.WidthM(r) * 0.5;
            double minDot = 1.0 / Math.Max(1.0, o.MaxMiter);
            bool dual = layout != null && layout.Attrs[ri].Has(RoadAttrFlags.Dual);
            double realHalf = prof == null ? w1Half : 0.5 * prof.RealM;

            sec.Count = 0;
            double s = 0;
            int nextCut = 0;
            for (int i = first; i <= last; i++)
            {
                double cx = p[2 * i] / 100.0, cz = p[2 * i + 1] / 100.0;
                if (i > first)
                {
                    double len = SegLen(p, i - 1);
                    if (len < 1e-6) continue;
                    double ax = p[2 * i - 2] / 100.0, az = p[2 * i - 1] / 100.0;
                    double dx = (cx - ax) / len, dz = (cz - az) / len;
                    int nsub = (int)Math.Ceiling(len / maxSeg);
                    int ns = Splits(nsub, s, len, cuts, ref nextCut);
                    for (int k = 0; k < ns; k++)
                    {
                        double f = _splits[k];
                        int ci = _splitCut[k];
                        if (ci >= 0)
                        {
                            RoadCut c = cuts[ci];
                            AddSection(sec, prof, r, c.CX, c.CZ, c.UX, c.UZ, c.Half, c.Shift, c.S, 1.0, cross);
                            continue;
                        }
                        double mx = ax + (cx - ax) * f, mz = az + (cz - az) * f, sm = s + len * f;
                        double half = prof == null ? w1Half : 0.5 * prof.WidthAt(sm);
                        AddSection(sec, prof, r, mx, mz, -dz, dx, half, ShiftAt(prof, dual, sm), sm, 1.0, cross);
                    }
                    s += len;
                }
                if (cuts != null && nextCut < cuts.Length && Math.Abs(cuts[nextCut].S - s) < 1e-6)
                {
                    // A junction cut exactly on this vertex: the section takes the cut's geometry.
                    RoadCut c = cuts[nextCut++];
                    AddSection(sec, prof, r, c.CX, c.CZ, c.UX, c.UZ, c.Half, c.Shift, c.S, 1.0, cross);
                    continue;
                }
                double halfHere = prof == null ? w1Half : 0.5 * prof.WidthAt(s);
                double tx, tz, miter, halfAt;
                Tangent(p, count, i, minDot, halfHere, maxSeg, out tx, out tz, out miter, out halfAt);
                double scale = halfHere > 1e-9 ? halfAt / halfHere : 1.0;
                int border = (i == first && r.HasPrevContext) || (i == last && r.HasNextContext)
                    ? BorderOf(p[2 * i], p[2 * i + 1], sizeCm) : -1;
                if (border >= 0)
                {
                    // Cut end: the cross-section lies on the border line, where both tiles' ribbons meet.
                    double ex = border == 0 ? 0 : 1, ez = border == 0 ? 1 : 0;
                    double crs = ex * tz - ez * tx;
                    if (Math.Abs(crs) < MinCutSine) crs = crs >= 0 ? MinCutSine : -MinCutSine;
                    AddSection(sec, prof, r, cx, cz, -ex / crs, -ez / crs, halfAt, ShiftAt(prof, dual, s) * scale, s, scale, cross);
                }
                else
                {
                    AddSection(sec, prof, r, cx, cz, -tz * miter, tx * miter, halfAt, ShiftAt(prof, dual, s) * scale, s, scale, cross);
                }
            }
            if (sec.Count < 2) return false;
            if (cuts != null)
                for (int k = 0; k + 1 < sec.Count; k++) sec.Gap[k] = layout.InGap(ri, 0.5 * (sec.S[k] + sec.S[k + 1]));

            uint edge = RoadStyle.SurfaceRgba(r.Surface);
            uint centre = MeshColor.Lighten(edge, o.CentreLighten);
            float lift = LiftOf(r, o);
            bool bridge = (r.Flags & RoadFlags.Bridge) != 0;
            bool drape = !bridge && o.Drape && ctx.TileSampler != null && ctx.TileSampler.HasHeights;
            if (drape) Draped(ref ctx, sec, lift, edge, centre, m);
            else Sampled(ref ctx, sec, lift, bridge, edge, centre, m);
            if (cross && !bridge) Extras(ref ctx, r, layout.Attrs[ri], prof, sec, lift, drape, o, m);
            return true;
        }

        private static double ShiftAt(RoadWidthProfile prof, bool dual, double s)
        {
            return prof != null && dual ? 0.5 * (prof.WidthAt(s) - prof.RealM) : 0.0;
        }

        private static void AddSection(Sections sec, RoadWidthProfile prof, RoadRecord r, double cx, double cz, double ux, double uz,
                                       double half, double shift, double s, double scale, bool cross)
        {
            double fl = 0, fr = 0;
            if (cross && prof != null)
            {
                fl = prof.Sample(prof.FootLeft, s) * scale;
                fr = prof.Sample(prof.FootRight, s) * scale;
            }
            sec.Add(cx, cz, ux, uz, half, shift, fl, fr, s);
        }

        /// <summary>Split fractions (0, 1) of a segment from along <paramref name="s0"/> of length <paramref name="len"/>:
        /// the uniform subdivisions plus the junction cuts inside it, in order. <see cref="_splitCut"/> holds the cut
        /// index of a split or -1. Returns the count.</summary>
        private static int Splits(int nsub, double s0, double len, RoadCut[] cuts, ref int nextCut)
        {
            int cap = nsub + (cuts == null ? 0 : cuts.Length) + 1;
            if (_splits == null || _splits.Length < cap)
            {
                _splits = new double[Math.Max(cap, 64)];
                _splitCut = new int[Math.Max(cap, 64)];
            }
            int n = 0, k = 1;
            while (true)
            {
                double fu = k < nsub ? (double)k / nsub : 2.0;
                double fc = 2.0;
                if (cuts != null && nextCut < cuts.Length)
                {
                    double c = cuts[nextCut].S;
                    if (c <= s0 + 1e-6)
                    {
                        nextCut++;
                        continue;
                    }
                    if (c < s0 + len - 1e-6) fc = (c - s0) / len;
                }
                if (fu >= 2.0 && fc >= 2.0) break;
                if (fc <= fu)
                {
                    _splits[n] = fc;
                    _splitCut[n] = nextCut;
                    nextCut++;
                    if (Math.Abs(fc - fu) < 1e-9) k++;
                }
                else
                {
                    _splits[n] = fu;
                    _splitCut[n] = -1;
                    k++;
                }
                n++;
            }
            return n;
        }

        /// <summary>Drape every strip triangle onto the rendered terrain (exact conformity).</summary>
        private static void Draped(ref Ctx ctx, Sections sec, float lift, uint edge, uint centre, MeshData m)
        {
            for (int k = 0; k + 1 < sec.Count; k++)
            {
                if (sec.Gap[k]) continue;
                float va = (float)(sec.S[k] / MetresPerV), vb = (float)(sec.S[k + 1] / MetresPerV);
                double la = sec.Left(k), ca = sec.Centre(k), ra = sec.Right(k);
                double lb = sec.Left(k + 1), cb = sec.Centre(k + 1), rb = sec.Right(k + 1);
                var aL = new GridDrape.Vertex(sec.X(k, la), sec.Z(k, la), 0f, va, edge);
                var aC = new GridDrape.Vertex(sec.X(k, ca), sec.Z(k, ca), 0.5f, va, centre);
                var aR = new GridDrape.Vertex(sec.X(k, ra), sec.Z(k, ra), 1f, va, edge);
                var bL = new GridDrape.Vertex(sec.X(k + 1, lb), sec.Z(k + 1, lb), 0f, vb, edge);
                var bC = new GridDrape.Vertex(sec.X(k + 1, cb), sec.Z(k + 1, cb), 0.5f, vb, centre);
                var bR = new GridDrape.Vertex(sec.X(k + 1, rb), sec.Z(k + 1, rb), 1f, vb, edge);
                DrapeStrip(ref ctx, aL, bL, aC, bC, lift, m);
                DrapeStrip(ref ctx, aC, bC, aR, bR, lift, m);
            }
        }

        private static void DrapeStrip(ref Ctx ctx, GridDrape.Vertex a0, GridDrape.Vertex b0, GridDrape.Vertex a1, GridDrape.Vertex b1,
                                       float lift, MeshData m)
        {
            if (FirstDiagonal(a0.X, a0.Z, b0.X, b0.Z, a1.X, a1.Z, b1.X, b1.Z))
            {
                GridDrape.Triangle(ctx.X0, ctx.Z0, ctx.TileSampler, a0, b0, b1, lift, true, m);
                GridDrape.Triangle(ctx.X0, ctx.Z0, ctx.TileSampler, a0, b1, a1, lift, true, m);
            }
            else
            {
                GridDrape.Triangle(ctx.X0, ctx.Z0, ctx.TileSampler, a0, b0, a1, lift, true, m);
                GridDrape.Triangle(ctx.X0, ctx.Z0, ctx.TileSampler, b0, b1, a1, lift, true, m);
            }
        }

        /// <summary>Cross-section vertices at sampled heights (bridges, and samplers without a terrain grid).</summary>
        private static void Sampled(ref Ctx ctx, Sections sec, float lift, bool bridge, uint edge, uint centre, MeshData m)
        {
            int n = sec.Count;
            double total = sec.S[n - 1] - sec.S[0];
            float hStart = 0f, hEnd = 0f;
            if (bridge)
            {
                hStart = Height(ref ctx, sec.Cx[0], sec.Cz[0]) + lift;
                hEnd = Height(ref ctx, sec.Cx[n - 1], sec.Cz[n - 1]) + lift;
            }
            m.Reserve(3 * n, 12 * n);
            int prev = -1;
            for (int k = 0; k < n; k++)
            {
                double lo = sec.Left(k), co = sec.Centre(k), ro = sec.Right(k);
                double lx = sec.X(k, lo), lz = sec.Z(k, lo), cx = sec.X(k, co), cz = sec.Z(k, co), rx = sec.X(k, ro), rz = sec.Z(k, ro);
                float hc = Height(ref ctx, cx, cz);
                float yl, yc, yr, nx, ny, nz;
                if (bridge)
                {
                    // A level deck on the straight line between the ends, never below the lifted terrain under it.
                    double f = total > 0 ? (sec.S[k] - sec.S[0]) / total : 0;
                    float ground = Math.Max(hc, Math.Max(Height(ref ctx, lx, lz), Height(ref ctx, rx, rz)));
                    yc = Math.Max((float)(hStart + (hEnd - hStart) * f), ground + lift);
                    yl = yr = yc;
                    nx = 0f;
                    ny = 1f;
                    nz = 0f;
                }
                else
                {
                    yl = Height(ref ctx, lx, lz) + lift;
                    yc = hc + lift;
                    yr = Height(ref ctx, rx, rz) + lift;
                    Normal(ref ctx, cx, cz, out nx, out ny, out nz);
                }
                float v = (float)(sec.S[k] / MetresPerV);
                int left = m.AddVertex((float)lx, yl, (float)lz, nx, ny, nz, edge, 0f, v);
                m.AddVertex((float)cx, yc, (float)cz, nx, ny, nz, centre, 0.5f, v);
                m.AddVertex((float)rx, yr, (float)rz, nx, ny, nz, edge, 1f, v);
                if (prev >= 0 && !sec.Gap[k - 1])
                {
                    Strip(m, prev, left, prev + 1, left + 1);
                    Strip(m, prev + 1, left + 1, prev + 2, left + 2);
                }
                prev = left;
            }
        }

        /// <summary>
        /// W2 cross-section extras between consecutive sections (outside junction caps): footpaths raised on a kerb
        /// (top surface plus inner and outer faces), the median half of a dual carriageway on its right (offside)
        /// edge, and shoulders where there is no footpath.
        /// </summary>
        private static void Extras(ref Ctx ctx, RoadRecord r, RoadAttrRecord a, RoadWidthProfile prof, Sections sec, float lift, bool drape,
                                   RoadOptions o, MeshData m)
        {
            uint paver = FootpathRgba(r);
            float kerbH = o.KerbHeightM;
            bool dual = a.Has(RoadAttrFlags.Dual);
            float median = dual ? Math.Max(RoadWidthModel.MinMedianM, a.MedianCm / 100f) : 0f;
            float shoulder = RoadWidthModel.ShoulderM(r.RoadClass, RoadWidthModel.AreaOf(a));
            for (int k = 0; k + 1 < sec.Count; k++)
            {
                if (sec.Gap[k]) continue;
                int j = k + 1;
                // Left footpath.
                if (sec.FootL[k] > 0 || sec.FootL[j] > 0)
                {
                    double i0 = sec.Left(k), i1 = sec.Left(j), o0 = i0 + sec.FootL[k], o1 = i1 + sec.FootL[j];
                    Band(ref ctx, sec, k, i0, o0, i1, o1, lift + kerbH, drape, paver, m);
                    Face(ref ctx, sec, k, i0, i1, lift - 0.1f, lift + kerbH, -1, RoadStyle.Kerb, m);
                    Face(ref ctx, sec, k, o0, o1, lift - 0.3f, lift + kerbH, +1, paver, m);
                }
                else if (shoulder > 0 && !dual)
                {
                    double i0 = sec.Left(k), i1 = sec.Left(j);
                    Band(ref ctx, sec, k, i0, i0 + shoulder, i1, i1 + shoulder, lift - 0.004f, drape, RoadStyle.Shoulder, m);
                }
                // Right side: median half on a dual carriageway, else footpath or shoulder.
                if (dual)
                {
                    double i0 = sec.Right(k), i1 = sec.Right(j), o0 = i0 - 0.5 * median, o1 = i1 - 0.5 * median;
                    Band(ref ctx, sec, k, o0, i0, o1, i1, lift + o.MedianHeightM, drape, RoadStyle.Kerb, m);
                    Face(ref ctx, sec, k, i0, i1, lift - 0.1f, lift + o.MedianHeightM, +1, RoadStyle.Kerb, m);
                }
                else if (sec.FootR[k] > 0 || sec.FootR[j] > 0)
                {
                    double i0 = sec.Right(k), i1 = sec.Right(j), o0 = i0 - sec.FootR[k], o1 = i1 - sec.FootR[j];
                    Band(ref ctx, sec, k, o0, i0, o1, i1, lift + kerbH, drape, paver, m);
                    Face(ref ctx, sec, k, i0, i1, lift - 0.1f, lift + kerbH, +1, RoadStyle.Kerb, m);
                    Face(ref ctx, sec, k, o0, o1, lift - 0.3f, lift + kerbH, -1, paver, m);
                }
                else if (shoulder > 0)
                {
                    double i0 = sec.Right(k), i1 = sec.Right(j);
                    Band(ref ctx, sec, k, i0 - shoulder, i0, i1 - shoulder, i1, lift - 0.004f, drape, RoadStyle.Shoulder, m);
                }
            }
        }

        /// <summary>A flat band between offsets (a0 at section k, a1 at k + 1) and (b0, b1), lifted.</summary>
        private static void Band(ref Ctx ctx, Sections sec, int k, double a0, double b0, double a1, double b1, float lift, bool drape,
                                 uint c, MeshData m)
        {
            int j = k + 1;
            double ax = sec.X(k, a0), az = sec.Z(k, a0), bx = sec.X(k, b0), bz = sec.Z(k, b0);
            double cx = sec.X(j, a1), cz = sec.Z(j, a1), dx = sec.X(j, b1), dz = sec.Z(j, b1);
            float va = (float)(sec.S[k] / MetresPerV), vb = (float)(sec.S[j] / MetresPerV);
            if (drape)
            {
                var p0 = new GridDrape.Vertex(ax, az, 0f, va, c);
                var p1 = new GridDrape.Vertex(bx, bz, 1f, va, c);
                var q0 = new GridDrape.Vertex(cx, cz, 0f, vb, c);
                var q1 = new GridDrape.Vertex(dx, dz, 1f, vb, c);
                DrapeStrip(ref ctx, p0, q0, p1, q1, lift, m);
                return;
            }
            float ya = Height(ref ctx, ax, az) + lift, yb = Height(ref ctx, bx, bz) + lift;
            float yc = Height(ref ctx, cx, cz) + lift, yd = Height(ref ctx, dx, dz) + lift;
            int v = m.AddVertex((float)ax, ya, (float)az, 0f, 1f, 0f, c, 0f, va);
            m.AddVertex((float)bx, yb, (float)bz, 0f, 1f, 0f, c, 1f, va);
            m.AddVertex((float)cx, yc, (float)cz, 0f, 1f, 0f, c, 0f, vb);
            m.AddVertex((float)dx, yd, (float)dz, 0f, 1f, 0f, c, 1f, vb);
            Strip(m, v, v + 2, v + 1, v + 3);
        }

        /// <summary>A vertical kerb face along offsets (o0 at section k, o1 at k + 1) from y0 to y1 above the terrain,
        /// facing toward +U (<paramref name="side"/> = +1) or −U.</summary>
        private static void Face(ref Ctx ctx, Sections sec, int k, double o0, double o1, float y0, float y1, int side, uint c, MeshData m)
        {
            int j = k + 1;
            double ax = sec.X(k, o0), az = sec.Z(k, o0), bx = sec.X(j, o1), bz = sec.Z(j, o1);
            float ha = Height(ref ctx, ax, az), hb = Height(ref ctx, bx, bz);
            double hx = side * 0.5 * (sec.Ux[k] + sec.Ux[j]), hz = side * 0.5 * (sec.Uz[k] + sec.Uz[j]);
            MeshKit.Quad(m, ax, ha + y0, az, bx, hb + y0, bz, bx, hb + y1, bz, ax, ha + y1, az, hx, 0, hz, c);
        }

        /// <summary>The quad a0, b0, b1, a1 (consecutive sections' pairs) as two up-facing triangles: split along
        /// a0-b1 unless that folds a triangle over and the other diagonal does not (sections rotating sharply over
        /// a short step make the quad non-convex).</summary>
        private static bool FirstDiagonal(double a0x, double a0z, double b0x, double b0z, double a1x, double a1z, double b1x, double b1z)
        {
            return Up(a0x, a0z, b0x, b0z, b1x, b1z) > 0 && Up(a0x, a0z, b1x, b1z, a1x, a1z) > 0 ||
                   !(Up(a0x, a0z, b0x, b0z, a1x, a1z) > 0 && Up(b0x, b0z, b1x, b1z, a1x, a1z) > 0);
        }

        private static void Strip(MeshData m, int a0, int b0, int a1, int b1)
        {
            float[] p = m.Positions;
            if (FirstDiagonal(p[3 * a0], p[3 * a0 + 2], p[3 * b0], p[3 * b0 + 2], p[3 * a1], p[3 * a1 + 2], p[3 * b1], p[3 * b1 + 2]))
            {
                UpTriangle(m, a0, b0, b1);
                UpTriangle(m, a0, b1, a1);
            }
            else
            {
                UpTriangle(m, a0, b0, a1);
                UpTriangle(m, b0, b1, a1);
            }
        }

        /// <summary>Add a triangle facing up: a folded one (a bow-tie quad where a cut section and a very short
        /// segment cross) is re-wound so it overlaps its neighbour instead of facing down; slivers are dropped.</summary>
        private static void UpTriangle(MeshData m, int a, int b, int c)
        {
            double up = GridDrape.UpFloat(m, a, b, c);
            if (up > 0) m.AddTriangle(a, b, c);
            else if (up < 0) m.AddTriangle(a, c, b);
        }

        /// <summary>Plan-view orientation of triangle (a, b, c): positive when it faces up in Unity's winding.</summary>
        private static double Up(double ax, double az, double bx, double bz, double cx, double cz)
        {
            double ux = bx - ax, uz = bz - az, vx = cx - ax, vz = cz - az;
            return uz * vx - ux * vz;
        }

        private static double SegLen(int[] p, int i)
        {
            double dx = (p[2 * i + 2] - p[2 * i]) / 100.0, dz = (p[2 * i + 3] - p[2 * i + 1]) / 100.0;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Unit tangent at point i from its neighbours (context points included): the bisector of the incoming and
        /// outgoing directions, with the miter scale that keeps the ribbon width. At a sharp corner between short
        /// segments the half width is reduced so the inner edge pulls back at most half of the neighbouring
        /// sub-segments (the densified spacing of each adjacent segment): otherwise the ribbon would fold over
        /// itself. The rule only uses the two adjacent segments, so both tiles of a cut compute the same section.
        /// </summary>
        private static void Tangent(int[] p, int count, int i, double minDot, double half, double maxSeg,
                                    out double tx, out double tz, out double miter, out double halfAt)
        {
            double ix = 0, iz = 0, ox = 0, oz = 0, li = 0, lo = 0;
            bool hasIn = false, hasOut = false;
            if (i > 0)
            {
                double dx = p[2 * i] - (double)p[2 * i - 2], dz = p[2 * i + 1] - (double)p[2 * i - 1];
                double l = Math.Sqrt(dx * dx + dz * dz);
                if (l > 0)
                {
                    ix = dx / l;
                    iz = dz / l;
                    li = l / 100.0;
                    hasIn = true;
                }
            }
            if (i < count - 1)
            {
                double dx = p[2 * i + 2] - (double)p[2 * i], dz = p[2 * i + 3] - (double)p[2 * i + 1];
                double l = Math.Sqrt(dx * dx + dz * dz);
                if (l > 0)
                {
                    ox = dx / l;
                    oz = dz / l;
                    lo = l / 100.0;
                    hasOut = true;
                }
            }
            miter = 1.0;
            halfAt = half;
            if (hasIn && hasOut)
            {
                double bx = ix + ox, bz = iz + oz, bl = Math.Sqrt(bx * bx + bz * bz);
                if (bl < 1e-9)
                {
                    // A full reversal: collapse the section to its centre point.
                    tx = ox;
                    tz = oz;
                    halfAt = 0;
                    return;
                }
                tx = bx / bl;
                tz = bz / bl;
                double cosHalf = tx * ox + tz * oz;
                miter = 1.0 / (cosHalf > minDot ? cosHalf : minDot);
                double sinHalf = Math.Sqrt(Math.Max(0.0, 1.0 - cosHalf * cosHalf));
                if (sinHalf > 1e-9)
                {
                    double subIn = li / Math.Ceiling(li / maxSeg), subOut = lo / Math.Ceiling(lo / maxSeg);
                    double limit = 0.5 * Math.Min(subIn, subOut) / (miter * sinHalf);
                    if (limit < halfAt) halfAt = limit;
                }
                return;
            }
            if (hasOut)
            {
                tx = ox;
                tz = oz;
            }
            else if (hasIn)
            {
                tx = ix;
                tz = iz;
            }
            else
            {
                tx = 1;
                tz = 0;
            }
        }

        /// <summary>Which tile border a cut point lies on: 0 vertical (x = 0 or size), 1 horizontal, -1 none.</summary>
        private static int BorderOf(int xCm, int zCm, int sizeCm)
        {
            if (xCm == 0 || xCm == sizeCm) return 0;
            if (zCm == 0 || zCm == sizeCm) return 1;
            return -1;
        }

        /// <summary>Shading normal at tile-local metres: the terrain mesh's own smooth normal when the sampler is a
        /// <see cref="TileHeightSampler"/>, else central differences of the sampled heights 1 m either side.</summary>
        private static void Normal(ref Ctx ctx, double lx, double lz, out float nx, out float ny, out float nz)
        {
            if (ctx.TileSampler != null && ctx.TileSampler.TrySmoothNormal(ctx.X0 + lx, ctx.Z0 + lz, out nx, out ny, out nz))
                return;
            const double e = 1.0;
            double gx = (Height(ref ctx, lx + e, lz) - (double)Height(ref ctx, lx - e, lz)) / (2 * e);
            double gz = (Height(ref ctx, lx, lz + e) - (double)Height(ref ctx, lx, lz - e)) / (2 * e);
            TileHeightSampler.FacetNormal(gx, gz, out nx, out ny, out nz);
        }

        /// <summary>Terrain height at tile-local metres; points off the sampler are clamped onto the tile.</summary>
        private static float Height(ref Ctx ctx, double lx, double lz)
        {
            float h;
            double x = ctx.X0 + lx, z = ctx.Z0 + lz;
            if (ctx.Sampler.TryHeight(x, z, out h))
            {
                ctx.LastH = h;
                return h;
            }
            if (ctx.TileSampler != null)
            {
                if (ctx.TileSampler.TryHeightClamped(x, z, out h))
                {
                    ctx.LastH = h;
                    return h;
                }
            }
            else
            {
                double cx = lx < 0 ? 0 : lx > ctx.Size ? ctx.Size : lx, cz = lz < 0 ? 0 : lz > ctx.Size ? ctx.Size : lz;
                if (ctx.Sampler.TryHeight(ctx.X0 + cx, ctx.Z0 + cz, out h))
                {
                    ctx.LastH = h;
                    return h;
                }
            }
            return ctx.LastH;
        }
    }
}
