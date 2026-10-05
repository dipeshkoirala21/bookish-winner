#ifndef GHUMANTE_COMMON_INCLUDED
#define GHUMANTE_COMMON_INCLUDED

// Shared helpers of the Ghumante world shaders. Include after
// Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl (needs _WorldSpaceCameraPos).

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

// Earth curvature (ARCHITECTURE.md 5.3): 1 / (2 R_eff), R_eff = 7/6 * 6 371 000 m (standard refraction).
// C# twin: Ghumante.World.Rendering.EarthCurvature.InverseTwoEffectiveRadius.
#define GH_INV_TWO_R_EFF 6.7269e-8

// Lowers a world position by d^2 / (2 R_eff), d its horizontal distance to the camera: about 0.07 m at 1 km,
// 168 m at 50 km, 1.7 km at 160 km, so far ridges sink below the horizon as they really do.
// scale: 1 = real curvature, 0 = flat.
float3 GhEarthCurvature(float3 positionWS, float scale)
{
    float2 d = positionWS.xz - _WorldSpaceCameraPos.xz;
    positionWS.y -= dot(d, d) * (GH_INV_TWO_R_EFF * scale);
    return positionWS;
}

// Moves a world position towards the camera by pull * distance. The point stays on the same view ray, so only its
// depth changes: overlays (roads, water) win the depth test against the terrain under them at any distance and on
// any depth buffer format, without the slope-dependent artefacts of a polygon offset alone.
float3 GhViewPull(float3 positionWS, float pull)
{
    return positionWS + (_WorldSpaceCameraPos.xyz - positionWS) * pull;
}

// Mesh vertex colours carry sRGB palette values (Core.Meshing palettes); lighting happens in linear space.
half3 GhVertexColorToLinear(half3 c)
{
#if defined(UNITY_COLORSPACE_GAMMA)
    return c;
#else
    return SRGBToLinear(c);
#endif
}

#endif
