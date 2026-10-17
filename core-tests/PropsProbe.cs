using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ghumante.Core.Data;
using Ghumante.Core.Generators.Placement;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Bridges;
using Ghumante.Core.Meshing.Roads;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class PropsProbe
    {
        [Test, Explicit]
        public void Probe()
        {
            var c = CultureInfo.InvariantCulture;
            int lamps = 0, under = 0, off1 = 0, gen = 0;
            var names = new Dictionary<string, int>();
            var kinds = new Dictionary<StreetPropKind, int>();
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (t == null || !t.HasDetail) continue;
                TileHeightSampler h = TileHeightSampler.ForArea(t, id, 1);
                var props = new List<StreetProp>();
                PropPlacement.Place(t, h, props);
                IBridgeDeckQuery decks = BridgeDeckIndex.ForTile(t);
                RoadSurfaceQuery q = RoadSurfaceQuery.For(t, h, new RoadOptions());
                foreach (StreetProp p in props)
                {
                    int n;
                    kinds.TryGetValue(p.Kind, out n);
                    kinds[p.Kind] = n + 1;
                    if (!p.Osm) gen++;
                    if (p.Kind != StreetPropKind.StreetLamp) continue;
                    lamps++;
                    float dy, nx, ny, nz;
                    if (decks.TryDeck(id.X0 + p.X, id.Z0 + p.Z, p.Y + 30f, out dy, out nx, out ny, out nz) && dy - p.Y > 2f) under++;
                    RoadSurfaceHit hit;
                    if (!p.Osm && q.TrySample(id.X0 + p.X, id.Z0 + p.Z, p.Y + 0.5f, out hit) && Math.Abs(hit.Y - p.Y) > 1f) off1++;
                }
                for (int i = 0; i < t.Roads.Count; i++)
                {
                    NameRecord nr = t.Name(t.Roads[i].NameRef);
                    if (nr == null) continue;
                    string k = nr.En.Length > 0 ? nr.En : nr.Default;
                    int n;
                    names.TryGetValue(k, out n);
                    names[k] = n + 1;
                }
            }
            TestContext.WriteLine(string.Format(c, "lamps {0} under deck {1} generated-off-surface {2} generated {3}", lamps, under, off1, gen));
            foreach (var kv in kinds) TestContext.WriteLine(kv.Key + " " + kv.Value);
            foreach (var kv in names.Where(k => k.Key.IndexOf("New", StringComparison.OrdinalIgnoreCase) >= 0 || k.Key.IndexOf("Juddha", StringComparison.OrdinalIgnoreCase) >= 0 || k.Key.IndexOf("Shukra", StringComparison.OrdinalIgnoreCase) >= 0 || k.Key.IndexOf("Durbar", StringComparison.OrdinalIgnoreCase) >= 0 || k.Key.IndexOf("Bishal", StringComparison.OrdinalIgnoreCase) >= 0|| k.Key.IndexOf("Indra", StringComparison.OrdinalIgnoreCase) >= 0|| k.Key.IndexOf("Khichapokhari", StringComparison.OrdinalIgnoreCase) >= 0)) TestContext.WriteLine(kv.Key + " " + kv.Value);
        }
    }
}
namespace Ghumante.Core.Tests
{
    public class PropsProbeCounts
    {
        [Test, Explicit]
        public void Counts()
        {
            var m = new Ghumante.Core.Meshing.MeshData();
            for (int k = 0; k < Ghumante.Core.Generators.Props.StreetPropMesher.Kinds; k++)
            {
                string line = ((Ghumante.Core.Generators.Placement.StreetPropKind)k).ToString();
                for (int lod = 0; lod < 3; lod++)
                {
                    m.Clear();
                    line += " " + Ghumante.Core.Generators.Props.StreetPropMesher.Build((Ghumante.Core.Generators.Placement.StreetPropKind)k, lod, m);
                    float x0, y0, z0, x1, y1, z1;
                    m.GetBounds(out x0, out y0, out z0, out x1, out y1, out z1);
                    if (lod == 0) line += string.Format(System.Globalization.CultureInfo.InvariantCulture, " [{0:0.00},{1:0.00},{2:0.00}..{3:0.00},{4:0.00},{5:0.00}]", x0, y0, z0, x1, y1, z1);
                }
                TestContext.WriteLine(line);
            }
        }
    }
}
namespace Ghumante.Core.Tests
{
    public class PropsProbeDropped
    {
        [Test, Explicit]
        public void Dropped()
        {
            var id = new Ghumante.Core.Data.TileId(10, 520, 162);
            Ghumante.Core.Data.TileData t = StreamingSampleRegion.Tile(id);
            var h = new Ghumante.Core.Meshing.TileHeightSampler(t, 2);
            var props = new System.Collections.Generic.List<Ghumante.Core.Generators.Placement.StreetProp>();
            Ghumante.Core.Generators.Placement.PropPlacement.Place(t, h, props);
            foreach (Ghumante.Core.Data.PropRecord p in t.Props)
            {
                Ghumante.Core.Generators.Placement.StreetPropKind k;
                if (!Ghumante.Core.Generators.Placement.PropPlacement.KindOf(p.Kind, out k)) continue;
                bool found = false;
                foreach (var pp in props) if (pp.Osm && pp.OsmRef == p.OsmRef) found = true;
                if (found) continue;
                TestContext.WriteLine("dropped " + p.Kind + " " + p.OsmRef + " at " + p.XCm / 100.0 + "," + p.ZCm / 100.0 + " flags " + p.Has(Ghumante.Core.Data.PropFlags.OnRoad));
                var q = Ghumante.Core.Meshing.Roads.RoadSurfaceQuery.For(t, h, new Ghumante.Core.Meshing.RoadOptions());
                var mask = new Ghumante.Core.Generators.Placement.PlacementMask(t);
                for (int dz = -10; dz <= 10; dz += 2)
                {
                    string line = "";
                    for (int dx = -10; dx <= 10; dx += 2)
                    {
                        double x = p.XCm / 100.0 + dx, z = p.ZCm / 100.0 + dz;
                        Ghumante.Core.Meshing.Roads.RoadSurfaceHit hit;
                        char c = '.';
                        if (q.TrySample(id.X0 + x, id.Z0 + z, (h.TryHeight(x, z, out float hy) ? hy : 0f) + 0.5f, out hit)) c = (hit.OnJunction ? 'J' : hit.OnCarriageway ? 'C' : 'f');
                        else if ((mask.At(x, z) & Ghumante.Core.Generators.Placement.PlacementMask.Building) != 0) c = 'B';
                        if (dx == 0 && dz == 0) TestContext.WriteLine("centre hit " + c + " road " + (c != '.' && c != 'B' ? "y" : "n"));
                        line += c;
                    }
                    TestContext.WriteLine(line);
                }
            }
        }
    }
}
namespace Ghumante.Core.Tests
{
    public class PropsProbeSignals
    {
        /// <summary>Lists every mapped traffic signal of the sample (lon, lat) and the signal poles placed within 45 m of it.</summary>
        [Test, Explicit]
        public void Signals()
        {
            var c = System.Globalization.CultureInfo.InvariantCulture;
            foreach (Ghumante.Core.Data.TileId id in StreamingSampleRegion.TilesAt(10))
            {
                Ghumante.Core.Data.TileData t = StreamingSampleRegion.Tile(id);
                if (t == null || !t.HasDetail) continue;
                var h = Ghumante.Core.Meshing.TileHeightSampler.ForArea(t, id, 1);
                var props = new System.Collections.Generic.List<Ghumante.Core.Generators.Placement.StreetProp>();
                Ghumante.Core.Generators.Placement.PropPlacement.Place(t, h, props);
                foreach (Ghumante.Core.Data.PropRecord r in t.Props)
                {
                    if (r.Kind != Ghumante.Core.Data.ObjectKind.TrafficSignals) continue;
                    double lon, lat;
                    Ghumante.Core.Geo.WorldFrame.GameToLonLat(id.X0 + r.XCm / 100.0, id.Z0 + r.ZCm / 100.0, out lon, out lat);
                    int near = 0;
                    foreach (var p in props)
                        if (Ghumante.Core.Generators.Props.StreetPropMesher.IsSignal(p.Kind) && (p.X - r.XCm / 100.0) * (p.X - r.XCm / 100.0) + (p.Z - r.ZCm / 100.0) * (p.Z - r.ZCm / 100.0) < 45 * 45) near++;
                    TestContext.WriteLine(string.Format(c, "{0} signal {1} at {2:0.000000},{3:0.000000}: {4} poles within 45 m", id, r.OsmRef, lon, lat, near));
                }
            }
        }
    }
}
namespace Ghumante.Core.Tests
{
    public class PropsProbeSignalSides
    {
        /// <summary>How many signals stand at the traffic's left kerb (arm reaching right) and how many on the right
        /// (a median or the far kerb, mirrored arm), per kind.</summary>
        [Test, Explicit]
        public void Sides()
        {
            var counts = new System.Collections.Generic.Dictionary<Ghumante.Core.Generators.Placement.StreetPropKind, int>();
            foreach (Ghumante.Core.Data.TileId id in StreamingSampleRegion.TilesAt(10))
            {
                Ghumante.Core.Data.TileData t = StreamingSampleRegion.Tile(id);
                if (t == null || !t.HasDetail) continue;
                var h = Ghumante.Core.Meshing.TileHeightSampler.ForArea(t, id, 1);
                var props = new System.Collections.Generic.List<Ghumante.Core.Generators.Placement.StreetProp>();
                Ghumante.Core.Generators.Placement.PropPlacement.Place(t, h, props);
                foreach (var p in props)
                {
                    if (!Ghumante.Core.Generators.Props.StreetPropMesher.IsSignal(p.Kind)) continue;
                    int n;
                    counts.TryGetValue(p.Kind, out n);
                    counts[p.Kind] = n + 1;
                }
            }
            foreach (var kv in counts) TestContext.WriteLine(kv.Key + " " + kv.Value);
        }
    }
}
namespace Ghumante.Core.Tests
{
    public class PropsProbeMirrored
    {
        /// <summary>For every mirrored signal (pole on the traffic's right), what lies beyond its head on the traffic's
        /// left: the surface every 0.5 m out to 9 m (C carriageway, J junction, F footpath, M median, S shoulder,
        /// B building, . nothing drawn).</summary>
        [Test, Explicit]
        public void Mirrored()
        {
            var c = System.Globalization.CultureInfo.InvariantCulture;
            foreach (Ghumante.Core.Data.TileId id in StreamingSampleRegion.TilesAt(10))
            {
                Ghumante.Core.Data.TileData t = StreamingSampleRegion.Tile(id);
                if (t == null || !t.HasDetail) continue;
                var h = Ghumante.Core.Meshing.TileHeightSampler.ForArea(t, id, 1);
                var props = new System.Collections.Generic.List<Ghumante.Core.Generators.Placement.StreetProp>();
                Ghumante.Core.Generators.Placement.PropPlacement.Place(t, h, props);
                var q = Ghumante.Core.Meshing.Roads.RoadSurfaceQuery.For(t, h, new Ghumante.Core.Meshing.RoadOptions());
                var mask = new Ghumante.Core.Generators.Placement.PlacementMask(t);
                foreach (var p in props)
                {
                    if (!Ghumante.Core.Generators.Props.StreetPropMesher.IsMirroredSignal(p.Kind)) continue;
                    Ghumante.Core.Generators.Props.StreetPropMesher.SignalHead head;
                    Ghumante.Core.Generators.Props.StreetPropMesher.TryHangingHead(p.Kind, out head);
                    double yaw = p.YawDeg * System.Math.PI / 180, fx = System.Math.Sin(yaw), fz = System.Math.Cos(yaw);
                    string line = "";
                    for (double d = 0; d <= 12; d += 0.5)
                    {
                        double x = p.X + fx * d, z = p.Z + fz * d;
                        Ghumante.Core.Meshing.Roads.RoadSurfaceHit hit;
                        char ch = '.';
                        if (q.TrySample(id.X0 + x, id.Z0 + z, p.Y + 0.5f, out hit))
                            ch = hit.OnJunction ? 'J' : hit.OnCarriageway ? 'C' : hit.OnFootpath ? 'F' : hit.OnMedian ? 'M' : hit.OnShoulder ? 'S' : hit.OnIsland ? 'I' : '?';
                        else if ((mask.At(x, z) & Ghumante.Core.Generators.Placement.PlacementMask.Building) != 0) ch = 'B';
                        if (System.Math.Abs(d - head.ForwardM) < 0.26) ch = char.ToLowerInvariant(ch);
                        line += ch;
                    }
                    double lon, lat;
                    Ghumante.Core.Geo.WorldFrame.GameToLonLat(id.X0 + p.X, id.Z0 + p.Z, out lon, out lat);
                    TestContext.WriteLine(string.Format(c, "{0} {1} at {2:0.000000},{3:0.000000} pole->left: {4}", id, p.Kind, lon, lat, line));
                }
            }
        }
    }
}
