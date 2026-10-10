using Ghumante.Platform;

namespace Ghumante.World.Rendering
{
    /// <summary>
    /// What the cartoon look costs per device tier (ARCHITECTURE.md 10 budgets): texture size and triplanar quality,
    /// how far the procedural textures reach before fading to the flat vertex colour, glints, and the inverted-hull
    /// outline. Outlines follow the ARCHITECTURE 10 "Outlines" row: Mid outlines characters, vehicles and landmarks (the
    /// explorer, traffic, people, animals, aircraft, hero replicas); High adds street props and the near building band
    /// (B0, inside the outline range); far bands, trees and the ground never have the pass. Low draws none at all
    /// (<see cref="ShaderLod"/> 200 selects the SubShader without the outline pass, so there is no extra draw): a recorded
    /// deviation from "landmarks only", which would need the outline SubShader and a second draw for every hero on the
    /// tier with the tightest batch budget. Engine-free values; <see cref="ToonLook"/> and
    /// <see cref="WorldMaterialDefaults.RoleOf"/> apply them.
    /// </summary>
    public struct ToonLookTier
    {
        /// <summary>Edge length of each material texture (power of two).</summary>
        public int TextureSize;

        /// <summary>Anisotropic filtering of the texture array (roads at grazing angles).</summary>
        public int AnisoLevel;

        /// <summary>Blended triplanar (3 samples) instead of the dominant projection only (1 sample).</summary>
        public bool Triplanar;

        /// <summary>Strength of the macro variation layer (0..1).</summary>
        public float Macro;

        /// <summary>Glints on gilt and water.</summary>
        public bool Glints;

        /// <summary>Distance (m) at which the textures have faded to the flat colour.</summary>
        public float DetailFadeM;

        /// <summary><c>Ghumante/ToonLit</c>'s maximum LOD: 300 with the outline pass, 200 without.</summary>
        public int ShaderLod;

        /// <summary>Outline width in pixels at 1080 p (scaled with the screen height).</summary>
        public float OutlineWidthPx;

        /// <summary>Outline fade: full width up to <see cref="OutlineFadeStartM"/>, gone at <see cref="OutlineFadeEndM"/>.</summary>
        public float OutlineFadeStartM, OutlineFadeEndM;

        /// <summary>Outline colour = surface colour × this × light level.</summary>
        public float OutlineDarkness;

        /// <summary>Outline the near building band (B0 cells) too (High).</summary>
        public bool OutlineNearBuildings;

        /// <summary>Outline street props (High; the <c>props</c> material).</summary>
        public bool OutlineProps;

        /// <summary>True when the tier draws outlines.</summary>
        public bool Outlines
        {
            get { return ShaderLod >= ToonLitLayout.LodWithOutline && OutlineWidthPx > 0f; }
        }

        public static ToonLookTier For(DeviceTier tier)
        {
            switch (tier)
            {
                case DeviceTier.Low:
                    return new ToonLookTier
                    {
                        TextureSize = 128, AnisoLevel = 1, Triplanar = false, Macro = 0.8f, Glints = false, DetailFadeM = 80f,
                        ShaderLod = ToonLitLayout.LodWithoutOutline, OutlineWidthPx = 0f, OutlineFadeStartM = 0f, OutlineFadeEndM = 0f,
                        OutlineDarkness = 0.3f, OutlineNearBuildings = false, OutlineProps = false,
                    };
                case DeviceTier.Mid:
                    return new ToonLookTier
                    {
                        TextureSize = 256, AnisoLevel = 2, Triplanar = true, Macro = 1f, Glints = true, DetailFadeM = 160f,
                        ShaderLod = ToonLitLayout.LodWithOutline, OutlineWidthPx = 1.6f, OutlineFadeStartM = 30f, OutlineFadeEndM = 80f,
                        OutlineDarkness = 0.3f, OutlineNearBuildings = false, OutlineProps = false,
                    };
                default:
                    return new ToonLookTier
                    {
                        TextureSize = 256, AnisoLevel = 4, Triplanar = true, Macro = 1f, Glints = true, DetailFadeM = 260f,
                        ShaderLod = ToonLitLayout.LodWithOutline, OutlineWidthPx = 2f, OutlineFadeStartM = 40f, OutlineFadeEndM = 110f,
                        OutlineDarkness = 0.3f, OutlineNearBuildings = true, OutlineProps = true,
                    };
            }
        }
    }
}
