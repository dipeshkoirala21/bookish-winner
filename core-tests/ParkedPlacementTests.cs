using System;
using System.Collections.Generic;
using System.Text;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;
using Ghumante.Core.Streaming;
using Ghumante.World.Instancing;
using Ghumante.World.Life;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Parked vehicles on the committed <c>kathmandu_core</c> sample, placed exactly as the streamer places them
    /// (<see cref="ParkedPlacement.Place"/> with the tier's road options), checked against the owner's first Unity playtest
    /// (docs/W2_PLAYTEST_1.md #3): "way too many motorcycles or scooters parked in the roads, narrow streets ... Do not show
    /// parked motorcycles or scooters in main streets like ring roads, Baneshwor roads ... or in busy market areas. Only some
    /// motorcycles will be parked in front of the houses in residential areas, in vertical position alongside the road and
    /// footpath", and the red LOD boxes he saw standing across a lane 10-30 m away.
    /// <para>Before this round (the placement at 3f71f0e) the sample held 77 940 parked vehicles, 68 804 of them
    /// two-wheelers: 68 791 standing in a carriageway (58 007 of them in lanes narrower than 6 m), 50 429 leaving less
    /// than 2.7 m of it, 46 735 not perpendicular to their road, 4 512 on secondary or bigger roads and 5 049 on market
    /// streets (<see cref="ProbeSurvey"/>).</para>
    /// </summary>
    public class ParkedPlacementTests
    {
        /// <summary>A parked vehicle and the road it belongs to.</summary>
        internal struct Seen
        {
            public ParkedVehicle V;
            public TileId Tile;

            /// <summary>The road under the body, else the road whose corridor is nearest the rear wheel (the end at the road).</summary>
            public int Road;

            public RoadClass Class;
            public AreaType Area;
            public bool Market;

            /// <summary>|cos| of the angle between the vehicle's heading and its road: 0 = perpendicular ("vertical").</summary>
            public double Cos;

            /// <summary>How deep the body reaches into a drawn carriageway, footpath, median or junction (0 = wholly off).</summary>
            public double Intrusion;

            /// <summary>The drawn carriageway width of its road there.</summary>
            public double Carriageway;

            /// <summary>A building lies within reach ahead of its nose.</summary>
            public bool HouseAhead;
        }

        internal static bool TwoWheeler(int variant)
        {
            return variant == VehicleCatalog.Scooter || variant == VehicleCatalog.MotorbikeCommuter || variant == VehicleCatalog.MotorbikeCruiser ||
                   variant == VehicleCatalog.EScooter || variant == VehicleCatalog.Bicycle;
        }

        /// <summary>The parked vehicles of a level-10 tile as the streamer places them.</summary>
        internal static List<ParkedVehicle> Parked(TileId id, int tier, out TileData t, out RoadSurfaceQuery surface)
        {
            MeshingSettings s = StreamingScene.Settings(tier, 10, false);
            t = StreamingSampleRegion.Tile(id);
            var parked = new List<ParkedVehicle>();
            surface = null;
            if (t == null) return parked;
            TileHeightSampler h = TileHeightSampler.ForArea(t, id, StreamingConfig.ForTier(tier).TerrainStep(id, id, t.HeightsN));
            ParkedPlacement.Place(t, h, parked, s.Roads);
            surface = RoadSurfaceQuery.For(t, h, s.Roads);
            return parked;
        }

        /// <summary>A 3 x 3 grid over a parked body's plan (tile-local): index 1 is the rear middle, 4 the centre, 7 the
        /// nose middle.</summary>
        internal static int Outline(in ParkedVehicle v, double[] xs, double[] zs)
        {
            VehicleCatalogEntry e = VehicleCatalog.At(v.Variant);
            double yaw = v.YawDeg * Math.PI / 180.0, fx = Math.Sin(yaw), fz = Math.Cos(yaw);
            double back = e.RearOverhangM, ahead = e.LengthM - e.RearOverhangM, w = 0.5 * e.WidthM;
            int n = 0;
            for (int a = 0; a < 3; a++)
                for (int k = -1; k <= 1; k++)
                {
                    double la = -back + (back + ahead) * a / 2.0, lw = w * k;
                    xs[n] = v.X + fx * la + fz * lw;
                    zs[n] = v.Z + fz * la - fx * lw;
                    n++;
                }
            return n;
        }

        /// <summary>A market street by the test's own count (independent of <see cref="ParkedPlacement.MarketIndex"/>): at least
        /// three shops, restaurants, cafés, banks, hotels or market stalls within 25 m and six per 100 m.</summary>
        internal static bool LocalMarket(TileData t, int road)
        {
            RoadRecord r = t.Roads[road];
            double len = 0;
            for (int k = 0; k + 1 < r.PointCount; k++)
            {
                double dx = (r.Points[2 * k + 2] - r.Points[2 * k]) / 100.0, dz = (r.Points[2 * k + 3] - r.Points[2 * k + 1]) / 100.0;
                len += Math.Sqrt(dx * dx + dz * dz);
            }
            int shops = 0;
            foreach (PoiRecord p in t.Pois)
            {
                switch (p.Kind)
                {
                    case PoiKind.Shop:
                    case PoiKind.Marketplace:
                    case PoiKind.Restaurant:
                    case PoiKind.Cafe:
                    case PoiKind.Bank:
                    case PoiKind.Hotel:
                    case PoiKind.GuestHouse:
                    case PoiKind.Hostel:
                        break;
                    default:
                        continue;
                }
                double x = p.XCm / 100.0, z = p.ZCm / 100.0, best = double.MaxValue;
                for (int k = 0; k + 1 < r.PointCount; k++)
                {
                    double ax = r.Points[2 * k] / 100.0, az = r.Points[2 * k + 1] / 100.0, bx = r.Points[2 * k + 2] / 100.0, bz = r.Points[2 * k + 3] / 100.0;
                    double dx = bx - ax, dz = bz - az, l2 = dx * dx + dz * dz;
                    double u = l2 > 1e-9 ? Math.Max(0, Math.Min(1, ((x - ax) * dx + (z - az) * dz) / l2)) : 0;
                    double qx = ax + dx * u - x, qz = az + dz * u - z;
                    best = Math.Min(best, qx * qx + qz * qz);
                }
                if (best <= 25.0 * 25.0) shops++;
            }
            return shops >= 3 && shops * 100.0 / Math.Max(1.0, len) >= 6.0;
        }

        /// <summary>The raw along of the point of road <paramref name="road"/>'s drawn centreline nearest (x, z) and the
        /// piece's direction there.</summary>
        internal static double NearestAlong(RoadLayout layout, TileData t, int road, double x, double z, out double dirX, out double dirZ)
        {
            RoadRecord r = t.Roads[road];
            double length = layout.AlongAt(road, r.PointCount - 1), best = double.MaxValue, bestS = 0;
            for (double sAt = 0; sAt <= length; sAt += 0.5)
            {
                RoadCut c = layout.DrawnCutAt(t, road, sAt);
                double dx = c.CX - x, dz = c.CZ - z, d = dx * dx + dz * dz;
                if (d < best)
                {
                    best = d;
                    bestS = sAt;
                }
            }
            RoadCut cut = layout.DrawnCutAt(t, road, bestS);
            dirX = cut.UZ;
            dirZ = -cut.UX;
            return bestS;
        }

        private static readonly Dictionary<int, List<Seen>> Surveys = new Dictionary<int, List<Seen>>();

        /// <summary>Every parked vehicle of the sample's level-10 tiles with the road it belongs to (cached per tier).</summary>
        internal static List<Seen> Survey(int tier)
        {
            lock (Surveys)
            {
                List<Seen> cached;
                if (Surveys.TryGetValue(tier, out cached)) return cached;
            }
            var all = new List<Seen>();
            var xs = new double[9];
            var zs = new double[9];
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t;
                RoadSurfaceQuery q;
                List<ParkedVehicle> parked = Parked(id, tier, out t, out q);
                if (t == null || parked.Count == 0) continue;
                RoadLayout layout = RoadLayout.For(t);
                RoadCorridorIndex corridors = RoadCorridorIndex.ForTile(t);
                var mask = new FootprintGrid(t);
                foreach (ParkedVehicle v in parked)
                {
                    var s = new Seen { V = v, Tile = id, Road = -1 };
                    int n = Outline(v, xs, zs);
                    double deepest = 0;
                    int under = -1;
                    for (int k = 0; k < n; k++)
                    {
                        RoadSurfaceHit hit;
                        if (!q.TrySample(id.X0 + xs[k], id.Z0 + zs[k], v.Y + 0.5f, out hit)) continue;
                        if (!hit.OnCarriageway && !hit.OnFootpath && !hit.OnJunction && !hit.OnMedian && !hit.OnIsland) continue;
                        double d = hit.OnCarriageway && hit.Road >= 0 ? hit.HalfWidthM + RoadSurfaceQuery.EdgeToleranceM - Math.Abs(hit.LateralM) : 0.3;
                        deepest = Math.Max(deepest, Math.Max(0.01, d));
                        if (hit.Road >= 0) under = hit.Road;
                    }
                    s.Intrusion = deepest;
                    double sd;
                    int road = under >= 0 ? under : corridors.NearestRoad(id.X0 + xs[1], id.Z0 + zs[1], out sd);
                    if (road >= 0)
                    {
                        s.Road = road;
                        s.Class = t.Roads[road].RoadClass;
                        s.Area = t.RoadAttrOf(road).Area;
                        s.Market = LocalMarket(t, road);
                        double dirX, dirZ;
                        double along = NearestAlong(layout, t, road, xs[1], zs[1], out dirX, out dirZ);
                        double yaw = v.YawDeg * Math.PI / 180.0;
                        s.Cos = Math.Abs(Math.Sin(yaw) * dirX + Math.Cos(yaw) * dirZ);
                        s.Carriageway = 2.0 * layout.DrawnHalfWidthAt(road, along);
                    }
                    double fx = Math.Sin(v.YawDeg * Math.PI / 180.0), fz = Math.Cos(v.YawDeg * Math.PI / 180.0);
                    for (double d = 0.3; d <= ParkedPlacement.HouseReachM + 0.5 && !s.HouseAhead; d += 0.25)
                        s.HouseAhead = mask.Inside(xs[7] + fx * d, zs[7] + fz * d);
                    all.Add(s);
                }
            }
            lock (Surveys) Surveys[tier] = all;
            return all;
        }

        /// <summary>Owner: "way too many motorcycles parked in the roads ... which takes up too many space". No parked vehicle
        /// stands on any drawn carriageway, footpath, median, island or junction of the sample, so every lane keeps its whole
        /// drawn width (never less than 2.7 m) free, with every tier's road options.</summary>
        [TestCase(0)]
        [TestCase(2)]
        public void NoParkedVehicleStandsOnADrawnRoad(int tier)
        {
            List<Seen> all = Survey(tier);
            int onRoad = 0, narrow = 0;
            string first = null;
            foreach (Seen s in all)
            {
                if (s.Intrusion <= 0) continue;
                onRoad++;
                if (s.Carriageway < 6.0) narrow++;
                if (first == null)
                    first = VehicleCatalog.At(s.V.Variant).AssetId + " on " + s.Tile + " at (" + s.V.X.ToString("0.0") + ", " + s.V.Z.ToString("0.0") + "), " +
                            s.Intrusion.ToString("0.00") + " m into a " + s.Class;
            }
            TestContext.WriteLine("{0} parked vehicles; {1} on a drawn road ({2} in lanes under 6 m); first {3}", all.Count, onRoad, narrow, first);
            Assert.That(all.Count, Is.GreaterThan(5000), "the city still has its parked vehicles, beside the roads");
            Assert.That(onRoad, Is.EqualTo(0), "no parked vehicle on a carriageway, footpath or junction");
        }

        /// <summary>Owner: "Do not show parked motorcycles or scooters in main streets like ring roads, Baneshwor roads ... or
        /// in busy market areas." No parked motorbike, scooter or bicycle belongs to a trunk, primary, secondary or tertiary
        /// road, nor (but for the odd corner bike) to a market street (shops counted by the test itself).</summary>
        [Test]
        public void NoParkedTwoWheelerBesideMainRoadsOrInMarketStreets()
        {
            int bikes = 0, main = 0, market = 0;
            string first = null;
            foreach (Seen s in Survey(2))
            {
                if (!TwoWheeler(s.V.Variant) || s.Road < 0) continue;
                bikes++;
                bool isMain = s.Class == RoadClass.Trunk || s.Class == RoadClass.Primary || s.Class == RoadClass.Secondary || s.Class == RoadClass.Tertiary ||
                              s.Class == RoadClass.Motorway;
                if (isMain) main++;
                if (s.Market) market++;
                if ((isMain || s.Market) && first == null)
                    first = s.Tile + " (" + s.V.X.ToString("0.0") + ", " + s.V.Z.ToString("0.0") + ") by a " + s.Class + (s.Market ? " market street" : "");
            }
            TestContext.WriteLine("{0} two-wheelers; {1} by main roads, {2} by market streets; first {3}", bikes, main, market, first);
            Assert.That(bikes, Is.GreaterThan(1000));
            // The test takes the corridor nearest the rear wheel: a bike in front of a house at the corner of a quiet lane and
            // a main road or a bazaar may count for it. Allow a handful in the whole valley.
            Assert.That(main, Is.LessThanOrEqualTo(bikes / 1000 + 3), "none by Ring Road, Baneshwor, Kantipath");
            Assert.That(market, Is.LessThanOrEqualTo(bikes / 1000 + 3), "none in the bazaars");
        }

        /// <summary>Owner: "Only some motorcycles will be parked in front of the houses in residential area, that too in
        /// vertical position alongside the road and footpath." In the city's residential streets: one bike per 30 m of kerb
        /// at most (some, not every house), perpendicular to their road (within 12°), each with a house in front of its nose.</summary>
        [Test]
        public void ResidentialTwoWheelersAreFewVerticalAndInFrontOfHouses()
        {
            double kerb = 0;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (t.Roads.Count == 0) continue;
                RoadLayout layout = RoadLayout.For(t);
                for (int i = 0; i < t.Roads.Count; i++)
                {
                    RoadRecord r = t.Roads[i];
                    AreaType a = t.RoadAttrOf(i).Area;
                    if (!ParkedPlacement.HomeStreet(r.RoadClass) || r.PointCount < 2 || (a != AreaType.Urban && a != AreaType.OldCore) || LocalMarket(t, i)) continue;
                    kerb += 2 * layout.AlongAt(i, r.PointCount - 1);
                }
            }
            int bikes = 0, city = 0, angled = 0, noHouse = 0;
            double tilt = Math.Sin(12 * Math.PI / 180);
            foreach (Seen s in Survey(2))
            {
                if (!TwoWheeler(s.V.Variant) || s.Road < 0) continue;
                bikes++;
                if (s.Area == AreaType.Urban || s.Area == AreaType.OldCore) city++;
                if (s.Cos > tilt) angled++;
                if (!s.HouseAhead) noHouse++;
            }
            TestContext.WriteLine("{0:0} km of city residential kerb, {1} city two-wheelers: one per {2:0} m; {3} of {4} not perpendicular, {5} without a house ahead",
                                  kerb / 1000, city, kerb / Math.Max(1, city), angled, bikes, noHouse);
            Assert.That(kerb / Math.Max(1, city), Is.GreaterThanOrEqualTo(30.0), "at most one per 30 m of kerb: not every house");
            Assert.That(kerb / Math.Max(1, city), Is.LessThanOrEqualTo(250.0), "but some: the streets do not look abandoned");
            // At a corner the corridor nearest the rear wheel may be the crossing street's.
            Assert.That(angled, Is.LessThanOrEqualTo(bikes / 100), "vertical: perpendicular to the road edge");
            Assert.That(noHouse, Is.EqualTo(0), "in front of a house");
        }

        /// <summary>The owner's screenshots: parked motorbikes drawn as red boxes across the middle of the Ganesh Mandir lane
        /// (about 4-5 m wide), and scooters standing at angles in the carriageway at Kalopul. No parked vehicle within 150 m of
        /// these streets stands on a drawn road or within 0.2 m of a road's clear corridor any more.</summary>
        [TestCase("Ganesh Mandir")]
        [TestCase("Ganesh Galli")]
        [TestCase("Kalopul")]
        public void TheOwnersLanesKeepTheirWholeCarriageway(string place)
        {
            int roads = 0, near = 0, onIt = 0;
            var xs = new double[9];
            var zs = new double[9];
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                var named = new List<int>();
                for (int i = 0; i < t.Roads.Count; i++)
                {
                    NameRecord n = t.Name(t.Roads[i].NameRef);
                    string name = n == null ? "" : n.En + "|" + n.Default;
                    if (name.IndexOf(place, StringComparison.OrdinalIgnoreCase) >= 0) named.Add(i);
                }
                if (named.Count == 0) continue;
                roads += named.Count;
                RoadSurfaceQuery q;
                TileData tt;
                List<ParkedVehicle> parked = Parked(id, 2, out tt, out q);
                RoadCorridorIndex corridors = RoadCorridorIndex.ForTile(t);
                foreach (ParkedVehicle v in parked)
                {
                    int n = Outline(v, xs, zs);
                    bool close = false;
                    foreach (int r in named)
                    {
                        RoadRecord rr = t.Roads[r];
                        for (int k = 0; k < rr.PointCount && !close; k++)
                        {
                            double dx = rr.Points[2 * k] / 100.0 - v.X, dz = rr.Points[2 * k + 1] / 100.0 - v.Z;
                            close = dx * dx + dz * dz < 150.0 * 150.0;
                        }
                    }
                    if (!close) continue;
                    near++;
                    for (int k = 0; k < n; k++)
                    {
                        RoadSurfaceHit hit;
                        bool on = q.TrySample(id.X0 + xs[k], id.Z0 + zs[k], v.Y + 0.5f, out hit) && (hit.OnCarriageway || hit.OnFootpath || hit.OnJunction);
                        if (on || corridors.SignedDistance(id.X0 + xs[k], id.Z0 + zs[k]) < 0.2)
                        {
                            onIt++;
                            break;
                        }
                    }
                }
            }
            TestContext.WriteLine("{0}: {1} named pieces, {2} parked vehicles within 150 m, {3} on or at a drawn road", place, roads, near, onIt);
            Assert.That(roads, Is.GreaterThan(0), place + " is on the sample");
            Assert.That(onIt, Is.EqualTo(0));
        }

        /// <summary>The red LOD boxes the owner saw 10-30 m away in a lane: with the parked draw levels of every tier
        /// (<see cref="ParkedLod"/>, assigned nearest first as the presenter does), every parked vehicle within 30 m of the
        /// camera is drawn with the near model and none within <see cref="ParkedLod.BlockM"/> as a box, wherever the camera
        /// stands on the sample (at every tenth parked vehicle: the densest places). The old levels (near model to 20 m,
        /// block-out to 80 m, near caps 2/10/10) are printed for comparison.</summary>
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void ParkedVehiclesNearTheCameraAreNeverBlocksOrBoxes(int tier)
        {
            // Every parked vehicle of the sample in game metres, in a 64 m grid.
            var gx = new List<double>();
            var gz = new List<double>();
            var grid = new Dictionary<long, List<int>>();
            foreach (Seen s in Survey(tier))
            {
                int i = gx.Count;
                gx.Add(s.Tile.X0 + s.V.X);
                gz.Add(s.Tile.Z0 + s.V.Z);
                long key = (long)Math.Floor(gx[i] / 64) * 1000003L + (long)Math.Floor(gz[i] / 64);
                List<int> l;
                if (!grid.TryGetValue(key, out l)) grid[key] = l = new List<int>();
                l.Add(i);
            }
            int near, block, total;
            ParkedLod.Caps(tier, out near, out block, out total);
            int[] caps = { near, block, Math.Max(0, total - near - block) };
            float[] radii = { ParkedLod.NearM, ParkedLod.BlockM, ParkedLod.BoxM };
            int[] oldNear = { 2, 10, 10 }, oldBlock = { 20, 60, 120 }, oldTotal = { 80, 200, 400 };
            int t = tier <= 0 ? 0 : tier >= 2 ? 2 : 1;
            int[] legacyCaps = { oldNear[t], oldBlock[t], oldTotal[t] - oldNear[t] - oldBlock[t] };
            float[] legacyRadii = { 20f, 80f, 150f };
            var dist = new float[4096];
            var level = new int[4096];
            var order = new int[4096];
            var keys = new float[4096];
            int cameras = 0, coarseNear = 0, boxNear = 0, legacyCoarse = 0, mostNear = 0, mostBlock = 0;
            for (int c = 0; c < gx.Count; c += 10)
            {
                cameras++;
                double cx = gx[c], cz = gz[c];
                int n = 0;
                long ci = (long)Math.Floor(cx / 64), cj = (long)Math.Floor(cz / 64);
                for (long i = ci - 3; i <= ci + 3; i++)
                    for (long j = cj - 3; j <= cj + 3; j++)
                    {
                        List<int> l;
                        if (!grid.TryGetValue(i * 1000003L + j, out l)) continue;
                        foreach (int k in l)
                        {
                            double dx = gx[k] - cx, dz = gz[k] - cz;
                            float d = (float)Math.Sqrt(dx * dx + dz * dz);
                            if (d > ParkedLod.BoxM || n == dist.Length) continue;
                            dist[n++] = d;
                        }
                    }
                int within30 = 0, withinBlock = 0;
                for (int k = 0; k < n; k++)
                {
                    if (dist[k] <= 30f) within30++;
                    if (dist[k] <= ParkedLod.BlockM) withinBlock++;
                }
                mostNear = Math.Max(mostNear, within30);
                mostBlock = Math.Max(mostBlock, withinBlock);
                LifeLod.Assign(dist, n, caps, radii, level, order, keys);
                for (int k = 0; k < n; k++)
                {
                    if (dist[k] <= 30f && level[k] != 0) coarseNear++;
                    if (dist[k] <= ParkedLod.BlockM && level[k] == 2) boxNear++;
                }
                LifeLod.Assign(dist, n, legacyCaps, legacyRadii, level, order, keys);
                for (int k = 0; k < n; k++)
                    if (dist[k] <= 30f && level[k] != 0)
                        legacyCoarse++;
            }
            TestContext.WriteLine("tier {0}: {1} camera spots, at most {2} parked within 30 m and {7} within {6} m; {3} drawn coarser than the near model within 30 m ({4} with the old levels), {5} boxes within {6} m",
                                  tier, cameras, mostNear, coarseNear, legacyCoarse, boxNear, ParkedLod.BlockM, mostBlock);
            Assert.That(cameras, Is.GreaterThan(100));
            Assert.That(coarseNear, Is.EqualTo(0), "the near model within 30 m");
            Assert.That(boxNear, Is.EqualTo(0), "never a box within the block-out radius");
        }

        /// <summary>Owner #11: "Allow the user to grab and start riding the parked vehicles around". Every parked vehicle the
        /// player can drive carries the borrow tag, and every class stands somewhere on the sample.</summary>
        [Test]
        public void EveryDrivableParkedVehicleCanBeBorrowed()
        {
            var kinds = new Dictionary<BodyShape, int>();
            int vehicles = 0;
            foreach (Seen s in Survey(1))
            {
                VehicleCatalogEntry e = VehicleCatalog.At(s.V.Variant);
                vehicles++;
                Assert.AreEqual(e.PlayerDrivable, s.V.CommunityFleet, e.AssetId);
                kinds[e.Shape] = kinds.TryGetValue(e.Shape, out int c) ? c + 1 : 1;
            }
            var sb = new StringBuilder();
            foreach (var kv in kinds) sb.Append(kv.Key).Append(' ').Append(kv.Value).Append(", ");
            TestContext.WriteLine(vehicles + " parked: " + sb);
            foreach (BodyShape shape in new[] { BodyShape.Scooter, BodyShape.Motorbike, BodyShape.Bus, BodyShape.Van, BodyShape.Truck, BodyShape.Tractor,
                                                BodyShape.Tempo, BodyShape.Pickup })
                Assert.That(kinds.ContainsKey(shape), shape + " parked somewhere on the sample");
        }

        /// <summary>The same tile gives the same parked vehicles every time (D10).</summary>
        [Test]
        public void PlacementIsDeterministic()
        {
            TileId id = StreamingSampleRegion.DensestLeaf();
            TileData t;
            RoadSurfaceQuery q;
            List<ParkedVehicle> a = Parked(id, 1, out t, out q), b = Parked(id, 1, out t, out q);
            Assert.That(a.Count, Is.GreaterThan(0));
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Id, b[i].Id);
                Assert.AreEqual(a[i].X, b[i].X);
                Assert.AreEqual(a[i].YawDeg, b[i].YawDeg);
            }
        }

        /// <summary>The market index agrees with the owner's bazaars: New Road, Thamel and Jyatha are market streets; the
        /// Ganesh Mandir lane is not.</summary>
        [TestCase("New Road", true)]
        [TestCase("Thamel Marg", true)]
        [TestCase("Jyatha Marg", true)]
        [TestCase("Ganesh Mandir Marg", false)]
        public void MarketStreetsAreTheBazaars(string street, bool market)
        {
            int pieces = 0, markets = 0;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                ParkedPlacement.MarketIndex index = null;
                for (int i = 0; i < t.Roads.Count; i++)
                {
                    NameRecord n = t.Name(t.Roads[i].NameRef);
                    if (n == null || !string.Equals(n.En.Length > 0 ? n.En : n.Default, street, StringComparison.OrdinalIgnoreCase)) continue;
                    if (t.Roads[i].PointCount < 2) continue;
                    index = index ?? ParkedPlacement.MarketIndex.For(t);
                    pieces++;
                    if (index.IsMarket(t, i)) markets++;
                }
            }
            TestContext.WriteLine("{0}: {1} of {2} pieces are market streets", street, markets, pieces);
            Assert.That(pieces, Is.GreaterThan(0));
            if (market) Assert.That(markets * 2, Is.GreaterThanOrEqualTo(pieces), "mostly a market street");
            else Assert.AreEqual(0, markets);
        }

        /// <summary>Diagnostics: the parked survey of the sample (the before/after numbers of the report).</summary>
        [Test, Explicit("diagnostics: prints the parked survey")]
        public void ProbeSurvey()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            int tiles = 0;
            long worst = 0;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t;
                RoadSurfaceQuery q;
                long t0 = watch.ElapsedMilliseconds;
                Parked(id, 2, out t, out q);
                worst = Math.Max(worst, watch.ElapsedMilliseconds - t0);
                tiles++;
            }
            TestContext.WriteLine("placement of " + tiles + " level-10 tiles: " + watch.ElapsedMilliseconds + " ms, worst " + worst + " ms");
            List<Seen> all = Survey(2);
            var byClass = new Dictionary<string, int>();
            int bikes = 0, bikesMain = 0, bikesMarket = 0, bikesAngled = 0, bikesIntruding = 0, narrowIntrude = 0;
            foreach (Seen s in all)
            {
                if (!TwoWheeler(s.V.Variant)) continue;
                bikes++;
                string key = s.Class + "/" + s.Area + (s.Market ? "/market" : "");
                byClass[key] = byClass.TryGetValue(key, out int c) ? c + 1 : 1;
                if (s.Class == RoadClass.Trunk || s.Class == RoadClass.Primary || s.Class == RoadClass.Secondary) bikesMain++;
                if (s.Market) bikesMarket++;
                if (s.Cos > Math.Sin(10 * Math.PI / 180)) bikesAngled++;
                if (s.Intrusion > 0) bikesIntruding++;
                if (s.Intrusion > 0 && s.Carriageway < 6.0) narrowIntrude++;
            }
            var sb = new StringBuilder();
            sb.AppendLine(all.Count + " parked, " + bikes + " two-wheelers; main roads " + bikesMain + ", markets " + bikesMarket + ", not perpendicular " +
                          bikesAngled + ", on a drawn road " + bikesIntruding + " (lanes under 6 m: " + narrowIntrude + ")");
            foreach (var kv in byClass) sb.AppendLine("  " + kv.Key + ": " + kv.Value);
            TestContext.WriteLine(sb.ToString());
        }
    }
}
