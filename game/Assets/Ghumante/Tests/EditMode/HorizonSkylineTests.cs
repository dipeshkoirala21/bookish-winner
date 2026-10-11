using Ghumante.Core.Geo;
using Ghumante.Core.Horizon;
using Ghumante.World.Sky;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ghumante.Tests.EditMode
{
    /// <summary>
    /// The distant mountains in Unity (owner playtest #12): the skyline shader ships (World/Sky/Resources), and
    /// <see cref="SkylineRing"/> builds the ring from the committed sample's horizon tiles on every tier within the
    /// tier's triangle cap, as one unshadowed renderer drawn after the opaque world and placed for the floating origin.
    /// The bearings and elevations themselves are checked headlessly (core-tests HorizonReproTests, HorizonTests).
    /// </summary>
    public class HorizonSkylineTests
    {
        [Test]
        public void SkylineShaderShipsAndDrawsAfterTheOpaqueWorld()
        {
            Shader shader = Shader.Find(SkylineRing.ShaderName);
            Assert.NotNull(shader, "World/Sky/Resources/GhumanteSkyline.shader");
            Assert.IsTrue(shader.isSupported, "compiles for the editor's graphics API");
            var m = new Material(shader);
            try
            {
                Assert.AreEqual(2450, m.renderQueue, "after the opaque terrain (Geometry+450), before the skybox and transparents");
            }
            finally
            {
                Object.DestroyImmediate(m);
            }
        }

        [Test]
        public void RingBuildsFromTheSampleOnEveryTier()
        {
            double x, z;
            WorldFrame.LonLatToGame(85.3075, 27.7041, out x, out z); // Basantapur
            var origin = new WorldPos(System.Math.Round(x), 0f, System.Math.Round(z));
            var eye = new WorldPos(x, 1306f, z);
            var parent = new GameObject("Horizon test root");
            try
            {
                for (int tier = 0; tier <= 2; tier++)
                {
                    var ring = new SkylineRing(parent.transform, SampleRegion.Pack, tier);
                    try
                    {
                        Assert.IsTrue(ring.BuildNow(eye, origin, 1.5e-5f), "tier " + tier);
                        Assert.IsTrue(ring.Ready);
                        HorizonSettings s = HorizonSettings.ForTier(tier);
                        Assert.That(ring.Triangles, Is.InRange(s.TriangleCap / 6, s.TriangleCap), "tier " + tier);
                        Transform t = parent.transform.Find("Skyline");
                        Assert.NotNull(t);
                        var r = t.GetComponent<MeshRenderer>();
                        Assert.IsTrue(r.enabled);
                        Assert.AreEqual(ShadowCastingMode.Off, r.shadowCastingMode);
                        Assert.AreEqual(SkylineRing.ShaderName, r.sharedMaterial.shader.name);
                        Assert.AreEqual((float)(x - origin.X), t.localPosition.x, 0.01f, "placed for the floating origin");
                        Assert.AreEqual((float)(z - origin.Z), t.localPosition.z, 0.01f);
                        Mesh mesh = t.GetComponent<MeshFilter>().sharedMesh;
                        Assert.Greater(mesh.bounds.extents.x, 100000f, "never culled as a whole");
                        Vector4 p = Shader.GetGlobalVector("_GhSkylineParams");
                        Assert.AreEqual((float)s.HandoverM, p.x, 1f, "handover distance");
                        Assert.AreEqual(1306f, p.y, 0.5f, "eye altitude");
                    }
                    finally
                    {
                        ring.Dispose();
                    }
                    Assert.IsNull(parent.transform.Find("Skyline"), "disposed with the world");
                }
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }
    }
}
