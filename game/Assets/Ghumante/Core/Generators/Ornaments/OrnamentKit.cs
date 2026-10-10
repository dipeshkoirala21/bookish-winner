using System;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;
using Ghumante.Core.Meshing.Shapes;

namespace Ghumante.Core.Generators.Ornaments
{
    /// <summary>
    /// Per-build state of the ornament generators: the mesh, the LOD, the island site and the terrain, plus reusable
    /// scratch profiles and paths. One instance per thread (<see cref="For"/>), so a tile build allocates nothing once
    /// the scratch has grown. Heights: <see cref="Ground"/> is the terrain, <see cref="RoadY"/> the road surface round
    /// the island, <see cref="TopY"/> the lawn (or paving) of the island. Placement: the centrepiece stands at
    /// (<see cref="CX"/>, <see cref="CZ"/>) and registers its footprint; furniture asks <see cref="FindSpot"/> for a
    /// place on the island clear of every footprint already taken and of the road corridors.
    /// </summary>
    internal sealed class OrnCtx
    {
        public MeshData M;
        public IHeightSampler H;
        public RoundaboutSite Site;
        public RoundaboutDesign Design;
        public OrnamentStats Stats;

        /// <summary>Feature LOD 0-3 (3 = silhouette only).</summary>
        public int Lod;

        /// <summary>Segment LOD for the shapes library (LOD 3 uses level 2).</summary>
        public ShapeLod L;

        public uint Seed;

        /// <summary>Kerb top above the road surface.</summary>
        public float KerbH = 0.22f;

        /// <summary>Lawn top at the centrepiece centre (absolute).</summary>
        public float CentreTopY;

        /// <summary>Centrepiece facing (degrees clockwise from north).</summary>
        public double FacingDeg;

        /// <summary>Centrepiece centre (tile-local): the island centre plus the design's offset, pulled in to fit.</summary>
        public double CX, CZ;

        /// <summary>Road corridor guard (null = off) and the disc it ignores (the layout island itself).</summary>
        public IRoadCorridorQuery Corridors;

        public double ExemptX, ExemptZ, ExemptR;

        private float _lastGround;

        public readonly Profile2 P = new Profile2(96);
        public readonly Profile2 Q = new Profile2(96);
        public readonly Path3 Path = new Path3(256);
        public readonly Path3 Path2 = new Path3(256);
        public double[] Xs = new double[512], Zs = new double[512], Nx = new double[512], Nz = new double[512];
        public double[] Ys = new double[64], Ss = new double[64];

        /// <summary>Blocked samples of a garden ring (beds and hedges leave gaps round furniture).</summary>
        public readonly bool[] Blocked = new bool[257];

        /// <summary>Small scratch polygons (niche frames, spandrels).</summary>
        public readonly double[] Ax = new double[64], Ay = new double[64], Aw = new double[64];

        /// <summary>Footprints taken so far on this island.</summary>
        private OrnamentFootprint[] _taken = new OrnamentFootprint[32];

        private int _takenCount;

        [ThreadStatic] private static OrnCtx s_ctx;

        public static OrnCtx For()
        {
            return s_ctx ?? (s_ctx = new OrnCtx());
        }

        public void Begin(MeshData m, IHeightSampler h, in RoundaboutSite site, in RoundaboutDesign design, int lod, OrnamentStats stats,
                          IRoadCorridorQuery corridors)
        {
            M = m;
            H = h;
            Site = site;
            Design = design;
            Lod = lod < 0 ? 0 : lod > 3 ? 3 : lod;
            L = new ShapeLod(Math.Min(Lod, 2));
            Seed = design.Seed;
            Stats = stats;
            KerbH = 0.22f;
            _lastGround = 0f;
            _takenCount = 0;
            Corridors = corridors;
            ExemptX = site.X;
            ExemptZ = site.Z;
            ExemptR = site.LayoutIsland ? site.RadiusM : 0;
            float g;
            if (h != null && h.TryHeight(site.TileX0 + site.X, site.TileZ0 + site.Z, out g)) _lastGround = g;
            CX = site.X;
            CZ = site.Z;
            CentreTopY = TopY(CX, CZ);
            FacingDeg = float.IsNaN(design.FacingDeg) ? site.MainArmDeg : design.FacingDeg;
        }

        /// <summary>Place the centrepiece: <paramref name="reach"/> is its farthest extent from its own centre. The
        /// design's offset is kept as far as the island allows: the centrepiece stays inside the planting radius
        /// <paramref name="inner"/> with <paramref name="garden"/> metres of garden beyond it on the offset side, and
        /// sits at the centre of an island too small for that.</summary>
        public void PlaceCentre(double reach, double inner, double garden)
        {
            double ox = Design.OffsetEastM, oz = Design.OffsetNorthM, o = Math.Sqrt(ox * ox + oz * oz);
            double room = Math.Max(0, Math.Min(o, inner - reach - garden));
            double k = o > 1e-6 ? room / o : 0;
            CX = Site.X + ox * k;
            CZ = Site.Z + oz * k;
            CentreTopY = TopY(CX, CZ);
        }

        /// <summary>Terrain height at a tile-local point (the last good height where the sampler has none).</summary>
        public float Ground(double x, double z)
        {
            float h;
            if (H != null && H.TryHeight(Site.TileX0 + x, Site.TileZ0 + z, out h))
            {
                _lastGround = h;
                return h;
            }
            return _lastGround;
        }

        /// <summary>Road surface height next to the island.</summary>
        public float RoadY(double x, double z)
        {
            return Ground(x, z) + Site.RoadLiftM;
        }

        /// <summary>Island top (lawn or paving) at a tile-local point: kerb top plus a gentle dome on big islands.</summary>
        public float TopY(double x, double z)
        {
            double dx = x - Site.X, dz = z - Site.Z;
            double r = Math.Max(1.0, Site.RadiusM);
            double t = (dx * dx + dz * dz) / (r * r);
            double dome = Site.RadiusM > 8 ? 0.12 * Math.Max(0, 1 - t) : 0;
            return (float)(Ground(x, z) + Site.RoadLiftM + KerbH + (Site.ApronM > 0 ? 0.05 : 0) + dome);
        }

