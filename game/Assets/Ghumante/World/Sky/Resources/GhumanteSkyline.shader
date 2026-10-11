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
//   two-tone light from the main light (sun, or moon at night) plus the sky ambient, then the valley haze (scene fog
//   colour, only over the part of the sight line below the haze top) and clear-air aerial perspective towards the
//   horizon colour of WorldSky (longer through the thinner air of high sight lines).
//
// Vertex data: normal = face pseudo-normal; colour R = rock exposure, G = band index (0 nearest); UV0.x = rib
// coordinate in metres (bearing from a fixed anchor times a radius per band: fixed to the ground between rebuilds).
Shader "Ghumante/Skyline"
{
    Properties
    {
        _RibStrength("Rib strength", Range(0, 2)) = 0.9
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
            // WorldSky: sky colour at the horizon (linear); x of _GhSkyAir = the air's own haze density (the palette's,
            // without the floor that hides the end of the streamed world; 0 = unknown).
            half4 _GhSkyHorizon;
            float4 _GhSkyAir;

            // HorizonLook constants.
            #define GH_HAZE_TOP_M 3000.0
            #define GH_CLEAR_AIR_M 75000.0
            #define GH_AIR_SCALE_HEIGHT_M 8000.0
            #define GH_RIB_PERIOD_M 150.0

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
                float coarse = GhSkylineNoise(float2(along / 520.0 + altitude / 1800.0, altitude / 2500.0));
                float fine = GhSkylineNoise(float2(along / GH_RIB_PERIOD_M, altitude / 450.0));
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
                half3 forest = GhLin(half3(0.231, 0.337, 0.212));  // #3B5636
                half3 conifer = GhLin(half3(0.184, 0.267, 0.220)); // #2F4438
                half3 alpine = GhLin(half3(0.424, 0.416, 0.353));  // #6C6A5A
                half3 rockC = GhLin(half3(0.329, 0.353, 0.408));   // #545A68
                half3 snowC = GhLin(half3(0.957, 0.965, 0.973));   // #F4F6F8
                half3 c = lerp(forest, conifer, smoothstep(2600.0, 2900.0, alt));
                c = lerp(c, alpine, smoothstep(3500.0, 4000.0, alt));
                c = lerp(c, rockC, smoothstep(snow - 900.0, snow - 500.0, alt));
                half s = smoothstep(snow - 80.0, snow + 80.0, alt) * (1.0 - smoothstep(0.56, 0.62, streak) * (0.3 + 0.6 * rock));
                return lerp(c, snowC, s);
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
                half ndl = dot(n, main.direction);
                half lit = smoothstep(0.02h, 0.22h, ndl);
                half3 ambient = SampleSH(n);
                half3 c = albedo * (ambient * 0.7h + main.color * (lit * 0.8h));

                // Valley haze over the part of the sight line below the haze top (the scene fog for lines that stay in
                // it, the air's own density for lines that rise above it), then clear-air perspective.
                float eye = _GhSkylineParams.y;
                float lo = min(eye, alt), hi = max(eye, alt);
                float below = hi <= GH_HAZE_TOP_M ? 1.0 : (lo >= GH_HAZE_TOP_M ? 0.0 : (GH_HAZE_TOP_M - lo) / max(1.0, hi - lo));
                float air = _GhSkyAir.x > 0.0 ? _GhSkyAir.x : _GhSkylineParams.z;
                float t = (air + (_GhSkylineParams.z - air) * below) * dist * below;
                half haze = exp(-t * t);
                c = lerp(unity_FogColor.rgb, c, haze);
                float airLength = GH_CLEAR_AIR_M * exp(0.5 * (max(eye, 0.0) + max(alt, 0.0)) / GH_AIR_SCALE_HEIGHT_M);
                half clearAir = exp(-dist / airLength);
                c = lerp(_GhSkyHorizon.rgb, c, clearAir);
                return half4(c, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
