using System;
using Ghumante.Core.Data;
using Ghumante.Core.Generators.Flora;

namespace Ghumante.Core.Generators.Placement
{
    /// <summary>
    /// Wild ground cover and field furniture (research street_life.md 11-12; ref_nature.md: the green valley hills,
    /// terraces and riverbeds): on an 8 m lattice over the tile, outside roads, buildings, water, parks and forest
    /// (forests have their own understorey): grass tufts in clusters on the grass and scrub hills and along field
    /// edges, a shrub here and there on scrub; rocks and boulders where the slope passes 29° (rocky outcrops) and on
    /// river banks and gravel beds; rice-straw stacks (kunyu) in twos and threes on flat paddy and cropland (drawn from
    /// the October harvest to March). None in the old core or the city's built-up cells except on river banks.
    /// Deterministic per lattice point; stops at the tile's plant budget.
    /// </summary>
    internal static class GroundCoverPlacement
    {
        private const uint Purpose = 0x47524E44;
        private const double StepM = 8.0;

        public static void Place(PlacementContext c)
        {
            int n = (int)(c.Size / StepM);
            for (int j = 0; j < n && !c.PlantsFull; j++)
            for (int i = 0; i < n && !c.PlantsFull; i++)
            {
                var rng = new FloraRng(FloraRng.Mix(c.Seed, (uint)(j * 4099 + i)), Purpose);
                double x = (i + 0.5) * StepM + rng.Jitter(3.5f), z = (j + 0.5) * StepM + rng.Jitter(3.5f);
                byte m = c.Mask.At(x, z);
                if ((m & (PlacementMask.Road | PlacementMask.Building | PlacementMask.Water | PlacementMask.Park | PlacementMask.Forest)) != 0) continue;
                bool bank = (m & PlacementMask.Bank) != 0, field = (m & PlacementMask.Field) != 0;
                AreaType at = c.AreaAt(x, z);
                bool built = at == AreaType.OldCore || at == AreaType.Urban;
                if (built && !bank) continue;
                Biome b = c.BiomeAt(x, z);
                float slope, aspect;
                c.Slope(x, z, out slope, out aspect);
                bool gravel = b == Biome.RiverbedGravel;
                if (bank || gravel)
                {
                    // River banks: boulders and cobbles with grass between.
                    if (rng.Chance(0.22f)) Put(c, rng.Chance(0.4f) ? TreeSpecies.Boulder : TreeSpecies.Rock, x, z, ref rng);
                    else if (rng.Chance(0.25f)) Cluster(c, TreeSpecies.GrassTuft, x, z, rng.Int(2, 4), 1.2f, ref rng);
                    continue;
                }
                if (slope > 0.55f)
                {
                    // Rocky outcrops on steep ground.
                    if (rng.Chance(0.16f)) Put(c, TreeSpecies.Boulder, x, z, ref rng);
                    else if (rng.Chance(0.18f)) Cluster(c, TreeSpecies.Rock, x, z, rng.Int(1, 3), 2f, ref rng);
                    else if (rng.Chance(0.15f)) Cluster(c, TreeSpecies.GrassTuft, x, z, rng.Int(2, 4), 1.5f, ref rng);
                    continue;
                }
                if (field)
                {
                    // Field edges: a tuft line where the field meets something else; straw stacks on flat fields.
                    bool edge = !Field(c, x + StepM * 0.75, z) || !Field(c, x - StepM * 0.75, z) || !Field(c, x, z + StepM * 0.75) || !Field(c, x, z - StepM * 0.75);
                    if (edge && rng.Chance(0.35f)) Cluster(c, TreeSpecies.GrassTuft, x, z, rng.Int(2, 4), 1.2f, ref rng);
                    else if (!edge && slope < 0.12f && rng.Chance(0.018f)) Cluster(c, TreeSpecies.StrawStack, x, z, rng.Int(2, 3), 3.5f, ref rng);
                    continue;
                }
                bool grassy = b == Biome.HillGrassland || b == Biome.HillScrub || b == Biome.AlpineMeadow || b == Biome.AlpineScrub || b == Biome.TeraiGrassland
                              || b == Biome.HillTerraces || b == Biome.None;
                if (!grassy) continue;
                if (rng.Chance(at == AreaType.PeriUrban ? 0.08f : 0.18f)) Cluster(c, TreeSpecies.GrassTuft, x, z, rng.Int(2, 5), 1.5f, ref rng);
                else if (b == Biome.HillScrub && rng.Chance(0.08f)) Put(c, TreeSpecies.Shrub, x, z, ref rng);
                else if (rng.Chance(0.03f)) Put(c, TreeSpecies.Rock, x, z, ref rng);
            }
        }

        private static bool Field(PlacementContext c, double x, double z)
        {
            return (c.Mask.At(x, z) & PlacementMask.Field) != 0;
        }

        private static void Cluster(PlacementContext c, TreeSpecies sp, double x, double z, int count, float spread, ref FloraRng rng)
        {
            for (int q = 0; q < count; q++)
                Put(c, sp, x + rng.Jitter(spread), z + rng.Jitter(spread), ref rng);
        }

        private static void Put(PlacementContext c, TreeSpecies sp, double x, double z, ref FloraRng rng)
        {
            float h, w;
            PlacementContext.Size01(sp, ref rng, out h, out w);
            if (!c.Clear(x, z, sp == TreeSpecies.GrassTuft ? 0.2 : 0.4 * w, PlacementContext.RoadMarginFor(sp, h, w))) return;
            c.Add(sp, x, z, h, w, rng.Range(0f, 360f), sp == TreeSpecies.StrawStack ? TreeOrigin.Field : TreeOrigin.Wild, 0, 0, false,
                  sp == TreeSpecies.GrassTuft ? 0.2f : -1f);
        }
    }
}
