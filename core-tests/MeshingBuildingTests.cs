using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class MeshingBuildingTests
    {
        private static readonly TileId Leaf = new TileId(10, 516, 161);
        private static readonly float Ground = Ght.Dequantize(Ght.Quantize(1300));

        private static BuildingRecord B(BuildingArchetype a, RoofShape roof, double heightM, params int[] ringCm)
        {
            return new BuildingRecord
            {
                Archetype = a, RoofShape = roof, HeightCm = (ulong)Math.Round(heightM * 100), Levels = 2, Seed = 12345,
                Rings = new[] { ringCm },
            };
        }

        private static MeshData Mesh(BuildingRecord b, BuildingOptions o = null, Func<double, double, double> f = null)
        {
            TileData t = MeshingChecks.SyntheticTile(Leaf, f ?? ((x, z) => 1300));
            t.Buildings.Add(b);
            var m = new MeshData();
            Assert.That(BuildingMesher.Build(t, new TileHeightSampler(t, 1), o ?? new BuildingOptions(), m), Is.EqualTo(1));
            MeshingChecks.AssertWellFormed(m, b.Archetype + "/" + b.RoofShape);
            Assert.That(MeshingChecks.AssertFrontFacesAgreeWithNormals(m, 0, m.TriangleCount, 0.999, b.Archetype + "/" + b.RoofShape), Is.EqualTo(0));
            return m;
        }

        private static void YRange(MeshData m, out float min, out float max)
        {
            min = float.MaxValue;
            max = float.MinValue;
            for (int v = 0; v < m.VertexCount; v++)
            {
                min = Math.Min(min, m.Positions[3 * v + 1]);
                max = Math.Max(max, m.Positions[3 * v + 1]);
            }
        }

        private static bool HasVertex(MeshData m, double x, double y, double z)
        {
            for (int v = 0; v < m.VertexCount; v++)
                if (Math.Abs(m.Positions[3 * v] - x) < 1e-3 && Math.Abs(m.Positions[3 * v + 1] - y) < 1e-3 && Math.Abs(m.Positions[3 * v + 2] - z) < 1e-3)
                    return true;
            return false;
        }

        /// <summary>Plan area of the up-facing horizontal triangles (flat decks).</summary>
        private static double DeckArea(MeshData m, out float deckY)
        {
            double area = 0;
            deckY = float.NaN;
            for (int t = 0; t < m.TriangleCount; t++)
            {
                double nx, ny, nz;
                MeshingChecks.Facet(m, t, out nx, out ny, out nz);
                double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (len < 1e-12 || ny / len < 0.99999) continue;
                area += 0.5 * ny;
                deckY = m.Positions[3 * m.Indices[3 * t] + 1];
            }
            return area;
        }

        [Test]
        public void GabledQuadHasItsRidgeAlongTheLongSide()
        {
            // 10 m (east-west) by 6 m, Newar, 9 m total height: eaves at 7.5 m, ridge at 9 m.
            MeshData m = Mesh(B(BuildingArchetype.Newar, RoofShape.Gabled, 9, 10000, 10000, 11000, 10000, 11000, 10600, 10000, 10600));
            Assert.That(m.VertexCount, Is.EqualTo(16 + 8 + 6));
            Assert.That(m.TriangleCount, Is.EqualTo(8 + 4 + 2));
            float min, max;
            YRange(m, out min, out max);
            Assert.That(min, Is.EqualTo(Ground - 1).Within(1e-3), "walls sink 1 m below the lowest ground");
            Assert.That(max, Is.EqualTo(Ground + 9).Within(1e-3));
            Assert.That(HasVertex(m, 100, Ground + 9, 103), Is.True, "ridge end over the west gable");
            Assert.That(HasVertex(m, 110, Ground + 9, 103), Is.True, "ridge end over the east gable");
            Assert.That(HasVertex(m, 100, Ground + 7.5, 100), Is.True, "eaves");
            // Walls face away from the footprint centre.
            for (int t = 0; t < m.TriangleCount; t++)
            {
                double nx, ny, nz;
                MeshingChecks.Facet(m, t, out nx, out ny, out nz);
                if (Math.Abs(ny) > 1e-9) continue;
                int a = m.Indices[3 * t];
                double ox = m.Positions[3 * a] - 105, oz = m.Positions[3 * a + 2] - 103;
                Assert.That(nx * ox + nz * oz, Is.GreaterThan(0), "wall " + t + " faces outward");
            }
            // Turned 90 degrees, the ridge turns with the long side.
            MeshData r = Mesh(B(BuildingArchetype.Newar, RoofShape.Gabled, 9, 10000, 10000, 10600, 10000, 10600, 11000, 10000, 11000));
            Assert.That(HasVertex(r, 103, Ground + 9, 100), Is.True);
            Assert.That(HasVertex(r, 103, Ground + 9, 110), Is.True);
        }

        [Test]
        public void FlatRoofHasADeckBehindAParapet()
        {
            int[] ring = { 20000, 20000, 21200, 20000, 21200, 20800, 20000, 20800 };
            MeshData m = Mesh(B(BuildingArchetype.ModernUrban, RoofShape.Flat, 12, ring));
            Assert.That(m.VertexCount, Is.EqualTo(16 + 16 + 4));
            Assert.That(m.TriangleCount, Is.EqualTo(8 + 8 + 2));
            float min, max, deckY;
            YRange(m, out min, out max);
            Assert.That(max, Is.EqualTo(Ground + 12).Within(1e-3));
            Assert.That(DeckArea(m, out deckY), Is.EqualTo(12 * 8).Within(1e-3));
            Assert.That(deckY, Is.EqualTo(Ground + 11.5).Within(1e-3));

            MeshData plain = Mesh(B(BuildingArchetype.ModernUrban, RoofShape.Flat, 12, ring), new BuildingOptions { Parapets = false });
            Assert.That(plain.TriangleCount, Is.EqualTo(10));
            Assert.That(DeckArea(plain, out deckY), Is.EqualTo(96).Within(1e-3));
            Assert.That(deckY, Is.EqualTo(Ground + 12).Within(1e-3));
        }

        [Test]
        public void ConcaveFootprintsGetAFlatEarClippedRoof()
        {
            // L-shape, 300 m²: gabled is only for quads, a pyramid only for convex rings, so it falls back to flat.
            int[] l = { 0, 0, 2000, 0, 2000, 1000, 1000, 1000, 1000, 2000, 0, 2000 };
            for (int k = 0; k < l.Length; k++) l[k] += 30000;
            float deckY;
            MeshData m = Mesh(B(BuildingArchetype.Newar, RoofShape.Gabled, 9, l));
            Assert.That(DeckArea(m, out deckY), Is.EqualTo(300).Within(1e-3));
            // A clockwise ring is accepted too (reversed), with the same result.
            var cw = new int[l.Length];
            for (int k = 0; k < l.Length / 2; k++)
            {
                cw[2 * k] = l[l.Length - 2 - 2 * k];
                cw[2 * k + 1] = l[l.Length - 1 - 2 * k];
            }
            Assert.That(DeckArea(Mesh(B(BuildingArchetype.Newar, RoofShape.Flat, 9, cw)), out deckY), Is.EqualTo(300).Within(1e-3));
            // A star-shaped 12-gon with collinear points.
            var star = new List<int>();
            for (int k = 0; k < 12; k++)
            {
                double a = k * Math.PI / 6, r = k % 2 == 0 ? 1500 : 700;
                star.Add(50000 + (int)Math.Round(r * Math.Cos(a)));
                star.Add(50000 + (int)Math.Round(r * Math.Sin(a)));
            }
            MeshData s = Mesh(B(BuildingArchetype.ModernUrban, RoofShape.Flat, 6, star.ToArray()));
            var xs = new double[12];
            var zs = new double[12];
            for (int k = 0; k < 12; k++)
            {
                xs[k] = star[2 * k] / 100.0;
                zs[k] = star[2 * k + 1] / 100.0;
            }
            Assert.That(DeckArea(s, out deckY), Is.EqualTo(Polygon.SignedArea(xs, zs, 12)).Within(1e-3));
        }

        [Test]
        public void CourtyardWallsFaceTheCourtyard()
        {
            var b = B(BuildingArchetype.Newar, RoofShape.Flat, 10, 0, 0, 3000, 0, 3000, 3000, 0, 3000);
            int[] hole = { 1000, 1000, 1000, 2000, 2000, 2000, 2000, 1000 }; // clockwise
            b.Rings = new[] { Offset(b.Rings[0], 40000), Offset(hole, 40000) };
            MeshData m = Mesh(b);
            int inward = 0;
            for (int t = 0; t < m.TriangleCount; t++)
            {
                double nx, ny, nz;
                MeshingChecks.Facet(m, t, out nx, out ny, out nz);
                if (Math.Abs(ny) > 1e-9) continue;
                int a = m.Indices[3 * t];
                double x = m.Positions[3 * a] - 400, z = m.Positions[3 * a + 2] - 400;
                bool onHole = x >= 9.99 && x <= 20.01 && z >= 9.99 && z <= 20.01;
                if (!onHole) continue;
                Assert.That(nx * (15 - x) + nz * (15 - z), Is.GreaterThan(0), "hole wall faces the courtyard centre");
                inward++;
            }
            Assert.That(inward, Is.EqualTo(8), "four hole walls, two triangles each");
        }

        private static int[] Offset(int[] ring, int d)
        {
            var r = new int[ring.Length];
            for (int k = 0; k < ring.Length; k++) r[k] = ring[k] + d;
            return r;
        }

        [Test]
        public void EveryRoofShapeAndArchetypeMeshesWithinItsHeight()
        {
            int[] quad = { 60000, 60000, 61400, 60200, 61300, 61100, 60100, 60900 };
            var hex = new List<int>();
            for (int k = 0; k < 6; k++)
            {
                hex.Add(70000 + (int)Math.Round(900 * Math.Cos(k * Math.PI / 3)));
                hex.Add(70000 + (int)Math.Round(900 * Math.Sin(k * Math.PI / 3)));
            }
            foreach (BuildingArchetype a in Enum.GetValues(typeof(BuildingArchetype)))
            {
                foreach (RoofShape r in Enum.GetValues(typeof(RoofShape)))
                {
                    foreach (int[] ring in new[] { quad, hex.ToArray() })
                    {
                        // Ground rises 8 % to the east; the lowest footprint point sets the base.
                        double ground = 1300 + 0.08 * (ring == quad ? 600 : 691);
                        foreach (double h in new[] { 4.0, 14.0, 30.0 })
                        {
                            MeshData m = Mesh(B(a, r, h, ring), null, (x, z) => 1300 + 0.08 * (x - Leaf.X0));
                            float min, max;
                            YRange(m, out min, out max);
                            Assert.That(max, Is.LessThanOrEqualTo(ground + h + 0.1), a + "/" + r + " " + h);
                            Assert.That(max, Is.GreaterThanOrEqualTo(ground + h - 0.1), a + "/" + r + " " + h + " reaches its height");
                            Assert.That(min, Is.EqualTo(ground - 1).Within(0.1), a + "/" + r + " " + h);
                        }
                    }
                }
            }
        }

        [Test]
        public void DegenerateAndLandmarkBuildingsAreSkipped()
        {
            TileData t = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300);
            t.Buildings.Add(B(BuildingArchetype.Generic, RoofShape.Flat, 6, 0, 0, 500, 500, 1000, 1000));          // collinear
            t.Buildings.Add(B(BuildingArchetype.Generic, RoofShape.Flat, 6, 0, 0, 50, 0, 50, 50, 0, 50));         // 0.25 m²
            t.Buildings.Add(B(BuildingArchetype.Generic, RoofShape.Flat, 6, 0, 0, 0, 0, 100, 100, 100, 100));     // repeated points
            var landmark = B(BuildingArchetype.TemplePagoda, RoofShape.Pagoda, 20, 5000, 5000, 6000, 5000, 6000, 6000, 5000, 6000);
            landmark.Flags = BuildingFlags.Landmark;
            t.Buildings.Add(landmark);
            var sampler = new TileHeightSampler(t, 1);
            Assert.That(BuildingMesher.Build(t, sampler, new BuildingOptions(), new MeshData()), Is.EqualTo(1));
            Assert.That(BuildingMesher.Build(t, sampler, new BuildingOptions { SkipLandmarks = true }, new MeshData()), Is.EqualTo(0));
            for (int i = 0; i < 3; i++) Assert.That(BuildingMesher.IsDrawn(t.Buildings[i], null), Is.False);
            Assert.That(BuildingMesher.IsDrawn(landmark, null), Is.True);
            Assert.That(BuildingMesher.IsDrawn(landmark, new BuildingOptions { SkipLandmarks = true }), Is.False);
        }

        [Test]
        public void HeightsColoursAndDefaultsFollowTheStyleTables()
        {
            var b = new BuildingRecord { Archetype = BuildingArchetype.ModernUrban, Levels = 4, RoofShape = RoofShape.Unknown, Seed = 7 };
            Assert.That(BuildingStyle.ResolvedShape(b), Is.EqualTo(RoofShape.Flat));
            Assert.That(BuildingStyle.HeightM(b), Is.EqualTo(4 * 3 + 0.5f), "storeys plus the flat roof allowance, as the pipeline");
            b.HeightCm = 1730;
            Assert.That(BuildingStyle.HeightM(b), Is.EqualTo(17.3f).Within(1e-4));
            Assert.That(BuildingStyle.DefaultRoof(BuildingArchetype.Newar), Is.EqualTo(RoofShape.Gabled));
            Assert.That(BuildingStyle.DefaultRoof(BuildingArchetype.TemplePagoda), Is.EqualTo(RoofShape.Pagoda));
            Assert.That(BuildingStyle.DefaultRoof(BuildingArchetype.Stupa), Is.EqualTo(RoofShape.Dome));
            // Deterministic: the same seed gives the same colour; the palette varies across seeds.
            var colours = new HashSet<uint>();
            for (uint seed = 0; seed < 200; seed++)
            {
                var r = new BuildingRecord { Archetype = BuildingArchetype.ModernUrban, Seed = seed * 2654435761u };
                Assert.That(BuildingStyle.WallRgba(r), Is.EqualTo(BuildingStyle.WallRgba(r)));
                colours.Add(BuildingStyle.WallRgba(r));
            }
            Assert.That(colours.Count, Is.GreaterThan(20));
            var newar = new BuildingRecord { Archetype = BuildingArchetype.Newar, WallMaterial = WallMaterial.Brick, Seed = 1 };
            uint c = BuildingStyle.WallRgba(newar);
            Assert.That(MeshColor.R(c), Is.GreaterThan(MeshColor.G(c) + 40), "Newar brick is red");
        }

        [Test]
        public void SampleBuildingsAllMeshWithDocumentedSkips()
        {
            var o = new BuildingOptions();
            var m = new MeshData();
            int records = 0, drawnTotal = 0, skipped = 0, decks = 0, deckMismatch = 0;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                var s = new TileHeightSampler(t, 2);
                m.Clear();
                int drawn = BuildingMesher.Build(t, s, o, m);
                int expected = 0;
                foreach (BuildingRecord b in t.Buildings)
                    if (BuildingMesher.IsDrawn(b, o)) expected++;
                Assert.That(drawn, Is.EqualTo(expected), id.ToString());
                records += t.Buildings.Count;
                drawnTotal += drawn;
                skipped += t.Buildings.Count - drawn;
                MeshingChecks.AssertWellFormed(m, id.ToString());
                // Roof quads over skewed footprints may be slightly non-planar and share one (Newell) normal.
                MeshingChecks.AssertFrontFacesAgreeWithNormals(m, 0, m.TriangleCount, 0.99, id.ToString());
                for (int v = 0; v < m.VertexCount; v++)
                {
                    float y = m.Positions[3 * v + 1];
                    if (y < 1200 || y > 1800) Assert.Fail(id + ": building vertex at " + y + " m");
                }
            }
            // Flat decks cover their footprints exactly (ear clipping), checked building by building on one tile.
            TileData dense = StreamingSampleRegion.Tile(StreamingSampleRegion.DensestLeaf());
            var ds = new TileHeightSampler(dense, 2);
            foreach (BuildingRecord b in dense.Buildings)
            {
                var one = new TileData { Tile = dense.Tile, HeightsN = dense.HeightsN, HeightsQ = dense.HeightsQ };
                one.Buildings.Add(b);
                var bm = new MeshData();
                if (BuildingMesher.Build(one, ds, o, bm) == 0) continue;
                float deckY;
                double deck = DeckArea(bm, out deckY);
                if (deck == 0) continue;
                int n = b.Rings[0].Length / 2;
                var xs = new double[n];
                var zs = new double[n];
                for (int k = 0; k < n; k++)
                {
                    xs[k] = b.Rings[0][2 * k] / 100.0;
                    zs[k] = b.Rings[0][2 * k + 1] / 100.0;
                }
                double area = Math.Abs(Polygon.SignedArea(xs, zs, n));
                decks++;
                if (Math.Abs(deck - area) > 1e-3 * area + 1e-3) deckMismatch++;
            }
            TestContext.WriteLine("buildings {0}, drawn {1}, skipped {2}; flat decks checked {3}, area mismatches {4}",
                                  records, drawnTotal, skipped, decks, deckMismatch);
            Assert.That(skipped, Is.LessThanOrEqualTo(records / 1000), "degenerate footprints are rare");
            Assert.That(decks, Is.GreaterThan(3000));
            Assert.That(deckMismatch, Is.LessThanOrEqualTo(decks / 1000), "self-intersecting OSM rings only");
        }
    }
}
