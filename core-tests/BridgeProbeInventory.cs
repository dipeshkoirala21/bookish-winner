using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Meshing.Bridges;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class BridgeProbeInventory
    {
        [Test, Explicit]
        public void Inventory()
        {
            int tiles = 0, rstr = 0;
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                tiles++;
                if (t.HasRoadStructures) rstr++;
                if (!t.HasDetail) continue;
                BridgeLayout l = BridgeLayout.For(t);
                foreach (BridgeSpan sp in l.Spans)
                {
                    NameRecord n = t.Name(sp.Record.NameRef);
                    float gmin = float.MaxValue, dmax = float.MinValue, rise = float.MinValue;
                    for (int k = 0; k < sp.Count; k++)
                    {
                        gmin = Math.Min(gmin, sp.Ground[k]);
                        dmax = Math.Max(dmax, sp.Path.Y[k]);
                        rise = Math.Max(rise, sp.Path.Y[k] - sp.Ground[k]);
                    }
                    var cross = new List<string>();
                    foreach (BridgeCrossing c in sp.Crossings)
                    {
                        NameRecord cn = t.Name(t.Roads[c.Road].NameRef);
                        cross.Add((cn != null ? cn.Default : "?") + "/" + t.Roads[c.Road].OsmWayId + "/" + t.Roads[c.Road].RoadClass + " low=" + c.LowY.ToString("F1"));
                    }
                    var open = new List<string>();
                    foreach (BridgeGap c in sp.Openings)
                        open.Add(c.Road >= 0 ? t.Roads[c.Road].OsmWayId + "/" + t.Roads[c.Road].RoadClass : "-");
                    TestContext.WriteLine(id + " way " + sp.Record.OsmWayId + " '" + (n != null ? n.Default : "") + "' " + sp.Record.RoadClass + " kind=" + sp.Kind
                                          + (sp.Foot ? " foot" : "") + (sp.Overbridge ? " overbridge" : "") + (sp.Water ? " water" : "") + (sp.Stair ? " stair" : "")
                                          + " len=" + sp.Length.ToString("F0") + " rise=" + rise.ToString("F1") + " stairs=" + sp.Stairs.Count
                                          + " ends=" + sp.StartEnd + "/" + sp.EndEnd + " cross=[" + string.Join("; ", cross) + "] open=[" + string.Join("; ", open) + "]");
                }
                foreach (BridgeSpan sp in l.Trenches)
                    TestContext.WriteLine(id + " trench way " + sp.Record.OsmWayId + " len=" + sp.Length.ToString("F0"));
                foreach (BridgeIssue i in l.Issues)
                    TestContext.WriteLine(id + " ISSUE way " + i.Way + " over " + i.LowerWay + " deckAbove=" + i.DeckAboveM);
            }
            TestContext.WriteLine("tiles " + tiles + " with RSTR " + rstr);
        }

        /// <summary>Roads whose name contains BRIDGE_NAME (comma-separated alternatives), over the whole sample.</summary>
        [Test, Explicit]
        public void FindNamed()
        {
            string spec = Environment.GetEnvironmentVariable("BRIDGE_NAME");
            if (string.IsNullOrEmpty(spec)) Assert.Ignore("set BRIDGE_NAME");
            string[] names = spec.Split(',');
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                if (!t.HasDetail) continue;
                for (int ri = 0; ri < t.Roads.Count; ri++)
                {
                    NameRecord n = t.Name(t.Roads[ri].NameRef);
                    if (n == null || n.Default == null) continue;
                    foreach (string q in names)
                        if (n.Default.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                            TestContext.WriteLine(id + " way " + t.Roads[ri].OsmWayId + " '" + n.Default + "' " + t.Roads[ri].RoadClass + " " + t.Roads[ri].Flags + " kind " + t.RoadStructureOf(ri).Kind
                                                  + " at (" + (t.Tile.X0 + t.Roads[ri].Points[0] / 100.0).ToString("F0") + ", " + (t.Tile.Z0 + t.Roads[ri].Points[1] / 100.0).ToString("F0") + ")");
                }
            }
        }

        /// <summary>Roads of one tile (BRIDGE_TILE = "tx,ty") that carry structure heights or are elevated, with their record
        /// kind and flags and whether the bridges package covers them (a span or a trench of the layout).</summary>
        [Test, Explicit]
        public void StructureRoads()
        {
            string spec = Environment.GetEnvironmentVariable("BRIDGE_TILE");
            if (string.IsNullOrEmpty(spec)) Assert.Ignore("set BRIDGE_TILE");
            string[] a = spec.Split(',');
            TileData t = StreamingSampleRegion.Tile(new TileId(10, int.Parse(a[0]), int.Parse(a[1])));
            var idx = new RoadSpatialIndex(t);
            BridgeLayout l = BridgeLayout.For(t);
            var span = new HashSet<int>();
            var trench = new HashSet<int>();
            foreach (BridgeSpan sp in l.Spans) span.Add(sp.Road);
            foreach (BridgeSpan sp in l.Trenches) trench.Add(sp.Road);
            for (int ri = 0; ri < t.Roads.Count; ri++)
            {
                float[] h = idx.SurfaceHeights(ri);
                if (h == null && !idx.IsElevated(ri)) continue;
                RoadRecord r = t.Roads[ri];
                RoadStructureRecord st = t.RoadStructureOf(ri);
                float lo = float.MaxValue, hi = float.MinValue;
                if (h != null)
                    foreach (float y in h)
                        if (!float.IsNaN(y))
                        {
                            lo = Math.Min(lo, y);
                            hi = Math.Max(hi, y);
                        }
                TestContext.WriteLine("#" + ri + " way " + r.OsmWayId + " " + r.RoadClass + " kind " + st.Kind + " flags " + st.Flags + " elevated " + idx.IsElevated(ri)
                                      + " heights " + (h != null ? lo.ToString("F1") + ".." + hi.ToString("F1") : "-") + (span.Contains(ri) ? " SPAN" : "") + (trench.Contains(ri) ? " TRENCH" : "")
                                      + " first (" + (t.Tile.X0 + r.Points[0] / 100.0).ToString("F1") + ", " + (t.Tile.Z0 + r.Points[1] / 100.0).ToString("F1") + ")");
            }
        }
    }
}
