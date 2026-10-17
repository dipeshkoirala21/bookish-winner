using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Ghumante.Core.Data;
using Ghumante.Core.Generators;
using Ghumante.Core.Generators.Sacred;
using Ghumante.Core.Generators.Sacred.Heroes.KdsPalace;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.Core.Generators.Flora;
using Ghumante.Core.Generators.Placement;
using Ghumante.Core.Meshing.Roads;
using Ghumante.Core.Streaming;
using Ghumante.World.Buildings;
using Ghumante.World.Instancing;
using Ghumante.World.Sacred;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The bespoke Hanuman Dhoka palace site (Heroes/KdsPalace; docs/research/heritage/kds-palace.md): every structure at
    /// its true place, budgets and LOD ordering, walkable plinths, stairs and courts, the generic boxes hidden, one level
    /// under the palace, and the streamed world (TileBuild, as the game builds it) drawing the bespoke models.
    /// <see cref="DumpKdsPalace"/> (GHUMANTE_PREVIEW_DIR) writes the in-situ scene and each part at LOD0-2 for the
    /// preview tool.
    /// </summary>
    public class GeneratorsSacredKdsPalaceTests
    {
        private static readonly TileId SiteTile = new TileId(10, 516, 161);

        private static TileData Tile()
        {
            TileData t = StreamingSampleRegion.Tile(SiteTile);
            Assume.That(t, Is.Not.Null, "the sample pack holds the Durbar Square tile");
            return t;
        }

        /// <summary>One part at a LOD (a fresh mesh).</summary>
        private static MeshData Part(KdsPalacePart part, int lod, GenColliders c = null, SacredStats stats = null)
        {
            var m = new MeshData();
            KdsPalaceSite.Build(part, Tile(), lod, m, c, stats);
            return m;
        }

        // =============================================================================================================
        // Previews
        // =============================================================================================================

        /// <summary>
        /// The site as the streamer builds it (tier 2, October): the terrain, roads, areas, the heroes at LOD0 and the house
        /// bands round the palace, cropped to the site, plus every part at LOD0, LOD1 and LOD2 on its own.
        /// </summary>
        [Test]
        public void DumpKdsPalace()
        {
            if (!ObjDump.Enabled) Assert.Pass("set " + ObjDump.EnvVar + " to dump the palace");
            MeshingSettings s = StreamingScene.Settings(2, 10);
            TileBuild b = StreamingScene.Build(SiteTile, s);
            Assert.That(b.Error, Is.Null);
            double ox, oz;
            WorldFrame.LonLatToGame(KdsPalaceSite.OriginLon, KdsPalaceSite.OriginLat, out ox, out oz);
            float cx = (float)(ox - SiteTile.X0), cz = (float)(oz - SiteTile.Z0);
            float x0 = cx - 230, x1 = cx + 190, z0 = cz - 200, z1 = cz + 190;
            var directives = new[] { "kds-palace site: tile " + SiteTile + " tier 2", "meshpreview: grid=0 sun=160,62" };
            void Put(string layer, MeshData m)
            {
                MeshData cropped = ObjDump.Crop(m, x0, z0, x1, z1, true);
                if (cropped.TriangleCount > 0) ObjDump.Write(cropped, "site/" + layer + ".obj", directives);
            }
            Put("terrain", StreamingScene.Layer(b, TileLayers.Terrain));
            Put("roads", StreamingScene.Layer(b, TileLayers.Roads));
            Put("decals", StreamingScene.Layer(b, TileLayers.RoadDecals));
            Put("areas", StreamingScene.Layer(b, TileLayers.Areas));
            Put("heroes", StreamingScene.Layer(b, TileLayers.Heroes, p => p % 4 == 0));
            Put("heroes_lod1", StreamingScene.Layer(b, TileLayers.Heroes, p => p % 4 == 1));
            Put("heroes_lod2", StreamingScene.Layer(b, TileLayers.Heroes, p => p % 4 == 2));
            var near = new MeshData();
            BuildingMesher.Build(b.Source, b.Sampler, BuildingBandTable.LiteOptions(2, s.DetailBuildings), near);
            Put("buildings_b0", near);
            Put("ornaments", StreamingScene.Pieces(b, TileLayers.Ornaments, 2, cx, cz));
            var trees = new MeshData();
            trees.HasUv0 = true;
            var unit = new MeshData();
            foreach (TreeInstance t in b.Instances.Trees)
            {
                if (t.X < x0 || t.X > x1 || t.Z < z0 || t.Z > z1) continue;
                unit.Clear();
                FloraMesher.Build(t.Species, 1, 10, unit);
                FloraObj.Append(trees, unit, t.X, t.Y - 0.1f, t.Z, t.YawDeg, t.CrownM, t.HeightM, t.CrownM);
            }
            Put("trees", trees);
            var report = new StringBuilder();
            report.AppendFormat(CultureInfo.InvariantCulture, "origin tile-local {0:0.00},{1:0.00}\n", cx, cz);
            foreach (HeroPiece p in b.Heroes)
                report.AppendFormat(CultureInfo.InvariantCulture, "{0,-40} {1,6} {2,6} {3,6} {4,4}  top {5:0.0} r {6:0.0}\n", p.Id, p.Tris0, p.Tris1, p.Tris2, p.Tris3,
                                    p.TopY - p.GroundY, p.RadiusM);
            for (int i = 0; i < KdsPalaceSite.PartCount; i++)
            {
                var part = (KdsPalacePart)i;
                string id = KdsPalaceSite.IdOf(part).Replace("her.ktm.", "").Replace('.', '_');
                for (int lod = 0; lod <= 3; lod++)
                {
                    MeshData m = Part(part, lod);
                    ObjDump.Write(m, "parts/" + id + "_lod" + lod + ".obj", new[] { "meshpreview: views=4" });
                }
            }
            File.WriteAllText(Path.Combine(ObjDump.Dir, "site", "report.txt"), report.ToString());
            TestContext.WriteLine(report.ToString());
        }
    }
}
