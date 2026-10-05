namespace Ghumante.Core.Motion
{
    /// <summary>
    /// Maths for a sheet that slides in from a screen edge (UI/Screens/SettingsSheet: a bottom sheet in
    /// portrait, a side panel in landscape): its pose during the slide, how it follows a dragging finger, and
    /// whether letting go dismisses it. Progress is 0 hidden and 1 open. Pure and allocation-free.
    /// </summary>
    public static class EdgeSheet
    {
        /// <summary>Released below this progress (dragged out more than about a third), the sheet closes.</summary>
        public const float DismissBelow = 0.66f;

        /// <summary>A flick at this speed (progress per second) decides on its own, wherever the sheet is.</summary>
        public const float FlickVelocity = 1.2f;

        /// <summary>How far past open a finger can pull the sheet (progress), with growing resistance.</summary>
        public const float MaxOverPull = 0.06f;

        /// <summary>
        /// The sheet's pose at slide progress <paramref name="progress"/>. <paramref name="offset"/> pushes it
        /// back towards its edge (0 open, <paramref name="travel"/> hidden). An open ease that overshoots past 1
        /// must not lift the sheet off its edge, where it has no border or radius and a gap would show: past 1
        /// it stays on the edge and <paramref name="stretch"/> scales it away from that edge instead (transform
        /// origin on the edge), so its inner edge still follows the bounce.
        /// </summary>
        /// <param name="progress">Slide progress (may overshoot past 1).</param>
        /// <param name="size">The sheet's size along the slide axis (height of a bottom sheet, width of a side panel).</param>
        /// <param name="travel">Distance from open to hidden (the size plus any shadow margin).</param>
        /// <param name="offset">Translation towards the edge, 0 to <paramref name="travel"/>.</param>
        /// <param name="stretch">Scale along the slide axis, 1 or more.</param>
        public static void Pose(float progress, float size, float travel, out float offset, out float stretch)
        {
            if (float.IsNaN(progress)) progress = 0f;
            float shown = progress < 0f ? 0f : (progress > 1f ? 1f : progress);
            offset = (1f - shown) * travel;
            stretch = progress > 1f && size > 0f ? 1f + (progress - 1f) * travel / size : 1f;
        }

        /// <summary>
        /// Progress while a finger drags the sheet: it went down at <paramref name="startProgress"/> and has
        /// moved <paramref name="outward"/> towards the sheet's edge since (negative is inwards). Dragging out
        /// follows the finger exactly; pulling in past open gives way less and less, up to
        /// <see cref="MaxOverPull"/> (a rubbery stretch, see <see cref="Pose"/>).
        /// </summary>
        public static float Drag(float startProgress, float outward, float travel)
        {
            if (!(travel > 0f) || float.IsNaN(outward)) return startProgress;
            float p = startProgress - outward / travel;
            if (p <= 0f) return 0f;
            if (p <= 1f) return p;
            float over = p - 1f;
            // Starts at half the finger's speed and approaches MaxOverPull.
            return 1f + MaxOverPull * over / (over + 2f * MaxOverPull);
        }

        /// <summary>
        /// Whether letting go of a dragged sheet dismisses it: a flick towards the edge does, a flick back in
        /// does not, and otherwise it closes when dragged out past <see cref="DismissBelow"/>.
        /// </summary>
        /// <param name="progress">Progress at release.</param>
        /// <param name="velocity">Progress per second at release (negative is towards hidden).</param>
        public static bool Dismisses(float progress, float velocity)
        {
            if (velocity <= -FlickVelocity) return true;
            if (velocity >= FlickVelocity) return false;
            return progress < DismissBelow;
        }
    }
}