        /// <summary>The frame of the centrepiece: origin at its centre on <see cref="CentreTopY"/>, +Z towards
        /// <see cref="FacingDeg"/>.</summary>
        public Affine3 CentreFrame(double lift = 0)
        {
            return Affine3.Translation(CX, CentreTopY + lift, CZ) * Affine3.Yaw(FacingDeg);
        }

        /// <summary>Tile-local point of centrepiece-frame (u, w): u right, w forward (towards the facing).</summary>
        public void Local(double u, double w, out double x, out double z)
        {
            double a = FacingDeg * Math.PI / 180.0, s = Math.Sin(a), c = Math.Cos(a);
            x = CX + u * c + w * s;
            z = CZ - u * s + w * c;
        }

        // ------------------------------------------------------------------ placement

        /// <summary>Register a footprint (also recorded in the stats).</summary>
        public void Take(in OrnamentFootprint f)
        {
            if (_takenCount == _taken.Length) Array.Resize(ref _taken, _taken.Length * 2);
            _taken[_takenCount++] = f;
            if (Stats != null) Stats.Footprints.Add(f);
        }

        /// <summary>The first footprint of a kind taken so far.</summary>
        public bool TryFind(FootprintKind kind, out OrnamentFootprint f)
        {
            for (int i = 0; i < _takenCount; i++)
            {
                if (_taken[i].Kind != kind) continue;
                f = _taken[i];
                return true;
            }
            f = default(OrnamentFootprint);
            return false;
        }

        /// <summary>Distance from (x, z) to the nearest footprint taken so far (+infinity when none).</summary>
        public double Clearance(double x, double z)
        {
            double best = double.PositiveInfinity;
            for (int i = 0; i < _takenCount; i++) best = Math.Min(best, _taken[i].Distance(x, z));
            return best;
        }

        /// <summary>Distance from (x, z) to the nearest centrepiece footprint (+infinity when none).</summary>
        public double CentreClearance(double x, double z)
        {
            double best = double.PositiveInfinity;
            for (int i = 0; i < _takenCount; i++)
                if (_taken[i].Kind == FootprintKind.Centrepiece) best = Math.Min(best, _taken[i].Distance(x, z));
            return best;
        }

        /// <summary>The smallest radius round the island centre (in 0.25 m steps, up to <paramref name="max"/>) whose
        /// circle has a point at least <paramref name="gap"/> clear of every footprint taken so far: where the garden's
        /// rings can start (right outside a round centrepiece, in front of and behind Shahid Gate, beside an offset
        /// mandala); beds on such rings leave gaps where the centrepiece stands.</summary>
        public double FreeRadius(double max, double gap)
        {
            for (double r = 0.25; r < max; r += 0.25)
            {
                for (int k = 0; k < 48; k++)
                {
                    double a = 2 * Math.PI * k / 48;
                    if (Clearance(Site.X + r * Math.Sin(a), Site.Z + r * Math.Cos(a)) >= gap) return r;
                }
            }
            return max;
        }

        /// <summary>True when a disc of radius r at (x, z) would stand on a road corridor: outside the exempt layout
        /// island and closer than r to a corridor (always false without a corridor guard).</summary>
        public bool OnRoad(double x, double z, double r)
        {
            if (Corridors == null) return false;
            double ex = x - ExemptX, ez = z - ExemptZ;
            if (ExemptR > 0 && Math.Sqrt(ex * ex + ez * ez) + r <= ExemptR) return false;
            return Corridors.SignedDistance(Site.TileX0 + x, Site.TileZ0 + z) < r;
        }

        /// <summary>
        /// A free spot for a round item of radius <paramref name="itemR"/> (plus <paramref name="gap"/> clearance to the
        /// footprints already taken): on the circle of radius <paramref name="r"/> round the island centre, at the
        /// bearing nearest <paramref name="preferDeg"/> (tried in steps of <paramref name="stepDeg"/> either side, up to
        /// <paramref name="maxTurnDeg"/>), inside the planting radius <paramref name="inner"/> and off the road
        /// corridors. False when there is none.
        /// </summary>
        public bool FindSpot(double preferDeg, double r, double itemR, double gap, double inner, double stepDeg, double maxTurnDeg,
                             out double x, out double z, out double deg)
        {
            x = z = deg = 0;
            if (r + itemR > inner + 1e-6) return false;
            int steps = (int)Math.Floor(maxTurnDeg / Math.Max(1, stepDeg));
            for (int k = 0; k <= 2 * steps; k++)
            {
                int q = (k + 1) / 2;
                double d = preferDeg + ((k & 1) == 1 ? q : -q) * stepDeg;
                double a = d * Math.PI / 180.0;
                double px = Site.X + r * Math.Sin(a), pz = Site.Z + r * Math.Cos(a);
                if (Clearance(px, pz) < itemR + gap) continue;
                if (OnRoad(px, pz, itemR)) continue;
                x = px;
                z = pz;
                deg = d;
                return true;
            }
            return false;
        }

        /// <summary>Count an item that found no free spot.</summary>
        public void Skip()
        {
            if (Stats != null) Stats.Skipped++;
        }

        public int VStart
        {
            get { return M.VertexCount; }
        }

        public int IStart
        {
            get { return M.IndexCount; }
        }

        /// <summary>Bake AO for everything appended since (v0, i0), standing on <paramref name="groundY"/>.</summary>
        public void Ao(int v0, int i0, double groundY, float concavity = 0.6f)
        {
            AoSettings s = ShapeAo.Defaults(groundY);
            s.Concavity = concavity;
            ShapeAo.Bake(M, v0, M.VertexCount - v0, i0, M.IndexCount - i0, s);
        }
    }

    /// <summary>Small shared pieces: brushes, colour passes, flower and foliage blobs, rings of instances, scaled
    /// lofts (concave spires, flared roofs), flags and lamps.</summary>
    internal static class OrnamentKit
    {
        public static ShapeBrush B(uint rgba, MaterialChannel ch)
        {
            return new ShapeBrush(rgba, ch, 1f);
        }

