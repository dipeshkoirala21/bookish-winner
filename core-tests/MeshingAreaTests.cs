using System;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class MeshingAreaTests
    {
        private static readonly TileId Leaf = new TileId(10, 516, 161);

        private static AreaRecord Square(AreaKind kind, int x0, int z0, int x1, int z1)
        {
            // Two counter-clockwise triangles, as the pipeline writes them.
            return new AreaRecord
            {
                Kind = kind, Vertices = new[] { x0, z0, x1, z0, x1, z1, x0, z1 }, Indices = new[] { 0, 1, 2, 0, 2, 3 },
                Rings = new[] { 0, 4 },
            };
        }

        private sealed class PlainSampler : IHeightSampler
        {
            private readonly TileHeightSampler _inner;

            public PlainSampler(TileHeightSampler inner)
            {
                _inner = inner;
            }

            public bool TryHeight(double x, double z, out float h)
            {
                return _inner.TryHeight(x, z, out h);
            }
        }

        private static double PlanArea(MeshData m)
        {
            double a = 0;
            for (int t = 0; t < m.TriangleCount; t++)
            {
                double nx, ny, nz;
                MeshingChecks.Facet(m, t, out nx, out ny, out nz);
                Assert.That(ny, Is.GreaterThan(0), "triangle " + t + " faces up");
                a += 0.5 * ny;
            }
            return a;
        }

        [Test]
        public void ParkIsDrapedExactlyAboveTheTerrain()
        {
            TileData t = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300 + 4 * Math.Sin((x - Leaf.X0) / 37.0) + 3 * Math.Cos((z - Leaf.Z0) / 23.0));
            t.Areas.Add(Square(AreaKind.Park, 10050, 20030, 25070, 31010));
            var s = new TileHeightSampler(t, 2);
            var m = new MeshData();
            var o = new AreaOptions();
            Assert.That(AreaMesher.Build(t, s, o, m), Is.EqualTo(1));
            MeshingChecks.AssertWellFormed(m, "park");
            Assert.That(PlanArea(m), Is.EqualTo(150.2 * 109.8).Within(1e-3 * 150 * 110));
            for (int tri = 0; tri < m.TriangleCount; tri++)
            {
                for (int k = 0; k < 3; k++)
                {
                    int a = m.Indices[3 * tri + k], b = m.Indices[3 * tri + (k + 1) % 3];
                    double mx = 0.5 * (m.Positions[3 * a] + m.Positions[3 * b]), mz = 0.5 * (m.Positions[3 * a + 2] + m.Positions[3 * b + 2]);
                    double my = 0.5 * (m.Positions[3 * a + 1] + m.Positions[3 * b + 1]);
                    float h;
                    Assert.That(s.TryHeight(Leaf.X0 + mx, Leaf.Z0 + mz, out h), Is.True);
                    Assert.That(my - h, Is.EqualTo(o.GreenLiftM).Within(1e-3), "edge midpoints on the lifted surface too");
                }
            }
            uint c = AreaStyle.Rgba(AreaKind.Park);
            Assert.That(m.Colors[0], Is.EqualTo((byte)(c >> 24)));
            // Shading follows the terrain's own normals.
            float nx, ny, nz;
            s.TrySmoothNormal(Leaf.X0 + m.Positions[0], Leaf.Z0 + m.Positions[2], out nx, out ny, out nz);
            Assert.That(m.Normals[0], Is.EqualTo(nx).Within(1e-5));
            Assert.That(m.Normals[1], Is.EqualTo(ny).Within(1e-5));
        }

        [Test]
        public void KindsMapToFamilies()
        {
            Assert.That(AreaStyle.Family(AreaKind.WaterLake), Is.EqualTo(AreaFamily.Water));
            Assert.That(AreaStyle.Family(AreaKind.WaterRiver), Is.EqualTo(AreaFamily.Water));
            Assert.That(AreaStyle.Family(AreaKind.Park), Is.EqualTo(AreaFamily.Green));
            Assert.That(AreaStyle.Family(AreaKind.Pitch), Is.EqualTo(AreaFamily.Green));
            Assert.That(AreaStyle.Family(AreaKind.Residential), Is.EqualTo(AreaFamily.Subtle));
            Assert.That(AreaStyle.Family(AreaKind.Protected), Is.EqualTo(AreaFamily.None));
            Assert.That(AreaStyle.Family(AreaKind.Military), Is.EqualTo(AreaFamily.None));
            foreach (AreaKind k in Enum.GetValues(typeof(AreaKind))) Assert.That(MeshColor.A(AreaStyle.Rgba(k)), Is.EqualTo(255));
            uint water = AreaStyle.Rgba(AreaKind.WaterLake);
            Assert.That(MeshColor.B(water), Is.GreaterThan(MeshColor.R(water) + 80), "water is blue");
            uint park = AreaStyle.Rgba(AreaKind.Park);
            Assert.That(MeshColor.G(park), Is.GreaterThan(MeshColor.R(park) + 40), "parks are green");

            TileData t = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300);
            t.Areas.Add(Square(AreaKind.WaterPond, 1000, 1000, 5000, 5000));
            t.Areas.Add(Square(AreaKind.Residential, 6000, 1000, 9000, 5000));
            t.Areas.Add(Square(AreaKind.Protected, 0, 0, 102400, 102400));
            var s = new TileHeightSampler(t, 1);
            var m = new MeshData();
            Assert.That(AreaMesher.Build(t, s, new AreaOptions(), m), Is.EqualTo(1), "subtle kinds are off by default");
            float y = Ght.Dequantize(Ght.Quantize(1300));
            for (int v = 0; v < m.VertexCount; v++) Assert.That(m.Positions[3 * v + 1], Is.EqualTo(y + 0.14f).Within(1e-3));
            Assert.That(AreaMesher.Build(t, s, new AreaOptions { IncludeSubtle = true }, new MeshData()), Is.EqualTo(2));
        }

        [Test]
        public void GenericSamplersSubdivide()
        {
            TileData t = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300 + 0.02 * (x - Leaf.X0));
            t.Areas.Add(Square(AreaKind.Meadow, 0, 0, 10000, 10000));
            var m = new MeshData();
            Assert.That(AreaMesher.Build(t, new PlainSampler(new TileHeightSampler(t, 1)), new AreaOptions { MaxEdgeM = 10 }, m), Is.EqualTo(1));
            MeshingChecks.AssertWellFormed(m, "subdivided");
            Assert.That(PlanArea(m), Is.EqualTo(10000).Within(1e-2));
            // Diagonal 141 m at 10 m: 15 subdivisions per triangle -> 225 triangles each.
            Assert.That(m.TriangleCount, Is.EqualTo(2 * 15 * 15));
            for (int v = 0; v < m.VertexCount; v++)
                Assert.That(m.Positions[3 * v + 1] - (1300 + 0.02 * m.Positions[3 * v]), Is.EqualTo(0.10).Within(0.1));
        }

        /// <summary>Real data: every drawn area covers exactly its triangles' plan area, lifted exactly above the
        /// rendered surface, inside the tile.</summary>
        [Test]
        public void SampleAreasCoverTheirTrianglesOnTheSurface()
        {
            var o = new AreaOptions { IncludeSubtle = true };
            int drawnTotal = 0;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                var s = new TileHeightSampler(t, 2);
                foreach (AreaRecord a in t.Areas)
                {
                    var one = new TileData { Tile = t.Tile, HeightsN = t.HeightsN, HeightsQ = t.HeightsQ };
                    one.Areas.Add(a);
                    var m = new MeshData();
                    int drawn = AreaMesher.Build(one, s, o, m);
                    Assert.That(drawn, Is.EqualTo(AreaMesher.IsDrawn(a, o) ? 1 : 0), id + " area " + a.OsmRef);
                    if (drawn == 0) continue;
                    drawnTotal++;
                    MeshingChecks.AssertWellFormed(m, id + " area " + a.OsmRef);
                    double expected = 0;
                    for (int k = 0; k + 2 < a.Indices.Length; k += 3)
                    {
                        int i0 = a.Indices[k], i1 = a.Indices[k + 1], i2 = a.Indices[k + 2];
                        double x0 = a.Vertices[2 * i0] / 100.0, z0 = a.Vertices[2 * i0 + 1] / 100.0;
                        double cr = (a.Vertices[2 * i1] / 100.0 - x0) * (a.Vertices[2 * i2 + 1] / 100.0 - z0) -
                                    (a.Vertices[2 * i1 + 1] / 100.0 - z0) * (a.Vertices[2 * i2] / 100.0 - x0);
                        Assert.That(cr, Is.GreaterThanOrEqualTo(0), "AREA triangles are counter-clockwise");
                        expected += 0.5 * cr;
                    }
                    Assert.That(PlanArea(m), Is.EqualTo(expected).Within(1e-4 * expected + 0.05), id + " area " + a.OsmRef);
                    float lift = AreaStyle.Family(a.Kind) == AreaFamily.Water ? o.WaterLiftM : AreaStyle.Family(a.Kind) == AreaFamily.Green ? o.GreenLiftM : o.SubtleLiftM;
                    for (int v = 0; v < m.VertexCount; v++)
                    {
                        float x = m.Positions[3 * v], z = m.Positions[3 * v + 2], h;
                        if (x < -1e-3 || z < -1e-3 || x > 1024.001 || z > 1024.001) Assert.Fail(id + ": area vertex outside the tile");
                        s.TryHeightClamped(id.X0 + x, id.Z0 + z, out h);
                        if (Math.Abs(m.Positions[3 * v + 1] - h - lift) > 1e-3) Assert.Fail(id + ": area vertex off the lifted surface");
                    }
                }
            }
            TestContext.WriteLine("areas drawn: " + drawnTotal);
            Assert.That(drawnTotal, Is.GreaterThan(500));
        }
    }
}
