using System;
using System.Collections.Generic;
using System.Linq;
using Ghumante.Core.Data;
using Ghumante.Core.Generators.Placement;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>W2_DESIGN 5.8 and 4.5-4.7: trees and street props on the W2 sample pack.</summary>
    public class GeneratorsPlacementTests
    {
        private static IEnumerable<TileId> SomeTiles()
        {
            return StreamingSampleRegion.TilesAt(10).OrderBy(i => i.Key).Where((_, k) => k % 6 == 0);
        }

        [Test]
        public void TreesKeepOsmPositionsAndGeneratedOnesAvoidRoadsBuildingsAndWater()
        {
            int osm = 0, generated = 0;
            foreach (TileId id in SomeTiles())
            {
                TileData t = StreamingSampleRegion.Tile(id);
                var h = new TileHeightSampler(t, 2);
                var trees = new List<TreeInstance>();
                int n = TreePlacement.Place(t, h, new TreePlacementOptions(), trees);
                Assert.That(n, Is.EqualTo(trees.Count));
                Assert.That(trees.Count(tr => tr.Origin == TreeOrigin.Forest), Is.LessThanOrEqualTo(new TreePlacementOptions().MaxForestTrees));
                var mask = new PlacementMask(t);
                int osmProps = t.Props.Count(p => p.Kind == ObjectKind.Tree);
                Assert.That(trees.Count(tr => tr.Origin == TreeOrigin.Osm), Is.EqualTo(osmProps), id + ": every OSM tree");
                foreach (TreeInstance tr in trees)
                {
                    Assert.That(float.IsNaN(tr.Y) || float.IsInfinity(tr.Y), Is.False);
                    Assert.That(tr.HeightM, Is.GreaterThan(1f).And.LessThan(45f));
                    if (tr.Origin == TreeOrigin.Osm)
                    {
                        osm++;
                        continue;
                    }
                    generated++;
                    Assert.That(tr.X, Is.InRange(0f, (float)t.Tile.Size));
                    Assert.That(tr.Z, Is.InRange(0f, (float)t.Tile.Size));
                    Assert.That(mask.Free(tr.X, tr.Z), Is.True, id + " " + tr.Origin + " tree at " + tr.X + ", " + tr.Z);
                }
                // Deterministic.
                var again = new List<TreeInstance>();
                TreePlacement.Place(t, h, new TreePlacementOptions(), again);
                Assert.That(again.Count, Is.EqualTo(trees.Count));
                for (int i = 0; i < trees.Count; i++)
                    Assert.That(again[i].X == trees[i].X && again[i].Z == trees[i].Z && again[i].Species == trees[i].Species, Is.True);
            }
            Assert.That(osm, Is.GreaterThan(0));
            Assert.That(generated, Is.GreaterThan(0));
        }

        [Test]
        public void OsmTreesKeepTheirClassAndChautari()
        {
            TileData t = MeshingChecks.SyntheticTile(new TileId(10, 516, 161), (x, z) => 1300);
            t.Props.Add(new PropRecord { OsmRef = 1 << 2, Kind = ObjectKind.Tree, Subtype = (byte)TreeClass.Pipal, Flags = PropFlags.Chautari, XCm = 20000, ZCm = 30000 });
            t.Props.Add(new PropRecord { OsmRef = 2 << 2, Kind = ObjectKind.Tree, Subtype = (byte)TreeClass.Bar, XCm = 40000, ZCm = 30000, HeightDm = 140 });
            t.Props.Add(new PropRecord { OsmRef = 3 << 2, Kind = ObjectKind.StreetLamp, Flags = PropFlags.Yaw, XCm = 50000, ZCm = 30000, YawCdeg = 9000 });
            var trees = new List<TreeInstance>();
            TreePlacement.Place(t, new TileHeightSampler(t, 1), new TreePlacementOptions(), trees);
            Assert.That(trees.Count(tr => tr.Origin == TreeOrigin.Osm), Is.EqualTo(2));
            TreeInstance pipal = trees.First(tr => tr.OsmRef == 1 << 2), bar = trees.First(tr => tr.OsmRef == 2 << 2);
            Assert.That(pipal.Chautari, Is.True);
            Assert.That(bar.Chautari, Is.False);
            Assert.That(pipal.X, Is.EqualTo(200f).Within(1e-3));
            Assert.That(bar.HeightM, Is.EqualTo(14f).Within(1e-3), "a tagged height wins");
            Assert.That(pipal.Species, Is.Not.EqualTo(bar.Species));
            var props = new List<StreetProp>();
            PropPlacement.Place(t, new TileHeightSampler(t, 1), props);
            Assert.That(props.Count, Is.EqualTo(1));
            Assert.That(props[0].Kind, Is.EqualTo(StreetPropKind.StreetLamp));
            Assert.That(props[0].YawDeg, Is.EqualTo(90f));
            Assert.That(props[0].Osm, Is.True);
        }

        [Test]
        public void StreetPropsKeepOsmObjectsAndLightTheStreets()
        {
            int osm = 0, lamps = 0;
            foreach (TileId id in SomeTiles())
            {
                TileData t = StreamingSampleRegion.Tile(id);
                var h = new TileHeightSampler(t, 2);
                var props = new List<StreetProp>();
                Assert.That(PropPlacement.Place(t, h, props), Is.EqualTo(props.Count));
                var mask = new PlacementMask(t);
                int expected = t.Props.Count(p => { StreetPropKind k; return PropPlacement.KindOf(p.Kind, out k); });
                Assert.That(props.Count(p => p.Osm), Is.EqualTo(expected), id + ": every mappable OSM object");
                RoadLayout layout = RoadLayout.For(t);
                foreach (StreetProp p in props)
                {
                    if (p.Osm)
                    {
                        osm++;
                        continue;
                    }
                    if (p.Kind != StreetPropKind.StreetLamp) continue;
                    lamps++;
                    Assert.That(p.HeightM, Is.InRange(9f, 12f));
                    Assert.That((mask.At(p.X, p.Z) & PlacementMask.Building) == 0, Is.True, "a lamp inside a building");
                    // Generated lamps are not within 15 m of an OSM lamp.
                    foreach (StreetProp q in props)
                        if (q.Osm && q.Kind == StreetPropKind.StreetLamp)
                            Assert.That(Math.Sqrt((q.X - p.X) * (q.X - p.X) + (q.Z - p.Z) * (q.Z - p.Z)), Is.GreaterThanOrEqualTo(15.0));
                }
                var again = new List<StreetProp>();
                PropPlacement.Place(t, h, again);
                Assert.That(again.Select(p => p.X + p.Z), Is.EqualTo(props.Select(p => p.X + p.Z)));
            }
            Assert.That(lamps, Is.GreaterThan(50));
        }

        [Test]
        public void TheMaskMatchesTheDrawnRoadWidths()
        {
            TileData t = StreamingSampleRegion.Tile(new TileId(10, 516, 161));
            var mask = new PlacementMask(t);
            RoadLayout layout = RoadLayout.For(t);
            int checkedRoads = 0;
            for (int ri = 0; ri < t.Roads.Count && checkedRoads < 200; ri++)
            {
                if ((t.Roads[ri].Flags & RoadFlags.Tunnel) != 0 || layout.Profiles[ri].LengthM < 10) continue;
                RoadCut c = layout.CutAt(t, ri, 0.5 * layout.Profiles[ri].LengthM);
                Assert.That(mask.At(c.CX, c.CZ) & PlacementMask.Road, Is.EqualTo(PlacementMask.Road));
                double e = c.Shift + 0.9 * c.Half;
                Assert.That(mask.At(c.CX + c.UX * e, c.CZ + c.UZ * e) & PlacementMask.Road, Is.EqualTo(PlacementMask.Road), "road " + ri);
                checkedRoads++;
            }
            Assert.That(mask.At(-5, 10), Is.EqualTo(PlacementMask.Road), "off the tile is never free");
            Assert.That(mask.Free(-5, 10), Is.False);
        }
    }
}
