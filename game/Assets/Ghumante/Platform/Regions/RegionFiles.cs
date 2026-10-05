using System;
using System.Collections.Generic;
using System.Text;
using Ghumante.Core.Save;
using Ghumante.Core.Services;

namespace Ghumante.Platform.Regions
{
    /// <summary>
    /// File layout of installed regions (DATA_FORMATS.md section 2). A region <c>id</c> lives in
    /// <c>&lt;root&gt;/Regions/&lt;id&gt;/</c> as <c>&lt;id&gt;.manifest.json</c>, <c>&lt;id&gt;.ghpk</c>,
    /// <c>&lt;id&gt;.search.ghsi</c> and <c>&lt;id&gt;.route.ghrg</c>; <c>&lt;root&gt;/Regions/regions.json</c> lists the
    /// region ids (needed where the root cannot be enumerated, such as StreamingAssets inside an Android APK).
    /// Roots may be local folders or URLs (<c>jar:file://...</c>), so paths are joined with '/'.
    /// Engine-free: no UnityEngine types.
    /// </summary>
    public static class RegionFiles
    {
        /// <summary>Folder under a root (StreamingAssets, persistent data) holding one folder per region.</summary>
        public const string FolderName = "Regions";

        /// <summary>Index of the region ids under <see cref="FolderName"/>.</summary>
        public const string IndexFileName = "regions.json";

        /// <summary>The index document's <c>format</c> field.</summary>
        public const string IndexFormat = "ghumante-region-index";

        public const int MaxRegionIdLength = 64;

        /// <summary>The data files a region manifest lists (everything but the manifest itself).</summary>
        public static readonly RegionFileKind[] DataKinds =
        {
            RegionFileKind.Pack, RegionFileKind.SearchIndex, RegionFileKind.RouteGraph,
        };

        /// <summary>The file name suffix of a kind (appended to the region id).</summary>
        public static string Suffix(RegionFileKind kind)
        {
            switch (kind)
            {
                case RegionFileKind.Manifest: return ".manifest.json";
                case RegionFileKind.Pack: return ".ghpk";
                case RegionFileKind.SearchIndex: return ".search.ghsi";
                case RegionFileKind.RouteGraph: return ".route.ghrg";
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        /// <summary>File name of a region file, for example <c>kathmandu_core.search.ghsi</c>.</summary>
        public static string FileName(string regionId, RegionFileKind kind)
        {
            RequireValid(regionId);
            return regionId + Suffix(kind);
        }

        /// <summary>
        /// True for ids made of 1 to 64 lowercase ASCII letters, digits, '_' and '-', starting with a letter or digit.
        /// Ids become folder and file names, so anything that could escape a folder is rejected.
        /// </summary>
        public static bool IsValidRegionId(string regionId)
        {
            if (string.IsNullOrEmpty(regionId) || regionId.Length > MaxRegionIdLength) return false;
            for (int i = 0; i < regionId.Length; i++)
            {
                char c = regionId[i];
                bool alnum = c >= 'a' && c <= 'z' || c >= '0' && c <= '9';
                if (alnum) continue;
                if (i > 0 && (c == '_' || c == '-')) continue;
                return false;
            }
            return true;
        }

        /// <summary>Throws <see cref="ArgumentException"/> unless <see cref="IsValidRegionId"/>.</summary>
        public static void RequireValid(string regionId)
        {
            if (!IsValidRegionId(regionId))
                throw new ArgumentException("invalid region id '" + regionId + "' (lowercase letters, digits, '_' and '-')", nameof(regionId));
        }

        /// <summary>Joins a root (folder or URL) and a relative part with a single '/'.</summary>
        public static string Join(string root, string part)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (part == null) throw new ArgumentNullException(nameof(part));
            return root.TrimEnd('/', '\\') + "/" + part.TrimStart('/', '\\');
        }

        /// <summary><c>&lt;root&gt;/Regions</c>.</summary>
        public static string RegionsFolder(string root)
        {
            return Join(root, FolderName);
        }

        /// <summary><c>&lt;root&gt;/Regions/&lt;id&gt;</c>.</summary>
        public static string RegionFolder(string root, string regionId)
        {
            RequireValid(regionId);
            return Join(RegionsFolder(root), regionId);
        }

        /// <summary><c>&lt;root&gt;/Regions/&lt;id&gt;/&lt;id&gt;&lt;suffix&gt;</c>.</summary>
        public static string FilePath(string root, string regionId, RegionFileKind kind)
        {
            return Join(RegionFolder(root, regionId), FileName(regionId, kind));
        }

        /// <summary><c>&lt;root&gt;/Regions/regions.json</c>.</summary>
        public static string IndexPath(string root)
        {
            return Join(RegionsFolder(root), IndexFileName);
        }

        /// <summary>
        /// True when <paramref name="root"/> is a URL rather than a folder: Android's
        /// <c>Application.streamingAssetsPath</c> is <c>jar:file://&lt;apk&gt;!/assets</c>, which only UnityWebRequest can
        /// read and which cannot be seeked, so packs must be unpacked to persistent storage first.
        /// </summary>
        public static bool IsUrl(string root)
        {
            if (string.IsNullOrEmpty(root)) return false;
            return root.StartsWith("jar:", StringComparison.OrdinalIgnoreCase) || root.IndexOf("://", StringComparison.Ordinal) >= 0;
        }

        /// <summary>
        /// The region ids of an index document (<c>{"format": "ghumante-region-index", "regions": [...]}</c>, or a bare
        /// array): valid ids only, ordinal order, no duplicates. Throws <see cref="FormatException"/> on malformed JSON.
        /// </summary>
        public static List<string> ParseIndex(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            JsonValue root = Json.Parse(json);
            JsonArray list = root.AsArray();
            if (list == null)
            {
                JsonObject o = root.AsObject();
                if (o == null) throw new FormatException("region index must be an object or an array");
                JsonValue regions;
                if (!o.TryGetValue("regions", out regions) || regions.AsArray() == null)
                    throw new FormatException("region index has no 'regions' array");
                list = regions.AsArray();
            }
            var ids = new List<string>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                string id = list[i].AsString();
                if (IsValidRegionId(id) && !ids.Contains(id)) ids.Add(id);
            }
            ids.Sort(StringComparer.Ordinal);
            return ids;
        }

        /// <summary>The index document for <paramref name="regionIds"/> (valid ids, ordinal order, deduplicated).</summary>
        public static string FormatIndex(IEnumerable<string> regionIds)
        {
            if (regionIds == null) throw new ArgumentNullException(nameof(regionIds));
            var ids = new List<string>();
            foreach (string id in regionIds)
                if (IsValidRegionId(id) && !ids.Contains(id)) ids.Add(id);
            ids.Sort(StringComparer.Ordinal);
            var sb = new StringBuilder();
            sb.Append("{\n  \"format\": \"").Append(IndexFormat).Append("\",\n  \"version\": 1,\n  \"regions\": [");
            for (int i = 0; i < ids.Count; i++)
            {
                sb.Append(i == 0 ? "\n    \"" : ",\n    \"").Append(ids[i]).Append('"');
            }
            sb.Append(ids.Count > 0 ? "\n  ]\n}\n" : "]\n}\n");
            return sb.ToString();
        }
    }
}
