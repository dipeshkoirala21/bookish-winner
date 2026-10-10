using System;
using System.Collections.Generic;
using System.Linq;
using Ghumante.Core.Data;
using Ghumante.Core.Generators.Flora;
using Ghumante.Core.Generators.Placement;
using Ghumante.Core.Meshing;
using NUnit.Framework;

namespace Ghumante.Core.Tests
{
    /// <summary>
    /// The nature kit's draw budget and level rules (<see cref="FloraBudget"/>, <see cref="FloraLodPlan"/>,
    /// <see cref="FloraView"/>), which the game's DressingRenderer and the preview scenes share, and the far impostor's
    /// shape: a forest seen from inside or beside it is drawn with detailed, simple and volume trees near the camera
    /// and rounded impostors with trunks only beyond.
    /// </summary>
    public class GeneratorsFloraBudgetTests
    {
        private static readonly TileId Leaf = new TileId(10, 516, 161);

        [Test]
        public void TierBudgetsSumToTheVegetationSlice()
        {
            int[] slice = { 18000, 54000, 100000 };
            for (int tier = 0; tier < 3; tier++)
            {
                FloraBudget b = FloraBudget.ForTier(tier);
                Assert.That(b.VegetationTris, Is.EqualTo(slice[tier]), "tier " + tier);
                Assert.That(b.TreeLod0M, Is.LessThan(b.TreeLod1M));
                Assert.That(b.TreeLod1M, Is.LessThan(b.TreeVolumeM));
                Assert.That(b.TreeVolumeM, Is.LessThan(b.TreeFarM));
                Assert.That(b.PlantLod0M, Is.LessThan(b.PlantM));
                Assert.That(b.PlantFarM, Is.GreaterThanOrEqualTo(2.5f * b.PlantM), "big plants reach well past the small ones");
                // The volume budget holds the volume cap: a crowded forest spills to volumes, not to impostors.
                Assert.That(b.TreeVolumeTris, Is.GreaterThanOrEqualTo(b.TreeVolumeCap * 88));
            }
            Assert.That(FloraBudget.ForTier(-3).VegetationTris, Is.EqualTo(18000));
            Assert.That(FloraBudget.ForTier(7).VegetationTris, Is.EqualTo(100000));
        }

