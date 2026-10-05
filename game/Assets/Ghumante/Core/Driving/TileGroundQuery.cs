using System;
using System.Collections.Generic;
using Ghumante.Core.Data;
using Ghumante.Core.Meshing;

namespace Ghumante.Core.Driving
{
    /// <summary>
    /// <see cref="IGroundQuery"/> over what the world currently draws. The streamer adds an area when its terrain
    /// mesh becomes visible and removes it when the mesh is hidden, with the same source tile and step it meshed
    /// with, so the ground always matches the screen:
    /// <list type="bullet">
    /// <item>Heights and normals come from the <b>finest</b> loaded area containing the point, through the meshing
    /// module's <see cref="TileHeightSampler"/> (the triangles <see cref="TerrainMesher"/> draws for that step;
    /// areas cropped from an ancestor source use <see cref="TileHeightSampler.ForArea"/>).</item>
    /// <item>Roads come from the <see cref="RoadSpatialIndex"/> of exact areas (area == source; cropped areas
    /// draw no roads) on the finest level that has any, including neighbours near a tile edge. A point within
    /// half the ribbon width + <see cref="RoadSpatialIndex.OnRoadMarginM"/> is on the road: the road's surface
    /// group applies and the height is lifted like <see cref="RoadMesher"/> (<see cref="RoadOptions.LiftM"/> plus
    /// the class lift), ramping to the terrain across the margin.</item>
    /// <item>Bridges follow the mesher's straight deck between the lifted ends. Where the deck spans a dip it has
    /// a hard edge and its own slope, and the layered overload (<see cref="ILayeredGroundQuery"/>) picks the deck
    /// or what lies under it from the asker's height.</item>
    /// <item>Off road the surface comes from the biome (<see cref="BiomeGround"/>).</item>
    /// </list>
    /// Not thread safe: use it from one thread (the main thread). Queries do not allocate.
    /// </summary>
    public sealed class TileGroundQuery : ILayeredGroundQuery, IRoadQuery
    {
        /// <summary>Upper bound on any road's half width, used to look across tile edges.</summary>
        public const float MaxRoadHalfWidthM = RoadStyle.MaxWidthM * 0.5f;

        /// <summary>A bridge deck more than this above the asker's height is overhead, not underfoot.</summary>
        public const float DeckStepUpM = 1.5f;

        private sealed class Entry
        {
            public TileId Area;
            public TileData Source;
            public TileHeightSampler Sampler;
            public RoadSpatialIndex Roads; // exact areas with drawn roads only
        }

        private readonly Dictionary<TileId, Entry> _areas = new Dictionary<TileId, Entry>();
        private readonly int[] _levelCount = new int[TileId.MaxLevel + 1];
        private readonly int[] _roadLevelCount = new int[TileId.MaxLevel + 1];
        private readonly int[] _stepForLevel = new int[TileId.MaxLevel + 1];
        private readonly RoadOptions _roadOptions;

        /// <summary>A ground query for roads meshed with <paramref name="roadOptions"/> (null: defaults); pass the
        /// options the world gives <see cref="RoadMesher"/> so lift and drawn trails agree.</summary>
        public TileGroundQuery(RoadOptions roadOptions = null)
        {
            _roadOptions = roadOptions ?? new RoadOptions();
            for (int l = 0; l < _stepForLevel.Length; l++) _stepForLevel[l] = 1;
        }

        /// <summary>The road options lift and road indexes follow.</summary>
        public RoadOptions RoadOptions
        {
            get { return _roadOptions; }
        }

        /// <summary>Number of loaded areas.</summary>
        public int Count
        {
            get { return _areas.Count; }
        }

        /// <summary>Decimation step used by the overloads without one, per area level (default 1). Set it to the
        /// terrain mesher's step for that level.</summary>
        public void SetStep(int level, int step)
        {
            if (level < 0 || level > TileId.MaxLevel) throw new ArgumentOutOfRangeException(nameof(level));
            if (step < 1) throw new ArgumentOutOfRangeException(nameof(step));
            _stepForLevel[level] = step;
        }

        public int StepFor(int level)
        {
            return _stepForLevel[level];
        }

        /// <summary>Adds (or replaces) an exact tile with the step configured for its level; builds its road index.</summary>
        public void Add(TileData tile)
        {
            if (tile == null) throw new ArgumentNullException(nameof(tile));
            Add(tile, _stepForLevel[tile.Tile.Level]);
        }

