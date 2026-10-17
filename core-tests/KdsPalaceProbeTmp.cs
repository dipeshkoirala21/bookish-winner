using System;
using System.Text;
using Ghumante.Core.Data;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class KdsPalaceProbeTmp
    {
        [Test]
        public void Probe()
        {
            double ox, oz, ex, ez, nx, nz;
            WorldFrame.LonLatToGame(85.3075, 27.7045, out ox, out oz);
            WorldFrame.LonLatToGame(85.3075 + 100 / 98573.0, 27.7045, out ex, out ez);
            WorldFrame.LonLatToGame(85.3075, 27.7045 + 100 / 110574.0, out nx, out nz);
            var sb = new StringBuilder();
            sb.AppendFormat("origin game {0:0.00},{1:0.00}; east100 -> {2:0.000},{3:0.000}; north100 -> {4:0.000},{5:0.000}\n", ox, oz, ex - ox, ez - oz, nx - ox, nz - oz);
            TileId id = TileId.At(10, ox, oz);
            TileData t = StreamingSampleRegion.Tile(id);
            sb.AppendFormat("tile {0} X0 {1} Z0 {2} size {3}\n", id, id.X0, id.Z0, id.Size);
            var h = new TileHeightSampler(t);
            sb.Append("heights (local x -120..80 step 10, y 100..-110 step 10):\n");
            for (int y = 100; y >= -110; y -= 10)
            {
                for (int x = -120; x <= 80; x += 10)
                {
                    float g;
                    h.TryHeight(ox + x, oz + y, out g);
                    sb.AppendFormat("{0,7:0.0}", g);
                }
                sb.Append("  y=" + y + "\n");
            }
            sb.Append("buildings with centroid in x -130..90, y -120..110:\n");
            foreach (BuildingRecord b in t.Buildings)
            {
                int[] r = b.Rings[0];
                double cx = 0, cz = 0; int n = r.Length / 2;
                for (int i = 0; i < n; i++) { cx += r[2 * i] / 100.0; cz += r[2 * i + 1] / 100.0; }
                cx = cx / n + id.X0 - ox; cz = cz / n + id.Z0 - oz;
                if (cx < -130 || cx > 90 || cz < -120 || cz > 110) continue;
                string osm = ((b.OsmRef & 1) == 1 ? "r" : "w") + (b.OsmRef >> 1);
                sb.AppendFormat("{0} c({1:0.0},{2:0.0}) n{3} lv{4} h{5:0.0} min{6:0.0} arch {7} flags {8}\n", osm, cx, cz, n, b.Levels, b.HeightCm / 100.0, b.MinHeightCm / 100.0, b.Archetype, b.Flags);
            }
            sb.Append("roads within:\n");
            foreach (RoadRecord rr in t.Roads)
            {
                bool any = false;
                for (int i = 0; i < rr.PointCount; i++)
                {
                    double px = rr.Points[2 * i] / 100.0 + id.X0 - ox, pz = rr.Points[2 * i + 1] / 100.0 + id.Z0 - oz;
                    if (px > -110 && px < 70 && pz > -85 && pz < 75) any = true;
                }
                if (!any) continue;
                sb.AppendFormat("road w{0} class {1} pts:", rr.OsmWayId, rr.RoadClass);
                for (int i = 0; i < rr.PointCount; i++) sb.AppendFormat(" ({0:0},{1:0})", rr.Points[2 * i] / 100.0 + id.X0 - ox, rr.Points[2 * i + 1] / 100.0 + id.Z0 - oz);
                sb.Append('\n');
            }
            var js = new StringBuilder("[");
            bool first = true;
            foreach (BuildingRecord b in t.Buildings)
            {
                int[] r = b.Rings[0];
                double cx = 0, cz = 0; int n = r.Length / 2;
                for (int i = 0; i < n; i++) { cx += r[2 * i] / 100.0; cz += r[2 * i + 1] / 100.0; }
                cx = cx / n + id.X0 - ox; cz = cz / n + id.Z0 - oz;
                if (cx < -180 || cx > 140 || cz < -160 || cz > 150) continue;
                string osm = ((b.OsmRef & 1) == 1 ? "r" : "w") + (b.OsmRef >> 1);
                if (!first) js.Append(",\n");
                first = false;
                js.AppendFormat("{{\"id\":\"{0}\",\"h\":{1},\"min\":{2},\"part\":{3},\"ring\":[", osm, b.HeightCm / 100.0, b.MinHeightCm / 100.0, ((b.Flags & BuildingFlags.Part) != 0) ? "true" : "false");
                for (int i = 0; i < n; i++)
                {
                    // game metres relative to origin, then to local east/north via the inverse Jacobian.
                    double gx = r[2 * i] / 100.0 + id.X0 - ox, gz = r[2 * i + 1] / 100.0 + id.Z0 - oz;
                    double a = (ex - ox) / 100, bq = (nx - ox) / 100, c = (ez - oz) / 100, d = (nz - oz) / 100, det = a * d - bq * c;
                    double lx = (d * gx - bq * gz) / det, ly = (-c * gx + a * gz) / det;
                    js.AppendFormat("{0}[{1:0.00},{2:0.00}]", i > 0 ? "," : "", lx, ly);
                }
                js.Append("]}");
            }
            js.Append("]");
            System.IO.File.WriteAllText("/tmp/claude-0/-home-user-bookish-winner/0df0fbfc-8489-5d3a-bb29-253bc2d039f8/scratchpad/rings.json", js.ToString());
            System.IO.File.WriteAllText("/tmp/claude-0/-home-user-bookish-winner/0df0fbfc-8489-5d3a-bb29-253bc2d039f8/scratchpad/probe.txt", sb.ToString());
        }
    }
}
