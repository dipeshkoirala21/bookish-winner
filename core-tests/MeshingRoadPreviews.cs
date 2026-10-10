using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Visual self-check of the road package (docs/W2_DETAIL_CONTRACT.md §6; ref_roads.md): street-level scenes of the
    /// sample region at Thamel, Asan, Kanti Path, a roundabout (Balaju) and a hairpin, as OBJ layers (terrain, roads,
    /// decals, buildings clear of the road corridors) with a chase-camera eye directive, plus render.sh. Writes nothing
    /// unless GHUMANTE_PREVIEW_DIR is set.
    /// </summary>
    public class MeshingRoadPreviews
    {
        private struct Place
        {
            public string Name;
            public double Lon, Lat;
            public bool Motor;
            public double Back, Up;
            public string Street;
        }

        private static readonly Place[] Places =
        {
            new Place { Name = "thamel", Lon = 85.3108, Lat = 27.7153, Back = 9, Up = 2.4 },
            new Place { Name = "asan", Lon = 85.3122, Lat = 27.7074, Back = 8, Up = 2.2 },
            new Place { Name = "kantipath", Lon = 85.3157, Lat = 27.7085, Motor = true, Back = 14, Up = 3.0, Street = "Kanti" },
            new Place { Name = "roundabout_balaju", Lon = 85.3046, Lat = 27.7270, Motor = true, Back = 26, Up = 7.0 },
            new Place { Name = "tripureshwor", Lon = 85.3141, Lat = 27.6938, Motor = true, Back = 24, Up = 6.0 },
        };

        [Test]
        public void DumpStreetScenes()
        {
            if (!ObjDump.Enabled) Assert.Pass("set " + ObjDump.EnvVar + " to dump the road scenes");
            var sh = new StringBuilder("#!/bin/sh\n# Road previews (tools/mesh-preview/README.md). Run from the repository root.\n");
            foreach (Place p in Places)
            {
                double gx, gz;
                WorldFrame.LonLatToGame(p.Lon, p.Lat, out gx, out gz);
                TileId id = TileId.At(10, gx, gz);
                TileData t = StreamingSampleRegion.Tile(id);
                if (t == null) continue;
                Dump(t, p.Name, gx - id.X0, gz - id.Z0, p.Motor, p.Back, p.Up, p.Street, sh);
            }
            // A switchback on the valley rim (the sample has no hill roads): a synthetic hillside with two hairpins.
            Dump(HillSwitchback(), "hairpin", 712, 333, false, 16, 6, null, sh);
            string dir = Path.Combine(ObjDump.Dir, "roads");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "render.sh"), sh.ToString());
        }

        private static void Dump(TileData t, string name, double px, double pz, bool motor, double back, double up, string street, StringBuilder sh)
        {
            TileId id = t.Tile;
            RoadLayout lay = RoadLayout.For(t);
            // The eye: behind the nearest street point, looking along it.
            int best = -1;
            double bd = double.MaxValue, bs = 0;
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                if (!RoadMesher.IsDrawn(t, i, null) || RoadWidthModel.IsFootClass(r.RoadClass)) continue;
                if (motor && !RoadWidthModel.IsMajor(r.RoadClass)) continue;
                if (street != null)
                {
                    NameRecord nm = t.Name(r.NameRef);
                    if (nm == null || (nm.Default + nm.En).IndexOf(street, StringComparison.OrdinalIgnoreCase) < 0) continue;
                }
                RoadCentreline c = lay.Centres[i];
                if (c == null) continue;
                int seg;
                double along, lat, d;
                if (!c.Nearest(px, pz, out seg, out along, out lat, out d) || d >= bd) continue;
                bd = d;
                best = i;
                bs = along;
            }
            if (best < 0) return;
            RoadCentreline cl = lay.Centres[best];
            double cx, cz, tx, tz;
            cl.At(bs, out cx, out cz, out tx, out tz);
            var terrain = new MeshData();
            TerrainMesher.Build(t, id, new TerrainOptions { Step = 1 }, terrain);
            TileHeightSampler sampler = TileHeightSampler.ForArea(t, id, 1);
            var o = new RoadOptions();
            var roads = new MeshData();
            RoadMesher.Build(t, sampler, o, roads);
            var decals = new MeshData();
            MarkingMesher.Build(t, sampler, o, decals);
            RoadGrade grade = RoadGrade.For(t, sampler, o);
            float ry;
            if (!grade.TrySurfaceAt(best, cx, cz, 30, out ry)) sampler.TryHeightClamped(id.X0 + cx, id.Z0 + cz, out ry);
            double ex = cx - tx * back, ez = cz - tz * back, lx = cx + tx * 25, lz = cz + tz * 25;
            float ey = ry + (float)up, ly = ry + 0.5f;
            // Buildings clear of the corridors (the pipeline and the buildings package trim the rest).
            RoadCorridorIndex corridor = RoadCorridorIndex.ForTile(t);
            var clear = new TileData { Tile = t.Tile, HeightsN = t.HeightsN, HeightsQ = t.HeightsQ, BiomesN = t.BiomesN, Biomes = t.Biomes, Flags = t.Flags };
            for (int b = 0; b < t.Buildings.Count; b++)
            {
                int[] ring = t.Buildings[b].Rings[0];
                int n = ring.Length / 2;
                var bx = new double[n];
                var bz = new double[n];
                for (int k = 0; k < n; k++)
                {
                    bx[k] = id.X0 + ring[2 * k] / 100.0;
                    bz[k] = id.Z0 + ring[2 * k + 1] / 100.0;
                }
                double depth;
                if (corridor.Overlaps(bx, bz, n, out depth) && depth > 0.3) continue;
                clear.Buildings.Add(t.Buildings[b]);
                if (t.HasBuildingFronts) clear.BuildingFronts.Add(t.BuildingFronts[b]);
            }
            var buildings = new MeshData();
            BuildingMesher.Build(clear, sampler, new BuildingOptions { Band = BuildingBand.B0KitLite }, buildings);
            const float R = 170f;
            float x0 = (float)px - R, z0 = (float)pz - R, x1 = (float)px + R, z1 = (float)pz + R;
            CultureInfo ic = CultureInfo.InvariantCulture;
            string eye = string.Format(ic, "{0:0.##},{1:0.##},{2:0.##}", ex, ey, -ez), look = string.Format(ic, "{0:0.##},{1:0.##},{2:0.##}", lx, ly, -lz);
            var directives = new[]
            {
                "roads preview " + name + ": tile " + id + ", metres from the tile's south-west corner",
                "meshpreview: grid=0 sun=150,55 eye=" + eye + " look=" + look,
            };
            string dir = "roads/" + name + "/";
            var files = new List<string>();
            foreach (var (layer, mesh) in new (string, MeshData)[]
                     {
                         ("terrain", ObjDump.Crop(terrain, x0, z0, x1, z1, true)), ("roads", ObjDump.Crop(roads, x0, z0, x1, z1, true)),
                         ("decals", ObjDump.Crop(decals, x0, z0, x1, z1, true)), ("buildings", ObjDump.Crop(buildings, x0, z0, x1, z1, true)),
                     })
            {
                if (mesh.TriangleCount == 0) continue;
                ObjDump.Write(mesh, dir + layer + ".obj", directives);
                files.Add("$D/" + name + "/" + layer + ".obj");
            }
            sh.Append("D=").Append(Path.Combine(ObjDump.Dir, "roads")).Append('\n');
            sh.Append("python3 tools/mesh-preview/render.py --combine ").Append(string.Join(" ", files)).Append(" -o $D/").Append(name)
              .Append("_street.png --size 1000 --height 620 --fog 260 --pull 2e-5\n");
            sh.Append("python3 tools/mesh-preview/render.py --combine ").Append(string.Join(" ", files.GetRange(0, Math.Min(3, files.Count)))).Append(" -o $D/")
              .Append(name).Append("_top.png --no-eye --views top --size 900 --pull 2e-5\n");
            TestContext.WriteLine(name + ": tile " + id + " road " + best + " (" + t.Roads[best].RoadClass + ") roads " + roads.TriangleCount + " decals " + decals.TriangleCount);
        }

        /// <summary>
        /// A synthetic rim road (ref_roads.md §2, hill roads): a hillside rising 16 % to the north with a ridge ripple, and a
        /// tertiary road climbing it in three legs joined by two hairpins, each mapped the way OSM maps them (four or five
        /// vertices around a 12 to 15 m bend), in the HILL column with a final corridor.
        /// </summary>
        private static TileData HillSwitchback()
        {
            var id = new TileId(10, 516, 161);
            Func<double, double, double> f = (x, z) =>
            {
                double lx = x - id.X0, lz = z - id.Z0;
                return 1500 + 0.16 * lz + 4 * Math.Sin(lx / 70.0) * Math.Cos(lz / 90.0) + 1.5 * Math.Sin(lx / 13.0 + lz / 17.0);
            };
            TileData t = MeshingChecks.SyntheticTile(id, f, 513, Biome.HillForest);
            int[] pts =
            {
                20000, 30000, 45000, 31500, 70000, 33000, 73500, 33600, 76000, 35000, 77000, 37500, 76000, 40000, 73500, 41200,
                50000, 42500, 30000, 44000, 26500, 44600, 24000, 46000, 23000, 48500, 24000, 51000, 26500, 52200, 50000, 54000,
                80000, 56000,
            };
            t.Roads.Add(new RoadRecord { OsmWayId = 777001, RoadClass = RoadClass.Tertiary, Surface = Surface.Asphalt, Points = pts });
            t.RoadAttrs.Add(new RoadAttrRecord { Area = AreaType.Hill, CorridorDm = new[] { 90 } });
            t.RoadStructures.Add(RoadStructureRecord.Absent);
            return t;
        }
    }
}
