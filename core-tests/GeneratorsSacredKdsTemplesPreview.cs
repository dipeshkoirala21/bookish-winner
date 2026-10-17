using System;
using System.Globalization;
using System.Text;
using Ghumante.Core.Data;
using Ghumante.Core.Generators;
using Ghumante.Core.Generators.Sacred;
using Ghumante.Core.Generators.Sacred.Heroes.KdsTemples;
using Ghumante.Core.Geo;
using Ghumante.Core.Meshing;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Visual self-check dumps of the bespoke Kathmandu Durbar Square temples and square (GHUMANTE_PREVIEW_DIR;
    /// tools/mesh-preview): every structure at LOD0-3 alone, turned so its door faces the OBJ front (Unity +Z), and the
    /// level-10 tile built exactly as the streamer builds it (TileBuild.Execute, High tier), recentred on the brief's origin
    /// (27.7045 N, 85.3075 E) for in-situ renders from the photo viewpoints.
    /// </summary>
    public class GeneratorsSacredKdsTemplesPreview
    {
        internal static readonly TileId SiteTile = new TileId(10, 516, 161);

        [Test]
        public void DumpStructures()
        {
            if (!ObjDump.Enabled) Assert.Pass("set " + ObjDump.EnvVar + " to dump the site");
            TileData tile = StreamingSampleRegion.Tile(SiteTile);
            string only = Environment.GetEnvironmentVariable("GHUMANTE_KDS_PART");
            var sb = new StringBuilder();
            foreach (string id in KdsTemplesSite.Ids)
            {
                if (!string.IsNullOrEmpty(only) && id.IndexOf(only, StringComparison.OrdinalIgnoreCase) < 0) continue;
                HeroRecipe recipe;
                Assert.That(HeroCatalog.TryGet(id, out recipe), Is.True, id);
                HeritageRecord r = HeroCatalog.ToRecord(recipe);
                string name = id.Substring(id.LastIndexOf('.') + 1);
                for (int lod = 0; lod <= 3; lod++)
                {
                    var m = new MeshData();
                    var c = new GenColliders();
                    var stats = new SacredStats();
                    int tris = KdsTemplesSite.Build(r, tile, lod, m, lod == 0 ? c : null, stats);
                    KdsCtx x;
                    Assert.That(KdsTemplesSite.Locate(Entry(id), tile, lod, m, null, null, out x), Is.True, id);
                    DoorFront(m, x);
                    ObjDump.Write(m, "mon-kds-temples/parts/" + name + "_lod" + lod + ".obj", new[] { "meshpreview: views=4 sun=150,55" });
                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0,-34} lod {1}: {2,7} tris top {3,5:0.0} yaw {4,6:0.0} colliders {5} boxes {6} ramps {7} cyl {8} walls\n",
                                    name, lod, tris, stats.TopM, stats.DoorYawDeg, c.Boxes.Count, c.Ramps.Count, c.Cylinders.Count, c.Walls.Count);
                }
            }
            TestContext.WriteLine(sb.ToString());
        }

        internal static KdsTemplesSite.Entry Entry(string id)
        {
            KdsTemplesSite.Entry e;
            return KdsTemplesSite.TryEntry(id, out e) ? e : null;
        }

        /// <summary>Turns a structure's tile mesh into its own frame with the door toward Unity +Z (the OBJ front), on its
        /// ground.</summary>
        internal static void DoorFront(MeshData m, KdsCtx x)
        {
            for (int v = 0; v < m.VertexCount; v++)
            {
                double u, w;
                x.Local(m.Positions[3 * v], m.Positions[3 * v + 2], out u, out w);
                double nu = m.Normals[3 * v] * x.Kf.UX + m.Normals[3 * v + 2] * x.Kf.UZ, nw = m.Normals[3 * v] * x.Kf.UZ - m.Normals[3 * v + 2] * x.Kf.UX;
                m.Positions[3 * v] = (float)-u;
                m.Positions[3 * v + 1] = (float)(m.Positions[3 * v + 1] - x.F.GroundY);
                m.Positions[3 * v + 2] = (float)w;
                m.Normals[3 * v] = (float)-nu;
                m.Normals[3 * v + 2] = (float)nw;
            }
        }

        /// <summary>The tile as streamed at the High tier (terrain, roads, areas, buildings, decals, heroes at LOD0),
        /// cropped round the square and recentred on the brief's origin at its ground (OBJ north is −Z).</summary>
        [Test]
        public void DumpInSitu()
        {
            if (!ObjDump.Enabled) Assert.Pass("set " + ObjDump.EnvVar + " to dump the site");
            MeshingSettings s = StreamingScene.Settings(2, 10);
            s.DrawInstances = false;
            TileBuild b = StreamingScene.Build(SiteTile, s);
            double gx, gz;
            WorldFrame.LonLatToGame(KdsTemplesSite.OriginLon, KdsTemplesSite.OriginLat, out gx, out gz);
            double ox = gx - b.Source.Tile.X0, oz = gz - b.Source.Tile.Z0;
            const double oy = 1301.0;
            var layers = new (string, int)[]
            {
                ("terrain", TileLayers.Terrain), ("roads", TileLayers.Roads), ("areas", TileLayers.Areas), ("buildings", TileLayers.Buildings),
                ("decals", TileLayers.RoadDecals),
            };
            foreach (var (layer, index) in layers)
            {
                MeshData m = StreamingScene.Layer(b, index);
                MeshData c = ObjDump.Crop(m, (float)(ox - 260), (float)(oz - 230), (float)(ox + 140), (float)(oz + 170), true);
                Shift(c, ox, oy, oz);
                if (c.TriangleCount > 0) ObjDump.Write(c, "mon-kds-temples/situ/" + layer + ".obj");
            }
            MeshData h = StreamingScene.Layer(b, TileLayers.Heroes, part => part % 4 == 0);
            Shift(h, ox, oy, oz);
            if (h.TriangleCount > 0) ObjDump.Write(h, "mon-kds-temples/situ/heroes.obj");
            var sb = new StringBuilder();
            int site = 0;
            foreach (HeroPiece p in b.Heroes)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, "  {0,-40} {1,7} {2,6} {3,6} {4,4}  top {5:0.0}\n", p.Id, p.Tris0, p.Tris1, p.Tris2, p.Tris3, p.TopY - oy);
                if (KdsTemplesSite.Handles(p.Id)) site += p.Tris0;
            }
            TestContext.WriteLine("tile {0}: heroes {1} ({2} tris LOD0; the KDS temples site {3})\n{4}", SiteTile, b.Heroes.Count, h.TriangleCount, site, sb);
        }

        private static void Shift(MeshData m, double x, double y, double z)
        {
            for (int v = 0; v < m.VertexCount; v++)
            {
                m.Positions[3 * v] = (float)(m.Positions[3 * v] - x);
                m.Positions[3 * v + 1] = (float)(m.Positions[3 * v + 1] - y);
                m.Positions[3 * v + 2] = (float)(m.Positions[3 * v + 2] - z);
            }
        }
    }
}
