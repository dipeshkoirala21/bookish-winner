using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ghumante.Core.Services;
using Ghumante.Platform.Regions;
using UnityEditor;
using UnityEngine;

namespace Ghumante.EditorTools
{
    /// <summary>
    /// Puts region packs where <see cref="BuiltInRegionSource"/> finds them: <c>Assets/StreamingAssets/Regions/&lt;id&gt;/</c>
    /// (git-ignored; DATA_FORMATS.md section 2 for the files).
    /// <list type="bullet">
    /// <item><b>Ghumante &gt; Import Region Pack...</b>: pick a pipeline output folder (<c>pipeline/build/regions/&lt;id&gt;</c>);
    /// its manifest and the files it lists (<c>.ghpk</c>, <c>.search.ghsi</c>, <c>.route.ghrg</c>) are checked against
    /// the manifest's sizes and SHA-256 and copied.</item>
    /// <item><see cref="EnsureSampleRegion"/> (Project Setup, and before every build): copies the committed sample
    /// <c>shared/sample-regions/kathmandu_core</c> when it is missing or out of date.</item>
    /// <item><see cref="RefreshIndex"/>: rewrites <c>Regions/regions.json</c>, the list Android reads because it cannot
    /// enumerate StreamingAssets inside the APK.</item>
    /// </list>
    /// </summary>
    public static class RegionImport
    {
        public const string SampleRegionId = "kathmandu_core";

        /// <summary>Project-relative folder of the installed regions.</summary>
        public const string RegionsAssetFolder = "Assets/StreamingAssets/" + RegionFiles.FolderName;

        /// <summary>The Unity project folder (<c>game/</c>).</summary>
        public static string ProjectFolder
        {
            get { return Path.GetDirectoryName(Path.GetFullPath(Application.dataPath)); }
        }

        /// <summary>The repository root (the folder above <c>game/</c>).</summary>
        public static string RepositoryFolder
        {
            get { return Path.GetDirectoryName(ProjectFolder); }
        }

        /// <summary><c>shared/sample-regions/kathmandu_core</c>.</summary>
        public static string SampleRegionSourceFolder
        {
            get { return Path.Combine(RepositoryFolder, "shared", "sample-regions", SampleRegionId); }
        }

        /// <summary>Absolute <c>Assets/StreamingAssets</c>, the packaged root BuiltInRegionSource reads.</summary>
        public static string StreamingAssetsFolder
        {
            get { return Path.Combine(Path.GetFullPath(Application.dataPath), "StreamingAssets"); }
        }

        [InitializeOnLoadMethod]
        private static void HookWorldPreview()
        {
            // Ghumante > World Preview opens the sample region: make sure it is in StreamingAssets first.
            Ghumante.World.EditorTools.WorldSetup.BeforePreview += () => EnsureSampleRegion();
        }

        [MenuItem("Ghumante/Import Region Pack...", priority = 10)]
        public static void ImportFromMenu()
        {
            string start = Path.Combine(RepositoryFolder, "pipeline", "build", "regions");
            if (!Directory.Exists(start)) start = RepositoryFolder;
            string folder = EditorUtility.OpenFolderPanel("Import region pack (pipeline/build/regions/<id>)", start, "");
            if (string.IsNullOrEmpty(folder)) return;
            try
            {
                string id = ImportFolder(folder);
                AssetDatabase.Refresh();
                string message = "Region '" + id + "' is in " + RegionsAssetFolder + "/" + id + ".\n\nOpen it with WorldRoot.OpenRegionAsync(\"" +
                                 id + "\") or Ghumante > World Preview.";
                Debug.Log("RegionImport: " + message);
                EditorUtility.DisplayDialog("Region imported", message, "OK");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Region import failed", e.Message, "OK");
            }
        }

        /// <summary>
        /// Verifies a region folder (one <c>*.manifest.json</c> plus the files it lists) and copies it into
        /// <see cref="RegionsAssetFolder"/>, replacing an older copy. Returns the region id. Throws with the reason when
        /// the folder is not a valid region. Call <see cref="AssetDatabase.Refresh()"/> afterwards when outside a build.
        /// </summary>
        public static string ImportFolder(string sourceFolder)
        {
            if (!Directory.Exists(sourceFolder)) throw new DirectoryNotFoundException(sourceFolder + " does not exist");
            string manifestPath = FindManifest(sourceFolder);
            string text = File.ReadAllText(manifestPath, Encoding.UTF8);
            RegionManifest manifest = RegionManifest.Parse(text);
            if (Path.GetFileName(manifestPath) != RegionFiles.FileName(manifest.RegionId, RegionFileKind.Manifest))
                throw new InvalidDataException(Path.GetFileName(manifestPath) + " describes region '" + manifest.RegionId + "'; expected " +
                                               RegionFiles.FileName(manifest.RegionId, RegionFileKind.Manifest));
            foreach (RegionFileEntry entry in manifest.Files)
            {
                string problem;
                if (!RegionFileVerifier.Check(Path.Combine(sourceFolder, entry.Path), entry, true, out problem))
                    throw new InvalidDataException("region '" + manifest.RegionId + "': " + problem);
            }
            foreach (RegionFileKind kind in RegionFiles.DataKinds)
            {
                if (manifest.FileFor(kind) == null)
                    Debug.LogWarning("RegionImport: region '" + manifest.RegionId + "' has no " + RegionFiles.FileName(manifest.RegionId, kind) +
                                     (kind == RegionFileKind.SearchIndex ? " (no search)" : kind == RegionFileKind.RouteGraph ? " (no routing)" : ""));
            }

            string destination = Path.Combine(StreamingAssetsFolder, RegionFiles.FolderName, manifest.RegionId);
            Directory.CreateDirectory(destination);
            // The manifest goes last, so an interrupted copy never looks complete.
            string destinationManifest = Path.Combine(destination, Path.GetFileName(manifestPath));
            if (File.Exists(destinationManifest)) File.Delete(destinationManifest);
            var keep = new HashSet<string>(StringComparer.Ordinal) { Path.GetFileName(manifestPath) };
            foreach (RegionFileEntry entry in manifest.Files)
            {
                File.Copy(Path.Combine(sourceFolder, entry.Path), Path.Combine(destination, entry.Path), true);
                keep.Add(entry.Path);
            }
            // Drop region files an older version listed but this one does not.
            foreach (string file in Directory.GetFiles(destination))
            {
                string name = Path.GetFileName(file);
                if (keep.Contains(name) || name.EndsWith(".meta", StringComparison.Ordinal)) continue;
                if (name.StartsWith(manifest.RegionId + ".", StringComparison.Ordinal)) File.Delete(file);
            }
            File.WriteAllText(destinationManifest, text, new UTF8Encoding(false));
            RefreshIndex();
            Debug.Log("RegionImport: imported region '" + manifest.RegionId + "' (" + (manifest.TotalBytes / 1048576.0).ToString("0.0") +
                      " MB) from " + sourceFolder);
            return manifest.RegionId;
        }

