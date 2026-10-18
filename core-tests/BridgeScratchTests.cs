using System;
using System.Collections.Generic;
using System.Text;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Bridges;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>Scratch diagnostics for the bridges2 package (deleted before commit).</summary>
    public class BridgeScratchTests
    {
        [Test, Explicit]
        public void TraceMappedOverbridge()
        {
            TileData t = BridgeScenes.MappedOverbridge(false);
            var g = new TileGroundQuery { DeckFactory = BridgeDeckIndex.ForTile };
            g.Add(t);
            BridgeLayout l = BridgeLayout.For(t);
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            foreach (BridgeSpan sp in l.Spans)
            {
                TestContext.WriteLine("span " + sp.Record.OsmWayId + " stair " + sp.Stair + " over " + sp.Overbridge + " count " + sp.Count + " ends " + sp.StartEnd + "/" + sp.EndEnd
                                      + " y0 " + sp.Path.Y[0] + " y1 " + sp.Path.Y[sp.Count - 1] + " start (" + sp.Path.X[0] + "," + sp.Path.Z[0] + ") end (" + sp.Path.X[sp.Count - 1] + "," + sp.Path.Z[sp.Count - 1] + ")");
                for (int i = 0; i < sp.Stairs.Count; i++)
                {
                    BridgeStair st = sp.Stairs[i];
                    TestContext.WriteLine("   flight " + i + " top (" + st.X.ToString("F2") + "," + st.Z.ToString("F2") + ") dir (" + st.Dx.ToString("F2") + "," + st.Dz.ToString("F2") + ") top " + st.TopY.ToString("F2")
                                          + " bottom " + st.BottomY.ToString("F2") + " len " + st.Length.ToString("F2") + " steps " + st.Steps + " half " + st.HalfWidth.ToString("F2")
                                          + " runs " + (2 * i < sp.HandrailRuns.Count ? string.Join(",", sp.HandrailRuns[2 * i]) + " | " + string.Join(",", sp.HandrailRuns[2 * i + 1]) : "-"));
                }
            }
            BridgeColliders c = BridgeColliders.For(l);
            foreach (BridgeColliders.Wall w in c.Walls)
                TestContext.WriteLine("wall (" + w.X0.ToString("F2") + "," + w.Z0.ToString("F2") + ")-(" + w.X1.ToString("F2") + "," + w.Z1.ToString("F2") + ") y " + w.Bottom.ToString("F2") + ".." + w.Top.ToString("F2"));
            foreach (BridgeColliders.Column k in c.Columns)
                TestContext.WriteLine("column (" + k.CX.ToString("F2") + "," + k.CZ.ToString("F2") + ") r " + k.Radius + " y " + k.Bottom.ToString("F2") + ".." + k.Top.ToString("F2"));
            foreach (BridgeColliders.Block k in c.Blocks)
                TestContext.WriteLine("block (" + k.CX.ToString("F2") + "," + k.CZ.ToString("F2") + ") " + k.HalfAlong.ToString("F2") + "x" + k.HalfAcross.ToString("F2") + " y " + k.Bottom.ToString("F2") + ".." + k.Top.ToString("F2"));
            double[] plan = { 478, 508, 486, 508, 490, 508, 494, 508, 498, 508, 500, 507, 500, 504, 500, 500, 500, 496, 500, 493, 498, 492, 494, 492, 490, 492, 486, 492, 478, 492 };
            var pts = new List<double>();
            for (int i = 0; i < plan.Length / 2; i++)
            {
                pts.Add(x0 + plan[2 * i]);
                pts.Add(z0 + plan[2 * i + 1]);
            }
            var v = new ArcadeVehicle(VehicleSpec.Walker());
            v.Teleport(pts[0], pts[1], (float)Math.Atan2(pts[2] - pts[0], pts[3] - pts[1]), g);
            int target = 1, n = pts.Count / 2;
            double lx = double.NaN, lz = 0;
            for (int i = 0; i < 60 * 60; i++)
            {
                double dx = pts[2 * target] - v.X, dz = pts[2 * target + 1] - v.Z;
                while (target < n - 1 && dx * dx + dz * dz < 0.8 * 0.8)
                {
                    target++;
                    dx = pts[2 * target] - v.X;
                    dz = pts[2 * target + 1] - v.Z;
                }
                if (target == n - 1 && dx * dx + dz * dz < 0.8 * 0.8) break;
                StepEvents ev = v.Step(new DriveInput(1f, 0f, v.SteerToward((float)Math.Atan2(dx, dz), BridgeProbe.Dt)), BridgeProbe.Dt, g, 0f);
                if (double.IsNaN(lx) || (v.X - lx) * (v.X - lx) + (v.Z - lz) * (v.Z - lz) > 0.09 || (ev & StepEvents.HitWall) != 0 && i % 30 == 0)
                {
                    lx = v.X;
                    lz = v.Z;
                    GroundSample s = v.Ground;
                    TestContext.WriteLine(i + " (" + (v.X - x0).ToString("F2") + ", " + (v.Z - z0).ToString("F2") + ") y " + v.Y.ToString("F2") + " gnd " + s.Height.ToString("F2") + (s.OnDeck ? " deck" : "")
                                          + (s.OnStructure ? " struct" : "") + " ny " + s.Ny.ToString("F2") + " v " + v.SpeedMps.ToString("F2") + " target " + target + " ev " + ev);
                }
            }
        }

        /// <summary>BRIDGE_GROUP = "tx,ty,group": the spans, flights and colliders of one structure.</summary>
        [Test, Explicit]
        public void DescribeGroup()
        {
            string specs = Environment.GetEnvironmentVariable("BRIDGE_GROUP");
            if (string.IsNullOrEmpty(specs)) Assert.Ignore("set BRIDGE_GROUP");
            foreach (string spec in specs.Split(';')) Describe(spec);
        }

        private static void Describe(string spec)
        {
            string[] a = spec.Split(',');
            TestContext.WriteLine("==== group " + spec);
            TileData t = StreamingSampleRegion.Tile(new TileId(10, int.Parse(a[0]), int.Parse(a[1])));
            Describe(t, ulong.Parse(a[2]));
        }

        [Test, Explicit]
        public void DescribeMappedOverbridge()
        {
            Describe(BridgeScenes.MappedOverbridge(false), 701);
        }

        private static void Describe(TileData t, ulong g)
        {
            BridgeLayout l = BridgeLayout.For(t);
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
            foreach (BridgeSpan sp in l.Spans)
            {
                if (sp.GroupWay != g) continue;
                NameRecord n = t.Name(sp.Record.NameRef);
                TestContext.WriteLine("span " + sp.Record.OsmWayId + " " + sp.Record.RoadClass + " '" + (n != null ? n.Default : "") + "' stair " + sp.Stair + " over " + sp.Overbridge + " ends " + sp.StartEnd + "/" + sp.EndEnd
                                      + " shift (" + sp.ShiftX.ToString("F2") + "," + sp.ShiftZ.ToString("F2") + ") rec " + t.RoadStructureOf(sp.Road).Kind);
                for (int k = 0; k < sp.Count; k++)
                {
                    TestContext.WriteLine("    st " + k + " (" + (x0 + sp.Path.X[k]).ToString("F2") + ", " + (z0 + sp.Path.Z[k]).ToString("F2") + ") y " + sp.Path.Y[k].ToString("F2") + " gnd " + sp.Ground[k].ToString("F2")
                                          + " half " + sp.HalfL[k].ToString("F2") + "/" + sp.HalfR[k].ToString("F2") + " walk " + sp.WalkL[k].ToString("F2") + "/" + sp.WalkR[k].ToString("F2") + " edge " + sp.EdgeL(k).ToString("F2") + "/" + sp.EdgeR(k).ToString("F2")
                                          + " deck " + sp.HasDeck(sp.Path.S[k]) + " zone " + sp.InStairZone(sp.Path.S[k]));
                    minX = Math.Min(minX, sp.Path.X[k]); maxX = Math.Max(maxX, sp.Path.X[k]); minZ = Math.Min(minZ, sp.Path.Z[k]); maxZ = Math.Max(maxZ, sp.Path.Z[k]);
                }
                int[] p = sp.Record.Points;
                var mp = new StringBuilder("    mapped:");
                float[] dy = t.RoadStructureOf(sp.Road).DeckY;
                for (int k = 0; k < sp.Record.PointCount; k++) mp.Append(" (").Append((x0 + p[2 * k] / 100.0).ToString("F1")).Append(",").Append((z0 + p[2 * k + 1] / 100.0).ToString("F1")).Append(dy != null && k < dy.Length ? " " + dy[k].ToString("F1") : "").Append(")");
                TestContext.WriteLine(mp.ToString());
                TestContext.WriteLine("    gapsL " + string.Join(";", sp.GapsL.ConvertAll(x => x.S0.ToString("F1") + ".." + x.S1.ToString("F1"))) + " gapsR " + string.Join(";", sp.GapsR.ConvertAll(x => x.S0.ToString("F1") + ".." + x.S1.ToString("F1")))
                                      + " zones " + string.Join(";", sp.StairZones.ConvertAll(x => x.S0.ToString("F1") + ".." + x.S1.ToString("F1"))) + " path " + sp.Path.Start.ToString("F1") + ".." + sp.Path.End.ToString("F1")
                                      + " railL " + string.Join(",", sp.RailRunsL.ConvertAll(x => x.ToString("F1"))) + " railR " + string.Join(",", sp.RailRunsR.ConvertAll(x => x.ToString("F1"))));
                for (int i = 0; i < sp.Stairs.Count; i++)
                {
                    BridgeStair st = sp.Stairs[i];
                    TestContext.WriteLine("    flight " + i + " top (" + (x0 + st.X).ToString("F2") + "," + (z0 + st.Z).ToString("F2") + ") bottom (" + (x0 + st.X + st.Dx * st.Length).ToString("F2") + "," + (z0 + st.Z + st.Dz * st.Length).ToString("F2")
                                          + ") dir (" + st.Dx.ToString("F2") + "," + st.Dz.ToString("F2") + ") y " + st.TopY.ToString("F2") + ".." + st.BottomY.ToString("F2") + " len " + st.Length.ToString("F2") + " steps " + st.Steps
                                          + " half " + st.HalfWidth.ToString("F2") + " mapped " + st.Mapped
                                          + " runs " + (2 * i + 1 < sp.HandrailRuns.Count ? string.Join(",", sp.HandrailRuns[2 * i].ConvertAll(x => x.ToString("F1"))) + " | " + string.Join(",", sp.HandrailRuns[2 * i + 1].ConvertAll(x => x.ToString("F1"))) : "-"));
                }
            }
        }

        /// <summary>BRIDGE_NEAR = "x,z;x,z": the roads within 10 m of each point (class, structure, heights there, drawn
        /// surface from the roads package's surface query).</summary>
        [Test, Explicit]
        public void NearRoads()
        {
            string specs = Environment.GetEnvironmentVariable("BRIDGE_NEAR");
            if (string.IsNullOrEmpty(specs)) Assert.Ignore("set BRIDGE_NEAR");
            foreach (string spec in specs.Split(';'))
            {
                string[] a = spec.Split(',');
                double x = double.Parse(a[0]), z = double.Parse(a[1]);
                TileId id = TileId.At(10, x, z);
                TileData t = StreamingSampleRegion.Tile(id);
                var opts = StreamingScene.Settings(2, 10).Roads;
                var h = new TileHeightSampler(t, 1);
                var q = Ghumante.Core.Meshing.Roads.RoadSurfaceQuery.For(t, h, opts);
                TestContext.WriteLine("==== near " + spec + " tile " + id);
                Ghumante.Core.Meshing.Roads.RoadSurfaceHit hit;
                if (q.TrySample(x, z, out hit))
                    TestContext.WriteLine("   drawn surface " + hit.Y.ToString("F2") + " road " + (hit.Road >= 0 ? t.Roads[hit.Road].OsmWayId.ToString() : "cap") + " deck " + hit.OnDeck + " foot " + hit.OnFootpath);
                double lx = x - t.Tile.X0, lz = z - t.Tile.Z0;
                for (int ri = 0; ri < t.Roads.Count; ri++)
                {
                    RoadRecord r = t.Roads[ri];
                    int[] p = r.Points;
                    double best = double.MaxValue;
                    int bk = 0;
                    double bf = 0;
                    for (int k = 0; k + 1 < r.PointCount; k++)
                    {
                        double ax = p[2 * k] / 100.0, az = p[2 * k + 1] / 100.0, dx = p[2 * k + 2] / 100.0 - ax, dz = p[2 * k + 3] / 100.0 - az, l2 = dx * dx + dz * dz;
                        double f = l2 > 1e-12 ? Math.Max(0, Math.Min(1, ((lx - ax) * dx + (lz - az) * dz) / l2)) : 0;
                        double qx = ax + dx * f - lx, qz = az + dz * f - lz, d = Math.Sqrt(qx * qx + qz * qz);
                        if (d < best)
                        {
                            best = d;
                            bk = k;
                            bf = f;
                        }
                    }
                    if (best > 10) continue;
                    RoadStructureRecord st = t.RoadStructureOf(ri);
                    string hy = st.DeckY != null && bk + 1 < st.DeckY.Length ? (st.DeckY[bk] + (st.DeckY[bk + 1] - st.DeckY[bk]) * bf).ToString("F2") : "-";
                    float th;
                    h.TryHeight(x, z, out th);
                    TestContext.WriteLine("   road " + r.OsmWayId + " " + r.RoadClass + " flags " + r.Flags + " kind " + st.Kind + " sflags " + st.Flags + " dist " + best.ToString("F1") + " seg " + bk + " heights " + hy + " terrain " + th.ToString("F2"));
                }
            }
        }

        /// <summary>BRIDGE_FIND = "text": every POI, road or building name of the sample containing it (any language), with the
        /// bridge spans within 120 m.</summary>
        [Test, Explicit]
        public void FindName()
        {
            string q = Environment.GetEnvironmentVariable("BRIDGE_FIND");
            if (string.IsNullOrEmpty(q)) Assert.Ignore("set BRIDGE_FIND");
            foreach (string part in q.Split(';'))
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                foreach (PoiRecord p in t.Pois)
                {
                    NameRecord n = t.Name(p.NameRef);
                    if (n == null) continue;
                    string all = n.Default + " | " + string.Join(" | ", AllNames(n));
                    if (all.IndexOf(part, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    double x = t.Tile.X0 + p.XCm / 100.0, z = t.Tile.Z0 + p.ZCm / 100.0;
                    var near = new StringBuilder();
                    foreach (BridgeSpan sp in BridgeLayout.For(t).Spans)
                    {
                        double d = BridgeMesher.Distance(sp, x - t.Tile.X0, z - t.Tile.Z0);
                        if (d < 120) near.Append(' ').Append(sp.Record.OsmWayId).Append('/').Append(sp.Record.RoadClass).Append('@').Append(d.ToString("F0"));
                    }
                    TestContext.WriteLine(id + " poi " + p.Kind + " '" + all + "' at (" + x.ToString("F1") + ", " + z.ToString("F1") + ") spans:" + near);
                }
            }
        }

        private static IEnumerable<string> AllNames(NameRecord n)
        {
            var list = new List<string>();
            foreach (System.Reflection.FieldInfo f in typeof(NameRecord).GetFields())
            {
                object v = f.GetValue(n);
                if (v is string str) list.Add(str);
            }
            return list;
        }

        /// <summary>Pairs of deck spans of one tile whose footprints overlap: the centreline of A inside B's deck edges.</summary>
        [Test, Explicit]
        public void OverlappingSpans()
        {
            int pairs = 0;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (!t.HasDetail) continue;
                BridgeLayout l = BridgeLayout.For(t);
                foreach (BridgeSpan a in l.Spans)
                {
                    if (a.Stair) continue;
                    foreach (BridgeSpan b in l.Spans)
                    {
                        if (b == a || b.Stair || b.Record.OsmWayId == a.Record.OsmWayId) continue;
                        int inside = 0, n = 0;
                        double dy = 0, latSum = 0;
                        for (double s = a.Path.Start; s <= a.Path.End; s += 1.0)
                        {
                            if (!a.HasDeck(s)) continue;
                            double px, pz, ux, uz, nx, nz, tx, tz;
                            float y;
                            a.Path.Frame(s, out px, out pz, out y, out ux, out uz, out nx, out nz, out tx, out tz);
                            n++;
                            double d = BridgeMesher.Distance(b, px, pz);
                            int k = b.StationAt(Math.Max(b.Path.Start, Math.Min(b.Path.End, s)));
                            double edge = Math.Max(b.EdgeL(0), b.EdgeR(0));
                            if (d > edge) continue;
                            inside++;
                            latSum += d;
                            // b's deck height near there: the nearest station
                            int bk = 0;
                            double best = double.MaxValue;
                            for (int q = 0; q < b.Count; q++)
                            {
                                double dd = (b.Path.X[q] - px) * (b.Path.X[q] - px) + (b.Path.Z[q] - pz) * (b.Path.Z[q] - pz);
                                if (dd < best) { best = dd; bk = q; }
                            }
                            dy += y - b.Path.Y[bk];
                        }
                        if (inside < 3) continue;
                        pairs++;
                        TestContext.WriteLine(id + " A " + a.Record.OsmWayId + " " + a.Record.RoadClass + (a.Foot ? " foot" : "") + " half " + a.HalfL[0].ToString("F1") + "+" + a.WalkL[0].ToString("F1")
                                              + " inside B " + b.Record.OsmWayId + " " + b.Record.RoadClass + (b.Foot ? " foot" : "") + " half " + b.HalfL[0].ToString("F1") + "+" + b.WalkL[0].ToString("F1") + "/" + b.HalfR[0].ToString("F1") + "+" + b.WalkR[0].ToString("F1")
                                              + ": " + inside + " of " + n + " m, mean lateral " + (latSum / inside).ToString("F1") + " dy " + (dy / inside).ToString("F2"));
                    }
                }
            }
            TestContext.WriteLine("pairs " + pairs);
        }

        /// <summary>BRIDGE_SPANS = "x,z,r;...": every span within r m of the point (ends, cuts, mapped points, runs, gaps,
        /// openings) and the roads with a point within r (their end points and classes).</summary>
        [Test, Explicit]
        public void SpansNear()
        {
            string specs = Environment.GetEnvironmentVariable("BRIDGE_SPANS");
            if (string.IsNullOrEmpty(specs)) Assert.Ignore("set BRIDGE_SPANS");
            foreach (string spec in specs.Split(';'))
            {
                string[] a = spec.Split(',');
                double x = double.Parse(a[0]), z = double.Parse(a[1]), r = double.Parse(a[2]);
                TileId id = TileId.At(10, x, z);
                TileData t = StreamingSampleRegion.Tile(id);
                BridgeLayout l = BridgeLayout.For(t);
                double x0 = t.Tile.X0, z0 = t.Tile.Z0;
                TestContext.WriteLine("==== spans near " + spec + " tile " + id);
                foreach (BridgeSpan sp in l.Spans)
                {
                    if (BridgeMesher.Distance(sp, x - x0, z - z0) > r) continue;
                    TestContext.WriteLine("span " + sp.Record.OsmWayId + " " + sp.Record.RoadClass + " foot " + sp.Foot + " stair " + sp.Stair + " over " + sp.Overbridge + " ends " + sp.StartEnd + (sp.StartCut ? "(cut)" : "") + "/" + sp.EndEnd + (sp.EndCut ? "(cut)" : "")
                                          + " group " + sp.GroupWay + " partners " + sp.PartnerL + "/" + sp.PartnerR + " pts " + sp.FirstPoint + ".." + sp.LastPoint + " path " + sp.Path.Start.ToString("F1") + ".." + sp.Path.End.ToString("F1")
                                          + " trims " + sp.TrimStart.ToString("F1") + "/" + sp.TrimEnd.ToString("F1"));
                    for (int k = 0; k < sp.Count; k += Math.Max(1, sp.Count / 6))
                        TestContext.WriteLine("    st " + k + " (" + (x0 + sp.Path.X[k]).ToString("F2") + ", " + (z0 + sp.Path.Z[k]).ToString("F2") + ") s " + sp.Path.S[k].ToString("F1") + " y " + sp.Path.Y[k].ToString("F2") + " gnd " + sp.Ground[k].ToString("F2")
                                              + " half " + sp.HalfL[k].ToString("F2") + "/" + sp.HalfR[k].ToString("F2") + " walk " + sp.WalkL[k].ToString("F2") + "/" + sp.WalkR[k].ToString("F2") + " clamp " + sp.ClampL[k].ToString("F2") + "/" + sp.ClampR[k].ToString("F2"));
                    int[] p = sp.Record.Points;
                    var mp = new StringBuilder("    mapped:");
                    float[] dy = t.RoadStructureOf(sp.Road).DeckY;
                    for (int k = 0; k < sp.Record.PointCount; k++) mp.Append(" (").Append((x0 + p[2 * k] / 100.0).ToString("F1")).Append(",").Append((z0 + p[2 * k + 1] / 100.0).ToString("F1")).Append(dy != null && k < dy.Length ? " " + dy[k].ToString("F2") : "").Append(")");
                    TestContext.WriteLine(mp.ToString());
                    foreach (BridgeStair st in sp.Stairs)
                        TestContext.WriteLine("    stair top (" + (x0 + st.X).ToString("F2") + ", " + (z0 + st.Z).ToString("F2") + ") dir (" + st.Dx.ToString("F2") + ", " + st.Dz.ToString("F2") + ") y "
                                              + st.TopY.ToString("F2") + ".." + st.BottomY.ToString("F2") + " len " + st.Length.ToString("F2") + " half " + st.HalfWidth.ToString("F2") + " steps " + st.Steps
                                              + " landings " + st.Landings + " tread " + st.Tread.ToString("F2") + " bottomrun " + st.BottomRun.ToString("F2") + (st.Mapped ? " mapped" : "")
                                              + " foot (" + (x0 + st.X + st.Dx * st.Length).ToString("F2") + ", " + (z0 + st.Z + st.Dz * st.Length).ToString("F2") + ")");
                    TestContext.WriteLine("    deck " + string.Join(",", sp.DeckRuns.ConvertAll(v => v.ToString("F1"))) + " railL " + string.Join(",", sp.RailRunsL.ConvertAll(v => v.ToString("F1"))) + " railR " + string.Join(",", sp.RailRunsR.ConvertAll(v => v.ToString("F1")))
                                          + " gapsL " + string.Join(";", sp.GapsL.ConvertAll(v => v.S0.ToString("F1") + ".." + v.S1.ToString("F1") + "@" + v.Road)) + " gapsR " + string.Join(";", sp.GapsR.ConvertAll(v => v.S0.ToString("F1") + ".." + v.S1.ToString("F1") + "@" + v.Road))
                                          + " open " + string.Join(";", sp.Openings.ConvertAll(v => v.S0.ToString("F1") + ".." + v.S1.ToString("F1"))));
                }
                for (int ri = 0; ri < t.Roads.Count; ri++)
                {
                    RoadRecord rr = t.Roads[ri];
                    int[] p = rr.Points;
                    bool near = false;
                    for (int k = 0; k < rr.PointCount && !near; k++)
                    {
                        double dx = x0 + p[2 * k] / 100.0 - x, dz = z0 + p[2 * k + 1] / 100.0 - z;
                        near = dx * dx + dz * dz < r * r;
                    }
                    if (!near) continue;
                    RoadStructureRecord st = t.RoadStructureOf(ri);
                    var mp = new StringBuilder();
                    for (int k = 0; k < rr.PointCount; k++)
                    {
                        double dx = x0 + p[2 * k] / 100.0 - x, dz = z0 + p[2 * k + 1] / 100.0 - z;
                        if (dx * dx + dz * dz > 4 * r * r && k != 0 && k != rr.PointCount - 1) continue;
                        mp.Append(" ").Append(k).Append(":(").Append((x0 + p[2 * k] / 100.0).ToString("F1")).Append(",").Append((z0 + p[2 * k + 1] / 100.0).ToString("F1"))
                          .Append(st.DeckY != null && k < st.DeckY.Length ? " " + st.DeckY[k].ToString("F2") : "").Append(")");
                    }
                    double[] cov = l.CoverOf(ri);
                    if (cov != null) TestContext.WriteLine("    stair cover of road #" + ri + ": " + string.Join(",", Array.ConvertAll(cov, v => v.ToString("F2"))));
                    TestContext.WriteLine("road #" + ri + " " + rr.OsmWayId + " " + rr.RoadClass + " kind " + st.Kind + " sflags " + st.Flags + " elevated " + st.IsElevated + mp);
                }
            }
        }
    }
}
