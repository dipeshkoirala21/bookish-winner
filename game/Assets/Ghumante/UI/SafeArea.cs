using UnityEngine;
using UnityEngine.UIElements;

namespace Ghumante.UI
{
    /// <summary>
    /// Pads a full-screen root so content avoids notches and rounded corners (<see cref="Screen.safeArea"/>).
    /// UI Toolkit does not do this by itself. Call <see cref="Track"/> once per root; the padding is
    /// recomputed whenever the root's geometry changes (rotation, split screen, resolution change). A
    /// 180-degree turn moves the insets without resizing the root, so screens that care also re-apply when
    /// <see cref="Screen.safeArea"/> changes (see <see cref="Insets"/>).
    /// </summary>
    public static class SafeArea
    {
        public static void Track(VisualElement root)
        {
            if (root == null) return;
            root.RegisterCallback<GeometryChangedEvent>(_ => Apply(root));
            root.RegisterCallback<AttachToPanelEvent>(_ => Apply(root));
            Apply(root);
        }

        public static void Apply(VisualElement root)
        {
            IPanel panel = root.panel;
            if (panel == null || Screen.width <= 0 || Screen.height <= 0) return;

            Rect safe = Screen.safeArea;
            // Screen.safeArea has its origin at the bottom-left; panel coordinates grow downwards from the
            // top-left, and RuntimePanelUtils.ScreenToPanel expects the top-left origin.
            Vector2 origin = RuntimePanelUtils.ScreenToPanel(panel, Vector2.zero);
            Vector2 end = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(Screen.width, Screen.height));
            Vector2 topLeft = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(safe.xMin, Screen.height - safe.yMax));
            Vector2 bottomRight = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(safe.xMax, Screen.height - safe.yMin));

            SetIfChanged(root, Mathf.Max(0f, topLeft.x - origin.x), Mathf.Max(0f, topLeft.y - origin.y),
                Mathf.Max(0f, end.x - bottomRight.x), Mathf.Max(0f, end.y - bottomRight.y));
        }

        /// <summary>
        /// The safe-area padding of <paramref name="root"/> as <see cref="Apply"/> writes it inline, re-applied
        /// first so it is current whatever order GeometryChangedEvent callbacks run in. Code that extends
        /// something under the insets (a backdrop, a sheet) must read these, not
        /// <c>root.resolvedStyle.padding*</c>: the resolved padding comes from the last layout pass, and the pass
        /// that applies new padding sends the root no GeometryChangedEvent, because padding does not change the
        /// root's own rect. Without a panel (EditMode tests) it returns whatever padding is set inline.
        /// </summary>
        public static void Insets(VisualElement root, out float left, out float top, out float right, out float bottom)
        {
            Apply(root);
            IStyle s = root.style;
            left = Inline(s.paddingLeft);
            top = Inline(s.paddingTop);
            right = Inline(s.paddingRight);
            bottom = Inline(s.paddingBottom);
        }

        private static float Inline(StyleLength length)
        {
            if (length.keyword != StyleKeyword.Undefined) return 0f;
            float v = length.value.value;
            return float.IsNaN(v) || float.IsInfinity(v) ? 0f : v;
        }

        private static void SetIfChanged(VisualElement root, float left, float top, float right, float bottom)
        {
            // Writing an identical value would still dirty the layout and re-trigger GeometryChangedEvent.
            IStyle s = root.style;
            if (!Mathf.Approximately(s.paddingLeft.value.value, left)) s.paddingLeft = left;
            if (!Mathf.Approximately(s.paddingTop.value.value, top)) s.paddingTop = top;
            if (!Mathf.Approximately(s.paddingRight.value.value, right)) s.paddingRight = right;
            if (!Mathf.Approximately(s.paddingBottom.value.value, bottom)) s.paddingBottom = bottom;
        }
    }
}
