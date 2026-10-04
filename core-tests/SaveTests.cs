using System;
using System.Linq;
using Ghumante.Core.Geo;
using Ghumante.Core.Save;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class JsonTests
    {
        [Test]
        public void RoundTripsUnicodeEscapesAndNumbers()
        {
            var o = new JsonObject()
                .Set("ne", "काठमाडौं उपत्यका")
                .Set("emoji", "\U0001F3D4 mountain")
                .Set("escapes", "quote \" backslash \\ slash / nl \n cr \r tab \t bs \b ff \f nul \0 us \u001f ls \u2028")
                .Set("int", 9007199254740993L) // 2^53 + 1: kept exactly
                .Set("neg", long.MinValue)
                .Set("dbl", 0.1)
                .Set("whole", 2.0)
                .Set("tiny", 5e-324)
                .Set("big", 1.7976931348623157e308)
                .Set("t", true).Set("f", false)
                .Set("nil", (JsonValue)null)
                .Set("arr", new JsonArray().Add(1).Add("two").Add(new JsonObject()).Add(new JsonArray()))
                .Set("empty", "");
            foreach (int indent in new[] { 0, 1, 2 })
            {
                string text = Json.Write(o, indent);
                JsonValue back = Json.Parse(text);
                Assert.That(JsonValue.DeepEquals(o, back), Is.True, text);
                Assert.That(Json.Write(back, indent), Is.EqualTo(text));
                Assert.That(text.Contains("काठमाडौं"), Is.True, "non-ASCII written raw");
                Assert.That(text.Contains("\\u0000") && text.Contains("\\u2028"), Is.True);
            }
            JsonObject p = Json.Parse(Json.Write(o)).AsObject();
            Assert.That(p.GetLong("int"), Is.EqualTo(9007199254740993L));
            Assert.That(p["int"].IsInteger, Is.True);
            Assert.That(p["whole"].IsInteger, Is.False);
            Assert.That(p.GetDouble("dbl"), Is.EqualTo(0.1));
            Assert.That(p.GetDouble("tiny"), Is.EqualTo(5e-324));
            Assert.That(p.GetLong("whole"), Is.EqualTo(2));
            Assert.That(p["nil"].IsNull, Is.True);
            Assert.That(p.GetString("escapes"), Is.EqualTo(o.GetString("escapes")));
            Assert.That(p.Keys.ToArray(), Is.EqualTo(o.Keys.ToArray()), "insertion order kept");

            // System.Text.Json agrees on what we write
            using (var doc = System.Text.Json.JsonDocument.Parse(Json.Write(o)))
            {
                Assert.That(doc.RootElement.GetProperty("ne").GetString(), Is.EqualTo("काठमाडौं उपत्यका"));
                Assert.That(doc.RootElement.GetProperty("emoji").GetString(), Is.EqualTo("\U0001F3D4 mountain"));
                Assert.That(doc.RootElement.GetProperty("escapes").GetString(), Is.EqualTo(o.GetString("escapes")));
            }
        }

        [Test]
        public void ParsesStandardInput()
        {
            JsonValue v = Json.Parse("\uFEFF { \"a\" : [ 1 , -0.5e+2 , 3E2, -0, \"\\u0915\\u093e\\uD83D\\uDE00\" ] , \"b\":{}} \n");
            JsonArray a = v.AsObject().GetArray("a");
            Assert.That(a[0].AsLong(), Is.EqualTo(1));
            Assert.That(a[1].AsDouble(), Is.EqualTo(-50.0));
            Assert.That(a[2].AsDouble(), Is.EqualTo(300.0));
            Assert.That(a[3].AsLong(), Is.EqualTo(0));
            Assert.That(a[4].AsString(), Is.EqualTo("का\U0001F600"));
            Assert.That(Json.Parse("12345678901234567890").AsDouble(), Is.EqualTo(12345678901234567890.0)); // beyond long
        }

        [TestCase("")]
        [TestCase("{")]
        [TestCase("{\"a\":1,}")]
        [TestCase("[1,]")]
        [TestCase("[1 2]")]
        [TestCase("{\"a\" 1}")]
        [TestCase("{a:1}")]
        [TestCase("\"unterminated")]
        [TestCase("\"bad \\x escape\"")]
        [TestCase("\"\\u12\"")]
        [TestCase("\"tab\tinside\"")]
        [TestCase("01")]
        [TestCase("1.")]
        [TestCase(".5")]
        [TestCase("1e")]
        [TestCase("+1")]
        [TestCase("NaN")]
        [TestCase("tru")]
        [TestCase("nulll")]
        [TestCase("{} {}")]
        [TestCase("{\"a\":1,\"a\":2}")]
        [TestCase("1e400")]
        public void RejectsMalformedInput(string text)
        {
            Assert.Throws<JsonParseException>(() => Json.Parse(text));
            JsonValue v;
            Assert.That(Json.TryParse(text, out v), Is.False);
        }

        [Test]
        public void DepthLimit()
        {
            string deep = new string('[', 200) + new string(']', 200);
            Assert.Throws<JsonParseException>(() => Json.Parse(deep));
            string ok = new string('[', 100) + new string(']', 100);
            Assert.That(Json.Parse(ok).Kind, Is.EqualTo(JsonKind.Array));
        }

        [Test]
        public void LoneSurrogatesAreEscaped()
        {
            string s = "a\uD800b";
            string text = Json.Write(JsonValue.From(s));
            Assert.That(text, Is.EqualTo("\"a\\ud800b\""));
            Assert.That(Json.Parse(text).AsString(), Is.EqualTo(s));
            Assert.Throws<ArgumentException>(() => JsonValue.From(double.NaN));
        }

        [Test]
        public void ObjectEditing()
        {
            var o = new JsonObject().Set("a", 1).Set("b", 2).Set("c", 3);
            o.Set("a", 10);
            Assert.That(o.Keys.ToArray(), Is.EqualTo(new[] { "a", "b", "c" }));
            Assert.That(o.Remove("b"), Is.True);
            Assert.That(o.Remove("b"), Is.False);
            Assert.That(o.Keys.ToArray(), Is.EqualTo(new[] { "a", "c" }));
            Assert.That(o.GetInt("c"), Is.EqualTo(3));
            Assert.That(o.GetString("a", "x"), Is.EqualTo("x"), "type mismatch gives the fallback");
            Assert.That(o["missing"], Is.Null);
        }
    }

    public class SaveTests
    {
        private static SaveData Sample()
        {
            var s = new SaveData();
            s.Player.Lon = 85.362039;
            s.Player.Lat = 27.721493;
            s.Player.ElevationM = 1320.5;
            s.Player.HeadingDeg = 271.25;
            s.Player.VehicleId = "scooter_red";
            s.Player.RegionId = "kathmandu_valley";
            s.Discovery.FoundPois.Add((56688296UL << 2) | 1);
            s.Discovery.FoundPois.Add(201223707UL << 2);
            s.Discovery.PassportStamps.Add(new SaveData.PassportStamp { PlaceId = "boudhanath", StampedAtUtc = "2026-10-04T10:00:00Z" });
            s.Discovery.FogBitmaps["kathmandu_valley"] = "eJzLSM3JyQcABiwCFQ==";
            s.Collections.Items["momos"] = new System.Collections.Generic.List<string> { "jhol", "buff \"special\"", "तरकारी" };
            s.Progress.Coins = 1250;
            s.Progress.Stars = 12;
            s.Progress.ExplorerXp = 4_000_000_000L;
            s.Settings.Language = "ne";
            s.Settings.NepaliNumerals = true;
            s.Settings.MusicVolume = 0.35;
            s.Settings.OrientationLock = "portrait";
            s.Story.Set("chapter", 1);
            s.Quests.Set("q_momo", new JsonObject().Set("state", "active"));
            return s;
        }

        [Test]
        public void RoundTrip()
        {
            SaveData s = Sample();
            string json = SaveSerializer.ToJson(s);
            SaveData back = SaveSerializer.FromJson(json);
            Assert.That(back.SchemaVersion, Is.EqualTo(SaveData.CurrentSchemaVersion));
            Assert.That(back.Player.Lon, Is.EqualTo(s.Player.Lon));
            Assert.That(back.Player.Lat, Is.EqualTo(s.Player.Lat));
            Assert.That(back.Player.ElevationM, Is.EqualTo(1320.5));
            Assert.That(back.Player.HeadingDeg, Is.EqualTo(271.25));
            Assert.That(back.Player.VehicleId, Is.EqualTo("scooter_red"));
            Assert.That(back.Player.RegionId, Is.EqualTo("kathmandu_valley"));
            Assert.That(back.Discovery.FoundPois, Is.EqualTo(s.Discovery.FoundPois));
            Assert.That(back.Discovery.PassportStamps.Single().PlaceId, Is.EqualTo("boudhanath"));
            Assert.That(back.Discovery.PassportStamps.Single().StampedAtUtc, Is.EqualTo("2026-10-04T10:00:00Z"));
            Assert.That(back.Discovery.FogBitmaps["kathmandu_valley"], Is.EqualTo("eJzLSM3JyQcABiwCFQ=="));
            Assert.That(back.Collections.Items["momos"], Is.EqualTo(new[] { "jhol", "buff \"special\"", "तरकारी" }));
            Assert.That(back.Progress.ExplorerXp, Is.EqualTo(4_000_000_000L));
            Assert.That(back.Progress.Coins, Is.EqualTo(1250));
            Assert.That(back.Settings.Language, Is.EqualTo("ne"));
            Assert.That(back.Settings.NepaliNumerals, Is.True);
            Assert.That(back.Settings.MusicVolume, Is.EqualTo(0.35));
            Assert.That(back.Settings.OrientationLock, Is.EqualTo("portrait"));
            Assert.That(back.Story.GetInt("chapter"), Is.EqualTo(1));
            Assert.That(back.Quests.GetObject("q_momo").GetString("state"), Is.EqualTo("active"));
            Assert.That(SaveSerializer.ToJson(back), Is.EqualTo(json), "stable output");

            using (var doc = System.Text.Json.JsonDocument.Parse(json))
            {
                var root = doc.RootElement;
                Assert.That(root.GetProperty("schemaVersion").GetInt32(), Is.EqualTo(1));
                foreach (string section in new[] { "player", "discovery", "collections", "progress", "settings", "story", "quests" })
                    Assert.That(root.TryGetProperty(section, out _), Is.True, section);
            }
        }

        [Test]
        public void UnknownKeysSurvive()
        {
            string json = SaveSerializer.ToJson(Sample());
            JsonObject doc = Json.Parse(json).AsObject();
            doc.Set("futureSection", new JsonObject().Set("x", 1));
            doc.GetObject("player").Set("mount", "yak");
            doc.GetObject("settings").Set("hapticStrength", 0.5);
            SaveData s = SaveSerializer.FromJson(Json.Write(doc));
            JsonObject again = Json.Parse(SaveSerializer.ToJson(s)).AsObject();
            Assert.That(again.GetObject("futureSection").GetInt("x"), Is.EqualTo(1));
            Assert.That(again.GetObject("player").GetString("mount"), Is.EqualTo("yak"));
            Assert.That(again.GetObject("settings").GetDouble("hapticStrength"), Is.EqualTo(0.5));
        }

        [Test]
        public void MissingSectionsGetDefaults()
        {
            SaveData s = SaveSerializer.FromJson("{\"schemaVersion\":1}");
            Assert.That(s.Player.VehicleId, Is.EqualTo(""));
            Assert.That(s.Progress.Coins, Is.EqualTo(0));
            Assert.That(s.Settings.Language, Is.EqualTo("en"));
            Assert.That(s.Discovery.FoundPois, Is.Empty);
            Assert.Throws<FormatException>(() => SaveSerializer.FromJson("[1,2]"));
            Assert.Throws<JsonParseException>(() => SaveSerializer.FromJson("{\"schemaVersion\":1"));
        }

        [Test]
        public void MigratesPrototypeV0Saves()
        {
            double x, z;
            WorldFrame.LonLatToGame(85.3240, 27.7172, out x, out z);
            string v0 = "{\"player\":{\"x\":" + x.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ",\"z\":"
                        + z.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                        + ",\"vehicleId\":\"bike\"},\"coins\":42,\"stars\":3,\"settings\":{\"language\":\"ne\"}}";
            SaveData s = SaveSerializer.FromJson(v0);
            Assert.That(s.SchemaVersion, Is.EqualTo(1));
            Assert.That(s.Player.Lon, Is.EqualTo(85.3240).Within(1e-9));
            Assert.That(s.Player.Lat, Is.EqualTo(27.7172).Within(1e-9));
            Assert.That(s.Player.VehicleId, Is.EqualTo("bike"));
            Assert.That(s.Player.Extra.Count, Is.EqualTo(0), "x/z were consumed");
            Assert.That(s.Progress.Coins, Is.EqualTo(42));
            Assert.That(s.Progress.Stars, Is.EqualTo(3));
            Assert.That(s.Extra.ContainsKey("coins"), Is.False);
            Assert.That(s.Settings.Language, Is.EqualTo("ne"));
        }

        private sealed class AddField : ISaveMigration
        {
            private readonly int _from;

            public AddField(int from)
            {
                _from = from;
            }

            public int FromVersion
            {
                get { return _from; }
            }

            public JsonObject Apply(JsonObject document)
            {
                JsonArray log = document.GetArray("log") ?? new JsonArray();
                log.Add("v" + _from);
                document.Set("log", log);
                return document;
            }
        }

        [Test]
        public void MigratorRunsStepsInOrder()
        {
            var m = new SaveMigrator(new ISaveMigration[] { new AddField(2), new AddField(0), new AddField(1) }, 3);
            JsonObject doc = m.Migrate(new JsonObject().Set("player", new JsonObject()));
            Assert.That(SaveMigrator.VersionOf(doc), Is.EqualTo(3));
            Assert.That(doc.GetArray("log").Select(v => v.AsString()), Is.EqualTo(new[] { "v0", "v1", "v2" }));

            doc = m.Migrate(new JsonObject().Set("schemaVersion", 2));
            Assert.That(doc.GetArray("log").Select(v => v.AsString()), Is.EqualTo(new[] { "v2" }));

            Assert.Throws<SaveVersionException>(() => m.Migrate(new JsonObject().Set("schemaVersion", 4)));
            Assert.Throws<FormatException>(() => m.Migrate(new JsonObject().Set("schemaVersion", "one")));
            Assert.Throws<InvalidOperationException>(() =>
                new SaveMigrator(new ISaveMigration[] { new AddField(0) }, 3).Migrate(new JsonObject()));
            Assert.Throws<ArgumentException>(() => new SaveMigrator(new ISaveMigration[] { new AddField(0), new AddField(0) }));
            Assert.Throws<SaveVersionException>(() => SaveSerializer.FromJson("{\"schemaVersion\":99}"));
        }

        [Test]
        public void CloudMergeTakesUnionAndMaximum()
        {
            SaveData local = Sample();
            var cloud = new SaveData();
            cloud.Discovery.FoundPois.Add(999UL << 2);
            cloud.Discovery.PassportStamps.Add(new SaveData.PassportStamp { PlaceId = "boudhanath", StampedAtUtc = "other" });
            cloud.Discovery.PassportStamps.Add(new SaveData.PassportStamp { PlaceId = "pokhara", StampedAtUtc = "x" });
            cloud.Collections.Items["momos"] = new System.Collections.Generic.List<string> { "jhol", "sukuti" };
            cloud.Progress.Coins = 5000;
            cloud.Progress.Stars = 1;
            local.MergeFrom(cloud);
            Assert.That(local.Discovery.FoundPois.Count, Is.EqualTo(3));
            Assert.That(local.Discovery.PassportStamps.Select(p => p.PlaceId), Is.EqualTo(new[] { "boudhanath", "pokhara" }));
            Assert.That(local.Collections.Items["momos"], Has.Member("sukuti").And.Count.EqualTo(4));
            Assert.That(local.Progress.Coins, Is.EqualTo(5000));
            Assert.That(local.Progress.Stars, Is.EqualTo(12));
        }
    }
}
