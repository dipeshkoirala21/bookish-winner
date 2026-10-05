using System;
using System.Collections.Generic;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ghumante.Vehicles.Visuals
{
    /// <summary>
    /// Uploads the procedural cartoon vehicles of <see cref="VehicleMesher"/> (W2_DESIGN 5.2) to Unity meshes and keeps
    /// one shared mesh per (variant, livery, level, rider) for traffic and parked vehicles, plus one wheel mesh per size and
    /// level, so instanced traffic draws from a handful of meshes with one <c>Ghumante/ToonLit</c> material. Shared bodies
    /// carry a blank-numbered plate; <see cref="CreateUnique"/> builds a body with its own plate number (the player's
    /// vehicle and the nearest traffic), which the caller owns and destroys. Main thread only; building allocates, so
    /// presenters warm the cache while loading (<see cref="Prewarm"/>), never per frame.
    /// </summary>
    public sealed class VehicleMeshCache : IDisposable
    {
        // Stream 0 positions, 1 normals, 2 colours (RGBA8, sRGB values: the shader linearises them).
        private static readonly VertexAttributeDescriptor[] Layout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 1),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, 2),
        };

        private const MeshUpdateFlags Flags = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontResetBoneBounds |
                                              MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds;

        private readonly Dictionary<ulong, Mesh> _bodies = new Dictionary<ulong, Mesh>();
        private readonly Dictionary<ulong, Mesh> _wheels = new Dictionary<ulong, Mesh>();
        private readonly MeshData _scratch = new MeshData(2048, 6144);

        /// <summary>Meshes held by the cache.</summary>
        public int Count
        {
            get { return _bodies.Count + _wheels.Count; }
        }

        /// <summary>The shared body of catalogue <paramref name="variant"/> (wheels included from LOD2 down).</summary>
        public Mesh Body(int variant, byte livery, VehicleLod lod, bool rider = false)
        {
            ulong key = ((ulong)(uint)variant << 24) | ((ulong)livery << 16) | ((ulong)lod << 8) | (rider ? 1UL : 0UL);
            Mesh mesh;
            if (_bodies.TryGetValue(key, out mesh) && mesh != null) return mesh;
            _scratch.Clear();
            VehicleMesher.Build(variant, livery, lod, _scratch, 0u, rider);
            mesh = Upload(_scratch, VehicleCatalog.At(variant).AssetId + "_" + lod);
            _bodies[key] = mesh;
            return mesh;
        }

        /// <summary>A wheel (axis along X, centred on the origin) for LOD0 and LOD1 bodies; sizes share meshes to the
        /// centimetre.</summary>
        public Mesh Wheel(float radius, float width, VehicleLod lod)
        {
            uint r = (uint)Mathf.Clamp(Mathf.RoundToInt(radius * 100f), 1, 0xFFFF);
            uint w = (uint)Mathf.Clamp(Mathf.RoundToInt(width * 100f), 1, 0xFFFF);
            ulong key = ((ulong)r << 32) | ((ulong)w << 8) | (byte)lod;
            Mesh mesh;
            if (_wheels.TryGetValue(key, out mesh) && mesh != null) return mesh;
            _scratch.Clear();
            VehicleMesher.BuildWheel(r * 0.01f, w * 0.01f, lod, _scratch);
            mesh = Upload(_scratch, "ghm_veh_wheel_" + lod);
            _wheels[key] = mesh;
            return mesh;
        }

        /// <summary>A body with its own plate number from <paramref name="plateSeed"/>; the caller owns (and destroys) it.</summary>
        public Mesh CreateUnique(int variant, byte livery, VehicleLod lod, uint plateSeed, bool rider = false)
        {
            _scratch.Clear();
            VehicleMesher.Build(variant, livery, lod, _scratch, plateSeed, rider);
            return Upload(_scratch, VehicleCatalog.At(variant).AssetId + "_unique");
        }

        /// <summary>Builds every livery of every catalogue entry at <paramref name="lod"/> (load time).</summary>
        public void Prewarm(VehicleLod lod)
        {
            for (int v = 0; v < VehicleCatalog.Count; v++)
            {
                VehicleCatalogEntry e = VehicleCatalog.At(v);
                for (int l = 0; l < e.LiveryCount; l++) Body(v, (byte)l, lod);
                if (lod <= VehicleLod.Lod1)
                    foreach (WheelSocket s in VehicleMesher.Wheels(e)) Wheel(s.Radius, s.Width, lod);
            }
        }

        /// <summary>Destroys every cached mesh.</summary>
        public void Dispose()
        {
            foreach (Mesh m in _bodies.Values) Destroy(m);
            foreach (Mesh m in _wheels.Values) Destroy(m);
            _bodies.Clear();
            _wheels.Clear();
        }

        private static void Destroy(Mesh m)
        {
            if (m == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(m);
            else UnityEngine.Object.DestroyImmediate(m);
        }

        /// <summary>One <see cref="MeshData"/> to a GPU-only Unity mesh through the advanced Mesh API.</summary>
        public static Mesh Upload(MeshData m, string name)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            int vc = m.VertexCount, ic = m.IndexCount;
            float minX, minY, minZ, maxX, maxY, maxZ;
            m.GetBounds(out minX, out minY, out minZ, out maxX, out maxY, out maxZ);
            var mesh = new Mesh();
            mesh.name = name;
            mesh.SetVertexBufferParams(vc, Layout);
            mesh.SetVertexBufferData(m.Positions, 0, 0, vc * 3, 0, Flags);
            mesh.SetVertexBufferData(m.Normals, 0, 0, vc * 3, 1, Flags);
            mesh.SetVertexBufferData(m.Colors, 0, 0, vc * 4, 2, Flags);
            mesh.SetIndexBufferParams(ic, vc <= 65535 ? IndexFormat.UInt16 : IndexFormat.UInt32);
            if (vc <= 65535)
            {
                var shorts = new ushort[ic];
                for (int i = 0; i < ic; i++) shorts[i] = (ushort)m.Indices[i];
                mesh.SetIndexBufferData(shorts, 0, 0, ic, Flags);
            }
            else mesh.SetIndexBufferData(m.Indices, 0, 0, ic, Flags);
            var bounds = new Bounds(new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f),
                                    new Vector3(maxX - minX, maxY - minY, maxZ - minZ));
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, ic, MeshTopology.Triangles)
            {
                firstVertex = 0,
                vertexCount = vc,
                bounds = bounds,
            }, Flags);
            mesh.bounds = bounds;
            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
