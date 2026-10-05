using Ghumante.World.Streaming;
using UnityEngine;

namespace Ghumante.World.Rendering
{
    /// <summary>Names of the world shaders (World/Shaders) and their material properties.</summary>
    public static class WorldShaders
    {
        public const string ToonLit = "Ghumante/ToonLit";
        public const string SkyGradient = "Ghumante/SkyGradient";
        public const string RouteRibbon = "Ghumante/RouteRibbon";

        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly int ShadowTint = Shader.PropertyToID("_ShadowTint");
        public static readonly int RampThresholds = Shader.PropertyToID("_RampThresholds");
        public static readonly int RampLevels = Shader.PropertyToID("_RampLevels");
        public static readonly int RimColor = Shader.PropertyToID("_RimColor");
        public static readonly int RimPower = Shader.PropertyToID("_RimPower");
        public static readonly int RimStrength = Shader.PropertyToID("_RimStrength");
        public static readonly int AmbientStrength = Shader.PropertyToID("_AmbientStrength");
        public static readonly int Curvature = Shader.PropertyToID("_Curvature");
        public static readonly int ViewPull = Shader.PropertyToID("_ViewPull");
        public static readonly int OffsetFactor = Shader.PropertyToID("_OffsetFactor");
        public static readonly int OffsetUnits = Shader.PropertyToID("_OffsetUnits");
        public static readonly int Cull = Shader.PropertyToID("_Cull");

        // Route ribbon.
        public static readonly int RibbonColor = Shader.PropertyToID("_RibbonColor");
        public static readonly int RibbonEdgeColor = Shader.PropertyToID("_RibbonEdgeColor");
        public static readonly int RibbonSpeed = Shader.PropertyToID("_ScrollSpeed");
        public static readonly int RibbonSpacing = Shader.PropertyToID("_ChevronSpacing");

        // Sky material.
        public static readonly int SunDiscSize = Shader.PropertyToID("_SunDiscSize");
        public static readonly int SunGlow = Shader.PropertyToID("_SunGlow");
    }

    /// <summary>
    /// The world's materials. Project Setup creates them under <c>Assets/Ghumante/Settings/Materials/</c> and this set
    /// at <c>Assets/Ghumante/Settings/Resources/GhumanteWorldMaterials.asset</c>, which keeps them (and so their
    /// shaders) in every build. <see cref="Load"/> falls back to materials made at runtime from the shaders when the
    /// asset is missing (Project Setup not run yet), which works in the editor.
    /// </summary>
    public sealed class WorldMaterialSet : ScriptableObject
    {
        /// <summary>Resources path of the set asset.</summary>
        public const string ResourcePath = "GhumanteWorldMaterials";

        [Tooltip("Terrain: Ghumante/ToonLit, casts shadows.")]
        public Material terrain;

        [Tooltip("Road ribbons: Ghumante/ToonLit with a depth pull so they never z-fight the terrain.")]
        public Material roads;

        [Tooltip("Buildings: Ghumante/ToonLit.")]
        public Material buildings;

        [Tooltip("Water and land-use surfaces: Ghumante/ToonLit with a smaller depth pull than roads.")]
        public Material areas;

        [Tooltip("Route ribbon: Ghumante/RouteRibbon (animated, transparent).")]
        public Material route;

        [Tooltip("Skybox: Ghumante/SkyGradient (colours come from WorldSky's shader globals).")]
        public Material sky;

        /// <summary>True when the set was made at runtime (destroy it with the world).</summary>
        public bool RuntimeCreated { get; private set; }

        public bool IsComplete
        {
            get { return terrain != null && roads != null && buildings != null && areas != null && route != null && sky != null; }
        }

        /// <summary>The material of a <see cref="TileLayers"/> layer.</summary>
        public Material ForLayer(int layer)
        {
            switch (layer)
            {
                case TileLayers.Terrain: return terrain;
                case TileLayers.Roads: return roads;
                case TileLayers.Buildings: return buildings;
                default: return areas;
            }
        }

        /// <summary>The project's set, or one made at runtime when the asset is missing or incomplete (logged).</summary>
        public static WorldMaterialSet Load()
        {
            var set = Resources.Load<WorldMaterialSet>(ResourcePath);
            if (set != null && set.IsComplete) return set;
            Debug.LogWarning("WorldMaterialSet: Resources/" + ResourcePath + " missing or incomplete; using runtime materials. " +
                             "Run Ghumante > Project Setup to create the material assets (needed for player builds).");
            return CreateRuntime();
        }

