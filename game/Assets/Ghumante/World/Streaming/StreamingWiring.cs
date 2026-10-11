using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;
using Ghumante.Core.Meshing.Bridges;
using Ghumante.Core.Meshing.Roads;

namespace Ghumante.World.Streaming
{
    /// <summary>
    /// The coordinate frames of the road corridors (docs/W2_DETAIL_CONTRACT.md §3): the roads package's
    /// <see cref="RoadCorridorIndex"/> answers in game metres, which the buildings, ornaments, fauna, sacred and collide
    /// packages consume, while the nature placement asks in tile-local metres (its <see cref="Core.Generators.Placement.TreePlacementOptions.Corridors"/>).
    /// <see cref="TileLocal"/> adapts one to the other. Engine-free; the adapters are cached per decoded tile and
    /// immutable, so worker threads may share them.
    /// </summary>
    public static class CorridorFrames
    {
        private static readonly ConditionalWeakTable<TileData, Shifted> Cache = new ConditionalWeakTable<TileData, Shifted>();
        private static readonly object Lock = new object();

        /// <summary>A factory answering in tile-local metres from one answering in game metres (cached per tile; a
        /// different <paramref name="gameMetres"/> factory for the same tile replaces the cached adapter).</summary>
        public static Func<TileData, IRoadCorridorQuery> TileLocal(Func<TileData, IRoadCorridorQuery> gameMetres)
        {
            if (gameMetres == null) throw new ArgumentNullException(nameof(gameMetres));
            return t =>
            {
                if (t == null) return null;
                IRoadCorridorQuery q = gameMetres(t);
                if (q == null) return null;
                lock (Lock)
                {
                    Shifted s;
                    if (Cache.TryGetValue(t, out s) && ReferenceEquals(s.Inner, q)) return s;
                    if (s != null) Cache.Remove(t);
                    s = new Shifted(q, t.Tile.X0, t.Tile.Z0);
                    Cache.Add(t, s);
                    return s;
                }
            };
        }

        /// <summary>A game-metre corridor query asked in tile-local metres (adds the tile origin).</summary>
        public sealed class Shifted : IRoadCorridorQuery
        {
            [ThreadStatic] private static double[] _x, _z;

            /// <summary>The query in game metres.</summary>
            public readonly IRoadCorridorQuery Inner;

            private readonly double _x0, _z0;

            public Shifted(IRoadCorridorQuery inner, double tileX0, double tileZ0)
            {
                Inner = inner ?? throw new ArgumentNullException(nameof(inner));
                _x0 = tileX0;
                _z0 = tileZ0;
            }

            public double SignedDistance(double x, double z)
            {
                return Inner.SignedDistance(x + _x0, z + _z0);
            }

            public bool Overlaps(double[] x, double[] z, int n, out double depthM)
            {
                if (_x == null || _x.Length < n)
                {
                    _x = new double[Math.Max(16, n * 2)];
                    _z = new double[_x.Length];
                }
                for (int i = 0; i < n; i++)
                {
                    _x[i] = x[i] + _x0;
                    _z[i] = z[i] + _z0;
                }
                return Inner.Overlaps(_x, _z, n, out depthM);
            }
        }
    }