        [Test]
        public void ADenseForestSpillsToVolumesBeforeImpostors()
        {
            // A closed Schima-Castanopsis canopy on a north-facing hill, the camera at its middle looking north.
            TileData hill = MeshingChecks.SyntheticTile(Leaf, (x, z) => 1700 - 0.2 * (z - Leaf.Z0), 129, Biome.HillForest);
            var trees = new List<TreeInstance>();
            TreePlacement.Place(hill, new TileHeightSampler(hill, 1), new TreePlacementOptions { GroundCover = false }, trees);
            List<TreeInstance> forest = trees.Where(t => FloraCatalog.IsTree(t.Species)).ToList();
            int[] t0 = new int[FloraCatalog.Count], t1 = new int[FloraCatalog.Count], fv = new int[FloraMesher.Families], fi = new int[FloraMesher.Families];
            for (int s = 0; s < FloraCatalog.Count; s++)
            {
                t0[s] = FloraMesher.Build((TreeSpecies)s, 0, 10, new MeshData());
                t1[s] = FloraMesher.Build((TreeSpecies)s, 1, 10, new MeshData());
            }
            for (int f = 0; f < FloraMesher.Families; f++)
            {
                fv[f] = FloraMesher.Family((TreeShape)f, 2, new MeshData());
                fi[f] = FloraMesher.Family((TreeShape)f, 3, new MeshData());
            }
            const double cx = 512, cz = 300;
            for (int tier = 0; tier < 3; tier++)
            {
                FloraBudget b = FloraBudget.ForTier(tier);
                FloraView view = FloraView.Cone(cx, cz, 0, -0.2, 1, 90, b.ViewMarginDeg, b.ViewNearM);
                var c = new FloraLodCounters();
                double nearestImpostor = double.MaxValue, farthestVolume = 0;
                int drawn = 0;
                foreach (var q in forest.Select(t => (t, d: Math.Sqrt((t.X - cx) * (t.X - cx) + (t.Z - cz) * (t.Z - cz))))
                                        .Where(q => view.Sees(q.t.X, q.t.Z, 0.5 * q.t.CrownM)).OrderBy(q => q.d))
                {
                    int s = (int)q.t.Species, f = (int)q.t.Shape;
                    int lod = FloraLodPlan.PickTree(b, ref c, (float)q.d, t0[s], t1[s], fv[f], fi[f]);
                    if (lod < 0) continue;
                    drawn++;
                    if (lod == 3) nearestImpostor = Math.Min(nearestImpostor, q.d);
                    if (lod == 2) farthestVolume = Math.Max(farthestVolume, q.d);
                }
                TestContext.WriteLine("tier " + tier + ": LOD0 " + c.Lod0 + ", LOD1 " + c.Lod1 + ", volumes " + c.Volume + " (to " + farthestVolume.ToString("0") +
                                      " m), impostors " + c.Impostor + " (from " + nearestImpostor.ToString("0") + " m)");
                Assert.That(c.Lod0Tris, Is.LessThanOrEqualTo(b.TreeLod0Tris));
                Assert.That(c.Lod1Tris, Is.LessThanOrEqualTo(b.TreeLod1Tris));
                Assert.That(c.VolumeTris, Is.LessThanOrEqualTo(b.TreeVolumeTris));
                Assert.That(c.ImpostorTris, Is.LessThanOrEqualTo(b.TreeImpostorTris));
                Assert.That(c.Far, Is.LessThanOrEqualTo(b.TreeFarCap));
                Assert.That(c.Lod0, Is.GreaterThan(0));
                Assert.That(c.Volume, Is.GreaterThan(0));
                // Nearest first: every impostor stands beyond the volumes (the near trees get the better levels).
                Assert.That(nearestImpostor, Is.GreaterThanOrEqualTo(farthestVolume - 1e-3), "tier " + tier + ": an impostor in front of a volume");
                // On Mid and High no impostor stands inside the LOD1 radius; on Low none within 60 m.
                Assert.That(nearestImpostor, Is.GreaterThan(tier == 0 ? 60.0 : b.TreeLod1M), "tier " + tier + ": impostors close to the camera");
            }
        }

        [Test]
        public void TheViewConeSeesAheadAndRoundTheCamera()
        {
            FloraView v = FloraView.Cone(0, 0, 0, 0, 1, 90, 12, 18);
            Assert.That(v.Directional, Is.True);
            Assert.That(v.Sees(0, 100, 1), Is.True, "ahead");
            Assert.That(v.Sees(90, 100, 1), Is.True, "inside the half angle plus margin");
            Assert.That(v.Sees(0, -100, 1), Is.False, "behind");
            Assert.That(v.Sees(100, -20, 1), Is.False, "beside and behind");
            Assert.That(v.Sees(10, -10, 1), Is.True, "close behind the camera (shadows, the ground below)");
            Assert.That(v.Sees(100, 40, 30), Is.True, "a big crown reaching into the cone");
            Assert.That(v.Sees(100, 40, 1), Is.False, "a small one there is not seen");
            Assert.That(FloraView.Cone(0, 0, 0, -1, 0.1, 90, 12, 18).Sees(0, -100, 1), Is.True, "looking straight down sees all round");
            Assert.That(FloraView.All(0, 0).Sees(0, -500, 1), Is.True);
            Assert.That(FloraView.HorizontalFov(60, 16.0 / 9.0), Is.EqualTo(91.5).Within(0.5));
            Assert.That(FloraView.HorizontalFov(60, 9.0 / 16.0), Is.EqualTo(36.0).Within(0.5), "portrait");
        }

