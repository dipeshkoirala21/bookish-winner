#ifndef GHUMANTE_TOON_LIT_INPUT_INCLUDED
#define GHUMANTE_TOON_LIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// Every per-material property lives in UnityPerMaterial, identical in all passes: SRP Batcher compatible.
// (_Cull, _OffsetFactor and _OffsetUnits are render-state only and never read here.)
CBUFFER_START(UnityPerMaterial)
    half4 _BaseColor;
    half4 _ShadowTint;
    half4 _RampThresholds;
    half4 _RampLevels;
    half4 _RimColor;
    half _RimPower;
    half _RimStrength;
    half _AmbientStrength;
    float _Curvature;
    float _ViewPull;
CBUFFER_END

#include "GhumanteCommon.hlsl"

// The world position every pass draws at: object to world, earth curvature, depth pull. One function, so the
// depth prepass and the colour pass produce the same depth.
float3 GhToonWorldPosition(float3 positionOS)
{
    float3 positionWS = TransformObjectToWorld(positionOS);
    positionWS = GhEarthCurvature(positionWS, _Curvature);
    return GhViewPull(positionWS, _ViewPull);
}

#endif
