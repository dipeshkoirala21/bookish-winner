using System;
using System.Collections.Generic;
using System.Linq;
using Ghumante.Core.Data;
using Ghumante.Core.Generators.Flora;
using Ghumante.Core.Generators.Placement;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>W2_DESIGN 5.8 and 4.5-4.7: trees, plants and street props on the W2 sample pack and synthetic tiles.</summary>
    public class GeneratorsPlacementTests
    {
        private static readonly TileId Leaf = new TileId(10, 516, 161);

        private static IEnumerable<TileId> SomeTiles()
        {
            return StreamingSampleRegion.TilesAt(10).OrderBy(i => i.Key).Where((_, k) => k % 6 == 0);
        }

        /// <summary>A straight road corridor along x at a fixed z (tile-local metres), for the corridor rules.</summary>
        internal sealed class BandCorridor : IRoadCorridorQuery
        {
            public double Z, Half;

            public double SignedDistance(double x, double z)
            {
                return Math.Abs(z - Z) - Half;
            }

            public bool Overlaps(double[] x, double[] z, int n, out double depthM)
            {
                depthM = 0;
                for (int i = 0; i < n; i++) depthM = Math.Max(depthM, -SignedDistance(x[i], z[i]));
                return depthM > 0;
            }
        }

        [Test]
        public void TreesKeepOsmPositionsAndGeneratedOnesAvoidRoadsBuildingsAndWater()
        {
            int osm = 0, generated = 0, plants = 0;
            var kinds = new HashSet<TreeSpecies>();
            foreach (TileId id in SomeTiles())
            {
                TileData t = StreamingSampleRegion.Tile(id);
                var h = new TileHeightSampler(t, 2);
                var trees = new List<TreeInstance>();
                var o = new TreePlacementOptions();
                int n = TreePlacement.Place(t, h, o, trees);
                Assert.That(n, Is.EqualTo(trees.Count));
                Assert.That(trees.Count(tr => tr.Origin == TreeOrigin.Forest && FloraCatalog.IsTree(tr.Species)), Is.LessThanOrEqualTo(o.MaxForestTrees));
                Assert.That(trees.Count(tr => !FloraCatalog.IsTree(tr.Species)), Is.LessThanOrEqualTo(o.MaxPlants));
                var mask = new PlacementMask(t);
                int osmProps = t.Props.Count(p => p.Kind == ObjectKind.Tree);
                Assert.That(trees.Count(tr => tr.Origin == TreeOrigin.Osm), Is.EqualTo(osmProps), id + ": every OSM tree (no corridor query: none moved)");
                foreach (TreeInstance tr in trees)
                {
                    Assert.That(float.IsNaN(tr.Y) || float.IsInfinity(tr.Y), Is.False);
                    FloraInfo info = FloraCatalog.Info(tr.Species);
                    if (FloraCatalog.IsTree(tr.Species)) Assert.That(tr.HeightM, Is.GreaterThan(1f).And.LessThan(45f));
                    else Assert.That(tr.HeightM, Is.InRange(0.15f, 4.5f), tr.Species.ToString());
                    Assert.That(tr.CrownM / tr.HeightM, Is.InRange(info.Aspect * 0.8f, info.Aspect * 1.25f), tr.Species + " keeps the model's proportions");
                    Assert.That(tr.Shape, Is.EqualTo(info.Family));
                    kinds.Add(tr.Species);
                    if (tr.Origin == TreeOrigin.Osm)
                    {
                        osm++;
                        continue;
                    }
                    generated++;
                    if (!FloraCatalog.IsTree(tr.Species)) plants++;
                    Assert.That(tr.X, Is.InRange(0f, (float)t.Tile.Size));
                    Assert.That(tr.Z, Is.InRange(0f, (float)t.Tile.Size));
                    // Park path hedges stand right beside the way they line (their 4 m mask cell may be a road cell);
                    // they keep off buildings and water like everything else.
                    if (tr.Species == TreeSpecies.Hedge && tr.Origin == TreeOrigin.Park)
                        Assert.That(mask.At(tr.X, tr.Z) & (PlacementMask.Building | PlacementMask.Water), Is.EqualTo(0), id + " path hedge at " + tr.X + ", " + tr.Z);
                    else Assert.That(mask.Free(tr.X, tr.Z), Is.True, id + " " + tr.Origin + " " + tr.Species + " at " + tr.X + ", " + tr.Z);
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
            Assert.That(plants, Is.GreaterThan(100), "gardens, parks and ground cover");
            Assert.That(kinds.Contains(TreeSpecies.PottedPlant), Is.True, "pots by the doors");
        }

        [Test]
        public void OsmTreesKeepTheirClassAndChautari()
        {
            TileData t = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300);
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
            Assert.That(pipal.Species, Is.EqualTo(TreeSpecies.Pipal));
            Assert.That(bar.Species, Is.EqualTo(TreeSpecies.Bar));
            var props = new List<StreetProp>();
            PropPlacement.Place(t, new TileHeightSampler(t, 1), props);
            Assert.That(props.Count, Is.EqualTo(1));
            Assert.That(props[0].Kind, Is.EqualTo(StreetPropKind.StreetLamp));
            Assert.That(props[0].YawDeg, Is.EqualTo(90f));
            Assert.That(props[0].Osm, Is.True);
        }

        [Test]
        public void NothingStandsInARoadCorridorAndOsmTreesInOneMoveOut()
        {
            TileData t = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1350 + 0.02 * (x - Leaf.X0), 129, Biome.HillGrassland);
            // An OSM pipal 1 m off the corridor's centreline (moved out) and one far inside a wide corridor (dropped).
            t.Props.Add(new PropRecord { OsmRef = 5 << 2, Kind = ObjectKind.Tree, Subtype = (byte)TreeClass.Pipal, XCm = 30000, ZCm = 50100 });
            var corridor = new BandCorridor { Z = 500, Half = 5 };
            var trees = new List<TreeInstance>();
            TreePlacement.Place(t, new TileHeightSampler(t, 1), new TreePlacementOptions(), corridor, trees);
            TreeInstance moved = trees.Single(tr => tr.OsmRef == 5 << 2);
            Assert.That(corridor.SignedDistance(moved.X, moved.Z), Is.GreaterThanOrEqualTo(1.19), "the OSM tree moved out of the corridor");
            Assert.That(Math.Abs(moved.X - 300f), Is.LessThan(0.5f), "moved across the road, not along it");
            Assert.That(trees.Count, Is.GreaterThan(50), "grassland ground cover");
            foreach (TreeInstance tr in trees)
                Assert.That(corridor.SignedDistance(tr.X, tr.Z), Is.GreaterThanOrEqualTo(Margin(tr) - 1e-6), tr.Species + " at " + tr.X + ", " + tr.Z);
            var wide = new BandCorridor { Z = 500, Half = 30 };
            var none = new List<TreeInstance>();
            TreePlacement.Place(t, new TileHeightSampler(t, 1), new TreePlacementOptions(), wide, none);
            Assert.That(none.Any(tr => tr.OsmRef == 5 << 2), Is.False, "deep inside a corridor: dropped");
            // The options' corridor provider is used by the 4-argument overload.
            var viaOptions = new List<TreeInstance>();
            TreePlacement.Place(t, new TileHeightSampler(t, 1), new TreePlacementOptions { Corridors = _ => corridor }, viaOptions);
            Assert.That(viaOptions.Count, Is.EqualTo(trees.Count));
        }

        /// <summary>The least corridor distance an instance must keep: big trees their trunk (1.2 m), the rest half
        /// their crown plus 0.3 m.</summary>
        private static double Margin(TreeInstance tr)
        {
            return tr.Origin == TreeOrigin.Osm ? 1.2 : Math.Min(1.2, 0.5 * tr.CrownM + 0.3);
        }

        [Test]
        public void ParksGetBedsHedgesAndOrnamentalTrees()
        {
            TileData t = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300);
            // A 120 × 80 m park as two triangles (counter-clockwise, centimetres).
            int[] v = { 30000, 30000, 42000, 30000, 42000, 38000, 30000, 38000 };
            t.Areas.Add(new AreaRecord { OsmRef = 77, Kind = AreaKind.Park, Vertices = v, Indices = new[] { 0, 1, 2, 0, 2, 3 } });
            var trees = new List<TreeInstance>();
            TreePlacement.Place(t, new TileHeightSampler(t, 1), new TreePlacementOptions(), trees);
            List<TreeInstance> park = trees.Where(tr => tr.Origin == TreeOrigin.Park).ToList();
            Assert.That(park.Count(tr => tr.Species == TreeSpecies.MarigoldBed), Is.GreaterThanOrEqualTo(4));
            Assert.That(park.Count(tr => tr.Species == TreeSpecies.Hedge), Is.GreaterThan(10));
            Assert.That(park.Count(tr => FloraCatalog.IsTree(tr.Species)), Is.GreaterThan(10));
            foreach (TreeInstance tr in park)
            {
                Assert.That(tr.X, Is.InRange(299f, 421f));
                Assert.That(tr.Z, Is.InRange(299f, 381f));
            }
            // Hedges run along the boundary: turned to 0 or 90 degrees on this rectangle.
            foreach (TreeInstance hdg in park.Where(tr => tr.Species == TreeSpecies.Hedge))
            {
                float yaw = (hdg.YawDeg % 90f + 90f) % 90f;
                Assert.That(Math.Min(yaw, 90f - yaw), Is.LessThan(0.5f), "hedge along the edge");
            }
        }

        [Test]
        public void VillageHousesGetGardensOutsideTheirWalls()
        {
            TileData t = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1400, 129, Biome.HillTerraces);
            for (int k = 0; k < 40; k++)
            {
                int x0 = 10000 + (k % 8) * 2500, z0 = 10000 + (k / 8) * 2500;
                t.Buildings.Add(new BuildingRecord
                {
                    OsmRef = (ulong)(k + 1) << 2, Seed = (uint)(k * 7919 + 1), Levels = 2,
                    Rings = new[] { new[] { x0, z0, x0 + 900, z0, x0 + 900, z0 + 700, x0, z0 + 700 } },
                });
            }
            var trees = new List<TreeInstance>();
            TreePlacement.Place(t, new TileHeightSampler(t, 1), new TreePlacementOptions { GroundCover = false }, trees);
            List<TreeInstance> garden = trees.Where(tr => tr.Origin == TreeOrigin.Garden).ToList();
            Assert.That(garden.Count, Is.GreaterThan(15));
            Assert.That(garden.Any(tr => tr.Species == TreeSpecies.PottedPlant), Is.True);
            Assert.That(garden.Any(tr => tr.Species == TreeSpecies.Banana || tr.Species == TreeSpecies.Bamboo), Is.True, "banana and bamboo by village houses");
            var mask = new PlacementMask(t);
            foreach (TreeInstance tr in garden)
            {
                Assert.That(mask.Free(tr.X, tr.Z), Is.True, tr.Species + " inside a building");
                bool near = t.Buildings.Any(b => tr.X > b.Rings[0][0] / 100f - 11 && tr.X < b.Rings[0][2] / 100f + 11 && tr.Z > b.Rings[0][1] / 100f - 11 &&
                                                 tr.Z < b.Rings[0][5] / 100f + 11);
                Assert.That(near, Is.True, tr.Species + " far from any house");
            }
        }

        [Test]
        public void HillsGetGrassAndRocksSteepSlopesBouldersFieldsStrawStacks()
        {
            // A steep grassy hillside in the west half, flat cropland in the east half.
            TileData t = MeshingChecks.SyntheticTile(Leaf, (x, z) => x - Leaf.X0 < 512 ? 1600 - 0.7 * (x - Leaf.X0) : 1241.6, 129, Biome.HillGrassland);
            for (int k = 0; k < t.Biomes.Length; k++)
                if (k % 65 >= 34) t.Biomes[k] = Biome.ValleyCropland;
            var trees = new List<TreeInstance>();
            TreePlacement.Place(t, new TileHeightSampler(t, 1), new TreePlacementOptions(), trees);
            Assert.That(trees.Count(tr => tr.Species == TreeSpecies.GrassTuft && tr.X < 500), Is.GreaterThan(100));
            Assert.That(trees.Count(tr => tr.Species == TreeSpecies.Boulder && tr.X < 500), Is.GreaterThan(10), "rocky outcrops on the steep slope");
            Assert.That(trees.Count(tr => tr.Species == TreeSpecies.StrawStack && tr.X > 540), Is.GreaterThan(10), "straw stacks on the flat fields");
            Assert.That(trees.Count(tr => tr.Species == TreeSpecies.StrawStack && tr.X < 500), Is.EqualTo(0), "none on the slope");
            int plants = trees.Count(tr => !FloraCatalog.IsTree(tr.Species));
            var capped = new List<TreeInstance>();
            TreePlacement.Place(t, new TileHeightSampler(t, 1), new TreePlacementOptions { MaxPlants = 50 }, capped);
            Assert.That(capped.Count(tr => !FloraCatalog.IsTree(tr.Species)), Is.EqualTo(Math.Min(50, plants)), "the plant budget holds");
        }

        [Test]
        public void ForestBandsFollowElevationAndAspect()
        {
            var rng = new FloraRng(1, 2);
            var counts = new Dictionary<TreeSpecies, int>();
            for (int i = 0; i < 2000; i++)
            {
                TreeSpecies s = TreePlacement.ForestSpecies(1600, true, ref rng);
                counts[s] = counts.TryGetValue(s, out int c) ? c + 1 : 1;
            }
            Assert.That(counts[TreeSpecies.ChirPine], Is.InRange(1000, 1400), "60% chir pine on south faces at 1,600 m");
            rng = new FloraRng(3, 4);
            for (int i = 0; i < 200; i++)
            {
                Assert.That(TreePlacement.ForestSpecies(2600, false, ref rng), Is.AnyOf(TreeSpecies.BrownOak, TreeSpecies.Rhododendron));
                Assert.That(TreePlacement.ForestSpecies(1350, false, ref rng), Is.Not.EqualTo(TreeSpecies.ChirPine));
            }
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
            // With a corridor query the road cells follow it exactly.
            TileData s = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300);
            var cm = new PlacementMask(s, new BandCorridor { Z = 200, Half = 6 });
            Assert.That(cm.Free(100, 200), Is.False);
            Assert.That(cm.Free(100, 205.5), Is.False);
            Assert.That(cm.Free(100, 212), Is.True);
        }
    }
}
