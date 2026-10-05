using System;

namespace Ghumante.World.Sacred
{
    /// <summary>
    /// The hero LOD allocator of W2_DESIGN 3.2 (contract: <c>World/Sacred/HeroLodBudget.cs</c>). Each frame the caller
    /// fills the visible heroes (screen height as a fraction of the viewport height, triangles per LOD, current LOD and
    /// when it last changed); <see cref="Allocate"/> gives each its LOD by screen height (0.35 / 0.12 / 0.04, ASSET_MANIFEST
    /// 1.5), then steps the smallest-on-screen hero down one LOD at a time until the hero slice fits. Low never uses LOD0.
    /// A hero changes at most one LOD per <see cref="MinChangeIntervalS"/>, and only once its screen height passes a
    /// threshold by the 10% hysteresis. Engine-free, allocation-free after the first frames.
    /// </summary>
    public sealed class HeroLodBudget
    {
        /// <summary>Screen-height thresholds for LOD0, LOD1 and LOD2 (below the last: LOD3).</summary>
        public static readonly float[] Thresholds = { 0.35f, 0.12f, 0.04f };

        /// <summary>Hysteresis as a fraction of the threshold.</summary>
        public const float Hysteresis = 0.10f;

        /// <summary>Seconds between two LOD changes of one hero.</summary>
        public const float MinChangeIntervalS = 0.5f;

        public const int LodCount = 4;

        /// <summary>One visible hero.</summary>
        public struct Entry
        {
            /// <summary>Screen height (fraction of the viewport height).</summary>
            public float ScreenHeight;

            /// <summary>Triangles of LOD 0..3.</summary>
            public int Tris0, Tris1, Tris2, Tris3;

            /// <summary>The LOD shown now (−1 when the hero was not shown last frame: it takes its target at once).</summary>
            public int CurrentLod;

            /// <summary>Time (s) of the last LOD change.</summary>
            public float LastChangeS;

            /// <summary>Output: the LOD to show.</summary>
            public int Lod;

            public int Tris(int lod)
            {
                switch (lod)
                {
                    case 0: return Tris0;
                    case 1: return Tris1;
                    case 2: return Tris2;
                    default: return Tris3;
                }
            }
        }

        private int[] _order = new int[16];
        private float[] _keys = new float[16];

        /// <summary>Triangle budget of the hero slice.</summary>
        public int BudgetTris;

        /// <summary>False on Low: LOD0 is never used.</summary>
        public bool AllowLod0 = true;

        /// <summary>Triangles of the last allocation.</summary>
        public int LastTris { get; private set; }

        public HeroLodBudget(int budgetTris, bool allowLod0)
        {
            BudgetTris = budgetTris;
            AllowLod0 = allowLod0;
        }

        /// <summary>The LOD a screen height asks for, with hysteresis around the current LOD.</summary>
        public static int TargetLod(float screenHeight, int currentLod)
        {
            int lod = Thresholds.Length;
            for (int k = 0; k < Thresholds.Length; k++)
            {
                float t = Thresholds[k];
                // Moving to a finer LOD than the current one needs 10% more height; keeping a finer one needs 10% less.
                if (currentLod >= 0)
                {
                    if (k < currentLod) t *= 1f + Hysteresis;
                    else t *= 1f - Hysteresis;
                }
                if (screenHeight >= t)
                {
                    lod = k;
                    break;
                }
            }
            return lod;
        }

        /// <summary>Assign <see cref="Entry.Lod"/> for the first <paramref name="count"/> entries at time
        /// <paramref name="nowS"/>; changed entries get <see cref="Entry.LastChangeS"/> = now and
        /// <see cref="Entry.CurrentLod"/> = their new LOD. Returns the triangles used.</summary>
        public int Allocate(Entry[] heroes, int count, float nowS)
        {
            if (heroes == null) throw new ArgumentNullException(nameof(heroes));
            if (count > heroes.Length) count = heroes.Length;
            if (_order.Length < count)
            {
                _order = new int[Math.Max(count, _order.Length * 2)];
                _keys = new float[_order.Length];
            }
            int minLod = AllowLod0 ? 0 : 1;
            int tris = 0;
            for (int i = 0; i < count; i++)
            {
                int want = Math.Max(minLod, TargetLod(heroes[i].ScreenHeight, heroes[i].CurrentLod));
                heroes[i].Lod = Limit(heroes[i], want, nowS);
                tris += heroes[i].Tris(heroes[i].Lod);
                _order[i] = i;
                _keys[i] = heroes[i].ScreenHeight;
            }
            // Smallest on screen first.
            Array.Sort(_keys, _order, 0, count);
            bool stepped = true;
            while (tris > BudgetTris && stepped)
            {
                stepped = false;
                for (int o = 0; o < count && tris > BudgetTris; o++)
                {
                    int i = _order[o];
                    int lod = heroes[i].Lod;
                    if (lod >= LodCount - 1) continue;
                    // At most one step from the shown LOD, and a real change waits for the interval (a hero new this
                    // frame, or one whose refinement is undone, steps freely).
                    int cur = heroes[i].CurrentLod;
                    if (cur >= 0)
                    {
                        if (lod > cur) continue;
                        if (lod + 1 != cur && nowS - heroes[i].LastChangeS < MinChangeIntervalS) continue;
                    }
                    tris -= heroes[i].Tris(lod) - heroes[i].Tris(lod + 1);
                    heroes[i].Lod = lod + 1;
                    stepped = true;
                    break; // re-scan from the smallest
                }
            }
            for (int i = 0; i < count; i++)
            {
                if (heroes[i].Lod == heroes[i].CurrentLod) continue;
                heroes[i].CurrentLod = heroes[i].Lod;
                heroes[i].LastChangeS = nowS;
            }
            LastTris = tris;
            return tris;
        }

        /// <summary>At most one LOD step per interval from the current LOD (a new hero takes its target at once).</summary>
        private static int Limit(in Entry e, int want, float nowS)
        {
            if (e.CurrentLod < 0) return want;
            if (want == e.CurrentLod) return want;
            if (nowS - e.LastChangeS < MinChangeIntervalS) return e.CurrentLod;
            return want < e.CurrentLod ? e.CurrentLod - 1 : e.CurrentLod + 1;
        }
    }
}
