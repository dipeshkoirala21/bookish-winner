// Ghumante/Skyline: the distant mountains (owner playtest #12; ARCHITECTURE.md 8 "Skyline"; ROADMAP 1.9). Draws the ring
// that Ghumante.Core.Horizon.HorizonMesher builds (curtains hung from the true silhouettes of the rim hills and the
// Himalaya) and Ghumante.World.Sky.SkylineRing keeps round the camera.
//
// * Geometry: vertices are real positions (absolute altitude in y); the earth curvature lowers them like every world
//   shader (GhEarthCurvature), then each vertex is moved along its view ray to the handover distance
//   (_GhSkylineParams.x, a little farther per band so nearer bands win), where it is drawn with depth. It is drawn
//   after the opaque world (queue Geometry+450): streamed terrain nearer than the handover stays in front, the coarse
//   horizon terrain farther than it is covered by the true silhouettes, and the sky fills what is left.
// * Look (C# twin: Ghumante.Core.Horizon.HorizonLook, same numbers): ground colour by altitude (hill forest, conifer,
//   alpine, rock, snow above a snow line that varies from face to face and rises on steep rock), rock ribs and gullies
//   through the snow, snow flutings and ribs that bend the normal (faded out where a rib would be under three pixels),
//   three-band toon light from the main light (sun, or moon at night; summits catch the sun before sunrise and after
//   sunset, the alpenglow) plus the sky ambient, then the valley haze (scene fog
//   colour, only over the part of the sight line below the haze top) and clear-air aerial perspective into the sky
//   right behind the range (Ghumante/SkyGradient's gradient, glow and fog band from WorldSky's globals; longer through
//   the thinner air of high sight lines), so a far range never turns brighter than the sky it stands against.
//
// Vertex data: normal = flat facet normal (one per silhouette segment: each peak has a lit and a shaded face); colour
// R = rock exposure, G = band index (0 nearest); UV0.x = rib coordinate in metres (bearing from a fixed anchor times a
// radius per band: fixed to the ground between rebuilds).
Shader "Ghumante/Skyline"
{
    Properties
    {
        _RibStrength("Rib strength", Range(0, 2)) = 0.6
        _SnowLine("Snow line (m)", Float) = 5000
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Geometry+450"
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Skyline"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex SkylineVert
            #pragma fragment SkylineFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Ghumante/World/Shaders/GhumanteCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _RibStrength;
                float _SnowLine;
            CBUFFER_END

            // SkylineRing: x handover distance (m), y eye altitude (m), z scene fog density (exp2, per m),
            // w altitude of scene y = 0 (the floating origin's height).
            float4 _GhSkylineParams;
            // WorldSky (linear colours): the sky dome of Ghumante/SkyGradient, its sun and golden-hour glow; x of
            // _GhSkyAir = the air's own haze density (the palette's, without the floor that hides the end of the
            // streamed world; 0 = unknown).
            half4 _GhSkyZenith;
            half4 _GhSkyHorizon;
            half4 _GhSkyGround;
            float4 _GhSunDirection;
            half4 _GhSunColor;
            half4 _GhSkyParams;
            float4 _GhSkyAir;

            // HorizonLook constants.
            #define GH_HAZE_TOP_M 3000.0
            #define GH_CLEAR_AIR_M 90000.0
            #define GH_AIR_SCALE_HEIGHT_M 8000.0
            #define GH_RIB_PERIOD_M 150.0
            #define GH_GULLY_SPREAD_M 700.0
            #define GH_SKY_HORIZON_BLEND 0.08

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float4 info : TEXCOORD1;     // x altitude (m), y horizontal distance (m), z metres along the curtain, w rock
                float3 tangentWS : TEXCOORD2; // along the curtain (horizontal)
                float3 viewWS : TEXCOORD3;    // from the camera to the drawn point (unnormalised)
            };

            Varyings SkylineVert(Attributes v)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float2 rel = posWS.xz - _WorldSpaceCameraPos.xz;
                float dist = length(rel);
                float3 curved = GhEarthCurvature(posWS, 1.0);
                float3 ray = curved - _WorldSpaceCameraPos.xyz;
                float band = v.color.g * 255.0;
                float radius = _GhSkylineParams.x * (1.0 + 0.004 * band);
                float3 drawWS = _WorldSpaceCameraPos.xyz + ray * (radius / max(length(ray), 1.0));
                o.positionCS = TransformWorldToHClip(drawWS);
                o.viewWS = drawWS - _WorldSpaceCameraPos.xyz;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                float2 flatDir = rel / max(dist, 1.0);
                o.tangentWS = float3(flatDir.y, 0.0, -flatDir.x);
                o.info = float4(posWS.y + _GhSkylineParams.w, dist, v.uv.x, v.color.r);
                return o;
            }

            // "Hash without sine" (HorizonLook.Hash): stable on every GPU.
            float GhSkylineHash(float2 p)
            {
                float3 p3 = frac(float3(p.x, p.y, p.x) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float GhSkylineNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = p - i;
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = GhSkylineHash(i), b = GhSkylineHash(i + float2(1, 0)), c = GhSkylineHash(i + float2(0, 1)), d = GhSkylineHash(i + float2(1, 1));
                return a + (b - a) * u.x + (c - a) * u.y + (a - b - c + d) * u.x * u.y;
            }

            // HorizonLook.Variation: the snow line moves from face to face.
            float GhVariation(float along)
            {
                return GhSkylineNoise(float2(along / 1800.0, 0.37));
            }

            // HorizonLook.Streak: fine ribs over larger slanting rock patches; the fine part fades with detail.
            float GhStreak(float along, float altitude, float detail)
            {
                float coarse = GhSkylineNoise(float2(along / 520.0 + altitude / 1200.0, altitude / 650.0));
                float fine = GhSkylineNoise(float2(along / GH_RIB_PERIOD_M + altitude / 700.0, altitude / 300.0));
                return lerp(coarse, 0.65 * fine + 0.35 * coarse, detail);
            }

            half3 GhLin(half3 srgb)
            {
            #if defined(UNITY_COLORSPACE_GAMMA)
                return srgb;
            #else
                return SRGBToLinear(srgb);
            #endif
            }

            half3 GhAlbedo(float alt, half rock, float along, float streak)
            {
                float snow = _SnowLine + (GhVariation(along) - 0.5) * 600.0 + rock * 400.0;
                half3 forest = GhLin(half3(0.353, 0.494, 0.227));  // #5A7E3A (BiomePalette HillForest, autumn)
                half3 conifer = GhLin(half3(0.353, 0.431, 0.251)); // #5A6E40 (SubalpineForest, autumn)
                half3 alpine = GhLin(half3(0.333, 0.329, 0.290));  // #55544A
                half3 rockC = GhLin(half3(0.298, 0.322, 0.384));   // #4C5262
                half3 snowC = GhLin(half3(0.957, 0.965, 0.973));   // #F4F6F8
                half3 c = lerp(forest, conifer, smoothstep(2600.0, 2900.0, alt));
                c = lerp(c, alpine, smoothstep(3600.0, 4100.0, alt));
                c = lerp(c, rockC, smoothstep(snow - 900.0, snow - 400.0, alt));
                // Snow reaches down the gullies and leaves the ribs bare; bare rock breaks it on steep ground.
                float snowHere = snow + (streak - 0.5) * GH_GULLY_SPREAD_M;
                // Bare rock breaks the snow on the lower faces; the top kilometre or so below a summit is mostly white.
                half bare = smoothstep(0.58, 0.64, streak) * (0.55 + 0.45 * rock) * (1.0 - 0.75 * smoothstep(snow + 600.0, snow + 1800.0, alt));
                half s = smoothstep(snowHere - 80.0, snowHere + 80.0, alt) * (1.0 - bare);
                return lerp(c, snowC, s);
            }

            // HorizonLook.SkyBehind: Ghumante/SkyGradient without the discs and halo (gradient, glow, fog band); the horizon
            // colour while WorldSky has not set the dome (all zero).
            half3 GhSkyBehind(float3 d)
            {
                if (dot(_GhSkyZenith.rgb, half3(1.0h, 1.0h, 1.0h)) <= 0.0h) return _GhSkyHorizon.rgb;
                half up = saturate(d.y);
                half down = saturate(-d.y);
                half3 color = lerp(_GhSkyHorizon.rgb, _GhSkyZenith.rgb, sqrt(up));
                color = lerp(color, _GhSkyGround.rgb, saturate(down * 6.0h));
                float2 flatD = normalize(d.xz + float2(1e-5, 0.0));
                float2 flatSun = normalize(_GhSunDirection.xz + float2(1e-5, 0.0));
                half glow = pow(saturate(dot(flatD, flatSun) * 0.5 + 0.5), 6.0) * pow(1.0h - up, 4.0h) * (1.0h - saturate(down * 8.0h));
                color += _GhSunColor.rgb * glow * _GhSkyParams.x * 0.55h;
                half band = 1.0h - smoothstep(0.0h, GH_SKY_HORIZON_BLEND, d.y);
                return lerp(color, unity_FogColor.rgb, band * 0.85h);
            }

            half4 SkylineFrag(Varyings i) : SV_Target
            {
                float alt = i.info.x, dist = i.info.y, along = i.info.z;
                half rock = i.info.w;
                // HorizonLook.Detail: all of the fine ribs from 6 pixels per rib, none under 3.
                float detail = saturate((GH_RIB_PERIOD_M / max(fwidth(along), 1e-3) - 3.0) / 3.0);
                float streak = GhStreak(along, alt, detail);
                // Ribs: bend the normal across the curtain by the slope of the rib noise.
                float ribSlope = (GhStreak(along + 20.0, alt, detail) - GhStreak(along - 20.0, alt, detail)) / 40.0 * GH_RIB_PERIOD_M;
                float3 n = normalize(normalize(i.normalWS) + i.tangentWS * ribSlope * _RibStrength);
                half3 albedo = GhAlbedo(alt, rock, along, streak);

                Light main = GetMainLight();
                // HorizonLook.Ramp: band edges 0.02 (ToonLit's; crisp, softness 0.02) and 0.30 (softness 0.05), mid 0.78;
                // dark faces get only the sky light.
                half ndl = dot(n, main.direction);
                half lit = 0.78h * smoothstep(0.0h, 0.04h, ndl) + 0.22h * smoothstep(0.25h, 0.35h, ndl);
                half3 ambient = SampleSH(n);
                // HorizonLook.DirectLight: while the sun is the light, a summit gets the sun at the elevation it sees over
                // the curve of the earth (alpenglow before sunrise and after sunset); by day this equals main.color.
                float3 sunDir = normalize(_GhSunDirection.xyz + float3(0.0, 1e-6, 0.0));
                float sunElev = degrees(asin(clamp(sunDir.y, -1.0, 1.0)));
                float dip = degrees(sqrt(4.0 * max(alt, 0.0) * GH_INV_TWO_R_EFF)) - degrees(sqrt(4.0 * max(_GhSkylineParams.y, 0.0) * GH_INV_TWO_R_EFF));
                float seen = sunElev + dip;
                half sunI = smoothstep(-1.0, 4.0, seen) * (0.7 + 0.35 * smoothstep(4.0, 35.0, seen));
                half3 direct = lerp(main.color, _GhSunColor.rgb * sunI, step(0.99, dot(main.direction, sunDir)));
                half3 c = albedo * (ambient * 0.55h + direct * (lit * 0.85h));

                // Valley haze over the part of the sight line below the haze top (the scene fog for lines that stay in
                // it, the air's own density for lines that rise above it), then clear-air perspective.
                float eye = _GhSkylineParams.y;
                float lo = min(eye, alt), hi = max(eye, alt);
                float below = hi <= GH_HAZE_TOP_M ? 1.0 : (lo >= GH_HAZE_TOP_M ? 0.0 : (GH_HAZE_TOP_M - lo) / max(1.0, hi - lo));
                float air = _GhSkyAir.x > 0.0 ? _GhSkyAir.x : _GhSkylineParams.z;
                float t = (air + (_GhSkylineParams.z - air) * below * below) * dist * below;
                half haze = exp(-t * t);
                c = lerp(unity_FogColor.rgb, c, haze);
                float airLength = GH_CLEAR_AIR_M * exp(0.5 * (max(eye, 0.0) + max(alt, 0.0)) / GH_AIR_SCALE_HEIGHT_M);
                half clearAir = exp(-dist / airLength);
                c = lerp(GhSkyBehind(normalize(i.viewWS)), c, clearAir);
                return half4(c, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
