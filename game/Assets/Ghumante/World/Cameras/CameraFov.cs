using System;

namespace Ghumante.World.Cameras
{
    /// <summary>
    /// Field-of-view maths for orientation-independent framing (ARCHITECTURE.md 7.10a, ADR-017).
    /// Unity's <c>Camera.fieldOfView</c> is vertical, so on a tall portrait screen a fixed value would
    /// collapse the horizontal view. Camera rigs instead declare a minimum horizontal FOV and set the
    /// vertical FOV from it every time the aspect changes.
    /// </summary>
    /// <remarks>
    /// Pure System.Math with no engine types, so it can move to <c>Ghumante.Core</c> unchanged if a
    /// non-Unity consumer ever needs it. Rigs in Vehicles, Characters and Activities share it through here.
    /// </remarks>
    public static class CameraFov
    {
        /// <summary>Upper bound for the computed vertical FOV, in degrees.</summary>
        public const float MaxVerticalFovDegrees = 100f;

        /// <summary>Minimum horizontal FOV of the driving chase camera, in degrees.</summary>
        public const float DrivingMinHorizontalFov = 62f;

        /// <summary>Minimum horizontal FOV of the walking camera, in degrees.</summary>
        public const float WalkingMinHorizontalFov = 55f;

        /// <summary>Minimum horizontal FOV of the flying camera, in degrees.</summary>
        public const float FlyingMinHorizontalFov = 70f;

        /// <summary>
        /// Vertical FOV (degrees) that shows exactly <paramref name="horizontalFovDegrees"/> horizontally at
        /// <paramref name="aspect"/> (width / height): <c>2·atan(tan(h/2) / aspect)</c>, clamped to
        /// <see cref="MaxVerticalFovDegrees"/>. Landscape (aspect &gt; 1) gives a smaller vertical FOV,
        /// portrait (aspect &lt; 1) a larger one.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="horizontalFovDegrees"/> is not in (0, 180) or <paramref name="aspect"/> is not a
        /// positive finite number.
        /// </exception>
        public static float VerticalFromHorizontal(float horizontalFovDegrees, float aspect)
        {
            if (!(horizontalFovDegrees > 0f && horizontalFovDegrees < 180f))
            {
                throw new ArgumentOutOfRangeException(nameof(horizontalFovDegrees), horizontalFovDegrees,
                    "Horizontal FOV must be in (0, 180) degrees.");
            }
            if (!(aspect > 0f) || float.IsInfinity(aspect))
            {
                throw new ArgumentOutOfRangeException(nameof(aspect), aspect, "Aspect must be positive and finite.");
            }

            double halfH = horizontalFovDegrees * (Math.PI / 360.0);
            double vertical = 2.0 * Math.Atan(Math.Tan(halfH) / aspect) * (180.0 / Math.PI);
            return (float)Math.Min(vertical, MaxVerticalFovDegrees);
        }

        /// <summary>
        /// Horizontal FOV (degrees) seen with <paramref name="verticalFovDegrees"/> at <paramref name="aspect"/>;
        /// the inverse of <see cref="VerticalFromHorizontal"/> before clamping.
        /// </summary>
        public static float HorizontalFromVertical(float verticalFovDegrees, float aspect)
        {
            double halfV = verticalFovDegrees * (Math.PI / 360.0);
            return (float)(2.0 * Math.Atan(Math.Tan(halfV) * aspect) * (180.0 / Math.PI));
        }
    }
}
