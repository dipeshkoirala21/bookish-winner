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
    }

    /// <summary>Road widths, colours and draw priority by class and surface.</summary>
    public static class RoadStyle
    {
        public const float MinWidthM = 1f;
        public const float MaxWidthM = 40f;

        /// <summary>Default carriageway width when the way has no width tag.</summary>
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
    /// heights (never below the lifted terrain); tunnels are skipped. Overlapping ribbons at junctions are left to the
    /// shader's depth offset and the class lift.
    /// </para>
    /// <para>Positions are relative to the tile's south-west corner (draw roads only for exact nodes). UV0: U across
    /// (0, 0.5, 1), V along at one unit per 4 m. Appends to <see cref="MeshData"/>; returns the number of pieces
    /// drawn. Thread-safe for distinct meshes.</para>
    /// </summary>
    public static class RoadMesher
    {
        private const double MetresPerV = 4.0;
        private const double MinCutSine = 1.0 / 3.0;

        /// <summary>Plan-view cross-sections of one piece (tile-local metres) and their arc length.</summary>
        private sealed class Sections
        {
            public double[] Lx = new double[64], Lz = new double[64], Cx = new double[64], Cz = new double[64];
            public double[] Rx = new double[64], Rz = new double[64], S = new double[64];
            public int Count;

            public void Add(double lx, double lz, double cx, double cz, double rx, double rz, double s)
            {
                if (Count == Lx.Length)
                {
                    int cap = Count * 2;
                    Array.Resize(ref Lx, cap);
                    Array.Resize(ref Lz, cap);
                    Array.Resize(ref Cx, cap);
                    Array.Resize(ref Cz, cap);
                    Array.Resize(ref Rx, cap);
                    Array.Resize(ref Rz, cap);
                    Array.Resize(ref S, cap);
                }
                Lx[Count] = lx;
                Lz[Count] = lz;
                Cx[Count] = cx;
                Cz[Count] = cz;
                Rx[Count] = rx;
                Rz[Count] = rz;
                S[Count] = s;
                Count++;
            }
        }

        [ThreadStatic] private static Sections _sections;

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
            int drawn = 0;
            for (int r = 0; r < t.Roads.Count; r++)
                if (Piece(ref ctx, t.Roads[r], o, maxSeg, sec, m)) drawn++;
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

        /// <summary>Height of a piece's ribbon above the terrain: <see cref="RoadOptions.LiftM"/> plus its class lift.</summary>
        public static float LiftOf(RoadRecord r, RoadOptions o)
        {
            if (o == null) o = new RoadOptions();
            int prio = RoadStyle.Priority(r.RoadClass);
            return o.LiftM + (prio > 8 ? 8 : prio) * o.ClassLiftStepM;
        }

        private static bool Piece(ref Ctx ctx, RoadRecord r, RoadOptions o, double maxSeg, Sections sec, MeshData m)
        {
            if (!IsDrawn(r, o)) return false;
            int[] p = r.Points;
            int count = p.Length / 2;
            int first = r.HasPrevContext ? 1 : 0, last = r.HasNextContext ? count - 2 : count - 1;
            int sizeCm = (int)Math.Round(ctx.Size * 100.0);
            double half = RoadStyle.WidthM(r) * 0.5;
            double minDot = 1.0 / Math.Max(1.0, o.MaxMiter);

            sec.Count = 0;
            double s = 0;
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
                    for (int k = 1; k < nsub; k++)
                    {
                        double f = (double)k / nsub, mx = ax + (cx - ax) * f, mz = az + (cz - az) * f;
                        sec.Add(mx - dz * half, mz + dx * half, mx, mz, mx + dz * half, mz - dx * half, s + len * f);
                    }
                    s += len;
                }
                double tx, tz, miter, halfAt;
                Tangent(p, count, i, minDot, half, maxSeg, out tx, out tz, out miter, out halfAt);
                int border = (i == first && r.HasPrevContext) || (i == last && r.HasNextContext)
                    ? BorderOf(p[2 * i], p[2 * i + 1], sizeCm) : -1;
                if (border >= 0)
                {
                    // Cut end: the cross-section lies on the border line, where both tiles' ribbons meet.
                    double ex = border == 0 ? 0 : 1, ez = border == 0 ? 1 : 0;
                    double cross = ex * tz - ez * tx;
                    if (Math.Abs(cross) < MinCutSine) cross = cross >= 0 ? MinCutSine : -MinCutSine;
                    double mu = -halfAt / cross;
                    sec.Add(cx + ex * mu, cz + ez * mu, cx, cz, cx - ex * mu, cz - ez * mu, s);
                }
                else
                {
                    double ox = -tz * halfAt * miter, oz = tx * halfAt * miter; // to the left of travel
                    sec.Add(cx + ox, cz + oz, cx, cz, cx - ox, cz - oz, s);
                }
            }
            if (sec.Count < 2) return false;

            uint edge = RoadStyle.SurfaceRgba(r.Surface);
            uint centre = MeshColor.Lighten(edge, o.CentreLighten);
            float lift = LiftOf(r, o);
            bool bridge = (r.Flags & RoadFlags.Bridge) != 0;
            if (!bridge && o.Drape && ctx.TileSampler != null && ctx.TileSampler.HasHeights) Draped(ref ctx, sec, lift, edge, centre, m);
            else Sampled(ref ctx, sec, lift, bridge, edge, centre, m);
            return true;
        }

        /// <summary>Drape every strip triangle onto the rendered terrain (exact conformity).</summary>
        private static void Draped(ref Ctx ctx, Sections sec, float lift, uint edge, uint centre, MeshData m)
        {
            for (int k = 0; k + 1 < sec.Count; k++)
            {
                float va = (float)(sec.S[k] / MetresPerV), vb = (float)(sec.S[k + 1] / MetresPerV);
                var aL = new GridDrape.Vertex(sec.Lx[k], sec.Lz[k], 0f, va, edge);
                var aC = new GridDrape.Vertex(sec.Cx[k], sec.Cz[k], 0.5f, va, centre);
                var aR = new GridDrape.Vertex(sec.Rx[k], sec.Rz[k], 1f, va, edge);
                var bL = new GridDrape.Vertex(sec.Lx[k + 1], sec.Lz[k + 1], 0f, vb, edge);
                var bC = new GridDrape.Vertex(sec.Cx[k + 1], sec.Cz[k + 1], 0.5f, vb, centre);
                var bR = new GridDrape.Vertex(sec.Rx[k + 1], sec.Rz[k + 1], 1f, vb, edge);
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
            double total = sec.S[n - 1];
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
                double cx = sec.Cx[k], cz = sec.Cz[k];
                float hc = Height(ref ctx, cx, cz);
                float yl, yc, yr, nx, ny, nz;
                if (bridge)
                {
                    // A level deck on the straight line between the ends, never below the lifted terrain under it.
                    double f = total > 0 ? sec.S[k] / total : 0;
                    float ground = Math.Max(hc, Math.Max(Height(ref ctx, sec.Lx[k], sec.Lz[k]), Height(ref ctx, sec.Rx[k], sec.Rz[k])));
                    yc = Math.Max((float)(hStart + (hEnd - hStart) * f), ground + lift);
                    yl = yr = yc;
                    nx = 0f;
                    ny = 1f;
                    nz = 0f;
                }
                else
                {
                    yl = Height(ref ctx, sec.Lx[k], sec.Lz[k]) + lift;
                    yc = hc + lift;
                    yr = Height(ref ctx, sec.Rx[k], sec.Rz[k]) + lift;
                    Normal(ref ctx, cx, cz, out nx, out ny, out nz);
                }
                float v = (float)(sec.S[k] / MetresPerV);
                int left = m.AddVertex((float)sec.Lx[k], yl, (float)sec.Lz[k], nx, ny, nz, edge, 0f, v);
                m.AddVertex((float)cx, yc, (float)cz, nx, ny, nz, centre, 0.5f, v);
                m.AddVertex((float)sec.Rx[k], yr, (float)sec.Rz[k], nx, ny, nz, edge, 1f, v);
                if (prev >= 0)
                {
                    Strip(m, prev, left, prev + 1, left + 1);
                    Strip(m, prev + 1, left + 1, prev + 2, left + 2);
                }
                prev = left;
            }
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
