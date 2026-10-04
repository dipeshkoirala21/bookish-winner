using System;
using System.Globalization;
using UnityEngine;

namespace Ghumante.Platform
{
    /// <summary>
    /// The device's UI language as an ISO 639-1 code ("ne", "en", ...). Unity's
    /// <see cref="Application.systemLanguage"/> has no Nepali entry (a Nepali device reports Unknown), so
    /// Android asks java.util.Locale directly. Elsewhere the .NET UI culture is used, which IL2CPP derives
    /// from the OS locale. M1 adds an iOS native call (NSLocale.preferredLanguages) if the culture proves
    /// unreliable on device.
    /// </summary>
    public static class DeviceLocale
    {
        /// <summary>Lower-case ISO 639-1 code, or an empty string when unknown.</summary>
        public static string LanguageCode()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var localeClass = new AndroidJavaClass("java.util.Locale"))
                using (AndroidJavaObject locale = localeClass.CallStatic<AndroidJavaObject>("getDefault"))
                {
                    string language = locale.Call<string>("getLanguage");
                    if (!string.IsNullOrEmpty(language)) return language.ToLowerInvariant();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("DeviceLocale: java.util.Locale lookup failed: " + e.Message);
            }
#endif
            try
            {
                string code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                if (!string.IsNullOrEmpty(code) && code != "iv") return code.ToLowerInvariant();
            }
            catch (CultureNotFoundException)
            {
                // Invariant-globalization players: fall through.
            }
            return Application.systemLanguage == SystemLanguage.English ? "en" : string.Empty;
        }
    }
}
