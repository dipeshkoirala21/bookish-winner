#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine;

namespace Ghumante.Platform.Native
{
    /// <summary>Small JNI helpers shared by the Android haptics and accessibility code.</summary>
    internal static class AndroidPlatform
    {
        /// <summary>android.os.Build.VERSION.SDK_INT.</summary>
        internal static int SdkInt()
        {
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                return version.GetStatic<int>("SDK_INT");
            }
        }

        /// <summary>
        /// The current activity as a new reference that the caller owns and must dispose, or null.
        /// <para>Unity 6000.3 also has <c>UnityEngine.Android.AndroidApplication.currentActivity</c> (verified in
        /// the 6000.3 reference source; it returns a Unity-owned object that must not be disposed), but the
        /// compile check's reference assemblies (UnityEngine.Modules 2021.3) do not declare it and its
        /// Unity6 stub project has no entry for it, so this reads the long-standing static field
        /// <c>com.unity3d.player.UnityPlayer.currentActivity</c>, which Unity 6 still sets for both the
        /// Activity and the GameActivity entry points.</para>
        /// </summary>
        internal static AndroidJavaObject CurrentActivity()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                return player.GetStatic<AndroidJavaObject>("currentActivity");
            }
        }
    }
}
#endif
