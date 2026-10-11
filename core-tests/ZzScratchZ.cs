using System;
using System.Collections.Generic;
using System.Linq;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Streaming;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class ZzScratchZ
    {
        [Test]
        public void Labels()
        {
            var agg = new Dictionary<string, double>();
            var cnt = new Dictionary<string, int>();
            foreach (OccluderSpot spot in WorldRenderingOccluderSpotsTests.Spots)
            {
                double gx, gz;
                WorldFrame.LonLatToGame(spot.Lon, spot.Lat, out gx, out gz);
                TileData t = DrivingData.SampleTiles()[TileId.At(10, gx, gz)];
                float sx = (float)(gx - t.Tile.X0), sz = (float)(gz - t.Tile.Z0);
                MeshingSettings settings = StreamingScene.Settings(2, 10, false);
                int step = StreamingConfig.ForTier(2).TerrainStep(t.Tile, t.Tile, t.HeightsN);
                var sampler = TileHeightSampler.ForArea(t, t.Tile, step);
                BuildingOptions o = BuildingBandTable.NearOptions(2, settings.DetailBuildings);
                for (int i = 0; i < t.Buildings.Count; i++)
                {
                    BuildingRecord b = t.Buildings[i];
                    if (b.Rings == null || b.Rings.Length == 0) continue;
                    float bx = b.Rings[0][0] / 100f, bz = b.Rings[0][1] / 100f;
                    if (Math.Abs(bx - sx) > 120 || Math.Abs(bz - sz) > 120) continue;
                    var m = new MeshData();
                    m.HasUv0 = true;
                    HouseBuilder.DebugLabels = new List<string>();
                    HouseBuilder.DebugStarts = new List<int>();
                    BuildingDetailMesher.One(t, i, sampler, o, m, null);
                    var starts = HouseBuilder.DebugStarts;
                    var labels = HouseBuilder.DebugLabels;
                    HouseBuilder.DebugLabels = null;
                    string Label(int tri)
                    {
                        int v = m.Indices[3 * tri];
                        int best = -1;
                        for (int k = 0; k < starts.Count; k++) if (starts[k] <= v && (best < 0 || starts[k] >= starts[best])) best = k;
                        return best < 0 ? "?" : labels[best] + "/" + (MaterialChannel)(int)Math.Round(m.Uv0[2 * v]);
                    }
                    CoplanarScan.Run(m, WorldRenderingZFightTests.FightM, WorldRenderingZFightTests.MinOverlapM2, 0, (a, c, area) =>
                    {
                        string la = Label(a), lc = Label(c);
                        string key = string.CompareOrdinal(la, lc) < 0 ? la + " | " + lc : lc + " | " + la;
                        agg[key] = (agg.TryGetValue(key, out double v) ? v : 0) + area;
                        cnt[key] = (cnt.TryGetValue(key, out int n) ? n : 0) + 1;
                    });
                }
            }
            foreach (var kv in agg.OrderByDescending(k => k.Value).Take(40))
                TestContext.WriteLine(kv.Value.ToString("0.00") + " m2  " + cnt[kv.Key] + " pairs  " + kv.Key);
        }
    }
}
namespace Ghumante.Core.Tests
{
    public class ZzScratchOpen
    {
        [Test]
        public void OpenEdges()
        {
            foreach (OccluderSpot spot in WorldRenderingOccluderSpotsTests.Spots)
            {
                OccluderSpotScene scene = OccluderSpotScene.Load(spot);
                MeshData m = scene.Houses;
                // Weld by position (1 mm), count edges by use.
                var key = new System.Collections.Generic.Dictionary<long, int>();
                var vid = new int[m.VertexCount];
                for (int v = 0; v < m.VertexCount; v++)
                {
                    long kx = (long)Math.Round(m.Positions[3 * v] * 1000), ky = (long)Math.Round(m.Positions[3 * v + 1] * 1000), kz = (long)Math.Round(m.Positions[3 * v + 2] * 1000);
                    long k = (kx * 73856093L) ^ (ky * 19349663L) ^ (kz * 83492791L);
                    if (!key.TryGetValue(k, out int id)) { id = key.Count; key[k] = id; }
                    vid[v] = id;
                }
                var edges = new System.Collections.Generic.Dictionary<long, int>();
                int tris = m.IndexCount / 3;
                for (int t = 0; t < tris; t++)
                    for (int e = 0; e < 3; e++)
                    {
                        int a = vid[m.Indices[3 * t + e]], b = vid[m.Indices[3 * t + (e + 1) % 3]];
                        if (a == b) continue;
                        long ek = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                        edges[ek] = (edges.TryGetValue(ek, out int c) ? c : 0) + 1;
                    }
                int open = 0, total = edges.Count;
                foreach (var kv in edges) if (kv.Value == 1) open++;
                TestContext.WriteLine(spot.Name + ": " + tris + " tris, " + total + " edges, " + open + " open edges (" + (100.0 * open / total).ToString("0.0") + "%)");
            }
        }
    }
}
