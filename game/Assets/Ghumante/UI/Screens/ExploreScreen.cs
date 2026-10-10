using System;
using Ghumante.Core.Characters;
using Ghumante.Core.Data;
using Ghumante.Core.Motion;
using Ghumante.Core.Search;
using Ghumante.Core.Services;
using Ghumante.UI.Hud;
using Ghumante.UI.Localization;
using Ghumante.UI.Motion;
using UnityEngine;
using UnityEngine.UIElements;
#if GHUMANTE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ghumante.UI.Screens
{
    /// <summary>
    /// Presenter of <c>Screens/Explore.uxml</c>: the in-world HUD (M1 track D; ARCHITECTURE.md 7.9, 7.10a), in the reference
    /// style and in both orientations (UI/Hud/Hud.uss), inside the safe area.
    /// <list type="bullet">
    /// <item><b>Loading</b>: a progress overlay while the region opens and the streets around the spawn stream in; or a
    /// friendly explanation with "Back to menu" when no region is installed or opening failed.</item>
    /// <item><b>HUD</b>: speed (km/h, Devanagari digits in Nepali), the surface under the wheels with a subtle dot when
    /// the road's surface is inferred, the place you are in (EN/NE), a compass, the camera-angle button, the compact
    /// route chip (the next turn's arrow, how far to it, distance and time left; placed by <see cref="RouteChipLayout"/>
    /// beside the speedometer or under the top bar, never over the road ahead), Search, Walk/Ride and Menu buttons, the
    /// OpenStreetMap credit, a debug time-of-day slider in development builds.</item>
    /// <item><b>Touch controls</b> (<see cref="TouchControls"/>), hidden while a keyboard or gamepad drives.</item>
    /// <item><b>Panels</b>: search (<see cref="SearchSheet"/>), pause (Resume, Settings, Main menu) and the shared
    /// <see cref="SettingsSheet"/>. Escape and Android back close the top panel, or pause.</item>
    /// <item><b>Feel</b>: every button uses the shared press feel and haptics; arrival plays confetti and a Success
    /// haptic; Reduce motion turns pops and slides into fades.</item>
    /// </list>
    /// App decides what happens through the events and feeds the HUD each frame; nothing here allocates per frame
    /// (text is written only when what it shows changes).
    /// </summary>
    public sealed class ExploreScreen : ScreenBase
    {
        public const string RideClass = "gh-hud--ride";
        public const string WalkClass = "gh-hud--walk";
        public const string PassengerClass = "gh-hud--passenger";
        public const string TwoWheelerClass = "gh-hud--two";
        public const string HeavyClass = "gh-hud--heavy";
        public const string PassengerOnClass = "gh-hud__passenger--on";
        public const string PromptOnClass = "gh-hud__prompt--on";
        public const string ActionHopClass = "gh-action-btn--hop";
        public const string ActionOffClass = "gh-action-btn--off";
        public const string ActionIconJump = "gh-action-btn__icon--jump";
        public const string ActionIconScooter = "gh-action-btn__icon--scooter";
        public const string ActionIconNone = "gh-action-btn__icon--none";
        public const string RouteVisibleClass = "gh-hud__route--visible";

        /// <summary>On the root while the route chip shows.</summary>
        public const string RoutingClass = "gh-hud--routing";
        public const string InferredOnClass = "gh-hud__inferred--on";
        public const string OffRoadClass = "gh-hud__surface--offroad";
        public const string DebugOnClass = "gh-hud__debug--on";
        public const string LoadingFailedClass = "gh-loading--failed";
        public const string LoadingHiddenClass = "gh-loading--hidden";
        public const string PauseOpenClass = "gh-sheet-layer--open";

        private static readonly string[] ConfettiClasses =
        {
            "gh-particle--confetti-red", "gh-particle--confetti-yellow", "gh-particle--confetti-cyan",
            "gh-particle--confetti-green", "gh-particle--star", "gh-particle--sparkle",
        };

        private readonly TouchControls _touch;
        private readonly SearchSheet _search;
        private readonly SettingsSheet _settings;
        private readonly HudToast _toast;
        private readonly ParticleBurst _particles;

        private readonly VisualElement _top;
        private readonly MotionNode _topNode;
        private readonly Label _placeName;
        private readonly VisualElement _place;
        private readonly MotionNode _placeNode;
        private readonly VisualElement _compassRose;
        private readonly MotionNode _modeIconNode;
        private readonly VisualElement _speedo;
        private readonly MotionNode _speedoNode;
        private readonly Label _speed;
        private readonly VisualElement _surface;
        private readonly MotionNode _surfaceNode;
        private readonly Label _surfaceLabel;
        private readonly VisualElement _inferred;
        private readonly VisualElement _route;
        private readonly MotionNode _routeNode;
        private readonly VisualElement _routePointer;
        private readonly Label _routeStep;
        private readonly Label _routeInfo;
        private readonly VisualElement _actionsRow;
        private readonly VisualElement _attribution;
        private readonly VisualElement[] _chipObstacles;
        private readonly HudRect[] _chipRects;
        private readonly VisualElement _debug;
        private readonly Label _debugClock;
        private readonly VisualElement _controls;
        private readonly MotionNode _controlsNode;
        private readonly Button _searchButton;
        private readonly Button _modeButton;
        private readonly Button _menuButton;
        private readonly Button _garageButton;
        private readonly Button _cameraButton;
        private readonly Button _action;
        private readonly Label _actionLabel;
        private readonly VisualElement _actionIcon;
        private readonly MotionNode _actionNode;
        private readonly Button _passenger;
        private readonly Label _hornLabel;
        private readonly VisualElement _prompt;
        private readonly Label _promptLabel;
        private string _actionKey;
        private string _actionStyle;
        private string _actionIconClass;
        private string _hornKey;
        private string _promptKey;
        private string _promptArgKey;
        private ControlLayout _layout = ControlLayout.TwoWheeler;

        private readonly VisualElement _pauseLayer;
        private readonly Button _pauseResume;
        private readonly Button _pauseSettings;
        private readonly VisualElement _searchLayer;
        private readonly VisualElement _settingsLayer;
        private readonly Button _settingsFirst;
        private readonly MotionNode _pausePanelNode;
        private readonly MotionNode _pauseScrimNode;

        private readonly VisualElement _loading;
        private readonly MotionNode _loadingNode;
        private readonly MotionNode _loadingPanelNode;
        private readonly MotionNode _loadingScooterNode;
        private readonly Label _loadingTitle;
        private readonly Label _loadingStatus;
        private readonly Label _loadingPercent;
        private readonly Label _loadingHelp;
        private readonly MotionNode _loadingFillNode;

        private VisualElement _backTarget;
        private HudSlider _timeSlider;

        // What the HUD shows (to write text only on change).
        private bool _riding = true;
        private int _shownKmh = -1;
        private bool _shownDevanagari;
        private string _surfaceKey;
        private string _surfaceClass;
        private bool _surfaceOffRoad;
        private bool _surfaceInferred;
        private NameRecord _placeRecord;
        private float _compassDeg = float.NaN;
        private float _arrowDeg = float.NaN;
        private string _routeDestination;
        private NameRecord _routeRecord;
        private long _routeBucket = -1;
        private double _routeRemainingM;
        private int _routeMinutes = -1;
        private string _routeStatusKey;
        private bool _routeStatusOnly;
        private RouteStepKind _stepKind = RouteStepKind.None;
        private long _stepBucket = -1;
        private double _stepM;
        private bool _placingChip;
        private RouteChipSlot _chipSlot = RouteChipSlot.Fallback;
        private float _loadingProgress = -1f;
        private int _loadingPercentShown = -1;
        private string _loadingStatusKey;
        private string _loadingStatusArg;
        private string _failureTitleKey;
        private string _failureBodyKey;
        private string _failureArg;
        private float _clockHours = float.NaN;
        private bool _scooterBobbing;

        public ExploreScreen(VisualElement root, Localizer localizer, IHaptics haptics, MotionSettings motion)
            : base(root, localizer, haptics, motion)
        {
            _top = Required<VisualElement>("hud-top");
            _topNode = Animator.Node(_top);
            _place = Required<VisualElement>("hud-place");
            _placeNode = Animator.Node(_place);
            _placeName = Required<Label>("hud-place-name");
            _compassRose = Required<VisualElement>("hud-compass-rose");
            _modeIconNode = Animator.Node(Required<VisualElement>("hud-mode-icon"));
            _speedo = Required<VisualElement>("hud-speedo");
            _speedoNode = Animator.Node(_speedo);
            _speed = Required<Label>("hud-speed");
            _surface = Required<VisualElement>("hud-surface");
            _surfaceNode = Animator.Node(_surface);
            _surfaceLabel = Required<Label>("hud-surface-label");
            _inferred = Required<VisualElement>("hud-surface-inferred");
            _route = Required<VisualElement>("hud-route");
            _routeNode = Animator.Node(_route);
            _routePointer = Required<VisualElement>("hud-route-pointer");
            _routeStep = Required<Label>("hud-route-step");
            _routeInfo = Required<Label>("hud-route-info");
            _debug = Required<VisualElement>("hud-debug");
            _debugClock = Required<Label>("hud-debug-clock");
            _controls = Required<VisualElement>("hud-controls");
            _controlsNode = Animator.Node(_controls);
            _attribution = Required<Label>("hud-attribution");

            _action = Required<Button>("hud-action");
            _actionLabel = Required<Label>("hud-action-label");
            _actionIcon = Required<VisualElement>("hud-action-icon");
            _actionNode = Animator.Node(_action);
            _passenger = Required<Button>("hud-passenger");
            _hornLabel = Required<Label>("hud-horn-label");
            _prompt = Required<VisualElement>("hud-prompt");
            _promptLabel = Required<Label>("hud-prompt-label");
            _touch = new TouchControls(new TouchControlsView
            {
                Controls = _controls,
                Pad = Required<VisualElement>("hud-pad"),
                StickZone = Required<VisualElement>("hud-stick-zone"),
                Stick = Required<VisualElement>("hud-stick"),
                StickKnob = Required<VisualElement>("hud-stick-knob"),
                DriveZone = Required<VisualElement>("hud-drive-zone"),
                DriveMarker = Required<VisualElement>("hud-drive-marker"),
                Throttle = Required<Button>("hud-throttle"),
                Brake = Required<Button>("hud-brake"),
                Action = _action,
                Passenger = _passenger,
                Emote = Required<Button>("hud-emote"),
                Horn = Required<Button>("hud-horn"),
                Bell = Required<Button>("hud-bell"),
            }, Animator, Haptics, Feel);

            // What the route chip must keep clear of (besides the speedometer and the top bar): every touch control, the
            // prompt chip and the debug panel, as laid out.
            _chipObstacles = new[]
            {
                Required<VisualElement>("hud-stick"), Required<VisualElement>("hud-pedals"), _action, _passenger, Required<VisualElement>("hud-emote"),
                Required<VisualElement>("hud-horn"), Required<VisualElement>("hud-bell"), _prompt, _debug, _attribution,
            };
            _chipRects = new HudRect[_chipObstacles.Length];

            _toast = new HudToast(Animator, Required<VisualElement>("toast-bubble"), Required<VisualElement>("toast-icon"),
                                  Required<Label>("toast"));
            _particles = new ParticleBurst(Animator, Required<VisualElement>("fx-layer"), Motion.LowPower ? 16 : 36, 5309u);

            _actionsRow = Required<VisualElement>("hud-actions");
            _cameraButton = Required<Button>("hud-camera");
            Feel(_cameraButton, HapticKind.Selection, () => Raise(CameraRequested));
            _searchButton = Required<Button>("hud-search");
            _modeButton = Required<Button>("hud-mode");
            _menuButton = Required<Button>("hud-menu");
            _garageButton = Required<Button>("hud-garage");
            Feel(_garageButton, HapticKind.LightImpact, () => Raise(GarageRequested));
            Feel(_searchButton, HapticKind.LightImpact, () => OpenSearch());
            Feel(_modeButton, HapticKind.MediumImpact, () => Raise(ModeToggleRequested));
            Feel(_menuButton, HapticKind.LightImpact, Pause);
            Feel(Required<Button>("hud-route-cancel"), HapticKind.LightImpact, () => Raise(RouteCancelRequested));

            _search = new SearchSheet(new SearchSheetView
            {
                Layer = Required<VisualElement>("search-layer"),
                Scrim = Required<Button>("search-scrim"),
                Sheet = Required<VisualElement>("search-sheet"),
                Close = Required<Button>("search-close"),
                FieldHost = Required<VisualElement>("search-field-host"),
                Placeholder = Required<Label>("search-placeholder"),
                Clear = Required<Button>("search-clear"),
                Hint = Required<Label>("search-hint"),
                Results = Required<ScrollView>("search-results"),
            }, Animator, Localizer, Haptics, Feel, StyledRoot);
            _search.RideRequested += entry => Raise(RideToRequested, entry);
            _search.TeleportRequested += entry => Raise(TeleportRequested, entry);
            Required<VisualElement>("search-content");
            Required<VisualElement>("search-handle");

            _searchLayer = Required<VisualElement>("search-layer");
            _pauseLayer = Required<VisualElement>("pause-layer");
            _pausePanelNode = Animator.Node(Required<VisualElement>("pause-panel"));
            Button pauseScrim = Required<Button>("pause-scrim");
            _pauseScrimNode = Animator.Node(pauseScrim);
            Feel(pauseScrim, HapticKind.Selection, Resume).Bouncy = false;
            _pauseResume = Required<Button>("pause-resume");
            _pauseSettings = Required<Button>("pause-settings");
            Feel(_pauseResume, HapticKind.LightImpact, Resume);
            Feel(_pauseSettings, HapticKind.LightImpact, OpenSettings);
            Feel(Required<Button>("pause-menu"), HapticKind.MediumImpact, () => Raise(MenuRequested));

            _settingsLayer = Required<VisualElement>("settings-layer");
            _settingsFirst = Required<Button>("settings-haptics-toggle");
            _settings = new SettingsSheet(new SettingsSheetView
            {
                Layer = _settingsLayer,
                Scrim = Required<Button>("settings-scrim"),
                Sheet = Required<VisualElement>("settings-sheet"),
                Content = Required<VisualElement>("settings-content"),
                Close = Required<Button>("settings-close"),
                Done = Required<Button>("settings-done"),
                HapticsToggle = Required<Button>("settings-haptics-toggle"),
                HapticsKnob = Required<VisualElement>("settings-haptics-knob"),
                HapticsNote = Required<Label>("settings-haptics-note"),
                MotionToggle = Required<Button>("settings-motion-toggle"),
                MotionKnob = Required<VisualElement>("settings-motion-knob"),
                LanguageEnglish = Required<Button>("settings-lang-en"),
                LanguageNepali = Required<Button>("settings-lang-ne"),
                FeelPlay = Required<Button>("settings-feel-play"),
                FeelStatus = Required<Label>("settings-feel-status"),
                Handle = Required<VisualElement>("settings-handle"),
                Rows = new[]
                {
                    Required<VisualElement>("settings-row-haptics"), Required<VisualElement>("settings-row-motion"),
                    Required<VisualElement>("settings-row-language"), Required<VisualElement>("settings-row-feel"),
                },
            }, Animator, Localizer, Haptics, Motion, Feel, StyledRoot, Root);
            _settings.HapticsChanged += on => Raise(HapticsChanged, on);
            _settings.ReduceMotionChanged += on => Raise(ReduceMotionChanged, on);
            _settings.LanguageRequested += locale => Raise(LanguageSelected, locale);

            _loading = Required<VisualElement>("loading-layer");
            _loadingNode = Animator.Node(_loading);
            _loadingPanelNode = Animator.Node(Required<VisualElement>("loading-panel"));
            _loadingScooterNode = Animator.Node(Required<VisualElement>("loading-scooter"));
            _loadingTitle = Required<Label>("loading-title");
            _loadingStatus = Required<Label>("loading-status");
            _loadingPercent = Required<Label>("loading-percent");
            _loadingHelp = Required<Label>("loading-help");
            Required<VisualElement>("loading-bar");
            _loadingFillNode = Animator.Node(Required<VisualElement>("loading-bar-fill"));
            _loadingFillNode.Set(MotionChannel.ScaleX, 0.001f); // the fill is full width, scaled from its left edge
            Feel(Required<Button>("loading-back"), HapticKind.LightImpact, () => Raise(MenuRequested));

            // HUD buttons never take keyboard focus: gamepad A and Space drive the scooter, they must not also "submit"
            // a button clicked earlier with the mouse. The pause panel and the Settings sheet keep theirs, so a gamepad
            // can move between them (D-pad) and press them (A); focus is dropped as soon as their panel closes
            // (DropStaleFocus). Search result pills are focusable the same way (SearchSheet).
            Root.Query<Button>().ForEach(b => b.focusable = false);
            Required<VisualElement>("pause-panel").Query<Button>().ForEach(b => b.focusable = true);
            Required<VisualElement>("settings-sheet").Query<Button>().ForEach(b => b.focusable = true);
            Animator.OnUpdate(DropStaleFocus);

            Root.RegisterCallback<AttachToPanelEvent>(OnAttach);
            // The route chip is placed whenever the HUD's geometry changes (rotation, safe area, the chip's own size).
            StyledRoot.RegisterCallback<GeometryChangedEvent>(OnHudGeometry);
            _route.RegisterCallback<GeometryChangedEvent>(OnHudGeometry);
            _speedo.RegisterCallback<GeometryChangedEvent>(OnHudGeometry);
            _top.RegisterCallback<GeometryChangedEvent>(OnHudGeometry);
            if (Root.panel != null) ListenForBack();
            Animator.OnIdle(UpdateIdle, () =>
            {
                _scooterBobbing = false;
                _loadingScooterNode.ClearOffsets();
            });

            SetMode(true);
            IsLoading = true;
            ShowLoading("explore.loading.finding", null);
            Refresh();
            Animator.Commit();
        }

        // ----- Events (App decides what they do) ----------------------------------------------------------------

        /// <summary>"Main menu" in the pause panel, or "Back to menu" when loading failed.</summary>
        public event Action MenuRequested;

        /// <summary>The Walk/Ride button (the same as the Action button: hop on or off).</summary>
        public event Action ModeToggleRequested;

        /// <summary>The garage button: whistle for the selected garage vehicle.</summary>
        public event Action GarageRequested;

        /// <summary>The × of the route chip.</summary>
        public event Action RouteCancelRequested;

        /// <summary>The camera button: the next camera angle of the current vehicle class.</summary>
        public event Action CameraRequested;

        /// <summary>"Ride there" on a search result.</summary>
        public event Action<SearchEntry> RideToRequested;

        /// <summary>"Teleport" on a search result (development builds and the editor).</summary>
        public event Action<SearchEntry> TeleportRequested;

        /// <summary>The debug time-of-day slider moved (hours).</summary>
        public event Action<float> TimeOfDayChanged;

        /// <summary>The game paused (true) or resumed (false).</summary>
        public event Action<bool> PauseChanged;

        /// <summary>Settings: Vibration switched (already applied to IHaptics.Enabled). App persists it.</summary>
        public event Action<bool> HapticsChanged;

        /// <summary>Settings: Reduce motion switched (already applied to MotionSettings). App persists it.</summary>
        public event Action<bool> ReduceMotionChanged;

        /// <summary>Settings: a language was picked ("en" / "ne"); App applies and persists it.</summary>
        public event Action<string> LanguageSelected;

        // ----- State ----------------------------------------------------------------------------------------------

        public TouchControls Touch
        {
            get { return _touch; }
        }

        public SearchSheet Search
        {
            get { return _search; }
        }

        public bool IsLoading { get; private set; }

        public bool IsPaused { get; private set; }

        public bool SearchOpen
        {
            get { return _search.IsOpen; }
        }

        public bool SettingsOpen
        {
            get { return _settings.IsOpen; }
        }

        /// <summary>True while something covers the world: loading, pause, search or settings. Gameplay stops.</summary>
        public bool BlocksGameplay
        {
            get { return IsLoading || IsPaused || _search.IsOpen || _settings.IsOpen; }
        }

        /// <summary>True when the HUD shows the riding layout.</summary>
        public bool Riding
        {
            get { return _riding; }
        }

        // ----- Loading ------------------------------------------------------------------------------------------

        /// <summary>Shows the loading overlay with a status line (a string key, optionally with one argument).</summary>
        public void ShowLoading(string statusKey, string argument)
        {
            IsLoading = true;
            _failureTitleKey = null;
            _loading.RemoveFromClassList(LoadingHiddenClass);
            _loading.RemoveFromClassList(LoadingFailedClass);
            _loadingNode.Set(MotionChannel.Opacity, 1f);
            _loadingStatusKey = statusKey;
            _loadingStatusArg = argument;
            ApplyLoadingText();
            _touch.ReleaseAll();
        }

        /// <summary>Progress of the loading bar in [0, 1] (the bar only moves forward).</summary>
        public void SetLoadingProgress(float progress)
        {
            if (float.IsNaN(progress)) return;
            progress = Mathf.Clamp01(progress);
            if (progress <= _loadingProgress) return;
            _loadingProgress = progress;
            _loadingFillNode.Set(MotionChannel.ScaleX, Mathf.Max(0.001f, progress));
            int percent = Mathf.FloorToInt(progress * 100f);
            if (percent == _loadingPercentShown) return;
            _loadingPercentShown = percent;
            _loadingPercent.text = HudFormat.Number(percent, Localizer.UsesDevanagariDigits) + "%";
        }

        /// <summary>No region pack is installed: explains Project Setup / Import Region Pack, with "Back to menu".</summary>
        public void ShowNoRegion()
        {
            ShowFailure("explore.no_region.title", "explore.no_region.body", null);
        }

        /// <summary>Opening the region failed (<paramref name="detail"/> says why), with "Back to menu".</summary>
        public void ShowLoadFailed(string detail)
        {
            ShowFailure("explore.load_failed.title", "explore.load_failed.body", detail ?? "?");
        }

        /// <summary>The world is ready: the overlay fades away and the HUD comes in.</summary>
        public void HideLoading()
        {
            if (!IsLoading) return;
            IsLoading = false;
            float fade = Animator.Reduced ? 0.25f : 0.45f;
            Animator.Play(_loadingNode, MotionChannel.Opacity, new Tween(1f, 0f, fade, Ease.OutCubic));
            if (!Animator.Reduced)
            {
                Animator.Play(_loadingPanelNode, MotionChannel.ScaleX, new Tween(1f, 1.08f, fade, Ease.OutCubic));
                Animator.Play(_loadingPanelNode, MotionChannel.ScaleY, new Tween(1f, 1.08f, fade, Ease.OutCubic));
            }
            Animator.After(fade, () =>
            {
                if (!IsLoading) _loading.AddToClassList(LoadingHiddenClass);
            });
            PlayEntrance();
        }

        // ----- HUD ------------------------------------------------------------------------------------------------

        /// <summary>Riding or walking layout: pedals or stick, the mode button's icon, the rig's speed readout.</summary>
        public void SetMode(bool riding)
        {
            bool changed = riding != _riding;
            _riding = riding;
            _layout = riding ? ControlLayout.TwoWheeler : ControlLayout.Walk;
            StyledRoot.EnableInClassList(RideClass, riding);
            StyledRoot.EnableInClassList(WalkClass, !riding);
            StyledRoot.EnableInClassList(PassengerClass, false);
            StyledRoot.EnableInClassList(TwoWheelerClass, riding);
            StyledRoot.EnableInClassList(HeavyClass, false);
            _touch.Riding = riding;
            _modeButton.tooltip = Localizer.Get(riding ? "hud.walk" : "hud.ride");
            if (changed)
            {
                _touch.ReleaseAll();
                if (!Animator.Reduced) _modeIconNode.KickHop(320f);
            }
        }

        /// <summary>
        /// The touch layout family (W2_DESIGN 6.5): on foot (stick, Action, Namaste), a two-wheeler, a car or a bus/truck
        /// (steering, pedals, Hop off, Horn) or riding along (Hop off, Stop bell). Sets the <c>gh-hud--*</c> classes.
        /// </summary>
        public void SetLayout(ControlLayout layout)
        {
            if (layout == _layout) return;
            _layout = layout;
            bool ride = layout == ControlLayout.TwoWheeler || layout == ControlLayout.Car || layout == ControlLayout.Heavy;
            bool walk = layout == ControlLayout.Walk;
            StyledRoot.EnableInClassList(RideClass, ride);
            StyledRoot.EnableInClassList(WalkClass, walk);
            StyledRoot.EnableInClassList(PassengerClass, layout == ControlLayout.Passenger);
            StyledRoot.EnableInClassList(TwoWheelerClass, layout == ControlLayout.TwoWheeler);
            StyledRoot.EnableInClassList(HeavyClass, layout == ControlLayout.Heavy);
            _riding = ride;
            _touch.Riding = ride;
            _modeButton.tooltip = Localizer.Get(ride ? "hud.walk" : "hud.ride");
            _touch.ReleaseAll();
            if (!Animator.Reduced) _modeIconNode.KickHop(320f);
        }

        public ControlLayout Layout
        {
            get { return _layout; }
        }

        /// <summary>
        /// The Action button: its label (a string key: hud.action.jump, hop_on, hop_off, stop), its colour style
        /// (null yellow, "hop" green, "off" cyan) and its icon (jump arrow, scooter, none). Pops when it changes meaning
        /// (with a selection tick when Hop on appears, W2_DESIGN 6.5).
        /// </summary>
        public void SetAction(string labelKey, string style, bool scooterIcon)
        {
            string iconClass = labelKey == "hud.action.jump" ? ActionIconJump : scooterIcon ? ActionIconScooter : ActionIconNone;
            if (labelKey == _actionKey && style == _actionStyle && iconClass == _actionIconClass) return;
            bool hopOnAppears = labelKey == "hud.action.hop_on" && _actionKey != labelKey;
            _actionKey = labelKey;
            _actionStyle = style;
            _action.EnableInClassList(ActionHopClass, style == "hop");
            _action.EnableInClassList(ActionOffClass, style == "off");
            if (_actionIconClass != null) _actionIcon.RemoveFromClassList(_actionIconClass);
            _actionIconClass = iconClass;
            _actionIcon.AddToClassList(iconClass);
            ApplyActionText();
            if (hopOnAppears) Haptics.Play(HapticKind.Selection);
            if (!Animator.Reduced)
            {
                Animator.Play(_actionNode, MotionChannel.ScaleX, new Tween(0.86f, 1f, 0.12f, Ease.OutBack));
                Animator.Play(_actionNode, MotionChannel.ScaleY, new Tween(0.86f, 1f, 0.12f, Ease.OutBack));
            }
        }

        /// <summary>The horn button's label: "Horn", or "Bell" on a bicycle.</summary>
        public void SetHornLabel(string key)
        {
            if (key == _hornKey) return;
            _hornKey = key;
            _hornLabel.text = Localizer.Get(key);
        }

        /// <summary>Shows the small "Ride as passenger" button (a passenger seat is offered).</summary>
        public void SetPassengerOffered(bool offered)
        {
            if (_passenger.ClassListContains(PassengerOnClass) == offered) return;
            _passenger.EnableInClassList(PassengerOnClass, offered);
        }

        /// <summary>The prompt chip: a string key (null hides it), optionally formatted with another key's text.</summary>
        public void SetPrompt(string key, string argumentKey = null)
        {
            if (key == _promptKey && argumentKey == _promptArgKey) return;
            _promptKey = key;
            _promptArgKey = argumentKey;
            _prompt.EnableInClassList(PromptOnClass, key != null);
            ApplyPromptText();
        }

        private void ApplyActionText()
        {
            if (_actionKey == null) return;
            string text = Localizer.Get(_actionKey);
            _actionLabel.text = text;
            _action.tooltip = text;
        }

        private void ApplyPromptText()
        {
            if (_promptKey == null)
            {
                _promptLabel.text = "";
                return;
            }
            _promptLabel.text = _promptArgKey != null ? Localizer.Format(_promptKey, Localizer.Get(_promptArgKey)) : Localizer.Get(_promptKey);
        }

        /// <summary>Shows or hides the touch controls (keyboard or gamepad took over, or a finger came back).</summary>
        public void SetTouchControlsVisible(bool visible)
        {
            if (_touch.Visible == visible) return;
            _touch.Visible = visible;
            if (visible && !Animator.Reduced) Animator.Play(_controlsNode, MotionChannel.Opacity, new Tween(0f, 1f, 0.3f, Ease.OutCubic));
            else _controlsNode.Set(MotionChannel.Opacity, 1f);
        }

        /// <summary>The speedometer, from metres per second (written only when the whole km/h changes).</summary>
        public void SetSpeed(float metresPerSecond)
        {
            int kmh = HudFormat.SpeedKmh(metresPerSecond);
            bool devanagari = Localizer.UsesDevanagariDigits;
            if (kmh == _shownKmh && devanagari == _shownDevanagari) return;
            _shownKmh = kmh;
            _shownDevanagari = devanagari;
            _speed.text = HudFormat.Number(kmh, devanagari);
        }

        /// <summary>
        /// The surface chip: on a road its own surface (asphalt, brick, gravel...), off road the ground's group, coloured by
        /// the physics group; <paramref name="inferred"/> adds the subtle dot (the road's surface was guessed).
        /// </summary>
        public void SetSurface(SurfaceGroup group, bool onRoad, Surface roadSurface, bool inferred)
        {
            string key = HudFormat.SurfaceKey(onRoad, roadSurface, group);
            string cls = HudFormat.SurfaceClass(group);
            bool offRoad = !onRoad;
            bool show = onRoad && inferred;
            if (key == _surfaceKey && cls == _surfaceClass && offRoad == _surfaceOffRoad && show == _surfaceInferred) return;
            bool first = _surfaceKey == null;
            if (_surfaceClass != cls)
            {
                if (_surfaceClass != null) _surface.RemoveFromClassList(_surfaceClass);
                else _surface.RemoveFromClassList("gh-hud__surface--paved");
                _surface.AddToClassList(cls);
                _surfaceClass = cls;
            }
            _surfaceKey = key;
            _surfaceOffRoad = offRoad;
            _surfaceInferred = show;
            _surface.EnableInClassList(OffRoadClass, offRoad);
            _inferred.EnableInClassList(InferredOnClass, show);
            ApplySurfaceText();
            if (!first && !Animator.Reduced)
            {
                Animator.Play(_surfaceNode, MotionChannel.ScaleX, new Tween(1.18f, 1f, 0.35f, Ease.OutBack));
                Animator.Play(_surfaceNode, MotionChannel.ScaleY, new Tween(1.18f, 1f, 0.35f, Ease.OutBack));
            }
        }

        /// <summary>The place you are in (null hides the pill). Shown in Nepali when the locale is Nepali and the name
        /// has a Nepali form (never machine-transliterated, ARCHITECTURE.md 6.2).</summary>
        public void SetPlace(NameRecord name)
        {
            if (ReferenceEquals(name, _placeRecord) || name != null && name.Equals(_placeRecord)) return;
            _placeRecord = name;
            bool visible = name != null && !name.IsEmpty;
            DisplayStyle d = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_place.style.display != d) _place.style.display = d;
            if (!visible) return;
            _placeName.text = name.Display(Localizer.Locale == Localizer.Nepali);
            if (!Animator.Reduced && !IsLoading)
            {
                Animator.Play(_placeNode, MotionChannel.ScaleX, new Tween(0.85f, 1f, 0.4f, Ease.OutBack));
                Animator.Play(_placeNode, MotionChannel.ScaleY, new Tween(0.85f, 1f, 0.4f, Ease.OutBack));
                Animator.Play(_placeNode, MotionChannel.Opacity, new Tween(0f, 1f, 0.2f, Ease.Linear));
            }
        }

        /// <summary>The compass: <paramref name="cameraYawRad"/> is the camera's heading (0 = north, clockwise).</summary>
        public void SetHeading(float cameraYawRad)
        {
            float deg = -cameraYawRad * Mathf.Rad2Deg;
            if (!float.IsNaN(_compassDeg) && Mathf.Abs(Mathf.DeltaAngle(deg, _compassDeg)) < 0.5f) return;
            _compassDeg = deg;
            _compassRose.style.rotate = new Rotate(new Angle(deg, AngleUnit.Degree));
        }

        /// <summary>Shows the route chip for <paramref name="destination"/> with a status line (e.g. finding the way).
        /// The name follows the language: a switch mid-route re-renders it.</summary>
        public void ShowRoute(NameRecord destination, string statusKey)
        {
            ShowRoute(destination != null ? destination.Display(Localizer.Locale == Localizer.Nepali) : "", statusKey);
            _routeRecord = destination;
        }

        /// <summary>Shows the route chip for a fixed <paramref name="destination"/> text with a status line: the big line
        /// names the destination ("To Boudhanath Stupa"), the small one says what is happening.</summary>
        public void ShowRoute(string destination, string statusKey)
        {
            _routeRecord = null;
            _routeDestination = destination ?? "";
            StyledRoot.AddToClassList(RoutingClass);
            _routeStatusKey = statusKey;
            _routeStatusOnly = statusKey != null;
            _routeBucket = -1;
            _routeMinutes = -1;
            _stepKind = RouteStepKind.None;
            _stepBucket = -1;
            bool wasVisible = _route.ClassListContains(RouteVisibleClass);
            _route.AddToClassList(RouteVisibleClass);
            ApplyRouteText();
            if (!wasVisible && !Animator.Reduced)
            {
                // In from the edge it sits on: up from the bottom edge, down from under the top bar.
                bool bottom = _chipSlot == RouteChipSlot.BesideSpeedoLeft || _chipSlot == RouteChipSlot.BesideSpeedoRight;
                Animator.Play(_routeNode, MotionChannel.TranslateY, new Tween(bottom ? 50f : -50f, 0f, 0.4f, Ease.OutBack));
                Animator.Play(_routeNode, MotionChannel.Opacity, new Tween(0f, 1f, 0.2f, Ease.Linear));
            }
            PlaceRouteChip();
        }

        /// <summary>Route progress: remaining metres, ETA seconds and the arrow's direction relative to the camera
        /// (radians, 0 = straight up the screen, clockwise): the next turn, or the way back to the route. The small line
        /// changes only when the rounded values change.</summary>
        public void SetRouteProgress(double remainingM, double etaSeconds, float arrowRad)
        {
            long bucket = HudFormat.DistanceBucket(remainingM);
            int minutes = HudFormat.EtaMinutes(etaSeconds);
            if (_routeStatusOnly || bucket != _routeBucket || minutes != _routeMinutes)
            {
                _routeStatusOnly = false;
                _routeStatusKey = null;
                _routeBucket = bucket;
                _routeRemainingM = remainingM;
                _routeMinutes = minutes;
                ApplyRouteText();
            }
            float deg = arrowRad * Mathf.Rad2Deg;
            if (float.IsNaN(_arrowDeg) || Mathf.Abs(Mathf.DeltaAngle(deg, _arrowDeg)) >= 1f)
            {
                _arrowDeg = deg;
                _routePointer.style.rotate = new Rotate(new Angle(deg, AngleUnit.Degree));
            }
        }

        /// <summary>
        /// The chip's big line: the distance to the next turn (<see cref="RouteStepKind.Turn"/>), "Straight on" when none
        /// is near, or "Back to the route" when off it. Text changes only when the rounded distance or the kind changes.
        /// </summary>
        public void SetRouteStep(RouteStepKind kind, double distanceM)
        {
            if (_routeStatusOnly) return;
            long bucket = kind == RouteStepKind.Turn ? HudFormat.DistanceBucket(distanceM) : -1;
            if (kind == _stepKind && bucket == _stepBucket) return;
            _stepKind = kind;
            _stepBucket = bucket;
            _stepM = distanceM;
            ApplyRouteText();
        }

        /// <summary>What the big line shows (<see cref="SetRouteStep"/>).</summary>
        public RouteStepKind RouteStep
        {
            get { return _stepKind; }
        }

        public void HideRoute()
        {
            _routeDestination = null;
            _routeRecord = null;
            _stepKind = RouteStepKind.None;
            _route.RemoveFromClassList(RouteVisibleClass);
            StyledRoot.RemoveFromClassList(RoutingClass);
        }

        /// <summary>The slot <see cref="RouteChipLayout"/> chose for the chip (Fallback until the HUD is laid out).</summary>
        public RouteChipSlot RouteChipSlot
        {
            get { return _chipSlot; }
        }

        public bool RouteVisible
        {
            get { return _route.ClassListContains(RouteVisibleClass); }
        }

        /// <summary>Arrived at <paramref name="destination"/>, named in the current language.</summary>
        public void CelebrateArrival(NameRecord destination)
        {
            CelebrateArrival(destination != null ? destination.Display(Localizer.Locale == Localizer.Nepali) : "");
        }

        /// <summary>Arrived: confetti, a star toast and a Success haptic.</summary>
        public void CelebrateArrival(string destination)
        {
            Haptics.Play(HapticKind.Success);
            _toast.Show(Localizer.Format("hud.arrived", destination ?? ""), "gh-toast__icon--star", 4f);
            if (Animator.Reduced) return;
            Rect r = StyledRoot.worldBound;
            if (float.IsNaN(r.width) || r.width < 1f) return;
            for (int i = 0; i < 3; i++)
            {
                int wave = i;
                Animator.After(0.12f * i, () =>
                {
                    float x = r.xMin + r.width * (0.25f + 0.25f * wave);
                    var at = new Vector2(x, r.yMin + r.height * 0.42f);
                    for (int k = 0; k < ConfettiClasses.Length; k++) _particles.Emit(at, ConfettiClasses[k], k < 4 ? 3 : 2);
                });
            }
        }

        /// <summary>A short HUD message (icon: a gh-toast__icon--* class or null).</summary>
        public void ShowToast(string text, string iconClass, float seconds = HudToast.DefaultSeconds)
        {
            _toast.Show(text, iconClass, seconds);
        }

        public HudToast Toast
        {
            get { return _toast; }
        }

        /// <summary>Development builds and the editor: shows the time-of-day slider at <paramref name="hours"/>.</summary>
        public void EnableDebugTime(float hours)
        {
            _debug.AddToClassList(DebugOnClass);
            if (_timeSlider == null)
            {
                _timeSlider = new HudSlider(_debug, "hud-debug-slider", 0f, 24f, hours);
                _timeSlider.ValueChanged += h => Raise(TimeOfDayChanged, h);
            }
            SetDebugClock(hours);
        }

        /// <summary>The debug clock and slider follow the world's time (written when the minute changes).</summary>
        public void SetDebugClock(float hours)
        {
            if (_timeSlider == null) return;
            if (!float.IsNaN(_clockHours) && Mathf.Abs(hours - _clockHours) < 1f / 60f) return;
            _clockHours = hours;
            _debugClock.text = Localizer.Format("hud.time", HudFormat.Clock(hours, Localizer.UsesDevanagariDigits));
            _timeSlider.SetValueWithoutNotify(hours);
        }

        // ----- Panels ------------------------------------------------------------------------------------------------

        /// <summary>Opens search (gameplay pauses while it is open). <paramref name="fromGamepad"/>: focus goes to the
        /// first result's "Ride there" instead of the text field, so the D-pad and A pick a place.</summary>
        public void OpenSearch(bool fromGamepad = false)
        {
            if (IsLoading || IsPaused) return;
            _touch.ReleaseAll();
            _search.Open(fromGamepad);
        }

        public void OpenSettings()
        {
            // Opened from the pause panel with the D-pad (its pill had focus): carry focus into the sheet.
            bool navigating = FocusedElement() is Button b && _pauseLayer.Contains(b);
            _settings.Open();
            if (navigating) _settingsFirst.Focus();
        }

        /// <summary>Pauses: the pause panel (Resume, Settings, Main menu) pops in over a dimmed world.</summary>
        public void Pause()
        {
            if (IsPaused || IsLoading) return;
            if (_search.IsOpen) _search.Close();
            IsPaused = true;
            _touch.ReleaseAll();
            _pauseLayer.AddToClassList(PauseOpenClass);
            _pauseScrimNode.Set(MotionChannel.Opacity, 1f);
            if (Animator.Reduced)
            {
                Animator.Play(_pausePanelNode, MotionChannel.Opacity, new Tween(0f, 1f, 0.15f, Ease.Linear));
            }
            else
            {
                Animator.Play(_pausePanelNode, MotionChannel.Opacity, new Tween(0f, 1f, 0.12f, Ease.Linear));
                Animator.Play(_pausePanelNode, MotionChannel.ScaleX, new Tween(0.8f, 1f, 0.35f, Ease.OutBack));
                Animator.Play(_pausePanelNode, MotionChannel.ScaleY, new Tween(0.8f, 1f, 0.35f, Ease.OutBack));
            }
            Raise(PauseChanged, true);
        }

        public void Resume()
        {
            if (!IsPaused) return;
            if (_settings.IsOpen) _settings.Close();
            IsPaused = false;
            _pauseLayer.RemoveFromClassList(PauseOpenClass);
            Raise(PauseChanged, false);
        }

        /// <summary>Gamepad Start: closes the top panel, or toggles pause (focusing Resume, so the D-pad reaches
        /// Settings and Main menu).</summary>
        public void TogglePause()
        {
            if (_settings.IsOpen)
            {
                _settings.Back();
                return;
            }
            if (_search.Back()) return;
            if (IsPaused)
            {
                Resume();
                return;
            }
            Pause();
            if (IsPaused) _pauseResume.Focus();
        }

        /// <summary>
        /// Escape / Android back / a UI cancel: closes the top panel (settings, search, pause) or, while playing,
        /// pauses; while loading it goes back to the menu. <paramref name="fromGamepad"/>: the gamepad's B (the brake)
        /// never pauses the game. Returns true when something happened.
        /// </summary>
        public bool Back(bool fromGamepad = false)
        {
            if (_settings.IsOpen) return _settings.Back();
            if (_search.Back()) return true;
            if (IsPaused)
            {
                Haptics.Play(HapticKind.LightImpact);
                Resume();
                return true;
            }
            if (IsLoading)
            {
                if (fromGamepad) return false;
                Raise(MenuRequested);
                return true;
            }
            if (fromGamepad) return false;
            Haptics.Play(HapticKind.LightImpact);
            Pause();
            return true;
        }

        /// <summary>The HUD comes in after loading: the top bar drops, the speedometer rises, the controls fade in.</summary>
        public void PlayEntrance()
        {
            if (Animator.Reduced)
            {
                Animator.Play(_topNode, MotionChannel.Opacity, new Tween(0f, 1f, 0.25f, Ease.OutCubic));
                Animator.Play(_speedoNode, MotionChannel.Opacity, new Tween(0f, 1f, 0.25f, Ease.OutCubic));
                return;
            }
            Animator.Play(_topNode, MotionChannel.TranslateY, new Tween(-160f, 0f, 0.55f, Ease.OutBounce, 0.15f));
            Animator.Play(_topNode, MotionChannel.Opacity, new Tween(0f, 1f, 0.15f, Ease.Linear, 0.15f));
            Animator.Play(_speedoNode, MotionChannel.TranslateY, new Tween(140f, 0f, 0.45f, Ease.OutBack, 0.3f));
            Animator.Play(_speedoNode, MotionChannel.Opacity, new Tween(0f, 1f, 0.2f, Ease.Linear, 0.3f));
            Animator.Play(_controlsNode, MotionChannel.Opacity, new Tween(0f, 1f, 0.4f, Ease.OutCubic, 0.4f));
        }

        public override void Dispose()
        {
            StopListeningForBack();
            Root.UnregisterCallback<AttachToPanelEvent>(OnAttach);
            StyledRoot.UnregisterCallback<GeometryChangedEvent>(OnHudGeometry);
            _route.UnregisterCallback<GeometryChangedEvent>(OnHudGeometry);
            _speedo.UnregisterCallback<GeometryChangedEvent>(OnHudGeometry);
            _top.UnregisterCallback<GeometryChangedEvent>(OnHudGeometry);
            _touch.Dispose();
            _search.Dispose();
            _settings.Dispose();
            base.Dispose();
        }

        protected override void OnRefresh()
        {
            // Called from the constructor's Refresh, then on every locale change.
            if (_settings == null) return;
            _settings.Refresh();
            _search.Refresh();
            _shownKmh = -1;
            if (_placeRecord != null && !_placeRecord.IsEmpty) _placeName.text = _placeRecord.Display(Localizer.Locale == Localizer.Nepali);
            if (_surfaceKey != null) ApplySurfaceText();
            if (_routeDestination != null) ApplyRouteText();
            _inferred.tooltip = Localizer.Get("hud.surface.inferred");
            _searchButton.tooltip = Localizer.Get("hud.search");
            _menuButton.tooltip = Localizer.Get("hud.menu");
            _garageButton.tooltip = Localizer.Get("hud.garage");
            _cameraButton.tooltip = Localizer.Get("hud.camera");
            ApplyActionText();
            ApplyPromptText();
            if (_hornKey != null) _hornLabel.text = Localizer.Get(_hornKey);
            _modeButton.tooltip = Localizer.Get(_riding ? "hud.walk" : "hud.ride");
            ApplyLoadingText();
            _loadingPercentShown = -1;
            if (_loadingProgress >= 0f)
            {
                float p = _loadingProgress;
                _loadingProgress = -1f;
                SetLoadingProgress(p);
            }
            if (!float.IsNaN(_clockHours))
            {
                float h = _clockHours;
                _clockHours = float.NaN;
                SetDebugClock(h);
            }
        }

        // ----- Text ------------------------------------------------------------------------------------------------

        private void ApplySurfaceText()
        {
            string name = Localizer.Get(_surfaceKey);
            _surfaceLabel.text = _surfaceOffRoad ? Localizer.Format("hud.surface.offroad", name) : name;
        }

        private void ApplyRouteText()
        {
            if (_routeDestination == null) return;
            if (_routeRecord != null) _routeDestination = _routeRecord.Display(Localizer.Locale == Localizer.Nepali);
            bool devanagari = Localizer.UsesDevanagariDigits;
            if (_routeStatusOnly && _routeStatusKey != null)
            {
                _routeStep.text = Localizer.Format("hud.route.to", _routeDestination);
                _routeInfo.text = Localizer.Format(_routeStatusKey, _routeDestination);
                return;
            }
            switch (_stepKind)
            {
                case RouteStepKind.Turn:
                    bool stepKm;
                    string step = HudFormat.Distance(_stepM, devanagari, out stepKm);
                    _routeStep.text = Localizer.Format(stepKm ? "hud.distance_km" : "hud.distance_m", step);
                    break;
                case RouteStepKind.Straight:
                    _routeStep.text = Localizer.Get("hud.route.straight");
                    break;
                case RouteStepKind.BackToRoute:
                    _routeStep.text = Localizer.Get("hud.route.back");
                    break;
                default:
                    _routeStep.text = Localizer.Format("hud.route.to", _routeDestination);
                    break;
            }
            if (_routeBucket < 0)
            {
                _routeInfo.text = "";
                return;
            }
            // Re-derived from the metres, so a language switch also switches the digits.
            bool km;
            string number = HudFormat.Distance(_routeRemainingM, devanagari, out km);
            string distance = Localizer.Format(km ? "hud.distance_km" : "hud.distance_m", number);
            string eta = Localizer.Format("hud.eta_min", HudFormat.Number(_routeMinutes, devanagari));
            _routeInfo.text = Localizer.Format("hud.route.info", distance, eta);
        }

        // ----- Route chip placement ---------------------------------------------------------------------------------

        private void OnHudGeometry(GeometryChangedEvent evt)
        {
            PlaceRouteChip();
        }

        /// <summary>
        /// Puts the route chip where <see cref="RouteChipLayout"/> says (inline left/top in the HUD root), from the laid-out
        /// rectangles of the screen, the safe area (the HUD root), the speedometer, the top bar and every touch control.
        /// Runs on geometry changes only; skipped until the chip and the HUD have been laid out (USS fallback until then).
        /// </summary>
        private void PlaceRouteChip()
        {
            if (_placingChip || !_route.ClassListContains(RouteVisibleClass)) return;
            Rect chip = _route.worldBound;
            Rect safeRect = StyledRoot.worldBound;
            Rect screenRect = Root.panel != null ? Root.panel.visualTree.worldBound : Root.worldBound;
            if (!Valid(chip) || !Valid(safeRect) || !Valid(screenRect)) return;
            int count = 0;
            for (int i = 0; i < _chipObstacles.Length; i++)
            {
                VisualElement e = _chipObstacles[i];
                if (!Shown(e)) continue;
                Rect r = e.worldBound;
                if (Valid(r)) _chipRects[count++] = ToHud(r);
            }
            HudRect topBar = default(HudRect);
            if (Shown(_place) && Valid(_place.worldBound)) topBar = ToHud(_place.worldBound);
            if (Shown(_actionsRow) && Valid(_actionsRow.worldBound))
            {
                HudRect actions = ToHud(_actionsRow.worldBound);
                topBar = topBar.IsEmpty ? actions
                    : HudRect.Edges(Mathf.Min(topBar.X, actions.X), Mathf.Min(topBar.Y, actions.Y), Mathf.Max(topBar.Right, actions.Right),
                                    Mathf.Max(topBar.Bottom, actions.Bottom));
            }
            // The speedometer cluster: the digits and surface chip, and the map credit under them.
            HudRect speedo = Shown(_speedo) && Valid(_speedo.worldBound) ? ToHud(_speedo.worldBound) : default(HudRect);
            if (!speedo.IsEmpty && Shown(_attribution) && Valid(_attribution.worldBound))
            {
                HudRect a = ToHud(_attribution.worldBound);
                speedo = HudRect.Edges(Mathf.Min(speedo.X, a.X), Mathf.Min(speedo.Y, a.Y), Mathf.Max(speedo.Right, a.Right), Mathf.Max(speedo.Bottom, a.Bottom));
            }
            bool portrait = StyledRoot.ClassListContains(OrientationWatcher.PortraitClass);
            RouteChipSlot slot;
            HudRect at = RouteChipLayout.Place(ToHud(screenRect), ToHud(safeRect), portrait, chip.width, chip.height, speedo, topBar, _chipRects,
                                               count, out slot);
            _chipSlot = slot;
            float left = at.X - safeRect.xMin, top = at.Y - safeRect.yMin;
            if (float.IsNaN(left) || float.IsNaN(top)) return;
            IStyle style = _route.style;
            bool same = style.left.keyword == StyleKeyword.Undefined && Mathf.Abs(style.left.value.value - left) < 0.5f &&
                        style.top.keyword == StyleKeyword.Undefined && Mathf.Abs(style.top.value.value - top) < 0.5f;
            if (same) return;
            _placingChip = true;
            style.left = left;
            style.top = top;
            style.right = StyleKeyword.Auto;
            style.bottom = StyleKeyword.Auto;
            _placingChip = false;
        }

        private static bool Shown(VisualElement e)
        {
            return e != null && e.resolvedStyle.display != DisplayStyle.None && e.resolvedStyle.visibility != Visibility.Hidden &&
                   (e.parent == null || e.parent.resolvedStyle.display != DisplayStyle.None);
        }

        private static bool Valid(Rect r)
        {
            return !float.IsNaN(r.x) && !float.IsNaN(r.y) && r.width > 0f && r.height > 0f && !float.IsInfinity(r.width);
        }

        private static HudRect ToHud(Rect r)
        {
            return new HudRect(r.xMin, r.yMin, r.width, r.height);
        }

        private void ApplyLoadingText()
        {
            if (_failureTitleKey != null)
            {
                _loadingTitle.text = Localizer.Get(_failureTitleKey);
                _loadingStatus.text = "";
                _loadingHelp.text = _failureArg != null ? Localizer.Format(_failureBodyKey, _failureArg) : Localizer.Get(_failureBodyKey);
                return;
            }
            _loadingTitle.text = Localizer.Get("explore.loading.title");
            if (_loadingStatusKey == null) _loadingStatus.text = "";
            else if (_loadingStatusArg != null) _loadingStatus.text = Localizer.Format(_loadingStatusKey, _loadingStatusArg);
            else _loadingStatus.text = Localizer.Get(_loadingStatusKey);
        }

        private void ShowFailure(string titleKey, string bodyKey, string argument)
        {
            IsLoading = true;
            _failureTitleKey = titleKey;
            _failureBodyKey = bodyKey;
            _failureArg = argument;
            _loading.RemoveFromClassList(LoadingHiddenClass);
            _loading.AddToClassList(LoadingFailedClass);
            _loadingNode.Set(MotionChannel.Opacity, 1f);
            ApplyLoadingText();
            Haptics.Play(HapticKind.Warning);
            if (!Animator.Reduced) _loadingPanelNode.KickWobble(220f);
        }

        // ----- Idle: the loading scooter bobs -------------------------------------------------------------------------

        private void UpdateIdle(float time, float dt)
        {
            if (!IsLoading || _failureTitleKey != null)
            {
                if (_scooterBobbing)
                {
                    _scooterBobbing = false;
                    _loadingScooterNode.ClearOffsets();
                }
                return;
            }
            _scooterBobbing = true;
            float bob = Mathf.Abs(Mathf.Sin(time * 7f));
            _loadingScooterNode.SetOffset(0f, -10f * bob);
            _loadingScooterNode.SetOffsetRotate(3f * Wave.Sine(time, 0.9f));
        }

        // ----- Back button ----------------------------------------------------------------------------------------

        private void OnAttach(AttachToPanelEvent evt)
        {
            ListenForBack();
        }

        /// <summary>
        /// Escape, Android back and gamepad B arrive as NavigationCancelEvent (UI/Cancel) at the panel's tree. Registered
        /// before the settings sheet's own listener, so this one steps aside while Settings is open.
        /// </summary>
        private void ListenForBack()
        {
            if (_backTarget != null || Root.panel == null) return;
            _backTarget = Root.panel.visualTree;
            _backTarget.RegisterCallback<NavigationCancelEvent>(OnNavigationCancel, TrickleDown.TrickleDown);
        }

        private void StopListeningForBack()
        {
            if (_backTarget == null) return;
            _backTarget.UnregisterCallback<NavigationCancelEvent>(OnNavigationCancel, TrickleDown.TrickleDown);
            _backTarget = null;
        }

        private void OnNavigationCancel(NavigationCancelEvent evt)
        {
            if (_settings.IsOpen) return; // the sheet handles it
            if (Back(GamepadCancelThisFrame())) evt.StopPropagation();
        }

        private Focusable FocusedElement()
        {
            IPanel panel = Root.panel;
            return panel != null && panel.focusController != null ? panel.focusController.focusedElement : null;
        }

        /// <summary>
        /// Every tick: a pill that kept focus after its panel closed (pause, Settings, a search result) is let go, so
        /// gamepad A or Enter never presses a hidden button while driving. Closing Settings over the pause panel hands
        /// focus back to the pause panel's Settings pill.
        /// </summary>
        private void DropStaleFocus(float time, float dt)
        {
            var focused = FocusedElement() as VisualElement;
            if (focused == null) return;
            if (!_settings.IsOpen && _settingsLayer.Contains(focused))
            {
                if (IsPaused) _pauseSettings.Focus();
                else focused.Blur();
            }
            else if (!IsPaused && _pauseLayer.Contains(focused))
            {
                focused.Blur();
            }
            else if (!_search.IsOpen && _searchLayer.Contains(focused))
            {
                focused.Blur();
            }
        }

        private static bool GamepadCancelThisFrame()
        {
#if GHUMANTE_INPUT_SYSTEM
            Gamepad pad = Gamepad.current;
            return pad != null && pad.buttonEast.wasPressedThisFrame;
#else
            return false;
#endif
        }

        private static void Raise(Action handler)
        {
            if (handler != null) handler();
        }

        private static void Raise<T>(Action<T> handler, T value)
        {
            if (handler != null) handler(value);
        }
    }
}
