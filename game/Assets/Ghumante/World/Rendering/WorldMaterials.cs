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
        public const string InstancedLights = "Ghumante/InstancedLights";

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

        // W2: building bands, wind, instancing.
        public static readonly int BandRange = Shader.PropertyToID("_BandRange");
        public static readonly int Wind = Shader.PropertyToID("_Wind");
        public static readonly int InstanceTint = Shader.PropertyToID("_InstanceTint");
        public static readonly int InstanceColor = Shader.PropertyToID("_InstanceColor");
        public static readonly int BandCentre = Shader.PropertyToID("_GhBandCentre");
        public static readonly int NightLights = Shader.PropertyToID("_GhNightLights");
        public const string KeywordBandFade = "_BAND_FADE";
        public const string KeywordWind = "_WIND";
        public const string KeywordInstanceTint = "_INSTANCE_TINT";

        // W2 detail pass (World/README.md "Look"): procedural textures, baked AO, highlights, outline, occluder fade.
        public static readonly int DetailStrength = Shader.PropertyToID("_DetailStrength");
        public static readonly int AoStrength = Shader.PropertyToID("_AoStrength");
        public static readonly int SpecularStrength = Shader.PropertyToID("_SpecularStrength");
        public static readonly int OutlineWidth = Shader.PropertyToID("_OutlineWidth");
        public static readonly int OccluderFade = Shader.PropertyToID("_OccluderFade");
        public const string KeywordOccluderFade = ToonLitLayout.KeywordOccluderFade;

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

        [Tooltip("W2 road markings (decal layer): Ghumante/ToonLit with a stronger depth pull than roads.")]
        public Material decals;

        [Tooltip("W2 building bands (B0 cells, B1, B1 standing in for B0, B2, B3): Ghumante/ToonLit with _BAND_FADE. " +
                 "The streamer clones them per world and sets the tier's radii.")]
        public Material bandB0, bandB1, bandB1Full, bandB2, bandB3;

        [Tooltip("W2 hero replicas: Ghumante/ToonLit.")]
        public Material heroes;

        [Tooltip("W2 instanced vehicles, people, animals, aircraft: Ghumante/ToonLit with GPU instancing.")]
        public Material instanced;

        [Tooltip("W2 instanced props with a per-instance tint: Ghumante/ToonLit, instancing + _INSTANCE_TINT.")]
        public Material instancedTint;

        [Tooltip("W2 trees: Ghumante/ToonLit, instancing + _INSTANCE_TINT + _WIND.")]
        public Material trees;

        [Tooltip("W2 airport and aircraft lights: Ghumante/InstancedLights.")]
        public Material lights;

        private bool _extrasRuntime;

        /// <summary>True when the set was made at runtime (destroy it with the world).</summary>
        public bool RuntimeCreated { get; private set; }

        public bool IsComplete
        {
            get { return terrain != null && roads != null && buildings != null && areas != null && route != null && sky != null; }
        }

        /// <summary>True when the W2 materials are all present.</summary>
        public bool HasExtras
        {
            get
            {
                return decals != null && bandB0 != null && bandB1 != null && bandB1Full != null && bandB2 != null && bandB3 != null &&
                       heroes != null && instanced != null && instancedTint != null && trees != null && lights != null;
            }
        }

        /// <summary>The material of a <see cref="TileLayers"/> layer (band layers get the streamer's per-world clones).</summary>
        public Material ForLayer(int layer)
        {
            switch (layer)
            {
                case TileLayers.Terrain: return terrain;
                case TileLayers.Roads: return roads;
                case TileLayers.Buildings: return bandB1 != null ? bandB1 : buildings;
                case TileLayers.RoadDecals: return decals != null ? decals : roads;
                case TileLayers.BuildingsFar: return bandB2 != null ? bandB2 : buildings;
                case TileLayers.BuildingsBlock: return bandB3 != null ? bandB3 : buildings;
                case TileLayers.Heroes: return heroes != null ? heroes : buildings;
                default: return areas;
            }
        }

        /// <summary>Creates any missing W2 material at runtime (a set made before W2, or Project Setup not run again);
        /// <see cref="DestroyRuntimeMaterials"/> destroys them with the world.</summary>
        public void EnsureExtras()
        {
            if (HasExtras) return;
            _extrasRuntime = true;
            if (decals == null) decals = MakeExtra(WorldShaders.ToonLit, "Ghumante Decals (runtime)");
            if (bandB0 == null) bandB0 = MakeExtra(WorldShaders.ToonLit, "Ghumante Band B0 (runtime)");
            if (bandB1 == null) bandB1 = MakeExtra(WorldShaders.ToonLit, "Ghumante Band B1 (runtime)");
            if (bandB1Full == null) bandB1Full = MakeExtra(WorldShaders.ToonLit, "Ghumante Band B1 full (runtime)");
            if (bandB2 == null) bandB2 = MakeExtra(WorldShaders.ToonLit, "Ghumante Band B2 (runtime)");
            if (bandB3 == null) bandB3 = MakeExtra(WorldShaders.ToonLit, "Ghumante Band B3 (runtime)");
            if (heroes == null) heroes = MakeExtra(WorldShaders.ToonLit, "Ghumante Heroes (runtime)");
            if (instanced == null) instanced = MakeExtra(WorldShaders.ToonLit, "Ghumante Instanced (runtime)");
            if (instancedTint == null) instancedTint = MakeExtra(WorldShaders.ToonLit, "Ghumante Instanced Tint (runtime)");
            if (trees == null) trees = MakeExtra(WorldShaders.ToonLit, "Ghumante Trees (runtime)");
            if (lights == null) lights = MakeExtra(WorldShaders.InstancedLights, "Ghumante Lights (runtime)");
            WorldMaterialDefaults.ApplyExtras(this, true);
            WorldMaterialDefaults.ApplyLook(this, true);
        }

        private static Material DropRuntime(Material m)
        {
            if (m == null || (m.hideFlags & HideFlags.DontSave) == 0) return m;
            Destroy(m);
            return null;
        }

        private static Material MakeExtra(string shaderName, string materialName)
        {
            Material m = Make(shaderName, materialName);
            if (m != null) m.hideFlags = HideFlags.DontSave;
            return m;
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
            WorldMaterialDefaults.ApplyLook(set, false);
            set.EnsureExtras();
            return set;
        }

        /// <summary>Destroys runtime-created materials (assets are left alone).</summary>
        public void DestroyRuntimeMaterials()
        {
            if (RuntimeCreated)
            {
                Material[] all = { terrain, roads, buildings, areas, route, sky };
                for (int i = 0; i < all.Length; i++)
                    if (all[i] != null) Destroy(all[i]);
                terrain = roads = buildings = areas = route = sky = null;
            }
            if (RuntimeCreated || _extrasRuntime)
            {
                // Only the extras made at runtime carry DontSave; assets are left alone.
                decals = DropRuntime(decals);
                bandB0 = DropRuntime(bandB0);
                bandB1 = DropRuntime(bandB1);
                bandB1Full = DropRuntime(bandB1Full);
                bandB2 = DropRuntime(bandB2);
                bandB3 = DropRuntime(bandB3);
                heroes = DropRuntime(heroes);
                instanced = DropRuntime(instanced);
                instancedTint = DropRuntime(instancedTint);
                trees = DropRuntime(trees);
                lights = DropRuntime(lights);
                _extrasRuntime = false;
            }
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

        /// <summary>Depth pull of road markings: above the road ribbons.</summary>
        public const float DecalViewPull = 0.0006f;

        /// <summary>Default wind of trees (W2_DESIGN 5.8: 0.3-0.6 Hz).</summary>
        public static readonly Vector4 TreeWind = new Vector4(0.035f, 0.45f, 0.8f, 0.6f);

        /// <summary>Settings of the W2 materials; <paramref name="onlyRuntime"/> limits it to materials made at runtime.</summary>
        public static void ApplyExtras(WorldMaterialSet set, bool onlyRuntime)
        {
            if (set == null) return;
            if (Fresh(set.decals, onlyRuntime))
            {
                Toon(set.decals, DecalViewPull, OverlayQueue + 1);
                set.decals.SetFloat(WorldShaders.OffsetFactor, -1f);
                set.decals.SetFloat(WorldShaders.OffsetUnits, -3f);
            }
            Material[] bands = { set.bandB0, set.bandB1, set.bandB1Full, set.bandB2, set.bandB3 };
            for (int i = 0; i < bands.Length; i++)
            {
                if (!Fresh(bands[i], onlyRuntime)) continue;
                Toon(bands[i], 0f, 2000);
                bands[i].EnableKeyword(WorldShaders.KeywordBandFade);
                bands[i].SetFloat("_BandFade", 1f);
                bands[i].SetVector(WorldShaders.BandRange, new Vector4(0f, 100000f, 4f, 0f));
            }
            if (Fresh(set.heroes, onlyRuntime)) Toon(set.heroes, 0f, 2000);
            if (Fresh(set.instanced, onlyRuntime))
            {
                Toon(set.instanced, 0f, 2000);
                set.instanced.enableInstancing = true;
            }
            if (Fresh(set.instancedTint, onlyRuntime))
            {
                Toon(set.instancedTint, 0f, 2000);
                set.instancedTint.enableInstancing = true;
                set.instancedTint.EnableKeyword(WorldShaders.KeywordInstanceTint);
                set.instancedTint.SetFloat("_InstanceTintOn", 1f);
            }
            if (Fresh(set.trees, onlyRuntime))
            {
                Toon(set.trees, 0f, 2000);
                set.trees.enableInstancing = true;
                set.trees.EnableKeyword(WorldShaders.KeywordInstanceTint);
                set.trees.EnableKeyword(WorldShaders.KeywordWind);
                set.trees.SetFloat("_InstanceTintOn", 1f);
                set.trees.SetFloat("_WindOn", 1f);
                set.trees.SetVector(WorldShaders.Wind, TreeWind);
            }
            if (Fresh(set.lights, onlyRuntime)) set.lights.enableInstancing = true;
        }

        private static bool Fresh(Material m, bool onlyRuntime)
        {
            return m != null && (!onlyRuntime || (m.hideFlags & HideFlags.DontSave) != 0);
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
            m.SetVector(WorldShaders.Wind, new Vector4(0f, 0.45f, 0.8f, 0.6f));
            m.SetFloat(WorldShaders.DetailStrength, 1f);
            m.SetFloat(WorldShaders.AoStrength, 1f);
            m.SetFloat(WorldShaders.SpecularStrength, 1f);
            m.renderQueue = queue;
        }

        /// <summary>How a world material takes part in the cartoon look: outline width (× the tier width; 0 = the outline
        /// pass is disabled for it, no draw) and whether it dissolves between the camera and the player.</summary>
        public struct LookRole
        {
            public float Outline;
            public bool OccluderFade;

            public LookRole(float outline, bool occluderFade)
            {
                Outline = outline;
                OccluderFade = occluderFade;
            }
        }

        /// <summary>
        /// The look role of each world material. Ground layers (terrain, roads, areas, markings) get neither (outlines on
        /// ground read as noise, and the ground must never dissolve). Near buildings, bands B0 and B1, heroes, trees, props,
        /// people and animals fade between the camera and the player; far bands (B2, B3) are beyond the outline range and the
        /// capsule. The plain building material is the source the explorer's own material is copied from (the player and
        /// their vehicle), so it keeps outlines and never fades; traffic vehicles (instanced) never fade either, so the bus a
        /// passenger rides stays solid.
        /// </summary>
        public static LookRole RoleOf(WorldMaterialSet set, Material m)
        {
            if (set == null || m == null) return new LookRole(0f, false);
            if (m == set.terrain || m == set.roads || m == set.areas || m == set.decals) return new LookRole(0f, false);
            if (m == set.bandB0 || m == set.bandB1 || m == set.bandB1Full || m == set.heroes) return new LookRole(1f, true);
            if (m == set.bandB2 || m == set.bandB3) return new LookRole(0f, false);
            if (m == set.instancedTint) return new LookRole(1f, true);
            if (m == set.trees) return new LookRole(0.8f, true);
            if (m == set.buildings || m == set.instanced) return new LookRole(1f, false);
            return new LookRole(0f, false);
        }

        /// <summary>
        /// Applies the look roles (outline pass on or off, outline width, occluder-fade keyword) to the set's ToonLit
        /// materials; <paramref name="onlyRuntime"/> limits it to materials made at runtime. Idempotent: Project Setup runs it
        /// on every material each time (these are structural choices of the look, not tuning).
        /// </summary>
        public static void ApplyLook(WorldMaterialSet set, bool onlyRuntime)
        {
            if (set == null) return;
            Material[] all =
            {
                set.terrain, set.roads, set.buildings, set.areas, set.decals, set.bandB0, set.bandB1, set.bandB1Full, set.bandB2,
                set.bandB3, set.heroes, set.instanced, set.instancedTint, set.trees,
            };
            for (int i = 0; i < all.Length; i++)
            {
                Material m = all[i];
                if (!Fresh(m, onlyRuntime) || m.shader == null || m.shader.name != WorldShaders.ToonLit) continue;
                ApplyLook(m, RoleOf(set, m));
            }
        }

        /// <summary>Applies one look role to a ToonLit material.</summary>
        public static void ApplyLook(Material m, LookRole role)
        {
            if (m == null) return;
            m.SetFloat(WorldShaders.OutlineWidth, role.Outline);
            m.SetShaderPassEnabled(ToonLitLayout.OutlinePassLightMode, role.Outline > 0f);
            m.SetFloat(WorldShaders.OccluderFade, role.OccluderFade ? 1f : 0f);
            if (role.OccluderFade) m.EnableKeyword(WorldShaders.KeywordOccluderFade);
            else m.DisableKeyword(WorldShaders.KeywordOccluderFade);
        }
    }
}
