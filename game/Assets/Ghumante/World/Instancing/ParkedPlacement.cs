using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;

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

        /// <summary>Appends the tile's parked vehicles; returns how many were added.</summary>
        public static int Place(TileData t, IHeightSampler h, List<ParkedVehicle> output)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (t.Roads.Count == 0) return 0;
            int before = output.Count;
            RoadLayout layout = RoadLayout.For(t);
            // Exact footprint tests: lanes in the old cores are 3-5 m wide, so a raster mask would have to be ~1 m fine
            // (a megabyte and ~100 ms per tile) to tell the kerb from the wall.
            var mask = new FootprintGrid(t);
            for (int i = 0; i < t.Roads.Count; i++)
            {
                RoadRecord r = t.Roads[i];
                if (!ParksOn(r.RoadClass) || r.Points == null || r.PointCount < 2) continue;
                RoadAttrRecord a = t.RoadAttrOf(i);
                if (a.Has(RoadAttrFlags.NoMotor) || a.Has(RoadAttrFlags.HeritagePedestrian) || a.Has(RoadAttrFlags.Dual)) continue;
                if ((r.Access & Travel.Car) == 0 && (r.Access & Travel.Motorbike) == 0) continue;
                PlaceAlong(t, h, layout, mask, i, a.Area, output);
            }
            return output.Count - before;
        }

        private static void PlaceAlong(TileData t, IHeightSampler h, RoadLayout layout, FootprintGrid mask, int road, AreaType area,
                                       List<ParkedVehicle> output)
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
            int seg = 0;
            uint slot = 0;
            for (double s = 12 + WorldHash.Range(way, PurposeSlot, 0f, (float)spacing); s < length - 12; slot++)
            {
                ulong key = way * 1315423911UL + slot;
                double step = spacing * WorldHash.Range(key, PurposeSlot, 0.6f, 1.4f);
                double at = s;
                s += step;
                if (layout.InGap(road, at)) continue;
                while (seg + 2 < r.PointCount && layout.AlongAt(road, seg + 1) < at) seg++;
                double s0 = layout.AlongAt(road, seg), s1 = layout.AlongAt(road, seg + 1);
                double x0 = r.Points[2 * seg] / 100.0, z0 = r.Points[2 * seg + 1] / 100.0;
                double x1 = r.Points[2 * seg + 2] / 100.0, z1 = r.Points[2 * seg + 3] / 100.0;
                double dx = x1 - x0, dz = z1 - z0, len = Math.Sqrt(dx * dx + dz * dz);
                if (len < 1e-3) continue;
                double u = s1 > s0 ? (at - s0) / (s1 - s0) : 0;
                double px = x0 + dx * u, pz = z0 + dz * u;
                double tx = dx / len, tz = dz / len;
                double lx = -tz, lz = tx; // left of travel
                RoadProfile prof = layout.ProfileAt(road, at);
                float half = 0.5f * prof.CarriagewayM;
                if (half < 1.5f) continue;
                double side = (WorldHash.Fnv1a(key, PurposeSlot + 7) & 1) == 0 ? 1.0 : -1.0; // +1 left kerb, −1 right kerb
                double ex = px + lx * (prof.CentreShiftM + side * half), ez = pz + lz * (prof.CentreShiftM + side * half);
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
                double nose = variant >= VehicleCatalog.Taxi ? 3.0 : 1.6;
                if (mask.Inside(ox, oz) || mask.Inside(ox + fx * nose, oz + fz * nose)) continue;
                float y;
                if (!h.TryHeight(t.Tile.X0 + ox, t.Tile.Z0 + oz, out y)) continue; // samplers take game metres
                VehicleCatalogEntry e = VehicleCatalog.At(variant);
                output.Add(new ParkedVehicle
                {
                    X = (float)ox, Y = y + RoadLiftM, Z = (float)oz,
                    YawDeg = (float)(Math.Atan2(fx, fz) * 180.0 / Math.PI),
                    Variant = variant,
                    Livery = e.PickLivery(WorldHash.Fnv1a(key, PurposeLivery)),
                    CommunityFleet = variant != VehicleCatalog.Bicycle && WorldHash.Unit(key, PurposeFleet) < CommunityFleetShare,
                    Id = WorldHash.Fnv1a(key, 0),
                });
            }
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