        /// <summary>
        /// Copies the committed sample region into StreamingAssets when it is missing or differs from the committed
        /// manifest (sizes are checked too). Returns true when it copied. <paramref name="refresh"/> refreshes the asset
        /// database afterwards (not during builds).
        /// </summary>
        public static bool EnsureSampleRegion(bool refresh = true)
        {
            string source = SampleRegionSourceFolder;
            string sourceManifest = Path.Combine(source, RegionFiles.FileName(SampleRegionId, RegionFileKind.Manifest));
            if (!File.Exists(sourceManifest))
            {
                Debug.LogWarning("RegionImport: sample region not found at " + source + "; Explore needs a region (Ghumante > Import Region Pack).");
                return false;
            }
            if (IsInstalled(SampleRegionId, File.ReadAllText(sourceManifest, Encoding.UTF8)))
            {
                RefreshIndex();
                return false;
            }
            ImportFolder(source);
            if (refresh) AssetDatabase.Refresh();
            return true;
        }

        /// <summary>True when the installed copy of <paramref name="regionId"/> has exactly this manifest and every listed
        /// file with its size.</summary>
        public static bool IsInstalled(string regionId, string expectedManifest)
        {
            string folder = Path.Combine(StreamingAssetsFolder, RegionFiles.FolderName, regionId);
            string manifestPath = Path.Combine(folder, RegionFiles.FileName(regionId, RegionFileKind.Manifest));
            if (!File.Exists(manifestPath)) return false;
            string text = File.ReadAllText(manifestPath, Encoding.UTF8);
            if (expectedManifest != null && text != expectedManifest) return false;
            try
            {
                RegionManifest manifest = RegionManifest.Parse(text);
                foreach (RegionFileEntry entry in manifest.Files)
                {
                    string problem;
                    if (!RegionFileVerifier.Check(Path.Combine(folder, entry.Path), entry, false, out problem)) return false;
                }
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        /// <summary>The ids of the regions in StreamingAssets (folders with their manifest).</summary>
        public static List<string> InstalledRegions()
        {
            var ids = new List<string>();
            string root = Path.Combine(StreamingAssetsFolder, RegionFiles.FolderName);
            if (!Directory.Exists(root)) return ids;
            foreach (string dir in Directory.GetDirectories(root))
            {
                string id = Path.GetFileName(dir);
                if (RegionFiles.IsValidRegionId(id) && File.Exists(Path.Combine(dir, RegionFiles.FileName(id, RegionFileKind.Manifest)))) ids.Add(id);
            }
            ids.Sort(StringComparer.Ordinal);
            return ids;
        }

        /// <summary>Rewrites <c>Regions/regions.json</c> when the installed regions changed.</summary>
        public static void RefreshIndex()
        {
            string root = Path.Combine(StreamingAssetsFolder, RegionFiles.FolderName);
            if (!Directory.Exists(root)) return;
            string path = Path.Combine(root, RegionFiles.IndexFileName);
            string json = RegionFiles.FormatIndex(InstalledRegions());
            if (File.Exists(path) && File.ReadAllText(path, Encoding.UTF8) == json) return;
            File.WriteAllText(path, json, new UTF8Encoding(false));
        }

        private static string FindManifest(string folder)
        {
            string preferred = Path.Combine(folder, Path.GetFileName(folder.TrimEnd('/', '\\')) + RegionFiles.Suffix(RegionFileKind.Manifest));
            if (File.Exists(preferred)) return preferred;
            string[] found = Directory.GetFiles(folder, "*" + RegionFiles.Suffix(RegionFileKind.Manifest));
            if (found.Length == 1) return found[0];
            if (found.Length == 0) throw new FileNotFoundException("no *.manifest.json in " + folder + " (pick pipeline/build/regions/<id>)");
            throw new InvalidDataException(found.Length + " manifests in " + folder + "; pick the folder of one region");
        }
    }
}
