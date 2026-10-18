using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>Scratch diagnostics (not committed).</summary>
    public class ZzCutfillScratch
    {
        private static List<double[]>[] Lists(TileData t, MeshData m)
        {
            var lists = new List<double[]>[4];
            for (int b = 0; b < 4; b++) lists[b] = new List<double[]>();
            float size = (float)t.Tile.Size;
            var used = new bool[m.VertexCount];
            for (int k = 0; k < m.IndexCount; k++) used[m.Indices[k]] = true;
            for (int v = 0; v < m.VertexCount; v++)
            {
                if (!used[v]) continue;
                float x = m.Positions[3 * v], y = m.Positions[3 * v + 1], z = m.Positions[3 * v + 2];
                int ch = m.HasUv0 ? (int)Math.Round(m.Uv0[2 * v]) : -1;
                if (Math.Abs(x) < 1e-3f) lists[0].Add(new[] { t.Tile.Z0 + z, (double)y, ch, x, z });
                if (Math.Abs(x - size) < 1e-3f) lists[1].Add(new[] { t.Tile.Z0 + z, (double)y, ch, x, z });
                if (Math.Abs(z) < 1e-3f) lists[2].Add(new[] { t.Tile.X0 + x, (double)y, ch, x, z });
                if (Math.Abs(z - size) < 1e-3f) lists[3].Add(new[] { t.Tile.X0 + x, (double)y, ch, x, z });
            }
            foreach (List<double[]> l in lists) l.Sort((a, b) => a[0] != b[0] ? a[0].CompareTo(b[0]) : a[1].CompareTo(b[1]));
            return lists;
        }

        [Test]
        public void Seams()
        {
            if (Environment.GetEnvironmentVariable("ZZ_SEAMS") == null) Assert.Pass();
            var ic = CultureInfo.InvariantCulture;
            var o = new RoadOptions();
            var borders = new Dictionary<ulong, List<double[]>[]>();
            var tiles = new Dictionary<ulong, TileData>();
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (t == null || t.Roads.Count == 0) continue;
                var m = new MeshData();
                RoadMesher.Build(t, new TileHeightSampler(t, 2), o, m);
                borders[t.Tile.Key] = Lists(t, m);
                tiles[t.Tile.Key] = t;
            }
            var sb = new StringBuilder();
            int shown = 0, total = 0;
            var hist = new int[8];
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                List<double[]>[] a;
                if (!borders.TryGetValue(id.Key, out a)) continue;
                for (int dir = 0; dir < 2; dir++)
                {
                    TileId nb = dir == 0 ? new TileId(10, id.Tx + 1, id.Ty) : new TileId(10, id.Tx, id.Ty + 1);
                    List<double[]>[] b;
                    if (!borders.TryGetValue(nb.Key, out b)) continue;
                    List<double[]> la = dir == 0 ? a[1] : a[3], lb = dir == 0 ? b[0] : b[2];
                    foreach (double[] p in la)
                    {
                        double best = double.MaxValue, by = 0, bch = -1;
                        foreach (double[] q in lb)
                        {
                            if (Math.Abs(q[0] - p[0]) > 0.01) continue;
                            double d = Math.Abs(q[1] - p[1]);
                            if (d < best) { best = d; by = q[1]; bch = q[2]; }
                        }
                        if (best <= 0.01) continue;
                        total++;
                        hist[best == double.MaxValue ? 7 : Math.Min(6, (int)(best / 0.05))]++;
                        if (shown++ < 60)
                            sb.AppendLine(string.Format(ic, "{0}->{1} at {2:0.00} y {3:0.000} ch {4} local {5:0.0},{6:0.0}: partner {7} (dy {8:0.000}, ch {9})", id, nb, p[0], p[1], p[2], p[3], p[4],
                                best == double.MaxValue ? "none" : by.ToString("0.000", ic), best == double.MaxValue ? 0 : best, bch));
                    }
                }
            }
            sb.AppendLine("unmatched (1 cm) east/north side: " + total + " hist(5cm bins, last none): " + string.Join(",", hist));
            TestContext.Progress.WriteLine(sb.ToString());
        }
    
        [Test]
        public void SeamEnds()
        {
            string spec = Environment.GetEnvironmentVariable("ZZ_SEAMENDS"); // tx,ty,dir(0 east,1 north),lo,hi
            if (spec == null) Assert.Pass();
            var ic = CultureInfo.InvariantCulture;
            string[] q = spec.Split(',');
            var id = new TileId(10, int.Parse(q[0]), int.Parse(q[1]));
            int dir = int.Parse(q[2]);
            double lo = double.Parse(q[3], ic), hi = double.Parse(q[4], ic);
            var nb = dir == 0 ? new TileId(10, id.Tx + 1, id.Ty) : new TileId(10, id.Tx, id.Ty + 1);
            var sb = new StringBuilder();
            foreach (TileId tid in new[] { id, nb })
            {
                TileData t = StreamingSampleRegion.Tile(tid);
                var h = new TileHeightSampler(t, 2);
                var o = new RoadOptions();
                RoadGrade g = RoadGrade.For(t, h, o);
                RoadLayout lay = RoadLayout.For(t);
                for (int i = 0; i < t.Roads.Count; i++)
                {
                    RoadCentreline c = lay.Centres[i];
                    if (c == null || c.Count < 2) continue;
                    for (int end = 0; end < 2; end++)
                    {
                        double s = end == 0 ? 0 : c.RawLengthM;
                        double x, z, tx, tz;
                        c.At(s, out x, out z, out tx, out tz);
                        double gx = t.Tile.X0 + x, gz = t.Tile.Z0 + z;
                        bool onB = dir == 0 ? Math.Abs(gx - (id.X0 + 1024)) < 0.01 : Math.Abs(gz - (id.Z0 + 1024)) < 0.01;
                        double along = dir == 0 ? gz - id.Z0 : gx - id.X0;
                        if (!onB || along < lo || along > hi) continue;
                        RoadWidthProfile prof = lay.Profiles[i];
                        float half = 0.5f * prof.DrawnAt(s), shift = prof.ShiftAt(s);
                        sb.Append(string.Format(ic, "{0} road {1} {2} end {3} at {4:0.000},{5:0.000} t {6:0.000},{7:0.000} half {8:0.00} shift {9:0.00} nat {10:0.000} has {11} abs {12}:",
                            tid, i, t.Roads[i].RoadClass, end, gx, gz, tx, tz, half, shift, g.NaturalTerrain(x, z), g.Has(i), g.Has(i) && g.IsAbsolute(i)));
                        foreach (double f in new[] { -1.0, -0.5, 0.0, 0.5, 1.0 })
                        {
                            double off = shift + f * half;
                            sb.Append(string.Format(ic, " {0:0.000}", g.Has(i) ? g.SurfaceY(i, s, off, x - tz * off, z + tx * off) : float.NaN));
                        }
                        sb.AppendLine();
                    }
                }
            }
            TestContext.Progress.WriteLine(sb.ToString());
        }
    
        [Test]
        public void TerrainSeams()
        {
            if (Environment.GetEnvironmentVariable("ZZ_TSEAMS") == null) Assert.Pass();
            var ic = CultureInfo.InvariantCulture;
            var borders = new Dictionary<ulong, List<double[]>[]>();
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (t == null) continue;
                var m = new MeshData();
                TerrainMesher.Build(t, t.Tile, new TerrainOptions { Step = 2, SkirtDepthM = 0f, Walls = false }, m);
                borders[t.Tile.Key] = Lists(t, m);
            }
            var sb = new StringBuilder();
            int shown = 0, total = 0, all = 0;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                List<double[]>[] a;
                if (!borders.TryGetValue(id.Key, out a)) continue;
                for (int dir = 0; dir < 2; dir++)
                {
                    TileId nb = dir == 0 ? new TileId(10, id.Tx + 1, id.Ty) : new TileId(10, id.Tx, id.Ty + 1);
                    List<double[]>[] b;
                    if (!borders.TryGetValue(nb.Key, out b)) continue;
                    List<double[]> la = dir == 0 ? a[1] : a[3], lb = dir == 0 ? b[0] : b[2];
                    // Every vertex of either side must lie on the other side's polyline (linear between its vertices).
                    foreach (var pair in new[] { (la, lb), (lb, la) })
                    foreach (double[] p in pair.Item1)
                    {
                        all++;
                        List<double[]> other = pair.Item2;
                        double y = double.NaN;
                        for (int k = 0; k + 1 < other.Count; k++)
                        {
                            if (other[k][0] <= p[0] + 1e-6 && other[k + 1][0] >= p[0] - 1e-6)
                            {
                                double f = other[k + 1][0] - other[k][0] < 1e-9 ? 0 : (p[0] - other[k][0]) / (other[k + 1][0] - other[k][0]);
                                y = other[k][1] + (other[k + 1][1] - other[k][1]) * Math.Max(0, Math.Min(1, f));
                                break;
                            }
                        }
                        if (double.IsNaN(y) || Math.Abs(y - p[1]) <= 0.01) continue;
                        total++;
                        if (shown++ < 40)
                            sb.AppendLine(string.Format(ic, "{0}/{1} at {2:0.00}: y {3:0.000} other {4:0.000}", id, nb, p[0], p[1], y));
                    }
                }
            }
            sb.AppendLine("terrain border vertices off the neighbour's edge by > 1 cm: " + total + " of " + all);
            TestContext.Progress.WriteLine(sb.ToString());
        }
    
        [Test]
        public void BorderVerts()
        {
            string spec = Environment.GetEnvironmentVariable("ZZ_BVERTS"); // tx,ty,dir,lo,hi
            if (spec == null) Assert.Pass();
            var ic = CultureInfo.InvariantCulture;
            string[] q = spec.Split(',');
            var id = new TileId(10, int.Parse(q[0]), int.Parse(q[1]));
            int dir = int.Parse(q[2]);
            double lo = double.Parse(q[3], ic), hi = double.Parse(q[4], ic);
            var nb = dir == 0 ? new TileId(10, id.Tx + 1, id.Ty) : new TileId(10, id.Tx, id.Ty + 1);
            var sb = new StringBuilder();
            foreach (TileId tid in new[] { id, nb })
            {
                TileData t = StreamingSampleRegion.Tile(tid);
                var m = new MeshData();
                RoadMesher.Build(t, new TileHeightSampler(t, 2), new RoadOptions(), m);
                var tm = new MeshData();
                TerrainMesher.Build(t, t.Tile, new TerrainOptions { Step = 2, SkirtDepthM = 0f, Walls = false }, tm);
                foreach (var (name, mesh) in new[] { ("road", m), ("terrain", tm) })
                for (int v = 0; v < mesh.VertexCount; v++)
                {
                    double gx = t.Tile.X0 + mesh.Positions[3 * v], gz = t.Tile.Z0 + mesh.Positions[3 * v + 2];
                    bool onB = dir == 0 ? Math.Abs(gx - (id.X0 + 1024)) < 0.002 : Math.Abs(gz - (id.Z0 + 1024)) < 0.002;
                    double along = dir == 0 ? gz - id.Z0 : gx - id.X0;
                    if (!onB || along < lo || along > hi) continue;
                    sb.AppendLine(string.Format(ic, "{0} {1} v{2} along {3:0.000} y {4:0.000} ny {5:0.00} ch {6}", tid, name, v, along, mesh.Positions[3 * v + 1], mesh.Normals[3 * v + 1],
                        mesh.HasUv0 ? (int)Math.Round(mesh.Uv0[2 * v]) : -1));
                }
            }
            TestContext.Progress.WriteLine(sb.ToString());
        }
    
        [Test]
        public void RoadAt()
        {
            string spec = Environment.GetEnvironmentVariable("ZZ_ROADAT"); // tx,ty,road,s;...
            if (spec == null) Assert.Pass();
            var ic = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            foreach (string item in spec.Split(';'))
            {
                string[] q = item.Split(',');
                var id = new TileId(10, int.Parse(q[0]), int.Parse(q[1]));
                int i = int.Parse(q[2]);
                double s0 = double.Parse(q[3], ic);
                TileData t = StreamingSampleRegion.Tile(id);
                var h = new TileHeightSampler(t, 2);
                RoadGrade g = RoadGrade.For(t, h, new RoadOptions());
                RoadLayout lay = RoadLayout.For(t);
                RoadCentreline c = lay.Centres[i];
                NameRecord nm = t.Name(t.Roads[i].NameRef);
                sb.AppendLine(string.Format(ic, "{0} road {1} {2} '{3}' len {4:0.0} flags {5} lowered {6} drawsRibbon {7} cuts {8} knees {9}/{10} abs {11} synth {12} struct {13}", id, i, t.Roads[i].RoadClass, nm != null ? nm.Default : "",
                    c.RawLengthM, t.Roads[i].Flags, g.IsLowered(i), lay.DrawsRibbon(i), lay.Cuts[i] == null ? 0 : lay.Cuts[i].Length, lay.Knees[2 * i].Has, lay.Knees[2 * i + 1].Has,
                    g.IsAbsolute(i), g.IsSyntheticDeck(i), t.RoadStructureOf(i).Kind));
                if (lay.Cuts[i] != null)
                    foreach (RoadCut cu in lay.Cuts[i])
                    {
                        int pad = g.Pads.PadOfCut(i, cu.S);
                        sb.Append(string.Format(ic, "  cut s {0:0.0} c {1:0.0},{2:0.0} pad {3}", cu.S, cu.CX, cu.CZ, pad));
                        if (pad >= 0) sb.Append(string.Format(ic, " padH {0:0.000} hill {1} ring {2}", g.Pads.Pads[pad].Height(cu.CX, cu.CZ), g.Pads.Pads[pad].Hill, g.Pads.Pads[pad].Ring));
                        sb.AppendLine();
                    }
                if (Environment.GetEnvironmentVariable("ZZ_PINS") != null) sb.Append(g.ExplainPins(i, s0));
                for (double s = Math.Max(0, s0 - 10); s <= Math.Min(c.RawLengthM, s0 + 10); s += 1)
                {
                    double x, z, tx, tz;
                    c.At(s, out x, out z, out tx, out tz);
                    float y = g.SurfaceY(i, s, 0, x, z);
                    float ter = g.Terrain(x, z), nat = g.NaturalTerrain(x, z);
                    RoadGrade.Row r = g.RowAt(i, s);
                    float b;
                    double gx, gz;
                    bool ext = h.Carve != null && h.Carve.ExtentAt(x, z, out b, out gx, out gz);
                    sb.AppendLine(string.Format(ic, "  s {0:0.0} at {1:0.0},{2:0.0}: surf {3:0.000} ter {4:0.000} nat {5:0.000} P {6:0.000} B {7:0.000} pinW {8:0.00} pinY {9:0.000} deckW {10:0.00} ext {11} gap {12} absorbed {13} ondeck {14} drawn {15:0.00}",
                        s, x, z, y, ter, nat, r.P, r.B, r.PinW, r.PinAbsY, r.DeckW, ext, lay.InGap(i, s), lay.InAbsorbed(i, s), g.OnDeck(i, s), lay.Profiles[i].DrawnAt(s)));
                }
            }
            TestContext.Progress.WriteLine(sb.ToString());
        }
    
        [Test]
        public void Ripple()
        {
            if (Environment.GetEnvironmentVariable("ZZ_RIPPLE") == null) Assert.Pass();
            var A = new TileId(10, 516, 161);
            Func<double, double, double> sm = (x, z) => 1300 + 6 * Math.Sin(x / 170.0) + 4 * Math.Cos(z / 130.0) + 0.01 * (x - 528000);
            Func<double, double, double> f = (x, z) => sm(x, z) + 0.25 * Math.Sin(x * 0.9) + 0.02 * (z - A.Z0);
            TileData t = MeshingChecks.SyntheticTile(A, f, 513);
            t.Roads.Add(new RoadRecord { OsmWayId = 4242, RoadClass = RoadClass.Secondary, Surface = Surface.Asphalt, Points = new[] { 10000, 40000, 50000, 42000, 90000, 60000 } });
            var h = new TileHeightSampler(t, 1);
            var o = new RoadOptions();
            RoadGrade g = RoadGrade.For(t, h, o);
            RoadCentreline c = RoadLayout.For(t).Centres[0];
            var ic = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            var m = new MeshData();
            RoadMesher.Build(t, h, o, m);
            float lift0 = g.LiftOf(0);
            for (int tri = 0; tri < m.TriangleCount; tri++)
            {
                double fx, fy, fz;
                MeshingChecks.Facet(m, tri, out fx, out fy, out fz);
                double len = Math.Sqrt(fx * fx + fy * fy + fz * fz);
                if (len < 1e-9 || fy < 0.9 * len) continue;
                for (int k = 0; k < 3; k++)
                {
                    int v = m.Indices[3 * tri + k];
                    float x = m.Positions[3 * v], y = m.Positions[3 * v + 1], z = m.Positions[3 * v + 2];
                    float ter = g.Terrain(x, z);
                    if (y >= ter + lift0 - RoadGrade.MaxBuryM - 2e-3f) continue;
                    float b; double gx2, gz2;
                    bool bench = h.Carve.BenchAt(x / h.Grid.CellM, z / h.Grid.CellM, out b, out gx2, out gz2);
                    sb.AppendLine(string.Format(ic, "buried v{0} at {1:0.00},{2:0.00} y {3:0.000} ter {4:0.000} nat {5:0.000} bench {6} ch {7} ny {8:0.00}", v, x, z, y, ter, g.NaturalTerrain(x, z), bench,
                        (int)Math.Round(m.Uv0[2 * v]), m.Normals[3 * v + 1]));
                }
            }
            for (double s = 0; s <= 30; s += 1)
            {
                double x, z, tx, tz;
                c.At(s, out x, out z, out tx, out tz);
                RoadGrade.Row r = g.RowAt(0, s);
                sb.AppendLine(string.Format(ic, "s {0} y {1:0.000} nat {2:0.000} ter {3:0.000} P {4:0.000} B {5:0.000} lift {6:0.00}", s, g.SurfaceY(0, s, 0, x, z), g.NaturalTerrain(x, z), g.Terrain(x, z), r.P, r.B, g.LiftOf(0)));
            }
            TestContext.Progress.WriteLine(sb.ToString());
        }
    
        [Test]
        public void AreaOff()
        {
            string spec = Environment.GetEnvironmentVariable("ZZ_AREAOFF");
            if (spec == null) Assert.Pass();
            var ic = CultureInfo.InvariantCulture;
            string[] q = spec.Split(',');
            var id = new TileId(10, int.Parse(q[0]), int.Parse(q[1]));
            TileData t = StreamingSampleRegion.Tile(id);
            var s = new TileHeightSampler(t, 2);
            var o = new AreaOptions { IncludeSubtle = true, WaterBanks = false, FieldLines = false };
            var sb = new StringBuilder();
            int shown = 0;
            foreach (AreaRecord a in t.Areas)
            {
                var one = new TileData { Tile = t.Tile, HeightsN = t.HeightsN, HeightsQ = t.HeightsQ };
                one.Areas.Add(a);
                var m = new MeshData();
                if (AreaMesher.Build(one, s, o, m) == 0) continue;
                float lift = AreaMesher.LiftOf(a, o);
                for (int v = 0; v < m.VertexCount; v++)
                {
                    float x = m.Positions[3 * v], z = m.Positions[3 * v + 2], h;
                    s.TryHeightClamped(id.X0 + x, id.Z0 + z, out h);
                    double d = m.Positions[3 * v + 1] - h - lift;
                    if (Math.Abs(d) <= 1e-3 || shown++ > 20) continue;
                    double u = x / s.Grid.CellM, w = z / s.Grid.CellM;
                    float b;
                    double gx, gz;
                    bool bench = s.Carve.BenchAt(u, w, out b, out gx, out gz);
                    sb.AppendLine(string.Format(ic, "area {0} {1} v{2} at {3:0.000},{4:0.000} mesh {5:0.000} sampler+lift {6:0.000} d {7:0.000} bench {8} refined {9}", a.OsmRef, a.Kind, v, x, z,
                        m.Positions[3 * v + 1], h + lift, d, bench, s.Carve.IsRefined((int)Math.Floor(u), (int)Math.Floor(w))));
                }
            }
            TestContext.Progress.WriteLine(sb.ToString());
        }
    
        [Test]
        public void Floats()
        {
            if (Environment.GetEnvironmentVariable("ZZ_FLOATS") == null) Assert.Pass();
            var ic = CultureInfo.InvariantCulture;
            var list = new List<KeyValuePair<double, string>>();
            int extTrue = 0, extFalse = 0, over1 = 0, over2 = 0;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (t == null || t.Roads.Count == 0) continue;
                var h = new TileHeightSampler(t, 2);
                RoadGrade g = RoadGrade.For(t, h, new RoadOptions());
                RoadLayout lay = RoadLayout.For(t);
                for (int i = 0; i < t.Roads.Count; i++)
                {
                    RoadRecord r = t.Roads[i];
                    if (!g.Has(i) || !lay.DrawsRibbon(i) || g.IsAbsolute(i) || (r.Flags & RoadFlags.Bridge) != 0 || g.IsLowered(i)) continue;
                    RoadCentreline c = lay.Centres[i];
                    RoadWidthProfile prof = lay.Profiles[i];
                    float lift = g.LiftOf(i);
                    double worst = 0, ws = 0; string wwhat = "";
                    for (double s = 1; s < c.RawLengthM - 1; s += 2)
                    {
                        if (g.OnDeck(i, s) || lay.InGap(i, s)) continue;
                        double x, z, tx, tz;
                        c.At(s, out x, out z, out tx, out tz);
                        double ux = -tz, uz = tx;
                        float shift = prof.ShiftAt(s), half = 0.5f * prof.DrawnAt(s);
                        for (int side = -1; side <= 1; side++)
                        {
                            double off = shift + side * Math.Max(0, half - (Environment.GetEnvironmentVariable("ZZ_IN") != null ? 0.15 : 0.0)), ex = x + ux * off, ez = z + uz * off;
                            double d = g.SurfaceY(i, s, off, ex, ez) - (g.Terrain(ex, ez) + lift);
                            if (d <= worst) continue;
                            worst = d;
                            ws = s;
                            float b; double gx, gz;
                            bool ext = h.Carve != null && h.Carve.ExtentAt(ex, ez, out b, out gx, out gz);
                            wwhat = string.Format(ic, "side {0} ext {1} absorbed {2} at {3:0.0},{4:0.0} nat {5:0.00} ter {6:0.00}", side, ext, lay.InAbsorbed(i, s), ex, ez, g.NaturalTerrain(ex, ez), g.Terrain(ex, ez));
                        }
                    }
                    if (worst > 0.5)
                    {
                        list.Add(new KeyValuePair<double, string>(worst, string.Format(ic, "{0:0.00} {1} road {2} {3} at {4:0} len {5:0}: {6}", worst, id, i, r.RoadClass, ws, c.RawLengthM, wwhat)));
                        if (wwhat.Contains("ext True")) extTrue++;
                        else extFalse++;
                        if (worst > 2) over2++;
                        else if (worst > 1) over1++;
                    }
                }
            }
            list.Sort((a, b) => b.Key.CompareTo(a.Key));
            var sb = new StringBuilder();
            sb.AppendLine("pieces with an edge or centre over 0.5 m above the drawn ground: " + list.Count + " (ext " + extTrue + ", no ext " + extFalse + "; over 1 m " + over1 + ", over 2 m " + over2 + ")");
            for (int k = 0; k < Math.Min(30, list.Count); k++) sb.AppendLine(list[k].Value);
            TestContext.Progress.WriteLine(sb.ToString());
        }

        private static bool PIn(double[] px, double[] pz, int n, double x, double z)
        {
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
                if ((pz[i] > z) != (pz[j] > z) && x < (px[j] - px[i]) * (z - pz[i]) / (pz[j] - pz[i]) + px[i]) inside = !inside;
            return inside;
        }

        [Test]
        public void QueryOff()
        {
            string spec = Environment.GetEnvironmentVariable("ZZ_QUERYOFF");
            if (spec == null) Assert.Pass();
            RoadGrade.FollowPinsOn = Environment.GetEnvironmentVariable("ZZ_NOFOLLOW") == null;
            var ic = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            foreach (string item in spec.Split(';'))
            {
                string[] q0 = item.Split(',');
                TileData t = StreamingSampleRegion.Tile(new TileId(10, int.Parse(q0[0]), int.Parse(q0[1])));
                var h = new TileHeightSampler(t, 2);
                var o = new RoadOptions();
                var m = new MeshData();
                RoadMesher.Build(t, h, o, m);
                RoadSurfaceQuery q = RoadSurfaceQuery.For(t, h, o);
                RoadGrade g = RoadGrade.For(t, h, o);
                RoadLayout lay = RoadLayout.For(t);
                var list = new List<KeyValuePair<double, string>>();
                int off = 0, inCap = 0, inRing = 0, roadOff = 0;
                var byRoad = new Dictionary<int, int>();
                for (int v = 0; v < m.VertexCount; v++)
                {
                    if (m.Normals[3 * v + 1] < 0.97f || (int)Math.Round(m.Uv0[2 * v]) != (int)MaterialChannel.Asphalt) continue;
                    float x = m.Positions[3 * v], y = m.Positions[3 * v + 1], z = m.Positions[3 * v + 2];
                    if (x < 1 || z < 1 || x > t.Tile.Size - 1 || z > t.Tile.Size - 1) continue;
                    RoadSurfaceHit hit;
                    if (!q.TrySample(t.Tile.X0 + x, t.Tile.Z0 + z, y + 0.2f, out hit) || !hit.OnCarriageway) continue;
                    double d = Math.Abs(hit.Y - y);
                    if (d <= RibbonMesher.RowToleranceM + 0.02) continue;
                    off++;
                    bool cap = false;
                    foreach (JunctionCap c in lay.Caps) if (PIn(c.PolyX, c.PolyZ, c.Count, x, z)) cap = true;
                    string ringInfo = "";
                    foreach (RoadRing rg in lay.Rings)
                    {
                        double rr = Math.Sqrt((x - rg.X) * (x - rg.X) + (z - rg.Z) * (z - rg.Z));
                        if (rr <= rg.OuterRadiusM + rg.FlareRadiusM + 20) ringInfo += string.Format(ic, " ring r {0:0.00} in {1:0.0} out {2:0.0}", rr, rg.InnerRadiusM, rg.OuterRadiusM);
                    }
                    float eb = 0f; double egx, egz;
                    bool ext = h.Carve != null && h.Carve.ExtentAt(x, z, out eb, out egx, out egz);
                    ringInfo += string.Format(ic, " ext {0} bench {1:0.000}", ext, eb);
                    if (cap) inCap++;
                    if (hit.Road < 0 && !cap) inRing++;
                    if (hit.Road >= 0) { roadOff++; int n0; byRoad.TryGetValue(hit.Road, out n0); byRoad[hit.Road] = n0 + 1; }
                    list.Add(new KeyValuePair<double, string>(d, string.Format(ic, "{0:0.000} at {1:0.000},{2:0.000}: mesh {3:0.000} hit {4:0.000} road {5} junction {6} cap {7} along {8:0.0} lat {9:0.0} ter {10:0.000} nat {11:0.000}",
                        d, x, z, y, hit.Y, hit.Road, hit.OnJunction, cap, hit.AlongM, hit.LateralM, g.Terrain(x, z), g.NaturalTerrain(x, z)) + ringInfo));
                }
                list.Sort((a, b) => b.Key.CompareTo(a.Key));
                sb.AppendLine(t.Tile + ": off " + off + " (in cap " + inCap + ", ring " + inRing + ", ribbon " + roadOff + ")");
                foreach (var e in byRoad) if (e.Value > 20) sb.AppendLine("  road " + e.Key + " " + t.Roads[e.Key].RoadClass + ": " + e.Value);
                for (int k = 0; k < Math.Min(25, list.Count); k += 2) sb.AppendLine("  " + list[k].Value);
                for (int k = 25; k < list.Count; k += Math.Max(1, list.Count / 20)) sb.AppendLine("  " + list[k].Value);
            }
            TestContext.Progress.WriteLine(sb.ToString());
        }
    }
}
