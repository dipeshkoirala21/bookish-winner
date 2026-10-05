using UnityEngine;

namespace Ghumante.Vehicles.Visuals
{
    /// <summary>
    /// Colours of the placeholder explorer (scooter, rider, walker), picked from the reference style's palette
    /// (UI/Styles/Ghumante.uss, ASSET_MANIFEST.md 1): bright, slightly warm, readable against grey roads and green
    /// hills. sRGB vertex colours; Ghumante/ToonLit linearises them.
    /// </summary>
    public static class ToonPalette
    {
        public static readonly Color32 ScooterBody = new Color32(232, 71, 60, 255);      // ribbon red
        public static readonly Color32 ScooterAccent = new Color32(255, 246, 227, 255);  // cream
        public static readonly Color32 ScooterTrim = new Color32(168, 43, 34, 255);      // ribbon shadow
        public static readonly Color32 Seat = new Color32(74, 48, 32, 255);
        public static readonly Color32 Tyre = new Color32(46, 44, 50, 255);
        public static readonly Color32 Hub = new Color32(196, 200, 206, 255);
        public static readonly Color32 Chrome = new Color32(214, 218, 224, 255);
        public static readonly Color32 Headlamp = new Color32(255, 243, 196, 255);
        public static readonly Color32 Taillight = new Color32(255, 70, 52, 255);
        public static readonly Color32 Black = new Color32(36, 32, 34, 255);

        public static readonly Color32 Shirt = new Color32(53, 194, 241, 255);           // pill cyan
        public static readonly Color32 Trousers = new Color32(64, 74, 118, 255);
        public static readonly Color32 Skin = new Color32(214, 158, 116, 255);
        public static readonly Color32 Helmet = new Color32(255, 203, 46, 255);          // pill yellow
        public static readonly Color32 HelmetStripe = new Color32(219, 141, 18, 255);
        public static readonly Color32 Visor = new Color32(58, 62, 78, 255);
        public static readonly Color32 Shoes = new Color32(92, 56, 32, 255);
        public static readonly Color32 Eyes = new Color32(30, 26, 28, 255);
        public static readonly Color32 Scarf = new Color32(123, 210, 62, 255);           // pill green

        /// <summary>Dust kicked up on dry gravel and dirt.</summary>
        public static readonly Color32 Dust = new Color32(222, 196, 150, 255);

        /// <summary>Mud splashes.</summary>
        public static readonly Color32 Mud = new Color32(120, 86, 56, 255);

        /// <summary>A soft grey puff (paved ground: landings and recoveries only).</summary>
        public static readonly Color32 Smoke = new Color32(232, 232, 236, 255);
    }
}
