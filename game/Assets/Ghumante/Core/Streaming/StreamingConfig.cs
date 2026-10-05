using System;
using Ghumante.Core.Data;

namespace Ghumante.Core.Streaming
{
    /// <summary>
    /// World-streaming settings for one device tier (ARCHITECTURE.md 7.2 and 10): the LOD rings, a cap on resident
    /// tile views and the byte budget of the decoded-tile cache. <see cref="ForTier"/> returns fresh instances, so
    /// callers may tweak them (call <see cref="Validate"/> afterwards).
    /// </summary>
    public sealed class StreamingConfig
    {
        public const int TierLow = 0;
        public const int TierMid = 1;
        public const int TierHigh = 2;

        /// <summary>LOD rings, finest level first, levels strictly decreasing and radii strictly increasing. The
        /// last (coarsest) ring's radius is the view radius: areas farther than that are not selected.</summary>
        public LodRing[] Rings;

        /// <summary>Cap on resident tile views (selected nodes with meshes). Sized from the worst-case selection
        /// of the tier on a fully covered pack (StreamingSelectorTests measures it) plus room for the nodes kept
        /// while their replacements load.</summary>
        public int MaxResidentTiles;

        /// <summary>Byte budget of the decoded-tile LRU (<see cref="LruByteCache{TKey,TValue}"/>, ARCHITECTURE 10:
        /// 30 / 60 / 90 MB).</summary>
        public long CacheBudgetBytes;

        /// <summary>Vector detail (roads, buildings, areas) is meshed only for exact nodes whose closest point lies
        /// within this many metres of the focus (<see cref="SelectedNode.DrawsDetail"/>); farther near-ring tiles draw
        /// terrain only. Positive infinity (the default of a hand-made config) draws detail on every exact node.</summary>
        public double DetailRadiusM = double.PositiveInfinity;

        /// <summary>
        /// Split/merge hysteresis as a fraction of each radius: an area split by the previous selection stays split
        /// until its closest point is beyond <c>radius × (1 + HysteresisFraction)</c>, and a node drawing detail
        /// keeps it until beyond <c>DetailRadiusM × (1 + HysteresisFraction)</c>. 0 disables it (selection becomes a
        /// pure function of the focus).
        /// </summary>
        public double HysteresisFraction = DefaultHysteresisFraction;

        /// <summary>Default <see cref="HysteresisFraction"/>: 10 % (75 m on the Low near ring).</summary>
        public const double DefaultHysteresisFraction = 0.1;

        /// <summary>
        /// Settings for tier 0 (Low), 1 (Mid) or 2 (High). Near/mid/far radii are ARCHITECTURE 7.2's; the horizon
        /// levels 7, 6 and 5 reach 50 / 80 / 120 km. Terrain quads per area (L10 32 / 64 / 64, L9 and L8 32, horizon
        /// 16) keep a whole worst-case selection's terrain within ARCHITECTURE 10's visible-triangle estimate
        /// (144 k / 395 k / 604 k triangles measured by StreamingConfigTests on a fully covered pack, of which
        /// roughly a third is in view); the 2 m near-field refinement of ARCHITECTURE 6.3 comes later.
        /// <para>
        /// Roads, buildings and areas cost far more than terrain in town (a dense 1 km tile of Thamel is about 140 k
        /// building triangles), so they are drawn only for level-10 tiles within <see cref="DetailRadiusM"/>
        /// (100 / 500 / 900 m) of the focus rather than across the whole near ring. Measured on the sample's densest
        /// tiles (StreamingConfigTests, radii stretched by the hysteresis margin), whole selections then hold at most
        /// about 0.62 M / 0.97 M / 1.54 M triangles (36 / 56 / 89 MB of meshes): within three times the visible estimate on Mid and High; Low's
        /// worst case, four dense tiles meeting at the focus, is about 1.4 times that until building LOD and
        /// distance bands land (ARCHITECTURE 7.5, W2).
        /// </para>
        /// MaxResidentTiles is about 1.25 to 1.3 times the worst-case selection with the hysteresis margin
        /// (120 / 209 / 334 nodes).
        /// </summary>
        public static StreamingConfig ForTier(int tier)
        {
            switch (tier)
            {
                case TierLow:
                    return new StreamingConfig
                    {
                        Rings = new[]
                        {
                            new LodRing(10, 750, 32), new LodRing(9, 2500, 32), new LodRing(8, 6000, 32),
                            new LodRing(7, 14000, 16), new LodRing(6, 28000, 16), new LodRing(5, 50000, 16),
                        },
                        MaxResidentTiles = 160,
                        CacheBudgetBytes = 30L << 20,
                        DetailRadiusM = 100,
                    };
                case TierMid:
                    return new StreamingConfig
                    {
                        Rings = new[]
                        {
                            new LodRing(10, 1250, 64), new LodRing(9, 4000, 32), new LodRing(8, 10000, 32),
                            new LodRing(7, 22000, 16), new LodRing(6, 44000, 16), new LodRing(5, 80000, 16),
                        },
                        MaxResidentTiles = 272,
                        CacheBudgetBytes = 60L << 20,
                        DetailRadiusM = 500,
                    };
                case TierHigh:
                    return new StreamingConfig
                    {
                        Rings = new[]
                        {
                            new LodRing(10, 1750, 64), new LodRing(9, 6000, 32), new LodRing(8, 14000, 32),
                            new LodRing(7, 30000, 16), new LodRing(6, 60000, 16), new LodRing(5, 120000, 16),
                        },
                        MaxResidentTiles = 420,
                        CacheBudgetBytes = 90L << 20,
                        DetailRadiusM = 900,
                    };
                default:
                    throw new ArgumentOutOfRangeException(nameof(tier), "tier must be 0 (Low), 1 (Mid) or 2 (High)");
            }
        }