        /// <summary>Adds (or replaces) an exact tile meshed with <paramref name="step"/>; builds the sampler and road
        /// index here (use <see cref="Add(TileId, TileHeightSampler, RoadSpatialIndex)"/> to build them on a worker).</summary>
        public void Add(TileData tile, int step)
        {
            if (tile == null) throw new ArgumentNullException(nameof(tile));
            if (step < 1) throw new ArgumentOutOfRangeException(nameof(step));
            Add(tile.Tile, new TileHeightSampler(tile, step), BuildRoadIndex(tile));
        }

        /// <summary>Adds (or replaces) <paramref name="area"/> drawn from <paramref name="source"/> (the area itself or
        /// an ancestor, as a streaming <c>SelectedNode</c>) with <paramref name="step"/>. Roads only for exact areas.</summary>
        public void Add(TileData source, TileId area, int step)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (step < 1) throw new ArgumentOutOfRangeException(nameof(step));
            Add(area, TileHeightSampler.ForArea(source, area, step), area == source.Tile ? BuildRoadIndex(source) : null);
        }

        /// <summary>Adds (or replaces) an area with a prebuilt sampler (the one its terrain/road meshes used) and road
        /// index (null: no roads). The sampler must sample <paramref name="area"/>; the index must belong to the
        /// sampler's source, and only an exact area may carry one.</summary>
        public void Add(TileId area, TileHeightSampler sampler, RoadSpatialIndex roads)
        {
            if (sampler == null) throw new ArgumentNullException(nameof(sampler));
            if (sampler.HasHeights && sampler.Grid.Area != area)
                throw new ArgumentException("sampler covers " + sampler.Grid.Area + ", not " + area, nameof(sampler));
            if (roads != null && (roads.Tile != sampler.SourceTile || area != sampler.SourceTile.Tile))
                throw new ArgumentException("a road index needs an exact area of its own tile", nameof(roads));
            Remove(area);
            _areas[area] = new Entry { Area = area, Source = sampler.SourceTile, Sampler = sampler, Roads = roads };
            _levelCount[area.Level]++;
            if (roads != null) _roadLevelCount[area.Level]++;
        }

        /// <summary>The road index <see cref="Add(TileData, int)"/> builds: null when the tile draws no roads.</summary>
        public RoadSpatialIndex BuildRoadIndex(TileData tile)
        {
            if (tile == null) throw new ArgumentNullException(nameof(tile));
            if (tile.Roads.Count == 0) return null;
            var idx = new RoadSpatialIndex(tile, _roadOptions);
            return idx.SegmentCount > 0 ? idx : null;
        }

        public bool Remove(TileId area)
        {
            Entry e;
            if (!_areas.TryGetValue(area, out e)) return false;
            _areas.Remove(area);
            _levelCount[area.Level]--;
            if (e.Roads != null) _roadLevelCount[area.Level]--;
            return true;
        }

        public void Clear()
        {
            _areas.Clear();
            Array.Clear(_levelCount, 0, _levelCount.Length);
            Array.Clear(_roadLevelCount, 0, _roadLevelCount.Length);
        }

        public bool Contains(TileId area)
        {
            return _areas.ContainsKey(area);
        }

        /// <summary>The road index of a loaded area, or null.</summary>
        public RoadSpatialIndex RoadIndexOf(TileId area)
        {
            Entry e;
            return _areas.TryGetValue(area, out e) ? e.Roads : null;
        }

        private static bool InWorld(double x, double z)
        {
            return x >= 0 && z >= 0 && x < TileId.RootSizeM && z < TileId.RootSizeM;
        }

        /// <summary>The finest loaded area containing (x, z) that has heights.</summary>
        private Entry FinestTerrain(double x, double z)
        {
            if (!InWorld(x, z)) return null;
            for (int level = TileId.MaxLevel; level >= 0; level--)
            {
                if (_levelCount[level] == 0) continue;
                Entry e;
                if (_areas.TryGetValue(TileId.At(level, x, z), out e) && e.Sampler.HasHeights) return e;
            }
            return null;
        }

        /// <summary>The area whose terrain answers at (x, z): the finest loaded one containing it.</summary>
        public bool TryFinestArea(double x, double z, out TileId area)
        {
            Entry e = FinestTerrain(x, z);
            area = e != null ? e.Area : default(TileId);
            return e != null;
        }

        /// <summary>Rendered terrain height (no road lift) from the finest loaded area.</summary>
        public bool TryTerrainHeight(double x, double z, out float h)
        {
            Entry e = FinestTerrain(x, z);
            if (e == null)
            {
                h = 0f;
                return false;
            }
            return e.Sampler.TryHeight(x, z, out h);
        }

