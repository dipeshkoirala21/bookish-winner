using System;

namespace Ghumante.UI.Hud
{
    /// <summary>
    /// Engine-free maths of the touch controls (ARCHITECTURE.md 7.10a): the floating stick, the portrait one-thumb drive
    /// zone, pinch zoom and when the touch controls show. Panel units throughout (about 2.3 per point on a phone).
    /// </summary>
    public static class TouchMath
    {
        /// <summary>Radius of the stick's travel, panel units.</summary>
        public const float StickRadius = 120f;

        /// <summary>Finger travel that counts as no deflection, panel units.</summary>
        public const float StickDeadZone = 12f;

        /// <summary>Horizontal drag (panel units) that gives full steering in the portrait drive zone.</summary>
        public const float DriveSteerRange = 150f;

        /// <summary>Drag that counts as none in the drive zone.</summary>
        public const float DriveDeadZone = 10f;

        /// <summary>Change of the distance between two fingers that counts as one zoom notch.</summary>
        public const float PinchPerNotch = 90f;

        /// <summary>Degrees of camera turn per panel unit dragged on the world.</summary>
        public const float LookDegPerUnit = 0.22f;

        /// <summary>
        /// Stick value for a finger at (dx, dy) from where it went down (panel units, y down): x right and y up in [−1, 1],
        /// with a dead zone at the centre and the length clamped to 1 at <see cref="StickRadius"/>. The knob offset
        /// (where to draw it, clamped to the rim) comes back in <paramref name="knobX"/>/<paramref name="knobY"/>.
        /// </summary>
        public static void Stick(float dx, float dy, out float x, out float y, out float knobX, out float knobY)
        {
            if (float.IsNaN(dx) || float.IsInfinity(dx)) dx = 0f;
            if (float.IsNaN(dy) || float.IsInfinity(dy)) dy = 0f;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length > StickRadius)
            {
                knobX = dx / length * StickRadius;
                knobY = dy / length * StickRadius;
            }
            else
            {
                knobX = dx;
                knobY = dy;
            }
            if (length <= StickDeadZone)
            {
                x = 0f;
                y = 0f;
                return;
            }
            float amount = Math.Min(1f, (length - StickDeadZone) / (StickRadius - StickDeadZone));
            x = dx / length * amount;
            y = -dy / length * amount;
        }

        /// <summary>Portrait drive zone: steering in [−1, 1] for a horizontal drag of <paramref name="dx"/> panel units.</summary>
        public static float DriveSteer(float dx)
        {
            if (float.IsNaN(dx) || float.IsInfinity(dx)) return 0f;
            float a = Math.Abs(dx);
            if (a <= DriveDeadZone) return 0f;
            float s = Math.Min(1f, (a - DriveDeadZone) / (DriveSteerRange - DriveDeadZone));
            return dx < 0f ? -s : s;
        }

        /// <summary>Zoom notches for a pinch from <paramref name="previousSpan"/> to <paramref name="span"/> (spreading
        /// zooms in, positive).</summary>
        public static float PinchNotches(float previousSpan, float span)
        {
            if (!(previousSpan > 0f) || !(span > 0f)) return 0f;
            return (span - previousSpan) / PinchPerNotch;
        }
    }

    /// <summary>
    /// When the HUD shows its touch controls: hidden as soon as a keyboard or gamepad drives (they would only cover the
    /// view), shown again on the first touch; at the start shown on touch devices only. Engine-free state machine.
    /// </summary>
    public sealed class TouchControlsVisibility
    {
        /// <summary>Which kind of input last drove.</summary>
        public enum Source : byte
        {
            None = 0,
            KeysOrPad = 1,
            Touch = 2,
        }

        public TouchControlsVisibility(bool touchDevice)
        {
            Visible = touchDevice;
        }

        public bool Visible { get; private set; }

        /// <summary>Feeds the source that produced input this frame; returns true when <see cref="Visible"/> changed.</summary>
        public bool Update(Source source)
        {
            bool before = Visible;
            if (source == Source.KeysOrPad) Visible = false;
            else if (source == Source.Touch) Visible = true;
            return Visible != before;
        }
    }
}
