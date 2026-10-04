using System;
using System.IO;
using System.Text.Json;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>Access to shared/golden (written by shared/golden/make_golden.py) and shared/enums.json.</summary>
    internal static class GoldenFiles
    {
        private static string _root;

        public static string RepoRoot
        {
            get
            {
                if (_root != null) return _root;
                var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
                while (dir != null && !File.Exists(Path.Combine(dir.FullName, "shared", "enums.json"))) dir = dir.Parent;
                if (dir == null) throw new InvalidOperationException("repository root (shared/enums.json) not found");
                _root = dir.FullName;
                return _root;
            }
        }

        public static string PathOf(string name)
        {
            return Path.Combine(RepoRoot, "shared", "golden", name);
        }

        public static byte[] Bytes(string name)
        {
            string p = PathOf(name);
            if (!File.Exists(p)) Assert.Fail(name + " missing: run python3 shared/golden/make_golden.py");
            return File.ReadAllBytes(p);
        }

        public static JsonElement Json(string name)
        {
            using (var doc = JsonDocument.Parse(File.ReadAllText(PathOf(name))))
                return doc.RootElement.Clone();
        }

        public static JsonElement SharedJson(string name)
        {
            using (var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot, "shared", name))))
                return doc.RootElement.Clone();
        }

        public static int[] Ints(JsonElement e)
        {
            var a = new int[e.GetArrayLength()];
            int i = 0;
            foreach (JsonElement x in e.EnumerateArray()) a[i++] = x.GetInt32();
            return a;
        }

        public static long[] Longs(JsonElement e)
        {
            var a = new long[e.GetArrayLength()];
            int i = 0;
            foreach (JsonElement x in e.EnumerateArray()) a[i++] = x.GetInt64();
            return a;
        }

        public static string[] Name(JsonElement e)
        {
            if (e.ValueKind == JsonValueKind.Null) return null;
            var a = new string[3];
            int i = 0;
            foreach (JsonElement x in e.EnumerateArray()) a[i++] = x.GetString();
            return a;
        }

        public static void AssertName(JsonElement expected, Data.NameRecord actual, string what)
        {
            string[] n = Name(expected);
            if (n == null)
            {
                Assert.That(actual, Is.Null, what);
                return;
            }
            Assert.That(actual, Is.Not.Null, what);
            Assert.That(new[] { actual.Default, actual.En, actual.Ne }, Is.EqualTo(n), what);
        }
    }
}
