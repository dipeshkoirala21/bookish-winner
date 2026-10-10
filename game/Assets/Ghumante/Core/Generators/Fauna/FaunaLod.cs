using System;

namespace Ghumante.Core.Generators.Fauna
{
    /// <summary>
    /// How many animals and birds are drawn at which level on each device tier, within the animals-and-birds triangle
    /// slice (W2_DESIGN 5.5, 5.6 and 10.4: 4 k / 14 k / 30 k on Low / Mid / High), and which ambient flocks run within
    /// 90 m. Engine-free; the wildlife presenters call it once per frame with scratch arrays (no allocation).
    /// <para>
    /// Animals: LOD0 within 10 m (0 / 1 / 2), LOD1 within 30 m (1 / 3 / 6), LOD2 beyond (6 / 15 / 35), nearest first.
    /// Birds: full birds within 8 m (0 / 4 / 8), light birds within 25 m (15 / 30 / 75), and every other bird as a
    /// 4-triangle paper bird with no count cap: flying up to 600 m, and on the ground (a sitting paper bird) up to
    /// 70 m, so a square's whole flock is always there, also the birds beyond the caps right next to the camera.
    /// The caps of each tier fit the slice (<see cref="CapTris"/>); when paper birds push a frame over it, the
    /// farthest light birds become paper birds and, last, the farthest far animals are left out.
    /// </para>
    /// </summary>
    public static class FaunaLod
    {
        /// <summary>Bird levels.</summary>
        public const int Hidden = -1, Full = 0, Light = 1, Paper = 2, PaperGround = 3;

        /// <summary>Radii of the bird levels (m): full birds, light birds, sitting paper birds, flying paper birds.</summary>
        public const float FullBirdRadiusM = 8f, LightBirdRadiusM = 25f, GroundPaperRadiusM = 70f, AirPaperRadiusM = 600f;

        /// <summary>Radii of the animal levels (m).</summary>
        public static readonly float[] AnimalRadii = { 10f, 30f, 150f };

        /// <summary>Triangle budgets of the design (W2_DESIGN 5.5-5.6): animal LOD0 / LOD1 / LOD2, full and light
        /// birds, paper birds.</summary>
        public const int AnimalLod0Tris = 2500, AnimalLod1Tris = 1000, AnimalLod2Tris = 300, FullBirdTris = 300, LightBirdTris = 80, PaperBirdTris = 4;

        /// <summary>Flocks are capped within this distance of the player (m).</summary>
        public const float FlockCapRadiusM = 90f;

        /// <summary>A running flock keeps its place against a new one up to this factor nearer (hysteresis).</summary>
        public const float RunningBias = 0.75f;

        private static int T(int tier)
        {
            return tier <= 0 ? 0 : tier >= 2 ? 2 : 1;
        }

        /// <summary>The animals-and-birds triangle slice of a tier (W2_DESIGN 10.4).</summary>
        public static int SliceTris(int tier)
        {
            switch (T(tier))
            {
                case 0: return 4000;
                case 1: return 14000;
                default: return 30000;
            }
        }

        /// <summary>Animal caps of a tier: LOD0, LOD1, LOD2 (fills <paramref name="caps"/>, three entries).</summary>
        public static void AnimalCaps(int tier, int[] caps)
        {
            switch (T(tier))
            {
                case 0:
                    caps[0] = 0;
                    caps[1] = 1;
                    caps[2] = 6;
                    break;
                case 1:
                    caps[0] = 1;
                    caps[1] = 3;
                    caps[2] = 15;
                    break;
                default:
                    caps[0] = 2;
                    caps[1] = 6;
                    caps[2] = 35;
                    break;
            }
        }

        /// <summary>Bird caps of a tier: full birds within 8 m, light birds within 25 m.</summary>
        public static void BirdCaps(int tier, out int full, out int light)
        {
            int t = T(tier);
            full = t == 0 ? 0 : t == 1 ? 4 : 8;
            light = t == 0 ? 15 : t == 1 ? 30 : 75;
        }

