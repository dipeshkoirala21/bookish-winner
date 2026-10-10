#ifndef GHUMANTE_TOON_LIT_OUTLINE_PASS_INCLUDED
#define GHUMANTE_TOON_LIT_OUTLINE_PASS_INCLUDED

// The Outline pass of Ghumante/ToonLit (LightMode SRPDefaultUnlit, drawn by URP's opaque pass right after the lit pass;
// only in the LOD 300 SubShader, which ToonLook selects on Mid and High). Inverted hull: the back faces pushed out along
// the screen-space normal by a constant pixel width, culled front faces, so a dark line shows around silhouettes and
// in front of whatever lies behind. The width fades out with distance (_GhOutlineParams), the hull collapses outside
// the clip volume when the width is zero (no raster cost), and it shares the band and occluder clips of the lit pass.
// Include after ToonLitInput.hlsl and URP Lighting.hlsl.

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
};

OutlineVaryings OutlineVert(OutlineAttributes input)
{
    OutlineVaryings o = (OutlineVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    float3 positionWS = GhToonWorldPosition(input.positionOS.xyz);
    float3 toCamera = _WorldSpaceCameraPos.xyz - positionWS;
    float distanceM = length(toCamera);
    float fade = 1.0 - smoothstep(_GhOutlineParams.y, max(_GhOutlineParams.z, _GhOutlineParams.y + 1e-3), distanceM);
    float widthPx = _GhOutlineParams.x * _OutlineWidth * fade * (_ScreenParams.y / 1080.0);

    // Push the hull a little away from the camera so it never covers the surface it outlines.
    positionWS -= toCamera * (min(0.02 + 0.004 * distanceM, 0.5) / max(distanceM, 1e-3));
    float4 positionCS = TransformWorldToHClip(positionWS);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    float2 normalCS = mul((float3x3)GetWorldToHClipMatrix(), normalWS).xy;
    float2 normalPx = normalCS * _ScreenParams.xy;
    float2 dir = normalPx / max(length(normalPx), 1e-5);
    positionCS.xy += dir * (widthPx * 2.0 / _ScreenParams.xy) * positionCS.w;
    // Zero width: collapse the vertex outside the clip volume (every triangle of the hull is culled).
    positionCS = widthPx > 0.05 ? positionCS : float4(2.0, 2.0, 2.0, 1.0);
    o.positionCS = positionCS;
    o.positionWSFog = float4(positionWS, ComputeFogFactor(positionCS.z));

    // A darker shade of the surface colour, lit roughly like the scene (dark at night).
    half3 level = _MainLightColor.rgb * 0.55h + SampleSH(half3(0.0h, 1.0h, 0.0h)) * 0.6h;
    o.color = GhVertexAlbedo(input.color) * (half)_GhOutlineParams.w * level;
    return o;
}

half4 OutlineFrag(OutlineVaryings i) : SV_Target
{
    float3 positionWS = i.positionWSFog.xyz;
    GhBandClip(positionWS, i.positionCS.xy);
    GhOccluderClip(positionWS, i.positionCS.xy);
    half fog = InitializeInputDataFog(float4(positionWS, 1.0), i.positionWSFog.w);
    return half4(MixFog(i.color, fog), 1.0h);
}

#endif
