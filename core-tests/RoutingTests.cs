using System;
using System.Linq;
using System.Text.Json;
using Ghumante.Core.Data;
using Ghumante.Core.Routing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class RoutingTests
    {
        private static RouteGraph Graph()
        {
            return RouteGraphReader.Read(GoldenFiles.Bytes("golden.ghrg"));
        }

        [Test]
        public void ProfilesMatchPythonTables()
        {
            JsonElement x = GoldenFiles.Json("golden_profiles.json");
            Assert.That(TravelProfiles.AlpineSac, Is.EqualTo(x.GetProperty("alpine_sac").GetInt32()));
            var trails = x.GetProperty("trail_classes").EnumerateArray().Select(e => e.GetString()).ToList();
            for (int c = 0; c < 18; c++)
                Assert.That(TravelProfiles.IsTrail(c), Is.EqualTo(trails.Contains(Upper((RoadClass)c))), "class " + c);
            int n = 0;
            foreach (JsonProperty p in x.GetProperty("profiles").EnumerateObject())
            {
                TravelProfile prof = TravelProfiles.Get(p.Name);
                Assert.That((int)prof.Travel, Is.EqualTo(p.Value.GetProperty("bit").GetInt32()));
                Assert.That(prof.Name, Is.EqualTo(p.Name));
                Assert.That(prof.MaxSac, Is.EqualTo(p.Value.GetProperty("max_sac").GetInt32()));
                Assert.That(prof.ClimbK, Is.EqualTo(p.Value.GetProperty("climb_k").GetDouble()));
                Assert.That(prof.MaxSpeedKmh, Is.EqualTo(p.Value.GetProperty("max_speed_kmh").GetDouble()));
                JsonElement alpine = p.Value.GetProperty("alpine_speed_kmh");
                Assert.That(prof.AlpineSpeedKmh, Is.EqualTo(alpine.ValueKind == JsonValueKind.Null ? double.PositiveInfinity : alpine.GetDouble()));
                var speeds = p.Value.GetProperty("speed_kmh").EnumerateObject().ToArray();
                Assert.That(prof.SpeedKmh.Length, Is.EqualTo(speeds.Length));
                for (int c = 0; c < speeds.Length; c++) Assert.That(prof.SpeedKmh[c], Is.EqualTo(speeds.First(s => s.Name == Upper((RoadClass)c)).Value.GetDouble()));
                foreach (JsonProperty g in p.Value.GetProperty("surface_factor").EnumerateObject())
                    Assert.That(prof.SurfaceFactor[(int)(SurfaceGroup)Enum.Parse(typeof(SurfaceGroup), g.Name, true)], Is.EqualTo(g.Value.GetDouble()));
                Assert.That(TravelProfiles.Get(prof.Travel), Is.SameAs(prof));
                n++;
            }
            Assert.That(n, Is.EqualTo(7));
            Assert.Throws<ArgumentException>(() => TravelProfiles.Get(Travel.Foot | Travel.Car));
            Assert.Throws<ArgumentException>(() => TravelProfiles.Get("rocket"));
        }

        private static string Upper(RoadClass c)
        {
            return string.Concat(c.ToString().Select((ch, i) => i > 0 && char.IsUpper(ch) ? "_" + ch : ch.ToString())).ToUpperInvariant();
        }

        [Test]
        public void ProfileRules()
        {
            TravelProfile foot = TravelProfiles.Get(Travel.Foot), car = TravelProfiles.Get(Travel.Car), jeep = TravelProfiles.Get(Travel.Jeep);
            Assert.That(car.Speed((int)RoadClass.Path, (int)Surface.Asphalt, 0), Is.EqualTo(0.0));
            Assert.That(foot.Speed((int)RoadClass.Path, (int)Surface.Rock, 5), Is.EqualTo(3.5));
            Assert.That(TravelProfiles.Get(Travel.Bicycle).Allows((int)RoadClass.Path, 3), Is.False);
            Assert.That(jeep.Allows((int)RoadClass.Track, 5), Is.True); // sac limit only applies to trails
            Assert.That(car.Speed((int)RoadClass.Primary, 250, 0), Is.EqualTo(70.0 * 0.6)); // unknown surface: DIRT
            Assert.That(car.Speed(200, (int)Surface.Asphalt, 0), Is.EqualTo(0.0));
            Assert.That(TravelProfiles.UsableMask((int)RoadClass.Steps, 0), Is.EqualTo(Travel.Foot | Travel.Bicycle));
            Assert.That(car.EdgeTimeS(Travel.Car, (int)RoadClass.Primary, (int)Surface.Asphalt, 0, 700, 0), Is.EqualTo(70.0 * 3.6 / 70.0));
            Assert.That(car.EdgeTimeS(Travel.Foot, (int)RoadClass.Primary, (int)Surface.Asphalt, 0, 700, 0), Is.EqualTo(double.PositiveInfinity));
            Assert.That(foot.EdgeTimeS(Travel.Foot, (int)RoadClass.Path, (int)Surface.Dirt, 0, 1000, 10), Is.EqualTo(100.0 * 3.6 / 5.0 * 1.6).Within(1e-12));
            Assert.That(foot.EdgeTimeS(Travel.Foot, (int)RoadClass.Path, (int)Surface.Dirt, 0, 0, 10), Is.EqualTo(0.0));
        }

        [Test]
        public void GraphMatchesPythonDecode()
        {
            RouteGraph g = Graph();
            JsonElement x = GoldenFiles.Json("golden_routes.json");
            Assert.That(g.NodeCount, Is.EqualTo(x.GetProperty("node_count").GetInt32()));
            Assert.That(g.EdgeCount, Is.EqualTo(x.GetProperty("edge_count").GetInt32()));
            Assert.That(g.Geometry.Length, Is.EqualTo(x.GetProperty("geom_bytes").GetInt32()));
            Assert.That(g.NodeXDm, Is.EqualTo(GoldenFiles.Ints(x.GetProperty("node_x_dm"))));
            Assert.That(g.NodeZDm, Is.EqualTo(GoldenFiles.Ints(x.GetProperty("node_z_dm"))));
            Assert.That(g.NodeElev.Select(v => (int)v), Is.EqualTo(GoldenFiles.Ints(x.GetProperty("node_elev"))));
            Assert.That(g.NodeElev, Does.Contain(RouteGraph.ElevUnknown));
            Assert.That(g.Offsets, Is.EqualTo(GoldenFiles.Ints(x.GetProperty("offsets"))));
            JsonElement ed = x.GetProperty("edges");
            Assert.That(g.EdgeTarget, Is.EqualTo(GoldenFiles.Ints(ed.GetProperty("edge_target"))));
            Assert.That(g.EdgeLengthDm.Select(v => (long)v), Is.EqualTo(GoldenFiles.Longs(ed.GetProperty("edge_length_dm"))));
            Assert.That(g.EdgeClass.Select(v => (int)v), Is.EqualTo(GoldenFiles.Ints(ed.GetProperty("edge_class"))));
            Assert.That(g.EdgeSurface.Select(v => (int)v), Is.EqualTo(GoldenFiles.Ints(ed.GetProperty("edge_surface"))));
            Assert.That(g.EdgeAccess.Select(v => (int)v), Is.EqualTo(GoldenFiles.Ints(ed.GetProperty("edge_access"))));
            Assert.That(g.EdgeFlags.Select(v => (int)v), Is.EqualTo(GoldenFiles.Ints(ed.GetProperty("edge_flags"))));
            Assert.That(g.EdgeSac.Select(v => (int)v), Is.EqualTo(GoldenFiles.Ints(ed.GetProperty("edge_sac"))));
            Assert.That(g.EdgeClimb.Select(v => (int)v), Is.EqualTo(GoldenFiles.Ints(ed.GetProperty("edge_climb"))));
            Assert.That(g.EdgeGeomOffset.Select(v => (long)v), Is.EqualTo(GoldenFiles.Longs(ed.GetProperty("edge_geom_offset"))));
            Assert.That(g.EdgeName.Select(v => (int)v), Is.EqualTo(GoldenFiles.Ints(ed.GetProperty("edge_name"))));
            Assert.That(g.EdgeGeomCount.Select(v => (int)v), Is.EqualTo(GoldenFiles.Ints(ed.GetProperty("edge_geom_count"))));
            var geoms = x.GetProperty("edge_geometry_dm").EnumerateArray().ToArray();
            for (int e = 0; e < g.EdgeCount; e++)
            {
                Assert.That(g.EdgeGeometryDm(e), Is.EqualTo(GoldenFiles.Longs(geoms[e])), "edge " + e);
                double[] m = g.EdgeGeometry(e);
                Assert.That(m[m.Length - 2], Is.EqualTo(g.NodeX(g.EdgeTarget[e])));
            }
            var names = x.GetProperty("names").EnumerateArray().ToArray();
            Assert.That(g.Names.Length, Is.EqualTo(names.Length));
            for (int i = 0; i < names.Length; i++) GoldenFiles.AssertName(names[i], g.Names[i], "name " + i);
            Assert.That(Enumerable.Range(0, g.EdgeCount).Any(e => g.EdgeNameRecord(e) != null && g.EdgeNameRecord(e).Ne == "चक्रपथ"), Is.True);
        }

        [Test]
        public void EdgeTimesBitIdentical()
        {
            var astar = new AStar(Graph());
            foreach (JsonProperty p in GoldenFiles.Json("golden_routes.json").GetProperty("edge_times").EnumerateObject())
            {
                double[] t = astar.EdgeTimes(TravelProfiles.Get(p.Name).Travel);
                var expected = p.Value.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.Null ? double.PositiveInfinity : e.GetDouble()).ToArray();
                Assert.That(t, Is.EqualTo(expected), p.Name);
            }
        }

        [Test]
        public void RoutesIdenticalToPython()
        {
            var astar = new AStar(Graph());
            int found = 0, missing = 0;
            foreach (JsonElement r in GoldenFiles.Json("golden_routes.json").GetProperty("routes").EnumerateArray())
            {
                Travel profile = TravelProfiles.Get(r.GetProperty("profile").GetString()).Travel;
                int src = r.GetProperty("src").GetInt32(), dst = r.GetProperty("dst").GetInt32();
                string what = r.GetProperty("profile").GetString() + " " + src + "->" + dst;
                Route got = astar.FindRoute(src, dst, profile);
                if (!r.GetProperty("found").GetBoolean())
                {
                    Assert.That(got, Is.Null, what);
                    Assert.That(astar.FindRoute(src, dst, profile, heuristic: false), Is.Null, what);
                    missing++;
                    continue;
                }
                found++;
                Assert.That(got, Is.Not.Null, what);
                Assert.That(got.Nodes, Is.EqualTo(GoldenFiles.Ints(r.GetProperty("nodes"))), what);
                Assert.That(got.Edges, Is.EqualTo(GoldenFiles.Ints(r.GetProperty("edges"))), what);
                double t = r.GetProperty("time_s").GetDouble();
                Assert.That(got.TimeS, Is.EqualTo(t).Within(1e-6 * Math.Max(1.0, t)), what);
                Assert.That(got.TimeS, Is.EqualTo(t), what + " (bit-identical)");
                Assert.That(got.LengthM, Is.EqualTo(r.GetProperty("length_m").GetDouble()), what);

                Route dij = astar.FindRoute(src, dst, profile, heuristic: false);
                Assert.That(dij.TimeS, Is.EqualTo(t).Within(1e-9 * Math.Max(1.0, t)), what + " dijkstra");

                double[] line = astar.RouteGeometry(got);
                Assert.That(line[0], Is.EqualTo(astar.Graph.NodeX(src)));
                Assert.That(line[line.Length - 1], Is.EqualTo(astar.Graph.NodeZ(dst)));
            }
            Assert.That(found, Is.GreaterThan(50));
            Assert.That(missing, Is.GreaterThan(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => astar.FindRoute(0, astar.Graph.NodeCount, Travel.Foot));
        }

        [Test]
        public void NearestNodeMatchesPython()
        {
            RouteGraph g = Graph();
            foreach (JsonElement q in GoldenFiles.Json("golden_routes.json").GetProperty("nearest").EnumerateArray())
            {
                Travel profile = TravelProfiles.Get(q.GetProperty("profile").GetString()).Travel;
                bool incoming = q.GetProperty("incoming").GetBoolean();
                double x = q.GetProperty("x").GetDouble(), z = q.GetProperty("z").GetDouble();
                JsonElement node = q.GetProperty("node");
                int expected = node.ValueKind == JsonValueKind.Null ? -1 : node.GetInt32();
                foreach (double cell in new[] { 64.0, 512.0, 5000.0 })
                    Assert.That(new NearestNode(g, profile, incoming, cell).Find(x, z), Is.EqualTo(expected), q.ToString() + " cell " + cell);
            }
        }

        [Test]
        public void NearestNodeAgreesWithBruteForce()
        {
            RouteGraph g = Graph();
            int[] nodes = NearestNode.UsableNodes(g, Travel.Car, false);
            var index = new NearestNode(g, Travel.Car, false, 100.0);
            var rng = new Random(7);
            for (int k = 0; k < 500; k++)
            {
                double x = 199000 + rng.NextDouble() * 4000, z = 299000 + rng.NextDouble() * 4000;
                int best = nodes.OrderBy(v => Math.Pow(g.NodeX(v) - x, 2) + Math.Pow(g.NodeZ(v) - z, 2)).ThenBy(v => v).First();
                Assert.That(index.Find(x, z), Is.EqualTo(best));
            }
            Assert.That(index.Find(200000.0, 300000.0, 0.001), Is.GreaterThanOrEqualTo(0));
            Assert.That(index.Find(150000.0, 300000.0, 1000.0), Is.EqualTo(-1));
            Assert.That(new NearestNode(g, new int[0]).Find(0, 0), Is.EqualTo(-1));
        }

        [Test]
        public void RejectsCorruptGraph()
        {
            byte[] good = GoldenFiles.Bytes("golden.ghrg");
            byte[] b = (byte[])good.Clone();
            b[0] = (byte)'X';
            Assert.Throws<System.IO.InvalidDataException>(() => RouteGraphReader.Read(b));
            b = (byte[])good.Clone();
            b[4] = 3;
            Assert.Throws<System.IO.InvalidDataException>(() => RouteGraphReader.Read(b));
            Assert.Throws<System.IO.InvalidDataException>(() => RouteGraphReader.Read(good.Take(good.Length - 1).ToArray()));
            Assert.Throws<System.IO.InvalidDataException>(() => RouteGraphReader.Read(good.Concat(new byte[] { 1 }).ToArray()));
        }
    }
}
