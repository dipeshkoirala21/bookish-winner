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
            "_Cull", "_OffsetFactor", "_OffsetUnits", "_BandFade", "_WindOn", "_InstanceTintOn", "_BandStencilRef", "_BandStencilRead",
            "_BandStencilWrite", "_BandStencilComp", "_BandStencilPass",
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

        /// <summary>Band order of the near B0 ring (also the whole B0 band), the innermost building band.</summary>
        public const int BandOrderNear = 0;

        /// <summary>Band order of the lite B0 ring.</summary>
        public const int BandOrderLite = 1;

        /// <summary>Band order of B1 (also B1 drawn from the camera when B0 is off).</summary>
        public const int BandOrderB1 = 2;

        /// <summary>Band order of B2.</summary>
        public const int BandOrderB2 = 3;

        /// <summary>Band order of B3, the outermost building band.</summary>
        public const int BandOrderB3 = 4;

        /// <summary>Render queue of the innermost band; band order k draws at <c>BandQueueBase + k</c>, so every band is
        /// drawn after all bands inside it (still opaque Geometry, before the overlay queues).</summary>
        public const int BandQueueBase = 2000;

        /// <summary>
        /// The stencil hand-off between building bands (owner playtest #4: a band edge's cross-fade showed holes into the
        /// houses and z-fought where both bands drew). Band order k (0 near ring ... 4 B3) marks bit k of the user stencil
        /// bits where it draws (B3, the last, marks nothing) and draws only where no inner band marked
        /// (<c>Comp Equal</c>: <c>(ref &amp; read) == (stencil &amp; read)</c> with <c>ref &amp; read == 0</c>). Bits 0-3
        /// are URP's user stencil bits (StencilUsage.UserMask); the outline pass tests but never marks. Engine-free.
        /// </summary>
        public static void BandStencil(int order, out int reference, out int readMask, out int writeMask)
        {
            if (order < 0) order = 0;
            if (order > BandOrderB3) order = BandOrderB3;
            writeMask = order < BandOrderB3 ? 1 << order : 0;
            readMask = (1 << order) - 1;
            reference = writeMask;
        }

        /// <summary>Render queue of band order <paramref name="order"/>.</summary>
        public static int BandQueue(int order)
        {
            return BandQueueBase + (order < 0 ? 0 : order > BandOrderB3 ? BandOrderB3 : order);
        }

        /// <summary>Channels with a texture slice and a look (<c>GH_SLICE_COUNT</c> = MaterialTextures.SliceCount); the
        /// shader renders any other channel value as Plain.</summary>
        public const int SliceCount = 26;
    }
}
