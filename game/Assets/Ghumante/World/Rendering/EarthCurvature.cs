namespace Ghumante.World.Rendering
{
    /// <summary>
    /// Earth curvature as the world shaders render it (ARCHITECTURE.md 5.3): every vertex is lowered by
    /// <c>d² / (2·R_eff)</c>, with <c>d</c> its horizontal distance to the camera and <c>R_eff = 7/6 · 6 371 000 m</c>
    /// (standard refraction). This is the C# twin of <c>GhEarthCurvature</c> in
    /// <c>Shaders/GhumanteCommon.hlsl</c>, used for renderer bounds and tests. Engine-free.
    /// </summary>
    public static class EarthCurvature
    {
        /// <summary>Mean Earth radius in metres.</summary>
        public const double EarthRadiusM = 6371000.0;

        /// <summary>Standard refraction makes the Earth look 7/6 as large.</summary>
        public const double RefractionFactor = 7.0 / 6.0;

        /// <summary>Effective radius: 7 432 833 m.</summary>
        public const double EffectiveRadiusM = EarthRadiusM * RefractionFactor;

        /// <summary>1 / (2·R_eff), the constant the shaders use (<c>GH_INV_TWO_R_EFF</c>).</summary>
        public const double InverseTwoEffectiveRadius = 1.0 / (2.0 * EffectiveRadiusM);

        /// <summary>Drop in metres at a horizontal distance: 0.067 m at 1 km, 168 m at 50 km, about 1.72 km at 160 km.</summary>
        public static double DropM(double horizontalDistanceM)
        {
            return horizontalDistanceM * horizontalDistanceM * InverseTwoEffectiveRadius;
        }
    }
}
