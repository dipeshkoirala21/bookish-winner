using Ghumante.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ghumante.Tests.EditMode
{
    /// <summary>ProjectSetup's rendering choices for the world: fog variants survive stripping, no unused depth
    /// texture, a floating-point depth attachment.</summary>
    public class WorldRenderingSetupTests
    {
        [Test]
        public void FogExp2VariantsAreKeptInPlayerBuilds()
        {
            ProjectSetup.ApplyShaderStripping();
            var so = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
            SerializedProperty stripping = so.FindProperty("m_FogStripping");
            SerializedProperty keepExp2 = so.FindProperty("m_FogKeepExp2");
            Assert.IsNotNull(stripping);
            Assert.IsNotNull(keepExp2);
            Assert.AreEqual(1, stripping.intValue, "Custom: no build scene uses fog, Automatic would strip FOG_EXP2");
            Assert.IsTrue(keepExp2.boolValue, "WorldSky turns ExponentialSquared fog on at runtime");
        }

        [Test]
        public void EveryTierSkipsTheUnusedDepthTextureAndUsesFloatDepth()
        {
            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            UniversalRenderPipelineAsset pipeline = UniversalRenderPipelineAsset.Create(renderer);
            try
            {
                foreach (ProjectSetup.TierProfile p in ProjectSetup.Tiers)
                {
                    ProjectSetup.ConfigureRenderer(renderer);
                    ProjectSetup.ConfigurePipeline(pipeline, renderer, p);
                    Assert.IsFalse(pipeline.supportsCameraDepthTexture, p.qualityName + ": nothing samples _CameraDepthTexture");
                    var so = new SerializedObject(renderer);
                    Assert.AreEqual((int)DepthFormat.Depth_32_Stencil_8, so.FindProperty("m_DepthAttachmentFormat").intValue,
                                    p.qualityName + ": D24 UNorm cannot resolve a 155 km view");
                }
            }
            finally
            {
                Object.DestroyImmediate(pipeline);
                Object.DestroyImmediate(renderer);
            }
        }
    }
}
