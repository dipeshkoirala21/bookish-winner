using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Ghumante.Core.Meshing;
using Ghumante.Core.Synth.Textures;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The shader side of the procedural look, checked from source (no Unity needed): Ghumante/ToonLit keeps the SRP
    /// Batcher layout (every material property in one UnityPerMaterial block shared by all passes, matching the C# twin
    /// ToonLitLayout), the outline pass exists only in the LOD 300 SubShader, the occluder-fade keyword is declared in every
    /// pass that clips, and the shader's constants match the texture table (channel array size, macro tile).
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
    }
}
