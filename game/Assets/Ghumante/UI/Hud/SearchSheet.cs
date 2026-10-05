using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Motion;
using Ghumante.Core.Search;
using Ghumante.Core.Services;
using Ghumante.UI.Localization;
using Ghumante.UI.Motion;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ghumante.UI.Hud
{
    /// <summary>The search sheet's elements, resolved by the Explore screen (check_ui verifies the names).</summary>
    public sealed class SearchSheetView
    {
        public VisualElement Layer;
        public Button Scrim;
        public VisualElement Sheet;
        public Button Close;
        public VisualElement FieldHost;
        public Label Placeholder;
        public Button Clear;
        public Label Hint;
        public ScrollView Results;
    }

    /// <summary>
    /// "Where to?" (ARCHITECTURE.md 7.8, 7.10a): a bottom sheet in portrait and a side panel in landscape, sliding in over
    /// a dimmed scrim like Settings (<see cref="EdgeSheet"/> pose; a fade with Reduce motion). It searches the region with
    /// Core's <see cref="SearchEngine"/> as the player types (latin, Devanagari, romanised Nepali, typos) and lists up to
    /// <see cref="MaxResults"/> places: the name in the current language with the other script below it, the kind and
    /// the distance from the explorer. Each row offers "Ride there" (a route) and, in development builds and the editor,
    /// "Teleport". With an empty query it suggests the region's landmarks. Enter rides to the first result.
    /// </summary>
    public sealed class SearchSheet : IDisposable
    {
        public const int MaxResults = 12;
        public const string OpenClass = "gh-sheet-layer--open";

        /// <summary>Seconds after the last keystroke before searching.</summary>
        public const float Debounce = 0.12f;

        private sealed class Row
        {
            public VisualElement Root;
            public Label Name;
            public Label Alternate;
            public Label Meta;
            public Button Ride;
            public Button Teleport;
            public Label RideLabel;
            public Label TeleportLabel;
            public SearchEntry Entry;
        }

        private readonly SearchSheetView _view;
        private readonly UiAnimator _animator;
        private readonly Localizer _localizer;
        private readonly IHaptics _haptics;
        private readonly VisualElement _orientationRoot;
        private readonly TextField _field;
        private readonly Row[] _rows = new Row[MaxResults];
        private readonly MotionNode _sheetNode;
        private readonly MotionNode _scrimNode;
        private readonly List<SearchEntry> _shown = new List<SearchEntry>(MaxResults);
        private SearchEngine _engine;
        private SearchEntry[] _suggestions;
        private bool _teleportAllowed;
        private double _fromX, _fromZ;
        private bool _hasFrom;
        private bool _open;
        private float _progress, _from, _target, _start, _duration;
        private bool _sliding;
        private int _searchToken;

        public SearchSheet(SearchSheetView view, UiAnimator animator, Localizer localizer, IHaptics haptics,
                           Func<Button, HapticKind, Action, PressFeel> feel, VisualElement orientationRoot)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _animator = animator ?? throw new ArgumentNullException(nameof(animator));
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
            _haptics = haptics ?? throw new ArgumentNullException(nameof(haptics));
            if (feel == null) throw new ArgumentNullException(nameof(feel));
            _orientationRoot = orientationRoot;
            _sheetNode = animator.Node(view.Sheet, true);
            _scrimNode = animator.Node(view.Scrim);

            feel(view.Scrim, HapticKind.Selection, Close).Bouncy = false;
            feel(view.Close, HapticKind.LightImpact, Close);
            feel(view.Clear, HapticKind.Selection, ClearQuery);

            _field = new TextField { name = "search-field", isDelayed = false };
            _field.AddToClassList("gh-search__input");
            view.FieldHost.Add(_field);
            view.Placeholder.BringToFront();
            _field.RegisterValueChangedCallback(OnQueryChanged);
            _field.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

            for (int i = 0; i < MaxResults; i++)
            {
                _rows[i] = BuildRow(i, feel);
                view.Results.Add(_rows[i].Root);
            }
            animator.OnUpdate(UpdateSlide);
            Refresh();
        }

        /// <summary>The player chose "Ride there".</summary>
        public event Action<SearchEntry> RideRequested;

        /// <summary>The player chose "Teleport" (development builds and the editor).</summary>
        public event Action<SearchEntry> TeleportRequested;

        public bool IsOpen
        {
            get { return _open; }
        }

        /// <summary>The region's search (null: the sheet says search is unavailable).</summary>
        public SearchEngine Engine
        {
            get { return _engine; }
            set
            {
                _engine = value;
                _suggestions = null;
                if (_open) RunSearch();
            }
        }

        /// <summary>Show "Teleport" on each row (development builds and the editor only).</summary>
        public bool TeleportAllowed
        {
            get { return _teleportAllowed; }
            set
            {
                _teleportAllowed = value;
                for (int i = 0; i < _rows.Length; i++) Show(_rows[i].Teleport, value);
            }
        }

        /// <summary>The query in the field.</summary>
        public string Query
        {
            get { return _field.value ?? ""; }
        }

        /// <summary>Entries currently listed, in order.</summary>
        public IReadOnlyList<SearchEntry> Results
        {
            get { return _shown; }
        }

        /// <summary>Where distances are measured from (the explorer, game metres).</summary>
        public void SetPlayerPosition(double x, double z)
        {
            _fromX = x;
            _fromZ = z;
            _hasFrom = true;
        }

        public void Open()
        {
            if (_open) return;
            _open = true;
            _view.Layer.AddToClassList(OpenClass);
            RunSearch();
            StartSlide(1f, _animator.Reduced ? 0.15f : 0.38f);
            // Give the field the keyboard once the layer is laid out (a phone shows its keyboard then).
            _view.FieldHost.schedule.Execute(() =>
            {
                if (_open) _field.Focus();
            }).StartingIn(60);
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            _field.Blur();
            StartSlide(0f, _animator.Reduced ? 0.12f : 0.22f);
        }

        /// <summary>Back / Escape: closes the sheet when open (true), else false.</summary>
        public bool Back()
        {
            if (!_open) return false;
            _haptics.Play(HapticKind.LightImpact);
            Close();
            return true;
        }

        /// <summary>Sets the query and searches at once (tests, deep links).</summary>
        public void SetQuery(string query)
        {
            _field.SetValueWithoutNotify(query ?? "");
            RunSearch();
        }

        /// <summary>Re-applies the localised labels (after a locale change) and the rows.</summary>
        public void Refresh()
        {
            _view.Placeholder.text = _localizer.Get("search.placeholder");
            for (int i = 0; i < _rows.Length; i++)
            {
                _rows[i].RideLabel.text = _localizer.Get("search.ride");
                _rows[i].TeleportLabel.text = _localizer.Get("search.teleport");
            }
            if (_open) RunSearch();
            else UpdatePlaceholder();
        }

        public void Dispose()
        {
            _searchToken++;
            _field.UnregisterValueChangedCallback(OnQueryChanged);
            _field.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        }

        // ----- Searching ------------------------------------------------------------------------------------------

        private void OnQueryChanged(ChangeEvent<string> evt)
        {
            UpdatePlaceholder();
            int token = ++_searchToken;
            _animator.After(Debounce, () =>
            {
                if (token == _searchToken) RunSearch();
            });
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
            RunSearch();
            if (_shown.Count > 0) Choose(_shown[0], false);
            evt.StopPropagation();
        }

        private void ClearQuery()
        {
            _field.value = "";
            _field.Focus();
        }

        private void RunSearch()
        {
            _searchToken++;
            UpdatePlaceholder();
            _shown.Clear();
            string query = Query.Trim();
            if (_engine == null)
            {
                _view.Hint.text = _localizer.Get("search.unavailable");
            }
            else if (query.Length == 0)
            {
                _view.Hint.text = _localizer.Get("search.suggestions");
                SearchEntry[] suggestions = Suggestions();
                for (int i = 0; i < suggestions.Length && _shown.Count < MaxResults; i++) _shown.Add(suggestions[i]);
            }
            else
            {
                List<SearchResult> results = _engine.Search(query, MaxResults);
                for (int i = 0; i < results.Count; i++) _shown.Add(results[i].Entry);
                _view.Hint.text = results.Count == 0 ? _localizer.Format("search.empty", query) : "";
            }
            Show(_view.Hint, _view.Hint.text.Length > 0);
            bool nepali = _localizer.Locale == Localizer.Nepali;
            for (int i = 0; i < _rows.Length; i++)
            {
                Row row = _rows[i];
                if (i >= _shown.Count)
                {
                    row.Entry = null;
                    Show(row.Root, false);
                    continue;
                }
                SearchEntry e = _shown[i];
                row.Entry = e;
                string primary = e.Name.Display(nepali);
                string other = e.Name.Display(!nepali);
                row.Name.text = primary;
                row.Alternate.text = other != primary ? other : "";
                Show(row.Alternate, row.Alternate.text.Length > 0);
                row.Meta.text = Meta(e);
                Show(row.Root, true);
            }
            _view.Results.scrollOffset = Vector2.zero;
        }

        private string Meta(SearchEntry e)
        {
            string kind = _localizer.Get(HudFormat.KindKey(e.Kind));
            if (!_hasFrom) return kind;
            double dx = e.X - _fromX, dz = e.Z - _fromZ;
            bool km;
            string number = HudFormat.Distance(Math.Sqrt(dx * dx + dz * dz), _localizer.UsesDevanagariDigits, out km);
            string distance = _localizer.Format(km ? "hud.distance_km" : "hud.distance_m", number);
            return kind + "  ·  " + _localizer.Format("search.away", distance);
        }

        /// <summary>The region's landmarks by importance (else its most important places), for an empty query.</summary>
        private SearchEntry[] Suggestions()
        {
            if (_suggestions != null) return _suggestions;
            SearchEntry[] all = _engine.Index.Entries;
            var picks = new List<SearchEntry>();
            for (int i = 0; i < all.Length; i++)
                if (all[i].Landmark && !all[i].Name.IsEmpty) picks.Add(all[i]);
            if (picks.Count == 0)
            {
                for (int i = 0; i < all.Length; i++)
                    if (!all[i].Name.IsEmpty) picks.Add(all[i]);
            }
            picks.Sort((a, b) => a.Importance != b.Importance ? b.Importance.CompareTo(a.Importance)
                                                               : string.CompareOrdinal(a.DisplayName, b.DisplayName));
            // One entry per name: a landmark often appears as a POI and as a place.
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var unique = new List<SearchEntry>(MaxResults);
            for (int i = 0; i < picks.Count && unique.Count < MaxResults; i++)
                if (seen.Add(picks[i].DisplayName)) unique.Add(picks[i]);
            _suggestions = unique.ToArray();
            return _suggestions;
        }

        private void Choose(SearchEntry entry, bool teleport)
        {
            if (entry == null) return;
            Action<SearchEntry> handler = teleport ? TeleportRequested : RideRequested;
            Close();
            if (handler != null) handler(entry);
        }

        private void UpdatePlaceholder()
        {
            bool empty = string.IsNullOrEmpty(_field.value);
            Show(_view.Placeholder, empty);
            Show(_view.Clear, !empty);
        }

        // ----- Rows ----------------------------------------------------------------------------------------------

        private Row BuildRow(int index, Func<Button, HapticKind, Action, PressFeel> feel)
        {
            var row = new Row { Root = new VisualElement { name = "search-row-" + index } };
            row.Root.AddToClassList("gh-result");
            var text = new VisualElement { pickingMode = PickingMode.Ignore };
            text.AddToClassList("gh-result__text");
            row.Name = new Label { pickingMode = PickingMode.Ignore };
            row.Name.AddToClassList("gh-result__name");
            row.Alternate = new Label { pickingMode = PickingMode.Ignore };
            row.Alternate.AddToClassList("gh-result__alt");
            row.Meta = new Label { pickingMode = PickingMode.Ignore };
            row.Meta.AddToClassList("gh-result__meta");
            text.Add(row.Name);
            text.Add(row.Alternate);
            text.Add(row.Meta);
            row.Root.Add(text);

            var actions = new VisualElement();
            actions.AddToClassList("gh-result__actions");
            row.Ride = Pill("search-ride-" + index, "gh-pill--green", out row.RideLabel);
            row.Teleport = Pill("search-teleport-" + index, "gh-pill--cyan", out row.TeleportLabel);
            actions.Add(row.Ride);
            actions.Add(row.Teleport);
            row.Root.Add(actions);
            Show(row.Teleport, _teleportAllowed);
            feel(row.Ride, HapticKind.MediumImpact, () => Choose(row.Entry, false));
            feel(row.Teleport, HapticKind.LightImpact, () => Choose(row.Entry, true));
            Show(row.Root, false);
            return row;
        }

        private static Button Pill(string name, string colour, out Label label)
        {
            var button = new Button { name = name, focusable = false };
            button.AddToClassList("gh-pill");
            button.AddToClassList("gh-pill--small");
            button.AddToClassList(colour);
            button.AddToClassList("gh-result__action");
            var gloss = new VisualElement { pickingMode = PickingMode.Ignore };
            gloss.AddToClassList("gh-pill__gloss");
            label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList("gh-pill__label");
            button.Add(gloss);
            button.Add(label);
            return button;
        }

        private static void Show(VisualElement e, bool visible)
        {
            DisplayStyle d = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (e.style.display != d) e.style.display = d;
        }

        // ----- Slide ---------------------------------------------------------------------------------------------

        private void StartSlide(float target, float duration)
        {
            _from = _progress;
            _target = target;
            _start = _animator.Time;
            _duration = duration;
            _sliding = true;
            ApplySlide();
        }

        private void UpdateSlide(float time, float dt)
        {
            if (!_sliding) return;
            float t = _duration > 0f ? (time - _start) / _duration : 1f;
            float eased = _target > _from ? (_animator.Reduced ? Easing.OutCubic(t) : Easing.OutBack(t, 1.1f)) : Easing.InCubic(t);
            _progress = Easing.Lerp(_from, _target, eased);
            if (t >= 1f)
            {
                _progress = _target;
                _sliding = false;
                if (_target <= 0f) _view.Layer.RemoveFromClassList(OpenClass);
            }
            ApplySlide();
        }

        private void ApplySlide()
        {
            float shown = Mathf.Clamp01(_progress);
            _scrimNode.Set(MotionChannel.Opacity, shown);
            if (_animator.Reduced)
            {
                _sheetNode.Set(MotionChannel.TranslateX, 0f);
                _sheetNode.Set(MotionChannel.TranslateY, 0f);
                _sheetNode.Set(MotionChannel.ScaleX, 1f);
                _sheetNode.Set(MotionChannel.ScaleY, 1f);
                _sheetNode.Set(MotionChannel.Opacity, shown);
                return;
            }
            bool portrait = _orientationRoot != null && _orientationRoot.ClassListContains(OrientationWatcher.PortraitClass);
            Rect r = _view.Sheet.layout;
            float size = portrait ? r.height : r.width;
            if (float.IsNaN(size) || size < 1f) size = 2400f;
            float offset, stretch;
            EdgeSheet.Pose(_progress, size, size + 48f, out offset, out stretch);
            _sheetNode.Set(MotionChannel.Opacity, 1f);
            _sheetNode.Set(MotionChannel.TranslateX, portrait ? 0f : offset);
            _sheetNode.Set(MotionChannel.TranslateY, portrait ? offset : 0f);
            _sheetNode.Set(MotionChannel.ScaleX, portrait ? 1f : stretch);
            _sheetNode.Set(MotionChannel.ScaleY, portrait ? stretch : 1f);
        }
    }
}
