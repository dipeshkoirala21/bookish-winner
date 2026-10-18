using System;
using System.Collections.Generic;
using System.Text;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing.Bridges;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Owner playtest #2 and #15 (docs/W2_PLAYTEST_1.md): walk and ride across every bridge of the sample, walk and ride
    /// under every one with a road beneath, climb every foot overbridge, on the world built the way the game streams it
    /// (<see cref="BridgeWorld"/>), with the real walker and motorbike (<see cref="ArcadeVehicle"/>).
    /// </summary>
    public class BridgeCrossingTests
    {
        /// <summary>Tiles with bridges; BRIDGE_TILES = "tx,ty;tx,ty" narrows the probes to those tiles.</summary>
        private static IEnumerable<TileId> BridgeTiles()
        {
            string only = Environment.GetEnvironmentVariable("BRIDGE_TILES");
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                if (!string.IsNullOrEmpty(only) && !(";" + only + ";").Contains(";" + id.Tx + "," + id.Ty + ";")) continue;
                TileData t = StreamingSampleRegion.Tile(id);
                if (!t.HasDetail) continue;
                BridgeLayout l = BridgeLayout.For(t);
                if (l.Spans.Count > 0) yield return id;
            }
        }

        private static string Name(TileData t, BridgeSpan sp)
        {
            NameRecord n = t.Name(sp.Record.NameRef);
            return sp.Record.OsmWayId + " '" + (n != null ? n.Default : "") + "' " + sp.Record.RoadClass;
        }

        /// <summary>Trace walks: BRIDGE_TRACE = "tx,ty,way[,mode[,lane|ride]]" (mode: walk, back, ride, under:lowerWay,
        /// stair:i:up|down), several separated by ';'.</summary>
        [Test, Explicit]
        public void TraceOne()
        {
            string specs = Environment.GetEnvironmentVariable("BRIDGE_TRACE");
            if (string.IsNullOrEmpty(specs)) Assert.Ignore("set BRIDGE_TRACE");
            foreach (string spec in specs.Split(';')) Trace(spec);
        }

        private static void Trace(string spec)
        {
            TestContext.WriteLine("==== trace " + spec);
            string[] a = spec.Split(',');
            var id = new TileId(10, int.Parse(a[0]), int.Parse(a[1]));
            ulong way = ulong.Parse(a[2]);
            string mode = a.Length > 3 ? a[3] : "walk";
            BridgeWorld w = BridgeWorld.Around(new[] { id }, 1);
            TileData t = w.Tile(id);
            foreach (BridgeSpan sp in BridgeLayout.For(t).Spans)
            {
                if (sp.Record.OsmWayId != way) continue;
                var trace = new StringBuilder();
                double before = sp.StartEnd == SpanEnd.Ground ? 8 : 3, after = sp.EndEnd == SpanEnd.Ground ? 8 : 3;
                ProbeRun r;
                if (mode.StartsWith("stair"))
                {
                    // stair:i:up|down
                    string[] m = mode.Split(':');
                    BridgeStair st = sp.Stairs[int.Parse(m[1])];
                    bool up = m[2] == "up";
                    TestContext.WriteLine("flight top (" + (t.Tile.X0 + st.X).ToString("F1") + ", " + (t.Tile.Z0 + st.Z).ToString("F1") + ") dir (" + st.Dx.ToString("F2") + ", "
                                          + st.Dz.ToString("F2") + ") top " + st.TopY.ToString("F2") + " bottom " + st.BottomY.ToString("F2") + " len " + st.Length.ToString("F2")
                                          + " half " + st.HalfWidth.ToString("F2") + " ends " + sp.StartEnd + "/" + sp.EndEnd);
                    r = BridgeProbe.Follow(w, VehicleSpec.Walker(), BridgeProbe.Flight(st, t.Tile, up), 1f,
                                           (up ? st.SurfaceAt(st.Length - 0.25) : st.SurfaceAt(0.25)) + 0.05f, 60f, trace);
                }
                else if (mode.StartsWith("under"))
                {
                    ulong low = ulong.Parse(mode.Substring(6));
                    BridgeCrossing c = sp.Crossings.Find(x => t.Roads[x.Road].OsmWayId == low);
                    bool ride = mode.Length > 0 && a.Length > 4;
                    r = BridgeProbe.Follow(w, ride ? VehicleSpec.Motorbike() : VehicleSpec.Walker(), BridgeProbe.RoadAround(t, c.Road, c.X, c.Z, 30, Math.Min(1.5, 0.3 * c.LowHalfM)),
                                           ride ? 0.5f : 1f, c.LowY + 0.3f, 120f, trace);
                }
                else
                {
                    bool ride = mode == "ride";
                    var lane = a.Length > 4 ? (BridgeProbe.Lane)int.Parse(a[4]) : BridgeProbe.Lane.LeftLane;
                    r = BridgeProbe.Follow(w, ride ? VehicleSpec.Motorbike() : VehicleSpec.Walker(), BridgeProbe.Along(sp, t.Tile, lane, before, after, mode == "back"),
                                           ride ? 0.5f : 1f, float.NaN, 120f, trace);
                }
                TestContext.WriteLine(Name(t, sp) + " " + mode + ": " + r);
                TestContext.WriteLine("span stations (drawn top / ground query at the centre and at the left walk):");
                for (int k = 0; k < sp.Count; k++)
                {
                    double cx = t.Tile.X0 + sp.Path.X[k], cz = t.Tile.Z0 + sp.Path.Z[k];
                    float top;
                    int layer;
                    GroundSample g;
                    string c = (w.DrawnBelow(cx, cz, sp.Path.Y[k] + 0.7f, out top, out layer) ? top.ToString("F2") + "TARB"[layer] : "-") + "/"
                               + (w.Ground.TrySample(cx, cz, sp.Path.Y[k] + 0.05f, out g) ? g.Height.ToString("F2") + (g.OnDeck ? "d" : "") : "none");
                    TestContext.WriteLine("  centre " + c);
                }
                for (int k = 0; k < sp.Count; k++)
                    TestContext.WriteLine("  " + k + " (" + (t.Tile.X0 + sp.Path.X[k]).ToString("F1") + ", " + (t.Tile.Z0 + sp.Path.Z[k]).ToString("F1") + ") s " + sp.Path.S[k].ToString("F1")
                                          + " y " + sp.Path.Y[k].ToString("F2") + " gnd " + sp.Ground[k].ToString("F2") + " half " + sp.HalfL[k].ToString("F1") + "/" + sp.HalfR[k].ToString("F1")
                                          + " walk " + sp.WalkL[k].ToString("F1") + "/" + sp.WalkR[k].ToString("F1") + " deck " + sp.HasDeck(sp.Path.S[k]));
                TestContext.WriteLine(trace.ToString());
            }
        }

        /// <summary>Every flight of every foot structure (overbridge decks and their mapped stairs): climb it from 3 m beyond
        /// its foot to 1.5 m onto the deck, and walk down it again.</summary>
        [Test, Explicit]
        public void ProbeEveryStair()
        {
            BridgeWorld w = BridgeWorld.Around(BridgeTiles(), 1);
            var report = new StringBuilder();
            int flights = 0, upBad = 0, downBad = 0;
            foreach (TileId id in BridgeTiles())
            {
                TileData t = w.Tile(id);
                if (t == null || !t.HasDetail) continue;
                foreach (BridgeSpan sp in BridgeLayout.For(t).Spans)
                {
                    if (!Wanted(sp)) continue;
                    for (int i = 0; i < sp.Stairs.Count; i++)
                    {
                        BridgeStair st = sp.Stairs[i];
                        if (st.Steps <= 0) continue;
                        flights++;
                        List<double> up = BridgeProbe.Flight(st, t.Tile, true), down = BridgeProbe.Flight(st, t.Tile, false);
                        ProbeRun ru = BridgeProbe.Follow(w, VehicleSpec.Walker(), up, 1f, st.SurfaceAt(st.Length - 0.25) + 0.05f, 60f);
                        ProbeRun rd = BridgeProbe.Follow(w, VehicleSpec.Walker(), down, 1f, st.SurfaceAt(0.25) + 0.05f, 60f);
                        bool okUp = ru.Reached && ru.MaxY > st.TopY - 0.25f && ru.BridgeSink < DeepSinkM;
                        bool okDown = rd.Reached && rd.MinY < st.SurfaceAt(st.Length) + 0.25f && rd.BridgeSink < DeepSinkM;
                        if (!okUp) upBad++;
                        if (!okDown) downBad++;
                        report.Append(t.Tile).Append(' ').Append(Name(t, sp)).Append(" flight ").Append(i).Append(" rise ").Append((st.TopY - st.BottomY).ToString("F1"))
                              .Append(" steps ").Append(st.Steps).Append(" tread ").Append(st.Tread.ToString("F2")).Append(" riser ").Append(st.Rise.ToString("F2"))
                              .Append(" len ").Append(st.Length.ToString("F1")).Append(st.Mapped ? " mapped" : "").Append('\n');
                        report.Append("   up   ").Append(ru).Append('\n').Append(BridgeProbe.Why(w, ru)).Append("   down ").Append(rd).Append('\n').Append(BridgeProbe.Why(w, rd));
                    }
                }
            }
            TestContext.WriteLine("flights " + flights + ": up bad " + upBad + ", down bad " + downBad);
            TestContext.WriteLine(report.ToString());
        }

        /// <summary>
        /// The ground the walker gets on every bridge deck against what is drawn there (owner playtest #2: "characters get
        /// sucked in ... disappears when trying to cross the bridge"): inside the railings of every span, every 0.5 m
        /// along and 0.25 m across, the ground query from just above the deck must put the feet on the drawn surface: no
        /// drawn surface facing up within 2 m over the feet (a <b>sink</b>, <b>deep</b> past
        /// <see cref="DeepSinkM"/>; a <b>hole</b> when the feet fall more than half a metre through a drawn deck), and no
        /// more than 0.15 m of air under them (a <b>float</b>). Each bad cell is classified by the layer drawn over or
        /// under the feet (terrain, areas, roads, bridges), whether it lies beyond the span's own tile and whether another
        /// span covers it; a summary per class with examples, then the spans with holes or deep sinks.
        /// </summary>
        [Test, Explicit]
        public void ScanDecks()
        {
            BridgeWorld w = BridgeWorld.Around(BridgeTiles(), 1);
            DeckScan scan = Scan(w, false);
            TestContext.WriteLine(scan.Summary());
            TestContext.WriteLine(scan.Report.ToString());
        }

        /// <summary>The result of <see cref="Scan"/>.</summary>
        internal sealed class DeckScan
        {
            public int Cells, Sinks, Deep, Holes, Floats;
            public readonly SortedDictionary<string, int> Causes = new SortedDictionary<string, int>();
            public readonly Dictionary<string, string> Examples = new Dictionary<string, string>();
            public readonly StringBuilder Report = new StringBuilder();

            public string Summary()
            {
                var sb = new StringBuilder();
                sb.Append("cells ").Append(Cells).Append(": holes ").Append(Holes).Append(", deep sinks ").Append(Deep).Append(", sinks ").Append(Sinks)
                  .Append(", floats ").Append(Floats).Append('\n');
                foreach (KeyValuePair<string, int> kv in Causes) sb.Append("   ").Append(kv.Key).Append(' ').Append(kv.Value).Append('\n').Append(Examples[kv.Key]).Append('\n');
                return sb.ToString();
            }

            public void Count(string cat, string example)
            {
                int c0;
                Causes.TryGetValue(cat, out c0);
                Causes[cat] = c0 + 1;
                if (c0 < 3) Examples[cat] = (Examples.ContainsKey(cat) ? Examples[cat] + "\n" : "") + "      " + example;
            }
        }

        /// <summary>Scans every deck span of the world's tiles (<see cref="ScanDecks"/>); <paramref name="only"/> = way ids.</summary>
        internal static DeckScan Scan(BridgeWorld w, bool quiet, ICollection<ulong> only = null)
        {
            var scan = new DeckScan();
            foreach (KeyValuePair<TileId, Ghumante.World.Streaming.TileBuild> kv in w.Builds)
            {
                TileData t = kv.Value.Source;
                if (!t.HasDetail) continue;
                foreach (BridgeSpan sp in BridgeLayout.For(t).Spans)
                {
                    if (sp.Stair || only != null && !only.Contains(sp.Record.OsmWayId)) continue;
                    int h = 0, k = 0, d = 0, f = 0, n = 0;
                    string ex = null;
                    for (double s = sp.Path.Start + 0.25; s < sp.Path.End - 0.25; s += 0.5)
                    {
                        if (!sp.HasDeck(s)) continue;
                        double px, pz, ux, uz, nx, nz, tx, tz;
                        float y;
                        sp.Path.Frame(s, out px, out pz, out y, out ux, out uz, out nx, out nz, out tx, out tz);
                        double il = sp.Lerp(sp.HalfL, s) + sp.Lerp(sp.WalkL, s), ir = sp.Lerp(sp.HalfR, s) + sp.Lerp(sp.WalkR, s);
                        il = Math.Min(il, sp.Lerp(sp.ClampL, s) - BridgeStyle.RailBaseM);
                        ir = Math.Min(ir, sp.Lerp(sp.ClampR, s) - BridgeStyle.RailBaseM);
                        for (double u = -ir + 0.2; u <= il - 0.2; u += 0.25)
                        {
                            double x = t.Tile.X0 + px + ux * u, z = t.Tile.Z0 + pz + uz * u;
                            float top;
                            int layer;
                            if (!w.DrawnBelow(x, z, y + 0.6f, out top, out layer) || top < y - 0.6f) continue; // nothing drawn at deck level here
                            n++;
                            GroundSample g;
                            bool got = w.Ground.TrySample(x, z, y + 0.05f, out g);
                            string what = null;
                            int whatLayer = layer;
                            float by = 0f;
                            if (!got || g.Height < top - 0.5f)
                            {
                                what = "hole";
                                h++;
                            }
                            else
                            {
                                float above, below;
                                bool up;
                                int al, bl;
                                bool covered = w.FirstAbove(x, z, g.Height + 0.02f, out above, out up, out al) && up && above - g.Height < 2f;
                                bool under = w.DrawnBelow(x, z, g.Height + 0.02f, out below, out bl);
                                if (covered && above - g.Height > 0.1f)
                                {
                                    k++;
                                    what = above - g.Height > DeepSinkM ? "deep" : null;
                                    whatLayer = al;
                                    by = above - g.Height;
                                    if (what != null) d++;
                                }
                                else if (!under || g.Height - below > 0.15f)
                                {
                                    f++;
                                    what = "float";
                                    whatLayer = under ? bl : 0;
                                    by = under ? g.Height - below : 99f;
                                }
                            }
                            if (what == null) continue;
                            bool outside = x < t.Tile.X0 || z < t.Tile.Z0 || x > t.Tile.X0 + t.Tile.Size || z > t.Tile.Z0 + t.Tile.Size;
                            bool other = OtherSpanCovers(w, sp, x, z);
                            bool end = s - sp.Path.Start < 2.5 || sp.Path.End - s < 2.5;
                            string feet = !got ? "none" : g.OnDeck ? "deck" : g.OnFootpath ? "footpath" : g.OnRoad ? "road" : "terrain";
                            string cat = what + ":" + "TARB"[whatLayer] + ":feet-" + feet + (outside ? ":outside" : "") + (other ? ":overlap" : "") + (end ? ":end" : ":mid");
                            string desc = Name(t, sp) + " s " + (s - sp.Path.Start).ToString("F1") + "/" + (sp.Path.End - sp.Path.Start).ToString("F1") + " u " + u.ToString("F2")
                                          + " deck " + y.ToString("F2") + " feet " + (got ? g.Height.ToString("F2") : "none") + " by " + by.ToString("F2") + " (" + x.ToString("F2") + ", " + z.ToString("F2") + ")";
                            scan.Count(cat, desc);
                            if (ex == null && what != "float") ex = what + " " + desc;
                        }
                    }
                    scan.Cells += n;
                    scan.Holes += h;
                    scan.Sinks += k;
                    scan.Deep += d;
                    scan.Floats += f;
                    if (!quiet && h + d > 0)
                        scan.Report.Append(t.Tile).Append(' ').Append(Name(t, sp)).Append(": ").Append(n).Append(" cells, holes ").Append(h).Append(", deep ").Append(d)
                            .Append(", sinks ").Append(k).Append(", floats ").Append(f).Append(" | ").Append(ex).Append('\n');
                }
            }
            return scan;
        }

        /// <summary>True when a deck span of the loaded tiles other than <paramref name="self"/> covers world (x, z) out to
        /// its deck edge.</summary>
        private static bool OtherSpanCovers(BridgeWorld w, BridgeSpan self, double x, double z)
        {
            foreach (KeyValuePair<TileId, Ghumante.World.Streaming.TileBuild> kv in w.Builds)
            {
                TileData t = kv.Value.Source;
                if (!t.HasDetail || x < t.Tile.X0 - 60 || z < t.Tile.Z0 - 60 || x > t.Tile.X0 + t.Tile.Size + 60 || z > t.Tile.Z0 + t.Tile.Size + 60) continue;
                foreach (BridgeSpan sp in BridgeLayout.For(t).Spans)
                {
                    if (sp == self || sp.Record.OsmWayId == self.Record.OsmWayId) continue;
                    double lx = x - t.Tile.X0, lz = z - t.Tile.Z0;
                    double d = BridgeMesher.Distance(sp, lx, lz);
                    if (d < Math.Max(sp.EdgeL(0), sp.EdgeR(0)) + 0.1) return true;
                }
            }
            return false;
        }

        /// <summary>What answers at one world point: BRIDGE_POINT = "x,z,y" (feet height).</summary>
        [Test, Explicit]
        public void ProbePoint()
        {
            string specs = Environment.GetEnvironmentVariable("BRIDGE_POINT");
            if (string.IsNullOrEmpty(specs)) Assert.Ignore("set BRIDGE_POINT");
            foreach (string spec in specs.Split(';')) Point(spec);
        }

        private static void Point(string spec)
        {
            TestContext.WriteLine("==== point " + spec);
            string[] a = spec.Split(',');
            double x = double.Parse(a[0]), z = double.Parse(a[1]);
            float y = float.Parse(a[2]);
            BridgeWorld w = BridgeWorld.Around(new[] { TileId.At(10, x, z) }, 1);
            foreach (KeyValuePair<TileId, Ghumante.World.Streaming.TileBuild> kv in w.Builds)
            {
                if (Math.Abs(kv.Key.X0 + 512 - x) > 600 || Math.Abs(kv.Key.Z0 + 512 - z) > 600) continue;
                BridgeDeckIndex d = BridgeDeckIndex.ForTile(kv.Value.Source);
                float dy, nx, ny, nz, under;
                bool deck = d.TryDeck(x, z, y, out dy, out nx, out ny, out nz);
                bool ceil = d.TryCeiling(x, z, y, out under);
                TestContext.WriteLine(kv.Key + " deck " + (deck ? dy.ToString("F2") : "-") + " ceiling " + (ceil ? under.ToString("F2") : "-"));
                foreach (BridgeSpan sp in d.Layout.Spans)
                {
                    double lx = x - kv.Key.X0, lz = z - kv.Key.Z0;
                    for (int si = 0; si < sp.Stairs.Count; si++)
                    {
                        BridgeStair st = sp.Stairs[si];
                        double px = lx - st.X, pz = lz - st.Z, al = px * st.Dx + pz * st.Dz, ac = -px * st.Dz + pz * st.Dx;
                        if (al < -0.5 || al > st.Length + 0.5 || Math.Abs(ac) > st.HalfWidth + 0.5) continue;
                        TestContext.WriteLine("   flight " + sp.Record.OsmWayId + "#" + si + " along " + al.ToString("F2") + "/" + st.Length.ToString("F2") + " across " + ac.ToString("F2") + "/" + st.HalfWidth.ToString("F2")
                                              + " surface " + (al >= 0 && al <= st.Length ? st.SurfaceAt(al).ToString("F2") : "-") + " y " + st.TopY.ToString("F2") + ".." + st.BottomY.ToString("F2") + " steps " + st.Steps + " landings " + st.Landings);
                    }
                    if (BridgeMesher.Distance(sp, lx, lz) > 12) continue;
                    TestContext.WriteLine("   span " + Name(kv.Value.Source, sp) + " dist " + BridgeMesher.Distance(sp, lx, lz).ToString("F2") + " ends " + sp.StartEnd + "/" + sp.EndEnd
                                          + " start (" + (kv.Key.X0 + sp.Path.X[0]).ToString("F1") + ", " + (kv.Key.Z0 + sp.Path.Z[0]).ToString("F1") + ") end ("
                                          + (kv.Key.X0 + sp.Path.X[sp.Count - 1]).ToString("F1") + ", " + (kv.Key.Z0 + sp.Path.Z[sp.Count - 1]).ToString("F1") + ") pre " + sp.HasPre + " post " + sp.HasPost
                                          + " stairs " + sp.Stairs.Count + " openings " + sp.Openings.Count + " stairzones " + sp.StairZones.Count);
                }
            }
            GroundSample g;
            bool got = w.Ground.TrySample(x, z, y, out g);
            TestContext.WriteLine("ground " + (got ? g.Height.ToString("F2") + (g.OnDeck ? " deck" : "") + (g.OnRoad ? " road " + g.RoadClass : "") + (g.OnStructure ? " struct" : "") : "blocked/none")
                                  + " blocked " + w.Ground.IsBlocked(x, z, y));
            float top;
            int layer;
            if (w.DrawnBelow(x, z, y + 1f, out top, out layer)) TestContext.WriteLine("drawn " + top.ToString("F2") + "TARB"[layer]);
            TestContext.WriteLine("column" + w.Column(x, z));
            TestContext.WriteLine(w.Explain(x, z, y));
            TestContext.WriteLine(w.Blockers(x, z, y, 0.6));
        }

        /// <summary>Dump the scene around a world point: BRIDGE_DUMP = "name,x,z,half" (needs GHUMANTE_PREVIEW_DIR).</summary>
        [Test, Explicit]
        public void DumpScene()
        {
            string spec = Environment.GetEnvironmentVariable("BRIDGE_DUMP");
            if (string.IsNullOrEmpty(spec) || !ObjDump.Enabled) Assert.Ignore("set BRIDGE_DUMP and GHUMANTE_PREVIEW_DIR");
            string[] a = spec.Split(',');
            double x = double.Parse(a[1]), z = double.Parse(a[2]), half = double.Parse(a[3]);
            BridgeWorld w = BridgeWorld.Around(new[] { TileId.At(10, x, z) }, 1);
            w.Dump(a[0], x, z, half);
        }

        /// <summary>A road's profile: BRIDGE_PROFILE = "tx,ty,way[,lateral]": every 2 m along it the ground query (from the
        /// structure height), and the highest drawn terrain, road and bridge surfaces.</summary>
        [Test, Explicit]
        public void ProfileRoad()
        {
            string specs = Environment.GetEnvironmentVariable("BRIDGE_PROFILE");
            if (string.IsNullOrEmpty(specs)) Assert.Ignore("set BRIDGE_PROFILE");
            foreach (string spec in specs.Split(';')) Profile(spec);
        }

        private static void Profile(string spec)
        {
            TestContext.WriteLine("==== profile " + spec);
            string[] a = spec.Split(',');
            var id = new TileId(10, int.Parse(a[0]), int.Parse(a[1]));
            ulong way = ulong.Parse(a[2]);
            double lateral = a.Length > 3 ? double.Parse(a[3]) : 0;
            BridgeWorld w = BridgeWorld.Around(new[] { id }, 1);
            TileData t = w.Tile(id);
            RoadSpatialIndex idx = w.Ground.RoadIndexOf(id);
            for (int ri = 0; ri < t.Roads.Count; ri++)
            {
                if (t.Roads[ri].OsmWayId != way) continue;
                RoadRecord r = t.Roads[ri];
                float[] sh = idx != null ? idx.SurfaceHeights(ri) : null;
                TestContext.WriteLine("road #" + ri + " " + r.RoadClass + " points " + r.PointCount + " structure " + t.RoadStructureOf(ri).Kind + " heights " + (sh != null));
                List<double> line = BridgeProbe.RoadAround(t, ri, r.Points[0] / 100.0, r.Points[1] / 100.0, 0, lateral);
                double len = 0;
                for (int k = 0; k + 1 < r.PointCount; k++)
                {
                    double dx = (r.Points[2 * k + 2] - r.Points[2 * k]) / 100.0, dz = (r.Points[2 * k + 3] - r.Points[2 * k + 1]) / 100.0;
                    len += Math.Sqrt(dx * dx + dz * dz);
                }
                line = BridgeProbe.RoadAround(t, ri, r.Points[0] / 100.0, r.Points[1] / 100.0, len + 10, lateral);
                for (int i = 0; i < line.Count / 2; i++)
                {
                    double x = line[2 * i], z = line[2 * i + 1];
                    float terr = w.LayerTop(x, z, 0), area = w.LayerTop(x, z, 1), road = w.LayerTop(x, z, 2), br = w.LayerTop(x, z, 3);
                    GroundSample g;
                    float from = float.IsNaN(road) ? (float.IsNaN(terr) ? 2000f : terr) : road;
                    bool got = w.Ground.TrySample(x, z, from + 0.3f, out g);
                    TestContext.WriteLine("  (" + x.ToString("F1") + ", " + z.ToString("F1") + ") ground " + (got ? g.Height.ToString("F2") + (g.OnDeck ? "d" : "") + (g.OnRoad ? "r" : "") : "X")
                                          + " drawn terrain " + terr.ToString("F2") + " areas " + area.ToString("F2") + " road " + road.ToString("F2") + " bridge " + br.ToString("F2"));
                }
            }
        }

        /// <summary>BRIDGE_WAYS = "way,way" narrows the probes to those spans.</summary>
        private static bool Wanted(BridgeSpan sp)
        {
            string only = Environment.GetEnvironmentVariable("BRIDGE_WAYS");
            return string.IsNullOrEmpty(only) || ("," + only + ",").Contains("," + sp.Record.OsmWayId + ",");
        }

        /// <summary>Feet this far inside a drawn bridge (or under the drawn surface while the deck carries them) is the
        /// owner's "sucked in".</summary>
        private const float DeepSinkM = 0.25f;

        /// <summary>
        /// Every bridge and flyover deck of the sample (owner playtest #2): walked and ridden across along the road it carries
        /// (from 6 m before the deck to 6 m beyond it, in the left lane, following the streets that continue it), walked
        /// back the other way, its left walkway walked on the deck from end to end, and every road under it walked and
        /// ridden 30 m to each side. A run fails when it stops, or when the feet sink into a drawn bridge or deck surface
        /// (<see cref="DeepSinkM"/>), or when a run under the deck is carried onto it; a stop at a pole, tree or parked
        /// vehicle of the dressing (other packages; the player steers around it) is counted apart. Prints the totals, the
        /// causes of the stops and every span.
        /// </summary>
        [Test, Explicit]
        public void ProbeEveryBridge()
        {
            BridgeWorld w = BridgeWorld.Around(BridgeTiles(), 1);
            BridgeSweep r = Sweep(w, null);
            TestContext.WriteLine(r.Summary());
            TestContext.WriteLine(r.Report.ToString());
        }

        /// <summary>The totals of <see cref="Sweep"/>.</summary>
        internal sealed class BridgeSweep
        {
            public int Spans, Runs, Bad, Dressing, Unders, UnderBad, Sunk;
            public readonly SortedDictionary<string, int> Causes = new SortedDictionary<string, int>();
            public readonly StringBuilder Report = new StringBuilder();

            public string Summary()
            {
                var sb = new StringBuilder();
                sb.Append("spans ").Append(Spans).Append(", runs ").Append(Runs).Append(": bad ").Append(Bad).Append(" (sunk ").Append(Sunk).Append("), stopped by dressing ")
                  .Append(Dressing).Append("; unders ").Append(Unders).Append(" bad ").Append(UnderBad).Append("\n   stop causes:");
                foreach (KeyValuePair<string, int> kv in Causes) sb.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
                return sb.ToString();
            }
        }

        /// <summary>Runs <see cref="ProbeEveryBridge"/> over the bridge tiles of <paramref name="w"/>; <paramref name="ways"/>
        /// narrows it to those spans (null: every span).</summary>
        internal static BridgeSweep Sweep(BridgeWorld w, ICollection<ulong> ways)
        {
            var r = new BridgeSweep();
            foreach (TileId id in BridgeTiles())
            {
                TileData t = w.Tile(id);
                if (t == null || !t.HasDetail) continue;
                foreach (BridgeSpan sp in BridgeLayout.For(t).Spans)
                {
                    if (sp.Stair || sp.Overbridge || sp.Length < 8 || !Wanted(sp) || ways != null && !ways.Contains(sp.Record.OsmWayId)) continue;
                    float rise = 0f;
                    for (int k = 0; k < sp.Count; k++) rise = Math.Max(rise, sp.Path.Y[k] - sp.Ground[k]);
                    r.Spans++;
                    r.Report.Append(t.Tile).Append(' ').Append(Name(t, sp)).Append(" rise ").Append(rise.ToString("F1")).Append(" length ").Append(sp.Length.ToString("F0")).Append('\n');
                    int mid = sp.Count / 2;
                    double lane = 0.5 * Math.Min(sp.HalfL[mid], sp.HalfR[mid]);
                    List<double> across = BridgeProbe.RoadAround(t, sp.Road, sp.Path.X[mid], sp.Path.Z[mid], 0.5 * sp.Length + 6, sp.Foot ? 0 : lane);
                    List<double> back = BridgeProbe.RoadAround(t, sp.Road, sp.Path.X[mid], sp.Path.Z[mid], 0.5 * sp.Length + 6, sp.Foot ? 0 : -lane);
                    BridgeProbe.Reverse(back);
                    Run(w, r, "walk", VehicleSpec.Walker(), across, 1f, float.NaN, false);
                    Run(w, r, "back", VehicleSpec.Walker(), back, 1f, float.NaN, false);
                    if (!sp.Foot) Run(w, r, "ride", VehicleSpec.Motorbike(), across, 0.5f, float.NaN, false);
                    if (!sp.Foot && sp.WalkL[mid] >= 0.6)
                    {
                        // The left walkway on the deck, end to end (the walking line in its middle).
                        List<double> walkway = BridgeProbe.Along(sp, t.Tile, BridgeProbe.Lane.LeftWalk, 0, 0, false);
                        Trim(walkway, 1.0);
                        if (walkway.Count >= 4) Run(w, r, "walkway", VehicleSpec.Walker(), walkway, 1f, float.NaN, false);
                    }
                    foreach (BridgeCrossing c in sp.Crossings)
                    {
                        r.Unders++;
                        List<double> line = BridgeProbe.RoadAround(t, c.Road, c.X, c.Z, 30, Math.Min(1.5, 0.3 * c.LowHalfM));
                        int before = r.Bad;
                        r.Report.Append("   under ").Append(t.Roads[c.Road].OsmWayId).Append('\n');
                        Run(w, r, "under walk", VehicleSpec.Walker(), line, 1f, c.LowY + 0.3f, true);
                        Run(w, r, "under ride", VehicleSpec.Motorbike(), line, 0.5f, c.LowY + 0.3f, true);
                        if (r.Bad > before) r.UnderBad++;
                    }
                }
            }
            return r;
        }

        private static void Run(BridgeWorld w, BridgeSweep r, string what, VehicleSpec spec, List<double> line, float throttle, float startY, bool under)
        {
            r.Runs++;
            ProbeRun run = BridgeProbe.Follow(w, spec, line, throttle, startY);
            bool sunk = run.BridgeSink >= DeepSinkM;
            bool carried = under && run.EverOnDeck;
            bool dressing = !run.Reached && run.Cause == "dressing";
            bool ok = (run.Reached || dressing) && !sunk && !carried;
            if (!run.Reached)
            {
                int c0;
                r.Causes.TryGetValue(run.Cause, out c0);
                r.Causes[run.Cause] = c0 + 1;
            }
            if (dressing) r.Dressing++;
            if (sunk) r.Sunk++;
            if (!ok) r.Bad++;
            r.Report.Append("   ").Append(what.PadRight(10)).Append(ok ? "ok  " : "BAD ").Append(run).Append(carried ? " CARRIED ONTO THE DECK" : "").Append('\n');
            if (!run.Reached && !dressing) r.Report.Append(BridgeProbe.Why(w, run));
        }

        /// <summary>Cuts <paramref name="m"/> metres off both ends of a waypoint line.</summary>
        private static void Trim(List<double> pts, double m)
        {
            for (int end = 0; end < 2; end++)
            {
                double left = m;
                while (pts.Count >= 4 && left > 0)
                {
                    int a = end == 0 ? 0 : pts.Count - 2, b = end == 0 ? 2 : pts.Count - 4;
                    double dx = pts[b] - pts[a], dz = pts[b + 1] - pts[a + 1], l = Math.Sqrt(dx * dx + dz * dz);
                    if (l > left)
                    {
                        pts[a] += dx / l * left;
                        pts[a + 1] += dz / l * left;
                        break;
                    }
                    left -= l;
                    if (end == 0) pts.RemoveRange(0, 2);
                    else pts.RemoveRange(pts.Count - 2, 2);
                }
            }
        }
    }
}
