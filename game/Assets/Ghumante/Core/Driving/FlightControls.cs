namespace Ghumante.Core.Driving
{
    /// <summary>
    /// What the aircraft HUD's touch controls say this frame (UI fills it, the ride reads it; docs/research/w2/ref_flight.md
    /// §5): the flight stick, the throttle slider, the rudder and brake buttons and the one-shot taps.
    /// </summary>
    public struct FlightTouch
    {
        /// <summary>The flight stick in [−1, 1]: x rolls right (and steers on the ground), y is up the screen (pushes
        /// the nose down).</summary>
        public float StickX, StickY;

        /// <summary>The throttle slider's position in [0, 1], or a negative value while the slider is untouched.</summary>
        public float Throttle;

        /// <summary>Rudder buttons held (−1 left, +1 right, 0 none) and the brake button (0–1).</summary>
        public float Rudder, Brake;

        /// <summary>Taps this frame: flaps down, flaps up, the view button, the get-out button.</summary>
        public bool FlapsDown, FlapsUp, View, Exit;

        /// <summary>A finger is on the flight stick.</summary>
        public bool Active;

        /// <summary>A finger touched the aircraft controls this frame (the touch controls should stay visible).</summary>
        public bool Touched;
    }
}