        /// <summary>Flocks allowed within 90 m: pigeon flocks and small flocks (crows, sparrows, mynas, swallows,
        /// egrets; kites are not capped).</summary>
        public static void FlockCaps(int tier, out int pigeonFlocks, out int smallFlocks)
        {
            int t = T(tier);
            pigeonFlocks = t <= 1 ? 1 : 2;
            smallFlocks = t == 0 ? 1 : t == 1 ? 2 : 3;
        }

        /// <summary>Worst-case triangles of a tier's animal caps at the design budgets.</summary>
        public static int AnimalCapTris(int tier)
        {
            switch (T(tier))
            {
                case 0: return 0 * AnimalLod0Tris + 1 * AnimalLod1Tris + 6 * AnimalLod2Tris;
                case 1: return 1 * AnimalLod0Tris + 3 * AnimalLod1Tris + 15 * AnimalLod2Tris;
                default: return 2 * AnimalLod0Tris + 6 * AnimalLod1Tris + 35 * AnimalLod2Tris;
            }
        }

        /// <summary>Worst-case triangles of a tier's full and light bird caps at the design budgets.</summary>
        public static int BirdCapTris(int tier)
        {
            BirdCaps(tier, out int full, out int light);
            return full * FullBirdTris + light * LightBirdTris;
        }

        /// <summary>Worst-case triangles of every capped level of a tier (animals and birds; paper birds come on top
        /// and are paid for by demoting light birds).</summary>
        public static int CapTris(int tier)
        {
            return AnimalCapTris(tier) + BirdCapTris(tier);
        }

        /// <summary>Triangles of a bird drawn at <paramref name="level"/>.</summary>
        public static int BirdTris(FaunaSpecies s, int level)
        {
            switch (level)
            {
                case Full: return FaunaCatalog.Info(s).Lod0Tris;
                case Light: return FaunaCatalog.Info(s).Lod1Tris;
                case Paper:
                case PaperGround: return PaperBirdTris;
                default: return 0;
            }
        }

        /// <summary>
        /// Assigns a level to each of <paramref name="count"/> birds (<see cref="Full"/>, <see cref="Light"/>,
        /// <see cref="Paper"/>, <see cref="PaperGround"/> or <see cref="Hidden"/>) from their distances to the camera,
        /// nearest first under the tier's caps; then, while the triangles exceed <paramref name="budgetTris"/>, the
        /// farthest light birds become paper birds (full birds next). Paper birds are never dropped within their
        /// radii. Returns the triangles drawn. <paramref name="order"/> and <paramref name="keys"/> are scratch (at
        /// least <paramref name="count"/>).
        /// </summary>
        public static int AssignBirds(float[] distance, bool[] airborne, FaunaSpecies[] species, int count, int tier, int budgetTris, int[] level, int[] order,
                                      float[] keys)
        {
            BirdCaps(tier, out int capFull, out int capLight);
            Sort(distance, count, order, keys);
            int full = 0, light = 0, tris = 0;
            for (int k = 0; k < count; k++)
            {
                int i = order[k];
                float d = keys[k];
                int l;
                if (d <= FullBirdRadiusM && full < capFull)
                {
                    l = Full;
                    full++;
                }
                else if (d <= LightBirdRadiusM && light < capLight)
                {
                    l = Light;
                    light++;
                }
                else if (airborne[i])
                {
                    l = d <= AirPaperRadiusM ? Paper : Hidden;
                }
                else
                {
                    l = d <= GroundPaperRadiusM ? PaperGround : Hidden;
                }
                level[i] = l;
                tris += BirdTris(species[i], l);
            }
            // Over the budget: the farthest light (then full) birds become paper birds.
            for (int pass = Light; pass >= Full && tris > budgetTris; pass--)
            {
                for (int k = count - 1; k >= 0 && tris > budgetTris; k--)
                {
                    int i = order[k];
                    if (level[i] != pass) continue;
                    int l = airborne[i] ? Paper : PaperGround;
                    tris += BirdTris(species[i], l) - BirdTris(species[i], pass);
                    level[i] = l;
                }
            }
            return tris;
        }