    /// <summary>
    /// Structure records for packs without the RSTR chunk (bridges_flyovers.md §6 open issue): the bridges package
    /// derives them from the W1 tags (<see cref="BridgeStructures.For"/>); this writes them into the decoded tile before
    /// anything is meshed and then rebuilds the tile's cached road layout from them (<see cref="RoadLayout.Rebuild"/>), so
    /// the road mesher lays the carriageway on the same mapped deck points and heights the bridge mesher builds under it,
    /// derived foot structures lose car access, and the ground query (<see cref="TileData.RoadStructureOf"/>), the deck
    /// index and traffic read the same records. The corridor rule (<see cref="RoadWidthModel.UsesFinalCorridor"/>, which
    /// reads the record count) is decided before the records are installed and kept by the rebuild, so the widths stay
    /// the stage-one widths the pack was made for. A pack with RSTR is left alone. Idempotent and thread-safe (locks
    /// the tile).
    /// </summary>
    public static class StructureFallback
    {
        /// <summary>Derive and install the records when the tile has roads and none; returns true when it did.</summary>
        public static bool Apply(TileData t)
        {
            if (t == null || t.Roads.Count == 0 || t.HasRoadStructures) return false;
            lock (t)
            {
                if (t.HasRoadStructures) return false;
                bool finalCorridor = RoadWidthModel.UsesFinalCorridor(t); // before the records exist
                IReadOnlyList<RoadStructureRecord> derived = BridgeStructures.For(t);
                if (derived == null || derived.Count != t.Roads.Count) return false;
                bool any = false;
                for (int i = 0; i < derived.Count; i++)
                {
                    if (derived[i].Kind != RoadStructureKind.None || derived[i].HasHeights)
                    {
                        any = true;
                        break;
                    }
                }
                if (!any) return false; // nothing elevated: keep the tile as it came
                for (int i = 0; i < derived.Count; i++) t.RoadStructures.Add(derived[i]);
                RoadLayout.Rebuild(t, finalCorridor);
                return true;
            }
        }
    }

    /// <summary>
    /// Which LOD of a decorated roundabout (<see cref="Core.Generators.Ornaments.RoundaboutDecorator"/>, LOD 0-3) the
    /// streamer shows at a distance on each tier (roundabouts.md §5: about 60 / 150 / 400 m on High), within the
    /// ornaments' hero budget (16 000 / 4 000 / 1 000 / 200 triangles per hero design). Low never builds LOD0.
    /// </summary>
    public static class OrnamentLod
    {
        /// <summary>Outer radius (m, plan distance from the island's footprint) of LOD 0, 1 and 2 per tier; beyond the
        /// last one LOD 3.</summary>
        public static readonly float[][] RadiiM =
        {
            new[] { 0f, 45f, 150f },
            new[] { 35f, 110f, 300f },
            new[] { 60f, 150f, 400f },
        };

        private static int Tier(int tier)
        {
            return tier <= 0 ? 0 : tier >= 2 ? 2 : 1;
        }

        /// <summary>Bit mask of the LODs built on a tier (Low: 1-3).</summary>
        public static int BuiltMask(int tier)
        {
            return RadiiM[Tier(tier)][0] > 0f ? 0xF : 0xE;
        }

        /// <summary>The LOD wanted at <paramref name="distanceM"/> on a tier.</summary>
        public static int For(int tier, double distanceM)
        {
            float[] r = RadiiM[Tier(tier)];
            for (int l = 0; l < 3; l++)
                if (distanceM < r[l]) return l;
            return 3;
        }
    }

    /// <summary>
    /// The LOD choice of the streamer's bridge and ornament pieces (<see cref="LodPiece"/>): the tier's LOD at the
    /// camera's plan distance from the piece's footprint, held while the distance stays within
    /// <see cref="HysteresisM"/> of a band edge so a rider passing the edge does not flicker, and moved to the nearest
    /// LOD the tile built. Engine-free.
    /// </summary>
    public static class PieceLod
    {
        /// <summary>A shown LOD is kept until the distance passes its band edge by this much.</summary>
        public const double HysteresisM = 6.0;

        /// <summary>The LOD a layer's rule wants at a distance (no hysteresis).</summary>
        public static int Want(int layer, int tier, double distanceM)
        {
            return layer == TileLayers.Bridges ? BridgeOptions.LodFor(tier, distanceM) : OrnamentLod.For(tier, distanceM);
        }

        /// <summary>The LOD to show for a piece: <paramref name="current"/> (-1 none yet) is kept while it lies between the
        /// wanted LODs at <c>distance ± </c><see cref="HysteresisM"/>; the result is a built LOD (-1 when none).</summary>
        public static int Choose(in LodPiece p, int tier, double distanceM, int current)
        {
            int want = Want(p.Layer, tier, distanceM);
            if (current >= 0)
            {
                int near = Want(p.Layer, tier, Math.Max(0.0, distanceM - HysteresisM));
                int far = Want(p.Layer, tier, distanceM + HysteresisM);
                if (current >= near && current <= far && (p.LodMask & (1 << current)) != 0) return current;
            }
            return p.Nearest(want);
        }
    }
}
