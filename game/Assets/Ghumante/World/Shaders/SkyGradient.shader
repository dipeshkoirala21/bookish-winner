// Ghumante/SkyGradient: the procedural cartoon sky (ARCHITECTURE.md 8; ASSET_MANIFEST ghm_mat_sky). Zenith, horizon and
// below-horizon colours, sun and moon discs and the sunrise/sunset glow all come from shader globals that
// Ghumante.World.Sky.WorldSky sets every frame from its time-of-day palette. Near the horizon the sky blends into
// the fog colour, so the hazy far terrain meets the sky without a seam. Use as RenderSettings.skybox.
Shader "Ghumante/SkyGradient"
{
    Properties
    {
        _SunDiscSize("Sun disc size (cosine of its radius)", Range(0.99, 0.99999)) = 0.9994
        _SunGlow("Sun glow", Range(0, 4)) = 1
        _HorizonBlend("Fog blend band above the horizon", Range(0.001, 0.3)) = 0.08
        _MoonDiscSize("Moon disc size (cosine of its radius)", Range(0.99, 0.99999)) = 0.9996
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType" = "Skybox"
        }
        Cull Off
        ZWrite Off

        Pass
        {
            Name "Sky"

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex SkyVert
            #pragma fragment SkyFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _SunDiscSize;
                half _SunGlow;
                half _HorizonBlend;
                float _MoonDiscSize;
            CBUFFER_END

            // Globals from WorldSky (colours already linear: set with Shader.SetGlobalColor).
            half4 _GhSkyZenith;
            half4 _GhSkyHorizon;
            half4 _GhSkyGround;
            float4 _GhSunDirection;   // xyz towards the sun, w disc visibility 0..1
            half4 _GhSunColor;
            float4 _GhMoonDirection;  // xyz towards the moon, w night amount 0..1
            half4 _GhSkyParams;       // x golden-hour amount, y night amount

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 directionWS : TEXCOORD0;
            };

            Varyings SkyVert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.directionWS = TransformObjectToWorldDir(input.positionOS.xyz, false);
                return o;
            }

            half4 SkyFrag(Varyings input) : SV_Target
            {
                float3 d = normalize(input.directionWS);
                half up = saturate(d.y);
                half down = saturate(-d.y);

                // Gradient: horizon to zenith above, horizon to ground below (cartoon: quick falloff from the horizon).
                half3 color = lerp(_GhSkyHorizon.rgb, _GhSkyZenith.rgb, sqrt(up));
                color = lerp(color, _GhSkyGround.rgb, saturate(down * 6.0h));

                // Sunrise and sunset glow along the horizon around the sun's azimuth, plus a halo round the sun.
                float3 sun = normalize(_GhSunDirection.xyz);
                float sunDot = dot(d, sun);
                float2 flatD = normalize(d.xz + float2(1e-5, 0.0));
                float2 flatSun = normalize(sun.xz + float2(1e-5, 0.0));
                half azimuthGlow = pow(saturate(dot(flatD, flatSun) * 0.5 + 0.5), 6.0);
                half nearHorizon = pow(1.0h - up, 4.0h) * (1.0h - saturate(down * 8.0h));
                half golden = _GhSkyParams.x;
                color += _GhSunColor.rgb * azimuthGlow * nearHorizon * golden * 0.55h * _SunGlow;
                half halo = pow(saturate(sunDot), 48.0) * 0.35h + pow(saturate(sunDot), 600.0) * 0.6h;
                color += _GhSunColor.rgb * halo * _GhSunDirection.w * _SunGlow;

                // Haze: blend into the fog colour just above the horizon (and below it).
                half fogBand = 1.0h - smoothstep(0.0h, _HorizonBlend, d.y);
                color = lerp(color, unity_FogColor.rgb, fogBand * 0.85h);

                // Discs drawn over the haze: a warm cartoon sun, a pale moon at night.
                half sunDisc = smoothstep(_SunDiscSize, _SunDiscSize + 0.00015, sunDot) * _GhSunDirection.w;
                color = lerp(color, _GhSunColor.rgb * 1.2h + 0.35h, sunDisc);
                float moonDot = dot(d, normalize(_GhMoonDirection.xyz));
                half moonDisc = smoothstep(_MoonDiscSize, _MoonDiscSize + 0.00012, moonDot) * _GhMoonDirection.w;
                color = lerp(color, half3(0.86h, 0.89h, 0.96h), moonDisc);

                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
