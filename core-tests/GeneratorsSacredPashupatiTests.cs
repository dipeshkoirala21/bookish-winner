using System;
using System.Collections.Generic;
using System.IO;
using Ghumante.Core.Data;
using Ghumante.Core.Generators;
using Ghumante.Core.Generators.Sacred;
using Ghumante.Core.Generators.Sacred.Heroes.Pashupati;
using Ghumante.Core.Meshing;
using Ghumante.World.Sacred;
using Ghumante.World.Streaming;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The bespoke Pashupatinath site (docs/research/heritage/pashupati-temple.md): every part within its triangle ceilings
    /// at every LOD and well formed, the material channels and baked AO on every vertex, the measured tops, nothing round
    /// the courtyard taller than the temple's upper eave but the two Araucarias, the level courtyard and forecourt, the
    /// parts added and their buildings hidden by the hero set, the six heroes built by the streamer's own tile build, and
    /// the colliders on the plinths, stairs and floors.
    /// </summary>
    public class GeneratorsSacredPashupatiTests
    {
        private static TileData _tile;

        private static TileData Tile
        {
            get { return _tile ?? (_tile = StreamingSampleRegion.Tile(GeneratorsSacredPashupatiPreview.TempleTile)); }
        }

        private static MeshData Build(PashupatiPart part, int lod, GenColliders c = null, SacredStats stats = null)
        {
            var m = new MeshData();
            PashupatiSite.Build(part, Tile, lod, m, c, stats);
            return m;
        }

        private static IEnumerable<PashupatiPart> Parts()
        {
            for (int p = 0; p <= (int)PashupatiPart.NorthCourt; p++) yield return (PashupatiPart)p;
        }

        [Test]
        public void EveryPartStaysWithinItsCeilingsAtEveryLod()
        {
            int site0 = 0;
            foreach (PashupatiPart part in Parts())
            {
                int[] budget = PashupatiSite.BudgetOf(part), tris = new int[4];
                for (int lod = 0; lod <= 3; lod++)
                {
                    MeshData m = Build(part, lod);
                    tris[lod] = m.TriangleCount;
                    Assert.That(tris[lod], Is.GreaterThan(0), part + " lod " + lod);
                    Assert.That(tris[lod], Is.LessThanOrEqualTo(budget[lod]), part + " lod " + lod + " ceiling");
                    MeshingChecks.AssertWellFormed(m, part + " lod " + lod);
                }
                // Each LOD a real step down, the far form a few boxes.
                Assert.That(tris[1], Is.LessThan(0.45 * tris[0]), part + " LOD1 about 30 %");
                Assert.That(tris[2], Is.LessThan(0.5 * tris[1]), part + " LOD2 below LOD1");
                Assert.That(tris[3], Is.LessThanOrEqualTo(200), part + " LOD3");
                site0 += tris[0];
                TestContext.WriteLine("{0,-12} {1,7} {2,7} {3,6} {4,5}", part, tris[0], tris[1], tris[2], tris[3]);
            }
            Assert.That(site0, Is.LessThanOrEqualTo(150000), "the whole site at LOD0");
            Assert.That(PashupatiSite.BudgetOf(PashupatiPart.Temple)[0], Is.LessThanOrEqualTo(60000), "a major temple's allowance");
        }

        [Test]
        public void EveryVertexCarriesAChannelAndBakedAo()
        {
            foreach (PashupatiPart part in Parts())
            {
                MeshData m = Build(part, 0);
                Assert.That(m.HasUv0, Is.True, part + " UV0");
                float minAo = 1;
                var channels = new HashSet<int>();
                for (int v = 0; v < m.VertexCount; v++)
                {
                    float ch = m.Uv0[2 * v], ao = m.Uv0[2 * v + 1];
                    Assert.That(ch, Is.EqualTo(Math.Round(ch)).Within(1e-4), part + " channel is an integer");
                    Assert.That(ch, Is.InRange(0, 25), part + " channel");
                    Assert.That(ao, Is.InRange(0.05f, 1.0001f), part + " AO");
                    minAo = Math.Min(minAo, ao);
                    channels.Add((int)Math.Round(ch));
                }
                Assert.That(minAo, Is.LessThan(0.9f), part + " AO is baked (some vertices are occluded)");
                Assert.That(channels.Count, Is.GreaterThanOrEqualTo(4), part + " uses several material channels");
            }
        }

        [Test]
        public void TopsMatchTheMeasuredHeightsAndOnlyTheTreesRiseAboveTheTempleUpperEave()
        {
            foreach (PashupatiPart part in Parts())
            {
                MeshData m = Build(part, 0);
                double top = double.MinValue, built = double.MinValue;
                for (int v = 0; v < m.VertexCount; v++)
                {
                    double y = m.Positions[3 * v + 1] - PashupatiSite.CourtyardLevelM;
                    top = Math.Max(top, y);
                    if ((int)Math.Round(m.Uv0[2 * v]) != (int)MaterialChannel.Foliage && (int)Math.Round(m.Uv0[2 * v]) != (int)MaterialChannel.Bark)
                        built = Math.Max(built, y);
                }
                double want = PashupatiSite.TopOf(part);
                Assert.That(top, Is.EqualTo(want).Within(Math.Max(0.3, 0.03 * want)), part + " top");
                if (part != PashupatiPart.Temple)
                    Assert.That(built, Is.LessThan(PashupatiTemple.UpperEaveM), part + ": nothing built round the courtyard is taller than the temple's upper eave");
                TestContext.WriteLine("{0,-12} top {1,5:0.00} (catalogue {2:0.00}), built {3,5:0.00}", part, top, want, built);
            }
        }

        [Test]
        public void TheCourtyardIsOneLevelAndTheForecourtThreeStepsAbove()
        {
            TileData tile = StreamingSampleRegion.Tile(GeneratorsSacredPashupatiPreview.TempleTile);
            PashupatiGround.Apply(tile);
            Assert.That(PashupatiGround.Apply(tile), Is.EqualTo(0), "idempotent");
            var f = PashupatiFrame.For(tile, 0, null);
            // Sample points at least one height-grid cell (8 m) inside the levelled zones (the grid interpolates across
            // the zone edges).
            double[] courtyard = { 0, 0, -20, 10, 14, -14, 10, 22, -28, -10, 12, 5 };
            for (int i = 0; i < courtyard.Length / 2; i++)
                Assert.That(f.GroundAt(courtyard[2 * i], courtyard[2 * i + 1], double.NaN), Is.EqualTo(0).Within(0.08),
                            "courtyard at " + courtyard[2 * i] + ", " + courtyard[2 * i + 1]);
            Assert.That(f.GroundAt(-55, 0, double.NaN), Is.EqualTo(PashupatiSite.ForecourtRiseM).Within(0.08), "forecourt");
            // The east stair's foot stays on the ghat walk below.
            Assert.That(f.GroundAt(36.2, 7.2, double.NaN), Is.LessThan(-1.5), "ghat walk below the east stair");
        }

        [Test]
        public void TheHeroSetAddsThePartsAndHidesTheBuildingsTheyRebuild()
        {
            CuratedDb db = CuratedDb.Read(File.ReadAllBytes(Path.Combine(GoldenFiles.RepoRoot, "shared", "sample-regions", "kathmandu_core",
                                                                        "kathmandu_core.curated.ghcd")));
            HeroSet set = HeroSet.From(db);
            HeritageRecord temple;
            Assert.That(db.TryGetHeritage(PashupatiSite.HeroId, out temple), Is.True, "the curated DB holds the temple");
            ulong key;
            Assert.That(HeroSet.TryAnchorTile(temple, out key), Is.True);
            var ids = new HashSet<string>();
            foreach (HeritageRecord r in set.InTile(key)) ids.Add(r.Id);
            foreach (PashupatiPart part in Parts()) Assert.That(ids, Does.Contain(PashupatiSite.IdOf(part)), part + " anchored in the temple's tile");
            foreach (PashupatiPart part in Parts())
                foreach (string osm in PashupatiSite.HideRefs(part))
                    Assert.That(set.HiddenRefs, Does.Contain(HeritageRecord.WayRelationRef(osm)), part + " hides " + osm);
        }

        [Test]
        public void TheStreamedTileBuildsTheSixHeroesWithColliders()
        {
            MeshingSettings s = StreamingScene.Settings(2, 10);
            TileBuild b = StreamingScene.Build(GeneratorsSacredPashupatiPreview.TempleTile, s);
            var built = new Dictionary<string, HeroPiece>();
            foreach (HeroPiece p in b.Heroes) built[p.Id] = p;
            foreach (PashupatiPart part in Parts())
            {
                HeroPiece p;
                Assert.That(built.TryGetValue(PashupatiSite.IdOf(part), out p), Is.True, part + " built by the streamer");
                Assert.That(p.Tris0, Is.GreaterThan(0).And.LessThanOrEqualTo(PashupatiSite.BudgetOf(part)[0]), part + " LOD0");
                Assert.That(p.Tris3, Is.GreaterThan(0).And.LessThanOrEqualTo(200), part + " LOD3");
            }
            // The generic house bands no longer draw the buildings the site rebuilds (the old 5-6 storey blocks).
            Assert.That(s.Heroes.HiddenRefs, Does.Contain(HeritageRecord.WayRelationRef("w913170309")), "north wing hidden");
        }

        [Test]
        public void PlinthsStairsAndFloorsAreWalkable()
        {
            var c = new GenColliders();
            Build(PashupatiPart.Temple, 0, c);
            Assert.That(c.Ramps.Count, Is.GreaterThanOrEqualTo(4), "the four temple flights");
            Assert.That(CountWalkable(c), Is.GreaterThanOrEqualTo(3), "temple plinth and Nandi base");
            var e = new GenColliders();
            Build(PashupatiPart.EastGate, 0, e);
            bool stair = false;
            foreach (GenRamp r in e.Ramps) stair |= r.Y1 - r.Y0 > 2.0 || r.Y0 - r.Y1 > 2.0;
            Assert.That(stair, Is.True, "the east stair climbs from the ghat walk to the gate");
            foreach (PashupatiPart part in new[] { PashupatiPart.WestGate, PashupatiPart.MuktiMandap, PashupatiPart.SouthCourt, PashupatiPart.NorthCourt })
            {
                var pc = new GenColliders();
                Build(part, 0, pc);
                Assert.That(CountWalkable(pc), Is.GreaterThan(0), part + " has walkable plinths or floors");
                Assert.That(pc.Boxes.Count + pc.Walls.Count + pc.Cylinders.Count, Is.GreaterThan(4), part + " is solid");
            }
            // Colliders only at LOD0.
            var none = new GenColliders();
            Build(PashupatiPart.Temple, 1, none);
            Assert.That(none.Boxes.Count + none.Ramps.Count + none.Walls.Count + none.Cylinders.Count, Is.EqualTo(0));
        }

        private static int CountWalkable(GenColliders c)
        {
            int n = 0;
            foreach (GenBox b in c.Boxes)
                if ((b.Flags & GenColliderFlags.Walkable) != 0)
                    n++;
            return n;
        }

        [Test]
        public void TheSiteIsDeterministic()
        {
            foreach (PashupatiPart part in Parts())
            {
                MeshData a = Build(part, 0), b = Build(part, 0);
                Assert.That(b.VertexCount, Is.EqualTo(a.VertexCount), part.ToString());
                for (int i = 0; i < a.VertexCount * 3; i += 97) Assert.That(b.Positions[i], Is.EqualTo(a.Positions[i]), part + " vertex " + i / 3);
            }
        }
    }
}
