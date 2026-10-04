using System;
using Ghumante.UI.Localization;
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

    /// <summary>Presenter for <c>Screens/MainMenu.uxml</c>.</summary>
    public sealed class MainMenuScreen : ScreenBase
    {
        private const float ToastSeconds = 2.2f;

        private readonly Label _hearts;
        private readonly Label _stars;
        private readonly Label _coins;
        private readonly Label _energy;
        private readonly Label _toast;
        private MenuCounters _counters;
        private IVisualElementScheduledItem _hideToast;

        public MainMenuScreen(VisualElement root, Localizer localizer, MenuCounters counters)
            : base(root, localizer)
        {
            _counters = counters;
            _hearts = Required<Label>("counter-hearts-value");
            _stars = Required<Label>("counter-stars-value");
            _coins = Required<Label>("counter-coins-value");
            _energy = Required<Label>("counter-energy-value");
            _toast = Required<Label>("toast");

            Required<Button>("explore-button").clicked += () => Raise(ExploreRequested);
            Required<Button>("map-button").clicked += () => Raise(MapRequested);
            Required<Button>("collections-button").clicked += () => Raise(CollectionsRequested);
            Required<Button>("settings-pill").clicked += () => Raise(SettingsRequested);
            Required<Button>("settings-button").clicked += () => Raise(SettingsRequested);
            Required<Button>("language-button").clicked += () => Raise(LanguageToggleRequested);
            Required<Button>("text-test-button").clicked += () => Raise(TextTestRequested);
            foreach (string counter in new[] { "hearts", "stars", "coins", "energy" })
            {
                string id = counter;
                Required<Button>("counter-" + id + "-add").clicked += () =>
                {
                    Action<string> handler = AddCounterRequested;
                    if (handler != null) handler(id);
                };
            }

            Refresh();
        }

        public event Action ExploreRequested;
        public event Action MapRequested;
        public event Action CollectionsRequested;
        public event Action SettingsRequested;
        public event Action LanguageToggleRequested;
        public event Action TextTestRequested;

        /// <summary>Raised with "hearts", "stars", "coins" or "energy".</summary>
        public event Action<string> AddCounterRequested;

        public void SetCounters(MenuCounters counters)
        {
            _counters = counters;
            OnRefresh();
        }

        /// <summary>Shows a short notice above the bottom bar, e.g. for features that land in M1.</summary>
        public void ShowToast(string text)
        {
            _toast.text = text;
            _toast.AddToClassList("gh-toast--visible");
            if (_hideToast != null) _hideToast.Pause();
            _hideToast = _toast.schedule.Execute(() => _toast.RemoveFromClassList("gh-toast--visible"))
                .StartingIn((long)(ToastSeconds * 1000f));
        }

        protected override void OnRefresh()
        {
            _hearts.text = Localizer.FormatNumber(_counters.hearts);
            _stars.text = Localizer.FormatNumber(_counters.stars);
            _coins.text = Localizer.FormatNumber(_counters.coins);
            _energy.text = Localizer.FormatNumber(_counters.energy);
        }

        private static void Raise(Action handler)
        {
            if (handler != null) handler();
        }
    }
}
