#ifndef GHUMANTE_TOON_LIT_OUTLINE_PASS_INCLUDED
#define GHUMANTE_TOON_LIT_OUTLINE_PASS_INCLUDED

// The Outline pass of Ghumante/ToonLit (LightMode SRPDefaultUnlit, drawn by URP's opaque pass right after the lit pass;
// only in the LOD 300 SubShader, which ToonLook selects on Mid and High). Inverted hull: the back faces pushed out along
// the screen-space normal by a constant pixel width, culled front faces, so a dark line shows around silhouettes and
// in front of whatever lies behind. The width fades out with distance (_GhOutlineParams); where it is zero the hull stays
// where it is, pushed behind the surface it outlines (hidden by the depth test), and the fragment shader discards what
// is left below 0.05 px. Vertices are never moved one by one to a fixed point: a triangle crossing the fade-out radius
// would turn into a screen-wide sliver. Only instanced draws skip whole instances beyond the outline range (decided from
// the instance origin, so all vertices of an instance agree). Shares the band and occluder clips of the lit pass.
// Include after ToonLitInput.hlsl and URP Lighting.hlsl.

// Instances whose origin is this much beyond the outline's fade-out distance are dropped whole (the largest instanced
// objects, aircraft, reach about 32 m from their origin).
#define GH_OUTLINE_INSTANCE_MARGIN_M 32.0

struct OutlineAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    half4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct OutlineVaryings
{
    float4 positionCS : SV_POSITION;
    float4 positionWSFog : TEXCOORD0; // xyz world position, w fog factor
    half3 color : TEXCOORD1;
    float widthPx : TEXCOORD2;        // outline width at this point (pixels)
};

// Outline width in pixels at a distance (tier width × material width, faded out between _GhOutlineParams.y and .z).
float GhOutlineWidthPx(float distanceM)
{
    float fade = 1.0 - smoothstep(_GhOutlineParams.y, max(_GhOutlineParams.z, _GhOutlineParams.y + 1e-3), distanceM);
    return _GhOutlineParams.x * _OutlineWidth * fade * (_ScreenParams.y / 1080.0);
}

OutlineVaryings OutlineVert(OutlineAttributes input)
{
    OutlineVaryings o = (OutlineVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    float3 positionWS = GhToonWorldPosition(input.positionOS.xyz);
    float3 toCamera = _WorldSpaceCameraPos.xyz - positionWS;
    float distanceM = length(toCamera);
    float widthPx = GhOutlineWidthPx(distanceM);

    // Push the hull a little away from the camera so it never covers the surface it outlines (with zero width it stays
    // entirely behind it).
    positionWS -= toCamera * (min(0.02 + 0.004 * distanceM, 0.5) / max(distanceM, 1e-3));
    float4 positionCS = TransformWorldToHClip(positionWS);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    float2 normalCS = mul((float3x3)GetWorldToHClipMatrix(), normalWS).xy;
    float2 normalPx = normalCS * _ScreenParams.xy;
    float2 dir = normalPx / max(length(normalPx), 1e-5);
    positionCS.xy += dir * (widthPx * 2.0 / _ScreenParams.xy) * positionCS.w;
#if defined(UNITY_INSTANCING_ENABLED)
    // A whole instance beyond the outline range: every vertex of it goes to the same point outside the clip volume, so
    // all its triangles are culled together (no raster cost, no partial triangles).
    float originDistance = distance(TransformObjectToWorld(float3(0.0, 0.0, 0.0)), _WorldSpaceCameraPos.xyz);
    if (originDistance > _GhOutlineParams.z + GH_OUTLINE_INSTANCE_MARGIN_M) positionCS = float4(2.0, 2.0, 2.0, 1.0);
#endif
    o.positionCS = positionCS;
    o.positionWSFog = float4(positionWS, ComputeFogFactor(positionCS.z));
    o.widthPx = widthPx;

    // A darker shade of the surface colour, lit roughly like the scene (dark at night).
    half3 level = _MainLightColor.rgb * 0.55h + SampleSH(half3(0.0h, 1.0h, 0.0h)) * 0.6h;
    o.color = GhVertexAlbedo(input.color) * (half)_GhOutlineParams.w * level;
    return o;
}

half4 OutlineFrag(OutlineVaryings i) : SV_Target
{
    // Where the outline has faded out (or a triangle reaches into the fade-out), the hull leaves no sub-pixel slivers.
    clip(i.widthPx - 0.05);
    float3 positionWS = i.positionWSFog.xyz;
    GhBandClip(positionWS, i.positionCS.xy);
    // Ground layers (the only ones with a normal-dependent fade) never draw the outline: no normal needed here.
    GhOccluderClip(positionWS, float3(0.0, 0.0, 0.0), i.positionCS.xy);
    half fog = InitializeInputDataFog(float4(positionWS, 1.0), i.positionWSFog.w);
    return half4(MixFog(i.color, fog), 1.0h);
}

#endif
