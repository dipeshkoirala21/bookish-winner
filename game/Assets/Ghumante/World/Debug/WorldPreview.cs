#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;
using Ghumante.Core.Routing;
using Ghumante.Core.Search;
using UnityEngine;
#if GHUMANTE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ghumante.World.Debugging
{
    /// <summary>
    /// Development-only stand-alone world viewer (menu <b>Ghumante &gt; World Preview</b> builds a scene with it and
    /// presses Play). Opens a region, places a free-fly camera above <see cref="startPlace"/> just before sunrise, draws
    /// the motorbike route to <see cref="routeTo"/> as the route ribbon and shows streaming statistics and the controls
    /// in a corner. <b>R</b> toggles the route; the world shortcuts (T, [, ], P) are <see cref="WorldDebugHotkeys"/>.
    /// It is the test bench for track C until the Explore flow exists; it is not gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WorldRoot))]
    [AddComponentMenu("Ghumante/Debug/World Preview")]
    public sealed class WorldPreview : MonoBehaviour
    {
        [SerializeField] private string regionId = "kathmandu_core";
        [SerializeField] private string startPlace = "Thamel";
        [SerializeField] private string routeTo = "Boudhanath";

        [Tooltip("Camera height above the ground at the start.")]
        [SerializeField] private float startHeightM = 70f;

        [Tooltip("Local time at the start: just before sunrise, the hero moment.")]
        [Range(0f, 24f)]
        [SerializeField] private float startHours = 5.9f;

        [SerializeField] private bool showRoute = true;

        private WorldRoot _world;
        private Camera _camera;
        private string _status = "opening the region...";
        private string _hud = "";
        private float _hudAt;
        private double[] _route;
        private bool _routeShown;
        private bool _placed;
        private WorldPos _start;
        private GUIStyle _style;

        private async void Start()
        {
            _world = GetComponent<WorldRoot>();
            _camera = Camera.main;
            if (_camera == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                _camera = go.AddComponent<Camera>();
            }
            try
            {
                string id = await PickRegion();
                if (this == null) return;
                await _world.OpenRegionAsync(id);
            }
            catch (Exception e)
            {
                if (this == null) return;
                _status = "Could not open a region: " + e.Message + "\nRun Ghumante > Project Setup (copies the sample region) or Import Region Pack.";
                Debug.LogException(e);
                return;
            }
            if (this == null || !_world.IsOpen) return;
            _world.TimeOfDayHours = startHours;

            WorldPos start, target;
            _start = Find(startPlace, out start) ? start : new WorldPos(_world.Manifest.CentreX, 0f, _world.Manifest.CentreZ);
            bool hasTarget = Find(routeTo, out target);
            _world.Teleport(_start);
            // High above the valley until the ground under the start arrives; then down to startHeightM.
            _camera.transform.position = _world.ToScene(_start) + Vector3.up * 1800f;
            Vector3 look = hasTarget ? _world.ToScene(target) - _world.ToScene(_start) : Vector3.forward;
            look.y = 0f;
            if (look.sqrMagnitude < 1f) look = Vector3.forward;
            _camera.transform.rotation = Quaternion.LookRotation(look.normalized) * Quaternion.Euler(14f, 0f, 0f);
            var fly = _camera.GetComponent<FreeFlyCamera>();
            if (fly == null) fly = _camera.gameObject.AddComponent<FreeFlyCamera>();
            fly.World = _world;
            fly.enabled = true;
            _status = null;

            if (hasTarget && _world.Routes != null)
            {
                RouteGraph graph = _world.Routes;
                WorldPos from = _start;
                _route = await Task.Run(() => PlanRoute(graph, from, target));
                if (this != null && _route != null && showRoute)
                {
                    _world.ShowRoute(_route);
                    _routeShown = true;
                }
            }
        }

        private async Task<string> PickRegion()
        {
            IReadOnlyList<string> regions = await _world.RegionSource.ListRegionsAsync();
            for (int i = 0; i < regions.Count; i++)
                if (regions[i] == regionId) return regionId;
            if (regions.Count > 0)
            {
                Debug.LogWarning("WorldPreview: region '" + regionId + "' not installed; opening '" + regions[0] + "'.");
                return regions[0];
            }
            return regionId;
        }

        private bool Find(string place, out WorldPos position)
        {
            position = default(WorldPos);
            if (string.IsNullOrEmpty(place) || _world.Search == null) return false;
            List<SearchResult> results = _world.Search.Search(place, 1);
            if (results.Count == 0) return false;
            position = new WorldPos(results[0].Entry.X, 0f, results[0].Entry.Z);
            return true;
        }

        /// <summary>Motorbike route between two points (worker thread; Core routing only).</summary>
        private static double[] PlanRoute(RouteGraph graph, WorldPos from, WorldPos to)
        {
            var starts = new NearestNode(graph, Travel.Motorbike, false);
            var ends = new NearestNode(graph, Travel.Motorbike, true);
            int a = starts.Find(from.X, from.Z), b = ends.Find(to.X, to.Z);
            if (a < 0 || b < 0) return null;
            var astar = new AStar(graph);
            Core.Routing.Route r = astar.FindRoute(a, b, Travel.Motorbike);
            return r != null ? astar.RouteGeometry(r) : null;
        }

        private void Update()
        {
            if (_world == null || !_world.IsOpen) return;
            if (!_placed && _world.HasGroundAt(_start.X, _start.Z))
            {
                Core.Driving.GroundSample g;
                if (_world.Ground.TrySample(_start.X, _start.Z, out g))
                {
                    Vector3 p = _camera.transform.position;
                    _camera.transform.position = new Vector3(p.x, g.Height + startHeightM, p.z);
                }
                _placed = true;
            }
#if GHUMANTE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame && _route != null)
            {
                _routeShown = !_routeShown;
                if (_routeShown) _world.ShowRoute(_route);
                else _world.ClearRoute();
            }
#endif
            if (Time.unscaledTime - _hudAt > 0.5f)
            {
                _hudAt = Time.unscaledTime;
                _hud = _world.DebugSummary() + (_route != null ? "\nroute " + (_world.Route.LengthM / 1000.0).ToString("0.0") + " km" : "");
            }
        }

        private void OnGUI()
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, wordWrap = true, fontSize = 13 };
                _style.normal.textColor = Color.white;
            }
            string text = _status ?? _hud + "\n\nWASD move, Q/E down/up, Shift fast, wheel speed, drag to look" +
                          "\ntouch: drag look, pinch fly, two-finger slide\nT fast time, [ ] hour -/+, P pause, R route, F3 free-fly overlay";
            float w = Mathf.Min(560f, Screen.width - 20f);
            GUI.Box(new Rect(10f, 10f, w, 150f), text, _style);
        }
    }
}
#endif
