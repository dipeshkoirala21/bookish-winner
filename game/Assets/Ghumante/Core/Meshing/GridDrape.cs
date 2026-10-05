using System;
using Ghumante.Core.Data;

namespace Ghumante.Core.Meshing
{
    /// <summary>
    /// Drapes flat triangles exactly onto a rendered terrain surface: each triangle is clipped against every terrain
    /// triangle of the sampler's <see cref="TerrainGrid"/> it overlaps (both halves of each quad, split along the
    /// (i, j)-(i+1, j+1) diagonal), and each piece is placed on that terrain facet's plane plus a lift, with the
    /// terrain's own smooth normals. The result lies exactly <c>lift</c> above what <see cref="TerrainMesher"/>
    /// draws, everywhere, not only at its vertices. Outside the grid square the surface is the clamped edge
    /// extension (as <see cref="TileHeightSampler.TryHeightClamped"/>), so geometry overhanging a tile edge
    /// still gets deterministic heights. Per-vertex UVs and colours are interpolated linearly across each input
    /// triangle. Allocation-free (thread-static scratch) apart from <see cref="MeshData"/> growth.
    /// </summary>
    internal static class GridDrape
    {
        private const int MaxPoly = 16;

        private sealed class Scratch
        {
            public readonly float[] N00 = new float[3], N10 = new float[3], N01 = new float[3], N11 = new float[3];
            public bool HasCorners;

            // Clip polygons in grid coordinates with barycentric weights (w1, w2) of the input triangle.
            public readonly double[] Ax = new double[MaxPoly], Az = new double[MaxPoly], A1 = new double[MaxPoly], A2 = new double[MaxPoly];
            public readonly double[] Bx = new double[MaxPoly], Bz = new double[MaxPoly], B1 = new double[MaxPoly], B2 = new double[MaxPoly];
        }

        [ThreadStatic] private static Scratch _scratch;

        /// <summary>One vertex of an input triangle: tile-local metres, UV and colour.</summary>
        public struct Vertex
        {
            public double X, Z;
            public float U, V;
            public uint Rgba;

            public Vertex(double x, double z, float u, float v, uint rgba)
            {
                X = x;
                Z = z;
                U = u;
                V = v;
                Rgba = rgba;
            }
        }