        /// <summary>Recolour vertices [first, first + count) between two colours by a position hash with cells of
        /// <paramref name="cell"/> metres (flower beds: orange and yellow marigolds).</summary>
        public static void Speckle(MeshData m, int first, int count, uint a, uint b, uint seed, double cell, float bias = 0.5f)
        {
            double inv = 1.0 / Math.Max(1e-3, cell);
            for (int v = first; v < first + count; v++)
            {
                int ix = (int)Math.Floor(m.Positions[3 * v] * inv), iy = (int)Math.Floor(m.Positions[3 * v + 1] * inv),
                    iz = (int)Math.Floor(m.Positions[3 * v + 2] * inv);
                uint h = ShapeNoise.Hash(ix, iy, iz, seed);
                uint c = (h & 0xFFFF) < (uint)(bias * 65535f) ? a : b;
                m.Colors[4 * v] = (byte)(c >> 24);
                m.Colors[4 * v + 1] = (byte)(c >> 16);
                m.Colors[4 * v + 2] = (byte)(c >> 8);
            }
        }

        /// <summary>Recolour every vertex of the range.</summary>
        public static void Paint(MeshData m, int first, int count, uint c)
        {
            for (int v = first; v < first + count; v++)
            {
                m.Colors[4 * v] = (byte)(c >> 24);
                m.Colors[4 * v + 1] = (byte)(c >> 16);
                m.Colors[4 * v + 2] = (byte)(c >> 8);
            }
        }

        /// <summary>A soft clump (shrub, flower head, canopy lobe): a displaced cube sphere of radii (rx, ry, rz)
        /// centred at (x, y, z); <paramref name="bump"/> is the displacement as a fraction of the radius.</summary>
        public static int Clump(OrnCtx c, double x, double y, double z, double rx, double ry, double rz, uint col, MaterialChannel ch,
                                int segments, double bump, uint seed)
        {
            MeshData m = c.M;
            int v0 = m.VertexCount, i0 = m.IndexCount;
            Shapes.CubeSphere(m, Affine3.Translation(x, y, z) * Affine3.Scaling(rx, ry, rz), B(col, ch), 1, segments, c.L);
            if (bump > 0 && c.Lod < 2)
            {
                ShapeNoise.Displace(m, v0, m.VertexCount - v0, bump * Math.Min(rx, Math.Min(ry, rz)), 2.2 / Math.Max(0.2, rx), seed, 2);
                ShapeNoise.RecomputeNormals(m, v0, m.VertexCount - v0, i0, m.IndexCount - i0);
            }
            return v0;
        }

        /// <summary>A flower head (marigold pom-pom, rose): a tiny low-poly ball.</summary>
        public static void Bloom(OrnCtx c, double x, double y, double z, double r, uint col)
        {
            Shapes.Sphere(c.M, Affine3.Translation(x, y, z) * Affine3.Scaling(1, 0.8, 1), B(col, MaterialChannel.Foliage), r, 5, c.L);
            if (c.Stats != null) c.Stats.Flowers++;
        }

        /// <summary>
        /// A mound of flowers or a clipped hedge along a closed (or open) path at ground level: a swept rounded section
        /// (width × height), bumped by noise, coloured with <paramref name="a"/>/<paramref name="b"/> speckles. Path
        /// points are tile-local with y = ground.
        /// </summary>
        public static void Mound(OrnCtx c, Path3 path, bool closed, double width, double height, uint a, uint b, MaterialChannel ch,
                                 double bump, uint seed, double cell, bool smooth = true)
        {
            MeshData m = c.M;
            Profile2 sec = c.Q;
            sec.Clear(true);
            // A loaf: flat-ish bottom, rounded shoulders, slightly domed top (section x = right, y = up).
            double hw = 0.5 * width;
            sec.Add(-hw, -0.05, true).Add(hw, -0.05, true).Add(hw, 0.55 * height).Add(0.55 * hw, height).Add(-0.55 * hw, height)
               .Add(-hw, 0.55 * height);
            if (smooth && c.Lod == 0 && width <= 1.3 && path.Count <= 48) sec.Smooth(2);
            int v0 = m.VertexCount, i0 = m.IndexCount;
            Shapes.Sweep(m, Affine3.Identity, B(a, ch), path, sec, closed, true, SweepFrames.Upright);
            if (bump > 0 && c.Lod < 2)
            {
                ShapeNoise.Displace(m, v0, m.VertexCount - v0, bump, 2.4 / Math.Max(0.3, width), seed, 2);
                ShapeNoise.RecomputeNormals(m, v0, m.VertexCount - v0, i0, m.IndexCount - i0);
            }
            if (a != b) Speckle(m, v0, m.VertexCount - v0, a, b, seed, cell);
            ShapeColor.JitterByPosition(m, v0, m.VertexCount - v0, 0.08f, seed ^ 0x5A5Au, 0.4);
        }

        /// <summary>Fill <paramref name="p"/> with a circle of radius r round (cx, cz) at terrain + lift, starting at
        /// bearing <paramref name="startDeg"/> and sweeping <paramref name="sweepDeg"/> (clockwise from north), with a
        /// radial wobble r·(1 + wobble·sin(k·θ)).</summary>
        public static Path3 Ring(OrnCtx c, Path3 p, double cx, double cz, double r, double lift, double startDeg, double sweepDeg,
                                 int segments, double wobble = 0, int lobes = 0, bool onTop = true)
        {
            p.Clear();
            bool full = Math.Abs(sweepDeg) >= 359.9;
            int n = Math.Max(3, segments);
            int cnt = full ? n : n + 1;
            for (int k = 0; k < cnt; k++)
            {
                double a = (startDeg + sweepDeg * k / n) * Math.PI / 180.0;
                double rr = r * (1 + wobble * Math.Sin(lobes * a));
                double x = cx + rr * Math.Sin(a), z = cz + rr * Math.Cos(a);
                p.Add(x, (onTop ? c.TopY(x, z) : c.Ground(x, z)) + lift, z);
            }
            return p;
        }

