using UnityEngine;
using UnityEngine.UIElements;

namespace Ghumante.UI
{
    /// <summary>
    /// Pads a full-screen root so content avoids notches and rounded corners (<see cref="Screen.safeArea"/>).
    /// UI Toolkit does not do this by itself. Call <see cref="Track"/> once per root; the padding is
    /// recomputed whenever the root's geometry changes (rotation, split screen, resolution change).
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
