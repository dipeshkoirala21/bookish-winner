namespace Ghumante.World.Rendering
{
    /// <summary>
    /// The C# twin of <c>Ghumante/ToonLit</c>'s SRP Batcher layout (<c>Shaders/ToonLitInput.hlsl</c>): every material
    /// property the shader reads lives in <c>UnityPerMaterial</c>, in this order, in every pass; render-state and keyword
    /// toggles are declared in the Properties block but never read. Tests parse the shader against these lists, so a
    /// property added to one side only fails the build instead of silently breaking the SRP Batcher. Engine-free.
    /// </summary>
    public static class ToonLitLayout
    {
        /// <summary>Properties in <c>CBUFFER_START(UnityPerMaterial)</c>, in declaration order.</summary>
        public static readonly string[] PerMaterial =
        {
            "_BaseColor", "_ShadowTint", "_RampThresholds", "_RampLevels", "_RimColor", "_RimPower", "_RimStrength",
            "_AmbientStrength", "_Curvature", "_ViewPull", "_BandRange", "_Wind", "_DetailStrength", "_AoStrength",
            "_SpecularStrength", "_OutlineWidth", "_OccluderFade", "_OccluderGround",
        };

        /// <summary>Properties that only drive render state or keywords (no shader variable).</summary>
        public static readonly string[] RenderStateOnly =
        {
            "_Cull", "_OffsetFactor", "_OffsetUnits", "_BandFade", "_WindOn", "_InstanceTintOn",
        };

        /// <summary>Global (per-frame or startup) shader variables the look sets; never per material.</summary>
        public static readonly string[] Globals =
        {
            "_GhBandCentre", "_GhMaterialTex", "_GhChannelA", "_GhChannelB", "_GhLookParams", "_GhOutlineParams",
            "_GhOccluderA", "_GhOccluderB", "_GhOccluderC", "_GhOccluderD",
        };

        /// <summary>Material keywords (shader_feature_local).</summary>
        public const string KeywordOccluderFade = "_OCCLUDER_FADE";

        /// <summary>LightMode of the outline pass (URP's opaque pass draws it after UniversalForward).</summary>
        public const string OutlinePassLightMode = "SRPDefaultUnlit";

        /// <summary>SubShader LOD with the outline pass (Mid, High) and without it (Low).</summary>
        public const int LodWithOutline = 300, LodWithoutOutline = 200;

        /// <summary>Size of the channel arrays (<c>GH_CHANNEL_ARRAY_SIZE</c> = MaterialLooks.ArraySize).</summary>
        public const int ChannelArraySize = 32;

        /// <summary>Channels with a texture slice and a look (<c>GH_SLICE_COUNT</c> = MaterialTextures.SliceCount); the
        /// shader renders any other channel value as Plain.</summary>
        public const int SliceCount = 26;
    }
}
