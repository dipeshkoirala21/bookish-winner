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
