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
    /// capsules with joint bulges, skirts, hats, sweeps along curves for brows, lips, straps and locks), superellipsoids
    /// (head, hands, shoes, packs), patches conforming to an ellipsoid (irises, eyelids, highlights), free grids with
    /// neighbour normals (the sculpted head, the topi's woven panels) and flat quads, written into a
    /// <see cref="MeshData"/> with matching <see cref="SkinWeights"/> (or none, for static NPC bodies). Every vertex
    /// carries UV0 = (<see cref="Channel"/>, <see cref="Ao"/>) for the toon shader's procedural material channels
    /// (docs/W2_DETAIL_CONTRACT.md §5); <see cref="CharacterAo"/> later multiplies in the body's own occlusion.
    /// Triangles are wound Unity's way (the cross product of the edges points along the vertex normals). Segment counts
    /// drop with the level of detail (<see cref="Seg"/>). Engine-free; scratch buffers are reused, so building allocates
    /// only when the mesh grows.
    /// </summary>
    public sealed class BodyKit
    {
        private const float TwoPi = 6.28318530718f;

        public MeshData M;
        public SkinWeights W;

        /// <summary>Kit level of detail, 0..2. The far crowd's level (<see cref="HumanoidMesher.FarLod"/>) builds at kit
        /// level 2 with <see cref="Far"/> set.</summary>
        public int Lod;

        /// <summary>The far crowd body (mesher level 3): level 2 with the fewest segments. Without it level 2 is the
        /// mid-distance body, a little rounder (<see cref="Seg"/>).</summary>
        public bool Far;

        /// <summary>Material channel written into UV0.x of the vertices that follow.</summary>
        public MaterialChannel Channel = MaterialChannel.Fabric;

        /// <summary>Ambient occlusion written into UV0.y of the vertices that follow (1 open, 0 occluded).</summary>
        public float Ao = 1f;

        /// <summary>
        /// Tint mask for the crowd's looks (one garment colour per person, <see cref="CrowdVariants"/>): when set,
        /// garment vertices (<see cref="TintScope"/>) whose colour is <see cref="TintKey"/> or a shade of it are written
        /// as grey with alpha 255 (the far shader multiplies them by the instance tint, the near bodies are recoloured
        /// the same way on the CPU), every other vertex keeps its colour with alpha 0 (untinted). Off: every vertex is
        /// opaque with alpha 255.
        /// </summary>
        public bool TintMask;

        /// <summary>The 0xRRGGBB colour replaced by the instance tint when <see cref="TintMask"/> is on (0: nothing).</summary>
        public uint TintKey;

        /// <summary>Under <see cref="TintMask"/>, only vertices written while this is set (the builder sets it for the
        /// garment: torso, sleeves and a matching lower garment) and in the <see cref="MaterialChannel.Fabric"/> channel
        /// are masked, so eyes, teeth, skin, laces and soles never take a garment colour.</summary>
        public bool TintScope;

        /// <summary>A flag in the top byte of a colour handed to the kit: the vertex is never tint-masked (the trouser
        /// colour of the hips inside the torso loft when the trousers are another cloth). Stripped before writing.</summary>
        public const uint NoTint = 0x01000000u;

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

        /// <summary>Segments for a part that uses <paramref name="lod0"/> at LOD0: half at LOD1, a third at the mid-distance
        /// LOD2 and a quarter on the far body (at least <paramref name="min"/>).</summary>
        public int Seg(int lod0, int min = 3)
        {
            int n = Lod == 0 ? lod0 : Lod == 1 ? (lod0 + 1) / 2 : Far ? (lod0 + 3) / 4 : (lod0 + 2) / 3;
            return n < min ? min : n;
        }

        // ----- Raw vertices and triangles ---------------------------------------------------------------------------

        public int Vertex(V3 p, V3 n, uint rgb, Bone b0, Bone b1, float w0)
        {
            V3 nn = n.Normalized;
            if (nn.LengthSq < 0.5f) nn = V3.Up;
            bool keep = (rgb & NoTint) != 0;
            rgb &= 0xFFFFFFu;
            uint rgba = !TintMask ? CharacterPalette.Rgba(rgb)
                        : !keep && TintScope && TintKey != 0 && Channel == MaterialChannel.Fabric ? Masked(rgb) : rgb << 8;
            int i = M.AddVertex(p.X, p.Y, p.Z, nn.X, nn.Y, nn.Z, rgba, (float)Channel, Ao);
            if (W != null) W.Add(b0, b1, w0);
            return i;
        }

        /// <summary>The RGBA of <paramref name="rgb"/> under the tint mask: a shade of <see cref="TintKey"/> becomes grey
        /// (its shade factor) with alpha 255, anything else keeps its colour with alpha 0.</summary>
        private uint Masked(uint rgb)
        {
            float f = ShadeOf(rgb, TintKey);
            if (f < 0f) return rgb << 8;
            int g = (int)(Math.Min(1f, f) * 255f + 0.5f);
            return (uint)(g << 24 | g << 16 | g << 8 | 0xFF);
        }

        /// <summary>The factor k when <paramref name="rgb"/> ≈ k · <paramref name="key"/> (0.35 ≤ k ≤ 1.3), else −1.</summary>
        public static float ShadeOf(uint rgb, uint key)
        {
            float kr = (key >> 16) & 0xFF, kg = (key >> 8) & 0xFF, kb = key & 0xFF;
            float r = (rgb >> 16) & 0xFF, g = (rgb >> 8) & 0xFF, b = rgb & 0xFF;
            if (rgb == key) return 1f;
            float sum = kr + kg + kb;
            if (sum < 30f) return -1f;
            float k = (r + g + b) / sum;
            if (k < 0.35f || k > 1.3f) return -1f;
            float tol = 10f + 0.06f * Math.Max(kr, Math.Max(kg, kb)) * k;
            if (Math.Abs(r - kr * k) > tol || Math.Abs(g - kg * k) > tol || Math.Abs(b - kb * k) > tol) return -1f;
            return k;
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

        // ----- Sweeps -------------------------------------------------------------------------------------------------

        /// <summary>
        /// A tube along <paramref name="n"/> points of <paramref name="path"/>: each ring is perpendicular to the path, its
        /// X axis toward <paramref name="side"/>, with radii <paramref name="rx"/>[i] across and <paramref name="rz"/>[i]
        /// (e.g. flat brows: thin toward the face normal, wide across). Ends get the given caps. Every ring weighs on
        /// <paramref name="bone"/> blended into <paramref name="bone1"/> by <paramref name="w0"/>.
        /// </summary>
        public int Sweep(V3[] path, int n, float[] rx, float[] rz, V3 side, uint rgb, int seg, Bone bone, LoftCap start = LoftCap.Round,
                         LoftCap end = LoftCap.Round, Bone bone1 = Bone.Root, float w0 = 1f, float exp = 2f)
        {
            if (n < 2) return 0;
            Bone b1 = bone1 == Bone.Root ? bone : bone1;
            BeginLoft();
            for (int i = 0; i < n; i++)
            {
                V3 d = i == 0 ? path[1] - path[0] : i == n - 1 ? path[n - 1] - path[n - 2] : path[i + 1] - path[i - 1];
                Ring(path[i], d, side, rx[i], rz[i], rgb, bone, b1, w0, exp);
            }
            return EndLoft(seg, start, end);
        }

        /// <summary>A sweep along a quadratic curve (<paramref name="a"/>, control <paramref name="c"/>,
        /// <paramref name="b"/>) with radii tapering linearly from the start to the end values.</summary>
        public int Curve(V3 a, V3 c, V3 b, int steps, float rxA, float rxB, float rzA, float rzB, V3 side, uint rgb, int seg, Bone bone,
                         LoftCap start = LoftCap.Round, LoftCap end = LoftCap.Round, Bone bone1 = Bone.Root, float w0 = 1f)
        {
            if (steps < 1) steps = 1;
            int n = steps + 1;
            EnsurePath(n);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)steps;
                float u = 1f - t;
                _path[i] = a * (u * u) + c * (2f * u * t) + b * (t * t);
                _rx[i] = rxA + (rxB - rxA) * t;
                _rz[i] = rzA + (rzB - rzA) * t;
            }
            return Sweep(_path, n, _rx, _rz, side, rgb, seg, bone, start, end, bone1, w0);
        }

        private V3[] _path = new V3[16];
        private float[] _rx = new float[16], _rz = new float[16];

        /// <summary>Scratch path buffers for <see cref="Sweep"/> callers (valid until the next call).</summary>
        public V3[] PathBuffer(int n, out float[] rx, out float[] rz)
        {
            EnsurePath(n);
            rx = _rx;
            rz = _rz;
            return _path;
        }

        private void EnsurePath(int n)
        {
            if (_path.Length >= n) return;
            _path = new V3[n * 2];
            _rx = new float[n * 2];
            _rz = new float[n * 2];
        }

        // ----- Ellipsoid patches --------------------------------------------------------------------------------------

        /// <summary>
        /// A patch lying on the front (+Z) of an ellipsoid (centre <paramref name="c"/>, radii <paramref name="r"/>,
        /// rotation <paramref name="rot"/>) lifted <paramref name="lift"/> along its normal: the region inside the ellipse
        /// centred at (<paramref name="cx"/>, <paramref name="cy"/>) with half-axes (<paramref name="ax"/>,
        /// <paramref name="ay"/>), all in units of the radii. Concentric colour rings: ring k covers
        /// (<paramref name="ringEdge"/>[k−1], <paramref name="ringEdge"/>[k]] of the patch radius in
        /// <paramref name="ringRgb"/>[k] with crisp edges (irises with a pupil and a limbal ring, highlights).
        /// </summary>
        public int EllipsoidPatch(V3 c, V3 r, Quat rot, float cx, float cy, float ax, float ay, float lift, uint[] ringRgb, float[] ringEdge,
                                  int rings, int seg, Bone bone)
        {
            int before = M.IndexCount;
            if (seg < 3) seg = 3;
            int bands = ringRgb != null ? ringRgb.Length : 1;
            int centre = Vertex(PatchPoint(c, r, rot, cx, cy, lift, out V3 n0), n0, ringRgb[0], bone, bone, 1f);
            int prevStart = -1;
            float prevEdge = 0f;
            for (int k = 0; k < bands; k++)
            {
                float edge = ringEdge != null && k < ringEdge.Length ? ringEdge[k] : 1f;
                int steps = k == 0 ? Math.Max(1, rings) : Math.Max(1, rings / 2);
                // Inner boundary of this band (duplicated with the band's colour), then its rings out to the edge.
                int innerStart = -1;
                if (k > 0)
                {
                    innerStart = M.VertexCount;
                    for (int j = 0; j < seg; j++) RingVertex(c, r, rot, cx, cy, ax * prevEdge, ay * prevEdge, j, seg, lift, ringRgb[k], bone);
                }
                int last = innerStart;
                for (int s = 1; s <= steps; s++)
                {
                    float f = prevEdge + (edge - prevEdge) * s / steps;
                    int start = M.VertexCount;
                    for (int j = 0; j < seg; j++) RingVertex(c, r, rot, cx, cy, ax * f, ay * f, j, seg, lift, ringRgb[k], bone);
                    for (int j = 0; j < seg; j++)
                    {
                        int j1 = (j + 1) % seg;
                        if (last < 0) Tri(centre, start + j, start + j1);
                        else
                        {
                            Tri(last + j, start + j, start + j1);
                            Tri(last + j, start + j1, last + j1);
                        }
                    }
                    last = start;
                }
                prevStart = last;
                prevEdge = edge;
            }
            return (M.IndexCount - before) / 3;
        }

        private void RingVertex(V3 c, V3 r, Quat rot, float cx, float cy, float ax, float ay, int j, int seg, float lift, uint rgb, Bone bone)
        {
            float a = TwoPi * j / seg;
            V3 p = PatchPoint(c, r, rot, cx + ax * MathF.Cos(a), cy + ay * MathF.Sin(a), lift, out V3 n);
            Vertex(p, n, rgb, bone, bone, 1f);
        }

        /// <summary>The point of an ellipsoid's front at normalised (u, v) (in units of the X and Y radii), lifted along
        /// the normal; points beyond the silhouette are clamped to it.</summary>
        public static V3 PatchPoint(V3 c, V3 r, Quat rot, float u, float v, float lift, out V3 normal)
        {
            float q = u * u + v * v;
            if (q > 0.999f)
            {
                float k = MathF.Sqrt(0.999f / q);
                u *= k;
                v *= k;
                q = 0.999f;
            }
            float z = MathF.Sqrt(1f - q);
            var local = new V3(u * r.X, v * r.Y, z * r.Z);
            var nl = new V3(local.X / (r.X * r.X), local.Y / (r.Y * r.Y), local.Z / (r.Z * r.Z)).Normalized;
            normal = rot * nl;
            return c + rot * local + normal * lift;
        }

        // ----- Free grids ---------------------------------------------------------------------------------------------

        private V3[] _gp = new V3[256];
        private uint[] _gc = new uint[256];
        private Bone[] _gb0 = new Bone[256], _gb1 = new Bone[256];
        private float[] _gw = new float[256], _gao = new float[256];

        /// <summary>Starts a grid of <paramref name="rows"/> × <paramref name="cols"/> points (fill with
        /// <see cref="GridSet"/>, build with <see cref="EndGrid"/>).</summary>
        public void BeginGrid(int rows, int cols)
        {
            int n = rows * cols;
            if (_gp.Length < n)
            {
                int cap = n * 2;
                _gp = new V3[cap];
                _gc = new uint[cap];
                _gb0 = new Bone[cap];
                _gb1 = new Bone[cap];
                _gw = new float[cap];
                _gao = new float[cap];
            }
        }

        public void GridSet(int row, int col, int cols, V3 p, uint rgb, Bone b0, Bone b1, float w0, float ao = 1f)
        {
            int i = row * cols + col;
            _gp[i] = p;
            _gc[i] = rgb;
            _gb0[i] = b0;
            _gb1[i] = b1;
            _gw[i] = w0;
            _gao[i] = ao;
        }

        /// <summary>The grid point set at (<paramref name="row"/>, <paramref name="col"/>).</summary>
        public V3 GridPoint(int row, int col, int cols)
        {
            return _gp[row * cols + col];
        }

        /// <summary>
        /// Builds the grid: rows along v, columns along u (closed around when <paramref name="closedU"/>). Normals come
        /// from the neighbours and point away from <paramref name="inside"/> (or from the nearest point of the axis
        /// through it along <paramref name="axis"/> when that is not zero). With <paramref name="quadRgb"/> each quad gets
        /// its own four vertices in its own colour (crisp woven patterns; <paramref name="quadRgb"/>(row, col) gives
        /// the colour of the quad from (row, col)); otherwise vertices are shared and carry the point colours.
        /// Optional poles close the first and last rows (fans).
        /// </summary>
        public int EndGrid(int rows, int cols, bool closedU, V3 inside, V3 axis, Func<int, int, uint> quadRgb = null, bool poleFirst = false,
                           bool poleLast = false)
        {
            int before = M.IndexCount;
            int n = rows * cols;
            if (_nrm.Length < n) _nrm = new V3[n * 2];
            bool hasAxis = axis.LengthSq > 1e-8f;
            V3 ax = hasAxis ? axis.Normalized : V3.Zero;
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    int jl = j > 0 ? j - 1 : closedU ? cols - 1 : j, jr = j < cols - 1 ? j + 1 : closedU ? 0 : j;
                    int iu = i > 0 ? i - 1 : i, id = i < rows - 1 ? i + 1 : i;
                    V3 du = _gp[i * cols + jr] - _gp[i * cols + jl];
                    V3 dv = _gp[id * cols + j] - _gp[iu * cols + j];
                    V3 nrm = V3.Cross(du, dv);
                    V3 p = _gp[i * cols + j];
                    V3 refp = hasAxis ? inside + ax * V3.Dot(p - inside, ax) : inside;
                    V3 radial = p - refp;
                    if (nrm.LengthSq < 1e-14f) nrm = radial;
                    if (V3.Dot(nrm, radial) < 0f) nrm = -nrm;
                    _nrm[i * cols + j] = nrm;
                }
            }
            int segs = closedU ? cols : cols - 1;
            if (quadRgb == null)
            {
                int baseV = M.VertexCount;
                for (int i = 0; i < n; i++) GridVertex(i, _gc[i]);
                for (int i = 0; i + 1 < rows; i++)
                {
                    for (int j = 0; j < segs; j++)
                    {
                        int j1 = (j + 1) % cols;
                        int a = baseV + i * cols + j, b = baseV + i * cols + j1, c = a + cols, d = b + cols;
                        Tri(a, b, d);
                        Tri(a, d, c);
                    }
                }
                if (poleFirst) GridPole(baseV, 0, cols, segs);
                if (poleLast) GridPole(baseV + (rows - 1) * cols, rows - 1, cols, segs);
            }
            else
            {
                for (int i = 0; i + 1 < rows; i++)
                {
                    for (int j = 0; j < segs; j++)
                    {
                        int j1 = (j + 1) % cols;
                        uint rgb = quadRgb(i, j);
                        int a = GridVertex(i * cols + j, rgb), b = GridVertex(i * cols + j1, rgb);
                        int c = GridVertex((i + 1) * cols + j, rgb), d = GridVertex((i + 1) * cols + j1, rgb);
                        Tri(a, b, d);
                        Tri(a, d, c);
                    }
                }
            }
            return (M.IndexCount - before) / 3;
        }

        /// <summary>
        /// Builds the grid like <see cref="EndGrid"/> with a colour per quad from <paramref name="quadRgb"/> (row-major,
        /// <c>(rows − 1) × segs</c> entries, segs = cols when closed else cols − 1): each quad gets its own vertices, so
        /// woven patterns stay crisp while the normals stay smooth. With <paramref name="rowRgb"/> instead (one colour per
        /// quad row; pass null for <paramref name="quadRgb"/>) the bands are crisp.
        /// </summary>
        public int EndGridColours(int rows, int cols, bool closedU, V3 inside, V3 axis, uint[] quadRgb, uint[] rowRgb)
        {
            int before = M.IndexCount;
            ComputeGridNormals(rows, cols, closedU, inside, axis);
            int segs = closedU ? cols : cols - 1;
            for (int i = 0; i + 1 < rows; i++)
            {
                for (int j = 0; j < segs; j++)
                {
                    int j1 = (j + 1) % cols;
                    uint rgb = quadRgb != null ? quadRgb[i * segs + j] : rowRgb != null ? rowRgb[i] : _gc[i * cols + j];
                    int a = GridVertex(i * cols + j, rgb), b = GridVertex(i * cols + j1, rgb);
                    int c = GridVertex((i + 1) * cols + j, rgb), d = GridVertex((i + 1) * cols + j1, rgb);
                    Tri(a, b, d);
                    Tri(a, d, c);
                }
            }
            return (M.IndexCount - before) / 3;
        }

        private void ComputeGridNormals(int rows, int cols, bool closedU, V3 inside, V3 axis)
        {
            int n = rows * cols;
            if (_nrm.Length < n) _nrm = new V3[n * 2];
            bool hasAxis = axis.LengthSq > 1e-8f;
            V3 ax = hasAxis ? axis.Normalized : V3.Zero;
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    int jl = j > 0 ? j - 1 : closedU ? cols - 1 : j, jr = j < cols - 1 ? j + 1 : closedU ? 0 : j;
                    int iu = i > 0 ? i - 1 : i, id = i < rows - 1 ? i + 1 : i;
                    V3 du = _gp[i * cols + jr] - _gp[i * cols + jl];
                    V3 dv = _gp[id * cols + j] - _gp[iu * cols + j];
                    V3 nrm = V3.Cross(du, dv);
                    V3 p = _gp[i * cols + j];
                    V3 refp = hasAxis ? inside + ax * V3.Dot(p - inside, ax) : inside;
                    V3 radial = p - refp;
                    if (nrm.LengthSq < 1e-14f) nrm = radial;
                    if (V3.Dot(nrm, radial) < 0f) nrm = -nrm;
                    _nrm[i * cols + j] = nrm;
                }
            }
        }

        /// <summary>Overrides the normal of grid point (<paramref name="row"/>, <paramref name="col"/>) after
        /// <see cref="ComputeGridNormals"/>; used by callers that know the true surface normal.</summary>
        public void GridNormal(int row, int col, int cols, V3 n)
        {
            _nrm[row * cols + col] = n;
        }

        private int GridVertex(int i, uint rgb)
        {
            float keep = Ao;
            Ao = keep * _gao[i];
            int v = Vertex(_gp[i], _nrm[i], rgb, _gb0[i], _gb1[i], _gw[i]);
            Ao = keep;
            return v;
        }

        private void GridPole(int first, int row, int cols, int segs)
        {
            V3 c = V3.Zero, nsum = V3.Zero;
            for (int j = 0; j < cols; j++)
            {
                c = c + _gp[row * cols + j];
                nsum = nsum + _nrm[row * cols + j].Normalized;
            }
            c = c * (1f / cols);
            int i0 = row * cols;
            float keep = Ao;
            Ao = keep * _gao[i0];
            int pole = Vertex(c, nsum, _gc[i0], _gb0[i0], _gb1[i0], _gw[i0]);
            Ao = keep;
            for (int j = 0; j < segs; j++) Tri(pole, first + j, first + (j + 1) % cols);
        }
    }
}
