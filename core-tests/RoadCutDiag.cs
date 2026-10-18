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
            Ghumante.Core.Meshing.TerrainCarve.PreCarveOn = System.Environment.GetEnvironmentVariable("CUTDIAG_NOPRE") == null;
            string only = System.Environment.GetEnvironmentVariable("CUTDIAG_TILES");
            string tunes = System.Environment.GetEnvironmentVariable("CUTDIAG_TUNE"); // refineMove,maxLower,strong(0/1);...
            var sbAll = new System.Text.StringBuilder();
            foreach (string tune in (string.IsNullOrEmpty(tunes) ? "" : tunes).Split(';'))
            {
                if (!string.IsNullOrEmpty(tune))
                {
                    string[] tp = tune.Split(',');
                    Ghumante.Core.Meshing.TerrainCarve.TuneRefineMove = float.Parse(tp[0], System.Globalization.CultureInfo.InvariantCulture);
                    Ghumante.Core.Meshing.TerrainCarve.TuneMaxLower = float.Parse(tp[1], System.Globalization.CultureInfo.InvariantCulture);
                    Ghumante.Core.Meshing.TerrainCarve.TuneStrongCriterion = tp[2] == "1";
                    if (tp.Length > 3) Ghumante.Core.Meshing.TerrainCarve.TuneSubCellM = float.Parse(tp[3], System.Globalization.CultureInfo.InvariantCulture);
                    if (tp.Length > 4) Ghumante.Core.Meshing.TerrainCarve.TuneCoarseSubCellM = float.Parse(tp[4], System.Globalization.CultureInfo.InvariantCulture);
                }
                Ghumante.Core.Meshing.TerrainCarve.CacheGeneration++;
                sbAll.AppendLine("tune " + tune + ":");
                sbAll.Append(BudgetOnce(only));
            }
            Ghumante.Core.Meshing.TerrainCarve.TuneRefineMove = Ghumante.Core.Meshing.TerrainCarve.RefineMoveM;
            Ghumante.Core.Meshing.TerrainCarve.TuneMaxLower = Ghumante.Core.Meshing.TerrainCarve.MaxLowerM;
            Ghumante.Core.Meshing.TerrainCarve.TuneStrongCriterion = true;
            Ghumante.Core.Meshing.TerrainCarve.TuneSubCellM = Ghumante.Core.Meshing.TerrainCarve.SubCellM;
            Ghumante.Core.Meshing.TerrainCarve.TuneCoarseSubCellM = Ghumante.Core.Meshing.TerrainCarve.CoarseSubCellM;
            Ghumante.Core.Meshing.TerrainCarve.PreCarveOn = true;
            Ghumante.Core.Meshing.TerrainCarve.CacheGeneration++;
            TestContext.Progress.WriteLine(sbAll.ToString());
        }

        private static string BudgetOnce(string only)
        {
            var sb = new System.Text.StringBuilder();
            long total2 = 0, total4 = 0, nat2 = 0;
            foreach (Ghumante.Core.Data.TileId id in StreamingSampleRegion.TilesAt(10))
            {
                var t = StreamingSampleRegion.Tile(id);
                if (t == null) continue;
                if (only != null && !only.Contains(id.Tx + "_" + id.Ty)) continue;
                foreach (int step in new[] { 2, 4 })
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var m = new Ghumante.Core.Meshing.MeshData();
                    Ghumante.Core.Meshing.TerrainMesher.Build(t, t.Tile, new Ghumante.Core.Meshing.TerrainOptions { Step = step }, m);
                    double ms = sw.Elapsed.TotalMilliseconds;
                    var h = Ghumante.Core.Meshing.TileHeightSampler.ForArea(t, t.Tile, step);
                    var c = h.Carve;
                    var nw = new Ghumante.Core.Meshing.MeshData();
                    Ghumante.Core.Meshing.TerrainMesher.Build(t, t.Tile, new Ghumante.Core.Meshing.TerrainOptions { Step = step, Walls = false }, nw);
                    sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} step {1}: tris {2,7} refined {3,5} poked {7,4} walls {8,6} carve {4,7:0} ms mesh {5,6:0} ms elements {6}",
                        id, step, m.TriangleCount, c != null ? c.RefinedCells : 0, c != null ? c.BuildMs : 0, ms, c != null ? c.ElementCount : 0, c != null ? c.PokedCells : 0,
                        m.TriangleCount - nw.TriangleCount));
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
            return sb.ToString();
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
                              " underRoadOnly " + c.DiagCutOnly + "\n  " + c.StrengthStats());
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
namespace Ghumante.Core.Tests
{
    /// <summary>Diagnostics (CUTDIAG_FLOAT=1): the worst floating or sinking contacts at the hill places, explained.</summary>
    public class RoadCutDiagFloat
    {
        private struct Row
        {
            public double Off;
            public string Text;
        }

