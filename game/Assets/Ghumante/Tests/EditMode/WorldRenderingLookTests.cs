using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Ghumante.Core.Synth.Look;
using Ghumante.Core.Synth.Textures;
using Ghumante.Platform;
using Ghumante.World.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Ghumante.Tests.EditMode
{
    /// <summary>
    /// The cartoon look (World/README.md "Look"): ToonLit's SRP Batcher layout matches its C# twin, the look roles of the
    /// world materials per tier (outline pass, occluder fade, ground mode), the tier table, the occluder-fade capsule, the
    /// runtime texture bank producing a mipmapped sRGB array with one slice per material channel, and the re-bake when the
    /// tier asks for another texture size.
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

        private static bool Outlined(Material m)
        {
            return m.GetShaderPassEnabled(ToonLitLayout.OutlinePassLightMode);
        }

        private static bool Fades(Material m)
        {
            return m.IsKeywordEnabled(ToonLitLayout.KeywordOccluderFade);
        }

        [Test]
        public void WorldMaterialsGetTheirLookRolesPerTier()
        {
            WorldMaterialSet set = WorldMaterialSet.CreateRuntime();
            try
            {
                Assert.IsNotNull(set.props, "the props material exists");
                // Mid (ARCHITECTURE 10): characters, vehicles, landmarks.
                WorldMaterialDefaults.ApplyLook(set, false, DeviceTier.Mid);
                foreach (Material m in new[] { set.buildings, set.instanced, set.instancedTint, set.heroes })
                    Assert.IsTrue(Outlined(m), m.name + " is outlined on Mid");
                foreach (Material m in new[] { set.terrain, set.roads, set.areas, set.decals, set.bandB0, set.bandB1, set.bandB1Full, set.bandB2, set.bandB3, set.trees, set.props })
                    Assert.IsFalse(Outlined(m), m.name + " has no outline on Mid");
                // High adds props and the near building band.
                WorldMaterialDefaults.ApplyLook(set, false, DeviceTier.High);
                Assert.IsTrue(Outlined(set.props) && Outlined(set.bandB0) && Outlined(set.heroes));
                foreach (Material m in new[] { set.bandB1, set.bandB1Full, set.bandB2, set.bandB3, set.trees, set.roads, set.terrain })
                    Assert.IsFalse(Outlined(m), m.name + " has no outline on High");
                // Low: none at all.
                WorldMaterialDefaults.ApplyLook(set, false, DeviceTier.Low);
                foreach (Material m in new[] { set.buildings, set.instanced, set.instancedTint, set.heroes, set.bandB0, set.props })
                    Assert.IsFalse(Outlined(m), m.name + " has no outline on Low");

                // The occluder fade does not depend on the tier.
                foreach (DeviceTier tier in new[] { DeviceTier.Low, DeviceTier.High })
                {
                    WorldMaterialDefaults.ApplyLook(set, false, tier);
                    foreach (Material m in new[] { set.terrain, set.areas, set.decals, set.bandB2, set.bandB3, set.buildings, set.instanced })
                        Assert.IsFalse(Fades(m), m.name + " never dissolves");
                    foreach (Material m in new[] { set.bandB0, set.bandB1, set.bandB1Full, set.heroes, set.trees, set.instancedTint, set.props })
                    {
                        Assert.IsTrue(Fades(m), m.name + " fades between camera and player");
                        Assert.AreEqual(0f, m.GetFloat(WorldShaders.OccluderGround), m.name + " fades whole");
                    }
                    // The road layer (bridge railings, piers, decks) fades as a ground layer: structures only.
                    Assert.IsTrue(Fades(set.roads));
                    Assert.AreEqual(1f, set.roads.GetFloat(WorldShaders.OccluderGround));
                }
                Assert.AreEqual(1f, set.terrain.GetFloat(WorldShaders.DetailStrength));
                // Idempotent: applying again changes nothing.
                WorldMaterialDefaults.ApplyLook(set, false, DeviceTier.High);
                WorldMaterialDefaults.ApplyLook(set, false, DeviceTier.High);
                Assert.IsTrue(Fades(set.bandB1) && Outlined(set.bandB0));
                Assert.IsFalse(Outlined(set.roads));
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
            Assert.IsFalse(mid.OutlineNearBuildings || mid.OutlineProps, "Mid: characters, vehicles, landmarks only");
            Assert.IsTrue(high.OutlineNearBuildings && high.OutlineProps, "High: + props and near buildings");
            Assert.AreEqual(128, low.TextureSize);
            Assert.AreNotEqual(low.TextureSize, mid.TextureSize, "a tier change can need a re-bake");
        }

        [Test]
        public void OutlineCostFollowsTheTier()
        {
            var stats = new RenderStats { VehicleTris = 1000, PeopleTris = 2000, HeroTris = 4000, PropTris = 8000, B0Tris = 16000, B1Tris = 32000, TreeTris = 64000 };
            Assert.AreEqual(0, stats.OutlinedTris(ToonLookTier.For(DeviceTier.Low)));
            Assert.AreEqual(1000 + 2000 + 4000 + 8000, stats.OutlinedTris(ToonLookTier.For(DeviceTier.Mid)), "no buildings, no trees");
            Assert.AreEqual(1000 + 2000 + 4000 + 8000 + 16000, stats.OutlinedTris(ToonLookTier.For(DeviceTier.High)), "+ B0, never B1 or trees");
        }

        [Test]
        public void OccluderCapsuleClearsTheViewButNotThePlayer()
        {
            // The full behaviour is covered by core-tests/SynthOccluderFadeTests; here the defaults ToonLook uses.
            OccluderFadeSettings s = ToonLook.Occluder;
            // Camera 1.5 m behind and above a rider (a camera squeezed into a galli), 60° portrait view.
            OccluderCapsule c = OccluderCapsule.From(s, 0f, 1.5f, -1.2f, 0f, 1f, 0f, Mathf.Tan(30f * Mathf.Deg2Rad), 9f / 16f);
            Assert.AreEqual(s.MinOpacity, c.Opacity(0f, 1.45f, -1.1f, 0f, false), 1e-4f, "a wall 0.1 m in front of the lens is gone");
            Assert.AreEqual(s.MinOpacity, c.Opacity(0f, 1.3f, -0.7f, 0f, false), 1e-4f, "halfway");
            Assert.AreEqual(1f, c.Opacity(0f, 1f, 0f, 0f, false), 1e-4f, "the player stays");
            Assert.AreEqual(1f, c.Opacity(0f, 0.9f, 1f, 0f, false), 1e-4f, "the background stays");
            Assert.AreEqual(1f, c.Opacity(0f, 0f, -0.7f, 1f, true), 1e-4f, "the road stays");
        }

        [Test]
        public void ChangingTheTierRebakesTheTextures()
        {
            ToonLook.EnsureInitialized();
            try
            {
                ToonLook.OverrideTier(DeviceTier.Low);
                WaitForBank(ToonLookTier.For(DeviceTier.Low).TextureSize);
                Texture2DArray low = ToonLook.Bank.Array;
                ToonLook.OverrideTier(DeviceTier.High);
                ToonLook.Refresh();
                // The old textures stay bound while the new size bakes.
                if (ToonLook.PendingBank != null) Assert.AreSame(low, Shader.GetGlobalTexture(ToonTextureBank.MaterialTexId));
                WaitForBank(ToonLookTier.For(DeviceTier.High).TextureSize);
                Assert.AreEqual(256, ToonLook.Bank.Array.width);
                Assert.AreSame(ToonLook.Bank.Array, Shader.GetGlobalTexture(ToonTextureBank.MaterialTexId));
                Assert.IsTrue(low == null, "the old array is destroyed after the swap");
            }
            finally
            {
                ToonLook.OverrideTier(null);
                ToonLook.Refresh();
            }
        }

        private static void WaitForBank(int size)
        {
            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < 60000)
            {
                ToonLook.Refresh();
                ToonTextureBank bank = ToonLook.Bank;
                if (bank != null && bank.IsUploaded && bank.Size == size && ToonLook.PendingBank == null) return;
                System.Threading.Thread.Sleep(10);
            }
            Assert.Fail("texture bank at " + size + " px not uploaded in time");
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