        /// <summary>The ground at (x, z); on a bridge, the deck (see the layered overload).</summary>
        public bool TrySample(double x, double z, out GroundSample s)
        {
            return TrySample(x, z, float.PositiveInfinity, out s);
        }

        /// <summary>
        /// The ground at (x, z) for someone at height <paramref name="nearY"/>: a bridge deck counts only when it is
        /// at most <see cref="DeckStepUpM"/> above them; otherwise the road or terrain under the bridge answers.
        /// </summary>
        public bool TrySample(double x, double z, float nearY, out GroundSample s)
        {
            s = default(GroundSample);
            Entry e = FinestTerrain(x, z);
            if (e == null) return false;
            float h, nx, ny, nz;
            if (!e.Sampler.TrySample(x, z, out h, out nx, out ny, out nz)) return false;
            s.Height = h;
            s.TerrainHeight = h;
            s.Nx = nx;
            s.Ny = ny;
            s.Nz = nz;
            s.TileLevel = e.Area.Level;
            s.Biome = BiomeAt(x, z, e);
            s.Surface = BiomeGround.Of(s.Biome);

            // A bridge deck within reach of the asker wins; otherwise the roads that are not bridges.
            const float margin = RoadSpatialIndex.OnRoadMarginM;
            RoadHit hit;
            float road = h, deckGrade = 0f;
            bool spans = false;
            bool found = TryNearestRoad(x, z, margin, RoadFlags.Bridge, RoadFlags.None, out hit) && hit.OnRoad
                         && TryRoadHeight(h, ref hit, out road, out spans, out deckGrade) && road <= nearY + DeckStepUpM;
            if (!found)
            {
                found = TryNearestRoad(x, z, margin, RoadFlags.None, RoadFlags.Bridge, out hit) && hit.OnRoad
                        && TryRoadHeight(h, ref hit, out road, out spans, out deckGrade);
                if (!found) return true;
            }

            RoadRecord r = hit.Road;
            s.OnRoad = true;
            s.Height = road;
            if (spans)
            {
                // On a deck spanning a dip, the slope is the deck's, not the valley's under it.
                float gx = deckGrade * hit.DirX, gz = deckGrade * hit.DirZ;
                float inv = 1f / MathF.Sqrt(gx * gx + 1f + gz * gz);
                s.Nx = -gx * inv;
                s.Ny = inv;
                s.Nz = -gz * inv;
            }
            s.RoadClass = r.RoadClass;
            s.RoadSurface = r.Surface;
            s.RoadFlags = r.Flags;
            s.Surface = SurfaceGroups.Of(r.Surface);
            s.RoadDirX = hit.DirX;
            s.RoadDirZ = hit.DirZ;
            s.RoadOffsetM = hit.LateralM;
            s.RoadHalfWidthM = hit.HalfWidthM;
            return true;
        }

        /// <summary>The ribbon lift of a road class, as <see cref="RoadMesher"/>: <c>LiftM + min(priority, 8) ×
        /// ClassLiftStepM</c>.</summary>
        public float RoadLiftM(RoadClass c)
        {
            int prio = RoadStyle.Priority(c);
            return _roadOptions.LiftM + (prio > 8 ? 8 : prio) * _roadOptions.ClassLiftStepM;
        }

