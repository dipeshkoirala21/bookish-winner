using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Ghumante.Core.Characters;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;
using Ghumante.Core.Streaming;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Preview only (writes nothing unless GHUMANTE_PREVIEW_DIR is set): the before/after frames of owner playtest #5. For
    /// each recorded ride of <see cref="CharactersCameraRideTests"/> it picks a few moments (a straight at cruise, the
    /// sharpest turn, for the scooter the narrowest lane), dumps the street there as the streamer builds it (tier High,
    /// tile-local OBJ) with the ridden vehicle placed on its recorded pose, and writes where the old and the new camera
    /// were at that frame of the ride, landscape and portrait (<c>rides/poses.txt</c>: name, orientation, before/after,
    /// eye, look and vertical field of view in tile-local Unity coordinates, for <c>render.py --unity</c>).
    /// </summary>
    public class CharactersCameraPreviewDumps
    {
        private struct Moment
        {
            public string Name;
            public int Frame;
        }

        private const float Landscape = 2400f / 1080f, Portrait = 1080f / 2400f, CropM = 150f;

        [Test]
        public void DumpRideMomentsForThePreviews()
        {
            if (!ObjDump.Enabled) Assert.Pass("set " + ObjDump.EnvVar + " to dump the camera previews");
            const int tier = 2;
            MeshingSettings s = StreamingScene.Settings(tier, 10);
            var builds = new Dictionary<ulong, TileBuild>();
            var near = new Dictionary<ulong, MeshData>();
            var poses = new StringBuilder();
            CultureInfo ci = CultureInfo.InvariantCulture;
            string path = Path.Combine(ObjDump.Dir, "rides", "poses.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            foreach (CharactersCameraRideTests.RideCase c in CharactersCameraRideTests.Rides)
            {
                List<RideFrame> frames = CharactersCameraRideTests.Record(c);
                List<CamSample> oldL = CharactersCameraRideTests.RunLegacy(c, frames, Landscape);
                List<CamSample> newL = CharactersCameraRideTests.RunNew(c, frames, Landscape);
                List<CamSample> oldP = CharactersCameraRideTests.RunLegacy(c, frames, Portrait);
                List<CamSample> newP = CharactersCameraRideTests.RunNew(c, frames, Portrait);
                string ride = c.Name.Split(' ')[0];
                foreach (Moment m in Moments(c, frames, oldL))
                {
                    RideFrame f = frames[m.Frame];
                    TileId id = TileId.At(10, f.X, f.Z);
                    TileBuild b;
                    if (!builds.TryGetValue(id.Key, out b))
                    {
                        b = StreamingScene.Build(id, s);
                        Assert.That(b.Error, Is.Null);
                        builds[id.Key] = b;
                        var houses = new MeshData();
                        BuildingMesher.Build(b.Source, b.Sampler, BuildingBandTable.LiteOptions(tier, s.DetailBuildings), houses);
                        near[id.Key] = houses;
                    }
                    string name = ride + "_" + m.Name;
                    string dir = "rides/" + name;
                    float vx = (float)(f.X - id.X0), vz = (float)(f.Z - id.Z0);
                    DumpStreet(b, near[id.Key], tier, dir, vx, vz);
                    DumpVehicle(c, f, dir, vx, f.Y, vz);
                    double t = 0;
                    for (int i = 0; i <= m.Frame; i++) t += frames[i].Dt;
                    poses.AppendFormat(ci, "# {0}: frame {1}, t {2:0.0} s, {3:0.0} m/s, heading {4:0.0}, tile {5}, vehicle {6:0.##},{7:0.##},{8:0.##}\n", name, m.Frame, t,
                                       f.SpeedMps, f.HeadingRad * Quat.Rad2Deg, id, vx, f.Y, vz);
                    Pose(poses, name, "landscape", "before", oldL[m.Frame], id);
                    Pose(poses, name, "landscape", "after", newL[m.Frame], id);
                    Pose(poses, name, "portrait", "before", oldP[m.Frame], id);
                    Pose(poses, name, "portrait", "after", newP[m.Frame], id);
                }
                File.WriteAllText(path, poses.ToString());
            }
            TestContext.WriteLine(poses.ToString());
        }

        private static void Pose(StringBuilder sb, string name, string orientation, string tag, in CamSample cam, TileId id)
        {
            V3 forward = cam.Rot * new V3(0f, 0f, 1f);
            double ex = cam.X - id.X0, ey = cam.Y, ez = cam.Z - id.Z0;
            sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} {2} {3:0.###},{4:0.###},{5:0.###} {6:0.###},{7:0.###},{8:0.###} {9:0.##} {10:0.##}\n", name, orientation,
                            tag, ex, ey, ez, ex + forward.X * 10.0, ey + forward.Y * 10.0, ez + forward.Z * 10.0, cam.VFovDeg, cam.BoomM);
        }

        /// <summary>A straight at cruise, the sharpest turn and (two-wheelers) the narrowest lane: frames at least 120 m
        /// inside their tile so the dumped street surrounds the camera.</summary>
        private static IEnumerable<Moment> Moments(CharactersCameraRideTests.RideCase c, List<RideFrame> f, List<CamSample> old)
        {
            int n = f.Count;
            var t = new double[n];
            for (int i = 1; i < n; i++) t[i] = t[i - 1] + f[i].Dt;
            int straight = -1, turn = -1, lane = -1;
            double bestMargin = 0, bestRate = 0, shortest = double.MaxValue;
            for (int i = 1; i < n; i++)
            {
                if (t[i] < 12.0) continue;
                double margin = Margin(f[i]);
                if (margin < 120.0) continue;
                int a = i, b = i;
                while (a > 0 && t[i] - t[a] < 0.25) a--;
                while (b < n - 1 && t[b] - t[i] < 0.25) b++;
                double rate = Math.Abs(CameraAngleSpring.Delta(f[a].HeadingRad * Quat.Rad2Deg, f[b].HeadingRad * Quat.Rad2Deg)) / Math.Max(1e-3, t[b] - t[a]);
                if (f[i].SpeedMps > 2.5f && rate > bestRate)
                {
                    bestRate = rate;
                    turn = i;
                }
                if (f[i].SpeedMps >= 0.85f * c.CruiseMps && margin > bestMargin + 20.0 && Steady(f, t, i, 1.5))
                {
                    bestMargin = margin;
                    straight = i;
                }
                if (c.Rig == RigClass.TwoWheeler && f[i].SpeedMps > 2f && old[i].BoomM < shortest)
                {
                    shortest = old[i].BoomM;
                    lane = i;
                }
            }
            if (straight >= 0) yield return new Moment { Name = "straight", Frame = straight };
            if (turn >= 0) yield return new Moment { Name = "turn", Frame = turn };
            if (lane >= 0) yield return new Moment { Name = "lane", Frame = lane };
        }

        private static bool Steady(List<RideFrame> f, double[] t, int i, double seconds)
        {
            float h = f[i].HeadingRad * Quat.Rad2Deg;
            for (int k = i; k >= 0 && t[i] - t[k] <= seconds; k--)
                if (Math.Abs(CameraAngleSpring.Delta(h, f[k].HeadingRad * Quat.Rad2Deg)) > 1.5f) return false;
            for (int k = i; k < f.Count && t[k] - t[i] <= seconds; k++)
                if (Math.Abs(CameraAngleSpring.Delta(h, f[k].HeadingRad * Quat.Rad2Deg)) > 1.5f) return false;
            return true;
        }

        private static double Margin(in RideFrame f)
        {
            TileId id = TileId.At(10, f.X, f.Z);
            double x = f.X - id.X0, z = f.Z - id.Z0;
            return Math.Min(Math.Min(x, id.Size - x), Math.Min(z, id.Size - z));
        }

        private static void DumpStreet(TileBuild b, MeshData houses, int tier, string dir, float vx, float vz)
        {
            float x0 = vx - CropM, z0 = vz - CropM, x1 = vx + CropM, z1 = vz + CropM;
            void Put(string layer, MeshData m)
            {
                MeshData cropped = ObjDump.Crop(m, x0, z0, x1, z1, true);
                if (cropped.TriangleCount > 0) ObjDump.Write(cropped, dir + "/" + layer + ".obj");
            }
            Put("terrain", StreamingScene.Layer(b, TileLayers.Terrain));
            Put("roads", StreamingScene.Layer(b, TileLayers.Roads));
            Put("decals", StreamingScene.Layer(b, TileLayers.RoadDecals));
            Put("areas", StreamingScene.Layer(b, TileLayers.Areas));
            Put("bridges", StreamingScene.Pieces(b, TileLayers.Bridges, tier, vx, vz));
            Put("ornaments", StreamingScene.Pieces(b, TileLayers.Ornaments, tier, vx, vz));
            Put("heroes", StreamingScene.Layer(b, TileLayers.Heroes, p => p % 4 == 0));
            float r0 = BuildingBandTable.OuterM(tier, 0);
            Put("buildings_b0", ObjDump.Crop(houses, vx - r0, vz - r0, vx + r0, vz + r0, true));
            Put("buildings_b1", ObjDump.Crop(StreamingScene.Layer(b, TileLayers.Buildings), vx - r0, vz - r0, vx + r0, vz + r0, false));
        }

        private static void DumpVehicle(CharactersCameraRideTests.RideCase c, in RideFrame f, string dir, float x, float y, float z)
        {
            int variant = CharactersCameraRideTests.VariantOf(c.Preset);
            var unit = new MeshData();
            VehicleMesher.Build(variant, 0, VehicleLod.Lod0, unit, 77u, c.Rig == RigClass.TwoWheeler || c.Rig == RigClass.Bicycle);
            var wheel = new MeshData();
            foreach (WheelSocket w in VehicleMesher.Wheels(VehicleCatalog.At(variant), VehicleMesher.ModelFor(variant, 77u)))
            {
                wheel.Clear();
                VehicleMesher.BuildWheel(w, VehicleLod.Lod0, wheel);
                ObjDump.Append(unit, wheel, w.X, w.Y, w.Z);
            }
            var placed = new MeshData();
            placed.HasUv0 = true;
            FloraObj.Append(placed, unit, x, y, z, f.HeadingRad * Quat.Rad2Deg);
            ObjDump.Write(placed, dir + "/vehicle.obj");
        }
    }
}
