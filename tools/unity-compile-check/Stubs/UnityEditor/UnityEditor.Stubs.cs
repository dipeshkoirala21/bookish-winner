// Hand-written declaration stubs for the part of the Unity 6.3 (6000.3) UnityEditor API that
// game/Assets/Ghumante/Editor and Tests/EditMode use. UnityEditor reference assemblies are not on NuGet,
// so the compile check builds the editor scripts against these declarations instead.
//
// Rules:
//   * Declare ONLY members our code uses, with the exact Unity 6.3 signature (names, parameter and return
//     types). Bodies throw: nothing here ever runs.
//   * Every stubbed member is verified against the Unity 6000.3 C# reference source by the API audit
//     (tools/unity-compile-check/run.sh --audit): it must exist there, with the same parameter types, and
//     must not be [Obsolete]. A stub cannot vouch for itself; the audit is what makes it trustworthy.
#pragma warning disable 1591
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class MenuItem : Attribute
    {
        public string menuItem;
        public bool validate;
        public int priority;
        public MenuItem(string itemName) { throw null; }
        public MenuItem(string itemName, bool isValidateFunction) { throw null; }
        public MenuItem(string itemName, bool isValidateFunction, int priority) { throw null; }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class InitializeOnLoadMethodAttribute : Attribute
    {
    }

    public sealed class EditorApplication
    {
        public static void Exit(int returnValue) { throw null; }
        public static bool isPlaying { get { throw null; } set { } }
        public static void EnterPlaymode() { throw null; }
    }

    public sealed class EditorUtility
    {
        public static void SetDirty(UnityEngine.Object target) { throw null; }
        public static string OpenFolderPanel(string title, string folder, string defaultName) { throw null; }
        public static bool DisplayDialog(string title, string message, string ok) { throw null; }
    }

    public sealed class AssetDatabase
    {
        public static T LoadAssetAtPath<T>(string assetPath) where T : UnityEngine.Object { throw null; }
        public static UnityEngine.Object[] LoadAllAssetsAtPath(string assetPath) { throw null; }
        public static void CreateAsset(UnityEngine.Object asset, string path) { throw null; }
        public static void SaveAssets() { throw null; }
        public static bool IsValidFolder(string path) { throw null; }
        public static string[] FindAssets(string filter, string[] searchInFolders) { throw null; }
        public static string GUIDToAssetPath(string guid) { throw null; }
        public static string CreateFolder(string parentFolder, string newFolderName) { throw null; }
        public static void Refresh() { throw null; }
    }

    public class EditorBuildSettingsScene
    {
        public EditorBuildSettingsScene(string path, bool enabled) { throw null; }
        public bool enabled { get { throw null; } set { } }
        public string path { get { throw null; } set { } }
    }

    public class EditorBuildSettings : UnityEngine.Object
    {
        public static EditorBuildSettingsScene[] scenes { get { throw null; } set { } }
    }

    // ----- Asset import --------------------------------------------------------------------------------

    public class AssetImporter : UnityEngine.Object
    {
        public string assetPath { get { throw null; } }
        public static AssetImporter GetAtPath(string path) { throw null; }
        public void SaveAndReimport() { throw null; }
    }

    public class AssetPostprocessor
    {
        public string assetPath { get { throw null; } set { } }
        public AssetImporter assetImporter { get { throw null; } }
        public virtual uint GetVersion() { throw null; }
    }

    public enum TextureImporterType
    {
        Default = 0,
        Sprite = 8,
    }

    public enum SpriteImportMode
    {
        None = 0,
        Single = 1,
        Multiple = 2,
        Polygon = 3,
    }

    public enum TextureImporterFormat
    {
        Automatic = -1,
        ASTC_4x4 = 48,
    }

    public sealed class TextureImporterPlatformSettings
    {
        public string name { get { throw null; } set { } }
        public bool overridden { get { throw null; } set { } }
        public TextureImporterFormat format { get { throw null; } set { } }
    }

    public sealed class TextureImporter : AssetImporter
    {
        public TextureImporterType textureType { get { throw null; } set { } }
        public SpriteImportMode spriteImportMode { get { throw null; } set { } }
        public bool mipmapEnabled { get { throw null; } set { } }
        public bool isReadable { get { throw null; } set { } }
        public bool alphaIsTransparency { get { throw null; } set { } }
        public TextureImporterPlatformSettings GetPlatformTextureSettings(string platform) { throw null; }
        public void SetPlatformTextureSettings(TextureImporterPlatformSettings platformSettings) { throw null; }
    }

    public enum SerializationMode
    {
        Mixed = 0,
        ForceBinary = 1,
        ForceText = 2,
    }

    public sealed class EditorSettings : UnityEngine.Object
    {
        public static SerializationMode serializationMode { get { throw null; } set { } }
    }

    public sealed class VersionControlSettings : UnityEngine.Object
    {
        public static string mode { get { throw null; } set { } }
    }

    public enum SerializedPropertyType
    {
        Generic = -1,
        Integer = 0,
        Boolean = 1,
        Float = 2,
        String = 3,
        Color = 4,
        ObjectReference = 5,
        LayerMask = 6,
        Enum = 7,
    }

    public class SerializedObject : IDisposable
    {
        public SerializedObject(UnityEngine.Object obj) { throw null; }
        public UnityEngine.Object targetObject { get { throw null; } }
        public SerializedProperty FindProperty(string propertyPath) { throw null; }
        public bool ApplyModifiedPropertiesWithoutUndo() { throw null; }
        public void Dispose() { throw null; }
    }

    public class SerializedProperty : IDisposable
    {
        public SerializedObject serializedObject { get { throw null; } }
        public string propertyPath { get { throw null; } }
        public SerializedPropertyType propertyType { get { throw null; } }
        public bool isArray { get { throw null; } }
        public int arraySize { get { throw null; } set { } }
        public int intValue { get { throw null; } set { } }
        public bool boolValue { get { throw null; } set { } }
        public float floatValue { get { throw null; } set { } }
        public string stringValue { get { throw null; } set { } }
        public UnityEngine.Object objectReferenceValue { get { throw null; } set { } }
        public SerializedProperty FindPropertyRelative(string relativePropertyPath) { throw null; }
        public SerializedProperty GetArrayElementAtIndex(int index) { throw null; }
        public void Dispose() { throw null; }
    }

    // ----- Build targets -------------------------------------------------------------------------------

    public enum BuildTarget
    {
        StandaloneOSX = 2,
        StandaloneWindows = 5,
        iOS = 9,
        Android = 13,
        StandaloneWindows64 = 19,
        WebGL = 20,
        StandaloneLinux64 = 24,
        NoTarget = -2,
    }

    public enum BuildTargetGroup
    {
        Unknown = 0,
        Standalone = 1,
        iOS = 4,
        Android = 7,
        WebGL = 13,
    }

    [Flags]
    public enum BuildOptions
    {
        None = 0,
        Development = 1 << 0,
        AutoRunPlayer = 1 << 2,
        ShowBuiltPlayer = 1 << 3,
        BuildAdditionalStreamedScenes = 1 << 4,
        AcceptExternalModificationsToPlayer = 1 << 5,
        ConnectWithProfiler = 1 << 8,
        AllowDebugging = 1 << 9,
        StrictMode = 1 << 21,
    }

    public enum MobileTextureSubtarget
    {
        Generic = 0,
        DXT = 1,
        PVRTC = 2,
        ETC = 4,
        ETC2 = 5,
        ASTC = 6,
    }

    public enum ScriptingImplementation
    {
        Mono2x = 0,
        IL2CPP = 1,
        WinRTDotNET = 2,
        CoreCLR = 3,
    }

    public enum ApiCompatibilityLevel
    {
        NET_Standard = 6,
        NET_Unity_4_8 = 3,
    }

    public enum ManagedStrippingLevel
    {
        Disabled = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Minimal = 4,
    }

    public enum UIOrientation
    {
        Portrait = 0,
        PortraitUpsideDown = 1,
        LandscapeRight = 2,
        LandscapeLeft = 3,
        AutoRotation = 4,
    }

    public enum AndroidSdkVersions
    {
        AndroidApiLevelAuto = 0,
        AndroidApiLevel25 = 25,
        AndroidApiLevel26 = 26,
        AndroidApiLevel27 = 27,
        AndroidApiLevel28 = 28,
        AndroidApiLevel29 = 29,
        AndroidApiLevel30 = 30,
        AndroidApiLevel31 = 31,
        AndroidApiLevel32 = 32,
        AndroidApiLevel33 = 33,
        AndroidApiLevel34 = 34,
        AndroidApiLevel35 = 35,
        AndroidApiLevel36 = 36,
        AndroidApiLevel37 = 37,
    }

    [Flags]
    public enum AndroidArchitecture : uint
    {
        None = 0,
        ARMv7 = 1 << 0,
        ARM64 = 1 << 1,
        X86_64 = 1 << 3,
        All = 0xffffffff,
    }

    [Flags]
    public enum AndroidApplicationEntry : uint
    {
        Activity = 1 << 0,
        GameActivity = 1 << 1,
    }

    public enum TextureCompressionFormat
    {
        Unknown = 0,
        ETC = 1,
        ETC2 = 2,
        ASTC = 3,
        DXTC = 5,
        BPTC = 6,
        DXTC_RGTC = 7,
    }

    public enum iOSSdkVersion
    {
        DeviceSDK = 988,
        SimulatorSDK = 989,
    }

    public sealed partial class PlayerSettings : UnityEngine.Object
    {
        public static string companyName { get { throw null; } set { } }
        public static string productName { get { throw null; } set { } }
        public static string bundleVersion { get { throw null; } set { } }
        public static ColorSpace colorSpace { get { throw null; } set { } }
        public static bool gcIncremental { get { throw null; } set { } }
        public static bool stripEngineCode { get { throw null; } set { } }
        public static bool enableFrameTimingStats { get { throw null; } set { } }
        public static UIOrientation defaultInterfaceOrientation { get { throw null; } set { } }
        public static bool allowedAutorotateToPortrait { get { throw null; } set { } }
        public static bool allowedAutorotateToPortraitUpsideDown { get { throw null; } set { } }
        public static bool allowedAutorotateToLandscapeRight { get { throw null; } set { } }
        public static bool allowedAutorotateToLandscapeLeft { get { throw null; } set { } }

        public static void SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget buildTarget, string identifier) { throw null; }
        public static void SetScriptingBackend(UnityEditor.Build.NamedBuildTarget buildTarget, ScriptingImplementation backend) { throw null; }
        public static void SetApiCompatibilityLevel(UnityEditor.Build.NamedBuildTarget buildTarget, ApiCompatibilityLevel value) { throw null; }
        public static void SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget buildTarget, ManagedStrippingLevel level) { throw null; }
        public static void SetUseDefaultGraphicsAPIs(BuildTarget platform, bool automatic) { throw null; }
        public static void SetGraphicsAPIs(BuildTarget platform, GraphicsDeviceType[] apis) { throw null; }

        public static partial class Android
        {
            public static AndroidSdkVersions minSdkVersion { get { throw null; } set { } }
            public static AndroidSdkVersions targetSdkVersion { get { throw null; } set { } }
            public static AndroidArchitecture targetArchitectures { get { throw null; } set { } }
            public static AndroidApplicationEntry applicationEntry { get { throw null; } set { } }
            public static bool optimizedFramePacing { get { throw null; } set { } }
            public static TextureCompressionFormat[] textureCompressionFormats { get { throw null; } set { } }
            public static int bundleVersionCode { get { throw null; } set { } }
            public static bool useCustomKeystore { get { throw null; } set { } }
            public static string keystoreName { get { throw null; } set { } }
            public static string keystorePass { get { throw null; } set { } }
            public static string keyaliasName { get { throw null; } set { } }
            public static string keyaliasPass { get { throw null; } set { } }
        }

        public static partial class iOS
        {
            public static string targetOSVersionString { get { throw null; } set { } }
            public static iOSSdkVersion sdkVersion { get { throw null; } set { } }
            public static string appleDeveloperTeamID { get { throw null; } set { } }
            public static string buildNumber { get { throw null; } set { } }
        }
    }

    public class EditorUserBuildSettings : UnityEngine.Object
    {
        public static MobileTextureSubtarget androidBuildSubtarget { get { throw null; } set { } }
        public static BuildTarget activeBuildTarget { get { throw null; } }
        public static bool exportAsGoogleAndroidProject { get { throw null; } set { } }
        public static bool buildAppBundle { get { throw null; } set { } }
        public static bool SwitchActiveBuildTarget(BuildTargetGroup targetGroup, BuildTarget target) { throw null; }
    }

    public struct BuildPlayerOptions
    {
        public string[] scenes { get { throw null; } set { } }
        public string locationPathName { get { throw null; } set { } }
        public BuildTargetGroup targetGroup { get { throw null; } set { } }
        public BuildTarget target { get { throw null; } set { } }
        public BuildOptions options { get { throw null; } set { } }
    }

    public class BuildPipeline
    {
        public static BuildTargetGroup GetBuildTargetGroup(BuildTarget platform) { throw null; }
        public static UnityEditor.Build.Reporting.BuildReport BuildPlayer(BuildPlayerOptions buildPlayerOptions) { throw null; }
    }
}