        /// <summary>
        /// A solid whose horizontal sections are a closed plan profile (centred, unit scale, CCW) scaled by
        /// <paramref name="scales"/>[k] at heights <paramref name="ys"/>[k]: concave spires, flared roofs, tapered
        /// dies, obelisks. Normals follow the true surface (plan normal tilted by the scale slope). Optional caps.
        /// Returns the first vertex.
        /// </summary>
        public static int StackLoft(MeshData m, in Affine3 xf, in ShapeBrush b, Profile2 plan, double[] ys, double[] scales, int rings,
                                    bool capBottom, bool capTop, double scaleZ = 1)
        {
            int first = m.VertexCount;
            int n = plan.Count;
            if (n < 3 || rings < 2) return first;
            m.Reserve(n * rings + 2 * n + 2, n * (rings - 1) * 6 + 6 * n);
            for (int k = 0; k < rings; k++)
            {
                double s = scales[k];
                // ds/dy by central differences.
                int ka = k > 0 ? k - 1 : k, kb = k < rings - 1 ? k + 1 : k;
                double dy = ys[kb] - ys[ka];
                double dsdy = Math.Abs(dy) > 1e-9 ? (scales[kb] - scales[ka]) / dy : 0;
                for (int i = 0; i < n; i++)
                {
                    double px = plan.X[i], pz = plan.Y[i] * scaleZ;
                    // Plan outward normal: average of the neighbouring edge normals.
                    int ia = i > 0 ? i - 1 : n - 1, ib = i < n - 1 ? i + 1 : 0;
                    double ex = plan.X[ib] - plan.X[ia], ez = (plan.Y[ib] - plan.Y[ia]) * scaleZ;
                    double nx = ez, nz = -ex;
                    double nl = Math.Sqrt(nx * nx + nz * nz);
                    if (nl < 1e-12) nl = 1;
                    nx /= nl;
                    nz /= nl;
                    // Surface p = (s·px, y, s·pz): normal ∝ (nx, -dsdy·(px·nx + pz·nz), nz).
                    double ny = -dsdy * (px * nx + pz * nz);
                    double l = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                    double ox, oy, oz, onx, ony, onz;
                    xf.Point(s * px, ys[k], s * pz, out ox, out oy, out oz);
                    xf.Normal(nx / l, ny / l, nz / l, out onx, out ony, out onz);
                    Norm(ref onx, ref ony, ref onz);
                    m.AddVertex((float)ox, (float)oy, (float)oz, (float)onx, (float)ony, (float)onz, b.Color, (float)b.Channel, b.Ao);
                }
            }
            for (int k = 0; k + 1 < rings; k++)
            {
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    int a0 = first + k * n + i, a1 = first + k * n + j, b0 = first + (k + 1) * n + i, b1 = first + (k + 1) * n + j;
                    Tri(m, a0, b0, a1);
                    Tri(m, a1, b0, b1);
                }
            }
            if (capBottom) Cap(m, xf, b, plan, ys[0], scales[0], -1, scaleZ);
            if (capTop) Cap(m, xf, b, plan, ys[rings - 1], scales[rings - 1], 1, scaleZ);
            return first;
        }

        private static void Cap(MeshData m, in Affine3 xf, in ShapeBrush b, Profile2 plan, double y, double s, int dir, double scaleZ)
        {
            if (s < 1e-6) return;
            int n = plan.Count;
            double onx, ony, onz;
            xf.Normal(0, dir, 0, out onx, out ony, out onz);
            Norm(ref onx, ref ony, ref onz);
            double cx, cy, cz;
            xf.Point(0, y, 0, out cx, out cy, out cz);
            int c0 = m.AddVertex((float)cx, (float)cy, (float)cz, (float)onx, (float)ony, (float)onz, b.Color, (float)b.Channel, b.Ao);
            for (int i = 0; i < n; i++)
            {
                double ox, oy, oz;
                xf.Point(s * plan.X[i], y, s * plan.Y[i] * scaleZ, out ox, out oy, out oz);
                m.AddVertex((float)ox, (float)oy, (float)oz, (float)onx, (float)ony, (float)onz, b.Color, (float)b.Channel, b.Ao);
            }
            for (int i = 0; i < n; i++) Tri(m, c0, c0 + 1 + i, c0 + 1 + (i + 1) % n);
        }

        /// <summary>
        /// A rib swept along a centreline lying in the frame's XY plane (arches, lancet frames, niche mouldings): the
        /// unit closed <paramref name="unit"/> section (x across the rib in the plane, y along the frame's Z) is
        /// scaled to <paramref name="pw"/>[k] × <paramref name="depth"/> at each point. Optional flat end caps.
        /// Returns the first vertex.
        /// </summary>
        public static int PlanarSweep(MeshData m, in Affine3 f, in ShapeBrush b, double[] px, double[] py, double[] pw, int n, double depth, Profile2 unit,
                                      bool caps)
        {
            int first = m.VertexCount;
            int sn = unit.Count;
            if (n < 2 || sn < 3) return first;
            for (int k = 0; k < n; k++)
            {
                int ka = k > 0 ? k - 1 : k, kb = k < n - 1 ? k + 1 : k;
                double tx = px[kb] - px[ka], ty = py[kb] - py[ka], tl = Math.Sqrt(tx * tx + ty * ty);
                if (tl < 1e-12) tl = 1;
                tx /= tl;
                ty /= tl;
                double nxp = ty, nyp = -tx;
                for (int i = 0; i < sn; i++)
                {
                    int ia = i > 0 ? i - 1 : sn - 1, ib = (i + 1) % sn;
                    double ex = unit.X[ib] - unit.X[ia], ez = unit.Y[ib] - unit.Y[ia];
                    double snu = ez, snz = -ex, sl = Math.Sqrt(snu * snu + snz * snz);
                    if (sl < 1e-12) sl = 1;
                    snu /= sl;
                    snz /= sl;
                    if (unit.SignedArea() < 0)
                    {
                        snu = -snu;
                        snz = -snz;
                    }
                    double lx = px[k] + nxp * unit.X[i] * pw[k], ly = py[k] + nyp * unit.X[i] * pw[k], lz = unit.Y[i] * depth;
                    double ox, oy, oz, onx, ony, onz;
                    f.Point(lx, ly, lz, out ox, out oy, out oz);
                    f.Normal(nxp * snu, nyp * snu, snz, out onx, out ony, out onz);
                    Norm(ref onx, ref ony, ref onz);
                    m.AddVertex((float)ox, (float)oy, (float)oz, (float)onx, (float)ony, (float)onz, b.Color, (float)b.Channel, b.Ao);
                }
            }
            for (int k = 0; k + 1 < n; k++)
            {
                for (int i = 0; i < sn; i++)
                {
                    int j = (i + 1) % sn;
                    int a0 = first + k * sn + i, a1 = first + k * sn + j, b0 = first + (k + 1) * sn + i, b1 = first + (k + 1) * sn + j;
                    Tri(m, a0, b0, a1);
                    Tri(m, a1, b0, b1);
                }
            }
            if (caps)
            {
                for (int e = 0; e < 2; e++)
                {
                    int k = e == 0 ? 0 : n - 1, kn = e == 0 ? 1 : n - 2;
                    double tx = px[k] - px[kn], ty = py[k] - py[kn], tl = Math.Sqrt(tx * tx + ty * ty);
                    if (tl < 1e-12) continue;
                    double onx, ony, onz, ox, oy, oz;
                    f.Normal(tx / tl, ty / tl, 0, out onx, out ony, out onz);
                    Norm(ref onx, ref ony, ref onz);
                    f.Point(px[k], py[k], 0, out ox, out oy, out oz);
                    int c0 = m.AddVertex((float)ox, (float)oy, (float)oz, (float)onx, (float)ony, (float)onz, b.Color, (float)b.Channel, b.Ao);
                    for (int i = 0; i < sn; i++)
                    {
                        int v = first + k * sn + i;
                        m.AddVertex(m.Positions[3 * v], m.Positions[3 * v + 1], m.Positions[3 * v + 2], (float)onx, (float)ony, (float)onz, b.Color,
                                    (float)b.Channel, b.Ao);
                    }
                    for (int i = 0; i < sn; i++) Tri(m, c0, c0 + 1 + i, c0 + 1 + (i + 1) % sn);
                }
            }
            return first;
        }

