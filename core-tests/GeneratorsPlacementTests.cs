using System;
using System.Collections.Generic;
using System.Linq;
using Ghumante.Core.Data;
using Ghumante.Core.Generators.Flora;
using Ghumante.Core.Generators.Placement;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Roads;
using Ghumante.Core.Geo;
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

        /// <summary>The least corridor distance an instance must keep: a tree everything it has below the 4.5 m
        /// overhead clearance (its measured low reach) plus 0.3 m and at least 1.2 m, the rest half their crown plus
        /// 0.3 m.</summary>
        private static double Margin(TreeInstance tr)
        {
            return PlacementContext.RoadMarginFor(tr.Species, tr.HeightM, tr.CrownM);
        }

        /// <summary>A rideable corridor round every way of a tile (tile-local metres): its drawn half width, at least
        /// half of <see cref="RoadClearance.MinCorridorM"/>, as RoadCorridorIndex draws it for footways too.</summary>
        internal sealed class WaysCorridor : IRoadCorridorQuery
        {
            private readonly TileData _t;

            public WaysCorridor(TileData t)
            {
                _t = t;
            }

            public double SignedDistance(double x, double z)
            {
                double best = double.MaxValue;
                foreach (RoadRecord r in _t.Roads)
                {
                    double half = Math.Max(0.5 * (r.WidthCm > 0 ? r.WidthCm / 100.0 : RoadStyle.DefaultWidthM(r.RoadClass)), 0.5 * RoadClearance.MinCorridorM);
                    for (int k = 0; k + 1 < r.PointCount; k++)
                    {
                        double ax = r.Points[2 * k] / 100.0, az = r.Points[2 * k + 1] / 100.0, bx = r.Points[2 * k + 2] / 100.0, bz = r.Points[2 * k + 3] / 100.0;
                        double dx = bx - ax, dz = bz - az, l2 = dx * dx + dz * dz;
                        double f = l2 > 1e-9 ? Math.Max(0, Math.Min(1, ((x - ax) * dx + (z - az) * dz) / l2)) : 0;
                        double ex = ax + dx * f - x, ez = az + dz * f - z;
                        best = Math.Min(best, Math.Sqrt(ex * ex + ez * ez) - half);
                    }
                }
                return best;
            }

            public bool Overlaps(double[] x, double[] z, int n, out double depthM)
            {
                depthM = 0;
                for (int i = 0; i < n; i++) depthM = Math.Max(depthM, -SignedDistance(x[i], z[i]));
                return depthM > 0;
            }
        }

        [Test]
        public void OsmTreesKeepTheirLowPartsAndPlatformsOutOfTheRoad()
        {
            // A bar (prop roots 6-8 m out) and a pipal on a chautari, both standing at the edge of a 10 m road.
            TileData t = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300, 129, Biome.UrbanGreen);
            t.Props.Add(new PropRecord { OsmRef = 7 << 2, Kind = ObjectKind.Tree, Subtype = (byte)TreeClass.Bar, XCm = 30000, ZCm = 50600, HeightDm = 180 });
            t.Props.Add(new PropRecord { OsmRef = 8 << 2, Kind = ObjectKind.Tree, Subtype = (byte)TreeClass.Pipal, Flags = PropFlags.Chautari, XCm = 60000, ZCm = 49200, HeightDm = 220 });
            var corridor = new BandCorridor { Z = 500, Half = 5 };
            var trees = new List<TreeInstance>();
            TreePlacement.Place(t, new TileHeightSampler(t, 1), new TreePlacementOptions { GroundCover = false }, corridor, trees);
            TreeInstance bar = trees.Single(tr => tr.OsmRef == 7 << 2), pipal = trees.Single(tr => tr.OsmRef == 8 << 2);
            double reach = FloraReach.LowReachM(TreeSpecies.Bar, bar.HeightM, bar.CrownM);
            Assert.That(reach, Is.GreaterThan(4.0), "the bar's prop roots reach far out below 4.5 m");
            Assert.That(corridor.SignedDistance(bar.X, bar.Z), Is.GreaterThanOrEqualTo(reach + 0.3 - 1e-6), "prop roots clear of the road");
            Assert.That(Math.Abs(bar.X - 300f), Is.LessThan(0.5f), "moved straight away from the road");
            // The pipal keeps its chautari: the whole turned platform (slab, porter ledge and step) outside the corridor,
            // the ledge facing the road.
            Assert.That(pipal.Chautari, Is.True);
            Assert.That(pipal.PlatformM, Is.InRange(3f, 9f));
            Assert.That(TreePlacement.PlatformClearance(corridor, pipal.X, pipal.Z, pipal.YawDeg, pipal.PlatformM), Is.GreaterThanOrEqualTo(0.3 - 1e-6));
            double a = pipal.YawDeg * Math.PI / 180;
            double ledgeX = pipal.X - 0.6 * pipal.PlatformM * Math.Sin(a), ledgeZ = pipal.Z - 0.6 * pipal.PlatformM * Math.Cos(a);
            Assert.That(corridor.SignedDistance(ledgeX, ledgeZ), Is.LessThan(corridor.SignedDistance(pipal.X, pipal.Z)), "the porter ledge faces the road");
            Assert.That(corridor.SignedDistance(pipal.X, pipal.Z), Is.GreaterThanOrEqualTo(PlacementContext.RoadMarginFor(TreeSpecies.Pipal, pipal.HeightM, pipal.CrownM) - 1e-6));
            // Every species' margin covers its measured low reach (a small tree's whole crown, a big one's trunk and
            // roots: a crown that starts above 4.5 m may overhang the road).
            Assert.That(PlacementContext.RoadMarginFor(TreeSpecies.Eucalyptus, 30f, 30f * FloraCatalog.Info(TreeSpecies.Eucalyptus).Aspect), Is.LessThan(1.5),
                        "a tall eucalyptus keeps only its trunk clear");
            for (int s = 0; s < FloraCatalog.Count; s++)
            {
                var sp = (TreeSpecies)s;
                if (!FloraCatalog.IsTree(sp)) continue;
                FloraInfo info = FloraCatalog.Info(sp);
                foreach (float h in new[] { info.MinHeightM, info.MaxHeightM })
                {
                    float w = h * info.Aspect;
                    Assert.That(PlacementContext.RoadMarginFor(sp, h, w), Is.GreaterThanOrEqualTo(Math.Max(1.2, FloraReach.LowReachM(sp, h, w) + 0.3) - 1e-6));
                    Assert.That(PlacementContext.RoadMarginFor(sp, h, w), Is.LessThan(0.7 * w + 0.31), sp + ": about the crown at most (crowns lean and arch off the trunk)");
                }
            }
        }

        [Test]
        public void ParkPathHedgesStayOutsideTheFootwayCorridor()
        {
            // A 120 × 80 m park with a footway through it and a crossing path; the corridor keeps 4.8 m clear round both.
            TileData t = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300);
            int[] v = { 30000, 30000, 42000, 30000, 42000, 38000, 30000, 38000 };
            t.Areas.Add(new AreaRecord { OsmRef = 77, Kind = AreaKind.Park, Vertices = v, Indices = new[] { 0, 1, 2, 0, 2, 3 } });
            // Way ids chosen so both paths are lined (two in three are).
            int lined = 0;
            for (ulong id = 1; lined < 2 && id < 100; id++)
            {
                var rr = new FloraRng((uint)(id ^ (id >> 32)), 0x5041524Bu + 2);
                if (!rr.Chance(0.67f)) continue;
                int[] pts = lined == 0 ? new[] { 29000, 34000, 43000, 34000 } : new[] { 36000, 29000, 36000, 39000 };
                t.Roads.Add(new RoadRecord { OsmWayId = id, RoadClass = RoadClass.Footway, Points = pts });
                lined++;
            }
            var corridor = new WaysCorridor(t);
            var trees = new List<TreeInstance>();
            TreePlacement.Place(t, new TileHeightSampler(t, 1), new TreePlacementOptions(), corridor, trees);
            List<TreeInstance> hedges = trees.Where(tr => tr.Species == TreeSpecies.Hedge && tr.Origin == TreeOrigin.Park).ToList();
            int alongPaths = hedges.Count(h => Math.Abs(h.Z - 340) < 4.5 || Math.Abs(h.X - 360) < 4.5);
            Assert.That(alongPaths, Is.GreaterThan(30), "the walks are lined with hedges");
            foreach (TreeInstance h in hedges)
            {
                // The hedge's 3 × 0.9 m footprint (mesh X along the yaw) stays 0.3 m outside every corridor.
                double a = h.YawDeg * Math.PI / 180, ux = Math.Cos(a), uz = -Math.Sin(a);
                for (int i = -1; i <= 1; i++)
                    for (int j = -1; j <= 1; j++)
                    {
                        double px = h.X + ux * 1.5 * i - uz * 0.45 * j, pz = h.Z + uz * 1.5 * i + ux * 0.45 * j;
                        Assert.That(corridor.SignedDistance(px, pz), Is.GreaterThanOrEqualTo(0.3 - 1e-3), "hedge at " + h.X + ", " + h.Z);
                    }
            }
            // Without a corridor query the same clearance comes from the ways' widths.
            var plain = new List<TreeInstance>();
            TreePlacement.Place(t, new TileHeightSampler(t, 1), new TreePlacementOptions(), plain);
            foreach (TreeInstance h in plain.Where(tr => tr.Species == TreeSpecies.Hedge && tr.Origin == TreeOrigin.Park))
                Assert.That(corridor.SignedDistance(h.X, h.Z), Is.GreaterThanOrEqualTo(0.45 + 0.3 - 1e-3), "hedge centre at " + h.X + ", " + h.Z);
            Assert.That(plain.Count(tr => tr.Species == TreeSpecies.Hedge && tr.Origin == TreeOrigin.Park && (Math.Abs(tr.Z - 340) < 4.5 || Math.Abs(tr.X - 360) < 4.5)),
                        Is.GreaterThan(30));
        }

        [Test]
        public void ForestsCloseTheirCanopy()
        {
            // The sample pack's most forested tile (the Mrigasthali and Shleshmantak woods by Pashupati) and synthetic
            // forest hills.
            CanopyCheck(StreamingSampleRegion.Tile(new TileId(10, 520, 161)), 0.88);
            // A north face (Schima-Castanopsis: a closed broadleaf canopy) and a south face (60 % chir pine: the more
            // open pine stands of Nagarjun and Chandragiri).
            TileData hill = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1700 - 0.2 * (z - Leaf.Z0), 129, Biome.HillForest);
            CanopyCheck(hill, 0.9);
            CanopyCheck(MeshingChecks.SyntheticTile(Leaf, (x, z) => 1500 + 0.2 * (z - Leaf.Z0), 129, Biome.HillForest), 0.78);
            // The lattice is world-fixed: two neighbouring tiles meet without doubled or missing trees at their edge.
            TileId east = new TileId(10, Leaf.Tx + 1, Leaf.Ty);
            TileData hill2 = MeshingChecks.SyntheticTile(east, (x, z) => 1700 - 0.2 * (z - east.Z0), 129, Biome.HillForest);
            var a = new List<TreeInstance>();
            var b = new List<TreeInstance>();
            TreePlacement.Place(hill, new TileHeightSampler(hill, 1), new TreePlacementOptions { GroundCover = false }, a);
            TreePlacement.Place(hill2, new TileHeightSampler(hill2, 1), new TreePlacementOptions { GroundCover = false }, b);
            var west = a.Where(tr => tr.Origin == TreeOrigin.Forest && FloraCatalog.IsTree(tr.Species) && tr.X > 1004).ToList();
            var eastEdge = b.Where(tr => tr.Origin == TreeOrigin.Forest && FloraCatalog.IsTree(tr.Species) && tr.X < 20).ToList();
            Assert.That(west.Count, Is.GreaterThan(50));
            Assert.That(eastEdge.Count, Is.GreaterThan(50));
            foreach (TreeInstance w in west)
            {
                double nearest = eastEdge.Min(e => Math.Sqrt((e.X + 1024 - w.X) * (e.X + 1024 - w.X) + (e.Z - w.Z) * (e.Z - w.Z)));
                Assert.That(nearest, Is.GreaterThan(2.0), "no doubled tree across the tile edge");
            }
        }

        /// <summary>Forest trees per hectare and the share of the free forest ground (not road, building or water)
        /// under at least one forest crown, at 1 m.</summary>
        private static void CanopyCheck(TileData t, double minCover)
        {
            var trees = new List<TreeInstance>();
            TreePlacement.Place(t, new TileHeightSampler(t, 1), new TreePlacementOptions(), trees);
            var mask = new PlacementMask(t);
            int n = (int)t.Tile.Size;
            var cov = new bool[n * n];
            var forest = trees.Where(tr => tr.Origin == TreeOrigin.Forest && FloraCatalog.IsTree(tr.Species)).ToList();
            foreach (TreeInstance tr in forest)
            {
                double r = 0.5 * tr.CrownM;
                for (int j = Math.Max(0, (int)(tr.Z - r)); j <= Math.Min(n - 1, (int)(tr.Z + r)); j++)
                    for (int i = Math.Max(0, (int)(tr.X - r)); i <= Math.Min(n - 1, (int)(tr.X + r)); i++)
                        if ((i + 0.5 - tr.X) * (i + 0.5 - tr.X) + (j + 0.5 - tr.Z) * (j + 0.5 - tr.Z) <= r * r) cov[j * n + i] = true;
            }
            int cells = 0, covered = 0, forestCells = 0;
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    byte m = mask.At(i + 0.5, j + 0.5);
                    if ((m & PlacementMask.Forest) == 0) continue;
                    forestCells++;
                    if (!mask.Free(i + 0.5, j + 0.5)) continue;
                    cells++;
                    if (cov[j * n + i]) covered++;
                }
            double ha = forestCells / 10000.0, perHa = forest.Count / ha, cover = covered / (double)cells;
            TestContext.WriteLine(t.Tile + ": " + forest.Count + " forest trees on " + ha.ToString("0.0") + " ha (" + perHa.ToString("0") + "/ha), canopy cover " + cover.ToString("0.00"));
            Assert.That(perHa, Is.InRange(55.0, 130.0), "canopy trees per hectare");
            Assert.That(cover, Is.GreaterThanOrEqualTo(minCover), "a closed canopy");
        }

        [Test]
        public void NoStrawStacksOnTundikhelOrOtherNonFarmLand()
        {
            // Tundikhel, the parade ground in central Kathmandu: the biome raster calls it valley cropland, but its
            // military and meadow polygons are no paddy.
            TileData t = StreamingSampleRegion.Tile(new TileId(10, 517, 161));
            var trees = new List<TreeInstance>();
            TreePlacement.Place(t, new TileHeightSampler(t, 1), new TreePlacementOptions(), trees);
            CropLand crop = CropLand.For(t);
            int stacks = 0;
            foreach (TreeInstance tr in trees)
            {
                if (tr.Species != TreeSpecies.StrawStack) continue;
                stacks++;
                Assert.That(crop.Excluded(tr.X, tr.Z), Is.False, "a straw stack on non-farm land at " + tr.X + ", " + tr.Z);
                double lon, lat;
                WorldFrame.GameToLonLat(t.Tile.X0 + tr.X, t.Tile.Z0 + tr.Z, out lon, out lat);
                Assert.That(lon > 85.3140 && lon < 85.3180 && lat > 27.7015 && lat < 27.7045, Is.False, "a straw stack on Tundikhel");
            }
            Assert.That(t.Areas.Any(a => a.Kind == AreaKind.Military || a.Kind == AreaKind.Meadow), Is.True, "Tundikhel is mapped in the sample");
            // Its centre: cropland in the biome raster (what used to draw paddy there), not farmed.
            double gx, gz;
            WorldFrame.LonLatToGame(85.3160, 27.7030, out gx, out gz);
            double lx = gx - t.Tile.X0, lz = gz - t.Tile.Z0;
            Biome b = t.Biomes[(int)Math.Round(lz / t.Tile.Size * (t.BiomesN - 1)) * t.BiomesN + (int)Math.Round(lx / t.Tile.Size * (t.BiomesN - 1))];
            Assert.That(FieldPattern.IsCrop(b), Is.True, "the raster calls Tundikhel cropland");
            Assert.That(crop.IsCrop(b, lx, lz), Is.False, "but it is no field");
            Assert.That(new PlacementMask(t).At(lx, lz) & PlacementMask.Field, Is.EqualTo(0));
            TestContext.WriteLine("straw stacks on the tile: " + stacks);
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
