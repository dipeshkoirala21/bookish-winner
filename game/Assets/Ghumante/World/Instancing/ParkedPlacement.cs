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
                float pitch = 0f, roll = 0f;
                if (surface != null && surface.TrySample(t.Tile.X0 + ox, t.Tile.Z0 + oz, y, out hit))
                {
                    // The drawn surface near the lifted terrain, or nothing: a vehicle never stands under a deck or in fill.
                    if (hit.OnDeck || Math.Abs(hit.Y - y) >= 1.5f) continue;
                    y = hit.Y;
                    // Every wheel on the drawn surface of a sloping street (cut/fill package): pitch and roll to it; a
                    // vehicle with a wheel over the road's edge (a drop, a cutting) is not parked there.
                    if (!Pose(surface, t, VehicleMesher.Wheels(e), ox, oz, fx, fz, ref y, out pitch, out roll)) continue;
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
                    YawDeg = (float)(Math.Atan2(fx, fz) * 180.0 / Math.PI), PitchDeg = pitch, RollDeg = roll,
                    Variant = variant,
                    Livery = e.PickLivery(WorldHash.Fnv1a(key, PurposeLivery)),
                    CommunityFleet = variant != VehicleCatalog.Bicycle && WorldHash.Unit(key, PurposeFleet) < CommunityFleetShare,
                    Id = WorldHash.Fnv1a(key, 0),
                });
            }
        }

        /// <summary>
        /// The pose that stands a parked vehicle's wheels on the drawn road surface (cut/fill package, owner playtest at
        /// Gaurighat: "half of the motorcycle is in the air"): the surface under each wheel (<paramref name="wheels"/>,
        /// right/forward from the origin at the rear axle) fitted with a plane, whose height at the origin becomes
        /// <paramref name="y"/> and whose fall along and across the vehicle become its pitch (Unity Euler x, positive noses
        /// down) and roll (Euler z, positive raises the right side). Two-wheelers stand upright on their stand (no roll).
        /// False when a wheel has no road surface under it within 0.6 m (it would hang over the road's edge).
        /// </summary>
        internal static bool Pose(RoadSurfaceQuery surface, TileData t, WheelSocket[] wheels, double ox, double oz, double fx, double fz, ref float y,
                                  out float pitchDeg, out float rollDeg)
        {
            pitchDeg = rollDeg = 0f;
            if (wheels == null || wheels.Length < 2) return true;
            double rx = fz, rz = -fx;
            // Least squares plane h = c + a * forward + b * right through the surface under the wheels.
            double s1 = 0, sf = 0, sr = 0, sff = 0, sfr = 0, srr = 0, sh = 0, sfh = 0, srh = 0;
            float minR = float.MaxValue, maxR = float.MinValue;
            foreach (WheelSocket w in wheels)
            {
                double wx = t.Tile.X0 + ox + fx * w.Z + rx * w.X, wz = t.Tile.Z0 + oz + fz * w.Z + rz * w.X;
                RoadSurfaceHit hit;
                if (!surface.TrySample(wx, wz, y, out hit) || hit.OnDeck || Math.Abs(hit.Y - y) >= 0.6f) return false;
                double h = hit.Y;
                s1 += 1;
                sf += w.Z;
                sr += w.X;
                sff += w.Z * w.Z;
                sfr += w.Z * w.X;
                srr += w.X * w.X;
                sh += h;
                sfh += w.Z * h;
                srh += w.X * h;
                minR = Math.Min(minR, w.X);
                maxR = Math.Max(maxR, w.X);
            }
            bool track = maxR - minR > 0.3f; // a car's, a tempo's or a rickshaw's wheels span a track; a bike's do not
            double c, a, b = 0;
            if (track)
            {
                // Cramer's rule on the 3 x 3 normal equations.
                double det = s1 * (sff * srr - sfr * sfr) - sf * (sf * srr - sfr * sr) + sr * (sf * sfr - sff * sr);
                if (Math.Abs(det) < 1e-9) return true;
                c = (sh * (sff * srr - sfr * sfr) - sf * (sfh * srr - sfr * srh) + sr * (sfh * sfr - sff * srh)) / det;
                a = (s1 * (sfh * srr - sfr * srh) - sh * (sf * srr - sfr * sr) + sr * (sf * srh - sfh * sr)) / det;
                b = (s1 * (sff * srh - sfh * sfr) - sf * (sf * srh - sfh * sr) + sh * (sf * sfr - sff * sr)) / det;
            }
            else
            {
                double det = s1 * sff - sf * sf;
                if (Math.Abs(det) < 1e-9) return true;
                a = (s1 * sfh - sf * sh) / det;
                c = (sh - a * sf) / s1;
            }
            y = (float)c;
            pitchDeg = (float)(-Math.Atan(a) * 180.0 / Math.PI);
            rollDeg = (float)(Math.Atan(b) * 180.0 / Math.PI);
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
