// Ghumante/RouteRibbon: the bright animated route line (ARCHITECTURE.md 7.8). Unlit and transparent: a glowing band
// with white edges and chevrons scrolling towards the destination (UV v is metres along the route), drawn a little
// above the ground with the same earth-curvature drop as the terrain and a depth pull so it is never hidden in the
// road it follows. Vertex alpha fades out stretches whose ground has not streamed in yet. Fog is applied at half
// strength so the route stays readable in the haze. SRP Batcher compatible.
Shader "Ghumante/RouteRibbon"
{
    Properties
    {
        _RibbonColor("Colour", Color) = (1, 0.83, 0.24, 1)
        _RibbonEdgeColor("Edge colour", Color) = (1, 1, 1, 1)
        _ScrollSpeed("Chevron speed (m/s)", Float) = 6
        _ChevronSpacing("Chevron spacing (m)", Float) = 7
        _EdgeWidth("Edge width (fraction of the width)", Range(0, 0.45)) = 0.14
        _Opacity("Opacity", Range(0, 1)) = 0.92
        _Curvature("Earth curvature (1 = real, 0 = flat)", Range(0, 1)) = 1
        _ViewPull("Depth pull towards the camera (fraction of distance)", Range(0, 0.01)) = 0.0008
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "RouteRibbon"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex RibbonVert
            #pragma fragment RibbonFrag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _RibbonColor;
                half4 _RibbonEdgeColor;
                float _ScrollSpeed;
                float _ChevronSpacing;
                half _EdgeWidth;
                half _Opacity;
                float _Curvature;
                float _ViewPull;
            CBUFFER_END

            #include "GhumanteCommon.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half alpha : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings RibbonVert(Attributes input)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS = GhEarthCurvature(positionWS, _Curvature);
                positionWS = GhViewPull(positionWS, _ViewPull);
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = input.uv;
                o.alpha = input.color.a;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 RibbonFrag(Varyings i) : SV_Target
            {
                half across = abs(i.uv.x - 0.5h) * 2.0h; // 0 centre, 1 edge
                half edge = smoothstep(1.0h - _EdgeWidth * 2.0h, 1.0h - _EdgeWidth, across);
                // Chevrons: a V pattern moving along the route.
                float phase = (i.uv.y - _Time.y * _ScrollSpeed) / max(_ChevronSpacing, 0.1);
                float v = frac(phase + across * 0.35);
                half chevron = smoothstep(0.0h, 0.08h, v) * (1.0h - smoothstep(0.32h, 0.42h, v));
                half3 color = lerp(_RibbonColor.rgb, _RibbonEdgeColor.rgb, saturate(edge + chevron * 0.55h));
                half outline = 1.0h - smoothstep(0.96h, 1.0h, across);
                half alpha = _Opacity * outline * i.alpha;

                half fog = InitializeInputDataFog(float4(i.positionWS, 1.0), i.fogFactor);
                color = lerp(color, MixFog(color, fog), 0.5h);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
