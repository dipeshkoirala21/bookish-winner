using System;
using System.Globalization;
using System.Text;
using Ghumante.Core.Data;
using Ghumante.Core.Generators;
using Ghumante.Core.Generators.Sacred;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>Scratch diagnostics (not committed).</summary>
    public class ZzCutfillScratch2
    {
        [Test]
        public void Kastha()
        {
            if (Environment.GetEnvironmentVariable("ZZ_KASTHA") == null) Assert.Pass();
            var ic = CultureInfo.InvariantCulture;
            HeroRecipe kr;
            HeroCatalog.TryGet("her.ktm.kasthamandap", out kr);
            TileData tile = null;
            double x = 0, z = 0;
            int building = -1;
            HeritageRecord rec = HeroCatalog.ToRecord(kr);
            HeroRecipe merged = HeroBuilder.Merge(rec);
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (t != null && HeroBuilder.TryLocate(rec, merged, t, out x, out z, out building)) { tile = t; break; }
            }
            var km = new MeshData();
            HeroBuilder.Build(rec, tile, 0, km, null, new SacredStats());
            var h1 = new TileHeightSampler(tile);
            var h2 = new TileHeightSampler(tile, 2);
            SacredClearance c1 = SacredClearance.For(tile, h1), c2 = SacredClearance.For(tile, h2);
            var sb = new StringBuilder();
            sb.AppendLine(tile.Tile + " at " + x.ToString("0.0", ic) + "," + z.ToString("0.0", ic));
            int shown = 0;
            for (int v = 0; v < km.VertexCount && shown < 12; v++)
            {
                double px = km.Positions[3 * v], py = km.Positions[3 * v + 1], pz = km.Positions[3 * v + 2];
                if (c2.Distance(px, pz) >= 0) continue;
                double g2 = c2.Ground(px, pz), g1 = c1.Ground(px, pz);
                if (py - g2 >= 4.49 || py - g2 <= 2.0) continue;
                shown++;
                float n1, n2, b; double gx, gz;
                h1.TryNaturalHeight(tile.Tile.X0 + px, tile.Tile.Z0 + pz, out n1);
                h2.TryNaturalHeight(tile.Tile.X0 + px, tile.Tile.Z0 + pz, out n2);
                bool e1 = h1.Carve != null && h1.Carve.ExtentAt(px, pz, out b, out gx, out gz);
                bool e2 = h2.Carve != null && h2.Carve.ExtentAt(px, pz, out b, out gx, out gz);
                sb.AppendLine(string.Format(ic, "v {0} at {1:0.00},{2:0.00} y {3:0.000}: g1 {4:0.000} g2 {5:0.000} nat1 {6:0.000} nat2 {7:0.000} ext1 {8} ext2 {9} d {10:0.00} minHang1 {11:0.000}",
                    v, px, pz, py, g1, g2, n1, n2, e1, e2, c2.Distance(px, pz), c1.MinHangY(px, pz, g1)));
            }
            TestContext.Progress.WriteLine(sb.ToString());
        }
    }
}
namespace Ghumante.Core.Tests
{
    public class ZzCutfillScratch3
    {
        [Test]
        public void BusBoom()
        {
            if (Environment.GetEnvironmentVariable("ZZ_BUS") == null) Assert.Pass();
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var c = CharactersCameraRideTests.Rides[2];
            var frames = CharactersCameraRideTests.Record(c);
            var diag = new System.Collections.Generic.List<string>();
            var got = CharactersCameraRideTests.RunNew(c, frames, 2400f / 1080f, null, diag);
            int worst = -1;
            double ws = 0;
            for (int i = 1; i < got.Count; i++)
            {
                double st = Math.Abs(got[i].BoomM - got[i - 1].BoomM);
                if (st > ws) { ws = st; worst = i; }
            }
            var sb = new System.Text.StringBuilder();
            for (int i = Math.Max(0, worst - 6); i < Math.Min(got.Count, worst + 4); i++)
            {
                double lon, lat;
                Ghumante.Core.Geo.WorldFrame.GameToLonLat(frames[i].X, frames[i].Z, out lon, out lat);
                sb.AppendLine(string.Format(ic, "{0}: car {1:0.00},{2:0.00},{3:0.00} ({4:0.00000},{5:0.00000}) cam {6:0.00},{7:0.00},{8:0.00} boom {9:0.00} | {10}", i, frames[i].X, frames[i].Y, frames[i].Z,
                    lat, lon, got[i].X, got[i].Y, got[i].Z, got[i].BoomM, diag[i]));
            }
            TestContext.Progress.WriteLine(sb.ToString());
        }
    }
}
namespace Ghumante.Core.Tests
{
    public class ZzCutfillScratch4
    {
        [Test]
        public void WayMid()
        {
            string spec = Environment.GetEnvironmentVariable("ZZ_WAY"); // tx,ty,wayid
            if (spec == null) Assert.Pass();
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            string[] q = spec.Split(',');
            var id = new TileId(10, int.Parse(q[0]), int.Parse(q[1]));
            ulong way = ulong.Parse(q[2]);
            TileData t = StreamingSampleRegion.Tile(id);
            var s = new TileHeightSampler(t, 2);
            var o = new RoadOptions { WidthModel = false };
            var sb = new System.Text.StringBuilder();
            foreach (RoadRecord r in t.Roads)
            {
                if (r.OsmWayId != way) continue;
                var c = new TileData { Tile = t.Tile, HeightsN = t.HeightsN, HeightsQ = t.HeightsQ, BiomesN = t.BiomesN, Biomes = t.Biomes, Flags = t.Flags };
                c.Roads.Add(r);
                var pm = new MeshData();
                RoadMesher.Build(c, s, o, pm);
                float lift = RoadMesher.LiftOf(r, o);
                sb.AppendLine("way " + way + " " + r.RoadClass + " lift " + lift);
                int shown = 0;
                for (int tri = 0; tri < pm.TriangleCount && shown < 12; tri++)
                    for (int k = 0; k < 3; k++)
                    {
                        int a = pm.Indices[3 * tri + k], b = pm.Indices[3 * tri + (k + 1) % 3];
                        double ax = pm.Positions[3 * a], az = pm.Positions[3 * a + 2], bx = pm.Positions[3 * b], bz = pm.Positions[3 * b + 2];
                        double mx = 0.5 * (ax + bx), mz = 0.5 * (az + bz), my = 0.5 * (pm.Positions[3 * a + 1] + pm.Positions[3 * b + 1]);
                        float hm;
                        s.TryHeightClamped(id.X0 + mx, id.Z0 + mz, out hm);
                        double vm = my - hm - lift;
                        if (Math.Abs(vm) < 1e-3) continue;
                        shown++;
                        float b0, b1, b2; double gx, gz;
                        bool ea = s.Carve.ExtentAt(ax, az, out b0, out gx, out gz), eb = s.Carve.ExtentAt(bx, bz, out b1, out gx, out gz), em = s.Carve.ExtentAt(mx, mz, out b2, out gx, out gz);
                        sb.AppendLine(string.Format(ic, "tri {0} edge {1:0.000},{2:0.000} -> {3:0.000},{4:0.000}: vm {5:0.0000} ext a {6} {7:0.000} b {8} {9:0.000} m {10} {11:0.000} ya {12:0.000} yb {13:0.000} hm {14:0.000}",
                            tri, ax, az, bx, bz, vm, ea, b0, eb, b1, em, b2, pm.Positions[3 * a + 1], pm.Positions[3 * b + 1], hm));
                    }
            }
            TestContext.Progress.WriteLine(sb.ToString());
        }
    }
}
namespace Ghumante.Core.Tests
{
    public class ZzCutfillScratch5
    {
        [Test]
        public void BarriersNear()
        {
            string spec = Environment.GetEnvironmentVariable("ZZ_BARR"); // gx,gz,r
            if (spec == null) Assert.Pass();
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            string[] q = spec.Split(',');
            double gx = double.Parse(q[0], ic), gz = double.Parse(q[1], ic), rad = double.Parse(q[2], ic);
            var sb = new System.Text.StringBuilder();
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                if (gx < id.X0 - rad || gx > id.X0 + id.Size + rad || gz < id.Z0 - rad || gz > id.Z0 + id.Size + rad) continue;
                TileData t = StreamingSampleRegion.Tile(id);
                if (t == null) continue;
                var h = new TileHeightSampler(t, 1);
                if (h.Carve == null) continue;
                var list = new System.Collections.Generic.List<CarveBarrier>();
                h.Carve.CopyBarriers(list);
                foreach (CarveBarrier b in list)
                {
                    double mx = 0.5 * (b.AX + b.BX), mz = 0.5 * (b.AZ + b.BZ);
                    if ((mx - gx) * (mx - gx) + (mz - gz) * (mz - gz) > rad * rad) continue;
                    sb.AppendLine(string.Format(ic, "{0} {1} {2:0.0},{3:0.0} -> {4:0.0},{5:0.0} y {6:0.00}..{7:0.00}", id, b.Wall ? "WALL" : "rail", b.AX, b.AZ, b.BX, b.BZ, b.Bottom, b.Top));
                }
            }
            TestContext.Progress.WriteLine(sb.ToString());
        }
    }
}
namespace Ghumante.Core.Tests
{
    public class ZzCutfillScratch6
    {
        [Test]
        public void TerrainAround()
        {
            string spec = Environment.GetEnvironmentVariable("ZZ_TERR"); // gx,gz,r,y
            if (spec == null) Assert.Pass();
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            string[] q = spec.Split(',');
            double gx = double.Parse(q[0], ic), gz = double.Parse(q[1], ic), rad = double.Parse(q[2], ic);
            float yref = float.Parse(q[3], ic);
            var g = CameraRides.Ground;
            var sb = new System.Text.StringBuilder();
            TileId id = TileId.At(10, gx, gz);
            TileData t = StreamingSampleRegion.Tile(id);
            var h1 = new TileHeightSampler(t, 1);
            int shown = 0;
            for (double z = gz - rad; z <= gz + rad; z += 1)
                for (double x = gx - rad; x <= gx + rad; x += 1)
                {
                    float h, n;
                    if (!g.TryTerrainHeight(x, z, out h)) continue;
                    h1.TryNaturalHeight(x, z, out n);
                    if (h > yref + 0.6f || h - n > 0.3f)
                        if (shown++ < 40) sb.AppendLine(string.Format(ic, "{0:0},{1:0}: terrain {2:0.00} natural {3:0.00}", x, z, h, n));
                }
            sb.AppendLine("shown " + shown);
            TestContext.Progress.WriteLine(sb.ToString());
        }
    }
}
namespace Ghumante.Core.Tests
{
    public class ZzCutfillScratch7
    {
        [Test]
        public void BusNoCarve()
        {
            if (Environment.GetEnvironmentVariable("ZZ_BUSNC") == null) Assert.Pass();
            TerrainCarve.Enabled = Environment.GetEnvironmentVariable("ZZ_BUSNC") != "0";
            var c = CharactersCameraRideTests.Rides[2];
            var frames = CharactersCameraRideTests.Record(c);
            CamMotion now = CamMotion.Of(CharactersCameraRideTests.RunNew(c, frames, 2400f / 1080f), frames);
            TestContext.Progress.WriteLine("carve " + TerrainCarve.Enabled + ": " + now + " | " + CharactersCameraRideTests.Failures(now));
            TerrainCarve.Enabled = true;
        }
    }
}
namespace Ghumante.Core.Tests
{
    public class ZzCutfillScratch8
    {
        [Test]
        public void BusSplit()
        {
            string mode = Environment.GetEnvironmentVariable("ZZ_BUSSPLIT"); // "rideoff" or "rideon"
            if (mode == null) Assert.Pass();
            var f = typeof(CameraRides).GetField("_ground", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var c = CharactersCameraRideTests.Rides[2];
            TerrainCarve.Enabled = mode != "rideoff";
            var frames = CharactersCameraRideTests.Record(c);
            TerrainCarve.Enabled = !TerrainCarve.Enabled;
            f.SetValue(null, null);
            CamMotion now = CamMotion.Of(CharactersCameraRideTests.RunNew(c, frames, 2400f / 1080f), frames);
            TestContext.Progress.WriteLine(mode + " camera carve " + TerrainCarve.Enabled + ": " + now + " | " + CharactersCameraRideTests.Failures(now));
            TerrainCarve.Enabled = true;
        }
    }
}
