using System;
using System.Collections.Generic;
using Ghumante.Core.Save;
using Ghumante.Core.Services;

namespace Ghumante.Platform.Regions
{
    /// <summary>One entry of a manifest's <c>files</c> list.</summary>
    public sealed class RegionFileEntry
    {
        /// <summary>File name relative to the region folder (never a path: no separators, no "..").</summary>
        public string Path;

        /// <summary>Exact size in bytes.</summary>
        public long Bytes;

        /// <summary>Lowercase hexadecimal SHA-256 of the file.</summary>
        public string Sha256;

        public override string ToString()
        {
            return Path + " (" + Bytes + " bytes)";
        }
    }

    /// <summary>
    /// A region manifest (<c>&lt;id&gt;.manifest.json</c>, DATA_FORMATS.md section 2): what the runtime needs from it.
    /// <see cref="Parse"/> validates the format, the region id and every file entry (sizes, SHA-256 strings, plain file
    /// names), because manifests come from downloads as well as from the app. Engine-free.
    /// </summary>
    public sealed class RegionManifest
    {
        public const string Format = "ghumante-region-manifest";
        public const int SupportedVersion = 1;

        public string RegionId;

        /// <summary>English display name (the region id when the manifest has none).</summary>
        public string NameEn;

        /// <summary>Nepali display name, or null.</summary>
        public string NameNe;

        public int DataVersion;
        public string PipelineVersion;
        public string BuiltAt;
        public string ScaleModel;

        /// <summary>Leaf (finest) tile level, 0 when the manifest does not say.</summary>
        public int LeafLevel;

        public int[] DetailLevels = new int[0];
        public int[] HorizonLevels = new int[0];

        /// <summary>True when <c>bbox_game</c> was present: the detail coverage in game metres.</summary>
        public bool HasBounds;

        public double MinX, MinZ, MaxX, MaxZ;

        public readonly List<RegionFileEntry> Files = new List<RegionFileEntry>();
        public readonly List<string> Attribution = new List<string>();

        /// <summary>Centre of <c>bbox_game</c> (game X), or 0.</summary>
        public double CentreX
        {
            get { return HasBounds ? (MinX + MaxX) * 0.5 : 0.0; }
        }

        /// <summary>Centre of <c>bbox_game</c> (game Z), or 0.</summary>
        public double CentreZ
        {
            get { return HasBounds ? (MinZ + MaxZ) * 0.5 : 0.0; }
        }

        /// <summary>Sum of the listed files' sizes.</summary>
        public long TotalBytes
        {
            get
            {
                long b = 0;
                for (int i = 0; i < Files.Count; i++) b += Files[i].Bytes;
                return b;
            }
        }

        /// <summary>The entry for a data file of this region (by its conventional name), or null when not listed.</summary>
        public RegionFileEntry FileFor(RegionFileKind kind)
        {
            string name = RegionFiles.FileName(RegionId, kind);
            for (int i = 0; i < Files.Count; i++)
                if (string.Equals(Files[i].Path, name, StringComparison.Ordinal)) return Files[i];
            return null;
        }