        /// <summary>A plain box (sx × sy × sz centred on the frame origin, flat faces, 12 triangles) for small
        /// repeated parts where a rounded box would waste triangles: posts, plaques, panels, beams.</summary>
        public static void Box(MeshData m, in Affine3 xf, in ShapeBrush b, double sx, double sy, double sz)
        {
            double hx = 0.5 * sx, hy = 0.5 * sy, hz = 0.5 * sz;
            for (int face = 0; face < 6; face++)
            {
                int axis = face >> 1;
                double sgn = (face & 1) == 0 ? 1 : -1;
                double nx = axis == 0 ? sgn : 0, ny = axis == 1 ? sgn : 0, nz = axis == 2 ? sgn : 0;
                double onx, ony, onz;
                xf.Normal(nx, ny, nz, out onx, out ony, out onz);
                Norm(ref onx, ref ony, ref onz);
                int v0 = m.VertexCount;
                for (int k = 0; k < 4; k++)
                {
                    double u = (k == 0 || k == 3) ? -1 : 1, w = k < 2 ? -1 : 1;
                    double px, py, pz;
                    if (axis == 0)
                    {
                        px = sgn * hx;
                        py = u * hy;
                        pz = w * hz;
                    }
                    else if (axis == 1)
                    {
                        px = u * hx;
                        py = sgn * hy;
                        pz = w * hz;
                    }
                    else
                    {
                        px = u * hx;
                        py = w * hy;
                        pz = sgn * hz;
                    }
                    double ox, oy, oz;
                    xf.Point(px, py, pz, out ox, out oy, out oz);
                    m.AddVertex((float)ox, (float)oy, (float)oz, (float)onx, (float)ony, (float)onz, b.Color, (float)b.Channel, b.Ao);
                }
                Tri(m, v0, v0 + 1, v0 + 2);
                Tri(m, v0, v0 + 2, v0 + 3);
            }
        }

        /// <summary>Append a triangle with its winding fixed so its face normal agrees with the vertex normals.</summary>
        public static void Tri(MeshData m, int a, int b, int c)
        {
            float[] p = m.Positions, n = m.Normals;
            double e1x = p[3 * b] - p[3 * a], e1y = p[3 * b + 1] - p[3 * a + 1], e1z = p[3 * b + 2] - p[3 * a + 2];
            double e2x = p[3 * c] - p[3 * a], e2y = p[3 * c + 1] - p[3 * a + 1], e2z = p[3 * c + 2] - p[3 * a + 2];
            double fx = e1y * e2z - e1z * e2y, fy = e1z * e2x - e1x * e2z, fz = e1x * e2y - e1y * e2x;
            double sx = n[3 * a] + n[3 * b] + n[3 * c], sy = n[3 * a + 1] + n[3 * b + 1] + n[3 * c + 1], sz = n[3 * a + 2] + n[3 * b + 2] + n[3 * c + 2];
            if (fx * sx + fy * sy + fz * sz >= 0) m.AddTriangle(a, b, c);
            else m.AddTriangle(a, c, b);
        }

        public static void Norm(ref double x, ref double y, ref double z)
        {
            double l = Math.Sqrt(x * x + y * y + z * z);
            if (l < 1e-12) return;
            x /= l;
            y /= l;
            z /= l;
        }

        /// <summary>A flat double-sided polygon (x, y in the frame's XY plane, z = 0) with its front facing +Z;
        /// <paramref name="thick"/> separates the two faces (no z-fighting). Fan-triangulated from the centroid
        /// (convex or star-shaped polygons).</summary>
        public static void FlatPoly(MeshData m, in Affine3 xf, uint col, MaterialChannel ch, double[] px, double[] py, int n, double thick,
                                    bool backFace = true)
        {
            for (int side = 0; side < (backFace ? 2 : 1); side++)
            {
                double dz = side == 0 ? 0.5 * thick : -0.5 * thick, nz = side == 0 ? 1 : -1;
                double cx = 0, cy = 0;
                for (int i = 0; i < n; i++)
                {
                    cx += px[i];
                    cy += py[i];
                }
                cx /= n;
                cy /= n;
                double onx, ony, onz, ox, oy, oz;
                xf.Normal(0, 0, nz, out onx, out ony, out onz);
                Norm(ref onx, ref ony, ref onz);
                xf.Point(cx, cy, dz, out ox, out oy, out oz);
                int c0 = m.AddVertex((float)ox, (float)oy, (float)oz, (float)onx, (float)ony, (float)onz, col, (float)ch, 1f);
                for (int i = 0; i < n; i++)
                {
                    xf.Point(px[i], py[i], dz, out ox, out oy, out oz);
                    m.AddVertex((float)ox, (float)oy, (float)oz, (float)onx, (float)ony, (float)onz, col, (float)ch, 1f);
                }
                for (int i = 0; i < n; i++) Tri(m, c0, c0 + 1 + i, c0 + 1 + (i + 1) % n);
            }
        }

