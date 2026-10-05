using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Characters
{
    /// <summary>How a loft closes its first or last ring.</summary>
    public enum LoftCap : byte
    {
        /// <summary>Open (hidden inside a neighbouring part).</summary>
        None = 0,

        /// <summary>A flat fan to the ring centre.</summary>
        Flat = 1,

        /// <summary>A rounded dome (two rings and a pole), like the end of a capsule.</summary>
        Round = 2,
    }

    /// <summary>One cross-section of a loft: a superellipse in the plane spanned by <see cref="AxisX"/> and
    /// <see cref="AxisZ"/>, with its colour and skin weights.</summary>
    public struct LoftRing
    {
        public V3 C, AxisX, AxisZ;
        public float Rx, Rz;

        /// <summary>Superellipse exponent (2 = ellipse, higher = boxier).</summary>
        public float Exp;

        /// <summary>0xRRGGBB.</summary>
        public uint Rgb;

        public Bone B0, B1;
        public float W0;

        /// <summary>Hem rings: vertices in front weight to skirt_F, behind to skirt_B (instead of B1).</summary>
        public bool Skirt;

        /// <summary>Radial ridges (daura pleats): this many evenly spaced bumps on the front half, 0 for none.</summary>
        public byte Pleats;
    }

    /// <summary>
    /// The parametric primitives the character is made of (P §2.4): lofted superellipse rings (torso, limbs as tapered
    /// capsules with joint bulges, skirts, hats), superellipsoids (head, hands, shoes, packs) and flat quads, written into
    /// a <see cref="MeshData"/> with matching <see cref="SkinWeights"/> (or none, for static NPC bodies). Triangles are
    /// wound Unity's way (the cross product of the edges points along the vertex normals). Segment counts drop with the
    /// level of detail (<see cref="Seg"/>). Engine-free; scratch buffers are reused, so building allocates only when
    /// the mesh grows.
    /// </summary>
    public sealed class BodyKit
    {
        private const float TwoPi = 6.28318530718f;

        public MeshData M;
        public SkinWeights W;
        public int Lod;

        private LoftRing[] _rings = new LoftRing[48];
        private int _ringCount;
        private V3[] _pos = new V3[1024];
        private V3[] _nrm = new V3[1024];

        public BodyKit(MeshData m, SkinWeights w, int lod)
        {
            M = m ?? throw new ArgumentNullException(nameof(m));
            W = w;
            Lod = lod < 0 ? 0 : lod > 2 ? 2 : lod;
        }

        /// <summary>Segments for a part that uses <paramref name="lod0"/> at LOD0: half at LOD1, a quarter (at least
        /// <paramref name="min"/>) at LOD2.</summary>
        public int Seg(int lod0, int min = 3)
        {
            int n = Lod == 0 ? lod0 : Lod == 1 ? (lod0 + 1) / 2 : (lod0 + 3) / 4;
            return n < min ? min : n;
        }

        // ----- Raw vertices and triangles ---------------------------------------------------------------------------

        public int Vertex(V3 p, V3 n, uint rgb, Bone b0, Bone b1, float w0)
        {
            V3 nn = n.Normalized;
            if (nn.LengthSq < 0.5f) nn = V3.Up;
            int i = M.AddVertex(p.X, p.Y, p.Z, nn.X, nn.Y, nn.Z, CharacterPalette.Rgba(rgb));
            if (W != null) W.Add(b0, b1, w0);
            return i;
        }

        /// <summary>A triangle, flipped if needed so its face normal agrees with the vertex normals.</summary>
        public void Tri(int a, int b, int c)
        {
            if (a == b || b == c || a == c) return;
            float[] p = M.Positions, n = M.Normals;
            int pa = a * 3, pb = b * 3, pc = c * 3;
            float ux = p[pb] - p[pa], uy = p[pb + 1] - p[pa + 1], uz = p[pb + 2] - p[pa + 2];
            float vx = p[pc] - p[pa], vy = p[pc + 1] - p[pa + 1], vz = p[pc + 2] - p[pa + 2];
            float fx = uy * vz - uz * vy, fy = uz * vx - ux * vz, fz = ux * vy - uy * vx;
            float sx = n[pa] + n[pb] + n[pc], sy = n[pa + 1] + n[pb + 1] + n[pc + 1], sz = n[pa + 2] + n[pb + 2] + n[pc + 2];
            if (fx * sx + fy * sy + fz * sz < 0f) M.AddTriangle(a, c, b);
            else M.AddTriangle(a, b, c);
        }

        /// <summary>A flat quad (corners in order around it) facing <paramref name="normal"/>.</summary>
        public void Quad(V3 a, V3 b, V3 c, V3 d, V3 normal, uint rgb, Bone bone)
        {
            int ia = Vertex(a, normal, rgb, bone, bone, 1f), ib = Vertex(b, normal, rgb, bone, bone, 1f);
            int ic = Vertex(c, normal, rgb, bone, bone, 1f), id = Vertex(d, normal, rgb, bone, bone, 1f);
            Tri(ia, ib, ic);
            Tri(ia, ic, id);
        }

        // ----- Lofts --------------------------------------------------------------------------------------------------

        public void BeginLoft()
        {
            _ringCount = 0;
        }

        public void Ring(in LoftRing r)
        {
            if (_ringCount == _rings.Length) Array.Resize(ref _rings, _rings.Length * 2);
            _rings[_ringCount++] = r;
        }

        /// <summary>A ring centred at <paramref name="c"/> perpendicular to <paramref name="dir"/>, its X axis as close to
        /// <paramref name="side"/> as possible.</summary>
        public void Ring(V3 c, V3 dir, V3 side, float rx, float rz, uint rgb, Bone b0, Bone b1, float w0, float exp = 2f)
        {
            V3 d = dir.Normalized;
            V3 x = (side - d * V3.Dot(side, d)).Normalized;
            if (x.LengthSq < 0.5f) x = Perpendicular(d);
            V3 z = V3.Cross(x, d);
            if (V3.Dot(z, V3.Cross(side, d)) < 0f) z = -z;
            Ring(new LoftRing { C = c, AxisX = x, AxisZ = z, Rx = rx, Rz = rz, Exp = exp, Rgb = rgb, B0 = b0, B1 = b1, W0 = w0 });
        }

        /// <summary>A horizontal ring (X right, Z forward) at <paramref name="c"/>.</summary>
        public void FlatRing(V3 c, float rx, float rz, uint rgb, Bone b0, Bone b1, float w0, float exp = 2f, bool skirt = false, byte pleats = 0)
        {
            Ring(new LoftRing
            {
                C = c, AxisX = V3.Right, AxisZ = V3.Forward, Rx = rx, Rz = rz, Exp = exp, Rgb = rgb, B0 = b0, B1 = b1, W0 = w0,
                Skirt = skirt, Pleats = pleats,
            });
        }

        /// <summary>Builds the rings given since <see cref="BeginLoft"/> with <paramref name="seg"/> vertices per ring.
        /// Returns the triangles added.</summary>
        public int EndLoft(int seg, LoftCap start, LoftCap end)
        {
            int before = M.IndexCount;
            if (_ringCount < 1 || seg < 3) return 0;
            if (start == LoftCap.Round) AddDome(0, seg, true);
            if (end == LoftCap.Round) AddDome(_ringCount - 1, seg, false);
            int n = _ringCount;
            int count = n * seg;
            if (_pos.Length < count)
            {
                _pos = new V3[count * 2];
                _nrm = new V3[count * 2];
            }
            for (int i = 0; i < n; i++)
            {
                LoftRing r = _rings[i];
                for (int j = 0; j < seg; j++) _pos[i * seg + j] = RingPoint(r, j, seg);
            }
            for (int i = 0; i < n; i++)
            {
                int ip = i > 0 ? i - 1 : i, inx = i < n - 1 ? i + 1 : i;
                for (int j = 0; j < seg; j++)
                {
                    V3 around = _pos[i * seg + (j + 1) % seg] - _pos[i * seg + (j + seg - 1) % seg];
                    V3 along = _pos[inx * seg + j] - _pos[ip * seg + j];
                    V3 nrm = V3.Cross(around, along);
                    V3 radial = _pos[i * seg + j] - _rings[i].C;
                    if (_rings[i].Rx <= 1e-5f && _rings[i].Rz <= 1e-5f)
                    {
                        // A pole: it points away from the neighbouring ring.
                        int nb = i > 0 ? i - 1 : Math.Min(n - 1, i + 1);
                        radial = _rings[i].C - _rings[nb].C;
                        nrm = radial;
                    }
                    if (nrm.LengthSq < 1e-12f) nrm = radial;
                    if (V3.Dot(nrm, radial) < 0f) nrm = -nrm;
                    _nrm[i * seg + j] = nrm;
                }
            }
            int baseV = M.VertexCount;
            for (int i = 0; i < n; i++)
            {
                LoftRing r = _rings[i];
                for (int j = 0; j < seg; j++)
                {
                    V3 p = _pos[i * seg + j];
                    Bone b1 = r.B1;
                    if (r.Skirt) b1 = V3.Dot(p - r.C, V3.Forward) >= 0f ? Bone.SkirtF : Bone.SkirtB;
                    Vertex(p, _nrm[i * seg + j], r.Rgb, r.B0, b1, r.W0);
                }
            }
            for (int i = 0; i + 1 < n; i++)
            {
                for (int j = 0; j < seg; j++)
                {
                    int a = baseV + i * seg + j, b = baseV + i * seg + (j + 1) % seg;
                    int c = a + seg, d = b + seg;
                    if (_rings[i].Rx <= 1e-5f && _rings[i].Rz <= 1e-5f)
                    {
                        Tri(a, d, c);
                        continue;
                    }
                    if (_rings[i + 1].Rx <= 1e-5f && _rings[i + 1].Rz <= 1e-5f)
                    {
                        Tri(a, b, c);
                        continue;
                    }
                    Tri(a, b, d);
                    Tri(a, d, c);
                }
            }
            if (start == LoftCap.Flat) Fan(baseV, seg, _rings[0], -1f);
            if (end == LoftCap.Flat) Fan(baseV + (n - 1) * seg, seg, _rings[n - 1], 1f);
            _ringCount = 0;
            return (M.IndexCount - before) / 3;
        }

        private void Fan(int first, int seg, in LoftRing r, float sign)
        {
            V3 axis = V3.Cross(r.AxisZ, r.AxisX).Normalized * sign;
            int c = Vertex(r.C, axis, r.Rgb, r.B0, r.B1, r.W0);
            for (int j = 0; j < seg; j++)
            {
                // Separate rim vertices so the cap is flat-shaded.
                float[] p = M.Positions;
                int pa = (first + j) * 3, pb = (first + (j + 1) % seg) * 3;
                int va = Vertex(new V3(p[pa], p[pa + 1], p[pa + 2]), axis, r.Rgb, r.B0, r.B1, r.W0);
                p = M.Positions;
                int vb = Vertex(new V3(p[pb], p[pb + 1], p[pb + 2]), axis, r.Rgb, r.B0, r.B1, r.W0);
                Tri(c, va, vb);
            }
        }

        /// <summary>Inserts two shrinking rings and a pole before the first ring (or after the last) so the loft ends
        /// in a dome.</summary>
        private void AddDome(int index, int seg, bool atStart)
        {
            LoftRing r = _rings[index];
            V3 axis = V3.Cross(r.AxisZ, r.AxisX).Normalized;
            int other = atStart ? Math.Min(_ringCount - 1, index + 1) : Math.Max(0, index - 1);
            V3 toward = _rings[other].C - r.C;
            float outward = V3.Dot(axis, toward) > 0f ? -1f : 1f;
            if (other == index) outward = atStart ? -1f : 1f;
            float rad = 0.5f * (r.Rx + r.Rz);
            int segDome = Lod >= 2 ? 1 : 2;
            var extra = new LoftRing[segDome + 1];
            for (int k = 1; k <= segDome; k++)
            {
                float a = k / (float)(segDome + 1) * 1.5707963f;
                LoftRing q = r;
                q.C = r.C + axis * (outward * MathF.Sin(a) * rad * 0.9f);
                q.Rx = r.Rx * MathF.Cos(a);
                q.Rz = r.Rz * MathF.Cos(a);
                extra[k - 1] = q;
            }
            LoftRing pole = r;
            pole.C = r.C + axis * (outward * rad * 0.9f);
            pole.Rx = 0f;
            pole.Rz = 0f;
            extra[segDome] = pole;
            int add = extra.Length;
            if (_ringCount + add > _rings.Length) Array.Resize(ref _rings, Math.Max(_rings.Length * 2, _ringCount + add));
            if (atStart)
            {
                Array.Copy(_rings, 0, _rings, add, _ringCount);
                for (int k = 0; k < add; k++) _rings[k] = extra[add - 1 - k];
            }
            else
            {
                for (int k = 0; k < add; k++) _rings[_ringCount + k] = extra[k];
            }
            _ringCount += add;
        }

        private static V3 RingPoint(in LoftRing r, int j, int seg)
        {
            float a = TwoPi * j / seg;
            float c = MathF.Cos(a), s = MathF.Sin(a);
            float e = r.Exp > 0.5f ? r.Exp : 2f;
            float x = r.Rx * SPow(c, 2f / e), z = r.Rz * SPow(s, 2f / e);
            if (r.Pleats > 0 && s > 0.05f)
            {
                // Pleats on the front half: soft ridges.
                float ridge = 0.5f + 0.5f * MathF.Cos(a * r.Pleats * 2f);
                float k = 1f + 0.06f * ridge * s;
                x *= k;
                z *= k;
            }
            return r.C + r.AxisX * x + r.AxisZ * z;
        }

        /// <summary>Signed power: sign(v)·|v|^p.</summary>
        public static float SPow(float v, float p)
        {
            float a = MathF.Abs(v);
            if (a < 1e-9f) return 0f;
            float r = MathF.Pow(a, p);
            return v < 0f ? -r : r;
        }

        private static V3 Perpendicular(V3 d)
        {
            V3 x = V3.Cross(V3.Up, d);
            if (x.LengthSq < 1e-6f) x = V3.Cross(V3.Forward, d);
            return x.Normalized;
        }

        // ----- Capsules and ellipsoids ------------------------------------------------------------------------------

        /// <summary>
        /// A tapered capsule from <paramref name="a"/> to <paramref name="b"/> (radii <paramref name="ra"/> to
        /// <paramref name="rb"/>), weighted to <paramref name="bone"/> and blended into <paramref name="parent"/> over the
        /// first quarter, with a 1.15× joint bulge at the far end when <paramref name="bulge"/> (elbows, knees).
        /// </summary>
        public int Capsule(V3 a, V3 b, float ra, float rb, uint rgb, Bone bone, Bone parent, int seg, int rings, bool bulge,
                           LoftCap startCap = LoftCap.Round, LoftCap endCap = LoftCap.Round, V3? side = null)
        {
            V3 dir = b - a;
            V3 s = side ?? V3.Right;
            BeginLoft();
            int n = Math.Max(2, rings);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                float r = ra + (rb - ra) * t;
                if (bulge && t > 0.8f) r *= 1f + 0.15f * MathF.Sin((t - 0.8f) / 0.2f * 1.5707963f);
                float w = t < 0.25f ? 0.5f + 2f * t : 1f;
                Ring(V3.Lerp(a, b, t), dir, s, r, r, rgb, bone, parent, w);
            }
            return EndLoft(seg, startCap, endCap);
        }

        /// <summary>
        /// A superellipsoid at <paramref name="c"/> with radii <paramref name="r"/> (x, y, z before rotation) and shape
        /// exponent <paramref name="exp"/> (2 = ellipsoid, 2.4 = the head's slightly boxy round, 4+ = a rounded box),
        /// rotated by <paramref name="rot"/>; <paramref name="u"/> segments around, <paramref name="v"/> from pole to pole.
        /// </summary>
        public int Ellipsoid(V3 c, V3 r, Quat rot, uint rgb, int u, int v, Bone bone, float exp = 2f, Bone bone1 = Bone.Root, float w0 = 1f)
        {
            int before = M.IndexCount;
            if (u < 3) u = 3;
            if (v < 2) v = 2;
            Bone b1 = bone1 == Bone.Root ? bone : bone1;
            float e = exp > 0.5f ? exp : 2f;
            float pe = 2f / e;
            int top = Vertex(c + rot * new V3(0f, r.Y, 0f), rot * V3.Up, rgb, bone, b1, w0);
            int first = M.VertexCount;
            for (int i = 1; i < v; i++)
            {
                float phi = MathF.PI * i / v; // from the top pole down
                float sp = MathF.Sin(phi), cp = MathF.Cos(phi);
                for (int j = 0; j < u; j++)
                {
                    float th = TwoPi * j / u;
                    float ct = MathF.Cos(th), st = MathF.Sin(th);
                    float x = r.X * SPow(sp, pe) * SPow(ct, pe);
                    float y = r.Y * SPow(cp, pe);
                    float z = r.Z * SPow(sp, pe) * SPow(st, pe);
                    // Normal of the implicit surface |x/a|^e + |y/b|^e + |z/c|^e = 1.
                    var nrm = new V3(SPow(x / r.X, e - 1f) / r.X, SPow(y / r.Y, e - 1f) / r.Y, SPow(z / r.Z, e - 1f) / r.Z);
                    Vertex(c + rot * new V3(x, y, z), rot * nrm, rgb, bone, b1, w0);
                }
            }
            int bottom = Vertex(c + rot * new V3(0f, -r.Y, 0f), rot * V3.Down, rgb, bone, b1, w0);
            for (int j = 0; j < u; j++)
            {
                int j1 = (j + 1) % u;
                Tri(top, first + j1, first + j);
                for (int i = 0; i + 2 < v; i++)
                {
                    int a = first + i * u + j, b = first + i * u + j1, cc = a + u, d = b + u;
                    Tri(a, b, d);
                    Tri(a, d, cc);
                }
                int last = first + (v - 2) * u;
                Tri(bottom, last + j, last + j1);
            }
            return (M.IndexCount - before) / 3;
        }

        /// <summary>A sphere (an <see cref="Ellipsoid"/> with equal radii).</summary>
        public int Sphere(V3 c, float radius, uint rgb, int u, int v, Bone bone)
        {
            return Ellipsoid(c, new V3(radius, radius, radius), Quat.Identity, rgb, u, v, bone);
        }

        /// <summary>A cone or frustum from a base ring at <paramref name="a"/> to a tip at <paramref name="b"/> (spikes,
        /// tassels).</summary>
        public int Cone(V3 a, V3 b, float baseR, float tipR, uint rgb, int seg, Bone bone)
        {
            BeginLoft();
            Ring(a, b - a, V3.Right, baseR, baseR, rgb, bone, bone, 1f);
            Ring(b, b - a, V3.Right, tipR, tipR, rgb, bone, bone, 1f);
            return EndLoft(seg, LoftCap.Flat, tipR > 1e-4f ? LoftCap.Flat : LoftCap.None);
        }
    }
}
