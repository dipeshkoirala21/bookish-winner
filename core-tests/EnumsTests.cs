using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Ghumante.Core.Data;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class EnumsTests
    {
        private static readonly Type[] FlagEnums = { typeof(Travel), typeof(RoadFlags), typeof(BuildingFlags), typeof(PoiFlags) };

        private static string Pascal(string name)
        {
            return string.Concat(name.Split('_').Select(w => w.Substring(0, 1).ToUpperInvariant() + w.Substring(1).ToLowerInvariant()));
        }

        private static IEnumerable<string> EnumNames()
        {
            JsonElement spec = GoldenFiles.SharedJson("enums.json").GetProperty("enums");
            return spec.EnumerateObject().Select(p => p.Name).ToList();
        }

        [TestCaseSource(nameof(EnumNames))]
        public void EnumMatchesSharedJson(string enumName)
        {
            JsonElement members = GoldenFiles.SharedJson("enums.json").GetProperty("enums").GetProperty(enumName);
            Type t = typeof(TileId).Assembly.GetType("Ghumante.Core.Data." + enumName);
            Assert.That(t, Is.Not.Null, enumName + " missing from Enums.cs");
            bool isFlags = FlagEnums.Contains(t);
            Assert.That(t.IsDefined(typeof(FlagsAttribute), false), Is.EqualTo(isFlags), enumName + " [Flags]");

            var expected = members.EnumerateObject().ToDictionary(p => Pascal(p.Name), p => p.Value.GetInt64());
            if (isFlags) expected["None"] = 0;
            var actual = Enum.GetNames(t).ToDictionary(n => n, n => Convert.ToInt64(Enum.Parse(t, n)));
            Assert.That(actual, Is.EquivalentTo(expected));
        }

        [Test]
        public void EveryEnumIsCovered()
        {
            var names = new HashSet<string>(EnumNames());
            var ours = typeof(TileId).Assembly.GetTypes()
                .Where(t => t.IsEnum && t.Namespace == "Ghumante.Core.Data" && t.Name != "LineFlags" && t.Name != "AreaFlags")
                .Select(t => t.Name);
            Assert.That(ours, Is.EquivalentTo(names));
        }

        [Test]
        public void SurfaceGroupTableMatches()
        {
            JsonElement sg = GoldenFiles.SharedJson("enums.json").GetProperty("surface_group");
            foreach (JsonProperty p in sg.EnumerateObject())
            {
                var s = (Surface)Enum.Parse(typeof(Surface), Pascal(p.Name));
                var g = (SurfaceGroup)Enum.Parse(typeof(SurfaceGroup), Pascal(p.Value.GetString()));
                Assert.That(SurfaceGroups.Of(s), Is.EqualTo(g), p.Name);
            }
            Assert.That(SurfaceGroups.Of((Surface)200), Is.EqualTo(SurfaceGroup.Dirt));
        }
    }
}
