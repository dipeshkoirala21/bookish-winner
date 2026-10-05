using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ghumante.Vehicles.Visuals
{
    /// <summary>
    /// Builds small cartoon meshes from primitives (boxes, cylinders, spheres, capsules) with <b>vertex colours</b>, so a
    /// whole model draws with one <c>Ghumante/ToonLit</c> material (the shader's albedo is the vertex colour, ARCHITECTURE.md
    /// 8) and stays SRP-Batcher friendly. Boxes and cylinder caps have flat normals, round parts smooth ones. Used at
    /// load time only (it allocates); the result is a regular <see cref="Mesh"/> the caller owns and destroys.
    /// </summary>
    public sealed class ToonMeshBuilder
    {
        private readonly List<Vector3> _vertices = new List<Vector3>(512);
        private readonly List<Vector3> _normals = new List<Vector3>(512);
        private readonly List<Color32> _colors = new List<Color32>(512);
        private readonly List<int> _indices = new List<int>(1536);

        public int VertexCount
        {
            get { return _vertices.Count; }
        }

        public void Clear()
        {
            _vertices.Clear();
            _normals.Clear();
            _colors.Clear();
            _indices.Clear();
        }

        /// <summary>A box of <paramref name="size"/> centred on <paramref name="center"/>, rotated by <paramref name="rotation"/>.</summary>
        public ToonMeshBuilder Box(Vector3 center, Vector3 size, Color32 color, Quaternion rotation)
        {
            Vector3 h = size * 0.5f;
            // +X, -X, +Y, -Y, +Z, -Z faces: normal, then two axes spanning the face (counter-clockwise seen from outside).
            Face(center, rotation, Vector3.right, Vector3.forward, Vector3.up, h.x, h.z, h.y, color);
            Face(center, rotation, Vector3.left, Vector3.back, Vector3.up, h.x, h.z, h.y, color);
            Face(center, rotation, Vector3.up, Vector3.right, Vector3.forward, h.y, h.x, h.z, color);
            Face(center, rotation, Vector3.down, Vector3.left, Vector3.forward, h.y, h.x, h.z, color);
            Face(center, rotation, Vector3.forward, Vector3.left, Vector3.up, h.z, h.x, h.y, color);
            Face(center, rotation, Vector3.back, Vector3.right, Vector3.up, h.z, h.x, h.y, color);
            return this;
        }

        public ToonMeshBuilder Box(Vector3 center, Vector3 size, Color32 color)
        {
            return Box(center, size, color, Quaternion.identity);
        }

        /// <summary>A cylinder from <paramref name="a"/> to <paramref name="b"/> with flat end caps.</summary>
        public ToonMeshBuilder Cylinder(Vector3 a, Vector3 b, float radius, Color32 color, int segments = 14)
        {
            Vector3 axis = b - a;
            float length = axis.magnitude;
            if (length < 1e-5f || radius <= 0f) return this;
            axis /= length;
            Vector3 u, w;
            Basis(axis, out u, out w);
            segments = Mathf.Max(3, segments);
            // Side: smooth normals.
            int start = _vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float t = i * (2f * Mathf.PI / segments);
                Vector3 n = u * Mathf.Cos(t) + w * Mathf.Sin(t);
                Add(a + n * radius, n, color);
                Add(b + n * radius, n, color);
            }
            for (int i = 0; i < segments; i++)
            {
                int k = start + i * 2;
                // Cross(u, w) == axis, so the angle grows clockwise seen from the b end: wind accordingly.
                Tri(k, k + 3, k + 1);
                Tri(k, k + 2, k + 3);
            }
            Cap(a, -axis, u, w, radius, segments, color);
            Cap(b, axis, u, w, radius, segments, color);
            return this;
        }

        /// <summary>An ellipsoid: a unit sphere scaled by <paramref name="radii"/>, rotated, at <paramref name="center"/>.</summary>
        public ToonMeshBuilder Sphere(Vector3 center, Vector3 radii, Color32 color, Quaternion rotation, int rings = 8, int segments = 14)
        {
            rings = Mathf.Max(2, rings);
            segments = Mathf.Max(3, segments);
            int start = _vertices.Count;
            var inv = new Vector3(1f / Mathf.Max(radii.x, 1e-5f), 1f / Mathf.Max(radii.y, 1e-5f), 1f / Mathf.Max(radii.z, 1e-5f));
            for (int r = 0; r <= rings; r++)
            {
                float phi = Mathf.PI * r / rings;
                float y = Mathf.Cos(phi), s = Mathf.Sin(phi);
                for (int i = 0; i <= segments; i++)
                {
                    float t = i * (2f * Mathf.PI / segments);
                    var unit = new Vector3(s * Mathf.Cos(t), y, s * Mathf.Sin(t));
                    Vector3 p = Vector3.Scale(unit, radii);
                    Vector3 n = Vector3.Scale(unit, inv).normalized; // ellipsoid normal
                    Add(center + rotation * p, rotation * n, color);
                }
            }
            int row = segments + 1;
            for (int r = 0; r < rings; r++)
            {
                for (int i = 0; i < segments; i++)
                {
                    int k = start + r * row + i;
                    Tri(k, k + 1, k + row + 1);
                    Tri(k, k + row + 1, k + row);
                }
            }
            return this;
        }

        public ToonMeshBuilder Sphere(Vector3 center, float radius, Color32 color, int rings = 8, int segments = 14)
        {
            return Sphere(center, new Vector3(radius, radius, radius), color, Quaternion.identity, rings, segments);
        }

        /// <summary>A capsule (a limb) from <paramref name="a"/> to <paramref name="b"/>: a cylinder with round ends.</summary>
        public ToonMeshBuilder Capsule(Vector3 a, Vector3 b, float radius, Color32 color, int segments = 10)
        {
            Cylinder(a, b, radius, color, segments);
            Sphere(a, radius, color, 6, segments);
            Sphere(b, radius, color, 6, segments);
            return this;
        }

        /// <summary>A new mesh with everything added so far (positions, normals, vertex colours, one submesh).</summary>
        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (_vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetColors(_colors);
            mesh.SetTriangles(_indices, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private void Face(Vector3 center, Quaternion rotation, Vector3 normal, Vector3 axisA, Vector3 axisB,
                          float depth, float halfA, float halfB, Color32 color)
        {
            Vector3 n = rotation * normal;
            Vector3 c = normal * depth;
            Vector3 a = axisA * halfA, b = axisB * halfB;
            int k = _vertices.Count;
            Add(center + rotation * (c - a - b), n, color);
            Add(center + rotation * (c + a - b), n, color);
            Add(center + rotation * (c + a + b), n, color);
            Add(center + rotation * (c - a + b), n, color);
            // Unity is left-handed with clockwise front faces seen from outside.
            Tri(k, k + 2, k + 1);
            Tri(k, k + 3, k + 2);
        }

        private void Cap(Vector3 center, Vector3 normal, Vector3 u, Vector3 w, float radius, int segments, Color32 color)
        {
            int middle = _vertices.Count;
            Add(center, normal, color);
            for (int i = 0; i <= segments; i++)
            {
                float t = i * (2f * Mathf.PI / segments);
                Add(center + (u * Mathf.Cos(t) + w * Mathf.Sin(t)) * radius, normal, color);
            }
            // Winding so the cap faces along its normal (clockwise seen from outside).
            bool alongAxis = Vector3.Dot(Vector3.Cross(u, w), normal) > 0f;
            for (int i = 0; i < segments; i++)
            {
                if (alongAxis) Tri(middle, middle + 1 + i, middle + 2 + i);
                else Tri(middle, middle + 2 + i, middle + 1 + i);
            }
        }

        private static void Basis(Vector3 axis, out Vector3 u, out Vector3 w)
        {
            Vector3 helper = Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right;
            u = Vector3.Cross(axis, helper).normalized;
            w = Vector3.Cross(axis, u);
        }

        private void Add(Vector3 p, Vector3 n, Color32 c)
        {
            _vertices.Add(p);
            _normals.Add(n);
            _colors.Add(c);
        }

        private void Tri(int a, int b, int c)
        {
            _indices.Add(a);
            _indices.Add(b);
            _indices.Add(c);
        }

        /// <summary>Destroys a mesh made by <see cref="ToMesh"/> (null-safe; immediate in edit mode).</summary>
        public static void Destroy(Mesh mesh)
        {
            if (mesh == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(mesh);
            else UnityEngine.Object.DestroyImmediate(mesh);
        }

        /// <summary>Thrown away when no material is available (should not happen with Project Setup run).</summary>
        internal static void RequireMaterial(Material material)
        {
            if (material == null) throw new ArgumentNullException(nameof(material), "a Ghumante/ToonLit material is required");
        }
    }
}
