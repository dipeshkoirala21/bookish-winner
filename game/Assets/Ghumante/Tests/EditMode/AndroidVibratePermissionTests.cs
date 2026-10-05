using System;
using System.IO;
using System.Xml;
using Ghumante.EditorTools;
using NUnit.Framework;
using UnityEditor.Android;
using UnityEditor.Build;

namespace Ghumante.Tests.EditMode
{
    public class AndroidVibratePermissionTests
    {
        private const string AndroidNamespace = "http://schemas.android.com/apk/res/android";

        // The shape of the unityLibrary manifest Unity 6 generates (trimmed).
        private const string UnityLibraryManifest =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\" " +
            "xmlns:tools=\"http://schemas.android.com/tools\">\n" +
            "  <uses-permission android:name=\"android.permission.INTERNET\" />\n" +
            "  <application android:extractNativeLibs=\"true\">\n" +
            "    <activity android:name=\"com.unity3d.player.UnityPlayerGameActivity\" android:exported=\"true\" />\n" +
            "  </application>\n" +
            "</manifest>\n";

        private static int CountPermission(string manifestXml, string permission)
        {
            var document = new XmlDocument();
            document.LoadXml(manifestXml);
            int count = 0;
            foreach (XmlNode node in document.DocumentElement.ChildNodes)
            {
                var element = node as XmlElement;
                if (element != null && element.Name == "uses-permission" &&
                    element.GetAttribute("name", AndroidNamespace) == permission)
                {
                    count++;
                }
            }
            return count;
        }

        [Test]
        public void DeclaresVibrateInTheGeneratedManifest()
        {
            // Regression: nothing declared VIBRATE, so AndroidHaptics never reached the Vibrator on any device.
            string patched = AndroidVibratePermission.EnsurePermission(UnityLibraryManifest, AndroidVibratePermission.Permission);
            Assert.AreEqual(1, CountPermission(patched, "android.permission.VIBRATE"));
            Assert.AreEqual(1, CountPermission(patched, "android.permission.INTERNET"), "existing permissions kept");
            StringAssert.Contains("<uses-permission android:name=\"android.permission.VIBRATE\" />\n  <application",
                                  patched, "android: prefix, before <application>, same indentation");
            StringAssert.Contains("UnityPlayerGameActivity", patched);
            StringAssert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", patched);
        }

        [Test]
        public void IsIdempotent()
        {
            string once = AndroidVibratePermission.EnsurePermission(UnityLibraryManifest, AndroidVibratePermission.Permission);
            string twice = AndroidVibratePermission.EnsurePermission(once, AndroidVibratePermission.Permission);
            Assert.AreSame(once, twice, "an already declared permission leaves the manifest untouched");
            Assert.AreEqual(1, CountPermission(twice, AndroidVibratePermission.Permission));
        }

        [Test]
        public void WorksWithoutAnApplicationElement()
        {
            const string bare = "<manifest xmlns:a=\"http://schemas.android.com/apk/res/android\"></manifest>";
            string patched = AndroidVibratePermission.EnsurePermission(bare, AndroidVibratePermission.Permission);
            Assert.AreEqual(1, CountPermission(patched, AndroidVibratePermission.Permission));
            StringAssert.Contains("a:name=", patched, "the manifest's own prefix for the android namespace");
        }

        [Test]
        public void RejectsSomethingThatIsNotAManifest()
        {
            Assert.Throws<FormatException>(() => AndroidVibratePermission.EnsurePermission("<resources />", "x"));
            Assert.Throws<XmlException>(() => AndroidVibratePermission.EnsurePermission("<manifest", "x"));
        }

        [Test]
        public void PatchesTheUnityLibraryManifestOnDisk()
        {
            IPostGenerateGradleAndroidProject hook = new AndroidVibratePermission();
            string root = Path.Combine(Path.GetTempPath(), "ghumante-gradle-" + Guid.NewGuid().ToString("N"));
            string main = Path.Combine(root, "src", "main");
            try
            {
                Directory.CreateDirectory(main);
                string manifestPath = Path.Combine(main, "AndroidManifest.xml");
                File.WriteAllText(manifestPath, UnityLibraryManifest);

                hook.OnPostGenerateGradleAndroidProject(root);
                Assert.AreEqual(1, CountPermission(File.ReadAllText(manifestPath), AndroidVibratePermission.Permission));

                hook.OnPostGenerateGradleAndroidProject(root);
                Assert.AreEqual(1, CountPermission(File.ReadAllText(manifestPath), AndroidVibratePermission.Permission),
                                "a second build does not add it twice");

                File.Delete(manifestPath);
                Assert.Throws<BuildFailedException>(() => hook.OnPostGenerateGradleAndroidProject(root),
                                                    "a missing manifest fails the build");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