        [Test]
        public void Float()
        {
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("CUTDIAG_FLOAT"))) Assert.Pass();
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new System.Text.StringBuilder();
            foreach (HillPlaces.Place place in HillPlaces.All)
            {
                double px, pz;
                place.Game(out px, out pz);
                Ghumante.Core.Data.TileData t = StreamingSampleRegion.Tile(place.Tile);
                if (t == null) continue;
                var h = Ghumante.Core.Meshing.TileHeightSampler.ForArea(t, t.Tile, HillPlaces.RuntimeStep);
                var terrain = new Ghumante.Core.Meshing.MeshData();
                Ghumante.Core.Meshing.TerrainMesher.Build(t, t.Tile, new Ghumante.Core.Meshing.TerrainOptions { Step = HillPlaces.RuntimeStep }, terrain);
                var roads = new Ghumante.Core.Meshing.MeshData();
                Ghumante.Core.Meshing.RoadMesher.Build(t, h, new Ghumante.Core.Meshing.RoadOptions(), roads);
                var g = new DrawnGround();
                g.Add(terrain);
                g.Add(roads, 0.55f);
                var surface = Ghumante.Core.Meshing.Roads.RoadSurfaceQuery.For(t, h, new Ghumante.Core.Meshing.RoadOptions());
                var rows = new System.Collections.Generic.List<Row>();
                System.Action<string, double, double, float> probe = (what, lx, lz, cy) =>
                {
                    float ground;
                    if (!g.TryHeight(lx, lz, cy, out ground)) return;
                    double off = cy - ground;
                    if (System.Math.Abs(off) < 0.05) return;
                    float bench = 0f;
                    double gx, gz;
                    bool ext = h.Carve != null && h.Carve.ExtentAt(lx, lz, out bench, out gx, out gz);
                    float drawn = h.Carve != null ? (float)h.Carve.DrawnSurface(lx / h.Grid.CellM, lz / h.Grid.CellM, out gx, out gz) : float.NaN;
                    Ghumante.Core.Meshing.Roads.RoadSurfaceHit hit;
                    bool rs = surface.TrySample(t.Tile.X0 + lx, t.Tile.Z0 + lz, cy, out hit);
                    double lon, lat;
                    Ghumante.Core.Geo.WorldFrame.GameToLonLat(t.Tile.X0 + lx, t.Tile.Z0 + lz, out lon, out lat);
                    rows.Add(new Row
                    {
                        Off = off,
                        Text = string.Format(ic, "{0,-28} off {1,6:0.000} at {2:0.00000},{3:0.00000} local {4:0.0},{5:0.0} y {6:0.00} ground {7:0.00} ext {8} bench {9:0.00} drawnTer {10:0.00} roadQ {11} y {12:0.00} road {13} lat {14:0.0} half {15:0.0} cw {16} fp {17} sh {18} junc {19}",
                            what, off, lat, lon, lx, lz, cy, ground, ext ? 1 : 0, bench, drawn, rs ? 1 : 0, rs ? hit.Y : 0f, rs ? hit.Road : -9, rs ? hit.LateralM : 0f,
                            rs ? hit.HalfWidthM : 0f, rs && hit.OnCarriageway ? 1 : 0, rs && hit.OnFootpath ? 1 : 0, rs && hit.OnShoulder ? 1 : 0, rs && hit.OnJunction ? 1 : 0),
                    });
                };
                var list = new System.Collections.Generic.List<Ghumante.World.Instancing.ParkedVehicle>();
                Ghumante.World.Instancing.ParkedPlacement.Place(t, h, list, new Ghumante.Core.Meshing.RoadOptions());
                foreach (var v in list)
                {
                    double gx0 = t.Tile.X0 + v.X, gz0 = t.Tile.Z0 + v.Z;
                    if ((gx0 - px) * (gx0 - px) + (gz0 - pz) * (gz0 - pz) > place.RadiusM * place.RadiusM) continue;
                    var e = Ghumante.Core.Driving.VehicleCatalog.At(v.Variant);
                    double hd = v.YawDeg * System.Math.PI / 180.0, fx = System.Math.Sin(hd), fz = System.Math.Cos(hd), rx = fz, rz = -fx;
                    foreach (var ws in Ghumante.Core.Driving.VehicleMesher.Wheels(e))
                        probe("parked " + e.AssetId, v.X + fx * ws.Z + rx * ws.X, v.Z + fz * ws.Z + rz * ws.X, v.Y + ws.Y - ws.Radius);
                }
                var cg = new Ghumante.Core.Meshing.CarvedGround();
                cg.Add(Ghumante.Core.Meshing.CarvedGround.Prepare(t, 1));
                var sim = new Ghumante.Core.Traffic.AnimalSim(TrafficData.Graph, Ghumante.Core.Traffic.AnimalSettings.High(), 7) { Ground = cg };
                sim.SetFocus(px, pz, (float)place.RadiusM);
                var poses = new Ghumante.Core.Traffic.AnimalPose[256];
                for (int step = 0; step < 6; step++)
                {
                    sim.Step(37f, 9f + step, 0f);
                    int n = sim.CopyPoses(poses);
                    for (int k = 0; k < n; k++)
                    {
                        var a = poses[k];
                        if (!t.Tile.Contains(a.X, a.Z)) continue;
                        float sy;
                        bool sr;
                        bool st = cg.TryStand(a.X, a.Z, a.Y, out sy, out sr);
                        probe("animal " + a.Kind + " clip " + a.ClipId + " stand " + (st ? (sr ? "road " : "ter ") + sy.ToString("0.00", ic) : "none"), a.X - t.Tile.X0, a.Z - t.Tile.Z0, a.Y);
                    }
                }
                // Walkers on the carriageway (as RoadCutHillTests): the drawn ground at their centre, feet around it.
                var lay = Ghumante.Core.Meshing.RoadLayout.For(t);
                for (int i = 0; i < t.Roads.Count; i++)
                {
                    var c = lay.Centres[i];
                    if (c == null || c.Count < 2 || !lay.DrawsRibbon(i)) continue;
                    var prof = lay.Profiles[i];
                    for (double s = 2; s < c.RawLengthM - 2; s += 6)
                    {
                        double x, z, tx, tz;
                        c.At(s, out x, out z, out tx, out tz);
                        double gx0 = t.Tile.X0 + x, gz0 = t.Tile.Z0 + z;
                        if ((gx0 - px) * (gx0 - px) + (gz0 - pz) * (gz0 - pz) > place.RadiusM * place.RadiusM) continue;
                        float half = 0.5f * prof.DrawnAt(s), shift = prof.ShiftAt(s);
                        double hfx = tx, hfz = tz, hrx = tz, hrz = -tx;
                        foreach (double f in new[] { -0.7, -0.35, 0.0, 0.35, 0.7 })
                        {
                            double off = shift + f * half;
                            double wx = x - tz * off, wz = z + tx * off;
                            float y0;
                            if (!g.TryHeight(wx, wz, float.PositiveInfinity, out y0)) continue;
                            for (int a = -1; a <= 1; a += 2)
                            for (int b = -1; b <= 1; b += 2)
                            {
                                double cx = wx + hfx * 0.13 * a + hrx * 0.11 * b, cz = wz + hfz * 0.13 * a + hrz * 0.11 * b;
                                float gy;
                                if (!g.TryHeight(cx, cz, y0, out gy) || System.Math.Abs(y0 - gy) > 0.5f) continue;
                                if (System.Math.Abs(y0 - gy) < 0.05f) continue;
                                probe("walker road " + i + " s " + s.ToString("0", ic) + " f " + f.ToString("0.00", ic), cx, cz, y0);
                            }
                        }
                    }
                }
                rows.Sort((a, b) => System.Math.Abs(b.Off).CompareTo(System.Math.Abs(a.Off)));
                sb.AppendLine(place.Name + " " + t.Tile + ": " + rows.Count + " contacts off by 5 cm or more");
                foreach (string cat in new[] { "parked", "animal", "walker" })
                {
                    int shown = 0;
                    for (int k = 0; k < rows.Count && shown < 6; k++)
                    {
                        if (!rows[k].Text.StartsWith(cat)) continue;
                        sb.AppendLine("  " + rows[k].Text);
                        shown++;
                    }
                }
            }
            TestContext.Progress.WriteLine(sb.ToString());
        }
    }
}
namespace Ghumante.Core.Tests
{
    /// <summary>Diagnostics (CUTDIAG_VERTS="level,tx,ty,step,road,lx,lz,r"): road mesh vertices near a point against the
    /// grade's surface of a road there.</summary>
    public class RoadCutDiagVerts
    {
        [Test]
        public void Verts()
        {
            string spec = System.Environment.GetEnvironmentVariable("CUTDIAG_VERTS");
            if (string.IsNullOrEmpty(spec)) Assert.Pass();
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new System.Text.StringBuilder();
            foreach (string item in spec.Split(';'))
            {
                string[] p = item.Split(',');
                var id = new Ghumante.Core.Data.TileId(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]));
                int step = int.Parse(p[3]), road = int.Parse(p[4]);
                double px = double.Parse(p[5], ic), pz = double.Parse(p[6], ic), r = double.Parse(p[7], ic);
                var t = StreamingSampleRegion.Tile(id);
                var h = Ghumante.Core.Meshing.TileHeightSampler.ForArea(t, t.Tile, step);
                var roads = new Ghumante.Core.Meshing.MeshData();
                Ghumante.Core.Meshing.RoadMesher.Build(t, h, new Ghumante.Core.Meshing.RoadOptions(), roads);
                var g = Ghumante.Core.Meshing.RoadGrade.For(t, h, new Ghumante.Core.Meshing.RoadOptions());
                var lay = Ghumante.Core.Meshing.RoadLayout.For(t);
                var c = lay.Centres[road];
                sb.AppendLine(id + " road " + road + " near " + px.ToString("0.0", ic) + "," + pz.ToString("0.0", ic));
                for (int v = 0; v < roads.VertexCount; v++)
                {
                    double vx = roads.Positions[3 * v], vy = roads.Positions[3 * v + 1], vz = roads.Positions[3 * v + 2];
                    if ((vx - px) * (vx - px) + (vz - pz) * (vz - pz) > r * r) continue;
                    int seg;
                    double along, lat, dist;
                    c.Nearest(vx, vz, out seg, out along, out lat, out dist);
                    float gy = g.SurfaceY(road, along, lat, vx, vz);
                    sb.AppendLine(string.Format(ic, "  v {0:0.00},{1:0.00} y {2:0.000} along {3:0.00} lat {4:0.00} grade {5:0.000} diff {6:0.000}", vx, vz, vy, along, lat, gy, vy - gy));
                }
            }
            TestContext.Progress.WriteLine(sb.ToString());
        }
    }

    /// <summary>Diagnostics (CUTDIAG_LINE="level,tx,ty,step,road,s0,s1,ds,lat;..."): drawn surfaces along a road at a lateral
    /// offset (0 terrain, 1 roads), with the road surface query and the carve's extent.</summary>
    public class RoadCutDiagLine
    {
        [Test]
        public void Line()
        {
            string spec = System.Environment.GetEnvironmentVariable("CUTDIAG_LINE");
            if (string.IsNullOrEmpty(spec)) Assert.Pass();
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new System.Text.StringBuilder();
            foreach (string item in spec.Split(';'))
            {
                string[] p = item.Split(',');
                var id = new Ghumante.Core.Data.TileId(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]));
                int step = int.Parse(p[3]), road = int.Parse(p[4]);
                double s0 = double.Parse(p[5], ic), s1 = double.Parse(p[6], ic), ds = double.Parse(p[7], ic), lat = double.Parse(p[8], ic);
                var t = StreamingSampleRegion.Tile(id);
                var h = Ghumante.Core.Meshing.TileHeightSampler.ForArea(t, t.Tile, step);
                var terrain = new Ghumante.Core.Meshing.MeshData();
                Ghumante.Core.Meshing.TerrainMesher.Build(t, t.Tile, new Ghumante.Core.Meshing.TerrainOptions { Step = step }, terrain);
                var roads = new Ghumante.Core.Meshing.MeshData();
                Ghumante.Core.Meshing.RoadMesher.Build(t, h, new Ghumante.Core.Meshing.RoadOptions(), roads);
                var dg = new DrawnGround();
                dg.Add(terrain);
                dg.Add(roads, 0.55f);
                var q = Ghumante.Core.Meshing.Roads.RoadSurfaceQuery.For(t, h, new Ghumante.Core.Meshing.RoadOptions());
                var lay = Ghumante.Core.Meshing.RoadLayout.For(t);
                var c = lay.Centres[road];
                sb.AppendLine(id + " road " + road + " lat " + lat.ToString("0.0", ic));
                for (double s = s0; s <= s1 + 1e-9; s += ds)
                {
                    double x, z, tx, tz;
                    c.At(s, out x, out z, out tx, out tz);
                    double lx = x - tz * lat, lz = z + tx * lat;
                    Ghumante.Core.Meshing.Roads.RoadSurfaceHit hit;
                    bool rs = q.TrySample(t.Tile.X0 + lx, t.Tile.Z0 + lz, float.PositiveInfinity, out hit);
                    float bench;
                    double gx, gz;
                    bool ext = h.Carve.ExtentAt(lx, lz, out bench, out gx, out gz);
                    sb.AppendLine(string.Format(ic, "  s {0,6:0.0} at {1:0.0},{2:0.0}:{3}; query {4} y {5:0.00} road {6} along {7:0.0} junc {8}; ext {9} bench {10:0.00}", s, lx, lz, dg.All(lx, lz),
                        rs ? 1 : 0, hit.Y, hit.Road, hit.AlongM, hit.OnJunction ? 1 : 0, ext ? 1 : 0, bench));
                }
            }
            TestContext.Progress.WriteLine(sb.ToString());
        }
    }

    /// <summary>Diagnostics (CUTDIAG_POINT="level,tx,ty,step,lx,lz;..."): the carve's state at tile-local points.</summary>
    public class RoadCutDiagPoint
    {
        [Test]
        public void Point()
        {
            string spec = System.Environment.GetEnvironmentVariable("CUTDIAG_POINT");
            if (string.IsNullOrEmpty(spec)) Assert.Pass();
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new System.Text.StringBuilder();
            foreach (string item in spec.Split(';'))
            {
                string[] p = item.Split(',');
                var id = new Ghumante.Core.Data.TileId(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]));
                int step = int.Parse(p[3]);
                double lx = double.Parse(p[4], ic), lz = double.Parse(p[5], ic);
                var t = StreamingSampleRegion.Tile(id);
                var h = Ghumante.Core.Meshing.TileHeightSampler.ForArea(t, t.Tile, step);
                var c = h.Carve;
                double cm = h.Grid.CellM, u = lx / cm, v = lz / cm;
                int i = (int)System.Math.Floor(u), j = (int)System.Math.Floor(v);
                float bench;
                double gx, gz;
                bool ext = c.ExtentAt(lx, lz, out bench, out gx, out gz);
                sb.AppendLine(string.Format(ic, "{0} step {1} at {2:0.0},{3:0.0}: cell {4},{5} refined {6} ext {7} bench {8:0.00} drawn {9:0.00} natural {10:0.00} sub {11} verge {12:0.00}",
                    id, step, lx, lz, i, j, c.IsRefined(i, j), ext, bench, c.DrawnSurface(u, v, out gx, out gz), c.NaturalSurface(u, v), c.Sub, c.VergeM));
                for (int k = j; k <= j + 1; k++)
                for (int l = i; l <= i + 1; l++)
                {
                    double vx = l * cm, vz = k * cm;
                    float vb;
                    bool ve = c.ExtentAt(vx, vz, out vb, out gx, out gz);
                    sb.AppendLine(string.Format(ic, "   vertex {0},{1}: coarse {2:0.00} natural {3:0.00} moved {4} carvedV {5} ext {6} bench {7:0.00}", k, l, c.CoarseVertex(k, l),
                        c.NaturalVertex(k, l), c.IsMoved(k, l), c.IsCarvedVertex(k, l), ve, vb));
                }
                sb.AppendLine("   " + c.Explain(lx, lz).Replace("\n", "\n   "));
                {
                    // What is drawn there (0 terrain, 1 roads) and what the road surface query answers.
                    var terrain = new Ghumante.Core.Meshing.MeshData();
                    Ghumante.Core.Meshing.TerrainMesher.Build(t, t.Tile, new Ghumante.Core.Meshing.TerrainOptions { Step = step }, terrain);
                    var roads = new Ghumante.Core.Meshing.MeshData();
                    Ghumante.Core.Meshing.RoadMesher.Build(t, h, new Ghumante.Core.Meshing.RoadOptions(), roads);
                    var dg = new DrawnGround();
                    dg.Add(terrain);
                    dg.Add(roads, 0.55f);
                    var q = Ghumante.Core.Meshing.Roads.RoadSurfaceQuery.For(t, h, new Ghumante.Core.Meshing.RoadOptions());
                    Ghumante.Core.Meshing.Roads.RoadSurfaceHit hit;
                    bool rs = q.TrySample(t.Tile.X0 + lx, t.Tile.Z0 + lz, float.PositiveInfinity, out hit);
                    sb.AppendLine(string.Format(ic, "   drawn surfaces{0}; query {1} y {2:0.00} road {3} along {4:0.0} lat {5:0.00} half {6:0.00} cw {7} fp {8} sh {9} junc {10}", dg.All(lx, lz),
                        rs ? 1 : 0, hit.Y, hit.Road, hit.AlongM, hit.LateralM, hit.HalfWidthM, hit.OnCarriageway ? 1 : 0, hit.OnFootpath ? 1 : 0, hit.OnShoulder ? 1 : 0, hit.OnJunction ? 1 : 0));
                    var lay = Ghumante.Core.Meshing.RoadLayout.For(t);
                    for (int ri = 0; ri < t.Roads.Count; ri++)
                    {
                        var cl = lay.Centres[ri];
                        if (cl == null || cl.Count < 2) continue;
                        int seg;
                        double along, lat, dist;
                        if (!cl.Nearest(lx, lz, out seg, out along, out lat, out dist) || dist > 12) continue;
                        var prof = lay.Profiles[ri];
                        var cuts = lay.Cuts[ri];
                        var cs = new System.Text.StringBuilder();
                        if (cuts != null) foreach (var cu in cuts) cs.Append(' ').Append(cu.S.ToString("0.0", ic));
                        float csl, csr;
                        lay.CorridorSides(ri, along, out csl, out csr);
                        sb.AppendLine(string.Format(ic, "   road {0} {1} along {2:0.0}/{3:0.0} lat {4:0.00} dist {5:0.00} half {6:0.00} shift {7:0.00} gap {8} ribbon {9} dual {11} shoulder {12:0.00} footL {13:0.00} footR {14:0.00} corridor {15:0.00}/{16:0.00} cuts{10}", ri,
                            t.Roads[ri].RoadClass, along, cl.RawLengthM, lat, dist, 0.5f * prof.DrawnAt(along), prof.ShiftAt(along), lay.InGap(ri, along) ? 1 : 0, lay.DrawsRibbon(ri) ? 1 : 0, cs,
                            prof.Dual ? 1 : 0, lay.ShoulderAt(ri, along), prof.Sample(prof.FootLeft, along), prof.Sample(prof.FootRight, along), csl, csr));
                        if (cuts != null) foreach (var cu in cuts) sb.AppendLine(string.Format(ic, "      cut s {0:0.0} c {1:0.00},{2:0.00} u {3:0.000},{4:0.000} half {5:0.00}", cu.S, cu.CX, cu.CZ, cu.UX, cu.UZ, cu.Half));
                    }
                }
                if (c.IsRefined(i, j))
                {
                    sb.Append("   neighbours refined:");
                    for (int dj = -1; dj <= 1; dj++)
                    for (int di = -1; di <= 1; di++) sb.Append(c.IsRefined(i + di, j + dj) ? " 1" : " 0");
                    sb.AppendLine();
                    for (int b = c.Sub; b >= 0; b--)
                    {
                        sb.Append("   sub b" + b + ":");
                        for (int a = 0; a <= c.Sub; a++)
                        {
                            double sx = i * cm + a * c.SubM, sz = j * cm + b * c.SubM;
                            float sbn;
                            bool se = c.ExtentAt(sx, sz, out sbn, out gx, out gz);
                            sb.Append(string.Format(ic, " {0:0.00}{1}", c.SubVertex(i, j, a, b), se ? "e" + sbn.ToString("0.00", ic) : ""));
                        }
                        sb.AppendLine();
                    }
                }
            }
            TestContext.Progress.WriteLine(sb.ToString());
        }
    }
}
namespace Ghumante.Core.Tests
{
    /// <summary>Diagnostics (CUTDIAG_CONTACTS=1): the hill test's own contacts (parked wheels, animal footprints), the worst
    /// ones explained per contact.</summary>
    public class RoadCutDiagContacts
    {
        [Test]
        public void Contacts()
        {
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("CUTDIAG_CONTACTS"))) Assert.Pass();
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new System.Text.StringBuilder();
            var rows = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<double, string>>();
            foreach (HillPlaces.Place place in HillPlaces.All)
            {
                double px, pz;
                place.Game(out px, out pz);
                Ghumante.Core.Data.TileData t = StreamingSampleRegion.Tile(place.Tile);
                if (t == null) continue;
                var h = Ghumante.Core.Meshing.TileHeightSampler.ForArea(t, t.Tile, HillPlaces.RuntimeStep);
                var terrain = new Ghumante.Core.Meshing.MeshData();
                Ghumante.Core.Meshing.TerrainMesher.Build(t, t.Tile, new Ghumante.Core.Meshing.TerrainOptions { Step = HillPlaces.RuntimeStep }, terrain);
                var roads = new Ghumante.Core.Meshing.MeshData();
                Ghumante.Core.Meshing.RoadMesher.Build(t, h, new Ghumante.Core.Meshing.RoadOptions(), roads);
                var g = new DrawnGround();
                g.Add(terrain);
                g.Add(roads, 0.55f);
                var surface = Ghumante.Core.Meshing.Roads.RoadSurfaceQuery.For(t, h, new Ghumante.Core.Meshing.RoadOptions());
                var cg = new Ghumante.Core.Meshing.CarvedGround();
                cg.Add(Ghumante.Core.Meshing.CarvedGround.Prepare(t, 1));
                System.Func<string, double, double, double, float, float, float, System.Collections.Generic.List<double>, string> measure =
                    (what, gx, gz, heading, y, pitchDeg, rollDeg, contacts) =>
                    {
                        double fx = System.Math.Sin(heading), fz = System.Math.Cos(heading), rx = fz, rz = -fx;
                        double p = pitchDeg * System.Math.PI / 180.0, r = rollDeg * System.Math.PI / 180.0;
                        var line = new System.Text.StringBuilder();
                        double worst = 0;
                        for (int k = 0; k + 2 < contacts.Count; k += 3)
                        {
                            double lf = contacts[k], lr = contacts[k + 1], lu = contacts[k + 2];
                            double r1 = lr * System.Math.Cos(r) - lu * System.Math.Sin(r), u1 = lr * System.Math.Sin(r) + lu * System.Math.Cos(r);
                            double u2 = u1 * System.Math.Cos(p) - lf * System.Math.Sin(p), f2 = u1 * System.Math.Sin(p) + lf * System.Math.Cos(p);
                            double cx = gx - t.Tile.X0 + fx * f2 + rx * r1, cz = gz - t.Tile.Z0 + fz * f2 + rz * r1;
                            float cy = y + (float)u2, ground;
                            if (!g.TryHeight(cx, cz, cy, out ground)) continue;
                            double off = cy - ground;
                            if (System.Math.Abs(off) > System.Math.Abs(worst)) worst = off;
                            Ghumante.Core.Meshing.Roads.RoadSurfaceHit hit;
                            bool rs = surface.TrySample(t.Tile.X0 + cx, t.Tile.Z0 + cz, cy, out hit);
                            float sy;
                            bool onRoad;
                            bool st = cg.TryStand(t.Tile.X0 + cx, t.Tile.Z0 + cz, cy, out sy, out onRoad);
                            float bench = 0f;
                            double bgx, bgz;
                            bool ext = h.Carve != null && h.Carve.ExtentAt(cx, cz, out bench, out bgx, out bgz);
                            line.Append(string.Format(ic, "\n      c{0} at {1:0.00},{2:0.00} cy {3:0.000} off {4,6:0.000} drawn{5} | q {6} {7:0.000} road {8} lat {9:0.00}/{10:0.00} cw{11}fp{12}sh{13}j{14} | stand {15} {16:0.000} | ext {17} {18:0.000}",
                                k / 3, cx, cz, cy, off, g.All(cx, cz), rs ? 1 : 0, hit.Y, hit.Road, hit.LateralM, hit.HalfWidthM, hit.OnCarriageway ? 1 : 0,
                                hit.OnFootpath ? 1 : 0, hit.OnShoulder ? 1 : 0, hit.OnJunction ? 1 : 0, st ? (onRoad ? "road" : "ter") : "none", sy, ext ? 1 : 0, bench));
                        }
                        rows.Add(new System.Collections.Generic.KeyValuePair<double, string>(System.Math.Abs(worst), string.Format(ic, "{0} {1} worst {2:0.000} y {3:0.000} pitch {4:0.0} roll {5:0.0} heading {6:0}{7}",
                            place.Name, what, worst, y, pitchDeg, rollDeg, heading * 180 / System.Math.PI, line)));
                        return null;
                    };
                var contactsL = new System.Collections.Generic.List<double>();
                var list = new System.Collections.Generic.List<Ghumante.World.Instancing.ParkedVehicle>();
                Ghumante.World.Instancing.ParkedPlacement.Place(t, h, list, new Ghumante.Core.Meshing.RoadOptions());
                foreach (var v in list)
                {
                    double gx0 = t.Tile.X0 + v.X, gz0 = t.Tile.Z0 + v.Z;
                    if ((gx0 - px) * (gx0 - px) + (gz0 - pz) * (gz0 - pz) > place.RadiusM * place.RadiusM) continue;
                    var e = Ghumante.Core.Driving.VehicleCatalog.At(v.Variant);
                    contactsL.Clear();
                    foreach (var ws in Ghumante.Core.Driving.VehicleMesher.Wheels(e))
                    {
                        contactsL.Add(ws.Z);
                        contactsL.Add(ws.X);
                        contactsL.Add(ws.Y - ws.Radius);
                    }
                    measure("parked " + e.AssetId + " at " + v.X.ToString("0.0", ic) + "," + v.Z.ToString("0.0", ic), gx0, gz0, v.YawDeg * System.Math.PI / 180.0, v.Y, v.PitchDeg, v.RollDeg,
                            contactsL);
                }
                var sim = new Ghumante.Core.Traffic.AnimalSim(TrafficData.Graph, Ghumante.Core.Traffic.AnimalSettings.High(), 7) { Ground = cg };
                sim.SetFocus(px, pz, (float)place.RadiusM);
                var poses = new Ghumante.Core.Traffic.AnimalPose[256];
                for (int step = 0; step < 6; step++)
                {
                    sim.Step(37f, 9f + step, 0f);
                    int n = sim.CopyPoses(poses);
                    for (int k = 0; k < n; k++)
                    {
                        var a = poses[k];
                        if (!t.Tile.Contains(a.X, a.Z)) continue;
                        bool cow = a.Kind == Ghumante.Core.Traffic.AnimalKind.Cow;
                        bool lying = a.ClipId == (byte)Ghumante.Core.Traffic.AnimalClip.Sleep || a.ClipId == (byte)Ghumante.Core.Traffic.AnimalClip.Lie;
                        double hl = cow ? (lying ? 0.8 : 0.7) : (lying ? 0.4 : 0.25), hw = cow ? (lying ? 0.35 : 0.2) : (lying ? 0.15 : 0.08);
                        contactsL.Clear();
                        for (int aa = -1; aa <= 1; aa += 2)
                        for (int bb = -1; bb <= 1; bb += 2)
                        {
                            contactsL.Add(aa * hl);
                            contactsL.Add(bb * hw);
                            contactsL.Add(0);
                        }
                        measure("animal " + a.Kind + (lying ? " lying" : " up") + " id " + a.AgentId + " at " + (a.X - t.Tile.X0).ToString("0.0", ic) + "," + (a.Z - t.Tile.Z0).ToString("0.0", ic),
                                a.X, a.Z, a.HeadingRad, a.Y, a.PitchDeg, a.RollDeg, contactsL);
                    }
                }
            }
            rows.Sort((a, b) => b.Key.CompareTo(a.Key));
            var seen = new System.Collections.Generic.HashSet<string>();
            int shown = 0;
            foreach (var r in rows)
            {
                string head = r.Value.Substring(0, r.Value.IndexOf(" worst"));
                if (!seen.Add(head)) continue;
                if (r.Key < 0.03 || shown++ >= 40) break;
                sb.AppendLine(r.Value);
            }
            TestContext.Progress.WriteLine(sb.ToString());
        }
    }
}
namespace Ghumante.Core.Tests
{
    /// <summary>Diagnostics (CUTDIAG_TRIS="level,tx,ty,step,lx,lz;..."): every road and terrain mesh triangle over a point.</summary>
    public class RoadCutDiagTris
    {
        [Test]
        public void Tris()
        {
            string spec = System.Environment.GetEnvironmentVariable("CUTDIAG_TRIS");
            if (string.IsNullOrEmpty(spec)) Assert.Pass();
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new System.Text.StringBuilder();
            foreach (string item in spec.Split(';'))
            {
                string[] p = item.Split(',');
                var id = new Ghumante.Core.Data.TileId(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]));
                int step = int.Parse(p[3]);
                double px = double.Parse(p[4], ic), pz = double.Parse(p[5], ic);
                var t = StreamingSampleRegion.Tile(id);
                var h = Ghumante.Core.Meshing.TileHeightSampler.ForArea(t, t.Tile, step);
                var roads = new Ghumante.Core.Meshing.MeshData();
                Ghumante.Core.Meshing.RoadMesher.Build(t, h, new Ghumante.Core.Meshing.RoadOptions(), roads);
                var terrain = new Ghumante.Core.Meshing.MeshData();
                Ghumante.Core.Meshing.TerrainMesher.Build(t, t.Tile, new Ghumante.Core.Meshing.TerrainOptions { Step = step }, terrain);
                sb.AppendLine(id + " at " + px.ToString("0.00", ic) + "," + pz.ToString("0.00", ic));
                foreach (var (name, m) in new[] { ("roads", roads), ("terrain", terrain) })
                {
                    for (int k = 0; k + 2 < m.IndexCount; k += 3)
                    {
                        int a = m.Indices[k], b = m.Indices[k + 1], c = m.Indices[k + 2];
                        double ax = m.Positions[3 * a], az = m.Positions[3 * a + 2], bx = m.Positions[3 * b], bz = m.Positions[3 * b + 2], cx = m.Positions[3 * c], cz = m.Positions[3 * c + 2];
                        double d = (bz - cz) * (ax - cx) + (cx - bx) * (az - cz);
                        if (System.Math.Abs(d) < 1e-12) continue;
                        double w0 = ((bz - cz) * (px - cx) + (cx - bx) * (pz - cz)) / d, w1 = ((cz - az) * (px - cx) + (ax - cx) * (pz - cz)) / d, w2 = 1 - w0 - w1;
                        if (w0 < -1e-5 || w1 < -1e-5 || w2 < -1e-5) continue;
                        double y = w0 * m.Positions[3 * a + 1] + w1 * m.Positions[3 * b + 1] + w2 * m.Positions[3 * c + 1];
                        double ux = bx - ax, uy = m.Positions[3 * b + 1] - m.Positions[3 * a + 1], uz = bz - az, vx = cx - ax, vy = m.Positions[3 * c + 1] - m.Positions[3 * a + 1], vz = cz - az;
                        double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx, len = System.Math.Sqrt(nx * nx + ny * ny + nz * nz);
                        sb.AppendLine(string.Format(ic, "  {0} tri {1} y {2:0.000} ny {3:0.00} verts ({4:0.00},{5:0.00},{6:0.00}) ({7:0.00},{8:0.00},{9:0.00}) ({10:0.00},{11:0.00},{12:0.00})", name, k / 3, y, ny / len,
                            ax, m.Positions[3 * a + 1], az, bx, m.Positions[3 * b + 1], bz, cx, m.Positions[3 * c + 1], cz));
                    }
                }
            }
            TestContext.Progress.WriteLine(sb.ToString());
        }
    }
}
namespace Ghumante.Core.Tests
{
    /// <summary>Diagnostics (CUTDIAG_WALK=1): the hill test's walker contacts off by more than 3 cm, by cause.</summary>
    public class RoadCutDiagWalk
    {
        [Test]
        public void Walk()
        {
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("CUTDIAG_WALK"))) Assert.Pass();
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new System.Text.StringBuilder();
            var counts = new System.Collections.Generic.Dictionary<string, int>();
            var examples = new System.Collections.Generic.Dictionary<string, string>();
            var tops = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<double, string>>();
            foreach (HillPlaces.Place place in HillPlaces.All)
            {
                double px, pz;
                place.Game(out px, out pz);
                var t = StreamingSampleRegion.Tile(place.Tile);
                if (t == null) continue;
                var h = Ghumante.Core.Meshing.TileHeightSampler.ForArea(t, t.Tile, HillPlaces.RuntimeStep);
                var terrain = new Ghumante.Core.Meshing.MeshData();
                Ghumante.Core.Meshing.TerrainMesher.Build(t, t.Tile, new Ghumante.Core.Meshing.TerrainOptions { Step = HillPlaces.RuntimeStep }, terrain);
                var roads = new Ghumante.Core.Meshing.MeshData();
                Ghumante.Core.Meshing.RoadMesher.Build(t, h, new Ghumante.Core.Meshing.RoadOptions(), roads);
                var g = new DrawnGround();
                g.Add(terrain);
                g.Add(roads, 0.55f);
                var gr = new DrawnGround();
                gr.Add(roads, 0.55f);
                var q = Ghumante.Core.Meshing.Roads.RoadSurfaceQuery.For(t, h, new Ghumante.Core.Meshing.RoadOptions());
                var lay = Ghumante.Core.Meshing.RoadLayout.For(t);
                var grade = Ghumante.Core.Meshing.RoadGrade.For(t, h, new Ghumante.Core.Meshing.RoadOptions());
                for (int i = 0; i < t.Roads.Count; i++)
                {
                    var c = lay.Centres[i];
                    if (c == null || c.Count < 2 || !lay.DrawsRibbon(i)) continue;
                    if (grade.Has(i) && grade.IsAbsolute(i)) continue;
                    var prof = lay.Profiles[i];
                    var cuts = lay.Cuts[i];
                    for (double s = 2; s < c.RawLengthM - 2; s += 6)
                    {
                        if (grade.Has(i) && grade.OnDeck(i, s)) continue;
                        double x, z, tx, tz;
                        c.At(s, out x, out z, out tx, out tz);
                        double gx0 = t.Tile.X0 + x, gz0 = t.Tile.Z0 + z;
                        if ((gx0 - px) * (gx0 - px) + (gz0 - pz) * (gz0 - pz) > place.RadiusM * place.RadiusM) continue;
                        double nearEnd = System.Math.Min(s, c.RawLengthM - s);
                        if (cuts != null) foreach (var cu in cuts) nearEnd = System.Math.Min(nearEnd, System.Math.Abs(cu.S - s));
                        float half = 0.5f * prof.DrawnAt(s), shift = prof.ShiftAt(s);
                        foreach (double f in new[] { -0.7, -0.35, 0.0, 0.35, 0.7 })
                        {
                            double off = shift + f * half;
                            double wx = x - tz * off, wz = z + tx * off;
                            float y0;
                            if (!g.TryHeight(wx, wz, float.PositiveInfinity, out y0)) continue;
                            float r0;
                            bool centreRoad = gr.TryHeight(wx, wz, y0 + 0.01f, out r0) && System.Math.Abs(r0 - y0) < 0.005f;
                            var fh = new float[4];
                            bool okf = true;
                            for (int k = 0; k < 4; k++)
                            {
                                int a = (k & 1) != 0 ? 1 : -1, b = (k & 2) != 0 ? 1 : -1;
                                if (!g.TryHeight(wx + tx * 0.13 * a + tz * 0.11 * b, wz + tz * 0.13 * a - tx * 0.11 * b, y0, out fh[k]) || System.Math.Abs(fh[k] - y0) > 0.5f) okf = false;
                            }
                            if (!okf) continue;
                            double pa = (fh[1] + fh[3] - fh[0] - fh[2]) / 4.0, pb = (fh[2] + fh[3] - fh[0] - fh[1]) / 4.0;
                            for (int k = 0; k < 4; k++)
                            {
                                int a = (k & 1) != 0 ? 1 : -1, b = (k & 2) != 0 ? 1 : -1;
                                double cx = wx + tx * 0.13 * a + tz * 0.11 * b, cz = wz + tz * 0.13 * a - tx * 0.11 * b;
                                float gy = fh[k] - (float)(a * pa + b * pb);
                                if (System.Math.Abs(y0 - gy) <= 0.03f) continue;
                                float ry;
                                bool footRoad = gr.TryHeight(cx, cz, gy + 0.01f, out ry) && System.Math.Abs(ry - gy) < 0.005f;
                                Ghumante.Core.Meshing.Roads.RoadSurfaceHit hit;
                                bool qh = q.TrySample(t.Tile.X0 + cx, t.Tile.Z0 + cz, y0, out hit);
                                string cause;
                                if (nearEnd < 6) cause = "near end or cut";
                                else if (!centreRoad) cause = "centre on terrain (poke)";
                                else if (!footRoad) cause = qh ? "foot on terrain, query says road " + (hit.Road == i ? "same" : "other") : "foot on terrain, off road";
                                else if (qh && hit.Road != i && hit.Road >= 0) cause = "foot on another road";
                                else if (qh && hit.OnJunction) cause = "junction";
                                else cause = "same road step";
                                if (cause != "near end or cut" && !cause.EndsWith("other") && cause != "foot on another road")
                                    tops.Add(new System.Collections.Generic.KeyValuePair<double, string>(System.Math.Abs(y0 - gy), string.Format(ic, "{0} {1} road {2} {3} s {4:0} f {5} at {6:0.00},{7:0.00} off {8:0.000} | {9} | q {10} road {11} lat {12:0.00}/{13:0.00} y {14:0.000}",
                                        cause, place.Name, i, t.Roads[i].RoadClass, s, f, cx, cz, y0 - gy, g.All(cx, cz), qh ? 1 : 0, hit.Road, hit.LateralM, hit.HalfWidthM, hit.Y)));
                                counts.TryGetValue(cause, out int n0);
                                counts[cause] = n0 + 1;
                                if (!examples.ContainsKey(cause) || System.Math.Abs(y0 - gy) > 0.2)
                                    examples[cause] = string.Format(ic, "{0} road {1} {2} s {3:0} f {4} at {5:0.0},{6:0.0} off {7:0.000} | {8}", place.Name, i, t.Roads[i].RoadClass, s, f, cx, cz,
                                        y0 - gy, g.All(cx, cz));
                            }
                        }
                    }
                }
            }
            foreach (var kv in counts) sb.AppendLine(kv.Key + ": " + kv.Value + "   e.g. " + examples[kv.Key]);
            tops.Sort((a, b) => b.Key.CompareTo(a.Key));
            for (int k = 0; k < System.Math.Min(14, tops.Count); k++) sb.AppendLine("  top " + tops[k].Value);
            TestContext.Progress.WriteLine(sb.ToString());
        }
    }
}
namespace Ghumante.Core.Tests
{
    /// <summary>Diagnostics (CUTDIAG_ROADTRIS=1): road mesh triangles over the sample's detail tiles (High and Low options).</summary>
    public class RoadCutDiagRoadTris
    {
        [Test]
        public void RoadTris()
        {
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("CUTDIAG_ROADTRIS"))) Assert.Pass();
            long hi = 0, lo = 0;
            foreach (var id in StreamingSampleRegion.TilesAt(10))
            {
                var t = StreamingSampleRegion.Tile(id);
                if (t == null || t.Roads.Count == 0) continue;
                var h = Ghumante.Core.Meshing.TileHeightSampler.ForArea(t, t.Tile, 2);
                var m = new Ghumante.Core.Meshing.MeshData();
                Ghumante.Core.Meshing.RoadMesher.Build(t, h, new Ghumante.Core.Meshing.RoadOptions(), m);
                hi += m.TriangleCount;
                var h4 = Ghumante.Core.Meshing.TileHeightSampler.ForArea(t, t.Tile, 4);
                var m2 = new Ghumante.Core.Meshing.MeshData();
                Ghumante.Core.Meshing.RoadMesher.Build(t, h4, new Ghumante.Core.Meshing.RoadOptions { Detail = false }, m2);
                lo += m2.TriangleCount;
            }
            TestContext.Progress.WriteLine("road triangles: detail " + hi + " low " + lo);
        }
    }
}
