using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Ghumante.Core.Synth.Textures;
using Ghumante.Platform;
using Ghumante.World.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Ghumante.Tests.EditMode
{
    /// <summary>
    /// The cartoon look (World/README.md "Look"): ToonLit's SRP Batcher layout matches its C# twin, the look roles of the
    /// world materials (outline pass, occluder fade), the tier table, the occluder-fade capsule, and the runtime texture
    /// bank producing a mipmapped sRGB array with one slice per material channel.
    /// </summary>
    public class WorldRenderingLookTests
    {
        private static string ShaderPath(string file)
        {
            return Path.Combine(Application.dataPath, "Ghumante", "World", "Shaders", file);
        }

        [Test]
        public void PerMaterialBufferMatchesTheLayoutTwin()
        {
            string hlsl = File.ReadAllText(ShaderPath("ToonLitInput.hlsl"));
            Match block = Regex.Match(hlsl, @"CBUFFER_START\(UnityPerMaterial\)(.*?)CBUFFER_END", RegexOptions.Singleline);
            Assert.IsTrue(block.Success);
            MatchCollection vars = Regex.Matches(Regex.Replace(block.Groups[1].Value, @"//[^\n]*", ""), @"\b(?:half|float)\d?\s+(_\w+)\s*;");
            Assert.AreEqual(ToonLitLayout.PerMaterial.Length, vars.Count);
            for (int i = 0; i < vars.Count; i++) Assert.AreEqual(ToonLitLayout.PerMaterial[i], vars[i].Groups[1].Value, "slot " + i);
        }

        [Test]
        public void ToonLitDeclaresEveryLayoutProperty()
        {
            Shader shader = Shader.Find(WorldShaders.ToonLit);
            Assert.IsNotNull(shader, "Ghumante/ToonLit compiled");
            foreach (string p in ToonLitLayout.PerMaterial) Assert.GreaterOrEqual(shader.FindPropertyIndex(p), 0, p);
            foreach (string p in ToonLitLayout.RenderStateOnly) Assert.GreaterOrEqual(shader.FindPropertyIndex(p), 0, p);
        }

        [Test]
        public void WorldMaterialsGetTheirLookRoles()
        {
            WorldMaterialSet set = WorldMaterialSet.CreateRuntime();
            try
            {
                foreach (Material ground in new[] { set.terrain, set.roads, set.areas, set.decals })
                {
                    Assert.IsFalse(ground.GetShaderPassEnabled(ToonLitLayout.OutlinePassLightMode), ground.name + " has no outline");
                    Assert.IsFalse(ground.IsKeywordEnabled(ToonLitLayout.KeywordOccluderFade), ground.name + " never dissolves");
                }
                foreach (Material near in new[] { set.bandB0, set.bandB1, set.bandB1Full, set.heroes, set.trees, set.instancedTint })
                {
                    Assert.IsTrue(near.GetShaderPassEnabled(ToonLitLayout.OutlinePassLightMode), near.name + " is outlined");
                    Assert.IsTrue(near.IsKeywordEnabled(ToonLitLayout.KeywordOccluderFade), near.name + " fades between camera and player");
                }
                foreach (Material far in new[] { set.bandB2, set.bandB3 })
                {
                    Assert.IsFalse(far.GetShaderPassEnabled(ToonLitLayout.OutlinePassLightMode), far.name + " is beyond the outline range");
                    Assert.IsFalse(far.IsKeywordEnabled(ToonLitLayout.KeywordOccluderFade), far.name + " is beyond the capsule");
                }
                // The explorer's material is copied from the plain building material: outlined, never faded.
                Assert.IsTrue(set.buildings.GetShaderPassEnabled(ToonLitLayout.OutlinePassLightMode));
                Assert.IsFalse(set.buildings.IsKeywordEnabled(ToonLitLayout.KeywordOccluderFade));
                Assert.IsFalse(set.instanced.IsKeywordEnabled(ToonLitLayout.KeywordOccluderFade), "traffic (the bus you ride) stays solid");
                Assert.AreEqual(1f, set.terrain.GetFloat(WorldShaders.DetailStrength));
                // Idempotent: applying again changes nothing.
                WorldMaterialDefaults.ApplyLook(set, false);
                Assert.IsTrue(set.bandB1.IsKeywordEnabled(ToonLitLayout.KeywordOccluderFade));
                Assert.IsFalse(set.roads.GetShaderPassEnabled(ToonLitLayout.OutlinePassLightMode));
            }
            finally
            {
                set.DestroyRuntimeMaterials();
                Object.DestroyImmediate(set);
            }
        }

        [Test]
        public void TiersScaleTheLook()
        {
            ToonLookTier low = ToonLookTier.For(DeviceTier.Low), mid = ToonLookTier.For(DeviceTier.Mid), high = ToonLookTier.For(DeviceTier.High);
            Assert.IsFalse(low.Outlines, "Low draws no outline pass at all");
            Assert.AreEqual(ToonLitLayout.LodWithoutOutline, low.ShaderLod);
            Assert.IsTrue(mid.Outlines && high.Outlines);
            Assert.AreEqual(ToonLitLayout.LodWithOutline, high.ShaderLod);
            Assert.IsFalse(low.Triplanar, "Low samples one projection");
            Assert.AreEqual(128, low.TextureSize);
            Assert.AreEqual(256, high.TextureSize);
            Assert.Less(low.DetailFadeM, mid.DetailFadeM);
            Assert.Less(mid.DetailFadeM, high.DetailFadeM);
            Assert.Less(mid.OutlineFadeEndM, high.OutlineFadeEndM + 1e-3f);
            Assert.Less(high.OutlineFadeEndM, 130f, "outlines stay inside the B1 band, where the outlined materials are");
        }

        [Test]
        public void OccluderCapsuleClearsTheViewButNotThePlayer()
        {
            OccluderFadeSettings s = OccluderFadeSettings.Default;
            // Camera at the origin looking at a player 8 m away along +z.
            float Opacity(float x, float y, float z) => OccluderFade.Opacity(x, y, z, 0f, 0f, 0f, 0f, 0f, 8f, s);
            Assert.AreEqual(s.MinOpacity, Opacity(0f, 0f, 4f), 1e-4f, "a wall halfway is dithered away");
            Assert.AreEqual(1f, Opacity(0f, 0f, 7.9f), 1e-4f, "the last metre before the player stays solid");
            Assert.AreEqual(1f, Opacity(0f, 0f, 9f), 1e-4f, "nothing behind the player fades");
            Assert.AreEqual(1f, Opacity(0f, 0f, -1f), 1e-4f, "nothing behind the camera fades");
            Assert.AreEqual(1f, Opacity(3f, 0f, 4f), 1e-4f, "walls to the side stay");
            Assert.Less(Opacity(1.2f, 0f, 6f), 1f, "the capsule widens towards the player");
            Assert.AreEqual(1f, Opacity(1.2f, 0f, 1f), 1e-4f, "and is narrow at the camera");
        }

        [Test]
        public void TextureBankUploadsOneSlicePerChannel()
        {
            Texture bound = Shader.GetGlobalTexture(ToonTextureBank.MaterialTexId);
            var bank = new ToonTextureBank(32);
            try
            {
                bank.Start();
                var watch = Stopwatch.StartNew();
                while (!bank.TryUpload(2) && watch.ElapsedMilliseconds < 20000) System.Threading.Thread.Sleep(10);
                Assert.IsTrue(bank.IsUploaded, "bake finished");
                Texture2DArray array = bank.Array;
                Assert.AreEqual(MaterialTextures.SliceCount, array.depth);
                Assert.AreEqual(32, array.width);
                Assert.AreEqual(MaterialTextures.MipCount(32), array.mipmapCount);
                Assert.AreEqual(TextureWrapMode.Repeat, array.wrapMode);
                Assert.AreSame(array, Shader.GetGlobalTexture(ToonTextureBank.MaterialTexId));
            }
            finally
            {
                bank.Dispose();
                if (bound != null) Shader.SetGlobalTexture(ToonTextureBank.MaterialTexId, bound); // the editor's own bank
            }
        }
    }
}