        /// <summary>
        /// Assigns a level (0, 1, 2; −1 = not drawn) to each of <paramref name="count"/> animals nearest first under
        /// the tier's caps and radii; then, while the triangles exceed <paramref name="budgetTris"/>, the farthest are
        /// demoted a level (LOD2 ones are left out). Returns the triangles drawn.
        /// </summary>
        public static int AssignAnimals(float[] distance, FaunaSpecies[] species, int count, int tier, int budgetTris, int[] level, int[] order, float[] keys,
                                        int[] caps)
        {
            AnimalCaps(tier, caps);
            Sort(distance, count, order, keys);
            int u0 = 0, u1 = 0, u2 = 0, tris = 0;
            for (int k = 0; k < count; k++)
            {
                int i = order[k];
                float d = keys[k];
                int l = -1;
                if (d <= AnimalRadii[0] && u0 < caps[0])
                {
                    l = 0;
                    u0++;
                }
                else if (d <= AnimalRadii[1] && u1 < caps[1])
                {
                    l = 1;
                    u1++;
                }
                else if (d <= AnimalRadii[2] && u2 < caps[2])
                {
                    l = 2;
                    u2++;
                }
                level[i] = l;
                if (l >= 0) tris += FaunaCatalog.Info(species[i]).Budget(l);
            }
            for (int k = count - 1; k >= 0 && tris > budgetTris; k--)
            {
                int i = order[k];
                int l = level[i];
                if (l < 0) continue;
                int now = FaunaCatalog.Info(species[i]).Budget(l);
                int next = l >= 2 ? -1 : l + 1;
                tris += (next < 0 ? 0 : FaunaCatalog.Info(species[i]).Budget(next)) - now;
                level[i] = next;
                if (next >= 0) k++; // look at the same animal again (it may need to go one more level)
            }
            return tris;
        }

        /// <summary>
        /// Chooses which planned groups run (<paramref name="keep"/>): within 90 m at most the tier's pigeon flocks and
        /// small flocks; the curated square flocks come first, a flock already running (<paramref name="running"/>)
        /// keeps its place against a new one up to 25% nearer, then the nearest. Herds, kites and flocks beyond 90 m
        /// always run. <paramref name="score"/> and <paramref name="order"/> are scratch (at least
        /// <paramref name="n"/>).
        /// </summary>
        public static void SelectGroups(AmbientGroup[] planned, int n, bool[] running, int tier, bool[] keep, float[] score, int[] order)
        {
            FlockCaps(tier, out int pigeonCap, out int smallCap);
            for (int i = 0; i < n; i++) keep[i] = true;
            Cap(planned, n, running, true, pigeonCap, keep, score, order);
            Cap(planned, n, running, false, smallCap, keep, score, order);
        }

        private static void Cap(AmbientGroup[] g, int n, bool[] running, bool pigeons, int cap, bool[] keep, float[] score, int[] order)
        {
            int m = 0;
            for (int i = 0; i < n; i++)
            {
                AmbientKind k = g[i].Kind;
                bool capped = pigeons ? k == AmbientKind.PigeonFlock : g[i].IsFlock && k != AmbientKind.PigeonFlock && k != AmbientKind.KiteGroup;
                if (!capped) continue;
                bool run = running != null && running[i];
                float d = g[i].DistanceM;
                // A running flock counts as within the cap radius a little longer (no flicker at 90 m).
                if (d >= FlockCapRadiusM * (run ? 1.15f : 1f)) continue;
                float s = run ? d * RunningBias : d;
                if (g[i].IsCurated) s -= 1e5f;
                order[m] = i;
                score[m] = s;
                m++;
            }
            // Insertion sort by score, then key (stable and deterministic).
            for (int a = 1; a < m; a++)
            {
                int oi = order[a];
                float sv = score[a];
                int b = a - 1;
                while (b >= 0 && (score[b] > sv || score[b] == sv && g[order[b]].Key > g[oi].Key))
                {
                    score[b + 1] = score[b];
                    order[b + 1] = order[b];
                    b--;
                }
                score[b + 1] = sv;
                order[b + 1] = oi;
            }
            for (int a = cap; a < m; a++) keep[order[a]] = false;
        }

        private static void Sort(float[] distance, int count, int[] order, float[] keys)
        {
            for (int i = 0; i < count; i++)
            {
                order[i] = i;
                keys[i] = distance[i];
            }
            Array.Sort(keys, order, 0, count);
        }
    }
}
