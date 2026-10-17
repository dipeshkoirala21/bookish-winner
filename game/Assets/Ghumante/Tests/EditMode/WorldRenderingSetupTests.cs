using Ghumante.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ghumante.Tests.EditMode
{
    /// <summary>ProjectSetup's rendering choices for the world: fog variants survive stripping, no unused depth
    /// texture, a floating-point depth attachment, and steady shadows (playtest #4).</summary>
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

        [Test]
        public void ShadowsStaySharpAndSteadyNearTheCamera()
        {
            // Playtest #4 ("buildings constantly flicker"): every tier's map covers the streets around the camera at about
            // 15 cm per texel or finer (the first cascade's reach over its tile, the bounding sphere about 0.75 of the
            // reach), the far edge fades over the cascade border instead of sweeping over the facades as a hard line, the
            // conservative enclosing sphere keeps casters at the cascade corners, and Mid filters its single cascade.
            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            UniversalRenderPipelineAsset pipeline = UniversalRenderPipelineAsset.Create(renderer);
            try
            {
                foreach (ProjectSetup.TierProfile p in ProjectSetup.Tiers)
                {
                    ProjectSetup.ConfigurePipeline(pipeline, renderer, p);
                    Assert.AreEqual(p.shadowDistance, pipeline.shadowDistance, 1e-4f, p.qualityName);
                    Assert.IsTrue(pipeline.conservativeEnclosingSphere, p.qualityName + ": casters at the cascade corners stay");
                    Assert.AreEqual(ProjectSetup.ShadowCascadeBorder, pipeline.cascadeBorder, 1e-6f, p.qualityName + ": the far edge fades");
                    Assert.Greater(pipeline.cascadeBorder, 0.1f);
                    float first = p.shadowCascades > 1 ? p.shadowDistance * p.shadowCascade2Split : p.shadowDistance;
                    int tile = p.shadowCascades > 1 ? p.mainLightShadowResolution / 2 : p.mainLightShadowResolution;
                    float texel = 2f * 0.75f * first / tile;
                    Assert.LessOrEqual(texel, 0.15f, p.qualityName + ": texel near the camera (m)");
                    if (p.shadowCascades > 1) Assert.AreEqual(p.shadowCascade2Split, pipeline.cascade2Split, 1e-6f);
                    var so = new SerializedObject(pipeline);
                    Assert.AreEqual(p.softShadows, so.FindProperty("m_SoftShadowsSupported").boolValue, p.qualityName);
                    Assert.AreEqual(p.softShadowQuality, so.FindProperty("m_SoftShadowQuality").intValue, p.qualityName);
                }
                Assert.IsTrue(ProjectSetup.Tiers[1].softShadows, "Mid filters its single cascade (no stair-stepping edges)");
                Assert.IsFalse(ProjectSetup.Tiers[0].softShadows, "Low keeps hard shadows (cost)");
            }
            finally
            {
                Object.DestroyImmediate(pipeline);
                Object.DestroyImmediate(renderer);
            }
        }
    }
}
