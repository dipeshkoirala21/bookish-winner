using System;
using Ghumante.Core.Data;
using Ghumante.Core.Streaming;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// The rendered terrain grid of a quadtree <see cref="Area"/> drawn from a <see cref="Source"/> tile's HGHT grid
    /// (the area itself or an ancestor): the source grid cropped to the area and decimated by <see cref="Step"/>.
    /// Area vertex (row k from the south, column l from the west) is source sample
    /// <c>(J0 + k·Step, I0 + l·Step)</c> at area-local <c>(l·CellM, k·CellM)</c>. Every quad is split along its
    /// (i, j)-(i+1, j+1) diagonal: <see cref="TerrainMesher"/> draws exactly this surface and
    /// <see cref="TileHeightSampler"/> samples it, so ground queries match what is drawn.
    /// <para>
    /// When the area is smaller than one source quad (more than log2(n - 1) levels below the source) the grid is a
    /// single quad whose corner heights are read off the source's full-resolution surface (<see cref="SubSample"/>).
    /// </para>
    /// </summary>
    public readonly struct TerrainGrid
    {
        public readonly TileId Source;
        public readonly TileId Area;

        /// <summary>Samples per side of the source HGHT grid (2^k + 1).</summary>
        public readonly int SourceN;

        /// <summary>Effective decimation in source samples (a power of two; 0 for a sub-sample grid).</summary>
        public readonly int Step;

        /// <summary>Quads per side of the area grid (vertices per side = Quads + 1).</summary>
        public readonly int Quads;

        /// <summary>Source column / row of the area's south-west vertex.</summary>
        public readonly int I0, J0;

        /// <summary>Quad side in metres.</summary>
        public readonly double CellM;

        /// <summary>The area is smaller than one source quad.</summary>
        public readonly bool SubSample;

        private TerrainGrid(TileId source, TileId area, int n, int step, int quads, int i0, int j0, bool sub)
        {
            Source = source;
            Area = area;
            SourceN = n;
            Step = step;
            Quads = quads;
            I0 = i0;
            J0 = j0;
            CellM = TileId.SizeAt(area.Level) / quads;
            SubSample = sub;
        }

        /// <summary>
        /// The grid for drawing <paramref name="area"/> from <paramref name="source"/> with a requested
        /// <paramref name="step"/> (source samples; rounded down to a power of two, at least 1, at most the area's
        /// share of the source grid). Throws when the source has no heights or does not contain the area.
        /// </summary>
        public static TerrainGrid For(TileData source, TileId area, int step)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!HasHeights(source)) throw new ArgumentException("tile " + source.Tile + " has no height grid");
            if (!TileArea.Contains(source.Tile, area))
                throw new ArgumentException("area " + area + " is not inside source tile " + source.Tile);
            int n = source.HeightsN, qn = n - 1;
            int d = area.Level - source.Tile.Level;
            int log = 0;
            while (1 << log < qn) log++;
            if (d > log) return new TerrainGrid(source.Tile, area, n, 0, 1, 0, 0, true);

            int crop = qn >> d;
            int s = FloorPow2(step < 1 ? 1 : step);
            if (s > crop) s = crop;
            int i0 = (area.Tx - (source.Tile.Tx << d)) * crop;
            int j0 = (area.Ty - (source.Tile.Ty << d)) * crop;
            return new TerrainGrid(source.Tile, area, n, s, crop / s, i0, j0, false);
        }

        /// <summary>True when the tile carries a usable height grid.</summary>
        public static bool HasHeights(TileData t)
        {
            return t != null && t.HeightsQ != null && t.HeightsN >= 2 && t.HeightsQ.Length >= t.HeightsN * t.HeightsN;
        }

        /// <summary>The effective step <see cref="For"/> would use (0 for a sub-sample grid).</summary>
        public static int EffectiveStep(TileData source, TileId area, int step)
        {
            return For(source, area, step).Step;
        }

        /// <summary>Vertices per side.</summary>
        public int VertexCountPerSide
        {
            get { return Quads + 1; }
        }

        /// <summary>West edge of the area in game metres.</summary>
        public double X0
        {
            get { return Area.X0; }
        }

        /// <summary>South edge of the area in game metres.</summary>
        public double Z0
        {
            get { return Area.Z0; }
        }

        /// <summary>Height in metres of area vertex (row k, column l), both in [0, Quads].</summary>
        public float VertexHeight(TileData source, int k, int l)
        {
            if (!SubSample) return source.HeightAt(J0 + k * Step, I0 + l * Step);
            double x = Area.X0 + l * CellM, z = Area.Z0 + k * CellM;
            return SourceSurface(source, x, z);
        }

        /// <summary>
        /// Height gradient (dh/dx, dh/dz) at area vertex (row k, column l) from central differences on the source
        /// grid at the step spacing (one-sided on the source tile's border; sub-sample grids difference the source
        /// surface one source quad either side). <see cref="TerrainMesher"/> derives its smooth normals from this.
        /// </summary>
        public void VertexGradient(TileData source, int k, int l, out double gx, out double gz)
        {
            int n = SourceN;
            double srcCell = Source.Size / (n - 1);
            if (!SubSample)
            {
                int i = I0 + l * Step, j = J0 + k * Step, s = Step;
                int il = i - s < 0 ? i : i - s, ir = i + s > n - 1 ? i : i + s;
                int jd = j - s < 0 ? j : j - s, ju = j + s > n - 1 ? j : j + s;
                gx = (source.HeightAt(j, ir) - (double)source.HeightAt(j, il)) / ((ir - il) * srcCell);
                gz = (source.HeightAt(ju, i) - (double)source.HeightAt(jd, i)) / ((ju - jd) * srcCell);
                return;
            }
            double x = Area.X0 + l * CellM, z = Area.Z0 + k * CellM;
            gx = (SourceSurface(source, x + srcCell, z) - (double)SourceSurface(source, x - srcCell, z)) / (2 * srcCell);
            gz = (SourceSurface(source, x, z + srcCell) - (double)SourceSurface(source, x, z - srcCell)) / (2 * srcCell);
        }

        /// <summary>
        /// The smooth terrain normal the mesh shows at area-local fractional grid coordinates (u, v): the vertex
        /// normals of the containing triangle (same diagonal rule as <see cref="Surface"/>) blended barycentrically.
        /// </summary>
        public void SmoothNormal(TileData source, double u, double v, out float nx, out float ny, out float nz)
        {
            int q = Quads;
            if (u < 0) u = 0;
            else if (u > q) u = q;
            if (v < 0) v = 0;
            else if (v > q) v = q;
            int i = (int)Math.Floor(u), j = (int)Math.Floor(v);
            if (i > q - 1) i = q - 1;
            if (j > q - 1) j = q - 1;
            double fu = u - i, fv = v - j;
            // Barycentric weights of (i,j), (i+1,j) or (i,j+1), and (i+1,j+1).
            int k1, l1;
            double w0, w1, w2;
            if (fu >= fv)
            {
                k1 = j;
                l1 = i + 1;
                w0 = 1 - fu;
                w1 = fu - fv;
                w2 = fv;
            }
            else
            {
                k1 = j + 1;
                l1 = i;
                w0 = 1 - fv;
                w1 = fv - fu;
                w2 = fu;
            }
            double ax, az, bx, bz, cx, cz;
            float n0x, n0y, n0z, n1x, n1y, n1z, n2x, n2y, n2z;
            VertexGradient(source, j, i, out ax, out az);
            VertexGradient(source, k1, l1, out bx, out bz);
            VertexGradient(source, j + 1, i + 1, out cx, out cz);
            TileHeightSampler.FacetNormal(ax, az, out n0x, out n0y, out n0z);
            TileHeightSampler.FacetNormal(bx, bz, out n1x, out n1y, out n1z);
            TileHeightSampler.FacetNormal(cx, cz, out n2x, out n2y, out n2z);
            double x = w0 * n0x + w1 * n1x + w2 * n2x, y = w0 * n0y + w1 * n1y + w2 * n2y, z = w0 * n0z + w1 * n1z + w2 * n2z;
            double inv = 1.0 / Math.Sqrt(x * x + y * y + z * z);
            nx = (float)(x * inv);
            ny = (float)(y * inv);
            nz = (float)(z * inv);
        }

        /// <summary>Source sample column of area column l (integer grids only).</summary>
        public int SourceColumn(int l)
        {
            return I0 + l * Step;
        }

        /// <summary>Source sample row of area row k (integer grids only).</summary>
        public int SourceRow(int k)
        {
            return J0 + k * Step;
        }

        /// <summary>
        /// Height of the grid's triangle surface at area-local fractional grid coordinates (u, v) in quads, clamped
        /// to the grid; also the facet's height gradient per quad (dh/du, dh/dv).
        /// </summary>
        public double Surface(TileData source, double u, double v, out double dhdu, out double dhdv)
        {
            int q = Quads;
            if (u < 0) u = 0;
            else if (u > q) u = q;
            if (v < 0) v = 0;
            else if (v > q) v = q;
            int i = (int)Math.Floor(u), j = (int)Math.Floor(v);
            if (i > q - 1) i = q - 1;
            if (j > q - 1) j = q - 1;
            double fu = u - i, fv = v - j;
            double h00 = VertexHeight(source, j, i), h10 = VertexHeight(source, j, i + 1);
            double h01 = VertexHeight(source, j + 1, i), h11 = VertexHeight(source, j + 1, i + 1);
            if (fu >= fv)
            {
                // Triangle (i,j), (i+1,j), (i+1,j+1): the south-east half.
                dhdu = h10 - h00;
                dhdv = h11 - h10;
            }
            else
            {
                // Triangle (i,j), (i+1,j+1), (i,j+1): the north-west half.
                dhdu = h11 - h01;
                dhdv = h01 - h00;
            }
            return h00 + fu * dhdu + fv * dhdv;
        }

        /// <summary>The source tile's full-resolution (step 1) surface at game (x, z), clamped onto the tile.</summary>
        internal static float SourceSurface(TileData t, double x, double z)
        {
            int q = t.HeightsN - 1;
            double cell = t.Tile.Size / q;
            double u = (x - t.Tile.X0) / cell, v = (z - t.Tile.Z0) / cell;
            if (u < 0) u = 0;
            else if (u > q) u = q;
            if (v < 0) v = 0;
            else if (v > q) v = q;
            int i = (int)Math.Floor(u), j = (int)Math.Floor(v);
            if (i > q - 1) i = q - 1;
            if (j > q - 1) j = q - 1;
            double fu = u - i, fv = v - j;
            double h00 = t.HeightAt(j, i), h10 = t.HeightAt(j, i + 1), h01 = t.HeightAt(j + 1, i), h11 = t.HeightAt(j + 1, i + 1);
            if (fu >= fv) return (float)(h00 + fu * (h10 - h00) + fv * (h11 - h10));
            return (float)(h00 + fu * (h11 - h01) + fv * (h01 - h00));
        }

        private static int FloorPow2(int v)
        {
            int p = 1;
            while (p <= v / 2) p *= 2;
            return p;
        }
    }
}
