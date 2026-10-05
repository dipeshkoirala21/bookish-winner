using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using Ghumante.Core.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    public class MeshingTerrainTests
    {
        private static readonly TileId Leaf = new TileId(10, 516, 161);

        private static MeshData Terrain(TileData src, TileId area, int step, float skirt = 0f)
        {
            var m = new MeshData();
            TerrainMesher.Build(src, area, new TerrainOptions { Step = step, SkirtDepthM = skirt }, m);
            return m;
        }

        [Test]
        public void GridLayoutDiagonalAndWinding()
        {
            TileData t = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300 + 0.05 * (x - Leaf.X0) + 0.02 * (z - Leaf.Z0));
            MeshData m = Terrain(t, Leaf, 4);
            int q = 32, side = 33;
            Assert.That(m.VertexCount, Is.EqualTo(side * side));
            Assert.That(m.TriangleCount, Is.EqualTo(q * q * 2));
            MeshingChecks.AssertWellFormed(m, "plane");
            Assert.That(MeshingChecks.AssertFrontFacesAgreeWithNormals(m, 0, m.TriangleCount, 0.99, "plane"), Is.EqualTo(0));
            // Vertex (k, l) at (l·32 m, k·32 m), south-west first.
            Assert.That(m.Positions[0], Is.EqualTo(0f));
            Assert.That(m.Positions[2], Is.EqualTo(0f));
            int last = side * side - 1;
            Assert.That(m.Positions[3 * last], Is.EqualTo(1024f));
            Assert.That(m.Positions[3 * last + 2], Is.EqualTo(1024f));
            Assert.That(m.Positions[3 * 1], Is.EqualTo(32f));
            Assert.That(m.Positions[3 * side + 2], Is.EqualTo(32f));
            // Every quad is split along (i,j)-(i+1,j+1): both triangles of quad 0 use vertices 0 and side + 1.
            for (int tri = 0; tri < 2; tri++)
            {
                var idx = new List<int> { m.Indices[3 * tri], m.Indices[3 * tri + 1], m.Indices[3 * tri + 2] };
                Assert.That(idx, Does.Contain(0));
                Assert.That(idx, Does.Contain(side + 1));
            }
            // Plane normal (-0.05, 1, -0.02) normalised (interior vertices are exact central differences).
            int mid = 16 * side + 16;
            double len = Math.Sqrt(0.05 * 0.05 + 1 + 0.02 * 0.02);
            Assert.That(m.Normals[3 * mid], Is.EqualTo(-0.05 / len).Within(4e-3));
            Assert.That(m.Normals[3 * mid + 1], Is.EqualTo(1 / len).Within(4e-3));
            Assert.That(m.Normals[3 * mid + 2], Is.EqualTo(-0.02 / len).Within(4e-3));
            // Colours come from the biome palette (flat ground keeps the flat colour).
            uint c = BiomePalette.Rgba(Biome.UrbanDense);
            Assert.That(m.Colors[4 * mid], Is.EqualTo((byte)(c >> 24)));
            Assert.That(m.Colors[4 * mid + 1], Is.EqualTo((byte)(c >> 16)));
        }

        [Test]
        public void SkirtsHangOutwardOnAllFourEdges()
        {
            TileData t = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300 + 20 * Math.Sin((x - Leaf.X0) / 150) + 10 * Math.Cos((z - Leaf.Z0) / 90));
            MeshData m = Terrain(t, Leaf, 8, 12f);
            int q = 16, side = 17, grid = side * side, gridTris = q * q * 2;
            Assert.That(m.VertexCount, Is.EqualTo(grid + 4 * side));
            Assert.That(m.TriangleCount, Is.EqualTo(gridTris + 4 * q * 2));
            MeshingChecks.AssertWellFormed(m, "skirts");
            for (int v = grid; v < m.VertexCount; v++)
            {
                // Each skirt vertex sits 12 m below a grid edge vertex at the same x, z.
                bool found = false;
                for (int g = 0; g < grid && !found; g++)
                    found = m.Positions[3 * g] == m.Positions[3 * v] && m.Positions[3 * g + 2] == m.Positions[3 * v + 2] &&
                            Math.Abs(m.Positions[3 * g + 1] - 12f - m.Positions[3 * v + 1]) < 1e-3;
                Assert.That(found, Is.True, "skirt vertex " + v);
            }
            for (int tri = gridTris; tri < m.TriangleCount; tri++)
            {
                double nx, ny, nz;
                MeshingChecks.Facet(m, tri, out nx, out ny, out nz);
                int a = m.Indices[3 * tri];
                double x = m.Positions[3 * a], z = m.Positions[3 * a + 2];
                // Outward: the facet points away from the tile centre (512, 512) and is vertical.
                double ox = 0, oz = 0;
                if (Math.Abs(nx) > Math.Abs(nz)) ox = x - 512;
                else oz = z - 512;
                Assert.That(nx * ox + nz * oz, Is.GreaterThan(0), "skirt triangle " + tri);
                Assert.That(Math.Abs(ny), Is.LessThan(1e-6 * Math.Sqrt(nx * nx + nz * nz) + 1e-9));
            }
            // Grid triangles face up.
            for (int tri = 0; tri < gridTris; tri++)
            {
                double nx, ny, nz;
                MeshingChecks.Facet(m, tri, out nx, out ny, out nz);
                Assert.That(ny, Is.GreaterThan(0));
            }
            Assert.That(TerrainOptions.AutoSkirtDepthM(8), Is.EqualTo(16f));
            Assert.That(TerrainOptions.AutoSkirtDepthM(1), Is.EqualTo(4f));
            Assert.That(TerrainOptions.AutoSkirtDepthM(1000), Is.EqualTo(600f));
        }

        [Test]
        public void StepIsRoundedAndClampedToTheCrop()
        {
            TileData t = StreamingSampleRegion.Tile(StreamingSampleRegion.TilesAt(9)[0]);
            TileId child = TileArea.Child(t.Tile, 2);
            Assert.That(TerrainGrid.For(t, t.Tile, 3).Step, Is.EqualTo(2), "rounded down to a power of two");
            Assert.That(TerrainGrid.For(t, t.Tile, 0).Step, Is.EqualTo(1));
            Assert.That(TerrainGrid.For(t, t.Tile, 1000).Step, Is.EqualTo(128));
            TerrainGrid g = TerrainGrid.For(t, child, 128);
            Assert.That(g.Step, Is.EqualTo(64), "clamped to the 64 source quads of a child");
            Assert.That(g.Quads, Is.EqualTo(1));
            Assert.That(g.I0, Is.EqualTo(0));
            Assert.That(g.J0, Is.EqualTo(64));
            var deepest = new TileId(16, child.Tx << 6, child.Ty << 6);
            TerrainGrid one = TerrainGrid.For(t, deepest, 1);
            Assert.That(one.SubSample, Is.False, "7 levels below a 129-sample source is exactly one source quad");
            Assert.That(one.Quads, Is.EqualTo(1));
            TileData l5 = StreamingSampleRegion.Tile(StreamingSampleRegion.TilesAt(5)[0]);
            TerrainGrid sub = TerrainGrid.For(l5, new TileId(13, l5.Tile.Tx << 8, l5.Tile.Ty << 8), 1);
            Assert.That(sub.SubSample, Is.True, "8 levels below: smaller than one source quad");
            Assert.That(sub.Quads, Is.EqualTo(1));
            Assert.Throws<ArgumentException>(() => TerrainGrid.For(t, new TileId(10, 0, 0), 1));
            Assert.Throws<ArgumentException>(() => TerrainGrid.For(new TileData { Tile = t.Tile }, t.Tile, 1));
        }

        /// <summary>The sampler returns exactly the rendered triangle's height (barycentric, within 1 mm) for
        /// whole tiles and cropped areas at several steps, and the whole-tile sampler agrees with the area sampler.</summary>
        [Test]
        public void SamplerMatchesTheRenderedTriangles()
        {
            var rng = new Random(42);
            var cases = new List<KeyValuePair<TileId, TileId>>();
            foreach (int level in new[] { 5, 6, 8, 9, 10 })
            {
                List<TileId> tiles = StreamingSampleRegion.TilesAt(level);
                TileId src = tiles[tiles.Count / 2];
                cases.Add(new KeyValuePair<TileId, TileId>(src, src));
                cases.Add(new KeyValuePair<TileId, TileId>(src, TileArea.Child(src, 3)));
                cases.Add(new KeyValuePair<TileId, TileId>(src, TileArea.Child(TileArea.Child(src, 1), 2)));
            }
            TileId l5 = StreamingSampleRegion.TilesAt(5)[10];
            cases.Add(new KeyValuePair<TileId, TileId>(l5, new TileId(13, (l5.Tx << 8) + 77, (l5.Ty << 8) + 130))); // sub-sample
            foreach (var c in cases)
            {
                TileData src = StreamingSampleRegion.Tile(c.Key);
                foreach (int step in new[] { 1, 2, 8 })
                {
                    TileId area = c.Value;
                    MeshData m = Terrain(src, area, step);
                    TileHeightSampler s = TileHeightSampler.ForArea(src, area, step);
                    TerrainGrid g = s.Grid;
                    var whole = new TileHeightSampler(src, step);
                    string what = c.Key + " -> " + area + " step " + step;
                    for (int k = 0; k < 400; k++)
                    {
                        double lx = rng.NextDouble() * area.Size, lz = rng.NextDouble() * area.Size;
                        if (k == 0)
                        {
                            lx = 0;
                            lz = area.Size; // a corner and the edges are covered too
                        }
                        float h;
                        Assert.That(s.TryHeight(area.X0 + lx, area.Z0 + lz, out h), Is.True, what);
                        int l = Math.Min((int)(lx / g.CellM), g.Quads - 1), kk = Math.Min((int)(lz / g.CellM), g.Quads - 1);
                        int tri = 2 * (kk * g.Quads + l);
                        double mh;
                        bool inside = MeshingChecks.TriangleHeight(m, tri, lx, lz, out mh) || MeshingChecks.TriangleHeight(m, tri + 1, lx, lz, out mh);
                        Assert.That(inside, Is.True, what + " point in its quad");
                        Assert.That(h, Is.EqualTo(mh).Within(1e-3), what);
                        if (!g.SubSample && g.Step == step)
                        {
                            float hw;
                            Assert.That(whole.TryHeight(area.X0 + lx, area.Z0 + lz, out hw), Is.True);
                            Assert.That(hw, Is.EqualTo(h).Within(1e-3), what + " whole-tile sampler");
                        }
                        float h2, nx, ny, nz;
                        Assert.That(s.TrySample(area.X0 + lx, area.Z0 + lz, out h2, out nx, out ny, out nz), Is.True);
                        Assert.That(h2, Is.EqualTo(h));
                        Assert.That(ny, Is.GreaterThan(0f));
                        Assert.That(nx * nx + ny * ny + nz * nz, Is.EqualTo(1f).Within(1e-4));
                    }
                    float outside;
                    Assert.That(s.TryHeight(area.X0 - 1, area.Z0 + 1, out outside), Is.False);
                    Assert.That(s.TryHeight(area.X0 + 1, area.Z0 + area.Size + 1, out outside), Is.False);
                    Assert.That(s.TryHeightClamped(area.X0 - 50, area.Z0 + 1, out outside), Is.True);
                }
            }
        }

        [Test]
        public void SamplerMatchesTrackBTriangleRule()
        {
            // Same diagonal as the driving surface: on the line u == v both halves agree, and off it the half is
            // chosen by fu >= fv.
            TileData t = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1300 + ((int)((x - Leaf.X0) / 8) % 2) * 3 + ((int)((z - Leaf.Z0) / 8) % 3));
            var s = new TileHeightSampler(t, 1);
            float a, b;
            Assert.That(s.TryHeight(Leaf.X0 + 8 * 10 + 6, Leaf.Z0 + 8 * 20 + 2, out a), Is.True); // fu 0.75 > fv 0.25: SE
            double h00 = t.HeightAt(20, 10), h10 = t.HeightAt(20, 11), h11 = t.HeightAt(21, 11);
            Assert.That(a, Is.EqualTo((float)(h00 + 0.75 * (h10 - h00) + 0.25 * (h11 - h10))).Within(1e-4));
            Assert.That(s.TryHeight(Leaf.X0 + 8 * 10 + 2, Leaf.Z0 + 8 * 20 + 6, out b), Is.True); // NW
            double h01 = t.HeightAt(21, 10);
            Assert.That(b, Is.EqualTo((float)(h00 + 0.25 * (h11 - h01) + 0.75 * (h01 - h00))).Within(1e-4));
        }

        [Test]
        public void AdjacentAreasShareIdenticalEdgeVertices()
        {
            // Exact neighbours at levels 10 and 9 (east and north), and cropped children of one source.
            var pairs = new List<TileId[]>();
            foreach (int level in new[] { 10, 9, 8, 6, 5 })
            {
                foreach (TileId a in StreamingSampleRegion.TilesAt(level))
                {
                    var east = new TileId(level, a.Tx + 1, a.Ty);
                    var north = new TileId(level, a.Tx, a.Ty + 1);
                    if (StreamingSampleRegion.Pack.Contains(east)) pairs.Add(new[] { a, east, a, east });
                    if (StreamingSampleRegion.Pack.Contains(north)) pairs.Add(new[] { a, north, a, north });
                }
            }
            TileId l9 = StreamingSampleRegion.TilesAt(9)[3];
            pairs.Add(new[] { l9, l9, TileArea.Child(l9, 0), TileArea.Child(l9, 1) });
            pairs.Add(new[] { l9, l9, TileArea.Child(l9, 0), TileArea.Child(l9, 2) });
            Assert.That(pairs.Count, Is.GreaterThan(150));

            int checkedEdges = 0;
            foreach (TileId[] p in pairs)
            {
                foreach (int step in new[] { 1, 4 })
                {
                    TileData sa = StreamingSampleRegion.Tile(p[0]), sb = StreamingSampleRegion.Tile(p[1]);
                    TileId aa = p[2], ab = p[3];
                    MeshData ma = Terrain(sa, aa, step), mb = Terrain(sb, ab, step);
                    int side = TerrainGrid.For(sa, aa, step).Quads + 1;
                    Assert.That(TerrainGrid.For(sb, ab, step).Quads + 1, Is.EqualTo(side));
                    bool east = ab.Tx == aa.Tx + 1;
                    for (int t = 0; t < side; t++)
                    {
                        int va = east ? t * side + side - 1 : (side - 1) * side + t; // A's east column or north row
                        int vb = east ? t * side : t;                               // B's west column or south row
                        double gxa = aa.X0 + ma.Positions[3 * va], gza = aa.Z0 + ma.Positions[3 * va + 2];
                        double gxb = ab.X0 + mb.Positions[3 * vb], gzb = ab.Z0 + mb.Positions[3 * vb + 2];
                        Assert.That(gxa, Is.EqualTo(gxb), "x " + aa + "/" + ab);
                        Assert.That(gza, Is.EqualTo(gzb), "z " + aa + "/" + ab);
                        Assert.That(ma.Positions[3 * va + 1], Is.EqualTo(mb.Positions[3 * vb + 1]), "height " + aa + "/" + ab + " step " + step);
                    }
                    checkedEdges++;
                }
            }
            TestContext.WriteLine("seams checked: " + checkedEdges);
        }

        /// <summary>
        /// Shared edge vertices of neighbouring tiles used to get different normals (and slope colours): a backward
        /// difference on one side, a forward one on the other. Meshed with their decoded edge neighbours, both
        /// tiles of every east-west and north-south pair of the real sample (levels 10, 9, 8, 6, 5; steps 1, 2 and 4)
        /// now show bit-identical normals and colours on the shared edge, and the samplers' smooth normals agree on
        /// it. Without neighbours the old mismatch (degrees on hillsides) is still measurable.
        /// </summary>
        [Test]
        public void NeighbourAwareNormalsMatchAcrossTileBorders()
        {
            Func<TileId, TileData> lookup = t => StreamingSampleRegion.Pack.Contains(t) ? StreamingSampleRegion.Tile(t) : null;
            int edges = 0;
            double worstWithout = 0;
            foreach (int level in new[] { 10, 9, 8, 6, 5 })
            {
                foreach (TileId a in StreamingSampleRegion.TilesAt(level))
                {
                    foreach (bool east in new[] { true, false })
                    {
                        if (east ? a.Tx + 1 >= 1 << level : a.Ty + 1 >= 1 << level) continue;
                        var b = east ? new TileId(level, a.Tx + 1, a.Ty) : new TileId(level, a.Tx, a.Ty + 1);
                        if (!StreamingSampleRegion.Pack.Contains(b)) continue;
                        TileData sa = StreamingSampleRegion.Tile(a), sb = StreamingSampleRegion.Tile(b);
                        TileNeighbours na = TileNeighbours.Of(a, lookup), nb = TileNeighbours.Of(b, lookup);
                        Assert.That(east ? na.East : na.North, Is.SameAs(sb));
                        foreach (int step in new[] { 1, 2, 4 })
                        {
                            var ma = new MeshData();
                            var mb = new MeshData();
                            TerrainMesher.Build(sa, a, new TerrainOptions { Step = step, SkirtDepthM = 0, Neighbours = na }, ma);
                            TerrainMesher.Build(sb, b, new TerrainOptions { Step = step, SkirtDepthM = 0, Neighbours = nb }, mb);
                            MeshData oa = Terrain(sa, a, step), ob = Terrain(sb, b, step);
                            int side = TerrainGrid.For(sa, a, step).Quads + 1;
                            TileHeightSampler ha = TileHeightSampler.ForArea(sa, a, step, na), hb = TileHeightSampler.ForArea(sb, b, step, nb);
                            for (int t = 0; t < side; t++)
                            {
                                int va = east ? t * side + side - 1 : (side - 1) * side + t;
                                int vb = east ? t * side : t;
                                for (int c = 0; c < 3; c++)
                                    Assert.That(ma.Normals[3 * va + c], Is.EqualTo(mb.Normals[3 * vb + c]), a + "/" + b + " step " + step + " normal");
                                for (int c = 0; c < 4; c++)
                                    Assert.That(ma.Colors[4 * va + c], Is.EqualTo(mb.Colors[4 * vb + c]), a + "/" + b + " step " + step + " colour");
                                double dot = oa.Normals[3 * va] * ob.Normals[3 * vb] + oa.Normals[3 * va + 1] * ob.Normals[3 * vb + 1] +
                                             oa.Normals[3 * va + 2] * ob.Normals[3 * vb + 2];
                                worstWithout = Math.Max(worstWithout, Math.Acos(Math.Min(1.0, dot)) * 180 / Math.PI);
                            }
                            // Overlays (roads, areas) read the same normals off the samplers along the border.
                            for (int t = 0; t <= 8; t++)
                            {
                                double f = (t + 0.37) / 9.0;
                                double x = east ? b.X0 : a.X0 + f * a.Size, z = east ? a.Z0 + f * a.Size : b.Z0;
                                float ax, ay, az, bx, by, bz;
                                Assert.That(ha.TrySmoothNormal(x, z, out ax, out ay, out az), Is.True);
                                Assert.That(hb.TrySmoothNormal(x, z, out bx, out by, out bz), Is.True);
                                Assert.That(ax, Is.EqualTo(bx).Within(1e-5));
                                Assert.That(ay, Is.EqualTo(by).Within(1e-5));
                                Assert.That(az, Is.EqualTo(bz).Within(1e-5));
                            }
                            edges++;
                        }
                    }
                }
            }
            TestContext.WriteLine("border edges checked: {0}; worst normal mismatch without neighbours {1:0.0} deg", edges, worstWithout);
            Assert.That(edges, Is.GreaterThan(300));
            Assert.That(worstWithout, Is.GreaterThan(5), "the one-sided fallback still differs (the old seam)");

            // Mismatched neighbours are ignored: a wrong tile in the east slot leaves the one-sided difference.
            TileId leaf = StreamingSampleRegion.TilesAt(10)[0];
            TileData src = StreamingSampleRegion.Tile(leaf);
            var wrong = new TileNeighbours(null, src, null, null);
            var m1 = new MeshData();
            TerrainMesher.Build(src, leaf, new TerrainOptions { Step = 2, SkirtDepthM = 0, Neighbours = wrong }, m1);
            MeshData m0 = Terrain(src, leaf, 2);
            Assert.That(m1.Normals, Is.EqualTo(m0.Normals));
        }

        /// <summary>Every tile of the sample meshes at its own area and at its four children (and a grandchild):
        /// no exceptions, finite values, unit normals, indices in range, heights in range, up-facing grid.</summary>
        [Test]
        public void EveryTileOfTheSampleMeshes()
        {
            var m = new MeshData();
            int meshes = 0;
            foreach (TileId id in StreamingSampleRegion.Tiles)
            {
                TileData src = StreamingSampleRegion.Tile(id);
                Assert.That(src.HeightsQ, Is.Not.Null, id.ToString());
                bool detail = id.Level >= 8;
                var areas = new List<TileId> { id };
                for (int q = 0; q < 4; q++) areas.Add(TileArea.Child(id, q));
                areas.Add(TileArea.Child(TileArea.Child(id, 1), 2));
                foreach (TileId area in areas)
                {
                    foreach (int step in new[] { 1, 4 })
                    {
                        m.Clear();
                        TerrainMesher.Build(src, area, new TerrainOptions { Step = step }, m);
                        string what = id + " -> " + area + " step " + step;
                        MeshingChecks.AssertWellFormed(m, what);
                        TerrainGrid g = TerrainGrid.For(src, area, step);
                        int side = g.Quads + 1, grid = side * side;
                        float lo = detail ? 500f : 0f, size = (float)area.Size;
                        for (int v = 0; v < grid; v++)
                        {
                            float y = m.Positions[3 * v + 1], x = m.Positions[3 * v], z = m.Positions[3 * v + 2];
                            if (y < lo || y > 9000f) Assert.Fail(what + ": height " + y);
                            if (!(m.Normals[3 * v + 1] > 0f)) Assert.Fail(what + ": normal does not point up");
                            if (x < 0 || x > size || z < 0 || z > size) Assert.Fail(what + ": vertex outside the area");
                        }
                        for (int v = grid; v < m.VertexCount; v++)
                            if (m.Positions[3 * v + 1] > 9000f || m.Positions[3 * v + 1] < lo - 600f) Assert.Fail(what + ": skirt height");
                        // Winding: every grid facet of a height field faces up. (On Himalayan cliffs at 256 m spacing
                        // a facet may be steeper than the smoothed vertex normals around it, so no dot test here.)
                        for (int tri = 0, nt = 2 * g.Quads * g.Quads; tri < nt; tri++)
                        {
                            double fx, fy, fz;
                            MeshingChecks.Facet(m, tri, out fx, out fy, out fz);
                            if (!(fy > 0)) Assert.Fail(what + ": grid triangle " + tri + " faces down");
                        }
                        meshes++;
                    }
                }
            }
            TestContext.WriteLine("terrain meshes built: " + meshes);
        }

        [Test]
        public void BiomePaletteCoversEveryBiomeAndSlopes()
        {
            foreach (Biome b in Enum.GetValues(typeof(Biome)))
            {
                foreach (Season s in Enum.GetValues(typeof(Season)))
                    Assert.That(MeshColor.A(BiomePalette.Rgba(b, s)), Is.EqualTo(255));
                float a0, a1;
                BiomePalette.SlopeBlendDeg(b, out a0, out a1);
                Assert.That(a1, Is.GreaterThan(a0));
                Assert.That(BiomePalette.Ground(b, Season.Monsoon, 1f), Is.EqualTo(BiomePalette.Rgba(b, Season.Monsoon)));
                Assert.That(BiomePalette.Ground(b, Season.Monsoon, 0f), Is.EqualTo(BiomePalette.RockRgba(b)));
            }
            Assert.That(BiomePalette.Rgba(Biome.None), Is.EqualTo(BiomePalette.Rgba(Biome.HillGrassland)));
            Assert.That(BiomePalette.Rgba((Biome)200), Is.EqualTo(BiomePalette.Rgba(Biome.HillGrassland)));
            Assert.That(BiomePalette.Rgba(Biome.UrbanDense), Is.EqualTo(MeshColor.FromHex(0xC9B9A0)));
            Assert.That(BiomePalette.Rgba(Biome.ValleyCropland, Season.Autumn), Is.EqualTo(MeshColor.FromHex(0xE2C04C)));
            Assert.That(BiomePalette.Rgba(Biome.ValleyCropland), Is.EqualTo(MeshColor.FromHex(0x7FD457)));
        }

        [Test]
        public void MeshDataGrowsAndClears()
        {
            var m = new MeshData(4, 6);
            for (int i = 0; i < 100; i++) m.AddVertex(i, 2 * i, 3 * i, 0, 1, 0, 0x11223344);
            m.AddTriangle(0, 1, 2);
            Assert.That(m.VertexCount, Is.EqualTo(100));
            Assert.That(m.HasUv0, Is.False);
            Assert.That(m.Colors[4 * 7], Is.EqualTo(0x11));
            Assert.That(m.Colors[4 * 7 + 3], Is.EqualTo(0x44));
            m.AddVertex(1, 1, 1, 0, 1, 0, 0xFFFFFFFF, 0.5f, 0.25f);
            Assert.That(m.HasUv0, Is.True);
            Assert.That(m.Uv0[2 * 100], Is.EqualTo(0.5f));
            Assert.That(m.Uv0[2 * 5], Is.EqualTo(0f));
            float x0, y0, z0, x1, y1, z1;
            m.GetBounds(out x0, out y0, out z0, out x1, out y1, out z1);
            Assert.That(x1, Is.EqualTo(99f));
            Assert.That(y1, Is.EqualTo(198f));
            int cap = m.VertexCapacity;
            m.Clear();
            Assert.That(m.VertexCount, Is.EqualTo(0));
            Assert.That(m.IndexCount, Is.EqualTo(0));
            Assert.That(m.HasUv0, Is.False);
            Assert.That(m.VertexCapacity, Is.EqualTo(cap));
        }
    }
}
