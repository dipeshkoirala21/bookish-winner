using System.Collections.Generic;
using Ghumante.App;
using Ghumante.EditorTools;
using Ghumante.Platform;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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
            Assert.AreEqual(DeviceTier.Mid, DeviceTierDetector.Classify(Android(7600, 8)));
            Assert.AreEqual(DeviceTier.High, DeviceTierDetector.Classify(Android(11800, 8)));
            Assert.AreEqual(DeviceTier.Mid, DeviceTierDetector.Classify(Ios(3700)));   // iPhone 12
            Assert.AreEqual(DeviceTier.High, DeviceTierDetector.Classify(Ios(7600)));  // iPhone 15 Pro
            Assert.AreEqual(DeviceTier.Low, DeviceTierDetector.Classify(Ios(2900)));
            Assert.AreEqual(30, DeviceTierDetector.TargetFrameRate(DeviceTier.Low));
            Assert.AreEqual(60, DeviceTierDetector.TargetFrameRate(DeviceTier.Mid));
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

        private static DeviceFacts Ios(int ramMB)
        {
            return new DeviceFacts { platform = RuntimePlatform.IPhonePlayer, systemMemoryMB = ramMB, processorCount = 6 };
        }
    }
}
