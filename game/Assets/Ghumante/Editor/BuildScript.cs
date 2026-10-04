using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Ghumante.EditorTools
{
    /// <summary>
    /// Player builds for Android (AAB) and iOS (Xcode project). Compatible with GameCI's unity-builder
    /// <c>buildMethod</c> (it passes -customBuildTarget, -customBuildPath, -buildVersion, -androidVersionCode,
    /// -androidKeystoreName/-Pass, -androidKeyaliasName/-Pass, -androidExportType, -androidSymbolType) and
    /// runnable locally:
    ///
    ///   Unity -batchmode -quit -projectPath game -buildTarget Android \
    ///         -executeMethod Ghumante.EditorTools.BuildScript.BuildFromCommandLine \
    ///         -customBuildPath Builds/Android/Ghumante.aab -buildVersion 0.1.0 -androidVersionCode 1
    ///
    /// Signing (Android): GameCI decodes its androidKeystoreBase64 input into the project folder and passes
    /// the name. Without GameCI, set ANDROID_KEYSTORE_BASE64, ANDROID_KEYSTORE_PASS, ANDROID_KEYALIAS_NAME
    /// and ANDROID_KEYALIAS_PASS. With neither, the AAB is signed with Unity's debug key: fine for
    /// bundletool/device testing, rejected by Play Console. iOS signing happens later, in fastlane.
    ///
    /// Exit codes (batch mode): 0 ok, 101 failed, 102 cancelled, 103 unknown, 110 bad arguments.
    /// </summary>
    public static class BuildScript
    {
        public const string DefaultAndroidPath = "Builds/Android/Ghumante.aab";
        public const string DefaultIosPath = "Builds/iOS";
        private const string KeystoreFromEnvPath = "Temp/ghumante-upload.keystore";

        private static readonly HashSet<string> SecretArgs = new HashSet<string>(StringComparer.Ordinal)
        {
            "androidKeystorePass", "androidKeyaliasName", "androidKeyaliasPass",
        };

        [MenuItem("Ghumante/Build/Android App Bundle", priority = 100)]
        public static void BuildAndroidFromMenu()
        {
            Build(BuildTarget.Android, DefaultAndroidPath, new Dictionary<string, string>(), exitWhenDone: false);
        }

        [MenuItem("Ghumante/Build/iOS Xcode Project", priority = 101)]
        public static void BuildIosFromMenu()
        {
            Build(BuildTarget.iOS, DefaultIosPath, new Dictionary<string, string>(), exitWhenDone: false);
        }

        /// <summary>-executeMethod entry with default output path.</summary>
        public static void BuildAndroid()
        {
            Dictionary<string, string> args = ParseArgs(Environment.GetCommandLineArgs());
            Build(BuildTarget.Android, Get(args, "customBuildPath") ?? DefaultAndroidPath, args, Application.isBatchMode);
        }

        /// <summary>-executeMethod entry with default output path.</summary>
        public static void BuildIOS()
        {
            Dictionary<string, string> args = ParseArgs(Environment.GetCommandLineArgs());
            Build(BuildTarget.iOS, Get(args, "customBuildPath") ?? DefaultIosPath, args, Application.isBatchMode);
        }

        /// <summary>GameCI customBuildMethod entry: the target comes from -customBuildTarget or -buildTarget.</summary>
        public static void BuildFromCommandLine()
        {
            Dictionary<string, string> args = ParseArgs(Environment.GetCommandLineArgs());
            string targetName = Get(args, "customBuildTarget") ?? Get(args, "buildTarget");
            BuildTarget target;
            if (string.IsNullOrEmpty(targetName))
            {
                target = EditorUserBuildSettings.activeBuildTarget;
            }
            else if (!TryParseTarget(targetName, out target))
            {
                Console.WriteLine("BuildScript: unknown build target '" + targetName + "'. Use Android or iOS.");
                EditorApplication.Exit(110);
                return;
            }
            if (target != BuildTarget.Android && target != BuildTarget.iOS)
            {
                Console.WriteLine("BuildScript: only Android and iOS are supported, got " + target + ".");
                EditorApplication.Exit(110);
                return;
            }
            string path = Get(args, "customBuildPath") ?? (target == BuildTarget.Android ? DefaultAndroidPath : DefaultIosPath);
            Build(target, path, args, exitWhenDone: true);
        }

        // ---------------------------------------------------------------------------------------------

        private static void Build(BuildTarget target, string outputPath, Dictionary<string, string> args, bool exitWhenDone)
        {
            BuildResult result;
            try
            {
                ProjectSetup.Apply();
                ApplyVersion(target, args);
                bool development = args.ContainsKey("developmentBuild") ||
                                   Environment.GetEnvironmentVariable("GHUMANTE_DEVELOPMENT_BUILD") == "1";

                BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
                if (EditorUserBuildSettings.activeBuildTarget != target)
                {
                    EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
                }

                if (target == BuildTarget.Android)
                {
                    outputPath = ConfigureAndroid(outputPath, args, development);
                }

                string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                if (scenes.Length == 0)
                {
                    throw new InvalidOperationException("No enabled scenes in the build settings.");
                }

                string directory = target == BuildTarget.iOS ? outputPath : Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = target,
                    targetGroup = group,
                    // StrictMode: any error logged during the build fails it, so CI never ships a half build.
                    options = BuildOptions.StrictMode |
                              (development ? BuildOptions.Development | BuildOptions.ConnectWithProfiler : BuildOptions.None),
                };

                Console.WriteLine("BuildScript: building " + target + " " + PlayerSettings.bundleVersion +
                                  (development ? " (development)" : "") + " -> " + outputPath);
                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;
                result = summary.result;
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "BuildScript: {0} in {1:0.0} s, {2} errors, {3} warnings, {4:0.0} MB at {5}",
                    summary.result, summary.totalTime.TotalSeconds, summary.totalErrors, summary.totalWarnings,
                    summary.totalSize / 1048576.0, summary.outputPath));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                result = BuildResult.Failed;
            }
            finally
            {
                // Never leave the upload key's passwords in ProjectSettings after a CI build.
                if (Application.isBatchMode) ClearAndroidSigningSecrets();
            }

            if (!exitWhenDone)
            {
                if (result != BuildResult.Succeeded) Debug.LogError("BuildScript: build " + result);
                return;
            }
            switch (result)
            {
                case BuildResult.Succeeded: EditorApplication.Exit(0); break;
                case BuildResult.Failed: EditorApplication.Exit(101); break;
                case BuildResult.Cancelled: EditorApplication.Exit(102); break;
                default: EditorApplication.Exit(103); break;
            }
        }

        private static void ApplyVersion(BuildTarget target, Dictionary<string, string> args)
        {
            string version = Get(args, "buildVersion");
            if (!string.IsNullOrEmpty(version) && version != "none")
            {
                PlayerSettings.bundleVersion = version;
            }

            string codeText = Get(args, "androidVersionCode") ?? Environment.GetEnvironmentVariable("GHUMANTE_VERSION_CODE");
            int code;
            if (!string.IsNullOrEmpty(codeText) && int.TryParse(codeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out code) && code > 0)
            {
                PlayerSettings.Android.bundleVersionCode = code;
                // The same monotonically increasing number works as the iOS build number (CFBundleVersion).
                PlayerSettings.iOS.buildNumber = code.ToString(CultureInfo.InvariantCulture);
            }
            else if (target == BuildTarget.Android)
            {
                Debug.LogWarning("BuildScript: no -androidVersionCode; keeping " + PlayerSettings.Android.bundleVersionCode +
                                 ". Play Console rejects a version code it has already seen.");
            }
        }

        private static string ConfigureAndroid(string outputPath, Dictionary<string, string> args, bool development)
        {
            string exportType = Get(args, "androidExportType") ?? "androidAppBundle";
            bool appBundle = exportType != "androidPackage";
            EditorUserBuildSettings.exportAsGoogleAndroidProject = exportType == "androidStudioProject";
            EditorUserBuildSettings.buildAppBundle = appBundle && exportType != "androidStudioProject";
            string wanted = EditorUserBuildSettings.buildAppBundle ? ".aab" : exportType == "androidPackage" ? ".apk" : "";
            if (wanted.Length > 0 && !outputPath.EndsWith(wanted, StringComparison.OrdinalIgnoreCase))
            {
                outputPath = Path.ChangeExtension(outputPath, wanted);
            }

            string targetSdk = Get(args, "androidTargetSdkVersion");
            AndroidSdkVersions sdk;
            if (!string.IsNullOrEmpty(targetSdk) && Enum.TryParse(targetSdk, out sdk))
            {
                PlayerSettings.Android.targetSdkVersion = sdk;
            }

            ConfigureAndroidSigning(args);
            SetAndroidDebugSymbols(Get(args, "androidSymbolType") ?? (development ? "debugging" : "public"));
            return outputPath;
        }

        private static void ConfigureAndroidSigning(Dictionary<string, string> args)
        {
            string keystore = Get(args, "androidKeystoreName");
            string base64 = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_BASE64");
            if (string.IsNullOrEmpty(keystore) && !string.IsNullOrEmpty(base64))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(KeystoreFromEnvPath));
                File.WriteAllBytes(KeystoreFromEnvPath, Convert.FromBase64String(base64.Trim()));
                keystore = Path.GetFullPath(KeystoreFromEnvPath);
            }

            if (string.IsNullOrEmpty(keystore))
            {
                PlayerSettings.Android.useCustomKeystore = false;
                Debug.LogWarning("BuildScript: no upload keystore configured; signing with the debug key. " +
                                 "Play Console will reject this AAB (see docs/CI_SECRETS.md).");
                return;
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = Get(args, "androidKeystorePass") ?? Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PASS") ?? "";
            PlayerSettings.Android.keyaliasName = Get(args, "androidKeyaliasName") ?? Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_NAME") ?? "";
            PlayerSettings.Android.keyaliasPass = Get(args, "androidKeyaliasPass") ?? Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_PASS") ?? "";
            if (PlayerSettings.Android.keystorePass.Length == 0 || PlayerSettings.Android.keyaliasName.Length == 0)
            {
                throw new InvalidOperationException("Android keystore given but its password or alias is missing.");
            }
        }

        private static void ClearAndroidSigningSecrets()
        {
            PlayerSettings.Android.keystorePass = "";
            PlayerSettings.Android.keyaliasPass = "";
            if (File.Exists(KeystoreFromEnvPath)) File.Delete(KeystoreFromEnvPath);
        }

        /// <summary>
        /// Native symbols for Play Console crash symbolication. Unity 6 moved this setting to
        /// UnityEditor.Android.UserBuildSettings.DebugSymbols.level (in the Android module, which is only
        /// present when Android support is installed), so it is set by reflection, as GameCI does.
        /// </summary>
        private static void SetAndroidDebugSymbols(string symbolType)
        {
            string level = symbolType == "none" ? "None" : symbolType == "debugging" ? "Full" : "SymbolTable";
            Type settings = Type.GetType("UnityEditor.Android.UserBuildSettings+DebugSymbols, UnityEditor.Android.Extensions");
            Type levelEnum = Type.GetType("Unity.Android.Types.DebugSymbolLevel, Unity.Android.Types");
            PropertyInfo property = settings == null ? null : settings.GetProperty("level", BindingFlags.Static | BindingFlags.Public);
            if (property == null || levelEnum == null)
            {
                Debug.LogWarning("BuildScript: Android debug symbol setting not found; leaving it unchanged.");
                return;
            }
            property.SetValue(null, Enum.Parse(levelEnum, level), null);
        }

        // ----- Arguments -------------------------------------------------------------------------------

        /// <summary>
        /// Parses "-flag value" pairs. A flag followed by another flag (or nothing) gets an empty value.
        /// Values may legitimately start with '-' only if quoted by the caller; GameCI never does that.
        /// </summary>
        public static Dictionary<string, string> ParseArgs(string[] argv)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < argv.Length; i++)
            {
                string a = argv[i];
                if (a.Length < 2 || a[0] != '-') continue;
                string key = a.TrimStart('-');
                string value = i + 1 < argv.Length && (argv[i + 1].Length == 0 || argv[i + 1][0] != '-') ? argv[++i] : "";
                result[key] = value;
                Console.WriteLine("BuildScript: -" + key + " " + (SecretArgs.Contains(key) ? "*****" : "\"" + value + "\""));
            }
            return result;
        }

        private static string Get(Dictionary<string, string> args, string key)
        {
            string value;
            return args.TryGetValue(key, out value) && !string.IsNullOrEmpty(value) ? value : null;
        }

        private static bool TryParseTarget(string name, out BuildTarget target)
        {
            if (string.Equals(name, "iOS", StringComparison.OrdinalIgnoreCase))
            {
                target = BuildTarget.iOS;
                return true;
            }
            return Enum.TryParse(name, true, out target);
        }
    }
}
