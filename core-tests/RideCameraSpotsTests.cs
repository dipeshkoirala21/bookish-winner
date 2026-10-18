using System;
using System.Collections.Generic;
using System.Linq;
using Ghumante.Core.Characters;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;
using Ghumante.Core.Routing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The spots the camera package's rides ran into (docs/research/w2/ref_camera.md §6, §7.1), on the camera rides' own
    /// ground query (<see cref="CameraRides.Ground"/>: every sample tile, Mid tier, deck and corridor factories) and with the
    /// camera's own bus autopilot: the bus blocked on Kantipath at the Ratna Park junction, the bus blocked turning into
    /// Bhaktithapa Road, and the "unidentified" overhead solid north of Thamel. The explicit probes print what stands at a
    /// place (RIDE_PROBE=lon,lat[,half]; maps and road meshes to GHUMANTE_PREVIEW_DIR/ride for the ride previews).
    /// </summary>
    public class RideCameraSpotsTests
    {
        private static void Game(double lon, double lat, out double x, out double z)
        {
            WorldFrame.LonLatToGame(lon, lat, out x, out z);
        }

        private static string Where(double x, double z)
        {
            double lon, lat;
            WorldFrame.GameToLonLat(x, z, out lon, out lat);
            return lon.ToString("0.00000") + ", " + lat.ToString("0.00000");
        }

        private static double Dist(in SolidPrim p, double x, double z)
        {
            double qx, qz;
            SolidSet.Closest(p.Ax, p.Az, p.Bx, p.Bz, x, z, out qx, out qz);
            return Math.Sqrt((qx - x) * (qx - x) + (qz - z) * (qz - z)) - p.Radius;
        }

        /// <summary>Prints what stands within <paramref name="r"/> of a place: the ground, the roads, every solid and the
        /// buildings whose outline is near.</summary>
        private static void Dump(double lon, double lat, double r)
        {
            TileGroundQuery g = CameraRides.Ground;
            double x, z;
            Game(lon, lat, out x, out z);
            TileId id = TileId.At(10, x, z);
            TileData t = DrivingData.SampleTiles()[id];
            GroundSample s;
            g.TrySample(x, z, float.PositiveInfinity, out s);
            TestContext.WriteLine("at " + Where(x, z) + " tile " + id + " ground " + s.Height.ToString("0.00") + " terrain " + s.TerrainHeight.ToString("0.00") +
                                  " onRoad " + s.OnRoad + " class " + s.RoadClass + " foot " + s.OnFootpath + " deck " + s.OnDeck);
            RoadHit hit;
            if (g.TryNearestRoad(x, z, 10, out hit))
                TestContext.WriteLine("  road " + hit.Road.RoadClass + " osm " + hit.Road.OsmWayId + " edge " + hit.EdgeDistanceM.ToString("0.00") + " half " +
                                      hit.HalfWidthM.ToString("0.00") + " structure " + hit.Tile.RoadStructureOf(hit.RoadIndex).Kind);
            var prims = new List<SolidPrim>();
            g.SolidsOf(id, prims);
            foreach (SolidPrim p in prims)
            {
                double d = Dist(p, x, z);
                if (d > r) continue;
                float th;
                g.TryTerrainHeight(0.5 * (p.Ax + p.Bx), 0.5 * (p.Az + p.Bz), out th);
                TestContext.WriteLine("  prim group " + p.Group + " dist " + d.ToString("0.00") + " r " + p.Radius.ToString("0.00") + " y " + p.Bottom.ToString("0.00") +
                                      ".." + p.Top.ToString("0.00") + " (terrain " + th.ToString("0.00") + ") " + Where(p.Ax, p.Az) + " -> " + Where(p.Bx, p.Bz) +
                                      " flags " + p.Flags);
            }
            for (int i = 0; i < t.Buildings.Count; i++)
            {
                BuildingRecord b = t.Buildings[i];
                if (b.Rings == null || b.Rings.Length == 0 || b.Rings[0] == null) continue;
                int[] ring = b.Rings[0];
                double best = double.MaxValue;
                for (int k = 0; k + 1 < ring.Length; k += 2)
                {
                    double bx = t.Tile.X0 + ring[k] / 100.0, bz = t.Tile.Z0 + ring[k + 1] / 100.0;
                    best = Math.Min(best, Math.Sqrt((bx - x) * (bx - x) + (bz - z) * (bz - z)));
                }
                if (best > r + 10) continue;
                TestContext.WriteLine("  building " + i + " osm " + b.OsmRef + " " + b.Archetype + " use " + b.Use + " flags " + b.Flags + " h " + b.HeightCm + " minh " +
                                      b.MinHeightCm + " levels " + b.Levels + " nearest corner " + best.ToString("0.0") + " m");
            }
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord rr = t.Roads[i];
                double best = double.MaxValue;
                for (int k = 0; k + 3 < rr.Points.Length; k += 2)
                {
                    double ax, az, bx, bz;
                    t.LocalToGame(rr.Points[k], rr.Points[k + 1], out ax, out az);
                    t.LocalToGame(rr.Points[k + 2], rr.Points[k + 3], out bx, out bz);
                    double qx, qz;
                    SolidSet.Closest(ax, az, bx, bz, x, z, out qx, out qz);
                    best = Math.Min(best, Math.Sqrt((qx - x) * (qx - x) + (qz - z) * (qz - z)));
                }
                if (best > r) continue;
                RoadStructureRecord st = t.RoadStructureOf(i);
                TestContext.WriteLine("  way " + i + " osm " + rr.OsmWayId + " " + rr.RoadClass + " flags " + rr.Flags + " width " + rr.WidthCm + " kind " + st.Kind + " layer " +
                                      st.Layer + " clearance " + st.ClearanceM + " dist " + best.ToString("0.0") + " drawn " + RoadMesher.IsDrawn(t, i, null));
            }
            // What a camera sphere meets straight up from the road.
            for (float up = 0.5f; up <= 3.0f; up += 1.25f)
            {
                double hd;
                bool hitUp = g.SphereCast(x, s.Height + up, z, 0, 1, 0, 0.3, 8.0, out hd);
                TestContext.WriteLine("  sphere up from " + up.ToString("0.0") + " m: " + (hitUp ? "hit at " + (s.Height + up + hd).ToString("0.00") : "clear"));
            }
        }

        /// <summary>
        /// The camera package's blocked bus on Kantipath (ref_camera.md §6: "Ratna Park to Maitighar along Kantipath is
        /// blocked for a bus near Ratna Park"): at the Ratna Park junction under the four foot overbridges the data lowers
        /// every arm 4 m (RSTR Underpass, one height at the shared node), and the junction cap stood at the terrain, a
        /// 4.3 m wall across the arms (the bus stopped 94 m in, at 85.31613, 27.70623). The cap now follows its arms
        /// (<see cref="CapLevels"/>): the camera's own bus autopilot drives the route in the centre and 1.5 m left.
        /// </summary>
        [Test]
        public void TheBusRidesKantipathThroughTheRatnaParkJunction()
        {
            foreach (double left in new[] { 0.0, 1.5 })
            {
                double[] path = CameraRides.Route(Travel.Bus, 85.3153, 27.7066, 85.3207, 27.6946, left);
                RideStats stats;
                CameraRides.Drive(path, HandlingPreset.Bus, 8f, 150f, out stats);
                TestContext.WriteLine("bus Ratna Park-Maitighar, " + left + " m left: " + stats);
                Assert.That(stats.Stuck, Is.False, "the bus passes the Ratna Park junction, " + left + " m left: " + stats);
                Assert.That(stats.DrivenM, Is.GreaterThan(600.0), stats.ToString());
            }
        }

        /// <summary>
        /// The camera package's bus from Maitighar east along Tanka Prasad Ghumti Sadak, which turns into Bhaktithapa Road
        /// (7 m) at 85.32753, 27.69324 (ref_camera.md §6): the corner house there (OSM 1286599936) is solid exactly where it
        /// is drawn now (owner #14), and its corner stood where the pipeline trimmed it to the straight corridors, with no
        /// kerb return (the corner room clamp of the junction caps); the bus's front overhang swung 1.1 m into it and
        /// stopped. Bus corners (<see cref="RoadLayout.BusCornerRadiusM"/>: car roads both at least 6.5 m wide, at least
        /// 60° apart, outside the old cores) keep a 4.5 m kerb return, the corner house is trimmed back to it, and the
        /// camera's bus on the centre line drives on. Open (docs/research/w2/ref_ride.md §4): the camera package's bus 1.5 m
        /// left of the centre line still stops there. It keeps to the outside of the 46° bend Bhaktithapa Road takes at the
        /// node, its front swings out over the short 14 m arm of Ghumti Sadak (which bends 4.5 m from the node, so the cap
        /// has no room for a kerb return on that side), and it reaches 0.65 m into the corner house (with the house hidden)
        /// at that arm's right edge by its cut; a bus steered from its front axle (<see cref="ProbeFrontAxleBus"/>) stops
        /// there too.
        /// </summary>
        [Test]
        public void TheBusTurnsFromGhumtiSadakIntoBhaktithapaRoad()
        {
            double[] path = CameraRides.Route(Travel.Bus, 85.3240, 27.6950, 85.3340, 27.6930, 0.0);
            RideStats stats;
            CameraRides.Drive(path, HandlingPreset.Bus, 8f, 150f, out stats);
            TestContext.WriteLine("bus Maitighar-Baneshwor on the centre line: " + stats);
            Assert.That(stats.Stuck, Is.False, stats.ToString());
            Assert.That(stats.DrivenM, Is.GreaterThan(900.0), "past the Bhaktithapa Road corner (446 m): " + stats);
        }

        /// <summary>
        /// The "unidentified overhead solid" over the road north of Thamel (85.3130, 27.7178; ref_camera.md §7.1, the car's
        /// far view cut its boom there) is the foot overbridge at Lainchaur: OSM Footway 1268595053 (RSTR Flyover,
        /// FootOverbridge, layer 1) over Primary 176144636, drawn by the bridges package with railings. It is a real
        /// structure, not a stray solid: the lowest thing over the drawn road under it is that deck's slab in the road index
        /// (<see cref="RoadSpatialIndex.SlabOverlaps"/>, the deck surface down to <see cref="RoadSpatialIndex.DeckThicknessM"/>),
        /// 4.40 m over the carriageway at its lowest. Open (ref_ride.md §4): the slab takes the vehicle deck depth (1.2 m)
        /// where the data and the drawn foot deck are 0.6 m deep (<see cref="RoadStructureRecord.FootDeckDepthM"/>), which
        /// would leave 4.99 m; with that depth the camera package's chase boom snaps 1.75 m on its car ride under this bridge
        /// (CharactersCameraRideTests), so the change waits for the camera and bridges packages.
        /// </summary>
        [Test]
        public void TheOverheadSolidNorthOfThamelIsTheLainchaurFootOverbridge()
        {
            TileGroundQuery g = CameraRides.Ground;
            double x, z;
            Game(85.3130, 27.7178, out x, out z);
            GroundSample top;
            Assert.That(g.TrySample(x, z, float.PositiveInfinity, out top), Is.True);
            Assert.That(top.OnDeck, Is.True, "a deck over the road");
            TileId id = TileId.At(10, x, z);
            TileData t = DrivingData.SampleTiles()[id];
            int bridge = -1, road = -1;
            for (int i = 0; i < t.Roads.Count; i++)
            {
                if (t.Roads[i].OsmWayId == 1268595053UL) bridge = i;
                if (t.Roads[i].OsmWayId == 176144636UL) road = i;
            }
            Assert.That(bridge, Is.GreaterThanOrEqualTo(0), "the foot overbridge way is in the tile");
            Assert.That(road, Is.GreaterThanOrEqualTo(0), "the road under it is in the tile");
            RoadStructureRecord st = t.RoadStructureOf(bridge);
            Assert.That(st.Kind, Is.EqualTo(RoadStructureKind.Flyover));
            Assert.That(st.Has(RoadStructureFlags.FootOverbridge), Is.True);
            Assert.That(RoadMesher.IsDrawn(t, bridge, null), Is.True);
            // Along the road under the overbridge (±12 m of the crossing): the lowest thing over the drawn surface, and the
            // foot deck's surface over the same point.
            RoadLayout layout = RoadLayout.For(t);
            RoadCentreline c = layout.Centres[road];
            int seg;
            double along, lateral, dist;
            Assert.That(c.Nearest(x - t.Tile.X0, z - t.Tile.Z0, out seg, out along, out lateral, out dist), Is.True);
            double lowest = double.MaxValue, deckOver = 0;
            string where = "";
            for (double s = along - 12; s <= along + 12; s += 0.5)
            for (double off = -3; off <= 3; off += 1.5)
            {
                double cx, cz, tx, tz;
                c.At(s, out cx, out cz, out tx, out tz);
                double px = t.Tile.X0 + cx - tz * off, pz = t.Tile.Z0 + cz + tx * off;
                GroundSample s0, deck;
                if (!g.TrySample(px, pz, float.NegativeInfinity, out s0) || s0.OnDeck) continue;
                double hit;
                // A 0.3 m sphere cast straight up from 0.5 m over the road: whatever it meets first.
                if (g.SphereCast(px, s0.Height + 0.5, pz, 0, 1, 0, 0.3, 10.0, out hit) && hit + 0.8 < lowest &&
                    g.TrySample(px, pz, float.PositiveInfinity, out deck) && deck.OnDeck)
                {
                    lowest = hit + 0.8;
                    deckOver = deck.Height - s0.Height;
                    where = Where(px, pz);
                }
            }
            TestContext.WriteLine("lowest structure over Primary 176144636 under the overbridge: " + lowest.ToString("0.00") + " m at " + where +
                                  ", the foot deck's surface " + deckOver.ToString("0.00") + " m over the road there");
            Assert.That(lowest, Is.LessThan(8.0), "the overbridge is over this road");
            // Nothing but the deck's slab: the lowest obstacle is its underside in the road index (within the 0.3 m sphere's
            // march step), and the drawn deck (0.6 m deep) clears the contract's 4.5 m.
            Assert.That(lowest, Is.EqualTo(deckOver - RoadSpatialIndex.DeckThicknessM).Within(0.15), "the deck slab, nothing lower, at " + where);
            Assert.That(deckOver - RoadStructureRecord.FootDeckDepthM, Is.GreaterThanOrEqualTo(RoadClearance.MinOverheadClearanceM), "the drawn foot deck's underside");
        }

        [Test, Explicit("probe")]
        public void ProbeSpots()
        {
            Dump(85.3130, 27.7178, 8);
            Dump(85.32753, 27.69324, 6);
        }

        private static void DumpAt(double x, double z, double r)
        {
            double lon, lat;
            WorldFrame.GameToLonLat(x, z, out lon, out lat);
            Dump(lon, lat, r);
        }

        /// <summary>The drawn road levels on a 1 m grid around a place, relative to the terrain, and the roads mesh of the
        /// area as OBJ (GHUMANTE_PREVIEW_DIR).</summary>
        [Test, Explicit("probe")]
        public void ProbeDrawnLevels()
        {
            string at = Environment.GetEnvironmentVariable("RIDE_PROBE") ?? "85.32752,27.69323";
            string[] p = at.Split(',');
            double lon = double.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture), lat = double.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture);
            double x, z;
            Game(lon, lat, out x, out z);
            TileId id = TileId.At(10, x, z);
            TileData t = DrivingData.SampleTiles()[id];
            var h = new TileHeightSampler(t, 1);
            var m = new MeshData();
            RoadMesher.Build(t, h, new RoadOptions(), m);
            DrawnRoadSurface d = DrawnRoadSurface.FromMesh(m, t.Tile.X0, t.Tile.Z0);
            RoadSurfaceQuery q = RoadSurfaceQuery.For(t, h, new RoadOptions());
            var lv = new float[8];
            for (double dz = 12; dz >= -12; dz -= 1)
            {
                var line = new System.Text.StringBuilder(dz.ToString("+00;-00") + ":");
                for (double dx = -16; dx <= 16; dx += 1)
                {
                    double qx = x + dx, qz = z + dz;
                    float th;
                    h.TryHeight(qx, qz, out th);
                    int n = d.Levels(qx, qz, lv);
                    if (n == 0)
                    {
                        line.Append("    .");
                        continue;
                    }
                    line.Append(" " + (lv[n - 1] - th).ToString("+0.0;-0.0").PadLeft(4));
                    if (n > 1) line.Append("*");
                }
                TestContext.WriteLine(line.ToString());
            }
            RoadSurfaceHit rh;
            for (double dx = -16; dx <= 16; dx += 4)
            {
                if (q.TrySample(x + dx, z, float.PositiveInfinity, out rh))
                    TestContext.WriteLine("  dx " + dx + ": surface " + rh.Y.ToString("0.00") + " road " + (rh.Road >= 0 ? t.Roads[rh.Road].OsmWayId.ToString() : "-") + " junc " +
                                          rh.OnJunction + " carr " + rh.OnCarriageway + " lowered " + rh.Lowered);
            }
            var local = new MeshData();
            ObjDump.Append(local, m, (float)-(x - t.Tile.X0), 0f, (float)-(z - t.Tile.Z0));
            float y0;
            h.TryHeight(x, z, out y0);
            var crop = ObjDump.Crop(local, -40, -40, 40, 40, true);
            var flat = new MeshData();
            ObjDump.Append(flat, crop, 0f, -y0, 0f);
            ObjDump.Write(flat, "ride/probe_roads.obj");
        }

        /// <summary>The camera's own bus ride (CameraRides.Drive), and the ground and solids ahead of the bus where it stops.</summary>
        [Test, Explicit("probe")]
        public void ProbeCameraBusStop()
        {
            TileGroundQuery g = CameraRides.Ground;
            CollisionBody body = CollisionBody.For(VehicleSpec.For(HandlingPreset.Bus));
            foreach (double left in new[] { 0.0, 1.5 })
            {
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                string[] rt = (Environment.GetEnvironmentVariable("RIDE_ROUTE") ?? "85.3240,27.6950,85.3340,27.6930").Split(',');
                double[] path = CameraRides.Route(Travel.Bus, double.Parse(rt[0], inv), double.Parse(rt[1], inv), double.Parse(rt[2], inv), double.Parse(rt[3], inv), left);
                RideStats stats;
                List<RideFrame> frames = CameraRides.Drive(path, HandlingPreset.Bus, 8f, 150f, out stats);
                TestContext.WriteLine("left " + left + ": " + stats);
                if (!stats.Stuck) continue;
                RideFrame f = frames[frames.Count - 1];
                TestContext.WriteLine("  last frame " + Where(f.X, f.Z) + " y " + f.Y.ToString("0.00") + " heading " + (f.HeadingRad * 57.3f).ToString("0"));
                double fx = Math.Sin(f.HeadingRad), fz = Math.Cos(f.HeadingRad);
                for (double a = -6; a <= 10; a += 1)
                {
                    var line = new System.Text.StringBuilder("  ahead " + a.ToString("+0;-0").PadLeft(3) + ":");
                    for (double lat = -3; lat <= 3; lat += 0.5)
                    {
                        double qx = f.X + fx * a + fz * lat, qz = f.Z + fz * a - fx * lat;
                        GroundSample s;
                        bool ok = g.TrySample(qx, qz, f.Y, out s);
                        line.Append(ok ? " " + (s.Height - f.Y).ToString("+0.00;-0.00") + (s.OnDeck ? "D" : s.OnFootpath ? "F" : s.OnRoad ? " " : "o") : "  XXXX ");
                    }
                    TestContext.WriteLine(line.ToString());
                }
                foreach (double dir in new[] { 0.0, -0.3, 0.3 })
                {
                    double hx = Math.Sin(f.HeadingRad + dir), hz = Math.Cos(f.HeadingRad + dir);
                    float t, nx, nz;
                    bool hit = g.SweepBody(in body, f.X, f.Z, f.HeadingRad, f.Y, hx * 2, hz * 2, out t, out nx, out nz);
                    TestContext.WriteLine("  sweep 2 m at " + dir + ": " + hit + " t " + t.ToString("0.00") + " normal " + nx.ToString("0.00") + "," + nz.ToString("0.00"));
                }
                // Every solid of the nine tiles around within 12 m that spans the bus body (feet to roof).
                TileId c0 = TileId.At(10, f.X, f.Z);
                var prims = new List<SolidPrim>();
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    prims.Clear();
                    var tid = new TileId(10, c0.Tx + dx, c0.Ty + dy);
                    g.SolidsOf(tid, prims);
                    foreach (SolidPrim sp in prims)
                    {
                        double dd = Dist(sp, f.X + fx * 3, f.Z + fz * 3);
                        if (dd > 9 || sp.Top < f.Y + 0.45f || sp.Bottom > f.Y + body.HeightM) continue;
                        TestContext.WriteLine("  spanning prim tile " + tid + " group " + sp.Group + " dist " + dd.ToString("0.00") + " r " + sp.Radius.ToString("0.00") + " y " +
                                              (sp.Bottom - f.Y).ToString("+0.00;-0.00") + ".." + (sp.Top - f.Y).ToString("+0.00;-0.00") + " " + Where(sp.Ax, sp.Az) + " -> " + Where(sp.Bx, sp.Bz));
                    }
                }
                TestContext.WriteLine("  body circles " + body.Circles + " first " + body.FirstM + " last " + body.LastM + " radius " + body.Radius + " height " + body.HeightM);
                DumpAt(f.X + fx * 4, f.Z + fz * 4, 10);
            }
        }

        /// <summary>
        /// A top-down map (text, rendered by the ride preview tool) of a place on the camera rides' ground: drawn road
        /// surface, drawn houses, solids at body height over the ground, and the camera bus route (centre and 1.5 m left).
        /// Codes: 0 open, 1 drawn road, 3 inside a drawn house, 4 solid on open ground, 5 solid on a drawn road.
        /// </summary>
        [Test, Explicit("probe")]
        public void ProbeMap()
        {
            string at = Environment.GetEnvironmentVariable("RIDE_PROBE") ?? "85.32752,27.69323";
            string[] p = at.Split(',');
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            double lon = double.Parse(p[0], inv), lat = double.Parse(p[1], inv);
            double half = p.Length > 2 ? double.Parse(p[2], inv) : 30, step = 0.25;
            double cx, cz;
            Game(lon, lat, out cx, out cz);
            TileGroundQuery g = CameraRides.Ground;
            TileId id = TileId.At(10, cx, cz);
            TileData t = DrivingData.SampleTiles()[id];
            var mesh = new MeshData();
            RoadMesher.Build(t, new TileHeightSampler(t, 1), new RoadOptions(), mesh);
            DrawnRoadSurface d = DrawnRoadSurface.FromMesh(mesh, t.Tile.X0, t.Tile.Z0);
            BuildingFootprints fp = BuildingFootprints.For(t, RoadCorridorIndex.ForTile(t));
            int w = (int)(2 * half / step);
            var sb = new System.Text.StringBuilder();
            sb.Append(w + " " + w + " " + step + "\n");
            var lv = new float[8];
            for (int j = 0; j < w; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    double x = cx - half + (i + 0.5) * step, z = cz + half - (j + 0.5) * step;
                    int n = d != null && d.InBounds(x, z) ? d.Levels(x, z, lv) : 0;
                    GroundSample s;
                    float feet = n > 0 ? lv[n - 1] : g.TrySample(x, z, float.PositiveInfinity, out s) ? s.Height : 0f;
                    bool solid = g.InsideSolid(x, z, feet);
                    char c = n > 0 ? (solid ? '5' : '1') : solid ? '4' : '0';
                    sb.Append(c);
                }
                sb.Append('\n');
            }
            for (int i = 0; i < t.Buildings.Count; i++)
            {
                if (fp.Dropped(i)) continue;
                BuildingRecord b = fp.Record(i);
                if (b == null || b.Rings == null || b.Rings.Length == 0 || b.Rings[0] == null) continue;
                var line = new System.Text.StringBuilder("H");
                bool near = false;
                for (int k = 0; k + 1 < b.Rings[0].Length; k += 2)
                {
                    double x = t.Tile.X0 + b.Rings[0][k] / 100.0, z = t.Tile.Z0 + b.Rings[0][k + 1] / 100.0;
                    double px = (x - (cx - half)) / step, pz = ((cz + half) - z) / step;
                    if (px > -40 && px < w + 40 && pz > -40 && pz < w + 40) near = true;
                    line.Append(" " + px.ToString("0.0", inv) + "," + pz.ToString("0.0", inv));
                }
                if (near) sb.Append(line).Append('\n');
            }
            foreach (JunctionCap cap in RoadLayout.For(t).Caps)
            {
                var line = new System.Text.StringBuilder("C");
                bool near = false;
                for (int k = 0; k < cap.Count; k++)
                {
                    double px = (t.Tile.X0 + cap.PolyX[k] - (cx - half)) / step, pz = ((cz + half) - (t.Tile.Z0 + cap.PolyZ[k])) / step;
                    if (px > 0 && px < w && pz > 0 && pz < w) near = true;
                    line.Append(" " + px.ToString("0.0", inv) + "," + pz.ToString("0.0", inv));
                }
                if (near) sb.Append(line).Append('\n');
            }
            foreach (double left in new[] { 0.0, 1.5 })
            {
                string[] rt = (Environment.GetEnvironmentVariable("RIDE_ROUTE") ?? "85.3240,27.6950,85.3340,27.6930").Split(',');
                double[] path = CameraRides.Route(Travel.Bus, double.Parse(rt[0], inv), double.Parse(rt[1], inv), double.Parse(rt[2], inv), double.Parse(rt[3], inv), left);
                var line = new System.Text.StringBuilder("P");
                for (int k = 0; k + 1 < path.Length; k += 2)
                    line.Append(" " + ((path[k] - (cx - half)) / step).ToString("0.0", inv) + "," + (((cz + half) - path[k + 1]) / step).ToString("0.0", inv));
                sb.Append(line).Append('\n');
                // The bus body (its collision circles' outline) every 0.5 s while it is on the map.
                RideStats stats;
                List<RideFrame> frames = CameraRides.Drive(path, HandlingPreset.Bus, 8f, 150f, out stats);
                CollisionBody body = CollisionBody.For(VehicleSpec.For(HandlingPreset.Bus));
                float since = 0.5f;
                for (int f = 0; f < frames.Count; f++)
                {
                    RideFrame fr = frames[f];
                    since += fr.Dt;
                    bool last = f == frames.Count - 1;
                    if (Math.Abs(fr.X - cx) > half || Math.Abs(fr.Z - cz) > half || (since < 0.5f && !last)) continue;
                    since = 0f;
                    double hx = Math.Sin(fr.HeadingRad), hz = Math.Cos(fr.HeadingRad), rx = hz, rz = -hx;
                    double a0 = body.FirstM - body.Radius, a1 = body.LastM + body.Radius, br = body.Radius;
                    var bl = new System.Text.StringBuilder(last && stats.Stuck ? "X" : "B");
                    foreach (double[] c in new[] { new[] { a0, -br }, new[] { a1, -br }, new[] { a1, br }, new[] { a0, br } })
                    {
                        double bx = fr.X + hx * c[0] + rx * c[1], bz = fr.Z + hz * c[0] + rz * c[1];
                        bl.Append(" " + ((bx - (cx - half)) / step).ToString("0.0", inv) + "," + (((cz + half) - bz) / step).ToString("0.0", inv));
                    }
                    sb.Append(bl).Append('\n');
                }
            }
            string dir = Environment.GetEnvironmentVariable("GHUMANTE_PREVIEW_DIR");
            if (dir != null)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dir, "ride"));
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "ride", "map_" + (Environment.GetEnvironmentVariable("RIDE_MAP_NAME") ?? "probe") + ".txt"), sb.ToString());
            }
        }

        /// <summary>The roads within 40 m of a place: RSTR kind, flags and deck heights against the terrain at every point.</summary>
        [Test, Explicit("probe")]
        public void ProbeStructures()
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string[] p = (Environment.GetEnvironmentVariable("RIDE_PROBE") ?? "85.31613,27.70623").Split(',');
            double x, z;
            Game(double.Parse(p[0], inv), double.Parse(p[1], inv), out x, out z);
            TileId id = TileId.At(10, x, z);
            TileData t = DrivingData.SampleTiles()[id];
            var h = new TileHeightSampler(t, 1);
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                double best = double.MaxValue;
                for (int k = 0; k + 1 < r.Points.Length; k += 2)
                {
                    double ax, az;
                    t.LocalToGame(r.Points[k], r.Points[k + 1], out ax, out az);
                    best = Math.Min(best, Math.Sqrt((ax - x) * (ax - x) + (az - z) * (az - z)));
                }
                if (best > 40) continue;
                RoadStructureRecord st = t.RoadStructureOf(i);
                var sb = new System.Text.StringBuilder("way " + i + " osm " + r.OsmWayId + " " + r.RoadClass + " " + st.Kind + " flags " + st.Flags + " layer " + st.Layer +
                                                       " clear " + st.ClearanceM + " drawn " + RoadMesher.IsDrawn(t, i, null) + ":");
                for (int k = 0; k < r.PointCount; k++)
                {
                    double ax, az;
                    t.LocalToGame(r.Points[2 * k], r.Points[2 * k + 1], out ax, out az);
                    float th;
                    h.TryHeightClamped(ax, az, out th);
                    float y;
                    string dy = st.TryHeightAt(k, out y) ? (y - th).ToString("+0.0;-0.0", inv) + st.RoleAt(k).ToString()[0] : "-";
                    sb.Append(" [" + k + " " + Where(ax, az) + " " + dy + "]");
                }
                TestContext.WriteLine(sb.ToString());
            }
        }

        private static double Sq(double v)
        {
            return v * v;
        }

        private static void PathAt(double[] p, double[] cum, double s, out double x, out double z)
        {
            int n = cum.Length;
            for (int i = 1; i < n; i++)
            {
                if (cum[i] < s) continue;
                double seg = cum[i] - cum[i - 1];
                double t = seg > 1e-9 ? Math.Max(0, (s - cum[i - 1]) / seg) : 0.0;
                x = p[2 * i - 2] + (p[2 * i] - p[2 * i - 2]) * t;
                z = p[2 * i - 1] + (p[2 * i + 1] - p[2 * i - 1]) * t;
                return;
            }
            x = p[2 * n - 2];
            z = p[2 * n - 1];
        }

        private static double PathProgress(double[] p, double[] cum, double x, double z, double s)
        {
            double best = double.MaxValue, bestS = s;
            for (int i = 1; i < cum.Length; i++)
            {
                if (cum[i] < s - 5.0) continue;
                if (cum[i - 1] > s + 30.0) break;
                double ax = p[2 * i - 2], az = p[2 * i - 1], ex = p[2 * i] - ax, ez = p[2 * i + 1] - az;
                double len2 = ex * ex + ez * ez;
                double t = len2 > 1e-12 ? Math.Max(0, Math.Min(1, ((x - ax) * ex + (z - az) * ez) / len2)) : 0.0;
                double d = Sq(ax + ex * t - x) + Sq(az + ez * t - z);
                if (d < best)
                {
                    best = d;
                    bestS = cum[i - 1] + Math.Sqrt(len2) * t;
                }
            }
            return Math.Max(s, bestS);
        }

        private static float PathBend(double[] p, double[] cum, double s, double ahead)
        {
            float worst = 0f;
            for (double a = 0; a < ahead; a += 4.0)
            {
                double x0, z0, x1, z1, x2, z2;
                PathAt(p, cum, s + a, out x0, out z0);
                PathAt(p, cum, s + a + 4.0, out x1, out z1);
                PathAt(p, cum, s + a + 8.0, out x2, out z2);
                float turn = Math.Abs(CharMath.WrapRad((float)(Math.Atan2(x2 - x1, z2 - z1) - Math.Atan2(x1 - x0, z1 - z0)))) / 4f;
                worst = Math.Max(worst, turn);
            }
            return worst;
        }

        /// <summary>Experiment: how deep the camera's bus (driven without the corner house's solid) reaches into the drawn
        /// outline of that house at the Bhaktithapa Road junction, and where the cap and the node are.</summary>
        [Test, Explicit("probe")]
        public void ProbeBusIntoCornerHouse()
        {
            TileGroundQuery g = StreamingScene.Ground(StreamingScene.Settings(1, 10));
            var hidden = new HashSet<ulong>(g.HiddenBuildingRefs ?? new HashSet<ulong>()) { 1286599936UL };
            g.HiddenBuildingRefs = hidden;
            foreach (TileData td in DrivingData.SampleTiles().Values) g.Add(td);
            double hx, hz;
            Game(85.32760, 27.69322, out hx, out hz);
            TileData t = DrivingData.SampleTiles()[TileId.At(10, hx, hz)];
            BuildingFootprints fp = BuildingFootprints.For(t, RoadCorridorIndex.ForTile(t));
            int bi = -1;
            for (int i = 0; i < t.Buildings.Count; i++)
                if (t.Buildings[i].OsmRef == 1286599936UL) bi = i;
            BuildingRecord dr = fp.Record(bi);
            int[] ring = dr.Rings[0];
            int rn = ring.Length / 2;
            var rx = new double[rn];
            var rz = new double[rn];
            for (int k = 0; k < rn; k++)
            {
                rx[k] = t.Tile.X0 + ring[2 * k] / 100.0;
                rz[k] = t.Tile.Z0 + ring[2 * k + 1] / 100.0;
                TestContext.WriteLine("  house corner " + k + ": " + Where(rx[k], rz[k]) + " local " + (rx[k] - t.Tile.X0).ToString("0.00") + "," + (rz[k] - t.Tile.Z0).ToString("0.00"));
            }
            RoadLayout layout = RoadLayout.For(t);
            foreach (JunctionCap cap in layout.Caps)
            {
                double d = Math.Sqrt(Sq(cap.X + t.Tile.X0 - hx) + Sq(cap.Z + t.Tile.Z0 - hz));
                if (d > 30) continue;
                var sb = new System.Text.StringBuilder("  cap node " + (cap.X).ToString("0.00") + "," + cap.Z.ToString("0.00") + " arms");
                for (int k = 0; k < cap.ArmRoads.Length; k++)
                    sb.Append(" [" + t.Roads[cap.ArmRoads[k]].OsmWayId + " cutS " + cap.ArmCutS[k].ToString("0.0") + " setback " + cap.ArmSetback[k].ToString("0.0") + " r " +
                              cap.CornerRadiusM[k].ToString("0.0") + "]");
                TestContext.WriteLine(sb.ToString());
                var pb = new System.Text.StringBuilder("  cap poly");
                for (int k = 0; k < cap.Count; k++) pb.Append(" " + cap.PolyX[k].ToString("0.0") + "," + cap.PolyZ[k].ToString("0.0"));
                TestContext.WriteLine(pb.ToString());
            }
            CollisionBody body = CollisionBody.For(VehicleSpec.For(HandlingPreset.Bus));
            foreach (double left in new[] { 0.0, 1.5 })
            {
                double[] path = CameraRides.Route(Travel.Bus, 85.3240, 27.6950, 85.3340, 27.6930, left);
                // Re-drive, recording the deepest reach of the body circles into the house outline.
                int n = path.Length / 2;
                var cum = new double[n];
                for (int i = 1; i < n; i++) cum[i] = cum[i - 1] + Math.Sqrt(Sq(path[2 * i] - path[2 * i - 2]) + Sq(path[2 * i + 1] - path[2 * i - 1]));
                var v = new ArcadeVehicle(VehicleSpec.For(HandlingPreset.Bus));
                double sx, sz, h2x, h2z;
                PathAt(path, cum, 0, out sx, out sz);
                PathAt(path, cum, 3.0, out h2x, out h2z);
                v.Teleport(sx, sz, (float)Math.Atan2(h2x - sx, h2z - sz), g);
                double s = 0, deepest = -99;
                string at = "";
                for (float time = 0f; time < 150f; time += 1f / 60f)
                {
                    s = PathProgress(path, cum, v.X, v.Z, s);
                    float look = Math.Max(4f, Math.Min(14f, 2.5f + 0.7f * Math.Abs(v.SpeedMps)));
                    double tx, tz;
                    PathAt(path, cum, s + look, out tx, out tz);
                    float err = CharMath.WrapRad((float)Math.Atan2(tx - v.X, tz - v.Z) - v.HeadingRad);
                    float bend = PathBend(path, cum, s, 25.0);
                    float target = Math.Min(8f, Math.Max(3f, (float)Math.Sqrt(3.0 / Math.Max(1e-3, bend))));
                    v.Step(new DriveInput(CharMath.Clamp((target - v.SpeedMps) * 0.6f + 0.15f, -1f, 1f), v.SpeedMps > target + 1.5f ? 0.5f : 0f,
                                          CharMath.Clamp(err * 2.2f, -1f, 1f)), 1f / 60f, g, 0f);
                    for (int k = 0; k < body.Circles; k++)
                    {
                        double cx, cz;
                        body.Centre(k, v.X, v.Z, v.HeadingRad, out cx, out cz);
                        double dmin = double.MaxValue;
                        bool inside = false;
                        for (int i = 0, j = rn - 1; i < rn; j = i++)
                        {
                            if ((rz[i] > cz) != (rz[j] > cz) && cx < (rx[j] - rx[i]) * (cz - rz[i]) / (rz[j] - rz[i]) + rx[i]) inside = !inside;
                            double qx, qz;
                            SolidSet.Closest(rx[j], rz[j], rx[i], rz[i], cx, cz, out qx, out qz);
                            dmin = Math.Min(dmin, Math.Sqrt(Sq(qx - cx) + Sq(qz - cz)));
                        }
                        double reach = inside ? dmin + body.Radius : body.Radius - dmin;
                        if (reach > deepest)
                        {
                            deepest = reach;
                            at = "circle " + k + " at " + Where(cx, cz) + " bus at s " + s.ToString("0") + " heading " + (v.HeadingRad * 57.3).ToString("0");
                        }
                    }
                    if (s >= cum[n - 1] - 8.0 || s > 520) break;
                }
                TestContext.WriteLine("left " + left + ": deepest reach into the house " + deepest.ToString("0.00") + " m, " + at);
            }
        }

        /// <summary>
        /// A bus driven as a driver steers it: the pursuit point is chased from the front axle (the camera rides' autopilot
        /// chases it from the vehicle origin under the rear axle, which swings the front of a 10.5 m bus 1.2 m wide of its
        /// path in a turn). Returns the metres driven and whether it got stuck; <paramref name="worstClear"/> is the least
        /// clearance of the body circles to the solids it passed (body sweep probe).
        /// </summary>
        internal static double DriveFrontAxle(double[] path, HandlingPreset preset, float cruise, float maxSeconds, out bool stuck, out double stuckX, out double stuckZ)
        {
            TileGroundQuery g = CameraRides.Ground;
            VehicleSpec spec = VehicleSpec.For(preset);
            int n = path.Length / 2;
            var cum = new double[n];
            for (int i = 1; i < n; i++) cum[i] = cum[i - 1] + Math.Sqrt(Sq(path[2 * i] - path[2 * i - 2]) + Sq(path[2 * i + 1] - path[2 * i - 1]));
            var v = new ArcadeVehicle(spec);
            double sx, sz, hx, hz;
            PathAt(path, cum, 0, out sx, out sz);
            PathAt(path, cum, 3.0, out hx, out hz);
            v.Teleport(sx, sz, (float)Math.Atan2(hx - sx, hz - sz), g);
            double s = 0;
            float slow = 0f;
            stuck = false;
            stuckX = stuckZ = 0;
            const float dt = 1f / 60f;
            for (float time = 0f; time < maxSeconds; time += dt)
            {
                double fx = v.X + Math.Sin(v.HeadingRad) * spec.WheelbaseM, fz = v.Z + Math.Cos(v.HeadingRad) * spec.WheelbaseM;
                s = PathProgress(path, cum, fx, fz, s);
                float look = Math.Max(3f, Math.Min(10f, 1.5f + 0.6f * Math.Abs(v.SpeedMps)));
                double tx, tz;
                PathAt(path, cum, s + look, out tx, out tz);
                float err = CharMath.WrapRad((float)Math.Atan2(tx - fx, tz - fz) - v.HeadingRad);
                float bend = PathBend(path, cum, s, 25.0);
                float target = Math.Min(cruise, Math.Max(3f, (float)Math.Sqrt(3.0 / Math.Max(1e-3, bend))));
                v.Step(new DriveInput(CharMath.Clamp((target - v.SpeedMps) * 0.6f + 0.15f, -1f, 1f), v.SpeedMps > target + 1.5f ? 0.5f : 0f,
                                      CharMath.Clamp(err * 2.2f, -1f, 1f)), dt, g, 0f);
                slow = Math.Abs(v.SpeedMps) < 0.5f && time > 2f ? slow + dt : 0f;
                if (slow > 3f)
                {
                    stuck = true;
                    stuckX = v.X;
                    stuckZ = v.Z;
                    break;
                }
                if (s >= cum[n - 1] - 8.0) break;
            }
            return s;
        }

        /// <summary>The junction caps within 30 m of RIDE_PROBE: per arm its road, direction, setback, cut section and the
        /// corner after it (radius, bus corner).</summary>
        [Test, Explicit("probe")]
        public void ProbeCaps()
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string[] p = (Environment.GetEnvironmentVariable("RIDE_PROBE") ?? "85.32752,27.69323").Split(',');
            double x, z;
            Game(double.Parse(p[0], inv), double.Parse(p[1], inv), out x, out z);
            TileData t = DrivingData.SampleTiles()[TileId.At(10, x, z)];
            RoadLayout layout = RoadLayout.For(t);
            foreach (JunctionCap cap in layout.Caps)
            {
                if (Math.Sqrt(Sq(cap.X + t.Tile.X0 - x) + Sq(cap.Z + t.Tile.Z0 - z)) > 30) continue;
                var poly = new System.Text.StringBuilder();
                for (int q = 0; q < cap.Count; q++) poly.Append(" " + cap.PolyX[q].ToString("0.0") + "," + cap.PolyZ[q].ToString("0.0"));
                TestContext.WriteLine("cap node " + cap.X.ToString("0.00") + "," + cap.Z.ToString("0.00") + " top " + t.Roads[cap.TopRoad].OsmWayId + " poly" + poly);
                for (int k = 0; k < cap.ArmRoads.Length; k++)
                {
                    RoadCut c = layout.StoredCut(t, cap.ArmRoads[k], cap.ArmCutS[k]);
                    double dx = c.CX - cap.X, dz = c.CZ - cap.Z;
                    TestContext.WriteLine("  arm " + k + " road " + t.Roads[cap.ArmRoads[k]].OsmWayId + " " + t.Roads[cap.ArmRoads[k]].RoadClass + " fwd " + cap.ArmForward[k] +
                                          " dir " + (Math.Atan2(dx, dz) * 180 / Math.PI).ToString("0") + "° setback " + cap.ArmSetback[k].ToString("0.0") + " cut s " + c.S.ToString("0.0") +
                                          " centre " + c.CX.ToString("0.0") + "," + c.CZ.ToString("0.0") + " u " + c.UX.ToString("0.00") + "," + c.UZ.ToString("0.00") + " half " +
                                          c.Half.ToString("0.00") + " shift " + c.Shift.ToString("0.00") + "; corner after: r " + cap.CornerRadiusM[k].ToString("0.0") + " bus " + cap.BusCorner[k]);
                }
            }
        }

        [Test, Explicit("probe")]
        public void ProbeFrontAxleBus()
        {
            foreach (double left in new[] { 0.0, 1.5, 1.75 })
            {
                double[] path = CameraRides.Route(Travel.Bus, 85.3240, 27.6950, 85.3340, 27.6930, left);
                bool stuck;
                double x, z;
                double m = DriveFrontAxle(path, HandlingPreset.Bus, 8f, 150f, out stuck, out x, out z);
                TestContext.WriteLine("front-axle bus Maitighar-Baneshwor " + left + " m left: drove " + m.ToString("0") + " m" + (stuck ? " STUCK at " + Where(x, z) : ""));
                path = CameraRides.Route(Travel.Bus, 85.3153, 27.7066, 85.3207, 27.6946, left);
                m = DriveFrontAxle(path, HandlingPreset.Bus, 8f, 150f, out stuck, out x, out z);
                TestContext.WriteLine("front-axle bus Ratna Park-Maitighar " + left + " m left: drove " + m.ToString("0") + " m" + (stuck ? " STUCK at " + Where(x, z) : ""));
            }
        }

        [Test, Explicit("probe")]
        public void ProbeBusRides()
        {
            foreach (double left in new[] { 0.0, 1.5 })
            {
                double[] path = CameraRides.Route(Travel.Bus, 85.3240, 27.6950, 85.3340, 27.6930, left);
                RideStats stats;
                CameraRides.Drive(path, HandlingPreset.Bus, 8f, 150f, out stats);
                TestContext.WriteLine("bus Maitighar-Baneshwor left " + left + ": " + stats);
                path = CameraRides.Route(Travel.Bus, 85.3153, 27.7066, 85.3207, 27.6946, left);
                CameraRides.Drive(path, HandlingPreset.Bus, 8f, 150f, out stats);
                TestContext.WriteLine("bus Ratna Park-Maitighar left " + left + ": " + stats);
            }
        }
    }
}
