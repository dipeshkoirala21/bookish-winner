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
    /// <summary>W2_DESIGN 10.6 V4 (replica fidelity) and the generic sacred generators: heights, tiers, plinths, door
    /// yaws, outlines, LOD budgets, strut and bell formulas, chaitya niches, closed sanctums.</summary>
    public class GeneratorsSacredTests
    {
        private static CuratedDb _db;
        private static Dictionary<TileId, TileData> _tiles;

        private static CuratedDb Db
        {
            get { return _db ?? (_db = CuratedDb.Read(System.IO.File.ReadAllBytes(GoldenFiles.RepoRoot + "/shared/sample-regions/kathmandu_core/kathmandu_core.curated.ghcd"))); }
        }

        private static Dictionary<TileId, TileData> Tiles
        {
            get { return _tiles ?? (_tiles = StreamingSampleRegion.TilesAt(10).ToDictionary(i => i, i => StreamingSampleRegion.Tile(i))); }
        }

        /// <summary>The record of a hero (curated DB when it has one, else the catalog) and a tile holding its anchor:
        /// the sample pack's, else a flat synthetic tile at its coordinates (Patan and Bhaktapur lie outside the pack).</summary>
        private static HeritageRecord Locate(HeroRecipe recipe, out TileData tile, out double x, out double z, out int building)
        {
            HeritageRecord rec;
            if (!Db.TryGetHeritage(recipe.Id, out rec)) rec = HeroCatalog.ToRecord(recipe);
            HeroRecipe merged = HeroBuilder.Merge(rec);
            foreach (TileData t in Tiles.Values)
            {
                if (!HeroBuilder.TryLocate(rec, merged, t, out x, out z, out building)) continue;
                tile = t;
                return rec;
            }
            double gx, gz;
            WorldFrame.LonLatToGame(recipe.Lon, recipe.Lat, out gx, out gz);
            double size = TileId.SizeAt(10);
            var id = new TileId(10, (int)Math.Floor(gx / size), (int)Math.Floor(gz / size));
            tile = MeshingChecks.SyntheticTile(id, (px, pz) => 1330);
            Assert.That(HeroBuilder.TryLocate(rec, merged, tile, out x, out z, out building), Is.True, recipe.Id);
            return rec;
        }

        private static bool IsColour(MeshData m, int tri, uint c)
        {
            int v = m.Indices[3 * tri];
            return m.Colors[4 * v] == MeshColor.R(c) && m.Colors[4 * v + 1] == MeshColor.G(c) && m.Colors[4 * v + 2] == MeshColor.B(c);
        }

        private static double Bearing(double nx, double nz)
        {
            double b = Math.Atan2(nx, nz) * 180 / Math.PI;
            return b < 0 ? b + 360 : b;
        }

        private static double AngleDiff(double a, double b)
        {
            double d = Math.Abs(a - b) % 360;
            return d > 180 ? 360 - d : d;
        }

        [Test]
        public void EveryStageOneHeroMatchesItsReplicaSpec()
        {
            Assert.That(HeroCatalog.S1.Count, Is.GreaterThanOrEqualTo(30));
            var stats = new SacredStats();
            foreach (HeroRecipe recipe in HeroCatalog.S1)
            {
                TileData tile;
                double x, z;
                int building;
                HeritageRecord rec = Locate(recipe, out tile, out x, out z, out building);
                HeroRecipe merged = HeroBuilder.Merge(rec);
                string id = recipe.Id;
                for (int lod = 0; lod <= 3; lod++)
                {
                    var m = new MeshData();
                    int tris = HeroBuilder.Build(rec, tile, lod, m, lod == 0 ? new GenColliders() : null, stats);
                    Assert.That(tris, Is.GreaterThan(0), id + " lod " + lod);
                    Assert.That(tris, Is.LessThanOrEqualTo(merged.Budget(lod)), id + " lod " + lod + " budget");
                    MeshingChecks.AssertWellFormed(m, id + " lod " + lod);
                    if (lod != 0) continue;

                    // Height within ±3% (or 0.15 m for the small pieces).
                    Assert.That(stats.TopM, Is.EqualTo(merged.HeightM).Within(Math.Max(0.15, 0.03 * merged.HeightM)), id + " height");
                    // Tier and plinth counts exact where the replica states them.
                    if (merged.Tiers > 0) Assert.That(stats.Tiers, Is.EqualTo(merged.Tiers), id + " tiers");
                    if (merged.PlinthLevels > 0) Assert.That(stats.PlinthLevels, Is.EqualTo(merged.PlinthLevels), id + " plinths");
                    // Door yaw within ±2°: as built, and as drawn (a closed sanctum door faces it).
                    if (!float.IsNaN(merged.YawDeg)) Assert.That(AngleDiff(stats.DoorYawDeg, merged.YawDeg), Is.LessThanOrEqualTo(2.0), id + " yaw");
                    double best = 360;
                    for (int t = 0; t < m.TriangleCount; t++)
                    {
                        if (!IsColour(m, t, SacredPalette.SanctumDark)) continue;
                        double nx, ny, nz;
                        MeshingChecks.Facet(m, t, out nx, out ny, out nz);
                        if (Math.Abs(ny) > 0.2) continue;
                        best = Math.Min(best, AngleDiff(Bearing(nx, nz), stats.DoorYawDeg));
                    }
                    if (best < 360) Assert.That(best, Is.LessThanOrEqualTo(2.0), id + " sanctum door faces the door yaw");
                    TestContext.WriteLine("{0,-36} {1,6} tris  top {2,5:0.0}/{3,5:0.0}  tiers {4}  plinths {5}  yaw {6:0}", id, tris, stats.TopM, merged.HeightM,
                                          stats.Tiers, stats.PlinthLevels, stats.DoorYawDeg);
                }
            }
        }

        [Test]
        public void HeroPlansFollowTheirOsmOutlines()
        {
            int outlined = 0;
            foreach (HeroRecipe recipe in HeroCatalog.S1)
            {
                if (recipe.Form != HeroForm.Pagoda && recipe.Form != HeroForm.Mandapa && recipe.Form != HeroForm.ShikharaStone &&
                    recipe.Form != HeroForm.ShikharaPlaster)
                    continue;
                TileData tile;
                double x, z;
                int building;
                HeritageRecord rec = Locate(recipe, out tile, out x, out z, out building);
                if (building < 0) continue;
                if (HeroBuilder.Merge(rec).NoStair) continue; // on a palace block: the block is the outline
                var m = new MeshData();
                HeroBuilder.Build(rec, tile, 0, m, null);
                int[] ring = tile.Buildings[building].Rings[0];
                for (int k = 0; k < ring.Length / 2; k++)
                {
                    double px = ring[2 * k] / 100.0, pz = ring[2 * k + 1] / 100.0, d = double.MaxValue;
                    for (int v = 0; v < m.VertexCount; v++)
                        d = Math.Min(d, Math.Sqrt(Math.Pow(m.Positions[3 * v] - px, 2) + Math.Pow(m.Positions[3 * v + 2] - pz, 2)));
                    Assert.That(d, Is.LessThanOrEqualTo(0.1), recipe.Id + " outline vertex " + k);
                }
                outlined++;
            }
            Assert.That(outlined, Is.GreaterThanOrEqualTo(5));
        }

        private static SacredStats BuildHero(string id, out MeshData m)
        {
            HeroRecipe recipe;
            Assert.That(HeroCatalog.TryGet(id, out recipe), Is.True, id);
            TileData tile;
            double x, z;
            int building;
            HeritageRecord rec = Locate(recipe, out tile, out x, out z, out building);
            var stats = new SacredStats();
            m = new MeshData();
            HeroBuilder.Build(rec, tile, 0, m, null, stats);
            return stats;
        }

        [Test]
        public void SignatureCountsOfTheCentrepieces()
        {
            MeshData m;
            Assert.That(BuildHero("her.ptn.krishna_mandir", out m).Pinnacles, Is.EqualTo(21), "Krishna Mandir");
            Assert.That(BuildHero("her.ktm.swayambhunath", out m).Steps, Is.EqualTo(365), "Swayambhu east stair");
            SacredStats ny = BuildHero("her.bkt.nyatapola", out m);
            Assert.That(ny.Tiers, Is.EqualTo(5));
            Assert.That(ny.PlinthLevels, Is.EqualTo(5));
            Assert.That(ny.TopM, Is.EqualTo(33.2).Within(0.03 * 33.2));
            HeroRecipe nr;
            HeroCatalog.TryGet("her.bkt.nyatapola", out nr);
            // Bells: floor(eave / 0.4) per side; struts: max(2, round(wall / 1.1)) + 1 per side plus 4 corners.
            int bells = nr.EaveWidths.Sum(w => 4 * (int)Math.Floor(w / 0.4));
            Assert.That(ny.Bells, Is.EqualTo(bells).Within(4 * nr.Tiers), "bells by the 0.4 m pitch");
            Assert.That(ny.Struts, Is.GreaterThanOrEqualTo(nr.Tiers * (4 * PagodaGenerator.StrutsPerSide(1) + 4)));
            SacredStats boudha = BuildHero("her.ktm.boudhanath", out m);
            Assert.That(boudha.Terraces, Is.EqualTo(3));
            Assert.That(boudha.Rings, Is.EqualTo(13));
            Assert.That(boudha.FlagLines, Is.InRange(60, 120));
            Assert.That(boudha.TopM, Is.EqualTo(36.0).Within(0.03 * 36));
        }

        [Test]
        public void PagodaStrutsBellsAndTheClosedSanctum()
        {
            Assert.That(PagodaGenerator.StrutsPerSide(1.0), Is.EqualTo(3));
            Assert.That(PagodaGenerator.StrutsPerSide(5.5), Is.EqualTo(6));
            Assert.That(PagodaGenerator.StrutsPerSide(11.0), Is.EqualTo(11));
            PagodaParams p = PagodaParams.Defaults(10f, 10f, 3);
            p.EaveWidths = new[] { 9f, 6.8f, 5f };
            var stats = new SacredStats();
            var m = new MeshData();
            var c = new GenColliders();
            PagodaGenerator.Build(p, new GenFrame(50, 50, 100f, 0f), 0, m, c, stats);
            MeshingChecks.AssertWellFormed(m, "pagoda");
            Assert.That(stats.Tiers, Is.EqualTo(3));
            Assert.That(stats.Bells, Is.EqualTo(4 * (22 + 17 + 12)));
            Assert.That(stats.Struts % 4, Is.EqualTo(0));
            Assert.That(m.TriangleCount, Is.LessThanOrEqualTo(6000), "generic 3-tier budget");
            Assert.That(c.Boxes.Count + c.Ramps.Count, Is.GreaterThan(0), "plinth and stair colliders");
            // No interior: nothing lies inside the sanctum volume (the door is an opaque dark panel on its face).
            Assert.That(stats.SanctumHalfU, Is.GreaterThan(0.5f));
            float hu = stats.SanctumHalfU - 0.05f, hw = stats.SanctumHalfW - 0.05f;
            for (int v = 0; v < m.VertexCount; v++)
            {
                double dx = Math.Abs(m.Positions[3 * v] - 50), dz = Math.Abs(m.Positions[3 * v + 2] - 50), y = m.Positions[3 * v + 1] - 100;
                bool inside = dx < hu && dz < hw && y > stats.SanctumV0 + 0.05 && y < stats.SanctumV1 - 0.05;
                Assert.That(inside, Is.False, "vertex " + v + " inside the sanctum");
            }
            // LODs thin the detail.
            int previous = m.TriangleCount;
            for (int lod = 1; lod <= 3; lod++)
            {
                var ml = new MeshData();
                PagodaGenerator.Build(p, new GenFrame(50, 50, 100f, 0f), lod, ml, null);
                Assert.That(ml.TriangleCount, Is.LessThan(previous), "lod " + lod);
                previous = ml.TriangleCount;
            }
            // Deterministic.
            var again = new MeshData();
            PagodaGenerator.Build(p, new GenFrame(50, 50, 100f, 0f), 0, again, null);
            Assert.That(again.Positions.Take(again.VertexCount * 3), Is.EqualTo(m.Positions.Take(m.VertexCount * 3)));
        }

        [Test]
        public void OpenMandapaShrineIsSolidAndItsSanctumMatchesTheDrawnBox()
        {
            // Kasthamandap-like open hall: walkable plinth and hall, the closed central shrine is a NoClimb box (W2-O1:
            // sanctums are never entered) and the recorded sanctum is the drawn shrine (V5 checks use it).
            PagodaParams p = PagodaParams.Defaults(24f, 24f, 3);
            p.Open = true;
            p.EaveWidths = new[] { 22f, 16f, 11f };
            var stats = new SacredStats();
            var m = new MeshData();
            var c = new GenColliders();
            PagodaGenerator.Build(p, new GenFrame(50, 50, 100f, 0f), 0, m, c, stats);
            MeshingChecks.AssertWellFormed(m, "open mandapa");
            float bodyY = 100f + stats.SanctumV0 + 1.0f;
            bool solid = c.Boxes.Any(b => (b.Flags & GenColliderFlags.NoClimb) != 0 && Math.Abs(b.CX - 50) < b.HalfX && Math.Abs(b.CZ - 50) < b.HalfZ
                                          && b.CY - b.HalfY <= bodyY && b.CY + b.HalfY >= bodyY);
            Assert.That(solid, Is.True, "a NoClimb box fills the shrine at body height");
            // The recorded sanctum spans the drawn shrine (a quarter of the core width each side), not half of it.
            Assert.That(stats.SanctumHalfU, Is.GreaterThan(2.0f));
            Assert.That(stats.SanctumV1, Is.GreaterThan(stats.SanctumV0 + 1.5f));
            float hu = stats.SanctumHalfU - 0.05f, hw = stats.SanctumHalfW - 0.05f;
            for (int v = 0; v < m.VertexCount; v++)
            {
                double dx = Math.Abs(m.Positions[3 * v] - 50), dz = Math.Abs(m.Positions[3 * v + 2] - 50), y = m.Positions[3 * v + 1] - 100;
                bool inside = dx < hu && dz < hw && y > stats.SanctumV0 + 0.05 && y < stats.SanctumV1 - 0.05;
                Assert.That(inside, Is.False, "vertex " + v + " inside the shrine");
            }
        }

        [Test]
        public void ChaityaNichesFaceTheCompassInOrder()
        {
            Assert.That(ChaityaGenerator.NicheOrder, Is.EqualTo(new[] { "Akshobhya", "Ratnasambhava", "Amitabha", "Amoghasiddhi" }));
            Assert.That(ChaityaGenerator.NicheBearing, Is.EqualTo(new[] { 90f, 180f, 270f, 0f }));
            foreach (float yaw in new[] { 0f, 37f, 200f })
            {
                var m = new MeshData();
                ChaityaGenerator.Build(ChaityaParams.Defaults(2f), new GenFrame(10, 10, 0, yaw), 0, m, null);
                MeshingChecks.AssertWellFormed(m, "chaitya");
                Assert.That(m.TriangleCount, Is.LessThanOrEqualTo(600));
                // Each Buddha colour sits on the side of its compass bearing, whatever the frame's yaw.
                for (int q = 0; q < 4; q++)
                {
                    double sx = 0, sz = 0;
                    int n = 0;
                    for (int t = 0; t < m.TriangleCount; t++)
                    {
                        if (!IsColour(m, t, SacredPalette.NicheBuddha[q])) continue;
                        for (int k = 0; k < 3; k++)
                        {
                            int v = m.Indices[3 * t + k];
                            sx += m.Positions[3 * v] - 10;
                            sz += m.Positions[3 * v + 2] - 10;
                            n++;
                        }
                    }
                    Assert.That(n, Is.GreaterThan(0), ChaityaGenerator.NicheOrder[q]);
                    Assert.That(AngleDiff(Bearing(sx, sz), ChaityaGenerator.NicheBearing[q]), Is.LessThan(10), ChaityaGenerator.NicheOrder[q] + " at yaw " + yaw);
                }
            }
        }

        [Test]
        public void GenericSacredBuildingsOfTheSamplePackStayInBudget()
        {
            int built = 0;
            foreach (TileId id in new[] { new TileId(10, 516, 161), new TileId(10, 514, 162), new TileId(10, 521, 163) })
            {
                TileData t = Tiles[id];
                var h = new TileHeightSampler(t, 2);
                for (int i = 0; i < t.Buildings.Count; i++)
                {
                    SacredKind kind;
                    SacredParams p;
                    if (!SacredSelector.TrySelect(t, i, out kind, out p)) continue;
                    for (int lod = 0; lod <= 3; lod++)
                    {
                        var m = new MeshData();
                        Assert.That(SacredSelector.BuildGeneric(t, i, h, lod, m, null), Is.True);
                        MeshingChecks.AssertWellFormed(m, kind + " " + i);
                        // Shrines and chaityas 1,500 / 600 / 120; temples 6,000 at LOD0, then the hero B-class
                        // budgets (3,000 / 800 / 200) as the ceiling for the thinned LODs.
                        bool small = kind == SacredKind.Shrine || kind == SacredKind.Chaitya || kind == SacredKind.Hiti;
                        int[] cap = small ? new[] { 1500, 600, 120, 120 } : new[] { 6000, 3000, 800, 200 };
                        Assert.That(m.TriangleCount, Is.LessThanOrEqualTo(cap[lod]), kind + " " + i + " lod " + lod);
                    }
                    built++;
                }
            }
            Assert.That(built, Is.GreaterThan(30));
        }
    }
}
