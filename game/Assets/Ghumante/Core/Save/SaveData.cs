using System;
using System.Collections.Generic;

namespace Ghumante.Core.Save
{
    /// <summary>
    /// The save document (ARCHITECTURE.md 7.10), schema version <see cref="CurrentSchemaVersion"/>. Positions are
    /// stored as WGS84 lon/lat (canonical, ADR-002), never as game or scene coordinates. Unknown keys in every
    /// section are kept in <c>Extra</c> and written back, so an older build does not drop data written by a
    /// newer one. Story and quests are reserved for Story Mode and kept as raw JSON.
    /// </summary>
    public sealed class SaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;
        public PlayerSection Player = new PlayerSection();
        public DiscoverySection Discovery = new DiscoverySection();
        public CollectionsSection Collections = new CollectionsSection();
        public ProgressSection Progress = new ProgressSection();
        public SettingsSection Settings = new SettingsSection();
        public JsonObject Story = new JsonObject();
        public JsonObject Quests = new JsonObject();
        public JsonObject Extra = new JsonObject();

        public sealed class PlayerSection
        {
            public double Lon = 85.3240; // Kathmandu Durbar Square
            public double Lat = 27.7172;
            public double ElevationM;
            public double HeadingDeg;
            public string VehicleId = "";
            public string RegionId = "";
            public JsonObject Extra = new JsonObject();
        }

        public struct PassportStamp
        {
            public string PlaceId;

            /// <summary>ISO-8601 UTC timestamp, set by the caller (Core never reads the clock).</summary>
            public string StampedAtUtc;
        }

        public sealed class DiscoverySection
        {
            /// <summary>Found POIs by osm_ref ((osm_id &lt;&lt; 2) | type, as in the tile POIS chunk), sorted.</summary>
            public SortedSet<ulong> FoundPois = new SortedSet<ulong>();

            public List<PassportStamp> PassportStamps = new List<PassportStamp>();

            /// <summary>Fog-of-discovery bitmaps per region id (compressed, base64).</summary>
            public SortedDictionary<string, string> FogBitmaps = new SortedDictionary<string, string>(StringComparer.Ordinal);

            public JsonObject Extra = new JsonObject();
        }

        public sealed class CollectionsSection
        {
            /// <summary>Collection id to collected item ids.</summary>
            public SortedDictionary<string, List<string>> Items = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        }

        public sealed class ProgressSection
        {
            public long Coins;
            public long Stars;
            public long ExplorerXp;
            public JsonObject Extra = new JsonObject();
        }

        public sealed class SettingsSection
        {
            public string Language = "en";
            public bool NepaliNumerals;
            public double MusicVolume = 0.8;
            public double SfxVolume = 1.0;

            /// <summary>"auto", "portrait" or "landscape" (ADR-017).</summary>
            public string OrientationLock = "auto";

            public bool AnalyticsConsent;

            /// <summary>Vibration/haptic feedback on supported phones (on by default; the OS settings still apply).</summary>
            public bool Haptics = true;

            /// <summary>Accessibility: replaces bounces, parallax and idle animation with simple fades.</summary>
            public bool ReduceMotion;

            public JsonObject Extra = new JsonObject();
        }

        // ------------------------------------------------------------------------------------- JSON mapping

