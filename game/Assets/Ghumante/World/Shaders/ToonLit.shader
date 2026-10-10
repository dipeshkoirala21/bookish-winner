// Ghumante/ToonLit: the world's cartoon surface (ARCHITECTURE.md 8, World/README.md "Look"). Albedo is the vertex colour
// (sRGB palette colours from the Core meshers) times a procedural material texture chosen per vertex by UV0
// (docs/W2_DETAIL_CONTRACT.md §5: u = MaterialChannel, v = baked AO), sampled triplanar in object space from the
// runtime-generated texture array (Core.Synth.Textures.MaterialTextures, uploaded by ToonLook), with a macro variation
// layer and a distance fade to the flat colour. Lighting: the main light through a 3-band toon ramp with soft edges and
// cool shadow tint, baked AO, a toon highlight per channel (metal and gilt tinted, glints on gilt and water), sky
// reflection on glass and water, a soft rim on the lit side (warm at sunrise: the alpenglow), SH ambient, main-light
// shadows, URP fog, and the earth-curvature vertex drop. Meshes without UV0 render exactly as before (Plain, no AO).
// Cartoon outlines: an inverted-hull pass (LightMode SRPDefaultUnlit) in the LOD 300 SubShader; ToonLook selects the
// LOD 200 SubShader (no outline pass, no extra draw) on Low. Occluder fade (_OCCLUDER_FADE): screen-door dither of
// everything inside the camera-to-player capsule, so houses never block the view.
// Terrain, buildings, roads and areas all use it; roads and areas add a depth pull (_ViewPull) and a polygon offset so
// they never z-fight the ground. URP 17.3 Forward, SRP Batcher compatible (UnityPerMaterial in ToonLitInput.hlsl),
// hand-written HLSL. W2: GPU instancing for the instanced dressing (Graphics.RenderMeshInstanced, GLES3-safe),
// _INSTANCE_TINT (a per-instance colour on vertex-alpha-masked parts), _WIND (vertex sway for trees) and _BAND_FADE
// (the dithered opaque cross-fade of the building bands, W2_DESIGN 2.4). Shader model 3.5 (GLES3, Metal, Vulkan).
Shader "Ghumante/ToonLit"
{
    Properties
    {
        _BaseColor("Tint", Color) = (1, 1, 1, 1)
        _ShadowTint("Shadow tint (cool, multiplies the unlit side)", Color) = (0.66, 0.72, 0.92, 1)
        _RampThresholds("Ramp: x dark|mid edge, y mid|lit edge (N.L), z softness", Vector) = (0.02, 0.42, 0.05, 0)
        _RampLevels("Ramp levels: x dark, y mid, z lit", Vector) = (0.32, 0.70, 1, 0)
        _RimColor("Rim colour", Color) = (1, 0.93, 0.82, 1)
        _RimPower("Rim power", Range(0.5, 8)) = 3.5
        _RimStrength("Rim strength", Range(0, 1)) = 0.3
        _AmbientStrength("Ambient strength", Range(0, 2)) = 0.85
        _Curvature("Earth curvature (1 = real, 0 = flat)", Range(0, 1)) = 1
        _ViewPull("Depth pull towards the camera (fraction of distance)", Range(0, 0.01)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        _OffsetFactor("Depth offset factor", Float) = 0
        _OffsetUnits("Depth offset units", Float) = 0
        _BandRange("Band: x inner, y outer, z fade (m from the camera)", Vector) = (0, 100000, 4, 0)
        _Wind("Wind: x amplitude, y Hz, zw direction", Vector) = (0, 0.45, 0.8, 0.6)
        _DetailStrength("Procedural texture strength", Range(0, 1)) = 1
        _AoStrength("Baked AO strength", Range(0, 1)) = 1
        _SpecularStrength("Highlight strength", Range(0, 2)) = 1
        _OutlineWidth("Outline width (x the tier width, 0 = none)", Range(0, 3)) = 1
        [Toggle(_OCCLUDER_FADE)] _OccluderFade("Fade between the camera and the player", Float) = 0
        [Toggle(_BAND_FADE)] _BandFade("Building band fade", Float) = 0
        [Toggle(_WIND)] _WindOn("Vertex wind", Float) = 0
        [Toggle(_INSTANCE_TINT)] _InstanceTintOn("Per-instance tint (instanced)", Float) = 0
    }

    // Mid and High: with the outline pass.
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "IgnoreProjector" = "True"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            Offset [_OffsetFactor], [_OffsetUnits]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ToonVert
            #pragma fragment ToonFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _BAND_FADE
            #pragma shader_feature_local_fragment _OCCLUDER_FADE
            #pragma shader_feature_local_vertex _WIND
            #pragma shader_feature_local_vertex _INSTANCE_TINT

            // Main light shadows (1 cascade on Low/Mid, 2 on High) and URP's soft-shadow quality keywords.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #include "ToonLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "ToonLitForwardPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On
            ZTest LEqual
            Offset [_OffsetFactor], [_OffsetUnits]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _BAND_FADE
            #pragma shader_feature_local_fragment _OCCLUDER_FADE
            #pragma shader_feature_local_vertex _WIND
            #pragma shader_feature_local_vertex _INSTANCE_TINT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #include "ToonLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "ToonLitOutlinePass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #pragma shader_feature_local _BAND_FADE
            #pragma shader_feature_local_vertex _WIND

            #define GH_SHADOW_CASTER_PASS 1
            #include "ToonLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "ToonLitDepthPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]
            Offset [_OffsetFactor], [_OffsetUnits]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _BAND_FADE
            #pragma shader_feature_local_fragment _OCCLUDER_FADE
            #pragma shader_feature_local_vertex _WIND

            #include "ToonLitInput.hlsl"
            #include "ToonLitDepthPasses.hlsl"
            ENDHLSL
        }

        // Used when URP renders a normals texture (SSAO and similar features).
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]
            Offset [_OffsetFactor], [_OffsetUnits]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #pragma shader_feature_local _BAND_FADE
            #pragma shader_feature_local_fragment _OCCLUDER_FADE
            #pragma shader_feature_local_vertex _WIND

            #include "ToonLitInput.hlsl"
            #include "ToonLitDepthPasses.hlsl"
            ENDHLSL
        }
    }

    // Low: the same passes without the outline (ToonLook sets Shader.maximumLOD = 200 on this shader).
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "IgnoreProjector" = "True"
        }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            Offset [_OffsetFactor], [_OffsetUnits]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ToonVert
            #pragma fragment ToonFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _BAND_FADE
            #pragma shader_feature_local_fragment _OCCLUDER_FADE
            #pragma shader_feature_local_vertex _WIND
            #pragma shader_feature_local_vertex _INSTANCE_TINT

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #include "ToonLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "ToonLitForwardPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #pragma shader_feature_local _BAND_FADE
            #pragma shader_feature_local_vertex _WIND

            #define GH_SHADOW_CASTER_PASS 1
            #include "ToonLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "ToonLitDepthPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]
            Offset [_OffsetFactor], [_OffsetUnits]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _BAND_FADE
            #pragma shader_feature_local_fragment _OCCLUDER_FADE
            #pragma shader_feature_local_vertex _WIND

            #include "ToonLitInput.hlsl"
            #include "ToonLitDepthPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]
            Offset [_OffsetFactor], [_OffsetUnits]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #pragma shader_feature_local _BAND_FADE
            #pragma shader_feature_local_fragment _OCCLUDER_FADE
            #pragma shader_feature_local_vertex _WIND

            #include "ToonLitInput.hlsl"
            #include "ToonLitDepthPasses.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
