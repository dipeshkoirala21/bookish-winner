using Ghumante.World.Debugging;
using Ghumante.World.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Ghumante.World.EditorTools
{
    /// <summary>
    /// Editor setup of the world: the materials with their look roles (called by Project Setup) and the
    /// <b>Ghumante &gt; World Preview</b>
    /// menu, which builds a throw-away scene (camera, sun, <see cref="WorldRoot"/> + <see cref="WorldPreview"/>) and
    /// enters Play mode, so the streamed world can be flown through before the Explore flow exists.
    /// </summary>
    public static class WorldSetup
    {
        public const string MaterialsFolder = "Assets/Ghumante/Settings/Materials";
        public const string ResourcesFolder = "Assets/Ghumante/Settings/Resources";
        public const string MaterialSetPath = ResourcesFolder + "/" + WorldMaterialSet.ResourcePath + ".asset";

        /// <summary>Runs before the preview scene is built. The region import tools (Ghumante.EditorTools, which this
        /// assembly cannot reference) hook in here to copy the sample region when it is missing.</summary>
        public static event System.Action BeforePreview;

        /// <summary>
        /// Creates (or completes) the world materials under <see cref="MaterialsFolder"/> and the material set in
        /// <see cref="ResourcesFolder"/> that keeps them in builds. New materials get <see cref="WorldMaterialDefaults"/>;
        /// existing ones keep their tuned values (only a wrong shader is corrected), and every ToonLit material gets its
        /// look role (<see cref="WorldMaterialDefaults.RoleOf"/>: outline pass, occluder fade) for High, the superset of
        /// the tiers; at runtime <see cref="WorldMaterialSet.Load"/> and <see cref="ToonLook"/> narrow it to the tier in use.
        /// Returns null when the shaders are not imported yet.
        /// </summary>
        public static WorldMaterialSet EnsureMaterials()
        {
            Shader toon = Shader.Find(WorldShaders.ToonLit);
            Shader sky = Shader.Find(WorldShaders.SkyGradient);
            Shader route = Shader.Find(WorldShaders.RouteRibbon);
            Shader lights = Shader.Find(WorldShaders.InstancedLights);
            if (toon == null || sky == null || route == null || lights == null)
            {
                Debug.LogError("WorldSetup: world shaders not found (Assets/Ghumante/World/Shaders). Reimport them and run Project Setup again.");
                return null;
            }
            EnsureFolder(MaterialsFolder);
            EnsureFolder(ResourcesFolder);

            var fresh = ScriptableObject.CreateInstance<WorldMaterialSet>();
            var set = AssetDatabase.LoadAssetAtPath<WorldMaterialSet>(MaterialSetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<WorldMaterialSet>();
                AssetDatabase.CreateAsset(set, MaterialSetPath);
            }
            set.terrain = Ensure("GhumanteTerrain", toon, set.terrain, m => fresh.terrain = m);
            set.roads = Ensure("GhumanteRoads", toon, set.roads, m => fresh.roads = m);
            set.buildings = Ensure("GhumanteBuildings", toon, set.buildings, m => fresh.buildings = m);
            set.areas = Ensure("GhumanteAreas", toon, set.areas, m => fresh.areas = m);
            set.route = Ensure("GhumanteRoute", route, set.route, m => fresh.route = m);
            set.sky = Ensure("GhumanteSky", sky, set.sky, m => fresh.sky = m);
            // W2: decals, building bands, heroes, instanced dressing and lights.
            set.decals = Ensure("GhumanteDecals", toon, set.decals, m => fresh.decals = m);
            set.bandB0 = Ensure("GhumanteBandB0", toon, set.bandB0, m => fresh.bandB0 = m);
            set.bandB1 = Ensure("GhumanteBandB1", toon, set.bandB1, m => fresh.bandB1 = m);
            set.bandB1Full = Ensure("GhumanteBandB1Full", toon, set.bandB1Full, m => fresh.bandB1Full = m);
            set.bandB2 = Ensure("GhumanteBandB2", toon, set.bandB2, m => fresh.bandB2 = m);
            set.bandB3 = Ensure("GhumanteBandB3", toon, set.bandB3, m => fresh.bandB3 = m);
            set.heroes = Ensure("GhumanteHeroes", toon, set.heroes, m => fresh.heroes = m);
            set.instanced = Ensure("GhumanteInstanced", toon, set.instanced, m => fresh.instanced = m);
            set.instancedTint = Ensure("GhumanteInstancedTint", toon, set.instancedTint, m => fresh.instancedTint = m);
            set.props = Ensure("GhumanteProps", toon, set.props, m => fresh.props = m);
            set.trees = Ensure("GhumanteTrees", toon, set.trees, m => fresh.trees = m);
            set.lights = Ensure("GhumanteLights", lights, set.lights, m => fresh.lights = m);
            WorldMaterialDefaults.Apply(fresh); // only the materials created now
            WorldMaterialDefaults.ApplyExtras(fresh, false);
            MarkDirty(fresh);
            Object.DestroyImmediate(fresh);
            // The look roles (outline pass, occluder fade) are structural: every material, every run (idempotent).
            WorldMaterialDefaults.ApplyLookSuperset(set);
            MarkDirty(set);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            return set;
        }

        [MenuItem("Ghumante/World Preview", priority = 20)]
        public static void OpenPreview()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.Log("WorldSetup: stop Play mode first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (BeforePreview != null) BeforePreview();
            WorldMaterialSet set = AssetDatabase.LoadAssetAtPath<WorldMaterialSet>(MaterialSetPath);
            if (set == null || !set.IsComplete) set = EnsureMaterials();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 150000f;
            camera.clearFlags = CameraClearFlags.Skybox;
            cameraGo.AddComponent<UniversalAdditionalCameraData>();
            cameraGo.AddComponent<AudioListener>();

            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sunGo.AddComponent<UniversalAdditionalLightData>();

            var worldGo = new GameObject("World");
            worldGo.AddComponent<WorldRoot>();
            worldGo.AddComponent<WorldPreview>();

            Debug.Log("WorldSetup: World Preview scene built (" + scene.name + ", not saved); entering Play mode. " +
                      "WASD/QE fly, drag to look, T fast time, [ ] hours, R route.");
            EditorApplication.EnterPlaymode();
        }

        private static Material Ensure(string name, Shader shader, Material current, System.Action<Material> created)
        {
            string path = MaterialsFolder + "/" + name + ".mat";
            Material m = current != null ? current : AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
                created(m);
            }
            else if (m.shader != shader)
            {
                m.shader = shader;
                created(m);
            }
            return m;
        }

        private static void MarkDirty(WorldMaterialSet set)
        {
            Material[] all =
            {
                set.terrain, set.roads, set.buildings, set.areas, set.route, set.sky, set.decals, set.bandB0, set.bandB1, set.bandB1Full,
                set.bandB2, set.bandB3, set.heroes, set.instanced, set.instancedTint, set.props, set.trees, set.lights,
            };
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null) EditorUtility.SetDirty(all[i]);
        }

        private static void EnsureFolder(string assetFolder)
        {
            string[] parts = assetFolder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
