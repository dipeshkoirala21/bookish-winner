using System;
using Ghumante.Core.Save;
using Ghumante.Core.Services;
using Ghumante.Platform;
using Ghumante.Platform.Haptics;
using Ghumante.Save;
using Ghumante.UI;
using Ghumante.UI.Localization;
using Ghumante.UI.Motion;
using Ghumante.UI.Screens;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ghumante.App
{
    /// <summary>
    /// Entry point of <c>Assets/Ghumante/Scenes/Bootstrap.unity</c> (created by
    /// <c>Ghumante.EditorTools.ProjectSetup</c>). It applies the device tier, loads the player's settings from
    /// the local save, wires services (haptics included), loads the string tables and shows the first screen:
    /// the main menu. The Devanagari TextSpike (ARCHITECTURE.md section 2, P3) is one tap away ("Text test"),
    /// or the start screen when <see cref="StartScreen.TextSpike"/> is picked in the inspector.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class Bootstrap : MonoBehaviour
    {
        /// <summary>
        /// The first screen. The value is serialized as an int: 0 is the default, so scenes saved while the
        /// TextSpike was the default (and serialized 0) now start on the main menu as well.
        /// </summary>
        public enum StartScreen
        {
            MainMenu = 0,
            TextSpike = 1,
        }

        [Header("UI (assigned by ProjectSetup)")]
        [SerializeField] private UIDocument document;
        [SerializeField] private VisualTreeAsset textSpikeScreen;
        [SerializeField] private VisualTreeAsset mainMenuScreen;
        [SerializeField] private StartScreen startScreen = StartScreen.MainMenu;

        [Header("Localisation")]
        [SerializeField] private TextAsset englishStrings;
        [SerializeField] private TextAsset nepaliStrings;

        private ScreenBase _current;
        private LocalSaveStore _store;
        private SaveData _save;
        private IHaptics _haptics;
        private MotionSettings _motion;
        private MenuCounters _demoCounters = DemoCounters();

        /// <summary>The live Bootstrap, once its Awake has run.</summary>
        public static Bootstrap Instance { get; private set; }

        public ServiceRegistry Services { get; private set; }

        public Localizer Localizer { get; private set; }

        public DeviceTier Tier { get; private set; }

        /// <summary>Haptics for every screen (also registered as <see cref="IHaptics"/>).</summary>
        public IHaptics Haptics
        {
            get { return _haptics; }
        }

        /// <summary>Shared motion settings (Reduce motion, Low tier, focus), also registered in <see cref="Services"/>.</summary>
        public MotionSettings Motion
        {
            get { return _motion; }
        }

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
            LoadSettings();
            WireFeel();

            Localizer = CreateLocalizer();
            Services.Register(Localizer);
            Application.lowMemory += OnLowMemory;
            DeviceAccessibility.ReducedMotionChanged += OnOsReducedMotionChanged;
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
            DeviceAccessibility.ReducedMotionChanged -= OnOsReducedMotionChanged;
            DisposeCurrentScreen();
            var disposable = _haptics as IDisposable;
            if (disposable != null) disposable.Dispose();
            Instance = null;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (_motion != null) _motion.AppFocused = hasFocus;
        }

        private void OnApplicationPause(bool paused)
        {
            if (_motion != null) _motion.AppFocused = !paused;
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
                var menu = new MainMenuScreen(root, Localizer, _demoCounters, _haptics, _motion);
                menu.TextTestRequested += () => Show(StartScreen.TextSpike);
                menu.LanguageToggleRequested += ToggleLanguage;
                menu.LanguageSelected += SetLanguage;
                // TODO(M1): Explore opens the world (docs/M1_PLAN.md, track D); until then a playful teaser.
                menu.ExploreRequested += menu.PlayExploreTeaser;
                menu.MapRequested += () => menu.PlayComingSoon("map");
                menu.CollectionsRequested += () => menu.PlayComingSoon("collections");
                menu.SettingsRequested += menu.OpenSettings;
                menu.AddCounterRequested += _ => _demoCounters = menu.Counters;
                menu.HapticsChanged += OnHapticsChanged;
                menu.ReduceMotionChanged += OnReduceMotionChanged;
                _current = menu;
            }
            else
            {
                var spike = new TextSpikeScreen(root, Localizer, _haptics, _motion);
                spike.BackRequested += () => Show(StartScreen.MainMenu);
                spike.LanguageToggleRequested += ToggleLanguage;
                _current = spike;
                Debug.Log("Bootstrap: TextSpike screen with " + spike.SampleCount + " Devanagari/Latin samples.");
            }
            // iOS keeps the Taptic Engine ready for a few seconds; the first tap then feels immediate.
            MobileHaptics.Prepare(_haptics);
        }

        private void ToggleLanguage()
        {
            SetLanguage(Localizer.Locale == Localizer.Nepali ? Localizer.English : Localizer.Nepali);
        }

        private void SetLanguage(string locale)
        {
            Localizer.SetLocale(locale);
            SettingsChoices.SetLanguage(_save.Settings, Localizer.Locale);
            PersistSettings();
        }

        private void OnHapticsChanged(bool on)
        {
            // MainMenuScreen already switched _haptics.Enabled; remember it.
            _save.Settings.Haptics = on;
            PersistSettings();
        }

        private void OnReduceMotionChanged(bool on)
        {
            SettingsChoices.SetReduceMotion(_save.Settings, on);
            PersistSettings();
        }

        private void OnOsReducedMotionChanged(bool prefersReduced)
        {
            // The OS setting changed while the game was in the background: follow it unless the player chose.
            if (!SettingsChoices.IsChosen(_save.Settings, SettingsChoices.ReduceMotionChosenKey))
            {
                _motion.ReduceMotion = prefersReduced;
            }
        }

        private void DisposeCurrentScreen()
        {
            if (_current == null) return;
            _current.Dispose();
            _current = null;
        }

        /// <summary>Reads the settings section of the local save (ARCHITECTURE.md 7.10); defaults on first launch.</summary>
        private void LoadSettings()
        {
            try
            {
                _store = LocalSaveStore.CreateDefault();
                _save = _store.Load();
                Debug.Log("Bootstrap: settings from " + _store.FilePath + " (" + _store.LastSource + ").");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _store = null;
                _save = new SaveData();
            }
        }

        private void PersistSettings()
        {
            if (_store != null) _store.Save(_save);
        }

        /// <summary>Haptics and motion settings from the save, the device and the tier.</summary>
        private void WireFeel()
        {
            _haptics = MobileHaptics.Create(_save.Settings.Haptics);
            Services.Register<IHaptics>(_haptics);
            bool reduce = SettingsChoices.ReduceMotion(_save.Settings, DeviceAccessibility.PrefersReducedMotion());
            _motion = new MotionSettings(reduce, Tier == DeviceTier.Low)
            {
                // Keep the editor's Game view moving while you click around the Inspector.
                PauseWhenUnfocused = !Application.isEditor,
            };
            Services.Register(_motion);
            Debug.Log("Bootstrap: haptics " + (_haptics.Enabled ? "on" : "off") +
                      (_haptics.IsSupported ? "" : " (no motor here: nothing to feel)") +
                      ", reduce motion " + (reduce ? "on" : "off") + (_motion.LowPower ? ", low-power motion" : "") + ".");
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
            string device = Localizer.LocaleForLanguageCode(DeviceLocale.LanguageCode());
            localizer.SetLocale(SettingsChoices.Language(_save.Settings, device));
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
            RegisterCoreServices(Services, Debug.LogException);
        }

        /// <summary>
        /// Registers the M0 defaults of every Ghumante.Core.Services interface (ARCHITECTURE.md 7.1, 7.11,
        /// 7.12) and the event bus. Public and static so EditMode tests can check the wiring without a scene.
        /// Haptics are registered separately (<c>WireFeel</c>) because they need the player's settings.
        /// </summary>
        /// <param name="services">Registry to fill.</param>
        /// <param name="handlerError">Receives exceptions thrown by event-bus handlers (may be null).</param>
        public static void RegisterCoreServices(ServiceRegistry services, Action<Exception> handlerError)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            var bus = new EventBus();
            if (handlerError != null)
            {
                bus.HandlerError += handlerError;
            }
            // DiscoveryMade, ActivityCompleted, PlaceEntered and friends travel on this bus.
            services.Register<IEventBus>(bus);

            services.Register<IAnalytics>(new NullAnalytics());            // opt-in only, ADR-009
            services.Register<ICrashReporter>(new NullCrashReporter());
            services.Register<ICloudSave>(new NullCloudSave());            // Game Center / Play Games in M1+
            services.Register<IQuestService>(new NullQuestService());      // Story Mode hook, 7.11
            services.Register<IDialogueService>(new NullDialogueService());
            services.Register<IWorldStateFlags>(new NullWorldStateFlags());
            services.Register<INpcRegistry>(new NullNpcRegistry());
            // TODO(M1): BuiltInSource / CdnSource from Ghumante.Platform (ARCHITECTURE.md section 9).
            services.Register<IRegionPackSource>(new NullRegionPackSource());
        }

        private void OnLowMemory()
        {
            // Android onTrimMemory / iOS memory warning: drop whatever is not referenced right now.
            Debug.LogWarning("Bootstrap: low-memory warning from the OS; unloading unused assets.");
            Resources.UnloadUnusedAssets();
        }

        private static MenuCounters DemoCounters()
        {
            // TODO(M1): read from the save's progress section (ARCHITECTURE.md 7.10). Demo values; "+" adds to
            // them for fun and they reset on restart. Hearts start below the cap so "+" has room to fill them.
            return new MenuCounters { hearts = 3, stars = 12, coins = 1250, energy = 80 };
        }
    }
}
