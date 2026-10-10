using System;
using System.Collections.Generic;
using System.Linq;
using Ghumante.Core.Data;
using Ghumante.Core.Generators;
using Ghumante.Core.Generators.Sacred;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Visual self-check dumps of the sacred generators (docs/W2_DETAIL_CONTRACT.md §6): every stage-one hero at LOD 0-2
    /// in its real tile with its OSM outline and door yaw, the generic pagodas, shrines, chaityas and stupas, as OBJ under
    /// <c>GHUMANTE_PREVIEW_DIR/temples/</c>. A no-op unless that variable is set; it also checks that every dumped mesh
    /// carries the material channels and baked AO (UV0) of the detail pass.
    /// </summary>
    public class GeneratorsSacredPreview
    {
        private static HeritageRecord Locate(HeroRecipe recipe, out TileData tile)
        {
            CuratedDb db = CuratedDb.Read(System.IO.File.ReadAllBytes(GoldenFiles.RepoRoot + "/shared/sample-regions/kathmandu_core/kathmandu_core.curated.ghcd"));
            HeritageRecord rec;
            if (!db.TryGetHeritage(recipe.Id, out rec)) rec = HeroCatalog.ToRecord(recipe);
            HeroRecipe merged = HeroBuilder.Merge(rec);
            foreach (TileId id in StreamingSampleRegion.TilesAt(10))
            {
                TileData t = StreamingSampleRegion.Tile(id);
                double x, z;
                int b;
                if (!HeroBuilder.TryLocate(rec, merged, t, out x, out z, out b)) continue;
                tile = t;
                return rec;
            }
            double gx, gz;
            WorldFrame.LonLatToGame(recipe.Lon, recipe.Lat, out gx, out gz);
            double size = TileId.SizeAt(10);
            var tid = new TileId(10, (int)Math.Floor(gx / size), (int)Math.Floor(gz / size));
            tile = MeshingChecks.SyntheticTile(tid, (px, pz) => 1330);
            return rec;
        }

        [Test]
        public void DumpFigureLineUp()
        {
            // Every carved figure at the centrepiece close-up (d0), the hero (d1) and generic (d2) LOD0 and the generic
            // LOD1 (d3), with its triangles.
            for (int i = 0; i < SacredFigureGallery.Names.Length; i++)
            {
                foreach (int det in new[] { 0, 1, 2, 3 })
                {
                    var m = new MeshData();
                    int tris = SacredFigureGallery.Build(i, 1.8, Math.Min(2, det), det == 3 ? 1 : 0, m);
                    MeshingChecks.AssertWellFormed(m, SacredFigureGallery.Names[i]);
                    Assert.That(m.HasUv0, Is.True);
                    TestContext.WriteLine("{0,-14} detail {1}: {2,5} tris", SacredFigureGallery.Names[i], det, tris);
                    ObjDump.Write(m, "temples/figures/" + SacredFigureGallery.Names[i] + "_d" + det + ".obj");
                }
            }
        }

        /// <summary>
        /// Heroes in their site: the hero at LOD0, the tile's road ribbons and terrain within a radius and its OSM outline
        /// as a thin red band, so plan alignment, props and stairs can be checked against the mapped ground (top and street
        /// views). A no-op unless GHUMANTE_PREVIEW_DIR is set.
        /// </summary>
        [Test]
        public void DumpHeroSites()
        {
            if (!ObjDump.Enabled) return;
            var sites = new[]
            {
                ("her.ktm.pashupatinath", 45.0), ("her.ktm.annapurna_asan", 30.0), ("her.ktm.kasthamandap", 40.0), ("her.ktm.swayambhunath", 330.0),
                ("her.ktm.boudhanath", 110.0), ("her.ktm.taleju", 60.0), ("her.bkt.nyatapola", 40.0), ("her.ptn.krishna_mandir", 30.0),
                ("her.ktm.kumari_ghar", 40.0), ("her.ptn.golden_temple", 40.0), ("her.ptn.kumbheshwar", 40.0), ("her.bkt.golden_gate", 30.0),
            };
            foreach (var (id, radius) in sites)
            {
                HeroRecipe recipe;
                Assert.That(HeroCatalog.TryGet(id, out recipe), Is.True, id);
                TileData tile;
                HeritageRecord rec = Locate(recipe, out tile);
                HeroRecipe merged = HeroBuilder.Merge(rec);
                double x, z;
                int b;
                if (!HeroBuilder.TryLocate(rec, merged, tile, out x, out z, out b)) continue;
                var hero = new MeshData();
                HeroBuilder.Build(rec, tile, 0, hero, null);
                var h = new TileHeightSampler(tile, 2);
                var roads = new MeshData();
                RoadMesher.Build(tile, h, new RoadOptions(), roads);
                var terrain = new MeshData();
                TerrainMesher.Build(tile, new TerrainOptions { Step = 1, SkirtDepthM = 0f }, terrain);
                var outline = new MeshData();
                float ground = float.MaxValue;
                for (int v = 0; v < hero.VertexCount; v++) ground = Math.Min(ground, hero.Positions[3 * v + 1]);
                if (b >= 0)
                {
                    int[] ring = tile.Buildings[b].Rings[0];
                    int n = ring.Length / 2;
                    uint red = MeshColor.FromHex(0xE0201A);
                    for (int i = 0; i < n; i++)
                    {
                        int j = (i + 1) % n;
                        double ax = ring[2 * i] / 100.0, az = ring[2 * i + 1] / 100.0, bx = ring[2 * j] / 100.0, bz = ring[2 * j + 1] / 100.0;
                        float ya, yb;
                        if (!h.TryHeight(ax, az, out ya)) ya = ground;
                        if (!h.TryHeight(bx, bz, out yb)) yb = ya;
                        int v = outline.VertexCount;
                        outline.AddVertex((float)ax, ya + 0.05f, (float)az, 0, 1, 0, red);
                        outline.AddVertex((float)bx, yb + 0.05f, (float)bz, 0, 1, 0, red);
                        outline.AddVertex((float)bx, yb + 0.65f, (float)bz, 0, 1, 0, red);
                        outline.AddVertex((float)ax, ya + 0.65f, (float)az, 0, 1, 0, red);
                        outline.AddTriangle(v, v + 1, v + 2);
                        outline.AddTriangle(v, v + 2, v + 3);
                        outline.AddTriangle(v, v + 2, v + 1);
                        outline.AddTriangle(v, v + 3, v + 2);
                    }
                }
                float x0 = (float)(x - radius), z0 = (float)(z - radius), x1 = (float)(x + radius), z1 = (float)(z + radius);
                ObjDump.Write("temples/sites/" + id.Replace('.', '_') + ".obj", new ObjPart("hero", hero), new ObjPart("roads", ObjDump.Crop(roads, x0, z0, x1, z1, true)),
                              new ObjPart("terrain", ObjDump.Crop(terrain, x0, z0, x1, z1, true)), new ObjPart("outline", outline));
                TestContext.WriteLine("{0}: centre {1:0.0},{2:0.0} building {3}", id, x, z, b);
            }
        }

        [Test]
        public void DumpSacredPreviews()
        {
            bool dump = ObjDump.Enabled;
            foreach (HeroRecipe recipe in HeroCatalog.S1)
            {
                TileData tile;
                HeritageRecord rec = Locate(recipe, out tile);
                for (int lod = 0; lod <= (dump ? 2 : 0); lod++)
                {
                    var m = new MeshData();
                    if (HeroBuilder.Build(rec, tile, lod, m, null) == 0) continue;
                    Assert.That(m.HasUv0, Is.True, recipe.Id + " carries material channels");
                    ObjDump.Write(m, "temples/heroes/" + recipe.Id.Replace('.', '_') + "_lod" + lod + ".obj");
                }
            }
            // Generic pagodas (1, 2 and 3 tiers), shrines, a chaitya and stupas S and M.
            for (int tiers = 1; tiers <= 3; tiers++)
            {
                PagodaParams p = PagodaParams.Defaults(6f + 3f * tiers, 6f + 3f * tiers, tiers);
                p.PlinthLevels = tiers;
                var m = new MeshData();
                PagodaGenerator.Build(p, new GenFrame(0, 0, 0, 0), 0, m, null);
                Assert.That(m.HasUv0, Is.True);
                ObjDump.Write(m, "temples/generic/pagoda_" + tiers + "tier.obj");
            }
            {
                // A large generic temple keeps its LOD0 form with its detail spread out to the generic ceiling.
                PagodaParams big = PagodaParams.Defaults(24f, 24f, 2);
                big.PlinthLevels = 2;
                big.DetailScale = SacredSelector.GenericDetailScale(24f, 24f, 2);
                var m = new MeshData();
                PagodaGenerator.Build(big, new GenFrame(0, 0, 0, 0), 0, m, null);
                Assert.That(m.TriangleCount, Is.LessThanOrEqualTo(SacredSelector.MaxTris(false, 0)));
                ObjDump.Write(m, "temples/generic/pagoda_24m_2tier.obj");
            }
            foreach (ShrineForm form in new[] { ShrineForm.Niche, ShrineForm.MiniPagoda, ShrineForm.TinCanopy })
            {
                foreach (ShrineKind kind in new[] { ShrineKind.Ganesh, ShrineKind.Linga, ShrineKind.Bhairav })
                {
                    var m = new MeshData();
                    ShrineGenerator.Build(new ShrineParams { Kind = kind, Form = form, LongSideM = 3.2f, ShortSideM = 2.6f }, new GenFrame(0, 0, 0, 0), 0, m, null);
                    Assert.That(m.HasUv0, Is.True);
                    ObjDump.Write(m, "temples/generic/shrine_" + form + "_" + kind + ".obj");
                }
            }
            var ch = new MeshData();
            ChaityaGenerator.Build(ChaityaParams.Defaults(2.2f), new GenFrame(0, 0, 0, 0), 0, ch, null);
            ObjDump.Write(ch, "temples/generic/chaitya.obj");
            foreach (float size in new[] { 6f, 14f })
            {
                var m = new MeshData();
                StupaGenerator.Build(StupaParams.ForSize(size), new GenFrame(0, 0, 0, 0), 0, m, null);
                Assert.That(m.HasUv0, Is.True);
                ObjDump.Write(m, "temples/generic/stupa_" + size + "m.obj");
            }
        }
    }
}
