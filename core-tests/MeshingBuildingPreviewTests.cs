using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Visual self-check dumps of the building package (docs/W2_DETAIL_CONTRACT.md §6), written only when
    /// <c>GHUMANTE_PREVIEW_DIR</c> is set: a lineup of every house archetype per place on a synthetic street, and
    /// street-level scenes of real places from the valley pack (Bhaktapur, Patan, Asan, Thamel, Baneshwor) with the
    /// B0 band near the eye, B1 beyond, terrain and roads, and a render.sh with the commands. Renders are compared with
    /// the reference photos (docs/research/w2/ref_buildings.md).
    /// </summary>
    public class MeshingBuildingPreviewTests
    {
        private static readonly TileId Leaf = new TileId(10, 516, 161);

        private static BuildingRecord House(BuildingArchetype a, int levels, uint seed, double x0, double z0, double w, double d)
        {
            int X(double v) => (int)Math.Round(v * 100);
            return new BuildingRecord
            {
                Archetype = a, Levels = (byte)levels, Seed = seed, Flags = BuildingFlags.None, HeightCm = 0,
                Rings = new[] { new[] { X(x0), X(z0), X(x0 + w), X(z0), X(x0 + w), X(z0 + d), X(x0), X(z0 + d) } },
            };
        }

        private struct Spec
        {
            public string Name;
            public StyleProfile Profile;
            public BuildingArchetype Arch;
            public int Levels;
            public bool Shop;
            public BuildingFrontFlags Flags;
            public double Width;
        }

        /// <summary>A street of houses along a 6 m lane (front edges facing south onto it), one per spec.</summary>
        private static TileData Street(Spec[] specs, out double laneZ)
        {
            TileData t = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300 + 0.002 * (x - Leaf.X0));
            laneZ = 500;
            double x = 300;
            for (int i = 0; i < specs.Length; i++)
            {
                Spec s = specs[i];
                // Ring counter-clockwise from the front-left corner: front edge 0 runs west->east facing south (-z).
                var b = House(s.Arch, s.Levels, (uint)(0x1000 + i * 7919), x, laneZ + 3.0, s.Width, 7.5);
                t.Buildings.Add(b);
                var f = BuildingFrontRecord.Absent;
                f.Profile = s.Profile;
                f.Area = AreaType.OldCore;
                f.FrontEdge = 0;
                f.ShopBays = (byte)(s.Shop ? 1 : 0);
                f.Flags = (byte)s.Flags;
                t.BuildingFronts.Add(f);
                x += s.Width + 0.0;
            }
            t.Roads.Add(new RoadRecord
            {
                OsmWayId = 1, RoadClass = RoadClass.Residential, Points = new[] { 28000, (int)(laneZ * 100), (int)((x + 20) * 100), (int)(laneZ * 100) }, WidthCm = 450,
            });
            return t;
        }

        private static readonly Spec[] Lineup =
        {
            new Spec { Name = "bhaktapur_newar", Profile = StyleProfile.Bhaktapur, Arch = BuildingArchetype.Newar, Levels = 4, Flags = BuildingFrontFlags.StructureMud, Width = 5.2 },
            new Spec { Name = "bhaktapur_newar_shop", Profile = StyleProfile.Bhaktapur, Arch = BuildingArchetype.Newar, Levels = 3, Shop = true, Flags = BuildingFrontFlags.StructureMud, Width = 4.8 },
            new Spec { Name = "patan_newar", Profile = StyleProfile.Patan, Arch = BuildingArchetype.Newar, Levels = 4, Flags = BuildingFrontFlags.StructureMud, Width = 7.2 },
            new Spec { Name = "ktm_hybrid", Profile = StyleProfile.KathmanduCore, Arch = BuildingArchetype.NewarHybrid, Levels = 6, Shop = true, Width = 6.0 },
            new Spec { Name = "asan_modern", Profile = StyleProfile.KathmanduCore, Arch = BuildingArchetype.ModernUrban, Levels = 5, Shop = true, Flags = BuildingFrontFlags.StructureRcc, Width = 6.5 },
            new Spec { Name = "thamel_hotel", Profile = StyleProfile.Thamel, Arch = BuildingArchetype.ModernUrban, Levels = 6, Shop = true, Flags = BuildingFrontFlags.StructureRcc, Width = 8.0 },
            new Spec { Name = "metro_house", Profile = StyleProfile.Metro, Arch = BuildingArchetype.ModernUrban, Levels = 4, Width = 7.8 },
            new Spec { Name = "metro_shops", Profile = StyleProfile.Metro, Arch = BuildingArchetype.ModernUrban, Levels = 5, Shop = true, Width = 9.0 },
            new Spec { Name = "kirtipur_newar", Profile = StyleProfile.Kirtipur, Arch = BuildingArchetype.Newar, Levels = 3, Flags = BuildingFrontFlags.StructureMud, Width = 6.6 },
            new Spec { Name = "rana", Profile = StyleProfile.Metro, Arch = BuildingArchetype.RanaPalace, Levels = 3, Width = 14.0 },
        };

        [Test]
        public void DumpArchetypeLineup()
        {
            if (!ObjDump.Enabled) Assert.Pass("set " + ObjDump.EnvVar + " to dump the lineup");
            double laneZ;
            TileData t = Street(Lineup, out laneZ);
            var h = new TileHeightSampler(t, 1);
            var all = new MeshData();
            var stats = new StringBuilder();
            for (int i = 0; i < t.Buildings.Count; i++)
            {
                var m = new MeshData();
                Assert.That(BuildingDetailMesher.One(t, i, h, new BuildingOptions(), m, null), Is.True, Lineup[i].Name);
                MeshingChecks.AssertWellFormed(m, Lineup[i].Name);
                HousePlan p = BuildingGrammar.Plan(t, i);
                stats.AppendLine(Lineup[i].Name + ": " + p.Archetype + " " + p.Storeys + " storeys, " + m.TriangleCount + " tris");
                ObjDump.Write(m, "lineup/" + Lineup[i].Name + ".obj", new[] { "meshpreview: views=34,front,top sun=200,45" });
                ObjDump.Append(all, m, 0, 0, 0);
            }
            var roads = new MeshData();
            RoadMesher.Build(t, h, new RoadOptions(), roads);
            var terrain = new MeshData();
            TerrainMesher.Build(t, t.Tile, new TerrainOptions { Step = 1 }, terrain);
            MeshData ground = ObjDump.Crop(terrain, 280, (float)laneZ - 30, 420, (float)laneZ + 40, true);
            ObjDump.Write("lineup/street.obj", new[] { "meshpreview: sun=200,50" }, new ObjPart("houses", all), new ObjPart("roads", roads), new ObjPart("ground", ground));
            File.WriteAllText(Path.Combine(ObjDump.Dir, "lineup", "stats.txt"), stats.ToString());
            TestContext.WriteLine(stats.ToString());
        }

        // -------------------------------------------------------------------------------------------------------------
        // Real places from the valley pack
        // -------------------------------------------------------------------------------------------------------------

        private struct Place
        {
            public string Name;
            public double Lon, Lat;
        }

        private static readonly Place[] Places =
        {
            new Place { Name = "bhaktapur_taumadhi", Lon = 85.4290, Lat = 27.6712 },
            new Place { Name = "bhaktapur_lane", Lon = 85.4318, Lat = 27.6720 },
            new Place { Name = "patan_lane", Lon = 85.3250, Lat = 27.6745 },
            new Place { Name = "asan", Lon = 85.3122, Lat = 27.7074 },
            new Place { Name = "thamel", Lon = 85.3105, Lat = 27.7150 },
            new Place { Name = "baneshwor", Lon = 85.3355, Lat = 27.6905 },
            new Place { Name = "kirtipur", Lon = 85.2775, Lat = 27.6785 },
        };

        private static PackReader _valley;

        private static PackReader Valley()
        {
            if (_valley != null) return _valley;
            string path = Path.Combine(GoldenFiles.RepoRoot, "pipeline", "build", "regions", "kathmandu_valley", "kathmandu_valley.ghpk");
            if (!File.Exists(path)) return null;
            return _valley = new PackReader(File.ReadAllBytes(path));
        }

        [Test]
        public void DumpStreetScenes()
        {
            if (!ObjDump.Enabled) Assert.Pass("set " + ObjDump.EnvVar + " to dump the street scenes");
            PackReader pack = Valley();
            if (pack == null) Assert.Ignore("the valley pack is not built here");
            string only = Environment.GetEnvironmentVariable("GHUMANTE_PREVIEW_PLACE");
            var sh = new StringBuilder("#!/bin/sh\n# Street scenes of the building package; run from the repository root.\n");
            foreach (Place pl in Places)
            {
                if (!string.IsNullOrWhiteSpace(only) && !only.Contains(pl.Name)) continue;
                double gx, gz;
                WorldFrame.LonLatToGame(pl.Lon, pl.Lat, out gx, out gz);
                double size = TileId.SizeAt(10);
                var id = new TileId(10, (int)Math.Floor(gx / size), (int)Math.Floor(gz / size));
                TileData t;
                try
                {
                    t = pack.ReadTile(id);
                }
                catch (Exception e)
                {
                    TestContext.WriteLine(pl.Name + ": " + e.Message);
                    continue;
                }
                if (t == null || t.Buildings.Count == 0) continue;
                double lx = gx - t.Tile.X0, lz = gz - t.Tile.Z0;
                TileHeightSampler sampler = TileHeightSampler.ForArea(t, id, 1);
                float ex, ey, ez, vx, vy, vz;
                Eye(t, sampler, lx, lz, out ex, out ey, out ez, out vx, out vy, out vz);
                // Side views: standing on the road looking across at each side's facades, and a raised view down it.
                double fx = vx - ex, fz = vz - ez, fl = Math.Sqrt(fx * fx + fz * fz);
                fx /= fl;
                fz /= fl;
                double rx = fz, rz = -fx; // right of the heading
                double mx = ex + fx * 9, mz = ez + fz * 9;
                var eyes = new StringBuilder();
                CultureInfo ci = CultureInfo.InvariantCulture;
                void View(string vname, double ax, double ay, double az, double bx, double by, double bz)
                {
                    eyes.AppendLine(string.Format(ci, "{0} {1:0.##},{2:0.##},{3:0.##} {4:0.##},{5:0.##},{6:0.##}", vname, ax, ay, -az, bx, by, -bz));
                }
                View("chase", ex, ey, ez, vx, vy, vz);
                View("right", mx - rx * 1.2 - fx * 3, ey - 0.6, mz - rz * 1.2 - fz * 3, mx + rx * 10 + fx * 4, ey + 4.5, mz + rz * 10 + fz * 4);
                View("left", mx + rx * 1.2 - fx * 3, ey - 0.6, mz + rz * 1.2 - fz * 3, mx - rx * 10 + fx * 4, ey + 4.5, mz - rz * 10 + fz * 4);
                View("raised", ex - fx * 10, ey + 14, ez - fz * 10, vx + fx * 10, vy - 2, vz + fz * 10);
                const float Near = 70f, Far = 260f;
                var near = new MeshData();
                var far = new MeshData();
                var b0 = new MeshData();
                var o0 = new BuildingOptions { Band = BuildingBand.B0KitLite };
                var o1 = new BuildingOptions { Band = BuildingBand.B1Styled };
                int count = 0;
                for (int i = 0; i < t.Buildings.Count; i++)
                {
                    double cx, cz;
                    Centre(t.Buildings[i], out cx, out cz);
                    double d = Math.Sqrt((cx - ex) * (cx - ex) + (cz - ez) * (cz - ez));
                    if (d > Far) continue;
                    if (d < Near)
                    {
                        b0.Clear();
                        if (BuildingDetailMesher.One(t, i, sampler, o0, b0, null))
                        {
                            ObjDump.Append(near, b0, 0, 0, 0);
                            count++;
                        }
                    }
                }
                var b1All = new MeshData();
                BuildingMesher.Build(t, sampler, o1, b1All);
                far = ObjDump.Crop(b1All, ex - Near, ez - Near, ex + Near, ez + Near, false);
                far = ObjDump.Crop(far, ex - Far, ez - Far, ex + Far, ez + Far, true);
                var roads = new MeshData();
                RoadMesher.Build(t, sampler, new RoadOptions(), roads);
                var decals = new MeshData();
                MarkingMesher.Build(t, sampler, new RoadOptions(), decals);
                var terrain = new MeshData();
                TerrainMesher.Build(t, id, new TerrainOptions { Step = 1 }, terrain);
                var areas = new MeshData();
                AreaMesher.Build(t, sampler, new AreaOptions(), areas);
                CultureInfo c = CultureInfo.InvariantCulture;
                string eye = string.Format(c, "{0:0.##},{1:0.##},{2:0.##}", ex, ey, -ez), look = string.Format(c, "{0:0.##},{1:0.##},{2:0.##}", vx, vy, -vz);
                var directives = new[] { pl.Name + " tile " + id, "meshpreview: grid=0 sun=160,55 eye=" + eye + " look=" + look };
                string dir = "places/" + pl.Name + "/";
                ObjDump.Write(near, dir + "buildings.obj", directives);
                ObjDump.Write(far, dir + "buildings_b1.obj", directives);
                ObjDump.Write(ObjDump.Crop(roads, ex - Far, ez - Far, ex + Far, ez + Far, true), dir + "roads.obj", directives);
                ObjDump.Write(ObjDump.Crop(decals, ex - Far, ez - Far, ex + Far, ez + Far, true), dir + "decals.obj", directives);
                ObjDump.Write(ObjDump.Crop(terrain, ex - Far, ez - Far, ex + Far, ez + Far, true), dir + "terrain.obj", directives);
                ObjDump.Write(ObjDump.Crop(areas, ex - Far, ez - Far, ex + Far, ez + Far, true), dir + "areas.obj", directives);
                string d0 = Path.Combine(ObjDump.Dir, "places", pl.Name);
                File.WriteAllText(Path.Combine(d0, "eyes.txt"), eyes.ToString());
                sh.Append("python3 tools/mesh-preview/render.py --combine ").Append(d0).Append("/*.obj -o ").Append(d0).Append("/street.png --size 1200 --height 760 --fog 300\n");
                TestContext.WriteLine("{0}: tile {1}, {2} B0 buildings, {3} B0 tris, {4} B1 tris", pl.Name, id, count, near.TriangleCount, far.TriangleCount);
            }
            File.WriteAllText(Path.Combine(ObjDump.Dir, "places", "render.sh"), sh.ToString());
        }

        private static void Centre(BuildingRecord b, out double cx, out double cz)
        {
            int[] r = b.Rings[0];
            int n = r.Length / 2;
            cx = cz = 0;
            for (int k = 0; k < n; k++)
            {
                cx += r[2 * k] / 100.0;
                cz += r[2 * k + 1] / 100.0;
            }
            cx /= n;
            cz /= n;
        }

        /// <summary>A chase-camera eye on the road nearest to the place: 5 m behind a point on it, 2.4 m up, looking
        /// 22 m along it.</summary>
        private static void Eye(TileData t, TileHeightSampler h, double px, double pz, out float ex, out float ey, out float ez, out float lx, out float ly,
                                out float lz)
        {
            double best = double.MaxValue, qx = px, qz = pz, dx = 1, dz = 0;
            foreach (RoadRecord r in t.Roads)
            {
                if (r.RoadClass == RoadClass.Unknown || r.PointCount < 2 || (r.Flags & RoadFlags.Tunnel) != 0) continue;
                for (int i = 0; i + 1 < r.PointCount; i++)
                {
                    double ax = r.Points[2 * i] * 0.01, az = r.Points[2 * i + 1] * 0.01, bx = r.Points[2 * i + 2] * 0.01, bz = r.Points[2 * i + 3] * 0.01;
                    double sx = bx - ax, sz = bz - az, len = Math.Sqrt(sx * sx + sz * sz);
                    if (len < 12) continue;
                    double tt = Math.Max(0, Math.Min(1, ((px - ax) * sx + (pz - az) * sz) / (len * len)));
                    double cx = ax + sx * tt, cz = az + sz * tt, d = Math.Sqrt((cx - px) * (cx - px) + (cz - pz) * (cz - pz));
                    if (d >= best) continue;
                    best = d;
                    qx = cx;
                    qz = cz;
                    dx = sx / len;
                    dz = sz / len;
                }
            }
            float g;
            double ox = t.Tile.X0, oz = t.Tile.Z0;
            ex = (float)(qx - dx * 5);
            ez = (float)(qz - dz * 5);
            ey = (h.TryHeight(ox + ex, oz + ez, out g) ? g : 1300f) + 2.4f;
            lx = (float)(qx + dx * 22);
            lz = (float)(qz + dz * 22);
            ly = (h.TryHeight(ox + lx, oz + lz, out g) ? g : 1300f) + 3.2f;
        }
    }
}
