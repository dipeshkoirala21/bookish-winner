#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ghumante.World.Debugging
{
    /// <summary>
    /// The pixels of the screenshot tour (docs/images/README.md): a camera render target with optional supersampling,
    /// the read-back to a texture of the exact output size, JPEG files, the output folder, and hiding the debug-only UI
    /// for shots that include the HUD. Editor and development builds only; main thread.
    /// </summary>
    public static class ScreenshotTourCapture
    {
        /// <summary>JPEG quality of every shot (0-100).</summary>
        public const int DefaultJpegQuality = 92;

        /// <summary>Folder (under the repository's docs/images) the tour writes to in the editor.</summary>
        public const string GameFolderName = "game";

        /// <summary>Folder (under the output folder) for single F12 shots.</summary>
        public const string ExtraFolderName = "shots";

        /// <summary>Names of the debug-only UI Toolkit elements hidden in HUD screenshots: the DebugHud overlay and the
        /// HUD's development time slider.</summary>
        public static readonly string[] DebugElementNames = { "gh-debug-hud", "hud-debug" };

        /// <summary>
        /// Where shots go by default: <c>&lt;repo&gt;/docs/images/game</c> in the editor (the project is
        /// <c>&lt;repo&gt;/game</c>), <c>&lt;persistentDataPath&gt;/Screenshots</c> in a development build.
        /// </summary>
        public static string DefaultFolder()
        {
            if (Application.isEditor)
            {
                return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "images", GameFolderName));
            }
            return Path.Combine(Application.persistentDataPath, "Screenshots");
        }

        /// <summary>
        /// A colour + depth target for a camera: <paramref name="width"/> x <paramref name="height"/> times the
        /// supersampling factor (1, 2 or 4; other values round down), 8-bit sRGB, 4x MSAA when not supersampled.
        /// </summary>
        public static RenderTexture CreateTarget(int width, int height, int superSample)
        {
            int ss = ScreenshotFraming.SuperSampleFactor(superSample);
            var rt = new RenderTexture(width * ss, height * ss, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "Screenshot Tour Target",
                antiAliasing = ss == 1 ? 4 : 1,
            };
            rt.Create();
            return rt;
        }

        /// <summary>
        /// Reads <paramref name="source"/> (the output size times 1, 2 or 4) back as a <paramref name="width"/> x
        /// <paramref name="height"/> RGB texture: halving blits (2x2 box filter each, which also resolves MSAA) down to
        /// the output size, then one ReadPixels. The caller destroys the texture.
        /// </summary>
        public static Texture2D ReadBack(RenderTexture source, int width, int height)
        {
            RenderTexture current = source;
            RenderTexture temp = null;
            while (current.width > width || current.antiAliasing > 1)
            {
                int w = Math.Max(width, current.width / 2), h = Math.Max(height, current.height / 2);
                RenderTexture next = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(current, next);
                if (temp != null) RenderTexture.ReleaseTemporary(temp);
                current = temp = next;
            }
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = current;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            RenderTexture.active = previous;
            if (temp != null) RenderTexture.ReleaseTemporary(temp);
            return tex;
        }

        /// <summary>Encodes <paramref name="tex"/> as a JPEG at <c>folder/fileName</c> (folders created) and returns the path.</summary>
        public static string WriteJpeg(Texture2D tex, string folder, string fileName, int quality)
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, fileName);
            File.WriteAllBytes(path, tex.EncodeToJPG(Mathf.Clamp(quality, 1, 100)));
            return path;
        }

        /// <summary>
        /// Hides every visible debug-only element (<see cref="DebugElementNames"/>) of every UIDocument and adds it to
        /// <paramref name="hidden"/>; <see cref="ShowDebugUi"/> brings them back.
        /// </summary>
        public static void HideDebugUi(List<VisualElement> hidden)
        {
            UIDocument[] docs = UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            for (int d = 0; d < docs.Length; d++)
            {
                VisualElement root = docs[d].rootVisualElement;
                if (root == null) continue;
                for (int n = 0; n < DebugElementNames.Length; n++)
                {
                    VisualElement e = root.Q(DebugElementNames[n]);
                    if (e == null || e.resolvedStyle.display == DisplayStyle.None) continue;
                    e.style.display = DisplayStyle.None;
                    hidden.Add(e);
                }
            }
        }

        /// <summary>Undoes <see cref="HideDebugUi"/> and clears the list.</summary>
        public static void ShowDebugUi(List<VisualElement> hidden)
        {
            for (int i = 0; i < hidden.Count; i++)
                if (hidden[i] != null) hidden[i].style.display = StyleKeyword.Null;
            hidden.Clear();
        }

        /// <summary>The orientation of the screen now (portrait when taller than wide).</summary>
        public static ShotOrientation ScreenOrientation()
        {
            return Screen.height > Screen.width ? ShotOrientation.Portrait : ShotOrientation.Landscape;
        }
    }
}
#endif
