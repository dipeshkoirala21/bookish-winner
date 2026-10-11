using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;
using Ghumante.Platform;
using Ghumante.World.Instancing;
using Ghumante.World.Life;
using Ghumante.World.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Ghumante.Tests.EditMode
{
    /// <summary>
    /// The W2 perf pass on the Unity side of the life presenters: the outline-free copies of the instanced materials keep
    /// everything but the outline pass, the draw budget's outline flag follows the look tier, and tinted instance batches
    /// draw short runs and full runs without tripping the property block's array size.
    /// </summary>
    public class LifeDrawBudgetTests
    {
        private static bool Outlined(Material m)
        {
            return m.GetShaderPassEnabled(ToonLitLayout.OutlinePassLightMode) && m.GetFloat(WorldShaders.OutlineWidth) > 0f;
        }

        [Test]
        public void OutlineFreeCopiesDropOnlyTheOutline()
        {
            WorldMaterialSet set = WorldMaterialSet.CreateRuntime();
            Material tint = null, plain = null;
            try
            {
                WorldMaterialDefaults.ApplyLook(set, false, DeviceTier.High);
                Assert.IsTrue(Outlined(set.instancedTint) && Outlined(set.instanced), "the sources are outlined on High");
                tint = WorldMaterialDefaults.CreateOutlineFree(set.instancedTint, "tint copy");
                plain = WorldMaterialDefaults.CreateOutlineFree(set.instanced, "plain copy");
                foreach (Material m in new[] { tint, plain })
                {
                    Assert.IsFalse(Outlined(m), m.name + " draws no outline pass");
                    Assert.IsTrue(m.enableInstancing, m.name + " stays instanced");
                    Assert.AreSame(set.instanced.shader, m.shader);
                    Assert.AreEqual(HideFlags.DontSave, m.hideFlags & HideFlags.DontSave);
                }
                Assert.AreEqual(set.instancedTint.IsKeywordEnabled(WorldShaders.KeywordInstanceTint), tint.IsKeywordEnabled(WorldShaders.KeywordInstanceTint),
                                "the copy keeps the per-instance tint");
                Assert.AreEqual(set.instancedTint.IsKeywordEnabled(WorldShaders.KeywordOccluderFade), tint.IsKeywordEnabled(WorldShaders.KeywordOccluderFade),
                                "and the occluder fade");
                Assert.IsTrue(Outlined(set.instancedTint), "the source keeps its outline");
                Assert.IsNull(WorldMaterialDefaults.CreateOutlineFree(null, "none"));
            }
            finally
            {
                WorldMaterialDefaults.DestroyRuntime(tint);
                WorldMaterialDefaults.DestroyRuntime(plain);
                set.DestroyRuntimeMaterials();
                Object.DestroyImmediate(set);
            }
        }

        [Test]
        public void BudgetOutlinesFollowTheLookTier()
        {
            foreach (DeviceTier tier in new[] { DeviceTier.Low, DeviceTier.Mid, DeviceTier.High })
            {
                LifeDrawBudget b = LifeDrawBudget.ForTier((int)tier);
                Assert.AreEqual(ToonLookTier.For(tier).Outlines, b.Outlines, tier + ": the budget counts the outline pass where the look draws it");
                Assert.AreEqual(b.Total, b.Sum, tier + ": the slices fill the frame");
            }
        }

        [Test]
        public void TintedBatchesDrawShortAndFullRuns()
        {
            var data = new MeshData(256, 768);
            ParkedLook.BuildNeutral(0, VehicleLod.Block, data);
            Mesh mesh = Ghumante.Vehicles.Visuals.VehicleMeshCache.Upload(data, "neutral block");
            WorldMaterialSet set = WorldMaterialSet.CreateRuntime();
            try
            {
                var b = new InstanceBatch(mesh, set.instancedTint, data.TriangleCount, true);
                for (int i = 0; i < 3; i++) b.Add(Matrix4x4.Translate(new Vector3(i, 0f, 0f)), Tint.Hex(ParkedLook.TintOf(0, (byte)i)));
                b.Flush();
                Assert.AreEqual(1, b.Draws);
                Assert.AreEqual(3, b.Instances);
                Assert.AreEqual(0, b.Pending);
                // A run longer than the last one, then a full one and a remainder: no array-size trouble.
                for (int i = 0; i < InstanceBatch.MaxPerDraw + 5; i++) b.Add(Matrix4x4.identity, Vector4.one);
                b.Flush();
                Assert.AreEqual(3, b.Draws);
                Assert.AreEqual(3 + InstanceBatch.MaxPerDraw + 5, b.Instances);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
                set.DestroyRuntimeMaterials();
                Object.DestroyImmediate(set);
            }
        }
    }
}
