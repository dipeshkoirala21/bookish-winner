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

        /// <summary>Trace one walk: BRIDGE_TRACE = "tx,ty,way[,mode]" (mode: walk, back, ride, under:lowerWay).</summary>
        [Test, Explicit]
        public void TraceOne()
        {
            string spec = Environment.GetEnvironmentVariable("BRIDGE_TRACE");
            if (string.IsNullOrEmpty(spec)) Assert.Ignore("set BRIDGE_TRACE");
            string[] a = spec.Split(',');
            var id = new TileId(10, int.Parse(a[0]), int.Parse(a[1]));
            ulong way = ulong.Parse(a[2]);
            string mode = a.Length > 3 ? a[3] : "walk";
            BridgeWorld w = BridgeWorld.Around(new[] { id }, 1);
            TileData t = w.Tile(id);
            int dbg = 0;
            ArcadeVehicle.DebugLog = m => { if (dbg++ < 40) TestContext.WriteLine("DBG " + m); };
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
                        report.Append("   up   ").Append(ru).Append('\n').Append(Why(w, ru)).Append("   down ").Append(rd).Append('\n').Append(Why(w, rd));
                    }
                }
            }
            TestContext.WriteLine("flights " + flights + ": up bad " + upBad + ", down bad " + downBad);
            TestContext.WriteLine(report.ToString());
        }

        /// <summary>
        /// The ground the walker gets on every bridge deck against what is drawn there: inside the railings of every span,
        /// every 0.5 m along and 0.25 m across, the ground query (from just above the deck) must stand within a decimetre
        /// of the highest drawn surface (bridge slab, walkway or road surfacing) at that point. Holes (the query falls
        /// through to the ground below while a deck is drawn), sinks (the drawn surface above the feet) and phantoms (a
        /// deck reported where none is drawn) are counted per span.
        /// </summary>
        [Test, Explicit]
        public void ScanDecks()
        {
            BridgeWorld w = BridgeWorld.Around(BridgeTiles(), 1);
            var report = new StringBuilder();
            int cells = 0, holes = 0, sinks = 0, phantoms = 0, deepAll = 0;
            foreach (KeyValuePair<TileId, Ghumante.World.Streaming.TileBuild> kv in w.Builds)
            {
                TileData t = kv.Value.Source;
                if (!t.HasDetail) continue;
                foreach (BridgeSpan sp in BridgeLayout.For(t).Spans)
                {
                    if (sp.Stair) continue;
                    int h = 0, k = 0, p = 0, n = 0, deep = 0;
                    string ex = null, exHole = null, exDeep = null;
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
                            if (!got || g.Height < top - 0.5f)
                            {
                                h++;
                                what = "hole";
                            }
                            else if (g.Height < top - 0.1f)
                            {
                                k++;
                                what = "sink";
                                if (g.Height < top - 0.25f && layer != 0)
                                {
                                    deep++;
                                    what = "deep";
                                }
                            }
                            else if (g.Height > top + 0.15f)
                            {
                                p++;
                                what = "phantom";
                            }
                            string desc = what == null ? null : what + " at s " + (s - sp.Path.Start).ToString("F1") + " u " + u.ToString("F2") + " drawn " + top.ToString("F2") + "TARB"[layer]
                                     + " ground " + (got ? g.Height.ToString("F2") + (g.OnDeck ? " deck" : "") + (g.OnStructure ? " struct" : "") : "none") + " (" + x.ToString("F1") + ", " + z.ToString("F1") + ")";
                            if (what == "hole" && exHole == null) exHole = desc;
                            if (what == "deep" && exDeep == null) exDeep = desc;
                            if (what != null && ex == null) ex = desc;
                        }
                    }
                    cells += n;
                    holes += h;
                    sinks += k;
                    phantoms += p;
                    deepAll += deep;
                    if (h + deep + p > 0)
                        report.Append(t.Tile).Append(' ').Append(Name(t, sp)).Append(": ").Append(n).Append(" cells, holes ").Append(h).Append(", sinks ").Append(k)
                              .Append(" (deep ").Append(deep).Append("), phantoms ").Append(p).Append(" | ").Append(exHole ?? exDeep ?? ex).Append('\n');
                }
            }
            TestContext.WriteLine("cells " + cells + ": holes " + holes + ", sinks " + sinks + " (deep, not terrain: " + deepAll + "), phantoms " + phantoms);
            TestContext.WriteLine(report.ToString());
        }

        /// <summary>What answers at one world point: BRIDGE_POINT = "x,z,y" (feet height).</summary>
        [Test, Explicit]
        public void ProbePoint()
        {
            string spec = Environment.GetEnvironmentVariable("BRIDGE_POINT");
            if (string.IsNullOrEmpty(spec)) Assert.Ignore("set BRIDGE_POINT");
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
            string spec = Environment.GetEnvironmentVariable("BRIDGE_PROFILE");
            if (string.IsNullOrEmpty(spec)) Assert.Ignore("set BRIDGE_PROFILE");
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

        /// <summary>What stopped a run (the solids and colliders at the stop), or nothing when it arrived.</summary>
        private static string Why(BridgeWorld w, ProbeRun r)
        {
            if (r.Reached) return "";
            var sb = new StringBuilder();
            sb.Append("      stop (").Append(r.StopX.ToString("F1")).Append(", ").Append(r.StopZ.ToString("F1")).Append(") y ").Append(r.StopY.ToString("F2"))
              .Append(" heading (").Append(r.StopDirX.ToString("F2")).Append(", ").Append(r.StopDirZ.ToString("F2")).Append(") ground ahead:");
            double firstX = double.NaN;
            foreach (double a in new[] { 0.0, 0.3, 0.6, 1.0, 1.5 })
            {
                double x = r.StopX + r.StopDirX * a, z = r.StopZ + r.StopDirZ * a;
                GroundSample g;
                bool got = w.Ground.TrySample(x, z, r.StopY, out g);
                if (!got && double.IsNaN(firstX)) firstX = a;
                sb.Append(' ').Append(a.ToString("F1")).Append('=').Append(got ? g.Height.ToString("F2") + (g.OnDeck ? "d" : "") + (g.OnStructure ? "s" : "") + (g.OnFootpath ? "f" : "")
                                                                             + (g.Ny < 0.766f ? "!ny" + g.Ny.ToString("F2") : "") : "X");
            }
            sb.Append('\n').Append(w.Blockers(r.StopX + r.StopDirX * 0.3, r.StopZ + r.StopDirZ * 0.3, r.StopY, 0.6));
            double ea = double.IsNaN(firstX) ? 0.3 : firstX;
            sb.Append(w.Explain(r.StopX + r.StopDirX * ea, r.StopZ + r.StopDirZ * ea, r.StopY, r.Body.HeightM));
            float t, nx, nz;
            bool hit = w.Ground.SweepBody(r.Body, r.StopX, r.StopZ, r.StopHeading, r.StopY, r.StopDirX * 0.3, r.StopDirZ * 0.3, out t, out nx, out nz);
            sb.Append("   sweep 0.3 m ahead: ").Append(hit ? "hit t " + t.ToString("F2") + " n (" + nx.ToString("F2") + ", " + nz.ToString("F2") + ")" : "clear").Append('\n');
            return sb.ToString();
        }

        /// <summary>Feet this far inside a drawn bridge (or under the drawn surface while the deck carries them) is the
        /// owner's "sucked in".</summary>
        private const float DeepSinkM = 0.25f;

        [Test, Explicit]
        public void ProbeEveryBridge()
        {
            BridgeWorld w = BridgeWorld.Around(BridgeTiles(), 1);
            var report = new StringBuilder();
            int spans = 0, walkBad = 0, rideBad = 0, underBad = 0, unders = 0;
            foreach (TileId id in BridgeTiles())
            {
                TileData t = w.Tile(id);
                if (t == null || !t.HasDetail) continue;
                BridgeLayout l = BridgeLayout.For(t);
                foreach (BridgeSpan sp in l.Spans)
                {
                    if (sp.Stair || sp.Overbridge || sp.Length < 8 || !Wanted(sp)) continue;
                    float rise = 0f;
                    for (int k = 0; k < sp.Count; k++) rise = Math.Max(rise, sp.Path.Y[k] - sp.Ground[k]);
                    if (rise < 1.0f && sp.Crossings.Count == 0) continue;
                    spans++;
                    double before = sp.StartEnd == SpanEnd.Ground ? 8 : 3, after = sp.EndEnd == SpanEnd.Ground ? 8 : 3;
                    BridgeProbe.Lane foot = sp.WalkL[sp.Count / 2] >= 0.6 && !sp.Foot ? BridgeProbe.Lane.LeftWalk : sp.Foot ? BridgeProbe.Lane.Centre : BridgeProbe.Lane.LeftLane;
                    ProbeRun walk = BridgeProbe.Follow(w, VehicleSpec.Walker(), BridgeProbe.Along(sp, t.Tile, foot, before, after, false), 1f);
                    ProbeRun walkBack = BridgeProbe.Follow(w, VehicleSpec.Walker(), BridgeProbe.Along(sp, t.Tile, foot, before, after, true), 1f);
                    bool wOk = walk.Reached && walkBack.Reached && walk.BridgeSink < DeepSinkM && walkBack.BridgeSink < DeepSinkM;
                    if (!wOk) walkBad++;
                    report.Append(t.Tile).Append(' ').Append(Name(t, sp)).Append(" rise ").Append(rise.ToString("F1")).Append('\n');
                    report.Append("   walk  ").Append(walk).Append(" | back ").Append(walkBack).Append('\n').Append(Why(w, walk)).Append(Why(w, walkBack));
                    if (!sp.Foot)
                    {
                        ProbeRun ride = BridgeProbe.Follow(w, VehicleSpec.Motorbike(), BridgeProbe.Along(sp, t.Tile, BridgeProbe.Lane.LeftLane, before, after, false), 0.5f);
                        bool rOk = ride.Reached && ride.BridgeSink < DeepSinkM;
                        if (!rOk) rideBad++;
                        report.Append("   ride  ").Append(ride).Append('\n').Append(Why(w, ride));
                    }
                    foreach (BridgeCrossing c in sp.Crossings)
                    {
                        unders++;
                        List<double> line = BridgeProbe.RoadAround(t, c.Road, c.X, c.Z, 30, Math.Min(1.5, 0.3 * c.LowHalfM));
                        ProbeRun uw = BridgeProbe.Follow(w, VehicleSpec.Walker(), line, 1f, c.LowY + 0.3f);
                        ProbeRun ur = BridgeProbe.Follow(w, VehicleSpec.Motorbike(), line, 0.5f, c.LowY + 0.3f);
                        bool uOk = uw.Reached && ur.Reached && !uw.EverOnDeck && !ur.EverOnDeck && uw.BridgeSink < DeepSinkM;
                        if (!uOk) underBad++;
                        report.Append("   under ").Append(t.Roads[c.Road].OsmWayId).Append(" walk ").Append(uw).Append(" | ride ").Append(ur).Append('\n').Append(Why(w, uw)).Append(Why(w, ur));
                    }
                }
            }
            TestContext.WriteLine("spans " + spans + ": walk bad " + walkBad + ", ride bad " + rideBad + "; unders " + unders + " bad " + underBad);
            TestContext.WriteLine(report.ToString());
        }
    }
}
