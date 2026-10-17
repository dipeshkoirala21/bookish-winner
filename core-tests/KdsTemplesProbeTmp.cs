using System;
using System.Globalization;
using System.IO;
using System.Text;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>TEMPORARY probe (mon-kds-temples): buildings, roads, areas and POIs of the square in the brief's local frame
    /// (origin 27.7045 N, 85.3075 E; x east, y north).</summary>
    public class KdsTemplesProbeTmp
    {
        [Test]
        public void Probe()
        {
            string dir = Environment.GetEnvironmentVariable("KDS_PROBE_DIR");
            if (string.IsNullOrEmpty(dir)) Assert.Pass("set KDS_PROBE_DIR");
            double gx, gz;
            WorldFrame.LonLatToGame(85.3075, 27.7045, out gx, out gz);
            TileId id = TileId.At(10, gx, gz);
            TileData t = StreamingSampleRegion.Tile(id);
            double ox = gx - t.Tile.X0, oz = gz - t.Tile.Z0;
            CultureInfo c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendFormat(c, "tile {0} X0 {1} Z0 {2} size {3} origin-local {4:0.###} {5:0.###}\n", id, t.Tile.X0, t.Tile.Z0, t.Tile.Size, ox, oz);
            TileHeightSampler h = TileHeightSampler.ForArea(t, id, 1);
            sb.Append("# buildings: osm idx cx cy w d bearing h min lv arch use flags | ring\n");
            for (int i = 0; i < t.Buildings.Count; i++)
            {
                BuildingRecord b = t.Buildings[i];
                int[] ring = b.Rings[0];
                int n = ring.Length / 2;
                double sx = 0, sz = 0;
                for (int k = 0; k < n; k++)
                {
                    sx += ring[2 * k] * 0.01 - ox;
                    sz += ring[2 * k + 1] * 0.01 - oz;
                }
                sx /= n;
                sz /= n;
                if (sx < -230 || sx > 60 || sz < -150 || sz > 130) continue;
                string osm = ((b.OsmRef & 1) != 0 ? "r" : "w") + (b.OsmRef >> 1).ToString(c);
                // Oriented box by the longest edge.
                double best = 0, bx = 1, bz = 0;
                for (int k = 0; k < n; k++)
                {
                    int j = (k + 1) % n;
                    double dx = (ring[2 * j] - ring[2 * k]) * 0.01, dz = (ring[2 * j + 1] - ring[2 * k + 1]) * 0.01, l = Math.Sqrt(dx * dx + dz * dz);
                    if (l > best)
                    {
                        best = l;
                        bx = dx / l;
                        bz = dz / l;
                    }
                }
                double u0 = 1e9, u1 = -1e9, w0 = 1e9, w1 = -1e9;
                for (int k = 0; k < n; k++)
                {
                    double px = ring[2 * k] * 0.01 - ox - sx, pz = ring[2 * k + 1] * 0.01 - oz - sz;
                    double u = px * bx + pz * bz, w = -px * bz + pz * bx;
                    u0 = Math.Min(u0, u);
                    u1 = Math.Max(u1, u);
                    w0 = Math.Min(w0, w);
                    w1 = Math.Max(w1, w);
                }
                double bearing = (Math.Atan2(bx, bz) * 180 / Math.PI + 360) % 180;
                float gy;
                h.TryHeight(t.Tile.X0 + sx + ox, t.Tile.Z0 + sz + oz, out gy);
                sb.AppendFormat(c, "{0}\t#{1}\t{2:0.0}\t{3:0.0}\t{4:0.0}x{5:0.0}\tb{6:0.0}\th{7:0.0}\tmin{8:0.0}\tlv{9}\t{10}\t{11}\t{12}\tg{13:0.00}\t|", osm, i, sx, sz, u1 - u0,
                                w1 - w0, bearing, b.HeightCm / 100.0, b.MinHeightCm / 100.0, b.Levels, b.Archetype, b.Use, b.Flags, gy);
                for (int k = 0; k < n; k++) sb.AppendFormat(c, " {0:0.00},{1:0.00}", ring[2 * k] * 0.01 - ox, ring[2 * k + 1] * 0.01 - oz);
                sb.Append('\n');
            }
            sb.Append("# roads: way class width layer flags | points\n");
            foreach (RoadRecord r in t.Roads)
            {
                bool inside = false;
                for (int k = 0; k < r.PointCount; k++)
                {
                    double px = r.Points[2 * k] * 0.01 - ox, pz = r.Points[2 * k + 1] * 0.01 - oz;
                    if (px > -230 && px < 60 && pz > -150 && pz < 130) inside = true;
                }
                if (!inside) continue;
                sb.AppendFormat(c, "w{0}\t{1}\t{2:0.0}\tL{3}\t{4}\t{5}\t|", r.OsmWayId, r.RoadClass, r.WidthCm / 100.0, r.Layer, r.Flags, r.Surface);
                for (int k = 0; k < r.PointCount; k++) sb.AppendFormat(c, " {0:0.0},{1:0.0}", r.Points[2 * k] * 0.01 - ox, r.Points[2 * k + 1] * 0.01 - oz);
                sb.Append('\n');
            }
            sb.Append("# areas: osm kind | first ring bbox\n");
            foreach (AreaRecord a in t.Areas)
            {
                double x0 = 1e9, x1 = -1e9, z0 = 1e9, z1 = -1e9;
                for (int k = 0; k < a.Vertices.Length / 2; k++)
                {
                    double px = a.Vertices[2 * k] * 0.01 - ox, pz = a.Vertices[2 * k + 1] * 0.01 - oz;
                    x0 = Math.Min(x0, px);
                    x1 = Math.Max(x1, px);
                    z0 = Math.Min(z0, pz);
                    z1 = Math.Max(z1, pz);
                }
                if (x1 < -230 || x0 > 60 || z1 < -150 || z0 > 130) continue;
                sb.AppendFormat(c, "{0}\t{1}\t{2}\tbbox {3:0.0},{4:0.0} .. {5:0.0},{6:0.0}\n", a.OsmRef, a.Kind, a.Flags, x0, z0, x1, z1);
            }
            sb.Append("# pois\n");
            foreach (PoiRecord p in t.Pois)
            {
                double px = p.XCm / 100.0 - ox, pz = p.ZCm / 100.0 - oz;
                if (px < -230 || px > 60 || pz < -150 || pz > 130) continue;
                ulong r = p.OsmRef;
                string osm = ((r & 3) == 0 ? "n" : (r & 3) == 1 ? "w" : "r") + (r >> 2).ToString(c);
                sb.AppendFormat(c, "{0}\t{1:0.0},{2:0.0}\t{3}\t{4}\n", osm, px, pz, p.Kind, p.NameRef >= 0 && p.NameRef < t.Names.Count ? t.Names[p.NameRef].Default : "");
            }
            sb.Append("# ground heights every 10 m (x -200..40, y -130..110)\n");
            for (int yy = 110; yy >= -130; yy -= 10)
            {
                sb.AppendFormat(c, "{0,5}:", yy);
                for (int xx = -200; xx <= 40; xx += 10)
                {
                    float gy;
                    h.TryHeight(t.Tile.X0 + xx + ox, t.Tile.Z0 + yy + oz, out gy);
                    sb.AppendFormat(c, " {0:0.0}", gy);
                }
                sb.Append('\n');
            }
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "probe.txt"), sb.ToString());
        }
    }
}
