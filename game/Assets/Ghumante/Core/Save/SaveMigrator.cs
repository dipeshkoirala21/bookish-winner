using System;
using System.Collections.Generic;
using Ghumante.Core.Geo;

namespace Ghumante.Core.Save
{
    /// <summary>One schema step: upgrades a document from <see cref="FromVersion"/> to FromVersion + 1.</summary>
    public interface ISaveMigration
    {
        int FromVersion { get; }

        /// <summary>Upgrade the document in place or return a new one; <c>schemaVersion</c> is set by the
        /// migrator afterwards.</summary>
        JsonObject Apply(JsonObject document);
    }

    /// <summary>The save was written by a newer build (schema version above what this build knows).</summary>
    public sealed class SaveVersionException : Exception
    {
        public readonly int Version;

        public SaveVersionException(string message, int version) : base(message)
        {
            Version = version;
        }
    }

    /// <summary>
    /// Runs ordered <see cref="ISaveMigration"/> steps on the raw JSON document before it is mapped to
    /// <see cref="SaveData"/> (ARCHITECTURE.md 7.10). A document without <c>schemaVersion</c> is version 0.
    /// </summary>
    public sealed class SaveMigrator
    {
        private readonly Dictionary<int, ISaveMigration> _steps = new Dictionary<int, ISaveMigration>();

        public readonly int TargetVersion;

        public SaveMigrator(IEnumerable<ISaveMigration> migrations, int targetVersion = SaveData.CurrentSchemaVersion)
        {
            TargetVersion = targetVersion;
            if (migrations == null) return;
            foreach (ISaveMigration m in migrations)
            {
                if (_steps.ContainsKey(m.FromVersion))
                    throw new ArgumentException("two migrations from version " + m.FromVersion);
                _steps[m.FromVersion] = m;
            }
        }

        /// <summary>The migrations shipped with this build.</summary>
        public static SaveMigrator Default()
        {
            return new SaveMigrator(new ISaveMigration[] { new V0ToV1() });
        }

        public static int VersionOf(JsonObject document)
        {
            JsonValue v = document["schemaVersion"];
            return v == null ? 0 : v.AsInt(-1);
        }

        /// <summary>Upgrade <paramref name="document"/> to <see cref="TargetVersion"/>.</summary>
        public JsonObject Migrate(JsonObject document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            int v = VersionOf(document);
            if (v < 0) throw new FormatException("schemaVersion is not an integer");
            if (v > TargetVersion)
                throw new SaveVersionException("save schema " + v + " is newer than this build (" + TargetVersion + ")", v);
            while (v < TargetVersion)
            {
                ISaveMigration step;
                if (!_steps.TryGetValue(v, out step)) throw new InvalidOperationException("no save migration from version " + v);
                document = step.Apply(document) ?? throw new InvalidOperationException("migration from " + v + " returned null");
                v++;
                document.Set("schemaVersion", v);
            }
            return document;
        }

        /// <summary>
        /// M0 prototype saves (no schemaVersion): the player position was stored in game metres
        /// (<c>player.x</c>/<c>player.z</c>) and coins/stars at the top level. Converts the position to
        /// canonical lon/lat and moves the counters into <c>progress</c>.
        /// </summary>
        public sealed class V0ToV1 : ISaveMigration
        {
            public int FromVersion
            {
                get { return 0; }
            }

            public JsonObject Apply(JsonObject doc)
            {
                JsonObject player = doc.GetObject("player");
                if (player != null && player.ContainsKey("x") && player.ContainsKey("z"))
                {
                    double lon, lat;
                    WorldFrame.GameToLonLat(player.GetDouble("x"), player.GetDouble("z"), out lon, out lat);
                    player.Remove("x");
                    player.Remove("z");
                    player.Set("lon", lon).Set("lat", lat);
                }
                JsonObject progress = doc.GetObject("progress");
                if (progress == null)
                {
                    progress = new JsonObject();
                    doc.Set("progress", progress);
                }
                foreach (string k in new[] { "coins", "stars" })
                {
                    JsonValue v = doc[k];
                    if (v == null) continue;
                    if (!progress.ContainsKey(k)) progress.Set(k, v);
                    doc.Remove(k);
                }
                return doc;
            }
        }
    }

    /// <summary>Text form of a save: migrate, map, and write (pretty-printed UTF-8 JSON).</summary>
    public static class SaveSerializer
    {
        public static string ToJson(SaveData save, bool pretty = true)
        {
            return Json.Write(save.ToJson(), pretty ? 1 : 0) + (pretty ? "\n" : "");
        }

        public static SaveData FromJson(string json, SaveMigrator migrator = null)
        {
            JsonObject doc = Json.Parse(json) as JsonObject;
            if (doc == null) throw new FormatException("a save document must be a JSON object");
            doc = (migrator ?? SaveMigrator.Default()).Migrate(doc);
            return SaveData.FromJson(doc);
        }
    }
}
