using System.Collections.Generic;
using System.IO;
using Ghumante.App.Explore;
using Ghumante.Characters;
using Ghumante.Core.Data;
using Ghumante.Core.Services;
using Ghumante.EditorTools;
using Ghumante.UI.Hud;
using Ghumante.UI.Localization;
using Ghumante.UI.Motion;
using Ghumante.UI.Screens;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ghumante.Tests.EditMode
{
    /// <summary>
    /// The Explore HUD (M1 track D) driven without a panel: buttons are tapped with ScreenBase.SimulateTap and time advances
    /// with UiAnimator.Tick. Loading and the no-region message, speed and surface in both languages, Walk/Ride, pause,
    /// search to "Ride there", the route banner and the arrival celebration.
    /// </summary>
    public class ExploreScreenTests
    {
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

        private readonly List<ExploreScreen> _screens = new List<ExploreScreen>();
        private Localizer _localizer;

        [TearDown]
        public void DisposeScreens()
        {
            foreach (ExploreScreen s in _screens) s.Dispose();
            _screens.Clear();
        }

        private ExploreScreen Create(RecordingHaptics haptics, out VisualElement root, bool reduceMotion = false)
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ProjectSetup.ExploreUxmlPath);
            Assert.IsNotNull(uxml, ProjectSetup.ExploreUxmlPath);
            root = uxml.CloneTree();
            _localizer = new Localizer();
            _localizer.AddTable(Localizer.English, File.ReadAllText(ProjectSetup.EnglishStringsPath));
            _localizer.AddTable(Localizer.Nepali, File.ReadAllText(ProjectSetup.NepaliStringsPath));
            _localizer.SetLocale(Localizer.English);
            var screen = new ExploreScreen(root, _localizer, haptics, new MotionSettings(reduceMotion));
            _screens.Add(screen);
            return screen;
        }

        private static void Run(ExploreScreen screen, float seconds)
        {
            for (float t = 0f; t < seconds; t += 0.05f) screen.Animator.Tick(0.05f);
        }

        [Test]
        public void StartsLoadingAndExplainsAMissingRegion()
        {
            var haptics = new RecordingHaptics();
            ExploreScreen screen = Create(haptics, out VisualElement root);
            Assert.IsTrue(screen.IsLoading);
            Assert.IsTrue(screen.BlocksGameplay);
            screen.SetLoadingProgress(0.42f);
            Assert.AreEqual("42%", root.Q<Label>("loading-percent").text);

            int menu = 0;
            screen.MenuRequested += () => menu++;
            screen.ShowNoRegion();
            VisualElement layer = root.Q("loading-layer");
            Assert.IsTrue(layer.ClassListContains(ExploreScreen.LoadingFailedClass));
            StringAssert.Contains("Project Setup", root.Q<Label>("loading-help").text);
            StringAssert.Contains("Import Region Pack", root.Q<Label>("loading-help").text);
            CollectionAssert.Contains(haptics.Played, HapticKind.Warning);
            Assert.IsTrue(screen.SimulateTap("loading-back"));
            Assert.AreEqual(1, menu, "Back to menu");
        }

        [Test]
        public void TheHudComesInWhenTheWorldIsReady()
        {
            ExploreScreen screen = Create(new RecordingHaptics(), out VisualElement root);
            screen.HideLoading();
            Assert.IsFalse(screen.IsLoading);
            Assert.IsFalse(screen.BlocksGameplay);
            Run(screen, 1f);
            Assert.IsTrue(root.Q("loading-layer").ClassListContains(ExploreScreen.LoadingHiddenClass));
            Assert.AreEqual(1f, root.Q("hud-top").style.opacity.value, 1e-3f);
            Assert.IsNotNull(root.Q<Label>("hud-attribution"));
            StringAssert.Contains("OpenStreetMap", root.Q<Label>("hud-attribution").text, "the credit is always on screen");
        }

        [Test]
        public void SpeedUsesNepaliNumeralsInNepali()
        {
            ExploreScreen screen = Create(new RecordingHaptics(), out VisualElement root);
            Label speed = root.Q<Label>("hud-speed");
            screen.SetSpeed(85f / 3.6f);
            Assert.AreEqual("85", speed.text);
            _localizer.SetLocale(Localizer.Nepali);
            screen.SetSpeed(85f / 3.6f);
            Assert.AreEqual("८५", speed.text);
            Assert.AreEqual("कि.मि./घण्टा", root.Q<Label>("hud-speed-unit").text);
        }

        [Test]
        public void SurfaceChipNamesTheSurfaceAndMarksGuesses()
        {
            ExploreScreen screen = Create(new RecordingHaptics(), out VisualElement root);
            VisualElement chip = root.Q("hud-surface");
            screen.SetSurface(SurfaceGroup.Paved, true, Surface.Asphalt, false);
            Assert.AreEqual("Asphalt", root.Q<Label>("hud-surface-label").text);
            Assert.IsTrue(chip.ClassListContains("gh-hud__surface--paved"));
            Assert.IsFalse(root.Q("hud-surface-inferred").ClassListContains(ExploreScreen.InferredOnClass));

            screen.SetSurface(SurfaceGroup.Dirt, true, Surface.Dirt, true);
            Assert.AreEqual("Dirt", root.Q<Label>("hud-surface-label").text);
            Assert.IsTrue(chip.ClassListContains("gh-hud__surface--dirt"));
            Assert.IsFalse(chip.ClassListContains("gh-hud__surface--paved"));
            Assert.IsTrue(root.Q("hud-surface-inferred").ClassListContains(ExploreScreen.InferredOnClass), "inferred: the dot");

            screen.SetSurface(SurfaceGroup.Mud, false, Surface.Unknown, true);
            Assert.AreEqual("Mud · off road", root.Q<Label>("hud-surface-label").text);
            Assert.IsFalse(root.Q("hud-surface-inferred").ClassListContains(ExploreScreen.InferredOnClass), "no road, no guess");

            _localizer.SetLocale(Localizer.Nepali);
            Assert.AreEqual("हिलो · सडक बाहिर", root.Q<Label>("hud-surface-label").text);
        }

        [Test]
        public void PlaceNameFollowsTheLanguage()
        {
            ExploreScreen screen = Create(new RecordingHaptics(), out VisualElement root);
            screen.SetPlace(new NameRecord("Thamel", "Thamel", "ठमेल"));
            Assert.AreEqual("Thamel", root.Q<Label>("hud-place-name").text);
            _localizer.SetLocale(Localizer.Nepali);
            Assert.AreEqual("ठमेल", root.Q<Label>("hud-place-name").text);
            screen.SetPlace(null);
            Assert.IsTrue(root.Q("hud-place").style.display == DisplayStyle.None);
        }

        [Test]
        public void WalkRideButtonAndLayout()
        {
            var haptics = new RecordingHaptics();
            ExploreScreen screen = Create(haptics, out VisualElement root);
            screen.HideLoading();
            int toggles = 0;
            screen.ModeToggleRequested += () => toggles++;
            Assert.IsTrue(screen.SimulateTap("hud-mode"));
            Assert.AreEqual(1, toggles);
            CollectionAssert.Contains(haptics.Played, HapticKind.MediumImpact);
            VisualElement styled = root.Q(className: "gh-root");
            Assert.IsTrue(styled.ClassListContains(ExploreScreen.RideClass));
            screen.SetMode(false);
            Assert.IsTrue(styled.ClassListContains(ExploreScreen.WalkClass));
            Assert.IsFalse(styled.ClassListContains(ExploreScreen.RideClass));
            Assert.IsFalse(screen.Touch.Riding);
        }

        [Test]
        public void W2LayoutsActionButtonAndPrompt()
        {
            var haptics = new RecordingHaptics();
            ExploreScreen screen = Create(haptics, out VisualElement root);
            screen.HideLoading();
            VisualElement styled = root.Q(className: "gh-root");
            screen.SetLayout(Ghumante.Core.Characters.ControlLayout.Passenger);
            Assert.IsTrue(styled.ClassListContains(ExploreScreen.PassengerClass));
            Assert.IsFalse(styled.ClassListContains(ExploreScreen.RideClass));
            screen.SetLayout(Ghumante.Core.Characters.ControlLayout.Heavy);
            Assert.IsTrue(styled.ClassListContains(ExploreScreen.RideClass));
            Assert.IsTrue(styled.ClassListContains(ExploreScreen.HeavyClass));
            Assert.IsTrue(screen.Touch.Riding);
            screen.SetAction("hud.action.hop_on", "hop", true);
            CollectionAssert.Contains(haptics.Played, HapticKind.Selection);
            Assert.AreEqual("Hop on", root.Q<Label>("hud-action-label").text);
            screen.SetPrompt("hud.prompt.rest_outside");
            Assert.IsTrue(root.Q("hud-prompt").ClassListContains(ExploreScreen.PromptOnClass));
            screen.SetPrompt(null);
            Assert.IsFalse(root.Q("hud-prompt").ClassListContains(ExploreScreen.PromptOnClass));
            int garage = 0;
            screen.GarageRequested += () => garage++;
            Assert.IsTrue(screen.SimulateTap("hud-garage"));
            Assert.AreEqual(1, garage);
        }

        [Test]
        public void MenuPausesAndOffersTheMainMenu()
        {
            ExploreScreen screen = Create(new RecordingHaptics(), out VisualElement root);
            screen.HideLoading();
            var pauses = new List<bool>();
            screen.PauseChanged += pauses.Add;
            int menu = 0;
            screen.MenuRequested += () => menu++;

            screen.SimulateTap("hud-menu");
            Assert.IsTrue(screen.IsPaused);
            Assert.IsTrue(screen.BlocksGameplay);
            Assert.IsTrue(root.Q("pause-layer").ClassListContains(ExploreScreen.PauseOpenClass));
            screen.SimulateTap("pause-settings");
            Assert.IsTrue(screen.SettingsOpen, "the shared Settings sheet");
            Assert.IsTrue(screen.Back(), "back closes Settings first");
            Assert.IsFalse(screen.SettingsOpen);
            Assert.IsTrue(screen.IsPaused);
            Assert.IsTrue(screen.Back(), "then resumes");
            Assert.IsFalse(screen.IsPaused);
            CollectionAssert.AreEqual(new[] { true, false }, pauses);

            Assert.IsFalse(screen.Back(true), "gamepad B is the brake: it never pauses");
            Assert.IsTrue(screen.Back(), "Escape / Android back pause");
            screen.SimulateTap("pause-menu");
            Assert.AreEqual(1, menu);
        }

        [Test]
        public void SearchThenRideThere()
        {
            var haptics = new RecordingHaptics();
            ExploreScreen screen = Create(haptics, out VisualElement root);
            screen.HideLoading();
            screen.Search.Engine = ExploreSample.Search;
            screen.Search.SetPlayerPosition(SampleRegion.ThamelX, SampleRegion.ThamelZ);
            SearchEntry chosen = null;
            screen.RideToRequested += e => chosen = e;

            screen.SimulateTap("hud-search");
            Assert.IsTrue(screen.SearchOpen);
            Assert.IsTrue(screen.BlocksGameplay, "the ride pauses while searching");
            Assert.Greater(screen.Search.Results.Count, 0, "landmarks are suggested before typing");

            screen.Search.SetQuery("Boudha");
            Assert.Greater(screen.Search.Results.Count, 0);
            StringAssert.StartsWith("Boudh", screen.Search.Results[0].DisplayName);
            StringAssert.Contains("km", root.Q<Label>(className: "gh-result__meta").text, "kind and distance");
            Assert.IsTrue(root.Q<Button>("search-teleport-0").style.display == DisplayStyle.None, "release builds: no teleport");

            Assert.IsTrue(screen.SimulateTap("search-ride-0"));
            Assert.AreSame(screen.Search.Results[0], chosen);
            Assert.IsFalse(screen.SearchOpen, "the sheet closes to ride");

            screen.Search.TeleportAllowed = true;
            Assert.IsTrue(root.Q<Button>("search-teleport-0").style.display == DisplayStyle.Flex);
        }

        [Test]
        public void RouteBannerShowsDistanceAndTime()
        {
            ExploreScreen screen = Create(new RecordingHaptics(), out VisualElement root);
            screen.HideLoading();
            screen.ShowRoute("Boudhanath Stupa", "hud.route.finding");
            Assert.IsTrue(screen.RouteVisible);
            StringAssert.Contains("Finding the way", root.Q<Label>("hud-route-info").text);
            screen.SetRouteProgress(4949, 481, 0.5f);
            Assert.AreEqual("To Boudhanath Stupa", root.Q<Label>("hud-route-to").text);
            Assert.AreEqual("4.9 km · 9 min", root.Q<Label>("hud-route-info").text);
            _localizer.SetLocale(Localizer.Nepali);
            Assert.AreEqual("४.९ कि.मि. · ९ मिनेट", root.Q<Label>("hud-route-info").text);
            screen.HideRoute();
            Assert.IsFalse(screen.RouteVisible);
        }

        [Test]
        public void RouteNameFollowsALanguageSwitchMidRoute()
        {
            // Regression: the destination used to be cached as an English string, so the banner and the arrival toast
            // stayed English after switching to Nepali in Settings.
            ExploreScreen screen = Create(new RecordingHaptics(), out VisualElement root);
            screen.HideLoading();
            var boudha = new NameRecord("Boudhanath", "Boudhanath Stupa", "बौद्धनाथ स्तूप");
            screen.ShowRoute(boudha, "hud.route.finding");
            Assert.AreEqual("To Boudhanath Stupa", root.Q<Label>("hud-route-to").text);
            _localizer.SetLocale(Localizer.Nepali);
            StringAssert.Contains("बौद्धनाथ स्तूप", root.Q<Label>("hud-route-to").text);
            screen.CelebrateArrival(boudha);
            StringAssert.Contains("बौद्धनाथ स्तूप", root.Q<Label>("toast").text);
        }

        [Test]
        public void ToastsDropBelowTheRouteBanner()
        {
            ExploreScreen screen = Create(new RecordingHaptics(), out VisualElement root);
            screen.HideLoading();
            Assert.IsFalse(screen.StyledRoot.ClassListContains(ExploreScreen.RoutingClass));
            screen.ShowRoute("Boudhanath Stupa", "hud.route.finding");
            Assert.IsTrue(screen.StyledRoot.ClassListContains(ExploreScreen.RoutingClass), "Hud.uss moves .gh-toast down");
            screen.HideRoute();
            Assert.IsFalse(screen.StyledRoot.ClassListContains(ExploreScreen.RoutingClass));
        }

        [Test]
        public void PanelPillsTakeGamepadFocusButTheHudDoesNot()
        {
            ExploreScreen screen = Create(new RecordingHaptics(), out VisualElement root);
            Assert.IsFalse(root.Q<Button>("hud-menu").focusable, "A drives the scooter; it must not press HUD buttons");
            Assert.IsFalse(root.Q<Button>("hud-search").focusable);
            Assert.IsTrue(root.Q<Button>("pause-resume").focusable);
            Assert.IsTrue(root.Q<Button>("pause-settings").focusable);
            Assert.IsTrue(root.Q<Button>("pause-menu").focusable);
            Assert.IsTrue(root.Q<Button>("settings-done").focusable);
            Assert.IsTrue(root.Q<Button>("search-ride-0").focusable, "the D-pad reaches the results");
        }

        [Test]
        public void TheSearchSheetRisesAboveThePhoneKeyboard()
        {
            // A 2400 px tall phone, a 1000 px keyboard, a 1000-unit panel: the keyboard covers the bottom 416.7 units.
            Assert.AreEqual(1000f - 583.333f, SearchSheet.KeyboardLift(1000f, 1000f, 1000f, 2400f), 0.01f);
            Assert.AreEqual(0f, SearchSheet.KeyboardLift(500f, 1000f, 1000f, 2400f), "above the keyboard: no lift");
            Assert.AreEqual(0f, SearchSheet.KeyboardLift(1000f, 1000f, 0f, 2400f), "no keyboard");
            Assert.AreEqual(0f, SearchSheet.KeyboardLift(1000f, 0f, 1000f, 2400f), "not laid out yet");
        }

        [Test]
        public void ArrivalCelebratesWithASuccessHaptic()
        {
            var haptics = new RecordingHaptics();
            ExploreScreen screen = Create(haptics, out VisualElement root);
            screen.HideLoading();
            screen.CelebrateArrival("Boudhanath Stupa");
            CollectionAssert.Contains(haptics.Played, HapticKind.Success);
            Assert.IsTrue(root.Q("toast-bubble").ClassListContains(HudToast.VisibleClass));
            StringAssert.Contains("Boudhanath Stupa", root.Q<Label>("toast").text);
        }

        [Test]
        public void ReduceMotionStillWorksWithFades()
        {
            var haptics = new RecordingHaptics();
            ExploreScreen screen = Create(haptics, out VisualElement root, reduceMotion: true);
            screen.HideLoading();
            Run(screen, 0.5f);
            Assert.IsFalse(screen.IsLoading);
            screen.CelebrateArrival("Thamel");
            CollectionAssert.Contains(haptics.Played, HapticKind.Success, "haptics are not motion");
            screen.SimulateTap("hud-menu");
            Assert.IsTrue(screen.IsPaused);
        }

        [Test]
        public void TouchStateBecomesAControlFrame()
        {
            ControlFrame f = ExploreSession.ControlsFromTouch(new TouchState
            {
                MoveX = 0.5f, Throttle = 1f, Reverse = 0f, ZoomSteps = 2f, LookYawDeg = 3f, Active = true,
            });
            Assert.AreEqual(0.5f, f.MoveX);
            Assert.AreEqual(1f, f.Throttle);
            Assert.AreEqual(2f, f.ZoomSteps);
            Assert.AreEqual(3f, f.LookYawDeg);
            Assert.AreEqual(ControlDevice.Touch, f.Device);
            Assert.AreEqual(ControlDevice.None, ExploreSession.ControlsFromTouch(new TouchState()).Device);
        }
    }
}
