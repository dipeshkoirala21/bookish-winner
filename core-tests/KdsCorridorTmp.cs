using System;
using Ghumante.Core.Data;
using Ghumante.Core.Generators;
using Ghumante.Core.Generators.Sacred;
using Ghumante.Core.Generators.Sacred.Heroes.KdsTemples;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>TEMPORARY: where each structure stands and how near the road corridors are.</summary>
    public class KdsCorridorTmp
    {
        [Test]
        public void Corridors()
        {
            TileData tile = StreamingSampleRegion.Tile(new TileId(10, 516, 161));
            double gx, gz;
            WorldFrame.LonLatToGame(KdsTemplesSite.OriginLon, KdsTemplesSite.OriginLat, out gx, out gz);
            double ox = gx - tile.Tile.X0, oz = gz - tile.Tile.Z0;
            foreach (string id in KdsTemplesSite.Ids)
            {
                KdsTemplesSite.Entry e;
                KdsTemplesSite.TryEntry(id, out e);
                KdsCtx x;
                if (!KdsTemplesSite.Locate(e, tile, 0, new MeshData(), null, null, out x)) { TestContext.WriteLine(id + " not located"); continue; }
                double minD = double.MaxValue, minU = 0, minW = 0;
                for (int s = 0; s < 4; s++)
                    for (int q = 0; q <= 20; q++)
                    {
                        double u, w;
                        SacredParts.OnRect(s, -1 + q / 10.0, x.PlanHu, x.PlanHw, out u, out w);
                        double d = x.CorridorDistance(u, w);
                        if (d < minD) { minD = d; minU = u; minW = w; }
                    }
                double c0 = x.CorridorDistance(0, 0);
                TestContext.WriteLine("{0,-36} at ({1,7:0.0},{2,7:0.0}) yaw {3,6:0.0} half {4,5:0.0}x{5,5:0.0} centreD {6,5:0.0} outlineMinD {7,5:0.0} at u {8,5:0.0} w {9,5:0.0} g {10:0.00}",
                    id, x.Kf.OX - ox, -(x.Kf.OZ - oz) * 0 + (x.Kf.OZ - oz), x.F.YawDeg, x.PlanHu, x.PlanHw, c0, minD, minU, minW, x.F.GroundY);
            }
        }
    }
}