        public JsonObject ToJson()
        {
            var root = new JsonObject();
            root.Set("schemaVersion", SchemaVersion);

            var p = new JsonObject()
                .Set("lon", Player.Lon).Set("lat", Player.Lat).Set("elevationM", Player.ElevationM)
                .Set("headingDeg", Player.HeadingDeg).Set("vehicleId", Player.VehicleId ?? "")
                .Set("regionId", Player.RegionId ?? "");
            CopyExtra(Player.Extra, p);
            root.Set("player", p);

            var found = new JsonArray();
            foreach (ulong r in Discovery.FoundPois) found.Add(checked((long)r));
            var stamps = new JsonArray();
            foreach (PassportStamp s in Discovery.PassportStamps)
                stamps.Add(new JsonObject().Set("placeId", s.PlaceId ?? "").Set("stampedAtUtc", s.StampedAtUtc ?? ""));
            var fog = new JsonObject();
            foreach (var kv in Discovery.FogBitmaps) fog.Set(kv.Key, kv.Value ?? "");
            var d = new JsonObject().Set("foundPois", found).Set("passportStamps", stamps).Set("fogBitmaps", fog);
            CopyExtra(Discovery.Extra, d);
            root.Set("discovery", d);

            var c = new JsonObject();
            foreach (var kv in Collections.Items)
            {
                var a = new JsonArray();
                foreach (string item in kv.Value) a.Add(item);
                c.Set(kv.Key, a);
            }
            root.Set("collections", c);

            var pr = new JsonObject().Set("coins", Progress.Coins).Set("stars", Progress.Stars)
                .Set("explorerXp", Progress.ExplorerXp);
            CopyExtra(Progress.Extra, pr);
            root.Set("progress", pr);

            var st = new JsonObject().Set("language", Settings.Language ?? "en")
                .Set("nepaliNumerals", Settings.NepaliNumerals).Set("musicVolume", Settings.MusicVolume)
                .Set("sfxVolume", Settings.SfxVolume).Set("orientationLock", Settings.OrientationLock ?? "auto")
                .Set("analyticsConsent", Settings.AnalyticsConsent).Set("haptics", Settings.Haptics)
                .Set("reduceMotion", Settings.ReduceMotion);
            CopyExtra(Settings.Extra, st);
            root.Set("settings", st);

            root.Set("story", Story ?? new JsonObject());
            root.Set("quests", Quests ?? new JsonObject());
            CopyExtra(Extra, root);
            return root;
        }

        private static readonly string[] RootKeys =
            { "schemaVersion", "player", "discovery", "collections", "progress", "settings", "story", "quests" };

        /// <summary>Map an (already migrated) document. Missing or mistyped fields get their defaults.</summary>
        public static SaveData FromJson(JsonObject root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            var s = new SaveData { SchemaVersion = root.GetInt("schemaVersion", CurrentSchemaVersion) };

            JsonObject p = root.GetObject("player") ?? new JsonObject();
            s.Player.Lon = p.GetDouble("lon", s.Player.Lon);
            s.Player.Lat = p.GetDouble("lat", s.Player.Lat);
            s.Player.ElevationM = p.GetDouble("elevationM");
            s.Player.HeadingDeg = p.GetDouble("headingDeg");
            s.Player.VehicleId = p.GetString("vehicleId", "");
            s.Player.RegionId = p.GetString("regionId", "");
            KeepExtra(p, s.Player.Extra, "lon", "lat", "elevationM", "headingDeg", "vehicleId", "regionId");

            JsonObject d = root.GetObject("discovery") ?? new JsonObject();
            JsonArray found = d.GetArray("foundPois");
            if (found != null)
                foreach (JsonValue v in found)
                    if (v.IsInteger && v.AsLong() >= 0) s.Discovery.FoundPois.Add((ulong)v.AsLong());
            JsonArray stamps = d.GetArray("passportStamps");
            if (stamps != null)
                foreach (JsonValue v in stamps)
                {
                    JsonObject o = v.AsObject();
                    if (o == null) continue;
                    s.Discovery.PassportStamps.Add(new PassportStamp
                        { PlaceId = o.GetString("placeId", ""), StampedAtUtc = o.GetString("stampedAtUtc", "") });
                }
            JsonObject fog = d.GetObject("fogBitmaps");
            if (fog != null)
                foreach (var kv in fog)
                    if (kv.Value.Kind == JsonKind.String) s.Discovery.FogBitmaps[kv.Key] = kv.Value.AsString();
            KeepExtra(d, s.Discovery.Extra, "foundPois", "passportStamps", "fogBitmaps");

            JsonObject c = root.GetObject("collections");
            if (c != null)
                foreach (var kv in c)
                {
                    JsonArray a = kv.Value.AsArray();
                    if (a == null) continue;
                    var items = new List<string>();
                    foreach (JsonValue v in a)
                        if (v.Kind == JsonKind.String) items.Add(v.AsString());
                    s.Collections.Items[kv.Key] = items;
                }

            JsonObject pr = root.GetObject("progress") ?? new JsonObject();
            s.Progress.Coins = pr.GetLong("coins");
            s.Progress.Stars = pr.GetLong("stars");
            s.Progress.ExplorerXp = pr.GetLong("explorerXp");
            KeepExtra(pr, s.Progress.Extra, "coins", "stars", "explorerXp");

            JsonObject st = root.GetObject("settings") ?? new JsonObject();
            s.Settings.Language = st.GetString("language", "en");
            s.Settings.NepaliNumerals = st.GetBool("nepaliNumerals");
            s.Settings.MusicVolume = st.GetDouble("musicVolume", s.Settings.MusicVolume);
            s.Settings.SfxVolume = st.GetDouble("sfxVolume", s.Settings.SfxVolume);
            s.Settings.OrientationLock = st.GetString("orientationLock", "auto");
            s.Settings.AnalyticsConsent = st.GetBool("analyticsConsent");
            s.Settings.Haptics = st.GetBool("haptics", true);
            s.Settings.ReduceMotion = st.GetBool("reduceMotion");
            KeepExtra(st, s.Settings.Extra, "language", "nepaliNumerals", "musicVolume", "sfxVolume", "orientationLock",
                      "analyticsConsent", "haptics", "reduceMotion");

            s.Story = root.GetObject("story") ?? new JsonObject();
            s.Quests = root.GetObject("quests") ?? new JsonObject();
            KeepExtra(root, s.Extra, RootKeys);
            return s;
        }

