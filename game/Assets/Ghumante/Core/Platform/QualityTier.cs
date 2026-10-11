using System;

namespace Ghumante.Core.Platform
{
    /// <summary>
    /// Graphics tier of the device (ARCHITECTURE.md section 10), engine-free. The values match
    /// <c>Ghumante.Platform.DeviceTier</c> (Low 0, Mid 1, High 2), the quality level index that Project Setup creates and
    /// the integer tiers of <c>StreamingConfig.ForTier</c>, so a cast converts between them.
    /// </summary>
    public enum QualityTier
    {
        Low = 0,
        Mid = 1,
        High = 2,
    }

    /// <summary>Per-tier facts every quality decision shares: frame rates, names and keys. Pure.</summary>
    public static class QualityTiers
    {
        /// <summary>Number of tiers.</summary>
        public const int Count = 3;

        /// <summary>The tier of an integer, clamped to Low..High.</summary>
        public static QualityTier Clamp(int tier)
        {
            return tier <= 0 ? QualityTier.Low : tier >= 2 ? QualityTier.High : QualityTier.Mid;
        }

        /// <summary>The lower of two tiers.</summary>
        public static QualityTier Min(QualityTier a, QualityTier b)
        {
            return a <= b ? a : b;
        }

        /// <summary>
        /// Default target frame rate (ARCHITECTURE.md section 10 and P2): Low 30 fps, Mid 30 fps (60 is a player
        /// option, see <see cref="MaxFrameRate"/>), High 60 fps.
        /// </summary>
        public static int DefaultFrameRate(QualityTier tier)
        {
            return tier == QualityTier.High ? 60 : 30;
        }

        /// <summary>Highest frame rate the player may pick on a tier: 30 on Low, 60 on Mid and High.</summary>
        public static int MaxFrameRate(QualityTier tier)
        {
            return tier == QualityTier.Low ? 30 : 60;
        }

        /// <summary>Name of the matching quality level, as Project Setup creates it ("Low", "Medium", "High").</summary>
        public static string QualityLevelName(QualityTier tier)
        {
            switch (tier)
            {
                case QualityTier.Low: return "Low";
                case QualityTier.Mid: return "Medium";
                default: return "High";
            }
        }

        /// <summary>
        /// Lower-case key of a tier ("low", "medium", "high"): the save's value and the suffix of the
        /// <c>settings.quality.*</c> string keys.
        /// </summary>
        public static string Key(QualityTier tier)
        {
            switch (tier)
            {
                case QualityTier.Low: return "low";
                case QualityTier.Mid: return "medium";
                default: return "high";
            }
        }

        /// <summary>Parses <see cref="Key"/> (also "mid", any case); false for anything else.</summary>
        public static bool TryParseKey(string key, out QualityTier tier)
        {
            tier = QualityTier.Mid;
            if (string.IsNullOrEmpty(key)) return false;
            if (string.Equals(key, "low", StringComparison.OrdinalIgnoreCase))
            {
                tier = QualityTier.Low;
                return true;
            }
            if (string.Equals(key, "medium", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "mid", StringComparison.OrdinalIgnoreCase))
            {
                tier = QualityTier.Mid;
                return true;
            }
            if (string.Equals(key, "high", StringComparison.OrdinalIgnoreCase))
            {
                tier = QualityTier.High;
                return true;
            }
            return false;
        }
    }
}
