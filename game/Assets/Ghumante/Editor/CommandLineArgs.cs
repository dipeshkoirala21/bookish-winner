using System;
using System.Collections.Generic;

namespace Ghumante.EditorTools
{
    /// <summary>
    /// "-flag value" command-line parsing shared by ProjectSetup and BuildScript. GameCI's unity-builder
    /// does not forward arbitrary environment variables into its Docker container, so CI passes settings
    /// such as the bundle id through customParameters (e.g. "-ghumanteBundleId com.example.app").
    /// </summary>
    public static class CommandLineArgs
    {
        private static readonly HashSet<string> Secrets = new HashSet<string>(StringComparer.Ordinal)
        {
            "androidKeystorePass", "androidKeyaliasName", "androidKeyaliasPass", "password", "serial", "username",
        };

        private static Dictionary<string, string> s_current;

        /// <summary>
        /// Parses "-flag value" pairs. A flag followed by another flag (or by nothing) gets an empty value.
        /// Values that start with '-' are not supported; GameCI never produces them.
        /// </summary>
        public static Dictionary<string, string> Parse(string[] argv, bool log)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < argv.Length; i++)
            {
                string a = argv[i];
                if (a.Length < 2 || a[0] != '-') continue;
                string key = a.TrimStart('-');
                string value = i + 1 < argv.Length && (argv[i + 1].Length == 0 || argv[i + 1][0] != '-') ? argv[++i] : "";
                result[key] = value;
                if (log)
                {
                    Console.WriteLine("args: -" + key + " " + (Secrets.Contains(key) ? "*****" : "\"" + value + "\""));
                }
            }
            return result;
        }

        /// <summary>Value of -<paramref name="key"/> on this editor's command line, or null if absent/empty.</summary>
        public static string Value(string key)
        {
            if (s_current == null) s_current = Parse(Environment.GetCommandLineArgs(), log: false);
            string value;
            return s_current.TryGetValue(key, out value) && !string.IsNullOrEmpty(value) ? value : null;
        }

        /// <summary>Environment variable first, then the command-line flag, then the fallback.</summary>
        public static string FromEnvironmentOrArgs(string environmentVariable, string flag, string fallback)
        {
            string fromEnv = Environment.GetEnvironmentVariable(environmentVariable);
            if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv.Trim();
            string fromArgs = Value(flag);
            return string.IsNullOrWhiteSpace(fromArgs) ? fallback : fromArgs.Trim();
        }
    }
}