namespace UnityEditor.Build
{
    public readonly struct NamedBuildTarget : IEquatable<NamedBuildTarget>
    {
        public static readonly NamedBuildTarget Android;
        public static readonly NamedBuildTarget iOS;
        public string TargetName { get { throw null; } }
        public bool Equals(NamedBuildTarget other) { throw null; }
    }

    public interface IOrderedCallback
    {
        int callbackOrder { get; }
    }

    public interface IPreprocessBuildWithReport : IOrderedCallback
    {
        void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report);
    }

    public class BuildFailedException : Exception
    {
        public BuildFailedException(string message) { throw null; }
    }
}

namespace UnityEditor.Android
{
    // Declared in UnityEditor.CoreModule (Editor/Mono/BuildPipeline/Android), so it is available without the
    // Android build support module.
    public interface IPostGenerateGradleAndroidProject : UnityEditor.Build.IOrderedCallback
    {
        void OnPostGenerateGradleAndroidProject(string path);
    }
}

namespace UnityEditor.Build.Reporting
{
    public enum BuildResult
    {
        Unknown = 0,
        Succeeded = 1,
        Failed = 2,
        Cancelled = 3,
    }

    public struct BuildSummary
    {
        public string outputPath { get { throw null; } }
        public ulong totalSize { get { throw null; } }
        public TimeSpan totalTime { get { throw null; } }
        public int totalErrors { get { throw null; } }
        public int totalWarnings { get { throw null; } }
        public BuildResult result { get { throw null; } }
    }

    public sealed class BuildReport : UnityEngine.Object
    {
        public BuildSummary summary { get { throw null; } }
    }
}

namespace UnityEditor.SceneManagement
{
    public enum NewSceneMode
    {
        Single,
        Additive,
    }

    public enum NewSceneSetup
    {
        EmptyScene,
        DefaultGameObjects,
    }

    public sealed class EditorSceneManager : SceneManager
    {
        public static Scene NewScene(NewSceneSetup setup, NewSceneMode mode) { throw null; }
        public static bool SaveScene(Scene scene, string dstScenePath) { throw null; }
        public static bool SaveCurrentModifiedScenesIfUserWantsTo() { throw null; }
    }
}
