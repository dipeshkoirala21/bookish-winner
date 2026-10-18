using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;
using Ghumante.Core.Meshing.Shapes;
using Ghumante.Core.Traffic;
using Ghumante.World.Instancing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Visual self-check of the cut/fill package (docs/W2_DETAIL_CONTRACT.md §6): street-level scenes of the hill roads of the
    /// sample (Gaurighat by the Bagmati, the owner's screenshot; Swayambhu; the Balaju / Nagarjun edge; Chabahil to Gaurighat)
    /// at the High tier's terrain step, with the parked vehicles (catalogue meshes with their wheels) and the street dogs and
    /// cows of the traffic sim as the game places them, so a float or a sink shows. Each scene looks along the hill road with
    /// the steepest natural cross slope near the place (the same view before and after the carve). Writes nothing unless
    /// GHUMANTE_PREVIEW_DIR is set; GHUMANTE_PREVIEW_TAG names the subfolder (for example "before" / "after").
    /// </summary>
    public class RoadCutPreviews
    {
        [Test]
        public void DumpHillRoadScenes()
        {
            if (!ObjDump.Enabled) Assert.Pass("set " + ObjDump.EnvVar + " to dump the hill road scenes");
            string tag = Environment.GetEnvironmentVariable("GHUMANTE_PREVIEW_TAG");
            if (string.IsNullOrEmpty(tag)) tag = "now";
            var sh = new StringBuilder("#!/bin/sh\n# Cut/fill previews (tools/mesh-preview/README.md). Run from the repository root.\n");
            foreach (HillPlaces.Place place in HillPlaces.All) Dump(place, tag, sh);
            string dir = Path.Combine(ObjDump.Dir, "cutfill", tag);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "render.sh"), sh.ToString());
        }

        /// <summary>The drawn piece and along position near <paramref name="place"/> whose natural cross slope is steepest
        /// (carriageways only, away from caps), from the natural terrain so the view does not change with the carve.</summary>
        private static bool Viewpoint(TileData t, HillPlaces.Place place, out int road, out double along)
        {
            double px, pz;
            place.Game(out px, out pz);
            RoadLayout lay = RoadLayout.For(t);
            road = -1;
            along = 0;
            double best = 0;
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                RoadCentreline c = lay.Centres[i];
                if (c == null || c.Count < 2 || !lay.DrawsRibbon(i) || RoadWidthModel.IsFootClass(r.RoadClass) || (r.Flags & RoadFlags.Bridge) != 0) continue;
                for (double s = 15; s < c.RawLengthM - 15; s += 3)
                {
                    if (lay.InGap(i, s)) continue;
                    double x, z, tx, tz;
                    c.At(s, out x, out z, out tx, out tz);
                    double gx = t.Tile.X0 + x, gz = t.Tile.Z0 + z;
                    if ((gx - px) * (gx - px) + (gz - pz) * (gz - pz) > place.RadiusM * place.RadiusM) continue;
                    double hl = Natural(t, x - tz * 6, z + tx * 6), hr = Natural(t, x + tz * 6, z - tx * 6);
                    double slope = Math.Abs(hl - hr) / 12.0;
                    if (slope <= best) continue;
                    best = slope;
                    road = i;
                    along = s;
                }
            }
            return road >= 0;
        }

        private static double Natural(TileData t, double lx, double lz)
        {
            lx = Math.Max(0, Math.Min(t.Tile.Size, lx));
            lz = Math.Max(0, Math.Min(t.Tile.Size, lz));
            TerrainGrid g = TerrainGrid.For(t, t.Tile, HillPlaces.RuntimeStep);
            double du, dv;
            return g.Surface(t, lx / g.CellM, lz / g.CellM, out du, out dv);
        }

        private static void Dump(HillPlaces.Place place, string tag, StringBuilder sh)
        {
            TileData t = StreamingSampleRegion.Tile(place.Tile);
            if (t == null) return;
            TileId id = t.Tile;
            int road;
            double along;
            if (!Viewpoint(t, place, out road, out along)) return;
            RoadLayout lay = RoadLayout.For(t);
            RoadCentreline cl = lay.Centres[road];
            double cx, cz, tx, tz;
            cl.At(along, out cx, out cz, out tx, out tz);
            int step = HillPlaces.RuntimeStep;
            var terrain = new MeshData();
            TerrainMesher.Build(t, id, new TerrainOptions { Step = step }, terrain);
            TileHeightSampler sampler = TileHeightSampler.ForArea(t, id, step);
            var o = new RoadOptions();
            var roads = new MeshData();
            RoadMesher.Build(t, sampler, o, roads);
            var decals = new MeshData();
            MarkingMesher.Build(t, sampler, o, decals);
            RoadGrade grade = RoadGrade.For(t, sampler, o);
            float ry;
            if (!grade.TrySurfaceAt(road, cx, cz, 30, out ry)) sampler.TryHeightClamped(id.X0 + cx, id.Z0 + cz, out ry);
            // Buildings clear of the corridors.
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
            // Life: parked vehicles (with wheels) and the street animals of the traffic sim near the view.
            const float R = 140f;
            var life = new MeshData();
            var parked = new List<ParkedVehicle>();
            ParkedPlacement.Place(t, sampler, parked, o);
            var body = new MeshData();
            var wheel = new MeshData();
            int vehicles = 0, animals = 0;
            foreach (ParkedVehicle v in parked)
            {
                if ((v.X - cx) * (v.X - cx) + (v.Z - cz) * (v.Z - cz) > R * R) continue;
                body.Clear();
                VehicleMesher.Build(v.Variant, v.Livery, VehicleLod.Lod1, body, v.Id);
                foreach (WheelSocket s in VehicleMesher.Wheels(VehicleCatalog.At(v.Variant)))
                {
                    wheel.Clear();
                    VehicleMesher.BuildWheel(s.Radius, s.Width, VehicleLod.Lod1, wheel);
                    ObjDump.Append(body, wheel, s.X, s.Y, s.Z);
                }
                Place(life, body, v.X, v.Y, v.Z, v.YawDeg, v.PitchDeg, v.RollDeg);
                vehicles++;
            }
            var ground = new CarvedGround();
            ground.Add(CarvedGround.Prepare(t, 1));
            var sim = new AnimalSim(TrafficData.Graph, AnimalSettings.High(), 7) { Ground = ground };
            sim.SetFocus(id.X0 + cx, id.Z0 + cz, R);
            sim.Step(37f, 10f, 0f);
            var poses = new AnimalPose[256];
            int na = sim.CopyPoses(poses);
            var dog = new MeshData();
            for (int k = 0; k < na; k++)
            {
                AnimalPose a = poses[k];
                double lx = a.X - id.X0, lz = a.Z - id.Z0;
                if ((lx - cx) * (lx - cx) + (lz - cz) * (lz - cz) > R * R) continue;
                dog.Clear();
                bool cow = a.Kind == AnimalKind.Cow;
                bool lying = a.ClipId == (byte)AnimalClip.Sleep || a.ClipId == (byte)AnimalClip.Lie;
                // A lying (or standing) animal as a rounded body on the ground at the height the sim gives it.
                float len = cow ? 1.9f : 0.85f, wid = cow ? 0.75f : 0.32f, hgt = cow ? (lying ? 0.75f : 1.3f) : (lying ? 0.28f : 0.55f);
                uint tint = cow ? 0xEDE6D6FFu : 0xB8834DFFu;
                Shapes.Superellipsoid(dog, Affine3.Translation(0, 0.5 * hgt, 0), new ShapeBrush(tint, MaterialChannel.Fabric), 0.5 * wid, 0.5 * hgt, 0.5 * len, 0.6, 0.6, 16);
                Place(life, dog, (float)lx, a.Y, (float)lz, a.HeadingRad * 180f / (float)Math.PI);
                animals++;
            }
            double ex = cx - tx * 10 + tz * 0.0, ez = cz - tz * 10, lx2 = cx + tx * 25, lz2 = cz + tz * 25;
            float ey = ry + 3.2f, ly = ry + 0.4f;
            float x0 = (float)cx - R, z0 = (float)cz - R, x1 = (float)cx + R, z1 = (float)cz + R;
            CultureInfo ic = CultureInfo.InvariantCulture;
            string eye = string.Format(ic, "{0:0.##},{1:0.##},{2:0.##}", ex, ey, -ez), look = string.Format(ic, "{0:0.##},{1:0.##},{2:0.##}", lx2, ly, -lz2);
            // A side view across the road (to see the cross-section and the cut and fill).
            double sx = cx - tz * 22 - tx * 6, sz = cz + tx * 22 - tz * 6;
            string side = string.Format(ic, "{0:0.##},{1:0.##},{2:0.##}", sx, ry + 6.0, -sz), sideLook = string.Format(ic, "{0:0.##},{1:0.##},{2:0.##}", cx, ry - 1.0, -cz);
            NameRecord nm = t.Name(t.Roads[road].NameRef);
            var directives = new[]
            {
                "cutfill preview " + place.Name + " (" + tag + "): tile " + id + " road " + road + " (" + t.Roads[road].RoadClass + " '" + (nm != null ? nm.Default : "") +
                "') at " + along.ToString("0", ic) + " m; metres from the tile's south-west corner",
                "meshpreview: grid=0 sun=150,55 eye=" + eye + " look=" + look,
            };
            string dir = "cutfill/" + tag + "/" + place.Name + "/";
            var files = new List<string>();
            foreach (var (layer, mesh) in new (string, MeshData)[]
                     {
                         ("terrain", ObjDump.Crop(terrain, x0, z0, x1, z1, true)), ("roads", ObjDump.Crop(roads, x0, z0, x1, z1, true)),
                         ("decals", ObjDump.Crop(decals, x0, z0, x1, z1, true)), ("buildings", ObjDump.Crop(buildings, x0, z0, x1, z1, true)),
                         ("life", life),
                     })
            {
                if (mesh.TriangleCount == 0) continue;
                ObjDump.Write(mesh, dir + layer + ".obj", directives);
                files.Add("$D/" + place.Name + "/" + layer + ".obj");
            }
            sh.Append("D=").Append(Path.Combine(ObjDump.Dir, "cutfill", tag)).Append('\n');
            sh.Append("python3 tools/mesh-preview/render.py --combine ").Append(string.Join(" ", files)).Append(" -o $D/").Append(place.Name)
              .Append("_street.png --size 1000 --height 620 --fog 260 --pull 2e-5\n");
            sh.Append("python3 tools/mesh-preview/render.py --combine ").Append(string.Join(" ", files)).Append(" -o $D/").Append(place.Name)
              .Append("_side.png --eye ").Append(side).Append(" --look ").Append(sideLook).Append(" --size 1000 --height 620 --fog 260 --pull 2e-5\n");
            TestContext.WriteLine(place.Name + ": tile " + id + " road " + road + " at " + along.ToString("0", ic) + ", " + vehicles + " parked, " + animals +
                                  " animals, terrain " + terrain.TriangleCount + " roads " + roads.TriangleCount);
        }

        /// <summary>Appends <paramref name="src"/> turned as Unity's <c>Quaternion.Euler(pitch, yaw, roll)</c> (roll about
        /// forward, then pitch about right, then yaw clockwise from north) at tile-local (x, y, z).</summary>
        private static void Place(MeshData dst, MeshData src, float x, float y, float z, float yawDeg, float pitchDeg = 0f, float rollDeg = 0f)
        {
            int v0 = dst.VertexCount;
            ObjDump.Append(dst, src, 0, 0, 0);
            double a = yawDeg * Math.PI / 180.0, c = Math.Cos(a), s = Math.Sin(a);
            double p = pitchDeg * Math.PI / 180.0, cp = Math.Cos(p), sp = Math.Sin(p);
            double r = rollDeg * Math.PI / 180.0, cr = Math.Cos(r), sr = Math.Sin(r);
            for (int v = v0; v < dst.VertexCount; v++)
            {
                for (int k = 0; k < 2; k++)
                {
                    float[] arr = k == 0 ? dst.Positions : dst.Normals;
                    double px = arr[3 * v], py = arr[3 * v + 1], pz = arr[3 * v + 2];
                    // Roll (z), pitch (x), yaw (y).
                    double x1 = px * cr - py * sr, y1 = px * sr + py * cr;
                    double y2 = y1 * cp - pz * sp, z2 = y1 * sp + pz * cp;
                    double x3 = x1 * c + z2 * s, z3 = -x1 * s + z2 * c;
                    if (k == 0)
                    {
                        arr[3 * v] = (float)(x + x3);
                        arr[3 * v + 1] = (float)(y + y2);
                        arr[3 * v + 2] = (float)(z + z3);
                    }
                    else
                    {
                        arr[3 * v] = (float)x3;
                        arr[3 * v + 1] = (float)y2;
                        arr[3 * v + 2] = (float)z3;
                    }
                }
            }
        }
    }
}