        /// <summary>A rectangle in the frame's XY plane (front +Z), single-sided.</summary>
        public static void Panel(MeshData m, in Affine3 xf, uint col, MaterialChannel ch, double x0, double y0, double x1, double y1, double z)
        {
            double onx, ony, onz;
            xf.Normal(0, 0, 1, out onx, out ony, out onz);
            Norm(ref onx, ref ony, ref onz);
            int v0 = m.VertexCount;
            double ox, oy, oz;
            xf.Point(x0, y0, z, out ox, out oy, out oz);
            m.AddVertex((float)ox, (float)oy, (float)oz, (float)onx, (float)ony, (float)onz, col, (float)ch, 1f);
            xf.Point(x1, y0, z, out ox, out oy, out oz);
            m.AddVertex((float)ox, (float)oy, (float)oz, (float)onx, (float)ony, (float)onz, col, (float)ch, 1f);
            xf.Point(x1, y1, z, out ox, out oy, out oz);
            m.AddVertex((float)ox, (float)oy, (float)oz, (float)onx, (float)ony, (float)onz, col, (float)ch, 1f);
            xf.Point(x0, y1, z, out ox, out oy, out oz);
            m.AddVertex((float)ox, (float)oy, (float)oz, (float)onx, (float)ony, (float)onz, col, (float)ch, 1f);
            Tri(m, v0, v0 + 1, v0 + 2);
            Tri(m, v0, v0 + 2, v0 + 3);
        }

        // ------------------------------------------------------------------ the national flag

        [ThreadStatic] private static double[] s_fx, s_fy;

        /// <summary>
        /// The flag of Nepal flying from a pole: the double pennant (crimson with a blue border) with the white moon
        /// and sun on both faces, the hoist on the frame's Y axis from y = 0 to <paramref name="h"/>, flying along +X
        /// (width h / 1.22), waving gently in Z.
        /// </summary>
        public static void Flag(OrnCtx c, in Affine3 xf, double h)
        {
            if (s_fx == null)
            {
                s_fx = new double[48];
                s_fy = new double[48];
                s_cx = new double[48];
                s_cy = new double[48];
            }
            MeshData m = c.M;
            double w = h / 1.22;
            int v0 = m.VertexCount;
            // The cloth is cut into vertical strips sharing the same stations in every layer, and the flutter is
            // piecewise linear between the stations, so each layer stays exactly parallel to the others (no
            // z-fighting between the blue border, the crimson field and the white emblems).
            int strips = c.Lod == 0 ? 6 : c.Lod == 1 ? 3 : 1;
            // Outline: hoist bottom, lower tip, notch, upper tip, hoist top (both hypotenuses at about 45 degrees).
            double[] ox = s_ox, oy = s_oy;
            ox[0] = 0; oy[0] = 0;
            ox[1] = w; oy[1] = 0;
            ox[2] = 0.4 * w; oy[2] = 0.6 * w;
            ox[3] = 0.66 * w; oy[3] = 0.6 * w;
            ox[4] = 0; oy[4] = h;
            Pennants(m, xf, ox, oy, c.Lod >= 3 ? OrnamentPalette.FlagCrimson : OrnamentPalette.FlagBlue, 0.002 * h, strips, w);
            if (c.Lod < 3)
            {
                // The crimson field, inset by the border (continuous across the notch line).
                double bw = 0.07 * w;
                ox[0] = bw; oy[0] = bw;
                ox[1] = w - 2.41 * bw; oy[1] = bw;
                ox[2] = 0.4 * w - 2.41 * bw; oy[2] = 0.6 * w + bw;
                ox[3] = 0.66 * w - 2.6 * bw; oy[3] = 0.6 * w + bw;
                ox[4] = bw; oy[4] = h - 2.6 * bw;
                Pennants(m, xf, ox, oy, OrnamentPalette.FlagCrimson, 0.006 * h, strips, w);
                if (c.Lod < 2)
                {
                    // Moon (upper pennant): a crescent, horns up, cradling a disc with eight rays. Sun (lower
                    // pennant): a disc with twelve rays. White on both faces.
                    Crescent(m, xf, 0.19 * w, 0.835 * w, 0.115 * w, c.Lod == 0 ? 12 : 6, 0.014 * h);
                    StarOrDisc(m, xf, 0.19 * w, 0.875 * w, 0.072 * w, 0.62, 8, 0.014 * h);
                    StarOrDisc(m, xf, 0.2 * w, 0.29 * w, 0.14 * w, 0.62, 12, 0.014 * h);
                }
            }
            // Flutter along the frame's Z, piecewise linear between the strip stations.
            double ax = xf.M00, ay = xf.M10, az = xf.M20, al2 = ax * ax + ay * ay + az * az;
            double zx = xf.M02, zy = xf.M12, zz = xf.M22, zl = Math.Sqrt(zx * zx + zy * zy + zz * zz);
            if (zl < 1e-12) return;
            zx /= zl;
            zy /= zl;
            zz /= zl;
            for (int v = v0; v < m.VertexCount; v++)
            {
                double px = m.Positions[3 * v] - xf.M03, py = m.Positions[3 * v + 1] - xf.M13, pz = m.Positions[3 * v + 2] - xf.M23;
                double lx = (px * ax + py * ay + pz * az) / al2;
                double t = Math.Max(0, Math.Min(strips, lx / w * strips));
                int k = Math.Min(strips - 1, (int)t);
                double wa = Wave(h, (double)k / strips), wb = Wave(h, (double)(k + 1) / strips);
                double wave = wa + (wb - wa) * (t - k);
                m.Positions[3 * v] += (float)(zx * wave);
                m.Positions[3 * v + 1] += (float)(zy * wave);
                m.Positions[3 * v + 2] += (float)(zz * wave);
            }
        }

        private static double Wave(double h, double u)
        {
            return 0.05 * h * Math.Sin(u * 5.0) * u;
        }

