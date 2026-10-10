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
