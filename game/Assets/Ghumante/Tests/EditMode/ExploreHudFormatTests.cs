using System.Collections.Generic;
using System.Text.RegularExpressions;
using Ghumante.Core.Data;
using Ghumante.UI.Hud;
using NUnit.Framework;

namespace Ghumante.Tests.EditMode
{
    /// <summary>
    /// What the Explore HUD prints (M1 track D): speed in whole km/h, Devanagari digits in Nepali, distances and ETAs,
    /// surface and kind keys, and that every key the HUD can show exists in both string tables. Engine-free.
    /// </summary>
    public class ExploreHudFormatTests
    {
        [Test]
        public void SpeedReadsWholeKilometresPerHour()
        {
            Assert.AreEqual(0, HudFormat.SpeedKmh(0f));
            Assert.AreEqual(4, HudFormat.SpeedKmh(1f), "3.6 km/h rounds to 4");
            Assert.AreEqual(18, HudFormat.SpeedKmh(-5f), "reversing reads positive");
            Assert.AreEqual(85, HudFormat.SpeedKmh(85f / 3.6f));
            Assert.AreEqual(0, HudFormat.SpeedKmh(float.NaN));
            Assert.AreEqual(999, HudFormat.SpeedKmh(1e6f));
        }

        [Test]
        public void NumbersUseDevanagariDigitsInNepaliAndAreCached()
        {
            Assert.AreEqual("85", HudFormat.Number(85, false));
            Assert.AreEqual("८५", HudFormat.Number(85, true));
            Assert.AreEqual("०", HudFormat.Number(0, true));
            Assert.AreEqual("१२३४", HudFormat.Number(1234, true), "beyond the cache too");
            Assert.AreEqual("-७", HudFormat.Number(-7, true));
            Assert.AreSame(HudFormat.Number(62, true), HudFormat.Number(62, true), "the speedometer allocates nothing per frame");
            Assert.AreEqual("०६:३०", HudFormat.ToDevanagariDigits("06:30"));
        }

        [Test]
        public void DistancesRoundTheWayTheHudShowsThem()
        {
            bool km;
            Assert.AreEqual("35", HudFormat.Distance(37, false, out km));
            Assert.IsFalse(km);
            Assert.AreEqual("350", HudFormat.Distance(352, false, out km));
            Assert.AreEqual("990", HudFormat.Distance(994, false, out km));
            Assert.IsFalse(km);
            Assert.AreEqual("1.0", HudFormat.Distance(995, false, out km));
            Assert.IsTrue(km);
            Assert.AreEqual("4.9", HudFormat.Distance(4949, false, out km));
            Assert.AreEqual("४.९", HudFormat.Distance(4949, true, out km));
            Assert.AreEqual("12", HudFormat.Distance(12345, false, out km));
            Assert.IsTrue(km);
            Assert.AreEqual("0", HudFormat.Distance(double.NaN, false, out km));
        }

        [Test]
        public void TheDistanceBucketChangesExactlyWhenTheTextDoes()
        {
            string previousText = null;
            long previousBucket = -1;
            for (double m = 0; m < 30000; m += 0.75)
            {
                bool km;
                string text = HudFormat.Distance(m, false, out km) + (km ? " km" : " m");
                long bucket = HudFormat.DistanceBucket(m);
                if (previousText != null)
                    Assert.AreEqual(text != previousText, bucket != previousBucket, "at " + m + " m: " + previousText + " -> " + text);
                previousText = text;
                previousBucket = bucket;
            }
        }

        [Test]
        public void EtaRoundsUpToWholeMinutes()
        {
            Assert.AreEqual(0, HudFormat.EtaMinutes(0));
            Assert.AreEqual(1, HudFormat.EtaMinutes(1));
            Assert.AreEqual(2, HudFormat.EtaMinutes(61));
            Assert.AreEqual(9, HudFormat.EtaMinutes(481), "Thamel to Boudhanath on the sample: 481 s");
            Assert.AreEqual(0, HudFormat.EtaMinutes(double.NaN));
        }

        [Test]
        public void ClockShowsHoursAndMinutes()
        {
            Assert.AreEqual("06:30", HudFormat.Clock(6.5f, false));
            Assert.AreEqual("23:59", HudFormat.Clock(23.99f, false));
            Assert.AreEqual("23:00", HudFormat.Clock(-1f, false));
            Assert.AreEqual("००:००", HudFormat.Clock(24f, true));
        }

