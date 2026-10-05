using Ghumante.Core.Driving;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ghumante.World.Navigation
{
    /// <summary>
    /// The in-world route line (ARCHITECTURE.md 7.8, "go there yourself"): a bright animated ribbon
    /// (<c>Ghumante/RouteRibbon</c>) a little above the ground along a game-metre polyline from Core routing
    /// (<c>AStar.RouteGeometry</c>). Heights come from <see cref="WorldRoot.Ground"/>; whenever the streamed ground
    /// changes the ribbon re-samples it, a few hundred points per frame, and re-uploads once a pass is done. Stretches
    /// whose ground is not loaded yet take the nearest known heights and fade out. The mesh is relative to the route's
    /// first point and placed against the floating origin every frame. Use <see cref="WorldRoot.ShowRoute"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class RouteRibbon : MonoBehaviour
    {
        /// <summary>Ground samples per frame while re-sampling.</summary>
        public int SamplesPerFrame = 256;

        private readonly RibbonBuilder _builder = new RibbonBuilder();
        private WorldRoot _world;
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private float[] _positions = new float[0], _uv = new float[0];
        private byte[] _visible = new byte[0];
        private Vector3[] _verts = new Vector3[0];
        private Vector2[] _uvs = new Vector2[0];
        private Color32[] _colors = new Color32[0];
        private int[] _indices = new int[0];
        private int _cursor, _passVersion = int.MinValue, _shownVersion = int.MinValue;
        private bool _passRunning, _hasRoute;

        /// <summary>True while a route is shown.</summary>
        public bool HasRoute
        {
            get { return _hasRoute; }
        }

        /// <summary>Length in metres of the shown route.</summary>
        public double LengthM
        {
            get { return _builder.LengthM; }
        }

        public RibbonBuilder Builder
        {
            get { return _builder; }
        }

        /// <summary>The ribbon material (Ghumante/RouteRibbon).</summary>
        public Material Material
        {
            get { return _renderer != null ? _renderer.sharedMaterial : null; }
            set
            {
                if (_renderer != null) _renderer.sharedMaterial = value;
            }
        }

        internal void Init(WorldRoot world)
        {
            _world = world;
            _mesh = new Mesh { name = "route ribbon" };
            _mesh.MarkDynamic();
            var filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = _mesh;
            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _renderer.enabled = false;
        }

        /// <summary>Show a route: interleaved x, z game metres (at least two points).</summary>
        public void Show(double[] polylineXZ)
        {
            Clear();
            if (polylineXZ == null || polylineXZ.Length < 4) return;
            _builder.SetPath(polylineXZ, polylineXZ.Length / 2);
            if (_builder.PointCount < 2) return;
            int vc = _builder.VertexCount;
            if (_verts.Length < vc)
            {
                _positions = new float[vc * 3];
                _uv = new float[vc * 2];
                _visible = new byte[vc];
                _verts = new Vector3[vc];
                _uvs = new Vector2[vc];
                _colors = new Color32[vc];
            }
            if (_indices.Length != _builder.IndexCount) _indices = new int[_builder.IndexCount];
            _builder.WriteIndices(_indices);
            _hasRoute = true;
            _passRunning = false;
            _passVersion = int.MinValue;
            _shownVersion = int.MinValue;
            // First draw straight away with the ground loaded now, then refine as tiles stream in.
            SampleAll();
            _shownVersion = _world != null ? _world.GroundVersion : int.MinValue;
            Place();
            Upload(true);
        }

        public void Clear()
        {
            _hasRoute = false;
            _passRunning = false;
            if (_mesh != null) _mesh.Clear();
            if (_renderer != null) _renderer.enabled = false;
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }

        private void LateUpdate()
        {
            if (!_hasRoute || _world == null || !_world.IsOpen) return;
            Place();

            int version = _world.GroundVersion;
            if (!_passRunning && version != _shownVersion)
            {
                _passRunning = true;
                _passVersion = version;
                _cursor = 0;
            }
            if (!_passRunning) return;
            IGroundQuery ground = _world.Ground;
            int end = Mathf.Min(_builder.PointCount, _cursor + Mathf.Max(1, SamplesPerFrame));
            for (int i = _cursor; i < end; i++) Sample(ground, i);
            _cursor = end;
            if (_cursor < _builder.PointCount) return;
            _passRunning = false;
            _shownVersion = _passVersion;
            Upload(false);
        }

        /// <summary>Put the mesh (relative to the route's first point) against the floating origin.</summary>
        private void Place()
        {
            if (_world == null) return;
            Vector3 p = _world.ToScene(_builder.AnchorX, 0f, _builder.AnchorZ);
            transform.localPosition = new Vector3(p.x, 0f, p.z);
        }

        private void SampleAll()
        {
            IGroundQuery ground = _world != null ? _world.Ground : null;
            for (int i = 0; i < _builder.PointCount; i++) Sample(ground, i);
        }

        private void Sample(IGroundQuery ground, int i)
        {
            GroundSample s;
            if (ground != null && ground.TrySample(_builder.X(i), _builder.Z(i), out s)) _builder.SetHeight(i, s.Height);
        }

        private void Upload(bool fresh)
        {
            int vc = _builder.VertexCount;
            _builder.WriteVertices(_positions, _uv, _visible);
            for (int v = 0; v < vc; v++)
            {
                _verts[v] = new Vector3(_positions[v * 3], _positions[v * 3 + 1], _positions[v * 3 + 2]);
                _uvs[v] = new Vector2(_uv[v * 2], _uv[v * 2 + 1]);
                _colors[v] = new Color32(255, 255, 255, _visible[v]);
            }
            if (fresh)
            {
                _mesh.Clear();
                _mesh.indexFormat = vc > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            }
            _mesh.SetVertices(_verts, 0, vc);
            _mesh.SetUVs(0, _uvs, 0, vc);
            _mesh.SetColors(_colors, 0, vc);
            if (fresh) _mesh.SetTriangles(_indices, 0, _indices.Length, 0, false);
            _mesh.RecalculateBounds();
            // The shader lowers far vertices for earth curvature; keep them inside the culling bounds.
            Bounds b = _mesh.bounds;
            b.Expand(new Vector3(0f, 2f * (float)Rendering.EarthCurvature.DropM(_builder.LengthM), 0f));
            _mesh.bounds = b;
            _renderer.enabled = true;
        }
    }
}
