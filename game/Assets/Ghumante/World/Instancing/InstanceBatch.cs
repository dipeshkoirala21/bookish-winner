using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ghumante.World.Instancing
{
    /// <summary>
    /// Collects instances of one mesh and material for a frame and submits them with
    /// <see cref="Graphics.RenderMeshInstanced{T}(in RenderParams, Mesh, int, T[], int, int)"/> in runs of at most
    /// <see cref="MaxPerDraw"/> (GPU instancing: GLES3, Metal and Vulkan; no compute, no BatchRendererGroup, so it works on
    /// every target device). With a tint the per-instance colours go through a <see cref="MaterialPropertyBlock"/> array
    /// (<c>_InstanceTint</c>, or another vector property). Buffers are allocated once; adding and flushing allocate
    /// nothing. Main thread only.
    /// </summary>
    public sealed class InstanceBatch
    {
        /// <summary>Instances per draw call (the instancing limit of RenderMeshInstanced arrays).</summary>
        public const int MaxPerDraw = 1023;

        private readonly Matrix4x4[] _matrices = new Matrix4x4[MaxPerDraw];
        private readonly Vector4[] _tints;
        private readonly MaterialPropertyBlock _block;
        private readonly int _tintId;
        private int _count;

        public Mesh Mesh;
        public Material Material;
        public ShadowCastingMode Shadows = ShadowCastingMode.On;

        /// <summary>Triangles of one instance (for the counters).</summary>
        public int TrisPerInstance;

        /// <summary>Instances, triangles and draw calls submitted since <see cref="ResetCounters"/>.</summary>
        public int Instances, Tris, Draws;

        /// <summary>Bounds every instance lies in (set each frame around the camera); Unity culls the draw by it.</summary>
        public Bounds WorldBounds = new Bounds(Vector3.zero, new Vector3(1e5f, 1e5f, 1e5f));

        public InstanceBatch(Mesh mesh, Material material, int trisPerInstance, bool tinted, int tintPropertyId = 0)
        {
            Mesh = mesh;
            Material = material;
            TrisPerInstance = trisPerInstance;
            if (tinted)
            {
                _tints = new Vector4[MaxPerDraw];
                _block = new MaterialPropertyBlock();
                _tintId = tintPropertyId != 0 ? tintPropertyId : Rendering.WorldShaders.InstanceTint;
            }
        }

        /// <summary>Instances waiting in the current run.</summary>
        public int Pending
        {
            get { return _count; }
        }

        public void ResetCounters()
        {
            Instances = Tris = Draws = 0;
        }

        /// <summary>Queue one instance; submits a full run on its own.</summary>
        public void Add(in Matrix4x4 m)
        {
            _matrices[_count] = m;
            if (_tints != null) _tints[_count] = Vector4.one;
            if (++_count == MaxPerDraw) Flush();
        }

        /// <summary>Queue one tinted instance (sRGB colour in xyz).</summary>
        public void Add(in Matrix4x4 m, Vector4 tint)
        {
            _matrices[_count] = m;
            if (_tints != null) _tints[_count] = tint;
            if (++_count == MaxPerDraw) Flush();
        }

        /// <summary>Submit what is queued.</summary>
        public void Flush()
        {
            if (_count == 0) return;
            if (Mesh == null || Material == null)
            {
                _count = 0;
                return;
            }
            var rp = new RenderParams(Material)
            {
                shadowCastingMode = Shadows,
                receiveShadows = true,
                lightProbeUsage = LightProbeUsage.Off,
                worldBounds = WorldBounds,
            };
            if (_block != null)
            {
                _block.SetVectorArray(_tintId, _tints);
                rp.matProps = _block;
            }
            Graphics.RenderMeshInstanced(rp, Mesh, 0, _matrices, _count);
            Instances += _count;
            Tris += _count * TrisPerInstance;
            Draws++;
            _count = 0;
        }

        /// <summary>Destroys the mesh (when the batch owns it).</summary>
        public void DestroyMesh()
        {
            if (Mesh != null) UnityEngine.Object.Destroy(Mesh);
            Mesh = null;
        }
    }

    /// <summary>Colour helpers for instance tints (sRGB hex to the vector the shaders expect).</summary>
    public static class Tint
    {
        public static Vector4 Hex(uint rgb)
        {
            return new Vector4(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        /// <summary>A small deterministic brightness variation (±<paramref name="amount"/>) of a tint.</summary>
        public static Vector4 Vary(Vector4 c, float u01, float amount)
        {
            float f = 1f + (u01 * 2f - 1f) * amount;
            return new Vector4(Math.Min(1f, c.x * f), Math.Min(1f, c.y * f), Math.Min(1f, c.z * f), 1f);
        }
    }
}
