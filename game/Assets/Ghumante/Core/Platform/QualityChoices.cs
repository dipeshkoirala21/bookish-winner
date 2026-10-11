using System;
using Ghumante.Core.Save;

namespace Ghumante.Core.Platform
{
    /// <summary>The Settings > Graphics choice. Values are stable (append only).</summary>
    public enum GraphicsChoice
    {
        /// <summary>The detected tier, lowered by the adaptive governor if the phone cannot hold it (default).</summary>
        Auto = 0,
        Low = 1,
        Medium = 2,
        High = 3,
    }

    /// <summary>
    /// The player's graphics settings and how they resolve to a tier and a frame rate (pure). They live in the save's
    /// <c>settings</c> section (ARCHITECTURE.md 7.10) as <c>graphics</c> ("auto", "low", "medium", "high") and
    /// <c>frameRate</c> (0 = the tier's default, else 30 or 60), plus what the governor learned for Auto on this phone
    /// (<c>autoTierCap</c> and <c>autoTierDevice</c>).
    /// </summary>
    public static class QualityChoices
    {
        /// <summary>Save value of <see cref="GraphicsChoice.Auto"/>.</summary>
        public const string AutoKey = "auto";

        /// <summary>The save value of a choice ("auto", "low", "medium", "high").</summary>
        public static string Key(GraphicsChoice choice)
        {
            switch (choice)
            {
                case GraphicsChoice.Low: return QualityTiers.Key(QualityTier.Low);
                case GraphicsChoice.Medium: return QualityTiers.Key(QualityTier.Mid);
                case GraphicsChoice.High: return QualityTiers.Key(QualityTier.High);
                default: return AutoKey;
            }
        }

        /// <summary>Reads a save value; anything unknown (an older or newer build's value, a typo) is Auto.</summary>
        public static GraphicsChoice Parse(string key)
        {
            QualityTier tier;
            if (QualityTiers.TryParseKey(key, out tier)) return FromTier(tier);
            return GraphicsChoice.Auto;
        }

        /// <summary>The manual choice of a tier.</summary>
        public static GraphicsChoice FromTier(QualityTier tier)
        {
            switch (tier)
            {
                case QualityTier.Low: return GraphicsChoice.Low;
                case QualityTier.Mid: return GraphicsChoice.Medium;
                default: return GraphicsChoice.High;
            }
        }

        /// <summary>The tier a choice runs: a manual tier as picked, Auto the tier Auto currently uses.</summary>
        public static QualityTier Resolve(GraphicsChoice choice, QualityTier autoTier)
        {
            switch (choice)
            {
                case GraphicsChoice.Low: return QualityTier.Low;
                case GraphicsChoice.Medium: return QualityTier.Mid;
                case GraphicsChoice.High: return QualityTier.High;
                default: return autoTier;
            }
        }

        /// <summary>
        /// The frame rate to target on <paramref name="tier"/>: the player's pick (30 or 60) where the tier allows it,
        /// else the tier's default (<paramref name="chosen"/> 0 or anything else means "default").
        /// </summary>
        public static int FrameRate(QualityTier tier, int chosen)
        {
            int max = QualityTiers.MaxFrameRate(tier);
            if (chosen == 30 || chosen == 60) return Math.Min(chosen, max);
            return QualityTiers.DefaultFrameRate(tier);
        }

        /// <summary>
        /// The tier Auto starts with: the detected tier, unless the governor lowered Auto on this very phone before
        /// (<paramref name="learnedCap"/> from the save, -1 when none; a save restored on another phone is ignored).
        /// </summary>
        public static QualityTier AutoStartTier(QualityTier detected, int learnedCap, string learnedDevice, string deviceModel)
        {
            if (learnedCap < 0 || learnedCap >= (int)detected) return detected;
            if (!string.Equals(learnedDevice ?? "", deviceModel ?? "", StringComparison.Ordinal)) return detected;
            return QualityTiers.Clamp(learnedCap);
        }

        // ----- The save's settings section ----------------------------------------------------------------------

        /// <summary>The graphics choice in the save.</summary>
        public static GraphicsChoice Choice(SaveData.SettingsSection settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return Parse(settings.Graphics);
        }

        /// <summary>Records a graphics choice.</summary>
        public static void SetChoice(SaveData.SettingsSection settings, GraphicsChoice choice)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.Graphics = Key(choice);
        }

        /// <summary>Records a frame rate pick (30 or 60; 0 returns to the tier's default).</summary>
        public static void SetFrameRate(SaveData.SettingsSection settings, int fps)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.FrameRate = fps == 30 || fps == 60 ? fps : 0;
        }

        /// <summary>Remembers the tier the governor settled Auto on for this phone (-1 forgets it).</summary>
        public static void SetAutoTierCap(SaveData.SettingsSection settings, int tier, string deviceModel)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.AutoTierCap = tier < 0 ? -1 : (int)QualityTiers.Clamp(tier);
            settings.AutoTierDevice = tier < 0 ? "" : deviceModel ?? "";
        }
    }
}
