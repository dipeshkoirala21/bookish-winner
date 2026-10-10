#ifndef GHUMANTE_TOON_LIT_FORWARD_PASS_INCLUDED
#define GHUMANTE_TOON_LIT_FORWARD_PASS_INCLUDED

// The ForwardLit pass of Ghumante/ToonLit (include after ToonLitInput.hlsl and URP Lighting.hlsl).
// Albedo: vertex colour × procedural material texture (UV0 channel, object-space triplanar) × macro variation.
// Light: 3-band toon ramp with soft edges and cool shadow tint, baked vertex AO on ambient (and half on direct), a toon
// highlight per channel (white, or the surface colour for metal and gilt), glints on gilt and water, sky reflection on
// glass and water, the soft warm rim, fog.

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    half4 color : COLOR;
    float2 uv0 : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float4 positionWSFog : TEXCOORD0;     // xyz world position, w fog factor
    half4 normalWSAo : TEXCOORD1;         // xyz world normal, w baked AO
    half3 albedo : TEXCOORD2;
    float3 positionOS : TEXCOORD3;
    float3 normalOS : TEXCOORD4;
    nointerpolation float4 matA : TEXCOORD5; // 1 / tile, specular, gloss, sparkle
    nointerpolation float4 matB : TEXCOORD6; // metallic, reflect, macro, flow
    nointerpolation float channel : TEXCOORD7;
};

Varyings ToonVert(Attributes input)
{
    Varyings o = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    float3 positionWS = GhToonWorldPosition(input.positionOS.xyz);
    o.positionCS = TransformWorldToHClip(positionWS);
    o.positionWSFog = float4(positionWS, ComputeFogFactor(o.positionCS.z));
    half ao;
    float channel;
    GhChannelAndAo(input.uv0, channel, ao);
    o.normalWSAo = half4(TransformObjectToWorldNormal(input.normalOS), ao);
    o.albedo = GhVertexAlbedo(input.color);
    o.positionOS = input.positionOS.xyz;
    o.normalOS = input.normalOS;
    int c = (int)channel;
    o.matA = _GhChannelA[c];
    o.matB = _GhChannelB[c];
    o.channel = channel;
    return o;
}

// Integer hash of a glint cell to [0, 1) (stable for any coordinate, unlike sin- or frac-of-product hashes).
float GhGlintHash(float3 cell)
{
    uint3 q = (uint3)(int3)cell;
    uint h = (q.x * 1597334677u) ^ (q.y * 3812015801u) ^ (q.z * 2741598397u);
    h = (h ^ (h >> 16)) * 2246822519u;
    h ^= h >> 13;
    return (float)(h & 0x00FFFFFFu) * (1.0 / 16777216.0);
}

half4 ToonFrag(Varyings i) : SV_Target
{
    float3 positionWS = i.positionWSFog.xyz;
    half3 viewDir = GetWorldSpaceNormalizeViewDir(positionWS);
    float viewDistance = distance(positionWS, _WorldSpaceCameraPos.xyz);

    // Everything that needs screen-space derivatives (texture gradients, the glint footprint) runs before the band and
    // occluder clips: after a discard the derivatives of the pixels left in a 2 × 2 quad are undefined on Vulkan and
    // GLES (wrong mips and sparkle right in the dithered cross-fade and the occluder hole).
    half4 texel;
    half detail;
    half3 albedo = GhMaterialAlbedo(i.albedo, i.positionOS, i.normalOS, i.matA, i.matB, i.channel, viewDistance, texel, detail);
    float footprint = max(length(ddx(i.positionOS)), length(ddy(i.positionOS))); // object-space metres per pixel

    GhBandClip(positionWS, i.positionCS.xy);
    GhOccluderClip(positionWS, i.normalWSAo.xyz, i.positionCS.xy);

    half3 n = normalize(i.normalWSAo.xyz);
    half ao = lerp(1.0h, i.normalWSAo.w, _AoStrength);

    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    // The overload with the position applies URP's shadow-distance fade (as URP Lit does): beyond the shadow distance
    // the coordinate leaves the map (one cascade clamps to its edge texels, the no-op matrix past the last cascade
    // samples texel 0), so without the fade far terrain gets random shadows.
    Light light = GetMainLight(shadowCoord, positionWS, half4(1.0h, 1.0h, 1.0h, 1.0h));
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
    // Baked AO darkens the ambient fully and the direct light by half (corners stay readable in the sun).
    half3 color = albedo * (ambient * coolShade * ao + lightColor * ramp * lerp(1.0h, ao, 0.5h));

    // Toon highlight: a soft-edged disc whose size follows the channel's gloss; metal and gilt tint it.
    half spec = (half)i.matA.y * _SpecularStrength;
    UNITY_BRANCH
    if (spec > 0.001h)
    {
        half3 h = SafeNormalize(light.direction + viewDir);
        half nh = saturate(dot(n, h));
        half lobe = pow(nh, (half)max(i.matA.z, 1.0));
        half disc = smoothstep(0.42h, 0.52h, lobe) * 0.85h + lobe * 0.15h;
        half3 specColor = lerp(half3(1.0h, 1.0h, 1.0h), albedo * 2.0h, (half)i.matB.x);
        color += disc * spec * shadow * saturate(ndl * 4.0h) * specColor * lightColor;

        // Glints (gilt copper, sunlit water): sparse cells that twinkle as the view turns. A cell is at least two pixels
        // wide (1/64 m up close, the next power of two as the pixel footprint grows), so cells never shrink below a pixel
        // and fizz; glints fade out while two pixels grow from 1/8 to 1/4 m (≈ 58 to 117 m at 1080 p) and with the
        // textures' distance fade. The view term reshuffles the lit cells over about 3° of view change, not every
        // centimetre of camera bob.
        half glint = (half)(i.matA.w * _GhLookParams.z) * detail * (half)saturate(2.0 - 16.0 * footprint);
        if (glint > 0.001h)
        {
            float cell = exp2(max(-6.0, ceil(log2(max(2.0 * footprint, 1e-6)))));
            float cellHash = frac(GhGlintHash(floor(i.positionOS / cell)) + dot((float3)viewDir, float3(0.11, 0.21, 0.17)));
            half g = step(0.985h, (half)cellHash) * glint * saturate(ndl) * shadow * (0.5h + texel.r);
            color += g * lightColor * 2.0h;
        }

        // Sky reflection at grazing angles (glass, water, metal).
        half reflectivity = (half)i.matB.y;
        if (reflectivity > 0.001h)
        {
            half fresnel = pow(1.0h - saturate(dot(n, viewDir)), 3.0h);
            half3 sky = SampleSH(reflect(-viewDir, n));
            color += sky * reflectivity * (0.12h + 0.88h * fresnel) * ao;
        }
    }

    // Soft rim on the lit side, in the light's colour (golden at sunrise on the snow peaks).
    half rim = pow(saturate(1.0h - saturate(dot(n, viewDir))), _RimPower) * _RimStrength;
    rim *= saturate(ndl + 0.35h) * shadow;
    color += rim * _RimColor.rgb * lightColor;

    half fog = InitializeInputDataFog(float4(positionWS, 1.0), i.positionWSFog.w);
    color = MixFog(color, fog);
    return half4(color, 1.0h);
}

#endif
