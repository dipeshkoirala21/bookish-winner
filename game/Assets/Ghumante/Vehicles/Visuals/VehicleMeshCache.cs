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
    /// one shared mesh per (variant, model type, livery, level, rider) for traffic and parked vehicles, plus one wheel
    /// mesh per style, size and level, so instanced traffic draws from a handful of meshes with one
    /// <c>Ghumante/ToonLit</c> material. Meshes carry UV0 (u = <see cref="MaterialChannel"/>, v = baked AO) for the
    /// shader's material channels. Shared bodies carry a blank-numbered plate; <see cref="CreateUnique(int, byte, VehicleLod, uint, bool)"/>
    /// builds a body with its own plate number (the player's vehicle and the nearest traffic), which the caller owns and
    /// destroys. The model type of a vehicle comes from its seed (<see cref="VehicleMesher.ModelFor"/>): pass the same
    /// model to <see cref="Body(int, byte, byte, VehicleLod, bool)"/> so a vehicle keeps its shape across levels. Main
    /// thread only; building allocates, so presenters warm the cache while loading (<see cref="Prewarm"/>), never per
    /// frame.
    /// </summary>
    public sealed class VehicleMeshCache : IDisposable
    {
        // Stream 0 positions, 1 normals, 2 colours (RGBA8, sRGB values: the shader linearises them), 3 UV0.
        private static readonly VertexAttributeDescriptor[] Layout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 1),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, 2),
        };

        private static readonly VertexAttributeDescriptor[] LayoutUv =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 1),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, 2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 3),
        };

        private const MeshUpdateFlags Flags = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontResetBoneBounds |
                                              MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds;

        private readonly Dictionary<ulong, Mesh> _bodies = new Dictionary<ulong, Mesh>();
        private readonly Dictionary<ulong, Mesh> _wheels = new Dictionary<ulong, Mesh>();
        private readonly MeshData _scratch = new MeshData(4096, 12288);
        private static ushort[] s_shorts;

        /// <summary>Meshes held by the cache.</summary>
        public int Count
        {
            get { return _bodies.Count + _wheels.Count; }
        }

        /// <summary>The shared body of catalogue <paramref name="variant"/> as the model type a zero seed picks (wheels
        /// included from LOD2 down). Prefer <see cref="Body(int, byte, byte, VehicleLod, bool)"/> with the vehicle's own
        /// model type.</summary>
        public Mesh Body(int variant, byte livery, VehicleLod lod, bool rider = false)
        {
            return Body(variant, livery, VehicleMesher.ModelFor(variant, 0u), lod, rider);
        }

        /// <summary>The shared body of catalogue <paramref name="variant"/>, model type <paramref name="model"/> (wrapped;
        /// the block-out and box levels ignore it), wheels included from LOD2 down.</summary>
        public Mesh Body(int variant, byte livery, byte model, VehicleLod lod, bool rider = false)
        {
            byte mdl = lod >= VehicleLod.Block ? (byte)0 : (byte)(model % VehicleMesher.ModelCount(variant));
            bool rd = rider && lod <= VehicleLod.Lod2;
            ulong key = ((ulong)(uint)variant << 32) | ((ulong)mdl << 24) | ((ulong)livery << 16) | ((ulong)lod << 8) | (rd ? 1UL : 0UL);
            Mesh mesh;
            if (_bodies.TryGetValue(key, out mesh) && mesh != null) return mesh;
            _scratch.Clear();
            VehicleMesher.Build(variant, livery, mdl, lod, _scratch, 0u, rd);
            mesh = Upload(_scratch, VehicleCatalog.At(variant).AssetId + "_" + VehicleMesher.ModelName(variant, mdl) + "_" + lod);
            _bodies[key] = mesh;
            return mesh;
        }

        /// <summary>The wheel of a catalogue socket (axis along X, centred on the origin) for LOD0 and LOD1 bodies;
        /// sockets of one style share meshes to the centimetre.</summary>
        public Mesh Wheel(in WheelSocket socket, VehicleLod lod)
        {
            return Wheel(socket.Style, socket.Radius, socket.Width, lod);
        }

        /// <summary>A wheel by size only: the style of the catalogue socket of that size (<see cref="VehicleMesher.StyleFor"/>).</summary>
        public Mesh Wheel(float radius, float width, VehicleLod lod)
        {
            return Wheel(VehicleMesher.StyleFor(radius, width), radius, width, lod);
        }

        /// <summary>A wheel of <paramref name="style"/> (axis along X, centred on the origin).</summary>
        public Mesh Wheel(WheelStyle style, float radius, float width, VehicleLod lod)
        {
            uint r = (uint)Mathf.Clamp(Mathf.RoundToInt(radius * 100f), 1, 0xFFFF);
            uint w = (uint)Mathf.Clamp(Mathf.RoundToInt(width * 100f), 1, 0xFFFF);
            ulong key = ((ulong)(byte)style << 48) | ((ulong)r << 32) | ((ulong)w << 8) | (byte)lod;
            Mesh mesh;
            if (_wheels.TryGetValue(key, out mesh) && mesh != null) return mesh;
            _scratch.Clear();
            VehicleMesher.BuildWheel(style, r * 0.01f, w * 0.01f, lod, _scratch);
            mesh = Upload(_scratch, "ghm_veh_wheel_" + style + "_" + lod);
            _wheels[key] = mesh;
            return mesh;
        }

        /// <summary>A body with its own plate number from <paramref name="plateSeed"/>, as the model type that seed picks;
        /// the caller owns (and destroys) it.</summary>
        public Mesh CreateUnique(int variant, byte livery, VehicleLod lod, uint plateSeed, bool rider = false)
        {
            return CreateUnique(variant, livery, VehicleMesher.ModelFor(variant, plateSeed), lod, plateSeed, rider);
        }

        /// <summary>A body of model type <paramref name="model"/> with its own plate number from
        /// <paramref name="plateSeed"/>; the caller owns (and destroys) it.</summary>
        public Mesh CreateUnique(int variant, byte livery, byte model, VehicleLod lod, uint plateSeed, bool rider = false)
        {
            _scratch.Clear();
            VehicleMesher.Build(variant, livery, model, lod, _scratch, plateSeed, rider);
            return Upload(_scratch, VehicleCatalog.At(variant).AssetId + "_unique");
        }

        /// <summary>Builds every model type and livery of every catalogue entry at <paramref name="lod"/> (load time),
        /// with the rider versions of two-wheelers and rickshaws at LOD2 (moving traffic), and their wheels at LOD0
        /// and LOD1.</summary>
        public void Prewarm(VehicleLod lod)
        {
            for (int v = 0; v < VehicleCatalog.Count; v++)
            {
                VehicleCatalogEntry e = VehicleCatalog.At(v);
                int models = lod >= VehicleLod.Block ? 1 : VehicleMesher.ModelCount(v);
                bool riders = lod == VehicleLod.Lod2 && Carries(e.Shape);
                for (int k = 0; k < models; k++)
                    for (int l = 0; l < e.LiveryCount; l++)
                    {
                        Body(v, (byte)l, (byte)k, lod);
                        if (riders) Body(v, (byte)l, (byte)k, lod, true);
                    }
                if (lod <= VehicleLod.Lod1)
                    foreach (WheelSocket s in VehicleMesher.Wheels(e)) Wheel(s, lod);
            }
        }

        /// <summary>Shapes drawn with a rider (or a puller) in traffic.</summary>
        private static bool Carries(BodyShape s)
        {
            return s == BodyShape.Scooter || s == BodyShape.Motorbike || s == BodyShape.Cruiser || s == BodyShape.Bicycle || s == BodyShape.Rickshaw;
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

        /// <summary>One <see cref="MeshData"/> to a GPU-only Unity mesh through the advanced Mesh API (with a UV0 stream
        /// when the data has one).</summary>
        public static Mesh Upload(MeshData m, string name)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            int vc = m.VertexCount, ic = m.IndexCount;
            float minX, minY, minZ, maxX, maxY, maxZ;
            m.GetBounds(out minX, out minY, out minZ, out maxX, out maxY, out maxZ);
            var mesh = new Mesh();
            mesh.name = name;
            mesh.SetVertexBufferParams(vc, m.HasUv0 ? LayoutUv : Layout);
            mesh.SetVertexBufferData(m.Positions, 0, 0, vc * 3, 0, Flags);
            mesh.SetVertexBufferData(m.Normals, 0, 0, vc * 3, 1, Flags);
            mesh.SetVertexBufferData(m.Colors, 0, 0, vc * 4, 2, Flags);
            if (m.HasUv0) mesh.SetVertexBufferData(m.Uv0, 0, 0, vc * 2, 3, Flags);
            if (vc <= 65535)
            {
                if (s_shorts == null || s_shorts.Length < ic) s_shorts = new ushort[Math.Max(ic, 8192)];
                for (int i = 0; i < ic; i++) s_shorts[i] = (ushort)m.Indices[i];
                mesh.SetIndexBufferParams(ic, IndexFormat.UInt16);
                mesh.SetIndexBufferData(s_shorts, 0, 0, ic, Flags);
            }
            else
            {
                mesh.SetIndexBufferParams(ic, IndexFormat.UInt32);
                mesh.SetIndexBufferData(m.Indices, 0, 0, ic, Flags);
            }
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
