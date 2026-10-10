using System;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// Terrain-following flat geometry for the junction and marking meshers: triangles and quads draped exactly onto
    /// the rendered terrain when the sampler is a <see cref="TileHeightSampler"/> (so a cap, a ring and the paint on them
    /// lie exactly a lift above what the terrain mesher draws, as the pinned ribbon ends do), else placed at sampled
    /// heights. Every vertex carries UV0 = (material channel, AO) (detail-pass contract §5). Tile-local metres in,
    /// tile-local metres with absolute Y out.
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

        /// <summary>Terrain shading normal at tile-local metres.</summary>
        public void Normal(double lx, double lz, out float nx, out float ny, out float nz)
        {
            if (_ths != null && _ths.TrySmoothNormal(_x0 + lx, _z0 + lz, out nx, out ny, out nz)) return;
            const double e = 1.0;
            double gx = (Height(lx + e, lz) - (double)Height(lx - e, lz)) / (2 * e);
            double gz = (Height(lx, lz + e) - (double)Height(lx, lz - e)) / (2 * e);
            TileHeightSampler.FacetNormal(gx, gz, out nx, out ny, out nz);
        }

        /// <summary>An up-facing triangle <paramref name="lift"/> above the terrain, UV0 = (channel, ao).</summary>
        public void Tri(double ax, double az, double bx, double bz, double cx, double cz, float lift, uint c, MaterialChannel ch, float ao, MeshData m)
        {
            float u = RoadMaterials.U(ch);
            if (Drapes)
            {
                GridDrape.Triangle(_x0, _z0, _ths, new GridDrape.Vertex(ax, az, u, ao, c), new GridDrape.Vertex(bx, bz, u, ao, c),
                                   new GridDrape.Vertex(cx, cz, u, ao, c), lift, true, m);
                return;
            }
            float ya = Height(ax, az) + lift, yb = Height(bx, bz) + lift, yc = Height(cx, cz) + lift;
            float nx, ny, nz;
            Normal((ax + bx + cx) / 3, (az + bz + cz) / 3, out nx, out ny, out nz);
            int v = m.AddVertex((float)ax, ya, (float)az, nx, ny, nz, c, u, ao);
            m.AddVertex((float)bx, yb, (float)bz, nx, ny, nz, c, u, ao);
            m.AddVertex((float)cx, yc, (float)cz, nx, ny, nz, c, u, ao);
            RoadSweep.Tri(m, v, v + 1, v + 2, 0, 1, 0);
        }

        /// <summary>As <see cref="Tri"/> with an AO value per vertex (interpolated across the triangle).</summary>
        public void TriAo(double ax, double az, float aoA, double bx, double bz, float aoB, double cx, double cz, float aoC, float lift, uint c,
                          MaterialChannel ch, MeshData m)
        {
            float u = RoadMaterials.U(ch);
            if (Drapes)
            {
                GridDrape.Triangle(_x0, _z0, _ths, new GridDrape.Vertex(ax, az, u, aoA, c), new GridDrape.Vertex(bx, bz, u, aoB, c),
                                   new GridDrape.Vertex(cx, cz, u, aoC, c), lift, true, m);
                return;
            }
            float ya = Height(ax, az) + lift, yb = Height(bx, bz) + lift, yc = Height(cx, cz) + lift;
            float nx, ny, nz;
            Normal((ax + bx + cx) / 3, (az + bz + cz) / 3, out nx, out ny, out nz);
            int v = m.AddVertex((float)ax, ya, (float)az, nx, ny, nz, c, u, aoA);
            m.AddVertex((float)bx, yb, (float)bz, nx, ny, nz, c, u, aoB);
            m.AddVertex((float)cx, yc, (float)cz, nx, ny, nz, c, u, aoC);
            RoadSweep.Tri(m, v, v + 1, v + 2, 0, 1, 0);
        }

        /// <summary>As <see cref="TriAo"/> with a colour per vertex too (interpolated across the triangle).</summary>
        public void TriAoC(double ax, double az, float aoA, uint cA, double bx, double bz, float aoB, uint cB, double cx, double cz, float aoC, uint cC,
                           float lift, MaterialChannel ch, MeshData m)
        {
            float u = RoadMaterials.U(ch);
            if (Drapes)
            {
                GridDrape.Triangle(_x0, _z0, _ths, new GridDrape.Vertex(ax, az, u, aoA, cA), new GridDrape.Vertex(bx, bz, u, aoB, cB),
                                   new GridDrape.Vertex(cx, cz, u, aoC, cC), lift, true, m);
                return;
            }
            float ya = Height(ax, az) + lift, yb = Height(bx, bz) + lift, yc = Height(cx, cz) + lift;
            float nx, ny, nz;
            Normal((ax + bx + cx) / 3, (az + bz + cz) / 3, out nx, out ny, out nz);
            int v = m.AddVertex((float)ax, ya, (float)az, nx, ny, nz, cA, u, aoA);
            m.AddVertex((float)bx, yb, (float)bz, nx, ny, nz, cB, u, aoB);
            m.AddVertex((float)cx, yc, (float)cz, nx, ny, nz, cC, u, aoC);
            RoadSweep.Tri(m, v, v + 1, v + 2, 0, 1, 0);
        }

        /// <summary>An up-facing quad (a, b, c, d around its perimeter).</summary>
        public void Quad(double ax, double az, double bx, double bz, double cx, double cz, double dx, double dz, float lift, uint c, MaterialChannel ch,
                         float ao, MeshData m)
        {
            Tri(ax, az, bx, bz, cx, cz, lift, c, ch, ao, m);
            Tri(ax, az, cx, cz, dx, dz, lift, c, ch, ao, m);
        }

        /// <summary>A flat disc ring between radii r0 (inner, 0 = full disc) and r1 on the terrain.</summary>
        public void Disc(double cx, double cz, double r0, double r1, int sides, float lift, uint c, MaterialChannel ch, float ao, MeshData m)
        {
            for (int i = 0; i < sides; i++)
            {
                double a0 = 2 * Math.PI * i / sides, a1 = 2 * Math.PI * (i + 1) / sides;
                double c0 = Math.Cos(a0), s0 = Math.Sin(a0), c1 = Math.Cos(a1), s1 = Math.Sin(a1);
                if (r0 <= 0) Tri(cx, cz, cx + r1 * c0, cz + r1 * s0, cx + r1 * c1, cz + r1 * s1, lift, c, ch, ao, m);
                else Quad(cx + r0 * c0, cz + r0 * s0, cx + r1 * c0, cz + r1 * s0, cx + r1 * c1, cz + r1 * s1, cx + r0 * c1, cz + r0 * s1, lift, c, ch, ao, m);
            }
        }

        /// <summary>A ring band between radii r0 and r1 over the angles [a0, a1] (radians), in <paramref name="steps"/>
        /// pieces.</summary>
        public void Arc(double cx, double cz, double r0, double r1, double a0, double a1, int steps, float lift, uint c, MaterialChannel ch, float ao,
                        MeshData m)
        {
            for (int i = 0; i < steps; i++)
            {
                double b0 = a0 + (a1 - a0) * i / steps, b1 = a0 + (a1 - a0) * (i + 1) / steps;
                double c0 = Math.Cos(b0), s0 = Math.Sin(b0), c1 = Math.Cos(b1), s1 = Math.Sin(b1);
                Quad(cx + r0 * c0, cz + r0 * s0, cx + r1 * c0, cz + r1 * s0, cx + r1 * c1, cz + r1 * s1, cx + r0 * c1, cz + r0 * s1, lift, c, ch, ao, m);
            }
        }
    }
}
