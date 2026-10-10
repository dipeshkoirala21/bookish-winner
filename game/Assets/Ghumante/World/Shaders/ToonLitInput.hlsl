#ifndef GHUMANTE_TOON_LIT_INPUT_INCLUDED
#define GHUMANTE_TOON_LIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// Every per-material property lives in UnityPerMaterial, identical in all passes: SRP Batcher compatible.
// (_Cull, _OffsetFactor, _OffsetUnits and the keyword toggles are render state or keywords only and never read here.)
// C# twin of the layout: Ghumante.World.Rendering.ToonLitLayout (tests check both against this block).
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
    // W2 detail pass (World/README.md "Look"): procedural texture strength, baked vertex AO strength, highlight
    // strength, outline width (× the tier's width; 0 = none) and the occluder fade switch (mirrors _OCCLUDER_FADE).
    half _DetailStrength;
    half _AoStrength;
    half _SpecularStrength;
    half _OutlineWidth;
    half _OccluderFade;
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

// ---- Look globals (Ghumante.World.Rendering.ToonLook sets them; all zero = the W1 flat look, no outline, no fade) ----

// The procedural material textures, one slice per MaterialChannel (Core.Synth.Textures.MaterialTextures): sRGB rgb,
// alpha = tint weight. Slice 0 holds the macro variation.
TEXTURE2D_ARRAY(_GhMaterialTex);
SAMPLER(sampler_GhMaterialTex);

// Per channel (MaterialLooks.FillShaderArrays): A = (1 / tile m, specular, gloss, sparkle), B = (metallic, reflect,
// macro, flow m/s). GH_CHANNEL_ARRAY_SIZE = MaterialLooks.ArraySize.
#define GH_CHANNEL_ARRAY_SIZE 32
float4 _GhChannelA[GH_CHANNEL_ARRAY_SIZE];
float4 _GhChannelB[GH_CHANNEL_ARRAY_SIZE];

// x: 1 = blended triplanar (3 samples), 0 = dominant axis only (1 sample, Low); y: macro variation strength;
// z: glints on (1) or off; w: distance (m) at which the textures have faded to the flat vertex colour (0 = off).
float4 _GhLookParams;

// Outline (inverted hull, LOD 300 SubShader only): x width in pixels at 1080 p, y fade start (m), z fade end (m),
// w darkness (outline colour = albedo × w × light level).
float4 _GhOutlineParams;

// Occluder fade (C# twin: Ghumante.World.Rendering.OccluderFade.Opacity): a capsule from the camera (A.xyz) to the
// player (B.xyz). A.w radius at the camera end, C.w radius at the player end, B.w 1 = on, C.x soft edge (m),
// C.y solid zone before the player (m), C.z opacity left inside (screen-door).
float4 _GhOccluderA;
float4 _GhOccluderB;
float4 _GhOccluderC;

// MaterialChannel values the shader treats specially (Core/Meshing/MaterialChannel.cs).
#define GH_CHANNEL_PLAIN 0
// Metres per repeat of the macro layer (MaterialLooks.MacroTileM).
#define GH_MACRO_TILE_M 23.0

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

