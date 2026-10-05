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
    /// <item>W2 (W2_DESIGN 10.3): raised footpaths of the road profiles stand at the kerb height; structure colliders
    /// (<see cref="IStructureGround"/>: plinths, stairs, squares) join the ground when registered, their walkable tops
    /// and ramps carrying anyone whose feet are within <see cref="StepUpM"/> below them, while higher tops and no-climb
    /// boxes block like walls; <see cref="GroundSample.Foot"/> reports the structure material, a paved AREA, the road
    /// surface or the biome, in that order.</item>
    /// </list>
    /// Not thread safe: use it from one thread (the main thread). Queries do not allocate.
    /// </summary>
    public sealed class TileGroundQuery : ILayeredGroundQuery, IRoadQuery, IStructureGround
    {
        /// <summary>Upper bound on any road's reach from its centreline (W2 game carriageway, dual shift and
        /// footpaths), used to look across tile edges.</summary>
        public const float MaxRoadHalfWidthM = 0.5f * RoadStyle.MaxGameWidthM + 4f;

        /// <summary>Auto step-up: a walkable top at most this far above the feet is stepped onto (W2_DESIGN 6.1:
        /// pikha aprons, temple steps).</summary>
        public const float StepUpM = 0.45f;

        /// <summary>Height and radius of the body a blocking box must leave room for.</summary>
        public const float BodyHeightM = 1.2f;

        public const float BodyRadiusM = 0.3f;

        /// <summary>A bridge deck more than this above the asker's height is overhead, not underfoot.</summary>
        public const float DeckStepUpM = 1.5f;

        private sealed class Entry
        {
            public TileId Area;
            public TileData Source;
            public TileHeightSampler Sampler;
            public RoadSpatialIndex Roads; // exact areas with drawn roads only
            public PavingIndex Paving; // exact areas only (null for cropped areas)
        }

        /// <summary>Paved AREA triangles of one tile (squares, courtyards, compounds, car parks) in game metres, bucketed
        /// in a 32 m grid over the tile square.</summary>
        private sealed class PavingIndex
        {
            private const int Cells = 32;
            private double[] _tri = new double[0]; // x0 z0 x1 z1 x2 z2 per triangle
            private FootSurface[] _foot = new FootSurface[0];
            private int[] _start = new int[Cells * Cells + 1], _items = new int[0];
            private double _x0, _z0, _cell;

            public static PavingIndex Build(TileData t)
            {
                var tris = new List<double>();
                var feet = new List<FootSurface>();
                double x0 = t.Tile.X0, z0 = t.Tile.Z0;
                foreach (AreaRecord a in t.Areas)
                {
                    FootSurface f;
                    if (!FootSurfaces.TryOfArea(a.Kind, out f) || a.Indices == null || a.Vertices == null) continue;
                    for (int k = 0; k + 2 < a.Indices.Length; k += 3)
                    {
                        for (int c = 0; c < 3; c++)
                        {
                            int v = a.Indices[k + c];
                            tris.Add(x0 + a.Vertices[2 * v] / 100.0);
                            tris.Add(z0 + a.Vertices[2 * v + 1] / 100.0);
                        }
                        feet.Add(f);
                    }
                }
                var p = new PavingIndex { _tri = tris.ToArray(), _foot = feet.ToArray(), _x0 = x0, _z0 = z0, _cell = t.Tile.Size / Cells };
                int n = p._foot.Length;
                for (int pass = 0; pass < 2; pass++)
                {
                    int[] fill = pass == 1 ? (int[])p._start.Clone() : null;
                    for (int i = 0; i < n; i++)
                    {
                        int cx0, cz0, cx1, cz1;
                        p.Bounds(i, out cx0, out cz0, out cx1, out cz1);
                        for (int cz = cz0; cz <= cz1; cz++)
                        for (int cx = cx0; cx <= cx1; cx++)
                        {
                            if (pass == 0) p._start[cz * Cells + cx + 1]++;
                            else p._items[fill[cz * Cells + cx]++] = i;
                        }
                    }
                    if (pass == 0)
                    {
                        for (int c = 0; c < Cells * Cells; c++) p._start[c + 1] += p._start[c];
                        p._items = new int[p._start[Cells * Cells]];
                    }
                }
                return p;
            }

            private void Bounds(int i, out int cx0, out int cz0, out int cx1, out int cz1)
            {
                int o = 6 * i;
                double minX = Math.Min(_tri[o], Math.Min(_tri[o + 2], _tri[o + 4])), maxX = Math.Max(_tri[o], Math.Max(_tri[o + 2], _tri[o + 4]));
                double minZ = Math.Min(_tri[o + 1], Math.Min(_tri[o + 3], _tri[o + 5])), maxZ = Math.Max(_tri[o + 1], Math.Max(_tri[o + 3], _tri[o + 5]));
                cx0 = CellOf(minX - _x0);
                cx1 = CellOf(maxX - _x0);
                cz0 = CellOf(minZ - _z0);
                cz1 = CellOf(maxZ - _z0);
            }

            private int CellOf(double local)
            {
                int c = (int)Math.Floor(local / _cell);
                return c < 0 ? 0 : c >= Cells ? Cells - 1 : c;
            }

            /// <summary>The paving under (x, z); the last listed area wins where they overlap (a courtyard listed after
            /// the compound that holds it).</summary>
            public bool TryAt(double x, double z, out FootSurface f)
            {
                f = FootSurface.Asphalt;
                if (_foot.Length == 0) return false;
                double lx = x - _x0, lz = z - _z0;
                if (lx < 0 || lz < 0 || lx >= _cell * Cells || lz >= _cell * Cells) return false;
                int c = CellOf(lz) * Cells + CellOf(lx);
                int best = -1;
                for (int k = _start[c], end = _start[c + 1]; k < end; k++)
                {
                    int i = _items[k], o = 6 * i;
                    if (i > best && InTri(x, z, _tri[o], _tri[o + 1], _tri[o + 2], _tri[o + 3], _tri[o + 4], _tri[o + 5])) best = i;
                }
                if (best < 0) return false;
                f = _foot[best];
                return true;
            }

            private static bool InTri(double px, double pz, double ax, double az, double bx, double bz, double cx, double cz)
            {
                double d1 = (px - bx) * (az - bz) - (ax - bx) * (pz - bz);
                double d2 = (px - cx) * (bz - cz) - (bx - cx) * (pz - cz);
                double d3 = (px - ax) * (cz - az) - (cx - ax) * (pz - az);
                bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                return !(neg && pos);
            }
        }

        // Registered structure colliders by tile key, kept sorted by key (deterministic, allocation-free iteration).
        private readonly List<ulong> _structureKeys = new List<ulong>();
        private readonly List<StructureSet> _structures = new List<StructureSet>();

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
            bool exact = sampler.SourceTile != null && area == sampler.SourceTile.Tile;
            _areas[area] = new Entry
            {
                Area = area, Source = sampler.SourceTile, Sampler = sampler, Roads = roads,
                Paving = exact ? PavingIndex.Build(sampler.SourceTile) : null,
            };
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
            _structures.Clear();
            _structureKeys.Clear();
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
            s.Foot = FootSurfaces.OfBiome(s.Biome);
            FootSurface paved;
            if (TryPaving(x, z, out paved))
            {
                s.Foot = paved;
                s.Surface = FootSurfaces.GroupOf(paved);
            }

            // A bridge deck within reach of the asker wins; otherwise the roads that are not bridges.
            const float margin = RoadSpatialIndex.OnRoadMarginM;
            RoadHit hit;
            float road = h, deckGrade = 0f;
            bool spans = false;
            bool found = TryNearestRoad(x, z, margin, RoadFlags.Bridge, RoadFlags.None, out hit) && hit.OnRoad
                         && TryRoadHeight(h, ref hit, out road, out spans, out deckGrade) && road <= nearY + DeckStepUpM;
            if (!found)
            {
                found = TryNearestRoad(x, z, RoadSpatialIndex.MaxFootpathM, RoadFlags.None, RoadFlags.Bridge, out hit)
                        && (hit.OnRoad || hit.OnFootpath);
                if (found && hit.OnFootpath)
                {
                    // A raised footpath: the paver top at kerb height above the ribbon lift, never a ramp.
                    s.Height = h + RoadLiftM(hit.Road) + _roadOptions.KerbHeightM;
                    s.OnFootpath = true;
                    s.Foot = FootSurface.Concrete;
                    s.Surface = SurfaceGroup.Paved;
                    SetRoad(ref s, ref hit);
                    return Structures(x, z, nearY, ref s);
                }
                found = found && TryRoadHeight(h, ref hit, out road, out spans, out deckGrade);
                if (!found) return Structures(x, z, nearY, ref s);
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
            s.Surface = SurfaceGroups.Of(r.Surface);
            s.Foot = FootSurfaces.OfRoad(r.Surface, r.RoadClass);
            SetRoad(ref s, ref hit);
            return Structures(x, z, nearY, ref s);
        }

        private static void SetRoad(ref GroundSample s, ref RoadHit hit)
        {
            RoadRecord r = hit.Road;
            s.RoadClass = r.RoadClass;
            s.RoadSurface = r.Surface;
            s.RoadFlags = r.Flags;
            s.RoadDirX = hit.DirX;
            s.RoadDirZ = hit.DirZ;
            s.RoadOffsetM = hit.LateralM;
            s.RoadHalfWidthM = hit.HalfWidthM;
        }

        /// <summary>Structure colliders over the resolved ground: a walkable top or ramp within reach raises the ground
        /// (and takes its material); a box blocking the body makes the point a wall (false).</summary>
        private bool Structures(double x, double z, float nearY, ref GroundSample s)
        {
            if (_structures.Count == 0) return true;
            float best = s.Height, gx = 0f, gz = 0f;
            FootSurface foot = s.Foot;
            bool found = false, blocked = false;
            float feet = float.IsInfinity(nearY) ? nearY : Math.Max(nearY, s.Height);
            for (int i = 0; i < _structures.Count; i++)
            {
                StructureSet set = _structures[i];
                if (!set.Covers(x, z)) continue;
                set.Query(x, z, feet, StepUpM, BodyHeightM, BodyRadiusM, ref best, ref foot, ref gx, ref gz, ref found, ref blocked);
            }
            if (blocked) return false;
            if (!found || best < s.Height - 0.02f) return true;
            s.Height = best;
            float inv = 1f / MathF.Sqrt(gx * gx + 1f + gz * gz);
            s.Nx = -gx * inv;
            s.Ny = inv;
            s.Nz = -gz * inv;
            s.Foot = foot;
            s.Surface = FootSurfaces.GroupOf(foot);
            s.OnStructure = true;
            s.OnRoad = false;
            s.OnFootpath = false;
            return true;
        }

        // ---- structure colliders (IStructureGround) ----

        /// <summary>Adds (or replaces) a tile's structure colliders (tile-local coordinates, copied).</summary>
        public void Register(ulong tileKey, StructureColliders c)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            var set = new StructureSet(tileKey, c, BodyRadiusM + StructureColliders.SoftMarginM);
            int i = _structureKeys.BinarySearch(tileKey);
            if (i >= 0)
            {
                _structures[i] = set;
                return;
            }
            i = ~i;
            _structureKeys.Insert(i, tileKey);
            _structures.Insert(i, set);
        }

        public void Unregister(ulong tileKey)
        {
            int i = _structureKeys.BinarySearch(tileKey);
            if (i < 0) return;
            _structureKeys.RemoveAt(i);
            _structures.RemoveAt(i);
        }

        /// <summary>Number of tiles with registered structure colliders.</summary>
        public int StructureTileCount
        {
            get { return _structures.Count; }
        }

        /// <summary>True when a structure blocks a body standing at (x, z) with its feet at <paramref name="feetY"/>
        /// (exit placement, spawn checks): a no-climb box or a walkable top too high to step onto spans the body.</summary>
        public bool IsBlocked(double x, double z, float feetY)
        {
            float best = feetY, gx = 0f, gz = 0f;
            FootSurface foot = FootSurface.Asphalt;
            bool found = false, blocked = false;
            for (int i = 0; i < _structures.Count; i++)
            {
                StructureSet set = _structures[i];
                if (!set.Covers(x, z)) continue;
                set.Query(x, z, feetY, StepUpM, BodyHeightM, BodyRadiusM, ref best, ref foot, ref gx, ref gz, ref found, ref blocked);
            }
            return blocked;
        }

        private bool TryPaving(double x, double z, out FootSurface f)
        {
            f = FootSurface.Asphalt;
            int level = FinestRoadLevel();
            if (level < 0) level = TileId.MaxLevel;
            for (int l = level; l >= 0; l--)
            {
                if (_levelCount[l] == 0) continue;
                Entry e;
                if (!_areas.TryGetValue(TileId.At(l, x, z), out e) || e.Paving == null) continue;
                return e.Paving.TryAt(x, z, out f);
            }
            return false;
        }

        /// <summary>The class part of the ribbon lift, as <see cref="RoadMesher"/>: <c>LiftM + min(priority, 8) ×
        /// ClassLiftStepM</c> (without the piece's own rank lift; see <see cref="RoadLiftM(RoadRecord)"/>).</summary>
        public float RoadLiftM(RoadClass c)
        {
            int prio = RoadStyle.Priority(c);
            return _roadOptions.LiftM + (prio > 8 ? 8 : prio) * _roadOptions.ClassLiftStepM;
        }

        /// <summary>The exact ribbon lift of a road piece, as drawn (<see cref="RoadMesher.LiftOf"/>: class lift plus
        /// the piece rank lift).</summary>
        public float RoadLiftM(RoadRecord r)
        {
            return RoadMesher.LiftOf(r, _roadOptions);
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
            float lift = RoadLiftM(hit.Road);
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
