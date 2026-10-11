using System;
using System.Collections.Generic;
using System.IO;
using Ghumante.Core.Platform;
using Ghumante.Core.Services;
using Ghumante.EditorTools;
using Ghumante.UI.Localization;
using Ghumante.UI.Motion;
using Ghumante.UI.Screens;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ghumante.Tests.EditMode
{
    /// <summary>
    /// Settings > Graphics quality and Frame rate (SettingsSheet) on the main menu and in Explore, driven without a panel
    /// (ScreenBase.SimulateTap) against a fake <see cref="IQualityOptions"/>: the Auto option names the tier ("Auto
    /// (Medium)"), picks reach the options, 60 fps is greyed out where the tier does not allow it, the redraw note, and
    /// both languages.
    /// </summary>
    public class QualitySettingsSheetTests
    {
        /// <summary>An in-memory IQualityOptions that resolves like App's director.</summary>
        private sealed class FakeQuality : IQualityOptions
        {
            public GraphicsChoice ChoiceValue = GraphicsChoice.Auto;
            public QualityTier Auto = QualityTier.Mid;
            public QualityTier Detected = QualityTier.Mid;
            public int ChosenFps;
            public bool Pending;
            public readonly List<string> Calls = new List<string>();

            public GraphicsChoice Choice
            {
                get { return ChoiceValue; }
            }

            public QualityTier AutoTier
            {
                get { return Auto; }
            }

            public QualityTier DetectedTier
            {
                get { return Detected; }
            }

            public QualityTier ActiveTier
            {
                get { return QualityChoices.Resolve(ChoiceValue, Auto); }
            }

            public int FrameRate
            {
                get { return QualityChoices.FrameRate(ActiveTier, ChosenFps); }
            }

            public int MaxFrameRate
            {
                get { return QualityTiers.MaxFrameRate(ActiveTier); }
            }

            public bool WorldRedrawPending
            {
                get { return Pending; }
            }

            public event Action Changed;

            public void SetChoice(GraphicsChoice choice)
            {
                Calls.Add("choice:" + choice);
                ChoiceValue = choice;
                Raise();
            }

            public void SetFrameRate(int fps)
            {
                Calls.Add("fps:" + fps);
                ChosenFps = fps;
                Raise();
            }

            public void Raise()
            {
                Action h = Changed;
                if (h != null) h();
            }
        }

        private sealed class RecordingHaptics : IHaptics
        {
            public readonly List<HapticKind> Played = new List<HapticKind>();

            public bool IsSupported
            {
                get { return true; }
            }

            public bool Enabled { get; set; } = true;

            public void Play(HapticKind kind)
            {
                if (Enabled) Played.Add(kind);
            }
        }

        private readonly List<ScreenBase> _screens = new List<ScreenBase>();
        private Localizer _localizer;

        [TearDown]
        public void DisposeScreens()
        {
            foreach (ScreenBase s in _screens) s.Dispose();
            _screens.Clear();
        }

        private Localizer NewLocalizer()
        {
            var localizer = new Localizer();
            localizer.AddTable(Localizer.English, File.ReadAllText(ProjectSetup.EnglishStringsPath));
            localizer.AddTable(Localizer.Nepali, File.ReadAllText(ProjectSetup.NepaliStringsPath));
            localizer.SetLocale(Localizer.English);
            return localizer;
        }

        private MainMenuScreen Menu(RecordingHaptics haptics, out VisualElement root)
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ProjectSetup.MainMenuUxmlPath);
            Assert.IsNotNull(uxml, ProjectSetup.MainMenuUxmlPath);
            root = uxml.CloneTree();
            _localizer = NewLocalizer();
            var counters = new MenuCounters { hearts = 3, stars = 12, coins = 1250, energy = 80 };
            var screen = new MainMenuScreen(root, _localizer, counters, haptics, new MotionSettings(reduceMotion: true));
            _screens.Add(screen);
            return screen;
        }

        private ExploreScreen Explore(RecordingHaptics haptics, out VisualElement root)
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ProjectSetup.ExploreUxmlPath);
            Assert.IsNotNull(uxml, ProjectSetup.ExploreUxmlPath);
            root = uxml.CloneTree();
            _localizer = NewLocalizer();
            var screen = new ExploreScreen(root, _localizer, haptics, new MotionSettings(reduceMotion: true));
            _screens.Add(screen);
            return screen;
        }

        private static bool Selected(VisualElement root, string name)
        {
            return root.Q<Button>(name).ClassListContains(SettingsSheet.SelectedClass);
        }

        private static bool Shown(VisualElement root, string name)
        {
            return root.Q(name).style.display != DisplayStyle.None;
        }

        [Test]
        public void RowsHideUntilQualityIsBound()
        {
            MainMenuScreen screen = Menu(new RecordingHaptics(), out VisualElement root);
            Assert.IsFalse(Shown(root, "settings-row-graphics"));
            Assert.IsFalse(Shown(root, "settings-row-fps"));
            screen.BindQuality(new FakeQuality());
            Assert.IsTrue(Shown(root, "settings-row-graphics"));
            Assert.IsTrue(Shown(root, "settings-row-fps"));
            screen.BindQuality(null);
            Assert.IsFalse(Shown(root, "settings-row-graphics"));
        }

        [Test]
        public void AutoShowsTheTierThePhoneRuns()
        {
            MainMenuScreen screen = Menu(new RecordingHaptics(), out VisualElement root);
            var q = new FakeQuality { Auto = QualityTier.Mid, Detected = QualityTier.Mid };
            screen.BindQuality(q);
            Assert.AreEqual("Auto (Medium)", root.Q<Button>("settings-graphics-auto").text);
            Assert.AreEqual("Your phone: Medium", root.Q<Label>("settings-graphics-status").text);
            Assert.IsTrue(Selected(root, "settings-graphics-auto"));
            Assert.IsFalse(Selected(root, "settings-graphics-medium"));

            // The governor stepped Auto down on this phone: the option and the status say so.
            q.Auto = QualityTier.Low;
            q.Raise();
            Assert.AreEqual("Auto (Low)", root.Q<Button>("settings-graphics-auto").text);
            StringAssert.Contains("Lowered to Low", root.Q<Label>("settings-graphics-status").text);

            _localizer.SetLocale(Localizer.Nepali);
            Assert.AreEqual("स्वचालित (न्यून)", root.Q<Button>("settings-graphics-auto").text);
            Assert.AreEqual("न्यून", root.Q<Button>("settings-graphics-low").text, "the fixed options are localised by key");
        }

        [Test]
        public void PickingATierReachesTheOptions()
        {
            var haptics = new RecordingHaptics();
            MainMenuScreen screen = Menu(haptics, out VisualElement root);
            var q = new FakeQuality();
            screen.BindQuality(q);
            Assert.IsTrue(screen.SimulateTap("settings-graphics-high"));
            CollectionAssert.AreEqual(new[] { "choice:High" }, q.Calls);
            Assert.IsTrue(Selected(root, "settings-graphics-high"));
            Assert.IsFalse(Selected(root, "settings-graphics-auto"));
            CollectionAssert.Contains(haptics.Played, HapticKind.Selection);
            screen.SimulateTap("settings-graphics-high");
            Assert.AreEqual(1, q.Calls.Count, "picking the selected option again does nothing");
            screen.SimulateTap("settings-graphics-auto");
            Assert.AreEqual("choice:Auto", q.Calls[1]);
        }

        [Test]
        public void SixtyFpsNeedsMediumOrHigh()
        {
            var haptics = new RecordingHaptics();
            MainMenuScreen screen = Menu(haptics, out VisualElement root);
            var q = new FakeQuality { ChoiceValue = GraphicsChoice.Low };
            screen.BindQuality(q);
            Button sixty = root.Q<Button>("settings-fps-60");
            Assert.AreEqual("30 fps", root.Q<Button>("settings-fps-30").text);
            Assert.IsTrue(sixty.ClassListContains(SettingsSheet.UnavailableClass));
            Assert.IsTrue(Selected(root, "settings-fps-30"));
            StringAssert.Contains("Pick Medium or High for 60", root.Q<Label>("settings-fps-hint").text);

            screen.SimulateTap("settings-fps-60");
            Assert.AreEqual(0, q.Calls.Count, "Low cannot run 60");
            CollectionAssert.Contains(haptics.Played, HapticKind.Warning);

            screen.SimulateTap("settings-graphics-medium");
            Assert.IsFalse(sixty.ClassListContains(SettingsSheet.UnavailableClass));
            screen.SimulateTap("settings-fps-60");
            CollectionAssert.AreEqual(new[] { "choice:Medium", "fps:60" }, q.Calls);
            Assert.IsTrue(Selected(root, "settings-fps-60"));
            Assert.IsFalse(Selected(root, "settings-fps-30"));

            _localizer.SetLocale(Localizer.Nepali);
            Assert.AreEqual("६० FPS", sixty.text, "Nepali digits");
        }

        [Test]
        public void TheRedrawNoteShowsWhileTheWorldStillHasToFollow()
        {
            ExploreScreen screen = Explore(new RecordingHaptics(), out VisualElement root);
            var q = new FakeQuality();
            screen.BindQuality(q);
            Assert.IsFalse(Shown(root, "settings-graphics-note"));
            q.Pending = true;
            screen.SimulateTap("settings-graphics-low");
            Assert.IsTrue(Shown(root, "settings-graphics-note"));
            Assert.AreEqual("The streets around you redraw when you close Settings.", root.Q<Label>("settings-graphics-note").text);
            q.Pending = false;
            q.Raise();
            Assert.IsFalse(Shown(root, "settings-graphics-note"));
        }

        [Test]
        public void ExploreSheetHasTheSameRows()
        {
            ExploreScreen screen = Explore(new RecordingHaptics(), out VisualElement root);
            var q = new FakeQuality { Auto = QualityTier.High, Detected = QualityTier.High };
            screen.BindQuality(q);
            Assert.AreEqual("Auto (High)", root.Q<Button>("settings-graphics-auto").text);
            Assert.IsTrue(Selected(root, "settings-fps-60"), "High runs 60 by default");
            Assert.IsTrue(screen.SimulateTap("settings-fps-30"));
            CollectionAssert.AreEqual(new[] { "fps:30" }, q.Calls);
        }
    }
}
