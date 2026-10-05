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
    // W2: building band range (x inner, y outer, z fade width; metres from the band centre) and vertex wind
    // (x amplitude per unit height, y frequency Hz, zw horizontal direction).
    float4 _BandRange;
    float4 _Wind;
CBUFFER_END

// Per-instance tint for instanced dressing (trees, props): only in instanced variants with _INSTANCE_TINT, so the tile
// materials keep the SRP Batcher layout above. Vertex alpha is the tint mask (255 = tinted, 0 = keep the colour).
#if defined(UNITY_INSTANCING_ENABLED) && defined(_INSTANCE_TINT)
UNITY_INSTANCING_BUFFER_START(GhPerInstance)
    UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceTint)
UNITY_INSTANCING_BUFFER_END(GhPerInstance)
#define GH_INSTANCE_TINT UNITY_ACCESS_INSTANCED_PROP(GhPerInstance, _InstanceTint)
#else
#define GH_INSTANCE_TINT float4(1.0, 1.0, 1.0, 1.0)
#endif

// The horizontal centre the building bands are measured from (the camera), set by the world streamer each frame.
float4 _GhBandCentre;

#include "GhumanteCommon.hlsl"

// Vertex wind (W2_DESIGN 5.8: 0.3-0.6 Hz, amplitude growing with height): sways the object-space position along the
// wind direction by amplitude × y², phase from the instance's world position so neighbours do not move together.
float3 GhWind(float3 positionOS)
{
#if defined(_WIND)
    float3 originWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
    float phase = dot(originWS.xz, float2(0.071, 0.113));
    float t = _Time.y * _Wind.y * 6.2831853 + phase;
    float sway = (sin(t) + 0.35 * sin(2.3 * t + 1.7)) * _Wind.x * positionOS.y * positionOS.y;
    positionOS.xz += _Wind.zw * sway;
#endif
    return positionOS;
}

// The world position every pass draws at: wind, object to world, earth curvature, depth pull. One function, so the
// depth prepass and the colour pass produce the same depth.
float3 GhToonWorldPosition(float3 positionOS)
{
    float3 positionWS = TransformObjectToWorld(GhWind(positionOS));
    positionWS = GhEarthCurvature(positionWS, _Curvature);
    return GhViewPull(positionWS, _ViewPull);
}

// Building bands (W2_DESIGN 2.4): opaque dithered cross-fade over _BandRange.z at each edge. The fade-in uses the
// complementary threshold of the fade-out, so two adjacent bands cover every pixel exactly once. C# twin:
// Ghumante.World.Buildings.BandConfig.Opacity.
// 4x4 Bayer matrix.
static const float GhBayerTable[16] = { 0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0 };

float GhBayer4(float2 pixel)
{
    uint2 p = uint2(pixel) & 3u;
    return (GhBayerTable[p.x + p.y * 4u] + 0.5) / 16.0;
}

void GhBandClip(float3 positionWS, float2 pixel)
{
#if defined(_BAND_FADE)
    float d = distance(positionWS.xz, _GhBandCentre.xz);
    float h = 0.5 * _BandRange.z;
    float fadeIn = _BandRange.x <= 0.0 ? 1.0 : saturate((d - (_BandRange.x - h)) / max(_BandRange.z, 1e-3));
    float fadeOut = saturate(((_BandRange.y + h) - d) / max(_BandRange.z, 1e-3));
    float b = GhBayer4(pixel);
    clip(min(fadeIn - (1.0 - b), fadeOut - b));
#endif
}

// Shadow casters cut each band at the middle of its fades (no dither: shadow maps have their own resolution).
void GhBandClipHard(float3 positionWS)
{
#if defined(_BAND_FADE)
    float d = distance(positionWS.xz, _GhBandCentre.xz);
    float inner = _BandRange.x <= 0.0 ? -1.0 : _BandRange.x;
    clip(min(d - inner, _BandRange.y - d));
#endif
}

#endif
