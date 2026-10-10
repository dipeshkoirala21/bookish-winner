using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Generators.Fauna
{
    /// <summary>
    /// One cross-section of a <see cref="FaunaBuilder.Tube"/>: a centre, half width <see cref="A"/> (along the side
    /// axis), half heights above (<see cref="BT"/>) and below (<see cref="BB"/>) the centre, a superellipse exponent
    /// <see cref="E"/> (2 = ellipse, 3 = rounded box), the bone it follows and its colour (0xRRGGBBAA).
    /// </summary>
    public struct Knot
    {
        public Fv3 C;
        public float A, BT, BB, E;
        public FaunaBone Bone;
        public uint Colour;

        public Knot(float x, float y, float z, float a, float bt, float bb, FaunaBone bone, uint colour, float e = 2f)
        {
            C = new Fv3(x, y, z);
            A = a;
            BT = bt;
            BB = bb;
            E = e;
            Bone = bone;
            Colour = colour;
        }

        public Knot(Fv3 c, float a, float b, FaunaBone bone, uint colour, float e = 2f) : this(c.X, c.Y, c.Z, a, b, b, bone, colour, e)
        {
        }
    }

    /// <summary>
    /// Builds skinned, rounded fauna geometry into a <see cref="FaunaMesh"/>: smooth lofted tubes through Catmull-Rom
    /// knots with superellipse cross-sections (bodies, necks, limbs, tails, horns, ears, wings), ellipsoids, cartoon
    /// eyes with a highlight, and left-right mirroring. Each vertex gets up to two bones (rigid inside a bone's
    /// knots, blended smoothly across a joint), a body part, a colour and a material channel; <see cref="Finish"/>
    /// bakes ambient occlusion (other parts as sphere occluders, undersides, ground contact) into UV0.v and writes
    /// UV0.u = channel (W2_DETAIL_CONTRACT §5). Shapes are smooth-shaded within a primitive. The shared
    /// <c>Meshing.Shapes</c> library has no skin weights, so the fauna keep this small skinned loft kernel. Build
    /// time only (it allocates scratch once per builder).
    /// </summary>
    public sealed class FaunaBuilder
    {
        private readonly FaunaMesh _f;
        private readonly MeshData _m;

        private byte[] _channel = new byte[1024];
        private int[] _prim = new int[1024];
        private int _primCount;

        // AO occluders: x, y, z, r, primitive.
        private float[] _occ = new float[5 * 256];
        private int _occCount;

        // Ring scratch.
        private Fv3[] _rc = new Fv3[64], _ru = new Fv3[64], _rv = new Fv3[64], _rt = new Fv3[64];
        private float[] _ra = new float[64], _rbt = new float[64], _rbb = new float[64], _re = new float[64], _rw = new float[64];
        private byte[] _rb0 = new byte[64], _rb1 = new byte[64];
        private uint[] _rcol = new uint[64];
        private float[] _nacc = new float[3 * 1024];

        /// <summary>Height of the ground plane for the contact AO (model space).</summary>
        public float GroundY;

        /// <summary>Contact shading reaches this high above <see cref="GroundY"/> (0 = no ground term).</summary>
        public float GroundAoHeightM = 0.3f;

        /// <summary>Strength of the occlusion from other parts (0..2).</summary>
        public float OcclusionStrength = 1f;

        public FaunaBuilder(FaunaMesh target, FaunaSpecies species, int lod, CoatPattern pattern)
        {
            _f = target ?? throw new ArgumentNullException(nameof(target));
            _m = target.Mesh;
            _f.Clear();
            _f.Species = species;
            _f.Lod = lod;
            _f.Pattern = pattern;
        }

        /// <summary>The mesh being built.</summary>
        public FaunaMesh Target
        {
            get { return _f; }
        }

        public int VertexCount
        {
            get { return _m.VertexCount; }
        }

        public int IndexCount
        {
            get { return _m.IndexCount; }
        }

        // ------------------------------------------------------------------------------------------------------
        // Pivots

        /// <summary>Sets the bind pivot of a bone (and, with <paramref name="mirror"/>, of its right/left twin).</summary>
        public void Pivot(FaunaBone b, Fv3 p, bool mirror = true)
        {
            _f.Pivot[(int)b] = p;
            FaunaBone m = FaunaRig.Mirror(b);
            if (mirror && m != b) _f.Pivot[(int)m] = p.MirrorX;
        }

        public void Pivot(FaunaBone b, float x, float y, float z, bool mirror = true)
        {
            Pivot(b, new Fv3(x, y, z), mirror);
        }

        // ------------------------------------------------------------------------------------------------------
        // Primitives

        /// <summary>
        /// A smooth tube through <paramref name="count"/> knots (Catmull-Rom centres, smoothly interpolated radii and
        /// colours), <paramref name="ringsPerSpan"/> rings per knot interval and <paramref name="segs"/> vertices per
        /// ring. Frames are parallel-transported from <paramref name="upHint"/> (the knots' "top" side). Ends are
        /// closed by rounded caps that bulge by <paramref name="capStart"/> / <paramref name="capEnd"/> times the end
        /// radius (negative: open end). Knots of different bones blend smoothly across their interval. Returns the
        /// first vertex index.
        /// </summary>
        public int Tube(Knot[] knots, int count, int ringsPerSpan, int segs, Fv3 upHint, MaterialChannel ch, FaunaPart part,
                        float capStart = 0.6f, float capEnd = 0.6f)
        {
            if (knots == null || count < 2) throw new ArgumentException("a tube needs two knots or more");
            if (ringsPerSpan < 1) ringsPerSpan = 1;
            if (segs < 3) segs = 3;
            int n = (count - 1) * ringsPerSpan + 1;
            EnsureRings(n);
            for (int k = 0; k < count - 1; k++)
            {
                Knot k0 = knots[Math.Max(0, k - 1)], k1 = knots[k], k2 = knots[k + 1], k3 = knots[Math.Min(count - 1, k + 2)];
                Fv3 p0 = k == 0 ? k1.C * 2f - k2.C : k0.C;
                Fv3 p3 = k + 2 >= count ? k2.C * 2f - k1.C : k3.C;
                for (int r = 0; r < ringsPerSpan; r++)
                {
                    float t = r / (float)ringsPerSpan;
                    int i = k * ringsPerSpan + r;
                    _rc[i] = Fv3.CatmullRom(p0, k1.C, k2.C, p3, t);
                    float s = FMath.SmoothStep(t);
                    _ra[i] = Smooth(k0.A, k1.A, k2.A, k3.A, t, k == 0, k + 2 >= count);
                    _rbt[i] = Smooth(k0.BT, k1.BT, k2.BT, k3.BT, t, k == 0, k + 2 >= count);
                    _rbb[i] = Smooth(k0.BB, k1.BB, k2.BB, k3.BB, t, k == 0, k + 2 >= count);
                    _re[i] = FMath.Lerp(k1.E, k2.E, t);
                    _rcol[i] = FMath.LerpColour(k1.Colour, k2.Colour, s);
                    _rb0[i] = (byte)k1.Bone;
                    _rb1[i] = (byte)k2.Bone;
                    _rw[i] = k1.Bone == k2.Bone ? 1f : 1f - s;
                }
            }
            Knot last = knots[count - 1];
            _rc[n - 1] = last.C;
            _ra[n - 1] = last.A;
            _rbt[n - 1] = last.BT;
            _rbb[n - 1] = last.BB;
            _re[n - 1] = last.E;
            _rcol[n - 1] = last.Colour;
            _rb0[n - 1] = _rb1[n - 1] = (byte)last.Bone;
            _rw[n - 1] = 1f;
            // Tangents and parallel-transported frames.
            for (int i = 0; i < n; i++)
            {
                Fv3 a = _rc[Math.Max(0, i - 1)], b = _rc[Math.Min(n - 1, i + 1)];
                _rt[i] = (b - a).NormalizedOr(i > 0 ? _rt[i - 1] : Fv3.Forward);
            }
            Fv3 u0 = Fv3.Cross(upHint, _rt[0]);
            if (u0.Length < 1e-4f) u0 = Fv3.Cross(Math.Abs(_rt[0].Y) < 0.9f ? Fv3.Up : Fv3.Forward, _rt[0]);
            _ru[0] = u0.Normalized;
            for (int i = 1; i < n; i++)
            {
                Fv3 u = _ru[i - 1] - _rt[i] * Fv3.Dot(_ru[i - 1], _rt[i]);
                _ru[i] = u.NormalizedOr(_ru[i - 1]);
            }
            for (int i = 0; i < n; i++) _rv[i] = Fv3.Cross(_rt[i], _ru[i]);
            return Strip(n, segs, ch, part, capStart, capEnd);
        }

        /// <summary>
        /// An ellipsoid centred at <paramref name="c"/>: <paramref name="rFwd"/> along <paramref name="fwd"/>,
        /// <paramref name="rUp"/> along <paramref name="up"/> and <paramref name="rSide"/> across, with
        /// <paramref name="rings"/> latitude bands (≥ 2) of <paramref name="segs"/> vertices. Rigid on one bone.
        /// </summary>
        public int Ellipsoid(Fv3 c, Fv3 fwd, Fv3 up, float rSide, float rUp, float rFwd, int rings, int segs, FaunaBone bone, uint colour,
                             MaterialChannel ch, FaunaPart part, float e = 2f)
        {
            if (rings < 2) rings = 2;
            if (segs < 3) segs = 3;
            Fv3 t = fwd.Normalized;
            Fv3 u = Fv3.Cross(up, t).NormalizedOr(Fv3.Right);
            Fv3 v = Fv3.Cross(t, u);
            int n = rings - 1;
            EnsureRings(n);
            for (int i = 0; i < n; i++)
            {
                float a = FMath.Pi * (i + 1) / rings;
                float z = -(float)Math.Cos(a), s = (float)Math.Sin(a);
                _rc[i] = c + t * (z * rFwd);
                _rt[i] = t;
                _ru[i] = u;
                _rv[i] = v;
                _ra[i] = rSide * s;
                _rbt[i] = _rbb[i] = rUp * s;
                _re[i] = e;
                _rcol[i] = colour;
                _rb0[i] = _rb1[i] = (byte)bone;
                _rw[i] = 1f;
            }
            // Caps reach the poles exactly.
            float capLen = rFwd * (1f - (float)Math.Cos(FMath.Pi / rings));
            float r0 = Math.Max(1e-5f, Math.Min(_ra[0], _rbt[0]));
            return Strip(n, segs, ch, part, capLen / r0, capLen / r0, true);
        }

        /// <summary>Ellipsoid with forward +Z and up +Y.</summary>
        public int Blob(Fv3 c, float rSide, float rUp, float rFwd, int rings, int segs, FaunaBone bone, uint colour, MaterialChannel ch,
                        FaunaPart part)
        {
            return Ellipsoid(c, Fv3.Forward, Fv3.Up, rSide, rUp, rFwd, rings, segs, bone, colour, ch, part);
        }

        /// <summary>
        /// A cartoon eye on the side of a head: a glossy eyeball (iris colour, slightly flattened along
        /// <paramref name="outward"/>), an optional dark pupil, and a white catch-light towards the sun, all on
        /// <paramref name="bone"/>. <paramref name="detail"/> 0 = eyeball only (far LODs).
        /// </summary>
        public void Eye(Fv3 c, float r, Fv3 outward, Fv3 up, FaunaBone bone, uint iris, uint pupil, int detail)
        {
            Fv3 o = outward.Normalized;
            int rings = detail >= 1 ? 3 : 2, segs = detail >= 2 ? 7 : detail >= 1 ? 5 : 4;
            Ellipsoid(c, o, up, r, r, r * 0.8f, rings, segs, bone, detail > 0 ? iris : pupil, MaterialChannel.Paint, FaunaPart.Eye);
            if (detail <= 0) return;
            if (detail >= 2 && pupil != iris)
                Ellipsoid(c + o * (r * 0.62f), o, up, r * 0.52f, r * 0.58f, r * 0.3f, 3, 5, bone, pupil, MaterialChannel.Paint, FaunaPart.Eye);
            Fv3 side = Fv3.Cross(up, o).NormalizedOr(Fv3.Forward);
            Fv3 hl = c + o * (r * 0.78f) + up.Normalized * (r * 0.38f) + side * (r * 0.22f);
            Ellipsoid(hl, o, up, r * 0.24f, r * 0.24f, r * 0.12f, 2, 4, bone, 0xFFFFFF00u, MaterialChannel.Glass, FaunaPart.Eye);
        }

        /// <summary>
        /// Mirrors vertices [<paramref name="v0"/>, end) and their triangles from index <paramref name="i0"/> across
        /// the median plane (x → −x, left bones ↔ right bones, winding reversed), with their AO occluders.
        /// </summary>
        public void Mirror(int v0, int i0, int occ0 = -1)
        {
            int v1 = _m.VertexCount, i1 = _m.IndexCount;
            int nv = v1 - v0;
            _m.Reserve(nv, i1 - i0);
            EnsureVerts(v1 + nv);
            int primBase = _primCount;
            int primMin = int.MaxValue;
            for (int v = v0; v < v1; v++) primMin = Math.Min(primMin, _prim[v]);
            for (int v = v0; v < v1; v++)
            {
                int p = 3 * v;
                int w = _m.AddVertex(-_m.Positions[p], _m.Positions[p + 1], _m.Positions[p + 2], -_m.Normals[p], _m.Normals[p + 1], _m.Normals[p + 2],
                                     ColourAt(v), (float)_channel[v], 1f);
                _f.Bone0[w] = (byte)FaunaRig.Mirror((FaunaBone)_f.Bone0[v]);
                _f.Bone1[w] = (byte)FaunaRig.Mirror((FaunaBone)_f.Bone1[v]);
                _f.Weight0[w] = _f.Weight0[v];
                _f.Part[w] = _f.Part[v];
                _channel[w] = _channel[v];
                _prim[w] = primBase + (_prim[v] - primMin);
                if (_prim[w] + 1 > _primCount) _primCount = _prim[w] + 1;
            }
            for (int i = i0; i < i1; i += 3)
            {
                int a = _m.Indices[i] - v0 + v1, b = _m.Indices[i + 1] - v0 + v1, c = _m.Indices[i + 2] - v0 + v1;
                _m.AddTriangle(a, c, b);
            }
            if (occ0 >= 0)
            {
                int oc = _occCount;
                for (int k = occ0; k < oc; k++)
                {
                    int q = 5 * k;
                    AddOccluder(-_occ[q], _occ[q + 1], _occ[q + 2], _occ[q + 3], primBase + ((int)_occ[q + 4] - primMin));
                }
            }
        }

        /// <summary>
        /// Adopts the vertices a <c>Meshing.Shapes</c> emitter appended since <paramref name="v0"/> (rounded rigid
        /// details: hooves, horns, bells, collars) as one primitive skinned rigidly to <paramref name="bone"/>: bone
        /// weights, body part, the material channel the brush wrote to UV0.u, and an AO occluder round its bounds.
        /// Returns <paramref name="v0"/>.
        /// </summary>
        public int Adopt(int v0, FaunaBone bone, FaunaPart part)
        {
            int n = _m.VertexCount;
            if (n <= v0) return v0;
            EnsureVerts(n);
            int prim = _primCount++;
            float cx = 0f, cy = 0f, cz = 0f;
            for (int v = v0; v < n; v++)
            {
                cx += _m.Positions[3 * v];
                cy += _m.Positions[3 * v + 1];
                cz += _m.Positions[3 * v + 2];
            }
            float inv = 1f / (n - v0);
            cx *= inv;
            cy *= inv;
            cz *= inv;
            float r2 = 0f;
            for (int v = v0; v < n; v++)
            {
                float dx = _m.Positions[3 * v] - cx, dy = _m.Positions[3 * v + 1] - cy, dz = _m.Positions[3 * v + 2] - cz;
                r2 = Math.Max(r2, dx * dx + dy * dy + dz * dz);
                _f.Bone0[v] = (byte)bone;
                _f.Bone1[v] = (byte)bone;
                _f.Weight0[v] = 1f;
                _f.Part[v] = (byte)part;
                _channel[v] = (byte)Math.Max(0f, Math.Min(255f, _m.Uv0[2 * v] + 0.5f));
                _prim[v] = prim;
            }
            AddOccluder(cx, cy, cz, 0.6f * (float)Math.Sqrt(r2), prim);
            return v0;
        }

        /// <summary>Number of AO occluders so far (pass to <see cref="Mirror"/> to mirror the ones added after).</summary>
        public int OccluderCount
        {
            get { return _occCount; }
        }

        // ------------------------------------------------------------------------------------------------------
        // Vertex access for painters

        public Fv3 PositionOf(int v)
        {
            return new Fv3(_m.Positions[3 * v], _m.Positions[3 * v + 1], _m.Positions[3 * v + 2]);
        }

        public Fv3 NormalOf(int v)
        {
            return new Fv3(_m.Normals[3 * v], _m.Normals[3 * v + 1], _m.Normals[3 * v + 2]);
        }

        public FaunaPart PartOf(int v)
        {
            return (FaunaPart)_f.Part[v];
        }

        public FaunaBone BoneOf(int v)
        {
            return (FaunaBone)_f.Bone0[v];
        }

        /// <summary>Colour of a vertex as 0xRRGGBBAA.</summary>
        public uint ColourAt(int v)
        {
            int c = 4 * v;
            return ((uint)_m.Colors[c] << 24) | ((uint)_m.Colors[c + 1] << 16) | ((uint)_m.Colors[c + 2] << 8) | _m.Colors[c + 3];
        }

        public void SetColour(int v, uint rgba)
        {
            int c = 4 * v;
            _m.Colors[c] = (byte)(rgba >> 24);
            _m.Colors[c + 1] = (byte)(rgba >> 16);
            _m.Colors[c + 2] = (byte)(rgba >> 8);
            _m.Colors[c + 3] = (byte)rgba;
        }

        public void SetChannel(int v, MaterialChannel ch)
        {
            _channel[v] = (byte)ch;
        }

        public MaterialChannel ChannelOf(int v)
        {
            return (MaterialChannel)_channel[v];
        }

        /// <summary>Sets the part of vertices [v0, v1).</summary>
        public void SetPart(int v0, int v1, FaunaPart part)
        {
            for (int v = v0; v < v1; v++) _f.Part[v] = (byte)part;
        }

        // ------------------------------------------------------------------------------------------------------
        // Finish

        /// <summary>Bakes AO, writes UV0 = (channel, AO), and fills the used-bone mask, eye height and bounds.</summary>
        public void Finish()
        {
            MeshData m = _m;
            int n = m.VertexCount;
            float maxR2 = 0f;
            for (int v = 0; v < n; v++)
            {
                int p = 3 * v;
                float x = m.Positions[p], y = m.Positions[p + 1], z = m.Positions[p + 2];
                float nx = m.Normals[p], ny = m.Normals[p + 1], nz = m.Normals[p + 2];
                float occ = 0f;
                int own = _prim[v];
                for (int k = 0; k < _occCount; k++)
                {
                    int q = 5 * k;
                    if ((int)_occ[q + 4] == own) continue;
                    float dx = _occ[q] - x, dy = _occ[q + 1] - y, dz = _occ[q + 2] - z;
                    float d2 = dx * dx + dy * dy + dz * dz;
                    if (d2 < 1e-10f) continue;
                    float d = (float)Math.Sqrt(d2);
                    float cos = (nx * dx + ny * dy + nz * dz) / d;
                    float r = _occ[q + 3];
                    if (d < r)
                    {
                        // Inside another part (a joint seam): darken gently, these faces are mostly hidden.
                        occ += 0.35f * Math.Max(0.2f, cos);
                        continue;
                    }
                    if (cos <= 0f) continue;
                    occ += Math.Min(1f, r * r / d2) * cos;
                }
                float ao = 1f / (1f + 0.9f * OcclusionStrength * occ);
                // Undersides catch less sky.
                if (ny < 0f) ao *= 1f - 0.25f * -ny;
                // Ground contact.
                if (GroundAoHeightM > 0f)
                {
                    float h = (y - GroundY) / GroundAoHeightM;
                    if (h < 1f)
                    {
                        float g = FMath.Lerp(0.5f, 1f, FMath.SmoothStep(FMath.Clamp01(h)));
                        ao *= FMath.Lerp(1f, g, FMath.Clamp01(0.6f - 0.6f * ny));
                    }
                }
                ao = FMath.Clamp(ao, 0.18f, 1f);
                m.Uv0[2 * v] = _channel[v];
                m.Uv0[2 * v + 1] = ao;
                float r2 = x * x + (y - 0.5f * _f.EyeHeightM) * (y - 0.5f * _f.EyeHeightM) + z * z;
                if (r2 > maxR2) maxR2 = r2;
                _f.UsedBones |= 1u << _f.Bone0[v];
                if (_f.Weight0[v] < 1f) _f.UsedBones |= 1u << _f.Bone1[v];
            }
            m.HasUv0 = true;
            // Belly line under the chest and the loins.
            float zc = _f.Pivot[(int)FaunaBone.Chest].Z, zp = _f.Pivot[(int)FaunaBone.Pelvis].Z, zm = 0.5f * (zc + zp);
            _f.BellyFrontY = _f.BellyRearY = float.MaxValue;
            _f.BodyHalfWidthM = 0f;
            for (int v = 0; v < n; v++)
            {
                if (_f.Part[v] != (byte)FaunaPart.Body) continue;
                float y = m.Positions[3 * v + 1], z = m.Positions[3 * v + 2];
                _f.BodyHalfWidthM = Math.Max(_f.BodyHalfWidthM, Math.Abs(m.Positions[3 * v]));
                if (z >= zm && z <= zc + 0.6f * (zc - zm) && y < _f.BellyFrontY)
                {
                    _f.BellyFrontY = y;
                    _f.BellyFrontZ = z;
                }
                else if (z < zm && z >= zp - 0.6f * (zm - zp) && y < _f.BellyRearY)
                {
                    _f.BellyRearY = y;
                    _f.BellyRearZ = z;
                }
            }
            if (_f.BellyFrontY == float.MaxValue)
            {
                _f.BellyFrontY = 0f;
                _f.BellyFrontZ = zc;
            }
            if (_f.BellyRearY == float.MaxValue)
            {
                _f.BellyRearY = _f.BellyFrontY;
                _f.BellyRearZ = zp;
            }
            // Poses can stretch a limb or lift a wing: leave a margin.
            _f.BoundsRadiusM = (float)Math.Sqrt(maxR2) * 1.35f + 0.1f;
        }

        // ------------------------------------------------------------------------------------------------------
        // Internals

        private static float Smooth(float a0, float a1, float a2, float a3, float t, bool first, bool last)
        {
            // Catmull-Rom on the radii (clamped so a fat knot next to a thin one cannot overshoot below zero).
            float p0 = first ? a1 : a0, p3 = last ? a2 : a3;
            float t2 = t * t, t3 = t2 * t;
            float v = 0.5f * (2f * a1 + (-p0 + a2) * t + (2f * p0 - 5f * a1 + 4f * a2 - p3) * t2 + (-p0 + 3f * a1 - 3f * a2 + p3) * t3);
            float lo = Math.Min(a1, a2), hi = Math.Max(a1, a2);
            return FMath.Clamp(v, lo * 0.85f, hi * 1.08f);
        }

        private void EnsureRings(int n)
        {
            if (_rc.Length >= n) return;
            int cap = Math.Max(n, _rc.Length * 2);
            Array.Resize(ref _rc, cap);
            Array.Resize(ref _ru, cap);
            Array.Resize(ref _rv, cap);
            Array.Resize(ref _rt, cap);
            Array.Resize(ref _ra, cap);
            Array.Resize(ref _rbt, cap);
            Array.Resize(ref _rbb, cap);
            Array.Resize(ref _re, cap);
            Array.Resize(ref _rw, cap);
            Array.Resize(ref _rb0, cap);
            Array.Resize(ref _rb1, cap);
            Array.Resize(ref _rcol, cap);
        }

        private void EnsureVerts(int n)
        {
            _f.EnsureSide(n);
            if (_channel.Length < n)
            {
                int cap = Math.Max(n, _channel.Length * 2);
                Array.Resize(ref _channel, cap);
                Array.Resize(ref _prim, cap);
            }
            if (_nacc.Length < 3 * n) Array.Resize(ref _nacc, Math.Max(3 * n, _nacc.Length * 2));
        }

        private void AddOccluder(float x, float y, float z, float r, int prim)
        {
            if (r < 0.006f) return;
            if (5 * (_occCount + 1) > _occ.Length) Array.Resize(ref _occ, _occ.Length * 2);
            int q = 5 * _occCount++;
            _occ[q] = x;
            _occ[q + 1] = y;
            _occ[q + 2] = z;
            _occ[q + 3] = r;
            _occ[q + 4] = prim;
        }

        /// <summary>Emits the rings in the scratch arrays as a closed strip with optional pole caps.</summary>
        private int Strip(int n, int segs, MaterialChannel ch, FaunaPart part, float capStart, float capEnd, bool exactCaps = false)
        {
            int prim = _primCount++;
            int first = _m.VertexCount;
            bool c0 = capStart >= 0f, c1 = capEnd >= 0f;
            int nv = n * segs + (c0 ? 1 : 0) + (c1 ? 1 : 0);
            _m.Reserve(nv, (n - 1) * segs * 6 + (c0 ? segs * 3 : 0) + (c1 ? segs * 3 : 0));
            EnsureVerts(first + nv);
            for (int i = 0; i < n; i++)
            {
                float a = _ra[i], bt = _rbt[i], bb = _rbb[i];
                float ex = 2f / Math.Max(0.5f, _re[i]);
                for (int j = 0; j < segs; j++)
                {
                    float phi = FMath.TwoPi * j / segs;
                    float su = (float)Math.Sin(phi), sv = (float)Math.Cos(phi);
                    float pu = Math.Sign(su) * (float)Math.Pow(Math.Abs(su), ex);
                    float pv = Math.Sign(sv) * (float)Math.Pow(Math.Abs(sv), ex);
                    Fv3 p = _rc[i] + _ru[i] * (a * pu) + _rv[i] * (pv * (pv >= 0f ? bt : bb));
                    AddVert(p, _rcol[i], ch, part, _rb0[i], _rb1[i], _rw[i], prim);
                }
                AddOccluder(_rc[i].X, _rc[i].Y, _rc[i].Z, 0.85f * Math.Min(a, 0.5f * (bt + bb)), prim);
            }
            int pole0 = -1, pole1 = -1;
            if (c0)
            {
                float r0 = Math.Min(_ra[0], 0.5f * (_rbt[0] + _rbb[0]));
                Fv3 p = _rc[0] - _rt[0] * (r0 * capStart);
                if (exactCaps) p = _rc[0] - _rt[0] * (capStart * Math.Max(1e-5f, Math.Min(_ra[0], _rbt[0])));
                pole0 = AddVert(p, _rcol[0], ch, part, _rb0[0], _rb1[0], _rw[0], prim);
            }
            if (c1)
            {
                int l = n - 1;
                float r1 = Math.Min(_ra[l], 0.5f * (_rbt[l] + _rbb[l]));
                Fv3 p = _rc[l] + _rt[l] * (r1 * capEnd);
                if (exactCaps) p = _rc[l] + _rt[l] * (capEnd * Math.Max(1e-5f, Math.Min(_ra[l], _rbt[l])));
                pole1 = AddVert(p, _rcol[l], ch, part, _rb0[l], _rb1[l], _rw[l], prim);
            }
            int t0 = _m.IndexCount;
            for (int i = 0; i < n - 1; i++)
            {
                Fv3 mid = (_rc[i] + _rc[i + 1]) * 0.5f;
                for (int j = 0; j < segs; j++)
                {
                    int j1 = (j + 1) % segs;
                    int a = first + i * segs + j, b = first + i * segs + j1, c = first + (i + 1) * segs + j, d = first + (i + 1) * segs + j1;
                    TriOut(a, b, c, mid, Fv3.Zero, false);
                    TriOut(b, d, c, mid, Fv3.Zero, false);
                }
            }
            if (c0)
                for (int j = 0; j < segs; j++)
                    TriOut(pole0, first + (j + 1) % segs, first + j, _rc[0], -_rt[0], true);
            if (c1)
            {
                int l = first + (n - 1) * segs;
                for (int j = 0; j < segs; j++) TriOut(pole1, l + j, l + (j + 1) % segs, _rc[n - 1], _rt[n - 1], true);
            }
            SmoothNormals(first, _m.VertexCount, t0, _m.IndexCount);
            return first;
        }

        private int AddVert(Fv3 p, uint colour, MaterialChannel ch, FaunaPart part, byte b0, byte b1, float w0, int prim)
        {
            int v = _m.AddVertex(p.X, p.Y, p.Z, 0f, 1f, 0f, colour, (float)ch, 1f);
            EnsureVerts(v + 1);
            // Keep the heavier bone first.
            if (w0 < 0.5f)
            {
                byte t = b0;
                b0 = b1;
                b1 = t;
                w0 = 1f - w0;
            }
            if (b0 == b1) w0 = 1f;
            _f.Bone0[v] = b0;
            _f.Bone1[v] = b1;
            _f.Weight0[v] = w0;
            _f.Part[v] = (byte)part;
            _channel[v] = (byte)ch;
            _prim[v] = prim;
            return v;
        }

        /// <summary>
        /// Adds a triangle whose front faces away from <paramref name="centre"/> (or along <paramref name="dir"/> when
        /// <paramref name="useDir"/>), swapping the winding when needed (Unity: cross(b − a, c − a) is the front).
        /// </summary>
        private void TriOut(int a, int b, int c, Fv3 centre, Fv3 dir, bool useDir)
        {
            Fv3 pa = PositionOf(a), pb = PositionOf(b), pc = PositionOf(c);
            Fv3 n = Fv3.Cross(pb - pa, pc - pa);
            if (n.Length < 1e-12f) return;
            Fv3 o = useDir ? dir : (pa + pb + pc) * (1f / 3f) - centre;
            if (Fv3.Dot(n, o) < 0f) _m.AddTriangle(a, c, b);
            else _m.AddTriangle(a, b, c);
        }

        private void SmoothNormals(int v0, int v1, int i0, int i1)
        {
            MeshData m = _m;
            Array.Clear(_nacc, 3 * v0, 3 * (v1 - v0));
            for (int i = i0; i < i1; i += 3)
            {
                int a = m.Indices[i], b = m.Indices[i + 1], c = m.Indices[i + 2];
                Fv3 pa = PositionOf(a), pb = PositionOf(b), pc = PositionOf(c);
                Fv3 n = Fv3.Cross(pb - pa, pc - pa);
                for (int k = 0; k < 3; k++)
                {
                    int v = k == 0 ? a : k == 1 ? b : c;
                    _nacc[3 * v] += n.X;
                    _nacc[3 * v + 1] += n.Y;
                    _nacc[3 * v + 2] += n.Z;
                }
            }
            for (int v = v0; v < v1; v++)
            {
                var n = new Fv3(_nacc[3 * v], _nacc[3 * v + 1], _nacc[3 * v + 2]);
                n = n.NormalizedOr(Fv3.Up);
                m.Normals[3 * v] = n.X;
                m.Normals[3 * v + 1] = n.Y;
                m.Normals[3 * v + 2] = n.Z;
            }
        }
    }
}
