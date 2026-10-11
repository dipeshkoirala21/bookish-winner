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
    }
}
