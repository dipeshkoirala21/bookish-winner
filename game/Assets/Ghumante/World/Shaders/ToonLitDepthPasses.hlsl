#ifndef GHUMANTE_TOON_LIT_DEPTH_PASSES_INCLUDED
#define GHUMANTE_TOON_LIT_DEPTH_PASSES_INCLUDED

// ShadowCaster (define GH_SHADOW_CASTER_PASS and include URP Shadows.hlsl first), DepthOnly and DepthNormals passes of
// Ghumante/ToonLit. Depth passes apply the same band and occluder clips as the lit pass, so a depth prepass and the
// colour pass agree; shadow casters keep the occluders' shadows (only the band edge is cut).

#if defined(GH_SHADOW_CASTER_PASS)

// Set by URP's shadow passes (ShadowUtils.SetupShadowCasterConstantBuffer).
float3 _LightDirection;
float3 _LightPosition;

struct ShadowAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct ShadowVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
};

ShadowVaryings ShadowVert(ShadowAttributes input)
{
    ShadowVaryings o;
    UNITY_SETUP_INSTANCE_ID(input);
    // No curvature here: within the shadow distance (at most 180 m) the drop is under 3 mm, far below the shadow bias,
    // and the camera position is not what the light renders from.
    float3 positionWS = TransformObjectToWorld(GhWind(input.positionOS.xyz));
    o.positionWS = positionWS;
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

half4 ShadowFrag(ShadowVaryings input) : SV_TARGET
{
    GhBandClipHard(input.positionWS);
    return 0;
}

#else

struct DepthAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct DepthVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    half3 normalWS : TEXCOORD1;
};

DepthVaryings DepthVert(DepthAttributes input)
{
    DepthVaryings o;
    UNITY_SETUP_INSTANCE_ID(input);
    o.positionWS = GhToonWorldPosition(input.positionOS.xyz);
    o.positionCS = TransformWorldToHClip(o.positionWS);
    o.normalWS = TransformObjectToWorldNormal(input.normalOS);
    return o;
}

half DepthFrag(DepthVaryings input) : SV_TARGET
{
    GhBandClip(input.positionWS, input.positionCS.xy);
    GhOccluderClip(input.positionWS, input.positionCS.xy);
    return input.positionCS.z;
}

// Used when URP renders a normals texture (SSAO and similar features).
half4 DepthNormalsFrag(DepthVaryings input) : SV_TARGET
{
    GhBandClip(input.positionWS, input.positionCS.xy);
    GhOccluderClip(input.positionWS, input.positionCS.xy);
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

#endif

#endif
