using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using Ghumante.Core.Streaming;
using Ghumante.World.Buildings;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Tests.EditMode
{
    /// <summary>
    /// W2 building bands (W2_DESIGN 2.4): band radii per tier, the dithered cross-fade, the conservative block test,
    /// the spatial regrouping of band layers and the part-aware upload chunks, and the band layers of a real dense tile.
    /// Engine-free.
    /// </summary>
    public class WorldW2BandTests
    {
        [Test]
        public void BandRadiiFollowTheDesignTablePerTier()
        {
            BandConfig low = BandConfig.ForTier(0), mid = BandConfig.ForTier(1), high = BandConfig.ForTier(2);
            Assert.AreEqual(new[] { 35f, 120f, 350f, 750f }, new[] { low.B0OuterM, low.B1OuterM, low.B2OuterM, low.B3OuterM });
            Assert.AreEqual(new[] { 60f, 200f, 500f, 1250f }, new[] { mid.B0OuterM, mid.B1OuterM, mid.B2OuterM, mid.B3OuterM });
            Assert.AreEqual(new[] { 80f, 250f, 700f, 1750f }, new[] { high.B0OuterM, high.B1OuterM, high.B2OuterM, high.B3OuterM });
            Assert.AreEqual(new[] { 24, 48, 96 }, new[] { low.CellCacheSize, mid.CellCacheSize, high.CellCacheSize });
            Assert.AreEqual(new[] { 20000, 40000, 60000 }, new[] { low.HeroBudgetTris, mid.HeroBudgetTris, high.HeroBudgetTris });
            Assert.IsFalse(low.HeroLod0Allowed, "Low never uses hero LOD0");
            Assert.IsTrue(mid.HeroLod0Allowed);

            float inner, outer;
            mid.Range(BuildingBandLayer.B1, false, out inner, out outer);
            Assert.AreEqual(60f, inner);
            Assert.AreEqual(200f, outer);
            mid.Range(BuildingBandLayer.B1, true, out inner, out outer);
            Assert.AreEqual(0f, inner, "a B1 block standing in for its B0 cells starts at the camera");
        }

        [Test]
        public void AdjacentBandsCrossFadeWithoutHolesOrDoubleCover()
        {
            BandConfig c = BandConfig.ForTier(1);
            BuildingBandLayer[] bands = { BuildingBandLayer.B0, BuildingBandLayer.B1, BuildingBandLayer.B2, BuildingBandLayer.B3 };
            for (double d = 0; d < c.B3OuterM - BandConfig.FadeM; d += 0.25)
            {
                float sum = 0f;
                foreach (BuildingBandLayer b in bands)
                {
                    float i, o;
                    c.Range(b, false, out i, out o);
                    sum += BandConfig.Opacity(d, i, o);
                }
                Assert.AreEqual(1f, sum, 1e-4f, "coverage at " + d + " m");
            }
            // The fade is 4 m wide around each edge.
            Assert.AreEqual(0.5f, BandConfig.Opacity(60, 60, 200), 1e-5f);
            Assert.AreEqual(0f, BandConfig.Opacity(57.9, 60, 200), 1e-5f);
            Assert.AreEqual(1f, BandConfig.Opacity(62.1, 60, 200), 1e-5f);
        }

        [Test]
        public void BlockTestIsConservativeAgainstTheFragmentTest()
        {
            var rng = new Random(7);
            for (int k = 0; k < 2000; k++)
            {
                double minX = rng.NextDouble() * 1000, minZ = rng.NextDouble() * 1000;
                double maxX = minX + 1 + rng.NextDouble() * 200, maxZ = minZ + 1 + rng.NextDouble() * 200;
                double cx = rng.NextDouble() * 1400 - 200, cz = rng.NextDouble() * 1400 - 200;
                float inner = (float)(rng.NextDouble() * 300), outer = inner + 20 + (float)(rng.NextDouble() * 500);
                bool touches = BandConfig.Touches(minX, minZ, maxX, maxZ, cx, cz, inner, outer);
                // Any point of the box drawn by the band shader must have its block enabled.
                for (int s = 0; s < 20 && !touches; s++)
                {
                    double x = minX + (maxX - minX) * rng.NextDouble(), z = minZ + (maxZ - minZ) * rng.NextDouble();
                    double d = Math.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));
                    Assert.AreEqual(0f, BandConfig.Opacity(d, inner, outer), 1e-6f, "a visible fragment in a disabled block");
                }
            }
        }

        [Test]
        public void ByBlocksKeepsEveryTriangleAndGroupsThemByBlock()
        {
            var src = new MeshData();
            var rng = new Random(3);
            for (int t = 0; t < 500; t++)
            {
                float x = (float)(rng.NextDouble() * 1024), z = (float)(rng.NextDouble() * 1024);
                int v = src.AddVertex(x, 1, z, 0, 1, 0, 0xFF0000FFu);
                src.AddVertex(x + 3, 1, z, 0, 1, 0, 0xFF0000FFu);
                src.AddVertex(x, 1, z + 3, 0, 1, 0, 0xFF0000FFu);
                src.AddTriangle(v, v + 2, v + 1);
                if (t % 3 == 0) src.AddTriangle(v + 1, v + 2, v); // shares vertices with the first
            }
            var dst = new MeshData();
            var parts = new List<PartRange>();
            int n = MeshParts.ByBlocks(src, 1024, 128, dst, parts, new MeshParts.Scratch());
            Assert.AreEqual(parts.Count, n);
            Assert.AreEqual(src.IndexCount, dst.IndexCount, "no triangle lost or added");
            int perSide = MeshParts.BlocksPerSide(1024, 128);
            Assert.AreEqual(8, perSide);
            int expected = 0, lastPart = -1;
            var signature = new Dictionary<string, int>();
            for (int t = 0; t < src.TriangleCount; t++) Count(signature, Sig(src, t), 1);
            foreach (PartRange p in parts)
            {
                Assert.AreEqual(expected, p.FirstIndex, "parts are consecutive");
                Assert.Greater(p.Part, lastPart, "row-major block order");
                lastPart = p.Part;
                for (int i = p.FirstIndex; i < p.FirstIndex + p.IndexCount; i += 3)
                {
                    double cx = 0, cz = 0;
                    for (int q = 0; q < 3; q++)
                    {
                        cx += dst.Positions[dst.Indices[i + q] * 3] / 3.0;
                        cz += dst.Positions[dst.Indices[i + q] * 3 + 2] / 3.0;
                    }
                    Assert.AreEqual(p.Part, MeshParts.BlockOf(cx, cz, 128, perSide));
                    Count(signature, Sig(dst, i / 3), -1);
                }
                expected += p.IndexCount;
            }
            foreach (KeyValuePair<string, int> kv in signature) Assert.AreEqual(0, kv.Value, "triangle " + kv.Key);

            // Part-aware chunks never span two parts.
            var chunks = new List<UploadChunk>();
            ushort[] s = null;
            TileBuild.SplitLayer(dst, TileLayers.Buildings, parts, 16, chunks, ref s);
            int covered = 0;
            foreach (UploadChunk c in chunks)
            {
                PartRange owner = parts.Find(p => p.Part == c.Part);
                Assert.GreaterOrEqual(c.FirstIndex, owner.FirstIndex);
                Assert.LessOrEqual(c.FirstIndex + c.IndexCount, owner.FirstIndex + owner.IndexCount);
                Assert.LessOrEqual(c.VertexCount, 16);
                covered += c.IndexCount;
            }
            Assert.AreEqual(dst.IndexCount, covered);
        }

        private static string Sig(MeshData m, int tri)
        {
            var s = new System.Text.StringBuilder();
            for (int q = 0; q < 3; q++)
            {
                int v = m.Indices[tri * 3 + q] * 3;
                s.Append(m.Positions[v].ToString("0.00")).Append(',').Append(m.Positions[v + 2].ToString("0.00")).Append(';');
            }
            return s.ToString();
        }

        private static void Count(Dictionary<string, int> d, string k, int delta)
        {
            int v;
            d.TryGetValue(k, out v);
            d[k] = v + delta;
        }

        [Test]
        public void DenseTileBuildsAllFourBandLayersWithinTheBuildingSlice()
        {
            var tile = new TileId(10, 516, 161);
            Assert.IsTrue(SampleRegion.Pack.Contains(tile));
            var meshing = new MeshingSettings();
            var b = new TileBuild { Node = new SelectedNode(tile, tile, true) };
            TileBuild.Execute(b, SampleRegion.Pack, StreamingConfig.ForTier(StreamingConfig.TierMid), meshing, null);
            Assert.IsTrue(b.HasLayer(TileLayers.Buildings), "B1");
            Assert.IsTrue(b.HasLayer(TileLayers.BuildingsFar), "B2");
            Assert.IsTrue(b.HasLayer(TileLayers.BuildingsBlock), "B3");
            Assert.IsTrue(b.HasLayer(TileLayers.RoadDecals), "road markings");
            int b1 = b.Layers[TileLayers.Buildings].TriangleCount, b2 = b.Layers[TileLayers.BuildingsFar].TriangleCount;
            int b3 = b.Layers[TileLayers.BuildingsBlock].TriangleCount;
            Assert.Greater(b1, b2, "B2 prisms are cheaper than B1");
            Assert.Greater(b2, b3, "B3 blocks are cheaper than B2");
            Assert.Greater(b.Parts[TileLayers.Buildings].Count, 20, "B1 split into many 128 m blocks");
            Assert.LessOrEqual(b.Parts[TileLayers.Buildings].Count, 64);
            Assert.LessOrEqual(b.Parts[TileLayers.BuildingsFar].Count, 64);
            Assert.LessOrEqual(b.Parts[TileLayers.BuildingsBlock].Count, 16);
            Assert.Greater(b.Parts[TileLayers.BuildingsBlock].Count, 4);
            foreach (UploadChunk c in b.Chunks)
                if (c.Layer == TileLayers.BuildingsBlock) Assert.That(c.Part, Is.InRange(0, 15));
        }
    

        /// <summary>
        /// V3 / §10.4 at the G1 tile (Asan): the B1 + B2 + B3 triangles of every block the camera's bands touch, on the 3 × 3
        /// detail tiles around Asan, taken at the design's 40% in-frustum share, fit the Mid and High building slices
        /// (72 k / 135 k, B0 cells on top: about 21 k / 41 k). The block test is conservative, so this is an upper bound.
        /// </summary>
        [Test]
        public void BandTrianglesAtAsanFitTheMidAndHighBuildingSlices()
        {
            double cx, cz;
            Ghumante.Core.Geo.WorldFrame.LonLatToGame(85.3122, 27.7074, out cx, out cz);
            TileId c = TileId.At(10, cx, cz);
            var builds = new List<TileBuild>();
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    var id = new TileId(10, c.Tx + dx, c.Ty + dz);
                    if (!SampleRegion.Pack.Contains(id)) continue;
                    var b = new TileBuild { Node = new SelectedNode(id, id, true) };
                    TileBuild.Execute(b, SampleRegion.Pack, StreamingConfig.ForTier(StreamingConfig.TierMid), new MeshingSettings { DrawInstances = false }, null);
                    builds.Add(b);
                }
            int[] slice = { 22000, 72000, 135000 }, b0 = { 6000, 21000, 41000 };
            for (int tier = 1; tier <= 2; tier++)
            {
                BandConfig bands = BandConfig.ForTier(tier);
                int tris = 0;
                foreach (TileBuild b in builds)
                {
                    double lx = cx - b.Node.Area.X0, lz = cz - b.Node.Area.Z0;
                    foreach (UploadChunk ch in b.Chunks)
                    {
                        BuildingBandLayer band;
                        if (ch.Layer == TileLayers.Buildings) band = BuildingBandLayer.B1;
                        else if (ch.Layer == TileLayers.BuildingsFar) band = BuildingBandLayer.B2;
                        else if (ch.Layer == TileLayers.BuildingsBlock) band = BuildingBandLayer.B3;
                        else continue;
                        float i, o;
                        bands.Range(band, false, out i, out o);
                        if (BandConfig.Touches(ch.MinX, ch.MinZ, ch.MaxX, ch.MaxZ, lx, lz, i, o)) tris += ch.IndexCount / 3;
                    }
                }
                Assert.LessOrEqual(tris * 0.4 + b0[tier], slice[tier] * 1.15, "tier " + tier + ": " + tris + " band triangles around Asan");
            }
        }
    }
}
