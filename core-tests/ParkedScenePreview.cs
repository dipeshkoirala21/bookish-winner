using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Ghumante.Core.Characters;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Generators.Placement;
using Ghumante.Core.Generators.Props;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;
using Ghumante.Core.Search;
using Ghumante.Core.Streaming;
using Ghumante.Core.Traffic;
using Ghumante.World.Instancing;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Visual check of the owner's playtest items #3, #6 and #11 (package play): street-level scenes of the sample pack as
    /// the streamer builds them (TileBuild.Execute on High) with every parked vehicle drawn at LOD0 where
    /// <see cref="ParkedPlacement"/> puts it, and the moving traffic of <see cref="TrafficSim"/> (High settings, the bus
    /// runner, the player standing at the shot's target and the camera's view for the out-of-view spawns) after a minute
    /// and a half, drawn at LOD0 with its riders. Writes nothing unless <c>GHUMANTE_PREVIEW_DIR</c> is set; then render
    /// each folder with <c>tools/mesh-preview/render.py --combine &lt;dir&gt;/*.obj</c> (the files carry the eye).
    /// </summary>
    public class ParkedScenePreview
    {
        /// <summary>A view: the target (a named road's middle, a search hit or a point), the eye <see cref="Back"/> metres
        /// behind it along the road (or towards <see cref="Bearing"/>) at <see cref="EyeH"/>.</summary>
        private struct Shot
        {
            public string Name, Road, Query;
            public double Lon, Lat;

            /// <summary>Degrees clockwise from north the eye stands from the target; NaN: behind it along the nearest road.</summary>
            public double Bearing;

            public double Back;
            public float EyeH, LookH;

            /// <summary>Metres ahead of the eye the camera looks along the road (default 14).</summary>
            public double Ahead;

            /// <summary>Snap to the nearest main road (trunk to tertiary) instead of any vehicle road.</summary>
            public bool MainRoad;
        }

        private static readonly Shot[] Shots =
        {
            // The owner's screenshot lane (red LOD boxes across a 4-5 m lane): a rider's view along it.
            new Shot { Name = "ganesh_galli_lane", Road = "Ganesh Galli", Bearing = double.NaN, Back = 22, EyeH = 2.4f, LookH = 1.0f },
            // Kalopul, where the owner saw scooters parked at angles in the carriageway.
            new Shot { Name = "kalopul_lane", Road = "Kalopul", Bearing = double.NaN, Back = 22, EyeH = 2.4f, LookH = 1.0f },
            new Shot { Name = "baluwatar_lanes", Lon = 85.3297, Lat = 27.7279, Bearing = double.NaN, Back = 24, EyeH = 2.6f, LookH = 1.0f },
            // Busy bazaars and main roads: no parked two-wheelers, riders moving instead; traffic on the left.
            new Shot { Name = "thamel_marg_bazaar", Road = "Thamel Marg", Bearing = double.NaN, Back = 8, EyeH = 5f, LookH = 0.5f, Ahead = 30 },
            new Shot { Name = "new_road_bazaar", Road = "New Road", Bearing = double.NaN, Back = 26, EyeH = 2.8f, LookH = 1.0f },
            new Shot { Name = "new_baneshwor_main", Lon = 85.3355, Lat = 27.6890, MainRoad = true, Bearing = double.NaN, Back = 10, EyeH = 6f, LookH = 0.5f, Ahead = 45 },
            new Shot { Name = "ring_road_chabahil", Lon = 85.3462, Lat = 27.7120, MainRoad = true, Bearing = double.NaN, Back = 10, EyeH = 6f, LookH = 0.5f, Ahead = 45 },
            // Heavy vehicles where they stand (README list): the bus parks.
            new Shot { Name = "ratna_park_bus_park", Query = "Ratna Park bus station", Bearing = 200, Back = 38, EyeH = 9f, LookH = 1.5f },
            new Shot { Name = "gongabu_bus_park", Query = "Naya Bus Park, Gongabu", Bearing = 120, Back = 40, EyeH = 10f, LookH = 1.5f },
        };

        [Test]
        public void DumpParkedAndTrafficScenes()
        {
            if (!ObjDump.Enabled) Assert.Pass("set " + ObjDump.EnvVar + " to dump the scenes");
            var c = CultureInfo.InvariantCulture;
            var settings = new MeshingSettings();
            var ground = new TileGroundQuery(settings.Roads);
            var cache = new Dictionary<ulong, TileBuild>();
            var index = new StringBuilder();
            var unit = new MeshData();
            var wheel = new MeshData();
            string only = Environment.GetEnvironmentVariable("PLAY_SHOTS");
            foreach (Shot shot in Shots)
            {
                if (!string.IsNullOrEmpty(only) && Array.IndexOf(only.Split(','), shot.Name) < 0) continue;
                TileId id;
                double cx, cz;
                if (!Locate(shot, out id, out cx, out cz))
                {
                    index.Append(shot.Name).Append(": not found\n");
                    continue;
                }
                TileBuild b;
                if (!cache.TryGetValue(id.Key, out b))
                {
                    b = new TileBuild { Node = new SelectedNode(id, id) };
                    TileBuild.Execute(b, StreamingSampleRegion.Pack, StreamingConfig.ForTier(2), settings, ground);
                    cache[id.Key] = b;
                }
                Assert.That(b.Error, Is.Null);
                // The camera: behind the target along its road (a chase camera's line), or from a bearing.
                double dirX, dirZ, eyeX, eyeZ;
                if (double.IsNaN(shot.Bearing))
                {
                    AlongRoad(b.Source, shot.Road, shot.MainRoad, cx, cz, shot.Back, shot.Ahead > 0 ? shot.Ahead : 14, out eyeX, out eyeZ, out cx, out cz);
                    double l = Math.Sqrt((cx - eyeX) * (cx - eyeX) + (cz - eyeZ) * (cz - eyeZ));
                    dirX = l > 1e-6 ? (cx - eyeX) / l : 0;
                    dirZ = l > 1e-6 ? (cz - eyeZ) / l : 1;
                }
                else
                {
                    double a = shot.Bearing * Math.PI / 180;
                    dirX = -Math.Sin(a);
                    dirZ = -Math.Cos(a);
                    eyeX = cx - dirX * shot.Back;
                    eyeZ = cz - dirZ * shot.Back;
                }
                float tx = (float)cx, tz = (float)cz;
                float ex = (float)eyeX, ez = (float)eyeZ;
                float ey = Ground(b, ex, ez) + shot.EyeH, ly = Ground(b, tx, tz) + shot.LookH;
                string eye = string.Format(c, "{0:0.##},{1:0.##},{2:0.##}", ex, ey, -ez), look = string.Format(c, "{0:0.##},{1:0.##},{2:0.##}", tx, ly, -tz);
                var directives = new[] { "play scene " + shot.Name + " tile " + id, "meshpreview: grid=0 sun=150,58 eye=" + eye + " look=" + look };
                const float r = 220f;
                float x0 = Math.Min(ex, tx) - r, z0 = Math.Min(ez, tz) - r, x1 = Math.Max(ex, tx) + r, z1 = Math.Max(ez, tz) + r;
                foreach (int layer in new[] { TileLayers.Terrain, TileLayers.Roads, TileLayers.RoadDecals, TileLayers.Areas, TileLayers.Buildings })
                {
                    MeshData m = ObjDump.Crop(b.Layers[layer], x0, z0, x1, z1, true);
                    if (m.TriangleCount > 0) ObjDump.Write(m, shot.Name + "/layer" + layer + ".obj", directives);
                }
                // The houses: the B0 detail band near the eye (as the game streams it round the camera), B1 beyond.
                var band = new MeshData();
                const float near = 110f;
                float bx0 = Math.Min(ex, tx) - near, bz0 = Math.Min(ez, tz) - near, bx1 = Math.Max(ex, tx) + near, bz1 = Math.Max(ez, tz) + near;
                BuildingMesher.Build(b.Source, b.Sampler, new BuildingOptions { Band = BuildingBand.B0KitLite, HiddenRefs = Outside(b.Source, bx0, bz0, bx1, bz1) }, band);
                MeshData houses = ObjDump.Crop(band, bx0, bz0, bx1, bz1, true);
                if (houses.TriangleCount > 0) ObjDump.Write(houses, shot.Name + "/buildings.obj", directives);
                band.Clear();
                BuildingMesher.Build(b.Source, b.Sampler, new BuildingOptions { Band = BuildingBand.B1Styled }, band);
                MeshData far = ObjDump.Crop(ObjDump.Crop(band, bx0, bz0, bx1, bz1, false), x0, z0, x1, z1, true);
                if (far.TriangleCount > 0) ObjDump.Write(far, shot.Name + "/buildings_b1.obj", directives);
                var props = new MeshData();
                foreach (StreetProp sp in b.Instances.Props)
                {
                    float dx = sp.X - ex, dz = sp.Z - ez;
                    if (dx * dx + dz * dz > r * r) continue;
                    unit.Clear();
                    StreetPropMesher.Build(sp.Kind, 0, unit);
                    float sc = sp.HeightM > 0 ? sp.HeightM / StreetPropMesher.DefaultHeight(sp.Kind) : 1f;
                    FloraObj.Append(props, unit, sp.X, sp.Y, sp.Z, sp.YawDeg, sc, sc, sc);
                }
                if (props.TriangleCount > 0) ObjDump.Write(props, shot.Name + "/props.obj", directives);

                // Parked vehicles exactly where the streamer placed them.
                var parked = new MeshData();
                int nParked = 0, nTwo = 0, nHeavy = 0;
                foreach (ParkedVehicle v in b.Instances.Parked)
                {
                    float dx = v.X - ex, dz = v.Z - ez;
                    if (dx * dx + dz * dz > 120f * 120f) continue;
                    AppendVehicle(parked, unit, wheel, v.Variant, v.Livery, v.X, v.Y, v.Z, v.YawDeg, false);
                    nParked++;
                    BodyShape s = VehicleCatalog.At(v.Variant).Shape;
                    if (VehicleRoles.IsTwoWheeler(s) || s == BodyShape.Bicycle) nTwo++;
                    if (s == BodyShape.Bus || s == BodyShape.Truck || s == BodyShape.Tipper || s == BodyShape.Tanker || s == BodyShape.Tractor ||
                        s == BodyShape.Van || s == BodyShape.Tempo)
                        nHeavy++;
                }
                if (parked.TriangleCount > 0) ObjDump.Write(parked, shot.Name + "/parked.obj", directives);

                // The moving traffic round the player after 90 s (High, as LifeHost runs it), the camera looking along the road.
                var traffic = new MeshData();
                int nMoving = 0, nRiders = 0;
                {
                    LaneGraph g = TrafficData.Graph;
                    double px = id.X0 + cx, pz = id.Z0 + cz;
                    var sim = new TrafficSim(g, TrafficSettings.High(), 17) { Sacred = TrafficData.Zones };
                    var bus = new BusRouteRunner(TrafficData.Routes, g, sim);
                    sim.SetFocus(px, pz, 0f);
                    sim.SetView((float)dirX, (float)dirZ);
                    const float dt = 0.1f;
                    for (int step = 0; step < 900; step++)
                    {
                        float hour = 8.5f + step * dt / 3600f;
                        bus.Step(dt, hour);
                        sim.Step(dt, hour, 0f);
                    }
                    var poses = new AgentPose[512];
                    int n = sim.CopyPoses(poses);
                    for (int k = 0; k < n; k++)
                    {
                        AgentPose p = poses[k];
                        if ((p.AnimState & (byte)AgentAnim.Ghost) != 0) continue;
                        float lx = (float)(p.X - id.X0), lz = (float)(p.Z - id.Z0);
                        float dx = lx - ex, dz = lz - ez;
                        if (dx * dx + dz * dz > 160f * 160f || lx < 0 || lz < 0 || lx > id.Size || lz > id.Size) continue;
                        bool two = p.Class == VehicleClass.TwoWheeler || p.Class == VehicleClass.Bicycle;
                        AppendVehicle(traffic, unit, wheel, p.Variant, p.Livery, lx, Ground(b, lx, lz), lz, (float)(p.HeadingRad * 180 / Math.PI), two);
                        nMoving++;
                        if (two) nRiders++;
                    }
                }
                if (traffic.TriangleCount > 0) ObjDump.Write(traffic, shot.Name + "/traffic.obj", directives);
                index.Append(string.Format(c, "{0}: tile {1} eye {2} look {3}; parked within 120 m {4} ({5} two-wheelers, {6} vans/heavy), moving within 160 m {7} ({8} riders)\n",
                                           shot.Name, id, eye, look, nParked, nTwo, nHeavy, nMoving, nRiders));
            }
            File.WriteAllText(Path.Combine(ObjDump.Dir, "play_scenes.txt"), index.ToString());
            TestContext.WriteLine(index.ToString());
        }

        /// <summary>Appends a catalogue vehicle at LOD0 with its wheels (rear axle at the point, compass yaw).</summary>
        private static void AppendVehicle(MeshData into, MeshData unit, MeshData wheel, int variant, byte livery, float x, float y, float z, float yawDeg, bool rider)
        {
            VehicleCatalogEntry e = VehicleCatalog.At(variant);
            unit.Clear();
            VehicleMesher.Build(variant, livery, VehicleLod.Lod0, unit, 1234u, rider);
            foreach (WheelSocket s in VehicleMesher.Wheels(e))
            {
                wheel.Clear();
                VehicleMesher.BuildWheel(s, VehicleLod.Lod0, wheel);
                ObjDump.Append(unit, wheel, s.X, s.Y, s.Z);
            }
            FloraObj.Append(into, unit, x, y, z, yawDeg);
        }

        /// <summary>Snaps (x, z) to the nearest vehicle road of the tile (the named one when <paramref name="name"/> is set)
        /// and walks its polyline <paramref name="back"/> metres back for the eye; the target is <paramref name="ahead"/> metres on from
        /// the eye.</summary>
        private static void AlongRoad(TileData t, string name, bool mainRoad, double x, double z, double back, double ahead, out double ex, out double ez, out double tx,
                                      out double tz)
        {
            double best = double.MaxValue, bestS = 0;
            int bestRoad = -1;
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                if (r.Points == null || r.PointCount < 2 || r.RoadClass == RoadClass.Unknown || r.RoadClass > (mainRoad ? RoadClass.Tertiary : RoadClass.Road)) continue;
                if (name != null)
                {
                    NameRecord nm = t.Name(r.NameRef);
                    if (nm == null || nm.Default != name && nm.En != name) continue;
                }
                double sAt = 0;
                for (int k = 0; k + 1 < r.PointCount; k++)
                {
                    double ax = r.Points[2 * k] / 100.0, az = r.Points[2 * k + 1] / 100.0;
                    double vx = r.Points[2 * k + 2] / 100.0 - ax, vz = r.Points[2 * k + 3] / 100.0 - az, l2 = vx * vx + vz * vz, l = Math.Sqrt(l2);
                    if (l2 < 1e-6) continue;
                    double u = Math.Max(0, Math.Min(1, ((x - ax) * vx + (z - az) * vz) / l2));
                    double qx = ax + vx * u, qz = az + vz * u, d = (qx - x) * (qx - x) + (qz - z) * (qz - z);
                    if (d < best)
                    {
                        best = d;
                        bestRoad = i;
                        bestS = sAt + u * l;
                    }
                    sAt += l;
                }
            }
            Assert.That(bestRoad, Is.GreaterThanOrEqualTo(0), "a vehicle road near the shot");
            RoadRecord road = t.Roads[bestRoad];
            double total = 0;
            for (int k = 0; k + 1 < road.PointCount; k++) total += Seg(road, k);
            // Keep the target 10 m short of the road's end and the eye on the road.
            double sTarget = Math.Max(Math.Min(back, total), Math.Min(bestS, total - 10));
            double sEye = Math.Max(0, sTarget - back);
            At(road, sEye, out ex, out ez);
            // Look along the road where the eye stands (a chase camera's line), not across a bend at the far target.
            At(road, Math.Min(total, sEye + ahead), out tx, out tz);
        }

        private static double Seg(RoadRecord r, int k)
        {
            double dx = (r.Points[2 * k + 2] - r.Points[2 * k]) / 100.0, dz = (r.Points[2 * k + 3] - r.Points[2 * k + 1]) / 100.0;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        private static void At(RoadRecord r, double s, out double x, out double z)
        {
            for (int k = 0; k + 1 < r.PointCount; k++)
            {
                double l = Seg(r, k);
                if (s > l && k + 2 < r.PointCount)
                {
                    s -= l;
                    continue;
                }
                double f = l > 1e-9 ? Math.Max(0, Math.Min(1, s / l)) : 0;
                x = (r.Points[2 * k] + (r.Points[2 * k + 2] - r.Points[2 * k]) * f) / 100.0;
                z = (r.Points[2 * k + 1] + (r.Points[2 * k + 3] - r.Points[2 * k + 1]) * f) / 100.0;
                return;
            }
            x = r.Points[0] / 100.0;
            z = r.Points[1] / 100.0;
        }

        /// <summary>The buildings of the tile with no footprint corner inside the box (left out of the B0 build).</summary>
        private static HashSet<ulong> Outside(TileData t, double x0, double z0, double x1, double z1)
        {
            var set = new HashSet<ulong>();
            foreach (BuildingRecord b in t.Buildings)
            {
                if (b.Rings == null || b.Rings.Length == 0) continue;
                bool inside = false;
                int[] ring = b.Rings[0];
                for (int k = 0; k + 1 < ring.Length && !inside; k += 2)
                    inside = ring[k] / 100.0 >= x0 && ring[k] / 100.0 <= x1 && ring[k + 1] / 100.0 >= z0 && ring[k + 1] / 100.0 <= z1;
                if (!inside) set.Add(b.OsmRef);
            }
            return set;
        }

        private static bool Locate(Shot w, out TileId id, out double cx, out double cz)
        {
            id = default(TileId);
            cx = cz = 0;
            if (w.Query != null)
            {
                string path = Path.Combine(GoldenFiles.RepoRoot, "shared", "sample-regions", "kathmandu_core", "kathmandu_core.search.ghsi");
                var engine = new SearchEngine(SearchIndexReader.Read(File.ReadAllBytes(path)));
                var hits = engine.Search(w.Query, 1);
                if (hits.Count == 0) return false;
                id = TileId.At(10, hits[0].Entry.X, hits[0].Entry.Z);
                cx = hits[0].Entry.X - id.X0;
                cz = hits[0].Entry.Z - id.Z0;
                return StreamingSampleRegion.Tile(id) != null;
            }
            if (w.Road == null)
            {
                double x, z;
                WorldFrame.LonLatToGame(w.Lon, w.Lat, out x, out z);
                id = TileId.At(10, x, z);
                cx = x - id.X0;
                cz = z - id.Z0;
                return StreamingSampleRegion.Tile(id) != null;
            }
            foreach (TileId tid in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(tid);
                if (t == null) continue;
                foreach (RoadRecord r in t.Roads)
                {
                    NameRecord nm = t.Name(r.NameRef);
                    if (nm == null || nm.Default != w.Road && nm.En != w.Road || r.Points == null || r.PointCount < 2) continue;
                    int mid = r.PointCount / 2;
                    id = tid;
                    cx = r.Points[2 * mid] / 100.0;
                    cz = r.Points[2 * mid + 1] / 100.0;
                    return true;
                }
            }
            return false;
        }

        private static float Ground(TileBuild b, double x, double z)
        {
            double gx = b.Node.Area.X0 + x, gz = b.Node.Area.Z0 + z;
            float y;
            b.Sampler.TryHeightClamped(gx, gz, out y);
            RoadSurfaceHit hit;
            if (RoadSurfaceQuery.For(b.Source, b.Sampler, new RoadOptions()).TrySample(gx, gz, y + 1f, out hit)) return hit.Y;
            return y;
        }
    }
}