        /// <summary>A set of new materials from the world shaders with the default settings.</summary>
        public static WorldMaterialSet CreateRuntime()
        {
            var set = CreateInstance<WorldMaterialSet>();
            set.name = "GhumanteWorldMaterials (runtime)";
            set.RuntimeCreated = true;
            set.terrain = Make(WorldShaders.ToonLit, "Ghumante Terrain");
            set.roads = Make(WorldShaders.ToonLit, "Ghumante Roads");
            set.buildings = Make(WorldShaders.ToonLit, "Ghumante Buildings");
            set.areas = Make(WorldShaders.ToonLit, "Ghumante Areas");
            set.route = Make(WorldShaders.RouteRibbon, "Ghumante Route");
            set.sky = Make(WorldShaders.SkyGradient, "Ghumante Sky");
            WorldMaterialDefaults.Apply(set);
            return set;
        }

        /// <summary>Destroys runtime-created materials (assets are left alone).</summary>
        public void DestroyRuntimeMaterials()
        {
            if (!RuntimeCreated) return;
            Material[] all = { terrain, roads, buildings, areas, route, sky };
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null) Destroy(all[i]);
            terrain = roads = buildings = areas = route = sky = null;
        }

        private static Material Make(string shaderName, string materialName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError("WorldMaterialSet: shader '" + shaderName + "' not found (not compiled, or not in the build).");
                shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) return null;
            }
            return new Material(shader) { name = materialName };
        }
    }

    /// <summary>
    /// Default material settings, shared by Project Setup (assets) and <see cref="WorldMaterialSet.CreateRuntime"/>.
    /// Tune the assets in the editor afterwards; Project Setup only fills properties it owns.
    /// </summary>
    public static class WorldMaterialDefaults
    {
        /// <summary>Render queue of the road and area overlays: after the terrain they lie on.</summary>
        public const int OverlayQueue = 2010;

        /// <summary>Depth pull of road ribbons (fraction of the camera distance; 0.4 mm per metre).</summary>
        public const float RoadViewPull = 0.0004f;

        /// <summary>Depth pull of area surfaces: below roads, above terrain.</summary>
        public const float AreaViewPull = 0.0002f;

        public static void Apply(WorldMaterialSet set)
        {
            if (set == null) return;
            Toon(set.terrain, 0f, 2000);
            Toon(set.buildings, 0f, 2000);
            Toon(set.roads, RoadViewPull, OverlayQueue);
            Toon(set.areas, AreaViewPull, OverlayQueue);
            if (set.roads != null)
            {
                set.roads.SetFloat(WorldShaders.OffsetFactor, -1f);
                set.roads.SetFloat(WorldShaders.OffsetUnits, -2f);
            }
            if (set.areas != null)
            {
                set.areas.SetFloat(WorldShaders.OffsetFactor, -1f);
                set.areas.SetFloat(WorldShaders.OffsetUnits, -1f);
            }
            if (set.route != null)
            {
                set.route.SetColor(WorldShaders.RibbonColor, new Color(1f, 0.83f, 0.24f, 1f));     // ui.pill.yellow, bright
                set.route.SetColor(WorldShaders.RibbonEdgeColor, new Color(1f, 1f, 1f, 1f));
                set.route.SetFloat(WorldShaders.RibbonSpeed, 6f);
                set.route.SetFloat(WorldShaders.RibbonSpacing, 7f);
                set.route.renderQueue = 3000;
            }
            if (set.sky != null)
            {
                set.sky.SetFloat(WorldShaders.SunDiscSize, 0.9994f);
                set.sky.SetFloat(WorldShaders.SunGlow, 1f);
            }
        }

        private static void Toon(Material m, float viewPull, int queue)
        {
            if (m == null) return;
            m.SetColor(WorldShaders.BaseColor, Color.white);
            m.SetColor(WorldShaders.ShadowTint, new Color(0.66f, 0.72f, 0.92f, 1f)); // soft cool shadows (ASSET_MANIFEST 1.9)
            m.SetVector(WorldShaders.RampThresholds, new Vector4(0.02f, 0.42f, 0.05f, 0f));
            m.SetVector(WorldShaders.RampLevels, new Vector4(0.32f, 0.70f, 1f, 0f));
            m.SetColor(WorldShaders.RimColor, new Color(1f, 0.93f, 0.82f, 1f));
            m.SetFloat(WorldShaders.RimPower, 3.5f);
            m.SetFloat(WorldShaders.RimStrength, 0.3f);
            m.SetFloat(WorldShaders.AmbientStrength, 0.85f);
            m.SetFloat(WorldShaders.Curvature, 1f);
            m.SetFloat(WorldShaders.ViewPull, viewPull);
            m.SetFloat(WorldShaders.OffsetFactor, 0f);
            m.SetFloat(WorldShaders.OffsetUnits, 0f);
            m.renderQueue = queue;
        }
    }
}
