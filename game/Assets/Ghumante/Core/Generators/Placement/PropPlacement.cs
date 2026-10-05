using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Generators.Placement
{
    /// <summary>A street prop kind for the instancing layer.</summary>
    public enum StreetPropKind : byte
    {
        StreetLamp = 0,
        BusStop = 1,
        BusShelter = 2,
        TrafficSignal = 3,
        Gate = 4,
        StorageTank = 5,
        Bench = 6,
        WaterTap = 7,
        Windsock = 8,
        Helipad = 9,
        TaxiStand = 10,
        AerowayGate = 11,
        ParkingPosition = 12,
        Mast = 13,
        Artwork = 14,
        PowerTower = 15,
        PowerPole = 16,
        Well = 17,
        SolarPanel = 18,
        Chimney = 19,
    }

    /// <summary>One placed street prop (tile-local metres, absolute Y; yaw clockwise from north).</summary>
    public struct StreetProp
    {
        public float X, Y, Z, YawDeg, HeightM;
        public StreetPropKind Kind;

        /// <summary>True for real OSM objects (PROP); false for generated ones (labelled procedural).</summary>
        public bool Osm;

        public ulong OsmRef;
    }

    /// <summary>
    /// Street props (W2_DESIGN 4.5, 4.7, 2.6): every real PROP object at its position and yaw (lamps, stops, signals,
    /// gates, tanks, aeroway objects; trees are <see cref="TreePlacement"/>'s), then generated street lamps where OSM
    /// has none within 15 m: arterial 10-12 m poles every 30-35 m, local 9-10 m every 25-30 m, alternating sides at
    /// the kerb (on the footpath when there is one), never in old cores (shared lanes) and never on a road or
    /// building. Signal heads at JNCT SIGNALS junctions without OSM signals. Deterministic. Returns the props added.
    /// </summary>
    public static class PropPlacement
    {
        private const uint PurposeLamp = 0x4C414D50;

        public static int Place(TileData t, IHeightSampler h, List<StreetProp> output)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (output == null) throw new ArgumentNullException(nameof(output));
            int before = output.Count;
            var g = new RoadSurface(t, h);
            var osmLamps = new List<double>();
            bool anySignals = false;
            foreach (PropRecord p in t.Props)
            {
                StreetPropKind kind;
                if (!KindOf(p.Kind, out kind)) continue;
                double x = p.XCm / 100.0, z = p.ZCm / 100.0;
                float hgt = p.HeightDm > 0 ? p.HeightDm / 10f : DefaultHeight(kind);
                output.Add(new StreetProp { X = (float)x, Y = g.Height(x, z), Z = (float)z, YawDeg = p.YawDeg, HeightM = hgt, Kind = kind, Osm = true, OsmRef = p.OsmRef });
                if (kind == StreetPropKind.StreetLamp)
                {
                    osmLamps.Add(x);
                    osmLamps.Add(z);
                }
                if (kind == StreetPropKind.TrafficSignal) anySignals = true;
            }
            if (t.Roads.Count == 0) return output.Count - before;
            RoadLayout layout = RoadLayout.For(t);
            var mask = new PlacementMask(t);
            for (int ri = 0; ri < t.Roads.Count; ri++)
            {
                RoadRecord r = t.Roads[ri];
                if (!RoadWidthModel.IsMotor(r.RoadClass) || r.RoadClass == RoadClass.Track || (r.Flags & RoadFlags.Tunnel) != 0) continue;
                AreaType area = RoadWidthModel.AreaOf(layout.Attrs[ri]);
                if (area == AreaType.OldCore || area == AreaType.Rural || area == AreaType.Forest || area == AreaType.Hill) continue;
                bool arterial = RoadWidthModel.IsMajor(r.RoadClass);
                var rng = new GrammarRng(GrammarRng.Mix((uint)r.OsmWayId, (uint)(r.OsmWayId >> 32)), PurposeLamp);
                double spacing = arterial ? rng.Range(30f, 35f) : rng.Range(25f, 30f);
                float pole = arterial ? rng.Range(10f, 12f) : rng.Range(9f, 10f);
                double length = layout.Profiles[ri].LengthM;
                int side = (r.OsmWayId & 1) == 0 ? 1 : -1;
                for (double s = 0.5 * spacing; s < length; s += spacing, side = -side)
                {
                    if (layout.InGap(ri, s)) continue;
                    RoadCut c = layout.CutAt(t, ri, s);
                    RoadProfile p = layout.ProfileAt(ri, s);
                    double foot = side > 0 ? p.FootpathLeftM : p.FootpathRightM;
                    double off = c.Shift + side * (c.Half + (foot > 0 ? 0.6 : 0.8 + p.ShoulderM));
                    double x = c.CX + c.UX * off, z = c.CZ + c.UZ * off;
                    if ((mask.At(x, z) & PlacementMask.Building) != 0 || x < 0 || z < 0 || x >= t.Tile.Size || z >= t.Tile.Size) continue;
                    if (NearAny(osmLamps, x, z, 15.0)) continue;
                    double yaw = Math.Atan2(-side * c.UX, -side * c.UZ) * 180 / Math.PI; // the arm reaches over the road
                    output.Add(new StreetProp
                    {
                        X = (float)x, Y = g.Height(x, z), Z = (float)z, YawDeg = (float)(yaw < 0 ? yaw + 360 : yaw), HeightM = pole,
                        Kind = StreetPropKind.StreetLamp,
                    });
                }
            }
            if (!anySignals)
            {
                foreach (JunctionRecord j in t.Junctions)
                {
                    if (j.Kind != JunctionKind.Signals) continue;
                    double x = j.XCm / 100.0 + 6, z = j.ZCm / 100.0 + 6;
                    output.Add(new StreetProp { X = (float)x, Y = g.Height(x, z), Z = (float)z, HeightM = 4.5f, Kind = StreetPropKind.TrafficSignal });
                }
            }
            return output.Count - before;
        }

        private static bool NearAny(List<double> pts, double x, double z, double r)
        {
            for (int i = 0; i + 1 < pts.Count; i += 2)
                if ((pts[i] - x) * (pts[i] - x) + (pts[i + 1] - z) * (pts[i + 1] - z) < r * r) return true;
            return false;
        }

        private static float DefaultHeight(StreetPropKind k)
        {
            switch (k)
            {
                case StreetPropKind.StreetLamp: return 9.5f;
                case StreetPropKind.TrafficSignal: return 4.5f;
                case StreetPropKind.BusShelter: return 2.6f;
                case StreetPropKind.StorageTank: return 1.4f;
                case StreetPropKind.Mast: return 25f;
                case StreetPropKind.PowerTower: return 30f;
                case StreetPropKind.PowerPole: return 9f;
                case StreetPropKind.Windsock: return 6f;
                case StreetPropKind.Chimney: return 20f;
                default: return 2f;
            }
        }

        /// <summary>The street-prop kind of a PROP object kind; false for kinds this layer does not place (trees).</summary>
        public static bool KindOf(ObjectKind k, out StreetPropKind kind)
        {
            switch (k)
            {
                case ObjectKind.StreetLamp: kind = StreetPropKind.StreetLamp; return true;
                case ObjectKind.BusStop: kind = StreetPropKind.BusStop; return true;
                case ObjectKind.Shelter: kind = StreetPropKind.BusShelter; return true;
                case ObjectKind.TrafficSignals: kind = StreetPropKind.TrafficSignal; return true;
                case ObjectKind.Gate: kind = StreetPropKind.Gate; return true;
                case ObjectKind.StorageTank: kind = StreetPropKind.StorageTank; return true;
                case ObjectKind.Bench: kind = StreetPropKind.Bench; return true;
                case ObjectKind.WaterTap: kind = StreetPropKind.WaterTap; return true;
                case ObjectKind.Well: kind = StreetPropKind.Well; return true;
                case ObjectKind.Windsock: kind = StreetPropKind.Windsock; return true;
                case ObjectKind.Helipad: kind = StreetPropKind.Helipad; return true;
                case ObjectKind.TaxiStand: kind = StreetPropKind.TaxiStand; return true;
                case ObjectKind.AerowayGate: kind = StreetPropKind.AerowayGate; return true;
                case ObjectKind.ParkingPosition: kind = StreetPropKind.ParkingPosition; return true;
                case ObjectKind.Mast:
                case ObjectKind.Tower: kind = StreetPropKind.Mast; return true;
                case ObjectKind.Artwork: kind = StreetPropKind.Artwork; return true;
                case ObjectKind.PowerTower: kind = StreetPropKind.PowerTower; return true;
                case ObjectKind.PowerPole: kind = StreetPropKind.PowerPole; return true;
                case ObjectKind.SolarPanel: kind = StreetPropKind.SolarPanel; return true;
                case ObjectKind.Chimney: kind = StreetPropKind.Chimney; return true;
                default: kind = StreetPropKind.StreetLamp; return false;
            }
        }
    }
}
