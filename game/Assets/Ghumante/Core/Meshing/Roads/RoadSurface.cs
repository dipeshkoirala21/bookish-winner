using System;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// Terrain-following flat geometry for the junction and marking meshers: triangles and quads draped exactly onto
    /// the rendered terrain when the sampler is a <see cref="TileHeightSampler"/> (as the road ribbons are), else
    /// placed at sampled heights. Tile-local metres in, tile-local metres with absolute Y out.
    /// </summary>
    internal struct RoadSurface
    {
        private readonly IHeightSampler _h;
        private readonly TileHeightSampler _ths;
        private readonly double _x0, _z0, _size;
        private float _last;

        public RoadSurface(TileData t, IHeightSampler h)
        {
            _h = h;
            _ths = h as TileHeightSampler;
            _x0 = t.Tile.X0;
            _z0 = t.Tile.Z0;
            _size = t.Tile.Size;
            _last = 0f;
        }

        public bool Drapes
        {
            get { return _ths != null && _ths.HasHeights; }
        }

        /// <summary>Terrain height at tile-local metres (clamped onto the tile off the sampler).</summary>
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

        /// <summary>An up-facing triangle <paramref name="lift"/> above the terrain.</summary>
        public void Tri(double ax, double az, double bx, double bz, double cx, double cz, float lift, uint c, MeshData m)
        {
            if (Drapes)
            {
                GridDrape.Triangle(_x0, _z0, _ths, new GridDrape.Vertex(ax, az, 0f, 0f, c), new GridDrape.Vertex(bx, bz, 0f, 0f, c),
                                   new GridDrape.Vertex(cx, cz, 0f, 0f, c), lift, false, m);
                return;
            }
            float ya = Height(ax, az) + lift, yb = Height(bx, bz) + lift, yc = Height(cx, cz) + lift;
            MeshKit.Tri(m, ax, ya, az, bx, yb, bz, cx, yc, cz, 0, 1, 0, c);
        }

        /// <summary>An up-facing quad (a, b, c, d around its perimeter).</summary>
        public void Quad(double ax, double az, double bx, double bz, double cx, double cz, double dx, double dz, float lift, uint c, MeshData m)
        {
            Tri(ax, az, bx, bz, cx, cz, lift, c, m);
            Tri(ax, az, cx, cz, dx, dz, lift, c, m);
        }

        /// <summary>A flat disc ring between radii r0 (inner, 0 = full disc) and r1 on the terrain.</summary>
        public void Disc(double cx, double cz, double r0, double r1, int sides, float lift, uint c, MeshData m)
        {
            for (int i = 0; i < sides; i++)
            {
                double a0 = 2 * Math.PI * i / sides, a1 = 2 * Math.PI * (i + 1) / sides;
                double c0 = Math.Cos(a0), s0 = Math.Sin(a0), c1 = Math.Cos(a1), s1 = Math.Sin(a1);
                if (r0 <= 0) Tri(cx, cz, cx + r1 * c0, cz + r1 * s0, cx + r1 * c1, cz + r1 * s1, lift, c, m);
                else Quad(cx + r0 * c0, cz + r0 * s0, cx + r1 * c0, cz + r1 * s0, cx + r1 * c1, cz + r1 * s1, cx + r0 * c1, cz + r0 * s1, lift, c, m);
            }
        }

        /// <summary>A vertical cylinder wall of radius r from (terrain + y0) to (terrain + y1), facing out.</summary>
        public void Wall(double cx, double cz, double r, int sides, float y0, float y1, uint c, MeshData m)
        {
            for (int i = 0; i < sides; i++)
            {
                double a0 = 2 * Math.PI * i / sides, a1 = 2 * Math.PI * (i + 1) / sides;
                double x0 = cx + r * Math.Cos(a0), z0 = cz + r * Math.Sin(a0), x1 = cx + r * Math.Cos(a1), z1 = cz + r * Math.Sin(a1);
                float h0 = Height(x0, z0), h1 = Height(x1, z1);
                double am = 0.5 * (a0 + a1);
                MeshKit.Quad(m, x0, h0 + y0, z0, x1, h1 + y0, z1, x1, h1 + y1, z1, x0, h0 + y1, z0, Math.Cos(am), 0, Math.Sin(am), c);
            }
        }
    }
}