        /// <summary>
        /// Height of the road surface at the query point: terrain + the ribbon lift across the road, ramping down to
        /// the terrain over the on-road margin outside the edge (a kerb ramp, not a cliff). On a bridge, the mesher's
        /// straight deck between the lifted heights of the piece's rendered ends, never below the lifted terrain;
        /// <paramref name="spans"/> tells when that deck is above the terrain under it, with its rise per metre along
        /// the road direction in <paramref name="grade"/>. A spanning deck has a hard edge: beside it (in the margin)
        /// this returns false and the ground is whatever lies below.
        /// </summary>
        private bool TryRoadHeight(float terrain, ref RoadHit hit, out float height, out bool spans, out float grade)
        {
            spans = false;
            grade = 0f;
            float lift = RoadLiftM(hit.Road.RoadClass);
            float ramp = 1f;
            if (hit.EdgeDistanceM > 0f) ramp = Math.Max(0f, 1f - hit.EdgeDistanceM / RoadSpatialIndex.OnRoadMarginM);
            float deck = terrain + lift;
            if ((hit.Road.Flags & RoadFlags.Bridge) != 0 && hit.PieceLengthM > 0f)
            {
                Entry e;
                if (_areas.TryGetValue(hit.Tile.Tile, out e))
                {
                    int first, last;
                    RoadSpatialIndex.RenderedRange(hit.Road, out first, out last);
                    int[] p = hit.Road.Points;
                    double ax, az, bx, bz;
                    hit.Tile.LocalToGame(p[2 * first], p[2 * first + 1], out ax, out az);
                    hit.Tile.LocalToGame(p[2 * last], p[2 * last + 1], out bx, out bz);
                    float ha, hb;
                    if (e.Sampler.TryHeightClamped(ax, az, out ha) && e.Sampler.TryHeightClamped(bx, bz, out hb))
                    {
                        float f = Math.Clamp(hit.AlongM / hit.PieceLengthM, 0f, 1f);
                        float line = ha + (hb - ha) * f + lift;
                        if (line > deck)
                        {
                            if (hit.EdgeDistanceM > 0f)
                            {
                                height = terrain;
                                return false;
                            }
                            deck = line;
                            spans = true;
                            grade = (hb - ha) / hit.PieceLengthM;
                        }
                    }
                }
            }
            height = terrain + (deck - terrain) * ramp;
            return true;
        }

        private int FinestRoadLevel()
        {
            for (int level = TileId.MaxLevel; level >= 0; level--)
                if (_roadLevelCount[level] > 0) return level;
            return -1;
        }

        /// <summary>
        /// The drawn road surface nearest to (x, z) within <paramref name="maxDistM"/> of its edge, searched in the
        /// loaded tiles of the finest level that has roads: the tile containing the point and any neighbour within
        /// reach (a road just across a tile edge counts). Exact ties go to the lower tile key.
        /// </summary>
        public bool TryNearestRoad(double x, double z, double maxDistM, out RoadHit hit)
        {
            return TryNearestRoad(x, z, maxDistM, RoadFlags.None, RoadFlags.None, out hit);
        }

        /// <summary>As <see cref="TryNearestRoad(double, double, double, out RoadHit)"/>, filtered by road flags
        /// (<see cref="RoadSpatialIndex.TryNearest(double, double, double, RoadFlags, RoadFlags, out RoadHit)"/>).</summary>
        public bool TryNearestRoad(double x, double z, double maxDistM, RoadFlags mustHave, RoadFlags mustNotHave, out RoadHit hit)
        {
            hit = default(RoadHit);
            if (!InWorld(x, z) || double.IsNaN(maxDistM)) return false;
            int level = FinestRoadLevel();
            if (level < 0) return false;
            double size = TileId.SizeAt(level);
            double r = Math.Max(0.0, maxDistM) + MaxRoadHalfWidthM;
            int n = 1 << level;
            int tx0 = Math.Max(0, (int)Math.Floor((x - r) / size)), tx1 = Math.Min(n - 1, (int)Math.Floor((x + r) / size));
            int tz0 = Math.Max(0, (int)Math.Floor((z - r) / size)), tz1 = Math.Min(n - 1, (int)Math.Floor((z + r) / size));
            bool found = false;
            ulong bestKey = 0;
            for (int ty = tz0; ty <= tz1; ty++)
            for (int tx = tx0; tx <= tx1; tx++)
            {
                Entry e;
                var id = new TileId(level, tx, ty);
                if (!_areas.TryGetValue(id, out e) || e.Roads == null) continue;
                RoadHit h;
                if (!e.Roads.TryNearest(x, z, maxDistM, mustHave, mustNotHave, out h)) continue;
                if (!found || h.EdgeDistanceM < hit.EdgeDistanceM || h.EdgeDistanceM == hit.EdgeDistanceM && id.Key < bestKey)
                {
                    hit = h;
                    bestKey = id.Key;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>Biome from the answering area's source, or the finest coarser area whose source has one.</summary>
        private Biome BiomeAt(double x, double z, Entry terrain)
        {
            if (terrain.Source.Biomes != null) return BiomeGround.Sample(terrain.Source, x, z);
            for (int level = terrain.Area.Level - 1; level >= 0; level--)
            {
                if (_levelCount[level] == 0) continue;
                Entry e;
                if (_areas.TryGetValue(TileId.At(level, x, z), out e) && e.Source.Biomes != null)
                    return BiomeGround.Sample(e.Source, x, z);
            }
            return Biome.None;
        }
    }
}