        /// <summary>
        /// Drape triangle (a, b, c), given in metres relative to <paramref name="tileX0"/>/<paramref name="tileZ0"/>
        /// (either winding), onto the sampler's surface plus <paramref name="lift"/>, appending up-facing pieces.
        /// With <paramref name="uvs"/> the vertices carry interpolated UVs.
        /// </summary>
        public static void Triangle(double tileX0, double tileZ0, TileHeightSampler s, Vertex a, Vertex b, Vertex c,
                                    float lift, bool uvs, MeshData m)
        {
            TerrainGrid g = s.Grid;
            TileData src = s.SourceTile;
            double cell = g.CellM, ox = tileX0 - g.X0, oz = tileZ0 - g.Z0; // tile-local -> grid-local metres
            double ax = (a.X + ox) / cell, az = (a.Z + oz) / cell, bx = (b.X + ox) / cell, bz = (b.Z + oz) / cell;
            double cx = (c.X + ox) / cell, cz = (c.Z + oz) / cell;
            double area2 = (bx - ax) * (cz - az) - (bz - az) * (cx - ax);
            if (Math.Abs(area2) < 1e-12) return;
            if (area2 < 0)
            {
                // Make it counter-clockwise in plan; swap b and c with their attributes.
                Vertex tv = b;
                b = c;
                c = tv;
                double t = bx;
                bx = cx;
                cx = t;
                t = bz;
                bz = cz;
                cz = t;
            }
            Scratch sc = _scratch ?? (_scratch = new Scratch());
            int q = g.Quads;
            int imin = (int)Math.Floor(Math.Min(ax, Math.Min(bx, cx))), imax = (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cx))) - 1;
            int jmin = (int)Math.Floor(Math.Min(az, Math.Min(bz, cz))), jmax = (int)Math.Ceiling(Math.Max(az, Math.Max(bz, cz))) - 1;
            // Outside the grid the clamped surface is flat across, so one virtual cell row/column suffices there.
            imin = imin < -1 ? -1 : imin > q ? q : imin;
            jmin = jmin < -1 ? -1 : jmin > q ? q : jmin;
            imax = imax < -1 ? -1 : imax > q ? q : imax;
            jmax = jmax < -1 ? -1 : jmax > q ? q : jmax;
            for (int j = jmin; j <= jmax; j++)
            {
                // Virtual cells outside the grid span to infinity: widen their bounds so the clip keeps everything.
                double z0 = j < 0 ? -1e9 : j, z1 = j >= q ? 1e9 : j + 1;
                int j0 = Clamp(j, q), j1 = Clamp(j + 1, q);
                for (int i = imin; i <= imax; i++)
                {
                    sc.HasCorners = false;
                    double x0 = i < 0 ? -1e9 : i, x1 = i >= q ? 1e9 : i + 1;
                    int i0 = Clamp(i, q), i1 = Clamp(i + 1, q);
                    bool virt = i < 0 || i >= q || j < 0 || j >= q;
                    for (int half = 0; half < (virt ? 1 : 2); half++)
                    {
                        int n = Seed(sc, ax, az, bx, bz, cx, cz);
                        if (virt)
                        {
                            // Outside the grid: the clamped surface is a single plane per virtual cell; clip to the box.
                            n = Clip(sc, n, x0, z0, x1, z0, true);
                            if (n >= 3) n = Clip(sc, n, x1, z0, x1, z1, false);
                            if (n >= 3) n = Clip(sc, n, x1, z1, x0, z1, true);
                            if (n >= 3) n = Clip(sc, n, x0, z1, x0, z0, false);
                        }
                        else if (half == 0)
                        {
                            // South-east triangle (i,j) (i+1,j) (i+1,j+1), counter-clockwise.
                            n = Clip(sc, n, i, j, i + 1, j, true);
                            if (n >= 3) n = Clip(sc, n, i + 1, j, i + 1, j + 1, false);
                            if (n >= 3) n = Clip(sc, n, i + 1, j + 1, i, j, true);
                        }
                        else
                        {
                            // North-west triangle (i,j) (i+1,j+1) (i,j+1).
                            n = Clip(sc, n, i, j, i + 1, j + 1, true);
                            if (n >= 3) n = Clip(sc, n, i + 1, j + 1, i, j + 1, false);
                            if (n >= 3) n = Clip(sc, n, i, j + 1, i, j, true);
                        }
                        if (n < 3) continue;
                        // Result is in A after an even number of passes (4 for the box) and in B after 3.
                        bool inA = virt;
                        double[] px = inA ? sc.Ax : sc.Bx, pz = inA ? sc.Az : sc.Bz, p1 = inA ? sc.A1 : sc.B1, p2 = inA ? sc.A2 : sc.B2;
                        n = Compact(px, pz, p1, p2, n, 1e-4 / cell);
                        if (n < 3 || PolyArea(px, pz, n) < 1e-10) continue;

                        float h00 = g.VertexHeight(src, j0, i0), h10 = g.VertexHeight(src, j0, i1);
                        float h01 = g.VertexHeight(src, j1, i0), h11 = g.VertexHeight(src, j1, i1);
                        double dhdu, dhdv;
                        if (half == 0)
                        {
                            dhdu = h10 - (double)h00;
                            dhdv = h11 - (double)h10;
                        }
                        else
                        {
                            dhdu = h11 - (double)h01;
                            dhdv = h01 - (double)h00;
                        }
                        double fu0 = virt ? 0 : i, fv0 = virt ? 0 : j;
                        if (virt)
                        {
                            // Constant across the clamped direction(s); linear along the edge it extends.
                            dhdu = i0 == i1 ? 0 : dhdu;
                            dhdv = j0 == j1 ? 0 : dhdv;
                        }
                        // The cell's corner normals (the terrain mesh's vertex normals), blended per vertex below
                        // exactly as TerrainGrid.SmoothNormal does; virtual cells use SmoothNormal's clamping.
                        if (!virt && !sc.HasCorners)
                        {
                            CornerNormal(g, src, j, i, sc.N00);
                            CornerNormal(g, src, j, i + 1, sc.N10);
                            CornerNormal(g, src, j + 1, i, sc.N01);
                            CornerNormal(g, src, j + 1, i + 1, sc.N11);
                            sc.HasCorners = true;
                        }
                        m.Reserve(n, 3 * (n - 2));
                        int first = m.VertexCount;
                        for (int k = 0; k < n; k++)
                        {
                            double gx = px[k], gz = pz[k];
                            double fu = virt ? Clamp01(gx - i0) : gx - fu0, fv = virt ? Clamp01(gz - j0) : gz - fv0;
                            if (virt)
                            {
                                fu = i0 == i1 ? 0 : fu;
                                fv = j0 == j1 ? 0 : fv;
                            }
                            float y = (float)(h00 + fu * dhdu + fv * dhdv) + lift;
                            float nx, ny, nz;
                            if (virt) g.SmoothNormal(src, gx, gz, out nx, out ny, out nz);
                            else Blend(sc, half, fu, fv, out nx, out ny, out nz);
                            double w1 = p1[k], w2 = p2[k], w0 = 1 - w1 - w2;
                            uint col = Blend(a.Rgba, b.Rgba, c.Rgba, w0, w1, w2);
                            float lx = (float)(gx * cell - ox), lz = (float)(gz * cell - oz);
                            if (uvs)
                                m.AddVertex(lx, y, lz, nx, ny, nz, col, (float)(w0 * a.U + w1 * b.U + w2 * c.U), (float)(w0 * a.V + w1 * b.V + w2 * c.V));
                            else m.AddVertex(lx, y, lz, nx, ny, nz, col);
                        }
                        // Counter-clockwise from above -> reversed for Unity's front face pointing up. Slivers that
                        // collapse when rounded to float are dropped (they cover no area).
                        for (int k = 1; k + 1 < n; k++)
                            if (UpFloat(m, first, first + k + 1, first + k) > 0) m.AddTriangle(first, first + k + 1, first + k);
                    }
                }
            }
        }

        private static void CornerNormal(TerrainGrid g, TileData src, int k, int l, float[] n)
        {
            double gx, gz;
            g.VertexGradient(src, k, l, out gx, out gz);
            TileHeightSampler.FacetNormal(gx, gz, out n[0], out n[1], out n[2]);
        }

        /// <summary>Vertex normals of the cell's terrain triangle blended barycentrically (as
        /// <see cref="TerrainGrid.SmoothNormal"/>), at fractional cell position (fu, fv).</summary>
        private static void Blend(Scratch sc, int half, double fu, double fv, out float nx, out float ny, out float nz)
        {
            float[] b;
            double w0, w1, w2;
            if (half == 0)
            {
                // South-east: (i,j), (i+1,j), (i+1,j+1).
                b = sc.N10;
                w0 = 1 - fu;
                w1 = fu - fv;
                w2 = fv;
            }
            else
            {
                // North-west: (i,j), (i,j+1), (i+1,j+1).
                b = sc.N01;
                w0 = 1 - fv;
                w1 = fv - fu;
                w2 = fu;
            }
            double x = w0 * sc.N00[0] + w1 * b[0] + w2 * sc.N11[0];
            double y = w0 * sc.N00[1] + w1 * b[1] + w2 * sc.N11[1];
            double z = w0 * sc.N00[2] + w1 * b[2] + w2 * sc.N11[2];
            double inv = 1.0 / Math.Sqrt(x * x + y * y + z * z);
            nx = (float)(x * inv);
            ny = (float)(y * inv);
            nz = (float)(z * inv);
        }

        private static int Seed(Scratch sc, double ax, double az, double bx, double bz, double cx, double cz)
        {
            sc.Ax[0] = ax;
            sc.Az[0] = az;
            sc.A1[0] = 0;
            sc.A2[0] = 0;
            sc.Ax[1] = bx;
            sc.Az[1] = bz;
            sc.A1[1] = 1;
            sc.A2[1] = 0;
            sc.Ax[2] = cx;
            sc.Az[2] = cz;
            sc.A1[2] = 0;
            sc.A2[2] = 1;
            return 3;
        }

        /// <summary>Sutherland–Hodgman step keeping the part left of the directed edge e0 -> e1, from A into B
        /// (<paramref name="aToB"/>) or from B into A.</summary>
        private static int Clip(Scratch sc, int n, double e0x, double e0z, double e1x, double e1z, bool aToB)
        {
            double[] ix = aToB ? sc.Ax : sc.Bx, iz = aToB ? sc.Az : sc.Bz, i1 = aToB ? sc.A1 : sc.B1, i2 = aToB ? sc.A2 : sc.B2;
            double[] ox = aToB ? sc.Bx : sc.Ax, oz = aToB ? sc.Bz : sc.Az, o1 = aToB ? sc.B1 : sc.A1, o2 = aToB ? sc.B2 : sc.A2;
            double ex = e1x - e0x, ez = e1z - e0z;
            int count = 0;
            for (int k = 0; k < n; k++)
            {
                int p = k == 0 ? n - 1 : k - 1;
                double dp = ex * (iz[p] - e0z) - ez * (ix[p] - e0x);
                double dk = ex * (iz[k] - e0z) - ez * (ix[k] - e0x);
                bool inP = dp >= 0, inK = dk >= 0;
                if (inK != inP && count < MaxPoly)
                {
                    double f = dp / (dp - dk);
                    ox[count] = ix[p] + (ix[k] - ix[p]) * f;
                    oz[count] = iz[p] + (iz[k] - iz[p]) * f;
                    o1[count] = i1[p] + (i1[k] - i1[p]) * f;
                    o2[count] = i2[p] + (i2[k] - i2[p]) * f;
                    count++;
                }
                if (inK && count < MaxPoly)
                {
                    ox[count] = ix[k];
                    oz[count] = iz[k];
                    o1[count] = i1[k];
                    o2[count] = i2[k];
                    count++;
                }
            }
            return count;
        }

        /// <summary>Plan orientation of an emitted triangle from its float positions: positive when it faces up.</summary>
        internal static double UpFloat(MeshData m, int a, int b, int c)
        {
            float[] p = m.Positions;
            double ux = p[3 * b] - (double)p[3 * a], uz = p[3 * b + 2] - (double)p[3 * a + 2];
            double vx = p[3 * c] - (double)p[3 * a], vz = p[3 * c + 2] - (double)p[3 * a + 2];
            return uz * vx - ux * vz;
        }

        /// <summary>Drop points of a convex clip result that are within <paramref name="tol"/> (grid units) of a
        /// neighbour or that deviate less than that from the line through their neighbours: they would make
        /// zero-area fan triangles. Returns the new count.</summary>
        private static int Compact(double[] x, double[] z, double[] w1, double[] w2, int n, double tol)
        {
            bool changed = true;
            while (changed && n >= 3)
            {
                changed = false;
                for (int k = 0; k < n && n >= 3; k++)
                {
                    int p = k == 0 ? n - 1 : k - 1, q = k == n - 1 ? 0 : k + 1;
                    double cr = (x[k] - x[p]) * (z[q] - z[p]) - (z[k] - z[p]) * (x[q] - x[p]);
                    double bx = x[q] - x[p], bz = z[q] - z[p], base2 = bx * bx + bz * bz;
                    double dpx = x[k] - x[p], dpz = z[k] - z[p];
                    // Distance of k from the line p-q is |cr| / |pq|; also catch k on top of p or q.
                    bool keep = cr * cr > tol * tol * base2 && dpx * dpx + dpz * dpz > tol * tol &&
                                (x[q] - x[k]) * (x[q] - x[k]) + (z[q] - z[k]) * (z[q] - z[k]) > tol * tol;
                    if (keep) continue;
                    for (int r = k; r < n - 1; r++)
                    {
                        x[r] = x[r + 1];
                        z[r] = z[r + 1];
                        w1[r] = w1[r + 1];
                        w2[r] = w2[r + 1];
                    }
                    n--;
                    changed = true;
                    k--;
                }
            }
            return n;
        }

        private static double PolyArea(double[] x, double[] z, int n)
        {
            double a = 0;
            for (int i = 0, j = n - 1; i < n; j = i++) a += x[j] * z[i] - x[i] * z[j];
            return 0.5 * a;
        }

        private static uint Blend(uint a, uint b, uint c, double w0, double w1, double w2)
        {
            if (a == b && b == c) return a;
            int r = (int)(w0 * MeshColor.R(a) + w1 * MeshColor.R(b) + w2 * MeshColor.R(c) + 0.5);
            int gg = (int)(w0 * MeshColor.G(a) + w1 * MeshColor.G(b) + w2 * MeshColor.G(c) + 0.5);
            int bb = (int)(w0 * MeshColor.B(a) + w1 * MeshColor.B(b) + w2 * MeshColor.B(c) + 0.5);
            return MeshColor.Pack(r, gg, bb, MeshColor.A(a));
        }

        private static int Clamp(int v, int q)
        {
            return v < 0 ? 0 : v > q ? q : v;
        }

        private static double Clamp01(double v)
        {
            return v < 0 ? 0 : v > 1 ? 1 : v;
        }
    }
}