        /// <summary>A deep copy (rings array included).</summary>
        public StreamingConfig Clone()
        {
            return new StreamingConfig
            {
                Rings = Rings == null ? null : (LodRing[])Rings.Clone(), MaxResidentTiles = MaxResidentTiles,
                CacheBudgetBytes = CacheBudgetBytes, DetailRadiusM = DetailRadiusM, HysteresisFraction = HysteresisFraction,
            };
        }

        /// <summary>Finest ring level (the deepest level the selector ever emits).</summary>
        public int FinestLevel
        {
            get { return Rings[0].Level; }
        }

        /// <summary>Coarsest ring level.</summary>
        public int CoarsestLevel
        {
            get { return Rings[Rings.Length - 1].Level; }
        }

        /// <summary>The coarsest ring's radius: nothing farther than this from the focus is selected.</summary>
        public double ViewRadiusM
        {
            get { return Rings[Rings.Length - 1].RadiusM; }
        }

        /// <summary>Throws <see cref="ArgumentException"/> unless the rings are usable.</summary>
        public void Validate()
        {
            if (Rings == null || Rings.Length == 0) throw new ArgumentException("streaming config needs at least one LOD ring");
            for (int k = 0; k < Rings.Length; k++)
            {
                LodRing r = Rings[k];
                if (r.Level < 0 || r.Level > TileId.MaxLevel) throw new ArgumentException("ring level " + r.Level + " outside the quadtree");
                if (!(r.RadiusM > 0) || double.IsInfinity(r.RadiusM)) throw new ArgumentException("ring radius must be positive and finite");
                if (r.TerrainQuads < 0) throw new ArgumentException("ring terrain quads must not be negative");
                if (k > 0 && r.Level >= Rings[k - 1].Level) throw new ArgumentException("rings must be listed finest level first");
                if (k > 0 && r.RadiusM <= Rings[k - 1].RadiusM) throw new ArgumentException("ring radii must grow with coarser levels");
            }
            if (MaxResidentTiles < 1) throw new ArgumentException("MaxResidentTiles must be positive");
            if (CacheBudgetBytes < 0) throw new ArgumentException("CacheBudgetBytes must not be negative");
            if (!(DetailRadiusM >= 0)) throw new ArgumentException("DetailRadiusM must not be negative or NaN");
            if (!(HysteresisFraction >= 0) || HysteresisFraction > 1) throw new ArgumentException("HysteresisFraction must lie in [0, 1]");
        }

        /// <summary>
        /// Radius within which an area of level <paramref name="level"/> - 1 is split into areas of
        /// <paramref name="level"/>: that ring's radius; for a level coarser than every ring, the view radius; for a
        /// level between two listed rings, the next finer ring's radius (the level is then mostly skipped); for a
        /// level finer than every ring, 0 (never split).
        /// </summary>
        public double SplitRadius(int level)
        {
            if (level > Rings[0].Level) return 0;
            for (int k = 0; k < Rings.Length; k++)
                if (Rings[k].Level <= level)
                    return Rings[k].Level == level || k == 0 ? Rings[k].RadiusM : Rings[k - 1].RadiusM;
            return ViewRadiusM;
        }

        /// <summary>The ring of <paramref name="level"/>, or the nearest coarser one (the coarsest when none).</summary>
        public LodRing RingFor(int level)
        {
            for (int k = 0; k < Rings.Length; k++)
                if (Rings[k].Level <= level) return Rings[k];
            return Rings[Rings.Length - 1];
        }

        /// <summary>
        /// The terrain decimation step (a power of two, in source samples) for drawing <paramref name="area"/> from
        /// <paramref name="source"/> whose height grid has <paramref name="sourceGridN"/> samples per side: the
        /// area's share of the source grid divided by the ring's <see cref="LodRing.TerrainQuads"/>, at least 1.
        /// </summary>
        public int TerrainStep(TileId area, TileId source, int sourceGridN)
        {
            int depth = area.Level - source.Level;
            int quads = sourceGridN - 1;
            if (depth < 0 || quads < 1) return 1;
            int cropQuads = depth >= 31 ? 0 : quads >> depth;
            int target = RingFor(area.Level).TerrainQuads;
            if (cropQuads <= 1 || target <= 0 || target >= cropQuads) return 1;
            int step = 1;
            while (step * 2 * target <= cropQuads) step *= 2;
            return step;
        }
    }
}
