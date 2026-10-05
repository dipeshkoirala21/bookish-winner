using System;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// Exactly the rendered terrain surface of one area (<see cref="TerrainGrid"/>): heights are interpolated
    /// linearly inside the same triangles <see cref="TerrainMesher"/> draws for the same step, so roads, buildings
    /// and vehicles sit on what is on screen (barycentric agreement well within a millimetre). The sampler of a
    /// whole tile (<c>new TileHeightSampler(tile, step)</c>) and the sampler of any area cropped from it with the
    /// same effective step describe the same surface. <see cref="TryHeight"/> answers inside the area square
    /// (edges included) and false outside or without heights. Allocation-free; immutable, so any thread may use it.
    /// </summary>
    public sealed class TileHeightSampler : IHeightSampler
    {
        private const double EdgeToleranceM = 1e-3;

        private readonly TileData _source;
        private readonly TerrainGrid _grid;
        private readonly bool _valid;
        private readonly double _x0, _z0, _size;

        /// <summary>Full-resolution sampler of a whole tile (step 1).</summary>
        public TileHeightSampler(TileData t) : this(t, 1)
        {
        }

        /// <summary>Sampler of a whole tile decimated by <paramref name="step"/> (as <see cref="TerrainOptions.Step"/>).</summary>
        public TileHeightSampler(TileData source, int step)
            : this(source, source == null ? default(TileId) : source.Tile, step)
        {
        }

        private TileHeightSampler(TileData source, TileId area, int step)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _valid = TerrainGrid.HasHeights(source);
            if (_valid) _grid = TerrainGrid.For(source, area, step);
            _x0 = area.X0;
            _z0 = area.Z0;
            _size = area.Size;
        }

        /// <summary>The sampler matching <c>TerrainMesher.Build(source, area, step)</c>: the area-cropped grid,
        /// answering only inside <paramref name="area"/>.</summary>
        public static TileHeightSampler ForArea(TileData source, TileId area, int step)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new TileHeightSampler(source, area, step);
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

        /// <summary>True when the source has a height grid.</summary>
        public bool HasHeights
        {
            get { return _valid; }
        }

        /// <summary>Vertex spacing of the rendered surface in metres (0 without heights).</summary>
        public double SpacingM
        {
            get { return _valid ? _grid.CellM : 0.0; }
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
            double du, dv;
            h = (float)_grid.Surface(_source, (x - _x0) / _grid.CellM, (z - _z0) / _grid.CellM, out du, out dv);
            FacetNormal(du / _grid.CellM, dv / _grid.CellM, out nx, out ny, out nz);
            return true;
        }

        /// <summary>The smooth shading normal the terrain mesh shows at game (x, z) (vertex normals blended across
        /// the triangle), clamped onto the sampled square. Overlays use it to light exactly like the ground below.</summary>
        public bool TrySmoothNormal(double x, double z, out float nx, out float ny, out float nz)
        {
            if (!_valid)
            {
                nx = 0f;
                ny = 1f;
                nz = 0f;
                return false;
            }
            _grid.SmoothNormal(_source, (x - _x0) / _grid.CellM, (z - _z0) / _grid.CellM, out nx, out ny, out nz);
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
            double du, dv;
            h = (float)_grid.Surface(_source, (x - _x0) / _grid.CellM, (z - _z0) / _grid.CellM, out du, out dv);
            return true;
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
