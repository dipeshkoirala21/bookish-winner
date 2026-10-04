using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ghumante.UI
{
    /// <summary>Layout family of the current screen (ARCHITECTURE.md 7.10a, ADR-017).</summary>
    public enum LayoutOrientation
    {
        Landscape = 0,
        Portrait = 1,
    }

    /// <summary>
    /// Derives portrait/landscape from the aspect ratio of the <b>safe area</b>, not from
    /// <see cref="Screen.orientation"/>, so foldables, split screen and iPad multitasking get the right
    /// layout (ARCHITECTURE.md 7.10a). It toggles <c>orient-portrait</c> / <c>orient-landscape</c> on a
    /// screen's styled root, so each UXML carries one layout with USS rules per orientation class, and
    /// raises <see cref="OrientationChanged"/> for camera rigs and controls.
    /// </summary>
    public static class OrientationWatcher
    {
        public const string PortraitClass = "orient-portrait";
        public const string LandscapeClass = "orient-landscape";

        /// <summary>Orientation last applied to a tracked root.</summary>
        public static LayoutOrientation Current { get; private set; } = LayoutOrientation.Landscape;

        /// <summary>Raised when the layout family changes (device rotation, window resize, fold/unfold).</summary>
        public static event Action<LayoutOrientation> OrientationChanged;

        /// <summary>Square or wider is landscape; taller than wide is portrait.</summary>
        public static LayoutOrientation Classify(float width, float height)
        {
            return height > width ? LayoutOrientation.Portrait : LayoutOrientation.Landscape;
        }

        /// <summary>Keeps the orientation classes of <paramref name="styledRoot"/> current.</summary>
        public static void Track(VisualElement styledRoot)
        {
            if (styledRoot == null) return;
            styledRoot.RegisterCallback<GeometryChangedEvent>(_ => Apply(styledRoot));
            Apply(styledRoot);
        }

        public static void Apply(VisualElement styledRoot)
        {
            Rect safe = Screen.safeArea;
            LayoutOrientation orientation = safe.width > 0f && safe.height > 0f
                ? Classify(safe.width, safe.height)
                : Classify(Screen.width, Screen.height);
            styledRoot.EnableInClassList(PortraitClass, orientation == LayoutOrientation.Portrait);
            styledRoot.EnableInClassList(LandscapeClass, orientation == LayoutOrientation.Landscape);
            if (orientation == Current) return;
            Current = orientation;
            Action<LayoutOrientation> handler = OrientationChanged;
            if (handler != null) handler(orientation);
        }
    }
}
