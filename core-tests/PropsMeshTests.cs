using System;
using System.Collections.Generic;
using System.Globalization;
using Ghumante.Core.Generators.Placement;
using Ghumante.Core.Generators.Props;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The street furniture kit (package props, docs/research/w2/ref_props.md): every kind at every LOD within its
    /// triangle budget, with UV0 (material channel + baked AO) on every vertex, finite unit normals, standing on the
    /// ground at its default height, deterministic, and the visual-check dump (set GHUMANTE_PREVIEW_DIR).
    /// </summary>
    public class PropsMeshTests
    {
        private static IEnumerable<StreetPropKind> Kinds()
        {
            for (int k = 0; k < StreetPropMesher.Kinds; k++) yield return (StreetPropKind)k;
        }

        [Test]
        public void EveryKindAtEveryLodFitsItsBudgetAndWritesChannelsAndAo()
        {
            var m = new MeshData();
            var c = CultureInfo.InvariantCulture;
            var over = new List<string>();
            foreach (StreetPropKind k in Kinds())
                for (int lod = 0; lod < StreetPropMesher.Lods; lod++)
                {
                    m.Clear();
                    int t = StreetPropMesher.Build(k, lod, m);
                    if (t > StreetPropMesher.Budget[lod]) over.Add(k + " LOD" + lod + " " + t);
                }
            Assert.That(over, Is.Empty, "over budget");
            foreach (StreetPropKind k in Kinds())
            {
                int prev = int.MaxValue;
                for (int lod = 0; lod < StreetPropMesher.Lods; lod++)
                {
                    m.Clear();
                    int tris = StreetPropMesher.Build(k, lod, m);
                    TestContext.WriteLine(string.Format(c, "{0} LOD{1}: {2} tris, {3} verts", k, lod, tris, m.VertexCount));
                    Assert.That(tris, Is.EqualTo(m.TriangleCount));
                    Assert.That(tris, Is.GreaterThan(0), k + " LOD" + lod);
                    Assert.That(tris, Is.LessThanOrEqualTo(StreetPropMesher.Budget[lod]), k + " LOD" + lod);
                    Assert.That(tris, Is.LessThanOrEqualTo(prev), k + ": a coarser level never has more triangles");
                    prev = tris;
                    Assert.That(m.HasUv0, Is.True, k + " writes UV0");
                    bool shaded = false;
                    for (int v = 0; v < m.VertexCount; v++)
                    {
                        float u = m.Uv0[2 * v], ao = m.Uv0[2 * v + 1];
                        Assert.That(u, Is.InRange(0f, 25f), k + ": a material channel");
                        Assert.That(u, Is.EqualTo((float)Math.Round(u)), k + ": channel is an integer");
                        Assert.That(ao, Is.InRange(0f, 1f), k + ": AO in [0, 1]");
                        if (ao < 0.95f) shaded = true;
                        float nx = m.Normals[3 * v], ny = m.Normals[3 * v + 1], nz = m.Normals[3 * v + 2];
                        Assert.That(Math.Abs(nx * nx + ny * ny + nz * nz - 1), Is.LessThan(1e-3), k + ": unit normals");
                        for (int a = 0; a < 3; a++) Assert.That(float.IsFinite(m.Positions[3 * v + a]), Is.True);
                    }
                    Assert.That(shaded, Is.True, k + " LOD" + lod + ": AO is baked");
                    for (int i = 0; i < m.IndexCount; i++) Assert.That(m.Indices[i], Is.InRange(0, m.VertexCount - 1));
                }
            }
        }

        [Test]
        public void PropsStandOnTheGroundAtTheirDefaultHeight()
        {
            var m = new MeshData();
            foreach (StreetPropKind k in Kinds())
                for (int lod = 0; lod < StreetPropMesher.Lods; lod++)
                {
                    m.Clear();
                    StreetPropMesher.Build(k, lod, m);
                    float x0, y0, z0, x1, y1, z1;
                    m.GetBounds(out x0, out y0, out z0, out x1, out y1, out z1);
                    float h = StreetPropMesher.DefaultHeight(k);
                    Assert.That(y0, Is.EqualTo(0f).Within(0.02f), k + " LOD" + lod + ": stands on y = 0");
                    float tol = k == StreetPropKind.Helipad || k == StreetPropKind.ParkingPosition ? 0.03f : 0.12f * h + 0.05f;
                    Assert.That(y1, Is.EqualTo(h).Within(tol), k + " LOD" + lod + ": top at the default height");
                }
        }

        [Test]
        public void LampArmsReachOverTheRoadAndGatesLeaveTheCorridorOpen()
        {
            var m = new MeshData();
            float x0, y0, z0, x1, y1, z1;
            foreach (StreetPropKind lamp in new[] { StreetPropKind.StreetLamp, StreetPropKind.StreetLampSolar, StreetPropKind.StreetLampHeritage })
            {
                m.Clear();
                StreetPropMesher.Build(lamp, 0, m);
                m.GetBounds(out x0, out y0, out z0, out x1, out y1, out z1);
                Assert.That(z1, Is.GreaterThan(lamp == StreetPropKind.StreetLampHeritage ? 0.8f : 1.5f), lamp + ": the head reaches out along +Z");
                Assert.That(Math.Max(-x0, x1), Is.LessThan(lamp == StreetPropKind.StreetLampSolar ? 0.7f : 0.4f), lamp + ": a slim pole");
            }
            // The gate: nothing inside |x| < 2.4 m (a 4.8 m corridor) and nothing overhead.
            m.Clear();
            StreetPropMesher.Build(StreetPropKind.Gate, 0, m);
            for (int v = 0; v < m.VertexCount; v++)
                Assert.That(Math.Abs(m.Positions[3 * v]), Is.GreaterThanOrEqualTo(2.45f), "gate geometry stays out of the corridor");
            // The falcha clears a standing adult under its eaves.
            m.Clear();
            StreetPropMesher.Build(StreetPropKind.BusShelter, 0, m);
            m.GetBounds(out x0, out y0, out z0, out x1, out y1, out z1);
            Assert.That(x1 - x0, Is.InRange(6.5f, 7.6f), "falcha length with its eaves");
            Assert.That(z1 - z0, Is.InRange(4.0f, 5.0f), "falcha depth with its eaves");
        }

        [Test]
        public void TheKitIsDeterministic()
        {
            var a = new MeshData();
            var b = new MeshData();
            foreach (StreetPropKind k in Kinds())
            {
                a.Clear();
                b.Clear();
                StreetPropMesher.Build(k, 0, a);
                StreetPropMesher.Build(k, 0, b);
                Assert.That(a.VertexCount, Is.EqualTo(b.VertexCount));
                for (int i = 0; i < 3 * a.VertexCount; i++) Assert.That(a.Positions[i], Is.EqualTo(b.Positions[i]));
                for (int i = 0; i < 2 * a.VertexCount; i++) Assert.That(a.Uv0[i], Is.EqualTo(b.Uv0[i]));
            }
        }

        [Test]
        public void TheTankBodyTakesTheInstanceTintAndEverythingElseIsFixed()
        {
            var m = new MeshData();
            foreach (StreetPropKind k in Kinds())
            {
                m.Clear();
                StreetPropMesher.Build(k, 0, m);
                int tinted = 0;
                for (int v = 0; v < m.VertexCount; v++)
                    if (m.Colors[4 * v + 3] == 255) tinted++;
                if (k == StreetPropKind.StorageTank) Assert.That(tinted, Is.GreaterThan(m.VertexCount / 3), "the tank body is tinted");
                else Assert.That(tinted, Is.EqualTo(0), k + ": fixed colours (alpha 0)");
            }
        }

        /// <summary>Visual check: every kind at LOD0 in a row, and LOD0/1/2 of each street kind side by side
        /// (GHUMANTE_PREVIEW_DIR, then tools/mesh-preview/render.py).</summary>
        [Test]
        public void DumpKit()
        {
            if (!ObjDump.Enabled) Assert.Pass("set " + ObjDump.EnvVar + " to dump");
            var one = new MeshData();
            foreach (StreetPropKind k in Kinds())
            {
                var lods = new MeshData();
                double x = 0;
                for (int lod = 0; lod < StreetPropMesher.Lods; lod++)
                {
                    one.Clear();
                    StreetPropMesher.Build(k, lod, one);
                    float x0, y0, z0, x1, y1, z1;
                    one.GetBounds(out x0, out y0, out z0, out x1, out y1, out z1);
                    ObjDump.Append(lods, one, (float)(x - x0), 0, 0);
                    x += (x1 - x0) + 0.6;
                    if (lod == 0) ObjDump.Write(one, "props/kind_" + k + ".obj");
                }
                ObjDump.Write(lods, "props/lods_" + k + ".obj");
            }
        }
    }
}
