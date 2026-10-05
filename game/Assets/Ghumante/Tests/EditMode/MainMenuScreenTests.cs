using System;
using System.Collections.Generic;
using System.IO;
using Ghumante.Core.Services;
using Ghumante.EditorTools;
using Ghumante.UI;
using Ghumante.UI.Localization;
using Ghumante.UI.Motion;
using Ghumante.UI.Screens;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ghumante.Tests.EditMode
{
    /// <summary>
    /// Wiring of the animated main menu, driven without a panel: buttons are tapped with
    /// ScreenBase.SimulateTap (UI Toolkit dispatches no pointer events outside a panel) and time advances with
    /// UiAnimator.Tick.
    /// </summary>
    public class MainMenuScreenTests
    {
        /// <summary>Records every haptic request (the fake IHaptics).</summary>
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

        private static readonly MenuCounters Demo = new MenuCounters { hearts = 3, stars = 12, coins = 1250, energy = 80 };

        private readonly List<MainMenuScreen> _screens = new List<MainMenuScreen>();

        [TearDown]
        public void DisposeScreens()
        {
            foreach (MainMenuScreen s in _screens) s.Dispose();
            _screens.Clear();
        }

        private MainMenuScreen Create(RecordingHaptics haptics, MotionSettings motion, MenuCounters counters, out VisualElement root,
                                      Action<VisualElement> beforeScreen = null)
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ProjectSetup.MainMenuUxmlPath);
            Assert.IsNotNull(uxml, ProjectSetup.MainMenuUxmlPath);
            root = uxml.CloneTree();
            if (beforeScreen != null) beforeScreen(root);
            var localizer = new Localizer();
            localizer.AddTable(Localizer.English, File.ReadAllText(ProjectSetup.EnglishStringsPath));
            localizer.AddTable(Localizer.Nepali, File.ReadAllText(ProjectSetup.NepaliStringsPath));
            localizer.SetLocale(Localizer.English);
            var screen = new MainMenuScreen(root, localizer, counters, haptics, motion);
            _screens.Add(screen);
            return screen;
        }

        private static void Run(MainMenuScreen screen, float seconds)
        {
            for (float t = 0f; t < seconds; t += 0.05f) screen.Animator.Tick(0.05f);
        }

        private static Vector3 InlineScale(VisualElement e)
        {
            StyleScale s = e.style.scale;
            return s.keyword == StyleKeyword.Undefined ? s.value.value : Vector3.one;
        }

        [Test]
        public void PlusPlaysSuccessHapticAndCountsUp()
        {
            var haptics = new RecordingHaptics();
            MainMenuScreen screen = Create(haptics, new MotionSettings(), Demo, out VisualElement root);
            string added = null;
            screen.AddCounterRequested += id => added = id;

            Assert.IsTrue(screen.SimulateTap("counter-coins-add"));

            CollectionAssert.AreEqual(new[] { HapticKind.LightImpact, HapticKind.Success }, haptics.Played,
                                      "press-down tick, then the reward");
            Assert.AreEqual(1300, screen.Counters.coins);
            Assert.AreEqual("coins", added);
            Run(screen, MainMenuScreen.CountUpSeconds + 0.2f);
            Assert.AreEqual("1,300", root.Q<Label>("counter-coins-value").text, "the label rolls up to the new value");
        }

        [Test]
        public void PlusOnFullHeartsWarnsInsteadOfAdding()
        {
            var haptics = new RecordingHaptics();
            var full = Demo;
            full.hearts = MainMenuScreen.MaxHearts;
            MainMenuScreen screen = Create(haptics, new MotionSettings(), full, out VisualElement root);

            screen.SimulateTap("counter-hearts-add");

            Assert.AreEqual(MainMenuScreen.MaxHearts, screen.Counters.hearts);
            CollectionAssert.Contains(haptics.Played, HapticKind.Warning);
            CollectionAssert.DoesNotContain(haptics.Played, HapticKind.Success);
            Assert.IsTrue(root.Q("toast-bubble").ClassListContains("gh-toast--visible"));
        }

        [Test]
        public void ReduceMotionStillCountsAndBuzzesButShowsTheValueAtOnce()
        {
            var haptics = new RecordingHaptics();
            MainMenuScreen screen = Create(haptics, new MotionSettings(reduceMotion: true), Demo, out VisualElement root);
            screen.SimulateTap("counter-stars-add");
            Assert.AreEqual(13, screen.Counters.stars);
            Assert.AreEqual("13", root.Q<Label>("counter-stars-value").text);
            CollectionAssert.Contains(haptics.Played, HapticKind.Success, "haptics still work with Reduce motion");
        }

        [Test]
        public void ReduceMotionDisablesIdleAnimation()
        {
            var motion = new MotionSettings(reduceMotion: true);
            MainMenuScreen screen = Create(new RecordingHaptics(), motion, Demo, out VisualElement root);
            Button explore = root.Q<Button>("explore-button");

            Assert.IsFalse(screen.IdleAnimating);
            Run(screen, 2f);
            Assert.AreEqual(0f, screen.Animator.IdleTime, "idle clock frozen");
            Assert.AreEqual(Vector3.one, InlineScale(explore), "Explore does not breathe");

            // Switching Reduce motion off brings the menu to life...
            motion.ReduceMotion = false;
            Assert.IsTrue(screen.IdleAnimating);
            Run(screen, 1.5f);
            Assert.Greater(screen.Animator.IdleTime, 1f);
            Assert.Greater(Mathf.Abs(InlineScale(explore).x - 1f), 1e-4f, "Explore breathes");

            // ...and switching it on again puts everything back at rest at once.
            motion.ReduceMotion = true;
            Run(screen, 0.1f);
            Assert.AreEqual(1f, InlineScale(explore).x, 1e-4);
            Assert.AreEqual(1f, InlineScale(explore).y, 1e-4);
        }

        [Test]
        public void UnfocusedAppFreezesIdleLoopsOnlyWhenAsked()
        {
            var motion = new MotionSettings();
            MainMenuScreen screen = Create(new RecordingHaptics(), motion, Demo, out VisualElement _);
            motion.AppFocused = false;
            Assert.IsFalse(screen.IdleAnimating, "players pause idle motion without focus");
            motion.PauseWhenUnfocused = false;
            Assert.IsTrue(screen.IdleAnimating, "the editor keeps it running");
        }

        [Test]
        public void EntranceEndsAtRestWithinAboutASecond()
        {
            MainMenuScreen screen = Create(new RecordingHaptics(), new MotionSettings(), Demo, out VisualElement root);
            VisualElement map = root.Q<Button>("map-button");
            Assert.Less(InlineScale(map).x, 0.9f, "pills start small (pop-in)");
            Assert.AreEqual(0f, root.Q("counter-coins").style.opacity.value, 1e-4, "counters start hidden");

            Run(screen, 1.25f);
            Assert.AreEqual(1f, InlineScale(map).x, 1e-3);
            Assert.AreEqual(1f, root.Q("counter-coins").style.opacity.value, 1e-4);
            Assert.AreEqual(0f, root.Q("counter-coins").style.translate.value.y.value, 1e-3);
            Assert.AreEqual(0f, root.Q("bottombar").style.translate.value.y.value, 1e-3);
        }

        [Test]
        public void ExploreTeaserWigglesWithAWarningAndAToast()
        {
            var haptics = new RecordingHaptics();
            MainMenuScreen screen = Create(haptics, new MotionSettings(), Demo, out VisualElement root);
            screen.ExploreRequested += screen.PlayExploreTeaser;  // what Bootstrap wires until M1

            screen.SimulateTap("explore-button");

            CollectionAssert.AreEqual(new[] { HapticKind.MediumImpact, HapticKind.Warning }, haptics.Played);
            VisualElement toast = root.Q("toast-bubble");
            Assert.IsTrue(toast.ClassListContains("gh-toast--visible"));
            StringAssert.Contains("scooter", root.Q<Label>("toast").text);
            Run(screen, MainMenuScreen.ToastSeconds + 0.1f);
            Assert.IsFalse(toast.ClassListContains("gh-toast--visible"), "the toast leaves by itself");
        }

        [Test]
        public void SettingsSwitchesApplyAndReport()
        {
            var haptics = new RecordingHaptics();
            var motion = new MotionSettings();
            MainMenuScreen screen = Create(haptics, motion, Demo, out VisualElement root);
            var hapticsEvents = new List<bool>();
            var motionEvents = new List<bool>();
            screen.HapticsChanged += hapticsEvents.Add;
            screen.ReduceMotionChanged += motionEvents.Add;
            screen.SettingsRequested += screen.OpenSettings;

            screen.SimulateTap("settings-button");
            Assert.IsTrue(screen.SettingsOpen);
            Assert.IsTrue(root.Q("settings-layer").ClassListContains(SettingsSheet.OpenClass));

            screen.SimulateTap("settings-haptics-toggle");  // off
            Assert.IsFalse(haptics.Enabled);
            haptics.Played.Clear();
            screen.SimulateTap("settings-haptics-toggle");  // back on: answers with a buzz
            Assert.IsTrue(haptics.Enabled);
            CollectionAssert.AreEqual(new[] { HapticKind.Success }, haptics.Played,
                                      "the press tick is silent while off; switching on plays Success");
            CollectionAssert.AreEqual(new[] { false, true }, hapticsEvents);

            screen.SimulateTap("settings-motion-toggle");
            Assert.IsTrue(motion.ReduceMotion);
            Assert.IsTrue(root.Q<Button>("settings-motion-toggle").ClassListContains(SettingsSheet.ToggleOnClass));
            CollectionAssert.AreEqual(new[] { true }, motionEvents);

            screen.SimulateTap("settings-done");
            Run(screen, 0.5f);
            Assert.IsFalse(screen.SettingsOpen);
            Assert.IsFalse(root.Q("settings-layer").ClassListContains(SettingsSheet.OpenClass), "hidden after sliding out");
        }

        [Test]
        public void FeelTheHapticsPlaysEveryKindInTurn()
        {
            var haptics = new RecordingHaptics();
            MainMenuScreen screen = Create(haptics, new MotionSettings(), Demo, out VisualElement _);
            screen.OpenSettings();
            screen.SimulateTap("settings-feel-play");
            haptics.Played.Clear();  // the press tick
            Run(screen, 7f);
            CollectionAssert.AreEqual(SettingsSheet.FeelOrder, haptics.Played);
        }

        [Test]
        public void LanguagePickerRequestsTheOtherLocale()
        {
            MainMenuScreen screen = Create(new RecordingHaptics(), new MotionSettings(), Demo, out VisualElement _);
            string picked = null;
            screen.LanguageSelected += l => picked = l;
            screen.OpenSettings();
            screen.SimulateTap("settings-lang-en");
            Run(screen, 0.3f);
            Assert.IsNull(picked, "English is already active");
            screen.SimulateTap("settings-lang-ne");
            Run(screen, 0.3f);  // the swap happens mid-flip
            Assert.AreEqual(Localizer.Nepali, picked);
        }

        [Test]
        public void PrayerFlagsHangInLungtaOrder()
        {
            MainMenuScreen screen = Create(new RecordingHaptics(), new MotionSettings(), Demo, out VisualElement root);
            Assert.AreEqual(LivingBackdrop.FlagCount, screen.PrayerFlagCount);
            string[] order = { "gh-flag--blue", "gh-flag--white", "gh-flag--red", "gh-flag--green", "gh-flag--yellow" };
            for (int i = 0; i < screen.PrayerFlagCount; i++)
            {
                VisualElement flag = root.Q("bg-flag-" + i);
                Assert.IsNotNull(flag, "flag " + i);
                Assert.IsTrue(flag.ClassListContains(order[i % 5]), "flag " + i);
            }

            MainMenuScreen low = Create(new RecordingHaptics(), new MotionSettings(lowPower: true), Demo, out VisualElement _);
            Assert.AreEqual(LivingBackdrop.LowPowerFlagCount, low.PrayerFlagCount, "fewer flags on the Low tier");
        }

        [Test]
        public void RopeCurveMatchesTheArt()
        {
            // make_ui_art.py flag_rope(): y = 0.08 + 0.80 * (1 - (2p - 1)^2).
            Assert.AreEqual(0.08f, LivingBackdrop.RopeY(0f), 1e-6);
            Assert.AreEqual(0.88f, LivingBackdrop.RopeY(0.5f), 1e-6);
            Assert.AreEqual(0.08f, LivingBackdrop.RopeY(1f), 1e-6);
        }

        [Test]
        public void BackdropAndSheetBleedUnderThePaddingSafeAreaWrote()
        {
            // SafeArea writes the insets inline on the document root inside its GeometryChangedEvent; the
            // resolved padding still holds the previous orientation's values then (and here, without a panel,
            // nothing at all), so the bleeds must follow what SafeArea wrote.
            MainMenuScreen screen = Create(new RecordingHaptics(), new MotionSettings(), Demo, out VisualElement root, r =>
            {
                r.style.paddingLeft = 59f;
                r.style.paddingTop = 0f;
                r.style.paddingRight = 47f;
                r.style.paddingBottom = 21f;
            });
            Assert.IsNotNull(screen);
            IStyle backdrop = root.Q("backdrop").style;
            Assert.AreEqual(-59f, backdrop.left.value.value, 1e-3);
            Assert.AreEqual(0f, backdrop.top.value.value, 1e-3);
            Assert.AreEqual(-47f, backdrop.right.value.value, 1e-3);
            Assert.AreEqual(-21f, backdrop.bottom.value.value, 1e-3);

            IStyle layer = root.Q("settings-layer").style;
            Assert.AreEqual(-59f, layer.left.value.value, 1e-3);
            Assert.AreEqual(-47f, layer.right.value.value, 1e-3);
            Assert.AreEqual(-21f, layer.bottom.value.value, 1e-3);
            IStyle content = root.Q("settings-content").style;
            bool portrait = OrientationWatcher.Detect() == LayoutOrientation.Portrait;
            Assert.AreEqual(portrait ? 59f : 0f, content.marginLeft.value.value, 1e-3, "the side panel does not touch the left edge");
            Assert.AreEqual(47f, content.marginRight.value.value, 1e-3);
            Assert.AreEqual(21f, content.marginBottom.value.value, 1e-3);
        }

        [Test]
        public void CounterPillsAndSettingsRowsAreTouchTargets()
        {
            var haptics = new RecordingHaptics();
            var motion = new MotionSettings();
            MainMenuScreen screen = Create(haptics, motion, Demo, out VisualElement _);

            Assert.IsTrue(screen.SimulateTap("counter-coins"), "the whole pill presses its +");
            Assert.AreEqual(1300, screen.Counters.coins);
            CollectionAssert.AreEqual(new[] { HapticKind.LightImpact, HapticKind.Success }, haptics.Played);

            screen.OpenSettings();
            Assert.IsTrue(screen.SimulateTap("settings-row-motion"), "the row flips its switch");
            Assert.IsTrue(motion.ReduceMotion);
            Assert.IsTrue(screen.SimulateTap("settings-row-haptics"));
            Assert.IsFalse(haptics.Enabled);
            Assert.IsFalse(screen.SimulateTap("settings-row-language"), "two choices: the row cannot pick one");
        }

        [Test]
        public void SheetOvershootNeverLiftsItOffTheScreenEdge()
        {
            MainMenuScreen screen = Create(new RecordingHaptics(), new MotionSettings(), Demo, out VisualElement root);
            VisualElement sheet = root.Q("settings-sheet");
            screen.OpenSettings();
            float stretched = 1f;
            for (int i = 0; i < 40; i++)
            {
                screen.Animator.Tick(1f / 60f);
                Vector2 t = new Vector2(sheet.style.translate.value.x.value, sheet.style.translate.value.y.value);
                Assert.GreaterOrEqual(t.x, -1e-3f, "moved past its right edge at tick " + i);
                Assert.GreaterOrEqual(t.y, -1e-3f, "moved past its bottom edge at tick " + i);
                Vector3 scale = InlineScale(sheet);
                stretched = Mathf.Max(stretched, Mathf.Max(scale.x, scale.y));
            }
            Assert.Greater(stretched, 1.02f, "the bounce is a stretch from the edge");
            Assert.AreEqual(1f, Mathf.Max(InlineScale(sheet).x, InlineScale(sheet).y), 1e-3, "settles unstretched");
        }

        [Test]
        public void BackClosesSettingsWithATap()
        {
            var haptics = new RecordingHaptics();
            MainMenuScreen screen = Create(haptics, new MotionSettings(), Demo, out VisualElement root);
            Assert.IsFalse(screen.HandleBack(), "nothing to close");
            screen.OpenSettings();
            Assert.IsTrue(screen.HandleBack());
            CollectionAssert.AreEqual(new[] { HapticKind.LightImpact }, haptics.Played);
            Run(screen, 0.5f);
            Assert.IsFalse(screen.SettingsOpen);
            Assert.IsFalse(root.Q("settings-layer").ClassListContains(SettingsSheet.OpenClass));
        }

        [Test]
        public void ParticlesDrawOverTheToast()
        {
            MainMenuScreen screen = Create(new RecordingHaptics(), new MotionSettings(), Demo, out VisualElement root);
            Assert.IsNotNull(screen);
            VisualElement fx = root.Q("fx-layer");
            VisualElement toast = root.Q("toast-bubble");
            VisualElement settings = root.Q("settings-layer");
            Assert.AreSame(fx.parent, toast.parent);
            Assert.Greater(fx.parent.IndexOf(fx), fx.parent.IndexOf(toast), "the scooter's exhaust shows over its bubble");
            Assert.Less(fx.parent.IndexOf(fx), fx.parent.IndexOf(settings), "Settings covers the effects");
        }

        [Test]
        public void DisposeStopsEverything()
        {
            var haptics = new RecordingHaptics();
            MainMenuScreen screen = Create(haptics, new MotionSettings(), Demo, out VisualElement _);
            screen.Dispose();
            _screens.Remove(screen);
            float t = screen.Animator.Time;
            screen.Animator.Tick(0.1f);
            Assert.AreEqual(t, screen.Animator.Time, "a disposed animator does not tick");
            Assert.IsFalse(screen.SimulateTap("counter-coins-add"), "buttons are unwired");
            Assert.IsEmpty(haptics.Played);
        }
    }
}