        /// <summary>Parses and validates a manifest. Throws <see cref="FormatException"/> with the reason.</summary>
        public static RegionManifest Parse(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            JsonObject o = Json.Parse(json).AsObject();
            if (o == null) throw new FormatException("region manifest must be a JSON object");
            if (Str(o, "format") != Format) throw new FormatException("not a region manifest (format '" + Str(o, "format") + "')");
            JsonValue version;
            if (o.TryGetValue("version", out version) && version.AsInt(-1) != SupportedVersion)
                throw new FormatException("unsupported region manifest version " + version);

            var m = new RegionManifest { RegionId = Str(o, "region") };
            if (!RegionFiles.IsValidRegionId(m.RegionId)) throw new FormatException("invalid region id '" + m.RegionId + "'");
            JsonValue name;
            if (o.TryGetValue("name", out name) && name.AsObject() != null)
            {
                m.NameEn = Str(name.AsObject(), "en");
                m.NameNe = Str(name.AsObject(), "ne");
            }
            if (string.IsNullOrEmpty(m.NameEn)) m.NameEn = m.RegionId;
            m.DataVersion = Int(o, "data_version");
            m.PipelineVersion = Str(o, "pipeline_version");
            m.BuiltAt = Str(o, "built_at");
            m.ScaleModel = Str(o, "scale_model") ?? "identity";
            m.LeafLevel = Int(o, "leaf_level");
            m.DetailLevels = Ints(o, "detail_levels");
            m.HorizonLevels = Ints(o, "horizon_levels");

            JsonValue bbox;
            if (o.TryGetValue("bbox_game", out bbox) && bbox.AsArray() != null && bbox.AsArray().Count == 4)
            {
                JsonArray b = bbox.AsArray();
                m.MinX = b[0].AsDouble();
                m.MinZ = b[1].AsDouble();
                m.MaxX = b[2].AsDouble();
                m.MaxZ = b[3].AsDouble();
                m.HasBounds = m.MaxX > m.MinX && m.MaxZ > m.MinZ;
            }

            JsonValue files;
            if (!o.TryGetValue("files", out files) || files.AsArray() == null) throw new FormatException("manifest has no 'files' list");
            foreach (JsonValue f in files.AsArray())
            {
                JsonObject fo = f.AsObject();
                if (fo == null) throw new FormatException("manifest file entry is not an object");
                var e = new RegionFileEntry { Path = Str(fo, "path"), Sha256 = Str(fo, "sha256") };
                JsonValue bytes;
                e.Bytes = fo.TryGetValue("bytes", out bytes) ? bytes.AsLong(-1) : -1;
                if (!IsPlainFileName(e.Path)) throw new FormatException("manifest file path '" + e.Path + "' is not a plain file name");
                if (e.Bytes < 0) throw new FormatException("manifest file '" + e.Path + "' has no valid size");
                if (!IsSha256Hex(e.Sha256)) throw new FormatException("manifest file '" + e.Path + "' has no valid sha256");
                for (int i = 0; i < m.Files.Count; i++)
                    if (m.Files[i].Path == e.Path) throw new FormatException("manifest lists '" + e.Path + "' twice");
                m.Files.Add(e);
            }
            if (m.FileFor(RegionFileKind.Pack) == null)
                throw new FormatException("manifest does not list " + RegionFiles.FileName(m.RegionId, RegionFileKind.Pack));

            JsonValue attribution;
            if (o.TryGetValue("attribution", out attribution) && attribution.AsArray() != null)
            {
                foreach (JsonValue a in attribution.AsArray())
                {
                    string s = a.AsString();
                    if (!string.IsNullOrEmpty(s)) m.Attribution.Add(s);
                }
            }
            return m;
        }

        /// <summary>True for a non-empty name without directory separators, drive colons or a ".." component.</summary>
        public static bool IsPlainFileName(string name)
        {
            if (string.IsNullOrEmpty(name) || name == "." || name == "..") return false;
            return name.IndexOfAny(new[] { '/', '\\', ':', '\0' }) < 0 && !name.StartsWith("..", StringComparison.Ordinal);
        }

        /// <summary>True for 64 lowercase hexadecimal digits.</summary>
        public static bool IsSha256Hex(string s)
        {
            if (s == null || s.Length != 64) return false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')) return false;
            }
            return true;
        }

        private static string Str(JsonObject o, string key)
        {
            JsonValue v;
            return o.TryGetValue(key, out v) ? v.AsString() : null;
        }

        private static int Int(JsonObject o, string key)
        {
            JsonValue v;
            return o.TryGetValue(key, out v) ? v.AsInt() : 0;
        }

        private static int[] Ints(JsonObject o, string key)
        {
            JsonValue v;
            if (!o.TryGetValue(key, out v) || v.AsArray() == null) return new int[0];
            JsonArray a = v.AsArray();
            var r = new int[a.Count];
            for (int i = 0; i < r.Length; i++) r[i] = a[i].AsInt();
            return r;
        }
    }
}
