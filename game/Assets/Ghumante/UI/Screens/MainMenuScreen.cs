using System;
using Ghumante.Core.Motion;
using Ghumante.Core.Services;
using Ghumante.UI.Localization;
using Ghumante.UI.Motion;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ghumante.UI.Screens
{
    /// <summary>Values shown in the counter pills. M1 reads them from the save's progress section.</summary>
    public struct MenuCounters
    {
        public int hearts;
        public int stars;
        public long coins;
        public int energy;
    }

    /// <summary>
    /// Presenter for <c>Screens/MainMenu.uxml</c>: the animated main menu (UI/README.md, "Motion").
    /// <list type="bullet">
    /// <item><b>Entrance</b> (about 1.1 s, replayed whenever the menu is shown): counters drop in one by one
    /// with a bounce, the title ribbon swings in and its tails flutter, the subtitle rises, the four pills pop
    /// in one after another, the bottom bar slides up. With Reduce motion: a short fade.</item>
    /// <item><b>Idle life</b>: a living backdrop (<see cref="LivingBackdrop"/>), the Explore pill breathes and a
    /// shine sweeps across it, the ribbon sways, counter icons hop now and then.</item>
    /// <item><b>Touch</b>: every button squashes with a haptic (<see cref="PressFeel"/>). "+" counts the demo
    /// value up with a burst of particles and a Success haptic; Explore (until M1's world arrives) wiggles
    /// with a Warning haptic and a scooter toast; the title ribbon says "Namaste!"; the globe flips the
    /// language; the gear and the Settings pill open the <see cref="SettingsSheet"/>.</item>
    /// </list>
    /// App decides what the buttons lead to through the events; the reactions above are methods it calls.
    /// </summary>
    public sealed class MainMenuScreen : ScreenBase
    {
        /// <summary>Demo caps for the counters with a maximum (hearts and energy refill; coins and stars grow).</summary>
        public const int MaxHearts = 5;
        public const int MaxEnergy = 100;

        /// <summary>Seconds a toast stays up.</summary>
        public const float ToastSeconds = 2.8f;

        /// <summary>Seconds the counter takes to roll up to its new value.</summary>
        public const float CountUpSeconds = 0.6f;

        private const int Hearts = 0;
        private const int Stars = 1;
        private const int Coins = 2;
        private const int Energy = 3;

        private static readonly string[] CounterIds = { "hearts", "stars", "coins", "energy" };
        private static readonly long[] DemoStep = { 1, 1, 50, 10 };
        private static readonly string[] ParticleClasses =
            { "gh-particle--heart", "gh-particle--star", "gh-particle--coin", "gh-particle--energy" };

        private readonly Label[] _values = new Label[4];
        private readonly VisualElement[] _icons = new VisualElement[4];
        private readonly MotionNode[] _counterNodes = new MotionNode[4];
        private readonly MotionNode[] _iconNodes = new MotionNode[4];
        private readonly MotionNode[] _valueNodes = new MotionNode[4];
        private readonly long[] _shown = new long[4];
        private readonly long[] _countFrom = new long[4];
        private readonly long[] _countTo = new long[4];
        private readonly float[] _countStart = new float[4];
        private readonly bool[] _counting = new bool[4];

        private readonly VisualElement _ribbon;
        private readonly MotionNode _ribbonNode;
        private readonly MotionNode _tailLeft;
        private readonly MotionNode _tailRight;
        private readonly MotionNode _subtitleNode;
        private readonly Label _bubble;
        private readonly MotionNode _bubbleNode;
        private readonly MotionNode _panelNode;
        private readonly MotionNode _bottomBarNode;
        private readonly Button _explore;
        private readonly MotionNode[] _pillNodes;
        private readonly MotionNode[] _actionNodes;
        private readonly MotionNode[] _flipNodes;
        private readonly MotionNode _globeNode;
        private readonly MotionNode _gearIconNode;
        private readonly MotionNode _shineNode;
        private readonly bool _shiny;

        private readonly VisualElement _toastBubble;
        private readonly VisualElement _toastIcon;
        private readonly Label _toast;
        private readonly MotionNode _toastNode;
        private readonly MotionNode _toastIconNode;
        private string _toastIconClass;
        private int _toastToken;

        private readonly ParticleBurst _particles;
        private readonly LivingBackdrop _backdrop;
        private readonly SettingsSheet _settings;

        private MenuCounters _counters;
        private MotionRandom _random = new MotionRandom(2072);
        private int _nextHop;
        private float _nextHopAt = 2.5f;
        private bool _flipping;
        private bool _wiggleRight;
        private bool _namasteShowing;

        public MainMenuScreen(VisualElement root, Localizer localizer, MenuCounters counters)
            : this(root, localizer, counters, null, null)
        {
        }

        public MainMenuScreen(VisualElement root, Localizer localizer, MenuCounters counters, IHaptics haptics,
                              MotionSettings motion)
            : base(root, localizer, haptics, motion)
        {
            _counters = counters;
            _values[Hearts] = Required<Label>("counter-hearts-value");
            _values[Stars] = Required<Label>("counter-stars-value");
            _values[Coins] = Required<Label>("counter-coins-value");
            _values[Energy] = Required<Label>("counter-energy-value");
            _icons[Hearts] = Required<VisualElement>("counter-hearts-icon");
            _icons[Stars] = Required<VisualElement>("counter-stars-icon");
            _icons[Coins] = Required<VisualElement>("counter-coins-icon");
            _icons[Energy] = Required<VisualElement>("counter-energy-icon");
            _counterNodes[Hearts] = Animator.Node(Required<VisualElement>("counter-hearts"));
            _counterNodes[Stars] = Animator.Node(Required<VisualElement>("counter-stars"));
            _counterNodes[Coins] = Animator.Node(Required<VisualElement>("counter-coins"));
            _counterNodes[Energy] = Animator.Node(Required<VisualElement>("counter-energy"));
            for (int i = 0; i < 4; i++)
            {
                _iconNodes[i] = Animator.Node(_icons[i]);
                _valueNodes[i] = Animator.Node(_values[i]);
                _shown[i] = Value(i);
            }

            _ribbon = Required<VisualElement>("title-ribbon");
            _ribbonNode = Animator.Node(_ribbon);
            _tailLeft = Animator.Node(Required<VisualElement>("ribbon-tail-left"));
            _tailRight = Animator.Node(Required<VisualElement>("ribbon-tail-right"));
            // The tails' USS rotate (-8 / 8 deg) is their rest pose; the flutter adds to it.
            _tailLeft.SetRest(MotionChannel.RotateDegrees, -8f);
            _tailRight.SetRest(MotionChannel.RotateDegrees, 8f);
            _subtitleNode = Animator.Node(Required<Label>("subtitle"));
            _bubble = Required<Label>("namaste-bubble");
            _bubbleNode = Animator.Node(_bubble);
            _bubbleNode.SetRest(MotionChannel.Opacity, 0f);
            _panelNode = Animator.Node(Required<VisualElement>("menu-panel"));
            _bottomBarNode = Animator.Node(Required<VisualElement>("bottombar"));
            _toastBubble = Required<VisualElement>("toast-bubble");
            _toastIcon = Required<VisualElement>("toast-icon");
            _toast = Required<Label>("toast");
            _toastNode = Animator.Node(_toastBubble);
            _toastIconNode = Animator.Node(_toastIcon);

            _explore = Required<Button>("explore-button");
            PressFeel exploreFeel = Feel(_explore, HapticKind.MediumImpact, () => Raise(ExploreRequested));
            PressFeel map = Feel(Required<Button>("map-button"), HapticKind.LightImpact, () => Raise(MapRequested));
            PressFeel collections = Feel(Required<Button>("collections-button"), HapticKind.LightImpact,
                                         () => Raise(CollectionsRequested));
            PressFeel settingsPill = Feel(Required<Button>("settings-pill"), HapticKind.LightImpact,
                                          () => Raise(SettingsRequested));
            _pillNodes = new[] { exploreFeel.Node, map.Node, collections.Node, settingsPill.Node };

            PressFeel globe = Feel(Required<Button>("language-button"), HapticKind.LightImpact,
                                   () => FlipLanguage(() => Raise(LanguageToggleRequested)));
            PressFeel gear = Feel(Required<Button>("settings-button"), HapticKind.LightImpact, OnGear);
            _globeNode = globe.Node;
            _gearIconNode = Animator.Node(Required<VisualElement>("settings-icon"));
            _actionNodes = new[] { globe.Node, gear.Node };
            Feel(Required<Button>("text-test-button"), HapticKind.LightImpact, () => Raise(TextTestRequested));

            // "+" is small (46 units); the whole counter pill is its touch target, as in most mobile games.
            Feel(Required<Button>("counter-hearts-add"), HapticKind.LightImpact, () => AddDemo(Hearts))
                .ExtendTo(_counterNodes[Hearts].Element).Node.SquashAmount = 0.16f;
            Feel(Required<Button>("counter-stars-add"), HapticKind.LightImpact, () => AddDemo(Stars))
                .ExtendTo(_counterNodes[Stars].Element).Node.SquashAmount = 0.16f;
            Feel(Required<Button>("counter-coins-add"), HapticKind.LightImpact, () => AddDemo(Coins))
                .ExtendTo(_counterNodes[Coins].Element).Node.SquashAmount = 0.16f;
            Feel(Required<Button>("counter-energy-add"), HapticKind.LightImpact, () => AddDemo(Energy))
                .ExtendTo(_counterNodes[Energy].Element).Node.SquashAmount = 0.16f;

            _flipNodes = new[]
            {
                Animator.Node(Required<Label>("explore-label")), Animator.Node(Required<Label>("map-label")),
                Animator.Node(Required<Label>("collections-label")), Animator.Node(Required<Label>("settings-label")),
                _subtitleNode,
            };

            _ribbon.RegisterCallback<ClickEvent>(OnRibbonClicked);

            // Shine: a light band swept across Explore. Its clipping mask costs a stencil pass, so not on Low.
            _shineNode = Animator.Node(Required<VisualElement>("explore-shine"));
            _shineNode.SetRest(MotionChannel.Opacity, 0f);
            _shiny = !Motion.LowPower;
            _explore.EnableInClassList("gh-pill--shiny", _shiny);

            _particles = new ParticleBurst(Animator, Required<VisualElement>("fx-layer"), Motion.LowPower ? 10 : 22, 4011u);
            _backdrop = new LivingBackdrop(Animator, Required<VisualElement>("backdrop"), StyledRoot, Root);

            _settings = new SettingsSheet(new SettingsSheetView
            {
                Layer = Required<VisualElement>("settings-layer"),
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
            _settings.LanguageRequested += locale => FlipLanguage(() => Raise(LanguageSelected, locale));

            Animator.OnIdle(UpdateIdle, null);
            Animator.OnUpdate(UpdateCounting);

            Refresh();
            PlayEntrance();
            Animator.Commit();
        }

        public event Action ExploreRequested;
        public event Action MapRequested;
        public event Action CollectionsRequested;
        public event Action SettingsRequested;
        public event Action LanguageToggleRequested;
        public event Action TextTestRequested;

        /// <summary>Raised with "hearts", "stars", "coins" or "energy" after "+" added to the demo value.</summary>
        public event Action<string> AddCounterRequested;

        /// <summary>Settings: Vibration switched (already applied to IHaptics.Enabled). App persists it.</summary>
        public event Action<bool> HapticsChanged;

        /// <summary>Settings: Reduce motion switched (already applied to MotionSettings). App persists it.</summary>
        public event Action<bool> ReduceMotionChanged;

        /// <summary>Settings: a language was picked ("en" / "ne"); App applies and persists it.</summary>
        public event Action<string> LanguageSelected;

        /// <summary>The values shown (after any "+" taps).</summary>
        public MenuCounters Counters
        {
            get { return _counters; }
        }

        /// <summary>True while idle animation runs (false with Reduce motion or while the app is unfocused).</summary>
        public bool IdleAnimating
        {
            get { return Animator.IdleRunning; }
        }

        public bool SettingsOpen
        {
            get { return _settings.IsOpen; }
        }

        /// <summary>Number of prayer flags on the backdrop's string.</summary>
        public int PrayerFlagCount
        {
            get { return _backdrop.FlagsCreated; }
        }

        public void SetCounters(MenuCounters counters)
        {
            _counters = counters;
            for (int i = 0; i < 4; i++)
            {
                _counting[i] = false;
                _shown[i] = Value(i);
            }
            OnRefresh();
        }

        public void OpenSettings()
        {
            _settings.Open();
        }

        public void CloseSettings()
        {
            _settings.Close();
        }

        /// <summary>
        /// The Android back button or Escape: closes Settings if it is open (the sheet also listens for
        /// NavigationCancelEvent itself while open). False when there was nothing to close, so App can decide
        /// what back means on the menu.
        /// </summary>
        public bool HandleBack()
        {
            return _settings.Back();
        }

        /// <summary>Shows a short notice that slides in from the top, e.g. for features that land in M1.</summary>
        public void ShowToast(string text)
        {
            ShowToast(text, null);
        }

        /// <summary>Shows a toast with an icon (a gh-toast__icon--* class, or null for none).</summary>
        public void ShowToast(string text, string iconClass)
        {
            _toast.text = text;
            if (_toastIconClass != iconClass)
            {
                if (_toastIconClass != null) _toastIcon.RemoveFromClassList(_toastIconClass);
                if (iconClass != null) _toastIcon.AddToClassList(iconClass);
                _toastIconClass = iconClass;
            }
            DisplayStyle iconDisplay = iconClass != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (_toastIcon.style.display != iconDisplay) _toastIcon.style.display = iconDisplay;
            _toastBubble.AddToClassList("gh-toast--visible");
            if (!Animator.Reduced)
            {
                Animator.Play(_toastNode, MotionChannel.TranslateY, new Tween(-90f, 0f, 0.5f, Ease.OutBack));
                Animator.Play(_toastNode, MotionChannel.ScaleX, new Tween(0.8f, 1f, 0.45f, Ease.OutBack));
                Animator.Play(_toastNode, MotionChannel.ScaleY, new Tween(0.8f, 1f, 0.45f, Ease.OutBack));
                if (iconClass != null) _toastIconNode.KickHop(260f);
            }
            int token = ++_toastToken;
            Animator.After(ToastSeconds, () =>
            {
                if (token != _toastToken) return;
                _toastBubble.RemoveFromClassList("gh-toast--visible");
                if (!Animator.Reduced) Animator.Play(_toastNode, MotionChannel.TranslateY, new Tween(0f, -40f, 0.3f, Ease.InCubic));
            });
        }

        /// <summary>
        /// Explore until the world arrives (M1): the pill wiggles with a Warning haptic and a scooter "warms up"
        /// in a toast, puffing exhaust.
        /// </summary>
        public void PlayExploreTeaser()
        {
            Haptics.Play(HapticKind.Warning);
            Wiggle(_pillNodes[0], 300f);
            ShowToast(Localizer.Get("menu.explore_soon"), "gh-toast__icon--scooter");
            if (Animator.Reduced) return;
            Animator.Play(_toastIconNode, MotionChannel.RotateDegrees, new Tween(-12f, 0f, 0.6f, Ease.OutElastic));
            for (int i = 0; i < 3; i++)
            {
                Animator.After(0.35f + 0.28f * i, () =>
                {
                    Rect r = _toastIcon.worldBound;
                    _particles.Emit(new Vector2(r.xMin + r.width * 0.15f, r.yMax - r.height * 0.25f), "gh-particle--puff", 2, ParticleStyle.Puff);
                    _toastIconNode.KickHop(120f);
                });
            }
        }

        /// <summary>Map / Collections until they exist: a wiggle, a Warning haptic and a friendly toast.</summary>
        public void PlayComingSoon(string feature)
        {
            Haptics.Play(HapticKind.Warning);
            if (feature == "collections")
            {
                Wiggle(_pillNodes[2], 240f);
                ShowToast(Localizer.Get("menu.collections_soon"), "gh-toast__icon--star");
            }
            else
            {
                Wiggle(_pillNodes[1], 240f);
                ShowToast(Localizer.Get("menu.map_soon"), "gh-toast__icon--map");
            }
        }

        /// <summary>The title ribbon's easter egg: a wobble, a "Namaste!" bubble and a Selection haptic.</summary>
        public void PlayNamaste()
        {
            Haptics.Play(HapticKind.Selection);
            if (_namasteShowing) return;
            _namasteShowing = true;
            Animator.Play(_bubbleNode, MotionChannel.Opacity, new Tween(0f, 1f, 0.15f, Ease.Linear));
            if (!Animator.Reduced)
            {
                Wiggle(_ribbonNode, 160f);
                Animator.Play(_ribbonNode, MotionChannel.ScaleX, new Tween(1.1f, 1f, 0.45f, Ease.OutBack));
                Animator.Play(_ribbonNode, MotionChannel.ScaleY, new Tween(0.9f, 1f, 0.45f, Ease.OutBack));
                Animator.Play(_bubbleNode, MotionChannel.ScaleX, new Tween(0.3f, 1f, 0.4f, Ease.OutBack));
                Animator.Play(_bubbleNode, MotionChannel.ScaleY, new Tween(0.3f, 1f, 0.4f, Ease.OutBack));
                Animator.Play(_bubbleNode, MotionChannel.TranslateY, new Tween(24f, 0f, 0.4f, Ease.OutBack));
                Rect r = _ribbon.worldBound;
                _particles.Emit(new Vector2(r.center.x, r.yMin + r.height * 0.3f), "gh-particle--sparkle", 6);
            }
            Animator.After(1.5f, () =>
            {
                Animator.Play(_bubbleNode, MotionChannel.Opacity, new Tween(1f, 0f, 0.3f, Ease.Linear));
                if (!Animator.Reduced) Animator.Play(_bubbleNode, MotionChannel.TranslateY, new Tween(0f, -26f, 0.3f, Ease.InCubic));
                Animator.After(0.3f, () => _namasteShowing = false);
            });
        }

        /// <summary>
        /// The entrance choreography (about 1.15 s). Runs on construction, so it plays every time App shows the
        /// menu, including when coming back from another screen.
        /// </summary>
        public void PlayEntrance()
        {
            if (Animator.Reduced)
            {
                FadeIn(0.25f);
                return;
            }
            var drop = new Stagger(0.05f, 0.08f, 0.55f, Ease.OutBounce);
            for (int i = 0; i < _counterNodes.Length; i++)
            {
                Animator.Play(_counterNodes[i], MotionChannel.TranslateY, drop.At(i, -150f, 0f));
                Animator.Play(_counterNodes[i], MotionChannel.Opacity, new Tween(0f, 1f, 0.15f, Ease.Linear, drop.DelayOf(i)));
            }
            var actions = new Stagger(0.3f, 0.07f, 0.4f, Ease.OutBack);
            for (int i = 0; i < _actionNodes.Length; i++)
            {
                Animator.Play(_actionNodes[i], MotionChannel.ScaleX, actions.At(i, 0.4f, 1f));
                Animator.Play(_actionNodes[i], MotionChannel.ScaleY, actions.At(i, 0.4f, 1f));
                Animator.Play(_actionNodes[i], MotionChannel.Opacity, new Tween(0f, 1f, 0.12f, Ease.Linear, actions.DelayOf(i)));
            }

            // The ribbon swings in from its top edge, the tails flutter into place behind it.
            Animator.Play(_ribbonNode, MotionChannel.RotateDegrees, new Tween(-14f, 0f, 0.6f, Ease.OutBack, 0.1f));
            Animator.Play(_ribbonNode, MotionChannel.TranslateY, new Tween(-110f, 0f, 0.5f, Ease.OutBack, 0.1f));
            Animator.Play(_ribbonNode, MotionChannel.Opacity, new Tween(0f, 1f, 0.2f, Ease.Linear, 0.1f));
            Animator.Play(_tailLeft, MotionChannel.RotateDegrees, new Tween(-36f, -8f, 0.8f, Ease.OutElastic, 0.35f));
            Animator.Play(_tailRight, MotionChannel.RotateDegrees, new Tween(36f, 8f, 0.8f, Ease.OutElastic, 0.38f));

            Animator.Play(_subtitleNode, MotionChannel.TranslateY, new Tween(36f, 0f, 0.45f, Ease.OutCubic, 0.35f));
            Animator.Play(_subtitleNode, MotionChannel.Opacity, new Tween(0f, 1f, 0.4f, Ease.OutCubic, 0.35f));

            Animator.Play(_panelNode, MotionChannel.ScaleX, new Tween(0.92f, 1f, 0.4f, Ease.OutBack, 0.25f));
            Animator.Play(_panelNode, MotionChannel.ScaleY, new Tween(0.92f, 1f, 0.4f, Ease.OutBack, 0.25f));
            Animator.Play(_panelNode, MotionChannel.Opacity, new Tween(0f, 1f, 0.25f, Ease.OutCubic, 0.25f));

            var pills = new Stagger(0.4f, 0.08f, 0.35f, Ease.OutBack);
            for (int i = 0; i < _pillNodes.Length; i++)
            {
                Animator.Play(_pillNodes[i], MotionChannel.ScaleX, pills.At(i, 0.6f, 1f));
                Animator.Play(_pillNodes[i], MotionChannel.ScaleY, pills.At(i, 0.6f, 1f));
                Animator.Play(_pillNodes[i], MotionChannel.Opacity, new Tween(0f, 1f, 0.12f, Ease.Linear, pills.DelayOf(i)));
            }

            Animator.Play(_bottomBarNode, MotionChannel.TranslateY, new Tween(160f, 0f, 0.45f, Ease.OutCubic, 0.55f));
        }

        public override void Dispose()
        {
            _ribbon.UnregisterCallback<ClickEvent>(OnRibbonClicked);
            _settings.Dispose();
            _backdrop.Dispose();
            base.Dispose();
        }

        protected override void OnRefresh()
        {
            for (int i = 0; i < 4; i++)
            {
                if (!_counting[i]) _shown[i] = Value(i);
                _values[i].text = Localizer.FormatNumber(_shown[i]);
            }
            // The constructor refreshes before the sheet exists.
            if (_settings != null) _settings.Refresh();
        }

        private void OnGear()
        {
            if (!Animator.Reduced)
            {
                float turn = _gearIconNode.Get(MotionChannel.RotateDegrees);
                Animator.Play(_gearIconNode, MotionChannel.RotateDegrees, new Tween(turn, turn + 120f, 0.5f, Ease.OutBack));
            }
            Raise(SettingsRequested);
        }

        private void OnRibbonClicked(ClickEvent evt)
        {
            PlayNamaste();
        }

        private void AddDemo(int i)
        {
            long before = Value(i);
            long cap = i == Hearts ? MaxHearts : (i == Energy ? MaxEnergy : long.MaxValue);
            if (before >= cap)
            {
                Haptics.Play(HapticKind.Warning);
                Wiggle(_counterNodes[i], 260f);
                ShowToast(Localizer.Get(i == Hearts ? "menu.hearts_full" : "menu.energy_full"),
                          i == Hearts ? "gh-toast__icon--heart" : "gh-toast__icon--energy");
                return;
            }
            long after = Math.Min(cap, before + DemoStep[i]);
            SetValue(i, after);
            Haptics.Play(HapticKind.Success);

            if (Animator.Reduced)
            {
                _shown[i] = after;
                _values[i].text = Localizer.FormatNumber(after);
            }
            else
            {
                _countFrom[i] = _shown[i];
                _countTo[i] = after;
                _countStart[i] = Animator.Time;
                _counting[i] = true;
                Animator.Play(_valueNodes[i], MotionChannel.ScaleX, new Tween(1.35f, 1f, 0.4f, Ease.OutBack));
                Animator.Play(_valueNodes[i], MotionChannel.ScaleY, new Tween(1.35f, 1f, 0.4f, Ease.OutBack));
                _iconNodes[i].KickHop(330f);
                _iconNodes[i].KickWobble(_random.Range(-1f, 1f) < 0f ? -140f : 140f);
                Vector2 origin = _icons[i].worldBound.center;
                _particles.Emit(origin, ParticleClasses[i], 6);
                _particles.Emit(origin, "gh-particle--sparkle", 4);
            }

            Action<string> handler = AddCounterRequested;
            if (handler != null) handler(CounterIds[i]);
        }

        private void UpdateCounting(float time, float dt)
        {
            for (int i = 0; i < 4; i++)
            {
                if (!_counting[i]) continue;
                float p = (time - _countStart[i]) / CountUpSeconds;
                long v = CountUp.Value(_countFrom[i], _countTo[i], Easing.OutCubic(p));
                if (p >= 1f)
                {
                    v = _countTo[i];
                    _counting[i] = false;
                }
                if (v == _shown[i]) continue;
                _shown[i] = v;
                _values[i].text = Localizer.FormatNumber(v);  // only when the digits change
            }
        }

        private void UpdateIdle(float time, float dt)
        {
            float breathe = 1f + 0.025f * Wave.Sine(time, 2.4f);
            _pillNodes[0].SetOffsetScale(breathe, breathe);

            if (_shiny)
            {
                float sweep = Wave.Pulse(time, 4.2f, 0.8f);
                if (sweep >= 0f)
                {
                    float width = _explore.layout.width;
                    if (float.IsNaN(width) || width < 1f) width = 420f;
                    _shineNode.Set(MotionChannel.TranslateX, Mathf.Lerp(-150f, width + 60f, Easing.InOutSine(sweep)));
                    _shineNode.Set(MotionChannel.Opacity, Mathf.Sin(sweep * Mathf.PI));
                }
                else if (_shineNode.Get(MotionChannel.Opacity) != 0f)
                {
                    _shineNode.Set(MotionChannel.Opacity, 0f);
                }
            }

            _ribbonNode.SetOffsetRotate(1.6f * Wave.Sine(time, 5.5f));
            _tailLeft.SetOffsetRotate(3.5f * Wave.Sine(time, 1.3f));
            _tailRight.SetOffsetRotate(-3.5f * Wave.Sine(time, 1.3f, 0.27f));

            if (time >= _nextHopAt)
            {
                _iconNodes[_nextHop].KickHop(240f);
                _iconNodes[_nextHop].KickWobble(_nextHop % 2 == 0 ? 90f : -90f);
                _nextHop = (_nextHop + 1 + _random.NextInt(3)) % 4;
                _nextHopAt = time + (Motion.LowPower ? 6f : 3.2f) + _random.Range(0f, 1.6f);
            }
        }

        private void FlipLanguage(Action swap)
        {
            if (Animator.Reduced)
            {
                Haptics.Play(HapticKind.Selection);
                swap();
                return;
            }
            if (_flipping) return;
            _flipping = true;
            const float half = 0.12f;
            for (int i = 0; i < _flipNodes.Length; i++)
                Animator.Play(_flipNodes[i], MotionChannel.ScaleY, new Tween(1f, 0f, half, Ease.InCubic));
            Animator.Play(_globeNode, MotionChannel.ScaleX, new Tween(1f, 0f, half, Ease.InCubic));
            Animator.After(half, () =>
            {
                // Swap the text while everything is edge-on, then flip back in, one after another.
                swap();
                Haptics.Play(HapticKind.Selection);
                for (int i = 0; i < _flipNodes.Length; i++)
                    Animator.Play(_flipNodes[i], MotionChannel.ScaleY, new Tween(0f, 1f, 0.34f, Ease.OutBack, i * 0.04f));
                Animator.Play(_globeNode, MotionChannel.ScaleX, new Tween(0f, 1f, 0.4f, Ease.OutBack));
                _flipping = false;
            });
        }

        private void Wiggle(MotionNode node, float strength)
        {
            if (Animator.Reduced) return;
            _wiggleRight = !_wiggleRight;
            node.KickWobble(_wiggleRight ? strength : -strength);
        }

        private long Value(int i)
        {
            switch (i)
            {
                case Hearts: return _counters.hearts;
                case Stars: return _counters.stars;
                case Coins: return _counters.coins;
                default: return _counters.energy;
            }
        }

        private void SetValue(int i, long v)
        {
            switch (i)
            {
                case Hearts: _counters.hearts = (int)v; break;
                case Stars: _counters.stars = (int)Math.Min(v, int.MaxValue); break;
                case Coins: _counters.coins = v; break;
                default: _counters.energy = (int)v; break;
            }
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
