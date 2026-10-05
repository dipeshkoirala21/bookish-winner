// Ghumante/InstancedLights: small unlit emissive markers drawn with GPU instancing (W2_DESIGN 8.1 airport lights,
// 8.5 aircraft navigation lights and beacons). Each instance carries its colour (_InstanceColor, sRGB) and the
// global _GhNightLights (0 by day .. 1 at night, set by the aviation presenter) brightens them after dusk; by day they
// stay visible as dim coloured studs. Opaque, no alpha blending, earth curvature like the world. URP 17.3 Forward.
Shader "Ghumante/InstancedLights"
{
    Properties
    {
        _DayLevel("Brightness by day", Range(0, 2)) = 0.6
        _NightLevel("Brightness at night", Range(0, 8)) = 3
        _Curvature("Earth curvature (1 = real, 0 = flat)", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LightsVert
            #pragma fragment LightsFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _DayLevel;
                half _NightLevel;
                float _Curvature;
            CBUFFER_END

            float _GhNightLights;

            UNITY_INSTANCING_BUFFER_START(GhLightInstance)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceColor)
            UNITY_INSTANCING_BUFFER_END(GhLightInstance)

            #include "GhumanteCommon.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 color : TEXCOORD0;
            };

            Varyings LightsVert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = GhEarthCurvature(TransformObjectToWorld(input.positionOS.xyz), _Curvature);
                o.positionCS = TransformWorldToHClip(positionWS);
                half3 c = GhVertexColorToLinear((half3)UNITY_ACCESS_INSTANCED_PROP(GhLightInstance, _InstanceColor).rgb);
                o.color = c * lerp(_DayLevel, _NightLevel, saturate(_GhNightLights));
                return o;
            }

            half4 LightsFrag(Varyings i) : SV_Target
            {
                return half4(i.color, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