        [ThreadStatic] private static double[] s_oxBuf, s_oyBuf, s_cx, s_cy;

        private static double[] s_ox
        {
            get { return s_oxBuf ?? (s_oxBuf = new double[8]); }
        }

        private static double[] s_oy
        {
            get { return s_oyBuf ?? (s_oyBuf = new double[8]); }
        }

        private static void Pennants(MeshData m, in Affine3 xf, double[] x, double[] y, uint col, double thick, int strips, double w)
        {
            // Lower pennant: hoist bottom, lower tip, notch, and the hoist point level with the notch.
            double hy = y[2];
            s_fx[0] = x[0];
            s_fy[0] = y[0];
            s_fx[1] = x[1];
            s_fy[1] = y[1];
            s_fx[2] = x[2];
            s_fy[2] = y[2];
            s_fx[3] = x[0];
            s_fy[3] = hy;
            StripPoly(m, xf, col, s_fx, s_fy, 4, thick, strips, w);
            // Upper pennant: hoist at notch level, upper tip, hoist top.
            s_fx[0] = x[4];
            s_fy[0] = hy;
            s_fx[1] = x[3];
            s_fy[1] = y[3];
            s_fx[2] = x[4];
            s_fy[2] = y[4];
            StripPoly(m, xf, col, s_fx, s_fy, 3, thick, strips, w);
        }

        /// <summary>A convex polygon cut into vertical strips at x = k·w/strips (Sutherland-Hodgman), each piece a
        /// double-sided flat fan.</summary>
        private static void StripPoly(MeshData m, in Affine3 xf, uint col, double[] px, double[] py, int n, double thick, int strips, double w)
        {
            for (int k = 0; k < strips; k++)
            {
                double xa = k == 0 ? double.NegativeInfinity : w * k / strips, xb = k == strips - 1 ? double.PositiveInfinity : w * (k + 1) / strips;
                int cn = ClipX(px, py, n, xa, xb, s_cx, s_cy);
                if (cn >= 3) FlatPoly(m, xf, col, MaterialChannel.Fabric, s_cx, s_cy, cn, thick);
            }
        }

