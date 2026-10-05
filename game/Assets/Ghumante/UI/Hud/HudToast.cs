using System;
using Ghumante.Core.Motion;
using Ghumante.UI.Motion;
using UnityEngine.UIElements;

namespace Ghumante.UI.Hud
{
    /// <summary>
    /// The Explore toast: the main menu's speech-bubble toast (<c>.gh-toast</c>, ASSET_MANIFEST.md 12.1) for HUD
    /// messages: it slides in from the top with a pop, stays a few seconds and leaves by itself; with Reduce motion it
    /// only fades (the USS opacity transition). A newer toast replaces the one showing.
    /// </summary>
    public sealed class HudToast
    {
        public const string VisibleClass = "gh-toast--visible";
        public const float DefaultSeconds = 2.8f;

        private readonly UiAnimator _animator;
        private readonly VisualElement _bubble;
        private readonly VisualElement _icon;
        private readonly Label _text;
        private readonly MotionNode _bubbleNode;
        private readonly MotionNode _iconNode;
        private string _iconClass;
        private int _token;

        public HudToast(UiAnimator animator, VisualElement bubble, VisualElement icon, Label text)
        {
            _animator = animator ?? throw new ArgumentNullException(nameof(animator));
            _bubble = bubble ?? throw new ArgumentNullException(nameof(bubble));
            _icon = icon ?? throw new ArgumentNullException(nameof(icon));
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _bubbleNode = animator.Node(bubble);
            _iconNode = animator.Node(icon);
        }

        /// <summary>True while a toast is up.</summary>
        public bool Showing
        {
            get { return _bubble.ClassListContains(VisibleClass); }
        }

        /// <summary>The text showing (or last shown).</summary>
        public string Text
        {
            get { return _text.text; }
        }

        /// <summary>Shows <paramref name="text"/> with an optional icon class (gh-toast__icon--*) for
        /// <paramref name="seconds"/> (0 or less: until <see cref="Hide"/>).</summary>
        public void Show(string text, string iconClass, float seconds = DefaultSeconds)
        {
            _text.text = text;
            if (_iconClass != iconClass)
            {
                if (_iconClass != null) _icon.RemoveFromClassList(_iconClass);
                if (iconClass != null) _icon.AddToClassList(iconClass);
                _iconClass = iconClass;
            }
            DisplayStyle iconDisplay = iconClass != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (_icon.style.display != iconDisplay) _icon.style.display = iconDisplay;
            bool wasShowing = Showing;
            _bubble.AddToClassList(VisibleClass);
            if (!_animator.Reduced)
            {
                if (!wasShowing) _animator.Play(_bubbleNode, MotionChannel.TranslateY, new Tween(-90f, 0f, 0.5f, Ease.OutBack));
                _animator.Play(_bubbleNode, MotionChannel.ScaleX, new Tween(0.85f, 1f, 0.4f, Ease.OutBack));
                _animator.Play(_bubbleNode, MotionChannel.ScaleY, new Tween(0.85f, 1f, 0.4f, Ease.OutBack));
                if (iconClass != null) _iconNode.KickHop(260f);
            }
            int token = ++_token;
            if (seconds > 0f) _animator.After(seconds, () => { if (token == _token) Hide(); });
        }

        /// <summary>Takes the toast away now.</summary>
        public void Hide()
        {
            _token++;
            if (!Showing) return;
            _bubble.RemoveFromClassList(VisibleClass);
            if (!_animator.Reduced) _animator.Play(_bubbleNode, MotionChannel.TranslateY, new Tween(0f, -40f, 0.3f, Ease.InCubic));
        }
    }
}
