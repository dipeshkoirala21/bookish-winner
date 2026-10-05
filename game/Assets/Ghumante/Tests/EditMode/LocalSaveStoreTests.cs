using System;
using System.IO;
using Ghumante.Core.Save;
using Ghumante.Save;
using NUnit.Framework;

namespace Ghumante.Tests.EditMode
{
    public class LocalSaveStoreTests
    {
        private string _dir;

        [SetUp]
        public void MakeFolder()
        {
            _dir = Path.Combine(Path.GetTempPath(), "ghumante-save-test-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void RemoveFolder()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void FirstLaunchGivesDefaults()
        {
            var store = new LocalSaveStore(_dir);
            SaveData data = store.Load();
            Assert.AreEqual(SaveSource.None, store.LastSource);
            Assert.IsTrue(data.Settings.Haptics, "haptics default on");
            Assert.IsFalse(data.Settings.ReduceMotion);
            Assert.IsFalse(store.ReadOnly);
        }

        [Test]
        public void SettingsRoundTrip()
        {
            var store = new LocalSaveStore(_dir);
            SaveData data = store.Load();
            data.Settings.Haptics = false;
            SettingsChoices.SetReduceMotion(data.Settings, true);
            SettingsChoices.SetLanguage(data.Settings, "ne");
            data.Progress.Coins = 1300;
            Assert.IsTrue(store.Save(data));
            Assert.IsTrue(File.Exists(store.FilePath));
            Assert.IsFalse(File.Exists(store.TempPath), "the temp file is renamed, not left behind");

            var reopened = new LocalSaveStore(_dir);
            SaveData back = reopened.Load();
            Assert.AreEqual(SaveSource.Primary, reopened.LastSource);
            Assert.IsFalse(back.Settings.Haptics);
            Assert.IsTrue(back.Settings.ReduceMotion);
            Assert.AreEqual("ne", back.Settings.Language);
            Assert.AreEqual(1300, back.Progress.Coins);
            Assert.IsTrue(SettingsChoices.IsChosen(back.Settings, SettingsChoices.ReduceMotionChosenKey));
            Assert.IsTrue(SettingsChoices.IsChosen(back.Settings, SettingsChoices.LanguageChosenKey));
        }

        [Test]
        public void SavesRotateTwoBackups()
        {
            var store = new LocalSaveStore(_dir);
            for (int i = 1; i <= 3; i++)
            {
                var data = new SaveData();
                data.Progress.Coins = i;
                Assert.IsTrue(store.Save(data));
            }
            Assert.AreEqual(3, SaveSerializer.FromJson(File.ReadAllText(store.FilePath)).Progress.Coins);
            Assert.AreEqual(2, SaveSerializer.FromJson(File.ReadAllText(store.BackupPath)).Progress.Coins);
            Assert.AreEqual(1, SaveSerializer.FromJson(File.ReadAllText(store.OlderBackupPath)).Progress.Coins);
        }

        [Test]
        public void CorruptSaveFallsBackToTheBackup()
        {
            var store = new LocalSaveStore(_dir);
            var first = new SaveData();
            first.Settings.Haptics = false;
            store.Save(first);
            store.Save(new SaveData());  // the first save becomes the backup
            File.WriteAllText(store.FilePath, "{ \"schemaVersion\": 1, \"settings\": ");  // torn write
            SaveData data = store.Load();
            Assert.AreEqual(SaveSource.Backup, store.LastSource);
            Assert.IsFalse(data.Settings.Haptics);
        }

        [Test]
        public void SaveFromANewerBuildIsNeverOverwritten()
        {
            Directory.CreateDirectory(_dir);
            var store = new LocalSaveStore(_dir);
            string newer = "{\"schemaVersion\": " + (SaveData.CurrentSchemaVersion + 1) + "}";
            File.WriteAllText(store.FilePath, newer);
            store.Load();
            Assert.AreEqual(SaveSource.NewerVersion, store.LastSource);
            Assert.IsTrue(store.ReadOnly);
            Assert.IsFalse(store.Save(new SaveData()));
            Assert.AreEqual(newer, File.ReadAllText(store.FilePath));
        }

        [Test]
        public void UnchosenSettingsFollowTheDevice()
        {
            var settings = new SaveData().Settings;
            Assert.IsTrue(SettingsChoices.ReduceMotion(settings, osPrefersReducedMotion: true), "OS preference by default");
            Assert.IsFalse(SettingsChoices.ReduceMotion(settings, osPrefersReducedMotion: false));
            Assert.AreEqual("ne", SettingsChoices.Language(settings, "ne"), "device language by default");

            SettingsChoices.SetReduceMotion(settings, false);
            SettingsChoices.SetLanguage(settings, "en");
            Assert.IsFalse(SettingsChoices.ReduceMotion(settings, osPrefersReducedMotion: true), "the player's choice wins");
            Assert.AreEqual("en", SettingsChoices.Language(settings, "ne"));
        }
    }
}