        private static int ClipX(double[] px, double[] py, int n, double xa, double xb, double[] ox, double[] oy)
        {
            // Clip against x >= xa into a scratch, then against x <= xb into the output.
            double[] tx = s_tx ?? (s_tx = new double[48]), ty = s_ty ?? (s_ty = new double[48]);
            int tn = 0;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                bool ai = px[i] >= xa, aj = px[j] >= xa;
                if (ai)
                {
                    tx[tn] = px[i];
                    ty[tn++] = py[i];
                }
                if (ai != aj)
                {
                    double t = (xa - px[i]) / (px[j] - px[i]);
                    tx[tn] = xa;
                    ty[tn++] = py[i] + (py[j] - py[i]) * t;
                }
            }
            int on = 0;
            for (int i = 0; i < tn; i++)
            {
                int j = (i + 1) % tn;
                bool ai = tx[i] <= xb, aj = tx[j] <= xb;
                if (ai)
                {
                    ox[on] = tx[i];
                    oy[on++] = ty[i];
                }
                if (ai != aj)
                {
                    double t = (xb - tx[i]) / (tx[j] - tx[i]);
                    ox[on] = xb;
                    oy[on++] = ty[i] + (ty[j] - ty[i]) * t;
                }
            }
            return on;
        }

        [ThreadStatic] private static double[] s_tx, s_ty;

        /// <summary>A disc of <paramref name="n"/> points, or with <paramref name="spike"/> &gt; 0 a star of
        /// <paramref name="n"/> triangular rays (inner radius r·(1 − 0.35·spike)), double-sided.</summary>
        private static void StarOrDisc(MeshData m, in Affine3 xf, double cx, double cy, double r, double spike, int n, double thick)
        {
            int k = spike > 0 ? 2 * n : n;
            for (int i = 0; i < k; i++)
            {
                double a = 2 * Math.PI * i / k;
                double rr = spike > 0 && (i & 1) == 1 ? r * (1 - 0.35 * spike) : r;
                s_fx[i] = cx + rr * Math.Cos(a);
                s_fy[i] = cy + rr * Math.Sin(a);
            }
            FlatPoly(m, xf, OrnamentPalette.FlagWhite, MaterialChannel.Fabric, s_fx, s_fy, k, thick);
        }

        /// <summary>
        /// A white crescent, horns up, double-sided: the part of the disc of radius <paramref name="r"/> at (cx, cy)
        /// outside a second disc of radius 0.85·r raised by 0.45·r, built as a band of quads between the two lower arcs
        /// (a crescent is not star-shaped, so it cannot be fanned).
        /// </summary>
        private static void Crescent(MeshData m, in Affine3 xf, double cx, double cy, double r, int segs, double thick)
        {
            const double r2 = 0.85, d = 0.45;
            double hy = (1 - r2 * r2 + d * d) / (2 * d), hx = Math.Sqrt(Math.Max(0, 1 - hy * hy));
            double a0 = Math.Atan2(hy, -hx), a1 = Math.Atan2(hy, hx) + 2 * Math.PI;
            double b0 = Math.Atan2(hy - d, -hx), b1 = Math.Atan2(hy - d, hx) + 2 * Math.PI;
            if (a0 < 0) a0 += 2 * Math.PI;
            if (b0 < 0) b0 += 2 * Math.PI;
            for (int side = 0; side < 2; side++)
            {
                double dz = side == 0 ? 0.5 * thick : -0.5 * thick, nz = side == 0 ? 1 : -1;
                double onx, ony, onz;
                xf.Normal(0, 0, nz, out onx, out ony, out onz);
                Norm(ref onx, ref ony, ref onz);
                int v0 = m.VertexCount;
                for (int k = 0; k <= segs; k++)
                {
                    double t = (double)k / segs, a = a0 + (a1 - a0) * t, b = b0 + (b1 - b0) * t, ox, oy, oz;
                    xf.Point(cx + r * Math.Cos(a), cy + r * Math.Sin(a), dz, out ox, out oy, out oz);
                    m.AddVertex((float)ox, (float)oy, (float)oz, (float)onx, (float)ony, (float)onz, OrnamentPalette.FlagWhite, (float)MaterialChannel.Fabric, 1f);
                    xf.Point(cx + r * r2 * Math.Cos(b), cy + r * (d + r2 * Math.Sin(b)), dz, out ox, out oy, out oz);
                    m.AddVertex((float)ox, (float)oy, (float)oz, (float)onx, (float)ony, (float)onz, OrnamentPalette.FlagWhite, (float)MaterialChannel.Fabric, 1f);
                }
                for (int k = 0; k < segs; k++)
                {
                    int a = v0 + 2 * k;
                    if (k > 0) Tri(m, a, a + 2, a + 1);
                    if (k < segs - 1) Tri(m, a + 1, a + 2, a + 3);
                }
            }
        }

        // ------------------------------------------------------------------ street furniture

        /// <summary>A solar street light standing at tile-local (x, z): a tapered steel pole, an arm with the LED head
        /// facing <paramref name="armDeg"/>, a battery box and a tilted solar panel on top (Kathmandu islands).</summary>
        public static void SolarLamp(OrnCtx c, double x, double z, double armDeg, double height)
        {
            MeshData m = c.M;
            int v0 = m.VertexCount, i0 = m.IndexCount;
            float y = c.TopY(x, z);
            Affine3 f = Affine3.Translation(x, y, z) * Affine3.Yaw(armDeg);
            ShapeBrush pole = B(OrnamentPalette.PoleGrey, MaterialChannel.Metal);
            if (c.Lod >= 2)
            {
                Shapes.Cylinder(m, f, pole, 0.08, height, 4, 0, 0, false, false, c.L);
                Box(m, Affine3.Translation(x, y + height + 0.15, z) * Affine3.RotationX(-0.4), B(OrnamentPalette.SolarPanel, MaterialChannel.Glass), 1.0, 0.05, 0.65);
                if (c.Stats != null) c.Stats.LampPosts++;
                return;
            }
            Shapes.Frustum(m, f, pole, 0.11, 0.06, height, 6, 0, 0, false, true, c.L);
            Shapes.Cylinder(m, f, B(OrnamentPalette.PoleGrey, MaterialChannel.Concrete), 0.18, 0.3, 6, 0, 0, false, true, c.L);
            if (c.Lod < 2)
            {
                // Arm and LED head.
                if (c.Lod == 0) Shapes.Bar(m, f, pole, 0, height - 0.35, 0, 0, height - 0.1, 1.1, 0.035, 4, c.L);
                Box(m, f * Affine3.Translation(0, height - 0.14, 1.25), B(OrnamentPalette.LampHead, MaterialChannel.Metal), 0.26, 0.09, 0.62);
                if (c.Lod == 0) Box(m, f * Affine3.Translation(0, height - 0.19, 1.25), B(OrnamentPalette.LampGlass, MaterialChannel.Glass), 0.2, 0.02, 0.5);
                // Battery box on the pole (close up only).
                if (c.Lod == 0) Box(m, f * Affine3.Translation(0, 0.62 * height, -0.14), B(OrnamentPalette.Steel, MaterialChannel.Metal), 0.32, 0.45, 0.16);
            }
            // Solar panel on top, tilted 25 degrees towards the south.
            Affine3 pf = Affine3.Translation(x, y + height + 0.18, z) * Affine3.Yaw(180) * Affine3.RotationX(-25 * Math.PI / 180);
            Box(m, pf, B(OrnamentPalette.SolarPanel, MaterialChannel.Glass), 1.05, 0.04, 0.68);
            if (c.Lod == 0)
                Box(m, pf * Affine3.Translation(0, -0.025, 0), B(OrnamentPalette.Steel, MaterialChannel.Metal), 1.1, 0.03, 0.72);
            Shapes.Bar(m, Affine3.Identity, pole, x, y + height - 0.05, z, x, y + height + 0.16, z, 0.03, 4, c.L);
            c.Ao(v0, i0, y);
            if (c.Stats != null) c.Stats.LampPosts++;
        }

        /// <summary>A globe lamp on a short post (fountain rims, Jawalakhel).</summary>
        public static void GlobeLamp(OrnCtx c, double x, double y, double z, double postH)
        {
            MeshData m = c.M;
            Affine3 f = Affine3.Translation(x, y, z);
            Shapes.Cylinder(m, f, B(OrnamentPalette.IronBlack, MaterialChannel.Metal), 0.045, postH, 5, 0, 0, false, true, c.L);
            Shapes.Sphere(m, f * Affine3.Translation(0, postH + 0.13, 0), B(OrnamentPalette.FlagWhite, MaterialChannel.Glass), 0.15, 7, c.L);
            if (c.Stats != null) c.Stats.LampPosts++;
        }

        /// <summary>A big terracotta pot with a leafy plant at tile-local (x, z) on a surface at y.</summary>
        public static void Pot(OrnCtx c, double x, double y, double z, double r, uint seed)
        {
            MeshData m = c.M;
            Profile2 p = c.Q.Clear(false);
            p.Add(0, 0, true).Add(0.62 * r, 0, true).Add(0.95 * r, 0.65 * r).Add(1.0 * r, 1.05 * r, true).Add(1.1 * r, 1.12 * r, true)
             .Add(1.1 * r, 1.2 * r, true).Add(0.9 * r, 1.2 * r, true).Add(0.9 * r, 1.1 * r, true).Add(0, 1.1 * r, true);
            Shapes.Lathe(m, Affine3.Translation(x, y, z), B(OrnamentPalette.Terracotta, MaterialChannel.Plaster), p, 10, c.L);
            Clump(c, x, y + 1.35 * r, z, 0.85 * r, 0.6 * r, 0.85 * r, OrnamentPalette.LeafGreen, MaterialChannel.Foliage, 8, 0.25, seed);
            if (c.Lod == 0)
            {
                for (int k = 0; k < 3; k++)
                {
                    double a = 2.1 * k + seed % 7;
                    Bloom(c, x + 0.55 * r * Math.Cos(a), y + 1.75 * r, z + 0.55 * r * Math.Sin(a), 0.09 * Math.Max(1, r), k == 1 ? OrnamentPalette.MarigoldYellow : OrnamentPalette.MarigoldOrange);
                }
            }
            if (c.Stats != null) c.Stats.Shrubs++;
        }
    }
}
