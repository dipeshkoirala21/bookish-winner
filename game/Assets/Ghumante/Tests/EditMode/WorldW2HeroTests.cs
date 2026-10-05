using System;
using System.Collections.Generic;
using System.IO;
using Ghumante.Core.Data;
using Ghumante.Core.Generators;
using Ghumante.Core.Meshing;
using Ghumante.Core.Streaming;
using Ghumante.World.Sacred;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Tests.EditMode
{
    /// <summary>
    /// Hero replicas in the streamer (W2_DESIGN 3.2-3.4): the hero LOD allocator, the hide zones from the curated DB,
    /// heroes built with their anchor tile at four LODs with colliders, G7 (no unhidden building intersects a hero
    /// footprint) and the determinism of B0 cells (V9). Engine-free; the sample region's curated DB.
    /// </summary>
    public class WorldW2HeroTests
    {
        private static CuratedDb _db;

        private static CuratedDb Db
        {
            get
            {
                if (_db == null)
                {
                    string path = SampleRegion.FilePath(".curated.ghcd");
                    if (!File.Exists(path)) Assert.Ignore("the sample region has no curated DB");
                    _db = CuratedDb.Read(File.ReadAllBytes(path));
                }
                return _db;
            }
        }

        private static HeroLodBudget.Entry Hero(float screen, int cur = -1, float changed = -10f)
        {
            return new HeroLodBudget.Entry { ScreenHeight = screen, Tris0 = 12000, Tris1 = 3000, Tris2 = 800, Tris3 = 200, CurrentLod = cur, LastChangeS = changed };
        }

        [Test]
        public void HeroLodsFollowScreenHeightAndFitTheSlice()
        {
            Assert.AreEqual(0, HeroLodBudget.TargetLod(0.5f, -1));
            Assert.AreEqual(1, HeroLodBudget.TargetLod(0.2f, -1));
            Assert.AreEqual(2, HeroLodBudget.TargetLod(0.05f, -1));
            Assert.AreEqual(3, HeroLodBudget.TargetLod(0.01f, -1));
            // Hysteresis: a hero at LOD1 keeps it down to 0.108 and needs 0.385 for LOD0.
            Assert.AreEqual(1, HeroLodBudget.TargetLod(0.11f, 1));
            Assert.AreEqual(1, HeroLodBudget.TargetLod(0.37f, 1));
            Assert.AreEqual(0, HeroLodBudget.TargetLod(0.39f, 1));

            // Basantapur-like scene on Mid: one big hero and many small ones stay within 40 k.
            var budget = new HeroLodBudget(40000, true);
            var heroes = new HeroLodBudget.Entry[14];
            heroes[0] = Hero(0.5f);
            for (int i = 1; i < 14; i++) heroes[i] = Hero(i < 7 ? 0.2f : 0.06f);
            int tris = budget.Allocate(heroes, 14, 0f);
            Assert.LessOrEqual(tris, 40000);
            Assert.AreEqual(0, heroes[0].Lod, "the biggest keeps LOD0");

            // Low never uses LOD0.
            var low = new HeroLodBudget(20000, false);
            var one = new[] { Hero(0.9f) };
            low.Allocate(one, 1, 0f);
            Assert.AreEqual(1, one[0].Lod);
        }

        [Test]
        public void AHeroChangesAtMostOneLodPerHalfSecond()
        {
            var budget = new HeroLodBudget(100000, true);
            var h = new[] { Hero(0.01f, 3, 0f) };
            h[0].ScreenHeight = 0.6f; // jumps close
            budget.Allocate(h, 1, 0.2f);
            Assert.AreEqual(3, h[0].Lod, "too soon after the last change");
            budget.Allocate(h, 1, 0.6f);
            Assert.AreEqual(2, h[0].Lod, "one step");
            budget.Allocate(h, 1, 0.8f);
            Assert.AreEqual(2, h[0].Lod);
            budget.Allocate(h, 1, 1.2f);
            Assert.AreEqual(1, h[0].Lod);
        }

        [Test]
        public void HeroSetIndexesTheCuratedHeroesAndTheirHideZones()
        {
            HeroSet set = HeroSet.From(Db);
            Assert.AreEqual(Db.Count, set.Count, "every curated hero has an anchor tile");
            foreach (HeritageRecord r in Db.Heritage)
            {
                if (r.FootprintRef != 0) Assert.IsTrue(set.HiddenRefs.Contains(r.FootprintRef), r.Id + " footprint hidden");
                foreach (ulong h in r.Hidden)
                    if (h != 0) Assert.IsTrue(set.HiddenRefs.Contains(h));
                ulong key;
                Assert.IsTrue(HeroSet.TryAnchorTile(r, out key));
                Assert.AreEqual(HeroSet.AnchorLevel, TileId.FromKey(key).Level);
            }
            // Without a DB the S1 catalogue stands in.
            Assert.Greater(HeroSet.From(null).Count, 10);
        }

        [Test]
        public void BasantapurTileBuildsItsHeroesAtFourLodsAndHidesTheirBuildings()
        {
            HeroSet set = HeroSet.From(Db);
            HeritageRecord taleju;
            Assert.IsTrue(Db.TryGetHeritage("her.ktm.taleju", out taleju));
            ulong key;
            Assert.IsTrue(HeroSet.TryAnchorTile(taleju, out key));
            TileId tile = TileId.FromKey(key);
            Assert.IsTrue(SampleRegion.Pack.Contains(tile));

            var meshing = new MeshingSettings { Heroes = set, DrawInstances = false };
            meshing.SetHiddenRefs(set.HiddenRefs);
            var b = new TileBuild { Node = new SelectedNode(tile, tile, true) };
            TileBuild.Execute(b, SampleRegion.Pack, StreamingConfig.ForTier(StreamingConfig.TierMid), meshing, null);
            Assert.AreEqual(0, b.HeroFailures);
            Assert.GreaterOrEqual(b.Heroes.Count, 8, "the Basantapur heroes");
            Assert.IsNotNull(b.HeroColliders, "plinths and stairs are walkable");
            Assert.Greater(b.HeroColliders.Count, 0);
            foreach (HeroPiece p in b.Heroes)
            {
                Assert.Greater(p.Tris0, 0, p.Id);
                Assert.GreaterOrEqual(p.Tris0, p.Tris1, p.Id + " LOD0 ≥ LOD1");
                Assert.GreaterOrEqual(p.Tris1, p.Tris2, p.Id + " LOD1 ≥ LOD2");
                Assert.LessOrEqual(p.Tris3, 400, p.Id + " LOD3 is a box");
                Assert.Greater(p.TopY - p.GroundY, 2f, p.Id);
                Assert.That(p.CX, Is.InRange(-50.0, tile.Size + 50));
            }
            // Parts are hero × 4 + lod, one run each.
            var seen = new HashSet<int>();
            foreach (PartRange r in b.Parts[TileLayers.Heroes]) Assert.IsTrue(seen.Add(r.Part));

            // G7: hidden buildings are not drawn, and no drawn building's centroid lies in a hero footprint.
            var visible = new MeshData();
            int drawnWith = BuildingMesher.Build(b.Source, b.Sampler, meshing.Buildings, visible);
            int drawnWithout = BuildingMesher.Build(b.Source, b.Sampler, new BuildingOptions(), new MeshData());
            Assert.Less(drawnWith, drawnWithout, "hero hide zones remove buildings");
            foreach (HeritageRecord r in set.InTile(key))
            {
                int fi = FindBuilding(b.Source, r.FootprintRef);
                if (fi < 0) continue;
                int[] ring = b.Source.Buildings[fi].Rings[0];
                for (int i = 0; i < b.Source.Buildings.Count; i++)
                {
                    BuildingRecord o = b.Source.Buildings[i];
                    if (i == fi || set.HiddenRefs.Contains(o.OsmRef) || (o.Flags & BuildingFlags.Part) != 0) continue;
                    double cx, cz;
                    Centroid(o.Rings[0], out cx, out cz);
                    Assert.IsFalse(Inside(ring, cx, cz), "building " + o.OsmRef + " stands inside " + r.Id);
                }
            }
        }

        [Test]
        public void DetailCellsAreDeterministic()
        {
            var tile = new TileId(10, 516, 161);
            TileData t = SampleRegion.Pack.ReadTile(tile);
            var h = new TileHeightSampler(t);
            var o = new BuildingOptions { Band = BuildingBand.B0KitLite };
            int[][] cells = BuildingDetailMesher.Cells(t);
            int per = BuildingDetailMesher.CellsPerSide(t);
            int densest = 0;
            for (int c = 1; c < cells.Length; c++)
                if (cells[c].Length > cells[densest].Length) densest = c;
            var a = new MeshData();
            var bm = new MeshData();
            var ga = new GenColliders();
            var gb = new GenColliders();
            int na = BuildingDetailMesher.BuildCell(t, h, densest % per, densest / per, o, a, ga);
            int nb = BuildingDetailMesher.BuildCell(t, h, densest % per, densest / per, o, bm, gb);
            Assert.Greater(na, 0);
            Assert.AreEqual(na, nb);
            Assert.AreEqual(a.VertexCount, bm.VertexCount);
            Assert.AreEqual(a.IndexCount, bm.IndexCount);
            for (int i = 0; i < a.VertexCount * 3; i++) Assert.AreEqual(a.Positions[i], bm.Positions[i]);
            Assert.AreEqual(ga.Boxes.Count, gb.Boxes.Count);
        }

        private static int FindBuilding(TileData t, ulong osmRef)
        {
            if (osmRef == 0) return -1;
            for (int i = 0; i < t.Buildings.Count; i++)
                if (t.Buildings[i].OsmRef == osmRef) return i;
            return -1;
        }

        private static void Centroid(int[] ring, out double x, out double z)
        {
            x = z = 0;
            int n = ring.Length / 2;
            for (int k = 0; k < n; k++)
            {
                x += ring[2 * k] / 100.0 / n;
                z += ring[2 * k + 1] / 100.0 / n;
            }
        }

        private static bool Inside(int[] ring, double x, double z)
        {
            bool inside = false;
            int n = ring.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = ring[2 * i] / 100.0, zi = ring[2 * i + 1] / 100.0, xj = ring[2 * j] / 100.0, zj = ring[2 * j + 1] / 100.0;
                if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi) inside = !inside;
            }
            return inside;
        }
    }
}
