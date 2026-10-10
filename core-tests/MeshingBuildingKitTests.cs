using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The facade and prop kit of the building package and the B0 houses built on it: walls with real openings,
    /// swept mouldings, lattice, props; material channels and baked AO on every vertex (contract §5); rounded
    /// (smooth-shaded) geometry; determinism.
    /// </summary>
    public class MeshingBuildingKitTests
    {
        private static readonly TileId Leaf = new TileId(10, 516, 161);

        private static double FrontArea(MeshData m, double wx, double wz)
        {
            // Area of triangles whose normal points along (wx, 0, wz).
            double a = 0;
            for (int t = 0; t < m.TriangleCount; t++)
            {
                double nx, ny, nz;
                MeshingChecks.Facet(m, t, out nx, out ny, out nz);
                double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (len < 1e-12) continue;
                if ((nx * wx + nz * wz) / len > 0.999) a += 0.5 * len;
            }
            return a;
        }

        [Test]
        public void WallsHaveRealOpeningsWithReveals()
        {
            var f = new KitFrame(0, 0, 0, 1, 0); // U east, W south (-Z)
            var holes = new[] { new KitHole(1, 1, 2, 2.5), new KitHole(3, 1, 4, 2.5), new KitHole(5, 0, 6, 2.2) };
            var m = new MeshData();
            int tris = FacadeKit.WallWithHoles(m, f, 0, 7, 0, 3, 0, holes, holes.Length, MeshColor.FromHex(0xB4432F));
            MeshingChecks.AssertWellFormed(m, "wall");
            Assert.That(MeshingChecks.AssertFrontFacesAgreeWithNormals(m, 0, m.TriangleCount, 0.999, "wall"), Is.EqualTo(0));
            double area = FrontArea(m, f.WX, f.WZ);
            Assert.That(area, Is.EqualTo(7 * 3 - 1.5 - 1.5 - 2.2).Within(1e-6), "the holes are cut out");
            Assert.That(tris, Is.LessThanOrEqualTo(2 * 10), "slabs with the same holes are merged");
            int before = m.TriangleCount;
            FacadeKit.Reveal(m, f, holes[0], 0, 0.3, MeshColor.FromHex(0x8A3020));
            Assert.That(m.TriangleCount - before, Is.EqualTo(8));
            MeshingChecks.AssertWellFormed(m, "reveal");
            Assert.That(MeshingChecks.AssertFrontFacesAgreeWithNormals(m, before, m.TriangleCount, 0.999, "reveal"), Is.EqualTo(0));
        }

        [Test]
        public void MouldingsAndLatticeStayInTheirBounds()
        {
            var f = new KitFrame(10, 100, 20, 0, 1);
            var m = new MeshData();
            FacadeKit.Band(m, f, 0, 4, 2.0, 0.2, 0.1, 0.03, 3, MeshColor.FromHex(0xFFFFFF));
            FacadeKit.Corbel(m, f, 0, 4, 3.0, 2, 0.11, 0.06, 3, MeshColor.FromHex(0xFFFFFF));
            FacadeKit.Ledge(m, f, 0, 4, 4.0, 0.1, 0.08, 0.03, 3, MeshColor.FromHex(0xFFFFFF));
            FacadeKit.Coping(m, f, 0, 4, 5.0, 0.07, 0.2, 0, MeshColor.FromHex(0xFFFFFF));
            MeshingChecks.AssertWellFormed(m, "mouldings");
            Assert.That(MeshingChecks.AssertFrontFacesAgreeWithNormals(m, 0, m.TriangleCount, 0.0, "mouldings"), Is.EqualTo(0));
            var l = new MeshData();
            int n = FacadeKit.Lattice(l, f, 1, 1, 1.7, 2, 0, 0.16, 0.034, 0, false, MeshColor.FromHex(0x4A2C1C));
            Assert.That(n, Is.GreaterThan(10));
            MeshingChecks.AssertWellFormed(l, "lattice");
            for (int v = 0; v < l.VertexCount; v++)
            {
                // Local u along +Z from the origin: z - 20; v: y - 100.
                double u = l.Positions[3 * v + 2] - 20, vv = l.Positions[3 * v + 1] - 100;
                Assert.That(u, Is.InRange(1 - 0.04, 1.7 + 0.04));
                Assert.That(vv, Is.InRange(1 - 0.04, 2 + 0.04));
            }
            var c = new MeshData();
            FacadeKit.Corrugated(c, f, 0, 0, 2.5, 2.4, -0.1, 0.15, 0.025, MeshColor.FromHex(0x8C949C));
            MeshingChecks.AssertWellFormed(c, "shutter");
        }

        [Test]
        public void PropsAreWellFormedAndRounded()
        {
            var m = new MeshData();
            PropKit.Tank(m, 0, 10, 0, 0.55, 1.1, 10, MeshColor.FromHex(0x2A2A2E), MeshColor.FromHex(0x444448));
            PropKit.Stand(m, 3, 10, 11, 0, 0.45, 1, 0, PropKit.Steel);
            PropKit.SolarHeater(m, 6, 10, 0, 0, -1, 1.6, 14, 38, MeshColor.FromHex(0xD9DDE0), MeshColor.FromHex(0x2B3A4A), PropKit.Galvanised);
            PropKit.RebarColumn(m, 9, 10, 0, 1, 0, 0.3, 0.8, BuildingGrammar.Concrete, BuildingGrammar.Rust, MeshColor.FromHex(0xD93A2B), true);
            PropKit.Dish(m, 12, 10, 0, 0.3, -0.95, 0.8, MeshColor.FromHex(0xE6E6E6), PropKit.Steel);
            PropKit.PottedPlant(m, 15, 10, 0, 0.3, PropKit.Terracotta, BuildingGrammar.Foliage, BuildingGrammar.Marigold);
            PropKit.Laundry(m, 18, 11.7, 0, 22, 11.7, 0, 5, BuildingGrammar.Cloth, 7u, MeshColor.FromHex(0xDDDDDD));
            PropKit.Umbrella(m, 25, 10, 0, 1.15, 2.0, BuildingGrammar.Sign[0], MeshColor.FromHex(0xDDDDDD), MeshColor.FromHex(0xF4F1EA));
            PropKit.Chair(m, 28, 10, 0, 1, 0, MeshColor.FromHex(0xF4F1EA));
            PropKit.PrayerFlags(m, 30, 12, 0, 36, 12.5, 0, MeshColor.FromHex(0xEDE6D6));
            MeshingChecks.AssertWellFormed(m, "props");
            // The tank is a smooth lathe: most of its vertices carry normals that differ from their face's.
            var tank = new MeshData();
            PropKit.Tank(tank, 0, 0, 0, 0.55, 1.1, 10, MeshColor.FromHex(0x2A2A2E), MeshColor.FromHex(0x444448));
            Assert.That(SmoothShare(tank), Is.GreaterThan(0.5));
            Assert.That(tank.TriangleCount, Is.LessThan(260), "a cheap tank");
        }

        /// <summary>Share of triangle corners whose vertex normal differs (by more than 3°) from the face normal.</summary>
        private static double SmoothShare(MeshData m)
        {
            int smooth = 0, all = 0;
            for (int t = 0; t < m.TriangleCount; t++)
            {
                double nx, ny, nz;
                MeshingChecks.Facet(m, t, out nx, out ny, out nz);
                double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (len < 1e-12) continue;
                for (int k = 0; k < 3; k++)
                {
                    int v = m.Indices[3 * t + k];
                    double d = (m.Normals[3 * v] * nx + m.Normals[3 * v + 1] * ny + m.Normals[3 * v + 2] * nz) / len;
                    if (d < Math.Cos(3 * Math.PI / 180)) smooth++;
                    all++;
                }
            }
            return all == 0 ? 0 : (double)smooth / all;
        }

        private static TileData Street(StyleProfile profile, BuildingArchetype arch, int levels, bool shop, BuildingFrontFlags flags, double width)
        {
            TileData t = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300);
            int X(double v) => (int)Math.Round(v * 100);
            t.Buildings.Add(new BuildingRecord
            {
                Archetype = arch, Levels = (byte)levels, Seed = 4242,
                Rings = new[] { new[] { X(300), X(503), X(300 + width), X(503), X(300 + width), X(511), X(300), X(511) } },
            });
            var f = BuildingFrontRecord.Absent;
            f.Profile = profile;
            f.Area = AreaType.OldCore;
            f.FrontEdge = 0;
            f.ShopBays = (byte)(shop ? 1 : 0);
            f.Flags = (byte)flags;
            t.BuildingFronts.Add(f);
            t.Roads.Add(new RoadRecord { OsmWayId = 1, RoadClass = RoadClass.Residential, Points = new[] { X(280), X(497), X(340), X(497) }, WidthCm = 600 });
            return t;
        }

        private static readonly MaterialChannel[] Allowed =
        {
            MaterialChannel.Plain, MaterialChannel.Plaster, MaterialChannel.Brick, MaterialChannel.BrickGlazed, MaterialChannel.Wood,
            MaterialChannel.WoodCarved, MaterialChannel.RoofTile, MaterialChannel.Metal, MaterialChannel.Stone, MaterialChannel.Concrete,
            MaterialChannel.Foliage, MaterialChannel.Fabric, MaterialChannel.Glass, MaterialChannel.Paint,
        };

        private static HashSet<MaterialChannel> Channels(MeshData m, string what)
        {
            Assert.That(m.HasUv0, Is.True, what + ": UV0 carries channel and AO");
            var set = new HashSet<MaterialChannel>();
            for (int v = 0; v < m.VertexCount; v++)
            {
                float u = m.Uv0[2 * v], ao = m.Uv0[2 * v + 1];
                Assert.That(u, Is.EqualTo(Math.Round(u)), what + ": channel is an integer");
                var ch = (MaterialChannel)(int)u;
                Assert.That(Allowed, Does.Contain(ch), what);
                Assert.That(ao, Is.InRange(0.15f, 1f), what + ": AO in range at vertex " + v);
                set.Add(ch);
            }
            return set;
        }

        [Test]
        public void HousesCarryTheirMaterialsAndBakedAo()
        {
            TileData newar = Street(StyleProfile.Bhaktapur, BuildingArchetype.Newar, 4, false, BuildingFrontFlags.StructureMud, 6.0);
            var m = new MeshData();
            Assert.That(BuildingDetailMesher.One(newar, 0, new TileHeightSampler(newar, 1), new BuildingOptions(), m, null), Is.True);
            MeshingChecks.AssertWellFormed(m, "newar");
            HashSet<MaterialChannel> nc = Channels(m, "newar");
            Assert.That(nc, Is.SupersetOf(new[] { MaterialChannel.Brick, MaterialChannel.BrickGlazed, MaterialChannel.WoodCarved, MaterialChannel.Wood, MaterialChannel.RoofTile }));
            Assert.That(SmoothShare(m), Is.GreaterThan(0.05), "bevels and sweeps are smooth-shaded");
            // AO: the ground contact is darker than the top of the wall.
            double low = 0, high = 0;
            int nl = 0, nh = 0;
            for (int v = 0; v < m.VertexCount; v++)
            {
                if ((MaterialChannel)(int)m.Uv0[2 * v] != MaterialChannel.Brick && (MaterialChannel)(int)m.Uv0[2 * v] != MaterialChannel.BrickGlazed) continue;
                double y = m.Positions[3 * v + 1];
                if (y < 1300.2)
                {
                    low += m.Uv0[2 * v + 1];
                    nl++;
                }
                else if (y > 1305)
                {
                    high += m.Uv0[2 * v + 1];
                    nh++;
                }
            }
            Assert.That(low / Math.Max(1, nl), Is.LessThan(high / Math.Max(1, nh) - 0.1), "ground contact darker");

            TileData modern = Street(StyleProfile.Metro, BuildingArchetype.ModernUrban, 5, true, BuildingFrontFlags.StructureRcc, 9.0);
            var mm = new MeshData();
            Assert.That(BuildingDetailMesher.One(modern, 0, new TileHeightSampler(modern, 1), new BuildingOptions(), mm, null), Is.True);
            MeshingChecks.AssertWellFormed(mm, "modern");
            HashSet<MaterialChannel> mc = Channels(mm, "modern");
            Assert.That(mc, Is.SupersetOf(new[] { MaterialChannel.Glass, MaterialChannel.Metal, MaterialChannel.Concrete }));
            Assert.That(mc.Contains(MaterialChannel.Paint) || mc.Contains(MaterialChannel.Brick), Is.True);
        }

        [Test]
        public void HousesAreDeterministicAndAcrossThreads()
        {
            TileData t = Street(StyleProfile.KathmanduCore, BuildingArchetype.NewarHybrid, 6, true, BuildingFrontFlags.None, 14.0);
            var h = new TileHeightSampler(t, 1);
            var a = new MeshData();
            BuildingDetailMesher.One(t, 0, h, new BuildingOptions(), a, null);
            var results = new MeshData[4];
            var threads = new System.Threading.Thread[4];
            for (int k = 0; k < 4; k++)
            {
                int i = k;
                threads[k] = new System.Threading.Thread(() =>
                {
                    var m = new MeshData();
                    for (int r = 0; r < 3; r++)
                    {
                        m.Clear();
                        BuildingDetailMesher.One(t, 0, h, new BuildingOptions(), m, null);
                    }
                    results[i] = m;
                });
                threads[k].Start();
            }
            foreach (var th in threads) th.Join();
            foreach (MeshData b in results)
            {
                Assert.That(b.VertexCount, Is.EqualTo(a.VertexCount));
                Assert.That(b.IndexCount, Is.EqualTo(a.IndexCount));
                for (int i = 0; i < a.VertexCount * 3; i++) Assert.That(b.Positions[i], Is.EqualTo(a.Positions[i]));
                for (int i = 0; i < a.VertexCount * 2; i++) Assert.That(b.Uv0[i], Is.EqualTo(a.Uv0[i]));
            }
        }

        [Test]
        public void LongFrontsBecomeARowOfDifferentHouses()
        {
            // A 30 m Kathmandu-core footprint: plots of 4-8 m, each its own house (heights and fronts differ).
            TileData t = Street(StyleProfile.KathmanduCore, BuildingArchetype.ModernUrban, 6, true, BuildingFrontFlags.StructureRcc, 30.0);
            var m = new MeshData();
            Assert.That(BuildingDetailMesher.One(t, 0, new TileHeightSampler(t, 1), new BuildingOptions { B0CapTris = 100000 }, m, null), Is.True);
            MeshingChecks.AssertWellFormed(m, "row");
            // The top of the facade (highest vertex) sampled every 2 m along the front: not all equal.
            var tops = new List<double>();
            for (double x = 301; x < 329; x += 2)
            {
                double top = double.MinValue;
                for (int v = 0; v < m.VertexCount; v++)
                {
                    double px = m.Positions[3 * v], pz = m.Positions[3 * v + 2];
                    if (Math.Abs(px - x) < 0.6 && pz > 503.5 && pz < 510.5) top = Math.Max(top, m.Positions[3 * v + 1]);
                }
                if (top > double.MinValue) tops.Add(top);
            }
            double min = double.MaxValue, max = double.MinValue;
            foreach (double v in tops)
            {
                min = Math.Min(min, v);
                max = Math.Max(max, v);
            }
            Assert.That(max - min, Is.GreaterThan(1.5), "the skyline of a row steps from house to house");
        }
    }
}
