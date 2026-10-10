using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// Terrain height under the buildings of a tile in tile-local metres (absolute Y), clamped onto the tile for
    /// footprints and projections that overhang its border; the last good value when the sampler has nothing.
    /// </summary>
    internal struct BuildingGround
    {
        private readonly IHeightSampler _h;
        private readonly TileHeightSampler _ths;
        private readonly double _x0, _z0, _size;
        private float _last;

        public BuildingGround(TileData t, IHeightSampler h)
        {
            _h = h;
            _ths = h as TileHeightSampler;
            _x0 = t.Tile.X0;
            _z0 = t.Tile.Z0;
            _size = t.Tile.Size;
            _last = 0f;
        }

        /// <summary>Terrain height at tile-local (lx, lz).</summary>
        public float Height(double lx, double lz)
        {
            float v;
            if (_h.TryHeight(_x0 + lx, _z0 + lz, out v)) return _last = v;
            if (_ths != null)
            {
                if (_ths.TryHeightClamped(_x0 + lx, _z0 + lz, out v)) return _last = v;
            }
            else
            {
                double cx = lx < 0 ? 0 : lx > _size ? _size : lx, cz = lz < 0 ? 0 : lz > _size ? _size : lz;
                if (_h.TryHeight(_x0 + cx, _z0 + cz, out v)) return _last = v;
            }
            return _last;
        }
    }
}
