using System;
using System.Collections.Generic;
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
                    Assert.That(my - h, Is.EqualTo(AreaMesher.LiftOf(t.Areas[0], o)).Within(1e-3), "edge midpoints on the lifted surface too");
                }
            }
            // The detail look varies the park colour by a few percent around its style colour and tags it as grass.
            uint c = AreaStyle.Rgba(AreaKind.Park);
            Assert.That(m.HasUv0, Is.True);
            for (int v = 0; v < m.VertexCount; v++)
            {
                Assert.That((double)m.Colors[4 * v + 1], Is.EqualTo((double)(byte)(c >> 16)).Within(0.12 * 255), "park green");
                Assert.That(m.Uv0[2 * v], Is.EqualTo((float)MaterialChannel.Grass));
                Assert.That(m.Uv0[2 * v + 1], Is.InRange(0.75f, 1f), "AO");
            }
            var plain = new MeshData();
            AreaMesher.Build(t, s, new AreaOptions { Detail = false }, plain);
            Assert.That(plain.Colors[0], Is.EqualTo((byte)(c >> 24)), "Detail = false keeps the flat style colour");
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
            Assert.That(AreaMesher.Build(t, s, new AreaOptions { WaterBanks = false }, m), Is.EqualTo(1), "subtle kinds are off by default");
            float y = Ght.Dequantize(Ght.Quantize(1300));
            // Water lift 0.14 m, plus the pond's kind rank (3 × 4 mm) and size rank (a 1 600 m² pond: 3 × 1 mm).
            Assert.That(AreaMesher.LiftOf(t.Areas[0], new AreaOptions()), Is.EqualTo(0.14f + 3 * 0.004f + 3 * 0.001f).Within(1e-6));
            for (int v = 0; v < m.VertexCount; v++) Assert.That(m.Positions[3 * v + 1], Is.EqualTo(y + 0.155f).Within(1e-3));
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
            // Banks and field lines are extra strips outside the records (tested on their own below).
            var o = new AreaOptions { IncludeSubtle = true, WaterBanks = false, FieldLines = false };
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
                    float lift = AreaMesher.LiftOf(a, o);
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

        /// <summary>
        /// Ponds get a lighter shallow rim and a mud bank strip outside every shore edge, lifted below the water and
        /// the green areas; a river's bank is gravel (Stone channel).
        /// </summary>
        [Test]
        public void WaterHasARimAndABank()
        {
            TileData t = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300 + 2 * Math.Sin((x - Leaf.X0) / 29.0));
            t.Areas.Add(Square(AreaKind.WaterPond, 10000, 10000, 14000, 14000));
            var s = new TileHeightSampler(t, 1);
            var o = new AreaOptions();
            var m = new MeshData();
            Assert.That(AreaMesher.Build(t, s, o, m), Is.EqualTo(1));
            MeshingChecks.AssertWellFormed(m, "pond");
            Assert.That(o.BankLiftM, Is.LessThan(o.GreenLiftM));
            float lift = AreaMesher.LiftOf(t.Areas[0], o);
            double bankArea = 0, waterArea = 0;
            int rim = -1, mid = -1;
            for (int tri = 0; tri < m.TriangleCount; tri++)
            {
                double nx, ny, nz;
                MeshingChecks.Facet(m, tri, out nx, out ny, out nz);
                Assert.That(ny, Is.GreaterThan(0), "faces up");
                int v = m.Indices[3 * tri];
                float h;
                s.TryHeightClamped(Leaf.X0 + m.Positions[3 * v], Leaf.Z0 + m.Positions[3 * v + 2], out h);
                double above = m.Positions[3 * v + 1] - h;
                if (Math.Abs(above - o.BankLiftM) < 1e-3)
                {
                    bankArea += 0.5 * ny;
                    Assert.That(m.Uv0[2 * v], Is.EqualTo((float)MaterialChannel.Dirt));
                    for (int k = 0; k < 3; k++)
                    {
                        int w = m.Indices[3 * tri + k];
                        double x = m.Positions[3 * w], z = m.Positions[3 * w + 2];
                        bool inside = x > 100.01 && x < 139.99 && z > 100.01 && z < 139.99;
                        Assert.That(inside, Is.False, "the bank lies outside the water");
                        Assert.That(x, Is.InRange(100 - o.BankWidthM - 1e-3, 140 + o.BankWidthM + 1e-3));
                    }
                }
                else
                {
                    Assert.That(above, Is.EqualTo(lift).Within(1e-3));
                    Assert.That(m.Uv0[2 * v], Is.EqualTo((float)MaterialChannel.Water));
                    waterArea += 0.5 * ny;
                    for (int k = 0; k < 3; k++)
                    {
                        int w = m.Indices[3 * tri + k];
                        double x = m.Positions[3 * w], z = m.Positions[3 * w + 2];
                        if (Math.Abs(x - 100) < 1e-3 && Math.Abs(z - 120) < 4.01) rim = w;
                        if (Math.Abs(x - 120) < 4.01 && Math.Abs(z - 120) < 4.01) mid = w;
                    }
                }
            }
            Assert.That(waterArea, Is.EqualTo(1600).Within(0.5));
            Assert.That(bankArea, Is.EqualTo(4 * 40 * o.BankWidthM).Within(1.0), "a strip outside each of the four shore edges");
            Assert.That(rim, Is.GreaterThanOrEqualTo(0));
            Assert.That(mid, Is.GreaterThanOrEqualTo(0));
            Assert.That(m.Colors[4 * rim] + m.Colors[4 * rim + 1], Is.GreaterThan(m.Colors[4 * mid] + m.Colors[4 * mid + 1] + 40), "shallow rim lighter than the middle");

            var river = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300);
            river.Areas.Add(Square(AreaKind.WaterRiver, 10000, 10000, 30000, 14000));
            var rm = new MeshData();
            AreaMesher.Build(river, new TileHeightSampler(river, 1), o, rm);
            int stone = 0;
            for (int v = 0; v < rm.VertexCount; v++)
                if (rm.Uv0[2 * v] == (float)MaterialChannel.Stone) stone++;
            Assert.That(stone, Is.GreaterThan(8), "gravel river banks");
            var none = new MeshData();
            AreaMesher.Build(river, new TileHeightSampler(river, 1), new AreaOptions { WaterBanks = false }, none);
            Assert.That(none.TriangleCount, Is.LessThan(rm.TriangleCount));
        }

        /// <summary>
        /// Cropland on a slope gets terrace lines (a darker riser band under a light lip at every
        /// <see cref="FieldPattern.TerraceStepM"/> of height, at world-fixed levels); flat cropland gets plot bunds;
        /// all lie exactly <see cref="AreaOptions.FieldLiftM"/> above the rendered surface, face up, stay in the tile,
        /// respect the triangle cap and are absent on non-crop ground.
        /// </summary>
        [Test]
        public void CroplandGetsTerracesOnSlopesAndBundsOnTheFlat()
        {
            const double Slope = 0.3;
            TileData hill = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300 + Slope * (x - Leaf.X0), 129, Biome.HillTerraces);
            var s = new TileHeightSampler(hill, 1);
            var o = new AreaOptions { TerraceLips = true, MaxFieldLineTris = 1000000 };
            var m = new MeshData();
            Assert.That(AreaMesher.Build(hill, s, o, m), Is.EqualTo(0), "no area records, only field lines");
            MeshingChecks.AssertWellFormed(m, "terraces");
            Assert.That(m.TriangleCount, Is.GreaterThan(1000));
            uint riser = MeshColor.FromHex(FieldPattern.RiserColour(o.Season)), lip = MeshColor.FromHex(FieldPattern.LipColour(o.Season));
            double riserArea = 0, lipArea = 0, all = 0;
            for (int tri = 0; tri < m.TriangleCount; tri++)
            {
                double nx, ny, nz;
                MeshingChecks.Facet(m, tri, out nx, out ny, out nz);
                Assert.That(ny, Is.GreaterThan(0), "faces up");
                int v = m.Indices[3 * tri];
                uint c = (uint)(m.Colors[4 * v] << 24 | m.Colors[4 * v + 1] << 16 | m.Colors[4 * v + 2] << 8 | m.Colors[4 * v + 3]);
                if (c == riser) riserArea += 0.5 * ny;
                else if (c == lip) lipArea += 0.5 * ny;
                all += 0.5 * ny;
                for (int k = 0; k < 3; k++)
                {
                    int w = m.Indices[3 * tri + k];
                    float x = m.Positions[3 * w], z = m.Positions[3 * w + 2], h;
                    Assert.That(x, Is.InRange(-1e-3, 1024.001));
                    Assert.That(z, Is.InRange(-1e-3, 1024.001));
                    s.TryHeightClamped(Leaf.X0 + x, Leaf.Z0 + z, out h);
                    Assert.That(m.Positions[3 * w + 1] - h, Is.EqualTo(o.FieldLiftM).Within(2e-3), "on the lifted surface");
                }
            }
            Assert.That(riserArea, Is.GreaterThan(0));
            // Thin risers: a line RiserPlanM wide in plan below every terrace level (TerraceStepM / Slope apart), the
            // lip a thinner line on top; never wide stripes of riser colour.
            double riserH = Math.Min(FieldPattern.RiserM, FieldPattern.RiserPlanM * Slope), lipH = Math.Min(FieldPattern.LipM, 0.3 * Slope);
            Assert.That(riserArea / lipArea, Is.EqualTo(riserH / lipH).Within(0.05 * riserH / lipH));
            Assert.That(riserArea / (1024.0 * 1024.0), Is.EqualTo(FieldPattern.RiserPlanM * Slope / FieldPattern.TerraceStepM).Within(0.01));
            Assert.That(riserArea + lipArea, Is.EqualTo(all).Within(1e-6 * all));
            // The bands sit at world-fixed heights: every riser vertex is within RiserM below a multiple of the step.
            for (int v = 0; v < m.VertexCount; v++)
            {
                uint c = (uint)(m.Colors[4 * v] << 24 | m.Colors[4 * v + 1] << 16 | m.Colors[4 * v + 2] << 8 | m.Colors[4 * v + 3]);
                if (c != riser) continue;
                double y = m.Positions[3 * v + 1] - o.FieldLiftM, level = Math.Ceiling((y - 2e-3) / FieldPattern.TerraceStepM) * FieldPattern.TerraceStepM;
                Assert.That(level - y, Is.LessThanOrEqualTo(FieldPattern.RiserM + 2e-3));
            }
            // Over budget, whole world-fixed blocks drop their lines evenly: the kept lines still span the tile.
            var capped = new MeshData();
            var co = new AreaOptions { MaxFieldLineTris = 3000 };
            AreaMesher.Build(hill, s, co, capped);
            Assert.That(capped.TriangleCount, Is.InRange(1500, co.MaxFieldLineTris + 16), "capped near the budget");
            int[] quarter = new int[4];
            for (int v = 0; v < capped.VertexCount; v++) quarter[Math.Min(3, (int)(capped.Positions[3 * v + 2] / 256f))]++;
            foreach (int qn in quarter) Assert.That(qn, Is.GreaterThan(capped.VertexCount / 10), "lines in every quarter of the tile");
            var noLips = new MeshData();
            AreaMesher.Build(hill, s, new AreaOptions { MaxFieldLineTris = 1000000 }, noLips);
            Assert.That(noLips.TriangleCount, Is.LessThan(0.75 * m.TriangleCount), "lips off by default (fewer triangles)");

            TileData flat = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300, 129, Biome.ValleyCropland);
            var fm = new MeshData();
            var fs = new TileHeightSampler(flat, 1);
            AreaMesher.Build(flat, fs, new AreaOptions { MaxFieldLineTris = 1000000 }, fm);
            double bunds = 0;
            for (int tri = 0; tri < fm.TriangleCount; tri++)
            {
                double nx, ny, nz;
                MeshingChecks.Facet(fm, tri, out nx, out ny, out nz);
                Assert.That(ny, Is.GreaterThan(0));
                bunds += 0.5 * ny;
            }
            // 0.6 m bunds every PlotU and PlotV metres: about 0.6/26 + 0.6/17 of the plan.
            double share = bunds / (1024.0 * 1024.0), expected = 0.6 / FieldPattern.PlotU + 0.6 / FieldPattern.PlotV;
            Assert.That(share, Is.EqualTo(expected).Within(0.25 * expected));

            TileData town = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300 + Slope * (x - Leaf.X0));
            var tm = new MeshData();
            AreaMesher.Build(town, new TileHeightSampler(town, 1), o, tm);
            Assert.That(tm.TriangleCount, Is.EqualTo(0), "no field lines in town");
            var off = new MeshData();
            AreaMesher.Build(hill, s, new AreaOptions { FieldLines = false }, off);
            Assert.That(off.TriangleCount, Is.EqualTo(0));
        }

        /// <summary>The lift a record had before kind and size ranks (family only).</summary>
        private static float FamilyLift(AreaRecord a, AreaOptions o)
        {
            AreaFamily f = AreaStyle.Family(a.Kind);
            return f == AreaFamily.Water ? o.WaterLiftM : f == AreaFamily.Green ? o.GreenLiftM : o.SubtleLiftM;
        }

        /// <summary>
        /// Overlapping AREA records of one family used to share the family lift and z-fight (a pitch in a park, a
        /// park in a forest). Rasterised at 2 m over every level-10 tile of the real sample, the plan area where two
        /// drawn records of different kinds overlap at the same lift is now zero, the area where any two records do is
        /// a small remainder (same kind and size class), the families keep their order, and a pitch lies above
        /// a park above a forest.
        /// </summary>
        [Test]
        public void OverlappingAreasOfOneFamilyAreNotCoplanar()
        {
            var o = new AreaOptions();
            Assert.That(9 * o.KindLiftStepM + 3 * o.SizeLiftStepM, Is.LessThan(o.GreenLiftM - o.SubtleLiftM));
            Assert.That(9 * o.KindLiftStepM + 3 * o.SizeLiftStepM, Is.LessThan(o.WaterLiftM - o.GreenLiftM));
            Assert.That(AreaStyle.KindRank(AreaKind.Pitch), Is.GreaterThan(AreaStyle.KindRank(AreaKind.Park)));
            Assert.That(AreaStyle.KindRank(AreaKind.Park), Is.GreaterThan(AreaStyle.KindRank(AreaKind.Meadow)));
            Assert.That(AreaStyle.KindRank(AreaKind.Meadow), Is.GreaterThan(AreaStyle.KindRank(AreaKind.Forest)));

            const double Cell = 2.0;
            const int N = 512;
            double before = 0, after = 0, afterDifferentKinds = 0;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                var cells = new Dictionary<int, List<int>>();
                for (int r = 0; r < t.Areas.Count; r++)
                {
                    AreaRecord a = t.Areas[r];
                    if (!AreaMesher.IsDrawn(a, o)) continue;
                    float lift = AreaMesher.LiftOf(a, o);
                    Assert.That(lift, Is.GreaterThanOrEqualTo(FamilyLift(a, o)));
                    Assert.That(lift, Is.LessThan(FamilyLift(a, o) + 0.04f));
                    var mine = new HashSet<int>();
                    for (int k = 0; k + 2 < a.Indices.Length; k += 3)
                    {
                        int i0 = a.Indices[k], i1 = a.Indices[k + 1], i2 = a.Indices[k + 2];
                        double x0 = a.Vertices[2 * i0] / 100.0, z0 = a.Vertices[2 * i0 + 1] / 100.0;
                        double x1 = a.Vertices[2 * i1] / 100.0, z1 = a.Vertices[2 * i1 + 1] / 100.0;
                        double x2 = a.Vertices[2 * i2] / 100.0, z2 = a.Vertices[2 * i2 + 1] / 100.0;
                        int cx0 = Math.Max(0, (int)(Math.Min(x0, Math.Min(x1, x2)) / Cell)), cx1 = Math.Min(N - 1, (int)(Math.Max(x0, Math.Max(x1, x2)) / Cell));
                        int cz0 = Math.Max(0, (int)(Math.Min(z0, Math.Min(z1, z2)) / Cell)), cz1 = Math.Min(N - 1, (int)(Math.Max(z0, Math.Max(z1, z2)) / Cell));
                        for (int cz = cz0; cz <= cz1; cz++)
                        {
                            for (int cx = cx0; cx <= cx1; cx++)
                            {
                                double px = (cx + 0.5) * Cell, pz = (cz + 0.5) * Cell;
                                double e0 = (x1 - x0) * (pz - z0) - (z1 - z0) * (px - x0);
                                double e1 = (x2 - x1) * (pz - z1) - (z2 - z1) * (px - x1);
                                double e2 = (x0 - x2) * (pz - z2) - (z0 - z2) * (px - x2);
                                bool inside = e0 >= 0 && e1 >= 0 && e2 >= 0 || e0 <= 0 && e1 <= 0 && e2 <= 0;
                                if (inside) mine.Add(cz * N + cx);
                            }
                        }
                    }
                    foreach (int cell in mine)
                    {
                        List<int> list;
                        if (!cells.TryGetValue(cell, out list)) cells[cell] = list = new List<int>();
                        list.Add(r);
                    }
                }
                foreach (List<int> list in cells.Values)
                {
                    bool sameBefore = false, sameAfter = false, sameAfterKinds = false;
                    for (int i = 0; i < list.Count; i++)
                    {
                        for (int j = i + 1; j < list.Count; j++)
                        {
                            AreaRecord a = t.Areas[list[i]], b = t.Areas[list[j]];
                            if (FamilyLift(a, o) == FamilyLift(b, o)) sameBefore = true;
                            if (AreaMesher.LiftOf(a, o) == AreaMesher.LiftOf(b, o))
                            {
                                sameAfter = true;
                                if (a.Kind != b.Kind) sameAfterKinds = true;
                            }
                        }
                    }
                    if (sameBefore) before += Cell * Cell;
                    if (sameAfter) after += Cell * Cell;
                    if (sameAfterKinds) afterDifferentKinds += Cell * Cell;
                }
            }
            TestContext.WriteLine("coplanar overlap of drawn areas: before {0:0} m², after {1:0} m² ({2:0} m² of different kinds)", before, after, afterDifferentKinds);
            Assert.That(before, Is.GreaterThan(20000), "the sample has same-family overlaps");
            Assert.That(afterDifferentKinds, Is.EqualTo(0));
            Assert.That(after, Is.LessThan(before * 0.5));
        }
        [Test]
        public void TerraceClassFollowsTheSmoothedSlopeNotSingleTriangles()
        {
            // A terraced hill at 0.26 with 2 m bumps every 40 m: single triangles range from about 0.0 to 0.55, but the
            // hillside as a whole is terraced, so it gets riser lines only (no bund grid in patches between them).
            TileData hill = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300 + 0.26 * (x - Leaf.X0) + 2.0 * Math.Sin((x - Leaf.X0) * 0.157) * Math.Cos((z - Leaf.Z0) * 0.13),
                                                        129, Biome.HillTerraces);
            var o = new AreaOptions { MaxFieldLineTris = 1000000 };
            var m = new MeshData();
            AreaMesher.Build(hill, new TileHeightSampler(hill, 1), o, m);
            uint riser = MeshColor.FromHex(FieldPattern.RiserColour(o.Season));
            int risers = 0, other = 0;
            for (int v = 0; v < m.VertexCount; v++)
            {
                uint c = (uint)(m.Colors[4 * v] << 24 | m.Colors[4 * v + 1] << 16 | m.Colors[4 * v + 2] << 8 | m.Colors[4 * v + 3]);
                // Within 24 m of the tile's edge the slope is taken one-sided (no heights beyond the tile).
                float x = m.Positions[3 * v], z = m.Positions[3 * v + 2];
                if (x < 24 || z < 24 || x > 1000 || z > 1000) continue;
                if (c == riser) risers++;
                else other++;
            }
            Assert.That(risers, Is.GreaterThan(1000));
            Assert.That(other, Is.EqualTo(0), "no bund fragments on a terraced hillside");
            // Gentle land (0.12, below the terrace slope) is bunded plots, not wide risers.
            TileData gentle = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300 + 0.12 * (x - Leaf.X0), 129, Biome.ValleyCropland);
            var gm = new MeshData();
            AreaMesher.Build(gentle, new TileHeightSampler(gentle, 1), o, gm);
            for (int v = 0; v < gm.VertexCount; v++)
                Assert.That(gm.Colors[4 * v] << 24 | gm.Colors[4 * v + 1] << 16 | gm.Colors[4 * v + 2] << 8 | gm.Colors[4 * v + 3], Is.Not.EqualTo((int)riser));
            Assert.That(gm.TriangleCount, Is.GreaterThan(1000), "bunds");
        }

        [Test]
        public void RisersAreGrassInTheGreenSeasonsAndDryInWinter()
        {
            TileData hill = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300 + 0.3 * (x - Leaf.X0), 129, Biome.HillTerraces);
            var s = new TileHeightSampler(hill, 1);
            foreach (Season season in new[] { Season.Autumn, Season.Monsoon, Season.Winter })
            {
                var m = new MeshData();
                AreaMesher.Build(hill, s, new AreaOptions { Season = season, MaxFieldLineTris = 1000000 }, m);
                Assert.That(m.HasUv0, Is.True);
                var want = season == Season.Winter ? MaterialChannel.Dirt : MaterialChannel.Grass;
                for (int v = 0; v < m.VertexCount; v += 7) Assert.That((MaterialChannel)(int)m.Uv0[2 * v], Is.EqualTo(want), season.ToString());
            }
        }

        [Test]
        public void FieldLinesStayInsideTheTierBudget()
        {
            // A whole cropland tile: terraced hill in the west, flat paddy in the east.
            TileData t = MeshingChecks.SyntheticTile(Leaf, (x, z) => x - Leaf.X0 < 512 ? 1300 + 0.3 * (x - Leaf.X0) : 1453.6, 129, Biome.ValleyCropland);
            int[] terrainBudget = { 30000, 90000, 160000 };
            for (int tier = 0; tier < 3; tier++)
                foreach (int step in new[] { 1, 2 })
                {
                    var areas = new MeshData();
                    var sampler = TileHeightSampler.ForArea(t, Leaf, step);
                    AreaMesher.Build(t, sampler, AreaOptions.ForTier(tier), areas);
                    var terrain = new MeshData();
                    TerrainMesher.Build(t, Leaf, new TerrainOptions { Step = step }, terrain);
                    Assert.That(areas.TriangleCount, Is.LessThanOrEqualTo(AreaOptions.FieldLineCap(tier, step) + 16), "tier " + tier + " step " + step);
                    Assert.That(areas.TriangleCount, Is.GreaterThan(AreaOptions.FieldLineCap(tier, step) / 3), "lines still drawn, tier " + tier + " step " + step);
                    // A coarse tile of the terrain ring stays well inside the tier's terrain slice with its field lines.
                    if (step == 2) Assert.That(terrain.TriangleCount + areas.TriangleCount, Is.LessThan(terrainBudget[tier] / 2), "tier " + tier);
                    TestContext.WriteLine("tier " + tier + " step " + step + ": terrain " + terrain.TriangleCount + ", field lines " + areas.TriangleCount);
                }
            Assert.That(new AreaOptions().MaxFieldLineTris, Is.EqualTo(AreaOptions.FieldLineCap(1)), "default: Mid");
        }

        [Test]
        public void NoFieldsUnderAParadeGroundParkOrHouses()
        {
            // Valley cropland by the biome raster, but a parade ground (military + meadow, as Tundikhel), a pitch and a
            // residential block are mapped over parts of it; a farmland plot inside the residential block stays a field.
            TileData t = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300, 129, Biome.ValleyCropland);
            t.Areas.Add(Square(AreaKind.Military, 10000, 10000, 40000, 40000));
            t.Areas.Add(Square(AreaKind.Meadow, 12000, 12000, 38000, 38000));
            t.Areas.Add(Square(AreaKind.Residential, 60000, 60000, 90000, 90000));
            t.Areas.Add(Square(AreaKind.Farmland, 70000, 70000, 80000, 80000));
            CropLand crop = CropLand.For(t);
            Assert.That(crop.IsCrop(Biome.ValleyCropland, 250, 250), Is.False, "parade ground");
            Assert.That(crop.IsCrop(Biome.ValleyCropland, 650, 650), Is.False, "houses");
            Assert.That(crop.IsCrop(Biome.ValleyCropland, 750, 750), Is.True, "a field mapped among the houses");
            Assert.That(crop.IsCrop(Biome.ValleyCropland, 500, 200), Is.True, "open cropland");
            Assert.That(crop.IsCrop(Biome.UrbanDense, 500, 200), Is.False);
            // The field lines and the terrain's crop patchwork follow it.
            var m = new MeshData();
            AreaMesher.Build(t, new TileHeightSampler(t, 1), new AreaOptions { MaxFieldLineTris = 1000000, IncludeSubtle = false }, m);
            uint bund = MeshColor.FromHex(0x8FB060u);
            for (int v = 0; v < m.VertexCount; v++)
            {
                uint c = (uint)(m.Colors[4 * v] << 24 | m.Colors[4 * v + 1] << 16 | m.Colors[4 * v + 2] << 8 | m.Colors[4 * v + 3]);
                if (c != bund) continue;
                float x = m.Positions[3 * v], z = m.Positions[3 * v + 2];
                Assert.That(x > 104 && x < 396 && z > 104 && z < 396, Is.False, "a bund on the parade ground at " + x + ", " + z);
                Assert.That(x > 604 && x < 696 && z > 604 && z < 696, Is.False, "a bund among the houses at " + x + ", " + z);
            }
            var terrain = new MeshData();
            TerrainMesher.Build(t, Leaf, new TerrainOptions { Step = 1, SkirtDepthM = 0 }, terrain);
            var plain = new MeshData();
            TileData open = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300, 129, Biome.ValleyCropland);
            TerrainMesher.Build(open, Leaf, new TerrainOptions { Step = 1, SkirtDepthM = 0 }, plain);
            int side = 129, changed = 0, inside = 0;
            for (int k = 0; k < side; k++)
                for (int l = 0; l < side; l++)
                {
                    int v = k * side + l;
                    bool parade = l * 8 >= 100 && l * 8 <= 400 && k * 8 >= 100 && k * 8 <= 400;
                    bool same = terrain.Colors[4 * v] == plain.Colors[4 * v] && terrain.Colors[4 * v + 1] == plain.Colors[4 * v + 1];
                    if (parade)
                    {
                        inside++;
                        if (!same) changed++;
                    }
                    else if (l * 8 < 90 || k * 8 < 90) Assert.That(same, Is.True, "open cropland keeps its patchwork");
                }
            Assert.That(changed, Is.EqualTo(inside), "no crop patchwork on the parade ground");
        }

        [Test]
        public void CropLandAgreesOnSharedTileEdges()
        {
            // One residential polygon cut at the border of two tiles: both pieces cover the border points.
            TileId east = new TileId(10, Leaf.Tx + 1, Leaf.Ty);
            TileData a = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300, 129, Biome.ValleyCropland);
            TileData b = MeshingChecks.SyntheticTile(east, (x, z) => 1300, 129, Biome.ValleyCropland);
            a.Areas.Add(Square(AreaKind.Residential, 90000, 30000, 102400, 50000));
            b.Areas.Add(Square(AreaKind.Residential, 0, 30000, 5000, 50000));
            CropLand ca = CropLand.For(a), cb = CropLand.For(b);
            for (double z = 0; z <= 1024; z += 8)
                Assert.That(ca.Excluded(1024, z), Is.EqualTo(cb.Excluded(0, z)), "edge sample at z " + z);
            Assert.That(ca.Excluded(1024, 400), Is.True);
            Assert.That(ca.Excluded(1024, 600), Is.False);
        }
    }
}
