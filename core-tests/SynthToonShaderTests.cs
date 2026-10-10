using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Ghumante.Core.Meshing;
using Ghumante.Core.Synth.Look;
using Ghumante.Core.Synth.Textures;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The shader side of the procedural look, checked from source (no Unity needed): Ghumante/ToonLit keeps the SRP
    /// Batcher layout (every material property in one UnityPerMaterial block shared by all passes, matching the C# twin
    /// ToonLitLayout), the outline pass exists only in the LOD 300 SubShader, the occluder-fade keyword is declared in every
    /// pass that clips, the shader's constants match the texture table (channel array size, slice count, macro tile), every
    /// texture period divides the tile sides, the outline never moves single vertices away, derivatives are taken before
    /// any clip, glints are footprint-filtered, and the occluder capsule matches its C# twin.
    /// </summary>
    public class SynthToonShaderTests
    {
        private static string ShaderDir
        {
            get { return Path.Combine(GoldenFiles.RepoRoot, "game", "Assets", "Ghumante", "World", "Shaders"); }
        }

        private static string Read(string name)
        {
            return File.ReadAllText(Path.Combine(ShaderDir, name));
        }

        private static string StripComments(string s)
        {
            s = Regex.Replace(s, @"/\*.*?\*/", "", RegexOptions.Singleline);
            return Regex.Replace(s, @"//[^\n]*", "");
        }

        private static List<string> PropertyNames(string shader)
        {
            int start = shader.IndexOf("Properties", StringComparison.Ordinal);
            int open = shader.IndexOf('{', start);
            int depth = 0, end = open;
            for (int i = open; i < shader.Length; i++)
            {
                if (shader[i] == '{') depth++;
                if (shader[i] == '}' && --depth == 0)
                {
                    end = i;
                    break;
                }
            }
            string block = StripComments(shader.Substring(open + 1, end - open - 1));
            var names = new List<string>();
            foreach (Match m in Regex.Matches(block, @"^\s*(?:\[[^\]]*\]\s*)*(_\w+)\s*\(", RegexOptions.Multiline)) names.Add(m.Groups[1].Value);
            return names;
        }

        private static List<string> PerMaterialBuffer(string hlsl)
        {
            Match m = Regex.Match(StripComments(hlsl), @"CBUFFER_START\(UnityPerMaterial\)(.*?)CBUFFER_END", RegexOptions.Singleline);
            Assert.That(m.Success, "UnityPerMaterial block");
            var names = new List<string>();
            foreach (Match v in Regex.Matches(m.Groups[1].Value, @"\b(?:half|float|int|uint)\d?(?:x\d)?\s+(_\w+)\s*;")) names.Add(v.Groups[1].Value);
            return names;
        }

        private static List<string> LayoutList(string field)
        {
            string src = File.ReadAllText(Path.Combine(GoldenFiles.RepoRoot, "game", "Assets", "Ghumante", "World", "Rendering", "ToonLitLayout.cs"));
            Match m = Regex.Match(src, field + @"\s*=\s*\{(.*?)\};", RegexOptions.Singleline);
            Assert.That(m.Success, "ToonLitLayout." + field);
            return Regex.Matches(m.Groups[1].Value, "\"(_\\w+)\"").Cast<Match>().Select(x => x.Groups[1].Value).ToList();
        }

        private static List<(string SubShaderLod, string Name, string LightMode, string Program)> Passes(string shader)
        {
            var result = new List<(string, string, string, string)>();
            string[] subs = Regex.Split(shader, @"\n\s*SubShader\s*\n");
            for (int s = 1; s < subs.Length; s++)
            {
                string lod = Regex.Match(subs[s], @"\bLOD\s+(\d+)").Groups[1].Value;
                foreach (Match p in Regex.Matches(subs[s], @"Pass\s*\{\s*Name\s+""(\w+)""\s*Tags\s*\{\s*""LightMode""\s*=\s*""(\w+)""\s*\}(.*?)ENDHLSL",
                                                  RegexOptions.Singleline))
                    result.Add((lod, p.Groups[1].Value, p.Groups[2].Value, p.Groups[3].Value));
            }
            return result;
        }

        [Test]
        public void EveryMaterialPropertyIsInTheSrpBatcherBuffer()
        {
            List<string> props = PropertyNames(Read("ToonLit.shader"));
            List<string> buffer = PerMaterialBuffer(Read("ToonLitInput.hlsl"));
            List<string> perMaterial = LayoutList("PerMaterial"), stateOnly = LayoutList("RenderStateOnly");
            Assert.That(buffer, Is.EqualTo(perMaterial), "UnityPerMaterial must match ToonLitLayout.PerMaterial, in order");
            foreach (string p in props)
                Assert.That(perMaterial.Contains(p) || stateOnly.Contains(p), p + " is neither in UnityPerMaterial nor render-state only");
            foreach (string p in perMaterial) Assert.That(props, Does.Contain(p), p + " has no Properties entry");
            foreach (string p in stateOnly) Assert.That(buffer, Does.Not.Contain(p), p + " is render state, not a shader variable");
        }

        [Test]
        public void EveryPassSharesTheOneInputBlock()
        {
            string shader = Read("ToonLit.shader");
            var passes = Passes(shader);
            Assert.That(passes.Count, Is.EqualTo(9), "5 passes with the outline (LOD 300) + 4 without (LOD 200)");
            foreach (var p in passes)
            {
                Assert.That(p.Program, Does.Contain("#include \"ToonLitInput.hlsl\""), p.Name + " (LOD " + p.SubShaderLod + ")");
                Assert.That(p.Program, Does.Not.Contain("UnityPerMaterial"), p.Name + " declares its own material buffer");
            }
            foreach (string f in Directory.GetFiles(ShaderDir, "ToonLit*.hlsl"))
            {
                if (Path.GetFileName(f) == "ToonLitInput.hlsl") continue;
                Assert.That(File.ReadAllText(f), Does.Not.Contain("CBUFFER_START(UnityPerMaterial)"), Path.GetFileName(f));
            }
        }

        [Test]
        public void OutlineLivesOnlyInTheLod300SubShader()
        {
            var passes = Passes(Read("ToonLit.shader"));
            var outline = passes.Where(p => p.LightMode == "SRPDefaultUnlit").ToList();
            Assert.That(outline.Count, Is.EqualTo(1));
            Assert.That(outline[0].SubShaderLod, Is.EqualTo("300"));
            Assert.That(outline[0].Program, Does.Contain("OutlineVert"));
            var low = passes.Where(p => p.SubShaderLod == "200").Select(p => p.LightMode).ToList();
            Assert.That(low, Is.EquivalentTo(new[] { "UniversalForward", "ShadowCaster", "DepthOnly", "DepthNormals" }));
            var high = passes.Where(p => p.SubShaderLod == "300").Select(p => p.LightMode).ToList();
            Assert.That(high, Is.EquivalentTo(new[] { "UniversalForward", "SRPDefaultUnlit", "ShadowCaster", "DepthOnly", "DepthNormals" }));
        }

        [Test]
        public void ClippingPassesDeclareTheirKeywords()
        {
            foreach (var p in Passes(Read("ToonLit.shader")))
            {
                string what = p.Name + " (LOD " + p.SubShaderLod + ")";
                Assert.That(p.Program, Does.Contain("shader_feature_local _BAND_FADE"), what);
                Assert.That(p.Program, Does.Contain("shader_feature_local_vertex _WIND"), what);
                bool fades = p.LightMode != "ShadowCaster";
                Assert.That(p.Program.Contains("_OCCLUDER_FADE"), Is.EqualTo(fades), what + ": shadows keep the occluders");
                Assert.That(p.Program, Does.Contain("#pragma target 3.5"), what);
            }
        }

        [Test]
        public void ShaderConstantsMatchTheTextureTable()
        {
            string input = Read("ToonLitInput.hlsl");
            int arraySize = int.Parse(Regex.Match(input, @"#define GH_CHANNEL_ARRAY_SIZE (\d+)").Groups[1].Value, CultureInfo.InvariantCulture);
            Assert.That(arraySize, Is.EqualTo(MaterialLooks.ArraySize));
            float macro = float.Parse(Regex.Match(input, @"#define GH_MACRO_TILE_M ([\d.]+)").Groups[1].Value, CultureInfo.InvariantCulture);
            Assert.That(macro, Is.EqualTo(MaterialLooks.MacroTileM));
            int plain = int.Parse(Regex.Match(input, @"#define GH_CHANNEL_PLAIN (\d+)").Groups[1].Value, CultureInfo.InvariantCulture);
            Assert.That(plain, Is.EqualTo((int)MaterialChannel.Plain));
            Assert.That(MaterialLooks.Count, Is.LessThanOrEqualTo(arraySize), "every channel fits the shader arrays");
            Assert.That(input, Does.Contain("float4 _GhChannelA[GH_CHANNEL_ARRAY_SIZE];"));
            Assert.That(input, Does.Contain("float4 _GhChannelB[GH_CHANNEL_ARRAY_SIZE];"));
            // The tint encoding the texture generator writes (MaterialTextures: albedo = lerp(rgb, tint × rgb × 2, a)).
            Assert.That(input, Does.Contain("lerp(texel.rgb, tint * texel.rgb * 2.0h, texel.a)"));
            // Meshes without UV0 read (0, 0): Plain with no AO, exactly the old look.
            Assert.That(input, Does.Contain("(channel < 0.5 && uv0.y <= 0.0) ? 1.0h"));
        }

        [Test]
        public void ForwardPassReadsUv0AsChannelAndAo()
        {
            string forward = Read("ToonLitForwardPass.hlsl");
            Assert.That(forward, Does.Contain("float2 uv0 : TEXCOORD0;"));
            Assert.That(forward, Does.Contain("GhChannelAndAo(input.uv0, channel, ao);"));
            Assert.That(forward, Does.Contain("nointerpolation float4 matA"));
            string outline = Read("ToonLitOutlinePass.hlsl");
            Assert.That(outline, Does.Contain("GhOccluderClip"), "faded houses lose their outline too");
            Assert.That(outline, Does.Contain("GhBandClip"));
        }
    
        private static string Function(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), signature);
            int open = source.IndexOf('{', start);
            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                if (source[i] == '}' && --depth == 0) return source.Substring(start, i - start + 1);
            }
            Assert.Fail("unbalanced " + signature);
            return null;
        }

        [Test]
        public void UnknownChannelsAreClampedToTheRealSlices()
        {
            string input = Read("ToonLitInput.hlsl");
            int slices = int.Parse(Regex.Match(input, @"#define GH_SLICE_COUNT (\d+)").Groups[1].Value, CultureInfo.InvariantCulture);
            Assert.That(slices, Is.EqualTo(MaterialTextures.SliceCount), "GH_SLICE_COUNT = MaterialTextures.SliceCount");
            Assert.That(slices, Is.EqualTo(MaterialLooks.Count));
            string layout = File.ReadAllText(Path.Combine(GoldenFiles.RepoRoot, "game", "Assets", "Ghumante", "World", "Rendering", "ToonLitLayout.cs"));
            Assert.That(layout, Does.Contain("public const int SliceCount = " + slices + ";"));
            string channel = StripComments(Function(input, "void GhChannelAndAo("));
            Assert.That(channel, Does.Contain("channel >= GH_SLICE_COUNT"), "channels without a slice render as Plain, not as Marking");
            Assert.That(channel, Does.Not.Contain("GH_CHANNEL_ARRAY_SIZE"));
        }

        [Test]
        public void EveryTexturePeriodDividesTheTileSides()
        {
            // Tile-local meshes are sampled in object space: a period that does not divide the tile side (and the 64 m grid
            // of anything placed on it) shows a seam at every tile border.
            float span = MaterialLooks.SeamFreeSpanM;
            Assert.That(span, Is.EqualTo(64f));
            var periods = new List<(string, float)> { ("macro", MaterialLooks.MacroTileM) };
            for (int i = 0; i < MaterialLooks.Count; i++) periods.Add((((MaterialChannel)i).ToString(), MaterialLooks.Of((MaterialChannel)i).TileM));
            foreach ((string name, float period) in periods)
            {
                double log = Math.Log(period, 2.0);
                Assert.That(Math.Abs(log - Math.Round(log)), Is.LessThan(1e-9), name + " " + period + " m is a power of two");
                double repeats = span / period;
                Assert.That(Math.Abs(repeats - Math.Round(repeats)), Is.LessThan(1e-9), name + ": " + span + " m / " + period + " m");
                // The smallest tile side the runtime draws (level 10, 1,024 m) and every coarser one.
                Assert.That(1024.0 / period % 1.0, Is.EqualTo(0.0), name);
            }
        }

        [Test]
        public void OutlineNeverMovesSingleVerticesAway()
        {
            string outline = StripComments(Read("ToonLitOutlinePass.hlsl"));
            string vert = Function(outline, "OutlineVaryings OutlineVert(");
            // The old per-vertex collapse turned triangles crossing the fade-out radius into screen-wide slivers.
            Assert.That(vert, Does.Not.Match(@"widthPx\s*>\s*0\.05\s*\?"), "no per-vertex collapse on the width");
            // The only collapse is per instance, from the instance origin (all vertices of an instance agree).
            Match collapse = Regex.Match(vert, @"#if defined\(UNITY_INSTANCING_ENABLED\)(.*?)#endif", RegexOptions.Singleline);
            Assert.That(collapse.Success, "instance-level collapse");
            Assert.That(collapse.Groups[1].Value, Does.Contain("TransformObjectToWorld(float3(0.0, 0.0, 0.0))"));
            Assert.That(vert.Replace(collapse.Value, ""), Does.Not.Contain("float4(2.0, 2.0, 2.0, 1.0)"), "nothing else collapses");
            Assert.That(vert, Does.Contain("o.widthPx = widthPx;"));
            string frag = Function(outline, "half4 OutlineFrag(");
            Assert.That(frag, Does.Contain("clip(i.widthPx - 0.05);"), "faded-out hull pixels are discarded per fragment");
        }

        [Test]
        public void DerivativesAreTakenBeforeAnyClip()
        {
            string frag = StripComments(Function(Read("ToonLitForwardPass.hlsl"), "half4 ToonFrag("));
            int albedo = frag.IndexOf("GhMaterialAlbedo(", StringComparison.Ordinal);
            int ddx = frag.IndexOf("ddx(", StringComparison.Ordinal);
            int band = frag.IndexOf("GhBandClip(", StringComparison.Ordinal);
            int occluder = frag.IndexOf("GhOccluderClip(", StringComparison.Ordinal);
            Assert.That(albedo, Is.GreaterThan(0));
            Assert.That(ddx, Is.GreaterThan(0));
            Assert.That(band, Is.GreaterThan(Math.Max(albedo, ddx)), "band clip after the texture gradients");
            Assert.That(occluder, Is.GreaterThan(Math.Max(albedo, ddx)), "occluder clip after the texture gradients");
            Assert.That(frag.LastIndexOf("ddx(", StringComparison.Ordinal), Is.LessThan(band), "no derivative after a discard");
            Assert.That(frag.IndexOf("ddy(", StringComparison.Ordinal), Is.LessThan(band));
            string input = StripComments(Read("ToonLitInput.hlsl"));
            Assert.That(Function(input, "half3 GhMaterialAlbedo(").Contains("clip("), Is.False);
        }

        [Test]
        public void GlintsAreFilteredByThePixelFootprint()
        {
            string frag = StripComments(Function(Read("ToonLitForwardPass.hlsl"), "half4 ToonFrag("));
            Assert.That(frag, Does.Not.Contain("positionOS * 60.0"), "no fixed 17 mm cells");
            Assert.That(frag, Does.Contain("footprint"));
            Assert.That(frag, Does.Contain("exp2(max(-6.0, ceil(log2(max(2.0 * footprint, 1e-6)))))"), "cells at least two pixels wide");
            Assert.That(frag, Does.Contain("i.matA.w * _GhLookParams.z) * detail * (half)saturate(2.0 - 16.0 * footprint)"),
                        "fading out continuously with the footprint (1/8 to 1/4 m per two pixels) and with the texture distance fade");
            // The view term reshuffles the glints over degrees of view change, not every centimetre of camera bob.
            Match view = Regex.Match(frag, @"dot\(\(float3\)viewDir, float3\(([\d.]+), ([\d.]+), ([\d.]+)\)\)");
            Assert.That(view.Success);
            double norm = Math.Sqrt(Enumerable.Range(1, 3).Sum(k => Math.Pow(double.Parse(view.Groups[k].Value, CultureInfo.InvariantCulture), 2)));
            Assert.That(norm, Is.LessThan(0.5));
        }

        [Test]
        public void OccluderShaderMatchesItsTwin()
        {
            string input = StripComments(Read("ToonLitInput.hlsl"));
            string fn = Function(input, "float GhOccluderOpacity(");
            foreach (string term in new[]
                     {
                         "float tc = saturate(t);",
                         "float radius = lerp(_GhOccluderA.w, _GhOccluderC.w, tc);",
                         "float side = saturate((radius + soft - distance(positionWS, a + ab * tc)) / soft);",
                         "float cap = saturate((1.0 - t) * len / (0.5 * soft));",
                         "float away = saturate((distance(positionWS, _GhOccluderB.xyz) - _GhOccluderC.y) / max(0.5 * _GhOccluderC.y, 1e-3));",
                         "float above = saturate((positionWS.y - _GhOccluderD.x) / 0.3);",
                         "float upright = saturate((0.7 - normalY) / 0.2);",
                         "float overhead = saturate((positionWS.y - _GhOccluderD.y) / 0.3);",
                         "fade *= above * max(upright, overhead);",
                         "return 1.0 - fade * (1.0 - _GhOccluderC.z);",
                     })
                Assert.That(fn, Does.Contain(term));
            Assert.That(fn, Does.Contain("_OccluderGround > 0.5"));
            Assert.That(OccluderCapsule.GroundRampM, Is.EqualTo(0.3f));
            Assert.That(input, Does.Contain("float4 _GhOccluderD;"));
            Assert.That(LayoutList("Globals"), Does.Contain("_GhOccluderD"));
            // No fade-in at the camera end any more: nothing scales the fade by the distance from the camera.
            Assert.That(fn, Does.Not.Contain("saturate(t * len"));
            // Every pass that clips passes a normal (the ground layers need it).
            foreach (string f in new[] { "ToonLitForwardPass.hlsl", "ToonLitDepthPasses.hlsl", "ToonLitOutlinePass.hlsl" })
                foreach (Match m in Regex.Matches(StripComments(Read(f)), @"GhOccluderClip\(([^;]*)\);"))
                    Assert.That(m.Groups[1].Value.Split(',').Length, Is.EqualTo(3 + (m.Groups[1].Value.Contains("float3(") ? 2 : 0)), f);
        }

        [Test]
        public void WaterDriftIsWrapped()
        {
            string fn = StripComments(Function(Read("ToonLitInput.hlsl"), "half3 GhMaterialAlbedo("));
            Assert.That(fn, Does.Contain("p.x += frac(_Time.y * matB.w * matA.x);"), "whole tiles: no precision loss after hours");
        }
    }
}
