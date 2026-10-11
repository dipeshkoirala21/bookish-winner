using System;
using System.Threading.Tasks;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;
using Ghumante.Core.Horizon;
using Ghumante.Core.Meshing;
using Ghumante.World.Streaming;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ghumante.World.Sky
{
    /// <summary>
    /// The distant mountains (owner playtest #12; ARCHITECTURE 8 "Skyline"; ROADMAP 1.9): the green rim hills and the
    /// Himalaya at their real bearings and heights, on every tier. On a worker it builds a
    /// <see cref="HorizonHeightField"/> from the region pack's horizon tiles plus the curated summits, then, whenever
    /// the camera has moved <see cref="HorizonSettings.RebuildDistanceM"/> from where the ring was built, marches a new
    /// <see cref="HorizonPanorama"/> and meshes it (<see cref="HorizonMesher"/>, within the tier's triangle cap). The ring
    /// is drawn with <c>Ghumante/Skyline</c> (World/Sky/Resources, so builds always include it): after the opaque world,
    /// at the handover distance, so the streamed terrain nearer than that stays in front and the coarse horizon terrain
    /// beyond is replaced by the true silhouettes. One draw call; its vertices are real positions, so parallax and earth
    /// curvature stay right between rebuilds. Main thread except the builds; <see cref="Dispose"/> with the world.
    /// </summary>
    public sealed class SkylineRing : IDisposable
    {
        /// <summary>Name of the skyline shader.</summary>
        public const string ShaderName = "Ghumante/Skyline";

        private static readonly int ParamsId = Shader.PropertyToID("_GhSkylineParams");

        /// <summary>Half size of the renderer bounds: the ring is never frustum-culled as a whole (it surrounds the
        /// camera).</summary>
        private const float BoundsHalfM = 300000f;

        private readonly HorizonSettings _settings;
        private readonly GameObject _go;
        private readonly MeshFilter _filter;
        private readonly MeshRenderer _renderer;
        private readonly Material _material;
        private readonly HorizonPanorama _panorama = new HorizonPanorama();
        private readonly HorizonMesher _mesher = new HorizonMesher();
        private readonly MeshData _data = new MeshData(4096, 12288);
        private Task<HorizonHeightField> _fieldTask;
        private HorizonHeightField _field;
        private Task _build;
        private Mesh _mesh;
        private double _builtX, _builtZ, _builtAlt;
        private double _buildX, _buildZ, _buildAlt;
        private bool _hasBuilt;
        private bool _disposed;
        private bool _loggedError;

        /// <summary>
        /// Starts building the height field of <paramref name="pack"/> for <paramref name="tier"/> on a worker. The ring
        /// stays hidden (and the world unchanged) when the skyline shader is missing.
        /// </summary>
        public SkylineRing(Transform parent, PackReader pack, int tier)
        {
            _settings = HorizonSettings.ForTier(tier);
            Shader shader = Shader.Find(ShaderName);
            if (shader == null || pack == null)
            {
                if (shader == null) Debug.LogWarning("SkylineRing: shader '" + ShaderName + "' not found; the distant mountains are off.");
                return;
            }
            _material = new Material(shader) { name = "Ghumante Skyline (runtime)", hideFlags = HideFlags.DontSave };
            _go = new GameObject("Skyline");
            _go.transform.SetParent(parent, false);
            _filter = _go.AddComponent<MeshFilter>();
            _renderer = _go.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _material;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            _renderer.allowOcclusionWhenDynamic = false;
            _renderer.enabled = false;
            double spacing = _settings.GridSpacingM;
            _fieldTask = Task.Run(() => HorizonHeightField.FromPack(pack, spacing));
        }

        /// <summary>The tier's settings.</summary>
        public HorizonSettings Settings
        {
            get { return _settings; }
        }

        /// <summary>True once a ring is shown.</summary>
        public bool Ready
        {
            get { return _mesh != null; }
        }

        /// <summary>Triangles of the ring shown (the debug HUD's sky share).</summary>
        public int Triangles { get; private set; }

        /// <summary>Where the ring shown was built (game metres, absolute altitude).</summary>
        public WorldPos BuiltAt
        {
            get { return new WorldPos(_builtX, (float)_builtAlt, _builtZ); }
        }

        /// <summary>
        /// Every frame, after the streamer: picks up finished builds, starts a rebuild when the camera has moved far
        /// enough, keeps the ring placed for the floating origin and hands the shader the eye altitude and the scene's
        /// fog density. <paramref name="camera"/> is the camera's world position (absolute altitude).
        /// </summary>
        public void Tick(WorldPos camera, WorldPos origin, float fogDensity)
        {
            if (_material == null || _disposed) return;
            if (_field == null && _fieldTask != null && _fieldTask.IsCompleted)
            {
                if (_fieldTask.Status == TaskStatus.RanToCompletion) _field = _fieldTask.Result;
                else LogOnce(_fieldTask.Exception);
                _fieldTask = null;
            }
            if (_build != null && _build.IsCompleted)
            {
                if (_build.Status == TaskStatus.RanToCompletion) Upload();
                else LogOnce(_build.Exception);
                _build = null;
            }
            if (_field != null && _build == null && NeedsRebuild(camera)) StartBuild(camera);
            if (_mesh != null)
            {
                _go.transform.localPosition = new Vector3((float)(_builtX - origin.X), -origin.Y, (float)(_builtZ - origin.Z));
                _renderer.enabled = true;
            }
            Shader.SetGlobalVector(ParamsId, new Vector4((float)_settings.HandoverM, camera.Y, fogDensity, origin.Y));
        }

        /// <summary>Builds the ring for <paramref name="eye"/> on this thread and shows it (tests and tools; waits for the
        /// height field). False when the skyline is off.</summary>
        internal bool BuildNow(WorldPos eye, WorldPos origin, float fogDensity)
        {
            if (_material == null || _disposed) return false;
            if (_field == null && _fieldTask != null)
            {
                _field = _fieldTask.Result;
                _fieldTask = null;
            }
            if (_build != null)
            {
                _build.Wait();
                _build = null;
            }
            if (_field == null) return false;
            _buildX = eye.X;
            _buildZ = eye.Z;
            _buildAlt = eye.Y;
            Work();
            Upload();
            Tick(eye, origin, fogDensity);
            return true;
        }

        private bool NeedsRebuild(WorldPos camera)
        {
            if (!_hasBuilt) return true;
            double dx = camera.X - _builtX, dz = camera.Z - _builtZ;
            return dx * dx + dz * dz > _settings.RebuildDistanceM * _settings.RebuildDistanceM ||
                   Math.Abs(camera.Y - _builtAlt) > _settings.RebuildHeightM;
        }

        private void StartBuild(WorldPos camera)
        {
            _buildX = camera.X;
            _buildZ = camera.Z;
            _buildAlt = camera.Y;
            _build = Task.Run((Action)Work);
        }

        /// <summary>The worker part: panorama and mesh into the reused buffers (nothing else touches them meanwhile).</summary>
        private void Work()
        {
            _panorama.Build(_field, _buildX, _buildZ, _buildAlt, _settings);
            _mesher.Build(_panorama, _settings, _data);
        }

        private void Upload()
        {
            if (_disposed) return;
            Mesh fresh = _data.TriangleCount > 0 ? MeshUpload.CreateWhole(_data, "Skyline") : null;
            if (fresh != null) fresh.bounds = new Bounds(Vector3.zero, Vector3.one * (2f * BoundsHalfM));
            Kill(_mesh);
            _mesh = fresh;
            _filter.sharedMesh = fresh;
            Triangles = _data.TriangleCount;
            _builtX = _buildX;
            _builtZ = _buildZ;
            _builtAlt = _buildAlt;
            _hasBuilt = true;
            if (fresh == null) _renderer.enabled = false;
        }

        private void LogOnce(Exception e)
        {
            if (_loggedError || _disposed || e == null) return;
            _loggedError = true;
            Debug.LogWarning("SkylineRing: " + e.GetBaseException().Message);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Kill(_mesh);
            _mesh = null;
            Kill(_go);
            Kill(_material);
        }

        private static void Kill(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
