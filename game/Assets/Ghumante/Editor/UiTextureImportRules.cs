using System;
using UnityEditor;

namespace Ghumante.EditorTools
{
    /// <summary>
    /// Import rules for UI textures (ARCHITECTURE.md section 10: "ASTC ... 4x4 for UI and faces. Mipmaps are
    /// disabled on UI. Read/Write is never enabled."). Enforced on every import, so the rules hold on a fresh
    /// checkout without .meta files and cannot drift through a hand-edited .meta; otherwise the PNGs under
    /// <see cref="UiFolder"/> would import as Default textures with mipmaps and the default ASTC block size.
    /// </summary>
    public sealed class UiTextureImportRules : AssetPostprocessor
    {
        /// <summary>Every texture under this folder is a UI texture.</summary>
        public const string UiFolder = "Assets/Ghumante/UI/";

        /// <summary>Platform names whose overrides are forced to <see cref="UiFormat"/>.</summary>
        public static readonly string[] MobilePlatforms = { "Android", "iPhone" };

        public const TextureImporterFormat UiFormat = TextureImporterFormat.ASTC_4x4;

        /// <summary>Bump when the rules change so Unity reimports textures from a cached Library.</summary>
        public const uint RulesVersion = 1;

        public override uint GetVersion()
        {
            return RulesVersion;
        }

        /// <summary>True when <paramref name="path"/> is a texture the UI rules apply to.</summary>
        public static bool AppliesTo(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   path.Replace('\\', '/').StartsWith(UiFolder, StringComparison.Ordinal);
        }

        private void OnPreprocessTexture()
        {
            if (!AppliesTo(assetPath)) return;
            var importer = assetImporter as TextureImporter;
            if (importer != null) Configure(importer);
        }

        /// <summary>Applies the UI rules to an importer.</summary>
        public static void Configure(TextureImporter importer)
        {
            if (importer == null) throw new ArgumentNullException(nameof(importer));
            importer.textureType = TextureImporterType.Sprite;  // "Sprite (2D and UI)"
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.alphaIsTransparency = true;
            foreach (string platform in MobilePlatforms)
            {
                TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
                settings.overridden = true;
                settings.format = UiFormat;
                importer.SetPlatformTextureSettings(settings);
            }
        }
    }
}
