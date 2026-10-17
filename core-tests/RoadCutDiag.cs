using System;
using System.Globalization;
using System.Text;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class RoadCutDiag
    {
        [Test]
        public void Diag()
        {
            string spec = Environment.GetEnvironmentVariable("CUTDIAG");
            if (string.IsNullOrEmpty(spec)) Assert.Pass();
            var sb = new StringBuilder();
            foreach (string item in spec.Split(';'))
            {
                string[] p = item.Split(',');
                var id = new TileId(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]));
                int road = int.Parse(p[3]);
                double s0 = double.Parse(p[4], CultureInfo.InvariantCulture), s1 = double.Parse(p[5], CultureInfo.InvariantCulture);
                TileData t = StreamingSampleRegion.Tile(id);
                var h = TileHeightSampler.ForArea(t, t.Tile, 2);
                RoadGrade g = RoadGrade.For(t, h, new RoadOptions());
                RoadLayout lay = g.Layout;
                RoadCentreline c = lay.Centres[road];
                RoadWidthProfile prof = lay.Profiles[road];
                sb.AppendLine(id + " road " + road + " " + t.Roads[road].RoadClass + " len " + c.RawLengthM.ToString("0.0") + " lift " + g.LiftOf(road) + " carve " +
                              (h.Carve != null ? h.Carve.RefinedCells + " cells" : "none"));
                for (double s = s0; s <= s1; s += 1.0)
                {
                    double x, z, tx, tz;
                    c.At(s, out x, out z, out tx, out tz);
                    double ux = -tz, uz = tx;
                    float shift = prof.ShiftAt(s), half = 0.5f * prof.DrawnAt(s);
                    double ol = shift + half, or = shift - half;
                    RoadGrade.Row r = g.RowAt(road, s);
                    float yl = g.SurfaceY(road, s, ol, x + ux * ol, z + uz * ol), yr = g.SurfaceY(road, s, or, x + ux * or, z + uz * or);
                    float tl = g.Terrain(x + ux * ol, z + uz * ol), tr = g.Terrain(x + ux * or, z + uz * or);
                    float nl = g.NaturalTerrain(x + ux * ol, z + uz * ol), nr = g.NaturalTerrain(x + ux * or, z + uz * or);
                    float bl = g.BenchY(r, ol), br = g.BenchY(r, or);
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "s {0,6:0.0} gap {1} abs {2} | P {3,7:0.00} B {4,6:0.000} pinW {5:0.00} abs {6} rel {7:0.00} clamp {8} deckW {9:0.00} | yL {10,7:0.00} yR {11,7:0.00} tilt {12,5:0.0}% | terL {13,7:0.00} terR {14,7:0.00} natL {15,7:0.00} natR {16,7:0.00} benL {17,7:0.00} benR {18,7:0.00} half {19:0.0}",
                        s, lay.InGap(road, s) ? 1 : 0, lay.InAbsorbed(road, s) ? 1 : 0, r.P, r.B, r.PinW, r.PinAbs ? 1 : 0, r.PinRel, r.Clamp ? 1 : 0, r.DeckW, yl, yr,
                        100 * Math.Abs(yl - yr) / (2 * half), tl, tr, nl, nr, bl, br, half));
                    if (p.Length > 6 && Math.Abs(s - double.Parse(p[6], CultureInfo.InvariantCulture)) < 0.01)
                    {
                        sb.AppendLine("  L: " + h.Carve.Explain(x + ux * ol, z + uz * ol).Replace("\n", "\n     "));
                        sb.AppendLine("  R: " + h.Carve.Explain(x + ux * or, z + uz * or).Replace("\n", "\n     "));
                    }
                }
            }
            TestContext.Progress.WriteLine(sb.ToString());
        }
    }
}
namespace Ghumante.Core.Tests
{
    public class RoadCutDiagBudget
    {
        [Test]
        public void Budget()
        {
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("CUTDIAG_BUDGET"))) Assert.Pass();
            var sb = new System.Text.StringBuilder();
            long total2 = 0, total4 = 0, nat2 = 0;
            foreach (Ghumante.Core.Data.TileId id in StreamingSampleRegion.TilesAt(10))
            {
                var t = StreamingSampleRegion.Tile(id);
                if (t == null) continue;
                foreach (int step in new[] { 2, 4 })
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var m = new Ghumante.Core.Meshing.MeshData();
                    Ghumante.Core.Meshing.TerrainMesher.Build(t, t.Tile, new Ghumante.Core.Meshing.TerrainOptions { Step = step }, m);
                    double ms = sw.Elapsed.TotalMilliseconds;
                    var h = Ghumante.Core.Meshing.TileHeightSampler.ForArea(t, t.Tile, step);
                    var c = h.Carve;
                    sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} step {1}: tris {2,7} refined {3,5} carve {4,7:0} ms mesh {5,6:0} ms elements {6}",
                        id, step, m.TriangleCount, c != null ? c.RefinedCells : 0, c != null ? c.BuildMs : 0, ms, c != null ? c.ElementCount : 0));
                    if (step == 2) total2 += m.TriangleCount; else total4 += m.TriangleCount;
                    if (step == 2)
                    {
                        var n = new Ghumante.Core.Meshing.MeshData();
                        Ghumante.Core.Meshing.TerrainMesher.Build(t, t.Tile, new Ghumante.Core.Meshing.TerrainOptions { Step = step, Carve = false }, n);
                        nat2 += n.TriangleCount;
                    }
                }
            }
            sb.AppendLine("total step2 " + total2 + " natural " + nat2 + " step4 " + total4);
            TestContext.Progress.WriteLine(sb.ToString());
        }
    }
}
namespace Ghumante.Core.Tests
{
    public class RoadCutDiagWhy
    {
        [Test]
        public void Why()
        {
            string spec = System.Environment.GetEnvironmentVariable("CUTDIAG_WHY");
            if (string.IsNullOrEmpty(spec)) Assert.Pass();
            Ghumante.Core.Meshing.TerrainCarve.DiagOn = true;
            var sb = new System.Text.StringBuilder();
            foreach (string item in spec.Split(';'))
            {
                string[] p = item.Split(',');
                var id = new Ghumante.Core.Data.TileId(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]));
                var t = StreamingSampleRegion.Tile(id);
                var h = Ghumante.Core.Meshing.TileHeightSampler.ForArea(t, t.Tile, int.Parse(p[3]));
                var c = h.Carve;
                if (c == null) { sb.AppendLine(id + " no carve"); continue; }
                string mapDir = System.Environment.GetEnvironmentVariable("CUTDIAG_MAPS");
                if (!string.IsNullOrEmpty(mapDir)) c.WriteMap(System.IO.Path.Combine(mapDir, "map_" + id.Tx + "_" + id.Ty + "_s" + p[3] + ".ppm"));
                sb.AppendLine(id + " refined " + c.RefinedCells + " move<0.5 " + c.DiagBuckets[0] + " <1 " + c.DiagBuckets[1] + " <2 " + c.DiagBuckets[2] + " >=2 " + c.DiagBuckets[3] +
                              " underRoadOnly " + c.DiagCutOnly + " natDiff>0.3 " + c.DiagNatDiff + "\n  " + c.StrengthStats());
                // A few refined cells with their worst sub-vertex explained.
                int shown = 0;
                for (int j = 0; j < c.Grid.Quads && shown < 6; j += 7)
                for (int i = 0; i < c.Grid.Quads && shown < 6; i += 5)
                {
                    if (!c.IsRefined(i, j)) continue;
                    double best = 0; double bx = 0, bz = 0; float bn = 0, bv = 0;
                    for (int b = 0; b <= c.Sub; b++)
                    for (int a = 0; a <= c.Sub; a++)
                    {
                        double lx = (i + a / (double)c.Sub) * c.Grid.CellM, lz = (j + b / (double)c.Sub) * c.Grid.CellM, du, dv;
                        float nat = (float)c.Grid.Surface(t, i + a / (double)c.Sub, j + b / (double)c.Sub, out du, out dv);
                        float v = c.CarvedAt(lx, lz, nat);
                        if (System.Math.Abs(v - nat) > best) { best = System.Math.Abs(v - nat); bx = lx; bz = lz; bn = nat; bv = v; }
                    }
                    sb.AppendLine("  cell " + i + "," + j + " worst at " + bx.ToString("0.0") + "," + bz.ToString("0.0") + " nat " + bn.ToString("0.00") + " carved " + bv.ToString("0.00"));
                    sb.AppendLine("    " + c.Explain(bx, bz).Replace("\n", "\n    "));
                    shown++;
                }
            }
            Ghumante.Core.Meshing.TerrainCarve.DiagOn = false;
            TestContext.Progress.WriteLine(sb.ToString());
        }
    }
}
