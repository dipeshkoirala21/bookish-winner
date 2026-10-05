using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Driving;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class DrivingGroundTests
    {
        private static double Bumpy(int i, int j)
        {
            return 1300.0 + 7.0 * Math.Sin(i * 0.37) + 5.0 * Math.Cos(j * 0.23) + ((i * 31 + j * 17) % 11) * 0.3;
        }

        [Test]
        public void GroundIsTheRenderedTriangleMesh()
        {
            // The ground must be exactly the mesh: quads split along (i,j)-(i+1,j+1), every step-th sample used.
            TileData t = DrivingData.Tile(10, 520, 160, Bumpy);
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            foreach (int step in new[] { 1, 2, 4 })
            {
                var g = new TileGroundQuery();
                g.SetStep(10, step);
                g.Add(t);
                double cell = 1024.0 / (128 / step);
                // Grid vertices of the decimated mesh are the samples themselves.
                for (int j = 0; j <= 128; j += step * 8)
                for (int i = 0; i <= 128; i += step * 8)
                {
                    float h;
                    double x = Math.Min(x0 + i * 8.0, x0 + 1023.9999), z = Math.Min(z0 + j * 8.0, z0 + 1023.9999);
                    Assert.That(g.TryTerrainHeight(x, z, out h), Is.True);
                    Assert.That(h, Is.EqualTo(t.HeightAt(j, i)).Within(2e-3), "vertex " + i + "," + j + " step " + step);
                }
                // Inside a cell: planar on each side of the SW-NE diagonal.
                int ci = 5, cj = 9;
                float h00 = t.HeightAt(cj * step, ci * step), h10 = t.HeightAt(cj * step, (ci + 1) * step);
                float h01 = t.HeightAt((cj + 1) * step, ci * step), h11 = t.HeightAt((cj + 1) * step, (ci + 1) * step);
                foreach (var f in new[] { (0.7, 0.2), (0.2, 0.7), (0.5, 0.5), (0.9, 0.05), (0.01, 0.99) })
                {
                    double fu = f.Item1, fv = f.Item2;
                    double expect = fu >= fv
                        ? h00 + fu * (h10 - h00) + fv * (h11 - h10)
                        : h00 + fv * (h01 - h00) + fu * (h11 - h01);
                    double x = x0 + (ci + fu) * cell, z = z0 + (cj + fv) * cell;
                    GroundSample s;
                    Assert.That(g.TrySample(x, z, out s), Is.True);
                    Assert.That(s.Height, Is.EqualTo(expect).Within(1e-3), "step " + step + " at " + fu + "," + fv);
                    Assert.That(s.Nx * s.Nx + s.Ny * s.Ny + s.Nz * s.Nz, Is.EqualTo(1f).Within(1e-5));
                    // The normal is the triangle's: compare with finite differences inside it.
                    const double e = 0.01;
                    float hx, hz;
                    g.TryTerrainHeight(x + e, z, out hx);
                    g.TryTerrainHeight(x, z + e, out hz);
                    if (Math.Abs(fu - fv) > 0.02)
                    {
                        Assert.That(-s.Nx / s.Ny, Is.EqualTo((hx - s.Height) / e).Within(0.02), "dh/dx");
                        Assert.That(-s.Nz / s.Ny, Is.EqualTo((hz - s.Height) / e).Within(0.02), "dh/dz");
                    }
                    // And it is the meshing track's sampler, sample for sample.
                    float hs;
                    new TileHeightSampler(t, step).TryHeight(x, z, out hs);
                    Assert.That(s.Height, Is.EqualTo(hs));
                }
            }
            // At step 2 the odd samples are not part of the mesh.
            var g2 = new TileGroundQuery();
            g2.Add(t, 2);
            float a;
            g2.TryTerrainHeight(x0 + 8.0, z0 + 16.0, out a);
            Assert.That(a, Is.EqualTo(0.5 * (t.HeightAt(2, 0) + t.HeightAt(2, 2))).Within(1e-3));
            // A tile without heights never answers.
            var none = new TileGroundQuery();
            none.Add(new TileData { Tile = t.Tile }, 1);
            GroundSample n;
            Assert.That(none.TrySample(x0 + 10, z0 + 10, out n), Is.False);
        }

        [Test]
        public void SharedEdgesAgreeBetweenNeighbours()
        {
            // Two neighbours sharing their edge samples (as the pack guarantees) give the same height there.
            Func<int, int, double> west = (i, j) => Bumpy(i, j), east = (i, j) => Bumpy(i + 128, j);
            TileData a = DrivingData.Tile(10, 520, 160, west), b = DrivingData.Tile(10, 521, 160, east);
            var g = new TileGroundQuery();
            g.Add(a);
            g.Add(b);
            var sa = new TileHeightSampler(a, 1);
            double xEdge = b.Tile.X0;
            for (int k = 0; k < 50; k++)
            {
                double z = a.Tile.Z0 + k * 20.3;
                float ha, hb;
                sa.TryHeight(xEdge, z, out ha);
                Assert.That(g.TryTerrainHeight(xEdge, z, out hb), Is.True);
                Assert.That(ha, Is.EqualTo(hb).Within(1e-3));
            }
        }

        [Test]
        public void CroppedAreasUseTheAncestorSurface()
        {
            // The streamer may draw an area from an ancestor's data (a level the pack lacks); the ground follows.
            TileData source = DrivingData.Tile(9, 260, 80, Bumpy, Biome.HillTerraces);
            source.Roads.Add(DrivingData.Road(RoadClass.Primary, Surface.Asphalt, 0, RoadFlags.None, 0, 100, 2000, 100));
            var area = new TileId(10, 521, 161); // the north-east child
            var g = new TileGroundQuery();
            g.Add(source, area, 1);
            Assert.That(g.Contains(area), Is.True);
            Assert.That(g.RoadIndexOf(area), Is.Null, "cropped areas draw no roads");
            var whole = new TileHeightSampler(source, 1);
            var rng = new Random(11);
            for (int k = 0; k < 100; k++)
            {
                double x = area.X0 + rng.NextDouble() * 1024, z = area.Z0 + rng.NextDouble() * 1024;
                GroundSample s;
                float h;
                Assert.That(g.TrySample(x, z, out s), Is.True);
                whole.TryHeight(x, z, out h);
                Assert.That(s.Height, Is.EqualTo(h).Within(1e-3), "same surface as the source at step 1");
                Assert.That(s.TileLevel, Is.EqualTo(10));
                Assert.That(s.Biome, Is.EqualTo(Biome.HillTerraces));
                Assert.That(s.OnRoad, Is.False);
            }
            GroundSample outside;
            Assert.That(g.TrySample(source.Tile.X0 + 10, source.Tile.Z0 + 10, out outside), Is.False, "only the area is loaded");
            Assert.Throws<ArgumentException>(() => g.Add(source, new TileId(10, 600, 161), 1), "area outside the source");
            // An exact source with its own index cannot be added under another area.
            Assert.Throws<ArgumentException>(() => g.Add(area, TileHeightSampler.ForArea(source, area, 1), new RoadSpatialIndex(source)));
            Assert.Throws<ArgumentException>(() => g.Add(source.Tile, TileHeightSampler.ForArea(source, area, 1), null));
        }

        [Test]
        public void FinestLoadedTileAnswers()
        {
            var g = new TileGroundQuery();
            TileData parent = DrivingData.Flat(9, 260, 80, 1300.0, Biome.HillForest);
            TileData child = DrivingData.Flat(10, 520, 160, 1310.0, Biome.UrbanDense);
            TileData coarse = DrivingData.Flat(5, 16, 5, 1200.0, Biome.HillScrub);
            g.Add(coarse);
            g.Add(parent);
            g.Add(child);
            Assert.That(g.Count, Is.EqualTo(3));
            GroundSample s;
            Assert.That(g.TrySample(child.Tile.X0 + 100, child.Tile.Z0 + 100, out s), Is.True);
            Assert.That(s.Height, Is.EqualTo(1310f).Within(0.1));
            Assert.That(s.TileLevel, Is.EqualTo(10));
            Assert.That(s.Biome, Is.EqualTo(Biome.UrbanDense));
            Assert.That(s.Surface, Is.EqualTo(SurfaceGroup.Paved));
            TileId id;
            Assert.That(g.TryFinestArea(child.Tile.X0 + 100, child.Tile.Z0 + 100, out id), Is.True);
            Assert.That(id, Is.EqualTo(child.Tile));

            // In the parent but outside the child: the parent answers.
            Assert.That(g.TrySample(child.Tile.X0 + 1500, child.Tile.Z0 + 100, out s), Is.True);
            Assert.That(s.Height, Is.EqualTo(1300f).Within(0.1));
            Assert.That(s.TileLevel, Is.EqualTo(9));
            Assert.That(s.Surface, Is.EqualTo(SurfaceGroup.Dirt), "forest floor");

            // Outside both: the coarse horizon tile.
            Assert.That(g.TrySample(parent.Tile.X0 + 5000, parent.Tile.Z0 + 100, out s), Is.True);
            Assert.That(s.TileLevel, Is.EqualTo(5));

            Assert.That(g.Remove(child.Tile), Is.True);
            Assert.That(g.Remove(child.Tile), Is.False);
            Assert.That(g.TrySample(child.Tile.X0 + 100, child.Tile.Z0 + 100, out s), Is.True);
            Assert.That(s.TileLevel, Is.EqualTo(9));
            Assert.That(s.Height, Is.EqualTo(1300f).Within(0.1));

            // Replacing a tile keeps one entry; a step change re-meshes the answer.
            g.Add(parent, 4);
            Assert.That(g.Count, Is.EqualTo(2));
            g.Clear();
            Assert.That(g.TrySample(child.Tile.X0 + 100, child.Tile.Z0 + 100, out s), Is.False);
            Assert.That(g.TrySample(-5, 10, out s), Is.False);
            Assert.That(g.TrySample(double.NaN, 10, out s), Is.False);
        }

        [Test]
        public void StepPerLevelIsUsed()
        {
            TileData t = DrivingData.Tile(10, 520, 160, Bumpy);
            var g = new TileGroundQuery();
            g.SetStep(10, 4);
            Assert.That(g.StepFor(10), Is.EqualTo(4));
            g.Add(t);
            var rng = new Random(3);
            for (int k = 0; k < 200; k++)
            {
                double x = t.Tile.X0 + rng.NextDouble() * 1024, z = t.Tile.Z0 + rng.NextDouble() * 1024;
                float expect, got;
                new TileHeightSampler(t, 4).TryHeight(x, z, out expect);
                Assert.That(g.TryTerrainHeight(x, z, out got), Is.True);
                Assert.That(got, Is.EqualTo(expect));
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => g.SetStep(10, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => g.SetStep(17, 1));
        }

        [Test]
        public void RoadsLiftTheGroundAndSetTheSurface()
        {
            TileData t = DrivingData.Flat(10, 520, 160, 1300.0, Biome.ValleyCropland);
            t.Roads.Add(DrivingData.Road(RoadClass.Residential, Surface.Brick, 0, RoadFlags.None, 100, 500, 900, 500));
            t.Roads.Add(DrivingData.Road(RoadClass.Track, Surface.Gravel, 0, RoadFlags.Oneway, 500, 600, 500, 1000));
            var g = new TileGroundQuery();
            g.Add(t);
            double x0 = t.Tile.X0, z0 = t.Tile.Z0;
            GroundSample s;

            Assert.That(g.TrySample(x0 + 300, z0 + 501, out s), Is.True);
            Assert.That(s.OnRoad, Is.True);
            Assert.That(s.RoadClass, Is.EqualTo(RoadClass.Residential));
            Assert.That(s.RoadSurface, Is.EqualTo(Surface.Brick));
            Assert.That(s.Surface, Is.EqualTo(SurfaceGroup.Paved));
            Assert.That(g.RoadLiftM(RoadClass.Residential), Is.EqualTo(0.25f + 4 * 0.008f).Within(1e-6), "the mesher's lift");
            Assert.That(s.Height, Is.EqualTo(s.TerrainHeight + g.RoadLiftM(RoadClass.Residential)).Within(1e-4));
            Assert.That(s.RoadHalfWidthM, Is.EqualTo(2.5f));
            Assert.That(s.RoadOffsetM, Is.EqualTo(-1f).Within(1e-3));
            Assert.That(s.RoadDirX, Is.EqualTo(1f).Within(1e-5));

            // In the margin the lift ramps down to the terrain (a kerb ramp, not a cliff).
            Assert.That(g.TrySample(x0 + 300, z0 + 502.75, out s), Is.True);
            Assert.That(s.OnRoad, Is.True);
            Assert.That(s.Height - s.TerrainHeight, Is.EqualTo(0.5f * g.RoadLiftM(RoadClass.Residential)).Within(1e-3));

            Assert.That(g.TrySample(x0 + 500, z0 + 800, out s), Is.True);
            Assert.That(s.Surface, Is.EqualTo(SurfaceGroup.Gravel));
            Assert.That(s.RoadFlags, Is.EqualTo(RoadFlags.Oneway));

            // Off road: the biome decides (valley cropland is dirt) and there is no lift.
            Assert.That(g.TrySample(x0 + 300, z0 + 520, out s), Is.True);
            Assert.That(s.OnRoad, Is.False);
            Assert.That(s.Surface, Is.EqualTo(SurfaceGroup.Dirt));
            Assert.That(s.Height, Is.EqualTo(s.TerrainHeight));
            Assert.That(s.RoadClass, Is.EqualTo(RoadClass.Unknown));

            // A world drawn without trails has no footpath surfaces.
            t.Roads.Add(DrivingData.Road(RoadClass.Footway, Surface.Brick, 0, RoadFlags.None, 100, 200, 900, 200));
            var noTrails = new TileGroundQuery(new RoadOptions { IncludeTrails = false });
            noTrails.Add(t);
            g.Add(t);
            Assert.That(g.TrySample(x0 + 300, z0 + 200, out s) && s.OnRoad, Is.True);
            Assert.That(noTrails.TrySample(x0 + 300, z0 + 200, out s) && s.OnRoad, Is.False);
        }

        [Test]
        public void RoadsAcrossATileEdgeCount()
        {
            // A trunk road running north-south 2 m east of the shared edge lives only in the east tile.
            TileData west = DrivingData.Flat(10, 520, 160, 1300.0), east = DrivingData.Flat(10, 521, 160, 1300.0);
            east.Roads.Add(DrivingData.Road(RoadClass.Trunk, Surface.Asphalt, 0, RoadFlags.None, 2, 0, 2, 1024));
            var g = new TileGroundQuery();
            g.Add(west);
            g.Add(east);
            GroundSample s;
            Assert.That(g.TrySample(east.Tile.X0 - 3, east.Tile.Z0 + 400, out s), Is.True);
            Assert.That(s.OnRoad, Is.True, "5 m from the centre of a 10 m road, in the neighbour tile");
            Assert.That(s.RoadClass, Is.EqualTo(RoadClass.Trunk));
            RoadHit hit;
            Assert.That(g.TryNearestRoad(east.Tile.X0 - 30, east.Tile.Z0 + 400, 30, out hit), Is.True);
            Assert.That(hit.Tile, Is.SameAs(east));
            Assert.That(g.RoadIndexOf(east.Tile), Is.Not.Null);
            Assert.That(g.RoadIndexOf(west.Tile), Is.Null);
        }

        [Test]
        public void BridgesSpanTheValley()
        {
            // A valley 20 m deep along local x 400..600; a bridge crosses it from x 300 to 700.
            TileData t = DrivingData.Tile(10, 520, 160, (i, j) =>
            {
                double x = i * 8.0;
                return x > 400 && x < 600 ? 1280.0 : 1300.0;
            }, Biome.RiverbedGravel);
            t.Roads.Add(DrivingData.Road(RoadClass.Secondary, Surface.Concrete, 0, RoadFlags.Bridge, 300, 500, 700, 500));
            var g = new TileGroundQuery();
            g.Add(t);
            GroundSample s;
            Assert.That(g.TrySample(t.Tile.X0 + 500, t.Tile.Z0 + 500, out s), Is.True);
            Assert.That(s.TerrainHeight, Is.EqualTo(1280f).Within(0.1));
            float bank = Ght.Dequantize(Ght.Quantize(1300.0));
            Assert.That(s.Height, Is.EqualTo(bank + g.RoadLiftM(RoadClass.Secondary)).Within(1e-3), "the deck, not the river bed");
            Assert.That(s.RoadFlags & RoadFlags.Bridge, Is.EqualTo(RoadFlags.Bridge));
            Assert.That(g.TrySample(t.Tile.X0 + 500, t.Tile.Z0 + 530, out s), Is.True);
            Assert.That(s.OnRoad, Is.False);
            Assert.That(s.Surface, Is.EqualTo(SurfaceGroup.Gravel), "riverbed");
        }

        /// <summary>A 20 m deep valley along local x 400..600, a bridge across it (x 300..700 at z 500) and a
        /// riverside track along the valley floor (x 500, z 300..700) passing under the bridge.</summary>
        internal static TileGroundQuery BridgeValley(out TileData t)
        {
            t = DrivingData.Tile(10, 520, 160, (i, j) =>
            {
                double x = i * 8.0;
                return x > 400 && x < 600 ? 1280.0 : 1300.0;
            }, Biome.RiverbedGravel);
            t.Roads.Add(DrivingData.Road(RoadClass.Secondary, Surface.Concrete, 0, RoadFlags.Bridge, 300, 500, 700, 500));
            t.Roads.Add(DrivingData.Road(RoadClass.Track, Surface.Dirt, 0, RoadFlags.None, 500, 300, 500, 700));
            var g = new TileGroundQuery();
            g.Add(t);
            return g;
        }

        [Test]
        public void LayeredQueryKeepsUnderBridgeUnder()
        {
            TileData t;
            TileGroundQuery g = BridgeValley(out t);
            double x = t.Tile.X0 + 500, z = t.Tile.Z0 + 500;
            float bank = Ght.Dequantize(Ght.Quantize(1300.0)), bed = Ght.Dequantize(Ght.Quantize(1280.0));
            GroundSample s;
            // Without a height hint (teleports): the deck.
            Assert.That(g.TrySample(x, z, out s), Is.True);
            Assert.That(s.RoadFlags & RoadFlags.Bridge, Is.EqualTo(RoadFlags.Bridge));
            Assert.That(s.Height, Is.EqualTo(bank + g.RoadLiftM(RoadClass.Secondary)).Within(1e-3));
            // From the deck: the deck.
            Assert.That(g.TrySample(x, z, 1300.3f, out s), Is.True);
            Assert.That(s.RoadClass, Is.EqualTo(RoadClass.Secondary));
            // From the river bed: the track under the bridge.
            Assert.That(g.TrySample(x, z, 1280.2f, out s), Is.True);
            Assert.That(s.RoadClass, Is.EqualTo(RoadClass.Track));
            Assert.That(s.Surface, Is.EqualTo(SurfaceGroup.Dirt));
            Assert.That(s.Height, Is.EqualTo(bed + g.RoadLiftM(RoadClass.Track)).Within(1e-3));
            // Beside the track on the bed: plain riverbed under the deck.
            Assert.That(g.TrySample(x + 10, z, 1280.2f, out s), Is.True);
            Assert.That(s.OnRoad, Is.False);
            Assert.That(s.Height, Is.EqualTo(bed).Within(1e-3));
        }

        [Test]
        public void BiomeSurfaces()
        {
            Assert.That(BiomeGround.Of(Biome.UrbanDense), Is.EqualTo(SurfaceGroup.Paved));
            Assert.That(BiomeGround.Of(Biome.RiverbedGravel), Is.EqualTo(SurfaceGroup.Gravel));
            Assert.That(BiomeGround.Of(Biome.ValleyCropland), Is.EqualTo(SurfaceGroup.Dirt));
            Assert.That(BiomeGround.Of(Biome.HillForest), Is.EqualTo(SurfaceGroup.Dirt));
            Assert.That(BiomeGround.Of(Biome.HillGrassland), Is.EqualTo(SurfaceGroup.Dirt));
            Assert.That(BiomeGround.Of(Biome.Glacier), Is.EqualTo(SurfaceGroup.Mud));
            Assert.That(BiomeGround.Of(Biome.Snow), Is.EqualTo(SurfaceGroup.Mud));
            Assert.That(BiomeGround.Of(Biome.Water), Is.EqualTo(SurfaceGroup.Mud));
            Assert.That(BiomeGround.Of((Biome)250), Is.EqualTo(SurfaceGroup.Dirt));
            foreach (Biome b in Enum.GetValues(typeof(Biome))) Assert.DoesNotThrow(() => BiomeGround.Of(b));
        }

        // ---- the real sample region ----

        [Test]
        public void ThamelToBoudhanathResolvesToLeafTerrain()
        {
            TileGroundQuery g = DrivingData.SampleGround();
            double ax, az, bx, bz;
            WorldFrame.LonLatToGame(85.312702, 27.716658, out ax, out az); // Thamel
            WorldFrame.LonLatToGame(85.362, 27.7215, out bx, out bz); // Boudhanath
            Assert.That(Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az)), Is.InRange(4500.0, 5300.0));
            const int samples = 400;
            int leaf = 0, valid = 0, onRoad = 0;
            float lo = float.MaxValue, hi = float.MinValue;
            for (int k = 0; k <= samples; k++)
            {
                double f = k / (double)samples, x = ax + (bx - ax) * f, z = az + (bz - az) * f;
                GroundSample s;
                Assert.That(g.TrySample(x, z, out s), Is.True, "sample " + k);
                if (s.TileLevel == 10) leaf++;
                if (s.Height >= 1250f && s.Height <= 1450f) valid++;
                if (s.OnRoad) onRoad++;
                lo = Math.Min(lo, s.Height);
                hi = Math.Max(hi, s.Height);
                Assert.That(s.Ny, Is.GreaterThan(0.5f), "valley floor slopes are gentle");
            }
            Assert.That(leaf, Is.GreaterThanOrEqualTo(samples * 9 / 10), "leaf tiles cover the line");
            Assert.That(valid, Is.GreaterThanOrEqualTo(samples * 95 / 100), "heights " + lo + ".." + hi);
            Assert.That(onRoad, Is.GreaterThan(5), "the line crosses streets");
        }

        [Test]
        public void SampleGroundOnThamelMarg()
        {
            TileGroundQuery g = DrivingData.SampleGround();
            double x, z;
            WorldFrame.LonLatToGame(85.31172094019205, 27.716693189023914, out x, out z);
            GroundSample s;
            Assert.That(g.TrySample(x, z, out s), Is.True);
            Assert.That(s.OnRoad, Is.True);
            Assert.That(s.TileLevel, Is.EqualTo(10));
            Assert.That(s.Surface, Is.EqualTo(SurfaceGroup.Paved));
            Assert.That(s.Height, Is.EqualTo(s.TerrainHeight + g.RoadLiftM(s.RoadClass)).Within(1e-3));
            Assert.That(s.Height, Is.InRange(1290f, 1360f));

            // With only the level-8 tile loaded the coarse terrain answers, within metres of the leaf terrain.
            var coarse = new TileGroundQuery();
            TileData t8 = DrivingData.SampleTiles()[TileId.At(8, x, z)];
            coarse.Add(t8);
            GroundSample c;
            Assert.That(coarse.TrySample(x, z, out c), Is.True);
            Assert.That(c.TileLevel, Is.EqualTo(8));
            Assert.That(c.OnRoad, Is.False, "level 8 carries no roads");
            Assert.That(c.Height, Is.EqualTo(s.TerrainHeight).Within(10f));
            // Adding the leaf makes it answer again.
            coarse.Add(DrivingData.SampleTiles()[TileId.At(10, x, z)]);
            Assert.That(coarse.TrySample(x, z, out c), Is.True);
            Assert.That(c.TileLevel, Is.EqualTo(10));
            Assert.That(c.Height, Is.EqualTo(s.Height));
        }
    }
}
