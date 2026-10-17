using System;
using System.Collections.Generic;
using System.IO;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;
using Ghumante.Core.Search;
using Ghumante.Core.Streaming;
using Ghumante.World.Instancing;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The community fleet's heavy vehicles on the committed <c>kathmandu_core</c> sample (game/README.md: "Borrow a
    /// community-fleet bus"): green city buses, micros, minibuses and tempos wait at the bus parks (Ratna Park, the
    /// Old Bus Park, Gongabu...), lorries and pickups at the depots, vans and lorries on wide streets and tractors on the
    /// village lanes, most of them with the green key tag, all drivable, so the bus, lorry and tractor camera angles can be
    /// reached by borrowing one. Placed exactly as the streamer does (<see cref="ParkedPlacement.Place"/> with the tier's
    /// road options).
    /// </summary>
    public class ExplorePlayFleetTests
    {
        private static bool Heavy(int variant)
        {
            VehicleCatalogEntry e = VehicleCatalog.At(variant);
            return e.Shape == BodyShape.Bus || e.Shape == BodyShape.Van || e.Shape == BodyShape.Tempo || e.Shape == BodyShape.Truck ||
                   e.Shape == BodyShape.Tipper || e.Shape == BodyShape.Tanker || e.Shape == BodyShape.Tractor || e.Shape == BodyShape.Pickup;
        }

        /// <summary>The parked vehicles of a level-10 tile as the streamer places them (tier 1).</summary>
        private static List<ParkedVehicle> Parked(TileId id, out TileData t)
        {
            MeshingSettings s = StreamingScene.Settings(1, 10, false);
            t = StreamingSampleRegion.Tile(id);
            var parked = new List<ParkedVehicle>();
            if (t == null) return parked;
            TileHeightSampler h = TileHeightSampler.ForArea(t, id, StreamingConfig.ForTier(1).TerrainStep(id, id, t.HeightsN));
            ParkedPlacement.Place(t, h, parked, s.Roads);
            return parked;
        }

        private static SearchEntry Find(string query)
        {
            string path = Path.Combine(GoldenFiles.RepoRoot, "shared", "sample-regions", "kathmandu_core", "kathmandu_core.search.ghsi");
            if (!File.Exists(path)) Assert.Ignore("sample search index not present: " + path);
            var engine = new SearchEngine(SearchIndexReader.Read(File.ReadAllBytes(path)));
            List<SearchResult> hits = engine.Search(query, 3);
            Assert.That(hits.Count, Is.GreaterThan(0), query);
            return hits[0].Entry;
        }

        [TestCase("Ratna Park bus station")]
        [TestCase("Naya Bus Park, Gongabu")]
        [TestCase("Chabahil bus station")]
        public void ABorrowableBusWaitsAtTheBusPark(string query)
        {
            SearchEntry hit = Find(query);
            double x = hit.X, z = hit.Z;
            int fleetBuses = 0, others = 0;
            string seen = "";
            var done = new HashSet<ulong>();
            // The tile holding the bus park and its neighbours (a bus park near a tile edge parks on both).
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                TileId c = TileId.At(10, x, z), id = new TileId(10, c.Tx + dx, c.Ty + dz);
                if (!done.Add(id.Key)) continue;
                TileData t;
                foreach (ParkedVehicle p in Parked(id, out t))
                {
                    if (!Heavy(p.Variant)) continue;
                    double px = id.X0 + p.X, pz = id.Z0 + p.Z, d = Math.Sqrt((px - x) * (px - x) + (pz - z) * (pz - z));
                    if (d > ParkedPlacement.BusParkReachM + 15.0) continue;
                    seen += " " + VehicleCatalog.At(p.Variant).AssetId + (p.CommunityFleet ? "*" : "") + "@" + d.ToString("0") + "m";
                    if (p.Variant == VehicleCatalog.BusCityGreen && p.CommunityFleet) fleetBuses++;
                    else others++;
                }
            }
            TestContext.WriteLine(query + ":" + seen);
            Assert.That(fleetBuses, Is.GreaterThanOrEqualTo(1), "a green city bus with the key tag by " + query);
            Assert.That(fleetBuses + others, Is.GreaterThanOrEqualTo(2), "and more route vehicles beside it");
        }

        [Test]
        public void ABorrowableTractorStandsByTheFieldsOfNagdesh()
        {
            // README: teleport to Nagdesh (Thimi's fields, east of the airport) for a tractor with the key tag.
            SearchEntry hit = Find("Nagdesh");
            double x = hit.X, z = hit.Z, best = double.MaxValue;
            TileId c = TileId.At(10, x, z);
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                var id = new TileId(10, c.Tx + dx, c.Ty + dz);
                TileData t;
                foreach (ParkedVehicle p in Parked(id, out t))
                {
                    if (p.Variant != VehicleCatalog.Tractor || !p.CommunityFleet) continue;
                    double px = id.X0 + p.X, pz = id.Z0 + p.Z;
                    best = Math.Min(best, Math.Sqrt((px - x) * (px - x) + (pz - z) * (pz - z)));
                }
            }
            TestContext.WriteLine("nearest fleet tractor " + best.ToString("0") + " m from Nagdesh");
            Assert.That(best, Is.LessThan(250.0), "a borrowable tractor by the fields of Nagdesh");
        }

        [Test]
        public void LorriesTractorsAndVansStandWhereTheyBelongAndCanBeBorrowed()
        {
            var count = new Dictionary<BodyShape, int>();
            int heavy = 0, fleet = 0;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t;
                List<ParkedVehicle> parked = Parked(id, out t);
                if (t == null) continue;
                foreach (ParkedVehicle p in parked)
                {
                    if (!Heavy(p.Variant)) continue;
                    VehicleCatalogEntry e = VehicleCatalog.At(p.Variant);
                    heavy++;
                    if (p.CommunityFleet) fleet++;
                    Assert.That(e.PlayerDrivable, Is.True, e.AssetId + " can be driven when borrowed");
                    count[e.Shape] = count.TryGetValue(e.Shape, out int c) ? c + 1 : 1;
                }
            }
            foreach (var kv in count) TestContext.WriteLine(kv.Key + ": " + kv.Value);
            TestContext.WriteLine(heavy + " heavy parked vehicles, " + fleet + " with the key tag");
            Assert.That(count.TryGetValue(BodyShape.Bus, out int buses) ? buses : 0, Is.GreaterThanOrEqualTo(10), "buses at the bus parks");
            Assert.That((count.TryGetValue(BodyShape.Truck, out int trucks) ? trucks : 0) + (count.TryGetValue(BodyShape.Tipper, out int tippers) ? tippers : 0),
                        Is.GreaterThanOrEqualTo(10), "lorries at depots and on wide streets");
            Assert.That(count.TryGetValue(BodyShape.Tractor, out int tractors) ? tractors : 0, Is.GreaterThanOrEqualTo(5), "tractors on the village lanes");
            Assert.That(count.TryGetValue(BodyShape.Van, out int vans) ? vans : 0, Is.GreaterThanOrEqualTo(10), "vans and micros");
            Assert.That(fleet, Is.GreaterThan(heavy / 2), "most carry the community-fleet key tag");
        }

        [Test]
        public void HeavyParkedVehiclesKeepOffBuildingsAndEachOther()
        {
            // Every heavy body (its true rectangle from the rear-axle origin) stays off building footprints and off every
            // other parked vehicle on Thamel's, Ratna Park's and Gongabu's tiles.
            int checkedBodies = 0, onBuilding = 0, overlapping = 0;
            foreach (double[] at in new[] { new[] { 85.3110, 27.7150 }, new[] { 85.31422, 27.70570 }, new[] { 85.30805, 27.73290 } })
            {
                TileId id = StreamingScene.TileAt(at[0], at[1]);
                TileData t;
                List<ParkedVehicle> parked = Parked(id, out t);
                var mask = new FootprintGrid(t);
                for (int i = 0; i < parked.Count; i++)
                {
                    if (!Heavy(parked[i].Variant)) continue;
                    checkedBodies++;
                    VehicleCatalogEntry e = VehicleCatalog.At(parked[i].Variant);
                    double yaw = parked[i].YawDeg * Math.PI / 180.0, fx = Math.Sin(yaw), fz = Math.Cos(yaw);
                    double back = e.RearOverhangM, ahead = e.LengthM - e.RearOverhangM, w = 0.5 * e.WidthM;
                    for (int k = 0; k < 9; k++)
                    {
                        double la = -back + (back + ahead) * (k % 3) / 2.0, lw = w * (k / 3 - 1);
                        double px = parked[i].X + fx * la + fz * lw, pz = parked[i].Z + fz * la - fx * lw;
                        if (mask.Inside(px, pz))
                        {
                            onBuilding++;
                            break;
                        }
                    }
                    for (int j = 0; j < parked.Count; j++)
                        if (j != i && ParkedPlacement.Overlap(parked[i], parked[j]))
                            overlapping++;
                }
            }
            TestContext.WriteLine(checkedBodies + " heavy bodies checked; " + onBuilding + " touching a building, " + overlapping + " overlapping");
            Assert.That(checkedBodies, Is.GreaterThan(3));
            Assert.That(onBuilding, Is.EqualTo(0));
            Assert.That(overlapping, Is.EqualTo(0));
        }
        [Test, Explicit("diagnostics: lane classes of the roads round a bus park")]
        public void ProbeLaneClassesRoundABusPark([Values("Ratna Park bus station", "Naya Bus Park, Gongabu", "Chabahil bus station", "Nagdesh")] string query)
        {
            SearchEntry hit = Find(query);
            TileId id = TileId.At(10, hit.X, hit.Z);
            TileData t = StreamingSampleRegion.Tile(id);
            RoadLayout lay = RoadLayout.For(t);
            double x = hit.X - id.X0, z = hit.Z - id.Z0;
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                double best = double.MaxValue;
                for (int k = 0; k < r.PointCount; k++)
                {
                    double dx = r.Points[2 * k] / 100.0 - x, dz = r.Points[2 * k + 1] / 100.0 - z;
                    best = Math.Min(best, dx * dx + dz * dz);
                }
                if (best > 120 * 120) continue;
                uint m = Ghumante.Core.Traffic.TrafficTables.LaneClassesOf(t, i, lay);
                RoadWidthProfile prof = lay.Profiles[i];
                TestContext.WriteLine(string.Format("road {0} {1} {2} {3:0} m: access {4} minW {5:0.0} real {6:0.0} lanes 0x{7:X} grounded {8} name {9}", i, r.RoadClass,
                    r.OsmWayId, Math.Sqrt(best), r.Access, prof.Count > 0 ? prof.MinWidth : 0f, prof.RealM, m, ParkedPlacement.Grounded(t, i), t.Name(r.NameRef)?.Default));
            }
        }

        /// <summary>Diagnostics for game/README.md's "where to find them" table: every named bus station and works yard of
        /// the sample with the heavy vehicles parked within 100 m of it.</summary>
        [Test, Explicit("diagnostics: lists the bus parks and depots with their parked heavy vehicles")]
        public void ProbeWhereTheHeavyVehiclesStand()
        {
            var lines = new SortedSet<string>(StringComparer.Ordinal);
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t;
                List<ParkedVehicle> parked = Parked(id, out t);
                if (t == null) continue;
                var sites = new List<(string kind, string name, double x, double z)>();
                foreach (PoiRecord p in t.Pois)
                    if (p.Kind == PoiKind.BusStation)
                        sites.Add(("bus station", t.Name(p.NameRef)?.En ?? t.Name(p.NameRef)?.Default ?? "(unnamed)", p.XCm / 100.0, p.ZCm / 100.0));
                foreach (AreaRecord a in t.Areas)
                {
                    if (a.Kind != AreaKind.Industrial && a.Kind != AreaKind.BusPark || a.Vertices == null || a.Vertices.Length < 6) continue;
                    double cx = 0, cz = 0;
                    for (int k = 0; k + 1 < a.Vertices.Length; k += 2)
                    {
                        cx += a.Vertices[k] / 100.0;
                        cz += a.Vertices[k + 1] / 100.0;
                    }
                    cx /= a.Vertices.Length / 2;
                    cz /= a.Vertices.Length / 2;
                    NameRecord n = t.Name(a.NameRef);
                    sites.Add((a.Kind == AreaKind.Industrial ? "industrial" : "bus park area", n == null ? "(unnamed)" : n.En.Length > 0 ? n.En : n.Default, cx, cz));
                }
                foreach (var site in sites)
                {
                    var kinds = new List<string>();
                    foreach (ParkedVehicle v in parked)
                    {
                        if (!Heavy(v.Variant)) continue;
                        double dx = v.X - site.x, dz = v.Z - site.z;
                        if (dx * dx + dz * dz < 100 * 100) kinds.Add(VehicleCatalog.At(v.Variant).Shape.ToString());
                    }
                    double lon, lat;
                    WorldFrame.GameToLonLat(id.X0 + site.x, id.Z0 + site.z, out lon, out lat);
                    lines.Add(site.kind + ": " + site.name + " (" + lon.ToString("0.0000") + ", " + lat.ToString("0.0000") + "): " + (kinds.Count == 0 ? "none" : string.Join(" ", kinds)));
                }
            }
            foreach (string l in lines) TestContext.WriteLine(l);
        }
    }
}
