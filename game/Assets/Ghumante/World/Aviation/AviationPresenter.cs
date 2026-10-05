using System;
using System.Collections.Generic;
using Ghumante.Core.Aviation;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Synth;
using Ghumante.World.Instancing;
using Ghumante.World.Life;
using Ghumante.World.Rendering;
using Ghumante.World.Streaming;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ghumante.World.Aviation
{
    /// <summary>
    /// Draws and voices the airport (W2_DESIGN 8): the runway corridor with its markings and lights (built at once from
    /// the sidecar's geometry and threshold elevations, over whatever ground level is resident, and rebuilt when finer
    /// ground arrives under it), and every aircraft of the <see cref="AirTrafficSim"/> snapshot within
    /// 30 km — the nearest first, at most 4 / 6 / 10 rendered (Low / Mid / High), at most one at LOD0 (none on Low,
    /// nearer than 300 m), 2 / 3 / 5 at LOD1, the rest at LOD2 out to 8 km and as light sprites beyond — with their
    /// navigation lights (red left, green right, white tail, red beacon at 60 per minute while engines run) and the
    /// livery colour as the tail tint. The two nearest aircraft get a live synth voice (<see cref="ISoundService.OpenAircraft"/>).
    /// Lights brighten after dusk through <c>_GhNightLights</c>. Main thread only.
    /// </summary>
    public sealed class AviationPresenter : IDisposable
    {
        public const float RenderRangeM = 30000f, Lod0M = 800f, Lod1M = 2500f, Lod2M = 8000f, Lod0MaxM = 300f;
        public const int VoiceCount = 2;

        private readonly InstanceBatch[,] _bodies = new InstanceBatch[6, 3];
        private readonly InstanceBatch _lights;
        private readonly int _renderCap, _lod1Cap, _lod0Cap;
        private readonly Material _runwayMaterial;
        private Mesh _runway;
        private readonly List<AirportLight> _airportLights = new List<AirportLight>();
        private readonly RunwayMesher _runwayMesher = new RunwayMesher();
        private bool _runwayBuilt;

        /// <summary>Coarsest ground level (TileLevel) under the runway when it was built; −1 = no ground at all.</summary>
        private int _runwayLevel = -1;

        private int _runwayCheck;

        /// <summary>Points along the runway (fractions from threshold 02 to 20) whose ground level decides rebuilds.</summary>
        private static readonly double[] LevelProbes = { 0.0, 0.25, 0.5, 0.75, 1.0 };

        /// <summary>Frames between checks for finer ground under a built runway.</summary>
        public const int RunwayCheckFrames = 30;
        private float[] _dist = new float[64];
        private int[] _order = new int[64];
        private readonly Dictionary<int, IAircraftSound> _voices = new Dictionary<int, IAircraftSound>();
        private readonly List<int> _drop = new List<int>();
        private readonly HashSet<int> _wanted = new HashSet<int>();

        /// <summary>Livery tail colours (W2_DESIGN 8.5 generic liveries) by the sidecar's livery ids, and the helicopter
        /// colours.</summary>
        private static readonly uint[] FixedWing = { 0x2A9D8F, 0xD1495B, 0x4FC3F7, 0x5B8C5A, 0xE76F51, 0x8D6E63 };

        private static readonly uint[] Heli = { 0xE63946, 0xFFC93C, 0x3A86FF };

        public AviationPresenter(int tier, WorldMaterialSet materials)
        {
            _renderCap = tier <= 0 ? 4 : tier >= 2 ? 10 : 6;
            _lod1Cap = tier <= 0 ? 2 : tier >= 2 ? 5 : 3;
            _lod0Cap = tier <= 0 ? 0 : 1;
            var m = new MeshData(4096, 12288);
            for (int c = 0; c < 6; c++)
                for (int lod = 0; lod < 3; lod++)
                {
                    m.Clear();
                    int tris = AircraftMesher.Build((AircraftClass)c, lod, m);
                    _bodies[c, lod] = new InstanceBatch(MeshUpload.CreateWhole(m, "aircraft_" + (AircraftClass)c + "_" + lod), materials.instancedTint, tris, true)
                    {
                        Shadows = lod == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    };
                }
            m.Clear();
            int stud = KitMeshes.LightStud(m);
            _lights = new InstanceBatch(MeshUpload.CreateWhole(m, "light_stud"), materials.lights, stud, true, WorldShaders.InstanceColor)
            {
                Shadows = ShadowCastingMode.Off,
            };
            _runwayMaterial = materials.decals != null ? materials.decals : materials.roads;
        }

        /// <summary>Triangles and aircraft drawn in the last frame.</summary>
        public int Tris { get; private set; }

        public int Drawn { get; private set; }

        /// <summary>The runway's lights and mesh are up.</summary>
        public bool RunwayReady
        {
            get { return _runwayBuilt; }
        }

        /// <summary>
        /// One frame: build the runway when its ground is there, draw the aircraft and lights, steer the voices.
        /// </summary>
        public void Draw(LifeHost life, IGroundQuery ground, WorldPos origin, Vector3 cameraScene, float gameHour, float timeS, ISoundService sound)
        {
            Tris = 0;
            Drawn = 0;
            if (life == null || !life.HasAirport) return;
            Shader.SetGlobalFloat(WorldShaders.NightLights, Night(gameHour));
            if (!_runwayBuilt || _runwayLevel < 10 && ++_runwayCheck >= RunwayCheckFrames) TryBuildRunway(life.Aviation, ground);
            var bounds = new Bounds(cameraScene, new Vector3(70000f, 20000f, 70000f));
            foreach (InstanceBatch b in _bodies)
            {
                b.ResetCounters();
                b.WorldBounds = bounds;
            }
            _lights.ResetCounters();
            _lights.WorldBounds = bounds;

            if (_runwayBuilt)
            {
                if (_runway != null && _runwayMaterial != null)
                {
                    var rp = new RenderParams(_runwayMaterial) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true, worldBounds = bounds };
                    var at = new Vector3((float)(_runwayMesher.OriginX - origin.X), -origin.Y, (float)(_runwayMesher.OriginZ - origin.Z));
                    Graphics.RenderMesh(rp, _runway, 0, Matrix4x4.Translate(at));
                    Tris += _runway.GetIndexCount(0) > 0 ? (int)_runway.GetIndexCount(0) / 3 : 0;
                }
                for (int i = 0; i < _airportLights.Count; i++)
                {
                    AirportLight l = _airportLights[i];
                    var p = new Vector3((float)(l.X - origin.X), l.Y - origin.Y, (float)(l.Z - origin.Z));
                    _lights.Add(Matrix4x4.TRS(p, Quaternion.identity, new Vector3(l.Size, l.Size, l.Size)), Tint.Hex(l.Colour));
                }
            }

            // Aircraft nearest first.
            int n = life.AircraftCount;
            AircraftState[] s = life.Aircraft;
            if (_dist.Length < n)
            {
                _dist = new float[n * 2];
                _order = new int[n * 2];
            }
            double camX = origin.X + cameraScene.x, camZ = origin.Z + cameraScene.z;
            float camY = origin.Y + cameraScene.y;
            for (int i = 0; i < n; i++)
            {
                double dx = s[i].X - camX, dz = s[i].Z - camZ, dy = s[i].Y - camY;
                _dist[i] = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                _order[i] = i;
            }
            Array.Sort(_dist, _order, 0, n);
            int lod0 = 0, lod1 = 0, rendered = 0;
            _wanted.Clear();
            for (int k = 0; k < n; k++)
            {
                float d = _dist[k];
                if (d > RenderRangeM) break;
                AircraftState a = s[_order[k]];
                Matrix4x4 m = Matrix4x4.TRS(new Vector3((float)(a.X - origin.X), a.Y - origin.Y, (float)(a.Z - origin.Z)),
                                            Quaternion.Euler(-a.PitchRad * Mathf.Rad2Deg, a.HeadingRad * Mathf.Rad2Deg, -a.BankRad * Mathf.Rad2Deg), Vector3.one);
                int cls = Math.Min(5, (int)a.Class);
                bool heli = a.Class == AircraftClass.Helicopter;
                Vector4 tint = Tint.Hex(heli ? Heli[a.Livery % Heli.Length] : FixedWing[a.Livery % FixedWing.Length]);
                if (rendered < _renderCap && d <= Lod2M)
                {
                    int lod;
                    if (d <= Lod0MaxM && lod0 < _lod0Cap) lod = 0;
                    else if (d <= Lod1M && lod1 < _lod1Cap) lod = 1;
                    else lod = 2;
                    if (lod == 0) lod0++;
                    else if (lod == 1) lod1++;
                    _bodies[cls, lod].Add(m, tint);
                    rendered++;
                }
                NavLights(a, m, timeS);
                if (k < VoiceCount) _wanted.Add(a.Id);
            }
            foreach (InstanceBatch b in _bodies)
            {
                b.Flush();
                Tris += b.Tris;
            }
            _lights.Flush();
            Tris += _lights.Tris;
            Drawn = rendered;
            Voices(life, origin, sound);
        }

        /// <summary>0 by day, 1 at night, ramps around sunrise and sunset.</summary>
        public static float Night(float hour)
        {
            if (hour >= 6.5f && hour <= 17.8f) return 0f;
            if (hour < 5.3f || hour > 19f) return 1f;
            return hour < 12f ? Mathf.InverseLerp(6.5f, 5.3f, hour) : Mathf.InverseLerp(17.8f, 19f, hour);
        }

        private void NavLights(in AircraftState a, in Matrix4x4 m, float timeS)
        {
            if ((a.Lights & (LightState.Navigation | LightState.Beacon)) == 0) return;
            float len, span, h;
            AircraftMesher.Dims(a.Class, out len, out span, out h);
            if ((a.Lights & LightState.Navigation) != 0)
            {
                Stud(m, new Vector3(-span * 0.5f, h * 0.3f, -len * 0.05f), 0xFF2A2A, 0.6f);
                Stud(m, new Vector3(span * 0.5f, h * 0.3f, -len * 0.05f), 0x2AFF5A, 0.6f);
                Stud(m, new Vector3(0f, h * 0.5f, -len * 0.5f), 0xFFFFFF, 0.5f);
            }
            if ((a.Lights & LightState.Beacon) != 0 && Mathf.Repeat(timeS + a.Id * 0.37f, 1f) < 0.15f) Stud(m, new Vector3(0f, h * 0.55f, 0f), 0xFF2020, 0.8f);
            if ((a.Lights & LightState.Landing) != 0) Stud(m, new Vector3(0f, h * 0.15f, len * 0.45f), 0xFFF6E0, 0.9f);
        }

        private void Stud(in Matrix4x4 aircraft, Vector3 local, uint colour, float size)
        {
            Vector3 p = aircraft.MultiplyPoint3x4(local);
            _lights.Add(Matrix4x4.TRS(p, Quaternion.identity, new Vector3(size, size, size)), Tint.Hex(colour));
        }

        private void Voices(LifeHost life, WorldPos origin, ISoundService sound)
        {
            if (sound == null) return;
            AircraftState[] s = life.Aircraft;
            for (int i = 0; i < life.AircraftCount; i++)
            {
                AircraftState a = s[i];
                if (!_wanted.Contains(a.Id)) continue;
                IAircraftSound v;
                if (!_voices.TryGetValue(a.Id, out v))
                {
                    v = sound.OpenAircraft(AircraftAudio.SoundClassOf(a.Class), (uint)a.Id);
                    _voices.Add(a.Id, v);
                }
                float vx = Mathf.Sin(a.HeadingRad) * a.SpeedMps * Mathf.Cos(a.PitchRad), vz = Mathf.Cos(a.HeadingRad) * a.SpeedMps * Mathf.Cos(a.PitchRad);
                float vy = Mathf.Sin(a.PitchRad) * a.SpeedMps;
                float thrust, reverse;
                AircraftAudio.Power(a, out thrust, out reverse);
                float agl = a.OnGround ? 0f : Math.Max(0f, a.Y - (life.Aviation != null ? life.Aviation.ElevationM : 1338f));
                v.Set((float)(a.X - origin.X), a.Y - origin.Y, (float)(a.Z - origin.Z), vx, vy, vz, thrust, reverse, agl, a.OnGround);
            }
            _drop.Clear();
            foreach (KeyValuePair<int, IAircraftSound> kv in _voices)
                if (!_wanted.Contains(kv.Key)) _drop.Add(kv.Key);
            for (int i = 0; i < _drop.Count; i++)
            {
                _voices[_drop[i]].Release();
                _voices.Remove(_drop[i]);
            }
        }

        private sealed class GroundSampler : IHeightSampler
        {
            public IGroundQuery Ground;

            public bool TryHeight(double x, double z, out float h)
            {
                GroundSample g;
                if (Ground != null && Ground.TrySample(x, z, out g))
                {
                    h = g.Height;
                    return true;
                }
                h = 0f;
                return false;
            }
        }

        /// <summary>
        /// Builds the runway from the sidecar geometry (its profile is the straight line between the threshold elevations,
        /// so it needs no ground; resident ground of any level only lifts it where the terrain pokes above) as soon as the
        /// airport exists, so aircraft never roll on a bare field. Rebuilt only when the coarsest ground level under the
        /// runway improves (at most once per level, never on coarsening), checked every <see cref="RunwayCheckFrames"/>.
        /// </summary>
        private void TryBuildRunway(AviationConfig c, IGroundQuery ground)
        {
            _runwayCheck = 0;
            if (c == null) return;
            int level = RunwayGroundLevel(c, ground);
            if (_runwayBuilt && level <= _runwayLevel) return;
            var m = new MeshData(8192, 24576);
            var lights = new List<AirportLight>();
            if (_runwayMesher.Build(c, ground != null ? new GroundSampler { Ground = ground } : null, m, lights) <= 0)
            {
                // Degenerate sidecar (thresholds < 100 m apart): nothing to draw, and never retried per frame.
                _runwayBuilt = true;
                _runwayLevel = int.MaxValue;
                return;
            }
            if (_runway != null) UnityEngine.Object.Destroy(_runway);
            _runway = MeshUpload.CreateWhole(m, "runway");
            _airportLights.Clear();
            _airportLights.AddRange(lights);
            _runwayLevel = level;
            _runwayBuilt = true;
        }

        /// <summary>The coarsest resident ground level along the runway (−1 when any probe has no ground).</summary>
        private static int RunwayGroundLevel(AviationConfig c, IGroundQuery ground)
        {
            if (ground == null) return -1;
            int level = int.MaxValue;
            for (int i = 0; i < LevelProbes.Length; i++)
            {
                double f = LevelProbes[i];
                double x = c.Threshold02.X + (c.Threshold20.X - c.Threshold02.X) * f, z = c.Threshold02.Z + (c.Threshold20.Z - c.Threshold02.Z) * f;
                GroundSample g;
                if (!ground.TrySample(x, z, out g)) return -1;
                level = Math.Min(level, g.TileLevel);
            }
            return level;
        }

        public void Dispose()
        {
            foreach (InstanceBatch b in _bodies) b.DestroyMesh();
            _lights.DestroyMesh();
            if (_runway != null) UnityEngine.Object.Destroy(_runway);
            _runway = null;
            foreach (IAircraftSound v in _voices.Values) v.Release();
            _voices.Clear();
        }
    }
}