        [Test]
        public void SurfaceChipNamesTheRoadOrTheGround()
        {
            Assert.AreEqual("hud.surface.asphalt", HudFormat.SurfaceKey(true, Surface.Asphalt, SurfaceGroup.Paved));
            Assert.AreEqual("hud.surface.brick", HudFormat.SurfaceKey(true, Surface.Brick, SurfaceGroup.Paved));
            Assert.AreEqual("hud.surface.dirt", HudFormat.SurfaceKey(true, Surface.Unknown, SurfaceGroup.Dirt),
                            "an untagged road falls back to its group");
            Assert.AreEqual("hud.surface.mud", HudFormat.SurfaceKey(false, Surface.Asphalt, SurfaceGroup.Mud),
                            "off the road the ground's group counts, not the last road");
            Assert.AreEqual("gh-hud__surface--gravel", HudFormat.SurfaceClass(SurfaceGroup.Gravel));
            Assert.AreEqual("gh-hud__surface--paved", HudFormat.SurfaceClass(SurfaceGroup.Paved));
            Assert.IsFalse(HudFormat.IsInferred(SurfaceSource.Tagged));
            Assert.IsFalse(HudFormat.IsInferred(SurfaceSource.Derived));
            Assert.IsTrue(HudFormat.IsInferred(SurfaceSource.Inferred));
            Assert.IsTrue(HudFormat.IsInferred(SurfaceSource.Default));
            foreach (Surface s in (Surface[])System.Enum.GetValues(typeof(Surface)))
            {
                CollectionAssert.Contains(HudFormat.AllSurfaceKeys, HudFormat.SurfaceKey(true, s, SurfaceGroup.Paved), s.ToString());
            }
        }

        [Test]
        public void KindsHaveFriendlyNames()
        {
            Assert.AreEqual("kind.stupa", HudFormat.KindKey((int)PoiKind.Stupa));
            Assert.AreEqual("kind.temple", HudFormat.KindKey((int)PoiKind.TempleHindu));
            Assert.AreEqual("kind.heritage", HudFormat.KindKey((int)PoiKind.HeritageSquare));
            Assert.AreEqual("kind.neighbourhood", HudFormat.KindKey(SearchEntry.PlaceKindOffset + (int)PlaceKind.Neighbourhood));
            Assert.AreEqual("kind.city", HudFormat.KindKey(SearchEntry.PlaceKindOffset + (int)PlaceKind.City));
            Assert.AreEqual("kind.adventure", HudFormat.KindKey((int)PoiKind.Rafting));
            Assert.AreEqual("kind.place", HudFormat.KindKey(999));
            foreach (PoiKind k in (PoiKind[])System.Enum.GetValues(typeof(PoiKind)))
                CollectionAssert.Contains(HudFormat.AllKindKeys, HudFormat.KindKey((int)k), k.ToString());
            foreach (PlaceKind k in (PlaceKind[])System.Enum.GetValues(typeof(PlaceKind)))
                CollectionAssert.Contains(HudFormat.AllKindKeys, HudFormat.KindKey(SearchEntry.PlaceKindOffset + (int)k), k.ToString());
        }

        [Test]
        public void EveryExploreStringIsTranslated()
        {
            Dictionary<string, string> en = ExploreSample.Strings("en");
            Dictionary<string, string> ne = ExploreSample.Strings("ne");
            var keys = new List<string>(HudFormat.AllKindKeys);
            keys.AddRange(HudFormat.AllSurfaceKeys);
            keys.AddRange(new[]
            {
                "explore.loading.title", "explore.loading.finding", "explore.loading.opening", "explore.loading.opening_map",
                "explore.loading.streets",
                "explore.no_region.title", "explore.no_region.body", "explore.load_failed.title", "explore.load_failed.body",
                "explore.back_to_menu", "explore.spawn", "hud.kmh", "hud.distance_m", "hud.distance_km", "hud.eta_min",
                "hud.route.to", "hud.route.finding", "hud.route.rerouting", "hud.route.none", "hud.route.unavailable",
                "hud.arrived", "hud.teleporting", "hud.slow_down", "hud.time", "hud.surface.offroad", "hud.surface.inferred",
                "search.title", "search.placeholder", "search.suggestions", "search.empty", "search.unavailable", "search.ride",
                "search.teleport", "search.away", "pause.title", "pause.resume", "pause.main_menu", "hud.compass.n",
            });
            var devanagari = new Regex("[ऀ-ॿ]");
            foreach (string key in keys)
            {
                Assert.IsTrue(en.ContainsKey(key), "strings.en.json lacks " + key);
                Assert.IsTrue(ne.ContainsKey(key), "strings.ne.json lacks " + key);
                Assert.IsTrue(devanagari.IsMatch(ne[key]), "Nepali text for " + key + " has no Devanagari: " + ne[key]);
            }
            Assert.IsFalse(en.ContainsKey("menu.explore_soon"), "the 'scooter warming up' teaser is gone");
        }
    }
}
