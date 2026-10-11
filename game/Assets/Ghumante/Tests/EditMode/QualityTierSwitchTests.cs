using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ghumante.App;
using Ghumante.Core.Platform;
using Ghumante.Core.Save;
using Ghumante.Core.Services;
using Ghumante.Core.Streaming;
using Ghumante.Platform;
using Ghumante.Platform.Regions;
using Ghumante.World;
using Ghumante.World.Buildings;
using Ghumante.World.Rendering;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ghumante.Tests.EditMode
{
    /// <summary>
    /// Switching the graphics tier at runtime: WorldRoot rebuilds its tier-bound parts (streaming radii, building bands,
    /// meshing options, life caps) without exceptions, and the quality director applies a pick (quality level, frame rate,
    /// look tier) and persists it.
    /// </summary>
    public class QualityTierSwitchTests
    {
        private readonly List<string> _temp = new List<string>();
        private readonly List<string> _exceptions = new List<string>();
        private int _qualityLevel;
        private int _targetFrameRate;

        [SetUp]
        public void Remember()
        {
            _qualityLevel = QualitySettings.GetQualityLevel();
            _targetFrameRate = Application.targetFrameRate;
            _exceptions.Clear();
            Application.logMessageReceived += OnLog;
        }

        [TearDown]
        public void Restore()
        {
            Application.logMessageReceived -= OnLog;
            if (QualitySettings.GetQualityLevel() != _qualityLevel) QualitySettings.SetQualityLevel(_qualityLevel, false);
            Application.targetFrameRate = _targetFrameRate;
            ToonLook.OverrideTier(null);
            ToonLook.DynamicRenderScale = 1f;
            foreach (string dir in _temp)
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (IOException)
                {
                }
            }
            _temp.Clear();
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception) _exceptions.Add(condition);
        }

        [Test]
        public void ClosedWorldTakesTheTierForItsNextOpen()
        {
            var go = new GameObject("Quality test world");
            try
            {
                WorldRoot world = go.AddComponent<WorldRoot>();
                var seen = new List<DeviceTier>();
                world.TierChanged += t => seen.Add(t);
                world.Tier = DeviceTier.High;
                Task task = world.ApplyTierAsync(DeviceTier.Low);
                Assert.IsTrue(task.IsCompleted, "nothing to rebuild while closed");
                Assert.IsFalse(task.IsFaulted);
                Assert.AreEqual(DeviceTier.Low, world.Tier);
                Assert.AreEqual(DeviceTier.Low, ToonLook.CurrentTier(), "the look follows the world's tier");
                Assert.AreEqual(-1, world.BuiltTier);
                Assert.IsFalse(world.IsSwitchingTier);
                CollectionAssert.AreEqual(new[] { DeviceTier.Low }, seen);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// Opens the committed sample region, switches High to Low to Mid while it is open and checks every tier-bound part
        /// was rebuilt for the new tier. The world's awaits run on a pumped synchronization context on this (the main)
        /// thread. Unity's edit mode refuses Object.Destroy (it logs an error and keeps the object), so those errors are
        /// let through and only exceptions fail the test; the objects are destroyed immediately at the end.
        /// </summary>
        [Test]
        public void OpenWorldSwitchesTierWithoutExceptions()
        {
            string root = RootWithSample();
            SynchronizationContext previous = SynchronizationContext.Current;
            var pump = new PumpContext();
            SynchronizationContext.SetSynchronizationContext(pump);
            bool ignored = IgnoreFailingLogs(true); // "Destroy may not be called from edit mode" from the disposals
            var go = new GameObject("Quality test world");
            WorldRoot world = go.AddComponent<WorldRoot>();
            try
            {
                world.RegionSource = new BuiltInRegionSource(root, null, false, new LocalFileFetcher());
                world.Tier = DeviceTier.High;
                var changes = new List<DeviceTier>();
                world.TierChanged += t => changes.Add(t);

                pump.RunUntil(world.OpenRegionAsync(SampleRegion.Id), 180f);
                Assert.IsTrue(world.IsOpen, "the sample opens");
                AssertBuiltFor(world, DeviceTier.High);

                foreach (DeviceTier tier in new[] { DeviceTier.Low, DeviceTier.Mid })
                {
                    Task sw = world.ApplyTierAsync(tier);
                    Assert.IsTrue(world.IsSwitchingTier || sw.IsCompleted);
                    pump.RunUntil(sw, 60f);
                    Assert.IsFalse(world.IsSwitchingTier);
                    AssertBuiltFor(world, tier);
                }
                CollectionAssert.AreEqual(new[] { DeviceTier.Low, DeviceTier.Mid }, changes);

                // A request for the tier already built does nothing but say so.
                Task same = world.ApplyTierAsync(DeviceTier.Mid);
                Assert.IsTrue(same.IsCompleted);
                AssertBuiltFor(world, DeviceTier.Mid);

                // A newer request supersedes one in flight: only the last tier is built.
                Task first = world.ApplyTierAsync(DeviceTier.High);
                Task second = world.ApplyTierAsync(DeviceTier.Low);
                pump.RunUntil(first, 60f);
                pump.RunUntil(second, 60f);
                AssertBuiltFor(world, DeviceTier.Low);
                CollectionAssert.IsEmpty(_exceptions, "no exception while switching");
            }
            finally
            {
                world.Close();
                pump.Run();
                Object.DestroyImmediate(go);
                SynchronizationContext.SetSynchronizationContext(previous);
                if (ignored) IgnoreFailingLogs(false);
            }
        }

        /// <summary>Runs posted continuations inline on the test thread (Unity's own context only runs them between editor
        /// frames, which a plain test never yields to).</summary>
        private sealed class PumpContext : SynchronizationContext
        {
            private readonly Queue<KeyValuePair<SendOrPostCallback, object>> _queue = new Queue<KeyValuePair<SendOrPostCallback, object>>();

            public override void Post(SendOrPostCallback d, object state)
            {
                lock (_queue) _queue.Enqueue(new KeyValuePair<SendOrPostCallback, object>(d, state));
            }

            public override void Send(SendOrPostCallback d, object state)
            {
                d(state);
            }

            public void Run()
            {
                while (true)
                {
                    KeyValuePair<SendOrPostCallback, object> item;
                    lock (_queue)
                    {
                        if (_queue.Count == 0) return;
                        item = _queue.Dequeue();
                    }
                    item.Key(item.Value);
                }
            }

            public void RunUntil(Task task, float seconds)
            {
                DateTime until = DateTime.UtcNow.AddSeconds(seconds);
                while (!task.IsCompleted && DateTime.UtcNow < until)
                {
                    Run();
                    Thread.Sleep(2);
                }
                Run();
                Assert.IsTrue(task.IsCompleted, "timed out");
                if (task.IsFaulted) throw task.Exception.GetBaseException();
            }
        }

        /// <summary>The test framework's LogAssert.ignoreFailingMessages, set through reflection (the compile check builds
        /// the tests without the test-framework package). False when it is not there.</summary>
        private static bool IgnoreFailingLogs(bool ignore)
        {
            Type logAssert = Type.GetType("UnityEngine.TestTools.LogAssert, UnityEngine.TestRunner");
            PropertyInfo p = logAssert != null ? logAssert.GetProperty("ignoreFailingMessages", BindingFlags.Public | BindingFlags.Static) : null;
            if (p == null) return false;
            p.SetValue(null, ignore);
            return true;
        }

        private static void AssertBuiltFor(WorldRoot world, DeviceTier tier)
        {
            int t = (int)tier;
            Assert.AreEqual(t, world.BuiltTier);
            Assert.AreEqual(tier, world.Tier);
            StreamingConfig want = StreamingConfig.ForTier(t);
            Assert.AreEqual(want.ViewRadiusM, world.Config.ViewRadiusM, 1e-6, "LOD rings of the tier");
            Assert.AreEqual(want.DetailRadiusM, world.Config.DetailRadiusM, 1e-6);
            Assert.IsNotNull(world.Streamer);
            Assert.AreEqual(BandConfig.ForTier(t).HeroBudgetTris, world.Streamer.Bands.HeroBudgetTris, "building bands of the tier");
            Assert.AreEqual(t, world.Meshing.Tier, "meshing options of the tier");
            Assert.IsNotNull(world.Life, "life simulations rebuilt");
            Assert.IsNotNull(world.GroundQuery, "the ground query object survives (controllers hold it)");
            Assert.AreEqual(tier, ToonLook.CurrentTier());
        }

        private string RootWithSample()
        {
            string root = Path.Combine(Path.GetTempPath(), "gh-quality-" + Guid.NewGuid().ToString("N"));
            string folder = Path.Combine(root, RegionFiles.FolderName, SampleRegion.Id);
            Directory.CreateDirectory(folder);
            _temp.Add(root);
            foreach (RegionFileKind kind in new[] { RegionFileKind.Manifest, RegionFileKind.Pack, RegionFileKind.SearchIndex, RegionFileKind.RouteGraph })
            {
                string name = RegionFiles.FileName(SampleRegion.Id, kind);
                File.Copy(Path.Combine(SampleRegion.Folder, name), Path.Combine(folder, name));
            }
            return root;
        }

        // ----- The director -------------------------------------------------------------------------------------------

        private sealed class FakeHost : IQualityHost
        {
            public int Persisted;
            public readonly List<string> Toasts = new List<string>();

            public WorldRoot QualityWorld
            {
                get { return null; }
            }

            public bool QualityBlocked
            {
                get { return true; }
            }

            public bool QualitySettingsOpen
            {
                get { return false; }
            }

            public void ShowQualityRedraw(bool show, string statusKey, string tierKey)
            {
            }

            public void ShowQualityToast(string key)
            {
                Toasts.Add(key);
            }

            public void PersistQuality()
            {
                Persisted++;
            }
        }

        private static DeviceFacts Phone(string gpu, int ramMB)
        {
            return new DeviceFacts
            {
                platform = RuntimePlatform.Android, systemMemoryMB = ramMB, processorCount = 8, deviceModel = "test phone",
                graphicsDeviceName = gpu,
            };
        }

        [Test]
        public void DirectorAppliesAPickAtOnceAndSavesIt()
        {
            if (QualityDirector.QualityLevelIndex(QualityTier.Low) < 0) Assert.Ignore("quality levels missing: run Ghumante > Project Setup");
            var save = new SaveData();
            var host = new FakeHost();
            var director = new QualityDirector(save, host, Phone("Adreno (TM) 710", 7500));
            try
            {
                var applied = new List<DeviceTier>();
                director.TierApplied += t => applied.Add(t);
                int changed = 0;
                director.Changed += () => changed++;
                director.ApplyAtStartup();
                Assert.AreEqual(QualityTier.Mid, director.DetectedTier, "Adreno 710 with 8 GB: Mid");
                Assert.AreEqual(QualityTier.Mid, director.ActiveTier);
                Assert.AreEqual(QualityDirector.QualityLevelIndex(QualityTier.Mid), QualitySettings.GetQualityLevel());
                Assert.AreEqual(30, Application.targetFrameRate);

                director.SetChoice(GraphicsChoice.Low);
                Assert.AreEqual(QualityTier.Low, director.ActiveTier);
                Assert.AreEqual(QualityDirector.QualityLevelIndex(QualityTier.Low), QualitySettings.GetQualityLevel());
                Assert.AreEqual(30, Application.targetFrameRate);
                Assert.AreEqual(DeviceTier.Low, ToonLook.CurrentTier());
                Assert.AreEqual("low", save.Settings.Graphics);
                Assert.AreEqual(1, host.Persisted);
                Assert.AreEqual(30, director.MaxFrameRate);

                director.SetChoice(GraphicsChoice.High);
                director.SetFrameRate(30);
                Assert.AreEqual(30, Application.targetFrameRate, "High with the battery saver");
                director.SetFrameRate(60);
                Assert.AreEqual(60, Application.targetFrameRate);
                Assert.AreEqual(60, save.Settings.FrameRate);

                director.SetChoice(GraphicsChoice.Auto);
                Assert.AreEqual(QualityTier.Mid, director.ActiveTier);
                Assert.AreEqual(60, Application.targetFrameRate, "Mid allows the 60 the player picked");
                CollectionAssert.AreEqual(new[] { DeviceTier.Mid, DeviceTier.Low, DeviceTier.High, DeviceTier.Mid }, applied);
                Assert.Greater(changed, 3);
                Assert.IsFalse(director.WorldRedrawPending, "no world open");

                // While nothing is played (the menu), the governor never judges a frame.
                for (int i = 0; i < 2000; i++) director.Tick(0.2f);
                Assert.AreEqual(QualityTier.Mid, director.ActiveTier);
                CollectionAssert.IsEmpty(host.Toasts);
            }
            finally
            {
                director.Dispose();
            }
        }

        [Test]
        public void AutoStartsWhereTheGovernorLeftItOnThisPhone()
        {
            if (QualityDirector.QualityLevelIndex(QualityTier.Low) < 0) Assert.Ignore("quality levels missing: run Ghumante > Project Setup");
            var save = new SaveData();
            QualityChoices.SetAutoTierCap(save.Settings, (int)QualityTier.Mid, "test phone");
            var director = new QualityDirector(save, new FakeHost(), Phone("Adreno (TM) 740", 11400));
            try
            {
                director.ApplyAtStartup();
                Assert.AreEqual(QualityTier.High, director.DetectedTier);
                Assert.AreEqual(QualityTier.Mid, director.AutoTier, "remembered step down");
                Assert.AreEqual(QualityTier.Mid, director.ActiveTier);
            }
            finally
            {
                director.Dispose();
            }
        }
    }
}
