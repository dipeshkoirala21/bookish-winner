using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Generators.Placement;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.World.Instancing
{
    /// <summary>
    /// Parked vehicles of a tile (W2_DESIGN 5.2, rewritten after the owner's first Unity playtest, docs/W2_PLAYTEST_1.md
    /// #3: "way too many motorcycles parked in the roads ... Only some motorcycles will be parked in front of the houses in
    /// residential areas, in vertical position alongside the road and footpath"). Every parked vehicle stands <b>beside</b>
    /// the drawn roads, never on one, so none ever narrows a carriageway, a footpath or the 4.8 m clear corridor of any
    /// road or footway (<see cref="RoadCorridorIndex"/>): the whole drawn carriageway (always more than
    /// <see cref="ClearWidthM"/>) stays free to ride.
    /// <list type="bullet">
    /// <item><b>Motorbikes, scooters and the odd bicycle</b> only beside residential streets (<see cref="HomeStreet"/>:
    /// residential, living street, unclassified, service, road) that are not market streets (<see cref="MarketIndex"/>:
    /// Asan, Indra Chowk, New Road, Thamel; commercial and marketplace land), never beside trunk, primary, secondary or
    /// tertiary roads (Ring Road, Baneshwor, Kantipath): one slot per about 35 m of kerb in the city
    /// (<see cref="TwoWheelerSpacing"/>), fewer outside it, each bike perpendicular to the road edge ("vertical"), its
    /// rear wheel at the road and its nose to the house in front of it (a house within <see cref="HouseReachM"/>).</item>
    /// <item><b>Cars</b> parallel beside urban residential streets, about one per 100 m of kerb, facing the way that side's
    /// traffic flows (left-hand traffic).</item>
    /// <item><b>Heavy vehicles where they really stand</b> (<see cref="PlaceHeavy"/>): a green city bus, minibuses,
    /// micros and Safa tempos in the yard of every mapped bus park (Ratna Park, the Old Bus Park, Gongabu, Chabahil...),
    /// nose to the road; lorries, tippers, tankers and pickups in the depots and works yards (industrial land); the odd
    /// van, micro, tempo or lorry beside side streets; tractors beside the lanes of the villages and farms.</item>
    /// </list>
    /// A vehicle stands on the terrain (the sampler of the terrain mesh), never on a building, a tree trunk, a pole,
    /// water, a sacred compound, a park, a pitch or another vehicle, never on a slope or down a bank, and never over a
    /// tile edge. Every parked vehicle the player can drive can be borrowed (<see cref="ParkedVehicle.CommunityFleet"/>:
    /// the green key tag). Deterministic from the way id, the site and the slot. Engine-free; runs on the tile build worker.
    /// </summary>
    public static class ParkedPlacement
    {
        private const uint PurposeSlot = 0x50524B53, PurposeKind = 0x50524B4B, PurposeLivery = 0x50524B4C, PurposeCar = 0x50524B43;

        /// <summary>Road lift of the ribbons (RoadOptions.LiftM default).</summary>
        public const float RoadLiftM = 0.25f;

        /// <summary>Most a vehicle standing on a drawn road surface may stand off it (metres). Parked vehicles stand beside
        /// the roads, so none stands on one; kept for the streaming tests that check it.</summary>
        public const float SurfaceToleranceM = 0.3f;

        /// <summary>Least gap (metres) between two parked vehicles.</summary>
        public const double KerbGapM = 0.3;

        /// <summary>The width of the drawn carriageway every parked vehicle leaves free (owner: three bikes side by side):
        /// all of it, as every vehicle stands beside the road; never less than this.</summary>
        public const double ClearWidthM = 2.7;

        /// <summary>Gap (metres) between a parked vehicle and the clear corridor of every road and footway.</summary>
        public const double CorridorGapM = 0.25;

        /// <summary>Farthest (metres) a parked vehicle's inner end may stand beyond the drawn road edge (it is pushed out of
        /// the clear corridor of narrow lanes, but stays by its road).</summary>
        public const double MaxSetbackM = 3.0;

        /// <summary>A motorbike stands "in front of a house": a building lies within this distance ahead of its nose.</summary>
        public const double HouseReachM = 8.0;

        /// <summary>Clearance (metres) a parked body keeps from buildings, trunks and poles.</summary>
        public const double SolidGapM = 0.2;

        /// <summary>Steepest terrain a vehicle parks on: rise across its body (metres).</summary>
        public const float MaxRiseM = 0.45f;

        /// <summary>Most the terrain under a parked vehicle may differ from the terrain at its road's edge (metres): never
        /// down a bank or up a wall.</summary>
        public const float MaxBankM = 1.2f;

        /// <summary>Spacing (metres) of motorbike slots along each kerb by area type: city (old core and urban) 26-44 m
        /// (one per about 35 m of kerb), peri-urban 50-90 m, rural and hill 120-240 m; none in forests. Slots without room
        /// or without a house in front stay empty, so the real count is lower.</summary>
        public static void TwoWheelerSpacing(AreaType a, out float min, out float max)
        {
            switch (a)
            {
                case AreaType.OldCore:
                case AreaType.Urban: min = 26f; max = 44f; return;
                case AreaType.PeriUrban: min = 50f; max = 90f; return;
                case AreaType.Rural:
                case AreaType.Hill: min = 120f; max = 240f; return;
                case AreaType.Forest: min = max = 0f; return;
                default: min = 40f; max = 70f; return;
            }
        }

        /// <summary>Spacing (metres) of car slots along each kerb of an urban residential street (about one per 100 m).</summary>
        public const float CarSpacingMinM = 70f, CarSpacingMaxM = 130f;

        /// <summary>Share of two-wheeler slots that hold a bicycle.</summary>
        public const float BicycleShare = 0.12f;

        /// <summary>True for the residential street classes motorbikes, scooters, bicycles and cars park beside (owner: in
        /// front of the houses in residential areas): residential, living street, unclassified, service and road. Never
        /// trunk, primary, secondary or tertiary roads (Ring Road, Baneshwor, Kantipath, New Road...).</summary>
        public static bool HomeStreet(RoadClass c)
        {
            switch (c)
            {
                case RoadClass.Residential:
                case RoadClass.LivingStreet:
                case RoadClass.Unclassified:
                case RoadClass.Service:
                case RoadClass.Road:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>True for the road classes a van, micro, tempo or lorry stands beside now and then (the home streets and
        /// tertiary roads; not the main roads).</summary>
        public static bool ParksOn(RoadClass c)
        {
            return HomeStreet(c) || c == RoadClass.Tertiary;
        }

        /// <summary>Appends the tile's parked vehicles; returns how many were added (see the class remarks). The
        /// <paramref name="roads"/> options are accepted for the streamer's call (the corridors are the tile's own).
        /// <paramref name="trees"/> and <paramref name="props"/> (tile-local, as placed for the same tile) are kept clear
        /// of; without them only buildings, roads, areas and other vehicles are.</summary>
        public static int Place(TileData t, IHeightSampler h, List<ParkedVehicle> output, RoadOptions roads = null, List<TreeInstance> trees = null,
                                List<StreetProp> props = null)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (t.Roads.Count == 0 || h == null) return 0;
            int before = output.Count;
            var site = new Site(t, h, roads, trees, props, output, before);
            // Heavy vehicles first: where a bike's slot overlaps a bus at the bus park, the bus stays.
            PlaceHeavy(site);
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                if (!HomeStreet(r.RoadClass) || r.Points == null || r.PointCount < 2) continue;
                RoadAttrRecord a = t.RoadAttrOf(i);
                if (a.Has(RoadAttrFlags.NoMotor) || a.Has(RoadAttrFlags.HeritagePedestrian) || a.Has(RoadAttrFlags.Dual)) continue;
                if ((r.Access & Travel.Car) == 0 && (r.Access & Travel.Motorbike) == 0) continue;
                if (!Grounded(t, i) || site.Layout.IsPassage(i) || !RoadMesher.IsDrawn(t, i, null)) continue;
                if (site.Markets.IsMarket(t, i)) continue;
                PlaceAlong(site, i, a.Area);
            }
            RemoveOverlaps(output, before);
            return output.Count - before;
        }

        // ---- The tile and what already stands on it ------------------------------------------------------------------

        /// <summary>Everything a placement on one tile consults (built once per <see cref="Place"/>).</summary>
        private sealed class Site
        {
            public readonly TileData T;
            public readonly IHeightSampler H;
            public readonly RoadLayout Layout;
            public readonly RoadCorridorIndex Corridors;

            /// <summary>The drawn road surface (null without road options): a second check beside the corridors.</summary>
            public readonly RoadSurfaceQuery Surface;

            public readonly FootprintGrid Mask;
            public readonly MarketIndex Markets;
            public readonly AreaMask Areas;
            public readonly List<ParkedVehicle> Output;
            public readonly int First;
            public readonly double Size;

            // Trunks and poles (tile-local x, z, radius) bucketed in 16 m cells.
            private readonly Dictionary<long, List<int>> _solidCells = new Dictionary<long, List<int>>();
            private readonly List<double> _solids = new List<double>();
            private const double SolidCellM = 16.0;

            // Scratch for the body test.
            public readonly double[] Px = new double[32], Pz = new double[32];

            public Site(TileData t, IHeightSampler h, RoadOptions roads, List<TreeInstance> trees, List<StreetProp> props, List<ParkedVehicle> output,
                        int first)
            {
                T = t;
                H = h;
                Layout = RoadLayout.For(t);
                Corridors = RoadCorridorIndex.ForTile(t);
                Surface = roads != null ? RoadSurfaceQuery.For(t, h, roads) : null;
                Mask = new FootprintGrid(t);
                Markets = MarketIndex.For(t);
                Areas = new AreaMask(t);
                Output = output;
                First = first;
                Size = t.Tile.Size;
                if (trees != null)
                    for (int i = 0; i < trees.Count; i++)
                    {
                        TreeInstance tr = trees[i];
                        bool tree = TileInstances.IsTree(tr.Species);
                        if (!tree && tr.Species != TreeSpecies.Boulder) continue;
                        AddSolid(tr.X, tr.Z, tree ? Math.Max(0.25, Math.Min(0.6, 0.08 * tr.CrownM)) : 0.45 * tr.CrownM);
                    }
                if (props != null)
                    for (int i = 0; i < props.Count; i++)
                        AddSolid(props[i].X, props[i].Z, 0.3);
            }

            private void AddSolid(double x, double z, double r)
            {
                int i = _solids.Count / 3;
                _solids.Add(x);
                _solids.Add(z);
                _solids.Add(r);
                long key = (long)Math.Floor(x / SolidCellM) * 1000003L + (long)Math.Floor(z / SolidCellM);
                List<int> l;
                if (!_solidCells.TryGetValue(key, out l)) _solidCells[key] = l = new List<int>(4);
                l.Add(i);
            }

            /// <summary>True when a trunk or pole stands within <paramref name="gap"/> of the oriented rectangle centred at
            /// (cx, cz) with forward (fx, fz), half length <paramref name="hl"/> and half width <paramref name="hw"/>.</summary>
            public bool HitsSolid(double cx, double cz, double fx, double fz, double hl, double hw, double gap)
            {
                if (_solids.Count == 0) return false;
                double reach = hl + hw + gap + 1.0;
                long c0x = (long)Math.Floor((cx - reach) / SolidCellM), c1x = (long)Math.Floor((cx + reach) / SolidCellM);
                long c0z = (long)Math.Floor((cz - reach) / SolidCellM), c1z = (long)Math.Floor((cz + reach) / SolidCellM);
                for (long i = c0x; i <= c1x; i++)
                    for (long j = c0z; j <= c1z; j++)
                    {
                        List<int> l;
                        if (!_solidCells.TryGetValue(i * 1000003L + j, out l)) continue;
                        for (int k = 0; k < l.Count; k++)
                        {
                            int s = l[k] * 3;
                            double dx = _solids[s] - cx, dz = _solids[s + 1] - cz, r = _solids[s + 2] + gap;
                            double a = Math.Abs(dx * fx + dz * fz) - hl, b = Math.Abs(dx * fz - dz * fx) - hw;
                            double ox = Math.Max(a, 0), oz = Math.Max(b, 0);
                            if (ox * ox + oz * oz < r * r) return true;
                        }
                    }
                return false;
            }
        }

        // ---- One vehicle beside the road ------------------------------------------------------------------------------

        /// <summary>
        /// Stands a vehicle of catalogue <paramref name="variant"/> with its body centred at (cx, cz) (tile-local) facing
        /// (fx, fz), if the place is free: the whole body plus <see cref="SolidGapM"/> on the tile, off every building,
        /// trunk, pole and other parked vehicle, at least <paramref name="corridorGap"/> outside the clear corridor of every
        /// road and footway, off water, sacred compounds, parks and pitches (<see cref="AreaMask"/>), on fairly level
        /// terrain no more than <see cref="MaxBankM"/> from <paramref name="roadY"/> (NaN: not checked). Returns false and
        /// adds nothing otherwise.
        /// </summary>
        private const int BodyPoints = 15;

        /// <summary>The body grid's four corners.</summary>
        private static readonly int[] Corners = { 0, 2, 12, 14 };

        private static bool TryStand(Site s, int variant, double cx, double cz, double fx, double fz, double corridorGap, float roadY, ulong key)
        {
            VehicleCatalogEntry e = VehicleCatalog.At(variant);
            double hl = 0.5 * e.LengthM, hw = 0.5 * e.WidthM;
            double rx = fz, rz = -fx; // right of the heading
            // The body (a 5 x 3 grid: BodyPoints) and a ring SolidGapM around it (its corners and the middle of every side).
            int n = 0;
            for (int a = -2; a <= 2; a++)
                for (int b = -1; b <= 1; b++)
                {
                    s.Px[n] = cx + fx * hl * a / 2.0 + rx * hw * b;
                    s.Pz[n] = cz + fz * hl * a / 2.0 + rz * hw * b;
                    n++;
                }
            double gl = hl + SolidGapM, gw = hw + SolidGapM;
            for (int a = -1; a <= 1; a++)
                for (int b = -1; b <= 1; b++)
                {
                    if (a == 0 && b == 0) continue;
                    s.Px[n] = cx + fx * gl * a + rx * gw * b;
                    s.Pz[n] = cz + fz * gl * a + rz * gw * b;
                    n++;
                }
            const double margin = 0.5;
            for (int k = 0; k < n; k++)
            {
                double px = s.Px[k], pz = s.Pz[k];
                if (px < margin || pz < margin || px >= s.Size - margin || pz >= s.Size - margin) return false;
                if (s.Mask.Inside(px, pz)) return false;
            }
            // Beside every road and footway, never on one: the body outside the clear corridors (game metres).
            double x0 = s.T.Tile.X0, z0 = s.T.Tile.Z0;
            // The corridor distance changes by at most the distance moved: a centre farther than the half diagonal clears
            // the whole body at once.
            if (s.Corridors.SignedDistance(x0 + cx, z0 + cz) < corridorGap + Math.Sqrt(hl * hl + hw * hw))
                for (int k = 0; k < BodyPoints; k++)
                    if (s.Corridors.SignedDistance(x0 + s.Px[k], z0 + s.Pz[k]) < corridorGap)
                        return false;
            for (int k = 0; k < n; k++)
                if (s.Areas.Forbidden(s.Px[k], s.Pz[k]))
                    return false;
            if (s.HitsSolid(cx, cz, fx, fz, hl, hw, SolidGapM)) return false;
            // Level ground: the terrain at the four corners.
            float lo = float.MaxValue, hi = float.MinValue;
            for (int a = -1; a <= 1; a += 2)
                for (int b = -1; b <= 1; b += 2)
                {
                    float y;
                    if (!s.H.TryHeight(x0 + cx + fx * hl * a + rx * hw * b, z0 + cz + fz * hl * a + rz * hw * b, out y)) return false;
                    if (y < lo) lo = y;
                    if (y > hi) hi = y;
                }
            if (hi - lo > MaxRiseM) return false;
            double back = hl - e.RearOverhangM; // body centre to the rear axle
            double ox = cx - fx * back, oz = cz - fz * back; // the vehicle frame's origin: under the rear axle
            float oy;
            if (!s.H.TryHeight(x0 + ox, z0 + oz, out oy)) return false;
            if (!float.IsNaN(roadY) && Math.Abs(oy - roadY) > MaxBankM) return false;
            var v = new ParkedVehicle
            {
                X = (float)ox, Y = oy, Z = (float)oz,
                YawDeg = (float)(Math.Atan2(fx, fz) * 180.0 / Math.PI),
                Variant = (ushort)variant,
                Livery = e.PickLivery(WorldHash.Fnv1a(key, PurposeLivery)),
                CommunityFleet = e.PlayerDrivable,
                Id = WorldHash.Fnv1a(key, 0),
            };
            for (int k = s.First; k < s.Output.Count; k++)
                if (Overlap(s.Output[k], v, KerbGapM))
                    return false;
            // Last (it is the dearest test): off the drawn surface itself wherever a corner comes near a corridor, should a
            // piece be drawn wider than its corridor.
            if (s.Surface != null)
                for (int c = 0; c < Corners.Length; c++)
                {
                    int k = Corners[c];
                    if (s.Corridors.SignedDistance(x0 + s.Px[k], z0 + s.Pz[k]) > 1.0) continue;
                    RoadSurfaceHit hit;
                    if (s.Surface.TrySample(x0 + s.Px[k], z0 + s.Pz[k], out hit) && (hit.OnCarriageway || hit.OnFootpath || hit.OnMedian || hit.OnIsland))
                        return false;
                }
            s.Output.Add(v);
            return true;
        }

        /// <summary>The road edge beside road <paramref name="road"/> at raw along <paramref name="at"/> on side
        /// <paramref name="side"/> (+1 left of the piece's point order, −1 right): the outer edge of the drawn extent
        /// (carriageway, then footpath or shoulder: <see cref="RoadLayout.CorridorSides"/>), its outward unit normal and the
        /// piece's direction there.</summary>
        private static void Edge(Site s, int road, double at, double side, out double ex, out double ez, out double wx, out double wz, out double tx,
                                 out double tz)
        {
            RoadCut cut = s.Layout.DrawnCutAt(s.T, road, at);
            float left, right;
            s.Layout.CorridorSides(road, at, out left, out right);
            double off = side > 0 ? left : -right;
            ex = cut.CX + cut.UX * off;
            ez = cut.CZ + cut.UZ * off;
            wx = cut.UX * side;
            wz = cut.UZ * side;
            tx = cut.UZ; // the piece's direction: the left normal turned clockwise
            tz = -cut.UX;
        }

        /// <summary>How much further out than the road edge point (ix, iz) a body's inner side (half its width
        /// <paramref name="halfAcross"/> along (rx, rz)) must move, along (wx, wz), to clear every road's corridor by
        /// <paramref name="gap"/> (narrow lanes: their corridor is wider than the drawn lane); −1 when that is more than
        /// <see cref="MaxSetbackM"/>.</summary>
        private static double Setback(Site s, double ix, double iz, double wx, double wz, double rx, double rz, double halfAcross, double gap)
        {
            double x0 = s.T.Tile.X0, z0 = s.T.Tile.Z0, push = 0;
            for (int it = 0; it < 5; it++)
            {
                double worst = double.MaxValue;
                for (int b = -1; b <= 1; b++)
                {
                    double px = ix + wx * push + rx * halfAcross * b, pz = iz + wz * push + rz * halfAcross * b;
                    worst = Math.Min(worst, s.Corridors.SignedDistance(x0 + px, z0 + pz));
                }
                if (worst >= gap) return push;
                push += gap - worst + 0.05;
                if (push > MaxSetbackM) return -1;
            }
            return -1;
        }

        /// <summary>True when a building footprint lies within <paramref name="reach"/> ahead of (x, z) along (fx, fz).</summary>
        private static bool HouseAhead(Site s, double x, double z, double fx, double fz, double reach)
        {
            for (double d = 0.3; d <= reach; d += 0.5)
                if (s.Mask.Inside(x + fx * d, z + fz * d))
                    return true;
            return false;
        }

        private static float TerrainAt(Site s, double x, double z)
        {
            float y;
            return s.H.TryHeight(s.T.Tile.X0 + x, s.T.Tile.Z0 + z, out y) ? y : float.NaN;
        }

        // ---- Motorbikes, scooters, bicycles and cars beside a residential street ------------------------------------

        private static void PlaceAlong(Site s, int road, AreaType area)
        {
            TileData t = s.T;
            RoadRecord r = t.Roads[road];
            double length = s.Layout.AlongAt(road, r.PointCount - 1);
            if (length < 20) return;
            ulong way = r.OsmWayId * 31UL + (ulong)road;
            float min, max;
            TwoWheelerSpacing(area, out min, out max);
            bool cars = (area == AreaType.Urban || area == AreaType.PeriUrban) && r.RoadClass != RoadClass.Service;
            for (int sideIx = 0; sideIx < 2; sideIx++)
            {
                double side = sideIx == 0 ? 1.0 : -1.0;
                ulong kerb = way * 2UL + (ulong)sideIx;
                if (max > 0f)
                {
                    uint slot = 0;
                    for (double at = 8 + WorldHash.Range(kerb, PurposeSlot, 0f, max); at < length - 8; slot++)
                    {
                        ulong key = kerb * 1315423911UL + slot;
                        double here = at;
                        at += WorldHash.Range(key, PurposeSlot, min, max);
                        bool bicycle = WorldHash.Unit(key, PurposeKind) < BicycleShare;
                        int variant = bicycle ? VehicleCatalog.Bicycle : PickBike(key);
                        // The slot's house front, or the next one along (a gate, a yard, a shop front: where there is room).
                        for (int tryIx = 0; tryIx < SlotTries.Length; tryIx++)
                        {
                            double along = here + SlotTries[tryIx];
                            if (along < 6 || along > length - 6 || s.Layout.InGap(road, along)) continue;
                            if (TryBike(s, road, along, side, variant, key)) break;
                        }
                    }
                }
                if (!cars) continue;
                uint cs = 0;
                for (double at = 10 + WorldHash.Range(kerb, PurposeCar, 0f, CarSpacingMaxM); at < length - 10; cs++)
                {
                    ulong key = (kerb * 2654435761UL + cs) ^ 0xCA2CA2UL;
                    double here = at;
                    at += WorldHash.Range(key, PurposeCar, CarSpacingMinM, CarSpacingMaxM);
                    if (s.Layout.InGap(road, here)) continue;
                    float u = WorldHash.Unit(key, PurposeKind + 2);
                    int variant = u < 0.5f ? VehicleCatalog.Hatchback : u < 0.68f ? VehicleCatalog.Taxi : u < 0.85f ? VehicleCatalog.Suv : VehicleCatalog.Ev;
                    TryParallel(s, road, here, side, variant, key, true);
                }
            }
        }

        /// <summary>Offsets (metres along the street) tried in turn for a motorbike slot.</summary>
        private static readonly double[] SlotTries = { 0, 3.5, -3.5, 7, -7, 10.5, -10.5, 14, -14 };

        private static int PickBike(ulong key)
        {
            float u = WorldHash.Unit(key, PurposeKind + 1);
            return u < 0.38f ? VehicleCatalog.Scooter : u < 0.9f ? VehicleCatalog.MotorbikeCommuter : u < 0.95f ? VehicleCatalog.MotorbikeCruiser : VehicleCatalog.EScooter;
        }

        /// <summary>A motorbike, scooter or bicycle perpendicular to the road edge ("vertical"), its rear wheel just
        /// outside the clear corridor and its nose to the house in front of it.</summary>
        private static bool TryBike(Site s, int road, double at, double side, int variant, ulong key)
        {
            double ex, ez, wx, wz, tx, tz;
            Edge(s, road, at, side, out ex, out ez, out wx, out wz, out tx, out tz);
            VehicleCatalogEntry e = VehicleCatalog.At(variant);
            double push = Setback(s, ex + wx * CorridorGapM, ez + wz * CorridorGapM, wx, wz, tx, tz, 0.5 * e.WidthM, CorridorGapM);
            if (push < 0) return false;
            double inner = CorridorGapM + push;
            double cx = ex + wx * (inner + 0.5 * e.LengthM), cz = ez + wz * (inner + 0.5 * e.LengthM);
            // In front of a house: a wall within reach of its nose.
            double nx = ex + wx * (inner + e.LengthM), nz = ez + wz * (inner + e.LengthM);
            if (!HouseAhead(s, nx, nz, wx, wz, HouseReachM)) return false;
            return TryStand(s, variant, cx, cz, wx, wz, CorridorGapM, TerrainAt(s, ex, ez), key);
        }

        /// <summary>A car, van or lorry parallel to the road edge just outside the clear corridor, facing the way that
        /// side's traffic flows (left-hand traffic: the +1 side faces the piece's point order); with
        /// <paramref name="house"/>, only in front of a house.</summary>
        private static bool TryParallel(Site s, int road, double at, double side, int variant, ulong key, bool house)
        {
            double ex, ez, wx, wz, tx, tz;
            Edge(s, road, at, side, out ex, out ez, out wx, out wz, out tx, out tz);
            VehicleCatalogEntry e = VehicleCatalog.At(variant);
            double push = Setback(s, ex + wx * CorridorGapM, ez + wz * CorridorGapM, wx, wz, tx, tz, 0.5 * e.LengthM, CorridorGapM);
            if (push < 0) return false;
            double inner = CorridorGapM + push;
            double cx = ex + wx * (inner + 0.5 * e.WidthM), cz = ez + wz * (inner + 0.5 * e.WidthM);
            if (house && !HouseAhead(s, ex + wx * (inner + e.WidthM), ez + wz * (inner + e.WidthM), wx, wz, HouseReachM + 2.0)) return false;
            return TryStand(s, variant, cx, cz, tx * side, tz * side, CorridorGapM, TerrainAt(s, ex, ez), key);
        }

        // ---- Heavy vehicles where they really stand ------------------------------------------------------------------

        private const uint PurposeHeavy = 0x48455659, PurposeHeavySlot = 0x48565359, PurposeHeavyKind = 0x4856594B;

        /// <summary>Vehicles in the yard of a bus park: a green city bus, then micros, minibuses and tempos.</summary>
        public const int BusParkVehicles = 4;

        /// <summary>Bus stations closer than this to one already served are the same bus park (OSM maps every ticket
        /// counter of Gongabu or the Old Bus Park as its own station).</summary>
        public const double BusParkSpacingM = 120.0;

        /// <summary>A bus park's vehicles stand within this distance of it.</summary>
        public const double BusParkReachM = 70.0;

        /// <summary>A depot's lorry stands within this distance of the middle of its works yard.</summary>
        public const double DepotReachM = 60.0;

        /// <summary>Vans, micros, tempos, pickups and lorries per 100 m of side street; tractors per 100 m of rural and hill
        /// lane, and of peri-urban lane.</summary>
        public const float WideStreetHeavyPer100M = 0.3f, RuralTractorPer100M = 0.5f, PeriUrbanTractorPer100M = 0.25f;

        /// <summary>Parks the tile's heavy vehicles (see the class remarks); deterministic from the site and the way.</summary>
        private static void PlaceHeavy(Site s)
        {
            TileData t = s.T;
            // 1. Bus parks: every mapped bus station (and bus-park area), one group per BusParkSpacingM, in the yard.
            var siteX = new List<double>();
            var siteZ = new List<double>();
            var siteKey = new List<ulong>();
            for (int i = 0; i < t.Pois.Count; i++)
            {
                PoiRecord p = t.Pois[i];
                if (p.Kind != PoiKind.BusStation) continue;
                AddSite(siteX, siteZ, siteKey, p.XCm / 100.0, p.ZCm / 100.0, p.OsmRef);
            }
            for (int i = 0; i < t.Areas.Count; i++)
            {
                AreaRecord a = t.Areas[i];
                if (a.Kind != AreaKind.BusPark || a.Vertices == null || a.Vertices.Length < 6) continue;
                double cx, cz;
                Centroid(a.Vertices, out cx, out cz);
                AddSite(siteX, siteZ, siteKey, cx, cz, a.OsmRef);
            }
            for (int k = 0; k < siteX.Count; k++) ParkInYard(s, siteX[k], siteZ[k], siteKey[k], SiteKind.BusPark, BusParkReachM, -1);
            // 2. Depots and works yards: a lorry, a tipper, a tanker or a pickup in every industrial area.
            for (int i = 0; i < t.Areas.Count; i++)
            {
                AreaRecord a = t.Areas[i];
                if (a.Kind != AreaKind.Industrial || a.Vertices == null || a.Vertices.Length < 6) continue;
                double cx, cz;
                Centroid(a.Vertices, out cx, out cz);
                ParkInYard(s, cx, cz, a.OsmRef * 7UL + 3UL, SiteKind.Depot, DepotReachM, i);
            }
            // 3. Side streets (a van, a micro, a tempo, a pickup or a lorry now and then) and farm lanes (tractors: rural,
            // hill and peri-urban lanes beside fields and orchards, not the city's parks and lawns).
            List<double> farms = FarmBoxes(t);
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                if (r.Points == null || r.PointCount < 2 || !Grounded(t, i) || s.Layout.IsPassage(i) || !RoadMesher.IsDrawn(t, i, null)) continue;
                RoadAttrRecord a = t.RoadAttrOf(i);
                if (a.Has(RoadAttrFlags.NoMotor) || a.Has(RoadAttrFlags.HeritagePedestrian) || (r.Access & Travel.Car) == 0) continue;
                bool rural = a.Area == AreaType.Rural || a.Area == AreaType.Hill;
                float per100;
                SiteKind kind;
                if ((rural || a.Area == AreaType.PeriUrban) && (HomeStreet(r.RoadClass) || r.RoadClass == RoadClass.Track) && NearFarm(farms, r))
                {
                    per100 = rural ? RuralTractorPer100M : PeriUrbanTractorPer100M;
                    kind = SiteKind.Farm;
                }
                else if ((a.Area == AreaType.Urban || a.Area == AreaType.PeriUrban) && ParksOn(r.RoadClass) && !a.Has(RoadAttrFlags.Dual) &&
                         !s.Markets.IsMarket(t, i))
                {
                    per100 = WideStreetHeavyPer100M;
                    kind = SiteKind.Street;
                }
                else continue;
                double length = s.Layout.AlongAt(i, r.PointCount - 1);
                if (length < 40) continue;
                ulong way = r.OsmWayId * 31UL + (ulong)i ^ 0x5A5A5A5AUL;
                double expect = length * per100 / 100.0;
                int count = (int)Math.Floor(expect + WorldHash.Unit(way, PurposeHeavy));
                for (int c = 0; c < count; c++)
                {
                    ulong key = way * 2654435761UL + (ulong)c;
                    double side = (WorldHash.Fnv1a(key, PurposeHeavySlot + 1) & 1) == 0 ? 1.0 : -1.0;
                    int variant = PickHeavy(kind, key, 0);
                    // A few tries along the street for a place with room beside it.
                    for (int tryIx = 0; tryIx < 4; tryIx++)
                    {
                        double along = 15 + WorldHash.Unit(key + (ulong)tryIx * 0x9E3779B9UL, PurposeHeavySlot) * Math.Max(0.0, length - 30);
                        if (s.Layout.InGap(i, along)) continue;
                        if (TryParallel(s, i, along, side, variant, key + (ulong)tryIx, kind == SiteKind.Street)) break;
                    }
                }
            }
        }

        /// <summary>Bounding boxes (minX, minZ, maxX, maxZ, tile metres) of the tile's fields and orchards.</summary>
        private static List<double> FarmBoxes(TileData t)
        {
            var boxes = new List<double>();
            for (int i = 0; i < t.Areas.Count; i++)
            {
                AreaRecord a = t.Areas[i];
                // Fields and orchards only: meadows and grassland include the city's lawns and parade grounds (Tundikhel).
                if (a.Kind != AreaKind.Farmland && a.Kind != AreaKind.Orchard) continue;
                if (a.Vertices == null || a.Vertices.Length < 6) continue;
                double x0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, z1 = double.MinValue;
                for (int k = 0; k + 1 < a.Vertices.Length; k += 2)
                {
                    double x = a.Vertices[k] / 100.0, z = a.Vertices[k + 1] / 100.0;
                    if (x < x0) x0 = x;
                    if (x > x1) x1 = x;
                    if (z < z0) z0 = z;
                    if (z > z1) z1 = z;
                }
                boxes.Add(x0);
                boxes.Add(z0);
                boxes.Add(x1);
                boxes.Add(z1);
            }
            return boxes;
        }

        /// <summary>A farm lane: some point of the road within 40 m of a field's or orchard's box.</summary>
        private static bool NearFarm(List<double> boxes, RoadRecord r)
        {
            const double reach = 40.0;
            for (int k = 0; k < r.PointCount; k++)
            {
                double x = r.Points[2 * k] / 100.0, z = r.Points[2 * k + 1] / 100.0;
                for (int b = 0; b + 3 < boxes.Count; b += 4)
                    if (x >= boxes[b] - reach && x <= boxes[b + 2] + reach && z >= boxes[b + 1] - reach && z <= boxes[b + 3] + reach)
                        return true;
            }
            return false;
        }

        private enum SiteKind : byte
        {
            BusPark,
            Depot,
            Street,
            Farm,
        }

        private static void AddSite(List<double> xs, List<double> zs, List<ulong> keys, double x, double z, ulong osm)
        {
            for (int k = 0; k < xs.Count; k++)
            {
                double dx = xs[k] - x, dz = zs[k] - z;
                if (dx * dx + dz * dz < BusParkSpacingM * BusParkSpacingM) return;
            }
            xs.Add(x);
            zs.Add(z);
            keys.Add(osm * 2654435761UL + 0x42555350UL);
        }

        private static void Centroid(int[] v, out double x, out double z)
        {
            x = z = 0;
            int n = v.Length / 2;
            for (int k = 0; k < n; k++)
            {
                x += v[2 * k] / 100.0;
                z += v[2 * k + 1] / 100.0;
            }
            x /= n;
            z /= n;
        }

        /// <summary>The variant for slot <paramref name="slot"/> of a site: at a bus park the green city bus first, then
        /// micros, minibuses and Safa tempos; lorries, tippers, tankers and pickups at a depot; vans (micros), tempos,
        /// pickups and lorries on a street; tractors on a farm lane.</summary>
        private static int PickHeavy(SiteKind kind, ulong key, int slot)
        {
            float u = WorldHash.Unit(key, PurposeHeavyKind);
            switch (kind)
            {
                case SiteKind.BusPark:
                    if (slot == 0) return VehicleCatalog.BusCityGreen;
                    return u < 0.4f ? VehicleCatalog.Microbus : u < 0.7f ? VehicleCatalog.Minibus : VehicleCatalog.Tempo;
                case SiteKind.Depot:
                    return u < 0.4f ? VehicleCatalog.TruckPainted : u < 0.65f ? VehicleCatalog.Tipper : u < 0.8f ? VehicleCatalog.Tanker : VehicleCatalog.Pickup;
                case SiteKind.Street:
                    return u < 0.35f ? VehicleCatalog.Microbus : u < 0.55f ? VehicleCatalog.Tempo : u < 0.75f ? VehicleCatalog.Pickup :
                        u < 0.92f ? VehicleCatalog.TruckPainted : VehicleCatalog.Tipper;
                default:
                    return VehicleCatalog.Tractor;
            }
        }

        /// <summary>
        /// Parks a site's vehicles in the open ground around (x, z) (a bus park's yard, a depot's yard), searching rings
        /// outwards to <paramref name="reach"/>: each nose to the nearest road (so it can drive out), 0.5 m clear of every
        /// road's corridor, building and other vehicle. A depot's vehicles stand inside its area (<paramref name="area"/>
        /// ≥ 0).
        /// </summary>
        private static void ParkInYard(Site s, double x, double z, ulong key, SiteKind kind, double reach, int area)
        {
            int want = kind == SiteKind.BusPark ? BusParkVehicles : 1;
            int placed = 0, slot = 0;
            double x0 = s.T.Tile.X0, z0 = s.T.Tile.Z0;
            const double step = 3.5;
            for (double r = 0; r <= reach && placed < want; r += step)
            {
                int around = r < 1 ? 1 : Math.Max(6, (int)Math.Ceiling(2 * Math.PI * r / step));
                double phase = WorldHash.Unit(key, PurposeHeavySlot + (uint)r) * 2 * Math.PI;
                for (int k = 0; k < around && placed < want; k++)
                {
                    double ang = phase + 2 * Math.PI * k / around;
                    double cx = x + r * Math.Cos(ang), cz = z + r * Math.Sin(ang);
                    if (cx < 1 || cz < 1 || cx > s.Size - 1 || cz > s.Size - 1) continue;
                    if (area >= 0 && !AreaMask.Contains(s.T.Areas[area], cx, cz)) continue;
                    double d0 = s.Corridors.SignedDistance(x0 + cx, z0 + cz);
                    if (d0 < 2.0 || d0 > 40.0) continue; // on a road, or far from every road
                    // Nose to the road: down the corridor distance's gradient.
                    double gx = s.Corridors.SignedDistance(x0 + cx + 0.5, z0 + cz) - s.Corridors.SignedDistance(x0 + cx - 0.5, z0 + cz);
                    double gz = s.Corridors.SignedDistance(x0 + cx, z0 + cz + 0.5) - s.Corridors.SignedDistance(x0 + cx, z0 + cz - 0.5);
                    double gl = Math.Sqrt(gx * gx + gz * gz);
                    if (gl < 1e-6) continue;
                    double fx = -gx / gl, fz = -gz / gl;
                    ulong slotKey = key * 31UL + (ulong)slot;
                    int variant = PickHeavy(kind, slotKey, placed);
                    VehicleCatalogEntry e = VehicleCatalog.At(variant);
                    slot++;
                    // The nose stops short of the road: move the body back until its front clears the corridor.
                    double ahead = 0.5 * e.LengthM + 0.6 - d0;
                    if (ahead > 0)
                    {
                        cx -= fx * ahead;
                        cz -= fz * ahead;
                    }
                    if (TryStand(s, variant, cx, cz, fx, fz, 0.5, float.NaN, slotKey)) placed++;
                }
            }
        }

        // ---- Shared tests -------------------------------------------------------------------------------------------

        /// <summary>
        /// Drops every vehicle (from <paramref name="first"/> on) whose plan rectangle overlaps one kept before it.
        /// Deterministic (placement order); a 4 m grid keeps it linear.
        /// </summary>
        public static int RemoveOverlaps(List<ParkedVehicle> output, int first)
        {
            if (output.Count - first < 2) return 0;
            const double cell = 4.0;
            var grid = new Dictionary<long, List<int>>();
            int w = first, removed = 0;
            for (int i = first; i < output.Count; i++)
            {
                ParkedVehicle v = output[i];
                long cx = (long)Math.Floor(v.X / cell), cz = (long)Math.Floor(v.Z / cell);
                bool hit = false;
                // A bus is 12 m long: its origin and another's may be three cells apart.
                for (long gx = cx - 4; gx <= cx + 4 && !hit; gx++)
                    for (long gz = cz - 4; gz <= cz + 4 && !hit; gz++)
                    {
                        List<int> list;
                        if (!grid.TryGetValue(gx * 1000003L + gz, out list)) continue;
                        for (int k = 0; k < list.Count && !hit; k++) hit = Overlap(output[list[k]], v);
                    }
                if (hit)
                {
                    removed++;
                    continue;
                }
                output[w] = v;
                List<int> own;
                long key = cx * 1000003L + cz;
                if (!grid.TryGetValue(key, out own)) grid[key] = own = new List<int>(4);
                own.Add(w);
                w++;
            }
            output.RemoveRange(w, output.Count - w);
            return removed;
        }

        /// <summary>True when two parked vehicles' plan rectangles (catalogue length and width about the body centre, which
        /// lies half a length minus <see cref="VehicleCatalogEntry.RearOverhangM"/> ahead of the rear-axle origin; compass
        /// yaw) overlap and they stand within 2 m of the same height (separating axes).</summary>
        public static bool Overlap(in ParkedVehicle a, in ParkedVehicle b)
        {
            return Overlap(a, b, 0.0);
        }

        /// <summary>As <see cref="Overlap(in ParkedVehicle, in ParkedVehicle)"/> with each rectangle grown by half of
        /// <paramref name="gap"/> on every side: true when they stand closer than <paramref name="gap"/>.</summary>
        public static bool Overlap(in ParkedVehicle a, in ParkedVehicle b, double gap)
        {
            if (Math.Abs(a.Y - b.Y) > 2f) return false;
            VehicleCatalogEntry ea = VehicleCatalog.At(a.Variant), eb = VehicleCatalog.At(b.Variant);
            double ya = a.YawDeg * Math.PI / 180.0, yb = b.YawDeg * Math.PI / 180.0;
            double afx = Math.Sin(ya), afz = Math.Cos(ya), bfx = Math.Sin(yb), bfz = Math.Cos(yb);
            double g = 0.5 * gap;
            double ha = 0.5 * ea.LengthM + g, wa = 0.5 * ea.WidthM + g, hb = 0.5 * eb.LengthM + g, wb = 0.5 * eb.WidthM + g;
            double ca = 0.5 * ea.LengthM - ea.RearOverhangM, cb = 0.5 * eb.LengthM - eb.RearOverhangM;
            double cx = (b.X + bfx * cb) - (a.X + afx * ca), cz = (b.Z + bfz * cb) - (a.Z + afz * ca);
            if (cx * cx + cz * cz > (ha + wa + hb + wb) * (ha + wa + hb + wb)) return false;
            for (int k = 0; k < 4; k++)
            {
                // Axes: a's forward and right, b's forward and right (right = (fz, -fx)).
                double nx = k == 0 ? afx : k == 1 ? afz : k == 2 ? bfx : bfz;
                double nz = k == 0 ? afz : k == 1 ? -afx : k == 2 ? bfz : -bfx;
                double ra = ha * Math.Abs(afx * nx + afz * nz) + wa * Math.Abs(afz * nx - afx * nz);
                double rb = hb * Math.Abs(bfx * nx + bfz * nz) + wb * Math.Abs(bfz * nx - bfx * nz);
                if (Math.Abs(cx * nx + cz * nz) >= ra + rb) return false;
            }
            return true;
        }

        /// <summary>
        /// True when road <paramref name="i"/> lies on the ground along its whole length: not a bridge, flyover or tunnel
        /// (tags or structure record) and no point on a deck, ramp or lowered cutting (<see cref="RoadStructureRecord.HasHeights"/>).
        /// Nobody parks beside a deck, an embankment or a cutting.
        /// </summary>
        public static bool Grounded(TileData t, int i)
        {
            RoadRecord r = t.Roads[i];
            if ((r.Flags & (RoadFlags.Bridge | RoadFlags.Tunnel)) != 0) return false;
            RoadStructureRecord s = t.RoadStructureOf(i);
            if (s.HasHeights || s.IsElevated) return false;
            switch (s.Kind)
            {
                case RoadStructureKind.Bridge:
                case RoadStructureKind.Flyover:
                case RoadStructureKind.Tunnel:
                    return false;
                default:
                    return true;
            }
        }

        // ---- Market streets -------------------------------------------------------------------------------------------

        /// <summary>
        /// Which streets of a tile are busy market streets (owner: no parked motorbikes "in busy market areas"): a street
        /// with at least <see cref="MinShops"/> shops, restaurants, cafés, banks, hotels or market stalls within
        /// <see cref="ShopReachM"/> of it and at least <see cref="ShopsPer100M"/> per 100 m of its length (on the sample:
        /// New Road about 25, Thamel 45, Jyatha 40-90, Putalisadak 25; residential lanes 0-2), or one whose middle lies on
        /// commercial or marketplace land. Lazy per street.
        /// </summary>
        public sealed class MarketIndex
        {
            /// <summary>Shop-like places a market street has at least (in all) ...</summary>
            public const int MinShops = 3;

            /// <summary>... and per 100 m of its length.</summary>
            public const float ShopsPer100M = 6f;

            /// <summary>How far from the street (metres) a shop counts as on it.</summary>
            public const double ShopReachM = 25.0;

            private const double CellM = 32.0;
            private readonly TileData _t;
            private readonly sbyte[] _market;
            private readonly Dictionary<long, List<int>> _shops = new Dictionary<long, List<int>>();
            private readonly List<int> _commercial = new List<int>();

            private MarketIndex(TileData t)
            {
                _t = t;
                _market = new sbyte[t.Roads.Count];
                for (int i = 0; i < t.Pois.Count; i++)
                {
                    if (!Shoplike(t.Pois[i].Kind)) continue;
                    long key = Key(t.Pois[i].XCm / 100.0, t.Pois[i].ZCm / 100.0);
                    List<int> l;
                    if (!_shops.TryGetValue(key, out l)) _shops[key] = l = new List<int>(8);
                    l.Add(i);
                }
                for (int i = 0; i < t.Areas.Count; i++)
                {
                    AreaKind k = t.Areas[i].Kind;
                    if ((k == AreaKind.Commercial || k == AreaKind.Marketplace) && t.Areas[i].Vertices != null && t.Areas[i].Vertices.Length >= 6)
                        _commercial.Add(i);
                }
            }

            /// <summary>A market index of a tile (cheap to build; streets are classified on first ask).</summary>
            public static MarketIndex For(TileData t)
            {
                if (t == null) throw new ArgumentNullException(nameof(t));
                return new MarketIndex(t);
            }

            /// <summary>Shops, market stalls, restaurants, cafés, banks, hotels, guest houses and hostels.</summary>
            public static bool Shoplike(PoiKind k)
            {
                switch (k)
                {
                    case PoiKind.Shop:
                    case PoiKind.Marketplace:
                    case PoiKind.Restaurant:
                    case PoiKind.Cafe:
                    case PoiKind.Bank:
                    case PoiKind.Hotel:
                    case PoiKind.GuestHouse:
                    case PoiKind.Hostel:
                        return true;
                    default:
                        return false;
                }
            }

            private static long Key(double x, double z)
            {
                return (long)Math.Floor(x / CellM) * 1000003L + (long)Math.Floor(z / CellM);
            }

            /// <summary>True when road <paramref name="road"/> of <paramref name="t"/> (the tile this index was built for) is a
            /// busy market street.</summary>
            public bool IsMarket(TileData t, int road)
            {
                if (!ReferenceEquals(t, _t)) throw new ArgumentException("another tile", nameof(t));
                sbyte m = _market[road];
                if (m != 0) return m > 0;
                bool market = Compute(t.Roads[road]);
                _market[road] = market ? (sbyte)1 : (sbyte)-1;
                return market;
            }

            /// <summary>Shop-like places within <see cref="ShopReachM"/> of road <paramref name="road"/>.</summary>
            public int ShopsAlong(int road)
            {
                double length;
                return Count(_t.Roads[road], out length);
            }

            private bool Compute(RoadRecord r)
            {
                double length;
                int shops = Count(r, out length);
                if (shops >= MinShops && shops * 100.0 / Math.Max(1.0, length) >= ShopsPer100M) return true;
                if (_commercial.Count == 0 || r.PointCount < 2) return false;
                // The middle of the street on commercial or marketplace land.
                double half = 0.5 * length, acc = 0, mx = r.Points[0] / 100.0, mz = r.Points[1] / 100.0;
                for (int k = 0; k + 1 < r.PointCount; k++)
                {
                    double ax = r.Points[2 * k] / 100.0, az = r.Points[2 * k + 1] / 100.0, bx = r.Points[2 * k + 2] / 100.0, bz = r.Points[2 * k + 3] / 100.0;
                    double l = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                    if (acc + l >= half && l > 1e-9)
                    {
                        double u = (half - acc) / l;
                        mx = ax + (bx - ax) * u;
                        mz = az + (bz - az) * u;
                        break;
                    }
                    acc += l;
                }
                for (int i = 0; i < _commercial.Count; i++)
                    if (AreaMask.Contains(_t.Areas[_commercial[i]], mx, mz))
                        return true;
                return false;
            }

            private int Count(RoadRecord r, out double length)
            {
                length = 0;
                if (r.PointCount < 2) return 0;
                double x0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, z1 = double.MinValue;
                for (int k = 0; k < r.PointCount; k++)
                {
                    double x = r.Points[2 * k] / 100.0, z = r.Points[2 * k + 1] / 100.0;
                    if (x < x0) x0 = x;
                    if (x > x1) x1 = x;
                    if (z < z0) z0 = z;
                    if (z > z1) z1 = z;
                    if (k > 0)
                    {
                        double dx = x - r.Points[2 * k - 2] / 100.0, dz = z - r.Points[2 * k - 1] / 100.0;
                        length += Math.Sqrt(dx * dx + dz * dz);
                    }
                }
                int count = 0;
                long i0 = (long)Math.Floor((x0 - ShopReachM) / CellM), i1 = (long)Math.Floor((x1 + ShopReachM) / CellM);
                long j0 = (long)Math.Floor((z0 - ShopReachM) / CellM), j1 = (long)Math.Floor((z1 + ShopReachM) / CellM);
                for (long i = i0; i <= i1; i++)
                    for (long j = j0; j <= j1; j++)
                    {
                        List<int> l;
                        if (!_shops.TryGetValue(i * 1000003L + j, out l)) continue;
                        for (int q = 0; q < l.Count; q++)
                        {
                            PoiRecord p = _t.Pois[l[q]];
                            if (Distance(r, p.XCm / 100.0, p.ZCm / 100.0) <= ShopReachM) count++;
                        }
                    }
                return count;
            }

            private static double Distance(RoadRecord r, double x, double z)
            {
                double best = double.MaxValue;
                for (int k = 0; k + 1 < r.PointCount; k++)
                {
                    double ax = r.Points[2 * k] / 100.0, az = r.Points[2 * k + 1] / 100.0, bx = r.Points[2 * k + 2] / 100.0, bz = r.Points[2 * k + 3] / 100.0;
                    double dx = bx - ax, dz = bz - az, l2 = dx * dx + dz * dz;
                    double u = l2 > 1e-9 ? Math.Max(0.0, Math.Min(1.0, ((x - ax) * dx + (z - az) * dz) / l2)) : 0.0;
                    double qx = ax + dx * u - x, qz = az + dz * u - z;
                    best = Math.Min(best, qx * qx + qz * qz);
                }
                return Math.Sqrt(best);
            }
        }
    }

    /// <summary>
    /// Draw levels of the parked vehicles (W2_DESIGN 5.2, retuned after the owner saw red boxes standing in a lane 10-30 m
    /// away, docs/W2_PLAYTEST_1.md #3): the near model (the moving LOD2: rounded body, baked wheels) to
    /// <see cref="NearM"/>, the block-out to <see cref="BlockM"/>, the palette box to <see cref="BoxM"/>, with per-tier
    /// caps large enough that the parked vehicles round any street corner of the sample (<see cref="ParkedPlacement"/>'s
    /// density) are all drawn with the near model within <see cref="NearM"/>. The one source for
    /// <c>DressingConfig</c>. Engine-free.
    /// </summary>
    public static class ParkedLod
    {
        /// <summary>The near model (moving LOD2) to here, the block-out to <see cref="BlockM"/>, the box to
        /// <see cref="BoxM"/>.</summary>
        public const float NearM = 45f, BlockM = 100f, BoxM = 160f;

        /// <summary>Caps per level (near, block-out) and in all for a tier (0 Low, 1 Mid, 2 High).</summary>
        public static void Caps(int tier, out int near, out int block, out int total)
        {
            switch (tier <= 0 ? 0 : tier >= 2 ? 2 : 1)
            {
                case 0:
                    near = 16;
                    block = 64;
                    total = 100;
                    return;
                case 1:
                    near = 28;
                    block = 80;
                    total = 160;
                    return;
                default:
                    near = 40;
                    block = 110;
                    total = 240;
                    return;
            }
        }
    }

    /// <summary>The areas of a tile no vehicle parks on (water, wetland, ice, sacred and heritage compounds, religious
    /// grounds, parks, pitches, stadiums, pools, golf, cemeteries, military, airfields, traffic islands, courtyards and
    /// pedestrian squares), bucketed by bounding box in 32 m cells for point tests (triangles, so holes are honoured).
    /// Engine-free.</summary>
    internal sealed class AreaMask
    {
        private const double CellM = 32.0;
        private readonly TileData _t;
        private readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();
        private readonly double[] _box;

        public AreaMask(TileData t)
        {
            _t = t;
            _box = new double[t.Areas.Count * 4];
            for (int i = 0; i < t.Areas.Count; i++)
            {
                AreaRecord a = t.Areas[i];
                if (!NoParking(a) || a.Vertices == null || a.Vertices.Length < 6) continue;
                double x0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, z1 = double.MinValue;
                for (int k = 0; k + 1 < a.Vertices.Length; k += 2)
                {
                    double x = a.Vertices[k] / 100.0, z = a.Vertices[k + 1] / 100.0;
                    if (x < x0) x0 = x;
                    if (x > x1) x1 = x;
                    if (z < z0) z0 = z;
                    if (z > z1) z1 = z;
                }
                _box[4 * i] = x0;
                _box[4 * i + 1] = z0;
                _box[4 * i + 2] = x1;
                _box[4 * i + 3] = z1;
                for (long ci = (long)Math.Floor(x0 / CellM); ci <= (long)Math.Floor(x1 / CellM); ci++)
                    for (long cj = (long)Math.Floor(z0 / CellM); cj <= (long)Math.Floor(z1 / CellM); cj++)
                    {
                        long key = ci * 1000003L + cj;
                        List<int> l;
                        if (!_cells.TryGetValue(key, out l)) _cells[key] = l = new List<int>(4);
                        l.Add(i);
                    }
            }
        }

        private static bool NoParking(AreaRecord a)
        {
            if ((a.Flags & (AreaFlags.SacredNoVehicle | AreaFlags.HeritageZone)) != 0) return true;
            switch (a.Kind)
            {
                case AreaKind.WaterLake:
                case AreaKind.WaterRiver:
                case AreaKind.WaterPond:
                case AreaKind.Glacier:
                case AreaKind.Wetland:
                case AreaKind.Religious:
                case AreaKind.Park:
                case AreaKind.Pitch:
                case AreaKind.Stadium:
                case AreaKind.Pool:
                case AreaKind.Golf:
                case AreaKind.Cemetery:
                case AreaKind.Military:
                case AreaKind.Aerodrome:
                case AreaKind.Apron:
                case AreaKind.TrafficIsland:
                case AreaKind.Courtyard:
                case AreaKind.Pedestrian:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>True when the tile-local point lies on an area no vehicle parks on.</summary>
        public bool Forbidden(double x, double z)
        {
            List<int> l;
            if (!_cells.TryGetValue((long)Math.Floor(x / CellM) * 1000003L + (long)Math.Floor(z / CellM), out l)) return false;
            for (int k = 0; k < l.Count; k++)
            {
                int i = l[k];
                if (x < _box[4 * i] || z < _box[4 * i + 1] || x > _box[4 * i + 2] || z > _box[4 * i + 3]) continue;
                if (Contains(_t.Areas[i], x, z)) return true;
            }
            return false;
        }

        /// <summary>True when the tile-local point lies in the area (its triangles; the outer ring when it has none).</summary>
        public static bool Contains(AreaRecord a, double x, double z)
        {
            int[] v = a.Vertices;
            if (v == null || v.Length < 6) return false;
            int[] tri = a.Indices;
            if (tri != null && tri.Length >= 3)
            {
                for (int k = 0; k + 2 < tri.Length; k += 3)
                {
                    double ax = v[2 * tri[k]] / 100.0, az = v[2 * tri[k] + 1] / 100.0;
                    double bx = v[2 * tri[k + 1]] / 100.0, bz = v[2 * tri[k + 1] + 1] / 100.0;
                    double cx = v[2 * tri[k + 2]] / 100.0, cz = v[2 * tri[k + 2] + 1] / 100.0;
                    double d1 = (x - bx) * (az - bz) - (ax - bx) * (z - bz);
                    double d2 = (x - cx) * (bz - cz) - (bx - cx) * (z - cz);
                    double d3 = (x - ax) * (cz - az) - (cx - ax) * (z - az);
                    bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                    if (!(neg && pos)) return true;
                }
                return false;
            }
            bool inside = false;
            int n = v.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = v[2 * i] / 100.0, zi = v[2 * i + 1] / 100.0, xj = v[2 * j] / 100.0, zj = v[2 * j + 1] / 100.0;
                if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi) inside = !inside;
            }
            return inside;
        }
    }

    /// <summary>Building footprints of a tile bucketed in 32 m cells by bounding box, for exact point-in-footprint
    /// tests (outer rings; holes count as inside, which keeps vehicles out of courtyards). Engine-free.</summary>
    public sealed class FootprintGrid
    {
        private const double CellM = 32.0;
        private readonly TileData _t;
        private readonly int _n;
        private readonly List<int>[] _cells;
        private readonly float[] _box; // minX, minZ, maxX, maxZ per building (metres)

        public FootprintGrid(TileData t)
        {
            _t = t ?? throw new ArgumentNullException(nameof(t));
            _n = Math.Max(1, (int)Math.Ceiling(t.Tile.Size / CellM));
            _cells = new List<int>[_n * _n];
            _box = new float[t.Buildings.Count * 4];
            for (int i = 0; i < t.Buildings.Count; i++)
            {
                int[] r = t.Buildings[i].Rings[0];
                float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
                for (int k = 0; k + 1 < r.Length; k += 2)
                {
                    float x = r[k] / 100f, z = r[k + 1] / 100f;
                    if (x < x0) x0 = x;
                    if (x > x1) x1 = x;
                    if (z < z0) z0 = z;
                    if (z > z1) z1 = z;
                }
                _box[4 * i] = x0;
                _box[4 * i + 1] = z0;
                _box[4 * i + 2] = x1;
                _box[4 * i + 3] = z1;
                int i0 = Clamp((int)Math.Floor(x0 / CellM)), i1 = Clamp((int)Math.Floor(x1 / CellM));
                int j0 = Clamp((int)Math.Floor(z0 / CellM)), j1 = Clamp((int)Math.Floor(z1 / CellM));
                for (int j = j0; j <= j1; j++)
                    for (int c = i0; c <= i1; c++)
                    {
                        List<int> l = _cells[j * _n + c];
                        if (l == null) _cells[j * _n + c] = l = new List<int>(4);
                        l.Add(i);
                    }
            }
        }

        private int Clamp(int i)
        {
            return i < 0 ? 0 : i >= _n ? _n - 1 : i;
        }

        /// <summary>True when the tile-local point lies inside a building's outer ring.</summary>
        public bool Inside(double x, double z)
        {
            int c = Clamp((int)Math.Floor(x / CellM)), j = Clamp((int)Math.Floor(z / CellM));
            List<int> l = _cells[j * _n + c];
            if (l == null) return false;
            for (int k = 0; k < l.Count; k++)
            {
                int i = l[k];
                if (x < _box[4 * i] || z < _box[4 * i + 1] || x > _box[4 * i + 2] || z > _box[4 * i + 3]) continue;
                if (InRing(_t.Buildings[i].Rings[0], x, z)) return true;
            }
            return false;
        }

        private static bool InRing(int[] r, double x, double z)
        {
            bool inside = false;
            int n = r.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = r[2 * i] / 100.0, zi = r[2 * i + 1] / 100.0, xj = r[2 * j] / 100.0, zj = r[2 * j + 1] / 100.0;
                if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi) inside = !inside;
            }
            return inside;
        }
    }
}
