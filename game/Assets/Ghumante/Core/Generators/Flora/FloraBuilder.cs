using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Generators.Flora
{
    /// <summary>
    /// The primitives the flora generators are built from (docs/W2_DETAIL_CONTRACT.md §1.6 look: rounded, smooth
    /// shading, many small parts): lumpy ellipsoid "puffs" for canopy clumps, shrubs and blooms (in any orientation,
    /// optionally spiky for needle tufts), swept tubes for trunks, branches, roots, culms and stems (with buttress
    /// flutes and bark colour patches), two-sided folded blades for leaves, fronds and grass, flat stars for flowers
    /// and bracts, ground discs, lathes for pots and stacks, bevelled boxes for platforms and hedges, and
    /// noise-displaced icosahedron rocks. Every vertex carries the material channel and baked ambient occlusion in UV0
    /// (contract §5: u = channel, v = AO, 1 open, 0 occluded) and an RGBA tint whose alpha is the instance-tint weight:
    /// 255 on foliage (the renderer varies the green per instance), 0 on bark, flowers, pots and stone.
    /// <para>
    /// Triangles are oriented automatically so their Unity front face agrees with the vertex normals. Meshes are built
    /// once per species, LOD and month (never per frame); scratch buffers are thread-static. Deterministic.
    /// </para>
    /// </summary>
    internal sealed class FloraBuilder
    {
        public readonly MeshData M;

        public FloraBuilder(MeshData m)
        {
            M = m ?? throw new ArgumentNullException(nameof(m));
        }

        // ---------------------------------------------------------------------------------------------------------
        // Colours, vertices and triangles

        /// <summary>A foliage colour: 0xRRGGBB scaled by <paramref name="shade"/>, alpha 255 (instance-tinted).</summary>
        public static uint Leaf(uint rgb, float shade)
        {
            uint c = MeshColor.FromHex(rgb);
            if (shade != 1f) c = MeshColor.Scale(c, shade);
            return (c & 0xFFFFFF00u) | 0xFFu;
        }

        /// <summary>A fixed colour (alpha 0: never tinted) from 0xRRGGBB scaled by <paramref name="shade"/>.</summary>
        public static uint Fixed(uint rgb, float shade = 1f)
        {
            uint c = MeshColor.FromHex(rgb);
            if (shade != 1f) c = MeshColor.Scale(c, shade);
            return c & 0xFFFFFF00u;
        }

        /// <summary>Blend of two 0xRRGGBB colours (t clamped to [0, 1]); returns 0xRRGGBB.</summary>
        public static uint Mix(uint a, uint b, float t)
        {
            return MeshColor.Lerp(MeshColor.FromHex(a), MeshColor.FromHex(b), t) >> 8;
        }

        public int Vertex(Vec3 p, Vec3 n, uint rgba, MaterialChannel ch, float ao)
        {
            ao = ao < 0.05f ? 0.05f : ao > 1f ? 1f : ao;
            return M.AddVertex(p.X, p.Y, p.Z, n.X, n.Y, n.Z, rgba, (float)ch, ao);
        }

        /// <summary>A triangle, flipped when needed so its front faces along the summed vertex normals.</summary>
        public void Tri(int a, int b, int c)
        {
            float[] p = M.Positions, n = M.Normals;
            double ux = p[3 * b] - p[3 * a], uy = p[3 * b + 1] - p[3 * a + 1], uz = p[3 * b + 2] - p[3 * a + 2];
            double vx = p[3 * c] - p[3 * a], vy = p[3 * c + 1] - p[3 * a + 1], vz = p[3 * c + 2] - p[3 * a + 2];
            double fx = uy * vz - uz * vy, fy = uz * vx - ux * vz, fz = ux * vy - uy * vx;
            double sx = n[3 * a] + n[3 * b] + n[3 * c], sy = n[3 * a + 1] + n[3 * b + 1] + n[3 * c + 1], sz = n[3 * a + 2] + n[3 * b + 2] + n[3 * c + 2];
            if (fx * sx + fy * sy + fz * sz < 0) M.AddTriangle(a, c, b);
            else M.AddTriangle(a, b, c);
        }

        public void Quad(int a, int b, int c, int d)
        {
            Tri(a, b, c);
            Tri(a, c, d);
        }

        /// <summary>A triangle oriented to face <paramref name="want"/> (ignoring vertex normals).</summary>
        public void AddOriented(int a, int b, int c, Vec3 want)
        {
            float[] p = M.Positions;
            double ux = p[3 * b] - p[3 * a], uy = p[3 * b + 1] - p[3 * a + 1], uz = p[3 * b + 2] - p[3 * a + 2];
            double vx = p[3 * c] - p[3 * a], vy = p[3 * c + 1] - p[3 * a + 1], vz = p[3 * c + 2] - p[3 * a + 2];
            double fx = uy * vz - uz * vy, fy = uz * vx - ux * vz, fz = ux * vy - uy * vx;
            if (fx * want.X + fy * want.Y + fz * want.Z < 0) M.AddTriangle(a, c, b);
            else M.AddTriangle(a, b, c);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Puffs (canopy clumps, shrubs, blooms, tufts)

        /// <summary>How a puff is shaded.</summary>
        public struct PuffStyle
        {
            /// <summary>Base colour 0xRRGGBB.</summary>
            public uint Rgb;

            /// <summary>Foliage (alpha 255, instance-tinted) or fixed (alpha 0).</summary>
            public bool Tinted;

            public MaterialChannel Channel;

            /// <summary>Centre of the whole crown: normals bend toward "away from it" by <see cref="Bend"/>, so a
            /// canopy of many puffs shades as one soft ball while its silhouette stays lumpy.</summary>
            public Vec3 CrownCentre;

            public float Bend;

            /// <summary>Crown radius for the AO falloff toward the crown centre (0: no inner darkening).</summary>
            public float CrownRadius;

            /// <summary>Shade at the bottom and at the top of the puff.</summary>
            public float ShadeLow, ShadeHigh;

            /// <summary>AO at the underside of the puff (1 at the top).</summary>
            public float AoLow;

            /// <summary>High-frequency spikes (fraction of the radius): needle tufts, marigold heads.</summary>
            public float Spike;

            /// <summary>Colour of the sunlit top (0: <see cref="Rgb"/>) the shade blends toward with height: a
            /// yellower, lighter green on top of a canopy reads as sun on leaves.</summary>
            public uint TopRgb;

            public static PuffStyle Foliage(uint rgb, Vec3 crown, float crownRadius)
            {
                return new PuffStyle
                {
                    Rgb = rgb, Tinted = true, Channel = MaterialChannel.Foliage, CrownCentre = crown, Bend = 0.4f, CrownRadius = crownRadius,
                    ShadeLow = 0.74f, ShadeHigh = 1.06f, AoLow = 0.5f,
                };
            }

            public static PuffStyle Solid(uint rgb, MaterialChannel ch)
            {
                return new PuffStyle { Rgb = rgb, Channel = ch, ShadeLow = 0.84f, ShadeHigh = 1.06f, AoLow = 0.7f };
            }
        }

        /// <summary>Unit icosphere directions and faces: level 0 (12 vertices, 20 faces), 1 (42, 80), 2 (162, 320).</summary>
        private sealed class Ico
        {
            public readonly Vec3[] V;
            public readonly int[] F;

            public Ico(Vec3[] v, int[] f)
            {
                V = v;
                F = f;
            }
        }

        private static Ico[] _icos;

        /// <summary>Built on first use (the static vertex tables below are initialised after this field would be);
        /// a race only builds identical tables twice.</summary>
        private static Ico[] Icos
        {
            get { return _icos ?? (_icos = BuildIcos()); }
        }

        private static Ico[] BuildIcos()
        {
            var levels = new Ico[3];
            var v = new System.Collections.Generic.List<Vec3>();
            for (int i = 0; i < 12; i++) v.Add(new Vec3(IcoV[3 * i], IcoV[3 * i + 1], IcoV[3 * i + 2]).Normalized);
            var f = new System.Collections.Generic.List<int>(IcoF);
            levels[0] = new Ico(v.ToArray(), f.ToArray());
            for (int l = 1; l < 3; l++)
            {
                var mids = new System.Collections.Generic.Dictionary<long, int>();
                var nf = new System.Collections.Generic.List<int>();
                for (int t = 0; t + 2 < f.Count; t += 3)
                {
                    int a = f[t], b = f[t + 1], c = f[t + 2];
                    int ab = MidIndex(v, mids, a, b), bc = MidIndex(v, mids, b, c), ca = MidIndex(v, mids, c, a);
                    nf.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                f = nf;
                levels[l] = new Ico(v.ToArray(), f.ToArray());
            }
            return levels;
        }

        private static int MidIndex(System.Collections.Generic.List<Vec3> v, System.Collections.Generic.Dictionary<long, int> mids, int a, int b)
        {
            long key = a < b ? (long)a << 32 | (uint)b : (long)b << 32 | (uint)a;
            int m;
            if (mids.TryGetValue(key, out m)) return m;
            m = v.Count;
            v.Add(((v[a] + v[b]) * 0.5f).Normalized);
            mids.Add(key, m);
            return m;
        }

        /// <summary>Triangles of a puff at a resolution (0: 20, 1: 80, 2: 320).</summary>
        public static int PuffTris(int res)
        {
            return Icos[res < 0 ? 0 : res > 2 ? 2 : res].F.Length / 3;
        }

        [ThreadStatic] private static int[] _ico;

        /// <summary>
        /// A lumpy ellipsoid about the vertical axis: an icosphere of resolution <paramref name="res"/> (0: 20
        /// triangles, 1: 80, 2: 320; evenly tessellated, so the silhouette stays round), radii <paramref name="r"/>,
        /// displaced by smooth noise of amplitude <paramref name="lumps"/> (fraction of the radius), turned by
        /// <paramref name="yaw"/> about the vertical. Returns the triangles added.
        /// </summary>
        public int Puff(Vec3 c, Vec3 r, int res, float lumps, uint seed, in PuffStyle st, float yaw = 0f)
        {
            float cy = (float)Math.Cos(yaw), sy = (float)Math.Sin(yaw);
            return PuffBasis(c, new Vec3(cy, 0f, sy), Vec3.Up, new Vec3(-sy, 0f, cy), r, res, lumps, seed, st);
        }

        /// <summary>A puff whose local Y axis points along <paramref name="axis"/> (unit): radius
        /// <paramref name="along"/> along it and <paramref name="across"/> across (needle tufts, culm sprays, hanging
        /// brushes).</summary>
        public int PuffAlong(Vec3 c, Vec3 axis, float along, float across, int res, float lumps, uint seed, in PuffStyle st)
        {
            Vec3 ey = axis.Normalized;
            Vec3 ex = ey.AnyPerpendicular();
            Vec3 ez = Vec3.Cross(ex, ey).Normalized;
            return PuffBasis(c, ex, ey, ez, new Vec3(across, along, across), res, lumps, seed, st);
        }

        private int PuffBasis(Vec3 c, Vec3 ex, Vec3 ey, Vec3 ez, Vec3 r, int res, float lumps, uint seed, in PuffStyle st)
        {
            int t0 = M.TriangleCount;
            Ico ico = Icos[res < 0 ? 0 : res > 2 ? 2 : res];
            if (_ico == null || _ico.Length < ico.V.Length) _ico = new int[Math.Max(ico.V.Length, 162)];
            int[] idx = _ico;
            for (int i = 0; i < ico.V.Length; i++) idx[i] = PuffVertex(c, ex, ey, ez, r, ico.V[i], lumps, seed, st);
            for (int t = 0; t + 2 < ico.F.Length; t += 3) Tri(idx[ico.F[t]], idx[ico.F[t + 1]], idx[ico.F[t + 2]]);
            return M.TriangleCount - t0;
        }

        private int PuffVertex(Vec3 c, Vec3 ex, Vec3 ey, Vec3 ez, Vec3 r, Vec3 d, float lumps, uint seed, in PuffStyle st)
        {
            Vec3 p = PuffPoint(c, ex, ey, ez, r, d, lumps, st.Spike, seed);
            // Normal of the displaced surface from finite differences in two tangent directions.
            Vec3 t1 = d.AnyPerpendicular(), t2 = Vec3.Cross(d, t1);
            const float e = 0.1f;
            Vec3 pa = PuffPoint(c, ex, ey, ez, r, (d + t1 * e).Normalized, lumps, st.Spike, seed) - PuffPoint(c, ex, ey, ez, r, (d - t1 * e).Normalized, lumps, st.Spike, seed);
            Vec3 pb = PuffPoint(c, ex, ey, ez, r, (d + t2 * e).Normalized, lumps, st.Spike, seed) - PuffPoint(c, ex, ey, ez, r, (d - t2 * e).Normalized, lumps, st.Spike, seed);
            Vec3 n = Vec3.Cross(pa, pb).Normalized;
            Vec3 outward = (p - c).Normalized;
            if (Vec3.Dot(n, outward) < 0) n = -n;
            if (st.Bend > 0f) n = Vec3.Lerp(n, (p - st.CrownCentre).Normalized, st.Bend).Normalized;
            float up = 0.5f + 0.5f * outward.Y;
            float ao = st.AoLow + (1f - st.AoLow) * up;
            if (st.CrownRadius > 0f)
            {
                float rr = (p - st.CrownCentre).Length / st.CrownRadius;
                ao *= 0.55f + 0.45f * Math.Min(1f, rr);
            }
            float shade = st.ShadeLow + (st.ShadeHigh - st.ShadeLow) * up + 0.07f * FloraNoise.Value(p.X * 1.3, p.Y * 1.3, p.Z * 1.3, seed ^ 0x51u);
            uint rgb = st.TopRgb != 0 ? Mix(st.Rgb, st.TopRgb, Math.Max(0f, outward.Y)) : st.Rgb;
            uint rgba = st.Tinted ? Leaf(rgb, shade * (0.84f + 0.16f * ao)) : Fixed(rgb, shade);
            return Vertex(p, n, rgba, st.Channel, ao);
        }

        /// <summary>A point on the surface of a puff (for fringe leaves and blooms) and its outward direction.</summary>
        public static Vec3 PuffSurface(Vec3 c, Vec3 r, Vec3 d, float yaw)
        {
            float cy = (float)Math.Cos(yaw), sy = (float)Math.Sin(yaw);
            float x = d.X * r.X, z = d.Z * r.Z;
            return new Vec3(c.X + x * cy - z * sy, c.Y + d.Y * r.Y, c.Z + x * sy + z * cy);
        }

        /// <summary>
        /// A leaf card sticking out of a canopy surface at <paramref name="b"/> along <paramref name="axis"/>: a diamond
        /// <paramref name="len"/> long and <paramref name="width"/> wide, two-sided (4 triangles). Both faces shade with
        /// the canopy's outward direction <paramref name="outward"/> (pushed a little toward each face), so the fringe
        /// breaks the silhouette into leaves without lighting up as flat cards.
        /// </summary>
        public int LeafCard(Vec3 b, Vec3 axis, Vec3 outward, float len, float width, uint rgba, float ao)
        {
            int t0 = M.TriangleCount;
            axis = axis.Normalized;
            Vec3 side = Vec3.Cross(axis, outward);
            if (side.Length < 1e-4f) side = axis.AnyPerpendicular();
            side = side.Normalized;
            Vec3 ng = Vec3.Cross(side, axis).Normalized;
            Vec3 tip = b + axis * len, mid = b + axis * (0.42f * len);
            Vec3 l = mid - side * (0.5f * width), r = mid + side * (0.5f * width);
            for (int face = 0; face < 2; face++)
            {
                Vec3 fn = face == 0 ? ng : -ng;
                // The outward direction without its part across the card, plus the face normal: always on the
                // face's side, and still shading like the canopy around it.
                Vec3 n = ((outward - fn * Vec3.Dot(outward, fn)) * 0.7f + fn * 0.75f).Normalized;
                int vb = Vertex(b, n, rgba, MaterialChannel.Foliage, ao * 0.85f);
                int vl = Vertex(l, n, rgba, MaterialChannel.Foliage, ao);
                int vr = Vertex(r, n, rgba, MaterialChannel.Foliage, ao);
                int vt = Vertex(tip, n, rgba, MaterialChannel.Foliage, Math.Min(1f, ao * 1.1f));
                AddOriented(vb, vl, vt, fn);
                AddOriented(vb, vt, vr, fn);
            }
            return M.TriangleCount - t0;
        }

        private static Vec3 PuffPoint(Vec3 c, Vec3 ex, Vec3 ey, Vec3 ez, Vec3 r, Vec3 d, float lumps, float spike, uint seed)
        {
            float k = 1f;
            if (lumps > 0f)
            {
                k += lumps * FloraNoise.Value(d.X * 1.7 + 11.3, d.Y * 1.7 + 3.1, d.Z * 1.7 + 7.7, seed);
                k += 0.5f * lumps * FloraNoise.Value(d.X * 3.9 + 1.3, d.Y * 3.9 + 5.9, d.Z * 3.9 + 2.7, seed ^ 0xA5A5u);
            }
            if (spike > 0f) k += spike * Math.Abs(FloraNoise.Value(d.X * 6.5 + 4.1, d.Y * 6.5 + 8.3, d.Z * 6.5 + 0.7, seed ^ 0x5A5Au));
            float x = d.X * r.X * k, y = d.Y * r.Y * k, z = d.Z * r.Z * k;
            return c + ex * x + ey * y + ez * z;
        }

        /// <summary>A small smooth blob (an octahedron with outward normals: 8 triangles, reads round when small):
        /// flower heads, rhododendron trusses, berries, buds.</summary>
        public int Blob(Vec3 c, float rx, float ry, float rz, uint rgba, MaterialChannel ch, float aoBottom)
        {
            int t0 = M.TriangleCount;
            uint under = (MeshColor.Scale(rgba | 0xFFu, 0.8f) & 0xFFFFFF00u) | (rgba & 0xFFu);
            int top = Vertex(c + new Vec3(0f, ry, 0f), Vec3.Up, rgba, ch, 1f);
            int bot = Vertex(c - new Vec3(0f, ry, 0f), -Vec3.Up, under, ch, aoBottom);
            int e = Vertex(c + new Vec3(rx, 0f, 0f), new Vec3(1f, 0f, 0f), rgba, ch, 0.85f);
            int n = Vertex(c + new Vec3(0f, 0f, rz), new Vec3(0f, 0f, 1f), rgba, ch, 0.85f);
            int w = Vertex(c - new Vec3(rx, 0f, 0f), new Vec3(-1f, 0f, 0f), rgba, ch, 0.85f);
            int s = Vertex(c - new Vec3(0f, 0f, rz), new Vec3(0f, 0f, -1f), rgba, ch, 0.85f);
            Tri(top, e, n);
            Tri(top, n, w);
            Tri(top, w, s);
            Tri(top, s, e);
            Tri(bot, n, e);
            Tri(bot, w, n);
            Tri(bot, s, w);
            Tri(bot, e, s);
            return M.TriangleCount - t0;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Tubes (trunks, branches, roots, culms, stems)

        [ThreadStatic] private static int[] _tubePrev, _tubeCur;

        /// <summary>How a tube is coloured.</summary>
        public struct TubeStyle
        {
            public uint Rgb;

            /// <summary>Second colour mixed in by noise patches (peeling eucalyptus bark, moss, lichen); 0 none.</summary>
            public uint PatchRgb;

            public float PatchAmount;

            /// <summary>Bands of <see cref="PatchRgb"/> at every other ring (bamboo nodes, palm leaf scars) instead of
            /// noise patches.</summary>
            public bool Banded;

            public MaterialChannel Channel;

            /// <summary>Radius roughness (fraction): furrowed bark.</summary>
            public float Rough;

            public bool Tinted;

            public static TubeStyle Bark(uint rgb, float rough = 0.06f)
            {
                return new TubeStyle { Rgb = rgb, Channel = MaterialChannel.Bark, Rough = rough };
            }
        }

        /// <summary>
        /// A tube swept along <paramref name="n"/> points with per-point radii (parallel-transport frames, so it never
        /// twists). <paramref name="lobes"/> &gt; 0 flutes the first third into buttress roots. AO runs from
        /// <paramref name="aoStart"/> to <paramref name="aoEnd"/>; optional rounded cap at the end. Returns triangles.
        /// </summary>
        public int Tube(Vec3[] pts, float[] radii, int n, int sides, in TubeStyle st, float aoStart, float aoEnd, bool capEnd,
                        int lobes = 0, float lobeDepth = 0f, uint seed = 0)
        {
            int t0 = M.TriangleCount;
            if (n < 2) return 0;
            if (_tubePrev == null || _tubePrev.Length < sides + 1)
            {
                _tubePrev = new int[Math.Max(sides + 1, 32)];
                _tubeCur = new int[_tubePrev.Length];
            }
            Vec3 u = (pts[1] - pts[0]).Normalized.AnyPerpendicular();
            float total = 0f;
            for (int i = 1; i < n; i++) total += (pts[i] - pts[i - 1]).Length;
            float along = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) along += (pts[i] - pts[i - 1]).Length;
                Vec3 ti = i == 0 ? (pts[1] - pts[0]).Normalized
                    : i == n - 1 ? (pts[n - 1] - pts[n - 2]).Normalized
                    : ((pts[i + 1] - pts[i]).Normalized + (pts[i] - pts[i - 1]).Normalized).Normalized;
                u = (u - ti * Vec3.Dot(u, ti)).Normalized; // parallel transport
                Vec3 w = Vec3.Cross(ti, u);
                float f = total > 0f ? along / total : 0f;
                float ao = aoStart + (aoEnd - aoStart) * f;
                float taper = i + 1 < n ? (radii[i] - radii[i + 1]) / Math.Max(1e-3f, (pts[i + 1] - pts[i]).Length)
                    : (radii[i - 1] - radii[i]) / Math.Max(1e-3f, (pts[i] - pts[i - 1]).Length);
                for (int s = 0; s < sides; s++)
                {
                    double a = 2 * Math.PI * s / sides;
                    float ca = (float)Math.Cos(a), sa = (float)Math.Sin(a);
                    float r = radii[i];
                    if (lobes > 0 && f < 0.34f)
                    {
                        float lobe = (float)Math.Pow(Math.Max(0.0, Math.Cos(lobes * a)), 3.0);
                        float fade = 1f - f / 0.34f;
                        r *= 1f + lobeDepth * lobe * fade * fade;
                    }
                    if (st.Rough > 0f) r *= 1f + st.Rough * FloraNoise.Value(ca * 2.1 + i * 0.7, sa * 2.1, along * 0.9, seed);
                    Vec3 radial = u * ca + w * sa;
                    Vec3 p = pts[i] + radial * r;
                    Vec3 nrm = (radial + ti * taper).Normalized;
                    uint rgb = st.Rgb;
                    float shade = 0.92f + 0.12f * FloraNoise.Value(p.X * 2.7, p.Y * 1.1, p.Z * 2.7, seed ^ 0x77u);
                    if (st.PatchRgb != 0)
                    {
                        float m = st.Banded ? (i % 2 == 1 && i + 1 < n ? st.PatchAmount : 0f)
                            : Smooth(0.1f, 0.5f, FloraNoise.Value(p.X * 1.6, p.Y * 0.8, p.Z * 1.6, seed ^ 0x3131u)) * st.PatchAmount;
                        rgb = Mix(rgb, st.PatchRgb, m);
                    }
                    uint rgba = st.Tinted ? Leaf(rgb, shade) : Fixed(rgb, shade);
                    _tubeCur[s] = Vertex(p, nrm, rgba, st.Channel, ao);
                }
                if (i > 0)
                    for (int s = 0; s < sides; s++)
                    {
                        int s1 = (s + 1) % sides;
                        Quad(_tubePrev[s], _tubeCur[s], _tubeCur[s1], _tubePrev[s1]);
                    }
                int[] tmp = _tubePrev;
                _tubePrev = _tubeCur;
                _tubeCur = tmp;
            }
            if (capEnd)
            {
                Vec3 tn = (pts[n - 1] - pts[n - 2]).Normalized;
                uint rgba = st.Tinted ? Leaf(st.Rgb, 0.95f) : Fixed(st.Rgb, 0.95f);
                int cv = Vertex(pts[n - 1] + tn * (radii[n - 1] * 0.5f), tn, rgba, st.Channel, aoEnd);
                for (int s = 0; s < sides; s++) Tri(cv, _tubePrev[s], _tubePrev[(s + 1) % sides]);
            }
            return M.TriangleCount - t0;
        }

        private static float Smooth(float e0, float e1, float x)
        {
            float t = (x - e0) / (e1 - e0);
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return t * t * (3f - 2f * t);
        }

        [ThreadStatic] private static Vec3[] _pts;
        [ThreadStatic] private static float[] _radii;

        /// <summary>Scratch point buffer (valid until the next call).</summary>
        public static Vec3[] Pts(int n)
        {
            if (_pts == null || _pts.Length < n) _pts = new Vec3[Math.Max(n, 32)];
            return _pts;
        }

        /// <summary>Scratch radius buffer (valid until the next call).</summary>
        public static float[] Radii(int n)
        {
            if (_radii == null || _radii.Length < n) _radii = new float[Math.Max(n, 32)];
            return _radii;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Blades, leaves, stars, discs

        /// <summary>
        /// A two-sided tapered strip from <paramref name="base0"/> along <paramref name="dir"/> (unit), bending down by
        /// <paramref name="droop"/> (metres at the tip, quadratic), <paramref name="segs"/> segments, width from
        /// <paramref name="w0"/> at the base through <paramref name="wMid"/> to <paramref name="wTip"/> (0: pointed),
        /// its edges dropped below the midrib by <paramref name="fold"/> × width (a V fold reads as a real leaf).
        /// <paramref name="side"/> is the width direction. Both faces share positions with opposite normals; the midrib
        /// can carry its own colour (banana, palm); <paramref name="serrate"/> narrows every other ring into a zig-zag
        /// edge (pinnate palm fronds, fern leaflets). Returns triangles.
        /// </summary>
        public int Blade(Vec3 base0, Vec3 dir, Vec3 side, float length, float w0, float wMid, float wTip, float droop, float fold,
                         int segs, uint rgba, MaterialChannel ch, float ao0, float ao1, uint midribRgba = 0, float serrate = 0f)
        {
            int t0 = M.TriangleCount;
            if (segs < 1) segs = 1;
            side = side.Normalized;
            Vec3 nrm = Vec3.Cross(side, dir).Normalized;
            if (nrm.Y < 0) nrm = -nrm;
            bool pointed = wTip <= 1e-4f;
            uint mid = fold > 0f && midribRgba != 0 ? midribRgba : rgba;
            for (int face = 0; face < 2; face++)
            {
                int pl = -1, pm = -1, pr = -1;
                for (int i = 0; i <= segs; i++)
                {
                    float f = (float)i / segs;
                    Vec3 c = base0 + dir * (length * f) - Vec3.Up * (droop * f * f);
                    Vec3 tan = (dir * length - Vec3.Up * (2f * droop * f)).Normalized;
                    Vec3 bn = Vec3.Cross(side, tan).Normalized;
                    if (Vec3.Dot(bn, nrm) < 0) bn = -bn;
                    if (face == 1) bn = -bn;
                    float w = f < 0.5f ? w0 + (wMid - w0) * (f / 0.5f) : wMid + (wTip - wMid) * ((f - 0.5f) / 0.5f);
                    if (serrate > 0f && (i & 1) == 1) w *= 1f - serrate; // pinnate leaflets: a zig-zag edge
                    float ao = ao0 + (ao1 - ao0) * f;
                    if (i == segs && pointed)
                    {
                        int tip = Vertex(c, bn, mid, ch, ao);
                        if (fold > 0f)
                        {
                            Tri(pl, tip, pm);
                            Tri(pm, tip, pr);
                        }
                        else Tri(pl, tip, pr);
                        break;
                    }
                    Vec3 drop = nrm * (fold * w * 0.3f);
                    float lean = face == 0 ? 0.3f * fold : -0.3f * fold;
                    int l = Vertex(c - side * (w * 0.5f) - drop, (bn - side * lean).Normalized, rgba, ch, ao * 0.95f);
                    int r = Vertex(c + side * (w * 0.5f) - drop, (bn + side * lean).Normalized, rgba, ch, ao * 0.95f);
                    int m = fold > 0f ? Vertex(c, bn, mid, ch, ao) : -1;
                    if (pl >= 0)
                    {
                        if (fold > 0f)
                        {
                            Quad(pl, l, m, pm);
                            Quad(pm, m, r, pr);
                        }
                        else Quad(pl, l, r, pr);
                    }
                    pl = l;
                    pm = m;
                    pr = r;
                }
            }
            return M.TriangleCount - t0;
        }

        /// <summary>A single two-sided grass blade (a triangle per face; 2 triangles), lit mostly from above.</summary>
        public void GrassBlade(Vec3 b, Vec3 tip, float width, uint rgbaBase, uint rgbaTip, float aoBase, MaterialChannel ch = MaterialChannel.Grass)
        {
            Vec3 axis = (tip - b).Normalized;
            Vec3 side = Vec3.Cross(axis, Vec3.Up);
            if (side.Length < 1e-4f) side = new Vec3(1f, 0f, 0f);
            side = side.Normalized;
            Vec3 n = Vec3.Cross(side, axis).Normalized;
            if (n.Y < 0) n = -n;
            Vec3 nTilt = (n + Vec3.Up * 0.8f).Normalized;
            for (int face = 0; face < 2; face++)
            {
                Vec3 fn = face == 0 ? nTilt : new Vec3(-nTilt.X, nTilt.Y, -nTilt.Z);
                int a = Vertex(b - side * (width * 0.5f), fn, rgbaBase, ch, aoBase);
                int c = Vertex(b + side * (width * 0.5f), fn, rgbaBase, ch, aoBase);
                int t = Vertex(tip, fn, rgbaTip, ch, 1f);
                AddOriented(a, c, t, face == 0 ? n : -n);
            }
        }

        /// <summary>A flat two-sided star (or disc when <paramref name="inner"/> equals <paramref name="outer"/>) of
        /// <paramref name="points"/> points around <paramref name="c"/> facing <paramref name="normal"/>, its centre
        /// raised by <paramref name="cup"/>. Triangles: 2·points·(2 for a star, 1 for a disc).</summary>
        public int Star(Vec3 c, Vec3 normal, int points, float outer, float inner, float cup, uint rgba, uint centre, MaterialChannel ch, float ao, double rot = 0)
        {
            int t0 = M.TriangleCount;
            Vec3 nn = normal.Normalized, u = nn.AnyPerpendicular(), w = Vec3.Cross(nn, u);
            bool star = inner < outer * 0.999f;
            int n = star ? points * 2 : points;
            for (int face = 0; face < 2; face++)
            {
                Vec3 fn = face == 0 ? nn : -nn;
                int cv = Vertex(c + nn * cup, fn, centre, ch, ao);
                int first = -1, prev = -1;
                for (int k = 0; k <= n; k++)
                {
                    int kk = k % n;
                    double a = rot + 2 * Math.PI * kk / n;
                    float r = star && (kk & 1) == 1 ? inner : outer;
                    Vec3 dir = u * (float)Math.Cos(a) + w * (float)Math.Sin(a);
                    int v = k < n ? Vertex(c + dir * r, (fn + dir * 0.3f).Normalized, rgba, ch, ao) : first;
                    if (k == 0) first = v;
                    if (prev >= 0) AddOriented(cv, prev, v, fn);
                    prev = v;
                }
            }
            return M.TriangleCount - t0;
        }

        /// <summary>A flat up-facing irregular disc (decal) at <paramref name="c"/>: an n-gon fan with a wobbly rim.</summary>
        public int Disc(Vec3 c, float rx, float rz, int n, uint rgba, MaterialChannel ch, float aoCentre, float aoEdge, uint seed = 0, float wobble = 0f, uint rimRgba = 0)
        {
            int t0 = M.TriangleCount;
            int cv = Vertex(c, Vec3.Up, rgba, ch, aoCentre);
            int first = -1, prev = -1;
            uint rim = rimRgba != 0 ? rimRgba : rgba;
            for (int k = 0; k <= n; k++)
            {
                int kk = k % n;
                double a = 2 * Math.PI * kk / n;
                float wob = 1f + wobble * FloraNoise.Value(Math.Cos(a) * 1.5, Math.Sin(a) * 1.5, seed);
                int v = k < n ? Vertex(new Vec3(c.X + rx * wob * (float)Math.Cos(a), c.Y, c.Z + rz * wob * (float)Math.Sin(a)), Vec3.Up, rim, ch, aoEdge) : first;
                if (k == 0) first = v;
                if (prev >= 0) AddOriented(cv, prev, v, Vec3.Up);
                prev = v;
            }
            return M.TriangleCount - t0;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Lathes and boxes

        /// <summary>A surface of revolution about the vertical axis through <paramref name="c"/>: profile radii and
        /// heights (bottom to top), <paramref name="sides"/> around, radii displaced by noise of amplitude
        /// <paramref name="lumps"/> (fraction); optional flat top cap at <paramref name="capY"/> (default: the last
        /// ring). Normals from the profile slope. Per-ring colours when <paramref name="ringRgba"/> is given.
        /// Triangles: 2·sides·(n − 1) (+ sides for the cap).</summary>
        public int Lathe(Vec3 c, float[] r, float[] y, int n, int sides, uint rgba, MaterialChannel ch, float aoBottom, float aoTop, bool capTop,
                         uint capRgba = 0, MaterialChannel capCh = MaterialChannel.Plain, float capY = float.NaN, float lumps = 0f, uint seed = 0,
                         uint[] ringRgba = null)
        {
            int t0 = M.TriangleCount;
            if (_tubePrev == null || _tubePrev.Length < sides + 1)
            {
                _tubePrev = new int[Math.Max(sides + 1, 32)];
                _tubeCur = new int[_tubePrev.Length];
            }
            for (int i = 0; i < n; i++)
            {
                float dr, dy;
                if (i == 0)
                {
                    dr = r[1] - r[0];
                    dy = y[1] - y[0];
                }
                else if (i == n - 1)
                {
                    dr = r[i] - r[i - 1];
                    dy = y[i] - y[i - 1];
                }
                else
                {
                    dr = r[i + 1] - r[i - 1];
                    dy = y[i + 1] - y[i - 1];
                }
                float f = n > 1 ? (float)i / (n - 1) : 0f;
                float ao = aoBottom + (aoTop - aoBottom) * f;
                uint col = ringRgba != null ? ringRgba[i] : rgba;
                for (int s = 0; s < sides; s++)
                {
                    double a = 2 * Math.PI * s / sides;
                    float ca = (float)Math.Cos(a), sa = (float)Math.Sin(a);
                    float rr = r[i];
                    if (lumps > 0f) rr *= 1f + lumps * FloraNoise.Value(ca * 1.8 + 3.3, y[i] * 1.4, sa * 1.8 + 7.1, seed);
                    Vec3 nrm = new Vec3(ca * dy, -dr, sa * dy).Normalized;
                    _tubeCur[s] = Vertex(new Vec3(c.X + rr * ca, c.Y + y[i], c.Z + rr * sa), nrm, col, ch, ao);
                }
                if (i > 0)
                    for (int s = 0; s < sides; s++)
                    {
                        int s1 = (s + 1) % sides;
                        Quad(_tubePrev[s], _tubeCur[s], _tubeCur[s1], _tubePrev[s1]);
                    }
                int[] tmp = _tubePrev;
                _tubePrev = _tubeCur;
                _tubeCur = tmp;
            }
            if (capTop)
            {
                float cy = float.IsNaN(capY) ? y[n - 1] : capY;
                uint cr = capRgba != 0 ? capRgba : rgba;
                MaterialChannel cc = capCh == MaterialChannel.Plain ? ch : capCh;
                int cv = Vertex(new Vec3(c.X, c.Y + cy, c.Z), Vec3.Up, cr, cc, aoTop);
                for (int s = 0; s < sides; s++)
                {
                    // The cap reuses the top ring's plan positions with up normals (a crisp rim).
                    int a = _tubePrev[s], b = _tubePrev[(s + 1) % sides];
                    int va = Vertex(new Vec3(M.Positions[3 * a], c.Y + cy, M.Positions[3 * a + 2]), Vec3.Up, cr, cc, aoTop);
                    int vb = Vertex(new Vec3(M.Positions[3 * b], c.Y + cy, M.Positions[3 * b + 2]), Vec3.Up, cr, cc, aoTop);
                    AddOriented(cv, va, vb, Vec3.Up);
                }
            }
            return M.TriangleCount - t0;
        }

        [ThreadStatic] private static int[] _bevel;

        /// <summary>
        /// A box with chamfered vertical edges and a bevelled top (an octagon in plan): centre (cx, cz), half sizes,
        /// from y0 to y1, chamfer <paramref name="bevel"/>, turned by <paramref name="yawRad"/>; optional lumpy sides
        /// (hedges). No bottom. 38 triangles; reads as a rounded block.
        /// </summary>
        public int BevelBox(float cx, float cz, float hx, float hz, float y0, float y1, float bevel, float yawRad, uint side, uint top,
                            MaterialChannel ch, float aoBottom, MaterialChannel topCh = MaterialChannel.Plain, uint seed = 0, float lumps = 0f)
        {
            int t0 = M.TriangleCount;
            bevel = Math.Min(bevel, Math.Min(Math.Min(hx, hz) * 0.9f, (y1 - y0) * 0.45f));
            if (topCh == MaterialChannel.Plain) topCh = ch;
            float cy = (float)Math.Cos(yawRad), sy = (float)Math.Sin(yawRad);
            if (_bevel == null) _bevel = new int[24];
            int[] v = _bevel;
            float yb = y1 - bevel;
            for (int i = 0; i < 8; i++)
            {
                float lx, lz;
                Corner(i, hx, hz, bevel, out lx, out lz);
                Vec3 radial = new Vec3(lx / Math.Max(hx, 1e-3f), 0f, lz / Math.Max(hz, 1e-3f)).Normalized;
                Vec3 nr = Rot(radial, cy, sy);
                float wob = lumps > 0f ? 1f + lumps * FloraNoise.Value(lx * 1.3 + cx, lz * 1.3 + cz, seed) : 1f;
                float wobTop = lumps > 0f ? 1f + lumps * FloraNoise.Value(lx * 1.3 + cx + 5.5, lz * 1.3 + cz, seed) : 1f;
                Vec3 pb = Rot(new Vec3(lx * wob, 0f, lz * wob), cy, sy) + new Vec3(cx, y0, cz);
                Vec3 pm = Rot(new Vec3(lx * wobTop, 0f, lz * wobTop), cy, sy) + new Vec3(cx, yb, cz);
                float ix = lx - Math.Sign(lx) * bevel * 0.7f, iz = lz - Math.Sign(lz) * bevel * 0.7f;
                Vec3 pt = Rot(new Vec3(ix * wobTop, 0f, iz * wobTop), cy, sy) + new Vec3(cx, y1, cz);
                v[i] = Vertex(pb, nr, side, ch, aoBottom);
                v[8 + i] = Vertex(pm, (nr + Vec3.Up * 0.35f).Normalized, side, ch, 1f);
                v[16 + i] = Vertex(pt, (nr * 0.45f + Vec3.Up).Normalized, top, topCh, 1f);
            }
            for (int i = 0; i < 8; i++)
            {
                int j = (i + 1) % 8;
                Quad(v[i], v[j], v[8 + j], v[8 + i]);
                Quad(v[8 + i], v[8 + j], v[16 + j], v[16 + i]);
            }
            for (int i = 1; i + 1 < 8; i++) AddOriented(v[16], v[16 + i], v[16 + i + 1], Vec3.Up);
            return M.TriangleCount - t0;
        }

        /// <summary>Octagon corner <paramref name="i"/> (counter-clockwise from +X) of a chamfered rectangle.</summary>
        private static void Corner(int i, float hx, float hz, float bevel, out float x, out float z)
        {
            switch (i)
            {
                case 0: x = hx; z = -hz + bevel; break;
                case 1: x = hx; z = hz - bevel; break;
                case 2: x = hx - bevel; z = hz; break;
                case 3: x = -hx + bevel; z = hz; break;
                case 4: x = -hx; z = hz - bevel; break;
                case 5: x = -hx; z = -hz + bevel; break;
                case 6: x = -hx + bevel; z = -hz; break;
                default: x = hx - bevel; z = -hz; break;
            }
        }

        private static Vec3 Rot(Vec3 v, float c, float s)
        {
            return new Vec3(v.X * c - v.Z * s, v.Y, v.X * s + v.Z * c);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Rocks

        private static readonly float[] IcoV =
        {
            -1, 1.618034f, 0, 1, 1.618034f, 0, -1, -1.618034f, 0, 1, -1.618034f, 0,
            0, -1, 1.618034f, 0, 1, 1.618034f, 0, -1, -1.618034f, 0, 1, -1.618034f,
            1.618034f, 0, -1, 1.618034f, 0, 1, -1.618034f, 0, -1, -1.618034f, 0, 1,
        };

        private static readonly int[] IcoF =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };

        private sealed class RockScratch
        {
            public readonly float[] P = new float[42 * 3];
            public readonly int[] F = new int[80 * 3], F2 = new int[80 * 3];
            public readonly int[] KeyA = new int[30], KeyB = new int[30], KeyV = new int[30];
            public readonly Vec3[] Pos = new Vec3[42], Nrm = new Vec3[42];
            public readonly int[] Idx = new int[42];
        }

        [ThreadStatic] private static RockScratch _rock;

        /// <summary>
        /// A rock or boulder: an icosahedron (subdivided once with <paramref name="subdiv"/> ≥ 1: 80 triangles, else
        /// 20) displaced by noise (<paramref name="rough"/>), scaled to <paramref name="size"/> (half extents), with a
        /// flattened base sunk into the ground and weathered flat top facets; smooth normals, AO darker toward the
        /// ground and in crevices, moss or lichen (<paramref name="mossRgb"/>, amount <paramref name="moss"/>) on the
        /// upper faces. Stone channel.
        /// </summary>
        public int Rock(Vec3 c, Vec3 size, int subdiv, float rough, uint seed, uint rgb, uint mossRgb, float moss)
        {
            int t0 = M.TriangleCount;
            RockScratch sc = _rock ?? (_rock = new RockScratch());
            float[] p = sc.P;
            int[] f = sc.F;
            int nv = 12, nf = 20;
            for (int i = 0; i < 36; i++) p[i] = IcoV[i];
            for (int i = 0; i < 60; i++) f[i] = IcoF[i];
            if (subdiv >= 1)
            {
                int nk = 0, k2 = 0;
                int[] g = sc.F2;
                for (int t = 0; t < 20; t++)
                {
                    int a = f[3 * t], b = f[3 * t + 1], cc = f[3 * t + 2];
                    int ab = Mid(a, b, p, ref nv, sc, ref nk);
                    int bc = Mid(b, cc, p, ref nv, sc, ref nk);
                    int ca = Mid(cc, a, p, ref nv, sc, ref nk);
                    g[k2++] = a; g[k2++] = ab; g[k2++] = ca;
                    g[k2++] = b; g[k2++] = bc; g[k2++] = ab;
                    g[k2++] = cc; g[k2++] = ca; g[k2++] = bc;
                    g[k2++] = ab; g[k2++] = bc; g[k2++] = ca;
                }
                Array.Copy(g, f, 240);
                nf = 80;
            }
            Vec3[] pos = sc.Pos, nrm = sc.Nrm;
            for (int i = 0; i < nv; i++)
            {
                Vec3 d = new Vec3(p[3 * i], p[3 * i + 1], p[3 * i + 2]).Normalized;
                float k = 1f + rough * FloraNoise.Value(d.X * 1.6 + 5.1, d.Y * 1.6 + 2.3, d.Z * 1.6 + 9.7, seed)
                          + 0.45f * rough * FloraNoise.Value(d.X * 3.7, d.Y * 3.7, d.Z * 3.7, seed ^ 0x77u);
                float y = d.Y * k;
                if (y < -0.3f) y = -0.3f + (y + 0.3f) * 0.2f; // flattened, sunk base
                if (y > 0.7f) y = 0.7f + (y - 0.7f) * 0.75f; // weathered, slightly flatter top
                pos[i] = new Vec3(c.X + d.X * k * size.X, c.Y + y * size.Y, c.Z + d.Z * k * size.Z);
                nrm[i] = Vec3.Zero;
            }
            for (int t = 0; t < nf; t++)
            {
                Vec3 a = pos[f[3 * t]], b = pos[f[3 * t + 1]], cc = pos[f[3 * t + 2]];
                Vec3 fn = Vec3.Cross(b - a, cc - a);
                if (Vec3.Dot(fn, (a + b + cc) * (1f / 3f) - c) < 0) fn = -fn;
                for (int q = 0; q < 3; q++) nrm[f[3 * t + q]] = nrm[f[3 * t + q]] + fn;
            }
            int[] idx = sc.Idx;
            for (int i = 0; i < nv; i++)
            {
                Vec3 n = nrm[i].Normalized;
                float h01 = Math.Max(0f, Math.Min(1f, (pos[i].Y - (c.Y - 0.3f * size.Y)) / (0.9f * size.Y)));
                float ao = 0.4f + 0.6f * h01;
                float crev = FloraNoise.Value(pos[i].X * 2.3, pos[i].Y * 2.3, pos[i].Z * 2.3, seed ^ 0x33u);
                ao *= 0.86f + 0.14f * crev;
                float m = moss * Math.Max(0f, n.Y) * (0.55f + 0.45f * FloraNoise.Value(pos[i].X * 1.1, pos[i].Z * 1.1, seed ^ 0x99u));
                uint col = Mix(rgb, mossRgb, Math.Max(0f, Math.Min(1f, m * 1.6f)));
                idx[i] = Vertex(pos[i], n, Fixed(col, 0.92f + 0.12f * crev), MaterialChannel.Stone, ao);
            }
            for (int t = 0; t < nf; t++) Tri(idx[f[3 * t]], idx[f[3 * t + 1]], idx[f[3 * t + 2]]);
            return M.TriangleCount - t0;
        }

        private static int Mid(int a, int b, float[] p, ref int nv, RockScratch sc, ref int nk)
        {
            int lo = Math.Min(a, b), hi = Math.Max(a, b);
            for (int i = 0; i < nk; i++)
                if (sc.KeyA[i] == lo && sc.KeyB[i] == hi) return sc.KeyV[i];
            int v = nv++;
            p[3 * v] = 0.5f * (p[3 * a] + p[3 * b]);
            p[3 * v + 1] = 0.5f * (p[3 * a + 1] + p[3 * b + 1]);
            p[3 * v + 2] = 0.5f * (p[3 * a + 2] + p[3 * b + 2]);
            sc.KeyA[nk] = lo;
            sc.KeyB[nk] = hi;
            sc.KeyV[nk++] = v;
            return v;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Normalisation

        /// <summary>
        /// Rescale the vertices from <paramref name="firstVertex"/> on to a unit model: x and z divided by
        /// <paramref name="width"/>, y by <paramref name="height"/>, normals by the inverse transpose. The instance
        /// matrix scales by (crown, height, crown) again, and the wind shader's y² sway weight then runs from 0 at the
        /// ground to 1 at the top of every plant.
        /// </summary>
        /// <summary>
        /// Taper the vertices from <paramref name="firstVertex"/> toward a vertical axis through
        /// (<paramref name="axisX"/>, <paramref name="axisZ"/>): the horizontal offset is scaled by
        /// 1 − <paramref name="amount"/> · t^1.2 with t running 0..1 from <paramref name="yLow"/> to
        /// <paramref name="yHigh"/> (a rounded mass becomes a cone or a column). Normals follow exactly (the inverse
        /// transpose of the taper), so the toon shading stays smooth.
        /// </summary>
        public void Taper(int firstVertex, float axisX, float axisZ, float yLow, float yHigh, float amount)
        {
            float[] p = M.Positions, n = M.Normals;
            double span = Math.Max(1e-4, yHigh - yLow);
            for (int v = firstVertex; v < M.VertexCount; v++)
            {
                double x = p[3 * v] - axisX, y = p[3 * v + 1], z = p[3 * v + 2] - axisZ;
                double t = (y - yLow) / span, sc, ds = 0;
                if (t <= 0) sc = 1;
                else if (t >= 1) sc = 1 - amount;
                else
                {
                    double tp = Math.Pow(t, 1.2);
                    sc = 1 - amount * tp;
                    ds = -amount * 1.2 * tp / t / span;
                }
                p[3 * v] = (float)(axisX + x * sc);
                p[3 * v + 2] = (float)(axisZ + z * sc);
                double nx = n[3 * v], ny = n[3 * v + 1], nz = n[3 * v + 2];
                double mx = nx, my = sc * ny - ds * (x * nx + z * nz), mz = nz;
                double l = Math.Sqrt(mx * mx + my * my + mz * mz);
                if (l < 1e-12) continue;
                n[3 * v] = (float)(mx / l);
                n[3 * v + 1] = (float)(my / l);
                n[3 * v + 2] = (float)(mz / l);
            }
        }

        public void Normalize(int firstVertex, float width, float height)
        {
            float sx = 1f / width, sy = 1f / height;
            float[] p = M.Positions, n = M.Normals;
            for (int v = firstVertex; v < M.VertexCount; v++)
            {
                p[3 * v] *= sx;
                p[3 * v + 1] *= sy;
                p[3 * v + 2] *= sx;
                double nx = n[3 * v] * width, ny = n[3 * v + 1] * height, nz = n[3 * v + 2] * width;
                double l = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (l < 1e-12) continue;
                n[3 * v] = (float)(nx / l);
                n[3 * v + 1] = (float)(ny / l);
                n[3 * v + 2] = (float)(nz / l);
            }
        }
    }
}