        [Test]
        public void BigPlantsAreDrawnFarSmallOnesNear()
        {
            FloraBudget b = FloraBudget.ForTier(1);
            Assert.That(FloraCatalog.DrawsFar(TreeSpecies.StrawStack) && FloraCatalog.DrawsFar(TreeSpecies.Boulder) && FloraCatalog.DrawsFar(TreeSpecies.Hedge), Is.True);
            Assert.That(FloraCatalog.DrawsFar(TreeSpecies.GrassTuft) || FloraCatalog.DrawsFar(TreeSpecies.Fern) || FloraCatalog.DrawsFar(TreeSpecies.Rock), Is.False);
            var c = new FloraLodCounters();
            Assert.That(FloraLodPlan.PickPlant(b, ref c, 5f, false, 100, 20), Is.EqualTo(0), "near: LOD0");
            Assert.That(FloraLodPlan.PickPlant(b, ref c, 0.5f * (b.PlantLod0M + b.PlantM), false, 100, 20), Is.EqualTo(1));
            Assert.That(FloraLodPlan.PickPlant(b, ref c, b.PlantM + 5f, false, 100, 20), Is.EqualTo(FloraLodPlan.None), "small kinds stop at the plant radius");
            Assert.That(FloraLodPlan.PickPlant(b, ref c, b.PlantM + 5f, true, 100, 20), Is.EqualTo(1), "straw stacks, boulders and hedges go on");
            Assert.That(FloraLodPlan.PickPlant(b, ref c, b.PlantFarM + 1f, true, 100, 20), Is.EqualTo(FloraLodPlan.None));
            // The triangle budget holds: LOD0 falls back to LOD1, then nothing.
            var full = new FloraLodCounters { PlantTris = b.PlantTris - 50 };
            Assert.That(FloraLodPlan.PickPlant(b, ref full, 5f, false, 100, 20), Is.EqualTo(1));
            Assert.That(FloraLodPlan.PickPlant(b, ref full, 5f, false, 100, 40), Is.EqualTo(FloraLodPlan.None));
        }

        [Test]
        public void ImpostorsAreRoundedCrownsOnTrunks()
        {
            var m = new MeshData();
            for (int f = 0; f < FloraMesher.Families; f++)
            {
                m.Clear();
                int tris = FloraMesher.Family((TreeShape)f, 3, m);
                Assert.That(tris, Is.InRange(16, FloraMesher.Budget[3]));
                float barkLow = float.MaxValue, barkHigh = float.MinValue, crownLow = float.MaxValue, crownHigh = float.MinValue, widest = 0f;
                for (int v = 0; v < m.VertexCount; v++)
                {
                    float x = m.Positions[3 * v], y = m.Positions[3 * v + 1], z = m.Positions[3 * v + 2];
                    if (m.Uv0[2 * v] == (float)MaterialChannel.Bark)
                    {
                        barkLow = Math.Min(barkLow, y);
                        barkHigh = Math.Max(barkHigh, y);
                    }
                    else
                    {
                        crownLow = Math.Min(crownLow, y);
                        crownHigh = Math.Max(crownHigh, y);
                        widest = Math.Max(widest, (float)Math.Sqrt(x * x + z * z));
                    }
                }
                string what = (TreeShape)f + " impostor";
                Assert.That(barkLow, Is.LessThan(0.01f), what + ": the trunk stands on the ground");
                Assert.That(barkHigh, Is.GreaterThan(crownLow), what + ": the trunk reaches into the crown");
                Assert.That(widest, Is.InRange(0.45f, 0.55f), what + ": as wide as the crown");
                // Rounded, not a crystal: the crown's upper and lower quarters are still broad (a dome and a shallow
                // underside), so no spike sticks out of the outline.
                float h = crownHigh - crownLow;
                float upper = 0f, lower = 0f;
                for (int v = 0; v < m.VertexCount; v++)
                {
                    if (m.Uv0[2 * v] == (float)MaterialChannel.Bark) continue;
                    float y = m.Positions[3 * v + 1], r = (float)Math.Sqrt(m.Positions[3 * v] * m.Positions[3 * v] + m.Positions[3 * v + 2] * m.Positions[3 * v + 2]);
                    if (y > crownLow + 0.7f * h) upper = Math.Max(upper, r);
                    if (y < crownLow + 0.3f * h) lower = Math.Max(lower, r);
                }
                Assert.That(upper, Is.GreaterThan(0.3f), what + ": a domed top");
                Assert.That(lower, Is.GreaterThan(0.3f), what + ": a rounded underside");
            }
        }
    }
}
