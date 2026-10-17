using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Ghumante.Core.Data;
using Ghumante.Core.Generators;
using Ghumante.Core.Generators.Sacred;
using Ghumante.Core.Generators.Sacred.Heroes.Boudha;
using Ghumante.Core.Meshing;
using Ghumante.World.Sacred;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The bespoke Boudhanath site (W2 monuments round, docs/research/heritage/boudha.md): built through the game's own
    /// stack (the curated record of the kathmandu_core sample, <see cref="TileBuild.Execute"/> at the High tier), it
    /// stands on its OSM footprint at the measured section, holds its LOD budgets, flattens the plaza, hides the
    /// front-row houses it rebuilds and lets the player walk the terraces. <see cref="DumpBoudhaScene"/>
    /// (GHUMANTE_PREVIEW_DIR) writes the in-situ scene for tools/mesh-preview.
    /// </summary>
    public class GeneratorsSacredBoudhaTests
    {
        /// <summary>The level-10 tile of the stupa.</summary>
        private static readonly TileId Tile = new TileId(10, 521, 163);

        private static TileBuild _high;
        private static readonly object Lock = new object();

        /// <summary>The Boudha tile as the High tier streams it (built once).</summary>
        private static TileBuild High
        {
            get
            {
                lock (Lock)
                {
                    if (_high == null)
                    {
                        MeshingSettings s = StreamingScene.Settings(2, 10);
                        _high = StreamingScene.Build(Tile, s);
                    }
                    return _high;
                }
            }
        }

        private static HeritageRecord Record()
        {
            foreach (HeritageRecord r in Curated().Heritage)
                if (r.Id == BoudhaSite.HeroId) return r;
            Assert.Fail("no " + BoudhaSite.HeroId + " in the sample's curated DB");
            return null;
        }

        private static CuratedDb _db;

        private static CuratedDb Curated()
        {
            lock (Lock)
            {
                if (_db == null)
                    _db = CuratedDb.Read(File.ReadAllBytes(Path.Combine(GoldenFiles.RepoRoot, "shared", "sample-regions", "kathmandu_core", "kathmandu_core.curated.ghcd")));
                return _db;
            }
        }

        /// <summary>A fresh decode of the tile with the plaza ground applied (as TileBuild does).</summary>
        private static TileData Source()
        {
            TileData t = StreamingSampleRegion.Tile(Tile);
            HeroBuilder.PrepareGround(t);
            return t;
        }

        [Test]
        public void TheStreamedTileBuildsTheBespokeSiteWithinItsBudgets()
        {
            TileBuild b = High;
            HeroPiece p = default(HeroPiece);
            bool found = false;
            foreach (HeroPiece h in b.Heroes)
                if (h.Id == BoudhaSite.HeroId)
                {
                    p = h;
                    found = true;
                }
            Assert.That(found, Is.True, "Boudhanath is built in its anchor tile");
            TestContext.WriteLine("Boudha tris LOD0..3: " + p.Tris0 + " " + p.Tris1 + " " + p.Tris2 + " " + p.Tris3 + ", top " + (p.TopY - p.GroundY));
            Assert.That(p.Tris0, Is.LessThanOrEqualTo(BoudhaSite.BudgetLod0));
            Assert.That(p.Tris1, Is.LessThanOrEqualTo(BoudhaSite.BudgetLod1));
            Assert.That(p.Tris2, Is.LessThanOrEqualTo(BoudhaSite.BudgetLod2));
            Assert.That(p.Tris3, Is.LessThanOrEqualTo(BoudhaSite.BudgetLod3));
            Assert.That(p.Tris0, Is.GreaterThan(60000), "LOD0 is the full bespoke site, not a template");
            Assert.That(p.Tris1, Is.LessThanOrEqualTo(0.4 * p.Tris0));
            Assert.That(p.Tris2, Is.LessThanOrEqualTo(0.12 * p.Tris0));
        }

        [Test]
        public void TheStupaStandsOnItsFootprintAtTheMeasuredSection()
        {
            TileData t = Source();
            HeritageRecord r = Record();
            var m = new MeshData();
            var stats = new SacredStats();
            int tris = HeroBuilder.Build(r, t, 0, m, new GenColliders(), stats);
            Assert.That(tris, Is.GreaterThan(0));
            double ox, oz;
            Assert.That(BoudhaGround.TryCentre(t, out ox, out oz), Is.True);
            double level = BoudhaGround.KoraLevel(t, ox, oz);
            // The finial tip 44.8 m above the kora on the stupa axis.
            float top = float.MinValue;
            float tx = 0, tz = 0;
            for (int i = 0; i < m.VertexCount; i++)
            {
                float y = m.Positions[3 * i + 1];
                if (y > top)
                {
                    top = y;
                    tx = m.Positions[3 * i];
                    tz = m.Positions[3 * i + 2];
                }
            }
            Assert.That(top - level, Is.EqualTo(BoudhaStupa.Tip).Within(0.3), "finial tip above the kora");
            Assert.That(Math.Sqrt((tx - ox) * (tx - ox) + (tz - oz) * (tz - oz)), Is.LessThan(2.0), "the tip over the T1 centre");
            Assert.That(stats.TopM, Is.EqualTo((float)BoudhaStupa.Tip).Within(0.01f));
            // The T1 outline (OSM w56688295): every vertex lies within 1.5 m of the drawn terrace edge.
            int[] ring = null;
            foreach (BuildingRecord b in t.Buildings)
                if (b.OsmRef == 2 * BoudhaSite.T1Way) ring = b.Rings[0];
            Assert.That(ring, Is.Not.Null);
            BoudhaFrame f;
            Assert.That(BoudhaSite.TryFrame(r, HeroBuilder.Merge(r), t, out f), Is.True);
            double[] pu = new double[32], pv = new double[32];
            int n = BoudhaStupa.Terrace(0, pu, pv);
            Affine3Inv(f, out double c, out double s);
            double worst = 0;
            for (int i = 0; i < ring.Length / 2; i++)
            {
                double dx = ring[2 * i] / 100.0 - f.AX, dz = ring[2 * i + 1] / 100.0 - f.AZ;
                double u = dx * c - dz * s, v = dx * s + dz * c;
                worst = Math.Max(worst, BoudhaKit.EdgeDistance(pu, pv, n, u, v));
            }
            Assert.That(worst, Is.LessThan(1.5), "T1 outline follows OSM");
        }

        /// <summary>Site-local axes of the frame: u = x cos − z sin, v = x sin + z cos for a 9° turn.</summary>
        private static void Affine3Inv(in BoudhaFrame f, out double c, out double s)
        {
            double a = BoudhaSite.AxisBearingDeg * Math.PI / 180.0;
            c = Math.Cos(a);
            s = Math.Sin(a);
        }

        [Test]
        public void ThePlazaIsOneFlatLevel()
        {
            TileData t = Source();
            double ox, oz;
            Assert.That(BoudhaGround.TryCentre(t, out ox, out oz), Is.True);
            double level = BoudhaGround.KoraLevel(t, ox, oz);
            var h = new TileHeightSampler(t);
            double lo = double.MaxValue, hi = double.MinValue;
            for (int r = 0; r <= 75; r += 5)
                for (int a = 0; a < 360; a += 10)
                {
                    double x = ox + r * Math.Sin(a * Math.PI / 180), z = oz + r * Math.Cos(a * Math.PI / 180);
                    float y;
                    Assert.That(h.TryHeight(t.Tile.X0 + x, t.Tile.Z0 + z, out y), Is.True);
                    lo = Math.Min(lo, y);
                    hi = Math.Max(hi, y);
                }
            Assert.That(hi - lo, Is.LessThan(0.2), "plaza flat within r 75 m");
            Assert.That(Math.Abs(lo - level), Is.LessThan(0.2));
            Assert.That(BoudhaGround.Apply(t), Is.EqualTo(0), "idempotent");
        }

        [Test]
        public void TheFrontRowHousesTheSiteRebuildsAreHidden()
        {
            HeroSet set = HeroSet.From(Curated());
            foreach (ulong w in BoudhaSite.HiddenWays) Assert.That(set.HiddenRefs.Contains(2 * w), Is.True, "w" + w + " hidden");
        }

        [Test]
        public void TheTerracesAndTheNorthStairAreWalkable()
        {
            TileData t = Source();
            HeritageRecord r = Record();
            var gen = new GenColliders();
            HeroBuilder.Build(r, t, 0, new MeshData(), gen);
            double ox, oz;
            BoudhaGround.TryCentre(t, out ox, out oz);
            double level = BoudhaGround.KoraLevel(t, ox, oz);
            int walk = 0;
            bool t1 = false, t2 = false, t3 = false;
            foreach (GenBox b in gen.Boxes)
            {
                if ((b.Flags & GenColliderFlags.Walkable) == 0) continue;
                walk++;
                double top = b.CY + b.HalfY - level;
                if (Math.Abs(top - BoudhaStupa.T1Top) < 0.05) t1 = true;
                if (Math.Abs(top - BoudhaStupa.T2Top) < 0.05) t2 = true;
                if (Math.Abs(top - BoudhaStupa.T3Top) < 0.05) t3 = true;
            }
            Assert.That(t1 && t2 && t3, Is.True, "walkable terrace tops at 4.0, 8.0, 10.5 m");
            Assert.That(gen.Ramps.Count, Is.GreaterThanOrEqualTo(4), "stairs as walkable ramps");
            Assert.That(gen.Walls.Count, Is.GreaterThan(18), "the compound wall's runs are solid");
        }

        // ------------------------------------------------------------------------------------------------- previews

        /// <summary>
        /// The in-situ scene for tools/mesh-preview (GHUMANTE_PREVIEW_DIR): the Boudha tile as the High tier streams it
        /// (terrain, roads, decals, areas, B1 buildings) plus the B0 detail houses within 260 m and the site at LOD 0-3,
        /// each a separate OBJ under <c>boudha/</c>.
        /// </summary>
        [Test]
        public void DumpBoudhaScene()
        {
            if (!ObjDump.Enabled) Assert.Pass("set " + ObjDump.EnvVar + " to dump the scene");
            MeshingSettings s = StreamingScene.Settings(2, 10);
            TileBuild b = High;
            TileData src = b.Source;
            double ox, oz;
            Assert.That(BoudhaGround.TryCentre(src, out ox, out oz), Is.True);
            double level = BoudhaGround.KoraLevel(src, ox, oz);
            float x0 = (float)(ox - 260), z0 = (float)(oz - 260), x1 = (float)(ox + 260), z1 = (float)(oz + 260);
            var layers = new List<(string, MeshData)>
            {
                ("terrain", ObjDump.Crop(StreamingScene.Layer(b, TileLayers.Terrain), x0, z0, x1, z1, true)),
                ("roads", ObjDump.Crop(StreamingScene.Layer(b, TileLayers.Roads), x0, z0, x1, z1, true)),
                ("decals", ObjDump.Crop(StreamingScene.Layer(b, TileLayers.RoadDecals), x0, z0, x1, z1, true)),
                ("areas", ObjDump.Crop(StreamingScene.Layer(b, TileLayers.Areas), x0, z0, x1, z1, true)),
            };
            var band = new MeshData();
            BuildingMesher.Build(src, b.Sampler, s.DetailBuildings, band);
            layers.Add(("buildings", ObjDump.Crop(band, x0, z0, x1, z1, true)));
            var far = new MeshData();
            BuildingMesher.Build(src, b.Sampler, s.Buildings, far);
            layers.Add(("buildings_b1", ObjDump.Crop(ObjDump.Crop(far, x0 - 300, z0 - 300, x1 + 300, z1 + 300, true), x0, z0, x1, z1, false)));
            HeritageRecord r = Record();
            for (int lod = 0; lod < 4; lod++)
            {
                var m = new MeshData();
                int tris = HeroBuilder.Build(r, src, lod, m, null);
                layers.Add(("hero" + lod, m));
                TestContext.WriteLine("hero LOD" + lod + ": " + tris + " triangles, " + m.VertexCount + " vertices");
            }
            CultureInfo c = CultureInfo.InvariantCulture;
            var directives = new[]
            {
                "Boudhanath site, tile " + Tile + " of kathmandu_core (High tier); tile-local metres, absolute height",
                string.Format(c, "centre (Unity x,z) {0:0.###},{1:0.###} kora level {2:0.###}", ox, oz, level),
                "meshpreview: grid=0 sun=160,62",
            };
            foreach (var (name, mesh) in layers)
            {
                if (mesh.TriangleCount == 0) continue;
                ObjDump.Write(mesh, "boudha/" + name + ".obj", directives);
            }
            File.WriteAllText(Path.Combine(ObjDump.Dir, "boudha", "centre.txt"), string.Format(c, "{0:0.###} {1:0.###} {2:0.###}\n", ox, level, oz));
        }
    }
}
