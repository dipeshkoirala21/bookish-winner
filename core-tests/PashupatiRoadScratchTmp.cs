using System;
using System.Text;
using Ghumante.Core.Data;
using Ghumante.Core.Generators.Sacred;
using Ghumante.Core.Generators.Sacred.Heroes.Pashupati;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class PashupatiRoadScratchTmp
    {
        [Test]
        public void MapDrawnRoadsAtTheGates()
        {
            TileData t = StreamingSampleRegion.Tile(GeneratorsSacredPashupatiPreview.TempleTile);
            SacredDrawnRoads drawn = SacredDrawnRoads.For(t);
            var f = PashupatiFrame.For(t, 0, null);
            foreach (var (name, a0, a1, b0, b1) in new[] { ("west", -46.0, -30.0, -8.0, 9.0), ("east", 18.0, 40.0, 2.0, 14.0) })
            {
                var sb = new StringBuilder();
                sb.AppendLine(name + " rows b from " + b1 + " down, cols a from " + a0 + " step 0.5");
                for (double b = b1; b >= b0; b -= 0.5)
                {
                    sb.Append(b.ToString("0.0").PadLeft(6)).Append(' ');
                    for (double a = a0; a <= a1; a += 0.5)
                    {
                        double x, z;
                        f.ToTile(a, b, out x, out z);
                        double sd = drawn.SignedDistance(x, z);
                        sb.Append(sd < -0.15 ? '#' : sd < 0 ? '+' : '.');
                    }
                    sb.AppendLine();
                }
                TestContext.WriteLine(sb.ToString());
            }
            foreach (RoadRecord r in t.Roads)
            {
                for (int k = 0; k < r.PointCount; k++)
                {
                    double a, b;
                    f.FromTile(r.Points[2 * k] / 100.0, r.Points[2 * k + 1] / 100.0, out a, out b);
                    if (a > -60 && a < 45 && b > -40 && b < 40)
                    {
                        TestContext.WriteLine("road osm {0} class {1} width {2} cm", r.OsmWayId, r.RoadClass, r.WidthCm);
                        break;
                    }
                }
            }
        }
    }
}
