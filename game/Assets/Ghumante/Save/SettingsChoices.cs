using System;
using Ghumante.Core.Save;

namespace Ghumante.Save
{
    /// <summary>
    /// Settings that follow the device until the player picks them. The save cannot tell "the player chose
    /// English" from "English is the default", so a choice is marked in <c>Settings.Extra</c> (which every build
    /// keeps and writes back). Until then the game follows the device: its language, and its OS-wide reduce
    /// motion / remove animations accessibility setting.
    /// </summary>
    public static class SettingsChoices
    {
        public const string ReduceMotionChosenKey = "reduceMotionChosen";
        public const string LanguageChosenKey = "languageChosen";

        /// <summary>Reduce motion: the player's choice if they made one, else the OS preference.</summary>
        public static bool ReduceMotion(SaveData.SettingsSection settings, bool osPrefersReducedMotion)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return IsChosen(settings, ReduceMotionChosenKey) ? settings.ReduceMotion : osPrefersReducedMotion;
        }

        /// <summary>Records the player's Reduce motion choice.</summary>
        public static void SetReduceMotion(SaveData.SettingsSection settings, bool value)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.ReduceMotion = value;
            Extra(settings).Set(ReduceMotionChosenKey, true);
        }

        /// <summary>The UI language: the player's choice if they made one, else <paramref name="deviceLocale"/>.</summary>
        public static string Language(SaveData.SettingsSection settings, string deviceLocale)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (IsChosen(settings, LanguageChosenKey) && !string.IsNullOrEmpty(settings.Language)) return settings.Language;
            return deviceLocale;
        }

        /// <summary>Records the player's language choice ("en" / "ne").</summary>
        public static void SetLanguage(SaveData.SettingsSection settings, string locale)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (string.IsNullOrEmpty(locale)) throw new ArgumentException("locale is required", nameof(locale));
            settings.Language = locale;
            Extra(settings).Set(LanguageChosenKey, true);
        }

        public static bool IsChosen(SaveData.SettingsSection settings, string key)
        {
            return settings.Extra != null && settings.Extra.GetBool(key);
        }

        private static JsonObject Extra(SaveData.SettingsSection settings)
        {
            if (settings.Extra == null) settings.Extra = new JsonObject();
            return settings.Extra;
        }
    }
}
