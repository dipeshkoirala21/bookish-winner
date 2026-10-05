using System;
using System.Collections.Generic;
using Ghumante.App;
using Ghumante.Core.Services;
using Ghumante.EditorTools;
using Ghumante.Platform;
using Ghumante.UI.Localization;
using Ghumante.UI.Screens;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Ghumante.Tests.EditMode
{
    public class ProjectTests
    {
        [Test]
        public void BootstrapSerializedFieldsMatchProjectSetup()
        {
            var go = new GameObject("bootstrap-test");
            try
            {
                // AddComponent would run Awake (DontDestroyOnLoad) only in play mode; in edit mode it does not.
                var bootstrap = go.AddComponent<Bootstrap>();
                var so = new SerializedObject(bootstrap);
                foreach (string field in ProjectSetup.BootstrapFields)
                {
                    Assert.IsNotNull(so.FindProperty(field), "Bootstrap has no serialized field '" + field + "'");
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TierProfilesCoverEveryTierInOrder()
        {
            Assert.AreEqual(3, ProjectSetup.Tiers.Length);
            for (int i = 0; i < ProjectSetup.Tiers.Length; i++)
            {
                Assert.AreEqual((DeviceTier)i, ProjectSetup.Tiers[i].tier);
                Assert.AreEqual(DeviceTierDetector.QualityLevelName((DeviceTier)i), ProjectSetup.Tiers[i].qualityName);
            }
        }

        [Test]
        public void AndroidTargetsMeetStoreRequirements()
        {
            Assert.AreEqual(29, (int)ProjectSetup.AndroidMinSdk);
            Assert.GreaterOrEqual((int)ProjectSetup.AndroidTargetSdk, 36, "Google Play requires API 36 from 2026-08-31");
        }

        [Test]
        public void DeviceTierHeuristic()
        {
            Assert.AreEqual(DeviceTier.Low, DeviceTierDetector.Classify(Android(3800, 8)));
            Assert.AreEqual(DeviceTier.Low, DeviceTierDetector.Classify(Android(7600, 4)));
            Assert.AreEqual(DeviceTier.Mid, DeviceTierDetector.Classify(Android(7600, 8)));   // SD 7 Gen 1, 8 GB
            Assert.AreEqual(DeviceTier.High, DeviceTierDetector.Classify(Android(11800, 8)));
            // ARCHITECTURE.md section 10 puts the iPhone 12 / A14+ in High.
            Assert.AreEqual(DeviceTier.High, DeviceTierDetector.Classify(Ios(3700, "iPhone13,2")));  // iPhone 12
            Assert.AreEqual(DeviceTier.High, DeviceTierDetector.Classify(Ios(3700, "iPhone14,6")));  // SE 3 (A15)
            Assert.AreEqual(DeviceTier.High, DeviceTierDetector.Classify(Ios(7600, "iPhone16,1")));  // 15 Pro
            Assert.AreEqual(DeviceTier.Mid, DeviceTierDetector.Classify(Ios(3700, "iPhone12,1")));   // iPhone 11 (A13)
            Assert.AreEqual(DeviceTier.Low, DeviceTierDetector.Classify(Ios(2900, "iPhone12,8")));   // SE 2, 3 GB
            Assert.AreEqual(DeviceTier.High, DeviceTierDetector.Classify(Ios(7600, "iPad13,4")));    // M1 iPad Pro
            Assert.AreEqual(DeviceTier.Mid, DeviceTierDetector.Classify(Ios(3700, null)));
            Assert.AreEqual(DeviceTier.Low, DeviceTierDetector.Classify(Ios(2900, "")));
            Assert.AreEqual(13, DeviceTierDetector.IphoneModelMajor("iPhone13,2"));
            Assert.AreEqual(0, DeviceTierDetector.IphoneModelMajor("iPhone"));
            Assert.AreEqual(0, DeviceTierDetector.IphoneModelMajor("iPhone13"));
            Assert.AreEqual(0, DeviceTierDetector.IphoneModelMajor("iPad13,4"));
        }

        [Test]
        public void FrameRatesMatchArchitectureBudgets()
        {
            // Section 10 / P2: Low 30, Mid 30 by default with a 60 fps option, High 60.
            Assert.AreEqual(30, DeviceTierDetector.TargetFrameRate(DeviceTier.Low));
            Assert.AreEqual(30, DeviceTierDetector.TargetFrameRate(DeviceTier.Mid));
            Assert.AreEqual(60, DeviceTierDetector.TargetFrameRate(DeviceTier.High));
            Assert.AreEqual(30, DeviceTierDetector.MaxFrameRate(DeviceTier.Low));
            Assert.AreEqual(60, DeviceTierDetector.MaxFrameRate(DeviceTier.Mid));
            Assert.AreEqual(60, DeviceTierDetector.MaxFrameRate(DeviceTier.High));
        }

        [Test]
        public void TierProfilesMatchArchitectureBudgets()
        {
            // ARCHITECTURE.md section 10 budget table and renderer rules.
            int[] textureBudgetsMB = { 200, 350, 450 };
            int[] cascades = { 1, 1, 2 };
            int[] shadowResolutions = { 512, 1024, 1024 };
            for (int i = 0; i < ProjectSetup.Tiers.Length; i++)
            {
                ProjectSetup.TierProfile p = ProjectSetup.Tiers[i];
                Assert.AreEqual(RenderingMode.Forward, p.renderingMode, p.qualityName + ": URP Forward, not Forward+");
                Assert.AreEqual(textureBudgetsMB[i], p.textureStreamingBudgetMB, p.qualityName + " texture budget");
                Assert.AreEqual(cascades[i], p.shadowCascades, p.qualityName + " shadow cascades");
                Assert.AreEqual(shadowResolutions[i], p.mainLightShadowResolution, p.qualityName + " shadow map");
            }
            Assert.AreEqual(0.7f, ProjectSetup.Tiers[(int)DeviceTier.Low].renderScale, 1e-6f);
        }

        [Test]
        public void BootstrapRegistersEveryCoreService()
        {
            var services = new ServiceRegistry();
            Bootstrap.RegisterCoreServices(services, null);
            Assert.IsInstanceOf<EventBus>(services.Get<IEventBus>());
            Assert.IsInstanceOf<NullAnalytics>(services.Get<IAnalytics>());
            Assert.IsInstanceOf<NullCrashReporter>(services.Get<ICrashReporter>());
            Assert.IsInstanceOf<NullCloudSave>(services.Get<ICloudSave>());
            Assert.IsInstanceOf<NullQuestService>(services.Get<IQuestService>());
            Assert.IsInstanceOf<NullDialogueService>(services.Get<IDialogueService>());
            Assert.IsInstanceOf<NullWorldStateFlags>(services.Get<IWorldStateFlags>());
            Assert.IsInstanceOf<NullNpcRegistry>(services.Get<INpcRegistry>());
            Assert.IsInstanceOf<NullRegionPackSource>(services.Get<IRegionPackSource>());

            // Handler exceptions go to the supplied sink instead of escaping Publish.
            var errors = new List<Exception>();
            var withSink = new ServiceRegistry();
            Bootstrap.RegisterCoreServices(withSink, errors.Add);
            IEventBus bus = withSink.Get<IEventBus>();
            bus.Subscribe<PlaceEntered>(_ => throw new InvalidOperationException("boom"));
            bus.Publish(new PlaceEntered());
            Assert.AreEqual(1, errors.Count);
        }

        [Test]
        public void UiTexturesImportWithoutMipmapsAsAstc4x4()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Ghumante/UI/Icons" });
            Assert.IsNotEmpty(guids, "no UI icons found");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Assert.IsTrue(UiTextureImportRules.AppliesTo(path), path);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(importer, path);
                Assert.IsFalse(importer.mipmapEnabled, path + ": mipmaps must be off on UI");
                Assert.IsFalse(importer.isReadable, path + ": Read/Write must be off");
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType, path);
                foreach (string platform in UiTextureImportRules.MobilePlatforms)
                {
                    TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
                    Assert.IsTrue(settings.overridden, path + " " + platform);
                    Assert.AreEqual(TextureImporterFormat.ASTC_4x4, settings.format, path + " " + platform);
                }
            }
            Assert.IsFalse(UiTextureImportRules.AppliesTo("Assets/Ghumante/World/rock.png"));
        }

        [Test]
        public void TextSpikeDeviceInfoFollowsResolutionChanges()
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ProjectSetup.TextSpikeUxmlPath);
            Assert.IsNotNull(uxml, ProjectSetup.TextSpikeUxmlPath);
            VisualElement root = uxml.CloneTree();
            var localizer = new Localizer();
            localizer.AddTable(Localizer.English, "{}");
            localizer.SetLocale(Localizer.English);
            var screen = new TextSpikeScreen(root, localizer);
            try
            {
                Assert.AreEqual(TextSpikeScreen.DeviceSummary(), screen.DeviceInfoText);
                root.Q<Label>("device-info").text = "1080x2400 (stale)";
                // What the GeometryChangedEvent handler runs after a rotation.
                screen.UpdateDeviceInfo();
                Assert.AreEqual(TextSpikeScreen.DeviceSummary(), screen.DeviceInfoText);
            }
            finally
            {
                screen.Dispose();
            }
        }

        [Test]
        public void BuildArgumentsParseLikeGameCi()
        {
            Dictionary<string, string> args = BuildScript.ParseArgs(new[]
            {
                "/opt/unity/Editor/Unity", "-quit", "-customBuildTarget", "Android", "-customBuildPath",
                "/github/workspace/build/Android/Android.aab", "-androidKeystoreName", "", "-buildVersion", "0.1.7",
                "-androidVersionCode", "1007", "-developmentBuild",
            });
            Assert.AreEqual("", args["quit"]);
            Assert.AreEqual("Android", args["customBuildTarget"]);
            Assert.AreEqual("/github/workspace/build/Android/Android.aab", args["customBuildPath"]);
            Assert.AreEqual("", args["androidKeystoreName"]);
            Assert.AreEqual("0.1.7", args["buildVersion"]);
            Assert.AreEqual("1007", args["androidVersionCode"]);
            Assert.IsTrue(args.ContainsKey("developmentBuild"));
        }

        private static DeviceFacts Android(int ramMB, int cpus)
        {
            return new DeviceFacts { platform = RuntimePlatform.Android, systemMemoryMB = ramMB, processorCount = cpus };
        }

        private static DeviceFacts Ios(int ramMB, string model)
        {
            return new DeviceFacts
            {
                platform = RuntimePlatform.IPhonePlayer, systemMemoryMB = ramMB, processorCount = 6, deviceModel = model,
            };
        }
    }
}
