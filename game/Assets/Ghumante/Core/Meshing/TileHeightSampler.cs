using System;
using System.Threading;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// Exactly the rendered terrain surface of one area (<see cref="TerrainGrid"/>): heights are interpolated
    /// linearly inside the same triangles <see cref="TerrainMesher"/> draws for the same step, so roads, buildings
    /// and vehicles sit on what is on screen (barycentric agreement well within a millimetre). On a detail tile that is
    /// the carved surface (<see cref="TerrainCarve"/>: the terrain cut and filled to meet its level roads, owner playtest at
    /// Gaurighat), the same refined triangles the terrain mesh draws; <see cref="TryNaturalHeight"/> answers the natural
    /// ground the roads' profiles and the cut and fill are measured from. The sampler of a whole tile
    /// (<c>new TileHeightSampler(tile, step)</c>) and the sampler of any area cropped from it with the same effective
    /// step describe the same surface. <see cref="TryHeight"/> answers inside the area square (edges included) and false
    /// outside or without heights. Allocation-free once the carve is built (on first use, cached per tile, area and
    /// step); immutable, so any thread may use it.
    /// </summary>
    public sealed class TileHeightSampler : IHeightSampler
    {
        private const double EdgeToleranceM = 1e-3;

        private readonly TileData _source;
        private readonly TerrainGrid _grid;
        private readonly bool _valid;
        private readonly double _x0, _z0, _size;
        private readonly TileId _area;
        private readonly bool _carveOn;
        private TerrainCarve _carve;
        private int _carveState; // 0 unresolved, 1 resolved

        /// <summary>Full-resolution sampler of a whole tile (step 1).</summary>
        public TileHeightSampler(TileData t) : this(t, 1)
        {
        }

        /// <summary>Sampler of a whole tile decimated by <paramref name="step"/> (as <see cref="TerrainOptions.Step"/>).</summary>
        public TileHeightSampler(TileData source, int step)
            : this(source, source == null ? default(TileId) : source.Tile, step, TileNeighbours.None, true)
        {
        }

        private TileHeightSampler(TileData source, TileId area, int step, TileNeighbours neighbours, bool carve)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _valid = TerrainGrid.HasHeights(source);
            if (_valid) _grid = TerrainGrid.For(source, area, step, neighbours);
            _area = area;
            _x0 = area.X0;
            _z0 = area.Z0;
            _size = area.Size;
            _carveOn = carve;
        }

        /// <summary>The sampler matching <c>TerrainMesher.Build(source, area, step)</c>: the area-cropped grid,
        /// answering only inside <paramref name="area"/>.</summary>
        public static TileHeightSampler ForArea(TileData source, TileId area, int step)
        {
            return ForArea(source, area, step, TileNeighbours.None);
        }

        /// <summary>The sampler matching <c>TerrainMesher.Build(source, area, o)</c> with
        /// <see cref="TerrainOptions.Neighbours"/> = <paramref name="neighbours"/>: heights are the same, and the
        /// smooth normals (<see cref="TrySmoothNormal"/>) match the mesh's border normals too.</summary>
        public static TileHeightSampler ForArea(TileData source, TileId area, int step, TileNeighbours neighbours)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new TileHeightSampler(source, area, step, neighbours, true);
        }

        /// <summary>As <see cref="ForArea(TileData, TileId, int)"/>, carving the road cut and fill only when
        /// <paramref name="carve"/> (the streamer carves the tiles it draws roads on; a terrain-only tile keeps the natural
        /// ground, as its terrain mesh with <see cref="TerrainOptions.Carve"/> off does).</summary>
        public static TileHeightSampler ForArea(TileData source, TileId area, int step, bool carve)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new TileHeightSampler(source, area, step, TileNeighbours.None, carve);
        }

        /// <summary>A sampler of the natural (uncarved) terrain of an area: what the terrain was before any road was cut
        /// into it (diagnostics, the carve's own tests). Everything the game draws and stands on uses the carved
        /// samplers.</summary>
        public static TileHeightSampler Natural(TileData source, TileId area, int step)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new TileHeightSampler(source, area, step, TileNeighbours.None, false);
        }

        /// <summary>The grid this sampler follows (default when the source has no heights).</summary>
        public TerrainGrid Grid
        {
            get { return _grid; }
        }

        public TileData SourceTile
        {
            get { return _source; }
        }

        /// <summary>The area this sampler answers for.</summary>
        public TileId Area
        {
            get { return _area; }
        }

        /// <summary>The grid's effective decimation step (0 for a sub-sample grid or without heights).</summary>
        public int Step
        {
            get { return _valid ? _grid.Step : 0; }
        }

        /// <summary>True when the source has a height grid.</summary>
        public bool HasHeights
        {
            get { return _valid; }
        }

        /// <summary>Vertex spacing of the rendered surface in metres (0 without heights; refined cells of the carve are
        /// finer, <see cref="TerrainCarve.SubM"/>).</summary>
        public double SpacingM
        {
            get { return _valid ? _grid.CellM : 0.0; }
        }

        /// <summary>The road cut and fill of this area (null where nothing is carved: no roads, cropped from an ancestor,
        /// or a natural sampler). Built on first use and shared by every sampler of the same tile, area and step.</summary>
        public TerrainCarve Carve
        {
            get
            {
                if (Volatile.Read(ref _carveState) == 1) return _carve;
                if (!_carveOn || !_valid) return null;
                if (TerrainCarve.Building) return null; // a carve under construction reads the natural ground
                TerrainCarve c = TerrainCarve.For(this);
                _carve = c;
                Volatile.Write(ref _carveState, 1);
                return c;
            }
        }

        /// <summary>True when game (x, z) lies in the sampled square (1 mm tolerance on the edges).</summary>
        public bool Covers(double x, double z)
        {
            return x >= _x0 - EdgeToleranceM && x <= _x0 + _size + EdgeToleranceM &&
                   z >= _z0 - EdgeToleranceM && z <= _z0 + _size + EdgeToleranceM;
        }

        public bool TryHeight(double x, double z, out float h)
        {
            if (!_valid || !Covers(x, z))
            {
                h = 0f;
                return false;
            }
            double gx, gz;
            h = Surface(x, z, out gx, out gz);
            return true;
        }

        /// <summary>The natural (uncarved) terrain at game (x, z): what roads' profiles and the cut and fill are measured
        /// from. False outside the area or without heights.</summary>
        public bool TryNaturalHeight(double x, double z, out float h)
        {
            if (!_valid || !Covers(x, z))
            {
                h = 0f;
                return false;
            }
            double du, dv;
            h = (float)_grid.Surface(_source, (x - _x0) / _grid.CellM, (z - _z0) / _grid.CellM, out du, out dv);
            return true;
        }

        /// <summary>The natural (uncarved) terrain at game (x, z), clamped onto the sampled square. False without
        /// heights.</summary>
        public bool TryNaturalHeightClamped(double x, double z, out float h)
        {
            if (!_valid)
            {
                h = 0f;
                return false;
            }
            double du, dv;
            h = (float)_grid.Surface(_source, (x - _x0) / _grid.CellM, (z - _z0) / _grid.CellM, out du, out dv);
            return true;
        }

        /// <summary>Height and the unit normal of the rendered triangle under game (x, z).</summary>
        public bool TrySample(double x, double z, out float h, out float nx, out float ny, out float nz)
        {
            nx = 0f;
            ny = 1f;
            nz = 0f;
            if (!_valid || !Covers(x, z))
            {
                h = 0f;
                return false;
            }
            double gx, gz;
            h = Surface(x, z, out gx, out gz);
            FacetNormal(gx, gz, out nx, out ny, out nz);
            return true;
        }

        /// <summary>The smooth shading normal the terrain mesh shows at game (x, z) (vertex normals blended across
        /// the triangle; the facet normal on carved sub-quads), clamped onto the sampled square. Overlays use it to light
        /// exactly like the ground below.</summary>
        public bool TrySmoothNormal(double x, double z, out float nx, out float ny, out float nz)
        {
            if (!_valid)
            {
                nx = 0f;
                ny = 1f;
                nz = 0f;
                return false;
            }
            double u = (x - _x0) / _grid.CellM, v = (z - _z0) / _grid.CellM;
            TerrainCarve c = Carve;
            // On the tile border the neighbour-aware natural normal, as the terrain mesh's border vertices show it (the
            // neighbouring tile cannot see this carve).
            const double Edge = 1e-6;
            int q = _grid.Quads;
            if (c != null && u > Edge && v > Edge && u < q - Edge && v < q - Edge && c.Carves(u, v))
            {
                double gx, gz;
                c.Surface(u, v, out gx, out gz);
                FacetNormal(gx, gz, out nx, out ny, out nz);
                return true;
            }
            _grid.SmoothNormal(_source, u, v, out nx, out ny, out nz);
            return true;
        }

        /// <summary>Height at (x, z) clamped onto the sampled square: the nearest edge height for points outside,
        /// so geometry overhanging a tile border still gets a deterministic height. False without heights.</summary>
        public bool TryHeightClamped(double x, double z, out float h)
        {
            if (!_valid)
            {
                h = 0f;
                return false;
            }
            double gx, gz;
            h = Surface(x, z, out gx, out gz);
            return true;
        }

        /// <summary>The drawn surface (carved where the carve refines the grid) and its gradient per metre.</summary>
        private float Surface(double x, double z, out double gx, out double gz)
        {
            double u = (x - _x0) / _grid.CellM, v = (z - _z0) / _grid.CellM;
            TerrainCarve c = Carve;
            if (c != null) return (float)c.Surface(u, v, out gx, out gz);
            double du, dv;
            double h = _grid.Surface(_source, u, v, out du, out dv);
            gx = du / _grid.CellM;
            gz = dv / _grid.CellM;
            return (float)h;
        }

        internal static void FacetNormal(double gx, double gz, out float nx, out float ny, out float nz)
        {
            // Normal of the plane h = f(x, z): (-dh/dx, 1, -dh/dz), normalised.
            double inv = 1.0 / Math.Sqrt(gx * gx + 1.0 + gz * gz);
            nx = (float)(-gx * inv);
            ny = (float)inv;
            nz = (float)(-gz * inv);
        }
    }
}
