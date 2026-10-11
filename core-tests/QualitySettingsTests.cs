using Ghumante.Core.Platform;
using Ghumante.Core.Save;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>The graphics settings: choices, frame rates per tier, the Auto memory, and the save round trip.</summary>
    public class QualitySettingsTests
    {
        [Test]
        public void ChoicesResolveToTiers()
        {
            Assert.That(QualityChoices.Resolve(GraphicsChoice.Auto, QualityTier.Mid), Is.EqualTo(QualityTier.Mid));
            Assert.That(QualityChoices.Resolve(GraphicsChoice.Auto, QualityTier.High), Is.EqualTo(QualityTier.High));
            Assert.That(QualityChoices.Resolve(GraphicsChoice.Low, QualityTier.High), Is.EqualTo(QualityTier.Low));
            Assert.That(QualityChoices.Resolve(GraphicsChoice.Medium, QualityTier.Low), Is.EqualTo(QualityTier.Mid));
            Assert.That(QualityChoices.Resolve(GraphicsChoice.High, QualityTier.Low), Is.EqualTo(QualityTier.High));
        }

        [Test]
        public void SaveKeysRoundTripAndUnknownIsAuto()
        {
            foreach (GraphicsChoice c in new[] { GraphicsChoice.Auto, GraphicsChoice.Low, GraphicsChoice.Medium, GraphicsChoice.High })
                Assert.That(QualityChoices.Parse(QualityChoices.Key(c)), Is.EqualTo(c));
            Assert.That(QualityChoices.Key(GraphicsChoice.Medium), Is.EqualTo("medium"));
            Assert.That(QualityChoices.Parse("HIGH"), Is.EqualTo(GraphicsChoice.High));
            Assert.That(QualityChoices.Parse("mid"), Is.EqualTo(GraphicsChoice.Medium));
            Assert.That(QualityChoices.Parse("ultra"), Is.EqualTo(GraphicsChoice.Auto), "a newer build's value");
            Assert.That(QualityChoices.Parse(null), Is.EqualTo(GraphicsChoice.Auto));
        }

        [Test]
        public void FrameRateFollowsWhatTheTierAllows()
        {
            Assert.That(QualityChoices.FrameRate(QualityTier.Low, 0), Is.EqualTo(30));
            Assert.That(QualityChoices.FrameRate(QualityTier.Low, 60), Is.EqualTo(30), "Low never runs 60");
            Assert.That(QualityChoices.FrameRate(QualityTier.Mid, 0), Is.EqualTo(30), "Mid defaults to 30");
            Assert.That(QualityChoices.FrameRate(QualityTier.Mid, 60), Is.EqualTo(60), "Mid offers 60");
            Assert.That(QualityChoices.FrameRate(QualityTier.High, 0), Is.EqualTo(60));
            Assert.That(QualityChoices.FrameRate(QualityTier.High, 30), Is.EqualTo(30), "battery saver on High");
            Assert.That(QualityChoices.FrameRate(QualityTier.High, 45), Is.EqualTo(60), "anything else is the default");
            Assert.That(QualityTiers.MaxFrameRate(QualityTier.Low), Is.EqualTo(30));
            Assert.That(QualityTiers.MaxFrameRate(QualityTier.Mid), Is.EqualTo(60));
        }

        [Test]
        public void AutoRemembersAStepDownOnTheSamePhoneOnly()
        {
            Assert.That(QualityChoices.AutoStartTier(QualityTier.High, -1, "", "SM-A566B"), Is.EqualTo(QualityTier.High));
            Assert.That(QualityChoices.AutoStartTier(QualityTier.High, 1, "SM-A566B", "SM-A566B"), Is.EqualTo(QualityTier.Mid));
            Assert.That(QualityChoices.AutoStartTier(QualityTier.High, 1, "SM-A566B", "SM-S931B"), Is.EqualTo(QualityTier.High),
                        "a save restored on another phone");
            Assert.That(QualityChoices.AutoStartTier(QualityTier.Mid, 2, "X", "X"), Is.EqualTo(QualityTier.Mid), "never above detection");
        }

        [Test]
        public void SettingsRoundTripThroughTheSave()
        {
            var save = new SaveData();
            Assert.That(QualityChoices.Choice(save.Settings), Is.EqualTo(GraphicsChoice.Auto), "Auto by default");
            Assert.That(save.Settings.FrameRate, Is.EqualTo(0));
            Assert.That(save.Settings.AutoTierCap, Is.EqualTo(-1));

            QualityChoices.SetChoice(save.Settings, GraphicsChoice.Medium);
            QualityChoices.SetFrameRate(save.Settings, 60);
            QualityChoices.SetAutoTierCap(save.Settings, (int)QualityTier.Mid, "samsung SM-A566B");
            save.Settings.Extra.Set("futureToggle", true);

            string text = Json.Write(save.ToJson());
            SaveData back = SaveData.FromJson(SaveMigrator.Default().Migrate(Json.Parse(text).AsObject()));
            Assert.That(QualityChoices.Choice(back.Settings), Is.EqualTo(GraphicsChoice.Medium));
            Assert.That(back.Settings.FrameRate, Is.EqualTo(60));
            Assert.That(back.Settings.AutoTierCap, Is.EqualTo((int)QualityTier.Mid));
            Assert.That(back.Settings.AutoTierDevice, Is.EqualTo("samsung SM-A566B"));
            Assert.That(back.Settings.Extra.GetBool("futureToggle"), Is.True, "unknown keys still survive");
            Assert.That(back.Settings.Extra.ContainsKey("graphics"), Is.False, "known keys are not duplicated into Extra");
            Assert.That(Json.Write(back.ToJson()), Is.EqualTo(text), "stable round trip");

            QualityChoices.SetFrameRate(back.Settings, 45);
            Assert.That(back.Settings.FrameRate, Is.EqualTo(0), "only 30 or 60 are stored");
            QualityChoices.SetAutoTierCap(back.Settings, -1, "ignored");
            Assert.That((back.Settings.AutoTierCap, back.Settings.AutoTierDevice), Is.EqualTo((-1, "")));
        }

        [Test]
        public void AnOlderSaveWithoutGraphicsKeysGetsDefaults()
        {
            JsonObject old = new SaveData().ToJson();
            JsonObject settings = old.GetObject("settings");
            settings.Remove("graphics");
            settings.Remove("frameRate");
            settings.Remove("autoTierCap");
            settings.Remove("autoTierDevice");
            SaveData s = SaveData.FromJson(old);
            Assert.That(QualityChoices.Choice(s.Settings), Is.EqualTo(GraphicsChoice.Auto));
            Assert.That(s.Settings.FrameRate, Is.EqualTo(0));
            Assert.That(s.Settings.AutoTierCap, Is.EqualTo(-1));
        }
    }
}
