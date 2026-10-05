using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Ghumante.App;
using Ghumante.Platform;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Ghumante.EditorTools
{
    /// <summary>
    /// Applies every project setting from code (ARCHITECTURE.md section 11): ProjectSettings/*.asset are
    /// generated, not hand-edited, so CI and every developer get identical settings. Idempotent: running it
    /// twice changes nothing the second time, and existing assets are updated in place (GUIDs kept).
    ///
    /// Entry points:
    ///   menu      Ghumante > Project Setup
    ///   batch     Unity -batchmode -quit -projectPath game -executeMethod Ghumante.EditorTools.ProjectSetup.ApplyFromCommandLine
    ///   builds    BuildScript runs <see cref="Apply"/> before every build.
    ///
    /// Environment (or the equivalent command-line flag, for GameCI, which does not forward environment):
    ///   GHUMANTE_BUNDLE_ID / -ghumanteBundleId   application id for Android and iOS (default
    ///                                            com.ghumante.game, a placeholder)
    ///   APPLE_TEAM_ID / -appleTeamId             optional; written to the iOS player settings
    /// </summary>
    public static class ProjectSetup
    {
        public const string CompanyName = "Ghumante";
        public const string ProductName = "Ghumante";
        public const string DefaultBundleId = "com.ghumante.game";
        public const string BundleIdEnvironmentVariable = "GHUMANTE_BUNDLE_ID";

        /// <summary>Android 10. The project's floor (ARCHITECTURE.md; Unity 6.3 itself supports API 25+).</summary>
        public const AndroidSdkVersions AndroidMinSdk = AndroidSdkVersions.AndroidApiLevel29;

        /// <summary>
        /// Android 16. Google Play requires new apps and app updates to target API 36 from 2026-08-31
        /// (extension possible to 2026-11-01): developer.android.com/google/play/requirements/target-sdk
        /// </summary>
        public const AndroidSdkVersions AndroidTargetSdk = AndroidSdkVersions.AndroidApiLevel36;

        /// <summary>
        /// Portrait and landscape are both fully supported, with live rotation (ADR-017). Upside-down
        /// portrait is not. Layouts switch on OrientationWatcher's orient-* classes.
        /// </summary>
        public const bool AllowPortrait = true;

        /// <summary>iOS deployment target (ARCHITECTURE.md; Unity 6.3 supports iOS 15+).</summary>
        public const string IosMinimumVersion = "16.0";

        public const string SettingsFolder = "Assets/Ghumante/Settings";
        public const string UrpFolder = SettingsFolder + "/URP";
        public const string UiSettingsFolder = SettingsFolder + "/UI";
        public const string ScenesFolder = "Assets/Ghumante/Scenes";
        public const string BootstrapScenePath = ScenesFolder + "/Bootstrap.unity";
        public const string PanelSettingsPath = UiSettingsFolder + "/GhumantePanelSettings.asset";
        public const string ThemePath = "Assets/Ghumante/UI/Themes/GhumanteRuntime.tss";
        public const string TextSpikeUxmlPath = "Assets/Ghumante/UI/Screens/TextSpike.uxml";
        public const string MainMenuUxmlPath = "Assets/Ghumante/UI/Screens/MainMenu.uxml";
        public const string EnglishStringsPath = "Assets/Ghumante/UI/Localization/strings.en.json";
        public const string NepaliStringsPath = "Assets/Ghumante/UI/Localization/strings.ne.json";

        private const string PostProcessDataPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        /// <summary>Serialized field names of <see cref="Bootstrap"/>; EditMode tests check they exist.</summary>
        public static readonly string[] BootstrapFields =
            { "document", "textSpikeScreen", "mainMenuScreen", "englishStrings", "nepaliStrings" };

        /// <summary>
        /// Per-tier rendering settings. They must match the budget table and renderer rules in
        /// ARCHITECTURE.md section 10: URP Forward (never Forward+) on every tier, render scale 0.7 on Low,
        /// texture streaming budgets 200/350/450 MB, shadows 1 cascade 512 / 1 cascade 1024 / 2 cascades 1024.
        /// EditMode test ProjectTests.TierProfilesMatchArchitectureBudgets pins the values.
        /// </summary>
        public sealed class TierProfile
        {
            public DeviceTier tier;
            public string qualityName;
            public float renderScale;
            public int msaa;
            public bool hdr;
            public float shadowDistance;
            public int shadowCascades;
            public int mainLightShadowResolution;
            public bool softShadows;
            public int maxAdditionalLights;
            public bool additionalLightsPerPixel;
            public RenderingMode renderingMode;
            public float lodBias;
            public int textureStreamingBudgetMB;
            public int particleRaycastBudget;
        }

        public static readonly TierProfile[] Tiers =
        {
            new TierProfile
            {
                tier = DeviceTier.Low, qualityName = "Low", renderScale = 0.7f, msaa = 1, hdr = false,
                shadowDistance = 60f, shadowCascades = 1, mainLightShadowResolution = 512, softShadows = false,
                maxAdditionalLights = 2, additionalLightsPerPixel = false, renderingMode = RenderingMode.Forward,
                lodBias = 0.6f, textureStreamingBudgetMB = 200, particleRaycastBudget = 64,
            },
            new TierProfile
            {
                tier = DeviceTier.Mid, qualityName = "Medium", renderScale = 1.0f, msaa = 2, hdr = false,
                shadowDistance = 120f, shadowCascades = 1, mainLightShadowResolution = 1024, softShadows = false,
                maxAdditionalLights = 4, additionalLightsPerPixel = true, renderingMode = RenderingMode.Forward,
                lodBias = 1.0f, textureStreamingBudgetMB = 350, particleRaycastBudget = 256,
            },
            new TierProfile
            {
                tier = DeviceTier.High, qualityName = "High", renderScale = 1.0f, msaa = 4, hdr = true,
                shadowDistance = 180f, shadowCascades = 2, mainLightShadowResolution = 1024, softShadows = true,
                maxAdditionalLights = 8, additionalLightsPerPixel = true, renderingMode = RenderingMode.Forward,
                lodBias = 1.5f, textureStreamingBudgetMB = 450, particleRaycastBudget = 1024,
            },
        };

        [MenuItem("Ghumante/Project Setup", priority = 0)]
        public static void ApplyFromMenu()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            Apply();
            Debug.Log("Ghumante: project setup applied.");
        }

        [MenuItem("Ghumante/Rebuild Bootstrap Scene", priority = 1)]
        public static void RebuildBootstrapSceneFromMenu()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            EnsureBootstrapScene(forceRebuild: true);
        }

        /// <summary>Batch-mode entry: exits 0 on success, 1 on failure.</summary>
        public static void ApplyFromCommandLine()
        {
            try
            {
                Apply();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Applies everything. Safe to call repeatedly.</summary>
        public static void Apply()
        {
            ApplyEditorSettings();
            ApplyPlayerSettings();
            ApplyAndroidSettings();
            ApplyIosSettings();
            Dictionary<DeviceTier, UniversalRenderPipelineAsset> pipelines = EnsureUrpAssets();
            ApplyQualityLevels(pipelines);
            GraphicsSettings.defaultRenderPipeline = pipelines[DeviceTier.High];
            AdvancedTextGeneratorSetting.Enable();
            PanelSettings panel = EnsurePanelSettings();
            EnsureBootstrapScene(forceRebuild: false, panelSettings: panel);
            AssetDatabase.SaveAssets();
        }

        /// <summary>GHUMANTE_BUNDLE_ID, else -ghumanteBundleId (GameCI customParameters), else the placeholder.</summary>
        public static string BundleId
        {
            get { return CommandLineArgs.FromEnvironmentOrArgs(BundleIdEnvironmentVariable, "ghumanteBundleId", DefaultBundleId); }
        }

        // ----- Player settings ---------------------------------------------------------------------

        private static void ApplyEditorSettings()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            VersionControlSettings.mode = "Visible Meta Files";
        }

        private static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.gcIncremental = true;
            PlayerSettings.stripEngineCode = true;

            // Both landscapes and (with AllowPortrait) upright portrait; the device rotates the game live.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = AllowPortrait;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            foreach (NamedBuildTarget target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS })
            {
                PlayerSettings.SetApplicationIdentifier(target, BundleId);
                PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
                PlayerSettings.SetApiCompatibilityLevel(target, ApiCompatibilityLevel.NET_Standard);
                // Medium keeps reflection-heavy packages (Localization, Addressables) working without a
                // large link.xml; revisit High once the M1 content is in (smaller IL2CPP output).
                PlayerSettings.SetManagedStrippingLevel(target, ManagedStrippingLevel.Medium);
            }
        }

        private static void ApplyAndroidSettings()
        {
            PlayerSettings.Android.minSdkVersion = AndroidMinSdk;
            PlayerSettings.Android.targetSdkVersion = AndroidTargetSdk;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.GameActivity;
            PlayerSettings.Android.optimizedFramePacing = true;
            PlayerSettings.Android.textureCompressionFormats = new[] { TextureCompressionFormat.ASTC };
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
                new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
        }

        private static void ApplyIosSettings()
        {
            PlayerSettings.iOS.targetOSVersionString = IosMinimumVersion;
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });

            string teamId = CommandLineArgs.FromEnvironmentOrArgs("APPLE_TEAM_ID", "appleTeamId", null);
            if (!string.IsNullOrWhiteSpace(teamId))
            {
                PlayerSettings.iOS.appleDeveloperTeamID = teamId.Trim();
            }
            // Signing is done by fastlane on the macOS runner (game/fastlane/Fastfile), not by Unity.
        }

        // ----- URP -----------------------------------------------------------------------------------

        private static Dictionary<DeviceTier, UniversalRenderPipelineAsset> EnsureUrpAssets()
        {
            EnsureFolder(UrpFolder);
            var postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessDataPath);
            if (postProcessData == null)
            {
                Debug.LogWarning("ProjectSetup: URP default PostProcessData not found at " + PostProcessDataPath +
                                 "; post-processing stays unavailable until it is assigned.");
            }

            var result = new Dictionary<DeviceTier, UniversalRenderPipelineAsset>();
            foreach (TierProfile profile in Tiers)
            {
                string rendererPath = UrpFolder + "/URP-" + profile.qualityName + "-Renderer.asset";
                string pipelinePath = UrpFolder + "/URP-" + profile.qualityName + ".asset";

                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
                if (renderer == null)
                {
                    renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(renderer, rendererPath);
                }
                if (renderer.postProcessData == null && postProcessData != null)
                {
                    renderer.postProcessData = postProcessData;
                }
                renderer.renderingMode = profile.renderingMode;
                EditorUtility.SetDirty(renderer);

                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
                if (pipeline == null)
                {
                    pipeline = UniversalRenderPipelineAsset.Create(renderer);
                    AssetDatabase.CreateAsset(pipeline, pipelinePath);
                }
                ConfigurePipeline(pipeline, renderer, profile);
                result[profile.tier] = pipeline;
            }
            return result;
        }

        private static void ConfigurePipeline(UniversalRenderPipelineAsset pipeline, UniversalRendererData renderer,
            TierProfile p)
        {
            pipeline.renderScale = p.renderScale;
            pipeline.msaaSampleCount = p.msaa;
            pipeline.supportsHDR = p.hdr;
            pipeline.shadowDistance = p.shadowDistance;
            pipeline.shadowCascadeCount = p.shadowCascades;
            pipeline.mainLightShadowmapResolution = p.mainLightShadowResolution;
            pipeline.maxAdditionalLightsCount = p.maxAdditionalLights;
            pipeline.supportsCameraDepthTexture = p.tier != DeviceTier.Low;
            pipeline.supportsCameraOpaqueTexture = false;
            pipeline.useSRPBatcher = true;
            pipeline.useAdaptivePerformance = true;

            // These have internal setters in URP 17.3; write the serialized fields instead.
            var so = new SerializedObject(pipeline);
            SetBool(so, "m_MainLightShadowsSupported", true);
            SetBool(so, "m_SoftShadowsSupported", p.softShadows);
            SetBool(so, "m_AdditionalLightShadowsSupported", false);
            // LightRenderingMode: Disabled = 0, PerPixel = 1, PerVertex = 2.
            SetInt(so, "m_AdditionalLightsRenderingMode", p.additionalLightsPerPixel ? 1 : 2);
            SerializedProperty renderers = so.FindProperty("m_RendererDataList");
            if (renderers != null && renderers.isArray)
            {
                if (renderers.arraySize == 0) renderers.arraySize = 1;
                renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            }
            else
            {
                Debug.LogWarning("ProjectSetup: URP asset has no m_RendererDataList; renderer not reassigned.");
            }
            SetInt(so, "m_DefaultRendererIndex", 0);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
        }

        // ----- Quality levels ------------------------------------------------------------------------

        private static void ApplyQualityLevels(Dictionary<DeviceTier, UniversalRenderPipelineAsset> pipelines)
        {
            UnityEngine.Object qualityAsset = QualitySettings.GetQualitySettings();
            var so = new SerializedObject(qualityAsset);
            SerializedProperty levels = so.FindProperty("m_QualitySettings");
            if (levels == null || !levels.isArray)
            {
                throw new InvalidOperationException("QualitySettings has no m_QualitySettings array.");
            }

            levels.arraySize = Tiers.Length;
            for (int i = 0; i < Tiers.Length; i++)
            {
                TierProfile p = Tiers[i];
                SerializedProperty level = levels.GetArrayElementAtIndex(i);
                SetString(level, "name", p.qualityName);
                SetObject(level, "customRenderPipeline", pipelines[p.tier]);
                SetNumber(level, "vSyncCount", 0);
                SetNumber(level, "antiAliasing", 0);           // MSAA comes from the URP asset
                SetNumber(level, "lodBias", p.lodBias);
                SetNumber(level, "maximumLODLevel", 0);
                SetNumber(level, "anisotropicTextures", p.tier == DeviceTier.Low ? 0 : 1);
                SetNumber(level, "globalTextureMipmapLimit", 0); // full-res UI everywhere; budgets via streaming
                SetBool(level, "streamingMipmapsActive", true);
                SetNumber(level, "streamingMipmapsMemoryBudget", p.textureStreamingBudgetMB);
                SetBool(level, "realtimeReflectionProbes", false);
                SetBool(level, "softParticles", false);
                SetBool(level, "billboardsFaceCameraPosition", p.tier != DeviceTier.Low);
                SetNumber(level, "skinWeights", p.tier == DeviceTier.Low ? 2 : 4);
                SetNumber(level, "particleRaycastBudget", p.particleRaycastBudget);
                SetNumber(level, "asyncUploadTimeSlice", p.tier == DeviceTier.High ? 4 : 2);
                SetNumber(level, "asyncUploadBufferSize", p.tier == DeviceTier.Low ? 16 : 32);
                SerializedProperty excluded = level.FindPropertyRelative("excludedTargetPlatforms");
                if (excluded != null && excluded.isArray) excluded.arraySize = 0;
            }

            // Default level per platform before Bootstrap picks the device tier: Medium on phones.
            SerializedProperty defaults = so.FindProperty("m_PerPlatformDefaultQuality");
            if (defaults != null && defaults.isArray)
            {
                for (int i = 0; i < defaults.arraySize; i++)
                {
                    SerializedProperty entry = defaults.GetArrayElementAtIndex(i);
                    SerializedProperty platform = entry.FindPropertyRelative("first");
                    SerializedProperty index = entry.FindPropertyRelative("second");
                    if (platform == null || index == null) continue;
                    bool mobile = platform.stringValue == "Android" || platform.stringValue == "iPhone";
                    index.intValue = mobile ? (int)DeviceTier.Mid : (int)DeviceTier.High;
                }
            }
            SetNumber(so.FindProperty("m_CurrentQuality"), (int)DeviceTier.High);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ----- UI Toolkit ----------------------------------------------------------------------------

        private static PanelSettings EnsurePanelSettings()
        {
            EnsureFolder(UiSettingsFolder);
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelSettingsPath);
            }
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme == null)
            {
                Debug.LogWarning("ProjectSetup: runtime theme missing at " + ThemePath);
            }
            panel.themeStyleSheet = theme;
            // Orientation-neutral scaling (ADR-017). UI Toolkit's MatchWidthOrHeight divides by a LINEAR blend
            // of width/refWidth and height/refHeight (PanelSettings.ResolveScale in Unity 6.3), so with a
            // square reference and match 0.5 the scale is (w + h) / 3000: identical in portrait and landscape.
            // A 1920x1080 screen maps to exactly 1920x1080 panel units; a 1080x2400 phone to 931x2069 or
            // 2069x931, so elements keep their physical size when the device rotates.
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1500, 1500);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.sortingOrder = 0;
            EditorUtility.SetDirty(panel);
            return panel;
        }

        // ----- Bootstrap scene -----------------------------------------------------------------------

        public static void EnsureBootstrapScene(bool forceRebuild, PanelSettings panelSettings = null)
        {
            EnsureFolder(ScenesFolder);
            if (panelSettings == null) panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);

            bool exists = File.Exists(BootstrapScenePath);
            if (!exists || forceRebuild)
            {
                CreateBootstrapScene(panelSettings);
            }

            // Bootstrap first and enabled; keep any other scenes after it.
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(BootstrapScenePath, true) };
            scenes.AddRange(EditorBuildSettings.scenes.Where(s => s.path != BootstrapScenePath));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void CreateBootstrapScene(PanelSettings panelSettings)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.50f, 0.83f, 0.97f);
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 20000f;
            cameraGo.AddComponent<UniversalAdditionalCameraData>();
            cameraGo.AddComponent<AudioListener>();
            cameraGo.transform.position = new Vector3(0f, 2f, -10f);

            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sunGo.AddComponent<UniversalAdditionalLightData>();
            sunGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var appGo = new GameObject("Ghumante");
            var document = appGo.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            var bootstrap = appGo.AddComponent<Bootstrap>();

            var so = new SerializedObject(bootstrap);
            SetObject(so, BootstrapFields[0], document);
            SetObject(so, BootstrapFields[1], AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TextSpikeUxmlPath));
            SetObject(so, BootstrapFields[2], AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(MainMenuUxmlPath));
            SetObject(so, BootstrapFields[3], AssetDatabase.LoadAssetAtPath<TextAsset>(EnglishStringsPath));
            SetObject(so, BootstrapFields[4], AssetDatabase.LoadAssetAtPath<TextAsset>(NepaliStringsPath));
            so.ApplyModifiedPropertiesWithoutUndo();

            if (!EditorSceneManager.SaveScene(scene, BootstrapScenePath))
            {
                throw new InvalidOperationException("Could not save " + BootstrapScenePath);
            }
            Debug.Log("ProjectSetup: created " + BootstrapScenePath);
        }

        // ----- Helpers -------------------------------------------------------------------------------

        public static void EnsureFolder(string assetFolder)
        {
            string[] parts = assetFolder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private static SerializedProperty Find(SerializedObject so, string name)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p == null) Debug.LogWarning("ProjectSetup: " + so.targetObject.GetType().Name + " has no property '" + name + "'.");
            return p;
        }

        private static SerializedProperty Find(SerializedProperty parent, string name)
        {
            SerializedProperty p = parent.FindPropertyRelative(name);
            if (p == null) Debug.LogWarning("ProjectSetup: " + parent.propertyPath + " has no property '" + name + "'.");
            return p;
        }

        private static void SetObject(SerializedObject so, string name, UnityEngine.Object value)
        {
            SerializedProperty p = Find(so, name);
            if (p != null) p.objectReferenceValue = value;
        }

        private static void SetObject(SerializedProperty parent, string name, UnityEngine.Object value)
        {
            SerializedProperty p = Find(parent, name);
            if (p != null) p.objectReferenceValue = value;
        }

        private static void SetString(SerializedProperty parent, string name, string value)
        {
            SerializedProperty p = Find(parent, name);
            if (p != null) p.stringValue = value;
        }

        private static void SetBool(SerializedObject so, string name, bool value)
        {
            SerializedProperty p = Find(so, name);
            if (p != null) p.boolValue = value;
        }

        private static void SetBool(SerializedProperty parent, string name, bool value)
        {
            SerializedProperty p = Find(parent, name);
            if (p != null) p.boolValue = value;
        }

        private static void SetInt(SerializedObject so, string name, int value)
        {
            SetNumber(Find(so, name), value);
        }

        private static void SetNumber(SerializedProperty parent, string name, double value)
        {
            SetNumber(Find(parent, name), value);
        }

        /// <summary>Writes a number whatever the serialized type (int, enum, float, bool).</summary>
        private static void SetNumber(SerializedProperty p, double value)
        {
            if (p == null) return;
            switch (p.propertyType)
            {
                case SerializedPropertyType.Float:
                    p.floatValue = (float)value;
                    break;
                case SerializedPropertyType.Boolean:
                    p.boolValue = Math.Abs(value) > 0.5;
                    break;
                default:
                    // Integer and Enum: intValue writes the underlying value (not the enum name index).
                    p.intValue = (int)Math.Round(value);
                    break;
            }
        }
    }

    /// <summary>
    /// "Enable Advanced Text Generator" (Project Settings > UI Toolkit) is required for Devanagari shaping
    /// (ARCHITECTURE.md P3, ADR-007). Unity 6.3 has no public API for it: the checkbox is backed by the
    /// internal static property <c>UnityEditor.UIElements.UIToolkitProjectSettings.enableAdvancedText</c>
    /// (serialized as m_EnableAdvancedText in ProjectSettings/UIToolkitProjectSettings.asset). We set it by
    /// reflection and fall back to a clear manual instruction if Unity renames it.
    /// </summary>
    public static class AdvancedTextGeneratorSetting
    {
        public const string ManualStep =
            "Edit > Project Settings > UI Toolkit > tick 'Enable Advanced Text Generator', then commit " +
            "ProjectSettings/UIToolkitProjectSettings.asset.";

        private const string TypeName = "UnityEditor.UIElements.UIToolkitProjectSettings";
        private const string PropertyName = "enableAdvancedText";

        /// <summary>True when enabled, false when disabled, null when the setting could not be found.</summary>
        public static bool? IsEnabled()
        {
            PropertyInfo property = FindProperty();
            if (property == null) return null;
            return (bool)property.GetValue(null, null);
        }

        public static bool Enable()
        {
            PropertyInfo property = FindProperty();
            if (property == null || !property.CanWrite)
            {
                Debug.LogWarning("ProjectSetup: could not find " + TypeName + "." + PropertyName +
                                 " in this Unity version. Manual step: " + ManualStep);
                return false;
            }
            try
            {
                if (!(bool)property.GetValue(null, null))
                {
                    property.SetValue(null, true, null);
                    Debug.Log("ProjectSetup: enabled UI Toolkit's Advanced Text Generator.");
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("ProjectSetup: enabling the Advanced Text Generator failed (" + e.Message +
                                 "). Manual step: " + ManualStep);
                return false;
            }
        }

        private static PropertyInfo FindProperty()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(TypeName, false);
                if (type == null) continue;
                return type.GetProperty(PropertyName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            }
            return null;
        }
    }
}
