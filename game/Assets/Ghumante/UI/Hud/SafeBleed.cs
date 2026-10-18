using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ghumante.UI.Hud
{
    /// <summary>Which edges of a bled layer's content stay inside the safe area.</summary>
    [Flags]
    public enum BleedEdges : byte
    {
        None = 0,
        Left = 1,
        Top = 2,
        Right = 4,
        Bottom = 8,
        All = Left | Top | Right | Bottom,
    }

    /// <summary>
    /// A full-screen layer of the Explore HUD (the search sheet, the pause scrim, the loading overlay) sits inside the
    /// document root, which <see cref="SafeArea"/> pads to the safe area; on a notched phone it would stop short of the
    /// screen's edges, leaving the world visible and undimmed under the notch, the punch hole and the home indicator.
    /// <see cref="Apply"/> extends the layer by the insets (negative offsets) and keeps its content inside the safe area
    /// on the chosen edges (margins on the content, or padding on the layer itself for a centred panel), as the Settings
    /// sheet does. It writes styles only when the insets change; call it on the root's GeometryChangedEvent and when
    /// <see cref="Screen.safeArea"/> changes (a 180-degree turn moves the insets without resizing the root).
    /// </summary>
    public static class SafeBleed
    {
        /// <param name="safeRoot">The element <see cref="SafeArea"/> pads (the document root).</param>
        /// <param name="layer">The absolutely positioned full-screen layer to extend.</param>
        /// <param name="content">The element kept inside the safe area (margins), or null to pad the layer itself.</param>
        /// <param name="keep">The edges kept inside (a bottom sheet: left, right, bottom; a side panel: top, right,
        /// bottom; a centred panel: all).</param>
        public static void Apply(VisualElement safeRoot, VisualElement layer, VisualElement content, BleedEdges keep)
        {
            if (safeRoot == null || layer == null) return;
            float left, top, right, bottom;
            SafeArea.Insets(safeRoot, out left, out top, out right, out bottom);
            IStyle s = layer.style;
            Set(s.left, -left, v => s.left = v);
            Set(s.top, -top, v => s.top = v);
            Set(s.right, -right, v => s.right = v);
            Set(s.bottom, -bottom, v => s.bottom = v);
            float kl = (keep & BleedEdges.Left) != 0 ? left : 0f, kt = (keep & BleedEdges.Top) != 0 ? top : 0f;
            float kr = (keep & BleedEdges.Right) != 0 ? right : 0f, kb = (keep & BleedEdges.Bottom) != 0 ? bottom : 0f;
            if (content != null)
            {
                IStyle c = content.style;
                Set(c.marginLeft, kl, v => c.marginLeft = v);
                Set(c.marginTop, kt, v => c.marginTop = v);
                Set(c.marginRight, kr, v => c.marginRight = v);
                Set(c.marginBottom, kb, v => c.marginBottom = v);
                return;
            }
            // A centred panel: padding on the layer keeps it in the safe area; absolutely placed children (the scrim)
            // still cover the whole layer, padding included.
            Set(s.paddingLeft, kl, v => s.paddingLeft = v);
            Set(s.paddingTop, kt, v => s.paddingTop = v);
            Set(s.paddingRight, kr, v => s.paddingRight = v);
            Set(s.paddingBottom, kb, v => s.paddingBottom = v);
        }

        /// <summary>The edges a sheet keeps inside the safe area: the portrait bottom sheet spans the width (left, right,
        /// bottom), the landscape side panel the height (top, right, bottom).</summary>
        public static BleedEdges SheetEdges(bool portrait)
        {
            return portrait ? BleedEdges.Left | BleedEdges.Right | BleedEdges.Bottom : BleedEdges.Top | BleedEdges.Right | BleedEdges.Bottom;
        }

        // Writing an identical value would still dirty the layout and re-trigger GeometryChangedEvent.
        private static void Set(StyleLength current, float value, Action<StyleLength> write)
        {
            if (current.keyword == StyleKeyword.Undefined && Mathf.Approximately(current.value.value, value)) return;
            write(value);
        }
    }
}