// The vertex albedo (linear): sRGB vertex colour, the per-instance tint on alpha-masked parts, the material tint.
half3 GhVertexAlbedo(half4 color)
{
    half3 albedo = GhVertexColorToLinear(color.rgb);
#if defined(_INSTANCE_TINT)
    // Vertex alpha masks the tinted parts (crowns, bodies); the tint is an sRGB palette colour.
    albedo = lerp(albedo, albedo * GhVertexColorToLinear((half3)GH_INSTANCE_TINT.rgb), color.a);
#endif
    return albedo * _BaseColor.rgb;
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

// Occluder fade (W2 detail pass decision: houses never block the view): the opacity of a world position against the
// camera-to-player capsule. 1 outside the capsule, _GhOccluderC.z deep inside it, soft over _GhOccluderC.x at the
// side and at both ends; the last _GhOccluderC.y metres before the player stay solid. C# twin: OccluderFade.Opacity.
float GhOccluderOpacity(float3 positionWS)
{
    float3 a = _GhOccluderA.xyz;
    float3 ab = _GhOccluderB.xyz - a;
    float len2 = max(dot(ab, ab), 1e-4);
    float len = sqrt(len2);
    float t = dot(positionWS - a, ab) / len2;
    float soft = max(_GhOccluderC.x, 1e-3);
    float along = saturate(t * len / soft) * saturate((len - _GhOccluderC.y - t * len) / soft);
    float radius = lerp(_GhOccluderA.w, _GhOccluderC.w, saturate(t));
    float d = distance(positionWS, a + ab * saturate(t));
    float inside = saturate((radius - d) / soft);
    float fade = along * inside * _GhOccluderB.w;
    return 1.0 - fade * (1.0 - _GhOccluderC.z);
}

// Screen-door clip of the occluder fade (opaque, no sorting); only materials with _OCCLUDER_FADE pay for it.
void GhOccluderClip(float3 positionWS, float2 pixel)
{
#if defined(_OCCLUDER_FADE)
    UNITY_BRANCH
    if (_GhOccluderB.w > 0.5)
    {
        clip(GhOccluderOpacity(positionWS) - GhBayer4(pixel));
    }
#endif
}

// ---- Procedural material sampling ----

// Material channel and baked AO from UV0 (docs/W2_DETAIL_CONTRACT.md §5: u = channel, v = AO). A mesh without UV0 reads
// (0, 0): Plain with no AO (the W1 look), so a Plain vertex with exactly zero AO counts as open.
void GhChannelAndAo(float2 uv0, out float channel, out half ao)
{
    channel = floor(uv0.x + 0.5);
    channel = (channel >= GH_CHANNEL_ARRAY_SIZE || channel < 0.0) ? GH_CHANNEL_PLAIN : channel;
    ao = (channel < 0.5 && uv0.y <= 0.0) ? 1.0h : (half)saturate(uv0.y);
}

// Triplanar weights from the object-space normal, sharpened (fourth power) so blends stay narrow.
float3 GhTriplanarWeights(float3 normalOS)
{
    float3 w = abs(normalOS);
    w *= w;
    w *= w;
    return w / max(w.x + w.y + w.z, 1e-4);
}

// One sample from the projection the normal faces most (x: zy, y: xz, z: xy). Explicit gradients, so it is safe in
// branches.
half4 GhSampleDominant(float3 p, float3 w, float3 dpdx, float3 dpdy, float slice)
{
    float2 uv = p.xz;
    float2 gx = dpdx.xz;
    float2 gy = dpdy.xz;
    if (w.x > w.y && w.x > w.z)
    {
        uv = p.zy;
        gx = dpdx.zy;
        gy = dpdy.zy;
    }
    else if (w.z > w.y)
    {
        uv = p.xy;
        gx = dpdx.xy;
        gy = dpdy.xy;
    }
    return SAMPLE_TEXTURE2D_ARRAY_GRAD(_GhMaterialTex, sampler_GhMaterialTex, uv, slice, gx, gy);
}

// Blended triplanar (Mid, High) or the dominant projection only (Low, _GhLookParams.x = 0).
half4 GhSampleTriplanar(float3 p, float3 w, float3 dpdx, float3 dpdy, float slice)
{
    half4 result;
    UNITY_BRANCH
    if (_GhLookParams.x < 0.5)
    {
        result = GhSampleDominant(p, w, dpdx, dpdy, slice);
    }
    else
    {
        half4 tx = SAMPLE_TEXTURE2D_ARRAY_GRAD(_GhMaterialTex, sampler_GhMaterialTex, p.zy, slice, dpdx.zy, dpdy.zy);
        half4 ty = SAMPLE_TEXTURE2D_ARRAY_GRAD(_GhMaterialTex, sampler_GhMaterialTex, p.xz, slice, dpdx.xz, dpdy.xz);
        half4 tz = SAMPLE_TEXTURE2D_ARRAY_GRAD(_GhMaterialTex, sampler_GhMaterialTex, p.xy, slice, dpdx.xy, dpdy.xy);
        result = tx * (half)w.x + ty * (half)w.y + tz * (half)w.z;
    }
    return result;
}

// The textured albedo of a surface: the channel's texture (object-space triplanar at the channel's scale, drifting for
// water) applied to the vertex tint as albedo = lerp(rgb, tint × rgb × 2, a), the macro layer on top, faded to the
// flat tint with distance. Plain surfaces skip the sampling. texel returns the blended sample (glints read it).
half3 GhMaterialAlbedo(half3 tint, float3 positionOS, float3 normalOS, float4 matA, float4 matB, float channel,
                       float viewDistance, out half4 texel)
{
    texel = half4(0.5h, 0.5h, 0.5h, 1.0h);
    float3 p = positionOS * matA.x;
    p.x += _Time.y * matB.w * matA.x;
    float3 m = positionOS * (1.0 / GH_MACRO_TILE_M);
    // Gradients outside the branch: the channel is flat per triangle, so the branch diverges only at channel borders.
    float3 dpdx = ddx(p);
    float3 dpdy = ddy(p);
    float3 mdx = ddx(m);
    float3 mdy = ddy(m);
    half detail = (half)(_DetailStrength * saturate((_GhLookParams.w - viewDistance) / max(0.25 * _GhLookParams.w, 1e-3)));
    half3 albedo = tint;
    UNITY_BRANCH
    if (channel > 0.5 && detail > 0.002h)
    {
        float3 w = GhTriplanarWeights(normalOS);
        texel = GhSampleTriplanar(p, w, dpdx, dpdy, channel);
        half3 textured = lerp(texel.rgb, tint * texel.rgb * 2.0h, texel.a);
        half macro = GhSampleDominant(m, w, mdx, mdy, GH_CHANNEL_PLAIN).r;
        textured *= lerp(1.0h, macro * 2.0h, (half)(matB.z * _GhLookParams.y));
        albedo = lerp(tint, textured, detail);
    }
    return albedo;
}

#endif
