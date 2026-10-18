using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Ghumante.Core.Data;
using Ghumante.Core.Generators;
using Ghumante.Core.Generators.Sacred;
using Ghumante.Core.Generators.Sacred.Heroes.Pashupati;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Shapes;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// Visual self-check dumps of the bespoke Pashupatinath site (GHUMANTE_PREVIEW_DIR; tools/mesh-preview): every part at
    /// LOD0-2 alone, and the two level-10 tiles it spans built exactly as the streamer builds them (TileBuild.Execute, High
    /// tier) with the eye points of the reference photos as directives.
    /// </summary>
    public class GeneratorsSacredPashupatiPreview
    {
        /// <summary>The level-10 tiles of the site: the temple's (520, 161) and the one north of it.</summary>
        internal static readonly TileId TempleTile = new TileId(10, 520, 161), NorthTile = new TileId(10, 520, 162);

        [Test]
        public void DumpParts()
        {
            if (!ObjDump.Enabled) Assert.Pass("set " + ObjDump.EnvVar + " to dump the site");
            TileData tile = StreamingSampleRegion.Tile(TempleTile);
            string only = Environment.GetEnvironmentVariable("GHUMANTE_PASHUPATI_PART");
            for (int p = 0; p <= (int)PashupatiPart.NorthCourt; p++)
            {
                var part = (PashupatiPart)p;
                if (!string.IsNullOrEmpty(only) && !string.Equals(only, part.ToString(), StringComparison.OrdinalIgnoreCase)) continue;
                for (int lod = 0; lod <= 3; lod++)
                {
                    var m = new MeshData();
                    var c = new GenColliders();
                    int tris = PashupatiSite.Build(part, tile, lod, m, lod == 0 ? c : null, null);
                    // Re-centre on the temple centroid at the courtyard level (OBJ north is −z).
                    Recentre(m, tile);
                    ObjDump.Write(m, "mon-pashupati-temple/parts/" + part.ToString().ToLowerInvariant() + "_lod" + lod + ".obj", new[] { "meshpreview: views=4" });
                    TestContext.WriteLine("{0,-12} lod {1}: {2,7} tris, colliders {3} boxes {4} ramps {5} cylinders {6} walls", part, lod, tris, c.Boxes.Count,
                                          c.Ramps.Count, c.Cylinders.Count, c.Walls.Count);
                }
            }
        }

        /// <summary>Triangles of every structure of the site at LOD0-3 (the budget breakdown behind the part ceilings).</summary>
        [Test]
        public void StructureBreakdown()
        {
            TileData tile = StreamingSampleRegion.Tile(TempleTile);
            var builders = new (string, Action<PashupatiFrame, MeshData>)[]
            {
                ("temple", (f, m) => PashupatiTemple.Build(ref f, m, null)), ("nandi", (f, m) => PashupatiNandi.Build(ref f, m)),
                ("floor", (f, m) => PashupatiGround.CourtyardFloor(ref f, m)), ("furniture", (f, m) => PashupatiShrines.CourtyardFurniture(ref f, m)),
                ("west gate", (f, m) => PashupatiGates.West(ref f, m)), ("west buildings", (f, m) => PashupatiRing.WestBuildings(ref f, m)),
                ("trident", (f, m) => PashupatiShrines.GreatTrident(ref f, m)), ("forecourt", (f, m) => PashupatiGround.Forecourt(ref f, m)),
                ("east gate", (f, m) => PashupatiGates.East(ref f, m)), ("east wing", (f, m) => PashupatiRing.EastWing(ref f, m)),
                ("mukti", (f, m) => PashupatiRing.MuktiMandap(ref f, m)), ("se block", (f, m) => PashupatiRing.SouthEastBlock(ref f, m)),
                ("bhairav", (f, m) => PashupatiShrines.Bhairav(ref f, m)), ("octagon", (f, m) => PashupatiShrines.Octagon(ref f, m)),
                ("kings", (f, m) => PashupatiShrines.KingsPillars(ref f, m)), ("south shrines", (f, m) => PashupatiShrines.SouthShrines(ref f, m)),
                ("south range", (f, m) => PashupatiRing.SouthRange(ref f, m)), ("priests", (f, m) => PashupatiRing.PriestsQuarters(ref f, m)),
                ("vasuki", (f, m) => PashupatiShrines.Vasuki(ref f, m)), ("small shrines", (f, m) => PashupatiShrines.SmallShrines(ref f, m)),
                ("north wing", (f, m) => PashupatiRing.NorthWing(ref f, m)), ("araucarias", (f, m) => PashupatiShrines.Araucarias(ref f, m)),
            };
            foreach (var (name, build) in builders)
            {
                var t = new int[4];
                for (int lod = 0; lod <= 3; lod++)
                {
                    var m = new MeshData();
                    ShapeAo.EnsureUv(m);
                    m.HasUv0 = true;
                    PashupatiFrame f = PashupatiFrame.For(tile, lod, null);
                    build(f, m);
                    t[lod] = m.TriangleCount;
                }
                TestContext.WriteLine("{0,-16} {1,7} {2,7} {3,6} {4,5}", name, t[0], t[1], t[2], t[3]);
                Assert.That(t[0], Is.GreaterThan(0), name);
            }
        }

        /// <summary>Move a tile-local site mesh so the temple centroid on the courtyard floor is the origin.</summary>
        internal static void Recentre(MeshData m, TileData tile)
        {
            double ox = PashupatiSite.CentreX - tile.Tile.X0, oz = PashupatiSite.CentreZ - tile.Tile.Z0;
            for (int v = 0; v < m.VertexCount; v++)
            {
                m.Positions[3 * v] = (float)(m.Positions[3 * v] - ox);
                m.Positions[3 * v + 1] = (float)(m.Positions[3 * v + 1] - PashupatiSite.CourtyardLevelM);
                m.Positions[3 * v + 2] = (float)(m.Positions[3 * v + 2] - oz);
            }
        }

        /// <summary>
        /// The two tiles as streamed at the High tier (terrain, roads, areas, B1 buildings, heroes at LOD0), recentred on
        /// the temple, one OBJ per layer and per tile, for in-situ renders from the photo viewpoints.
        /// </summary>
        [Test]
        public void DumpInSitu()
        {
            if (!ObjDump.Enabled) Assert.Pass("set " + ObjDump.EnvVar + " to dump the site");
            MeshingSettings s = StreamingScene.Settings(2, 10);
            s.DrawInstances = false;
            foreach (TileId id in new[] { TempleTile, NorthTile })
            {
                TileBuild b = StreamingScene.Build(id, s);
                string name = "mon-pashupati-temple/situ/" + id.Tx + "_" + id.Ty + "_";
                var layers = new (string, int)[]
                {
                    ("terrain", TileLayers.Terrain), ("roads", TileLayers.Roads), ("areas", TileLayers.Areas), ("buildings", TileLayers.Buildings),
                    ("decals", TileLayers.RoadDecals),
                };
                double ox = PashupatiSite.CentreX - b.Source.Tile.X0, oz = PashupatiSite.CentreZ - b.Source.Tile.Z0;
                foreach (var (layer, index) in layers)
                {
                    MeshData m = StreamingScene.Layer(b, index);
                    Crop(m, ox, oz, 260);
                    Recentre(m, b.Source);
                    if (m.TriangleCount > 0) ObjDump.Write(m, name + layer + ".obj");
                }
                // Heroes at LOD0 (part = hero × 4 + lod).
                MeshData h = StreamingScene.Layer(b, TileLayers.Heroes, part => part % 4 == 0);
                Recentre(h, b.Source);
                if (h.TriangleCount > 0) ObjDump.Write(h, name + "heroes.obj");
                TestContext.WriteLine("tile {0}: heroes {1} ({2} tris LOD0)", id, b.Heroes.Count, h.TriangleCount);
                foreach (HeroPiece p in b.Heroes)
                    TestContext.WriteLine("  {0,-40} {1,7} {2,6} {3,6} {4,4}  top {5:0.0}", p.Id, p.Tris0, p.Tris1, p.Tris2, p.Tris3, p.TopY - PashupatiSite.CourtyardLevelM);
            }
        }

        /// <summary>Drop triangles whose centroid is farther than r (m) from (x, z).</summary>
        private static void Crop(MeshData m, double x, double z, double r)
        {
            MeshData c = ObjDump.Crop(m, (float)(x - r), (float)(z - r), (float)(x + r), (float)(z + r), true);
            m.Clear();
            ObjDump.Append(m, c, 0, 0, 0);
        }
    }
}
