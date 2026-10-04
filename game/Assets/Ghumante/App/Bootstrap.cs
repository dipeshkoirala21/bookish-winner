using System;
using Ghumante.Platform;
using Ghumante.UI;
using Ghumante.UI.Localization;
using Ghumante.UI.Screens;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ghumante.App
{
    /// <summary>
    /// Entry point of <c>Assets/Ghumante/Scenes/Bootstrap.unity</c> (created by
    /// <c>Ghumante.EditorTools.ProjectSetup</c>). It applies the device tier, wires services, loads the
    /// string tables and shows the first screen. M0 starts on the Devanagari TextSpike screen so every
    /// device build doubles as the P3 shaping test (ARCHITECTURE.md section 2).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class Bootstrap : MonoBehaviour
    {
        public enum StartScreen
        {
            TextSpike = 0,
            MainMenu = 1,
        }

        [Header("UI (assigned by ProjectSetup)")]
        [SerializeField] private UIDocument document;
        [SerializeField] private VisualTreeAsset textSpikeScreen;
        [SerializeField] private VisualTreeAsset mainMenuScreen;
        [SerializeField] private StartScreen startScreen = StartScreen.TextSpike;

        [Header("Localisation")]
        [SerializeField] private TextAsset englishStrings;
        [SerializeField] private TextAsset nepaliStrings;

        private ScreenBase _current;

        /// <summary>The live Bootstrap, once its Awake has run.</summary>
        public static Bootstrap Instance { get; private set; }

        public ServiceRegistry Services { get; private set; }

        public Localizer Localizer { get; private set; }

        public DeviceTier Tier { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Services = new ServiceRegistry();
            ApplyDeviceTier(DeviceTierDetector.Detect());
            WireCoreServices();

            Localizer = CreateLocalizer();
            Services.Register(Localizer);
            Application.lowMemory += OnLowMemory;
        }

        private void Start()
        {
            // UIDocument creates its root in OnEnable; Start runs after every OnEnable in the scene.
            if (document == null)
            {
                document = GetComponent<UIDocument>();
            }
            if (document == null)
            {
                Debug.LogError("Bootstrap: no UIDocument assigned. Run Ghumante > Project Setup to rebuild the scene.");
                return;
            }
            Show(startScreen);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Application.lowMemory -= OnLowMemory;
            DisposeCurrentScreen();
            Instance = null;
        }

        /// <summary>Swaps the UIDocument to the requested screen and wires its presenter.</summary>
        public void Show(StartScreen screen)
        {
            DisposeCurrentScreen();
            VisualTreeAsset asset = screen == StartScreen.MainMenu ? mainMenuScreen : textSpikeScreen;
            if (asset == null)
            {
                Debug.LogError("Bootstrap: screen asset for " + screen + " is not assigned.");
                return;
            }

            document.visualTreeAsset = asset;
            VisualElement root = document.rootVisualElement;
            SafeArea.Track(root);

            if (screen == StartScreen.MainMenu)
            {
                var menu = new MainMenuScreen(root, Localizer, DemoCounters());
                menu.TextTestRequested += () => Show(StartScreen.TextSpike);
                menu.LanguageToggleRequested += ToggleLanguage;
                menu.ExploreRequested += () => menu.ShowToast(Localizer.Get("menu.coming_soon"));
                menu.MapRequested += () => menu.ShowToast(Localizer.Get("menu.coming_soon"));
                menu.CollectionsRequested += () => menu.ShowToast(Localizer.Get("menu.coming_soon"));
                menu.SettingsRequested += () => menu.ShowToast(Localizer.Get("menu.coming_soon"));
                menu.AddCounterRequested += _ => menu.ShowToast(Localizer.Get("menu.coming_soon"));
                _current = menu;
            }
            else
            {
                var spike = new TextSpikeScreen(root, Localizer);
                spike.BackRequested += () => Show(StartScreen.MainMenu);
                spike.LanguageToggleRequested += ToggleLanguage;
                _current = spike;
                Debug.Log("Bootstrap: TextSpike screen with " + spike.SampleCount + " Devanagari/Latin samples.");
            }
        }

        private void ToggleLanguage()
        {
            Localizer.SetLocale(Localizer.Locale == Localizer.Nepali ? Localizer.English : Localizer.Nepali);
        }

        private void DisposeCurrentScreen()
        {
            if (_current == null) return;
            _current.Dispose();
            _current = null;
        }

        private Localizer CreateLocalizer()
        {
            var localizer = new Localizer();
            try
            {
                localizer.AddTable(Localizer.English, englishStrings);
                localizer.AddTable(Localizer.Nepali, nepaliStrings);
            }
            catch (FormatException e)
            {
                Debug.LogException(e);
            }
            // TODO(M1): read the player's choice from the save's settings section first.
            localizer.SetLocale(Localizer.LocaleForLanguageCode(DeviceLocale.LanguageCode()));
            return localizer;
        }

        private void ApplyDeviceTier(DeviceTier tier)
        {
            Tier = tier;
            string levelName = DeviceTierDetector.QualityLevelName(tier);
            int level = Array.IndexOf(QualitySettings.names, levelName);
            if (level >= 0)
            {
                QualitySettings.SetQualityLevel(level, true);
            }
            else
            {
                Debug.LogWarning("Bootstrap: quality level '" + levelName + "' missing. Run Ghumante > Project Setup.");
            }
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = DeviceTierDetector.TargetFrameRate(tier);
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Debug.Log("Bootstrap: device tier " + tier + " (" + SystemInfo.deviceModel + ", " +
                      SystemInfo.systemMemorySize + " MB RAM), quality '" + levelName + "', " +
                      Application.targetFrameRate + " fps target.");
        }

        /// <summary>
        /// Registers the service implementations. All wiring lives here so that swapping a null
        /// implementation for a real one is a one-line change.
        /// </summary>
        private void WireCoreServices()
        {
            // TODO(core): register the no-op implementations from Ghumante.Core once that assembly lands
            // (ARCHITECTURE.md 7.1, 7.11, 7.12). The interfaces, all in Ghumante.Core.Services:
            //   IAnalytics        -> NullAnalytics      (collects nothing; opt-in only, ADR-009)
            //   ICrashReporter    -> NullCrashReporter
            //   ICloudSave        -> NullCloudSave      (Game Center / Play Games adapters in M1+)
            //   IQuestService     -> NullQuestService   (Story Mode hook, 7.11)
            //   IDialogueService  -> NullDialogueService
            //   IWorldStateFlags  -> NullWorldStateFlags
            //   INpcRegistry      -> NullNpcRegistry
            // e.g. Services.Register<IAnalytics>(new NullAnalytics());
            // The event bus (DiscoveryMade, ActivityCompleted, PlaceEntered) is registered here too.
        }

        private void OnLowMemory()
        {
            // Android onTrimMemory / iOS memory warning: drop whatever is not referenced right now.
            Debug.LogWarning("Bootstrap: low-memory warning from the OS; unloading unused assets.");
            Resources.UnloadUnusedAssets();
        }

        private static MenuCounters DemoCounters()
        {
            // TODO(M1): read from the save's progress section (ARCHITECTURE.md 7.10).
            return new MenuCounters { hearts = 5, stars = 12, coins = 1250, energy = 80 };
        }
    }
}