        private static void KeepExtra(JsonObject src, JsonObject extra, params string[] known)
        {
            foreach (var kv in src)
                if (Array.IndexOf(known, kv.Key) < 0) extra.Set(kv.Key, kv.Value);
        }

        private static void CopyExtra(JsonObject extra, JsonObject dst)
        {
            if (extra == null) return;
            foreach (var kv in extra)
                if (!dst.ContainsKey(kv.Key)) dst.Set(kv.Key, kv.Value);
        }

        /// <summary>Merge a cloud copy into this save (7.10): union of discoveries and collections, maximum of
        /// progress. Player position and settings stay local.</summary>
        public void MergeFrom(SaveData other)
        {
            if (other == null) return;
            Discovery.FoundPois.UnionWith(other.Discovery.FoundPois);
            var stamped = new HashSet<string>(StringComparer.Ordinal);
            foreach (PassportStamp s in Discovery.PassportStamps) stamped.Add(s.PlaceId ?? "");
            foreach (PassportStamp s in other.Discovery.PassportStamps)
                if (stamped.Add(s.PlaceId ?? "")) Discovery.PassportStamps.Add(s);
            foreach (var kv in other.Discovery.FogBitmaps)
                if (!Discovery.FogBitmaps.ContainsKey(kv.Key)) Discovery.FogBitmaps[kv.Key] = kv.Value;
            foreach (var kv in other.Collections.Items)
            {
                List<string> mine;
                if (!Collections.Items.TryGetValue(kv.Key, out mine)) Collections.Items[kv.Key] = mine = new List<string>();
                foreach (string item in kv.Value)
                    if (!mine.Contains(item)) mine.Add(item);
            }
            Progress.Coins = Math.Max(Progress.Coins, other.Progress.Coins);
            Progress.Stars = Math.Max(Progress.Stars, other.Progress.Stars);
            Progress.ExplorerXp = Math.Max(Progress.ExplorerXp, other.Progress.ExplorerXp);
        }
    }
}
