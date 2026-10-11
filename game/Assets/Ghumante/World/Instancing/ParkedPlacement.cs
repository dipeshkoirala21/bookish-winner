using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.World.Instancing
{
    /// <summary>
    /// Parked vehicles along the streets of a tile (W2_DESIGN 5.2): motorbikes and scooters angled 60-90° to the kerb
    /// (parallel along the wall on lanes narrower than 4.5 m) at 10-25 per 100 m in old cores, 5-15 in URBAN streets
    /// and 1-4 in PERI_URBAN, bicycles at 0.5-2 per 100 m, and a few cars parked parallel on wider URBAN residential
    /// streets. Never on trunk or primary roads, dual carriageways, heritage-pedestrian or no-motor ways, footways or
    /// trails, never inside a junction cap or within 12 m of a piece end, and never on a building footprint. About 30%
    /// carry the community-fleet tag (W2-O5). Positions sit on the carriageway edge of the W2 game width
    /// (<see cref="RoadLayout"/>), so the parked row follows the drawn road. Deterministic from the way id and slot.
    /// Engine-free; runs on the tile build worker.
    /// <para><b>Heavy vehicles where they really stand</b> (<see cref="PlaceHeavy"/>): green city buses, minibuses,
    /// micros and Safa tempos line the kerb at the bus parks (every mapped bus station: Ratna Park, the Old Bus Park,
    /// Gongabu, Chabahil...), lorries, tippers and pickups at the depots and works yards (industrial land), the odd van,
    /// micro, tempo or lorry on wide service and side streets, and tractors on the lanes of the villages and farms
    /// (rural and hill land). Most carry the community-fleet tag (<see cref="HeavyFleetShare"/>; the first at every bus
    /// park always does), so the player can borrow a bus, a lorry or a tractor and try its camera angles.</para>
    /// </summary>
    public static class ParkedPlacement
    {
        private const uint PurposeDensity = 0x50524B44, PurposeSlot = 0x50524B53, PurposeKind = 0x50524B4B, PurposeFleet = 0x50524B46,
                           PurposeAngle = 0x50524B41, PurposeLivery = 0x50524B4C;

        /// <summary>Share of parked vehicles with the green community-fleet key tag.</summary>
        public const float CommunityFleetShare = 0.30f;

        /// <summary>Road lift of the ribbons (RoadOptions.LiftM default).</summary>
        public const float RoadLiftM = 0.25f;

        /// <summary>Motorbikes per 100 m of street (both sides) by area type: old core 10-25, urban 5-15, peri-urban
        /// 1-4, rural and hill 0-1.</summary>
        public static void MotorbikeDensity(AreaType a, out float min, out float max)
        {
            switch (a)
            {
                case AreaType.OldCore: min = 10f; max = 25f; return;
                case AreaType.Urban: min = 5f; max = 15f; return;
                case AreaType.PeriUrban: min = 1f; max = 4f; return;
                case AreaType.Rural:
                case AreaType.Hill:
                case AreaType.Forest: min = 0f; max = 1f; return;
                default: min = 3f; max = 8f; return;
            }
        }

        /// <summary>True for road classes that carry parked vehicles.</summary>
        public static bool ParksOn(RoadClass c)
        {
            switch (c)
            {
                case RoadClass.Secondary:
                case RoadClass.Tertiary:
                case RoadClass.Unclassified:
                case RoadClass.Residential:
                case RoadClass.LivingStreet:
                case RoadClass.Service:
                case RoadClass.Road:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Appends the tile's parked vehicles; returns how many were added. With <paramref name="roads"/> (the
        /// road mesher's options) each vehicle stands on the drawn road surface (<see cref="RoadSurfaceQuery"/>: the smoothed
        /// profile, decks and caps), else on the terrain plus the default ribbon lift.</summary>
        public static int Place(TileData t, IHeightSampler h, List<ParkedVehicle> output, RoadOptions roads = null)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (t.Roads.Count == 0) return 0;
            int before = output.Count;
            RoadLayout layout = RoadLayout.For(t);
            RoadSurfaceQuery surface = roads != null && h != null ? RoadSurfaceQuery.For(t, h, roads) : null;
            // Exact footprint tests: lanes in the old cores are 3-5 m wide, so a raster mask would have to be ~1 m fine
            // (a megabyte and ~100 ms per tile) to tell the kerb from the wall.
            var mask = new FootprintGrid(t);
            // Heavy vehicles first: where a bike's slot overlaps a bus at the bus park, the bus stays.
            PlaceHeavy(t, h, surface, layout, mask, output);
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                if (!ParksOn(r.RoadClass) || r.Points == null || r.PointCount < 2) continue;
                RoadAttrRecord a = t.RoadAttrOf(i);
                if (a.Has(RoadAttrFlags.NoMotor) || a.Has(RoadAttrFlags.HeritagePedestrian) || a.Has(RoadAttrFlags.Dual)) continue;
                if ((r.Access & Travel.Car) == 0 && (r.Access & Travel.Motorbike) == 0) continue;
                if (!Grounded(t, i)) continue;
                PlaceAlong(t, h, surface, layout, mask, i, a.Area, output);
            }
            RemoveOverlaps(output, before);
            return output.Count - before;
        }

        /// <summary>
        /// Drops every vehicle (from <paramref name="first"/> on) whose plan rectangle overlaps one kept before it: two
        /// road pieces sharing a kerb (a way split at a tile edge, roads meeting at a junction) would otherwise park two
        /// vehicles in one spot. Kerb-to-kerb spacing on one road is already kept by <see cref="KerbGapM"/>. Deterministic
        /// (placement order); a 4 m grid keeps it linear.
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
                for (long gx = cx - 1; gx <= cx + 1 && !hit; gx++)
                    for (long gz = cz - 1; gz <= cz + 1 && !hit; gz++)
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

        /// <summary>True when two parked vehicles' plan rectangles (catalogue length and width, compass yaw) overlap and
        /// they stand within 2 m of the same height (separating axes).</summary>
        public static bool Overlap(in ParkedVehicle a, in ParkedVehicle b)
        {
            if (Math.Abs(a.Y - b.Y) > 2f) return false;
            VehicleCatalogEntry ea = VehicleCatalog.At(a.Variant), eb = VehicleCatalog.At(b.Variant);
            double ya = a.YawDeg * Math.PI / 180.0, yb = b.YawDeg * Math.PI / 180.0;
            double afx = Math.Sin(ya), afz = Math.Cos(ya), bfx = Math.Sin(yb), bfz = Math.Cos(yb);
            double ha = 0.5 * ea.LengthM, wa = 0.5 * ea.WidthM, hb = 0.5 * eb.LengthM, wb = 0.5 * eb.WidthM;
            double cx = b.X - a.X, cz = b.Z - a.Z;
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
        /// Nobody parks on a deck, an embankment or in a cutting, and a vehicle placed by the terrain there would stand
        /// under the deck or inside the fill.
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

        /// <summary>Most a parked vehicle may stand off the drawn road surface under it (metres) before the slot is
        /// skipped.</summary>
        public const float SurfaceToleranceM = 0.3f;

        /// <summary>Least gap (metres) between two vehicles parked one after the other at the same kerb.</summary>
        public const double KerbGapM = 0.15;

        private static void PlaceAlong(TileData t, IHeightSampler h, RoadSurfaceQuery surface, RoadLayout layout, FootprintGrid mask, int road,
                                       AreaType area, List<ParkedVehicle> output)
        {
            RoadRecord r = t.Roads[road];
            ulong way = r.OsmWayId * 31UL + (ulong)road;
            float min, max;
            MotorbikeDensity(area, out min, out max);
            float bikes = WorldHash.Range(way, PurposeDensity, min, max);
            float cycles = area == AreaType.OldCore || area == AreaType.Urban ? WorldHash.Range(way, PurposeDensity + 1, 0.5f, 2f) : 0.5f;
            float cars = area == AreaType.Urban && (r.RoadClass == RoadClass.Residential || r.RoadClass == RoadClass.Unclassified) ? 1f : 0f;
            float perM = (bikes + cycles + cars) / 100f;
            if (perM <= 0f) return;
            double length = layout.AlongAt(road, r.PointCount - 1);
            if (length < 30) return;
            double spacing = 1.0 / perM;
            // The last vehicle placed at each kerb (its centre along the road and half its length along it), so the next
            // one at that kerb leaves a gap instead of overlapping it (angled bikes are 0.7-1.6 m along the kerb).
            double lastLeft = double.NegativeInfinity, lastRight = double.NegativeInfinity, halfLeft = 0, halfRight = 0;
            uint slot = 0;
            for (double s = 12 + WorldHash.Range(way, PurposeSlot, 0f, (float)spacing); s < length - 12; slot++)
            {
                ulong key = way * 1315423911UL + slot;
                double step = spacing * WorldHash.Range(key, PurposeSlot, 0.6f, 1.4f);
                double at = s;
                s += step;
                if (layout.InGap(road, at)) continue;
                // The drawn cross-section (the smoothed centreline, the drawn carriageway and its shift), so parked
                // vehicles line the kerb the road mesher draws, also on filleted bends.
                RoadCut cut = layout.CutAt(t, road, at);
                double px = cut.CX, pz = cut.CZ;
                double lx = cut.UX, lz = cut.UZ; // left of travel
                double tx = lz, tz = -lx;
                float half = (float)cut.Half;
                if (half < 1.5f) continue;
                double side = (WorldHash.Fnv1a(key, PurposeSlot + 7) & 1) == 0 ? 1.0 : -1.0; // +1 left kerb, −1 right kerb
                double ex = px + lx * (cut.Shift + side * half), ez = pz + lz * (cut.Shift + side * half);
                double wx = lx * side, wz = lz * side; // towards the kerb

                float pick = WorldHash.Unit(key, PurposeKind) * (bikes + cycles + cars);
                ushort variant;
                double ox, oz, fx, fz;
                if (pick < bikes || pick >= bikes + cycles && half < 3f)
                {
                    float u2 = WorldHash.Unit(key, PurposeKind + 1);
                    variant = (ushort)(u2 < 0.35f ? VehicleCatalog.Scooter : u2 < 0.92f ? VehicleCatalog.MotorbikeCommuter : u2 < 0.95f ? VehicleCatalog.MotorbikeCruiser : VehicleCatalog.EScooter);
                    if (half >= 2.25f)
                    {
                        // Angled 60-90° with the nose to the kerb.
                        double ang = WorldHash.Range(key, PurposeAngle, 60f, 90f) * Math.PI / 180.0;
                        double dir = (WorldHash.Fnv1a(key, PurposeAngle + 1) & 1) == 0 ? 1.0 : -1.0;
                        fx = tx * Math.Cos(ang) * dir + wx * Math.Sin(ang);
                        fz = tz * Math.Cos(ang) * dir + wz * Math.Sin(ang);
                        ox = ex - wx * 0.2 - fx * 1.75;
                        oz = ez - wz * 0.2 - fz * 1.75;
                    }
                    else
                    {
                        fx = tx * side;
                        fz = tz * side;
                        ox = ex - wx * 0.45 - fx * 0.65;
                        oz = ez - wz * 0.45 - fz * 0.65;
                    }
                }
                else if (pick < bikes + cycles)
                {
                    variant = VehicleCatalog.Bicycle;
                    fx = tx * side;
                    fz = tz * side;
                    ox = ex - wx * 0.4 - fx * 0.6;
                    oz = ez - wz * 0.4 - fz * 0.6;
                }
                else
                {
                    if (half < 3f) continue;
                    float u3 = WorldHash.Unit(key, PurposeKind + 2);
                    variant = (ushort)(u3 < 0.55f ? VehicleCatalog.Hatchback : u3 < 0.75f ? VehicleCatalog.Taxi : u3 < 0.9f ? VehicleCatalog.Suv : VehicleCatalog.Ev);
                    // Left-hand traffic: cars park facing the way their kerb's traffic flows.
                    fx = tx * side;
                    fz = tz * side;
                    ox = ex - wx * 1.05 - fx * 1.3;
                    oz = ez - wz * 1.05 - fz * 1.3;
                }
                if (ox < 0 || oz < 0 || ox >= t.Tile.Size || oz >= t.Tile.Size) continue;
                VehicleCatalogEntry e = VehicleCatalog.At(variant);
                // Along-road centre and half extent of the footprint (length and width projected on the road tangent).
                double cosA = Math.Abs(fx * tx + fz * tz), sinA = Math.Sqrt(Math.Max(0.0, 1.0 - cosA * cosA));
                double alongC = at + (ox - px) * tx + (oz - pz) * tz;
                double halfAlong = 0.5 * (e.LengthM * cosA + e.WidthM * sinA);
                double prevC = side > 0 ? lastLeft : lastRight, prevHalf = side > 0 ? halfLeft : halfRight;
                if (alongC - prevC < prevHalf + halfAlong + KerbGapM) continue;
                double nose = variant >= VehicleCatalog.Taxi ? 3.0 : 1.6;
                if (mask.Inside(ox, oz) || mask.Inside(ox + fx * nose, oz + fz * nose)) continue;
                float y;
                if (!h.TryHeight(t.Tile.X0 + ox, t.Tile.Z0 + oz, out y)) continue; // samplers take game metres
                y += RoadLiftM;
                RoadSurfaceHit hit;
                if (surface != null && surface.TrySample(t.Tile.X0 + ox, t.Tile.Z0 + oz, y, out hit))
                {
                    // The drawn surface near the lifted terrain, or nothing: a vehicle never stands under a deck or in fill.
                    if (hit.OnDeck || Math.Abs(hit.Y - y) >= 1.5f) continue;
                    y = hit.Y;
                }
                if (side > 0)
                {
                    lastLeft = alongC;
                    halfLeft = halfAlong;
                }
                else
                {
                    lastRight = alongC;
                    halfRight = halfAlong;
                }
                output.Add(new ParkedVehicle
                {
                    X = (float)ox, Y = y, Z = (float)oz,
                    YawDeg = (float)(Math.Atan2(fx, fz) * 180.0 / Math.PI),
                    Variant = variant,
                    Livery = e.PickLivery(WorldHash.Fnv1a(key, PurposeLivery)),
                    CommunityFleet = variant != VehicleCatalog.Bicycle && WorldHash.Unit(key, PurposeFleet) < CommunityFleetShare,
                    Id = WorldHash.Fnv1a(key, 0),
                });
            }
        }

        // ---- Heavy vehicles where they really stand ------------------------------------------------------------------

        private const uint PurposeHeavy = 0x48455659, PurposeHeavySlot = 0x48565359, PurposeHeavyKind = 0x4856594B;

        /// <summary>Share of the heavy parked vehicles with the community-fleet key tag (the first at every bus park
        /// always has it): most, so the player finds a bus, a lorry or a tractor to borrow.</summary>
        public const float HeavyFleetShare = 0.75f;

        /// <summary>Vehicles lining the kerb at a bus park (a green city bus, then a micro, a minibus or a tempo).</summary>
        public const int BusParkVehicles = 3;

        /// <summary>Bus stations closer than this to one already served are the same bus park (OSM maps every ticket
        /// counter of Gongabu or the Old Bus Park as its own station).</summary>
        public const double BusParkSpacingM = 120.0;

        /// <summary>A bus park's vehicles stand on the street pieces within this distance of it.</summary>
        public const double BusParkReachM = 70.0;

        /// <summary>Vans, micros, tempos, pickups and lorries per 100 m of wide urban street; tractors per 100 m of rural
        /// and hill lane, and of peri-urban lane.</summary>
        public const float WideStreetHeavyPer100M = 0.3f, RuralTractorPer100M = 0.5f, PeriUrbanTractorPer100M = 0.25f;

        /// <summary>Half the drawn carriageway a van, micro, tempo or lorry parked on a street needs (the street stays
        /// passable beside it); the lanes of a bus park or a works yard and a village lane (a tractor at its verge) need
        /// less: they are where these vehicles stand.</summary>
        public const double HeavyMinHalfM = 3.0, YardMinHalfM = 2.2;

        /// <summary>Parks the tile's heavy vehicles (see the class remarks); deterministic from the site and the way.</summary>
        public static void PlaceHeavy(TileData t, IHeightSampler h, RoadSurfaceQuery surface, RoadLayout layout, FootprintGrid mask,
                                      List<ParkedVehicle> output)
        {
            if (h == null) return;
            int first = output.Count;
            // 1. Bus parks: every mapped bus station (and bus-park area), one group per BusParkSpacingM.
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
            for (int k = 0; k < siteX.Count; k++)
                ParkAtSite(t, h, surface, layout, mask, siteX[k], siteZ[k], siteKey[k], SiteKind.BusPark, output, first);
            // 2. Depots and works yards: a lorry, a tipper, a tanker or a pickup by every industrial area.
            for (int i = 0; i < t.Areas.Count; i++)
            {
                AreaRecord a = t.Areas[i];
                if (a.Kind != AreaKind.Industrial || a.Vertices == null || a.Vertices.Length < 6) continue;
                double cx, cz;
                Centroid(a.Vertices, out cx, out cz);
                ParkAtSite(t, h, surface, layout, mask, cx, cz, a.OsmRef * 7UL + 3UL, SiteKind.Depot, output, first);
            }
            // 3. Wide streets (a van, a micro, a tempo, a pickup or a lorry now and then) and farm lanes (tractors: rural,
            // hill and peri-urban lanes beside fields and orchards, not the city's parks and lawns).
            List<double> farms = FarmBoxes(t);
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                if (r.Points == null || r.PointCount < 2 || !Grounded(t, i)) continue;
                RoadAttrRecord a = t.RoadAttrOf(i);
                if (a.Has(RoadAttrFlags.NoMotor) || a.Has(RoadAttrFlags.HeritagePedestrian) || (r.Access & Travel.Car) == 0) continue;
                bool rural = a.Area == AreaType.Rural || a.Area == AreaType.Hill;
                float per100;
                SiteKind kind;
                if ((rural || a.Area == AreaType.PeriUrban) && (ParksOn(r.RoadClass) || r.RoadClass == RoadClass.Track) && NearFarm(farms, r))
                {
                    per100 = rural ? RuralTractorPer100M : PeriUrbanTractorPer100M;
                    kind = SiteKind.Farm;
                }
                else if ((a.Area == AreaType.Urban || a.Area == AreaType.PeriUrban) && ParksOn(r.RoadClass) && !a.Has(RoadAttrFlags.Dual))
                {
                    per100 = WideStreetHeavyPer100M;
                    kind = SiteKind.Street;
                }
                else continue;
                double length = layout.AlongAt(i, r.PointCount - 1);
                if (length < 40) continue;
                ulong way = r.OsmWayId * 31UL + (ulong)i ^ 0x5A5A5A5AUL;
                double expect = length * per100 / 100.0;
                int count = (int)Math.Floor(expect + WorldHash.Unit(way, PurposeHeavy));
                for (int c = 0; c < count; c++)
                {
                    ulong key = way * 2654435761UL + (ulong)c;
                    double along = 15 + WorldHash.Unit(key, PurposeHeavySlot) * Math.Max(0.0, length - 30);
                    double side = (WorldHash.Fnv1a(key, PurposeHeavySlot + 1) & 1) == 0 ? 1.0 : -1.0;
                    int variant = PickHeavy(kind, key, 0);
                    TryParkHeavy(t, h, surface, layout, mask, i, along, side, variant, key, WorldHash.Unit(key, PurposeFleet) < HeavyFleetShare,
                                 kind == SiteKind.Farm ? YardMinHalfM : HeavyMinHalfM, output, first);
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

        /// <summary>Parks a site's vehicles at the kerb of the street pieces nearest it (on its side of the street),
        /// stepping along the kerb from the point nearest the site until they all stand or no place is left.</summary>
        private static void ParkAtSite(TileData t, IHeightSampler h, RoadSurfaceQuery surface, RoadLayout layout, FootprintGrid mask, double x, double z,
                                       ulong key, SiteKind kind, List<ParkedVehicle> output, int first)
        {
            int want = kind == SiteKind.BusPark ? BusParkVehicles : 1;
            // Street pieces near the site, nearest first (a bus park stands on the trunk road it opens onto too).
            var near = new List<KeyValuePair<double, int>>();
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                if (r.Points == null || r.PointCount < 2 || !Grounded(t, i) || (r.Access & Travel.Car) == 0) continue;
                if (!HeavyRoad(r.RoadClass)) continue;
                RoadAttrRecord a = t.RoadAttrOf(i);
                if (a.Has(RoadAttrFlags.NoMotor) || a.Has(RoadAttrFlags.HeritagePedestrian)) continue;
                double s, d = Nearest(r, x, z, out s);
                if (d <= BusParkReachM) near.Add(new KeyValuePair<double, int>(d + i * 1e-6, i));
            }
            near.Sort((p, q) => p.Key.CompareTo(q.Key));
            int placed = 0, slot = 0;
            for (int n = 0; n < near.Count && n < 3 && placed < want; n++)
            {
                int road = near[n].Value;
                RoadRecord r = t.Roads[road];
                double s0;
                Nearest(r, x, z, out s0);
                // From the point nearest the site outwards, on and back in 3 m steps, between the side streets (a bus park's
                // street is all junctions); the overlap test keeps them a body apart.
                for (int step = 0; step < 41 && placed < want; step++)
                {
                    double off = (step + 1) / 2 * 3.0 * ((step & 1) == 0 ? 1 : -1);
                    double along = s0 + off;
                    RoadCut cut = layout.CutAt(t, road, along);
                    double side = (x - cut.CX) * cut.UX + (z - cut.CZ) * cut.UZ >= 0 ? 1.0 : -1.0; // the site's side of the street
                    ulong slotKey = key * 31UL + (ulong)slot;
                    int variant = PickHeavy(kind, slotKey, placed);
                    bool fleet = placed == 0 || WorldHash.Unit(slotKey, PurposeFleet) < HeavyFleetShare;
                    if (TryParkHeavy(t, h, surface, layout, mask, road, along, side, variant, slotKey, fleet, YardMinHalfM, output, first)) placed++;
                    slot++;
                }
            }
        }

        /// <summary>Road classes a bus, lorry or tractor may stand on (beside the carriageway of a trunk road at a bus park
        /// too); not footways, paths, steps or pedestrian streets.</summary>
        private static bool HeavyRoad(RoadClass c)
        {
            switch (c)
            {
                case RoadClass.Trunk:
                case RoadClass.Primary:
                case RoadClass.Secondary:
                case RoadClass.Tertiary:
                case RoadClass.Unclassified:
                case RoadClass.Residential:
                case RoadClass.LivingStreet:
                case RoadClass.Service:
                case RoadClass.Road:
                case RoadClass.Track:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Distance from (x, z) to a road's polyline (tile-local metres) and the arc length of the nearest point
        /// along it (the polyline's own, close to the layout's along the road).</summary>
        private static double Nearest(RoadRecord r, double x, double z, out double along)
        {
            double best = double.MaxValue, acc = 0;
            along = 0;
            for (int k = 0; k + 1 < r.PointCount; k++)
            {
                double ax = r.Points[2 * k] / 100.0, az = r.Points[2 * k + 1] / 100.0;
                double bx = r.Points[2 * k + 2] / 100.0, bz = r.Points[2 * k + 3] / 100.0;
                double dx = bx - ax, dz = bz - az, len2 = dx * dx + dz * dz, len = Math.Sqrt(len2);
                double u = len2 > 1e-9 ? Math.Max(0.0, Math.Min(1.0, ((x - ax) * dx + (z - az) * dz) / len2)) : 0.0;
                double qx = ax + dx * u - x, qz = az + dz * u - z, d = Math.Sqrt(qx * qx + qz * qz);
                if (d < best)
                {
                    best = d;
                    along = acc + u * len;
                }
                acc += len;
            }
            return best;
        }

        /// <summary>
        /// One heavy vehicle parallel to the kerb of road <paramref name="road"/> at <paramref name="along"/> on side
        /// <paramref name="side"/> (+1 left of the piece's direction, −1 right), facing the way that kerb's traffic flows
        /// (left-hand traffic), 0.3 m off the kerb: refused in a junction gap or within its body of a piece end, on a
        /// carriageway too narrow to leave room beside it, off the tile, on a building, off the drawn road surface (a deck,
        /// an embankment) or over another parked vehicle.
        /// </summary>
        private static bool TryParkHeavy(TileData t, IHeightSampler h, RoadSurfaceQuery surface, RoadLayout layout, FootprintGrid mask, int road,
                                         double along, double side, int variant, ulong key, bool fleet, double minHalf, List<ParkedVehicle> output,
                                         int first)
        {
            VehicleCatalogEntry e = VehicleCatalog.At(variant);
            double halfLen = 0.5 * e.LengthM;
            double length = layout.AlongAt(road, t.Roads[road].PointCount - 1);
            if (along < halfLen + 8 || along > length - halfLen - 8) return false;
            if (layout.InGap(road, along) || layout.InGap(road, along - halfLen) || layout.InGap(road, along + halfLen)) return false;
            RoadCut cut = layout.CutAt(t, road, along);
            double half = cut.Half;
            if (half < minHalf) return false;
            double lx = cut.UX, lz = cut.UZ, tx = lz, tz = -lx;
            double ex = cut.CX + lx * (cut.Shift + side * half), ez = cut.CZ + lz * (cut.Shift + side * half);
            double wx = lx * side, wz = lz * side; // towards the kerb
            double fx = tx * side, fz = tz * side; // facing the kerb's traffic
            double inset = 0.5 * e.WidthM + 0.3;
            double cx = ex - wx * inset, cz = ez - wz * inset; // body centre
            double back = halfLen - e.RearOverhangM; // body centre to the rear axle
            double ox = cx - fx * back, oz = cz - fz * back; // the vehicle frame's origin: under the rear axle
            // The whole body on the tile and off every building: a 5 x 3 grid over its plan (ends, quarters and middle;
            // both sides and the centre line).
            for (int a = -2; a <= 2; a++)
                for (int w = -1; w <= 1; w++)
                {
                    double la = halfLen * a / 2.0, lw = 0.5 * e.WidthM * w;
                    double px = cx + fx * la + wx * lw, pz = cz + fz * la + wz * lw;
                    if (px < 0 || pz < 0 || px >= t.Tile.Size || pz >= t.Tile.Size || mask.Inside(px, pz)) return false;
                }
            float y;
            if (!h.TryHeight(t.Tile.X0 + ox, t.Tile.Z0 + oz, out y)) return false;
            y += RoadLiftM;
            RoadSurfaceHit hit;
            if (surface != null && surface.TrySample(t.Tile.X0 + ox, t.Tile.Z0 + oz, y, out hit))
            {
                if (hit.OnDeck || Math.Abs(hit.Y - y) >= 1.5f) return false;
                y = hit.Y;
            }
            var v = new ParkedVehicle
            {
                X = (float)ox, Y = y, Z = (float)oz,
                YawDeg = (float)(Math.Atan2(fx, fz) * 180.0 / Math.PI),
                Variant = (ushort)variant,
                Livery = e.PickLivery(WorldHash.Fnv1a(key, PurposeLivery)),
                CommunityFleet = fleet && e.PlayerDrivable,
                Id = WorldHash.Fnv1a(key, PurposeHeavy),
            };
            for (int k = first; k < output.Count; k++)
                if (Overlap(output[k], v)) return false;
            output.Add(v);
            return true;
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
