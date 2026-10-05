// Ghumante/ToonLit: the world's cartoon surface (ARCHITECTURE.md 8): vertex-colour albedo (sRGB palette colours from
// the Core meshers), the main light through a 3-band toon ramp with soft edges and cool shadow tint, a soft rim on
// the lit side (warm at sunrise: the alpenglow), SH ambient, main-light shadows, URP fog, and the earth-curvature
// vertex drop. Terrain, buildings, roads and areas all use it; roads and areas add a depth pull (_ViewPull) and a
// polygon offset so they never z-fight the ground. URP 17.3 Forward, SRP Batcher compatible (UnityPerMaterial in
// ToonLitInput.hlsl), hand-written HLSL.
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
            #pragma target 2.0
            #pragma vertex ToonVert
            #pragma fragment ToonFrag

            // Main light shadows (1 cascade on Low/Mid, 2 on High) and URP's soft-shadow quality keywords.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #include "ToonLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 albedo : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings ToonVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                float3 positionWS = GhToonWorldPosition(input.positionOS.xyz);
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.albedo = GhVertexColorToLinear(input.color.rgb) * _BaseColor.rgb;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 ToonFrag(Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light light = GetMainLight(shadowCoord);
                half shadow = light.shadowAttenuation;

                // Three bands (dark, mid, lit) with soft edges; cast shadows pull down to the dark band.
                half ndl = dot(n, light.direction);
                half soft = max(_RampThresholds.z, 0.001h);
                half b1 = smoothstep(_RampThresholds.x - soft, _RampThresholds.x + soft, ndl);
                half b2 = smoothstep(_RampThresholds.y - soft, _RampThresholds.y + soft, ndl);
                half ramp = lerp(_RampLevels.x, lerp(_RampLevels.y, _RampLevels.z, b2), b1);
                ramp = min(ramp, lerp(_RampLevels.x, 1.0h, shadow));

                half3 lightColor = light.color * light.distanceAttenuation;
                half3 ambient = SampleSH(n) * _AmbientStrength;
                half3 coolShade = lerp(_ShadowTint.rgb, half3(1.0h, 1.0h, 1.0h), saturate(ramp));
                half3 color = i.albedo * (ambient * coolShade + lightColor * ramp);

                // Soft rim on the lit side, in the light's colour (golden at sunrise on the snow peaks).
                half3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half rim = pow(saturate(1.0h - saturate(dot(n, viewDir))), _RimPower) * _RimStrength;
                rim *= saturate(ndl + 0.35h) * shadow;
                color += rim * _RimColor.rgb * lightColor;

                half fog = InitializeInputDataFog(float4(i.positionWS, 1.0), i.fogFactor);
                color = MixFog(color, fog);
                return half4(color, 1.0h);
            }
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
            #pragma target 2.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "ToonLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // Set by URP's shadow passes (ShadowUtils.SetupShadowCasterConstantBuffer).
            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings o;
                // No curvature here: within the shadow distance (at most 180 m) the drop is under 3 mm, far below the
                // shadow bias, and the camera position is not what the light renders from.
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                o.positionCS = ApplyShadowClamping(positionCS);
                return o;
            }

            half4 ShadowFrag(Varyings input) : SV_TARGET
            {
                return 0;
            }
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
            #pragma target 2.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #include "ToonLitInput.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformWorldToHClip(GhToonWorldPosition(input.positionOS.xyz));
                return o;
            }

            half DepthFrag(Varyings input) : SV_TARGET
            {
                return input.positionCS.z;
            }
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
            #pragma target 2.0
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            #include "ToonLitInput.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
            };

            Varyings DepthNormalsVert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformWorldToHClip(GhToonWorldPosition(input.positionOS.xyz));
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 DepthNormalsFrag(Varyings input) : SV_TARGET
            {
            #if defined(_GBUFFER_NORMALS_OCT)
                float3 normalWS = normalize(input.normalWS);
                float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
                return half4(packedNormalWS, 0.0);
            #else
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            #endif
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
