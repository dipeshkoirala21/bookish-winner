using System;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Characters
{
    public static partial class HumanoidMesher
    {
        /// <summary>
        /// The shape parameters of a face state (P §2.8): mouth width, corner lift, opening, teeth and tongue, O-shape;
        /// eyelid closure, lower-lid raise, brow raise and inner-brow tilt, cheek puff and gaze. Every state drives the
        /// same topology (mouth, teeth, tongue and lid grids are always built, collapsed when unused), so the player's
        /// mesh carries the states as blend shapes.
        /// </summary>
        private struct Expression
        {
            public float MouthW, Corner, Open, TeethTop, TeethBottom, Tongue, Round;
            public float Lid, LowerLid, BrowRaise, BrowInner, Cheek;
            public float LookX, LookY;

            public static Expression For(in FaceState f, CharacterRecipe r)
            {
                var e = new Expression { MouthW = 0.031f, Corner = 0.0075f, Open = 0.0042f, Lid = 0f, LowerLid = 0.08f, Cheek = 0.010f };
                switch (f.Expression)
                {
                    case FaceExpression.Neutral:
                        e.MouthW = 0.024f;
                        e.Corner = 0.0005f;
                        e.Open = 0.003f;
                        e.Lid = 0.06f;
                        e.LowerLid = 0.04f;
                        e.Cheek = 0.008f;
                        break;
                    case FaceExpression.Joy:
                        e.MouthW = 0.036f;
                        e.Corner = 0.006f;
                        e.Open = 0.026f;
                        e.TeethTop = 0.0068f;
                        e.Tongue = 0.42f;
                        e.Lid = 0.5f;
                        e.LowerLid = 0.8f;
                        e.BrowRaise = 0.006f;
                        e.Cheek = 0.017f;
                        break;
                    case FaceExpression.WinceLaugh:
                        e.MouthW = 0.039f;
                        e.Corner = 0.003f;
                        e.Open = 0.015f;
                        e.TeethTop = 0.0066f;
                        e.TeethBottom = 0.006f;
                        e.Lid = 0.86f;
                        e.LowerLid = 0.45f;
                        e.BrowInner = -0.007f;
                        e.Cheek = 0.016f;
                        break;
                    case FaceExpression.Puff:
                        e.MouthW = 0.012f;
                        e.Corner = 0f;
                        e.Open = 0.019f;
                        e.Round = 1f;
                        e.Lid = 0.45f;
                        e.LowerLid = 0.1f;
                        e.BrowInner = 0.005f;
                        e.Cheek = 0.03f;
                        break;
                    case FaceExpression.Calm:
                        e.MouthW = 0.025f;
                        e.Corner = 0.0045f;
                        e.Open = 0.0032f;
                        e.Lid = 0.32f;
                        e.LowerLid = 0.16f;
                        e.BrowRaise = -0.002f;
                        break;
                }
                if (r.Age == AgeGroup.Elder) e.Lid = Math.Max(e.Lid, 0.14f); // a little heavier lids
                float blink = CharMath.Clamp01(f.Blink);
                e.Lid = e.Lid + (1f - e.Lid) * blink;
                e.LookX = CharMath.Clamp(f.LookX, -1f, 1f);
                e.LookY = CharMath.Clamp(f.LookY, -1f, 1f);
                return e;
            }
        }

        private sealed partial class Builder
        {
            private readonly Expression _expr;

            // Scratch for face bands (cols ≤ 16).
            private readonly float[] _fx = new float[16], _fyA = new float[16], _fyB = new float[16];

            /// <summary>Eye height relative to the head centre (45% of the head up from the chin) and eye spacing.</summary>
            private float EyeY
            {
                get { return -0.1f * _hr.Y; }
            }

            private float EyeX
            {
                get { return 0.058f * _hs; }
            }

            private float NoseY
            {
                get { return -0.33f * _hr.Y; }
            }

            private float MouthY
            {
                get { return -0.565f * _hr.Y; }
            }

            // ----- The sculpted head --------------------------------------------------------------------------------

            /// <summary>Jaw taper: the lower head narrows toward the chin.</summary>
            private static float JawX(float ny)
            {
                if (ny >= -0.12f) return 1f;
                float t = Math.Min(1f, (-0.12f - ny) / 0.88f);
                return 1f - 0.27f * MathF.Pow(t, 1.5f);
            }

            private static float G(float a, float sa, float b, float sb)
            {
                return MathF.Exp(-(a * a / sa + b * b / sb));
            }

            /// <summary>Forward relief of the face in metres at normalised (nx, ny): cheeks (puffed by the expression),
            /// eye sockets, brow ridge, muzzle and chin.</summary>
            private float Relief(float nx, float ny)
            {
                float ax = MathF.Abs(nx);
                float d = _expr.Cheek * G(ax - 0.52f, 0.03f, ny + 0.37f, 0.035f);
                d -= 0.008f * G(ax - 0.31f, 0.012f, ny + 0.10f, 0.012f);
                d += 0.004f * G(ax - 0.30f, 0.025f, ny - 0.14f, 0.005f);
                d += 0.006f * G(nx, 0.06f, ny + 0.58f, 0.025f);
                d += 0.009f * G(nx, 0.035f, ny + 0.88f, 0.015f);
                return d * _hs;
            }

            /// <summary>The head surface point for a point (nx, ny, nz) of the unit superellipsoid.</summary>
            private V3 HeadDeform(float nx, float ny, float nz)
            {
                float x = nx * _hr.X * JawX(ny), y = ny * _hr.Y, z = nz * _hr.Z;
                if (ny < -0.25f)
                {
                    float t = (-0.25f - ny) / 0.75f;
                    z *= 1f - (nz < 0f ? 0.3f : 0.08f) * t * t; // the back of the lower head narrows into the neck
                }
                float front = CharMath.Clamp01(nz * 1.8f);
                if (front > 0f) z += Relief(nx, ny) * front;
                return new V3(_hc.X + x, _hc.Y + y, _hc.Z + z);
            }

            /// <summary>The face surface at (x, y) metres from the head centre, lifted along its normal.</summary>
            private V3 FacePoint(float x, float y, float lift, out V3 n)
            {
                V3 p = FaceRaw(x, y);
                V3 px = FaceRaw(x + 0.0015f, y), py = FaceRaw(x, y + 0.0015f);
                n = V3.Cross(py - p, px - p).Normalized;
                if (n.Z < 0f) n = -n;
                if (n.LengthSq < 0.5f) n = V3.Forward;
                return p + n * lift;
            }

            private V3 FaceRaw(float x, float y)
            {
                float ny = CharMath.Clamp(y / _hr.Y, -0.999f, 0.999f);
                float nx = x / (_hr.X * JawX(ny));
                float t = 1f - MathF.Pow(MathF.Abs(nx), HeadExponent) - MathF.Pow(MathF.Abs(ny), HeadExponent);
                float nz = t > 0f ? MathF.Pow(t, 1f / HeadExponent) : 0f;
                return HeadDeform(CharMath.Clamp(nx, -1f, 1f), ny, nz);
            }

            private void Head()
            {
                Mat(MaterialChannel.Skin);
                int cols = Lod == 0 ? 26 : Lod == 1 ? 12 : Mid ? 9 : 7;
                int rows = Lod == 0 ? 17 : Lod == 1 ? 8 : Mid ? 6 : 5;
                float pe = 2f / HeadExponent;
                bool stubble = _r.Beard == FacialHair.Stubble || _r.Beard == FacialHair.ShortBeard || _r.Beard == FacialHair.Goatee ||
                               _r.Beard == FacialHair.LongBeard;
                uint shadowJaw = CharacterPalette.Mix(_skin, _hair, 0.28f);
                _k.BeginGrid(rows, cols);
                for (int i = 0; i < rows; i++)
                {
                    float phi = 0.035f + (MathF.PI - 0.07f) * i / (rows - 1);
                    float sp = MathF.Sin(phi), cp = MathF.Cos(phi);
                    for (int j = 0; j < cols; j++)
                    {
                        float a = 6.28318530718f * j / cols;
                        float nx = BodyKit.SPow(sp, pe) * BodyKit.SPow(MathF.Sin(a), pe);
                        float ny = BodyKit.SPow(cp, pe);
                        float nz = BodyKit.SPow(sp, pe) * BodyKit.SPow(MathF.Cos(a), pe);
                        uint col = _skin;
                        if (stubble && Lod < 2)
                        {
                            // Jaw, chin and upper lip; not the cheeks.
                            float jaw = CharMath.Clamp01((-0.42f - ny) * 4f) * CharMath.Clamp01((nz + 0.35f) * 3f);
                            float lip = G(nx, 0.03f, ny + 0.47f, 0.004f) * CharMath.Clamp01(nz * 3f);
                            float m = Math.Max(jaw, lip);
                            if (m > 0.45f) col = shadowJaw;
                        }
                        _k.GridSet(i, j, cols, HeadDeform(nx, ny, nz), col, Bone.Head, Bone.Head, 1f);
                    }
                }
                _k.EndGrid(rows, cols, true, _hc, V3.Zero, null, true, true);
            }

            // ----- Face ---------------------------------------------------------------------------------------------

            private void Face()
            {
                if (Far)
                {
                    // Far body: two dark eyes.
                    Mat(MaterialChannel.Plain);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        V3 p = FacePoint(s * EyeX, EyeY, -0.002f, out V3 n);
                        _k.Ellipsoid(p, new V3(0.017f, 0.022f, 0.008f) * _hs, Quat.Euler(0f, s * 14f, 0f), CharacterPalette.Pupil, 5, 2, Bone.Head);
                    }
                    return;
                }
                if (Lod >= 2)
                {
                    // Mid-distance body: eye whites with dark irises looking out of them (they read at 15–40 m).
                    Mat(MaterialChannel.Plain);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        EyeFrame(s, out V3 c, out V3 r, out Quat rot);
                        _k.Ellipsoid(c, r, rot, CharacterPalette.EyeWhite, 5, 3, Bone.Head);
                        V3 ip = BodyKit.PatchPoint(c, r, rot, 0f, -0.04f, -0.25f * r.Z, out V3 inrm);
                        _k.Ellipsoid(ip, new V3(0.6f * r.X, 0.62f * r.Y, 0.5f * r.Z), rot, CharacterPalette.Iris[_r.EyeColour % CharacterPalette.Iris.Length] == CharacterPalette.Iris[0]
                                         ? CharacterPalette.Pupil : CharacterPalette.Shade(CharacterPalette.Iris[_r.EyeColour % CharacterPalette.Iris.Length], 0.7f), 4, 2, Bone.Head);
                    }
                    return;
                }
                for (int s = -1; s <= 1; s += 2) Eye(s);
                Brows();
                Nose();
                Mouth();
                if (Lod == 0) Cheeks();
            }

            /// <summary>The frame of an eye: centre (sunk into the socket), radii and rotation.</summary>
            private void EyeFrame(int s, out V3 c, out V3 r, out Quat rot)
            {
                V3 p = FacePoint(s * EyeX, EyeY, 0f, out V3 n);
                r = new V3(0.026f, 0.031f, 0.013f) * _hs;
                float yaw = MathF.Atan2(n.X, n.Z) * Quat.Rad2Deg * 0.75f;
                rot = Quat.Euler(-5f, yaw, 0f);
                c = p - n * (0.3f * r.Z);
            }

            private void Eye(int s)
            {
                EyeFrame(s, out V3 c, out V3 r, out Quat rot);
                Mat(MaterialChannel.Plain);
                _k.Ellipsoid(c, r, rot, CharacterPalette.EyeWhite, _k.Seg(12, 6), _k.Seg(8, 4), Bone.Head);
                // Iris: pupil, iris, a darker ring toward the edge and the limbal line.
                uint iris = CharacterPalette.Iris[_r.EyeColour % CharacterPalette.Iris.Length];
                uint[] cols = Lod == 0
                    ? new[] { CharacterPalette.Pupil, iris, CharacterPalette.Shade(iris, 0.68f), CharacterPalette.Shade(iris, 0.38f) }
                    : new[] { CharacterPalette.Pupil, iris, CharacterPalette.Shade(iris, 0.5f) };
                float[] edges = Lod == 0 ? new[] { 0.44f, 0.76f, 0.9f, 1f } : new[] { 0.45f, 0.85f, 1f };
                float ix = _expr.LookX * 0.24f, iy = -0.04f + _expr.LookY * 0.18f;
                _k.EllipsoidPatch(c, r, rot, ix, iy, 0.6f, 0.6f * r.X / r.Y * 1.04f, 0.0006f * _hs, cols, edges, Lod == 0 ? 2 : 1, _k.Seg(12, 7), Bone.Head);
                // Highlights from the upper left (both eyes the same way, as the light comes).
                // They shrink away as the lid closes (same topology for the blink blend shape).
                float open = Math.Max(0f, 1f - Math.Max(0f, _expr.Lid - 0.3f) / 0.35f);
                float hl = Math.Max(0.02f, open);
                V3 h1 = BodyKit.PatchPoint(c, r, rot, ix - 0.22f, iy + 0.26f, 0.0012f * _hs, out V3 hn);
                _k.Ellipsoid(h1, new V3(0.0046f, 0.0052f, 0.0014f) * (_hs * hl), rot, CharacterPalette.Highlight, 5, 3, Bone.Head);
                if (Lod == 0)
                {
                    V3 h2 = BodyKit.PatchPoint(c, r, rot, ix + 0.2f, iy - 0.24f, 0.0012f * _hs, out hn);
                    _k.Ellipsoid(h2, new V3(0.0022f, 0.0022f, 0.0009f) * (_hs * hl), rot, CharacterPalette.Highlight, 4, 3, Bone.Head);
                }
                if (Lod == 0) Lids(s, c, r, rot);
                else LashLine(c, r, rot);
            }

            /// <summary>LOD1 eyes: just the upper lash line, which shapes the eye.</summary>
            private void LashLine(V3 c, V3 r, Quat rot)
            {
                Mat(MaterialChannel.Skin);
                V3 rl = new V3(r.X * 1.07f, r.Y * 1.05f, r.Z * 1.12f) + new V3(0.0008f, 0.0008f, 0.0008f);
                int cols = 5;
                _k.BeginGrid(2, cols);
                for (int j = 0; j < cols; j++)
                {
                    float u = -1.1f + 2.2f * j / (cols - 1);
                    float edge = LidEdge(u, _expr.Lid, _expr.LowerLid);
                    for (int i = 0; i < 2; i++)
                    {
                        float top = MathF.Sqrt(Math.Max(0f, 1f - Math.Min(1f, u * u)));
                        float v = i == 0 ? edge : top + 0.05f;
                        _k.GridSet(i, j, cols, BodyKit.PatchPoint(c, rl, rot, u, v, 0f, out V3 n), CharacterPalette.Lashes, Bone.Head, Bone.Head, 1f);
                    }
                }
                _k.EndGrid(2, cols, false, c, V3.Zero);
            }

            /// <summary>Upper lid (skin with a dark lash line at its edge) and lower lid over the eye, positioned by the
            /// expression's closure. The lids are sheets turning about the eye's horizontal axis (<see cref="LidPoint"/>):
            /// rows climb in angle and columns run across, so no face state folds a triangle over and the winding never
            /// depends on the closure (blend shapes share one topology).</summary>
            private void Lids(int s, V3 c, V3 r, Quat rot)
            {
                Mat(MaterialChannel.Skin);
                V3 rl = new V3(r.X * 1.07f, r.Y * 1.06f, r.Z * 1.2f) + new V3(0.0008f, 0.0008f, 0.0008f);
                int cols = Lod == 0 ? 9 : 7, rows = Lod == 0 ? 6 : 3;
                float lash = (_r.Figure != 0 ? 0.17f : 0.12f) * (Lod == 0 ? 1f : 1.3f);
                float close = _expr.Lid;
                int firstLid = _k.M.VertexCount;
                // Upper lid: rows from the edge (row 0, lash) up over the top of the eye.
                _k.BeginGrid(rows, cols);
                for (int j = 0; j < cols; j++)
                {
                    float u = -1.1f + 2.2f * j / (cols - 1);
                    float edge = LidAngle(u, LidEdge(u, close, _expr.LowerLid));
                    float top = Math.Max(LidTopAngle, edge + lash + 0.3f);
                    for (int i = 0; i < rows; i++)
                    {
                        float phi;
                        if (i == 0) phi = edge;
                        else
                        {
                            // Denser toward the top, where the lid curves over the eye.
                            float t = MathF.Sin((i - 1) / (float)(rows - 2) * 1.5707963f);
                            phi = edge + lash + (top - (edge + lash)) * t;
                        }
                        _k.GridSet(i, j, cols, LidPoint(c, rl, rot, u, phi), _skin, Bone.Head, Bone.Head, 1f);
                    }
                }
                var rowCols = new uint[rows - 1];
                for (int i = 0; i < rowCols.Length; i++) rowCols[i] = i == 0 ? CharacterPalette.Lashes : _skin;
                _k.Channel = MaterialChannel.Skin;
                _k.EndGridColours(rows, cols, false, c, V3.Zero, null, rowCols);
                // Lower lid: from under the eye up to its edge, with a soft line; a hair inside the upper lid, which
                // closes over it.
                int lrows = Lod == 0 ? 4 : 2;
                V3 rlLow = new V3(rl.X * 0.995f, rl.Y * 0.995f, rl.Z * 0.985f);
                _k.BeginGrid(lrows, cols);
                float raise = _expr.LowerLid;
                for (int j = 0; j < cols; j++)
                {
                    float u = -1.1f + 2.2f * j / (cols - 1);
                    float top = MathF.Sqrt(Math.Max(0f, 1f - Math.Min(1f, u * u)));
                    float edge = Math.Max(LidAngle(u, -top * (0.86f - 0.75f * raise)), -LidTopAngle + 0.3f);
                    for (int i = 0; i < lrows; i++)
                    {
                        // Denser toward the bottom, where the lid curves under the eye.
                        float t = 1f - MathF.Cos(i / (float)(lrows - 1) * 1.5707963f);
                        float phi = -LidTopAngle + (edge + LidTopAngle) * t;
                        _k.GridSet(i, j, cols, LidPoint(c, rlLow, rot, u, phi), _skin, Bone.Head, Bone.Head, 1f);
                    }
                }
                var lowCols = new uint[lrows - 1];
                for (int i = 0; i < lowCols.Length; i++) lowCols[i] = i == lowCols.Length - 1 && Lod == 0 ? CharacterPalette.Mix(_skin, _skinShade, 0.6f) : _skin;
                _k.EndGridColours(lrows, cols, false, c, V3.Zero, null, lowCols);
                SoftenLidNormals(firstLid, rot * V3.Forward);
                if (_r.Figure != 0 && Lod == 0)
                {
                    // A small lash flick at the outer corner.
                    V3 a = BodyKit.PatchPoint(c, rl, rot, s * 0.97f, LidEdge(0.97f, close, _expr.LowerLid) + 0.08f, 0.0004f, out V3 n0);
                    V3 b = a + (rot * new V3(s * 0.009f, 0.006f, -0.002f)) * _hs;
                    _k.Channel = MaterialChannel.Hair;
                    _k.Cone(a, b, 0.0022f * _hs, 0.0004f * _hs, CharacterPalette.Lashes, 4, Bone.Head);
                }
            }

            /// <summary>How far round the top (and the bottom) of the eye the lids reach, in radians from the front.</summary>
            private const float LidTopAngle = 1.75f;

            /// <summary>Half the height of the eye's vertical section at <paramref name="u"/> across it (never quite zero,
            /// so the lid's corner columns keep their row order).</summary>
            private static float LidSection(float u)
            {
                float uc = Math.Min(1f, u * u);
                return MathF.Sqrt(Math.Max(0.05f, 1f - 0.995f * uc));
            }

            /// <summary>The lid angle that puts a lid edge at height <paramref name="v"/> (eye radii) at <paramref name="u"/>.</summary>
            private static float LidAngle(float u, float v)
            {
                return MathF.Asin(CharMath.Clamp(v / LidSection(u), -0.99f, 0.99f));
            }

            /// <summary>A point of a lid sheet: across the eye at <paramref name="u"/> and turned <paramref name="phi"/>
            /// radians up from the front about the eye's horizontal axis.</summary>
            private static V3 LidPoint(V3 c, V3 r, Quat rot, float u, float phi)
            {
                float sec = LidSection(u);
                return c + rot * new V3(u * r.X, sec * MathF.Sin(phi) * r.Y, sec * MathF.Cos(phi) * r.Z);
            }

            /// <summary>Height (in eye radii) of the upper lid's edge at <paramref name="u"/> across the eye for a closure
            /// (0 open: only the top sliver covered; 1 shut: down onto the lower lid, raised by
            /// <paramref name="raise"/>, with a little overlap so no eye white shows between them).</summary>
            private static float LidEdge(float u, float close, float raise)
            {
                float top = MathF.Sqrt(Math.Max(0f, 1f - Math.Min(1f, u * u)));
                float open = top * 0.8f - 0.05f, shut = -top * (0.86f - 0.75f * raise) - 0.06f;
                return open + (shut - open) * close;
            }

            /// <summary>Bends the normals of a lid sheet toward the face's forward direction so the lids shade like the
            /// skin around the eye (no dark pouch where a sheet turns under or over the eyeball).</summary>
            private void SoftenLidNormals(int first, V3 forward)
            {
                float[] n = _k.M.Normals;
                for (int v = first; v < _k.M.VertexCount; v++)
                {
                    var nv = new V3(n[v * 3], n[v * 3 + 1], n[v * 3 + 2]);
                    V3 b = (nv + forward * 1.2f).Normalized;
                    n[v * 3] = b.X;
                    n[v * 3 + 1] = b.Y;
                    n[v * 3 + 2] = b.Z;
                }
            }

            private void Brows()
            {
                Mat(MaterialChannel.Hair);
                uint brow = _r.HairColour == 6 || _r.HairColour == 5 ? CharacterPalette.Shade(_hair, 0.85f) : CharacterPalette.Shade(_hair, 1.05f);
                if (_r.Hair == CharacterRecipe.HairShaved) brow = CharacterPalette.Mix(_skin, 0x2A2422u, 0.75f);
                int n = Lod == 0 ? 7 : 4;
                V3[] path = _k.PathBuffer(n, out float[] rx, out float[] rz);
                float thick = (_r.Figure != 0 ? 0.0042f : 0.0055f) * (0.85f + 0.3f * _v1) * _hs;
                for (int s = -1; s <= 1; s += 2)
                {
                    float ey = EyeY;
                    var inner = new V3(s * 0.027f * _hs, ey + (0.044f + _expr.BrowInner) * _hs, 0f);
                    var mid = new V3(s * 0.058f * _hs, ey + (0.058f + _expr.BrowRaise) * _hs, 0f);
                    var outer = new V3(s * 0.088f * _hs, ey + (0.043f + _expr.BrowRaise * 0.6f) * _hs, 0f);
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)(n - 1);
                        float u = 1f - t;
                        V3 q = inner * (u * u) + mid * (2f * u * t) + outer * (t * t);
                        // Paths run from the wearer's right to left so ring frames match on both sides.
                        path[s < 0 ? n - 1 - i : i] = FacePoint(q.X, q.Y, 0.0012f * _hs, out V3 nrm);
                        float w = thick * (1.05f - 0.45f * t);
                        rx[s < 0 ? n - 1 - i : i] = w;
                        rz[s < 0 ? n - 1 - i : i] = 0.0022f * _hs;
                    }
                    _k.Sweep(path, n, rx, rz, V3.Up, brow, _k.Seg(6, 4), Bone.Head, Lod == 0 ? LoftCap.Round : LoftCap.Flat, Lod == 0 ? LoftCap.Round : LoftCap.Flat);
                }
            }

            private void Nose()
            {
                Mat(MaterialChannel.Skin);
                uint nose = CharacterPalette.Mix(_skin, _skinShade, 0.12f);
                float big = 0.85f + 0.25f * _v2;
                // A soft rounded nose: a bulb at the tip that runs up into the face, with two nostril wings.
                V3 tip = FacePoint(0f, NoseY, 0.002f * _hs, out V3 n);
                _k.Ellipsoid(tip + new V3(0f, 0.006f * _hs, -0.002f * _hs), new V3(0.0145f, 0.022f, 0.0125f) * (_hs * big), Quat.Euler(14f, 0f, 0f), nose,
                             _k.Seg(9, 6), _k.Seg(7, 4), Bone.Head);
                if (Lod > 0) return;
                for (int s = -1; s <= 1; s += 2)
                {
                    V3 wing = FacePoint(s * 0.0125f * _hs * big, NoseY - 0.006f * _hs, 0.0003f * _hs, out V3 wn);
                    _k.Ellipsoid(wing, new V3(0.009f, 0.0075f, 0.008f) * _hs * big, Quat.Euler(0f, s * 25f, 0f), nose, 6, 4, Bone.Head);
                }
                // Nostril shadows under the tip.
                for (int s = -1; s <= 1; s += 2)
                    FaceDisc(s * 0.0075f * _hs, NoseY - 0.0135f * _hs, 0.004f * _hs, 0.0022f * _hs, 0.0075f * _hs, CharacterPalette.Mix(_skinShade, 0x2A1810u, 0.35f), 1, 8, s * 20f);
            }

            /// <summary>The mouth: an interior shape between an upper and a lower curve (a closed smile is a thin
            /// crescent, a laugh a D, a puff an O), upper and lower teeth strips, a tongue and the lower lip. All parts
            /// exist in every state.</summary>
            private void Mouth()
            {
                Mat(MaterialChannel.Skin);
                float w = _expr.MouthW * _hs * (0.92f + 0.16f * _v3);
                int cols = Lod == 0 ? 11 : 5;
                float my = MouthY;
                for (int j = 0; j < cols; j++)
                {
                    float u = -1f + 2f * j / (cols - 1);
                    float side = MathF.Sqrt(Math.Max(0f, 1f - u * u));
                    float up, low;
                    if (_expr.Round > 0.5f)
                    {
                        up = 0.5f * _expr.Open * side;
                        low = -0.5f * _expr.Open * side;
                    }
                    else
                    {
                        up = _expr.Corner * u * u * _hs + 0.0012f * _hs;
                        float depth = _expr.Open * _hs;
                        // A flatter bottom for a wide grin, a round one for a laugh.
                        float shape = _expr.TeethBottom > 0f ? MathF.Pow(Math.Max(0f, 1f - u * u * u * u), 0.5f) : side;
                        low = up - depth * shape - 0.0006f * _hs;
                    }
                    _fx[j] = u * w;
                    _fyA[j] = my + low;
                    _fyB[j] = my + up;
                }
                // Interior (dark), lower lip (lip tint) under it.
                FaceBand(cols, Lod == 0 ? 3 : 2, 0.0008f * _hs, CharacterPalette.MouthInside, _fx, _fyA, _fyB);
                if (Lod > 0) return;
                for (int j = 0; j < cols; j++)
                {
                    float u = -1f + 2f * j / (cols - 1);
                    float side = MathF.Sqrt(Math.Max(0f, 1f - u * u));
                    _fyB[j] = _fyA[j] - 0.0006f * _hs;
                    _fyA[j] = _fyB[j] - 0.0042f * _hs * side * (0.8f + 0.4f * _v0);
                }
                FaceBand(cols, 2, 0.0006f * _hs, CharacterPalette.Mix(_skin, _lip, 0.55f), _fx, _fyA, _fyB);
                // Upper teeth just under the upper curve; lower teeth on the lower curve; the tongue at the bottom.
                for (int j = 0; j < cols; j++)
                {
                    float u = -1f + 2f * j / (cols - 1);
                    float side = MathF.Sqrt(Math.Max(0f, 1f - u * u));
                    float up = _expr.Round > 0.5f ? 0.5f * _expr.Open * side : _expr.Corner * u * u * _hs + 0.0012f * _hs;
                    float th = _expr.TeethTop * _hs * MathF.Sqrt(side);
                    _fx[j] = u * w * 0.9f;
                    _fyB[j] = my + up - 0.0005f * _hs;
                    _fyA[j] = _fyB[j] - th;
                }
                FaceBand(cols, 2, 0.0013f * _hs, CharacterPalette.Teeth, _fx, _fyA, _fyB);
                for (int j = 0; j < cols; j++)
                {
                    float u = -1f + 2f * j / (cols - 1);
                    float side = MathF.Sqrt(Math.Max(0f, 1f - u * u));
                    float up = _expr.Corner * u * u * _hs + 0.0012f * _hs;
                    float flat = MathF.Pow(Math.Max(0f, 1f - u * u * u * u), 0.5f);
                    float low = up - _expr.Open * _hs * flat - 0.0006f * _hs;
                    float th = _expr.TeethBottom * _hs * MathF.Sqrt(side);
                    _fx[j] = u * w * 0.85f;
                    _fyA[j] = my + low + 0.0005f * _hs;
                    _fyB[j] = _fyA[j] + th;
                }
                FaceBand(cols, 2, 0.0013f * _hs, CharacterPalette.Teeth, _fx, _fyA, _fyB);
                int tc = 7;
                for (int j = 0; j < tc; j++)
                {
                    float u = -1f + 2f * j / (tc - 1);
                    float side = MathF.Sqrt(Math.Max(0f, 1f - u * u));
                    float bottom = -_expr.Open * _hs * MathF.Sqrt(Math.Max(0f, 1f - u * u * 0.3f)) + 0.0006f * _hs;
                    float up0 = _expr.Corner * (u * 0.55f) * (u * 0.55f) * _hs + 0.0012f * _hs;
                    _fx[j] = u * w * 0.55f;
                    _fyA[j] = my + up0 + bottom;
                    _fyB[j] = _fyA[j] + _expr.Tongue * _expr.Open * _hs * side;
                }
                FaceBand(tc, 2, 0.0011f * _hs, CharacterPalette.Tongue, _fx, _fyA, _fyB);
                // Smile creases at the corners (soft shade).
                for (int s = -1; s <= 1; s += 2)
                    FaceDisc(s * (w + 0.003f * _hs), my + _expr.Corner * _hs + 0.0006f * _hs, 0.0026f * _hs, 0.0012f * _hs, 0.0005f * _hs,
                             CharacterPalette.Mix(_skin, _skinShade, 0.7f), 1, 6, s * -40f);
            }

            /// <summary>A band on the face between a lower and an upper curve sampled at <paramref name="cols"/> x
            /// positions (rows run upward and columns rightward, so the winding never flips when a band collapses).</summary>
            private void FaceBand(int cols, int rows, float lift, uint rgb, float[] xs, float[] yLow, float[] yHigh)
            {
                _k.BeginGrid(rows, cols);
                for (int i = 0; i < rows; i++)
                {
                    float t = i / (float)(rows - 1);
                    for (int j = 0; j < cols; j++)
                    {
                        float y = yLow[j] + (yHigh[j] - yLow[j]) * t;
                        V3 p = FacePoint(xs[j], y, lift, out V3 n);
                        _k.GridSet(i, j, cols, p, rgb, Bone.Head, Bone.Head, 1f);
                    }
                }
                _k.EndGrid(rows, cols, false, _hc, V3.Zero);
            }

            /// <summary>An elliptic disc lying on the face (blush, bindi, tika, tilak lines, nostril shadows).</summary>
            private void FaceDisc(float cx, float cy, float ax, float ay, float lift, uint rgb, int rings, int seg, float rotDeg = 0f)
            {
                float cr = MathF.Cos(rotDeg * Quat.Deg2Rad), sr = MathF.Sin(rotDeg * Quat.Deg2Rad);
                V3 p0 = FacePoint(cx, cy, lift, out V3 n0);
                int centre = _k.Vertex(p0, n0, rgb, Bone.Head, Bone.Head, 1f);
                int prev = -1;
                for (int k = 1; k <= rings; k++)
                {
                    float f = k / (float)rings;
                    int start = _k.M.VertexCount;
                    for (int j = 0; j < seg; j++)
                    {
                        float a = 6.28318530718f * j / seg;
                        float dx = ax * f * MathF.Cos(a), dy = ay * f * MathF.Sin(a);
                        V3 p = FacePoint(cx + dx * cr - dy * sr, cy + dx * sr + dy * cr, lift, out V3 n);
                        _k.Vertex(p, n, rgb, Bone.Head, Bone.Head, 1f);
                    }
                    for (int j = 0; j < seg; j++)
                    {
                        int j1 = (j + 1) % seg;
                        if (prev < 0) _k.Tri(centre, start + j, start + j1);
                        else
                        {
                            _k.Tri(prev + j, start + j, start + j1);
                            _k.Tri(prev + j, start + j1, prev + j1);
                        }
                    }
                    prev = start;
                }
            }

            private void Cheeks()
            {
                Mat(MaterialChannel.Skin);
                uint blush = CharacterPalette.Mix(_skin, _blush, _expr.Cheek > 0.012f ? 0.62f : 0.42f);
                for (int s = -1; s <= 1; s += 2)
                    FaceDisc(s * 0.098f * _hs, -0.074f * _hs, 0.022f * _hs, 0.012f * _hs, 0.0007f * _hs, blush, 2, 12);
            }

            // ----- Ears ---------------------------------------------------------------------------------------------

            private void Ears()
            {
                if (Lod >= 2) return;
                Mat(MaterialChannel.Skin);
                float ny = -0.17f;
                for (int s = -1; s <= 1; s += 2)
                {
                    float x = _hr.X * JawX(ny) - 0.004f * _hs;
                    var c = new V3(_hc.X + s * x, _hc.Y + ny * _hr.Y, _hc.Z - 0.012f * _hs);
                    var r = new V3(0.029f, 0.044f, 0.015f) * _hs;
                    Quat rot = Quat.Euler(0f, s * 72f, s * -6f);
                    _k.Ellipsoid(c, r, rot, _skin, _k.Seg(10, 6), _k.Seg(6, 5), Bone.Head);
                    if (Lod > 0) continue;
                    // The inner fold (concha) and the helix rim read from the side.
                    var cols = new[] { CharacterPalette.Mix(_skinShade, _blush, 0.3f), CharacterPalette.Mix(_skin, _skinShade, 0.6f) };
                    _k.EllipsoidPatch(c, r, rot, -0.06f, -0.02f, 0.62f, 0.66f, 0.0012f * _hs, cols, new[] { 0.6f, 1f }, 1, 10, Bone.Head);
                    V3 lobe = c + rot * new V3(0.006f, -0.034f, 0.002f) * _hs;
                    _k.Sphere(lobe, 0.011f * _hs, _skin, 6, 4, Bone.Head);
                }
            }
        }
    }
}
