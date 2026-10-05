using System;

namespace Ghumante.World.Life
{
    /// <summary>
    /// Render levels of the life presenters per tier (W2_DESIGN 10.4 and 5.4-5.5): how many moving vehicles, people and
    /// animals may be drawn at each level and how far, plus the nearest-first allocator they share. Engine-free.
    /// </summary>
    public sealed class LifeLod
    {
        /// <summary>Moving vehicles at LOD0 / LOD1 / LOD2 (the player's vehicle is extra) and their radii.</summary>
        public int[] VehicleCaps;

        public float[] VehicleRadii = { 25f, 80f, 400f };

        /// <summary>People: skinned-equivalent LOD0 (≤ 15 m), LOD1 (≤ 40 m), far block-out (≤ 90 m).</summary>
        public int[] PeopleCaps;

        public float[] PeopleRadii = { 15f, 40f, 90f };

        /// <summary>Animals: LOD0 (≤ 10 m), LOD1 (≤ 30 m), far (beyond, nearest first).</summary>
        public int[] AnimalCaps;

        public float[] AnimalRadii = { 10f, 30f, 150f };

        /// <summary>Live engine voices for traffic (the synth budget minus the player's engine).</summary>
        public int EngineVoices;

        /// <summary>NPC footsteps are played within this distance of the camera (the mixer caps them per tier).</summary>
        public float FootstepRadiusM = 12f;

        public static LifeLod ForTier(int tier)
        {
            switch (tier <= 0 ? 0 : tier >= 2 ? 2 : 1)
            {
                case 0:
                    return new LifeLod { VehicleCaps = new[] { 0, 1, 6 }, PeopleCaps = new[] { 1, 4, 13 }, AnimalCaps = new[] { 0, 1, 6 }, EngineVoices = 3 };
                case 1:
                    return new LifeLod { VehicleCaps = new[] { 1, 6, 16 }, PeopleCaps = new[] { 3, 10, 39 }, AnimalCaps = new[] { 1, 3, 15 }, EngineVoices = 6 };
                default:
                    return new LifeLod { VehicleCaps = new[] { 3, 10, 20 }, PeopleCaps = new[] { 6, 20, 78 }, AnimalCaps = new[] { 2, 6, 35 }, EngineVoices = 10 };
            }
        }

        /// <summary>
        /// Assigns a level (0, 1, 2; −1 = not drawn) to each of <paramref name="count"/> items whose distances are in
        /// <paramref name="distance"/>: nearest first, an item takes the finest level whose radius covers it and whose
        /// cap is not full, else the next coarser; beyond the last radius or every cap it is not drawn.
        /// <paramref name="order"/> is scratch of at least <paramref name="count"/> (it ends sorted by distance) and
        /// <paramref name="keys"/> too.
        /// </summary>
        public static void Assign(float[] distance, int count, int[] caps, float[] radii, int[] level, int[] order, float[] keys)
        {
            for (int i = 0; i < count; i++)
            {
                order[i] = i;
                keys[i] = distance[i];
                level[i] = -1;
            }
            Array.Sort(keys, order, 0, count);
            int u0 = 0, u1 = 0, u2 = 0;
            for (int k = 0; k < count; k++)
            {
                float d = keys[k];
                int i = order[k];
                if (d <= radii[0] && u0 < caps[0])
                {
                    level[i] = 0;
                    u0++;
                }
                else if (d <= radii[1] && u1 < caps[1])
                {
                    level[i] = 1;
                    u1++;
                }
                else if (d <= radii[2] && u2 < caps[2])
                {
                    level[i] = 2;
                    u2++;
                }
            }
        }
    }
}
