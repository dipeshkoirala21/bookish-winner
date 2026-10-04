using System;
using Ghumante.UI.Localization;
using UnityEngine.UIElements;

namespace Ghumante.UI.Screens
{
    /// <summary>
    /// Base for a full-screen presenter over a cloned UXML tree. A presenter owns no Unity objects; the
    /// App assembly decides which screen is visible and swaps the UIDocument's visual tree.
    /// </summary>
    public abstract class ScreenBase : IDisposable
    {
        protected ScreenBase(VisualElement root, Localizer localizer)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (localizer == null) throw new ArgumentNullException(nameof(localizer));
            Root = root;
            // Our UXML puts the Ghumante stylesheet on a .gh-root element under the document root. Classes
            // that USS selectors key on (gh-lang-*, spike--standard) go there, inside the sheet's scope.
            StyledRoot = root.Q(className: "gh-root") ?? root;
            Localizer = localizer;
            Localizer.Changed += OnLocaleChanged;
        }

        /// <summary>The UIDocument root the screen was cloned into.</summary>
        public VisualElement Root { get; private set; }

        /// <summary>The screen's .gh-root element (falls back to <see cref="Root"/>).</summary>
        public VisualElement StyledRoot { get; private set; }

        protected Localizer Localizer { get; private set; }

        /// <summary>Re-applies all localised and computed text.</summary>
        public void Refresh()
        {
            Localizer.Apply(StyledRoot);
            OnRefresh();
        }

        /// <summary>Screen-specific text that is computed rather than keyed (counters, device info...).</summary>
        protected virtual void OnRefresh()
        {
        }

        public virtual void Dispose()
        {
            Localizer.Changed -= OnLocaleChanged;
        }

        private void OnLocaleChanged()
        {
            Refresh();
        }

        /// <summary>Finds a named element and fails loudly if the UXML and the presenter disagree.</summary>
        protected T Required<T>(string name) where T : VisualElement
        {
            T element = Root.Q<T>(name);
            if (element == null)
            {
                throw new InvalidOperationException(
                    GetType().Name + ": UXML has no " + typeof(T).Name + " named '" + name + "'.");
            }
            return element;
        }
    }
}
