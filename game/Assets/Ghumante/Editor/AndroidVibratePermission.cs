using System;
using System.IO;
using System.Text;
using System.Xml;
using UnityEditor.Android;
using UnityEditor.Build;

namespace Ghumante.EditorTools
{
    /// <summary>
    /// Declares <c>android.permission.VIBRATE</c> in every Android build (ARCHITECTURE.md 7.9, UI/README.md
    /// "Haptics"). <c>AndroidHaptics</c> plays the bigger moments (MediumImpact and up: rewards, warnings) on the
    /// Vibrator with predefined VibrationEffects, which needs that permission; without it every kind falls back
    /// to View.performHapticFeedback, so a reward feels like a key tap and the touch-feedback switch silences it.
    /// Unity adds VIBRATE by itself only when a script calls <c>Handheld.Vibrate</c>, which the game does not,
    /// so this hook adds it to the generated unityLibrary manifest and Gradle's manifest merger carries it into
    /// the app. VIBRATE is a normal (install-time) permission: no runtime prompt, no Play Console declaration.
    /// <para>The build fails if the manifest is missing or unreadable, rather than shipping haptics that
    /// silently degrade. Idempotent: a manifest that already declares the permission is left untouched.</para>
    /// </summary>
    public sealed class AndroidVibratePermission : IPostGenerateGradleAndroidProject
    {
        public const string Permission = "android.permission.VIBRATE";

        private const string AndroidNamespace = "http://schemas.android.com/apk/res/android";

        /// <summary>Order among IPostGenerateGradleAndroidProject callbacks; nothing else depends on it.</summary>
        public int callbackOrder
        {
            get { return 0; }
        }

        /// <summary><paramref name="path"/> is the root of the generated unityLibrary Gradle module.</summary>
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                throw new BuildFailedException("AndroidVibratePermission: no unityLibrary manifest at " + manifestPath +
                                               "; cannot declare " + Permission + " (haptics would fall back).");
            }
            string xml = File.ReadAllText(manifestPath);
            string patched;
            try
            {
                patched = EnsurePermission(xml, Permission);
            }
            catch (Exception e) when (e is XmlException || e is FormatException)
            {
                throw new BuildFailedException("AndroidVibratePermission: cannot read " + manifestPath + ": " +
                                               e.Message);
            }
            if (!ReferenceEquals(patched, xml))
            {
                File.WriteAllText(manifestPath, patched, new UTF8Encoding(false));
            }
        }

        /// <summary>
        /// Returns <paramref name="manifestXml"/> with a <c>uses-permission</c> element for
        /// <paramref name="permission"/> inserted before <c>application</c> (formatting kept), or the very same
        /// string instance when the manifest already declares it. Throws <see cref="XmlException"/> for
        /// malformed XML and <see cref="FormatException"/> when the root element is not <c>manifest</c>.
        /// </summary>
        public static string EnsurePermission(string manifestXml, string permission)
        {
            if (manifestXml == null) throw new ArgumentNullException(nameof(manifestXml));
            if (string.IsNullOrEmpty(permission)) throw new ArgumentException("permission is empty", nameof(permission));

            var document = new XmlDocument { PreserveWhitespace = true };
            document.LoadXml(manifestXml);
            XmlElement manifest = document.DocumentElement;
            if (manifest == null || manifest.Name != "manifest")
            {
                throw new FormatException("not an AndroidManifest: the root element is " +
                                          (manifest == null ? "missing" : "<" + manifest.Name + ">"));
            }

            XmlElement application = null;
            foreach (XmlNode child in manifest.ChildNodes)
            {
                var element = child as XmlElement;
                if (element == null) continue;
                if (element.Name == "uses-permission" &&
                    element.GetAttribute("name", AndroidNamespace) == permission)
                {
                    return manifestXml;
                }
                if (application == null && element.Name == "application") application = element;
            }

            string prefix = manifest.GetPrefixOfNamespace(AndroidNamespace);
            if (string.IsNullOrEmpty(prefix)) prefix = "android";
            XmlElement uses = document.CreateElement("uses-permission");
            XmlAttribute name = document.CreateAttribute(prefix, "name", AndroidNamespace);
            name.Value = permission;
            uses.Attributes.Append(name);

            if (application != null)
            {
                // <uses-permission .../> then the indentation <application> had, so the file stays tidy.
                var indent = application.PreviousSibling as XmlWhitespace;
                manifest.InsertBefore(uses, application);
                manifest.InsertBefore(document.CreateWhitespace(indent != null ? indent.Value : "\n    "),
                                      application);
            }
            else
            {
                manifest.AppendChild(uses);
            }
            return document.OuterXml;
        }
    }
}
