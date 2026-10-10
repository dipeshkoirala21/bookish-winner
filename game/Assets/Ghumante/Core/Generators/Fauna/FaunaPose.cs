using System;

namespace Ghumante.Core.Generators.Fauna
{
    /// <summary>
    /// A pose of a fauna rig: per bone a local rotation about the bone's bind pivot (in its parent's frame) and a
    /// uniform scale (0 hides a part: the folded wings in flight, the spread wings at rest), plus a root offset and
    /// root rotation (lying down, crouching, banking). The identity pose is the bind pose. Plain arrays, reused
    /// across frames (no allocation after construction).
    /// </summary>
    public sealed class FaunaPose
    {
        /// <summary>Local rotation of each bone (<see cref="FaunaRig.BoneCount"/> entries).</summary>
        public readonly FRot[] Rot = new FRot[FaunaRig.BoneCount];

        /// <summary>Uniform scale of each bone about its pivot (1 = bind size; 0 hides the bone's vertices).</summary>
        public readonly float[] Scale = new float[FaunaRig.BoneCount];

        /// <summary>Translation of the root (model space, metres), applied after <see cref="RootRot"/>.</summary>
        public Fv3 RootOffset;

        /// <summary>Rotation of the whole animal about the root pivot.</summary>
        public FRot RootRot;

        public FaunaPose()
        {
            Reset();
        }

        /// <summary>Back to the bind pose.</summary>
        public void Reset()
        {
            for (int i = 0; i < FaunaRig.BoneCount; i++)
            {
                Rot[i] = FRot.Identity;
                Scale[i] = 1f;
            }
            RootOffset = Fv3.Zero;
            RootRot = FRot.Identity;
        }

        /// <summary>Sets a bone's local rotation from Euler angles in degrees (pitch about X: positive tips +Z down;
        /// yaw about Y: positive turns +Z towards +X; roll about Z: positive lifts +X).</summary>
        public void Set(FaunaBone b, float pitchDeg, float yawDeg, float rollDeg)
        {
            Rot[(int)b] = FRot.Euler(pitchDeg * FMath.Deg, yawDeg * FMath.Deg, rollDeg * FMath.Deg);
        }

        /// <summary>Adds a rotation (Euler degrees) after the bone's current local rotation.</summary>
        public void Add(FaunaBone b, float pitchDeg, float yawDeg, float rollDeg)
        {
            if (pitchDeg == 0f && yawDeg == 0f && rollDeg == 0f) return;
            Rot[(int)b] = Rot[(int)b] * FRot.Euler(pitchDeg * FMath.Deg, yawDeg * FMath.Deg, rollDeg * FMath.Deg);
        }

        /// <summary>
        /// Sets the left bone <paramref name="left"/> and its right twin with mirrored yaw and roll (the same pitch),
        /// so a symmetric pose is one call.
        /// </summary>
        public void SetPair(FaunaBone left, float pitchDeg, float yawDeg, float rollDeg)
        {
            Set(left, pitchDeg, yawDeg, rollDeg);
            FaunaBone r = FaunaRig.Mirror(left);
            if (r != left) Set(r, pitchDeg, -yawDeg, -rollDeg);
        }

        /// <summary>Sets the root rotation from Euler degrees.</summary>
        public void SetRoot(float pitchDeg, float yawDeg, float rollDeg)
        {
            RootRot = FRot.Euler(pitchDeg * FMath.Deg, yawDeg * FMath.Deg, rollDeg * FMath.Deg);
        }

        /// <summary>Copies another pose.</summary>
        public void CopyFrom(FaunaPose o)
        {
            Array.Copy(o.Rot, Rot, Rot.Length);
            Array.Copy(o.Scale, Scale, Scale.Length);
            RootOffset = o.RootOffset;
            RootRot = o.RootRot;
        }
    }

    /// <summary>A rigid transform with uniform scale: x → <see cref="R"/>·(<see cref="S"/>·x) + <see cref="T"/>.</summary>
    public struct FXform
    {
        public FRot R;
        public float S;
        public Fv3 T;

        public static readonly FXform Identity = new FXform { R = FRot.Identity, S = 1f, T = Fv3.Zero };

        public Fv3 Point(Fv3 p)
        {
            return R * (p * S) + T;
        }

        public Fv3 Vector(Fv3 v)
        {
            return R * v;
        }
    }

