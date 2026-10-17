using System.Collections.Generic;
using System.IO;
using Ghumante.App.Explore;
using Ghumante.Characters;
using Ghumante.Characters.Rides;
using Ghumante.Core.Driving;
using Ghumante.Core.Services;
using Ghumante.EditorTools;
using Ghumante.UI.Hud;
using Ghumante.UI.Localization;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Ghumante.Tests.EditMode
{
    /// <summary>
    /// The flying package's Unity side without a scene (owner playtest #11): the aircraft HUD builds over the Explore HUD,
    /// shows the instruments in both languages, hides the speedometer only while it shows and reads its touch controls;
    /// the pilot's controls map the stick and the throttle slider; every toast, prompt and craft name the ride uses is in
    /// both string tables. (The flight itself is covered headless by core-tests/Flight*.)
    /// </summary>
    public class FlightHudTests
    {
        private Localizer _localizer;
        private VisualElement _hudRoot, _speedo;

        [SetUp]
        public void SetUp()
        {
            _localizer = new Localizer();
            _localizer.AddTable(Localizer.English, File.ReadAllText(ProjectSetup.EnglishStringsPath));
            _localizer.AddTable(Localizer.Nepali, File.ReadAllText(ProjectSetup.NepaliStringsPath));
            _localizer.SetLocale(Localizer.English);
            _hudRoot = new VisualElement();
            _hudRoot.Add(new VisualElement { name = "hud-pad" });
            _hudRoot.Add(new VisualElement { name = "hud-controls" });
            _speedo = new VisualElement { name = "hud-speedo" };
            _hudRoot.Add(_speedo);
        }

        private static FlightInstruments Cruise()
        {
            return new FlightInstruments
            {
                Craft = FlightCraft.Turboprop, AirspeedKt = 182.4f, AglM = 914.4f, VerticalSpeedMps = 2.6f, HeadingDeg = 20f, Power = 0.71f, Flaps = 1,
                GearDown = 0f, RotateKt = 107f, StallKt = 98f,
            };
        }

        [Test]
        public void TheHudSitsAboveTheExplorerControlsAndShowsOnlyWhileFlying()
        {
            var hud = new AircraftHud(_hudRoot, _localizer, new NullHaptics());
            Assert.AreEqual(_hudRoot.IndexOf(_hudRoot.Q("hud-controls")) + 1, _hudRoot.IndexOf(hud.Root), "just above the explorer's controls");
            Assert.IsTrue(hud.Root.style.display == DisplayStyle.None, "hidden on foot");
            hud.Show(true, FlightCraft.Turboprop);
            Assert.IsTrue(hud.Visible);
            Assert.IsTrue(_speedo.style.display == DisplayStyle.None, "the km/h speedometer hides while flying");
            hud.Show(false);
            Assert.AreEqual(StyleKeyword.Null, _speedo.style.display.keyword, "and comes back on foot");
            hud.Dispose();
            Assert.IsNull(hud.Root.parent);
        }

        [Test]
        public void TheInstrumentsReadKnotsFeetAndClimbInBothLanguages()
        {
            var hud = new AircraftHud(_hudRoot, _localizer, new NullHaptics());
            hud.Show(true, FlightCraft.Turboprop);
            hud.UpdateHud(Cruise(), 0.7f, 0.016f);
            var texts = new List<string>();
            hud.Root.Query<Label>().ForEach(l => texts.Add(l.text));
            CollectionAssert.Contains(texts, "182", "airspeed in knots");
            CollectionAssert.Contains(texts, "3000", "914 m above the ground is 3,000 ft");
            CollectionAssert.Contains(texts, "+500", "2.6 m/s climb is about 500 ft/min");
            CollectionAssert.Contains(texts, "20", "heading");
            CollectionAssert.Contains(texts, "71", "power %");
            CollectionAssert.Contains(texts, _localizer.Get("flight.hud.gear_up"));

            _localizer.SetLocale(Localizer.Nepali);
            hud.UpdateHud(Cruise(), 0.7f, 0.016f);
            texts.Clear();
            hud.Root.Query<Label>().ForEach(l => texts.Add(l.text));
            CollectionAssert.Contains(texts, Localizer.ToDevanagariDigits("182"), "Nepali digits");
            CollectionAssert.Contains(texts, _localizer.Get("flight.hud.speed"));
            hud.Dispose();
        }

        [Test]
        public void StallAndTurnBackShowABanner()
        {
            var hud = new AircraftHud(_hudRoot, _localizer, new NullHaptics());
            hud.Show(true, FlightCraft.Commuter);
            FlightInstruments i = Cruise();
            i.StallWarning = true;
            hud.UpdateHud(i, 0.5f, 0.016f);
            var texts = new List<string>();
            hud.Root.Query<Label>().ForEach(l => texts.Add(l.text));
            CollectionAssert.Contains(texts, _localizer.Get("flight.hud.stall"));
            i.StallWarning = false;
            i.TurningBack = true;
            hud.UpdateHud(i, 0.5f, 0.016f);
            texts.Clear();
            hud.Root.Query<Label>().ForEach(l => texts.Add(l.text));
            CollectionAssert.Contains(texts, _localizer.Get("flight.hud.turn_back"));
            hud.Dispose();
        }

        [Test]
        public void UntouchedControlsLeaveTheLeverWhereItIs()
        {
            var hud = new AircraftHud(_hudRoot, _localizer, new NullHaptics());
            hud.Show(true, FlightCraft.Turboprop);
            FlightTouch t = hud.Read();
            Assert.Less(t.Throttle, 0f, "slider untouched");
            Assert.AreEqual(0f, t.StickX);
            Assert.AreEqual(0f, t.Rudder);
            Assert.IsFalse(t.Exit || t.View || t.FlapsDown || t.FlapsUp);
            hud.Dispose();
        }

        [Test]
        public void TheStickPitchesAndRollsAndTheSliderSetsTheLever()
        {
            var input = new AircraftInput();
            var frame = new ControlFrame();
            bool view, exit;
            var touch = new FlightTouch { StickX = 0.5f, StickY = -1f, Throttle = 0.8f, Active = true };
            FlightInput f = input.Read(0.016f, frame, touch, out view, out exit);
            Assert.AreEqual(0.8f, input.Lever, 1e-4f, "the slider puts the lever there");
            Assert.Greater(f.Pitch, 0.5f, "pulling the stick down the screen raises the nose");
            Assert.AreEqual(0.5f, f.Roll, 1e-4f);
            touch = new FlightTouch { Throttle = -1f };
            input.Read(0.016f, frame, touch, out view, out exit);
            Assert.AreEqual(0.8f, input.Lever, 1e-4f, "and it stays there when let go");
            input.Dispose();
        }

        [Test]
        public void EveryFlightStringIsInBothTables()
        {
            Dictionary<string, string> en = ExploreSample.Strings("en"), ne = ExploreSample.Strings("ne");
            var keys = new List<string>(AircraftRide.NoticeKeys) { "hud.action.board", "hud.prompt.board" };
            foreach (FlightCraft c in FlightSpec.All) keys.Add(ExploreSession.CraftNameKey(c));
            foreach (string k in keys)
            {
                Assert.IsTrue(en.ContainsKey(k), k + " (en)");
                Assert.IsTrue(ne.ContainsKey(k), k + " (ne)");
            }
        }
    }
}