    /// <summary>
    /// Poses a <see cref="FaunaMesh"/>: bone transforms from a <see cref="FaunaPose"/> (each bone rotates and scales
    /// about its bind pivot inside its parent, the root about the ground point), then linear-blend skinning with the
    /// mesh's two weights per vertex. Used to bake keyframe meshes, to CPU-skin the near animals and for previews.
    /// Allocation-free after construction.
    /// </summary>
    public sealed class FaunaSkinner
    {
        /// <summary>World (model-space) transform of every bone of the last <see cref="Solve"/>.</summary>
        public readonly FXform[] Bones = new FXform[FaunaRig.BoneCount];

        /// <summary>Computes <see cref="Bones"/> for <paramref name="pose"/> on the pivots of <paramref name="mesh"/>.</summary>
        public void Solve(FaunaMesh mesh, FaunaPose pose)
        {
            Fv3[] piv = mesh.Pivot;
            for (int b = 0; b < FaunaRig.BoneCount; b++)
            {
                int p = FaunaRig.Parent[b];
                FXform parent;
                if (p < 0)
                {
                    parent = new FXform { R = pose.RootRot, S = 1f, T = pose.RootOffset };
                }
                else
                {
                    parent = Bones[p];
                }
                // Local: x → pivot + Rb·(sb·(x − pivot)).
                FRot rb = pose.Rot[b];
                float sb = pose.Scale[b];
                Fv3 pv = piv[b];
                Fv3 localT = pv - rb * (pv * sb);
                var w = new FXform
                {
                    R = parent.R * rb,
                    S = parent.S * sb,
                    T = parent.R * (localT * parent.S) + parent.T,
                };
                Bones[b] = w;
            }
        }

        /// <summary>
        /// Skins the bind mesh with the last <see cref="Solve"/> into <paramref name="positions"/> and
        /// <paramref name="normals"/> (xyz per vertex; at least 3 × vertex count each).
        /// </summary>
        public void Skin(FaunaMesh mesh, float[] positions, float[] normals)
        {
            float[] sp = mesh.Mesh.Positions, sn = mesh.Mesh.Normals;
            int n = mesh.VertexCount;
            for (int v = 0; v < n; v++)
            {
                int p = 3 * v;
                var x = new Fv3(sp[p], sp[p + 1], sp[p + 2]);
                var nn = new Fv3(sn[p], sn[p + 1], sn[p + 2]);
                FXform a = Bones[mesh.Bone0[v]];
                float w = mesh.Weight0[v];
                Fv3 px, pn;
                if (w >= 0.999f)
                {
                    px = a.Point(x);
                    pn = a.Vector(nn);
                }
                else
                {
                    FXform b = Bones[mesh.Bone1[v]];
                    px = a.Point(x) * w + b.Point(x) * (1f - w);
                    pn = a.Vector(nn) * w + b.Vector(nn) * (1f - w);
                }
                float l = pn.Length;
                if (l > 1e-8f) pn = pn * (1f / l);
                positions[p] = px.X;
                positions[p + 1] = px.Y;
                positions[p + 2] = px.Z;
                normals[p] = pn.X;
                normals[p + 1] = pn.Y;
                normals[p + 2] = pn.Z;
            }
        }

        /// <summary>
        /// Writes the posed mesh into <paramref name="dst"/> (cleared first): skinned positions and normals, the bind
        /// colours, UV0 (channel, AO) and indices. Triangles whose three vertices all sit on hidden bones (scale 0)
        /// are dropped, so a baked keyframe carries no degenerate wings.
        /// </summary>
        public void Bake(FaunaMesh mesh, FaunaPose pose, Ghumante.Core.Meshing.MeshData dst)
        {
            Solve(mesh, pose);
            dst.Clear();
            int n = mesh.VertexCount;
            dst.Reserve(n, mesh.Mesh.IndexCount);
            Skin(mesh, dst.Positions, dst.Normals);
            Array.Copy(mesh.Mesh.Colors, dst.Colors, 4 * n);
            Array.Copy(mesh.Mesh.Uv0, dst.Uv0, 2 * n);
            dst.VertexCount = n;
            dst.HasUv0 = mesh.Mesh.HasUv0;
            int[] src = mesh.Mesh.Indices;
            for (int i = 0; i < mesh.Mesh.IndexCount; i += 3)
            {
                int a = src[i], b = src[i + 1], c = src[i + 2];
                if (Hidden(mesh, pose, a) && Hidden(mesh, pose, b) && Hidden(mesh, pose, c)) continue;
                dst.AddTriangle(a, b, c);
            }
        }

        private static bool Hidden(FaunaMesh mesh, FaunaPose pose, int v)
        {
            // A vertex is hidden when the chain of its heavier bone has a zero scale somewhere.
            int b = mesh.Bone0[v];
            while (b >= 0)
            {
                if (pose.Scale[b] < 1e-4f) return true;
                b = FaunaRig.Parent[b];
            }
            return false;
        }
    }
}
